using System;
using System.Collections.Generic;

public sealed class SurvivalBuildingAbilityHandler :
    IBuildingAbilityWorkCompletedHandler
{
    private static readonly Type[] Types =
    {
        typeof(BuildingWaterSourceAbility),
        typeof(BuildingCookingAbility),
        typeof(BuildingMedicalAbility),
        typeof(BuildingFuelConsumerAbility),
        typeof(BuildingGolemRechargeAbility)
    };

    private readonly ISurvivalFoodCommand survivalRuntime;

    public SurvivalBuildingAbilityHandler(ISurvivalFoodCommand survivalRuntime)
    {
        this.survivalRuntime = survivalRuntime
            ?? throw new ArgumentNullException(nameof(survivalRuntime));
    }

    public IReadOnlyCollection<Type> AbilityTypes => Types;

    public int Apply(BuildingAbility ability, BuildingAbilityWorkContext context)
    {
        return Supports(ability, context.WorkTypeId)
            && survivalRuntime.TryApplySurvivalWork(
                context.Actor,
                context.Building,
                context.WorkTypeId,
                out int amount,
                out _)
            ? amount
            : 0;
    }

    private static bool Supports(
        BuildingAbility ability,
        WorkTypeId workTypeId)
    {
        return ability is BuildingWaterSourceAbility
                && workTypeId == BuiltInWorkTypeIds.DrawWater
            || ability is BuildingCookingAbility
                && workTypeId == BuiltInWorkTypeIds.Cook
            || ability is BuildingMedicalAbility
                && workTypeId == BuiltInWorkTypeIds.Treat
            || ability is BuildingFuelConsumerAbility
                && workTypeId == BuiltInWorkTypeIds.Refuel
            || ability is BuildingGolemRechargeAbility
                && workTypeId == BuiltInWorkTypeIds.Refuel;
    }
}
