using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

public interface IProductionExactCapabilityOutputRestoreJoin
{
    void Validate(DungeonProductionBillSaveData payload);
}

public sealed class EmptyProductionExactCapabilityOutputRestoreJoin :
    IProductionExactCapabilityOutputRestoreJoin
{
    public static readonly EmptyProductionExactCapabilityOutputRestoreJoin
        Instance = new();

    private EmptyProductionExactCapabilityOutputRestoreJoin()
    {
    }

    public void Validate(DungeonProductionBillSaveData payload)
    {
        if ((payload?.bills ?? new List<ProductionBillSaveData>())
            .SelectMany(value => value?.resolvedOutputs
                ?? new List<ProductionResolvedOutputSaveData>())
            .Any(value => value != null
                && !string.IsNullOrEmpty(value.pendingCommitId)))
        {
            throw new InvalidOperationException(
                "Exact-capability production restore requires the physical join authority.");
        }
    }
}

/// <summary>
/// Validates the detached Production V21 exact-output owner against detached
/// physical stacks and facility capacity before either aggregate is published.
/// It dispatches by the frozen capability descriptor through the common
/// registries; content identifiers are never interpreted here.
/// </summary>
public sealed class ProductionExactCapabilityOutputRestoreJoin :
    IProductionExactCapabilityOutputRestoreJoin
{
    private readonly IFacilityBufferPlannedOutputRestoreCandidateQuery pending;
    private readonly IFacilityBufferAcknowledgedOutputRestoreCandidateQuery
        acknowledged;
    private readonly IProductionOutputCapabilityRegistry capabilities;
    private readonly IProductionOutputMaximumMassRegistry maximumMass;
    private readonly IProductionOutputDetachedFacilityCapacityRestoreGuard
        detachedCapacity;
    private readonly IProductionOutputDetachedFacilityCapacityProjectionQuery
        detachedCapacityProjection;
    private readonly IProductionResolvedOutputRestoreCapabilityValidatorRegistry
        semanticValidators;

    public ProductionExactCapabilityOutputRestoreJoin(
        IFacilityBufferPlannedOutputRestoreCandidateQuery pending,
        IFacilityBufferAcknowledgedOutputRestoreCandidateQuery acknowledged,
        IProductionOutputCapabilityRegistry capabilities,
        IProductionOutputMaximumMassRegistry maximumMass,
        IProductionOutputDetachedFacilityCapacityRestoreGuard detachedCapacity)
        : this(
            pending,
            acknowledged,
            capabilities,
            maximumMass,
            detachedCapacity,
            null,
            EmptyProductionResolvedOutputRestoreCapabilityValidatorRegistry
                .Instance)
    {
    }

    [Inject]
    public ProductionExactCapabilityOutputRestoreJoin(
        IFacilityBufferPlannedOutputRestoreCandidateQuery pending,
        IFacilityBufferAcknowledgedOutputRestoreCandidateQuery acknowledged,
        IProductionOutputCapabilityRegistry capabilities,
        IProductionOutputMaximumMassRegistry maximumMass,
        IProductionOutputDetachedFacilityCapacityRestoreGuard detachedCapacity,
        IProductionOutputDetachedFacilityCapacityProjectionQuery
            detachedCapacityProjection,
        IProductionResolvedOutputRestoreCapabilityValidatorRegistry
            semanticValidators)
    {
        this.pending = pending ?? throw new ArgumentNullException(nameof(pending));
        this.acknowledged = acknowledged
            ?? throw new ArgumentNullException(nameof(acknowledged));
        this.capabilities = capabilities
            ?? throw new ArgumentNullException(nameof(capabilities));
        this.maximumMass = maximumMass
            ?? throw new ArgumentNullException(nameof(maximumMass));
        this.detachedCapacity = detachedCapacity
            ?? throw new ArgumentNullException(nameof(detachedCapacity));
        this.detachedCapacityProjection = detachedCapacityProjection;
        this.semanticValidators = semanticValidators
            ?? throw new ArgumentNullException(nameof(semanticValidators));
    }

    public void Validate(DungeonProductionBillSaveData payload)
    {
        if (payload?.bills == null)
            throw new InvalidOperationException(
                "Exact-capability production restore has no bill owner collection.");
        if (!pending.IsCandidateAvailable
            || pending.Batches == null
            || !acknowledged.IsCandidateAvailable
            || acknowledged.Batches == null)
        {
            throw new InvalidOperationException(
                "Exact-capability production restore requires both physical lifecycle candidates.");
        }

        Dictionary<string, Owner> owners = new(StringComparer.Ordinal);
        foreach (ProductionBillSaveData bill in payload.bills
                     .Where(value => value != null)
                     .OrderBy(value => value.billId, StringComparer.Ordinal))
        {
            foreach (ProductionResolvedOutputSaveData output in
                     (bill.resolvedOutputs
                         ?? new List<ProductionResolvedOutputSaveData>())
                     .Where(value => value != null)
                     .OrderBy(value => value.outputLineId, StringComparer.Ordinal))
            {
                if (string.IsNullOrEmpty(output.pendingCommitId))
                    continue;
                if (!ProductionOutputCommitIdentity.IsOwnedCommitId(
                        output.pendingCommitId)
                    || !owners.TryAdd(
                        output.pendingCommitId,
                        new Owner(bill, output)))
                {
                    throw new InvalidOperationException(
                        "Duplicate or invalid exact-output restore owner: "
                        + output.pendingCommitId);
                }
            }
        }

        foreach (KeyValuePair<string, Owner> pair in owners)
            ValidateOwner(pair.Key, pair.Value);

        foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot batch in
                 pending.Batches.OrderBy(
                     value => value.BatchCommitId,
                     StringComparer.Ordinal))
        {
            if (ProductionOutputCommitIdentity.IsOwnedCommitId(
                    batch.BatchCommitId)
                && !owners.ContainsKey(batch.BatchCommitId))
            {
                throw new InvalidOperationException(
                    "Orphan pending exact-output physical batch: "
                    + batch.BatchCommitId);
            }
        }
    }

    private void ValidateOwner(string commitId, Owner owner)
    {
        ProductionResolvedOutputSaveData output = owner.Output;
        ProductionOutputCapabilityDescriptor descriptor = new(
            output.outputLineId,
            output.itemId,
            output.outputCapabilityId,
            output.outputCapabilityVersion,
            output.outputComponentCodecId,
            output.outputComponentCodecVersion,
            output.outputCapabilityFingerprint);
        if (!capabilities.TryValidateExact(
                descriptor,
                out IProductionOutputCapability capability,
                out DomainFailure capabilityFailure)
            || capability is not IProductionOutputHandler handler
            || handler is not IIdempotentProductionOutputHandler)
        {
            throw new InvalidOperationException(
                "Exact-output restore capability is unavailable: "
                + commitId + ":" + capabilityFailure.Code);
        }

        ProductionOutputMaximumMassProjection projection =
            maximumMass.CaptureDeclared(descriptor, maximumQuantity: 1);
        ProductionOutputBatchMaximumMassProof proof = new(
            new[] { projection });
        bool hasPending = pending.TryGetBatch(commitId, out var pendingBatch);
        bool hasAcknowledged = acknowledged.TryGetBatch(
            commitId,
            out var acknowledgedBatch);
        if (hasPending == hasAcknowledged)
        {
            throw new InvalidOperationException(
                "Exact-output restore owner must resolve exactly one physical lifecycle state: "
                + commitId);
        }
        FacilityBufferPlannedOutputRestoreBatchSnapshot physical = hasPending
            ? pendingBatch
            : acknowledgedBatch;

        if (!output.pendingCommitApplied)
        {
            if (!hasPending)
                throw new InvalidOperationException(
                    "Unapplied exact-output owner has no pending physical publication: "
                    + commitId);
            ValidateUnapplied(owner, physical, proof);
            ValidateCapabilitySemantics(
                owner,
                descriptor,
                proof,
                physical,
                hasPending,
                ProductionExactOutputPublicationSaveData.Empty());
            return;
        }

        ProductionExactOutputPublicationSaveData envelope =
            output.pendingOutputPublication
            ?? throw new InvalidOperationException(
                "Applied exact-output owner has no frozen publication envelope: "
                + commitId);
        if (!string.Equals(
                envelope.maximumProofDigest,
                proof.SourceDigest,
                StringComparison.Ordinal)
            || envelope.maximumMassGrams != proof.MaximumBatchMassGrams)
        {
            throw new InvalidOperationException(
                "Exact-output maximum-mass proof drifted: " + commitId);
        }
        detachedCapacity.Validate(
            envelope.ownerStableId,
            envelope.facilityInstanceId,
            proof,
            envelope.capacitySourceDigest,
            envelope.requiredMinimumCapacityGrams);
        ValidateApplied(owner, envelope, physical);
        ValidateCapabilitySemantics(
            owner,
            descriptor,
            proof,
            physical,
            hasPending,
            envelope);
    }

    private void ValidateCapabilitySemantics(
        Owner owner,
        ProductionOutputCapabilityDescriptor descriptor,
        ProductionOutputBatchMaximumMassProof proof,
        FacilityBufferPlannedOutputRestoreBatchSnapshot physical,
        bool isPendingPhysical,
        ProductionExactOutputPublicationSaveData envelope)
    {
        if (!semanticValidators.RequiresValidation(descriptor))
            return;
        if (detachedCapacityProjection == null)
        {
            throw new InvalidOperationException(
                "Exact-output semantic restore requires detached facility projection: "
                + owner.Output.pendingCommitId);
        }
        ProductionOutputDetachedFacilityCapacityProjection facilityCapacity =
            detachedCapacityProjection.Capture(
                owner.Bill.billId,
                owner.Bill.buildingInstanceId,
                proof);
        semanticValidators.Validate(
            new ProductionResolvedOutputRestoreValidationContext(
                owner.Bill,
                owner.Output,
                descriptor,
                proof,
                facilityCapacity,
                physical,
                isPendingPhysical,
                envelope));
    }

    private static void ValidateUnapplied(
        Owner owner,
        FacilityBufferPlannedOutputRestoreBatchSnapshot physical,
        ProductionOutputBatchMaximumMassProof proof)
    {
        if (physical == null
            || physical.TotalQuantity <= 0
            || physical.TotalMassGrams <= 0L
            || physical.TotalMassGrams > proof.MaximumBatchMassGrams
            || physical.Stacks.Count == 0
            || physical.Stacks.Any(value => value == null
                || !string.Equals(
                    value.ItemId,
                    owner.Output.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    value.DestinationId,
                    owner.Bill.outputDestinationId,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Unapplied exact-output physical publication is inconsistent: "
                + owner.Output.pendingCommitId);
        }
    }

    private static void ValidateApplied(
        Owner owner,
        ProductionExactOutputPublicationSaveData envelope,
        FacilityBufferPlannedOutputRestoreBatchSnapshot physical)
    {
        ProductionExactOutputPublicationStackSaveData[] expected =
            envelope.stacks
                .OrderBy(value => value.stackOrdinal)
                .ToArray();
        FacilityBufferPlannedOutputRestoreStackSnapshot[] actual =
            physical.Stacks
                .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
                .ThenBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
        bool exact = string.Equals(
                physical.BatchCommitId,
                envelope.commitId,
                StringComparison.Ordinal)
            && string.Equals(
                physical.OutcomeFingerprint,
                envelope.outcomeFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                physical.PlannedOutputFingerprint,
                envelope.plannedOutputFingerprint,
                StringComparison.Ordinal)
            && physical.TotalMassGrams == envelope.exactMassGrams
            && expected.Length == actual.Length;
        for (int index = 0; exact && index < expected.Length; index++)
        {
            exact = string.Equals(
                    expected[index].outputLineId,
                    actual[index].OutputLineId,
                    StringComparison.Ordinal)
                && string.Equals(
                    expected[index].stackId,
                    actual[index].StackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    expected[index].itemId,
                    actual[index].ItemId,
                    StringComparison.Ordinal)
                && expected[index].quantity == actual[index].Quantity
                && expected[index].massGrams == actual[index].MassGrams
                && string.Equals(
                    expected[index].componentSignature,
                    actual[index].ComponentSignature,
                    StringComparison.Ordinal)
                && string.Equals(
                    expected[index].itemInstanceId,
                    actual[index].ItemInstanceId,
                    StringComparison.Ordinal);
        }
        if (!exact)
        {
            throw new InvalidOperationException(
                "Applied exact-output envelope does not match physical publication: "
                + owner.Output.pendingCommitId);
        }
    }

    private readonly struct Owner
    {
        internal Owner(
            ProductionBillSaveData bill,
            ProductionResolvedOutputSaveData output)
        {
            Bill = bill;
            Output = output;
        }

        internal ProductionBillSaveData Bill { get; }
        internal ProductionResolvedOutputSaveData Output { get; }
    }
}
