using System;
using System.Collections.Generic;
using UnityEngine;

public static class CharacterProficiencyLearningRules
{
    private const int NeutralAptitude = 50;

    public static float Resolve(
        CharacterActor actor,
        ProficiencyWorkProfile profile,
        WorkTypeId workTypeId = default)
    {
        CharacterRuntimeProfile runtime = actor?.profile;
        if (runtime == null || !profile.IsValid) return 1f;
        float sharedMultiplier = actor.ProjectDetailedStat(
            GameplayEffectTargetIds.EarnedWorkExperience,
            1f).Value;
        return Resolve(runtime, profile, sharedMultiplier)
            * CharacterSpeciesWorkAptitudeRules.GetLearningMultiplier(
                actor,
                workTypeId);
    }

    private static float Resolve(
        CharacterRuntimeProfile runtime,
        ProficiencyWorkProfile profile,
        float sharedMultiplier)
    {
        if (runtime == null || !profile.IsValid) return 1f;

        float aptitude = ResolveAptitude(runtime, profile.Primary);
        if (profile.Secondary.IsValid)
        {
            aptitude = aptitude * profile.PrimaryWeight
                + ResolveAptitude(runtime, profile.Secondary)
                    * profile.SecondaryWeight;
        }

        float aptitudeMultiplier = 0.70f + aptitude * 0.006f;
        return Mathf.Clamp(
            aptitudeMultiplier
                * runtime.EarnedWorkExperienceMultiplier
                * Mathf.Max(.1f, sharedMultiplier),
            0.70f,
            1.75f);
    }

    private static int ResolveAptitude(
        CharacterRuntimeProfile runtime,
        CharacterProficiencyId proficiencyId)
    {
        IReadOnlyDictionary<string, int> aptitudes = runtime.InnateAptitudes;
        if (aptitudes != null
            && aptitudes.TryGetValue(proficiencyId.Value, out int value))
        {
            return Math.Clamp(value, 0, 100);
        }
        return NeutralAptitude;
    }
}
