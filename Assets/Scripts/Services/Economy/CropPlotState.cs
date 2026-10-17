using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

internal sealed class CropPlotState
{
    public BuildingInstanceId PlotId;
    public BuildableObject Building;
    public BuildingCropPlotAbility Ability;
    public string PlotDisplayName = string.Empty;
    public string RetirementPublicationKey = string.Empty;
    public Vector2Int LastKnownPosition;
    public string CropId = string.Empty;
    public int NextCropSelectionOperationSequence;
    public CropSelectionCommandSaveData CropSelectionOutcome = new();
    public CropPlotPhase Phase;
    public float SowWork;
    public float GrowthHours;
    public float HarvestWork;
    public string MaterialDestinationId = string.Empty;
    public bool MaterialsConsumed;
    public int FrozenSowInputOperationSequence = -1;
    public string FrozenSowInputSourceDigest = string.Empty;
    public string FrozenSowInputVectorDigest = string.Empty;
    public SurvivalWeatherType FrozenSowInputWeather;
    public float FrozenSowInputConsumptionMultiplier;
    public string FrozenSowInputSelectedFuelItemId = string.Empty;
    public Dictionary<string, int> FrozenSowInputs = new(StringComparer.Ordinal);
    public string BlockedReason = string.Empty;
    public string GoldenHarvestHarvesterId = string.Empty;
    public int GoldenHarvestAttemptSequence;
    public CropGoldenHarvestScheduleSaveData GoldenHarvestScheduleOutcome = new();
    public int NextSowOperationSequence;
    public CropPhysicalCommitSaveData PendingSow = new();
    public string PendingCycleCorrelationId = string.Empty;
    public CropCycleExecutionReceiptSaveData CycleExecutionReceipt = new();
    public CropFailedOutcomeSaveData FailedOutcome = new();
    public float CurrentWater;
    public float WaterCapacity = CropWaterRules.WaterCapacity;
    public int NextWaterRefillOperationSequence;
    public CropIrrigationPublicationSaveData IrrigationPublication = new();
    public CropWaterRefillSaveData WaterRefill = new();
    public int NextTreatmentOperationSequence;
    public int PestLureNextAllowedDay;
    public int BotanicalPesticideNextAllowedDay;
    public int FungicideNextAllowedDay;
    public CropTreatmentOrderSaveData Treatment = new();
    public int NextHarvestOperationSequence;
    public CropHarvestOutputSaveData PendingHarvest = new();
    public CropSeasonalYieldDamageSaveData SeasonalYieldDamage = new();
}

internal sealed class CropPlotAggregateState
{
    internal Dictionary<BuildingInstanceId, CropPlotState> States { get; } =
        new();
    internal Dictionary<BuildableObject, CropPlotState> StatesByBuilding { get; } =
        new();
    internal List<CropPlotSnapshot> Snapshots { get; } = new();
    internal HashSet<string> HandledSeasonalStartInstanceIds { get; } =
        new(StringComparer.Ordinal);
    internal int ObservedBuildingVersion { get; set; } = -1;
    internal float NextMaterialRequestTime { get; set; }
    internal bool SnapshotsDirty { get; set; } = true;
    internal int Version { get; set; }
}
