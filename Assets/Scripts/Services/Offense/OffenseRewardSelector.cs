using System;
using System.Collections.Generic;
using System.Linq;

public sealed class OffenseRewardSelector : IOffenseRewardSelector
{
    private readonly IOffenseRewardCatalog catalog;

    public OffenseRewardSelector(IOffenseRewardCatalog catalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public BuildingSO SelectRareFacility(
        OffenseRewardContext context,
        IReadOnlyCollection<int> additionallyExcludedBuildingIds)
    {
        HashSet<int> alreadyGranted = context.rewardState != null
            ? context.rewardState.RareFacilityBuildingIds.ToHashSet()
            : new HashSet<int>();
        if (additionallyExcludedBuildingIds != null)
        {
            foreach (int buildingId in additionallyExcludedBuildingIds)
            {
                alreadyGranted.Add(buildingId);
            }
        }

        return catalog.Buildings
            .Where((building) => building != null
                && !building.IsGridMovement
                && !building.IsWall
                && FacilityShopService.GetBuildingStar(building) >= 2
                && !alreadyGranted.Contains(building.id))
            .OrderBy((building) => FacilityShopService.GetBuildingStar(building))
            .ThenBy((building) => building.id)
            .FirstOrDefault();
    }

    public FacilityBlueprintSO SelectBlueprint(
        OffenseBlueprintRewardSpec rewardSpec,
        OffenseRewardContext context)
    {
        IEnumerable<FacilityBlueprintSO> blueprints = catalog.Blueprints
            .Where((blueprint) => blueprint != null
                && rewardSpec != null
                && rewardSpec.IsEligible(blueprint, catalog.Buildings));

        HashSet<int> acquired = context.rewardState != null
            ? context.rewardState.AcquiredBlueprintIds.ToHashSet()
            : new HashSet<int>();

        return blueprints
            .Where((blueprint) => !acquired.Contains(blueprint.id))
            .Where((blueprint) => context.shopUnlockState == null || !context.shopUnlockState.IsBlueprintAcquired(blueprint))
            .Where((blueprint) => context.researchState == null || !context.researchState.IsCompleted(blueprint))
            .OrderByDescending((blueprint) => blueprint.rarity)
            .ThenBy((blueprint) => blueprint.id)
            .FirstOrDefault();
    }
}
