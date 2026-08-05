using System;
using System.Collections.Generic;
using System.Linq;

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

public sealed class MetaProgressionRuntimeReader : IMetaProgressionRuntimeReader
{
    private readonly MetaProgressionRuntime runtime;

    public MetaProgressionRuntimeReader(
        ProgressionSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .MetaProgression
            ?? throw new InvalidOperationException(
                $"{nameof(MetaProgressionRuntimeReader)} requires a loaded {nameof(MetaProgressionRuntime)}.");
    }

    public int GetStartingFacilityCandidateBonus()
    {
        return runtime.GetStartingFacilityCandidateBonus();
    }

    public int GetStartingOwnerTraitCandidateBonus()
    {
        return runtime.GetStartingOwnerTraitCandidateBonus();
    }

    public float GetOwnerMaxHealthMultiplier()
    {
        return runtime.GetOwnerMaxHealthMultiplier();
    }

    public float GetInvasionWarningThresholdMultiplier()
    {
        return runtime.GetInvasionWarningThresholdMultiplier();
    }

    public float GetCommerceStockCostMultiplier(StockCategory category)
    {
        return runtime.GetCommerceStockCostMultiplier(
            category == StockCategory.Food || category == StockCategory.General);
    }

    public float GetFortressFacilityCostMultiplier(BuildingSO building)
    {
        return runtime.GetFortressFacilityCostMultiplier(
            building?.Defense != null && building.Defense.IsDefenseFacility);
    }

    public float GetArcaneResearchWorkMultiplier()
    {
        return runtime.GetArcaneResearchWorkMultiplier();
    }

    public bool IsRecipePreserved(string recipeId)
    {
        return runtime.IsRecipePreserved(recipeId);
    }

    public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)
    {
        return runtime.GetExpandedBasicPurchaseBuildingIds((buildings ?? Array.Empty<BuildingSO>())
            .Where(building => building != null)
            .Select(building => new MetaFacilityCandidateSnapshot(
                building.id,
                !building.IsGridMovement
                && !building.IsWall
                && FacilityShopService.GetBuildingStar(building) <= 1)));
    }
}
