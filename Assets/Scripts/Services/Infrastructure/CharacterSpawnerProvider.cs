using System;

public interface ICharacterSpawnerProvider
{
    bool TryGetSpawner(out CharacterSpawner spawner);
}

public sealed class CharacterSpawnerProvider : ICharacterSpawnerProvider
{
    private readonly CharacterSceneRuntimeReferences runtimeReferences;

    public CharacterSpawnerProvider(
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetSpawner(out CharacterSpawner resolvedSpawner)
    {
        resolvedSpawner = runtimeReferences.Spawner;
        return resolvedSpawner != null;
    }
}
