using System;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

// Owns only unfinished work-accident delivery/follow-up in the existing work
// aggregate. The executor remains the authority for damage and interruption.
public sealed class WorkAccidentObservationRuntime : ITickable, IDungeonSaveRestoreCompletedHook
{
    private readonly WorkOrderAggregateStateStore state;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly IEnvironmentalFireCommand fire;
    private readonly IGameClock clock;
    private bool busy;
    private float nextAttempt;
    public string LastFailure { get; private set; } = string.Empty;

    public WorkAccidentObservationRuntime(WorkOrderAggregateStateStore state,
        IDomainFactRecoveryPublisher recovery, CharacterIdentityEventPublisher identityEvents,
        IEnvironmentalFireCommand fire, IGameClock clock)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.identityEvents = identityEvents ?? throw new ArgumentNullException(nameof(identityEvents));
        this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void RequireReady(string workerId)
    {
        if (busy || state.Current.PendingAccidents.Values.Any(row => row.fact.workerId == workerId))
            throw new InvalidOperationException("work-accident-prior-observation-unresolved:" + workerId);
    }

    public static void CaptureFirePlan(WorkAccidentFactSaveData fact, BuildableObject facility)
    {
        var source = facility.BuildingData?.GetAbility<BuildingProcessAccidentFireSourceAbility>();
        var target = facility.BuildingData?.GetAbility<BuildingEnvironmentalFireAbility>();
        if (source == null || target == null || !source.Supports(fact.workTypeId)
            || !target.Accepts(EnvironmentalFireIgnitionKind.ProcessAccident)) return;
        source.ValidateOrThrow();
        target.CreateProfileOrThrow();
        fact.fireEligible = true;
        fact.fireIntensity = source.ignitionIntensity;
        fact.fireTargetDisplayName = FacilityShopService.GetBuildingName(facility.BuildingData);
    }

    public void RecordApplied(WorkAccidentFact fact, string publicationKey)
    {
        var snapshot = fact.Snapshot();
        RequireReady(snapshot.workerId);
        var row = new WorkAccidentObservationSaveData { fact = snapshot, publicationKey = publicationKey };
        row.Validate();
        state.Writable.PendingAccidents.Add(snapshot.operationId, row);
    }

    public void MarkDelivery(string operationId, DomainFactDelivery result)
    {
        var row = state.Writable.PendingAccidents[operationId];
        if (result.Completed && result.OperationKey == row.publicationKey
            && !string.IsNullOrWhiteSpace(result.PublicationId)
            && result.Fingerprint.Length == 64 && result.Fingerprint.All(Uri.IsHexDigit))
            row.observationCompleted = true;
        else if (result.Completed)
            LastFailure = "work-accident-observation-proof-invalid:" + operationId;
    }

    public void RollbackRejected(string operationId, Action rollback)
    {
        var row = state.Writable.PendingAccidents[operationId];
        row.rollbackBlocked = true;
        row.rollbackFailure = "work-accident-known-rejection";
        // Keep the failed applied fact if rollback itself fails. Never replay it.
        rollback();
        state.Writable.PendingAccidents.Remove(operationId);
    }

    public void CompleteFollowups(string operationId)
    {
        if (busy) return;
        busy = true;
        try { ContinueOne(operationId); }
        catch (Exception exception) when (Recoverable(exception))
        { Report(operationId, exception); }
        finally { busy = false; }
    }

    private void ContinueOne(string operationId)
    {
        if (!state.Writable.PendingAccidents.TryGetValue(operationId, out var row) || row.rollbackBlocked) return;
        var fact = row.fact;
        // Injury is already applied even while its observation is uncertain.
        // These transient fan-out notifications are attempted once, not durable
        // effects. Mark first to prevent a throwing subscriber from duplicating
        // earlier listeners when the fire follow-up is retried.
        if (!row.identityNotificationAttempted)
        {
            row.identityNotificationAttempted = true;
            try
            {
                identityEvents.Publish(new CharacterInjuredIdentityEvent(new CharacterId(fact.workerId), default,
                    CombatDamageType.Blunt, fact.appliedDamage, fact.absoluteDay, fact.operationId,
                    fact.workerDisplayName, new WorkTypeId(fact.workTypeId), new BuildingInstanceId(fact.facilityId),
                    fact.facilityDisplayName, new CoreGridCell(fact.x, fact.y), fact.anatomyNodeId,
                    fact.observedCause, (CharacterCommandOrigin)fact.commandOrigin, fact.accidentMultiplier));
            }
            catch (Exception exception) when (Recoverable(exception))
            { Report(operationId, exception, "work-accident-identity-notification-failed"); }
        }
        if (!state.Current.PendingAccidents.TryGetValue(operationId, out var current)
            || !ReferenceEquals(current, row)) return;
        if (!row.observationCompleted) return;
        if (fact.fireEligible)
        {
            var result = fire.TryIgnite(new EnvironmentalFireIgnitionRequest(
                "environmental-fire-process-accident:" + fact.operationId,
                EnvironmentalFireIgnitionKind.ProcessAccident, fact.operationId,
                new EnvironmentalFireTargetRef(EnvironmentalFireTargetKind.Building, fact.facilityId),
                fact.fireIntensity, "work-accident-receipt:" + fact.operationId,
                targetDisplayName: fact.fireTargetDisplayName));
            if (result.Disposition is EnvironmentalFireIgnitionDisposition.CauseConflict
                or EnvironmentalFireIgnitionDisposition.InvalidRequest)
                throw new InvalidOperationException("work-accident-fire-request-rejected:" + result.Disposition);
            if (result.Disposition is not (EnvironmentalFireIgnitionDisposition.Ignited
                or EnvironmentalFireIgnitionDisposition.PreviouslyProcessed or EnvironmentalFireIgnitionDisposition.AlreadyBurning))
                Debug.LogWarning("work-accident-fire-not-applied:" + operationId + ":" + result.Disposition);
        }
        if (state.Current.PendingAccidents.TryGetValue(operationId, out current)
            && ReferenceEquals(current, row))
            state.Writable.PendingAccidents.Remove(operationId);
    }

    public void Tick()
    {
        if (clock.IsPaused || clock.Time < nextAttempt) return;
        nextAttempt = clock.Time + 5f;
        Resume();
    }
    public void OnRestoreCompleted()
    {
        nextAttempt = clock.Time;
        Resume();
    }

    private void Resume()
    {
        if (busy) return;
        busy = true;
        try
        {
            foreach (string id in state.Current.PendingAccidents.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray())
            {
                try
                {
                    var row = state.Current.PendingAccidents[id];
                    if (row.rollbackBlocked) continue;
                    if (!row.observationCompleted)
                    {
                        var results = new DomainFactDelivery[1];
                        recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey, new WorkAccidentFact(row.fact)) },
                            results, out string failure);
                        MarkDelivery(id, results[0]);
                        if (!results[0].Completed) LastFailure = "work-accident-observation-pending:" + id + ":" + failure;
                    }
                    ContinueOne(id);
                }
                catch (Exception exception) when (Recoverable(exception))
                { Report(id, exception, "work-accident-recovery-failed"); }
            }
        }
        finally { busy = false; }
    }

    private void Report(string id, Exception exception, string prefix = "work-accident-fire-follow-up-failed")
    {
        LastFailure = prefix + ";operation=" + id + ";injuryCommitted=true;error="
            + exception.GetType().FullName + ":" + exception.Message;
        Debug.LogError(LastFailure);
    }
    private static bool Recoverable(Exception exception) =>
        exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
}
