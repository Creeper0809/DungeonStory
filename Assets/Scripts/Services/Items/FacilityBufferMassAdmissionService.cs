using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

/// <summary>
/// Reads exact physical occupancy and lot mass from the same repository state
/// used by delivery retargeting. Callers cannot author occupancy totals.
/// </summary>
public sealed class FacilityBufferPhysicalOccupancyQuery :
    IFacilityBufferCustodyOwnedPhysicalOccupancyQuery
{
    private readonly WorldItemRepository repository;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IRestoreHaulDeliveryIntentCandidateQuery restoreHaulIntents;

    public FacilityBufferPhysicalOccupancyQuery(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        IItemQuantityReservationService quantityReservations,
        DungeonRuntimeAggregateRootStore aggregateRootStore = null,
        IRestoreHaulDeliveryIntentCandidateQuery restoreHaulIntents = null)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.quantityReservations = quantityReservations
            ?? throw new ArgumentNullException(nameof(quantityReservations));
        this.aggregateRootStore = aggregateRootStore;
        this.restoreHaulIntents = restoreHaulIntents;
    }

    public long MassAuthorityRevision => massQuery.AuthorityRevision;

    public FacilityBufferPhysicalOccupancySnapshot Capture(string destinationId)
    {
        string destination = destinationId ?? string.Empty;
        if (!IsCanonicalRequired(destination))
            throw new ArgumentException("A canonical destination is required.");

        long nonCarried = 0L;
        foreach (WorldItemStackRecord record in repository.Records
                     .Where(record => record != null
                         && record.quantity > 0
                         && record.state != WorldItemStackState.Carried
                         && string.Equals(
                             record.destinationId,
                             destination,
                             StringComparison.Ordinal))
                     .OrderBy(record => record.stackId, StringComparer.Ordinal))
        {
            nonCarried = checked(nonCarried + GetMass(record, record.quantity));
        }

        long carried = 0L;
        foreach (HaulDeliveryIntentSaveData intent in HaulDeliveryIntentAuthorityView
                     .Capture(
                         aggregateRootStore,
                         restoreHaulIntents,
                         repository.HaulDeliveryIntents)
                     .Where(intent => intent != null
                         && string.Equals(
                             intent.destinationId,
                             destination,
                             StringComparison.Ordinal))
                     .OrderBy(intent => intent.operationId, StringComparer.Ordinal))
        {
            foreach (HaulDeliveryItemCommitmentSaveData commitment in
                     (intent.commitments
                         ?? new List<HaulDeliveryItemCommitmentSaveData>())
                     .Where(value => value != null)
                     .OrderBy(value => value.carriedStackId, StringComparer.Ordinal))
            {
                if (!repository.RecordsById.TryGetValue(
                        commitment.carriedStackId,
                        out WorldItemStackRecord record)
                    || record == null)
                {
                    // A successful facility deposit may aggregate the carried
                    // identity before AbilityHaul retires its intent later in
                    // the same synchronous action. The physical destination
                    // total above is already authoritative in that window.
                    continue;
                }
                if (record.state != WorldItemStackState.Carried)
                {
                    if (record.state == WorldItemStackState.FacilityBuffer
                        && string.Equals(
                            record.destinationId,
                            destination,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    throw new InvalidOperationException(
                        $"Committed carried lot '{commitment.carriedStackId}' has invalid state for facility-buffer '{destination}'.");
                }
                if (record.quantity != commitment.quantity
                    || !string.Equals(
                        record.destinationId,
                        intent.ownerCharacterId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        record.itemId,
                        commitment.itemId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Committed carried lot '{commitment.carriedStackId}' is inconsistent with facility-buffer '{destination}'.");
                }
                carried = checked(carried + GetMass(record, commitment.quantity));
            }
        }

        return new FacilityBufferPhysicalOccupancySnapshot(
            nonCarried,
            carried);
    }

    public bool TryCaptureExactLot(
        IReadOnlyList<FacilityBufferMassLotSlice> slices,
        out FacilityBufferExactLotSnapshot lot,
        out string failureReason)
    {
        lot = default;
        failureReason = string.Empty;
        FacilityBufferMassLotSlice[] requested =
            (slices ?? Array.Empty<FacilityBufferMassLotSlice>()).ToArray();
        if (requested.Length == 0
            || requested.Any(slice => !IsCanonicalRequired(slice.StackId)
                || slice.Quantity <= 0
                || slice.ExpectedReservationRevision < 0L)
            || requested.Select(slice => slice.StackId)
                .Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            failureReason = "facility-buffer-exact-lot-invalid";
            return false;
        }

        long massGrams = 0L;
        List<string> fingerprintParts = new(requested.Length);
        foreach (FacilityBufferMassLotSlice slice in requested
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            if (!repository.RecordsById.TryGetValue(
                    slice.StackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.reservationRevision != slice.ExpectedReservationRevision
                || slice.Quantity > quantityReservations.GetAvailableQuantity(
                    (ItemStackId)slice.StackId))
            {
                failureReason =
                    "facility-buffer-exact-lot-changed:" + slice.StackId;
                return false;
            }
            long sliceMass = GetMass(record, slice.Quantity);
            massGrams = checked(massGrams + sliceMass);
            fingerprintParts.Add(
                slice.StackId + ":"
                + slice.ExpectedReservationRevision.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                + ":"
                + slice.Quantity.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                + ":"
                + sliceMass.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        if (massGrams <= 0L)
        {
            failureReason = "facility-buffer-exact-lot-mass-invalid";
            return false;
        }

        lot = new FacilityBufferExactLotSnapshot(
            string.Join("|", fingerprintParts),
            new PhysicalMassGrams(massGrams));
        return true;
    }

    public bool TryCaptureCustodyOwnedExactLot(
        IReadOnlyList<FacilityBufferMassLotSlice> slices,
        string expectedDestinationId,
        string expectedRouteOperationId,
        string expectedPhysicalReceiptFingerprint,
        long expectedMassAuthorityRevision,
        out FacilityBufferExactLotSnapshot lot,
        out string failureReason)
    {
        lot = default;
        failureReason = string.Empty;
        FacilityBufferMassLotSlice[] requested =
            (slices ?? Array.Empty<FacilityBufferMassLotSlice>()).ToArray();
        if (!IsCanonicalRequired(expectedDestinationId)
            || !IsCanonicalRequired(expectedRouteOperationId)
            || !IsCanonicalRequired(expectedPhysicalReceiptFingerprint)
            || expectedMassAuthorityRevision <= 0L
            || expectedMassAuthorityRevision != massQuery.AuthorityRevision
            || requested.Length == 0
            || requested.Any(slice => !IsCanonicalRequired(slice.StackId)
                || slice.Quantity <= 0
                || slice.ExpectedReservationRevision < 0L
                || !IsCanonicalRequired(
                    slice.ExpectedCustodyComponentFingerprint)
                || slice.ExpectedCustodyMassGrams <= 0L)
            || requested.Select(slice => slice.StackId)
                .Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            failureReason = "facility-buffer-custody-lot-invalid-or-stale";
            return false;
        }

        long totalMass = 0L;
        List<string> fingerprintParts = new(requested.Length);
        foreach (FacilityBufferMassLotSlice slice in requested
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            if (!repository.RecordsById.TryGetValue(
                    slice.StackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.state != WorldItemStackState.Loose
                || record.quantity != slice.Quantity
                || record.reservationRevision != slice.ExpectedReservationRevision
                || !FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                || custody.Phase != FacilityOutputExactRouteCustodyPhase.Routable
                || custody.Quantity != slice.Quantity
                || !string.Equals(
                    custody.CurrentSourceStackId,
                    record.stackId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    custody.RouteOperationId,
                    expectedRouteOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    custody.PhysicalReceiptFingerprint,
                    expectedPhysicalReceiptFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    custody.ComponentFingerprint,
                    slice.ExpectedCustodyComponentFingerprint,
                    StringComparison.Ordinal)
                || slice.ExpectedCustodyMassGrams <= 0L
                || custody.MassGrams != slice.ExpectedCustodyMassGrams)
            {
                failureReason =
                    "facility-buffer-custody-lot-changed:" + slice.StackId;
                return false;
            }

            long exactMass;
            try
            {
                exactMass = GetMass(record, slice.Quantity);
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or OverflowException)
            {
                failureReason = "facility-buffer-custody-mass-invalid:"
                    + exception.Message;
                return false;
            }
            if (exactMass <= 0L || exactMass != custody.MassGrams)
            {
                failureReason =
                    "facility-buffer-custody-mass-changed:" + slice.StackId;
                return false;
            }
            totalMass = checked(totalMass + exactMass);
            fingerprintParts.Add(
                slice.StackId + ":"
                + slice.ExpectedReservationRevision.ToString(
                    CultureInfo.InvariantCulture) + ":"
                + custody.SourceOffsetQuantity.ToString(
                    CultureInfo.InvariantCulture) + ":"
                + slice.Quantity.ToString(CultureInfo.InvariantCulture) + ":"
                + exactMass.ToString(CultureInfo.InvariantCulture) + ":"
                + custody.PhysicalReceiptFingerprint);
        }
        if (massQuery.AuthorityRevision != expectedMassAuthorityRevision
            || totalMass <= 0L)
        {
            failureReason = "facility-buffer-custody-mass-authority-stale";
            return false;
        }

        lot = new FacilityBufferExactLotSnapshot(
            string.Join("|", fingerprintParts),
            new PhysicalMassGrams(totalMass));
        return true;
    }

    private long GetMass(WorldItemStackRecord record, int quantity)
    {
        ItemDefinitionId itemId = (ItemDefinitionId)record.itemId;
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            itemId,
            record.itemInstanceId,
            record.components);
        return massQuery.GetQuantityMass(itemId, subject, quantity).Value;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

/// <summary>
/// Transient synchronous admission authority for existing-lot retargeting and
/// not-yet-physical output publication. Both token kinds spend the same destination
/// ledger. Once physical publication succeeds, repository occupancy replaces the
/// reservation, so neither token payload is save authority.
/// </summary>
public sealed class FacilityBufferMassAdmissionService :
    IFacilityBufferMassAdmissionService,
    IFacilityBufferPlannedOutputProjectionQuery,
    IFacilityBufferMassCapacityAuthorityQuery,
    IDungeonPreStageRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "221.world.facility-buffer-mass-capacity";
    private const int MaximumTerminalTokenCount = 2_048;
    private sealed class TokenState
    {
        internal FacilityBufferMassAdmissionToken Token;
        internal FacilityBufferMassAdmissionTokenStatus Status;
        internal FacilityBufferMassAdmissionReleaseReason ReleaseReason;
        internal FacilityBufferMassAdmissionReceipt Receipt;
    }

    private sealed class PlannedOutputTokenState
    {
        internal FacilityBufferPlannedOutputToken Token;
        internal FacilityBufferMassAdmissionTokenStatus Status;
        internal FacilityBufferMassAdmissionReleaseReason ReleaseReason;
        internal FacilityBufferPlannedOutputPublicationReceipt Publication;
        internal FacilityBufferPlannedOutputReceipt Receipt;
    }

    private readonly IFacilityBufferDestinationClaimAuthorityQuery destinationClaims;
    private readonly IFacilityBufferPhysicalOccupancyQuery physicalOccupancy;
    private readonly IPhysicalItemMassQuery plannedOutputMassQuery;
    private readonly IFacilityBufferDestinationAdmissionFenceQuery
        admissionFences;
    private Dictionary<string, FacilityBufferCapacityProfile> profiles =
        CreateProfileMap();
    private Dictionary<string, FacilityBufferCapacityProfile> candidateProfiles;
    private Dictionary<string, FacilityBufferCapacityProfile> previousProfiles;
    private readonly Dictionary<string, TokenState> tokens =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, PlannedOutputTokenState> plannedOutputTokens =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> reservedByDestination =
        new(StringComparer.Ordinal);
    private readonly Queue<string> terminalTokenOrder = new();
    private readonly Queue<string> plannedTerminalTokenOrder = new();
    private long revision;
    private long nextTokenSequence = 1L;
    private long preparedPublishRevision;
    private long preparedRollbackRevision;
    private bool restoreActive;
    private bool restorePublished;

    public FacilityBufferMassAdmissionService(
        IFacilityBufferDestinationClaimAuthorityQuery destinationClaims,
        IFacilityBufferPhysicalOccupancyQuery physicalOccupancy,
        IPhysicalItemMassQuery plannedOutputMassQuery = null,
        IProductionFacilityDestructiveDrainOpenOperationQuery
            openDestructiveDrains = null)
        : this(
            destinationClaims,
            physicalOccupancy,
            plannedOutputMassQuery,
            openDestructiveDrains == null
                ? null
                : new FacilityBufferDestinationAdmissionFenceQuery(new[]
                {
                    new ProductionFacilityDestructiveDrainAdmissionFenceSource(
                        openDestructiveDrains)
                }))
    {
    }

    [VContainer.Inject]
    public FacilityBufferMassAdmissionService(
        IFacilityBufferDestinationClaimAuthorityQuery destinationClaims,
        IFacilityBufferPhysicalOccupancyQuery physicalOccupancy,
        IPhysicalItemMassQuery plannedOutputMassQuery,
        IFacilityBufferDestinationAdmissionFenceQuery admissionFences)
    {
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        this.physicalOccupancy = physicalOccupancy
            ?? throw new ArgumentNullException(nameof(physicalOccupancy));
        this.plannedOutputMassQuery = plannedOutputMassQuery;
        this.admissionFences = admissionFences;
    }

    public string ParticipantId => RestoreParticipantId;
    public long Revision => revision;

    public IReadOnlyList<FacilityBufferCapacityProfile> CaptureProfiles() =>
        profiles.Values
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<FacilityBufferCapacityProfile>
        CaptureAuthorityProfiles() => GetAuthorityView().Values
        .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
        .ToArray();

    public bool TryValidateExactDestinationClaim(
        string destinationId,
        Vector2Int dropPosition,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!destinationClaims.TryGetAuthorityClaim(
                destinationId,
                dropPosition,
                out FacilityBufferDestinationClaim claim)
            || claim == null)
        {
            failureReason =
                "facility-buffer-exact-destination-claim-missing";
            return false;
        }
        return true;
    }

    public bool TryGetCapacity(
        string destinationId,
        Vector2Int dropPosition,
        out FacilityBufferMassCapacitySnapshot snapshot)
    {
        snapshot = default;
        if (!profiles.TryGetValue(
                destinationId ?? string.Empty,
                out FacilityBufferCapacityProfile profile)
            || profile.DropPosition != dropPosition)
        {
            return false;
        }

        snapshot = new FacilityBufferMassCapacitySnapshot(
            profile,
            reservedByDestination.GetValueOrDefault(profile.DestinationId, 0L),
            physicalOccupancy is IFacilityBufferCustodyOwnedPhysicalOccupancyQuery
                custodyOccupancy
                ? custodyOccupancy.MassAuthorityRevision
                : plannedOutputMassQuery?.AuthorityRevision ?? 0L);
        return true;
    }

    public bool TryGetCapacityAuthorityFingerprint(
        string destinationId,
        Vector2Int dropPosition,
        out string fingerprint)
    {
        fingerprint = string.Empty;
        if (!TryGetCapacity(destinationId, dropPosition, out var snapshot)
            || snapshot.Profile == null)
        {
            return false;
        }
        FacilityBufferCapacityProfile profile = snapshot.Profile;
        string payload = string.Join("|",
            "facility-buffer-capacity-v1",
            profile.DestinationId,
            profile.DropPosition.x.ToString(CultureInfo.InvariantCulture),
            profile.DropPosition.y.ToString(CultureInfo.InvariantCulture),
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.MaxMassGrams.ToString(CultureInfo.InvariantCulture),
            profile.CapacityRevision.ToString(CultureInfo.InvariantCulture),
            revision.ToString(CultureInfo.InvariantCulture),
            snapshot.MassAuthorityRevision.ToString(
                CultureInfo.InvariantCulture));
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        StringBuilder hex = new(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        fingerprint = hex.ToString();
        return true;
    }

    public bool TryGetReceipt(
        string tokenId,
        out FacilityBufferMassAdmissionReceipt receipt)
    {
        receipt = default;
        return tokens.TryGetValue(tokenId ?? string.Empty, out TokenState state)
            && state.Status == FacilityBufferMassAdmissionTokenStatus.Routed
            && (receipt = state.Receipt).CommittedMassGrams > 0L;
    }

    public bool TryGetPlannedOutputReceipt(
        string tokenId,
        out FacilityBufferPlannedOutputReceipt receipt)
    {
        receipt = default;
        return plannedOutputTokens.TryGetValue(
                tokenId ?? string.Empty,
                out PlannedOutputTokenState state)
            && state.Status == FacilityBufferMassAdmissionTokenStatus.Routed
            && (receipt = state.Receipt).CommittedMassGrams > 0L;
    }

    public bool TryGetPlannedOutputToken(
        string tokenId,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionTokenStatus status)
    {
        token = default;
        status = FacilityBufferMassAdmissionTokenStatus.Released;
        if (!plannedOutputTokens.TryGetValue(
                tokenId ?? string.Empty,
                out PlannedOutputTokenState state))
        {
            return false;
        }
        token = state.Token;
        status = state.Status;
        return !string.IsNullOrEmpty(token.TokenId);
    }

    public bool TryValidatePlannedOutputReservation(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!TryValidatePlannedOutputPublicationToken(
                token,
                out bool publicationCommitted,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (!publicationCommitted)
            return true;
        return Fail(
            FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
            $"Facility-buffer planned-output token '{token.TokenId}' is already committed.",
            out failureCode,
            out failureReason);
    }

    public bool TryValidatePlannedOutputPublicationToken(
        FacilityBufferPlannedOutputToken token,
        out bool publicationCommitted,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        publicationCommitted = false;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!plannedOutputTokens.TryGetValue(
                token.TokenId ?? string.Empty,
                out PlannedOutputTokenState state))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer planned-output token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Routed)
        {
            if (!PlannedOutputTokenMatches(state.Token, token))
            {
                return Fail(
                    FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                    $"Facility-buffer planned-output token '{token.TokenId}' replay mismatched.",
                    out failureCode,
                    out failureReason);
            }
            publicationCommitted = true;
            return true;
        }
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer planned-output token '{token.TokenId}' is not reserved.",
                out failureCode,
                out failureReason);
        }
        if (!PlannedOutputTokenMatches(state.Token, token)
            || plannedOutputMassQuery == null
            || token.CapacityAuthorityRevision != revision
            || token.MassAuthorityRevision != plannedOutputMassQuery.AuthorityRevision
            || !profiles.TryGetValue(
                token.Request.DestinationId,
                out FacilityBufferCapacityProfile profile)
            || !ProfileMatchesPlannedOutputRequest(profile, token.Request))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' is stale or mismatched.",
                out failureCode,
                out failureReason);
        }
        return TryValidateProfile(
            profile,
            profile.OwnerDomain,
            out failureCode,
            out failureReason);
    }

    public bool TryReplaceOwnedProfiles(
        string ownerDomain,
        IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!IsCanonicalRequired(ownerDomain))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidProfile,
                "Facility-buffer capacity owner domain is invalid.",
                out failureCode,
                out failureReason);
        }
        if (!TryGetMutationTarget(
                out Dictionary<string, FacilityBufferCapacityProfile> target,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        FacilityBufferCapacityProfile[] desired =
            (desiredProfiles ?? Array.Empty<FacilityBufferCapacityProfile>())
            .ToArray();
        Dictionary<string, FacilityBufferCapacityProfile> replacements =
            new(StringComparer.Ordinal);
        foreach (FacilityBufferCapacityProfile profile in desired)
        {
            if (!TryValidateProfile(
                    profile,
                    ownerDomain,
                    out failureCode,
                    out failureReason))
            {
                return false;
            }
            if (!replacements.TryAdd(profile.DestinationId, profile))
            {
                return Fail(
                    FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                    $"Duplicate facility-buffer capacity '{profile.DestinationId}'.",
                    out failureCode,
                    out failureReason);
            }
        }

        if (!restoreActive
            && OwnedProfileSetMatches(target, ownerDomain, replacements))
        {
            return true;
        }

        string[] retiring = target.Values
            .Where(value => string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal))
            .Select(value => value.DestinationId)
            .Where(destinationId => !replacements.ContainsKey(destinationId))
            .ToArray();
        if (retiring.Any(destinationId =>
                reservedByDestination.GetValueOrDefault(destinationId, 0L) > 0L
                || physicalOccupancy.Capture(destinationId).TotalMassGrams > 0L))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                "A facility-buffer capacity with a live admission cannot be retired.",
                out failureCode,
                out failureReason);
        }

        Dictionary<string, FacilityBufferCapacityProfile> next =
            new(target, StringComparer.Ordinal);
        foreach (string destinationId in next.Values
                     .Where(value => string.Equals(
                         value.OwnerDomain,
                         ownerDomain,
                         StringComparison.Ordinal))
                     .Select(value => value.DestinationId)
                     .ToArray())
        {
            next.Remove(destinationId);
        }
        foreach (KeyValuePair<string, FacilityBufferCapacityProfile> pair in
                 replacements)
        {
            if (next.TryGetValue(pair.Key, out FacilityBufferCapacityProfile foreign)
                && !string.Equals(
                    foreign.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
            {
                return Fail(
                    FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                    $"Facility-buffer capacity '{pair.Key}' belongs to another domain.",
                    out failureCode,
                    out failureReason);
            }
            long occupied = checked(
                physicalOccupancy.Capture(pair.Key).TotalMassGrams
                + reservedByDestination.GetValueOrDefault(pair.Key, 0L));
            if (target.TryGetValue(
                    pair.Key,
                    out FacilityBufferCapacityProfile existing)
                && !ProfileOwnershipMatches(existing, pair.Value)
                && occupied > 0L)
            {
                return Fail(
                    FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                    $"Facility-buffer capacity '{pair.Key}' cannot change ownership while {occupied}g remains.",
                    out failureCode,
                    out failureReason);
            }
            if (occupied > pair.Value.MaxMassGrams)
            {
                return Fail(
                    FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
                    $"Facility-buffer capacity '{pair.Key}' cannot shrink below {occupied}g.",
                    out failureCode,
                    out failureReason);
            }
            next[pair.Key] = pair.Value;
        }

        if (restoreActive)
            candidateProfiles = next;
        else
        {
            profiles = next;
            revision = checked(revision + 1L);
        }
        return true;
    }

    public bool TryReserveExactLot(
        FacilityBufferMassAdmissionRequest request,
        out FacilityBufferMassAdmissionToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
        => TryReserveExactLotCore(
            request,
            null,
            out token,
            out failureCode,
            out failureReason);

    public bool TryReserveCustodyOwnedExactLot(
        FacilityBufferCustodyOwnedAdmissionRequest request,
        out FacilityBufferMassAdmissionToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!IsCanonicalRequired(request.ExpectedRouteOperationId)
            || !IsCanonicalRequired(request.ExpectedPhysicalReceiptFingerprint)
            || request.ExpectedMassAuthorityRevision <= 0L
            || physicalOccupancy
                is not IFacilityBufferCustodyOwnedPhysicalOccupancyQuery)
        {
            token = default;
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer custody-owned admission request is invalid.",
                out failureCode,
                out failureReason);
        }
        return TryReserveExactLotCore(
            request.Admission,
            request,
            out token,
            out failureCode,
            out failureReason);
    }

    private bool TryReserveExactLotCore(
        FacilityBufferMassAdmissionRequest request,
        FacilityBufferCustodyOwnedAdmissionRequest? custodyOwned,
        out FacilityBufferMassAdmissionToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        token = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!TryValidateRequest(request, out failureCode, out failureReason))
            return false;
        if (!profiles.TryGetValue(
                request.DestinationId,
                out FacilityBufferCapacityProfile profile))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileMissing,
                $"Facility-buffer capacity '{request.DestinationId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (!TryValidateProfile(
                profile,
                profile.OwnerDomain,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (IsOwnerMutationFenceOpen(profile, out string mutationOperationId))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode
                    .OwnerMutationFenceOpen,
                "Facility-buffer owner is fenced by mutation operation '"
                + mutationOperationId + "'.",
                out failureCode,
                out failureReason);
        }
        if (!ProfileMatchesRequest(profile, request))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                $"Facility-buffer capacity '{request.DestinationId}' changed before admission.",
                out failureCode,
                out failureReason);
        }
        FacilityBufferExactLotSnapshot exactLot;
        string lotFailure;
        bool captured = custodyOwned.HasValue
            ? ((IFacilityBufferCustodyOwnedPhysicalOccupancyQuery)physicalOccupancy)
                .TryCaptureCustodyOwnedExactLot(
                    request.ExactLotSlices,
                    request.DestinationId,
                    custodyOwned.Value.ExpectedRouteOperationId,
                    custodyOwned.Value.ExpectedPhysicalReceiptFingerprint,
                    custodyOwned.Value.ExpectedMassAuthorityRevision,
                    out exactLot,
                    out lotFailure)
            : physicalOccupancy.TryCaptureExactLot(
                request.ExactLotSlices,
                out exactLot,
                out lotFailure);
        if (!captured)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                lotFailure,
                out failureCode,
                out failureReason);
        }
        if (HasOperationId(request.TransferOperationId))
        {
            TokenState existing = tokens.Values.SingleOrDefault(value =>
                string.Equals(
                    value.Token.Request.TransferOperationId,
                    request.TransferOperationId,
                    StringComparison.Ordinal));
            if (custodyOwned.HasValue
                && existing != null
                && existing.Status == FacilityBufferMassAdmissionTokenStatus.Reserved
                && AdmissionRequestsMatch(existing.Token.Request, request)
                && string.Equals(
                    existing.Token.ExactLot.Fingerprint,
                    exactLot.Fingerprint,
                    StringComparison.Ordinal)
                && existing.Token.ReservedMassGrams == exactLot.Mass.Value)
            {
                token = existing.Token;
                return true;
            }
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer transfer '{request.TransferOperationId}' is duplicated or conflicts.",
                out failureCode,
                out failureReason);
        }
        FacilityBufferPhysicalOccupancySnapshot physical =
            physicalOccupancy.Capture(request.DestinationId);
        long alreadyReserved = reservedByDestination.GetValueOrDefault(
            request.DestinationId,
            0L);
        long occupied;
        try
        {
            occupied = checked(
                physical.NonCarriedMassGrams
                + physical.CommittedCarriedMassGrams
                + alreadyReserved);
        }
        catch (OverflowException)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer observed occupancy overflowed.",
                out failureCode,
                out failureReason);
        }
        if (occupied > profile.MaxMassGrams
            || exactLot.Mass.Value > profile.MaxMassGrams - occupied)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
                $"Facility-buffer '{request.DestinationId}' cannot admit "
                + $"{exactLot.Mass.Value}g at {occupied}/{profile.MaxMassGrams}g.",
                out failureCode,
                out failureReason);
        }

        string tokenId = $"facility-buffer-admission:{nextTokenSequence:D12}";
        nextTokenSequence = checked(nextTokenSequence + 1L);
        token = new FacilityBufferMassAdmissionToken(
            tokenId,
            request,
            exactLot,
            revision);
        tokens.Add(tokenId, new TokenState
        {
            Token = token,
            Status = FacilityBufferMassAdmissionTokenStatus.Reserved
        });
        reservedByDestination[request.DestinationId] = checked(
            alreadyReserved + exactLot.Mass.Value);
        return true;
    }

    public bool TryCommitRouted(
        FacilityBufferMassAdmissionToken token,
        string exactLotFingerprint,
        long routedMassGrams,
        out FacilityBufferMassAdmissionReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        receipt = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!tokens.TryGetValue(token.TokenId ?? string.Empty, out TokenState state))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer admission token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Routed)
        {
            receipt = state.Receipt;
            if (receipt.CommittedMassGrams == routedMassGrams
                && string.Equals(
                    receipt.ExactLotFingerprint,
                    exactLotFingerprint,
                    StringComparison.Ordinal))
            {
                return true;
            }
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer admission token '{token.TokenId}' replay conflicts.",
                out failureCode,
                out failureReason);
        }
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer admission token '{token.TokenId}' is not reserved.",
                out failureCode,
                out failureReason);
        }
        if (!TokenMatches(state.Token, token)
            || routedMassGrams != token.ReservedMassGrams
            || !string.Equals(
                token.ExactLot.Fingerprint,
                exactLotFingerprint,
                StringComparison.Ordinal))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer admission token '{token.TokenId}' commit mismatched.",
                out failureCode,
                out failureReason);
        }

        RemoveReservedMass(token.Request.DestinationId, token.ReservedMassGrams);
        state.Status = FacilityBufferMassAdmissionTokenStatus.Routed;
        state.Receipt = new FacilityBufferMassAdmissionReceipt(token);
        receipt = state.Receipt;
        TrackTerminalToken(token.TokenId);
        return true;
    }

    public bool TryRelease(
        FacilityBufferMassAdmissionToken token,
        FacilityBufferMassAdmissionReleaseReason reason,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!Enum.IsDefined(typeof(FacilityBufferMassAdmissionReleaseReason), reason)
            || !tokens.TryGetValue(token.TokenId ?? string.Empty, out TokenState state))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer admission token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Released)
            return state.ReleaseReason == reason;
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved
            || !TokenMatches(state.Token, token))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer admission token '{token.TokenId}' cannot be released.",
                out failureCode,
                out failureReason);
        }

        RemoveReservedMass(token.Request.DestinationId, token.ReservedMassGrams);
        state.Status = FacilityBufferMassAdmissionTokenStatus.Released;
        state.ReleaseReason = reason;
        TrackTerminalToken(token.TokenId);
        return true;
    }

    public bool TryRollbackRouted(
        FacilityBufferMassAdmissionToken token,
        FacilityBufferMassAdmissionReceipt expectedReceipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!tokens.TryGetValue(token.TokenId ?? string.Empty, out TokenState state)
            || state.Status != FacilityBufferMassAdmissionTokenStatus.Routed)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer routed token '{token.TokenId}' cannot roll back.",
                out failureCode,
                out failureReason);
        }
        if (!TokenMatches(state.Token, token)
            || expectedReceipt.TokenId != state.Receipt.TokenId
            || expectedReceipt.CommittedMassGrams
                != state.Receipt.CommittedMassGrams
            || !string.Equals(
                expectedReceipt.ExactLotFingerprint,
                state.Receipt.ExactLotFingerprint,
                StringComparison.Ordinal))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer routed token '{token.TokenId}' rollback mismatched.",
                out failureCode,
                out failureReason);
        }
        tokens.Remove(token.TokenId);
        return true;
    }

    public bool TryReservePlannedOutput(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        token = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (plannedOutputMassQuery == null)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer planned output requires the physical-mass authority.",
                out failureCode,
                out failureReason);
        }
        if (!TryValidatePlannedOutputRequest(
                request,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (!profiles.TryGetValue(
                request.DestinationId,
                out FacilityBufferCapacityProfile profile))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileMissing,
                $"Facility-buffer capacity '{request.DestinationId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (!TryValidateProfile(
                profile,
                profile.OwnerDomain,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (IsOwnerMutationFenceOpen(profile, out string mutationOperationId))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode
                    .OwnerMutationFenceOpen,
                "Facility-buffer owner is fenced by mutation operation '"
                + mutationOperationId + "'.",
                out failureCode,
                out failureReason);
        }
        if (!ProfileMatchesPlannedOutputRequest(profile, request))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.ProfileConflict,
                $"Facility-buffer capacity '{request.DestinationId}' changed before planned output admission.",
                out failureCode,
                out failureReason);
        }
        if (HasOperationId(request.PublicationOperationId))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer operation '{request.PublicationOperationId}' is duplicated.",
                out failureCode,
                out failureReason);
        }
        long massAuthorityRevision = plannedOutputMassQuery.AuthorityRevision;
        if (!TryCapturePlannedOutput(
                request,
                massAuthorityRevision,
                out FacilityBufferPlannedOutputSnapshot planned,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        FacilityBufferPhysicalOccupancySnapshot physical =
            physicalOccupancy.Capture(request.DestinationId);
        long alreadyReserved = reservedByDestination.GetValueOrDefault(
            request.DestinationId,
            0L);
        long occupied;
        try
        {
            occupied = checked(
                physical.NonCarriedMassGrams
                + physical.CommittedCarriedMassGrams
                + alreadyReserved);
        }
        catch (OverflowException)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer observed occupancy overflowed.",
                out failureCode,
                out failureReason);
        }
        if (occupied > profile.MaxMassGrams
            || planned.TotalMassGrams > profile.MaxMassGrams - occupied)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
                $"Facility-buffer '{request.DestinationId}' cannot admit planned output "
                + $"{planned.TotalMassGrams}g at {occupied}/{profile.MaxMassGrams}g.",
                out failureCode,
                out failureReason);
        }

        string tokenId =
            $"facility-buffer-planned-output-admission:{nextTokenSequence:D12}";
        nextTokenSequence = checked(nextTokenSequence + 1L);
        token = new FacilityBufferPlannedOutputToken(
            tokenId,
            request,
            planned,
            revision,
            massAuthorityRevision);
        plannedOutputTokens.Add(tokenId, new PlannedOutputTokenState
        {
            Token = token,
            Status = FacilityBufferMassAdmissionTokenStatus.Reserved
        });
        reservedByDestination[request.DestinationId] = checked(
            alreadyReserved + planned.TotalMassGrams);
        return true;
    }

    public bool TryCommitPlannedOutput(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out FacilityBufferPlannedOutputReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        receipt = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!plannedOutputTokens.TryGetValue(
                token.TokenId ?? string.Empty,
                out PlannedOutputTokenState state))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer planned-output token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Routed)
        {
            receipt = state.Receipt;
            if (PlannedOutputTokenMatches(state.Token, token)
                && PublicationReceiptsMatch(state.Publication, publication))
            {
                return true;
            }
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' replay conflicts.",
                out failureCode,
                out failureReason);
        }
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer planned-output token '{token.TokenId}' is not reserved.",
                out failureCode,
                out failureReason);
        }
        if (!PlannedOutputTokenMatches(state.Token, token))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' commit mismatched.",
                out failureCode,
                out failureReason);
        }
        if (plannedOutputMassQuery == null
            || token.MassAuthorityRevision
                != plannedOutputMassQuery.AuthorityRevision
            || !profiles.TryGetValue(
                token.Request.DestinationId,
                out FacilityBufferCapacityProfile profile))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' is stale.",
                out failureCode,
                out failureReason);
        }
        if (!TryValidateProfile(
                profile,
                profile.OwnerDomain,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (!ProfileMatchesPlannedOutputRequest(profile, token.Request))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' profile changed.",
                out failureCode,
                out failureReason);
        }
        if (!TryValidatePlannedOutputPublication(
                token,
                publication,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        RemoveReservedMass(
            token.Request.DestinationId,
            token.ReservedMassGrams);
        state.Status = FacilityBufferMassAdmissionTokenStatus.Routed;
        state.Publication = publication;
        state.Receipt = new FacilityBufferPlannedOutputReceipt(
            token,
            publication);
        receipt = state.Receipt;
        TrackPlannedTerminalToken(token.TokenId);
        return true;
    }

    public bool TryReleasePlannedOutput(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferMassAdmissionReleaseReason reason,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!Enum.IsDefined(typeof(FacilityBufferMassAdmissionReleaseReason), reason)
            || !plannedOutputTokens.TryGetValue(
                token.TokenId ?? string.Empty,
                out PlannedOutputTokenState state))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMissing,
                $"Facility-buffer planned-output token '{token.TokenId}' is missing.",
                out failureCode,
                out failureReason);
        }
        if (state.Status == FacilityBufferMassAdmissionTokenStatus.Released)
            return state.ReleaseReason == reason;
        if (state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved
            || !PlannedOutputTokenMatches(state.Token, token))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenNotReserved,
                $"Facility-buffer planned-output token '{token.TokenId}' cannot be released.",
                out failureCode,
                out failureReason);
        }

        RemoveReservedMass(
            token.Request.DestinationId,
            token.ReservedMassGrams);
        state.Status = FacilityBufferMassAdmissionTokenStatus.Released;
        state.ReleaseReason = reason;
        TrackPlannedTerminalToken(token.TokenId);
        return true;
    }

    public void BeginRestoreCandidate()
    {
        if (restoreActive)
        {
            throw new InvalidOperationException(
                "Facility-buffer mass-capacity restore is already active.");
        }
        if (reservedByDestination.Count != 0
            || tokens.Values.Any(value =>
                value.Status == FacilityBufferMassAdmissionTokenStatus.Reserved)
            || plannedOutputTokens.Values.Any(value =>
                value.Status == FacilityBufferMassAdmissionTokenStatus.Reserved))
        {
            throw new InvalidOperationException(
                "Facility-buffer mass-capacity restore cannot begin with an active admission token.");
        }

        // Prepare every allocation and checked value before exposing an active
        // restore candidate. Publish and rollback are transaction callbacks;
        // once Begin succeeds neither callback may discover arithmetic or map
        // construction failures while the aggregate restore is being swapped.
        Dictionary<string, FacilityBufferCapacityProfile> preparedPrevious =
            CopyProfiles(profiles);
        Dictionary<string, FacilityBufferCapacityProfile> preparedCandidate =
            CreateProfileMap();
        long nextPublishRevision = checked(revision + 1L);
        long nextRollbackRevision = checked(nextPublishRevision + 1L);

        previousProfiles = preparedPrevious;
        candidateProfiles = preparedCandidate;
        preparedPublishRevision = nextPublishRevision;
        preparedRollbackRevision = nextRollbackRevision;
        restoreActive = true;
        restorePublished = false;
    }

    public void PublishRestoreCandidate()
    {
        profiles = candidateProfiles;
        candidateProfiles = null;
        restorePublished = true;
        revision = preparedPublishRevision;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (!restoreActive)
            return;
        if (restorePublished && previousProfiles != null)
        {
            profiles = previousProfiles;
            revision = preparedRollbackRevision;
        }
        ResetRestoreState();
    }

    public void CompleteRestoreCandidate()
    {
        if (restorePublished)
        {
            tokens.Clear();
            terminalTokenOrder.Clear();
            plannedOutputTokens.Clear();
            plannedTerminalTokenOrder.Clear();
            reservedByDestination.Clear();
        }
        ResetRestoreState();
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublished)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        ResetRestoreState();
    }

    private static bool TryValidatePlannedOutputRequest(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        FacilityBufferPlannedOutputSlice[] slices = request.Slices.ToArray();
        bool productionCapacitySource = string.Equals(
            request.ExpectedOwnerDomain,
            "economy.production-output",
            StringComparison.Ordinal);
        if (!IsCanonicalRequired(request.PublicationOperationId)
            || !IsCanonicalRequired(request.BatchCommitId)
            || !IsCanonicalRequired(request.OutcomeFingerprint)
            || !IsCanonicalRequired(request.DestinationId)
            || !IsCanonicalRequired(request.ExpectedOwnerDomain)
            || !IsCanonicalRequired(request.ExpectedOwnerOperationId)
            || !IsCanonicalRequired(request.ExpectedOwnerFacilityId)
            || request.ExpectedCapacityRevision <= 0L
            || productionCapacitySource
                && (!IsLowercaseSha256(request.CapacitySourceDigest)
                    || request.ExpectedMinimumCapacityGrams <= 0L
                    || request.CapacityAuthorityDigest.Length != 0
                        && !IsLowercaseSha256(
                            request.CapacityAuthorityDigest))
            || !productionCapacitySource
                && (request.CapacitySourceDigest.Length != 0
                    || request.ExpectedMinimumCapacityGrams != 0L
                    || request.CapacityAuthorityDigest.Length != 0)
            || slices.Length == 0
            || slices.Any(slice =>
                !IsCanonicalRequired(slice.OutputLineId)
                || slice.Subject == null
                || !slice.ItemDefinitionId.IsValid
                || slice.Quantity <= 0
                || (slice.PreparedComponentFingerprint ?? string.Empty).Length > 0
                    && !IsLowercaseSha256(
                        slice.PreparedComponentFingerprint)
                || (slice.UniqueBindingCapabilityId ?? string.Empty).Length > 0
                    && !IsCanonicalRequired(slice.UniqueBindingCapabilityId))
            || slices.Select(slice => slice.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != slices.Length)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer planned-output request is invalid.",
                out failureCode,
                out failureReason);
        }

        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

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

    private bool TryCapturePlannedOutput(
        FacilityBufferPlannedOutputRequest request,
        long massAuthorityRevision,
        out FacilityBufferPlannedOutputSnapshot planned,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        planned = default;
        try
        {
            List<FacilityBufferPlannedOutputSliceSnapshot> snapshots = new();
            int totalQuantity = 0;
            long totalMassGrams = 0L;
            foreach (FacilityBufferPlannedOutputSlice slice in request.Slices
                         .OrderBy(value => value.OutputLineId, StringComparer.Ordinal))
            {
                IReadOnlyList<ItemInstanceComponentSaveData> runtimeComponents =
                    slice.MaterializeRuntimeComponents();
                PhysicalItemMassSubject reconstructed =
                    PhysicalItemMassSubjectAdapter.Create(
                        plannedOutputMassQuery,
                        slice.ItemDefinitionId,
                        slice.Subject.ItemInstanceId,
                        runtimeComponents);
                if (!PlannedSubjectsMatch(slice.Subject, reconstructed))
                {
                    return Fail(
                        FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                        $"Facility-buffer planned-output line '{slice.OutputLineId}' component subject mismatched.",
                        out failureCode,
                        out failureReason);
                }
                PhysicalMassGrams exactMass = plannedOutputMassQuery.GetQuantityMass(
                    slice.ItemDefinitionId,
                    reconstructed,
                    slice.Quantity);
                totalQuantity = checked(totalQuantity + slice.Quantity);
                totalMassGrams = checked(totalMassGrams + exactMass.Value);
                snapshots.Add(new FacilityBufferPlannedOutputSliceSnapshot(
                    slice,
                    exactMass));
            }
            if (totalQuantity <= 0 || totalMassGrams <= 0L)
            {
                return Fail(
                    FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                    "Facility-buffer planned output has no positive physical mass.",
                    out failureCode,
                    out failureReason);
            }
            if (plannedOutputMassQuery.AuthorityRevision
                != massAuthorityRevision)
            {
                return Fail(
                    FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                    "Facility-buffer planned-output mass authority changed during projection.",
                    out failureCode,
                    out failureReason);
            }

            string fingerprint = BuildPlannedOutputFingerprint(
                request,
                snapshots,
                totalQuantity,
                totalMassGrams,
                massAuthorityRevision);
            planned = new FacilityBufferPlannedOutputSnapshot(
                fingerprint,
                snapshots,
                totalQuantity,
                new PhysicalMassGrams(totalMassGrams));
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer planned-output mass projection failed: "
                + exception.Message,
                out failureCode,
                out failureReason);
        }
    }

    private bool TryValidatePlannedOutputPublication(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!string.Equals(
                publication.AdmissionTokenId,
                token.TokenId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.BatchCommitId,
                token.Request.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.OutcomeFingerprint,
                token.Request.OutcomeFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.DestinationId,
                token.Request.DestinationId,
                StringComparison.Ordinal)
            || publication.DropPosition != token.Request.DropPosition
            || !string.Equals(
                publication.OwnerDomain,
                token.Request.ExpectedOwnerDomain,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.OwnerOperationId,
                token.Request.ExpectedOwnerOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.OwnerFacilityId,
                token.Request.ExpectedOwnerFacilityId,
                StringComparison.Ordinal)
            || publication.CapacityRevision
                != token.Request.ExpectedCapacityRevision
            || !string.Equals(
                publication.PlannedOutputFingerprint,
                token.PlannedOutput.Fingerprint,
                StringComparison.Ordinal))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' publication ownership mismatched.",
                out failureCode,
                out failureReason);
        }

        FacilityBufferPublishedOutputStackReceipt[] published =
            publication.Stacks.ToArray();
        if (published.Length == 0
            || published.Any(stack => !IsCanonicalRequired(stack.StackId)
                || !IsCanonicalRequired(stack.OutputLineId)
                || !stack.ItemDefinitionId.IsValid
                || stack.Quantity <= 0
                || stack.MassGrams <= 0L)
            || published.Select(stack => stack.StackId)
                .Distinct(StringComparer.Ordinal).Count() != published.Length)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' publication receipt is invalid.",
                out failureCode,
                out failureReason);
        }

        Dictionary<string, FacilityBufferPlannedOutputSliceSnapshot> expected =
            token.PlannedOutput.Slices.ToDictionary(
                slice => slice.OutputLineId,
                StringComparer.Ordinal);
        if (published.Select(stack => stack.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != expected.Count
            || published.Any(stack => !expected.ContainsKey(stack.OutputLineId)))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                $"Facility-buffer planned-output token '{token.TokenId}' publication lines mismatched.",
                out failureCode,
                out failureReason);
        }

        try
        {
            int totalQuantity = 0;
            long totalMassGrams = 0L;
            HashSet<string> publishedItemInstanceIds = new(
                StringComparer.Ordinal);
            foreach (IGrouping<string, FacilityBufferPublishedOutputStackReceipt> group in
                     published.GroupBy(
                         stack => stack.OutputLineId,
                         StringComparer.Ordinal))
            {
                FacilityBufferPlannedOutputSliceSnapshot expectedLine =
                    expected[group.Key];
                int lineQuantity = 0;
                long lineMassGrams = 0L;
                foreach (FacilityBufferPublishedOutputStackReceipt stack in group)
                {
                    if (!stack.ItemDefinitionId.Equals(
                            expectedLine.ItemDefinitionId))
                    {
                        return Fail(
                            FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                            $"Facility-buffer planned-output line '{group.Key}' item mismatched.",
                            out failureCode,
                            out failureReason);
                    }
                    string expectedInstanceId =
                        expectedLine.Source.Subject.ItemInstanceId
                        ?? string.Empty;
                    string publishedInstanceId = stack.ItemInstanceId
                        ?? string.Empty;
                    bool exactInstance = string.Equals(
                        publishedInstanceId,
                        expectedInstanceId,
                        StringComparison.Ordinal);
                    bool publicationAllocatedGenericInstance =
                        expectedInstanceId.Length == 0
                        && expectedLine.Source.Subject.Kind
                            == PhysicalItemMassSubjectKind.GenericDefinition
                        && stack.Quantity == 1
                        && publishedInstanceId.Length > 0
                        && ((ItemInstanceId)publishedInstanceId).IsValid
                        && string.Equals(
                            ((ItemInstanceId)publishedInstanceId).Value,
                            publishedInstanceId,
                            StringComparison.Ordinal);
                    if (!exactInstance && !publicationAllocatedGenericInstance)
                    {
                        return Fail(
                            FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                            $"Facility-buffer planned-output stack '{stack.StackId}' item instance mismatched.",
                            out failureCode,
                            out failureReason);
                    }
                    if (publishedInstanceId.Length > 0
                        && !publishedItemInstanceIds.Add(publishedInstanceId))
                    {
                        return Fail(
                            FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                            $"Facility-buffer planned-output item instance '{publishedInstanceId}' is duplicated.",
                            out failureCode,
                            out failureReason);
                    }
                    long exactStackMass = plannedOutputMassQuery.GetQuantityMass(
                        expectedLine.ItemDefinitionId,
                        expectedLine.Source.Subject,
                        stack.Quantity).Value;
                    if (stack.MassGrams != exactStackMass)
                    {
                        return Fail(
                            FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                            $"Facility-buffer planned-output stack '{stack.StackId}' mass mismatched.",
                            out failureCode,
                            out failureReason);
                    }
                    lineQuantity = checked(lineQuantity + stack.Quantity);
                    lineMassGrams = checked(lineMassGrams + stack.MassGrams);
                }
                if (lineQuantity != expectedLine.Quantity
                    || lineMassGrams != expectedLine.ExactMassGrams)
                {
                    return Fail(
                        FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                        $"Facility-buffer planned-output line '{group.Key}' totals mismatched.",
                        out failureCode,
                        out failureReason);
                }
                totalQuantity = checked(totalQuantity + lineQuantity);
                totalMassGrams = checked(totalMassGrams + lineMassGrams);
            }
            if (totalQuantity != token.PlannedOutput.TotalQuantity
                || totalMassGrams != token.PlannedOutput.TotalMassGrams)
            {
                return Fail(
                    FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                    $"Facility-buffer planned-output token '{token.TokenId}' batch totals mismatched.",
                    out failureCode,
                    out failureReason);
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.TokenMismatch,
                "Facility-buffer planned-output publication validation failed: "
                + exception.Message,
                out failureCode,
                out failureReason);
        }

        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    private bool TryValidateProfile(
        FacilityBufferCapacityProfile profile,
        string expectedOwnerDomain,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (profile == null
            || !IsCanonicalRequired(profile.DestinationId)
            || !IsCanonicalRequired(profile.OwnerDomain)
            || !IsCanonicalRequired(profile.OwnerOperationId)
            || profile.OwnerFacilityId != null
                && !IsCanonicalRequired(profile.OwnerFacilityId)
            || profile.MaxMassGrams <= 0L
            || profile.CapacityRevision <= 0L
            || !string.Equals(
                profile.OwnerDomain,
                expectedOwnerDomain,
                StringComparison.Ordinal))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidProfile,
                "Facility-buffer capacity profile is invalid.",
                out failureCode,
                out failureReason);
        }
        if (!destinationClaims.TryGetAuthorityClaim(
                profile.DestinationId,
                profile.DropPosition,
                out FacilityBufferDestinationClaim claim)
            || claim.AnchorKind != FacilityBufferDestinationAnchorKind.ReservedTarget
                && !IsCanonicalRequired(profile.OwnerFacilityId)
            || !string.Equals(
                claim.OwnerDomain,
                profile.OwnerDomain,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.OwnerOperationId,
                profile.OwnerOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                claim.OwnerFacilityId,
                profile.OwnerFacilityId,
                StringComparison.Ordinal))
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.ClaimMissingOrMismatched,
                $"Facility-buffer capacity '{profile.DestinationId}' has no exact matching claim.",
                out failureCode,
                out failureReason);
        }
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    private static bool TryValidateRequest(
        FacilityBufferMassAdmissionRequest request,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        if (!IsCanonicalRequired(request.TransferOperationId)
            || !IsCanonicalRequired(request.DestinationId)
            || !IsCanonicalRequired(request.ExpectedOwnerDomain)
            || !IsCanonicalRequired(request.ExpectedOwnerOperationId)
            || request.ExpectedOwnerFacilityId != null
                && !IsCanonicalRequired(request.ExpectedOwnerFacilityId)
            || request.ExpectedCapacityRevision <= 0L
            || request.ExactLotSlices == null
            || request.ExactLotSlices.Count == 0)
        {
            return Fail(
                FacilityBufferMassAdmissionFailureCode.InvalidRequest,
                "Facility-buffer mass admission request is invalid.",
                out failureCode,
                out failureReason);
        }
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        return true;
    }

    private static bool ProfileMatchesRequest(
        FacilityBufferCapacityProfile profile,
        FacilityBufferMassAdmissionRequest request) =>
        profile.DropPosition == request.DropPosition
        && profile.CapacityRevision == request.ExpectedCapacityRevision
        && string.Equals(
            profile.OwnerDomain,
            request.ExpectedOwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerOperationId,
            request.ExpectedOwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerFacilityId,
            request.ExpectedOwnerFacilityId,
            StringComparison.Ordinal);

    private static bool ProfileMatchesPlannedOutputRequest(
        FacilityBufferCapacityProfile profile,
        FacilityBufferPlannedOutputRequest request) =>
        profile.DropPosition == request.DropPosition
        && profile.CapacityRevision == request.ExpectedCapacityRevision
        && profile.MaxMassGrams >= request.ExpectedMinimumCapacityGrams
        && string.Equals(
            profile.AuthorityDigest,
            request.CapacityAuthorityDigest,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerDomain,
            request.ExpectedOwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerOperationId,
            request.ExpectedOwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerFacilityId,
            request.ExpectedOwnerFacilityId,
            StringComparison.Ordinal);

    private static bool ProfileOwnershipMatches(
        FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) =>
        left != null
        && right != null
        && left.DropPosition == right.DropPosition
        && string.Equals(left.OwnerDomain, right.OwnerDomain, StringComparison.Ordinal)
        && string.Equals(
            left.OwnerOperationId,
            right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.OwnerFacilityId,
            right.OwnerFacilityId,
            StringComparison.Ordinal)
        && string.Equals(
            left.AuthorityDigest,
            right.AuthorityDigest,
            StringComparison.Ordinal);

    private static bool ProfilesMatch(
        FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) =>
        left != null
        && right != null
        && left.DropPosition == right.DropPosition
        && left.MaxMassGrams == right.MaxMassGrams
        && left.CapacityRevision == right.CapacityRevision
        && string.Equals(
            left.DestinationId,
            right.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.OwnerDomain,
            right.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            left.OwnerOperationId,
            right.OwnerOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.OwnerFacilityId,
            right.OwnerFacilityId,
            StringComparison.Ordinal)
        && string.Equals(
            left.AuthorityDigest,
            right.AuthorityDigest,
            StringComparison.Ordinal);

    private static bool OwnedProfileSetMatches(
        IReadOnlyDictionary<string, FacilityBufferCapacityProfile> current,
        string ownerDomain,
        IReadOnlyDictionary<string, FacilityBufferCapacityProfile> desired)
    {
        if (current == null || desired == null)
            return false;
        int ownedCount = 0;
        foreach (FacilityBufferCapacityProfile profile in current.Values)
        {
            if (profile != null && string.Equals(
                    profile.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
            {
                ownedCount++;
            }
        }
        if (ownedCount != desired.Count)
            return false;
        foreach (KeyValuePair<string, FacilityBufferCapacityProfile> pair in desired)
        {
            if (!current.TryGetValue(
                    pair.Key,
                    out FacilityBufferCapacityProfile candidate)
                || !ProfilesMatch(candidate, pair.Value))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TokenMatches(
        FacilityBufferMassAdmissionToken left,
        FacilityBufferMassAdmissionToken right) =>
        left.ProfileRevision == right.ProfileRevision
        && left.ReservedMassGrams == right.ReservedMassGrams
        && string.Equals(left.TokenId, right.TokenId, StringComparison.Ordinal)
        && string.Equals(
            left.Request.TransferOperationId,
            right.Request.TransferOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.ExactLot.Fingerprint,
            right.ExactLot.Fingerprint,
            StringComparison.Ordinal);

    private static bool AdmissionRequestsMatch(
        FacilityBufferMassAdmissionRequest left,
        FacilityBufferMassAdmissionRequest right) =>
        string.Equals(left.TransferOperationId, right.TransferOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.DestinationId, right.DestinationId,
            StringComparison.Ordinal)
        && left.DropPosition == right.DropPosition
        && left.ExpectedCapacityRevision == right.ExpectedCapacityRevision
        && string.Equals(left.ExpectedOwnerDomain, right.ExpectedOwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(left.ExpectedOwnerOperationId,
            right.ExpectedOwnerOperationId, StringComparison.Ordinal)
        && string.Equals(left.ExpectedOwnerFacilityId,
            right.ExpectedOwnerFacilityId, StringComparison.Ordinal)
        && left.ExactLotSlices.Count == right.ExactLotSlices.Count
        && left.ExactLotSlices.OrderBy(value => value.StackId,
                StringComparer.Ordinal)
            .Zip(right.ExactLotSlices.OrderBy(value => value.StackId,
                    StringComparer.Ordinal),
                (a, b) => a.StackId == b.StackId
                    && a.Quantity == b.Quantity
                    && a.ExpectedReservationRevision
                        == b.ExpectedReservationRevision
                    && a.ExpectedCustodyMassGrams
                        == b.ExpectedCustodyMassGrams
                    && string.Equals(a.ExpectedCustodyComponentFingerprint,
                        b.ExpectedCustodyComponentFingerprint,
                        StringComparison.Ordinal))
            .All(value => value);

    private static bool PlannedOutputTokenMatches(
        FacilityBufferPlannedOutputToken left,
        FacilityBufferPlannedOutputToken right) =>
        left.CapacityAuthorityRevision == right.CapacityAuthorityRevision
        && left.MassAuthorityRevision == right.MassAuthorityRevision
        && left.ReservedMassGrams == right.ReservedMassGrams
        && string.Equals(left.TokenId, right.TokenId, StringComparison.Ordinal)
        && string.Equals(
            left.Request.PublicationOperationId,
            right.Request.PublicationOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.Request.BatchCommitId,
            right.Request.BatchCommitId,
            StringComparison.Ordinal)
        && string.Equals(
            left.Request.OutcomeFingerprint,
            right.Request.OutcomeFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            left.Request.DestinationId,
            right.Request.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.PlannedOutput.Fingerprint,
            right.PlannedOutput.Fingerprint,
            StringComparison.Ordinal);

    private static bool PublicationReceiptsMatch(
        FacilityBufferPlannedOutputPublicationReceipt left,
        FacilityBufferPlannedOutputPublicationReceipt right)
    {
        if (!string.Equals(
                left.AdmissionTokenId,
                right.AdmissionTokenId,
                StringComparison.Ordinal)
            || !string.Equals(
                left.BatchCommitId,
                right.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                left.OutcomeFingerprint,
                right.OutcomeFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                left.DestinationId,
                right.DestinationId,
                StringComparison.Ordinal)
            || left.DropPosition != right.DropPosition
            || !string.Equals(
                left.OwnerDomain,
                right.OwnerDomain,
                StringComparison.Ordinal)
            || !string.Equals(
                left.OwnerOperationId,
                right.OwnerOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                left.OwnerFacilityId,
                right.OwnerFacilityId,
                StringComparison.Ordinal)
            || left.CapacityRevision != right.CapacityRevision
            || !string.Equals(
                left.PlannedOutputFingerprint,
                right.PlannedOutputFingerprint,
                StringComparison.Ordinal)
            || left.Stacks.Count != right.Stacks.Count)
        {
            return false;
        }

        FacilityBufferPublishedOutputStackReceipt[] leftStacks = left.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferPublishedOutputStackReceipt[] rightStacks = right.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < leftStacks.Length; index++)
        {
            FacilityBufferPublishedOutputStackReceipt a = leftStacks[index];
            FacilityBufferPublishedOutputStackReceipt b = rightStacks[index];
            if (!string.Equals(a.StackId, b.StackId, StringComparison.Ordinal)
                || !string.Equals(
                    a.OutputLineId,
                    b.OutputLineId,
                    StringComparison.Ordinal)
                || !a.ItemDefinitionId.Equals(b.ItemDefinitionId)
                || a.Quantity != b.Quantity
                || a.MassGrams != b.MassGrams
                || !string.Equals(
                    a.ItemInstanceId,
                    b.ItemInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private bool HasOperationId(string operationId) => tokens.Values.Any(value =>
            string.Equals(
                value.Token.Request.TransferOperationId,
                operationId,
                StringComparison.Ordinal))
        || plannedOutputTokens.Values.Any(value => string.Equals(
            value.Token.Request.PublicationOperationId,
            operationId,
            StringComparison.Ordinal));

    private static string BuildPlannedOutputFingerprint(
        FacilityBufferPlannedOutputRequest request,
        IReadOnlyList<FacilityBufferPlannedOutputSliceSnapshot> slices,
        int totalQuantity,
        long totalMassGrams,
        long massAuthorityRevision)
    {
        StringBuilder canonical = new();
        AppendFingerprintValue(canonical, "facility-buffer-planned-output-v5");
        AppendFingerprintValue(canonical, request.PublicationOperationId);
        AppendFingerprintValue(canonical, request.BatchCommitId);
        AppendFingerprintValue(canonical, request.OutcomeFingerprint);
        AppendFingerprintValue(canonical, request.DestinationId);
        AppendFingerprintValue(canonical, request.DropPosition.x.ToString(
            CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, request.DropPosition.y.ToString(
            CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, request.ExpectedOwnerDomain);
        AppendFingerprintValue(canonical, request.ExpectedOwnerOperationId);
        AppendFingerprintValue(canonical, request.ExpectedOwnerFacilityId);
        AppendFingerprintValue(canonical, request.ExpectedCapacityRevision.ToString(
            CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, request.CapacitySourceDigest);
        AppendFingerprintValue(canonical, request.CapacityAuthorityDigest);
        AppendFingerprintValue(canonical, request.ExpectedMinimumCapacityGrams
            .ToString(CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, massAuthorityRevision.ToString(
            CultureInfo.InvariantCulture));

        foreach (FacilityBufferPlannedOutputSliceSnapshot slice in slices
                     .OrderBy(value => value.OutputLineId, StringComparer.Ordinal))
        {
            PhysicalItemMassSubject subject = slice.Source.Subject;
            AppendFingerprintValue(canonical, slice.OutputLineId);
            AppendFingerprintValue(canonical, slice.ItemDefinitionId.Value);
            AppendFingerprintValue(canonical, ((int)subject.Kind).ToString(
                CultureInfo.InvariantCulture));
            AppendFingerprintValue(canonical, subject.ItemInstanceId);
            AppendFingerprintValue(canonical, subject.ComponentFingerprint);
            AppendFingerprintValue(
                canonical,
                slice.Source.PreparedComponentFingerprint);
            AppendFingerprintValue(
                canonical,
                slice.Source.UniqueBindingCapabilityId);
            AppendFingerprintValue(canonical, slice.Quantity.ToString(
                CultureInfo.InvariantCulture));
            AppendFingerprintValue(canonical, slice.ExactMassGrams.ToString(
                CultureInfo.InvariantCulture));
            foreach (PhysicalItemComponentSnapshot component in subject.Components
                         .OrderBy(value => value.ComponentTypeId, StringComparer.Ordinal)
                         .ThenBy(value => value.SchemaVersion)
                         .ThenBy(value => value.Fingerprint, StringComparer.Ordinal))
            {
                AppendFingerprintValue(canonical, component.ComponentTypeId);
                AppendFingerprintValue(canonical, component.SchemaVersion.ToString(
                    CultureInfo.InvariantCulture));
                AppendFingerprintValue(canonical, component.CanonicalPayload);
                AppendFingerprintValue(canonical, component.Fingerprint);
                AppendFingerprintValue(
                    canonical,
                    component.PreparedUnitMass.HasValue
                        ? component.PreparedUnitMass.Value.Value.ToString(
                            CultureInfo.InvariantCulture)
                        : string.Empty);
                foreach (PhysicalItemMassContribution contribution in
                         component.MassContributions
                             .OrderBy(value => value.ItemId.Value, StringComparer.Ordinal)
                             .ThenBy(value => value.Quantity))
                {
                    AppendFingerprintValue(canonical, contribution.ItemId.Value);
                    AppendFingerprintValue(canonical, contribution.Quantity.ToString(
                        CultureInfo.InvariantCulture));
                }
            }
            foreach (FacilityBufferPlannedOutputComponentSnapshot component in
                     slice.Source.RuntimeComponents
                         .OrderBy(value => value.ComponentTypeId, StringComparer.Ordinal)
                         .ThenBy(value => value.SchemaVersion)
                         .ThenBy(value => value.CanonicalFingerprint, StringComparer.Ordinal))
            {
                AppendFingerprintValue(canonical, component.ComponentTypeId);
                AppendFingerprintValue(canonical, component.SchemaVersion.ToString(
                    CultureInfo.InvariantCulture));
                AppendFingerprintValue(canonical, component.AffectsStacking ? "1" : "0");
                AppendFingerprintValue(canonical, component.CanonicalFingerprint);
            }
        }
        AppendFingerprintValue(canonical, totalQuantity.ToString(
            CultureInfo.InvariantCulture));
        AppendFingerprintValue(canonical, totalMassGrams.ToString(
            CultureInfo.InvariantCulture));

        using SHA256 sha256 = SHA256.Create();
        byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(
            canonical.ToString()));
        StringBuilder hex = new(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    private static bool PlannedSubjectsMatch(
        PhysicalItemMassSubject left,
        PhysicalItemMassSubject right) => left != null
        && right != null
        && left.ItemId.Equals(right.ItemId)
        && string.Equals(left.ItemInstanceId, right.ItemInstanceId, StringComparison.Ordinal)
        && left.Kind == right.Kind
        && string.Equals(
            left.ComponentFingerprint,
            right.ComponentFingerprint,
            StringComparison.Ordinal);

    private static void AppendFingerprintValue(
        StringBuilder target,
        string value)
    {
        string canonical = value ?? string.Empty;
        target.Append(canonical.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(canonical);
        target.Append('|');
    }

    private void TrackTerminalToken(string tokenId)
    {
        terminalTokenOrder.Enqueue(tokenId);
        while (terminalTokenOrder.Count > MaximumTerminalTokenCount)
        {
            string oldest = terminalTokenOrder.Dequeue();
            if (tokens.TryGetValue(oldest, out TokenState state)
                && state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
            {
                tokens.Remove(oldest);
            }
        }
    }

    private void TrackPlannedTerminalToken(string tokenId)
    {
        plannedTerminalTokenOrder.Enqueue(tokenId);
        while (plannedTerminalTokenOrder.Count > MaximumTerminalTokenCount)
        {
            string oldest = plannedTerminalTokenOrder.Dequeue();
            if (plannedOutputTokens.TryGetValue(
                    oldest,
                    out PlannedOutputTokenState state)
                && state.Status != FacilityBufferMassAdmissionTokenStatus.Reserved)
            {
                plannedOutputTokens.Remove(oldest);
            }
        }
    }

    private bool TryGetMutationTarget(
        out Dictionary<string, FacilityBufferCapacityProfile> target,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        target = restoreActive ? candidateProfiles : profiles;
        if (!restoreActive || (!restorePublished && candidateProfiles != null))
        {
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }

        target = null;
        return Fail(
            FacilityBufferMassAdmissionFailureCode.RestoreMutationAfterPublish,
            "Facility-buffer mass-capacity profiles cannot mutate after restore publication.",
            out failureCode,
            out failureReason);
    }

    private IReadOnlyDictionary<string, FacilityBufferCapacityProfile>
        GetAuthorityView() =>
        restoreActive && !restorePublished && candidateProfiles != null
            ? candidateProfiles
            : profiles;

    private static Dictionary<string, FacilityBufferCapacityProfile>
        CreateProfileMap() => new(StringComparer.Ordinal);

    private static Dictionary<string, FacilityBufferCapacityProfile> CopyProfiles(
        IReadOnlyDictionary<string, FacilityBufferCapacityProfile> source) =>
        source == null
            ? CreateProfileMap()
            : new Dictionary<string, FacilityBufferCapacityProfile>(
                source,
                StringComparer.Ordinal);

    private void ResetRestoreState()
    {
        candidateProfiles = null;
        previousProfiles = null;
        preparedPublishRevision = 0L;
        preparedRollbackRevision = 0L;
        restoreActive = false;
        restorePublished = false;
    }

    private void RemoveReservedMass(string destinationId, long massGrams)
    {
        long current = reservedByDestination.GetValueOrDefault(destinationId, 0L);
        if (current < massGrams)
        {
            throw new InvalidOperationException(
                $"Facility-buffer reserved mass underflow for '{destinationId}'.");
        }
        long next = current - massGrams;
        if (next == 0L)
            reservedByDestination.Remove(destinationId);
        else
            reservedByDestination[destinationId] = next;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private bool IsOwnerMutationFenceOpen(
        FacilityBufferCapacityProfile profile,
        out string operationId)
    {
        operationId = string.Empty;
        if (admissionFences == null || profile == null)
        {
            return false;
        }

        FacilityBufferDestinationAdmissionFenceSubject subject = new(
            profile.DestinationId,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId);
        if (!admissionFences.TryCaptureOpenFence(
                subject,
                out FacilityBufferDestinationAdmissionFenceSnapshot pending))
        {
            return false;
        }
        operationId = pending.OperationId;
        return true;
    }

    public bool TryProjectPlannedOutput(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputSnapshot planned,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        planned = default;
        long revision = plannedOutputMassQuery.AuthorityRevision;
        return TryValidatePlannedOutputRequest(
                request,
                out failureCode,
                out failureReason)
            && TryCapturePlannedOutput(
                request,
                revision,
                out planned,
                out failureCode,
                out failureReason);
    }

    private static bool Fail(
        FacilityBufferMassAdmissionFailureCode code,
        string reason,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        failureCode = code;
        failureReason = reason ?? string.Empty;
        return false;
    }
}
