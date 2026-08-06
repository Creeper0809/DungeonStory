using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;

public sealed class ResourceDungeonFactionCatalogApplicationAdapter
{
    private readonly IReadOnlyList<FactionDefinitionSnapshot> definitions;

    public ResourceDungeonFactionCatalogApplicationAdapter(IGameContentCatalog content)
    {
        List<DungeonFactionDefinitionSO> loaded =
            (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<DungeonFactionDefinitionSO>()
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.StableId))
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToList();
        if (loaded.Count == 0)
        {
            throw new InvalidOperationException(
                "The root content catalog contains no authored faction definitions.");
        }

        string[] duplicateIds = loaded
            .GroupBy(value => value.StableId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"The root content catalog contains duplicate faction definition IDs: {string.Join(", ", duplicateIds)}.");
        }

        definitions = loaded.Select(value => value.ToSnapshot()).ToArray();
    }

    public IReadOnlyList<FactionDefinitionSnapshot> Definitions => definitions;
}

public sealed class FactionItemLogisticsDependencies
{
    public FactionItemLogisticsDependencies(
        IWorldItemSpawner itemSpawner,
        IWorldItemStackRuntime itemRuntime,
        IWorldDropZoneQuery dropZones)
    {
        ItemSpawner = itemSpawner
            ?? throw new ArgumentNullException(nameof(itemSpawner));
        ItemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        DropZones = dropZones ?? throw new ArgumentNullException(nameof(dropZones));
    }

    public IWorldItemSpawner ItemSpawner { get; }
    public IWorldItemStackRuntime ItemRuntime { get; }
    public IWorldDropZoneQuery DropZones { get; }
}

public sealed class FactionCharacterSpawnDependencies
{
    public FactionCharacterSpawnDependencies(
        IRunCharacterCatalog characterCatalog,
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterSpawnObjectFactory characterFactory,
        ICharacterAiWorldRegistry worldRegistry)
    {
        CharacterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
        SpawnerProvider = spawnerProvider
            ?? throw new ArgumentNullException(nameof(spawnerProvider));
        CharacterFactory = characterFactory
            ?? throw new ArgumentNullException(nameof(characterFactory));
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
    }

    public IRunCharacterCatalog CharacterCatalog { get; }
    public ICharacterSpawnerProvider SpawnerProvider { get; }
    public ICharacterSpawnObjectFactory CharacterFactory { get; }
    public ICharacterAiWorldRegistry WorldRegistry { get; }
}
