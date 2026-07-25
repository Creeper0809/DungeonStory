public enum CharacterFacing
{
    RIGHT,
    LEFT
}

public enum CharacterType
{
    NPC,
    Customer,
    Intruder
}

public enum CharacterRole
{
    Regular,
    Owner
}

public enum CharacterStatType
{
    Attack = 0,
    Sales = 1,
    Research = 2,
    MoveSpeed = 3,
    Strength = 4,
    Toughness = 5,
    Dexterity = 6,
    Cleaning = 7,
    Endurance = 8,
    Shooting = 9,
    Evasion = 10
}

public enum CharacterCondition
{
    HUNGER,
    THIRST,
    SLEEP,
    FUN,
    MOOD,
    EXCRETION,
    HYGIENE
}

public enum CharacterDecisionState
{
    DECIDE,
    MOVE,
    EXECUTE
}

public enum CharacterLifecycleState
{
    None,
    SpawningOutside,
    EnteringDungeon,
    Active,
    ExitingDungeon,
    PreparingExpedition,
    DepartingExpedition,
    ReturningExpedition,
    OnExpedition,
    Downed,
    Despawned
}
