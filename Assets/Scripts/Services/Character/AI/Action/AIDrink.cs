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

    public override void Execute(CharacterActor actor)
    {
        actor?.DeprivationCommands?.TryRunRoutineDrink(actor, out _);
    }
}
