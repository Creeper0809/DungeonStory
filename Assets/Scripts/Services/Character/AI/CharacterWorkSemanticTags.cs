using System;
using System.Collections.Generic;

/// <summary>
/// Produces exact semantic tags from the real work type and current character
/// state. Trait utility rules match only these tags; broad prefix matching is
/// intentionally forbidden because it made unrelated preferences cancel or
/// reinforce every work/self-care action.
/// </summary>
public static class CharacterWorkSemanticTags
{
    public static IReadOnlyCollection<string> Resolve(
        CharacterActor actor,
        WorkTypeId workTypeId)
    {
        HashSet<string> tags = new(StringComparer.Ordinal)
        {
            CharacterAiActionTags.Work
        };
        if (workTypeId.IsValid)
            tags.Add(workTypeId.Value);

        bool dangerous = workTypeId == BuiltInWorkTypeIds.Guard
            || workTypeId == BuiltInWorkTypeIds.Hunt
            || workTypeId == BuiltInWorkTypeIds.Rescue
            || workTypeId == BuiltInWorkTypeIds.ThreatMitigation;
        if (dangerous) tags.Add("work:dangerous");
        if (workTypeId == BuiltInWorkTypeIds.Rescue)
        {
            tags.Add("work:crisis-rescue");
            tags.Add("work:immediate");
        }
        if (workTypeId == BuiltInWorkTypeIds.ThreatMitigation
            || workTypeId == BuiltInWorkTypeIds.Warden)
            tags.Add("work:subdue");
        if (workTypeId == BuiltInWorkTypeIds.Haul
            || workTypeId == BuiltInWorkTypeIds.Restock)
            tags.Add("work:heavy-haul");
        if (workTypeId == BuiltInWorkTypeIds.Craft)
            tags.Add("work:precision");
        if (workTypeId == BuiltInWorkTypeIds.Treat
            || workTypeId == BuiltInWorkTypeIds.Surgery)
            tags.Add("work:precision");
        if (workTypeId == BuiltInWorkTypeIds.Dismantle)
            tags.Add("work:salvage");
        if (workTypeId == BuiltInWorkTypeIds.Clean)
            tags.Add("work:clean");
        if (actor?.InjurySeverity > 0.001f)
            tags.Add("work:while-in-pain");
        if (actor != null && actor.TryGetAbility(out AbilityWork work))
        {
            IReadOnlyList<string> activeConditions =
                work.GetActiveGameplayEffectConditionIds();
            for (int index = 0; index < activeConditions.Count; index++)
            {
                string condition = activeConditions[index];
                if (!string.IsNullOrWhiteSpace(condition))
                    tags.Add(condition);
            }
        }

        float daySeconds = GameCalendarRules.SecondsPerDay;
        float elapsed = actor?.GameClock?.Time ?? 0f;
        float hour = daySeconds <= 0f
            ? 12f
            : elapsed % daySeconds / daySeconds * 24f;
        tags.Add(hour >= 18f || hour < 6f ? "shift:night" : "shift:day");
        return tags;
    }
}
