using System;
using System.Collections.Generic;

[Serializable]
public sealed class DungeonCharacterWorldSaveData
{
    public List<DungeonCharacterSaveData> actors =
        new List<DungeonCharacterSaveData>();
    public List<WorldCharacterProfile> populationProfiles =
        new List<WorldCharacterProfile>();
    public GlobalFacilityReputationSnapshot globalFacilityReputation =
        new GlobalFacilityReputationSnapshot();
}

[Serializable]
public sealed class DungeonCharacterSaveData
{
    public string persistentId = string.Empty;
    public int dataId = -1;
    public bool isOwner;
    public string displayName = string.Empty;
    public CharacterType characterType;
    public CharacterRole role;
    public int gridX;
    public int gridY;
    public CharacterLifecycleState lifecycleState =
        CharacterLifecycleState.Active;
    public float currentHealth;
    public float injurySeverity;
    public float baseMood = CharacterMoodRules.DefaultBaseMood;
    public List<DungeonCharacterConditionSaveData> conditions =
        new List<DungeonCharacterConditionSaveData>();
    public List<DungeonCharacterMoodFactorSaveData> moodFactors =
        new List<DungeonCharacterMoodFactorSaveData>();
    public List<DungeonCharacterWorkPrioritySaveData> workPriorities =
        new List<DungeonCharacterWorkPrioritySaveData>();
    public AbilityWork.DutyState dutyState = AbilityWork.DutyState.OnDuty;
    public int visitCount;
    public int lookAroundCount;
    public int holdingMoney;
    public List<string> recentLogEntries = new List<string>();
    public int level = 1;
    public int currentExperience;
    public List<string> learnedSkillIds = new List<string>();
    public List<string> equippedSkillIds = new List<string>();
    public CharacterGrowthState growth = new CharacterGrowthState();
    public CharacterNarrativeLedger narrative = new CharacterNarrativeLedger();
    public CharacterSocialMemorySnapshot socialMemory =
        new CharacterSocialMemorySnapshot();
    public CharacterExpeditionRecoveryState expeditionRecovery =
        new CharacterExpeditionRecoveryState();
    public CharacterCarryInventorySaveData carryInventory =
        new CharacterCarryInventorySaveData();
}

[Serializable]
public sealed class DungeonCharacterConditionSaveData
{
    public CharacterCondition condition;
    public float value;
}

[Serializable]
public sealed class DungeonCharacterMoodFactorSaveData
{
    public string id = string.Empty;
    public string label = string.Empty;
    public float value;
    public float remainingSeconds;
}

[Serializable]
public sealed class DungeonCharacterWorkPrioritySaveData
{
    public string workTypeId = string.Empty;
    public WorkPriorityLevel priority;
}
