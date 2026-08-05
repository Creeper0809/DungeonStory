using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public interface IPlayerStaffCommandSource
{
    CharacterActor SelectedActor { get; }
    bool TrySelectActor(CharacterActor actor, out string message);
    bool TryIssuePriorityWorkCommand(BuildableObject target, out string message);
    bool TryIssueSuppressCommand(CharacterActor target, out string message);
}

public sealed class UnavailablePlayerStaffCommandSource :
    IPlayerStaffCommandSource
{
    public CharacterActor SelectedActor => null;

    public bool TrySelectActor(CharacterActor actor, out string message)
    {
        return Unavailable(out message);
    }

    public bool TryIssuePriorityWorkCommand(
        BuildableObject target,
        out string message)
    {
        return Unavailable(out message);
    }

    public bool TryIssueSuppressCommand(
        CharacterActor target,
        out string message)
    {
        return Unavailable(out message);
    }

    private static bool Unavailable(out string message)
    {
        message = "사장 명령 컨트롤러를 사용할 수 없습니다.";
        return false;
    }
}

public class OwnerCommandController :
    MonoBehaviour,
    IPlayerCombatCommandSource,
    IPlayerStaffCommandSource
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask commandTargetMask = ~0;
    [SerializeField, Min(2f)] private float dragSelectionThresholdPixels = 12f;

    private IStaffDiscontentRuntimeService staffDiscontentRuntimeService;
    private IMainCameraProvider mainCameraProvider;
    private IPlayerInputReader inputReader;
    private IWorldPointerRaycaster pointerRaycaster;
    private IUiPointerBlocker uiPointerBlocker;
    private ICharacterCombatCommandRuntime combatCommandRuntime;
    private ICharacterAiWorldRegistry worldRegistry;
    private IDefenseEngagementRuntime defenseEngagementRuntime;
    private IGameEventBus gameEventBus;
    private readonly OwnerCommandSelectionState selection = new();
    private OwnerCommandDragSelector dragSelector;
    private OwnerCommandInfoFeedBridge infoFeedBridge;
    private CombatCommandType combatInputMode;

    private CharacterActor selectedActor
    {
        get => selection.Primary;
        set => selection.Primary = value;
    }
    private IReadOnlyList<CharacterActor> selectedActors => selection.Actors;
    public CharacterActor SelectedActor => selection.Primary;
    public IReadOnlyList<CharacterActor> SelectedActors => selection.Actors;
    public CombatCommandType CombatInputMode => combatInputMode;
    public bool HasCombatStanceSelection => GetSelectedCommandActors()
        .Any(actor => combatCommandRuntime != null
            && combatCommandRuntime.IsInCombatStance(actor));

    private IStaffDiscontentRuntimeService StaffDiscontentRuntimeService => staffDiscontentRuntimeService
        ?? throw new InvalidOperationException($"{nameof(OwnerCommandController)} requires {nameof(IStaffDiscontentRuntimeService)} injection.");

    [Inject]
    public void ConstructOwnerCommandController(
        IStaffDiscontentRuntimeService staffDiscontentRuntimeService,
        IMainCameraProvider mainCameraProvider,
        IPlayerInputReader inputReader,
        IWorldPointerRaycaster pointerRaycaster,
        IUiPointerBlocker uiPointerBlocker,
        ICharacterCombatCommandRuntime combatCommandRuntime,
        ICharacterAiWorldRegistry worldRegistry,
        IDefenseEngagementRuntime defenseEngagementRuntime,
        IGameEventBus gameEventBus)
    {
        this.staffDiscontentRuntimeService = staffDiscontentRuntimeService
            ?? throw new ArgumentNullException(nameof(staffDiscontentRuntimeService));
        this.mainCameraProvider = mainCameraProvider
            ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        this.inputReader = inputReader
            ?? throw new ArgumentNullException(nameof(inputReader));
        this.pointerRaycaster = pointerRaycaster
            ?? throw new ArgumentNullException(nameof(pointerRaycaster));
        this.uiPointerBlocker = uiPointerBlocker
            ?? throw new ArgumentNullException(nameof(uiPointerBlocker));
        this.combatCommandRuntime = combatCommandRuntime
            ?? throw new ArgumentNullException(nameof(combatCommandRuntime));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.defenseEngagementRuntime = defenseEngagementRuntime
            ?? throw new ArgumentNullException(nameof(defenseEngagementRuntime));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        dragSelector = new OwnerCommandDragSelector(
            selection,
            this.inputReader,
            this.uiPointerBlocker,
            this.mainCameraProvider,
            this.worldRegistry);
        infoFeedBridge = new OwnerCommandInfoFeedBridge(
            this.gameEventBus,
            OnTriggerEvent);
        infoFeedBridge.Enable(isActiveAndEnabled);
    }

    private void Update()
    {
        PruneSelection();
        dragSelector?.Update(
            targetCamera,
            dragSelectionThresholdPixels,
            IsShiftHeld());

        if (selectedActor == null)
        {
            return;
        }

        if (selectedActor.Stats != null && selectedActor.Stats.IsDead)
        {
            return;
        }

        if (RequireInputReader().GetMouseButtonDown(1) && !RequireUiPointerBlocker().IsPointerOverUi())
        {
            TryIssuePriorityWorkCommand();
        }
    }

    private void TryIssuePriorityWorkCommand()
    {
        Camera camera = targetCamera != null ? targetCamera : RequireMainCameraProvider().Camera;
        if (camera == null) return;

        bool hasHit = RequirePointerRaycaster().TryRaycast(
            camera,
            commandTargetMask,
            out RaycastHit2D hit);
        if (HasCombatStanceSelection)
        {
            TryIssueCombatPointerCommand(camera, hasHit ? hit : default, hasHit);
            return;
        }

        if (hasHit)
        {
            InvasionIntruderRuntime intruder =
                hit.collider.GetComponentInParent<InvasionIntruderRuntime>();
            if (intruder != null)
            {
                TryIssueDefenseInterceptCommand(intruder, out _);
                return;
            }

            CharacterActor targetActor = hit.collider.GetComponentInParent<CharacterActor>();
            if (targetActor != null && targetActor != selectedActor)
            {
                TryIssueSuppressCommand(targetActor, out _);
                return;
            }

            BuildableObject target = hit.collider.GetComponentInParent<BuildableObject>();
            if (target != null)
            {
                if (target.BuildingData?.GetCoverAbility() != null)
                {
                    TryIssueCoverMoveCommand(target, out _);
                }
                else
                {
                    TryIssuePriorityWorkCommand(target, out _);
                }
                return;
            }
        }

        if (!TryResolvePointerGridPosition(camera, out Vector2Int gridPosition))
        {
            return;
        }

        TryIssueMoveCommand(gridPosition, out _);
    }

    public void SetCombatInputMode(CombatCommandType mode)
    {
        combatInputMode = mode;
    }

    public bool ToggleSelectedCombatStance(out string message)
    {
        CharacterActor[] actors = GetSelectedCommandActors()
            .Where(actor => actor != null && !actor.IsDead)
            .ToArray();
        if (actors.Length == 0)
        {
            message = "전투 태세를 바꿀 캐릭터를 선택하세요.";
            return false;
        }

        bool enable = actors.Any(actor => !combatCommandRuntime.IsInCombatStance(actor));
        int changed = 0;
        string last = string.Empty;
        foreach (CharacterActor actor in actors)
        {
            if (combatCommandRuntime.SetCombatStance(actor, enable, out string result))
            {
                changed++;
            }

            last = result;
        }

        combatInputMode = CombatCommandType.None;
        message = changed > 0
            ? $"{changed}명 {(enable ? "전투 태세" : "생활 태세")}"
            : last;
        gameEventBus.ShowNotice(
            message,
            changed > 0 ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
        return changed > 0;
    }

    public bool TryReloadSelected(out string message)
    {
        return IssueToCombatSelection(
            (CharacterActor actor, out string result) => combatCommandRuntime.TryIssueReload(actor, out result),
            "재장전",
            out message);
    }

    public bool TrySwitchSelectedWeapons(out string message)
    {
        return IssueToCombatSelection(
            (CharacterActor actor, out string result) => combatCommandRuntime.TryIssueSwitchWeapon(actor, out result),
            "무기 교체",
            out message);
    }

    public bool TrySetSelectedFireMode(CombatFireMode mode, out string message)
    {
        return IssueToCombatSelection(
            (CharacterActor actor, out string result) => combatCommandRuntime.TrySetFireMode(actor, mode, out result),
            mode switch
            {
                CombatFireMode.Rapid => "속사",
                CombatFireMode.Suppressive => "제압",
                _ => "조준"
            },
            out message);
    }

    public bool TrySetSelectedHoldFire(bool holdFire, out string message)
    {
        return IssueToCombatSelection(
            (CharacterActor actor, out string result) => combatCommandRuntime.TrySetHoldFire(actor, holdFire, out result),
            holdFire ? "사격 중지" : "사격 허용",
            out message);
    }

    private void TryIssueCombatPointerCommand(
        Camera camera,
        RaycastHit2D hit,
        bool hasHit)
    {
        CombatCommandType mode = combatInputMode;
        CharacterActor targetCharacter = hasHit
            ? hit.collider.GetComponentInParent<CharacterActor>()
            : null;
        WildlifeActor targetWildlife = hasHit
            ? hit.collider.GetComponentInParent<WildlifeActor>()
            : null;
        BuildableObject targetBuilding = hasHit
            ? hit.collider.GetComponentInParent<BuildableObject>()
            : null;

        if (mode == CombatCommandType.Rescue
            || targetCharacter != null
                && targetCharacter.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            if (targetCharacter == null)
            {
                PublishCombatCommandResult(false, "쓰러진 캐릭터를 정확히 선택하세요.");
                return;
            }

            bool rescued = IssueToCombatSelection(
                (CharacterActor actor, out string result) =>
                    combatCommandRuntime.TryIssueRescue(actor, targetCharacter, out result),
                "구조",
                out string rescueMessage);
            PublishCombatCommandResult(rescued, rescueMessage);
            combatInputMode = CombatCommandType.None;
            return;
        }

        CombatParticipantRef target = targetCharacter != null
            ? new CombatParticipantRef(targetCharacter)
            : targetWildlife != null
                ? new CombatParticipantRef(targetWildlife)
                : default;
        if (target.IsValid
            && mode is CombatCommandType.None
                or CombatCommandType.Attack
                or CombatCommandType.ForceFire)
        {
            bool forceFire = mode == CombatCommandType.ForceFire;
            bool attacked = IssueToCombatSelection(
                (CharacterActor actor, out string result) =>
                    combatCommandRuntime.TryIssueAttack(actor, target, forceFire, out result),
                forceFire ? "강제 사격" : "공격",
                out string attackMessage);
            PublishCombatCommandResult(attacked, attackMessage);
            combatInputMode = CombatCommandType.None;
            return;
        }

        if (mode == CombatCommandType.MoveToCover)
        {
            if (targetBuilding?.BuildingData?.GetCoverAbility() == null)
            {
                PublishCombatCommandResult(false, "엄폐물을 정확히 선택하세요.");
                return;
            }

            bool movedToCover = TryIssueCoverMoveCommand(targetBuilding, out string coverMessage);
            PublishCombatCommandResult(movedToCover, coverMessage);
            combatInputMode = CombatCommandType.None;
            return;
        }

        if (!TryResolvePointerGridPosition(camera, out Vector2Int gridPosition))
        {
            PublishCombatCommandResult(false, "유효한 Grid 칸을 선택하세요.");
            return;
        }

        if (mode == CombatCommandType.ForceFire)
        {
            bool fired = IssueToCombatSelection(
                (CharacterActor actor, out string result) =>
                    combatCommandRuntime.TryIssueForceFireAtCell(actor, gridPosition, out result),
                "강제 사격",
                out string fireMessage);
            PublishCombatCommandResult(fired, fireMessage);
        }
        else
        {
            bool moved = IssueToCombatSelection(
                (CharacterActor actor, out string result) =>
                    combatCommandRuntime.TryIssueMove(
                        actor,
                        GetFormationDestination(gridPosition, GetActorFormationIndex(actor)),
                        out result),
                "전투 이동",
                out string moveMessage);
            PublishCombatCommandResult(moved, moveMessage);
        }

        combatInputMode = CombatCommandType.None;
    }

    private delegate bool CombatActorCommand(CharacterActor actor, out string message);

    private bool IssueToCombatSelection(
        CombatActorCommand issue,
        string actionLabel,
        out string message)
    {
        CharacterActor[] actors = GetSelectedCommandActors()
            .Where(actor => actor != null
                && !actor.IsDead
                && combatCommandRuntime.IsInCombatStance(actor))
            .ToArray();
        if (actors.Length == 0)
        {
            message = "전투 태세인 캐릭터를 선택하세요.";
            return false;
        }

        int succeeded = 0;
        string lastFailure = string.Empty;
        foreach (CharacterActor actor in actors)
        {
            if (issue(actor, out string result))
            {
                succeeded++;
            }
            else
            {
                lastFailure = result;
            }
        }

        message = succeeded > 0
            ? $"{succeeded}명 {actionLabel}"
            : string.IsNullOrWhiteSpace(lastFailure) ? $"{actionLabel} 실패" : lastFailure;
        return succeeded > 0;
    }

    private int GetActorFormationIndex(CharacterActor actor)
    {
        CharacterActor[] actors = GetSelectedCommandActors().ToArray();
        int index = Array.IndexOf(actors, actor);
        return Mathf.Max(0, index);
    }

    private void PublishCombatCommandResult(bool success, string message)
    {
        gameEventBus.ShowNotice(
            message,
            success ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
    }

    public bool TryIssueMoveCommand(Vector2Int destination, out string message)
    {
        CharacterActor[] actors = GetSelectedCommandActors()
            .Where(actor => actor != null && !actor.IsDead)
            .ToArray();
        if (actors.Length == 0)
        {
            message = "먼저 이동할 직원을 선택하세요.";
            return false;
        }

        int moved = 0;
        string lastFailure = string.Empty;
        for (int index = 0; index < actors.Length; index++)
        {
            CharacterActor actor = actors[index];
            if (!actor.TryGetAbility(out AbilityMove move))
            {
                lastFailure = $"{actor.name}: 이동 능력이 없습니다.";
                continue;
            }

            Vector2Int actorDestination = GetFormationDestination(destination, index);
            if (move.TryStartPlayerMove(actorDestination, out string result))
            {
                moved++;
            }
            else
            {
                lastFailure = $"{actor.name}: {result}";
            }
        }

        if (moved <= 0)
        {
            message = string.IsNullOrWhiteSpace(lastFailure)
                ? "선택한 칸으로 이동할 수 없습니다."
                : lastFailure;
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
            return false;
        }

        message = moved > 1
            ? $"{moved}명에게 이동 명령"
            : $"{actors[0].name}: ({destination.x}, {destination.y}) 이동";
        gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.NONE);
        return true;
    }

    private bool TryIssueCoverMoveCommand(BuildableObject cover, out string message)
    {
        if (cover == null)
        {
            message = "엄폐물을 찾을 수 없습니다.";
            return false;
        }

        Grid grid = cover.Grid;
        if (grid == null)
        {
            message = "엄폐물의 그리드를 찾을 수 없습니다.";
            return false;
        }

        int width = Mathf.Max(1, cover.BuildingData?.width ?? 1);
        Vector2Int left = new Vector2Int(cover.centerPos.x - 1, cover.centerPos.y);
        Vector2Int right = new Vector2Int(cover.centerPos.x + width, cover.centerPos.y);
        Vector2Int destination = GetSelectedCommandActors()
            .Where(actor => actor != null)
            .Select(actor => grid.GetXY(actor.transform.position))
            .DefaultIfEmpty(left)
            .Average(position => position.x) <= cover.centerPos.x
                ? left
                : right;
        if (HasCombatStanceSelection)
        {
            return IssueToCombatSelection(
                (CharacterActor actor, out string result) =>
                    combatCommandRuntime.TryIssueMoveToCover(actor, destination, out result),
                "엄폐 이동",
                out message);
        }

        return TryIssueMoveCommand(destination, out message);
    }

    private bool TryResolvePointerGridPosition(Camera camera, out Vector2Int position)
    {
        position = default;
        if (camera == null || worldRegistry == null || !worldRegistry.TryGetGrid(out Grid grid))
        {
            return false;
        }

        Vector3 screenPosition = RequireInputReader().MousePosition;
        screenPosition.z = Mathf.Abs(camera.transform.position.z - grid.OriginPosition.z);
        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        position = grid.GetXY(worldPosition);
        return grid.IsValidGridPos(position);
    }

    private static Vector2Int GetFormationDestination(Vector2Int center, int index)
    {
        if (index <= 0)
        {
            return center;
        }

        int offset = (index + 1) / 2;
        return new Vector2Int(
            center.x + (index % 2 == 1 ? -offset : offset),
            center.y);
    }

    public bool TrySelectActor(CharacterActor actor, out string message)
    {
        return TrySelectActor(actor, additive: false, out message);
    }

    public bool TrySelectActor(
        CharacterActor actor,
        bool additive,
        out string message)
    {
        actor = CharacterActorCollection.GetCanonical(actor);
        if (!IsCommandableActor(actor))
        {
            message = "명령할 직원을 선택할 수 없습니다.";
            return false;
        }

        if (!additive)
        {
            ClearSelection();
        }

        AddSelection(actor);
        selectedActor = actor;
        message = selectedActors.Count > 1
            ? $"명령 대상 {selectedActors.Count}명 선택"
            : $"명령 대상 선택: {actor.name}";
        return true;
    }

    public bool TryIssueDefenseInterceptCommand(
        InvasionIntruderRuntime intruder,
        out string message)
    {
        if (intruder == null || intruder.IntruderActor == null)
        {
            message = "저지할 침입자를 선택할 수 없습니다.";
            return false;
        }

        IDefenseEngagementRuntime runtime = defenseEngagementRuntime;
        if (runtime == null)
        {
            message = "디펜스 교전 시스템이 준비되지 않았습니다.";
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
            return false;
        }

        CharacterActor[] defenders = GetSelectedCommandActors()
            .Where(actor => actor != null && !actor.IsOwner)
            .ToArray();
        if (defenders.Length == 0)
        {
            message = "저지할 경비를 먼저 선택하세요.";
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
            return false;
        }

        int assigned = 0;
        string lastFailure = string.Empty;
        foreach (CharacterActor defender in defenders)
        {
            if (runtime.TryAssignManual(defender, intruder, out string failureReason))
            {
                assigned++;
                defender.Brain?.ClearPathSearchCache();
            }
            else if (!string.IsNullOrWhiteSpace(failureReason))
            {
                lastFailure = failureReason;
            }
        }

        if (assigned <= 0)
        {
            message = string.IsNullOrWhiteSpace(lastFailure)
                ? "선택한 경비를 저지 전선에 배치할 수 없습니다."
                : lastFailure;
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
            return false;
        }

        message = assigned > 1
            ? $"{assigned}명에게 침입자 저지 명령"
            : $"{defenders[0].name}: 침입자 저지 명령";
        gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.NONE);
        return true;
    }

    public bool TryIssuePriorityWorkCommand(BuildableObject target, out string message)
    {
        if (selectedActor == null || (selectedActor.Stats != null && selectedActor.Stats.IsDead))
        {
            message = "먼저 명령할 직원을 선택하세요.";
            return false;
        }

        if (target == null || target.isDestroy)
        {
            message = "명령할 시설을 선택할 수 없습니다.";
            return false;
        }

        if (!selectedActor.TryGetAbility(out AbilityWork work))
        {
            message = "선택한 캐릭터는 작업 능력이 없습니다.";
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
            return false;
        }

        if (!WorkCommandResolver.TryResolveFacilityCommand(selectedActor, target, out WorkTypeId workTypeId, out string errorMessage))
        {
            selectedActor.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Command,
                CharacterActivityOutcomes.Failed,
                $"우선 지정 실패: {errorMessage}",
                target,
                actionId: "command:priority-work",
                reasonCode: "unsupported-facility-command",
                bubbleEligible: true));
            gameEventBus.ShowNotice(errorMessage, NoticeFeedEvent.Grade.WARNING);
            message = errorMessage;
            return false;
        }

        GridPathSearchResult searchResult = selectedActor.Brain != null
            ? selectedActor.Brain.GetPathSearch(selectedActor)
            : null;

        if (!work.TrySetPriorityWorkTarget(target, workTypeId, searchResult, out errorMessage))
        {
            selectedActor.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.Command,
                CharacterActivityOutcomes.Failed,
                $"우선 지정 실패: {errorMessage}",
                target,
                actionId: "command:priority-work",
                reasonCode: "priority-target-rejected",
                bubbleEligible: true));
            gameEventBus.ShowNotice(errorMessage, NoticeFeedEvent.Grade.WARNING);
            message = errorMessage;
            return false;
        }

        message = $"{selectedActor.name}: {target.name} {WorkTaskCatalog.GetDisplayName(workTypeId)} 우선 지정";
        gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.NONE);
        selectedActor.Brain?.ClearPathSearchCache();
        return true;
    }

    public bool TryIssueSuppressCommand(CharacterActor target, out string message)
    {
        if (selectedActor == null || (selectedActor.Stats != null && selectedActor.Stats.IsDead))
        {
            message = "먼저 명령할 직원을 선택하세요.";
            return false;
        }

        if (!WorkCommandResolver.IsSuppressTarget(target, StaffDiscontentRuntimeService.IsRebellionTarget))
        {
            message = "제압할 수 있는 반란 대상이 아닙니다.";
            return false;
        }

        if (!selectedActor.TryGetAbility(out AbilityWork work))
        {
            message = "선택한 캐릭터는 작업 능력이 없습니다.";
            selectedActor.AddActivity(CreateSuppressCommandFailure(
                target,
                message,
                "missing-work-ability"));
            gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.WARNING);
            return false;
        }

        GridPathSearchResult searchResult = selectedActor.Brain != null
            ? selectedActor.Brain.GetPathSearch(selectedActor)
            : null;

        if (!work.TrySetPrioritySuppressTarget(target, searchResult, out string errorMessage))
        {
            selectedActor.AddActivity(CreateSuppressCommandFailure(
                target,
                errorMessage,
                "priority-target-rejected"));
            gameEventBus.ShowNotice(errorMessage, NoticeFeedEvent.Grade.WARNING);
            message = errorMessage;
            return false;
        }

        message = $"{selectedActor.name}: {target.name} 제압 우선 지정";
        gameEventBus.ShowNotice(message, NoticeFeedEvent.Grade.NONE);
        selectedActor.Brain?.ClearPathSearchCache();
        return true;
    }

    private static CharacterActivityEvent CreateSuppressCommandFailure(
        CharacterActor target,
        string message,
        string reasonCode)
    {
        return CharacterActivityEvent.Create(
            CharacterActivityKinds.Command,
            CharacterActivityOutcomes.Failed,
            $"우선 지정 실패: {message}",
            actionId: "command:priority-suppress",
            targetId: target != null
                ? CharacterPersistentIdentity.Require(target).Value
                : string.Empty,
            targetName: target != null ? target.name : string.Empty,
            reasonCode: reasonCode,
            sentiment: -0.7f,
            bubbleEligible: true);
    }

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        CharacterActor actor = eventType.Target as CharacterActor;
        if (actor != null)
        {
            TrySelectActor(actor, IsShiftHeld(), out _);
        }
    }

    private void OnEnable()
    {
        infoFeedBridge?.Enable(isActiveAndEnabled);
    }

    private void OnDisable()
    {
        infoFeedBridge?.Disable();
        ClearSelection();
        dragSelector?.Reset();
    }

    public int SelectActorsInScreenRect(Vector2 start, Vector2 end, bool additive)
    {
        int added = dragSelector?.SelectActorsInScreenRect(
            targetCamera,
            start,
            end,
            additive) ?? 0;
        if (added > 0)
        {
            gameEventBus.ShowNotice(
                $"명령 대상 {selectedActors.Count}명 선택",
                NoticeFeedEvent.Grade.NONE);
        }

        return added;
    }

    private IEnumerable<CharacterActor> GetSelectedCommandActors() =>
        selection.GetCommandActors();

    private static bool IsCommandableActor(CharacterActor actor) =>
        OwnerCommandSelectionState.IsCommandable(actor);

    private bool AddSelection(CharacterActor actor) => selection.Add(actor);

    private void ClearSelection() => selection.Clear();

    private void PruneSelection() => selection.Prune();

    private bool IsShiftHeld()
    {
        return inputReader != null
            && (inputReader.GetKey(KeyCode.LeftShift)
                || inputReader.GetKey(KeyCode.RightShift));
    }

    private IMainCameraProvider RequireMainCameraProvider() => mainCameraProvider
        ?? throw new InvalidOperationException($"{nameof(OwnerCommandController)} requires {nameof(IMainCameraProvider)} injection.");

    private IPlayerInputReader RequireInputReader() => inputReader
        ?? throw new InvalidOperationException($"{nameof(OwnerCommandController)} requires {nameof(IPlayerInputReader)} injection.");

    private IWorldPointerRaycaster RequirePointerRaycaster() => pointerRaycaster
        ?? throw new InvalidOperationException($"{nameof(OwnerCommandController)} requires {nameof(IWorldPointerRaycaster)} injection.");

    private IUiPointerBlocker RequireUiPointerBlocker() => uiPointerBlocker
        ?? throw new InvalidOperationException($"{nameof(OwnerCommandController)} requires {nameof(IUiPointerBlocker)} injection.");
}
