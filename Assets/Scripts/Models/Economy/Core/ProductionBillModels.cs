using System;
using System.Collections.Generic;
using UnityEngine;

public enum ProductionBillStatus
{
    WaitingForMaterials = 0,
    Ready = 1,
    InProgress = 2,
    Suspended = 3,
    Completed = 4,
    Cancelled = 5
}

[Serializable]
public sealed class ProductionBillSaveData
{
    public string billId = string.Empty;
    public string recipeId = string.Empty;
    public int buildingId;
    public int gridX;
    public int gridY;
    public ProductionOrderMode mode;
    public int remainingCycles = 1;
    public int targetStock = 10;
    public int minimumReserve;
    public bool suspended;
    public bool materialsConsumed;
    public float completedWork;
    public string reservedWorkerId = string.Empty;
    public string materialDestinationId = string.Empty;
    public List<string> allowedMaterialIds = new List<string>();
    public List<string> allowedWorkerIds = new List<string>();
}

[Serializable]
public sealed class DungeonProductionBillSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int nextBillSequence = 1;
    public List<ProductionBillSaveData> bills = new List<ProductionBillSaveData>();
}

public sealed class ProductionBillSnapshot
{
    public string BillId { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public int BuildingId { get; set; }
    public Vector2Int Position { get; set; }
    public WorkTypeId WorkTypeId { get; set; }
    public ProductionOrderMode Mode { get; set; }
    public ProductionBillStatus Status { get; set; }
    public int RemainingCycles { get; set; }
    public int TargetStock { get; set; }
    public int MinimumReserve { get; set; }
    public float RequiredWork { get; set; }
    public float CompletedWork { get; set; }
    public bool MaterialsConsumed { get; set; }
    public string ReservedWorkerId { get; set; } = string.Empty;
    public string MaterialDestinationId { get; set; } = string.Empty;
    public string BlockedReason { get; set; } = string.Empty;
    public IReadOnlyList<ItemAmountDefinition> Inputs { get; set; } =
        Array.Empty<ItemAmountDefinition>();
    public IReadOnlyList<ProductionOutputDefinition> Outputs { get; set; } =
        Array.Empty<ProductionOutputDefinition>();

    public float ProgressRatio => RequiredWork <= 0f
        ? 0f
        : Mathf.Clamp01(CompletedWork / RequiredWork);
}

public sealed class ProductionBillCommandResult
{
    private ProductionBillCommandResult(
        bool succeeded,
        string billId,
        string message)
    {
        Succeeded = succeeded;
        BillId = billId ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string BillId { get; }
    public string Message { get; }

    public static ProductionBillCommandResult Success(
        string billId,
        string message = "") =>
        new ProductionBillCommandResult(true, billId, message);

    public static ProductionBillCommandResult Failure(string message) =>
        new ProductionBillCommandResult(false, string.Empty, message);
}

public interface IProductionBillRuntime
{
    int Version { get; }
    IReadOnlyList<ProductionBillSnapshot> GetBills(BuildableObject facility);
    ProductionBillCommandResult AddBill(
        BuildableObject facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount);
    ProductionBillCommandResult RemoveBill(string billId, bool returnMaterials);
    ProductionBillCommandResult MoveBill(string billId, int targetIndex);
    ProductionBillCommandResult SetSuspended(string billId, bool suspended);
    ProductionBillCommandResult SetStockPolicy(
        string billId,
        int minimumReserve,
        int targetStock);
    bool HasWorkAvailable(
        BuildableObject facility,
        WorkTypeId workTypeId,
        out string reason);
    bool TryBeginWork(
        CharacterActor worker,
        BuildableObject facility,
        WorkTypeId workTypeId,
        out ProductionBillSnapshot bill,
        out string failureReason);
    bool ApplyWork(
        CharacterActor worker,
        BuildableObject facility,
        string billId,
        float amount,
        out bool cycleCompleted,
        out string message);
    DungeonProductionBillSaveData Capture();
    void Restore(DungeonProductionBillSaveData snapshot);
}
