using System;
using System.Collections.Generic;
using System.Linq;

public enum ProductionOutputClearanceExecutableDescriptorGapReason
{
    ContributorUnregistered = 1,
    CurrentSourceJoinMissing = 2,
    SelectedBranchDrift = 3,
    ExecutionPayloadIncomplete = 4
}

public sealed class ProductionOutputClearanceExecutableInput
{
    public ProductionOutputClearanceExecutableInput(string itemId, int quantity)
    {
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            itemId,
            nameof(itemId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        ItemId = itemId;
        Quantity = quantity;
    }

    public string ItemId { get; }
    public int Quantity { get; }

    internal void AppendTo(CanonicalSemanticDigestBuilder digest)
    {
        digest.Append(ItemId);
        digest.Append(Quantity);
    }
}

public sealed class ProductionOutputClearanceExecutableOutput
{
    public ProductionOutputClearanceExecutableOutput(
        string outputLineId,
        string itemId,
        int quantity,
        long massGrams,
        ProductionOutputCapabilityDescriptor descriptor,
        string projectionSourceDigest)
    {
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            outputLineId,
            nameof(outputLineId));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            itemId,
            nameof(itemId));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            projectionSourceDigest,
            nameof(projectionSourceDigest));
        if (quantity <= 0 || massGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (!string.Equals(descriptor.OutputLineId, outputLineId,
                StringComparison.Ordinal)
            || !string.Equals(descriptor.ItemId, itemId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Executable output descriptor drifted from its exact line.");
        }

        OutputLineId = outputLineId;
        ItemId = itemId;
        Quantity = quantity;
        MassGrams = massGrams;
        Descriptor = descriptor;
        ProjectionSourceDigest = projectionSourceDigest;
    }

    public string OutputLineId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public ProductionOutputCapabilityDescriptor Descriptor { get; }
    public string ProjectionSourceDigest { get; }

    internal void AppendTo(CanonicalSemanticDigestBuilder digest)
    {
        digest.Append(OutputLineId);
        digest.Append(ItemId);
        digest.Append(Quantity);
        digest.Append(MassGrams);
        digest.Append(Descriptor.Fingerprint);
        digest.Append(ProjectionSourceDigest);
    }
}

public sealed class ProductionOutputClearanceExecutableSupport
{
    public ProductionOutputClearanceExecutableSupport(
        ProductionAuthoredSupportProfileSnapshot support)
    {
        if (support == null) throw new ArgumentNullException(nameof(support));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            support.SupportId,
            nameof(support));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            support.SourceDigest,
            nameof(support));
        SupportId = support.SupportId;
        Kind = support.Kind;
        InstanceCount = 1;
        BatchCapacity = support.BatchCapacity;
        RequiresFuel = support.RequiresFuel;
        FallbackFuelItemId = support.FallbackFuelItemId ?? string.Empty;
        FuelPerCycle = support.FuelPerCycle;
        CleanWaterPerCycle = support.CleanWaterPerCycle;
        WastewaterPerCycle = support.WastewaterPerCycle;
        SourceDigest = support.SourceDigest;
    }

    public string SupportId { get; }
    public ProductionSupportKind Kind { get; }
    public int InstanceCount { get; }
    public int BatchCapacity { get; }
    public bool RequiresFuel { get; }
    public string FallbackFuelItemId { get; }
    public int FuelPerCycle { get; }
    public float CleanWaterPerCycle { get; }
    public float WastewaterPerCycle { get; }
    public string SourceDigest { get; }

    internal void AppendTo(CanonicalSemanticDigestBuilder digest)
    {
        digest.Append(SupportId);
        digest.AppendEnum(Kind);
        digest.Append(InstanceCount);
        digest.Append(BatchCapacity);
        digest.Append(RequiresFuel);
        digest.Append(FallbackFuelItemId);
        digest.Append(FuelPerCycle);
        digest.AppendFloat(CleanWaterPerCycle);
        digest.AppendFloat(WastewaterPerCycle);
        digest.Append(SourceDigest);
    }
}

public interface IProductionOutputClearanceExecutablePayload
{
    string PayloadKind { get; }
    string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceRecipeExecutablePayload :
    IProductionOutputClearanceExecutablePayload
{
    internal ProductionOutputClearanceRecipeExecutablePayload(
        ProductionRecipeSO recipe,
        ProductionAuthoredSupportAssignmentSnapshot assignment,
        IReadOnlyList<ProductionOutputClearanceExecutableInput> inputs,
        IReadOnlyList<ProductionOutputClearanceExecutableOutput> outputs)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        if (assignment == null) throw new ArgumentNullException(nameof(assignment));
        ProductionOutputClearanceExecutableInput[] orderedInputs = (inputs
                ?? throw new ArgumentNullException(nameof(inputs)))
            .OrderBy(value => value?.ItemId, StringComparer.Ordinal)
            .ToArray();
        ProductionOutputClearanceExecutableOutput[] orderedOutputs = (outputs
                ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        ProductionOutputClearanceExecutableSupport[] orderedSupports =
            assignment.Supports
                .Select(value => new ProductionOutputClearanceExecutableSupport(value))
                .OrderBy(value => value.SupportId, StringComparer.Ordinal)
                .ToArray();
        if (orderedOutputs.Length == 0
            || orderedInputs.Any(value => value == null)
            || orderedOutputs.Any(value => value == null)
            || orderedInputs.Select(value => value.ItemId)
                .Distinct(StringComparer.Ordinal).Count() != orderedInputs.Length
            || orderedOutputs.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != orderedOutputs.Length)
        {
            throw new InvalidOperationException(
                "Recipe executable payload has incomplete or duplicate physical "
                + "lines. recipe=" + recipe.RecipeId
                + ";inputs=" + orderedInputs.Length
                + ";outputs=" + orderedOutputs.Length);
        }

        RecipeId = recipe.RecipeId;
        ProcessKind = recipe.ProcessKind;
        WorkTypeId = recipe.WorkTypeId.Value;
        RequiredResearchId = recipe.RequiredResearchId;
        RequiredWork = recipe.RequiredWork;
        PreparationWork = recipe.PreparationWork;
        FinishingWork = recipe.FinishingWork;
        ProcessingGameHours = recipe.ProcessingGameHours;
        CleanWaterPerCycle = recipe.CleanWaterPerCycle;
        WastewaterPerCycle = recipe.WastewaterPerCycle;
        OptimalTemperatureMinimum = recipe.OptimalTemperatureMinimum;
        OptimalTemperatureMaximum = recipe.OptimalTemperatureMaximum;
        WarningTemperatureMinimum = recipe.WarningTemperatureMinimum;
        WarningTemperatureMaximum = recipe.WarningTemperatureMaximum;
        Inputs = Array.AsReadOnly(orderedInputs);
        Outputs = Array.AsReadOnly(orderedOutputs);
        Supports = Array.AsReadOnly(orderedSupports);
        SupportAssignmentSourceDigest = assignment.SourceDigest;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-recipe-executable-payload@1");
        digest.Append(ProductionRecipeSemanticDigest.Capture(recipe));
        digest.Append(RecipeId);
        digest.AppendEnum(ProcessKind);
        digest.Append(WorkTypeId);
        digest.Append(RequiredResearchId);
        digest.AppendFloat(RequiredWork);
        digest.AppendFloat(PreparationWork);
        digest.AppendFloat(FinishingWork);
        digest.AppendFloat(ProcessingGameHours);
        digest.AppendFloat(CleanWaterPerCycle);
        digest.AppendFloat(WastewaterPerCycle);
        digest.AppendFloat(OptimalTemperatureMinimum);
        digest.AppendFloat(OptimalTemperatureMaximum);
        digest.AppendFloat(WarningTemperatureMinimum);
        digest.AppendFloat(WarningTemperatureMaximum);
        digest.Append(Inputs.Count);
        foreach (ProductionOutputClearanceExecutableInput input in Inputs)
            input.AppendTo(digest);
        digest.Append(Outputs.Count);
        foreach (ProductionOutputClearanceExecutableOutput output in Outputs)
            output.AppendTo(digest);
        digest.Append(Supports.Count);
        foreach (ProductionOutputClearanceExecutableSupport support in Supports)
            support.AppendTo(digest);
        digest.Append(SupportAssignmentSourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string PayloadKind => "recipe";
    public string RecipeId { get; }
    public ProductionProcessKind ProcessKind { get; }
    public string WorkTypeId { get; }
    public string RequiredResearchId { get; }
    public float RequiredWork { get; }
    public float PreparationWork { get; }
    public float FinishingWork { get; }
    public float ProcessingGameHours { get; }
    public float CleanWaterPerCycle { get; }
    public float WastewaterPerCycle { get; }
    public float OptimalTemperatureMinimum { get; }
    public float OptimalTemperatureMaximum { get; }
    public float WarningTemperatureMinimum { get; }
    public float WarningTemperatureMaximum { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableInput> Inputs { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableOutput> Outputs { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableSupport> Supports { get; }
    public string SupportAssignmentSourceDigest { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceExecutableDescriptor
{
    internal ProductionOutputClearanceExecutableDescriptor(
        ProductionOutputClearanceMeasurementPlan plan,
        string facilitySourceDigest,
        int outputBufferCycleCapacity,
        IProductionOutputClearanceExecutablePayload payload)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            facilitySourceDigest,
            nameof(facilitySourceDigest));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            payload.SourceDigest,
            nameof(payload));
        if (outputBufferCycleCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputBufferCycleCapacity));
        FacilitySourceDigest = facilitySourceDigest;
        OutputBufferCycleCapacity = outputBufferCycleCapacity;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-executable-descriptor@1");
        digest.Append(Plan.SourceDigest);
        digest.Append(Plan.DefinitionId);
        digest.Append(Plan.WorkstationTag);
        digest.Append(Plan.Winner.MeasurementCapabilityId);
        digest.Append(Plan.Winner.Source.SourceDigest);
        digest.Append(FacilitySourceDigest);
        digest.Append(OutputBufferCycleCapacity);
        digest.Append(Payload.PayloadKind);
        digest.Append(Payload.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceMeasurementPlan Plan { get; }
    public string FacilitySourceDigest { get; }
    public int OutputBufferCycleCapacity { get; }
    public IProductionOutputClearanceExecutablePayload Payload { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceExecutableDescriptorGap
{
    internal ProductionOutputClearanceExecutableDescriptorGap(
        ProductionOutputClearanceMeasurementPlan plan,
        ProductionOutputClearanceExecutableDescriptorGapReason reason,
        string detail)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        if (!Enum.IsDefined(typeof(
                ProductionOutputClearanceExecutableDescriptorGapReason), reason)
            || string.IsNullOrWhiteSpace(detail)
            || !string.Equals(detail, detail.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Executable descriptor gap is invalid.");
        }
        Reason = reason;
        Detail = detail;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-executable-gap@1");
        digest.Append(Plan.SourceDigest);
        digest.Append((int)Reason);
        digest.Append(Detail);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceMeasurementPlan Plan { get; }
    public ProductionOutputClearanceExecutableDescriptorGapReason Reason { get; }
    public string Detail { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceExecutableDescriptorContribution
{
    private ProductionOutputClearanceExecutableDescriptorContribution(
        ProductionOutputClearanceExecutableDescriptor descriptor,
        ProductionOutputClearanceExecutableDescriptorGap gap)
    {
        if ((descriptor == null) == (gap == null))
            throw new ArgumentException(
                "Executable contribution requires exactly one descriptor or gap.");
        Descriptor = descriptor;
        Gap = gap;
    }

    public ProductionOutputClearanceExecutableDescriptor Descriptor { get; }
    public ProductionOutputClearanceExecutableDescriptorGap Gap { get; }
    public bool IsComplete => Descriptor != null;

    public static ProductionOutputClearanceExecutableDescriptorContribution
        Complete(ProductionOutputClearanceExecutableDescriptor descriptor) =>
        new(descriptor, null);

    public static ProductionOutputClearanceExecutableDescriptorContribution
        Incomplete(
            ProductionOutputClearanceMeasurementPlan plan,
            ProductionOutputClearanceExecutableDescriptorGapReason reason,
            string detail) => new(
            null,
            new ProductionOutputClearanceExecutableDescriptorGap(
                plan,
                reason,
                detail));
}

public interface IProductionOutputClearanceExecutableDescriptorContributor
{
    string MeasurementCapabilityId { get; }
    int ContractVersion { get; }

    ProductionOutputClearanceExecutableDescriptorContribution Capture(
        ProductionOutputClearanceMeasurementPlan plan,
        ProductionOutputClearanceMeasurementScopeSnapshot scope);
}

public sealed class ProductionOutputClearanceExecutableDescriptorCoverage
{
    internal ProductionOutputClearanceExecutableDescriptorCoverage(
        IReadOnlyList<ProductionOutputClearanceExecutableDescriptor> descriptors,
        IReadOnlyList<ProductionOutputClearanceExecutableDescriptorGap> gaps,
        string registryFingerprint)
    {
        ProductionOutputClearanceExecutableDescriptor[] orderedDescriptors =
            (descriptors ?? throw new ArgumentNullException(nameof(descriptors)))
            .OrderBy(value => value.Plan.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value.Plan.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        ProductionOutputClearanceExecutableDescriptorGap[] orderedGaps = (gaps
                ?? throw new ArgumentNullException(nameof(gaps)))
            .OrderBy(value => value.Plan.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value.Plan.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        if (orderedDescriptors.Select(Key).Concat(orderedGaps.Select(Key))
                .Distinct(StringComparer.Ordinal).Count()
            != orderedDescriptors.Length + orderedGaps.Length)
        {
            throw new InvalidOperationException(
                "Executable descriptor coverage contains duplicate producer keys.");
        }
        Descriptors = Array.AsReadOnly(orderedDescriptors);
        Gaps = Array.AsReadOnly(orderedGaps);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-executable-coverage@1");
        digest.Append(registryFingerprint);
        digest.Append(Descriptors.Count);
        foreach (ProductionOutputClearanceExecutableDescriptor descriptor in Descriptors)
            digest.Append(descriptor.SourceDigest);
        digest.Append(Gaps.Count);
        foreach (ProductionOutputClearanceExecutableDescriptorGap gap in Gaps)
            digest.Append(gap.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public IReadOnlyList<ProductionOutputClearanceExecutableDescriptor> Descriptors
        { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableDescriptorGap> Gaps
        { get; }
    public string SourceDigest { get; }

    private static string Key(ProductionOutputClearanceExecutableDescriptor value) =>
        value.Plan.DefinitionId + "\n" + value.Plan.WorkstationTag;
    private static string Key(ProductionOutputClearanceExecutableDescriptorGap value) =>
        value.Plan.DefinitionId + "\n" + value.Plan.WorkstationTag;
}

public sealed class ProductionOutputClearanceExecutableDescriptorRegistry
{
    private readonly Dictionary<string,
        IProductionOutputClearanceExecutableDescriptorContributor> contributors;

    public ProductionOutputClearanceExecutableDescriptorRegistry(
        IEnumerable<IProductionOutputClearanceExecutableDescriptorContributor>
            contributors)
    {
        IProductionOutputClearanceExecutableDescriptorContributor[] ordered =
            (contributors ?? throw new ArgumentNullException(nameof(contributors)))
            .OrderBy(value => value?.MeasurementCapabilityId,
                StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null
                || value.ContractVersion <= 0
                || !ProductionOutputClearanceMeasurementSourceBranch.Canonical(
                    value.MeasurementCapabilityId))
            || ordered.Select(value => value.MeasurementCapabilityId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Executable descriptor contributors are invalid or duplicated.");
        }
        this.contributors = ordered.ToDictionary(
            value => value.MeasurementCapabilityId,
            StringComparer.Ordinal);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-executable-registry@1");
        digest.Append(ordered.Length);
        foreach (IProductionOutputClearanceExecutableDescriptorContributor value in ordered)
        {
            digest.Append(value.MeasurementCapabilityId);
            digest.Append(value.ContractVersion);
        }
        RegistryFingerprint = digest.ComputeSha256();
    }

    public string RegistryFingerprint { get; }

    public ProductionOutputClearanceExecutableDescriptorCoverage Capture(
        ProductionOutputClearanceMeasurementScopeSnapshot scope)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        List<ProductionOutputClearanceExecutableDescriptor> descriptors = new();
        List<ProductionOutputClearanceExecutableDescriptorGap> gaps = new();
        foreach (ProductionOutputClearanceMeasurementPlan plan in scope.Plans)
        {
            if (!contributors.TryGetValue(
                    plan.Winner.MeasurementCapabilityId,
                    out IProductionOutputClearanceExecutableDescriptorContributor
                        contributor))
            {
                gaps.Add(new ProductionOutputClearanceExecutableDescriptorGap(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .ContributorUnregistered,
                    plan.Winner.MeasurementCapabilityId));
                continue;
            }
            ProductionOutputClearanceExecutableDescriptorContribution result =
                contributor.Capture(plan, scope);
            if (result.IsComplete) descriptors.Add(result.Descriptor);
            else gaps.Add(result.Gap);
        }
        if (descriptors.Count + gaps.Count != scope.Plans.Count)
            throw new InvalidOperationException(
                "Executable descriptor coverage did not account for every plan.");
        return new ProductionOutputClearanceExecutableDescriptorCoverage(
            descriptors,
            gaps,
            RegistryFingerprint);
    }
}

public sealed class ProductionOutputClearanceRecipeExecutableDescriptorContributor :
    IProductionOutputClearanceExecutableDescriptorContributor
{
    private readonly IProductionMaximumOutputFactorCatalog maximumFactors;
    private readonly IProductionRecipeThroughputBranchQuery branches;
    private readonly IProductionOutputMaximumMassRegistry maximumMass;
    private readonly IProductionPassiveBatchOutputPortfolioQuery passiveOutputs;

    public ProductionOutputClearanceRecipeExecutableDescriptorContributor(
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IProductionRecipeThroughputBranchQuery branches,
        IProductionOutputMaximumMassRegistry maximumMass,
        IProductionPassiveBatchOutputPortfolioQuery passiveOutputs)
    {
        this.maximumFactors = maximumFactors
            ?? throw new ArgumentNullException(nameof(maximumFactors));
        this.branches = branches ?? throw new ArgumentNullException(nameof(branches));
        this.maximumMass = maximumMass
            ?? throw new ArgumentNullException(nameof(maximumMass));
        this.passiveOutputs = passiveOutputs
            ?? throw new ArgumentNullException(nameof(passiveOutputs));
    }

    public string MeasurementCapabilityId =>
        ProductionOutputClearanceMeasurementCapabilityIds.Recipe;
    public int ContractVersion => 1;

    public ProductionOutputClearanceExecutableDescriptorContribution Capture(
        ProductionOutputClearanceMeasurementPlan plan,
        ProductionOutputClearanceMeasurementScopeSnapshot scope)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (!string.Equals(plan.Winner.MeasurementCapabilityId,
                MeasurementCapabilityId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Recipe executable contributor received an unowned plan.");

        ProductionAuthoredThroughputFacilitySubject facility = scope.AuthoredScope
            .Facilities.SingleOrDefault(value => string.Equals(
                    value.DefinitionId, plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(value.WorkstationTag, plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionFacilityOutputCensusRow row = scope.AuthoredScope.Census.Rows
            .SingleOrDefault(value => string.Equals(
                    value.DefinitionId, plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(value.WorkstationTag, plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionRecipeSO recipe = facility?.Recipes.SingleOrDefault(value =>
            string.Equals(value.RecipeId, plan.Winner.Source.ProducerId,
                StringComparison.Ordinal));
        if (facility == null || row == null || recipe == null)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .CurrentSourceJoinMissing,
                    "recipe-current-source-join-missing");
        }

        List<(ProductionAuthoredSupportAssignmentSnapshot Assignment,
            ProductionRecipeThroughputBranchSnapshot Branch)> matches = new();
        foreach (ProductionAuthoredSupportAssignmentSnapshot assignment in
                 maximumFactors.CaptureFeasibleAssignments(recipe))
        {
            ProductionRecipeThroughputBranchQueryResult result = branches.Capture(
                recipe,
                facility.ProcessFluidProfile,
                assignment);
            if (!result.IsComplete) continue;
            matches.AddRange(result.Branches
                .Where(branch => string.Equals(branch.SourceDigest,
                    plan.Winner.Source.UpstreamSourceDigest,
                    StringComparison.Ordinal))
                .Select(branch => (assignment, branch)));
        }
        if (matches.Count != 1)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .SelectedBranchDrift,
                    "recipe-selected-branch-match-count:" + matches.Count);
        }

        (ProductionAuthoredSupportAssignmentSnapshot selectedAssignment,
            ProductionRecipeThroughputBranchSnapshot selectedBranch) = matches[0];
        ProductionOutputClearanceExecutableOutput[] outputs = CaptureOutputs(
            recipe,
            facility.ProcessFluidProfile,
            selectedAssignment);
        long totalMass = outputs.Aggregate(
            0L,
            (sum, output) => checked(sum + output.MassGrams));
        string[] capabilities = outputs.Select(value => value.Descriptor.CapabilityId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (totalMass != selectedBranch.MaximumOutputMassGrams
            || totalMass != plan.Winner.Source.MaximumSingleCompletionMassGrams
            || !capabilities.SequenceEqual(
                plan.Winner.Source.OutputCapabilityIds,
                StringComparer.Ordinal))
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .ExecutionPayloadIncomplete,
                    "recipe-output-vector-drift");
        }

        ProductionOutputClearanceExecutableInput[] inputs = recipe.Inputs
            .Select(value => new ProductionOutputClearanceExecutableInput(
                value.ItemId,
                value.Amount))
            .ToArray();
        ProductionOutputClearanceRecipeExecutablePayload payload = new(
            recipe,
            selectedAssignment,
            inputs,
            outputs);
        return ProductionOutputClearanceExecutableDescriptorContribution.Complete(
            new ProductionOutputClearanceExecutableDescriptor(
                plan,
                row.SourceDigest,
                row.OutputBufferCycleCapacity,
                payload));
    }

    private ProductionOutputClearanceExecutableOutput[] CaptureOutputs(
        ProductionRecipeSO recipe,
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile,
        ProductionAuthoredSupportAssignmentSnapshot assignment)
    {
        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch)
        {
            ProductionPassiveBatchOutputPortfolioSnapshot portfolio =
                passiveOutputs.Capture(recipe, processFluidProfile, assignment);
            return portfolio.Normal.Projections.Select(CreateOutput).ToArray();
        }

        ProductionOutputFactor supportFactor = assignment.Supports.Aggregate(
            ProductionOutputFactor.One,
            (current, support) => current.Multiply(support.OutputFactor));
        ProductionOutputFactor factor = ProductionOutputFactorAuthority
            .ResolveMaximumGrandProject(recipe.FacilityTag)
            .Multiply(supportFactor);
        return recipe.CaptureCanonicalOutputs()
            .Where(value => ProductionOutputRoleRules.IsPhysical(value.Role)
                && value.Probability > 0f)
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .Select(output => CreateOutput(maximumMass.CaptureAutomatic(
                output.OutputLineId,
                output.ItemId,
                factor.CeilQuantity(output.Amount))))
            .ToArray();
    }

    private static ProductionOutputClearanceExecutableOutput CreateOutput(
        ProductionOutputMaximumMassProjection projection) => new(
        projection.Descriptor.OutputLineId,
        projection.Descriptor.ItemId,
        projection.MaximumQuantity,
        projection.MaximumMassGrams,
        projection.Descriptor,
        projection.SourceDigest);
}

public sealed class ProductionOutputClearanceCombatCraftExecutablePayload :
    IProductionOutputClearanceExecutablePayload
{
    internal ProductionOutputClearanceCombatCraftExecutablePayload(
        CombatCraftCycleSnapshot cycle,
        IReadOnlyList<ProductionOutputClearanceExecutableOutput> outputs)
    {
        if (cycle == null) throw new ArgumentNullException(nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.CraftDefinitionId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.BranchId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.SelectedMaterialId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.SelectedMaterialItemId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.ExecutionPath,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            cycle.SourceDigest,
            nameof(cycle));
        if (!float.IsFinite(cycle.RequiredWork) || cycle.RequiredWork <= 0f
            || !string.Equals(
                CombatCraftFacilityOutputBranchIdentity.Primary(
                    cycle.CraftDefinitionId),
                cycle.BranchId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Combat executable payload requires an exact primary craft witness.");
        }

        ProductionOutputClearanceExecutableInput[] orderedInputs = cycle
            .PhysicalInputs
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new ProductionOutputClearanceExecutableInput(
                value.Key,
                value.Value))
            .ToArray();
        ProductionOutputClearanceExecutableOutput[] orderedOutputs = (outputs
                ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (orderedInputs.Length == 0
            || orderedOutputs.Length == 0
            || orderedOutputs.Any(value => value == null)
            || orderedOutputs.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != orderedOutputs.Length
            || !orderedInputs.Any(value => string.Equals(
                value.ItemId,
                cycle.SelectedMaterialItemId,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Combat executable payload has incomplete physical witnesses.");
        }

        CraftDefinitionId = cycle.CraftDefinitionId;
        BranchId = cycle.BranchId;
        SelectedMaterialId = cycle.SelectedMaterialId;
        SelectedMaterialItemId = cycle.SelectedMaterialItemId;
        RequiredWork = cycle.RequiredWork;
        ExecutionPath = cycle.ExecutionPath;
        CycleSourceDigest = cycle.SourceDigest;
        Inputs = Array.AsReadOnly(orderedInputs);
        Outputs = Array.AsReadOnly(orderedOutputs);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-combat-craft-executable@1");
        digest.Append(CraftDefinitionId);
        digest.Append(BranchId);
        digest.Append(SelectedMaterialId);
        digest.Append(SelectedMaterialItemId);
        digest.AppendFloat(RequiredWork);
        digest.Append(ExecutionPath);
        digest.Append(CycleSourceDigest);
        digest.Append(Inputs.Count);
        foreach (ProductionOutputClearanceExecutableInput input in Inputs)
            input.AppendTo(digest);
        digest.Append(Outputs.Count);
        foreach (ProductionOutputClearanceExecutableOutput output in Outputs)
            output.AppendTo(digest);
        SourceDigest = digest.ComputeSha256();
    }

    public string PayloadKind => "combat-craft";
    public string CraftDefinitionId { get; }
    public string BranchId { get; }
    public string SelectedMaterialId { get; }
    public string SelectedMaterialItemId { get; }
    public float RequiredWork { get; }
    public string ExecutionPath { get; }
    public string CycleSourceDigest { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableInput> Inputs { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableOutput> Outputs { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceCombatCraftExecutableDescriptorContributor :
    IProductionOutputClearanceExecutableDescriptorContributor
{
    private readonly ICombatCraftCycleMaximumQuery cycles;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery masses;

    public ProductionOutputClearanceCombatCraftExecutableDescriptorContributor(
        ICombatCraftCycleMaximumQuery cycles,
        IProductionFacilityOutputCapacityBranchMassQuery masses)
    {
        this.cycles = cycles ?? throw new ArgumentNullException(nameof(cycles));
        this.masses = masses ?? throw new ArgumentNullException(nameof(masses));
    }

    public string MeasurementCapabilityId =>
        ProductionOutputClearanceMeasurementCapabilityIds.CombatCraft;
    public int ContractVersion => 1;

    public ProductionOutputClearanceExecutableDescriptorContribution Capture(
        ProductionOutputClearanceMeasurementPlan plan,
        ProductionOutputClearanceMeasurementScopeSnapshot scope)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (!string.Equals(plan.Winner.MeasurementCapabilityId,
                MeasurementCapabilityId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Combat executable contributor received an unowned plan.");

        ProductionOutputClearanceMeasurementFacilityContext context = scope
            .Contexts.SingleOrDefault(value => string.Equals(
                    value.DefinitionId, plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(value.WorkstationTag, plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionFacilityOutputCensusRow row = scope.AuthoredScope.Census.Rows
            .SingleOrDefault(value => string.Equals(
                    value.DefinitionId, plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(value.WorkstationTag, plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionFacilityOutputCapacityContribution contribution = context?
            .CapacityContributions.SingleOrDefault(value =>
                value.AppliesToFacility
                && string.Equals(value.ContributorId,
                    CombatCraftFacilityOutputCapacityContributor.Id,
                    StringComparison.Ordinal));
        if (context == null || row == null || contribution == null
            || contribution.ContractVersion
            != CombatCraftFacilityOutputCapacityContributor.Version
            || !string.Equals(plan.Winner.Source.SourceCapabilityId,
                contribution.ContributorId, StringComparison.Ordinal)
            || plan.Winner.Source.SourceCapabilityVersion
            != contribution.ContractVersion)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .CurrentSourceJoinMissing,
                    "combat-current-source-join-missing");
        }

        ProductionFacilityOutputCapacityBranch branch = contribution.Branches
            .SingleOrDefault(value => string.Equals(
                value.BranchId,
                plan.Winner.Source.BranchId,
                StringComparison.Ordinal));
        if (branch == null)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .SelectedBranchDrift,
                    "combat-selected-branch-missing");
        }

        ProductionFacilityOutputCapacityBranchMassSnapshot mass =
            masses.Capture(branch);
        CombatCraftCycleSnapshot cycle = cycles.Capture(branch.BranchId);
        string[] capabilities = mass.Projections
            .Select(value => value.Descriptor.CapabilityId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!string.Equals(mass.SourceDigest,
                plan.Winner.Source.UpstreamSourceDigest,
                StringComparison.Ordinal)
            || mass.MaximumMassGrams
            != plan.Winner.Source.MaximumSingleCompletionMassGrams
            || !capabilities.SequenceEqual(
                plan.Winner.Source.OutputCapabilityIds,
                StringComparer.Ordinal)
            || !string.Equals(cycle.BranchId, branch.BranchId,
                StringComparison.Ordinal))
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .SelectedBranchDrift,
                    "combat-selected-branch-drift");
        }

        ProductionOutputClearanceExecutableOutput[] outputs = mass.Projections
            .Select(value => new ProductionOutputClearanceExecutableOutput(
                value.Descriptor.OutputLineId,
                value.Descriptor.ItemId,
                value.MaximumQuantity,
                value.MaximumMassGrams,
                value.Descriptor,
                value.SourceDigest))
            .ToArray();
        ProductionOutputClearanceCombatCraftExecutablePayload payload;
        try
        {
            payload = new ProductionOutputClearanceCombatCraftExecutablePayload(
                cycle,
                outputs);
        }
        catch (Exception exception) when (exception is ArgumentException
            || exception is InvalidOperationException)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .ExecutionPayloadIncomplete,
                    "combat-executable-witness-incomplete");
        }
        return ProductionOutputClearanceExecutableDescriptorContribution.Complete(
            new ProductionOutputClearanceExecutableDescriptor(
                plan,
                row.SourceDigest,
                row.OutputBufferCycleCapacity,
                payload));
    }
}

public sealed class ProductionOutputClearanceApparelExecutablePayload :
    IProductionOutputClearanceExecutablePayload
{
    internal ProductionOutputClearanceApparelExecutablePayload(
        ApparelCraftCycleSnapshot cycle,
        IReadOnlyList<ProductionOutputClearanceExecutableOutput> outputs)
    {
        if (cycle == null) throw new ArgumentNullException(nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.ApparelId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.BranchId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.SelectedMaterialId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.SelectedPhysicalItemId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.ExecutionPath,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            cycle.SourceDigest,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            cycle.SelectedWitnessSourceDigest,
            nameof(cycle));
        if (!float.IsFinite(cycle.RequiredWork) || cycle.RequiredWork <= 0f
            || cycle.ExactMaterialQuantity <= 0
            || !string.Equals(
                ApparelFacilityOutputBranchIdentity.Craft(cycle.ApparelId),
                cycle.BranchId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Apparel executable payload requires an exact primary craft witness.");
        }

        ProductionOutputClearanceExecutableOutput[] orderedOutputs = (outputs
                ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (orderedOutputs.Length == 0
            || orderedOutputs.Any(value => value == null)
            || orderedOutputs.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != orderedOutputs.Length)
        {
            throw new InvalidOperationException(
                "Apparel executable payload has incomplete physical outputs.");
        }

        ApparelId = cycle.ApparelId;
        BranchId = cycle.BranchId;
        SelectedMaterialId = cycle.SelectedMaterialId;
        SelectedPhysicalItemId = cycle.SelectedPhysicalItemId;
        SelectedSize = cycle.SelectedSize;
        SelectedModifications = cycle.SelectedModifications;
        RequiredWork = cycle.RequiredWork;
        ExecutionPath = cycle.ExecutionPath;
        CycleSourceDigest = cycle.SourceDigest;
        SelectedWitnessSourceDigest = cycle.SelectedWitnessSourceDigest;
        Inputs = Array.AsReadOnly(new[]
        {
            new ProductionOutputClearanceExecutableInput(
                cycle.SelectedPhysicalItemId,
                cycle.ExactMaterialQuantity)
        });
        Outputs = Array.AsReadOnly(orderedOutputs);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-apparel-executable@1");
        digest.Append(ApparelId);
        digest.Append(BranchId);
        digest.Append(SelectedMaterialId);
        digest.Append(SelectedPhysicalItemId);
        digest.Append((int)SelectedSize);
        digest.Append((int)SelectedModifications);
        digest.AppendFloat(RequiredWork);
        digest.Append(ExecutionPath);
        digest.Append(CycleSourceDigest);
        digest.Append(SelectedWitnessSourceDigest);
        digest.Append(Inputs.Count);
        foreach (ProductionOutputClearanceExecutableInput input in Inputs)
            input.AppendTo(digest);
        digest.Append(Outputs.Count);
        foreach (ProductionOutputClearanceExecutableOutput output in Outputs)
            output.AppendTo(digest);
        SourceDigest = digest.ComputeSha256();
    }

    public string PayloadKind => "apparel";
    public string ApparelId { get; }
    public string BranchId { get; }
    public string SelectedMaterialId { get; }
    public string SelectedPhysicalItemId { get; }
    public ApparelSizeClass SelectedSize { get; }
    public ApparelModificationKind SelectedModifications { get; }
    public float RequiredWork { get; }
    public string ExecutionPath { get; }
    public string CycleSourceDigest { get; }
    public string SelectedWitnessSourceDigest { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableInput> Inputs { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableOutput> Outputs { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceApparelExecutableDescriptorContributor :
    IProductionOutputClearanceExecutableDescriptorContributor
{
    private readonly IApparelCraftCycleMaximumQuery cycles;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery masses;

    public ProductionOutputClearanceApparelExecutableDescriptorContributor(
        IApparelCraftCycleMaximumQuery cycles,
        IProductionFacilityOutputCapacityBranchMassQuery masses)
    {
        this.cycles = cycles ?? throw new ArgumentNullException(nameof(cycles));
        this.masses = masses ?? throw new ArgumentNullException(nameof(masses));
    }

    public string MeasurementCapabilityId =>
        ProductionOutputClearanceMeasurementCapabilityIds.Apparel;
    public int ContractVersion => 1;

    public ProductionOutputClearanceExecutableDescriptorContribution Capture(
        ProductionOutputClearanceMeasurementPlan plan,
        ProductionOutputClearanceMeasurementScopeSnapshot scope)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (!string.Equals(plan.Winner.MeasurementCapabilityId,
                MeasurementCapabilityId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Apparel executable contributor received an unowned plan.");

        ProductionOutputClearanceMeasurementFacilityContext context = scope
            .Contexts.SingleOrDefault(value => string.Equals(
                    value.DefinitionId, plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(value.WorkstationTag, plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionFacilityOutputCensusRow row = scope.AuthoredScope.Census.Rows
            .SingleOrDefault(value => string.Equals(
                    value.DefinitionId, plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(value.WorkstationTag, plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionFacilityOutputCapacityContribution contribution = context?
            .CapacityContributions.SingleOrDefault(value =>
                value.AppliesToFacility
                && string.Equals(value.ContributorId,
                    ApparelFacilityOutputCapacityContributor.Id,
                    StringComparison.Ordinal));
        if (context == null || row == null || contribution == null
            || contribution.ContractVersion
            != ApparelFacilityOutputCapacityContributor.Version
            || !string.Equals(plan.Winner.Source.SourceCapabilityId,
                contribution.ContributorId, StringComparison.Ordinal)
            || plan.Winner.Source.SourceCapabilityVersion
            != contribution.ContractVersion)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .CurrentSourceJoinMissing,
                    "apparel-current-source-join-missing");
        }

        ProductionFacilityOutputCapacityBranch branch = contribution.Branches
            .SingleOrDefault(value => string.Equals(
                value.BranchId,
                plan.Winner.Source.BranchId,
                StringComparison.Ordinal));
        if (branch == null)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .SelectedBranchDrift,
                    "apparel-selected-branch-missing");
        }

        ProductionFacilityOutputCapacityBranchMassSnapshot mass =
            masses.Capture(branch);
        ApparelCraftCycleSnapshot cycle = cycles.Capture(branch.BranchId);
        string[] capabilities = mass.Projections
            .Select(value => value.Descriptor.CapabilityId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!string.Equals(mass.SourceDigest,
                plan.Winner.Source.UpstreamSourceDigest,
                StringComparison.Ordinal)
            || mass.MaximumMassGrams
            != plan.Winner.Source.MaximumSingleCompletionMassGrams
            || !capabilities.SequenceEqual(
                plan.Winner.Source.OutputCapabilityIds,
                StringComparer.Ordinal)
            || !string.Equals(cycle.BranchId, branch.BranchId,
                StringComparison.Ordinal))
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .SelectedBranchDrift,
                    "apparel-selected-branch-drift");
        }

        ProductionOutputClearanceExecutableOutput[] outputs = mass.Projections
            .Select(value => new ProductionOutputClearanceExecutableOutput(
                value.Descriptor.OutputLineId,
                value.Descriptor.ItemId,
                value.MaximumQuantity,
                value.MaximumMassGrams,
                value.Descriptor,
                value.SourceDigest))
            .ToArray();
        ProductionOutputClearanceApparelExecutablePayload payload;
        try
        {
            payload = new ProductionOutputClearanceApparelExecutablePayload(
                cycle,
                outputs);
        }
        catch (Exception exception) when (exception is ArgumentException
            || exception is InvalidOperationException)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .ExecutionPayloadIncomplete,
                    "apparel-executable-witness-incomplete");
        }
        return ProductionOutputClearanceExecutableDescriptorContribution.Complete(
            new ProductionOutputClearanceExecutableDescriptor(
                plan,
                row.SourceDigest,
                row.OutputBufferCycleCapacity,
                payload));
    }
}

public sealed class ProductionOutputClearanceCropHarvestExecutablePayload :
    IProductionOutputClearanceExecutablePayload
{
    internal ProductionOutputClearanceCropHarvestExecutablePayload(
        CropHarvestCycleMaximumSnapshot cycle,
        CropCycleInputRequirementSnapshot input,
        IReadOnlyList<ProductionOutputClearanceExecutableOutput> outputs)
    {
        if (cycle == null) throw new ArgumentNullException(nameof(cycle));
        if (input == null) throw new ArgumentNullException(nameof(input));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.BranchId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.CropId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            cycle.SourceDigest,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            input.SourceDigest,
            nameof(input));
        if (!string.Equals(cycle.CropId, input.CropId, StringComparison.Ordinal)
            || cycle.Indoor != input.Indoor
            || !float.IsFinite(cycle.SowWork)
            || cycle.SowWork <= 0f
            || !float.IsFinite(cycle.HarvestWork)
            || cycle.HarvestWork <= 0f
            || cycle.GrowthHours <= 0m
            || cycle.MaximumSustainableGrowthRate <= 0m)
        {
            throw new InvalidOperationException(
                "Crop executable payload requires one exact reachable cycle.");
        }

        ProductionOutputClearanceExecutableInput[] orderedInputs = input
            .Requirements
            .Select(value => new ProductionOutputClearanceExecutableInput(
                value.Key,
                value.Value))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        ProductionOutputClearanceExecutableOutput[] orderedOutputs = (outputs
                ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        string expectedHarvestLine = CropHarvestOutputMaximumAuthority
            .HarvestOutputLineId(cycle.CropId);
        string expectedSeedLine = CropHarvestOutputMaximumAuthority
            .SeedOutputLineId(cycle.CropId);
        if (orderedInputs.Length == 0
            || orderedOutputs.Length != 2
            || orderedOutputs.Any(value => value == null)
            || !orderedOutputs.Any(value => string.Equals(
                value.OutputLineId,
                expectedHarvestLine,
                StringComparison.Ordinal))
            || !orderedOutputs.Any(value => string.Equals(
                value.OutputLineId,
                expectedSeedLine,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Crop executable payload requires exact input and two-line output vectors.");
        }

        BranchId = cycle.BranchId;
        CropId = cycle.CropId;
        Indoor = cycle.Indoor;
        SowWork = cycle.SowWork;
        HarvestWork = cycle.HarvestWork;
        GrowthHours = cycle.GrowthHours;
        MaximumSustainableGrowthRate = cycle.MaximumSustainableGrowthRate;
        EffectiveGrowthHours = cycle.EffectiveGrowthHours;
        Weather = input.Weather;
        MilestoneConsumptionMultiplier =
            input.MilestoneConsumptionMultiplier;
        SelectedFuelItemId = input.SelectedFuelItemId;
        CycleSourceDigest = cycle.SourceDigest;
        InputSourceDigest = input.SourceDigest;
        Inputs = Array.AsReadOnly(orderedInputs);
        Outputs = Array.AsReadOnly(orderedOutputs);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(
            "production-output-clearance-crop-harvest-executable-payload@1");
        digest.Append(BranchId);
        digest.Append(CropId);
        digest.Append(Indoor);
        digest.AppendFloat(SowWork);
        digest.AppendFloat(HarvestWork);
        digest.Append(GrowthHours.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        digest.Append(MaximumSustainableGrowthRate.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        digest.Append(EffectiveGrowthHours.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        digest.AppendEnum(Weather);
        digest.AppendFloat(MilestoneConsumptionMultiplier);
        digest.Append(SelectedFuelItemId);
        digest.Append(CycleSourceDigest);
        digest.Append(InputSourceDigest);
        digest.Append(Inputs.Count);
        foreach (ProductionOutputClearanceExecutableInput value in Inputs)
            value.AppendTo(digest);
        digest.Append(Outputs.Count);
        foreach (ProductionOutputClearanceExecutableOutput value in Outputs)
            value.AppendTo(digest);
        SourceDigest = digest.ComputeSha256();
    }

    public string PayloadKind => "crop-harvest";
    public string BranchId { get; }
    public string CropId { get; }
    public bool Indoor { get; }
    public float SowWork { get; }
    public float HarvestWork { get; }
    public decimal GrowthHours { get; }
    public decimal MaximumSustainableGrowthRate { get; }
    public decimal EffectiveGrowthHours { get; }
    public SurvivalWeatherType Weather { get; }
    public float MilestoneConsumptionMultiplier { get; }
    public string SelectedFuelItemId { get; }
    public string CycleSourceDigest { get; }
    public string InputSourceDigest { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableInput> Inputs
        { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableOutput> Outputs
        { get; }
    public string SourceDigest { get; }
}

public sealed class
    ProductionOutputClearanceCropHarvestExecutableDescriptorContributor :
    IProductionOutputClearanceExecutableDescriptorContributor
{
    private readonly ICropHarvestCycleMaximumQuery cycles;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery masses;
    private readonly ICropCycleInputRequirementQuery inputRequirements;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IGameContentDefinitionSource content;

    public ProductionOutputClearanceCropHarvestExecutableDescriptorContributor(
        ICropHarvestCycleMaximumQuery cycles,
        IProductionFacilityOutputCapacityBranchMassQuery masses,
        ICropCycleInputRequirementQuery inputRequirements,
        IResourceEconomyContentCatalog catalog,
        IGameContentDefinitionSource content)
    {
        this.cycles = cycles ?? throw new ArgumentNullException(nameof(cycles));
        this.masses = masses ?? throw new ArgumentNullException(nameof(masses));
        this.inputRequirements = inputRequirements
            ?? throw new ArgumentNullException(nameof(inputRequirements));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public string MeasurementCapabilityId =>
        ProductionOutputClearanceMeasurementCapabilityIds.CropHarvest;
    public int ContractVersion => 1;

    public ProductionOutputClearanceExecutableDescriptorContribution Capture(
        ProductionOutputClearanceMeasurementPlan plan,
        ProductionOutputClearanceMeasurementScopeSnapshot scope)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (!string.Equals(
                plan.Winner.MeasurementCapabilityId,
                MeasurementCapabilityId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop executable contributor received an unowned plan.");
        }

        ProductionOutputClearanceMeasurementFacilityContext context = scope
            .Contexts.SingleOrDefault(value => string.Equals(
                    value.DefinitionId,
                    plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.WorkstationTag,
                    plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionFacilityOutputCensusRow row = scope.AuthoredScope.Census.Rows
            .SingleOrDefault(value => string.Equals(
                    value.DefinitionId,
                    plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.WorkstationTag,
                    plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionFacilityOutputCapacityContribution contribution = context?
            .CapacityContributions.SingleOrDefault(value =>
                value.AppliesToFacility
                && string.Equals(
                    value.ContributorId,
                    CropHarvestFacilityOutputCapacityContributor.Id,
                    StringComparison.Ordinal));
        if (context == null || row == null || contribution == null
            || contribution.ContractVersion
                != CropHarvestFacilityOutputCapacityContributor.Version
            || !string.Equals(
                plan.Winner.Source.SourceCapabilityId,
                contribution.ContributorId,
                StringComparison.Ordinal)
            || plan.Winner.Source.SourceCapabilityVersion
                != contribution.ContractVersion)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .CurrentSourceJoinMissing,
                    "crop-current-source-join-missing");
        }

        ProductionFacilityOutputCapacityBranch branch = contribution.Branches
            .SingleOrDefault(value => string.Equals(
                value.BranchId,
                plan.Winner.Source.BranchId,
                StringComparison.Ordinal));
        if (branch == null)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .SelectedBranchDrift,
                    "crop-selected-branch-missing");
        }

        ProductionFacilityOutputCapacityBranchMassSnapshot mass =
            masses.Capture(branch);
        CropHarvestCycleMaximumSnapshot cycle = cycles.Capture(
            plan.DefinitionId,
            branch.BranchId);
        string[] capabilities = mass.Projections
            .Select(value => value.Descriptor.CapabilityId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!string.Equals(
                mass.SourceDigest,
                plan.Winner.Source.UpstreamSourceDigest,
                StringComparison.Ordinal)
            || mass.MaximumMassGrams
                != plan.Winner.Source.MaximumSingleCompletionMassGrams
            || !capabilities.SequenceEqual(
                plan.Winner.Source.OutputCapabilityIds,
                StringComparer.Ordinal)
            || !string.Equals(
                cycle.BranchId,
                branch.BranchId,
                StringComparison.Ordinal))
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .SelectedBranchDrift,
                    "crop-selected-branch-drift");
        }

        CropDefinitionSO crop = catalog.Crops.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.CropId,
                cycle.CropId,
                StringComparison.Ordinal));
        BuildingSO definition = content.GetAll<BuildingSO>()
            .SingleOrDefault(value => value != null
                && value.GetAbility<BuildingCropPlotAbility>() != null
                && string.Equals(
                    ProductionFacilityDefinitionIdentity.Resolve(value),
                    plan.DefinitionId,
                    StringComparison.Ordinal));
        BuildingCropPlotAbility ability = definition?
            .GetAbility<BuildingCropPlotAbility>();
        if (crop == null || ability == null || ability.Indoor != cycle.Indoor)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .CurrentSourceJoinMissing,
                    "crop-definition-or-ability-missing");
        }

        ProductionOutputClearanceExecutableOutput[] outputs = mass.Projections
            .Select(value => new ProductionOutputClearanceExecutableOutput(
                value.Descriptor.OutputLineId,
                value.Descriptor.ItemId,
                value.MaximumQuantity,
                value.MaximumMassGrams,
                value.Descriptor,
                value.SourceDigest))
            .ToArray();
        try
        {
            CropCycleInputRequirementSnapshot input = inputRequirements.Capture(
                crop,
                ability,
                string.Empty,
                ability.Indoor
                    ? SurvivalWeatherType.Clear
                    : SurvivalWeatherType.Rain,
                1f,
                countAvailableStock: null);
            ProductionOutputClearanceCropHarvestExecutablePayload payload = new(
                cycle,
                input,
                outputs);
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Complete(new ProductionOutputClearanceExecutableDescriptor(
                    plan,
                    row.SourceDigest,
                    row.OutputBufferCycleCapacity,
                    payload));
        }
        catch (Exception exception) when (exception is ArgumentException
            || exception is InvalidOperationException)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .ExecutionPayloadIncomplete,
                    "crop-executable-witness-incomplete");
        }
    }
}

public sealed class ProductionOutputClearanceCertifiedSeedExecutablePayload :
    IProductionOutputClearanceExecutablePayload
{
    internal ProductionOutputClearanceCertifiedSeedExecutablePayload(
        CertifiedSeedCycleSnapshot cycle,
        IReadOnlyList<ProductionOutputClearanceExecutableOutput> outputs)
    {
        if (cycle == null) throw new ArgumentNullException(nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.BranchId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.CropId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.SeedItemId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.CertificationKitItemId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            cycle.TransformContractId,
            nameof(cycle));
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            cycle.SourceDigest,
            nameof(cycle));
        if (cycle.SeedInputQuantity <= 0
            || cycle.CertificationKitInputQuantity <= 0
            || cycle.OutputQuantity <= 0
            || !float.IsFinite(cycle.PathogenLoadReduction)
            || cycle.PathogenLoadReduction <= 0f
            || cycle.PathogenLoadReduction > 100f
            || !string.Equals(
                CertifiedSeedFacilityOutputBranchIdentity.ForCrop(cycle.CropId),
                cycle.BranchId,
                StringComparison.Ordinal)
            || cycle.InputSeedLot == null
            || cycle.OutputSeedLot == null
            || !string.Equals(
                cycle.InputSeedLot.CropId,
                cycle.CropId,
                StringComparison.Ordinal)
            || !string.Equals(
                cycle.OutputSeedLot.CropId,
                cycle.CropId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Certified-seed executable payload requires an exact transform witness.");
        }

        ProductionOutputClearanceExecutableOutput[] orderedOutputs = (outputs
                ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (orderedOutputs.Length != 1
            || orderedOutputs[0] == null
            || !string.Equals(
                orderedOutputs[0].OutputLineId,
                CertifiedSeedOutputCapability.OutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                orderedOutputs[0].ItemId,
                cycle.SeedItemId,
                StringComparison.Ordinal)
            || orderedOutputs[0].Quantity != cycle.OutputQuantity
            || !string.Equals(
                orderedOutputs[0].Descriptor.CapabilityId,
                ProductionOutputCapabilityIds.CertifiedSeed,
                StringComparison.Ordinal)
            || !string.Equals(
                orderedOutputs[0].Descriptor.ComponentCodecId,
                ProductionOutputCapabilityIds.SeedLotStateCodec,
                StringComparison.Ordinal)
            || orderedOutputs[0].Descriptor.ComponentCodecVersion
                != ProductionOutputCapabilityIds.SeedLotStateCodecVersion)
        {
            throw new InvalidOperationException(
                "Certified-seed executable output drifted from its transform contract.");
        }

        BranchId = cycle.BranchId;
        CropId = cycle.CropId;
        SeedItemId = cycle.SeedItemId;
        CertificationKitItemId = cycle.CertificationKitItemId;
        OperatingHoursPerCycle = cycle.OperatingHoursPerCycle;
        PathogenLoadReduction = cycle.PathogenLoadReduction;
        TransformContractId = cycle.TransformContractId;
        CycleSourceDigest = cycle.SourceDigest;
        InputSeedLot = cycle.InputSeedLot;
        OutputSeedLot = cycle.OutputSeedLot;
        Inputs = Array.AsReadOnly(new[]
        {
            new ProductionOutputClearanceExecutableInput(
                cycle.SeedItemId,
                cycle.SeedInputQuantity),
            new ProductionOutputClearanceExecutableInput(
                cycle.CertificationKitItemId,
                cycle.CertificationKitInputQuantity)
        }.OrderBy(value => value.ItemId, StringComparer.Ordinal).ToArray());
        Outputs = Array.AsReadOnly(orderedOutputs);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(
            "production-output-clearance-certified-seed-executable@1");
        digest.Append(BranchId);
        digest.Append(CropId);
        digest.Append(SeedItemId);
        digest.Append(CertificationKitItemId);
        digest.Append(OperatingHoursPerCycle);
        digest.AppendFloat(PathogenLoadReduction);
        digest.Append(TransformContractId);
        digest.Append(CycleSourceDigest);
        digest.Append(InputSeedLot.SourceDigest);
        digest.Append(OutputSeedLot.SourceDigest);
        digest.Append(Inputs.Count);
        foreach (ProductionOutputClearanceExecutableInput input in Inputs)
            input.AppendTo(digest);
        digest.Append(Outputs.Count);
        foreach (ProductionOutputClearanceExecutableOutput output in Outputs)
            output.AppendTo(digest);
        SourceDigest = digest.ComputeSha256();
    }

    public string PayloadKind => "certified-seed";
    public string BranchId { get; }
    public string CropId { get; }
    public string SeedItemId { get; }
    public string CertificationKitItemId { get; }
    public int OperatingHoursPerCycle { get; }
    public float PathogenLoadReduction { get; }
    public string TransformContractId { get; }
    public string CycleSourceDigest { get; }
    public CertifiedSeedLotWitnessSnapshot InputSeedLot { get; }
    public CertifiedSeedLotWitnessSnapshot OutputSeedLot { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableInput> Inputs
        { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutableOutput> Outputs
        { get; }
    public string SourceDigest { get; }
}

public sealed class
    ProductionOutputClearanceCertifiedSeedExecutableDescriptorContributor :
    IProductionOutputClearanceExecutableDescriptorContributor
{
    private readonly ICertifiedSeedCycleMaximumQuery cycles;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery masses;

    public ProductionOutputClearanceCertifiedSeedExecutableDescriptorContributor(
        ICertifiedSeedCycleMaximumQuery cycles,
        IProductionFacilityOutputCapacityBranchMassQuery masses)
    {
        this.cycles = cycles ?? throw new ArgumentNullException(nameof(cycles));
        this.masses = masses ?? throw new ArgumentNullException(nameof(masses));
    }

    public string MeasurementCapabilityId =>
        ProductionOutputClearanceMeasurementCapabilityIds.CertifiedSeed;
    public int ContractVersion => 1;

    public ProductionOutputClearanceExecutableDescriptorContribution Capture(
        ProductionOutputClearanceMeasurementPlan plan,
        ProductionOutputClearanceMeasurementScopeSnapshot scope)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (!string.Equals(
                plan.Winner.MeasurementCapabilityId,
                MeasurementCapabilityId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Certified-seed executable contributor received an unowned plan.");
        }

        ProductionOutputClearanceMeasurementFacilityContext context = scope
            .Contexts.SingleOrDefault(value => string.Equals(
                    value.DefinitionId,
                    plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.WorkstationTag,
                    plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionFacilityOutputCensusRow row = scope.AuthoredScope.Census.Rows
            .SingleOrDefault(value => string.Equals(
                    value.DefinitionId,
                    plan.DefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.WorkstationTag,
                    plan.WorkstationTag,
                    StringComparison.Ordinal));
        ProductionFacilityOutputCapacityContribution contribution = context?
            .CapacityContributions.SingleOrDefault(value =>
                value.AppliesToFacility
                && string.Equals(
                    value.ContributorId,
                    CertifiedSeedFacilityOutputCapacityContributor.Id,
                    StringComparison.Ordinal));
        if (context == null || row == null || contribution == null
            || contribution.ContractVersion
                != CertifiedSeedFacilityOutputCapacityContributor.Version
            || !string.Equals(
                plan.Winner.Source.SourceCapabilityId,
                contribution.ContributorId,
                StringComparison.Ordinal)
            || plan.Winner.Source.SourceCapabilityVersion
                != contribution.ContractVersion)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .CurrentSourceJoinMissing,
                    "certified-seed-current-source-join-missing");
        }

        ProductionFacilityOutputCapacityBranch branch = contribution.Branches
            .SingleOrDefault(value => string.Equals(
                value.BranchId,
                plan.Winner.Source.BranchId,
                StringComparison.Ordinal));
        if (branch == null)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .SelectedBranchDrift,
                    "certified-seed-selected-branch-missing");
        }

        ProductionFacilityOutputCapacityBranchMassSnapshot mass =
            masses.Capture(branch);
        CertifiedSeedCycleSnapshot cycle = cycles.Capture(branch.BranchId);
        string[] capabilities = mass.Projections
            .Select(value => value.Descriptor.CapabilityId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!string.Equals(
                mass.SourceDigest,
                plan.Winner.Source.UpstreamSourceDigest,
                StringComparison.Ordinal)
            || mass.MaximumMassGrams
                != plan.Winner.Source.MaximumSingleCompletionMassGrams
            || !capabilities.SequenceEqual(
                plan.Winner.Source.OutputCapabilityIds,
                StringComparer.Ordinal)
            || !string.Equals(
                cycle.BranchId,
                branch.BranchId,
                StringComparison.Ordinal))
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .SelectedBranchDrift,
                    "certified-seed-selected-branch-drift");
        }

        ProductionOutputClearanceExecutableOutput[] outputs = mass.Projections
            .Select(value => new ProductionOutputClearanceExecutableOutput(
                value.Descriptor.OutputLineId,
                value.Descriptor.ItemId,
                value.MaximumQuantity,
                value.MaximumMassGrams,
                value.Descriptor,
                value.SourceDigest))
            .ToArray();
        ProductionOutputClearanceCertifiedSeedExecutablePayload payload;
        try
        {
            payload =
                new ProductionOutputClearanceCertifiedSeedExecutablePayload(
                    cycle,
                    outputs);
        }
        catch (Exception exception) when (exception is ArgumentException
            || exception is InvalidOperationException)
        {
            return ProductionOutputClearanceExecutableDescriptorContribution
                .Incomplete(
                    plan,
                    ProductionOutputClearanceExecutableDescriptorGapReason
                        .ExecutionPayloadIncomplete,
                    "certified-seed-executable-witness-incomplete");
        }
        return ProductionOutputClearanceExecutableDescriptorContribution
            .Complete(new ProductionOutputClearanceExecutableDescriptor(
                plan,
                row.SourceDigest,
                row.OutputBufferCycleCapacity,
                payload));
    }
}
