using System;
using System.Collections.Generic;

[Serializable]
public sealed class DungeonRunVariableSaveData
{
    public int runSeed;
    public int currentDay = 1;
    public List<int> randomDrawMaxima = new List<int>();
    public bool hasStartVariables;
    public DungeonRunStartSaveData startVariables =
        new DungeonRunStartSaveData();
    public List<DungeonActiveRunVariableSaveData> activeOperationVariables =
        new List<DungeonActiveRunVariableSaveData>();
    public string invasionVariableId = string.Empty;
}

[Serializable]
public sealed class DungeonRunStartSaveData
{
    public int seed;
    public string ownerSpeciesTag = string.Empty;
    public string ownerDoctrineId = string.Empty;
    public InvasionThreatDifficulty difficulty;
    public DungeonDifficulty runDifficulty = DungeonDifficulty.Normal;
    public List<int> startingFacilityCandidateIds = new List<int>();
    public List<string> startingGuestSpeciesCandidates = new List<string>();
    public List<int> startingBlueprintCandidateIds = new List<int>();
    public int initialShopSeed;
    public string initialDungeonLayoutId = string.Empty;
    public float threatRiseMultiplier = 1f;
}

[Serializable]
public sealed class DungeonActiveRunVariableSaveData
{
    public string definitionId = string.Empty;
    public int startDay = 1;
    public int remainingDays = 1;
}

[Serializable]
public sealed class DungeonRunFlowSaveData
{
    public DungeonRunPhase phase = DungeonRunPhase.Preparation;
    public DungeonRunOutcome outcome = DungeonRunOutcome.None;
    public int currentDay = 1;
    public bool bossArmed;
    public bool bossActive;
    public bool finalInvasionDefended;
    public int bossCycle;
}
