using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CropPlotSnapshotProjector
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionItemGateway items;
    private readonly ICropEcologyService ecology;
    private readonly ICropIrrigationRuntime irrigation;
    private readonly IEnvironmentalFieldQuery environmentalField;
    private readonly IGameCalendar calendar;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly CropPlotInputRequirementsQuery inputRequirementsQuery;
    private readonly CropTreatmentDefinitionQuery treatmentDefinitions;

    private int observedEnvironmentalFieldVersion = -1;
    private int observedIrrigationVersion = -1;

    internal CropPlotSnapshotProjector(
        CropPlotWorldDependencies world,
        CropPlotSimulationDependencies simulation,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CropPlotInputRequirementsQuery inputRequirementsQuery,
        CropTreatmentDefinitionQuery treatmentDefinitions)
    {
        catalog = world.Catalog;
        items = world.Items;
        ecology = world.Ecology;
        irrigation = world.Irrigation;
        environmentalField = simulation.EnvironmentalField;
        calendar = simulation.Calendar;
        this.aggregateRootStore = aggregateRootStore;
        this.inputRequirementsQuery = inputRequirementsQuery;
        this.treatmentDefinitions = treatmentDefinitions;
    }

    private CropPlotAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState());
    private Dictionary<BuildingInstanceId, CropPlotState> states =>
        aggregateState.States;
    private List<CropPlotSnapshot> snapshots => aggregateState.Snapshots;
    private bool snapshotsDirty
    {
        get => aggregateState.SnapshotsDirty;
        set => aggregateState.SnapshotsDirty = value;
    }

    private int CurrentAbsoluteDay => calendar.Day;

    internal void RefreshSnapshots()
    {
        int environmentalFieldVersion = environmentalField.Version;
        int irrigationVersion = irrigation.Version;
        if (!snapshotsDirty
            && observedEnvironmentalFieldVersion == environmentalFieldVersion
            && observedIrrigationVersion == irrigationVersion)
        {
            return;
        }

        snapshots.Clear();
        foreach (CropPlotState state in states.Values
                     .OrderBy(entry => entry.PlotId.Value, StringComparer.Ordinal))
        {
            // A destroyed facility can retain its crop state until terminal
            // physical owners finish releasing their claims and inputs. That
            // detached state is lifecycle-only and must not be projected.
            if (state.Building == null || state.Building.IsBuildingDestroyed)
            {
                continue;
            }
            if (state.Ability == null)
            {
                throw new InvalidOperationException(
                    "A live crop plot has no authored crop-plot ability: "
                    + state.PlotId.Value);
            }
            if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
            {
                continue;
            }

            Dictionary<string, int> required = CropPlotInputRequirementsQuery.RequiresSowInputAuthority(state)
                ? inputRequirementsQuery.BuildMaterialRequirements(state, crop)
                : new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> delivered = required.Keys.ToDictionary(
                itemId => itemId,
                itemId => items.CountDelivered(
                    itemId,
                    state.MaterialDestinationId),
                StringComparer.Ordinal);
            CropCycleWaterSupplyStatus waterSupplyStatus =
                ResolveCycleWaterSupplyStatus(
                    state,
                    crop,
                    required,
                    out int cycleWaterQuantity);
            float dailyWaterDemand = CropWaterRules.ResolveDailyDemand(
                crop,
                state.Ability);
            CropWaterRefillSaveData waterRefill = state.WaterRefill
                ?? new CropWaterRefillSaveData();
            int waterRefillDelivered = waterRefill.phase
                    == CropWaterRefillPhase.None
                ? 0
                : items.CountDelivered(
                    waterRefill.itemId,
                    waterRefill.destinationId);
            CropEcologyPlotSaveData ecologyPlot = ecology.Plots.FirstOrDefault(value =>
                string.Equals(value.plotId, state.PlotId.Value, StringComparison.Ordinal));
            CropTreatmentOrderSaveData treatment = state.Treatment
                ?? new CropTreatmentOrderSaveData();
            EnvironmentalCellSnapshot environmentCell = default;
            bool hasEnvironmentObservation = state.Building != null
                && environmentalField.IsInitialized
                && environmentalField.TryGetCell(
                    state.Building.centerPos,
                    out environmentCell);
            bool requiresEcologyPhenotype = state.Phase is
                CropPlotPhase.Sowing
                or CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting;
            if (ecologyPlot == null && requiresEcologyPhenotype)
                throw new InvalidOperationException(
                    $"Crop plot '{state.PlotId.Value}' in phase {state.Phase} "
                    + "requires an owned ecology phenotype.");
            CropGenomePhenotype temperaturePhenotype = requiresEcologyPhenotype
                ? ecology.GetPhenotype(state.PlotId.Value)
                : new CropGenomePhenotype(
                    coldToleranceDegrees: 0f,
                    heatToleranceDegrees: 0f,
                    growthMultiplier: 1f,
                    yieldMultiplier: 1f,
                    diseaseRiskMultiplier: 1f,
                    seedYieldBonus: 0);
            CropGrowthTemperatureEvaluation temperatureEvaluation =
                CropGrowthCycleAuthority.EvaluateTemperature(
                    crop,
                    temperaturePhenotype,
                    hasEnvironmentObservation,
                    hasEnvironmentObservation
                        ? environmentCell.TemperatureC
                        : 0f);
            CropGrowthLightEvaluation lightEvaluation =
                CropGrowthCycleAuthority.EvaluateLight(
                    crop,
                    hasEnvironmentObservation,
                    hasEnvironmentObservation
                        ? environmentCell.LightLevel
                        : 0f);
            CropIrrigationAssessment irrigationAssessment = irrigation.Assess(
                new CropIrrigationRequest(
                    state.Building,
                    dailyWaterDemand > 0f
                        && state.CurrentWater + 0.001f < dailyWaterDemand,
                    state.CurrentWater,
                    state.WaterCapacity,
                    waterRefill.phase != CropWaterRefillPhase.None));
            int treatmentDelivered = treatment.phase
                    == CropTreatmentOrderPhase.None
                ? 0
                : items.CountDelivered(
                    treatment.itemId,
                    treatment.destinationId);
            snapshots.Add(new CropPlotSnapshot
            {
                PlotId = state.PlotId.Value,
                BuildingId = state.Building != null ? state.Building.id : 0,
                Position = state.Building != null
                    ? state.Building.centerPos
                    : Vector2Int.zero,
                Indoor = state.Ability?.Indoor ?? false,
                CropId = crop.CropId,
                CropName = crop.DisplayName,
                SeedItemId = crop.SeedItemId,
                CultivarGenomeId = ecologyPlot?.cultivarGenomeId ?? string.Empty,
                Fertility = ecologyPlot?.fertility ?? 100f,
                PestPressure = ecologyPlot?.pestPressure ?? 0f,
                DiseasePressure = ecologyPlot?.diseasePressure ?? 0f,
                CropDisease = ecologyPlot?.disease ?? CropDiseaseKind.None,
                Phase = state.Phase,
                SowProgress = crop.SowWork <= 0f
                    ? 0f
                    : Mathf.Clamp01(state.SowWork / crop.SowWork),
                GrowthProgress = crop.GrowthHours <= 0f
                    ? 0f
                    : Mathf.Clamp01(state.GrowthHours / crop.GrowthHours),
                HarvestProgress = crop.HarvestWork <= 0f
                    ? 0f
                    : Mathf.Clamp01(state.HarvestWork / crop.HarvestWork),
                MaterialDestinationId = state.MaterialDestinationId,
                RequiredMaterials = required,
                DeliveredMaterials = delivered,
                CycleWaterSupplyStatus = waterSupplyStatus,
                CycleWaterQuantity = cycleWaterQuantity,
                WaterStatus = ResolveWaterStatus(
                    state,
                    dailyWaterDemand),
                CurrentWater = state.CurrentWater,
                WaterCapacity = state.WaterCapacity,
                WaterDemandPerDay = dailyWaterDemand,
                WaterGrowthMultiplier = CropWaterRules.ResolveGrowthMultiplier(
                    state.CurrentWater,
                    dailyWaterDemand),
                TemperatureStatus = temperatureEvaluation.Status,
                CurrentTemperatureC =
                    temperatureEvaluation.ObservedTemperatureC,
                MinimumTemperatureC =
                    temperatureEvaluation.MinimumTemperatureC,
                MaximumTemperatureC =
                    temperatureEvaluation.MaximumTemperatureC,
                LightStatus = lightEvaluation.Status,
                CurrentLight = lightEvaluation.ObservedLight,
                LightStopThreshold = lightEvaluation.StopLight,
                LightSufficientThreshold = lightEvaluation.SufficientLight,
                LightGrowthMultiplier = lightEvaluation.GrowthMultiplier,
                WaterRefillPhase = waterRefill.phase,
                WaterRefillDestinationId = waterRefill.destinationId,
                WaterRefillDeliveredQuantity = waterRefillDelivered,
                WaterRefillRequiredWork = waterRefill.requiredWork,
                WaterRefillCompletedWork = waterRefill.completedWork,
                WaterRefillFailureReason = waterRefill.failureReason,
                IrrigationStatus = irrigationAssessment.Status,
                IrrigationFacilityId = irrigationAssessment.IrrigatorId.Value,
                IrrigationReason = irrigationAssessment.Reason,
                BlockedReason = state.BlockedReason,
                GoldenHarvestHarvesterId = state.GoldenHarvestHarvesterId,
                GoldenHarvestAttemptSequence = state.GoldenHarvestAttemptSequence,
                HasSeasonalPrimaryYieldDamage =
                    state.SeasonalYieldDamage is { IsEmpty: false },
                SeasonalDamageSourceEventInstanceId =
                    state.SeasonalYieldDamage?.sourceEventInstanceId
                    ?? string.Empty,
                SeasonalPrimaryBatchLossPercent =
                    state.SeasonalYieldDamage?.primaryBatchLossPercent ?? 0,
                FrozenPrimaryQuantityBeforeSeasonalLoss =
                    state.PendingHarvest?.primaryQuantityBeforeSeasonalLoss ?? 0,
                FrozenPrimarySeasonalLossQuantity =
                    state.PendingHarvest?.seasonalPrimaryLossQuantity ?? 0,
                FrozenPrimaryQuantityAfterSeasonalLoss =
                    state.PendingHarvest is
                        { seasonalPrimaryBatchLossPercent: > 0 }
                        ? state.PendingHarvest.harvestQuantity
                        : 0,
                TreatmentScheduled = treatment.phase
                    != CropTreatmentOrderPhase.None,
                TreatmentPhase = treatment.phase,
                TreatmentItemId = treatment.itemId,
                TreatmentItemName = treatmentDefinitions.ResolveTreatmentDisplayName(
                    treatment.itemId),
                TreatmentKind = treatment.treatmentKind,
                TreatmentRequiredQuantity = treatment.quantity,
                TreatmentDeliveredQuantity = treatmentDelivered,
                TreatmentRequiredWork = treatment.requiredWork,
                TreatmentCompletedWork = treatment.completedWork,
                TreatmentEffectAmount = treatment.effectAmount,
                TreatmentCooldownDays = treatment.cooldownDays,
                TreatmentDestinationId = treatment.destinationId,
                TreatmentFailureReason = treatment.failureReason,
                CurrentAbsoluteDay = CurrentAbsoluteDay,
                PestLureNextAllowedDay = state.PestLureNextAllowedDay,
                BotanicalPesticideNextAllowedDay =
                    state.BotanicalPesticideNextAllowedDay,
                FungicideNextAllowedDay = state.FungicideNextAllowedDay
            });
        }

        observedEnvironmentalFieldVersion = environmentalFieldVersion;
        observedIrrigationVersion = irrigationVersion;
        snapshotsDirty = false;
    }

    internal IReadOnlyList<CropPlotSnapshot> CreateSnapshotView()
    {
        CropPlotSnapshot[] view = snapshots
            .Select(snapshot => snapshot.Clone())
            .ToArray();
        return ReadOnlyView.List(view);
    }

    private static CropCycleWaterSupplyStatus ResolveCycleWaterSupplyStatus(
        CropPlotState state,
        CropDefinitionSO crop,
        IReadOnlyDictionary<string, int> currentRequirements,
        out int cycleWaterQuantity)
    {
        cycleWaterQuantity = 0;
        if (currentRequirements != null
            && currentRequirements.TryGetValue(
                CropCycleInputRequirementAuthority.CleanWaterItemId,
                out int requiredWater))
        {
            cycleWaterQuantity = requiredWater;
            return CropCycleWaterSupplyStatus.AwaitingCycleSupply;
        }

        CropCycleExecutionReceiptSaveData receipt = state?.CycleExecutionReceipt;
        if (state?.MaterialsConsumed == true
            && receipt != null
            && receipt.status == CropCycleExecutionReceiptStatus.Active)
        {
            cycleWaterQuantity = (receipt.inputs
                    ?? new List<CropPhysicalInputSaveData>())
                .Where(input => input != null && string.Equals(
                    input.itemId,
                    CropCycleInputRequirementAuthority.CleanWaterItemId,
                    StringComparison.Ordinal))
                .Sum(input => input.quantity);
            if (cycleWaterQuantity > 0)
                return CropCycleWaterSupplyStatus.SuppliedForCurrentCycle;
            if (crop.DailyWater > 0f)
                throw new InvalidOperationException(
                    "Active crop cycle is missing its committed clean-water input.");
            return CropCycleWaterSupplyStatus.NotRequired;
        }

        return crop.DailyWater <= 0f
            ? CropCycleWaterSupplyStatus.NotRequired
            : CropCycleWaterSupplyStatus.None;
    }

    private static CropPlotWaterStatus ResolveWaterStatus(
        CropPlotState state,
        float dailyWaterDemand)
    {
        if (dailyWaterDemand <= 0f)
            return CropPlotWaterStatus.NotRequired;
        CropWaterRefillPhase phase = state?.WaterRefill?.phase
            ?? CropWaterRefillPhase.None;
        return phase switch
        {
            CropWaterRefillPhase.WaitingForDelivery =>
                CropPlotWaterStatus.WaitingForDelivery,
            CropWaterRefillPhase.ReadyForWork => CropPlotWaterStatus.ReadyForWork,
            CropWaterRefillPhase.Working => CropPlotWaterStatus.Working,
            CropWaterRefillPhase.InputCommitted =>
                CropPlotWaterStatus.InputCommitted,
            CropWaterRefillPhase.OutcomePublished =>
                CropPlotWaterStatus.OutcomePublished,
            CropWaterRefillPhase.LedgerCommitted =>
                CropPlotWaterStatus.OutcomePublished,
            _ => state.CurrentWater <= 0f
                ? CropPlotWaterStatus.Empty
                : state.CurrentWater + 0.001f < dailyWaterDemand
                    ? CropPlotWaterStatus.Low
                    : CropPlotWaterStatus.Sufficient
        };
    }
}
