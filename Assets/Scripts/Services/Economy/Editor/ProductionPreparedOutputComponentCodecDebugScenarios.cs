#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
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
        VerifyParameterContentCanary();
        VerifyCanonicalRoundTrip();
        VerifyStatefulDefinitionsFailLoud();
        VerifyPerishableFoodProjection();
        VerifyMaterializerRegistryExtensionClosure();
        Debug.Log("V27_PRODUCTION_PREPARED_OUTPUT_COMPONENT_CODEC=PASS");
    }

    private static void VerifyMaterializerRegistryExtensionClosure()
    {
        const string extensionCapabilityId =
            "production-output:qa-prepared-extension";
        const string extensionCodecId =
            "production-output-codec:qa-prepared-extension";
        const string exactCapabilityId = "production-output:qa-exact";
        const string exactCodecId = "production-output-codec:qa-exact";
        ResourceItemDefinitionSO definition = CreateDefinition(
            "item:qa:prepared-extension");
        try
        {
            ProductionPreparedOutputComponentCodec componentCodec = new();
            FakePreparedCapability standard = new(
                ProductionOutputCapabilityIds.StandardDefinition,
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
                new[] { "item:qa:standard-definition" });
            FakePreparedCapability extension = new(
                extensionCapabilityId,
                7,
                extensionCodecId,
                3,
                new[] { definition.ItemId });
            ProductionOutputHandlerRegistry forwardCapabilities = new(
                new IProductionOutputCapability[] { extension, standard });
            ProductionOutputHandlerRegistry reverseCapabilities = new(
                new IProductionOutputCapability[] { standard, extension });
            FakePreparedMaterializer standardMaterializer = new(
                standard.CapabilityId,
                standard.ContractVersion,
                standard.ComponentCodecId,
                standard.ComponentCodecVersion,
                componentCodec);
            FakePreparedMaterializer extensionMaterializer = new(
                extension.CapabilityId,
                extension.ContractVersion,
                extension.ComponentCodecId,
                extension.ComponentCodecVersion,
                componentCodec);
            ProductionPreparedOutputMaterializerRegistry forward = new(
                new IProductionPreparedOutputMaterializer[]
                {
                    extensionMaterializer,
                    standardMaterializer
                },
                forwardCapabilities);
            ProductionPreparedOutputMaterializerRegistry reverse = new(
                new IProductionPreparedOutputMaterializer[]
                {
                    standardMaterializer,
                    extensionMaterializer
                },
                reverseCapabilities);
            Require(
                string.Equals(
                    forward.RegistryFingerprint,
                    reverse.RegistryFingerprint,
                    StringComparison.Ordinal),
                "Materializer registry fingerprint depended on registration order.");

            ProductionOutputCapabilityDescriptor descriptor =
                forwardCapabilities.CaptureDeclaredDescriptor(
                    "output:qa-prepared-extension",
                    definition.ItemId,
                    extensionCapabilityId);
            Require(
                ProductionPreparedOutputCapabilitySelection
                    .ClassifyPhysicalCapabilities(
                        new[] { descriptor },
                        forwardCapabilities.CapabilityContracts)
                    == ProductionOutputCapabilityRoute.PreparedBatch,
                "Nonstandard prepared capability did not enter the prepared batch route.");
            ProductionPreparedOutputComponentProjection encoded =
                forward.Create(descriptor, definition);
            ProductionPreparedOutputComponentProjection decoded =
                reverse.ValidateAndDecode(
                    descriptor,
                    definition,
                    encoded.CanonicalPayload,
                    encoded.Fingerprint);
            RequireGeneric(encoded, definition.ItemId);
            RequireGeneric(decoded, definition.ItemId);
            Require(
                string.Equals(
                    encoded.Fingerprint,
                    decoded.Fingerprint,
                    StringComparison.Ordinal),
                "Nonstandard prepared materializer changed during round-trip.");

            RequireThrows(
                () => new ProductionPreparedOutputMaterializerRegistry(
                    new IProductionPreparedOutputMaterializer[]
                    {
                        standardMaterializer
                    },
                    forwardCapabilities),
                "Prepared participant without a materializer was accepted.");
            RequireThrows(
                () => new ProductionPreparedOutputMaterializerRegistry(
                    new IProductionPreparedOutputMaterializer[]
                    {
                        standardMaterializer,
                        extensionMaterializer,
                        extensionMaterializer
                    },
                    forwardCapabilities),
                "Duplicate prepared materializer was accepted.");
            RequireThrows(
                () => new ProductionPreparedOutputMaterializerRegistry(
                    new IProductionPreparedOutputMaterializer[]
                    {
                        standardMaterializer,
                        new FakePreparedMaterializer(
                            extensionCapabilityId,
                            extension.ContractVersion + 1,
                            extensionCodecId,
                            extension.ComponentCodecVersion,
                            componentCodec)
                    },
                    forwardCapabilities),
                "Drifted prepared materializer metadata was accepted.");

            FakeExactCapability exact = new(
                exactCapabilityId,
                exactCodecId,
                new[] { definition.ItemId });
            ProductionOutputHandlerRegistry exactCapabilities = new(
                new IProductionOutputCapability[] { standard, exact });
            RequireThrows(
                () => new ProductionPreparedOutputMaterializerRegistry(
                    new IProductionPreparedOutputMaterializer[]
                    {
                        standardMaterializer,
                        new FakePreparedMaterializer(
                            exact.CapabilityId,
                            exact.ContractVersion,
                            exact.ComponentCodecId,
                            exact.ComponentCodecVersion,
                            componentCodec)
                    },
                    exactCapabilities),
                "Nonparticipant capability materializer was accepted.");

            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                CultureInfo.CurrentUICulture =
                    CultureInfo.GetCultureInfo("tr-TR");
                ProductionPreparedOutputMaterializerRegistry culture = new(
                    new IProductionPreparedOutputMaterializer[]
                    {
                        extensionMaterializer,
                        standardMaterializer
                    },
                    reverseCapabilities);
                Require(
                    string.Equals(
                        forward.RegistryFingerprint,
                        culture.RegistryFingerprint,
                        StringComparison.Ordinal),
                    "Materializer registry fingerprint depended on locale.");
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void VerifyParameterContentCanary()
    {
        ResourceItemDefinitionSO canary = CreateDefinition(
            "material:qa-definition-only-canary");
        try
        {
            IProductionPreparedOutputComponentCodec codec =
                new ProductionPreparedOutputComponentCodec();
            ProductionPreparedOutputComponentProjection encoded =
                codec.Create(canary);
            ProductionPreparedOutputComponentProjection decoded =
                codec.ValidateAndDecode(
                    canary,
                    encoded.CanonicalPayload,
                    encoded.Fingerprint);
            RequireGeneric(encoded, canary.ItemId);
            RequireGeneric(decoded, canary.ItemId);
            Require(
                string.Equals(
                    encoded.Fingerprint,
                    decoded.Fingerprint,
                    StringComparison.Ordinal),
                "Definition-only parameter canary changed during round-trip.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canary);
        }
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

            RequireGeneric(codec.Create(packaged), packaged.ItemId);
            foreach (ResourceItemDefinitionSO definition in new[]
                     { equipment, food, custom, module })
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

    private static void VerifyPerishableFoodProjection()
    {
        ResourceItemDefinitionSO food = ScriptableObject
            .CreateInstance<ResourceItemDefinitionSO>();
        try
        {
            food.Configure(
                "meal:qa:fresh",
                "meal:qa:fresh",
                "perishable prepared-output fixture",
                StockCategory.Food,
                ResourceItemKind.FinishedGood,
                ResourceIngredientTag.Plant,
                price: 2,
                weight: 0.4f,
                stackLimit: 20,
                researchId: string.Empty);
            food.SetFeature(new FoodItemFeature
            {
                nutrition = 35f,
                freshnessSeconds = 180f,
                preserved = false
            });
            food.ConfigureMarketSaleRate(0.5f);

            ProductionOutputCapabilityDescriptor descriptor = new(
                "output:qa:fresh-food",
                food.ItemId,
                ProductionOutputCapabilityIds.PerishableFood,
                ProductionOutputCapabilityIds.PerishableFoodVersion,
                ProductionOutputCapabilityIds.PerishableFoodFreshnessCodec,
                ProductionOutputCapabilityIds
                    .PerishableFoodFreshnessCodecVersion,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    "output:qa:fresh-food",
                    food.ItemId,
                    ProductionOutputCapabilityIds.PerishableFood,
                    ProductionOutputCapabilityIds.PerishableFoodVersion,
                    ProductionOutputCapabilityIds
                        .PerishableFoodFreshnessCodec,
                    ProductionOutputCapabilityIds
                        .PerishableFoodFreshnessCodecVersion));
            PerishableFoodPreparedOutputMaterializer materializer = new();
            ProductionPreparedOutputComponentProjection first =
                materializer.Create(descriptor, food);
            ProductionPreparedOutputComponentProjection decoded =
                materializer.ValidateAndDecode(
                    descriptor,
                    food,
                    first.CanonicalPayload,
                    first.Fingerprint);

            Require(first.MassSubject.Kind ==
                    PhysicalItemMassSubjectKind.GenericDefinition
                && first.MassSubject.Components.Count == 0
                && first.MassSubject.ComponentFingerprint.Length == 0
                && first.RuntimeComponents.Count == 1,
                "Perishable food did not separate non-mass freshness from its generic mass subject.");
            Require(FoodFreshnessComponentCodec.TryRead(
                    first.RuntimeComponents,
                    out double remaining,
                    out bool preserved)
                && remaining == 180d
                && !preserved,
                "Perishable food did not materialize exact authored freshness.");
            ItemInstanceComponentSaveData aged =
                FoodFreshnessComponentCodec.Create(119.375d, false);
            Require(string.Equals(
                    FacilityBufferPlannedOutputPublicationService
                        .CreateRuntimeComponentSignature(
                            first.RuntimeComponents),
                    FacilityBufferPlannedOutputPublicationService
                        .CreateRuntimeComponentSignature(new[] { aged }),
                    StringComparison.Ordinal),
                "Legitimate freshness aging changed exact-route custody identity.");
            ItemInstanceComponentSaveData malformed = aged.Clone();
            malformed.values.Add(new ItemStateValueSaveData
            {
                key = "unexpected",
                kind = ItemStateValueKind.Integer,
                integerValue = 1L
            });
            Require(!FoodFreshnessComponentCodec.TryRead(
                    new[] { malformed },
                    out _,
                    out _)
                && !string.Equals(
                    FacilityBufferPlannedOutputPublicationService
                        .CreateRuntimeComponentSignature(new[] { malformed }),
                    FacilityBufferPlannedOutputPublicationService
                        .CreateRuntimeComponentSignature(
                            first.RuntimeComponents),
                    StringComparison.Ordinal),
                "Malformed freshness was hidden from exact-route validation.");
            Require(
                string.Equals(
                    first.CanonicalPayload,
                    decoded.CanonicalPayload,
                    StringComparison.Ordinal)
                && string.Equals(
                    first.Fingerprint,
                    decoded.Fingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    first.RuntimeComponents[0].ToCanonicalString(),
                    decoded.RuntimeComponents[0].ToCanonicalString(),
                    StringComparison.Ordinal),
                "Perishable food freshness changed during canonical round-trip.");
            RequireFailure(
                () => new ProductionPreparedOutputComponentCodec().Create(food),
                ProductionPreparedOutputComponentFailureCode
                    .UnsupportedStatefulDefinition);
            RequireFailure(
                () => materializer.ValidateAndDecode(
                    descriptor,
                    food,
                    first.CanonicalPayload + " ",
                    first.Fingerprint),
                ProductionPreparedOutputComponentFailureCode
                    .NonCanonicalPayload);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(food);
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

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
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

    private sealed class FakePreparedCapability :
        IProductionOutputCapability,
        IProductionPreparedOutputParticipantCapability
    {
        private readonly HashSet<string> itemIds;

        internal FakePreparedCapability(
            string capabilityId,
            int contractVersion,
            string componentCodecId,
            int componentCodecVersion,
            IEnumerable<string> itemIds)
        {
            CapabilityId = capabilityId;
            ContractVersion = contractVersion;
            ComponentCodecId = componentCodecId;
            ComponentCodecVersion = componentCodecVersion;
            this.itemIds = new HashSet<string>(
                itemIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
        }

        public string CapabilityId { get; }
        public int ContractVersion { get; }
        public string ComponentCodecId { get; }
        public int ComponentCodecVersion { get; }
        public bool SupportsAutomaticSelection => false;
        public bool CanHandle(string itemId) => itemIds.Contains(itemId);
    }

    private sealed class FakeExactCapability : IProductionOutputCapability
    {
        private readonly HashSet<string> itemIds;

        internal FakeExactCapability(
            string capabilityId,
            string componentCodecId,
            IEnumerable<string> itemIds)
        {
            CapabilityId = capabilityId;
            ComponentCodecId = componentCodecId;
            itemIds = itemIds ?? Array.Empty<string>();
            this.itemIds = new HashSet<string>(itemIds, StringComparer.Ordinal);
        }

        public string CapabilityId { get; }
        public int ContractVersion => 1;
        public string ComponentCodecId { get; }
        public int ComponentCodecVersion => 1;
        public bool SupportsAutomaticSelection => false;
        public bool CanHandle(string itemId) => itemIds.Contains(itemId);
    }

    private sealed class FakePreparedMaterializer :
        IProductionPreparedOutputMaterializer
    {
        private readonly IProductionPreparedOutputComponentCodec codec;

        internal FakePreparedMaterializer(
            string capabilityId,
            int capabilityVersion,
            string componentCodecId,
            int componentCodecVersion,
            IProductionPreparedOutputComponentCodec codec)
        {
            CapabilityId = capabilityId;
            CapabilityVersion = capabilityVersion;
            ComponentCodecId = componentCodecId;
            ComponentCodecVersion = componentCodecVersion;
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        public string CapabilityId { get; }
        public int CapabilityVersion { get; }
        public string ComponentCodecId { get; }
        public int ComponentCodecVersion { get; }

        public ProductionPreparedOutputComponentProjection Create(
            ProductionOutputCapabilityDescriptor descriptor,
            ItemDefinitionSO definition) => codec.Create(definition);

        public ProductionPreparedOutputComponentProjection ValidateAndDecode(
            ProductionOutputCapabilityDescriptor descriptor,
            ItemDefinitionSO definition,
            string canonicalPayload,
            string fingerprint) => codec.ValidateAndDecode(
            definition,
            canonicalPayload,
            fingerprint);
    }
}
#endif
