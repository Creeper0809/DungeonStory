using UnityEngine;

public sealed class AIHunt : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.Work,
        "사냥",
        CharacterAiActionTags.Work,
        "work:hunt",
        "work:dangerous",
        "work:inspect-scout");

    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    public override bool RequiresDestination => false;
    public override bool IsContinuous => true;
    public override float MinimumDuration => 0.25f;
    public override int InterruptPriority => 44;

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        CharacterAiDecisionContext context =
            CharacterAiDecisionContext.Capture(actor, CharacterAiBranch.Work);
        return AdjustScore(actor, in context, baseScore);
    }

    public override float AdjustScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        float baseScore)
    {
        if (!TryGetEnabledPriority(actor, out WorkPriorityLevel priority))
        {
            return 0f;
        }

        AbilityHunt hunt = AbilityHunt.Ensure(actor);
        if (hunt == null || !hunt.CanStartHunting(out _))
        {
            return 0f;
        }

        float priorityWeight = priority switch
        {
            WorkPriorityLevel.Priority1 => 1f,
            WorkPriorityLevel.Priority2 => 0.82f,
            WorkPriorityLevel.Priority3 => 0.52f,
            _ => 0f
        };
        float survivalBoost = context.FoodStockPressure >= 0.85f ? 1.35f : 1f;
        float healthWindow = Mathf.Lerp(1f, 0.35f, Mathf.Max(context.HealthUrgency, context.InjuryUrgency));
        float weatherWindow = Mathf.Lerp(1f, 0.72f, context.WeatherPressure);
        float threatCaution = Mathf.Lerp(1f, 0.68f, context.NearbyWildlifeThreat);
        return Mathf.Clamp01(baseScore * priorityWeight * survivalBoost * healthWindow * weatherWindow * threatCaution);
    }

    public override bool CanStart(CharacterActor actor)
    {
        return TryGetEnabledPriority(actor, out _)
            && AbilityHunt.Ensure(actor)?.CanStartHunting(out _) == true;
    }

    public override bool CanContinue(CharacterActor actor, AIAction runningAction, out string stopReason)
    {
        stopReason = string.Empty;
        AbilityHunt hunt = actor != null ? actor.GetComponent<AbilityHunt>() : null;
        return hunt != null && hunt.IsHunting;
    }

    public override bool CanInterrupt(CharacterActor actor, AIAction runningAction, out string interruptReason)
    {
        interruptReason = string.Empty;
        return false;
    }

    public override void Execute(CharacterActor actor)
    {
        AbilityHunt.Ensure(actor)?.StartHunting();
    }

    public override void OnStop(CharacterActor actor, AIAction runningAction, string reason)
    {
        actor?.GetComponent<AbilityHunt>()?.StopHunting(reason);
    }

    private static bool TryGetEnabledPriority(CharacterActor actor, out WorkPriorityLevel priority)
    {
        priority = WorkPriorityLevel.Off;
        return actor != null
            && actor.TryGetAbility(out AbilityWork work)
            && work.WorkPriorities != null
            && (priority = work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Hunt))
                != WorkPriorityLevel.Off;
    }
}
