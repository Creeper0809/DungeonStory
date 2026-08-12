using System;
using System.Linq;

public static class CharacterSpeciesWorkAptitudeRules
{
    public const float StrongLearningMultiplier = 1.10f;
    public const float WeakLearningMultiplier = 0.90f;
    public const float StrongAutonomousUtilityAdjustment = 10f;
    public const float WeakAutonomousUtilityAdjustment = -10f;

    public static float GetLearningMultiplier(
        CharacterActor actor,
        WorkTypeId workTypeId)
    {
        int disposition = GetDisposition(actor, workTypeId);
        return disposition > 0
            ? StrongLearningMultiplier
            : disposition < 0
                ? WeakLearningMultiplier
                : 1f;
    }

    public static float GetAutonomousUtilityAdjustment(
        CharacterActor actor,
        WorkTypeId workTypeId)
    {
        int disposition = GetDisposition(actor, workTypeId);
        return disposition > 0
            ? StrongAutonomousUtilityAdjustment
            : disposition < 0
                ? WeakAutonomousUtilityAdjustment
                : 0f;
    }

    private static int GetDisposition(
        CharacterActor actor,
        WorkTypeId workTypeId)
    {
        if (actor == null || !workTypeId.IsValid)
            return 0;
        CharacterSpeciesSO species = actor.Identity?.Data?.species;
        if (species == null)
            return 0;
        bool strong = Contains(species.strongWorkTypeIds, workTypeId.Value);
        bool weak = Contains(species.weakWorkTypeIds, workTypeId.Value);
        if (strong && weak)
            throw new InvalidOperationException(
                $"Species '{species.speciesTag}' lists work '{workTypeId.Value}' "
                + "as both strong and weak.");
        return strong ? 1 : weak ? -1 : 0;
    }

    private static bool Contains(string[] values, string id) =>
        (values ?? Array.Empty<string>()).Any(value =>
            string.Equals(value, id, StringComparison.Ordinal));
}
