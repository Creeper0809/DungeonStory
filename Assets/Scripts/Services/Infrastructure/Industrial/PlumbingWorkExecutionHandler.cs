using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlumbingWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids =
    {
        BuiltInWorkTypeIds.Plumbing
    };

    private readonly IFluidInfrastructureQuery query;
    private readonly IFluidInfrastructureCommand commands;

    public PlumbingWorkExecutionHandler(
        IFluidInfrastructureQuery query,
        IFluidInfrastructureCommand commands)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands
            ?? throw new ArgumentNullException(nameof(commands));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        if (workTypeId != BuiltInWorkTypeIds.Plumbing
            || !query.TryGetMaintenance(
                target,
                out float blockage,
                out float leak))
        {
            return false;
        }

        bool available = blockage > 0.01f || leak > 0.01f;
        if (!available)
        {
            reason = "수리할 배관 문제가 없습니다.";
        }

        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return workTypeId == BuiltInWorkTypeIds.Plumbing
            && query.TryGetMaintenance(
                target,
                out float blockage,
                out float leak)
            ? Mathf.Clamp01(Mathf.Max(blockage, leak) / 100f)
            : 0f;
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (!query.TryGetMaintenance(
                context.Target,
                out float blockage,
                out float leak)
            || blockage <= 0.01f && leak <= 0.01f)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        float requiredWork = 8f + blockage * 0.25f + leak * 0.3f;
        yield return context.ExecuteWorkAmount(
            requiredWork,
            leak >= blockage ? "누수 수리" : "막힘 제거");
        if (!context.CanContinue)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        if (blockage > 0.01f)
        {
            commands.ClearBlockage(context.Target);
        }

        if (leak > 0.01f)
        {
            commands.RepairLeak(context.Target);
        }

        result.CompletedSuccessfully = true;
        result.CompletionEffectsAlreadyApplied = true;
    }
}
