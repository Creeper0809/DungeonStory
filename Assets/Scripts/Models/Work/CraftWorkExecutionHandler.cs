using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

public sealed class CraftWorkerHandle
{
    public CraftWorkerHandle(object runtimeObject, string persistentId)
    {
        RuntimeObject = runtimeObject
            ?? throw new ArgumentNullException(nameof(runtimeObject));
        PersistentId = persistentId?.Trim() ?? string.Empty;
    }

    public object RuntimeObject { get; }
    public string PersistentId { get; }
}

public sealed class CraftFacilityHandle
{
    public CraftFacilityHandle(object runtimeObject, string persistentId)
    {
        RuntimeObject = runtimeObject
            ?? throw new ArgumentNullException(nameof(runtimeObject));
        PersistentId = persistentId?.Trim() ?? string.Empty;
    }

    public object RuntimeObject { get; }
    public string PersistentId { get; }
}

public enum CraftWorkOperationKind
{
    None = 0,
    FacilityRelocation = 1,
    FacilityEvolution = 2,
    EquipmentReforge = 3,
    EquipmentReattunement = 4,
    ProductionBill = 5,
    LegacyEquipmentCraft = 6,
    RegisteredCapability = 7
}

public readonly struct CraftWorkAvailability
{
    public CraftWorkAvailability(
        bool available,
        string failureCode,
        bool blocksLegacyFallback = false)
    {
        Available = available;
        FailureCode = failureCode?.Trim() ?? string.Empty;
        BlocksLegacyFallback = blocksLegacyFallback;
    }

    public bool Available { get; }
    public string FailureCode { get; }
    public bool BlocksLegacyFallback { get; }
}

public readonly struct CraftWorkExecutionPlan
{
    public CraftWorkExecutionPlan(
        CraftWorkOperationKind kind,
        string operationId,
        float requiredWork,
        float completedWork,
        string label,
        string contributorId = "")
    {
        if (kind == CraftWorkOperationKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        string exactContributorId = contributorId ?? string.Empty;
        bool registered = kind == CraftWorkOperationKind.RegisteredCapability;
        if (registered != exactContributorId.Length > 0
            || exactContributorId.Length > 0
                && (!string.Equals(
                        exactContributorId,
                        exactContributorId.Trim(),
                        StringComparison.Ordinal)
                    || exactContributorId.Any(char.IsWhiteSpace)))
        {
            throw new ArgumentException(
                "Registered craft operations require one canonical contributor ID.",
                nameof(contributorId));
        }

        Kind = kind;
        OperationId = operationId?.Trim() ?? string.Empty;
        RequiredWork = requiredWork;
        CompletedWork = completedWork;
        Label = label ?? string.Empty;
        ContributorId = exactContributorId;
    }

    public CraftWorkOperationKind Kind { get; }
    public string OperationId { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public string Label { get; }
    public string ContributorId { get; }
    public bool IsPersistent => Kind != CraftWorkOperationKind.LegacyEquipmentCraft;
    public bool RequiresCycleCompletionForSuccess =>
        Kind != CraftWorkOperationKind.FacilityRelocation;
}

public readonly struct CraftWorkProgressResult
{
    public CraftWorkProgressResult(bool succeeded, bool cycleCompleted)
    {
        Succeeded = succeeded;
        CycleCompleted = cycleCompleted;
    }

    public bool Succeeded { get; }
    public bool CycleCompleted { get; }
}

public interface ICraftPersistentOperationContributor
{
    string ContributorId { get; }

    bool TryCapturePlan(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan);

    CraftWorkProgressResult ApplyProgress(
        CraftFacilityHandle facility,
        CraftWorkExecutionPlan plan,
        float amount);
}

public interface IBuildingCraftWorkRuntimePort
{
    CraftWorkerHandle CaptureWorker(object runtimeWorker);
    CraftFacilityHandle CaptureFacility(object runtimeFacility);

    bool TryGetFacilityRelocation(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan);
    bool TryGetFacilityEvolution(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan);
    bool TryGetEquipmentReforge(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan);
    bool TryGetEquipmentReattunement(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan);
    bool TryGetRegisteredPersistentOperation(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan);

    CraftWorkAvailability CheckProductionAvailability(
        CraftFacilityHandle facility);
    bool TryBeginProduction(
        CraftWorkerHandle worker,
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan);

    bool HasPendingLegacyEquipmentCraft(CraftFacilityHandle facility);
    CraftWorkExecutionPlan CreateLegacyEquipmentCraftPlan(
        CraftFacilityHandle facility);

    CraftWorkProgressResult ApplyProgress(
        CraftWorkerHandle worker,
        CraftFacilityHandle facility,
        CraftWorkExecutionPlan plan,
        float amount);
    CraftWorkProgressResult ApplyRegisteredPersistentOperation(
        CraftFacilityHandle facility,
        CraftWorkExecutionPlan plan,
        float amount);
    int CompleteLegacyEquipmentCraft(
        CraftWorkerHandle worker,
        CraftFacilityHandle facility);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CraftWorkExecutionHandler
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Craft };
    private readonly IBuildingCraftWorkRuntimePort runtime;

    public CraftWorkExecutionHandler(IBuildingCraftWorkRuntimePort runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CraftFacilityHandle facility,
        out string reason)
    {
        reason = string.Empty;
        if (facility == null || workTypeId != BuiltInWorkTypeIds.Craft)
        {
            return false;
        }

        if (runtime.TryGetFacilityRelocation(facility, out _)
            || runtime.TryGetFacilityEvolution(facility, out _)
            || runtime.TryGetEquipmentReforge(facility, out _)
            || runtime.TryGetEquipmentReattunement(facility, out _)
            || runtime.TryGetRegisteredPersistentOperation(facility, out _))
        {
            return true;
        }

        CraftWorkAvailability availability =
            runtime.CheckProductionAvailability(facility);
        if (availability.Available)
        {
            return true;
        }

        reason = availability.FailureCode;
        if (availability.BlocksLegacyFallback)
        {
            return false;
        }
        return runtime.HasPendingLegacyEquipmentCraft(facility);
    }

    public bool TryCreatePlan(
        CraftWorkerHandle worker,
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan)
    {
        if (facility == null)
        {
            plan = default;
            return false;
        }

        if (runtime.TryGetFacilityRelocation(facility, out plan)
            || runtime.TryGetFacilityEvolution(facility, out plan)
            || runtime.TryGetEquipmentReforge(facility, out plan)
            || runtime.TryGetEquipmentReattunement(facility, out plan)
            || runtime.TryGetRegisteredPersistentOperation(facility, out plan))
        {
            return true;
        }

        CraftWorkAvailability availability =
            runtime.CheckProductionAvailability(facility);
        if (availability.Available
            && runtime.TryBeginProduction(worker, facility, out plan))
        {
            return true;
        }
        if (availability.BlocksLegacyFallback
            || runtime.CheckProductionAvailability(facility)
                .BlocksLegacyFallback)
        {
            plan = default;
            return false;
        }

        if (!runtime.HasPendingLegacyEquipmentCraft(facility))
        {
            plan = default;
            return false;
        }

        plan = runtime.CreateLegacyEquipmentCraftPlan(facility);
        return true;
    }

    public CraftWorkProgressResult ApplyProgress(
        CraftWorkerHandle worker,
        CraftFacilityHandle facility,
        CraftWorkExecutionPlan plan,
        float amount)
    {
        if (!plan.IsPersistent)
        {
            throw new InvalidOperationException(
                "Legacy equipment crafting does not use persistent progress.");
        }

        return plan.Kind == CraftWorkOperationKind.RegisteredCapability
            ? runtime.ApplyRegisteredPersistentOperation(facility, plan, amount)
            : runtime.ApplyProgress(worker, facility, plan, amount);
    }

    public int CompleteLegacyEquipmentCraft(
        CraftWorkerHandle worker,
        CraftFacilityHandle facility)
    {
        return runtime.CompleteLegacyEquipmentCraft(worker, facility);
    }
}
