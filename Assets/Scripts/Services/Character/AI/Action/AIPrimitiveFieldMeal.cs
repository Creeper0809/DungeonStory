using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Action/Primitive Field Meal", order = 0)]
public sealed class AIPrimitiveFieldMeal : AIPrimitiveSurvivalAction
{
    private static readonly CharacterAiActionDescriptor ActionDescriptor = new(
        CharacterAiBranch.Eat,
        "야전식 섭취",
        CharacterAiActionTags.SelfCare,
        "survival:primitive");
    public override CharacterAiActionDescriptor Descriptor => ActionDescriptor;
    public override bool CanStart(CharacterActor actor) =>
        actor?.DeprivationQuery?.NeedsPrimitiveMeal(actor, out _) == true
        && CanUsePrimitiveFallback(
            actor,
            FacilityRole.Meal,
            CharacterCondition.HUNGER);
    public override float AdjustScore(CharacterActor actor, float baseScore) =>
        PrimitiveScore(actor, CharacterCondition.HUNGER, baseScore);
    public override void Execute(CharacterActor actor) =>
        actor?.DeprivationCommands?.TryRunPrimitiveMeal(actor, out _);
}
