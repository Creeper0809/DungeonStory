using System;

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
