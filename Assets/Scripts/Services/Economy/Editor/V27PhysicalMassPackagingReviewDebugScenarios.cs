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

    [MenuItem("DungeonStory/V27/Physical Mass/Capture Packaging Contract Review")]
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
            + $"reviewed={first.ReviewedCount}; authoredPackages="
            + $"{first.AuthoredPackageCount}; status=PASS.");
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
        Require(ledgerItemIds.Length > 0,
            "Dynamic canonical ledger scope is empty.");
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
        Require(items.Count == ledgerItemIds.Length,
            "Scoped item definitions are not an exact ledger bijection.");
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
        Require(semantics.Keys.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(ledgerItemIds, StringComparer.Ordinal),
            "Compiled semantics are not an exact canonical-ledger bijection.");
        ProductionRecipeSO[] recipes = domain.GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(recipes.Length > 0
                && recipes.Select(value => value.RecipeId)
                    .Distinct(StringComparer.Ordinal).Count() == recipes.Length,
            "Dynamic recipe scope is empty or contains duplicate IDs.");

        ItemDefinitionSO[] missing = items.Values
            .Where(value => !semantics.ContainsKey(value.ItemId))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        Require(missing.Length == 0,
            "Canonical semantics must cover the full dynamic ledger before coupling: "
            + string.Join(",", missing.Select(value => value.ItemId)) + ".");

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

        CanonicalItemUnitSemantic[] packagingCandidates = semantics.Values
            .Where(value => value.PackagingReviewDisposition
                != PackagingReviewDisposition.Unspecified)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        Require(packagingCandidates.Length == 52,
            "Expected 51 reviewed integral units and one detachable packaged lot, "
            + $"found {packagingCandidates.Length} packaging candidates.");

        PackagingRow[] rows = packagingCandidates
            .Select(semantic => CaptureRow(
                items[semantic.ItemId],
                semantic,
                recipes,
                runtimeConsumers))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();

        string afterDigest = ComputeAggregateDigest(projectRoot, inspectedPaths);
        Require(string.Equals(beforeDigest, afterDigest, StringComparison.Ordinal),
            "AuditOnly packaging review mutated an inspected asset.");
        int authoredPackageCount = rows.Count(value => value.HasPackagedLotFeature);
        int integralCount = rows.Count(value => value.PackagingReviewDisposition
            == PackagingReviewDisposition.IntegralUnitNoDetachableTare.ToString());
        int detachableCount = rows.Count(value => value.PackagingReviewDisposition
            == PackagingReviewDisposition.DetachableTare.ToString());
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
        int unresolvedCount = rows.Count(value => value.Status != "integral-unit-reviewed"
            && value.Status != "detachable-tare-authored");
        Require(integralCount == 51,
            $"Expected 51 integral-unit packaging decisions, found {integralCount}.");
        Require(detachableCount == 1 && authoredPackageCount == 1,
            "The one detachable packaging decision must match one authored packaged lot.");
        Require(unresolvedCount == 0,
            $"Packaging review contains {unresolvedCount} unresolved rows.");
        Require(executionOrphanCount == 0,
            "A packaging-review row has no execution lifecycle.");

        byte[] csv = BuildCsv(rows);
        string report =
            "RESULT=PASS; phase=packaging-contract-review; assetMutations=0\n"
            + $"items={items.Count}; semantics={semantics.Count}; reviewed={rows.Length}; "
            + $"recipes={recipes.Length}\n"
            + $"authoredPackagedLotFeatures={authoredPackageCount}; "
            + $"integralUnitNoDetachableTare={integralCount}; "
            + $"detachableTare={detachableCount}; unresolved={unresolvedCount}; "
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
            + "nextGate=NONE; status=PASS\n";
        return new CaptureResult(
            csv,
            Encoding.UTF8.GetBytes(report),
            rows.Length,
            authoredPackageCount);
    }

    private static PackagingRow CaptureRow(
        ItemDefinitionSO item,
        CanonicalItemUnitSemantic semantic,
        IReadOnlyList<ProductionRecipeSO> recipes,
        IReadOnlyDictionary<string, string[]> runtimeConsumers)
    {
        Require(item != null, "Packaging review item is null.");
        Require(string.Equals(item.ItemId, semantic.ItemId, StringComparison.Ordinal),
            "Packaging semantic was joined to the wrong item authority.");
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
        bool semanticPackageMatches = hasPackage
            && semantic.PackageTareGrams == tareGrams
            && semantic.PackageTareDisposition == disposition
            && string.Equals(
                semantic.PackageContainerItemId,
                containerId,
                StringComparison.Ordinal);
        string status = semantic.PackagingReviewDisposition switch
        {
            PackagingReviewDisposition.IntegralUnitNoDetachableTare
                when !hasPackage
                     && semantic.PackageTareGrams == 0
                     && semantic.PackageTareDisposition == PackageTareDisposition.None
                     && string.IsNullOrEmpty(semantic.PackageContainerItemId) =>
                "integral-unit-reviewed",
            PackagingReviewDisposition.DetachableTare
                when semanticPackageMatches => "detachable-tare-authored",
            _ => "packaging-contract-mismatch"
        };
        return new PackagingRow(
            item.ItemId,
            PhysicalMassGrams.FromCanonicalKilograms(item.UnitWeight).Value,
            item.MaxStack,
            item.StockCategory.ToString(),
            hasPackage,
            tareGrams,
            disposition.ToString(),
            semantic.PackagingReviewDisposition.ToString(),
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
            "tareDisposition", "packagingReviewDisposition", "containerItemId", "producerCount", "producerRecipeIds",
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
                row.PackagingReviewDisposition,
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
            string packagingReviewDisposition,
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
            PackagingReviewDisposition = packagingReviewDisposition;
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
        public string PackagingReviewDisposition { get; }
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
            int reviewedCount,
            int authoredPackageCount)
        {
            Csv = csv;
            Report = report;
            ReviewedCount = reviewedCount;
            AuthoredPackageCount = authoredPackageCount;
        }

        public byte[] Csv { get; }
        public byte[] Report { get; }
        public int ReviewedCount { get; }
        public int AuthoredPackageCount { get; }
    }
}
#endif
