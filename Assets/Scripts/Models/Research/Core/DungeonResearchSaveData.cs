using System;
using System.Collections.Generic;

// Serialization-only Research aggregate DTOs; no runtime behavior or Unity scene references.

public enum FacilityShopFulfillmentPhase
{
    Pending = 0,
    Spawned = 1,
    Delivered = 2
}

[Serializable]
public sealed class DungeonResearchSaveData
{
    // V7: domain revision and, only while observation is uncertain, its frozen fact.
    public long outcomeSequence;
    public ResearchWorkPendingPublication pendingWorkPublication = new();
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
    // Additive exact parent identities for completed project research results.
    public List<DungeonResearchCompletionResultSaveData> completedProjectResults =
        new List<DungeonResearchCompletionResultSaveData>();
    public List<DungeonResearchQueueEntrySaveData> projectQueue =
        new List<DungeonResearchQueueEntrySaveData>();
    public string activeProjectId = string.Empty;
    public bool materializeLegacyBlueprintItems;
    // Additive V6 fulfillment outbox. Missing data in older V6 saves means
    // that no purchase-keyed delivery history was recorded yet.
    public List<FacilityShopFulfillmentSaveData> shopFulfillments =
        new List<FacilityShopFulfillmentSaveData>();
}

[Serializable]
public sealed class FacilityShopFulfillmentSaveData
{
    public string purchaseResultKey = string.Empty;
    public string offerTypeId = string.Empty;
    public int offerDataId;
    public string itemDefinitionId = string.Empty;
    public string itemDisplayName = string.Empty;
    public FacilityShopFulfillmentPhase phase;
    public string stackId = string.Empty;
    public string itemInstanceId = string.Empty;
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
public sealed class DungeonResearchCompletionResultSaveData
{
    public string projectId = string.Empty;
    public string publicationKey = string.Empty;
}

[Serializable]
public sealed class DungeonResearchQueueEntrySaveData
{
    public string projectId = string.Empty;
    public string suspendedReason = string.Empty;
}
