public interface IWorkAmountCalculator
{
    float CalculateWorkPerSecond(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        float environmentDurationMultiplier);
}
