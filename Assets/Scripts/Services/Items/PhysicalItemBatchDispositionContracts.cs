using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct PhysicalItemBatchDispositionReceipt
{
    internal PhysicalItemBatchDispositionReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        string requestFingerprint,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams)
        : this(
            kind,
            operationId,
            reasonCode,
            requestFingerprint,
            sourceStackIds,
            quantity,
            inputMassGrams,
            0L,
            Array.Empty<PhysicalItemDispositionSourceFact>())
    {
    }

    internal PhysicalItemBatchDispositionReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        string requestFingerprint,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams,
        long ownerRevision,
        IReadOnlyList<PhysicalItemDispositionSourceFact> sourceFacts)
    {
        Kind = kind;
        OperationId = operationId;
        ReasonCode = reasonCode;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        SourceStackIds = Array.AsReadOnly((sourceStackIds ?? Array.Empty<string>()).ToArray());
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
        OwnerRevision = ownerRevision;
        SourceFacts = Array.AsReadOnly((sourceFacts
            ?? Array.Empty<PhysicalItemDispositionSourceFact>()).ToArray());
        CommitId = $"physical-batch-disposition:{(int)kind}:{operationId}:{quantity}:{inputMassGrams}";
    }

    public PhysicalItemDispositionKind Kind { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public long OwnerRevision { get; }
    public IReadOnlyList<PhysicalItemDispositionSourceFact> SourceFacts { get; }
    public string CommitId { get; }
    public bool IsCommitted => Kind is PhysicalItemDispositionKind.Transfer
            or PhysicalItemDispositionKind.Sink
        && OperationId?.Length > 0
        && ReasonCode?.Length > 0
        && SourceStackIds?.Count > 0
        && Quantity > 0
        && InputMassGrams > 0L;
}

public interface IPhysicalItemBatchDispositionService
{
    bool TryCommit(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool TryCommitPending(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool Acknowledge(string commitId, out string failureReason);

    bool TryGetPending(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt);
}

/// <summary>
/// Item-layer boundary for converting an already reserved physical quantity
/// into a durable pending terminal receipt. The reservation debit and world
/// stack debit are committed as one in-process transaction; the owning domain
/// acknowledges the receipt only after publishing its own terminal state.
/// </summary>
public interface IReservedPhysicalItemBatchDispositionService
{
    bool TryCommitReservedSinkPending(
        string leaseId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool TryCommitReservedTransferPending(
        string leaseId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);
}

/// <summary>
/// Dedicated terminal boundary for an exact stack already held by a character.
/// Generic batch disposition continues to reject Carried sources; the owning
/// character adapter must update its carry inventory in the same rollback scope.
/// </summary>
public interface ICarriedPhysicalItemBatchDispositionService
{
    bool TryCommitCarriedSinkPending(
        string stackId,
        int quantity,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);
}

/// <summary>
/// Short-lived in-process rollback handle for a carried terminal Sink. The
/// handle is deliberately not a second inventory authority: it only retains
/// the exact repository mutation until the owning domain publishes or rejects
/// its state transition in the same synchronous call.
/// </summary>
public interface IReversiblePhysicalItemDisposition
{
    PhysicalItemBatchDispositionReceipt Receipt { get; }
    bool TryRollback(out string failureReason);
    bool TryAcknowledge(out string failureReason);
}

public interface IReversibleCarriedPhysicalItemBatchDispositionService
{
    bool TryCommitCarriedSinkReversible(
        string stackId,
        int quantity,
        string operationId,
        string reasonCode,
        out IReversiblePhysicalItemDisposition transaction,
        out string failureReason);
}
