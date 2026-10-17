using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

using static CropPlotOperationIdentity;

internal sealed class CropPlotCycleSelectionRuntime
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IGameCalendar calendar;
    private readonly IGameClock gameClock;
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly BlueprintResearchRuntime research;
    private readonly CropPlotMutationRuntime mutation;
    private readonly CropPlotPhysicalInputRuntime physicalInputs;
    private readonly CropPlotFactPublication factPublication;

    internal CropPlotCycleSelectionRuntime(CropPlotWorldDependencies world,
        CropPlotSimulationDependencies simulation,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CropPlotMutationRuntime mutation,
        CropPlotPhysicalInputRuntime physicalInputs,
        CropPlotFactPublication factPublication,
        BlueprintResearchRuntime research)
    {
        this.aggregateRootStore = aggregateRootStore;
        catalog = world.Catalog;
        calendar = simulation.Calendar;
        gameClock = simulation.GameClock;
        extremeTraits = simulation.ExtremeTraits;
        this.research = research;
        this.mutation = mutation;
        this.physicalInputs = physicalInputs;
        this.factPublication = factPublication;
    }

    private int CurrentAbsoluteDay => calendar.Day;
    private bool snapshotsDirty
    {
        set => aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState()).SnapshotsDirty = value;
    }

    internal bool TrySetCrop(
        CropPlotState state,
        string cropId,
        out string message)
    {
        if (!mutation.TryRequireMutable(state.PlotId, out message))
            return false;
        if (state.CropSelectionOutcome is { IsEmpty: false }
            && !TryAdvanceCropSelectionOutcome(state))
        {
            message = "이전 작물 지정 결과를 원장에 확정하는 중입니다: "
                + state.CropSelectionOutcome.failureReason;
            return false;
        }

        if (state.MaterialsConsumed
            || state.PendingSow.phase != CropPhysicalCommitPhase.None
            || state.WaterRefill.phase != CropWaterRefillPhase.None
            || state.Treatment.phase != CropTreatmentOrderPhase.None
            || state.FailedOutcome != null
                && !state.FailedOutcome.IsEmpty
            || state.CycleExecutionReceipt != null
                && !state.CycleExecutionReceipt.IsEmpty
                && state.CycleExecutionReceipt.explicitCorrelation
            || state.Phase is CropPlotPhase.Sowing
                or CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting)
        {
            message = "현재 재배 주기가 끝난 뒤 작물을 바꿀 수 있습니다.";
            return false;
        }

        if (!catalog.TryGetCrop(cropId, out CropDefinitionSO crop))
        {
            message = "알 수 없는 작물입니다.";
            return false;
        }

        if (state.Ability.Indoor && !crop.IndoorAllowed)
        {
            message = "이 작물은 실내에서 재배할 수 없습니다.";
            return false;
        }

        if (!IsResearchUnlocked(crop, out message))
        {
            return false;
        }

        if (string.Equals(state.CropId, crop.CropId, StringComparison.Ordinal))
        {
            message = $"{crop.DisplayName} 재배가 이미 지정되어 있습니다.";
            return true;
        }

        int selectionSequence = state.NextCropSelectionOperationSequence;
        string selectionOperationId = CropSelectionOperationPrefix
            + state.PlotId.Value + ":"
            + selectionSequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
        var pendingSelection = new CropSelectionCommandSaveData
        {
            phase = CropCommandOutcomePhase.MutationApplied,
            operationSequence = selectionSequence, operationId = selectionOperationId,
            cropBefore = state.CropId, cropAfter = crop.CropId,
            waterBefore = state.CurrentWater, waterAfter = 0f,
            completedAbsoluteDay = CurrentAbsoluteDay,
            plotDisplayName = state.PlotDisplayName, cropDisplayName = crop.DisplayName,
            locationX = state.LastKnownPosition.x, locationY = state.LastKnownPosition.y
        };
        IDomainFactPublisher publisher = factPublication.RequireDomainFacts();
        if (!publisher.TryReserve(new CropSelectionChangedFact(state.PlotId, pendingSelection),
                out DomainFactLease selectionReservation, out string selectionReserveFailure))
        {
            message = "작물 지정 결과를 예약할 수 없습니다: " + selectionReserveFailure;
            return false;
        }
        pendingSelection.publicationKey = selectionReservation.PublicationKey;

        if (!physicalInputs.TryRetireDestination(
                state,
                state.MaterialDestinationId,
                CropPlotInputOwnerAuthority.CropChangedReleaseReasonCode,
                out string retireFailure))
        {
            publisher.Cancel(selectionReservation);
            message = "기존 경작지 입력 소유권을 종료할 수 없습니다: "
                + retireFailure;
            return false;
        }
        state.CropId = crop.CropId;
        state.NextSowOperationSequence = checked(
            state.NextSowOperationSequence + 1);
        state.MaterialDestinationId = BuildSowDestinationId(
            state.PlotId,
            state.NextSowOperationSequence);
        state.Phase = CropPlotPhase.Empty;
        state.SowWork = 0f;
        state.GrowthHours = 0f;
        state.HarvestWork = 0f;
        state.MaterialsConsumed = false;
        state.CurrentWater = 0f;
        state.WaterCapacity = CropWaterRules.WaterCapacity;
        state.WaterRefill = new CropWaterRefillSaveData();
        CropPlotSowRuntime.ClearFrozenSowInputs(state);
        state.PendingCycleCorrelationId = string.Empty;
        state.CycleExecutionReceipt = new CropCycleExecutionReceiptSaveData();
        state.SeasonalYieldDamage =
            new CropSeasonalYieldDamageSaveData();
        state.BlockedReason = string.Empty;
        state.CropSelectionOutcome = pendingSelection;
        mutation.MarkChanged();
        bool recorded = TryAdvanceCropSelectionOutcome(
            state,
            selectionReservation);
        message = recorded
            ? $"{crop.DisplayName} 재배를 지정했습니다."
            : $"{crop.DisplayName} 재배 지정은 적용됐지만 결과 원장 확정이 대기 중입니다: "
                + state.CropSelectionOutcome.failureReason;
        return recorded;
    }

    internal bool TryAdvanceCropSelectionOutcome(
        CropPlotState state, DomainFactLease reserved = default)
    {
        CropSelectionCommandSaveData owner = state?.CropSelectionOutcome;
        if (owner == null || owner.IsEmpty) return true;
        DomainFactDelivery delivery = factPublication.DeliverCropCommandFact(reserved, owner.publicationKey,
            new CropSelectionChangedFact(state.PlotId, owner));
        if (!CropPlotFactPublication.ConfirmCropCommandPublication(delivery, owner.publicationKey,
                owner.gameplayOutcomeId, owner.gameplayOutcomeDigest, out string failure))
        {
            owner.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        owner.phase = CropCommandOutcomePhase.LedgerCommitted;
        owner.gameplayOutcomeId = delivery.PublicationId;
        owner.gameplayOutcomeDigest = delivery.Fingerprint;
        if (delivery.Phase != DomainFactDeliveryPhase.Delivered)
        {
            owner.failureReason = delivery.Detail;
            snapshotsDirty = true;
            return false;
        }
        state.NextCropSelectionOperationSequence = checked(owner.operationSequence + 1);
        state.CropSelectionOutcome = new CropSelectionCommandSaveData();
        mutation.MarkChanged(replan: false);
        return true;
    }

    internal bool TryScheduleGoldenHarvest(
        CropPlotState state,
        CharacterActor harvester,
        out string failureReason)
    {
        if (!mutation.TryRequireMutable(state.PlotId, out failureReason))
            return false;
        if (!string.IsNullOrWhiteSpace(state.GoldenHarvestHarvesterId))
        {
            failureReason = "이미 황금 수확 작업자가 지정되어 있습니다.";
            return false;
        }
        string harvesterId = harvester.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        if (harvesterId.Length == 0)
        {
            failureReason = "저장 가능한 작업자 ID가 필요합니다.";
            return false;
        }
        int scheduleSequence = state.GoldenHarvestAttemptSequence;
        string operationId = GoldenHarvestScheduleOperationPrefix
            + state.PlotId.Value + ":"
            + scheduleSequence.ToString(
                "D6",
                System.Globalization.CultureInfo.InvariantCulture);
        if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
        {
            failureReason = "golden-harvest-crop-definition-missing";
            return false;
        }
        var pendingSchedule = new CropGoldenHarvestScheduleSaveData
        {
            phase = CropCommandOutcomePhase.MutationApplied,
            operationSequence = scheduleSequence, operationId = operationId,
            cropId = state.CropId, harvesterId = harvesterId,
            harvesterDisplayName = harvester.Identity.DisplayName,
            harvesterBefore = string.Empty, harvesterAfter = harvesterId,
            attemptBefore = scheduleSequence, attemptAfter = checked(scheduleSequence + 1),
            completedAbsoluteDay = CurrentAbsoluteDay,
            plotDisplayName = state.PlotDisplayName, cropDisplayName = crop.DisplayName,
            locationX = state.LastKnownPosition.x, locationY = state.LastKnownPosition.y
        };
        IDomainFactPublisher publisher = factPublication.RequireDomainFacts();
        if (!publisher.TryReserve(new GoldenHarvestScheduledFact(state.PlotId, pendingSchedule),
                out DomainFactLease reserved, out string reserveFailure))
        {
            failureReason = "황금 수확 예약 결과를 예약할 수 없습니다: " + reserveFailure;
            return false;
        }
        pendingSchedule.publicationKey = reserved.PublicationKey;
        if (extremeTraits == null
            || !extremeTraits.TryScheduleGoldenHarvest(
                harvester,
                state.PlotId.Value,
                state.GoldenHarvestAttemptSequence,
                gameClock.Time))
        {
            publisher.Cancel(reserved);
            failureReason = "황금 수확을 예약할 수 없습니다.";
            return false;
        }
        state.GoldenHarvestHarvesterId = harvesterId;
        state.GoldenHarvestAttemptSequence = checked(
            state.GoldenHarvestAttemptSequence + 1);
        state.GoldenHarvestScheduleOutcome = pendingSchedule;
        mutation.MarkChanged();
        bool recorded = TryAdvanceGoldenHarvestScheduleOutcome(state, reserved);
        failureReason = recorded
            ? $"{harvester.Identity.DisplayName}에게 황금 수확을 예약했습니다."
            : "황금 수확 예약은 적용됐지만 결과 원장 확정이 대기 중입니다: "
                + state.GoldenHarvestScheduleOutcome.failureReason;
        return recorded;
    }

    internal bool TryAdvanceGoldenHarvestScheduleOutcome(
        CropPlotState state, DomainFactLease reserved = default)
    {
        CropGoldenHarvestScheduleSaveData owner = state?.GoldenHarvestScheduleOutcome;
        if (owner == null || owner.IsEmpty) return true;
        DomainFactDelivery delivery = factPublication.DeliverCropCommandFact(reserved, owner.publicationKey,
            new GoldenHarvestScheduledFact(state.PlotId, owner));
        if (!CropPlotFactPublication.ConfirmCropCommandPublication(delivery, owner.publicationKey,
                owner.gameplayOutcomeId, owner.gameplayOutcomeDigest, out string failure))
        {
            owner.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        owner.phase = CropCommandOutcomePhase.LedgerCommitted;
        owner.gameplayOutcomeId = delivery.PublicationId;
        owner.gameplayOutcomeDigest = delivery.Fingerprint;
        if (delivery.Phase != DomainFactDeliveryPhase.Delivered)
        {
            owner.failureReason = delivery.Detail;
            snapshotsDirty = true;
            return false;
        }
        state.GoldenHarvestScheduleOutcome = new CropGoldenHarvestScheduleSaveData();
        mutation.MarkChanged(replan: false);
        return true;
    }

    internal bool IsResearchUnlocked(
        CropDefinitionSO crop,
        out string reason)
    {
        reason = string.Empty;
        if (crop == null || string.IsNullOrWhiteSpace(crop.RequiredResearchId))
        {
            return true;
        }

        if (!research.State.Projects.IsCompleted(
                new ResearchProjectId(crop.RequiredResearchId)))
        {
            reason = $"연구 필요: {crop.RequiredResearchId}";
            return false;
        }

        return true;
    }
}
