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

public readonly struct CropPlotWorkSnapshot
{
    public CropPlotWorkSnapshot(
        string plotId,
        WorkTypeId workTypeId,
        string displayName,
        float requiredWork,
        float completedWork,
        bool available,
        string unavailableReason)
    {
        PlotId = plotId ?? string.Empty;
        WorkTypeId = workTypeId;
        DisplayName = displayName ?? string.Empty;
        RequiredWork = Mathf.Max(0.1f, requiredWork);
        CompletedWork = Mathf.Clamp(completedWork, 0f, RequiredWork);
        Available = available;
        UnavailableReason = unavailableReason ?? string.Empty;
    }

    public string PlotId { get; }
    public WorkTypeId WorkTypeId { get; }
    public string DisplayName { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public bool Available { get; }
    public string UnavailableReason { get; }
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
    public string BlockedReason { get; set; } = string.Empty;
    public string GoldenHarvestHarvesterId { get; set; } = string.Empty;
    public int GoldenHarvestAttemptSequence { get; set; }
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
    public string cropId = string.Empty;
    public CropPlotPhase phase;
    public float sowWork;
    public float growthHours;
    public float harvestWork;
    public bool materialsConsumed;
    public string goldenHarvestHarvesterId = string.Empty;
    public int goldenHarvestAttemptSequence;
}

[Serializable]
public sealed class DungeonCropPlotSaveData
{
    public const int CurrentVersion = 3;

    public int version = CurrentVersion;
    public List<CropPlotSaveData> plots = new List<CropPlotSaveData>();
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
    internal CropPlotRestoreCandidate(CropPlotAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CropPlotAggregateState State { get; }
}

public interface ICropPlotPersistence
{
    DungeonCropPlotSaveData Capture();
    CropPlotRestoreCandidate BuildRestore(DungeonCropPlotSaveData snapshot);
    void Restore(CropPlotRestoreCandidate candidate);
}
