using System;
using System.Collections.Generic;
using System.Linq;

public interface IRuntimeBuildingArchetypeCatalog
{
    BuildingSO WorldResourceNode { get; }
    BuildingSO WorldFilthWorkTarget { get; }
    BuildingSO RequireDefinition(int definitionId);
    BuildingSO RequireExteriorZone(ExteriorZoneType zoneType, GridLayer layer);
}

public static class RuntimeBuildingArchetypeIds
{
    public const int WorldResourceNode = -1950010000;
    public const int WorldFilthWorkTarget = -1950010001;

    public static int ExteriorZone(ExteriorZoneType zoneType, GridLayer layer) =>
        -1950000000 + (int)zoneType * 100 + (int)layer;
}

public static class StarterBuildingDefinitionIds
{
    public const int Hallway = 0;
    public const int Door = 1;
    public const int Wall = 7;
}

public sealed class RuntimeBuildingArchetypeCatalog : IRuntimeBuildingArchetypeCatalog
{
    private readonly IReadOnlyDictionary<int, BuildingSO> byId;

    public RuntimeBuildingArchetypeCatalog(IGameContentCatalog content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        byId = content.GetAll<BuildingSO>()
            .Where(definition => definition != null)
            .GroupBy(definition => definition.id)
            .ToDictionary(
                group => group.Key,
                group => group.Count() == 1
                    ? group.Single()
                    : throw new InvalidOperationException(
                        $"Duplicate BuildingSO id '{group.Key}' in the root content catalog."));

        WorldResourceNode = Require(RuntimeBuildingArchetypeIds.WorldResourceNode);
        WorldFilthWorkTarget = Require(RuntimeBuildingArchetypeIds.WorldFilthWorkTarget);
    }

    public BuildingSO WorldResourceNode { get; }
    public BuildingSO WorldFilthWorkTarget { get; }

    public BuildingSO RequireDefinition(int definitionId) => Require(definitionId);

    public BuildingSO RequireExteriorZone(ExteriorZoneType zoneType, GridLayer layer)
    {
        BuildingSO definition = Require(RuntimeBuildingArchetypeIds.ExteriorZone(zoneType, layer));
        if (definition.layer != layer)
        {
            throw new InvalidOperationException(
                $"Exterior archetype '{definition.name}' is authored for {definition.layer}, not {layer}.");
        }

        return definition;
    }

    private BuildingSO Require(int id)
    {
        return byId.TryGetValue(id, out BuildingSO definition) && definition != null
            ? definition
            : throw new InvalidOperationException(
                $"Required runtime BuildingSO archetype '{id}' is missing from GameContentCatalogSO.");
    }
}
