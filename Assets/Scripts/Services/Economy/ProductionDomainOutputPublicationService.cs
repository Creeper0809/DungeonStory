using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public enum ProductionDomainOutputPublicationStatus
{
    CommittedAwaitingOwnerAcknowledgement = 0,
    WaitingForOutputSpace = 1,
    Pending = 2,
    Conflict = 3
}

public readonly struct ProductionDomainOutputPublicationResult
{
    public ProductionDomainOutputPublicationResult(
        ProductionDomainOutputPublicationStatus status,
        string failureReason)
    {
        Status = status;
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionDomainOutputPublicationStatus Status { get; }
    public string FailureReason { get; }
    public bool IsCommitted =>
        Status == ProductionDomainOutputPublicationStatus
            .CommittedAwaitingOwnerAcknowledgement;
}

public sealed class ProductionDomainOutputLine
{
    private readonly IReadOnlyList<ItemInstanceComponentSaveData> components;

    public ProductionDomainOutputLine(
        string outputLineId,
        string itemId,
        int quantity,
        string itemInstanceId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        ProductionOutputCapabilityDescriptor outputCapability)
    {
        OutputLineId = outputLineId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        Quantity = quantity;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        OutputCapability = outputCapability;
        this.components = Array.AsReadOnly((components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Select(value => value?.Clone()
                ?? throw new ArgumentException(
                    "Domain output line contains a null component.",
                    nameof(components)))
            .ToArray());
    }

    public string OutputLineId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public string ItemInstanceId { get; }
    public ProductionOutputCapabilityDescriptor OutputCapability { get; }
    public IReadOnlyList<ItemInstanceComponentSaveData> Components => components;
}

public sealed class ProductionDomainOutputPublicationPlan
{
    private readonly IReadOnlyList<ProductionDomainOutputLine> lines;
    private readonly IReadOnlyList<ProductionDomainOutputMaximumMassClaim>
        maximumMassClaims;

    public ProductionDomainOutputPublicationPlan(
        string publicationOperationPrefix,
        string ownerId,
        string batchCommitId,
        string outcomeFingerprint,
        object facilityRuntimeObject,
        IReadOnlyList<ProductionDomainOutputLine> lines,
        FacilityBufferAcknowledgedOutputReleaseTarget releaseTarget = default,
        ProductionDomainOutputAcknowledgementDisposition
            acknowledgementDisposition =
                ProductionDomainOutputAcknowledgementDisposition
                    .ReleaseLooseOrDestination,
        IReadOnlyList<ProductionDomainOutputMaximumMassClaim>
            maximumMassClaims = null)
    {
        PublicationOperationPrefix = publicationOperationPrefix ?? string.Empty;
        OwnerId = ownerId ?? string.Empty;
        BatchCommitId = batchCommitId ?? string.Empty;
        OutcomeFingerprint = outcomeFingerprint ?? string.Empty;
        FacilityRuntimeObject = facilityRuntimeObject;
        ReleaseTarget = releaseTarget;
        AcknowledgementDisposition = acknowledgementDisposition;
        this.lines = Array.AsReadOnly((lines
                ?? throw new ArgumentNullException(nameof(lines)))
            .ToArray());
        this.maximumMassClaims = Array.AsReadOnly((maximumMassClaims
                ?? this.lines.Select(value =>
                    new ProductionDomainOutputMaximumMassClaim(
                        value.OutputCapability,
                        value.Quantity)))
            .ToArray());
    }

    public string PublicationOperationPrefix { get; }
    public string OwnerId { get; }
    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public object FacilityRuntimeObject { get; }
    public FacilityBufferAcknowledgedOutputReleaseTarget ReleaseTarget { get; }
    public ProductionDomainOutputAcknowledgementDisposition
        AcknowledgementDisposition { get; }
    public IReadOnlyList<ProductionDomainOutputLine> Lines => lines;
    public IReadOnlyList<ProductionDomainOutputMaximumMassClaim>
        MaximumMassClaims => maximumMassClaims;
}

public interface IProductionDomainOutputPublicationService
{
    ProductionDomainOutputPublicationResult EnsureCommitted(
        ProductionDomainOutputPublicationSaveData owner,
        ProductionDomainOutputPublicationPlan plan);

    bool TryAcknowledge(
        ProductionDomainOutputPublicationSaveData owner,
        out string failureReason);
}

/// <summary>
/// Shared output transaction for domain-owned producers. It reserves the whole
/// resolved batch by authoritative grams, atomically publishes it, and commits
/// admission. The caller acknowledges its input receipt before asking this
/// service to acknowledge output provenance.
/// </summary>
public sealed class ProductionDomainOutputPublicationService :
    IProductionDomainOutputPublicationService
{
    private const string ComponentFingerprintSchema =
        "production-domain-output-components@1";

    private readonly IProductionFacilityHandleQuery facilities;
    private readonly IProductionOutputDestinationAuthorityRuntime destinations;
    private readonly IProductionOutputBufferCapacityProjector capacityProjector;
    private readonly IProductionOutputMaximumMassRegistry maximumMassRegistry;
    private readonly IFacilityBufferMassAdmissionService admission;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;
    private readonly IPhysicalItemMassQuery massQuery;

    public ProductionDomainOutputPublicationService(
        IProductionFacilityHandleQuery facilities,
        IProductionOutputDestinationAuthorityRuntime destinations,
        IProductionOutputBufferCapacityProjector capacityProjector,
        IProductionOutputMaximumMassRegistry maximumMassRegistry,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication,
        IPhysicalItemMassQuery massQuery)
    {
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.destinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
        this.capacityProjector = capacityProjector
            ?? throw new ArgumentNullException(nameof(capacityProjector));
        this.maximumMassRegistry = maximumMassRegistry
            ?? throw new ArgumentNullException(nameof(maximumMassRegistry));
        this.admission = admission
            ?? throw new ArgumentNullException(nameof(admission));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
    }

    public ProductionDomainOutputPublicationResult EnsureCommitted(
        ProductionDomainOutputPublicationSaveData owner,
        ProductionDomainOutputPublicationPlan plan)
    {
        if (owner == null)
            return Conflict("domain-output-owner-missing");
        if (owner.outputAcknowledged)
            return Conflict("domain-output-owner-already-acknowledged");
        if (!TryValidatePlan(plan, out string planFailure))
            return Conflict(planFailure);

        try
        {
            ProductionFacilityHandle handle = facilities.CaptureFacility(
                plan.FacilityRuntimeObject);
            if (handle == null || handle.IsDestroyed || !handle.InstanceId.IsValid)
                return Conflict("domain-output-facility-invalid");

            List<FacilityBufferPlannedOutputSlice> slices = new();
            Dictionary<string, ProductionDomainOutputMaximumMassClaim>
                maximumClaimByLine = plan.MaximumMassClaims.ToDictionary(
                    value => value.Descriptor.OutputLineId,
                    StringComparer.Ordinal);
            List<ProductionOutputMaximumMassProjection> maximumProjections =
                plan.MaximumMassClaims
                    .OrderBy(
                        value => value.Descriptor.OutputLineId,
                        StringComparer.Ordinal)
                    .Select(value => maximumMassRegistry.CaptureDeclared(
                        value.Descriptor,
                        value.MaximumQuantity))
                    .ToList();
            long outputMassGrams = 0L;
            foreach (ProductionDomainOutputLine line in plan.Lines)
            {
                ProductionDomainOutputMaximumMassClaim maximumClaim =
                    maximumClaimByLine[line.OutputLineId];
                ProductionOutputMaximumMassProjection maximumProjection =
                    maximumMassRegistry.CaptureDeclared(
                        maximumClaim.Descriptor,
                        maximumClaim.MaximumQuantity);
                if (!string.Equals(
                        maximumProjection.Descriptor.OutputLineId,
                        line.OutputLineId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        maximumProjection.Descriptor.ItemId,
                        line.ItemId,
                        StringComparison.Ordinal))
                {
                    return Conflict(
                        "domain-output-maximum-mass-descriptor-drift");
                }
                PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter
                    .Create(
                        massQuery,
                        (ItemDefinitionId)line.ItemId,
                        line.ItemInstanceId,
                        line.Components);
                long lineMass = massQuery.GetQuantityMass(
                        (ItemDefinitionId)line.ItemId,
                        subject,
                        line.Quantity)
                    .Value;
                if (lineMass <= 0L)
                    return Conflict("domain-output-line-mass-invalid");
                if (lineMass > maximumProjection.MaximumMassGrams)
                {
                    return Conflict(
                        "domain-output-line-mass-exceeds-capability-maximum");
                }
                outputMassGrams = checked(outputMassGrams + lineMass);
                slices.Add(new FacilityBufferPlannedOutputSlice(
                    line.OutputLineId,
                    subject,
                    line.Quantity,
                    line.Components,
                    CaptureComponentFingerprint(line.Components)));
            }

            ProductionOutputBatchMaximumMassProof maximumMassProof = new(
                maximumProjections);
            ProductionOutputBufferCapacitySourceSnapshot capacity =
                capacityProjector.CaptureSource(handle, maximumMassProof);
            if (!destinations.TryEnsureCapacitySource(
                    handle,
                    capacity,
                    out FacilityBufferCapacityProfile profile,
                    out string destinationFailure))
            {
                return Pending("domain-output-authority:" + destinationFailure);
            }

            if (!TryAdoptFrozenOwner(
                    owner,
                    plan,
                    capacity,
                    profile,
                    maximumMassProof,
                    outputMassGrams,
                    out string frozenFailure))
            {
                return Conflict(frozenFailure);
            }

            FacilityBufferPlannedOutputToken token;
            if (!TryGetOrReserveToken(
                    owner,
                    plan,
                    handle,
                    profile,
                    slices,
                    out token,
                    out FacilityBufferMassAdmissionFailureCode reserveCode,
                    out string reserveFailure))
            {
                return reserveCode ==
                        FacilityBufferMassAdmissionFailureCode.CapacityUnavailable
                    ? Waiting(reserveFailure)
                    : Pending(reserveFailure);
            }

            if (!publication.TryPublishFullBatch(
                    token,
                    out FacilityBufferPlannedOutputPublicationReceipt published,
                    out _,
                    out string publicationFailure))
            {
                ReleaseReserved(owner, token);
                return Pending("domain-output-publication:" + publicationFailure);
            }

            owner.outputPublished = true;
            owner.plannedOutputFingerprint = published.PlannedOutputFingerprint;
            if (!admission.TryCommitPlannedOutput(
                    token,
                    published,
                    out FacilityBufferPlannedOutputReceipt committed,
                    out _,
                    out string commitFailure))
            {
                if (!publication.TryRollbackPublishedBatch(
                        published,
                        out _,
                        out string rollbackFailure))
                {
                    return Conflict(
                        "domain-output-admission-rollback:"
                        + commitFailure + ":" + rollbackFailure);
                }
                owner.outputPublished = false;
                ReleaseReserved(owner, token);
                return Pending("domain-output-admission:" + commitFailure);
            }
            if (committed.CommittedMassGrams != owner.outputMassGrams)
                return Conflict("domain-output-admission-mass-drift");
            owner.admissionCommitted = true;
            owner.stacks = published.Stacks
                .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
                .ThenBy(value => value.StackId, StringComparer.Ordinal)
                .Select(value => new ProductionDomainPublishedStackSaveData
                {
                    outputLineId = value.OutputLineId,
                    itemId = value.ItemDefinitionId.Value,
                    itemInstanceId = value.ItemInstanceId,
                    stackId = value.StackId,
                    quantity = value.Quantity,
                    massGrams = value.MassGrams
                })
                .ToList();
            return new ProductionDomainOutputPublicationResult(
                ProductionDomainOutputPublicationStatus
                    .CommittedAwaitingOwnerAcknowledgement,
                string.Empty);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return Conflict("domain-output-exception:" + exception.Message);
        }
    }

    public bool TryAcknowledge(
        ProductionDomainOutputPublicationSaveData owner,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryValidateCommittedOwner(owner, out failureReason))
            return false;
        if (owner.outputAcknowledged)
            return true;
        FacilityBufferPlannedOutputPublicationReceipt receipt = new(
            owner.admissionTokenId,
            owner.batchCommitId,
            owner.outcomeFingerprint,
            owner.destinationId,
            new Vector2Int(owner.destinationX, owner.destinationY),
            owner.ownerDomain,
            owner.ownerOperationId,
            owner.ownerFacilityId,
            owner.capacityRevision,
            owner.plannedOutputFingerprint,
            owner.stacks.Select(value =>
                new FacilityBufferPublishedOutputStackReceipt(
                    value.stackId,
                    value.outputLineId,
                    (ItemDefinitionId)value.itemId,
                    value.quantity,
                    new PhysicalMassGrams(value.massGrams),
                    value.itemInstanceId))
                .ToArray());
        if (!TryCaptureReleaseTarget(owner, out FacilityBufferAcknowledgedOutputReleaseTarget releaseTarget))
        {
            failureReason = "domain-output-release-target-invalid";
            return false;
        }
        bool acknowledged = owner.acknowledgementDisposition ==
                ProductionDomainOutputAcknowledgementDisposition
                    .RetainFacilityOutputBuffer
            ? publication.TryAcknowledgePublishedBatch(
                receipt,
                out _,
                out failureReason)
            : publication.TryAcknowledgeAndReleasePublishedBatch(
                receipt,
                releaseTarget,
                out _,
                out failureReason);
        if (acknowledged)
            owner.outputAcknowledged = true;
        return acknowledged;
    }

    public static string CaptureComponentFingerprint(
        IReadOnlyList<ItemInstanceComponentSaveData> components)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(ComponentFingerprintSchema);
        string[] rows = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Select(value => value?.ToCanonicalString()
                ?? throw new ArgumentException(
                    "Domain output component cannot be null.",
                    nameof(components)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        digest.Append(rows.Length);
        foreach (string row in rows)
            digest.Append(row);
        return digest.ComputeSha256();
    }

    public static bool TryValidateCommittedOwner(
        ProductionDomainOutputPublicationSaveData owner,
        out string failureReason)
    {
        bool valid;
        try
        {
            valid = owner != null
            && owner.schemaVersion ==
                ProductionDomainOutputPublicationSaveData.CurrentSchemaVersion
            && owner.publicationAttempt >= 0
            && Canonical(owner.publicationOperationId)
            && Canonical(owner.batchCommitId)
            && Sha(owner.outcomeFingerprint)
            && Sha(owner.maximumMassProofDigest)
            && owner.maximumBatchMassGrams > 0L
            && Sha(owner.capacitySourceDigest)
            && owner.requiredMinimumCapacityGrams > 0L
            && owner.outputMassGrams > 0L
            && owner.outputMassGrams <= owner.maximumBatchMassGrams
            && Canonical(owner.admissionTokenId)
            && Sha(owner.plannedOutputFingerprint)
            && Canonical(owner.destinationId)
            && Canonical(owner.ownerDomain)
            && Canonical(owner.ownerOperationId)
            && Canonical(owner.ownerFacilityId)
            && owner.capacityRevision > 0L
            && Enum.IsDefined(
                typeof(ProductionDomainOutputAcknowledgementDisposition),
                owner.acknowledgementDisposition)
            && (owner.acknowledgementDisposition !=
                    ProductionDomainOutputAcknowledgementDisposition
                        .RetainFacilityOutputBuffer
                || !owner.releaseHasDestination)
            && TryCaptureReleaseTarget(owner, out _)
            && owner.outputPublished
            && owner.admissionCommitted
            && owner.stacks != null
            && owner.stacks.Count > 0
            && owner.stacks.All(value => value != null
                && Canonical(value.outputLineId)
                && Canonical(value.itemId)
                && Canonical(value.stackId)
                && (string.IsNullOrEmpty(value.itemInstanceId)
                    || Canonical(value.itemInstanceId))
                && value.quantity > 0
                && value.massGrams > 0L)
            && owner.stacks
                .Where(value => !string.IsNullOrEmpty(value.itemInstanceId))
                .Select(value => value.itemInstanceId)
                .Distinct(StringComparer.Ordinal).Count()
                == owner.stacks.Count(value =>
                    !string.IsNullOrEmpty(value.itemInstanceId))
            && owner.stacks.Aggregate(
                    0L,
                    (sum, value) => checked(sum + value.massGrams))
                == owner.outputMassGrams;
        }
        catch (OverflowException)
        {
            valid = false;
        }
        failureReason = valid
            ? string.Empty
            : "domain-output-owner-not-committed";
        return valid;
    }

    public static bool TryValidateRestorableOwner(
        ProductionDomainOutputPublicationSaveData owner,
        out bool committed,
        out string failureReason)
    {
        committed = false;
        failureReason = string.Empty;
        if (owner == null || owner.IsEmpty)
            return true;
        if (TryValidateCommittedOwner(owner, out _))
        {
            committed = true;
            return true;
        }

        bool frozenAwaitingReservation = owner.schemaVersion ==
                ProductionDomainOutputPublicationSaveData.CurrentSchemaVersion
            && owner.publicationAttempt >= 0
            && (string.IsNullOrEmpty(owner.publicationOperationId)
                || Canonical(owner.publicationOperationId))
            && Canonical(owner.batchCommitId)
            && Sha(owner.outcomeFingerprint)
            && Sha(owner.maximumMassProofDigest)
            && owner.maximumBatchMassGrams > 0L
            && Sha(owner.capacitySourceDigest)
            && owner.requiredMinimumCapacityGrams > 0L
            && owner.outputMassGrams > 0L
            && owner.outputMassGrams <= owner.maximumBatchMassGrams
            && string.IsNullOrEmpty(owner.admissionTokenId)
            && string.IsNullOrEmpty(owner.plannedOutputFingerprint)
            && Canonical(owner.destinationId)
            && Canonical(owner.ownerDomain)
            && Canonical(owner.ownerOperationId)
            && Canonical(owner.ownerFacilityId)
            && owner.capacityRevision > 0L
            && Enum.IsDefined(
                typeof(ProductionDomainOutputAcknowledgementDisposition),
                owner.acknowledgementDisposition)
            && (owner.acknowledgementDisposition !=
                    ProductionDomainOutputAcknowledgementDisposition
                        .RetainFacilityOutputBuffer
                || !owner.releaseHasDestination)
            && TryCaptureReleaseTarget(owner, out _)
            && !owner.outputPublished
            && !owner.admissionCommitted
            && !owner.outputAcknowledged
            && (owner.stacks == null || owner.stacks.Count == 0);
        failureReason = frozenAwaitingReservation
            ? string.Empty
            : "domain-output-owner-not-restorable";
        return frozenAwaitingReservation;
    }

    private bool TryGetOrReserveToken(
        ProductionDomainOutputPublicationSaveData owner,
        ProductionDomainOutputPublicationPlan plan,
        ProductionFacilityHandle handle,
        FacilityBufferCapacityProfile profile,
        IReadOnlyList<FacilityBufferPlannedOutputSlice> slices,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        token = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (Canonical(owner.admissionTokenId))
        {
            if (!admission.TryGetPlannedOutputToken(
                    owner.admissionTokenId,
                    out token,
                    out FacilityBufferMassAdmissionTokenStatus status))
            {
                failureReason = "domain-output-admission-token-missing";
                return false;
            }
            if (status != FacilityBufferMassAdmissionTokenStatus.Released)
                return TokenMatchesOwner(owner, token, out failureReason);
            ResetReleasedAttempt(owner);
        }

        owner.publicationOperationId = plan.PublicationOperationPrefix
            + plan.OwnerId + ":"
            + owner.publicationAttempt.ToString("D4", CultureInfo.InvariantCulture);
        FacilityBufferPlannedOutputRequest request = new(
            owner.publicationOperationId,
            owner.batchCommitId,
            owner.outcomeFingerprint,
            profile.DestinationId,
            handle.Position,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            slices,
            owner.capacitySourceDigest,
            owner.requiredMinimumCapacityGrams,
            profile.AuthorityDigest);
        if (!admission.TryReservePlannedOutput(
                request,
                out token,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        owner.admissionTokenId = token.TokenId;
        owner.plannedOutputFingerprint = token.PlannedOutput.Fingerprint;
        return true;
    }

    private static bool TryAdoptFrozenOwner(
        ProductionDomainOutputPublicationSaveData owner,
        ProductionDomainOutputPublicationPlan plan,
        ProductionOutputBufferCapacitySourceSnapshot capacity,
        FacilityBufferCapacityProfile profile,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        long outputMassGrams,
        out string failureReason)
    {
        failureReason = string.Empty;
        bool empty = string.IsNullOrEmpty(owner.batchCommitId);
        if (!empty)
        {
            bool matches = owner.schemaVersion ==
                    ProductionDomainOutputPublicationSaveData.CurrentSchemaVersion
                && string.Equals(
                    owner.batchCommitId,
                    plan.BatchCommitId,
                    StringComparison.Ordinal)
                && string.Equals(
                    owner.outcomeFingerprint,
                    plan.OutcomeFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    owner.maximumMassProofDigest,
                    maximumMassProof.SourceDigest,
                    StringComparison.Ordinal)
                && owner.maximumBatchMassGrams
                    == maximumMassProof.MaximumBatchMassGrams
                && string.Equals(
                    owner.capacitySourceDigest,
                    capacity.SourceDigest,
                    StringComparison.Ordinal)
                && owner.requiredMinimumCapacityGrams
                    == capacity.RequiredMinimumCapacityGrams
                && owner.outputMassGrams == outputMassGrams
                && string.Equals(
                    owner.destinationId,
                    profile.DestinationId,
                    StringComparison.Ordinal)
                && owner.destinationX == profile.DropPosition.x
                && owner.destinationY == profile.DropPosition.y
                && ReleaseTargetMatchesOwner(owner, plan.ReleaseTarget)
                && owner.acknowledgementDisposition
                    == plan.AcknowledgementDisposition
                && string.Equals(
                    owner.ownerDomain,
                    profile.OwnerDomain,
                    StringComparison.Ordinal)
                && string.Equals(
                    owner.ownerOperationId,
                    profile.OwnerOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    owner.ownerFacilityId,
                    profile.OwnerFacilityId,
                    StringComparison.Ordinal)
                && owner.capacityRevision == profile.CapacityRevision;
            failureReason = matches
                ? string.Empty
                : "domain-output-frozen-owner-drift";
            return matches;
        }

        if (!owner.IsEmpty)
        {
            failureReason = "domain-output-owner-partial";
            return false;
        }
        owner.schemaVersion =
            ProductionDomainOutputPublicationSaveData.CurrentSchemaVersion;
        owner.batchCommitId = plan.BatchCommitId;
        owner.outcomeFingerprint = plan.OutcomeFingerprint;
        owner.maximumMassProofDigest = maximumMassProof.SourceDigest;
        owner.maximumBatchMassGrams = maximumMassProof.MaximumBatchMassGrams;
        owner.capacitySourceDigest = capacity.SourceDigest;
        owner.requiredMinimumCapacityGrams =
            capacity.RequiredMinimumCapacityGrams;
        owner.outputMassGrams = outputMassGrams;
        owner.destinationId = profile.DestinationId;
        owner.destinationX = profile.DropPosition.x;
        owner.destinationY = profile.DropPosition.y;
        owner.releaseHasDestination = plan.ReleaseTarget.HasDestination;
        owner.releaseDestinationId = plan.ReleaseTarget.HasDestination
            ? plan.ReleaseTarget.DestinationId
            : string.Empty;
        owner.releaseDestinationX = plan.ReleaseTarget.HasDestination
            ? plan.ReleaseTarget.DestinationPosition.x
            : 0;
        owner.releaseDestinationY = plan.ReleaseTarget.HasDestination
            ? plan.ReleaseTarget.DestinationPosition.y
            : 0;
        owner.acknowledgementDisposition = plan.AcknowledgementDisposition;
        owner.ownerDomain = profile.OwnerDomain;
        owner.ownerOperationId = profile.OwnerOperationId;
        owner.ownerFacilityId = profile.OwnerFacilityId;
        owner.capacityRevision = profile.CapacityRevision;
        return true;
    }

    private void ReleaseReserved(
        ProductionDomainOutputPublicationSaveData owner,
        FacilityBufferPlannedOutputToken token)
    {
        if (owner.outputPublished || owner.admissionCommitted)
            return;
        if (admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _))
        {
            ResetReleasedAttempt(owner);
        }
    }

    private static void ResetReleasedAttempt(
        ProductionDomainOutputPublicationSaveData owner)
    {
        owner.publicationAttempt = checked(owner.publicationAttempt + 1);
        owner.publicationOperationId = string.Empty;
        owner.admissionTokenId = string.Empty;
        owner.plannedOutputFingerprint = string.Empty;
        owner.outputPublished = false;
        owner.admissionCommitted = false;
        owner.outputAcknowledged = false;
        (owner.stacks ??= new List<ProductionDomainPublishedStackSaveData>())
            .Clear();
    }

    private static bool TokenMatchesOwner(
        ProductionDomainOutputPublicationSaveData owner,
        FacilityBufferPlannedOutputToken token,
        out string failureReason)
    {
        bool matches = string.Equals(
                token.TokenId,
                owner.admissionTokenId,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.PublicationOperationId,
                owner.publicationOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.BatchCommitId,
                owner.batchCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.OutcomeFingerprint,
                owner.outcomeFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.DestinationId,
                owner.destinationId,
                StringComparison.Ordinal)
            && token.Request.DropPosition
                == new Vector2Int(owner.destinationX, owner.destinationY)
            && string.Equals(
                token.Request.ExpectedOwnerDomain,
                owner.ownerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.ExpectedOwnerOperationId,
                owner.ownerOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.ExpectedOwnerFacilityId,
                owner.ownerFacilityId,
                StringComparison.Ordinal)
            && token.Request.ExpectedCapacityRevision == owner.capacityRevision
            && string.Equals(
                token.Request.CapacitySourceDigest,
                owner.capacitySourceDigest,
                StringComparison.Ordinal)
            && token.Request.ExpectedMinimumCapacityGrams
                == owner.requiredMinimumCapacityGrams
            && token.ReservedMassGrams == owner.outputMassGrams
            && (string.IsNullOrEmpty(owner.plannedOutputFingerprint)
                || string.Equals(
                    token.PlannedOutput.Fingerprint,
                    owner.plannedOutputFingerprint,
                    StringComparison.Ordinal));
        failureReason = matches
            ? string.Empty
            : "domain-output-admission-token-drift";
        return matches;
    }

    private static bool TryValidatePlan(
        ProductionDomainOutputPublicationPlan plan,
        out string failureReason)
    {
        bool valid = plan != null
            && Canonical(plan.PublicationOperationPrefix)
            && plan.PublicationOperationPrefix.EndsWith(":", StringComparison.Ordinal)
            && Canonical(plan.OwnerId)
            && Canonical(plan.BatchCommitId)
            && Sha(plan.OutcomeFingerprint)
            && plan.ReleaseTarget.IsValid
            && Enum.IsDefined(
                typeof(ProductionDomainOutputAcknowledgementDisposition),
                plan.AcknowledgementDisposition)
            && (plan.AcknowledgementDisposition !=
                    ProductionDomainOutputAcknowledgementDisposition
                        .RetainFacilityOutputBuffer
                || !plan.ReleaseTarget.HasDestination)
            && plan.FacilityRuntimeObject != null
            && plan.Lines.Count > 0
            && plan.Lines.All(value => value != null
                && Canonical(value.OutputLineId)
                && Canonical(value.ItemId)
                && value.Quantity > 0
                && (value.ItemInstanceId.Length == 0
                    || Canonical(value.ItemInstanceId))
                && CapabilityMatchesLine(value))
            && MaximumClaimsMatchLines(plan);
        failureReason = valid ? string.Empty : "domain-output-plan-invalid";
        return valid;
    }

    private static ProductionDomainOutputPublicationResult Conflict(
        string reason) => new(
        ProductionDomainOutputPublicationStatus.Conflict,
        reason);

    private static ProductionDomainOutputPublicationResult Pending(
        string reason) => new(
        ProductionDomainOutputPublicationStatus.Pending,
        reason);

    private static ProductionDomainOutputPublicationResult Waiting(
        string reason) => new(
        ProductionDomainOutputPublicationStatus.WaitingForOutputSpace,
        reason);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool Sha(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');

    private static bool CapabilityMatchesLine(
        ProductionDomainOutputLine line)
    {
        ProductionOutputCapabilityDescriptor descriptor =
            line.OutputCapability;
        return string.Equals(
                descriptor.OutputLineId,
                line.OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(
                descriptor.ItemId,
                line.ItemId,
                StringComparison.Ordinal)
            && Canonical(descriptor.CapabilityId)
            && descriptor.CapabilityVersion > 0
            && Canonical(descriptor.ComponentCodecId)
            && descriptor.ComponentCodecVersion > 0
            && Sha(descriptor.Fingerprint)
            && string.Equals(
                descriptor.Fingerprint,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    descriptor.OutputLineId,
                    descriptor.ItemId,
                    descriptor.CapabilityId,
                    descriptor.CapabilityVersion,
                    descriptor.ComponentCodecId,
                    descriptor.ComponentCodecVersion),
                StringComparison.Ordinal);
    }

    private static bool MaximumClaimsMatchLines(
        ProductionDomainOutputPublicationPlan plan)
    {
        if (plan.MaximumMassClaims == null
            || plan.MaximumMassClaims.Count < plan.Lines.Count
            || plan.MaximumMassClaims.Any(value => value == null
                || value.MaximumQuantity <= 0))
        {
            return false;
        }

        Dictionary<string, ProductionDomainOutputMaximumMassClaim> claims;
        try
        {
            claims = plan.MaximumMassClaims.ToDictionary(
                value => value.Descriptor.OutputLineId,
                StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return plan.Lines.All(line =>
            claims.TryGetValue(
                line.OutputLineId,
                out ProductionDomainOutputMaximumMassClaim claim)
            && line.Quantity <= claim.MaximumQuantity
            && CapabilityMatchesClaim(line, claim));
    }

    private static bool CapabilityMatchesClaim(
        ProductionDomainOutputLine line,
        ProductionDomainOutputMaximumMassClaim claim)
    {
        ProductionOutputCapabilityDescriptor descriptor = claim.Descriptor;
        ProductionOutputCapabilityDescriptor actual = line.OutputCapability;
        return string.Equals(
                descriptor.OutputLineId,
                line.OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(
                descriptor.ItemId,
                line.ItemId,
                StringComparison.Ordinal)
            && string.Equals(
                descriptor.CapabilityId,
                actual.CapabilityId,
                StringComparison.Ordinal)
            && descriptor.CapabilityVersion == actual.CapabilityVersion
            && string.Equals(
                descriptor.ComponentCodecId,
                actual.ComponentCodecId,
                StringComparison.Ordinal)
            && descriptor.ComponentCodecVersion == actual.ComponentCodecVersion
            && string.Equals(
                descriptor.Fingerprint,
                actual.Fingerprint,
                StringComparison.Ordinal);
    }

    internal static bool TryCaptureReleaseTarget(
        ProductionDomainOutputPublicationSaveData owner,
        out FacilityBufferAcknowledgedOutputReleaseTarget target)
    {
        target = default;
        if (owner == null)
            return false;
        if (!owner.releaseHasDestination)
        {
            return string.IsNullOrEmpty(owner.releaseDestinationId)
                && owner.releaseDestinationX == 0
                && owner.releaseDestinationY == 0;
        }
        if (!Canonical(owner.releaseDestinationId))
            return false;
        target = new FacilityBufferAcknowledgedOutputReleaseTarget(
            owner.releaseDestinationId,
            new Vector2Int(
                owner.releaseDestinationX,
                owner.releaseDestinationY));
        return target.IsValid;
    }

    private static bool ReleaseTargetMatchesOwner(
        ProductionDomainOutputPublicationSaveData owner,
        FacilityBufferAcknowledgedOutputReleaseTarget target) =>
        TryCaptureReleaseTarget(owner, out FacilityBufferAcknowledgedOutputReleaseTarget frozen)
        && frozen.HasDestination == target.HasDestination
        && (!target.HasDestination
            || string.Equals(
                    frozen.DestinationId,
                    target.DestinationId,
                    StringComparison.Ordinal)
                && frozen.DestinationPosition == target.DestinationPosition);
}

public sealed class ProductionDomainOutputRestoreAcknowledgement
{
    public ProductionDomainOutputRestoreAcknowledgement(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        FacilityBufferAcknowledgedOutputReleaseTarget releaseTarget)
        : this(
            candidate,
            releaseTarget,
            ProductionDomainOutputAcknowledgementDisposition
                .ReleaseLooseOrDestination)
    {
    }

    public ProductionDomainOutputRestoreAcknowledgement(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        FacilityBufferAcknowledgedOutputReleaseTarget releaseTarget,
        ProductionDomainOutputAcknowledgementDisposition disposition)
    {
        Candidate = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        ReleaseTarget = releaseTarget;
        Disposition = disposition;
        if (!releaseTarget.IsValid)
        {
            throw new ArgumentException(
                "Domain-output restore release target is invalid.",
                nameof(releaseTarget));
        }
        if (!Enum.IsDefined(
                typeof(ProductionDomainOutputAcknowledgementDisposition),
                disposition)
            || disposition == ProductionDomainOutputAcknowledgementDisposition
                    .RetainFacilityOutputBuffer
                && releaseTarget.HasDestination)
        {
            throw new ArgumentException(
                "Domain-output restore acknowledgement disposition is invalid.",
                nameof(disposition));
        }
    }

    public FacilityBufferPlannedOutputRestoreBatchSnapshot Candidate { get; }
    public FacilityBufferAcknowledgedOutputReleaseTarget ReleaseTarget { get; }
    public ProductionDomainOutputAcknowledgementDisposition Disposition { get; }
    public string BatchCommitId => Candidate.BatchCommitId;
}

public interface IProductionDomainOutputRestoreJoin
{
    ProductionDomainOutputRestoreAcknowledgement AdoptPending(
        ProductionDomainOutputPublicationSaveData owner);

    void RequireNoPending(
        ProductionDomainOutputPublicationSaveData owner);

    void Acknowledge(
        IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement>
            candidates);
}

/// <summary>
/// Domain-neutral staging adapter used by every custom producer. The domain
/// decides which durable phase means "restored and acknowledged"; this adapter
/// owns the exact physical-batch join and marker acknowledgement mechanics.
/// </summary>
public sealed class ProductionDomainOutputRestoreJoin :
    IProductionDomainOutputRestoreJoin
{
    private readonly IFacilityBufferPlannedOutputRestoreCandidateQuery query;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;

    public ProductionDomainOutputRestoreJoin(
        IFacilityBufferPlannedOutputRestoreCandidateQuery query,
        IFacilityBufferPlannedOutputPublicationService publication)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
    }

    public ProductionDomainOutputRestoreAcknowledgement AdoptPending(
        ProductionDomainOutputPublicationSaveData owner)
    {
        RequireCandidateQuery();
        if (!ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                owner,
                out string failureReason)
            || owner.outputAcknowledged)
        {
            throw new InvalidOperationException(
                "Domain-output restore owner is not pending acknowledgement: "
                + failureReason);
        }
        if (!query.TryGetBatch(
                owner.batchCommitId,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot incoming))
        {
            throw new InvalidOperationException(
                "Domain-output restore owner has no exact incoming physical batch: "
                + owner.batchCommitId);
        }
        ProductionDomainOutputRestoreGuard.ValidateIncoming(owner, incoming);
        if (!ProductionDomainOutputPublicationService.TryCaptureReleaseTarget(
                owner,
                out FacilityBufferAcknowledgedOutputReleaseTarget releaseTarget))
        {
            throw new InvalidOperationException(
                "Domain-output restore owner has an invalid release target: "
                + owner.batchCommitId);
        }
        return new ProductionDomainOutputRestoreAcknowledgement(
            incoming,
            releaseTarget,
            owner.acknowledgementDisposition);
    }

    public void RequireNoPending(
        ProductionDomainOutputPublicationSaveData owner)
    {
        if (owner == null || owner.IsEmpty)
            return;
        RequireCandidateQuery();
        if (Canonical(owner.batchCommitId)
            && query.TryGetBatch(owner.batchCommitId, out _))
        {
            throw new InvalidOperationException(
                "Acknowledged or unpublished domain-output owner still has a physical marker: "
                + owner.batchCommitId);
        }
    }

    public void Acknowledge(
        IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement>
            candidates)
    {
        foreach (ProductionDomainOutputRestoreAcknowledgement acknowledgement in
                 (candidates
                     ?? Array.Empty<
                         ProductionDomainOutputRestoreAcknowledgement>())
                 .OrderBy(value => value?.BatchCommitId, StringComparer.Ordinal))
        {
            if (acknowledgement == null)
                throw new InvalidOperationException(
                    "Domain-output restore acknowledgement candidate is null.");
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate =
                acknowledgement.Candidate;
            bool succeeded = acknowledgement.Disposition ==
                    ProductionDomainOutputAcknowledgementDisposition
                        .RetainFacilityOutputBuffer
                ? publication.TryAcknowledgeRestoreCandidate(
                    candidate,
                    out FacilityBufferPlannedOutputPublicationFailureCode code,
                    out string reason)
                : publication.TryAcknowledgeAndReleaseRestoreCandidate(
                    candidate,
                    acknowledgement.ReleaseTarget,
                    out code,
                    out reason);
            if (!succeeded)
            {
                throw new InvalidOperationException(
                    $"Domain-output restore acknowledgement failed ({code}): {reason}");
            }
        }
    }

    private void RequireCandidateQuery()
    {
        if (!query.IsCandidateAvailable || query.Batches == null)
        {
            throw new InvalidOperationException(
                "Domain-output restore requires the incoming physical candidate.");
        }
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

/// <summary>
/// Cross-aggregate restore join for every custom producer registered through
/// IProductionDomainOutputRestoreOwnerSource. It owns no gameplay state: it
/// proves after section-level adoption that no pending domain batch or owner is
/// orphaned.
/// </summary>
public sealed class ProductionDomainOutputRestoreGuard :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "174.world.production-domain-output-publications";

    private readonly IReadOnlyList<IProductionDomainOutputRestoreOwnerSource>
        sources;
    private readonly IFacilityBufferPlannedOutputRestoreCandidateQuery query;
    private readonly IProductionOutputMaximumMassRegistry maximumMassRegistry;
    private bool active;
    private bool published;

    public ProductionDomainOutputRestoreGuard(
        IEnumerable<IProductionDomainOutputRestoreOwnerSource> sources,
        IFacilityBufferPlannedOutputRestoreCandidateQuery query,
        IProductionOutputMaximumMassRegistry maximumMassRegistry)
    {
        this.sources = Array.AsReadOnly((sources
                ?? throw new ArgumentNullException(nameof(sources)))
            .OrderBy(value => value?.OutputOwnerDomainId, StringComparer.Ordinal)
            .ToArray());
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.maximumMassRegistry = maximumMassRegistry
            ?? throw new ArgumentNullException(nameof(maximumMassRegistry));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
            throw new InvalidOperationException(
                "Production domain-output restore validation is already active.");
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
            throw new InvalidOperationException(
                "Production domain-output restore validation is not ready to publish.");
        ValidateOwnerSet(sources, query, maximumMassRegistry);
        published = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public void CompleteRestoreCandidate()
    {
        if (!active || !published)
            throw new InvalidOperationException(
                "Production domain-output restore validation cannot complete.");
        active = false;
        published = false;
    }

    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public static void ValidateOwnerSet(
        IEnumerable<IProductionDomainOutputRestoreOwnerSource> sources,
        IFacilityBufferPlannedOutputRestoreCandidateQuery query,
        IProductionOutputMaximumMassRegistry maximumMassRegistry)
    {
        if (query == null || !query.IsCandidateAvailable || query.Batches == null)
            throw new InvalidOperationException(
                "Production domain-output restore requires the incoming physical candidate.");
        if (maximumMassRegistry == null)
            throw new ArgumentNullException(nameof(maximumMassRegistry));

        Dictionary<string, ProductionDomainOutputRestoreOwnerSnapshot> owners =
            new(StringComparer.Ordinal);
        HashSet<string> domains = new(StringComparer.Ordinal);
        HashSet<string> prefixes = new(StringComparer.Ordinal);
        foreach (IProductionDomainOutputRestoreOwnerSource source in
                 (sources ?? Array.Empty<
                         IProductionDomainOutputRestoreOwnerSource>())
                 .OrderBy(value => value?.OutputOwnerDomainId,
                     StringComparer.Ordinal))
        {
            if (source == null
                || !Canonical(source.OutputOwnerDomainId)
                || !Canonical(source.OutputBatchCommitPrefix)
                || !source.OutputBatchCommitPrefix.StartsWith(
                    ProductionDomainOutputPublicationIdentity.BatchCommitPrefix,
                    StringComparison.Ordinal)
                || !source.OutputBatchCommitPrefix.EndsWith(
                    ":",
                    StringComparison.Ordinal)
                || !domains.Add(source.OutputOwnerDomainId)
                || !prefixes.Add(source.OutputBatchCommitPrefix))
            {
                throw new InvalidOperationException(
                    "Production domain-output restore source registration is invalid or duplicated.");
            }

            IReadOnlyList<ProductionDomainOutputRestoreOwnerSnapshot>
                snapshots = source.CapturePendingOutputOwners()
                    ?? throw new InvalidOperationException(
                        "Production domain-output restore source returned no owner collection: "
                        + source.OutputOwnerDomainId);
            foreach (ProductionDomainOutputRestoreOwnerSnapshot snapshot in
                     snapshots.OrderBy(value => value?.OwnerStableId,
                         StringComparer.Ordinal))
            {
                if (snapshot == null
                    || !Canonical(snapshot.OwnerStableId)
                    || snapshot.Publication == null
                    || !snapshot.Publication.batchCommitId.StartsWith(
                        source.OutputBatchCommitPrefix,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Production domain-output owner is invalid or duplicated: "
                        + (snapshot?.OwnerStableId ?? "<null>"));
                }
                if (!ProductionDomainOutputPublicationService
                        .TryValidateCommittedOwner(
                            snapshot.Publication,
                            out string ownerFailure))
                {
                    throw new InvalidOperationException(
                        "Production domain-output owner is invalid: "
                        + snapshot.OwnerStableId + ":" + ownerFailure);
                }
                ValidateMaximumMassProof(snapshot, maximumMassRegistry);
                if (snapshot.Publication.outputAcknowledged
                    && !snapshot.Publication.restoredInCurrentTransaction)
                {
                    throw new InvalidOperationException(
                        "Production domain-output acknowledged owner was not adopted in the current restore transaction: "
                        + snapshot.OwnerStableId);
                }
                if (!owners.TryAdd(
                        snapshot.Publication.batchCommitId,
                        snapshot))
                {
                    throw new InvalidOperationException(
                        "Production domain-output owner is duplicated: "
                        + snapshot.OwnerStableId);
                }
            }
        }

        foreach ((string batchCommitId,
                     ProductionDomainOutputRestoreOwnerSnapshot snapshot) in
                 owners.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            if (!query.TryGetBatch(
                    batchCommitId,
                    out FacilityBufferPlannedOutputRestoreBatchSnapshot incoming))
            {
                throw new InvalidOperationException(
                    "Production domain-output owner has no exact incoming physical batch: "
                    + batchCommitId);
            }
            ValidateIncoming(snapshot.Publication, incoming);
        }

        HashSet<string> incomingIds = new(StringComparer.Ordinal);
        foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot incoming in
                 query.Batches
                     .Where(value => value?.BatchCommitId.StartsWith(
                         ProductionDomainOutputPublicationIdentity.BatchCommitPrefix,
                         StringComparison.Ordinal) == true)
                     .OrderBy(value => value.BatchCommitId,
                         StringComparer.Ordinal))
        {
            if (!incomingIds.Add(incoming.BatchCommitId)
                || !owners.ContainsKey(incoming.BatchCommitId))
            {
                throw new InvalidOperationException(
                    "Incoming domain-output physical batch has no exact registered owner: "
                    + (incoming?.BatchCommitId ?? "<null>"));
            }
        }
    }

    private static void ValidateMaximumMassProof(
        ProductionDomainOutputRestoreOwnerSnapshot snapshot,
        IProductionOutputMaximumMassRegistry maximumMassRegistry)
    {
        if (snapshot.MaximumMassClaims == null
            || snapshot.MaximumMassClaims.Count == 0)
        {
            throw new InvalidOperationException(
                "Production domain-output owner has no maximum-mass claim: "
                + snapshot.OwnerStableId);
        }
        ProductionOutputMaximumMassProjection[] projections = snapshot
            .MaximumMassClaims
            .Select(value => value == null || value.MaximumQuantity <= 0
                ? throw new InvalidOperationException(
                    "Production domain-output maximum-mass claim is invalid: "
                    + snapshot.OwnerStableId)
                : maximumMassRegistry.CaptureDeclared(
                    value.Descriptor,
                    value.MaximumQuantity))
            .ToArray();
        ProductionOutputBatchMaximumMassProof proof = new(projections);
        if (!string.Equals(
                proof.SourceDigest,
                snapshot.Publication.maximumMassProofDigest,
                StringComparison.Ordinal)
            || proof.MaximumBatchMassGrams
                != snapshot.Publication.maximumBatchMassGrams)
        {
            throw new InvalidOperationException(
                "Production domain-output maximum-mass proof is stale: "
                + snapshot.OwnerStableId);
        }
    }

    public static void ValidateIncoming(
        ProductionDomainOutputPublicationSaveData owner,
        FacilityBufferPlannedOutputRestoreBatchSnapshot incoming)
    {
        Dictionary<string, ProductionDomainPublishedStackSaveData> expected =
            owner.stacks.ToDictionary(value => value.stackId,
                StringComparer.Ordinal);
        bool matches = incoming != null
            && string.Equals(
                incoming.BatchCommitId,
                owner.batchCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                incoming.OutcomeFingerprint,
                owner.outcomeFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                incoming.PlannedOutputFingerprint,
                owner.plannedOutputFingerprint,
                StringComparison.Ordinal)
            && incoming.TotalMassGrams == owner.outputMassGrams
            && incoming.TotalQuantity == owner.stacks.Sum(value => value.quantity)
            && incoming.Stacks.Count == expected.Count
            && incoming.Stacks.All(value => value != null
                && expected.TryGetValue(value.StackId, out var stack)
                && string.Equals(
                    value.OutputLineId,
                    stack.outputLineId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ItemId,
                    stack.itemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ItemInstanceId,
                    stack.itemInstanceId,
                    StringComparison.Ordinal)
                && value.Quantity == stack.quantity
                && value.MassGrams == stack.massGrams
                && value.State == WorldItemStackState.FacilityOutputBuffer
                && value.Position == new Vector2Int(
                    owner.destinationX,
                    owner.destinationY)
                && string.Equals(
                    value.DestinationId,
                    owner.destinationId,
                    StringComparison.Ordinal));
        if (!matches)
        {
            throw new InvalidOperationException(
                "Production domain-output owner conflicts with its incoming physical batch: "
                + owner.batchCommitId);
        }
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
