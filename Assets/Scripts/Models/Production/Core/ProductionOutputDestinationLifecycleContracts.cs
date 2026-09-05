using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ProductionOutputDestinationId : IEquatable<ProductionOutputDestinationId>
{
    public const string Prefix = "production-output:";
    private readonly string value;

    private ProductionOutputDestinationId(string value)
    {
        this.value = value;
    }

    public string Value => value ?? string.Empty;
    public bool IsValid => TryParse(Value, out _);

    public static ProductionOutputDestinationId FromFacility(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "A production output destination requires a valid facility ID.",
                nameof(facilityId));
        return new ProductionOutputDestinationId(Prefix + facilityId.Value);
    }

    public static bool TryParse(
        string candidate,
        out ProductionOutputDestinationId destinationId)
    {
        destinationId = default;
        if (string.IsNullOrEmpty(candidate)
            || !string.Equals(candidate, candidate.Trim(), StringComparison.Ordinal)
            || !candidate.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string facility = candidate.Substring(Prefix.Length);
        if (!((BuildingInstanceId)facility).IsValid)
            return false;
        destinationId = new ProductionOutputDestinationId(candidate);
        return true;
    }

    public bool Equals(ProductionOutputDestinationId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) =>
        obj is ProductionOutputDestinationId other && Equals(other);
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ProductionFacilityBillLifecycleSnapshot
{
    public ProductionFacilityBillLifecycleSnapshot(
        BuildingInstanceId facilityId,
        int billCount,
        int activeWipCount,
        int waitingForOutputSpaceCount,
        int publicationPreparedCount,
        int physicalCommitPendingCount,
        long billAuthorityRevision,
        string semanticFingerprint,
        string durableSemanticFingerprint = null)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "A production facility lifecycle snapshot requires a valid facility ID.",
                nameof(facilityId));
        if (billCount < 0
            || activeWipCount < 0
            || waitingForOutputSpaceCount < 0
            || publicationPreparedCount < 0
            || physicalCommitPendingCount < 0
            || billAuthorityRevision < 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(billCount));
        }
        if (!ProductionOutputLifecycleCanonical.IsFingerprint(semanticFingerprint))
            throw new ArgumentException(
                "The production lifecycle fingerprint must be canonical.",
                nameof(semanticFingerprint));
        string durable = durableSemanticFingerprint ?? semanticFingerprint;
        if (!ProductionOutputLifecycleCanonical.IsFingerprint(durable))
            throw new ArgumentException(
                "The durable production lifecycle fingerprint must be canonical.",
                nameof(durableSemanticFingerprint));

        FacilityId = facilityId;
        BillCount = billCount;
        ActiveWipCount = activeWipCount;
        WaitingForOutputSpaceCount = waitingForOutputSpaceCount;
        PublicationPreparedCount = publicationPreparedCount;
        PhysicalCommitPendingCount = physicalCommitPendingCount;
        BillAuthorityRevision = billAuthorityRevision;
        SemanticFingerprint = semanticFingerprint;
        DurableSemanticFingerprint = durable;
    }

    public BuildingInstanceId FacilityId { get; }
    public int BillCount { get; }
    public int ActiveWipCount { get; }
    public int WaitingForOutputSpaceCount { get; }
    public int PublicationPreparedCount { get; }
    public int PhysicalCommitPendingCount { get; }
    public long BillAuthorityRevision { get; }
    public string SemanticFingerprint { get; }
    public string DurableSemanticFingerprint { get; }
    public bool IsEmpty => BillCount == 0
        && ActiveWipCount == 0
        && WaitingForOutputSpaceCount == 0
        && PublicationPreparedCount == 0
        && PhysicalCommitPendingCount == 0;
}

public enum ProductionOutputLifecycleBlockCode
{
    GenericBill = 0,
    GenericWorkInProgress = 1,
    WaitingForOutputSpace = 2,
    PublicationPrepared = 3,
    PhysicalCommitPending = 4,
    EquipmentCraftOrder = 5,
    ApparelWorkOrder = 6,
    ReservedCapacityMass = 7,
    BufferedPhysicalMass = 8,
    RoutingLine = 9,
    RouteOperation = 10,
    ExactRouteOutbox = 11,
    OriginPhysicalStack = 12,
    CustodyPhysicalStack = 13,
    HaulIntent = 14,
    CarriedPhysicalMass = 15,
    RecoveryPending = 16,
    EquipmentRepairOrder = 17,
    StockSensorInstallPending = 18,
    StockSensorEmbedded = 19,
    StockSensorRemovalAwaitingAck = 20
}

public readonly struct ProductionOutputLifecycleBlock
{
    public ProductionOutputLifecycleBlock(
        ProductionOutputLifecycleBlockCode code,
        int count,
        long massGrams,
        string detail = "")
    {
        if (!Enum.IsDefined(typeof(ProductionOutputLifecycleBlockCode), code))
            throw new ArgumentOutOfRangeException(nameof(code));
        if (count < 0 || massGrams < 0L || (count == 0 && massGrams == 0L))
            throw new ArgumentOutOfRangeException(nameof(count));
        string canonicalDetail = detail ?? string.Empty;
        if (!string.Equals(
                canonicalDetail,
                canonicalDetail.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Lifecycle block detail must be canonical.", nameof(detail));
        }
        Code = code;
        Count = count;
        MassGrams = massGrams;
        Detail = canonicalDetail;
    }

    public ProductionOutputLifecycleBlockCode Code { get; }
    public int Count { get; }
    public long MassGrams { get; }
    public string Detail { get; }
}

public sealed class ProductionOutputDestinationLifecycleContribution
{
    public ProductionOutputDestinationLifecycleContribution(
        string contributorId,
        bool hasAuthority,
        long authorityRevision,
        int activeRecordCount,
        long ownedMassGrams,
        IReadOnlyList<ProductionOutputLifecycleBlock> blocks,
        string semanticFingerprint,
        string durableSemanticFingerprint = null)
    {
        if (!ProductionOutputLifecycleCanonical.IsToken(contributorId))
            throw new ArgumentException("A canonical contributor ID is required.", nameof(contributorId));
        if (authorityRevision < 0L || activeRecordCount < 0 || ownedMassGrams < 0L)
            throw new ArgumentOutOfRangeException(nameof(authorityRevision));
        if (!ProductionOutputLifecycleCanonical.IsFingerprint(semanticFingerprint))
            throw new ArgumentException("A canonical contribution fingerprint is required.", nameof(semanticFingerprint));
        string durable = durableSemanticFingerprint ?? semanticFingerprint;
        if (!ProductionOutputLifecycleCanonical.IsFingerprint(durable))
            throw new ArgumentException(
                "A canonical durable contribution fingerprint is required.",
                nameof(durableSemanticFingerprint));

        ContributorId = contributorId;
        HasAuthority = hasAuthority;
        AuthorityRevision = authorityRevision;
        ActiveRecordCount = activeRecordCount;
        OwnedMassGrams = ownedMassGrams;
        Blocks = blocks == null
            ? Array.Empty<ProductionOutputLifecycleBlock>()
            : Copy(blocks);
        SemanticFingerprint = semanticFingerprint;
        DurableSemanticFingerprint = durable;
    }

    public string ContributorId { get; }
    public bool HasAuthority { get; }
    public long AuthorityRevision { get; }
    public int ActiveRecordCount { get; }
    public long OwnedMassGrams { get; }
    public IReadOnlyList<ProductionOutputLifecycleBlock> Blocks { get; }
    public string SemanticFingerprint { get; }
    public string DurableSemanticFingerprint { get; }
    public bool IsEmpty => Blocks.Count == 0;

    private static ProductionOutputLifecycleBlock[] Copy(
        IReadOnlyList<ProductionOutputLifecycleBlock> source)
    {
        ProductionOutputLifecycleBlock[] result =
            new ProductionOutputLifecycleBlock[source.Count];
        for (int i = 0; i < source.Count; i++)
            result[i] = source[i];
        return result;
    }
}

public sealed class ProductionOutputDestinationLifecycleSnapshot
{
    public ProductionOutputDestinationLifecycleSnapshot(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId,
        IReadOnlyList<ProductionOutputDestinationLifecycleContribution> contributions,
        string semanticFingerprint,
        string durableSemanticFingerprint = null)
    {
        if (!facilityId.IsValid || !destinationId.IsValid)
            throw new ArgumentException("A valid facility and output destination are required.");
        if (!ProductionOutputLifecycleCanonical.IsFingerprint(semanticFingerprint))
            throw new ArgumentException("A canonical lifecycle fingerprint is required.", nameof(semanticFingerprint));
        string durable = durableSemanticFingerprint ?? semanticFingerprint;
        if (!ProductionOutputLifecycleCanonical.IsFingerprint(durable))
            throw new ArgumentException(
                "A canonical durable lifecycle fingerprint is required.",
                nameof(durableSemanticFingerprint));

        FacilityId = facilityId;
        DestinationId = destinationId;
        Contributions = Copy(contributions);
        SemanticFingerprint = semanticFingerprint;
        DurableSemanticFingerprint = durable;

        int records = 0;
        long mass = 0L;
        bool authority = false;
        List<ProductionOutputLifecycleBlock> blocks = new();
        for (int i = 0; i < Contributions.Count; i++)
        {
            ProductionOutputDestinationLifecycleContribution contribution = Contributions[i];
            authority |= contribution.HasAuthority;
            records = checked(records + contribution.ActiveRecordCount);
            mass = checked(mass + contribution.OwnedMassGrams);
            for (int j = 0; j < contribution.Blocks.Count; j++)
                blocks.Add(contribution.Blocks[j]);
        }
        HasAnyAuthority = authority;
        ActiveRecordCount = records;
        OwnedMassGrams = mass;
        Blocks = blocks.ToArray();
    }

    public BuildingInstanceId FacilityId { get; }
    public ProductionOutputDestinationId DestinationId { get; }
    public IReadOnlyList<ProductionOutputDestinationLifecycleContribution> Contributions { get; }
    public IReadOnlyList<ProductionOutputLifecycleBlock> Blocks { get; }
    public bool HasAnyAuthority { get; }
    public int ActiveRecordCount { get; }
    public long OwnedMassGrams { get; }
    public string SemanticFingerprint { get; }
    public string DurableSemanticFingerprint { get; }
    public bool CanRevokeEmpty => Blocks.Count == 0;

    private static ProductionOutputDestinationLifecycleContribution[] Copy(
        IReadOnlyList<ProductionOutputDestinationLifecycleContribution> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        ProductionOutputDestinationLifecycleContribution[] result =
            new ProductionOutputDestinationLifecycleContribution[source.Count];
        for (int i = 0; i < source.Count; i++)
            result[i] = source[i]
                ?? throw new ArgumentException("Lifecycle contributions cannot contain null.", nameof(source));
        return result;
    }
}

public interface IProductionOutputDestinationLifecycleContributor
{
    string ContributorId { get; }
    ProductionOutputDestinationLifecycleContribution Capture(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId);
}

public interface IProductionOutputDestinationLifecycleQuery
{
    ProductionOutputDestinationLifecycleSnapshot Capture(BuildingInstanceId facilityId);
}

public enum ProductionFacilityMutationFenceKind
{
    TransientTopology = 0,
    DurableDestructiveDrain = 1
}

public readonly struct ProductionFacilityMutationFenceSnapshot
{
    public ProductionFacilityMutationFenceSnapshot(
        BuildingInstanceId facilityId,
        string operationId,
        long operationRevision,
        ProductionFacilityMutationFenceKind kind)
    {
        if (!facilityId.IsValid
            || !ProductionOutputLifecycleCanonical.IsToken(operationId)
            || operationRevision <= 0L
            || (kind != ProductionFacilityMutationFenceKind.TransientTopology
                && kind != ProductionFacilityMutationFenceKind
                    .DurableDestructiveDrain))
        {
            throw new ArgumentException(
                "Production facility mutation fence snapshot is invalid.");
        }

        FacilityId = facilityId;
        OperationId = operationId;
        OperationRevision = operationRevision;
        Kind = kind;
    }

    public BuildingInstanceId FacilityId { get; }
    public string OperationId { get; }
    public long OperationRevision { get; }
    public ProductionFacilityMutationFenceKind Kind { get; }
}

public interface IProductionFacilityMutationEpochQuery
{
    long Revision { get; }
    bool IsFrozen(BuildingInstanceId facilityId);

    bool TryCaptureOpen(
        BuildingInstanceId facilityId,
        out ProductionFacilityMutationFenceSnapshot snapshot);
}

public interface IProductionFacilityMutationEpochAuthority :
    IProductionFacilityMutationEpochQuery
{
    bool TryBegin(
        BuildingInstanceId facilityId,
        string ownerOperationId,
        out long epoch,
        out string failureReason);

    bool IsCurrent(
        BuildingInstanceId facilityId,
        string ownerOperationId,
        long epoch);

    bool TryEnd(
        BuildingInstanceId facilityId,
        string ownerOperationId,
        long epoch,
        out string failureReason);
}

internal static class ProductionOutputLifecycleCanonical
{
    internal static bool IsToken(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    internal static bool IsFingerprint(string value) =>
        value != null
        && value.Length == 64
        && string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);
}
