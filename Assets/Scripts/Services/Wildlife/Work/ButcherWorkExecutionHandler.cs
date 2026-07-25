using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ButcherWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Butcher };
    private readonly IWildlifeCarcassService carcassService;

    public ButcherWorkExecutionHandler(IWildlifeCarcassService carcassService)
    {
        this.carcassService = carcassService
            ?? throw new ArgumentNullException(nameof(carcassService));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        return carcassService.HasButcherWorkAvailable(target);
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return carcassService.GetButcherWorkUrgency();
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        result.CompletedSuccessfully =
            carcassService.HasButcherWorkAvailable(context.Target);
        if (!result.CompletedSuccessfully)
        {
            yield break;
        }

        float requiredWork = Mathf.Max(
            0.1f,
            context.Target.BuildingData?.GetAbility<BuildingButcherAbility>()?.workSeconds ?? 1f);
        yield return context.ExecuteWorkAmount(requiredWork, "도축");
        result.CompletedSuccessfully = context.CanContinue;
    }
}
