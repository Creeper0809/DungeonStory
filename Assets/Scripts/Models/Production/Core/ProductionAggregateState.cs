using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionBillRecord
{
    internal ProductionBillRecord()
    {
    }

    public ProductionBillId billId { get; internal set; }
    public string recipeId { get; internal set; } = string.Empty;
    public BuildingInstanceId buildingInstanceId { get; internal set; }
    public ProductionOrderMode mode { get; internal set; }
    public int remainingCycles { get; internal set; }
    public int targetStock { get; internal set; }
    public int minimumReserve { get; internal set; }
    public bool suspended { get; internal set; }
    public bool materialsConsumed { get; internal set; }
    public bool processFluidConsumed { get; internal set; }
    public float completedWork { get; internal set; }
    public ProductionBatchStage batchStage { get; internal set; }
    public float remainingProcessingHours { get; internal set; }
    public float batchIntegrity { get; internal set; } = 100f;
    public float utilityOutageHours { get; internal set; }
    public float temperatureOutageHours { get; internal set; }
    public string occupiedSupportNodeId { get; internal set; } = string.Empty;
    public DomainFailure blockedFailure { get; internal set; } = DomainFailure.None;
    public string reservedWorkerId { get; internal set; } = string.Empty;
    public string materialDestinationId { get; internal set; } = string.Empty;
    public int prefetchBatchCount { get; internal set; } = 1;
    public float estimatedDeliverySeconds { get; internal set; } = 12f;
    public float estimatedProductionCycleSeconds { get; internal set; }
    public ProductionLogisticsStatus logisticsStatus { get; internal set; } =
        ProductionLogisticsStatus.None;
    public bool hasPendingModeTransition { get; internal set; }
    public ProductionOrderMode pendingMode { get; internal set; }
    public string outputDestinationId { get; internal set; } = string.Empty;
    internal readonly Dictionary<string, int> mutableOutputReservations =
        new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, int> outputReservations =>
        mutableOutputReservations;
    public ProductionDistributionMode distributionMode { get; internal set; } =
        ProductionDistributionMode.DemandWeighted;
    internal readonly List<ProductionConsumerRoutePolicy> mutableRoutePolicies = new();
    public IReadOnlyList<ProductionConsumerRoutePolicy> routePolicies =>
        mutableRoutePolicies;
    internal readonly Dictionary<string, string> mutableSelectedSupplies =
        new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> selectedSupplies =>
        mutableSelectedSupplies;
    internal readonly HashSet<string> mutableAllowedMaterialIds =
        new(StringComparer.Ordinal);
    public IReadOnlyCollection<string> allowedMaterialIds =>
        mutableAllowedMaterialIds;
    internal readonly HashSet<string> mutableAllowedWorkerIds =
        new(StringComparer.Ordinal);
    public IReadOnlyCollection<string> allowedWorkerIds => mutableAllowedWorkerIds;
    public WorkerSelectionPolicySaveData workerPolicy { get; internal set; } =
        WorkerSelectionPolicySaveData.Anyone(WorkerCandidateSortMode.Fastest);
    internal readonly List<CraftContributionSaveData> mutableWorkerContributions = new();
    public IReadOnlyList<CraftContributionSaveData> workerContributions =>
        mutableWorkerContributions;

    public static ProductionBillRecord Create(
        ProductionBillId billId,
        string recipeId,
        BuildingInstanceId buildingInstanceId,
        ProductionOrderMode mode,
        int remainingCycles,
        int targetStock,
        ProductionBatchStage batchStage,
        string materialDestinationId)
    {
        return new ProductionBillRecord
        {
            billId = billId,
            recipeId = recipeId?.Trim() ?? string.Empty,
            buildingInstanceId = buildingInstanceId,
            mode = mode,
            remainingCycles = remainingCycles,
            targetStock = targetStock,
            batchStage = batchStage,
            batchIntegrity = 100f,
            materialDestinationId = materialDestinationId?.Trim() ?? string.Empty
        };
    }

    public void SetSuspended(bool value)
    {
        suspended = value;
        reservedWorkerId = string.Empty;
    }

    public void SetStockPolicy(int minimum, int target)
    {
        minimumReserve = Math.Max(0, minimum);
        targetStock = Math.Max(minimumReserve, target);
    }

    public void SetRepeatCount(int cycles)
    {
        remainingCycles = Math.Max(0, cycles);
    }

    public void SetOrderMode(ProductionOrderMode value) => mode = value;
    public void SetMaterialsConsumed(bool value) => materialsConsumed = value;
    public void SetProcessFluidConsumed(bool value) => processFluidConsumed = value;
    public void SetCompletedWork(float value) => completedWork = value;
    public void SetBatchStage(ProductionBatchStage value) => batchStage = value;
    public void SetRemainingProcessingHours(float value) =>
        remainingProcessingHours = value;
    public void SetBatchIntegrity(float value) => batchIntegrity = value;
    public void SetUtilityOutageHours(float value) => utilityOutageHours = value;
    public void SetTemperatureOutageHours(float value) =>
        temperatureOutageHours = value;
    public void SetOccupiedSupportNode(string id) =>
        occupiedSupportNodeId = id?.Trim() ?? string.Empty;
    public void SetBlockedFailure(DomainFailure failure) => blockedFailure = failure;
    public void SetReservedWorker(string id) =>
        reservedWorkerId = id?.Trim() ?? string.Empty;
    public void SetWorkerPolicy(WorkerSelectionPolicySaveData value) =>
        workerPolicy = value?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
    public void ReplaceWorkerContributions(
        IEnumerable<CraftContributionSaveData> values)
    {
        mutableWorkerContributions.Clear();
        mutableWorkerContributions.AddRange((values
                ?? Array.Empty<CraftContributionSaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone()));
    }
    public void SetOutputDestination(string id) =>
        outputDestinationId = id?.Trim() ?? string.Empty;

    public void SetPrefetchPlan(
        float productionCycleSeconds,
        int batchCount,
        ProductionLogisticsStatus status)
    {
        estimatedProductionCycleSeconds = productionCycleSeconds;
        prefetchBatchCount = Math.Max(1, batchCount);
        logisticsStatus = status;
    }

    public void RequestModeTransition(ProductionOrderMode value)
    {
        hasPendingModeTransition = true;
        pendingMode = value;
    }

    public void ClearModeTransition()
    {
        hasPendingModeTransition = false;
    }

    public void ReplaceDistributionPolicy(
        ProductionDistributionMode value,
        IEnumerable<ProductionConsumerRoutePolicy> routes)
    {
        distributionMode = value;
        mutableRoutePolicies.Clear();
        if (routes != null)
        {
            mutableRoutePolicies.AddRange(routes);
        }
    }

    public int RemoveRoutes(Predicate<ProductionConsumerRoutePolicy> predicate) =>
        mutableRoutePolicies.RemoveAll(predicate);
    public void AddRoute(ProductionConsumerRoutePolicy route)
    {
        if (route != null)
        {
            mutableRoutePolicies.Add(route);
        }
    }

    public void ClearOutputReservations() => mutableOutputReservations.Clear();
    public void SetOutputReservation(string itemId, int amount) =>
        mutableOutputReservations[itemId] = amount;
    public void ClearSelectedSupplies() => mutableSelectedSupplies.Clear();
    public void SelectSupply(string supplyKey, string itemId) =>
        mutableSelectedSupplies[supplyKey] = itemId;

    internal void AddAllowedMaterial(string itemId) =>
        mutableAllowedMaterialIds.Add(itemId);
    internal void AddAllowedWorker(string workerId) =>
        mutableAllowedWorkerIds.Add(workerId);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class ProductionAggregateState
{
    internal List<ProductionBillRecord> Bills { get; } = new();
    internal HashSet<string> InstalledStockSensorFacilityIds { get; } =
        new(StringComparer.Ordinal);
    internal HashSet<string> AcknowledgedStockSensorFacilityIds { get; } =
        new(StringComparer.Ordinal);
    internal int NextBillSequence { get; set; } = 1;
    internal int BillVersion { get; set; }
    internal int StockSensorVersion { get; set; }
}

public sealed class ProductionAggregateStateSession
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;

    public ProductionAggregateStateSession(DungeonRuntimeAggregateRootStore rootStore)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
    }

    private ProductionAggregateState Current =>
        rootStore.GetOrCreate(() => new ProductionAggregateState());

    public IReadOnlyList<ProductionBillRecord> Bills => Current.Bills;
    public int NextBillSequence
    {
        get => Current.NextBillSequence;
        set => Current.NextBillSequence = Math.Max(1, value);
    }
    public int BillVersion => Current.BillVersion;
    public int StockSensorVersion => Current.StockSensorVersion;
    public IReadOnlyCollection<string> InstalledStockSensorFacilityIds =>
        Current.InstalledStockSensorFacilityIds;
    public IReadOnlyCollection<string> AcknowledgedStockSensorFacilityIds =>
        Current.AcknowledgedStockSensorFacilityIds;

    public void AddBill(ProductionBillRecord bill) =>
        Current.Bills.Add(bill ?? throw new ArgumentNullException(nameof(bill)));
    public bool RemoveBill(ProductionBillRecord bill) => Current.Bills.Remove(bill);
    public void MoveBill(
        ProductionBillRecord bill,
        ProductionBillRecord anchor,
        bool insertAfter)
    {
        if (bill == null || anchor == null || ReferenceEquals(bill, anchor))
        {
            return;
        }
        List<ProductionBillRecord> bills = Current.Bills;
        if (!bills.Remove(bill))
        {
            return;
        }
        int anchorIndex = bills.IndexOf(anchor);
        bills.Insert(
            anchorIndex < 0
                ? bills.Count
                : Math.Min(bills.Count, anchorIndex + (insertAfter ? 1 : 0)),
            bill);
    }
    public void IncrementBillVersion() => Current.BillVersion++;
    public void IncrementStockSensorVersion() => Current.StockSensorVersion++;
    public bool HasInstalledSensor(string id) =>
        Current.InstalledStockSensorFacilityIds.Contains(id);
    public bool HasAcknowledgedSensor(string id) =>
        Current.AcknowledgedStockSensorFacilityIds.Contains(id);
    public bool AddInstalledSensor(string id) =>
        Current.InstalledStockSensorFacilityIds.Add(id);
    public bool RemoveInstalledSensor(string id) =>
        Current.InstalledStockSensorFacilityIds.Remove(id);
    public bool AddAcknowledgedSensor(string id) =>
        Current.AcknowledgedStockSensorFacilityIds.Add(id);
    public bool RemoveAcknowledgedSensor(string id) =>
        Current.AcknowledgedStockSensorFacilityIds.Remove(id);

    public void Restore(ProductionBillRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        rootStore.Replace(candidate.State);
    }

    internal static ProductionAggregateState CreateRestoreState(
        DungeonProductionBillSaveData snapshot,
        int billVersion,
        int stockSensorVersion)
    {
        ProductionAggregateState restored = new()
        {
            NextBillSequence = snapshot.nextBillSequence,
            BillVersion = billVersion,
            StockSensorVersion = stockSensorVersion
        };
        restored.InstalledStockSensorFacilityIds.UnionWith(
            snapshot.installedStockSensorFacilityIds);
        restored.AcknowledgedStockSensorFacilityIds.UnionWith(
            snapshot.acknowledgedStockSensorFacilityIds);
        foreach (ProductionBillSaveData saved in snapshot.bills)
        {
            ProductionBillRecord record = new()
            {
                billId = (ProductionBillId)saved.billId,
                recipeId = saved.recipeId,
                buildingInstanceId = (BuildingInstanceId)saved.buildingInstanceId,
                mode = saved.mode,
                remainingCycles = saved.remainingCycles,
                targetStock = saved.targetStock,
                minimumReserve = saved.minimumReserve,
                suspended = saved.suspended,
                materialsConsumed = saved.materialsConsumed,
                processFluidConsumed = saved.processFluidConsumed,
                completedWork = saved.completedWork,
                batchStage = saved.batchStage,
                remainingProcessingHours = saved.remainingProcessingHours,
                batchIntegrity = saved.batchIntegrity,
                utilityOutageHours = saved.utilityOutageHours,
                temperatureOutageHours = saved.temperatureOutageHours,
                occupiedSupportNodeId = saved.occupiedSupportNodeId,
                blockedFailure = new DomainFailure(
                    saved.blocked.code,
                    saved.blocked.parameters.ToArray()),
                reservedWorkerId = string.Empty,
                workerPolicy = saved.workerPolicy?.CloneNormalized()
                    ?? WorkerSelectionPolicySaveData.Anyone(),
                materialDestinationId = saved.materialDestinationId,
                prefetchBatchCount = saved.prefetchBatchCount,
                estimatedDeliverySeconds = saved.estimatedDeliverySeconds,
                estimatedProductionCycleSeconds = saved.estimatedProductionCycleSeconds,
                logisticsStatus = new ProductionLogisticsStatus(
                    saved.logistics.outcome,
                    saved.logistics.parameters.ToArray()),
                hasPendingModeTransition = saved.hasPendingModeTransition,
                pendingMode = saved.pendingMode,
                outputDestinationId = saved.outputDestinationId,
                distributionMode = saved.distributionMode
            };
            foreach (ProductionOutputReservationSaveData reservation in
                     saved.outputReservations)
            {
                record.mutableOutputReservations.Add(
                    reservation.itemId,
                    reservation.amount);
            }
            record.mutableRoutePolicies.AddRange(
                saved.routePolicies.Select(route => route.Clone()));
            foreach (ProductionSelectedSupplySaveData supply in saved.selectedSupplies)
            {
                record.mutableSelectedSupplies.Add(supply.supplyKey, supply.itemId);
            }
            record.mutableAllowedMaterialIds.UnionWith(saved.allowedMaterialIds);
            record.mutableAllowedWorkerIds.UnionWith(saved.allowedWorkerIds);
            record.mutableWorkerContributions.AddRange(
                (saved.workerContributions
                    ?? new List<CraftContributionSaveData>())
                .Where(value => value != null)
                .Select(value => value.Clone()));
            restored.Bills.Add(record);
        }
        return restored;
    }
}
