public enum CharacterPotentialGrade
{
    Ordinary,
    Promising,
    Excellent,
    Exceptional,
    Genius
}

public enum CharacterSkillRarity
{
    Common,
    Advanced,
    Rare,
    Heroic,
    Legendary
}

public enum CharacterSkillKind
{
    SpeciesActive,
    OwnerFixed,
    Active,
    Passive,
    Ultimate
}

public enum CharacterSkillTrigger
{
    ManualCombat,
    BattleStarted,
    DamageTaken,
    EnemyDefeated,
    BattleCompleted,
    InvasionStarted,
    WorkStarted,
    WorkCompleted,
    NeedChanged,
    MoodChanged,
    RelationshipChanged,
    OperatingDayStarted,
    ManualWork
}

public enum CharacterSkillTarget
{
    Self,
    Ally,
    Enemy,
    AllAllies,
    AllEnemies,
    Facility,
    Dungeon
}

public enum CharacterNarrativeDomain
{
    Work,
    FacilityUse,
    Need,
    Mood,
    Relationship,
    Injury,
    Survival,
    Invasion,
    Expedition,
    Combat
}
