using System;

// Presentation-neutral notice emitted after the consumption owner accepted this operation.
// The consumables owner, not this synchronous notice, owns durable effects and replay.
public readonly struct SubstanceUseCompletedEvent
{
    public SubstanceUseCompletedEvent(CharacterActor actor, ConsumableOperationId operation,
        ItemDefinitionId item, ItemStackId stack, string displayName)
    {
        if (actor == null || !operation.IsValid || !item.IsValid || !stack.IsValid)
            throw new ArgumentException("substance-use-completed-event-invalid");
        Actor = actor; Operation = operation; Item = item; Stack = stack;
        // Do not introduce a display-name limit after consumption has already committed.
        DisplayName = displayName ?? string.Empty;
    }
    public CharacterActor Actor { get; }
    public ConsumableOperationId Operation { get; }
    public ItemDefinitionId Item { get; }
    public ItemStackId Stack { get; }
    public string DisplayName { get; }
}
