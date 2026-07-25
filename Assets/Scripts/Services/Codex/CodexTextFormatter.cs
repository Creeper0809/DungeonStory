using System;
using System.Collections.Generic;
using System.Linq;

public static class CodexTextFormatter
{
    public static string FormatEvolutionMutationTags(IReadOnlyList<string> mutationTags)
    {
        if (mutationTags == null || mutationTags.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", mutationTags
            .Where((tag) => !string.IsNullOrWhiteSpace(tag))
            .Distinct());
    }

    public static string FormatFacilityRoles(FacilityRole roles)
    {
        return string.Join(", ", FacilityRoleCatalog
            .Enumerate(roles)
            .Select((definition) => definition.RoomLabel));
    }

    public static string FormatWorkTypes(IEnumerable<WorkTypeId> workTypeIds)
    {
        if (workTypeIds == null)
        {
            return string.Empty;
        }

        return string.Join(", ", workTypeIds
            .Where((workTypeId) => workTypeId.IsValid)
            .Select((workTypeId) => WorkTypeCatalog.TryGet(
                    workTypeId,
                    out WorkTypeDefinition definition)
                ? definition.DisplayName
                : workTypeId.ToString()));
    }

    internal static string FormatLegacyWorkTypes(FacilityWorkType workTypes)
    {
        return string.Join(", ", WorkTypeCatalog
            .Enumerate(workTypes)
            .Select((definition) => definition.DisplayName));
    }

    public static string FormatDefenseConcept(DefenseAttackConcept concept)
    {
        return concept switch
        {
            DefenseAttackConcept.Physical => "물리",
            DefenseAttackConcept.Poison => "독",
            DefenseAttackConcept.Fire => "화염",
            DefenseAttackConcept.Lightning => "번개",
            DefenseAttackConcept.Ice => "냉기",
            DefenseAttackConcept.Guard => "경비",
            _ => "없음"
        };
    }

    public static string FormatTriggerTimings(DefenseTriggerTiming timings)
    {
        if (timings == DefenseTriggerTiming.None)
        {
            return "없음";
        }

        return string.Join(", ", Enum.GetValues(typeof(DefenseTriggerTiming))
            .Cast<DefenseTriggerTiming>()
            .Where((timing) => timing != DefenseTriggerTiming.None && (timings & timing) != 0)
            .Select((timing) => timing switch
            {
                DefenseTriggerTiming.OnEnter => "입장 시",
                DefenseTriggerTiming.Periodic => "머무는 동안",
                DefenseTriggerTiming.Cooldown => "쿨타임",
                DefenseTriggerTiming.GuardResponse => "경비 반응",
                _ => timing.ToString()
            }));
    }

    public static string FormatTargetRule(DefenseTargetRule targetRule)
    {
        return targetRule switch
        {
            DefenseTargetRule.EnteringIntruder => "입장한 침입자",
            DefenseTargetRule.IntrudersInRoom => "방 안 침입자",
            DefenseTargetRule.AllIntrudersInRoom => "방 안 모든 침입자",
            DefenseTargetRule.GuardTarget => "경비 대상",
            _ => targetRule.ToString()
        };
    }

    public static IEnumerable<string> FormatDefenseEffects(DefenseFacilityData defense)
    {
        return (defense?.effectAssets ?? Array.Empty<DefenseEffectSO>())
            .Where((effect) => effect != null)
            .Select((effect) => effect.FormatSummary());
    }

}
