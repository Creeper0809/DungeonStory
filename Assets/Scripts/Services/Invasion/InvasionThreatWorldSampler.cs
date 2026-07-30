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

    public InvasionThreatWorldSampler(
        IBuildingWorldQuery buildingWorld,
        ICharacterWorldQuery characterWorld,
        IFacilityCrimeRiskEvaluator crimeRiskEvaluator)
    {
        this.buildingWorld = buildingWorld
            ?? throw new System.ArgumentNullException(nameof(buildingWorld));
        this.characterWorld = characterWorld
            ?? throw new System.ArgumentNullException(nameof(characterWorld));
        this.crimeRiskEvaluator = crimeRiskEvaluator
            ?? throw new System.ArgumentNullException(nameof(crimeRiskEvaluator));
    }

    public InvasionThreatFactors Sample(float secondsSinceLastInvasion)
    {
        IReadOnlyList<BuildableObject> buildings = buildingWorld.Buildings;
        IReadOnlyList<CharacterActor> characters = characterWorld.Characters;

        float dungeonValue = CalculateDungeonValue(buildings);
        float reputation = CalculateReputation(characters);
        float time = Mathf.Clamp(secondsSinceLastInvasion / 180f, 0f, 10f);
        float risk = CalculateRisk(buildings);
        return new InvasionThreatFactors(dungeonValue, reputation, time, risk);
    }

    private static float CalculateDungeonValue(IEnumerable<BuildableObject> buildings)
    {
        return InvasionThreatValueCalculator.CalculateDungeonValue(buildings);
    }

    private static float CalculateReputation(IEnumerable<CharacterActor> characters)
    {
        if (characters == null)
        {
            return 0f;
        }

        int customers = 0;
        float mood = 0f;
        foreach (CharacterActor character in characters)
        {
            CharacterIdentity identity = character != null ? character.Identity : null;
            if (identity == null || identity.CharacterType != CharacterType.Customer)
            {
                continue;
            }

            customers++;
            CharacterStats stats = character.Stats;
            if (stats != null && stats.Stats.TryGetValue(CharacterCondition.MOOD, out float sample))
            {
                mood += Mathf.Clamp(sample, 0f, 100f) / 100f;
            }
        }

        if (customers == 0)
        {
            return 0f;
        }

        return customers + (mood / Mathf.Max(1, customers));
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
