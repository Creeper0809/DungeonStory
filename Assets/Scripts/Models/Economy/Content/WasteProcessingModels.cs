using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public readonly struct WasteFeedResult
{
    public WasteFeedResult(
        bool succeeded,
        string itemId,
        WasteOriginKind origin,
        float contamination,
        float nutrition,
        float diseaseChance,
        WasteFeedOutcomeCode outcome,
        DomainFailure failure)
    {
        Succeeded = succeeded;
        ItemId = itemId ?? string.Empty;
        Origin = origin;
        Contamination = Mathf.Clamp(contamination, 0f, 100f);
        Nutrition = Mathf.Clamp01(nutrition);
        DiseaseChance = Mathf.Clamp01(diseaseChance);
        Outcome = outcome;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public string ItemId { get; }
    public WasteOriginKind Origin { get; }
    public float Contamination { get; }
    public float Nutrition { get; }
    public float DiseaseChance { get; }
    public WasteFeedOutcomeCode Outcome { get; }
    public DomainFailure Failure { get; }

}

public readonly struct WasteDirectFeedCandidate
{
    public WasteDirectFeedCandidate(
        ItemStackId stackId,
        string itemId,
        WasteOriginKind origin,
        float contamination,
        float nutrition,
        float diseaseChance)
    {
        StackId = stackId;
        ItemId = itemId ?? string.Empty;
        Origin = origin;
        Contamination = Mathf.Clamp(contamination, 0f, 100f);
        Nutrition = Mathf.Clamp01(nutrition);
        DiseaseChance = Mathf.Clamp01(diseaseChance);
    }

    public ItemStackId StackId { get; }
    public string ItemId { get; }
    public WasteOriginKind Origin { get; }
    public float Contamination { get; }
    public float Nutrition { get; }
    public float DiseaseChance { get; }
    public bool IsValid => StackId.IsValid
        && !string.IsNullOrWhiteSpace(ItemId)
        && Origin != WasteOriginKind.Unknown
        && Nutrition > 0f;
}

public enum WasteFeedOutcomeCode
{
    None = 0,
    FeedDeliveryRequested,
    FeedConsumed
}

public readonly struct WastePolicyCommandResult
{
    public WastePolicyCommandResult(bool succeeded, DomainFailure failure)
        : this(
            succeeded,
            failure,
            string.Empty,
            0L,
            null,
            null)
    {
    }

    public WastePolicyCommandResult(
        bool succeeded,
        DomainFailure failure,
        string operationId,
        long ownerRevision,
        WastePolicyData beforePolicy,
        WastePolicyData afterPolicy)
    {
        Succeeded = succeeded;
        Failure = failure;
        OperationId = operationId?.Trim() ?? string.Empty;
        OwnerRevision = ownerRevision;
        BeforePolicy = beforePolicy?.Clone();
        AfterPolicy = afterPolicy?.Clone();
    }

    public bool Succeeded { get; }
    public DomainFailure Failure { get; }
    public string OperationId { get; }
    public long OwnerRevision { get; }
    public WastePolicyData BeforePolicy { get; }
    public WastePolicyData AfterPolicy { get; }
}

public readonly struct WastePolicyOutcomeKey : IEquatable<WastePolicyOutcomeKey>
{
    public WastePolicyOutcomeKey(
        string producerId,
        string operationId,
        long commitRevision,
        int localResultIndex)
    {
        ProducerId = producerId?.Trim() ?? string.Empty;
        OperationId = operationId?.Trim() ?? string.Empty;
        CommitRevision = commitRevision;
        LocalResultIndex = localResultIndex;
    }

    public string ProducerId { get; }
    public string OperationId { get; }
    public long CommitRevision { get; }
    public int LocalResultIndex { get; }
    public bool IsValid => IsStableId(ProducerId)
        && IsStableId(OperationId)
        && CommitRevision >= 0L
        && LocalResultIndex >= 0;

    public bool Equals(WastePolicyOutcomeKey other) =>
        string.Equals(ProducerId, other.ProducerId, StringComparison.Ordinal)
        && string.Equals(OperationId, other.OperationId, StringComparison.Ordinal)
        && CommitRevision == other.CommitRevision
        && LocalResultIndex == other.LocalResultIndex;
    public override bool Equals(object obj) =>
        obj is WastePolicyOutcomeKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(
        ProducerId,
        OperationId,
        CommitRevision,
        LocalResultIndex);

    private static bool IsStableId(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (!((current >= 'a' && current <= 'z')
                || (current >= '0' && current <= '9')
                || current is '.' or '_' or ':' or '-'))
                return false;
        }
        return true;
    }
}

public readonly struct WastePolicyOutcomeAttachment
{
    public WastePolicyOutcomeAttachment(
        WastePolicyOutcomeKey resultKey,
        string outcomeRunId,
        long outcomeSequence,
        int replayState,
        bool acknowledgementProven,
        string canonicalPayloadHash)
    {
        ResultKey = resultKey;
        OutcomeRunId = outcomeRunId?.Trim() ?? string.Empty;
        OutcomeSequence = outcomeSequence;
        ReplayState = replayState;
        AcknowledgementProven = acknowledgementProven;
        CanonicalPayloadHash = canonicalPayloadHash?.Trim() ?? string.Empty;
    }

    public WastePolicyOutcomeKey ResultKey { get; }
    public string OutcomeRunId { get; }
    public long OutcomeSequence { get; }
    public int ReplayState { get; }
    public bool AcknowledgementProven { get; }
    public string CanonicalPayloadHash { get; }
    public bool IsValid => ResultKey.IsValid
        && IsStableId(OutcomeRunId)
        && OutcomeSequence >= 0L
        && ReplayState >= 3
        && ReplayState <= 8
        && IsLowercaseSha256(CanonicalPayloadHash);

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

    private static bool IsStableId(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (!((current >= 'a' && current <= 'z')
                || (current >= '0' && current <= '9')
                || current is '.' or '_' or ':' or '-'))
                return false;
        }
        return true;
    }
}

public readonly struct WastePolicyGameplayOutcomePreview
{
    public WastePolicyGameplayOutcomePreview(
        string operationId,
        long ownerRevision,
        WasteOriginKind origin,
        WasteDispositionKind beforeDisposition,
        bool beforeEnabled,
        float beforeMaximumFeedContamination,
        WasteDispositionKind afterDisposition,
        bool afterEnabled,
        float afterMaximumFeedContamination,
        string displayName)
    {
        OperationId = operationId?.Trim() ?? string.Empty;
        OwnerRevision = ownerRevision;
        Origin = origin;
        BeforeDisposition = beforeDisposition;
        BeforeEnabled = beforeEnabled;
        BeforeMaximumFeedContamination = beforeMaximumFeedContamination;
        AfterDisposition = afterDisposition;
        AfterEnabled = afterEnabled;
        AfterMaximumFeedContamination = afterMaximumFeedContamination;
        DisplayName = displayName?.Trim() ?? string.Empty;
    }

    public string OperationId { get; }
    public long OwnerRevision { get; }
    public WasteOriginKind Origin { get; }
    public WasteDispositionKind BeforeDisposition { get; }
    public bool BeforeEnabled { get; }
    public float BeforeMaximumFeedContamination { get; }
    public WasteDispositionKind AfterDisposition { get; }
    public bool AfterEnabled { get; }
    public float AfterMaximumFeedContamination { get; }
    public string DisplayName { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(OperationId)
        && string.Equals(OperationId, OperationId.Trim(), StringComparison.Ordinal)
        && OwnerRevision > 0L
        && Origin is >= WasteOriginKind.Plant and <= WasteOriginKind.Forbidden
        && Enum.IsDefined(typeof(WasteDispositionKind), BeforeDisposition)
        && Enum.IsDefined(typeof(WasteDispositionKind), AfterDisposition)
        && float.IsFinite(BeforeMaximumFeedContamination)
        && float.IsFinite(AfterMaximumFeedContamination)
        && BeforeMaximumFeedContamination is >= 0f and <= 100f
        && AfterMaximumFeedContamination is >= 0f and <= 100f
        && !string.IsNullOrWhiteSpace(DisplayName)
        && string.Equals(DisplayName, DisplayName.Trim(), StringComparison.Ordinal);
}

public interface IPreparedWastePolicyGameplayOutcome
{
    WastePolicyOutcomeKey ResultKey { get; }
    bool IsCanonicalReplay { get; }
    bool TryCommit(
        long expectedOwnerRevision,
        out WastePolicyOutcomeAttachment attachment,
        out bool canonicalCommitted,
        out string failureReason);
    void Cancel();
}

public interface IWastePolicyGameplayOutcomeParticipant
{
    bool TryPrepare(
        in WastePolicyGameplayOutcomePreview preview,
        out IPreparedWastePolicyGameplayOutcome prepared,
        out string failureReason);
}

public readonly struct WasteFeedRequestResult
{
    public WasteFeedRequestResult(
        bool succeeded,
        string itemId,
        WasteFeedOutcomeCode outcome,
        DomainFailure failure)
    {
        Succeeded = succeeded;
        ItemId = itemId ?? string.Empty;
        Outcome = outcome;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public string ItemId { get; }
    public WasteFeedOutcomeCode Outcome { get; }
    public DomainFailure Failure { get; }
}

public sealed class WasteProcessingOverview
{
    public int PlantWaste { get; set; }
    public int AnimalWaste { get; set; }
    public int MixedWaste { get; set; }
    public int ForbiddenWaste { get; set; }
    public int ToxicWaste { get; set; }
    public int ProcessingBills { get; set; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonWasteProcessingSaveData
{
    public const int CurrentVersion = 3;

    public int version = CurrentVersion;
    public List<WastePolicyData> policies = new List<WastePolicyData>();
    public long nextOutcomeSequence = 1L;
    public WastePolicyOutcomeCommitSaveData pendingPolicyOutcome;
}

public enum WastePolicyOutcomeCommitPhase
{
    None = 0,
    DomainCommitted = 1,
    CanonicalOutcomeCommitted = 2,
    PublishedAcknowledged = 3
}

[Serializable]
public sealed class WastePolicyGameplayOutcomeAttachmentSaveData
{
    public string producerId = string.Empty;
    public string operationId = string.Empty;
    public long commitRevision;
    public int localResultIndex;
    public string outcomeRunId = string.Empty;
    public long outcomeSequence;
    public int replayState;
    public string canonicalPayloadHash = string.Empty;

    public WastePolicyGameplayOutcomeAttachmentSaveData Clone() => new()
    {
        producerId = producerId ?? string.Empty,
        operationId = operationId ?? string.Empty,
        commitRevision = commitRevision,
        localResultIndex = localResultIndex,
        outcomeRunId = outcomeRunId ?? string.Empty,
        outcomeSequence = outcomeSequence,
        replayState = replayState,
        canonicalPayloadHash = canonicalPayloadHash ?? string.Empty
    };
}

[Serializable]
public sealed class WastePolicyOutcomeCommitSaveData
{
    public int phase;
    public string operationId = string.Empty;
    public long ownerRevision;
    public WasteOriginKind origin;
    public WasteDispositionKind beforeDisposition;
    public bool beforeEnabled;
    public float beforeMaximumFeedContamination;
    public WasteDispositionKind afterDisposition;
    public bool afterEnabled;
    public float afterMaximumFeedContamination;
    public string displayName = string.Empty;
    public string expectedProducerId = string.Empty;
    public string expectedOperationId = string.Empty;
    public long expectedCommitRevision;
    public int expectedLocalResultIndex;
    public WastePolicyGameplayOutcomeAttachmentSaveData gameplayOutcome;

    public WastePolicyOutcomeCommitSaveData Clone() => new()
    {
        phase = phase,
        operationId = operationId ?? string.Empty,
        ownerRevision = ownerRevision,
        origin = origin,
        beforeDisposition = beforeDisposition,
        beforeEnabled = beforeEnabled,
        beforeMaximumFeedContamination = beforeMaximumFeedContamination,
        afterDisposition = afterDisposition,
        afterEnabled = afterEnabled,
        afterMaximumFeedContamination = afterMaximumFeedContamination,
        displayName = displayName ?? string.Empty,
        expectedProducerId = expectedProducerId ?? string.Empty,
        expectedOperationId = expectedOperationId ?? string.Empty,
        expectedCommitRevision = expectedCommitRevision,
        expectedLocalResultIndex = expectedLocalResultIndex,
        gameplayOutcome = gameplayOutcome?.Clone()
    };
}

public interface IWasteProcessingQuery
{
    int Version { get; }
    IReadOnlyList<WastePolicyData> Policies { get; }
    WastePolicyData GetPolicy(WasteOriginKind origin);
    WasteProcessingOverview CaptureOverview();
}

public interface IWastePolicyCommand
{
    WastePolicyCommandResult SetPolicy(WastePolicyData policy);
}

public interface IWasteFeedCommand
{
    WasteFeedRequestResult RequestDirectFeed(
        WildlifeDietType diet,
        Vector2Int destinationPosition,
        string destinationId);
}

public interface IWasteFeedCandidateQuery
{
    bool TryGetDirectFeedCandidate(
        WildlifeDietType diet,
        string destinationId,
        out WasteDirectFeedCandidate candidate,
        out DomainFailure failure);
}

public sealed class WasteProcessingRestoreCandidate
{
    internal WasteProcessingRestoreCandidate(WasteProcessingAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal WasteProcessingAggregateState State { get; }
}

public interface IWasteProcessingPersistence
{
    DungeonWasteProcessingSaveData Capture();
    WasteProcessingRestoreCandidate BuildRestore(
        DungeonWasteProcessingSaveData saveData);
    void Restore(WasteProcessingRestoreCandidate candidate);
}
