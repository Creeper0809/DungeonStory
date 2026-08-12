using System.Collections;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum FacilityAssignmentFailureKind
{
    None,
    MissingWorker,
    Destroyed,
    UnsupportedWork,
    WorkNotNeeded,
    Damaged,
    Occupied,
    Reserved,
    Unknown
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct FacilityAssignmentStatus
{
    private FacilityAssignmentStatus(
        bool isAllowed,
        FacilityAssignmentFailureKind failureKind,
        string reason)
    {
        IsAllowed = isAllowed;
        FailureKind = failureKind;
        Reason = reason ?? string.Empty;
    }

    public bool IsAllowed { get; }
    public FacilityAssignmentFailureKind FailureKind { get; }
    public string Reason { get; }

    public static FacilityAssignmentStatus Allowed()
    {
        return new FacilityAssignmentStatus(
            true,
            FacilityAssignmentFailureKind.None,
            string.Empty);
    }

    public static FacilityAssignmentStatus Rejected(
        FacilityAssignmentFailureKind failureKind,
        string reason)
    {
        return new FacilityAssignmentStatus(false, failureKind, reason);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IInteractable
{
    IEnumerator Interact(IBuildingVisitorPort actor);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IGridMovementHandler
{
    IEnumerator Traverse(IBuildingVisitorPort actor, GridMoveStep step);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IStockedFacility
{
    int CurrentStock { get; }
    bool HasAvailableStock { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IRetailFacility : IStockedFacility, IInteractable
{
    bool HasServingWorker { get; }
    bool HasWaitingCheckout { get; }
    bool RequiresStaffedCheckout { get; }
    int WaitingCheckoutCount { get; }
    int MaxInternalStock { get; }
    float CurrentPriceMultiplier { get; }
    IReadOnlyList<RetailProductSnapshot> ProductSnapshots { get; }
    IReadOnlyList<Stock> GetPurchasableStock();
    float GetCheckoutCrimeChance(int cartItemCount);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IRetailStockStateOwner
{
    ShopStockStateSnapshot CreateStockSnapshot();
    void ApplyStockSnapshot(ShopStockStateSnapshot snapshot);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IWorkableFacility
{
    FacilityAssignmentStatus GetWorkerAssignmentStatus(IBuildingVisitorPort actor);
    bool CanAssignWorker(IBuildingVisitorPort actor, out string failureReason);
    IEnumerator AllocateWorker(IBuildingVisitorPort actor);
    void DeallocateWorker(IBuildingVisitorPort actor);
}

/// <summary>
/// Optional capacity-aware worker reservation boundary for facilities that
/// intentionally accept more than one simultaneous worker. Ordinary facilities
/// continue to use BuildingAssignment's single-worker reservation.
/// </summary>
public interface IParallelWorkerReservationFacility
{
    IBuildingCharacterPort PrimaryWorkerReservation { get; }
    bool TryReserveParallelWorker(
        IBuildingCharacterPort worker,
        out FacilityAssignmentStatus status,
        float seconds);
    void RefreshParallelWorkerReservation(
        IBuildingCharacterPort worker,
        float seconds);
    bool HasParallelWorkerReservationForOther(IBuildingCharacterPort worker);
    void ReleaseParallelWorkerReservation(IBuildingCharacterPort worker);
}
