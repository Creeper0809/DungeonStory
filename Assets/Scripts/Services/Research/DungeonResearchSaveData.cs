using System;
using System.Collections.Generic;

[Serializable]
public sealed class DungeonResearchSaveData
{
    public List<DungeonResearchTaskSaveData> tasks =
        new List<DungeonResearchTaskSaveData>();
    public List<int> completedBlueprintIds = new List<int>();
    public List<int> unlockedBuildingIds = new List<int>();
    public List<string> unlockedRecipeIds = new List<string>();
    public List<KnowledgeResidueTaskSaveData> knowledgeTasks =
        new List<KnowledgeResidueTaskSaveData>();
    public List<DungeonResearchProjectProgressSaveData> projectProgress =
        new List<DungeonResearchProjectProgressSaveData>();
    public List<string> completedProjectIds = new List<string>();
    public List<DungeonResearchQueueEntrySaveData> projectQueue =
        new List<DungeonResearchQueueEntrySaveData>();
    public string activeProjectId = string.Empty;
    public bool materializeLegacyBlueprintItems;
}

[Serializable]
public sealed class DungeonResearchTaskSaveData
{
    public int blueprintId = -1;
    public float progress;
}

[Serializable]
public sealed class DungeonResearchProjectProgressSaveData
{
    public string projectId = string.Empty;
    public float progress;
}

[Serializable]
public sealed class DungeonResearchQueueEntrySaveData
{
    public string projectId = string.Empty;
    public string suspendedReason = string.Empty;
}
