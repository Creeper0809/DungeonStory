using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface ICharacterSettlementStandingQuery
{
    CharacterSettlementStanding GetStanding(CharacterActor actor);
    CharacterSettlementStanding GetStanding(string persistentCharacterId);
    CharacterSettlementPopulationSnapshot GetSettlementPopulation();
    bool IsFormalResident(CharacterActor actor);
    bool IsMinion(CharacterActor actor);
    bool CanJoinExpedition(CharacterActor actor, out string failureReason);
    bool CanParticipateInMentoring(
        CharacterActor actor,
        out string failureReason);
    bool IsWorkAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string failureReason);
    float GetApprovedWorkExperienceMultiplier(CharacterActor actor);
}

public static class MinionIntegrationRules
{
    public const int MinimumMinionCaptureDays = 3;
    public const int MinimumRecruitCaptureDays = 10;
    public const int RequiredRehabilitationDays = 15;
    public const float MinimumMinionCorruption = 80f;
    public const float MinimumRecruitTrust = 70f;
    public const float MaximumRecruitGrudge = 30f;
    public const float MaximumDirectRecruitCorruption = 60f;
    public const float MaximumRehabilitatedCorruption = 30f;
    public const float MinionApprovedWorkExperienceMultiplier = 0.5f;
    public const float RehabilitationRequiredWork =
        CaptivityStateTransitionRules.RehabilitationRequiredWork;
    public const int RehabilitationFoodCost = 1;
    public const float RehabilitationTrustDelta = 5f;
    public const float RehabilitationGrudgeDelta = -3f;
    public const float RehabilitationCorruptionDelta = -6f;
    public const int ConversionResidentMoodDays = 2;
    public const float ConversionResidentMoodDelta = -6f;
    public const int OriginFactionGrievanceDelta = 12;

    private static readonly HashSet<WorkTypeId> AllowedWorkTypes = new()
    {
        BuiltInWorkTypeIds.Operate,
        BuiltInWorkTypeIds.Restock,
        BuiltInWorkTypeIds.Construct,
        BuiltInWorkTypeIds.Repair,
        BuiltInWorkTypeIds.Clean,
        BuiltInWorkTypeIds.Guard,
        BuiltInWorkTypeIds.Rescue,
        BuiltInWorkTypeIds.Rest,
        BuiltInWorkTypeIds.Craft,
        BuiltInWorkTypeIds.Haul,
        BuiltInWorkTypeIds.Hunt,
        BuiltInWorkTypeIds.Butcher,
        BuiltInWorkTypeIds.DrawWater,
        BuiltInWorkTypeIds.Cook,
        BuiltInWorkTypeIds.Refuel,
        BuiltInWorkTypeIds.Gather,
        BuiltInWorkTypeIds.Sow,
        BuiltInWorkTypeIds.Harvest,
        BuiltInWorkTypeIds.Logging,
        BuiltInWorkTypeIds.Quarry,
        BuiltInWorkTypeIds.AnimalCare,
        BuiltInWorkTypeIds.Plumbing,
        BuiltInWorkTypeIds.Dismantle
    };

    public static IReadOnlyCollection<WorkTypeId> AllowedWorkTypeIds =>
        AllowedWorkTypes.OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();

    public static int CaptiveDays(int capturedAbsoluteDay, int currentAbsoluteDay)
    {
        return Math.Max(0, currentAbsoluteDay - Math.Max(0, capturedAbsoluteDay));
    }

    public static bool CanConvertToMinion(
        float corruption,
        int capturedAbsoluteDay,
        int currentAbsoluteDay) => corruption >= MinimumMinionCorruption
        && CaptiveDays(capturedAbsoluteDay, currentAbsoluteDay)
            >= MinimumMinionCaptureDays;

    public static bool CanRecruitDirectly(
        float trust,
        float grudge,
        float corruption,
        int capturedAbsoluteDay,
        int currentAbsoluteDay) => trust >= MinimumRecruitTrust
        && grudge <= MaximumRecruitGrudge
        && corruption < MaximumDirectRecruitCorruption
        && CaptiveDays(capturedAbsoluteDay, currentAbsoluteDay)
            >= MinimumRecruitCaptureDays;

    public static bool CanRecruitRehabilitated(
        float trust,
        float grudge,
        float corruption,
        int rehabilitationDays) => rehabilitationDays
            >= RequiredRehabilitationDays
        && trust >= MinimumRecruitTrust
        && grudge <= MaximumRecruitGrudge
        && corruption <= MaximumRehabilitatedCorruption;

    public static bool IsWorkAllowed(WorkTypeId workTypeId) =>
        workTypeId.IsValid && AllowedWorkTypes.Contains(workTypeId);

    public static float ResolveMinionRatio(int residents, int minions)
    {
        int total = Math.Max(0, residents) + Math.Max(0, minions);
        return total > 0 ? Math.Max(0, minions) / (float)total : 0f;
    }

    public static int ResolveResidentMoodDelta(float minionRatio)
    {
        float ratio = Mathf.Clamp01(minionRatio);
        if (ratio >= 0.50f) return -9;
        if (ratio >= 0.25f) return -5;
        if (ratio >= 0.10f) return -2;
        return 0;
    }

    public static float ResolveConflictChancePercent(
        float minionRatio,
        float grudge,
        float mood) => Mathf.Clamp(
        5f
            + 20f * Mathf.Clamp01(minionRatio)
            + 0.1f * Mathf.Clamp(grudge, 0f, 100f)
            + 0.3f * Mathf.Max(0f, 50f - Mathf.Clamp(mood, 0f, 100f)),
        5f,
        35f);

    public static int ResolveDailyConflictLimit(int minionCount) =>
        Math.Max(0, (Math.Max(0, minionCount) + 3) / 4);

    public static float ResolveBrawlChancePercent(
        float relationship,
        float minionMood,
        float residentMood) => relationship <= -0.2f
            || minionMood <= 25f
            || residentMood <= 25f
                ? 40f
                : 10f;

    public static float ResolveControlStability(
        float corruption,
        float trust,
        float grudge) => Mathf.Max(
            Mathf.Clamp(corruption, 0f, 100f),
            Mathf.Clamp(trust, 0f, 100f))
        - Mathf.Clamp(grudge, 0f, 100f) * 0.25f;

    public static float ResolveControlBreakChancePercent(
        float corruption,
        float trust,
        float grudge,
        float mood)
    {
        float stability = ResolveControlStability(corruption, trust, grudge);
        float normalizedMood = Mathf.Clamp(mood, 0f, 100f);
        if (stability >= 30f && normalizedMood >= 20f)
        {
            return 0f;
        }
        return Mathf.Min(
            10f,
            Mathf.Max(0f, 30f - stability) * 0.25f
                + Mathf.Max(0f, 20f - normalizedMood) * 0.25f);
    }
}
