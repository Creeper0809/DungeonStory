public static class SurvivalFacilityUtility
{
    internal static FacilityWorkType AddFallbackWorkTypes(BuildableObject building, FacilityWorkType supportedTypes)
    {
        return AddFallbackWorkTypes(building?.BuildingData, supportedTypes);
    }

    internal static FacilityWorkType AddFallbackWorkTypes(BuildingSO building, FacilityWorkType supportedTypes)
    {
        if (building == null)
        {
            return supportedTypes;
        }

        if (building.GetAbility<BuildingWaterSourceAbility>() != null)
        {
            supportedTypes |= FacilityWorkType.DrawWater;
        }

        if (building.GetAbility<BuildingCookingAbility>() != null)
        {
            supportedTypes |= FacilityWorkType.Cook;
        }

        if (building.GetAbility<BuildingMedicalAbility>() != null)
        {
            supportedTypes |= FacilityWorkType.Treat;
        }

        if (building.GetAbility<BuildingFuelConsumerAbility>() != null)
        {
            supportedTypes |= FacilityWorkType.Refuel;
        }

        return supportedTypes;
    }

    internal static bool IsSurvivalWork(FacilityWorkType workType)
    {
        return workType == FacilityWorkType.DrawWater
            || workType == FacilityWorkType.Cook
            || workType == FacilityWorkType.Treat
            || workType == FacilityWorkType.Refuel;
    }
}
