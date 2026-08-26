using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ProductionFacilityCapacitySubjectAdapter
{
    public static ProductionFacilityCapacitySubject FromSave(
        ModularFacilityBuildingSaveData source,
        IBuildingDefinitionLookup definitions)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions));
        string instanceId = source.persistentInstanceId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(instanceId)
            || !string.Equals(instanceId, instanceId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Saved production facility ID is not canonical.");
        }

        BuildingSO definition = definitions.GetBuilding(source.buildingId)
            ?? throw new InvalidOperationException(
                "Saved production facility definition is missing.");
        if (definition.id != source.buildingId)
            throw new InvalidOperationException(
                "Saved production facility definition lookup returned the wrong ID.");
        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        if (workstation == null || buffer == null)
        {
            throw new InvalidOperationException(
                "Saved building is not a production output-capacity facility.");
        }

        return new ProductionFacilityCapacitySubject(
            (BuildingInstanceId)instanceId,
            new Vector2Int(source.centerX, source.centerY),
            ProductionFacilityDefinitionIdentity.Resolve(definition),
            workstation.WorkstationTag,
            buffer.physicalOutputBufferCycleCapacity);
    }
}

public readonly struct ProductionOutputBufferCapacitySourceSnapshot
{
    public ProductionOutputBufferCapacitySourceSnapshot(
        int cycleCapacity,
        long maximumBatchMassGrams,
        long projectedPortfolioCapacityGrams,
        long batchMinimumCapacityGrams,
        long requiredMinimumCapacityGrams,
        string sourceDigest)
    {
        if (cycleCapacity is < 2 or > 4
            || maximumBatchMassGrams < 0L
            || projectedPortfolioCapacityGrams < 0L
            || batchMinimumCapacityGrams < 0L
            || requiredMinimumCapacityGrams
                != Math.Max(
                    projectedPortfolioCapacityGrams,
                    batchMinimumCapacityGrams))
        {
            throw new ArgumentException(
                "Capacity source snapshot has invalid exact quantities.");
        }
        if (string.IsNullOrEmpty(sourceDigest) || sourceDigest.Length != 64)
            throw new ArgumentException(
                "Capacity source digest must be SHA-256.",
                nameof(sourceDigest));
        CycleCapacity = cycleCapacity;
        MaximumBatchMassGrams = maximumBatchMassGrams;
        ProjectedPortfolioCapacityGrams = projectedPortfolioCapacityGrams;
        BatchMinimumCapacityGrams = batchMinimumCapacityGrams;
        RequiredMinimumCapacityGrams = requiredMinimumCapacityGrams;
        SourceDigest = sourceDigest;
    }

    public int CycleCapacity { get; }
    public long MaximumBatchMassGrams { get; }
    public long ProjectedPortfolioCapacityGrams { get; }
    public long BatchMinimumCapacityGrams { get; }
    public long RequiredMinimumCapacityGrams { get; }
    public string SourceDigest { get; }
}

/// <summary>
/// Pure source-revision gate shared by runtime resume/restore and focused
/// verification. It never rewrites saved output or accepts a recomputed
/// fallback when any exact capacity input has drifted.
/// </summary>
public static class ProductionOutputBufferCapacitySourceGuard
{
    public const string StaleFailureToken =
        "prepared-output-capacity-source-stale";

    public static ProductionOutputBufferCapacitySourceSnapshot ValidateSaved(
        ProductionPreparedOutputBatchSaveData batch,
        ProductionOutputBufferCapacitySourceSnapshot current,
        string context)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));
        if (!string.Equals(
                batch.capacitySourceDigest,
                current.SourceDigest,
                StringComparison.Ordinal)
            || batch.outputBufferCycleCapacity != current.CycleCapacity
            || batch.projectedPortfolioCapacityGrams
                != current.ProjectedPortfolioCapacityGrams
            || batch.requiredMinimumCapacityGrams
                != current.RequiredMinimumCapacityGrams)
        {
            throw new InvalidOperationException(
                (context ?? string.Empty) + ":" + StaleFailureToken);
        }
        return current;
    }
}

/// <summary>
/// Projects one facility's physical output-buffer capacity from the heaviest
/// reachable prepared-output branch. Legacy count capacity is deliberately not
/// an input to this authority.
/// </summary>
public sealed class ProductionOutputBufferCapacityProjector
{
    public const string SourceDigestSchemaToken =
        "production-output-buffer-capacity-source@1";
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionMaximumOutputFactorCatalog maximumFactors;
    private readonly IProductionPreparedOutputComponentCodec componentCodec;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly Func<ProductionFacilityHandle, int> resolveCycleCapacity;
    private readonly Func<ProductionFacilityHandle, ProductionRecipeSO, bool>
        matchesWorkstation;

    public ProductionOutputBufferCapacityProjector(
        IResourceEconomyContentCatalog catalog,
        IProductionAssemblyBridge bridge,
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IProductionPreparedOutputComponentCodec componentCodec,
        IPhysicalItemMassQuery massQuery)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        IProductionAssemblyBridge requiredBridge = bridge
            ?? throw new ArgumentNullException(nameof(bridge));
        this.maximumFactors = maximumFactors
            ?? throw new ArgumentNullException(nameof(maximumFactors));
        this.componentCodec = componentCodec
            ?? throw new ArgumentNullException(nameof(componentCodec));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        resolveCycleCapacity = requiredBridge.ResolveOutputBufferCycleCapacity;
        matchesWorkstation = requiredBridge.MatchesWorkstation;
    }

    /// <summary>
    /// Deterministic audit constructor. Runtime composition uses the assembly
    /// bridge overload; Editor audits inject only the two pure facility reads
    /// consumed by this projector.
    /// </summary>
    public ProductionOutputBufferCapacityProjector(
        IResourceEconomyContentCatalog catalog,
        IProductionMaximumOutputFactorCatalog maximumFactors,
        IProductionPreparedOutputComponentCodec componentCodec,
        IPhysicalItemMassQuery massQuery,
        Func<ProductionFacilityHandle, int> resolveCycleCapacity,
        Func<ProductionFacilityHandle, ProductionRecipeSO, bool>
            matchesWorkstation)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.maximumFactors = maximumFactors
            ?? throw new ArgumentNullException(nameof(maximumFactors));
        this.componentCodec = componentCodec
            ?? throw new ArgumentNullException(nameof(componentCodec));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.resolveCycleCapacity = resolveCycleCapacity
            ?? throw new ArgumentNullException(nameof(resolveCycleCapacity));
        this.matchesWorkstation = matchesWorkstation
            ?? throw new ArgumentNullException(nameof(matchesWorkstation));
    }

    /// <summary>
    /// Returns zero when the facility cannot execute a migrated prepared-output
    /// recipe. Any malformed reachable recipe fails loudly instead of silently
    /// publishing a smaller capacity.
    /// </summary>
    public long ResolveRequiredCapacityGrams(ProductionFacilityHandle facility)
        => CaptureSource(facility, 0L).ProjectedPortfolioCapacityGrams;

    public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
        ProductionFacilityHandle facility,
        long exactBatchMassGrams)
    {
        if (facility == null || facility.IsDestroyed || !facility.InstanceId.IsValid)
            throw new ArgumentException("A live production facility is required.", nameof(facility));
        ProductionFacilityCapacitySubject subject =
            ProductionFacilityCapacitySubject.FromLive(facility);
        int resolvedCycle = resolveCycleCapacity(facility);
        if (resolvedCycle != subject.OutputBufferCycleCapacity)
        {
            throw new InvalidOperationException(
                "Live production output cycle authority drifted from its immutable subject.");
        }
        foreach (ProductionRecipeSO recipe in (catalog.Recipes
                     ?? Array.Empty<ProductionRecipeSO>())
                 .Where(value => value != null
                     && ProductionPreparedOutputMigrationScope.Contains(value.RecipeId)))
        {
            bool pure = string.Equals(
                subject.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal);
            if (matchesWorkstation(facility, recipe) != pure)
            {
                throw new InvalidOperationException(
                    "Live workstation matching drifted from the immutable capacity subject: "
                    + recipe.RecipeId);
            }
        }
        return CaptureSource(subject, exactBatchMassGrams);
    }

    public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
        ProductionFacilityCapacitySubject subject,
        long exactBatchMassGrams)
    {
        if (!subject.FacilityId.IsValid
            || string.IsNullOrEmpty(subject.DefinitionId)
            || string.IsNullOrEmpty(subject.WorkstationTag)
            || subject.OutputBufferCycleCapacity is < 2 or > 4
            || exactBatchMassGrams < 0L)
        {
            throw new InvalidOperationException(
                "Production facility capacity semantic identity is incomplete.");
        }

        int cycleCapacity = subject.OutputBufferCycleCapacity;

        ProductionRecipeSO[] matchingRecipes = (catalog.Recipes
                ?? Array.Empty<ProductionRecipeSO>())
            .Where(value => value != null
                && ProductionPreparedOutputMigrationScope.Contains(value.RecipeId)
                && string.Equals(
                    subject.WorkstationTag,
                    value.WorkstationTag,
                    StringComparison.Ordinal))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();

        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(SourceDigestSchemaToken);
        canonical.Append(ProductionOutputDestinationAuthorityRuntime
            .CapacitySchemaRevision);
        canonical.Append(massQuery.AuthorityRevision);
        canonical.Append(subject.DefinitionId);
        canonical.Append(subject.FacilityId.Value);
        canonical.Append(subject.Position.x);
        canonical.Append(subject.Position.y);
        canonical.Append(subject.WorkstationTag);
        canonical.Append(ProductionBillRuntime.OutputDestinationPrefix
            + subject.FacilityId.Value);
        canonical.Append(ProductionOutputDestinationAuthorityRuntime.OwnerDomain);
        canonical.Append(ProductionBillRuntime.OutputDestinationPrefix
            + subject.FacilityId.Value);
        canonical.Append(subject.FacilityId.Value);
        canonical.Append(cycleCapacity);
        canonical.Append(matchingRecipes.Length);

        long maximumBatchMassGrams = 0L;
        foreach (ProductionRecipeSO recipe in matchingRecipes)
        {
            long branchMassGrams = ResolveMaximumBranchMassGrams(
                recipe,
                canonical);
            maximumBatchMassGrams = Math.Max(
                maximumBatchMassGrams,
                branchMassGrams);
        }

        long projectedPortfolioCapacity = maximumBatchMassGrams == 0L
            ? 0L
            : checked(maximumBatchMassGrams * cycleCapacity);
        long batchMinimumCapacity = exactBatchMassGrams == 0L
            ? 0L
            : checked(exactBatchMassGrams * cycleCapacity);
        long requiredMinimumCapacity = Math.Max(
            projectedPortfolioCapacity,
            batchMinimumCapacity);
        canonical.Append(maximumBatchMassGrams);
        canonical.Append(projectedPortfolioCapacity);
        canonical.Append(exactBatchMassGrams);
        canonical.Append(batchMinimumCapacity);
        canonical.Append(requiredMinimumCapacity);
        return new ProductionOutputBufferCapacitySourceSnapshot(
            cycleCapacity,
            maximumBatchMassGrams,
            projectedPortfolioCapacity,
            batchMinimumCapacity,
            requiredMinimumCapacity,
            canonical.ComputeSha256());
    }

    private long ResolveMaximumBranchMassGrams(
        ProductionRecipeSO recipe,
        CanonicalSemanticDigestBuilder canonical)
    {
        ProductionOutputFactor multiplier = maximumFactors.ResolveMaximum(recipe);

        ProductionPreparedOutputMigrationScope.ValidateExactProfileOrThrow(recipe);
        canonical.Append(recipe.RecipeId);
        canonical.Append(ProductionRecipeSemanticDigest.Capture(recipe));
        canonical.Append(ProductionPreparedOutputMigrationScope
            .CaptureProfileDigest(recipe.RecipeId));
        canonical.Append(maximumFactors.CaptureRecipeSourceDigest(recipe));
        canonical.Append(multiplier.Numerator);
        canonical.Append(multiplier.Denominator);

        long normalBranchMassGrams = 0L;
        ProductionOutputDefinition[] physicalOutputs = recipe
            .CaptureCanonicalOutputs()
            .Where(value => value.Probability > 0f
                && value.Role != ProductionOutputRole.DeclaredLoss)
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        canonical.Append(physicalOutputs.Length);
        foreach (ProductionOutputDefinition output in physicalOutputs)
        {
            int maximumQuantity = multiplier.CeilQuantity(output.Amount);
            long outputMass = ResolvePhysicalMassGrams(
                output.ItemId,
                maximumQuantity,
                out string componentProfileDigest);
            canonical.Append(output.OutputLineId);
            canonical.Append(output.ItemId);
            canonical.Append(maximumQuantity);
            canonical.Append(componentProfileDigest);
            canonical.Append(outputMass);
            normalBranchMassGrams = checked(
                normalBranchMassGrams + outputMass);
        }

        long ruinedBranchMassGrams = 0L;
        string ruinedProfileDigest = string.Empty;
        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && !string.IsNullOrWhiteSpace(recipe.SpoilageItemId))
        {
            ruinedBranchMassGrams = ResolvePhysicalMassGrams(
                recipe.SpoilageItemId,
                1,
                out ruinedProfileDigest);
        }
        canonical.Append(normalBranchMassGrams);
        canonical.Append(recipe.SpoilageItemId);
        canonical.Append(ruinedProfileDigest);
        canonical.Append(ruinedBranchMassGrams);

        long result = Math.Max(normalBranchMassGrams, ruinedBranchMassGrams);
        if (result <= 0L)
        {
            throw new InvalidOperationException(
                $"Reachable recipe '{recipe.RecipeId}' has no physical output mass.");
        }
        canonical.Append(result);
        return result;
    }

    private long ResolvePhysicalMassGrams(
        string itemId,
        int quantity,
        out string componentProfileDigest)
    {
        if (quantity <= 0
            || !catalog.TryGetItem(itemId, out ResourceItemDefinitionSO definition))
        {
            throw new InvalidOperationException(
                $"Physical output item '{itemId}' is missing or has invalid quantity '{quantity}'.");
        }

        ProductionPreparedOutputComponentProjection projection =
            componentCodec.Create(definition);
        componentProfileDigest = projection.Fingerprint;
        return massQuery.GetQuantityMass(
            (ItemDefinitionId)itemId,
            projection.MassSubject,
            quantity).Value;
    }
}
