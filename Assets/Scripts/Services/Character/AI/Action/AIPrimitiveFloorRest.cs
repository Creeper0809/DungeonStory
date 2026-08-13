using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Primitive Floor Rest", order = 0)]
public sealed class AIPrimitiveFloorRest : AIPrimitiveSurvivalAction
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new(
        CharacterAiBranch.Rest,
        "바닥 취침",
        CharacterAiActionTags.SelfCare,
        "survival:primitive");
    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    public override bool CanStart(CharacterActor actor) =>
        actor?.DeprivationQuery?.NeedsPrimitiveRest(actor, out _) == true
        && CanUsePrimitiveFallback(
            actor,
            FacilityRole.Rest,
            CharacterCondition.SLEEP);
    public override float AdjustScore(CharacterActor actor, float baseScore) =>
        PrimitiveScore(actor, CharacterCondition.SLEEP, baseScore);
    public override void Execute(CharacterActor actor)
    {
        if (RevalidateAtExecution(actor))
        {
            actor?.DeprivationCommands?.TryRunPrimitiveRest(actor, out _);
        }
    }
}
