using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CraftWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Craft };
    private readonly IProductionBillRuntime productionBills;

    public CraftWorkExecutionHandler(
        IProductionBillRuntime productionBills = null)
    {
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
        if (target == null)
        {
            return false;
        }

        if (productionBills != null
            && productionBills.HasWorkAvailable(
                target,
                BuiltInWorkTypeIds.Craft,
                out reason))
        {
            return true;
        }

        reason = string.Empty;
        return target.HasPendingEquipmentCraftWork();
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        if (productionBills != null
            && productionBills.TryBeginWork(
                context.Actor,
                context.Target,
                BuiltInWorkTypeIds.Craft,
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
                ?.workUnitsPerCycle ?? 1f);
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
