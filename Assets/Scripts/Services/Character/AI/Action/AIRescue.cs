using UnityEngine;

public sealed class AIRescue : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor =
        new CharacterAiActionDescriptor(
            CharacterAiBranch.Work,
            "구조",
            CharacterAiActionTags.Work);

    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    public override bool RequiresDestination => false;
    public override bool IsContinuous => true;
    public override float MinimumDuration => 0.25f;
    public override int InterruptPriority => 96;
    public override bool AllowsSurvivalEmergencyInterrupt => false;

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        if (!TryGetEnabledPriority(actor, out WorkPriorityLevel priority)
            || AbilityRescue.Ensure(actor)?.CanStartRescue(out _) != true)
        {
            return 0f;
        }

        float priorityScore = priority switch
        {
            WorkPriorityLevel.Priority1 => 1f,
            WorkPriorityLevel.Priority2 => 0.92f,
            WorkPriorityLevel.Priority3 => 0.72f,
            _ => 0f
        };
        return Mathf.Clamp01(Mathf.Max(baseScore, 0.82f) * priorityScore);
    }

    public override bool CanStart(CharacterActor actor)
    {
        return TryGetEnabledPriority(actor, out _)
            && AbilityRescue.Ensure(actor)?.CanStartRescue(out _) == true;
    }

    public override bool CanContinue(
        CharacterActor actor,
        AIAction runningAction,
        out string stopReason)
    {
        stopReason = string.Empty;
        AbilityRescue rescue = actor != null ? actor.GetComponent<AbilityRescue>() : null;
        return rescue != null && rescue.IsRescuing;
    }

    public override bool CanInterrupt(
        CharacterActor actor,
        AIAction runningAction,
        out string interruptReason)
    {
        interruptReason = string.Empty;
        return false;
    }

    public override void Execute(CharacterActor actor)
    {
        AbilityRescue.Ensure(actor)?.StartRescue();
    }

    public override void OnStop(CharacterActor actor, AIAction runningAction, string reason)
    {
        actor?.GetComponent<AbilityRescue>()?.StopRescue(
            CharacterMedicalStatusCode.RescueInterrupted);
    }

    private static bool TryGetEnabledPriority(
        CharacterActor actor,
        out WorkPriorityLevel priority)
    {
        priority = WorkPriorityLevel.Off;
        return actor != null
            && !actor.IsDead
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && actor.TryGetAbility(out AbilityWork work)
            && work.WorkPriorities != null
            && (priority = work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Rescue))
                != WorkPriorityLevel.Off;
    }
}
