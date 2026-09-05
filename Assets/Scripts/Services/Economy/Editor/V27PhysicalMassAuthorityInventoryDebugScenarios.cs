#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

public static class V27PhysicalMassAuthorityInventoryDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-physical-mass-authority-inventory.txt";
    public const string CsvPath =
        "Artifacts/QA/v27-physical-mass-authority-inventory.csv";
    public const string WriterManifestPath =
        "Artifacts/QA/v27-physical-mass-weight-writer-manifest.txt";
    public const string SemanticCandidatesPath =
        "Artifacts/QA/v27-physical-mass-unit-semantic-candidates.csv";

    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassAuthorityInventoryDebugScenarios.cs";
    private const string SchemaPath =
        "Assets/Scripts/Models/Economy/Content/PhysicalMassAuthoringContracts.cs";

    [MenuItem("DungeonStory/V27/Physical Mass/Capture Authority Inventory")]
    public static void RunFromMenu()
    {
        CaptureResult first = Capture();
        CaptureResult second = Capture();
        Require(first.Csv.SequenceEqual(second.Csv),
            "Physical-mass authority CSV changed between identical captures.");
        Require(first.Report.SequenceEqual(second.Report),
            "Physical-mass authority report changed between identical captures.");
        Require(first.WriterManifest.SequenceEqual(second.WriterManifest),
            "Physical-mass writer manifest changed between identical captures.");
        Require(first.SemanticCandidates.SequenceEqual(second.SemanticCandidates),
            "Physical-mass unit semantic candidates changed between identical captures.");

        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
            stream.Write(first.Csv, 0, first.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(WriterManifestPath, stream =>
            stream.Write(first.WriterManifest, 0, first.WriterManifest.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(SemanticCandidatesPath, stream =>
            stream.Write(first.SemanticCandidates, 0, first.SemanticCandidates.Length));

        Debug.Log(
            $"V27 physical-mass authority inventory passed: "
            + $"ledgerItems={first.LedgerItemCount}; sites={first.SiteCount}; "
            + $"recipes={first.RecipeCount}; equipment={first.EquipmentCount}; "
            + "asset mutations=0; deterministic recapture=PASS.");
    }

    public static IReadOnlyList<string> CaptureCanonicalLedgerItemIds()
    {
        return CaptureCurrentEconomyDenominator().LedgerItemIds;
    }

    internal static EconomyDenominatorSnapshot CaptureCurrentEconomyDenominator()
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
        ItemDefinitionSO[] catalogItems = itemCatalog.Definitions
            .Where(value => value != null)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        ProductionRecipeSO[] recipes = UniqueDefinitions(
            domain.GetAll<ProductionRecipeSO>(),
            value => value.RecipeId,
            "recipe");
        CropDefinitionSO[] crops = UniqueDefinitions(
            domain.GetAll<CropDefinitionSO>(),
            value => value.CropId,
            "crop");
        CombatEquipmentDefinitionSO[] equipment = UniqueDefinitions(
            domain.GetAll<CombatEquipmentDefinitionSO>(),
            value => value.EquipmentId,
            "equipment");
        CraftMaterialDefinitionSO[] materials = UniqueDefinitions(
            domain.GetAll<CraftMaterialDefinitionSO>(),
            value => value.MaterialId,
            "craft material");
        MassContentSource content = new MassContentSource(domain, itemCatalog);
        ResourceMaterialEconomicProfileCatalog materialProfiles = new(content);
        V23BalanceWorkCalculator work = new V23BalanceWorkCalculator(materialProfiles);
        EmbeddedWorkValueSnapshot before = new V23EmbeddedWorkValueCalculator(
            recipes,
            catalogItems,
            equipment,
            materials,
            work).Calculate();
        V27EmbeddedWorkValueSnapshot after = new V27EmbeddedWorkValueCalculator(
            recipes,
            crops,
            catalogItems,
            equipment,
            materials,
            before,
            work,
            materialProfiles,
            2.25m,
            V27BalanceAssetApplication.CaptureHistoricalBeforeValues()).Calculate();
        if (before.UnresolvedItemIds.Count != 0
            || before.NonConvergentRecipeIds.Count != 0
            || !after.IsComplete)
        {
            throw new InvalidOperationException(
                "The current economy denominator has unresolved or non-convergent EWU authority.");
        }

        ResourceEconomyContentCatalog economy = new(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        string[] catalogItemIds = catalogItems
            .Select(value => value.ItemId)
            .ToArray();
        string[] catalogRecipeIds = recipes
            .Select(value => value.RecipeId)
            .ToArray();
        string[] expectedEconomyItemIds = catalogItems
            .OfType<ResourceItemDefinitionSO>()
            .Select(value => value.ItemId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedEwuRecipeIds = recipes
            .Where(value => value.FlowRole != ProductionFlowRole.Sink
                && value.Outputs.Count > 0)
            .Select(value => value.RecipeId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> producedItemIds = recipes
            .SelectMany(value => value.Outputs)
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.ItemId))
            .Select(value => value.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        string[] expectedExternalSeedItemIds = recipes
            .SelectMany(value => value.Inputs)
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.ItemId)
                && !producedItemIds.Contains(value.ItemId))
            .Select(value => value.ItemId)
            .Where(value => catalogItemIds.Contains(
                value,
                StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] ledgerItemIds = catalogItems
            .Where(value =>
                after.Items.ContainsKey(value.ItemId)
                && before.TryGetItemWork(value.ItemId, out _))
            .Select(value => value.ItemId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new EconomyDenominatorSnapshot(
            catalogItemIds,
            catalogRecipeIds,
            economy.Items.Select(value => value.ItemId),
            economy.Recipes.Select(value => value.RecipeId),
            before.ItemWork.Keys,
            before.Recipes.Keys,
            after.Items.Keys,
            after.Recipes.Keys,
            expectedEconomyItemIds,
            expectedEwuRecipeIds,
            expectedExternalSeedItemIds,
            before.ExternalSeedItemIds,
            after.ExternalSeedItemIds,
            ledgerItemIds);
    }

    internal sealed class EconomyDenominatorSnapshot
    {
        internal EconomyDenominatorSnapshot(
            IEnumerable<string> catalogItemIds,
            IEnumerable<string> catalogRecipeIds,
            IEnumerable<string> economyItemIds,
            IEnumerable<string> economyRecipeIds,
            IEnumerable<string> v23ItemIds,
            IEnumerable<string> v23RecipeIds,
            IEnumerable<string> v27ItemIds,
            IEnumerable<string> v27RecipeIds,
            IEnumerable<string> expectedEconomyItemIds,
            IEnumerable<string> expectedEwuRecipeIds,
            IEnumerable<string> expectedExternalSeedItemIds,
            IEnumerable<string> v23ExternalSeedItemIds,
            IEnumerable<string> v27ExternalSeedItemIds,
            IEnumerable<string> ledgerItemIds)
        {
            CatalogItemIds = CanonicalSet(catalogItemIds, "catalog item");
            CatalogRecipeIds = CanonicalSet(catalogRecipeIds, "catalog recipe");
            EconomyItemIds = CanonicalSet(economyItemIds, "economy item");
            EconomyRecipeIds = CanonicalSet(economyRecipeIds, "economy recipe");
            V23ItemIds = CanonicalSet(v23ItemIds, "V23 EWU item");
            V23RecipeIds = CanonicalSet(v23RecipeIds, "V23 EWU recipe");
            V27ItemIds = CanonicalSet(v27ItemIds, "V27 EWU item");
            V27RecipeIds = CanonicalSet(v27RecipeIds, "V27 EWU recipe");
            string[] expectedEconomyItems = CanonicalSet(
                expectedEconomyItemIds,
                "expected economy item");
            string[] expectedRecipes = CanonicalSet(
                expectedEwuRecipeIds,
                "expected EWU recipe");
            string[] expectedExternalSeeds = CanonicalSet(
                expectedExternalSeedItemIds,
                "expected external EWU seed");
            V23ExternalSeedItemIds = CanonicalSet(
                v23ExternalSeedItemIds,
                "V23 external EWU seed");
            V27ExternalSeedItemIds = CanonicalSet(
                v27ExternalSeedItemIds,
                "V27 external EWU seed");
            LedgerItemIds = CanonicalSet(ledgerItemIds, "ledger item");

            // ResourceEconomyContentCatalog intentionally exposes only resource
            // definitions. Other ItemDefinitionSO families remain valid physical
            // catalog members and are not silently required to join that projection.
            RequireExact(expectedEconomyItems, EconomyItemIds,
                "resource-item/economy projection");
            RequireExact(CatalogRecipeIds, EconomyRecipeIds,
                "production recipe catalog/economy projection");
            RequireSubset(V23ItemIds, CatalogItemIds,
                "V23 EWU item/catalog projection");
            RequireSubset(V27ItemIds, CatalogItemIds,
                "V27 EWU item/catalog projection");
            RequireExact(
                CatalogItemIds.Where(value => V23ItemIds.Contains(
                        value,
                        StringComparer.Ordinal)
                    && V27ItemIds.Contains(value, StringComparer.Ordinal)),
                LedgerItemIds,
                "V23/V27 EWU intersection/ledger projection");
            RequireExact(expectedRecipes, V23RecipeIds,
                "production recipe/V23 EWU projection");
            RequireExact(expectedRecipes, V27RecipeIds,
                "production recipe/V27 EWU projection");
            RequireExact(expectedExternalSeeds, V23ExternalSeedItemIds,
                "recipe-boundary/V23 external seed projection");
            RequireExact(expectedExternalSeeds, V27ExternalSeedItemIds,
                "recipe-boundary/V27 external seed projection");
        }

        internal IReadOnlyList<string> CatalogItemIds { get; }
        internal IReadOnlyList<string> CatalogRecipeIds { get; }
        internal IReadOnlyList<string> EconomyItemIds { get; }
        internal IReadOnlyList<string> EconomyRecipeIds { get; }
        internal IReadOnlyList<string> V23ItemIds { get; }
        internal IReadOnlyList<string> V23RecipeIds { get; }
        internal IReadOnlyList<string> V27ItemIds { get; }
        internal IReadOnlyList<string> V27RecipeIds { get; }
        internal IReadOnlyList<string> V23ExternalSeedItemIds { get; }
        internal IReadOnlyList<string> V27ExternalSeedItemIds { get; }
        internal IReadOnlyList<string> LedgerItemIds { get; }

        internal void RequireExactAugmentationOf(
            EconomyDenominatorSnapshot baseline,
            string addedItemId,
            string addedRecipeId)
        {
            if (baseline == null)
                throw new ArgumentNullException(nameof(baseline));
            RequireSingleAddition(
                baseline.CatalogItemIds, CatalogItemIds, addedItemId,
                "catalog item");
            RequireSingleAddition(
                baseline.EconomyItemIds, EconomyItemIds, addedItemId,
                "economy item");
            RequireSingleAddition(
                baseline.V23ItemIds, V23ItemIds, addedItemId,
                "V23 EWU item");
            RequireSingleAddition(
                baseline.V27ItemIds, V27ItemIds, addedItemId,
                "V27 EWU item");
            RequireSingleAddition(
                baseline.LedgerItemIds, LedgerItemIds, addedItemId,
                "ledger item");
            RequireSingleAddition(
                baseline.CatalogRecipeIds, CatalogRecipeIds, addedRecipeId,
                "catalog recipe");
            RequireSingleAddition(
                baseline.EconomyRecipeIds, EconomyRecipeIds, addedRecipeId,
                "economy recipe");
            RequireSingleAddition(
                baseline.V23RecipeIds, V23RecipeIds, addedRecipeId,
                "V23 EWU recipe");
            RequireSingleAddition(
                baseline.V27RecipeIds, V27RecipeIds, addedRecipeId,
                "V27 EWU recipe");
            RequireExact(
                baseline.V23ExternalSeedItemIds,
                V23ExternalSeedItemIds,
                "V23 external EWU seed augmentation");
            RequireExact(
                baseline.V27ExternalSeedItemIds,
                V27ExternalSeedItemIds,
                "V27 external EWU seed augmentation");
        }

        internal void RequireExactIdentity(EconomyDenominatorSnapshot expected)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));
            RequireExact(expected.CatalogItemIds, CatalogItemIds, "catalog item cleanup");
            RequireExact(expected.CatalogRecipeIds, CatalogRecipeIds, "catalog recipe cleanup");
            RequireExact(expected.EconomyItemIds, EconomyItemIds, "economy item cleanup");
            RequireExact(expected.EconomyRecipeIds, EconomyRecipeIds, "economy recipe cleanup");
            RequireExact(expected.V23ItemIds, V23ItemIds, "V23 EWU item cleanup");
            RequireExact(expected.V23RecipeIds, V23RecipeIds, "V23 EWU recipe cleanup");
            RequireExact(expected.V27ItemIds, V27ItemIds, "V27 EWU item cleanup");
            RequireExact(expected.V27RecipeIds, V27RecipeIds, "V27 EWU recipe cleanup");
            RequireExact(expected.V23ExternalSeedItemIds, V23ExternalSeedItemIds,
                "V23 external EWU seed cleanup");
            RequireExact(expected.V27ExternalSeedItemIds, V27ExternalSeedItemIds,
                "V27 external EWU seed cleanup");
            RequireExact(expected.LedgerItemIds, LedgerItemIds, "ledger item cleanup");
        }

        internal void RequireAbsent(string itemId, string recipeId)
        {
            if (CatalogItemIds.Contains(itemId, StringComparer.Ordinal)
                || EconomyItemIds.Contains(itemId, StringComparer.Ordinal)
                || V23ItemIds.Contains(itemId, StringComparer.Ordinal)
                || V27ItemIds.Contains(itemId, StringComparer.Ordinal)
                || V23ExternalSeedItemIds.Contains(itemId, StringComparer.Ordinal)
                || V27ExternalSeedItemIds.Contains(itemId, StringComparer.Ordinal)
                || LedgerItemIds.Contains(itemId, StringComparer.Ordinal)
                || CatalogRecipeIds.Contains(recipeId, StringComparer.Ordinal)
                || EconomyRecipeIds.Contains(recipeId, StringComparer.Ordinal)
                || V23RecipeIds.Contains(recipeId, StringComparer.Ordinal)
                || V27RecipeIds.Contains(recipeId, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "A removed synthetic definition remains in an economy denominator.");
            }
        }

        private static string[] CanonicalSet(
            IEnumerable<string> values,
            string label)
        {
            string[] ordered = (values ?? throw new ArgumentNullException(nameof(values)))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (ordered.Any(value => string.IsNullOrWhiteSpace(value)
                    || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                || ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            {
                throw new InvalidOperationException(
                    $"The {label} denominator is empty, non-canonical, or duplicated.");
            }
            return ordered;
        }

        private static void RequireSingleAddition(
            IReadOnlyList<string> baseline,
            IReadOnlyList<string> augmented,
            string expectedId,
            string label)
        {
            string[] expected = baseline
                .Concat(new[] { expectedId })
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            RequireExact(expected, augmented, label + " augmentation");
        }

        private static void RequireSubset(
            IEnumerable<string> subset,
            IEnumerable<string> superset,
            string label)
        {
            HashSet<string> allowed = new(superset, StringComparer.Ordinal);
            string[] outside = subset
                .Where(value => !allowed.Contains(value))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (outside.Length != 0)
            {
                throw new InvalidOperationException(
                    $"The {label} contains IDs outside its authoritative catalog: "
                    + string.Join(",", outside));
            }
        }

        private static void RequireExact(
            IEnumerable<string> expected,
            IEnumerable<string> actual,
            string label)
        {
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The {label} denominator is not an exact stable-ID bijection.");
            }
        }
    }

    private static CaptureResult Capture()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        ItemDefinitionCatalogSO itemCatalog =
            root.GetItemDefinitions<ItemDefinitionCatalogSO>()
            ?? throw new InvalidOperationException("Item definition catalog is missing.");
        GameDomainContentCatalogSO domain = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .Single();

        ItemDefinitionSO[] catalogItems = itemCatalog.Definitions
            .Where(value => value != null)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ThenBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, ItemDefinitionSO> catalogById = BuildUniqueIndex(
            catalogItems,
            value => value.ItemId,
            "item catalog");
        ProductionRecipeSO[] recipes = UniqueDefinitions(
            domain.GetAll<ProductionRecipeSO>(),
            value => value.RecipeId,
            "recipe");
        CropDefinitionSO[] crops = UniqueDefinitions(
            domain.GetAll<CropDefinitionSO>(),
            value => value.CropId,
            "crop");
        CombatEquipmentDefinitionSO[] equipment = UniqueDefinitions(
            domain.GetAll<CombatEquipmentDefinitionSO>(),
            value => value.EquipmentId,
            "equipment");
        ApparelDefinitionSO[] apparel = UniqueDefinitions(
            domain.GetAll<ApparelDefinitionSO>(),
            value => value.ApparelId,
            "apparel");
        TextileMaterialDefinitionSO[] textiles = UniqueDefinitions(
            domain.GetAll<TextileMaterialDefinitionSO>(),
            value => value.MaterialId,
            "textile");
        CraftMaterialDefinitionSO[] materials = UniqueDefinitions(
            domain.GetAll<CraftMaterialDefinitionSO>(),
            value => value.MaterialId,
            "craft material");

        MassContentSource content = new MassContentSource(domain, itemCatalog);
        ResourceMaterialEconomicProfileCatalog materialProfiles = new(content);
        V23BalanceWorkCalculator work = new V23BalanceWorkCalculator(materialProfiles);
        EmbeddedWorkValueSnapshot before = new V23EmbeddedWorkValueCalculator(
            recipes,
            catalogItems,
            equipment,
            materials,
            work).Calculate();
        V27EmbeddedWorkValueSnapshot after = new V27EmbeddedWorkValueCalculator(
            recipes,
            crops,
            catalogItems,
            equipment,
            materials,
            before,
            work,
            materialProfiles,
            2.25m,
            V27BalanceAssetApplication.CaptureHistoricalBeforeValues()).Calculate();
        string[] ledgerItemIds = catalogItems
            .Where(value =>
                after.Items.ContainsKey(value.ItemId)
                && before.TryGetItemWork(value.ItemId, out _))
            .Select(value => value.ItemId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> ledgerItems = new HashSet<string>(
            ledgerItemIds,
            StringComparer.Ordinal);
        SemanticCandidateRow[] semanticCandidates = ledgerItemIds
            .Select(itemId => BuildSemanticCandidate(catalogById[itemId]))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();

        ItemDefinitionSO[] sites = AssetDatabase
            .FindAssets("t:ItemDefinitionSO", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path))
            .Where(value => value != null)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ThenBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
            .ToArray();

        string[] sitePaths = sites
            .Select(AssetDatabase.GetAssetPath)
            .Select(CanonicalPath)
            .ToArray();
        string beforeAssetDigest = ComputeAggregateDigest(projectRoot, sitePaths);

        List<MassAuthoritySiteRow> rows = new List<MassAuthoritySiteRow>(sites.Length);
        List<string> failures = new List<string>();
        foreach (ItemDefinitionSO site in sites)
        {
            string path = CanonicalPath(AssetDatabase.GetAssetPath(site));
            SerializedObject serialized = new SerializedObject(site);
            SerializedProperty weight = serialized.FindProperty("unitWeight");
            if (weight == null)
            {
                failures.Add($"Missing unitWeight property: {path}.");
                continue;
            }
            string itemId = site.ItemId;
            if (string.IsNullOrWhiteSpace(itemId))
                failures.Add($"Missing item ID: {path}.");
            bool catalogMember = catalogById.TryGetValue(itemId, out ItemDefinitionSO authority);
            if (!catalogMember)
                failures.Add($"Weight site is absent from the item catalog: {itemId}@{path}.");
            float kilograms = weight.floatValue;
            if (!float.IsFinite(kilograms) || kilograms <= 0f)
                failures.Add($"Invalid unitWeight: {itemId}@{path}={kilograms:R}.");
            long grams = 0L;
            bool canonicalGram = true;
            try
            {
                grams = PhysicalMassGrams
                    .FromCanonicalKilograms(kilograms)
                    .Value;
            }
            catch (Exception)
            {
                canonicalGram = false;
                failures.Add(
                    $"NON_CANONICAL_ITEM_MASS: {itemId}@{path}={kilograms:R}kg.");
            }
            rows.Add(new MassAuthoritySiteRow(
                itemId,
                site.GetType().Name,
                path,
                "unitWeight",
                kilograms.ToString("R", CultureInfo.InvariantCulture),
                grams.ToString(CultureInfo.InvariantCulture),
                site.MaxStack.ToString(CultureInfo.InvariantCulture),
                site.StockCategory.ToString(),
                ledgerItems.Contains(itemId) ? "true" : "false",
                catalogMember ? "true" : "false",
                authority == site ? "catalog-authority" : "non-authority-shadow",
                authority != null
                    ? CanonicalPath(AssetDatabase.GetAssetPath(authority))
                    : string.Empty,
                canonicalGram ? "canonical-gram" : "non-canonical-gram",
                ledgerItems.Contains(itemId) ? "missing" : "out-of-ledger-scope",
                AssetDatabase.AssetPathToGUID(path)));
        }

        int equipmentMappingFailures = ValidateEquipmentMappings(
            equipment,
            catalogById,
            failures);
        int apparelMappingFailures = ValidateApparelMappings(
            apparel,
            catalogById,
            failures);
        int textileMappingFailures = ValidateTextileMappings(
            textiles,
            catalogById,
            failures);
        ValidateSchemaContract();

        WriterInventory writerInventory = CaptureWriterInventory(projectRoot, failures);
        string afterAssetDigest = ComputeAggregateDigest(projectRoot, sitePaths);
        if (!string.Equals(beforeAssetDigest, afterAssetDigest, StringComparison.Ordinal))
            failures.Add("Asset bytes changed during AuditOnly mass inventory capture.");

        Require(ledgerItemIds.Length > 0,
            "The live V27 ledger item scope is empty.");
        Require(sites.Length >= ledgerItemIds.Length,
            "Serialized mass authority sites cannot cover the live ledger scope.");
        Require(recipes.Length > 0,
            "The live production recipe scope is empty.");
        Require(equipment.Length > 0,
            "The live combat-equipment scope is empty.");
        Require(ledgerItemIds.All(catalogById.ContainsKey),
            "At least one ledger item identity is absent from the live item catalog.");
        Require(catalogById.Keys.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    catalogItems.Select(value => value.ItemId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal),
            "The live item catalog ID set is not an exact bijection.");
        Require(failures.Count == 0,
            "V27 physical-mass authority inventory failed:\n" + string.Join("\n", failures));

        byte[] csv = BuildCsv(rows);
        string sourceDigest = ComputeAggregateDigest(
            projectRoot,
            sitePaths
                .Concat(writerInventory.Paths)
                .Append(SelfPath)
                .Append(SchemaPath)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
        byte[] report = Utf8(
            BuildReport(
                rows,
                ledgerItemIds.Length,
                catalogItems.Length,
                recipes.Length,
                equipment.Length,
                apparel.Length,
                textiles.Length,
                materials.Length,
                equipmentMappingFailures,
                apparelMappingFailures,
                textileMappingFailures,
                writerInventory.Paths.Length,
                semanticCandidates.Count(value => value.HighConfidence),
                sourceDigest,
                beforeAssetDigest));
        byte[] writerManifest = Utf8(BuildWriterManifest(writerInventory, sourceDigest));
        byte[] semanticCandidateCsv = BuildSemanticCandidateCsv(semanticCandidates);
        return new CaptureResult(
            csv,
            report,
            writerManifest,
            semanticCandidateCsv,
            ledgerItemIds.Length,
            sites.Length,
            recipes.Length,
            equipment.Length);
    }

    private static int ValidateEquipmentMappings(
        IReadOnlyList<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        ICollection<string> failures)
    {
        int count = 0;
        foreach (CombatEquipmentDefinitionSO definition in equipment)
        {
            if (!items.TryGetValue(definition.ItemId, out ItemDefinitionSO item)
                || !item.TryGetFeature(out EquipmentItemFeature feature)
                || !string.Equals(
                    feature.equipmentDefinitionId,
                    definition.EquipmentId,
                    StringComparison.Ordinal))
            {
                count++;
                failures.Add(
                    $"Equipment/item mapping drift: {definition.EquipmentId}->{definition.ItemId}.");
                continue;
            }
            if (MassGrams(item.UnitWeight) != MassGrams(definition.Weight))
            {
                count++;
                failures.Add(
                    $"Equipment/item mass drift: {definition.EquipmentId}; "
                    + $"equipment={definition.Weight:R}; item={item.UnitWeight:R}.");
            }
        }
        return count;
    }

    private static int ValidateApparelMappings(
        IReadOnlyList<ApparelDefinitionSO> apparel,
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        ICollection<string> failures)
    {
        int count = 0;
        foreach (ApparelDefinitionSO definition in apparel)
        {
            if (string.IsNullOrWhiteSpace(definition.PhysicalItemId)
                || !items.ContainsKey(definition.PhysicalItemId))
            {
                count++;
                failures.Add(
                    $"Apparel physical item mapping drift: "
                    + $"{definition.ApparelId}->{definition.PhysicalItemId}.");
            }
        }
        return count;
    }

    private static int ValidateTextileMappings(
        IReadOnlyList<TextileMaterialDefinitionSO> textiles,
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        ICollection<string> failures)
    {
        int count = 0;
        foreach (TextileMaterialDefinitionSO definition in textiles)
        {
            if (string.IsNullOrWhiteSpace(definition.PhysicalItemId)
                || !items.ContainsKey(definition.PhysicalItemId))
            {
                count++;
                failures.Add(
                    $"Textile physical item mapping drift: "
                    + $"{definition.MaterialId}->{definition.PhysicalItemId}.");
            }
        }
        return count;
    }

    private static WriterInventory CaptureWriterInventory(
        string projectRoot,
        ICollection<string> failures)
    {
        V27PhysicalMassWriterProvenanceSnapshot snapshot =
            V27PhysicalMassWriterProvenanceRegistry.Capture(
                projectRoot,
                SelfPath);
        foreach (string value in snapshot.Unknown)
            failures.Add("Unknown physical-mass writer provenance: " + value + ".");
        foreach (string value in snapshot.DuplicatePaths)
            failures.Add("Duplicate physical-mass writer path: " + value + ".");
        if (snapshot.DeclaredCount != snapshot.DiscoveredCount)
        {
            failures.Add(
                "Physical-mass writer declaration/discovery mismatch: declared="
                + snapshot.DeclaredCount + "; discovered="
                + snapshot.DiscoveredCount + ".");
        }
        if (snapshot.DeclaredNotDiscoveredCount != 0)
        {
            failures.Add(
                "Physical-mass writer registry retained stale declarations: "
                + snapshot.DeclaredNotDiscoveredCount + ".");
        }
        string[] paths = snapshot.Rows
            .Select(value => value.Path)
            .ToArray();
        WriterRow[] rows = snapshot.Rows
            .Select(value => new WriterRow(
                value.Path,
                value.Role,
                value.EvidenceShape,
                value.WriteSiteCount,
                value.Digest))
            .ToArray();
        return new WriterInventory(paths, rows);
    }

    private static byte[] BuildCsv(IReadOnlyList<MassAuthoritySiteRow> rows)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 16384);
        string[] header =
        {
            "schemaVersion", "itemId", "definitionType", "assetPath", "propertyPath",
            "authoredKilograms", "canonicalUnitMassGrams", "maxStack", "stockCategory",
            "inV27Ledger", "inItemCatalog", "siteRole", "catalogAuthorityPath",
            "gramStatus", "unitSemanticStatus", "assetGuid"
        };
        WriteCsvRow(writer, header);
        foreach (MassAuthoritySiteRow row in rows)
            WriteCsvRow(writer, row.Fields);
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteCsvRow(V27Utf8CsvWriter writer, IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index != 0)
                writer.WriteAscii(',');
            writer.WriteEscapedField((fields[index] ?? string.Empty).AsSpan());
        }
        writer.WriteCrLf();
    }

    private static string BuildReport(
        IReadOnlyList<MassAuthoritySiteRow> rows,
        int ledgerItems,
        int catalogItems,
        int recipes,
        int equipment,
        int apparel,
        int textiles,
        int materials,
        int equipmentMappingFailures,
        int apparelMappingFailures,
        int textileMappingFailures,
        int writerFiles,
        int highConfidenceSemanticCandidates,
        string sourceDigest,
        string assetDigest)
    {
        int ledgerSites = rows.Count(row => row.InLedger);
        int nonLedgerSites = rows.Count - ledgerSites;
        int semanticMissing = rows
            .Where(row => row.InLedger)
            .Select(row => row.ItemId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        StringBuilder report = new StringBuilder(1024);
        report.Append("RESULT=PASS; phase=authority-inventory; assetMutations=0\n")
            .Append("ledgerCanonicalItems=").Append(ledgerItems)
            .Append("; serializedWeightSites=").Append(rows.Count)
            .Append("; catalogDefinitions=").Append(catalogItems)
            .Append("; ledgerSites=").Append(ledgerSites)
            .Append("; nonLedgerSites=").Append(nonLedgerSites).Append('\n')
            .Append("recipes=").Append(recipes)
            .Append("; equipment=").Append(equipment)
            .Append("; apparel=").Append(apparel)
            .Append("; textiles=").Append(textiles)
            .Append("; craftMaterials=").Append(materials).Append('\n')
            .Append("equipmentMappingFailures=").Append(equipmentMappingFailures)
            .Append("; apparelMappingFailures=").Append(apparelMappingFailures)
            .Append("; textileMappingFailures=").Append(textileMappingFailures)
            .Append("; unknownWeightWriters=0\n")
            .Append("writerFiles=").Append(writerFiles)
            .Append("; schemaContract=PASS; deterministicRecapture=PASS; byteIdentical=true\n")
            .Append("unitSemanticCandidates=").Append(ledgerItems)
            .Append("; highConfidenceCandidates=").Append(highConfidenceSemanticCandidates)
            .Append("; requiresReview=").Append(ledgerItems - highConfidenceSemanticCandidates)
            .Append("; assignmentsAuthoritative=0\n")
            .Append("unitSemanticAssigned=0/").Append(ledgerItems)
            .Append("; nextGate=MISSING_ITEM_UNIT_SEMANTIC; status=IN_PROGRESS\n")
            .Append("sourceDigest=").Append(sourceDigest).Append('\n')
            .Append("inspectedAssetDigest=").Append(assetDigest).Append('\n')
            .Append("classification=balance-baseline-captured; item kg assets unchanged\n");
        Require(semanticMissing == ledgerItems,
            "Initial unit-semantic inventory did not cover every ledger item exactly once.");
        return report.ToString();
    }

    private static SemanticCandidateRow BuildSemanticCandidate(ItemDefinitionSO item)
    {
        string itemId = item.ItemId;
        if (item.TryGetFeature(out EquipmentItemFeature _))
        {
            ItemUnitSemanticKind kind = itemId.Contains(":shield:", StringComparison.Ordinal)
                ? ItemUnitSemanticKind.Shield
                : itemId.Contains(":armor:", StringComparison.Ordinal)
                    ? ItemUnitSemanticKind.ArmorPiece
                    : ItemUnitSemanticKind.Weapon;
            return Candidate(item, kind, "equipment-feature+stable-subtype", true);
        }
        if (itemId.StartsWith("apparel:", StringComparison.Ordinal))
            return Candidate(item, ItemUnitSemanticKind.ApparelPiece, "apparel-stable-id", true);
        if (item.StockCategory == StockCategory.Ammunition)
            return Candidate(item, ItemUnitSemanticKind.AmmunitionUnitOrPack, "ammunition-stock-category", true);
        if (item.StockCategory == StockCategory.Water
            || string.Equals(itemId, "resource:clean-water", StringComparison.Ordinal))
        {
            return Candidate(item, ItemUnitSemanticKind.LiquidPortion, "water-authority", true);
        }
        if (itemId.StartsWith("food:", StringComparison.Ordinal))
            return Candidate(item, ItemUnitSemanticKind.MealPortion, "food-stable-id", true);
        if (item.TryGetFeature(out MedicineItemFeature _)
            || item.TryGetFeature(out VaccineItemFeature _)
            || item.TryGetFeature(out MedicalProcedureSupplyItemFeature _))
        {
            return Candidate(item, ItemUnitSemanticKind.MedicineDoseOrKit, "medical-feature", true);
        }
        if (itemId.StartsWith("medicine:", StringComparison.Ordinal)
            || itemId.StartsWith("drug:", StringComparison.Ordinal)
            || itemId.StartsWith("sample:", StringComparison.Ordinal))
        {
            return Candidate(item, ItemUnitSemanticKind.MedicineDoseOrKit, "medical-stable-id", true);
        }
        if (itemId.StartsWith("fiber:", StringComparison.Ordinal)
            || itemId.StartsWith("yarn:", StringComparison.Ordinal)
            || itemId.StartsWith("textile:", StringComparison.Ordinal))
        {
            return Candidate(item, ItemUnitSemanticKind.TextileRollOrSheet, "textile-stable-id", true);
        }
        if (itemId.StartsWith("waste:", StringComparison.Ordinal))
            return Candidate(item, ItemUnitSemanticKind.WasteBundle, "waste-stable-id", true);
        if (itemId.StartsWith("record:", StringComparison.Ordinal)
            || itemId.StartsWith("book:", StringComparison.Ordinal))
        {
            return Candidate(item, ItemUnitSemanticKind.BlueprintOrRecord, "record-stable-id", true);
        }
        if (itemId.StartsWith("feed:", StringComparison.Ordinal))
            return Candidate(item, ItemUnitSemanticKind.ProduceBundle, "feed-stable-id", true);
        if (string.Equals(itemId, "material:lumber", StringComparison.Ordinal))
        {
            return Candidate(item, ItemUnitSemanticKind.ProcessedLumberBundle, "exact-lumber-id", true);
        }
        if (itemId.EndsWith("-ingot", StringComparison.Ordinal))
            return Candidate(item, ItemUnitSemanticKind.MetalIngot, "ingot-stable-id", true);

        return Candidate(
            item,
            ItemUnitSemanticKind.OtherExplicitPhysicalUnit,
            "manual-unit-meaning-required",
            false);
    }

    private static SemanticCandidateRow Candidate(
        ItemDefinitionSO item,
        ItemUnitSemanticKind kind,
        string rule,
        bool highConfidence) => new SemanticCandidateRow(
        item.ItemId,
        MassGrams(item.UnitWeight),
        item.MaxStack,
        item.StockCategory.ToString(),
        kind.ToString(),
        rule,
        highConfidence);

    private static byte[] BuildSemanticCandidateCsv(
        IReadOnlyList<SemanticCandidateRow> rows)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 16384);
        WriteCsvRow(writer, new[]
        {
            "schemaVersion", "itemId", "currentUnitMassGrams", "maxStack",
            "stockCategory", "candidateUnitSemanticKind", "candidateRule",
            "candidateConfidence", "assignmentStatus", "reviewReason"
        });
        foreach (SemanticCandidateRow row in rows)
            WriteCsvRow(writer, row.Fields);
        writer.Flush();
        return stream.ToArray();
    }

    private static void ValidateSchemaContract()
    {
        CanonicalItemUnitSemantic valid = new CanonicalItemUnitSemantic(
            "water:test-bottle",
            ItemUnitSemanticKind.LiquidPortion,
            "1 bottle",
            "One sealed one-liter test portion.",
            1000,
            200,
            PackageTareDisposition.ReusableContainerReturn,
            "container:test-bottle-empty",
            "material:water",
            PhysicalMassDerivationKind.VolumeDensity,
            new PhysicalMassGrams(1200),
            PhysicalHaulMassClass.Ordinary,
            "mass:test-water-bottle");
        Require(valid.CanonicalUnitMass.Value == 1200,
            "Canonical item unit semantic did not preserve exact gram mass.");

        MaterialMassProfile material = new MaterialMassProfile(
            "material:test-water",
            1000,
            0,
            1000,
            1000);
        Require(material.DensityGramsPerLiter == 1000,
            "Material mass profile did not preserve exact density.");

        RequireThrows(() => new CanonicalItemUnitSemantic(
                "water:test-invalid",
                ItemUnitSemanticKind.LiquidPortion,
                "1 bottle",
                "Invalid tare test.",
                1000,
                200,
                PackageTareDisposition.None,
                string.Empty,
                "material:water",
                PhysicalMassDerivationKind.VolumeDensity,
                new PhysicalMassGrams(1200),
                PhysicalHaulMassClass.Ordinary,
                "mass:test-invalid"),
            "Positive tare without disposition was accepted.");
        RequireThrows(() => new MaterialMassProfile(
                "material:test-invalid",
                1000,
                0,
                1001,
                1000),
            "Out-of-range packing efficiency was accepted.");
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static string BuildWriterManifest(
        WriterInventory inventory,
        string sourceDigest)
    {
        StringBuilder report = new StringBuilder(2048);
        report.Append("RESULT=PASS; writers=")
            .Append(inventory.Rows.Length)
            .Append("; declared=").Append(inventory.Rows.Length)
            .Append("; discovered=").Append(inventory.Rows.Length)
            .Append("; unknown=0; declaredNotDiscovered=0; duplicatePaths=0")
            .Append("; registryMode=source-derived-no-static-declarations\n")
            .Append("sourceDigest=").Append(sourceDigest).Append('\n');
        foreach (WriterRow row in inventory.Rows)
        {
            report.Append(row.Role).Append('\t')
                .Append(row.Path).Append('\t')
                .Append(row.EvidenceShape).Append('\t')
                .Append(row.WriteSiteCount).Append('\t')
                .Append(row.Digest).Append('\n');
        }
        return report.ToString();
    }

    private static T[] UniqueDefinitions<T>(
        IEnumerable<T> values,
        Func<T, string> id,
        string label)
        where T : ScriptableObject
    {
        T[] result = (values ?? Array.Empty<T>())
            .Where(value => value != null)
            .GroupBy(id, StringComparer.Ordinal)
            .Select(group => group.Single())
            .OrderBy(id, StringComparer.Ordinal)
            .ToArray();
        if (result.Any(value => string.IsNullOrWhiteSpace(id(value))))
            throw new InvalidOperationException($"{label} has a missing stable ID.");
        return result;
    }

    private static Dictionary<string, T> BuildUniqueIndex<T>(
        IEnumerable<T> values,
        Func<T, string> id,
        string label)
    {
        Dictionary<string, T> result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string key = id(value);
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException($"{label} has a missing stable ID.");
            if (!result.TryAdd(key, value))
                throw new InvalidOperationException($"{label} has duplicate ID '{key}'.");
        }
        return result;
    }

    private static long MassGrams(float kilograms)
    {
        if (!float.IsFinite(kilograms) || kilograms <= 0f)
            throw new InvalidOperationException($"Invalid physical mass '{kilograms:R}'.");
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
        return Hex(sha.Hash);
    }

    private static string Hex(byte[] bytes)
    {
        const string Alphabet = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = Alphabet[bytes[index] >> 4];
            result[index * 2 + 1] = Alphabet[bytes[index] & 0x0f];
        }
        return new string(result);
    }

    private static byte[] Utf8(string text) => new UTF8Encoding(false, true).GetBytes(text);

    private static string CanonicalPath(string path) =>
        (path ?? string.Empty).Replace('\\', '/');

    private static string ProjectAbsolute(string root, string projectRelativePath) =>
        Path.Combine(root, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class MassAuthoritySiteRow
    {
        public MassAuthoritySiteRow(
            string itemId,
            string definitionType,
            string assetPath,
            string propertyPath,
            string authoredKilograms,
            string canonicalUnitMassGrams,
            string maxStack,
            string stockCategory,
            string inLedger,
            string inCatalog,
            string siteRole,
            string catalogAuthorityPath,
            string gramStatus,
            string unitSemanticStatus,
            string assetGuid)
        {
            ItemId = itemId ?? string.Empty;
            InLedger = string.Equals(inLedger, "true", StringComparison.Ordinal);
            Fields = new[]
            {
                "v27.mass.authority.1", ItemId, definitionType ?? string.Empty,
                assetPath ?? string.Empty, propertyPath ?? string.Empty,
                authoredKilograms ?? string.Empty, canonicalUnitMassGrams ?? string.Empty,
                maxStack ?? string.Empty, stockCategory ?? string.Empty,
                inLedger ?? string.Empty, inCatalog ?? string.Empty, siteRole ?? string.Empty,
                catalogAuthorityPath ?? string.Empty, gramStatus ?? string.Empty,
                unitSemanticStatus ?? string.Empty, assetGuid ?? string.Empty
            };
        }

        public string ItemId { get; }
        public bool InLedger { get; }
        public IReadOnlyList<string> Fields { get; }
    }

    private readonly struct WriterRow
    {
        public WriterRow(
            string path,
            string role,
            string evidenceShape,
            int writeSiteCount,
            string digest)
        {
            Path = path;
            Role = role;
            EvidenceShape = evidenceShape;
            WriteSiteCount = writeSiteCount;
            Digest = digest;
        }

        public string Path { get; }
        public string Role { get; }
        public string EvidenceShape { get; }
        public int WriteSiteCount { get; }
        public string Digest { get; }
    }

    private sealed class SemanticCandidateRow
    {
        public SemanticCandidateRow(
            string itemId,
            long currentUnitMassGrams,
            int maxStack,
            string stockCategory,
            string candidateKind,
            string candidateRule,
            bool highConfidence)
        {
            ItemId = itemId ?? string.Empty;
            HighConfidence = highConfidence;
            Fields = new[]
            {
                "v27.mass.semantic-candidate.1",
                ItemId,
                currentUnitMassGrams.ToString(CultureInfo.InvariantCulture),
                maxStack.ToString(CultureInfo.InvariantCulture),
                stockCategory ?? string.Empty,
                candidateKind ?? string.Empty,
                candidateRule ?? string.Empty,
                highConfidence ? "high" : "manual-review",
                "candidate-only",
                highConfidence
                    ? "Exact feature/category/stable-ID evidence; explicit assignment still required."
                    : "No safe unit meaning can be inferred without reviewing producer, consumer, and BOM."
            };
        }

        public string ItemId { get; }
        public bool HighConfidence { get; }
        public IReadOnlyList<string> Fields { get; }
    }

    private sealed class WriterInventory
    {
        public WriterInventory(string[] paths, WriterRow[] rows)
        {
            Paths = paths;
            Rows = rows;
        }

        public string[] Paths { get; }
        public WriterRow[] Rows { get; }
    }

    private sealed class MassContentSource : IGameContentDefinitionSource
    {
        private readonly GameDomainContentCatalogSO domain;
        private readonly ItemDefinitionCatalogSO items;

        public MassContentSource(
            GameDomainContentCatalogSO domain,
            ItemDefinitionCatalogSO items)
        {
            this.domain = domain
                ?? throw new ArgumentNullException(nameof(domain));
            this.items = items
                ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            if (typeof(T) == typeof(ItemDefinitionSO))
                return items.Definitions.Cast<T>().ToArray();
            return domain.GetAll<T>();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            return values.Count == 1
                ? values[0]
                : throw new InvalidOperationException(
                    $"Expected one {typeof(T).Name}, found {values.Count}.");
        }
    }

    private sealed class CaptureResult
    {
        public CaptureResult(
            byte[] csv,
            byte[] report,
            byte[] writerManifest,
            byte[] semanticCandidates,
            int ledgerItemCount,
            int siteCount,
            int recipeCount,
            int equipmentCount)
        {
            Csv = csv;
            Report = report;
            WriterManifest = writerManifest;
            SemanticCandidates = semanticCandidates;
            LedgerItemCount = ledgerItemCount;
            SiteCount = siteCount;
            RecipeCount = recipeCount;
            EquipmentCount = equipmentCount;
        }

        public byte[] Csv { get; }
        public byte[] Report { get; }
        public byte[] WriterManifest { get; }
        public byte[] SemanticCandidates { get; }
        public int LedgerItemCount { get; }
        public int SiteCount { get; }
        public int RecipeCount { get; }
        public int EquipmentCount { get; }
    }
}
#endif
