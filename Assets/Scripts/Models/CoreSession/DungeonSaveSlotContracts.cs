public readonly struct DungeonSaveSlotLocation
{
    public DungeonSaveSlotLocation(string slotId, string path)
    {
        SlotId = slotId ?? string.Empty;
        Path = path ?? string.Empty;
    }

    public string SlotId { get; }
    public string Path { get; }
}

public readonly struct DungeonSaveSlotSummary
{
    public DungeonSaveSlotSummary(
        string savedAtUtc,
        string sceneName,
        int day,
        int money,
        bool debugModified,
        int survivalPressureValue)
    {
        SavedAtUtc = savedAtUtc ?? string.Empty;
        SceneName = sceneName ?? string.Empty;
        Day = day;
        Money = money;
        DebugModified = debugModified;
        SurvivalPressureValue = survivalPressureValue;
    }

    public string SavedAtUtc { get; }
    public string SceneName { get; }
    public int Day { get; }
    public int Money { get; }
    public bool DebugModified { get; }
    public int SurvivalPressureValue { get; }
}

public readonly struct DungeonSaveSlotCompatibility
{
    public DungeonSaveSlotCompatibility(
        bool isValid,
        string incompatibilityReason)
    {
        IsValid = isValid;
        IncompatibilityReason = incompatibilityReason?.Trim() ?? string.Empty;
    }

    public bool IsValid { get; }
    public string IncompatibilityReason { get; }
}

public sealed class DungeonSaveSlotInfo
{
    public DungeonSaveSlotInfo(string slotId, string path)
        : this(
            new DungeonSaveSlotLocation(slotId, path),
            new DungeonSaveSlotSummary(
                string.Empty,
                string.Empty,
                day: 1,
                money: 0,
                debugModified: false,
                survivalPressureValue: 0),
            new DungeonSaveSlotCompatibility(
                isValid: false,
                incompatibilityReason: string.Empty))
    {
    }

    public DungeonSaveSlotInfo(
        DungeonSaveSlotLocation location,
        DungeonSaveSlotSummary summary,
        DungeonSaveSlotCompatibility compatibility)
    {
        SlotId = location.SlotId;
        Path = location.Path;
        SavedAtUtc = summary.SavedAtUtc;
        SceneName = summary.SceneName;
        Day = summary.Day;
        Money = summary.Money;
        IsValid = compatibility.IsValid;
        DebugModified = summary.DebugModified;
        SurvivalPressureValue = summary.SurvivalPressureValue;
        IncompatibilityReason = compatibility.IncompatibilityReason;
    }

    public string SlotId { get; }
    public string Path { get; }
    public string SavedAtUtc { get; }
    public string SceneName { get; }
    public int Day { get; }
    public int Money { get; }
    public bool IsValid { get; }
    public bool DebugModified { get; }
    public int SurvivalPressureValue { get; }
    public string IncompatibilityReason { get; }
}
