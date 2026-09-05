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

public static class V27ProductionReachabilityAuditOnly
{
    public const string CsvPath =
        "Artifacts/QA/v27-production-reachability-audit.csv";
    public const string ReportPath =
        "Artifacts/QA/v27-production-reachability-audit.txt";

    private const int ExpectedRecipeCount = 355;
    private const int ExpectedItemCount = 364;
    private const int ExpectedResearchCount = 180;
    private const int ExpectedOrphanCount = 0;
    private const int ExpectedProductionSupportCount = 28;
    private const int ExpectedUnreachableSupportCount = 0;
    private const int ExpectedSupportConsumerLinkCount = 50;
    private const int ExpectedSupportTaggedRecipeCount = 40;
    private const string RecipeRoot = "Assets/Resources/SO/Economy/Recipes";
    private const string ItemRoot = "Assets/Resources/SO/Economy";
    private const string ResearchRoot = "Assets/Resources/SO/Research";
    private const string BuildingRoot = "Assets/Resources/SO/Building";
    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/V27ProductionReachabilityAuditOnly.cs";

    private static readonly string[] InspectedScriptPaths =
    {
        SelfPath,
        "Assets/Scripts/Models/Buildings/Core/BuildingAbilityContracts.cs",
        "Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs",
        "Assets/Scripts/Models/FacilityShop/Core/BlueprintUnlock.cs",
        "Assets/Scripts/Models/Research/Core/ResearchProjectContracts.cs",
        "Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs",
        "Assets/Scripts/Services/Buildings/SO/BuildingSO.cs",
        "Assets/Scripts/Services/Economy/Editor/ProductionWorkshopContentAssetBuilder.cs",
        "Assets/Scripts/Services/Economy/Editor/V27BalanceSerialization.cs",
        "Assets/Scripts/Services/Economy/ProductionAssemblyBridgeAdapter.cs",
        "Assets/Scripts/Services/Economy/ProductionInputLogisticsService.cs",
        "Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs",
        "Assets/Scripts/Services/Economy/ProductionWorkshopRuntime.cs",
        "Assets/Scripts/Services/Research/Editor/ResearchProjectAssetBuilder.cs",
        "Assets/Scripts/Services/Research/Editor/ResearchOverhaulContentAssetBuilder.cs",
        "Assets/Scripts/Services/Research/Editor/V27ProductionReachabilityApplyApproved.cs",
        "Assets/Scripts/Services/Research/Editor/V21ResearchConsolidation.cs"
    };

    private static readonly ExpectedOrphan[] ExpectedOrphans =
    {
        new("recipe:component:blast-coat-shell",
            "research:equipment:blast-protection",
            "research:equipment:pressure-barrels"),
        new("recipe:component:engineering-drawing",
            "research:equipment:engineering-drawing",
            "research:industry:powered-tools"),
        new("recipe:component:factory-installation-plan",
            "research:industry:factory-layout",
            "research:industry:powered-tools"),
        new("recipe:component:rune-bus-coupler",
            "research:industry:rune-grid",
            "research:industry:mana-power"),
        new("recipe:component:rune-conductor",
            "research:industry:rune-grid",
            "research:industry:mana-power"),
        new("recipe:component:stock-sensor-panel",
            "research:industry:stock-sensors",
            "research:industry:automatic-bills"),
        new("recipe:material:paper",
            "research:equipment:engineering-drawing",
            "research:industry:powered-tools"),
        new("recipe:supply:botanical-pesticide",
            "research:agriculture:pest-control",
            "research:agriculture:soil-cycles"),
        new("recipe:supply:fungicide",
            "research:agriculture:crop-pathology",
            "research:agriculture:soil-cycles"),
        new("recipe:supply:pest-lure",
            "research:agriculture:pest-control",
            "research:agriculture:soil-cycles"),
        new("recipe:tool:maintenance-kit",
            "research:industry:maintenance",
            "research:industry:breakers")
    };

    private static readonly ExpectedUnreachableSupport[] ExpectedUnreachableSupports =
    {
        new(1607, "production-support:ws08", "support:hearth"),
        new(1609, "production-support:ws10", "support:oven")
    };

    private static readonly string[] ExpectedHearthConsumers =
    {
        "recipe:boar-stew",
        "recipe:egg-pancake",
        "recipe:garden-meal",
        "recipe:grain-porridge",
        "recipe:grape-syrup",
        "recipe:lavish-meat",
        "recipe:lavish-vegan",
        "recipe:malt-porridge",
        "recipe:moonflower-tea",
        "recipe:mushroom-soup",
        "recipe:preserved-vegetable",
        "recipe:roasted-meat",
        "recipe:root-stew",
        "recipe:salted-meat-stew",
        "recipe:stuffed-mushroom"
    };

    private static readonly string[] ExpectedOvenConsumers =
    {
        "recipe:meat-pie",
        "recipe:vegetable-pie"
    };

    [MenuItem("DungeonStory/V27/Production/Capture Reachability AuditOnly")]
    public static void RunFromMenu()
    {
        CaptureResult first = Capture();
        CaptureResult second = Capture();
        Require(first.Csv.SequenceEqual(second.Csv),
            "Production reachability CSV changed between identical captures.");
        Require(first.Report.SequenceEqual(second.Report),
            "Production reachability report changed between identical captures.");
        Require(first.SourceDigest == second.SourceDigest,
            "Production reachability source digest changed between captures.");
        Require(first.ScriptableObjectMutationCount == 0
                && second.ScriptableObjectMutationCount == 0,
            "AuditOnly capture mutated one or more ScriptableObjects.");

        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
            stream.Write(first.Csv, 0, first.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));

        Debug.Log(
            "V27 production reachability AuditOnly captured: "
            + $"recipes={first.RecipeCount}; items={first.ItemCount}; "
            + $"research={first.ResearchCount}; "
            + $"orphans={first.OrphanCount}; supports={first.SupportCount}; "
            + $"unreachable={first.UnreachableSupportCount}; "
            + "gameplayReachabilityGreen=true; soMutation=0.");
    }

    private static CaptureResult Capture()
    {
        AssetEntry<ProductionRecipeSO>[] recipeAssets =
            LoadAssets<ProductionRecipeSO>(RecipeRoot);
        AssetEntry<ResourceItemDefinitionSO>[] itemAssets =
            LoadAssets<ResourceItemDefinitionSO>(ItemRoot);
        AssetEntry<ResearchProjectSO>[] researchAssets =
            LoadAssets<ResearchProjectSO>(ResearchRoot);
        AssetEntry<BuildingSO>[] buildingAssets =
            LoadAssets<BuildingSO>(BuildingRoot);
        UnityEngine.Object[] inspectedObjects = recipeAssets.Select(value => value.Asset)
            .Cast<UnityEngine.Object>()
            .Concat(itemAssets.Select(value => value.Asset))
            .Concat(researchAssets.Select(value => value.Asset))
            .Concat(buildingAssets.Select(value => value.Asset))
            .Distinct()
            .ToArray();
        Dictionary<string, AssetState> initialStates = CaptureAssetStates(inspectedObjects);

        Require(recipeAssets.Length == ExpectedRecipeCount,
            $"Expected {ExpectedRecipeCount} recipes, found {recipeAssets.Length}.");
        Require(itemAssets.Length == ExpectedItemCount,
            $"Expected {ExpectedItemCount} resource items, found {itemAssets.Length}.");
        Require(researchAssets.Length == ExpectedResearchCount,
            $"Expected {ExpectedResearchCount} research projects, "
            + $"found {researchAssets.Length}.");

        RecipeRecord[] recipes = recipeAssets
            .Select(CaptureRecipe)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        ItemRecord[] items = itemAssets
            .Select(CaptureItem)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        ResearchRecord[] research = researchAssets
            .Select(CaptureResearch)
            .OrderBy(value => value.ProjectId, StringComparer.Ordinal)
            .ToArray();
        RequireUnique(recipes.Select(value => value.RecipeId), "recipe");
        RequireUnique(items.Select(value => value.ItemId), "resource item");
        RequireUnique(research.Select(value => value.ProjectId), "research project");

        HashSet<string> researchIds = research
            .Select(value => value.ProjectId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<int> researchBuildingUnlocks = research
            .SelectMany(value => value.BuildingUnlockIds)
            .ToHashSet();

        OrphanRecord[] orphans = recipes
            .Where(value => !researchIds.Contains(value.RequiredResearchId))
            .Select(value => new OrphanRecord(
                value.RecipeId,
                value.RequiredResearchId,
                V21ResearchConsolidation.Normalize(value.RequiredResearchId),
                value.SourcePath))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        ValidateOrphans(orphans, recipes, researchIds);
        ValidateItems(items, researchIds);

        SupportRecord[] supports = buildingAssets
            .SelectMany(CaptureSupports)
            .OrderBy(value => value.SupportId, StringComparer.Ordinal)
            .ToArray();
        Require(supports.Length == ExpectedProductionSupportCount,
            $"Expected {ExpectedProductionSupportCount} production supports, "
            + $"found {supports.Length}.");
        RequireUnique(supports.Select(value => value.SupportId), "production support");
        RequireUnique(supports.Select(value => Token(value.BuildingId)),
            "production support building");
        foreach (SupportRecord support in supports)
        {
            support.Reachable = support.InitiallyUnlocked
                || researchBuildingUnlocks.Contains(support.BuildingId);
        }
        ValidateUnreachableSupports(supports);

        ConsumerRecord[] consumers = recipes
            .SelectMany(recipe => recipe.SupportTags.Select(tag =>
                new ConsumerRecord(recipe.RecipeId, tag, recipe.SourcePath)))
            .OrderBy(value => value.SupportTag, StringComparer.Ordinal)
            .ThenBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        ValidateConsumers(recipes, supports, consumers);

        List<string> sourcePaths = recipeAssets.Select(value => value.Path)
            .Concat(itemAssets.Select(value => value.Path))
            .Concat(researchAssets.Select(value => value.Path))
            .Concat(buildingAssets.Select(value => value.Path))
            .Concat(InspectedScriptPaths)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        string sourceDigest = ComputeAggregateDigest(sourcePaths);
        Dictionary<string, string> authorityDigests = sourcePaths
            .ToDictionary(value => value, ComputeFileDigest, StringComparer.Ordinal);

        List<AuditRow> rows = BuildRows(
            recipes,
            items,
            research,
            orphans,
            supports,
            consumers,
            authorityDigests,
            sourceDigest);
        byte[] csv = BuildCsv(rows);
        string csvDigest = ComputeBytesDigest(csv);
        int mutationCount = CountAssetStateChanges(initialStates, inspectedObjects);
        Require(mutationCount == 0,
            $"AuditOnly capture mutated {mutationCount} ScriptableObject(s).");
        byte[] report = BuildReport(
            recipes,
            items,
            research,
            orphans,
            supports,
            consumers,
            sourceDigest,
            csvDigest,
            sourcePaths.Count,
            mutationCount);
        return new CaptureResult(
            csv,
            report,
            sourceDigest,
            recipes.Length,
            items.Length,
            research.Length,
            orphans.Length,
            supports.Length,
            supports.Count(value => !value.Reachable),
            mutationCount);
    }

    private static RecipeRecord CaptureRecipe(AssetEntry<ProductionRecipeSO> entry)
    {
        SerializedObject serialized = new(entry.Asset);
        string recipeId = RequireId(
            RequiredProperty(serialized, "recipeId", entry.Path).stringValue,
            "recipe",
            entry.Path);
        string researchId = RequireId(
            RequiredProperty(serialized, "requiredResearchId", entry.Path).stringValue,
            $"recipe research reference for {recipeId}",
            entry.Path);
        SerializedProperty supportTagProperty = RequiredProperty(
            serialized,
            "requiredSupportTags",
            entry.Path);
        Require(supportTagProperty.isArray,
            $"Recipe requiredSupportTags is not an array: {entry.Path}.");
        List<string> authoredTags = new(supportTagProperty.arraySize);
        for (int index = 0; index < supportTagProperty.arraySize; index++)
        {
            authoredTags.Add(supportTagProperty
                .GetArrayElementAtIndex(index).stringValue);
        }
        string[] tags = CanonicalIds(
            authoredTags,
            $"required support tag for {recipeId}");
        string batchTag = RequiredProperty(
            serialized,
            "batchSupportTag",
            entry.Path).stringValue;
        if (!string.IsNullOrWhiteSpace(batchTag))
        {
            batchTag = RequireId(batchTag, $"batch support tag for {recipeId}", entry.Path);
            Require(tags.Contains(batchTag, StringComparer.Ordinal),
                $"Recipe {recipeId} batch support tag is absent from required tags: "
                + batchTag + ".");
        }
        return new RecipeRecord(recipeId, researchId, tags, batchTag, entry.Path);
    }

    private static ItemRecord CaptureItem(
        AssetEntry<ResourceItemDefinitionSO> entry)
    {
        string itemId = RequireId(entry.Asset.ItemId, "resource item", entry.Path);
        string researchId = entry.Asset.RequiredResearchId;
        if (!string.IsNullOrEmpty(researchId))
        {
            researchId = RequireId(
                researchId,
                $"item research reference for {itemId}",
                entry.Path);
        }
        return new ItemRecord(itemId, researchId, entry.Path);
    }

    private static ResearchRecord CaptureResearch(AssetEntry<ResearchProjectSO> entry)
    {
        SerializedObject serialized = new(entry.Asset);
        string projectId = RequireId(
            RequiredProperty(serialized, "projectId", entry.Path).stringValue,
            "research project",
            entry.Path);
        SerializedProperty unlockCollection = RequiredProperty(
            serialized,
            "unlocks",
            entry.Path);
        SerializedProperty unlockItems = unlockCollection.FindPropertyRelative("items");
        Require(unlockItems != null && unlockItems.isArray,
            $"Research unlock collection is not serialized as an array: {entry.Path}.");
        List<int> buildingUnlocks = new();
        for (int index = 0; index < unlockItems.arraySize; index++)
        {
            SerializedProperty element = unlockItems.GetArrayElementAtIndex(index);
            if (element.managedReferenceValue is BlueprintBuildingUnlock buildingUnlock)
                buildingUnlocks.Add(buildingUnlock.BuildingId);
        }
        int[] canonicalUnlocks = buildingUnlocks
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        return new ResearchRecord(projectId, canonicalUnlocks, entry.Path);
    }

    private static IEnumerable<SupportRecord> CaptureSupports(
        AssetEntry<BuildingSO> entry)
    {
        SerializedObject serialized = new(entry.Asset);
        SerializedProperty abilityCollection = RequiredProperty(
            serialized,
            BuildingSO.AbilityModulesFieldName,
            entry.Path);
        SerializedProperty abilityItems = abilityCollection.FindPropertyRelative("items");
        Require(abilityItems != null && abilityItems.isArray,
            $"Building ability collection is not serialized as an array: {entry.Path}.");
        List<BuildingProductionSupportAbility> authoredSupports = new();
        for (int index = 0; index < abilityItems.arraySize; index++)
        {
            SerializedProperty element = abilityItems.GetArrayElementAtIndex(index);
            if (element.managedReferenceValue is BuildingProductionSupportAbility support)
                authoredSupports.Add(support);
        }
        BuildingProductionSupportAbility[] abilities = authoredSupports.ToArray();
        if (abilities.Length == 0)
            yield break;
        Require(abilities.Length == 1,
            $"Production support building {entry.Asset.id} has {abilities.Length} "
            + "support abilities; exactly one is required.");
        BuildingProductionSupportAbility ability = abilities[0];
        string supportId = RequireId(
            ability.SupportId,
            $"production support on building {entry.Asset.id}",
            entry.Path);
        string[] features = CanonicalIds(
            ability.featureTags,
            $"feature tag for {supportId}");
        string[] workstationTags = CanonicalIds(
            ability.compatibleWorkstationTags,
            $"compatible workstation tag for {supportId}");
        Require(features.Length == 1,
            $"Production support {supportId} must have exactly one feature tag; "
            + $"found {features.Length}.");
        Require(workstationTags.Length is >= 1 and <= 2,
            $"Production support {supportId} must have one or two compatible "
            + $"workstation tags; found {workstationTags.Length}.");
        Require(ability.outputMultiplier == 1f,
            $"Production support {supportId} output multiplier changed from 1.");
        yield return new SupportRecord(
            entry.Asset.id,
            entry.Asset.ContentDefinitionId,
            supportId,
            features[0],
            workstationTags,
            entry.Asset.unlocked,
            entry.Path);
    }

    private static void ValidateOrphans(
        IReadOnlyList<OrphanRecord> actual,
        IReadOnlyList<RecipeRecord> recipes,
        HashSet<string> researchIds)
    {
        Require(actual.Count == ExpectedOrphanCount,
            $"Expected {ExpectedOrphanCount} orphan recipe research references, "
            + $"found {actual.Count}.");
        Require(actual.Count == 0,
            "Canonical V27 recipe research graph must not contain orphan references.");
        Dictionary<string, RecipeRecord> byId = recipes
            .ToDictionary(value => value.RecipeId, StringComparer.Ordinal);
        foreach (ExpectedOrphan expected in ExpectedOrphans)
        {
            Require(byId.TryGetValue(expected.RecipeId, out RecipeRecord recipe),
                "Canonicalized recipe is absent: " + expected.RecipeId + ".");
            Require(recipe.RequiredResearchId == expected.CanonicalTargetId,
                $"Recipe {expected.RecipeId} must reference canonical research "
                + $"{expected.CanonicalTargetId}; actual={recipe.RequiredResearchId}.");
            Require(researchIds.Contains(expected.CanonicalTargetId),
                $"Canonical recipe research target is absent: "
                + expected.CanonicalTargetId + ".");
        }
    }

    private static void ValidateItems(
        IReadOnlyList<ItemRecord> items,
        HashSet<string> researchIds)
    {
        ItemRecord[] orphans = items
            .Where(value => value.RequiredResearchId.Length > 0
                && !researchIds.Contains(value.RequiredResearchId))
            .ToArray();
        Require(orphans.Length == 0,
            "Resource item research graph contains orphan references: "
            + string.Join(",", orphans.Select(value => value.ItemId)) + ".");

        Dictionary<string, ItemRecord> byId = items
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        foreach (ExpectedOrphan expected in ExpectedOrphans)
        {
            string itemId = expected.RecipeId.Substring("recipe:".Length);
            Require(byId.TryGetValue(itemId, out ItemRecord item),
                "Canonicalized resource item is absent: " + itemId + ".");
            Require(item.RequiredResearchId == expected.CanonicalTargetId,
                $"Item {itemId} must reference canonical research "
                + $"{expected.CanonicalTargetId}; actual={item.RequiredResearchId}.");
        }
    }

    private static void ValidateUnreachableSupports(IReadOnlyList<SupportRecord> supports)
    {
        SupportRecord[] actual = supports.Where(value => !value.Reachable)
            .OrderBy(value => value.BuildingId)
            .ToArray();
        Require(actual.Length == ExpectedUnreachableSupportCount,
            $"Expected {ExpectedUnreachableSupportCount} unreachable supports, "
            + $"found {actual.Length}.");
        foreach (ExpectedUnreachableSupport expected in ExpectedUnreachableSupports)
        {
            SupportRecord match = supports.SingleOrDefault(value =>
                value.BuildingId == expected.BuildingId);
            Require(match != null,
                $"Expected canonical support building is missing: {expected.BuildingId}.");
            Require(match.SupportId == expected.SupportId
                    && match.FeatureTag == expected.FeatureTag,
                $"Canonical support {expected.BuildingId} identity changed.");
            Require(match.Reachable && !match.InitiallyUnlocked,
                $"Canonical support {expected.SupportId} must be research-reachable.");
        }
        Require(actual.Length == 0,
            "Every authored production support must be initially or research reachable.");
    }

    private static void ValidateConsumers(
        IReadOnlyList<RecipeRecord> recipes,
        IReadOnlyList<SupportRecord> supports,
        IReadOnlyList<ConsumerRecord> consumers)
    {
        Require(consumers.Count == ExpectedSupportConsumerLinkCount,
            $"Expected {ExpectedSupportConsumerLinkCount} recipe/support links, "
            + $"found {consumers.Count}.");
        Require(recipes.Count(value => value.SupportTags.Length > 0)
                == ExpectedSupportTaggedRecipeCount,
            $"Expected {ExpectedSupportTaggedRecipeCount} support-tagged recipes.");
        int[] distribution = recipes
            .GroupBy(value => value.SupportTags.Length)
            .OrderBy(value => value.Key)
            .SelectMany(value => new[] { value.Key, value.Count() })
            .ToArray();
        Require(distribution.SequenceEqual(new[] { 0, 315, 1, 34, 2, 4, 4, 2 }),
            "Recipe support-tag cardinality distribution changed.");

        HashSet<string> providedTags = supports
            .Select(value => value.FeatureTag)
            .ToHashSet(StringComparer.Ordinal);
        string[] missingProviders = consumers.Select(value => value.SupportTag)
            .Distinct(StringComparer.Ordinal)
            .Where(value => !providedTags.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(missingProviders.Length == 0,
            "Recipe support tags have no authored provider: "
            + string.Join(",", missingProviders) + ".");

        ValidateExactConsumers(
            consumers,
            "support:hearth",
            ExpectedHearthConsumers);
        ValidateExactConsumers(
            consumers,
            "support:oven",
            ExpectedOvenConsumers);
        Require(supports.Count(value => value.FeatureTag == "support:hearth") == 1,
            "support:hearth must have exactly one authored provider.");
        Require(supports.Single(value => value.FeatureTag == "support:hearth")
                .SupportId == "production-support:ws08",
            "support:hearth provider identity changed.");
        Require(supports.Any(value => value.FeatureTag == "support:oven"
                                      && value.Reachable),
            "support:oven lost every reachable provider.");
    }

    private static void ValidateExactConsumers(
        IEnumerable<ConsumerRecord> consumers,
        string tag,
        IEnumerable<string> expected)
    {
        string[] actual = consumers
            .Where(value => value.SupportTag == tag)
            .Select(value => value.RecipeId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] canonicalExpected = expected
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(actual.SequenceEqual(canonicalExpected),
            $"Exact consumer census changed for {tag}: "
            + string.Join("|", actual) + ".");
    }

    private static List<AuditRow> BuildRows(
        IEnumerable<RecipeRecord> recipes,
        IEnumerable<ItemRecord> items,
        IEnumerable<ResearchRecord> research,
        IEnumerable<OrphanRecord> orphans,
        IEnumerable<SupportRecord> supports,
        IEnumerable<ConsumerRecord> consumers,
        IReadOnlyDictionary<string, string> authorityDigests,
        string sourceDigest)
    {
        List<AuditRow> rows = new();
        rows.AddRange(recipes.Select(value => new AuditRow(
            "recipe",
            value.RecipeId,
            string.Empty,
            value.RequiredResearchId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Join("|", value.SupportTags),
            value.SourcePath,
            authorityDigests[value.SourcePath],
            sourceDigest)));
        rows.AddRange(items.Select(value => new AuditRow(
            "item",
            value.ItemId,
            string.Empty,
            value.RequiredResearchId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            value.SourcePath,
            authorityDigests[value.SourcePath],
            sourceDigest)));
        rows.AddRange(research.Select(value => new AuditRow(
            "research",
            value.ProjectId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            value.SourcePath,
            authorityDigests[value.SourcePath],
            sourceDigest)));
        rows.AddRange(orphans.Select(value => new AuditRow(
            "orphan-research",
            value.RecipeId,
            string.Empty,
            value.AuthoredResearchId,
            value.CanonicalTargetId,
            string.Empty,
            string.Empty,
            "false",
            "V21 absorbed ID remains authored on recipe",
            value.SourcePath,
            authorityDigests[value.SourcePath],
            sourceDigest)));
        rows.AddRange(supports.Select(value => new AuditRow(
            value.Reachable ? "support" : "unreachable-support",
            value.SupportId,
            value.BuildingDefinitionId,
            string.Empty,
            string.Empty,
            Token(value.BuildingId),
            value.FeatureTag,
            value.Reachable ? "true" : "false",
            string.Join("|", value.WorkstationTags),
            value.SourcePath,
            authorityDigests[value.SourcePath],
            sourceDigest)));
        rows.AddRange(consumers.Select(value => new AuditRow(
            "support-consumer",
            value.RecipeId,
            value.SupportTag,
            string.Empty,
            string.Empty,
            string.Empty,
            value.SupportTag,
            string.Empty,
            string.Empty,
            value.SourcePath,
            authorityDigests[value.SourcePath],
            sourceDigest)));
        rows.AddRange(authorityDigests
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new AuditRow(
                "source",
                value.Key,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "included-in-aggregate-source-digest",
                value.Key,
                value.Value,
                sourceDigest)));
        return rows
            .OrderBy(value => value.RecordKind, StringComparer.Ordinal)
            .ThenBy(value => value.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.OwnerId, StringComparer.Ordinal)
            .ThenBy(value => value.FeatureTag, StringComparer.Ordinal)
            .ThenBy(value => value.SourceAuthority, StringComparer.Ordinal)
            .ToList();
    }

    private static byte[] BuildCsv(IEnumerable<AuditRow> rows)
    {
        using MemoryStream stream = new();
        V27Utf8CsvWriter writer = new(stream, 16384);
        WriteRow(writer, new[]
        {
            "schemaVersion", "recordKind", "stableId", "ownerId",
            "authoredReferenceId", "canonicalTargetId", "buildingId",
            "featureTag", "reachable", "detail", "sourceAuthority",
            "authorityDigest", "sourceDigest"
        });
        foreach (AuditRow row in rows)
        {
            WriteRow(writer, new[]
            {
                "v27.production-reachability-audit.2",
                row.RecordKind,
                row.StableId,
                row.OwnerId,
                row.AuthoredReferenceId,
                row.CanonicalTargetId,
                row.BuildingId,
                row.FeatureTag,
                row.Reachable,
                row.Detail,
                row.SourceAuthority,
                row.AuthorityDigest,
                row.SourceDigest
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] BuildReport(
        IReadOnlyList<RecipeRecord> recipes,
        IReadOnlyList<ItemRecord> items,
        IReadOnlyList<ResearchRecord> research,
        IReadOnlyList<OrphanRecord> orphans,
        IReadOnlyList<SupportRecord> supports,
        IReadOnlyList<ConsumerRecord> consumers,
        string sourceDigest,
        string csvDigest,
        int sourceFileCount,
        int mutationCount)
    {
        StringBuilder text = new();
        text.AppendLine("V27 PRODUCTION REACHABILITY AUDITONLY");
        text.AppendLine("auditStatus=PASS");
        text.AppendLine("gameplayReachabilityGreen=true");
        text.AppendLine("recipes=" + Token(recipes.Count));
        text.AppendLine("resourceItems=" + Token(items.Count));
        text.AppendLine("orphanItemResearchReferences=0");
        text.AppendLine("researchProjects=" + Token(research.Count));
        text.AppendLine("orphanRecipeResearchReferences=" + Token(orphans.Count));
        text.AppendLine("productionSupports=" + Token(supports.Count));
        text.AppendLine("unreachableProductionSupports="
                        + Token(supports.Count(value => !value.Reachable)));
        text.AppendLine("supportTaggedRecipes="
                        + Token(recipes.Count(value => value.SupportTags.Length > 0)));
        text.AppendLine("supportConsumerLinks=" + Token(consumers.Count));
        text.AppendLine("hearthConsumers="
                        + Token(consumers.Count(value => value.SupportTag == "support:hearth")));
        text.AppendLine("ovenConsumers="
                        + Token(consumers.Count(value => value.SupportTag == "support:oven")));
        text.AppendLine("sourceFiles=" + Token(sourceFileCount));
        text.AppendLine("deterministicSecondCapture=required-by-menu-entrypoint");
        text.AppendLine("scriptableObjectMutationCount=" + Token(mutationCount));
        text.AppendLine("sourceDigest=" + sourceDigest);
        text.AppendLine("csvSha256=" + csvDigest);
        text.AppendLine();
        text.AppendLine("ORPHAN RECIPE RESEARCH REFERENCES");
        foreach (OrphanRecord orphan in orphans)
        {
            text.AppendLine(orphan.RecipeId + " | " + orphan.AuthoredResearchId
                            + " -> " + orphan.CanonicalTargetId);
        }
        text.AppendLine();
        text.AppendLine("UNREACHABLE PRODUCTION SUPPORTS");
        foreach (SupportRecord support in supports.Where(value => !value.Reachable))
        {
            text.AppendLine(Token(support.BuildingId) + " | " + support.SupportId
                            + " | " + support.FeatureTag);
        }
        text.AppendLine();
        text.AppendLine("SUPPORT CONSUMER COUNTS");
        foreach (IGrouping<string, ConsumerRecord> group in consumers
                     .GroupBy(value => value.SupportTag, StringComparer.Ordinal)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            text.AppendLine(group.Key + "=" + Token(group.Count()));
        }
        return new UTF8Encoding(false, true).GetBytes(
            text.ToString().Replace("\r\n", "\n"));
    }

    private static AssetEntry<T>[] LoadAssets<T>(string root)
        where T : UnityEngine.Object
    {
        string[] paths = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(CanonicalPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(paths.Length > 0, $"No {typeof(T).Name} assets found under {root}.");
        Require(paths.Distinct(StringComparer.Ordinal).Count() == paths.Length,
            $"Duplicate {typeof(T).Name} asset path discovered under {root}.");
        return paths.Select(path =>
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset != null, $"Failed to load {typeof(T).Name}: {path}.");
            return new AssetEntry<T>(path, asset);
        }).ToArray();
    }

    private static Dictionary<string, AssetState> CaptureAssetStates(
        IEnumerable<UnityEngine.Object> assets)
    {
        Dictionary<string, AssetState> states = new(StringComparer.Ordinal);
        foreach (UnityEngine.Object asset in assets)
        {
            string path = CanonicalPath(AssetDatabase.GetAssetPath(asset));
            Require(!string.IsNullOrWhiteSpace(path),
                "Inspected ScriptableObject has no asset path: " + asset.name + ".");
            Require(states.TryAdd(path, new AssetState(
                    EditorJsonUtility.ToJson(asset, false),
                    EditorUtility.IsDirty(asset))),
                "Duplicate inspected ScriptableObject path: " + path + ".");
        }
        return states;
    }

    private static int CountAssetStateChanges(
        IReadOnlyDictionary<string, AssetState> before,
        IEnumerable<UnityEngine.Object> assets)
    {
        int changed = 0;
        foreach (UnityEngine.Object asset in assets)
        {
            string path = CanonicalPath(AssetDatabase.GetAssetPath(asset));
            Require(before.TryGetValue(path, out AssetState state),
                "Asset state baseline is missing: " + path + ".");
            if (!string.Equals(
                    state.Json,
                    EditorJsonUtility.ToJson(asset, false),
                    StringComparison.Ordinal)
                || state.WasDirty != EditorUtility.IsDirty(asset))
            {
                changed++;
            }
        }
        return changed;
    }

    private static string[] CanonicalIds(IEnumerable<string> values, string label)
    {
        string[] raw = (values ?? Array.Empty<string>()).ToArray();
        string[] canonical = raw
            .Select(value => RequireId(value, label, "authoring collection"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(canonical.Distinct(StringComparer.Ordinal).Count() == canonical.Length,
            "Duplicate " + label + ".");
        return canonical;
    }

    private static SerializedProperty RequiredProperty(
        SerializedObject serialized,
        string propertyName,
        string sourcePath)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        Require(property != null,
            $"Serialized property '{propertyName}' is missing: {sourcePath}.");
        return property;
    }

    private static string RequireId(string value, string label, string path)
    {
        string canonical = value?.Trim() ?? string.Empty;
        Require(!string.IsNullOrWhiteSpace(canonical),
            $"Missing {label} ID in {path}.");
        Require(string.Equals(value, canonical, StringComparison.Ordinal),
            $"Non-canonical {label} ID in {path}: '{value}'.");
        return canonical;
    }

    private static void RequireUnique(IEnumerable<string> values, string label)
    {
        string[] captured = values.ToArray();
        Require(captured.Distinct(StringComparer.Ordinal).Count() == captured.Length,
            "Duplicate " + label + " ID detected.");
    }

    private static string ComputeAggregateDigest(IEnumerable<string> paths)
    {
        using SHA256 sha = SHA256.Create();
        foreach (string path in paths
                     .Select(CanonicalPath)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[] pathBytes = Encoding.UTF8.GetBytes(path + "\n");
            sha.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
            byte[] bytes = ReadProjectFile(path);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            byte[] separator = { (byte)'\n' };
            sha.TransformBlock(separator, 0, separator.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(sha.Hash);
    }

    private static string ComputeFileDigest(string projectRelativePath)
    {
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(ReadProjectFile(projectRelativePath)));
    }

    private static string ComputeBytesDigest(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(bytes));
    }

    private static byte[] ReadProjectFile(string projectRelativePath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string canonical = CanonicalPath(projectRelativePath);
        string absolute = Path.Combine(
            root,
            canonical.Replace('/', Path.DirectorySeparatorChar));
        Require(File.Exists(absolute), "Inspected source file is missing: " + canonical + ".");
        return File.ReadAllBytes(absolute);
    }

    private static string Hex(IEnumerable<byte> bytes) => string.Concat(
        bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

    private static string CanonicalPath(string path) =>
        (path ?? string.Empty).Replace('\\', '/');

    private static string Token(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static void WriteRow(V27Utf8CsvWriter writer, IEnumerable<string> fields)
    {
        bool first = true;
        foreach (string field in fields)
        {
            if (!first)
                writer.WriteAscii(',');
            writer.WriteEscapedField((field ?? string.Empty).AsSpan());
            first = false;
        }
        writer.WriteCrLf();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct AssetEntry<T> where T : UnityEngine.Object
    {
        public AssetEntry(string path, T asset)
        {
            Path = path;
            Asset = asset;
        }

        public string Path { get; }
        public T Asset { get; }
    }

    private readonly struct AssetState
    {
        public AssetState(string json, bool wasDirty)
        {
            Json = json;
            WasDirty = wasDirty;
        }

        public string Json { get; }
        public bool WasDirty { get; }
    }

    private readonly struct ExpectedOrphan
    {
        public ExpectedOrphan(
            string recipeId,
            string authoredResearchId,
            string canonicalTargetId)
        {
            RecipeId = recipeId;
            AuthoredResearchId = authoredResearchId;
            CanonicalTargetId = canonicalTargetId;
        }

        public string RecipeId { get; }
        public string AuthoredResearchId { get; }
        public string CanonicalTargetId { get; }
    }

    private readonly struct ExpectedUnreachableSupport
    {
        public ExpectedUnreachableSupport(
            int buildingId,
            string supportId,
            string featureTag)
        {
            BuildingId = buildingId;
            SupportId = supportId;
            FeatureTag = featureTag;
        }

        public int BuildingId { get; }
        public string SupportId { get; }
        public string FeatureTag { get; }
    }

    private sealed class RecipeRecord
    {
        public RecipeRecord(
            string recipeId,
            string requiredResearchId,
            string[] supportTags,
            string batchSupportTag,
            string sourcePath)
        {
            RecipeId = recipeId;
            RequiredResearchId = requiredResearchId;
            SupportTags = supportTags;
            BatchSupportTag = batchSupportTag;
            SourcePath = sourcePath;
        }

        public string RecipeId { get; }
        public string RequiredResearchId { get; }
        public string[] SupportTags { get; }
        public string BatchSupportTag { get; }
        public string SourcePath { get; }
    }

    private sealed class ItemRecord
    {
        public ItemRecord(
            string itemId,
            string requiredResearchId,
            string sourcePath)
        {
            ItemId = itemId;
            RequiredResearchId = requiredResearchId;
            SourcePath = sourcePath;
        }

        public string ItemId { get; }
        public string RequiredResearchId { get; }
        public string SourcePath { get; }
    }

    private sealed class ResearchRecord
    {
        public ResearchRecord(
            string projectId,
            int[] buildingUnlockIds,
            string sourcePath)
        {
            ProjectId = projectId;
            BuildingUnlockIds = buildingUnlockIds;
            SourcePath = sourcePath;
        }

        public string ProjectId { get; }
        public int[] BuildingUnlockIds { get; }
        public string SourcePath { get; }
    }

    private sealed class OrphanRecord
    {
        public OrphanRecord(
            string recipeId,
            string authoredResearchId,
            string canonicalTargetId,
            string sourcePath)
        {
            RecipeId = recipeId;
            AuthoredResearchId = authoredResearchId;
            CanonicalTargetId = canonicalTargetId;
            SourcePath = sourcePath;
        }

        public string RecipeId { get; }
        public string AuthoredResearchId { get; }
        public string CanonicalTargetId { get; }
        public string SourcePath { get; }
    }

    private sealed class SupportRecord
    {
        public SupportRecord(
            int buildingId,
            string buildingDefinitionId,
            string supportId,
            string featureTag,
            string[] workstationTags,
            bool initiallyUnlocked,
            string sourcePath)
        {
            BuildingId = buildingId;
            BuildingDefinitionId = buildingDefinitionId;
            SupportId = supportId;
            FeatureTag = featureTag;
            WorkstationTags = workstationTags;
            InitiallyUnlocked = initiallyUnlocked;
            SourcePath = sourcePath;
        }

        public int BuildingId { get; }
        public string BuildingDefinitionId { get; }
        public string SupportId { get; }
        public string FeatureTag { get; }
        public string[] WorkstationTags { get; }
        public bool InitiallyUnlocked { get; }
        public bool Reachable { get; set; }
        public string SourcePath { get; }
    }

    private readonly struct ConsumerRecord
    {
        public ConsumerRecord(string recipeId, string supportTag, string sourcePath)
        {
            RecipeId = recipeId;
            SupportTag = supportTag;
            SourcePath = sourcePath;
        }

        public string RecipeId { get; }
        public string SupportTag { get; }
        public string SourcePath { get; }
    }

    private sealed class AuditRow
    {
        public AuditRow(
            string recordKind,
            string stableId,
            string ownerId,
            string authoredReferenceId,
            string canonicalTargetId,
            string buildingId,
            string featureTag,
            string reachable,
            string detail,
            string sourceAuthority,
            string authorityDigest,
            string sourceDigest)
        {
            RecordKind = recordKind;
            StableId = stableId;
            OwnerId = ownerId;
            AuthoredReferenceId = authoredReferenceId;
            CanonicalTargetId = canonicalTargetId;
            BuildingId = buildingId;
            FeatureTag = featureTag;
            Reachable = reachable;
            Detail = detail;
            SourceAuthority = sourceAuthority;
            AuthorityDigest = authorityDigest;
            SourceDigest = sourceDigest;
        }

        public string RecordKind { get; }
        public string StableId { get; }
        public string OwnerId { get; }
        public string AuthoredReferenceId { get; }
        public string CanonicalTargetId { get; }
        public string BuildingId { get; }
        public string FeatureTag { get; }
        public string Reachable { get; }
        public string Detail { get; }
        public string SourceAuthority { get; }
        public string AuthorityDigest { get; }
        public string SourceDigest { get; }
    }

    private sealed class CaptureResult
    {
        public CaptureResult(
            byte[] csv,
            byte[] report,
            string sourceDigest,
            int recipeCount,
            int itemCount,
            int researchCount,
            int orphanCount,
            int supportCount,
            int unreachableSupportCount,
            int scriptableObjectMutationCount)
        {
            Csv = csv;
            Report = report;
            SourceDigest = sourceDigest;
            RecipeCount = recipeCount;
            ItemCount = itemCount;
            ResearchCount = researchCount;
            OrphanCount = orphanCount;
            SupportCount = supportCount;
            UnreachableSupportCount = unreachableSupportCount;
            ScriptableObjectMutationCount = scriptableObjectMutationCount;
        }

        public byte[] Csv { get; }
        public byte[] Report { get; }
        public string SourceDigest { get; }
        public int RecipeCount { get; }
        public int ItemCount { get; }
        public int ResearchCount { get; }
        public int OrphanCount { get; }
        public int SupportCount { get; }
        public int UnreachableSupportCount { get; }
        public int ScriptableObjectMutationCount { get; }
    }
}
#endif
