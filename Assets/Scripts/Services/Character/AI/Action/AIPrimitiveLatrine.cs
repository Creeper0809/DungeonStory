using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Primitive Latrine", order = 0)]
public sealed class AIPrimitiveLatrine : AIPrimitiveSurvivalAction
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new(
        CharacterAiBranch.Toilet,
        "임시 변소 사용",
        CharacterAiActionTags.SelfCare,
        "survival:primitive");
    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    public override bool CanStart(CharacterActor actor) =>
        actor?.DeprivationQuery?.NeedsPrimitiveRelief(actor, out _) == true
        && CanUsePrimitiveFallback(
            actor,
            FacilityRole.Toilet,
            CharacterCondition.EXCRETION);
    public override float AdjustScore(CharacterActor actor, float baseScore) =>
        PrimitiveScore(actor, CharacterCondition.EXCRETION, baseScore);
    public override void Execute(CharacterActor actor) =>
        actor?.DeprivationCommands?.TryRunPrimitiveRelief(actor, out _);
}
