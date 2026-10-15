using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

/// <summary>An optional second domain owner enlisted before the exact physical debit.</summary>
public interface IPhysicalItemDispositionEffect
{
    bool TryStage(in PhysicalItemBatchDispositionReceipt exactReceipt, long expectedOwnerRevision,
        out IDomainFactStage stage, out string failureReason);
}

public interface IJointPhysicalItemBatchDispositionService
{
    bool TryCommitPending(IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind, string operationId, string reasonCode,
        IPhysicalItemDispositionEffect effect, out PhysicalItemBatchDispositionReceipt receipt, out string failureReason);
}

public interface IJointReservedPhysicalItemBatchDispositionService
{
    bool TryCommitReservedSinkPending(string leaseId, int quantity, string operationId, string reasonCode,
        IPhysicalItemDispositionEffect effect, out PhysicalItemBatchDispositionReceipt receipt, out string failureReason);
}
