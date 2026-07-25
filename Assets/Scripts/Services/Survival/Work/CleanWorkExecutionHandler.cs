using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CleanWorkExecutionHandler : IWorkExecutionHandler
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Clean };
    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        WorldFilthWorkTarget filthTarget = context.Target as WorldFilthWorkTarget;
        float requiredWork = filthTarget != null
            ? Mathf.Max(0.1f, filthTarget.RequiredCleaningWork)
            : Mathf.Max(
                0.1f,
                context.Target.BuildingData.GetRequiredWork(BuiltInWorkTypeIds.Clean));
        float cleaningMultiplier =
            CharacterSkillRuntimeEffects.GetCleaningSpeedMultiplier(context.Actor);
        yield return context.ExecuteWorkAmount(requiredWork, "청소", cleaningMultiplier);
        if (!context.CanContinue)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        filthTarget?.CompleteCleaning(requiredWork);
    }
}
