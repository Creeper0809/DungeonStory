using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

// Transitional observation-layer adapter for joint owners not yet migrated to
// domain facts. The physical mutation core never knows these legacy contracts.
public sealed class PhysicalItemLegacyOutcomeEffect : IPhysicalItemDispositionEffect
{
    private readonly IPhysicalItemBatchDispositionOutcomeParticipant participant;
    public PhysicalItemLegacyOutcomeEffect(IPhysicalItemBatchDispositionOutcomeParticipant participant)
        => this.participant = participant ?? throw new ArgumentNullException(nameof(participant));

    public bool TryStage(in PhysicalItemBatchDispositionReceipt receipt, long revision,
        out IDomainFactStage stage, out string failure)
    {
        stage = null;
        if (!participant.TryPrepare(receipt, revision, out var prepared, out failure)) return false;
        if (prepared == null || !prepared.ResultKey.IsValid || prepared.ResultKey.CommitRevision != revision)
        {
            prepared?.Cancel();
            failure = "physical-joint-publication-invalid";
            return false;
        }
        stage = new Stage(prepared, revision);
        return true;
    }

    private sealed class Stage : IDomainFactStage
    {
        private readonly IPreparedPhysicalItemGameplayOutcome prepared;
        private bool terminal;
        public Stage(IPreparedPhysicalItemGameplayOutcome prepared, long revision)
        { this.prepared = prepared; OwnerRevision = revision; }
        public string OperationKey => prepared.ResultKey.ToString();
        public long OwnerRevision { get; }
        public bool IsReplay => prepared.IsCanonicalReplay;
        public bool IsActive => !terminal;
        public DomainFactDelivery Commit()
        {
            if (terminal) return DomainFactDelivery.Reject("physical-joint-stage-terminal");
            terminal = true;
            try
            {
                bool accepted = prepared.TryCommit(OwnerRevision, out var attachment, out bool committed, out string failure);
                if (attachment.IsValid && attachment.ResultKey == prepared.ResultKey)
                    return new DomainFactDelivery(attachment.HasAcknowledgementProof
                        ? DomainFactDeliveryPhase.Delivered : DomainFactDeliveryPhase.AcceptedPendingDelivery,
                        OperationKey, attachment.OutcomeId.ToString(), attachment.CanonicalPayloadHash, failure);
                if (accepted || committed)
                    return new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt, OperationKey, null, null, failure);
                prepared.Cancel();
                return DomainFactDelivery.Reject(failure);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            {
                return new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt, OperationKey, null, null,
                    "physical-joint-commit-uncertain:" + exception.GetType().Name);
            }
        }
        public void Cancel()
        {
            if (terminal) return;
            terminal = true;
            prepared.Cancel();
        }
    }
}

public sealed class PhysicalItemLegacyOutcomeServices :
    IOutcomeAwarePhysicalItemBatchDispositionService, IOutcomeAwareReservedPhysicalItemBatchDispositionService
{
    private readonly IJointPhysicalItemBatchDispositionService batches;
    private readonly IJointReservedPhysicalItemBatchDispositionService reserved;
    public PhysicalItemLegacyOutcomeServices(IJointPhysicalItemBatchDispositionService batches,
        IJointReservedPhysicalItemBatchDispositionService reserved)
    { this.batches = batches; this.reserved = reserved; }
    public bool TryCommitPending(IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind, string operationId, string reasonCode,
        IPhysicalItemBatchDispositionOutcomeParticipant participant,
        out PhysicalItemBatchDispositionReceipt receipt, out string failureReason)
        => batches.TryCommitPending(inputs, kind, operationId, reasonCode,
            participant == null ? null : new PhysicalItemLegacyOutcomeEffect(participant), out receipt, out failureReason);
    public bool TryCommitReservedSinkPending(string leaseId, int quantity, string operationId, string reasonCode,
        IPhysicalItemBatchDispositionOutcomeParticipant participant,
        out PhysicalItemBatchDispositionReceipt receipt, out string failureReason)
        => reserved.TryCommitReservedSinkPending(leaseId, quantity, operationId, reasonCode,
            participant == null ? null : new PhysicalItemLegacyOutcomeEffect(participant), out receipt, out failureReason);
}
