using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class ProductionBillExecutionRuntime
{
    private const float SafeUtilityOutageHours = 6f;
    private const float DangerousTemperatureGraceHours = 3f;
    private readonly IProductionAssemblyBridge items;
    private readonly IProductionAssemblyBridge workforceReplanService;
    private readonly IProductionAssemblyBridge workshops;
    private readonly IProductionOutputPlanningService outputPlanning;
    private readonly IProductionOutputExecutionService outputExecution;
    private readonly IProductionPreparedOutputExecutionPort preparedOutputExecution;
    private readonly IProductionRuinedBatchExecutionPort ruinedBatchExecution;
    private readonly IProductionRecipeExecutionReceiptAuthority
        recipeExecutionReceipts;
    private readonly IProductionAssemblyBridge cycleUtilities;
    private readonly IProductionAssemblyBridge inputLogistics;
    private readonly IProductionStockSensorRuntime stockSensors;
    private readonly ProductionAggregateStateStore stateStore;
    private readonly IProductionAssemblyBridge buildingWorld;
    private readonly IProductionFacilityMutationEpochQuery facilityMutationEpoch;
    private readonly IGameClock clock;
    private readonly IRecipeBalanceWorkCalculator balanceWorkCalculator;
    private readonly IGameCalendar calendar;
    private int lastOutputAuthorityBuildingVersion = int.MinValue;
    private readonly ProductionBillRuntimeQuery billQueries;
    private readonly ProductionBillInputRuntime inputRuntime;
    private readonly ProductionBillCommandRuntime commandRuntime;
    private readonly ProductionBillTerminalRuntime terminalRuntime;
    private IReadOnlyList<ProductionBillRecord> bills => stateStore.Bills;

    internal ProductionBillExecutionRuntime(
        ProductionBillOrderDependencies order,
        ProductionBillExecutionDependencies execution,
        ProductionBillRuntimeQuery billQueries,
        ProductionBillInputRuntime inputRuntime,
        ProductionBillCommandRuntime commandRuntime,
        ProductionBillTerminalRuntime terminalRuntime)
    {
        this.items = order.Bridge;
        this.workforceReplanService = order.Bridge;
        this.inputLogistics = order.Bridge;
        this.stockSensors = order.StockSensors;
        this.stateStore = order.StateStore;
        this.workshops = order.Bridge;
        this.outputPlanning = execution.OutputPlanning;
        this.outputExecution = execution.OutputExecution;
        this.preparedOutputExecution = execution.PreparedOutputExecution;
        this.ruinedBatchExecution = execution.RuinedBatchExecution;
        this.recipeExecutionReceipts = execution.RecipeExecutionReceipts;
        this.cycleUtilities = execution.Bridge;
        this.buildingWorld = execution.Bridge;
        this.facilityMutationEpoch = order.FacilityMutationEpoch;
        this.clock = execution.Clock;
        this.balanceWorkCalculator = order.BalanceWorkCalculator;
        this.calendar = order.Calendar;
        this.billQueries = billQueries;
        this.inputRuntime = inputRuntime;
        this.commandRuntime = commandRuntime;
        this.terminalRuntime = terminalRuntime;
    }

    // The facade restores output authorities immediately before the root swap.
    internal void NotifyOutputAuthoritiesRestored(int buildingVersion) =>
        lastOutputAuthorityBuildingVersion = buildingVersion;

    private DomainFailure CheckQualityTarget(ProductionBillRecord record, ProductionRecipeSO recipe,
        ProductionFacilityHandle facility, bool automatic)
    {
        if (record.minimumCraftQuality < 0) return DomainFailure.None;
        // Processing owns no worker tick; the stage transition validates again before finishing.
        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && record.batchStage == ProductionBatchStage.Processing) return DomainFailure.None;
        if (record.outputOutcomeResolved)
        {
            if (items is not IProductionCraftQualityProjectionBridge quality)
                return ProductionQualityTargetRules.Check(record.minimumCraftQuality, -1);
            foreach (var output in record.resolvedOutputs)
            {
                var descriptor = items.CaptureOutputCapability(output.outputLineId, output.itemId);
                if (quality.TryResolveCraftQualityTier(descriptor, output.qualityModifier, 100f, out int tier)
                    && tier < record.minimumCraftQuality)
                    return ProductionQualityTargetRules.Check(record.minimumCraftQuality, tier);
            }
            return DomainFailure.None; // Frozen output remains authoritative, including a zero-output outcome.
        }
        float completed = record.completedWork;
        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && record.batchStage == ProductionBatchStage.Finishing)
            completed += ResolveCycleRequiredWork(recipe) - ResolveCurrentRequiredWork(record, recipe);
        float ceiling = automatic ? facility.AutomaticQualityScoreCeiling
            : ProductionAutomaticQualityRules.ResolveScoreCeiling(facility.AutomaticQualityScoreCeiling,
                completed, record.workerContributions);
        return ProductionQualityTargetRules.Check(record.minimumCraftQuality,
            ProductionQualityTargetRules.CaptureTier(recipe, facility, items, outputPlanning, ceiling));
    }

    internal ProductionWorkAvailabilityResult CheckWorkAvailability(
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId)
    {
        ProductionBillRecord record = FindRunnableBill(
            facility,
            workTypeId,
            requireDeliveredInputs: true,
            out DomainFailure failure);
        if (record != null)
        {
            ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
            string utilityFailure = recipe == null
                ? "production-recipe-missing"
                : string.Empty;
            bool finishingPassiveBatch = recipe != null
                && recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                && record.batchStage == ProductionBatchStage.Finishing;
            bool utilitiesValid = recipe != null
                && (finishingPassiveBatch
                    ? cycleUtilities.ValidateProcessingUtilities(
                        record.occupiedSupportNodeId,
                        recipe,
                        facility,
                        out utilityFailure)
                    : cycleUtilities.ValidateCycleRequirements(
                        record,
                        recipe,
                        facility,
                        bills,
                        out utilityFailure));
            if (recipe == null
                || !utilitiesValid)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable,
                    utilityFailure ?? "production-cycle-utility-missing");
                record = null;
            }
        }
        return new ProductionWorkAvailabilityResult(
            record != null,
            failure,
            record != null ? billQueries.ToSnapshot(record, facility) : null);
    }

    internal ProductionWorkBeginResult BeginWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId)
    {
        ProductionBillRecord record = FindRunnableBill(
            facility,
            workTypeId,
            requireDeliveredInputs: true,
            out DomainFailure failure,
            automatic: string.IsNullOrEmpty(worker?.PersistentId));
        if (record == null)
        {
            return new ProductionWorkBeginResult(null, failure);
        }
        if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
        {
            DomainFailure frozen = new(
                FailureCode.ProductionBillUnavailable,
                record.buildingInstanceId.Value,
                "facility-mutation-frozen");
            return new ProductionWorkBeginResult(null, frozen);
        }

        if (!workshops.IsWorkerEligible(
                worker,
                record.workerPolicy,
                out string workerFailure))
        {
            DomainFailure ineligible = new(
                FailureCode.WorkOrderWorkerIneligible,
                workerFailure ?? string.Empty);
            record.SetBlockedFailure(ineligible);
            record.SetReservedWorker(string.Empty);
            return new ProductionWorkBeginResult(null, ineligible);
        }

        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        if (record.materialsConsumed
            && !string.IsNullOrEmpty(record.wipInputCommitId)
            && !items.AcknowledgeWipInput(
                record.wipInputCommitId,
                out string restoredAcknowledgeFailure))
        {
            throw new InvalidOperationException(
                $"Restored production WIP input '{record.wipInputCommitId}' could not be acknowledged: {restoredAcknowledgeFailure}");
        }
        ProductionPreparedOutputPhase preparedPhase = record.preparedOutput?.phase
            ?? ProductionPreparedOutputPhase.Unresolved;
        bool usesPreparedOutput = billQueries.UsesPreparedOutput(recipe);
        if (usesPreparedOutput
            && !record.materialsConsumed
            && preparedPhase != ProductionPreparedOutputPhase.Unresolved)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                record.billId.Value,
                "prepared-output-cycle-start-phase-conflict");
            record.SetBlockedFailure(failure);
            commandRuntime.Touch(default, requestWorker: false);
            return new ProductionWorkBeginResult(null, failure);
        }
        bool requiresCycleStartCapacity = usesPreparedOutput
            ? ProductionPreparedOutputMigrationScope
                .RequiresCycleStartCapacity(record)
            : !record.materialsConsumed;
        if (requiresCycleStartCapacity
            && !TryValidateCycleStart(
                record,
                recipe,
                facility,
                out DomainFailure cycleFailure))
        {
            failure = cycleFailure.IsFailure
                ? cycleFailure
                : new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    record.billId.Value,
                    "cycle-start-invalid-result");
            record.SetBlockedFailure(failure);
            commandRuntime.Touch(default, requestWorker: false);
            return new ProductionWorkBeginResult(null, failure);
        }

        IReadOnlyDictionary<string, int> cycleInputs =
            inputRuntime.ToCycleInputMap(record, recipe, facility);
        ProductionWipInputReceipt wipReceipt = default;
        if (!record.materialsConsumed
            && cycleInputs.Count > 0
            && !items.ConsumeDeliveredToWip(
                record.materialDestinationId,
                cycleInputs,
                $"production-wip:{record.billId.Value}:{record.cycleSequence:D8}",
                out wipReceipt,
                out _))
        {
            inputRuntime.RequestMissingInputs(record, recipe, facility);
            return new ProductionWorkBeginResult(
                null,
                new DomainFailure(FailureCode.ProductionMaterialsMissing));
        }

        if (!record.materialsConsumed && cycleInputs.Count > 0)
        {
            record.SetWipInput(wipReceipt);
            record.SetMaterialsConsumed(true);
            if (!items.AcknowledgeWipInput(
                    wipReceipt.CommitId,
                    out string acknowledgeFailure))
            {
                throw new InvalidOperationException(
                    $"Production WIP input '{wipReceipt.CommitId}' was recorded but could not be acknowledged: {acknowledgeFailure}");
            }
        }
        else
        {
            record.SetMaterialsConsumed(true);
        }
        if (record.processFluidConsumed
            && record.processManualWaterTransfers.Count > 0
            && !cycleUtilities.AcknowledgeCycleUtilities(
                record.CaptureProcessFluidReceipt(),
                out string replayAcknowledgeFailure))
        {
            failure = new DomainFailure(
                FailureCode.ProductionUtilitiesUnavailable,
                replayAcknowledgeFailure);
            record.SetBlockedFailure(failure);
            return new ProductionWorkBeginResult(null, failure);
        }
        if (!record.processFluidConsumed)
        {
            if (!TryConsumeCycleUtilities(
                    record,
                    recipe,
                    facility,
                    out ProductionProcessFluidReceipt fluidReceipt,
                    out string utilityFailure))
            {
                failure = new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable,
                    utilityFailure);
                record.SetBlockedFailure(failure);
                return new ProductionWorkBeginResult(null, failure);
            }
            record.SetProcessFluid(fluidReceipt);
            record.SetProcessFluidConsumed(true);
            if (!cycleUtilities.AcknowledgeCycleUtilities(
                    fluidReceipt,
                    out string fluidAcknowledgeFailure))
            {
                throw new InvalidOperationException(
                    $"Production process-fluid receipt for '{record.billId}' could not be acknowledged: {fluidAcknowledgeFailure}");
            }
        }

        record.SetBlockedFailure(DomainFailure.None);
        record.SetReservedWorker(worker?.PersistentId);
        inputRuntime.RecalculatePrefetch(record, recipe, worker);
        inputRuntime.RequestMissingInputs(record, recipe, facility);
        commandRuntime.Touch(default, requestWorker: false);
        return new ProductionWorkBeginResult(
            billQueries.ToSnapshot(record, facility),
            DomainFailure.None);
    }

    internal ProductionWorkExecutionResult ExecuteWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        ProductionBillId billId,
        float amount)
    {
        commandRuntime.AssertCommandObservationIdle();
        ProductionBillRecord record = billQueries.Find(billId);
        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        if (record == null
            || recipe == null
            || !ProductionBillRuntimeQuery.MatchesFacility(record, facility)
            || record.suspended
            || !record.materialsConsumed)
        {
            return FailedExecution(FailureCode.ProductionBillUnavailable);
        }
        DomainFailure qualityFailure = CheckQualityTarget(record, recipe, facility,
            string.IsNullOrEmpty(worker?.PersistentId));
        if (qualityFailure.IsFailure)
        {
            record.SetBlockedFailure(qualityFailure);
            return new ProductionWorkExecutionResult(false, false, ProductionBillOutcomeCode.None, qualityFailure);
        }
        if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
        {
            return FailedExecution(
                FailureCode.ProductionBillUnavailable,
                record.buildingInstanceId.Value,
                "facility-mutation-frozen");
        }

        string workerId = worker?.PersistentId ?? string.Empty;
        if (!workshops.IsWorkerEligible(
                worker,
                record.workerPolicy,
                out string workerFailure))
        {
            record.SetReservedWorker(string.Empty);
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.WorkOrderWorkerIneligible,
                workerFailure ?? string.Empty));
            return FailedExecution(
                FailureCode.WorkOrderWorkerIneligible,
                workerFailure ?? string.Empty);
        }
        if (!string.IsNullOrWhiteSpace(record.reservedWorkerId)
            && !string.Equals(record.reservedWorkerId, workerId, StringComparison.Ordinal))
        {
            return FailedExecution(
                FailureCode.ProductionBillReservedByOtherWorker,
                record.reservedWorkerId);
        }

        record.SetReservedWorker(workerId);
        float requiredWork = ResolveCurrentRequiredWork(record, recipe);
        float supportWorkMultiplier =
            outputExecution.ResolveWorkSpeedMultiplier(facility, recipe);
        float acceptedWork = Mathf.Min(
            Mathf.Max(0f, amount) * supportWorkMultiplier,
            Mathf.Max(0f, requiredWork - record.completedWork));
        record.SetCompletedWork(Mathf.Clamp(
            record.completedWork
                + acceptedWork,
            0f,
            requiredWork));
        CraftContributionAccumulator contributions =
            new(record.workerContributions);
        contributions.Add(
            workerId,
            acceptedWork,
            workshops.GetRelevantCraftSkill(worker, recipe));
        record.ReplaceWorkerContributions(contributions.Capture());
        if (record.completedWork + 0.001f < requiredWork)
        {
            return SuccessfulExecution(
                cycleCompleted: false,
                outcome: ProductionBillOutcomeCode.WorkProgressed);
        }

        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && record.batchStage == ProductionBatchStage.Preparing)
        {
            if (!TryOccupyBatchSupport(
                    record,
                    recipe,
                    facility,
                    out string supportFailure))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionSupportUnavailable));
                record.SetReservedWorker(string.Empty);
                return new ProductionWorkExecutionResult(
                    false,
                    false,
                    ProductionBillOutcomeCode.None,
                    record.blockedFailure);
            }

            record.SetBatchStage(ProductionBatchStage.Processing);
            record.SetRemainingProcessingHours(recipe.ProcessingGameHours);
            record.SetCompletedWork(0f);
            record.SetReservedWorker(string.Empty);
            record.SetBlockedFailure(DomainFailure.None);
            commandRuntime.Touch(recipe.WorkTypeId, requestWorker: false);
            return SuccessfulExecution(
                cycleCompleted: false,
                outcome: ProductionBillOutcomeCode.ProcessingStarted);
        }

        bool usesPreparedOutput = billQueries.UsesPreparedOutput(recipe);
        if (usesPreparedOutput)
        {
            if (ProductionBillRuntimeQuery.HasLegacyOutputAuthority(record))
            {
                return BlockPreparedOutput(record,
                    ProductionBillRuntimeQuery.PreparedOutputAuthorityConflict(record));
            }
            ProductionPreparedOutputExecutionResult preparedResult =
                preparedOutputExecution.Execute(
                    record,
                    recipe,
                    facility,
                    worker);
            if (!preparedResult.IsValid
                || !preparedResult.CycleOutputCompleted)
            {
                bool phaseMatches = preparedResult.IsValid
                    && record.preparedOutput?.phase == preparedResult.Phase;
                DomainFailure preparedFailure = phaseMatches
                    && preparedResult.Failure.IsFailure
                    ? preparedResult.Failure
                    : new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        preparedResult.IsValid && !phaseMatches
                            ? "prepared-output-result-phase-mismatch"
                            : "prepared-output-incomplete-result");
                return BlockPreparedOutput(record, preparedFailure);
            }
            if (preparedResult.Phase != ProductionPreparedOutputPhase.Completed
                || record.preparedOutput?.phase !=
                    ProductionPreparedOutputPhase.Completed)
            {
                return BlockPreparedOutput(
                    record,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        "prepared-output-completion-state-mismatch"));
            }
            if (!recipeExecutionReceipts.TryPublishCompleted(
                    record.billId,
                    record.cycleSequence,
                    record.recipeId,
                    record.buildingInstanceId,
                    worker?.AuthorityKind == ProductionWorkerAuthorityKind.Actor
                        ? workerId
                        : string.Empty,
                    record.wipInputCommitId,
                    record.wipInputQuantity,
                    record.wipInputMassGrams,
                    record.preparedOutput,
                    out string receiptFailure))
            {
                return BlockPreparedOutput(
                    record,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        string.IsNullOrEmpty(receiptFailure)
                            ? "recipe-execution-receipt-publication-failed"
                            : receiptFailure));
            }
            record.ClearCompletedPreparedOutput();
        }
        else
        {
            if (!recipeExecutionReceipts.TryEnsureExactCapture(
                    record.billId,
                    record.cycleSequence,
                    record.recipeId,
                    record.buildingInstanceId,
                    out string ensureExactFailure))
            {
                record.SetReservedWorker(string.Empty);
                return new ProductionWorkExecutionResult(
                    false,
                    false,
                    ProductionBillOutcomeCode.None,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        string.IsNullOrEmpty(ensureExactFailure)
                            ? "recipe-exact-capture-reservation-failed"
                            : ensureExactFailure));
            }
            if (!record.outputOutcomeResolved)
            {
                record.SetResolvedOutputs(outputExecution.ResolveAll(
                    recipe,
                    facility,
                    worker,
                    record.batchIntegrity,
                    ProductionAutomaticQualityRules.ResolveScoreCeiling(
                        facility.AutomaticQualityScoreCeiling,
                        ResolveCycleRequiredWork(recipe),
                        record.workerContributions)));
            }
            foreach (ProductionResolvedOutputSaveData output in record.resolvedOutputs)
            {
                while (output.committedAmount < output.amount)
                {
                    if (string.IsNullOrEmpty(output.pendingCommitId))
                    {
                        record.BeginResolvedOutputUnit(
                            output.outputLineId,
                            ProductionOutputCommitIdentity.Format(
                                record.billId,
                                record.cycleSequence,
                                output.outputLineId,
                                output.itemId,
                                output.committedAmount));
                    }
                    string commitId = output.pendingCommitId;
                    bool captureExact = recipeExecutionReceipts
                        .RequiresExactCapture(
                            record.billId,
                            record.cycleSequence);
                    ProductionCommittedOutputSnapshot diagnosticUnit = null;
                    if (!output.pendingCommitApplied)
                    {
                        DomainFailure outputFailure = outputExecution.ProduceOne(
                            recipe,
                            facility,
                            worker,
                            output,
                            record.outputDestinationId,
                            commitId,
                            out diagnosticUnit,
                            out ProductionOutputPublicationExposure
                                publicationExposure);
                        if (outputFailure.IsFailure)
                        {
                            if (publicationExposure ==
                                ProductionOutputPublicationExposure.None)
                            {
                                record.AbortUnpublishedResolvedOutputUnit(
                                    output.outputLineId,
                                    commitId);
                            }
                            record.SetReservedWorker(string.Empty);
                            return new ProductionWorkExecutionResult(
                                false,
                                false,
                                ProductionBillOutcomeCode.None,
                                outputFailure);
                        }
                        record.MarkResolvedOutputUnitCommitted(
                            output.outputLineId,
                            commitId,
                            diagnosticUnit);
                    }
                    else if (captureExact)
                    {
                        diagnosticUnit = output.pendingOutputPublication
                            ?.ToRuntimeSnapshot();
                    }
                    if (captureExact
                        && !recipeExecutionReceipts.TryCaptureExactCommittedUnit(
                            record.billId,
                            record.cycleSequence,
                            record.recipeId,
                            record.buildingInstanceId,
                            output,
                            diagnosticUnit,
                            out string unitReceiptFailure))
                    {
                        record.SetReservedWorker(string.Empty);
                        return new ProductionWorkExecutionResult(
                            false,
                            false,
                            ProductionBillOutcomeCode.None,
                            new DomainFailure(
                                FailureCode.ProductionOutputUnavailable,
                                record.billId.Value,
                                string.IsNullOrEmpty(unitReceiptFailure)
                                    ? "recipe-exact-unit-receipt-failed"
                                    : unitReceiptFailure));
                    }
                    DomainFailure acknowledgeFailure = outputExecution.AcknowledgeOne(
                        output,
                        commitId);
                    if (acknowledgeFailure.IsFailure)
                    {
                        record.SetReservedWorker(string.Empty);
                        return new ProductionWorkExecutionResult(
                            false,
                            false,
                            ProductionBillOutcomeCode.None,
                            acknowledgeFailure);
                    }
                    record.ClearResolvedOutputPendingCommit(
                        output.outputLineId,
                        commitId);
                }
            }

            record.ClearOutputReservations();
            if (record.CompletionObservation == null && recipeExecutionReceipts.RequiresExactCapture(
                    record.billId,
                    record.cycleSequence)
                && !recipeExecutionReceipts.TryFinalizeExactCompleted(
                    record.billId,
                    record.cycleSequence,
                    record.recipeId,
                    record.buildingInstanceId,
                    record.wipInputCommitId,
                    record.wipInputQuantity,
                    record.wipInputMassGrams,
                    record.resolvedOutputs,
                    out string exactReceiptFailure))
            {
                record.SetReservedWorker(string.Empty);
                return new ProductionWorkExecutionResult(
                    false,
                    false,
                    ProductionBillOutcomeCode.None,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        string.IsNullOrEmpty(exactReceiptFailure)
                            ? "recipe-exact-cycle-receipt-failed"
                            : exactReceiptFailure));
            }
            if (!recipeExecutionReceipts.TryCommitExactCompletedOutcome(
                    record.billId,
                    record.cycleSequence,
                    record.recipeId,
                    record.buildingInstanceId,
                    worker?.AuthorityKind == ProductionWorkerAuthorityKind.Actor
                        ? workerId
                        : string.Empty,
                    record.resolvedOutputs,
                    billQueries.ResolveFacility(record)?.DisplayName ?? string.Empty,
                    worker?.AuthorityKind == ProductionWorkerAuthorityKind.Actor ? worker.DisplayName : string.Empty,
                    calendar?.Day ?? 0,
                    out string gameplayOutcomeFailure))
            {
                record.SetReservedWorker(string.Empty);
                return new ProductionWorkExecutionResult(
                    false,
                    false,
                    ProductionBillOutcomeCode.None,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        string.IsNullOrEmpty(gameplayOutcomeFailure)
                            ? "production-gameplay-outcome-commit-failed"
                            : gameplayOutcomeFailure));
            }
            record.ClearResolvedOutputs();
        }

        record.ClearSelectedSupplies();
        record.SetCompletedWork(0f);
        record.SetMaterialsConsumed(false);
        record.ClearWipInput();
        record.AdvanceCycleSequence();
        record.ClearProcessFluid();
        record.SetBatchStage(recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            ? ProductionBatchStage.Preparing
            : ProductionBatchStage.None);
        record.SetRemainingProcessingHours(0f);
        record.SetBatchIntegrity(100f);
        record.SetUtilityOutageHours(0f);
        record.SetTemperatureOutageHours(0f);
        record.SetOccupiedSupportNode(string.Empty);
        record.SetBlockedFailure(DomainFailure.None);
        record.SetReservedWorker(string.Empty);
        record.ReplaceWorkerContributions(
            Array.Empty<CraftContributionSaveData>());
        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            record.SetRepeatCount(record.remainingCycles - 1);
        }
        ApplyPendingModeTransition(record, facility);

        bool finished = !inputRuntime.ShouldRunAnotherCycle(record, recipe);
        if (finished && record.mode == ProductionOrderMode.RepeatCount)
        {
            terminalRuntime.TryRetireFinishedRepeatCountBill(record, facility);
        }
        else
        {
            inputRuntime.RequestMissingInputs(record, recipe, facility);
        }

        commandRuntime.Touch(recipe.WorkTypeId, requestWorker: !finished);
        workforceReplanService.RequestOneHaulerToReplan(forceInterrupt: false);
        return SuccessfulExecution(
            cycleCompleted: true,
            outcome: ProductionBillOutcomeCode.CycleCompleted);
    }

    private static ProductionWorkExecutionResult BlockPreparedOutput(
        ProductionBillRecord record,
        DomainFailure failure)
    {
        DomainFailure exact = failure.IsFailure
            ? failure
            : new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                record == null ? string.Empty : record.billId.Value,
                "prepared-output-unspecified-failure");
        if (record != null)
        {
            record.SetReservedWorker(string.Empty);
            record.SetBlockedFailure(exact);
        }
        return new ProductionWorkExecutionResult(
            false,
            false,
            ProductionBillOutcomeCode.None,
            exact);
    }

    private static ProductionWorkExecutionResult SuccessfulExecution(
        bool cycleCompleted,
        ProductionBillOutcomeCode outcome)
    {
        return new ProductionWorkExecutionResult(
            true,
            cycleCompleted,
            outcome,
            DomainFailure.None);
    }

    private static ProductionWorkExecutionResult FailedExecution(
        FailureCode code,
        params string[] parameters)
    {
        return new ProductionWorkExecutionResult(
            false,
            false,
            ProductionBillOutcomeCode.None,
            new DomainFailure(code, parameters));
    }

    internal void Tick()
    {
        commandRuntime.AssertCommandObservationIdle();
        commandRuntime.FlushPendingCommandObservations();
        ReconcileOutputDestinationAuthorities();
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        commandRuntime.FinalizeDeliveredStockSensors();
        terminalRuntime.ReconcileFinishedOutputBills();
        terminalRuntime.ReconcileMissingFacilities();
        float elapsedHours = clock.DeltaTime
            / GameSimulationTimeRules.SecondsPerGameHour;
        foreach (ProductionBillRecord record in bills.ToArray())
        {
            ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
            if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
            {
                continue;
            }
            if (recipe == null
                || recipe.ProcessKind != ProductionProcessKind.PassiveBatch
                || record.batchStage != ProductionBatchStage.Processing)
            {
                continue;
            }

            ProductionFacilityHandle facility = billQueries.ResolveFacility(record);
            if (facility == null)
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionWorkstationMissing,
                    record.buildingInstanceId.Value));
                continue;
            }

            if (!TryValidateProcessingUtilities(
                    record,
                    recipe,
                    facility,
                    out string utilityFailure))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable));
                record.SetUtilityOutageHours(ApplyOutageDecay(
                    record.utilityOutageHours,
                    elapsedHours,
                    SafeUtilityOutageHours,
                    5f,
                    record));

                TryConvertRuinedBatch(record, recipe, facility);
                continue;
            }

            record.SetUtilityOutageHours(0f);
            ProductionFacilityHandle temperatureTarget =
                ResolveOccupiedBatchSupport(record, facility) ?? facility;
            float temperatureSpeed = ResolveTemperatureSpeed(
                recipe,
                temperatureTarget,
                out bool dangerous);
            if (dangerous)
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable,
                    "temperature-dangerous"));
                record.SetTemperatureOutageHours(ApplyOutageDecay(
                    record.temperatureOutageHours,
                    elapsedHours,
                    DangerousTemperatureGraceHours,
                    5f,
                    record));

                TryConvertRuinedBatch(record, recipe, facility);
                continue;
            }

            record.SetTemperatureOutageHours(0f);
            record.SetBlockedFailure(temperatureSpeed < 1f
                ? new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable,
                    "temperature-slow")
                : DomainFailure.None);
            if (temperatureSpeed < 1f)
            {
                record.SetBatchIntegrity(Mathf.Max(
                    0f,
                    record.batchIntegrity - elapsedHours));
            }

            record.SetRemainingProcessingHours(Mathf.Max(
                0f,
                record.remainingProcessingHours
                    - elapsedHours * temperatureSpeed));
            if (TryConvertRuinedBatch(record, recipe, facility)
                || record.remainingProcessingHours > 0.001f)
            {
                continue;
            }

            record.SetBatchStage(ProductionBatchStage.Finishing);
            record.SetCompletedWork(0f);
            record.SetReservedWorker(string.Empty);
            record.SetBlockedFailure(DomainFailure.None);
            if (recipe.FinishingWork > 0f)
            {
                commandRuntime.Touch(recipe.WorkTypeId, requestWorker: true);
            }
            else
            {
                ExecuteWork(
                    ProductionWorkerHandle.PassiveProcessor,
                    facility,
                    record.billId,
                    0f);
            }
        }
    }

    private void ReconcileOutputDestinationAuthorities()
    {
        int currentVersion = buildingWorld.BuildingVersion;
        if (currentVersion == lastOutputAuthorityBuildingVersion)
            return;
        preparedOutputExecution.RestoreDestinationAuthorities(
            bills,
            buildingWorld.Facilities);
        if (!stockSensors.TryReconcileDestinationAuthorities(
                out string sensorFailure))
        {
            throw new InvalidOperationException(
                "Production stock-sensor destination authorities could not be reconciled: "
                + sensorFailure);
        }
        lastOutputAuthorityBuildingVersion = currentVersion;
    }

    private bool TryValidateCycleStart(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (billQueries.UsesPreparedOutput(recipe))
        {
            if (ProductionBillRuntimeQuery.HasLegacyOutputAuthority(record))
            {
                failure = ProductionBillRuntimeQuery.PreparedOutputAuthorityConflict(record);
                return false;
            }
            ProductionPreparedOutputCapacityResult capacity =
                preparedOutputExecution.AssessCycleStart(
                    record,
                    recipe,
                    facility);
            if (!capacity.IsValid)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    record.billId.Value,
                    "prepared-output-capacity-invalid-result");
                return false;
            }
            if (!capacity.CanBeginCycle)
            {
                failure = capacity.Failure.IsFailure
                    ? capacity.Failure
                    : new DomainFailure(
                        FailureCode.ProductionOutputSpaceUnavailable,
                        record.outputDestinationId);
                return false;
            }
        }
        else if (!inputRuntime.EnsureOutputReservation(
                     record,
                     recipe,
                     facility,
                     out string outputFailureReason))
        {
            failure = new DomainFailure(
                FailureCode.ProductionUtilitiesUnavailable,
                outputFailureReason);
            return false;
        }

        if (cycleUtilities.ValidateCycleRequirements(
                record,
                recipe,
                facility,
                bills,
                out string utilityFailureReason))
        {
            return true;
        }
        failure = new DomainFailure(
            FailureCode.ProductionUtilitiesUnavailable,
            utilityFailureReason);
        return false;
    }

    private bool TryValidateProcessingUtilities(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason)
    {
        return cycleUtilities.ValidateProcessingUtilities(
            record?.occupiedSupportNodeId ?? string.Empty,
            recipe,
            facility,
            out failureReason);
    }

    private bool TryConsumeCycleUtilities(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out ProductionProcessFluidReceipt receipt,
        out string failureReason)
    {
        return cycleUtilities.TryConsumeCycleUtilities(
            record,
            recipe,
            facility,
            out receipt,
            out failureReason);
    }

    private bool TryOccupyBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason)
    {
        if (!cycleUtilities.TryResolveBatchSupport(
            record,
            recipe,
            facility,
            bills,
            out string supportNodeId,
            out failureReason))
        {
            return false;
        }

        record.SetOccupiedSupportNode(supportNodeId);
        return true;
    }

    private float ResolveTemperatureSpeed(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out bool dangerous)
    {
        return cycleUtilities.ResolveTemperatureSpeed(
            recipe,
            facility,
            out dangerous);
    }

    private ProductionFacilityHandle ResolveOccupiedBatchSupport(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        return cycleUtilities.ResolveOccupiedBatchSupport(
            record?.occupiedSupportNodeId ?? string.Empty,
            facility);
    }

    private bool TryConvertRuinedBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        if (record.batchIntegrity > 0f)
        {
            return false;
        }

        // Ruined output follows the same capability decision as successful
        // output. A passive definition-only recipe added later therefore uses
        // the conservative prepared-output disposition without a recipe-ID
        // branch; stateful/custom output remains on its declared owner.
        bool usesPreparedRuinedBatch = billQueries.UsesPreparedOutput(recipe);
        if (!usesPreparedRuinedBatch)
        {
            // No implicit Loose fallback is allowed: it has no gram admission,
            // no exact disposition receipt and can delete or create mass. A
            // stateful/custom passive recipe must declare its own ruined-batch
            // capability before this WIP can be terminally disposed.
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                record.billId.Value,
                "ruined-output-capability-unsupported"));
            record.SetReservedWorker(string.Empty);
            commandRuntime.Touch(recipe.WorkTypeId, requestWorker: false);
            return true;
        }
        else
        {
            ProductionRuinedBatchExecutionResult ruinedResult =
                ruinedBatchExecution.ExecuteRuinedBatch(
                    record,
                    recipe,
                    facility);
            if (!ruinedResult.IsValid
                || !ruinedResult.BatchDispositionCompleted)
            {
                bool phaseMatches = ruinedResult.IsValid
                    && record.preparedOutput?.phase == ruinedResult.Phase;
                record.SetBlockedFailure(phaseMatches
                    && ruinedResult.Failure.IsFailure
                        ? ruinedResult.Failure
                        : new DomainFailure(
                            FailureCode.ProductionOutputUnavailable,
                            record.billId.Value,
                            ruinedResult.IsValid && !phaseMatches
                                ? "ruined-output-result-phase-mismatch"
                                : "ruined-output-incomplete-result"));
                commandRuntime.Touch(recipe.WorkTypeId, requestWorker: false);
                return true;
            }
            if (!IsCompletedRuinedBatchDispositionValid(
                    record,
                    recipe,
                    ruinedResult))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    record.billId.Value,
                    "ruined-output-completion-state-mismatch"));
                commandRuntime.Touch(recipe.WorkTypeId, requestWorker: false);
                return true;
            }

            record.ClearCompletedPreparedOutput();
        }

        record.ClearSelectedSupplies();
        record.SetCompletedWork(0f);
        record.SetMaterialsConsumed(false);
        record.ClearWipInput();
        record.AdvanceCycleSequence();
        record.ClearProcessFluid();
        record.SetBatchStage(ProductionBatchStage.Preparing);
        record.SetRemainingProcessingHours(0f);
        record.SetBatchIntegrity(100f);
        record.SetUtilityOutageHours(0f);
        record.SetTemperatureOutageHours(0f);
        record.SetOccupiedSupportNode(string.Empty);
        record.SetReservedWorker(string.Empty);
        record.SetBlockedFailure(new DomainFailure(
            FailureCode.ProductionBatchRuined,
            recipe.SpoilageItemId));
        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            record.SetRepeatCount(record.remainingCycles - 1);
        }

        if (!inputRuntime.ShouldRunAnotherCycle(record, recipe)
            && record.mode == ProductionOrderMode.RepeatCount)
        {
            terminalRuntime.TryRetireFinishedRepeatCountBill(record, facility);
        }
        else
        {
            inputRuntime.RequestMissingInputs(record, recipe, facility);
        }

        commandRuntime.Touch(recipe.WorkTypeId, requestWorker: false);
        return true;
    }

    private static bool IsCompletedRuinedBatchDispositionValid(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionRuinedBatchExecutionResult result)
    {
        if (record == null
            || recipe == null
            || result.Phase != ProductionPreparedOutputPhase.Completed
            || record.preparedOutput?.phase != ProductionPreparedOutputPhase.Completed)
        {
            return false;
        }

        ProductionRuinedBatchDispositionPlan disposition = result.Disposition;
        long availableMassGrams;
        long preparedWasteMassGrams;
        long preparedLossMassGrams;
        int preparedWasteQuantity;
        int preparedWasteLineCount;
        int preparedLossLineCount;
        try
        {
            availableMassGrams = checked(
                record.wipInputMassGrams
                + record.processCleanWaterMassGrams);
            preparedWasteMassGrams = record.preparedOutput.lines
                .Where(line => line != null
                    && line.role == ProductionOutputRole.RecoverableWaste
                    && string.Equals(
                        line.outputLineId,
                        ProductionRuinedBatchDispositionPlan
                            .RecoverableWasteOutputLineId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        line.itemId,
                        recipe.SpoilageItemId,
                        StringComparison.Ordinal))
                .Aggregate(0L, (total, line) => checked(
                    total + line.exactMassGrams));
            preparedWasteQuantity = record.preparedOutput.lines
                .Where(line => line != null
                    && line.role == ProductionOutputRole.RecoverableWaste
                    && string.Equals(
                        line.outputLineId,
                        ProductionRuinedBatchDispositionPlan
                            .RecoverableWasteOutputLineId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        line.itemId,
                        recipe.SpoilageItemId,
                        StringComparison.Ordinal))
                .Aggregate(0, (total, line) => checked(total + line.quantity));
            preparedWasteLineCount = record.preparedOutput.lines.Count(line =>
                line != null
                && line.role == ProductionOutputRole.RecoverableWaste
                && string.Equals(
                    line.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    StringComparison.Ordinal));
            preparedLossMassGrams = record.preparedOutput.lines
                .Where(line => line != null
                    && line.role == ProductionOutputRole.DeclaredLoss
                    && string.Equals(
                        line.outputLineId,
                        ProductionRuinedBatchDispositionPlan
                            .DeclaredLossOutputLineId,
                        StringComparison.Ordinal))
                .Aggregate(0L, (total, line) => checked(
                    total + line.exactMassGrams));
            preparedLossLineCount = record.preparedOutput.lines.Count(line =>
                line != null
                && line.role == ProductionOutputRole.DeclaredLoss
                && string.Equals(
                    line.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .DeclaredLossOutputLineId,
                    StringComparison.Ordinal));
        }
        catch (OverflowException)
        {
            return false;
        }

        return disposition.AvailableMassGrams == availableMassGrams
            && disposition.ProcessWastewaterMassGrams ==
                record.processWastewaterMassGrams
            && string.Equals(
                disposition.SpoilageItemId,
                recipe.SpoilageItemId,
                StringComparison.Ordinal)
            && disposition.RecoverableWasteMassGrams == preparedWasteMassGrams
            && disposition.RecoverableWasteQuantity == preparedWasteQuantity
            && preparedWasteLineCount == 1
            && disposition.DeclaredLossMassGrams == preparedLossMassGrams
            && preparedLossLineCount ==
                (disposition.DeclaredLossMassGrams > 0L ? 1 : 0)
            && record.preparedOutput.totalPhysicalMassGrams ==
                disposition.RecoverableWasteMassGrams
            && record.preparedOutput.totalDeclaredLossMassGrams ==
                disposition.DeclaredLossMassGrams
            && checked(
                disposition.RecoverableWasteMassGrams
                + disposition.ProcessWastewaterMassGrams
                + disposition.DeclaredLossMassGrams)
                == availableMassGrams;
    }

    private float ResolveCurrentRequiredWork(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        float balancedWork = ResolveCycleRequiredWork(recipe);
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch)
        {
            return balancedWork;
        }

        return record.batchStage == ProductionBatchStage.Finishing
            ? (recipe.FinishingWork > 0f ? balancedWork * 0.20f : 0f)
            : (recipe.FinishingWork > 0f ? balancedWork * 0.80f : balancedWork);
    }

    private float ResolveCycleRequiredWork(ProductionRecipeSO recipe) =>
        balanceWorkCalculator?.CalculateRecipe(recipe) ?? recipe.RequiredWork;

    private static float ApplyOutageDecay(
        float accumulatedHours,
        float elapsedHours,
        float graceHours,
        float integrityLossPerHour,
        ProductionBillRecord record)
    {
        float previous = Mathf.Max(0f, accumulatedHours);
        accumulatedHours = previous + Mathf.Max(0f, elapsedHours);
        float damagingHours = Mathf.Max(0f, accumulatedHours - graceHours)
            - Mathf.Max(0f, previous - graceHours);
        if (damagingHours <= 0f)
        {
            return accumulatedHours;
        }

        record.SetBatchIntegrity(Mathf.Max(
            0f,
            record.batchIntegrity - damagingHours * integrityLossPerHour));
        return accumulatedHours;
    }

    private void ApplyPendingModeTransition(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        if (record == null || !record.hasPendingModeTransition)
        {
            return;
        }

        record.SetOrderMode(record.pendingMode);
        record.ClearModeTransition();
        items.ReleaseDestination(
            record.materialDestinationId,
            facility?.Position ?? Vector2Int.zero);
        record.SetPrefetchPlan(
            record.estimatedProductionCycleSeconds,
            1,
            new ProductionLogisticsStatus(
                ProductionBillOutcomeCode.OrderModeTransitionCompleted));
    }

    private ProductionBillRecord FindRunnableBill(
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out DomainFailure failure,
        bool automatic = false)
    {
        DomainFailure qualityFailure = DomainFailure.None;
        var eligible = bills.Where(record =>
        {
            if (!ProductionBillRuntimeQuery.MatchesFacility(record, facility) || record.suspended) return true;
            var recipe = billQueries.ResolveRecipe(record);
            if (recipe == null || recipe.WorkTypeId != workTypeId) return true;
            DomainFailure candidate = CheckQualityTarget(record, recipe, facility, automatic);
            if (!candidate.IsFailure) return true;
            qualityFailure = candidate;
            return false;
        }).ToArray();
        var found = inputLogistics.FindRunnableBill(
            eligible,
            facility,
            workTypeId,
            requireDeliveredInputs,
            out failure);
        if (found == null && qualityFailure.IsFailure) failure = qualityFailure;
        return found;
    }
}
