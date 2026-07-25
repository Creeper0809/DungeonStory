using System;
using System.Collections.Generic;

public sealed class ButcherBuildingAbilityHandler :
    IBuildingAbilityWorkCompletedHandler,
    IBuildingWorkCompletionFallbackHandler
{
    private static readonly Type[] Types = { typeof(BuildingButcherAbility) };
    private static readonly WorkTypeId[] WorkTypes = { BuiltInWorkTypeIds.Butcher };
    private readonly IWildlifeCarcassService carcassService;

    public ButcherBuildingAbilityHandler(IWildlifeCarcassService carcassService)
    {
        this.carcassService = carcassService
            ?? throw new ArgumentNullException(nameof(carcassService));
    }

    public IReadOnlyCollection<Type> AbilityTypes => Types;
    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => WorkTypes;

    public int Apply(BuildingAbility ability, BuildingAbilityWorkContext context)
    {
        return context.WorkTypeId == BuiltInWorkTypeIds.Butcher
            ? Apply(context)
            : 0;
    }

    public int Apply(BuildingAbilityWorkContext context)
    {
        return context.WorkTypeId == BuiltInWorkTypeIds.Butcher
            && WildlifeButcherFacilityUtility.IsButcherFacility(context.Building)
            && carcassService.TryButcherNextCarcass(
                context.Actor,
                context.Building,
                out int produced,
                out _)
                ? produced
                : 0;
    }
}
