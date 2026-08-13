using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
[DisallowMultipleComponent]
public class CharacterStats :
    SerializedMonoBehaviour
{
    [SerializeField, ReadOnly] private CharacterActor actor;
    [SerializeField, ReadOnly] private CharacterIdentity identity;
    [SerializeField, ReadOnly] private CharacterVisual visual;
    [SerializeField, ReadOnly] private CharacterLifecycle lifecycle;
    [SerializeField, ReadOnly] private CharacterLog log;
    [SerializeField]
    private Dictionary<CharacterCondition, float> stats;
    [SerializeField, ReadOnly] private float maxHealth = 100f;
    [SerializeField, ReadOnly] private float currentHealth = 100f;
    [SerializeField, ReadOnly, Range(0f, 1f)] private float injurySeverity;
    [SerializeField, Range(0f, 100f)]
    private float baseMood = CharacterMoodRules.DefaultBaseMood;
    [SerializeField, ReadOnly]
    private List<CharacterMoodMemory> interactionMoodFactors = new List<CharacterMoodMemory>();
    private float lastCalculatedMood = float.NaN;
    private ICharacterNeedDefinitionCatalog needDefinitionCatalog;
    private IDungeonDebugRuleQuery debugRules;
    private IGameClock gameClock;
    private IGameEventBus gameEventBus;
    private CharacterStatsProjectionService projectionService;
    private CharacterNeedStateService needStateService;
    private CharacterStatsVitalsService vitalsService;
    private CharacterMoodStateService moodStateService;
    private CharacterStatsMaintenanceSchedule maintenanceSchedule;
    private ICharacterPerformanceQuery performance;
    private CharacterWorkPerformanceContextResolver workPerformanceContext;
    private ICombatEquipmentRuntime combatEquipment;
    [NonSerialized]
    private ControlledDictionary<CharacterCondition, float> controlledStats;
    public IDictionary<CharacterCondition, float> Stats
    {
        get
        {
            EnsureStats();
            return controlledStats ??=
                new ControlledDictionary<CharacterCondition, float>(
                    new DelegatingControlledDictionaryStore<CharacterCondition, float>(
                        () => StatSnapshot, TryGetConditionValue, SetControlledStatValue,
                        RemoveControlledStatValue, ResetControlledStatValues));
        }
        set
        {
            stats = value != null
                ? new Dictionary<CharacterCondition, float>(value)
                : new Dictionary<CharacterCondition, float>();
            EnsureStats();
            AdoptAssignedMoodAsBase();
            RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
            PublishStatsChanged(includeMood: true);
        }
    }
    public bool IsDead => GetVitalsProjection().IsDead;
    public float MaxHealth => GetVitalsProjection().MaximumHealth;
    public float CurrentHealth => GetVitalsProjection().CurrentHealth;
    public float InjurySeverity => GetVitalsProjection().InjurySeverity;
    public float Mood
    {
        get
        {
            EnsureStats();
            return stats.TryGetValue(CharacterCondition.MOOD, out float value)
                ? value
                : baseMood;
        }
    }
    public bool TryGetConditionValue(
        CharacterCondition condition,
        out float value)
    {
        EnsureStats();
        return stats.TryGetValue(condition, out value);
    }
    public float GetConditionValue(
        CharacterCondition condition,
        float fallback = 0f)
    {
        return TryGetConditionValue(condition, out float value)
            ? value
            : fallback;
    }
    public IReadOnlyDictionary<CharacterCondition, float> StatSnapshot => CreateStatSnapshot();
    public ICharacterNeedDefinitionCatalog NeedDefinitionCatalog => needDefinitionCatalog
        ?? throw new InvalidOperationException($"{nameof(CharacterStats)} requires {nameof(ICharacterNeedDefinitionCatalog)} injection.");
    public event Action OnStatsInvalidated;
    public event Action<IReadOnlyDictionary<CharacterCondition, float>> OnStatChange;
    public event Action<CharacterMoodSnapshot> OnMoodChange;
    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
    }
    [Inject]
    public void ConstructCharacterStats(
        IGameClock gameClock,
        ICharacterNeedDefinitionCatalog needDefinitionCatalog,
        IDungeonDebugRuleQuery debugRules,
        CharacterStatsProjectionService projectionService,
        CharacterNeedStateService needStateService,
        CharacterMoodStateService moodStateService,
        CharacterStatsMaintenanceSchedule maintenanceSchedule,
        IGameEventBus gameEventBus,
        ICharacterPerformanceQuery performance = null,
        CharacterWorkPerformanceContextResolver workPerformanceContext = null,
        ICombatEquipmentRuntime combatEquipment = null)
    {
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.needDefinitionCatalog = needDefinitionCatalog
            ?? throw new ArgumentNullException(nameof(needDefinitionCatalog));
        this.debugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        this.projectionService = projectionService
            ?? throw new ArgumentNullException(nameof(projectionService));
        this.needStateService = needStateService
            ?? throw new ArgumentNullException(nameof(needStateService));
        this.moodStateService = moodStateService
            ?? throw new ArgumentNullException(nameof(moodStateService));
        this.maintenanceSchedule = maintenanceSchedule
            ?? throw new ArgumentNullException(nameof(maintenanceSchedule));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.workPerformanceContext = workPerformanceContext
            ?? throw new ArgumentNullException(nameof(workPerformanceContext));
        this.combatEquipment = combatEquipment;
        EnsureStats();
        if (actor != null)
        {
            RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        }
    }
    [Inject]
    public void ConstructCharacterVitals(
        CharacterStatsVitalsService vitalsService)
    {
        this.vitalsService = vitalsService
            ?? throw new ArgumentNullException(nameof(vitalsService));
        if (actor != null && CharacterPersistentIdentity.TryGet(actor, out _))
        {
            vitalsService.Configure(actor, maxHealth, resetCurrentHealth: false);
        }
    }
    public void Bind(CharacterActor owner)
    {
        actor = owner;
        identity = GetComponent<CharacterIdentity>();
        visual = GetComponent<CharacterVisual>();
        lifecycle = GetComponent<CharacterLifecycle>();
        log = GetComponent<CharacterLog>();
        EnsureStats();
        lastCalculatedMood = float.NaN;
        if (gameClock != null)
        {
            RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        }
    }

    public void RunScheduledMaintenance(float now)
    {
        maintenanceSchedule.Run(
            now,
            interactionMoodFactors != null && interactionMoodFactors.Count > 0,
            ApplyNeedDecayTick,
            () => RecalculateMood(
                notify: true,
                forceNotify: false,
                adoptExternalOverride: true));
    }

    public void BeginNeedDecaySchedule()
    {
        float now = gameClock != null ? gameClock.Time : 0f;
        maintenanceSchedule.BeginNeedDecay(
            CharacterPersistentIdentity.Require(actor),
            now);
    }

    public IEnumerator ChangeStatByTick()
    {
        while (true)
        {
            ApplyNeedDecayTick();
            yield return new WaitForSeconds(5f);
        }
    }

    private void ApplyNeedDecayTick()
    {
        EnsureStats();
        SynchronizeExternalMoodOverride();
        CharacterNeedDecayBatch decay = RequireNeedStateService()
            .CalculateTimedDecay(actor, 5f);
        bool changed = false;
        changed |= ApplyStatDeltaWithoutPublishing(
            CharacterCondition.HUNGER,
            -decay.Hunger);
        changed |= ApplyStatDeltaWithoutPublishing(
            CharacterCondition.THIRST,
            -decay.Thirst);
        changed |= ApplyStatDeltaWithoutPublishing(
            CharacterCondition.FUN,
            -decay.Fun);
        changed |= ApplyStatDeltaWithoutPublishing(
            CharacterCondition.EXCRETION,
            -decay.Excretion);
        changed |= ApplyStatDeltaWithoutPublishing(
            CharacterCondition.HYGIENE,
            -decay.Hygiene);
        if (!changed)
        {
            return;
        }

        RecalculateMood(
            notify: false,
            forceNotify: false,
            adoptExternalOverride: false);
        PublishStatsChanged(includeMood: true);
    }

    public void ChangesStat(CharacterCondition condition, float value)
    {
        if (RequireNeedStateService().ShouldFreeze(condition, value))
        {
            return;
        }

        EnsureStats();
        if (condition == CharacterCondition.MOOD)
        {
            ApplyMoodFactor(
                "legacy:mood-adjustment",
                value >= 0f ? "최근 좋은 경험" : "최근 불편한 경험",
                value,
                120f,
                4);
            return;
        }

        SynchronizeExternalMoodOverride();
        ApplyStatDeltaWithoutPublishing(condition, value);
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: true);
    }

    public void RecoverNeed(
        CharacterCondition condition,
        float amount,
        CharacterNeedRecoverySource source,
        IEnumerable<string> activeConditionIds = null)
    {
        float previousValue = GetConditionValue(condition, 0f);
        float sharedMultiplier = condition == CharacterCondition.SLEEP
            && source == CharacterNeedRecoverySource.Rest
                ? GetDetailedStatMultiplier(
                    "character:sleep-recovery",
                    activeConditionIds)
                : 1f;
        ChangesStat(
            condition,
            RequireNeedStateService().ApplyRecoveryMultiplier(
                condition,
                amount,
                source) * sharedMultiplier);
        if (condition == CharacterCondition.SLEEP
            && source == CharacterNeedRecoverySource.Rest
            && gameEventBus != null
            && actor != null
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
        {
            List<string> conditions = new();
            if (activeConditionIds != null)
            {
                foreach (string conditionId in activeConditionIds)
                {
                    if (!string.IsNullOrWhiteSpace(conditionId))
                        conditions.Add(conditionId.Trim());
                }
            }
            gameEventBus.Publish(new RestOutcomeIdentityEvent(
                id,
                previousValue,
                GetConditionValue(condition, previousValue),
                conditions,
                Mathf.Max(
                    0,
                    Mathf.FloorToInt(
                        gameClock.Time / GameCalendarRules.SecondsPerDay))));
        }
    }

    public void ApplyWorkNeedDepletion(float elapsedSeconds = 1f)
    {
        float elapsed = Mathf.Max(0f, elapsedSeconds);
        if (elapsed <= 0f)
        {
            return;
        }

        ApplyWorkDepletion(CharacterCondition.SLEEP, elapsed);
        ApplyWorkDepletion(CharacterCondition.EXCRETION, elapsed);
        ApplyWorkDepletion(CharacterCondition.HYGIENE, elapsed);
    }

    public CharacterNeedResponseProfile GetNeedResponse(
        CharacterCondition condition) =>
        RequireNeedStateService().GetResponse(condition);

    public float GetExpectedTimedNeedLoss(
        CharacterCondition condition,
        float elapsedSeconds)
    {
        CharacterNeedDecayBatch decay = RequireNeedStateService()
            .CalculateTimedDecay(actor, Mathf.Max(0f, elapsedSeconds));
        return condition switch
        {
            CharacterCondition.HUNGER => decay.Hunger,
            CharacterCondition.THIRST => decay.Thirst,
            CharacterCondition.FUN => decay.Fun,
            CharacterCondition.EXCRETION => decay.Excretion,
            CharacterCondition.HYGIENE => decay.Hygiene,
            _ => 0f
        };
    }

    private void ApplyWorkDepletion(
        CharacterCondition condition,
        float elapsedSeconds)
    {
        float loss = RequireNeedStateService().GetWorkDepletion(
            condition,
            elapsedSeconds);
        if (condition == CharacterCondition.SLEEP && actor != null)
        {
            loss *= Mathf.Max(
                0f,
                RequirePerformance().Evaluate(
                    actor,
                    "performance:survival:fatigue-rate").Value);
            loss *= Mathf.Max(
                0f,
                actor.Identity?.Data?.species?.needs?.sleepRateMultiplier ?? 1f);
        }

        if (loss > 0f)
        {
            ChangesStat(condition, -loss);
        }
    }

    private bool ApplyStatDeltaWithoutPublishing(
        CharacterCondition condition,
        float value)
    {
        if (RequireNeedStateService().ShouldFreeze(condition, value))
        {
            return false;
        }

        float previousValue = stats[condition];
        float nextValue = Mathf.Clamp(
            previousValue + value,
            0f,
            GetConditionMaximum(condition));
        if (Mathf.Approximately(previousValue, nextValue))
        {
            return false;
        }

        stats[condition] = nextValue;
        RequestNeedThresholdDecision(condition, previousValue, nextValue);
        if (actor?.Progression != null
            && ((previousValue >= 20f && nextValue < 20f)
                || (previousValue <= 80f && nextValue > 80f)))
        {
            actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Need,
                $"need:{condition.ToString().ToLowerInvariant()}",
                string.Empty,
                nextValue < 20f ? "critical" : "satisfied",
                nextValue);
        }

        return true;
    }

    private void RequestNeedThresholdDecision(
        CharacterCondition condition,
        float previousValue,
        float nextValue)
    {
        if (actor?.Brain == null
            || condition == CharacterCondition.MOOD
            || nextValue >= previousValue)
        {
            return;
        }

        CharacterNeedResponseProfile response =
            RequireNeedStateService().GetResponse(condition);
        bool crossedRoutine = previousValue > response.routineStart
            && nextValue <= response.routineStart;
        bool crossedEmergency = previousValue > response.emergencyStart
            && nextValue <= response.emergencyStart;
        if (!crossedRoutine && !crossedEmergency)
        {
            return;
        }

        string threshold = crossedEmergency ? "emergency" : "routine";
        actor.Brain.RequestImmediateDecision(
            $"Need threshold crossed: {condition} {threshold} "
            + $"({previousValue:0.###}->{nextValue:0.###}).");
    }

    public void ApplyMoodFactor(
        string id,
        string label,
        float value,
        float durationSeconds = 180f,
        int maxStacks = 1)
    {
        if (actor != null)
        {
            actor.ApplyMoodFactor(id, label, value, durationSeconds, maxStacks);
            return;
        }
        ApplyResolvedMoodFactor(id, label, value, durationSeconds, maxStacks);
    }

    internal void ApplyResolvedMoodFactor(
        string id,
        string label,
        float value,
        float durationSeconds = 180f,
        int maxStacks = 1)
    {
        EnsureStats();
        SynchronizeExternalMoodOverride();
        if (!RequireMoodStateService().TryApplyFactor(
                interactionMoodFactors,
                id,
                label,
                value,
                durationSeconds,
                maxStacks,
                out float now))
        {
            return;
        }

        maintenanceSchedule.DeferMoodExpiry(now);
        RecalculateMood(notify: true, forceNotify: true, adoptExternalOverride: false);
        if (actor?.Progression != null
            && !id.StartsWith("skill:", StringComparison.Ordinal))
        {
            actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Mood,
                id,
                string.Empty,
                value >= 0f ? "positive" : "negative",
                value);
        }
    }

    public bool RemoveMoodFactor(string id)
    {
        EnsureStats();
        SynchronizeExternalMoodOverride();
        bool removed = RequireMoodStateService().RemoveFactor(
            interactionMoodFactors,
            id);
        if (removed)
        {
            RecalculateMood(notify: true, forceNotify: true, adoptExternalOverride: false);
        }

        return removed;
    }

    public CharacterMoodSnapshot GetMoodSnapshot()
    {
        EnsureStats();
        SynchronizeExternalMoodOverride();
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        return BuildMoodSnapshot(gameClock.Time);
    }

    public float GetMoveSpeed()
    {
        return RequireProjectionService().GetMoveSpeed(
            CreateProjectionContext());
    }

    public CharacterPerformanceSnapshot EvaluatePerformance(
        string formulaId,
        CharacterPerformanceEvaluationContext context = null)
    {
        if (actor == null)
            throw new InvalidOperationException(
                "Character performance evaluation requires a bound actor.");
        return context == null
            ? RequirePerformance().Evaluate(actor, formulaId)
            : RequirePerformance().Evaluate(actor, formulaId, context);
    }

    public float GetConsumptionMultiplier()
    {
        return RequireProjectionService().GetConsumptionMultiplier(
            CreateProjectionContext());
    }

    public GameplayEffectProjectionResult ProjectDetailedStat(
        string targetId,
        float baseValue,
        IEnumerable<string> activeConditionIds = null)
    {
        return RequireProjectionService().ProjectDetailedStat(
            CreateProjectionContext(),
            targetId,
            baseValue,
            activeConditionIds);
    }

    public float GetDetailedStatMultiplier(
        string targetId,
        IEnumerable<string> activeConditionIds = null)
    {
        return RequireProjectionService().GetDetailedStatMultiplier(
            CreateProjectionContext(),
            targetId,
            activeConditionIds);
    }

    public float GetStayDurationMultiplier()
    {
        return RequireProjectionService().GetStayDurationMultiplier(
            CreateProjectionContext());
    }

    public float GetCrowdSensitivityMultiplier()
    {
        return RequireProjectionService().GetCrowdSensitivityMultiplier(
            CreateProjectionContext());
    }

    public float GetWaitPatienceMultiplier()
    {
        return RequireProjectionService().GetWaitPatienceMultiplier(
            CreateProjectionContext());
    }

    public float GetWorkSpeedMultiplier(WorkTypeId workTypeId)
    {
        return EvaluateWorkPerformance(workTypeId, "speed").Value;
    }

    public float GetWorkSpeedMultiplier(
        WorkTypeId workTypeId,
        BuildableObject target)
    {
        return EvaluateWorkPerformance(
            workTypeId,
            "speed",
            target).Value;
    }

    public float GetWorkContextMultiplier(WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(
                workTypeId,
                out WorkTypeDefinition definition)
            ? RequireProjectionService().GetWorkContextMultiplier(
                CreateProjectionContext(),
                definition)
            : throw new InvalidOperationException(
                $"Work type '{workTypeId.Value}' is not registered.");
    }

    public float GetWorkPreferenceScore(WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? GetEffectiveProfile()?.GetWorkPreferenceScore(
                definition.WorkTypeId) ?? 0.5f
            : 0.5f;
    }

    public float GetFacilityPreferenceScore(FacilityRole roles)
    {
        return GetEffectiveProfile()?.GetFacilityPreferenceScore(roles) ?? 0.5f;
    }

    public float GetAccidentChanceMultiplier()
    {
        WorkTypeId workTypeId = actor != null
            && actor.TryGetAbility(out AbilityWork work)
                ? work.AssignedWorkTypeId
                : default;
        if (!workTypeId.IsValid)
            throw new InvalidOperationException(
                "Work accident projection requires an assigned work type.");
        return EvaluateWorkPerformance(workTypeId, "accident").Value;
    }

    public CharacterSpeciesIncidentType GetIncidentType()
    {
        return GetEffectiveProfile()?.GetIncidentType()
            ?? CharacterSpeciesIncidentType.None;
    }

    public float GetCrimeRiskMultiplier()
    {
        return GetEffectiveProfile()?.GetCrimeRiskMultiplier() ?? 1f;
    }

    public float GetCombatPowerMultiplier()
    {
        string formulaId = "performance:combat:melee-power";
        string characterId = identity?.PersistentId ?? string.Empty;
        if (combatEquipment != null
            && combatEquipment.TryGetActiveWeapon(
                characterId,
                out CombatWeaponSnapshot weapon)
            && weapon?.IsRanged == true)
        {
            formulaId = "performance:combat:ranged-hit";
        }
        CharacterPerformanceSnapshot snapshot = RequirePerformance().Evaluate(
            actor,
            formulaId,
            new CharacterPerformanceEvaluationContext
            {
                ContextFactor = RequireProjectionService()
                    .GetCombatContextMultiplier(CreateProjectionContext())
            });
        if (!snapshot.IsApplicable)
            throw new InvalidOperationException(
                snapshot.Failure?.Message ?? "Combat performance is unavailable.");
        return snapshot.Value;
    }

    private CharacterPerformanceSnapshot EvaluateWorkPerformance(
        WorkTypeId workTypeId,
        string channel,
        BuildableObject targetOverride = null)
    {
        if (!workTypeId.IsValid || actor == null)
            throw new InvalidOperationException(
                "Work performance requires a live actor and valid work type.");
        if (workPerformanceContext == null)
            throw new InvalidOperationException(
                "Work performance context resolver was not injected.");
        BuildableObject target = targetOverride != null
            ? targetOverride
            : actor.TryGetAbility(out AbilityWork work)
                ? work.assignedShop
                : null;
        if (!workPerformanceContext.TryResolve(
                actor,
                target,
                workTypeId,
                out ProficiencyWorkProfile profile,
                out string failureReason))
            throw new InvalidOperationException(failureReason);
        CharacterPerformanceResultChannel resultChannel = channel switch
        {
            "speed" => CharacterPerformanceResultChannel.Speed,
            "accident" => CharacterPerformanceResultChannel.AccidentRisk,
            _ => throw new ArgumentOutOfRangeException(
                nameof(channel),
                channel,
                null)
        };
        CharacterPerformanceSnapshot snapshot = RequirePerformance().EvaluateWork(
            actor,
            workTypeId,
            resultChannel,
            workPerformanceContext.BuildEvaluationContext(
                profile,
                new GameplayEffectContext(new[] { workTypeId.Value }),
                channel == "speed"
                    ? GetWorkContextMultiplier(workTypeId)
                    : 1f));
        if (!snapshot.IsApplicable)
            throw new InvalidOperationException(
                snapshot.Failure?.Message
                ?? $"Work performance '{workTypeId.Value}:{channel}' is unavailable.");
        return snapshot;
    }

    public float GetSpendingMultiplier()
    {
        return RequireProjectionService().GetSpendingMultiplier(
            CreateProjectionContext());
    }

    public float GetFatigueEfficiencyMultiplier()
    {
        return CharacterStatsProjectionService.GetFatigueEfficiencyMultiplier(
            GetConditionValue(CharacterCondition.SLEEP, 100f));
    }

    public float GetInjuryEfficiencyMultiplier()
    {
        return CharacterStatsProjectionService.GetInjuryEfficiencyMultiplier(
            InjurySeverity);
    }

    private CharacterRuntimeProfile GetEffectiveProfile()
    {
        return CreateProjectionContext().EffectiveProfile;
    }

    private CharacterStatsProjectionContext CreateProjectionContext()
    {
        EnsureStats();
        return new CharacterStatsProjectionContext(
            actor,
            identity,
            GetConditionValue(CharacterCondition.SLEEP, 100f),
            InjurySeverity);
    }

    private CharacterStatsProjectionService RequireProjectionService()
    {
        return projectionService
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterStats)} requires "
                + $"{nameof(CharacterStatsProjectionService)} injection.");
    }

    private CharacterNeedStateService RequireNeedStateService()
    {
        return needStateService
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterStats)} requires "
                + $"{nameof(CharacterNeedStateService)} injection.");
    }

    public void ApplyDamage(float amount, string reason = "") =>
        ApplyDamageInternal(amount, reason, allowAggregateDeath: true);

    public void ApplyNonLethalDamage(float amount, string reason = "") =>
        ApplyDamageInternal(amount, reason, allowAggregateDeath: false);

    private void ApplyDamageInternal(
        float amount,
        string reason,
        bool allowAggregateDeath)
    {
        if (amount <= 0f || IsDead || debugRules.ShouldBlockFriendlyDamage(actor)) return;

        RequireVitalsService().ApplyDamage(
            actor,
            amount,
            reason,
            allowAggregateDeath);
    }

    internal void NotifyAggregateDamage(
        float amount,
        string reason,
        bool died,
        CharacterDeathCauseCode deathCause)
    {
        RequireVitalsService().NotifyDamage(
            this,
            log,
            amount,
            reason,
            died,
            deathCause);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead) return;

        float projectedAmount = actor != null
            ? amount * RequirePerformance().Evaluate(
                actor,
                "performance:medical:wound-recovery").Value
            : amount;
        RequireVitalsService().Heal(actor, Mathf.Max(0f, projectedAmount));
    }

    internal void NotifyAggregateHealing(float amount)
    {
        RequireVitalsService().NotifyHealing(this, log, amount);
    }

    private ICharacterPerformanceQuery RequirePerformance() => performance
        ?? throw new InvalidOperationException(
            "CharacterStats requires the authoritative character performance query.");

    public void ScaleMaxHealth(float multiplier) =>
        RequireVitalsService().ScaleMaximumHealth(actor, multiplier);

    public void SetInjurySeverity(float value) =>
        RequireVitalsService().SetInjurySeverity(actor, value);

    internal void NotifyAggregateInjurySeverity(float value)
    {
        injurySeverity = RequireVitalsService().NotifyInjurySeverity(
            log,
            value);
    }

    public void Die(string reason = "") =>
        RequireVitalsService().Kill(actor, reason);

    public void Die(CharacterDeathCauseCode cause, string reasonCode) =>
        RequireVitalsService().Kill(actor, cause, reasonCode);

    internal void NotifyAggregateDeath(
        CharacterDeathCauseCode cause,
        string reasonCode)
    {
        RequireVitalsService().NotifyDeath(
            this,
            actor,
            identity,
            visual,
            lifecycle,
            log,
            cause,
            reasonCode);
    }

    public void RecalculateVitals(bool resetCurrentHealth)
    {
        float calculatedMaximum = RequireProjectionService()
            .CalculateMaximumHealth(CreateProjectionContext());

        RequireVitalsService().Configure(
            actor,
            calculatedMaximum,
            resetCurrentHealth);
    }

    public void RestorePersistentState(
        IReadOnlyDictionary<CharacterCondition, float> savedStats,
        float savedCurrentHealth,
        float savedInjurySeverity,
        float savedBaseMood,
        IReadOnlyList<CharacterMoodFactorSnapshot> savedInteractionMoodFactors)
    {
        stats = savedStats != null
            ? new Dictionary<CharacterCondition, float>(savedStats)
            : new Dictionary<CharacterCondition, float>();
        baseMood = Mathf.Clamp(savedBaseMood, 0f, 100f);
        interactionMoodFactors = RequireMoodStateService().RestoreFactors(
            savedInteractionMoodFactors);
        EnsureStats();
        stats[CharacterCondition.MOOD] = baseMood;

        float now = gameClock.Time;
        float restoredMaximum = RequireProjectionService()
            .CalculateMaximumHealth(CreateProjectionContext());
        RequireVitalsService().RestoreProjection(
            actor,
            restoredMaximum,
            savedCurrentHealth,
            savedInjurySeverity);
        maintenanceSchedule.DeferMoodExpiry(now);
        lastCalculatedMood = float.NaN;
        RecalculateMood(notify: true, forceNotify: true, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: false);
    }

    internal void ApplyVitalsProjection(CharacterVitalsSnapshot snapshot)
    {
        maxHealth = snapshot.MaximumHealth;
        currentHealth = snapshot.CurrentHealth;
        injurySeverity = snapshot.InjurySeverity;
    }

    private CharacterVitalsSnapshot GetVitalsProjection()
    {
        CharacterVitalsSnapshot local = new CharacterVitalsSnapshot(
            maxHealth,
            currentHealth,
            injurySeverity);
        if (vitalsService == null)
        {
            return local;
        }

        return vitalsService.GetProjection(this, actor, local);
    }

    private CharacterStatsVitalsService RequireVitalsService()
    {
        return vitalsService
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterStats)} requires "
                + $"{nameof(CharacterStatsVitalsService)} injection "
                + "before changing health.");
    }

    private void EnsureStats()
    {
        stats ??= new Dictionary<CharacterCondition, float>();
        interactionMoodFactors ??= new List<CharacterMoodMemory>();
        if (needDefinitionCatalog == null) return;

        foreach (CharacterNeedDefinition definition in needDefinitionCatalog.All)
        {
            EnsureStat(definition.Condition, definition.DefaultValue);
        }

        EnsureStat(CharacterCondition.MOOD, baseMood);
    }

    private void AdoptAssignedMoodAsBase()
    {
        RequireMoodStateService().AdoptAssignedMoodAsBase(
            stats,
            interactionMoodFactors,
            ref baseMood,
            ref lastCalculatedMood);
    }

    private void SynchronizeExternalMoodOverride()
    {
        RequireMoodStateService().SynchronizeExternalOverride(
            stats,
            interactionMoodFactors,
            ref baseMood,
            ref lastCalculatedMood);
    }

    private void RecalculateMood(
        bool notify,
        bool forceNotify,
        bool adoptExternalOverride)
    {
        EnsureStats();
        CharacterMoodRecalculation result = RequireMoodStateService().Recalculate(
            stats,
            interactionMoodFactors,
            ref baseMood,
            ref lastCalculatedMood,
            adoptExternalOverride);
        if (notify
            && (forceNotify
                || result.Expired
                || !Mathf.Approximately(result.Previous, result.Current)))
        {
            OnStatsInvalidated?.Invoke();
            if (OnStatChange != null)
            {
                OnStatChange(CreateStatSnapshot());
            }

            if (OnMoodChange != null)
            {
                OnMoodChange(result.Snapshot);
            }
        }
    }

    internal void SetControlledStatValue(CharacterCondition condition, float value)
    {
        EnsureStats();
        stats[condition] = Mathf.Clamp(value, 0f, GetConditionMaximum(condition));
        if (condition == CharacterCondition.MOOD)
        {
            AdoptAssignedMoodAsBase();
        }

        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: true);
    }

    internal bool RemoveControlledStatValue(CharacterCondition condition)
    {
        EnsureStats();
        bool removed = stats.Remove(condition);
        if (!removed)
        {
            return false;
        }

        EnsureStats();
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: true);
        return true;
    }

    internal void ResetControlledStatValues()
    {
        stats.Clear();
        EnsureStats();
        AdoptAssignedMoodAsBase();
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: true);
    }

    private void PublishStatsChanged(bool includeMood)
    {
        OnStatsInvalidated?.Invoke();
        if (OnStatChange != null)
        {
            OnStatChange(CreateStatSnapshot());
        }

        if (includeMood && OnMoodChange != null)
        {
            OnMoodChange(BuildMoodSnapshot(gameClock.Time));
        }
    }

    private IReadOnlyDictionary<CharacterCondition, float> CreateStatSnapshot()
    {
        EnsureStats();
        return new ReadOnlyDictionary<CharacterCondition, float>(
            new Dictionary<CharacterCondition, float>(stats));
    }


    private CharacterMoodSnapshot BuildMoodSnapshot(float now) =>
        RequireMoodStateService().BuildSnapshot(
            stats,
            interactionMoodFactors,
            baseMood,
            now);

    private CharacterMoodStateService RequireMoodStateService()
    {
        return moodStateService
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterStats)} requires "
                + $"{nameof(CharacterMoodStateService)} injection.");
    }

    private void EnsureStat(CharacterCondition condition, float defaultValue)
    {
        if (!stats.ContainsKey(condition))
        {
            stats[condition] = defaultValue;
        }
    }

    private static float GetConditionMaximum(CharacterCondition condition) =>
        condition == CharacterCondition.HUNGER ? 115f : 100f;

}
