using System;
using System.Collections.Generic;

internal sealed class WildlifePopulationState : IWildlifeGameplayOutcomeOwner
{
    public List<WildlifeActor> Actors { get; } =
        new List<WildlifeActor>();

    public Dictionary<string, float> NextBehaviorTickByWildlifeId { get; } =
        new Dictionary<string, float>(StringComparer.Ordinal);

    public List<WildlifeFoodRaidOrderSaveData> FoodRaidOrders { get; } =
        new List<WildlifeFoodRaidOrderSaveData>();

    public List<WildlifeGameplayOutcomeSaveData> GameplayOutcomes { get; } =
        new List<WildlifeGameplayOutcomeSaveData>();

    public int NextSequence { get; set; } = 1;
    public int NextGameplayOutcomeSequence { get; set; }
    public int LastDiseaseVectorAbsoluteDay { get; set; }
    public bool InitialSpawnCompleted { get; set; }
    public float NextCarcassTickAt { get; set; }
}
