using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public enum ProductionInputDestinationCustodyDrainPhase
{
    Prepared = 0,
    ReleasingActors = 1,
    ReleasingOperationAuthority = 2,
    ReleasingDestination = 3,
    EffectCommittedAwaitingBillAck = 4,
    BillAcknowledgedAwaitingCheckpointGc = 5
}

public enum ProductionInputDestinationCustodyDrainStatus
{
    Applied = 0,
    Replay = 1,
    Deferred = 2,
    Conflict = 3
}

[Serializable]
public sealed class ProductionInputDestinationDrainStackSaveData
{
    public string stackId = string.Empty;
    public string itemId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string componentFingerprint = string.Empty;
    public int quantity;
    public long massGrams;
    public WorldItemStackState state;
    public int positionX;
    public int positionY;
    public string sourceStorageDestinationId = string.Empty;
    public int destinationPositionX;
    public int destinationPositionY;
    public long reservationRevision;

    public ProductionInputDestinationDrainStackSaveData Clone() => new()
    {
        stackId = stackId,
        itemId = itemId,
        itemInstanceId = itemInstanceId,
        componentFingerprint = componentFingerprint,
        quantity = quantity,
        massGrams = massGrams,
        state = state,
        positionX = positionX,
        positionY = positionY,
        sourceStorageDestinationId = sourceStorageDestinationId,
        destinationPositionX = destinationPositionX,
        destinationPositionY = destinationPositionY,
        reservationRevision = reservationRevision
    };
}

[Serializable]
public sealed class ProductionInputDestinationDrainOperationSaveData
{
    public string operationId = string.Empty;
    public string actorId = string.Empty;
    public bool hadCommittedPickup;
    public string operationFingerprint = string.Empty;
    // Stable authority fingerprints, not transient ItemQuantityLease.leaseId
    // values. Runtime lease IDs are regenerated during restore.
    public List<string> leaseAuthorityFingerprints = new();
    public List<string> carriedStackIds = new();

    public ProductionInputDestinationDrainOperationSaveData Clone() => new()
    {
        operationId = operationId,
        actorId = actorId,
        hadCommittedPickup = hadCommittedPickup,
        operationFingerprint = operationFingerprint,
        leaseAuthorityFingerprints = Clone(leaseAuthorityFingerprints),
        carriedStackIds = Clone(carriedStackIds)
    };

    private static List<string> Clone(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>()).ToList();
}

[Serializable]
public sealed class ProductionInputDestinationDrainActorSaveData
{
    public string actorId = string.Empty;
    public string sourcePhysicalFingerprint = string.Empty;
    public List<string> allowedOperationIds = new();

    public ProductionInputDestinationDrainActorSaveData Clone() => new()
    {
        actorId = actorId,
        sourcePhysicalFingerprint = sourcePhysicalFingerprint,
        allowedOperationIds = (allowedOperationIds ?? new List<string>()).ToList()
    };
}

/// <summary>
/// Immutable, one-read closure of every physical stack, haul operation and actor
/// currently owned by one production input destination. The snapshot is captured
/// once and is then used both for terminal-source mass accounting and for the
/// child custody-drain request, preventing a prepare-time TOCTOU split.
/// </summary>
public sealed class ProductionInputDestinationCustodySourceSnapshot
{
    public ProductionInputDestinationCustodySourceSnapshot(
        string sourceDestinationId,
        long massAuthorityRevision,
        string sourceOwnershipFingerprint,
        IEnumerable<ProductionInputDestinationDrainStackSaveData> sourceStacks,
        IEnumerable<ProductionInputDestinationDrainOperationSaveData>
            sourceOperations,
        IEnumerable<ProductionInputDestinationDrainActorSaveData> sourceActors,
        int inputQuantity,
        long inputMassGrams)
    {
        SourceDestinationId = sourceDestinationId ?? string.Empty;
        MassAuthorityRevision = massAuthorityRevision;
        SourceOwnershipFingerprint = sourceOwnershipFingerprint ?? string.Empty;
        SourceStacks = Array.AsReadOnly((sourceStacks
                ?? Array.Empty<ProductionInputDestinationDrainStackSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stackId, StringComparer.Ordinal)
            .ToArray());
        SourceOperations = Array.AsReadOnly((sourceOperations
                ?? Array.Empty<ProductionInputDestinationDrainOperationSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.operationId, StringComparer.Ordinal)
            .ToArray());
        SourceActors = Array.AsReadOnly((sourceActors
                ?? Array.Empty<ProductionInputDestinationDrainActorSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.actorId, StringComparer.Ordinal)
            .ToArray());
        InputQuantity = inputQuantity;
        InputMassGrams = inputMassGrams;
    }

    public string SourceDestinationId { get; }
    public long MassAuthorityRevision { get; }
    public string SourceOwnershipFingerprint { get; }
    public IReadOnlyList<ProductionInputDestinationDrainStackSaveData> SourceStacks
    { get; }
    public IReadOnlyList<ProductionInputDestinationDrainOperationSaveData>
        SourceOperations { get; }
    public IReadOnlyList<ProductionInputDestinationDrainActorSaveData> SourceActors
    { get; }
    public int InputQuantity { get; }
    public long InputMassGrams { get; }
}

public sealed class ProductionInputDestinationCustodyDrainRequest
{
    public ProductionInputDestinationCustodyDrainRequest(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        string billId,
        string facilityId,
        string sourceDestinationId,
        int ownerGridX,
        int ownerGridY,
        string sourceClaimFingerprint,
        string sourceOwnershipFingerprint,
        IEnumerable<ProductionInputDestinationDrainStackSaveData> sourceStacks,
        IEnumerable<ProductionInputDestinationDrainOperationSaveData>
            sourceOperations,
        IEnumerable<ProductionInputDestinationDrainActorSaveData> sourceActors,
        int inputQuantity,
        long inputMassGrams,
        string requestFingerprint)
    {
        ParentOperationId = parentOperationId ?? string.Empty;
        StepOperationId = stepOperationId ?? string.Empty;
        OwnerStableId = ownerStableId ?? string.Empty;
        BillId = billId ?? string.Empty;
        FacilityId = facilityId ?? string.Empty;
        SourceDestinationId = sourceDestinationId ?? string.Empty;
        OwnerGridX = ownerGridX;
        OwnerGridY = ownerGridY;
        SourceClaimFingerprint = sourceClaimFingerprint ?? string.Empty;
        SourceOwnershipFingerprint = sourceOwnershipFingerprint ?? string.Empty;
        SourceStacks = Array.AsReadOnly((sourceStacks
                ?? Array.Empty<ProductionInputDestinationDrainStackSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stackId, StringComparer.Ordinal)
            .ToArray());
        SourceOperations = Array.AsReadOnly((sourceOperations
                ?? Array.Empty<ProductionInputDestinationDrainOperationSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.operationId, StringComparer.Ordinal)
            .ToArray());
        SourceActors = Array.AsReadOnly((sourceActors
                ?? Array.Empty<ProductionInputDestinationDrainActorSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.actorId, StringComparer.Ordinal)
            .ToArray());
        InputQuantity = inputQuantity;
        InputMassGrams = inputMassGrams;
        RequestFingerprint = requestFingerprint ?? string.Empty;
    }

    public string ParentOperationId { get; }
    public string StepOperationId { get; }
    public string OwnerStableId { get; }
    public string BillId { get; }
    public string FacilityId { get; }
    public string SourceDestinationId { get; }
    public int OwnerGridX { get; }
    public int OwnerGridY { get; }
    public string SourceClaimFingerprint { get; }
    public string SourceOwnershipFingerprint { get; }
    public IReadOnlyList<ProductionInputDestinationDrainStackSaveData> SourceStacks
    { get; }
    public IReadOnlyList<ProductionInputDestinationDrainOperationSaveData>
        SourceOperations { get; }
    public IReadOnlyList<ProductionInputDestinationDrainActorSaveData> SourceActors
    { get; }
    public int InputQuantity { get; }
    public long InputMassGrams { get; }
    public string RequestFingerprint { get; }
}

public static class ProductionInputDestinationCustodyDrainFingerprint
{
    public const string CommitPrefix =
        "production-input-destination-custody-drain-commit:";

    public static string CreateRequest(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        string billId,
        string facilityId,
        string sourceDestinationId,
        int ownerGridX,
        int ownerGridY,
        string sourceClaimFingerprint,
        string sourceOwnershipFingerprint,
        IEnumerable<ProductionInputDestinationDrainStackSaveData> sourceStacks,
        IEnumerable<ProductionInputDestinationDrainOperationSaveData>
            sourceOperations,
        IEnumerable<ProductionInputDestinationDrainActorSaveData> sourceActors,
        int inputQuantity,
        long inputMassGrams)
    {
        StringBuilder canonical = new StringBuilder(512)
            .Append("production-input-destination-custody-drain-request@1|");
        Token(canonical, parentOperationId);
        Token(canonical, stepOperationId);
        Token(canonical, ownerStableId);
        Token(canonical, billId);
        Token(canonical, facilityId);
        Token(canonical, sourceDestinationId);
        canonical.Append(ownerGridX.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(ownerGridY.ToString(CultureInfo.InvariantCulture)).Append('|');
        Token(canonical, sourceClaimFingerprint);
        Token(canonical, sourceOwnershipFingerprint);
        canonical.Append(inputQuantity.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(inputMassGrams.ToString(CultureInfo.InvariantCulture))
            .Append('|');

        foreach (ProductionInputDestinationDrainStackSaveData value in
                 (sourceStacks ?? Array.Empty<
                     ProductionInputDestinationDrainStackSaveData>())
                 .OrderBy(value => value?.stackId, StringComparer.Ordinal))
        {
            Token(canonical, value?.stackId);
            Token(canonical, value?.itemId);
            Token(canonical, value?.itemInstanceId);
            Token(canonical, value?.componentFingerprint);
            canonical.Append(value?.quantity ?? -1).Append('|')
                .Append(value?.massGrams ?? -1L).Append('|')
                .Append(value == null ? -1 : (int)value.state).Append('|')
                .Append(value?.positionX ?? 0).Append('|')
                .Append(value?.positionY ?? 0).Append('|');
            Token(canonical, value?.sourceStorageDestinationId);
            canonical.Append(value?.destinationPositionX ?? 0).Append('|')
                .Append(value?.destinationPositionY ?? 0).Append('|')
                .Append(value?.reservationRevision ?? -1L).Append('|');
        }
        canonical.Append("ops|");
        foreach (ProductionInputDestinationDrainOperationSaveData value in
                 (sourceOperations ?? Array.Empty<
                     ProductionInputDestinationDrainOperationSaveData>())
                 .OrderBy(value => value?.operationId, StringComparer.Ordinal))
        {
            Token(canonical, value?.operationId);
            Token(canonical, value?.actorId);
            canonical.Append(value?.hadCommittedPickup == true ? '1' : '0')
                .Append('|');
            Token(canonical, value?.operationFingerprint);
            Tokens(canonical, value?.leaseAuthorityFingerprints);
            Tokens(canonical, value?.carriedStackIds);
        }
        canonical.Append("actors|");
        foreach (ProductionInputDestinationDrainActorSaveData value in
                 (sourceActors ?? Array.Empty<
                     ProductionInputDestinationDrainActorSaveData>())
                 .OrderBy(value => value?.actorId, StringComparer.Ordinal))
        {
            Token(canonical, value?.actorId);
            Token(canonical, value?.sourcePhysicalFingerprint);
            Tokens(canonical, value?.allowedOperationIds);
        }
        return Hash(canonical.ToString());
    }

    public static string CreateCommit(string stepOperationId, string requestFingerprint) =>
        CommitPrefix + Hash((stepOperationId ?? string.Empty) + "\n"
            + (requestFingerprint ?? string.Empty));

    public static string CreateReceipt(
        string requestFingerprint,
        string resultFingerprint,
        int releasedQuantity,
        long releasedMassGrams,
        IEnumerable<string> releasedStackIds,
        IEnumerable<string> releasedOperationIds)
    {
        StringBuilder canonical = new StringBuilder(256)
            .Append("production-input-destination-custody-drain-receipt@1|")
            .Append(requestFingerprint ?? string.Empty).Append('|')
            .Append(resultFingerprint ?? string.Empty).Append('|')
            .Append(releasedQuantity.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(releasedMassGrams.ToString(CultureInfo.InvariantCulture))
            .Append('|');
        Tokens(canonical, releasedStackIds);
        Tokens(canonical, releasedOperationIds);
        return Hash(canonical.ToString());
    }

    public static string Hash(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(value ?? string.Empty));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte current in digest)
            result.Append(current.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static void Tokens(StringBuilder target, IEnumerable<string> values)
    {
        foreach (string value in (values ?? Array.Empty<string>())
                 .OrderBy(value => value, StringComparer.Ordinal))
            Token(target, value);
        target.Append('|');
    }

    private static void Token(StringBuilder target, string value)
    {
        string token = value ?? string.Empty;
        target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(token).Append('|');
    }
}

public readonly struct ProductionInputDestinationCustodyDrainResult
{
    public ProductionInputDestinationCustodyDrainResult(
        ProductionInputDestinationCustodyDrainStatus status,
        string commitId,
        string receiptFingerprint,
        string failureReason)
    {
        Status = status;
        CommitId = commitId ?? string.Empty;
        ReceiptFingerprint = receiptFingerprint ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionInputDestinationCustodyDrainStatus Status { get; }
    public string CommitId { get; }
    public string ReceiptFingerprint { get; }
    public string FailureReason { get; }
}

public interface IProductionInputDestinationCustodyDrainCheckpointGcCandidate
{
}

public interface IProductionInputDestinationCustodyDrainLiveQuery
{
    IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> CaptureAll();
}

/// <summary>
/// Row-scoped checkpoint collector for acknowledged input-destination child
/// tombstones. Preparation is read-only and rollback restores only rows that
/// the same candidate published.
/// </summary>
public interface IProductionInputDestinationCustodyDrainCheckpointGcPort
{
    bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> records,
        out IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason);

    bool TryPublishCheckpointGarbageCollection(
        IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason);

    void RollbackCheckpointGarbageCollection(
        IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate);

    void CompleteCheckpointGarbageCollection(
        IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate);
}

public interface IProductionInputDestinationCustodyDrainOutbox
{
    ProductionInputDestinationCustodyDrainResult TryPrepare(
        ProductionInputDestinationCustodyDrainRequest request);
    ProductionInputDestinationCustodyDrainResult TryBeginDraining(
        string stepOperationId,
        string requestFingerprint);
    ProductionInputDestinationCustodyDrainResult TryRecordActorCompleted(
        string stepOperationId,
        string actorId);
    ProductionInputDestinationCustodyDrainResult
        TryBeginReleasingOperationAuthority(string stepOperationId);
    ProductionInputDestinationCustodyDrainResult TryRecordOperationReleased(
        string stepOperationId,
        string operationId);
    ProductionInputDestinationCustodyDrainResult TryBeginReleasingDestination(
        string stepOperationId);
    ProductionInputDestinationCustodyDrainResult TryCommitEffect(
        string stepOperationId,
        IEnumerable<string> releasedStackIds,
        int releasedQuantity,
        long releasedMassGrams,
        string resultFingerprint);
    ProductionInputDestinationCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint);
    ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint);
    bool TryCapture(
        string stepOperationId,
        out ProductionInputDestinationCustodyDrainSaveData record);
}

[Serializable]
public sealed class ProductionInputDestinationCustodyDrainSaveData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string parentOperationId = string.Empty;
    public string stepOperationId = string.Empty;
    public string ownerStableId = string.Empty;
    public string billId = string.Empty;
    public string facilityId = string.Empty;
    public string sourceDestinationId = string.Empty;
    public int ownerGridX;
    public int ownerGridY;
    public string sourceClaimFingerprint = string.Empty;
    public string sourceOwnershipFingerprint = string.Empty;
    public string requestFingerprint = string.Empty;
    public ProductionInputDestinationCustodyDrainPhase phase;
    public List<ProductionInputDestinationDrainStackSaveData> sourceStacks = new();
    public List<ProductionInputDestinationDrainOperationSaveData> sourceOperations =
        new();
    public List<ProductionInputDestinationDrainActorSaveData> sourceActors = new();
    public List<string> completedActorIds = new();
    public List<string> releasedOperationIds = new();
    public List<string> releasedStackIds = new();
    public int inputQuantity;
    public long inputMassGrams;
    public int releasedQuantity;
    public long releasedMassGrams;
    public string resultFingerprint = string.Empty;
    public string commitId = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionInputDestinationCustodyDrainSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        parentOperationId = parentOperationId,
        stepOperationId = stepOperationId,
        ownerStableId = ownerStableId,
        billId = billId,
        facilityId = facilityId,
        sourceDestinationId = sourceDestinationId,
        ownerGridX = ownerGridX,
        ownerGridY = ownerGridY,
        sourceClaimFingerprint = sourceClaimFingerprint,
        sourceOwnershipFingerprint = sourceOwnershipFingerprint,
        requestFingerprint = requestFingerprint,
        phase = phase,
        sourceStacks = (sourceStacks ?? new()).Select(value => value?.Clone()).ToList(),
        sourceOperations = (sourceOperations ?? new()).Select(value => value?.Clone())
            .ToList(),
        sourceActors = (sourceActors ?? new()).Select(value => value?.Clone()).ToList(),
        completedActorIds = Clone(completedActorIds),
        releasedOperationIds = Clone(releasedOperationIds),
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

public static class ProductionInputDestinationCustodyDrainContract
{
    public static bool IsValidSourceSnapshot(
        ProductionInputDestinationCustodySourceSnapshot snapshot)
    {
        if (snapshot == null
            || snapshot.MassAuthorityRevision < 0L
            || !Token(snapshot.SourceDestinationId)
            || !Digest(snapshot.SourceOwnershipFingerprint))
        {
            return false;
        }
        const string token = "source-snapshot-validation";
        const string digest =
            "0000000000000000000000000000000000000000000000000000000000000000";
        string requestFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                token,
                token,
                token,
                token,
                token,
                snapshot.SourceDestinationId,
                0,
                0,
                digest,
                snapshot.SourceOwnershipFingerprint,
                snapshot.SourceStacks,
                snapshot.SourceOperations,
                snapshot.SourceActors,
                snapshot.InputQuantity,
                snapshot.InputMassGrams);
        ProductionInputDestinationCustodyDrainRequest request = new(
            token,
            token,
            token,
            token,
            token,
            snapshot.SourceDestinationId,
            0,
            0,
            digest,
            snapshot.SourceOwnershipFingerprint,
            snapshot.SourceStacks,
            snapshot.SourceOperations,
            snapshot.SourceActors,
            snapshot.InputQuantity,
            snapshot.InputMassGrams,
            requestFingerprint);
        return IsValidRequest(request);
    }

    public static bool IsValidRequest(
        ProductionInputDestinationCustodyDrainRequest request)
    {
        if (request == null)
            return false;
        ProductionInputDestinationCustodyDrainSaveData prepared = new()
        {
            schemaVersion =
                ProductionInputDestinationCustodyDrainSaveData
                    .CurrentSchemaVersion,
            parentOperationId = request.ParentOperationId,
            stepOperationId = request.StepOperationId,
            ownerStableId = request.OwnerStableId,
            billId = request.BillId,
            facilityId = request.FacilityId,
            sourceDestinationId = request.SourceDestinationId,
            ownerGridX = request.OwnerGridX,
            ownerGridY = request.OwnerGridY,
            sourceClaimFingerprint = request.SourceClaimFingerprint,
            sourceOwnershipFingerprint = request.SourceOwnershipFingerprint,
            requestFingerprint = request.RequestFingerprint,
            phase = ProductionInputDestinationCustodyDrainPhase.Prepared,
            sourceStacks = request.SourceStacks.Select(value => value?.Clone())
                .ToList(),
            sourceOperations = request.SourceOperations
                .Select(value => value?.Clone()).ToList(),
            sourceActors = request.SourceActors.Select(value => value?.Clone())
                .ToList(),
            completedActorIds = new List<string>(),
            releasedOperationIds = new List<string>(),
            releasedStackIds = new List<string>(),
            inputQuantity = request.InputQuantity,
            inputMassGrams = request.InputMassGrams
        };
        return IsValidSave(prepared);
    }

    public static bool IsValidSave(
        ProductionInputDestinationCustodyDrainSaveData value)
    {
        if (value == null
            || value.schemaVersion !=
                ProductionInputDestinationCustodyDrainSaveData.CurrentSchemaVersion
            || !Token(value.parentOperationId)
            || !Token(value.stepOperationId)
            || !Token(value.ownerStableId)
            || !Token(value.billId)
            || !Token(value.facilityId)
            || !Token(value.sourceDestinationId)
            || !Digest(value.sourceClaimFingerprint)
            || !Digest(value.sourceOwnershipFingerprint)
            || !Digest(value.requestFingerprint)
            || !Enum.IsDefined(
                typeof(ProductionInputDestinationCustodyDrainPhase),
                value.phase)
            || value.sourceStacks == null
            || value.sourceOperations == null
            || value.sourceActors == null
            || !Rows(value.sourceStacks, row => row?.stackId)
            || !Rows(value.sourceOperations, row => row?.operationId)
            || !Rows(value.sourceActors, row => row?.actorId)
            || !Strings(value.completedActorIds)
            || !Strings(value.releasedOperationIds)
            || !Strings(value.releasedStackIds)
            || value.inputQuantity < 0
            || value.inputMassGrams < 0L
            || !TrySumStacks(
                value.sourceStacks,
                out int sourceQuantity,
                out long sourceMassGrams)
            || sourceQuantity != value.inputQuantity
            || sourceMassGrams != value.inputMassGrams)
        {
            return false;
        }

        if (value.sourceStacks.Any(row => row == null
                || !Token(row.stackId)
                || !Token(row.itemId)
                || !OptionalToken(row.itemInstanceId)
                || !Digest(row.componentFingerprint)
                || row.quantity <= 0
                || row.massGrams <= 0L
                || !Enum.IsDefined(typeof(WorldItemStackState), row.state)
                || row.reservationRevision < 0L)
            || value.sourceOperations.Any(row => row == null
                || !Token(row.operationId)
                || row.hadCommittedPickup && !Token(row.actorId)
                || !Digest(row.operationFingerprint)
                || !Digests(row.leaseAuthorityFingerprints)
                || !Strings(row.carriedStackIds)
                || (row.hadCommittedPickup
                    ? row.carriedStackIds.Count == 0
                    : row.carriedStackIds.Count != 0
                        || row.leaseAuthorityFingerprints.Count == 0))
            || value.sourceActors.Any(row => row == null
                || !Token(row.actorId)
                || !Digest(row.sourcePhysicalFingerprint)
                || !Strings(row.allowedOperationIds)
                || row.allowedOperationIds.Count == 0))
        {
            return false;
        }

        string expectedRequest = ProductionInputDestinationCustodyDrainFingerprint
            .CreateRequest(
                value.parentOperationId,
                value.stepOperationId,
                value.ownerStableId,
                value.billId,
                value.facilityId,
                value.sourceDestinationId,
                value.ownerGridX,
                value.ownerGridY,
                value.sourceClaimFingerprint,
                value.sourceOwnershipFingerprint,
                value.sourceStacks,
                value.sourceOperations,
                value.sourceActors,
                value.inputQuantity,
                value.inputMassGrams);
        if (!string.Equals(expectedRequest, value.requestFingerprint,
                StringComparison.Ordinal))
            return false;

        string[] actors = value.sourceActors.Select(row => row.actorId).ToArray();
        string[] operations = value.sourceOperations.Select(row => row.operationId)
            .ToArray();
        string[] stacks = value.sourceStacks.Select(row => row.stackId).ToArray();
        bool actorComplete = value.completedActorIds.SequenceEqual(
            actors,
            StringComparer.Ordinal);
        bool operationComplete = value.releasedOperationIds.SequenceEqual(
            operations,
            StringComparer.Ordinal);
        bool terminal = value.phase is
            ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck
            or ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc;
        bool progress = value.phase switch
        {
            ProductionInputDestinationCustodyDrainPhase.Prepared =>
                value.completedActorIds.Count == 0
                && value.releasedOperationIds.Count == 0
                && value.releasedStackIds.Count == 0,
            ProductionInputDestinationCustodyDrainPhase.ReleasingActors =>
                Prefix(value.completedActorIds, actors)
                && value.releasedOperationIds.Count == 0
                && value.releasedStackIds.Count == 0,
            ProductionInputDestinationCustodyDrainPhase
                .ReleasingOperationAuthority => actorComplete
                && Prefix(value.releasedOperationIds, operations)
                && value.releasedStackIds.Count == 0,
            ProductionInputDestinationCustodyDrainPhase.ReleasingDestination =>
                actorComplete && operationComplete
                && value.releasedStackIds.Count == 0,
            ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck or
            ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc =>
                actorComplete && operationComplete
                && value.releasedStackIds.SequenceEqual(
                    stacks,
                    StringComparer.Ordinal),
            _ => false
        };
        if (!progress)
            return false;

        if (!terminal)
        {
            return value.releasedQuantity == 0
                && value.releasedMassGrams == 0L
                && string.IsNullOrEmpty(value.resultFingerprint)
                && string.IsNullOrEmpty(value.commitId)
                && string.IsNullOrEmpty(value.receiptFingerprint);
        }

        return value.releasedQuantity == value.inputQuantity
            && value.releasedMassGrams == value.inputMassGrams
            && Digest(value.resultFingerprint)
            && string.Equals(
                value.commitId,
                ProductionInputDestinationCustodyDrainFingerprint.CreateCommit(
                    value.stepOperationId,
                    value.requestFingerprint),
                StringComparison.Ordinal)
            && string.Equals(
                value.receiptFingerprint,
                ProductionInputDestinationCustodyDrainFingerprint.CreateReceipt(
                    value.requestFingerprint,
                    value.resultFingerprint,
                    value.releasedQuantity,
                    value.releasedMassGrams,
                    value.releasedStackIds,
                    value.releasedOperationIds),
                StringComparison.Ordinal);
    }

    private static bool Rows<T>(IEnumerable<T> source, Func<T, string> identity)
    {
        string[] values = (source ?? Array.Empty<T>()).Select(identity).ToArray();
        return Strings(values);
    }

    private static bool TrySumStacks(
        IEnumerable<ProductionInputDestinationDrainStackSaveData> source,
        out int quantity,
        out long massGrams)
    {
        quantity = 0;
        massGrams = 0L;
        try
        {
            foreach (ProductionInputDestinationDrainStackSaveData row in
                     source ?? Array.Empty<
                         ProductionInputDestinationDrainStackSaveData>())
            {
                if (row == null)
                    return false;
                quantity = checked(quantity + row.quantity);
                massGrams = checked(massGrams + row.massGrams);
            }
            return true;
        }
        catch (OverflowException)
        {
            quantity = 0;
            massGrams = 0L;
            return false;
        }
    }

    private static bool Strings(IEnumerable<string> source)
    {
        string[] values = (source ?? Array.Empty<string>()).ToArray();
        return values.All(Token)
            && values.Distinct(StringComparer.Ordinal).Count() == values.Length
            && values.SequenceEqual(
                values.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static bool Digests(IEnumerable<string> source)
    {
        string[] values = (source ?? Array.Empty<string>()).ToArray();
        return values.All(Digest)
            && values.Distinct(StringComparer.Ordinal).Count() == values.Length
            && values.SequenceEqual(
                values.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static bool Prefix(
        IReadOnlyList<string> prefix,
        IReadOnlyList<string> values) => prefix.Count <= values.Count
        && prefix.Where((value, index) => !string.Equals(
            value,
            values[index],
            StringComparison.Ordinal)).Any() == false;

    private static bool OptionalToken(string value) =>
        string.IsNullOrEmpty(value) || Token(value);

    private static bool Token(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool Digest(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}
