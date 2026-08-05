using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class OffenseBattleSession
{
    private const int MaxLogEntries = 60;
    private readonly List<OffenseBattleCombatant> combatants;
    private readonly IReadOnlyList<OffenseBattleCombatant> combatantsView;
    private readonly List<string> initiativeOrder = new List<string>();
    private readonly IReadOnlyList<string> initiativeOrderView;
    private readonly List<string> log = new List<string>();
    private readonly IReadOnlyList<string> logView;
    private readonly ICombatResolutionService combatResolution;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly Dictionary<string, string> thrownOwnerByInstance =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private bool recoverableEquipmentFinalized;
    private int currentOrderIndex = -1;

    public OffenseBattleSession(
        string battleId,
        string expeditionId,
        string targetId,
        string targetTitle,
        DungeonDifficulty difficulty,
        IEnumerable<OffenseBattleCombatant> combatants,
        ICombatResolutionService combatResolution,
        ICombatEquipmentRuntime combatEquipmentRuntime)
        : this(
            battleId,
            expeditionId,
            targetId,
            targetTitle,
            difficulty,
            combatants,
            combatResolution,
            combatEquipmentRuntime,
            true)
    {
    }

    private OffenseBattleSession(
        string battleId,
        string expeditionId,
        string targetId,
        string targetTitle,
        DungeonDifficulty difficulty,
        IEnumerable<OffenseBattleCombatant> combatants,
        ICombatResolutionService combatResolution,
        ICombatEquipmentRuntime combatEquipmentRuntime,
        bool startImmediately)
    {
        BattleId = string.IsNullOrWhiteSpace(battleId) ? Guid.NewGuid().ToString("N") : battleId;
        ExpeditionId = expeditionId ?? string.Empty;
        TargetId = targetId ?? string.Empty;
        TargetTitle = targetTitle ?? string.Empty;
        Difficulty = difficulty;
        this.combatants = combatants?
            .Where(combatant => combatant != null)
            .GroupBy(combatant => combatant.PersistentId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList() ?? new List<OffenseBattleCombatant>();
        combatantsView = this.combatants.AsReadOnly();
        initiativeOrderView = initiativeOrder.AsReadOnly();
        logView = log.AsReadOnly();
        this.combatResolution = combatResolution
            ?? throw new ArgumentNullException(nameof(combatResolution));
        this.combatEquipmentRuntime = combatEquipmentRuntime
            ?? throw new ArgumentNullException(nameof(combatEquipmentRuntime));

        Outcome = startImmediately ? ResolveOutcome() : OffenseBattleOutcome.InProgress;
        if (startImmediately && Outcome == OffenseBattleOutcome.InProgress)
        {
            RoundNumber = 1;
            BuildInitiativeOrder();
            currentOrderIndex = 0;
            PrepareCurrentTurn();
            if (!IsComplete && CurrentActor?.PinnedThisTurn == true)
            {
                AddLog($"{CurrentActor.DisplayName}은(는) 제압되어 이번 행동을 잃었습니다.");
                AdvanceTurn();
            }
            AddLog($"{TargetTitle} 전투가 시작되었습니다.");
        }
    }

    public string BattleId { get; }
    public string ExpeditionId { get; }
    public string TargetId { get; }
    public string TargetTitle { get; }
    public DungeonDifficulty Difficulty { get; }
    public OffenseBattleOutcome Outcome { get; private set; }
    public int RoundNumber { get; private set; }
    public long LastProcessedCommandId { get; private set; }
    public int CurrentOrderIndex => currentOrderIndex;
    public IReadOnlyList<OffenseBattleCombatant> Combatants => combatantsView;
    public IReadOnlyList<string> InitiativeOrder => initiativeOrderView;
    public IReadOnlyList<string> Log => logView;
    public bool IsComplete => Outcome != OffenseBattleOutcome.InProgress;
    public OffenseBattleCombatant CurrentActor => currentOrderIndex >= 0
        && currentOrderIndex < initiativeOrder.Count
            ? FindCombatant(initiativeOrder[currentOrderIndex])
            : null;

    public OffenseBattleCombatant FindCombatant(string persistentId)
    {
        return combatants.FirstOrDefault(combatant => string.Equals(
            combatant.PersistentId,
            persistentId,
            StringComparison.Ordinal));
    }

    public OffenseBattlePersistenceState CapturePersistentState()
    {
        return new OffenseBattlePersistenceState
        {
            battleId = BattleId,
            expeditionId = ExpeditionId,
            targetId = TargetId,
            targetTitle = TargetTitle,
            difficulty = Difficulty,
            outcome = Outcome,
            roundNumber = RoundNumber,
            currentOrderIndex = currentOrderIndex,
            lastProcessedCommandId = LastProcessedCommandId,
            initiativeOrder = initiativeOrder.ToList(),
            log = log.ToList(),
            thrownEquipment = thrownOwnerByInstance
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new OffenseThrownEquipmentPersistenceState
                {
                    instanceId = pair.Key,
                    ownerCharacterId = pair.Value
                })
                .ToList(),
            combatants = combatants.Select(combatant => new OffenseBattleCombatantPersistenceState
            {
                persistentId = combatant.PersistentId,
                maxHealth = combatant.Stats.MaxHealth,
                attack = combatant.Stats.Attack,
                strength = combatant.Stats.Strength,
                toughness = combatant.Stats.Toughness,
                dexterity = combatant.Stats.Dexterity,
                moveSpeed = combatant.Stats.MoveSpeed,
                shooting = combatant.Stats.Shooting,
                evasion = combatant.Stats.Evasion,
                currentHealth = combatant.CurrentHealth,
                totalDamageTaken = combatant.TotalDamageTaken,
                initiativePenalty = combatant.InitiativePenalty,
                turnsStarted = combatant.TurnsStarted,
                formation = combatant.Formation,
                suppression = combatant.Suppression,
                bloodLoss = combatant.BloodLoss,
                lastHitBodyPart = combatant.LastHitBodyPart,
                fireMode = combatant.FireMode,
                bodyParts = combatant.BodyParts
                    .Select(part => new CharacterBodyPartHealthState
                    {
                        bodyPart = part.bodyPart,
                        maxHealth = part.maxHealth,
                        currentHealth = part.currentHealth,
                        bleedingPerSecond = part.bleedingPerSecond
                    })
                    .ToList(),
                cooldowns = combatant.GetCooldownSnapshot()
                    .Where(pair => pair.Value > 0)
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new OffenseBattleCooldownPersistenceState
                    {
                        abilityId = pair.Key,
                        remainingTurns = pair.Value
                    })
                    .ToList(),
                statuses = combatant.Statuses.Select(status => new OffenseBattleStatusPersistenceState
                {
                    id = status.Id,
                    type = status.Type,
                    value = status.Value,
                    remainingTurns = status.RemainingTurns,
                    sourceId = status.SourceId
                }).ToList()
            }).ToList()
        };
    }

    public static OffenseBattleSession Restore(
        OffenseBattlePersistenceState state,
        IEnumerable<OffenseBattleCombatant> configuredCombatants,
        ICombatResolutionService combatResolution,
        ICombatEquipmentRuntime combatEquipmentRuntime)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        OffenseBattleSession session = new OffenseBattleSession(
            state.battleId,
            state.expeditionId,
            state.targetId,
            state.targetTitle,
            state.difficulty,
            configuredCombatants,
            combatResolution,
            combatEquipmentRuntime,
            false);
        foreach (OffenseBattleCombatantPersistenceState saved in state.combatants
            ?? new List<OffenseBattleCombatantPersistenceState>())
        {
            OffenseBattleCombatant combatant = session.FindCombatant(saved?.persistentId);
            if (combatant == null) continue;

            if (saved.maxHealth > 0f)
            {
                combatant.RestoreStats(new OffenseBattleStats(
                    saved.maxHealth,
                    saved.attack,
                    saved.strength,
                    saved.toughness,
                    saved.dexterity,
                    saved.moveSpeed,
                    saved.shooting,
                    saved.evasion));
            }
            combatant.RestoreHealth(saved.currentHealth, saved.totalDamageTaken);
            combatant.RestoreInitiativePenalty(saved.initiativePenalty);
            combatant.RestoreTurnsStarted(saved.turnsStarted);
            combatant.RestoreFormation(saved.formation);
            combatant.RestoreCombatState(
                saved.suppression,
                saved.bloodLoss,
                saved.lastHitBodyPart,
                saved.fireMode);
            if (saved.bodyParts != null && saved.bodyParts.Count > 0)
            {
                combatant.ApplyBodyHealth(new CharacterBodyHealthSnapshot(
                    saved.bodyParts,
                    saved.bloodLoss,
                    saved.suppression,
                    1f,
                    1f,
                    1f,
                    false));
            }
            combatant.RestoreCooldowns((saved.cooldowns
                    ?? new List<OffenseBattleCooldownPersistenceState>())
                .Where(value => value != null)
                .Select(value => new KeyValuePair<string, int>(value.abilityId, value.remainingTurns)));
            combatant.RestoreStatuses((saved.statuses
                    ?? new List<OffenseBattleStatusPersistenceState>())
                .Where(value => value != null)
                .Select(value => new OffenseBattleStatus(
                    value.id,
                    value.type,
                    value.value,
                    value.remainingTurns,
                    value.sourceId)));
        }

        session.RestoreTurnState(
            state.roundNumber,
            state.initiativeOrder,
            state.currentOrderIndex,
            state.lastProcessedCommandId,
            state.outcome,
            state.log);
        foreach (OffenseThrownEquipmentPersistenceState thrown in state.thrownEquipment
            ?? new List<OffenseThrownEquipmentPersistenceState>())
        {
            if (thrown != null && !string.IsNullOrWhiteSpace(thrown.instanceId))
            {
                session.thrownOwnerByInstance[thrown.instanceId] =
                    thrown.ownerCharacterId ?? string.Empty;
            }
        }

        session.FinalizeRecoverableEquipment();
        return session;
    }

    public bool TryExecuteCommand(
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result)
    {
        if (command == null)
        {
            result = new OffenseBattleCommandResult(false, "명령이 없습니다.");
            return false;
        }

        if (IsComplete)
        {
            result = new OffenseBattleCommandResult(false, "이미 끝난 전투입니다.");
            return false;
        }

        if (command.CommandId <= LastProcessedCommandId)
        {
            result = new OffenseBattleCommandResult(false, "이미 처리한 명령입니다.");
            return false;
        }

        OffenseBattleCombatant actor = CurrentActor;
        if (actor == null || !actor.CanTakeTurn
            || !string.Equals(actor.PersistentId, command.ActorId, StringComparison.Ordinal))
        {
            result = new OffenseBattleCommandResult(false, "현재 행동할 캐릭터가 아닙니다.");
            return false;
        }

        if (!TryResolveCommand(actor, command, out result))
        {
            return false;
        }

        LastProcessedCommandId = command.CommandId;
        Outcome = ResolveOutcome();
        if (!IsComplete)
        {
            AdvanceTurn();
        }

        FinalizeRecoverableEquipment();
        return true;
    }

    public bool TryExecutePlannedCommand(
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result)
    {
        if (command == null)
        {
            result = new OffenseBattleCommandResult(false, "명령이 없습니다.");
            return false;
        }

        if (IsComplete)
        {
            result = new OffenseBattleCommandResult(false, "이미 끝난 전투입니다.");
            return false;
        }

        if (command.CommandId <= LastProcessedCommandId)
        {
            result = new OffenseBattleCommandResult(false, "이미 처리한 명령입니다.");
            return false;
        }

        OffenseBattleCombatant actor = FindCombatant(command.ActorId);
        if (actor == null || !actor.CanTakeTurn)
        {
            result = new OffenseBattleCommandResult(
                false,
                "행동 가능한 전투원이 아닙니다.");
            return false;
        }

        if (!TryResolveCommand(actor, command, out result))
        {
            return false;
        }

        LastProcessedCommandId = command.CommandId;
        Outcome = ResolveOutcome();
        FinalizeRecoverableEquipment();
        return true;
    }

    public OffenseBattleCommand CreateEnemyCommand(long commandId)
    {
        OffenseBattleCombatant actor = CurrentActor;
        if (actor == null || actor.Team != OffenseBattleTeam.Enemies || !actor.CanTakeTurn)
        {
            return null;
        }

        List<OffenseBattleCombatant> targets = combatants
            .Where(target => target.Team == OffenseBattleTeam.Allies
                && !target.IsDead
                && !target.IsDowned)
            .Where(target => IsReachableByBasicAttack(actor, target))
            .OrderBy(target => WouldBasicAttackKill(actor, target) ? 0 : 1)
            .ThenBy(target => target.HealthRatio)
            .ThenByDescending(target => target.Stats.Attack)
            .ThenBy(target => target.PersistentId, StringComparer.Ordinal)
            .ToList();
        OffenseBattleCombatant target = targets.FirstOrDefault();
        if (target == null)
        {
            return null;
        }

        CharacterCombatAbilityDefinition bestAbility = actor.Abilities
            .Where(ability => actor.GetCooldown(ability.Id) <= 0
                && ability.TargetRule == OffenseBattleTargetRule.Enemy
                && IsPositionAllowed(ability.UsableFrom, actor.Formation)
                && combatants.Any(candidate => IsAbilityTargetValid(actor, candidate, ability)))
            .OrderByDescending(OffenseBattleSessionRules.EstimateAbilityDamageMultiplier)
            .ThenBy(ability => ability.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (bestAbility != null
            && OffenseBattleSessionRules.EstimateAbilityDamageMultiplier(bestAbility) > 1.05f)
        {
            OffenseBattleCombatant abilityTarget = combatants
                .Where(candidate => IsAbilityTargetValid(actor, candidate, bestAbility))
                .OrderBy(candidate => candidate.HealthRatio)
                .ThenBy(candidate => candidate.PersistentId, StringComparer.Ordinal)
                .First();
            return new OffenseBattleCommand(
                commandId,
                actor.PersistentId,
                OffenseBattleActionType.Ability,
                abilityTarget.PersistentId,
                bestAbility.Id);
        }

        if (actor.HealthRatio < 0.25f)
        {
            return new OffenseBattleCommand(
                commandId,
                actor.PersistentId,
                OffenseBattleActionType.Guard,
                actor.PersistentId);
        }

        return new OffenseBattleCommand(
            commandId,
            actor.PersistentId,
            OffenseBattleActionType.BasicAttack,
            target.PersistentId);
    }

    public float CalculateBasicDamage(OffenseBattleCombatant source, OffenseBattleCombatant target)
    {
        if (source == null || target == null)
        {
            return 0f;
        }

        float attackMultiplier = 1f + source.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.AttackModifier)
            .Sum(status => status.Value);
        CombatWeaponSnapshot weapon = source.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        CombatAttackVerb verb = weapon.Verb ?? CombatWeaponSnapshot.CreateUnarmed().Verb;
        CombatRangeBand band = CombatRangeRules.GetBand(
            OffenseBattleSessionRules.GetFormationDistance(source, target));
        float rangeDamage = Mathf.Max(0.1f, weapon.GetDamageMultiplier(band));
        return Mathf.Max(
            1f,
            (verb.baseDamage
                + (weapon.IsRanged ? source.Stats.Shooting * 0.45f : source.Stats.Attack * 0.75f)
                + source.Stats.Strength * 0.35f)
            * rangeDamage
            * Mathf.Max(0.1f, attackMultiplier)
            - target.Stats.Toughness * 0.2f);
    }

    public CombatAttackPreview PreviewBasicAttack(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        if (source == null || target == null)
        {
            return new CombatAttackPreview(
                false,
                "공격 대상을 선택하세요.",
                CombatRangeBand.OutOfRange,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f);
        }

        int distance = OffenseBattleSessionRules.GetFormationDistance(source, target);
        return combatResolution.Preview(new CombatAttackRequest(
            $"{BattleId}:preview",
            source.PersistentId,
            target.PersistentId,
            OffenseBattleSessionRules.CreateCombatStats(source),
            OffenseBattleSessionRules.CreateCombatStats(target),
            source.Weapon ?? CombatWeaponSnapshot.CreateUnarmed(),
            distance,
            source.FireMode,
            target.CoverBlockChance > 0f
                ? new CombatCoverSnapshot(
                    CombatCoverHeight.Low,
                    target.CoverBlockChance,
                    0f,
                    "offense-cover")
                : default,
            defenderDowned: target.IsDowned,
            defenderMeleeLocked: distance <= 1,
            attackerSuppression: source.Suppression,
            defenderSuppression: target.Suppression,
            defenderArmor: target.Armor));
    }

    public int GetFormationDistanceForPreview(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        return source == null || target == null
            ? 0
            : OffenseBattleSessionRules.GetFormationDistance(source, target);
    }

    internal float ApplyDamage(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target,
        float rawAmount)
    {
        if (target == null || target.IsDead)
        {
            return 0f;
        }

        float guard = target.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Guard)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float vulnerability = target.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Vulnerability)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float finalAmount = Mathf.Max(1f, rawAmount * (1f - Mathf.Clamp01(guard)) * (1f + vulnerability));
        float applied = target.ApplyRawDamage(finalAmount);
        if (target.IsDead)
        {
            AddLog($"{target.DisplayName}이(가) 쓰러졌습니다.");
            CompactFormation(target.Team);
        }
        else if (target.IsDowned)
        {
            AddLog($"{target.DisplayName}은(는) 부상으로 쓰러졌습니다.");
            CompactFormation(target.Team);
        }

        return applied;
    }

    internal float Heal(OffenseBattleCombatant target, float amount)
    {
        return target?.Heal(amount) ?? 0f;
    }

    internal void AddStatus(
        OffenseBattleCombatant target,
        OffenseBattleStatusType type,
        float value,
        int turns,
        string sourceId,
        string statusId)
    {
        target?.AddStatus(new OffenseBattleStatus(statusId, type, value, turns, sourceId));
    }

    internal void Delay(OffenseBattleCombatant target, float amount)
    {
        target?.AddInitiativePenalty(amount);
    }

    internal int Cleanse(OffenseBattleCombatant target, int maximum)
    {
        return target?.RemoveStatuses(
            status => status.Type == OffenseBattleStatusType.Vulnerability
                || status.Type == OffenseBattleStatusType.DamageOverTime
                || (status.Type == OffenseBattleStatusType.AttackModifier && status.Value < 0f),
            maximum) ?? 0;
    }

    internal void AdjustCooldowns(OffenseBattleCombatant target, int delta)
    {
        target?.AdjustCooldowns(delta);
    }

    internal IReadOnlyList<OffenseBattleCombatant> GetLivingTeam(OffenseBattleTeam team)
    {
        return combatants.Where(combatant => combatant.Team == team && !combatant.IsDead).ToArray();
    }

    internal void Reposition(OffenseBattleCombatant target, int offset)
    {
        if (target == null || offset == 0)
        {
            return;
        }

        int next = Mathf.Clamp((int)target.Formation + offset, 0, 2);
        target.SetFormation((OffenseFormationSlot)next);
    }

    public void AbortForOwnerDeath()
    {
        if (IsComplete)
        {
            return;
        }

        Outcome = OffenseBattleOutcome.AbortedOwnerDeath;
        AddLog("사장이 쓰러져 원정 전투가 중단되었습니다.");
    }

    internal void RestoreTurnState(
        int roundNumber,
        IEnumerable<string> restoredInitiativeOrder,
        int restoredCurrentOrderIndex,
        long lastProcessedCommandId,
        OffenseBattleOutcome outcome,
        IEnumerable<string> restoredLog)
    {
        RoundNumber = Mathf.Max(1, roundNumber);
        initiativeOrder.Clear();
        initiativeOrder.AddRange((restoredInitiativeOrder ?? Array.Empty<string>())
            .Where(id => FindCombatant(id) != null)
            .Distinct(StringComparer.Ordinal));
        if (initiativeOrder.Count == 0 && outcome == OffenseBattleOutcome.InProgress)
        {
            BuildInitiativeOrder();
        }

        currentOrderIndex = Mathf.Clamp(restoredCurrentOrderIndex, 0, Mathf.Max(0, initiativeOrder.Count - 1));
        LastProcessedCommandId = Math.Max(0, lastProcessedCommandId);
        Outcome = outcome;
        log.Clear();
        log.AddRange((restoredLog ?? Array.Empty<string>()).TakeLast(MaxLogEntries));
    }

    private bool TryResolveCommand(
        OffenseBattleCombatant actor,
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result)
    {
        switch (command.ActionType)
        {
            case OffenseBattleActionType.BasicAttack:
                return TryBasicAttack(actor, command.TargetId, out result);
            case OffenseBattleActionType.Guard:
                AddStatus(
                    actor,
                    OffenseBattleStatusType.Guard,
                    0.5f,
                    1,
                    actor.PersistentId,
                    $"common-guard:{actor.PersistentId}");
                AddLog($"{actor.DisplayName}이(가) 방어 태세를 취했습니다.");
                result = new OffenseBattleCommandResult(true, "방어 태세");
                return true;
            case OffenseBattleActionType.Retreat:
                if (actor.Team != OffenseBattleTeam.Allies)
                {
                    result = new OffenseBattleCommandResult(false, "적은 후퇴 명령을 사용할 수 없습니다.");
                    return false;
                }

                Outcome = OffenseBattleOutcome.Retreated;
                AddLog($"{actor.DisplayName}의 지시로 원정대가 후퇴했습니다.");
                result = new OffenseBattleCommandResult(true, "후퇴했습니다.");
                return true;
            case OffenseBattleActionType.Ability:
                return TryUseAbility(actor, command.TargetId, command.AbilityId, out result);
            case OffenseBattleActionType.Reload:
                return TryReloadWeapon(actor, out result);
            case OffenseBattleActionType.SwitchWeapon:
                return TrySwitchWeapon(actor, command.AbilityId, out result);
            case OffenseBattleActionType.SetFireMode:
                return TrySetFireMode(actor, command.AbilityId, out result);
            default:
                result = new OffenseBattleCommandResult(false, "지원하지 않는 행동입니다.");
                return false;
        }
    }

    private bool TryBasicAttack(
        OffenseBattleCombatant actor,
        string targetId,
        out OffenseBattleCommandResult result)
    {
        OffenseBattleCombatant target = FindCombatant(targetId);
        if (!IsValidTarget(actor, target, OffenseBattleTargetRule.Enemy)
            || !IsReachableByBasicAttack(actor, target))
        {
            result = new OffenseBattleCommandResult(false, "공격할 수 없는 대상입니다.");
            return false;
        }

        CombatWeaponSnapshot weapon = actor.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        int distance = OffenseBattleSessionRules.GetFormationDistance(actor, target);
        CombatAttackResult resolved = combatResolution.Resolve(new CombatAttackRequest(
            $"{BattleId}:{LastProcessedCommandId + 1}:basic",
            actor.PersistentId,
            target.PersistentId,
            OffenseBattleSessionRules.CreateCombatStats(actor),
            OffenseBattleSessionRules.CreateCombatStats(target),
            weapon,
            distance,
            actor.FireMode,
            target.CoverBlockChance > 0f
                ? new CombatCoverSnapshot(CombatCoverHeight.Low, target.CoverBlockChance, 0f, "offense-cover")
                : default,
            defenderDowned: target.HealthRatio <= 0.15f,
            defenderMeleeLocked: distance <= 1,
            attackerSuppression: actor.Suppression,
            defenderSuppression: target.Suppression,
            defenderArmor: target.Armor));
        if (!resolved.Executed)
        {
            result = new OffenseBattleCommandResult(false, resolved.FailureReason);
            return false;
        }

        if (weapon.RequiresAmmo && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            combatEquipmentRuntime?.TryConsumeLoadedAmmo(weapon.InstanceId);
            if (combatEquipmentRuntime != null
                && combatEquipmentRuntime.TryGetActiveWeapon(
                    actor.PersistentId,
                    out CombatWeaponSnapshot refreshed))
            {
                actor.SetCombatEquipment(refreshed, actor.Armor);
            }
        }
        else if (weapon.Verb?.DropsWeaponOnUse == true
            && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            thrownOwnerByInstance[weapon.InstanceId] = actor.PersistentId;
            actor.SetCombatEquipment(CombatWeaponSnapshot.CreateUnarmed(), actor.Armor);
        }

        float damage = ApplyResolvedCombatDamage(actor, target, resolved);
        string combatMessage = resolved.Hit
            ? $"{actor.DisplayName}이(가) {target.DisplayName}의 {OffenseBattleSessionRules.GetBodyPartName(resolved.BodyPart)}에 {damage:0.#} 피해를 줬습니다."
            : resolved.CoverBlocked
                ? $"{target.DisplayName}이(가) 엄폐물 뒤에서 공격을 피했습니다."
                : resolved.Evaded
                    ? $"{target.DisplayName}이(가) 공격을 회피했습니다."
                    : $"{actor.DisplayName}의 공격이 빗나갔습니다.";
        AddLog(combatMessage);
        result = new OffenseBattleCommandResult(true, combatMessage, damage);
        return true;
    }

    private bool TryReloadWeapon(
        OffenseBattleCombatant actor,
        out OffenseBattleCommandResult result)
    {
        CombatWeaponSnapshot weapon = actor?.Weapon;
        if (actor == null
            || weapon == null
            || !weapon.RequiresAmmo
            || string.IsNullOrWhiteSpace(weapon.InstanceId)
            || combatEquipmentRuntime == null
            || !combatEquipmentRuntime.TryReloadFromCharacterInventory(
                actor.PersistentId,
                weapon.InstanceId,
                out int consumedAmmo)
            || consumedAmmo <= 0
            || !combatEquipmentRuntime.TryGetActiveWeapon(
                actor.PersistentId,
                out CombatWeaponSnapshot refreshed))
        {
            result = new OffenseBattleCommandResult(false, "재장전할 탄약이 없습니다.");
            return false;
        }

        actor.SetCombatEquipment(refreshed, actor.Armor);
        result = new OffenseBattleCommandResult(
            true,
            $"{actor.DisplayName}이(가) {consumedAmmo}발을 재장전했습니다.");
        return true;
    }

    private bool TrySwitchWeapon(
        OffenseBattleCombatant actor,
        string instanceId,
        out OffenseBattleCommandResult result)
    {
        string failureReason = string.Empty;
        if (actor == null
            || combatEquipmentRuntime == null
            || !combatEquipmentRuntime.TrySetActiveWeapon(
                actor.PersistentId,
                instanceId,
                out failureReason)
            || !combatEquipmentRuntime.TryGetActiveWeapon(
                actor.PersistentId,
                out CombatWeaponSnapshot weapon))
        {
            result = new OffenseBattleCommandResult(
                false,
                string.IsNullOrWhiteSpace(failureReason)
                    ? "교체할 무기가 없습니다."
                    : failureReason);
            return false;
        }

        actor.SetCombatEquipment(weapon, actor.Armor);
        result = new OffenseBattleCommandResult(
            true,
            $"{actor.DisplayName}이(가) 무기를 교체했습니다.");
        return true;
    }

    private static bool TrySetFireMode(
        OffenseBattleCombatant actor,
        string modeId,
        out OffenseBattleCommandResult result)
    {
        if (actor == null
            || !Enum.TryParse(modeId, ignoreCase: true, out CombatFireMode mode)
            || !SupportsFireMode(actor.Weapon, mode))
        {
            result = new OffenseBattleCommandResult(false, "이 무기로 사용할 수 없는 사격 모드입니다.");
            return false;
        }

        actor.SetFireMode(mode);
        result = new OffenseBattleCommandResult(
            true,
            $"사격 모드를 {GetFireModeName(mode)}(으)로 변경했습니다.");
        return true;
    }

    private static bool SupportsFireMode(CombatWeaponSnapshot weapon, CombatFireMode mode)
    {
        if (weapon == null || !weapon.IsRanged)
        {
            return false;
        }

        return mode switch
        {
            CombatFireMode.Aimed => weapon.SupportsAimed,
            CombatFireMode.Rapid => weapon.SupportsRapid,
            CombatFireMode.Suppressive => weapon.SupportsSuppressive,
            _ => false
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

    private void FinalizeRecoverableEquipment()
    {
        if (!IsComplete || recoverableEquipmentFinalized)
        {
            return;
        }

        recoverableEquipmentFinalized = true;
        bool battleRecovered = Outcome == OffenseBattleOutcome.Victory;
        foreach (KeyValuePair<string, string> thrown in thrownOwnerByInstance)
        {
            OffenseBattleCombatant owner = FindCombatant(thrown.Value);
            if (!battleRecovered || owner == null || owner.IsDead)
            {
                combatEquipmentRuntime?.TryMarkLost(thrown.Key);
            }
        }
    }

    private bool TryUseAbility(
        OffenseBattleCombatant actor,
        string targetId,
        string abilityId,
        out OffenseBattleCommandResult result)
    {
        CharacterCombatAbilityDefinition ability = actor.Abilities.FirstOrDefault(value => string.Equals(
            value.Id,
            abilityId,
            StringComparison.Ordinal));
        if (ability == null)
        {
            result = new OffenseBattleCommandResult(false, "사용할 수 없는 능력입니다.");
            return false;
        }

        int cooldown = actor.GetCooldown(ability.Id);
        if (cooldown > 0)
        {
            result = new OffenseBattleCommandResult(false, $"재사용까지 {cooldown}턴 남았습니다.");
            return false;
        }

        if (!IsPositionAllowed(ability.UsableFrom, actor.Formation))
        {
            result = new OffenseBattleCommandResult(
                false,
                $"{OffenseFormationUtility.GetDisplayName(actor.Formation)}에서는 이 능력을 사용할 수 없습니다.");
            return false;
        }

        OffenseBattleCombatant target = ability.TargetRule == OffenseBattleTargetRule.Self
            ? actor
            : FindCombatant(targetId);
        if (!IsAbilityTargetValid(actor, target, ability))
        {
            result = new OffenseBattleCommandResult(false, "능력을 사용할 수 없는 대상입니다.");
            return false;
        }

        OffenseBattleEffectContext context = new OffenseBattleEffectContext(this, actor, target);
        foreach (OffenseCombatEffectModule effect in ability.Effects)
        {
            OffenseCombatEffectRuntime.Apply(effect, context);
        }

        actor.SetCooldown(ability.Id, ability.CooldownTurns);
        AddLog($"{actor.DisplayName}이(가) {ability.DisplayName}을(를) 사용했습니다.");
        result = new OffenseBattleCommandResult(true, ability.DisplayName, context.DamageDealt);
        return true;
    }

    private void AdvanceTurn()
    {
        int attempts = 0;
        do
        {
            currentOrderIndex++;
            if (currentOrderIndex >= initiativeOrder.Count)
            {
                RoundNumber++;
                BuildInitiativeOrder();
                currentOrderIndex = 0;
            }

            PrepareCurrentTurn();
            Outcome = ResolveOutcome();
            if (!IsComplete && CurrentActor?.PinnedThisTurn == true)
            {
                AddLog($"{CurrentActor.DisplayName}은(는) 제압되어 이번 행동을 잃었습니다.");
            }
            attempts++;
        }
        while (!IsComplete
            && (CurrentActor == null || !CurrentActor.CanTakeTurn)
            && attempts <= combatants.Count * 2);
    }

    private void PrepareCurrentTurn()
    {
        OffenseBattleCombatant actor = CurrentActor;
        if (actor == null || actor.IsDead || actor.IsDowned)
        {
            return;
        }

        actor.BeginTurn();
        foreach (OffenseBattleStatus status in actor.Statuses.ToArray())
        {
            if (status.Type == OffenseBattleStatusType.DamageOverTime)
            {
                OffenseBattleCombatant source = FindCombatant(status.SourceId);
                float damage = ApplyDamage(source, actor, status.Value);
                AddLog($"{actor.DisplayName}이(가) 지속 피해 {damage:0.#}을 받았습니다.");
            }

            if (status.ConsumeTurn())
            {
                actor.RemoveStatus(status);
            }
        }
    }

    private void BuildInitiativeOrder()
    {
        initiativeOrder.Clear();
        initiativeOrder.AddRange(combatants
            .Where(combatant => !combatant.IsDead && !combatant.IsDowned)
            .OrderByDescending(combatant => combatant.Initiative)
            .ThenBy(combatant => combatant.PersistentId, StringComparer.Ordinal)
            .Select(combatant => combatant.PersistentId));
    }

    private OffenseBattleOutcome ResolveOutcome()
    {
        bool alliesAlive = combatants.Any(combatant =>
            combatant.Team == OffenseBattleTeam.Allies
            && !combatant.IsDead
            && !combatant.IsDowned);
        bool enemiesAlive = combatants.Any(combatant =>
            combatant.Team == OffenseBattleTeam.Enemies
            && !combatant.IsDead
            && !combatant.IsDowned);
        if (!alliesAlive) return OffenseBattleOutcome.Defeat;
        if (!enemiesAlive) return OffenseBattleOutcome.Victory;
        return Outcome is OffenseBattleOutcome.Retreated or OffenseBattleOutcome.AbortedOwnerDeath
            ? Outcome
            : OffenseBattleOutcome.InProgress;
    }

    private bool IsValidTarget(
        OffenseBattleCombatant actor,
        OffenseBattleCombatant target,
        OffenseBattleTargetRule rule)
    {
        if (actor == null || target == null || target.IsDead)
        {
            return false;
        }

        return rule switch
        {
            OffenseBattleTargetRule.Self => ReferenceEquals(actor, target),
            OffenseBattleTargetRule.Ally => actor.Team == target.Team,
            OffenseBattleTargetRule.Enemy => actor.Team != target.Team && !target.IsDowned,
            _ => false
        };
    }

    private bool IsAbilityTargetValid(
        OffenseBattleCombatant actor,
        OffenseBattleCombatant target,
        CharacterCombatAbilityDefinition ability)
    {
        return ability != null
            && IsValidTarget(actor, target, ability.TargetRule)
            && IsPositionAllowed(ability.TargetPositions, target.Formation);
    }

    private bool IsReachableByBasicAttack(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        if (source == null || target == null || target.IsDead || target.IsDowned)
        {
            return false;
        }

        CombatWeaponSnapshot weapon = source.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        int distance = OffenseBattleSessionRules.GetFormationDistance(source, target);
        if (distance > weapon.MaximumRange
            || weapon.GetAccuracyMultiplier(CombatRangeRules.GetBand(distance)) <= 0f)
        {
            return false;
        }

        if (weapon.IsRanged)
        {
            return true;
        }

        bool hasForwardTarget = combatants.Any(candidate => candidate.Team == target.Team
            && !candidate.IsDead
            && !candidate.IsDowned
            && candidate.Formation != OffenseFormationSlot.Rear);
        return !hasForwardTarget || target.Formation != OffenseFormationSlot.Rear;
    }

    private static bool IsPositionAllowed(OffenseFormationMask mask, OffenseFormationSlot slot)
    {
        return (mask & OffenseFormationUtility.ToMask(slot)) != 0;
    }

    private void CompactFormation(OffenseBattleTeam team)
    {
        OffenseBattleCombatant[] survivors = combatants
            .Where(combatant => combatant.Team == team
                && !combatant.IsDead
                && !combatant.IsDowned)
            .OrderBy(combatant => combatant.Formation)
            .ThenBy(combatant => combatant.PersistentId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < survivors.Length; index++)
        {
            survivors[index].RestoreFormation(
                (OffenseFormationSlot)Mathf.Clamp(index / 2, 0, 2));
        }
    }

    private bool WouldBasicAttackKill(OffenseBattleCombatant source, OffenseBattleCombatant target)
    {
        return CalculateBasicDamage(source, target) >= target.CurrentHealth;
    }

    private float ApplyResolvedCombatDamage(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target,
        CombatAttackResult resolved)
    {
        if (!resolved.Hit)
        {
            target.ApplyCombatInjury(resolved);
            return 0f;
        }

        float guard = target.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Guard)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float vulnerability = target.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Vulnerability)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float adjustedDamage = Mathf.Max(
            0.5f,
            resolved.AppliedDamage
            * (1f - Mathf.Clamp01(guard))
            * (1f + vulnerability));
        CombatAttackResult adjusted = new CombatAttackResult(
            resolved.Executed,
            resolved.Hit,
            resolved.CoverBlocked,
            resolved.Evaded,
            resolved.BodyPart,
            resolved.RawDamage,
            adjustedDamage,
            resolved.Bleeding,
            resolved.Suppression,
            resolved.ArmorDurabilityDamage,
            resolved.ArmorInstanceId,
            resolved.FailureReason,
            resolved.ShieldBlocked,
            resolved.CoverSourceId,
            resolved.CoverDamage,
            resolved.ArmorDurabilityHits);
        float applied = target.ApplyCombatInjury(adjusted);
        if (resolved.ArmorDurabilityHits.Count > 0)
        {
            for (int i = 0; i < resolved.ArmorDurabilityHits.Count; i++)
            {
                CombatArmorDurabilityHit hit = resolved.ArmorDurabilityHits[i];
                combatEquipmentRuntime?.TryApplyDurabilityDamage(hit.InstanceId, hit.Damage);
            }
        }
        else if (!string.IsNullOrWhiteSpace(resolved.ArmorInstanceId))
        {
            combatEquipmentRuntime?.TryApplyDurabilityDamage(
                resolved.ArmorInstanceId,
                resolved.ArmorDurabilityDamage);
        }

        if (target.IsDead)
        {
            AddLog($"{target.DisplayName}이(가) 쓰러졌습니다.");
            CompactFormation(target.Team);
        }

        return applied;
    }

    private void AddLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        log.Add(message);
        if (log.Count > MaxLogEntries)
        {
            log.RemoveRange(0, log.Count - MaxLogEntries);
        }
    }
}
