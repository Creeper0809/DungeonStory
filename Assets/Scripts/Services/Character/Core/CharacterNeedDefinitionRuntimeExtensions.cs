using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Default-assembly adapter for actor-specific need projections. Pure mood projection stays
/// in DungeonStory.CharacterNeeds and receives value dictionaries instead of CharacterActor.
/// </summary>
public static class CharacterNeedDefinitionRuntimeExtensions
{
    public static float GetUrgency(
        this CharacterNeedDefinition definition,
        CharacterActor actor)
    {
        CharacterStats stats = actor != null ? actor.Stats : null;
        if (stats == null
            || !stats.TryGetConditionValue(definition.Condition, out float value))
        {
            return 0.5f;
        }

        return Mathf.Clamp01(1f - value / 100f);
    }

    public static bool TryCreateMoodFactor(
        this CharacterNeedDefinition definition,
        IReadOnlyDictionary<CharacterCondition, float> stats,
        out CharacterMoodFactorSnapshot factor) =>
        CharacterMoodRules.TryCreateNeedMoodFactor(
            definition,
            stats,
            out factor);
}
