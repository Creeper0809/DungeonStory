using System;

/// <summary>
/// Cohesive world capability used by medical transport, supply, and restore.
/// </summary>
public sealed class CharacterMedicalWorldServices
{
    public CharacterMedicalWorldServices(
        IGridSystemProvider gridProvider,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldItemStackRuntime itemStackRuntime)
    {
        GridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        ItemStacks = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
    }

    public IGridSystemProvider GridProvider { get; }
    public ICharacterAiWorldRegistry WorldRegistry { get; }
    public IWorldItemStackRuntime ItemStacks { get; }
}
