public struct InvasionSpawnedEvent
{
    public CharacterActor intruderActor;
    public InvasionThreatSnapshot threatSnapshot;

    public InvasionSpawnedEvent(CharacterActor intruder, InvasionThreatSnapshot threatSnapshot)
    {
        intruderActor = intruder;
        this.threatSnapshot = threatSnapshot;
    }
}

public struct InvasionDungeonBreachedEvent
{
    public InvasionIntruderRuntime intruderRuntime;
    public CharacterActor intruderActor;
    public InvasionThreatSnapshot threatSnapshot;

    public InvasionDungeonBreachedEvent(
        InvasionIntruderRuntime intruderRuntime,
        CharacterActor intruderActor,
        InvasionThreatSnapshot threatSnapshot)
    {
        this.intruderRuntime = intruderRuntime;
        this.intruderActor = intruderActor;
        this.threatSnapshot = threatSnapshot;
    }
}

public struct InvasionFacilityDamagedEvent
{
    public CharacterActor intruderActor;
    public BuildableObject facility;

    public InvasionFacilityDamagedEvent(CharacterActor intruder, BuildableObject facility)
    {
        intruderActor = intruder;
        this.facility = facility;
    }

}

public struct InvasionFinalCombatStartedEvent
{
    public CharacterActor intruderActor;
    public CharacterActor ownerActor;

    public InvasionFinalCombatStartedEvent(CharacterActor intruder, CharacterActor owner)
    {
        intruderActor = intruder;
        ownerActor = owner;
    }

}

public readonly struct DefenseFrontCollapsedEvent
{
    public DefenseEngagement Engagement { get; }
    public string Reason { get; }

    public DefenseFrontCollapsedEvent(
        DefenseEngagement engagement,
        string reason)
    {
        Engagement = engagement;
        Reason = reason ?? string.Empty;
    }
}
