using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CraftWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Craft };
    private readonly IProductionBillRuntime productionBills;
    private readonly IFacilityEvolutionRuntime facilityEvolution;
    private readonly IEquipmentEvolutionRuntime equipmentEvolution;

    public CraftWorkExecutionHandler(
        IProductionBillRuntime productionBills = null,
        IFacilityEvolutionRuntime facilityEvolution = null,
        IEquipmentEvolutionRuntime equipmentEvolution = null)
    {
        this.productionBills = productionBills;
        this.facilityEvolution = facilityEvolution;
        this.equipmentEvolution = equipmentEvolution;
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

        if (facilityEvolution != null
            && facilityEvolution.TryGetPendingRelocation(
                target,
                out _))
        {
            return true;
        }

        if (facilityEvolution != null
            && facilityEvolution.TryGetPendingWork(
                target,
                out _,
                out _))
        {
            return true;
        }

        if (equipmentEvolution != null
            && equipmentEvolution.TryGetActiveReforge(target, out _))
        {
            return true;
        }

        if (equipmentEvolution != null
            && equipmentEvolution.TryGetActiveReattunement(target, out _))
        {
            return true;
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
        if (facilityEvolution != null
            && facilityEvolution.TryGetPendingRelocation(
                context.Target,
                out FacilityRelocationOrder relocation))
        {
            bool progressApplied = true;
            bool completed = false;
            yield return context.ExecutePersistentWorkAmount(
                relocation.ActiveRequiredWork,
                relocation.ActiveCompletedWork,
                relocation.phase == FacilityRelocationPhase.Dismantling
                    ? "시설 해체"
                    : "시설 재설치",
                delta =>
                {
                    bool succeeded = facilityEvolution.ApplyRelocationWork(
                        context.Target,
                        delta,
                        out _,
                        out bool cycleCompleted,
                        out _);
                    progressApplied &= succeeded;
                    completed |= cycleCompleted;
                    return succeeded;
                });
            result.CompletedSuccessfully = progressApplied;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }

        if (facilityEvolution != null
            && facilityEvolution.TryGetPendingWork(
                context.Target,
                out FacilityModificationOrder modification,
                out FacilityRecalibrationOrder recalibration))
        {
            float facilityRequiredWork = modification?.requiredWork
                ?? recalibration?.requiredWork
                ?? 0f;
            float facilityCompletedWork = modification?.completedWork
                ?? recalibration?.completedWork
                ?? 0f;
            bool progressApplied = true;
            bool completed = false;
            yield return context.ExecutePersistentWorkAmount(
                facilityRequiredWork,
                facilityCompletedWork,
                modification != null ? "시설 개조" : "시설 재조율",
                delta =>
                {
                    bool succeeded = facilityEvolution.ApplyPendingWork(
                        context.Target,
                        delta,
                        out _,
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

        if (equipmentEvolution != null
            && equipmentEvolution.TryGetActiveReforge(
                context.Target,
                out EvolutionReforgeOrder reforge))
        {
            bool progressApplied = true;
            bool completed = false;
            yield return context.ExecutePersistentWorkAmount(
                reforge.requiredWork,
                reforge.completedWork,
                "장비 재단조",
                delta =>
                {
                    bool succeeded = equipmentEvolution.ApplyReforgeWork(
                        reforge.orderId,
                        delta,
                        out EvolutionNode node,
                        out _);
                    progressApplied &= succeeded;
                    completed |= node != null;
                    return succeeded;
                });
            result.CompletedSuccessfully = progressApplied && completed;
            result.CompletionEffectsAlreadyApplied = completed;
            yield break;
        }

        if (equipmentEvolution != null
            && equipmentEvolution.TryGetActiveReattunement(
                context.Target,
                out EquipmentReattunementOrder reattunement))
        {
            bool progressApplied = true;
            bool completed = false;
            yield return context.ExecutePersistentWorkAmount(
                reattunement.requiredWork,
                reattunement.completedWork,
                "장비 재귀속",
                delta =>
                {
                    bool succeeded =
                        equipmentEvolution.ApplyReattunementWork(
                            reattunement.orderId,
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
