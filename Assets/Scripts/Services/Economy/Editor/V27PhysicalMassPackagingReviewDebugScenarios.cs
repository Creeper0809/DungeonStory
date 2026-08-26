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

public static class V27PhysicalMassPackagingReviewDebugScenarios
{
    public const string CsvPath =
        "Artifacts/QA/v27-physical-mass-packaging-review.csv";
    public const string ReportPath =
        "Artifacts/QA/v27-physical-mass-packaging-review.txt";

    private const int ExpectedLedgerItems = 414;
    private const int ExpectedRecipes = 355;
    private const int ExpectedSemantics = 363;
    private const int ExpectedMissing = 51;
    private const int ExpectedRuntimeConsumerRows = 28;
    private const int ExpectedRuntimeConsumerLinks = 31;
    private static readonly string[] ExpectedPackagingReviewItemIds =
    {
        "craft:fang-poison",
        "craft:resin-balm",
        "craft:ritual-reagent",
        "craft:toxic-trap-coating",
        "drug:blood-stimulant",
        "drug:dreamleaf-analgesic",
        "drug:hallucinogenic-distillate",
        "drug:mana-awakener",
        "drug:moonflower-tea",
        "drug:vitality-tonic",
        "food:expedition-ration-pack",
        "food:preserved-ration",
        "medical:cross-lineage-medium",
        "medical:fertility-treatment",
        "medical:isolation-care-kit",
        "medical:organ-preservation-canister",
        "medical:regenerative-medium",
        "medical:rejuvenation-serum",
        "medical:trait-analysis-kit",
        "medical:trauma-care-kit",
        "medical:whole-body-regeneration-medium",
        "medicine:advanced",
        "medicine:antidote",
        "medicine:antiseptic",
        "medicine:mycelial-culture-pack",
        "medicine:standard",
        "medicine:vaccine:blood-wasting",
        "medicine:vaccine:cave-flu",
        "medicine:vaccine:gut-rot",
        "medicine:vaccine:mana-pox",
        "medicine:vaccine:red-fever",
        "medicine:vaccine:slime-blight",
        "medicine:vaccine:spore-lung",
        "sample:antigen:blood-wasting",
        "sample:antigen:cave-flu",
        "sample:antigen:gut-rot",
        "sample:antigen:mana-pox",
        "sample:antigen:red-fever",
        "sample:antigen:slime-blight",
        "sample:antigen:spore-lung",
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
    };
    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassPackagingReviewDebugScenarios.cs";
    private const string SemanticPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassExplicitSemanticDebugScenarios.cs";
    private const string InventoryPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassAuthorityInventoryDebugScenarios.cs";
    private const string ItemAuthorityPath =
        "Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs";
    private const string RecipeAuthorityPath =
        "Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs";
    private const string ContractPath =
        "Assets/Scripts/Models/Economy/Content/PhysicalMassAuthoringContracts.cs";
    private const string TareRuntimePath =
        "Assets/Scripts/Services/Items/PackagedLotTareDispositionService.cs";
    private const string FacilitySinkGatewayPath =
        "Assets/Scripts/Services/Items/PhysicalFacilityItemSinkGateway.cs";
    private const string SurvivalTareConsumerPath =
        "Assets/Scripts/Services/Survival/CharacterConsumablesApplicationAdapters.cs";
    private const string SurgeryTareConsumerPath =
        "Assets/Scripts/Services/Medical/SurgeryLogisticsRuntime.cs";
    private const string RuntimeConsumerCatalogPath =
        "Assets/Scripts/Models/Items/Core/PhysicalItemRuntimeConsumerCatalog.cs";
    private const string DiseaseFieldResponseRuntimePath =
        "Assets/Scripts/Services/Character/DiseaseFieldResponseRuntime.cs";
    private const string PhysicalVaccinationRuntimePath =
        "Assets/Scripts/Services/Character/PhysicalVaccinationRuntime.cs";
    private const string CharacterMedicalSupplyRuntimePath =
        "Assets/Scripts/Services/Combat/CharacterMedicalSupplyCoordinator.cs";
    private const string OffenseSupplyCatalogPath =
        "Assets/Scripts/Services/Offense/OffenseJourneyModel.cs";
    private const string OffenseSupplyRuntimePath =
        "Assets/Scripts/Services/Offense/OffensePreparationService.cs";
    private const string OffenseUrgentMitigationRuntimePath =
        "Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationRuntime.cs";
    private const string SerializationPath =
        "Assets/Scripts/Services/Economy/Editor/V27BalanceSerialization.cs";

    [MenuItem("DungeonStory/V27/Physical Mass/Capture Remaining Packaging Review")]
    public static void RunFromMenu()
    {
        CaptureResult first = Capture();
        CaptureResult second = Capture();
        Require(first.Csv.SequenceEqual(second.Csv),
            "Packaging review CSV changed between identical captures.");
        Require(first.Report.SequenceEqual(second.Report),
            "Packaging review report changed between identical captures.");
        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
            stream.Write(first.Csv, 0, first.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));
        Debug.Log(
            "V27 physical-mass packaging review captured: "
            + $"missing={first.MissingCount}; authoredPackages="
            + $"{first.AuthoredPackageCount}; status=IN_PROGRESS.");
    }

    private static CaptureResult Capture()
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
        string[] ledgerItemIds = V27PhysicalMassAuthorityInventoryDebugScenarios
            .CaptureCanonicalLedgerItemIds()
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(ledgerItemIds.Length == ExpectedLedgerItems,
            $"Expected {ExpectedLedgerItems} canonical ledger items, "
            + $"found {ledgerItemIds.Length}.");
        Require(ledgerItemIds.Distinct(StringComparer.Ordinal).Count()
                == ledgerItemIds.Length,
            "Canonical ledger item IDs contain duplicates.");
        HashSet<string> ledgerItems = new(
            ledgerItemIds,
            StringComparer.Ordinal);
        Dictionary<string, ItemDefinitionSO> items = UniqueIndex(
            itemCatalog.Definitions.Where(value => value != null
                && ledgerItems.Contains(value.ItemId)),
            value => value.ItemId,
            "item");
        Require(items.Count == ExpectedLedgerItems,
            $"Expected {ExpectedLedgerItems} scoped ledger items, "
            + $"found {items.Count}.");
        Require(ledgerItemIds.All(items.ContainsKey),
            "A canonical ledger item is absent from the live item catalog.");
        HashSet<string> allCatalogItemIds = itemCatalog.Definitions
            .Where(value => value != null)
            .Select(value => value.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string[]> runtimeConsumers =
            CaptureRuntimeConsumerIndex(allCatalogItemIds);
        Dictionary<string, CanonicalItemUnitSemantic> semantics = UniqueIndex(
            V27PhysicalMassExplicitSemanticDebugScenarios
                .CaptureCanonicalUnitSemanticsForAudit(),
            value => value.ItemId,
            "unit semantic");
        Require(semantics.Count == ExpectedSemantics,
            $"Expected {ExpectedSemantics} compiled semantics, "
            + $"found {semantics.Count}.");
        Require(semantics.Keys.All(ledgerItems.Contains),
            "A compiled semantic is outside the canonical 414-item ledger.");
        ProductionRecipeSO[] recipes = domain.GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(recipes.Length == ExpectedRecipes,
            $"Expected {ExpectedRecipes} recipes, found {recipes.Length}.");

        ItemDefinitionSO[] missing = items.Values
            .Where(value => !semantics.ContainsKey(value.ItemId))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        Require(ExpectedPackagingReviewItemIds.Length == ExpectedMissing,
            "Packaging-review identity authority count is stale.");
        Require(missing.Length == ExpectedMissing,
            $"Expected {ExpectedMissing} packaging-review items, "
            + $"found {missing.Length}.");
        Require(missing.Select(value => value.ItemId)
                .SequenceEqual(ExpectedPackagingReviewItemIds),
            "Packaging-review item identity set drifted despite matching row count.");

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string ledgerScopeDigest = ComputeTokenDigest(ledgerItemIds);
        string[] inspectedPaths = items.Values
            .Select(AssetDatabase.GetAssetPath)
            .Concat(recipes.Select(AssetDatabase.GetAssetPath))
            .Append(AssetDatabase.GetAssetPath(itemCatalog))
            .Append(SelfPath)
            .Append(SemanticPath)
            .Append(InventoryPath)
            .Append(ItemAuthorityPath)
            .Append(RecipeAuthorityPath)
            .Append(ContractPath)
            .Append(TareRuntimePath)
            .Append(FacilitySinkGatewayPath)
            .Append(SurvivalTareConsumerPath)
            .Append(SurgeryTareConsumerPath)
            .Append(RuntimeConsumerCatalogPath)
            .Append(DiseaseFieldResponseRuntimePath)
            .Append(PhysicalVaccinationRuntimePath)
            .Append(CharacterMedicalSupplyRuntimePath)
            .Append(OffenseSupplyCatalogPath)
            .Append(OffenseSupplyRuntimePath)
            .Append(OffenseUrgentMitigationRuntimePath)
            .Append(SerializationPath)
            .Select(CanonicalPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string beforeDigest = ComputeAggregateDigest(projectRoot, inspectedPaths);

        PackagingRow[] rows = missing
            .Select(item => CaptureRow(item, recipes, runtimeConsumers))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();

        string afterDigest = ComputeAggregateDigest(projectRoot, inspectedPaths);
        Require(string.Equals(beforeDigest, afterDigest, StringComparison.Ordinal),
            "AuditOnly packaging review mutated an inspected asset.");
        int authoredPackageCount = rows.Count(value => value.HasPackagedLotFeature);
        int reusableCount = rows.Count(value => value.TareDisposition
            == PackageTareDisposition.ReusableContainerReturn.ToString());
        int disposableCount = rows.Count(value => value.TareDisposition
            == PackageTareDisposition.DisposableWasteByproduct.ToString());
        int destroyCount = rows.Count(value => value.TareDisposition
            == PackageTareDisposition.DestroyedDuringUse.ToString());
        int bulkCandidateCount = rows.Count(value => value.ReviewRoute
            == "bulk-infrastructure-candidate");
        int servingCandidateCount = rows.Count(value => value.ReviewRoute
            == "meal-serving-or-ration-package-review");
        int physicalPackageCount = rows.Length
            - bulkCandidateCount
            - servingCandidateCount;
        int sourceInputCount = rows.Count(value => value.ExecutionLifecycle
            == "source-input");
        int transformIntermediateCount = rows.Count(value => value.ExecutionLifecycle
            == "transform-intermediate");
        int terminalOutputCount = rows.Count(value => value.ExecutionLifecycle
            == "terminal-output");
        int executionOrphanCount = rows.Count(value => value.ExecutionLifecycle
            == "execution-orphan");
        int runtimeConsumerRows = rows.Count(value => value.RuntimeConsumerCount > 0);
        int runtimeConsumerLinks = rows.Sum(value => value.RuntimeConsumerCount);
        Require(runtimeConsumerRows == ExpectedRuntimeConsumerRows,
            $"Expected {ExpectedRuntimeConsumerRows} remaining items with runtime "
            + $"consumer owners, found {runtimeConsumerRows}.");
        Require(runtimeConsumerLinks == ExpectedRuntimeConsumerLinks,
            $"Expected {ExpectedRuntimeConsumerLinks} remaining runtime consumer "
            + $"links, found {runtimeConsumerLinks}.");

        byte[] csv = BuildCsv(rows);
        string report =
            "RESULT=IN_PROGRESS; phase=remaining-packaging-review; assetMutations=0\n"
            + $"items={items.Count}; semantics={semantics.Count}; missing={rows.Length}; "
            + $"recipes={recipes.Length}\n"
            + $"authoredPackagedLotFeatures={authoredPackageCount}; "
            + $"reusable={reusableCount}; disposable={disposableCount}; "
            + $"destroyedDuringUse={destroyCount}\n"
            + $"bulkInfrastructureCandidates={bulkCandidateCount}; "
            + $"mealServingOrRationReview={servingCandidateCount}; "
            + $"physicalPackageReview={physicalPackageCount}\n"
            + $"executionLifecycleSourceInput={sourceInputCount}; "
            + $"transformIntermediate={transformIntermediateCount}; "
            + $"terminalOutput={terminalOutputCount}; "
            + $"executionOrphan={executionOrphanCount}\n"
            + $"runtimeConsumerRows={runtimeConsumerRows}; "
            + $"runtimeConsumerLinks={runtimeConsumerLinks}; "
            + "runtimeConsumerCatalogExact=true\n"
            + "automaticTareAssignment=false; semanticAutoApproval=false; "
            + "producerConsumerVectorsRequired=true\n"
            + "runtimeGatewayEvidence=terminal-sink-service+survival-adapter+surgery-logistics; "
            + "gatewayPresenceIsNotPerItemRouteProof=true\n"
            + "deterministicRecapture=PASS; byteIdentical=true\n"
            + $"ledgerScopeDigest={ledgerScopeDigest}\n"
            + $"sourceDigest={beforeDigest}\n"
            + "nextGate=AUTHOR_CONTAINER_RETURN_WASTE_TRANSFER_OR_EXPLICIT_SINK; "
            + "status=IN_PROGRESS\n";
        return new CaptureResult(
            csv,
            Encoding.UTF8.GetBytes(report),
            rows.Length,
            authoredPackageCount);
    }

    private static PackagingRow CaptureRow(
        ItemDefinitionSO item,
        IReadOnlyList<ProductionRecipeSO> recipes,
        IReadOnlyDictionary<string, string[]> runtimeConsumers)
    {
        Require(item != null, "Packaging review item is null.");
        string[] producers = recipes
            .Where(recipe => recipe.Outputs.Any(output => string.Equals(
                output.ItemId,
                item.ItemId,
                StringComparison.Ordinal)))
            .Select(recipe => recipe.RecipeId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] consumers = recipes
            .Where(recipe => recipe.Inputs.Any(input => string.Equals(
                input.ItemId,
                item.ItemId,
                StringComparison.Ordinal)))
            .Select(recipe => recipe.RecipeId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] runtimeConsumerOwners = runtimeConsumers.TryGetValue(
            item.ItemId,
            out string[] owners)
            ? owners
            : Array.Empty<string>();
        bool hasPackage = item.TryGetFeature(out PackagedLotItemFeature package);
        int tareGrams = hasPackage ? package.packageTareGrams : 0;
        PackageTareDisposition disposition = hasPackage
            ? package.tareDisposition
            : PackageTareDisposition.None;
        string containerId = hasPackage
            ? package.containerItemId
            : string.Empty;
        string reviewRoute = ClassifyReviewRoute(item.ItemId);
        string executionLifecycle = ClassifyExecutionLifecycle(
            producers.Length,
            checked(consumers.Length + runtimeConsumerOwners.Length));
        string requiredDispositionProof = RequiredDispositionProof(reviewRoute);
        string runtimeGatewayEvidence = RuntimeGatewayEvidence(reviewRoute);
        string status = hasPackage
            ? executionLifecycle == "execution-orphan"
                ? "authored-package-orphan-requires-live-route"
                : "authored-package-requires-runtime-route-proof"
            : "package-contract-required";
        return new PackagingRow(
            item.ItemId,
            PhysicalMassGrams.FromCanonicalKilograms(item.UnitWeight).Value,
            item.MaxStack,
            item.StockCategory.ToString(),
            hasPackage,
            tareGrams,
            disposition.ToString(),
            containerId,
            producers.Length,
            string.Join("|", producers),
            consumers.Length,
            string.Join("|", consumers),
            runtimeConsumerOwners.Length,
            string.Join("|", runtimeConsumerOwners),
            reviewRoute,
            executionLifecycle,
            requiredDispositionProof,
            runtimeGatewayEvidence,
            status,
            CanonicalPath(AssetDatabase.GetAssetPath(item)));
    }

    private static Dictionary<string, string[]> CaptureRuntimeConsumerIndex(
        ISet<string> allCatalogItemIds)
    {
        Require(allCatalogItemIds != null,
            "Runtime consumer catalog item authority is missing.");
        HashSet<string> pairs = new(StringComparer.Ordinal);
        Dictionary<string, List<string>> ownersByItem = new(StringComparer.Ordinal);
        foreach (PhysicalItemRuntimeConsumerCatalog.Link link in
                 PhysicalItemRuntimeConsumerCatalog.All)
        {
            Require(!string.IsNullOrWhiteSpace(link.ItemId),
                "Runtime physical-item consumer has an empty item ID.");
            Require(string.Equals(link.ItemId, link.ItemId.Trim(),
                    StringComparison.Ordinal),
                $"Runtime consumer item ID is non-canonical: '{link.ItemId}'.");
            Require(!string.IsNullOrWhiteSpace(link.OwnerId)
                    && link.OwnerId.StartsWith("runtime:", StringComparison.Ordinal)
                    && string.Equals(link.OwnerId, link.OwnerId.Trim(),
                        StringComparison.Ordinal),
                $"Runtime consumer owner ID is non-canonical for {link.ItemId}: "
                + $"'{link.OwnerId}'.");
            Require(allCatalogItemIds.Contains(link.ItemId),
                $"Runtime consumer references an unknown physical item: {link.ItemId}.");
            Require(pairs.Add(link.ItemId + "\n" + link.OwnerId),
                $"Duplicate runtime consumer link: {link.ItemId} -> {link.OwnerId}.");
            if (!ownersByItem.TryGetValue(link.ItemId, out List<string> itemOwners))
            {
                itemOwners = new List<string>();
                ownersByItem.Add(link.ItemId, itemOwners);
            }
            itemOwners.Add(link.OwnerId);
        }

        return ownersByItem.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static string ClassifyExecutionLifecycle(int producers, int consumers)
    {
        if (producers == 0 && consumers == 0)
            return "execution-orphan";
        if (producers == 0)
            return "source-input";
        if (consumers == 0)
            return "terminal-output";
        return "transform-intermediate";
    }

    private static string RequiredDispositionProof(string reviewRoute)
    {
        return reviewRoute switch
        {
            "meal-serving-or-ration-package-review" =>
                "terminal-sink-return-waste-or-explicit-disposable-loss",
            "bulk-infrastructure-candidate" =>
                "bulk-infrastructure-exclusion-or-physical-container",
            "specimen-container-review" =>
                "transform-container-transfer-return-or-waste",
            "supply-package-content-role-review" =>
                "transform-content-role-and-package-byproduct",
            "dose-container-return-or-waste-review" =>
                "terminal-sink-return-waste-or-explicit-destroyed-loss",
            "medical-kit-integral-or-returned-package-review" =>
                "integral-kit-transform-or-terminal-return-waste",
            "coating-reagent-container-or-target-transfer-review" =>
                "target-transfer-return-waste-or-explicit-process-loss",
            _ => "manual-source-transfer-transform-sink-proof"
        };
    }

    private static string RuntimeGatewayEvidence(string reviewRoute)
    {
        return reviewRoute switch
        {
            "meal-serving-or-ration-package-review" =>
                "survival-terminal-sink-gateway-present-item-join-unproven",
            "dose-container-return-or-waste-review" =>
                "survival-or-medical-terminal-gateway-present-item-join-unproven",
            "medical-kit-integral-or-returned-package-review" =>
                "surgery-terminal-sink-gateway-present-item-join-unproven",
            _ => "no-specialized-runtime-gateway-proof"
        };
    }

    private static string ClassifyReviewRoute(string itemId)
    {
        if (itemId.StartsWith("food:", StringComparison.Ordinal))
            return "meal-serving-or-ration-package-review";
        if (string.Equals(itemId, "material:alchemical-solvent", StringComparison.Ordinal))
            return "bulk-infrastructure-candidate";
        if (itemId.StartsWith("sample:", StringComparison.Ordinal))
            return "specimen-container-review";
        if (itemId.StartsWith("supply:", StringComparison.Ordinal))
            return "supply-package-content-role-review";
        if (itemId.StartsWith("medicine:", StringComparison.Ordinal)
            || itemId.StartsWith("drug:", StringComparison.Ordinal))
        {
            return "dose-container-return-or-waste-review";
        }
        if (itemId.StartsWith("medical:", StringComparison.Ordinal)
            || itemId.StartsWith("component:temporal-", StringComparison.Ordinal))
        {
            return "medical-kit-integral-or-returned-package-review";
        }
        if (itemId.StartsWith("craft:", StringComparison.Ordinal))
            return "coating-reagent-container-or-target-transfer-review";
        return "manual-package-disposition-review";
    }

    private static byte[] BuildCsv(IEnumerable<PackagingRow> rows)
    {
        using MemoryStream stream = new();
        V27Utf8CsvWriter writer = new(stream, 16384);
        WriteRow(writer, new[]
        {
            "schemaVersion", "itemId", "currentUnitMassGrams", "maxStack",
            "stockCategory", "hasPackagedLotFeature", "packageTareGrams",
            "tareDisposition", "containerItemId", "producerCount", "producerRecipeIds",
            "recipeConsumerCount", "consumerRecipeIds", "runtimeConsumerCount",
            "runtimeConsumerOwnerIds", "totalConsumerCount", "reviewRoute", "status",
            "executionLifecycle", "requiredDispositionProof",
            "runtimeGatewayEvidence", "sourceAuthority"
        });
        foreach (PackagingRow row in rows)
        {
            WriteRow(writer, new[]
            {
                "v27.mass.packaging-review.2",
                row.ItemId,
                Token(row.CurrentUnitMassGrams),
                row.MaxStack.ToString(CultureInfo.InvariantCulture),
                row.StockCategory,
                row.HasPackagedLotFeature ? "true" : "false",
                row.PackageTareGrams.ToString(CultureInfo.InvariantCulture),
                row.TareDisposition,
                row.ContainerItemId,
                row.ProducerCount.ToString(CultureInfo.InvariantCulture),
                row.ProducerRecipeIds,
                row.ConsumerCount.ToString(CultureInfo.InvariantCulture),
                row.ConsumerRecipeIds,
                row.RuntimeConsumerCount.ToString(CultureInfo.InvariantCulture),
                row.RuntimeConsumerOwnerIds,
                checked(row.ConsumerCount + row.RuntimeConsumerCount)
                    .ToString(CultureInfo.InvariantCulture),
                row.ReviewRoute,
                row.Status,
                row.ExecutionLifecycle,
                row.RequiredDispositionProof,
                row.RuntimeGatewayEvidence,
                row.SourceAuthority
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static Dictionary<string, T> UniqueIndex<T>(
        IEnumerable<T> values,
        Func<T, string> key,
        string label)
    {
        Dictionary<string, T> result = new(StringComparer.Ordinal);
        foreach (T value in values)
        {
            string id = key(value);
            if (!result.TryAdd(id, value))
                throw new InvalidOperationException($"Duplicate {label} ID: {id}.");
        }
        return result;
    }

    private static string ComputeAggregateDigest(
        string projectRoot,
        IEnumerable<string> paths)
    {
        using SHA256 sha = SHA256.Create();
        foreach (string path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[] pathBytes = Encoding.UTF8.GetBytes(path);
            sha.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
            string absolute = Path.Combine(
                projectRoot,
                path.Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = File.ReadAllBytes(absolute);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return string.Concat(sha.Hash.Select(value =>
            value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string ComputeTokenDigest(IEnumerable<string> tokens)
    {
        using SHA256 sha = SHA256.Create();
        foreach (string token in tokens.OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            byte[] delimiter = { 0 };
            sha.TransformBlock(delimiter, 0, delimiter.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return string.Concat(sha.Hash.Select(value =>
            value.ToString("x2", CultureInfo.InvariantCulture)));
    }

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

    private sealed class PackagingRow
    {
        public PackagingRow(
            string itemId,
            long currentUnitMassGrams,
            int maxStack,
            string stockCategory,
            bool hasPackagedLotFeature,
            int packageTareGrams,
            string tareDisposition,
            string containerItemId,
            int producerCount,
            string producerRecipeIds,
            int consumerCount,
            string consumerRecipeIds,
            int runtimeConsumerCount,
            string runtimeConsumerOwnerIds,
            string reviewRoute,
            string executionLifecycle,
            string requiredDispositionProof,
            string runtimeGatewayEvidence,
            string status,
            string sourceAuthority)
        {
            ItemId = itemId;
            CurrentUnitMassGrams = currentUnitMassGrams;
            MaxStack = maxStack;
            StockCategory = stockCategory;
            HasPackagedLotFeature = hasPackagedLotFeature;
            PackageTareGrams = packageTareGrams;
            TareDisposition = tareDisposition;
            ContainerItemId = containerItemId;
            ProducerCount = producerCount;
            ProducerRecipeIds = producerRecipeIds;
            ConsumerCount = consumerCount;
            ConsumerRecipeIds = consumerRecipeIds;
            RuntimeConsumerCount = runtimeConsumerCount;
            RuntimeConsumerOwnerIds = runtimeConsumerOwnerIds;
            ReviewRoute = reviewRoute;
            ExecutionLifecycle = executionLifecycle;
            RequiredDispositionProof = requiredDispositionProof;
            RuntimeGatewayEvidence = runtimeGatewayEvidence;
            Status = status;
            SourceAuthority = sourceAuthority;
        }

        public string ItemId { get; }
        public long CurrentUnitMassGrams { get; }
        public int MaxStack { get; }
        public string StockCategory { get; }
        public bool HasPackagedLotFeature { get; }
        public int PackageTareGrams { get; }
        public string TareDisposition { get; }
        public string ContainerItemId { get; }
        public int ProducerCount { get; }
        public string ProducerRecipeIds { get; }
        public int ConsumerCount { get; }
        public string ConsumerRecipeIds { get; }
        public int RuntimeConsumerCount { get; }
        public string RuntimeConsumerOwnerIds { get; }
        public string ReviewRoute { get; }
        public string ExecutionLifecycle { get; }
        public string RequiredDispositionProof { get; }
        public string RuntimeGatewayEvidence { get; }
        public string Status { get; }
        public string SourceAuthority { get; }
    }

    private sealed class CaptureResult
    {
        public CaptureResult(
            byte[] csv,
            byte[] report,
            int missingCount,
            int authoredPackageCount)
        {
            Csv = csv;
            Report = report;
            MissingCount = missingCount;
            AuthoredPackageCount = authoredPackageCount;
        }

        public byte[] Csv { get; }
        public byte[] Report { get; }
        public int MissingCount { get; }
        public int AuthoredPackageCount { get; }
    }
}
#endif
