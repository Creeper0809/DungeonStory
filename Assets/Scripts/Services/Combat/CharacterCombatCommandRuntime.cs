using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterCombatCommandRuntime :
    ICharacterCombatCommandRuntime,
    ITickable,
    IDisposable
{
    private readonly IGridSystemProvider gridProvider;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ICombatResolutionService resolution;
    private readonly ICombatFiringSolutionService firingSolutions;
    private readonly ICombatLineOfSightService lineOfSight;
    private readonly ICombatCoverQuery coverQuery;
    private readonly ICombatAffiliationService affiliation;
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly ICombatAmmoResupplyRuntime ammoResupply;
    private readonly IDefenseTacticalCoordinator tacticalCoordinator;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock gameClock;
    private readonly Dictionary<string, CharacterCombatCommand> commands =
        new Dictionary<string, CharacterCombatCommand>(StringComparer.Ordinal);
    private readonly HashSet<string> combatStance =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> nextMoveRetryAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> commandRevisions =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<string> tickCommandActorIds = new List<string>();
    private IReadOnlyList<CharacterCombatCommand> commandView = Array.Empty<CharacterCombatCommand>();
    private bool viewDirty = true;
    private int commandSequence;

    public CharacterCombatCommandRuntime(
        IGridSystemProvider gridProvider,
        ICombatEquipmentRuntime equipment,
        ICombatResolutionService resolution,
        ICombatFiringSolutionService firingSolutions,
        ICombatLineOfSightService lineOfSight,
        ICombatCoverQuery coverQuery,
        ICombatAffiliationService affiliation,
        ICharacterBodyHealthRuntime bodyHealth,
        ICombatAmmoResupplyRuntime ammoResupply,
        IDefenseTacticalCoordinator tacticalCoordinator,
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock)
    {
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        this.firingSolutions = firingSolutions ?? throw new ArgumentNullException(nameof(firingSolutions));
        this.lineOfSight = lineOfSight ?? throw new ArgumentNullException(nameof(lineOfSight));
        this.coverQuery = coverQuery ?? throw new ArgumentNullException(nameof(coverQuery));
        this.affiliation = affiliation ?? throw new ArgumentNullException(nameof(affiliation));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.ammoResupply = ammoResupply ?? throw new ArgumentNullException(nameof(ammoResupply));
        this.tacticalCoordinator = tacticalCoordinator
            ?? throw new ArgumentNullException(nameof(tacticalCoordinator));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public IReadOnlyList<CharacterCombatCommand> ActiveCommands
    {
        get
        {
            if (viewDirty)
            {
                commandView = commands.Values
                    .Where(command => command != null
                        && command.state is not CharacterCombatCommandState.Completed
                        and not CharacterCombatCommandState.Cancelled)
                    .Select(command => command.Clone())
                    .ToArray();
                viewDirty = false;
            }

            return commandView;
        }
    }

    public void Dispose()
    {
        foreach (CharacterActor actor in worldRegistry.Characters.ToArray())
        {
            if (actor != null && combatStance.Contains(GetId(actor)))
            {
                ReleaseCombatStance(actor);
            }
        }

        commands.Clear();
        combatStance.Clear();
        nextMoveRetryAt.Clear();
        tickCommandActorIds.Clear();
    }

    public void Tick()
    {
        float deltaTime = gameClock.DeltaTime;
        if (commands.Count == 0 || deltaTime <= 0f)
        {
            return;
        }

        tickCommandActorIds.Clear();
        foreach (string actorId in commands.Keys)
        {
            tickCommandActorIds.Add(actorId);
        }

        for (int index = 0; index < tickCommandActorIds.Count; index++)
        {
            string actorId = tickCommandActorIds[index];
            if (!commands.TryGetValue(actorId, out CharacterCombatCommand command))
            {
                continue;
            }

            CharacterActor actor = FindCharacter(actorId);
            if (actor == null || actor.IsDead || actor.CurrentLifecycleState != CharacterLifecycleState.Active)
            {
                CancelCommandById(actorId, "전투 명령 수행 불가");
                continue;
            }

            if (!combatStance.Contains(actorId))
            {
                CancelCommandById(actorId, "전투 태세 해제");
                continue;
            }

            command.attackCooldownRemaining = Mathf.Max(
                0f,
                command.attackCooldownRemaining - deltaTime);
            if (command.reloadRemaining > 0f)
            {
                command.reloadRemaining = Mathf.Max(0f, command.reloadRemaining - deltaTime);
                command.status = $"재장전 {Mathf.CeilToInt(command.reloadRemaining * 10f) / 10f:0.0}초";
                if (command.reloadRemaining <= 0f)
                {
                    CompleteReload(actor, command);
                }

                MarkDirty();
                continue;
            }

            switch (command.type)
            {
                case CombatCommandType.Move:
                case CombatCommandType.MoveToCover:
                    TickMove(actor, command);
                    break;
                case CombatCommandType.Attack:
                case CombatCommandType.ForceFire:
                    TickAttack(actor, command);
                    break;
                case CombatCommandType.Reload:
                    BeginReload(actor, command, completeCommand: true);
                    break;
                case CombatCommandType.Rescue:
                    TickRescue(actor, command);
                    break;
                default:
                    CompleteCommand(command, "명령 완료");
                    break;
            }
        }
    }

    public bool IsInCombatStance(CharacterActor actor)
    {
        return actor != null && combatStance.Contains(GetId(actor));
    }

    public bool SetCombatStance(CharacterActor actor, bool enabled, out string message)
    {
        message = string.Empty;
        if (!CanCommand(actor, out message))
        {
            return false;
        }

        string id = GetId(actor);
        if (enabled)
        {
            if (combatStance.Add(id))
            {
                actor.Brain?.StopCurrentActionForReplan("전투 태세 전환");
                actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
                actor.SetAiPaused(true);
                DefenseCombatPresentation.Ensure(actor)?.SetStatus("전투 태세", combatActive: true);
            }

            message = $"{GetName(actor)}: 전투 태세";
            return true;
        }

        CancelCommand(actor, "전투 태세 해제");
        ReleaseCombatStance(actor);
        message = $"{GetName(actor)}: 생활 태세";
        return true;
    }

    public bool TryIssueMove(CharacterActor actor, Vector2Int destination, out string message)
    {
        return TryIssueMoveInternal(
            actor,
            destination,
            CombatCommandType.Move,
            CombatPositionReservationKind.Move,
            out message);
    }

    public bool TryIssueMoveToCover(
        CharacterActor actor,
        Vector2Int destination,
        out string message)
    {
        return TryIssueMoveInternal(
            actor,
            destination,
            CombatCommandType.MoveToCover,
            CombatPositionReservationKind.Cover,
            out message);
    }

    private bool TryIssueMoveInternal(
        CharacterActor actor,
        Vector2Int destination,
        CombatCommandType type,
        CombatPositionReservationKind reservationKind,
        out string message)
    {
        string reservationFailure = string.Empty;
        if (!RequireCombatStance(actor, out message)
            || !gridProvider.TryGetGrid(out Grid grid)
            || !grid.IsValidGridPos(destination)
            || !grid.IsWalkable(destination)
            || !tacticalCoordinator.TryReserve(
                GetId(actor),
                string.Empty,
                destination,
                reservationKind,
                0f,
                out reservationFailure))
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                message = string.IsNullOrWhiteSpace(reservationFailure)
                    ? "이동할 수 없는 칸입니다."
                    : reservationFailure;
            }

            return false;
        }

        CharacterCombatCommand command = CreateCommand(actor, type, releaseReservation: false);
        command.TargetCell = destination;
        command.status = "이동 준비";
        commands[command.actorId] = command;
        nextMoveRetryAt[command.actorId] = 0f;
        MarkDirty();
        message = $"{GetName(actor)}: ({destination.x}, {destination.y}) 이동";
        return true;
    }

    public bool TryIssueAttack(
        CharacterActor actor,
        CombatParticipantRef target,
        bool forceFire,
        out string message)
    {
        if (!RequireCombatStance(actor, out message)
            || !target.IsValid
            || target.IsDead)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "유효한 전투 대상을 정확히 선택해야 합니다.";
            }

            return false;
        }

        CombatParticipantRef attacker = new CombatParticipantRef(actor);
        CombatRelationship relationship = affiliation.GetRelationship(attacker, target);
        if (!forceFire && relationship != CombatRelationship.Hostile)
        {
            message = "아군·손님·중립 대상은 일반 공격할 수 없습니다.";
            return false;
        }

        if (!tacticalCoordinator.CanAssignTarget(GetId(actor), target.Id))
        {
            message = "해당 목표에는 이미 충분한 전투원이 배치되었습니다.";
            return false;
        }

        equipment.TryGetActiveWeapon(GetId(actor), out CombatWeaponSnapshot activeWeapon);
        if (!tacticalCoordinator.TryReserve(
                GetId(actor),
                target.Id,
                actor.GetNowXY(),
                activeWeapon?.IsRanged == true
                    ? CombatPositionReservationKind.Ranged
                    : CombatPositionReservationKind.Melee,
                activeWeapon?.MaximumRange ?? 1f,
                out message))
        {
            return false;
        }

        CharacterCombatCommand command = CreateCommand(
            actor,
            forceFire ? CombatCommandType.ForceFire : CombatCommandType.Attack,
            releaseReservation: false);
        command.targetId = target.Id;
        command.TargetCell = target.GridPosition;
        command.forceFire = forceFire;
        command.status = forceFire ? "강제 사격 준비" : "공격 준비";
        commands[command.actorId] = command;
        nextMoveRetryAt[command.actorId] = 0f;
        MarkDirty();
        message = $"{GetName(actor)}: {target.DisplayName} {(forceFire ? "강제 사격" : "공격")}";
        return true;
    }

    public bool TryIssueForceFireAtCell(
        CharacterActor actor,
        Vector2Int targetCell,
        out string message)
    {
        if (!RequireCombatStance(actor, out message)
            || !gridProvider.TryGetGrid(out Grid grid)
            || !grid.IsValidGridPos(targetCell))
        {
            message = string.IsNullOrWhiteSpace(message)
                ? "유효한 사격 칸을 선택해야 합니다."
                : message;
            return false;
        }

        CharacterCombatCommand command = CreateCommand(actor, CombatCommandType.ForceFire);
        command.TargetCell = targetCell;
        command.forceFire = true;
        command.status = "지정 칸 강제 사격 준비";
        commands[command.actorId] = command;
        MarkDirty();
        message = $"{GetName(actor)}: ({targetCell.x}, {targetCell.y}) 강제 사격";
        return true;
    }

    public bool TryIssueReload(CharacterActor actor, out string message)
    {
        if (!RequireCombatStance(actor, out message)
            || !equipment.TryGetActiveWeapon(GetId(actor), out CombatWeaponSnapshot weapon)
            || weapon == null
            || !weapon.RequiresAmmo
            || string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            message = string.IsNullOrWhiteSpace(message)
                ? "재장전할 원거리 무기가 없습니다."
                : message;
            return false;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.FindByCharacterId(GetId(actor));
        if (inventory == null || inventory.CountItem(weapon.AmmunitionItemId) <= 0)
        {
            return ammoResupply.TryRequestAmmoResupply(actor, out message);
        }

        CharacterCombatCommand command = CreateCommand(actor, CombatCommandType.Reload);
        command.weaponInstanceId = weapon.InstanceId;
        command.status = "재장전 준비";
        commands[command.actorId] = command;
        MarkDirty();
        message = $"{GetName(actor)}: 재장전";
        return true;
    }

    public bool TryIssueSwitchWeapon(CharacterActor actor, out string message)
    {
        if (!RequireCombatStance(actor, out message))
        {
            return false;
        }

        string id = GetId(actor);
        CharacterCombatLoadoutProfile profile = equipment.GetActiveProfileSnapshot(id);
        if (profile == null || profile.weaponInstanceIds.Count < 2)
        {
            message = "교체할 예비 무기가 없습니다.";
            return false;
        }

        int current = profile.weaponInstanceIds.FindIndex(instanceId =>
            string.Equals(instanceId, profile.activeWeaponInstanceId, StringComparison.Ordinal));
        for (int offset = 1; offset <= profile.weaponInstanceIds.Count; offset++)
        {
            string candidate = profile.weaponInstanceIds[
                (Mathf.Max(0, current) + offset) % profile.weaponInstanceIds.Count];
            if (equipment.TrySetActiveWeapon(id, candidate, out _))
            {
                message = $"{GetName(actor)}: 무기 교체";
                DefenseCombatPresentation.Ensure(actor)?.SetStatus("무기 교체", combatActive: true);
                return true;
            }
        }

        message = "사용 가능한 예비 무기가 없습니다.";
        return false;
    }

    public bool TrySetFireMode(CharacterActor actor, CombatFireMode mode, out string message)
    {
        if (!RequireCombatStance(actor, out message)
            || !equipment.TrySetFireMode(GetId(actor), mode, out message))
        {
            return false;
        }

        message = $"{GetName(actor)}: {GetFireModeName(mode)}";
        return true;
    }

    public bool TrySetHoldFire(CharacterActor actor, bool holdFire, out string message)
    {
        if (!RequireCombatStance(actor, out message)
            || !equipment.TrySetHoldFire(GetId(actor), holdFire))
        {
            message = string.IsNullOrWhiteSpace(message) ? "사격 중지 설정 실패" : message;
            return false;
        }

        message = $"{GetName(actor)}: {(holdFire ? "사격 중지" : "사격 허용")}";
        return true;
    }

    public bool TryIssueRescue(
        CharacterActor rescuer,
        CharacterActor patient,
        out string message)
    {
        if (!RequireCombatStance(rescuer, out message)
            || patient == null
            || patient.CurrentLifecycleState != CharacterLifecycleState.Downed)
        {
            message = string.IsNullOrWhiteSpace(message)
                ? "쓰러진 구조 대상을 정확히 선택해야 합니다."
                : message;
            return false;
        }

        CharacterCombatCommand command = CreateCommand(rescuer, CombatCommandType.Rescue);
        command.targetId = GetId(patient);
        command.TargetCell = patient.GetNowXY();
        command.status = "구조 준비";
        commands[command.actorId] = command;
        MarkDirty();
        message = $"{GetName(rescuer)}: {GetName(patient)} 구조";
        return true;
    }

    public bool TryGetCommand(CharacterActor actor, out CharacterCombatCommand command)
    {
        command = null;
        if (actor == null
            || !commands.TryGetValue(GetId(actor), out CharacterCombatCommand stored))
        {
            return false;
        }

        command = stored.Clone();
        return true;
    }

    public void CancelCommand(CharacterActor actor, string reason)
    {
        if (actor != null)
        {
            CancelCommandById(GetId(actor), reason);
        }
    }

    public CharacterCombatCommandSaveData Capture()
    {
        return new CharacterCombatCommandSaveData
        {
            stanceCharacterIds = combatStance.OrderBy(id => id).ToList(),
            commands = commands.Values
                .Where(command => command != null
                    && command.state is not CharacterCombatCommandState.Completed
                    and not CharacterCombatCommandState.Cancelled)
                .Select(command => command.Clone())
                .ToList()
        };
    }

    public void Restore(CharacterCombatCommandSaveData saveData, IList<string> warnings)
    {
        foreach (string actorId in combatStance.ToArray())
        {
            CharacterActor actor = FindCharacter(actorId);
            if (actor != null)
            {
                ReleaseCombatStance(actor);
            }
        }

        combatStance.Clear();
        commands.Clear();
        if (saveData == null)
        {
            MarkDirty();
            return;
        }

        foreach (string actorId in saveData.stanceCharacterIds ?? new List<string>())
        {
            CharacterActor actor = FindCharacter(actorId);
            if (actor == null || actor.IsDead
                || actor.CurrentLifecycleState != CharacterLifecycleState.Active)
            {
                warnings?.Add($"전투 태세 복원 대상 '{actorId}'을 찾지 못해 해제했습니다.");
                continue;
            }

            combatStance.Add(actorId);
            actor.SetAiPaused(true);
        }

        foreach (CharacterCombatCommand source in saveData.commands
            ?? new List<CharacterCombatCommand>())
        {
            CharacterActor actor = FindCharacter(source?.actorId);
            if (source == null || actor == null || !combatStance.Contains(source.actorId))
            {
                warnings?.Add("대상 없는 전투 명령 예약을 해제했습니다.");
                continue;
            }

            CharacterCombatCommand restored = source.Clone();
            restored.attackCooldownRemaining = Mathf.Max(0f, source.attackCooldownRemaining);
            restored.reloadRemaining = Mathf.Max(0f, source.reloadRemaining);
            restored.state = CharacterCombatCommandState.Queued;
            commands[restored.actorId] = restored;
        }

        MarkDirty();
    }

    private void TickMove(CharacterActor actor, CharacterCombatCommand command)
    {
        if (!command.hasTargetCell)
        {
            BlockCommand(command, "이동 목표 없음");
            return;
        }

        if (actor.GetNowXY() == command.TargetCell)
        {
            CompleteCommand(command, "이동 완료");
            return;
        }

        if (!CanRetryMove(command.actorId))
        {
            return;
        }

        AbilityMove move = actor.GetComponent<AbilityMove>();
        string result = string.Empty;
        if (move != null && move.TryStartPlayerMove(command.TargetCell, out result))
        {
            command.state = CharacterCombatCommandState.Moving;
            command.status = "전투 위치로 이동";
            nextMoveRetryAt[command.actorId] = gameClock.Time + 0.35f;
            DefenseCombatPresentation.Ensure(actor)?.SetStatus(command.status, combatActive: true);
        }
        else
        {
            BlockCommand(command, string.IsNullOrWhiteSpace(result) ? "이동 경로 없음" : result);
        }
    }

    private void TickAttack(CharacterActor actor, CharacterCombatCommand command)
    {
        if (ammoResupply.IsResupplying(actor))
        {
            command.state = CharacterCombatCommandState.WaitingForAmmo;
            command.status = "창고 탄약 재보급 중";
            MarkDirty();
            return;
        }

        if (!gridProvider.TryGetGrid(out Grid grid))
        {
            BlockCommand(command, "전투 Grid 없음");
            return;
        }

        CombatParticipantRef intendedTarget = FindParticipant(command.targetId);
        Vector2Int targetCell = intendedTarget.IsValid
            ? intendedTarget.GridPosition
            : command.TargetCell;
        command.TargetCell = targetCell;
        if (!intendedTarget.IsValid && command.type != CombatCommandType.ForceFire)
        {
            CompleteCommand(command, "대상 소실");
            return;
        }

        if (intendedTarget.IsValid && intendedTarget.IsDead)
        {
            CompleteCommand(command, "대상 제압");
            return;
        }

        string actorId = GetId(actor);
        equipment.TryGetActiveWeapon(actorId, out CombatWeaponSnapshot weapon);
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();
        CharacterCombatLoadoutProfile profile = equipment.GetActiveProfileSnapshot(actorId);
        if (profile?.holdFire == true && weapon.IsRanged)
        {
            BlockCommand(command, "사격 중지 상태");
            return;
        }

        int distance = Manhattan(actor.GetNowXY(), targetCell);
        bool inRange = weapon.IsRanged
            ? distance <= weapon.MaximumRange
            : distance == 1;
        bool hasLine = !weapon.IsRanged
            || lineOfSight.Evaluate(
                grid,
                actor.GetNowXY(),
                targetCell,
                actorId,
                intendedTarget.Id).HasLineOfSight;
        if (!inRange || !hasLine)
        {
            if (!TryMoveToAttackPosition(grid, actor, command, weapon, targetCell))
            {
                BlockCommand(command, !hasLine ? "사선 확보 불가" : "공격 위치 없음");
            }

            return;
        }

        if (weapon.RequiresAmmo && weapon.LoadedAmmo <= 0)
        {
            if (BeginReload(actor, command, completeCommand: false))
            {
                return;
            }

            if (TrySelectFallbackWeapon(actor, preferLoadedRanged: true, out _))
            {
                command.status = "백업 무기로 교체";
                MarkDirty();
                return;
            }

            if (ammoResupply.TryRequestAmmoResupply(actor, out string resupplyMessage))
            {
                command.state = CharacterCombatCommandState.WaitingForAmmo;
                command.status = string.IsNullOrWhiteSpace(resupplyMessage)
                    ? "창고 탄약 재보급 중"
                    : resupplyMessage;
                MarkDirty();
                return;
            }

            command.state = CharacterCombatCommandState.WaitingForAmmo;
            command.status = "탄약 재보급 필요";
            DefenseCombatPresentation.Ensure(actor)?.SetStatus(command.status, combatActive: true);
            MarkDirty();
            return;
        }

        if (command.attackCooldownRemaining > 0f)
        {
            command.state = CharacterCombatCommandState.Aiming;
            command.status = weapon.IsRanged ? "조준 중" : "공격 준비";
            return;
        }

        if (weapon.IsRanged)
        {
            PerformRangedAttack(grid, actor, command, intendedTarget, weapon, profile);
        }
        else
        {
            PerformMeleeAttack(actor, command, intendedTarget, weapon);
        }
    }

    private void PerformRangedAttack(
        Grid grid,
        CharacterActor actor,
        CharacterCombatCommand command,
        CombatParticipantRef intendedTarget,
        CombatWeaponSnapshot weapon,
        CharacterCombatLoadoutProfile profile)
    {
        if (!intendedTarget.IsValid)
        {
            CombatLineOfSightResult tileSight = lineOfSight.Evaluate(
                grid,
                actor.GetNowXY(),
                command.TargetCell,
                GetId(actor),
                string.Empty);
            if (!tileSight.HasLineOfSight)
            {
                BlockCommand(command, tileSight.FailureReason);
                return;
            }

            LaunchProjectile(actor.transform.position, grid.GetWorldPos(command.TargetCell), weapon);
            equipment.TryConsumeLoadedAmmo(weapon.InstanceId);
            command.attackCooldownRemaining = resolution.CalculateAttackInterval(
                CombatRuntimeStatFactory.Create(actor, bodyHealth.GetSnapshot(actor)),
                weapon,
                profile?.fireMode ?? CombatFireMode.Aimed);
            command.status = "지정 칸 사격";
            MarkDirty();
            return;
        }

        CombatFiringSolution solution = firingSolutions.Evaluate(
            grid,
            new CombatParticipantRef(actor),
            intendedTarget);
        if (!command.forceFire && !solution.AutoFireAllowed)
        {
            BlockCommand(command, solution.FailureReason);
            return;
        }

        if (!solution.LineOfSight.HasLineOfSight)
        {
            BlockCommand(command, solution.FailureReason);
            return;
        }

        CombatParticipantRef impactTarget = firingSolutions.ResolveImpactTarget(
            solution,
            command.forceFire,
            out bool intercepted);
        if (!impactTarget.IsValid)
        {
            BlockCommand(command, "보호 대상이 사선에 있어 사격 보류");
            return;
        }

        CombatFireMode mode = ResolveSupportedFireMode(
            weapon,
            profile?.fireMode ?? CombatFireMode.Aimed);
        CharacterBodyHealthSnapshot attackerBody = bodyHealth.GetSnapshot(actor);
        CombatStatSnapshot defenderStats = GetCombatStats(impactTarget);
        CharacterBodyHealthSnapshot defenderBody = impactTarget.IsCharacter
            ? bodyHealth.GetSnapshot(impactTarget.Character)
            : default;
        CombatAttackResult result = resolution.Resolve(new CombatAttackRequest(
            command.commandId + ":" + command.revision++,
            GetId(actor),
            impactTarget.Id,
            CombatRuntimeStatFactory.Create(actor, attackerBody),
            defenderStats,
            weapon,
            Manhattan(actor.GetNowXY(), impactTarget.GridPosition),
            mode,
            coverQuery.GetCover(grid, actor.GetNowXY(), impactTarget.GridPosition),
            hasLineOfSight: true,
            friendlyFireRisk: solution.LineOfSight.FriendlyFireRisk,
            forceFire: command.forceFire,
            defenderDowned: impactTarget.IsCharacter && defenderBody.Downed,
            defenderMeleeLocked: false,
            attackerSuppression: attackerBody.Suppression,
            defenderSuppression: impactTarget.IsCharacter ? defenderBody.Suppression : 0f,
            attackPowerMultiplier: actor.GetCombatPowerMultiplier(),
            defenderArmor: impactTarget.IsCharacter
                ? equipment.GetArmor(impactTarget.Id)
                : Array.Empty<CombatArmorSnapshot>(),
            defenderShield: impactTarget.IsCharacter
                ? equipment.GetShield(impactTarget.Id)
                : default));
        if (!result.Executed)
        {
            BlockCommand(command, result.FailureReason);
            return;
        }

        LaunchProjectile(actor.transform.position, impactTarget.IsCharacter
            ? impactTarget.Character.transform.position
            : impactTarget.Wildlife.transform.position, weapon);
        DefenseCombatPresentation.Ensure(actor)?.PlayAttack(
            impactTarget.IsCharacter
                ? impactTarget.Character.transform.position
                : impactTarget.Wildlife.transform.position,
            weapon);
        equipment.TryConsumeLoadedAmmo(weapon.InstanceId);
        ApplyCombatResult(
            impactTarget,
            result,
            actor,
            weapon.Verb?.damageType ?? CombatDamageType.Pierce);
        ApplyArmorDurabilityDamage(result);
        command.attackCooldownRemaining = resolution.CalculateAttackInterval(
            CombatRuntimeStatFactory.Create(actor, attackerBody),
            weapon,
            mode);
        command.state = CharacterCombatCommandState.Executing;
        command.status = intercepted
            ? $"{impactTarget.DisplayName} 오발 피격"
            : result.Hit ? "사격 명중" : result.CoverBlocked ? "엄폐물 피격" : "사격 빗나감";
        MarkDirty();
    }

    private void PerformMeleeAttack(
        CharacterActor actor,
        CharacterCombatCommand command,
        CombatParticipantRef target,
        CombatWeaponSnapshot weapon)
    {
        if (!target.IsValid
            || affiliation.GetRelationship(new CombatParticipantRef(actor), target)
                != CombatRelationship.Hostile)
        {
            BlockCommand(command, "근접 공격할 적대 대상이 없습니다.");
            return;
        }

        CharacterBodyHealthSnapshot attackerBody = bodyHealth.GetSnapshot(actor);
        CharacterBodyHealthSnapshot defenderBody = target.IsCharacter
            ? bodyHealth.GetSnapshot(target.Character)
            : default;
        CombatAttackResult result = resolution.Resolve(new CombatAttackRequest(
            command.commandId + ":" + command.revision++,
            GetId(actor),
            target.Id,
            CombatRuntimeStatFactory.Create(actor, attackerBody),
            GetCombatStats(target),
            weapon,
            1,
            CombatFireMode.Aimed,
            default,
            defenderDowned: target.IsCharacter && defenderBody.Downed,
            defenderMeleeLocked: true,
            attackerSuppression: attackerBody.Suppression,
            defenderSuppression: target.IsCharacter ? defenderBody.Suppression : 0f,
            attackPowerMultiplier: actor.GetCombatPowerMultiplier(),
            defenderArmor: target.IsCharacter
                ? equipment.GetArmor(target.Id)
                : Array.Empty<CombatArmorSnapshot>(),
            defenderShield: target.IsCharacter
                ? equipment.GetShield(target.Id)
                : default));
        if (!result.Executed)
        {
            BlockCommand(command, result.FailureReason);
            return;
        }

        Vector3 targetWorld = target.IsCharacter
            ? target.Character.transform.position
            : target.Wildlife.transform.position;
        DefenseCombatPresentation.Ensure(actor)?.PlayAttack(targetWorld, weapon);
        ApplyCombatResult(
            target,
            result,
            actor,
            weapon.Verb?.damageType ?? CombatDamageType.Slash);
        ApplyArmorDurabilityDamage(result);
        command.attackCooldownRemaining = resolution.CalculateAttackInterval(
            CombatRuntimeStatFactory.Create(actor, attackerBody),
            weapon,
            CombatFireMode.Aimed);
        command.state = CharacterCombatCommandState.Executing;
        command.status = result.Hit ? "근접 공격 명중" : "근접 공격 빗나감";
        MarkDirty();
    }

    private bool BeginReload(
        CharacterActor actor,
        CharacterCombatCommand command,
        bool completeCommand)
    {
        string actorId = GetId(actor);
        equipment.TryGetActiveWeapon(actorId, out CombatWeaponSnapshot weapon);
        if (weapon == null
            || !weapon.RequiresAmmo
            || string.IsNullOrWhiteSpace(weapon.InstanceId)
            || weapon.LoadedAmmo >= weapon.MagazineCapacity)
        {
            if (completeCommand)
            {
                CompleteCommand(command, "이미 장전됨");
            }

            return false;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.FindByCharacterId(actorId);
        if (inventory == null || inventory.CountItem(weapon.AmmunitionItemId) <= 0)
        {
            command.state = CharacterCombatCommandState.WaitingForAmmo;
            command.status = "탄약 재보급 필요";
            MarkDirty();
            return false;
        }

        command.weaponInstanceId = weapon.InstanceId;
        command.reloadRemaining = resolution.CalculateReloadTime(
            CombatRuntimeStatFactory.Create(actor, bodyHealth.GetSnapshot(actor)),
            weapon);
        command.state = CharacterCombatCommandState.Executing;
        command.status = "재장전 중";
        command.revision = completeCommand ? -Mathf.Abs(command.revision + 1) : Mathf.Abs(command.revision);
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(command.status, combatActive: true);
        DefenseCombatPresentation.Ensure(actor)?.PlayReload(weapon, command.reloadRemaining);
        MarkDirty();
        return true;
    }

    private void CompleteReload(CharacterActor actor, CharacterCombatCommand command)
    {
        bool completeCommand = command.type == CombatCommandType.Reload || command.revision < 0;
        if (!equipment.TryReloadFromCharacterInventory(
                GetId(actor),
                command.weaponInstanceId,
                out int consumed)
            || consumed <= 0)
        {
            command.state = CharacterCombatCommandState.WaitingForAmmo;
            command.status = "탄약 재보급 필요";
            command.reloadRemaining = 0f;
            MarkDirty();
            return;
        }

        command.reloadRemaining = 0f;
        command.revision = Mathf.Abs(command.revision);
        if (completeCommand)
        {
            CompleteCommand(command, $"{consumed}발 재장전 완료");
        }
        else
        {
            command.state = CharacterCombatCommandState.Queued;
            command.status = $"{consumed}발 재장전 완료";
            MarkDirty();
        }
    }

    private void TickRescue(CharacterActor actor, CharacterCombatCommand command)
    {
        CharacterActor patient = FindCharacter(command.targetId);
        if (patient == null || patient.IsDead)
        {
            CompleteCommand(command, "구조 대상 소실");
            return;
        }

        if (patient.CurrentLifecycleState != CharacterLifecycleState.Downed)
        {
            CompleteCommand(command, "구조 대상 회복");
            return;
        }

        AbilityRescue rescue = AbilityRescue.Ensure(actor);
        if (rescue == null)
        {
            BlockCommand(command, "구조 능력 없음");
            return;
        }

        if (!rescue.IsRescuing)
        {
            actor.SetAiPaused(false);
            rescue.StartRescue(patient);
            actor.SetAiPaused(true);
            command.state = CharacterCombatCommandState.Executing;
            command.status = "구조 중";
            MarkDirty();
            return;
        }

        command.status = "구조 중";
    }

    private bool TryMoveToAttackPosition(
        Grid grid,
        CharacterActor actor,
        CharacterCombatCommand command,
        CombatWeaponSnapshot weapon,
        Vector2Int targetCell)
    {
        if (!CanRetryMove(command.actorId))
        {
            return true;
        }

        Vector2Int destination;
        if (!TryFindAttackPosition(grid, actor, weapon, targetCell, out destination))
        {
            return false;
        }

        if (destination == actor.GetNowXY())
        {
            return true;
        }

        AbilityMove move = actor.GetComponent<AbilityMove>();
        if (move == null || !move.TryStartPlayerMove(destination, out _))
        {
            nextMoveRetryAt[command.actorId] = gameClock.Time + 0.5f;
            return false;
        }

        command.state = CharacterCombatCommandState.Moving;
        command.status = weapon.IsRanged ? "사격 위치로 이동" : "근접 접근";
        nextMoveRetryAt[command.actorId] = gameClock.Time + 0.5f;
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(command.status, combatActive: true);
        MarkDirty();
        return true;
    }

    private bool TryFindAttackPosition(
        Grid grid,
        CharacterActor actor,
        CombatWeaponSnapshot weapon,
        Vector2Int targetCell,
        out Vector2Int destination)
    {
        destination = default;
        int preferredRange = weapon.IsRanged
            ? Mathf.Clamp(weapon.MaximumRange * 2 / 3, 2, weapon.MaximumRange)
            : 1;
        List<Vector2Int> candidates = new List<Vector2Int>();
        int radius = weapon.IsRanged ? weapon.MaximumRange : 1;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int remaining = radius - Mathf.Abs(dy);
            for (int dx = -remaining; dx <= remaining; dx++)
            {
                Vector2Int cell = targetCell + new Vector2Int(dx, dy);
                int distance = Manhattan(cell, targetCell);
                if (distance <= 0
                    || (!weapon.IsRanged && distance != 1)
                    || (weapon.IsRanged && distance > weapon.MaximumRange)
                    || !grid.IsValidGridPos(cell)
                    || !grid.IsWalkable(cell))
                {
                    continue;
                }

                if (weapon.IsRanged)
                {
                    CombatLineOfSightResult sight = lineOfSight.Evaluate(
                        grid,
                        cell,
                        targetCell,
                        GetId(actor),
                        string.Empty);
                    if (!sight.HasLineOfSight || sight.FriendlyFireRisk)
                    {
                        continue;
                    }
                }

                candidates.Add(cell);
            }
        }

        Vector2Int actorCell = actor.GetNowXY();
        string actorId = GetId(actor);
        candidates.RemoveAll(cell =>
            tacticalCoordinator.IsReservedForOther(actorId, cell));
        candidates.Sort((left, right) =>
        {
            int rangeComparison = Mathf.Abs(
                    Manhattan(left, targetCell) - preferredRange)
                .CompareTo(Mathf.Abs(
                    Manhattan(right, targetCell) - preferredRange));
            return rangeComparison != 0
                ? rangeComparison
                : Manhattan(actorCell, left).CompareTo(
                    Manhattan(actorCell, right));
        });

        Vector2Int? selected = null;
        for (int index = 0; index < candidates.Count; index++)
        {
            Vector2Int candidate = candidates[index];
            if (candidate == actorCell)
            {
                selected = candidate;
                break;
            }

            Queue<GridMoveStep> path = pathSearchBroker.GetMovePathTo(
                grid,
                actorCell,
                candidate);
            if (path == null)
            {
                // The broker resumes this exact A* search in a later frame.
                return false;
            }

            if (path.Count > 0)
            {
                selected = candidate;
                break;
            }
        }

        if (!selected.HasValue)
        {
            return false;
        }

        destination = selected.Value;
        return tacticalCoordinator.TryReserve(
            actorId,
            FindTargetIdAt(targetCell),
            destination,
            weapon.IsRanged
                ? CombatPositionReservationKind.Ranged
                : CombatPositionReservationKind.Melee,
            weapon.IsRanged ? weapon.MaximumRange : 1f,
            out _);
    }

    private bool TrySelectFallbackWeapon(
        CharacterActor actor,
        bool preferLoadedRanged,
        out CombatWeaponSnapshot selected)
    {
        selected = null;
        string actorId = GetId(actor);
        CharacterCombatLoadoutProfile profile = equipment.GetActiveProfileSnapshot(actorId);
        if (profile == null)
        {
            return false;
        }

        string original = profile.activeWeaponInstanceId;
        List<(string id, CombatWeaponSnapshot weapon)> options =
            new List<(string id, CombatWeaponSnapshot weapon)>();
        foreach (string instanceId in profile.weaponInstanceIds)
        {
            if (string.Equals(instanceId, original, StringComparison.Ordinal)
                || !equipment.TrySetActiveWeapon(actorId, instanceId, out _)
                || !equipment.TryGetActiveWeapon(actorId, out CombatWeaponSnapshot weapon)
                || weapon == null)
            {
                continue;
            }

            options.Add((instanceId, weapon));
        }

        (string id, CombatWeaponSnapshot weapon) choice = options
            .OrderBy(option => preferLoadedRanged
                && option.weapon.IsRanged
                && (!option.weapon.RequiresAmmo || option.weapon.LoadedAmmo > 0)
                    ? 0
                    : !option.weapon.IsRanged ? 1 : 2)
            .FirstOrDefault();
        if (choice.weapon != null
            && equipment.TrySetActiveWeapon(actorId, choice.id, out _))
        {
            selected = choice.weapon;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(original))
        {
            equipment.TrySetActiveWeapon(actorId, original, out _);
        }

        return false;
    }

    private void ApplyCombatResult(
        CombatParticipantRef target,
        CombatAttackResult result,
        CharacterActor attacker,
        CombatDamageType damageType)
    {
        if (result.CoverBlocked)
        {
            CombatCoverDurability.TryApplyDamage(result.CoverSourceId, result.CoverDamage);
            CombatImpactPresentation.Play(
                target.IsCharacter
                    ? target.Character.transform.position
                    : target.Wildlife.transform.position,
                damageType,
                gameClock,
                coverHit: true);
            if (target.IsCharacter)
            {
                bodyHealth.AddSuppression(target.Character, result.Suppression);
            }

            return;
        }

        if (target.IsCharacter)
        {
            if (result.Hit)
            {
                bodyHealth.ApplyCombatResult(
                    target.Character,
                    result,
                    $"직접 전투 명령: {GetName(attacker)}");
                DefenseCombatPresentation.Ensure(target.Character)?.PlayHit(
                    result.AppliedDamage,
                    damageType);
            }
            else
            {
                bodyHealth.AddSuppression(target.Character, result.Suppression);
            }
        }
        else if (target.IsWildlife)
        {
            target.Wildlife.ApplyCombatDamage(result, attacker);
        }
    }

    private void ApplyArmorDurabilityDamage(CombatAttackResult result)
    {
        foreach (CombatArmorDurabilityHit hit in result.ArmorDurabilityHits)
        {
            equipment.TryApplyDurabilityDamage(hit.InstanceId, hit.Damage);
        }

        if (result.ArmorDurabilityHits.Count == 0
            && !string.IsNullOrWhiteSpace(result.ArmorInstanceId))
        {
            equipment.TryApplyDurabilityDamage(
                result.ArmorInstanceId,
                result.ArmorDurabilityDamage);
        }
    }

    private void CancelCommandById(
        string actorId,
        string reason,
        bool releaseReservation = true)
    {
        if (!commands.TryGetValue(actorId, out CharacterCombatCommand command))
        {
            return;
        }

        CharacterActor actor = FindCharacter(actorId);
        actor?.GetComponent<AbilityMove>()?.CancelActiveMovement();
        AbilityRescue.Ensure(actor)?.StopRescue(reason);
        command.state = CharacterCombatCommandState.Cancelled;
        command.status = reason ?? string.Empty;
        commands.Remove(actorId);
        nextMoveRetryAt.Remove(actorId);
        if (releaseReservation)
        {
            tacticalCoordinator.Release(actorId);
        }
        MarkDirty();
    }

    private void ReleaseCombatStance(CharacterActor actor)
    {
        string id = GetId(actor);
        combatStance.Remove(id);
        actor.SetAiPaused(false);
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(string.Empty, combatActive: false);
    }

    private CharacterCombatCommand CreateCommand(
        CharacterActor actor,
        CombatCommandType type,
        bool releaseReservation = true)
    {
        string actorId = GetId(actor);
        CancelCommandById(actorId, "새 전투 명령", releaseReservation);
        commandRevisions.TryGetValue(actorId, out int revision);
        commandRevisions[actorId] = revision + 1;
        return new CharacterCombatCommand
        {
            commandId = $"combat-command:{++commandSequence}",
            actorId = actorId,
            type = type,
            state = CharacterCombatCommandState.Queued,
            revision = revision + 1
        };
    }

    private void CompleteCommand(CharacterCombatCommand command, string status)
    {
        if (command == null)
        {
            return;
        }

        command.state = CharacterCombatCommandState.Completed;
        command.status = status ?? string.Empty;
        CharacterActor actor = FindCharacter(command.actorId);
        actor?.GetComponent<AbilityMove>()?.CancelActiveMovement();
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(
            IsInCombatStance(actor) ? "전투 태세" : string.Empty,
            combatActive: IsInCombatStance(actor));
        commands.Remove(command.actorId);
        nextMoveRetryAt.Remove(command.actorId);
        tacticalCoordinator.Release(command.actorId);
        MarkDirty();
    }

    private void BlockCommand(CharacterCombatCommand command, string reason)
    {
        if (command == null)
        {
            return;
        }

        command.state = CharacterCombatCommandState.Blocked;
        command.status = string.IsNullOrWhiteSpace(reason) ? "명령 수행 불가" : reason;
        CharacterActor actor = FindCharacter(command.actorId);
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(command.status, combatActive: true);
        MarkDirty();
    }

    private bool RequireCombatStance(CharacterActor actor, out string message)
    {
        if (!CanCommand(actor, out message))
        {
            return false;
        }

        if (!IsInCombatStance(actor))
        {
            message = "먼저 전투 태세를 켜야 합니다.";
            return false;
        }

        return true;
    }

    private static bool CanCommand(CharacterActor actor, out string message)
    {
        if (actor == null || actor.IsDead)
        {
            message = "명령 가능한 캐릭터가 아닙니다.";
            return false;
        }

        if (actor.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            message = actor.CurrentLifecycleState == CharacterLifecycleState.Downed
                ? "쓰러진 캐릭터에게 명령할 수 없습니다."
                : "현재 상태에서는 전투 명령을 받을 수 없습니다.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool CanRetryMove(string actorId)
    {
        return !nextMoveRetryAt.TryGetValue(actorId, out float retryAt)
            || gameClock.Time >= retryAt;
    }

    private CombatParticipantRef FindParticipant(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return default;
        }

        CharacterActor character = FindCharacter(id);
        if (character != null)
        {
            return new CombatParticipantRef(character);
        }

        WildlifeActor wildlife = worldRegistry.Wildlife.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.WildlifeId, id, StringComparison.Ordinal));
        return wildlife != null ? new CombatParticipantRef(wildlife) : default;
    }

    private CharacterActor FindCharacter(string id)
    {
        return worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null && string.Equals(GetId(actor), id, StringComparison.Ordinal));
    }

    private string FindTargetIdAt(Vector2Int cell)
    {
        CharacterActor character = worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null && actor.GetNowXY() == cell && !actor.IsDead);
        if (character != null)
        {
            return GetId(character);
        }

        WildlifeActor wildlife = worldRegistry.Wildlife.FirstOrDefault(actor =>
            actor != null && actor.IsAlive && actor.GridPosition == cell);
        return wildlife != null ? wildlife.WildlifeId : string.Empty;
    }

    private CombatStatSnapshot GetCombatStats(CombatParticipantRef participant)
    {
        return participant.IsCharacter
            ? CombatRuntimeStatFactory.Create(
                participant.Character,
                bodyHealth.GetSnapshot(participant.Character))
            : CombatRuntimeStatFactory.Create(participant.Wildlife);
    }

    private void LaunchProjectile(
        Vector3 start,
        Vector3 end,
        CombatWeaponSnapshot weapon)
    {
        float speed = weapon?.Verb switch
        {
            ProjectileVerb projectile => projectile.projectileSpeed,
            RecoverableThrowVerb recoverable => recoverable.projectileSpeed,
            _ => 12f
        };
        CombatProjectilePresentation.Launch(
            start,
            end,
            speed,
            weapon?.Verb?.damageType ?? CombatDamageType.Pierce,
            weapon?.Kind == CombatEquipmentKind.RecoverableThrowingWeapon,
            gameClock);
    }

    private static CombatFireMode ResolveSupportedFireMode(
        CombatWeaponSnapshot weapon,
        CombatFireMode requested)
    {
        if (weapon == null || !weapon.IsRanged)
        {
            return CombatFireMode.Aimed;
        }

        return requested switch
        {
            CombatFireMode.Rapid when weapon.SupportsRapid => CombatFireMode.Rapid,
            CombatFireMode.Suppressive when weapon.SupportsSuppressive => CombatFireMode.Suppressive,
            _ => CombatFireMode.Aimed
        };
    }

    private static string GetFireModeName(CombatFireMode mode)
    {
        return mode switch
        {
            CombatFireMode.Rapid => "속사",
            CombatFireMode.Suppressive => "제압",
            _ => "조준"
        };
    }

    private static string GetId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId
            ?? (actor != null ? $"character:{actor.GetInstanceID()}" : string.Empty);
    }

    private static string GetName(CharacterActor actor)
    {
        return actor?.Identity?.DisplayName
            ?? (actor != null ? actor.name : "캐릭터");
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void MarkDirty()
    {
        viewDirty = true;
    }
}
