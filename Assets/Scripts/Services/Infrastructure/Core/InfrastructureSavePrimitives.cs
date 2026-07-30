using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IDungeonGameSaveService
{
    DungeonGameSaveData Capture();
    string ToJson(DungeonGameSaveData saveData, bool prettyPrint = false);
    DungeonGameSaveData FromJson(string json);
    bool TryRestore(DungeonGameSaveData saveData, out DungeonGameRestoreReport report);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IDungeonGameSaveSlotService
{
    string Save(string slotId, bool prettyPrint = false);
    bool TryLoad(string slotId, out DungeonGameRestoreReport report);
    bool HasSave(string slotId);
    IReadOnlyList<DungeonSaveSlotInfo> GetSlots();
    bool Delete(string slotId);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IDungeonSaveSlotCatalog
{
    bool HasSave(string slotId);
    IReadOnlyList<DungeonSaveSlotInfo> GetSlots();
    bool Delete(string slotId);
    string GetPath(string slotId);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonGameSaveData
{
    public const int CurrentVersion = 17;

    public int version = CurrentVersion;
    public string savedAtUtc = string.Empty;
    public string sceneName = string.Empty;
    public List<DungeonSaveSectionEnvelope> sections = new List<DungeonSaveSectionEnvelope>();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonSaveSlotInfo
{
    public DungeonSaveSlotInfo(
        string slotId,
        string path,
        string savedAtUtc = "",
        string sceneName = "",
        int day = 1,
        int money = 0,
        bool isValid = false,
        bool debugModified = false)
    {
        SlotId = slotId ?? string.Empty;
        Path = path ?? string.Empty;
        SavedAtUtc = savedAtUtc ?? string.Empty;
        SceneName = sceneName ?? string.Empty;
        Day = day;
        Money = money;
        IsValid = isValid;
        DebugModified = debugModified;
    }

    public string SlotId { get; }
    public string Path { get; }
    public string SavedAtUtc { get; }
    public string SceneName { get; }
    public int Day { get; }
    public int Money { get; }
    public bool IsValid { get; }
    public bool DebugModified { get; }
}
