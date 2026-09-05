using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class SurgicalPartProductionOutputHandler :
    IProductionOutputHandler,
    IIdempotentProductionOutputHandler
{
    public const string HandlerCapabilityId =
        "production-output:surgical-part";
    public const int HandlerContractVersion = 2;
    public const string HandlerComponentCodecId =
        "production-output-codec:surgical-part";
    public const int HandlerComponentCodecVersion = 1;

    public static readonly string ProstheticArmOutputId =
        SurgeryItemDefinitions.GetProstheticItemId("arm:left");
    public static readonly string ProstheticLegOutputId =
        SurgeryItemDefinitions.GetProstheticItemId("leg:left");
    public static readonly string ArtificialEyeOutputId =
        SurgeryItemDefinitions.GetProstheticItemId("eye:left");

    private readonly ISurgicalPartPreparedOutputRuntime preparedParts;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly IProductionFacilityHandleQuery facilities;
    private readonly IProductionOutputDestinationAuthorityRuntime destinations;
    private readonly IProductionOutputBufferCapacityProjector capacityProjector;
    private readonly IProductionOutputMaximumMassRegistry outputMaximumMass;
    private readonly IItemInstanceRepository itemInstances;
    private readonly ISurgicalPartOutputAdmissionPort admission;
    private readonly ISurgicalPartOutputPublicationPort publication;

    public SurgicalPartProductionOutputHandler(
        ISurgicalPartRuntime parts,
        IItemDefinitionCatalog itemCatalog,
        IProductionFacilityHandleQuery facilities,
        IProductionOutputDestinationAuthorityRuntime destinations,
        IProductionOutputBufferCapacityProjector capacityProjector,
        IProductionOutputMaximumMassRegistry outputMaximumMass,
        IItemInstanceRepository itemInstances,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication)
    {
        preparedParts = parts as ISurgicalPartPreparedOutputRuntime
            ?? throw new ArgumentException(
                "Surgical-part runtime does not implement prepared-output custody.",
                nameof(parts));
        this.itemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.destinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
        this.capacityProjector = capacityProjector
            ?? throw new ArgumentNullException(nameof(capacityProjector));
        this.outputMaximumMass = outputMaximumMass
            ?? throw new ArgumentNullException(nameof(outputMaximumMass));
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.admission = new SurgicalPartOutputAdmissionPort(
            admission ?? throw new ArgumentNullException(nameof(admission)));
        this.publication = new SurgicalPartOutputPublicationPort(
            publication ?? throw new ArgumentNullException(nameof(publication)));
    }

    internal SurgicalPartProductionOutputHandler(
        ISurgicalPartPreparedOutputRuntime preparedParts,
        ISurgicalPartOutputAdmissionPort admission,
        ISurgicalPartOutputPublicationPort publication)
    {
        this.preparedParts = preparedParts
            ?? throw new ArgumentNullException(nameof(preparedParts));
        this.admission = admission ?? throw new ArgumentNullException(nameof(admission));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
    }

    public string CapabilityId => HandlerCapabilityId;
    public int ContractVersion => HandlerContractVersion;
    public string ComponentCodecId => HandlerComponentCodecId;
    public int ComponentCodecVersion => HandlerComponentCodecVersion;
    public bool SupportsAutomaticSelection => true;

    public bool CanHandle(string itemId) =>
        SurgicalPartProductionOutputSemantics.TryResolveDefinition(
            itemId,
            out _,
            out _);

    public bool TryProduce(
        ProductionOutputContext context,
        out string failureReason)
    {
        bool succeeded = TryProduceIdempotent(context, out DomainFailure failure);
        failureReason = succeeded ? string.Empty : failure.Code.ToString();
        return succeeded;
    }

    public bool TryProduceIdempotent(
        ProductionOutputContext context,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!CanHandle(context.ItemId)
            || context.Amount != 1
            || context.Facility == null
            || !ProductionOutputDefinition.IsCanonicalOutputLineId(
                context.OutputLineId)
            || !IsCanonicalRequired(context.CommitId))
        {
            failure = new DomainFailure(FailureCode.SurgeryPartUnavailable);
            return false;
        }

        ResolveDefinition(context.ItemId, out string nodeId, out SurgicalPartKind kind);
        string displayName = itemCatalog
            .GetRequired(new ItemDefinitionId(context.ItemId))
            .DisplayName;
        if (!preparedParts.TryPrepareCraftedOutput(
                context.ItemId,
                nodeId,
                displayName,
                kind,
                context.WorkerQuality,
                context.CommitId,
                out SurgicalPartPreparedOutput prepared,
                out failure))
        {
            return false;
        }
        if (!prepared.IsReplay
            && !((ItemInstanceId)prepared.PhysicalItemInstanceId).IsValid)
        {
            if (itemInstances == null)
            {
                failure = Fail("physical-item-instance-authority-missing");
                return false;
            }
            prepared.PhysicalItemInstanceId = itemInstances
                .AllocateItemInstanceId().Value;
        }
        ProductionFacilityHandle facility;
        try
        {
            facility = facilities.CaptureFacility(context.Facility);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            failure = Fail("facility-capture-failed", exception.Message);
            return false;
        }
        string expectedDestinationId = ProductionOutputDestinationId
            .FromFacility(facility.InstanceId)
            .Value;
        if (!string.Equals(
                context.OutputDestinationId,
                expectedDestinationId,
                StringComparison.Ordinal))
        {
            failure = Fail(
                "output-destination-mismatch",
                context.OutputDestinationId);
            return false;
        }
        ProductionOutputBatchMaximumMassProof maximumMassProof;
        ProductionOutputBufferCapacitySourceSnapshot capacity;
        try
        {
            ProductionOutputMaximumMassProjection maximumProjection =
                outputMaximumMass.CaptureAutomatic(
                    context.OutputLineId,
                    context.ItemId,
                    context.Amount);
            if (!string.Equals(
                    maximumProjection.Descriptor.CapabilityId,
                    HandlerCapabilityId,
                    StringComparison.Ordinal)
                || maximumProjection.Descriptor.CapabilityVersion
                    != HandlerContractVersion
                || !string.Equals(
                    maximumProjection.Descriptor.ComponentCodecId,
                    HandlerComponentCodecId,
                    StringComparison.Ordinal)
                || maximumProjection.Descriptor.ComponentCodecVersion
                    != HandlerComponentCodecVersion)
            {
                failure = Fail("maximum-mass-capability-execution-drift");
                return false;
            }
            maximumMassProof = new ProductionOutputBatchMaximumMassProof(
                new[] { maximumProjection });
            capacity = capacityProjector.CaptureSource(
                facility,
                maximumMassProof);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failure = Fail("maximum-mass-proof-invalid", exception.Message);
            return false;
        }
        ItemInstanceComponentSaveData replayComponent =
            SurgicalPartPreparedOutputComponentCodec.Create(prepared);
        string expectedOutcomeFingerprint = CreateOutcomeFingerprint(
            prepared,
            context.OutputLineId,
            SurgicalPartPreparedOutputComponentCodec.Hash(
                replayComponent.ToCanonicalString()),
            maximumMassProof,
            capacity);
        if (publication.TryCaptureBatch(
                context.CommitId,
                allowAcknowledged: true,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot existing,
                out bool acknowledged,
                out _,
                out string captureFailure))
        {
            if (!prepared.IsReplay
                || !TryValidateExistingBatch(
                    context,
                    facility,
                    existing,
                    acknowledged,
                    expectedOutcomeFingerprint,
                    maximumMassProof,
                    out failure))
            {
                failure = !failure.IsFailure
                    ? Fail("existing-publication-conflict")
                    : failure;
                return false;
            }
            return preparedParts.TryValidateCommittedCraftedOutput(
                context.CommitId,
                requireAcknowledged: false,
                out _,
                out failure);
        }
        if (prepared.IsReplay || !IsMissingBatch(captureFailure))
        {
            failure = Fail(
                prepared.IsReplay
                    ? "commit-replay-batch-missing"
                    : "existing-publication-conflict",
                captureFailure);
            return false;
        }
        if (!destinations.TryEnsureCapacitySource(
                facility,
                capacity,
                out FacilityBufferCapacityProfile profile,
                out string destinationFailure))
        {
            failure = Fail("output-destination-unavailable", destinationFailure);
            return false;
        }

        return TryPublishPreparedOutput(
            prepared,
            profile,
            facility.Position,
            context.OutputLineId,
            maximumMassProof,
            capacity,
            out failure);
    }

    internal bool TryPublishPreparedOutputForEditorTest(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferCapacityProfile profile,
        Vector2Int position,
        string outputLineId,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputBufferCapacitySourceSnapshot capacity,
        out DomainFailure failure) => TryPublishPreparedOutput(
        prepared,
        profile,
        position,
        outputLineId,
        maximumMassProof,
        capacity,
        out failure);

    private bool TryPublishPreparedOutput(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferCapacityProfile profile,
        Vector2Int position,
        string outputLineId,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputBufferCapacitySourceSnapshot capacity,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (prepared == null
            || profile == null
            || prepared.IsReplay
            || !((ItemInstanceId)prepared.PhysicalItemInstanceId).IsValid
            || !TryValidateProofAndCapacity(
                prepared,
                outputLineId,
                profile,
                maximumMassProof,
                capacity)
            || position != profile.DropPosition
            || capacity.RequiredMinimumCapacityGrams <= 0L
            || !string.Equals(
                capacity.SourceDigest,
                capacity.SourceDigest?.Trim(),
                StringComparison.Ordinal))
        {
            failure = Fail("prepared-output-request-invalid");
            return false;
        }
        ItemInstanceComponentSaveData component =
            SurgicalPartPreparedOutputComponentCodec.Create(prepared);
        string componentFingerprint = SurgicalPartPreparedOutputComponentCodec.Hash(
            component.ToCanonicalString());
        string outcomeFingerprint = CreateOutcomeFingerprint(
            prepared,
            outputLineId,
            componentFingerprint,
            maximumMassProof,
            capacity);
        FacilityBufferPlannedOutputRequest request = new(
            SurgicalPartProductionOutputSemantics.PublicationOperationId(
                prepared.CommitId),
            prepared.CommitId,
            outcomeFingerprint,
            profile.DestinationId,
            position,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            new[]
            {
                new FacilityBufferPlannedOutputSlice(
                    outputLineId,
                    new PhysicalItemMassSubject(
                        new ItemDefinitionId(prepared.ItemId),
                        prepared.PhysicalItemInstanceId,
                        PhysicalItemMassSubjectKind.GenericDefinition,
                        Array.Empty<PhysicalItemComponentSnapshot>(),
                        string.Empty),
                    1,
                    new[] { component },
                    componentFingerprint)
            },
            capacity.SourceDigest,
            capacity.RequiredMinimumCapacityGrams,
            profile.AuthorityDigest);
        if (!admission.TryReserve(
                request,
                out FacilityBufferPlannedOutputToken token,
                out FacilityBufferMassAdmissionFailureCode admissionCode,
                out string admissionFailure))
        {
            failure = new DomainFailure(
                admissionCode == FacilityBufferMassAdmissionFailureCode.CapacityUnavailable
                    ? FailureCode.ProductionOutputSpaceUnavailable
                    : FailureCode.ProductionOutputUnavailable,
                prepared.CommitId,
                admissionFailure);
            return false;
        }
        if (token.ReservedMassGrams
            > maximumMassProof.MaximumBatchMassGrams)
        {
            bool released = admission.TryRelease(
                token,
                out _,
                out string releaseFailure);
            failure = Fail(
                released
                    ? "surgical-part-output-mass-exceeds-capability-maximum"
                    : "surgical-part-output-maximum-release-failed",
                releaseFailure);
            return false;
        }

        if (!publication.TryPublish(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt published,
                out _,
                out string publicationFailure))
        {
            admission.TryRelease(token, out _, out _);
            failure = Fail("publication-failed", publicationFailure);
            return false;
        }

        if (!preparedParts.TryCommitCraftedOutput(prepared, published, out failure))
        {
            RollbackUncommitted(token, published, prepared, out string rollbackFailure);
            if (rollbackFailure.Length > 0)
                failure = Fail("runtime-join-rollback-failed", rollbackFailure);
            return false;
        }

        if (!admission.TryCommit(
                token,
                published,
                out FacilityBufferPlannedOutputReceipt committed,
                out _,
                out string commitFailure)
            || committed.CommittedMassGrams != token.ReservedMassGrams)
        {
            RollbackUncommitted(token, published, prepared, out string rollbackFailure);
            failure = Fail(
                "admission-commit-failed",
                commitFailure + (rollbackFailure.Length == 0
                    ? string.Empty
                    : $";rollback={rollbackFailure}"));
            return false;
        }
        return true;
    }

    public bool TryAcknowledge(string commitId, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!preparedParts.TryValidateCommittedCraftedOutput(
                commitId,
                requireAcknowledged: false,
                out SurgicalPartPublishedOutputSnapshot joined,
                out failure))
        {
            return false;
        }
        if (joined.Acknowledged)
            return true;

        if (!publication.TryCapturePending(
                commitId,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot pending,
                out _,
                out string captureFailure)
            || pending.Stacks.Count != 1
            || !string.Equals(
                pending.Stacks[0].StackId,
                joined.StackId,
                StringComparison.Ordinal)
            || pending.Stacks[0].MassGrams != joined.MassGrams)
        {
            failure = Fail("acknowledgement-join-failed", captureFailure);
            return false;
        }
        if (!publication.TryAcknowledge(
                pending,
                out _,
                out string acknowledgementFailure))
        {
            failure = Fail("acknowledgement-failed", acknowledgementFailure);
            return false;
        }
        return preparedParts.TryValidateCommittedCraftedOutput(
            commitId,
            requireAcknowledged: true,
            out _,
            out failure);
    }

    public bool TryCaptureCommittedOutput(
        ProductionOutputContext context,
        out ProductionCommittedOutputSnapshot snapshot,
        out DomainFailure failure)
    {
        snapshot = null;
        failure = DomainFailure.None;
        if (!CanHandle(context.ItemId)
            || context.Amount != 1
            || context.Facility == null
            || itemCatalog == null
            || facilities == null
            || destinations == null
            || capacityProjector == null
            || outputMaximumMass == null
            || !ProductionOutputDefinition.IsCanonicalOutputLineId(
                context.OutputLineId)
            || !IsCanonicalRequired(context.CommitId))
        {
            failure = Fail("committed-output-snapshot-context-invalid");
            return false;
        }
        if (!preparedParts.TryValidateCommittedCraftedOutput(
                context.CommitId,
                requireAcknowledged: false,
                out SurgicalPartPublishedOutputSnapshot joined,
                out failure))
        {
            return false;
        }

        ResolveDefinition(
            context.ItemId,
            out string nodeId,
            out SurgicalPartKind kind);
        string displayName = itemCatalog
            .GetRequired(new ItemDefinitionId(context.ItemId))
            .DisplayName;
        if (!preparedParts.TryPrepareCraftedOutput(
                context.ItemId,
                nodeId,
                displayName,
                kind,
                context.WorkerQuality,
                context.CommitId,
                out SurgicalPartPreparedOutput prepared,
                out failure)
            || !prepared.IsReplay)
        {
            if (!failure.IsFailure)
                failure = Fail("committed-output-snapshot-replay-missing");
            return false;
        }

        ProductionFacilityHandle facility;
        ProductionOutputBatchMaximumMassProof maximumMassProof;
        ProductionOutputBufferCapacitySourceSnapshot capacity;
        try
        {
            facility = facilities.CaptureFacility(context.Facility);
            ProductionOutputMaximumMassProjection projection =
                outputMaximumMass.CaptureAutomatic(
                    context.OutputLineId,
                    context.ItemId,
                    context.Amount);
            maximumMassProof = new ProductionOutputBatchMaximumMassProof(
                new[] { projection });
            capacity = capacityProjector.CaptureSource(
                facility,
                maximumMassProof);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failure = Fail(
                "committed-output-snapshot-authority-invalid",
                exception.Message);
            return false;
        }
        ItemInstanceComponentSaveData component =
            SurgicalPartPreparedOutputComponentCodec.Create(prepared);
        string expectedOutcomeFingerprint = CreateOutcomeFingerprint(
            prepared,
            context.OutputLineId,
            SurgicalPartPreparedOutputComponentCodec.Hash(
                component.ToCanonicalString()),
            maximumMassProof,
            capacity);
        if (!publication.TryCaptureBatch(
                context.CommitId,
                allowAcknowledged: true,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                out bool acknowledged,
                out _,
                out string captureFailure)
            || !TryValidateExistingBatch(
                context,
                facility,
                batch,
                acknowledged,
                expectedOutcomeFingerprint,
                maximumMassProof,
                out failure)
            || batch.TotalMassGrams != joined.MassGrams)
        {
            if (!failure.IsFailure)
            {
                failure = Fail(
                    "committed-output-snapshot-missing",
                    captureFailure);
            }
            return false;
        }
        string expectedDestinationId = ProductionOutputDestinationId
            .FromFacility(facility.InstanceId)
            .Value;
        if (!destinations.TryValidate(
                facility,
                out FacilityBufferCapacityProfile profile,
                out string destinationFailure)
            || !string.Equals(
                context.OutputDestinationId,
                expectedDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                profile.DestinationId,
                expectedDestinationId,
                StringComparison.Ordinal)
            || profile.DropPosition != facility.Position
            || profile.MaxMassGrams < capacity.RequiredMinimumCapacityGrams)
        {
            failure = Fail(
                "committed-output-snapshot-destination-invalid",
                destinationFailure);
            return false;
        }
        ProductionCommittedOutputStackSnapshot[] stacks = batch.Stacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => new ProductionCommittedOutputStackSnapshot(
                value.OutputLineId,
                value.StackId,
                value.ItemId,
                value.Quantity,
                value.MassGrams,
                value.ComponentSignature,
                value.ItemInstanceId))
            .ToArray();
        snapshot = new ProductionCommittedOutputSnapshot(
            context.CommitId,
            facility.InstanceId.Value,
            HandlerCapabilityId,
            HandlerContractVersion,
            HandlerComponentCodecId,
            HandlerComponentCodecVersion,
            maximumMassProof.SourceDigest,
            maximumMassProof.MaximumBatchMassGrams,
            capacity.SourceDigest,
            capacity.RequiredMinimumCapacityGrams,
            batch.TotalMassGrams,
            batch.OutcomeFingerprint,
            batch.PlannedOutputFingerprint,
            profile.DestinationId,
            profile.DropPosition.x,
            profile.DropPosition.y,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            acknowledged,
            stacks);
        return true;
    }

    private void RollbackUncommitted(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt published,
        SurgicalPartPreparedOutput prepared,
        out string failureReason)
    {
        List<string> failures = new();
        if (!preparedParts.TryRollbackCraftedOutput(prepared, published, out string runtimeFailure))
            failures.Add(runtimeFailure);
        if (!publication.TryRollback(published, out _, out string publicationFailure))
            failures.Add(publicationFailure);
        if (!admission.TryRelease(token, out _, out string admissionFailure))
        {
            failures.Add(admissionFailure);
        }
        failureReason = string.Join(";", failures.Where(value => value.Length > 0));
    }

    private static DomainFailure Fail(string detail, string reason = "") =>
        new(FailureCode.ProductionOutputUnavailable, reason ?? string.Empty, detail);

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string CreateOutcomeFingerprint(
        SurgicalPartPreparedOutput prepared,
        string outputLineId,
        string componentFingerprint,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputBufferCapacitySourceSnapshot capacity)
    {
        return SurgicalPartProductionOutputSemantics.CreateOutcomeFingerprint(
            prepared.CommitId,
            outputLineId,
            prepared.ItemId,
            componentFingerprint,
            maximumMassProof,
            capacity);
    }

    private static bool TryValidateProofAndCapacity(
        SurgicalPartPreparedOutput prepared,
        string outputLineId,
        FacilityBufferCapacityProfile profile,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputBufferCapacitySourceSnapshot capacity)
    {
        if (prepared == null
            || profile == null
            || maximumMassProof == null
            || !ProductionOutputDefinition.IsCanonicalOutputLineId(outputLineId)
            || maximumMassProof.Projections.Count != 1)
        {
            return false;
        }
        ProductionOutputMaximumMassProjection projection =
            maximumMassProof.Projections[0];
        ProductionOutputCapabilityDescriptor descriptor =
            projection.Descriptor;
        long expectedBatchMinimum;
        try
        {
            expectedBatchMinimum = checked(
                maximumMassProof.MaximumBatchMassGrams
                * capacity.CycleCapacity);
        }
        catch (OverflowException)
        {
            return false;
        }
        return string.Equals(
                descriptor.OutputLineId,
                outputLineId,
                StringComparison.Ordinal)
            && string.Equals(
                descriptor.ItemId,
                prepared.ItemId,
                StringComparison.Ordinal)
            && string.Equals(
                descriptor.CapabilityId,
                HandlerCapabilityId,
                StringComparison.Ordinal)
            && descriptor.CapabilityVersion == HandlerContractVersion
            && string.Equals(
                descriptor.ComponentCodecId,
                HandlerComponentCodecId,
                StringComparison.Ordinal)
            && descriptor.ComponentCodecVersion == HandlerComponentCodecVersion
            && projection.MaximumQuantity == 1
            && capacity.BatchMinimumCapacityGrams == expectedBatchMinimum
            && capacity.MaximumBatchMassGrams
                >= maximumMassProof.MaximumBatchMassGrams
            && profile.MaxMassGrams
                >= capacity.RequiredMinimumCapacityGrams;
    }

    private static bool TryValidateExistingBatch(
        ProductionOutputContext context,
        ProductionFacilityHandle facility,
        FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
        bool acknowledged,
        string expectedOutcomeFingerprint,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        FacilityBufferPlannedOutputRestoreStackSnapshot stack =
            batch?.Stacks?.Count == 1 ? batch.Stacks[0] : null;
        string destinationId = ProductionOutputDestinationId
            .FromFacility(facility.InstanceId)
            .Value;
        bool exact = batch != null
            && stack != null
            && string.Equals(
                batch.BatchCommitId,
                context.CommitId,
                StringComparison.Ordinal)
            && string.Equals(
                batch.OutcomeFingerprint,
                expectedOutcomeFingerprint,
                StringComparison.Ordinal)
            && batch.TotalQuantity == 1
            && batch.TotalMassGrams > 0L
            && batch.TotalMassGrams <= maximumMassProof.MaximumBatchMassGrams
            && string.Equals(
                stack.OutputLineId,
                context.OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.ItemId,
                context.ItemId,
                StringComparison.Ordinal)
            && stack.Quantity == 1
            && stack.MassGrams == batch.TotalMassGrams
            && !string.IsNullOrEmpty(stack.ComponentSignature)
            && (acknowledged
                || stack.State == WorldItemStackState.FacilityOutputBuffer
                    && stack.Position == facility.Position
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal));
        if (exact)
            return true;
        failure = Fail("commit-replay-batch-mismatch");
        return false;
    }

    private static bool IsMissingBatch(string failureReason) =>
        (failureReason ?? string.Empty).StartsWith(
            "planned-output-batch-missing:",
            StringComparison.Ordinal);

    private static void ResolveDefinition(
        string itemId,
        out string nodeId,
        out SurgicalPartKind kind)
    {
        SurgicalPartProductionOutputSemantics.ResolveDefinition(
            itemId,
            out nodeId,
            out kind);
    }
}

/// <summary>
/// Pure maximum-mass companion for crafted surgical parts. The surgical
/// component describes the fitted node and kind but adds no separate matter;
/// production therefore uses the definition mass as its complete bound.
/// </summary>
public sealed class SurgicalPartProductionOutputMaximumMassCapability :
    IProductionOutputMaximumMassCapability
{
    public string CapabilityId =>
        SurgicalPartProductionOutputHandler.HandlerCapabilityId;
    public int ContractVersion =>
        SurgicalPartProductionOutputHandler.HandlerContractVersion;
    public string ComponentCodecId =>
        SurgicalPartProductionOutputHandler.HandlerComponentCodecId;
    public int ComponentCodecVersion =>
        SurgicalPartProductionOutputHandler.HandlerComponentCodecVersion;
    public bool SupportsAutomaticSelection => true;

    public bool CanHandle(string itemId) =>
        SurgicalPartProductionOutputSemantics.TryResolveDefinition(
            itemId,
            out _,
            out _);

    public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity,
        IPhysicalItemMassQuery massQuery) =>
        ProductionOutputDefinitionMaximumMassProjection.Capture(
            this,
            descriptor,
            maximumQuantity,
            massQuery);
}

internal interface ISurgicalPartOutputAdmissionPort
{
    bool TryReserve(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryCommit(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out FacilityBufferPlannedOutputReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
    bool TryRelease(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason);
}

internal interface ISurgicalPartOutputPublicationPort
{
    bool TryCaptureBatch(
        string batchCommitId,
        bool allowAcknowledged,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out bool acknowledged,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryPublish(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryRollback(
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryCapturePending(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryAcknowledge(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
}

internal sealed class SurgicalPartOutputAdmissionPort :
    ISurgicalPartOutputAdmissionPort
{
    private readonly IFacilityBufferMassAdmissionService inner;

    internal SurgicalPartOutputAdmissionPort(
        IFacilityBufferMassAdmissionService inner) =>
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public bool TryReserve(
        FacilityBufferPlannedOutputRequest request,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason) => inner.TryReservePlannedOutput(
        request,
        out token,
        out failureCode,
        out failureReason);

    public bool TryCommit(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt publication,
        out FacilityBufferPlannedOutputReceipt receipt,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason) => inner.TryCommitPlannedOutput(
        token,
        publication,
        out receipt,
        out failureCode,
        out failureReason);

    public bool TryRelease(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason) => inner.TryReleasePlannedOutput(
        token,
        FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
        out failureCode,
        out failureReason);
}

internal sealed class SurgicalPartOutputPublicationPort :
    ISurgicalPartOutputPublicationPort
{
    private readonly IFacilityBufferPlannedOutputPublicationService inner;

    internal SurgicalPartOutputPublicationPort(
        IFacilityBufferPlannedOutputPublicationService inner) =>
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public bool TryCaptureBatch(
        string batchCommitId,
        bool allowAcknowledged,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out bool acknowledged,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason) => inner.TryCaptureBatch(
        batchCommitId,
        allowAcknowledged,
        out candidate,
        out acknowledged,
        out failureCode,
        out failureReason);

    public bool TryPublish(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason) => inner.TryPublishFullBatch(
        token,
        out receipt,
        out failureCode,
        out failureReason);

    public bool TryRollback(
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason) => inner.TryRollbackPublishedBatch(
        receipt,
        out failureCode,
        out failureReason);

    public bool TryCapturePending(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason) => inner.TryCapturePendingBatch(
        batchCommitId,
        out candidate,
        out failureCode,
        out failureReason);

    public bool TryAcknowledge(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason) => inner.TryAcknowledgeRestoreCandidate(
        candidate,
        out failureCode,
        out failureReason);
}

internal sealed class SurgicalPartPreparedOutput
{
    internal string ItemId { get; set; }
    internal string PhysicalItemInstanceId { get; set; }
    internal string PartInstanceId { get; set; }
    internal string NodeId { get; set; }
    internal string DisplayName { get; set; }
    internal SurgicalPartKind Kind { get; set; }
    internal float Quality { get; set; }
    internal string CommitId { get; set; }
    internal int ExpectedSequence { get; set; }
    internal bool IsReplay { get; set; }
}

internal readonly struct SurgicalPartPublishedOutputSnapshot
{
    internal SurgicalPartPublishedOutputSnapshot(
        string stackId,
        string itemInstanceId,
        long massGrams,
        bool acknowledged)
    {
        StackId = stackId;
        ItemInstanceId = itemInstanceId;
        MassGrams = massGrams;
        Acknowledged = acknowledged;
    }

    internal string StackId { get; }
    internal string ItemInstanceId { get; }
    internal long MassGrams { get; }
    internal bool Acknowledged { get; }
}

internal interface ISurgicalPartPreparedOutputRuntime
{
    bool TryPrepareCraftedOutput(
        string itemId,
        string nodeId,
        string displayName,
        SurgicalPartKind kind,
        float quality,
        string commitId,
        out SurgicalPartPreparedOutput prepared,
        out DomainFailure failure);
    bool TryCommitCraftedOutput(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out DomainFailure failure);
    bool TryRollbackCraftedOutput(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out string failureReason);
    bool TryValidateCommittedCraftedOutput(
        string commitId,
        bool requireAcknowledged,
        out SurgicalPartPublishedOutputSnapshot joined,
        out DomainFailure failure);
}

internal static class SurgicalPartPreparedOutputComponentCodec
{
    internal const string ComponentTypeId = "medical:surgical-part-output";
    private const string PartIdKey = "part-instance-id";
    private const string NodeIdKey = "node-id";
    private const string KindKey = "kind";
    private const string QualityKey = "quality";
    private const string CommitIdKey = "production-commit-id";

    internal static ItemInstanceComponentSaveData Create(
        SurgicalPartPreparedOutput prepared) => new()
    {
        componentTypeId = ComponentTypeId,
        schemaVersion = 1,
        affectsStacking = true,
        values = new List<ItemStateValueSaveData>
        {
            String(PartIdKey, prepared.PartInstanceId),
            String(NodeIdKey, prepared.NodeId),
            Integer(KindKey, (int)prepared.Kind),
            Decimal(QualityKey, prepared.Quality),
            String(CommitIdKey, prepared.CommitId)
        }
    };

    internal static bool TryRead(
        IEnumerable<ItemInstanceComponentSaveData> components,
        out string partInstanceId,
        out string nodeId,
        out SurgicalPartKind kind,
        out float quality,
        out string commitId)
    {
        partInstanceId = string.Empty;
        nodeId = string.Empty;
        kind = default;
        quality = 0f;
        commitId = string.Empty;
        ItemInstanceComponentSaveData[] matches = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(value => value != null
                && string.Equals(value.componentTypeId, ComponentTypeId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || matches[0].schemaVersion != 1
            || !matches[0].affectsStacking)
        {
            return false;
        }
        IReadOnlyList<ItemStateValueSaveData> values = matches[0].values
            ?? new List<ItemStateValueSaveData>();
        if (!TryString(values, PartIdKey, out partInstanceId)
            || !TryString(values, NodeIdKey, out nodeId)
            || !TryInteger(values, KindKey, out long kindValue)
            || kindValue < int.MinValue
            || kindValue > int.MaxValue
            || !Enum.IsDefined(typeof(SurgicalPartKind), (int)kindValue)
            || !TryDecimal(values, QualityKey, out double qualityValue)
            || qualityValue < 0.1d
            || qualityValue > 1.75d
            || !TryString(values, CommitIdKey, out commitId))
        {
            return false;
        }
        kind = (SurgicalPartKind)kindValue;
        quality = (float)qualityValue;
        return true;
    }

    internal static string Hash(string canonical)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical ?? string.Empty));
        StringBuilder text = new(bytes.Length * 2);
        foreach (byte value in bytes)
            text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return text.ToString();
    }

    private static ItemStateValueSaveData String(string key, string value) => new()
    {
        key = key,
        kind = ItemStateValueKind.String,
        stringValue = value ?? string.Empty
    };

    private static ItemStateValueSaveData Integer(string key, long value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Integer,
        integerValue = value
    };

    private static ItemStateValueSaveData Decimal(string key, double value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Decimal,
        decimalValue = value
    };

    private static bool TryString(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out string result)
    {
        ItemStateValueSaveData[] found = values.Where(value => value != null
                && value.kind == ItemStateValueKind.String
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = found.Length == 1 ? found[0].stringValue ?? string.Empty : string.Empty;
        return found.Length == 1 && IsCanonicalRequired(result);
    }

    private static bool TryInteger(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out long result)
    {
        ItemStateValueSaveData[] found = values.Where(value => value != null
                && value.kind == ItemStateValueKind.Integer
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = found.Length == 1 ? found[0].integerValue : 0L;
        return found.Length == 1;
    }

    private static bool TryDecimal(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out double result)
    {
        ItemStateValueSaveData[] found = values.Where(value => value != null
                && value.kind == ItemStateValueKind.Decimal
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = found.Length == 1 ? found[0].decimalValue : 0d;
        return found.Length == 1 && !double.IsNaN(result) && !double.IsInfinity(result);
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
