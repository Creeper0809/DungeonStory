using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

internal sealed class CharacterMoodDeliveryTransactionSnapshot
{
    public CharacterMoodDeliveryTransactionSnapshot(
        IReadOnlyDictionary<CharacterCondition, float> stats,
        float currentHealth,
        float injurySeverity,
        float baseMood,
        float lastCalculatedMood,
        CharacterStatsMaintenanceScheduleSnapshot maintenance,
        IReadOnlyList<CharacterMoodMemory> factors,
        long restEffectRevision)
    {
        Stats = new Dictionary<CharacterCondition, float>(
            stats ?? throw new ArgumentNullException(nameof(stats)));
        CurrentHealth = currentHealth;
        InjurySeverity = injurySeverity;
        BaseMood = baseMood;
        LastCalculatedMood = lastCalculatedMood;
        Maintenance = maintenance;
        RestEffectRevision = restEffectRevision;
        Factors = (factors ?? Array.Empty<CharacterMoodMemory>())
            .Select(value => value?.DeepClone())
            .Where(value => value != null)
            .ToArray();
    }

    public IReadOnlyDictionary<CharacterCondition, float> Stats { get; }
    public float CurrentHealth { get; }
    public float InjurySeverity { get; }
    public float BaseMood { get; }
    public float LastCalculatedMood { get; }
    public CharacterStatsMaintenanceScheduleSnapshot Maintenance { get; }
    public long RestEffectRevision { get; }
    public IReadOnlyList<CharacterMoodMemory> Factors { get; }
}

[DisallowMultipleComponent]
public class CharacterStats :
    SerializedMonoBehaviour
{
    [SerializeField, ReadOnly] private CharacterActor actor;
    [SerializeField, ReadOnly] private CharacterIdentity identity;
    [SerializeField, ReadOnly] private CharacterVisual visual;
    [SerializeField, ReadOnly] private CharacterLifecycle lifecycle;
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
    private IGameCalendar calendar;
    private IGameEventBus gameEventBus;
    private CharacterStatsProjectionService projectionService;
    private CharacterNeedStateService needStateService;
    private CharacterStatsVitalsService vitalsService;
    private CharacterMoodStateService moodStateService;
    private CharacterStatsMaintenanceSchedule maintenanceSchedule;
    private ICharacterPerformanceQuery performance;
    private CharacterWorkPerformanceContextResolver workPerformanceContext;
    private ICombatEquipmentRuntime combatEquipment;
    private IDomainFactPublisher facts;
    private IDomainFactRecoveryPublisher recovery;
    private CharacterRestObservationSaveData pendingRest;
    private bool restBusy;
    private float nextRestRetry;
    private long restEffectRevision;
    public bool HasPendingRestObservation => pendingRest != null;
    public bool IsRestRollbackBlocked => pendingRest?.rollbackBlocked == true;
    public bool HasUnconfirmedRestObservation => pendingRest != null && !pendingRest.observationCommitted;
    internal bool TryCompleteRestObservation()
    {
        ResumeRestObservation();
        return !HasUnconfirmedRestObservation;
    }
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
    public string LastRestOutcomeResultKey { get; private set; } = string.Empty;
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
        IGameCalendar calendar,
        ICharacterNeedDefinitionCatalog needDefinitionCatalog,
        IDungeonDebugRuleQuery debugRules,
        CharacterStatsProjectionService projectionService,
        CharacterNeedStateService needStateService,
        CharacterMoodStateService moodStateService,
        CharacterStatsMaintenanceSchedule maintenanceSchedule,
        IGameEventBus gameEventBus,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery,
        ICharacterPerformanceQuery performance = null,
        CharacterWorkPerformanceContextResolver workPerformanceContext = null,
        ICombatEquipmentRuntime combatEquipment = null)
    {
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
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
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
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
        if (actor != null && actor.Identity?.Profile != null && CharacterPersistentIdentity.TryGet(actor, out _))
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
        EnsureStats();
        lastCalculatedMood = float.NaN;
        if (gameClock != null)
        {
            RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        }
    }

    public void RunScheduledMaintenance(float now)
    {
        if (pendingRest != null && now >= nextRestRetry)
        {
            nextRestRetry = now + 5f;
            ResumeRestObservation();
        }
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
        CharacterCondition condition, float amount, CharacterNeedRecoverySource source,
        IEnumerable<string> activeConditionIds = null, CharacterEventContext eventContext = null)
    {
        float sharedMultiplier = condition == CharacterCondition.SLEEP && source == CharacterNeedRecoverySource.Rest
            ? GetDetailedStatMultiplier("character:sleep-recovery", activeConditionIds) : 1f;
        float appliedAmount = RequireNeedStateService().ApplyRecoveryMultiplier(condition, amount, source) * sharedMultiplier;
        if (condition != CharacterCondition.SLEEP || source != CharacterNeedRecoverySource.Rest)
        { ChangesStat(condition, appliedAmount); return; }
        if (RequireNeedStateService().ShouldFreeze(condition, appliedAmount)) return;
        if (restBusy) throw new InvalidOperationException("rest-recovery-reentrant");
        // Finishing an earlier applied rest must never apply that recovery twice.
        if (pendingRest != null)
        {
            if (pendingRest.rollbackBlocked) Debug.LogError("rest-recovery-blocked:" + pendingRest.rollbackFailure);
            else ResumeRestObservation();
            return;
        }
        if (actor == null || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
        { Debug.LogError("rest-outcome-character-identity-unavailable"); return; }

        EnsureStats();
        SynchronizeExternalMoodOverride();
        float before = GetConditionValue(condition, 0f);
        if (restEffectRevision == long.MaxValue)
            throw new InvalidOperationException("rest-effect-revision-exhausted");
        float projectedAfter = Mathf.Clamp(before + appliedAmount, 0f, GetConditionMaximum(condition));
        if (Mathf.Approximately(before, projectedAfter)) projectedAfter = before;
        var data = new CharacterRestAppliedFactData
        {
            characterId = id.Value, displayName = identity?.DisplayName ?? actor.BuildingDisplayName,
            before = before, after = projectedAfter,
            absoluteDay = calendar.Day, activeConditions = SnapshotConditionIds(activeConditionIds),
            context = eventContext?.Clone()
        };
        var fact = new CharacterRestAppliedFact(data);
        if (!facts.TryReserve(fact, out DomainFactLease lease, out string failure))
        { Debug.LogError("rest-observation-reservation-failed:" + failure); return; }

        CharacterMoodDeliveryTransactionSnapshot rollback;
        CharacterRestObservationSaveData row;
        try
        {
            rollback = CaptureMoodDeliveryTransactionState();
            row = new CharacterRestObservationSaveData { fact = fact.Snapshot(), publicationKey = lease.PublicationKey };
            row.Validate(id.Value);
        }
        catch { facts.Cancel(lease); throw; }
        restBusy = true;
        try
        {
            try
            {
                ApplyStatDeltaWithoutPublishing(condition, appliedAmount, publishConsequences: false);
                RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
                if (GetConditionValue(condition, before) != data.after)
                    throw new InvalidOperationException("rest-applied-value-conflict");
            }
            catch
            {
                pendingRest = row;
                RollbackRejectedRest(row, rollback, "rest-effect-application-failed");
                throw;
            }
            // Save the applied fact before crossing the uncertain publication boundary.
            pendingRest = row;
            restEffectRevision = checked(restEffectRevision + 1);
            DomainFactDelivery result;
            try { result = facts.Publish(lease, fact); }
            catch (Exception exception) when (RestRecoverable(exception))
            {
                result = DomainFactDelivery.Uncertain("rest-publication-response-unknown:" + exception.GetType().Name);
                Debug.LogError(result.Detail);
            }
            if (!result.MayBeCommitted)
            {
                RollbackRejectedRest(row, rollback, result.Detail);
                Debug.LogError("rest-observation-rejected:" + result.Detail);
                return;
            }
            AcceptRestDelivery(row, result);
            if (!row.observationCommitted)
                Debug.LogWarning("rest-observation-unconfirmed:" + result.Detail);
            FinishRestObservation(row);
        }
        finally
        {
            restBusy = false;
            if (lease.IsValid)
            {
                try { facts.Cancel(lease); }
                catch (Exception exception) when (RestRecoverable(exception))
                { Debug.LogError("rest-reservation-release-failed:" + exception.Message); }
            }
        }
    }

    public CharacterRestObservationSaveData CaptureRestObservation()
    {
        if (restBusy) throw new InvalidOperationException("rest-save-during-effect-or-notification");
        pendingRest?.Validate(CharacterPersistentIdentity.Require(actor).Value);
        return pendingRest?.Clone();
    }

    private void RollbackRejectedRest(CharacterRestObservationSaveData row,
        CharacterMoodDeliveryTransactionSnapshot snapshot, string failure)
    {
        row.rollbackBlocked = true;
        row.rollbackFailure = string.IsNullOrWhiteSpace(failure) ? "rest-known-rejection" : failure;
        RestoreMoodDeliveryTransactionState(snapshot, publish: false, allowRestRollback: true);
        if (ReferenceEquals(pendingRest, row)) pendingRest = null;
    }

    private void ResumeRestObservation()
    {
        if (restBusy || pendingRest == null || pendingRest.rollbackBlocked) return;
        restBusy = true;
        var row = pendingRest;
        try
        {
            if (!row.deliveryCompleted)
            {
                var results = new DomainFactDelivery[1];
                recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey,
                    new CharacterRestAppliedFact(row.fact)) }, results, out string failure);
                AcceptRestDelivery(row, results[0]);
                if (!row.deliveryCompleted)
                    Debug.LogWarning("rest-observation-pending:" + failure + ":" + results[0].Detail);
            }
            FinishRestObservation(row);
        }
        catch (Exception exception) when (RestRecoverable(exception))
        { Debug.LogError("rest-observation-recovery-failed:" + exception.Message); }
        finally { restBusy = false; }
    }

    private void AcceptRestDelivery(CharacterRestObservationSaveData row, DomainFactDelivery result)
    {
        if (!ReferenceEquals(pendingRest, row)) return;
        bool committed = result.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery;
        if (!committed || result.OperationKey != row.publicationKey
            || string.IsNullOrWhiteSpace(result.PublicationId)
            || result.Fingerprint.Length != 64 || !result.Fingerprint.All(Uri.IsHexDigit)) return;
        row.observationCommitted = true;
        row.deliveryCompleted |= result.Completed;
    }

    private void FinishRestObservation(CharacterRestObservationSaveData row)
    {
        if (!ReferenceEquals(pendingRest, row) || !row.observationCommitted || row.rollbackBlocked) return;
        LastRestOutcomeResultKey = row.publicationKey; // Opaque confirmation, never interpreted by the effect owner.
        if (!row.notificationsAttempted)
        {
            row.notificationsAttempted = true;
            void Notify(Action action, string channel)
            {
                try { action(); }
                catch (Exception exception) when (RestRecoverable(exception))
                { Debug.LogError("rest-" + channel + "-observer-failed:" + exception.Message); }
            }
            var data = row.fact;
            Notify(() =>
            {
                RequestNeedThresholdDecision(CharacterCondition.SLEEP, data.before, data.after);
                PublishConditionEvent(CharacterCondition.SLEEP, data.before, data.after,
                    data.context?.occurredDay ?? 0, data.context);
            }, "condition");
            if (!ReferenceEquals(pendingRest, row)) return;
            Notify(() => PublishStatsChanged(includeMood: true), "stats");
            if (!ReferenceEquals(pendingRest, row)) return;
            Notify(() => gameEventBus.Publish(new RestOutcomeIdentityEvent(new CharacterId(data.characterId),
                data.before, data.after, data.activeConditions.ToArray(), data.absoluteDay)), "identity");
        }
        if (ReferenceEquals(pendingRest, row) && row.deliveryCompleted) pendingRest = null;
    }

    private static bool RestRecoverable(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;

    private static List<string> SnapshotConditionIds(
        IEnumerable<string> activeConditionIds)
    {
        var conditions = new List<string>();
        if (activeConditionIds == null)
            return conditions;
        foreach (string conditionId in activeConditionIds)
        {
            if (!string.IsNullOrWhiteSpace(conditionId))
                conditions.Add(conditionId.Trim());
        }
        return conditions;
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
            loss *= RequireProjectionService()
                .GetFatigueAccumulationMultiplier(CreateProjectionContext());
        }

        if (loss > 0f)
        {
            ChangesStat(condition, -loss);
        }
    }

    private bool ApplyStatDeltaWithoutPublishing(
        CharacterCondition condition,
        float value,
        bool publishConsequences = true,
        CharacterEventContext eventContext = null)
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
        if (!publishConsequences)
            return true;
        RequestNeedThresholdDecision(condition, previousValue, nextValue);
        PublishConditionEvent(condition, previousValue, nextValue,
            eventContext?.occurredDay ?? 0, eventContext);

        return true;
    }

    internal float ChangeConditionBeforeOutcomeCommit(CharacterCondition condition, float value)
    {
        if (condition == CharacterCondition.MOOD)
            throw new ArgumentException("Mood must be staged through its mood policy.", nameof(condition));
        EnsureStats();
        SynchronizeExternalMoodOverride();
        float previous = stats[condition];
        ApplyStatDeltaWithoutPublishing(condition, value, publishConsequences: false);
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        return previous;
    }

    internal float RecoverEmergencyNeedBeforeOutcomeCommit(
        CharacterCondition condition, float amount) =>
        ChangeConditionBeforeOutcomeCommit(
            condition,
            RequireNeedStateService().ApplyRecoveryMultiplier(
                condition, amount, CharacterNeedRecoverySource.Emergency));

    internal void PublishCommittedConditionChange(
        CharacterCondition condition, float previous, float next, int absoluteDay)
    {
        List<Exception> failures = null;
        void Publish(Action callback)
        {
            try { callback(); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            { (failures ??= new List<Exception>()).Add(exception); }
        }
        if (!Mathf.Approximately(previous, next))
        {
            Publish(() => RequestNeedThresholdDecision(condition, previous, next));
            Publish(() => PublishConditionEvent(condition, previous, next, absoluteDay));
        }
        Publish(() => OnStatsInvalidated?.Invoke());
        Publish(() => OnStatChange?.Invoke(CreateStatSnapshot()));
        Publish(() => OnMoodChange?.Invoke(BuildMoodSnapshot(gameClock.Time)));
        if (failures != null)
            throw new AggregateException("Committed condition observer publication failed.", failures);
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

    internal bool ApplyResolvedMoodFactor(
        string id,
        string label,
        float value,
        float durationSeconds = 180f,
        int maxStacks = 1,
        bool publish = true,
        int absoluteDay = 0,
        CharacterEventContext eventContext = null)
    {
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
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
            return false;
        }

        maintenanceSchedule.DeferMoodExpiry(now);
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        if (publish)
            PublishCommittedMoodFactor(
                id,
                value,
                absoluteDay,
                eventContext);
        return true;
    }

    // Publish only an applied mood factor. Transactional callers defer this until
    // their outcome is durably committed; ordinary applications publish above.
    // Record the authoritative experience before independent observer callbacks.
    internal void PublishCommittedMoodFactor(string id, float resolvedValue, int absoluteDay,
        CharacterEventContext eventContext = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Committed mood factor requires an ID.", nameof(id));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        List<Exception> failures = null;
        void Publish(Action callback)
        {
            try { callback(); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            { (failures ??= new List<Exception>()).Add(exception); }
        }
        // Each post-commit channel is attempted independently. Keep all failures
        // visible to the domain caller without undoing a durable gameplay result.
        Publish(() => PublishAppliedMoodFact(id, resolvedValue, absoluteDay, eventContext));
        Publish(() => OnStatsInvalidated?.Invoke());
        Publish(() => OnStatChange?.Invoke(CreateStatSnapshot()));
        Publish(() => OnMoodChange?.Invoke(BuildMoodSnapshot(gameClock.Time)));
        if (failures != null)
            throw new AggregateException("Committed mood observer publication failed.", failures);
    }

    private void PublishConditionEvent(CharacterCondition condition, float before, float after,
        int absoluteDay = 0, CharacterEventContext context = null)
    {
        if (actor == null) return;
        gameEventBus.Publish(new CharacterConditionChangedEvent(CharacterPersistentIdentity.Require(actor),
            condition, before, after, absoluteDay, context));
    }

    private void PublishAppliedMoodFact(string id, float value, int absoluteDay = 0,
        CharacterEventContext eventContext = null)
    {
        if (actor == null) return;
        gameEventBus.Publish(new CharacterMoodFactorAppliedEvent(CharacterPersistentIdentity.Require(actor),
            id, value, absoluteDay, eventContext));
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

    internal CharacterMoodDeliveryTransactionSnapshot
        CaptureMoodDeliveryTransactionState()
    {
        EnsureStats();
        return new CharacterMoodDeliveryTransactionSnapshot(
            CreateStatSnapshot(),
            CurrentHealth,
            InjurySeverity,
            baseMood,
            lastCalculatedMood,
            maintenanceSchedule.Capture(),
            interactionMoodFactors,
            restEffectRevision);
    }

    internal void RestoreMoodDeliveryTransactionState(
        CharacterMoodDeliveryTransactionSnapshot snapshot,
        bool publish = true,
        bool allowRestRollback = false)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        if (!allowRestRollback && snapshot.RestEffectRevision != restEffectRevision)
            throw new InvalidOperationException("stat-rollback-would-undo-applied-rest");
        stats = new Dictionary<CharacterCondition, float>(snapshot.Stats);
        baseMood = Mathf.Clamp(snapshot.BaseMood, 0f, 100f);
        interactionMoodFactors = snapshot.Factors
            .Select(value => value?.DeepClone())
            .Where(value => value != null)
            .ToList();
        EnsureStats();
        float restoredMaximum = RequireProjectionService()
            .CalculateMaximumHealth(CreateProjectionContext());
        RequireVitalsService().RestoreProjection(
            actor,
            restoredMaximum,
            snapshot.CurrentHealth,
            snapshot.InjurySeverity);
        lastCalculatedMood = snapshot.LastCalculatedMood;
        maintenanceSchedule.Restore(snapshot.Maintenance);
        restEffectRevision = snapshot.RestEffectRevision;
        if (publish) PublishStatsChanged(includeMood: true);
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
        return EvaluateWorkSpeedPerformance(workTypeId).Value;
    }

    public float GetWorkSpeedMultiplier(
        WorkTypeId workTypeId,
        BuildableObject target)
    {
        return EvaluateWorkSpeedPerformance(
            workTypeId,
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

    internal GameplayEffectContext BuildWorkEffectContext(
        WorkTypeId workTypeId) => RequireProjectionService()
        .BuildWorkEffectContext(CreateProjectionContext(), workTypeId);

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

    private CharacterPerformanceSnapshot EvaluateWorkSpeedPerformance(
        WorkTypeId workTypeId,
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
        CharacterPerformanceSnapshot snapshot = RequirePerformance().EvaluateWork(
            actor,
            workTypeId,
            CharacterPerformanceResultChannel.Speed,
            workPerformanceContext.BuildEvaluationContext(
                profile,
                BuildWorkEffectContext(workTypeId),
                GetWorkContextMultiplier(workTypeId)
                * CharacterWorkPerformanceContextResolver.GetSpecializedWorkSpeed(actor, target, workTypeId)));
        if (!snapshot.IsApplicable)
            throw new InvalidOperationException(
                snapshot.Failure?.Message
                ?? $"Work performance '{workTypeId.Value}:speed' is unavailable.");
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
            actor,
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
        RequireVitalsService().NotifyHealing(this, actor, amount);
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
            actor,
            value);
    }

    public void Die(string reason = "") =>
        RequireVitalsService().Kill(actor, reason);

    public void Die(CharacterDeathCauseCode cause, string reasonCode) =>
        RequireVitalsService().Kill(actor, cause, reasonCode);

    internal void NotifyAggregateDeath(
        CharacterDeathCauseCode cause,
        string reasonCode,
        string parentPublicationKey = "")
    {
        RequireVitalsService().NotifyDeath(
            this,
            actor,
            identity,
            visual,
            lifecycle,
            cause,
            reasonCode,
            parentPublicationKey);
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

    [GameplayInternalOnly(
        "Installed-part lifecycle and bounded health maintenance refresh derived maximum health without healing.",
        "CharacterBodyHealthRuntime|InstallSurgicalPartEffectHandler|SurgicalPartRuntime")]
    internal void RefreshDerivedMaximumHealthPreservingCurrent()
    {
        float calculatedMaximum = RequireProjectionService()
            .CalculateMaximumHealth(CreateProjectionContext());

        RequireVitalsService().RefreshMaximumHealthPreservingCurrent(
            actor,
            calculatedMaximum);
    }

    public void RestorePersistentState(
        IReadOnlyDictionary<CharacterCondition, float> savedStats,
        float savedCurrentHealth,
        float savedInjurySeverity,
        float savedBaseMood,
        IReadOnlyList<CharacterMoodFactorSnapshot> savedInteractionMoodFactors,
        CharacterRestObservationSaveData savedRestObservation = null)
    {
        if (restBusy) throw new InvalidOperationException("rest-restore-during-effect-or-notification");
        savedRestObservation?.Validate(CharacterPersistentIdentity.Require(actor).Value);
        restEffectRevision = checked(restEffectRevision + 1);
        pendingRest = savedRestObservation?.Clone();
        LastRestOutcomeResultKey = string.Empty;
        nextRestRetry = 0f;
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
