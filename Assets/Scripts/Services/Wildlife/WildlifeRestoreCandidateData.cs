using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WildlifeRestoreCandidate :
    IDungeonDiscardableRestoreCandidate
{
    private WildlifeRestoreCandidate(
        WildlifePopulationState population,
        WildlifeEcosystemRestoreCandidate ecosystem,
        List<WildlifeCarcassFreshnessSaveData> carcasses)
    {
        Population = population ?? throw new ArgumentNullException(nameof(population));
        Ecosystem = ecosystem ?? throw new ArgumentNullException(nameof(ecosystem));
        Carcasses = carcasses ?? throw new ArgumentNullException(nameof(carcasses));
    }

    internal WildlifePopulationState Population { get; }
    internal WildlifeEcosystemRestoreCandidate Ecosystem { get; }
    internal List<WildlifeCarcassFreshnessSaveData> Carcasses { get; }
    internal Action<WildlifeRestoreCandidate> DiscardAction { get; set; }

    public void Discard()
    {
        Action<WildlifeRestoreCandidate> discard = DiscardAction;
        DiscardAction = null;
        discard?.Invoke(this);
    }

    public static WildlifeRestoreCandidate Create(
        DungeonWildlifeSaveData source,
        float nextCarcassTickAt,
        WildlifeEcosystemRestoreCandidate ecosystem)
    {
        WildlifePopulationState population = new WildlifePopulationState
        {
            NextSequence = source.nextSequence,
            InitialSpawnCompleted = true,
            NextCarcassTickAt = nextCarcassTickAt
        };
        foreach (WildlifeFoodRaidOrderSaveData order in
                 source.foodRaidOrders)
        {
            population.FoodRaidOrders.Add(
                WildlifeBehaviorRuntime.CloneFoodRaidOrder(order));
        }

        return new WildlifeRestoreCandidate(
            population,
            ecosystem,
            source.carcasses
                .Select(CloneCarcass)
                .ToList());
    }

    private static WildlifeCarcassFreshnessSaveData CloneCarcass(
        WildlifeCarcassFreshnessSaveData source)
    {
        return new WildlifeCarcassFreshnessSaveData
        {
            stackId = source.stackId,
            speciesId = source.speciesId,
            remainingFreshnessSeconds = source.remainingFreshnessSeconds
        };
    }

}
