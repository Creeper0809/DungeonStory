using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

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

    private readonly ProductionPreparedOutputResolutionPlanner resolutionPlanner;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionAssemblyBridge bridge;
    private readonly IProductionPreparedOutputMaterializerRegistry materializers;
    private readonly ProductionOutputBufferCapacityProjector capacityProjector;
    private readonly IProductionOutputDestinationAuthorityRuntime destinations;
    private readonly IFacilityBufferMassCapacityQuery capacities;
    private readonly IFacilityBufferPhysicalOccupancyQuery occupancy;
    private readonly IFacilityBufferMassAdmissionService admission;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;
    private readonly IPreparedFacilityBufferOutputPublicationService
        preparedPublication;
    private readonly ProductionCompletionObservationRuntime observations;
    private readonly IGameCalendar calendar;
    private readonly IProductionPreparedOutputRoutingAuthority routingAuthority;
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
        ProductionMassExplanationCapabilityRegistry massExplanations = null,
        IDomainFactStagingPublisher completionObservations = null,
        IDomainFactRecoveryPublisher completionRecovery = null,
        IGameCalendar calendar = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ = outputPlanning
            ?? throw new ArgumentNullException(nameof(outputPlanning));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _ = grandProjectBenefits
            ?? throw new ArgumentNullException(nameof(grandProjectBenefits));
        _ = resolver ?? throw new ArgumentNullException(nameof(resolver));
        this.materializers = materializers
            ?? throw new ArgumentNullException(nameof(materializers));
        _ = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
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
        preparedPublication = publication
            as IPreparedFacilityBufferOutputPublicationService
            ?? throw new ArgumentException(
                "Prepared publication must expose a reversible prepare port.",
                nameof(publication));
        this.routingAuthority = routingAuthority
            ?? throw new ArgumentNullException(nameof(routingAuthority));
        massExplanations = massExplanations
            ?? ProductionMassExplanationCapabilityRegistry.CreateDefault();
        if ((completionObservations == null) != (completionRecovery == null))
            throw new ArgumentException("Production completion observation ports must be connected together.");
        observations = completionObservations == null ? null : new ProductionCompletionObservationRuntime(completionObservations, completionRecovery);
        this.calendar = calendar;
        resolutionPlanner = new ProductionPreparedOutputResolutionPlanner(
            catalog, outputPlanning, bridge, grandProjectBenefits, resolver,
            materializers, massQuery, capacityProjector, massExplanations);
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
            string destinationId = ProductionBillIdentity.OutputDestinationPrefix
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
                resolutionPlanner.ValidateResolvedSourceAuthority(
                    record,
                    preparedRecipe,
                    facility,
                    $"Production output restore bill '{record.billId.Value}'");
                ProductionOutputBufferCapacitySourceSnapshot batchCapacity =
                    resolutionPlanner.ValidateCapacitySource(
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
        if (!resolutionPlanner.TryValidateScope(record, recipe, facility, out DomainFailure failure))
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
            return ProductionPreparedOutputCapacityResult.Unavailable(ProductionPreparedOutputResolutionPlanner.Fail(
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
            return ProductionPreparedOutputCapacityResult.Unavailable(ProductionPreparedOutputResolutionPlanner.Fail(
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
            return ProductionPreparedOutputCapacityResult.Unavailable(ProductionPreparedOutputResolutionPlanner.Fail(
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
        if (!resolutionPlanner.TryValidateScope(record, recipe, facility, out DomainFailure failure)
            || !resolutionPlanner.TryResolveBatch(
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
            bootstrapCapacity = resolutionPlanner.ValidateCapacitySource(
                    record,
                    recipe,
                    batch,
                    facility,
                    $"Production bill '{record.billId.Value}'")
                .RequiredMinimumCapacityGrams;
        }
        catch (OverflowException)
        {
            return ProductionPreparedOutputCapacityResult.Unavailable(ProductionPreparedOutputResolutionPlanner.Fail(
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
            return ProductionPreparedOutputCapacityResult.Unavailable(ProductionPreparedOutputResolutionPlanner.Fail(
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
            return ProductionPreparedOutputCapacityResult.Unavailable(ProductionPreparedOutputResolutionPlanner.Fail(
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
        if (!resolutionPlanner.TryValidateScope(record, recipe, facility, out DomainFailure failure))
        {
            return ProductionPreparedOutputExecutionResult.Blocked(
                SafePhase(record),
                failure);
        }

        try
        {
            if (SafePhase(record) != ProductionPreparedOutputPhase.Unresolved)
            {
                resolutionPlanner.ValidateResolvedSourceAuthority(
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
                        if (!resolutionPlanner.TryResolveBatch(record, recipe, facility, out ProductionPreparedOutputBatchSaveData resolved, out failure))
                            return Block(record, phase, failure);
                        record.ResolvePreparedOutput(resolved);
                        break;

                    case ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace:
                        if (!TryReserve(record, recipe, facility, out failure))
                            return Block(record, phase, failure);
                        break;

                    case ProductionPreparedOutputPhase.PublicationPrepared:
                        if (!TryPublishOrJoin(
                                record,
                                facility,
                                worker,
                                out failure))
                            return Block(record, SafePhase(record), failure);
                        break;

                    case ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending:
                        if (!TryCommitAndAcknowledge(record, out failure))
                            return Block(record, phase, failure);
                        record.MarkPreparedOutputCompleted();
                        break;

                    case ProductionPreparedOutputPhase.Completed:
                        if (!TryRequireCompletedObservation(
                                record,
                                out failure))
                        {
                            return Block(record, phase, failure);
                        }
                        routingAuthority.PublishCommittedBatch(
                            record.preparedOutput,
                            facility.InstanceId);
                        return ProductionPreparedOutputExecutionResult.Completed();

                    default:
                        return Block(
                            record,
                            ProductionPreparedOutputPhase.Unresolved,
                            ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-phase-invalid"));
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
                ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-execution-failed", exception.Message));
        }

        return Block(
            record,
            SafePhase(record),
            ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-transition-budget-exhausted"));
    }

    public ProductionRuinedBatchExecutionResult ExecuteRuinedBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        if (!resolutionPlanner.TryCreateRuinedDisposition(
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
                resolutionPlanner.ValidateResolvedSourceAuthority(
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
                        if (!resolutionPlanner.TryResolveRuinedBatch(
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
                        if (!TryPublishOrJoin(
                                record,
                                facility,
                                null,
                                out failure))
                            return RuinedBlocked(record, failure);
                        break;

                    case ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending:
                        if (!TryCommitAndAcknowledge(record, out failure))
                            return RuinedBlocked(record, failure);
                        record.MarkPreparedOutputCompleted();
                        break;

                    case ProductionPreparedOutputPhase.Completed:
                        if (!TryRequireCompletedObservation(
                                record,
                                out failure))
                        {
                            return RuinedBlocked(record, failure);
                        }
                        if (!ProductionPreparedOutputResolutionPlanner.IsExactRuinedBatch(
                                record.preparedOutput,
                                recipe,
                                disposition))
                        {
                            return RuinedBlocked(
                                record,
                                ProductionPreparedOutputResolutionPlanner.Fail(record, "ruined-output-completed-batch-conflict"));
                        }
                        routingAuthority.PublishCommittedBatch(
                            record.preparedOutput,
                            facility.InstanceId);
                        return ProductionRuinedBatchExecutionResult.Completed(
                            disposition);

                    default:
                        return RuinedBlocked(
                            record,
                            ProductionPreparedOutputResolutionPlanner.Fail(record, "ruined-output-phase-invalid"));
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
                ProductionPreparedOutputResolutionPlanner.Fail(record, "ruined-output-execution-failed", exception.Message));
        }

        return RuinedBlocked(
            record,
            ProductionPreparedOutputResolutionPlanner.Fail(record, "ruined-output-transition-budget-exhausted"));
    }

    public ProductionPreparedOutputReleaseResult Release(
        ProductionBillRecord record,
        ProductionWipTerminalReason reason)
    {
        if (record?.preparedOutput == null)
        {
            return ProductionPreparedOutputReleaseResult.Blocked(
                false,
                ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-release-state-missing"));
        }
        ProductionPreparedOutputPhase phase = record.preparedOutput.phase;
        if (phase == ProductionPreparedOutputPhase.Unresolved)
            return ProductionPreparedOutputReleaseResult.ReleasedUnpublished();
        if (phase is ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending
            or ProductionPreparedOutputPhase.Completed)
        {
            return ProductionPreparedOutputReleaseResult.Blocked(
                true,
                ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-physical-batch-retained"));
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
                            ProductionPreparedOutputResolutionPlanner.Fail(
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
                        ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-admission-release-failed", releaseFailure));
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
                    ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-physical-batch-retained"));
            }
            else if (!captureFailure.StartsWith(
                         MissingBatchPrefix,
                         StringComparison.Ordinal))
            {
                return ProductionPreparedOutputReleaseResult.Blocked(
                    true,
                    ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-publication-conflict", captureFailure));
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
            resolutionPlanner.ValidateCapacitySource(
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
            failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-authority-unavailable", authorityFailure);
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
            failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-admission-mass-mismatch");
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
            failure = ProductionPreparedOutputResolutionPlanner.Fail(
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

    private bool TryPublishOrJoin(ProductionBillRecord record, ProductionFacilityHandle facility,
        ProductionWorkerHandle worker, out DomainFailure failure)
    {
        record.BeginCompletionPublication();
        try { return TryPublishOrJoinCore(record, facility, worker, out failure); }
        finally { record.EndCompletionPublication(); }
    }

    private bool TryPublishOrJoinCore(
        ProductionBillRecord record,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        out DomainFailure failure)
    {
        ProductionPreparedOutputBatchSaveData batch = record.preparedOutput;
#if UNITY_EDITOR
        if (observations == null)
            return TryPublishOrJoinDetachedEditorFixture(record, out failure);
#endif
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
                {
                    return false;
                }
                FacilityBufferPlannedOutputPublicationReceipt restoredReceipt =
                    RestorePublicationReceipt(batch, restored, facility);
                if (!TryPrepareCompletionFact(
                        record,
                        facility,
                        worker,
                        restoredReceipt,
                        out IDomainFactStage restoredOutcome,
                        out failure))
                {
                    return false;
                }
                record.MarkPreparedOutputPhysicalBatchCommitted(candidates);
                DomainFactDelivery restoredCommit = observations.Commit(record, restoredOutcome);
                restoredOutcome.Cancel();
                if (restoredCommit.Phase == DomainFactDeliveryPhase.Rejected)
                {
                    if (!publication.TryRollbackRestoreCandidate(
                            restored,
                            out _,
                            out string rollbackFailure))
                    {
                        // Keep the aggregate in the matching physical-pending
                        // phase.  The repository remains authoritative and a
                        // later retry can prepare a fresh outcome token for
                        // this same immutable batch.
                        failure = ProductionPreparedOutputResolutionPlanner.Fail(
                            record,
                            "prepared-output-outcome-rollback-failed",
                            rollbackFailure);
                        return false;
                    }
                    record.RollbackPreparedOutputPhysicalBatch();
                    record.ClearCompletionObservation();
                    restoredOutcome.Cancel();
                    failure = ProductionPreparedOutputResolutionPlanner.Fail(
                        record,
                        "prepared-output-outcome-commit-failed",
                        restoredCommit.Detail);
                    return false;
                }
                failure = DomainFailure.None;
                return true;
            }
            if (!restoreFailure.StartsWith(MissingBatchPrefix, StringComparison.Ordinal))
            {
                failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-publication-conflict", restoreFailure);
                return false;
            }
            record.ReturnPreparedOutputToWaitingForSpace();
            failure = new DomainFailure(
                FailureCode.ProductionOutputSpaceUnavailable,
                record.outputDestinationId,
                "prepared-output-admission-restored-as-waiting");
            return false;
        }

        if (live.HasReceipt)
        {
            if (!TryCreatePhysicalCandidates(
                    batch,
                    live.Receipt,
                    out ProductionPreparedOutputPhysicalCandidateSaveData[] existing,
                    out failure))
            {
                return false;
            }
            record.MarkPreparedOutputPhysicalBatchCommitted(existing);
            return true;
        }

        if (!preparedPublication.TryPrepareFullBatch(
                live.Token,
                out IPreparedFacilityBufferOutputPublication preparedPhysical,
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
            failure = ProductionPreparedOutputResolutionPlanner.Fail(
                record,
                "prepared-output-publication-prepare-failed",
                publicationFailure);
            return false;
        }

        FacilityBufferPlannedOutputPublicationReceipt receipt =
            preparedPhysical.Preview.Receipt;
        if (!TryCreatePhysicalCandidates(
                batch,
                receipt,
                out ProductionPreparedOutputPhysicalCandidateSaveData[] physical,
                out failure))
        {
            preparedPhysical.Cancel();
            return false;
        }
        if (!TryPrepareCompletionFact(
                record,
                facility,
                worker,
                receipt,
                out IDomainFactStage preparedOutcome,
                out failure))
        {
            preparedPhysical.Cancel();
            return false;
        }
        if (preparedOutcome.IsReplay)
        {
            // A durable outcome without the matching physical publication is
            // a split-brain conflict; never mint a second batch.
            preparedPhysical.Cancel();
            preparedOutcome.Cancel();
            failure = ProductionPreparedOutputResolutionPlanner.Fail(
                record,
                "prepared-output-outcome-replay-without-physical-owner");
            return false;
        }
        if (!preparedPhysical.TryApply(
                out IReversibleFacilityBufferOutputPublication applied,
                out _,
                out publicationFailure))
        {
            preparedOutcome.Cancel();
            record.ClearCompletionObservation();
            failure = ProductionPreparedOutputResolutionPlanner.Fail(
                record,
                "prepared-output-publication-failed",
                publicationFailure);
            return false;
        }
        try
        {
            record.MarkPreparedOutputPhysicalBatchCommitted(physical);
        }
        catch (Exception transitionError)
        {
            bool rolledBack = applied.TryRollback(out _, out string rollbackFailure);
            preparedOutcome.Cancel();
            if (rolledBack) record.ClearCompletionObservation();
            else throw new InvalidOperationException(
                "prepared-output-owner-transition-rollback-failed:" + rollbackFailure, transitionError);
            throw;
        }

        DomainFactDelivery outcomeCommit = observations.Commit(record, preparedOutcome);
        preparedOutcome.Cancel();
        if (outcomeCommit.Phase == DomainFactDeliveryPhase.Rejected)
        {
            if (!applied.TryRollback(out _, out string rollbackFailure))
            {
                // Do not publish an aggregate rollback while the physical
                // repository still owns the exact batch.  The pending owner
                // state makes the split recoverable on the next retry.
                failure = ProductionPreparedOutputResolutionPlanner.Fail(
                    record,
                    "prepared-output-outcome-rollback-failed",
                    rollbackFailure);
                return false;
            }
            record.RollbackPreparedOutputPhysicalBatch();
            record.ClearCompletionObservation();
            preparedOutcome.Cancel();
            failure = ProductionPreparedOutputResolutionPlanner.Fail(
                record,
                "prepared-output-outcome-commit-failed",
                outcomeCommit.Detail);
            return false;
        }
        live.SetReceipt(receipt);
        failure = DomainFailure.None;
        return true;
    }

#if UNITY_EDITOR
    [Obsolete(
        "Detached physical-publication fixtures do not stand in for the mandatory runtime gameplay-outcome transaction.")]
    private bool TryPublishOrJoinDetachedEditorFixture(
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
                        out ProductionPreparedOutputPhysicalCandidateSaveData[] restoredPhysical,
                        out failure))
                {
                    return false;
                }
                record.MarkPreparedOutputPhysicalBatchCommitted(restoredPhysical);
                return true;
            }
            if (!restoreFailure.StartsWith(MissingBatchPrefix, StringComparison.Ordinal))
            {
                failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-publication-conflict", restoreFailure);
                return false;
            }
            record.ReturnPreparedOutputToWaitingForSpace();
            failure = new DomainFailure(
                FailureCode.ProductionOutputSpaceUnavailable,
                record.outputDestinationId,
                "prepared-output-admission-restored-as-waiting");
            return false;
        }
        FacilityBufferPlannedOutputPublicationReceipt receipt = default;
        if (!live.HasReceipt
            && !publication.TryPublishFullBatch(
                live.Token,
                out receipt,
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
            failure = ProductionPreparedOutputResolutionPlanner.Fail(
                record,
                "prepared-output-publication-failed",
                publicationFailure);
            return false;
        }
        else if (!live.HasReceipt)
        {
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
#endif

    private bool TryCommitAndAcknowledge(
        ProductionBillRecord record,
        out DomainFailure failure)
    {
        // Keep the physical publication and owner in the same retryable phase
        // until its frozen completion fact is confirmed by the observer.
        if (!TryRequireCompletedObservation(record, out failure)) return false;
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
                failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-admission-commit-failed", commitFailure);
                return false;
            }
            if (!publication.TryAcknowledgePublishedBatch(
                    live.Receipt,
                    out _,
                    out string liveAcknowledgementFailure))
            {
                failure = ProductionPreparedOutputResolutionPlanner.Fail(
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
                failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-restore-join-failed", restoreFailure);
            return false;
        }
        if (!publication.TryAcknowledgeRestoreCandidate(
                restored,
                out _,
                out string acknowledgementFailure))
        {
            failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-restore-acknowledgement-failed", acknowledgementFailure);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    private bool TryPrepareCompletionFact(ProductionBillRecord record, ProductionFacilityHandle facility,
        ProductionWorkerHandle worker, in FacilityBufferPlannedOutputPublicationReceipt receipt,
        out IDomainFactStage prepared, out DomainFailure failure)
    {
        prepared = null;
        if (observations == null || calendar == null)
        { failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "production-completion-observer-not-connected"); return false; }
        var frozenPreview = receipt;
        if (!observations.TryPrepare(record,
                () => ProductionCycleFactCapture.Prepared(record, facility, worker, frozenPreview, calendar.Day),
                out prepared, out string reason))
        { failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "production-completion-fact-prepare-failed", reason); return false; }
        failure = DomainFailure.None; return true;
    }

    private bool TryRequireCompletedObservation(ProductionBillRecord record, out DomainFailure failure)
    {
#if UNITY_EDITOR
        // Existing detached physical-only fixtures do not prove completion observation.
        if (observations == null) { failure = DomainFailure.None; return true; }
#endif
        if (observations == null)
        { failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "production-completion-observer-not-connected"); return false; }
        record.BeginCompletionPublication();
        try
        {
            if (observations.TryComplete(record, out string reason))
            { failure = DomainFailure.None; return true; }
            failure = ProductionPreparedOutputResolutionPlanner.Fail(record, "production-completion-awaiting-acknowledgement", reason); return false;
        }
        finally { record.EndCompletionPublication(); }
    }

    private static FacilityBufferPlannedOutputPublicationReceipt
        RestorePublicationReceipt(
            ProductionPreparedOutputBatchSaveData batch,
            FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
            ProductionFacilityHandle facility)
    {
        FacilityBufferPublishedOutputStackReceipt[] stacks = restored.Stacks
            .Select(value => new FacilityBufferPublishedOutputStackReceipt(
                value.StackId,
                value.OutputLineId,
                (ItemDefinitionId)value.ItemId,
                value.Quantity,
                new PhysicalMassGrams(value.MassGrams),
                value.ItemInstanceId))
            .ToArray();
        Vector2Int position = restored.Stacks.Count == 0
            ? facility.Position
            : restored.Stacks[0].Position;
        return new FacilityBufferPlannedOutputPublicationReceipt(
            string.Empty,
            restored.BatchCommitId,
            restored.OutcomeFingerprint,
            batch.destinationId,
            position,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            batch.destinationId,
            facility.InstanceId.Value,
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision,
            restored.PlannedOutputFingerprint,
            stacks);
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
            ProductionBillIdentity.OutputDestinationPrefix + facility.InstanceId.Value,
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

    private static ProductionPreparedOutputExecutionResult Block(
        ProductionBillRecord record,
        ProductionPreparedOutputPhase phase,
        DomainFailure failure) => ProductionPreparedOutputExecutionResult.Blocked(
        phase == ProductionPreparedOutputPhase.Completed
            ? ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending
            : phase,
        failure.IsFailure ? failure : ProductionPreparedOutputResolutionPlanner.Fail(record, "prepared-output-failure-missing"));

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
                : ProductionPreparedOutputResolutionPlanner.Fail(record, "ruined-output-failure-missing"));
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

    private void ResetRestoreCandidate()
    {
        previousLiveByBatch = null;
        restoreCandidateActive = false;
        restoreCandidatePublished = false;
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
