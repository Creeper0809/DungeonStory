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
        Require(string.Equals(
                first.InspectedAssetDigest,
                second.InspectedAssetDigest,
                StringComparison.Ordinal),
            "Physical-mass family proposal source assets changed between captures.");

        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
            stream.Write(first.Csv, 0, first.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));

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
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal));

        Dictionary<string, string[]> producersByItem = BuildRelationIndex(
            recipes,
            recipe => recipe.Outputs.Select(output => output.ItemId));
        Dictionary<string, string[]> consumersByItem = BuildRelationIndex(
            recipes,
            recipe => recipe.Inputs.Select(input => input.ItemId));

        V27PhysicalMassFamilyProposalRow[] rows = ledgerIds
            .Select(itemId => BuildRow(
                items[itemId],
                semantics.TryGetValue(itemId, out CanonicalItemUnitSemantic semantic),
                semantic,
                GetRelations(producersByItem, itemId),
                GetRelations(consumersByItem, itemId),
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
            sourceDigest,
            beforeAssetDigest));
        return new CaptureResult(rows, csv, report, beforeAssetDigest);
    }

    private static V27PhysicalMassFamilyProposalRow BuildRow(
        ItemDefinitionSO item,
        bool hasSemantic,
        CanonicalItemUnitSemantic semantic,
        IReadOnlyList<string> producerIds,
        IReadOnlyList<string> consumerIds,
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
        string anomalyDisposition = ClassifyAnomaly(
            hasSemantic,
            beforeGrams,
            proposedGrams,
            haulClass);

        return new V27PhysicalMassFamilyProposalRow(
            item.ItemId,
            family,
            beforeGrams,
            proposedGrams,
            formula,
            reason,
            string.Join("|", producerIds),
            string.Join("|", consumerIds),
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
        PhysicalHaulMassClass haulClass)
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
        if (beforeGrams == proposedGrams)
            return "no-change";

        decimal absolutePercent = Math.Abs(
            ((decimal)proposedGrams - beforeGrams) * 100m / beforeGrams);
        if (absolutePercent > 300m)
            return "critical-review:delta-over-300-percent";
        if (absolutePercent > 100m)
            return "warning-review:delta-over-100-percent";
        return "proposal-ready";
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

    private static IReadOnlyList<string> GetRelations(
        IReadOnlyDictionary<string, string[]> index,
        string itemId) => index.TryGetValue(itemId, out string[] values)
            ? values
            : Array.Empty<string>();

    private static byte[] BuildCsv(
        IReadOnlyList<V27PhysicalMassFamilyProposalRow> rows)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 16384);
        WriteRow(writer, new[]
        {
            "schemaVersion", "stableId", "family", "beforeGrams",
            "proposedGrams", "formula", "reason", "producerIds",
            "consumerIds", "anomalyDisposition", "sourceDigest"
        });
        foreach (V27PhysicalMassFamilyProposalRow row in rows)
        {
            WriteRow(writer, new[]
            {
                "v27.mass.family-proposal.1",
                row.StableId,
                row.Family,
                Token(row.BeforeGrams),
                Token(row.ProposedGrams),
                row.Formula,
                row.Reason,
                row.ProducerIds,
                row.ConsumerIds,
                row.AnomalyDisposition,
                row.SourceDigest
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static string BuildReport(
        IReadOnlyList<V27PhysicalMassFamilyProposalRow> rows,
        int recipeCount,
        int semanticCount,
        string sourceDigest,
        string assetDigest)
    {
        int changed = rows.Count(value => value.BeforeGrams != value.ProposedGrams);
        int critical = rows.Count(value =>
            value.AnomalyDisposition.Contains("critical", StringComparison.Ordinal));
        int warning = rows.Count(value =>
            value.AnomalyDisposition.StartsWith("warning", StringComparison.Ordinal));
        int ready = rows.Count(value =>
            value.AnomalyDisposition == "proposal-ready");
        int unchanged = rows.Count(value =>
            value.AnomalyDisposition == "no-change");
        return "RESULT=IN_PROGRESS; phase=mass-family-proposal; assetMutations=0\n"
            + $"rows={rows.Count}; recipes={recipeCount}; canonicalSemantics={semanticCount}\n"
            + $"proposedChanges={changed}; ready={ready}; unchanged={unchanged}; "
            + $"critical={critical}; warning={warning}\n"
            + "proposalMode=AuditOnly; appliesScriptableObjects=false; "
            + "missingSemanticPolicy=fail-visible-no-guess\n"
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
                PhysicalHaulMassClass.Ordinary)
            == "local-critical:missing-unit-semantic",
            "Missing semantic did not remain fail-visible.");
        Require(ClassifyAnomaly(
                true,
                1_000L,
                1_000L,
                PhysicalHaulMassClass.Ordinary) == "no-change",
            "Equal canonical mass was not classified as no-change.");
        Require(ClassifyAnomaly(
                true,
                1_000L,
                1_500L,
                PhysicalHaulMassClass.Ordinary) == "proposal-ready",
            "A bounded family proposal was not marked ready.");
        Require(ClassifyAnomaly(
                true,
                1_000L,
                5_000L,
                PhysicalHaulMassClass.Heavy)
            == "critical-review:delta-over-300-percent",
            "An extreme family delta did not require critical review.");
        Require(ClassifyAnomaly(
                true,
                1_000L,
                20_001L,
                PhysicalHaulMassClass.Ordinary)
            == "local-critical:ordinary-unit-over-20kg",
            "An ordinary unit over 20kg did not fail visibly.");
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CaptureResult
    {
        public CaptureResult(
            IReadOnlyList<V27PhysicalMassFamilyProposalRow> rows,
            byte[] csv,
            byte[] report,
            string inspectedAssetDigest)
        {
            Rows = rows;
            Csv = csv;
            Report = report;
            InspectedAssetDigest = inspectedAssetDigest;
        }

        public IReadOnlyList<V27PhysicalMassFamilyProposalRow> Rows { get; }
        public byte[] Csv { get; }
        public byte[] Report { get; }
        public string InspectedAssetDigest { get; }
        public int ProposedChangeCount => Rows.Count(value =>
            value.BeforeGrams != value.ProposedGrams);
        public int CriticalCount => Rows.Count(value =>
            value.AnomalyDisposition.Contains("critical", StringComparison.Ordinal));
        public int WarningCount => Rows.Count(value =>
            value.AnomalyDisposition.StartsWith("warning", StringComparison.Ordinal));
    }
}

internal readonly struct V27PhysicalMassFamilyProposalRow
{
    public V27PhysicalMassFamilyProposalRow(
        string stableId,
        string family,
        long beforeGrams,
        long proposedGrams,
        string formula,
        string reason,
        string producerIds,
        string consumerIds,
        string anomalyDisposition,
        string sourceDigest)
    {
        StableId = stableId;
        Family = family;
        BeforeGrams = beforeGrams;
        ProposedGrams = proposedGrams;
        Formula = formula;
        Reason = reason;
        ProducerIds = producerIds;
        ConsumerIds = consumerIds;
        AnomalyDisposition = anomalyDisposition;
        SourceDigest = sourceDigest;
    }

    public string StableId { get; }
    public string Family { get; }
    public long BeforeGrams { get; }
    public long ProposedGrams { get; }
    public string Formula { get; }
    public string Reason { get; }
    public string ProducerIds { get; }
    public string ConsumerIds { get; }
    public string AnomalyDisposition { get; }
    public string SourceDigest { get; }
}
#endif
