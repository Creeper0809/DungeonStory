using System;

public enum ShoppingActionTransition
{
    MissingDestination = 1,
    MissingMovementContext = 2,
    DestinationUnreachable = 3,
    LooseItemStolen = 4
}

public readonly struct ShoppingActionObservedEvent
{
    public ShoppingActionObservedEvent(
        CharacterActor actor,
        ShoppingActionTransition transition,
        BuildableObject facility = null,
        string itemStackId = "",
        string itemDisplayName = "",
        float itemValue = 0f)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        if (!Enum.IsDefined(typeof(ShoppingActionTransition), transition))
        {
            throw new ArgumentOutOfRangeException(nameof(transition));
        }

        Transition = transition;
        Facility = facility;
        ItemStackId = itemStackId ?? string.Empty;
        ItemDisplayName = itemDisplayName ?? string.Empty;
        ItemValue = itemValue;
        ActorName = actor.name ?? string.Empty;
    }

    public CharacterActor Actor { get; }
    public ShoppingActionTransition Transition { get; }
    public BuildableObject Facility { get; }
    public string ItemStackId { get; }
    public string ItemDisplayName { get; }
    public float ItemValue { get; }
    public string ActorName { get; }
}
