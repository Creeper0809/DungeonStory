using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WildlifeRestoreCandidate :
    IDungeonDiscardableRestoreCandidate
{
    private WildlifeRestoreCandidate(
        WildlifePopulationState population,
        WildlifeEcosystemRestoreCandidate ecosystem,
        List<WildlifeCarcassFreshnessSaveData> carcasses,
        List<WildlifeCarcassProcessingSaveData> processing)
    {
        Population = population ?? throw new ArgumentNullException(nameof(population));
        Ecosystem = ecosystem ?? throw new ArgumentNullException(nameof(ecosystem));
        Carcasses = carcasses ?? throw new ArgumentNullException(nameof(carcasses));
        Processing = processing ?? throw new ArgumentNullException(nameof(processing));
    }

    internal WildlifePopulationState Population { get; }
    internal WildlifeEcosystemRestoreCandidate Ecosystem { get; }
    internal List<WildlifeCarcassFreshnessSaveData> Carcasses { get; }
    internal List<WildlifeCarcassProcessingSaveData> Processing { get; }
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
            NextGameplayOutcomeSequence = source.nextGameplayOutcomeSequence,
            LastDiseaseVectorAbsoluteDay = source.lastDiseaseVectorAbsoluteDay,
            InitialSpawnCompleted = true,
            NextCarcassTickAt = nextCarcassTickAt
        };
        foreach (WildlifeFoodRaidOrderSaveData order in
                 source.foodRaidOrders)
        {
            population.FoodRaidOrders.Add(
                WildlifeBehaviorRuntime.CloneFoodRaidOrder(order));
        }
        foreach (WildlifeGameplayOutcomeSaveData outcome in
                 source.gameplayOutcomes)
        {
            population.GameplayOutcomes.Add(
                WildlifeGameplayOutcomeRuntime.Clone(outcome));
        }

        return new WildlifeRestoreCandidate(
            population,
            ecosystem,
            source.carcasses
                .Select(CloneCarcass)
                .ToList(),
            source.carcassProcessing.Select(row => row.Clone()).ToList());
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
