using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ProductionOutputClearanceMeasurementScopeGapReason
{
    RecipeBranchAuthorityIncomplete = 1,
    MeasurementPlanIncomplete = 2
}

public static class ProductionOutputClearanceMeasurementCapabilityIds
{
    public const string Recipe =
        "production-output-clearance-measurement:recipe";
    public const string CropHarvest =
        "production-output-clearance-measurement:crop-harvest";
    public const string Apparel =
        "production-output-clearance-measurement:apparel";
    public const string CombatCraft =
        "production-output-clearance-measurement:combat-craft";
    public const string CertifiedSeed =
        "production-output-clearance-measurement:certified-seed";
}

public static class ProductionOutputClearanceMeasurementContributorIds
{
    public const string Recipe =
        "production-output-clearance-measurement-owner:recipe";
    public const string CropHarvest =
        "production-output-clearance-measurement-owner:crop-harvest";
    public const string Apparel =
        "production-output-clearance-measurement-owner:apparel";
    public const string CombatCraft =
        "production-output-clearance-measurement-owner:combat-craft";
    public const string CertifiedSeed =
        "production-output-clearance-measurement-owner:certified-seed";

    public static IReadOnlyList<
        IProductionOutputClearanceMeasurementPlanContributor> CreateCurrent()
    {
        return Array.AsReadOnly(new
            IProductionOutputClearanceMeasurementPlanContributor[]
            {
                Bind(
                    Recipe,
                    ProductionOutputClearanceMeasurementPlanRegistry
                        .RecipeSourceCapabilityId,
                    ProductionOutputClearanceMeasurementPlanRegistry
                        .RecipeSourceCapabilityVersion,
                    ProductionOutputClearanceMeasurementCapabilityIds.Recipe),
                Bind(
                    CropHarvest,
                    CropHarvestFacilityOutputCapacityContributor.Id,
                    CropHarvestFacilityOutputCapacityContributor.Version,
                    ProductionOutputClearanceMeasurementCapabilityIds
                        .CropHarvest),
                Bind(
                    Apparel,
                    ApparelFacilityOutputCapacityContributor.Id,
                    ApparelFacilityOutputCapacityContributor.Version,
                    ProductionOutputClearanceMeasurementCapabilityIds.Apparel),
                Bind(
                    CombatCraft,
                    CombatCraftFacilityOutputCapacityContributor.Id,
                    CombatCraftFacilityOutputCapacityContributor.Version,
                    ProductionOutputClearanceMeasurementCapabilityIds
                        .CombatCraft),
                Bind(
                    CertifiedSeed,
                    CertifiedSeedFacilityOutputCapacityContributor.Id,
                    CertifiedSeedFacilityOutputCapacityContributor.Version,
                    ProductionOutputClearanceMeasurementCapabilityIds
                        .CertifiedSeed)
            });
    }

    private static ProductionOutputClearanceMeasurementPlanContributor Bind(
        string contributorId,
        string sourceCapabilityId,
        int sourceCapabilityVersion,
        string measurementCapabilityId) => new(
        contributorId,
        1,
        sourceCapabilityId,
        sourceCapabilityVersion,
        measurementCapabilityId);
}

public sealed class ProductionOutputClearanceMeasurementScopeGap
{
    internal ProductionOutputClearanceMeasurementScopeGap(
        string definitionId,
        string workstationTag,
        string producerId,
        string branchId,
        ProductionOutputClearanceMeasurementScopeGapReason reason,
        string detail,
        string upstreamSourceDigest)
    {
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            definitionId,
            nameof(definitionId));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
            producerId,
            nameof(producerId));
        if (!string.IsNullOrEmpty(branchId))
        {
            ProductionOutputClearanceMeasurementSourceBranch.RequireCanonical(
                branchId,
                nameof(branchId));
        }
        if (!Enum.IsDefined(typeof(
                ProductionOutputClearanceMeasurementScopeGapReason), reason)
            || string.IsNullOrEmpty(detail)
            || !string.Equals(detail, detail.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Clearance measurement scope gap is invalid.");
        }
        ProductionOutputClearanceMeasurementSourceBranch.RequireDigest(
            upstreamSourceDigest,
            nameof(upstreamSourceDigest));

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        ProducerId = producerId;
        BranchId = branchId ?? string.Empty;
        Reason = reason;
        Detail = detail;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-measurement-scope-gap@1");
        digest.Append(DefinitionId);
        digest.Append(WorkstationTag);
        digest.Append(ProducerId);
        digest.Append(BranchId);
        digest.Append((int)Reason);
        digest.Append(Detail);
        digest.Append(upstreamSourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public string ProducerId { get; }
    public string BranchId { get; }
    public ProductionOutputClearanceMeasurementScopeGapReason Reason { get; }
    public string Detail { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceMeasurementScopeSnapshot
{
    internal ProductionOutputClearanceMeasurementScopeSnapshot(
        ProductionAuthoredThroughputFacilityScopeSnapshot authoredScope,
        IReadOnlyList<ProductionOutputClearanceMeasurementFacilityContext>
            contexts,
        IReadOnlyList<ProductionOutputClearanceMeasurementPlan> plans,
        IReadOnlyList<ProductionOutputClearanceMeasurementScopeGap> gaps)
    {
        AuthoredScope = authoredScope
            ?? throw new ArgumentNullException(nameof(authoredScope));
        ProductionOutputClearanceMeasurementFacilityContext[] orderedContexts =
            (contexts ?? throw new ArgumentNullException(nameof(contexts)))
            .OrderBy(value => value?.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value?.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        ProductionOutputClearanceMeasurementPlan[] orderedPlans = (plans
                ?? throw new ArgumentNullException(nameof(plans)))
            .OrderBy(value => value?.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value?.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        ProductionOutputClearanceMeasurementScopeGap[] orderedGaps = (gaps
                ?? throw new ArgumentNullException(nameof(gaps)))
            .OrderBy(value => value?.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value?.WorkstationTag, StringComparer.Ordinal)
            .ThenBy(value => value?.ProducerId, StringComparer.Ordinal)
            .ThenBy(value => value?.BranchId, StringComparer.Ordinal)
            .ThenBy(value => (int)value.Reason)
            .ToArray();
        if (orderedContexts.Any(value => value == null)
            || orderedPlans.Any(value => value == null)
            || orderedGaps.Any(value => value == null)
            || orderedContexts.Select(Key).Distinct(StringComparer.Ordinal)
                .Count() != orderedContexts.Length
            || orderedPlans.Select(Key).Distinct(StringComparer.Ordinal)
                .Count() != orderedPlans.Length
            || orderedGaps.Select(value => value.SourceDigest)
                .Distinct(StringComparer.Ordinal).Count() != orderedGaps.Length)
        {
            throw new InvalidOperationException(
                "Clearance measurement scope contains null or duplicate records.");
        }

        string[] expected = AuthoredScope.Facilities.Select(Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] accounted = orderedPlans.Select(Key)
            .Concat(orderedGaps.Select(value => Key(
                value.DefinitionId,
                value.WorkstationTag)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!accounted.SequenceEqual(expected, StringComparer.Ordinal)
            || orderedPlans.Any(plan => !orderedContexts.Any(context =>
                string.Equals(Key(context), Key(plan),
                    StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "Clearance measurement plans and typed gaps do not exactly account for the authored producer scope.");
        }

        Contexts = Array.AsReadOnly(orderedContexts);
        Plans = Array.AsReadOnly(orderedPlans);
        Gaps = Array.AsReadOnly(orderedGaps);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-measurement-scope@1");
        digest.Append(AuthoredScope.SourceDigest);
        digest.Append(Contexts.Count);
        foreach (ProductionOutputClearanceMeasurementFacilityContext context in
                 Contexts)
            digest.Append(context.SourceDigest);
        digest.Append(Plans.Count);
        foreach (ProductionOutputClearanceMeasurementPlan plan in Plans)
            digest.Append(plan.SourceDigest);
        digest.Append(Gaps.Count);
        foreach (ProductionOutputClearanceMeasurementScopeGap gap in Gaps)
            digest.Append(gap.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionAuthoredThroughputFacilityScopeSnapshot AuthoredScope
        { get; }
    public IReadOnlyList<ProductionOutputClearanceMeasurementFacilityContext>
        Contexts { get; }
    public IReadOnlyList<ProductionOutputClearanceMeasurementPlan> Plans { get; }
    public IReadOnlyList<ProductionOutputClearanceMeasurementScopeGap> Gaps
        { get; }
    public string SourceDigest { get; }

    private static string Key(
        ProductionAuthoredThroughputFacilitySubject value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(
        ProductionOutputClearanceMeasurementFacilityContext value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(ProductionOutputClearanceMeasurementPlan value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(string definitionId, string workstationTag) =>
        definitionId + "\n" + workstationTag;
}

public interface IProductionOutputClearanceMeasurementScopeQuery
{
    ProductionOutputClearanceMeasurementScopeSnapshot Capture();
}

/// <summary>
/// Projects the shared live-producer scope into capability-preserving natural
/// measurement plans. New producers join through parameter content plus a
/// declarative source-capability owner; no facility-ID switch or generic item
/// fallback is used.
/// </summary>
public sealed class ProductionOutputClearanceMeasurementScopeAuthority :
    IProductionOutputClearanceMeasurementScopeQuery
{
    private readonly IProductionAuthoredThroughputFacilityScopeQuery
        authoredScope;
    private readonly ProductionFacilityDefinitionCatalog facilityDefinitions;
    private readonly IProductionFacilityOutputCapacityContributorRegistry
        capacityContributors;
    private readonly IProductionMaximumOutputFactorCatalog maximumFactors;
    private readonly IProductionRecipeThroughputBranchQuery recipeBranches;
    private readonly IProductionOutputClearanceMeasurementPlanQuery plans;

    public ProductionOutputClearanceMeasurementScopeAuthority(
        IProductionAuthoredThroughputFacilityScopeQuery authoredScope,
        ProductionFacilityDefinitionCatalog facilityDefinitions,
        IProductionFacilityOutputCapacityContributorRegistry capacityContributors,
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IProductionRecipeThroughputBranchQuery recipeBranches,
        IProductionOutputClearanceMeasurementPlanQuery plans)
    {
        this.authoredScope = authoredScope
            ?? throw new ArgumentNullException(nameof(authoredScope));
        this.facilityDefinitions = facilityDefinitions
            ?? throw new ArgumentNullException(nameof(facilityDefinitions));
        this.capacityContributors = capacityContributors
            ?? throw new ArgumentNullException(nameof(capacityContributors));
        this.maximumFactors = maximumFactors
            ?? throw new ArgumentNullException(nameof(maximumFactors));
        this.recipeBranches = recipeBranches
            ?? throw new ArgumentNullException(nameof(recipeBranches));
        this.plans = plans ?? throw new ArgumentNullException(nameof(plans));
    }

    public ProductionOutputClearanceMeasurementScopeSnapshot Capture()
    {
        ProductionAuthoredThroughputFacilityScopeSnapshot scope =
            authoredScope.Capture();
        Dictionary<string, ProductionFacilityOutputCensusRow> rows =
            scope.Census.Rows.Where(value => value.IsAutomaticProducer)
                .ToDictionary(Key, StringComparer.Ordinal);
        List<ProductionOutputClearanceMeasurementFacilityContext> contexts =
            new(scope.Facilities.Count);
        List<ProductionOutputClearanceMeasurementPlan> completePlans =
            new(scope.Facilities.Count);
        List<ProductionOutputClearanceMeasurementScopeGap> gaps = new();

        foreach (ProductionAuthoredThroughputFacilitySubject facility in
                 scope.Facilities)
        {
            if (!rows.TryGetValue(Key(facility),
                    out ProductionFacilityOutputCensusRow row))
            {
                throw new InvalidOperationException(
                    "Clearance measurement facility is absent from the producer census: "
                    + facility.DefinitionId);
            }

            ProductionOutputClearanceRecipeMeasurementBranch[] recipeSources =
                CaptureRecipeSources(facility, gaps);
            if (gaps.Any(value => string.Equals(
                    Key(value.DefinitionId, value.WorkstationTag),
                    Key(facility),
                    StringComparison.Ordinal)))
            {
                continue;
            }

            ProductionFacilityOutputCapacityContribution[] capacitySources =
                CaptureCapacitySources(facility, row);
            ProductionOutputClearanceMeasurementFacilityContext context = new(
                facility.DefinitionId,
                facility.WorkstationTag,
                recipeSources,
                capacitySources);
            contexts.Add(context);

            ProductionOutputClearanceMeasurementPlanResult result =
                plans.Capture(context);
            if (result.IsComplete)
            {
                completePlans.Add(result.Plan);
                continue;
            }

            foreach (ProductionOutputClearanceMeasurementGap gap in result.Gaps)
            {
                gaps.Add(new ProductionOutputClearanceMeasurementScopeGap(
                    facility.DefinitionId,
                    facility.WorkstationTag,
                    gap.Source.ProducerId,
                    gap.Source.BranchId,
                    ProductionOutputClearanceMeasurementScopeGapReason
                        .MeasurementPlanIncomplete,
                    gap.Reason + ":" + gap.Detail,
                    gap.SourceDigest));
            }
        }

        return new ProductionOutputClearanceMeasurementScopeSnapshot(
            scope,
            contexts,
            completePlans,
            gaps);
    }

    private ProductionOutputClearanceRecipeMeasurementBranch[]
        CaptureRecipeSources(
            ProductionAuthoredThroughputFacilitySubject facility,
            ICollection<ProductionOutputClearanceMeasurementScopeGap> gaps)
    {
        List<ProductionOutputClearanceRecipeMeasurementBranch> captured = new();
        foreach (ProductionRecipeSO recipe in facility.Recipes)
        {
            IReadOnlyList<ProductionAuthoredSupportAssignmentSnapshot>
                assignments = maximumFactors.CaptureFeasibleAssignments(recipe);
            foreach (ProductionAuthoredSupportAssignmentSnapshot assignment in
                     assignments)
            {
                ProductionRecipeThroughputBranchQueryResult result =
                    recipeBranches.Capture(
                        recipe,
                        facility.ProcessFluidProfile,
                        assignment);
                if (!result.IsComplete)
                {
                    gaps.Add(new ProductionOutputClearanceMeasurementScopeGap(
                        facility.DefinitionId,
                        facility.WorkstationTag,
                        recipe.RecipeId,
                        string.Empty,
                        ProductionOutputClearanceMeasurementScopeGapReason
                            .RecipeBranchAuthorityIncomplete,
                        result.MissingReason + ":" + result.Detail,
                        result.SourceDigest));
                    continue;
                }
                captured.AddRange(result.Branches.Select(branch =>
                    new ProductionOutputClearanceRecipeMeasurementBranch(
                        branch.RecipeId,
                        branch.BranchId,
                        branch.MaximumOutputMassGrams,
                        branch.OutputCapabilityIds,
                        branch.SourceDigest)));
            }
        }
        return captured.OrderBy(value => value.Source.ProducerId,
                StringComparer.Ordinal)
            .ThenBy(value => value.Source.BranchId, StringComparer.Ordinal)
            .ThenBy(value => value.Source.SourceDigest, StringComparer.Ordinal)
            .ToArray();
    }

    private ProductionFacilityOutputCapacityContribution[]
        CaptureCapacitySources(
            ProductionAuthoredThroughputFacilitySubject facility,
            ProductionFacilityOutputCensusRow row)
    {
        if (facility.Recipes.Count > 0)
        {
            if (row.CapacityContributorIds.Count != 0)
                throw new InvalidOperationException(
                    "Recipe-only clearance facility unexpectedly has a capacity contributor: "
                    + facility.DefinitionId);
            return Array.Empty<ProductionFacilityOutputCapacityContribution>();
        }

        BuildingSO definition = facilityDefinitions.Require(
            facility.DefinitionId);
        ProductionFacilityCapacitySubject subject = new(
            (BuildingInstanceId)("building:audit-output-clearance:"
                + facility.DefinitionId),
            Vector2Int.zero,
            facility.DefinitionId,
            facility.WorkstationTag,
            row.OutputBufferCycleCapacity,
            ProductionFacilityCapacitySubjectAdapter
                .CaptureWorkstationLaneProfile(definition),
            ProductionFacilityCapacitySubjectAdapter
                .CaptureProcessFluidProfile(definition));
        ProductionFacilityOutputCapacityContribution[] applicable =
            capacityContributors.CaptureContributions(subject)
                .Where(value => value.AppliesToFacility)
                .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
                .ToArray();
        string[] actualIds = applicable.Select(value => value.ContributorId)
            .ToArray();
        string[] actualDigests = applicable.Select(value => value.SourceDigest)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actualIds.SequenceEqual(
                row.CapacityContributorIds.OrderBy(value => value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal)
            || !actualDigests.SequenceEqual(
                row.CapacityContributionSourceDigests.OrderBy(value => value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Clearance measurement capacity contribution drifted from the census: "
                + facility.DefinitionId);
        }
        return applicable;
    }

    private static string Key(ProductionFacilityOutputCensusRow value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(
        ProductionAuthoredThroughputFacilitySubject value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(string definitionId, string workstationTag) =>
        definitionId + "\n" + workstationTag;
}
