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
    private const string MassExplanationAuthoringPath =
        "Assets/Scripts/Models/Economy/Content/ProductionMassExplanationAuthoring.cs";
    private const string MassExplanationRegistryPath =
        "Assets/Scripts/Services/Economy/ProductionMassExplanationCapabilityRegistry.cs";
    private const string ProductionPrimitivePath =
        "Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs";
    private const string SerializationPath =
        "Assets/Scripts/Services/Economy/Editor/V27BalanceSerialization.cs";

    [MenuItem("DungeonStory/V27/Physical Mass/Capture All Recipe Mass Inventory")]
    public static void RunFromMenu()
    {
        VerifyExplanatoryClosureContract();
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
            + $"explanationMissing={first.MissingDispositionCount}; "
            + $"externalInputAuthorityMissing={first.MassCreationCount}; "
            + "status=PASS.");
    }

    private static void VerifyExplanatoryClosureContract()
    {
        PhysicalMassTransformContract externalInput = new(
            "recipe:qa:external-input",
            800L,
            0L,
            200L,
            PhysicalMassExternalInputKind.ProcessWater,
            1_000L,
            0L,
            0L,
            PhysicalMassTerminalSinkKind.None,
            0L,
            PhysicalMassLossKind.None,
            "QA verifies that declared external input can close a transform without hidden mass creation.");
        Require(externalInput.TotalInputGrams == 1_000L
                && externalInput.TotalDispositionGrams == 1_000L,
            "Declared external-input mass did not close exactly.");

        PhysicalMassTransformContract abstractLoss = new(
            "recipe:qa:abstract-loss",
            1_000L,
            0L,
            0L,
            PhysicalMassExternalInputKind.None,
            700L,
            0L,
            0L,
            PhysicalMassTerminalSinkKind.None,
            300L,
            PhysicalMassLossKind.MoistureEvaporation,
            "QA verifies that an explicit non-item loss can close a transform without spawning a byproduct.");
        Require(abstractLoss.TotalInputGrams == 1_000L
                && abstractLoss.TotalDispositionGrams == 1_000L,
            "Declared abstract-loss mass did not close exactly.");

        bool untypedExternalRejected = false;
        try
        {
            _ = new PhysicalMassTransformContract(
                "recipe:qa:untyped-external",
                800L,
                0L,
                200L,
                PhysicalMassExternalInputKind.None,
                1_000L,
                0L,
                0L,
                PhysicalMassTerminalSinkKind.None,
                0L,
                PhysicalMassLossKind.None,
                "QA invalid untyped external input.");
        }
        catch (ArgumentException)
        {
            untypedExternalRejected = true;
        }
        Require(untypedExternalRejected,
            "Positive external input without a typed authority was accepted.");
    }

    /// <summary>
    /// Captures current recipe mass-disposition states directly from authoring
    /// authority. Downstream proposal generators must not trust an older CSV.
    /// </summary>
    internal static IReadOnlyDictionary<string, string>
        CaptureMassBalanceStatusesForAudit()
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
        Dictionary<string, PhysicalMassTransformContract> reviewed = UniqueIndex(
            V27PhysicalMassExplicitSemanticDebugScenarios
                .CaptureReviewedTransformContractsForAudit(),
            value => value.TransformId,
            "reviewed transform");
        ProductionRecipeSO[] recipes = domain.GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(recipes.Select(value => value.RecipeId)
                .Distinct(StringComparer.Ordinal).Count() == recipes.Length,
            "Recipe mass audit status contains duplicate recipe IDs.");
        return recipes.ToDictionary(
            recipe => recipe.RecipeId,
            recipe => CaptureRow(recipe, items, semantics, reviewed).Status,
            StringComparer.Ordinal);
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
        Require(recipes.Length > 0,
            "Dynamic recipe catalog scope is empty.");
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
            .Append(MassExplanationAuthoringPath)
            .Append(MassExplanationRegistryPath)
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
        int balancedExactCount = rows.Count(value =>
            value.Status == "balanced-exact");
        int runtimeBalancedProposalMismatchCount = rows.Count(value =>
            value.Status == "runtime-balanced-proposal-mismatch");
        int proposedOnlyReviewedDriftCount = rows.Count(value =>
            value.Status == "reviewed-proposed-runtime-mismatch");
        int missingCount = rows.Count(value =>
            value.Status == "mass-balance-explanation-missing");
        int creationCount = rows.Count(value =>
            value.Status == "external-input-authority-missing");
        int roleMismatchCount = rows.Count(value => value.RoleShapeValid == "false");
        int probabilisticCount = rows.Count(value => value.ProbabilisticOutputCount > 0);
        int missingSemanticRecipeCount = rows.Count(value =>
            value.MissingSemanticIds.Length > 0);
        int massCreationCandidateCount = rows.Count(value => value.MassCreationCandidate);
        int runtimeProposedMassMismatchRecipeCount = rows.Count(value =>
            value.RuntimeProposedMassMismatchIds.Length > 0);
        int runtimeMassCreationCandidateCount = rows.Count(value =>
            value.RuntimeMassCreationCandidate);
        int closedRecipeCount = sourceCount + sinkCount + reviewedCount
            + balancedExactCount + runtimeBalancedProposalMismatchCount;
        bool passed = rows.Count == recipes.Length
            && rows.Select(value => value.RecipeId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    recipes.Select(value => value.RecipeId),
                    StringComparer.Ordinal)
            && roleMismatchCount == 0
            && missingCount == 0
            && creationCount == 0
            && missingSemanticRecipeCount == 0
            && runtimeProposedMassMismatchRecipeCount == 0
            && proposedOnlyReviewedDriftCount == 0
            && closedRecipeCount == rows.Count;
        Require(passed,
            "V27 recipe mass inventory remains incomplete: "
            + $"recipes={rows.Count}/{recipes.Length}; closed={closedRecipeCount}; "
            + $"roleMismatch={roleMismatchCount}; explanationMissing={missingCount}; "
            + $"externalInputAuthorityMissing={creationCount}; "
            + $"missingSemantic={missingSemanticRecipeCount}; "
            + $"runtimeProposalMismatch={runtimeProposedMassMismatchRecipeCount}; "
            + $"reviewedProposalDrift={proposedOnlyReviewedDriftCount}.");

        byte[] csv = BuildCsv(rows);
        string report = "RESULT=PASS; phase=recipe-mass-inventory; "
            + "assetMutations=0\n"
            + $"recipes={rows.Count}; sources={sourceCount}; transforms={transformCount}; "
            + $"sinks={sinkCount}; roleShapeMismatch={roleMismatchCount}\n"
            + $"reviewedExact={reviewedCount}; balancedExact={balancedExactCount}; "
            + $"runtimeBalancedProposalMismatch={runtimeBalancedProposalMismatchCount}; "
            + $"closedTransformRecipes={reviewedCount + balancedExactCount + runtimeBalancedProposalMismatchCount}; "
            + $"closedRecipes={closedRecipeCount}; "
            + $"explanationMissing={missingCount}; "
            + $"reviewedProposedRuntimeMismatch={proposedOnlyReviewedDriftCount}; "
            + $"externalInputAuthorityMissing={creationCount}; "
            + $"massCreationCandidates={massCreationCandidateCount}; "
            + $"runtimeMassCreationCandidates={runtimeMassCreationCandidateCount}; "
            + $"runtimeProposedMassMismatchRecipes={runtimeProposedMassMismatchRecipeCount}; "
            + $"missingSemanticRecipes={missingSemanticRecipeCount}; "
            + $"probabilisticRecipes={probabilisticCount}\n"
            + "minimumBranchUsesGuaranteedOutputs=true; "
            + "maximumBranchUsesAllPositiveProbabilityOutputs=true; "
            + "expectedOutputUsesDecimalProbability=true\n"
            + "sourceAndSinkExternalMassExcludedFromTransformConservation=true\n"
            + "transformMassDeltaRequiresDeclaredExternalInputOrDisposition=true\n"
            + "deterministicRecapture=PASS; byteIdentical=true\n"
            + $"sourceDigest={beforeAssetDigest}\n"
            + "nextGate=COMPLETE; status=PASS\n";
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
        string[] runtimeProposedMassMismatchIds = recipe.Inputs
            .Select(value => value.ItemId)
            .Concat(recipe.Outputs.Select(value => value.ItemId))
            .Distinct(StringComparer.Ordinal)
            .Where(itemId => semantics.TryGetValue(
                    itemId,
                    out CanonicalItemUnitSemantic semantic)
                && RuntimeUnitGrams(items, itemId) !=
                    semantic.CanonicalUnitMass.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        long physicalInput = 0L;
        long runtimePhysicalInput = 0L;
        foreach (ItemAmountDefinition input in recipe.Inputs)
        {
            Require(input != null && input.HasCanonicalAuthoredValue,
                $"Recipe has an invalid input: {recipe.RecipeId}.");
            physicalInput = checked(
                physicalInput + checked(UnitGrams(
                    items,
                    semantics,
                    input.ItemId) * input.Amount));
            runtimePhysicalInput = checked(
                runtimePhysicalInput + checked(RuntimeUnitGrams(
                    items,
                    input.ItemId) * input.Amount));
        }
        long cleanWater = ScaleFluid(recipe.CleanWaterPerCycle);
        long wastewater = ScaleFluid(recipe.WastewaterPerCycle);
        long guaranteedOutput = 0L;
        long maximumOutput = 0L;
        decimal expectedOutput = 0m;
        long runtimeGuaranteedOutput = 0L;
        long runtimeMaximumOutput = 0L;
        decimal runtimeExpectedOutput = 0m;
        int probabilisticOutputCount = 0;
        foreach (ProductionOutputDefinition output in recipe.Outputs)
        {
            Require(output != null && !string.IsNullOrWhiteSpace(output.ItemId),
                $"Recipe has an invalid output: {recipe.RecipeId}.");
            long lineMass = checked(UnitGrams(
                items,
                semantics,
                output.ItemId) * output.Amount);
            long runtimeLineMass = checked(RuntimeUnitGrams(
                items,
                output.ItemId) * output.Amount);
            if (output.Probability > 0f)
            {
                maximumOutput = checked(maximumOutput + lineMass);
                runtimeMaximumOutput = checked(
                    runtimeMaximumOutput + runtimeLineMass);
            }
            if (Mathf.Approximately(output.Probability, 1f))
            {
                guaranteedOutput = checked(guaranteedOutput + lineMass);
                runtimeGuaranteedOutput = checked(
                    runtimeGuaranteedOutput + runtimeLineMass);
            }
            else if (output.Probability > 0f)
                probabilisticOutputCount++;
            expectedOutput += lineMass * (decimal)output.Probability;
            runtimeExpectedOutput +=
                runtimeLineMass * (decimal)output.Probability;
        }

        long available = checked(physicalInput + cleanWater);
        long minimumDisposition = checked(guaranteedOutput + wastewater);
        long maximumDisposition = checked(maximumOutput + wastewater);
        long minimumResidual = checked(available - maximumDisposition);
        long maximumResidual = checked(available - minimumDisposition);
        bool massCreationCandidate = minimumResidual < 0L;
        long runtimeAvailable = checked(runtimePhysicalInput + cleanWater);
        long runtimeMinimumResidual = checked(
            runtimeAvailable - checked(runtimeMaximumOutput + wastewater));
        long runtimeMaximumResidual = checked(
            runtimeAvailable - checked(runtimeGuaranteedOutput + wastewater));
        bool runtimeMassCreationCandidate = runtimeMinimumResidual < 0L;
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
        else if (!recipe.MassExplanation.IsEmpty)
        {
            Require(probabilisticOutputCount == 0,
                $"Residual process-loss capability requires deterministic output: {recipe.RecipeId}.");
            reviewed.TryGetValue(
                recipe.RecipeId,
                out PhysicalMassTransformContract auditExpectation);
            bool hasAuditExpectation = !string.IsNullOrEmpty(
                auditExpectation.TransformId);
            if (hasAuditExpectation)
            {
                // Explicit-semantic transforms are immutable audit fixtures, not
                // a second gameplay writer. The recipe capability remains the
                // only authored/runtime authority and must agree with the fixture
                // exactly before the fixture may corroborate it.
                Require(
                    auditExpectation.PhysicalInputGrams == physicalInput
                    && auditExpectation.InfrastructureInputGrams == cleanWater
                    && auditExpectation.DeclaredExternalInputGrams == 0L
                    && auditExpectation.PhysicalOutputGrams == maximumOutput
                    && auditExpectation.ByproductGrams == wastewater
                    && auditExpectation.TerminalSinkGrams == 0L
                    && minimumResidual == maximumResidual
                    && auditExpectation.DeclaredLossGrams == minimumResidual,
                    $"Recipe capability drifted from its audit-only mass expectation: {recipe.RecipeId}.");
            }
            ProductionMassExplanationDisposition disposition;
            ProductionMassExplanationDisposition runtimeDisposition;
            try
            {
                disposition = ProductionMassExplanationCapabilityRegistry
                    .CreateDefault()
                    .Resolve(
                        recipe.MassExplanation,
                        new ProductionMassExplanationEquationSubject(
                            recipe.RecipeId,
                            physicalInput,
                            0L,
                            cleanWater,
                            maximumOutput,
                            wastewater,
                            0L));
                runtimeDisposition = ProductionMassExplanationCapabilityRegistry
                    .CreateDefault()
                    .Resolve(
                        recipe.MassExplanation,
                        new ProductionMassExplanationEquationSubject(
                            recipe.RecipeId,
                            runtimePhysicalInput,
                            0L,
                            cleanWater,
                            runtimeMaximumOutput,
                            wastewater,
                            0L));
            }
            catch (Exception exception) when (
                exception is ArgumentException
                    or InvalidOperationException
                    or OverflowException)
            {
                throw new InvalidOperationException(
                    "Recipe mass capability resolution failed: "
                    + recipe.RecipeId,
                    exception);
            }
            Require(disposition.HasDisposition
                    && minimumResidual == maximumResidual
                    && ClosesResidual(disposition, minimumResidual)
                    && runtimeDisposition.HasDisposition
                    && runtimeMinimumResidual == runtimeMaximumResidual
                    && ClosesResidual(
                        runtimeDisposition,
                        runtimeMinimumResidual),
                $"Recipe mass capability did not explain the exact residual: {recipe.RecipeId}.");
            if (hasAuditExpectation)
            {
                Require(
                    auditExpectation.LossKind == disposition.LossKind
                    && auditExpectation.DeclaredLossGrams
                        == disposition.DeclaredLossGrams,
                    $"Recipe capability disposition drifted from its audit-only mass expectation: {recipe.RecipeId}.");
            }
            status = "reviewed-exact";
            reviewedContractId = recipe.MassExplanation.CapabilityId
                + "@"
                + recipe.MassExplanation.ContractVersion.ToString(
                    CultureInfo.InvariantCulture);
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
            status = runtimeProposedMassMismatchIds.Length == 0
                ? "reviewed-exact"
                : "reviewed-proposed-runtime-mismatch";
            reviewedContractId = contract.TransformId;
        }
        else if (probabilisticOutputCount == 0
                 && runtimeMinimumResidual == 0L
                 && runtimeMaximumResidual == 0L)
        {
            bool proposalAlsoBalanced = minimumResidual == 0L
                && maximumResidual == 0L;
            status = proposalAlsoBalanced
                ? "balanced-exact"
                : "runtime-balanced-proposal-mismatch";
            reviewedContractId = proposalAlsoBalanced
                ? "balanced-exact@1"
                : "runtime-balanced-proposal-mismatch@1";
        }
        else if (massCreationCandidate)
        {
            status = "external-input-authority-missing";
        }
        else
        {
            status = "mass-balance-explanation-missing";
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
            runtimePhysicalInput,
            runtimeGuaranteedOutput,
            runtimeMaximumOutput,
            runtimeExpectedOutput,
            runtimeMinimumResidual,
            runtimeMaximumResidual,
            probabilisticOutputCount,
            string.Join("|", missingSemanticIds),
            string.Join("|", runtimeProposedMassMismatchIds),
            massCreationCandidate,
            runtimeMassCreationCandidate,
            reviewedContractId,
            status,
            sourcePath,
            ComputeFileDigest(sourcePath));
    }

    private static bool ClosesResidual(
        ProductionMassExplanationDisposition disposition,
        long residual)
    {
        if (residual > 0L)
        {
            return disposition.DeclaredLossGrams == residual
                && disposition.LossKind != PhysicalMassLossKind.None
                && disposition.DeclaredExternalInputGrams == 0L
                && disposition.ExternalInputKind
                    == PhysicalMassExternalInputKind.None;
        }
        if (residual < 0L)
        {
            return disposition.DeclaredLossGrams == 0L
                && disposition.LossKind == PhysicalMassLossKind.None
                && disposition.DeclaredExternalInputGrams == -residual
                && disposition.ExternalInputKind
                    != PhysicalMassExternalInputKind.None;
        }
        return !disposition.HasDisposition;
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
            "maximumResidualGrams",
            "runtimePhysicalInputGrams", "runtimeGuaranteedOutputGrams",
            "runtimeMaximumOutputGrams", "runtimeExpectedOutputGrams",
            "runtimeMinimumResidualGrams", "runtimeMaximumResidualGrams",
            "probabilisticOutputCount",
            "missingSemanticIds", "runtimeProposedMassMismatchIds",
            "massCreationCandidate", "runtimeMassCreationCandidate",
            "reviewedContractId", "massBalanceStatus", "sourceAuthority",
            "sourceDigest"
        });
        foreach (RecipeMassRow row in rows)
        {
            WriteRow(writer, new[]
            {
                "v27.mass.recipe-inventory.3",
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
                Token(row.RuntimePhysicalInputGrams),
                Token(row.RuntimeGuaranteedOutputGrams),
                Token(row.RuntimeMaximumOutputGrams),
                row.RuntimeExpectedOutputGrams.ToString(
                    "0.############################",
                    CultureInfo.InvariantCulture),
                Token(row.RuntimeMinimumResidualGrams),
                Token(row.RuntimeMaximumResidualGrams),
                Token(row.ProbabilisticOutputCount),
                row.MissingSemanticIds,
                row.RuntimeProposedMassMismatchIds,
                row.MassCreationCandidate ? "true" : "false",
                row.RuntimeMassCreationCandidate ? "true" : "false",
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

    private static long RuntimeUnitGrams(
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        string itemId)
    {
        Require(items.TryGetValue(itemId, out ItemDefinitionSO item),
            $"Recipe runtime mass item is missing: {itemId}.");
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
            long runtimePhysicalInputGrams,
            long runtimeGuaranteedOutputGrams,
            long runtimeMaximumOutputGrams,
            decimal runtimeExpectedOutputGrams,
            long runtimeMinimumResidualGrams,
            long runtimeMaximumResidualGrams,
            int probabilisticOutputCount,
            string missingSemanticIds,
            string runtimeProposedMassMismatchIds,
            bool massCreationCandidate,
            bool runtimeMassCreationCandidate,
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
            RuntimePhysicalInputGrams = runtimePhysicalInputGrams;
            RuntimeGuaranteedOutputGrams = runtimeGuaranteedOutputGrams;
            RuntimeMaximumOutputGrams = runtimeMaximumOutputGrams;
            RuntimeExpectedOutputGrams = runtimeExpectedOutputGrams;
            RuntimeMinimumResidualGrams = runtimeMinimumResidualGrams;
            RuntimeMaximumResidualGrams = runtimeMaximumResidualGrams;
            ProbabilisticOutputCount = probabilisticOutputCount;
            MissingSemanticIds = missingSemanticIds;
            RuntimeProposedMassMismatchIds = runtimeProposedMassMismatchIds;
            MassCreationCandidate = massCreationCandidate;
            RuntimeMassCreationCandidate = runtimeMassCreationCandidate;
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
        public long RuntimePhysicalInputGrams { get; }
        public long RuntimeGuaranteedOutputGrams { get; }
        public long RuntimeMaximumOutputGrams { get; }
        public decimal RuntimeExpectedOutputGrams { get; }
        public long RuntimeMinimumResidualGrams { get; }
        public long RuntimeMaximumResidualGrams { get; }
        public int ProbabilisticOutputCount { get; }
        public string MissingSemanticIds { get; }
        public string RuntimeProposedMassMismatchIds { get; }
        public bool MassCreationCandidate { get; }
        public bool RuntimeMassCreationCandidate { get; }
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
