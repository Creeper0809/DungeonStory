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

public static class V27PhysicalMassRecipeInventoryDebugScenarios
{
    public const string CsvPath =
        "Artifacts/QA/v27-recipe-mass-balance.csv";
    public const string ReportPath =
        "Artifacts/QA/v27-recipe-mass-balance-audit.txt";

    private const int ExpectedRecipes = 355;
    private const int ExpectedReviewedContracts = 42;
    private const long WaterUnitGrams = 500L;
    private const string SelfPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassRecipeInventoryDebugScenarios.cs";
    private const string SemanticPath =
        "Assets/Scripts/Services/Economy/Editor/V27PhysicalMassExplicitSemanticDebugScenarios.cs";
    private const string ContractPath =
        "Assets/Scripts/Models/Economy/Content/PhysicalMassAuthoringContracts.cs";
    private const string RecipeAuthorityPath =
        "Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs";
    private const string ProductionPrimitivePath =
        "Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs";
    private const string SerializationPath =
        "Assets/Scripts/Services/Economy/Editor/V27BalanceSerialization.cs";

    [MenuItem("DungeonStory/V27/Physical Mass/Capture All Recipe Mass Inventory")]
    public static void RunFromMenu()
    {
        CaptureResult first = Capture();
        CaptureResult second = Capture();
        Require(first.Csv.SequenceEqual(second.Csv),
            "Recipe mass CSV changed between identical captures.");
        Require(first.Report.SequenceEqual(second.Report),
            "Recipe mass report changed between identical captures.");
        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
            stream.Write(first.Csv, 0, first.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));
        Debug.Log(
            "V27 recipe mass inventory captured: "
            + $"recipes={first.RecipeCount}; reviewed={first.ReviewedCount}; "
            + $"missing={first.MissingDispositionCount}; "
            + $"massCreation={first.MassCreationCount}; status=IN_PROGRESS.");
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
        Dictionary<string, ItemDefinitionSO> items = UniqueIndex(
            itemCatalog.Definitions.Where(value => value != null),
            value => value.ItemId,
            "item");
        Dictionary<string, CanonicalItemUnitSemantic> semantics = UniqueIndex(
            V27PhysicalMassExplicitSemanticDebugScenarios
                .CaptureCanonicalUnitSemanticsForAudit(),
            value => value.ItemId,
            "unit semantic");
        ProductionRecipeSO[] recipes = domain.GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(recipes.Length == ExpectedRecipes,
            $"Expected {ExpectedRecipes} recipes, found {recipes.Length}.");
        Require(recipes.Select(value => value.RecipeId)
                .Distinct(StringComparer.Ordinal).Count() == recipes.Length,
            "Recipe mass inventory contains duplicate recipe IDs.");

        PhysicalMassTransformContract[] reviewed =
            V27PhysicalMassExplicitSemanticDebugScenarios
                .CaptureReviewedTransformContractsForAudit()
                .OrderBy(value => value.TransformId, StringComparer.Ordinal)
                .ToArray();
        Require(reviewed.Length == ExpectedReviewedContracts,
            $"Expected {ExpectedReviewedContracts} reviewed transforms, "
            + $"found {reviewed.Length}.");
        Dictionary<string, PhysicalMassTransformContract> reviewedById =
            UniqueIndex(reviewed, value => value.TransformId, "reviewed transform");
        Require(reviewedById.Keys.All(id => recipes.Any(recipe => string.Equals(
                recipe.RecipeId,
                id,
                StringComparison.Ordinal))),
            "A reviewed transform is absent from the current recipe catalog.");

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string[] inspectedPaths = items.Values
            .Select(AssetDatabase.GetAssetPath)
            .Concat(recipes.Select(AssetDatabase.GetAssetPath))
            .Append(SelfPath)
            .Append(SemanticPath)
            .Append(ContractPath)
            .Append(RecipeAuthorityPath)
            .Append(ProductionPrimitivePath)
            .Append(SerializationPath)
            .Select(CanonicalPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string beforeAssetDigest = ComputeAggregateDigest(projectRoot, inspectedPaths);

        List<RecipeMassRow> rows = new(recipes.Length);
        foreach (ProductionRecipeSO recipe in recipes)
            rows.Add(CaptureRow(recipe, items, semantics, reviewedById));

        string afterAssetDigest = ComputeAggregateDigest(projectRoot, inspectedPaths);
        Require(string.Equals(beforeAssetDigest, afterAssetDigest, StringComparison.Ordinal),
            "AuditOnly recipe mass inventory mutated an inspected asset.");

        int sourceCount = rows.Count(value => value.FlowRole == ProductionFlowRole.Source);
        int sinkCount = rows.Count(value => value.FlowRole == ProductionFlowRole.Sink);
        int transformCount = rows.Count(value => value.FlowRole == ProductionFlowRole.Transform);
        int reviewedCount = rows.Count(value => value.Status == "reviewed-exact");
        int missingCount = rows.Count(value =>
            value.Status == "disposition-contract-missing");
        int creationCount = rows.Count(value => value.Status == "mass-creation-critical");
        int roleMismatchCount = rows.Count(value => value.RoleShapeValid == "false");
        int probabilisticCount = rows.Count(value => value.ProbabilisticOutputCount > 0);
        int missingSemanticRecipeCount = rows.Count(value =>
            value.MissingSemanticIds.Length > 0);
        int massCreationCandidateCount = rows.Count(value => value.MassCreationCandidate);

        byte[] csv = BuildCsv(rows);
        string report = "RESULT=IN_PROGRESS; phase=recipe-mass-inventory; "
            + "assetMutations=0\n"
            + $"recipes={rows.Count}; sources={sourceCount}; transforms={transformCount}; "
            + $"sinks={sinkCount}; roleShapeMismatch={roleMismatchCount}\n"
            + $"reviewedExact={reviewedCount}; dispositionMissing={missingCount}; "
            + $"massCreationCritical={creationCount}; "
            + $"massCreationCandidates={massCreationCandidateCount}; "
            + $"missingSemanticRecipes={missingSemanticRecipeCount}; "
            + $"probabilisticRecipes={probabilisticCount}\n"
            + "minimumBranchUsesGuaranteedOutputs=true; "
            + "maximumBranchUsesAllPositiveProbabilityOutputs=true; "
            + "expectedOutputUsesDecimalProbability=true\n"
            + "sourceAndSinkExternalMassExcludedFromTransformConservation=true\n"
            + "deterministicRecapture=PASS; byteIdentical=true\n"
            + $"sourceDigest={beforeAssetDigest}\n"
            + "nextGate=REVIEW_EVERY_DISPOSITION_CONTRACT; status=IN_PROGRESS\n";
        return new CaptureResult(
            csv,
            Encoding.UTF8.GetBytes(report),
            rows.Count,
            reviewedCount,
            missingCount,
            creationCount);
    }

    private static RecipeMassRow CaptureRow(
        ProductionRecipeSO recipe,
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        IReadOnlyDictionary<string, CanonicalItemUnitSemantic> semantics,
        IReadOnlyDictionary<string, PhysicalMassTransformContract> reviewed)
    {
        Require(!string.IsNullOrWhiteSpace(recipe.RecipeId)
                && string.Equals(recipe.RecipeId, recipe.RecipeId.Trim(), StringComparison.Ordinal),
            "Recipe mass inventory found a non-canonical recipe ID.");
        string[] missingSemanticIds = recipe.Inputs
            .Select(value => value.ItemId)
            .Concat(recipe.Outputs.Select(value => value.ItemId))
            .Where(value => !semantics.ContainsKey(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        long physicalInput = 0L;
        foreach (ItemAmountDefinition input in recipe.Inputs)
        {
            Require(input != null && input.HasCanonicalAuthoredValue,
                $"Recipe has an invalid input: {recipe.RecipeId}.");
            physicalInput = checked(
                physicalInput + checked(UnitGrams(
                    items,
                    semantics,
                    input.ItemId) * input.Amount));
        }
        long cleanWater = ScaleFluid(recipe.CleanWaterPerCycle);
        long wastewater = ScaleFluid(recipe.WastewaterPerCycle);
        long guaranteedOutput = 0L;
        long maximumOutput = 0L;
        decimal expectedOutput = 0m;
        int probabilisticOutputCount = 0;
        foreach (ProductionOutputDefinition output in recipe.Outputs)
        {
            Require(output != null && !string.IsNullOrWhiteSpace(output.ItemId),
                $"Recipe has an invalid output: {recipe.RecipeId}.");
            long lineMass = checked(UnitGrams(
                items,
                semantics,
                output.ItemId) * output.Amount);
            if (output.Probability > 0f)
                maximumOutput = checked(maximumOutput + lineMass);
            if (Mathf.Approximately(output.Probability, 1f))
                guaranteedOutput = checked(guaranteedOutput + lineMass);
            else if (output.Probability > 0f)
                probabilisticOutputCount++;
            expectedOutput += lineMass * (decimal)output.Probability;
        }

        long available = checked(physicalInput + cleanWater);
        long minimumDisposition = checked(guaranteedOutput + wastewater);
        long maximumDisposition = checked(maximumOutput + wastewater);
        long minimumResidual = checked(available - maximumDisposition);
        long maximumResidual = checked(available - minimumDisposition);
        bool massCreationCandidate = minimumResidual < 0L;
        bool roleShapeValid = recipe.FlowRole switch
        {
            ProductionFlowRole.Source => recipe.Inputs.Count == 0
                && recipe.Outputs.Count > 0,
            ProductionFlowRole.Sink => recipe.Inputs.Count > 0
                && recipe.Outputs.Count == 0,
            ProductionFlowRole.Transform => recipe.Inputs.Count > 0
                && recipe.Outputs.Count > 0,
            _ => false
        };

        string status;
        string reviewedContractId = string.Empty;
        if (!roleShapeValid)
        {
            status = "flow-role-shape-critical";
        }
        else if (recipe.FlowRole == ProductionFlowRole.Source)
        {
            status = "source-external-mass";
        }
        else if (recipe.FlowRole == ProductionFlowRole.Sink)
        {
            status = "sink-explicit-mass";
        }
        else if (missingSemanticIds.Length > 0)
        {
            status = "unit-semantic-missing";
        }
        else if (reviewed.TryGetValue(
                     recipe.RecipeId,
                     out PhysicalMassTransformContract contract))
        {
            Require(contract.PhysicalInputGrams == physicalInput
                    && contract.InfrastructureInputGrams == cleanWater
                    && contract.PhysicalOutputGrams == maximumOutput
                    && contract.ByproductGrams >= wastewater,
                $"Reviewed mass contract drifted from recipe authority: {recipe.RecipeId}.");
            status = "reviewed-exact";
            reviewedContractId = contract.TransformId;
        }
        else if (massCreationCandidate)
        {
            status = "mass-creation-critical";
        }
        else
        {
            status = "disposition-contract-missing";
        }

        string sourcePath = CanonicalPath(AssetDatabase.GetAssetPath(recipe));
        return new RecipeMassRow(
            recipe.RecipeId,
            recipe.FlowRole,
            roleShapeValid ? "true" : "false",
            FormatInputs(recipe.Inputs),
            FormatOutputs(recipe.Outputs),
            physicalInput,
            cleanWater,
            guaranteedOutput,
            maximumOutput,
            expectedOutput,
            wastewater,
            minimumResidual,
            maximumResidual,
            probabilisticOutputCount,
            string.Join("|", missingSemanticIds),
            massCreationCandidate,
            reviewedContractId,
            status,
            sourcePath,
            ComputeFileDigest(sourcePath));
    }

    private static byte[] BuildCsv(IReadOnlyList<RecipeMassRow> rows)
    {
        using MemoryStream stream = new();
        V27Utf8CsvWriter writer = new(stream, 16384);
        WriteRow(writer, new[]
        {
            "schemaVersion", "recipeId", "flowRole", "roleShapeValid",
            "inputVector", "outputVector", "physicalInputGrams",
            "cleanWaterGrams", "guaranteedOutputGrams", "maximumOutputGrams",
            "expectedOutputGrams", "wastewaterGrams", "minimumResidualGrams",
            "maximumResidualGrams", "probabilisticOutputCount",
            "missingSemanticIds", "massCreationCandidate",
            "reviewedContractId", "massBalanceStatus", "sourceAuthority",
            "sourceDigest"
        });
        foreach (RecipeMassRow row in rows)
        {
            WriteRow(writer, new[]
            {
                "v27.mass.recipe-inventory.1",
                row.RecipeId,
                row.FlowRole.ToString(),
                row.RoleShapeValid,
                row.InputVector,
                row.OutputVector,
                Token(row.PhysicalInputGrams),
                Token(row.CleanWaterGrams),
                Token(row.GuaranteedOutputGrams),
                Token(row.MaximumOutputGrams),
                row.ExpectedOutputGrams.ToString(
                    "0.############################",
                    CultureInfo.InvariantCulture),
                Token(row.WastewaterGrams),
                Token(row.MinimumResidualGrams),
                Token(row.MaximumResidualGrams),
                Token(row.ProbabilisticOutputCount),
                row.MissingSemanticIds,
                row.MassCreationCandidate ? "true" : "false",
                row.ReviewedContractId,
                row.Status,
                row.SourceAuthority,
                row.SourceDigest
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static string FormatInputs(IEnumerable<ItemAmountDefinition> inputs) =>
        string.Join("|", inputs
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ThenBy(value => value.Amount)
            .Select(value => value.ItemId + "*" + Token(value.Amount)));

    private static string FormatOutputs(IEnumerable<ProductionOutputDefinition> outputs) =>
        string.Join("|", outputs
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ThenBy(value => value.Amount)
            .ThenBy(value => value.Probability)
            .Select(value => value.ItemId + "*" + Token(value.Amount)
                + "@" + value.Probability.ToString("R", CultureInfo.InvariantCulture)));

    private static long UnitGrams(
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        IReadOnlyDictionary<string, CanonicalItemUnitSemantic> semantics,
        string itemId)
    {
        if (semantics.TryGetValue(
                itemId,
                out CanonicalItemUnitSemantic semantic))
        {
            return semantic.CanonicalUnitMass.Value;
        }
        Require(items.TryGetValue(itemId, out ItemDefinitionSO item),
            $"Recipe mass item is missing: {itemId}.");
        return PhysicalMassGrams.FromCanonicalKilograms(item.UnitWeight).Value;
    }

    private static long ScaleFluid(float units)
    {
        Require(float.IsFinite(units) && units >= 0f,
            $"Invalid recipe fluid amount: {units:R}.");
        decimal exact = WaterUnitGrams * (decimal)units;
        Require(exact == decimal.Truncate(exact),
            $"Recipe fluid amount is not an exact gram quantity: {exact}.");
        return checked((long)exact);
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
        return Hex(sha.Hash);
    }

    private static string ComputeFileDigest(string projectRelativePath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(File.ReadAllBytes(Path.Combine(
            root,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar)))));
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

    private sealed class RecipeMassRow
    {
        public RecipeMassRow(
            string recipeId,
            ProductionFlowRole flowRole,
            string roleShapeValid,
            string inputVector,
            string outputVector,
            long physicalInputGrams,
            long cleanWaterGrams,
            long guaranteedOutputGrams,
            long maximumOutputGrams,
            decimal expectedOutputGrams,
            long wastewaterGrams,
            long minimumResidualGrams,
            long maximumResidualGrams,
            int probabilisticOutputCount,
            string missingSemanticIds,
            bool massCreationCandidate,
            string reviewedContractId,
            string status,
            string sourceAuthority,
            string sourceDigest)
        {
            RecipeId = recipeId;
            FlowRole = flowRole;
            RoleShapeValid = roleShapeValid;
            InputVector = inputVector;
            OutputVector = outputVector;
            PhysicalInputGrams = physicalInputGrams;
            CleanWaterGrams = cleanWaterGrams;
            GuaranteedOutputGrams = guaranteedOutputGrams;
            MaximumOutputGrams = maximumOutputGrams;
            ExpectedOutputGrams = expectedOutputGrams;
            WastewaterGrams = wastewaterGrams;
            MinimumResidualGrams = minimumResidualGrams;
            MaximumResidualGrams = maximumResidualGrams;
            ProbabilisticOutputCount = probabilisticOutputCount;
            MissingSemanticIds = missingSemanticIds;
            MassCreationCandidate = massCreationCandidate;
            ReviewedContractId = reviewedContractId;
            Status = status;
            SourceAuthority = sourceAuthority;
            SourceDigest = sourceDigest;
        }

        public string RecipeId { get; }
        public ProductionFlowRole FlowRole { get; }
        public string RoleShapeValid { get; }
        public string InputVector { get; }
        public string OutputVector { get; }
        public long PhysicalInputGrams { get; }
        public long CleanWaterGrams { get; }
        public long GuaranteedOutputGrams { get; }
        public long MaximumOutputGrams { get; }
        public decimal ExpectedOutputGrams { get; }
        public long WastewaterGrams { get; }
        public long MinimumResidualGrams { get; }
        public long MaximumResidualGrams { get; }
        public int ProbabilisticOutputCount { get; }
        public string MissingSemanticIds { get; }
        public bool MassCreationCandidate { get; }
        public string ReviewedContractId { get; }
        public string Status { get; }
        public string SourceAuthority { get; }
        public string SourceDigest { get; }
    }

    private sealed class CaptureResult
    {
        public CaptureResult(
            byte[] csv,
            byte[] report,
            int recipeCount,
            int reviewedCount,
            int missingDispositionCount,
            int massCreationCount)
        {
            Csv = csv;
            Report = report;
            RecipeCount = recipeCount;
            ReviewedCount = reviewedCount;
            MissingDispositionCount = missingDispositionCount;
            MassCreationCount = massCreationCount;
        }

        public byte[] Csv { get; }
        public byte[] Report { get; }
        public int RecipeCount { get; }
        public int ReviewedCount { get; }
        public int MissingDispositionCount { get; }
        public int MassCreationCount { get; }
    }
}
#endif
