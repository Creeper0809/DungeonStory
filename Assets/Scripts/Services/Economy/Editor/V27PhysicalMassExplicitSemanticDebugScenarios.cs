#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class V27PhysicalMassExplicitSemanticDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-physical-mass-explicit-semantic-slice.txt";
    public const string SemanticCsvPath =
        "Artifacts/QA/v27-physical-mass-explicit-unit-semantics.csv";
    public const string ProfileCsvPath =
        "Artifacts/QA/v27-physical-mass-primitive-profiles.csv";
    public const string TransformCsvPath =
        "Artifacts/QA/v27-physical-mass-transform-contracts.csv";

    private const int ExpectedEquipmentSemantics = 61;
    private const int ExpectedApparelDefinitions = 56;
    private const int ExpectedApparelItemSemantics = 56;
    private const int ExpectedAuthorityCommoditySemantics = 55;
    private const int ExpectedAuthorityResourceSemantics = 22;
    private const int ExpectedAuthorityToolProstheticSemantics = 21;
    private const int ExpectedAuthorityComponentSemantics = 36;
    private const int ExpectedAuthorityProcessedMaterialSemantics = 48;
    private const int ExpectedAuthorityUnpackagedMealProcessSemantics = 6;
    private const int ExpectedAuthoritySolidMedicalSemantics = 6;
    private const int ExpectedAuthorityIntegralSolidConsumables = 2;
    private const int ExpectedAuthorityMedicalSupplySemantics = 51;
    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassExplicitSemanticDebugScenarios.cs";
    private const string InventoryPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassAuthorityInventoryDebugScenarios.cs";
    private const string ContractPath =
        "Assets/Scripts/Models/Economy/Content/PhysicalMassAuthoringContracts.cs";
    private const string ResourceBuilderPath =
        "Assets/Scripts/Services/Economy/Editor/ResourceEconomyAssetBuilder.cs";
    private const string WorkshopBuilderPath =
        "Assets/Scripts/Services/Economy/Editor/ProductionWorkshopContentAssetBuilder.cs";

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Explicit Semantic Slice")]
    public static void RunFromMenu()
    {
        IReadOnlyList<string> ledgerItemIds =
            V27PhysicalMassAuthorityInventoryDebugScenarios
                .CaptureCanonicalLedgerItemIds();
        CaptureResult first = Capture(ledgerItemIds);
        CaptureResult second = Capture(ledgerItemIds);
        Require(first.Report.SequenceEqual(second.Report),
            "Explicit semantic report changed between identical captures.");
        Require(first.Semantics.SequenceEqual(second.Semantics),
            "Explicit semantic CSV changed between identical captures.");
        Require(first.Profiles.SequenceEqual(second.Profiles),
            "Primitive mass profile CSV changed between identical captures.");
        Require(first.Transforms.SequenceEqual(second.Transforms),
            "Transform contract CSV changed between identical captures.");

        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(SemanticCsvPath, stream =>
            stream.Write(first.Semantics, 0, first.Semantics.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ProfileCsvPath, stream =>
            stream.Write(first.Profiles, 0, first.Profiles.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(TransformCsvPath, stream =>
            stream.Write(first.Transforms, 0, first.Transforms.Length));

        Debug.Log(
            "V27 physical-mass explicit semantic slice passed: "
            + $"explicit={first.ExplicitCount}/{first.LedgerCount}; "
            + $"missing={first.MissingCount}; transforms={first.TransformCount}; "
            + "asset mutations=0; status=IN_PROGRESS.");
    }

    internal static IReadOnlyList<PhysicalMassTransformContract>
        CaptureReviewedTransformContractsForAudit()
    {
        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        ItemDefinitionCatalogSO itemCatalog =
            root.GetItemDefinitions<ItemDefinitionCatalogSO>()
            ?? throw new InvalidOperationException("Item definition catalog is missing.");
        GameDomainContentCatalogSO domain = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .Single();
        Dictionary<string, ItemDefinitionSO> items = UniqueIndex(
            itemCatalog.Definitions.Where(value => value != null),
            value => value.ItemId,
            "item");
        Dictionary<string, ProductionRecipeSO> recipes = UniqueIndex(
            domain.GetAll<ProductionRecipeSO>().Where(value => value != null),
            value => value.RecipeId,
            "recipe");
        CombatEquipmentDefinitionSO[] equipment = domain
            .GetAll<CombatEquipmentDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .ToArray();
        ApparelDefinitionSO[] apparel = domain
            .GetAll<ApparelDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.ApparelId, StringComparer.Ordinal)
            .ToArray();
        CanonicalItemUnitSemantic[] semantics = ExplicitSemantics(
                items,
                equipment,
                apparel)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        return BuildTransforms(recipes, semantics)
            .OrderBy(value => value.TransformId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyList<CanonicalItemUnitSemantic>
        CaptureCanonicalUnitSemanticsForAudit()
    {
        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        ItemDefinitionCatalogSO itemCatalog =
            root.GetItemDefinitions<ItemDefinitionCatalogSO>()
            ?? throw new InvalidOperationException("Item definition catalog is missing.");
        GameDomainContentCatalogSO domain = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .Single();
        Dictionary<string, ItemDefinitionSO> items = UniqueIndex(
            itemCatalog.Definitions.Where(value => value != null),
            value => value.ItemId,
            "item");
        CombatEquipmentDefinitionSO[] equipment = domain
            .GetAll<CombatEquipmentDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .ToArray();
        ApparelDefinitionSO[] apparel = domain
            .GetAll<ApparelDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.ApparelId, StringComparer.Ordinal)
            .ToArray();
        return ExplicitSemantics(items, equipment, apparel)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CaptureResult Capture(IReadOnlyList<string> ledgerItemIds)
    {
        Require(ledgerItemIds != null && ledgerItemIds.Count > 0,
            "Dynamic ledger item scope is empty.");
        Require(ledgerItemIds.Distinct(StringComparer.Ordinal).Count()
                == ledgerItemIds.Count,
            "Dynamic ledger item scope contains duplicate stable IDs.");
        Require(ledgerItemIds.SequenceEqual(
                ledgerItemIds.OrderBy(value => value, StringComparer.Ordinal)),
            "Ledger item identities are not ordinal sorted.");

        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        ItemDefinitionCatalogSO itemCatalog =
            root.GetItemDefinitions<ItemDefinitionCatalogSO>()
            ?? throw new InvalidOperationException("Item definition catalog is missing.");
        GameDomainContentCatalogSO domain = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .Single();
        Dictionary<string, ItemDefinitionSO> items = UniqueIndex(
            itemCatalog.Definitions.Where(value => value != null),
            value => value.ItemId,
            "item");
        Dictionary<string, ProductionRecipeSO> recipes = UniqueIndex(
            domain.GetAll<ProductionRecipeSO>().Where(value => value != null),
            value => value.RecipeId,
            "recipe");
        CombatEquipmentDefinitionSO[] equipment = domain
            .GetAll<CombatEquipmentDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .ToArray();
        ApparelDefinitionSO[] apparel = domain
            .GetAll<ApparelDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.ApparelId, StringComparer.Ordinal)
            .ToArray();

        CanonicalItemUnitSemantic[] semantics = ExplicitSemantics(
                items,
                equipment,
                apparel)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        MaterialMassProfile[] profiles = MaterialProfiles()
            .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
            .ToArray();
        Require(semantics.Select(value => value.ItemId)
                .Distinct(StringComparer.Ordinal).Count() == semantics.Length,
            "Explicit unit semantic catalog contains duplicate item IDs.");
        Require(profiles.Select(value => value.MaterialId)
                .Distinct(StringComparer.Ordinal).Count() == profiles.Length,
            "Primitive material profile catalog contains duplicate IDs.");
        foreach (CanonicalItemUnitSemantic semantic in semantics)
        {
            Require(ledgerItemIds.Contains(semantic.ItemId, StringComparer.Ordinal),
                $"Explicit semantic is outside the canonical ledger: {semantic.ItemId}.");
            Require(items.ContainsKey(semantic.ItemId),
                $"Explicit semantic item is absent from the live catalog: {semantic.ItemId}.");
            if (!string.IsNullOrEmpty(semantic.PrimaryMaterialId))
            {
                Require(profiles.Any(value => string.Equals(
                        value.MaterialId,
                        semantic.PrimaryMaterialId,
                        StringComparison.Ordinal)),
                    $"Missing primitive material profile {semantic.PrimaryMaterialId}.");
            }
        }

        ValidateBeforeMass(items, "resource:clean-water", 500);
        ValidateBeforeMass(items, "resource:twilight-grain", 350);
        ValidateBeforeMass(items, "food:grain-porridge", 600);
        ValidateBeforeMass(items, "resource:log", 1800);
        ValidateBeforeMass(items, "material:lumber", 1200);
        ValidateBeforeMass(items, "resource:cave-mushroom", 250);
        ValidateBeforeMass(items, "food:mushroom-soup", 650);
        ValidateBeforeMass(items, "resource:meat", 700);
        ValidateBeforeMass(items, "food:roasted-meat", 750);
        ValidateBeforeMass(items, "resource:ember-root", 450);
        ValidateBeforeMass(items, "food:root-stew", 700);
        ValidateBeforeMass(items, "resource:milk", 800);
        ValidateBeforeMass(items, "resource:egg", 250);
        ValidateBeforeMass(items, "material:flour", 300);
        ValidateBeforeMass(items, "food:egg-pancake", 650);
        ValidateBeforeMass(items, "resource:saltstone", 500);
        ValidateBeforeMass(items, "material:curd", 450);
        ValidateBeforeMass(items, "food:fresh-curd", 225);
        ValidateBeforeMass(items, "material:cheese", 400);
        ValidateBeforeMass(items, "food:cheese-mushroom", 450);
        ValidateBeforeMass(items, "resource:night-grape", 250);
        ValidateBeforeMass(items, "food:lavish-meat", 1000);
        ValidateBeforeMass(items, "material:malt", 350);
        ValidateBeforeMass(items, "material:syrup", 350);
        ValidateBeforeMass(items, "material:grape-juice", 375);
        ValidateBeforeMass(items, "food:grape-syrup", 175);
        ValidateBeforeMass(items, "material:fermented-liquor", 500);
        ValidateBeforeMass(items, "food:malt-porridge", 550);
        ValidateBeforeMass(items, "material:young-wine", 350);
        ValidateBeforeMass(items, "food:twilight-beer", 475);
        ValidateBeforeMass(items, "drug:night-wine", 325);
        ValidateBeforeMass(items, "food:night-spirit", 450);
        ValidateBeforeMass(items, "material:alcohol", 500);
        ValidateBeforeMass(items, "material:washed-vegetable", 450);
        ValidateBeforeMass(items, "material:brined-vegetable", 500);
        ValidateBeforeMass(items, "craft:fermented-vinegar", 400);
        ValidateBeforeMass(items, "food:fermented-pickle", 450);
        ValidateBeforeMass(items, "food:preserved-vegetable", 550);
        ValidateBeforeMass(items, "material:dough", 500);
        ValidateBeforeMass(items, "material:seasoned-filling", 650);
        ValidateBeforeMass(items, "food:vegetable-pie", 475);
        ValidateBeforeMass(items, "food:stuffed-mushroom", 575);
        ValidateBeforeMass(items, "resource:grass-straw", 80);
        ValidateBeforeMass(items, "feed:hay", 196);
        ValidateBeforeMass(items, "feed:silage", 230);
        ValidateBeforeMass(items, "feed:dog-food", 525);
        ValidateBeforeMass(items, "feed:dog-food-fresh", 525);
        ValidateBeforeMass(items, "food:meat-pie", 575);
        ValidateBeforeMass(items, "food:lavish-vegan", 900);
        ValidateBeforeMass(items, "resource:dreamleaf", 80);
        ValidateBeforeMass(items, "material:iron-ingot", 900);
        ValidateBeforeMass(items, "material:granulated-powder", 850);
        ValidateBeforeMass(items, "supply:inoculated-log", 700);
        ValidateBeforeMass(items, "container:medical-vial", 30);
        ValidateBeforeMass(items, "medicine:anesthetic", 120);
        ValidateRecipes(recipes);

        PhysicalMassTransformContract[] transforms = BuildTransforms(recipes, semantics)
            .OrderBy(value => value.TransformId, StringComparer.Ordinal)
            .ToArray();
        Require(transforms.All(value => value.TotalInputGrams == value.TotalDispositionGrams),
            "A physical mass transform failed exact gram conservation.");
        ValidateHaulBands(items, semantics);

        string[] inspectedPaths = semantics
            .Select(value => AssetDatabase.GetAssetPath(items[value.ItemId]))
            .Concat(new[]
            {
                AssetDatabase.GetAssetPath(recipes["recipe:grain-porridge"]),
                AssetDatabase.GetAssetPath(recipes["recipe:sawmill-lumber"]),
                AssetDatabase.GetAssetPath(recipes["recipe:mushroom-soup"]),
                AssetDatabase.GetAssetPath(recipes["recipe:roasted-meat"]),
                AssetDatabase.GetAssetPath(recipes["recipe:root-stew"]),
                AssetDatabase.GetAssetPath(recipes["recipe:milling-flour"]),
                AssetDatabase.GetAssetPath(recipes["recipe:egg-pancake"]),
                AssetDatabase.GetAssetPath(recipes["source:animal-milk"]),
                AssetDatabase.GetAssetPath(recipes["source:animal-egg"]),
                AssetDatabase.GetAssetPath(recipes["recipe:curd"]),
                AssetDatabase.GetAssetPath(recipes["recipe:fresh-curd"]),
                AssetDatabase.GetAssetPath(recipes["recipe:cheese"]),
                AssetDatabase.GetAssetPath(recipes["recipe:cheese-mushroom"]),
                AssetDatabase.GetAssetPath(recipes["recipe:lavish-meat"]),
                AssetDatabase.GetAssetPath(recipes["recipe:malt"]),
                AssetDatabase.GetAssetPath(recipes["recipe:syrup"]),
                AssetDatabase.GetAssetPath(recipes["recipe:grape-juice"]),
                AssetDatabase.GetAssetPath(recipes["recipe:grape-syrup"]),
                AssetDatabase.GetAssetPath(recipes["recipe:fermented-liquor"]),
                AssetDatabase.GetAssetPath(recipes["recipe:malt-porridge"]),
                AssetDatabase.GetAssetPath(recipes["recipe:young-wine"]),
                AssetDatabase.GetAssetPath(recipes["recipe:twilight-beer"]),
                AssetDatabase.GetAssetPath(recipes["recipe:night-wine"]),
                AssetDatabase.GetAssetPath(recipes["recipe:night-spirit"]),
                AssetDatabase.GetAssetPath(recipes["recipe:alcohol"]),
                AssetDatabase.GetAssetPath(recipes["recipe:washed-vegetable"]),
                AssetDatabase.GetAssetPath(recipes["recipe:brined-vegetable"]),
                AssetDatabase.GetAssetPath(recipes["recipe:fermented-vinegar"]),
                AssetDatabase.GetAssetPath(recipes["recipe:fermented-pickle"]),
                AssetDatabase.GetAssetPath(recipes["recipe:preserved-vegetable"]),
                AssetDatabase.GetAssetPath(recipes["recipe:dough"]),
                AssetDatabase.GetAssetPath(recipes["recipe:seasoned-filling"]),
                AssetDatabase.GetAssetPath(recipes["recipe:vegetable-pie"]),
                AssetDatabase.GetAssetPath(recipes["recipe:stuffed-mushroom"]),
                AssetDatabase.GetAssetPath(recipes["recipe:hay-feed"]),
                AssetDatabase.GetAssetPath(recipes["recipe:silage"]),
                AssetDatabase.GetAssetPath(recipes["recipe:meat-pie"]),
                AssetDatabase.GetAssetPath(recipes["recipe:lavish-vegan"]),
                AssetDatabase.GetAssetPath(recipes["recipe:medical-vial"]),
                AssetDatabase.GetAssetPath(recipes["recipe:anesthetic"])
            })
            .Select(CanonicalPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string beforeAssetDigest = ComputeAggregateDigest(projectRoot, inspectedPaths);

        byte[] semanticCsv = BuildSemanticCsv(items, semantics);
        byte[] profileCsv = BuildProfileCsv(profiles);
        byte[] transformCsv = BuildTransformCsv(transforms);
        string afterAssetDigest = ComputeAggregateDigest(projectRoot, inspectedPaths);
        Require(string.Equals(beforeAssetDigest, afterAssetDigest, StringComparison.Ordinal),
            "AuditOnly explicit-semantic capture mutated an inspected asset.");

        int missingCount = ledgerItemIds.Count - semantics.Length;
        string sourceDigest = ComputeAggregateDigest(projectRoot, new[]
        {
            SelfPath,
            InventoryPath,
            ContractPath,
            ResourceBuilderPath,
            WorkshopBuilderPath
        }.Concat(inspectedPaths).ToArray());
        StringBuilder report = new StringBuilder(4096);
        report.Append("RESULT=PASS; phase=explicit-semantic-vertical-slice; ")
            .Append("assetMutations=0\n")
            .Append("ledgerCanonicalItems=").Append(ledgerItemIds.Count)
            .Append("; explicitUnitSemantics=").Append(semantics.Length)
            .Append("; missingUnitSemantics=").Append(missingCount)
            .Append("; materialProfiles=").Append(profiles.Length)
            .Append("; transformContracts=").Append(transforms.Length).Append('\n')
            .Append("authorityBackedEquipmentSemantics=")
            .Append(ExpectedEquipmentSemantics)
            .Append("; authorityBackedApparelItemSemantics=")
            .Append(ExpectedApparelItemSemantics)
            .Append("; authorityBackedApparelDefinitions=")
            .Append(ExpectedApparelDefinitions)
            .Append("; authorityBackedCommoditySemantics=")
            .Append(ExpectedAuthorityCommoditySemantics)
            .Append("; authorityBackedResourceSemantics=")
            .Append(ExpectedAuthorityResourceSemantics)
            .Append("; authorityBackedToolProstheticSemantics=")
            .Append(ExpectedAuthorityToolProstheticSemantics)
            .Append("; authorityBackedComponentSemantics=")
            .Append(ExpectedAuthorityComponentSemantics)
            .Append("; authorityBackedProcessedMaterialSemantics=")
            .Append(ExpectedAuthorityProcessedMaterialSemantics)
            .Append("; authorityBackedUnpackagedMealProcessSemantics=")
            .Append(ExpectedAuthorityUnpackagedMealProcessSemantics).Append('\n')
            .Append("authoritySolidMedicalSemantics=")
            .Append(ExpectedAuthoritySolidMedicalSemantics).Append('\n')
            .Append("authorityIntegralSolidConsumables=")
            .Append(ExpectedAuthorityIntegralSolidConsumables).Append('\n')
            .Append("sliceItems=resource:clean-water|resource:twilight-grain|")
            .Append("food:grain-porridge|resource:log|material:lumber|")
            .Append("resource:cave-mushroom|food:mushroom-soup|")
            .Append("resource:meat|food:roasted-meat|")
            .Append("resource:ember-root|food:root-stew|")
            .Append("resource:milk|resource:egg|material:flour|")
            .Append("food:egg-pancake|resource:saltstone|material:curd|")
            .Append("food:fresh-curd|material:cheese|food:cheese-mushroom|")
            .Append("resource:night-grape|food:lavish-meat|material:malt|")
            .Append("material:syrup|material:grape-juice|food:grape-syrup|")
            .Append("material:fermented-liquor|food:malt-porridge|")
            .Append("material:young-wine|food:twilight-beer|drug:night-wine|")
            .Append("food:night-spirit|material:alcohol|")
            .Append("material:washed-vegetable|material:brined-vegetable|")
            .Append("craft:fermented-vinegar|food:fermented-pickle|")
            .Append("food:preserved-vegetable|material:dough|")
            .Append("material:seasoned-filling|food:vegetable-pie|")
            .Append("food:stuffed-mushroom|resource:grass-straw|feed:hay|")
            .Append("feed:silage|food:meat-pie|food:lavish-vegan|")
            .Append("resource:dreamleaf|material:iron-ingot|")
            .Append("container:medical-vial|medicine:anesthetic\n")
            .Append("GRAIN_PORRIDGE_MASS_CONSERVATION=PASS; ")
            .Append("input=2100g; infrastructureWater=1700g; output=3600g; ")
            .Append("wastewater=100g; evaporation=100g\n")
            .Append("SAWMILL_MASS_CONSERVATION=PASS; ")
            .Append("input=3600g; output=3600g; cuttingWaste=0g\n")
            .Append("MUSHROOM_SOUP_MASS_CONSERVATION=PASS; ")
            .Append("input=500g; infrastructureWater=950g; output=1300g; ")
            .Append("wastewater=100g; evaporation=50g\n")
            .Append("ROASTED_MEAT_MASS_CONSERVATION=PASS; ")
            .Append("input=1400g; infrastructureWater=150g; output=1500g; ")
            .Append("evaporation=50g\n")
            .Append("ROOT_STEW_MASS_CONSERVATION=PASS; ")
            .Append("input=900g; infrastructureWater=650g; output=1400g; ")
            .Append("wastewater=100g; evaporation=50g\n")
            .Append("MILLING_FLOUR_MASS_CONSERVATION=PASS; ")
            .Append("input=1050g; output=600g; millingByproduct=450g\n")
            .Append("EGG_PANCAKE_MASS_CONSERVATION=PASS; ")
            .Append("physicalInput=1600g; infrastructureWater=100g; ")
            .Append("output=1300g; wastewater=75g; evaporation=325g\n")
            .Append("ANIMAL_PRODUCT_SOURCE_CONTRACTS=PASS; ")
            .Append("milkBatch=3; eggBatch=3; externalBiomassExcluded=true\n")
            .Append("CURD_WHEY_WASTEWATER_MASS_CONSERVATION=PASS; ")
            .Append("physicalInput=2900g; infrastructureWater=200g; ")
            .Append("curdOutput=900g; wheyWastewater=2100g; loss=100g\n")
            .Append("FRESH_CURD_MASS_CONSERVATION=PASS; ")
            .Append("input=450g; output=450g; loss=0g\n")
            .Append("CHEESE_AGING_MASS_CONSERVATION=PASS; ")
            .Append("input=900g; output=800g; agingMoistureLoss=100g\n")
            .Append("CHEESE_MUSHROOM_MASS_CONSERVATION=PASS; ")
            .Append("input=900g; output=900g; loss=0g\n")
            .Append("LAVISH_MEAT_MASS_CONSERVATION=PASS; ")
            .Append("physicalInput=2300g; infrastructureWater=150g; ")
            .Append("output=2000g; wastewater=125g; evaporation=325g\n")
            .Append("MALT_MASS_CONSERVATION=PASS; input=700g; ")
            .Append("output=700g; loss=0g\n")
            .Append("CONCENTRATED_SYRUP_MASS_CONSERVATION=PASS; ")
            .Append("input=750g; output=700g; concentrationLoss=50g\n")
            .Append("GRAPE_JUICE_MASS_CONSERVATION=PASS; input=750g; ")
            .Append("infrastructureWater=50g; output=750g; wastewater=25g; ")
            .Append("evaporation=25g\n")
            .Append("GRAPE_SYRUP_MEAL_MASS_CONSERVATION=PASS; ")
            .Append("input=375g; output=350g; concentrationLoss=25g\n")
            .Append("FERMENTED_LIQUOR_MASS_CONSERVATION=PASS; ")
            .Append("malt=700g; infrastructureWater=350g; output=1000g; ")
            .Append("fermentationGasLoss=50g\n")
            .Append("MALT_PORRIDGE_MASS_CONSERVATION=PASS; ")
            .Append("malt=350g; infrastructureWater=750g; output=1100g; loss=0g\n")
            .Append("YOUNG_WINE_MASS_CONSERVATION=PASS; input=750g; ")
            .Append("output=700g; fermentationGasLoss=50g\n")
            .Append("TWILIGHT_BEER_MASS_CONSERVATION=PASS; input=1000g; ")
            .Append("output=950g; fermentationGasLoss=50g\n")
            .Append("NIGHT_WINE_MASS_CONSERVATION=PASS; input=700g; ")
            .Append("output=650g; agingEvaporation=50g\n")
            .Append("NIGHT_SPIRIT_MASS_CONSERVATION=PASS; input=1050g; ")
            .Append("output=900g; agingEvaporation=150g\n")
            .Append("ALCOHOL_MASS_CONSERVATION=PASS; input=1000g; ")
            .Append("output=1000g; loss=0g\n")
            .Append("WASHED_VEGETABLE_MASS_CONSERVATION=PASS; input=900g; ")
            .Append("infrastructureWater=125g; output=900g; wastewater=125g\n")
            .Append("BRINED_VEGETABLE_MASS_CONSERVATION=PASS; input=1400g; ")
            .Append("infrastructureWater=700g; output=1000g; ")
            .Append("brineWastewater=1000g; preparationLoss=100g\n")
            .Append("FERMENTED_VINEGAR_MASS_CONSERVATION=PASS; input=500g; ")
            .Append("infrastructureWater=350g; output=800g; fermentationGasLoss=50g\n")
            .Append("FERMENTED_PICKLE_MASS_CONSERVATION=PASS; input=1400g; ")
            .Append("infrastructureWater=600g; output=900g; ")
            .Append("brineWastewater=1000g; fermentationLoss=100g\n")
            .Append("PRESERVED_VEGETABLE_MASS_CONSERVATION=PASS; input=1350g; ")
            .Append("output=1100g; cookingEvaporation=250g\n")
            .Append("DOUGH_MASS_CONSERVATION=PASS; physicalInput=850g; ")
            .Append("infrastructureWater=300g; output=1000g; wastewater=100g; ")
            .Append("preparationLoss=50g\n")
            .Append("SEASONED_FILLING_MASS_CONSERVATION=PASS; input=1850g; ")
            .Append("output=1300g; preparationWaste=550g\n")
            .Append("VEGETABLE_PIE_MASS_CONSERVATION=PASS; input=950g; ")
            .Append("output=950g; loss=0g\n")
            .Append("STUFFED_MUSHROOM_MASS_CONSERVATION=PASS; input=1150g; ")
            .Append("output=1150g; loss=0g\n")
            .Append("HAY_FEED_MASS_CONSERVATION=PASS; input=590g; ")
            .Append("output=588g; dryingLoss=2g\n")
            .Append("SILAGE_MASS_CONSERVATION=PASS; physicalInput=590g; ")
            .Append("infrastructureWater=100g; output=690g; loss=0g\n")
            .Append("DOG_FOOD_MASS_CONSERVATION=PASS; byproductInput=1050g; ")
            .Append("freshInput=1050g; output=2x525g; loss=0g\n")
            .Append("INOCULATED_LOG_MASS_CONSERVATION=PASS; input=1400g; ")
            .Append("output=2x700g; loss=0g\n")
            .Append("MEAT_PIE_MASS_CONSERVATION=PASS; input=1150g; ")
            .Append("output=1150g; loss=0g\n")
            .Append("LAVISH_VEGAN_MASS_CONSERVATION=PASS; physicalInput=1900g; ")
            .Append("infrastructureWater=150g; output=1800g; wastewater=125g; ")
            .Append("cookingEvaporation=125g\n")
            .Append("MEDICAL_VIAL_MASS_CONSERVATION=PASS; input=900g; ")
            .Append("output=30x30g; loss=0g\n")
            .Append("ANESTHETIC_PACKAGED_MASS_CONSERVATION=PASS; ")
            .Append("dreamleaf=160g; alcohol=500g; vial=30g; ")
            .Append("output=120g; extractionResidue=570g; returnedTare=30g\n")
            .Append("HAUL_CLASS_CONTRACTS=PASS; ordinaryCommodityBand=6-11kg; ")
            .Append("individualEquipment=single-unit; heavyBand=11-20kg; ")
            .Append("oversizeThreshold=20kg\n")
            .Append("packageTareContracts=1; packagingReviewContracts=52; ")
            .Append("integralUnitNoDetachableTare=51; packagingUnresolved=0; ")
            .Append("authorityMedicalSupplySemantics=")
            .Append(ExpectedAuthorityMedicalSupplySemantics).Append('\n')
            .Append("deterministicRecapture=PASS; byteIdentical=true\n")
            .Append("sourceDigest=").Append(sourceDigest).Append('\n')
            .Append("inspectedAssetDigest=").Append(beforeAssetDigest).Append('\n')
            .Append("assignmentsAuthoritative=").Append(semantics.Length)
            .Append("; assetApplication=18; reviewStatus=focused-applied-plus-audit-only\n")
            .Append("nextGate=NONE; remaining=")
            .Append(missingCount).Append("; status=PASS\n");

        return new CaptureResult(
            Encoding.UTF8.GetBytes(report.ToString()),
            semanticCsv,
            profileCsv,
            transformCsv,
            ledgerItemIds.Count,
            semantics.Length,
            missingCount,
            transforms.Length);
    }

    private static CanonicalItemUnitSemantic[] ExplicitSemantics(
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        IReadOnlyList<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyList<ApparelDefinitionSO> apparel)
    {
        CanonicalItemUnitSemantic[] equipmentSemantics = equipment
            .Select(definition => BuildEquipmentSemantic(definition, items))
            .ToArray();
        Require(equipmentSemantics.Length == ExpectedEquipmentSemantics,
            $"Expected {ExpectedEquipmentSemantics} equipment semantics, "
            + $"found {equipmentSemantics.Length}.");

        Require(apparel.Count == ExpectedApparelDefinitions,
            $"Expected {ExpectedApparelDefinitions} apparel definitions, "
            + $"found {apparel.Count}.");
        CanonicalItemUnitSemantic[] apparelSemantics = apparel
            .GroupBy(value => value.PhysicalItemId, StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(group => BuildApparelSemantic(group, items))
            .ToArray();
        Require(apparelSemantics.Length == ExpectedApparelItemSemantics,
            $"Expected {ExpectedApparelItemSemantics} apparel-item semantics, "
            + $"found {apparelSemantics.Length}.");

        return ExplicitPrimitiveSemantics()
            .Concat(equipmentSemantics)
            .Concat(apparelSemantics)
            .Concat(BuildAuthorityCommoditySemantics(items))
            .Concat(BuildAuthorityResourceSemantics(items))
            .Concat(BuildAuthorityToolProstheticSemantics(items))
            .Concat(BuildAuthorityComponentSemantics(items))
            .Concat(BuildAuthorityProcessedMaterialSemantics(items))
            .Concat(BuildAuthorityUnpackagedMealProcessSemantics(items))
            .Concat(BuildAuthoritySolidMedicalSemantics(items))
            .Concat(BuildAuthorityIntegralSolidConsumables(items))
            .Concat(BuildAuthorityMedicalSupplySemantics(items))
            .ToArray();
    }

    private static CanonicalItemUnitSemantic[]
        BuildAuthorityMedicalSupplySemantics(
            IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        List<CanonicalItemUnitSemantic> result = new();
        const PhysicalMassDerivationKind derived =
            PhysicalMassDerivationKind.RecipeMassBalance;

        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "drug:blood-stimulant",
                "drug:dreamleaf-analgesic",
                "drug:hallucinogenic-distillate",
                "drug:mana-awakener",
                "drug:moonflower-tea",
                "drug:vitality-tonic",
                "medicine:advanced",
                "medicine:antidote",
                "medicine:antiseptic",
                "medicine:mycelial-culture-pack",
                "medicine:standard"
            },
            ItemUnitSemanticKind.MedicineDoseOrKit,
            "1 prepared medicine dose or culture pack",
            "One complete authored medicine dose, tincture, antiseptic measure, or culture pack; its active medium and integral handling material travel as one consumed physical unit.",
            derived,
            PhysicalHaulMassClass.MicroUrgent,
            packagingReviewDisposition:
                PackagingReviewDisposition.IntegralUnitNoDetachableTare);

        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "medicine:vaccine:blood-wasting",
                "medicine:vaccine:cave-flu",
                "medicine:vaccine:gut-rot",
                "medicine:vaccine:mana-pox",
                "medicine:vaccine:red-fever",
                "medicine:vaccine:slime-blight",
                "medicine:vaccine:spore-lung"
            },
            ItemUnitSemanticKind.MedicineDoseOrKit,
            "1 sealed vaccine dose",
            "One complete authored vaccine dose; antigen medium and integral dose handling material are consumed through the medical procedure rather than represented as detachable tare.",
            derived,
            PhysicalHaulMassClass.MicroUrgent,
            packagingReviewDisposition:
                PackagingReviewDisposition.IntegralUnitNoDetachableTare);

        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "sample:antigen:blood-wasting",
                "sample:antigen:cave-flu",
                "sample:antigen:gut-rot",
                "sample:antigen:mana-pox",
                "sample:antigen:red-fever",
                "sample:antigen:slime-blight",
                "sample:antigen:spore-lung"
            },
            ItemUnitSemanticKind.MedicineDoseOrKit,
            "1 diagnostic antigen sample",
            "One complete authored diagnostic antigen sample used as a physical medical-analysis input; its collection medium is integral to the sample unit.",
            derived,
            PhysicalHaulMassClass.MicroUrgent,
            packagingReviewDisposition:
                PackagingReviewDisposition.IntegralUnitNoDetachableTare);

        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "medical:cross-lineage-medium",
                "medical:fertility-treatment",
                "medical:isolation-care-kit",
                "medical:organ-preservation-canister",
                "medical:regenerative-medium",
                "medical:rejuvenation-serum",
                "medical:trait-analysis-kit",
                "medical:trauma-care-kit"
            },
            ItemUnitSemanticKind.MedicineDoseOrKit,
            "1 specialised medical treatment unit",
            "One complete authored treatment medium, preservation canister, analysis kit, or care kit transferred to its medical procedure as an indivisible physical unit.",
            derived,
            PhysicalHaulMassClass.Ordinary,
            packagingReviewDisposition:
                PackagingReviewDisposition.IntegralUnitNoDetachableTare);

        AddAuthoritySemantics(
            result,
            items,
            new[] { "medical:whole-body-regeneration-medium" },
            ItemUnitSemanticKind.MedicineDoseOrKit,
            "1 heavy whole-body regeneration medium",
            "One complete 14.5-kilogram whole-body regeneration medium transferred as a single heavy medical load.",
            derived,
            PhysicalHaulMassClass.Heavy,
            packagingReviewDisposition:
                PackagingReviewDisposition.IntegralUnitNoDetachableTare);

        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "craft:fang-poison",
                "craft:resin-balm",
                "craft:ritual-reagent",
                "craft:toxic-trap-coating"
            },
            ItemUnitSemanticKind.CatalystOrRelic,
            "1 prepared reagent or coating unit",
            "One complete authored poison, balm, ritual reagent, or trap coating consumed by its target action; no detachable container item exists in the current physical catalog.",
            derived,
            PhysicalHaulMassClass.MicroUrgent,
            packagingReviewDisposition:
                PackagingReviewDisposition.IntegralUnitNoDetachableTare);

        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "food:expedition-ration-pack",
                "food:preserved-ration"
            },
            ItemUnitSemanticKind.MealPortion,
            "1 sealed ration portion",
            "One complete authored preserved or expedition ration consumed as one meal portion; packaging is integral to the current physical unit and exits through the ration-consumption sink.",
            derived,
            PhysicalHaulMassClass.MicroUrgent,
            packagingReviewDisposition:
                PackagingReviewDisposition.IntegralUnitNoDetachableTare);

        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "supply:alliance-signal-kit",
                "supply:botanical-pesticide",
                "supply:certified-seed-kit",
                "supply:defense-mixed-ammo-box",
                "supply:funeral-preparation-kit",
                "supply:fungicide",
                "supply:greenhouse-nutrient",
                "supply:mushroom-substrate",
                "supply:nitrate-fertilizer",
                "supply:performance-prop-box",
                "supply:pest-lure"
            },
            ItemUnitSemanticKind.FacilityInstallationKit,
            "1 operational supply kit or material lot",
            "One complete authored operational supply, treatment material, seed kit, prop box, ammunition box, or installation lot consumed by the named facility or world operation; its handling material is integral to the current item unit.",
            derived,
            PhysicalHaulMassClass.Ordinary,
            packagingReviewDisposition:
                PackagingReviewDisposition.IntegralUnitNoDetachableTare);

        Require(result.Count == ExpectedAuthorityMedicalSupplySemantics,
            $"Expected {ExpectedAuthorityMedicalSupplySemantics} medical/supply semantics, "
            + $"found {result.Count}.");
        return result.ToArray();
    }

    private static CanonicalItemUnitSemantic[]
        BuildAuthorityIntegralSolidConsumables(
            IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        List<CanonicalItemUnitSemantic> result = new();
        AddAuthoritySemantics(
            result,
            items,
            new[] { "medicine:herbal-poultice" },
            ItemUnitSemanticKind.MedicineDoseOrKit,
            "1 complete herbal poultice dressing",
            "One complete authored moonflower-and-shade-fiber poultice applied as the treatment itself; the fibrous dressing is integral material rather than separable package tare.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "supply:inoculated-log" },
            ItemUnitSemanticKind.LogSection,
            "1 inoculated cultivation log section",
            "One complete authored cultivation-log section made from half of a treated-lumber bundle plus cave mushroom inoculum and installed as one fungal-shelf cycle input, without separable package tare.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        Require(result.Count == ExpectedAuthorityIntegralSolidConsumables,
            $"Expected {ExpectedAuthorityIntegralSolidConsumables} integral "
            + $"solid consumables, found {result.Count}.");
        return result.ToArray();
    }

    private static CanonicalItemUnitSemantic[]
        BuildAuthoritySolidMedicalSemantics(
            IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        List<CanonicalItemUnitSemantic> result = new();
        AddAuthoritySemantics(
            result,
            items,
            new[] { "medical:sterile-bandage" },
            ItemUnitSemanticKind.MedicineDoseOrKit,
            "1 sterile bandage dressing",
            "One complete authored sterile bandage dressing whose textile and antiseptic mass is the treatment material itself, without separable package tare.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "medical:rune-hibernation-catalyst",
                "medical:sterile-mycelium-graft"
            },
            ItemUnitSemanticKind.MedicineDoseOrKit,
            "1 solid surgical catalyst or graft",
            "One complete authored rune-hibernation catalyst or sterile mycelium graft physically transferred into the surgical procedure; the solid item itself is the consumed treatment material rather than packaged content.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "medical:mana-core-case",
                "medical:organ-regeneration-scaffold",
                "medical:slime-coagulation-frame"
            },
            ItemUnitSemanticKind.LargeComponent,
            "1 solid surgical frame or case",
            "One complete authored mana-core case, organ-regeneration scaffold, or slime-coagulation frame installed or consumed as the solid surgical component itself without separable package tare.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        Require(result.Count == ExpectedAuthoritySolidMedicalSemantics,
            $"Expected {ExpectedAuthoritySolidMedicalSemantics} solid medical "
            + $"semantics, found {result.Count}.");
        return result.ToArray();
    }

    private static CanonicalItemUnitSemantic[]
        BuildAuthorityUnpackagedMealProcessSemantics(
            IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        List<CanonicalItemUnitSemantic> result = new();
        AddAuthoritySemantics(
            result,
            items,
            new[] { "component:temporal-stasis-seal" },
            ItemUnitSemanticKind.LargeComponent,
            "1 temporal-stasis seal component",
            "One complete authored temporal-stasis seal component consumed or installed as a solid physical BOM unit without separable package tare.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "material:alchemical-solvent" },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 alchemical-solvent process measure",
            "One authored alchemical-solvent process measure transferred in reusable production and warehouse vessels whose mass is bulk infrastructure outside the item unit.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary,
            PackageTareDisposition.BulkInfrastructureNotInUnit);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "food:boar-stew",
                "food:garden-meal",
                "food:salted-meat-stew"
            },
            ItemUnitSemanticKind.MealPortion,
            "1 served meal portion",
            "One complete authored meal serving; reusable bowls and serving ware are facility infrastructure outside the consumable meal unit and do not disappear as package tare.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary,
            PackageTareDisposition.BulkInfrastructureNotInUnit);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "food:jerky" },
            ItemUnitSemanticKind.MealPortion,
            "1 jerky ration portion",
            "One complete authored solid jerky ration portion stored without a separately modeled wrapper or container.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        Require(result.Count == ExpectedAuthorityUnpackagedMealProcessSemantics,
            $"Expected {ExpectedAuthorityUnpackagedMealProcessSemantics} "
            + $"unpackaged meal/process semantics, found {result.Count}.");
        return result.ToArray();
    }

    private static CanonicalItemUnitSemantic[]
        BuildAuthorityProcessedMaterialSemantics(
            IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        List<CanonicalItemUnitSemantic> result = new();
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "material:barrel-steel",
                "material:black-powder",
                "material:cartridge-paper",
                "material:chain-mesh",
                "material:granulated-powder",
                "material:lead-shot",
                "material:mana-alloy",
                "material:niter",
                "material:plate-blank",
                "material:spring-steel",
                "material:sterile-composite"
            },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 manufactured-material lot",
            "One standardized authored metal, powder, paper, mesh, shot, alloy, blank, or composite process lot; warehouse bins and production vessels are bulk infrastructure outside the unit mass.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary,
            PackageTareDisposition.BulkInfrastructureNotInUnit);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "material:cave-silk",
                "material:cloth",
                "material:common-wool",
                "material:deep-goat-wool",
                "material:ember-cotton",
                "material:frost-linen",
                "material:frost-wool",
                "material:hardened-leather",
                "material:leather",
                "material:mire-canvas",
                "material:rope",
                "material:rune-leather",
                "material:sewing-thread",
                "material:spore-hemp"
            },
            ItemUnitSemanticKind.TextileRollOrSheet,
            "1 processed textile or leather lot",
            "One standardized authored cloth, wool, leather, canvas, rope, or thread stock unit transferred without separately modeled disposable packaging.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary,
            PackageTareDisposition.BulkInfrastructureNotInUnit);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "material:bowstring", "material:dreamweave" },
            ItemUnitSemanticKind.TextileRollOrSheet,
            "1 small textile component",
            "One authored bowstring or dreamweave textile component; its current full stack remains below the ordinary 6kg batch minimum.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.MicroUrgent,
            PackageTareDisposition.BulkInfrastructureNotInUnit);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "material:charcoal",
                "material:compost",
                "material:low-fuel",
                "material:ration-mixture",
                "material:rot-toxin",
                "material:salted-meat",
                "material:starch",
                "material:tallow"
            },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 processed organic or fuel lot",
            "One standardized authored fuel, compost, ration mixture, toxin, preserved ingredient, starch, or rendered-fat lot handled in reusable bulk infrastructure.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary,
            PackageTareDisposition.BulkInfrastructureNotInUnit);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "material:laminated-lumber", "material:treated-lumber" },
            ItemUnitSemanticKind.ProcessedLumberBundle,
            "1 processed-lumber bundle",
            "One standardized authored laminated or treated lumber bundle used by an exact physical BOM line.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "material:stone-block" },
            ItemUnitSemanticKind.StoneOrBrickBlock,
            "1 cut stone block",
            "One complete authored cut stone block retained as a discrete construction material unit.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "material:paper" },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 paper-stock bundle",
            "One standardized authored paper-stock bundle used as a divisible physical production input.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "material:mending-scrap" },
            ItemUnitSemanticKind.WasteBundle,
            "1 mending-scrap bundle",
            "One standardized authored recoverable mending-scrap bundle retained for repair or recycling rather than deleted as an abstract credit.",
            PhysicalMassDerivationKind.DerivedByproduct,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "craft:bone-charm",
                "craft:candle",
                "craft:dreamweave-ritual-banner",
                "craft:gold-ornament",
                "craft:soap",
                "craft:stone-ornament"
            },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 complete solid craft item",
            "One complete authored solid charm, candle, ritual banner, ornament, or soap item; liquid coatings, poisons, balms, and reagents remain deferred for packaging review.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "offense:appraised-valuables",
                "offense:unappraised-loot"
            },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 small valuables lot",
            "One small authored expedition-loot or appraised-valuables lot whose full current stack remains below the ordinary 6kg batch minimum.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.MicroUrgent);

        Require(result.Count == ExpectedAuthorityProcessedMaterialSemantics,
            $"Expected {ExpectedAuthorityProcessedMaterialSemantics} processed-material semantics, "
            + $"found {result.Count}.");
        return result.ToArray();
    }

    private static CanonicalItemUnitSemantic[] BuildAuthorityComponentSemantics(
        IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        List<CanonicalItemUnitSemantic> result = new();
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "component:engineering-drawing",
                "component:factory-installation-plan"
            },
            ItemUnitSemanticKind.BlueprintOrRecord,
            "1 engineering document",
            "One complete authored engineering drawing or factory-installation plan consumed as a physical project document.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);

        string[] componentIds =
        {
            "component:blacksteel-defense-plate",
            "component:blast-coat-shell",
            "component:brigandine-padding",
            "component:climate-control-manifold",
            "component:corridor-detonator",
            "component:dreamweave-rune-lining",
            "component:golem-core-case",
            "component:growth-frame",
            "component:insulated-wiring",
            "component:lead-counterweight",
            "component:machine-parts",
            "component:mana-shield-plate",
            "component:material-test-coupon",
            "component:paper-paste",
            "component:powered-armor-joint",
            "component:precision-optics",
            "component:precision-parts",
            "component:price-board",
            "component:prototype-package",
            "component:reclaimed-water-filter",
            "component:room-partition-kit",
            "component:rune-bus-coupler",
            "component:rune-conductor",
            "component:rune-control-panel",
            "component:rune-leather-lining",
            "component:rune-leather-strap",
            "component:rune-purification-crystal",
            "component:rune-tuning-shield",
            "component:sealed-seasonal-container",
            "component:siege-counterweight",
            "component:siege-reinforcement-kit",
            "component:stock-sensor-panel",
            "component:textile-hardener",
            "component:waterwheel-drive-shaft"
        };
        foreach (string itemId in componentIds.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            Require(items.TryGetValue(itemId, out ItemDefinitionSO item),
                $"Authority component semantic item is missing: {itemId}.");
            long grams = MassGrams(item.UnitWeight);
            ItemUnitSemanticKind kind = grams <= 2000L
                ? ItemUnitSemanticKind.SmallComponent
                : ItemUnitSemanticKind.LargeComponent;
            result.Add(new CanonicalItemUnitSemantic(
                itemId,
                kind,
                "1 manufactured component or subassembly",
                "One complete authored manufactured component, installation subassembly, liner, panel, filter, container, or reinforcement kit consumed by an exact BOM line.",
                0,
                0,
                PackageTareDisposition.None,
                string.Empty,
                string.Empty,
                PhysicalMassDerivationKind.RecipeMassBalance,
                new PhysicalMassGrams(grams),
                PhysicalHaulMassClass.Ordinary,
                "mass:semantic-authority:" + itemId + ":v1"));
        }

        Require(result.Count == ExpectedAuthorityComponentSemantics,
            $"Expected {ExpectedAuthorityComponentSemantics} component semantics, "
            + $"found {result.Count}.");
        return result.ToArray();
    }

    private static CanonicalItemUnitSemantic[]
        BuildAuthorityToolProstheticSemantics(
            IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        List<CanonicalItemUnitSemantic> result = new();
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "tool:administrative-seal",
                "tool:banquet-cart",
                "tool:inspection-gauge",
                "tool:prisoner-work-kit",
                "tool:reinforced-restraint",
                "tool:rune-identification-lens",
                "tool:sewing-kit",
                "tool:watch-signal-horn",
                "tool:weather-observation-kit"
            },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 complete reusable tool",
            "One complete authored reusable tool, instrument, restraint, cart, or administrative implement retained as an indivisible physical item.",
            PhysicalMassDerivationKind.EquipmentShapeAndMaterial,
            PhysicalHaulMassClass.IndividualEquipment);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "tool:alloy-crucible",
                "tool:deep-shaft-hoist",
                "tool:field-repair-kit",
                "tool:maintenance-kit",
                "tool:mana-probe",
                "tool:powered-tool-head",
                "tool:precision-gauge",
                "tool:prospecting-kit"
            },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 tool or maintenance kit",
            "One standardized authored tool, replaceable tool head, or maintenance/prospecting kit that can be stocked in ordinary logistics batches.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "surgery:prosthetic:arm:left",
                "surgery:prosthetic:eye:left",
                "surgery:prosthetic:leg:left"
            },
            ItemUnitSemanticKind.LargeComponent,
            "1 complete prosthetic part",
            "One complete authored left-side prosthetic arm, eye, or leg installed as an indivisible surgical component.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.IndividualEquipment);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "husbandry:bedding" },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 husbandry-bedding bundle",
            "One standardized authored animal-bedding bundle used as a physical husbandry input.",
            PhysicalMassDerivationKind.RecipeMassBalance,
            PhysicalHaulMassClass.Ordinary);

        Require(result.Count == ExpectedAuthorityToolProstheticSemantics,
            $"Expected {ExpectedAuthorityToolProstheticSemantics} tool/prosthetic semantics, "
            + $"found {result.Count}.");
        return result.ToArray();
    }

    private static CanonicalItemUnitSemantic[] BuildAuthorityResourceSemantics(
        IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        List<CanonicalItemUnitSemantic> result = new();
        Require(items.TryGetValue("resource:blood", out ItemDefinitionSO blood),
            "Authority resource semantic item is missing: resource:blood.");
        result.Add(new CanonicalItemUnitSemantic(
            "resource:blood",
            ItemUnitSemanticKind.LiquidPortion,
            "0.5 L blood portion",
            "One half-liter physical blood portion obtained from a biological source without disposable packaging.",
            500,
            0,
            PackageTareDisposition.None,
            string.Empty,
            string.Empty,
            PhysicalMassDerivationKind.WorldSource,
            new PhysicalMassGrams(MassGrams(blood.UnitWeight)),
            PhysicalHaulMassClass.Ordinary,
            "mass:semantic-authority:resource:blood:v1"));
        AddAuthoritySemantics(
            result,
            items,
            new[] { "resource:bloodleaf", "resource:moonflower" },
            ItemUnitSemanticKind.ProduceBundle,
            "1 medicinal-herb bundle",
            "One standardized authored medicinal-herb harvest bundle without disposable packaging.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "resource:bone",
                "resource:fang",
                "resource:fat",
                "resource:hide",
                "resource:horn",
                "resource:wool"
            },
            ItemUnitSemanticKind.AnimalProductPortion,
            "1 animal-material bundle",
            "One standardized authored animal-material portion or bundle produced by hunting, husbandry, or butchery.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "resource:feather" },
            ItemUnitSemanticKind.AnimalProductPortion,
            "1 feather bundle",
            "One small authored feather bundle; its current maximum stack remains below the ordinary 6kg batch minimum.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.MicroUrgent);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "resource:coal",
                "resource:gold-ore",
                "resource:iron-ore",
                "resource:lead-ore",
                "resource:sulfur"
            },
            ItemUnitSemanticKind.OreChunkOrBasket,
            "1 mined mineral lot",
            "One standardized authored ore, coal, or sulfur lot extracted from a world resource node.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "resource:stone" },
            ItemUnitSemanticKind.StoneOrBrickBlock,
            "1 quarried stone block",
            "One standardized authored stone block quarried from a world resource node.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "resource:dark-resin" },
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 resin bundle",
            "One standardized authored dark-resin collection bundle without disposable packaging.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "resource:mana-crystal" },
            ItemUnitSemanticKind.CatalystOrRelic,
            "1 mana crystal",
            "One complete authored mana crystal retained as an indivisible physical catalyst unit.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "resource:rune-dust" },
            ItemUnitSemanticKind.CatalystOrRelic,
            "1 rune-dust sachet",
            "One small authored rune-dust measure; its current maximum stack remains below the ordinary 6kg batch minimum.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.MicroUrgent);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "resource:shade-fiber" },
            ItemUnitSemanticKind.TextileRollOrSheet,
            "1 shade-fiber bundle",
            "One standardized authored shade-fiber harvest bundle used as a physical textile input.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "resource:trail-charm" },
            ItemUnitSemanticKind.CatalystOrRelic,
            "1 trail charm",
            "One complete authored trail charm consumed as a single information-unlock catalyst.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.MicroUrgent);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "resource:manure" },
            ItemUnitSemanticKind.WasteBundle,
            "1 manure bundle",
            "One standardized authored manure bundle produced by an animal source and retained for hauling or downstream processing.",
            PhysicalMassDerivationKind.WorldSource,
            PhysicalHaulMassClass.Ordinary);

        Require(result.Count == ExpectedAuthorityResourceSemantics,
            $"Expected {ExpectedAuthorityResourceSemantics} authority resource semantics, "
            + $"found {result.Count}.");
        return result.ToArray();
    }

    private static CanonicalItemUnitSemantic[] BuildAuthorityCommoditySemantics(
        IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        const PhysicalMassDerivationKind derived =
            PhysicalMassDerivationKind.RecipeMassBalance;
        List<CanonicalItemUnitSemantic> result = new();
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "ammo:armor-piercing-cartridge",
                "ammo:arrow-bone",
                "ammo:arrow-iron",
                "ammo:arrow-rune",
                "ammo:arrow-steel",
                "ammo:blacksteel-bolt",
                "ammo:blasting-charge",
                "ammo:bolt-bone",
                "ammo:bolt-iron",
                "ammo:bolt-rune",
                "ammo:bolt-steel",
                "ammo:incendiary-arrow",
                "ammo:incendiary-bolt",
                "ammo:mana-disruptor-bolt",
                "ammo:paper-cartridge",
                "ammo:rune-cartridge",
                "ammo:scatter-cartridge",
                "ammo:signal-flare",
                "ammo:smoke-cartridge",
                "ammo:tranquilizer-dart",
                "ammo:trap-canister"
            },
            ItemUnitSemanticKind.AmmunitionUnitOrPack,
            "1 ammunition unit",
            "One authored projectile, cartridge, charge, flare, dart, or canister consumed as one ammunition unit; reusable weapon parts are not included.",
            derived,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "book:seasonal-almanac",
                "record:arcane-index",
                "record:breeding-ledger",
                "record:career-ledger"
            },
            ItemUnitSemanticKind.BlueprintOrRecord,
            "1 bound record or volume",
            "One complete authored book, index, or ledger retained as a single physical record rather than a divisible paper bundle.",
            derived,
            PhysicalHaulMassClass.MicroUrgent);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "waste:animal-rot",
                "waste:forbidden-rot",
                "waste:mixed-rot",
                "waste:plant-rot"
            },
            ItemUnitSemanticKind.WasteBundle,
            "1 standardized waste bundle",
            "One haulable authored waste bundle produced by spoilage or processing; treatment and disposal remain explicit downstream transforms or sinks.",
            PhysicalMassDerivationKind.DerivedByproduct,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "material:blacksteel-ingot",
                "material:gold-ingot",
                "material:lead-ingot",
                "material:steel-ingot"
            },
            ItemUnitSemanticKind.MetalIngot,
            "1 standardized metal ingot",
            "One complete authored metal ingot; smelting input, slag, and declared process loss are audited at the producing recipe boundary.",
            derived,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "fiber:cave-silk",
                "fiber:deep-goat-wool",
                "fiber:ember-cotton",
                "fiber:frost-flax",
                "fiber:frost-wool",
                "fiber:mire-reed",
                "fiber:spore-hemp"
            },
            ItemUnitSemanticKind.TextileRollOrSheet,
            "1 raw-fiber bundle",
            "One standardized authored raw-fiber bundle used as a divisible textile-production input without disposable packaging.",
            derived,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "yarn:cave-silk",
                "yarn:common-wool",
                "yarn:deep-goat-wool",
                "yarn:dreamweave",
                "yarn:ember-cotton",
                "yarn:frost-linen",
                "yarn:frost-wool",
                "yarn:mire-canvas",
                "yarn:shade-cloth",
                "yarn:spore-hemp"
            },
            ItemUnitSemanticKind.TextileRollOrSheet,
            "1 yarn skein",
            "One standardized authored yarn skein used as a divisible apparel-production input without disposable packaging.",
            derived,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[]
            {
                "textile:insulating-cloth",
                "textile:quilted-liner",
                "textile:sterile-cloth"
            },
            ItemUnitSemanticKind.TextileRollOrSheet,
            "1 textile roll or sheet",
            "One complete authored processed-textile roll, liner, or sterile sheet used as a physical crafting input.",
            derived,
            PhysicalHaulMassClass.Ordinary);
        AddAuthoritySemantics(
            result,
            items,
            new[] { "feed:dog-food", "feed:dog-food-fresh" },
            ItemUnitSemanticKind.ProduceBundle,
            "1 animal-feed ration",
            "One authored dog-food ration consumed as a single animal-feeding unit without disposable packaging.",
            derived,
            PhysicalHaulMassClass.Ordinary);

        Require(result.Count == ExpectedAuthorityCommoditySemantics,
            $"Expected {ExpectedAuthorityCommoditySemantics} authority commodity semantics, "
            + $"found {result.Count}.");
        return result.ToArray();
    }

    private static void AddAuthoritySemantics(
        ICollection<CanonicalItemUnitSemantic> output,
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        IEnumerable<string> itemIds,
        ItemUnitSemanticKind semanticKind,
        string unitLabel,
        string unitDescription,
        PhysicalMassDerivationKind derivationKind,
        PhysicalHaulMassClass haulClass,
        PackageTareDisposition packageTareDisposition =
            PackageTareDisposition.None,
        PackagingReviewDisposition packagingReviewDisposition =
            PackagingReviewDisposition.Unspecified)
    {
        foreach (string itemId in itemIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            Require(items.TryGetValue(itemId, out ItemDefinitionSO item),
                $"Authority commodity semantic item is missing: {itemId}.");
            long grams = MassGrams(item.UnitWeight);
            output.Add(new CanonicalItemUnitSemantic(
                itemId,
                semanticKind,
                unitLabel,
                unitDescription,
                0,
                0,
                packageTareDisposition,
                string.Empty,
                string.Empty,
                derivationKind,
                new PhysicalMassGrams(grams),
                haulClass,
                "mass:semantic-authority:" + itemId + ":v1",
                packagingReviewDisposition));
        }
    }

    private static CanonicalItemUnitSemantic BuildEquipmentSemantic(
        CombatEquipmentDefinitionSO definition,
        IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        Require(definition != null, "Equipment semantic definition is null.");
        Require(items.TryGetValue(definition.ItemId, out ItemDefinitionSO item),
            $"Equipment semantic item is missing: {definition.EquipmentId}.");
        long grams = MassGrams(item.UnitWeight);
        Require(grams == MassGrams(definition.Weight),
            $"Equipment semantic mass authority drifted: {definition.EquipmentId}.");
        ItemUnitSemanticKind kind = definition.Kind switch
        {
            CombatEquipmentKind.Armor => ItemUnitSemanticKind.ArmorPiece,
            CombatEquipmentKind.Shield => ItemUnitSemanticKind.Shield,
            CombatEquipmentKind.MeleeWeapon => ItemUnitSemanticKind.Weapon,
            CombatEquipmentKind.RangedWeapon => ItemUnitSemanticKind.Weapon,
            CombatEquipmentKind.RecoverableThrowingWeapon =>
                ItemUnitSemanticKind.Weapon,
            _ => throw new InvalidOperationException(
                $"Unsupported equipment semantic kind: {definition.Kind}.")
        };
        return new CanonicalItemUnitSemantic(
            definition.ItemId,
            kind,
            "1 complete combat equipment item",
            "One complete authored combat-equipment item; attached modules and loaded ammunition are separate dynamic physical-mass components.",
            0,
            0,
            PackageTareDisposition.None,
            string.Empty,
            string.Empty,
            PhysicalMassDerivationKind.EquipmentShapeAndMaterial,
            new PhysicalMassGrams(grams),
            ClassifyIndividualEquipment(grams),
            "mass:" + definition.EquipmentId + ":authority:v1");
    }

    private static CanonicalItemUnitSemantic BuildApparelSemantic(
        IGrouping<string, ApparelDefinitionSO> definitions,
        IReadOnlyDictionary<string, ItemDefinitionSO> items)
    {
        string itemId = definitions.Key;
        Require(!string.IsNullOrWhiteSpace(itemId),
            "Apparel semantic has an empty physical item ID.");
        Require(items.TryGetValue(itemId, out ItemDefinitionSO item),
            $"Apparel semantic item is missing: {itemId}.");
        ApparelDefinitionSO[] ordered = definitions
            .OrderBy(value => value.ApparelId, StringComparer.Ordinal)
            .ToArray();
        Require(ordered.Length > 0, $"Apparel semantic group is empty: {itemId}.");
        long grams = MassGrams(item.UnitWeight);
        return new CanonicalItemUnitSemantic(
            itemId,
            ItemUnitSemanticKind.ApparelPiece,
            "1 complete apparel item",
            "One complete authored apparel item; fitted body-form definitions share this physical item and textile variants are projected by the apparel mass authority.",
            0,
            0,
            PackageTareDisposition.None,
            string.Empty,
            string.Empty,
            PhysicalMassDerivationKind.ApparelShapeAndTextile,
            new PhysicalMassGrams(grams),
            ClassifyIndividualEquipment(grams),
            "mass:apparel-item:" + itemId + ":authority:v1");
    }

    private static PhysicalHaulMassClass ClassifyIndividualEquipment(long grams)
    {
        if (grams <= 0)
            throw new ArgumentOutOfRangeException(nameof(grams));
        if (grams <= 11000)
            return PhysicalHaulMassClass.IndividualEquipment;
        if (grams <= 20000)
            return PhysicalHaulMassClass.Heavy;
        return PhysicalHaulMassClass.OversizeEquipment;
    }

    private static CanonicalItemUnitSemantic[] ExplicitPrimitiveSemantics() => new[]
    {
        new CanonicalItemUnitSemantic(
            "resource:clean-water",
            ItemUnitSemanticKind.LiquidPortion,
            "0.5 L water unit",
            "One half-liter clean-water unit transferred from bulk water infrastructure.",
            500,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:water",
            PhysicalMassDerivationKind.VolumeDensity,
            new PhysicalMassGrams(500),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:resource:clean-water:v1"),
        new CanonicalItemUnitSemantic(
            "resource:twilight-grain",
            ItemUnitSemanticKind.ProduceBundle,
            "0.5 L grain measure",
            "One half-liter dry twilight-grain measure without disposable packaging.",
            500,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:twilight-grain",
            PhysicalMassDerivationKind.VolumeDensity,
            new PhysicalMassGrams(350),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:resource:twilight-grain:v1"),
        new CanonicalItemUnitSemantic(
            "food:grain-porridge",
            ItemUnitSemanticKind.MealPortion,
            "1 meal portion",
            "One edible grain-porridge serving; tableware is reusable infrastructure, not tare.",
            350,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:cooked-grain",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(600),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:grain-porridge:v1"),
        new CanonicalItemUnitSemantic(
            "resource:log",
            ItemUnitSemanticKind.LogSection,
            "1 cut log section",
            "One standardized green-wood log section delivered from a world resource node.",
            3000,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:green-wood",
            PhysicalMassDerivationKind.WorldSource,
            new PhysicalMassGrams(1800),
            PhysicalHaulMassClass.Ordinary,
            "mass:resource:log:v1"),
        new CanonicalItemUnitSemantic(
            "material:lumber",
            ItemUnitSemanticKind.ProcessedLumberBundle,
            "1 lumber bundle",
            "One standardized 1,200-gram processed-lumber bundle; the current sawmill batch divides two log sections exactly into three bundles.",
            2000,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:dry-lumber",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(1200),
            PhysicalHaulMassClass.Ordinary,
            "mass:recipe:sawmill-lumber:v1"),
        new CanonicalItemUnitSemantic(
            "resource:cave-mushroom",
            ItemUnitSemanticKind.ProduceBundle,
            "1 harvest basket",
            "One 250-gram cave-mushroom harvest basket without disposable packaging.",
            750,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:cave-mushroom",
            PhysicalMassDerivationKind.WorldSource,
            new PhysicalMassGrams(250),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:resource:cave-mushroom:v1"),
        new CanonicalItemUnitSemantic(
            "food:mushroom-soup",
            ItemUnitSemanticKind.MealPortion,
            "1 meal portion",
            "One edible mushroom-soup serving; tableware is reusable infrastructure, not tare.",
            280,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:mushroom-soup",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(650),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:mushroom-soup:v1"),
        new CanonicalItemUnitSemantic(
            "resource:meat",
            ItemUnitSemanticKind.AnimalProductPortion,
            "1 butchered cut",
            "One standardized 700-gram butchered meat cut without disposable packaging.",
            700,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:raw-meat",
            PhysicalMassDerivationKind.WorldSource,
            new PhysicalMassGrams(700),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:resource:meat:v1"),
        new CanonicalItemUnitSemantic(
            "food:roasted-meat",
            ItemUnitSemanticKind.MealPortion,
            "1 meal portion",
            "One roasted-meat serving after explicit cooking moisture loss.",
            600,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:roasted-meat",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(750),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:roasted-meat:v1"),
        new CanonicalItemUnitSemantic(
            "resource:ember-root",
            ItemUnitSemanticKind.ProduceBundle,
            "1 root harvest bundle",
            "One standardized 450-gram ember-root harvest bundle without disposable packaging.",
            500,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:ember-root",
            PhysicalMassDerivationKind.WorldSource,
            new PhysicalMassGrams(450),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:resource:ember-root:v1"),
        new CanonicalItemUnitSemantic(
            "food:root-stew",
            ItemUnitSemanticKind.MealPortion,
            "1 meal portion",
            "One edible ember-root stew serving; tableware is reusable infrastructure, not tare.",
            470,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:root-stew",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(700),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:root-stew:v1"),
        new CanonicalItemUnitSemantic(
            "resource:milk",
            ItemUnitSemanticKind.AnimalProductPortion,
            "0.8 L milk measure",
            "One 0.8-liter milk measure; the reusable pail is livestock infrastructure, not item tare.",
            800,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:whole-milk",
            PhysicalMassDerivationKind.WorldSource,
            new PhysicalMassGrams(800),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:source:animal-milk:v1"),
        new CanonicalItemUnitSemantic(
            "resource:egg",
            ItemUnitSemanticKind.AnimalProductPortion,
            "1 four-egg collection",
            "One standardized four-egg collection with shell mass included and no disposable packaging.",
            250,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:whole-egg",
            PhysicalMassDerivationKind.WorldSource,
            new PhysicalMassGrams(250),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:source:animal-egg:v1"),
        new CanonicalItemUnitSemantic(
            "material:flour",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 mill measure",
            "One 300-gram flour measure transferred from the mill without disposable packaging.",
            800,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:flour",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(300),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:milling-flour:v1"),
        new CanonicalItemUnitSemantic(
            "food:egg-pancake",
            ItemUnitSemanticKind.MealPortion,
            "1 meal portion",
            "One egg-pancake meal portion; cookware and tableware are reusable infrastructure, not tare.",
            760,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:egg-pancake",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(650),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:egg-pancake:v1"),
        new CanonicalItemUnitSemantic(
            "resource:saltstone",
            ItemUnitSemanticKind.OreChunkOrBasket,
            "1 saltstone chunk",
            "One standardized 500-gram saltstone chunk from surface quarrying.",
            300,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:saltstone",
            PhysicalMassDerivationKind.WorldSource,
            new PhysicalMassGrams(500),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:source:saltstone:v1"),
        new CanonicalItemUnitSemantic(
            "material:curd",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 drained curd batch",
            "One 450-gram drained curd batch after whey is discharged to the wastewater network.",
            430,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:curd",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(450),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:curd:v1"),
        new CanonicalItemUnitSemantic(
            "food:fresh-curd",
            ItemUnitSemanticKind.MealPortion,
            "1 small meal portion",
            "One 225-gram fresh-curd serving; reusable tableware is not item tare.",
            215,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:fresh-curd",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(225),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:fresh-curd:v1"),
        new CanonicalItemUnitSemantic(
            "material:cheese",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 aged cheese portion",
            "One 400-gram aged cheese portion after explicit moisture loss on the cheese rack.",
            380,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:aged-cheese",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(400),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:cheese:v1"),
        new CanonicalItemUnitSemantic(
            "food:cheese-mushroom",
            ItemUnitSemanticKind.MealPortion,
            "1 meal portion",
            "One 450-gram cheese-and-mushroom meal portion; reusable cookware and tableware are not item tare.",
            430,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:cheese-mushroom",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(450),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:cheese-mushroom:v1"),
        new CanonicalItemUnitSemantic(
            "resource:night-grape",
            ItemUnitSemanticKind.ProduceBundle,
            "1 grape harvest cluster",
            "One standardized 250-gram night-grape harvest cluster without disposable packaging.",
            270,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:night-grape",
            PhysicalMassDerivationKind.WorldSource,
            new PhysicalMassGrams(250),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:resource:night-grape:v1"),
        new CanonicalItemUnitSemantic(
            "food:lavish-meat",
            ItemUnitSemanticKind.MealPortion,
            "1 lavish meal portion",
            "One 1000-gram lavish meat meal portion; reusable cookware and tableware are not item tare.",
            900,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:lavish-meat",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(1000),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:lavish-meat:v1"),
        new CanonicalItemUnitSemantic(
            "material:malt",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 malt measure",
            "One 350-gram malt measure produced from one grain measure without disposable packaging.",
            500,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:malt",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(350),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:malt:v1"),
        new CanonicalItemUnitSemantic(
            "material:syrup",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 bulk syrup measure",
            "One 350-gram concentrated night-grape syrup measure transferred in reusable production vessels.",
            300,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:night-grape-syrup",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(350),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:syrup:v1"),
        new CanonicalItemUnitSemantic(
            "material:grape-juice",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 grape-juice measure",
            "One 375-gram grape-juice measure transferred in reusable production vessels.",
            360,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:grape-juice",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(375),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:grape-juice:v1"),
        new CanonicalItemUnitSemantic(
            "food:grape-syrup",
            ItemUnitSemanticKind.MealPortion,
            "1 syrup serving",
            "One 175-gram concentrated grape-syrup serving; reusable tableware is not item tare.",
            150,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:grape-syrup-serving",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(175),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:grape-syrup:v1"),
        new CanonicalItemUnitSemantic(
            "material:fermented-liquor",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 fermented-liquor measure",
            "One 500-gram malt fermentation measure transferred in reusable production vessels.",
            500,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:fermented-liquor",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(500),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:fermented-liquor:v1"),
        new CanonicalItemUnitSemantic(
            "food:malt-porridge",
            ItemUnitSemanticKind.MealPortion,
            "1 malt-porridge serving",
            "One 550-gram full-meal serving made from malt and process water; reusable tableware is not item tare.",
            550,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:malt-porridge",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(550),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:malt-porridge:v1"),
        new CanonicalItemUnitSemantic(
            "material:young-wine",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 young-wine measure",
            "One 350-gram young-wine measure after explicit fermentation-gas loss; reusable vessels are not item tare.",
            350,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:young-wine",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(350),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:young-wine:v1"),
        new CanonicalItemUnitSemantic(
            "food:twilight-beer",
            ItemUnitSemanticKind.LiquidPortion,
            "1 twilight-beer serving",
            "One 475-gram beer serving after secondary fermentation-gas loss; reusable vessels are not item tare.",
            475,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:twilight-beer",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(475),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:twilight-beer:v1"),
        new CanonicalItemUnitSemantic(
            "drug:night-wine",
            ItemUnitSemanticKind.LiquidPortion,
            "1 night-wine serving",
            "One 325-gram aged night-wine serving after explicit cask evaporation; reusable vessels are not item tare.",
            325,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:night-wine",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(325),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:night-wine:v1"),
        new CanonicalItemUnitSemantic(
            "food:night-spirit",
            ItemUnitSemanticKind.LiquidPortion,
            "1 night-spirit serving",
            "One 450-gram oak-aged spirit serving; reusable vessels are not item tare.",
            450,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:night-spirit",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(450),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:night-spirit:v1"),
        new CanonicalItemUnitSemantic(
            "material:alcohol",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 alcohol measure",
            "One 500-gram bulk alcohol measure transferred in reusable production vessels.",
            500,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:alcohol",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(500),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:alcohol:v1"),
        new CanonicalItemUnitSemantic(
            "material:washed-vegetable",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 washed-vegetable portion",
            "One 450-gram cleaned ember-root portion transferred in reusable preparation vessels.",
            450,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:washed-vegetable",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(450),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:washed-vegetable:v1"),
        new CanonicalItemUnitSemantic(
            "material:brined-vegetable",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 brined-vegetable portion",
            "One 500-gram drained brined-vegetable portion; discarded brine is process wastewater.",
            450,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:brined-vegetable",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(500),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:brined-vegetable:v1"),
        new CanonicalItemUnitSemantic(
            "craft:fermented-vinegar",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 fermented-vinegar measure",
            "One 400-gram fermented-vinegar measure transferred in reusable production vessels.",
            400,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:fermented-vinegar",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(400),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:fermented-vinegar:v1"),
        new CanonicalItemUnitSemantic(
            "food:fermented-pickle",
            ItemUnitSemanticKind.MealPortion,
            "1 fermented-pickle serving",
            "One 450-gram drained fermented-pickle serving; reusable tableware is not item tare.",
            400,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:fermented-pickle",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(450),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:fermented-pickle:v1"),
        new CanonicalItemUnitSemantic(
            "food:preserved-vegetable",
            ItemUnitSemanticKind.MealPortion,
            "1 preserved-vegetable serving",
            "One 550-gram cooked preserved-vegetable serving; reusable tableware is not item tare.",
            500,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:preserved-vegetable",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(550),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:preserved-vegetable:v1"),
        new CanonicalItemUnitSemantic(
            "material:dough",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 dough portion",
            "One 500-gram prepared dough portion transferred in reusable preparation vessels.",
            500,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:dough",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(500),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:dough:v1"),
        new CanonicalItemUnitSemantic(
            "material:seasoned-filling",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 seasoned-filling portion",
            "One 650-gram prepared meat-and-vegetable filling portion transferred in reusable preparation vessels.",
            650,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:seasoned-filling",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(650),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:seasoned-filling:v1"),
        new CanonicalItemUnitSemantic(
            "food:vegetable-pie",
            ItemUnitSemanticKind.MealPortion,
            "1 vegetable-pie serving",
            "One 475-gram vegetable-pie serving; reusable tableware is not item tare.",
            450,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:vegetable-pie",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(475),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:vegetable-pie:v1"),
        new CanonicalItemUnitSemantic(
            "food:stuffed-mushroom",
            ItemUnitSemanticKind.MealPortion,
            "1 stuffed-mushroom serving",
            "One 575-gram stuffed-mushroom serving; reusable tableware is not item tare.",
            550,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:stuffed-mushroom",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(575),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:stuffed-mushroom:v1"),
        new CanonicalItemUnitSemantic(
            "resource:grass-straw",
            ItemUnitSemanticKind.ProduceBundle,
            "1 grass-and-straw sheaf",
            "One 80-gram dry grass-and-straw sheaf gathered from an exterior source.",
            250,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:grass-straw",
            PhysicalMassDerivationKind.ExplicitPrimitive,
            new PhysicalMassGrams(80),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:resource:grass-straw:v1"),
        new CanonicalItemUnitSemantic(
            "feed:hay",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 hay-feed ration",
            "One 196-gram compact dry hay ration without disposable packaging.",
            500,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:hay-feed",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(196),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:hay-feed:v1"),
        new CanonicalItemUnitSemantic(
            "feed:silage",
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "1 silage ration",
            "One 230-gram moist fermented-feed ration transferred from reusable storage.",
            250,
            0,
            PackageTareDisposition.BulkInfrastructureNotInUnit,
            string.Empty,
            "mass-material:silage",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(230),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:silage:v1"),
        new CanonicalItemUnitSemantic(
            "food:meat-pie",
            ItemUnitSemanticKind.MealPortion,
            "1 meat-pie serving",
            "One 575-gram meat-pie serving; reusable tableware is not item tare.",
            550,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:meat-pie",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(575),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:meat-pie:v1"),
        new CanonicalItemUnitSemantic(
            "food:lavish-vegan",
            ItemUnitSemanticKind.MealPortion,
            "1 lavish-vegan serving",
            "One 900-gram lavish vegan meal serving; reusable tableware is not item tare.",
            800,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:lavish-vegan",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(900),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:lavish-vegan:v1"),
        new CanonicalItemUnitSemantic(
            "resource:dreamleaf",
            ItemUnitSemanticKind.ProduceBundle,
            "1 dreamleaf herb bundle",
            "One 80-gram harvested dreamleaf bundle used for medicine extraction.",
            120,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:dreamleaf",
            PhysicalMassDerivationKind.ExplicitPrimitive,
            new PhysicalMassGrams(80),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:primitive:dreamleaf:v1"),
        new CanonicalItemUnitSemantic(
            "material:iron-ingot",
            ItemUnitSemanticKind.MetalIngot,
            "1 iron ingot",
            "One 900-gram forged iron ingot used as a conserved metal stock unit.",
            115,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:iron-ingot",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(900),
            PhysicalHaulMassClass.Ordinary,
            "mass:recipe:iron-ingot:v1"),
        new CanonicalItemUnitSemantic(
            "container:medical-vial",
            ItemUnitSemanticKind.SmallComponent,
            "1 reusable medical vial",
            "One 30-gram forged iron vial that remains physical packaging through medicine production and returns after terminal use.",
            30,
            0,
            PackageTareDisposition.None,
            string.Empty,
            "mass-material:medical-vial-iron",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(30),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:medical-vial:v1"),
        new CanonicalItemUnitSemantic(
            "medicine:anesthetic",
            ItemUnitSemanticKind.MedicineDoseOrKit,
            "1 packaged anesthetic dose",
            "One 120-gram anesthetic dose containing 90 grams of medicine and one reusable 30-gram medical vial.",
            120,
            30,
            PackageTareDisposition.ReusableContainerReturn,
            "container:medical-vial",
            "mass-material:anesthetic-solution",
            PhysicalMassDerivationKind.RecipeMassBalance,
            new PhysicalMassGrams(120),
            PhysicalHaulMassClass.MicroUrgent,
            "mass:recipe:anesthetic:v1",
            PackagingReviewDisposition.DetachableTare)
    };

    private static MaterialMassProfile[] MaterialProfiles() => new[]
    {
        new MaterialMassProfile("mass-material:cooked-grain", 1035, 480, 1000, 999),
        new MaterialMassProfile("mass-material:dry-lumber", 550, 120, 1000, 917),
        new MaterialMassProfile("mass-material:green-wood", 600, 180, 1000, 917),
        new MaterialMassProfile("mass-material:cave-mushroom", 333, 900, 1000, 990),
        new MaterialMassProfile("mass-material:mushroom-soup", 1018, 870, 1000, 992),
        new MaterialMassProfile("mass-material:raw-meat", 1000, 700, 1000, 900),
        new MaterialMassProfile("mass-material:roasted-meat", 1050, 560, 1000, 900),
        new MaterialMassProfile("mass-material:ember-root", 900, 760, 1000, 990),
        new MaterialMassProfile("mass-material:root-stew", 1020, 820, 1000, 985),
        new MaterialMassProfile("mass-material:whole-milk", 1000, 870, 1000, 995),
        new MaterialMassProfile("mass-material:whole-egg", 1000, 750, 1000, 950),
        new MaterialMassProfile("mass-material:flour", 625, 140, 1000, 952),
        new MaterialMassProfile("mass-material:egg-pancake", 1050, 520, 1000, 871),
        new MaterialMassProfile("mass-material:saltstone", 2160, 20, 1000, 1000),
        new MaterialMassProfile("mass-material:curd", 1050, 700, 1000, 310),
        new MaterialMassProfile("mass-material:fresh-curd", 1050, 680, 1000, 1000),
        new MaterialMassProfile("mass-material:aged-cheese", 1100, 420, 1000, 889),
        new MaterialMassProfile("mass-material:cheese-mushroom", 1000, 660, 1000, 1000),
        new MaterialMassProfile("mass-material:night-grape", 925, 820, 1000, 1000),
        new MaterialMassProfile("mass-material:lavish-meat", 1110, 610, 1000, 870),
        new MaterialMassProfile("mass-material:malt", 700, 90, 1000, 1000),
        new MaterialMassProfile("mass-material:night-grape-syrup", 1167, 260, 1000, 933),
        new MaterialMassProfile("mass-material:grape-juice", 1042, 860, 1000, 938),
        new MaterialMassProfile("mass-material:grape-syrup-serving", 1167, 260, 1000, 933),
        new MaterialMassProfile("mass-material:fermented-liquor", 1000, 900, 1000, 952),
        new MaterialMassProfile("mass-material:malt-porridge", 1000, 850, 1000, 1000),
        new MaterialMassProfile("mass-material:young-wine", 1000, 900, 1000, 933),
        new MaterialMassProfile("mass-material:twilight-beer", 1000, 900, 1000, 950),
        new MaterialMassProfile("mass-material:night-wine", 1000, 880, 1000, 929),
        new MaterialMassProfile("mass-material:night-spirit", 950, 700, 1000, 857),
        new MaterialMassProfile("mass-material:alcohol", 900, 0, 1000, 1000),
        new MaterialMassProfile("mass-material:washed-vegetable", 900, 760, 1000, 1000),
        new MaterialMassProfile("mass-material:brined-vegetable", 1000, 700, 1000, 714),
        new MaterialMassProfile("mass-material:fermented-vinegar", 1000, 950, 1000, 941),
        new MaterialMassProfile("mass-material:fermented-pickle", 1000, 650, 1000, 643),
        new MaterialMassProfile("mass-material:preserved-vegetable", 1000, 600, 1000, 815),
        new MaterialMassProfile("mass-material:dough", 1050, 500, 1000, 755),
        new MaterialMassProfile("mass-material:seasoned-filling", 1050, 620, 1000, 703),
        new MaterialMassProfile("mass-material:vegetable-pie", 950, 550, 1000, 1000),
        new MaterialMassProfile("mass-material:stuffed-mushroom", 950, 650, 1000, 1000),
        new MaterialMassProfile("mass-material:grass-straw", 160, 80, 1000, 1000),
        new MaterialMassProfile("mass-material:hay-feed", 220, 60, 1000, 997),
        new MaterialMassProfile("mass-material:silage", 920, 700, 1000, 1000),
        new MaterialMassProfile("mass-material:meat-pie", 1000, 550, 1000, 1000),
        new MaterialMassProfile("mass-material:lavish-vegan", 1050, 650, 1000, 939),
        new MaterialMassProfile("mass-material:twilight-grain", 700, 120, 1000, 1000),
        new MaterialMassProfile("mass-material:water", 1000, 1000, 1000, 1000),
        new MaterialMassProfile("mass-material:dreamleaf", 410, 700, 1000, 1000),
        new MaterialMassProfile("mass-material:iron-ingot", 7870, 0, 1000, 1000),
        new MaterialMassProfile("mass-material:medical-vial-iron", 7870, 0, 1000, 1000),
        new MaterialMassProfile("mass-material:anesthetic-solution", 1000, 0, 1000, 174)
    };

    private static PhysicalMassTransformContract[] BuildTransforms(
        IReadOnlyDictionary<string, ProductionRecipeSO> recipes,
        IReadOnlyList<CanonicalItemUnitSemantic> semantics)
    {
        ProductionRecipeSO porridge = recipes["recipe:grain-porridge"];
        ProductionRecipeSO sawmill = recipes["recipe:sawmill-lumber"];
        ProductionRecipeSO mushroomSoup = recipes["recipe:mushroom-soup"];
        ProductionRecipeSO roastedMeat = recipes["recipe:roasted-meat"];
        ProductionRecipeSO rootStew = recipes["recipe:root-stew"];
        ProductionRecipeSO millingFlour = recipes["recipe:milling-flour"];
        ProductionRecipeSO eggPancake = recipes["recipe:egg-pancake"];
        ProductionRecipeSO curd = recipes["recipe:curd"];
        ProductionRecipeSO freshCurd = recipes["recipe:fresh-curd"];
        ProductionRecipeSO cheese = recipes["recipe:cheese"];
        ProductionRecipeSO cheeseMushroom = recipes["recipe:cheese-mushroom"];
        ProductionRecipeSO lavishMeat = recipes["recipe:lavish-meat"];
        ProductionRecipeSO malt = recipes["recipe:malt"];
        ProductionRecipeSO syrup = recipes["recipe:syrup"];
        ProductionRecipeSO grapeJuice = recipes["recipe:grape-juice"];
        ProductionRecipeSO grapeSyrup = recipes["recipe:grape-syrup"];
        ProductionRecipeSO fermentedLiquor = recipes["recipe:fermented-liquor"];
        ProductionRecipeSO maltPorridge = recipes["recipe:malt-porridge"];
        ProductionRecipeSO youngWine = recipes["recipe:young-wine"];
        ProductionRecipeSO twilightBeer = recipes["recipe:twilight-beer"];
        ProductionRecipeSO nightWine = recipes["recipe:night-wine"];
        ProductionRecipeSO nightSpirit = recipes["recipe:night-spirit"];
        ProductionRecipeSO alcohol = recipes["recipe:alcohol"];
        ProductionRecipeSO washedVegetable = recipes["recipe:washed-vegetable"];
        ProductionRecipeSO brinedVegetable = recipes["recipe:brined-vegetable"];
        ProductionRecipeSO fermentedVinegar = recipes["recipe:fermented-vinegar"];
        ProductionRecipeSO fermentedPickle = recipes["recipe:fermented-pickle"];
        ProductionRecipeSO preservedVegetable = recipes["recipe:preserved-vegetable"];
        ProductionRecipeSO dough = recipes["recipe:dough"];
        ProductionRecipeSO seasonedFilling = recipes["recipe:seasoned-filling"];
        ProductionRecipeSO vegetablePie = recipes["recipe:vegetable-pie"];
        ProductionRecipeSO stuffedMushroom = recipes["recipe:stuffed-mushroom"];
        ProductionRecipeSO hayFeed = recipes["recipe:hay-feed"];
        ProductionRecipeSO silage = recipes["recipe:silage"];
        ProductionRecipeSO dogFood = recipes["recipe:dog-food"];
        ProductionRecipeSO freshDogFood = recipes["recipe:dog-food-fresh"];
        ProductionRecipeSO meatPie = recipes["recipe:meat-pie"];
        ProductionRecipeSO lavishVegan = recipes["recipe:lavish-vegan"];
        ProductionRecipeSO medicalVial = recipes["recipe:medical-vial"];
        ProductionRecipeSO anesthetic = recipes["recipe:anesthetic"];
        ProductionRecipeSO granulatedPowder =
            recipes["recipe:material:granulated-powder"];
        ProductionRecipeSO inoculatedLog =
            recipes["recipe:supply:inoculated-log"];
        long waterGrams = MassOf(semantics, "resource:clean-water");
        long grainGrams = MassOf(semantics, "resource:twilight-grain");
        long porridgeGrams = MassOf(semantics, "food:grain-porridge");
        long logGrams = MassOf(semantics, "resource:log");
        long lumberGrams = MassOf(semantics, "material:lumber");
        long mushroomGrams = MassOf(semantics, "resource:cave-mushroom");
        long mushroomSoupGrams = MassOf(semantics, "food:mushroom-soup");
        long meatGrams = MassOf(semantics, "resource:meat");
        long roastedMeatGrams = MassOf(semantics, "food:roasted-meat");
        long emberRootGrams = MassOf(semantics, "resource:ember-root");
        long rootStewGrams = MassOf(semantics, "food:root-stew");
        long milkGrams = MassOf(semantics, "resource:milk");
        long eggGrams = MassOf(semantics, "resource:egg");
        long flourGrams = MassOf(semantics, "material:flour");
        long eggPancakeGrams = MassOf(semantics, "food:egg-pancake");
        long saltstoneGrams = MassOf(semantics, "resource:saltstone");
        long curdGrams = MassOf(semantics, "material:curd");
        long freshCurdGrams = MassOf(semantics, "food:fresh-curd");
        long cheeseGrams = MassOf(semantics, "material:cheese");
        long cheeseMushroomGrams = MassOf(semantics, "food:cheese-mushroom");
        long nightGrapeGrams = MassOf(semantics, "resource:night-grape");
        long lavishMeatGrams = MassOf(semantics, "food:lavish-meat");
        long maltGrams = MassOf(semantics, "material:malt");
        long syrupGrams = MassOf(semantics, "material:syrup");
        long grapeJuiceGrams = MassOf(semantics, "material:grape-juice");
        long grapeSyrupGrams = MassOf(semantics, "food:grape-syrup");
        long fermentedLiquorGrams = MassOf(semantics, "material:fermented-liquor");
        long maltPorridgeGrams = MassOf(semantics, "food:malt-porridge");
        long youngWineGrams = MassOf(semantics, "material:young-wine");
        long twilightBeerGrams = MassOf(semantics, "food:twilight-beer");
        long nightWineGrams = MassOf(semantics, "drug:night-wine");
        long nightSpiritGrams = MassOf(semantics, "food:night-spirit");
        long alcoholGrams = MassOf(semantics, "material:alcohol");
        long washedVegetableGrams = MassOf(semantics, "material:washed-vegetable");
        long brinedVegetableGrams = MassOf(semantics, "material:brined-vegetable");
        long fermentedVinegarGrams = MassOf(semantics, "craft:fermented-vinegar");
        long fermentedPickleGrams = MassOf(semantics, "food:fermented-pickle");
        long preservedVegetableGrams = MassOf(semantics, "food:preserved-vegetable");
        long doughGrams = MassOf(semantics, "material:dough");
        long seasonedFillingGrams = MassOf(semantics, "material:seasoned-filling");
        long vegetablePieGrams = MassOf(semantics, "food:vegetable-pie");
        long stuffedMushroomGrams = MassOf(semantics, "food:stuffed-mushroom");
        long grassStrawGrams = MassOf(semantics, "resource:grass-straw");
        long hayGrams = MassOf(semantics, "feed:hay");
        long silageGrams = MassOf(semantics, "feed:silage");
        long animalRotGrams = MassOf(semantics, "waste:animal-rot");
        long dogFoodGrams = MassOf(semantics, "feed:dog-food");
        long freshDogFoodGrams = MassOf(semantics, "feed:dog-food-fresh");
        long meatPieGrams = MassOf(semantics, "food:meat-pie");
        long lavishVeganGrams = MassOf(semantics, "food:lavish-vegan");
        long dreamleafGrams = MassOf(semantics, "resource:dreamleaf");
        long ironIngotGrams = MassOf(semantics, "material:iron-ingot");
        long medicalVialGrams = MassOf(semantics, "container:medical-vial");
        long anestheticGrams = MassOf(semantics, "medicine:anesthetic");
        long blackPowderGrams = MassOf(semantics, "material:black-powder");
        long paperGrams = MassOf(semantics, "material:paper");
        long granulatedPowderGrams =
            MassOf(semantics, "material:granulated-powder");
        long treatedLumberGrams = MassOf(semantics, "material:treated-lumber");
        long inoculatedLogGrams = MassOf(semantics, "supply:inoculated-log");
        long waterInput = ScaleMass(waterGrams, porridge.CleanWaterPerCycle);
        long wastewater = ScaleMass(waterGrams, porridge.WastewaterPerCycle);
        long porridgeInput = checked(grainGrams * 6);
        long porridgeOutput = checked(porridgeGrams * 6);
        long porridgeLoss = checked(porridgeInput + waterInput - porridgeOutput - wastewater);
        long sawmillInput = checked(logGrams * 2);
        long sawmillOutput = checked(lumberGrams * 3);
        long sawmillLoss = checked(sawmillInput - sawmillOutput);
        long mushroomWater = ScaleMass(waterGrams, mushroomSoup.CleanWaterPerCycle);
        long mushroomWastewater = ScaleMass(waterGrams, mushroomSoup.WastewaterPerCycle);
        long mushroomInput = checked(mushroomGrams * 2);
        long mushroomOutput = checked(mushroomSoupGrams * 2);
        long mushroomLoss = checked(
            mushroomInput + mushroomWater - mushroomOutput - mushroomWastewater);
        long roastedInput = checked(meatGrams * 2);
        long roastedWater = ScaleMass(
            waterGrams,
            roastedMeat.CleanWaterPerCycle);
        long roastedOutput = checked(roastedMeatGrams * 2);
        long roastedLoss = checked(roastedInput + roastedWater - roastedOutput);
        long rootStewWater = ScaleMass(waterGrams, rootStew.CleanWaterPerCycle);
        long rootStewWastewater = ScaleMass(waterGrams, rootStew.WastewaterPerCycle);
        long rootStewInput = checked(emberRootGrams * 2);
        long rootStewOutput = checked(rootStewGrams * 2);
        long rootStewLoss = checked(
            rootStewInput + rootStewWater - rootStewOutput - rootStewWastewater);
        long flourInput = checked(grainGrams * 3);
        long flourOutput = checked(flourGrams * 2);
        long flourLoss = checked(flourInput - flourOutput);
        long pancakePhysicalInput = checked(eggGrams * 2 + flourGrams + milkGrams);
        long pancakeWater = ScaleMass(waterGrams, eggPancake.CleanWaterPerCycle);
        long pancakeWastewater = ScaleMass(waterGrams, eggPancake.WastewaterPerCycle);
        long pancakeOutput = checked(eggPancakeGrams * 2);
        long pancakeLoss = checked(
            pancakePhysicalInput + pancakeWater - pancakeOutput - pancakeWastewater);
        long curdPhysicalInput = checked(milkGrams * 3 + saltstoneGrams);
        long curdWater = ScaleMass(waterGrams, curd.CleanWaterPerCycle);
        long curdWastewater = ScaleMass(waterGrams, curd.WastewaterPerCycle);
        long curdOutput = checked(curdGrams * 2);
        long curdLoss = checked(
            curdPhysicalInput + curdWater - curdOutput - curdWastewater);
        long freshCurdInput = curdGrams;
        long freshCurdOutput = checked(freshCurdGrams * 2);
        long cheeseInput = checked(curdGrams * 2);
        long cheeseOutput = checked(cheeseGrams * 2);
        long cheeseAgingLoss = checked(cheeseInput - cheeseOutput);
        long cheeseMushroomInput = checked(cheeseGrams + mushroomGrams * 2);
        long cheeseMushroomOutput = checked(cheeseMushroomGrams * 2);
        long lavishMeatPhysicalInput = checked(
            meatGrams * 2 + cheeseGrams + nightGrapeGrams * 2);
        long lavishMeatWater = ScaleMass(waterGrams, lavishMeat.CleanWaterPerCycle);
        long lavishMeatWastewater = ScaleMass(
            waterGrams,
            lavishMeat.WastewaterPerCycle);
        long lavishMeatOutput = checked(lavishMeatGrams * 2);
        long lavishMeatLoss = checked(
            lavishMeatPhysicalInput + lavishMeatWater
            - lavishMeatOutput - lavishMeatWastewater);
        long maltInput = checked(grainGrams * 2);
        long maltOutput = checked(maltGrams * 2);
        long syrupInput = checked(nightGrapeGrams * 3);
        long syrupOutput = checked(syrupGrams * 2);
        long syrupLoss = checked(syrupInput - syrupOutput);
        long grapeJuiceInput = checked(nightGrapeGrams * 3);
        long grapeJuiceWater = ScaleMass(waterGrams, grapeJuice.CleanWaterPerCycle);
        long grapeJuiceWastewater = ScaleMass(
            waterGrams,
            grapeJuice.WastewaterPerCycle);
        long grapeJuiceOutput = checked(grapeJuiceGrams * 2);
        long grapeJuiceLoss = checked(
            grapeJuiceInput + grapeJuiceWater
            - grapeJuiceOutput - grapeJuiceWastewater);
        long grapeSyrupInput = grapeJuiceGrams;
        long grapeSyrupOutput = checked(grapeSyrupGrams * 2);
        long grapeSyrupLoss = checked(grapeSyrupInput - grapeSyrupOutput);
        long fermentedLiquorInput = checked(maltGrams * 2);
        long fermentedLiquorWater = ScaleMass(
            waterGrams,
            fermentedLiquor.CleanWaterPerCycle);
        long fermentedLiquorOutput = checked(fermentedLiquorGrams * 2);
        long fermentedLiquorLoss = checked(
            fermentedLiquorInput + fermentedLiquorWater - fermentedLiquorOutput);
        long maltPorridgeInput = maltGrams;
        long maltPorridgeWater = ScaleMass(
            waterGrams,
            maltPorridge.CleanWaterPerCycle);
        long maltPorridgeOutput = checked(maltPorridgeGrams * 2);
        long maltPorridgeLoss = checked(
            maltPorridgeInput + maltPorridgeWater - maltPorridgeOutput);
        long youngWineInput = checked(grapeJuiceGrams * 2);
        long youngWineOutput = checked(youngWineGrams * 2);
        long youngWineLoss = checked(youngWineInput - youngWineOutput);
        long twilightBeerInput = checked(fermentedLiquorGrams * 2);
        long twilightBeerOutput = checked(twilightBeerGrams * 2);
        long twilightBeerLoss = checked(twilightBeerInput - twilightBeerOutput);
        long nightWineInput = checked(youngWineGrams * 2);
        long nightWineOutput = checked(nightWineGrams * 2);
        long nightWineLoss = checked(nightWineInput - nightWineOutput);
        long nightSpiritInput = checked(youngWineGrams * 2 + syrupGrams);
        long nightSpiritOutput = checked(nightSpiritGrams * 2);
        long nightSpiritLoss = checked(nightSpiritInput - nightSpiritOutput);
        long alcoholInput = checked(fermentedLiquorGrams * 2);
        long alcoholOutput = checked(alcoholGrams * 2);
        long alcoholLoss = checked(alcoholInput - alcoholOutput);
        long washedVegetableInput = checked(emberRootGrams * 2);
        long washedVegetableWater = ScaleMass(
            waterGrams,
            washedVegetable.CleanWaterPerCycle);
        long washedVegetableWastewater = ScaleMass(
            waterGrams,
            washedVegetable.WastewaterPerCycle);
        long washedVegetableOutput = checked(washedVegetableGrams * 2);
        long washedVegetableLoss = checked(
            washedVegetableInput + washedVegetableWater
            - washedVegetableOutput - washedVegetableWastewater);
        long brinedVegetableInput = checked(washedVegetableGrams * 2 + saltstoneGrams);
        long brinedVegetableWater = ScaleMass(
            waterGrams,
            brinedVegetable.CleanWaterPerCycle);
        long brinedVegetableWastewater = ScaleMass(
            waterGrams,
            brinedVegetable.WastewaterPerCycle);
        long brinedVegetableOutput = checked(brinedVegetableGrams * 2);
        long brinedVegetableLoss = checked(
            brinedVegetableInput + brinedVegetableWater
            - brinedVegetableOutput - brinedVegetableWastewater);
        long fermentedVinegarInput = fermentedLiquorGrams;
        long fermentedVinegarWater = ScaleMass(
            waterGrams,
            fermentedVinegar.CleanWaterPerCycle);
        long fermentedVinegarOutput = checked(fermentedVinegarGrams * 2);
        long fermentedVinegarLoss = checked(
            fermentedVinegarInput + fermentedVinegarWater - fermentedVinegarOutput);
        long fermentedPickleInput = checked(
            brinedVegetableGrams * 2 + fermentedVinegarGrams);
        long fermentedPickleWater = ScaleMass(
            waterGrams,
            fermentedPickle.CleanWaterPerCycle);
        long fermentedPickleWastewater = ScaleMass(
            waterGrams,
            fermentedPickle.WastewaterPerCycle);
        long fermentedPickleOutput = checked(fermentedPickleGrams * 2);
        long fermentedPickleLoss = checked(
            fermentedPickleInput + fermentedPickleWater
            - fermentedPickleOutput - fermentedPickleWastewater);
        long preservedVegetableInput = checked(
            brinedVegetableGrams + washedVegetableGrams + fermentedVinegarGrams);
        long preservedVegetableOutput = checked(preservedVegetableGrams * 2);
        long preservedVegetableLoss = checked(
            preservedVegetableInput - preservedVegetableOutput);
        long doughPhysicalInput = checked(flourGrams * 2 + eggGrams);
        long doughWater = ScaleMass(waterGrams, dough.CleanWaterPerCycle);
        long doughWastewater = ScaleMass(waterGrams, dough.WastewaterPerCycle);
        long doughOutput = checked(doughGrams * 2);
        long doughLoss = checked(
            doughPhysicalInput + doughWater - doughOutput - doughWastewater);
        long seasonedFillingInput = checked(meatGrams * 2 + washedVegetableGrams);
        long seasonedFillingOutput = checked(seasonedFillingGrams * 2);
        long seasonedFillingLoss = checked(
            seasonedFillingInput - seasonedFillingOutput);
        long vegetablePieInput = checked(doughGrams + washedVegetableGrams);
        long vegetablePieOutput = checked(vegetablePieGrams * 2);
        long vegetablePieLoss = checked(vegetablePieInput - vegetablePieOutput);
        long stuffedMushroomInput = checked(
            seasonedFillingGrams + mushroomGrams * 2);
        long stuffedMushroomOutput = checked(stuffedMushroomGrams * 2);
        long stuffedMushroomLoss = checked(
            stuffedMushroomInput - stuffedMushroomOutput);
        long hayInput = checked(grassStrawGrams * 3 + grainGrams);
        long hayOutput = checked(hayGrams * 3);
        long hayLoss = checked(hayInput - hayOutput);
        long silagePhysicalInput = checked(grassStrawGrams * 3 + grainGrams);
        long silageWater = ScaleMass(waterGrams, silage.CleanWaterPerCycle);
        long silageOutput = checked(silageGrams * 3);
        long silageLoss = checked(silagePhysicalInput + silageWater - silageOutput);
        long dogFoodByproductInput = checked(animalRotGrams + grainGrams);
        long dogFoodFreshInput = checked(meatGrams + grainGrams);
        long dogFoodOutput = checked(dogFoodGrams * 2);
        long freshDogFoodOutput = checked(freshDogFoodGrams * 2);
        long meatPieInput = checked(doughGrams + seasonedFillingGrams);
        long meatPieOutput = checked(meatPieGrams * 2);
        long meatPieLoss = checked(meatPieInput - meatPieOutput);
        long lavishVeganPhysicalInput = checked(
            flourGrams * 2 + syrupGrams + mushroomGrams * 2 + emberRootGrams);
        long lavishVeganWater = ScaleMass(waterGrams, lavishVegan.CleanWaterPerCycle);
        long lavishVeganWastewater = ScaleMass(
            waterGrams,
            lavishVegan.WastewaterPerCycle);
        long lavishVeganOutput = checked(lavishVeganGrams * 2);
        long lavishVeganLoss = checked(
            lavishVeganPhysicalInput + lavishVeganWater
            - lavishVeganOutput - lavishVeganWastewater);
        long medicalVialInput = ironIngotGrams;
        long medicalVialOutput = checked(medicalVialGrams * 30);
        long anestheticInput = checked(
            dreamleafGrams * 2 + alcoholGrams + medicalVialGrams);
        long anestheticOutput = anestheticGrams;
        long anestheticResidue = checked(anestheticInput - anestheticOutput);
        long granulatedPowderInput = checked(
            blackPowderGrams * 2 + paperGrams);
        long granulatedPowderOutput = checked(granulatedPowderGrams * 6);
        long granulatedPowderLoss = checked(
            granulatedPowderInput - granulatedPowderOutput);
        long inoculatedLogInput = checked(treatedLumberGrams + mushroomGrams);
        long inoculatedLogOutput = checked(inoculatedLogGrams * 2);
        return new[]
        {
            new PhysicalMassTransformContract(
                porridge.RecipeId,
                porridgeInput,
                waterInput,
                porridgeOutput,
                wastewater,
                porridgeLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Six grain units plus 3.4 clean-water units produce six 600 g portions, 0.2 wastewater units and 100 g cooking loss under the 500 g/authored-unit fluid authority."),
            new PhysicalMassTransformContract(
                sawmill.RecipeId,
                sawmillInput,
                0,
                sawmillOutput,
                0,
                sawmillLoss,
                PhysicalMassLossKind.None,
                "Two 1,800 g log sections produce three exact 1,200 g lumber bundles without untracked loss."),
            new PhysicalMassTransformContract(
                granulatedPowder.RecipeId,
                granulatedPowderInput,
                0,
                granulatedPowderOutput,
                0,
                granulatedPowderLoss,
                PhysicalMassLossKind.MillingByproduct,
                "Two black-powder lots and one paper lot are screened into six 850 g granulated-powder charges with 200 g of declared paper trim and uncollected powder dust."),
            new PhysicalMassTransformContract(
                dogFood.RecipeId,
                dogFoodByproductInput,
                0,
                dogFoodOutput,
                0,
                0,
                PhysicalMassLossKind.None,
                "One 700 g animal-rot bundle and one 350 g grain measure are divided into two exact 525 g dog-food rations."),
            new PhysicalMassTransformContract(
                freshDogFood.RecipeId,
                dogFoodFreshInput,
                0,
                freshDogFoodOutput,
                0,
                0,
                PhysicalMassLossKind.None,
                "One 700 g meat portion and one 350 g grain measure are divided into two exact 525 g fresh dog-food rations."),
            new PhysicalMassTransformContract(
                inoculatedLog.RecipeId,
                inoculatedLogInput,
                0,
                inoculatedLogOutput,
                0,
                0,
                PhysicalMassLossKind.None,
                "One 1,150 g treated-lumber bundle and one 250 g cave-mushroom basket produce two exact 700 g inoculated cultivation-log sections."),
            new PhysicalMassTransformContract(
                mushroomSoup.RecipeId,
                mushroomInput,
                mushroomWater,
                mushroomOutput,
                mushroomWastewater,
                mushroomLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two mushroom baskets plus 1.9 clean-water units produce two 650 g soup portions, 0.2 wastewater units and 50 g cooking loss under the 500 g/authored-unit fluid authority."),
            new PhysicalMassTransformContract(
                roastedMeat.RecipeId,
                roastedInput,
                roastedWater,
                roastedOutput,
                0,
                roastedLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two raw meat cuts plus 0.3 clean-water units produce two 750 g roasted portions with 50 g cooking moisture loss."),
            new PhysicalMassTransformContract(
                rootStew.RecipeId,
                rootStewInput,
                rootStewWater,
                rootStewOutput,
                rootStewWastewater,
                rootStewLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two ember-root harvest bundles plus 1.3 clean-water units produce two 700 g stew portions, 0.2 wastewater units and 50 g cooking loss under the 500 g/authored-unit fluid authority."),
            new PhysicalMassTransformContract(
                millingFlour.RecipeId,
                flourInput,
                0,
                flourOutput,
                0,
                flourLoss,
                PhysicalMassLossKind.MillingByproduct,
                "Three grain measures produce two 300 g flour measures with 450 g explicitly uncollected bran and milling residue."),
            new PhysicalMassTransformContract(
                eggPancake.RecipeId,
                pancakePhysicalInput,
                pancakeWater,
                pancakeOutput,
                pancakeWastewater,
                pancakeLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two egg collections, one 300 g flour measure, one milk measure and 0.2 clean-water units produce two 650 g portions, 0.15 wastewater units and 325 g cooking loss."),
            new PhysicalMassTransformContract(
                curd.RecipeId,
                curdPhysicalInput,
                curdWater,
                curdOutput,
                curdWastewater,
                curdLoss,
                PhysicalMassLossKind.ExtractionResidue,
                "Three 0.8-liter milk measures, one saltstone chunk and 0.4 clean-water units produce two drained-curd batches, discharge 4.2 whey units and leave 100 g typed separation residue."),
            new PhysicalMassTransformContract(
                freshCurd.RecipeId,
                freshCurdInput,
                0,
                freshCurdOutput,
                0,
                0,
                PhysicalMassLossKind.None,
                "One drained-curd batch is divided into two fresh-curd servings with exact physical mass conservation."),
            new PhysicalMassTransformContract(
                cheese.RecipeId,
                cheeseInput,
                0,
                cheeseOutput,
                0,
                cheeseAgingLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two drained-curd batches age into two cheese portions with 100 g explicit aging moisture loss."),
            new PhysicalMassTransformContract(
                cheeseMushroom.RecipeId,
                cheeseMushroomInput,
                0,
                cheeseMushroomOutput,
                0,
                0,
                PhysicalMassLossKind.None,
                "One cheese portion and two mushroom baskets produce two cheese-mushroom meal portions without untracked loss."),
            new PhysicalMassTransformContract(
                lavishMeat.RecipeId,
                lavishMeatPhysicalInput,
                lavishMeatWater,
                lavishMeatOutput,
                lavishMeatWastewater,
                lavishMeatLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two raw meat cuts, one cheese portion, two night-grape clusters and 0.3 clean-water units produce two lavish meals, 0.25 wastewater units and 325 g cooking moisture loss."),
            new PhysicalMassTransformContract(
                malt.RecipeId,
                maltInput,
                0,
                maltOutput,
                0,
                0,
                PhysicalMassLossKind.None,
                "Two grain measures produce two malt measures without untracked physical mass loss."),
            new PhysicalMassTransformContract(
                syrup.RecipeId,
                syrupInput,
                0,
                syrupOutput,
                0,
                syrupLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Three night-grape clusters concentrate into two bulk syrup measures with 50 g explicit moisture loss."),
            new PhysicalMassTransformContract(
                grapeJuice.RecipeId,
                grapeJuiceInput,
                grapeJuiceWater,
                grapeJuiceOutput,
                grapeJuiceWastewater,
                grapeJuiceLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Three night-grape clusters plus 0.1 clean-water units produce two grape-juice measures, 0.05 wastewater units and 25 g process loss."),
            new PhysicalMassTransformContract(
                grapeSyrup.RecipeId,
                grapeSyrupInput,
                0,
                grapeSyrupOutput,
                0,
                grapeSyrupLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "One grape-juice measure concentrates into two syrup servings with 25 g explicit moisture loss."),
            new PhysicalMassTransformContract(
                fermentedLiquor.RecipeId,
                fermentedLiquorInput,
                fermentedLiquorWater,
                fermentedLiquorOutput,
                0,
                fermentedLiquorLoss,
                PhysicalMassLossKind.FermentationGasLoss,
                "Two malt measures plus 0.7 clean-water units produce two fermented-liquor measures with 50 g explicit atmospheric fermentation-gas loss."),
            new PhysicalMassTransformContract(
                maltPorridge.RecipeId,
                maltPorridgeInput,
                maltPorridgeWater,
                maltPorridgeOutput,
                0,
                maltPorridgeLoss,
                PhysicalMassLossKind.None,
                "One malt measure plus 1.5 clean-water units produce two full-meal porridge servings without untracked loss."),
            new PhysicalMassTransformContract(
                youngWine.RecipeId,
                youngWineInput,
                0,
                youngWineOutput,
                0,
                youngWineLoss,
                PhysicalMassLossKind.FermentationGasLoss,
                "Two grape-juice measures ferment into two young-wine measures with 50 g explicit atmospheric fermentation-gas loss."),
            new PhysicalMassTransformContract(
                twilightBeer.RecipeId,
                twilightBeerInput,
                0,
                twilightBeerOutput,
                0,
                twilightBeerLoss,
                PhysicalMassLossKind.FermentationGasLoss,
                "Two fermented-liquor measures finish into two twilight-beer servings with 50 g explicit atmospheric fermentation-gas loss."),
            new PhysicalMassTransformContract(
                nightWine.RecipeId,
                nightWineInput,
                0,
                nightWineOutput,
                0,
                nightWineLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two young-wine measures age into two night-wine servings with 50 g explicit cask evaporation."),
            new PhysicalMassTransformContract(
                nightSpirit.RecipeId,
                nightSpiritInput,
                0,
                nightSpiritOutput,
                0,
                nightSpiritLoss,
                PhysicalMassLossKind.ExtractionResidue,
                "Two young-wine measures and one concentrated-syrup measure are distilled into two night-spirit servings with 150 g of declared vapour and filter residue."),
            new PhysicalMassTransformContract(
                alcohol.RecipeId,
                alcoholInput,
                0,
                alcoholOutput,
                0,
                alcoholLoss,
                PhysicalMassLossKind.None,
                "Two fermented-liquor measures are separated into two bulk alcohol measures without untracked physical mass loss."),
            new PhysicalMassTransformContract(
                washedVegetable.RecipeId,
                washedVegetableInput,
                washedVegetableWater,
                washedVegetableOutput,
                washedVegetableWastewater,
                washedVegetableLoss,
                PhysicalMassLossKind.None,
                "Two ember-root harvest bundles and 0.25 clean-water units produce two washed portions and 0.25 wastewater units without untracked loss."),
            new PhysicalMassTransformContract(
                brinedVegetable.RecipeId,
                brinedVegetableInput,
                brinedVegetableWater,
                brinedVegetableOutput,
                brinedVegetableWastewater,
                brinedVegetableLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two washed portions, one saltstone chunk and 1.4 clean-water units produce two drained brined portions, 2.0 brine-wastewater units and 100 g preparation loss."),
            new PhysicalMassTransformContract(
                fermentedVinegar.RecipeId,
                fermentedVinegarInput,
                fermentedVinegarWater,
                fermentedVinegarOutput,
                0,
                fermentedVinegarLoss,
                PhysicalMassLossKind.FermentationGasLoss,
                "One fermented-liquor measure and 0.7 clean-water units produce two vinegar measures with 50 g explicit atmospheric fermentation-gas loss."),
            new PhysicalMassTransformContract(
                fermentedPickle.RecipeId,
                fermentedPickleInput,
                fermentedPickleWater,
                fermentedPickleOutput,
                fermentedPickleWastewater,
                fermentedPickleLoss,
                PhysicalMassLossKind.FermentationGasLoss,
                "Two brined portions, one vinegar measure and 1.2 clean-water units produce two drained pickle servings, 2.0 discarded-brine units and 100 g fermentation loss."),
            new PhysicalMassTransformContract(
                preservedVegetable.RecipeId,
                preservedVegetableInput,
                0,
                preservedVegetableOutput,
                0,
                preservedVegetableLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "One brined portion, one washed portion and one vinegar measure produce two preserved-vegetable meals with 250 g explicit cooking evaporation."),
            new PhysicalMassTransformContract(
                dough.RecipeId,
                doughPhysicalInput,
                doughWater,
                doughOutput,
                doughWastewater,
                doughLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two 300 g flour measures, one egg collection and 0.6 clean-water units produce two 500 g dough portions, 0.2 wastewater units and 50 g explicit preparation loss."),
            new PhysicalMassTransformContract(
                seasonedFilling.RecipeId,
                seasonedFillingInput,
                0,
                seasonedFillingOutput,
                0,
                seasonedFillingLoss,
                PhysicalMassLossKind.CuttingWaste,
                "Two raw meat cuts and one washed-vegetable portion produce two seasoned-filling portions with 550 g explicit trimming and preparation waste."),
            new PhysicalMassTransformContract(
                vegetablePie.RecipeId,
                vegetablePieInput,
                0,
                vegetablePieOutput,
                0,
                vegetablePieLoss,
                PhysicalMassLossKind.None,
                "One dough portion and one washed-vegetable portion produce two vegetable-pie servings without untracked physical mass loss."),
            new PhysicalMassTransformContract(
                stuffedMushroom.RecipeId,
                stuffedMushroomInput,
                0,
                stuffedMushroomOutput,
                0,
                stuffedMushroomLoss,
                PhysicalMassLossKind.None,
                "One seasoned-filling portion and two mushroom baskets produce two stuffed-mushroom servings without untracked physical mass loss."),
            new PhysicalMassTransformContract(
                hayFeed.RecipeId,
                hayInput,
                0,
                hayOutput,
                0,
                hayLoss,
                PhysicalMassLossKind.FiberProcessingWaste,
                "Three grass-and-straw sheaves and one grain measure produce three hay rations with 2 g of declared screening and blending residue."),
            new PhysicalMassTransformContract(
                silage.RecipeId,
                silagePhysicalInput,
                silageWater,
                silageOutput,
                0,
                silageLoss,
                PhysicalMassLossKind.None,
                "Three grass-and-straw sheaves, one grain measure and 0.2 clean-water units produce three silage rations without untracked physical mass loss."),
            new PhysicalMassTransformContract(
                meatPie.RecipeId,
                meatPieInput,
                0,
                meatPieOutput,
                0,
                meatPieLoss,
                PhysicalMassLossKind.None,
                "One dough portion and one seasoned-filling portion produce two meat-pie servings without untracked physical mass loss."),
            new PhysicalMassTransformContract(
                lavishVegan.RecipeId,
                lavishVeganPhysicalInput,
                lavishVeganWater,
                lavishVeganOutput,
                lavishVeganWastewater,
                lavishVeganLoss,
                PhysicalMassLossKind.MoistureEvaporation,
                "Two flour measures, one syrup measure, two mushroom baskets, one ember-root bundle and 0.3 clean-water units produce two lavish-vegan servings, 0.25 wastewater units and 125 g cooking evaporation."),
            new PhysicalMassTransformContract(
                medicalVial.RecipeId,
                medicalVialInput,
                0,
                medicalVialOutput,
                0,
                0,
                PhysicalMassLossKind.None,
                "One 900 g iron ingot is forged into thirty reusable 30 g medical vials without untracked physical mass loss."),
            new PhysicalMassTransformContract(
                anesthetic.RecipeId,
                anestheticInput,
                0,
                anestheticOutput,
                0,
                anestheticResidue,
                PhysicalMassLossKind.ExtractionResidue,
                "Two 80 g dreamleaf bundles, one 500 g alcohol unit and one reusable 30 g vial produce one packaged 120 g anesthetic dose and 570 g declared spent-solvent and herb extraction residue.")
        };
    }

    private static void ValidateRecipes(
        IReadOnlyDictionary<string, ProductionRecipeSO> recipes)
    {
        ProductionRecipeSO granulatedPowder =
            recipes["recipe:material:granulated-powder"];
        Require(granulatedPowder.Inputs.Count == 2
            && HasInput(granulatedPowder, "material:black-powder", 2)
            && HasInput(granulatedPowder, "material:paper", 1)
            && granulatedPowder.Outputs.Count == 1
            && granulatedPowder.Outputs[0].ItemId
                == "material:granulated-powder"
            && granulatedPowder.Outputs[0].Amount == 6
            && Mathf.Approximately(
                granulatedPowder.Outputs[0].Probability,
                1f),
            "Granulated-powder mass-conserving recipe contract drifted.");
        ProductionRecipeSO dogFood = recipes["recipe:dog-food"];
        Require(dogFood.Inputs.Count == 2
            && HasInput(dogFood, "waste:animal-rot", 1)
            && HasInput(dogFood, "resource:twilight-grain", 1)
            && dogFood.Outputs.Count == 1
            && dogFood.Outputs[0].ItemId == "feed:dog-food"
            && dogFood.Outputs[0].Amount == 2
            && Mathf.Approximately(dogFood.Outputs[0].Probability, 1f),
            "Dog-food byproduct recipe contract drifted.");
        ProductionRecipeSO freshDogFood = recipes["recipe:dog-food-fresh"];
        Require(freshDogFood.Inputs.Count == 2
            && HasInput(freshDogFood, "resource:meat", 1)
            && HasInput(freshDogFood, "resource:twilight-grain", 1)
            && freshDogFood.Outputs.Count == 1
            && freshDogFood.Outputs[0].ItemId == "feed:dog-food-fresh"
            && freshDogFood.Outputs[0].Amount == 2
            && Mathf.Approximately(freshDogFood.Outputs[0].Probability, 1f),
            "Fresh dog-food recipe contract drifted.");
        ProductionRecipeSO inoculatedLog =
            recipes["recipe:supply:inoculated-log"];
        Require(inoculatedLog.Inputs.Count == 2
            && HasInput(inoculatedLog, "material:treated-lumber", 1)
            && HasInput(inoculatedLog, "resource:cave-mushroom", 1)
            && inoculatedLog.Outputs.Count == 1
            && inoculatedLog.Outputs[0].ItemId == "supply:inoculated-log"
            && inoculatedLog.Outputs[0].Amount == 2
            && Mathf.Approximately(
                inoculatedLog.Outputs[0].Probability,
                1f),
            "Inoculated-log recipe contract drifted.");
        ProductionRecipeSO medicalVial = recipes["recipe:medical-vial"];
        Require(medicalVial.Inputs.Count == 1
            && medicalVial.Inputs[0].ItemId == "material:iron-ingot"
            && medicalVial.Inputs[0].Amount == 1
            && medicalVial.Outputs.Count == 1
            && medicalVial.Outputs[0].ItemId == "container:medical-vial"
            && medicalVial.Outputs[0].Amount == 30
            && Mathf.Approximately(medicalVial.Outputs[0].Probability, 1f),
            "Medical-vial mass-conserving recipe contract drifted.");
        ProductionRecipeSO anesthetic = recipes["recipe:anesthetic"];
        Require(anesthetic.Inputs.Count == 3
            && anesthetic.Inputs.Count(input => input.ItemId == "resource:dreamleaf"
                && input.Amount == 2) == 1
            && anesthetic.Inputs.Count(input => input.ItemId == "material:alcohol"
                && input.Amount == 1) == 1
            && anesthetic.Inputs.Count(input => input.ItemId == "container:medical-vial"
                && input.Amount == 1) == 1
            && anesthetic.Outputs.Count == 1
            && anesthetic.Outputs[0].ItemId == "medicine:anesthetic"
            && anesthetic.Outputs[0].Amount == 1
            && Mathf.Approximately(anesthetic.Outputs[0].Probability, 1f),
            "Packaged anesthetic recipe contract drifted.");
        ProductionRecipeSO porridge = recipes["recipe:grain-porridge"];
        Require(porridge.Inputs.Count == 1
            && porridge.Inputs[0].ItemId == "resource:twilight-grain"
            && porridge.Inputs[0].Amount == 6,
            "Grain-porridge input contract drifted.");
        Require(porridge.Outputs.Count == 1
            && porridge.Outputs[0].ItemId == "food:grain-porridge"
            && porridge.Outputs[0].Amount == 6
            && Mathf.Approximately(porridge.Outputs[0].Probability, 1f),
            "Grain-porridge output contract drifted.");
        Require(Mathf.Approximately(porridge.CleanWaterPerCycle, 3.4f)
            && Mathf.Approximately(porridge.WastewaterPerCycle, 0.2f),
            "Grain-porridge fluid contract drifted.");

        ProductionRecipeSO sawmill = recipes["recipe:sawmill-lumber"];
        Require(sawmill.Inputs.Count == 1
            && sawmill.Inputs[0].ItemId == "resource:log"
            && sawmill.Inputs[0].Amount == 2,
            "Sawmill input contract drifted.");
        Require(sawmill.Outputs.Count == 1
            && sawmill.Outputs[0].ItemId == "material:lumber"
            && sawmill.Outputs[0].Amount == 3
            && Mathf.Approximately(sawmill.Outputs[0].Probability, 1f),
            "Sawmill output contract drifted.");

        ProductionRecipeSO mushroomSoup = recipes["recipe:mushroom-soup"];
        Require(mushroomSoup.Inputs.Count == 1
            && mushroomSoup.Inputs[0].ItemId == "resource:cave-mushroom"
            && mushroomSoup.Inputs[0].Amount == 2,
            "Mushroom-soup input contract drifted.");
        Require(mushroomSoup.Outputs.Count == 1
            && mushroomSoup.Outputs[0].ItemId == "food:mushroom-soup"
            && mushroomSoup.Outputs[0].Amount == 2
            && Mathf.Approximately(mushroomSoup.Outputs[0].Probability, 1f)
            && Mathf.Approximately(mushroomSoup.CleanWaterPerCycle, 1.9f)
            && Mathf.Approximately(mushroomSoup.WastewaterPerCycle, 0.2f),
            "Mushroom-soup output or fluid contract drifted.");

        ProductionRecipeSO roastedMeat = recipes["recipe:roasted-meat"];
        Require(roastedMeat.Inputs.Count == 1
            && roastedMeat.Inputs[0].ItemId == "resource:meat"
            && roastedMeat.Inputs[0].Amount == 2,
            "Roasted-meat input contract drifted.");
        Require(roastedMeat.Outputs.Count == 1
            && roastedMeat.Outputs[0].ItemId == "food:roasted-meat"
            && roastedMeat.Outputs[0].Amount == 2
            && Mathf.Approximately(roastedMeat.Outputs[0].Probability, 1f)
            && Mathf.Approximately(roastedMeat.CleanWaterPerCycle, 0.3f)
            && Mathf.Approximately(roastedMeat.WastewaterPerCycle, 0f),
            "Roasted-meat output or fluid contract drifted.");

        ProductionRecipeSO rootStew = recipes["recipe:root-stew"];
        Require(rootStew.Inputs.Count == 1
            && rootStew.Inputs[0].ItemId == "resource:ember-root"
            && rootStew.Inputs[0].Amount == 2,
            "Root-stew input contract drifted.");
        Require(rootStew.Outputs.Count == 1
            && rootStew.Outputs[0].ItemId == "food:root-stew"
            && rootStew.Outputs[0].Amount == 2
            && Mathf.Approximately(rootStew.Outputs[0].Probability, 1f)
            && Mathf.Approximately(rootStew.CleanWaterPerCycle, 1.3f)
            && Mathf.Approximately(rootStew.WastewaterPerCycle, 0.2f),
            "Root-stew output or fluid contract drifted.");

        ProductionRecipeSO millingFlour = recipes["recipe:milling-flour"];
        Require(millingFlour.Inputs.Count == 1
            && millingFlour.Inputs[0].ItemId == "resource:twilight-grain"
            && millingFlour.Inputs[0].Amount == 3,
            "Milling-flour input contract drifted.");
        Require(millingFlour.Outputs.Count == 1
            && millingFlour.Outputs[0].ItemId == "material:flour"
            && millingFlour.Outputs[0].Amount == 2
            && Mathf.Approximately(millingFlour.Outputs[0].Probability, 1f)
            && Mathf.Approximately(millingFlour.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(millingFlour.WastewaterPerCycle, 0f),
            "Milling-flour output or fluid contract drifted.");

        ProductionRecipeSO eggPancake = recipes["recipe:egg-pancake"];
        Require(eggPancake.Inputs.Count == 3
            && HasInput(eggPancake, "resource:egg", 2)
            && HasInput(eggPancake, "material:flour", 1)
            && HasInput(eggPancake, "resource:milk", 1),
            "Egg-pancake input contract drifted.");
        Require(eggPancake.Outputs.Count == 1
            && eggPancake.Outputs[0].ItemId == "food:egg-pancake"
            && eggPancake.Outputs[0].Amount == 2
            && Mathf.Approximately(eggPancake.Outputs[0].Probability, 1f)
            && Mathf.Approximately(eggPancake.CleanWaterPerCycle, 0.2f)
            && Mathf.Approximately(eggPancake.WastewaterPerCycle, 0.15f),
            "Egg-pancake output or fluid contract drifted.");

        ValidateAnimalSource(recipes["source:animal-milk"], "resource:milk");
        ValidateAnimalSource(recipes["source:animal-egg"], "resource:egg");

        ProductionRecipeSO curd = recipes["recipe:curd"];
        Require(curd.Inputs.Count == 2
            && HasInput(curd, "resource:milk", 3)
            && HasInput(curd, "resource:saltstone", 1),
            "Curd input contract drifted.");
        Require(curd.Outputs.Count == 1
            && curd.Outputs[0].ItemId == "material:curd"
            && curd.Outputs[0].Amount == 2
            && Mathf.Approximately(curd.Outputs[0].Probability, 1f)
            && Mathf.Approximately(curd.CleanWaterPerCycle, 0.4f)
            && Mathf.Approximately(curd.WastewaterPerCycle, 4.2f),
            "Curd output or whey-wastewater contract drifted.");

        ProductionRecipeSO freshCurd = recipes["recipe:fresh-curd"];
        Require(freshCurd.Inputs.Count == 1
            && HasInput(freshCurd, "material:curd", 1),
            "Fresh-curd input contract drifted.");
        Require(freshCurd.Outputs.Count == 1
            && freshCurd.Outputs[0].ItemId == "food:fresh-curd"
            && freshCurd.Outputs[0].Amount == 2
            && Mathf.Approximately(freshCurd.Outputs[0].Probability, 1f)
            && Mathf.Approximately(freshCurd.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(freshCurd.WastewaterPerCycle, 0f),
            "Fresh-curd output or fluid contract drifted.");

        ProductionRecipeSO cheese = recipes["recipe:cheese"];
        Require(cheese.Inputs.Count == 1
            && HasInput(cheese, "material:curd", 2),
            "Cheese-aging input contract drifted.");
        Require(cheese.Outputs.Count == 1
            && cheese.Outputs[0].ItemId == "material:cheese"
            && cheese.Outputs[0].Amount == 2
            && Mathf.Approximately(cheese.Outputs[0].Probability, 1f)
            && Mathf.Approximately(cheese.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(cheese.WastewaterPerCycle, 0f),
            "Cheese-aging output or fluid contract drifted.");

        ProductionRecipeSO cheeseMushroom = recipes["recipe:cheese-mushroom"];
        Require(cheeseMushroom.Inputs.Count == 2
            && HasInput(cheeseMushroom, "material:cheese", 1)
            && HasInput(cheeseMushroom, "resource:cave-mushroom", 2),
            "Cheese-mushroom input contract drifted.");
        Require(cheeseMushroom.Outputs.Count == 1
            && cheeseMushroom.Outputs[0].ItemId == "food:cheese-mushroom"
            && cheeseMushroom.Outputs[0].Amount == 2
            && Mathf.Approximately(cheeseMushroom.Outputs[0].Probability, 1f)
            && Mathf.Approximately(cheeseMushroom.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(cheeseMushroom.WastewaterPerCycle, 0f),
            "Cheese-mushroom output or fluid contract drifted.");

        ProductionRecipeSO lavishMeat = recipes["recipe:lavish-meat"];
        Require(lavishMeat.Inputs.Count == 3
            && HasInput(lavishMeat, "resource:meat", 2)
            && HasInput(lavishMeat, "material:cheese", 1)
            && HasInput(lavishMeat, "resource:night-grape", 2),
            "Lavish-meat input contract drifted.");
        Require(lavishMeat.Outputs.Count == 1
            && lavishMeat.Outputs[0].ItemId == "food:lavish-meat"
            && lavishMeat.Outputs[0].Amount == 2
            && Mathf.Approximately(lavishMeat.Outputs[0].Probability, 1f)
            && Mathf.Approximately(lavishMeat.CleanWaterPerCycle, 0.3f)
            && Mathf.Approximately(lavishMeat.WastewaterPerCycle, 0.25f),
            "Lavish-meat output or fluid contract drifted.");

        ProductionRecipeSO malt = recipes["recipe:malt"];
        Require(malt.Inputs.Count == 1
            && HasInput(malt, "resource:twilight-grain", 2)
            && malt.Outputs.Count == 1
            && malt.Outputs[0].ItemId == "material:malt"
            && malt.Outputs[0].Amount == 2
            && Mathf.Approximately(malt.Outputs[0].Probability, 1f)
            && Mathf.Approximately(malt.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(malt.WastewaterPerCycle, 0f),
            "Malt transform contract drifted.");

        ProductionRecipeSO syrup = recipes["recipe:syrup"];
        Require(syrup.Inputs.Count == 1
            && HasInput(syrup, "resource:night-grape", 3)
            && syrup.Outputs.Count == 1
            && syrup.Outputs[0].ItemId == "material:syrup"
            && syrup.Outputs[0].Amount == 2
            && Mathf.Approximately(syrup.Outputs[0].Probability, 1f)
            && Mathf.Approximately(syrup.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(syrup.WastewaterPerCycle, 0f),
            "Concentrated-syrup transform contract drifted.");

        ProductionRecipeSO grapeJuice = recipes["recipe:grape-juice"];
        Require(grapeJuice.Inputs.Count == 1
            && HasInput(grapeJuice, "resource:night-grape", 3)
            && grapeJuice.Outputs.Count == 1
            && grapeJuice.Outputs[0].ItemId == "material:grape-juice"
            && grapeJuice.Outputs[0].Amount == 2
            && Mathf.Approximately(grapeJuice.Outputs[0].Probability, 1f)
            && Mathf.Approximately(grapeJuice.CleanWaterPerCycle, 0.1f)
            && Mathf.Approximately(grapeJuice.WastewaterPerCycle, 0.05f),
            "Grape-juice transform contract drifted.");

        ProductionRecipeSO grapeSyrup = recipes["recipe:grape-syrup"];
        Require(grapeSyrup.Inputs.Count == 1
            && HasInput(grapeSyrup, "material:grape-juice", 1)
            && grapeSyrup.Outputs.Count == 1
            && grapeSyrup.Outputs[0].ItemId == "food:grape-syrup"
            && grapeSyrup.Outputs[0].Amount == 2
            && Mathf.Approximately(grapeSyrup.Outputs[0].Probability, 1f)
            && Mathf.Approximately(grapeSyrup.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(grapeSyrup.WastewaterPerCycle, 0f),
            "Grape-syrup meal transform contract drifted.");

        ProductionRecipeSO fermentedLiquor = recipes["recipe:fermented-liquor"];
        Require(fermentedLiquor.Inputs.Count == 1
            && HasInput(fermentedLiquor, "material:malt", 2)
            && fermentedLiquor.Outputs.Count == 1
            && fermentedLiquor.Outputs[0].ItemId == "material:fermented-liquor"
            && fermentedLiquor.Outputs[0].Amount == 2
            && Mathf.Approximately(fermentedLiquor.Outputs[0].Probability, 1f)
            && Mathf.Approximately(fermentedLiquor.CleanWaterPerCycle, 0.7f)
            && Mathf.Approximately(fermentedLiquor.WastewaterPerCycle, 0f),
            "Fermented-liquor transform or fluid contract drifted.");

        ProductionRecipeSO maltPorridge = recipes["recipe:malt-porridge"];
        Require(maltPorridge.Inputs.Count == 1
            && HasInput(maltPorridge, "material:malt", 1)
            && maltPorridge.Outputs.Count == 1
            && maltPorridge.Outputs[0].ItemId == "food:malt-porridge"
            && maltPorridge.Outputs[0].Amount == 2
            && Mathf.Approximately(maltPorridge.Outputs[0].Probability, 1f)
            && Mathf.Approximately(maltPorridge.CleanWaterPerCycle, 1.5f)
            && Mathf.Approximately(maltPorridge.WastewaterPerCycle, 0f),
            "Malt-porridge transform or fluid contract drifted.");

        ProductionRecipeSO youngWine = recipes["recipe:young-wine"];
        Require(youngWine.Inputs.Count == 1
            && HasInput(youngWine, "material:grape-juice", 2)
            && youngWine.Outputs.Count == 1
            && youngWine.Outputs[0].ItemId == "material:young-wine"
            && youngWine.Outputs[0].Amount == 2
            && Mathf.Approximately(youngWine.Outputs[0].Probability, 1f)
            && Mathf.Approximately(youngWine.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(youngWine.WastewaterPerCycle, 0f),
            "Young-wine transform contract drifted.");

        ProductionRecipeSO twilightBeer = recipes["recipe:twilight-beer"];
        Require(twilightBeer.Inputs.Count == 1
            && HasInput(twilightBeer, "material:fermented-liquor", 2)
            && twilightBeer.Outputs.Count == 1
            && twilightBeer.Outputs[0].ItemId == "food:twilight-beer"
            && twilightBeer.Outputs[0].Amount == 2
            && Mathf.Approximately(twilightBeer.Outputs[0].Probability, 1f)
            && Mathf.Approximately(twilightBeer.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(twilightBeer.WastewaterPerCycle, 0f),
            "Twilight-beer transform contract drifted.");

        ProductionRecipeSO nightWine = recipes["recipe:night-wine"];
        Require(nightWine.Inputs.Count == 1
            && HasInput(nightWine, "material:young-wine", 2)
            && nightWine.Outputs.Count == 1
            && nightWine.Outputs[0].ItemId == "drug:night-wine"
            && nightWine.Outputs[0].Amount == 2
            && Mathf.Approximately(nightWine.Outputs[0].Probability, 1f)
            && Mathf.Approximately(nightWine.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(nightWine.WastewaterPerCycle, 0f),
            "Night-wine transform contract drifted.");

        ProductionRecipeSO nightSpirit = recipes["recipe:night-spirit"];
        Require(nightSpirit.Inputs.Count == 2
            && HasInput(nightSpirit, "material:young-wine", 2)
            && HasInput(nightSpirit, "material:syrup", 1)
            && nightSpirit.Outputs.Count == 1
            && nightSpirit.Outputs[0].ItemId == "food:night-spirit"
            && nightSpirit.Outputs[0].Amount == 2
            && Mathf.Approximately(nightSpirit.Outputs[0].Probability, 1f)
            && Mathf.Approximately(nightSpirit.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(nightSpirit.WastewaterPerCycle, 0f),
            "Night-spirit transform contract drifted.");

        ProductionRecipeSO alcohol = recipes["recipe:alcohol"];
        Require(alcohol.Inputs.Count == 1
            && HasInput(alcohol, "material:fermented-liquor", 2)
            && alcohol.Outputs.Count == 1
            && alcohol.Outputs[0].ItemId == "material:alcohol"
            && alcohol.Outputs[0].Amount == 2
            && Mathf.Approximately(alcohol.Outputs[0].Probability, 1f)
            && Mathf.Approximately(alcohol.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(alcohol.WastewaterPerCycle, 0f),
            "Alcohol transform contract drifted.");

        ProductionRecipeSO washedVegetable = recipes["recipe:washed-vegetable"];
        Require(washedVegetable.Inputs.Count == 1
            && HasInput(washedVegetable, "resource:ember-root", 2)
            && washedVegetable.Outputs.Count == 1
            && washedVegetable.Outputs[0].ItemId == "material:washed-vegetable"
            && washedVegetable.Outputs[0].Amount == 2
            && Mathf.Approximately(washedVegetable.Outputs[0].Probability, 1f)
            && Mathf.Approximately(washedVegetable.CleanWaterPerCycle, 0.25f)
            && Mathf.Approximately(washedVegetable.WastewaterPerCycle, 0.25f),
            "Washed-vegetable transform or fluid contract drifted.");

        ProductionRecipeSO brinedVegetable = recipes["recipe:brined-vegetable"];
        Require(brinedVegetable.Inputs.Count == 2
            && HasInput(brinedVegetable, "material:washed-vegetable", 2)
            && HasInput(brinedVegetable, "resource:saltstone", 1)
            && brinedVegetable.Outputs.Count == 1
            && brinedVegetable.Outputs[0].ItemId == "material:brined-vegetable"
            && brinedVegetable.Outputs[0].Amount == 2
            && Mathf.Approximately(brinedVegetable.Outputs[0].Probability, 1f)
            && Mathf.Approximately(brinedVegetable.CleanWaterPerCycle, 1.4f)
            && Mathf.Approximately(brinedVegetable.WastewaterPerCycle, 2f),
            "Brined-vegetable transform or fluid contract drifted.");

        ProductionRecipeSO fermentedVinegar = recipes["recipe:fermented-vinegar"];
        Require(fermentedVinegar.Inputs.Count == 1
            && HasInput(fermentedVinegar, "material:fermented-liquor", 1)
            && fermentedVinegar.Outputs.Count == 1
            && fermentedVinegar.Outputs[0].ItemId == "craft:fermented-vinegar"
            && fermentedVinegar.Outputs[0].Amount == 2
            && Mathf.Approximately(fermentedVinegar.Outputs[0].Probability, 1f)
            && Mathf.Approximately(fermentedVinegar.CleanWaterPerCycle, 0.7f)
            && Mathf.Approximately(fermentedVinegar.WastewaterPerCycle, 0f),
            "Fermented-vinegar transform or fluid contract drifted.");

        ProductionRecipeSO fermentedPickle = recipes["recipe:fermented-pickle"];
        Require(fermentedPickle.Inputs.Count == 2
            && HasInput(fermentedPickle, "material:brined-vegetable", 2)
            && HasInput(fermentedPickle, "craft:fermented-vinegar", 1)
            && fermentedPickle.Outputs.Count == 1
            && fermentedPickle.Outputs[0].ItemId == "food:fermented-pickle"
            && fermentedPickle.Outputs[0].Amount == 2
            && Mathf.Approximately(fermentedPickle.Outputs[0].Probability, 1f)
            && Mathf.Approximately(fermentedPickle.CleanWaterPerCycle, 1.2f)
            && Mathf.Approximately(fermentedPickle.WastewaterPerCycle, 2f),
            "Fermented-pickle transform or fluid contract drifted.");

        ProductionRecipeSO preservedVegetable = recipes["recipe:preserved-vegetable"];
        Require(preservedVegetable.Inputs.Count == 3
            && HasInput(preservedVegetable, "material:brined-vegetable", 1)
            && HasInput(preservedVegetable, "material:washed-vegetable", 1)
            && HasInput(preservedVegetable, "craft:fermented-vinegar", 1)
            && preservedVegetable.Outputs.Count == 1
            && preservedVegetable.Outputs[0].ItemId == "food:preserved-vegetable"
            && preservedVegetable.Outputs[0].Amount == 2
            && Mathf.Approximately(preservedVegetable.Outputs[0].Probability, 1f)
            && Mathf.Approximately(preservedVegetable.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(preservedVegetable.WastewaterPerCycle, 0f),
            "Preserved-vegetable transform contract drifted.");

        ProductionRecipeSO dough = recipes["recipe:dough"];
        Require(dough.Inputs.Count == 2
            && HasInput(dough, "material:flour", 2)
            && HasInput(dough, "resource:egg", 1)
            && dough.Outputs.Count == 1
            && dough.Outputs[0].ItemId == "material:dough"
            && dough.Outputs[0].Amount == 2
            && Mathf.Approximately(dough.Outputs[0].Probability, 1f)
            && Mathf.Approximately(dough.CleanWaterPerCycle, 0.6f)
            && Mathf.Approximately(dough.WastewaterPerCycle, 0.2f),
            "Dough transform or fluid contract drifted.");

        ProductionRecipeSO seasonedFilling = recipes["recipe:seasoned-filling"];
        Require(seasonedFilling.Inputs.Count == 2
            && HasInput(seasonedFilling, "resource:meat", 2)
            && HasInput(seasonedFilling, "material:washed-vegetable", 1)
            && seasonedFilling.Outputs.Count == 1
            && seasonedFilling.Outputs[0].ItemId == "material:seasoned-filling"
            && seasonedFilling.Outputs[0].Amount == 2
            && Mathf.Approximately(seasonedFilling.Outputs[0].Probability, 1f)
            && Mathf.Approximately(seasonedFilling.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(seasonedFilling.WastewaterPerCycle, 0f),
            "Seasoned-filling transform contract drifted.");

        ProductionRecipeSO vegetablePie = recipes["recipe:vegetable-pie"];
        Require(vegetablePie.Inputs.Count == 2
            && HasInput(vegetablePie, "material:dough", 1)
            && HasInput(vegetablePie, "material:washed-vegetable", 1)
            && vegetablePie.Outputs.Count == 1
            && vegetablePie.Outputs[0].ItemId == "food:vegetable-pie"
            && vegetablePie.Outputs[0].Amount == 2
            && Mathf.Approximately(vegetablePie.Outputs[0].Probability, 1f)
            && Mathf.Approximately(vegetablePie.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(vegetablePie.WastewaterPerCycle, 0f),
            "Vegetable-pie transform contract drifted.");

        ProductionRecipeSO stuffedMushroom = recipes["recipe:stuffed-mushroom"];
        Require(stuffedMushroom.Inputs.Count == 2
            && HasInput(stuffedMushroom, "material:seasoned-filling", 1)
            && HasInput(stuffedMushroom, "resource:cave-mushroom", 2)
            && stuffedMushroom.Outputs.Count == 1
            && stuffedMushroom.Outputs[0].ItemId == "food:stuffed-mushroom"
            && stuffedMushroom.Outputs[0].Amount == 2
            && Mathf.Approximately(stuffedMushroom.Outputs[0].Probability, 1f)
            && Mathf.Approximately(stuffedMushroom.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(stuffedMushroom.WastewaterPerCycle, 0f),
            "Stuffed-mushroom transform contract drifted.");

        ProductionRecipeSO hayFeed = recipes["recipe:hay-feed"];
        Require(hayFeed.Inputs.Count == 2
            && HasInput(hayFeed, "resource:grass-straw", 3)
            && HasInput(hayFeed, "resource:twilight-grain", 1)
            && hayFeed.Outputs.Count == 1
            && hayFeed.Outputs[0].ItemId == "feed:hay"
            && hayFeed.Outputs[0].Amount == 3
            && Mathf.Approximately(hayFeed.Outputs[0].Probability, 1f)
            && Mathf.Approximately(hayFeed.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(hayFeed.WastewaterPerCycle, 0f),
            "Hay-feed transform contract drifted.");

        ProductionRecipeSO silage = recipes["recipe:silage"];
        Require(silage.Inputs.Count == 2
            && HasInput(silage, "resource:grass-straw", 3)
            && HasInput(silage, "resource:twilight-grain", 1)
            && silage.Outputs.Count == 1
            && silage.Outputs[0].ItemId == "feed:silage"
            && silage.Outputs[0].Amount == 3
            && Mathf.Approximately(silage.Outputs[0].Probability, 1f)
            && Mathf.Approximately(silage.CleanWaterPerCycle, 0.2f)
            && Mathf.Approximately(silage.WastewaterPerCycle, 0f),
            "Silage transform or fluid contract drifted.");

        ProductionRecipeSO meatPie = recipes["recipe:meat-pie"];
        Require(meatPie.Inputs.Count == 2
            && HasInput(meatPie, "material:dough", 1)
            && HasInput(meatPie, "material:seasoned-filling", 1)
            && meatPie.Outputs.Count == 1
            && meatPie.Outputs[0].ItemId == "food:meat-pie"
            && meatPie.Outputs[0].Amount == 2
            && Mathf.Approximately(meatPie.Outputs[0].Probability, 1f)
            && Mathf.Approximately(meatPie.CleanWaterPerCycle, 0f)
            && Mathf.Approximately(meatPie.WastewaterPerCycle, 0f),
            "Meat-pie transform contract drifted.");

        ProductionRecipeSO lavishVegan = recipes["recipe:lavish-vegan"];
        Require(lavishVegan.Inputs.Count == 4
            && HasInput(lavishVegan, "material:flour", 2)
            && HasInput(lavishVegan, "material:syrup", 1)
            && HasInput(lavishVegan, "resource:cave-mushroom", 2)
            && HasInput(lavishVegan, "resource:ember-root", 1)
            && lavishVegan.Outputs.Count == 1
            && lavishVegan.Outputs[0].ItemId == "food:lavish-vegan"
            && lavishVegan.Outputs[0].Amount == 2
            && Mathf.Approximately(lavishVegan.Outputs[0].Probability, 1f)
            && Mathf.Approximately(lavishVegan.CleanWaterPerCycle, 0.3f)
            && Mathf.Approximately(lavishVegan.WastewaterPerCycle, 0.25f),
            "Lavish-vegan transform or fluid contract drifted.");
    }

    private static bool HasInput(ProductionRecipeSO recipe, string itemId, int amount) =>
        recipe.Inputs.Any(value => value.ItemId == itemId && value.Amount == amount);

    private static void ValidateAnimalSource(ProductionRecipeSO source, string itemId)
    {
        Require(source.Inputs.Count == 0
            && source.Outputs.Count == 1
            && source.Outputs[0].ItemId == itemId
            && source.Outputs[0].Amount == 3
            && Mathf.Approximately(source.Outputs[0].Probability, 1f),
            $"Animal-product source contract drifted for {itemId}.");
    }

    private static void ValidateHaulBands(
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        IReadOnlyList<CanonicalItemUnitSemantic> semantics)
    {
        foreach (CanonicalItemUnitSemantic semantic in semantics)
        {
            long grams = semantic.CanonicalUnitMass.Value;
            Require(grams > 0, $"Non-positive semantic mass: {semantic.ItemId}.");
            if (semantic.HaulClass == PhysicalHaulMassClass.MicroUrgent)
            {
                Require(grams <= 11000,
                    $"Micro/urgent item exceeds 11 kg: {semantic.ItemId}.");
                continue;
            }
            if (semantic.HaulClass == PhysicalHaulMassClass.IndividualEquipment)
            {
                Require(grams <= 11000,
                    $"Individual equipment exceeds 11 kg: {semantic.ItemId}.");
                Require(items[semantic.ItemId].MaxStack >= 1,
                    $"Individual equipment cannot form one physical unit: {semantic.ItemId}.");
                continue;
            }
            if (semantic.HaulClass == PhysicalHaulMassClass.Heavy)
            {
                Require(grams > 11000 && grams <= 20000,
                    $"Heavy item is outside the 11-20 kg band: {semantic.ItemId}.");
                continue;
            }
            if (semantic.HaulClass == PhysicalHaulMassClass.OversizeEquipment)
            {
                Require(grams > 20000,
                    $"Oversize item does not exceed 20 kg: {semantic.ItemId}.");
                continue;
            }
            if (semantic.HaulClass == PhysicalHaulMassClass.DedicatedTransport)
                continue;

            Require(semantic.HaulClass == PhysicalHaulMassClass.Ordinary,
                $"Unknown haul class for {semantic.ItemId}: {semantic.HaulClass}.");
            long minimumUnits = (6000 + grams - 1) / grams;
            long maximumUnits = 11000 / grams;
            Require(minimumUnits <= maximumUnits,
                $"No 6-11 kg ordinary haul batch exists for {semantic.ItemId}.");
            Require(minimumUnits <= items[semantic.ItemId].MaxStack,
                $"Max stack blocks a 6 kg haul batch for {semantic.ItemId}.");
        }
    }

    private static byte[] BuildSemanticCsv(
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        IReadOnlyList<CanonicalItemUnitSemantic> semantics)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 8192);
        WriteRow(writer, new[]
        {
            "schemaVersion", "itemId", "unitSemanticKind", "unitLabel",
            "unitDescription", "nominalVolumeMilliLiters", "packageTareGrams",
            "tareDisposition", "packagingReviewDisposition", "containerItemId", "primaryMaterialId",
            "derivationKind", "beforeMassGrams", "proposedAfterMassGrams",
            "deltaGrams", "haulClass", "minimumUnitsFor6Kg",
            "maximumUnitsFor11Kg", "maxStack", "massBalanceSourceId",
            "reviewStatus", "assetApplied"
        });
        foreach (CanonicalItemUnitSemantic semantic in semantics)
        {
            long before = MassGrams(items[semantic.ItemId].UnitWeight);
            long after = semantic.CanonicalUnitMass.Value;
            WriteRow(writer, new[]
            {
                "v27.mass.explicit-semantic.2",
                semantic.ItemId,
                semantic.UnitSemanticKind.ToString(),
                semantic.UnitLabel,
                semantic.UnitDescription,
                semantic.NominalVolumeMilliLiters.ToString(CultureInfo.InvariantCulture),
                semantic.PackageTareGrams.ToString(CultureInfo.InvariantCulture),
                semantic.PackageTareDisposition.ToString(),
                semantic.PackagingReviewDisposition.ToString(),
                semantic.PackageContainerItemId,
                semantic.PrimaryMaterialId,
                semantic.MassDerivationKind.ToString(),
                before.ToString(CultureInfo.InvariantCulture),
                after.ToString(CultureInfo.InvariantCulture),
                (after - before).ToString(CultureInfo.InvariantCulture),
                semantic.HaulClass.ToString(),
                ((6000 + after - 1) / after).ToString(CultureInfo.InvariantCulture),
                (11000 / after).ToString(CultureInfo.InvariantCulture),
                items[semantic.ItemId].MaxStack.ToString(CultureInfo.InvariantCulture),
                semantic.MassBalanceSourceId,
                ResolveSemanticReviewStatus(semantic),
                IsAppliedMassItem(semantic.ItemId) ? "true" : "false"
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static string ResolveSemanticReviewStatus(
        CanonicalItemUnitSemantic semantic)
    {
        if (IsAppliedMassItem(semantic.ItemId))
            return "focused-applied";
        if (semantic.MassBalanceSourceId.StartsWith(
                "mass:semantic-authority:",
                StringComparison.Ordinal))
        {
            return "unit-reviewed-mass-provisional";
        }
        if (semantic.MassDerivationKind
                == PhysicalMassDerivationKind.EquipmentShapeAndMaterial
            || semantic.MassDerivationKind
                == PhysicalMassDerivationKind.ApparelShapeAndTextile)
        {
            return "authority-backed-mass-coherent";
        }
        return "reviewed-audit-only";
    }

    private static byte[] BuildProfileCsv(IReadOnlyList<MaterialMassProfile> profiles)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 4096);
        WriteRow(writer, new[]
        {
            "schemaVersion", "materialId", "densityGramsPerLiter",
            "defaultMoisturePermille", "packingEfficiencyPermille",
            "defaultProcessYieldPermille", "reviewStatus"
        });
        foreach (MaterialMassProfile profile in profiles)
        {
            WriteRow(writer, new[]
            {
                "v27.mass.material-profile.1",
                profile.MaterialId,
                profile.DensityGramsPerLiter.ToString(CultureInfo.InvariantCulture),
                profile.DefaultMoisturePermille.ToString(CultureInfo.InvariantCulture),
                profile.PackingEfficiencyPermille.ToString(CultureInfo.InvariantCulture),
                profile.DefaultProcessYieldPermille.ToString(CultureInfo.InvariantCulture),
                "reviewed-audit-only"
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildTransformCsv(
        IReadOnlyList<PhysicalMassTransformContract> transforms)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 4096);
        WriteRow(writer, new[]
        {
            "schemaVersion", "transformId", "physicalInputGrams",
            "infrastructureInputGrams", "physicalOutputGrams", "byproductGrams",
            "declaredLossGrams", "lossKind", "inputEqualsDisposition",
            "evidence", "reviewStatus"
        });
        foreach (PhysicalMassTransformContract transform in transforms)
        {
            WriteRow(writer, new[]
            {
                "v27.mass.transform.1",
                transform.TransformId,
                transform.PhysicalInputGrams.ToString(CultureInfo.InvariantCulture),
                transform.InfrastructureInputGrams.ToString(CultureInfo.InvariantCulture),
                transform.PhysicalOutputGrams.ToString(CultureInfo.InvariantCulture),
                transform.ByproductGrams.ToString(CultureInfo.InvariantCulture),
                transform.DeclaredLossGrams.ToString(CultureInfo.InvariantCulture),
                transform.LossKind.ToString(),
                (transform.TotalInputGrams == transform.TotalDispositionGrams)
                    .ToString().ToLowerInvariant(),
                transform.Evidence,
                "reviewed-audit-only"
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteRow(V27Utf8CsvWriter writer, IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index != 0)
                writer.WriteAscii(',');
            writer.WriteEscapedField((fields[index] ?? string.Empty).AsSpan());
        }
        writer.WriteCrLf();
    }

    private static bool IsAppliedMassItem(string itemId) =>
        string.Equals(itemId, "food:fresh-curd", StringComparison.Ordinal)
        || string.Equals(itemId, "food:cheese-mushroom", StringComparison.Ordinal)
        || string.Equals(itemId, "material:grape-juice", StringComparison.Ordinal)
        || string.Equals(itemId, "food:grape-syrup", StringComparison.Ordinal)
        || string.Equals(itemId, "material:young-wine", StringComparison.Ordinal)
        || string.Equals(itemId, "food:twilight-beer", StringComparison.Ordinal)
        || string.Equals(itemId, "drug:night-wine", StringComparison.Ordinal)
        || string.Equals(itemId, "food:vegetable-pie", StringComparison.Ordinal)
        || string.Equals(itemId, "food:stuffed-mushroom", StringComparison.Ordinal)
        || string.Equals(itemId, "feed:hay", StringComparison.Ordinal)
        || string.Equals(itemId, "feed:silage", StringComparison.Ordinal)
        || string.Equals(itemId, "food:meat-pie", StringComparison.Ordinal);

    private static Dictionary<string, T> UniqueIndex<T>(
        IEnumerable<T> values,
        Func<T, string> id,
        string label)
    {
        Dictionary<string, T> result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string key = id(value);
            Require(!string.IsNullOrWhiteSpace(key)
                && string.Equals(key, key.Trim(), StringComparison.Ordinal),
                $"{label} has a noncanonical stable ID.");
            Require(result.TryAdd(key, value), $"{label} has duplicate ID {key}.");
        }
        return result;
    }

    private static long MassOf(
        IReadOnlyList<CanonicalItemUnitSemantic> semantics,
        string itemId) => semantics.Single(value => string.Equals(
            value.ItemId,
            itemId,
            StringComparison.Ordinal)).CanonicalUnitMass.Value;

    private static long ScaleMass(long unitGrams, float units)
    {
        decimal exact = unitGrams * (decimal)units;
        Require(exact == decimal.Truncate(exact),
            $"Fluid mass scale is not an exact gram quantity: {exact}.");
        return checked((long)exact);
    }

    private static void ValidateBeforeMass(
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        string itemId,
        long expectedGrams)
    {
        Require(items.TryGetValue(itemId, out ItemDefinitionSO item),
            $"Missing live item {itemId}.");
        Require(MassGrams(item.UnitWeight) == expectedGrams,
            $"Before mass drifted for {itemId}.");
    }

    private static long MassGrams(float kilograms)
    {
        Require(float.IsFinite(kilograms) && kilograms > 0f,
            $"Invalid physical mass {kilograms:R}.");
        return checked((long)Math.Round(
            kilograms * 1000d,
            MidpointRounding.AwayFromZero));
    }

    private static string ComputeAggregateDigest(
        string projectRoot,
        IReadOnlyList<string> paths)
    {
        using SHA256 sha = SHA256.Create();
        foreach (string path in paths
                     .Select(CanonicalPath)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[] pathBytes = Encoding.UTF8.GetBytes(path);
            sha.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
            byte[] bytes = File.ReadAllBytes(ProjectAbsolute(projectRoot, path));
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        byte[] digest = sha.Hash ?? Array.Empty<byte>();
        char[] characters = new char[digest.Length * 2];
        const string Hex = "0123456789abcdef";
        for (int index = 0; index < digest.Length; index++)
        {
            characters[index * 2] = Hex[digest[index] >> 4];
            characters[index * 2 + 1] = Hex[digest[index] & 0x0f];
        }
        return new string(characters);
    }

    private static string ProjectAbsolute(string projectRoot, string path) =>
        Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar));

    private static string CanonicalPath(string path) =>
        (path ?? string.Empty).Replace('\\', '/');

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CaptureResult
    {
        public CaptureResult(
            byte[] report,
            byte[] semantics,
            byte[] profiles,
            byte[] transforms,
            int ledgerCount,
            int explicitCount,
            int missingCount,
            int transformCount)
        {
            Report = report;
            Semantics = semantics;
            Profiles = profiles;
            Transforms = transforms;
            LedgerCount = ledgerCount;
            ExplicitCount = explicitCount;
            MissingCount = missingCount;
            TransformCount = transformCount;
        }

        public byte[] Report { get; }
        public byte[] Semantics { get; }
        public byte[] Profiles { get; }
        public byte[] Transforms { get; }
        public int LedgerCount { get; }
        public int ExplicitCount { get; }
        public int MissingCount { get; }
        public int TransformCount { get; }
    }
}
#endif
