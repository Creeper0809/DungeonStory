using UnityEngine;

public sealed class AIHaul : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new CharacterAiActionDescriptor(
        CharacterAiBranch.Work,
        "운반",
        CharacterAiActionTags.Work,
        "work:haul",
        "work:heavy-haul");

    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    public override bool RequiresDestination => false;
    public override bool IsContinuous => true;
    public override float MinimumDuration => 0.25f;
    public override int InterruptPriority => 42;

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
        AbilityHaul haul = AbilityHaul.Ensure(actor);
        if (haul == null || !haul.CanStartHauling(out _))
        {
            return 0f;
        }

        // A restored pickup-committed delivery is already owned physical work.
        // It is resumed by the ordinary Brain -> AIHaul path after restore,
        // independent of whether autonomous hauling is currently enabled.
        if (haul.HasBoundDeliveryIntent)
        {
            return Mathf.Clamp01(Mathf.Max(baseScore, 0.98f));
        }

        if (!TryGetEnabledPriority(actor, out WorkPriorityLevel priority))
        {
            return 0f;
        }

        float priorityWeight = priority switch
        {
            WorkPriorityLevel.Priority1 => 1f,
            WorkPriorityLevel.Priority2 => 0.78f,
            WorkPriorityLevel.Priority3 => 0.48f,
            _ => 0f
        };
        float loadWindow = Mathf.Lerp(1f, 0.72f, context.CarryLoad);
        float pressureBoost = Mathf.Lerp(1f, 1.24f, Mathf.Max(context.FoodStockPressure, context.WaterStockPressure));
        float pathStability = Mathf.Lerp(0.86f, 1.08f, context.PathConfidence);
        float failureDrag = Mathf.Lerp(1f, 0.78f, context.RecentFailurePressure);
        return Mathf.Clamp01(baseScore * priorityWeight * loadWindow * pressureBoost * pathStability * failureDrag);
    }

    public override bool CanStart(CharacterActor actor)
    {
        AbilityHaul haul = AbilityHaul.Ensure(actor);
        return haul != null
            && (haul.HasBoundDeliveryIntent || TryGetEnabledPriority(actor, out _))
            && haul.CanStartHauling(out _);
    }

    public override bool CanContinue(CharacterActor actor, AIAction runningAction, out string stopReason)
    {
        stopReason = string.Empty;
        AbilityHaul haul = actor != null ? actor.GetComponent<AbilityHaul>() : null;
        return haul != null && haul.IsHauling;
    }

    public override bool CanInterrupt(CharacterActor actor, AIAction runningAction, out string interruptReason)
    {
        interruptReason = string.Empty;
        return false;
    }

    public override void Execute(CharacterActor actor)
    {
        AbilityHaul.Ensure(actor)?.StartHauling();
    }

    public override void OnStop(CharacterActor actor, AIAction runningAction, string reason)
    {
        actor?.GetComponent<AbilityHaul>()?.StopHaulingForReplan(reason);
    }

    private static bool TryGetEnabledPriority(CharacterActor actor, out WorkPriorityLevel priority)
    {
        priority = WorkPriorityLevel.Off;
        if (actor == null
            || !actor.TryGetAbility(out AbilityWork work)
            || work.WorkPriorities == null)
        {
            return false;
        }

        // A priority-work target is an explicit player command. AIHaul is a
        // separate autonomous work branch and must not become the score-based
        // fallback while the commanded AIWork candidate is waiting for its
        // path-search budget. Urgency still controls need/duty interruption;
        // it does not weaken ownership of the exact work command.
        if (work.PriorityWorkTarget != null)
        {
            return false;
        }

        priority = work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul);
        return priority != WorkPriorityLevel.Off;
    }
}
