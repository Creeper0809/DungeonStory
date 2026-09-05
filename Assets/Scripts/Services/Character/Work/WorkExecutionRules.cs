using System;
using System.Linq;
using UnityEngine;

internal static class WorkExecutionRules
{
    private const float MinimumNaturalExteriorWorkSeconds = 3.2f;

    public static float CalculateWorkPerSecond(
        IWorkAmountCalculator calculator,
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        float environmentDurationMultiplier)
    {
        if (calculator != null && workTypeId.IsValid)
        {
            return calculator.CalculateWorkPerSecond(
                actor,
                target,
                workTypeId,
                environmentDurationMultiplier);
        }

        float workSpeed = actor != null
            ? Mathf.Max(
                0.1f,
                workTypeId.IsValid
                    ? actor.GetWorkSpeedMultiplier(workTypeId, target)
                    : 1f)
            : 1f;
        float environment =
            1f / Mathf.Max(0.1f, environmentDurationMultiplier);
        return WorkRateBoundsAuthority.Clamp(workSpeed * environment);
    }

    public static EnvironmentalWorkKind ResolveEnvironmentWorkKind(
        WorkTypeId workTypeId)
    {
        string id = workTypeId.Value ?? string.Empty;
        return id.IndexOf("research", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("craft", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("medical", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("treat", StringComparison.OrdinalIgnoreCase) >= 0
                ? EnvironmentalWorkKind.Precision
                : EnvironmentalWorkKind.General;
    }

    public static bool TryGetExteriorWorkSeconds(
        BuildableObject target,
        CharacterActor actor,
        WorkTypeId workTypeId,
        out float seconds)
    {
        seconds = 0f;
        if (target?.BuildingData == null)
        {
            return false;
        }

        IBuildingVisitorPort visitor = actor?.BuildingVisitor;
        foreach (IBuildingExteriorWorkRuntimeAbility ability
                 in target.BuildingData.Abilities
                     .OfType<IBuildingExteriorWorkRuntimeAbility>())
        {
            if (!ability.SupportsExteriorWork(workTypeId)
                || !ability.IsExteriorWorkAvailable(
                    visitor,
                    target,
                    workTypeId))
            {
                continue;
            }

            seconds = Mathf.Max(
                MinimumNaturalExteriorWorkSeconds,
                ability.GetExteriorWorkSeconds(
                    visitor,
                    target,
                    workTypeId));
            return true;
        }

        return false;
    }
}
