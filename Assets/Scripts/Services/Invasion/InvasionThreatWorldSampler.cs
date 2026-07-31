using System.Collections.Generic;
using UnityEngine;

public interface IInvasionThreatWorldSampler
{
    InvasionThreatFactors Sample(float secondsSinceLastInvasion);
}

public sealed class InvasionThreatWorldSampler : IInvasionThreatWorldSampler
{
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IFacilityCrimeRiskEvaluator crimeRiskEvaluator;
    private readonly IExternalInfluenceRuntime externalInfluence;

    public InvasionThreatWorldSampler(
        IBuildingWorldQuery buildingWorld,
        ICharacterWorldQuery characterWorld,
        IFacilityCrimeRiskEvaluator crimeRiskEvaluator,
        IExternalInfluenceRuntime externalInfluence)
    {
        this.buildingWorld = buildingWorld
            ?? throw new System.ArgumentNullException(nameof(buildingWorld));
        this.characterWorld = characterWorld
            ?? throw new System.ArgumentNullException(nameof(characterWorld));
        this.crimeRiskEvaluator = crimeRiskEvaluator
            ?? throw new System.ArgumentNullException(nameof(crimeRiskEvaluator));
        this.externalInfluence = externalInfluence
            ?? throw new System.ArgumentNullException(nameof(externalInfluence));
    }

    public InvasionThreatFactors Sample(float secondsSinceLastInvasion)
    {
        IReadOnlyList<BuildableObject> buildings = buildingWorld.Buildings;
        IReadOnlyList<CharacterActor> characters = characterWorld.Characters;

        float dungeonValue = CalculateDungeonValue(buildings);
        float reputation = externalInfluence.HostileRumor / 10f;
        float time = Mathf.Clamp(secondsSinceLastInvasion / 180f, 0f, 10f);
        float risk = CalculateRisk(buildings);
        return new InvasionThreatFactors(dungeonValue, reputation, time, risk);
    }

    private static float CalculateDungeonValue(IEnumerable<BuildableObject> buildings)
    {
        return InvasionThreatValueCalculator.CalculateDungeonValue(buildings);
    }

    private float CalculateRisk(IEnumerable<BuildableObject> buildings)
    {
        if (buildings == null)
        {
            return 0f;
        }

        float risk = 0f;
        foreach (BuildableObject building in buildings)
        {
            if (building == null || building.isDestroy)
            {
                continue;
            }

            if (building.IsDamaged)
            {
                risk += 1.5f;
            }

            if (building is IRetailFacility retail && building.Facility != null)
            {
                risk += crimeRiskEvaluator.CalculateOperationalRisk(new FacilityCrimeRiskContext(
                    building,
                    actor: null,
                    retail.HasServingWorker,
                    retail.HasWaitingCheckout,
                    building.CurrentUserCount,
                    cartItemCount: 1,
                    cartValue: 0,
                    retail.CurrentStock,
                    building.IsDamaged));

                if (retail.CurrentStock <= building.GetRestockRequestThreshold())
                {
                    risk += 0.5f;
                }
            }
        }

        return risk;
    }
}

public static class InvasionThreatValueCalculator
{
    public static float CalculateDungeonValue(IEnumerable<BuildableObject> buildings)
    {
        if (buildings == null)
        {
            return 0f;
        }

        float value = 0f;
        foreach (BuildableObject building in buildings)
        {
            if (building == null || building.isDestroy)
            {
                continue;
            }

            value += CalculateBuildingValue(building.BuildingData);
        }

        return Mathf.Max(0f, value);
    }

    public static float CalculateBuildingValue(BuildingSO building)
    {
        if (building == null
            || building.IsWall
            || building.IsDoor
            || building.IsGridMovement)
        {
            return 0f;
        }

        float constructionValue = building.GetConstructionValue() / 100f;
        float maintenanceValue = building.GetMaintenanceCost() / 100f;
        float operationalValue = 0f;

        FacilityData facility = building.Facility;
        if (facility != null && facility.roles != FacilityRole.None)
        {
            operationalValue += 0.5f;
        }

        if (building.Defense != null && building.Defense.IsDefenseFacility)
        {
            operationalValue += 0.5f;
        }

        int stockCapacity = building.GetInternalStockCapacity();
        if (stockCapacity > 0)
        {
            operationalValue += Mathf.Min(0.5f, stockCapacity / 100f);
        }

        return Mathf.Max(0.1f, constructionValue + maintenanceValue + operationalValue);
    }
}
