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

    private readonly ISurvivalFoodQuery survivalRuntime;
    private readonly IProductionBillWorkExecution productionBills;
    private readonly IProcessFluidUseRuntime processFluids;
    private readonly IReadOnlyDictionary<WorkTypeId, Func<BuildableObject, float>> workAmounts;

    public SurvivalWorkExecutionHandler(
        ISurvivalFoodQuery survivalRuntime,
        IProductionBillWorkExecution productionBills,
        IProcessFluidUseRuntime processFluids)
    {
        this.survivalRuntime = survivalRuntime
            ?? throw new ArgumentNullException(nameof(survivalRuntime));
        this.productionBills = productionBills
            ?? throw new ArgumentNullException(nameof(productionBills));
        this.processFluids = processFluids
            ?? throw new ArgumentNullException(nameof(processFluids));
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
        if (workTypeId == BuiltInWorkTypeIds.Cook)
        {
            ProductionWorkAvailabilityResult availability =
                productionBills.CheckWorkAvailability(target, workTypeId);
            if (availability.Available)
            {
                return true;
            }
            reason = availability.Failure.IsFailure
                ? availability.Failure.Code.ToString()
                : string.Empty;
        }

        bool survivalAvailable = workTypeId.IsValid
            && survivalRuntime.HasSurvivalWorkAvailable(target, workTypeId);
        if (survivalAvailable)
        {
            reason = string.Empty;
        }
        return survivalAvailable;
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
        if (context.WorkTypeId == BuiltInWorkTypeIds.Cook)
        {
            ProductionWorkBeginResult begin = productionBills.BeginWork(
                context.Actor,
                context.Target,
                context.WorkTypeId);
            if (begin.Succeeded)
            {
                ProductionBillSnapshot bill = begin.Bill;
                bool applied = true;
                bool completed = false;
                yield return context.ExecutePersistentWorkAmount(
                    bill.RequiredWork,
                    bill.CompletedWork,
                    bill.RecipeName,
                    delta =>
                    {
                        ProductionWorkExecutionResult work =
                            productionBills.ExecuteWork(
                                context.Actor,
                                context.Target,
                                bill.BillId,
                                delta);
                        applied &= work.Succeeded;
                        completed |= work.CycleCompleted;
                        return work.Succeeded;
                    });
                result.CompletedSuccessfully = applied && completed;
                result.CompletionEffectsAlreadyApplied = completed;
                yield break;
            }
        }

        if (context.WorkTypeId == BuiltInWorkTypeIds.Cook
            && !processFluids.TryConsumeCycle(
                context.Target,
                context.WorkTypeId,
                out _))
        {
            result.CompletedSuccessfully = false;
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
