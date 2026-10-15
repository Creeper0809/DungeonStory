using System;
using DungeonStory.Foundation.Facts;

/// <summary>Durable domain publication only. The staff owner applies/rolls back gameplay; this class never reapplies it.</summary>
public sealed class StaffDiscontentPublicationRuntime
{
    private readonly IDomainFactStagingPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    public StaffDiscontentPublicationRuntime(IDomainFactStagingPublisher facts, IDomainFactRecoveryPublisher recovery)
    {
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }

    public bool TryStage(StaffDiscontentFactData data, out IDomainFactStage stage, out string failure)
    {
        stage = null; failure = string.Empty;
        IDomainFactStage candidate = null;
        try
        {
            bool accepted = data.kind == StaffDiscontentFactKind.DailyTransition
                ? facts.TryStage(new StaffDiscontentTransitionedFact(data), out candidate, out failure)
                : facts.TryStage(new StaffRebellionResponseResolvedFact(data), out candidate, out failure);
            if (!accepted) return false;
            if (candidate == null || !candidate.IsActive || candidate.IsReplay || candidate.OwnerRevision != data.revision
                || string.IsNullOrWhiteSpace(candidate.OperationKey))
            { failure = "staff-discontent-unapplied-publication-conflict"; return false; }
            stage = candidate; candidate = null; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "staff-discontent-stage:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { candidate?.Cancel(); }
    }

    public DomainFactDelivery CommitApplied(StaffDiscontentState owner, StaffDiscontentFactData data, IDomainFactStage stage)
    {
        owner.RecordAppliedPublication(data, stage.OperationKey);
        DomainFactDelivery delivery;
        try { delivery = stage.Commit(); }
        catch (Exception exception) when (Recoverable(exception))
        { delivery = DomainFactDelivery.Uncertain("staff-discontent-commit:" + exception.GetType().Name); }
        if (Delivered(stage.OperationKey, delivery))
        {
            // Once Commit may have succeeded, an acknowledgement failure must not undo gameplay.
            try { owner.MarkPublicationDelivered(data.kind, stage.OperationKey); }
            catch (Exception exception) when (Recoverable(exception))
            { delivery = DomainFactDelivery.Uncertain("staff-discontent-acknowledgement:" + exception.GetType().Name); }
        }
        else if (delivery.Completed) delivery = DomainFactDelivery.Uncertain("staff-discontent-delivery-proof-invalid");
        return delivery;
    }

    public bool TryRecover(StaffDiscontentState owner, out string failure)
    {
        failure = string.Empty;
        foreach (var observation in new[] { owner.TransitionObservation, owner.ResponseObservation })
        {
            if (!observation.pending) continue;
            try
            {
                var data = observation.fact;
                DomainFactReplay replay = data.kind == StaffDiscontentFactKind.DailyTransition
                    ? DomainFactReplay.Create(observation.publicationKey, new StaffDiscontentTransitionedFact(data))
                    : DomainFactReplay.Create(observation.publicationKey, new StaffRebellionResponseResolvedFact(data));
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { replay }, results, out failure)
                    || !Delivered(observation.publicationKey, results[0]))
                { failure = "staff-discontent-recovery-pending:" + failure + ":" + results[0].Detail; return false; }
                owner.MarkPublicationDelivered(data.kind, observation.publicationKey);
            }
            catch (Exception exception) when (Recoverable(exception))
            { failure = "staff-discontent-recovery:" + exception.GetType().Name + ":" + exception.Message; return false; }
        }
        return true;
    }

    private static bool Delivered(string key, DomainFactDelivery value)
    {
        if (!value.Completed || value.OperationKey != key || string.IsNullOrWhiteSpace(value.PublicationId) || value.Fingerprint?.Length != 64)
            return false;
        foreach (char c in value.Fingerprint)
            if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')) return false;
        return true;
    }
    private static bool Recoverable(Exception value) => value is not OutOfMemoryException
        && value is not StackOverflowException && value is not AccessViolationException;
}
