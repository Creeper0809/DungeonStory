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
/// AuditOnly inventory and deterministic backfill proposal for authored production
/// output-line identities. This verifier reads serialized recipe authority directly,
/// emits review artifacts, and never mutates a recipe or any other ScriptableObject.
/// </summary>
public static class V27CanonicalOutputLineBackfillProposalDebugScenarios
{
    public const string CsvPath =
        "Artifacts/QA/v27-canonical-output-line-backfill-proposals.csv";
    public const string ReportPath =
        "Artifacts/QA/v27-canonical-output-line-backfill-proposals.txt";

    private const int ExpectedRecipeCount = 355;
    private const int ExpectedPhysicalOutputLineCount = 357;
    private const int ExpectedCanonicalLineCount = 4;
    private const int ExpectedMissingLineCount = 353;
    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/V27CanonicalOutputLineBackfillProposalDebugScenarios.cs";
    private const string RecipeAuthorityPath =
        "Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs";
    private const string OutputContractPath =
        "Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs";

    [MenuItem("DungeonStory/V27/Production/Capture Output-Line Backfill Proposals (AuditOnly)")]
    public static void RunFromMenu()
    {
        VerifyPureProposalScenario();
        CaptureResult first = Capture();
        CaptureResult second = Capture();
        Require(first.Csv.SequenceEqual(second.Csv),
            "Canonical output-line proposal CSV changed between identical captures.");
        Require(first.Report.SequenceEqual(second.Report),
            "Canonical output-line proposal report changed between identical captures.");
        Require(string.Equals(
                first.InspectedAssetDigest,
                second.InspectedAssetDigest,
                StringComparison.Ordinal),
            "Canonical output-line source assets changed between captures.");
        Require(first.CanonicalCount == ExpectedCanonicalLineCount
                && first.MissingCount == ExpectedMissingLineCount,
            "The reviewed pre-apply proposal artifact must not be overwritten "
            + "after output-line backfill has already been applied.");

        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
            stream.Write(first.Csv, 0, first.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));

        Debug.Log(
            "V27 canonical output-line backfill proposal passed: "
            + $"recipes={first.RecipeCount}; physicalLines={first.Rows.Count}; "
            + $"preserved={first.CanonicalCount}; proposed={first.MissingCount}; "
            + "assetMutations=0; deterministicRecapture=PASS; status=IN_PROGRESS.");
    }

    /// <summary>
    /// Read-only integration entrypoint for a later approved asset-application batch.
    /// Rows retain authored output ordinal and are recipe-ID/ordinal sorted.
    /// </summary>
    internal static IReadOnlyList<V27CanonicalOutputLineBackfillProposalRow>
        CaptureProposalRowsForAudit() => Capture().Rows;

    internal static V27CanonicalOutputLineBackfillProposalSnapshot
        CaptureProposalSnapshotForAudit()
    {
        CaptureResult capture = Capture();
        return new V27CanonicalOutputLineBackfillProposalSnapshot(
            capture.Rows,
            capture.SourceDigest,
            capture.InspectedAssetDigest);
    }

    private static CaptureResult Capture()
    {
        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        GameDomainContentCatalogSO domain = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .Single();
        ProductionRecipeSO[] recipes = domain.GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => CanonicalPath(AssetDatabase.GetAssetPath(value)),
                StringComparer.Ordinal)
            .ToArray();

        Require(recipes.Length == ExpectedRecipeCount,
            $"Expected {ExpectedRecipeCount} recipes, found {recipes.Length}.");
        Require(recipes.Select(value => value.RecipeId)
                .Distinct(StringComparer.Ordinal).Count() == recipes.Length,
            "Output-line proposal inventory contains duplicate recipe IDs.");

        string[] recipePaths = recipes
            .Select(value => CanonicalPath(AssetDatabase.GetAssetPath(value)))
            .ToArray();
        Require(recipePaths.All(value => !string.IsNullOrWhiteSpace(value)),
            "At least one live recipe has no asset path.");
        string beforeAssetDigest = ComputeAggregateDigest(recipePaths);
        Dictionary<string, string> sourceDigestByRecipePath = recipePaths
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                value => value,
                value => ComputeAggregateDigest(new[]
                {
                    value,
                    SelfPath,
                    RecipeAuthorityPath,
                    OutputContractPath
                }),
                StringComparer.Ordinal);

        List<V27CanonicalOutputLineBackfillProposalRow> rows =
            new List<V27CanonicalOutputLineBackfillProposalRow>(
                ExpectedPhysicalOutputLineCount);
        foreach (ProductionRecipeSO recipe in recipes)
        {
            string sourcePath = CanonicalPath(AssetDatabase.GetAssetPath(recipe));
            CaptureRecipeRows(
                recipe,
                sourcePath,
                sourceDigestByRecipePath[sourcePath],
                rows);
        }
        V27CanonicalOutputLineBackfillProposalRow[] frozen = rows
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => value.AuthoredOutputOrdinal)
            .ToArray();

        int canonicalCount = frozen.Count(value => value.HasCanonicalAuthoredLine);
        int missingCount = frozen.Length - canonicalCount;
        Require(frozen.Length == ExpectedPhysicalOutputLineCount,
            $"Expected {ExpectedPhysicalOutputLineCount} physical output lines, "
            + $"found {frozen.Length}.");
        bool initialBackfillState = canonicalCount == ExpectedCanonicalLineCount
            && missingCount == ExpectedMissingLineCount;
        bool fullyAppliedState = canonicalCount == ExpectedPhysicalOutputLineCount
            && missingCount == 0;
        Require(initialBackfillState || fullyAppliedState,
            "Output-line authority is neither the exact initial backfill state "
            + $"({ExpectedCanonicalLineCount}/{ExpectedMissingLineCount}) nor the "
            + $"fully applied state ({ExpectedPhysicalOutputLineCount}/0): "
            + $"canonical={canonicalCount}; missing={missingCount}.");
        Require(frozen.Where(value => value.HasCanonicalAuthoredLine).All(value =>
                string.Equals(
                    value.AuthoredOutputLineId,
                    value.ProposedOutputLineId,
                    StringComparison.Ordinal)
                && value.AuthoredRole == value.ProposedRole),
            "An existing canonical output line was not preserved exactly.");
        Require(frozen.Where(value => !value.HasCanonicalAuthoredLine).All(value =>
                ProductionOutputDefinition.IsCanonicalOutputLineId(
                    value.ProposedOutputLineId)
                && ProductionOutputRoleRules.IsPhysical(value.ProposedRole)),
            "A missing physical output line received a non-canonical proposal.");

        foreach (IGrouping<string, V27CanonicalOutputLineBackfillProposalRow> recipeRows
                 in frozen.GroupBy(value => value.RecipeId, StringComparer.Ordinal))
        {
            Require(recipeRows.Select(value => value.ProposedOutputLineId)
                    .Distinct(StringComparer.Ordinal).Count() == recipeRows.Count(),
                "Proposed output line ID collision in recipe: "
                + recipeRows.Key + ".");
        }

        string afterAssetDigest = ComputeAggregateDigest(recipePaths);
        Require(string.Equals(beforeAssetDigest, afterAssetDigest, StringComparison.Ordinal),
            "AuditOnly output-line proposal capture mutated a recipe asset.");

        string sourceDigest = ComputeAggregateDigest(recipePaths
            .Append(SelfPath)
            .Append(RecipeAuthorityPath)
            .Append(OutputContractPath));
        byte[] csv = BuildCsv(frozen);
        byte[] report = Encoding.UTF8.GetBytes(BuildReport(
            recipes.Length,
            frozen,
            sourceDigest,
            beforeAssetDigest));
        return new CaptureResult(
            frozen,
            csv,
            report,
            recipes.Length,
            canonicalCount,
            missingCount,
            sourceDigest,
            beforeAssetDigest);
    }

    private static void CaptureRecipeRows(
        ProductionRecipeSO recipe,
        string sourcePath,
        string sourceDigest,
        ICollection<V27CanonicalOutputLineBackfillProposalRow> outputRows)
    {
        SerializedObject serialized = new SerializedObject(recipe);
        SerializedProperty recipeIdProperty = serialized.FindProperty("recipeId")
            ?? throw new InvalidOperationException(
                "Recipe is missing serialized recipeId: " + sourcePath + ".");
        string rawRecipeId = recipeIdProperty.stringValue ?? string.Empty;
        RequireCanonicalToken(rawRecipeId, "recipe ID", sourcePath);
        Require(string.Equals(rawRecipeId, recipe.RecipeId, StringComparison.Ordinal),
            "Recipe getter masks non-canonical serialized authority: "
            + sourcePath + ".");

        SerializedProperty outputs = serialized.FindProperty("outputs")
            ?? throw new InvalidOperationException(
                "Recipe is missing serialized outputs: " + rawRecipeId + ".");
        Require(outputs.isArray,
            "Serialized recipe outputs are not an array: " + rawRecipeId + ".");
        Require(outputs.arraySize == recipe.Outputs.Count,
            "Serialized/runtime output count drift: " + rawRecipeId + ".");

        HashSet<string> authoredIds = new HashSet<string>(StringComparer.Ordinal);
        for (int ordinal = 0; ordinal < outputs.arraySize; ordinal++)
        {
            SerializedProperty element = outputs.GetArrayElementAtIndex(ordinal);
            string authoredLineId = RequireStringProperty(
                element,
                "outputLineId",
                rawRecipeId,
                ordinal);
            int authoredRoleValue = RequireIntProperty(
                element,
                "role",
                rawRecipeId,
                ordinal);
            string itemId = RequireStringProperty(
                element,
                "itemId",
                rawRecipeId,
                ordinal);
            int amount = RequireIntProperty(
                element,
                "amount",
                rawRecipeId,
                ordinal);
            float probability = RequireFloatProperty(
                element,
                "probability",
                rawRecipeId,
                ordinal);

            RequireCanonicalToken(itemId, "output item ID", rawRecipeId);
            Require(amount > 0,
                $"Recipe '{rawRecipeId}' output[{ordinal}] has non-positive amount.");
            Require(float.IsFinite(probability)
                    && probability >= 0f
                    && probability <= 1f,
                $"Recipe '{rawRecipeId}' output[{ordinal}] has invalid probability "
                + $"'{probability:R}'.");
            Require(Enum.IsDefined(
                    typeof(ProductionOutputRole),
                    authoredRoleValue),
                $"Recipe '{rawRecipeId}' output[{ordinal}] has invalid role "
                + $"'{authoredRoleValue}'.");
            ProductionOutputRole authoredRole =
                (ProductionOutputRole)authoredRoleValue;
            Require(ProductionOutputRoleRules.IsPhysical(authoredRole),
                $"Recipe '{rawRecipeId}' output[{ordinal}] authors DeclaredLoss "
                + "as a physical item line. Declared loss belongs to the mass "
                + "disposition contract, not output routing.");

            bool hasCanonicalLine = !string.IsNullOrEmpty(authoredLineId);
            if (hasCanonicalLine)
            {
                Require(ProductionOutputDefinition.IsCanonicalOutputLineId(
                        authoredLineId),
                    $"Recipe '{rawRecipeId}' output[{ordinal}] has a non-canonical "
                    + $"authored line ID '{authoredLineId}'.");
                Require(authoredIds.Add(authoredLineId),
                    $"Recipe '{rawRecipeId}' contains duplicate authored output "
                    + $"line ID '{authoredLineId}'.");
            }

            ProductionOutputRole proposedRole = hasCanonicalLine
                ? authoredRole
                : InferRole(ordinal, itemId);
            Require(ProductionOutputRoleRules.IsPhysical(proposedRole),
                "DeclaredLoss cannot be proposed as a physical output role.");
            string proposedLineId = hasCanonicalLine
                ? authoredLineId
                : BuildProposedOutputLineId(
                    rawRecipeId,
                    ordinal,
                    itemId,
                    proposedRole);
            Require(ProductionOutputDefinition.IsCanonicalOutputLineId(
                    proposedLineId),
                $"Generated non-canonical output line ID '{proposedLineId}'.");

            outputRows.Add(new V27CanonicalOutputLineBackfillProposalRow(
                rawRecipeId,
                ordinal,
                itemId,
                amount,
                probability,
                authoredLineId,
                authoredRole,
                proposedLineId,
                proposedRole,
                hasCanonicalLine,
                hasCanonicalLine
                    ? "preserve-existing-canonical-line"
                    : BuildProposalReason(ordinal, itemId, proposedRole),
                hasCanonicalLine ? "preserve-canonical" : "backfill-proposal-ready",
                sourcePath,
                sourceDigest));
        }
    }

    internal static ProductionOutputRole InferRole(int ordinal, string itemId)
    {
        if (ordinal < 0)
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        if (string.IsNullOrWhiteSpace(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical output item ID is required.",
                nameof(itemId));
        }
        if (ordinal == 0)
            return ProductionOutputRole.Main;
        if (itemId.StartsWith("container:", StringComparison.Ordinal)
            || itemId.StartsWith("packaging:", StringComparison.Ordinal))
        {
            return ProductionOutputRole.ReturnedPackaging;
        }
        if (itemId.StartsWith("waste:", StringComparison.Ordinal))
            return ProductionOutputRole.RecoverableWaste;
        return ProductionOutputRole.Byproduct;
    }

    internal static string BuildProposedOutputLineId(
        string recipeId,
        int ordinal,
        string itemId,
        ProductionOutputRole role)
    {
        return ProductionOutputLineAuthoring.BuildStableId(
            recipeId,
            ordinal,
            itemId,
            role);
    }

    private static string BuildProposalReason(
        int ordinal,
        string itemId,
        ProductionOutputRole role)
    {
        string rule = ordinal == 0
            ? "authored-ordinal-zero-main"
            : role == ProductionOutputRole.ReturnedPackaging
                ? "non-primary-container-return"
                : role == ProductionOutputRole.RecoverableWaste
                    ? "non-primary-physical-waste"
                    : "non-primary-byproduct";
        return "missing-output-line-id; rule=" + rule
            + "; item=" + itemId;
    }

    private static byte[] BuildCsv(
        IReadOnlyList<V27CanonicalOutputLineBackfillProposalRow> rows)
    {
        using MemoryStream stream = new MemoryStream();
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 16384);
        WriteRow(writer, new[]
        {
            "schemaVersion", "recipeId", "authoredOutputOrdinal", "itemId",
            "amount", "probability", "authoredOutputLineId", "authoredRole",
            "proposedOutputLineId", "proposedRole", "proposalReason",
            "proposalDisposition", "sourceAuthority", "sourceDigest"
        });
        foreach (V27CanonicalOutputLineBackfillProposalRow row in rows)
        {
            WriteRow(writer, new[]
            {
                "v27.production.output-line-backfill.1",
                row.RecipeId,
                Token(row.AuthoredOutputOrdinal),
                row.ItemId,
                Token(row.Amount),
                row.Probability.ToString("R", CultureInfo.InvariantCulture),
                row.AuthoredOutputLineId,
                row.AuthoredRole.ToString(),
                row.ProposedOutputLineId,
                row.ProposedRole.ToString(),
                row.ProposalReason,
                row.ProposalDisposition,
                row.SourceAuthority,
                row.SourceDigest
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static string BuildReport(
        int recipeCount,
        IReadOnlyList<V27CanonicalOutputLineBackfillProposalRow> rows,
        string sourceDigest,
        string assetDigest)
    {
        int canonical = rows.Count(value => value.HasCanonicalAuthoredLine);
        int missing = rows.Count - canonical;
        int main = rows.Count(value => value.ProposedRole == ProductionOutputRole.Main);
        int byproduct = rows.Count(value =>
            value.ProposedRole == ProductionOutputRole.Byproduct);
        int packaging = rows.Count(value =>
            value.ProposedRole == ProductionOutputRole.ReturnedPackaging);
        int waste = rows.Count(value =>
            value.ProposedRole == ProductionOutputRole.RecoverableWaste);
        int probabilistic = rows.Count(value => value.Probability < 1f);
        return "RESULT=IN_PROGRESS; phase=canonical-output-line-backfill; assetMutations=0\n"
            + $"recipes={recipeCount}; physicalOutputLines={rows.Count}; "
            + $"canonicalPreserved={canonical}; missingProposed={missing}\n"
            + $"main={main}; byproduct={byproduct}; returnedPackaging={packaging}; "
            + $"recoverableWaste={waste}; declaredLossPhysicalLines=0\n"
            + $"probabilisticLines={probabilistic}; probabilityValidation=PASS; "
            + "duplicateValidation=PASS; canonicalValidation=PASS\n"
            + "proposalMode=AuditOnly; appliesScriptableObjects=false; "
            + "sort=recipeId+authoredOutputOrdinal; csv=rfc4180-crlf\n"
            + "deterministicRecapture=PASS; byteIdentical=true\n"
            + $"sourceDigest={sourceDigest}\n"
            + $"inspectedAssetDigest={assetDigest}\n"
            + "nextGate=REVIEW_THEN_APPLY_APPROVED_OUTPUT_LINES\n";
    }

    private static void VerifyPureProposalScenario()
    {
        Require(InferRole(0, "container:test-vial") == ProductionOutputRole.Main,
            "A primary manufactured container was not kept as Main.");
        Require(InferRole(1, "container:test-vial")
                == ProductionOutputRole.ReturnedPackaging,
            "A non-primary container was not proposed as ReturnedPackaging.");
        Require(InferRole(1, "waste:test")
                == ProductionOutputRole.RecoverableWaste,
            "A non-primary waste item was not proposed as RecoverableWaste.");
        Require(InferRole(1, "material:test") == ProductionOutputRole.Byproduct,
            "A non-primary material was not proposed as Byproduct.");
        string first = BuildProposedOutputLineId(
            "recipe:test",
            1,
            "material:test",
            ProductionOutputRole.Byproduct);
        string second = BuildProposedOutputLineId(
            "recipe:test",
            1,
            "material:test",
            ProductionOutputRole.Byproduct);
        Require(first == "output:recipe:test/001/byproduct/material:test"
                && string.Equals(first, second, StringComparison.Ordinal),
            "Stable output-line ID projection is not deterministic.");
        Require(ProductionOutputDefinition.IsCanonicalOutputLineId(first),
            "Stable output-line ID projection violated the production grammar.");
    }

    private static string RequireStringProperty(
        SerializedProperty owner,
        string relativeName,
        string recipeId,
        int ordinal)
    {
        SerializedProperty property = owner.FindPropertyRelative(relativeName)
            ?? throw new InvalidOperationException(
                $"Recipe '{recipeId}' output[{ordinal}] is missing serialized "
                + $"property '{relativeName}'.");
        Require(property.propertyType == SerializedPropertyType.String,
            $"Recipe '{recipeId}' output[{ordinal}] property '{relativeName}' "
            + "is not a string.");
        return property.stringValue ?? string.Empty;
    }

    private static int RequireIntProperty(
        SerializedProperty owner,
        string relativeName,
        string recipeId,
        int ordinal)
    {
        SerializedProperty property = owner.FindPropertyRelative(relativeName)
            ?? throw new InvalidOperationException(
                $"Recipe '{recipeId}' output[{ordinal}] is missing serialized "
                + $"property '{relativeName}'.");
        Require(property.propertyType == SerializedPropertyType.Integer
                || property.propertyType == SerializedPropertyType.Enum,
            $"Recipe '{recipeId}' output[{ordinal}] property '{relativeName}' "
            + "is not an integer or enum.");
        return property.intValue;
    }

    private static float RequireFloatProperty(
        SerializedProperty owner,
        string relativeName,
        string recipeId,
        int ordinal)
    {
        SerializedProperty property = owner.FindPropertyRelative(relativeName)
            ?? throw new InvalidOperationException(
                $"Recipe '{recipeId}' output[{ordinal}] is missing serialized "
                + $"property '{relativeName}'.");
        Require(property.propertyType == SerializedPropertyType.Float,
            $"Recipe '{recipeId}' output[{ordinal}] property '{relativeName}' "
            + "is not a float.");
        return property.floatValue;
    }

    private static void RequireCanonicalToken(
        string value,
        string label,
        string context)
    {
        Require(!string.IsNullOrWhiteSpace(value)
                && string.Equals(value, value.Trim(), StringComparison.Ordinal)
                && value.All(character => character <= 0x7f),
            $"Non-canonical {label} in '{context}': '{value}'.");
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
            IReadOnlyList<V27CanonicalOutputLineBackfillProposalRow> rows,
            byte[] csv,
            byte[] report,
            int recipeCount,
            int canonicalCount,
            int missingCount,
            string sourceDigest,
            string inspectedAssetDigest)
        {
            Rows = rows;
            Csv = csv;
            Report = report;
            RecipeCount = recipeCount;
            CanonicalCount = canonicalCount;
            MissingCount = missingCount;
            SourceDigest = sourceDigest;
            InspectedAssetDigest = inspectedAssetDigest;
        }

        public IReadOnlyList<V27CanonicalOutputLineBackfillProposalRow> Rows { get; }
        public byte[] Csv { get; }
        public byte[] Report { get; }
        public int RecipeCount { get; }
        public int CanonicalCount { get; }
        public int MissingCount { get; }
        public string SourceDigest { get; }
        public string InspectedAssetDigest { get; }
    }
}

internal readonly struct V27CanonicalOutputLineBackfillProposalSnapshot
{
    public V27CanonicalOutputLineBackfillProposalSnapshot(
        IReadOnlyList<V27CanonicalOutputLineBackfillProposalRow> rows,
        string sourceDigest,
        string inspectedAssetDigest)
    {
        Rows = rows;
        SourceDigest = sourceDigest;
        InspectedAssetDigest = inspectedAssetDigest;
    }

    public IReadOnlyList<V27CanonicalOutputLineBackfillProposalRow> Rows { get; }
    public string SourceDigest { get; }
    public string InspectedAssetDigest { get; }
}

internal readonly struct V27CanonicalOutputLineBackfillProposalRow
{
    public V27CanonicalOutputLineBackfillProposalRow(
        string recipeId,
        int authoredOutputOrdinal,
        string itemId,
        int amount,
        float probability,
        string authoredOutputLineId,
        ProductionOutputRole authoredRole,
        string proposedOutputLineId,
        ProductionOutputRole proposedRole,
        bool hasCanonicalAuthoredLine,
        string proposalReason,
        string proposalDisposition,
        string sourceAuthority,
        string sourceDigest)
    {
        RecipeId = recipeId;
        AuthoredOutputOrdinal = authoredOutputOrdinal;
        ItemId = itemId;
        Amount = amount;
        Probability = probability;
        AuthoredOutputLineId = authoredOutputLineId;
        AuthoredRole = authoredRole;
        ProposedOutputLineId = proposedOutputLineId;
        ProposedRole = proposedRole;
        HasCanonicalAuthoredLine = hasCanonicalAuthoredLine;
        ProposalReason = proposalReason;
        ProposalDisposition = proposalDisposition;
        SourceAuthority = sourceAuthority;
        SourceDigest = sourceDigest;
    }

    public string RecipeId { get; }
    public int AuthoredOutputOrdinal { get; }
    public string ItemId { get; }
    public int Amount { get; }
    public float Probability { get; }
    public string AuthoredOutputLineId { get; }
    public ProductionOutputRole AuthoredRole { get; }
    public string ProposedOutputLineId { get; }
    public ProductionOutputRole ProposedRole { get; }
    public bool HasCanonicalAuthoredLine { get; }
    public string ProposalReason { get; }
    public string ProposalDisposition { get; }
    public string SourceAuthority { get; }
    public string SourceDigest { get; }
}
#endif
