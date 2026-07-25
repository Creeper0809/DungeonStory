using System.Collections;
using System.Collections.Generic;

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
        return new FacilityAssignmentStatus(true, FacilityAssignmentFailureKind.None, string.Empty);
    }

    public static FacilityAssignmentStatus Rejected(
        FacilityAssignmentFailureKind failureKind,
        string reason)
    {
        return new FacilityAssignmentStatus(false, failureKind, reason);
    }
}

public interface IInteractable
{
    public IEnumerator Interact(CharacterActor actor);
}

public interface IGridMovementHandler
{
    public IEnumerator Traverse(CharacterActor actor, GridMoveStep step);
}

public interface IStockedFacility
{
    public int CurrentStock { get; }
    public bool HasAvailableStock { get; }
}

public interface IRetailFacility : IStockedFacility, IInteractable
{
    public bool HasServingWorker { get; }
    public bool HasWaitingCheckout { get; }
    public bool RequiresStaffedCheckout { get; }
    public int WaitingCheckoutCount { get; }
    public int MaxInternalStock { get; }
    public float CurrentPriceMultiplier { get; }
    public IReadOnlyList<RetailProductSnapshot> ProductSnapshots { get; }
    public IReadOnlyList<Stock> GetPurchasableStock();
    public float GetCheckoutCrimeChance(int cartItemCount);
}

public interface IRestockableFacility : IStockedFacility
{
    public int MaxStock { get; }
    public int MissingStock { get; }
    public bool NeedsRestock { get; }
    public int RestockFrom(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out string resultMessage);
    public bool TryFindRestockSource(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out IWarehouseFacility warehouse,
        out SaleItem saleItem,
        out int availableAmount,
        out string failureReason);
    public int ReceiveRestock(
        SaleItem saleItem,
        int amount,
        int requestedAmount,
        out string resultMessage);
    public bool HasRestockSupply(
        IEnumerable<IWarehouseFacility> warehouses,
        out string failureReason);
}

public interface IRetailStockStateOwner
{
    ShopStockStateSnapshot CreateStockSnapshot();
    void ApplyStockSnapshot(ShopStockStateSnapshot snapshot);
}

public interface IWarehouseFacility
{
    public WarehouseInventory Inventory { get; }
    public bool HasWarehouseInventory { get; }
}

public interface IWorkableFacility
{
    public FacilityAssignmentStatus GetWorkerAssignmentStatus(CharacterActor actor);
    public bool CanAssignWorker(CharacterActor actor, out string failureReason);
    public IEnumerator AllocateWorker(CharacterActor actor);
    public void DeallocateWorker(CharacterActor actor);
}
