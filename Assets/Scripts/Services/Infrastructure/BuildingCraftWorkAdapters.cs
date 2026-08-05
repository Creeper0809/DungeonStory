using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BuildingCraftWorkRuntimeAdapter :
    IBuildingCraftWorkRuntimePort
{
    private readonly IProductionBillWorkExecution production;
    private readonly IFacilityEvolutionRuntime facilityEvolution;
    private readonly IEquipmentEvolutionRuntime equipmentEvolution;

    public BuildingCraftWorkRuntimeAdapter(
        IProductionBillWorkExecution production,
        IFacilityEvolutionRuntime facilityEvolution,
        IEquipmentEvolutionRuntime equipmentEvolution)
    {
        this.production = production
            ?? throw new ArgumentNullException(nameof(production));
        this.facilityEvolution = facilityEvolution;
        this.equipmentEvolution = equipmentEvolution;
    }

    public CraftWorkerHandle CaptureWorker(object runtimeWorker)
    {
        if (!CharacterBuildingVisitorAdapter.TryResolve(
                runtimeWorker,
                out IBuildingVisitorPort worker))
        {
            throw new InvalidOperationException(
                "Craft work requires a building visitor worker.");
        }
        return new CraftWorkerHandle(
            runtimeWorker,
            worker.BuildingCharacterId.Value);
    }

    public CraftFacilityHandle CaptureFacility(object runtimeFacility)
    {
        BuildableObject facility = runtimeFacility as BuildableObject
            ?? throw new InvalidOperationException(
                "Craft work requires a BuildableObject facility.");
        return new CraftFacilityHandle(
            facility,
            facility.RequirePersistentInstanceId().Value);
    }

    public bool TryGetFacilityRelocation(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan)
    {
        if (facilityEvolution != null
            && facilityEvolution.TryGetPendingRelocation(
                RequireFacility(facility),
                out FacilityRelocationOrder relocation))
        {
            plan = new CraftWorkExecutionPlan(
                CraftWorkOperationKind.FacilityRelocation,
                string.Empty,
                relocation.ActiveRequiredWork,
                relocation.ActiveCompletedWork,
                relocation.phase == FacilityRelocationPhase.Dismantling
                    ? "시설 해체"
                    : "시설 재설치");
            return true;
        }

        plan = default;
        return false;
    }

    public bool TryGetFacilityEvolution(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan)
    {
        if (facilityEvolution != null
            && facilityEvolution.TryGetPendingWork(
                RequireFacility(facility),
                out FacilityModificationOrder modification,
                out FacilityRecalibrationOrder recalibration))
        {
            plan = new CraftWorkExecutionPlan(
                CraftWorkOperationKind.FacilityEvolution,
                string.Empty,
                modification?.requiredWork ?? recalibration?.requiredWork ?? 0f,
                modification?.completedWork ?? recalibration?.completedWork ?? 0f,
                modification != null ? "시설 개조" : "시설 재조정");
            return true;
        }

        plan = default;
        return false;
    }

    public bool TryGetEquipmentReforge(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan)
    {
        if (equipmentEvolution != null
            && equipmentEvolution.TryGetActiveReforge(
                RequireFacility(facility),
                out EvolutionReforgeOrder reforge))
        {
            plan = new CraftWorkExecutionPlan(
                CraftWorkOperationKind.EquipmentReforge,
                reforge.orderId,
                reforge.requiredWork,
                reforge.completedWork,
                "장비 재단조");
            return true;
        }

        plan = default;
        return false;
    }

    public bool TryGetEquipmentReattunement(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan)
    {
        if (equipmentEvolution != null
            && equipmentEvolution.TryGetActiveReattunement(
                RequireFacility(facility),
                out EquipmentReattunementOrder reattunement))
        {
            plan = new CraftWorkExecutionPlan(
                CraftWorkOperationKind.EquipmentReattunement,
                reattunement.orderId,
                reattunement.requiredWork,
                reattunement.completedWork,
                "장비 재조율");
            return true;
        }

        plan = default;
        return false;
    }

    public CraftWorkAvailability CheckProductionAvailability(
        CraftFacilityHandle facility)
    {
        ProductionWorkAvailabilityResult availability =
            production.CheckWorkAvailability(
                RequireFacility(facility),
                BuiltInWorkTypeIds.Craft);
        return new CraftWorkAvailability(
            availability.Available,
            availability.Failure.IsFailure
                ? availability.Failure.Code.ToString()
                : string.Empty);
    }

    public bool TryBeginProduction(
        CraftWorkerHandle worker,
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan)
    {
        ProductionWorkBeginResult begin = production.BeginWork(
            RequireCharacter(worker),
            RequireFacility(facility),
            BuiltInWorkTypeIds.Craft);
        if (!begin.Succeeded)
        {
            plan = default;
            return false;
        }

        ProductionBillSnapshot bill = begin.Bill;
        plan = new CraftWorkExecutionPlan(
            CraftWorkOperationKind.ProductionBill,
            bill.BillId.Value,
            bill.RequiredWork,
            bill.CompletedWork,
            bill.RecipeName);
        return true;
    }

    public bool HasPendingLegacyEquipmentCraft(CraftFacilityHandle facility)
    {
        return RequireFacility(facility).HasPendingEquipmentCraftWork();
    }

    public CraftWorkExecutionPlan CreateLegacyEquipmentCraftPlan(
        CraftFacilityHandle facility)
    {
        BuildableObject building = RequireFacility(facility);
        float requiredWork = Mathf.Max(
            0.1f,
            building.BuildingData
                ?.GetAbility<BuildingEquipmentCraftingAbility>()
                ?.workUnitsPerCycle ?? 1f);
        return new CraftWorkExecutionPlan(
            CraftWorkOperationKind.LegacyEquipmentCraft,
            string.Empty,
            requiredWork,
            0f,
            "제작");
    }

    public CraftWorkProgressResult ApplyProgress(
        CraftWorkerHandle worker,
        CraftFacilityHandle facility,
        CraftWorkExecutionPlan plan,
        float amount)
    {
        BuildableObject building = RequireFacility(facility);
        switch (plan.Kind)
        {
            case CraftWorkOperationKind.FacilityRelocation:
            {
                bool completed = false;
                bool succeeded = facilityEvolution != null
                    && facilityEvolution.ApplyRelocationWork(
                        building,
                        amount,
                        out _,
                        out completed,
                        out _);
                return new CraftWorkProgressResult(succeeded, succeeded && completed);
            }
            case CraftWorkOperationKind.FacilityEvolution:
            {
                bool completed = false;
                bool succeeded = facilityEvolution != null
                    && facilityEvolution.ApplyPendingWork(
                        building,
                        amount,
                        out _,
                        out completed,
                        out _);
                return new CraftWorkProgressResult(succeeded, succeeded && completed);
            }
            case CraftWorkOperationKind.EquipmentReforge:
            {
                EvolutionNode node = null;
                bool succeeded = equipmentEvolution != null
                    && equipmentEvolution.ApplyReforgeWork(
                        plan.OperationId,
                        amount,
                        out node,
                        out _);
                return new CraftWorkProgressResult(succeeded, succeeded && node != null);
            }
            case CraftWorkOperationKind.EquipmentReattunement:
            {
                bool completed = false;
                bool succeeded = equipmentEvolution != null
                    && equipmentEvolution.ApplyReattunementWork(
                        plan.OperationId,
                        amount,
                        out completed,
                        out _);
                return new CraftWorkProgressResult(succeeded, succeeded && completed);
            }
            case CraftWorkOperationKind.ProductionBill:
            {
                ProductionWorkExecutionResult work = production.ExecuteWork(
                    RequireCharacter(worker),
                    building,
                    (ProductionBillId)plan.OperationId,
                    amount);
                return new CraftWorkProgressResult(
                    work.Succeeded,
                    work.Succeeded && work.CycleCompleted);
            }
            default:
                throw new InvalidOperationException(
                    $"Unsupported persistent craft operation '{plan.Kind}'.");
        }
    }

    public int CompleteLegacyEquipmentCraft(
        CraftWorkerHandle worker,
        CraftFacilityHandle facility)
    {
        return ModularFacilityRuntimeEffects.ApplyWorkCompleted(
            RequireVisitor(worker),
            RequireFacility(facility),
            BuiltInWorkTypeIds.Craft);
    }

    private static IBuildingVisitorPort RequireVisitor(CraftWorkerHandle worker)
    {
        if (CharacterBuildingVisitorAdapter.TryResolve(
                worker?.RuntimeObject,
                out IBuildingVisitorPort visitor))
        {
            return visitor;
        }

        throw new InvalidOperationException(
            "Craft work requires a building visitor worker handle.");
    }

    private static CharacterActor RequireCharacter(CraftWorkerHandle worker)
    {
        return worker?.RuntimeObject as CharacterActor
            ?? throw new InvalidOperationException(
                "Production craft work requires a CharacterActor worker handle.");
    }

    private static BuildableObject RequireFacility(CraftFacilityHandle facility)
    {
        return facility?.RuntimeObject as BuildableObject
            ?? throw new InvalidOperationException(
                "Craft work requires a BuildableObject facility handle.");
    }
}

public sealed class CraftWorkExecutionAdapter :
    IWorkExecutionHandler,
    IWorkCandidateProvider
{
    private readonly CraftWorkExecutionHandler handler;
    private readonly IBuildingCraftWorkRuntimePort runtime;

    public CraftWorkExecutionAdapter(
        CraftWorkExecutionHandler handler,
        IBuildingCraftWorkRuntimePort runtime)
    {
        this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => handler.WorkTypeIds;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        if (target == null)
        {
            reason = string.Empty;
            return false;
        }

        return handler.IsAvailable(
            workTypeId,
            runtime.CaptureFacility(target),
            out reason);
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (result == null) throw new ArgumentNullException(nameof(result));

        CraftWorkerHandle worker = runtime.CaptureWorker(context.Actor);
        CraftFacilityHandle facility = runtime.CaptureFacility(context.Target);
        if (!handler.TryCreatePlan(worker, facility, out CraftWorkExecutionPlan plan))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        if (!plan.IsPersistent)
        {
            yield return context.ExecuteWorkAmount(plan.RequiredWork, plan.Label);
            if (!context.CanContinue)
            {
                result.CompletedSuccessfully = false;
                yield break;
            }

            int applied = handler.CompleteLegacyEquipmentCraft(worker, facility);
            result.CompletedSuccessfully = applied > 0;
            result.CompletionEffectsAlreadyApplied = true;
            yield break;
        }

        bool progressApplied = true;
        bool completed = false;
        yield return context.ExecutePersistentWorkAmount(
            plan.RequiredWork,
            plan.CompletedWork,
            plan.Label,
            delta =>
            {
                CraftWorkProgressResult progress =
                    handler.ApplyProgress(worker, facility, plan, delta);
                progressApplied &= progress.Succeeded;
                completed |= progress.CycleCompleted;
                return progress.Succeeded;
            });
        result.CompletedSuccessfully = progressApplied
            && (!plan.RequiresCycleCompletionForSuccess || completed);
        result.CompletionEffectsAlreadyApplied = completed;
    }
}
