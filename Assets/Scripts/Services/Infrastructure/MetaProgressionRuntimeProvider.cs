using System;
using System.Collections.Generic;

public interface IMetaProgressionRuntimeProvider
{
    bool TryGetRuntime(out MetaProgressionRuntime runtime);
}

public interface IMetaProgressionRuntimeReader
{
    int GetStartingFacilityCandidateBonus();
    int GetStartingOwnerTraitCandidateBonus();
    float GetOwnerMaxHealthMultiplier();
    float GetInvasionWarningThresholdMultiplier();
    float GetCommerceStockCostMultiplier(StockCategory category);
    float GetFortressFacilityCostMultiplier(BuildingSO building);
    float GetArcaneResearchWorkMultiplier();
    bool IsRecipePreserved(string recipeId);
    IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings);
}

public sealed class MetaProgressionRuntimeProvider :
    IMetaProgressionRuntimeProvider
{
    private readonly ProgressionSceneRuntimeReferences runtimeReferences;

    public MetaProgressionRuntimeProvider(
        ProgressionSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out MetaProgressionRuntime runtime)
    {
        runtime = runtimeReferences.MetaProgression;
        return runtime != null;
    }
}

public sealed class MetaProgressionRuntimeReader : IMetaProgressionRuntimeReader
{
    private readonly IMetaProgressionRuntimeProvider provider;

    public MetaProgressionRuntimeReader(IMetaProgressionRuntimeProvider provider)
    {
        this.provider = provider
            ?? throw new ArgumentNullException(nameof(provider));
    }

    public int GetStartingFacilityCandidateBonus()
    {
        return provider.TryGetRuntime(out MetaProgressionRuntime runtime)
            ? runtime.GetStartingFacilityCandidateBonus()
            : 0;
    }

    public int GetStartingOwnerTraitCandidateBonus()
    {
        return provider.TryGetRuntime(out MetaProgressionRuntime runtime)
            ? runtime.GetStartingOwnerTraitCandidateBonus()
            : 0;
    }

    public float GetOwnerMaxHealthMultiplier()
    {
        return provider.TryGetRuntime(out MetaProgressionRuntime runtime)
            ? runtime.GetOwnerMaxHealthMultiplier()
            : 1f;
    }

    public float GetInvasionWarningThresholdMultiplier()
    {
        return provider.TryGetRuntime(out MetaProgressionRuntime runtime)
            ? runtime.GetInvasionWarningThresholdMultiplier()
            : 1f;
    }

    public float GetCommerceStockCostMultiplier(StockCategory category)
    {
        return provider.TryGetRuntime(out MetaProgressionRuntime runtime)
            ? runtime.GetCommerceStockCostMultiplier(category)
            : 1f;
    }

    public float GetFortressFacilityCostMultiplier(BuildingSO building)
    {
        return provider.TryGetRuntime(out MetaProgressionRuntime runtime)
            ? runtime.GetFortressFacilityCostMultiplier(building)
            : 1f;
    }

    public float GetArcaneResearchWorkMultiplier()
    {
        return provider.TryGetRuntime(out MetaProgressionRuntime runtime)
            ? runtime.GetArcaneResearchWorkMultiplier()
            : 1f;
    }

    public bool IsRecipePreserved(string recipeId)
    {
        return provider.TryGetRuntime(out MetaProgressionRuntime runtime)
            && runtime.IsRecipePreserved(recipeId);
    }

    public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)
    {
        return provider.TryGetRuntime(out MetaProgressionRuntime runtime)
            ? runtime.GetExpandedBasicPurchaseBuildingIds(buildings)
            : Array.Empty<int>();
    }
}
