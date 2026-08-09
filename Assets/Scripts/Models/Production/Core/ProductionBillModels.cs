using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ProductionBillStatus
{
    WaitingForMaterials = 0,
    Ready = 1,
    InProgress = 2,
    Suspended = 3,
    Completed = 4,
    Cancelled = 5,
    WaitingForSupports = 6,
    WaitingForUtilities = 7,
    Processing = 8,
    WaitingForFinishing = 9,
    WaitingForOutputSpace = 10,
    WaitingForStockSensor = 11,
    WaitingForDistributionRoute = 12,
    WaitingForEligibleWorker = 13
}

public enum ProductionBatchStage
{
    None = 0,
    Preparing = 1,
    Processing = 2,
    Finishing = 3
}

public readonly struct ProductionBillId : IPersistentEntityId, IEquatable<ProductionBillId>
{
    private readonly string value;

    public ProductionBillId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => PersistentEntityId.IsKind(Value, "production-bill");
    public bool Equals(ProductionBillId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is ProductionBillId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static bool operator ==(ProductionBillId left, ProductionBillId right) =>
        left.Equals(right);
    public static bool operator !=(ProductionBillId left, ProductionBillId right) =>
        !left.Equals(right);
    public static explicit operator ProductionBillId(string value) => new(value);
}

public enum ProductionBillOutcomeCode
{
    None = 0,
    BillAdded,
    BillRemoved,
    BillUpdated,
    StockSensorDeliveryRequested,
    StockSensorInstalled,
    StockSensorRemoved,
    StockSensorAcknowledged,
    WorkProgressed,
    ProcessingStarted,
    CycleCompleted,
    OrderModeTransitionCompleted,
    MaterialPrefetchAdjusted
}

[Serializable]
public sealed class ProductionStatusSaveData
{
    public FailureCode code;
    public ProductionBillOutcomeCode outcome;
    public List<string> parameters = new();
}

public readonly struct ProductionLogisticsStatus
{
    public ProductionLogisticsStatus(
        ProductionBillOutcomeCode code,
        params string[] parameters)
    {
        Code = code;
        Parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly((string[])parameters.Clone());
    }

    public ProductionBillOutcomeCode Code { get; }
    public IReadOnlyList<string> Parameters { get; }
    public bool HasStatus => Code != ProductionBillOutcomeCode.None;
    public static ProductionLogisticsStatus None =>
        new(ProductionBillOutcomeCode.None);
}

[Serializable]
public sealed class ProductionBillSaveData
{
    public string billId = string.Empty;
    public string recipeId = string.Empty;
    public string buildingInstanceId = string.Empty;
    public ProductionOrderMode mode;
    public int remainingCycles = 1;
    public int targetStock = 10;
    public int minimumReserve;
    public bool suspended;
    public bool materialsConsumed;
    public bool processFluidConsumed;
    public float completedWork;
    public ProductionBatchStage batchStage;
    public float remainingProcessingHours;
    public float batchIntegrity = 100f;
    public float utilityOutageHours;
    public float temperatureOutageHours;
    public string occupiedSupportNodeId = string.Empty;
    public ProductionStatusSaveData blocked = new();
    public string reservedWorkerId = string.Empty;
    public string materialDestinationId = string.Empty;
    public int prefetchBatchCount = 1;
    public float estimatedDeliverySeconds = 12f;
    public float estimatedProductionCycleSeconds;
    public ProductionStatusSaveData logistics = new();
    public List<string> allowedMaterialIds = new List<string>();
    public List<string> allowedWorkerIds = new List<string>();
    public WorkerSelectionPolicySaveData workerPolicy =
        WorkerSelectionPolicySaveData.Anyone(WorkerCandidateSortMode.Fastest);
    public List<CraftContributionSaveData> workerContributions = new();
    public bool hasPendingModeTransition;
    public ProductionOrderMode pendingMode;
    public string outputDestinationId = string.Empty;
    public List<ProductionOutputReservationSaveData> outputReservations = new();
    public ProductionDistributionMode distributionMode =
        ProductionDistributionMode.DemandWeighted;
    public List<ProductionConsumerRoutePolicy> routePolicies = new();
    public List<ProductionSelectedSupplySaveData> selectedSupplies = new();
}

[Serializable]
public sealed class ProductionOutputReservationSaveData
{
    public string itemId = string.Empty;
    public int amount;
}

[Serializable]
public sealed class ProductionSelectedSupplySaveData
{
    public string supplyKey = string.Empty;
    public string itemId = string.Empty;
}

[Serializable]
public sealed class DungeonProductionBillSaveData
{
    public const int CurrentVersion = 6;

    public int version = CurrentVersion;
    public int nextBillSequence = 1;
    public List<ProductionBillSaveData> bills = new List<ProductionBillSaveData>();
    public List<string> installedStockSensorFacilityIds = new List<string>();
    public List<string> acknowledgedStockSensorFacilityIds = new List<string>();
}

public sealed class ProductionBillSnapshot
{
    public ProductionBillId BillId { get; set; }
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public BuildingInstanceId BuildingInstanceId { get; set; }
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
    public bool ProcessFluidConsumed { get; set; }
    public ProductionBatchStage BatchStage { get; set; }
    public float RemainingProcessingHours { get; set; }
    public float BatchIntegrity { get; set; } = 100f;
    public float UtilityOutageHours { get; set; }
    public float TemperatureOutageHours { get; set; }
    public string OccupiedSupportNodeId { get; set; } = string.Empty;
    public string ReservedWorkerId { get; set; } = string.Empty;
    public WorkerSelectionPolicySaveData WorkerPolicy { get; set; } =
        WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.Fastest);
    public string MaterialDestinationId { get; set; } = string.Empty;
    public DomainFailure BlockedFailure { get; set; } = DomainFailure.None;
    public int PrefetchBatchCount { get; set; } = 1;
    public float EstimatedDeliverySeconds { get; set; } = 12f;
    public float EstimatedProductionCycleSeconds { get; set; }
    public ProductionLogisticsStatus Logistics { get; set; } =
        ProductionLogisticsStatus.None;
    public IReadOnlyList<ItemAmountDefinition> Inputs { get; set; } =
        Array.Empty<ItemAmountDefinition>();
    public IReadOnlyList<ProductionOutputDefinition> Outputs { get; set; } =
        Array.Empty<ProductionOutputDefinition>();

    public float ProgressRatio => RequiredWork <= 0f
        ? 0f
        : Mathf.Clamp01(CompletedWork / RequiredWork);

    public float ProcessingProgressRatio { get; set; }
    public bool HasPendingModeTransition { get; set; }
    public ProductionOrderMode PendingMode { get; set; }
    public string OutputDestinationId { get; set; } = string.Empty;
    public int OutputBufferedQuantity { get; set; }
    public int ReservedOutputQuantity { get; set; }
    public int OutputCapacity { get; set; }
    public bool HasStockSensor { get; set; }
    public bool HasUnacknowledgedStockSensorUnlock { get; set; }
    public ProductionDistributionMode DistributionMode { get; set; }
    public IReadOnlyList<ProductionConsumerRoutePolicy> RoutePolicies { get; set; } =
        Array.Empty<ProductionConsumerRoutePolicy>();
    public IReadOnlyList<ProductionConsumerRouteState> RouteStates { get; set; } =
        Array.Empty<ProductionConsumerRouteState>();
}

public sealed class ProductionBillCommandResult
{
    private ProductionBillCommandResult(
        bool succeeded,
        ProductionBillId billId,
        ProductionBillOutcomeCode outcome,
        DomainFailure failure)
    {
        Succeeded = succeeded;
        BillId = billId;
        Outcome = outcome;
        Failure = failure;
    }

    public bool Succeeded { get; }
    public ProductionBillId BillId { get; }
    public ProductionBillOutcomeCode Outcome { get; }
    public DomainFailure Failure { get; }

    public static ProductionBillCommandResult Success(
        ProductionBillId billId,
        ProductionBillOutcomeCode outcome = ProductionBillOutcomeCode.BillUpdated) =>
        new(true, billId, outcome, DomainFailure.None);

    public static ProductionBillCommandResult Failed(DomainFailure failure) =>
        new(false, default, ProductionBillOutcomeCode.None, failure);
}

public static class ProductionMaterialPrefetchPolicy
{
    public static int CalculateBatchCount(
        float estimatedDeliverySeconds,
        float safetySeconds,
        float effectiveProductionCycleSeconds,
        int maximumBatches = 3)
    {
        return Mathf.Clamp(
            Mathf.CeilToInt(
                (Mathf.Max(0f, estimatedDeliverySeconds)
                    + Mathf.Max(0f, safetySeconds))
                / Mathf.Max(0.1f, effectiveProductionCycleSeconds)),
            1,
            Mathf.Max(1, maximumBatches));
    }
}

public readonly struct ProductionWorkAvailabilityResult
{
    public ProductionWorkAvailabilityResult(bool available, DomainFailure failure)
    {
        Available = available;
        Failure = failure;
    }

    public bool Available { get; }
    public DomainFailure Failure { get; }
}

public readonly struct ProductionWorkBeginResult
{
    public ProductionWorkBeginResult(
        ProductionBillSnapshot bill,
        DomainFailure failure)
    {
        Bill = bill;
        Failure = failure;
    }

    public ProductionBillSnapshot Bill { get; }
    public DomainFailure Failure { get; }
    public bool Succeeded => Bill != null && !Failure.IsFailure;
}

public readonly struct ProductionWorkExecutionResult
{
    public ProductionWorkExecutionResult(
        bool succeeded,
        bool cycleCompleted,
        ProductionBillOutcomeCode outcome,
        DomainFailure failure,
        params string[] parameters)
    {
        Succeeded = succeeded;
        CycleCompleted = cycleCompleted;
        Outcome = outcome;
        Failure = failure;
        Parameters = parameters == null || parameters.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly((string[])parameters.Clone());
    }

    public bool Succeeded { get; }
    public bool CycleCompleted { get; }
    public ProductionBillOutcomeCode Outcome { get; }
    public DomainFailure Failure { get; }
    public IReadOnlyList<string> Parameters { get; }
}

public sealed class ProductionBillRestoreCandidate
{
    internal ProductionBillRestoreCandidate(ProductionAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal ProductionAggregateState State { get; }

    public static ProductionBillRestoreCandidate Create(
        DungeonProductionBillSaveData snapshot,
        int billVersion,
        int stockSensorVersion)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }
        return new ProductionBillRestoreCandidate(
            ProductionAggregateStateSession.CreateRestoreState(
                snapshot,
                billVersion,
                stockSensorVersion));
    }
}

public interface IProductionBillPersistence
{
    DungeonProductionBillSaveData Capture();
    ProductionBillRestoreCandidate BuildRestore(
        DungeonProductionBillSaveData snapshot);
    void Restore(ProductionBillRestoreCandidate candidate);
}
