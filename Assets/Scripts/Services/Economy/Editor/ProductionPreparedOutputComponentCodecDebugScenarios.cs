#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionPreparedOutputComponentCodecDebugScenarios
{
    private static readonly string[] FeedbenchItemAssets =
    {
        "Assets/Resources/SO/Economy/Items/feed_dog_food.asset",
        "Assets/Resources/SO/Economy/Items/feed_hay.asset",
        "Assets/Resources/SO/Economy/Items/Workshop/feed_silage.asset"
    };
    private const string PlantRotAsset =
        "Assets/Resources/SO/Economy/Items/waste_plant_rot.asset";
    private const string LumberAsset =
        "Assets/Resources/SO/Economy/Items/material_lumber.asset";
    private static readonly string[] WorkOnlyDefinitionAssets =
    {
        "Assets/Resources/SO/Economy/Items/material_charcoal.asset",
        "Assets/Resources/SO/Economy/Items/material_flour.asset",
        "Assets/Resources/SO/Economy/Items/Workshop/material_malt.asset",
        "Assets/Resources/SO/Economy/Items/material_starch.asset",
        "Assets/Resources/SO/Economy/Items/material_steel_ingot.asset",
        "Assets/Resources/SO/Economy/Items/material_treated_lumber.asset"
    };

    [MenuItem("DungeonStory/Debug/Economy/Run Prepared Output Component Codec")]
    public static void RunAll()
    {
        VerifyFeedbenchDefinitions();
        VerifyPlantRotDefinition();
        VerifyLumberDefinition();
        VerifyWorkOnlyDefinitions();
        VerifyCanonicalRoundTrip();
        VerifyStatefulDefinitionsFailLoud();
        Debug.Log("V27_PRODUCTION_PREPARED_OUTPUT_COMPONENT_CODEC=PASS");
    }

    private static void VerifyWorkOnlyDefinitions()
    {
        IProductionPreparedOutputComponentCodec codec =
            new ProductionPreparedOutputComponentCodec();
        foreach (string assetPath in WorkOnlyDefinitionAssets)
        {
            ResourceItemDefinitionSO definition =
                AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(assetPath);
            Require(definition != null,
                $"Missing WorkOnly prepared-output item: {assetPath}");
            ProductionPreparedOutputComponentProjection encoded =
                codec.Create(definition);
            RequireGeneric(encoded, definition.ItemId);
            ProductionPreparedOutputComponentProjection decoded =
                codec.ValidateAndDecode(
                    definition,
                    encoded.CanonicalPayload,
                    encoded.Fingerprint);
            RequireGeneric(decoded, definition.ItemId);
            Require(string.Equals(
                    encoded.Fingerprint,
                    decoded.Fingerprint,
                    StringComparison.Ordinal),
                $"WorkOnly component fingerprint changed for '{definition.ItemId}'.");
        }
    }

    private static void VerifyLumberDefinition()
    {
        ResourceItemDefinitionSO definition =
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(LumberAsset);
        Require(definition != null, $"Missing lumber item: {LumberAsset}");
        IProductionPreparedOutputComponentCodec codec =
            new ProductionPreparedOutputComponentCodec();
        ProductionPreparedOutputComponentProjection encoded =
            codec.Create(definition);
        RequireGeneric(encoded, "material:lumber");
        ProductionPreparedOutputComponentProjection decoded =
            codec.ValidateAndDecode(
                definition,
                encoded.CanonicalPayload,
                encoded.Fingerprint);
        RequireGeneric(decoded, "material:lumber");
        Require(string.Equals(
                encoded.Fingerprint,
                decoded.Fingerprint,
                StringComparison.Ordinal),
            "Lumber component fingerprint changed during round-trip.");
    }

    private static void VerifyPlantRotDefinition()
    {
        ResourceItemDefinitionSO definition =
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(PlantRotAsset);
        Require(definition != null, $"Missing plant-rot item: {PlantRotAsset}");
        IProductionPreparedOutputComponentCodec codec =
            new ProductionPreparedOutputComponentCodec();
        ProductionPreparedOutputComponentProjection encoded =
            codec.Create(definition);
        RequireGeneric(encoded, "waste:plant-rot");
        ProductionPreparedOutputComponentProjection decoded =
            codec.ValidateAndDecode(
                definition,
                encoded.CanonicalPayload,
                encoded.Fingerprint);
        RequireGeneric(decoded, "waste:plant-rot");
        Require(string.Equals(
                encoded.Fingerprint,
                decoded.Fingerprint,
                StringComparison.Ordinal),
            "Plant-rot component fingerprint changed during round-trip.");
    }

    private static void VerifyFeedbenchDefinitions()
    {
        IProductionPreparedOutputComponentCodec codec =
            new ProductionPreparedOutputComponentCodec();
        foreach (string assetPath in FeedbenchItemAssets)
        {
            ResourceItemDefinitionSO definition =
                AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(assetPath);
            Require(definition != null, $"Missing feedbench item: {assetPath}");
            ProductionPreparedOutputComponentProjection encoded =
                codec.Create(definition);
            RequireGeneric(encoded, definition.ItemId);
            ProductionPreparedOutputComponentProjection decoded =
                codec.ValidateAndDecode(
                    definition,
                    encoded.CanonicalPayload,
                    encoded.Fingerprint);
            Require(
                string.Equals(
                    encoded.CanonicalPayload,
                    decoded.CanonicalPayload,
                    StringComparison.Ordinal)
                && string.Equals(
                    encoded.Fingerprint,
                    decoded.Fingerprint,
                    StringComparison.Ordinal),
                $"Feedbench component round-trip changed '{definition.ItemId}'.");
        }
    }

    private static void VerifyCanonicalRoundTrip()
    {
        ResourceItemDefinitionSO definition = CreateDefinition("feed:hay");
        ResourceItemDefinitionSO driftedDefinition =
            CreateDefinition("feed:hay");
        try
        {
            IProductionPreparedOutputComponentCodec codec =
                new ProductionPreparedOutputComponentCodec();
            ProductionPreparedOutputComponentProjection first = codec.Create(definition);
            ProductionPreparedOutputComponentProjection second = codec.Create(definition);
            RequireGeneric(first, definition.ItemId);
            Require(
                string.Equals(
                    first.CanonicalPayload,
                    "production-prepared-output-components@1|kind=generic-definition|item=8:feed:hay|components=0",
                    StringComparison.Ordinal),
                "Definition-only payload is not byte-length canonical.");
            Require(
                string.Equals(first.Fingerprint, second.Fingerprint, StringComparison.Ordinal)
                && string.Equals(
                    first.ItemDefinitionDigest,
                    second.ItemDefinitionDigest,
                    StringComparison.Ordinal)
                && first.ItemDefinitionDigest.Length == 64
                && first.Fingerprint.Length == 64
                && first.Fingerprint.All(character =>
                    character is >= '0' and <= '9'
                    || character is >= 'a' and <= 'f'),
                "Definition-only fingerprint is not deterministic lowercase SHA-256.");

            RequireFailure(
                () => codec.ValidateAndDecode(
                    definition,
                    first.CanonicalPayload + " ",
                    first.Fingerprint),
                ProductionPreparedOutputComponentFailureCode.NonCanonicalPayload);
            RequireFailure(
                () => codec.ValidateAndDecode(
                    definition,
                    first.CanonicalPayload,
                    first.Fingerprint.ToUpperInvariant()),
                ProductionPreparedOutputComponentFailureCode.FingerprintMismatch);
            RequireFailure(
                () => codec.ValidateAndDecode(
                    definition,
                    first.CanonicalPayload,
                    new string('0', 64)),
                ProductionPreparedOutputComponentFailureCode.FingerprintMismatch);

            driftedDefinition.Configure(
                "feed:hay",
                "feed:hay",
                "prepared output codec fixture",
                StockCategory.General,
                ResourceItemKind.FinishedGood,
                ResourceIngredientTag.Feed,
                price: 1,
                weight: 0.251f,
                stackLimit: 50,
                researchId: string.Empty);
            RequireFailure(
                () => codec.ValidateAndDecode(
                    driftedDefinition,
                    first.CanonicalPayload,
                    first.Fingerprint),
                ProductionPreparedOutputComponentFailureCode
                    .FingerprintMismatch);
            RequireFailureToken(
                () => ProductionPreparedOutputComponentProfileDigest.Validate(
                    driftedDefinition,
                    first.CanonicalPayload,
                    first.Fingerprint,
                    "component-profile-fixture"),
                ProductionPreparedOutputComponentProfileDigest
                    .StaleFailureToken);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(driftedDefinition);
        }
    }

    private static void VerifyStatefulDefinitionsFailLoud()
    {
        IProductionPreparedOutputComponentCodec codec =
            new ProductionPreparedOutputComponentCodec();
        ResourceItemDefinitionSO equipment = CreateDefinition("feed:hay");
        ResourceItemDefinitionSO packaged = CreateDefinition("feed:dog-food");
        ResourceItemDefinitionSO food = CreateDefinition("feed:silage");
        ResourceItemDefinitionSO custom = CreateDefinition("feed:hay");
        ResourceItemDefinitionSO module = CreateDefinition(PhysicalItemIds.EquipmentModule);
        try
        {
            equipment.SetFeature(new EquipmentItemFeature
            {
                equipmentDefinitionId = "equipment:qa"
            });
            packaged.SetFeature(new PackagedLotItemFeature
            {
                packageTareGrams = 50,
                tareDisposition = PackageTareDisposition.ReusableContainerReturn,
                containerItemId = "container:qa"
            });
            food.SetFeature(new FoodItemFeature
            {
                nutrition = 10f,
                freshnessSeconds = 60f
            });
            custom.SetFeature(new StatefulQaFeature());

            foreach (ResourceItemDefinitionSO definition in new[]
                     { equipment, packaged, food, custom, module })
            {
                RequireFailure(
                    () => codec.Create(definition),
                    ProductionPreparedOutputComponentFailureCode
                        .UnsupportedStatefulDefinition);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(equipment);
            UnityEngine.Object.DestroyImmediate(packaged);
            UnityEngine.Object.DestroyImmediate(food);
            UnityEngine.Object.DestroyImmediate(custom);
            UnityEngine.Object.DestroyImmediate(module);
        }
    }

    private static ResourceItemDefinitionSO CreateDefinition(string itemId)
    {
        ResourceItemDefinitionSO definition =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        definition.Configure(
            itemId,
            itemId,
            "prepared output codec fixture",
            StockCategory.General,
            ResourceItemKind.FinishedGood,
            ResourceIngredientTag.Feed,
            price: 1,
            weight: 0.25f,
            stackLimit: 50,
            researchId: string.Empty);
        definition.ConfigureFacilitySupply(
            authoredFuelValue: 0f,
            authoredNutritionValue: 1f,
            canFeedFacilities: true,
            isSharedIntermediate: false);
        definition.ConfigureMarketSaleRate(0.5f);
        return definition;
    }

    private static void RequireGeneric(
        ProductionPreparedOutputComponentProjection projection,
        string itemId)
    {
        Require(
            projection != null
            && projection.MassSubject != null
            && projection.MassSubject.Kind ==
                PhysicalItemMassSubjectKind.GenericDefinition
            && string.Equals(
                projection.MassSubject.ItemId.Value,
                itemId,
                StringComparison.Ordinal)
            && projection.MassSubject.Components.Count == 0
            && projection.RuntimeComponents.Count == 0,
            $"Prepared output '{itemId}' did not remain definition-only.");
    }

    private static void RequireFailure(
        Action action,
        ProductionPreparedOutputComponentFailureCode expected)
    {
        try
        {
            action();
        }
        catch (ProductionPreparedOutputComponentCodecException exception)
        {
            Require(
                exception.FailureCode == expected,
                $"Expected {expected}, got {exception.FailureCode}.");
            return;
        }
        throw new InvalidOperationException(
            $"Prepared output component codec accepted {expected} input.");
    }

    private static void RequireFailureToken(Action action, string token)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(token, StringComparison.Ordinal),
                $"Expected failure token '{token}', got '{exception.Message}'.");
            return;
        }
        throw new InvalidOperationException(
            $"Prepared output component profile accepted '{token}' input.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    [Serializable]
    private sealed class StatefulQaFeature : ItemFeatureDefinition
    {
        public override string FeatureId => "qa-stateful";
    }
}
#endif
