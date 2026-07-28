using System;
using System.Collections.Generic;
using UnityEngine;

public enum WasteDispositionKind
{
    Store = 0,
    DirectFeed = 1,
    Compost = 2,
    Fuel = 3,
    Alchemy = 4,
    Incinerate = 5
}

[Serializable]
public sealed class WastePolicyData
{
    public WasteOriginKind origin;
    public WasteDispositionKind disposition;
    public bool enabled = true;
    [Range(0f, 100f)] public float maximumFeedContamination = 79f;

    public WastePolicyData Clone()
    {
        return (WastePolicyData)MemberwiseClone();
    }
}

public readonly struct WasteFeedResult
{
    public WasteFeedResult(
        bool succeeded,
        string itemId,
        WasteOriginKind origin,
        float contamination,
        float nutrition,
        float diseaseChance,
        string message)
    {
        Succeeded = succeeded;
        ItemId = itemId ?? string.Empty;
        Origin = origin;
        Contamination = Mathf.Clamp(contamination, 0f, 100f);
        Nutrition = Mathf.Clamp01(nutrition);
        DiseaseChance = Mathf.Clamp01(diseaseChance);
        Message = message ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string ItemId { get; }
    public WasteOriginKind Origin { get; }
    public float Contamination { get; }
    public float Nutrition { get; }
    public float DiseaseChance { get; }
    public string Message { get; }
}

public sealed class WasteProcessingOverview
{
    public int PlantWaste { get; set; }
    public int AnimalWaste { get; set; }
    public int MixedWaste { get; set; }
    public int ForbiddenWaste { get; set; }
    public int ToxicWaste { get; set; }
    public int ProcessingBills { get; set; }
}

[Serializable]
public sealed class DungeonWasteProcessingSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<WastePolicyData> policies = new List<WastePolicyData>();
}

public interface IWasteProcessingRuntime
{
    int Version { get; }
    IReadOnlyList<WastePolicyData> Policies { get; }
    WastePolicyData GetPolicy(WasteOriginKind origin);
    bool SetPolicy(WastePolicyData policy, out string failureReason);
    WasteProcessingOverview CaptureOverview();
    bool TryRequestDirectFeed(
        WildlifeDietType diet,
        Vector2Int destinationPosition,
        string destinationId,
        out string itemId,
        out string failureReason);
    bool TryConsumeDirectFeed(
        WildlifeDietType diet,
        string destinationId,
        out WasteFeedResult result);
    DungeonWasteProcessingSaveData Capture();
    void Restore(DungeonWasteProcessingSaveData saveData);
}
