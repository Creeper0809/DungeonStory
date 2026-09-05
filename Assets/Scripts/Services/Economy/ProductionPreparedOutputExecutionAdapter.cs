using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Default-assembly bridge for the positive exact-profile generic production recipes.
/// Resolved output remains Production authority; gram admission and physical
/// publication remain Items authority. Transient admission tokens are never saved.
/// </summary>
public sealed class ProductionPreparedOutputExecutionAdapter :
    IProductionPreparedOutputExecutionPort,
    IProductionRuinedBatchExecutionPort,
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "365.economy.production-prepared-output-transient";
    private const string PublicationOperationSuffix = ":publication";
    private const string MissingBatchPrefix = "planned-output-batch-missing:";

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionOutputPlanningService outputPlanning;
    private readonly IProductionAssemblyBridge bridge;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly CanonicalProductionOutputResolver resolver;
    private readonly IProductionPreparedOutputMaterializerRegistry materializers;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly ProductionOutputBufferCapacityProjector capacityProjector;
    private readonly IProductionOutputDestinationAuthorityRuntime destinations;
    private readonly IFacilityBufferMassCapacityQuery capacities;
    private readonly IFacilityBufferPhysicalOccupancyQuery occupancy;
    private readonly IFacilityBufferMassAdmissionService admission;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;
    private readonly IProductionPreparedOutputRoutingAuthority routingAuthority;
    private readonly ProductionMassExplanationCapabilityRegistry
        massExplanations;
    private readonly Dictionary<string, LivePublication> liveByBatch =
        new(StringComparer.Ordinal);
    private Dictionary<string, LivePublication> previousLiveByBatch;
    private bool restoreCandidateActive;
    private bool restoreCandidatePublished;

    public ProductionPreparedOutputExecutionAdapter(
        IResourceEconomyContentCatalog catalog,
        IProductionOutputPlanningService outputPlanning,
        IProductionAssemblyBridge bridge,
        IGrandProjectBenefitQuery grandProjectBenefits,
        CanonicalProductionOutputResolver resolver,
        IProductionPreparedOutputMaterializerRegistry materializers,
        IPhysicalItemMassQuery massQuery,
        ProductionOutputBufferCapacityProjector capacityProjector,
        IProductionOutputDestinationAuthorityRuntime destinations,
        IFacilityBufferMassCapacityQuery capacities,
        IFacilityBufferPhysicalOccupancyQuery occupancy,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication,
        IProductionPreparedOutputRoutingAuthority routingAuthority,
        ProductionMassExplanationCapabilityRegistry massExplanations = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.outputPlanning = outputPlanning
            ?? throw new ArgumentNullException(nameof(outputPlanning));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.grandProjectBenefits = grandProjectBenefits
            ?? throw new ArgumentNullException(nameof(grandProjectBenefits));
        this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        this.materializers = materializers
            ?? throw new ArgumentNullException(nameof(materializers));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.capacityProjector = capacityProjector
            ?? throw new ArgumentNullException(nameof(capacityProjector));
        this.destinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
        this.occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
        this.admission = admission ?? throw new ArgumentNullException(nameof(admission));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
        this.routingAuthority = routingAuthority
            ?? throw new ArgumentNullException(nameof(routingAuthority));
        this.massExplanations = massExplanations
            ?? ProductionMassExplanationCapabilityRegistry.CreateDefault();
    }

    public void RestoreDestinationAuthorities(
        IReadOnlyList<ProductionBillRecord> records,
        IReadOnlyList<ProductionFacilityHandle> facilities)
    {
        IReadOnlyList<ProductionBillRecord> exactRecords = records
            ?? throw new ArgumentNullException(nameof(records));
        ProductionFacilityHandle[] exactFacilities = (facilities
                ?? throw new ArgumentNullException(nameof(facilities)))
            .Where(value => value != null && !value.IsDestroyed)
            .OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        if (exactFacilities.Select(value => value.InstanceId.Value)
            .Distinct(StringComparer.Ordinal).Count() != exactFacilities.Length)
        {
            throw new InvalidOperationException(
                "Production output restore contains duplicate facility identities.");
        }
        HashSet<string> facilityIds = exactFacilities
            .Select(value => value.InstanceId.Value)
            .ToHashSet(StringComparer.Ordinal);
        ProductionBillRecord orphan = exactRecords
            .Where(value => value != null
                && catalog.TryGetRecipe(
                    value.recipeId,
                    out ProductionRecipeSO recipe)
                && ProductionPreparedOutputCapabilitySelection
                    .UsesPreparedOutputMaterializer(recipe, bridge))
            .OrderBy(value => value.billId.Value, StringComparer.Ordinal)
            .FirstOrDefault(value => !facilityIds.Contains(
                value.buildingInstanceId.Value));
        if (orphan != null)
        {
            throw new InvalidOperationException(
                $"Production output restore bill '{orphan.billId.Value}' has no live facility '{orphan.buildingInstanceId.Value}'.");
        }

        Dictionary<string, ProductionOutputBufferCapacitySourceSnapshot>
            projectedByFacility =
            new(StringComparer.Ordinal);
        foreach (ProductionFacilityHandle facility in exactFacilities)
        {
            string destinationId = ProductionBillRuntime.OutputDestinationPrefix
                + facility.InstanceId.Value;
            ProductionOutputBufferCapacitySourceSnapshot portfolio =
                capacityProjector.CapturePortfolioSource(facility);
            ProductionOutputBufferCapacitySourceSnapshot projectedSource =
                portfolio;
            long projectedCapacity = portfolio.RequiredMinimumCapacityGrams;
            foreach (ProductionBillRecord record in exactRecords
                         .Where(value => value != null
                             && value.buildingInstanceId.Equals(
                                 facility.InstanceId)
                             && value.preparedOutput != null
                             && value.preparedOutput.phase !=
                                  ProductionPreparedOutputPhase.Unresolved))
            {
                if (!catalog.TryGetRecipe(
                        record.recipeId,
                        out ProductionRecipeSO preparedRecipe))
                {
                    throw new InvalidOperationException(
                        $"Production output restore bill '{record.billId.Value}' has no recipe '{record.recipeId}'.");
                }
                ValidateResolvedSourceAuthority(
                    record,
                    preparedRecipe,
                    facility,
                    $"Production output restore bill '{record.billId.Value}'");
                ProductionOutputBufferCapacitySourceSnapshot batchCapacity =
                    ValidateCapacitySource(
                        record,
                        preparedRecipe,
                        record.preparedOutput,
                        facility,
                        $"Production output restore bill '{record.billId.Value}'");
                if (!string.Equals(
                        projectedSource.ClearanceGateDigest,
                        batchCapacity.ClearanceGateDigest,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Production output restore clearance authority drifted within facility '"
                        + facility.InstanceId.Value + "'.");
                }
                if (batchCapacity.RequiredMinimumCapacityGrams
                    > projectedCapacity)
                {
                    projectedSource = batchCapacity;
                    projectedCapacity = batchCapacity.RequiredMinimumCapacityGrams;
                }
            }
            long occupiedMass = occupancy.Capture(destinationId).TotalMassGrams;
            if (projectedCapacity == 0L)
            {
                if (occupiedMass != 0L)
                {
                    throw new InvalidOperationException(
                        $"Production output restore found {occupiedMass}g at non-capable destination '{destinationId}'.");
                }
                continue;
            }
            if (occupiedMass > projectedCapacity)
            {
                throw new InvalidOperationException(
                    $"Production output restore destination '{destinationId}' exceeds its exact {projectedCapacity}g capacity with {occupiedMass}g physical occupancy.");
            }
            projectedByFacility.Add(facility.InstanceId.Value, projectedSource);
        }

        ProductionFacilityHandle[] capableFacilities = exactFacilities
            .Where(value => projectedByFacility.ContainsKey(value.InstanceId.Value))
            .ToArray();
        if (!destinations.TryReplaceProjectedCapacitySources(
                capableFacilities,
                projectedByFacility,
                out string authorityFailure))
        {
            throw new InvalidOperationException(
                "Production output projected authority replacement failed: "
                + authorityFailure);
        }
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (restoreCandidateActive)
        {
            throw new InvalidOperationException(
                "Prepared-output transient restore is already active.");
        }
        previousLiveByBatch = null;
        restoreCandidateActive = true;
        restoreCandidatePublished = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreCandidateActive || restoreCandidatePublished)
        {
            throw new InvalidOperationException(
                "Prepared-output transient restore is not ready to publish.");
        }
        previousLiveByBatch = new Dictionary<string, LivePublication>(
            liveByBatch,
            StringComparer.Ordinal);
        liveByBatch.Clear();
        restoreCandidatePublished = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (restoreCandidatePublished)
        {
            liveByBatch.Clear();
            foreach (KeyValuePair<string, LivePublication> pair in
                     previousLiveByBatch
                     ?? new Dictionary<string, LivePublication>(
                         StringComparer.Ordinal))
            {
                liveByBatch.Add(pair.Key, pair.Value);
            }
        }
        ResetRestoreCandidate();
    }

    public void CompleteRestoreCandidate() => ResetRestoreCandidate();

    public void DiscardRestoreCandidate()
    {
        if (restoreCandidatePublished)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        ResetRestoreCandidate();
    }

    public ProductionPreparedOutputCapacityResult AssessCycleStart(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        if (!TryValidateScope(record, recipe, facility, out DomainFailure failure))
        {
            return ProductionPreparedOutputCapacityResult.Unavailable(failure);
        }

        ProductionOutputBufferCapacitySourceSnapshot capacitySource;
        try
        {
            // Cycle-start admission runs before physical inputs become durable
            // WIP. Resolving an exact prepared batch here would therefore feed
            // a zero input mass into process-loss/external-input equations and
            // would also decide the exact outcome before work completion. The
            // immutable facility portfolio is already the conservative maximum
            // proof required for a read-only start check.
            capacitySource = capacityProjector.CapturePortfolioSource(facility);
            if (capacitySource.RequiredMinimumCapacityGrams <= 0L
                || capacitySource.MaximumBatchMassGrams <= 0L)
            {
                throw new InvalidOperationException(
                    "Prepared-output facility has no positive maximum capacity proof.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return ProductionPreparedOutputCapacityResult.Unavailable(Fail(
                record,
                "prepared-output-capacity-invalid",
                exception.Message));
        }

        long bootstrapCapacity = capacitySource.RequiredMinimumCapacityGrams;
        long maximumBatchMass = capacitySource.MaximumBatchMassGrams;
        string destinationId = record.outputDestinationId;
        if (!capacities.TryGetCapacity(
                destinationId,
                facility.Position,
                out FacilityBufferMassCapacitySnapshot capacity))
        {
            // Read-only bootstrap assessment: the exact authority pair is created
            // only by Execute after the completed work has durable resolved output.
            return ProductionPreparedOutputCapacityResult.Available(
                bootstrapCapacity,
                0L,
                0L);
        }
        if (!ProfileMatches(facility, capacity.Profile))
        {
            return ProductionPreparedOutputCapacityResult.Unavailable(Fail(
                record,
                "prepared-output-capacity-owner-conflict"));
        }

        FacilityBufferPhysicalOccupancySnapshot physical;
        try
        {
            physical = occupancy.Capture(destinationId);
            long used = checked(
                physical.TotalMassGrams + capacity.ReservedMassGrams);
            if (used <= capacity.Profile.MaxMassGrams
                && maximumBatchMass
                    <= capacity.Profile.MaxMassGrams - used)
            {
                return ProductionPreparedOutputCapacityResult.Available(
                    capacity.Profile.MaxMassGrams,
                    physical.TotalMassGrams,
                    capacity.ReservedMassGrams);
            }
            return ProductionPreparedOutputCapacityResult.Blocked(
                capacity.Profile.MaxMassGrams,
                physical.TotalMassGrams,
                capacity.ReservedMassGrams,
                new DomainFailure(
                    FailureCode.ProductionOutputSpaceUnavailable,
                    destinationId));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return ProductionPreparedOutputCapacityResult.Unavailable(Fail(
                record,
                "prepared-output-capacity-read-failed",
                exception.Message));
        }
    }

    public ProductionPreparedOutputCapacityResult AssessCurrentCapacity(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        if (ProductionPreparedOutputMigrationScope
            .RequiresAdditionalOutputCapacity(record))
        {
            return AssessCycleStart(record, recipe, facility);
        }
        if (!TryValidateScope(record, recipe, facility, out DomainFailure failure)
            || !TryResolveBatch(
                record,
                recipe,
                facility,
                out ProductionPreparedOutputBatchSaveData batch,
                out failure))
        {
            return ProductionPreparedOutputCapacityResult.Unavailable(failure);
        }

        long bootstrapCapacity;
        try
        {
            bootstrapCapacity = ValidateCapacitySource(
                    record,
                    recipe,
                    batch,
                    facility,
                    $"Production bill '{record.billId.Value}'")
                .RequiredMinimumCapacityGrams;
        }
        catch (OverflowException)
        {
            return ProductionPreparedOutputCapacityResult.Unavailable(Fail(
                record,
                "prepared-output-capacity-overflow"));
        }

        if (!capacities.TryGetCapacity(
                batch.destinationId,
                facility.Position,
                out FacilityBufferMassCapacitySnapshot capacity))
        {
            return ProductionPreparedOutputCapacityResult.Available(
                bootstrapCapacity,
                0L,
                0L);
        }
        if (!ProfileMatches(facility, capacity.Profile))
        {
            return ProductionPreparedOutputCapacityResult.Unavailable(Fail(
                record,
                "prepared-output-capacity-owner-conflict"));
        }

        try
        {
            FacilityBufferPhysicalOccupancySnapshot physical =
                occupancy.Capture(batch.destinationId);
            long used = checked(
                physical.TotalMassGrams + capacity.ReservedMassGrams);
            return used <= capacity.Profile.MaxMassGrams
                ? ProductionPreparedOutputCapacityResult.Available(
                    capacity.Profile.MaxMassGrams,
                    physical.TotalMassGrams,
                    capacity.ReservedMassGrams)
                : ProductionPreparedOutputCapacityResult.Blocked(
                    capacity.Profile.MaxMassGrams,
                    physical.TotalMassGrams,
                    capacity.ReservedMassGrams,
                    new DomainFailure(
                        FailureCode.ProductionOutputSpaceUnavailable,
                        batch.destinationId));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return ProductionPreparedOutputCapacityResult.Unavailable(Fail(
                record,
                "prepared-output-capacity-read-failed",
                exception.Message));
        }
    }

    public ProductionPreparedOutputExecutionResult Execute(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker)
    {
        if (!TryValidateScope(record, recipe, facility, out DomainFailure failure))
        {
            return ProductionPreparedOutputExecutionResult.Blocked(
                SafePhase(record),
                failure);
        }

        try
        {
            if (SafePhase(record) != ProductionPreparedOutputPhase.Unresolved)
            {
                ValidateResolvedSourceAuthority(
                    record,
                    recipe,
                    facility,
                    "prepared-output-execution");
            }
            for (int transition = 0; transition < 6; transition++)
            {
                ProductionPreparedOutputPhase phase = SafePhase(record);
                switch (phase)
                {
                    case ProductionPreparedOutputPhase.Unresolved:
                        if (!TryResolveBatch(record, recipe, facility, out ProductionPreparedOutputBatchSaveData resolved, out failure))
                            return Block(record, phase, failure);
                        record.ResolvePreparedOutput(resolved);
                        break;

                    case ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace:
                        if (!TryReserve(record, recipe, facility, out failure))
                            return Block(record, phase, failure);
                        break;

                    case ProductionPreparedOutputPhase.PublicationPrepared:
                        if (!TryPublishOrJoin(record, out failure))
                            return Block(record, SafePhase(record), failure);
                        break;

                    case ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending:
                        if (!TryCommitAndAcknowledge(record, out failure))
                            return Block(record, phase, failure);
                        record.MarkPreparedOutputCompleted();
                        break;

                    case ProductionPreparedOutputPhase.Completed:
                        routingAuthority.PublishCommittedBatch(
                            record.preparedOutput,
                            facility.InstanceId);
                        return ProductionPreparedOutputExecutionResult.Completed();

                    default:
                        return Block(
                            record,
                            ProductionPreparedOutputPhase.Unresolved,
                            Fail(record, "prepared-output-phase-invalid"));
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            return Block(
                record,
                SafePhase(record),
                Fail(record, "prepared-output-execution-failed", exception.Message));
        }

        return Block(
            record,
            SafePhase(record),
            Fail(record, "prepared-output-transition-budget-exhausted"));
    }

    public ProductionRuinedBatchExecutionResult ExecuteRuinedBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        if (!TryCreateRuinedDisposition(
                record,
                recipe,
                facility,
                out ProductionRuinedBatchDispositionPlan disposition,
                out ProductionRuinedOutputCapacityClaim capacityClaim,
                out DomainFailure failure))
        {
            return RuinedBlocked(record, failure);
        }

        try
        {
            if (SafePhase(record) != ProductionPreparedOutputPhase.Unresolved)
            {
                ValidateResolvedSourceAuthority(
                    record,
                    recipe,
                    facility,
                    "ruined-output-execution");
            }
            for (int transition = 0; transition < 6; transition++)
            {
                ProductionPreparedOutputPhase phase = SafePhase(record);
                switch (phase)
                {
                    case ProductionPreparedOutputPhase.Unresolved:
                        if (!TryResolveRuinedBatch(
                                record,
                                recipe,
                                facility,
                                disposition,
                                capacityClaim,
                                out ProductionPreparedOutputBatchSaveData resolved,
                                out failure))
                        {
                            return RuinedBlocked(record, failure);
                        }
                        record.ResolvePreparedOutput(resolved);
                        break;

                    case ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace:
                        if (!TryReserve(record, recipe, facility, out failure))
                            return RuinedBlocked(record, failure);
                        break;

                    case ProductionPreparedOutputPhase.PublicationPrepared:
                        if (!TryPublishOrJoin(record, out failure))
                            return RuinedBlocked(record, failure);
                        break;

                    case ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending:
                        if (!TryCommitAndAcknowledge(record, out failure))
                            return RuinedBlocked(record, failure);
                        record.MarkPreparedOutputCompleted();
                        break;

                    case ProductionPreparedOutputPhase.Completed:
                        if (!IsExactRuinedBatch(
                                record.preparedOutput,
                                recipe,
                                disposition))
                        {
                            return RuinedBlocked(
                                record,
                                Fail(record, "ruined-output-completed-batch-conflict"));
                        }
                        routingAuthority.PublishCommittedBatch(
                            record.preparedOutput,
                            facility.InstanceId);
                        return ProductionRuinedBatchExecutionResult.Completed(
                            disposition);

                    default:
                        return RuinedBlocked(
                            record,
                            Fail(record, "ruined-output-phase-invalid"));
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            return RuinedBlocked(
                record,
                Fail(record, "ruined-output-execution-failed", exception.Message));
        }

        return RuinedBlocked(
            record,
            Fail(record, "ruined-output-transition-budget-exhausted"));
    }

    public ProductionPreparedOutputReleaseResult Release(
        ProductionBillRecord record,
        ProductionWipTerminalReason reason)
    {
        if (record?.preparedOutput == null)
        {
            return ProductionPreparedOutputReleaseResult.Blocked(
                false,
                Fail(record, "prepared-output-release-state-missing"));
        }
        ProductionPreparedOutputPhase phase = record.preparedOutput.phase;
        if (phase == ProductionPreparedOutputPhase.Unresolved)
            return ProductionPreparedOutputReleaseResult.ReleasedUnpublished();
        if (phase is ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending
            or ProductionPreparedOutputPhase.Completed)
        {
            return ProductionPreparedOutputReleaseResult.Blocked(
                true,
                Fail(record, "prepared-output-physical-batch-retained"));
        }

        string batchCommitId = record.preparedOutput.batchCommitId;
        if (phase == ProductionPreparedOutputPhase.PublicationPrepared)
        {
            if (liveByBatch.TryGetValue(batchCommitId, out LivePublication live))
            {
                if (live.HasReceipt)
                {
                    // Publication created physical stacks, but the admission
                    // token has not been committed yet while the aggregate is
                    // still PublicationPrepared.  Cancellation must roll that
                    // uncommitted physical batch back before releasing its
                    // reserved mass.  Once admission commit advances the
                    // aggregate, the earlier physical-retained guard applies.
                    if (!publication.TryRollbackPublishedBatch(
                            live.Receipt,
                            out _,
                            out string rollbackFailure))
                    {
                        return ProductionPreparedOutputReleaseResult.Blocked(
                            true,
                            Fail(
                                record,
                                "prepared-output-publication-rollback-failed",
                                rollbackFailure));
                    }
                }
                if (!admission.TryReleasePlannedOutput(
                        live.Token,
                        reason == ProductionWipTerminalReason.FacilityDestroyed
                            ? FacilityBufferMassAdmissionReleaseReason.DestinationInvalidated
                            : FacilityBufferMassAdmissionReleaseReason.OwnerCancelled,
                        out _,
                        out string releaseFailure))
                {
                    return ProductionPreparedOutputReleaseResult.Blocked(
                        false,
                        Fail(record, "prepared-output-admission-release-failed", releaseFailure));
                }
                liveByBatch.Remove(batchCommitId);
            }
            else if (publication.TryCapturePendingBatch(
                         batchCommitId,
                         out _,
                         out _,
                         out string captureFailure))
            {
                return ProductionPreparedOutputReleaseResult.Blocked(
                    true,
                    Fail(record, "prepared-output-physical-batch-retained"));
            }
            else if (!captureFailure.StartsWith(
                         MissingBatchPrefix,
                         StringComparison.Ordinal))
            {
                return ProductionPreparedOutputReleaseResult.Blocked(
                    true,
                    Fail(record, "prepared-output-publication-conflict", captureFailure));
            }
            record.ReturnPreparedOutputToWaitingForSpace();
        }

        record.ReleaseUnpublishedPreparedOutput();
        return ProductionPreparedOutputReleaseResult.ReleasedUnpublished();
    }

    private bool TryReserve(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out DomainFailure failure)
    {
        ProductionPreparedOutputBatchSaveData batch = record.preparedOutput;
        ProductionOutputBufferCapacitySourceSnapshot capacity =
            ValidateCapacitySource(
                record,
                recipe,
                batch,
                facility,
                $"Production bill '{record.billId.Value}'");
        if (!destinations.TryEnsureCapacitySource(
                facility,
                capacity,
                out FacilityBufferCapacityProfile profile,
                out string authorityFailure))
        {
            failure = Fail(record, "prepared-output-authority-unavailable", authorityFailure);
            return false;
        }

        FacilityBufferPlannedOutputSlice[] slices = batch.lines
            .Where(line => ProductionOutputRoleRules.IsPhysical(line.role)
                && line.quantity > 0)
            .Select(line => CreateSlice(line))
            .ToArray();
        FacilityBufferPlannedOutputRequest request = new(
            batch.batchCommitId + PublicationOperationSuffix,
            batch.batchCommitId,
            batch.outcomeFingerprint,
            batch.destinationId,
            facility.Position,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            slices,
            batch.capacitySourceDigest,
            batch.requiredMinimumCapacityGrams,
            capacity.ClearanceGateDigest);
        if (!admission.TryReservePlannedOutput(
                request,
                out FacilityBufferPlannedOutputToken token,
                out FacilityBufferMassAdmissionFailureCode admissionFailure,
                out string admissionReason))
        {
            failure = new DomainFailure(
                admissionFailure == FacilityBufferMassAdmissionFailureCode.CapacityUnavailable
                    ? FailureCode.ProductionOutputSpaceUnavailable
                    : FailureCode.ProductionOutputUnavailable,
                record.billId.Value,
                admissionReason);
            return false;
        }
        if (token.ReservedMassGrams != batch.totalPhysicalMassGrams)
        {
            admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _);
            failure = Fail(record, "prepared-output-admission-mass-mismatch");
            return false;
        }

        try
        {
            if (liveByBatch.ContainsKey(batch.batchCommitId))
                throw new InvalidOperationException(
                    $"Prepared output batch '{batch.batchCommitId}' already has a live publication owner.");
            record.MarkPreparedOutputPublicationPrepared(
                token.PlannedOutput.Fingerprint);
            liveByBatch.Add(batch.batchCommitId, new LivePublication(token));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            liveByBatch.Remove(batch.batchCommitId);
            bool released = admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out string releaseFailure);
            if (record.preparedOutput?.phase ==
                ProductionPreparedOutputPhase.PublicationPrepared)
            {
                record.ReturnPreparedOutputToWaitingForSpace();
            }
            failure = Fail(
                record,
                "prepared-output-reserve-publication-transition-failed",
                exception.Message
                    + (released
                        ? string.Empty
                        : ";release=" + releaseFailure));
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    private bool TryPublishOrJoin(
        ProductionBillRecord record,
        out DomainFailure failure)
    {
        ProductionPreparedOutputBatchSaveData batch = record.preparedOutput;
        if (!liveByBatch.TryGetValue(batch.batchCommitId, out LivePublication live))
        {
            if (publication.TryCapturePendingBatch(
                    batch.batchCommitId,
                    out FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
                    out _,
                    out string restoreFailure))
            {
                if (!TryCreatePhysicalCandidates(
                        batch,
                        restored,
                        out ProductionPreparedOutputPhysicalCandidateSaveData[] candidates,
                        out failure))
                    return false;
                record.MarkPreparedOutputPhysicalBatchCommitted(candidates);
                return true;
            }
            if (!restoreFailure.StartsWith(MissingBatchPrefix, StringComparison.Ordinal))
            {
                failure = Fail(record, "prepared-output-publication-conflict", restoreFailure);
                return false;
            }
            record.ReturnPreparedOutputToWaitingForSpace();
            failure = new DomainFailure(
                FailureCode.ProductionOutputSpaceUnavailable,
                record.outputDestinationId,
                "prepared-output-admission-restored-as-waiting");
            return false;
        }

        if (!live.HasReceipt)
        {
            if (!publication.TryPublishFullBatch(
                    live.Token,
                    out FacilityBufferPlannedOutputPublicationReceipt receipt,
                    out _,
                    out string publicationFailure))
            {
                admission.TryReleasePlannedOutput(
                    live.Token,
                    FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                    out _,
                    out _);
                liveByBatch.Remove(batch.batchCommitId);
                record.ReturnPreparedOutputToWaitingForSpace();
                failure = Fail(record, "prepared-output-publication-failed", publicationFailure);
                return false;
            }
            live.SetReceipt(receipt);
        }
        if (!TryCreatePhysicalCandidates(
                batch,
                live.Receipt,
                out ProductionPreparedOutputPhysicalCandidateSaveData[] physical,
                out failure))
        {
            return false;
        }
        record.MarkPreparedOutputPhysicalBatchCommitted(physical);
        return true;
    }

    private bool TryCommitAndAcknowledge(
        ProductionBillRecord record,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        ProductionPreparedOutputBatchSaveData batch = record.preparedOutput;
        if (liveByBatch.TryGetValue(batch.batchCommitId, out LivePublication live))
        {
            string commitFailure = string.Empty;
            if (!live.HasReceipt
                || !admission.TryCommitPlannedOutput(
                    live.Token,
                    live.Receipt,
                    out FacilityBufferPlannedOutputReceipt committed,
                    out _,
                    out commitFailure)
                || committed.CommittedMassGrams != batch.totalPhysicalMassGrams)
            {
                failure = Fail(record, "prepared-output-admission-commit-failed", commitFailure);
                return false;
            }
            if (!publication.TryAcknowledgePublishedBatch(
                    live.Receipt,
                    out _,
                    out string liveAcknowledgementFailure))
            {
                failure = Fail(
                    record,
                    "prepared-output-acknowledgement-failed",
                    liveAcknowledgementFailure);
                return false;
            }
            liveByBatch.Remove(batch.batchCommitId);
            failure = DomainFailure.None;
            return true;
        }

        if (!publication.TryCapturePendingBatch(
                batch.batchCommitId,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
                out _,
                out string restoreFailure)
            || !TryCreatePhysicalCandidates(batch, restored, out _, out failure))
        {
            if (!failure.IsFailure)
                failure = Fail(record, "prepared-output-restore-join-failed", restoreFailure);
            return false;
        }
        if (!publication.TryAcknowledgeRestoreCandidate(
                restored,
                out _,
                out string acknowledgementFailure))
        {
            failure = Fail(record, "prepared-output-restore-acknowledgement-failed", acknowledgementFailure);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    private bool TryResolveBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out ProductionPreparedOutputBatchSaveData batch,
        out DomainFailure failure)
    {
        if (record.preparedOutput != null
            && record.preparedOutput.phase != ProductionPreparedOutputPhase.Unresolved)
        {
            try
            {
                batch = record.preparedOutput.Clone();
                ValidateResolvedSourceAuthority(
                    record,
                    recipe,
                    facility,
                    "prepared-output-existing-batch");
                failure = DomainFailure.None;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or OverflowException)
            {
                batch = null;
                failure = Fail(
                    record,
                    "prepared-output-persisted-batch-invalid",
                    exception.Message);
                return false;
            }
        }
        try
        {
            ProductionOutputFactor multiplier = ProductionOutputFactorAuthority
                .ResolveCurrent(grandProjectBenefits, recipe.FacilityTag)
                .Multiply(ProductionOutputFactor.FromAuthoredMultiplier(
                    outputPlanning.ResolveSupportModifier(
                        facility,
                        recipe,
                        ProductionSupportModifierKind.Output,
                        1f,
                        multiply: true)));
            CanonicalProductionOutputResolution resolution = resolver.Resolve(
                record.billId,
                record.cycleSequence,
                recipe.RecipeId,
                recipe.CaptureCanonicalOutputs(),
                multiplier,
                recipe.ProcessKind,
                record.batchIntegrity);
            string recipeDigest = ComputeRecipeDigest(recipe);
            List<ProductionPreparedOutputLineSaveData> lines = new();
            long physicalMass = 0L;
            foreach (CanonicalProductionResolvedOutputLine resolved in
                     resolution.Lines.OrderBy(value => value.OutputLineId, StringComparer.Ordinal))
            {
                if (ProductionOutputRoleRules.IsNonPhysical(resolved.Role))
                {
                    throw new InvalidOperationException(
                        "Authored non-physical output lines cannot enter the physical prepared-output resolver.");
                }
                if (!catalog.TryGetItem(
                        resolved.ItemId,
                        out ResourceItemDefinitionSO definition))
                {
                    throw new InvalidOperationException(
                        $"Prepared output item '{resolved.ItemId}' is missing.");
                }
                ProductionOutputCapabilityDescriptor capability =
                    bridge.CaptureOutputCapability(
                        resolved.OutputLineId,
                        resolved.ItemId);
                ProductionPreparedOutputComponentProjection projection =
                    materializers.Create(capability, definition);
                long exactMass = resolved.ResolvedQuantity == 0
                    ? 0L
                    : massQuery.GetQuantityMass(
                        (ItemDefinitionId)resolved.ItemId,
                        projection.MassSubject,
                        resolved.ResolvedQuantity).Value;
                physicalMass = checked(physicalMass + exactMass);
                lines.Add(new ProductionPreparedOutputLineSaveData
                {
                    outputLineId = resolved.OutputLineId,
                    role = resolved.Role,
                    itemId = resolved.ItemId,
                    outputCapabilityId = capability.CapabilityId,
                    outputCapabilityVersion = capability.CapabilityVersion,
                    outputComponentCodecId = capability.ComponentCodecId,
                    outputComponentCodecVersion = capability.ComponentCodecVersion,
                    outputCapabilityFingerprint = capability.Fingerprint,
                    quantity = resolved.ResolvedQuantity,
                    componentPayload = projection.CanonicalPayload,
                    componentFingerprint = projection.Fingerprint,
                    qualityPermille = 1000,
                    rollKind = "canonical-keyed",
                    rollValue = 0L,
                    rollUpperExclusive = 1L,
                    rollSucceeded = resolved.Included,
                    exactMassGrams = exactMass
                });
            }
            if (physicalMass <= 0L)
                throw new InvalidOperationException("Prepared output resolved no physical mass.");

            ProductionPreparedOutputCapacityClaim capacityClaim =
                capacityProjector.CapturePreparedClaim(
                    record.billId.Value,
                    record.cycleSequence,
                    recipe,
                    lines);
            if (capacityClaim.ExactBatchMassGrams != physicalMass)
            {
                throw new InvalidOperationException(
                    "Prepared output exact mass drifted from its capacity claim.");
            }
            ProductionOutputBufferCapacitySourceSnapshot capacitySource =
                capacityProjector.CaptureSource(facility, capacityClaim);

            long declaredLossMass = 0L;
            long declaredExternalInputMass = 0L;
            ProductionMassExplanationAuthoringSnapshot massAuthoring =
                recipe.MassExplanation;
            if (!massAuthoring.IsEmpty)
            {
                ProductionMassExplanationDisposition disposition =
                    massExplanations.Resolve(
                        massAuthoring,
                        new ProductionMassExplanationEquationSubject(
                            recipe.RecipeId,
                            record.wipInputMassGrams,
                            0L,
                            record.processCleanWaterMassGrams,
                            physicalMass,
                            record.processWastewaterMassGrams,
                            0L));
                if (!disposition.HasDisposition)
                {
                    throw new InvalidOperationException(
                        "Authored production mass explanation resolved no disposition.");
                }
                declaredLossMass = disposition.DeclaredLossGrams;
                declaredExternalInputMass =
                    disposition.DeclaredExternalInputGrams;
                if (declaredLossMass > 0L)
                {
                    lines.Add(new ProductionPreparedOutputLineSaveData
                    {
                        outputLineId = BuildProcessLossOutputLineId(
                            recipe.RecipeId),
                        role = ProductionOutputRole.DeclaredLoss,
                        itemId = string.Empty,
                        quantity = 0,
                        componentPayload = disposition.CanonicalReceiptPayload,
                        componentFingerprint = disposition.Fingerprint,
                        qualityPermille = 1000,
                        rollKind = "process-loss",
                        rollValue = 0L,
                        rollUpperExclusive = 1L,
                        rollSucceeded = true,
                        exactMassGrams = declaredLossMass
                    });
                }
                else if (declaredExternalInputMass > 0L)
                {
                    lines.Add(new ProductionPreparedOutputLineSaveData
                    {
                        outputLineId = BuildProcessExternalInputOutputLineId(
                            recipe.RecipeId),
                        role = ProductionOutputRole.DeclaredExternalInput,
                        itemId = string.Empty,
                        quantity = 0,
                        componentPayload = disposition.CanonicalReceiptPayload,
                        componentFingerprint = disposition.Fingerprint,
                        qualityPermille = 1000,
                        rollKind = "process-addition",
                        rollValue = 0L,
                        rollUpperExclusive = 1L,
                        rollSucceeded = true,
                        exactMassGrams = declaredExternalInputMass
                    });
                }
                lines = lines
                    .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                    .ToList();
            }

            string outcomeFingerprint = ComputeOutcomeFingerprint(
                resolution,
                lines);
            string batchCommitId = ProductionPreparedOutputIdentity.BuildBatchCommitId(
                record.billId,
                record.cycleSequence,
                outcomeFingerprint);
            foreach (ProductionPreparedOutputLineSaveData line in lines)
            {
                line.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
                    batchCommitId,
                    line.outputLineId);
            }
            batch = new ProductionPreparedOutputBatchSaveData
            {
                phase = ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace,
                billId = record.billId.Value,
                cycleSequence = record.cycleSequence,
                recipeId = recipe.RecipeId,
                destinationId = record.outputDestinationId,
                recipeDefinitionDigest = recipeDigest,
                migrationProfileDigest = ProductionPreparedOutputMigrationScope
                    .CaptureProfileDigest(recipe),
                capacitySourceDigest = capacitySource.SourceDigest,
                maximumMassProofDigest = capacityClaim.MaximumMassProof
                    .SourceDigest,
                maximumBatchMassGrams = capacityClaim.MaximumMassProof
                    .MaximumBatchMassGrams,
                capacityClaimDigest = capacityClaim.SourceDigest,
                outputBufferCycleCapacity = capacitySource.CycleCapacity,
                projectedPortfolioCapacityGrams = capacitySource
                    .ProjectedPortfolioCapacityGrams,
                requiredMinimumCapacityGrams = capacitySource
                    .RequiredMinimumCapacityGrams,
                outcomeFingerprint = outcomeFingerprint,
                batchCommitId = batchCommitId,
                totalPhysicalMassGrams = physicalMass,
                totalDeclaredLossMassGrams = declaredLossMass,
                totalDeclaredExternalInputMassGrams =
                    declaredExternalInputMass,
                lines = lines
            };
            ProductionPreparedOutputContract.ValidateForBill(
                batch,
                record.billId,
                recipe.RecipeId,
                record.cycleSequence,
                record.outputDestinationId);
            failure = DomainFailure.None;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            batch = null;
            failure = Fail(record, "prepared-output-resolution-failed", exception.Message);
            return false;
        }
    }

    private bool TryResolveRuinedBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionRuinedBatchDispositionPlan disposition,
        ProductionRuinedOutputCapacityClaim capacityClaim,
        out ProductionPreparedOutputBatchSaveData batch,
        out DomainFailure failure)
    {
        if (record.preparedOutput != null
            && record.preparedOutput.phase != ProductionPreparedOutputPhase.Unresolved)
        {
            batch = record.preparedOutput.Clone();
            try
            {
                ValidateResolvedSourceAuthority(
                    record,
                    recipe,
                    facility,
                    "ruined-output-existing-batch");
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or OverflowException)
            {
                batch = null;
                failure = Fail(
                    record,
                    "prepared-output-persisted-batch-invalid",
                    exception.Message);
                return false;
            }
            if (!IsExactRuinedBatch(batch, recipe, disposition))
            {
                failure = Fail(record, "ruined-output-existing-batch-conflict");
                return false;
            }
            failure = DomainFailure.None;
            return true;
        }

        try
        {
            List<ProductionPreparedOutputLineSaveData> lines = new();
            foreach (ProductionOutputDefinition authored in
                     recipe.CaptureCanonicalOutputs()
                         .OrderBy(value => value.OutputLineId, StringComparer.Ordinal))
            {
                if (!catalog.TryGetItem(
                        authored.ItemId,
                        out ResourceItemDefinitionSO definition))
                {
                    throw new InvalidOperationException(
                        $"Ruined output item '{authored.ItemId}' is missing.");
                }
                ProductionOutputCapabilityDescriptor capability =
                    bridge.CaptureOutputCapability(
                        authored.OutputLineId,
                        authored.ItemId);
                ProductionPreparedOutputComponentProjection projection =
                    materializers.Create(capability, definition);
                lines.Add(new ProductionPreparedOutputLineSaveData
                {
                    outputLineId = authored.OutputLineId,
                    role = authored.Role,
                    itemId = authored.ItemId,
                    outputCapabilityId = capability.CapabilityId,
                    outputCapabilityVersion = capability.CapabilityVersion,
                    outputComponentCodecId = capability.ComponentCodecId,
                    outputComponentCodecVersion = capability.ComponentCodecVersion,
                    outputCapabilityFingerprint = capability.Fingerprint,
                    quantity = 0,
                    componentPayload = projection.CanonicalPayload,
                    componentFingerprint = projection.Fingerprint,
                    qualityPermille = 1000,
                    rollKind = "ruined-batch",
                    rollValue = 0L,
                    rollUpperExclusive = 1L,
                    rollSucceeded = false,
                    exactMassGrams = 0L
                });
            }

            if (!catalog.TryGetItem(
                    disposition.SpoilageItemId,
                    out ResourceItemDefinitionSO spoilageDefinition))
            {
                throw new InvalidOperationException(
                    $"Ruined recoverable item '{disposition.SpoilageItemId}' is missing.");
            }
            ProductionOutputCapabilityDescriptor wasteCapability =
                bridge.CaptureOutputCapability(
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    disposition.SpoilageItemId);
            ProductionPreparedOutputComponentProjection wasteProjection =
                materializers.Create(wasteCapability, spoilageDefinition);
            long queriedWasteMass = massQuery.GetQuantityMass(
                    (ItemDefinitionId)disposition.SpoilageItemId,
                    wasteProjection.MassSubject,
                    disposition.RecoverableWasteQuantity)
                .Value;
            if (queriedWasteMass != disposition.RecoverableWasteMassGrams)
            {
                throw new InvalidOperationException(
                    "Ruined recoverable-waste authored and queried masses conflict.");
            }
            lines.Add(new ProductionPreparedOutputLineSaveData
            {
                outputLineId = ProductionRuinedBatchDispositionPlan
                    .RecoverableWasteOutputLineId,
                role = ProductionOutputRole.RecoverableWaste,
                itemId = disposition.SpoilageItemId,
                outputCapabilityId = wasteCapability.CapabilityId,
                outputCapabilityVersion = wasteCapability.CapabilityVersion,
                outputComponentCodecId = wasteCapability.ComponentCodecId,
                outputComponentCodecVersion = wasteCapability.ComponentCodecVersion,
                outputCapabilityFingerprint = wasteCapability.Fingerprint,
                quantity = disposition.RecoverableWasteQuantity,
                componentPayload = wasteProjection.CanonicalPayload,
                componentFingerprint = wasteProjection.Fingerprint,
                qualityPermille = 1000,
                rollKind = "ruined-batch",
                rollValue = 0L,
                rollUpperExclusive = 1L,
                rollSucceeded = true,
                exactMassGrams = queriedWasteMass
            });

            if (disposition.DeclaredLossMassGrams > 0L)
            {
                const string lossPayload =
                    "production-ruined-declared-loss@1|reason=passive-batch-ruin-remainder";
                lines.Add(new ProductionPreparedOutputLineSaveData
                {
                    outputLineId = ProductionRuinedBatchDispositionPlan
                        .DeclaredLossOutputLineId,
                    role = ProductionOutputRole.DeclaredLoss,
                    itemId = string.Empty,
                    quantity = 0,
                    componentPayload = lossPayload,
                    componentFingerprint = Sha256(lossPayload),
                    qualityPermille = 1000,
                    rollKind = "ruined-batch",
                    rollValue = 0L,
                    rollUpperExclusive = 1L,
                    rollSucceeded = true,
                    exactMassGrams = disposition.DeclaredLossMassGrams
                });
            }

            lines = lines
                .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                .ToList();
            string recipeDigest = ComputeRecipeDigest(recipe);
            if (capacityClaim == null
                || capacityClaim.Disposition.RecoverableWasteQuantity
                    != disposition.RecoverableWasteQuantity
                || capacityClaim.Disposition.RecoverableWasteMassGrams
                    != disposition.RecoverableWasteMassGrams)
            {
                throw new InvalidOperationException(
                    "Ruined output capacity claim drifted before batch resolution.");
            }
            ProductionOutputBufferCapacitySourceSnapshot capacitySource =
                capacityProjector.CaptureSource(facility, capacityClaim);
            if (disposition.RecoverableWasteMassGrams
                > capacityClaim.MaximumMassProof.MaximumBatchMassGrams)
            {
                throw new InvalidOperationException(
                    "Ruined output exceeds its pre-publication maximum-mass proof.");
            }
            string outcomeFingerprint = ComputeRuinedOutcomeFingerprint(
                record,
                recipe,
                disposition,
                capacityClaim,
                lines);
            string batchCommitId = ProductionPreparedOutputIdentity.BuildBatchCommitId(
                record.billId,
                record.cycleSequence,
                outcomeFingerprint);
            foreach (ProductionPreparedOutputLineSaveData line in lines)
            {
                line.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
                    batchCommitId,
                    line.outputLineId);
            }
            batch = new ProductionPreparedOutputBatchSaveData
            {
                phase = ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace,
                billId = record.billId.Value,
                cycleSequence = record.cycleSequence,
                recipeId = recipe.RecipeId,
                destinationId = record.outputDestinationId,
                recipeDefinitionDigest = recipeDigest,
                migrationProfileDigest = ProductionPreparedOutputMigrationScope
                    .CaptureProfileDigest(recipe),
                capacitySourceDigest = capacitySource.SourceDigest,
                maximumMassProofDigest = capacityClaim.MaximumMassProof
                    .SourceDigest,
                maximumBatchMassGrams = capacityClaim.MaximumMassProof
                    .MaximumBatchMassGrams,
                capacityClaimDigest = capacityClaim.SourceDigest,
                outputBufferCycleCapacity = capacitySource.CycleCapacity,
                projectedPortfolioCapacityGrams = capacitySource
                    .ProjectedPortfolioCapacityGrams,
                requiredMinimumCapacityGrams = capacitySource
                    .RequiredMinimumCapacityGrams,
                outcomeFingerprint = outcomeFingerprint,
                batchCommitId = batchCommitId,
                totalPhysicalMassGrams = disposition.RecoverableWasteMassGrams,
                totalDeclaredLossMassGrams = disposition.DeclaredLossMassGrams,
                lines = lines
            };
            ProductionPreparedOutputContract.ValidateForBill(
                batch,
                record.billId,
                recipe.RecipeId,
                record.cycleSequence,
                record.outputDestinationId);
            if (!IsExactRuinedBatch(batch, recipe, disposition))
                throw new InvalidOperationException("Ruined output batch is not exact.");
            failure = DomainFailure.None;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            batch = null;
            failure = Fail(record, "ruined-output-resolution-failed", exception.Message);
            return false;
        }
    }

    private FacilityBufferPlannedOutputSlice CreateSlice(
        ProductionPreparedOutputLineSaveData line)
    {
        if (!catalog.TryGetItem(line.itemId, out ResourceItemDefinitionSO definition))
            throw new InvalidOperationException($"Prepared output item '{line.itemId}' is missing.");
        ProductionOutputCapabilityDescriptor capability = new(
            line.outputLineId,
            line.itemId,
            line.outputCapabilityId,
            line.outputCapabilityVersion,
            line.outputComponentCodecId,
            line.outputComponentCodecVersion,
            line.outputCapabilityFingerprint);
        ProductionPreparedOutputComponentProjection projection =
            materializers.ValidateAndDecode(
                capability,
                definition,
                line.componentPayload,
                line.componentFingerprint);
        if (projection.MassSubject.ItemInstanceId.Length != 0
            && line.quantity != 1)
        {
            throw new InvalidOperationException(
                $"Prepared output line '{line.outputLineId}' cannot materialize multiple unique instances from one saved state.");
        }
        return new FacilityBufferPlannedOutputSlice(
            line.outputLineId,
            projection.MassSubject,
            line.quantity,
            projection.RuntimeComponents,
            projection.Fingerprint);
    }

    private static bool TryCreatePhysicalCandidates(
        ProductionPreparedOutputBatchSaveData batch,
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        out ProductionPreparedOutputPhysicalCandidateSaveData[] candidates,
        out DomainFailure failure)
    {
        candidates = receipt.Stacks
            .Select(stack => Candidate(
                batch,
                stack.StackId,
                stack.OutputLineId,
                stack.ItemDefinitionId.Value,
                stack.Quantity,
                stack.MassGrams,
                receipt.DestinationId))
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
        return ValidatePhysicalCandidates(batch, candidates, out failure);
    }

    private static bool TryCreatePhysicalCandidates(
        ProductionPreparedOutputBatchSaveData batch,
        FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
        out ProductionPreparedOutputPhysicalCandidateSaveData[] candidates,
        out DomainFailure failure)
    {
        if (!string.Equals(restored.BatchCommitId, batch.batchCommitId, StringComparison.Ordinal)
            || !string.Equals(restored.OutcomeFingerprint, batch.outcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(restored.PlannedOutputFingerprint, batch.admissionFingerprint, StringComparison.Ordinal))
        {
            candidates = Array.Empty<ProductionPreparedOutputPhysicalCandidateSaveData>();
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                batch.billId,
                "prepared-output-restore-fingerprint-mismatch");
            return false;
        }
        candidates = restored.Stacks
            .Select(stack => Candidate(
                batch,
                stack.StackId,
                stack.OutputLineId,
                stack.ItemId,
                stack.Quantity,
                stack.MassGrams,
                stack.DestinationId))
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
        return ValidatePhysicalCandidates(batch, candidates, out failure);
    }

    private static ProductionPreparedOutputPhysicalCandidateSaveData Candidate(
        ProductionPreparedOutputBatchSaveData batch,
        string stackId,
        string outputLineId,
        string itemId,
        int quantity,
        long massGrams,
        string destinationId)
    {
        ProductionPreparedOutputLineSaveData line = batch.lines.Single(value =>
            string.Equals(value.outputLineId, outputLineId, StringComparison.Ordinal));
        return new ProductionPreparedOutputPhysicalCandidateSaveData
        {
            stackId = stackId,
            batchCommitId = batch.batchCommitId,
            outputLineId = outputLineId,
            lineCommitId = line.lineCommitId,
            itemId = itemId,
            quantity = quantity,
            massGrams = massGrams,
            destinationId = destinationId,
            state = ProductionPreparedPhysicalCandidateState.FacilityOutputBuffer
        };
    }

    private static bool ValidatePhysicalCandidates(
        ProductionPreparedOutputBatchSaveData batch,
        ProductionPreparedOutputPhysicalCandidateSaveData[] candidates,
        out DomainFailure failure)
    {
        try
        {
            ProductionPreparedOutputBatchSaveData probe = batch.Clone();
            probe.phase =
                ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending;
            probe.physicalCandidates = candidates.Select(value => value.Clone()).ToList();
            ProductionPreparedOutputContract.ValidateForBill(
                probe,
                (ProductionBillId)batch.billId,
                batch.recipeId,
                batch.cycleSequence,
                batch.destinationId);
            failure = DomainFailure.None;
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                           or ArgumentException
                                           or OverflowException)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                batch.billId,
                "prepared-output-physical-candidate-invalid",
                exception.Message);
            return false;
        }
    }

    private static bool ProfileMatches(
        ProductionFacilityHandle facility,
        FacilityBufferCapacityProfile profile) => facility != null
        && profile != null
        && profile.CapacityRevision ==
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision
        && string.Equals(
            profile.DestinationId,
            ProductionBillRuntime.OutputDestinationPrefix + facility.InstanceId.Value,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerDomain,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerOperationId,
            profile.DestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            profile.OwnerFacilityId,
            facility.InstanceId.Value,
            StringComparison.Ordinal);

    private ProductionOutputBufferCapacitySourceSnapshot ValidateCapacitySource(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionPreparedOutputBatchSaveData batch,
        ProductionFacilityHandle facility,
        string context)
    {
        if (record == null || recipe == null || batch == null)
            throw new ArgumentNullException(nameof(batch));
        ProductionOutputBufferCapacitySourceSnapshot current;
        ProductionPreparedOutputLineSaveData ruinedWaste = batch.lines?
            .SingleOrDefault(value => value != null
                && value.role == ProductionOutputRole.RecoverableWaste
                && string.Equals(
                    value.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    StringComparison.Ordinal));
        if (ruinedWaste != null)
        {
            ProductionOutputCapabilityDescriptor descriptor = new(
                ruinedWaste.outputLineId,
                ruinedWaste.itemId,
                ruinedWaste.outputCapabilityId,
                ruinedWaste.outputCapabilityVersion,
                ruinedWaste.outputComponentCodecId,
                ruinedWaste.outputComponentCodecVersion,
                ruinedWaste.outputCapabilityFingerprint);
            ProductionRuinedOutputCapacityClaim claim = capacityProjector
                .CaptureRuinedClaim(record, recipe, descriptor);
            if (!string.Equals(
                    batch.maximumMassProofDigest,
                    claim.MaximumMassProof.SourceDigest,
                    StringComparison.Ordinal)
                || batch.maximumBatchMassGrams
                    != claim.MaximumMassProof.MaximumBatchMassGrams
                || !string.Equals(
                    batch.capacityClaimDigest,
                    claim.SourceDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    (context ?? string.Empty)
                    + ":prepared-output-capacity-proof-stale");
            }
            current = capacityProjector.CaptureSource(facility, claim);
        }
        else
        {
            ProductionPreparedOutputCapacityClaim claim = capacityProjector
                .CapturePreparedClaim(batch, recipe);
            if (!string.Equals(
                    batch.maximumMassProofDigest,
                    claim.MaximumMassProof.SourceDigest,
                    StringComparison.Ordinal)
                || batch.maximumBatchMassGrams
                    != claim.MaximumMassProof.MaximumBatchMassGrams
                || !string.Equals(
                    batch.capacityClaimDigest,
                    claim.SourceDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    (context ?? string.Empty)
                    + ":prepared-output-capacity-proof-stale");
            }
            current = capacityProjector.CaptureSource(facility, claim);
        }
        return ProductionOutputBufferCapacitySourceGuard.ValidateSaved(
            batch,
            current,
            context);
    }

    private void ValidateResolvedSourceAuthority(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        string context)
    {
        if (record == null || recipe == null || facility == null)
            throw new ArgumentNullException(nameof(record));
        ProductionPreparedOutputBatchSaveData batch = record.preparedOutput
            ?? throw new InvalidOperationException(
                "Prepared output source validation has no batch.");
        ProductionPreparedOutputContract.ValidateForBill(
            batch,
            record.billId,
            recipe.RecipeId,
            record.cycleSequence,
            record.outputDestinationId);
        ProductionPreparedOutputMigrationScope.ValidateCanonicalProfileOrThrow(
            recipe);
        ProductionPreparedOutputSourceRevisionGuard.ValidateResolvedBatch(
            batch,
            recipe,
            context);
        ProductionPreparedOutputMigrationScope.ValidateSavedProfileDigest(
            batch,
            recipe,
            context);
        foreach (ProductionPreparedOutputLineSaveData line in batch.lines
                     .Where(value => ProductionOutputRoleRules.IsPhysical(
                         value.role)))
        {
            ProductionOutputCapabilityDescriptor capability = new(
                line.outputLineId,
                line.itemId,
                line.outputCapabilityId,
                line.outputCapabilityVersion,
                line.outputComponentCodecId,
                line.outputComponentCodecVersion,
                line.outputCapabilityFingerprint);
            if (!bridge.TryValidateOutputCapability(
                    capability,
                    out DomainFailure capabilityFailure))
            {
                throw new InvalidOperationException(
                    $"Prepared output line '{line.outputLineId}' has a missing or drifted output capability: {capabilityFailure.Code}.");
            }
            if (!catalog.TryGetItem(
                    line.itemId,
                    out ResourceItemDefinitionSO definition))
            {
                throw new InvalidOperationException(
                    $"Prepared output item '{line.itemId}' is missing.");
            }
            materializers.ValidateAndDecode(
                capability,
                definition,
                line.componentPayload,
                line.componentFingerprint);
        }
        ValidateCapacitySource(record, recipe, batch, facility, context);
    }

    private bool TryValidateScope(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out DomainFailure failure)
    {
        if (record == null
            || recipe == null
            || facility == null
            || facility.IsDestroyed
            || ProductionPreparedOutputMigrationScope
                .HasLegacyOutputAuthority(record)
            || !ProductionPreparedOutputCapabilitySelection
                .UsesPreparedOutputMaterializer(recipe, bridge)
            || !string.Equals(record.recipeId, recipe.RecipeId, StringComparison.Ordinal)
            || !record.buildingInstanceId.Equals(facility.InstanceId)
            || !string.Equals(
                record.outputDestinationId,
                ProductionBillRuntime.OutputDestinationPrefix + facility.InstanceId.Value,
                StringComparison.Ordinal))
        {
            failure = Fail(record, "prepared-output-scope-invalid");
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    private bool TryCreateRuinedDisposition(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out ProductionRuinedBatchDispositionPlan disposition,
        out ProductionRuinedOutputCapacityClaim capacityClaim,
        out DomainFailure failure)
    {
        disposition = default;
        capacityClaim = null;
        if (!TryValidateScope(record, recipe, facility, out failure))
            return false;
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch
            || record.batchIntegrity > 0f
            || record.wipInputMassGrams <= 0L
            || record.processCleanWaterMassGrams < 0L
            || record.processWastewaterMassGrams < 0L
            || ProductionPreparedOutputMigrationScope
                .HasLegacyOutputAuthority(record))
        {
            failure = Fail(record, "ruined-output-capability-contract-invalid");
            return false;
        }
        if (recipe.CaptureCanonicalOutputs().Any(output => string.Equals(
                    output.OutputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    StringComparison.Ordinal)
                || string.Equals(
                    output.OutputLineId,
                    ProductionRuinedBatchDispositionPlan.DeclaredLossOutputLineId,
                    StringComparison.Ordinal)))
        {
            failure = Fail(record, "ruined-output-reserved-line-id-conflict");
            return false;
        }

        try
        {
            if (!catalog.TryGetItem(
                    recipe.SpoilageItemId,
                    out ResourceItemDefinitionSO spoilageDefinition))
            {
                failure = Fail(
                    record,
                    "ruined-output-spoilage-item-missing",
                    recipe.SpoilageItemId);
                return false;
            }
            ProductionOutputCapabilityDescriptor spoilageCapability =
                bridge.CaptureOutputCapability(
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    recipe.SpoilageItemId);
            try
            {
                materializers.ValidateDescriptor(spoilageCapability);
            }
            catch (InvalidOperationException exception)
            {
                failure = Fail(
                    record,
                    "ruined-output-spoilage-capability-unsupported",
                    recipe.SpoilageItemId,
                    exception.Message);
                return false;
            }
            capacityClaim = capacityProjector.CaptureRuinedClaim(
                record,
                recipe,
                spoilageCapability);
            disposition = capacityClaim.Disposition;
            if (disposition.SpoilageUnitMassGrams <= 0L
                || checked(
                    disposition.RecoverableWasteMassGrams
                    + disposition.ProcessWastewaterMassGrams
                    + disposition.DeclaredLossMassGrams)
                    != disposition.AvailableMassGrams)
            {
                failure = Fail(record, "ruined-output-capability-mass-invalid");
                disposition = default;
                capacityClaim = null;
                return false;
            }
            failure = DomainFailure.None;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or KeyNotFoundException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failure = Fail(
                record,
                "ruined-output-capability-plan-failed",
                exception.Message);
            disposition = default;
            capacityClaim = null;
            return false;
        }
    }

    private static bool IsExactRuinedBatch(
        ProductionPreparedOutputBatchSaveData batch,
        ProductionRecipeSO recipe,
        ProductionRuinedBatchDispositionPlan disposition)
    {
        if (batch == null
            || recipe == null
            || !string.Equals(
                batch.recipeId,
                recipe.RecipeId,
                StringComparison.Ordinal)
            || batch.totalPhysicalMassGrams !=
                disposition.RecoverableWasteMassGrams
            || batch.totalDeclaredLossMassGrams !=
                disposition.DeclaredLossMassGrams
            || batch.lines == null
            || batch.lines.Any(value => value == null)
            || batch.lines.Select(value => value.outputLineId)
                .Distinct(StringComparer.Ordinal).Count() != batch.lines.Count)
        {
            return false;
        }
        ProductionOutputDefinition[] authored = recipe.CaptureCanonicalOutputs()
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        int expectedLineCount = authored.Length + 1
            + (disposition.DeclaredLossMassGrams > 0L ? 1 : 0);
        if (batch.lines.Count != expectedLineCount)
            return false;
        foreach (ProductionOutputDefinition output in authored)
        {
            ProductionPreparedOutputLineSaveData failed = batch.lines
                .SingleOrDefault(value => string.Equals(
                    value.outputLineId,
                    output.OutputLineId,
                    StringComparison.Ordinal));
            if (failed == null
                || failed.role != output.Role
                || !string.Equals(
                    failed.itemId,
                    output.ItemId,
                    StringComparison.Ordinal)
                || failed.rollSucceeded
                || failed.quantity != 0
                || failed.exactMassGrams != 0L)
            {
                return false;
            }
        }
        ProductionPreparedOutputLineSaveData waste = batch.lines.SingleOrDefault(
            value => value != null
                && value.role == ProductionOutputRole.RecoverableWaste
                && string.Equals(
                    value.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    StringComparison.Ordinal));
        ProductionPreparedOutputLineSaveData loss = batch.lines.SingleOrDefault(
            value => value != null
                && value.role == ProductionOutputRole.DeclaredLoss
                && string.Equals(
                    value.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .DeclaredLossOutputLineId,
                    StringComparison.Ordinal));
        return waste != null
            && string.Equals(
                waste.itemId,
                disposition.SpoilageItemId,
                StringComparison.Ordinal)
            && waste.rollSucceeded
            && waste.quantity == disposition.RecoverableWasteQuantity
            && waste.exactMassGrams == disposition.RecoverableWasteMassGrams
            && (disposition.DeclaredLossMassGrams == 0L
                ? loss == null
                : loss != null
                    && loss.rollSucceeded
                    && loss.quantity == 0
                    && loss.itemId.Length == 0
                    && loss.exactMassGrams ==
                        disposition.DeclaredLossMassGrams);
    }

    private static ProductionPreparedOutputExecutionResult Block(
        ProductionBillRecord record,
        ProductionPreparedOutputPhase phase,
        DomainFailure failure) => ProductionPreparedOutputExecutionResult.Blocked(
        phase == ProductionPreparedOutputPhase.Completed
            ? ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending
            : phase,
        failure.IsFailure ? failure : Fail(record, "prepared-output-failure-missing"));

    private static ProductionRuinedBatchExecutionResult RuinedBlocked(
        ProductionBillRecord record,
        DomainFailure failure)
    {
        ProductionPreparedOutputPhase phase = SafePhase(record);
        if (phase == ProductionPreparedOutputPhase.Completed)
        {
            phase = ProductionPreparedOutputPhase
                .PhysicalBatchCommittedPublicationPending;
        }
        return ProductionRuinedBatchExecutionResult.Blocked(
            phase,
            failure.IsFailure
                ? failure
                : Fail(record, "ruined-output-failure-missing"));
    }

    private static ProductionPreparedOutputPhase SafePhase(
        ProductionBillRecord record)
    {
        ProductionPreparedOutputPhase phase = record?.preparedOutput?.phase
            ?? ProductionPreparedOutputPhase.Unresolved;
        return Enum.IsDefined(typeof(ProductionPreparedOutputPhase), phase)
            ? phase
            : ProductionPreparedOutputPhase.Unresolved;
    }

    private static DomainFailure Fail(
        ProductionBillRecord record,
        params string[] details)
    {
        string[] parameters = new[] { record?.billId.Value ?? string.Empty }
            .Concat(details ?? Array.Empty<string>())
            .ToArray();
        return new DomainFailure(FailureCode.ProductionOutputUnavailable, parameters);
    }

    private void ResetRestoreCandidate()
    {
        previousLiveByBatch = null;
        restoreCandidateActive = false;
        restoreCandidatePublished = false;
    }

    private static string ComputeRecipeDigest(ProductionRecipeSO recipe)
        => ProductionRecipeSemanticDigest.Capture(recipe);

    private static string BuildProcessLossOutputLineId(string recipeId) =>
        "output:" + recipeId + "/999/declared-loss/process-loss";

    private static string BuildProcessExternalInputOutputLineId(
        string recipeId) =>
        "output:" + recipeId
        + "/999/declared-external-input/process-addition";

    private static string ComputeOutcomeFingerprint(
        CanonicalProductionOutputResolution resolution,
        IEnumerable<ProductionPreparedOutputLineSaveData> lines)
    {
        StringBuilder text = new();
        Append(text, "production-prepared-outcome-v2");
        Append(text, resolution.RootSeed.ToString(CultureInfo.InvariantCulture));
        Append(text, resolution.BillId.Value);
        Append(text, resolution.CycleSequence.ToString(CultureInfo.InvariantCulture));
        Append(text, resolution.RecipeId);
        Append(text, resolution.OutputFactorNumerator.ToString(CultureInfo.InvariantCulture));
        Append(text, resolution.OutputFactorDenominator.ToString(CultureInfo.InvariantCulture));
        Append(text, resolution.PassiveBatchIntegrity.ToString("R", CultureInfo.InvariantCulture));
        foreach (ProductionPreparedOutputLineSaveData line in lines
                     .OrderBy(value => value.outputLineId, StringComparer.Ordinal))
        {
            Append(text, line.outputLineId);
            Append(text, ((int)line.role).ToString(CultureInfo.InvariantCulture));
            Append(text, line.itemId);
            Append(text, line.outputCapabilityId);
            Append(text, line.outputCapabilityVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(text, line.outputComponentCodecId);
            Append(text, line.outputComponentCodecVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(text, line.outputCapabilityFingerprint);
            Append(text, line.quantity.ToString(CultureInfo.InvariantCulture));
            Append(text, line.exactMassGrams.ToString(CultureInfo.InvariantCulture));
            Append(text, line.componentFingerprint);
            Append(text, line.rollSucceeded ? "1" : "0");
        }
        return Sha256(text.ToString());
    }

    private static string ComputeRuinedOutcomeFingerprint(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionRuinedBatchDispositionPlan disposition,
        ProductionRuinedOutputCapacityClaim capacityClaim,
        IEnumerable<ProductionPreparedOutputLineSaveData> lines)
    {
        StringBuilder text = new();
        Append(text, "production-ruined-outcome-v4");
        Append(text, record.billId.Value);
        Append(text, record.cycleSequence.ToString(CultureInfo.InvariantCulture));
        Append(text, recipe.RecipeId);
        Append(text, record.wipInputMassGrams.ToString(CultureInfo.InvariantCulture));
        Append(text, record.processCleanWaterMassGrams.ToString(
            CultureInfo.InvariantCulture));
        Append(text, record.processWastewaterMassGrams.ToString(
            CultureInfo.InvariantCulture));
        Append(text, disposition.SpoilageItemId);
        Append(text, capacityClaim.SourceDigest);
        Append(text, capacityClaim.MaximumMassProof.SourceDigest);
        Append(text, capacityClaim.MaximumMassProof.MaximumBatchMassGrams
            .ToString(CultureInfo.InvariantCulture));
        foreach (ProductionPreparedOutputLineSaveData line in lines
                     .OrderBy(value => value.outputLineId, StringComparer.Ordinal))
        {
            Append(text, line.outputLineId);
            Append(text, ((int)line.role).ToString(CultureInfo.InvariantCulture));
            Append(text, line.itemId);
            Append(text, line.outputCapabilityId);
            Append(text, line.outputCapabilityVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(text, line.outputComponentCodecId);
            Append(text, line.outputComponentCodecVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(text, line.outputCapabilityFingerprint);
            Append(text, line.quantity.ToString(CultureInfo.InvariantCulture));
            Append(text, line.exactMassGrams.ToString(CultureInfo.InvariantCulture));
            Append(text, line.componentFingerprint);
            Append(text, line.rollSucceeded ? "1" : "0");
        }
        return Sha256(text.ToString());
    }

    private static void Append(StringBuilder target, string value)
    {
        string canonical = value ?? string.Empty;
        target.Append(Encoding.UTF8.GetByteCount(canonical).ToString(
            CultureInfo.InvariantCulture));
        target.Append(':').Append(canonical).Append('|');
    }

    private static string Sha256(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte part in digest)
            result.Append(part.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private sealed class LivePublication
    {
        internal LivePublication(FacilityBufferPlannedOutputToken token)
        {
            Token = token;
        }

        internal FacilityBufferPlannedOutputToken Token { get; }
        internal bool HasReceipt { get; private set; }
        internal FacilityBufferPlannedOutputPublicationReceipt Receipt { get; private set; }

        internal void SetReceipt(
            FacilityBufferPlannedOutputPublicationReceipt receipt)
        {
            Receipt = receipt;
            HasReceipt = true;
        }
    }
}
