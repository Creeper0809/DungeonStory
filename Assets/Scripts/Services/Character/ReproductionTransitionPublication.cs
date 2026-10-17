using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

/// <summary>Reproduction owns the reversible candidate and its durable facts.
/// Observers own storage, identities and delivery; recovery never repeats gameplay.</summary>
public sealed class ReproductionTransitionPublication
{
    private readonly IReproductionPersistence persistence;
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly ISettlementAlertService alerts;
    private int recoveryCursor;
    public ReproductionTransitionFactSource Source { get; }

    public ReproductionTransitionPublication(IReproductionPersistence persistence, ReproductionTransitionFactSource source,
        IDomainFactStagingPublisher publisher, IDomainFactRecoveryPublisher recovery, ISettlementAlertService alerts)
    {
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
    }

    public bool TryCommit(ReproductionWorldSaveData before, ReproductionWorldAggregate candidate,
        ReproductionProcess process, ReproductionTransitionFact fact, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        IDomainFactStage stage;
        try
        {
            if (!publisher.TryStage(fact, out stage, out string reason)) return Fail(reason, out failure);
        }
        catch (Exception exception) { return Fail("reproduction-observer-prepare:" + exception.Message, out failure); }
        try
        {
            process.StageTransition(fact, stage.OperationKey);
            persistence.PublishRestore(candidate);
        }
        catch { stage.Cancel(); throw; }
        DomainFactDelivery result;
        try { result = stage.Commit(); }
        catch (Exception exception) { result = DomainFactDelivery.Uncertain("reproduction-observer-reply:" + exception.Message); }
        finally { if (stage.IsActive) stage.Cancel(); }
        if (result.Phase == DomainFactDeliveryPhase.Rejected)
        {
            // Previously consumed inputs/created child authorities are in 'before'.
            // Only the reversible process transition is restored here.
            persistence.PublishRestore(persistence.PrepareRestore(before));
            return Fail(result.Detail, out failure);
        }
        if (result.Completed && result.OperationKey == process.PendingTransition.publicationKey)
        {
            if (!TryFinish(process.ProcessId, result, out failure)) Report(failure);
        }
        else UnityEngine.Debug.LogWarning("reproduction-observation-pending:" + process.ProcessId + ":" + result.Detail);
        // An unknown reply cannot authorize the enclosing equipment command to
        // roll back an effect that might already have been durably observed.
        failure = DomainFailure.None;
        return true;
    }

    public bool TryResume(string processId, out DomainFailure failure)
    {
        var candidate = persistence.PrepareRestore(persistence.Capture());
        if (!candidate.TryGet(processId, out var process)) return Fail("reproduction-process-missing", out failure);
        var pending = process.PendingTransition;
        if (pending == null) { failure = DomainFailure.None; return true; }
        var replies = new DomainFactDelivery[1];
        try
        {
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey,
                    new ReproductionTransitionFact(pending.fact)) }, replies, out string reason))
                return Fail(reason, out failure);
        }
        catch (Exception exception) { return Fail("reproduction-observer-recovery:" + exception.Message, out failure); }
        if (!replies[0].Completed || replies[0].OperationKey != pending.publicationKey)
            return Fail("reproduction-observation-awaiting-delivery:" + replies[0].Detail, out failure);
        return TryFinish(processId, replies[0], out failure);
    }

    public bool TryResumePending(out DomainFailure failure)
    {
        var pending = persistence.Capture().processes.Where(value => value.pendingTransitions.Count > 0)
            .OrderBy(value => value.processId, StringComparer.Ordinal).ToArray();
        failure = DomainFailure.None;
        if (pending.Length == 0) { recoveryCursor = 0; return true; }
        int start = recoveryCursor % pending.Length;
        int count = Math.Min(8, pending.Length);
        for (int index = 0; index < count; index++)
            if (!TryResume(pending[(start + index) % pending.Length].processId, out var reason))
                failure = reason;
        recoveryCursor = (start + count) % pending.Length;
        return !failure.IsFailure;
    }

    private bool TryFinish(string processId, DomainFactDelivery delivery, out DomainFailure failure)
    {
        // Never mutate an already published candidate while acknowledging. A failed
        // acknowledgement must leave the durable fact available for the next replay.
        try { return TryFinishCore(processId, delivery, out failure); }
        catch (Exception exception) { return Fail("reproduction-observer-acknowledgement:" + exception.Message, out failure); }
    }

    private bool TryFinishCore(string processId, DomainFactDelivery delivery, out DomainFailure failure)
    {
        var candidate = persistence.PrepareRestore(persistence.Capture());
        if (!candidate.TryGet(processId, out var process) || process.PendingTransition == null
            || process.PendingTransition.publicationKey != delivery.OperationKey)
            return Fail("reproduction-observer-pending-key-conflict", out failure);
        var fact = process.PendingTransition.fact;
        if (string.IsNullOrWhiteSpace(delivery.PublicationId) || string.IsNullOrWhiteSpace(delivery.Fingerprint))
            return Fail("reproduction-observer-acknowledgement-missing", out failure);
        if (fact.transitionKind == "emergency-extraction" || fact.failureAfter == ReproductionFailureCode.EmergencyExtractionExpired)
        {
            string incident = ReproductionEmergencyExtractionCommandRuntime.IncidentId(process.ProcessId);
            var result = alerts.ResolveIncident(incident, alerts.GetNextIncidentRevision(incident));
            if (!result.Success) return Fail("reproduction-emergency-resolution:" + result.Code, out failure);
        }
        process.AcknowledgeTransition(delivery.OperationKey, delivery.PublicationId, delivery.Fingerprint);
        persistence.PublishRestore(candidate);
        failure = DomainFailure.None;
        return true;
    }

    private static bool Fail(string reason, out DomainFailure failure)
    {
        failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable,
            string.IsNullOrWhiteSpace(reason) ? "reproduction-observation-unavailable" : reason);
        return false;
    }
    private static void Report(DomainFailure failure) => UnityEngine.Debug.LogWarning(
        "reproduction-observation-recovery-pending:" + string.Join(",", failure.Parameters.ToArray()));
}
