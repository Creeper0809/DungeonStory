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

/// <summary>
/// AuditOnly projection from the existing V27 item ledger and canonical unit-semantic
/// authority into deterministic, reviewable family proposals. This type never mutates
/// ScriptableObjects; applying an accepted proposal belongs to a later asset-application gate.
/// </summary>
public static class V27PhysicalMassFamilyProposalDebugScenarios
{
    private const string Schema = "v27.mass.family-proposal.3";
    public const string CsvPath =
        "Artifacts/QA/v27-physical-mass-family-proposals.csv";
    public const string ReportPath =
        "Artifacts/QA/v27-physical-mass-family-proposals.txt";

    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassFamilyProposalDebugScenarios.cs";
    private const string SemanticAuthorityPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassExplicitSemanticDebugScenarios.cs";
    private const string InventoryAuthorityPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassAuthorityInventoryDebugScenarios.cs";
    private const string ContractPath =
        "Assets/Scripts/Models/Economy/Content/PhysicalMassAuthoringContracts.cs";
    private const string RuntimeConsumerAuthorityPath =
        "Assets/Scripts/Models/Items/Core/PhysicalItemRuntimeConsumerCatalog.cs";
    private const string ItemPrimitivesAuthorityPath =
        "Assets/Scripts/Models/Items/Core/ItemPrimitives.cs";
    private const string PhysicalMassContractsAuthorityPath =
        "Assets/Scripts/Models/Items/Core/PhysicalMassContracts.cs";
    private const string ProductionRecipeAuthorityPath =
        "Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs";
    private const string ProductionPrimitivesAuthorityPath =
        "Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs";
    private const string ItemDefinitionAuthorityPath =
        "Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs";
    private const string RecipeMassInventoryAuthorityPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassRecipeInventoryDebugScenarios.cs";
    private const string CouplingAuthorityPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassCouplingAuditDebugScenarios.cs";

    [MenuItem("DungeonStory/V27/Physical Mass/Capture Family Proposals (AuditOnly)")]
    public static void RunFromMenu()
    {
        VerifyPureClassificationScenario();

        CaptureResult first = Capture();
        CaptureResult second = Capture();
        Require(first.Csv.SequenceEqual(second.Csv),
            "Physical-mass family proposal CSV changed between identical captures.");
        Require(first.Report.SequenceEqual(second.Report),
            "Physical-mass family proposal report changed between identical captures.");
        Require(first.Coupling.Csv.SequenceEqual(second.Coupling.Csv),
            "Physical-mass coupling CSV changed between family captures.");
        Require(first.Coupling.Report.SequenceEqual(second.Coupling.Report),
            "Physical-mass coupling report changed between family captures.");
        Require(string.Equals(
                first.InspectedAssetDigest,
                second.InspectedAssetDigest,
                StringComparison.Ordinal),
            "Physical-mass family proposal source assets changed between captures.");

        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
            stream.Write(first.Csv, 0, first.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(
            V27PhysicalMassCouplingAuditDebugScenarios.CsvPath,
            stream => stream.Write(
                first.Coupling.Csv, 0, first.Coupling.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(
            V27PhysicalMassCouplingAuditDebugScenarios.ReportPath,
            stream => stream.Write(
                first.Coupling.Report, 0, first.Coupling.Report.Length));

        Debug.Log(
            "V27 physical-mass family proposal capture passed: "
            + $"rows={first.Rows.Count}; proposedChanges={first.ProposedChangeCount}; "
            + $"critical={first.CriticalCount}; warning={first.WarningCount}; "
            + "assetMutations=0; deterministicRecapture=PASS; status=IN_PROGRESS.");
    }

    /// <summary>
    /// Read-only integration entrypoint for later anomaly review or approved application.
    /// Callers receive stable-ID-sorted frozen rows; no asset is changed.
    /// </summary>
    internal static IReadOnlyList<V27PhysicalMassFamilyProposalRow>
        CaptureProposalRowsForAudit() => Capture().Rows;

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

        Dictionary<string, ItemDefinitionSO> items = UniqueIndex(
            itemCatalog.Definitions.Where(value => value != null),
            value => value.ItemId,
            "item");
        ProductionRecipeSO[] recipes = domain.GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => CanonicalPath(AssetDatabase.GetAssetPath(value)),
                StringComparer.Ordinal)
            .ToArray();
        Require(recipes.Select(value => value.RecipeId)
                .Distinct(StringComparer.Ordinal).Count() == recipes.Length,
            "Physical-mass family proposal graph contains duplicate recipe IDs.");

        string[] ledgerIds = V27PhysicalMassAuthorityInventoryDebugScenarios
            .CaptureCanonicalLedgerItemIds()
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, CanonicalItemUnitSemantic> semantics = UniqueIndex(
            V27PhysicalMassExplicitSemanticDebugScenarios
                .CaptureCanonicalUnitSemanticsForAudit(),
            value => value.ItemId,
            "canonical unit semantic");
        Require(ledgerIds.All(items.ContainsKey),
            "At least one canonical ledger ID is absent from the item catalog.");
        Require(semantics.Keys.All(ledgerIds.Contains),
            "Canonical unit semantic exists outside the V27 ledger.");

        string[] inspectedAssetPaths = ledgerIds
            .Select(itemId => CanonicalPath(AssetDatabase.GetAssetPath(items[itemId])))
            .Concat(recipes.Select(value =>
                CanonicalPath(AssetDatabase.GetAssetPath(value))))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string beforeAssetDigest = ComputeAggregateDigest(inspectedAssetPaths);
        string sourceDigest = ComputeAggregateDigest(inspectedAssetPaths
            .Append(SelfPath)
            .Append(SemanticAuthorityPath)
            .Append(InventoryAuthorityPath)
            .Append(ContractPath)
            .Append(RuntimeConsumerAuthorityPath)
            .Append(ItemPrimitivesAuthorityPath)
            .Append(PhysicalMassContractsAuthorityPath)
            .Append(ProductionRecipeAuthorityPath)
            .Append(ProductionPrimitivesAuthorityPath)
            .Append(ItemDefinitionAuthorityPath)
            .Append(RecipeMassInventoryAuthorityPath)
            .Append(CouplingAuthorityPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal));

        Dictionary<string, string[]> producersByItem = BuildRelationIndex(
            recipes,
            recipe => recipe.Outputs.Select(output => output.ItemId));
        Dictionary<string, string[]> consumersByItem = BuildRelationIndex(
            recipes,
            recipe => recipe.Inputs.Select(input => input.ItemId));
        RuntimeConsumerCaptureResult runtimeConsumers =
            CaptureRuntimeConsumerIndex(items.Keys, ledgerIds);

        Dictionary<string, long> beforeGramsByItem = ledgerIds.ToDictionary(
            itemId => itemId,
            itemId => PhysicalMassGrams
                .FromCanonicalKilograms(items[itemId].UnitWeight)
                .Value,
            StringComparer.Ordinal);
        Dictionary<string, long> proposedGramsByItem = ledgerIds.ToDictionary(
            itemId => itemId,
            itemId => semantics.TryGetValue(
                    itemId,
                    out CanonicalItemUnitSemantic semantic)
                ? semantic.CanonicalUnitMass.Value
                : beforeGramsByItem[itemId],
            StringComparer.Ordinal);
        Dictionary<string, string> recipeMassImpactByItem =
            BuildRecipeMassImpactIndex(
                recipes,
                beforeGramsByItem,
                proposedGramsByItem);
        IReadOnlyDictionary<string, string> recipeMassStatuses =
            V27PhysicalMassRecipeInventoryDebugScenarios
                .CaptureMassBalanceStatusesForAudit();
        Require(recipeMassStatuses.Keys
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    recipes.Select(value => value.RecipeId),
                    StringComparer.Ordinal),
            "Recipe status and catalog scopes are not an exact bijection.");
        V27PhysicalMassCouplingCapture coupling =
            V27PhysicalMassCouplingAuditDebugScenarios.Capture(
                domain,
                itemCatalog,
                ledgerIds,
                semantics);
        Require(coupling.Summaries.Keys
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(ledgerIds, StringComparer.Ordinal),
            "Mass coupling summary and ledger scopes are not an exact bijection.");

        V27PhysicalMassFamilyProposalRow[] rows = ledgerIds
            .Select(itemId => BuildRow(
                items[itemId],
                semantics.TryGetValue(itemId, out CanonicalItemUnitSemantic semantic),
                semantic,
                GetRelations(producersByItem, itemId),
                MergeRelations(
                    GetRelations(consumersByItem, itemId),
                    GetRelations(runtimeConsumers.Index, itemId)),
                CaptureBlockingRecipeIds(
                    GetRelations(producersByItem, itemId),
                    GetRelations(consumersByItem, itemId),
                    recipeMassStatuses),
                recipeMassImpactByItem.TryGetValue(
                    itemId,
                    out string recipeMassImpact)
                    ? recipeMassImpact
                    : "none",
                coupling.Summaries[itemId],
                sourceDigest))
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();

        Require(rows.Length == ledgerIds.Length,
            "Physical-mass family proposal did not cover every ledger item exactly once.");
        Require(rows.Select(value => value.StableId)
                .Distinct(StringComparer.Ordinal).Count() == rows.Length,
            "Physical-mass family proposal contains duplicate stable IDs.");
        Require(rows.Select(value => value.StableId)
                .SequenceEqual(ledgerIds, StringComparer.Ordinal),
            "Physical-mass family proposal rows are not stable-ID sorted.");

        string afterAssetDigest = ComputeAggregateDigest(inspectedAssetPaths);
        Require(string.Equals(beforeAssetDigest, afterAssetDigest, StringComparison.Ordinal),
            "AuditOnly family proposal capture mutated an inspected asset.");

        byte[] csv = BuildCsv(rows);
        byte[] report = Encoding.UTF8.GetBytes(BuildReport(
            rows,
            recipes.Length,
            semantics.Count,
            runtimeConsumers,
            coupling,
            sourceDigest,
            beforeAssetDigest));
        return new CaptureResult(
            rows, csv, report, beforeAssetDigest, coupling);
    }

    private static V27PhysicalMassFamilyProposalRow BuildRow(
        ItemDefinitionSO item,
        bool hasSemantic,
        CanonicalItemUnitSemantic semantic,
        IReadOnlyList<string> producerIds,
        IReadOnlyList<string> consumerIds,
        IReadOnlyList<string> blockingRecipeIds,
        string recipeMassImpact,
        V27PhysicalMassCouplingSummary coupling,
        string sourceDigest)
    {
        long beforeGrams = PhysicalMassGrams
            .FromCanonicalKilograms(item.UnitWeight)
            .Value;
        long proposedGrams = hasSemantic
            ? semantic.CanonicalUnitMass.Value
            : beforeGrams;
        string family = hasSemantic
            ? semantic.UnitSemanticKind + "/" + semantic.MassDerivationKind
            : "Unresolved/" + item.StockCategory;
        string formula = hasSemantic
            ? BuildFormula(semantic)
            : "hold(before-authored-grams)";
        string reason = hasSemantic
            ? "authority=" + semantic.MassBalanceSourceId
                + "; unit=" + semantic.UnitLabel
                + "; haul=" + semantic.HaulClass
                + "; tareGrams=" + Token(semantic.PackageTareGrams)
                + "; tareDisposition=" + semantic.PackageTareDisposition
            : "missing-canonical-unit-semantic; no automatic mass proposal";
        PhysicalHaulMassClass haulClass = hasSemantic
            ? semantic.HaulClass
            : PhysicalHaulMassClass.Ordinary;
        string unitSemantic = hasSemantic
            ? semantic.UnitSemanticKind + ":" + semantic.UnitLabel
            : "unresolved";
        string tareDisposition = hasSemantic
            ? semantic.PackageTareDisposition
                + ":" + Token(semantic.PackageTareGrams) + "g:"
                + (string.IsNullOrEmpty(semantic.PackageContainerItemId)
                    ? "none"
                    : semantic.PackageContainerItemId)
            : "unresolved";
        long beforeMaxStackMass = checked(beforeGrams * item.MaxStack);
        long proposedMaxStackMass = checked(proposedGrams * item.MaxStack);
        string maxStackMass = "units=" + Token(item.MaxStack)
            + ";before=" + Token(beforeMaxStackMass) + "g"
            + ";proposed=" + Token(proposedMaxStackMass) + "g";
        string warehouseAndBufferImpact = coupling.WarehouseAndBufferImpact;
        HaulBatchProjection haulBatch = BuildHaulBatch(
            proposedGrams,
            item.MaxStack,
            haulClass);
        string anomalyDisposition = ClassifyAnomaly(
            hasSemantic,
            beforeGrams,
            proposedGrams,
            haulClass,
            haulBatch.IsFeasible,
            item.MaxStack,
            blockingRecipeIds.Count == 0);
        if (blockingRecipeIds.Count > 0
            && beforeGrams != proposedGrams)
        {
            reason += "; blockingRecipeMassContracts="
                + string.Join("|", blockingRecipeIds);
        }
        string ewuAndPriceImpact = coupling.EwuAndPriceImpact
            + ";authoredUnitPrice=" + Token(item.UnitPrice)
            + ";massRatioDiagnostic=" + RatioToken(proposedGrams, beforeGrams);

        return new V27PhysicalMassFamilyProposalRow(
            item.ItemId,
            "ItemDefinitionSO",
            unitSemantic,
            family,
            beforeGrams,
            proposedGrams,
            formula,
            string.Join("|", producerIds),
            string.Join("|", consumerIds),
            recipeMassImpact,
            tareDisposition,
            maxStackMass,
            warehouseAndBufferImpact,
            haulBatch.Text,
            ewuAndPriceImpact,
            reason,
            anomalyDisposition,
            sourceDigest);
    }

    private static string BuildFormula(CanonicalItemUnitSemantic semantic) =>
        semantic.MassDerivationKind switch
        {
            PhysicalMassDerivationKind.ExplicitPrimitive =>
                "explicit-primitive(" + semantic.MassBalanceSourceId + ")",
            PhysicalMassDerivationKind.VolumeDensity =>
                "volume-density(" + Token(semantic.NominalVolumeMilliLiters)
                + "mL," + semantic.PrimaryMaterialId + ",tare="
                + Token(semantic.PackageTareGrams) + "g)",
            PhysicalMassDerivationKind.RecipeMassBalance =>
                "recipe-mass-balance(" + semantic.MassBalanceSourceId + ")",
            PhysicalMassDerivationKind.EquipmentShapeAndMaterial =>
                "equipment-shape-material(" + semantic.MassBalanceSourceId + ")",
            PhysicalMassDerivationKind.ApparelShapeAndTextile =>
                "apparel-shape-textile(" + semantic.MassBalanceSourceId + ")",
            PhysicalMassDerivationKind.PackedFacilitySubassembly =>
                "packed-facility-subassembly(" + semantic.MassBalanceSourceId + ")",
            PhysicalMassDerivationKind.WorldSource =>
                "world-source(" + semantic.MassBalanceSourceId + ")",
            PhysicalMassDerivationKind.DerivedByproduct =>
                "derived-byproduct(" + semantic.MassBalanceSourceId + ")",
            _ => throw new InvalidOperationException(
                "Unknown physical-mass derivation kind: "
                + semantic.MassDerivationKind + ".")
        };

    internal static string ClassifyAnomaly(
        bool hasSemantic,
        long beforeGrams,
        long proposedGrams,
        PhysicalHaulMassClass haulClass,
        bool haulBandFeasible,
        int maxStack,
        bool dependencyMassContractsClosed)
    {
        Require(beforeGrams > 0L, "Before physical mass must be positive.");
        Require(proposedGrams > 0L, "Proposed physical mass must be positive.");
        if (!hasSemantic)
            return "local-critical:missing-unit-semantic";
        if (proposedGrams > 20_000L
            && haulClass == PhysicalHaulMassClass.Ordinary)
        {
            return "local-critical:ordinary-unit-over-20kg";
        }
        if (!haulBandFeasible)
        {
            if (maxStack == 1
                && haulClass == PhysicalHaulMassClass.Heavy)
            {
                return "intentional-single-heavy";
            }
            return "local-critical:haul-band-unreachable";
        }
        if (beforeGrams == proposedGrams)
            return "no-change";
        if (!dependencyMassContractsClosed)
            return "proposal-blocked:dependency-mass-contract-open";

        decimal absolutePercent = Math.Abs(
            ((decimal)proposedGrams - beforeGrams) * 100m / beforeGrams);
        if (absolutePercent > 300m)
            return "critical-review:delta-over-300-percent";
        if (absolutePercent > 100m)
            return "warning-review:delta-over-100-percent";
        return "proposal-ready";
    }

    private static string[] CaptureBlockingRecipeIds(
        IReadOnlyList<string> producerIds,
        IReadOnlyList<string> consumerIds,
        IReadOnlyDictionary<string, string> statuses)
    {
        return (producerIds ?? Array.Empty<string>())
            .Concat(consumerIds ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .Where(recipeId => !statuses.TryGetValue(recipeId, out string status)
                || (!string.Equals(status, "reviewed-exact", StringComparison.Ordinal)
                    && !string.Equals(status, "source-external-mass", StringComparison.Ordinal)
                    && !string.Equals(status, "sink-explicit-mass", StringComparison.Ordinal)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, string[]> BuildRelationIndex(
        IEnumerable<ProductionRecipeSO> recipes,
        Func<ProductionRecipeSO, IEnumerable<string>> itemIds)
    {
        return recipes
            .SelectMany(recipe => itemIds(recipe)
                .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
                .Select(itemId => new { ItemId = itemId, recipe.RecipeId }))
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.RecipeId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, string> BuildRecipeMassImpactIndex(
        IReadOnlyList<ProductionRecipeSO> recipes,
        IReadOnlyDictionary<string, long> beforeGramsByItem,
        IReadOnlyDictionary<string, long> proposedGramsByItem)
    {
        Dictionary<string, List<string>> mutable = new(StringComparer.Ordinal);
        foreach (ProductionRecipeSO recipe in recipes
                     .OrderBy(value => value.RecipeId, StringComparer.Ordinal))
        {
            for (int index = 0; index < recipe.Inputs.Count; index++)
            {
                ItemAmountDefinition input = recipe.Inputs[index];
                Require(input != null && input.HasCanonicalAuthoredValue,
                    $"Recipe '{recipe.RecipeId}' has a noncanonical input at {index}.");
                if (!beforeGramsByItem.TryGetValue(
                        input.ItemId,
                        out long beforeUnitGrams)
                    || !proposedGramsByItem.TryGetValue(
                        input.ItemId,
                        out long proposedUnitGrams))
                {
                    continue;
                }
                AddRecipeImpact(
                    mutable,
                    input.ItemId,
                    recipe.RecipeId + ":input:" + Token(index)
                    + ":qty=" + Token(input.Amount)
                    + ":before="
                    + Token(checked(beforeUnitGrams * input.Amount)) + "g"
                    + ":proposed="
                    + Token(checked(proposedUnitGrams * input.Amount)) + "g");
            }

            IReadOnlyList<ProductionOutputDefinition> outputs =
                recipe.CaptureCanonicalOutputs();
            for (int index = 0; index < outputs.Count; index++)
            {
                ProductionOutputDefinition output = outputs[index];
                if (!beforeGramsByItem.TryGetValue(
                        output.ItemId,
                        out long beforeUnitGrams)
                    || !proposedGramsByItem.TryGetValue(
                        output.ItemId,
                        out long proposedUnitGrams))
                {
                    continue;
                }
                AddRecipeImpact(
                    mutable,
                    output.ItemId,
                    recipe.RecipeId + ":output:" + output.OutputLineId
                    + ":role=" + output.Role
                    + ":qty=" + Token(output.Amount)
                    + ":probability=" + output.Probability.ToString(
                        "0.#########",
                        CultureInfo.InvariantCulture)
                    + ":before="
                    + Token(checked(beforeUnitGrams * output.Amount)) + "g"
                    + ":proposed="
                    + Token(checked(proposedUnitGrams * output.Amount)) + "g");
            }
        }

        return mutable.ToDictionary(
            pair => pair.Key,
            pair => string.Join("|", pair.Value
                .OrderBy(value => value, StringComparer.Ordinal)),
            StringComparer.Ordinal);
    }

    private static void AddRecipeImpact(
        IDictionary<string, List<string>> impacts,
        string itemId,
        string value)
    {
        if (!impacts.TryGetValue(itemId, out List<string> rows))
        {
            rows = new List<string>();
            impacts.Add(itemId, rows);
        }
        rows.Add(value);
    }

    private static HaulBatchProjection BuildHaulBatch(
        long proposedUnitGrams,
        int maxStack,
        PhysicalHaulMassClass haulClass)
    {
        if (proposedUnitGrams <= 0L || maxStack <= 0)
            throw new InvalidOperationException("Haul-batch inputs must be positive.");
        if (haulClass == PhysicalHaulMassClass.IndividualEquipment
            || haulClass == PhysicalHaulMassClass.OversizeEquipment
            || haulClass == PhysicalHaulMassClass.DedicatedTransport)
        {
            return new HaulBatchProjection(
                "class=" + haulClass + ";units=1;grams="
                + Token(proposedUnitGrams) + ";band=single-unit",
                true);
        }

        long minimumGrams = haulClass == PhysicalHaulMassClass.Heavy
            ? 15_000L
            : haulClass == PhysicalHaulMassClass.MicroUrgent
                ? 1_000L
                : 6_000L;
        long maximumGrams = haulClass == PhysicalHaulMassClass.Heavy
            ? 20_000L
            : haulClass == PhysicalHaulMassClass.MicroUrgent
                ? 6_000L
                : 11_000L;
        long minimumUnits = checked(
            (minimumGrams + proposedUnitGrams - 1L) / proposedUnitGrams);
        long maximumUnits = maximumGrams / proposedUnitGrams;
        maximumUnits = Math.Min(maximumUnits, maxStack);
        minimumUnits = Math.Max(1L, minimumUnits);
        if (minimumUnits > maximumUnits)
        {
            return new HaulBatchProjection(
                "class=" + haulClass + ";units=none;unitGrams="
                + Token(proposedUnitGrams) + ";target="
                + Token(minimumGrams) + "-" + Token(maximumGrams)
                + "g;maxStack=" + Token(maxStack),
                false);
        }
        return new HaulBatchProjection(
            "class=" + haulClass + ";units=" + Token(minimumUnits)
                + "-" + Token(maximumUnits) + ";grams="
                + Token(checked(minimumUnits * proposedUnitGrams)) + "-"
                + Token(checked(maximumUnits * proposedUnitGrams)) + "g",
            true);
    }

    private static IReadOnlyList<string> GetRelations(
        IReadOnlyDictionary<string, string[]> index,
        string itemId) => index.TryGetValue(itemId, out string[] values)
            ? values
            : Array.Empty<string>();

    private static RuntimeConsumerCaptureResult CaptureRuntimeConsumerIndex(
        IEnumerable<string> allCatalogItemIds,
        IEnumerable<string> ledgerItemIds)
    {
        HashSet<string> catalog = new(
            allCatalogItemIds ?? throw new ArgumentNullException(
                nameof(allCatalogItemIds)),
            StringComparer.Ordinal);
        HashSet<string> ledger = new(
            ledgerItemIds ?? throw new ArgumentNullException(
                nameof(ledgerItemIds)),
            StringComparer.Ordinal);
        HashSet<string> exactPairs = new(StringComparer.Ordinal);
        List<string> skippedNonLedgerItemIds = new();
        int totalLinkCount = 0;
        int ledgerLinkCount = 0;
        Dictionary<string, List<string>> mutable = new(StringComparer.Ordinal);
        foreach (PhysicalItemRuntimeConsumerCatalog.Link link in
                 PhysicalItemRuntimeConsumerCatalog.All)
        {
            totalLinkCount++;
            Require(!string.IsNullOrWhiteSpace(link.ItemId)
                    && string.Equals(
                        link.ItemId,
                        link.ItemId.Trim(),
                        StringComparison.Ordinal),
                $"Runtime consumer item ID is noncanonical: '{link.ItemId}'.");
            Require(!string.IsNullOrWhiteSpace(link.OwnerId)
                    && link.OwnerId.StartsWith("runtime:", StringComparison.Ordinal)
                    && string.Equals(
                        link.OwnerId,
                        link.OwnerId.Trim(),
                        StringComparison.Ordinal),
                $"Runtime consumer owner ID is noncanonical: '{link.OwnerId}'.");
            Require(catalog.Contains(link.ItemId),
                "Runtime consumer references an unknown item: " + link.ItemId + ".");
            Require(exactPairs.Add(link.ItemId + "\u001f" + link.OwnerId),
                "Duplicate runtime consumer link: " + link.ItemId + " -> "
                + link.OwnerId + ".");
            if (!ledger.Contains(link.ItemId))
            {
                skippedNonLedgerItemIds.Add(link.ItemId);
                continue;
            }
            ledgerLinkCount++;
            if (!mutable.TryGetValue(link.ItemId, out List<string> owners))
            {
                owners = new List<string>();
                mutable.Add(link.ItemId, owners);
            }
            owners.Add(link.OwnerId);
        }
        Dictionary<string, string[]> index = mutable.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
        string[] skippedDistinct = skippedNonLedgerItemIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new RuntimeConsumerCaptureResult(
            index,
            totalLinkCount,
            ledgerLinkCount,
            skippedNonLedgerItemIds.Count,
            skippedDistinct,
            ComputeCanonicalTokenDigest(skippedDistinct));
    }

    private static IReadOnlyList<string> MergeRelations(
        IReadOnlyList<string> first,
        IReadOnlyList<string> second) => (first ?? Array.Empty<string>())
        .Concat(second ?? Array.Empty<string>())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static byte[] BuildCsv(
        IReadOnlyList<V27PhysicalMassFamilyProposalRow> rows)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 16384);
        WriteRow(writer, new[]
        {
            "schemaVersion", "stableId", "definitionKind", "unitSemantic",
            "authoringFamily", "beforeUnitGrams", "proposedUnitGrams",
            "massFormula", "producerIds", "consumerIds",
            "recipeInputAndOutputGrams", "packageTareDisposition",
            "maxStackMass", "warehouseAndBufferImpact",
            "ordinaryHaulBatch", "ewuAndPriceImpact", "sourceDigest",
            "proposalReason", "reviewDisposition"
        });
        foreach (V27PhysicalMassFamilyProposalRow row in rows)
        {
            WriteRow(writer, new[]
            {
                Schema,
                row.StableId,
                row.DefinitionKind,
                row.UnitSemantic,
                row.AuthoringFamily,
                Token(row.BeforeUnitGrams),
                Token(row.ProposedUnitGrams),
                row.MassFormula,
                row.ProducerIds,
                row.ConsumerIds,
                row.RecipeInputAndOutputGrams,
                row.PackageTareDisposition,
                row.MaxStackMass,
                row.WarehouseAndBufferImpact,
                row.OrdinaryHaulBatch,
                row.EwuAndPriceImpact,
                row.SourceDigest,
                row.ProposalReason,
                row.ReviewDisposition
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static string BuildReport(
        IReadOnlyList<V27PhysicalMassFamilyProposalRow> rows,
        int recipeCount,
        int semanticCount,
        RuntimeConsumerCaptureResult runtimeConsumers,
        V27PhysicalMassCouplingCapture coupling,
        string sourceDigest,
        string assetDigest)
    {
        int changed = rows.Count(value =>
            value.BeforeUnitGrams != value.ProposedUnitGrams);
        int critical = rows.Count(value =>
            value.ReviewDisposition.Contains("critical", StringComparison.Ordinal));
        int warning = rows.Count(value =>
            value.ReviewDisposition.StartsWith("warning", StringComparison.Ordinal));
        int ready = rows.Count(value =>
            value.ReviewDisposition == "proposal-ready");
        int blocked = rows.Count(value =>
            value.ReviewDisposition.StartsWith(
                "proposal-blocked", StringComparison.Ordinal));
        int unchanged = rows.Count(value =>
            value.ReviewDisposition == "no-change");
        return "RESULT=IN_PROGRESS; phase=mass-family-proposal; assetMutations=0\n"
            + $"schema={Schema}; scope=ledger-items-only\n"
            + $"rows={rows.Count}; recipes={recipeCount}; canonicalSemantics={semanticCount}\n"
            + $"runtimeConsumerLinks={runtimeConsumers.TotalLinkCount}; "
            + $"ledgerRuntimeConsumerLinks={runtimeConsumers.LedgerLinkCount}; "
            + $"skippedNonLedgerLinks={runtimeConsumers.SkippedNonLedgerLinkCount}; "
            + $"skippedNonLedgerDistinctItems={runtimeConsumers.SkippedNonLedgerItemIds.Count}; "
            + $"skippedNonLedgerItemDigest={runtimeConsumers.SkippedNonLedgerItemDigest}\n"
            + $"proposedChanges={changed}; ready={ready}; blocked={blocked}; "
            + $"unchanged={unchanged}; "
            + $"critical={critical}; warning={warning}\n"
            + "proposalMode=AuditOnly; appliesScriptableObjects=false; "
            + "missingSemanticPolicy=fail-visible-no-guess\n"
            + $"couplingRows={coupling.Rows.Count}; "
            + $"couplingChangedRows={coupling.ChangedRowCount}; "
            + "couplingCritical=0; "
            + "warehouseAndBufferAuthority=PASS; "
            + "ewuAndPriceAuthority=PASS; rootlessDelta=0\n"
            + "sort=stableId:ordinal; csv=rfc4180-crlf; "
            + "deterministicRecapture=PASS; byteIdentical=true\n"
            + $"sourceDigest={sourceDigest}\n"
            + $"inspectedAssetDigest={assetDigest}\n"
            + "nextGate=REVIEW_ANOMALIES_THEN_APPLY_APPROVED_ONLY\n";
    }

    private static void VerifyPureClassificationScenario()
    {
        Require(ClassifyAnomaly(
                false,
                1_000L,
                1_000L,
                PhysicalHaulMassClass.Ordinary,
                true,
                50,
                true)
            == "local-critical:missing-unit-semantic",
            "Missing semantic did not remain fail-visible.");
        Require(ClassifyAnomaly(
                true,
                1_000L,
                1_000L,
                PhysicalHaulMassClass.Ordinary,
                true,
                50,
                true) == "no-change",
            "Equal canonical mass was not classified as no-change.");
        Require(ClassifyAnomaly(
                true,
                1_000L,
                1_500L,
                PhysicalHaulMassClass.Ordinary,
                true,
                50,
                true) == "proposal-ready",
            "A bounded family proposal was not marked ready.");
        Require(ClassifyAnomaly(
                true,
                1_000L,
                5_000L,
                PhysicalHaulMassClass.Heavy,
                true,
                50,
                true)
            == "critical-review:delta-over-300-percent",
            "An extreme family delta did not require critical review.");
        Require(ClassifyAnomaly(
                true,
                1_000L,
                20_001L,
                PhysicalHaulMassClass.Ordinary,
                false,
                50,
                true)
            == "local-critical:ordinary-unit-over-20kg",
            "An ordinary unit over 20kg did not fail visibly.");
        Require(ClassifyAnomaly(
                true,
                13_000L,
                13_000L,
                PhysicalHaulMassClass.Heavy,
                false,
                1,
                true)
            == "intentional-single-heavy",
            "A canonical singleton heavy item was not classified as intentional.");
        Require(ClassifyAnomaly(
                true,
                1_000L,
                1_500L,
                PhysicalHaulMassClass.Ordinary,
                true,
                50,
                false)
            == "proposal-blocked:dependency-mass-contract-open",
            "An open dependency mass contract did not block proposal readiness.");
        HaulBatchProjection ordinary = BuildHaulBatch(
                1_000L,
                75,
                PhysicalHaulMassClass.Ordinary);
        Require(ordinary.IsFeasible
                && ordinary.Text
                == "class=Ordinary;units=6-11;grams=6000-11000g",
            "Ordinary 1kg items did not produce the 6-11kg haul band.");
        HaulBatchProjection ordinaryOverBand = BuildHaulBatch(
                12_000L,
                75,
                PhysicalHaulMassClass.Ordinary);
        Require(!ordinaryOverBand.IsFeasible
                && ordinaryOverBand.Text
                == "class=Ordinary;units=none;unitGrams=12000;target=6000-11000g;maxStack=75",
            "An ordinary unit above the target band did not remain fail-visible.");
        HaulBatchProjection heavy = BuildHaulBatch(
                5_000L,
                75,
                PhysicalHaulMassClass.Heavy);
        Require(heavy.IsFeasible
                && heavy.Text == "class=Heavy;units=3-4;grams=15000-20000g",
            "Heavy 5kg items did not produce the 15-20kg haul band.");
    }

    private static Dictionary<string, T> UniqueIndex<T>(
        IEnumerable<T> values,
        Func<T, string> key,
        string label)
    {
        Dictionary<string, T> result = new Dictionary<string, T>(
            StringComparer.Ordinal);
        foreach (T value in values)
        {
            string id = key(value);
            Require(!string.IsNullOrWhiteSpace(id)
                    && string.Equals(id, id.Trim(), StringComparison.Ordinal),
                $"Non-canonical {label} ID: '{id}'.");
            if (!result.TryAdd(id, value))
                throw new InvalidOperationException($"Duplicate {label} ID: {id}.");
        }
        return result;
    }

    private static string ComputeAggregateDigest(IEnumerable<string> paths)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        using SHA256 sha = SHA256.Create();
        foreach (string path in paths
                     .Select(CanonicalPath)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            string absolute = Path.Combine(
                projectRoot,
                path.Replace('/', Path.DirectorySeparatorChar));
            Require(File.Exists(absolute), "Digest source is missing: " + path + ".");
            byte[] pathBytes = Encoding.UTF8.GetBytes(path);
            sha.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
            byte[] bytes = File.ReadAllBytes(absolute);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(sha.Hash);
    }

    private static string ComputeCanonicalTokenDigest(IEnumerable<string> values)
    {
        using SHA256 sha = SHA256.Create();
        byte[] separator = { 0 };
        foreach (string value in (values ?? Array.Empty<string>())
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            Require(!string.IsNullOrWhiteSpace(value)
                    && value.IndexOf('\0') < 0
                    && string.Equals(value, value.Trim(), StringComparison.Ordinal),
                "Digest token must be canonical and must not contain NUL.");
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            sha.TransformBlock(separator, 0, separator.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(sha.Hash);
    }

    private static string Hex(IEnumerable<byte> bytes) => string.Concat(
        bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

    private static void WriteRow(
        V27Utf8CsvWriter writer,
        IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index != 0)
                writer.WriteAscii(',');
            writer.WriteEscapedField((fields[index] ?? string.Empty).AsSpan());
        }
        writer.WriteCrLf();
    }

    private static string CanonicalPath(string path) =>
        (path ?? string.Empty).Replace('\\', '/');

    private static string Token(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string SignedToken(long value) => value > 0L
        ? "+" + Token(value)
        : Token(value);

    private static string RatioToken(long numerator, long denominator) =>
        ((decimal)numerator / denominator).ToString(
            "0.######",
            CultureInfo.InvariantCulture);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct HaulBatchProjection
    {
        public HaulBatchProjection(string text, bool isFeasible)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            IsFeasible = isFeasible;
        }

        public string Text { get; }
        public bool IsFeasible { get; }
    }

    private sealed class RuntimeConsumerCaptureResult
    {
        public RuntimeConsumerCaptureResult(
            IReadOnlyDictionary<string, string[]> index,
            int totalLinkCount,
            int ledgerLinkCount,
            int skippedNonLedgerLinkCount,
            IReadOnlyList<string> skippedNonLedgerItemIds,
            string skippedNonLedgerItemDigest)
        {
            Index = index ?? throw new ArgumentNullException(nameof(index));
            TotalLinkCount = totalLinkCount;
            LedgerLinkCount = ledgerLinkCount;
            SkippedNonLedgerLinkCount = skippedNonLedgerLinkCount;
            SkippedNonLedgerItemIds = skippedNonLedgerItemIds
                ?? throw new ArgumentNullException(nameof(skippedNonLedgerItemIds));
            SkippedNonLedgerItemDigest = skippedNonLedgerItemDigest
                ?? throw new ArgumentNullException(nameof(skippedNonLedgerItemDigest));
            Require(TotalLinkCount >= 0
                    && LedgerLinkCount >= 0
                    && SkippedNonLedgerLinkCount >= 0
                    && TotalLinkCount
                    == LedgerLinkCount + SkippedNonLedgerLinkCount,
                "Runtime-consumer capture counts are inconsistent.");
        }

        public IReadOnlyDictionary<string, string[]> Index { get; }
        public int TotalLinkCount { get; }
        public int LedgerLinkCount { get; }
        public int SkippedNonLedgerLinkCount { get; }
        public IReadOnlyList<string> SkippedNonLedgerItemIds { get; }
        public string SkippedNonLedgerItemDigest { get; }
    }

    private sealed class CaptureResult
    {
        public CaptureResult(
            IReadOnlyList<V27PhysicalMassFamilyProposalRow> rows,
            byte[] csv,
            byte[] report,
            string inspectedAssetDigest,
            V27PhysicalMassCouplingCapture coupling)
        {
            Rows = rows;
            Csv = csv;
            Report = report;
            InspectedAssetDigest = inspectedAssetDigest;
            Coupling = coupling
                ?? throw new ArgumentNullException(nameof(coupling));
        }

        public IReadOnlyList<V27PhysicalMassFamilyProposalRow> Rows { get; }
        public byte[] Csv { get; }
        public byte[] Report { get; }
        public string InspectedAssetDigest { get; }
        public V27PhysicalMassCouplingCapture Coupling { get; }
        public int ProposedChangeCount => Rows.Count(value =>
            value.BeforeUnitGrams != value.ProposedUnitGrams);
        public int CriticalCount => Rows.Count(value =>
            value.ReviewDisposition.Contains("critical", StringComparison.Ordinal));
        public int WarningCount => Rows.Count(value =>
            value.ReviewDisposition.StartsWith("warning", StringComparison.Ordinal));
    }
}

internal readonly struct V27PhysicalMassFamilyProposalRow
{
    public V27PhysicalMassFamilyProposalRow(
        string stableId,
        string definitionKind,
        string unitSemantic,
        string authoringFamily,
        long beforeUnitGrams,
        long proposedUnitGrams,
        string massFormula,
        string producerIds,
        string consumerIds,
        string recipeInputAndOutputGrams,
        string packageTareDisposition,
        string maxStackMass,
        string warehouseAndBufferImpact,
        string ordinaryHaulBatch,
        string ewuAndPriceImpact,
        string proposalReason,
        string reviewDisposition,
        string sourceDigest)
    {
        StableId = stableId;
        DefinitionKind = definitionKind;
        UnitSemantic = unitSemantic;
        AuthoringFamily = authoringFamily;
        BeforeUnitGrams = beforeUnitGrams;
        ProposedUnitGrams = proposedUnitGrams;
        MassFormula = massFormula;
        ProducerIds = producerIds;
        ConsumerIds = consumerIds;
        RecipeInputAndOutputGrams = recipeInputAndOutputGrams;
        PackageTareDisposition = packageTareDisposition;
        MaxStackMass = maxStackMass;
        WarehouseAndBufferImpact = warehouseAndBufferImpact;
        OrdinaryHaulBatch = ordinaryHaulBatch;
        EwuAndPriceImpact = ewuAndPriceImpact;
        ProposalReason = proposalReason;
        ReviewDisposition = reviewDisposition;
        SourceDigest = sourceDigest;
    }

    public string StableId { get; }
    public string DefinitionKind { get; }
    public string UnitSemantic { get; }
    public string AuthoringFamily { get; }
    public long BeforeUnitGrams { get; }
    public long ProposedUnitGrams { get; }
    public string MassFormula { get; }
    public string ProducerIds { get; }
    public string ConsumerIds { get; }
    public string RecipeInputAndOutputGrams { get; }
    public string PackageTareDisposition { get; }
    public string MaxStackMass { get; }
    public string WarehouseAndBufferImpact { get; }
    public string OrdinaryHaulBatch { get; }
    public string EwuAndPriceImpact { get; }
    public string ProposalReason { get; }
    public string ReviewDisposition { get; }
    public string SourceDigest { get; }
}
#endif
