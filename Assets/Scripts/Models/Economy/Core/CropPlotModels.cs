using System;
using System.Collections.Generic;
using UnityEngine;

public enum CropPlotPhase
{
    Empty = 0,
    WaitingForMaterials = 1,
    ReadyToSow = 2,
    Sowing = 3,
    Growing = 4,
    ReadyToHarvest = 5,
    Harvesting = 6,
    Blocked = 7
}

public enum CropCycleWaterSupplyStatus
{
    None = 0,
    AwaitingCycleSupply = 1,
    SuppliedForCurrentCycle = 2,
    NotRequired = 3
}

public enum CropPlotWaterStatus
{
    NotRequired = 0,
    Sufficient = 1,
    Low = 2,
    Empty = 3,
    WaitingForDelivery = 4,
    ReadyForWork = 5,
    Working = 6,
    InputCommitted = 7,
    OutcomePublished = 8
}

public enum CropWaterRefillPhase
{
    None = 0,
    WaitingForDelivery = 1,
    ReadyForWork = 2,
    Working = 3,
    InputCommitted = 4,
    // Kept at value 5 for save compatibility. The physical water has been
    // applied to the plot and the durable gameplay outcome is the next step.
    OutcomePublished = 5,
    LedgerCommitted = 6
}

public enum CropCommandOutcomePhase
{
    None = 0,
    MutationApplied = 1,
    LedgerCommitted = 2
}

public static class CropWaterRules
{
    public const float InitialWaterQuantity = 1f;
    public const float WaterCapacity = 2f;
    public const int RefillQuantity = 1;
    public const float RainConsumptionMultiplier = 0.5f;

    public static float ResolveDailyDemand(
        CropDefinitionSO crop,
        BuildingCropPlotAbility ability)
    {
        if (crop == null) throw new ArgumentNullException(nameof(crop));
        if (ability == null) throw new ArgumentNullException(nameof(ability));
        if (!float.IsFinite(crop.DailyWater)
            || crop.DailyWater < 0f
            || !float.IsFinite(ability.WaterMultiplier)
            || ability.WaterMultiplier <= 0f)
        {
            throw new InvalidOperationException(
                "Crop water authoring requires finite non-negative demand and a positive plot multiplier.");
        }
        return crop.DailyWater * ability.WaterMultiplier;
    }

    public static float ResolveHourlyConsumption(
        CropDefinitionSO crop,
        BuildingCropPlotAbility ability,
        SurvivalWeatherType weather)
    {
        if (!Enum.IsDefined(typeof(SurvivalWeatherType), weather))
            throw new ArgumentOutOfRangeException(nameof(weather));
        float weatherMultiplier = weather is SurvivalWeatherType.Rain
            or SurvivalWeatherType.Storm
                ? RainConsumptionMultiplier
                : 1f;
        return ResolveDailyDemand(crop, ability) / 24f * weatherMultiplier;
    }

    public static float ResolveGrowthMultiplier(
        float currentWater,
        float dailyDemand)
    {
        if (!float.IsFinite(currentWater)
            || currentWater < 0f
            || !float.IsFinite(dailyDemand)
            || dailyDemand < 0f)
            throw new ArgumentOutOfRangeException(nameof(currentWater));
        return dailyDemand <= 0f
            ? 1f
            : Mathf.Clamp01(currentWater / dailyDemand);
    }

    public static float RequireRefillWork(BuildableObject building)
    {
        BuildingWorkAmountAbility work = building?.BuildingData?
            .GetAbility<BuildingWorkAmountAbility>();
        if (work == null
            || !float.IsFinite(work.operateWorkRequired)
            || work.operateWorkRequired < 0.1f)
            throw new InvalidOperationException(
                "Crop water refill requires authored facility operate work.");
        return work.operateWorkRequired;
    }
}

public readonly struct CropPlotWorkSnapshot
{
    public CropPlotWorkSnapshot(
        string plotId,
        WorkTypeId workTypeId,
        string displayName,
        float requiredWork,
        float completedWork,
        bool available,
        string unavailableReason,
        int harvestOperationSequence = -1,
        bool harvestCompletionPending = false)
    {
        PlotId = plotId ?? string.Empty;
        WorkTypeId = workTypeId;
        DisplayName = displayName ?? string.Empty;
        RequiredWork = Mathf.Max(0.1f, requiredWork);
        CompletedWork = Mathf.Clamp(completedWork, 0f, RequiredWork);
        Available = available;
        UnavailableReason = unavailableReason ?? string.Empty;
        HarvestOperationSequence = harvestOperationSequence;
        HarvestCompletionPending = harvestCompletionPending;
    }

    public string PlotId { get; }
    public WorkTypeId WorkTypeId { get; }
    public string DisplayName { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public bool Available { get; }
    public string UnavailableReason { get; }
    public int HarvestOperationSequence { get; }
    public bool HarvestCompletionPending { get; }
}

public sealed class CropPlotSnapshot
{
    public string PlotId { get; set; } = string.Empty;
    public int BuildingId { get; set; }
    public Vector2Int Position { get; set; }
    public bool Indoor { get; set; }
    public string CropId { get; set; } = string.Empty;
    public string CropName { get; set; } = string.Empty;
    public string SeedItemId { get; set; } = string.Empty;
    public string CultivarGenomeId { get; set; } = string.Empty;
    public float Fertility { get; set; } = 100f;
    public float PestPressure { get; set; }
    public float DiseasePressure { get; set; }
    public CropDiseaseKind CropDisease { get; set; }
    public CropPlotPhase Phase { get; set; }
    public float SowProgress { get; set; }
    public float GrowthProgress { get; set; }
    public float HarvestProgress { get; set; }
    public string MaterialDestinationId { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, int> RequiredMaterials { get; set; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> DeliveredMaterials { get; set; } =
        new Dictionary<string, int>();
    public CropCycleWaterSupplyStatus CycleWaterSupplyStatus { get; set; }
    public int CycleWaterQuantity { get; set; }
    public CropPlotWaterStatus WaterStatus { get; set; }
    public float CurrentWater { get; set; }
    public float WaterCapacity { get; set; }
    public float WaterDemandPerDay { get; set; }
    public float WaterGrowthMultiplier { get; set; } = 1f;
    public CropGrowthTemperatureStatus TemperatureStatus { get; set; }
    public float CurrentTemperatureC { get; set; }
    public float MinimumTemperatureC { get; set; }
    public float MaximumTemperatureC { get; set; }
    public CropGrowthLightStatus LightStatus { get; set; }
    public float CurrentLight { get; set; }
    public float LightStopThreshold { get; set; }
    public float LightSufficientThreshold { get; set; }
    public float LightGrowthMultiplier { get; set; } = 1f;
    public CropWaterRefillPhase WaterRefillPhase { get; set; }
    public string WaterRefillDestinationId { get; set; } = string.Empty;
    public int WaterRefillDeliveredQuantity { get; set; }
    public float WaterRefillRequiredWork { get; set; }
    public float WaterRefillCompletedWork { get; set; }
    public string WaterRefillFailureReason { get; set; } = string.Empty;
    public CropIrrigationStatus IrrigationStatus { get; set; }
    public string IrrigationFacilityId { get; set; } = string.Empty;
    public string IrrigationReason { get; set; } = string.Empty;
    public string BlockedReason { get; set; } = string.Empty;
    public string GoldenHarvestHarvesterId { get; set; } = string.Empty;
    public int GoldenHarvestAttemptSequence { get; set; }
    public bool TreatmentScheduled { get; set; }
    public CropTreatmentOrderPhase TreatmentPhase { get; set; }
    public string TreatmentItemId { get; set; } = string.Empty;
    public string TreatmentItemName { get; set; } = string.Empty;
    public CropTreatmentKind TreatmentKind { get; set; }
    public int TreatmentRequiredQuantity { get; set; }
    public int TreatmentDeliveredQuantity { get; set; }
    public float TreatmentRequiredWork { get; set; }
    public float TreatmentCompletedWork { get; set; }
    public float TreatmentEffectAmount { get; set; }
    public int TreatmentCooldownDays { get; set; }
    public string TreatmentDestinationId { get; set; } = string.Empty;
    public string TreatmentFailureReason { get; set; } = string.Empty;
    public int CurrentAbsoluteDay { get; set; }
    public int PestLureNextAllowedDay { get; set; }
    public int BotanicalPesticideNextAllowedDay { get; set; }
    public int FungicideNextAllowedDay { get; set; }
    public bool HasSeasonalPrimaryYieldDamage { get; set; }
    public string SeasonalDamageSourceEventInstanceId { get; set; } =
        string.Empty;
    public int SeasonalPrimaryBatchLossPercent { get; set; }
    public int FrozenPrimaryQuantityBeforeSeasonalLoss { get; set; }
    public int FrozenPrimarySeasonalLossQuantity { get; set; }
    public int FrozenPrimaryQuantityAfterSeasonalLoss { get; set; }

    public CropPlotSnapshot Clone()
    {
        CropPlotSnapshot clone = (CropPlotSnapshot)MemberwiseClone();
        clone.RequiredMaterials = CloneQuantities(RequiredMaterials);
        clone.DeliveredMaterials = CloneQuantities(DeliveredMaterials);
        return clone;
    }

    private static IReadOnlyDictionary<string, int> CloneQuantities(
        IReadOnlyDictionary<string, int> source)
    {
        Dictionary<string, int> copy =
            new Dictionary<string, int>(StringComparer.Ordinal);
        if (source != null)
        {
            foreach (KeyValuePair<string, int> quantity in source)
            {
                copy.Add(quantity.Key, quantity.Value);
            }
        }

        return ReadOnlyView.Dictionary(copy);
    }
}

public readonly struct CropPlotVisualState
{
    public CropPlotVisualState(
        string plotId,
        BuildableObject building,
        string cropId,
        CropPlotPhase phase,
        float growthProgress)
    {
        PlotId = plotId ?? string.Empty;
        Building = building;
        CropId = cropId ?? string.Empty;
        Phase = phase;
        GrowthProgress = Mathf.Clamp01(growthProgress);
    }

    public string PlotId { get; }
    public BuildableObject Building { get; }
    public string CropId { get; }
    public CropPlotPhase Phase { get; }
    public float GrowthProgress { get; }
}

[Serializable]
public sealed class CropPlotSaveData
{
    public string buildingInstanceId = string.Empty;
    public string retirementPublicationKey = string.Empty;
    public string plotDisplayName = string.Empty;
    public int lastKnownGridX;
    public int lastKnownGridY;
    public string cropId = string.Empty;
    public int nextCropSelectionOperationSequence;
    public CropSelectionCommandSaveData cropSelectionOutcome = new();
    public CropPlotPhase phase;
    public float sowWork;
    public float growthHours;
    public float harvestWork;
    public bool materialsConsumed;
    public int frozenSowInputOperationSequence = -1;
    public string frozenSowInputSourceDigest = string.Empty;
    public string frozenSowInputVectorDigest = string.Empty;
    public SurvivalWeatherType frozenSowInputWeather;
    public float frozenSowInputConsumptionMultiplier;
    public string frozenSowInputSelectedFuelItemId = string.Empty;
    public List<CropCycleInputRequirementSaveData> frozenSowInputs = new();
    public string goldenHarvestHarvesterId = string.Empty;
    public int goldenHarvestAttemptSequence;
    public CropGoldenHarvestScheduleSaveData goldenHarvestScheduleOutcome = new();
    public int nextSowOperationSequence;
    public CropPhysicalCommitSaveData pendingSow = new();
    public string pendingCycleCorrelationId = string.Empty;
    public CropCycleExecutionReceiptSaveData cycleExecutionReceipt = new();
    public CropFailedOutcomeSaveData failedOutcome = new();
    public float currentWater;
    public float waterCapacity = CropWaterRules.WaterCapacity;
    public int nextWaterRefillOperationSequence;
    public CropIrrigationPublicationSaveData irrigationPublication = new();
    public CropWaterRefillSaveData waterRefill = new();
    public int nextTreatmentOperationSequence;
    public int pestLureNextAllowedDay;
    public int botanicalPesticideNextAllowedDay;
    public int fungicideNextAllowedDay;
    public CropTreatmentOrderSaveData treatment = new();
    public int nextHarvestOperationSequence;
    public CropHarvestOutputSaveData pendingHarvest = new();
    public CropSeasonalYieldDamageSaveData seasonalYieldDamage = new();
}

[Serializable]
public sealed class CropFailedOutcomeSaveData
{
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string publicationFingerprint = string.Empty;
    public string receiptDigest = string.Empty;
    public string plotDisplayName = string.Empty;
    public string cropDisplayName = string.Empty;
    public int absoluteDay;
    public long ownerRevision;

    public bool IsEmpty => string.IsNullOrEmpty(publicationKey)
        && string.IsNullOrEmpty(publicationId)
        && string.IsNullOrEmpty(publicationFingerprint)
        && string.IsNullOrEmpty(receiptDigest)
        && string.IsNullOrEmpty(plotDisplayName)
        && string.IsNullOrEmpty(cropDisplayName)
        && absoluteDay == 0
        && ownerRevision == 0L;

    public CropFailedOutcomeSaveData DeepClone() => new()
    {
        publicationKey = publicationKey,
        publicationId = publicationId,
        publicationFingerprint = publicationFingerprint,
        receiptDigest = receiptDigest ?? string.Empty,
        plotDisplayName = plotDisplayName ?? string.Empty,
        cropDisplayName = cropDisplayName ?? string.Empty,
        absoluteDay = absoluteDay,
        ownerRevision = ownerRevision
    };
}

[Serializable]
public sealed class CropSeasonalYieldDamageSaveData
{
    public string sourceEventInstanceId = string.Empty;
    public string sourceDefinitionId = string.Empty;
    public int sourceStartedAbsoluteDay;
    public string cropId = string.Empty;
    public int sowOperationSequence;
    public int primaryBatchLossPercent;

    public bool IsEmpty => string.IsNullOrEmpty(sourceEventInstanceId)
        && string.IsNullOrEmpty(sourceDefinitionId)
        && sourceStartedAbsoluteDay == 0
        && string.IsNullOrEmpty(cropId)
        && sowOperationSequence == 0
        && primaryBatchLossPercent == 0;

    public CropSeasonalYieldDamageSaveData DeepClone() => new()
    {
        sourceEventInstanceId = sourceEventInstanceId ?? string.Empty,
        sourceDefinitionId = sourceDefinitionId ?? string.Empty,
        sourceStartedAbsoluteDay = sourceStartedAbsoluteDay,
        cropId = cropId ?? string.Empty,
        sowOperationSequence = sowOperationSequence,
        primaryBatchLossPercent = primaryBatchLossPercent
    };
}

[Serializable]
public sealed class CropWaterRefillSaveData
{
    public const int CurrentOutcomeSchemaVersion = 2;

    public CropWaterRefillPhase phase;
    public int outcomeSchemaVersion;
    public int operationSequence;
    public string operationId = string.Empty;
    public string publicationKey = string.Empty;
    public string plotDisplayName = string.Empty;
    public string itemDisplayName = string.Empty;
    public int locationX;
    public int locationY;
    public string reasonCode = string.Empty;
    public string destinationId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public float requiredWork;
    public float completedWork;
    public string commitId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public string requestFingerprint = string.Empty;
    public string physicalRequestFingerprint = string.Empty;
    public long physicalOutcomeOwnerRevision;
    public List<string> sourceStackIds = new();
    public string responsibleWorkerId = string.Empty;
    public string responsibleWorkerDisplayName = string.Empty;
    public List<CraftContributionSaveData> workContributions = new();
    public float waterBefore;
    public float waterAfter;
    public int completedAbsoluteDay;
    public string gameplayOutcomeId = string.Empty;
    public string gameplayOutcomeDigest = string.Empty;
    public string failureReason = string.Empty;

    public CropWaterRefillSaveData DeepClone() => new()
    {
        phase = phase,
        outcomeSchemaVersion = outcomeSchemaVersion,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        publicationKey = publicationKey,
        plotDisplayName = plotDisplayName,
        itemDisplayName = itemDisplayName,
        locationX = locationX,
        locationY = locationY,
        reasonCode = reasonCode ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        itemId = itemId ?? string.Empty,
        quantity = quantity,
        requiredWork = requiredWork,
        completedWork = completedWork,
        commitId = commitId ?? string.Empty,
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        requestFingerprint = requestFingerprint ?? string.Empty,
        physicalRequestFingerprint = physicalRequestFingerprint ?? string.Empty,
        physicalOutcomeOwnerRevision = physicalOutcomeOwnerRevision,
        sourceStackIds = new List<string>(
            sourceStackIds ?? new List<string>()),
        responsibleWorkerId = responsibleWorkerId ?? string.Empty,
        responsibleWorkerDisplayName = responsibleWorkerDisplayName
            ?? string.Empty,
        workContributions = (workContributions
                ?? new List<CraftContributionSaveData>())
            .ConvertAll(value => value?.Clone()),
        waterBefore = waterBefore,
        waterAfter = waterAfter,
        completedAbsoluteDay = completedAbsoluteDay,
        gameplayOutcomeId = gameplayOutcomeId ?? string.Empty,
        gameplayOutcomeDigest = gameplayOutcomeDigest ?? string.Empty,
        failureReason = failureReason ?? string.Empty
    };
}

[Serializable]
public sealed class CropSelectionCommandSaveData
{
    public CropCommandOutcomePhase phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string publicationKey = string.Empty;
    public string plotDisplayName = string.Empty;
    public string cropDisplayName = string.Empty;
    public int locationX;
    public int locationY;
    public string cropBefore = string.Empty;
    public string cropAfter = string.Empty;
    public float waterBefore;
    public float waterAfter;
    public int completedAbsoluteDay;
    public string gameplayOutcomeId = string.Empty;
    public string gameplayOutcomeDigest = string.Empty;
    public string failureReason = string.Empty;

    public bool IsEmpty => phase == CropCommandOutcomePhase.None
        && operationSequence == 0
        && string.IsNullOrEmpty(operationId)
        && string.IsNullOrEmpty(publicationKey)
        && string.IsNullOrEmpty(plotDisplayName)
        && string.IsNullOrEmpty(cropDisplayName)
        && locationX == 0 && locationY == 0
        && string.IsNullOrEmpty(cropBefore)
        && string.IsNullOrEmpty(cropAfter)
        && Mathf.Approximately(waterBefore, 0f)
        && Mathf.Approximately(waterAfter, 0f)
        && completedAbsoluteDay == 0
        && string.IsNullOrEmpty(gameplayOutcomeId)
        && string.IsNullOrEmpty(gameplayOutcomeDigest)
        && string.IsNullOrEmpty(failureReason);

    public CropSelectionCommandSaveData DeepClone() => new()
    {
        phase = phase,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        publicationKey = publicationKey,
        plotDisplayName = plotDisplayName,
        cropDisplayName = cropDisplayName,
        locationX = locationX,
        locationY = locationY,
        cropBefore = cropBefore ?? string.Empty,
        cropAfter = cropAfter ?? string.Empty,
        waterBefore = waterBefore,
        waterAfter = waterAfter,
        completedAbsoluteDay = completedAbsoluteDay,
        gameplayOutcomeId = gameplayOutcomeId ?? string.Empty,
        gameplayOutcomeDigest = gameplayOutcomeDigest ?? string.Empty,
        failureReason = failureReason ?? string.Empty
    };
}

[Serializable]
public sealed class CropGoldenHarvestScheduleSaveData
{
    public CropCommandOutcomePhase phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string publicationKey = string.Empty;
    public string plotDisplayName = string.Empty;
    public string cropDisplayName = string.Empty;
    public int locationX;
    public int locationY;
    public string cropId = string.Empty;
    public string harvesterId = string.Empty;
    public string harvesterDisplayName = string.Empty;
    public string harvesterBefore = string.Empty;
    public string harvesterAfter = string.Empty;
    public int attemptBefore;
    public int attemptAfter;
    public int completedAbsoluteDay;
    public string gameplayOutcomeId = string.Empty;
    public string gameplayOutcomeDigest = string.Empty;
    public string failureReason = string.Empty;

    public bool IsEmpty => phase == CropCommandOutcomePhase.None
        && operationSequence == 0
        && string.IsNullOrEmpty(operationId)
        && string.IsNullOrEmpty(publicationKey)
        && string.IsNullOrEmpty(plotDisplayName)
        && string.IsNullOrEmpty(cropDisplayName)
        && locationX == 0 && locationY == 0
        && string.IsNullOrEmpty(cropId)
        && string.IsNullOrEmpty(harvesterId)
        && string.IsNullOrEmpty(harvesterDisplayName)
        && string.IsNullOrEmpty(harvesterBefore)
        && string.IsNullOrEmpty(harvesterAfter)
        && attemptBefore == 0
        && attemptAfter == 0
        && completedAbsoluteDay == 0
        && string.IsNullOrEmpty(gameplayOutcomeId)
        && string.IsNullOrEmpty(gameplayOutcomeDigest)
        && string.IsNullOrEmpty(failureReason);

    public CropGoldenHarvestScheduleSaveData DeepClone() => new()
    {
        phase = phase,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        publicationKey = publicationKey,
        plotDisplayName = plotDisplayName,
        cropDisplayName = cropDisplayName,
        locationX = locationX,
        locationY = locationY,
        cropId = cropId ?? string.Empty,
        harvesterId = harvesterId ?? string.Empty,
        harvesterDisplayName = harvesterDisplayName ?? string.Empty,
        harvesterBefore = harvesterBefore ?? string.Empty,
        harvesterAfter = harvesterAfter ?? string.Empty,
        attemptBefore = attemptBefore,
        attemptAfter = attemptAfter,
        completedAbsoluteDay = completedAbsoluteDay,
        gameplayOutcomeId = gameplayOutcomeId ?? string.Empty,
        gameplayOutcomeDigest = gameplayOutcomeDigest ?? string.Empty,
        failureReason = failureReason ?? string.Empty
    };
}

[Serializable]
public sealed class CropCycleInputRequirementSaveData
{
    public string itemId = string.Empty;
    public int quantity;

    public CropCycleInputRequirementSaveData DeepClone() => new()
    {
        itemId = itemId ?? string.Empty,
        quantity = quantity
    };
}

[Serializable]
public enum CropCycleExecutionReceiptStatus
{
    None = 0,
    Active = 1,
    Completed = 2,
    FailedCropDeath = 3,
    FailedPlotDestroyed = 4
}

[Serializable]
public sealed class CropCycleExecutionReceiptSaveData
{
    public const int CurrentSchemaVersion = 4;

    public int schemaVersion = CurrentSchemaVersion;
    public string plotId = string.Empty;
    public string cropId = string.Empty;
    public string correlationId = string.Empty;
    public bool explicitCorrelation;
    public CropCycleExecutionReceiptStatus status;
    public string terminalReasonCode = string.Empty;
    public bool indoor;
    public int sowOperationSequence;
    public string sowOperationId = string.Empty;
    public string sowCommitId = string.Empty;
    public string sowRequestFingerprint = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public string inputVectorDigest = string.Empty;
    public SeedLotState inputSeedLot;
    public List<CropPhysicalInputSaveData> inputs = new();
    public List<CraftContributionSaveData> sowContributions = new();
    public float waterBefore;
    public float waterAfter;
    public List<string> irrigationChildResultKeys = new();
    public int harvestOperationSequence;
    public string harvestOperationId = string.Empty;
    public string outputBatchCommitId = string.Empty;
    public string outputOutcomeFingerprint = string.Empty;
    public string plannedOutputFingerprint = string.Empty;
    public ProductionOutputCapabilitySaveData harvestCapability = new();
    public ProductionOutputCapabilitySaveData seedCapability = new();
    public long outputMassGrams;
    public string outputVectorDigest = string.Empty;
    public SeedLotState returnedSeedLot;
    public List<ProductionDomainPublishedStackSaveData> outputs = new();
    public List<CraftContributionSaveData> harvestContributions = new();
    public string harvesterId = string.Empty;
    public string ecologyBeforeFingerprint = string.Empty;
    public string ecologyAfterFingerprint = string.Empty;
    public bool goldenPrepared;
    public string goldenTraitDefinitionId = string.Empty;
    public string goldenOutcomeFingerprint = string.Empty;
    public ExtremeRiskOutcome goldenOutcome;
    public float goldenPrimaryMultiplier;
    public float goldenSecondaryMultiplier;
    public ulong goldenRollHash;
    public bool completed;
    public string sourceDigest = string.Empty;

    public bool IsEmpty => string.IsNullOrEmpty(plotId)
        && string.IsNullOrEmpty(cropId)
        && string.IsNullOrEmpty(correlationId)
        && !explicitCorrelation
        && status == CropCycleExecutionReceiptStatus.None
        && string.IsNullOrEmpty(terminalReasonCode)
        && !indoor
        && sowOperationSequence == 0
        && string.IsNullOrEmpty(sowOperationId)
        && string.IsNullOrEmpty(sowCommitId)
        && string.IsNullOrEmpty(sowRequestFingerprint)
        && inputQuantity == 0
        && inputMassGrams == 0L
        && string.IsNullOrEmpty(inputVectorDigest)
        && IsSemanticEmptySeedLot(inputSeedLot)
        && string.IsNullOrEmpty(sourceDigest)
        && (inputs == null || inputs.Count == 0)
        && (sowContributions == null || sowContributions.Count == 0)
        && Mathf.Approximately(waterBefore, 0f)
        && Mathf.Approximately(waterAfter, 0f)
        && (irrigationChildResultKeys == null
            || irrigationChildResultKeys.Count == 0)
        && harvestOperationSequence == 0
        && string.IsNullOrEmpty(harvestOperationId)
        && string.IsNullOrEmpty(outputBatchCommitId)
        && string.IsNullOrEmpty(outputOutcomeFingerprint)
        && string.IsNullOrEmpty(plannedOutputFingerprint)
        && (harvestCapability == null || harvestCapability.IsEmpty)
        && (seedCapability == null || seedCapability.IsEmpty)
        && outputMassGrams == 0L
        && string.IsNullOrEmpty(outputVectorDigest)
        && IsSemanticEmptySeedLot(returnedSeedLot)
        && (outputs == null || outputs.Count == 0)
        && (harvestContributions == null || harvestContributions.Count == 0)
        && string.IsNullOrEmpty(harvesterId)
        && string.IsNullOrEmpty(ecologyBeforeFingerprint)
        && string.IsNullOrEmpty(ecologyAfterFingerprint)
        && !goldenPrepared
        && string.IsNullOrEmpty(goldenTraitDefinitionId)
        && string.IsNullOrEmpty(goldenOutcomeFingerprint)
        && goldenOutcome == default
        && Mathf.Approximately(goldenPrimaryMultiplier, 0f)
        && Mathf.Approximately(goldenSecondaryMultiplier, 0f)
        && goldenRollHash == 0UL
        && !completed;

    public CropCycleExecutionReceiptSaveData DeepClone() => new()
    {
        schemaVersion = schemaVersion,
        plotId = plotId ?? string.Empty,
        cropId = cropId ?? string.Empty,
        correlationId = correlationId ?? string.Empty,
        explicitCorrelation = explicitCorrelation,
        status = status,
        terminalReasonCode = terminalReasonCode ?? string.Empty,
        indoor = indoor,
        sowOperationSequence = sowOperationSequence,
        sowOperationId = sowOperationId ?? string.Empty,
        sowCommitId = sowCommitId ?? string.Empty,
        sowRequestFingerprint = sowRequestFingerprint ?? string.Empty,
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        inputVectorDigest = inputVectorDigest ?? string.Empty,
        inputSeedLot = inputSeedLot?.Clone(),
        inputs = (inputs ?? new List<CropPhysicalInputSaveData>())
            .ConvertAll(value => value?.DeepClone()),
        sowContributions = (sowContributions
                ?? new List<CraftContributionSaveData>())
            .ConvertAll(value => value?.Clone()),
        waterBefore = waterBefore,
        waterAfter = waterAfter,
        irrigationChildResultKeys = new List<string>(
            irrigationChildResultKeys ?? new List<string>()),
        harvestOperationSequence = harvestOperationSequence,
        harvestOperationId = harvestOperationId ?? string.Empty,
        outputBatchCommitId = outputBatchCommitId ?? string.Empty,
        outputOutcomeFingerprint = outputOutcomeFingerprint ?? string.Empty,
        plannedOutputFingerprint = plannedOutputFingerprint ?? string.Empty,
        harvestCapability = harvestCapability?.Clone()
            ?? new ProductionOutputCapabilitySaveData(),
        seedCapability = seedCapability?.Clone()
            ?? new ProductionOutputCapabilitySaveData(),
        outputMassGrams = outputMassGrams,
        outputVectorDigest = outputVectorDigest ?? string.Empty,
        returnedSeedLot = returnedSeedLot?.Clone(),
        outputs = (outputs ?? new List<ProductionDomainPublishedStackSaveData>())
            .ConvertAll(value => value?.Clone()),
        harvestContributions = (harvestContributions
                ?? new List<CraftContributionSaveData>())
            .ConvertAll(value => value?.Clone()),
        harvesterId = harvesterId ?? string.Empty,
        ecologyBeforeFingerprint = ecologyBeforeFingerprint ?? string.Empty,
        ecologyAfterFingerprint = ecologyAfterFingerprint ?? string.Empty,
        goldenPrepared = goldenPrepared,
        goldenTraitDefinitionId = goldenTraitDefinitionId ?? string.Empty,
        goldenOutcomeFingerprint = goldenOutcomeFingerprint ?? string.Empty,
        goldenOutcome = goldenOutcome,
        goldenPrimaryMultiplier = goldenPrimaryMultiplier,
        goldenSecondaryMultiplier = goldenSecondaryMultiplier,
        goldenRollHash = goldenRollHash,
        completed = completed,
        sourceDigest = sourceDigest ?? string.Empty
    };

    public static bool IsSemanticEmptySeedLot(SeedLotState value) =>
        value == null
        || string.IsNullOrEmpty(value.cropId)
            && string.IsNullOrEmpty(value.cultivarGenomeId)
            && value.generation == 0
            && Mathf.Approximately(value.pathogenLoad, 0f);
}

public enum CropHarvestOutputPhase
{
    None = 0,
    Frozen = 1,
    OutputCommitted = 2,
    OutputRestoredAwaitingFinalization = 3
}

[Serializable]
public sealed class CropHarvestOutputSaveData
{
    public CropHarvestOutputPhase phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string cropId = string.Empty;
    public bool indoor;
    public string harvesterId = string.Empty;
    public string outcomeId = string.Empty;
    public string ecologyOutcomeFingerprint = string.Empty;
    public string ecologyBeforeFingerprint = string.Empty;
    public string ecologyAfterFingerprint = string.Empty;
    public bool ecologyCommitted;
    public bool ecologyAcknowledged;
    public bool goldenPrepared;
    public string goldenTraitDefinitionId = string.Empty;
    public string goldenOutcomeFingerprint = string.Empty;
    public ExtremeRiskOutcome goldenOutcome;
    public float goldenPrimaryMultiplier;
    public float goldenSecondaryMultiplier;
    public ulong goldenRollHash;
    public bool goldenCommitted;
    public bool goldenAcknowledged;
    public bool cropOutcomeAcknowledged;
    public CropCycleExecutionReceiptSaveData terminalReceipt = new();
    public string terminalPlotDisplayName = string.Empty;
    public string terminalCropDisplayName = string.Empty;
    public string terminalPublicationKey = string.Empty;
    public string terminalPublicationId = string.Empty;
    public string terminalPublicationFingerprint = string.Empty;
    public bool completionEventPublished;
    public string completionPublicationKey = string.Empty;
    public string completionDeliveryId = string.Empty;
    public string completionDeliveryFingerprint = string.Empty;
    public int completionAbsoluteDay;
    public string harvestItemId = string.Empty;
    public int harvestQuantity;
    public string seedItemId = string.Empty;
    public int seedQuantity;
    public SeedLotState returnedSeedLot;
    public int maximumHarvestQuantity;
    public int maximumSeedQuantity;
    public string seasonalDamageSourceEventInstanceId = string.Empty;
    public string seasonalDamageSourceDefinitionId = string.Empty;
    public int seasonalPrimaryBatchLossPercent;
    public int primaryQuantityBeforeSeasonalLoss;
    public int seasonalPrimaryLossQuantity;
    public ProductionOutputCapabilitySaveData harvestCapability = new();
    public ProductionOutputCapabilitySaveData seedCapability = new();
    public ProductionDomainOutputPublicationSaveData outputPublication = new();

    public CropHarvestOutputSaveData DeepClone() => new()
    {
        phase = phase,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        cropId = cropId ?? string.Empty,
        indoor = indoor,
        harvesterId = harvesterId ?? string.Empty,
        outcomeId = outcomeId ?? string.Empty,
        ecologyOutcomeFingerprint = ecologyOutcomeFingerprint ?? string.Empty,
        ecologyBeforeFingerprint = ecologyBeforeFingerprint ?? string.Empty,
        ecologyAfterFingerprint = ecologyAfterFingerprint ?? string.Empty,
        ecologyCommitted = ecologyCommitted,
        ecologyAcknowledged = ecologyAcknowledged,
        goldenPrepared = goldenPrepared,
        goldenTraitDefinitionId = goldenTraitDefinitionId ?? string.Empty,
        goldenOutcomeFingerprint = goldenOutcomeFingerprint ?? string.Empty,
        goldenOutcome = goldenOutcome,
        goldenPrimaryMultiplier = goldenPrimaryMultiplier,
        goldenSecondaryMultiplier = goldenSecondaryMultiplier,
        goldenRollHash = goldenRollHash,
        goldenCommitted = goldenCommitted,
        goldenAcknowledged = goldenAcknowledged,
        cropOutcomeAcknowledged = cropOutcomeAcknowledged,
        terminalReceipt = terminalReceipt?.DeepClone(),
        terminalPlotDisplayName = terminalPlotDisplayName,
        terminalCropDisplayName = terminalCropDisplayName,
        terminalPublicationKey = terminalPublicationKey,
        terminalPublicationId = terminalPublicationId,
        terminalPublicationFingerprint = terminalPublicationFingerprint,
        completionEventPublished = completionEventPublished,
        completionPublicationKey = completionPublicationKey,
        completionDeliveryId = completionDeliveryId ?? string.Empty,
        completionDeliveryFingerprint = completionDeliveryFingerprint
            ?? string.Empty,
        completionAbsoluteDay = completionAbsoluteDay,
        harvestItemId = harvestItemId ?? string.Empty,
        harvestQuantity = harvestQuantity,
        seedItemId = seedItemId ?? string.Empty,
        seedQuantity = seedQuantity,
        returnedSeedLot = returnedSeedLot?.Clone(),
        maximumHarvestQuantity = maximumHarvestQuantity,
        maximumSeedQuantity = maximumSeedQuantity,
        seasonalDamageSourceEventInstanceId =
            seasonalDamageSourceEventInstanceId ?? string.Empty,
        seasonalDamageSourceDefinitionId =
            seasonalDamageSourceDefinitionId ?? string.Empty,
        seasonalPrimaryBatchLossPercent = seasonalPrimaryBatchLossPercent,
        primaryQuantityBeforeSeasonalLoss =
            primaryQuantityBeforeSeasonalLoss,
        seasonalPrimaryLossQuantity = seasonalPrimaryLossQuantity,
        harvestCapability = harvestCapability?.Clone()
            ?? new ProductionOutputCapabilitySaveData(),
        seedCapability = seedCapability?.Clone()
            ?? new ProductionOutputCapabilitySaveData(),
        outputPublication = outputPublication?.Clone()
            ?? new ProductionDomainOutputPublicationSaveData()
    };
}

public enum CropTreatmentOrderPhase
{
    None = 0,
    WaitingForDelivery = 1,
    ReadyForWork = 2,
    Working = 3,
    InputCommitted = 4,
    // Kept at value 5 for save compatibility. This phase means that the
    // ecology/cooldown mutation is frozen and the gameplay ledger row is next.
    OutcomePublished = 5,
    PlotDestroyedLossPending = 6,
    LedgerCommitted = 7
}

public enum CropTreatmentTerminalDisposition
{
    None = 0,
    DestroyedWithPlotLoss = 1
}

[Serializable]
public sealed class CropTreatmentOrderSaveData
{
    public const int CurrentOutcomeSchemaVersion = 2;

    public CropTreatmentOrderPhase phase;
    public int outcomeSchemaVersion;
    public int operationSequence;
    public string operationId = string.Empty;
    public string publicationKey = string.Empty;
    public string plotDisplayName = string.Empty;
    public string itemDisplayName = string.Empty;
    public int locationX;
    public int locationY;
    public string reasonCode = string.Empty;
    public string destinationId = string.Empty;
    public string itemId = string.Empty;
    public CropTreatmentKind treatmentKind;
    public int quantity;
    public float requiredWork;
    public float completedWork;
    public float effectAmount;
    public int cooldownDays;
    public int scheduledAbsoluteDay;
    public string failureReason = string.Empty;
    public List<string> sourceStackIds = new();
    public long inputMassGrams;
    public string commitId = string.Empty;
    public string requestFingerprint = string.Empty;
    public int tareOutputQuantity;
    public long tareOutputMassGrams;
    public long destroyedTareMassGrams;
    public List<string> tareOutputCommitIds = new();
    public string ecologyBeforeFingerprint = string.Empty;
    public string ecologyAfterFingerprint = string.Empty;
    public string responsibleWorkerId = string.Empty;
    public string responsibleWorkerDisplayName = string.Empty;
    public List<CraftContributionSaveData> workContributions = new();
    public float pestPressureBefore;
    public float pestPressureAfter;
    public float diseasePressureBefore;
    public float diseasePressureAfter;
    public int cooldownBeforeDay;
    public int cooldownAfterDay;
    public int completedAbsoluteDay;
    public string gameplayOutcomeId = string.Empty;
    public string gameplayOutcomeDigest = string.Empty;
    public CropTreatmentTerminalDisposition terminalDisposition;
    public string terminalReasonCode = string.Empty;
    public int terminalLossQuantity;
    public long terminalLossMassGrams;

    public CropTreatmentOrderSaveData DeepClone() => new()
    {
        phase = phase,
        outcomeSchemaVersion = outcomeSchemaVersion,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        publicationKey = publicationKey,
        plotDisplayName = plotDisplayName,
        itemDisplayName = itemDisplayName,
        locationX = locationX,
        locationY = locationY,
        reasonCode = reasonCode ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        itemId = itemId ?? string.Empty,
        treatmentKind = treatmentKind,
        quantity = quantity,
        requiredWork = requiredWork,
        completedWork = completedWork,
        effectAmount = effectAmount,
        cooldownDays = cooldownDays,
        scheduledAbsoluteDay = scheduledAbsoluteDay,
        failureReason = failureReason ?? string.Empty,
        sourceStackIds = new List<string>(sourceStackIds ?? new List<string>()),
        inputMassGrams = inputMassGrams,
        commitId = commitId ?? string.Empty,
        requestFingerprint = requestFingerprint ?? string.Empty,
        tareOutputQuantity = tareOutputQuantity,
        tareOutputMassGrams = tareOutputMassGrams,
        destroyedTareMassGrams = destroyedTareMassGrams,
        tareOutputCommitIds = new List<string>(
            tareOutputCommitIds ?? new List<string>()),
        ecologyBeforeFingerprint = ecologyBeforeFingerprint ?? string.Empty,
        ecologyAfterFingerprint = ecologyAfterFingerprint ?? string.Empty,
        responsibleWorkerId = responsibleWorkerId ?? string.Empty,
        responsibleWorkerDisplayName = responsibleWorkerDisplayName
            ?? string.Empty,
        workContributions = (workContributions
                ?? new List<CraftContributionSaveData>())
            .ConvertAll(value => value?.Clone()),
        pestPressureBefore = pestPressureBefore,
        pestPressureAfter = pestPressureAfter,
        diseasePressureBefore = diseasePressureBefore,
        diseasePressureAfter = diseasePressureAfter,
        cooldownBeforeDay = cooldownBeforeDay,
        cooldownAfterDay = cooldownAfterDay,
        completedAbsoluteDay = completedAbsoluteDay,
        gameplayOutcomeId = gameplayOutcomeId ?? string.Empty,
        gameplayOutcomeDigest = gameplayOutcomeDigest ?? string.Empty,
        terminalDisposition = terminalDisposition,
        terminalReasonCode = terminalReasonCode ?? string.Empty,
        terminalLossQuantity = terminalLossQuantity,
        terminalLossMassGrams = terminalLossMassGrams
    };
}

public enum CropPhysicalCommitPhase
{
    None = 0,
    InputCommitted = 1,
    OutcomePublished = 2,
    PlotDestroyedLossPending = 3,
    FacilityDestroyedLossPending = 4
}

public enum CropWipTerminalDisposition
{
    None = 0,
    DestroyedWithPlotLoss = 1,
    DestroyedWithFacilityLoss = 2
}

[Serializable]
public sealed class CropPhysicalInputSaveData
{
    public string itemId = string.Empty;
    public string sourceStackId = string.Empty;
    public int quantity;

    public CropPhysicalInputSaveData DeepClone() => new()
    {
        itemId = itemId ?? string.Empty,
        sourceStackId = sourceStackId ?? string.Empty,
        quantity = quantity
    };
}

/// <summary>
/// Domain-owned half of a pending physical input disposition. The matching
/// item receipt remains unacknowledged until the crop outcome is published.
/// </summary>
[Serializable]
public sealed class CropPhysicalCommitSaveData
{
    public CropPhysicalCommitPhase phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string destinationId = string.Empty;
    public string cropId = string.Empty;
    public string seedItemId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public string commitId = string.Empty;
    public string requestFingerprint = string.Empty;
    public bool hasSeedLot;
    public SeedLotState seedLot;
    public string ecologyBeforeFingerprint = string.Empty;
    public string ecologyAfterFingerprint = string.Empty;
    public CropWipTerminalDisposition terminalDisposition;
    public string terminalOperationId = string.Empty;
    public string terminalReasonCode = string.Empty;
    public int terminalLossQuantity;
    public long terminalLossMassGrams;
    public List<CropPhysicalInputSaveData> inputs = new();

    public CropPhysicalCommitSaveData DeepClone() => new()
    {
        phase = phase,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        reasonCode = reasonCode ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        cropId = cropId ?? string.Empty,
        seedItemId = seedItemId ?? string.Empty,
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        commitId = commitId ?? string.Empty,
        requestFingerprint = requestFingerprint ?? string.Empty,
        hasSeedLot = hasSeedLot,
        seedLot = hasSeedLot ? seedLot?.Clone() : null,
        ecologyBeforeFingerprint = ecologyBeforeFingerprint ?? string.Empty,
        ecologyAfterFingerprint = ecologyAfterFingerprint ?? string.Empty,
        terminalDisposition = terminalDisposition,
        terminalOperationId = terminalOperationId ?? string.Empty,
        terminalReasonCode = terminalReasonCode ?? string.Empty,
        terminalLossQuantity = terminalLossQuantity,
        terminalLossMassGrams = terminalLossMassGrams,
        inputs = (inputs ?? new List<CropPhysicalInputSaveData>())
            .ConvertAll(value => value?.DeepClone())
    };
}

[Serializable]
public sealed class DungeonCropPlotSaveData
{
    public const int CurrentVersion = 21;

    public int version = CurrentVersion;
    public List<CropPlotSaveData> plots = new List<CropPlotSaveData>();
    public List<string> handledSeasonalStartInstanceIds = new List<string>();
}

public interface ICropPlotRuntime
{
    int Version { get; }
    IReadOnlyList<CropPlotSnapshot> Plots { get; }
    void CopyVisualStates(List<CropPlotVisualState> destination);
    bool TrySetCrop(
        BuildableObject plot,
        string cropId,
        out string message);
    bool CanScheduleTreatment(
        BuildableObject plot,
        string treatmentItemId,
        out string reason);
    bool TryScheduleTreatment(
        BuildableObject plot,
        string treatmentItemId,
        out string message);
    bool TryCancelTreatment(
        BuildableObject plot,
        out string message);
    bool TryGetWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        out CropPlotWorkSnapshot snapshot);
    bool ApplyWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        float amount,
        out bool cycleCompleted);
    bool ApplyWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        float amount,
        CharacterActor worker,
        out bool cycleCompleted);
    bool TryScheduleGoldenHarvest(
        BuildableObject plot,
        CharacterActor harvester,
        out string failureReason);
    bool IsGoldenHarvestWorkerEligible(
        BuildableObject plot,
        CharacterActor harvester,
        out string failureReason);
    bool TryGetGoldenHarvestDelay(
        BuildableObject plot,
        CharacterActor harvester,
        out float remainingSeconds);
}

public sealed class CropPlotRestoreCandidate
{
    internal CropPlotRestoreCandidate(
        CropPlotAggregateState state,
        IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement>
            outputAcknowledgements = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        OutputAcknowledgements = outputAcknowledgements
            ?? Array.Empty<ProductionDomainOutputRestoreAcknowledgement>();
    }

    internal CropPlotAggregateState State { get; }
    internal IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement>
        OutputAcknowledgements { get; }
}

public interface ICropPlotPersistence
{
    DungeonCropPlotSaveData Capture();
    CropPlotRestoreCandidate BuildRestore(DungeonCropPlotSaveData snapshot);
    void Restore(CropPlotRestoreCandidate candidate);
}
