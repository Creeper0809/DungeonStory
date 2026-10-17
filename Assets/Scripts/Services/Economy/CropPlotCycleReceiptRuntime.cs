using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

internal sealed class CropPlotCycleReceiptRuntime
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly CropPlotMutationRuntime mutation;
    private readonly CropPlotPhysicalInputRuntime physicalInputs;
    private readonly CropPlotWaterRuntime waterRuntime;
    private readonly CropPlotTreatmentRuntime treatmentRuntime;
    private readonly CropPlotCycleSelectionRuntime cycleSelection;
    private readonly CropPlotFactPublication factPublication;

    internal CropPlotCycleReceiptRuntime(IResourceEconomyContentCatalog catalog,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CropPlotMutationRuntime mutation,
        CropPlotPhysicalInputRuntime physicalInputs,
        CropPlotWaterRuntime waterRuntime,
        CropPlotTreatmentRuntime treatmentRuntime,
        CropPlotCycleSelectionRuntime cycleSelection,
        CropPlotFactPublication factPublication)
    {
        this.catalog = catalog;
        this.aggregateRootStore = aggregateRootStore;
        this.mutation = mutation;
        this.physicalInputs = physicalInputs;
        this.waterRuntime = waterRuntime;
        this.treatmentRuntime = treatmentRuntime;
        this.cycleSelection = cycleSelection;
        this.factPublication = factPublication;
    }

    private Dictionary<BuildingInstanceId, CropPlotState> states =>
        aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState()).States;
    private bool snapshotsDirty
    {
        set => aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState()).SnapshotsDirty = value;
    }

    internal bool TryBindNextCycle(
        string correlationId,
        string plotId,
        string cropId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(correlationId)
            || !string.Equals(
                correlationId,
                correlationId.Trim(),
                StringComparison.Ordinal)
            || correlationId.Any(char.IsWhiteSpace)
            || string.IsNullOrWhiteSpace(plotId)
            || !string.Equals(plotId, plotId.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(cropId)
            || !string.Equals(cropId, cropId.Trim(), StringComparison.Ordinal))
        {
            failureReason = "crop-cycle-correlation-invalid";
            return false;
        }
        if (!states.TryGetValue(
                (BuildingInstanceId)plotId,
                out CropPlotState state)
            || state == null
            || !string.IsNullOrEmpty(state.RetirementPublicationKey)
            || state.MaterialsConsumed
            || state.PendingSow.phase != CropPhysicalCommitPhase.None
            || state.WaterRefill.phase != CropWaterRefillPhase.None
            || state.Phase is CropPlotPhase.Sowing
                or CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting
            || !string.Equals(state.CropId, cropId, StringComparison.Ordinal))
        {
            failureReason = "crop-cycle-correlation-state-conflict";
            return false;
        }
        if (!mutation.TryRequireMutable(state.PlotId, out failureReason))
            return false;
        if (state.GoldenHarvestScheduleOutcome is { IsEmpty: false }
            && !cycleSelection.TryAdvanceGoldenHarvestScheduleOutcome(state))
        {
            failureReason = "이전 황금 수확 예약 결과를 원장에 확정하는 중입니다: "
                + state.GoldenHarvestScheduleOutcome.failureReason;
            return false;
        }
        CropPlotState[] existingOwners = states.Values
            .Where(candidate => candidate != null
                && (string.Equals(
                        candidate.PendingCycleCorrelationId,
                        correlationId,
                        StringComparison.Ordinal)
                    || candidate.CycleExecutionReceipt != null
                    && !candidate.CycleExecutionReceipt.IsEmpty
                    && string.Equals(
                        candidate.CycleExecutionReceipt.correlationId,
                        correlationId,
                        StringComparison.Ordinal)))
            .ToArray();
        if (existingOwners.Length > 1
            || existingOwners.Length == 1
            && !ReferenceEquals(existingOwners[0], state))
        {
            failureReason = "crop-cycle-correlation-global-conflict";
            return false;
        }
        if (!string.IsNullOrEmpty(state.PendingCycleCorrelationId)
            && !string.Equals(
                state.PendingCycleCorrelationId,
                correlationId,
                StringComparison.Ordinal))
        {
            failureReason = "crop-cycle-correlation-already-bound";
            return false;
        }
        if (state.CycleExecutionReceipt != null
            && !state.CycleExecutionReceipt.IsEmpty)
        {
            CropPlanExecutionReceiptAuthority.Validate(
                state.CycleExecutionReceipt,
                requireCompleted: false);
            if (state.CycleExecutionReceipt.status
                    == CropCycleExecutionReceiptStatus.Active)
            {
                failureReason = "crop-cycle-execution-receipt-active";
                return false;
            }
            if (IsFailedCycleReceipt(state.CycleExecutionReceipt))
            {
                failureReason = "crop-cycle-failed-outcome-awaiting-acknowledgement";
                return false;
            }
            if (state.CycleExecutionReceipt.explicitCorrelation)
            {
                failureReason = "crop-cycle-execution-receipt-unacknowledged";
                return false;
            }
            state.CycleExecutionReceipt =
                new CropCycleExecutionReceiptSaveData();
        }
        state.PendingCycleCorrelationId = correlationId;
        mutation.MarkChanged(replan: false);
        return true;
    }

    internal bool TryCaptureExecutionReceipt(
        string actionId,
        out CropPlanExecutionReceipt receipt)
    {
        receipt = null;
        if (string.IsNullOrWhiteSpace(actionId)
            || !string.Equals(actionId, actionId.Trim(), StringComparison.Ordinal)
            || actionId.Any(char.IsWhiteSpace))
        {
            return false;
        }

        CropPlotState[] owners = states.Values
            .Where(candidate => candidate?.CycleExecutionReceipt != null
                && !candidate.CycleExecutionReceipt.IsEmpty
                && candidate.CycleExecutionReceipt.status
                    != CropCycleExecutionReceiptStatus.Active
                && string.Equals(
                    candidate.CycleExecutionReceipt.correlationId,
                    actionId,
                    StringComparison.Ordinal))
            .ToArray();
        if (owners.Length > 1)
            throw new InvalidOperationException(
                "Crop execution correlation has multiple durable owners.");
        if (owners.Length == 0)
            return false;

        receipt = new CropPlanExecutionReceipt(
            actionId,
            owners[0].CycleExecutionReceipt);
        return true;
    }

    internal bool TryAcknowledgeExecutionReceipt(
        string correlationId,
        string expectedRuntimeReceiptDigest,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(correlationId)
            || !string.Equals(
                correlationId,
                correlationId.Trim(),
                StringComparison.Ordinal)
            || correlationId.Any(char.IsWhiteSpace))
        {
            failureReason = "crop-cycle-correlation-invalid";
            return false;
        }

        CropPlotState[] owners = states.Values
            .Where(candidate => candidate?.CycleExecutionReceipt != null
                && !candidate.CycleExecutionReceipt.IsEmpty
                && string.Equals(
                    candidate.CycleExecutionReceipt.correlationId,
                    correlationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (owners.Length != 1)
        {
            failureReason = owners.Length == 0
                ? "crop-cycle-execution-receipt-not-found"
                : "crop-cycle-correlation-global-conflict";
            return false;
        }
        CropPlanExecutionReceiptAuthority.Validate(
            owners[0].CycleExecutionReceipt,
            requireCompleted: false);
        if (owners[0].IrrigationPublication.pending)
        {
            failureReason = "crop-cycle-irrigation-publication-pending";
            return false;
        }
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                expectedRuntimeReceiptDigest)
            || !string.Equals(
                owners[0].CycleExecutionReceipt.sourceDigest,
                expectedRuntimeReceiptDigest,
                StringComparison.Ordinal))
        {
            failureReason = "crop-cycle-execution-receipt-digest-mismatch";
            return false;
        }
        if (owners[0].CycleExecutionReceipt.status
            == CropCycleExecutionReceiptStatus.Active)
        {
            failureReason = "crop-cycle-execution-receipt-not-terminal";
            return false;
        }

        if (IsFailedCycleReceipt(owners[0].CycleExecutionReceipt)
            && (!TryEnsureFailedOutcomeAcknowledged(
                    owners[0], out failureReason)
                || !TryCloseFailedCyclePhysicalOwners(
                    owners[0], out failureReason)))
            return false;

        owners[0].CycleExecutionReceipt =
            new CropCycleExecutionReceiptSaveData();
        owners[0].FailedOutcome = new CropFailedOutcomeSaveData();
        mutation.MarkChanged(replan: false);
        return true;
    }

    internal static void RecordCycleContribution(
        CropPlotState state,
        CharacterActor worker,
        float acceptedWork,
        bool harvest)
    {
        if (acceptedWork <= 0f || state?.CycleExecutionReceipt == null)
            return;
        string characterId = worker?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        if (characterId.Length == 0)
            return;
        if (state.CycleExecutionReceipt.IsEmpty
            || state.CycleExecutionReceipt.status
                != CropCycleExecutionReceiptStatus.Active)
        {
            throw new InvalidOperationException(
                "Crop work contribution has no active cycle receipt.");
        }
        state.CycleExecutionReceipt = CropPlanExecutionReceiptAuthority
            .RecordContribution(
                state.CycleExecutionReceipt,
                characterId,
                acceptedWork,
                harvest);
    }

    internal void FreezeFailedOutcome(CropPlotState state, int absoluteDay)
    {
        CropCycleExecutionReceiptSaveData terminal =
            state?.CycleExecutionReceipt;
        if (!IsFailedCycleReceipt(terminal))
            throw new InvalidOperationException(
                "A crop failure outcome requires a failed execution receipt.");
        if (state.FailedOutcome != null && !state.FailedOutcome.IsEmpty)
        {
            if (!string.Equals(
                    state.FailedOutcome.receiptDigest,
                    terminal.sourceDigest,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop failure outcome snapshot drifted from its receipt.");
            return;
        }
        if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
            throw new InvalidOperationException(
                "Crop failure outcome references unknown crop content.");
        string plotName = state.PlotDisplayName?.Trim() ?? string.Empty;
        string cropName = crop.DisplayName?.Trim() ?? string.Empty;
        if (plotName.Length == 0 || cropName.Length == 0
            || string.Equals(cropName, crop.CropId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Crop failure outcome lacks an authored display snapshot.");
        state.FailedOutcome = new CropFailedOutcomeSaveData
        {
            receiptDigest = terminal.sourceDigest,
            plotDisplayName = plotName,
            cropDisplayName = cropName,
            absoluteDay = Math.Max(0, absoluteDay),
            ownerRevision = checked((long)state.NextHarvestOperationSequence + 1L)
        };
    }

    internal bool TryEnsureFailedOutcomeAcknowledged(
        CropPlotState state,
        out string failureReason)
    {
        if (state?.IrrigationPublication.pending == true)
        {
            failureReason = "crop-failed-cycle-irrigation-publication-pending";
            return false;
        }
        CropCycleExecutionReceiptSaveData terminal =
            state?.CycleExecutionReceipt;
        CropFailedOutcomeSaveData frozen = state?.FailedOutcome;
        if (!IsFailedCycleReceipt(terminal)
            || frozen == null
            || frozen.IsEmpty
            || !string.Equals(
                frozen.receiptDigest,
                terminal.sourceDigest,
                StringComparison.Ordinal))
        {
            failureReason = "crop-failed-outcome-snapshot-invalid";
            return false;
        }
        CropCycleTerminalFact fact = new(state.PlotId, terminal,
            frozen.plotDisplayName, frozen.cropDisplayName, frozen.absoluteDay, frozen.ownerRevision);
        return factPublication.TryDeliverCropFact(fact, ref frozen.publicationKey,
            ref frozen.publicationId, ref frozen.publicationFingerprint, out failureReason);
    }

    private bool TryCloseFailedCyclePhysicalOwners(
        CropPlotState state,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (state.IrrigationPublication.pending)
        {
            failureReason = "crop-failed-cycle-irrigation-publication-pending";
            return false;
        }
        if (state.PendingHarvest.phase != CropHarvestOutputPhase.None
            || state.PendingSow.phase != CropPhysicalCommitPhase.None)
        {
            failureReason = "crop-failed-cycle-physical-child-pending";
            return false;
        }
        if (!state.MaterialsConsumed
            && state.CurrentWater == 0f
            && state.Phase == CropPlotPhase.Blocked
            && state.WaterRefill.phase == CropWaterRefillPhase.None
            && state.Treatment.phase == CropTreatmentOrderPhase.None
            && state.FrozenSowInputOperationSequence < 0)
            return true;
        if (!waterRuntime.TryRetireInactiveWaterRefill(state))
        {
            failureReason = "crop-failed-cycle-water-refill-pending";
            return false;
        }
        CropTreatmentOrderSaveData treatment = state.Treatment;
        if (treatment.phase is CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted)
            treatmentRuntime.TryFinalizeTreatment(state);
        else if (treatment.phase is CropTreatmentOrderPhase.WaitingForDelivery
                or CropTreatmentOrderPhase.ReadyForWork
                or CropTreatmentOrderPhase.Working)
        {
            if (!physicalInputs.TryRetireDestination(
                    state,
                    treatment.destinationId,
                    CropPlotInputOwnerAuthority.TreatmentCancelledReleaseReasonCode,
                    out failureReason))
                return false;
            CropTreatmentPhysicalOutbox.Clear(treatment);
        }
        if (treatment.phase != CropTreatmentOrderPhase.None)
        {
            failureReason = "crop-failed-cycle-treatment-child-pending";
            return false;
        }
        string releaseReason = state.Building == null
            || state.Building.isDestroy
            ? CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode
            : CropPlotInputOwnerAuthority.SowCompletedReleaseReasonCode;
        if (!physicalInputs.TryRetireDestination(
                state,
                state.MaterialDestinationId,
                releaseReason,
                out failureReason))
            return false;
        state.MaterialsConsumed = false;
        state.CurrentWater = 0f;
        state.Phase = CropPlotPhase.Blocked;
        state.SeasonalYieldDamage = new CropSeasonalYieldDamageSaveData();
        CropPlotSowRuntime.ClearFrozenSowInputs(state);
        mutation.MarkChanged(replan: false);
        return true;
    }

    internal bool TryAdvanceFailedCycle(CropPlotState state)
    {
        if (!TryEnsureFailedOutcomeAcknowledged(
                state, out string outcomeFailure))
        {
            state.BlockedReason = outcomeFailure;
            snapshotsDirty = true;
            return false;
        }
        if (!TryCloseFailedCyclePhysicalOwners(
                state, out string cleanupFailure))
        {
            state.BlockedReason = cleanupFailure;
            snapshotsDirty = true;
            return false;
        }
        if (state.CycleExecutionReceipt.explicitCorrelation)
        {
            state.BlockedReason =
                "crop-cycle-execution-receipt-awaiting-acknowledgement";
            snapshotsDirty = true;
            return false;
        }
        state.CycleExecutionReceipt = new CropCycleExecutionReceiptSaveData();
        state.FailedOutcome = new CropFailedOutcomeSaveData();
        mutation.MarkChanged(replan: false);
        return true;
    }
}
