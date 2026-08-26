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

        int generated = RebuildEquipmentItemsOnly();
        generated += RebuildWildlifeCarcassItemsCore();

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

    public static int RebuildEquipmentItemsOnly()
    {
        EnsureFolder("Assets/Resources/SO/Items", "Definitions");

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

        GameContentCatalogAssetBuilder.ReindexItemDefinitions();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return generated;
    }

    [MenuItem("Tools/DungeonStory/Items/Rebuild Wildlife Carcass Definitions")]
    public static void RebuildWildlifeCarcassItems()
    {
        RebuildWildlifeCarcassItemsCore();
    }

    private static int RebuildWildlifeCarcassItemsCore()
    {
        EnsureFolder("Assets/Resources/SO/Items", "Definitions");

        WildlifeSpeciesSO[] species = Resources
            .LoadAll<WildlifeSpeciesSO>("SO")
            .Where(value => value != null)
            .OrderBy(value => value.SpeciesId, StringComparer.Ordinal)
            .ToArray();
        if (species.Length == 0)
        {
            throw new InvalidOperationException(
                "No authored wildlife species exist for carcass item generation.");
        }

        int changed = 0;
        foreach (WildlifeSpeciesSO definition in species)
        {
            string itemId = WildlifeItemDefinitions.GetCarcassItemId(
                definition.SpeciesId);
            GenericItemDefinitionSO asset = GetOrCreate(itemId);
            string before = EditorJsonUtility.ToJson(asset);
            asset.ConfigureCore(
                itemId,
                definition.DisplayName + " 사체",
                "도축 시설로 옮기면 식량과 부산물을 얻습니다.",
                StockCategory.Food,
                price: 4,
                weight: definition.CarcassWeight,
                stackLimit: 1);
            asset.SetFeature(new ProductionItemFeature
            {
                kind = ResourceItemKind.Intermediate,
                ingredientTags = ResourceIngredientTag.None,
                sharedIntermediate = false
            });
            asset.SetFeature(new FoodItemFeature
            {
                quality = MealQualityTier.Simple,
                qualityBand = MealQualityBand.Simple,
                servingRole = MealServingRole.FullMeal,
                nutrition = 10f,
                mood = 0f,
                freshnessSeconds = 600f,
                preserved = false
            });
            string after = EditorJsonUtility.ToJson(asset);
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                EditorUtility.SetDirty(asset);
                changed++;
            }
        }

        GameContentCatalogAssetBuilder.ReindexItemDefinitions();
        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
        }
        AssetDatabase.Refresh();
        Debug.Log(
            $"Wildlife carcass definitions rebuilt: species={species.Length}; changed={changed}.");
        return changed;
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
        if (equipmentItems != 61)
        {
            throw new InvalidOperationException(
                $"Expected exactly 61 equipment item features, found {equipmentItems}.");
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
