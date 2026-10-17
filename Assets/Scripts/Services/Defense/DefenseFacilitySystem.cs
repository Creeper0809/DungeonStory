using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DungeonStory.Foundation.Facts;





public class DefenseActivationReport
{
    private readonly List<string> effectTags = new List<string>();
    private readonly IReadOnlyList<string> effectTagsView;

    public DefenseActivationReport(
        DefenseFacility facility,
        CharacterActor target,
        DefenseTriggerTiming timing,
        int activationCount = 0)
    {
        effectTagsView = effectTags.AsReadOnly();
        Facility = facility;
        TargetActor = target;
        Timing = timing;
        Concept = facility != null && facility.BuildingData != null
            ? facility.BuildingData.Defense.concept
            : DefenseAttackConcept.None;
        ActivationCount = Mathf.Max(0, activationCount);
        TargetPosition = target != null ? target.GetNowXY() : default;
        TargetPersistentId = target != null
            && CharacterPersistentIdentity.TryGet(target, out CharacterId targetId)
                ? targetId.Value
                : string.Empty;
    }

    public DefenseFacility Facility { get; }
    public CharacterActor TargetActor { get; }
    public DefenseTriggerTiming Timing { get; }
    public DefenseAttackConcept Concept { get; }
    public int ActivationCount { get; }
    public Vector2Int TargetPosition { get; }
    public string TargetPersistentId { get; }
    public float TotalDamage { get; private set; }
    public float MovementDelaySeconds { get; private set; }
    public float EnvironmentalIgnitionIntensity { get; private set; }
    public IReadOnlyList<string> EffectTags => effectTagsView;
    public bool Triggered => Facility != null && TargetActor != null;

    public void AddDamage(float amount)
    {
        TotalDamage += Mathf.Max(0f, amount);
    }

    public void AddMovementDelay(float seconds)
    {
        MovementDelaySeconds = Mathf.Max(MovementDelaySeconds, seconds);
    }

    public void AddEffectTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag) && !effectTags.Contains(tag))
        {
            effectTags.Add(tag);
        }
    }

    public void AddEnvironmentalIgnition(float intensity)
    {
        EnvironmentalIgnitionIntensity = Mathf.Max(
            EnvironmentalIgnitionIntensity,
            Mathf.Clamp01(intensity));
    }

    public string FormatSummary()
    {
        string facilityName = Facility != null && Facility.BuildingData != null
            ? Facility.BuildingData.objectName
            : "방어 시설";
        string damageText = TotalDamage > 0f ? $" 피해 {TotalDamage:0.#}" : string.Empty;
        string tags = effectTags.Count > 0 ? $" [{string.Join(", ", effectTags)}]" : string.Empty;
        return $"{facilityName} 발동{damageText}{tags}";
    }

    public DefenseActivationSnapshot CreateSnapshot()
    {
        return new DefenseActivationSnapshot(this);
    }

    internal DefenseActivationEffectState CaptureEffectState() => new(
        TotalDamage,
        MovementDelaySeconds,
        EnvironmentalIgnitionIntensity,
        effectTags.ToArray());

    internal void RestoreEffectState(in DefenseActivationEffectState state)
    {
        TotalDamage = state.TotalDamage;
        MovementDelaySeconds = state.MovementDelaySeconds;
        EnvironmentalIgnitionIntensity = state.EnvironmentalIgnitionIntensity;
        effectTags.Clear();
        effectTags.AddRange(state.EffectTags ?? Array.Empty<string>());
    }
}

internal readonly struct DefenseActivationEffectState
{
    public DefenseActivationEffectState(
        float totalDamage,
        float movementDelaySeconds,
        float environmentalIgnitionIntensity,
        string[] effectTags)
    {
        TotalDamage = totalDamage;
        MovementDelaySeconds = movementDelaySeconds;
        EnvironmentalIgnitionIntensity = environmentalIgnitionIntensity;
        EffectTags = effectTags ?? Array.Empty<string>();
    }

    public float TotalDamage { get; }
    public float MovementDelaySeconds { get; }
    public float EnvironmentalIgnitionIntensity { get; }
    public string[] EffectTags { get; }
}

public sealed class DefenseActivationSnapshot
{
    internal DefenseActivationSnapshot(DefenseActivationReport report)
    {
        SourceFacility = report?.Facility;
        Facility = report?.Facility != null ? report.Facility.BuildingData : null;
        FacilityRuntimeId = report?.Facility != null ? report.Facility.GetInstanceID() : 0;
        FacilityName = report?.Facility != null && report.Facility.BuildingData != null
            ? report.Facility.BuildingData.objectName
            : "방어 시설";
        TargetName = report?.TargetActor != null ? report.TargetActor.name : string.Empty;
        SourceFacilityPersistentId = report?.Facility != null
                && report.Facility.PersistentInstanceId.IsValid
            ? report.Facility.PersistentInstanceId.Value
            : string.Empty;
        ActivationCount = report?.ActivationCount ?? 0;
        TargetPosition = report?.TargetPosition ?? default;
        TargetPersistentId = report?.TargetPersistentId ?? string.Empty;
        Timing = report?.Timing ?? DefenseTriggerTiming.None;
        Concept = report?.Concept ?? DefenseAttackConcept.None;
        TotalDamage = report?.TotalDamage ?? 0f;
        MovementDelaySeconds = report?.MovementDelaySeconds ?? 0f;
        EnvironmentalIgnitionIntensity =
            report?.EnvironmentalIgnitionIntensity ?? 0f;
        EffectTags = EventPayloadSnapshot.Copy(report?.EffectTags);
        Summary = report?.FormatSummary() ?? string.Empty;
    }

    public DefenseFacility SourceFacility { get; }
    public BuildingSO Facility { get; }
    public int FacilityRuntimeId { get; }
    public string SourceFacilityPersistentId { get; }
    public int ActivationCount { get; }
    public string FacilityName { get; }
    public string TargetName { get; }
    public Vector2Int TargetPosition { get; }
    public string TargetPersistentId { get; }
    public DefenseTriggerTiming Timing { get; }
    public DefenseAttackConcept Concept { get; }
    public float TotalDamage { get; }
    public float MovementDelaySeconds { get; }
    public float EnvironmentalIgnitionIntensity { get; }
    public IReadOnlyList<string> EffectTags { get; }
    public string Summary { get; }

    public string FormatSummary()
    {
        return Summary;
    }
}

public readonly struct DefenseFacilityTriggeredEvent
{
    public DefenseActivationSnapshot report { get; }

    public DefenseFacilityTriggeredEvent(DefenseActivationReport report)
    {
        this.report = report?.CreateSnapshot();
    }

    public DefenseFacilityTriggeredEvent(DefenseActivationSnapshot report)
    {
        this.report = report;
    }
}

public class DefenseFacility : Facility
{
    private float nextTriggerTime;
    private DungeonStory.Foundation.IGameEventBus gameEventBus;
    private IWorldThreatModifierQuery worldThreatModifiers;
    private IDefenseFacilityRuntime defenseRuntime;
    private DefenseActivationPublicationRuntime activationPublications;
    private IGameCalendar calendar;
    private ICharacterBodyHealthMutationTransaction bodyHealthMutation;

    [VContainer.Inject]
    public void ConstructDefenseFacilityEventBus(
        DungeonStory.Foundation.IGameEventBus gameEventBus,
        IWorldThreatModifierQuery worldThreatModifiers,
        IDefenseFacilityRuntime defenseRuntime)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.worldThreatModifiers = worldThreatModifiers;
        this.defenseRuntime = defenseRuntime;
    }

    [VContainer.Inject]
    public void ConstructDefenseActivation(
        DefenseActivationPublicationRuntime activationPublications,
        IGameCalendar calendar,
        ICharacterBodyHealthMutationTransaction bodyHealthMutation)
    {
        this.activationPublications = activationPublications
            ?? throw new ArgumentNullException(nameof(activationPublications));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.bodyHealthMutation = bodyHealthMutation
            ?? throw new ArgumentNullException(nameof(bodyHealthMutation));
    }

    public DefenseFacilityData Defense => BuildingData != null ? BuildingData.Defense : null;
    public float CooldownRemaining => Mathf.Max(
        Mathf.Max(0f, nextTriggerTime - GameTime),
        defenseRuntime?.GetSnapshot(this).CooldownRemaining ?? 0f);

    public bool CanTrigger(DefenseTriggerTiming timing, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        DefenseFacilityData defense = Defense;
        if (defense == null || !defense.IsDefenseFacility)
        {
            failure = new DomainFailure(FailureCode.DefenseFacilityUnavailable);
            return false;
        }

        if (isDestroy)
        {
            failure = new DomainFailure(FailureCode.DefenseDestroyed);
            return false;
        }

        if (IsDamaged && (Facility == null || Facility.disabledWhenDamaged))
        {
            failure = new DomainFailure(FailureCode.DefenseConditionCritical);
            return false;
        }

        if (!defense.SupportsTrigger(timing))
        {
            failure = new DomainFailure(
                FailureCode.DefenseTriggerUnsupported,
                timing.ToString());
            return false;
        }

        if (GameTime < nextTriggerTime)
        {
            failure = new DomainFailure(
                FailureCode.DefenseCooldownActive,
                (nextTriggerTime - GameTime).ToString(
                    "0.0",
                    System.Globalization.CultureInfo.InvariantCulture));
            return false;
        }

        if (defenseRuntime != null
            && !defenseRuntime.CanActivate(
                this,
                null,
                timing,
                out failure))
        {
            return false;
        }

        return true;
    }

    public DefenseActivationReport Trigger(
        CharacterActor intruder,
        DefenseTriggerTiming timing,
        IDefenseStatusRuntimeService statusRuntimeService,
        float paidPerformanceMultiplier = 1f)
    {
        if (intruder == null || !CanTrigger(timing, out _))
        {
            return null;
        }

        if (!CharacterPersistentIdentity.TryGet(
                intruder,
                out CharacterId intruderId))
        {
            Debug.LogError(
                "defense-facility-effect-target-identity-unavailable");
            return null;
        }
        string targetId = intruderId.Value;

        if (defenseRuntime == null || activationPublications == null
            || calendar == null || bodyHealthMutation == null)
        {
            Debug.LogError("defense-facility-activation-dependency-unavailable");
            return null;
        }

        BuildingInstanceId facilityId = RequirePersistentInstanceId();
        DefenseFacilityState rollback = defenseRuntime.CaptureActivationState(this);
        float previousNextTriggerTime = nextTriggerTime;
        var activation = new DefenseActivationFactData
        {
            facilityId = facilityId.Value,
            facilityName = string.IsNullOrWhiteSpace(name) ? facilityId.Value : name,
            targetId = targetId,
            targetName = string.IsNullOrWhiteSpace(intruder.name) ? targetId : intruder.name,
            absoluteDay = calendar.Day,
            activationCount = checked(rollback.activationCount + 1),
            timing = timing,
            concept = Defense.concept,
            supplyBefore = rollback.supply,
            supplyAfter = rollback.supply,
            conditionBefore = rollback.condition,
            conditionAfter = rollback.condition,
            afterState = rollback.operationalState,
            blockedReason = rollback.blockedReason ?? string.Empty
        };
        if (!activationPublications.TryBegin(new DefenseActivationFact(activation, isProposal: true),
                out var publication, out string reserveFailure))
        {
            Debug.LogError("defense-facility-activation-reservation-failed:" + reserveFailure);
            return null;
        }

        DefenseActivationReport report;
        using (publication)
        {
            publication.SetActivationRollback(() =>
            {
                defenseRuntime.RestoreActivationState(this, rollback);
                nextTriggerTime = previousNextTriggerTime;
            });
            if (!defenseRuntime.TryBeginActivation(this, intruder, timing,
                    out DefenseActivationAuthorization operational, out _))
                return null;

            report = new DefenseActivationReport(this, intruder, timing, activation.activationCount);
            nextTriggerTime = GameTime + Mathf.Max(0f, Defense.cooldownSeconds);
            if (operational.Jammed) report.AddEffectTag("걸림");
            if (operational.Misfired) report.AddEffectTag("오작동");
            defenseRuntime.CompleteActivation(this, operational);
            DefenseFacilitySnapshot committedState = defenseRuntime.GetSnapshot(this);
            if (committedState.ActivationCount != activation.activationCount)
                throw new InvalidOperationException("defense-activation-owner-count-conflict");
            activation.supplyAfter = committedState.Supply;
            activation.conditionAfter = committedState.Condition;
            activation.afterState = committedState.OperationalState;
            activation.blockedReason = committedState.BlockedReason.ToString();
            activation.jammed = operational.Jammed;
            activation.misfired = operational.Misfired;
            DomainFactDelivery activationDelivery = publication.PublishActivation(new DefenseActivationFact(activation));
            if (!activationDelivery.MayBeCommitted)
            {
                Debug.LogError("defense-facility-activation-rejected:" + activationDelivery.Detail);
                return null;
            }

            try
            {
                DefenseEffectApplicationResult application = DefenseEffectResolver.ApplyEffectsTransactional(
                    this, intruder, report, statusRuntimeService, bodyHealthMutation,
                    (worldThreatModifiers?.GetMultiplier(OffenseThreatModifierKind.AutomatedDefense) ?? 1f)
                        * Mathf.Max(0f, paidPerformanceMultiplier) * operational.EffectMultiplier,
                    operational.Jammed, operational.Misfired);
                publication.SetEffectActions(application.Rollback, application.Complete);
                var effect = new DefenseEffectFact(new DefenseEffectFactData
                {
                    activation = activation,
                    result = application.Status,
                    damage = application.Damage,
                    healthBefore = application.HealthBefore,
                    healthAfter = application.HealthAfter,
                    statusesBefore = SnapshotStatuses(application.StatusesBefore),
                    statusesAfter = SnapshotStatuses(application.StatusesAfter),
                    movementDelaySeconds = report.MovementDelaySeconds,
                    ignitionIntensity = report.EnvironmentalIgnitionIntensity,
                    effectTags = report.EffectTags.ToArray(),
                    failure = application.FailureSummary
                });
                DomainFactDelivery effectDelivery = publication.PublishEffect(effect);
                if (!effectDelivery.MayBeCommitted)
                    Debug.LogError("defense-facility-effect-observation-rejected:" + effectDelivery.Detail);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException && exception is not StackOverflowException
                && exception is not AccessViolationException)
            {
                publication.RecordEffectFailure("defense-effect-transaction:" + exception.GetType().Name + ":" + exception.Message);
                Debug.LogError("defense-facility-effect-transaction-failed:" + exception.GetType().Name + ":" + exception.Message);
            }
        }
        PublishPostCommitObserver(
            () => defenseRuntime.PublishCommittedState(this),
            "defense-facility-state-observer");
        PublishPostCommitObserver(
            () => (gameEventBus
                ?? throw new InvalidOperationException(
                    $"{nameof(DefenseFacility)} requires "
                    + $"{nameof(DungeonStory.Foundation.IGameEventBus)} injection."))
                .Publish(new DefenseFacilityTriggeredEvent(report)),
            "defense-facility-triggered-event-observer");
        return report;
    }

    private static DefenseEffectStatusFactData[] SnapshotStatuses(IReadOnlyList<DefenseStatusSnapshot> statuses) =>
        statuses.Select(value => new DefenseEffectStatusFactData
        { kind = value.Kind, value = value.Value, remainingSeconds = value.RemainingSeconds, stacks = value.Stacks }).ToArray();

    private static void PublishPostCommitObserver(Action observer, string context)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(context + ":" + exception.GetType().Name);
        }
    }

}

public static class DefenseFacilityResolver
{
    public static List<DefenseActivationReport> TriggerAt(
        Grid grid,
        CharacterActor intruder,
        Vector2Int position,
        DefenseTriggerTiming timing,
        IDefenseStatusRuntimeService statusRuntimeService,
        ITreasuryDefenseRuntime treasuryDefenseRuntime,
        string invasionId = "",
        float threat = 0f,
        bool isBoss = false)
    {
        if (statusRuntimeService == null)
        {
            throw new ArgumentNullException(nameof(statusRuntimeService));
        }

        List<DefenseActivationReport> reports = new List<DefenseActivationReport>();
        if (grid == null || intruder == null || !grid.IsValidGridPos(position))
        {
            return reports;
        }

        GridCell cell = grid.GetGridCell(position);
        if (cell == null)
        {
            return reports;
        }

        IEnumerable<DefenseFacility> defenses = cell.GetAllOccupants()
            .OfType<DefenseFacility>()
            .Distinct();
        foreach (DefenseFacility defense in defenses)
        {
            BuildingTreasuryPoweredDefenseAbility paidAbility =
                defense.BuildingData?
                    .GetAbility<BuildingTreasuryPoweredDefenseAbility>();
            TreasuryDefenseAuthorization authorization =
                new TreasuryDefenseAuthorization(
                    true,
                    false,
                    1f,
                    0,
                    string.Empty);
            if (paidAbility != null)
            {
                if (treasuryDefenseRuntime == null)
                {
                    continue;
                }

                authorization = treasuryDefenseRuntime.AuthorizeShot(
                    defense,
                    intruder,
                    invasionId,
                    threat,
                    isBoss);
                if (!authorization.Authorized)
                {
                    continue;
                }
            }

            DefenseActivationReport report = defense.Trigger(
                intruder,
                timing,
                statusRuntimeService,
                authorization.EffectMultiplier);
            if (report != null)
            {
                if (authorization.Malfunctioned)
                {
                    report.AddEffectTag("오작동");
                }

                reports.Add(report);
            }
        }

        return reports;
    }

}

public static class DefenseEffectResolver
{
    public static DefenseEffectApplicationResult ApplyEffectsTransactional(
        DefenseFacility facility,
        CharacterActor target,
        DefenseActivationReport report,
        IDefenseStatusRuntimeService statusRuntimeService,
        ICharacterBodyHealthMutationTransaction bodyHealthMutation,
        float effectMultiplier,
        bool jammed,
        bool misfired)
    {
        if (facility == null
            || target == null
            || report == null
            || facility.Defense == null)
        {
            throw new ArgumentException(
                "Transactional defense effects require facility, target and report.");
        }
        if (statusRuntimeService == null)
            throw new ArgumentNullException(nameof(statusRuntimeService));
        if (bodyHealthMutation == null)
            throw new ArgumentNullException(nameof(bodyHealthMutation));

        DefenseStatusRuntime statusRuntime = statusRuntimeService.GetOrAdd(target)
            ?? throw new InvalidOperationException(
                $"{nameof(IDefenseStatusRuntimeService)} could not provide "
                + $"{nameof(DefenseStatusRuntime)}.");
        CharacterBodyHealthMutationSnapshot bodyBefore =
            bodyHealthMutation.CaptureCombatMutation(target);
        DefenseStatusSnapshot[] statusesBefore = statusRuntime.CaptureMutation();
        DefenseActivationEffectState reportBefore = report.CaptureEffectState();
        var damageReceipts =
            new List<CharacterPreparedAggregateDamageReceipt>();
        var failures = new List<string>();
        int successfulEffects = 0;
        int attemptedEffects = 0;
        float multiplier = Mathf.Clamp(effectMultiplier, 0f, 2f);

        if (jammed)
        {
            failures.Add("mechanical-jam");
        }
        else if (multiplier <= 0f)
        {
            failures.Add("effect-multiplier-zero");
        }
        else
        {
            DefenseFacilityData defense = facility.Defense;
            var context = new DefenseEffectContext(
                target,
                statusRuntime,
                report,
                defense,
                multiplier,
                bodyHealthMutation,
                damageReceipts);
            foreach (DefenseEffectSO effectAsset in
                     defense.effectAssets ?? Array.Empty<DefenseEffectSO>())
            {
                if (effectAsset == null)
                    continue;
                attemptedEffects++;
                CharacterBodyHealthMutationSnapshot effectBodyBefore =
                    bodyHealthMutation.CaptureCombatMutation(target);
                DefenseStatusSnapshot[] effectStatusesBefore =
                    statusRuntime.CaptureMutation();
                DefenseActivationEffectState effectReportBefore =
                    report.CaptureEffectState();
                int receiptCountBefore = damageReceipts.Count;
                try
                {
                    effectAsset.Apply(context);
                    successfulEffects++;
                }
                catch (Exception exception) when (
                    exception is not OutOfMemoryException
                    && exception is not StackOverflowException
                    && exception is not AccessViolationException)
                {
                    bodyHealthMutation.RestoreCombatMutation(
                        target,
                        effectBodyBefore,
                        "defense-effect-asset-rollback");
                    statusRuntime.RestoreMutation(effectStatusesBefore);
                    report.RestoreEffectState(effectReportBefore);
                    if (damageReceipts.Count > receiptCountBefore)
                    {
                        damageReceipts.RemoveRange(
                            receiptCountBefore,
                            damageReceipts.Count - receiptCountBefore);
                    }
                    failures.Add(
                        "effect:" + effectAsset.EffectId + ":"
                        + exception.GetType().Name);
                }
            }
            if (attemptedEffects == 0)
                failures.Add("no-effect-assets");
        }

        CharacterBodyHealthMutationSnapshot bodyAfter =
            bodyHealthMutation.CaptureCombatMutation(target);
        DefenseStatusSnapshot[] statusesAfter = statusRuntime.CaptureMutation();
        DefenseEffectResultKind status = successfulEffects == 0
            ? DefenseEffectResultKind.Failed
            : failures.Count > 0 || misfired
                ? DefenseEffectResultKind.Partial
                : DefenseEffectResultKind.Succeeded;
        string failureSummary = failures.Count == 0
            ? misfired ? "partial-misfire" : "none"
            : string.Join("|", failures);
        return new DefenseEffectApplicationResult(
            target,
            bodyHealthMutation,
            statusRuntime,
            report,
            bodyBefore,
            bodyAfter,
            statusesBefore,
            statusesAfter,
            reportBefore,
            damageReceipts,
            status,
            failureSummary);
    }


    public static void ApplyEffects(
        DefenseFacility facility,
        CharacterActor target,
        DefenseActivationReport report,
        IDefenseStatusRuntimeService statusRuntimeService,
        float effectMultiplier = 1f)
    {
        if (facility == null || target == null || report == null || facility.Defense == null)
        {
            return;
        }

        if (statusRuntimeService == null)
        {
            throw new ArgumentNullException(nameof(statusRuntimeService));
        }

        DefenseFacilityData defense = facility.Defense;
        DefenseStatusRuntime statusRuntime = statusRuntimeService.GetOrAdd(target)
            ?? throw new InvalidOperationException($"{nameof(IDefenseStatusRuntimeService)} could not provide {nameof(DefenseStatusRuntime)}.");
        DefenseEffectContext context = new DefenseEffectContext(
            target,
            statusRuntime,
            report,
            defense,
            effectMultiplier);
        foreach (DefenseEffectSO effectAsset in defense.effectAssets ?? Array.Empty<DefenseEffectSO>())
        {
            if (effectAsset != null)
            {
                effectAsset.Apply(context);
            }
        }
    }

    public static float TickStatuses(
        CharacterActor target,
        float deltaSeconds,
        IDefenseStatusRuntimeService statusRuntimeService)
    {
        if (target == null || target.IsDead)
        {
            return 0f;
        }

        if (statusRuntimeService == null)
        {
            throw new ArgumentNullException(nameof(statusRuntimeService));
        }

        return statusRuntimeService.TickStatuses(target, deltaSeconds);
    }
}

public class DefenseStatusRuntime : MonoBehaviour
{
    private readonly List<DefenseRuntimeStatus> statuses = new List<DefenseRuntimeStatus>();

    public IReadOnlyList<DefenseStatusSnapshot> ActiveStatuses => statuses
        .Select((status) => new DefenseStatusSnapshot(
            status.kind,
            status.value,
            status.remainingSeconds,
            status.stacks))
        .ToArray();

    internal DefenseStatusSnapshot[] CaptureMutation() =>
        ActiveStatuses.ToArray();

    internal void RestoreMutation(
        IReadOnlyList<DefenseStatusSnapshot> snapshot)
    {
        statuses.Clear();
        foreach (DefenseStatusSnapshot value in
                 snapshot ?? Array.Empty<DefenseStatusSnapshot>())
        {
            statuses.Add(new DefenseRuntimeStatus(value.Kind)
            {
                value = value.Value,
                remainingSeconds = value.RemainingSeconds,
                stacks = value.Stacks
            });
        }
    }

    public int ApplyStatus(DefenseStatusKind kind, float value, float duration, int stacks)
    {
        DefenseRuntimeStatus status = statuses.FirstOrDefault((item) => item.kind == kind);
        if (status == null)
        {
            status = new DefenseRuntimeStatus(kind);
            statuses.Add(status);
        }

        status.value = Mathf.Max(status.value, value);
        status.remainingSeconds = Mathf.Max(status.remainingSeconds, duration);
        status.stacks = kind == DefenseStatusKind.Charge
            ? Mathf.Max(0, status.stacks) + Mathf.Max(1, stacks)
            : Mathf.Max(1, stacks);
        return status.stacks;
    }

    public void ClearStatus(DefenseStatusKind kind)
    {
        statuses.RemoveAll((status) => status.kind == kind);
    }

    public float GetIncomingDamageMultiplier()
    {
        DefenseRuntimeStatus corrosion = statuses.FirstOrDefault((status) => status.kind == DefenseStatusKind.Corrosion);
        return corrosion != null ? 1f + Mathf.Max(0f, corrosion.value) : 1f;
    }

    public float Tick(CharacterActor target, float deltaSeconds)
    {
        if (target == null || deltaSeconds <= 0f)
        {
            return 0f;
        }

        float burnDamage = 0f;
        foreach (DefenseRuntimeStatus status in statuses.ToArray())
        {
            if (status.kind == DefenseStatusKind.Burn && status.value > 0f)
            {
                burnDamage += status.value * deltaSeconds;
            }

            status.remainingSeconds -= deltaSeconds;
            if (status.remainingSeconds <= 0f)
            {
                statuses.Remove(status);
            }
        }

        if (burnDamage > 0f && !target.IsDead)
        {
            target.ApplyDamage(burnDamage, "연소");
        }

        return burnDamage;
    }

    private sealed class DefenseRuntimeStatus
    {
        public DefenseRuntimeStatus(DefenseStatusKind kind)
        {
            this.kind = kind;
        }

        public DefenseStatusKind kind;
        public float value;
        public float remainingSeconds;
        public int stacks;
    }
}

public sealed class DefenseEffectApplicationResult
{
    private readonly CharacterActor target;
    private readonly ICharacterBodyHealthMutationTransaction bodyHealthMutation;
    private readonly DefenseStatusRuntime statusRuntime;
    private readonly DefenseActivationReport report;
    private readonly CharacterBodyHealthMutationSnapshot bodyBefore;
    private readonly DefenseActivationEffectState reportBefore;
    private readonly IReadOnlyList<CharacterPreparedAggregateDamageReceipt>
        damageReceipts;
    private bool closed;
    private int completedReceiptCount;

    internal DefenseEffectApplicationResult(
        CharacterActor target,
        ICharacterBodyHealthMutationTransaction bodyHealthMutation,
        DefenseStatusRuntime statusRuntime,
        DefenseActivationReport report,
        in CharacterBodyHealthMutationSnapshot bodyBefore,
        in CharacterBodyHealthMutationSnapshot bodyAfter,
        DefenseStatusSnapshot[] statusesBefore,
        DefenseStatusSnapshot[] statusesAfter,
        in DefenseActivationEffectState reportBefore,
        IReadOnlyList<CharacterPreparedAggregateDamageReceipt> damageReceipts,
        DefenseEffectResultKind status,
        string failureSummary)
    {
        this.target = target;
        this.bodyHealthMutation = bodyHealthMutation;
        this.statusRuntime = statusRuntime;
        this.report = report;
        this.bodyBefore = bodyBefore;
        this.reportBefore = reportBefore;
        this.damageReceipts = damageReceipts
            ?? Array.Empty<CharacterPreparedAggregateDamageReceipt>();
        Status = status;
        FailureSummary = failureSummary ?? "none";
        StatusesBefore = statusesBefore ?? Array.Empty<DefenseStatusSnapshot>();
        StatusesAfter = statusesAfter ?? Array.Empty<DefenseStatusSnapshot>();
        HealthBefore = bodyBefore.State.currentHealth;
        HealthAfter = bodyAfter.State.currentHealth;
        Damage = Mathf.Max(0f, HealthBefore - HealthAfter);
    }

    public DefenseEffectResultKind Status { get; }
    public string FailureSummary { get; }
    public IReadOnlyList<DefenseStatusSnapshot> StatusesBefore { get; }
    public IReadOnlyList<DefenseStatusSnapshot> StatusesAfter { get; }
    public float HealthBefore { get; }
    public float HealthAfter { get; }
    public float Damage { get; }

    public void Rollback()
    {
        if (closed)
            return;
        bodyHealthMutation.RestoreCombatMutation(
            target,
            bodyBefore,
            "defense-effect-outcome-rollback");
        statusRuntime.RestoreMutation(StatusesBefore);
        report.RestoreEffectState(reportBefore);
        closed = true;
    }

    public void Complete()
    {
        if (closed)
            return;
        while (completedReceiptCount < damageReceipts.Count)
        {
            bodyHealthMutation.CompletePreparedAggregateDamage(target, damageReceipts[completedReceiptCount]);
            completedReceiptCount++;
        }
        closed = true;
    }
}
