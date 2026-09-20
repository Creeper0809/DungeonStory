using System;
using System.Collections.Generic;

// Serialization-only Research aggregate DTOs; no runtime behavior or Unity scene references.

[Serializable]
public sealed class DungeonResearchSaveData
{
    // Additive V6 field. Earlier V6 saves have no research outcomes and start at zero.
    public long outcomeSequence;
    // Zero means legacy allocation history is unknown, not that no task existed.
    public int nextKnowledgeTaskSequence;
    // Zero is an early-V6 payload. When its sequence history is also absent,
    // restore moves future allocations to the disjoint migrated namespace.
    public int knowledgeTaskIdentityGeneration;
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
    public float requiredWorkAtCapture;
}

[Serializable]
public sealed class DungeonResearchQueueEntrySaveData
{
    public string projectId = string.Empty;
    public string suspendedReason = string.Empty;
}
