using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

using static CropPlotSaveContract;
internal sealed class CropPlotSaveCodec
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IGameClock gameClock;
    private readonly V20StoryContentCatalog storyCatalog;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly CropPlotPhysicalSaveValidator physicalSaveValidator;

    internal CropPlotSaveCodec(CropPlotWorldDependencies world,
        CropPlotSimulationDependencies simulation,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CropPlotPhysicalSaveValidator physicalSaveValidator)
    {
        catalog = world.Catalog;
        gameClock = simulation.GameClock;
        storyCatalog = simulation.StoryCatalog;
        this.aggregateRootStore = aggregateRootStore;
        this.physicalSaveValidator = physicalSaveValidator;
    }

    private CropPlotAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState());
    private Dictionary<BuildingInstanceId, CropPlotState> states => aggregateState.States;


    internal DungeonCropPlotSaveData Capture()
    {
        DungeonCropPlotSaveData data = new DungeonCropPlotSaveData
        {
            handledSeasonalStartInstanceIds = aggregateState
                .HandledSeasonalStartInstanceIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList()
        };
        foreach (CropPlotState state in states.Values
                     .OrderBy(entry => entry.PlotId.Value, StringComparer.Ordinal))
        {
            data.plots.Add(new CropPlotSaveData
            {
                buildingInstanceId = state.PlotId.Value,
                plotDisplayName = state.PlotDisplayName,
                lastKnownGridX = state.LastKnownPosition.x,
                lastKnownGridY = state.LastKnownPosition.y,
                cropId = state.CropId,
                retirementPublicationKey = state.RetirementPublicationKey,
                nextCropSelectionOperationSequence =
                    state.NextCropSelectionOperationSequence,
                cropSelectionOutcome = state.CropSelectionOutcome.DeepClone(),
                phase = state.Phase,
                sowWork = state.SowWork,
                growthHours = state.GrowthHours,
                harvestWork = state.HarvestWork,
                materialsConsumed = state.MaterialsConsumed,
                frozenSowInputOperationSequence =
                    state.FrozenSowInputOperationSequence,
                frozenSowInputSourceDigest = state.FrozenSowInputSourceDigest,
                frozenSowInputVectorDigest = state.FrozenSowInputVectorDigest,
                frozenSowInputWeather = state.FrozenSowInputWeather,
                frozenSowInputConsumptionMultiplier =
                    state.FrozenSowInputConsumptionMultiplier,
                frozenSowInputSelectedFuelItemId =
                    state.FrozenSowInputSelectedFuelItemId,
                frozenSowInputs = state.FrozenSowInputs
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => new CropCycleInputRequirementSaveData
                    {
                        itemId = value.Key,
                        quantity = value.Value
                    })
                    .ToList(),
                goldenHarvestHarvesterId = state.GoldenHarvestHarvesterId,
                goldenHarvestAttemptSequence = state.GoldenHarvestAttemptSequence,
                goldenHarvestScheduleOutcome =
                    state.GoldenHarvestScheduleOutcome.DeepClone(),
                nextSowOperationSequence = state.NextSowOperationSequence,
                pendingSow = state.PendingSow.DeepClone(),
                pendingCycleCorrelationId =
                    state.PendingCycleCorrelationId,
                cycleExecutionReceipt = state.CycleExecutionReceipt.DeepClone(),
                failedOutcome = state.FailedOutcome.DeepClone(),
                currentWater = state.CurrentWater,
                waterCapacity = state.WaterCapacity,
                nextWaterRefillOperationSequence =
                    state.NextWaterRefillOperationSequence,
                irrigationPublication = state.IrrigationPublication.DeepClone(),
                waterRefill = state.WaterRefill.DeepClone(),
                nextTreatmentOperationSequence =
                    state.NextTreatmentOperationSequence,
                pestLureNextAllowedDay = state.PestLureNextAllowedDay,
                botanicalPesticideNextAllowedDay =
                    state.BotanicalPesticideNextAllowedDay,
                fungicideNextAllowedDay = state.FungicideNextAllowedDay,
                treatment = state.Treatment.DeepClone(),
                nextHarvestOperationSequence =
                    state.NextHarvestOperationSequence,
                pendingHarvest = state.PendingHarvest.DeepClone(),
                seasonalYieldDamage = state.SeasonalYieldDamage.DeepClone()
            });
        }

        return data;
    }

    internal CropPlotRestoreCandidate BuildRestore(
        DungeonCropPlotSaveData snapshot)
    {
        HashSet<string> handledSeasonalStarts = RequireSaveRoot(snapshot);
        CropPlotAggregateState restored = new()
        {
            ObservedBuildingVersion = -1,
            NextMaterialRequestTime = gameClock.Time,
            SnapshotsDirty = true,
            Version = aggregateState.Version + 1
        };
        restored.HandledSeasonalStartInstanceIds.UnionWith(
            handledSeasonalStarts);
        HashSet<BuildingInstanceId> seen = new();
        HashSet<string> correlations = new(StringComparer.Ordinal);
        foreach (CropPlotSaveData saved in snapshot.plots)
        {
            BuildingInstanceId plotId = RequireRestorePlotId(saved, seen);
            CropDefinitionSO crop = RequireCrop(saved);
            ValidateRestoreProgress(
                saved,
                crop,
                plotId,
                handledSeasonalStarts);
            string correlationId = !string.IsNullOrEmpty(
                    saved.pendingCycleCorrelationId)
                ? saved.pendingCycleCorrelationId
                : saved.cycleExecutionReceipt.IsEmpty
                    ? string.Empty
                    : saved.cycleExecutionReceipt.correlationId;
            if (correlationId.Length > 0 && !correlations.Add(correlationId))
                throw new InvalidOperationException(
                    "Crop execution correlation is duplicated across plots: "
                    + correlationId);
            restored.States.Add(plotId, new CropPlotState
            {
                PlotId = plotId,
                PlotDisplayName = saved.plotDisplayName ?? string.Empty,
                RetirementPublicationKey = saved.retirementPublicationKey,
                LastKnownPosition = new Vector2Int(
                    saved.lastKnownGridX,
                    saved.lastKnownGridY),
                CropId = crop.CropId,
                NextCropSelectionOperationSequence = Math.Max(
                    0,
                    saved.nextCropSelectionOperationSequence),
                CropSelectionOutcome = saved.cropSelectionOutcome?.DeepClone()
                    ?? new CropSelectionCommandSaveData(),
                Phase = saved.phase,
                SowWork = saved.sowWork,
                GrowthHours = saved.growthHours,
                HarvestWork = saved.harvestWork,
                MaterialDestinationId = BuildSowDestinationId(
                    plotId,
                    saved.nextSowOperationSequence),
                MaterialsConsumed = saved.materialsConsumed,
                FrozenSowInputOperationSequence =
                    saved.frozenSowInputOperationSequence,
                FrozenSowInputSourceDigest =
                    saved.frozenSowInputSourceDigest,
                FrozenSowInputVectorDigest =
                    saved.frozenSowInputVectorDigest,
                FrozenSowInputWeather = saved.frozenSowInputWeather,
                FrozenSowInputConsumptionMultiplier =
                    saved.frozenSowInputConsumptionMultiplier,
                FrozenSowInputSelectedFuelItemId =
                    saved.frozenSowInputSelectedFuelItemId,
                FrozenSowInputs = saved.frozenSowInputs.ToDictionary(
                    value => value.itemId,
                    value => value.quantity,
                    StringComparer.Ordinal),
                GoldenHarvestHarvesterId = saved.goldenHarvestHarvesterId?.Trim()
                    ?? string.Empty,
                GoldenHarvestAttemptSequence = Math.Max(
                    0,
                    saved.goldenHarvestAttemptSequence),
                GoldenHarvestScheduleOutcome = saved
                    .goldenHarvestScheduleOutcome?.DeepClone()
                    ?? new CropGoldenHarvestScheduleSaveData(),
                NextSowOperationSequence = saved.nextSowOperationSequence,
                PendingSow = saved.pendingSow.DeepClone(),
                PendingCycleCorrelationId =
                    saved.pendingCycleCorrelationId ?? string.Empty,
                CycleExecutionReceipt =
                    saved.cycleExecutionReceipt.DeepClone(),
                FailedOutcome = saved.failedOutcome?.DeepClone()
                    ?? new CropFailedOutcomeSaveData(),
                CurrentWater = saved.currentWater,
                WaterCapacity = saved.waterCapacity,
                NextWaterRefillOperationSequence =
                    saved.nextWaterRefillOperationSequence,
                IrrigationPublication = saved.irrigationPublication.DeepClone(),
                WaterRefill = saved.waterRefill.DeepClone(),
                NextTreatmentOperationSequence =
                    saved.nextTreatmentOperationSequence,
                PestLureNextAllowedDay = saved.pestLureNextAllowedDay,
                BotanicalPesticideNextAllowedDay =
                    saved.botanicalPesticideNextAllowedDay,
                FungicideNextAllowedDay = saved.fungicideNextAllowedDay,
                Treatment = saved.treatment.DeepClone(),
                NextHarvestOperationSequence =
                    saved.nextHarvestOperationSequence,
                PendingHarvest = saved.pendingHarvest.phase ==
                        CropHarvestOutputPhase.None
                    ? new CropHarvestOutputSaveData()
                    : saved.pendingHarvest.DeepClone(),
                SeasonalYieldDamage = saved.seasonalYieldDamage.DeepClone(),
                BlockedReason = string.Empty
            });
        }

        return new CropPlotRestoreCandidate(restored);
    }

    private HashSet<string> RequireSaveRoot(
        DungeonCropPlotSaveData snapshot)
    {
        if (snapshot == null)
        {
            throw new InvalidOperationException("Crop-plot payload is null.");
        }
        if (snapshot.version != DungeonCropPlotSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Crop-plot payload version {snapshot.version} is not current V{DungeonCropPlotSaveData.CurrentVersion}.");
        }
        if (snapshot.plots == null || snapshot.plots.Count > 512)
        {
            throw new InvalidOperationException(
                "Crop-plot payload must contain at most 512 non-null plot records.");
        }
        if (snapshot.handledSeasonalStartInstanceIds == null
            || snapshot.handledSeasonalStartInstanceIds.Count > 2048)
            throw new InvalidOperationException(
                "Crop-plot handled seasonal starts are missing or exceed 2048 records.");
        HashSet<string> handled = new(StringComparer.Ordinal);
        foreach (string instanceId in snapshot.handledSeasonalStartInstanceIds)
        {
            if (!TryParseSeasonalEventInstanceId(
                    instanceId,
                    out _,
                    out string definitionId)
                || !storyCatalog.SeasonalEvents.Any(value => value != null
                    && value.cropPrimaryBatchLossPercent > 0
                    && string.Equals(
                        value.StableId,
                        definitionId,
                        StringComparison.Ordinal))
                || !handled.Add(instanceId))
                throw new InvalidOperationException(
                    "Crop-plot handled seasonal start IDs must be canonical and unique.");
        }
        return handled;
    }

    private static BuildingInstanceId RequireRestorePlotId(
        CropPlotSaveData saved,
        ISet<BuildingInstanceId> seen)
    {
        if (saved == null)
        {
            throw new InvalidOperationException(
                "Crop-plot payload contains a null plot record.");
        }

        BuildingInstanceId buildingId =
            (BuildingInstanceId)saved.buildingInstanceId;
        if (!buildingId.IsValid
            || !string.Equals(
                buildingId.Value,
                saved.buildingInstanceId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Crop-plot building instance ID '{saved.buildingInstanceId}' is not canonical.");
        }
        if (!seen.Add(buildingId))
        {
            throw new InvalidOperationException(
                $"Crop-plot building instance ID '{buildingId.Value}' is duplicated.");
        }
        return buildingId;
    }

    private CropDefinitionSO RequireCrop(CropPlotSaveData saved)
    {
        if (string.IsNullOrWhiteSpace(saved.cropId)
            || !string.Equals(saved.cropId, saved.cropId.Trim(), StringComparison.Ordinal)
            || !catalog.TryGetCrop(saved.cropId, out CropDefinitionSO crop))
        {
            throw new InvalidOperationException(
                $"Crop-plot payload references unknown crop '{saved.cropId}'.");
        }
        return crop;
    }

    private void ValidateRestoreProgress(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId,
        ISet<string> handledSeasonalStarts)
    {
        if (saved.retirementPublicationKey == null)
            throw new InvalidOperationException("Crop retirement publication key is missing.");
        if (saved.retirementPublicationKey.Length != 0)
        {
            ValidateCropCommandPublicationKey(saved.retirementPublicationKey);
            if (!string.IsNullOrEmpty(saved.pendingCycleCorrelationId)
                || saved.materialsConsumed || saved.pendingHarvest?.phase != CropHarvestOutputPhase.None
                || saved.pendingSow?.phase != CropPhysicalCommitPhase.None
                || saved.treatment?.phase != CropTreatmentOrderPhase.None
                || saved.waterRefill?.phase != CropWaterRefillPhase.None)
                throw new InvalidOperationException("Retiring crop plot still has active work or physical owners.");
        }
        if (!Enum.IsDefined(typeof(CropPlotPhase), saved.phase))
        {
            throw new InvalidOperationException(
                $"Crop-plot payload contains unknown phase {(int)saved.phase}.");
        }
        RequireFiniteRange(saved.sowWork, 0f, crop.SowWork, "sow work");
        RequireFiniteRange(saved.growthHours, 0f, crop.GrowthHours, "growth hours");
        RequireFiniteRange(saved.harvestWork, 0f, crop.HarvestWork, "harvest work");
        if (!saved.waterCapacity.Equals(CropWaterRules.WaterCapacity))
            throw new InvalidOperationException(
                "Crop-plot water capacity contradicts the current authored rule.");
        RequireFiniteRange(
            saved.currentWater,
            0f,
            saved.waterCapacity,
            "current water");
        if (saved.goldenHarvestAttemptSequence < 0
            || saved.nextCropSelectionOperationSequence < 0
            || saved.cropSelectionOutcome == null
            || saved.goldenHarvestScheduleOutcome == null)
            throw new InvalidOperationException(
                "Crop-plot command outcome owner or sequence is invalid.");
        if (saved.nextSowOperationSequence < 0 || saved.pendingSow == null)
            throw new InvalidOperationException(
                "Crop-plot sow transaction owner is missing or invalid.");
        if (saved.cycleExecutionReceipt == null)
            throw new InvalidOperationException(
                "Crop-plot cycle execution receipt owner is missing.");
        if (!string.IsNullOrEmpty(saved.pendingCycleCorrelationId)
            && (!string.Equals(
                    saved.pendingCycleCorrelationId,
                    saved.pendingCycleCorrelationId.Trim(),
                    StringComparison.Ordinal)
                || saved.pendingCycleCorrelationId.Any(char.IsWhiteSpace)
                || !saved.cycleExecutionReceipt.IsEmpty))
        {
            throw new InvalidOperationException(
                "Crop-plot pending cycle correlation is invalid or already consumed.");
        }
        if (saved.nextTreatmentOperationSequence < 0
            || saved.pestLureNextAllowedDay < 0
            || saved.botanicalPesticideNextAllowedDay < 0
            || saved.fungicideNextAllowedDay < 0
            || saved.treatment == null)
            throw new InvalidOperationException(
                "Crop-plot treatment owner or cooldown is invalid.");
        if (saved.nextWaterRefillOperationSequence < 0
            || saved.waterRefill == null)
            throw new InvalidOperationException(
                "Crop-plot water-refill owner or sequence is invalid.");
        if (saved.seasonalYieldDamage == null)
            throw new InvalidOperationException(
                "Crop-plot seasonal yield-damage owner is missing.");
        if (!string.IsNullOrEmpty(saved.goldenHarvestHarvesterId)
            && (!string.Equals(
                    saved.goldenHarvestHarvesterId,
                    saved.goldenHarvestHarvesterId.Trim(),
                    StringComparison.Ordinal)
                || saved.phase is not (CropPlotPhase.ReadyToHarvest
                    or CropPlotPhase.Harvesting)))
            throw new InvalidOperationException(
                "Crop-plot golden-harvest worker requires a canonical ID and harvest phase.");

        bool requiresConsumedMaterials = saved.phase is
            CropPlotPhase.ReadyToSow
            or CropPlotPhase.Sowing
            or CropPlotPhase.Growing
            or CropPlotPhase.ReadyToHarvest
            or CropPlotPhase.Harvesting;
        bool requiresUnconsumedMaterials = saved.phase is
            CropPlotPhase.Empty or CropPlotPhase.WaitingForMaterials;
        if ((requiresConsumedMaterials && !saved.materialsConsumed)
            || (requiresUnconsumedMaterials && saved.materialsConsumed))
        {
            throw new InvalidOperationException(
                $"Crop-plot phase {saved.phase} contradicts materialsConsumed={saved.materialsConsumed}.");
        }
        if (!saved.materialsConsumed && saved.currentWater != 0f)
            throw new InvalidOperationException(
                "An inactive crop cycle retained physical plot water.");
        if (crop.DailyWater <= 0f
            && (saved.currentWater != 0f
                || saved.waterRefill.phase != CropWaterRefillPhase.None))
            throw new InvalidOperationException(
                "A zero-demand crop retained water or refill provenance.");
        if (saved.waterRefill.phase != CropWaterRefillPhase.None
            && !saved.materialsConsumed)
            throw new InvalidOperationException(
                "Crop water-refill provenance requires an active physical cycle.");

        ValidatePendingSow(saved, crop);
        ValidateIrrigationPublication(saved, plotId);
        ValidateCropSelectionOutcome(saved, plotId);
        ValidateGoldenHarvestScheduleOutcome(saved, plotId);
        ValidateFrozenSowInputs(saved, crop, plotId);
        ValidateCycleExecutionReceipt(saved, crop, plotId);
        ValidateSeasonalYieldDamage(
            saved.seasonalYieldDamage,
            saved.cropId,
            saved.cycleExecutionReceipt,
            handledSeasonalStarts);
        if (!saved.seasonalYieldDamage.IsEmpty)
        {
            SeasonalWorldEventDefinitionSO sourceDefinition = storyCatalog
                .SeasonalEvents.SingleOrDefault(value => value != null
                    && string.Equals(
                        value.StableId,
                        saved.seasonalYieldDamage.sourceDefinitionId,
                        StringComparison.Ordinal));
            if (sourceDefinition == null
                || sourceDefinition.cropPrimaryBatchLossPercent
                    != saved.seasonalYieldDamage.primaryBatchLossPercent)
            {
                throw new InvalidOperationException(
                    "Crop seasonal yield damage contradicts its authored source declaration.");
            }
        }
        if (!saved.seasonalYieldDamage.IsEmpty
            && saved.phase is not (CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting))
            throw new InvalidOperationException(
                "Crop seasonal yield damage requires a live post-sow cycle.");
        physicalSaveValidator.ValidatePendingWaterRefill(saved, crop, plotId);
        physicalSaveValidator.ValidatePendingTreatment(saved, plotId);
        physicalSaveValidator.ValidatePendingHarvest(saved, crop, plotId);

        const float Epsilon = 0.001f;
        if (saved.phase is CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting
            && saved.sowWork + Epsilon < crop.SowWork)
        {
            throw new InvalidOperationException(
                "Crop-plot post-sowing phase has incomplete sow work.");
        }
        if (saved.phase is CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting
            && saved.growthHours + Epsilon < crop.GrowthHours)
        {
            throw new InvalidOperationException(
                "Crop-plot harvest phase has incomplete growth.");
        }
    }

    private void ValidateFrozenSowInputs(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId)
    {
        if (saved.frozenSowInputs == null)
            throw new InvalidOperationException(
                "Crop sow frozen input vector is missing.");

        bool postSow = saved.materialsConsumed
            || saved.phase is CropPlotPhase.ReadyToSow
                or CropPlotPhase.Sowing
                or CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting;
        bool hasFrozen = saved.frozenSowInputs.Count > 0
            || saved.frozenSowInputOperationSequence >= 0
            || !string.IsNullOrEmpty(saved.frozenSowInputSourceDigest)
            || !string.IsNullOrEmpty(saved.frozenSowInputVectorDigest)
            || saved.frozenSowInputConsumptionMultiplier != 0f
            || !string.IsNullOrEmpty(saved.frozenSowInputSelectedFuelItemId);
        bool hasPendingSow = saved.pendingSow.phase
            != CropPhysicalCommitPhase.None;
        if (postSow && !hasPendingSow)
        {
            if (hasFrozen)
                throw new InvalidOperationException(
                    "A post-sow crop plot retained a stale frozen input vector.");
            return;
        }

        bool requiresFrozen = saved.phase == CropPlotPhase.WaitingForMaterials
            || hasPendingSow;
        if (!requiresFrozen && !hasFrozen)
            return;
        if (saved.frozenSowInputOperationSequence
                != saved.nextSowOperationSequence
            || saved.frozenSowInputs.Count == 0
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                saved.frozenSowInputSourceDigest)
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                saved.frozenSowInputVectorDigest))
        {
            throw new InvalidOperationException(
                "Crop sow frozen input owner is incomplete or stale.");
        }
        if (!Enum.IsDefined(
                typeof(SurvivalWeatherType),
                saved.frozenSowInputWeather)
            || !float.IsFinite(saved.frozenSowInputConsumptionMultiplier)
            || saved.frozenSowInputConsumptionMultiplier is < 0.1f or > 1f
            || !string.IsNullOrEmpty(saved.frozenSowInputSelectedFuelItemId)
                && (!string.Equals(
                        saved.frozenSowInputSelectedFuelItemId,
                        saved.frozenSowInputSelectedFuelItemId.Trim(),
                        StringComparison.Ordinal)
                    || !catalog.TryGetItem(
                        saved.frozenSowInputSelectedFuelItemId,
                        out _)))
        {
            throw new InvalidOperationException(
                "Crop sow frozen input selection context is invalid.");
        }

        Dictionary<string, int> requirements = new(StringComparer.Ordinal);
        foreach (CropCycleInputRequirementSaveData input in
                 saved.frozenSowInputs)
        {
            if (input == null
                || string.IsNullOrWhiteSpace(input.itemId)
                || !string.Equals(input.itemId, input.itemId.Trim(),
                    StringComparison.Ordinal)
                || input.quantity <= 0
                || !catalog.TryGetItem(input.itemId, out _)
                || !requirements.TryAdd(input.itemId, input.quantity))
            {
                throw new InvalidOperationException(
                    "Crop sow frozen input vector contains an invalid item.");
            }
        }
        if (!string.IsNullOrEmpty(saved.frozenSowInputSelectedFuelItemId)
            && !requirements.ContainsKey(
                saved.frozenSowInputSelectedFuelItemId))
        {
            throw new InvalidOperationException(
                "Crop sow frozen fuel is absent from its input vector.");
        }
        if (hasPendingSow)
        {
            Dictionary<string, int> committed = saved.pendingSow.inputs
                .GroupBy(value => value.itemId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(value => value.quantity),
                    StringComparer.Ordinal);
            bool committedExact = committed.Count == requirements.Count
                && requirements.All(value =>
                    committed.TryGetValue(value.Key, out int quantity)
                    && quantity == value.Value);
            if (!committedExact)
                throw new InvalidOperationException(
                    "Crop sow frozen inputs drifted from the committed transaction.");
        }
        string expectedDigest = CaptureFrozenSowInputVectorDigest(
            plotId,
            crop.CropId,
            saved.frozenSowInputOperationSequence,
            requirements);
        if (!string.Equals(
                saved.frozenSowInputVectorDigest,
                expectedDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop sow frozen input vector digest drifted.");
        }
    }
}
