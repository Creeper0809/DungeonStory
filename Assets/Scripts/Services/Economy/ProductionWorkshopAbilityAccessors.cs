using System;

public static class ProductionWorkshopAbilityAccessors
{
    public static BuildingProductionWorkstationAbility
        GetProductionWorkstationAbility(this BuildingSO building)
    {
        BuildingProductionWorkstationAbility ability =
            building?.GetAbility<BuildingProductionWorkstationAbility>();
        return ability != null && ability.IsValid ? ability : null;
    }

    public static BuildingProductionSupportAbility
        GetProductionSupportAbility(this BuildingSO building)
    {
        BuildingProductionSupportAbility ability =
            building?.GetAbility<BuildingProductionSupportAbility>();
        return ability != null && ability.IsValid ? ability : null;
    }

    public static BuildingProductionBufferAbility
        GetProductionBufferAbility(this BuildingSO building) =>
        building?.GetAbility<BuildingProductionBufferAbility>();

    public static BuildingProductionOutputDispositionAbility
        GetProductionOutputDispositionAbility(this BuildingSO building)
    {
        BuildingProductionOutputDispositionAbility ability =
            building?.GetAbility<BuildingProductionOutputDispositionAbility>();
        return ability != null && ability.IsValid ? ability : null;
    }

    public static BuildingFacilitySupplyAbility
        GetFacilitySupplyAbility(this BuildingSO building) =>
        building?.GetAbility<BuildingFacilitySupplyAbility>();

    public static string GetProductionWorkstationTag(this BuildableObject building)
    {
        return building?.BuildingData
            .GetProductionWorkstationAbility()?.WorkstationTag
            ?? string.Empty;
    }

    public static bool MatchesProductionWorkstation(
        this BuildableObject building,
        ProductionRecipeSO recipe)
    {
        if (building == null || recipe == null || building.BuildingData == null)
        {
            return false;
        }

        BuildingProductionWorkstationAbility workstation =
            building.BuildingData.GetProductionWorkstationAbility();
        return workstation != null
            && string.Equals(
                workstation.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal);
    }
}
