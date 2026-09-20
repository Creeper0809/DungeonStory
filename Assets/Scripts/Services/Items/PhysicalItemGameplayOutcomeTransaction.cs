using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Narrative.Korean;
using UnityEngine;

/// <summary>
/// Immutable, pre-mutation snapshot of one exact source slice participating in
/// a physical disposition.  This is receipt data, not a second inventory
/// authority.
/// </summary>
public readonly struct PhysicalItemDispositionSourceFact
{
    public PhysicalItemDispositionSourceFact(
        string stackId,
        string itemDefinitionId,
        string itemInstanceId,
        int quantity,
        long massGrams,
        Vector2Int sourcePosition,
        KoreanNameSnapshot displayName)
    {
        StackId = stackId?.Trim() ?? string.Empty;
        ItemDefinitionId = itemDefinitionId?.Trim() ?? string.Empty;
        ItemInstanceId = itemInstanceId?.Trim() ?? string.Empty;
        Quantity = quantity;
        MassGrams = massGrams;
        SourcePosition = sourcePosition;
        DisplayName = displayName;
    }

    public string StackId { get; }
    public string ItemDefinitionId { get; }
    public string ItemInstanceId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public Vector2Int SourcePosition { get; }
    public KoreanNameSnapshot DisplayName { get; }

    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(StackId)
        && GameplayOutcomeStableIdSyntax.IsValid(ItemDefinitionId)
        && (ItemInstanceId.Length == 0
            || GameplayOutcomeStableIdSyntax.IsValid(ItemInstanceId))
        && Quantity > 0
        && MassGrams > 0L
        && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(DisplayName);
}

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

internal static class PhysicalGameplayOutcomeSaveCodec
{
    internal static PhysicalItemDispositionSourceFactSaveData ToSave(
        in PhysicalItemDispositionSourceFact fact) => new()
    {
        stackId = fact.StackId,
        itemDefinitionId = fact.ItemDefinitionId,
        itemInstanceId = fact.ItemInstanceId,
        quantity = fact.Quantity,
        massGrams = fact.MassGrams,
        sourceX = fact.SourcePosition.x,
        sourceY = fact.SourcePosition.y,
        displayText = fact.DisplayName.DisplayText,
        displaySnapshotRevision = fact.DisplayName.DisplaySnapshotRevision,
        pronunciationMode = (int)fact.DisplayName.PronunciationHint.Mode,
        pronunciationValue = fact.DisplayName.PronunciationHint.Value,
        explicitFinalConsonant =
            (int)fact.DisplayName.PronunciationHint.ExplicitFinalConsonant,
        pronunciationRevision = fact.DisplayName.PronunciationHint.Revision,
        locale = fact.DisplayName.Locale
    };

    internal static PhysicalItemDispositionSourceFact FromSave(
        PhysicalItemDispositionSourceFactSaveData fact)
    {
        if (fact == null)
            return default;
        return new PhysicalItemDispositionSourceFact(
            fact.stackId,
            fact.itemDefinitionId,
            fact.itemInstanceId,
            fact.quantity,
            fact.massGrams,
            new Vector2Int(fact.sourceX, fact.sourceY),
            new KoreanNameSnapshot(
                fact.displayText,
                fact.displaySnapshotRevision,
                new KoreanPronunciationHint(
                    (KoreanPronunciationMode)fact.pronunciationMode,
                    fact.pronunciationValue,
                    (KoreanFinalConsonantKind)fact.explicitFinalConsonant,
                    fact.pronunciationRevision),
                fact.locale));
    }

    internal static PhysicalGameplayOutcomeAttachmentSaveData ToSave(
        in PhysicalGameplayOutcomeAttachment attachment) => new()
    {
        producerId = attachment.ResultKey.ProducerId,
        operationId = attachment.ResultKey.OperationId.Value,
        commitRevision = attachment.ResultKey.CommitRevision,
        localResultIndex = attachment.ResultKey.LocalResultIndex,
        outcomeRunId = attachment.OutcomeId.RunId.Value,
        outcomeSequence = attachment.OutcomeId.Sequence,
        replayState = (int)attachment.State,
        canonicalPayloadHash = attachment.CanonicalPayloadHash
    };

    internal static PhysicalGameplayOutcomeAttachment FromSave(
        PhysicalGameplayOutcomeAttachmentSaveData attachment)
    {
        if (attachment == null)
            return default;
        return new PhysicalGameplayOutcomeAttachment(
            new GameplayResultKey(
                attachment.producerId,
                new GameplayOperationId(attachment.operationId),
                attachment.commitRevision,
                attachment.localResultIndex),
            new GameplayOutcomeId(
                new GameplayOutcomeRunId(attachment.outcomeRunId),
                attachment.outcomeSequence),
            (GameplayOutcomeReplayState)attachment.replayState,
            attachment.canonicalPayloadHash);
    }

    internal static bool Equals(
        PhysicalGameplayOutcomeAttachmentSaveData left,
        PhysicalGameplayOutcomeAttachmentSaveData right) =>
        left != null
        && right != null
        && string.Equals(left.producerId, right.producerId, StringComparison.Ordinal)
        && string.Equals(left.operationId, right.operationId, StringComparison.Ordinal)
        && left.commitRevision == right.commitRevision
        && left.localResultIndex == right.localResultIndex
        && string.Equals(
            left.outcomeRunId,
            right.outcomeRunId,
            StringComparison.Ordinal)
        && left.outcomeSequence == right.outcomeSequence
        && left.replayState == right.replayState
        && string.Equals(
            left.canonicalPayloadHash,
            right.canonicalPayloadHash,
            StringComparison.Ordinal);
}
