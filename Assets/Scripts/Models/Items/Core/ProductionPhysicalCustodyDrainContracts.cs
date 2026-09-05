using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public enum ProductionPhysicalCustodyDrainPhase
{
    Prepared = 0,
    ReleasingActors = 1,
    ReleasingIntents = 2,
    ReleasingDestination = 3,
    EffectCommittedAwaitingOwnerAck = 4,
    OwnerAcknowledgedAwaitingCheckpointGc = 5
}

public enum ProductionPhysicalCustodyDrainStatus
{
    Applied = 0,
    Replay = 1,
    Deferred = 2,
    Conflict = 3
}

public sealed class ProductionPhysicalCustodyDrainRequest
{
    public ProductionPhysicalCustodyDrainRequest(
        string stepOperationId,
        string ownerStableId,
        string sourceDestinationId,
        int ownerGridX,
        int ownerGridY,
        string requestFingerprint,
        string sourceOwnershipFingerprint,
        IEnumerable<string> sourceStackIds,
        IEnumerable<string> sourceActorIds,
        IEnumerable<string> sourceHaulIntentOperationIds,
        int inputQuantity,
        long inputMassGrams)
    {
        StepOperationId = stepOperationId ?? string.Empty;
        OwnerStableId = ownerStableId ?? string.Empty;
        SourceDestinationId = sourceDestinationId ?? string.Empty;
        OwnerGridX = ownerGridX;
        OwnerGridY = ownerGridY;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        SourceOwnershipFingerprint = sourceOwnershipFingerprint
            ?? string.Empty;
        SourceStackIds = Array.AsReadOnly((sourceStackIds
                ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
        SourceHaulIntentOperationIds = Array.AsReadOnly((
                sourceHaulIntentOperationIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
        SourceActorIds = Array.AsReadOnly((sourceActorIds
                ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
        InputQuantity = inputQuantity;
        InputMassGrams = inputMassGrams;
    }

    public string StepOperationId { get; }
    public string OwnerStableId { get; }
    public string SourceDestinationId { get; }
    public int OwnerGridX { get; }
    public int OwnerGridY { get; }
    public string RequestFingerprint { get; }
    public string SourceOwnershipFingerprint { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public IReadOnlyList<string> SourceActorIds { get; }
    public IReadOnlyList<string> SourceHaulIntentOperationIds { get; }
    public int InputQuantity { get; }
    public long InputMassGrams { get; }
}

/// <summary>
/// Canonical request identity for the Items-owned physical drain. The digest
/// binds every immutable input so an arbitrary 64-character token cannot be
/// substituted for the actual source plan.
/// </summary>
public static class ProductionPhysicalCustodyDrainFingerprint
{
    public static string CreateRequest(
        string stepOperationId,
        string ownerStableId,
        string sourceDestinationId,
        int ownerGridX,
        int ownerGridY,
        string sourceOwnershipFingerprint,
        IEnumerable<string> sourceStackIds,
        IEnumerable<string> sourceActorIds,
        IEnumerable<string> sourceHaulIntentOperationIds,
        int inputQuantity,
        long inputMassGrams)
    {
        string[] stacks = Canonical(sourceStackIds);
        string[] actors = Canonical(sourceActorIds);
        string[] intents = Canonical(sourceHaulIntentOperationIds);
        StringBuilder canonical = new StringBuilder(256)
            .Append("production-physical-custody-drain-request@1|")
            .Append(stepOperationId ?? string.Empty).Append('|')
            .Append(ownerStableId ?? string.Empty).Append('|')
            .Append(sourceDestinationId ?? string.Empty).Append('|')
            .Append(ownerGridX.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(ownerGridY.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(sourceOwnershipFingerprint ?? string.Empty).Append('|')
            .Append(inputQuantity.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(inputMassGrams.ToString(CultureInfo.InvariantCulture))
            .Append('|');
        Append(canonical, stacks);
        Append(canonical, actors);
        Append(canonical, intents);
        return Hash(canonical.ToString());
    }

    private static string[] Canonical(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>())
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static void Append(StringBuilder target, IEnumerable<string> values)
    {
        foreach (string value in values ?? Array.Empty<string>())
        {
            string token = value ?? string.Empty;
            target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(token).Append(';');
        }
        target.Append('|');
    }

    private static string Hash(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        StringBuilder result = new StringBuilder(digest.Length * 2);
        foreach (byte current in digest)
        {
            result.Append(current.ToString(
                "x2",
                CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }
}

public readonly struct ProductionPhysicalCustodyDrainResult
{
    public ProductionPhysicalCustodyDrainResult(
        ProductionPhysicalCustodyDrainStatus status,
        string commitId,
        string receiptFingerprint,
        string failureReason)
    {
        Status = status;
        CommitId = commitId ?? string.Empty;
        ReceiptFingerprint = receiptFingerprint ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionPhysicalCustodyDrainStatus Status { get; }
    public string CommitId { get; }
    public string ReceiptFingerprint { get; }
    public string FailureReason { get; }
}

public interface IProductionPhysicalCustodyDrainCheckpointGcCandidate
{
}

/// <summary>
/// Row-scoped checkpoint collector for acknowledged physical-custody
/// tombstones. Preparation is read-only; publish removes only the captured
/// rows and rollback restores those exact rows without changing gameplay
/// repository revisions.
/// </summary>
public interface IProductionPhysicalCustodyDrainCheckpointGcPort
{
    bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionPhysicalCustodyDrainSaveData> records,
        out IProductionPhysicalCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason);

    bool TryPublishCheckpointGarbageCollection(
        IProductionPhysicalCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason);

    void RollbackCheckpointGarbageCollection(
        IProductionPhysicalCustodyDrainCheckpointGcCandidate candidate);

    void CompleteCheckpointGarbageCollection(
        IProductionPhysicalCustodyDrainCheckpointGcCandidate candidate);
}

public interface IProductionPhysicalCustodyDrainOutbox
{
    ProductionPhysicalCustodyDrainResult TryPrepare(
        ProductionPhysicalCustodyDrainRequest request);
    ProductionPhysicalCustodyDrainResult TryBeginDraining(
        string stepOperationId,
        string requestFingerprint);
    ProductionPhysicalCustodyDrainResult TryBeginReleasingIntents(
        string stepOperationId);
    ProductionPhysicalCustodyDrainResult TryBeginReleasingDestination(
        string stepOperationId);
    ProductionPhysicalCustodyDrainResult TryRecordActorCompleted(
        string stepOperationId,
        string actorId);
    ProductionPhysicalCustodyDrainResult TryRecordHaulIntentReleased(
        string stepOperationId,
        string haulIntentOperationId);
    ProductionPhysicalCustodyDrainResult TryCommitEffect(
        string stepOperationId,
        IEnumerable<string> releasedStackIds,
        int releasedQuantity,
        long releasedMassGrams,
        string resultFingerprint);
    ProductionPhysicalCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint);
    ProductionPhysicalCustodyDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint);
    bool TryCapture(
        string stepOperationId,
        out ProductionPhysicalCustodyDrainSaveData record);
}

/// <summary>
/// Live Items boundary used by the production destructive-drain participant.
/// Capture is mutation-free. Prepare persists the immutable source vector;
/// commit resumes actor, intent, and destination effects from that vector.
/// </summary>
public interface IProductionPhysicalCustodyDrainPort
{
    [GameplayInternalOnly(
        "Captures the immutable Items source vector for one journal-owned production destination drain.",
        "Production physical-custody destructive-drain participant only")]
    bool TryCaptureRequest(
        string stepOperationId,
        string ownerStableId,
        string sourceDestinationId,
        int ownerGridX,
        int ownerGridY,
        string expectedSourceOwnershipFingerprint,
        out ProductionPhysicalCustodyDrainRequest request,
        out string failureReason);

    [GameplayInternalOnly(
        "Persists the immutable Items source vector only after the upper journal entry exists.",
        "Production physical-custody destructive-drain participant only")]
    ProductionPhysicalCustodyDrainResult TryPrepare(
        ProductionPhysicalCustodyDrainRequest request);

    [GameplayInternalOnly(
        "Advances exactly one replay-safe actor, intent, or destination effect step.",
        "Production physical-custody destructive-drain participant only")]
    ProductionPhysicalCustodyDrainResult TryCommit(
        string stepOperationId,
        string requestFingerprint);

    [GameplayInternalOnly(
        "Acknowledges the Items receipt only after the upper journal durably records it.",
        "Production physical-custody destructive-drain participant only")]
    ProductionPhysicalCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint);

    [GameplayInternalOnly(
        "Deletes terminal Items evidence only from the ordered durable-save checkpoint callback.",
        "Production physical-custody destructive-drain checkpoint GC only")]
    ProductionPhysicalCustodyDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint);

    bool TryCapture(
        string stepOperationId,
        out ProductionPhysicalCustodyDrainSaveData record);
}

/// <summary>
/// Durable Items-side evidence for one facility-output destination drain.
/// The immutable source vectors describe the effect that may be resumed; the
/// progress vectors only move forward and are never inferred from collection
/// enumeration order.
/// </summary>
[Serializable]
public sealed class ProductionPhysicalCustodyDrainSaveData
{
    public string stepOperationId = string.Empty;
    public string ownerStableId = string.Empty;
    public string sourceDestinationId = string.Empty;
    public int ownerGridX;
    public int ownerGridY;
    public string requestFingerprint = string.Empty;
    public string sourceOwnershipFingerprint = string.Empty;
    public ProductionPhysicalCustodyDrainPhase phase;
    public List<string> sourceStackIds = new();
    public List<string> sourceActorIds = new();
    public List<string> sourceHaulIntentOperationIds = new();
    public List<string> completedActorIds = new();
    public List<string> releasedHaulIntentOperationIds = new();
    public List<string> releasedStackIds = new();
    public int inputQuantity;
    public long inputMassGrams;
    public int releasedQuantity;
    public long releasedMassGrams;
    public string resultFingerprint = string.Empty;
    public string commitId = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionPhysicalCustodyDrainSaveData Clone() => new()
    {
        stepOperationId = stepOperationId,
        ownerStableId = ownerStableId,
        sourceDestinationId = sourceDestinationId,
        ownerGridX = ownerGridX,
        ownerGridY = ownerGridY,
        requestFingerprint = requestFingerprint,
        sourceOwnershipFingerprint = sourceOwnershipFingerprint,
        phase = phase,
        sourceStackIds = Clone(sourceStackIds),
        sourceActorIds = Clone(sourceActorIds),
        sourceHaulIntentOperationIds = Clone(sourceHaulIntentOperationIds),
        completedActorIds = Clone(completedActorIds),
        releasedHaulIntentOperationIds = Clone(
            releasedHaulIntentOperationIds),
        releasedStackIds = Clone(releasedStackIds),
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        releasedQuantity = releasedQuantity,
        releasedMassGrams = releasedMassGrams,
        resultFingerprint = resultFingerprint,
        commitId = commitId,
        receiptFingerprint = receiptFingerprint
    };

    private static List<string> Clone(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>()).ToList();
}
