using System;
using UnityEngine;

public static class CharacterMoodImpulseUtility
{
    private const float GoodMoodRoutineStart = 0.72f;
    private const float LowMoodAutonomyStart = 0.38f;
    private const float CriticalMoodAutonomyStart = 0.20f;
    private const float StrongImpulseInterruptThreshold = 0.65f;

    public static float GetMood01(CharacterActor actor)
    {
        return Mathf.Clamp01(GetCondition(actor, CharacterCondition.MOOD) / 100f);
    }

    public static float GetGoodMoodAdherenceMultiplier(
        CharacterActor actor,
        AIActionSet actionSet,
        float configuredMultiplier)
    {
        float mood01 = GetMood01(actor);
        if (mood01 < GoodMoodRoutineStart)
        {
            return configuredMultiplier;
        }

        float distanceFromNeutral = configuredMultiplier - 1f;
        if (Mathf.Abs(distanceFromNeutral) < 0.001f)
        {
            return configuredMultiplier;
        }

        float moodWeight = Mathf.InverseLerp(GoodMoodRoutineStart, 1f, mood01);
        float amplified = 1f + distanceFromNeutral * (1f + moodWeight * 0.35f);
        return Mathf.Clamp(amplified, 0.1f, 4.5f);
    }

    public static float ApplyRoutineBias(
        CharacterActor actor,
        CharacterAiBranch routineBranch,
        float priority,
        out string reasonSuffix)
    {
        reasonSuffix = string.Empty;
        if (actor == null)
        {
            return Mathf.Clamp(priority, 0f, 100f);
        }

        float adjusted = priority;
        float mood01 = GetMood01(actor);
        if (priority > 0f && mood01 >= GoodMoodRoutineStart)
        {
            float moodWeight = Mathf.InverseLerp(GoodMoodRoutineStart, 1f, mood01);
            if (routineBranch == CharacterAiBranch.DutyWork && IsOnDuty(actor))
            {
                adjusted += Mathf.Lerp(2f, 10f, moodWeight);
                reasonSuffix = AppendReason(reasonSuffix, $"goodMoodAdherence={moodWeight:0.###}");
            }
            else if (routineBranch == CharacterAiBranch.LeisureVisit && !IsOnDuty(actor))
            {
                adjusted += Mathf.Lerp(1f, 6f, moodWeight);
                reasonSuffix = AppendReason(reasonSuffix, $"goodMoodLeisure={moodWeight:0.###}");
            }
        }

        if (priority > 0f && mood01 < LowMoodAutonomyStart)
        {
            float lowMoodWeight = Mathf.InverseLerp(LowMoodAutonomyStart, 0f, mood01);
            if (routineBranch == CharacterAiBranch.DutyWork)
            {
                adjusted *= Mathf.Lerp(1f, 0.55f, lowMoodWeight);
                reasonSuffix = AppendReason(reasonSuffix, $"lowMoodDutyDrop={lowMoodWeight:0.###}");
            }
            else if (routineBranch == CharacterAiBranch.LeisureVisit)
            {
                adjusted = Mathf.Max(adjusted, Mathf.Lerp(18f, 44f, lowMoodWeight));
                reasonSuffix = AppendReason(reasonSuffix, $"lowMoodLeisure={lowMoodWeight:0.###}");
            }
            else if (routineBranch == CharacterAiBranch.Idle)
            {
                adjusted = Mathf.Max(adjusted, Mathf.Lerp(22f, 52f, lowMoodWeight));
                reasonSuffix = AppendReason(reasonSuffix, $"lowMoodAutonomy={lowMoodWeight:0.###}");
            }
        }

        if (!TryGetActiveImpulse(actor, out CharacterMoodImpulse impulse))
        {
            return Mathf.Clamp(adjusted, 0f, 100f);
        }

        float strength = Mathf.Clamp01(impulse.strength);
        switch (routineBranch)
        {
            case CharacterAiBranch.SurvivalNeeds:
                if (IsSurvivalImpulse(impulse.type))
                {
                    adjusted = Mathf.Max(adjusted, 30f + strength * 60f);
                    reasonSuffix = AppendImpulseReason(reasonSuffix, impulse);
                }
                break;

            case CharacterAiBranch.DutyWork:
                if (impulse.type == CharacterMoodImpulseType.FollowRoutine)
                {
                    adjusted += 8f + strength * 14f;
                    reasonSuffix = AppendImpulseReason(reasonSuffix, impulse);
                }
                else if (impulse.type == CharacterMoodImpulseType.IgnoreDuty)
                {
                    adjusted *= Mathf.Lerp(1f, 0.18f, strength);
                    reasonSuffix = AppendImpulseReason(reasonSuffix, impulse);
                }
                else if (IsTemperamentalImpulse(impulse.type))
                {
                    adjusted *= Mathf.Lerp(1f, 0.65f, strength);
                    reasonSuffix = AppendImpulseReason(reasonSuffix, impulse);
                }
                break;

            case CharacterAiBranch.LeisureVisit:
                if (IsLeisureImpulse(impulse.type))
                {
                    adjusted = Mathf.Max(adjusted, 18f + strength * 62f);
                    reasonSuffix = AppendImpulseReason(reasonSuffix, impulse);
                }
                break;

            case CharacterAiBranch.Idle:
                if (impulse.type == CharacterMoodImpulseType.Wait
                    || impulse.type == CharacterMoodImpulseType.Wander
                    || impulse.type == CharacterMoodImpulseType.IgnoreDuty
                    || impulse.type == CharacterMoodImpulseType.Complain)
                {
                    adjusted = Mathf.Max(adjusted, 12f + strength * 45f);
                    reasonSuffix = AppendImpulseReason(reasonSuffix, impulse);
                }
                break;
        }

        return Mathf.Clamp(adjusted, 0f, 100f);
    }

    public static float ApplyJobGiverBias(
        CharacterActor actor,
        CharacterAiBranch branch,
        float domainScore,
        out string reasonSuffix)
    {
        reasonSuffix = string.Empty;
        float adjusted = Mathf.Clamp01(domainScore);
        float mood01 = GetMood01(actor);
        if (mood01 < LowMoodAutonomyStart)
        {
            float lowMoodWeight = Mathf.InverseLerp(LowMoodAutonomyStart, 0f, mood01);
            if (branch == CharacterAiBranch.Work)
            {
                adjusted *= Mathf.Lerp(1f, 0.2f, lowMoodWeight);
                reasonSuffix = AppendReason(reasonSuffix, $"lowMoodDutyDrop={lowMoodWeight:0.###}");
            }
            else if (branch == CharacterAiBranch.Wait || branch == CharacterAiBranch.LookAround)
            {
                adjusted = Mathf.Max(adjusted, Mathf.Lerp(0.48f, 0.9f, lowMoodWeight));
                reasonSuffix = AppendReason(reasonSuffix, $"lowMoodAutonomy={lowMoodWeight:0.###}");
            }
        }

        if (!TryGetActiveImpulse(actor, out CharacterMoodImpulse impulse))
        {
            return adjusted;
        }

        float strength = Mathf.Clamp01(impulse.strength);
        if (MatchesBranch(impulse.type, branch))
        {
            adjusted = Mathf.Max(adjusted, 0.22f + strength * 0.62f);
            reasonSuffix = AppendImpulseReason(reasonSuffix, impulse);
            return Mathf.Clamp01(adjusted);
        }

        if (branch == CharacterAiBranch.Work && impulse.type == CharacterMoodImpulseType.IgnoreDuty)
        {
            adjusted *= Mathf.Lerp(1f, 0.12f, strength);
            reasonSuffix = AppendImpulseReason(reasonSuffix, impulse);
            return Mathf.Clamp01(adjusted);
        }

        if (branch == CharacterAiBranch.Work && IsTemperamentalImpulse(impulse.type))
        {
            adjusted *= Mathf.Lerp(1f, 0.7f, strength);
            reasonSuffix = AppendImpulseReason(reasonSuffix, impulse);
        }

        return Mathf.Clamp01(adjusted);
    }

    public static bool ShouldInterruptCurrentAction(
        CharacterActor actor,
        AIAction runningAction,
        out string reason)
    {
        reason = string.Empty;
        if (runningAction == null || runningAction.actionset == null)
        {
            return false;
        }

        CharacterAiBranch runningBranch = GetBranchForActionSet(runningAction.actionset);
        if (runningBranch == CharacterAiBranch.Work
            && GetMood01(actor) <= CriticalMoodAutonomyStart)
        {
            reason = "기분이 바닥나 일을 멈추고 제멋대로 행동";
            return true;
        }

        if (!TryGetActiveImpulse(actor, out CharacterMoodImpulse impulse)
            || impulse.strength < StrongImpulseInterruptThreshold
            || impulse.type == CharacterMoodImpulseType.FollowRoutine)
        {
            return false;
        }

        if (impulse.type == CharacterMoodImpulseType.AvoidFacility
            && MatchesFacilityTarget(runningAction.destination, impulse))
        {
            reason = $"Mood impulse {impulse.type} avoids {GetFacilityLabel(runningAction.destination)}: {impulse.reason}";
            return true;
        }

        if (runningBranch == CharacterAiBranch.None)
        {
            return false;
        }

        if (MatchesBranch(impulse.type, runningBranch))
        {
            return false;
        }

        if (impulse.type == CharacterMoodImpulseType.IgnoreDuty && runningBranch == CharacterAiBranch.Work)
        {
            reason = $"Mood impulse {impulse.type} interrupts {runningBranch}: {impulse.reason}";
            return true;
        }

        if (!IsTemperamentalImpulse(impulse.type) && !IsSurvivalImpulse(impulse.type))
        {
            return false;
        }

        reason = $"Mood impulse {impulse.type} interrupts {runningBranch}: {impulse.reason}";
        return true;
    }

    public static bool ShouldPreferAutonomousIdle(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (actor == null)
        {
            return false;
        }

        float mood01 = GetMood01(actor);
        if (mood01 < LowMoodAutonomyStart)
        {
            reason = mood01 <= CriticalMoodAutonomyStart
                ? "기분이 바닥나 제멋대로 서성이는 중"
                : "기분을 풀려고 잠시 서성이는 중";
            return true;
        }

        if (!TryGetActiveImpulse(actor, out CharacterMoodImpulse impulse)
            || !IsTemperamentalImpulse(impulse.type))
        {
            return false;
        }

        reason = impulse.type switch
        {
            CharacterMoodImpulseType.IgnoreDuty => "일을 제쳐두고 제멋대로 돌아다니는 중",
            CharacterMoodImpulseType.Complain => "투덜거리며 주변을 서성이는 중",
            CharacterMoodImpulseType.Vandalize => "화를 삭이지 못하고 거칠게 돌아다니는 중",
            CharacterMoodImpulseType.Wait => "마음을 식히며 서성이는 중",
            _ => "기분 내키는 대로 돌아다니는 중"
        };
        return true;
    }

    public static float ApplyFinalAutonomyBias(
        CharacterActor actor,
        CharacterAiBranch branch,
        float utility)
    {
        float adjusted = Mathf.Clamp01(utility);
        if (!ShouldPreferAutonomousIdle(actor, out _))
        {
            return adjusted;
        }

        return branch switch
        {
            CharacterAiBranch.Work => adjusted * 0.15f,
            CharacterAiBranch.Wait => Mathf.Max(adjusted, 0.88f),
            CharacterAiBranch.LookAround => Mathf.Max(adjusted, 0.82f),
            _ => adjusted
        };
    }

    public static CharacterAiBranch GetBranchForActionSet(AIActionSet actionSet)
    {
        return actionSet?.Branch ?? CharacterAiBranch.None;
    }

    public static bool MatchesBranch(CharacterMoodImpulseType impulse, CharacterAiBranch branch)
    {
        return impulse switch
        {
            CharacterMoodImpulseType.FollowRoutine => branch == CharacterAiBranch.Work
                || branch == CharacterAiBranch.DutyWork
                || branch == CharacterAiBranch.LeisureVisit,
            CharacterMoodImpulseType.SeekFood => branch == CharacterAiBranch.Eat
                || branch == CharacterAiBranch.SurvivalNeeds,
            CharacterMoodImpulseType.SeekRest => branch == CharacterAiBranch.Rest
                || branch == CharacterAiBranch.SurvivalNeeds,
            CharacterMoodImpulseType.SeekToilet => branch == CharacterAiBranch.Toilet
                || branch == CharacterAiBranch.SurvivalNeeds,
            CharacterMoodImpulseType.SeekHygiene => branch == CharacterAiBranch.Hygiene
                || branch == CharacterAiBranch.SurvivalNeeds,
            CharacterMoodImpulseType.SeekFun => branch == CharacterAiBranch.Shopping
                || branch == CharacterAiBranch.LookAround
                || branch == CharacterAiBranch.LeisureVisit,
            CharacterMoodImpulseType.ImpulseShopping => branch == CharacterAiBranch.Shopping
                || branch == CharacterAiBranch.LeisureVisit,
            CharacterMoodImpulseType.Wander => branch == CharacterAiBranch.LookAround
                || branch == CharacterAiBranch.Idle
                || branch == CharacterAiBranch.LeisureVisit,
            CharacterMoodImpulseType.Wait => branch == CharacterAiBranch.Wait
                || branch == CharacterAiBranch.Idle,
            CharacterMoodImpulseType.IgnoreDuty => branch == CharacterAiBranch.Wait
                || branch == CharacterAiBranch.LookAround
                || branch == CharacterAiBranch.Idle
                || branch == CharacterAiBranch.LeisureVisit,
            CharacterMoodImpulseType.Complain => branch == CharacterAiBranch.Wait
                || branch == CharacterAiBranch.LookAround
                || branch == CharacterAiBranch.Idle,
            CharacterMoodImpulseType.ExitDungeon => branch == CharacterAiBranch.ExitDungeon
                || branch == CharacterAiBranch.SurvivalNeeds,
            CharacterMoodImpulseType.Vandalize => branch == CharacterAiBranch.LookAround
                || branch == CharacterAiBranch.Wait
                || branch == CharacterAiBranch.Idle,
            _ => false
        };
    }

    public static string AppendReason(string reason, string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return reason ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(reason)
            ? suffix
            : $"{reason} {suffix}";
    }

    private static bool TryGetActiveImpulse(CharacterActor actor, out CharacterMoodImpulse impulse)
    {
        impulse = null;
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        if (blackboard == null || !blackboard.HasActiveMoodImpulse())
        {
            return false;
        }

        impulse = blackboard.ActiveMoodImpulse;
        return impulse != null
            && impulse.type != CharacterMoodImpulseType.None
            && impulse.strength > 0f;
    }

    private static bool IsOnDuty(CharacterActor actor)
    {
        return CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
            && !work.IsOffDuty;
    }

    private static bool IsSurvivalImpulse(CharacterMoodImpulseType impulse)
    {
        return impulse == CharacterMoodImpulseType.SeekFood
            || impulse == CharacterMoodImpulseType.SeekRest
            || impulse == CharacterMoodImpulseType.SeekToilet
            || impulse == CharacterMoodImpulseType.SeekHygiene
            || impulse == CharacterMoodImpulseType.ExitDungeon;
    }

    private static bool IsLeisureImpulse(CharacterMoodImpulseType impulse)
    {
        return impulse == CharacterMoodImpulseType.SeekFun
            || impulse == CharacterMoodImpulseType.ImpulseShopping
            || impulse == CharacterMoodImpulseType.Wander
            || impulse == CharacterMoodImpulseType.IgnoreDuty
            || impulse == CharacterMoodImpulseType.Complain
            || impulse == CharacterMoodImpulseType.Vandalize;
    }

    private static bool IsTemperamentalImpulse(CharacterMoodImpulseType impulse)
    {
        return impulse == CharacterMoodImpulseType.SeekFun
            || impulse == CharacterMoodImpulseType.ImpulseShopping
            || impulse == CharacterMoodImpulseType.Wander
            || impulse == CharacterMoodImpulseType.Wait
            || impulse == CharacterMoodImpulseType.IgnoreDuty
            || impulse == CharacterMoodImpulseType.Complain
            || impulse == CharacterMoodImpulseType.ExitDungeon
            || impulse == CharacterMoodImpulseType.Vandalize;
    }

    private static bool MatchesFacilityTarget(BuildableObject facility, CharacterMoodImpulse impulse)
    {
        if (facility == null || impulse == null)
        {
            return false;
        }

        if (impulse.targetFacilityId >= 0 && facility.id == impulse.targetFacilityId)
        {
            return true;
        }

        string tag = impulse.targetFacilityTag;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return facility.HasSemanticTag(tag);
    }

    private static string AppendImpulseReason(string reason, CharacterMoodImpulse impulse)
    {
        return AppendReason(reason, $"moodImpulse={impulse.type}:{impulse.strength:0.###}");
    }

    private static string GetFacilityLabel(BuildableObject facility)
    {
        if (facility == null)
        {
            return "None";
        }

        return facility.BuildingData != null && !string.IsNullOrWhiteSpace(facility.BuildingData.objectName)
            ? facility.BuildingData.objectName
            : facility.name;
    }

    private static float GetCondition(CharacterActor actor, CharacterCondition condition)
    {
        return actor != null
            && actor.Stats != null
            && actor.Stats.Stats != null
            && actor.Stats.Stats.TryGetValue(condition, out float value)
                ? value
                : 100f;
    }
}
