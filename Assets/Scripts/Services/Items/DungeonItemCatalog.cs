using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ResourceDungeonItemCatalogProvider : IDungeonItemCatalogProvider
{
    private readonly IItemDefinitionCatalog itemDefinitionCatalog;
    private readonly IReadOnlyList<DungeonItemDefinition> all;

    public ResourceDungeonItemCatalogProvider(
        IItemDefinitionCatalog itemDefinitionCatalog)
    {
        this.itemDefinitionCatalog = itemDefinitionCatalog
            ?? throw new ArgumentNullException(nameof(itemDefinitionCatalog));
        all = itemDefinitionCatalog.All
            .Select(definition => definition.ToDungeonItemDefinition())
            .ToArray();
    }

    public IReadOnlyList<DungeonItemDefinition> All => all;

    public DungeonItemDefinition GetDefinition(string itemId)
    {
        return itemDefinitionCatalog
            .GetRequired((ItemDefinitionId)itemId)
            .ToDungeonItemDefinition();
    }

    public bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
    {
        if (itemDefinitionCatalog.TryGet((ItemDefinitionId)itemId, out ItemDefinitionSO authored))
        {
            definition = authored.ToDungeonItemDefinition();
            return true;
        }

        definition = null;
        return false;
    }
}
