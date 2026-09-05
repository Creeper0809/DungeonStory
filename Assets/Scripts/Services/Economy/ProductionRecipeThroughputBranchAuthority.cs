using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// First production-safe recipe throughput branch authority. Work-only normal
/// outputs are projected through the shared maximum-mass registry inside one
/// exact feasible support assignment. Passive batches remain incomplete until
/// their normal/ruined branch authority is extracted from the durable WIP path.
/// </summary>
public sealed class ProductionRecipeThroughputBranchAuthority :
    IProductionRecipeThroughputBranchQuery
{
    public const string Schema =
        "production-recipe-throughput-branch-authority@2";
    public const string NormalBranchId = "branch:normal";

    private readonly IProductionOutputMaximumMassRegistry maximumMass;
    private readonly IProductionPassiveBatchOutputPortfolioQuery
        passiveBatchOutputs;
    private readonly string registryFingerprint;

    public ProductionRecipeThroughputBranchAuthority(
        IProductionOutputMaximumMassRegistry maximumMass,
        IProductionPassiveBatchOutputPortfolioQuery passiveBatchOutputs = null)
    {
        this.maximumMass = maximumMass
            ?? throw new ArgumentNullException(nameof(maximumMass));
        registryFingerprint = maximumMass.RegistryFingerprint;
        this.passiveBatchOutputs = passiveBatchOutputs;
        ProductionAuthoredThroughputContractRules.RequireDigest(
            registryFingerprint,
            nameof(maximumMass));
    }

    public ProductionRecipeThroughputBranchQueryResult Capture(
        ProductionRecipeSO recipe,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        ProductionAuthoredSupportAssignmentSnapshot supportAssignment)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        if (processFluidProfile == null)
            throw new ArgumentNullException(nameof(processFluidProfile));
        if (supportAssignment == null)
            throw new ArgumentNullException(nameof(supportAssignment));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            processFluidProfile.SourceDigest,
            nameof(processFluidProfile));
        if (!string.Equals(
                maximumMass.RegistryFingerprint,
                registryFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production output maximum-mass registry fingerprint drifted.");
        }
        ValidateAssignment(supportAssignment);

        string recipeDigest = ProductionRecipeSemanticDigest.Capture(recipe);
        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch)
        {
            if (passiveBatchOutputs == null)
            {
                return Missing(
                    recipe,
                    processFluidProfile,
                    supportAssignment,
                    recipeDigest,
                    "passive normal and ruined branches require durable WIP authority");
            }

            ProductionPassiveBatchOutputPortfolioSnapshot portfolio =
                passiveBatchOutputs.Capture(
                    recipe,
                    processFluidProfile,
                    supportAssignment);
            if (!portfolio.Normal.DeterministicOutcome)
            {
                return Missing(
                    recipe,
                    processFluidProfile,
                    supportAssignment,
                    recipeDigest,
                    "probabilistic passive normal output requires an explicit maximum witness");
            }
            if (portfolio.Normal.Disposition
                    != ProductionOutputBranchDisposition.ProductiveCycle
                || portfolio.Ruined.Disposition
                    != ProductionOutputBranchDisposition.TerminalFault
                || !string.Equals(
                    portfolio.Normal.SupportAssignmentSourceDigest,
                    supportAssignment.SourceDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Passive output portfolio drifted from its exact productive assignment.");
            }

            CanonicalSemanticDigestBuilder passiveBranchDigest = new();
            passiveBranchDigest.Append(Schema + ":passive-normal");
            passiveBranchDigest.Append(recipe.RecipeId);
            passiveBranchDigest.Append(recipeDigest);
            passiveBranchDigest.Append(processFluidProfile.SourceDigest);
            passiveBranchDigest.Append(supportAssignment.SourceDigest);
            passiveBranchDigest.Append(registryFingerprint);
            passiveBranchDigest.Append(portfolio.OutcomeRuleDigest);
            passiveBranchDigest.Append(portfolio.Normal.SourceDigest);
            passiveBranchDigest.Append(portfolio.Normal.TotalPhysicalMassGrams);
            ProductionRecipeThroughputBranchSnapshot passiveBranch = new(
                recipe.RecipeId,
                NormalBranchId,
                supportAssignment.SourceDigest,
                portfolio.Normal.TotalPhysicalMassGrams,
                portfolio.Normal.Projections
                    .Select(value => value.Descriptor.CapabilityId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                passiveBranchDigest.ComputeSha256());

            CanonicalSemanticDigestBuilder passiveResultDigest = new();
            passiveResultDigest.Append(Schema + ":result");
            passiveResultDigest.Append(portfolio.SourceDigest);
            passiveResultDigest.Append(passiveBranch.SourceDigest);
            return ProductionRecipeThroughputBranchQueryResult.Complete(
                new[] { passiveBranch },
                passiveResultDigest.ComputeSha256());
        }
        if (recipe.ProcessKind != ProductionProcessKind.WorkOnly)
        {
            throw new InvalidOperationException(
                "Recipe throughput process kind is unsupported: "
                + recipe.RecipeId);
        }

        ProductionOutputDefinition[] physical = recipe
            .CaptureCanonicalOutputs()
            .Where(value => ProductionOutputRoleRules.IsPhysical(value.Role)
                && value.Probability > 0f)
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (physical.Length == 0)
        {
            return Missing(
                recipe,
                processFluidProfile,
                supportAssignment,
                recipeDigest,
                "recipe has no positive physical normal output");
        }

        ProductionOutputFactor supportFactor = supportAssignment.Supports
            .Aggregate(
                ProductionOutputFactor.One,
                (current, support) => current.Multiply(support.OutputFactor));
        ProductionOutputFactor grandProjectFactor =
            ProductionOutputFactorAuthority.ResolveMaximumGrandProject(
                recipe.FacilityTag);
        ProductionOutputFactor assignmentFactor = grandProjectFactor.Multiply(
            supportFactor);
        List<LineProjection> lines = new(physical.Length);
        long totalMassGrams = 0L;
        long? massAuthorityRevision = null;
        foreach (ProductionOutputDefinition output in physical)
        {
            decimal scaled = assignmentFactor.Scale(output.Amount);
            if (scaled <= 0m || scaled > int.MaxValue)
            {
                throw new OverflowException(
                    "Recipe normal output quantity exceeds Int32: "
                    + recipe.RecipeId + "/" + output.OutputLineId);
            }
            int maximumQuantity = checked((int)decimal.Ceiling(scaled));
            ProductionOutputMaximumMassProjection projection = maximumMass
                .CaptureAutomatic(
                    output.OutputLineId,
                    output.ItemId,
                    maximumQuantity);
            ValidateProjection(output, maximumQuantity, projection);
            if (massAuthorityRevision.HasValue
                && massAuthorityRevision.Value
                    != projection.MassAuthorityRevision)
            {
                throw new InvalidOperationException(
                    "Recipe normal output projections used different mass revisions: "
                    + recipe.RecipeId);
            }
            massAuthorityRevision ??= projection.MassAuthorityRevision;
            totalMassGrams = checked(
                totalMassGrams + projection.MaximumMassGrams);
            lines.Add(new LineProjection(
                output,
                maximumQuantity,
                projection));
        }
        if (totalMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Recipe normal output projection produced zero physical mass: "
                + recipe.RecipeId);
        }

        CanonicalSemanticDigestBuilder branchDigest = new();
        branchDigest.Append(Schema);
        branchDigest.Append(recipe.RecipeId);
        branchDigest.Append(recipeDigest);
        branchDigest.Append(processFluidProfile.SourceDigest);
        branchDigest.Append(supportAssignment.SourceDigest);
        branchDigest.Append(registryFingerprint);
        branchDigest.Append(grandProjectFactor.Numerator);
        branchDigest.Append(grandProjectFactor.Denominator);
        branchDigest.Append(supportFactor.Numerator);
        branchDigest.Append(supportFactor.Denominator);
        branchDigest.Append(assignmentFactor.Numerator);
        branchDigest.Append(assignmentFactor.Denominator);
        branchDigest.Append(lines.Count);
        foreach (LineProjection line in lines)
            line.AppendTo(branchDigest);
        branchDigest.Append(massAuthorityRevision.GetValueOrDefault());
        branchDigest.Append(totalMassGrams);
        ProductionRecipeThroughputBranchSnapshot branch = new(
            recipe.RecipeId,
            NormalBranchId,
            supportAssignment.SourceDigest,
            totalMassGrams,
            lines.Select(value => value.OutputCapabilityId)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            branchDigest.ComputeSha256());

        CanonicalSemanticDigestBuilder resultDigest = new();
        resultDigest.Append(Schema + ":result");
        resultDigest.Append(branch.SourceDigest);
        return ProductionRecipeThroughputBranchQueryResult.Complete(
            new[] { branch },
            resultDigest.ComputeSha256());
    }

    private static void ValidateAssignment(
        ProductionAuthoredSupportAssignmentSnapshot assignment)
    {
        ProductionAuthoredThroughputContractRules.RequireDigest(
            assignment.SourceDigest,
            nameof(assignment));
        if (assignment.Supports == null
            || assignment.Supports.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.SupportId)
                || !string.Equals(
                    value.SupportId,
                    value.SupportId.Trim(),
                    StringComparison.Ordinal)
                || value.OutputFactor.Numerator <= 0L
                || value.OutputFactor.Denominator <= 0L
                || !ProductionAuthoredThroughputContractRules
                    .IsLowercaseSha256(value.SourceDigest))
            || assignment.Supports.Select(value => value.SupportId)
                .Distinct(StringComparer.Ordinal).Count()
                != assignment.Supports.Count)
        {
            throw new InvalidOperationException(
                "Recipe throughput support assignment is invalid.");
        }
    }

    private static void ValidateProjection(
        ProductionOutputDefinition output,
        int expectedQuantity,
        ProductionOutputMaximumMassProjection projection)
    {
        ProductionOutputCapabilityDescriptor descriptor =
            projection.Descriptor;
        string expectedFingerprint =
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                descriptor.OutputLineId,
                descriptor.ItemId,
                descriptor.CapabilityId,
                descriptor.CapabilityVersion,
                descriptor.ComponentCodecId,
                descriptor.ComponentCodecVersion);
        if (projection.MaximumQuantity != expectedQuantity
            || projection.DefinitionUnitMassGrams <= 0L
            || projection.MaximumMassGrams <= 0L
            || projection.MaximumMassGrams != checked(
                projection.DefinitionUnitMassGrams * expectedQuantity)
            || projection.MassAuthorityRevision < 0L
            || !string.Equals(
                descriptor.OutputLineId,
                output.OutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                descriptor.ItemId,
                output.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                descriptor.Fingerprint,
                expectedFingerprint,
                StringComparison.Ordinal)
            || !ProductionAuthoredThroughputContractRules.IsLowercaseSha256(
                projection.SourceDigest))
        {
            throw new InvalidOperationException(
                "Recipe output maximum-mass projection drifted: "
                + output.OutputLineId);
        }
    }

    private ProductionRecipeThroughputBranchQueryResult Missing(
        ProductionRecipeSO recipe,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        ProductionAuthoredSupportAssignmentSnapshot assignment,
        string recipeDigest,
        string detail)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema + ":missing");
        digest.Append(recipe.RecipeId);
        digest.Append(recipeDigest);
        digest.Append(processFluidProfile.SourceDigest);
        digest.Append(assignment.SourceDigest);
        digest.Append(registryFingerprint);
        digest.Append(detail);
        return ProductionRecipeThroughputBranchQueryResult.Missing(
            ProductionThroughputGapReason.RecipeOutputBranchAuthorityMissing,
            detail,
            digest.ComputeSha256());
    }

    private readonly struct LineProjection
    {
        internal LineProjection(
            ProductionOutputDefinition output,
            int maximumQuantity,
            ProductionOutputMaximumMassProjection projection)
        {
            Output = output;
            MaximumQuantity = maximumQuantity;
            Projection = projection;
        }

        private ProductionOutputDefinition Output { get; }
        private int MaximumQuantity { get; }
        private ProductionOutputMaximumMassProjection Projection { get; }
        internal string OutputCapabilityId =>
            Projection.Descriptor.CapabilityId;

        internal void AppendTo(CanonicalSemanticDigestBuilder digest)
        {
            digest.Append(Output.OutputLineId);
            digest.AppendEnum(Output.Role);
            digest.Append(Output.ItemId);
            digest.Append(Output.Amount);
            digest.AppendFloat(Output.Probability);
            digest.Append(MaximumQuantity);
            digest.Append(Projection.Descriptor.CapabilityId);
            digest.Append(Projection.Descriptor.CapabilityVersion);
            digest.Append(Projection.Descriptor.ComponentCodecId);
            digest.Append(Projection.Descriptor.ComponentCodecVersion);
            digest.Append(Projection.Descriptor.Fingerprint);
            digest.Append(Projection.DefinitionUnitMassGrams);
            digest.Append(Projection.MaximumMassGrams);
            digest.Append(Projection.MassAuthorityRevision);
            digest.Append(Projection.SourceDigest);
        }
    }
}
