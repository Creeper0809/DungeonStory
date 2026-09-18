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
    private readonly ICharacterSpeciesRechargeService golemRecharge;
    private readonly ICropPlotRuntime cropPlots;
    private readonly IReadOnlyDictionary<WorkTypeId, Func<BuildableObject, float>> workAmounts;

    public SurvivalWorkExecutionHandler(
        ISurvivalFoodQuery survivalRuntime,
        IProductionBillWorkExecution productionBills,
        IProcessFluidUseRuntime processFluids,
        ICharacterSpeciesRechargeService golemRecharge,
        ICropPlotRuntime cropPlots)
    {
        this.survivalRuntime = survivalRuntime
            ?? throw new ArgumentNullException(nameof(survivalRuntime));
        this.productionBills = productionBills
            ?? throw new ArgumentNullException(nameof(productionBills));
        this.processFluids = processFluids
            ?? throw new ArgumentNullException(nameof(processFluids));
        this.golemRecharge = golemRecharge
            ?? throw new ArgumentNullException(nameof(golemRecharge));
        this.cropPlots = cropPlots
            ?? throw new ArgumentNullException(nameof(cropPlots));
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
        if (workTypeId == BuiltInWorkTypeIds.Refuel
            && target?.BuildingData?.GetAbility<BuildingGolemRechargeAbility>() != null)
            return golemRecharge.IsRechargeAvailable(actor, target, out reason);
        if (workTypeId == BuiltInWorkTypeIds.Cook)
        {
            ProductionWorkAvailabilityResult availability =
                productionBills.CheckWorkAvailability(target, workTypeId);
            if (availability.Available)
            {
                if (processFluids.EnsureCycleSupply(
                        target,
                        workTypeId,
                        out DomainFailure supplyFailure))
                {
                    return true;
                }

                reason = supplyFailure.IsFailure
                    ? supplyFailure.Code.ToString()
                    : FailureCode.ProductionUtilitiesUnavailable.ToString();
                return false;
            }
            reason = availability.Failure.IsFailure
                ? availability.Failure.Code.ToString()
                : string.Empty;
            if (ProductionWorkstationExecutionModeRules
                .BlocksManualProductionFallback(availability.Failure))
            {
                return false;
            }
        }
        if (workTypeId == BuiltInWorkTypeIds.Treat
            && cropPlots.TryGetWork(
                target,
                workTypeId,
                out CropPlotWorkSnapshot cropTreatment))
        {
            reason = cropTreatment.UnavailableReason;
            return cropTreatment.Available;
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
        if (workTypeId == BuiltInWorkTypeIds.Refuel
            && target?.BuildingData?.GetAbility<BuildingGolemRechargeAbility>() != null)
            return golemRecharge.GetRechargeUrgency(actor, target);
        if (workTypeId == BuiltInWorkTypeIds.Treat
            && cropPlots.TryGetWork(
                target,
                workTypeId,
                out CropPlotWorkSnapshot cropTreatment))
            return cropTreatment.Available ? 42f : 0f;
        return workTypeId.IsValid
            ? survivalRuntime.GetSurvivalWorkUrgency(target, workTypeId)
            : 0f;
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        if (context.WorkTypeId == BuiltInWorkTypeIds.Treat
            && cropPlots.TryGetWork(
                context.Target,
                context.WorkTypeId,
                out CropPlotWorkSnapshot cropTreatment)
            && cropTreatment.Available)
        {
            bool applied = true;
            bool completed = false;
            yield return context.ExecutePersistentWorkAmount(
                cropTreatment.RequiredWork,
                cropTreatment.CompletedWork,
                cropTreatment.DisplayName,
                delta =>
                {
                    applied &= cropPlots.ApplyWork(
                        context.Target,
                        context.WorkTypeId,
                        delta,
                        context.Actor,
                        out bool cycleCompleted);
                    completed |= cycleCompleted;
                    return applied;
                });
            result.CompletedSuccessfully = applied && completed;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }

        if (context.WorkTypeId == BuiltInWorkTypeIds.Refuel
            && context.Target?.BuildingData?
                .GetAbility<BuildingGolemRechargeAbility>()
                is BuildingGolemRechargeAbility rechargeAbility)
        {
            if (!golemRecharge.TryBeginRecharge(
                    context.Actor,
                    context.Target,
                    out float completedWork,
                    out _))
            {
                result.CompletedSuccessfully = false;
                yield break;
            }
            bool applied = true;
            bool completed = false;
            yield return context.ExecutePersistentWorkAmount(
                rechargeAbility.requiredWork,
                completedWork,
                "골렘 충전",
                delta =>
                {
                    applied &= golemRecharge.TryApplyRechargeWork(
                        context.Actor,
                        context.Target,
                        delta,
                        out bool cycleCompleted,
                        out _);
                    completed |= cycleCompleted;
                    return applied;
                });
            result.CompletedSuccessfully = applied && completed;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }
        if (context.WorkTypeId == BuiltInWorkTypeIds.Cook)
        {
            ProductionWorkAvailabilityResult availability =
                productionBills.CheckWorkAvailability(
                    context.Target,
                    context.WorkTypeId);
            if (availability.Available)
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
                    DomainFailure executionFailure = DomainFailure.None;
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
                            if (!work.Succeeded && work.Failure.IsFailure)
                            {
                                executionFailure = work.Failure;
                            }
                            return work.Succeeded;
                        });
                    result.CompletedSuccessfully = applied && completed;
                    result.CompletionEffectsAlreadyApplied = completed;
                    result.Failure = executionFailure;
                    if (executionFailure.IsFailure)
                    {
                        ObserveProductionFailureSource(result);
                    }
                    yield break;
                }

                // A selected production bill owns this action. Falling through
                // after BeginWork rejects could consume legacy process fluids or
                // create byproducts without committing the production WIP.
                result.CompletedSuccessfully = false;
                result.Failure = begin.Failure.IsFailure
                    ? begin.Failure
                    : begin.Bill?.BlockedFailure ?? DomainFailure.None;
                ObserveProductionFailureSource(result);
                yield break;
            }
            if (ProductionFacilityDefinitionIdentity.IsProductionWorkstation(
                    context.Target)
                || ProductionWorkstationExecutionModeRules
                    .BlocksManualProductionFallback(availability.Failure))
            {
                result.CompletedSuccessfully = false;
                result.Failure = availability.Failure.IsFailure
                    ? availability.Failure
                    : availability.Bill?.BlockedFailure ?? DomainFailure.None;
                ObserveProductionFailureSource(result);
                yield break;
            }
        }

        if (context.WorkTypeId == BuiltInWorkTypeIds.Cook
            && !processFluids.TryConsumeCycle(
                context.Target,
                context.WorkTypeId,
                out DomainFailure fluidFailure))
        {
            result.CompletedSuccessfully = false;
            result.Failure = fluidFailure;
            result.FailureAxis = CharacterOperationBlockAxis.Water;
            yield break;
        }

        if (context.WorkTypeId == BuiltInWorkTypeIds.Treat)
        {
            if (survivalRuntime is not ISurvivalTreatmentCompletionCommand treatment)
            {
                throw new InvalidOperationException(
                    "The survival Treat handler requires the typed treatment completion command.");
            }
            if (!treatment.TryEnsureTreatmentSupply(
                    context.Actor?.BuildingVisitor,
                    context.Target,
                    context.RunId,
                    out bool completionOnly,
                    out DomainFailure supplyFailure))
            {
                result.CompletedSuccessfully = false;
                result.Failure = supplyFailure;
                yield break;
            }
            if (completionOnly)
            {
                result.CompletedSuccessfully = treatment.TryApplyTreatmentWork(
                    context.Actor?.BuildingVisitor,
                    context.Target,
                    context.RunId,
                    out _,
                    out DomainFailure completionFailure);
                result.Failure = completionFailure;
                result.CompletionEffectsAlreadyApplied =
                    result.CompletedSuccessfully;
                yield break;
            }
        }

        if (context.WorkTypeId == BuiltInWorkTypeIds.Refuel)
        {
            if (survivalRuntime is not ISurvivalRefuelCompletionCommand refuel)
            {
                throw new InvalidOperationException(
                    "The survival Refuel handler requires the typed refuel completion command.");
            }
            if (!refuel.TryEnsureRefuelSupply(
                    context.Actor?.BuildingVisitor,
                    context.Target,
                    out bool completionOnly,
                    out DomainFailure supplyFailure))
            {
                result.CompletedSuccessfully = false;
                result.Failure = supplyFailure;
                yield break;
            }
            if (completionOnly)
            {
                result.CompletedSuccessfully = refuel.TryApplyRefuelWork(
                    context.Actor?.BuildingVisitor,
                    context.Target,
                    out _,
                    out DomainFailure completionFailure);
                result.Failure = completionFailure;
                result.CompletionEffectsAlreadyApplied =
                    result.CompletedSuccessfully;
                yield break;
            }
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
        if (result.CompletedSuccessfully
            && context.WorkTypeId == BuiltInWorkTypeIds.Treat)
        {
            if (survivalRuntime is not ISurvivalTreatmentCompletionCommand treatment)
            {
                throw new InvalidOperationException(
                    "The survival Treat handler requires the typed treatment completion command.");
            }
            result.CompletedSuccessfully = treatment.TryApplyTreatmentWork(
                context.Actor?.BuildingVisitor,
                context.Target,
                context.RunId,
                out _,
                out _);
            result.CompletionEffectsAlreadyApplied =
                result.CompletedSuccessfully;
        }
        else if (result.CompletedSuccessfully
            && context.WorkTypeId == BuiltInWorkTypeIds.Refuel)
        {
            if (survivalRuntime is not ISurvivalRefuelCompletionCommand refuel)
            {
                throw new InvalidOperationException(
                    "The survival Refuel handler requires the typed refuel completion command.");
            }
            result.CompletedSuccessfully = refuel.TryApplyRefuelWork(
                context.Actor?.BuildingVisitor,
                context.Target,
                out _,
                out DomainFailure completionFailure);
            result.Failure = completionFailure;
            result.CompletionEffectsAlreadyApplied =
                result.CompletedSuccessfully;
        }
    }

    private void ObserveProductionFailureSource(WorkExecutionResult result)
    {
        if (productionBills is IProductionBillQuery query)
        {
            result.ObserveFailureSource(() => query.Version);
        }
    }
}
