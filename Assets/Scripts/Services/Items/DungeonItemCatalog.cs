using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PhysicalItemIds
{
    private const string EquipmentPrefix = "equipment-item:";
    public const string EquipmentModule = "item:equipment-module";

    public static string ForEquipment(string equipmentId)
    {
        return EquipmentPrefix + (equipmentId?.Trim() ?? string.Empty);
    }

    public static bool TryGetEquipmentDefinitionId(string itemId, out string equipmentId)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (normalized.StartsWith(EquipmentPrefix, StringComparison.Ordinal))
        {
            equipmentId = normalized.Substring(EquipmentPrefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(equipmentId);
        }

        equipmentId = string.Empty;
        return false;
    }

    public static string ForEquipmentModule() => EquipmentModule;

    public static bool IsEquipmentModule(string itemId) =>
        string.Equals(
            itemId?.Trim() ?? string.Empty,
            EquipmentModule,
            StringComparison.Ordinal);
}

public interface IDungeonItemCatalogProvider
{
    IReadOnlyList<DungeonItemDefinition> All { get; }
    DungeonItemDefinition GetDefinition(string itemId);
    bool TryGetDefinition(string itemId, out DungeonItemDefinition definition);
}

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
