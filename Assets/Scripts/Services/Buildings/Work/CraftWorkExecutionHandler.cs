using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CraftWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Craft };
    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        return target != null && target.HasPendingEquipmentCraftWork();
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        result.CompletedSuccessfully =
            context.Target != null && context.Target.HasPendingEquipmentCraftWork();
        if (!result.CompletedSuccessfully)
        {
            yield break;
        }

        float requiredWork = Mathf.Max(
            0.1f,
            context.Target.BuildingData
                ?.GetAbility<BuildingEquipmentCraftingAbility>()
                ?.workSecondsPerCycle ?? 1f);
        yield return context.ExecuteWorkAmount(requiredWork, "제작");
        if (!context.CanContinue)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        int applied = ModularFacilityRuntimeEffects.ApplyWorkCompleted(
            context.Actor,
            context.Target,
            BuiltInWorkTypeIds.Craft);
        result.CompletedSuccessfully = applied > 0;
        result.CompletionEffectsAlreadyApplied = true;
    }
}
