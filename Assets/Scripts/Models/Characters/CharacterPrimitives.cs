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
