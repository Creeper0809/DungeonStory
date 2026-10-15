using System;

/// <summary>
/// Transient references used to present an already-applied carcass-processing
/// fact. These references are event-local and are never part of save state.
/// </summary>
public readonly struct WildlifeCarcassProcessedEvent
{
    public WildlifeCarcassProcessedEvent(
        IBuildingVisitorPort visitor,
        BuildableObject building,
        CharacterActor actor,
        WildlifeCarcassProcessedFact fact)
    {
        Fact = fact ?? throw new ArgumentNullException(nameof(fact));
        if (fact.Snapshot().taboo && actor == null)
            throw new ArgumentNullException(nameof(actor));

        Visitor = visitor;
        Building = building;
        Actor = actor;
    }

    public IBuildingVisitorPort Visitor { get; }
    public BuildableObject Building { get; }
    public CharacterActor Actor { get; }
    public WildlifeCarcassProcessedFact Fact { get; }
}
