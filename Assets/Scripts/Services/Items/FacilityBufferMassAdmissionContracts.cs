using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum FacilityBufferMassAdmissionFailureCode
{
    None = 0,
    InvalidProfile = 1,
    InvalidRequest = 2,
    ClaimMissingOrMismatched = 3,
    ProfileConflict = 4,
    ProfileMissing = 5,
    CapacityUnavailable = 6,
    TokenMissing = 7,
    TokenMismatch = 8,
    TokenNotReserved = 9,
    RestoreMutationAfterPublish = 10,
    OwnerMutationFenceOpen = 11,
    OwnerDestructiveDrainOpen = OwnerMutationFenceOpen
}

public enum FacilityBufferMassAdmissionTokenStatus
{
    Reserved = 0,
    Routed = 1,
    Released = 2
}

public enum FacilityBufferMassAdmissionReleaseReason
{
    TransactionRollback = 0,
    OwnerCancelled = 1,
    DestinationInvalidated = 2
}

/// <summary>
/// Immutable owner-authored mass limit for one transient FacilityBuffer.
/// Physical occupancy remains repository authority; this profile only owns the
/// positive limit and the exact destination/owner join.
/// </summary>
public sealed class FacilityBufferCapacityProfile
{
    public FacilityBufferCapacityProfile(
        string destinationId,
        Vector2Int dropPosition,
        string ownerDomain,
        string ownerOperationId,
        string ownerFacilityId,
        PhysicalMassGrams maxMass,
        long capacityRevision,
        string authorityDigest = "")
    {
        DestinationId = destinationId;
        DropPosition = dropPosition;
        OwnerDomain = ownerDomain;
        OwnerOperationId = ownerOperationId;
        OwnerFacilityId = ownerFacilityId;
        MaxMass = maxMass;
        CapacityRevision = capacityRevision;
        AuthorityDigest = authorityDigest ?? string.Empty;
        if (AuthorityDigest.Length != 0 && !IsLowercaseSha256(AuthorityDigest))
        {
            throw new ArgumentException(
                "Facility-buffer capacity authority digest must be lowercase SHA-256.",
                nameof(authorityDigest));
        }
    }

    public string DestinationId { get; }
    public Vector2Int DropPosition { get; }
    public string OwnerDomain { get; }
    public string OwnerOperationId { get; }
    public string OwnerFacilityId { get; }
    public PhysicalMassGrams MaxMass { get; }
    public long MaxMassGrams => MaxMass.Value;
    public long CapacityRevision { get; }
    public string AuthorityDigest { get; }

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        foreach (char character in value)
        {
            if (!(character is >= '0' and <= '9')
                && !(character is >= 'a' and <= 'f'))
                return false;
        }
        return true;
    }
}

public readonly struct FacilityBufferMassLotSlice
{
    public FacilityBufferMassLotSlice(
        string stackId,
        int quantity,
        long expectedReservationRevision,
        string expectedCustodyComponentFingerprint = "",
        long expectedCustodyMassGrams = 0L)
    {
        StackId = stackId;
        Quantity = quantity;
        ExpectedReservationRevision = expectedReservationRevision;
        ExpectedCustodyComponentFingerprint =
            expectedCustodyComponentFingerprint ?? string.Empty;
        ExpectedCustodyMassGrams = expectedCustodyMassGrams;
    }

    public string StackId { get; }
    public int Quantity { get; }
    public long ExpectedReservationRevision { get; }
    public string ExpectedCustodyComponentFingerprint { get; }
    public long ExpectedCustodyMassGrams { get; }
}

public readonly struct FacilityBufferPhysicalOccupancySnapshot
{
    public FacilityBufferPhysicalOccupancySnapshot(
        long nonCarriedMassGrams,
        long committedCarriedMassGrams)
    {
        if (nonCarriedMassGrams < 0L || committedCarriedMassGrams < 0L)
            throw new ArgumentOutOfRangeException(nameof(nonCarriedMassGrams));
        NonCarriedMassGrams = nonCarriedMassGrams;
        CommittedCarriedMassGrams = committedCarriedMassGrams;
    }

    public long NonCarriedMassGrams { get; }
    public long CommittedCarriedMassGrams { get; }
    public long TotalMassGrams => checked(
        NonCarriedMassGrams + CommittedCarriedMassGrams);
}

public readonly struct FacilityBufferExactLotSnapshot
{
    public FacilityBufferExactLotSnapshot(
        string fingerprint,
        PhysicalMassGrams mass)
    {
        Fingerprint = fingerprint;
        Mass = mass;
    }

    public string Fingerprint { get; }
    public PhysicalMassGrams Mass { get; }
}

public interface IFacilityBufferPhysicalOccupancyQuery
{
    FacilityBufferPhysicalOccupancySnapshot Capture(string destinationId);
    bool TryCaptureExactLot(
        IReadOnlyList<FacilityBufferMassLotSlice> slices,
        out FacilityBufferExactLotSnapshot lot,
        out string failureReason);
}

public interface IFacilityBufferCustodyOwnedPhysicalOccupancyQuery :
    IFacilityBufferPhysicalOccupancyQuery
{
    long MassAuthorityRevision { get; }
    bool TryCaptureCustodyOwnedExactLot(
        IReadOnlyList<FacilityBufferMassLotSlice> slices,
        string expectedDestinationId,
        string expectedRouteOperationId,
        string expectedPhysicalReceiptFingerprint,
        long expectedMassAuthorityRevision,
        out FacilityBufferExactLotSnapshot lot,
        out string failureReason);
}

public readonly struct FacilityBufferCustodyOwnedAdmissionRequest
{
    public FacilityBufferCustodyOwnedAdmissionRequest(
        FacilityBufferMassAdmissionRequest admission,
        string expectedRouteOperationId,
        string expectedPhysicalReceiptFingerprint,
        long expectedMassAuthorityRevision)
    {
        Admission = admission;
        ExpectedRouteOperationId = expectedRouteOperationId;
        ExpectedPhysicalReceiptFingerprint = expectedPhysicalReceiptFingerprint;
        ExpectedMassAuthorityRevision = expectedMassAuthorityRevision;
    }

    public FacilityBufferMassAdmissionRequest Admission { get; }
    public string ExpectedRouteOperationId { get; }
    public string ExpectedPhysicalReceiptFingerprint { get; }
    public long ExpectedMassAuthorityRevision { get; }
}

public readonly struct FacilityBufferMassAdmissionRequest
{
    public FacilityBufferMassAdmissionRequest(
        string transferOperationId,
        string destinationId,
        Vector2Int dropPosition,
        string expectedOwnerDomain,
        string expectedOwnerOperationId,
        string expectedOwnerFacilityId,
        long expectedCapacityRevision,
        IReadOnlyList<FacilityBufferMassLotSlice> exactLotSlices)
    {
        TransferOperationId = transferOperationId;
        DestinationId = destinationId;
        DropPosition = dropPosition;
        ExpectedOwnerDomain = expectedOwnerDomain;
        ExpectedOwnerOperationId = expectedOwnerOperationId;
        ExpectedOwnerFacilityId = expectedOwnerFacilityId;
        ExpectedCapacityRevision = expectedCapacityRevision;
        ExactLotSlices = exactLotSlices
            ?? throw new ArgumentNullException(nameof(exactLotSlices));
    }

    public string TransferOperationId { get; }
    public string DestinationId { get; }
    public Vector2Int DropPosition { get; }
    public string ExpectedOwnerDomain { get; }
    public string ExpectedOwnerOperationId { get; }
    public string ExpectedOwnerFacilityId { get; }
    public long ExpectedCapacityRevision { get; }
    public IReadOnlyList<FacilityBufferMassLotSlice> ExactLotSlices { get; }
}

public readonly struct FacilityBufferMassAdmissionToken
{
    internal FacilityBufferMassAdmissionToken(
        string tokenId,
        FacilityBufferMassAdmissionRequest request,
        FacilityBufferExactLotSnapshot exactLot,
        long profileRevision)
    {
        TokenId = tokenId;
        Request = request;
        ExactLot = exactLot;
        ReservedMass = exactLot.Mass;
        ProfileRevision = profileRevision;
    }

    public string TokenId { get; }
    public FacilityBufferMassAdmissionRequest Request { get; }
    public FacilityBufferExactLotSnapshot ExactLot { get; }
    public long ProfileRevision { get; }
    public PhysicalMassGrams ReservedMass { get; }
    public long ReservedMassGrams => ReservedMass.Value;
}

public readonly struct FacilityBufferMassAdmissionReceipt
{
    internal FacilityBufferMassAdmissionReceipt(
        FacilityBufferMassAdmissionToken token)
    {
        TokenId = token.TokenId;
        TransferOperationId = token.Request.TransferOperationId;
        DestinationId = token.Request.DestinationId;
        ExactLotFingerprint = token.ExactLot.Fingerprint;
        CommittedMassGrams = token.ReservedMassGrams;
        ProfileRevision = token.ProfileRevision;
    }

    public string TokenId { get; }
    public string TransferOperationId { get; }
    public string DestinationId { get; }
    public string ExactLotFingerprint { get; }
    public long CommittedMassGrams { get; }
    public long ProfileRevision { get; }
}

/// <summary>
/// One immutable output line which does not exist in the physical repository yet.
/// The mass subject, rather than a caller-authored gram total, is projected by the
/// shared physical-mass authority when the admission is reserved.
/// </summary>
public readonly struct FacilityBufferPlannedOutputSlice
{
    private readonly IReadOnlyList<FacilityBufferPlannedOutputComponentSnapshot>
        runtimeComponents;

    public FacilityBufferPlannedOutputSlice(
        string outputLineId,
        PhysicalItemMassSubject subject,
        int quantity)
        : this(
            outputLineId,
            subject,
            quantity,
            Array.Empty<ItemInstanceComponentSaveData>())
    {
    }

    public FacilityBufferPlannedOutputSlice(
        string outputLineId,
        PhysicalItemMassSubject subject,
        int quantity,
        IReadOnlyList<ItemInstanceComponentSaveData> runtimeComponents)
        : this(
            outputLineId,
            subject,
            quantity,
            runtimeComponents,
            string.Empty)
    {
    }

    public FacilityBufferPlannedOutputSlice(
        string outputLineId,
        PhysicalItemMassSubject subject,
        int quantity,
        IReadOnlyList<ItemInstanceComponentSaveData> runtimeComponents,
        string preparedComponentFingerprint)
        : this(
            outputLineId,
            subject,
            quantity,
            runtimeComponents,
            preparedComponentFingerprint,
            string.Empty)
    {
    }

    public FacilityBufferPlannedOutputSlice(
        string outputLineId,
        PhysicalItemMassSubject subject,
        int quantity,
        IReadOnlyList<ItemInstanceComponentSaveData> runtimeComponents,
        string preparedComponentFingerprint,
        string uniqueBindingCapabilityId)
    {
        OutputLineId = outputLineId;
        Subject = subject;
        Quantity = quantity;
        PreparedComponentFingerprint = preparedComponentFingerprint
            ?? string.Empty;
        UniqueBindingCapabilityId = uniqueBindingCapabilityId ?? string.Empty;
        if (!string.Equals(
                PreparedComponentFingerprint,
                PreparedComponentFingerprint.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Prepared component fingerprint must already be canonical.",
                nameof(preparedComponentFingerprint));
        }
        if (!string.Equals(
                UniqueBindingCapabilityId,
                UniqueBindingCapabilityId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unique binding capability ID must already be canonical.",
                nameof(uniqueBindingCapabilityId));
        }
        FacilityBufferPlannedOutputComponentSnapshot[] copied =
            (runtimeComponents ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Select(component =>
                new FacilityBufferPlannedOutputComponentSnapshot(component))
            .ToArray();
        this.runtimeComponents = Array.AsReadOnly(copied);
    }

    public string OutputLineId { get; }
    public PhysicalItemMassSubject Subject { get; }
    public ItemDefinitionId ItemDefinitionId => Subject?.ItemId ?? default;
    public int Quantity { get; }
    public string PreparedComponentFingerprint { get; }
    public string UniqueBindingCapabilityId { get; }
    public IReadOnlyList<FacilityBufferPlannedOutputComponentSnapshot>
        RuntimeComponents => runtimeComponents
            ?? Array.Empty<FacilityBufferPlannedOutputComponentSnapshot>();

    internal IReadOnlyList<ItemInstanceComponentSaveData>
        MaterializeRuntimeComponents() => RuntimeComponents
        .Select(component => component.Materialize())
        .ToArray();

#if UNITY_EDITOR
    public IReadOnlyList<ItemInstanceComponentSaveData>
        MaterializeEditorFixtureComponents() => MaterializeRuntimeComponents();
#endif
}

public sealed class FacilityBufferPlannedOutputComponentSnapshot
{
    private readonly ItemInstanceComponentSaveData component;

    public FacilityBufferPlannedOutputComponentSnapshot(
        ItemInstanceComponentSaveData component)
    {
        this.component = component?.Clone()
            ?? throw new ArgumentNullException(nameof(component));
        CanonicalFingerprint = this.component.ToCanonicalString();
    }

    public string ComponentTypeId => component.componentTypeId;
    public int SchemaVersion => component.schemaVersion;
    public bool AffectsStacking => component.affectsStacking;
    public string CanonicalFingerprint { get; }

    internal ItemInstanceComponentSaveData Materialize() => component.Clone();
}

/// <summary>
/// Capacity request for a complete, resolved output batch. Planned output never
/// impersonates a repository lot and therefore has no stack ID or reservation
/// revision. The slices are defensively copied at the request boundary.
/// </summary>
public readonly struct FacilityBufferPlannedOutputRequest
{
    private readonly IReadOnlyList<FacilityBufferPlannedOutputSlice> slices;

    public FacilityBufferPlannedOutputRequest(
        string publicationOperationId,
        string batchCommitId,
        string outcomeFingerprint,
        string destinationId,
        Vector2Int dropPosition,
        string expectedOwnerDomain,
        string expectedOwnerOperationId,
        string expectedOwnerFacilityId,
        long expectedCapacityRevision,
        IReadOnlyList<FacilityBufferPlannedOutputSlice> slices,
        string capacitySourceDigest = "",
        long expectedMinimumCapacityGrams = 0L,
        string capacityAuthorityDigest = "")
    {
        PublicationOperationId = publicationOperationId;
        BatchCommitId = batchCommitId;
        OutcomeFingerprint = outcomeFingerprint;
        DestinationId = destinationId;
        DropPosition = dropPosition;
        ExpectedOwnerDomain = expectedOwnerDomain;
        ExpectedOwnerOperationId = expectedOwnerOperationId;
        ExpectedOwnerFacilityId = expectedOwnerFacilityId;
        ExpectedCapacityRevision = expectedCapacityRevision;
        CapacitySourceDigest = capacitySourceDigest ?? string.Empty;
        ExpectedMinimumCapacityGrams = expectedMinimumCapacityGrams;
        CapacityAuthorityDigest = capacityAuthorityDigest ?? string.Empty;
        FacilityBufferPlannedOutputSlice[] copied = (slices
                ?? throw new ArgumentNullException(nameof(slices)))
            .ToArray();
        this.slices = Array.AsReadOnly(copied);
    }

    public string PublicationOperationId { get; }
    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public string DestinationId { get; }
    public Vector2Int DropPosition { get; }
    public string ExpectedOwnerDomain { get; }
    public string ExpectedOwnerOperationId { get; }
    public string ExpectedOwnerFacilityId { get; }
    public long ExpectedCapacityRevision { get; }
    public string CapacitySourceDigest { get; }
    public long ExpectedMinimumCapacityGrams { get; }
    public string CapacityAuthorityDigest { get; }
    public IReadOnlyList<FacilityBufferPlannedOutputSlice> Slices =>
        slices ?? Array.Empty<FacilityBufferPlannedOutputSlice>();
}

public readonly struct FacilityBufferPlannedOutputSliceSnapshot
{
    internal FacilityBufferPlannedOutputSliceSnapshot(
        FacilityBufferPlannedOutputSlice source,
        PhysicalMassGrams exactMass)
    {
        Source = source;
        ExactMass = exactMass;
    }

    public FacilityBufferPlannedOutputSlice Source { get; }
    public string OutputLineId => Source.OutputLineId;
    public ItemDefinitionId ItemDefinitionId => Source.ItemDefinitionId;
    public int Quantity => Source.Quantity;
    public PhysicalMassGrams ExactMass { get; }
    public long ExactMassGrams => ExactMass.Value;
}

public readonly struct FacilityBufferPlannedOutputSnapshot
{
    private readonly IReadOnlyList<FacilityBufferPlannedOutputSliceSnapshot> slices;

    internal FacilityBufferPlannedOutputSnapshot(
        string fingerprint,
        IReadOnlyList<FacilityBufferPlannedOutputSliceSnapshot> slices,
        int totalQuantity,
        PhysicalMassGrams totalMass)
    {
        Fingerprint = fingerprint;
        FacilityBufferPlannedOutputSliceSnapshot[] copied = (slices
                ?? throw new ArgumentNullException(nameof(slices)))
            .ToArray();
        this.slices = Array.AsReadOnly(copied);
        TotalQuantity = totalQuantity;
        TotalMass = totalMass;
    }

    public string Fingerprint { get; }
    public IReadOnlyList<FacilityBufferPlannedOutputSliceSnapshot> Slices =>
        slices ?? Array.Empty<FacilityBufferPlannedOutputSliceSnapshot>();
    public int TotalQuantity { get; }
    public PhysicalMassGrams TotalMass { get; }
    public long TotalMassGrams => TotalMass.Value;
}

public readonly struct FacilityBufferPlannedOutputToken
{
    internal FacilityBufferPlannedOutputToken(
        string tokenId,
        FacilityBufferPlannedOutputRequest request,
        FacilityBufferPlannedOutputSnapshot plannedOutput,
        long capacityAuthorityRevision,
        long massAuthorityRevision)
    {
        TokenId = tokenId;
        Request = request;
        PlannedOutput = plannedOutput;
        CapacityAuthorityRevision = capacityAuthorityRevision;
        MassAuthorityRevision = massAuthorityRevision;
    }

    public string TokenId { get; }
    public FacilityBufferPlannedOutputRequest Request { get; }
    public FacilityBufferPlannedOutputSnapshot PlannedOutput { get; }
    public long CapacityAuthorityRevision { get; }
    public long MassAuthorityRevision { get; }
    public PhysicalMassGrams ReservedMass => PlannedOutput.TotalMass;
    public long ReservedMassGrams => ReservedMass.Value;
}

/// <summary>
/// One physical stack reported by the future atomic publication service. This is
/// a commit receipt, not an instruction to spawn. Admission validates every stack
/// against the mass-authority snapshot captured in the planned token.
/// </summary>
public readonly struct FacilityBufferPublishedOutputStackReceipt
{
    public FacilityBufferPublishedOutputStackReceipt(
        string stackId,
        string outputLineId,
        ItemDefinitionId itemDefinitionId,
        int quantity,
        PhysicalMassGrams mass,
        string itemInstanceId = "")
    {
        StackId = stackId;
        OutputLineId = outputLineId;
        ItemDefinitionId = itemDefinitionId;
        Quantity = quantity;
        Mass = mass;
        ItemInstanceId = itemInstanceId ?? string.Empty;
    }

    public string StackId { get; }
    public string OutputLineId { get; }
    public ItemDefinitionId ItemDefinitionId { get; }
    public int Quantity { get; }
    public PhysicalMassGrams Mass { get; }
    public string ItemInstanceId { get; }
    public long MassGrams => Mass.Value;
}

public readonly struct FacilityBufferPlannedOutputPublicationReceipt
{
    private readonly IReadOnlyList<FacilityBufferPublishedOutputStackReceipt> stacks;

    public FacilityBufferPlannedOutputPublicationReceipt(
        string admissionTokenId,
        string batchCommitId,
        string outcomeFingerprint,
        string destinationId,
        Vector2Int dropPosition,
        string ownerDomain,
        string ownerOperationId,
        string ownerFacilityId,
        long capacityRevision,
        string plannedOutputFingerprint,
        IReadOnlyList<FacilityBufferPublishedOutputStackReceipt> stacks)
    {
        AdmissionTokenId = admissionTokenId;
        BatchCommitId = batchCommitId;
        OutcomeFingerprint = outcomeFingerprint;
        DestinationId = destinationId;
        DropPosition = dropPosition;
        OwnerDomain = ownerDomain;
        OwnerOperationId = ownerOperationId;
        OwnerFacilityId = ownerFacilityId;
        CapacityRevision = capacityRevision;
        PlannedOutputFingerprint = plannedOutputFingerprint;
        FacilityBufferPublishedOutputStackReceipt[] copied = (stacks
                ?? throw new ArgumentNullException(nameof(stacks)))
            .ToArray();
        this.stacks = Array.AsReadOnly(copied);
    }

    public string AdmissionTokenId { get; }
    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public string DestinationId { get; }
    public Vector2Int DropPosition { get; }
    public string OwnerDomain { get; }
    public string OwnerOperationId { get; }
    public string OwnerFacilityId { get; }
    public long CapacityRevision { get; }
    public string PlannedOutputFingerprint { get; }
    public IReadOnlyList<FacilityBufferPublishedOutputStackReceipt> Stacks =>
        stacks ?? Array.Empty<FacilityBufferPublishedOutputStackReceipt>();
}

public readonly struct FacilityBufferPlannedOutputReceipt
{
    private readonly IReadOnlyList<FacilityBufferPublishedOutputStackReceipt> stacks;

    internal FacilityBufferPlannedOutputReceipt(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication)
    {
        TokenId = token.TokenId;
        PublicationOperationId = token.Request.PublicationOperationId;
        BatchCommitId = token.Request.BatchCommitId;
        OutcomeFingerprint = token.Request.OutcomeFingerprint;
        PlannedOutputFingerprint = token.PlannedOutput.Fingerprint;
        DestinationId = token.Request.DestinationId;
        DropPosition = token.Request.DropPosition;
        OwnerDomain = token.Request.ExpectedOwnerDomain;
        OwnerOperationId = token.Request.ExpectedOwnerOperationId;
        OwnerFacilityId = token.Request.ExpectedOwnerFacilityId;
        ProfileCapacityRevision = token.Request.ExpectedCapacityRevision;
        FacilityBufferPublishedOutputStackReceipt[] copied = publication.Stacks
            .ToArray();
        stacks = Array.AsReadOnly(copied);
        PublishedStackCount = copied.Length;
        PublishedQuantity = token.PlannedOutput.TotalQuantity;
        CommittedMassGrams = token.ReservedMassGrams;
        CapacityAuthorityRevision = token.CapacityAuthorityRevision;
        MassAuthorityRevision = token.MassAuthorityRevision;
    }

    public string TokenId { get; }
    public string PublicationOperationId { get; }
    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public string PlannedOutputFingerprint { get; }
    public string DestinationId { get; }
    public Vector2Int DropPosition { get; }
    public string OwnerDomain { get; }
    public string OwnerOperationId { get; }
    public string OwnerFacilityId { get; }
    public long ProfileCapacityRevision { get; }
    public IReadOnlyList<FacilityBufferPublishedOutputStackReceipt> Stacks =>
        stacks ?? Array.Empty<FacilityBufferPublishedOutputStackReceipt>();
    public int PublishedStackCount { get; }
    public int PublishedQuantity { get; }
    public long CommittedMassGrams { get; }
    public long CapacityAuthorityRevision { get; }
    public long MassAuthorityRevision { get; }
}

public readonly struct FacilityBufferMassCapacitySnapshot
{
    internal FacilityBufferMassCapacitySnapshot(
        FacilityBufferCapacityProfile profile,
        long reservedMassGrams,
        long massAuthorityRevision)
    {
        Profile = profile;
        ReservedMassGrams = reservedMassGrams;
        MassAuthorityRevision = massAuthorityRevision;
    }

    public FacilityBufferCapacityProfile Profile { get; }
    public long ReservedMassGrams { get; }
    public long MassAuthorityRevision { get; }
}

public interface IFacilityBufferMassCapacityQuery
{
    long Revision { get; }
    bool TryGetCapacity(
        string destinationId,
        Vector2Int dropPosition,
        out FacilityBufferMassCapacitySnapshot snapshot);
    bool TryGetReceipt(
        string tokenId,
        out FacilityBufferMassAdmissionReceipt receipt);
    IReadOnlyList<FacilityBufferCapacityProfile> CaptureProfiles();
    bool TryGetCapacityAuthorityFingerprint(
        string destinationId,
        Vector2Int dropPosition,
        out string fingerprint);
}

public interface IFacilityBufferMassCapacityAuthorityQuery
{
    IReadOnlyList<FacilityBufferCapacityProfile> CaptureAuthorityProfiles();
}

public interface IFacilityBufferMassCapacityCommand
{
    bool TryReplaceOwnedProfiles(
        string ownerDomain,
        IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
}

public interface IFacilityBufferMassAdmissionService :
    IFacilityBufferMassCapacityQuery,
    IFacilityBufferMassCapacityCommand
{
    bool TryValidateExactDestinationClaim(
        string destinationId,
        Vector2Int dropPosition,
        out string failureReason);
    bool TryReserveExactLot(
        FacilityBufferMassAdmissionRequest request,
        out FacilityBufferMassAdmissionToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryReserveCustodyOwnedExactLot(
        FacilityBufferCustodyOwnedAdmissionRequest request,
        out FacilityBufferMassAdmissionToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryCommitRouted(
        FacilityBufferMassAdmissionToken token,
        string exactLotFingerprint,
        long routedMassGrams,
        out FacilityBufferMassAdmissionReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryRelease(
        FacilityBufferMassAdmissionToken token,
        FacilityBufferMassAdmissionReleaseReason reason,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryRollbackRouted(
        FacilityBufferMassAdmissionToken token,
        FacilityBufferMassAdmissionReceipt expectedReceipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryReservePlannedOutput(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryValidatePlannedOutputReservation(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryValidatePlannedOutputPublicationToken(
        FacilityBufferPlannedOutputToken token,
        out bool publicationCommitted,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryCommitPlannedOutput(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out FacilityBufferPlannedOutputReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryReleasePlannedOutput(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferMassAdmissionReleaseReason reason,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryGetPlannedOutputReceipt(
        string tokenId,
        out FacilityBufferPlannedOutputReceipt receipt);
    bool TryGetPlannedOutputToken(
        string tokenId,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionTokenStatus status)
    {
        token = default;
        status = FacilityBufferMassAdmissionTokenStatus.Released;
        return false;
    }
}

public interface IReversibleFacilityBufferPlannedOutputAdmission
{
    FacilityBufferPlannedOutputReceipt Receipt { get; }
    bool TryRollback(
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryAcknowledge(out string failureReason);
}

/// <summary>
/// Synchronous rollback seam for a larger output/outcome transaction. The
/// handle is not save authority and must be acknowledged or rolled back in the
/// same call that committed it.
/// </summary>
public interface IReversibleFacilityBufferPlannedOutputAdmissionService
{
    bool TryCommitPlannedOutputReversible(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out IReversibleFacilityBufferPlannedOutputAdmission transaction,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
}

/// <summary>
/// Pure projection boundary used by detached restore validators. It computes
/// the same exact mass and fingerprint as admission without reserving capacity
/// or mutating token state.
/// </summary>
public interface IFacilityBufferPlannedOutputProjectionQuery
{
    bool TryProjectPlannedOutput(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputSnapshot planned,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
}
