using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Opaque scene reference captured at the Assembly-CSharp composition boundary.
/// Economy code owns the stable identity and authored values; only the adapter
/// may inspect <see cref="RuntimeObject"/>.
/// </summary>
public sealed class ProductionFacilityHandle
{
    public ProductionFacilityHandle(
        object runtimeObject,
        BuildingInstanceId instanceId,
        Vector2Int position,
        bool isDestroyed,
        string stockSensorInstallationItemId,
        bool allowsOverflowDump,
        Vector2Int overflowOffset)
    {
        RuntimeObject = runtimeObject
            ?? throw new ArgumentNullException(nameof(runtimeObject));
        InstanceId = instanceId;
        Position = position;
        IsDestroyed = isDestroyed;
        StockSensorInstallationItemId =
            stockSensorInstallationItemId?.Trim() ?? string.Empty;
        AllowsOverflowDump = allowsOverflowDump;
        OverflowOffset = overflowOffset;
    }

    public object RuntimeObject { get; }
    public BuildingInstanceId InstanceId { get; }
    public Vector2Int Position { get; }
    public bool IsDestroyed { get; }
    public string StockSensorInstallationItemId { get; }
    public bool AllowsOverflowDump { get; }
    public Vector2Int OverflowOffset { get; }
}

public sealed class ProductionWorkerHandle
{
    public ProductionWorkerHandle(object runtimeObject, string persistentId)
    {
        RuntimeObject = runtimeObject;
        PersistentId = persistentId?.Trim() ?? string.Empty;
    }

    public object RuntimeObject { get; }
    public string PersistentId { get; }
}

public enum ProductionSupportModifierKind
{
    WorkSpeed = 0,
    Output = 1,
    Quality = 2
}

/// <summary>
/// Anti-corruption port for scene actors and legacy production implementations.
/// It is implemented only in the default composition assembly; the production
/// aggregate never depends on BuildableObject, CharacterActor, or their services.
/// </summary>
public interface IProductionAssemblyBridge
{
    IReadOnlyList<ProductionFacilityHandle> Facilities { get; }
    ProductionFacilityHandle CaptureFacility(object runtimeObject);
    ProductionWorkerHandle CaptureWorker(object runtimeObject);

    int CountDelivered(string itemId, string destinationId);
    int CountPending(string itemId, string destinationId);
    int CountAvailableStock(string itemId, string excludedDestinationId);
    int CountBufferedOutput(string itemId);
    int CountBufferedOutput(string itemId, string destinationId);
    bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
    bool ConsumeDelivered(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason);
    bool SpawnOutput(string itemId, int amount, Vector2Int position);
    bool SpawnBufferedOutput(
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId);
    bool TryRouteBufferedOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure);
    void PrioritizeDestination(string destinationId);
    int ReleaseDestination(string destinationId, Vector2Int releasePosition);
    int RemoveDestination(string destinationId);
    string GetOldestAvailableStackId(
        string itemId,
        string excludedDestinationId);

    ProductionBillRecord FindRunnableBill(
        IReadOnlyList<ProductionBillRecord> bills,
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out DomainFailure failure);
    bool HasDeliveredInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out DomainFailure failure);
    void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);
    void RecalculatePrefetch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionWorkerHandle worker);
    bool ShouldRunAnotherCycle(
        ProductionBillRecord record,
        ProductionRecipeSO recipe);
    bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out DomainFailure failure);
    Dictionary<string, int> ToCycleInputMap(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);

    bool ValidateCycleRequirements(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string failureReason);
    bool ValidateProcessingUtilities(
        string occupiedSupportNodeId,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason);
    bool TryConsumeCycleUtilities(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason);
    bool TryResolveBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string supportNodeId,
        out string failureReason);
    float ResolveTemperatureSpeed(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out bool dangerous);
    ProductionFacilityHandle ResolveOccupiedBatchSupport(
        string occupiedSupportNodeId,
        ProductionFacilityHandle facility);

    int ResolveOutputCapacity(
        ProductionFacilityHandle facility,
        string itemId,
        int outputPerBatch,
        int stackLimit);
    float ResolveSupportModifier(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe,
        ProductionSupportModifierKind kind,
        float defaultValue,
        bool multiply);
    bool TryHandleOutput(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        string itemId,
        int amount,
        float qualityModifier,
        out bool handled,
        out DomainFailure failure);

    bool MatchesWorkstation(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe);
    bool HasRequiredSupports(
        ProductionFacilityHandle facility,
        IReadOnlyList<string> requiredFeatureTags,
        out string failureReason);
    bool HasCompatibleWarehouse(StockCategory category);
    void RequestWorkReplan(WorkTypeId workTypeId);
    void RequestOneHaulerToReplan(bool forceInterrupt);
}

public interface IProductionBillCoreQuery
{
    int Version { get; }
    IReadOnlyList<ProductionBillSnapshot> GetBills(
        ProductionFacilityHandle facility);
    bool HasStockSensor(ProductionFacilityHandle facility);
}

public interface IProductionBillCoreOrderCommand
{
    ProductionBillCommandResult AddBill(
        ProductionFacilityHandle facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount);
    ProductionBillCommandResult RemoveBill(
        ProductionBillId billId,
        bool returnMaterials);
    ProductionBillCommandResult MoveBill(
        ProductionBillId billId,
        int targetIndex);
    ProductionBillCommandResult SetSuspended(
        ProductionBillId billId,
        bool suspended);
    ProductionBillCommandResult SetStockPolicy(
        ProductionBillId billId,
        int minimumReserve,
        int targetStock);
    ProductionBillCommandResult SetOrderMode(
        ProductionBillId billId,
        ProductionOrderMode mode,
        int amount);
    ProductionBillCommandResult SetDistributionPolicy(
        ProductionBillId billId,
        ProductionDistributionMode mode,
        IReadOnlyList<ProductionConsumerRoutePolicy> routes);
    ProductionBillCommandResult RequestStockSensorInstallation(
        ProductionFacilityHandle facility);
    ProductionBillCommandResult AcknowledgeStockSensorUnlock(
        ProductionFacilityHandle facility);
    ProductionBillCommandResult RemoveStockSensor(
        ProductionFacilityHandle facility);
}

public interface IProductionBillCoreWorkExecution
{
    ProductionWorkAvailabilityResult CheckWorkAvailability(
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId);
    ProductionWorkBeginResult BeginWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId);
    ProductionWorkExecutionResult ExecuteWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        ProductionBillId billId,
        float amount);
}
