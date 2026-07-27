using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class CaptivityRuntime :
    ICaptivityRuntime,
    ICaptiveLaborQuery,
    ICaptivityCommandService,
    ICaptivityEscortRuntime,
    ICaptivityEscapeRuntime,
    ICharacterCarePriorityQuery,
    IStartable,
    ITickable,
    IDisposable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CaptivityRuntime.Tick");

    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly ICombatEquipmentRuntime combatEquipment;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IGridSystemProvider gridProvider;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IGameDataProvider gameDataProvider;
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly IDoorAccessCommandService doorAccessCommands;
    private readonly IDoorAccessSubjectRegistry doorSubjectRegistry;
    private readonly CaptivityInteractionRegistry interactions;
    private readonly IGameClock gameClock;
    private readonly IGameEventBus gameEventBus;
    private readonly IRandomStream random;
    private readonly List<CaptiveState> captives = new List<CaptiveState>();
    private readonly List<CaptivePolicyData> policies = new List<CaptivePolicyData>();
    private readonly Dictionary<string, Transform> carriedParents =
        new Dictionary<string, Transform>(StringComparer.Ordinal);
    private IDisposable downedSubscription;
    private IDisposable recoveredSubscription;
    private IDisposable deathSubscription;
    private IDisposable invasionSubscription;
    private int captureSequence;
    private int policySequence;

    public CaptivityRuntime(
        ICharacterAiWorldRegistry worldRegistry,
        ICharacterBodyHealthRuntime bodyHealth,
        ICombatEquipmentRuntime combatEquipment,
        IWorldItemStackRuntime itemRuntime,
        IGridSystemProvider gridProvider,
        IGridPathSearchBroker pathSearchBroker,
        IRoomLayoutCache roomLayoutCache,
        IGameDataProvider gameDataProvider,
        IDoorAccessQuery doorAccessQuery,
        IDoorAccessCommandService doorAccessCommands,
        IDoorAccessSubjectRegistry doorSubjectRegistry,
        CaptivityInteractionRegistry interactions,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IGameEventBus gameEventBus)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.combatEquipment = combatEquipment
            ?? throw new ArgumentNullException(nameof(combatEquipment));
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.doorAccessQuery = doorAccessQuery
            ?? throw new ArgumentNullException(nameof(doorAccessQuery));
        this.doorAccessCommands = doorAccessCommands
            ?? throw new ArgumentNullException(nameof(doorAccessCommands));
        this.doorSubjectRegistry = doorSubjectRegistry
            ?? throw new ArgumentNullException(nameof(doorSubjectRegistry));
        this.interactions = interactions
            ?? throw new ArgumentNullException(nameof(interactions));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        random = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("captivity.security");
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        AddBuiltInPolicies();
    }

    public IReadOnlyList<CaptiveState> Captives =>
        captives.Select(state => state.Clone()).ToArray();

    public IReadOnlyList<CaptivePolicyData> Policies =>
        policies.Select(policy => policy.Clone()).ToArray();

    public int GetCarePriority(string persistentCharacterId)
    {
        CaptiveState captive = captives.FirstOrDefault(candidate =>
            string.Equals(
                candidate?.captiveId,
                persistentCharacterId,
                StringComparison.Ordinal));
        return captive?.carePriorityUnlocked == true ? 100 : 0;
    }

    public bool IsCareSubject(string persistentCharacterId)
    {
        return captives.Any(candidate =>
            candidate?.IsActive == true
            && string.Equals(
                candidate.captiveId,
                persistentCharacterId,
                StringComparison.Ordinal));
    }

    public void Start()
    {
        downedSubscription =
            gameEventBus.Subscribe<CharacterBodyHealthRuntime.CharacterDownedEvent>(
                gameEvent => OnCharacterDowned(gameEvent.Actor));
        recoveredSubscription =
            gameEventBus.Subscribe<CharacterBodyHealthRuntime.CharacterRecoveredEvent>(
                gameEvent => OnCharacterRecovered(gameEvent.Actor));
        deathSubscription = gameEventBus.Subscribe<CharacterDeathEvent>(OnCharacterDeath);
        invasionSubscription =
            gameEventBus.Subscribe<InvasionStartedEvent>(_ => OnInvasionStarted());

        foreach (CharacterActor actor in worldRegistry.AllCharacters)
        {
            if (IsEligibleDownedIntruder(actor))
            {
                EnsureCandidate(actor);
            }
        }
    }

    public void Dispose()
    {
        downedSubscription?.Dispose();
        recoveredSubscription?.Dispose();
        deathSubscription?.Dispose();
        invasionSubscription?.Dispose();
        downedSubscription = null;
        recoveredSubscription = null;
        deathSubscription = null;
        invasionSubscription = null;
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        if (gameClock.IsPaused || gameClock.DeltaTime <= 0f)
        {
            return;
        }

        for (int index = 0; index < captives.Count; index++)
        {
            CaptiveState state = captives[index];
            if (state == null
                || !state.IsActive
                || (state.status != CaptivityStatus.Confined
                    && state.status != CaptivityStatus.Labor))
            {
                continue;
            }

            CharacterActor actor = FindActor(state.captiveId);
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            state.health = EstimateHealth(actor);
            TickCarePriority(state, actor);
            RecalculateCaptiveState(state);
            if (gameClock.Time + 0.001f < state.nextSecurityCheckAt)
            {
                continue;
            }

            state.nextSecurityCheckAt = gameClock.Time + 5f;
            if (state.escapeRisk < 65f)
            {
                continue;
            }

            float chance = Mathf.Lerp(
                0.02f,
                0.18f,
                Mathf.InverseLerp(65f, 100f, state.escapeRisk));
            if (state.falseCompliance)
            {
                chance *= 1.4f;
            }

            if (random.Chance(Mathf.Clamp01(chance)))
            {
                TryBeginEscapeAttempt(
                    state,
                    state.falseCompliance
                        ? "거짓 복종 중 훔친 열쇠"
                        : "감방 탈출 시도",
                    out _);
            }
        }
    }

    private void TickCarePriority(CaptiveState state, CharacterActor actor)
    {
        if (state?.carePriorityUnlocked != true
            || actor == null
            || gameClock.Time + 0.001f < state.nextCareSupplyAt)
        {
            return;
        }

        string destinationId = $"captive-care:{state.captiveId}";
        Dictionary<StockCategory, int> foodCost = new Dictionary<StockCategory, int>
        {
            [StockCategory.Food] = 1
        };
        if (itemRuntime.TryConsumeFacilityBuffer(destinationId, foodCost, out _))
        {
            actor.ChangesStat(CharacterCondition.HUNGER, 35f);
            state.health = Mathf.Clamp(state.health + 5f, 0f, 100f);
            state.nextCareSupplyAt = gameClock.Time + 120f;
            state.lastResult = "명성 특혜 식량을 배급받았습니다.";
            return;
        }

        bool deliveryOutstanding = itemRuntime.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && stack.StockCategory == StockCategory.Food
            && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
        if (deliveryOutstanding)
        {
            state.nextCareSupplyAt = gameClock.Time + 5f;
            return;
        }

        bool deliveryRequested = itemRuntime.TryRequestFacilityDelivery(
            StockCategory.Food,
            1,
            state.housingPosition,
            destinationId,
            out int requested,
            out _)
            && requested > 0;
        state.nextCareSupplyAt = gameClock.Time + (deliveryRequested ? 15f : 5f);
    }

    public bool TryGetCaptive(string captiveId, out CaptiveState captive)
    {
        CaptiveState state = FindState(captiveId);
        captive = state?.Clone();
        return captive != null;
    }

    public bool TryGetActor(string captiveId, out CharacterActor actor)
    {
        actor = FindActor(captiveId);
        return actor != null;
    }

    public bool TryGetHousing(string captiveId, out BuildableObject housing)
    {
        CaptiveState state = FindState(captiveId);
        string housingId = state?.housingBuildingId ?? string.Empty;
        housing = worldRegistry.Buildings.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.isDestroy
            && string.Equals(
                GetHousingId(candidate),
                housingId,
                StringComparison.Ordinal));
        return housing != null;
    }

    public bool IsCaptive(string persistentId)
    {
        CaptiveState state = FindState(persistentId);
        return state != null && state.IsActive;
    }

public bool IsWorkAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string reason)
    {
        reason = string.Empty;
        CaptiveState state = FindState(GetCharacterId(actor));
        if (state == null || !state.IsActive)
        {
            return true;
        }

        if (state.status != CaptivityStatus.Labor || !state.CanLabor)
        {
            reason = "이 포로는 현재 노역에 투입할 수 없습니다.";
            return false;
        }

        CaptiveLaborPermission required = GetLaborPermission(workTypeId);
        if (required != CaptiveLaborPermission.None
            && (state.laborPermissions & required) != 0)
        {
            return true;
        }

        reason = $"{WorkTaskCatalog.GetDisplayName(workTypeId)} 노역은 허용되지 않았습니다.";
        return false;
    }

    public bool HasSecureHousing(
        CharacterActor captive,
        out BuildableObject housing,
        out string reason)
    {
        housing = null;
        reason = "사용 가능한 감방이 없습니다.";
        if (captive == null || !gridProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        foreach (BuildableObject candidate in worldRegistry.Buildings
                     .Where(building => building != null
                         && !building.isDestroy
                         && building.BuildingData.GetCaptiveHousingAbility() != null)
                     .OrderBy(building => Manhattan(captive.GetNowXY(), building.centerPos)))
        {
            BuildingCaptiveHousingAbility ability =
                candidate.BuildingData.GetCaptiveHousingAbility();
            string housingId = GetHousingId(candidate);
            int assigned = captives.Count(state =>
                state.IsActive
                && string.Equals(
                    state.housingBuildingId,
                    housingId,
                    StringComparison.Ordinal));
            if (assigned >= ability.capacity)
            {
                reason = "감방 수용 인원이 가득 찼습니다.";
                continue;
            }

            if (!TryGetHousingRoom(grid, candidate, out RoomInstance room)
                || !room.IsUsable)
            {
                reason = "감방이 닫힌 정식 방이 아닙니다.";
                continue;
            }

            GridTraversalContext captiveContext =
                GridTraversalContext.ForCharacter(captive);
            Door escapingDoor = room.Doors
                .OfType<Door>()
                .FirstOrDefault(door =>
                    doorAccessQuery.CanUse(door, captiveContext, out _));
            if (escapingDoor != null)
            {
                reason = "수용 대상이 감방 문을 사용할 수 있습니다.";
                continue;
            }

            if (!TryFindHousingCell(grid, room, candidate, out _))
            {
                reason = "감방 안에 포로를 둘 빈 칸이 없습니다.";
                continue;
            }

            housing = candidate;
            reason = string.Empty;
            return true;
        }

        return false;
    }

    public bool TryOrderCapture(
        CharacterActor subject,
        CharacterActor carrier,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsEligibleDownedIntruder(subject))
        {
            failureReason = "살아 있는 쓰러진 침입자만 포획할 수 있습니다.";
            return false;
        }

        if (carrier == null
            || carrier.IsDead
            || carrier.characterType != CharacterType.NPC
            || carrier.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            failureReason = "포로를 운반할 직원이나 사장이 필요합니다.";
            return false;
        }

        CaptiveState state = EnsureCandidate(subject);
        if (state.status is CaptivityStatus.Escorting
            or CaptivityStatus.Confined
            or CaptivityStatus.Labor
            or CaptivityStatus.Interaction
            or CaptivityStatus.Performer)
        {
            failureReason = "이미 포획 절차가 진행 중입니다.";
            return false;
        }

        doorSubjectRegistry.SetCaptive(state.captiveId, true);
        if (!HasSecureHousing(subject, out BuildableObject housing, out failureReason)
            || !TryGetHousingRoom(
                gridProvider.TryGetGrid(out Grid grid) ? grid : null,
                housing,
                out RoomInstance room)
            || !TryFindHousingCell(grid, room, housing, out Vector2Int housingCell))
        {
            return false;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(carrier);
        bool carrierHasRestraint =
            inventory != null
            && inventory.CountItem(CaptivityItemDefinitions.RestraintsItemId) > 0;
        WorldItemReservedStackQuantity restraintReservation = default;
        Vector2Int pickupPosition = default;
        if (!carrierHasRestraint
            && !itemRuntime.TryReserveStoredItemForDirectPickup(
                carrier,
                CaptivityItemDefinitions.RestraintsItemId,
                1,
                out restraintReservation,
                out pickupPosition,
                out failureReason))
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "창고에 사용할 수 있는 구속구가 없습니다."
                : failureReason;
            return false;
        }

        state.status = CaptivityStatus.Stabilizing;
        state.reservedCarrierId = GetCharacterId(carrier);
        state.housingBuildingId = GetHousingId(housing);
        state.housingPosition = housingCell;
        state.capturePosition = subject.GetNowXY();
        state.restraintStackId = restraintReservation.StackId;
        state.restraintItemId = carrierHasRestraint
            ? CaptivityItemDefinitions.RestraintsItemId
            : restraintReservation.ItemId;
        state.restraintQuantity = 1;
        state.restraintPickupPosition = pickupPosition;
        state.requiredInteractionWork = Mathf.Min(
            30f,
            8f + bodyHealth.GetTotalBleeding(subject) * 40f);
        state.completedInteractionWork = 0f;
        state.lastResult = "현장 안정화 대기";

        AbilityCaptiveEscort escort = AbilityCaptiveEscort.Ensure(carrier);
        if (escort == null)
        {
            FailEscort(state.captiveId, carrier, "호송 행동을 시작할 수 없습니다.");
            failureReason = state.lastResult;
            return false;
        }

        escort.Configure(this, gameClock);
        escort.StartEscort(state.captiveId);
        return true;
    }

    public bool CancelCapture(string captiveId, string reason)
    {
        CaptiveState state = FindState(captiveId);
        if (state == null || !state.IsActive)
        {
            return false;
        }

        CharacterActor carrier = FindActor(state.reservedCarrierId);
        FailEscort(captiveId, carrier, reason);
        state.status = CaptivityStatus.AwaitingCapture;
        return true;
    }

    public bool TrySetPolicy(
        string captiveId,
        string policyId,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CaptivePolicyData policy = policies.FirstOrDefault(candidate =>
            string.Equals(candidate.policyId, policyId, StringComparison.Ordinal));
        if (state == null || policy == null)
        {
            failureReason = "포로 또는 수용 정책을 찾을 수 없습니다.";
            return false;
        }

        state.policyId = policy.policyId;
        ApplyPolicyLabor(state, policy.allowedLabor);
        return true;
    }

    public bool TryCreatePolicy(
        string displayName,
        out string policyId,
        out string failureReason)
    {
        policyId = string.Empty;
        failureReason = string.Empty;
        string normalizedName = displayName?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0)
        {
            normalizedName = $"수용 정책 {policySequence + 1}";
        }

        policySequence++;
        policyId = $"captivity:custom:{policySequence}";
        policies.Add(new CaptivePolicyData
        {
            policyId = policyId,
            displayName = normalizedName,
            allowedLabor =
                CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul,
            allowRansom = true,
            allowRecruitment = true,
            allowPerformance = true
        });
        return true;
    }

    public bool TryDuplicatePolicy(
        string sourcePolicyId,
        out string policyId,
        out string failureReason)
    {
        policyId = string.Empty;
        failureReason = string.Empty;
        CaptivePolicyData source = FindPolicy(sourcePolicyId);
        if (source == null)
        {
            failureReason = "복제할 수용 정책을 찾을 수 없습니다.";
            return false;
        }

        policySequence++;
        CaptivePolicyData duplicate = source.Clone();
        duplicate.policyId = $"captivity:custom:{policySequence}";
        duplicate.displayName = $"{source.displayName} 사본";
        policies.Add(duplicate);
        policyId = duplicate.policyId;
        return true;
    }

    public bool TryUpdatePolicy(
        CaptivePolicyData policy,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptivePolicyData target = FindPolicy(policy?.policyId);
        if (target == null)
        {
            failureReason = "수정할 수용 정책을 찾을 수 없습니다.";
            return false;
        }

        string displayName = policy.displayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0)
        {
            failureReason = "수용 정책 이름은 비워 둘 수 없습니다.";
            return false;
        }

        target.displayName = displayName;
        target.allowedLabor = policy.allowedLabor & CaptiveLaborPermission.All;
        target.allowRansom = policy.allowRansom;
        target.allowRecruitment = policy.allowRecruitment;
        target.allowCorruption = policy.allowCorruption;
        target.allowPerformance = policy.allowPerformance;
        foreach (CaptiveState state in captives.Where(candidate =>
                     candidate != null
                     && string.Equals(
                         candidate.policyId,
                         target.policyId,
                         StringComparison.Ordinal)))
        {
            ApplyPolicyLabor(state, target.allowedLabor);
        }

        return true;
    }

    public bool TryDeletePolicy(
        string policyId,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptivePolicyData policy = FindPolicy(policyId);
        if (policy == null)
        {
            failureReason = "삭제할 수용 정책을 찾을 수 없습니다.";
            return false;
        }

        if (string.Equals(
                policy.policyId,
                "captivity:standard",
                StringComparison.Ordinal))
        {
            failureReason = "기본 수용 정책은 삭제할 수 없습니다.";
            return false;
        }

        CaptivePolicyData fallback = policies.FirstOrDefault(candidate =>
            string.Equals(
                candidate.policyId,
                "captivity:standard",
                StringComparison.Ordinal));
        if (fallback == null)
        {
            failureReason = "포로를 재배정할 기본 수용 정책이 없습니다.";
            return false;
        }

        foreach (CaptiveState state in captives.Where(candidate =>
                     candidate != null
                     && string.Equals(
                         candidate.policyId,
                         policy.policyId,
                         StringComparison.Ordinal)))
        {
            state.policyId = fallback.policyId;
            ApplyPolicyLabor(state, fallback.allowedLabor);
        }

        policies.Remove(policy);
        return true;
    }

    public bool TrySetLaborPermissions(
        string captiveId,
        CaptiveLaborPermission permissions,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        if (state == null || !state.IsActive)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        if (!state.CanLabor)
        {
            failureReason = "순응도 50 이상, 건강 40% 이상부터 노역을 허용할 수 있습니다.";
            return false;
        }

        state.laborPermissions = permissions & CaptiveLaborPermission.All;
        state.status = permissions == CaptiveLaborPermission.None
            ? CaptivityStatus.Confined
            : CaptivityStatus.Labor;
        CharacterActor laborer = FindActor(captiveId);
        if (laborer != null)
        {
            laborer.characterType = permissions == CaptiveLaborPermission.None
                ? CharacterType.Intruder
                : CharacterType.NPC;
        }
        laborer?.SetAiPaused(permissions == CaptiveLaborPermission.None);
        laborer?.SetLifecycleState(
            permissions == CaptiveLaborPermission.None
                ? CharacterLifecycleState.Downed
                : CharacterLifecycleState.Active);
        state.lastResult = permissions == CaptiveLaborPermission.None
            ? "노역 해제"
            : "노역 허용";
        return true;
    }

    public bool TryStartInteraction(
        string captiveId,
        string interactionId,
        CharacterActor warden,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor subject = FindActor(captiveId);
        if (state == null
            || subject == null
            || !interactions.TryGet(interactionId, out ICaptivityInteractionHandler handler))
        {
            failureReason = "포로 또는 상호작용을 찾을 수 없습니다.";
            return false;
        }

        CaptivityInteractionContext context = new CaptivityInteractionContext(
            state,
            subject,
            warden,
            facility,
            facility != null ? facility.centerPos : state.housingPosition);
        if (!handler.CanExecute(context, out failureReason))
        {
            return false;
        }

        string materialDestinationId =
            $"captivity-interaction:{state.captiveId}:{handler.InteractionId}";
        foreach (KeyValuePair<StockCategory, int> cost in
                 handler.MaterialRequirements.Where(item => item.Value > 0))
        {
            if (itemRuntime.TryRequestFacilityDelivery(
                    cost.Key,
                    cost.Value,
                    facility.centerPos,
                    materialDestinationId,
                    out int requested,
                    out string deliveryReason)
                && requested >= cost.Value)
            {
                continue;
            }

            itemRuntime.ReleaseStacksByDestination(
                materialDestinationId,
                facility.centerPos);
            failureReason = string.IsNullOrWhiteSpace(deliveryReason)
                ? $"{cost.Key} 재료를 충분히 예약할 수 없습니다."
                : deliveryReason;
            return false;
        }

        state.status = CaptivityStatus.Interaction;
        subject.SetAiPaused(true);
        state.reservedWardenId = GetCharacterId(warden);
        state.currentInteractionId = handler.InteractionId;
        state.interactionMaterialDestinationId = materialDestinationId;
        state.interactionMaterialsConsumed =
            handler.MaterialRequirements.Count == 0;
        state.completedInteractionWork = 0f;
        state.requiredInteractionWork = Mathf.Max(1f, handler.RequiredWork);
        state.lastResult = $"{handler.DisplayName} 준비";
        return true;
    }

    public bool AdvanceInteraction(
        string captiveId,
        CharacterActor warden,
        float workAmount,
        out string status)
    {
        status = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor subject = FindActor(captiveId);
        if (state == null
            || subject == null
            || state.status != CaptivityStatus.Interaction
            || !string.Equals(
                state.reservedWardenId,
                GetCharacterId(warden),
                StringComparison.Ordinal)
            || !interactions.TryGet(
                state.currentInteractionId,
                out ICaptivityInteractionHandler handler))
        {
            status = "유효한 관리 작업이 아닙니다.";
            return false;
        }

        if (!state.interactionMaterialsConsumed)
        {
            if (!itemRuntime.TryConsumeFacilityBuffer(
                    state.interactionMaterialDestinationId,
                    handler.MaterialRequirements,
                    out string materialReason))
            {
                status = string.IsNullOrWhiteSpace(materialReason)
                    ? "관리 작업 재료 운반 대기"
                    : $"재료 운반 대기 · {materialReason}";
                return false;
            }

            state.interactionMaterialsConsumed = true;
        }

        state.completedInteractionWork = Mathf.Min(
            state.requiredInteractionWork,
            state.completedInteractionWork + Mathf.Max(0f, workAmount));
        if (state.completedInteractionWork + 0.001f < state.requiredInteractionWork)
        {
            status = $"{handler.DisplayName} "
                + $"{Mathf.RoundToInt(state.completedInteractionWork / state.requiredInteractionWork * 100f)}%";
            return true;
        }

        TryGetHousing(state.captiveId, out BuildableObject housing);
        CaptivityInteractionContext context = new CaptivityInteractionContext(
            state,
            subject,
            warden,
            housing,
            state.housingPosition);
        if (!handler.CanExecute(context, out status))
        {
            state.status = CaptivityStatus.Confined;
            ClearInteraction(state);
            return false;
        }

        ApplyInteractionResult(state, handler.Execute(context));
        status = state.lastResult;
        state.status = CaptivityStatus.Confined;
        ClearInteraction(state);
        return true;
    }

    public bool TryRecruit(string captiveId, out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        CaptivePolicyData policy = FindPolicy(state?.policyId);
        if (state == null || actor == null || !state.CanRecruit)
        {
            failureReason = "신뢰 70 이상, 원한 30 이하, 타락 60 미만이 필요합니다.";
            return false;
        }

        if (policy?.allowRecruitment != true)
        {
            failureReason = "현재 수용 정책은 정식 영입을 허용하지 않습니다.";
            return false;
        }

        state.status = CaptivityStatus.Recruited;
        state.lastResult = "정식 직원으로 영입됨";
        actor.characterType = CharacterType.NPC;
        actor.SetAiPaused(false);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        return true;
    }

    public bool TryConvertToMinion(string captiveId, out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        CaptivePolicyData policy = FindPolicy(state?.policyId);
        if (state == null || actor == null || !state.CanBecomeMinion)
        {
            failureReason = "타락 80 이상부터 하수인으로 전환할 수 있습니다.";
            return false;
        }

        if (policy?.allowCorruption != true)
        {
            failureReason = "현재 수용 정책은 하수인 전환을 허용하지 않습니다.";
            return false;
        }

        state.status = CaptivityStatus.Minion;
        state.lastResult = "타락한 하수인으로 전환됨";
        actor.characterType = CharacterType.NPC;
        actor.SetAiPaused(false);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        return true;
    }

    public bool TryRansom(
        string captiveId,
        out int paidAmount,
        out string failureReason)
    {
        paidAmount = 0;
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        CaptivePolicyData policy = state != null
            ? policies.FirstOrDefault(item => string.Equals(
                item.policyId,
                state.policyId,
                StringComparison.Ordinal))
            : null;
        if (state == null || actor == null || !state.IsActive)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        if (policy?.allowRansom != true)
        {
            failureReason = "현재 수용 정책은 몸값 협상을 허용하지 않습니다.";
            return false;
        }

        if (state.status is CaptivityStatus.Escorting
            or CaptivityStatus.Interaction
            or CaptivityStatus.Performer
            or CaptivityStatus.EscapeAttempt)
        {
            failureReason = "현재 진행 중인 포로 작업을 먼저 끝내야 합니다.";
            return false;
        }

        if (!gameDataProvider.TryGetGameData(out GameData gameData)
            || gameData.holdingMoney == null)
        {
            failureReason = "게임 자금 정보를 찾을 수 없습니다.";
            return false;
        }

        paidAmount = state.RansomValue;
        state.status = CaptivityStatus.Ransom;
        state.retaliationPressure = ClampStat(
            state.retaliationPressure + state.grudge * 0.35f);
        gameData.holdingMoney.Value += paidAmount;
        ReleaseCaptive(
            state,
            actor,
            $"몸값 {paidAmount:N0}을 받고 석방");
        gameEventBus.Publish(new CaptiveRansomedEvent(
            state.captiveId,
            paidAmount,
            state.retaliationPressure));
        return true;
    }

    public bool TryRelease(string captiveId, out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        if (state == null || actor == null)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        ReleaseCaptive(state, actor, "석방됨");
        return true;
    }

    public bool TryTriggerBetrayal(
        string captiveId,
        string trigger,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        if (state == null || actor == null || !state.IsActive)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        if (!state.falseCompliance)
        {
            failureReason = "거짓 복종 상태가 아닙니다.";
            return false;
        }

        if (state.status is CaptivityStatus.Escorting
            or CaptivityStatus.Stabilizing
            or CaptivityStatus.AwaitingEscort
            or CaptivityStatus.Interaction
            or CaptivityStatus.EscapeAttempt)
        {
            failureReason = "현재 상태에서는 배신 행동을 시작할 수 없습니다.";
            return false;
        }

        float betrayalChance = Mathf.Clamp01(
            0.25f
            + state.grudge * 0.005f
            + state.escapeRisk * 0.003f);
        if (!random.Chance(betrayalChance))
        {
            failureReason = "배신할 기회를 엿보고 있습니다.";
            return false;
        }

        state.status = CaptivityStatus.Escaped;
        state.restrained = false;
        state.betrayalTrigger = string.IsNullOrWhiteSpace(trigger)
            ? "기회 포착"
            : trigger.Trim();
        state.retaliationPressure = ClampStat(
            state.retaliationPressure + 20f + state.grudge * 0.25f);
        state.lastResult = $"{state.betrayalTrigger} 중 복종을 깨고 배신";
        actor.characterType = CharacterType.Intruder;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.SetAiPaused(false);
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        gameEventBus.Publish(new CaptiveEscapedEvent(
            state.captiveId,
            state.betrayalTrigger,
            betrayal: true));
        return true;
    }

    public bool TryAssignPerformer(
        string captiveId,
        bool assigned,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        CaptivePolicyData policy = state != null
            ? policies.FirstOrDefault(item => string.Equals(
                item.policyId,
                state.policyId,
                StringComparison.Ordinal))
            : null;
        if (state == null || actor == null || !state.IsActive)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        if (assigned && policy?.allowPerformance != true)
        {
            failureReason = "현재 포로 정책은 공연을 허용하지 않습니다.";
            return false;
        }

        state.status = assigned
            ? CaptivityStatus.Performer
            : CaptivityStatus.Confined;
        state.lastResult = assigned ? "공연 참가 준비" : "감방 복귀";
        actor.SetAiPaused(true);
        if (assigned)
        {
            actor.SetLifecycleState(CharacterLifecycleState.Active);
        }
        else
        {
            actor.SetLifecycleState(CharacterLifecycleState.Downed);
        }

        return true;
    }

    public void RecordPerformance(
        string captiveId,
        float fameGain,
        float skillGain,
        bool injured)
    {
        CaptiveState state = FindState(captiveId);
        if (state == null)
        {
            return;
        }

        state.performerFame = ClampStat(state.performerFame + Mathf.Max(0f, fameGain));
        state.performerSkill = ClampStat(state.performerSkill + Mathf.Max(0f, skillGain));
        if (injured)
        {
            state.performerInjuries++;
        }

        int previousPrivilegeTier = state.privilegeTier;
        state.privilegeTier = state.performerFame >= 75f
            ? 2
            : state.performerFame >= 50f
                ? 1
                : 0;
        ApplyPerformerMilestones(state, previousPrivilegeTier);
    }

    public bool TryResolvePerformerMilestone(
        string captiveId,
        CaptivePerformerMilestoneChoice choice,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        if (state == null || !state.IsActive)
        {
            failureReason = "공연자를 찾을 수 없습니다.";
            return false;
        }

        switch (choice)
        {
            case CaptivePerformerMilestoneChoice.StaffContract:
                if (!state.staffContractUnlocked)
                {
                    failureReason = "직원 계약 조건이 아직 열리지 않았습니다.";
                    return false;
                }

                if (!state.CanRecruit)
                {
                    failureReason = "신뢰·원한·타락 조건을 충족해야 직원 계약을 맺을 수 있습니다.";
                    return false;
                }

                if (!TryRecruit(captiveId, out failureReason))
                {
                    return false;
                }
                break;

            case CaptivePerformerMilestoneChoice.ReleaseNegotiation:
                if (!state.finalContractPending)
                {
                    failureReason = "명성 100의 최종 계약 선택이 열리지 않았습니다.";
                    return false;
                }

                state.resolvedMilestoneChoice = choice;
                state.finalContractPending = false;
                return TryRelease(captiveId, out failureReason);

            case CaptivePerformerMilestoneChoice.ExclusiveFighterContract:
                if (!state.finalContractPending)
                {
                    failureReason = "명성 100의 최종 계약 선택이 열리지 않았습니다.";
                    return false;
                }

                state.exclusiveFighter = true;
                state.finalContractPending = false;
                state.resolvedMilestoneChoice = choice;
                state.status = CaptivityStatus.Performer;
                state.lastResult = "전속 투사 계약을 맺었습니다.";
                break;

            default:
                failureReason = "선택할 계약이 없습니다.";
                return false;
        }

        return true;
    }

    private void ApplyPerformerMilestones(
        CaptiveState state,
        int previousPrivilegeTier)
    {
        if (state == null)
        {
            return;
        }

        if (state.performerFame >= 50f && !state.carePriorityUnlocked)
        {
            state.carePriorityUnlocked = true;
            state.lastResult = "공연 명성으로 우선 식량·치료 특혜를 얻었습니다.";
            itemRuntime.TryRequestFacilityDelivery(
                StockCategory.Food,
                1,
                state.housingPosition,
                $"captive-care:{state.captiveId}",
                out _,
                out _);
            PublishPerformerMilestone(state, 50, state.lastResult);
        }

        if (state.performerFame >= 75f && !state.staffContractUnlocked)
        {
            state.staffContractUnlocked = true;
            state.lastResult = "조건을 충족하면 직원 계약을 제안할 수 있습니다.";
            PublishPerformerMilestone(state, 75, state.lastResult);
        }

        if (state.performerFame >= 100f
            && state.resolvedMilestoneChoice == CaptivePerformerMilestoneChoice.None
            && !state.finalContractPending)
        {
            state.finalContractPending = true;
            state.lastResult = "석방 협상과 전속 투사 계약 중 하나를 선택할 수 있습니다.";
            PublishPerformerMilestone(state, 100, state.lastResult);
        }

        if (state.privilegeTier > previousPrivilegeTier)
        {
            state.health = Mathf.Clamp(state.health + 5f, 0f, 100f);
        }
    }

    private void PublishPerformerMilestone(
        CaptiveState state,
        int threshold,
        string message)
    {
        gameEventBus.Publish(new CaptivePerformerMilestoneEvent(
            state.captiveId,
            threshold,
            message));
        gameEventBus.RaiseAlert(
            $"공연자 명성 {threshold}",
            message,
            threshold >= 100
                ? EventAlertImportance.High
                : EventAlertImportance.Medium,
            "포로·노역");
    }

    public bool TryGetEscortState(
        string captiveId,
        CharacterActor carrier,
        out CaptiveState captive,
        out CharacterActor subject,
        out string failureReason)
    {
        CaptiveState state = FindState(captiveId);
        subject = FindActor(captiveId);
        captive = state;
        failureReason = string.Empty;
        if (state == null
            || subject == null
            || carrier == null
            || !string.Equals(
                state.reservedCarrierId,
                GetCharacterId(carrier),
                StringComparison.Ordinal))
        {
            failureReason = "호송 예약이 유효하지 않습니다.";
            return false;
        }

        return true;
    }

    public IDisposable BeginEscortPass(CharacterActor carrier, string captiveId)
    {
        DoorAccessSubjectRef subject = new DoorAccessSubjectRef(
            GetCharacterId(carrier),
            carrier != null && carrier.IsOwner
                ? DoorAccessGroup.Owner
                : DoorAccessGroup.Staff,
            character: carrier);
        return doorAccessCommands.BeginTemporaryOverride(
            subject,
            DoorAccessOverrideKind.EscortPass,
            $"escort:{captiveId?.Trim() ?? string.Empty}");
    }

    public bool TryPickupReservedRestraint(
        CaptiveState captive,
        CharacterActor carrier,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(carrier);
        if (inventory == null)
        {
            failureReason = "운반 인벤토리를 사용할 수 없습니다.";
            return false;
        }

        if (inventory.CountItem(CaptivityItemDefinitions.RestraintsItemId) > 0)
        {
            return true;
        }

        WorldItemReservedStackQuantity reservation =
            new WorldItemReservedStackQuantity(
                captive.restraintStackId,
                captive.restraintItemId,
                Mathf.Max(1, captive.restraintQuantity),
                captive.restraintPickupPosition,
                WorldItemHaulDestinationKind.Warehouse,
                string.Empty);
        return itemRuntime.TryPickupReservedStackQuantity(
            carrier,
            inventory,
            reservation,
            out int pickedUp,
            out failureReason)
            && pickedUp > 0;
    }

    public float AdvanceStabilization(
        string captiveId,
        CharacterActor carrier,
        float workAmount)
    {
        if (!TryGetEscortState(
                captiveId,
                carrier,
                out CaptiveState state,
                out CharacterActor subject,
                out _))
        {
            return 0f;
        }

        state.completedInteractionWork = Mathf.Min(
            state.requiredInteractionWork,
            state.completedInteractionWork + Mathf.Max(0f, workAmount));
        if (state.completedInteractionWork + 0.001f >= state.requiredInteractionWork)
        {
            state.stabilized = bodyHealth.Stabilize(subject)
                || bodyHealth.GetTotalBleeding(subject) <= 0.001f;
            state.status = CaptivityStatus.AwaitingEscort;
            state.lastResult = "현장 안정화 완료";
        }

        return state.completedInteractionWork
            / Mathf.Max(0.01f, state.requiredInteractionWork);
    }

    public bool TryBeginEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason)
    {
        if (!TryGetEscortState(
                captiveId,
                carrier,
                out CaptiveState state,
                out CharacterActor subject,
                out failureReason))
        {
            return false;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(carrier);
        if (!state.stabilized)
        {
            failureReason = "먼저 현장 안정화가 필요합니다.";
            return false;
        }

        if (inventory == null
            || !inventory.TryConsumeItem(CaptivityItemDefinitions.RestraintsItemId, 1))
        {
            failureReason = "구속구가 없습니다.";
            return false;
        }

        ConfiscateEquipment(subject, state.capturePosition);
        state.equipmentConfiscated = true;
        state.restrained = true;
        carriedParents[state.captiveId] = subject.transform.parent;
        subject.transform.SetParent(carrier.transform, worldPositionStays: false);
        subject.transform.localPosition = new Vector3(-0.28f, 0.16f, 0f);
        state.status = CaptivityStatus.Escorting;
        state.lastResult = "감방으로 호송 중";
        return true;
    }

    public bool TryCompleteEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason)
    {
        if (!TryGetEscortState(
                captiveId,
                carrier,
                out CaptiveState state,
                out CharacterActor subject,
                out failureReason)
            || !gridProvider.TryGetGrid(out Grid grid)
            || !grid.IsValidGridPos(state.housingPosition))
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "감방 위치가 유효하지 않습니다."
                : failureReason;
            return false;
        }

        RestoreParent(state.captiveId, subject);
        subject.transform.position = grid.GetWorldPos(state.housingPosition);
        subject.SetAiPaused(true);
        subject.characterType = CharacterType.Intruder;
        subject.SetLifecycleState(CharacterLifecycleState.Downed);
        state.status = CaptivityStatus.Confined;
        state.reservedCarrierId = string.Empty;
        state.health = EstimateHealth(subject);
        state.nextSecurityCheckAt = gameClock.Time + 5f;
        state.lastResult = "감방 수용 완료";
        RecalculateCaptiveState(state);
        return true;
    }

    public void FailEscort(string captiveId, CharacterActor carrier, string reason)
    {
        CaptiveState state = FindState(captiveId);
        CharacterActor subject = FindActor(captiveId);
        if (state == null)
        {
            return;
        }

        if (subject != null)
        {
            RestoreParent(state.captiveId, subject);
            if (carrier != null)
            {
                subject.transform.position = carrier.transform.position;
                state.capturePosition = carrier.GetNowXY();
            }
        }

        if (!string.IsNullOrWhiteSpace(state.restraintStackId))
        {
            itemRuntime.ReleaseReservation(
                state.restraintStackId,
                state.reservedCarrierId);
        }

        state.status = CaptivityStatus.AwaitingCapture;
        state.reservedCarrierId = string.Empty;
        state.housingBuildingId = string.Empty;
        state.restraintStackId = string.Empty;
        state.lastResult = string.IsNullOrWhiteSpace(reason)
            ? "포획 중단"
            : reason;
    }

    public bool TryGetEscapeState(
        string captiveId,
        CharacterActor actor,
        out Vector2Int destination,
        out string failureReason)
    {
        CaptiveState state = FindState(captiveId);
        destination = state?.escapeDestination ?? default;
        failureReason = string.Empty;
        if (state == null
            || actor == null
            || state.status != CaptivityStatus.EscapeAttempt
            || !string.Equals(
                state.captiveId,
                GetCharacterId(actor),
                StringComparison.Ordinal))
        {
            failureReason = "유효한 탈출 시도가 아닙니다.";
            return false;
        }

        return true;
    }

    public IDisposable BeginEscapePass(CharacterActor actor, string captiveId)
    {
        DoorAccessSubjectRef subject = new DoorAccessSubjectRef(
            GetCharacterId(actor),
            DoorAccessGroup.Captive,
            character: actor);
        return doorAccessCommands.BeginTemporaryOverride(
            subject,
            DoorAccessOverrideKind.CaptiveEscape,
            $"captive-escape:{captiveId?.Trim() ?? string.Empty}");
    }

    public void CompleteEscape(string captiveId, CharacterActor actor)
    {
        CaptiveState state = FindState(captiveId);
        if (state == null || actor == null)
        {
            return;
        }

        state.status = CaptivityStatus.Escaped;
        state.restrained = false;
        state.lastResult = string.IsNullOrWhiteSpace(state.betrayalTrigger)
            ? "감방에서 탈출"
            : state.betrayalTrigger;
        state.retaliationPressure = ClampStat(
            state.retaliationPressure + 15f + state.grudge * 0.2f);
        actor.characterType = CharacterType.Intruder;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.SetAiPaused(false);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        gameEventBus.Publish(new CaptiveEscapedEvent(
            state.captiveId,
            state.lastResult,
            betrayal: false));
        actor.GetAbility<AbilityMove>()?.StartSystemExitDungeon();
    }

    public void FailEscape(
        string captiveId,
        CharacterActor actor,
        string reason)
    {
        CaptiveState state = FindState(captiveId);
        if (state == null)
        {
            return;
        }

        state.status = CaptivityStatus.Confined;
        state.failedEscapeAttempts++;
        state.grudge = ClampStat(state.grudge + 6f);
        state.escapeRisk = ClampStat(state.escapeRisk + 8f);
        state.restrained = true;
        state.lastResult = string.IsNullOrWhiteSpace(reason)
            ? "탈출 실패 후 재구속"
            : $"{reason} · 재구속";
        if (actor != null)
        {
            actor.characterType = CharacterType.Intruder;
            actor.SetAiPaused(true);
            actor.SetLifecycleState(CharacterLifecycleState.Downed);
        }
    }

    public CaptivitySaveData Capture()
    {
        return new CaptivitySaveData
        {
            version = CaptivitySaveData.CurrentVersion,
            captureSequence = captureSequence,
            policySequence = policySequence,
            captives = captives.Select(state => state.Clone()).ToList(),
            policies = policies.Select(policy => policy.Clone()).ToList()
        };
    }

    public void Restore(CaptivitySaveData saveData, IList<string> warnings)
    {
        foreach (CaptiveState state in captives)
        {
            doorSubjectRegistry.SetCaptive(state.captiveId, false);
        }

        captives.Clear();
        policies.Clear();
        carriedParents.Clear();
        captureSequence = Mathf.Max(0, saveData?.captureSequence ?? 0);
        policySequence = Mathf.Max(0, saveData?.policySequence ?? 0);
        foreach (CaptivePolicyData policy in saveData?.policies
                     ?? new List<CaptivePolicyData>())
        {
            if (policy == null
                || string.IsNullOrWhiteSpace(policy.policyId)
                || policies.Any(existing => string.Equals(
                    existing.policyId,
                    policy.policyId,
                    StringComparison.Ordinal)))
            {
                warnings?.Add("유효하지 않거나 중복된 포로 정책을 건너뛰었습니다.");
                continue;
            }

            policies.Add(policy.Clone());
        }

        if (policies.Count == 0)
        {
            AddBuiltInPolicies();
        }
        else
        {
            EnsureStandardPolicy();
        }

        foreach (CaptiveState source in saveData?.captives
                     ?? new List<CaptiveState>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.captiveId)
                || captives.Any(existing => string.Equals(
                    existing.captiveId,
                    source.captiveId,
                    StringComparison.Ordinal)))
            {
                warnings?.Add("유효하지 않거나 중복된 포로 상태를 건너뛰었습니다.");
                continue;
            }

            CaptiveState restored = source.Clone();
            CharacterActor actor = FindActor(restored.captiveId);
            if (actor == null || actor.IsDead)
            {
                restored.status = CaptivityStatus.Dead;
                restored.lastResult = "복원 대상 없음";
                warnings?.Add(
                    $"포로 {restored.displayName}의 캐릭터를 찾을 수 없습니다.");
            }
            else
            {
                doorSubjectRegistry.SetCaptive(restored.captiveId, restored.IsActive);
                if (restored.status == CaptivityStatus.Escorting)
                {
                    restored.status = CaptivityStatus.AwaitingCapture;
                    restored.reservedCarrierId = string.Empty;
                    restored.lastResult = "호송 예약 재설정 필요";
                    warnings?.Add(
                        $"포로 {restored.displayName}의 진행 중 호송을 안전하게 해제했습니다.");
                }
            }

            RecalculateCaptiveState(restored);
            captives.Add(restored);
        }
    }

    private void OnInvasionStarted()
    {
        foreach (CaptiveState state in captives.Where(candidate =>
                     candidate != null
                     && candidate.IsActive
                     && candidate.falseCompliance).ToArray())
        {
            if (state.status is CaptivityStatus.Labor
                or CaptivityStatus.Performer)
            {
                TryTriggerBetrayal(state.captiveId, "침공의 혼란", out _);
            }
            else if (state.status == CaptivityStatus.Confined)
            {
                TryBeginEscapeAttempt(state, "침공 중 훔친 열쇠", out _);
            }
        }
    }

    private bool TryBeginEscapeAttempt(
        CaptiveState state,
        string trigger,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterActor actor = state != null ? FindActor(state.captiveId) : null;
        if (state == null
            || actor == null
            || !state.IsActive
            || state.status is CaptivityStatus.Escorting
                or CaptivityStatus.Interaction
                or CaptivityStatus.Performer
                or CaptivityStatus.EscapeAttempt)
        {
            failureReason = "현재 상태에서는 탈출을 시도할 수 없습니다.";
            return false;
        }

        if (!gridProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "탈출 경로를 계산할 그리드가 없습니다.";
            return false;
        }

        GridTraversalContext context = GridTraversalContext.ForCharacter(
            actor,
            DoorAccessOverrideKind.CaptiveEscape);
        if (!pathSearchBroker.TryGetSearch(
                grid,
                actor.GetNowXY(),
                out GridPathSearchResult search,
                GridPathSearchPriority.Urgent,
                context))
        {
            failureReason = "탈출 경로 탐색 예산을 확보하지 못했습니다.";
            return false;
        }

        Vector2Int destination = grid.GetCells()
            .Where(cell =>
                cell != null
                && cell.AreaType == GridCellAreaType.ExteriorPath
                && cell.IsWalkableArea
                && grid.IsWalkable(cell.Position)
                && search.ContainsPosition(cell.Position))
            .OrderBy(cell => Manhattan(actor.GetNowXY(), cell.Position))
            .Select(cell => cell.Position)
            .FirstOrDefault();
        if (!search.ContainsPosition(destination)
            || grid.GetGridCell(destination)?.AreaType
                != GridCellAreaType.ExteriorPath)
        {
            failureReason = "외부까지 이어지는 탈출 경로가 없습니다.";
            return false;
        }

        state.status = CaptivityStatus.EscapeAttempt;
        state.escapeDestination = destination;
        state.betrayalTrigger = string.IsNullOrWhiteSpace(trigger)
            ? "탈출 시도"
            : trigger.Trim();
        state.restrained = false;
        state.lastResult = state.betrayalTrigger;
        actor.characterType = CharacterType.Intruder;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.SetAiPaused(true);
        AbilityCaptiveEscape ability = AbilityCaptiveEscape.Ensure(actor);
        if (ability == null)
        {
            FailEscape(
                state.captiveId,
                actor,
                "탈출 행동을 시작할 수 없습니다.");
            failureReason = state.lastResult;
            return false;
        }

        ability.Configure(this, gameClock);
        ability.StartEscape(state.captiveId);
        return true;
    }

    private void OnCharacterDowned(CharacterActor actor)
    {
        if (!IsEligibleDownedIntruder(actor))
        {
            return;
        }

        actor.SetLifecycleState(CharacterLifecycleState.Downed);
        EnsureCandidate(actor);
    }

    private void OnCharacterRecovered(CharacterActor actor)
    {
        CaptiveState state = FindState(GetCharacterId(actor));
        if (state == null || !state.IsActive)
        {
            return;
        }

        if (state.status is CaptivityStatus.Confined
            or CaptivityStatus.Labor
            or CaptivityStatus.Interaction
            or CaptivityStatus.Performer)
        {
            actor.SetAiPaused(state.status != CaptivityStatus.Labor);
            actor.SetLifecycleState(
                state.status == CaptivityStatus.Labor
                    ? CharacterLifecycleState.Active
                    : CharacterLifecycleState.Downed);
        }
    }

    private void OnCharacterDeath(CharacterDeathEvent gameEvent)
    {
        CharacterActor actor = gameEvent.Actor;
        CaptiveState state = FindState(GetCharacterId(actor));
        if (state == null)
        {
            return;
        }

        state.status = CaptivityStatus.Dead;
        state.lastResult = "수용 중 사망";
        ReleaseInteractionMaterials(state);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        RestoreParent(state.captiveId, actor);
    }

    private CaptiveState EnsureCandidate(CharacterActor actor)
    {
        string id = GetCharacterId(actor);
        CaptiveState existing = FindState(id);
        if (existing != null)
        {
            return existing;
        }

        CaptiveState created = new CaptiveState
        {
            captiveId = id,
            displayName = actor.Identity?.DisplayName ?? actor.name,
            speciesTag = actor.SpeciesTag,
            status = CaptivityStatus.AwaitingCapture,
            capturePosition = actor.GetNowXY(),
            policyId = policies[0].policyId,
            laborPermissions = policies[0].allowedLabor,
            health = EstimateHealth(actor),
            lastResult = "포획 가능"
        };
        captureSequence++;
        captives.Add(created);
        doorSubjectRegistry.SetCaptive(id, true);
        return created;
    }

    private void ApplyInteractionResult(
        CaptiveState state,
        CaptivityInteractionResult result)
    {
        if (!result.Success)
        {
            state.lastResult = result.Message;
            return;
        }

        state.will = ClampStat(state.will + result.WillDelta);
        state.fear = ClampStat(state.fear + result.FearDelta);
        state.trust = ClampStat(state.trust + result.TrustDelta);
        state.grudge = ClampStat(state.grudge + result.GrudgeDelta);
        state.corruption = ClampStat(state.corruption + result.CorruptionDelta);
        state.health = ClampStat(state.health + result.HealthDelta);
        state.lastResult = result.Message;
        if (!string.IsNullOrWhiteSpace(result.OutputItemId)
            && result.OutputAmount > 0)
        {
            itemRuntime.SpawnItemAt(
                result.OutputItemId,
                result.OutputAmount,
                state.housingPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out _);
        }

        RecalculateCaptiveState(state);
    }

    private void RecalculateCaptiveState(CaptiveState state)
    {
        if (state == null)
        {
            return;
        }

        state.compliance = ClampStat(
            (100f - state.will) * 0.45f
            + state.fear * 0.35f
            + state.trust * 0.2f);
        state.escapeRisk = ClampStat(
            25f
            + state.grudge * 0.45f
            + (100f - state.fear) * 0.2f
            - state.compliance * 0.25f);
        state.falseCompliance =
            state.compliance >= 50f
            && state.grudge >= 60f
            && state.trust < 35f;
        state.retaliationPressure = ClampStat(
            state.grudge * 0.6f
            + state.corruption * 0.15f
            + state.failedEscapeAttempts * 8f);
    }

    private void ReleaseCaptive(
        CaptiveState state,
        CharacterActor actor,
        string result)
    {
        state.status = CaptivityStatus.Released;
        state.restrained = false;
        state.lastResult = result ?? "석방됨";
        actor.characterType = CharacterType.Intruder;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.SetAiPaused(false);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        actor.GetAbility<AbilityMove>()?.StartSystemExitDungeon();
    }

    private void ConfiscateEquipment(CharacterActor subject, Vector2Int position)
    {
        foreach (CombatEquipmentInstance instance in
                 combatEquipment.ConfiscateAllFromCharacter(GetCharacterId(subject)))
        {
            string itemId = DungeonItemCatalogSO.EquipmentItemId(instance.definitionId);
            if (itemRuntime.SpawnUniqueItemAt(
                    itemId,
                    position,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out string stackId))
            {
                combatEquipment.TryLinkToWorldStack(
                    instance.instanceId,
                    stackId,
                    CombatEquipmentWorldState.Loose);
            }
        }
    }

    private bool TryGetHousingRoom(
        Grid grid,
        BuildableObject housing,
        out RoomInstance room)
    {
        room = null;
        if (grid == null || housing == null)
        {
            return false;
        }

        if (roomLayoutCache.TryGetRoom(grid, housing.centerPos, out room))
        {
            return true;
        }

        foreach (Vector2Int position in housing.buildPoses)
        {
            foreach (Vector2Int direction in CardinalDirections)
            {
                if (roomLayoutCache.TryGetRoom(grid, position + direction, out room))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindHousingCell(
        Grid grid,
        RoomInstance room,
        BuildableObject housing,
        out Vector2Int position)
    {
        position = default;
        if (grid == null || room == null)
        {
            return false;
        }

        HashSet<Vector2Int> occupied = housing != null
            ? new HashSet<Vector2Int>(housing.buildPoses)
            : new HashSet<Vector2Int>();
        foreach (Vector2Int candidate in room.Cells
                     .Where(cell => !occupied.Contains(cell) && grid.IsWalkable(cell))
                     .OrderBy(cell => housing != null
                         ? Manhattan(cell, housing.centerPos)
                         : 0))
        {
            position = candidate;
            return true;
        }

        return false;
    }

    private CharacterActor FindActor(string id)
    {
        string normalized = id?.Trim() ?? string.Empty;
        return worldRegistry.AllCharacters.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                GetCharacterId(actor),
                normalized,
                StringComparison.Ordinal));
    }

    private CaptiveState FindState(string id)
    {
        string normalized = id?.Trim() ?? string.Empty;
        return captives.FirstOrDefault(state =>
            state != null
            && string.Equals(
                state.captiveId,
                normalized,
                StringComparison.Ordinal));
    }

    private void RestoreParent(string captiveId, CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        carriedParents.TryGetValue(captiveId ?? string.Empty, out Transform parent);
        carriedParents.Remove(captiveId ?? string.Empty);
        actor.transform.SetParent(parent, worldPositionStays: true);
    }

    private void ClearInteraction(CaptiveState state)
    {
        ReleaseInteractionMaterials(state);
        state.reservedWardenId = string.Empty;
        state.currentInteractionId = string.Empty;
        state.interactionMaterialDestinationId = string.Empty;
        state.interactionMaterialsConsumed = false;
        state.completedInteractionWork = 0f;
        state.requiredInteractionWork = 0f;
    }

    private void ReleaseInteractionMaterials(CaptiveState state)
    {
        if (state == null
            || state.interactionMaterialsConsumed
            || string.IsNullOrWhiteSpace(state.interactionMaterialDestinationId))
        {
            return;
        }

        itemRuntime.ReleaseStacksByDestination(
            state.interactionMaterialDestinationId,
            state.housingPosition);
    }

    private void ApplyPolicyLabor(
        CaptiveState state,
        CaptiveLaborPermission permissions)
    {
        if (state == null)
        {
            return;
        }

        state.laborPermissions = permissions & CaptiveLaborPermission.All;
        if (state.status != CaptivityStatus.Labor)
        {
            return;
        }

        CharacterActor laborer = FindActor(state.captiveId);
        if (state.laborPermissions != CaptiveLaborPermission.None)
        {
            return;
        }

        state.status = CaptivityStatus.Confined;
        if (laborer != null)
        {
            laborer.characterType = CharacterType.Intruder;
            laborer.SetAiPaused(true);
            laborer.SetLifecycleState(CharacterLifecycleState.Downed);
        }
    }

    private CaptivePolicyData FindPolicy(string policyId)
    {
        string id = policyId?.Trim() ?? string.Empty;
        return policies.FirstOrDefault(policy => string.Equals(
            policy.policyId,
            id,
            StringComparison.Ordinal));
    }

    private void AddBuiltInPolicies()
    {
        policies.Add(CreateStandardPolicy());
        policies.Add(new CaptivePolicyData
        {
            policyId = "captivity:forced-labor",
            displayName = "강제 노역",
            allowedLabor = CaptiveLaborPermission.All,
            allowRansom = true,
            allowRecruitment = false,
            allowCorruption = true,
            allowPerformance = false
        });
        policies.Add(new CaptivePolicyData
        {
            policyId = "captivity:performer",
            displayName = "공연자 관리",
            allowedLabor =
                CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul,
            allowRansom = false,
            allowRecruitment = true,
            allowCorruption = false,
            allowPerformance = true
        });
        policies.Add(new CaptivePolicyData
        {
            policyId = "captivity:corruption",
            displayName = "타락 의식",
            allowedLabor = CaptiveLaborPermission.None,
            allowRansom = false,
            allowRecruitment = false,
            allowCorruption = true,
            allowPerformance = true
        });
    }

    private void EnsureStandardPolicy()
    {
        if (FindPolicy("captivity:standard") != null)
        {
            return;
        }

        policies.Insert(0, CreateStandardPolicy());
    }

    private static CaptivePolicyData CreateStandardPolicy()
    {
        return new CaptivePolicyData
        {
            policyId = "captivity:standard",
            displayName = "표준 수용",
            allowedLabor =
                CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul,
            allowRansom = true,
            allowRecruitment = true,
            allowCorruption = false,
            allowPerformance = true
        };
    }

    private static bool IsEligibleDownedIntruder(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.characterType == CharacterType.Intruder
            && actor.CurrentLifecycleState == CharacterLifecycleState.Downed;
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        return id.Length > 0
            ? id
            : actor != null ? $"character:{actor.GetInstanceID()}" : string.Empty;
    }

    private static string GetHousingId(BuildableObject building)
    {
        return building == null
            ? string.Empty
            : $"housing:{building.id}:{building.centerPos.x}:{building.centerPos.y}";
    }

    private static float EstimateHealth(CharacterActor actor)
    {
        if (actor?.Stats == null)
        {
            return 0f;
        }

        return Mathf.Clamp(
            actor.Stats.CurrentHealth / Mathf.Max(1f, actor.Stats.MaxHealth) * 100f,
            0f,
            100f);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

    private static float ClampStat(float value)
    {
        return Mathf.Clamp(value, 0f, 100f);
    }

    private static CaptiveLaborPermission GetLaborPermission(WorkTypeId workTypeId)
    {
        if (workTypeId == BuiltInWorkTypeIds.Clean)
        {
            return CaptiveLaborPermission.Clean;
        }

        if (workTypeId == BuiltInWorkTypeIds.Haul
            || workTypeId == BuiltInWorkTypeIds.Restock)
        {
            return CaptiveLaborPermission.Haul;
        }

        if (workTypeId == BuiltInWorkTypeIds.DrawWater)
        {
            return CaptiveLaborPermission.DrawWater;
        }

        if (workTypeId == BuiltInWorkTypeIds.Refuel)
        {
            return CaptiveLaborPermission.Refuel;
        }

        if (workTypeId == BuiltInWorkTypeIds.Construct)
        {
            return CaptiveLaborPermission.Construct;
        }

        if (workTypeId == BuiltInWorkTypeIds.Repair)
        {
            return CaptiveLaborPermission.Repair;
        }

        if (workTypeId == BuiltInWorkTypeIds.Butcher)
        {
            return CaptiveLaborPermission.Butcher;
        }

        if (workTypeId == BuiltInWorkTypeIds.Craft)
        {
            return CaptiveLaborPermission.CraftAssist;
        }

        return CaptiveLaborPermission.None;
    }

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.left,
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.down
    };
}
