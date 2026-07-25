using System;
using System.Collections.Generic;

[Serializable]
public sealed class DungeonMetaProgressionSaveData
{
    public int lifetimeEarnedCurrency;
    public int spentCurrency;
    public int completedRunCount;
    public List<DungeonStringIntSaveEntry> upgradeLevels =
        new List<DungeonStringIntSaveEntry>();
    public List<string> preservedRecipeIds = new List<string>();
    public DungeonMetaRunProgressSaveData runProgress =
        new DungeonMetaRunProgressSaveData();
    public bool ended;
    public bool hasLatestResult;
    public DungeonRunResultSaveData latestResult =
        new DungeonRunResultSaveData();
}

[Serializable]
public sealed class DungeonMetaRunProgressSaveData
{
    public float elapsedSeconds;
    public int currentDay = 1;
    public int settlementCount;
    public int defendedInvasionCount;
    public InvasionThreatStage maxThreatStage = InvasionThreatStage.Peaceful;
    public float finalInvasionThreat;
    public int offenseSuccessCount;
    public List<int> discoveredFacilityIds = new List<int>();
    public List<string> unlockedRecipeIds = new List<string>();
}

[Serializable]
public sealed class DungeonRunResultSaveData
{
    public string ownerName = string.Empty;
    public string endReason = string.Empty;
    public float survivalSeconds;
    public int survivedOperatingDays;
    public int settlementCount;
    public int defendedInvasionCount;
    public InvasionThreatStage maxThreatStage = InvasionThreatStage.Peaceful;
    public float finalInvasionThreat;
    public int firstDiscoveredFacilityCount;
    public int firstUnlockedRecipeCount;
    public int offenseSuccessCount;
    public float difficultyMultiplier = 1f;
    public DungeonDifficulty difficulty = DungeonDifficulty.Normal;
    public int legacyCurrency;
    public DungeonRunOutcome outcome = DungeonRunOutcome.Defeat;
}
