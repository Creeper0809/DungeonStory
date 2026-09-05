using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Immutable current-source join between the producer census, the exact
/// facility subjects consumed by the authored-throughput projector, and the
/// resulting envelope publication. Current producer counts are deliberately
/// not authored here: adding parameter content expands this scope through the
/// content catalog and census without a core-code allowlist change.
/// </summary>
public sealed class ProductionAuthoredThroughputFacilityScopeSnapshot
{
    internal ProductionAuthoredThroughputFacilityScopeSnapshot(
        ProductionFacilityOutputCensusSnapshot census,
        IReadOnlyList<ProductionAuthoredThroughputFacilitySubject> facilities,
        ProductionAuthoredThroughputCoverageSnapshot coverage)
    {
        Census = census ?? throw new ArgumentNullException(nameof(census));
        Coverage = coverage ?? throw new ArgumentNullException(nameof(coverage));

        ProductionAuthoredThroughputFacilitySubject[] orderedFacilities =
            (facilities ?? throw new ArgumentNullException(nameof(facilities)))
            .OrderBy(value => value?.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value?.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        if (orderedFacilities.Length == 0
            || orderedFacilities.Any(value => value == null)
            || orderedFacilities.Select(Key)
                .Distinct(StringComparer.Ordinal).Count()
                != orderedFacilities.Length)
        {
            throw new InvalidOperationException(
                "Authored throughput facility scope is empty or duplicated.");
        }

        string[] expectedKeys = Census.Rows
            .Where(value => value.IsAutomaticProducer)
            .Select(value => Key(value.DefinitionId, value.WorkstationTag))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualKeys = orderedFacilities
            .Select(Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actualKeys.SequenceEqual(expectedKeys, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Authored throughput subjects do not exactly cover the live producer census.");
        }

        string[] publishedKeys = Coverage.CompleteEnvelopes
            .Select(value => Key(value.DefinitionId, value.WorkstationTag))
            .Concat(Coverage.Gaps.Select(value =>
                Key(value.DefinitionId, value.WorkstationTag)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!publishedKeys.SequenceEqual(actualKeys, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Authored throughput envelope publication does not account for every scoped facility.");
        }

        Facilities = Array.AsReadOnly(orderedFacilities);
        AutomaticProducerCount = orderedFacilities.Length;
        RecipeOnlyProducerCount = orderedFacilities.Count(value =>
            value.Recipes.Count > 0);
        SpecialProducerCount = orderedFacilities.Count(value =>
            value.Recipes.Count == 0);
        DistinctRecipeCount = orderedFacilities
            .SelectMany(value => value.Recipes)
            .Select(value => value.RecipeId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        SpecialCandidateCount = orderedFacilities.Sum(value =>
            value.SpecialCandidates.Count);
        SpecialGapCount = orderedFacilities.Sum(value =>
            value.SpecialGaps.Count);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(
            "production-authored-throughput-facility-scope-snapshot@1");
        digest.Append(Census.SourceDigest);
        digest.Append(Facilities.Count);
        foreach (ProductionAuthoredThroughputFacilitySubject facility in
                 Facilities)
        {
            digest.Append(facility.SourceDigest);
        }
        digest.Append(Coverage.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionFacilityOutputCensusSnapshot Census { get; }
    public IReadOnlyList<ProductionAuthoredThroughputFacilitySubject>
        Facilities { get; }
    public ProductionAuthoredThroughputCoverageSnapshot Coverage { get; }
    public int AutomaticProducerCount { get; }
    public int RecipeOnlyProducerCount { get; }
    public int SpecialProducerCount { get; }
    public int DistinctRecipeCount { get; }
    public int SpecialCandidateCount { get; }
    public int SpecialGapCount { get; }
    public string SourceDigest { get; }

    private static string Key(
        ProductionAuthoredThroughputFacilitySubject value) =>
        Key(value.DefinitionId, value.WorkstationTag);

    private static string Key(string definitionId, string workstationTag) =>
        definitionId + "\n" + workstationTag;
}

public interface IProductionAuthoredThroughputFacilityScopeQuery
{
    ProductionAuthoredThroughputFacilityScopeSnapshot Capture();
}

/// <summary>
/// Runtime-safe current-source authority for all automatic producer keys.
/// Recipe-only facilities are joined by exact recipe identity and semantic
/// digest. Capacity-contributor facilities deliberately receive no generic
/// tag-matched recipes: their polymorphic special candidates/gaps are the
/// execution authority even when an unrelated recipe shares the workstation
/// tag.
/// </summary>
public sealed class ProductionAuthoredThroughputFacilityScopeAuthority :
    IProductionAuthoredThroughputFacilityScopeQuery
{
    public const string Schema =
        "production-authored-throughput-facility-scope-authority@1";

    private readonly IGameContentDefinitionSource content;
    private readonly IProductionFacilityOutputCensusQuery censusQuery;
    private readonly ProductionFacilityDefinitionCatalog facilityDefinitions;
    private readonly IProductionAuthoredThroughputEnvelopeQuery envelopeQuery;

    public ProductionAuthoredThroughputFacilityScopeAuthority(
        IGameContentDefinitionSource content,
        IProductionFacilityOutputCensusQuery censusQuery,
        ProductionFacilityDefinitionCatalog facilityDefinitions,
        IProductionAuthoredThroughputEnvelopeQuery envelopeQuery)
    {
        this.content = content
            ?? throw new ArgumentNullException(nameof(content));
        this.censusQuery = censusQuery
            ?? throw new ArgumentNullException(nameof(censusQuery));
        this.facilityDefinitions = facilityDefinitions
            ?? throw new ArgumentNullException(nameof(facilityDefinitions));
        this.envelopeQuery = envelopeQuery
            ?? throw new ArgumentNullException(nameof(envelopeQuery));
    }

    public ProductionAuthoredThroughputFacilityScopeSnapshot Capture()
    {
        IReadOnlyList<BuildingSO> buildingDefinitions =
            content.GetAll<BuildingSO>()
            ?? throw new InvalidOperationException(
                "Current content source returned no building definition list.");
        ProductionFacilityOutputCensusSnapshot census =
            censusQuery.Capture(buildingDefinitions);
        ProductionFacilityOutputCensusRow[] producerRows = census.Rows
            .Where(value => value.IsAutomaticProducer)
            .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
            .ToArray();
        if (producerRows.Length == 0)
        {
            throw new InvalidOperationException(
                "Current production census contains no automatic producer.");
        }

        IReadOnlyList<ProductionRecipeSO> recipeDefinitions =
            content.GetAll<ProductionRecipeSO>()
            ?? throw new InvalidOperationException(
                "Current content source returned no recipe definition list.");
        if (recipeDefinitions.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "Current production recipe source contains a null definition.");
        }
        Dictionary<string, ProductionRecipeSO> recipesById =
            new(StringComparer.Ordinal);
        foreach (ProductionRecipeSO recipe in recipeDefinitions
                     .OrderBy(value => value.RecipeId, StringComparer.Ordinal))
        {
            if (!recipesById.TryAdd(recipe.RecipeId, recipe))
            {
                throw new InvalidOperationException(
                    "Current production recipe source contains duplicate ID: "
                    + recipe.RecipeId);
            }
        }

        List<ProductionAuthoredThroughputFacilitySubject> subjects = new(
            producerRows.Length);
        foreach (ProductionFacilityOutputCensusRow row in producerRows)
        {
            BuildingSO definition = facilityDefinitions.Require(
                row.DefinitionId);
            ValidateFacilityJoin(row, definition);

            ProductionRecipeSO[] joinedRecipes =
                row.CapacityContributorIds.Count == 0
                    ? JoinRecipeOnlyFacility(row, recipesById)
                    : JoinSpecialFacility(row);
            subjects.Add(new ProductionAuthoredThroughputFacilitySubject(
                row.DefinitionId,
                row.WorkstationTag,
                ProductionFacilityCapacitySubjectAdapter
                    .CaptureWorkstationLaneProfile(definition),
                ProductionFacilityCapacitySubjectAdapter
                    .CaptureProcessFluidProfile(definition),
                joinedRecipes,
                row.SpecialThroughputCandidates,
                row.SpecialThroughputGaps));
        }

        ProductionAuthoredThroughputCoverageSnapshot coverage =
            envelopeQuery.Capture(subjects);
        return new ProductionAuthoredThroughputFacilityScopeSnapshot(
            census,
            subjects,
            coverage);
    }

    private static ProductionRecipeSO[] JoinRecipeOnlyFacility(
        ProductionFacilityOutputCensusRow row,
        IReadOnlyDictionary<string, ProductionRecipeSO> recipesById)
    {
        if (row.RecipeIds.Count == 0
            || row.SpecialThroughputCandidates.Count != 0
            || row.SpecialThroughputGaps.Count != 0)
        {
            throw new InvalidOperationException(
                "Recipe-only producer classification drifted: "
                + row.DefinitionId);
        }

        ProductionRecipeSO[] joined = row.RecipeIds
            .Select(recipeId => recipesById.TryGetValue(
                    recipeId,
                    out ProductionRecipeSO recipe)
                ? recipe
                : throw new InvalidOperationException(
                    "Recipe-only census references an unknown recipe: "
                    + recipeId))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        string[] joinedIds = joined
            .Select(value => value.RecipeId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedIds = row.RecipeIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] joinedDigests = joined
            .Select(ProductionRecipeSemanticDigest.Capture)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expectedDigests = row.RecipeSourceDigests
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!joinedIds.SequenceEqual(expectedIds, StringComparer.Ordinal)
            || !joinedDigests.SequenceEqual(
                expectedDigests,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Recipe-only census semantic digest join drifted: "
                + row.DefinitionId);
        }
        return joined;
    }

    private static ProductionRecipeSO[] JoinSpecialFacility(
        ProductionFacilityOutputCensusRow row)
    {
        if (row.CapacityContributorIds.Count == 0
            || row.SpecialThroughputCandidates.Count == 0
                && row.SpecialThroughputGaps.Count == 0)
        {
            throw new InvalidOperationException(
                "Capacity-contributor producer has no special throughput authority: "
                + row.DefinitionId);
        }

        // The census may report recipes that overlap this workstation tag.
        // Those recipes belong to generic recipe executors on other facility
        // keys; a capacity contributor is not made recipe-capable by tag alone.
        return Array.Empty<ProductionRecipeSO>();
    }

    private static void ValidateFacilityJoin(
        ProductionFacilityOutputCensusRow row,
        BuildingSO definition)
    {
        string definitionId =
            ProductionFacilityDefinitionIdentity.Resolve(definition);
        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        if (!string.Equals(
                definitionId,
                row.DefinitionId,
                StringComparison.Ordinal)
            || workstation == null
            || buffer == null
            || !string.Equals(
                workstation.WorkstationTag,
                row.WorkstationTag,
                StringComparison.Ordinal)
            || buffer.physicalOutputBufferCycleCapacity
                != row.OutputBufferCycleCapacity)
        {
            throw new InvalidOperationException(
                "Production census facility definition join drifted: "
                + row.DefinitionId);
        }

        ProductionFacilityWorkstationLaneCapacityProfile laneProfile =
            ProductionFacilityCapacitySubjectAdapter
                .CaptureWorkstationLaneProfile(definition);
        ProductionFacilityProcessFluidCapacityProfile processFluidProfile =
            ProductionFacilityCapacitySubjectAdapter
                .CaptureProcessFluidProfile(definition);
        if (!string.Equals(
                laneProfile.SourceDigest,
                row.WorkstationLaneSourceDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                processFluidProfile.SourceDigest,
                row.ProcessFluidSourceDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production census facility capacity provenance drifted: "
                + row.DefinitionId);
        }
    }
}
