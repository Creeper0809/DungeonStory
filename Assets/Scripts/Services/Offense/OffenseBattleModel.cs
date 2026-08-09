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
    private readonly OffenseBattleEncounterRules encounterRules;
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
        ICombatEquipmentRuntime combatEquipmentRuntime,
        OffenseBattleEncounterRules encounterRules = null)
        : this(
            battleId,
            expeditionId,
            targetId,
            targetTitle,
            difficulty,
            combatants,
            combatResolution,
            combatEquipmentRuntime,
            encounterRules,
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
        OffenseBattleEncounterRules encounterRules,
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
        this.encounterRules = encounterRules ?? new OffenseBattleEncounterRules(
            OffenseEncounterObjective.DefeatAll,
            0,
            string.Empty,
            string.Empty,
            Array.Empty<BattlefieldModifierDefinitionSO>());
        this.encounterRules.ResolveProtectedCombatant(this.combatants);
        this.encounterRules.EvaluatePartyCounters(this.combatants);

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
            AddLog(CreateObjectiveLog());
        }
    }

    public string BattleId { get; }
    public string ExpeditionId { get; }
    public string TargetId { get; }
    public string TargetTitle { get; }
    public DungeonDifficulty Difficulty { get; }
    public OffenseBattleOutcome Outcome { get; private set; }
    public OffenseBattleEncounterRules EncounterRules => encounterRules;
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
                participatesInInitiative = combatant.ParticipatesInInitiative,
                currentHealth = combatant.CurrentHealth,
                totalDamageTaken = combatant.TotalDamageTaken,
                initiativePenalty = combatant.InitiativePenalty,
                coverBlockChance = combatant.CoverBlockChance,
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
        ICombatEquipmentRuntime combatEquipmentRuntime,
        OffenseBattleEncounterRules encounterRules = null)
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
            encounterRules,
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
            combatant.SetCover(saved.coverBlockChance);
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
            * encounterRules.GetDamageMultiplier(source.Team)
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
        CombatWeaponSnapshot weapon = source.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        return combatResolution.Preview(new CombatAttackRequest(
            $"{BattleId}:preview",
            source.PersistentId,
            target.PersistentId,
            CreateModifiedCombatStats(source, attacking: true),
            CreateModifiedCombatStats(target, attacking: false),
            weapon,
            distance,
            source.FireMode,
            CreateEffectiveCover(target),
            defenderDowned: target.IsDowned,
            defenderMeleeLocked: distance <= 1,
            attackerSuppression: source.Suppression,
            defenderSuppression: target.Suppression,
            lightMultiplier: GetVisibilityMultiplier(source, target, weapon),
            attackPowerMultiplier: GetAttackMultiplier(source),
            defenderArmor: target.Armor,
            defenderShield: target.Shield,
            defenderConstruct: IsConstruct(target)));
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
        float finalAmount = Mathf.Max(
            1f,
            rawAmount
            * encounterRules.GetDamageMultiplier(source?.Team ?? target.Team)
            * (1f - Mathf.Clamp01(guard))
            * (1f + vulnerability));
        finalAmount = Mathf.Max(
            0f,
            finalAmount - AbsorbWithSummonedGuard(target, finalAmount));
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

    internal void ApplySmoke(
        OffenseBattleCombatant center,
        float obscuration,
        int turns,
        string sourceId)
    {
        if (center == null)
        {
            return;
        }

        foreach (OffenseBattleCombatant combatant in GetLivingTeam(center.Team))
        {
            AddStatus(
                combatant,
                OffenseBattleStatusType.SmokeObscured,
                Mathf.Clamp(obscuration, 0.1f, 0.8f),
                Mathf.Max(1, turns),
                sourceId,
                $"smoke:{sourceId}:{combatant.PersistentId}");
        }
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
                || status.Type == OffenseBattleStatusType.Sedated
                || status.Type == OffenseBattleStatusType.ManaBlocked
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
                if ((actor.Shield.RoleFlags
                        & CombatEquipmentRoleFlags.DeployableCover) != 0
                    && actor.CoverBlockChance <= 0f)
                {
                    return TryDeployCover(actor, out result);
                }
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

                if (encounterRules.Objective == OffenseEncounterObjective.Escape)
                {
                    if (RoundNumber < encounterRules.RoundLimit)
                    {
                        result = new OffenseBattleCommandResult(
                            false,
                            $"탈출로 확보까지 {encounterRules.RoundLimit - RoundNumber}라운드 남았습니다.");
                        return false;
                    }

                    Outcome = OffenseBattleOutcome.Victory;
                    AddLog($"{actor.DisplayName}의 지시로 원정대가 탈출로를 돌파했습니다.");
                    result = new OffenseBattleCommandResult(true, "전장에서 탈출했습니다.");
                    return true;
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
            case OffenseBattleActionType.DeployCover:
                return TryDeployCover(actor, out result);
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
        ConsumePoweredEquipment(weapon, 5f);
        CombatAttackResult resolved = combatResolution.Resolve(new CombatAttackRequest(
            $"{BattleId}:{LastProcessedCommandId + 1}:basic",
            actor.PersistentId,
            target.PersistentId,
            CreateModifiedCombatStats(actor, attacking: true),
            CreateModifiedCombatStats(target, attacking: false),
            weapon,
            distance,
            actor.FireMode,
            CreateEffectiveCover(target),
            defenderDowned: target.HealthRatio <= 0.15f,
            defenderMeleeLocked: distance <= 1,
            attackerSuppression: actor.Suppression,
            defenderSuppression: target.Suppression,
            lightMultiplier: GetVisibilityMultiplier(actor, target, weapon),
            attackPowerMultiplier: GetAttackMultiplier(actor),
            defenderArmor: target.Armor,
            defenderShield: target.Shield,
            defenderConstruct: IsConstruct(target)));
        if (!resolved.Executed)
        {
            result = new OffenseBattleCommandResult(false, resolved.FailureReason);
            return false;
        }

        ConsumePoweredDefense(target, resolved);

        if (weapon.RequiresAmmo && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            combatEquipmentRuntime?.TryConsumeLoadedAmmo(
                weapon.InstanceId,
                Mathf.Max(1, resolved.AmmunitionConsumed));
            if (combatEquipmentRuntime != null
                && combatEquipmentRuntime.TryGetActiveWeapon(
                    actor.PersistentId,
                    out CombatWeaponSnapshot refreshed))
            {
                actor.SetCombatEquipment(refreshed, actor.Armor, actor.Shield);
            }
        }
        else if (weapon.Verb?.DropsWeaponOnUse == true
            && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            thrownOwnerByInstance[weapon.InstanceId] = actor.PersistentId;
            actor.SetCombatEquipment(
                CombatWeaponSnapshot.CreateUnarmed(),
                actor.Armor,
                actor.Shield);
        }
        else if ((weapon.RoleFlags & CombatEquipmentRoleFlags.Powered) != 0)
        {
            RefreshCombatantEquipment(actor);
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

        actor.SetCombatEquipment(refreshed, actor.Armor, actor.Shield);
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

        actor.SetCombatEquipment(weapon, actor.Armor, actor.Shield);
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
        if (actor.Statuses.Any(value =>
                value.Type == OffenseBattleStatusType.ManaBlocked))
        {
            result = new OffenseBattleCommandResult(
                false,
                "마나 차단 상태에서는 전투 능력을 사용할 수 없습니다.");
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
        if (ability.Effects.Any(effect => effect is OffenseDamageEffect
                or OffenseDamageOverTimeEffect
                or OffenseConditionalAmplifyEffect
                or OffenseMultiTargetEffect))
        {
            ConsumePoweredEquipment(actor.Weapon, 5f);
        }
        foreach (OffenseCombatEffectModule effect in ability.Effects)
        {
            OffenseCombatEffectRuntime.Apply(effect, context);
        }
        RefreshCombatantEquipment(actor);

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
            .Where(combatant => combatant.ParticipatesInInitiative
                && !combatant.IsDead
                && !combatant.IsDowned)
            .OrderByDescending(combatant =>
                combatant.Initiative
                    * encounterRules.GetMovementMultiplier(combatant.Team))
            .ThenBy(combatant => combatant.PersistentId, StringComparer.Ordinal)
            .Select(combatant => combatant.PersistentId));
    }

    private OffenseBattleOutcome ResolveOutcome()
    {
        if (Outcome != OffenseBattleOutcome.InProgress)
        {
            return Outcome;
        }

        bool alliesAlive = combatants.Any(combatant =>
            combatant.Team == OffenseBattleTeam.Allies
            && !combatant.IsDead
            && !combatant.IsDowned);
        bool enemiesAlive = combatants.Any(combatant =>
            combatant.Team == OffenseBattleTeam.Enemies
            && !combatant.IsDead
            && !combatant.IsDowned);
        OffenseBattleCombatant objectiveTarget = FindCombatant(
            encounterRules.ObjectiveCombatantId);
        bool deadlineExpired = encounterRules.RoundLimit > 0
            && RoundNumber > encounterRules.RoundLimit;

        switch (encounterRules.Objective)
        {
            case OffenseEncounterObjective.SurviveRounds:
                if (!alliesAlive) return OffenseBattleOutcome.Defeat;
                if (deadlineExpired || !enemiesAlive)
                    return OffenseBattleOutcome.Victory;
                return OffenseBattleOutcome.InProgress;
            case OffenseEncounterObjective.ProtectTarget:
                bool escortAlive = combatants.Any(value =>
                    value.Team == OffenseBattleTeam.Allies
                    && !string.Equals(
                        value.PersistentId,
                        encounterRules.ObjectiveCombatantId,
                        StringComparison.Ordinal)
                    && !value.IsDead
                    && !value.IsDowned);
                if (objectiveTarget == null
                    || objectiveTarget.IsDead
                    || objectiveTarget.IsDowned
                    || !escortAlive)
                    return OffenseBattleOutcome.Defeat;
                if (deadlineExpired || !enemiesAlive)
                    return OffenseBattleOutcome.Victory;
                return OffenseBattleOutcome.InProgress;
            case OffenseEncounterObjective.SabotageTarget:
                if (!alliesAlive) return OffenseBattleOutcome.Defeat;
                if (objectiveTarget == null)
                    return OffenseBattleOutcome.Defeat;
                if (objectiveTarget.IsDead || objectiveTarget.IsDowned)
                    return OffenseBattleOutcome.Victory;
                return deadlineExpired
                    ? OffenseBattleOutcome.Defeat
                    : OffenseBattleOutcome.InProgress;
            case OffenseEncounterObjective.Escape:
                if (!alliesAlive) return OffenseBattleOutcome.Defeat;
                return !enemiesAlive
                    ? OffenseBattleOutcome.Victory
                    : OffenseBattleOutcome.InProgress;
            case OffenseEncounterObjective.CaptureLeader:
                if (!alliesAlive || objectiveTarget == null || objectiveTarget.IsDead)
                    return OffenseBattleOutcome.Defeat;
                if (objectiveTarget.IsDowned)
                    return OffenseBattleOutcome.Victory;
                return deadlineExpired
                    ? OffenseBattleOutcome.Defeat
                    : OffenseBattleOutcome.InProgress;
            default:
                if (!alliesAlive) return OffenseBattleOutcome.Defeat;
                if (!enemiesAlive) return OffenseBattleOutcome.Victory;
                return OffenseBattleOutcome.InProgress;
        }
    }

    private bool TryDeployCover(
        OffenseBattleCombatant actor,
        out OffenseBattleCommandResult result)
    {
        if (actor == null
            || !actor.Shield.IsValid
            || (actor.Shield.RoleFlags
                & CombatEquipmentRoleFlags.DeployableCover) == 0)
        {
            result = new OffenseBattleCommandResult(
                false,
                "설치 가능한 엄폐 방패가 없습니다.");
            return false;
        }
        if (actor.CoverBlockChance > 0f)
        {
            result = new OffenseBattleCommandResult(
                false,
                "이미 엄폐가 설치되어 있습니다.");
            return false;
        }

        float cover = Mathf.Clamp(
            Mathf.Max(0.55f, actor.Shield.GetBlockChance()),
            0f,
            0.8f);
        actor.SetCover(cover);
        AddLog($"{actor.DisplayName}이(가) 파비스를 설치해 엄폐 {cover * 100f:0}%를 확보했습니다.");
        result = new OffenseBattleCommandResult(
            true,
            $"파비스 설치 · 엄폐 {cover * 100f:0}%");
        return true;
    }

    private CombatStatSnapshot CreateModifiedCombatStats(
        OffenseBattleCombatant combatant,
        bool attacking)
    {
        CombatStatSnapshot value = OffenseBattleSessionRules.CreateCombatStats(combatant);
        float accuracy = attacking
            ? encounterRules.GetAccuracyMultiplier(combatant.Team)
            : 1f;
        float sedation = combatant.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Sedated)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float activity = 1f - Mathf.Clamp(sedation, 0f, 0.8f);
        return new CombatStatSnapshot(
            value.Melee * accuracy * activity,
            value.Shooting * accuracy * activity,
            value.Evasion * encounterRules.GetMovementMultiplier(combatant.Team),
            value.MoveSpeed
                * encounterRules.GetMovementMultiplier(combatant.Team)
                * activity,
            value.Strength,
            value.Toughness,
            value.Dexterity * accuracy * activity,
            value.HealthMultiplier);
    }

    private string CreateObjectiveLog()
    {
        string objective = encounterRules.Objective switch
        {
            OffenseEncounterObjective.SurviveRounds =>
                $"목표: {encounterRules.RoundLimit}라운드 동안 생존",
            OffenseEncounterObjective.ProtectTarget =>
                $"목표: 보호 대상을 {encounterRules.RoundLimit}라운드까지 방어",
            OffenseEncounterObjective.SabotageTarget =>
                $"목표: {encounterRules.RoundLimit}라운드 안에 핵심 장치 파괴",
            OffenseEncounterObjective.Escape =>
                $"목표: {encounterRules.RoundLimit}라운드부터 후퇴 명령으로 탈출",
            OffenseEncounterObjective.CaptureLeader =>
                $"목표: {encounterRules.RoundLimit}라운드 안에 지휘관을 죽이지 않고 제압",
            _ => "목표: 적 전멸"
        };
        string modifiers = encounterRules.Modifiers.Count == 0
            ? string.Empty
            : " | 전장: " + string.Join(", ",
                encounterRules.Modifiers.Select(value => value.displayName));
        string counters = encounterRules.MatchedCounterTags.Count == 0
            ? string.Empty
            : " | 대응 성공: " + string.Join(", ",
                encounterRules.MatchedCounterTags.OrderBy(value => value, StringComparer.Ordinal));
        return objective + modifiers + counters;
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
            * encounterRules.GetDamageMultiplier(source?.Team ?? target.Team)
            * (1f - Mathf.Clamp01(guard))
            * (1f + vulnerability));
        adjustedDamage = Mathf.Max(
            0f,
            adjustedDamage - AbsorbWithSummonedGuard(target, adjustedDamage));
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
            resolved.ArmorDurabilityHits,
            resolved.SmokeExposure,
            resolved.AmmunitionItemId,
            resolved.SpecialEffects,
            resolved.StatusPotency,
            resolved.StatusTurns,
            resolved.Nonlethal,
            resolved.PelletHits,
            resolved.TargetAirborneExposure,
            resolved.AmmunitionConsumed,
            resolved.ForcedMovement);
        float applied = target.ApplyCombatInjury(adjusted);
        ApplyAmmunitionStatuses(source, target, resolved);
        if (resolved.ForcedMovement != 0 && !target.IsDead)
        {
            Reposition(target, resolved.ForcedMovement);
        }
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

    private void ConsumePoweredEquipment(
        CombatWeaponSnapshot weapon,
        float amount)
    {
        if (weapon != null
            && (weapon.RoleFlags & CombatEquipmentRoleFlags.Powered) != 0
            && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            combatEquipmentRuntime?.TryConsumePower(weapon.InstanceId, amount);
        }
    }

    private void ConsumePoweredDefense(
        OffenseBattleCombatant target,
        CombatAttackResult result)
    {
        if (target == null)
        {
            return;
        }

        if (target.Shield.IsValid
            && (target.Shield.RoleFlags & CombatEquipmentRoleFlags.Powered) != 0)
        {
            combatEquipmentRuntime?.TryConsumePower(target.Shield.InstanceId, 3f);
        }
        if (result.Hit)
        {
            foreach (string instanceId in target.Armor
                .Where(value =>
                    (value.RoleFlags & CombatEquipmentRoleFlags.Powered) != 0)
                .Select(value => value.InstanceId)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal))
            {
                combatEquipmentRuntime?.TryConsumePower(instanceId, 2f);
            }
        }

        RefreshCombatantEquipment(target);
    }

    private void RefreshCombatantEquipment(OffenseBattleCombatant combatant)
    {
        if (combatant != null
            && combatEquipmentRuntime != null
            && combatEquipmentRuntime.TryGetActiveWeapon(
                combatant.PersistentId,
                out CombatWeaponSnapshot refreshedWeapon))
        {
            combatant.SetCombatEquipment(
                refreshedWeapon,
                combatEquipmentRuntime.GetArmor(combatant.PersistentId),
                combatEquipmentRuntime.GetShield(combatant.PersistentId));
        }
    }

    private float AbsorbWithSummonedGuard(
        OffenseBattleCombatant target,
        float incomingDamage)
    {
        OffenseBattleStatus summonedGuard = target?.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.SummonedGuard
                && status.Value > 0f)
            .OrderBy(status => status.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (summonedGuard == null || incomingDamage <= 0f)
        {
            return 0f;
        }

        float absorbed = summonedGuard.Absorb(incomingDamage);
        if (summonedGuard.Value <= 0f)
        {
            target.RemoveStatus(summonedGuard);
        }
        if (absorbed > 0f)
        {
            AddLog($"{target.DisplayName}의 소환 지원체가 피해 {absorbed:0.#}을 가로막았습니다.");
        }
        return absorbed;
    }

    private static CombatCoverSnapshot CreateEffectiveCover(
        OffenseBattleCombatant target)
    {
        if (target == null)
        {
            return default;
        }

        float smoke = GetSmokeObscuration(target);
        float blockChance = Mathf.Max(
            target.CoverBlockChance,
            smoke * 0.4f);
        return blockChance <= 0f
            ? default
            : new CombatCoverSnapshot(
                CombatCoverHeight.Low,
                blockChance,
                0f,
                smoke > 0f ? "offense-smoke" : "offense-cover");
    }

    private static float GetVisibilityMultiplier(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target,
        CombatWeaponSnapshot weapon)
    {
        if (weapon?.IsRanged != true)
        {
            return 1f;
        }

        float obscuration = Mathf.Max(
            GetSmokeObscuration(source),
            GetSmokeObscuration(target));
        return Mathf.Clamp(1f - obscuration * 0.65f, 0.35f, 1f);
    }

    private static float GetSmokeObscuration(OffenseBattleCombatant combatant) =>
        combatant?.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.SmokeObscured)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max() ?? 0f;

    private void ApplyAmmunitionStatuses(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target,
        CombatAttackResult result)
    {
        if ((result.SpecialEffects & CombatSpecialEffectFlags.Burning) != 0)
        {
            AddStatus(
                target,
                OffenseBattleStatusType.DamageOverTime,
                result.StatusPotency,
                result.StatusTurns,
                source?.PersistentId ?? string.Empty,
                $"ammo:burning:{result.AmmunitionItemId}");
        }
        if ((result.SpecialEffects & CombatSpecialEffectFlags.Tranquilized) != 0)
        {
            AddStatus(
                target,
                OffenseBattleStatusType.Sedated,
                result.StatusPotency,
                result.StatusTurns,
                source?.PersistentId ?? string.Empty,
                "ammo:tranquilized");
        }
        if ((result.SpecialEffects & CombatSpecialEffectFlags.ManaBlocked) != 0)
        {
            AddStatus(
                target,
                OffenseBattleStatusType.ManaBlocked,
                result.StatusPotency,
                result.StatusTurns,
                source?.PersistentId ?? string.Empty,
                "ammo:mana-blocked");
        }
        if ((result.SpecialEffects & CombatSpecialEffectFlags.SignalSupport) != 0
            && source != null)
        {
            foreach (OffenseBattleCombatant ally in GetLivingTeam(source.Team))
            {
                AddStatus(
                    ally,
                    OffenseBattleStatusType.AttackModifier,
                    result.StatusPotency,
                    result.StatusTurns,
                    source.PersistentId,
                    "ammo:signal-support");
            }
        }
    }

    private static float GetAttackMultiplier(OffenseBattleCombatant actor)
    {
        if (actor == null) return 1f;
        float support = actor.Statuses
            .Where(value => value.Type == OffenseBattleStatusType.AttackModifier)
            .Sum(value => value.Value);
        float sedation = actor.Statuses
            .Where(value => value.Type == OffenseBattleStatusType.Sedated)
            .Select(value => value.Value)
            .DefaultIfEmpty(0f)
            .Max();
        return Mathf.Max(0.1f, (1f + support) * (1f - Mathf.Clamp01(sedation)));
    }

    private static bool IsConstruct(OffenseBattleCombatant target)
    {
        string species = target?.SpeciesTag ?? string.Empty;
        return species.IndexOf("golem", StringComparison.OrdinalIgnoreCase) >= 0
            || species.IndexOf("construct", StringComparison.OrdinalIgnoreCase) >= 0
            || species.IndexOf("clockwork", StringComparison.OrdinalIgnoreCase) >= 0;
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
