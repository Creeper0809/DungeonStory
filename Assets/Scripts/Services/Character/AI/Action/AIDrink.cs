using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Drink", order = 0)]
public sealed class AIDrink : AIActionSet
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor =
        new CharacterAiActionDescriptor(
            CharacterAiBranch.Drink,
            "음수",
            CharacterAiActionTags.SelfCare);

    public override CharacterAiActionDescriptor Descriptor =>
        ActionDescriptor;
    public override bool RequiresDestination => false;
    public override bool IsContinuous => true;
    public override int InterruptPriority => 60;

    public override bool CanStart(CharacterActor actor)
    {
        return actor?.DeprivationQuery?
            .NeedsRoutineDrink(actor, out _) == true;
    }

    public override bool CanStart(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        return CanStart(actor);
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        float utility = CharacterNeedAiThresholds.GetRoutineUtility(
            actor,
            CharacterCondition.THIRST);
        return Mathf.Clamp01(Mathf.Max(baseScore, utility));
    }

    public override bool CanContinue(
        CharacterActor actor,
        AIAction runningAction,
        out string stopReason)
    {
        stopReason = string.Empty;
        if (actor?.DeprivationQuery?
                .IsRoutineDrinkActionActive(actor) == true)
        {
            return true;
        }

        stopReason = "The routine safe-drink transaction is no longer active.";
        return false;
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
        AIBrain brain = actor?.Brain;
        AIAction expectedAction = brain?.bestAction;
        string status = string.Empty;
        bool accepted = actor?.DeprivationCommands?
            .TryRunRoutineDrink(actor, out status) == true;
        if (brain == null || brain.IsExternallyDrivenActionActive)
        {
            return;
        }

        // Routine safe-drink execution now remains under the selected
        // AIDrink epoch so source/lease faults can terminate as typed Failed.
        // Only a deferred retry returns ownership to the scheduler here.
        if (accepted
            && actor.DeprivationQuery.IsRoutineDrinkActionActive(actor))
        {
            return;
        }

        // Safe-drink planning may intentionally defer because of its retry
        // cooldown, per-frame admission budget, or temporary lack of water.
        // Deferred is not a running action: the runner owns a timer and wakes
        // the scheduler later. Keeping AIDrink started here suppresses Execute
        // forever and leaves the character staring at an ownerless action.
        bool deferred = brain.DeferExpectedActionWithoutImmediateDecision(
            expectedAction,
            accepted
                ? status
                : string.IsNullOrWhiteSpace(status)
                    ? "routine-drink-start-rejected"
                    : status);
        if (deferred && !accepted)
        {
            brain.RequestImmediateDecision(
                "Routine drink start was rejected before a retry timer was acquired.");
        }
    }
}
