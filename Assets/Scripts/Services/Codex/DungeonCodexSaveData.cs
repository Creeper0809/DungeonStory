using System;
using System.Collections.Generic;

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
