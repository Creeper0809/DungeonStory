using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Save-compatible join between a physical-domain outbox row and the
/// canonical gameplay-outcome row. The gameplay payload remains authoritative
/// in the gameplay-outcome save section.
/// </summary>
public readonly struct PhysicalGameplayOutcomeAttachment
{
    public PhysicalGameplayOutcomeAttachment(
        GameplayResultKey resultKey,
        GameplayOutcomeId outcomeId,
        GameplayOutcomeReplayState state,
        string canonicalPayloadHash)
    {
        ResultKey = resultKey;
        OutcomeId = outcomeId;
        State = state;
        CanonicalPayloadHash = canonicalPayloadHash?.Trim() ?? string.Empty;
    }

    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeId OutcomeId { get; }
    public GameplayOutcomeReplayState State { get; }
    public string CanonicalPayloadHash { get; }
    public bool IsValid => ResultKey.IsValid
        && OutcomeId.IsValid
        && State is >= GameplayOutcomeReplayState.Committed
            and <= GameplayOutcomeReplayState.Forgotten
        && IsLowercaseSha256(CanonicalPayloadHash);

    public bool HasAcknowledgementProof => IsValid
        && State is >= GameplayOutcomeReplayState.PublishedAcknowledged
            and <= GameplayOutcomeReplayState.Forgotten;

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (!((current >= '0' && current <= '9')
                || (current >= 'a' && current <= 'f')))
                return false;
        }
        return true;
    }
}

/// <summary>
/// Domain-agnostic participant prepared after the exact physical receipt is
/// calculated and before any physical state mutation is applied.
/// </summary>
public interface IPreparedPhysicalItemGameplayOutcome
{
    GameplayResultKey ResultKey { get; }
    bool IsCanonicalReplay { get; }
    bool TryCommit(
        long expectedOwnerRevision,
        out PhysicalGameplayOutcomeAttachment attachment,
        out bool canonicalCommitted,
        out string failureReason);
    void Cancel();
}

// A prepared (not replayed) standard physical outcome may join an existing
// owner atomic batch. This does not expose a second item mutation authority.
internal interface IPreparedPhysicalItemGameplayOutcomeToken :
    IPreparedPhysicalItemGameplayOutcome
{
    PreparedOutcomeToken Token { get; }
}

public interface IPhysicalItemBatchDispositionOutcomeParticipant
{
    bool TryPrepare(
        in PhysicalItemBatchDispositionReceipt exactReceipt,
        long expectedOwnerRevision,
        out IPreparedPhysicalItemGameplayOutcome prepared,
        out string failureReason);
}

/// <summary>
/// Marker for the standard physical-disposition narrative participant. Other
/// domains may provide their own typed participant without teaching the item
/// transaction about receipt types.
/// </summary>
public interface IDefaultPhysicalItemBatchDispositionOutcomeParticipant :
    IPhysicalItemBatchDispositionOutcomeParticipant
{
}

public interface IOutcomeAwarePhysicalItemBatchDispositionService
{
    bool TryCommitPending(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        IPhysicalItemBatchDispositionOutcomeParticipant outcomeParticipant,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);
}

public interface IOutcomeAwareReservedPhysicalItemBatchDispositionService
{
    bool TryCommitReservedSinkPending(string leaseId, int quantity, string operationId, string reasonCode,
        IPhysicalItemBatchDispositionOutcomeParticipant outcomeParticipant,
        out PhysicalItemBatchDispositionReceipt receipt, out string failureReason);
}

/// <summary>Callback-free owner publication joined to exact pending-receipt removal.</summary>
public interface IPhysicalItemDispositionAcknowledgementParticipant
{
    bool TryCommit(out string failureReason);
    void Rollback();
}

public interface IOutcomeAwarePhysicalItemDispositionAcknowledgementService
{
    bool Acknowledge(string commitId, IPhysicalItemDispositionAcknowledgementParticipant participant,
        out string failureReason);
}
