using System;
using System.Collections.Generic;
using System.Linq;

public enum ProductionOutputBranchDisposition
{
    ProductiveCycle = 1,
    TerminalFault = 2
}

/// <summary>
/// One exact physical branch for one passive recipe/support assignment. The
/// normal branch is a repeatable productive cycle; the ruined branch is a
/// terminal fault and is published only to capacity and conservation audits.
/// </summary>
public sealed class ProductionPhysicalOutputBranchMaximumSnapshot
{
    internal ProductionPhysicalOutputBranchMaximumSnapshot(
        string recipeId,
        string branchId,
        string supportAssignmentSourceDigest,
        ProductionOutputBranchDisposition disposition,
        IReadOnlyList<ProductionOutputMaximumMassProjection> projections,
        long wipInputMassGrams,
        long processCleanWaterMassGrams,
        long processWastewaterMassGrams,
        long declaredLossMassGrams,
        bool deterministicOutcome,
        string sourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            recipeId,
            nameof(recipeId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            supportAssignmentSourceDigest,
            nameof(supportAssignmentSourceDigest));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        if (!Enum.IsDefined(typeof(ProductionOutputBranchDisposition), disposition)
            || wipInputMassGrams <= 0L
            || processCleanWaterMassGrams < 0L
            || processWastewaterMassGrams < 0L
            || declaredLossMassGrams < 0L)
        {
            throw new ArgumentException(
                "Passive production output branch metadata is invalid.");
        }

        ProductionOutputMaximumMassProjection[] ordered = (projections
                ?? throw new ArgumentNullException(nameof(projections)))
            .OrderBy(value => value.Descriptor.OutputLineId,
                StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Select(value => value.Descriptor.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || ordered.Select(value => value.MassAuthorityRevision)
                .Distinct().Count() != 1)
        {
            throw new InvalidOperationException(
                "Passive production output branch projections are empty, duplicated, or revision-mixed.");
        }

        long totalPhysicalMassGrams = 0L;
        foreach (ProductionOutputMaximumMassProjection projection in ordered)
            totalPhysicalMassGrams = checked(
                totalPhysicalMassGrams + projection.MaximumMassGrams);
        if (totalPhysicalMassGrams <= 0L)
            throw new InvalidOperationException(
                "Passive production output branch has no positive physical mass.");

        RecipeId = recipeId;
        BranchId = branchId;
        SupportAssignmentSourceDigest = supportAssignmentSourceDigest;
        Disposition = disposition;
        Projections = Array.AsReadOnly(ordered);
        WipInputMassGrams = wipInputMassGrams;
        ProcessCleanWaterMassGrams = processCleanWaterMassGrams;
        ProcessWastewaterMassGrams = processWastewaterMassGrams;
        DeclaredLossMassGrams = declaredLossMassGrams;
        DeterministicOutcome = deterministicOutcome;
        TotalPhysicalMassGrams = totalPhysicalMassGrams;
        MassAuthorityRevision = ordered[0].MassAuthorityRevision;
        SourceDigest = sourceDigest;
    }

    public string RecipeId { get; }
    public string BranchId { get; }
    public string SupportAssignmentSourceDigest { get; }
    public ProductionOutputBranchDisposition Disposition { get; }
    public IReadOnlyList<ProductionOutputMaximumMassProjection> Projections { get; }
    public long WipInputMassGrams { get; }
    public long ProcessCleanWaterMassGrams { get; }
    public long ProcessWastewaterMassGrams { get; }
    public long DeclaredLossMassGrams { get; }
    public bool DeterministicOutcome { get; }
    public long TotalPhysicalMassGrams { get; }
    public long MassAuthorityRevision { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionPassiveBatchOutputPortfolioSnapshot
{
    internal ProductionPassiveBatchOutputPortfolioSnapshot(
        ProductionPhysicalOutputBranchMaximumSnapshot normal,
        ProductionPhysicalOutputBranchMaximumSnapshot ruined,
        string outcomeRuleDigest,
        string sourceDigest)
    {
        Normal = normal ?? throw new ArgumentNullException(nameof(normal));
        Ruined = ruined ?? throw new ArgumentNullException(nameof(ruined));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            outcomeRuleDigest,
            nameof(outcomeRuleDigest));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        if (!string.Equals(Normal.RecipeId, Ruined.RecipeId,
                StringComparison.Ordinal)
            || !string.Equals(
                Normal.SupportAssignmentSourceDigest,
                Ruined.SupportAssignmentSourceDigest,
                StringComparison.Ordinal)
            || Normal.Disposition != ProductionOutputBranchDisposition.ProductiveCycle
            || Ruined.Disposition != ProductionOutputBranchDisposition.TerminalFault
            || Normal.MassAuthorityRevision != Ruined.MassAuthorityRevision
            || checked(Ruined.TotalPhysicalMassGrams
                    + Ruined.ProcessWastewaterMassGrams
                    + Ruined.DeclaredLossMassGrams)
                != checked(Ruined.WipInputMassGrams
                    + Ruined.ProcessCleanWaterMassGrams))
        {
            throw new InvalidOperationException(
                "Passive production normal/ruined portfolio is inconsistent or non-conservative.");
        }
        OutcomeRuleDigest = outcomeRuleDigest;
        MaximumBufferMassGrams = Math.Max(
            Normal.TotalPhysicalMassGrams,
            Ruined.TotalPhysicalMassGrams);
        SourceDigest = sourceDigest;
    }

    public ProductionPhysicalOutputBranchMaximumSnapshot Normal { get; }
    public ProductionPhysicalOutputBranchMaximumSnapshot Ruined { get; }
    public string OutcomeRuleDigest { get; }
    public long MaximumBufferMassGrams { get; }
    public string SourceDigest { get; }
}

public interface IProductionPassiveBatchOutputPortfolioQuery
{
    ProductionPassiveBatchOutputPortfolioSnapshot Capture(
        ProductionRecipeSO recipe,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        ProductionAuthoredSupportAssignmentSnapshot supportAssignment);
}

/// <summary>
/// Shared passive-batch normal/ruined mass authority consumed by both buffer
/// capacity and throughput projection. It never treats ruined output as a
/// repeatable production cycle.
/// </summary>
public sealed class ProductionPassiveBatchOutputPortfolioAuthority :
    IProductionPassiveBatchOutputPortfolioQuery
{
    public const string Schema =
        "production-passive-batch-output-portfolio@1";
    public const string NormalBranchId = "branch:normal";
    public const string RuinedBranchId = "branch:ruined";

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionMaximumOutputFactorCatalog maximumFactors;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly Func<string, string, int,
        ProductionOutputMaximumMassProjection> captureAutomatic;
    private readonly Func<ProductionOutputCapabilityDescriptor, int,
        ProductionOutputMaximumMassProjection> captureDeclared;

    [VContainer.Inject]
    public ProductionPassiveBatchOutputPortfolioAuthority(
        IResourceEconomyContentCatalog catalog,
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IPhysicalItemMassQuery massQuery,
        IProductionOutputMaximumMassRegistry maximumMass)
        : this(
            catalog,
            maximumFactors,
            massQuery,
            (maximumMass ?? throw new ArgumentNullException(nameof(maximumMass)))
                .CaptureAutomatic,
            maximumMass.CaptureDeclared)
    {
    }

    internal ProductionPassiveBatchOutputPortfolioAuthority(
        IResourceEconomyContentCatalog catalog,
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IPhysicalItemMassQuery massQuery,
        Func<string, string, int, ProductionOutputMaximumMassProjection>
            captureAutomatic,
        Func<ProductionOutputCapabilityDescriptor, int,
            ProductionOutputMaximumMassProjection> captureDeclared)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.maximumFactors = maximumFactors
            ?? throw new ArgumentNullException(nameof(maximumFactors));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.captureAutomatic = captureAutomatic
            ?? throw new ArgumentNullException(nameof(captureAutomatic));
        this.captureDeclared = captureDeclared
            ?? throw new ArgumentNullException(nameof(captureDeclared));
    }

    public ProductionPassiveBatchOutputPortfolioSnapshot Capture(
        ProductionRecipeSO recipe,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        ProductionAuthoredSupportAssignmentSnapshot supportAssignment)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        if (processFluidProfile == null)
            throw new ArgumentNullException(nameof(processFluidProfile));
        if (supportAssignment == null)
            throw new ArgumentNullException(nameof(supportAssignment));
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch
            || string.IsNullOrEmpty(recipe.SpoilageItemId))
        {
            throw new InvalidOperationException(
                "Passive output portfolio requires an authored passive recipe and spoilage item.");
        }
        ValidateAssignment(recipe, supportAssignment);
        ProductionPreparedOutputMigrationScope.ValidateCanonicalProfileOrThrow(recipe);

        string recipeDigest = ProductionRecipeSemanticDigest.Capture(recipe);
        ProductionOutputFactor supportFactor = supportAssignment.Supports.Aggregate(
            ProductionOutputFactor.One,
            (current, support) => current.Multiply(support.OutputFactor));
        ProductionOutputFactor grandProjectFactor =
            ProductionOutputFactorAuthority.ResolveMaximumGrandProject(
                recipe.FacilityTag);
        ProductionOutputFactor outputFactor = grandProjectFactor.Multiply(
            supportFactor);
        ProductionOutputDefinition[] outputs = recipe.CaptureCanonicalOutputs()
            .Where(value => value.Probability > 0f
                && ProductionOutputRoleRules.IsPhysical(value.Role))
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (outputs.Length == 0)
            throw new InvalidOperationException(
                "Passive recipe has no positive physical normal output: "
                + recipe.RecipeId);

        List<ProductionOutputMaximumMassProjection> normalProjections = new();
        bool deterministicNormal = true;
        foreach (ProductionOutputDefinition output in outputs)
        {
            int maximumQuantity = outputFactor.CeilQuantity(output.Amount);
            ProductionOutputMaximumMassProjection projection = captureAutomatic(
                output.OutputLineId,
                output.ItemId,
                maximumQuantity);
            ValidateProjection(output, maximumQuantity, projection);
            normalProjections.Add(projection);
            deterministicNormal &= output.Probability >= 1f;
        }

        long wipInputMassGrams = CaptureWipInputMass(recipe);
        bool facilityFluidApplies = processFluidProfile.Supports(recipe.WorkTypeId);
        float cleanWaterUnits = recipe.CleanWaterPerCycle
            + (facilityFluidApplies
                ? processFluidProfile.CleanWaterAuthoredUnitsPerCycle
                : 0f);
        float wastewaterUnits = recipe.WastewaterPerCycle
            + (facilityFluidApplies
                ? processFluidProfile.WastewaterAuthoredUnitsPerCycle
                : 0f);
        foreach (ProductionAuthoredSupportProfileSnapshot support in
                 supportAssignment.Supports)
        {
            cleanWaterUnits += support.CleanWaterPerCycle;
            wastewaterUnits += support.WastewaterPerCycle;
            wipInputMassGrams = checked(
                wipInputMassGrams + ResolveMaximumSupportFuelMassGrams(support));
        }
        long cleanWaterMassGrams =
            ProductionFluidMassRules.ToMassGrams(cleanWaterUnits);
        long wastewaterMassGrams =
            ProductionFluidMassRules.ToMassGrams(wastewaterUnits);

        ProductionOutputMaximumMassProjection spoilageUnit = captureAutomatic(
            ProductionRuinedBatchDispositionPlan.RecoverableWasteOutputLineId,
            recipe.SpoilageItemId,
            1);
        ProductionRuinedBatchDispositionPlan disposition =
            ProductionRuinedBatchDispositionPlan.Create(
                wipInputMassGrams,
                cleanWaterMassGrams,
                wastewaterMassGrams,
                recipe.SpoilageItemId,
                spoilageUnit.DefinitionUnitMassGrams);
        ProductionOutputMaximumMassProjection ruinedProjection = captureDeclared(
            spoilageUnit.Descriptor,
            disposition.RecoverableWasteQuantity);
        if (ruinedProjection.DefinitionUnitMassGrams
                != disposition.SpoilageUnitMassGrams
            || ruinedProjection.MaximumMassGrams
                != disposition.RecoverableWasteMassGrams)
        {
            throw new InvalidOperationException(
                "Passive ruined projection drifted from its conservative disposition.");
        }
        long massRevision = normalProjections[0].MassAuthorityRevision;
        if (normalProjections.Any(value => value.MassAuthorityRevision
                != massRevision)
            || ruinedProjection.MassAuthorityRevision != massRevision
            || massRevision != massQuery.AuthorityRevision)
        {
            throw new InvalidOperationException(
                "Passive portfolio mixed physical mass authority revisions.");
        }

        string outcomeRuleDigest = CaptureOutcomeRuleDigest(
            recipe,
            recipeDigest,
            processFluidProfile,
            supportAssignment,
            grandProjectFactor,
            supportFactor,
            outputFactor,
            deterministicNormal);
        ProductionPhysicalOutputBranchMaximumSnapshot normal = CreateBranch(
            recipe,
            NormalBranchId,
            supportAssignment,
            ProductionOutputBranchDisposition.ProductiveCycle,
            normalProjections,
            wipInputMassGrams,
            cleanWaterMassGrams,
            wastewaterMassGrams,
            0L,
            deterministicNormal,
            outcomeRuleDigest);
        ProductionPhysicalOutputBranchMaximumSnapshot ruined = CreateBranch(
            recipe,
            RuinedBranchId,
            supportAssignment,
            ProductionOutputBranchDisposition.TerminalFault,
            new[] { ruinedProjection },
            wipInputMassGrams,
            cleanWaterMassGrams,
            wastewaterMassGrams,
            disposition.DeclaredLossMassGrams,
            true,
            outcomeRuleDigest);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(outcomeRuleDigest);
        digest.Append(normal.SourceDigest);
        digest.Append(ruined.SourceDigest);
        digest.Append(Math.Max(
            normal.TotalPhysicalMassGrams,
            ruined.TotalPhysicalMassGrams));
        return new ProductionPassiveBatchOutputPortfolioSnapshot(
            normal,
            ruined,
            outcomeRuleDigest,
            digest.ComputeSha256());
    }

    private void ValidateAssignment(
        ProductionRecipeSO recipe,
        ProductionAuthoredSupportAssignmentSnapshot assignment)
    {
        ProductionAuthoredSupportAssignmentSnapshot canonical = maximumFactors
            .CaptureFeasibleAssignments(recipe)
            .SingleOrDefault(value => string.Equals(
                value.SourceDigest,
                assignment.SourceDigest,
                StringComparison.Ordinal));
        if (canonical == null)
            throw new InvalidOperationException(
                "Passive portfolio received a non-feasible support assignment.");
        string[] batchProviderDigests = maximumFactors
            .CaptureBatchSupportProfiles(recipe)
            .Select(value => value.SourceDigest)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!assignment.Supports.Any(value =>
                value.Kind == ProductionSupportKind.BatchProcessor
                && batchProviderDigests.Contains(
                    value.SourceDigest,
                    StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Passive support assignment omits its authored batch processor.");
        }
    }

    private long CaptureWipInputMass(ProductionRecipeSO recipe)
    {
        long total = 0L;
        foreach (ItemAmountDefinition input in (recipe.Inputs
                     ?? Array.Empty<ItemAmountDefinition>())
                 .OrderBy(value => value?.ItemId ?? string.Empty,
                     StringComparer.Ordinal)
                 .ThenBy(value => value?.Amount ?? 0))
        {
            if (input == null || !input.HasCanonicalAuthoredValue)
                throw new InvalidOperationException(
                    "Passive recipe has a non-canonical WIP input: "
                    + recipe.RecipeId);
            long unitMass = massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)input.ItemId).Value;
            total = checked(total + checked(unitMass * input.Amount));
        }
        if (total <= 0L)
            throw new InvalidOperationException(
                "Passive recipe has no positive WIP input mass: "
                + recipe.RecipeId);
        return total;
    }

    private long ResolveMaximumSupportFuelMassGrams(
        ProductionAuthoredSupportProfileSnapshot support)
    {
        if (!support.RequiresFuel)
            return 0L;
        ResourceItemDefinitionSO[] candidates;
        if (support.FuelSupplyRule.HasAuthoredProfile)
        {
            candidates = (catalog.Items ?? Array.Empty<ResourceItemDefinitionSO>())
                .Where(value => support.FuelSupplyRule.Allows(value))
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();
        }
        else
        {
            if (!catalog.TryGetItem(
                    support.FallbackFuelItemId,
                    out ResourceItemDefinitionSO fallback))
            {
                throw new InvalidOperationException(
                    "Passive support has no physical fallback fuel: "
                    + support.SupportId);
            }
            candidates = new[] { fallback };
        }
        if (candidates.Length == 0)
            throw new InvalidOperationException(
                "Passive support has no physical fuel candidate: "
                + support.SupportId);
        long maximumUnitMass = candidates.Max(value => massQuery
            .GetDefinitionUnitMass((ItemDefinitionId)value.ItemId).Value);
        if (maximumUnitMass <= 0L || support.FuelPerCycle <= 0)
            throw new InvalidOperationException(
                "Passive support fuel mass authority is invalid: "
                + support.SupportId);
        return checked(maximumUnitMass * support.FuelPerCycle);
    }

    private static ProductionPhysicalOutputBranchMaximumSnapshot CreateBranch(
        ProductionRecipeSO recipe,
        string branchId,
        ProductionAuthoredSupportAssignmentSnapshot assignment,
        ProductionOutputBranchDisposition disposition,
        IReadOnlyList<ProductionOutputMaximumMassProjection> projections,
        long wipInputMassGrams,
        long cleanWaterMassGrams,
        long wastewaterMassGrams,
        long declaredLossMassGrams,
        bool deterministicOutcome,
        string outcomeRuleDigest)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema + ":branch");
        digest.Append(recipe.RecipeId);
        digest.Append(branchId);
        digest.Append(assignment.SourceDigest);
        digest.Append((int)disposition);
        digest.Append(wipInputMassGrams);
        digest.Append(cleanWaterMassGrams);
        digest.Append(wastewaterMassGrams);
        digest.Append(declaredLossMassGrams);
        digest.Append(deterministicOutcome);
        digest.Append(outcomeRuleDigest);
        foreach (ProductionOutputMaximumMassProjection projection in projections
                     .OrderBy(value => value.Descriptor.OutputLineId,
                         StringComparer.Ordinal))
        {
            digest.Append(projection.Descriptor.Fingerprint);
            digest.Append(projection.MaximumQuantity);
            digest.Append(projection.DefinitionUnitMassGrams);
            digest.Append(projection.MaximumMassGrams);
            digest.Append(projection.MassAuthorityRevision);
            digest.Append(projection.SourceDigest);
        }
        return new ProductionPhysicalOutputBranchMaximumSnapshot(
            recipe.RecipeId,
            branchId,
            assignment.SourceDigest,
            disposition,
            projections,
            wipInputMassGrams,
            cleanWaterMassGrams,
            wastewaterMassGrams,
            declaredLossMassGrams,
            deterministicOutcome,
            digest.ComputeSha256());
    }

    private static string CaptureOutcomeRuleDigest(
        ProductionRecipeSO recipe,
        string recipeDigest,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        ProductionAuthoredSupportAssignmentSnapshot assignment,
        ProductionOutputFactor grandProjectFactor,
        ProductionOutputFactor supportFactor,
        ProductionOutputFactor outputFactor,
        bool deterministicNormal)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema + ":outcome-rule");
        digest.Append(recipe.RecipeId);
        digest.Append(recipeDigest);
        digest.Append(ProductionPreparedOutputMigrationScope
            .CaptureProfileDigest(recipe));
        digest.Append(processFluidProfile.SourceDigest);
        digest.Append(assignment.SourceDigest);
        digest.Append(grandProjectFactor.Numerator);
        digest.Append(grandProjectFactor.Denominator);
        digest.Append(supportFactor.Numerator);
        digest.Append(supportFactor.Denominator);
        digest.Append(outputFactor.Numerator);
        digest.Append(outputFactor.Denominator);
        digest.Append(recipe.SpoilageItemId);
        digest.Append(deterministicNormal);
        return digest.ComputeSha256();
    }

    private static void ValidateProjection(
        ProductionOutputDefinition output,
        int quantity,
        ProductionOutputMaximumMassProjection projection)
    {
        if (!string.Equals(projection.Descriptor.OutputLineId,
                output.OutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(projection.Descriptor.ItemId,
                output.ItemId,
                StringComparison.Ordinal)
            || projection.MaximumQuantity != quantity
            || projection.MaximumMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Passive normal output projection drifted from its authored line.");
        }
    }
}
