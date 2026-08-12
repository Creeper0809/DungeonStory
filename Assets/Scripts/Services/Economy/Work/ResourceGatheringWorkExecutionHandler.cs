using System;
using System.Collections;
using System.Collections.Generic;

public sealed class ResourceGatheringWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids =
    {
        BuiltInWorkTypeIds.Gather,
        BuiltInWorkTypeIds.Sow,
        BuiltInWorkTypeIds.Harvest,
        BuiltInWorkTypeIds.Logging,
        BuiltInWorkTypeIds.Quarry
    };

    private readonly IWorldResourceRuntime worldResources;
    private readonly ICropPlotRuntime cropPlots;
    private readonly IProductionBillWorkExecution productionBills;

    public ResourceGatheringWorkExecutionHandler(
        IWorldResourceRuntime worldResources,
        ICropPlotRuntime cropPlots,
        IProductionBillWorkExecution productionBills)
    {
        this.worldResources = worldResources;
        this.cropPlots = cropPlots;
        this.productionBills = productionBills
            ?? throw new ArgumentNullException(nameof(productionBills));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        if (TryGetResourceNode(target, out WorldResourceNode resourceNode)
            && worldResources != null
            && worldResources.TryGetWork(
                resourceNode,
                workTypeId,
                out WorldResourceWorkSnapshot resource))
        {
            reason = resource.UnavailableReason;
            return resource.Available;
        }

        if (cropPlots != null
            && cropPlots.TryGetWork(
                target,
                workTypeId,
                out CropPlotWorkSnapshot cropWork))
        {
            if (workTypeId == BuiltInWorkTypeIds.Harvest
                && !cropPlots.IsGoldenHarvestWorkerEligible(
                    target,
                    actor,
                    out reason))
                return false;
            if (workTypeId == BuiltInWorkTypeIds.Harvest
                && cropPlots.TryGetGoldenHarvestDelay(
                    target,
                    actor,
                    out float remainingSeconds))
            {
                reason = $"황금 수확 숙성 대기 {remainingSeconds:F1}초";
                return false;
            }
            reason = cropWork.UnavailableReason;
            return cropWork.Available;
        }

        if (workTypeId == BuiltInWorkTypeIds.Quarry)
        {
            ProductionWorkAvailabilityResult availability =
                productionBills.CheckWorkAvailability(target, workTypeId);
            if (availability.Available)
            {
                return true;
            }
            if (availability.Failure.IsFailure)
            {
                reason = availability.Failure.Code.ToString();
                return false;
            }
        }

        reason = workTypeId == BuiltInWorkTypeIds.Sow
            || workTypeId == BuiltInWorkTypeIds.Harvest
                ? "작업 가능한 경작지가 없음"
                : "작업할 외부 자원이 없음";
        return false;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        if (TryGetResourceNode(target, out WorldResourceNode resourceNode)
            && worldResources != null
            && worldResources.TryGetWork(
                resourceNode,
                workTypeId,
                out WorldResourceWorkSnapshot snapshot)
            && snapshot.Available)
        {
            return resourceNode.GetLegacyWorkUrgency(workTypeId);
        }

        if (cropPlots != null
            && cropPlots.TryGetWork(
                target,
                workTypeId,
                out CropPlotWorkSnapshot cropWork)
            && cropWork.Available)
        {
            return workTypeId == BuiltInWorkTypeIds.Harvest
                ? 46f
                : 34f;
        }

        return workTypeId == BuiltInWorkTypeIds.Quarry
            && productionBills.CheckWorkAvailability(target, workTypeId).Available
                ? 32f
                : 0f;
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (TryGetResourceNode(
                context.Target,
                out WorldResourceNode resourceNode)
            && worldResources != null
            && worldResources.TryGetWork(
                resourceNode,
                context.WorkTypeId,
                out WorldResourceWorkSnapshot snapshot)
            && snapshot.Available)
        {
            bool progressApplied = true;
            bool completed = false;
            yield return context.ExecutePersistentWorkAmount(
                snapshot.RequiredWork,
                snapshot.CompletedWork,
                snapshot.DisplayName,
                delta =>
                {
                    bool succeeded = worldResources.ApplyWork(
                        resourceNode,
                        context.WorkTypeId,
                        delta,
                        out bool cycleCompleted);
                    progressApplied &= succeeded;
                    completed |= cycleCompleted;
                    return succeeded;
                });
            result.CompletedSuccessfully = progressApplied && completed;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }

        if (cropPlots != null
            && cropPlots.TryGetWork(
                context.Target,
                context.WorkTypeId,
                out CropPlotWorkSnapshot cropWork)
            && cropWork.Available)
        {
            bool progressApplied = true;
            bool completed = false;
            yield return context.ExecutePersistentWorkAmount(
                cropWork.RequiredWork,
                cropWork.CompletedWork,
                cropWork.DisplayName,
                delta =>
                {
                    bool succeeded = cropPlots.ApplyWork(
                        context.Target,
                        context.WorkTypeId,
                        delta,
                        context.Actor,
                        out bool cycleCompleted);
                    progressApplied &= succeeded;
                    completed |= cycleCompleted;
                    return succeeded;
                });
            result.CompletedSuccessfully = progressApplied && completed;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }

        if (context.WorkTypeId == BuiltInWorkTypeIds.Quarry)
        {
            ProductionWorkBeginResult begin = productionBills.BeginWork(
                context.Actor,
                context.Target,
                context.WorkTypeId);
            if (!begin.Succeeded)
            {
                result.CompletedSuccessfully = false;
                yield break;
            }

            ProductionBillSnapshot bill = begin.Bill;
            bool progressApplied = true;
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
                    progressApplied &= work.Succeeded;
                    completed |= work.CycleCompleted;
                    return work.Succeeded;
                });
            result.CompletedSuccessfully = progressApplied && completed;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }

        result.CompletedSuccessfully = false;
    }

    private static bool TryGetResourceNode(
        BuildableObject target,
        out WorldResourceNode node)
    {
        node = (target as IWorldResourceNodeHost)?.ResourceNode;
        return node != null;
    }
}
