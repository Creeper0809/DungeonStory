using System;
using System.Collections.Generic;

public enum CodexEntryCategory
{
    Monster,
    Invasion,
    Facility
}

public enum CodexInfoSource
{
    System,
    Observation,
    Research,
    Synthesis,
    Evolution
}

[Serializable]
public sealed class DungeonCodexSaveData
{
    public List<DungeonCodexEntrySaveData> entries =
        new List<DungeonCodexEntrySaveData>();
}

[Serializable]
public sealed class DungeonCodexEntrySaveData
{
    public CodexEntryCategory category;
    public string entryId = string.Empty;
    public string title = string.Empty;
    public List<DungeonCodexLineSaveData> lines =
        new List<DungeonCodexLineSaveData>();
}

[Serializable]
public sealed class DungeonCodexLineSaveData
{
    public string text = string.Empty;
    public CodexInfoSource source;
}
