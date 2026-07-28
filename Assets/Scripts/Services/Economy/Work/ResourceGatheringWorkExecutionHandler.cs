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
    private readonly IProductionBillRuntime productionBills;

    public ResourceGatheringWorkExecutionHandler(
        IWorldResourceRuntime worldResources,
        ICropPlotRuntime cropPlots,
        IProductionBillRuntime productionBills = null)
    {
        this.worldResources = worldResources;
        this.cropPlots = cropPlots;
        this.productionBills = productionBills;
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        if (target is WorldResourceNode resourceNode
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
            reason = cropWork.UnavailableReason;
            return cropWork.Available;
        }

        if (workTypeId == BuiltInWorkTypeIds.Quarry
            && productionBills != null
            && productionBills.HasWorkAvailable(target, workTypeId, out reason))
        {
            return true;
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
        if (target is WorldResourceNode resourceNode
            && worldResources != null
            && worldResources.TryGetWork(
                resourceNode,
                workTypeId,
                out WorldResourceWorkSnapshot snapshot)
            && snapshot.Available)
        {
            return 18f + snapshot.ResourceRatio * 22f;
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
            && productionBills?.HasWorkAvailable(target, workTypeId, out _) == true
                ? 32f
                : 0f;
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (context.Target is WorldResourceNode resourceNode
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
                        out bool cycleCompleted);
                    progressApplied &= succeeded;
                    completed |= cycleCompleted;
                    return succeeded;
                });
            result.CompletedSuccessfully = progressApplied && completed;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }

        if (context.WorkTypeId == BuiltInWorkTypeIds.Quarry
            && productionBills != null
            && productionBills.TryBeginWork(
                context.Actor,
                context.Target,
                context.WorkTypeId,
                out ProductionBillSnapshot bill,
                out _))
        {
            bool progressApplied = true;
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
                    progressApplied &= succeeded;
                    completed |= cycleCompleted;
                    return succeeded;
                });
            result.CompletedSuccessfully = progressApplied && completed;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }

        result.CompletedSuccessfully = false;
    }
}
