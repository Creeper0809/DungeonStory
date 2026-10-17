using System;
using System.Collections.Generic;
using System.Linq;

public static class SeasonalWildlifeActorRestoreJoin
{

    public static void ValidateRestoreJoin(
        SeasonalEventWorldSaveData payload,
        IReadOnlyList<WildlifeActor> candidateWildlife)
    {
        if (payload?.activeEvents == null || candidateWildlife == null)
            throw new InvalidOperationException(
                "Seasonal wildlife restore requires seasonal and wildlife candidates.");

        Dictionary<string, string> speciesByWildlifeId = candidateWildlife
            .Where(value => value != null && value.IsAlive)
            .ToDictionary(
                value => value.WildlifeId,
                value => value.SpeciesId,
                StringComparer.Ordinal);
        SeasonalWildlifeArrivalRules.ValidateRestoreJoin(payload, speciesByWildlifeId);
    }
}
