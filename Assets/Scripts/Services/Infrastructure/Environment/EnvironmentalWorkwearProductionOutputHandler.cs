using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Publishes authored apparel output through the common FacilityBuffer gram
/// reservation and atomic planned-output authority. The production aggregate
/// owns the durable commit ID; this handler never repairs state by rewriting an
/// already published item.
/// </summary>
public sealed class EnvironmentalWorkwearProductionOutputHandler :
    IProductionOutputHandler,
    IDomainFailureProductionOutputHandler,
    IIdempotentProductionOutputHandler
{
    public const string HandlerCapabilityId =
        "production-output:environmental-workwear";
    public const int HandlerContractVersion = 2;
    public const string HandlerComponentCodecId =
        "production-output-codec:apparel-state";
    public const int HandlerComponentCodecVersion = 3;

    private readonly IApparelDefinitionCatalog apparelCatalog;
    private readonly ITextileMaterialCatalog materialCatalog;
    private readonly IItemInstanceRepository itemInstances;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IGameClock clock;
    private readonly IProductionFacilityHandleQuery facilities;
    private readonly IProductionOutputDestinationAuthorityRuntime destinations;
    private readonly IProductionOutputBufferCapacityProjector capacityProjector;
    private readonly IFacilityBufferMassAdmissionService admission;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;
    private readonly IProductionOutputMaximumMassRegistry outputMaximumMass;

    public EnvironmentalWorkwearProductionOutputHandler(
        IApparelDefinitionCatalog apparelCatalog,
        ITextileMaterialCatalog materialCatalog,
        IItemInstanceRepository itemInstances,
        IPhysicalItemMassQuery massQuery,
        IGameClock clock,
        IProductionFacilityHandleQuery facilities,
        IProductionOutputDestinationAuthorityRuntime destinations,
        IProductionOutputBufferCapacityProjector capacityProjector,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication,
        IProductionOutputMaximumMassRegistry outputMaximumMass)
    {
        this.apparelCatalog = apparelCatalog
            ?? throw new ArgumentNullException(nameof(apparelCatalog));
        this.materialCatalog = materialCatalog
            ?? throw new ArgumentNullException(nameof(materialCatalog));
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.destinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
        this.capacityProjector = capacityProjector
            ?? throw new ArgumentNullException(nameof(capacityProjector));
        this.admission = admission
            ?? throw new ArgumentNullException(nameof(admission));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
        this.outputMaximumMass = outputMaximumMass
            ?? throw new ArgumentNullException(nameof(outputMaximumMass));
    }

    public string CapabilityId => HandlerCapabilityId;
    public int ContractVersion => HandlerContractVersion;
    public string ComponentCodecId => HandlerComponentCodecId;
    public int ComponentCodecVersion => HandlerComponentCodecVersion;
    public bool SupportsAutomaticSelection => true;

    public bool CanHandle(string itemId) =>
        apparelCatalog.TryGetByItemId(itemId, out _);

    public bool TryProduce(
        ProductionOutputContext context,
        out DomainFailure failure) => TryProduceIdempotent(context, out failure);

    public bool TryProduceIdempotent(
        ProductionOutputContext context,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (context.Facility == null
            || context.Amount <= 0
            || !Canonical(context.CommitId)
            || !ProductionOutputDefinition.IsCanonicalOutputLineId(
                context.OutputLineId)
            || !apparelCatalog.TryGetByItemId(
                context.ItemId,
                out ApparelDefinitionSO apparel))
        {
            failure = InvalidContext(context);
            return false;
        }

        ProductionFacilityHandle facility;
        try
        {
            facility = facilities.CaptureFacility(context.Facility);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            failure = Fail(
                context.ItemId,
                "facility-capture-failed",
                exception.Message);
            return false;
        }

        string expectedDestinationId = ProductionOutputDestinationId
            .FromFacility(facility.InstanceId)
            .Value;
        if (!Canonical(context.OutputDestinationId)
            || !string.Equals(
                context.OutputDestinationId,
                expectedDestinationId,
                StringComparison.Ordinal))
        {
            failure = Fail(
                context.ItemId,
                "output-destination-mismatch",
                context.OutputDestinationId);
            return false;
        }

        TextileMaterialDefinitionSO material = ResolvePrimaryMaterial(
            context.Recipe);
        if (material == null
            || (material.Tags & apparel.AllowedMaterialTags) == 0)
        {
            failure = Fail(
                context.ItemId,
                "apparel-material-invalid");
            return false;
        }
        ProductionOutputMaximumMassProjection maximumProjection;
        ProductionOutputBatchMaximumMassProof maximumMassProof;
        ProductionOutputBufferCapacitySourceSnapshot capacity;
        try
        {
            maximumProjection = outputMaximumMass.CaptureAutomatic(
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
                failure = Fail(
                    context.ItemId,
                    "maximum-mass-capability-execution-drift");
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
            failure = Fail(
                context.ItemId,
                "maximum-mass-proof-invalid",
                exception.Message);
            return false;
        }
        string expectedOutcomeFingerprint = CreateOutcomeFingerprint(
            context,
            facility,
            material,
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
            if (!TryValidateExistingBatch(
                    context,
                    facility,
                    existing,
                    acknowledged,
                    expectedOutcomeFingerprint,
                    out failure))
            {
                return false;
            }
            // Generic production must freeze its durable pending-unit owner
            // before acknowledgement. Acknowledgement is a separate call from
            // ProductionBillRuntime after that owner has been published.
            return true;
        }
        if (!IsMissingBatch(captureFailure))
        {
            failure = Fail(
                context.ItemId,
                "existing-publication-conflict",
                captureFailure);
            return false;
        }

        try
        {
            int craftedDay = Mathf.Max(
                0,
                Mathf.FloorToInt(clock.Time / GameCalendarRules.SecondsPerDay));
            List<FacilityBufferPlannedOutputSlice> slices = new(context.Amount);

            long exactBatchMassGrams = 0L;
            for (int index = 0; index < context.Amount; index++)
            {
                string instanceId = itemInstances.AllocateItemInstanceId().Value;
                ItemInstanceComponentSaveData component =
                    ApparelItemStateCodec.Create(CreateState(
                        apparel,
                        material,
                        context,
                        facility.InstanceId.Value,
                        craftedDay,
                        index));
                string componentFingerprint = Hash(
                    component.ToCanonicalString());
                PhysicalItemMassSubject subject =
                    PhysicalItemMassSubjectAdapter.Create(
                        massQuery,
                        (ItemDefinitionId)context.ItemId,
                        instanceId,
                        new[] { component });
                long unitMassGrams = massQuery.GetQuantityMass(
                    (ItemDefinitionId)context.ItemId,
                    subject,
                    1).Value;
                if (unitMassGrams <= 0L)
                {
                    failure = Fail(
                        context.ItemId,
                        "apparel-output-mass-invalid");
                    return false;
                }

                exactBatchMassGrams = checked(
                    exactBatchMassGrams + unitMassGrams);
                string unitLineId = FormatUnitOutputLineId(
                    context.OutputLineId,
                    index);
                slices.Add(new FacilityBufferPlannedOutputSlice(
                    unitLineId,
                    subject,
                    1,
                    new[] { component },
                    componentFingerprint));
            }
            if (exactBatchMassGrams > maximumMassProof.MaximumBatchMassGrams)
            {
                failure = Fail(
                    context.ItemId,
                    "apparel-output-mass-exceeds-capability-maximum");
                return false;
            }
            if (!destinations.TryEnsureCapacitySource(
                    facility,
                    capacity,
                    out FacilityBufferCapacityProfile profile,
                    out string destinationFailure))
            {
                failure = Fail(
                    context.ItemId,
                    "output-destination-unavailable",
                    destinationFailure,
                    FailureCode.ProductionOutputSpaceUnavailable);
                return false;
            }

            FacilityBufferPlannedOutputRequest request = new(
                EnvironmentalWorkwearProductionOutputSemantics
                    .PublicationOperationPrefix + context.CommitId,
                context.CommitId,
                expectedOutcomeFingerprint,
                profile.DestinationId,
                facility.Position,
                profile.OwnerDomain,
                profile.OwnerOperationId,
                profile.OwnerFacilityId,
                profile.CapacityRevision,
                slices,
                capacity.SourceDigest,
                capacity.RequiredMinimumCapacityGrams,
                profile.AuthorityDigest);
            if (!admission.TryReservePlannedOutput(
                    request,
                    out FacilityBufferPlannedOutputToken token,
                    out FacilityBufferMassAdmissionFailureCode admissionCode,
                    out string admissionFailure))
            {
                failure = Fail(
                    context.ItemId,
                    "planned-output-reservation-failed",
                    admissionFailure,
                    admissionCode ==
                        FacilityBufferMassAdmissionFailureCode.CapacityUnavailable
                        ? FailureCode.ProductionOutputSpaceUnavailable
                        : FailureCode.ProductionOutputUnavailable);
                return false;
            }

            if (!publication.TryPublishFullBatch(
                    token,
                    out FacilityBufferPlannedOutputPublicationReceipt published,
                    out _,
                    out string publicationFailure))
            {
                admission.TryReleasePlannedOutput(
                    token,
                    FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                    out _,
                    out _);
                failure = Fail(
                    context.ItemId,
                    "planned-output-publication-failed",
                    publicationFailure);
                return false;
            }

            if (!admission.TryCommitPlannedOutput(
                    token,
                    published,
                    out FacilityBufferPlannedOutputReceipt committed,
                    out _,
                    out string commitFailure))
            {
                RollbackUncommitted(token, published, out string rollbackFailure);
                failure = Fail(
                    context.ItemId,
                    "planned-output-admission-commit-failed",
                    JoinFailure(commitFailure, rollbackFailure));
                return false;
            }
            if (committed.CommittedMassGrams != exactBatchMassGrams
                || committed.Stacks.Count != context.Amount)
            {
                failure = Fail(
                    context.ItemId,
                    "planned-output-admission-receipt-mismatch");
                return false;
            }

            if (!TryValidatePublicationReceipt(
                    context,
                    facility,
                    published,
                    exactBatchMassGrams,
                    out failure))
            {
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failure = Fail(
                context.ItemId,
                "planned-output-exception",
                exception.Message);
            return false;
        }
    }

    public bool TryAcknowledge(
        string commitId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!Canonical(commitId))
        {
            failure = Fail(
                commitId,
                "commit-acknowledgement-id-invalid");
            return false;
        }
        if (!publication.TryCaptureBatch(
                commitId,
                allowAcknowledged: true,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                out bool acknowledged,
                out _,
                out string captureFailure))
        {
            failure = Fail(
                commitId,
                "commit-acknowledgement-join-failed",
                captureFailure);
            return false;
        }
        if (acknowledged)
            return true;
        if (!publication.TryAcknowledgeRestoreCandidate(
                batch,
                out _,
                out string acknowledgementFailure))
        {
            failure = Fail(
                commitId,
                "commit-acknowledgement-failed",
                acknowledgementFailure);
            return false;
        }
        if (!publication.TryCaptureBatch(
                commitId,
                allowAcknowledged: true,
                out _,
                out bool confirmed,
                out _,
                out string confirmationFailure)
            || !confirmed)
        {
            failure = Fail(
                commitId,
                "commit-acknowledgement-not-durable",
                confirmationFailure);
            return false;
        }
        return true;
    }

    public bool TryCaptureCommittedOutput(
        ProductionOutputContext context,
        out ProductionCommittedOutputSnapshot snapshot,
        out DomainFailure failure)
    {
        snapshot = null;
        failure = DomainFailure.None;
        if (context.Facility == null
            || context.Amount <= 0
            || !Canonical(context.CommitId)
            || !ProductionOutputDefinition.IsCanonicalOutputLineId(
                context.OutputLineId)
            || !apparelCatalog.TryGetByItemId(
                context.ItemId,
                out ApparelDefinitionSO apparel))
        {
            failure = InvalidContext(context);
            return false;
        }
        ProductionFacilityHandle facility;
        ProductionOutputBatchMaximumMassProof maximumMassProof;
        ProductionOutputBufferCapacitySourceSnapshot capacity;
        TextileMaterialDefinitionSO material = ResolvePrimaryMaterial(context.Recipe);
        if (material == null
            || (material.Tags & apparel.AllowedMaterialTags) == 0)
        {
            failure = Fail(context.ItemId, "snapshot-apparel-material-invalid");
            return false;
        }
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
                context.ItemId,
                "committed-output-snapshot-authority-invalid",
                exception.Message);
            return false;
        }
        string expectedDestinationId = ProductionOutputDestinationId
            .FromFacility(facility.InstanceId)
            .Value;
        string expectedOutcomeFingerprint = CreateOutcomeFingerprint(
            context,
            facility,
            material,
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
                out failure)
            || batch.TotalQuantity <= 0
            || batch.TotalMassGrams <= 0L
            || batch.TotalMassGrams > maximumMassProof.MaximumBatchMassGrams)
        {
            if (!failure.IsFailure)
            {
                failure = Fail(
                    context.CommitId,
                    "committed-output-snapshot-missing",
                    captureFailure);
            }
            return false;
        }
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
                context.ItemId,
                "committed-output-snapshot-destination-invalid",
                destinationFailure);
            return false;
        }
        ProductionCommittedOutputStackSnapshot[] stacks = batch.Stacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => new ProductionCommittedOutputStackSnapshot(
                // The FacilityBuffer keeps one physical unit-slice line per
                // unique item.  The recipe execution receipt, however, owns
                // the authored logical output line.  Do not leak the physical
                // ":unit:NNNN" slice identifier across that semantic boundary.
                context.OutputLineId,
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

    bool IProductionOutputHandler.TryProduce(
        ProductionOutputContext context,
        out string diagnosticCode)
    {
        bool succeeded = TryProduce(context, out DomainFailure failure);
        diagnosticCode = succeeded ? string.Empty : failure.Code.ToString();
        return succeeded;
    }

    private ApparelInstanceState CreateState(
        ApparelDefinitionSO apparel,
        TextileMaterialDefinitionSO material,
        ProductionOutputContext context,
        string facilityId,
        int craftedDay,
        int outputIndex) => new()
    {
        apparelDefinitionId = apparel.ApparelId,
        primaryMaterialId = material.MaterialId,
        craftsmanshipQuality = ResolveCraftsmanship(context.QualityModifier),
        sourceKind = ResolveSourceKind(material.Tags),
        sourceDefinitionId = material.MaterialId,
        size = ApparelSizeClass.Medium,
        modifications = ApparelModificationKind.None,
        closedOpenings = ApparelModificationKind.None,
        durability = 100f,
        moisture = 0f,
        contamination = 0f,
        craftedAbsoluteDay = craftedDay,
        deterministicBatchHash = DeterministicHash(
            context.Recipe?.RecipeId,
            facilityId,
            context.CommitId,
            craftedDay,
            outputIndex)
    };

    private TextileMaterialDefinitionSO ResolvePrimaryMaterial(
        ProductionRecipeSO recipe)
    {
        foreach (ItemAmountDefinition input in recipe?.Inputs
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            if (input != null
                && materialCatalog.TryGetByItemId(
                    input.ItemId,
                    out TextileMaterialDefinitionSO material))
            {
                return material;
            }
        }
        return null;
    }

    private void RollbackUncommitted(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out string failureReason)
    {
        List<string> failures = new();
        if (!publication.TryRollbackPublishedBatch(
                published,
                out _,
                out string publicationFailure))
        {
            failures.Add(publicationFailure);
        }
        if (!admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out string admissionFailure))
        {
            failures.Add(admissionFailure);
        }
        failureReason = string.Join(
            ";",
            failures.Where(value => !string.IsNullOrEmpty(value)));
    }

    private static bool TryValidateExistingBatch(
        ProductionOutputContext context,
        ProductionFacilityHandle facility,
        FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
        bool acknowledged,
        string expectedOutcomeFingerprint,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string[] expectedLines = Enumerable.Range(0, context.Amount)
            .Select(index => FormatUnitOutputLineId(context.OutputLineId, index))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferPlannedOutputRestoreStackSnapshot[] stacks =
            (batch?.Stacks
                ?? Array.Empty<FacilityBufferPlannedOutputRestoreStackSnapshot>())
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        string destinationId = ProductionBillRuntime.OutputDestinationPrefix
            + facility.InstanceId.Value;
        bool exact = batch != null
            && string.Equals(
                batch.BatchCommitId,
                context.CommitId,
                StringComparison.Ordinal)
            && batch.TotalQuantity == context.Amount
            && batch.TotalMassGrams > 0L
            && string.Equals(
                batch.OutcomeFingerprint,
                expectedOutcomeFingerprint,
                StringComparison.Ordinal)
            && batch.PlannedOutputFingerprint.Length == 64
            && stacks.Length == context.Amount
            && stacks.Select(value => value.OutputLineId)
                .SequenceEqual(expectedLines, StringComparer.Ordinal)
            && stacks.Select(value => value.ItemInstanceId)
                .Distinct(StringComparer.Ordinal).Count() == context.Amount
            && stacks.All(value => value != null
                && string.Equals(
                    value.ItemId,
                    context.ItemId,
                    StringComparison.Ordinal)
                && value.Quantity == 1
                && value.MassGrams > 0L
                && !string.IsNullOrEmpty(value.ComponentSignature)
                && ((ItemInstanceId)value.ItemInstanceId).IsValid
                && (acknowledged
                    || value.State == WorldItemStackState.FacilityOutputBuffer
                        && value.Position == facility.Position
                        && string.Equals(
                            value.DestinationId,
                            destinationId,
                            StringComparison.Ordinal)));
        if (exact)
            return true;
        failure = Fail(
            context.ItemId,
            "commit-replay-batch-mismatch");
        return false;
    }

    private static bool TryValidatePublicationReceipt(
        ProductionOutputContext context,
        ProductionFacilityHandle facility,
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        long exactBatchMassGrams,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string[] expectedLines = Enumerable.Range(0, context.Amount)
            .Select(index => FormatUnitOutputLineId(context.OutputLineId, index))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferPublishedOutputStackReceipt[] stacks = receipt.Stacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        bool exact = string.Equals(
                receipt.BatchCommitId,
                context.CommitId,
                StringComparison.Ordinal)
            && receipt.DropPosition == facility.Position
            && receipt.OutcomeFingerprint.Length == 64
            && receipt.PlannedOutputFingerprint.Length == 64
            && stacks.Length == context.Amount
            && stacks.Sum(value => value.Quantity) == context.Amount
            && stacks.Sum(value => value.MassGrams) == exactBatchMassGrams
            && stacks.Select(value => value.OutputLineId)
                .SequenceEqual(expectedLines, StringComparer.Ordinal)
            && stacks.Select(value => value.ItemInstanceId)
                .Distinct(StringComparer.Ordinal).Count() == context.Amount
            && stacks.All(value => value.Quantity == 1
                && value.ItemDefinitionId.Equals(
                    (ItemDefinitionId)context.ItemId)
                && ((ItemInstanceId)value.ItemInstanceId).IsValid);
        if (exact)
            return true;
        failure = Fail(
            context.ItemId,
            "published-receipt-mismatch");
        return false;
    }

    private static CraftsmanshipQualityTier ResolveCraftsmanship(
        float qualityModifier) =>
        EnvironmentalWorkwearProductionOutputSemantics.ResolveCraftsmanship(
            qualityModifier);

    private static TextileSourceKind ResolveSourceKind(TextileMaterialTag tags) =>
        EnvironmentalWorkwearProductionOutputSemantics.ResolveSourceKind(tags);

    private static ulong DeterministicHash(
        string recipeId,
        string facilityId,
        string commitId,
        int craftedDay,
        int outputIndex)
        => EnvironmentalWorkwearProductionOutputSemantics.DeterministicHash(
            recipeId,
            facilityId,
            commitId,
            craftedDay,
            outputIndex);

    private static string FormatUnitOutputLineId(
        string outputLineId,
        int outputIndex) => EnvironmentalWorkwearProductionOutputSemantics
        .FormatUnitOutputLineId(outputLineId, outputIndex);

    private static string Hash(string canonical)
        => EnvironmentalWorkwearProductionOutputSemantics
            .HashCanonicalComponent(canonical);

    private static string CreateOutcomeFingerprint(
        ProductionOutputContext context,
        ProductionFacilityHandle facility,
        TextileMaterialDefinitionSO material,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputBufferCapacitySourceSnapshot capacity)
        => EnvironmentalWorkwearProductionOutputSemantics
            .CreateOutcomeFingerprint(
                context.CommitId,
                context.OutputLineId,
                context.ItemId,
                context.Amount,
                context.OutputDestinationId,
                context.Recipe?.RecipeId ?? string.Empty,
                facility.InstanceId.Value,
                material?.MaterialId ?? string.Empty,
                maximumMassProof,
                capacity,
                context.QualityModifier,
                context.WorkerQuality);

    private static bool IsMissingBatch(string failureReason) =>
        (failureReason ?? string.Empty).StartsWith(
            "planned-output-batch-missing:",
            StringComparison.Ordinal);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string JoinFailure(string first, string second) =>
        string.IsNullOrEmpty(second)
            ? first ?? string.Empty
            : (first ?? string.Empty) + ";rollback=" + second;

    private static DomainFailure InvalidContext(
        ProductionOutputContext context) => new(
        FailureCode.EnvironmentWorkwearProductionContextInvalid,
        context.ItemId ?? string.Empty,
        context.Amount.ToString(CultureInfo.InvariantCulture));

    private static DomainFailure Fail(
        string subject,
        string detail,
        string reason = "",
        FailureCode code = FailureCode.ProductionOutputUnavailable) => new(
        code,
        subject ?? string.Empty,
        string.IsNullOrEmpty(reason) ? detail : detail + ":" + reason);
}

/// <summary>
/// Execution-free maximum-mass companion for environmental workwear output.
/// Apparel state does not add physical matter in V27, so the authored
/// definition mass is the complete production-time upper bound.
/// </summary>
public sealed class EnvironmentalWorkwearProductionOutputMaximumMassCapability :
    IProductionOutputMaximumMassCapability
{
    private readonly IApparelDefinitionCatalog apparelCatalog;

    public EnvironmentalWorkwearProductionOutputMaximumMassCapability(
        IApparelDefinitionCatalog apparelCatalog)
    {
        this.apparelCatalog = apparelCatalog
            ?? throw new ArgumentNullException(nameof(apparelCatalog));
    }

    public string CapabilityId =>
        EnvironmentalWorkwearProductionOutputHandler.HandlerCapabilityId;
    public int ContractVersion =>
        EnvironmentalWorkwearProductionOutputHandler.HandlerContractVersion;
    public string ComponentCodecId =>
        EnvironmentalWorkwearProductionOutputHandler.HandlerComponentCodecId;
    public int ComponentCodecVersion =>
        EnvironmentalWorkwearProductionOutputHandler.HandlerComponentCodecVersion;
    public bool SupportsAutomaticSelection => true;

    public bool CanHandle(string itemId) =>
        apparelCatalog.TryGetByItemId(itemId, out ApparelDefinitionSO _);

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
