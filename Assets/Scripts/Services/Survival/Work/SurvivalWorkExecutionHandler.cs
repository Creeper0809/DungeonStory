using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class SurvivalWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids =
    {
        BuiltInWorkTypeIds.DrawWater,
        BuiltInWorkTypeIds.Cook,
        BuiltInWorkTypeIds.Treat,
        BuiltInWorkTypeIds.Refuel
    };

    private readonly ISurvivalFoodRuntime survivalRuntime;
    private readonly IProductionBillRuntime productionBills;
    private readonly IReadOnlyDictionary<WorkTypeId, Func<BuildableObject, float>> workAmounts;

    public SurvivalWorkExecutionHandler(
        ISurvivalFoodRuntime survivalRuntime,
        IProductionBillRuntime productionBills = null)
    {
        this.survivalRuntime = survivalRuntime
            ?? throw new ArgumentNullException(nameof(survivalRuntime));
        this.productionBills = productionBills;
        workAmounts = new Dictionary<WorkTypeId, Func<BuildableObject, float>>
        {
            [BuiltInWorkTypeIds.DrawWater] = target =>
                target.BuildingData.GetAbility<BuildingWaterSourceAbility>()?.workSeconds ?? 1f,
            [BuiltInWorkTypeIds.Cook] = target =>
                target.BuildingData.GetAbility<BuildingCookingAbility>()?.workSeconds ?? 1f,
            [BuiltInWorkTypeIds.Treat] = target =>
                target.BuildingData.GetAbility<BuildingMedicalAbility>()?.workSeconds ?? 1f,
            [BuiltInWorkTypeIds.Refuel] = target =>
                target.BuildingData.GetAbility<BuildingFuelConsumerAbility>()?.workSeconds ?? 1f
        };
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        if (workTypeId == BuiltInWorkTypeIds.Cook
            && productionBills != null
            && productionBills.HasWorkAvailable(target, workTypeId, out reason))
        {
            return true;
        }

        reason = string.Empty;
        return workTypeId.IsValid
            && survivalRuntime.HasSurvivalWorkAvailable(target, workTypeId);
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return workTypeId.IsValid
            ? survivalRuntime.GetSurvivalWorkUrgency(target, workTypeId)
            : 0f;
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        if (context.WorkTypeId == BuiltInWorkTypeIds.Cook
            && productionBills != null
            && productionBills.TryBeginWork(
                context.Actor,
                context.Target,
                context.WorkTypeId,
                out ProductionBillSnapshot bill,
                out _))
        {
            bool applied = true;
            bool completed = false;
            yield return context.ExecutePersistentWorkAmount(
                bill.RequiredWork,
                bill.CompletedWork,
                bill.RecipeName,
                delta =>
                {
                    bool succeeded = productionBills.ApplyWork(
                        context.Actor,
                        context.Target,
                        bill.BillId,
                        delta,
                        out bool cycleCompleted,
                        out _);
                    applied &= succeeded;
                    completed |= cycleCompleted;
                    return succeeded;
                });
            result.CompletedSuccessfully = applied && completed;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }

        result.CompletedSuccessfully =
            survivalRuntime.HasSurvivalWorkAvailable(context.Target, context.WorkTypeId);
        if (!result.CompletedSuccessfully)
        {
            yield break;
        }

        if (!workAmounts.TryGetValue(
                context.WorkTypeId,
                out Func<BuildableObject, float> resolveAmount))
        {
            throw new InvalidOperationException(
                $"Survival handler does not support '{context.LegacyWorkType}'.");
        }

        yield return context.ExecuteWorkAmount(
            Mathf.Max(0.1f, resolveAmount(context.Target)),
            WorkTaskCatalog.GetLegacyDisplayName(context.LegacyWorkType));
        result.CompletedSuccessfully = context.CanContinue;
    }
}
