#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused V22 authoring and deterministic-rule gate. This deliberately
/// validates the V22 slice independently from older production graph debt.
/// </summary>
public static class V22ApparelDebugScenarios
{
    private const string ApparelRoot = "Assets/Resources/SO/Apparel/Definitions";
    private const string MaterialRoot = "Assets/Resources/SO/Apparel/Materials";
    private const string ItemRoot = "Assets/Resources/SO/Economy/Items/V22Apparel";
    private const string FacilityRoot = "Assets/Resources/SO/Building/V22Apparel";
    private const string CropRoot = "Assets/Resources/SO/Economy/Crops/V22Textiles";
    private const string GenomeRoot = "Assets/Resources/SO/Economy/CropGenomes/V22Textiles";
    private const string RecipeRoot = "Assets/Resources/SO/Economy/Recipes/V22Apparel";

    [MenuItem("DungeonStory/Debug/Economy/Run V22 Apparel Contracts")]
    public static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunAll()
    {
        ValidateDefinitionsAndAssets();
        ValidateFacilitiesAndRecipes();
        ValidateAnimalFiberProducts();
        ValidateFiberQualityTradeoff();
        ValidateSaveBoundary();
        Debug.Log(
            "V22 apparel contracts passed: apparel=56, woven=10, crops=4, "
            + "genomes=12, animal-products=3, facilities=14, recipes=89.");
    }

    private static void ValidateDefinitionsAndAssets()
    {
        ApparelDefinitionSO[] apparel = LoadAll<ApparelDefinitionSO>(ApparelRoot);
        TextileMaterialDefinitionSO[] materials =
            LoadAll<TextileMaterialDefinitionSO>(MaterialRoot);
        CropDefinitionSO[] crops = LoadAll<CropDefinitionSO>(CropRoot);
        CropGenomeDefinitionSO[] genomes = LoadAll<CropGenomeDefinitionSO>(GenomeRoot);
        GameDomainContentCatalogSO domainCatalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(
                "Assets/Resources/SO/Content/GameDomainContentCatalog.asset")
            ?? throw new InvalidOperationException(
                "The authoritative domain content catalog is missing.");
        CropDefinitionSO[] allCrops = domainCatalog.GetAll<CropDefinitionSO>()
            .Where(value => value != null)
            .ToArray();
        CropGenomeDefinitionSO[] allGenomes = domainCatalog
            .GetAll<CropGenomeDefinitionSO>()
            .Where(value => value != null)
            .ToArray();

        Require(apparel.Length == 56, $"Expected 56 apparel definitions, found {apparel.Length}.");
        Require(apparel.Select(value => value.ApparelId).Distinct(StringComparer.Ordinal).Count() == 56,
            "Apparel definition IDs must be unique.");
        Require(apparel.All(value => value.id > 0)
                && apparel.Select(value => value.id).Distinct().Count() == 56,
            "Apparel numeric compatibility IDs must be positive and unique.");
        Require(apparel.Select(value => value.PhysicalItemId).Distinct(StringComparer.Ordinal).Count() == 56,
            "Apparel physical item IDs must be unique.");
        Require(apparel.All(value => value.RequiredPoints != AnatomyAttachmentPoint.None),
            "Every apparel definition must require a real anatomy attachment point.");

        Require(materials.Length == 12,
            $"Expected 10 woven and 2 non-woven material definitions, found {materials.Length}.");
        Require(materials.Count(value => (value.Tags & TextileMaterialTag.Woven) != 0) == 10,
            "Expected exactly 10 woven textile definitions.");
        Require(materials.Count(value => (value.Tags & TextileMaterialTag.NonWoven) != 0) == 2,
            "Expected exactly 2 non-woven apparel material definitions.");
        Require(materials.All(value => value.id > 0)
                && materials.Select(value => value.id).Distinct().Count() == 12,
            "Textile material numeric compatibility IDs must be positive and unique.");
        Require(materials.All(value =>
        {
            MonoScript script = MonoScript.FromScriptableObject(value);
            return script != null && string.Equals(
                AssetDatabase.GetAssetPath(script).Replace('\\', '/'),
                "Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs",
                StringComparison.Ordinal);
        }), "Every textile material asset must reference its concrete MonoScript.");

        Require(crops.Length == 4, $"Expected 4 V22 fiber crops, found {crops.Length}.");
        Require(genomes.Length == 12, $"Expected 12 V22 crop genomes, found {genomes.Length}.");
        Require(allCrops.Length == 12,
            $"V22 runtime authority requires 12 total crops, found {allCrops.Length}.");
        Require(allGenomes.Length == 32,
            $"V22 runtime authority requires 32 total genomes, found {allGenomes.Length}.");
        Require(allGenomes.Count(value => value.GenomeId.EndsWith(
                    ":base",
                    StringComparison.Ordinal)) == 12,
            "V22 runtime authority requires exactly one base genome per crop.");
        Require(genomes.All(value => value.ValidateDefinition().Count == 0),
            "Every V22 crop genome must contain exactly six valid loci.");

        HashSet<string> expectedCropIds = crops
            .Select(value => value.CropId)
            .ToHashSet(StringComparer.Ordinal);
        Require(genomes.All(value => expectedCropIds.Contains(value.CropId)),
            "Every V22 crop genome must belong to one of the four V22 crops.");

        ResourceItemDefinitionSO[] items =
            LoadAll<ResourceItemDefinitionSO>("Assets/Resources/SO/Economy");
        foreach (ResourceItemDefinitionSO item in items.Where(value =>
                     value.ItemId.StartsWith("fiber:", StringComparison.Ordinal)
                     || value.ItemId.StartsWith("yarn:", StringComparison.Ordinal)))
        {
            Require(item.MaxStack == 200,
                $"Raw fiber and yarn '{item.ItemId}' must have MaxStack 200.");
        }
        foreach (TextileMaterialDefinitionSO material in materials.Where(value =>
                     (value.Tags & TextileMaterialTag.Woven) != 0))
        {
            ResourceItemDefinitionSO item = items.FirstOrDefault(value =>
                string.Equals(value.ItemId, material.PhysicalItemId, StringComparison.Ordinal));
            int expectedMaxStack = material.PhysicalItemId switch
            {
                "material:cloth" => 75,
                "material:dreamweave" => 40,
                _ => 100
            };
            Require(item != null && item.MaxStack == expectedMaxStack,
                $"Woven material '{material.PhysicalItemId}' must have authored "
                + $"MaxStack {expectedMaxStack}.");
        }
    }

    private static void ValidateFacilitiesAndRecipes()
    {
        BuildingSO[] facilities = LoadAll<BuildingSO>(FacilityRoot);
        Require(facilities.Length == 14,
            $"Expected 14 V22 apparel facilities, found {facilities.Length}.");
        int[] ids = facilities.Select(value => value.id).OrderBy(value => value).ToArray();
        Require(ids.SequenceEqual(Enumerable.Range(9301, 14)),
            "V22 apparel facilities must occupy IDs 9301..9314 exactly.");

        foreach (BuildingSO facility in facilities)
        {
            BuildingWorkAmountAbility work = facility.GetAbility<BuildingWorkAmountAbility>();
            Require(work != null && work.ConstructionMaterials.Count > 0,
                $"Facility {facility.id} requires concrete construction materials.");
            BuildingProductionOutputDispositionAbility disposition =
                facility.GetProductionOutputDispositionAbility();
            bool connectedCommand = facility.ResearchFacilityCommand
                    != ResearchFacilityCommandKind.None
                && ResearchFacilityCommandConsumerRegistry.HasExecutionContract(
                    facility.ResearchFacilityCommand);
            bool deferredContent = disposition != null
                && disposition.dispositionKind
                    == ProductionOutputDispositionAuthoringKind.DeclaredNoOutput
                && disposition.ReasonCode.StartsWith(
                    "content-gap:",
                    StringComparison.Ordinal);
            Require(connectedCommand || deferredContent,
                $"Facility {facility.id} has neither a typed apparel execution contract nor an explicit content-gap disposition.");
        }

        Require(facilities.Count(value =>
                value.GetProductionOutputDispositionAbility() != null) == 5,
            "Exactly five V22 apparel facilities must retain explicit deferred-output content-gap markers.");

        ProductionRecipeSO[] recipes = LoadAll<ProductionRecipeSO>(RecipeRoot);
        Require(recipes.Length == 89, $"Expected 89 V22 textile recipes, found {recipes.Length}.");
        Require(recipes.All(value => value.id > 0), "Every V22 recipe requires a numeric ID.");
        Require(recipes.Select(value => value.id).Distinct().Count() == recipes.Length,
            "V22 recipe numeric IDs must be unique.");
        Require(recipes.Select(value => value.RecipeId).Distinct(StringComparer.Ordinal).Count()
                == recipes.Length,
            "V22 recipe stable IDs must be unique.");
        Require(recipes.All(value => value.Inputs.Count > 0 && value.Outputs.Count > 0),
            "Every V22 recipe requires physical inputs and outputs.");
    }

    private static void ValidateAnimalFiberProducts()
    {
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["silk_spider"] = "fiber:cave-silk",
            ["frost_ram"] = "fiber:frost-wool",
            ["deep_goat"] = "fiber:deep-goat-wool"
        };
        WildlifeSpeciesSO[] species = LoadAll<WildlifeSpeciesSO>("Assets/Resources/SO");
        foreach (KeyValuePair<string, string> pair in expected)
        {
            WildlifeSpeciesSO animal = species.FirstOrDefault(value =>
                string.Equals(value.SpeciesId, pair.Key, StringComparison.Ordinal));
            Require(animal != null, $"Missing V22 fiber animal '{pair.Key}'.");
            Require(animal.Husbandry.Products.Count(value =>
                        string.Equals(value.ItemId, pair.Value, StringComparison.Ordinal)) == 1,
                $"Fiber animal '{pair.Key}' must produce exactly one '{pair.Value}' entry.");
        }
    }

    private static void ValidateFiberQualityTradeoff()
    {
        for (int yieldIndex = 0; yieldIndex < 9; yieldIndex++)
        {
            float yield = 0.9f + yieldIndex * 0.025f;
            for (int growthIndex = 0; growthIndex < 9; growthIndex++)
            {
                float growth = 0.84f + growthIndex * 0.04f;
                FiberCropResourceInput input = new(yield, growth);
                FiberCropResourceResult first = FiberCropResourceRules.Evaluate(input);
                FiberCropResourceResult second = FiberCropResourceRules.Evaluate(input);
                Require(Mathf.Approximately(first.WaterMultiplier, second.WaterMultiplier)
                        && Mathf.Approximately(first.FertilityMultiplier,
                            second.FertilityMultiplier),
                    $"Fiber resource demand must be deterministic at {yieldIndex}/{growthIndex}.");
            }
        }

        FiberCropResourceResult bothMaximum = FiberCropResourceRules.Evaluate(
            new FiberCropResourceInput(1.1f, 1.16f));
        Require(Mathf.Approximately(bothMaximum.WaterMultiplier, 1.85f)
                && Mathf.Approximately(bothMaximum.FertilityMultiplier, 2f),
            "Maximum throughput water/fertility multipliers drifted from the V23 contract.");
    }

    private static void ValidateSaveBoundary()
    {
        Require(DungeonGameSaveData.CurrentVersion == 24,
            "The full-world save generation must be V23.");
        Require(DungeonCharacterEnvironmentSaveData.CurrentVersion >= 8,
            "The current character environment section must include apparel terminal authority.");
        Require(new DungeonCharacterEnvironmentSaveData().apparelWorkOrders == null,
            "Missing apparel work-order arrays must remain distinguishable from captured empties.");
        Require(new DungeonCharacterEnvironmentSaveData()
                .apparelWorkOrderTerminalStates == null,
            "Missing apparel terminal-state arrays must remain distinguishable from captured empties.");
    }

    private static T[] LoadAll<T>(string root) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
