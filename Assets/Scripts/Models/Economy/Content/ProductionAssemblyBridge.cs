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
        Vector2Int overflowOffset,
        string definitionId,
        string workstationTag,
        int outputBufferCycleCapacity)
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
        DefinitionId = RequireCanonicalOptional(
            definitionId,
            nameof(definitionId));
        WorkstationTag = RequireCanonicalOptional(
            workstationTag,
            nameof(workstationTag));
        if (outputBufferCycleCapacity is < 2 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputBufferCycleCapacity));
        }
        OutputBufferCycleCapacity = outputBufferCycleCapacity;
    }

    public object RuntimeObject { get; }
    public BuildingInstanceId InstanceId { get; }
    public Vector2Int Position { get; }
    public bool IsDestroyed { get; }
    public string StockSensorInstallationItemId { get; }
    public bool AllowsOverflowDump { get; }
    public Vector2Int OverflowOffset { get; }
    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public int OutputBufferCycleCapacity { get; }

    private static string RequireCanonical(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Production facility semantic identity must be canonical.",
                parameter);
        }
        return value;
    }

    private static string RequireCanonicalOptional(string value, string parameter)
    {
        string token = value ?? string.Empty;
        if (token.Length > 0
            && (string.IsNullOrWhiteSpace(token)
                || !string.Equals(token, token.Trim(), StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Production facility semantic identity must be canonical.",
                parameter);
        }
        return token;
    }
}

/// <summary>
/// Immutable authored identity consumed by output-capacity projection. It is
/// deliberately detached from scene objects so live and current-format save
/// candidates run through the same calculation.
/// </summary>
public readonly struct ProductionFacilityCapacitySubject : IEquatable<ProductionFacilityCapacitySubject>
{
    public ProductionFacilityCapacitySubject(
        BuildingInstanceId facilityId,
        Vector2Int position,
        string definitionId,
        string workstationTag,
        int outputBufferCycleCapacity)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("Capacity subject requires a valid facility ID.", nameof(facilityId));
        if (string.IsNullOrWhiteSpace(definitionId)
            || !string.Equals(definitionId, definitionId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Capacity definition ID must be canonical.", nameof(definitionId));
        if (string.IsNullOrWhiteSpace(workstationTag)
            || !string.Equals(workstationTag, workstationTag.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Capacity workstation tag must be canonical.", nameof(workstationTag));
        if (outputBufferCycleCapacity is < 2 or > 4)
            throw new ArgumentOutOfRangeException(nameof(outputBufferCycleCapacity));

        FacilityId = facilityId;
        Position = position;
        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        OutputBufferCycleCapacity = outputBufferCycleCapacity;
    }

    public BuildingInstanceId FacilityId { get; }
    public Vector2Int Position { get; }
    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public int OutputBufferCycleCapacity { get; }

    public static ProductionFacilityCapacitySubject FromLive(
        ProductionFacilityHandle facility)
    {
        if (facility == null || facility.IsDestroyed)
            throw new ArgumentException("A live capacity facility is required.", nameof(facility));
        return new ProductionFacilityCapacitySubject(
            facility.InstanceId,
            facility.Position,
            facility.DefinitionId,
            facility.WorkstationTag,
            facility.OutputBufferCycleCapacity);
    }

    public bool Equals(ProductionFacilityCapacitySubject other) =>
        FacilityId.Equals(other.FacilityId)
        && Position == other.Position
        && string.Equals(DefinitionId, other.DefinitionId, StringComparison.Ordinal)
        && string.Equals(WorkstationTag, other.WorkstationTag, StringComparison.Ordinal)
        && OutputBufferCycleCapacity == other.OutputBufferCycleCapacity;

    public override bool Equals(object obj) =>
        obj is ProductionFacilityCapacitySubject other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        FacilityId,
        Position,
        DefinitionId,
        WorkstationTag,
        OutputBufferCycleCapacity);
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
public interface IProductionFacilityHandleQuery
{
    ProductionFacilityHandle CaptureFacility(object runtimeObject);
}

public interface IProductionAssemblyBridge : IProductionFacilityHandleQuery
{
    int BuildingVersion => 0;
    IReadOnlyList<ProductionFacilityHandle> Facilities { get; }
    ProductionWorkerHandle CaptureWorker(object runtimeObject);
    bool IsWorkerEligible(
        ProductionWorkerHandle worker,
        WorkerSelectionPolicySaveData policy,
        out string failureReason)
    {
        failureReason = string.Empty;
        return true;
    }
    float GetRelevantCraftSkill(
        ProductionWorkerHandle worker,
        ProductionRecipeSO recipe) => 50f;

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
    bool ConsumeDeliveredToWip(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        string operationId,
        out ProductionWipInputReceipt receipt,
        out string failureReason);
    bool AcknowledgeWipInput(
        string commitId,
        out string failureReason);
    bool CommitStockSensorInstallPending(
        string destinationId,
        string itemId,
        string operationId,
        string reasonCode,
        out ProductionStockSensorPhysicalReceipt receipt,
        out string failureReason);
    bool TryGetPendingStockSensorInstall(
        string operationId,
        out ProductionStockSensorPhysicalReceipt receipt);
    bool AcknowledgeStockSensorInstall(
        string commitId,
        out string failureReason);
    bool SpawnOutput(string itemId, int amount, Vector2Int position);
    bool SpawnBufferedOutput(
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId);
    bool TryCommitBufferedOutput(
        string commitId,
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId,
        out DomainFailure failure);
    bool AcknowledgeBufferedOutput(
        string commitId,
        out DomainFailure failure);
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
    bool TryReleaseDestinationAtomically(
        string destinationId,
        Vector2Int releasePosition,
        out int released,
        out string failureReason);
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
    long ResolveInputBufferMassCapacity(
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
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out ProductionProcessFluidReceipt receipt,
        out string failureReason);
    bool AcknowledgeCycleUtilities(
        ProductionProcessFluidReceipt receipt,
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
    int ResolveOutputBufferCycleCapacity(
        ProductionFacilityHandle facility) => 4;
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
        float workerQuality,
        string commitId,
        out bool handled,
        out DomainFailure failure);
    bool AcknowledgeHandledOutput(
        string itemId,
        string commitId,
        out DomainFailure failure);
    bool TryGetCommittedOutputMassGrams(
        string itemId,
        string commitId,
        out long massGrams,
        out DomainFailure failure);

    bool MatchesWorkstation(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe);
    bool HasRequiredSupports(
        ProductionFacilityHandle facility,
        IReadOnlyList<string> requiredFeatureTags,
        out string failureReason);
    bool HasCompatibleWarehouse(string itemId, StockCategory category);
    void RequestWorkReplan(WorkTypeId workTypeId);
    void RequestOneHaulerToReplan(bool forceInterrupt);
}

public interface IProductionInputDestinationClaimRuntime
{
    bool TryValidateClaim(
        ProductionBillRecord record,
        out string failureReason);

    bool TryClaim(
        ProductionBillRecord record,
        ProductionFacilityHandle facility,
        long maxInputBufferMassGrams,
        out string failureReason);

    bool TryEnsureCapacity(
        ProductionBillRecord record,
        long minimumInputBufferMassGrams,
        out string failureReason);

    bool TryRevoke(
        ProductionBillRecord record,
        out string failureReason);

    bool TryRevokeIfPresent(
        ProductionBillRecord record,
        out string failureReason);

    bool TryReplace(
        IReadOnlyList<ProductionBillRecord> records,
        IReadOnlyList<ProductionFacilityHandle> facilities,
        IReadOnlyDictionary<string, long> inputBufferMassGramsByBillId,
        out string failureReason);
}

public interface IProductionBillCoreQuery
{
    int Version { get; }
    IReadOnlyList<ProductionBillSnapshot> GetBills(
        ProductionFacilityHandle facility);
    ProductionFacilityBillLifecycleSnapshot CaptureFacilityLifecycle(
        BuildingInstanceId facilityId);
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
    ProductionBillCommandResult SetWorkerPolicy(
        ProductionBillId billId,
        WorkerSelectionPolicySaveData policy);
    ProductionBillCommandResult SetEmergencyWorker(
        ProductionBillId billId,
        string characterId);
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
