#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Narrow WIM-030 publication entry. It owns only the carapace definition,
/// the pre-existing crystal-beetle butcher yield, and the existing item index.
/// It intentionally never saves assets, refreshes Unity, regenerates V20, or
/// adds a recipe, research, husbandry, or runtime capability.
/// </summary>
public static class Wim030CrystalBeetleCarapaceAssetBuilder
{
    public const string CarapaceItemId = "resource:crystal-beetle-carapace";
    public const string CrystalBeetleSpeciesId = "crystal_beetle";
    public const string CrystalBeetleCarcassItemId = "wild:carcass:crystal_beetle";

    private const int CarapaceNumericId = 8110;
    private const int CarapacePrice = 5;
    private const float CarapaceWeightKg = 1f;
    // Reuses resource:hide's existing ordinary animal-material storage bound.
    private const int CarapaceStackLimit = 40;
    private const string CarapaceAssetPath =
        "Assets/Resources/SO/Economy/Items/resource_crystal_beetle_carapace.asset";
    private const string CrystalBeetleAssetPath =
        "Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_crystal_beetle.asset";
    private const string CrystalBeetleCarcassAssetPath =
        "Assets/Resources/SO/Items/Definitions/wild_carcass_crystal_beetle.asset";
    private const string ItemCatalogAssetPath =
        "Assets/Resources/SO/Content/ItemDefinitionCatalog.asset";
    private static readonly string[] PublicationTargetPaths =
    {
        CarapaceAssetPath,
        CrystalBeetleAssetPath,
        ItemCatalogAssetPath
    };

    /// <summary>
    /// The only assets this narrow publisher may create or mark dirty. It does
    /// not call SaveAssets, Refresh, or any broad content rebuild.
    /// </summary>
    public static string TargetAssetPathsForOperator =>
        string.Join(" | ", PublicationTargetPaths);

    [MenuItem("DungeonStory/WIM-030/Publish Crystal Beetle Carapace (No Save)")]
    public static void PublishFromMenu()
    {
        Wim030CrystalBeetleCarapacePublication publication = Publish();
        Debug.Log(
            "WIM030 crystal-beetle carapace publication prepared; changed="
            + publication.ChangedAssetCount
            + "; itemCatalogReindexed=" + publication.ItemCatalogReindexed
            + "; targetPaths=" + TargetAssetPathsForOperator
            + ". Save/Refresh is intentionally owned by the publishing operator.");
    }

    /// <summary>
    /// Operator-only second publication. Run this after the first targeted
    /// publication; it executes Publish again and fails unless that real
    /// second pass is a no-op. It deliberately does not save or refresh.
    /// </summary>
    [MenuItem("DungeonStory/WIM-030/Second Publish Crystal Beetle Carapace (Require No-op; No Save)")]
    public static void PublishSecondPassFromMenu()
    {
        if (!InspectPublishedStateReadOnly().MatchesPublishedState)
        {
            throw new InvalidOperationException(
                "WIM030 second publication requires an already published first pass; "
                + "targetPaths=" + TargetAssetPathsForOperator + ".");
        }

        Wim030CrystalBeetleCarapacePublication publication = Publish();
        if (!publication.IsNoOp)
        {
            throw new InvalidOperationException(
                "WIM030 second publication was not a no-op; changed="
                + publication.ChangedAssetCount
                + "; itemCatalogReindexed=" + publication.ItemCatalogReindexed
                + "; targetPaths=" + TargetAssetPathsForOperator + ".");
        }

        Debug.Log(
            "WIM030 secondPublish=no-op; targetPaths=" + TargetAssetPathsForOperator
            + ". Save/Refresh remains operator-owned.");
    }

    /// <summary>
    /// Applies the approved authoring slice once. A subsequent call reports
    /// zero changed assets when the item, yield, and catalog already match.
    /// </summary>
    public static Wim030CrystalBeetleCarapacePublication Publish()
    {
        PrevalidatePublication(
            out ResourceItemDefinitionSO carapace,
            out WildlifeSpeciesSO crystalBeetle,
            out ItemDefinitionCatalogSO itemCatalog);

        int changedAssetCount = 0;
        if (carapace == null)
        {
            carapace = ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
            AssetDatabase.CreateAsset(carapace, CarapaceAssetPath);
        }
        if (!MatchesCarapace(carapace))
        {
            carapace.id = CarapaceNumericId;
            carapace.Configure(
                CarapaceItemId,
                "수정딱정벌레 갑각",
                "수정딱정벌레 사체에서 분리한 장식성 갑각. 아직 제작 재료로 소비되지 않는다.",
                StockCategory.General,
                ResourceItemKind.AnimalProduct,
                ResourceIngredientTag.Mineral,
                CarapacePrice,
                CarapaceWeightKg,
                CarapaceStackLimit,
                researchId: string.Empty);
            EditorUtility.SetDirty(carapace);
            changedAssetCount++;
        }

        if (!MatchesOnlyCarapaceYield(crystalBeetle))
        {
            crystalBeetle.ConfigureButcherYields(new[]
            {
                new WildlifeButcherYield { itemId = CarapaceItemId, amount = 1 }
            });
            EditorUtility.SetDirty(crystalBeetle);
            changedAssetCount++;
        }

        bool itemCatalogReindexed = !ContainsExactlyOnce(itemCatalog, carapace);
        if (itemCatalogReindexed)
        {
            // This is the existing narrow indexer, not a V20 or economy rebuild.
            GameContentCatalogAssetBuilder.ReindexItemDefinitions();
        }
        itemCatalog = RequireItemCatalog();
        if (!ContainsExactlyOnce(itemCatalog, carapace))
        {
            throw new InvalidOperationException(
                "WIM030 item-definition index does not contain exactly one carapace entry.");
        }

        return new Wim030CrystalBeetleCarapacePublication(
            changedAssetCount,
            itemCatalogReindexed);
    }

    /// <summary>
    /// Read-only inspection for the focused verifier. It establishes that the
    /// currently loaded authoring matches this slice, but is not a second
    /// Publish execution and must not be reported as one.
    /// </summary>
    public static Wim030CrystalBeetleCarapaceReadOnlyInspection InspectPublishedStateReadOnly()
    {
        ResourceItemDefinitionSO carapace = AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
            CarapaceAssetPath);
        WildlifeSpeciesSO crystalBeetle = AssetDatabase.LoadAssetAtPath<WildlifeSpeciesSO>(
            CrystalBeetleAssetPath);
        ItemDefinitionCatalogSO itemCatalog =
            AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalogSO>(ItemCatalogAssetPath);
        bool correct = carapace != null
            && MatchesCarapace(carapace)
            && crystalBeetle != null
            && string.Equals(
                crystalBeetle.SpeciesId,
                CrystalBeetleSpeciesId,
                StringComparison.Ordinal)
            && MatchesOnlyCarapaceYield(crystalBeetle)
            && itemCatalog != null
            && ContainsExactlyOnce(itemCatalog, carapace);
        return new Wim030CrystalBeetleCarapaceReadOnlyInspection(correct);
    }

    private static void PrevalidatePublication(
        out ResourceItemDefinitionSO carapace,
        out WildlifeSpeciesSO crystalBeetle,
        out ItemDefinitionCatalogSO itemCatalog)
    {
        RequireExistingCarcassBaseline();
        carapace = LoadExistingCarapaceAtTargetPath();
        crystalBeetle = RequireCrystalBeetle();
        itemCatalog = RequireItemCatalog();
        RequireNoConflictingCatalogCarapace(itemCatalog, carapace);
    }

    private static ResourceItemDefinitionSO LoadExistingCarapaceAtTargetPath()
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(CarapaceAssetPath);
        if (asset == null)
            return null;
        if (asset is not ResourceItemDefinitionSO carapace)
        {
            throw new InvalidOperationException(
                "WIM030 carapace path is occupied by '" + asset.GetType().Name + "'.");
        }
        if (!string.Equals(carapace.ItemId, CarapaceItemId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "WIM030 carapace target path has a conflicting item identity '"
                + carapace.ItemId + "'.");
        }
        return carapace;
    }

    private static WildlifeSpeciesSO RequireCrystalBeetle()
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(CrystalBeetleAssetPath);
        if (asset is not WildlifeSpeciesSO crystalBeetle)
        {
            throw new InvalidOperationException(
                "WIM030 requires a WildlifeSpeciesSO at '" + CrystalBeetleAssetPath + "'.");
        }
        if (!string.Equals(
                crystalBeetle.SpeciesId,
                CrystalBeetleSpeciesId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "WIM030 crystal-beetle asset identity does not match its required species ID.");
        }
        return crystalBeetle;
    }

    private static ItemDefinitionCatalogSO RequireItemCatalog()
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(ItemCatalogAssetPath);
        if (asset is not ItemDefinitionCatalogSO itemCatalog)
        {
            throw new InvalidOperationException(
                "WIM030 requires an ItemDefinitionCatalogSO at '" + ItemCatalogAssetPath + "'.");
        }
        return itemCatalog;
    }

    private static void RequireNoConflictingCatalogCarapace(
        ItemDefinitionCatalogSO itemCatalog,
        ResourceItemDefinitionSO carapaceAtTargetPath)
    {
        foreach (ItemDefinitionSO definition in itemCatalog.Definitions)
        {
            if (definition == null
                || !string.Equals(definition.ItemId, CarapaceItemId, StringComparison.Ordinal))
            {
                continue;
            }
            if (carapaceAtTargetPath == null
                || !ReferenceEquals(definition, carapaceAtTargetPath))
            {
                throw new InvalidOperationException(
                    "WIM030 item catalog contains a conflicting carapace identity before publication.");
            }
        }
    }

    private static void RequireExistingCarcassBaseline()
    {
        ItemDefinitionSO carcass = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
            CrystalBeetleCarcassAssetPath)
            ?? throw new InvalidOperationException(
                "WIM030 requires the existing crystal-beetle carcass at '"
                + CrystalBeetleCarcassAssetPath + "'.");
        if (!string.Equals(carcass.ItemId, CrystalBeetleCarcassItemId, StringComparison.Ordinal)
            || carcass.UnitWeight != 4f
            || carcass.UnitPrice != 4)
        {
            throw new InvalidOperationException(
                "WIM030 carcass baseline must remain wild:carcass:crystal_beetle, 4kg, price 4.");
        }
    }

    private static bool MatchesCarapace(ResourceItemDefinitionSO item)
    {
        return item != null
            && item.id == CarapaceNumericId
            && string.Equals(item.ItemId, CarapaceItemId, StringComparison.Ordinal)
            && string.Equals(item.DisplayName, "수정딱정벌레 갑각", StringComparison.Ordinal)
            && string.Equals(
                item.Description,
                "수정딱정벌레 사체에서 분리한 장식성 갑각. 아직 제작 재료로 소비되지 않는다.",
                StringComparison.Ordinal)
            && item.StockCategory == StockCategory.General
            && item.Kind == ResourceItemKind.AnimalProduct
            && item.IngredientTags == ResourceIngredientTag.Mineral
            && item.UnitPrice == CarapacePrice
            && item.UnitWeight == CarapaceWeightKg
            && item.MaxStack == CarapaceStackLimit
            && string.IsNullOrEmpty(item.RequiredResearchId);
    }

    private static bool MatchesOnlyCarapaceYield(WildlifeSpeciesSO species)
    {
        WildlifeButcherYield[] yields = (species?.ButcherYields
                ?? Array.Empty<WildlifeButcherYield>())
            .Where(value => value != null)
            .ToArray();
        return yields.Length == 1
            && string.Equals(yields[0].itemId, CarapaceItemId, StringComparison.Ordinal)
            && yields[0].amount == 1;
    }

    private static bool ContainsExactlyOnce(
        ItemDefinitionCatalogSO catalog,
        ItemDefinitionSO expected)
    {
        return catalog != null
            && expected != null
            && catalog.Definitions.Count(value => ReferenceEquals(value, expected)) == 1
            && catalog.Definitions.Count(value => value != null
                && string.Equals(value.ItemId, CarapaceItemId, StringComparison.Ordinal)) == 1;
    }
}

public readonly struct Wim030CrystalBeetleCarapacePublication
{
    public Wim030CrystalBeetleCarapacePublication(
        int changedAssetCount,
        bool itemCatalogReindexed)
    {
        ChangedAssetCount = changedAssetCount;
        ItemCatalogReindexed = itemCatalogReindexed;
    }

    public int ChangedAssetCount { get; }
    public bool ItemCatalogReindexed { get; }
    public bool IsNoOp => ChangedAssetCount == 0 && !ItemCatalogReindexed;
}

public readonly struct Wim030CrystalBeetleCarapaceReadOnlyInspection
{
    public Wim030CrystalBeetleCarapaceReadOnlyInspection(bool matchesPublishedState)
    {
        MatchesPublishedState = matchesPublishedState;
    }

    public bool MatchesPublishedState { get; }
}
#endif
