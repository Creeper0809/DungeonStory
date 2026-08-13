using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Primitive Bucket Wash", order = 0)]
public sealed class AIPrimitiveBucketWash : AIPrimitiveSurvivalAction
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new(
        CharacterAiBranch.Hygiene,
        "물로 간이 세척",
        CharacterAiActionTags.SelfCare,
        "survival:primitive");
    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    public override bool CanStart(CharacterActor actor) =>
        actor?.DeprivationQuery?.NeedsPrimitiveWash(actor, out _) == true
        && CanUsePrimitiveFallback(
            actor,
            FacilityRole.Hygiene,
            CharacterCondition.HYGIENE);
    public override float AdjustScore(CharacterActor actor, float baseScore) =>
        PrimitiveScore(actor, CharacterCondition.HYGIENE, baseScore);
    public override void Execute(CharacterActor actor)
    {
        if (RevalidateAtExecution(actor))
        {
            actor?.DeprivationCommands?.TryRunPrimitiveWash(actor, out _);
        }
    }
}
