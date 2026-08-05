#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class UnifiedItemDefinitionAssetBuilder
{
    private const string Root = "Assets/Resources/SO/Items/Definitions";

    [MenuItem("Tools/DungeonStory/Items/Rebuild Unified Item Definitions")]
    public static void RebuildAll()
    {
        EnsureFolder("Assets/Resources/SO/Items", "Definitions");

        // These builders are the existing authoring sources. Their ResourceItemDefinitionSO
        // output now writes composed features through the compatibility subtype.
        ResourceEconomyAssetBuilder.Rebuild();
        CombatEquipmentAssetBuilder.BuildAll();
        ResearchProjectAssetBuilder.Rebuild();

        HashSet<string> authoredIds = Resources
            .LoadAll<ItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath)
            .Where(definition => definition != null && definition.StableId.IsValid)
            .Select(definition => definition.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        int generated = 0;
        foreach (CombatEquipmentDefinitionSO equipment in Resources
                     .LoadAll<CombatEquipmentDefinitionSO>(ResourceCombatEquipmentCatalog.ResourcePath)
                     .Where(definition => definition != null)
                     .OrderBy(definition => definition.EquipmentId, StringComparer.Ordinal))
        {
            string itemId = equipment.ItemId;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new InvalidOperationException(
                    $"Equipment '{equipment.EquipmentId}' has no physical item ID.");
            }

            if (authoredIds.Contains(itemId))
            {
                continue;
            }

            GenericItemDefinitionSO asset = GetOrCreate(itemId);
            StockCategory category = StockCategory.Weapon;
            asset.ConfigureCore(
                itemId,
                equipment.DisplayName,
                equipment.Description,
                category,
                Mathf.Max(1, Mathf.RoundToInt(equipment.Weight * 18f)),
                Mathf.Max(0.1f, equipment.Weight),
                1);
            asset.SetFeature(new EquipmentItemFeature
            {
                equipmentDefinitionId = equipment.EquipmentId
            });
            asset.SetFeature(new ProductionItemFeature
            {
                kind = ResourceItemKind.FinishedGood
            });
            if (!string.IsNullOrWhiteSpace(equipment.RequiredResearchId))
            {
                asset.SetFeature(new ResearchGateItemFeature
                {
                    requiredResearchId = equipment.RequiredResearchId
                });
            }
            EditorUtility.SetDirty(asset);
            authoredIds.Add(itemId);
            generated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ItemDefinitionSO[] all = Resources.LoadAll<ItemDefinitionSO>(
            ItemDefinitionSO.UnifiedResourcePath);
        ResourceItemDefinitionCatalog catalog = new(all);
        if (catalog.Validate().Count > 0)
        {
            throw new InvalidOperationException(
                "Unified item validation failed:\n" + string.Join("\n", catalog.Validate()));
        }

        Debug.Log(
            $"Unified item definitions rebuilt: {catalog.All.Count} SO assets, "
            + $"{generated} generated legacy/equipment definitions, duplicate IDs 0.");
    }

    public static string ValidateAll()
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

        return $"ITEM V6 PASS: {catalog.All.Count} canonical SOs, "
            + $"{equipmentItems} equipment features, duplicate IDs 0, invalid features 0.";
    }

    private static GenericItemDefinitionSO GetOrCreate(string itemId)
    {
        string fileName = string.Concat(itemId.Select(character =>
            char.IsLetterOrDigit(character) ? character : '_'));
        string path = $"{Root}/{fileName}.asset";
        GenericItemDefinitionSO asset =
            AssetDatabase.LoadAssetAtPath<GenericItemDefinitionSO>(path);
        if (asset != null)
        {
            return asset;
        }

        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            // Only this dedicated generated folder is repaired automatically. A prior compile
            // may have produced a missing-script placeholder before the concrete SO got its own
            // Unity script asset.
            AssetDatabase.DeleteAsset(path);
        }

        asset = ScriptableObject.CreateInstance<GenericItemDefinitionSO>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
