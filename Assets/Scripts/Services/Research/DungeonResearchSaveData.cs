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
}

[Serializable]
public sealed class DungeonResearchTaskSaveData
{
    public int blueprintId = -1;
    public float progress;
}
