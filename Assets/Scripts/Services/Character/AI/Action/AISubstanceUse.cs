using UnityEngine;

public sealed class AISubstanceUse : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor =
        new CharacterAiActionDescriptor(
            CharacterAiBranch.Work,
            "정책에 따라 복용",
            CharacterAiActionTags.SelfCare);

    public override CharacterAiActionDescriptor Descriptor =>
        ActionDescriptor;
    public override bool RequiresDestination => false;
    public override bool IsContinuous => true;
    public override float MinimumDuration => 0.25f;
    public override int InterruptPriority => 72;
    public override bool AllowsSurvivalEmergencyInterrupt => false;

    public override float AdjustScore(
        CharacterActor actor,
        float baseScore)
    {
        AbilityUseSubstance ability =
            AbilityUseSubstance.Ensure(actor);
        if (ability == null
            || !ability.CanStart(out CharacterSubstanceUseRequest request))
        {
            return 0f;
        }

        return Mathf.Clamp01(
            Mathf.Max(baseScore, 0.55f) * request.Urgency);
    }

    public override bool CanStart(CharacterActor actor)
    {
        return AbilityUseSubstance.Ensure(actor)?
            .CanStart(out _) == true;
    }

    public override bool CanContinue(
        CharacterActor actor,
        AIAction runningAction,
        out string stopReason)
    {
        stopReason = string.Empty;
        return actor != null
            && actor.GetComponent<AbilityUseSubstance>()?.IsUsingSubstance
                == true;
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
        AbilityUseSubstance.Ensure(actor)?.StartUse();
    }

    public override void OnStop(
        CharacterActor actor,
        AIAction runningAction,
        string reason)
    {
        actor?.GetComponent<AbilityUseSubstance>()?.StopUse(reason);
    }
}
