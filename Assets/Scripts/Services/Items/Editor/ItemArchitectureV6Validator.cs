#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

public static class ItemArchitectureV6Validator
{
    public static string RunAndReport()
    {
        ItemDefinitionSO[] definitions = Resources.LoadAll<ItemDefinitionSO>(
            ItemDefinitionSO.UnifiedResourcePath);
        ResourceItemDefinitionCatalog catalog = new(definitions);
        if (catalog.Validate().Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", catalog.Validate()));
        }

        if (catalog.All.Count < 290)
        {
            throw new InvalidOperationException(
                $"Expected at least 290 canonical item SO assets, found {catalog.All.Count}.");
        }

        int equipmentItems = catalog.All.Count(definition =>
            definition.TryGetFeature(out EquipmentItemFeature _));
        if (equipmentItems != 43)
        {
            throw new InvalidOperationException(
                $"Expected exactly 43 equipment item features, found {equipmentItems}.");
        }

        if (!catalog.TryGet((ItemDefinitionId)"ammo:paper-cartridge", out ItemDefinitionSO cartridge)
            || cartridge.GetFeatureOrDefault<ProductionItemFeature>() == null)
        {
            throw new InvalidOperationException(
                "Paper cartridge is not in the canonical item catalog.");
        }

        ItemInstanceComponentSaveData pristine = new()
        {
            componentTypeId = ItemInstanceComponentIds.Freshness,
            values = new()
            {
                new ItemStateValueSaveData
                {
                    key = "remaining-seconds",
                    kind = ItemStateValueKind.Decimal,
                    decimalValue = 120d
                }
            }
        };
        ItemInstanceComponentSaveData stale = pristine.Clone();
        stale.values[0].decimalValue = 30d;
        string pristineSignature = ItemStackSignature.Create(
            "food:test",
            new[] { pristine });
        string staleSignature = ItemStackSignature.Create(
            "food:test",
            new[] { stale });
        if (string.Equals(pristineSignature, staleSignature, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Stack signatures ignored stack-affecting freshness state.");
        }

        return $"ITEM V6 PASS: {catalog.All.Count} canonical SOs, "
            + $"{equipmentItems} equipment features, duplicate IDs 0, invalid features 0, "
            + "stack-component signature isolation PASS.";
    }
}
#endif
