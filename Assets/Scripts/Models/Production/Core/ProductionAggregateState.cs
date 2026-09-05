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
    public int cycleSequence { get; internal set; } = 1;
    public string wipInputCommitId { get; internal set; } = string.Empty;
    public int wipInputQuantity { get; internal set; }
    public long wipInputMassGrams { get; internal set; }
    public bool outputOutcomeResolved { get; internal set; }
    internal readonly List<ProductionResolvedOutputSaveData> mutableResolvedOutputs =
        new();
    public IReadOnlyList<ProductionResolvedOutputSaveData> resolvedOutputs =>
        mutableResolvedOutputs;
    public ProductionPreparedOutputBatchSaveData preparedOutput { get; internal set; } =
        ProductionPreparedOutputBatchSaveData.Unresolved();
    public bool processFluidConsumed { get; internal set; }
    public long processCleanWaterMassGrams { get; internal set; }
    public long processWastewaterMassGrams { get; internal set; }
    internal readonly List<ProductionWastewaterComponentSaveData>
        mutableProcessWastewaterComponents = new();
    public IReadOnlyList<ProductionWastewaterComponentSaveData>
        processWastewaterComponents => mutableProcessWastewaterComponents;
    internal readonly List<ProductionManualWaterTransferSaveData>
        mutableProcessManualWaterTransfers = new();
    public IReadOnlyList<ProductionManualWaterTransferSaveData>
        processManualWaterTransfers => mutableProcessManualWaterTransfers;
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
    public string emergencyWorkerId { get; internal set; } = string.Empty;
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
            cycleSequence = 1,
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
    public void AdvanceCycleSequence() => cycleSequence = checked(cycleSequence + 1);
    public void SetWipInput(ProductionWipInputReceipt receipt)
    {
        if (!receipt.IsCommitted)
        {
            throw new ArgumentException(
                "Production WIP input receipt must be committed.",
                nameof(receipt));
        }
        wipInputCommitId = receipt.CommitId;
        wipInputQuantity = receipt.Quantity;
        wipInputMassGrams = receipt.InputMassGrams;
    }
    public void ClearWipInput()
    {
        wipInputCommitId = string.Empty;
        wipInputQuantity = 0;
        wipInputMassGrams = 0L;
    }
    public void SetResolvedOutputs(
        IEnumerable<ProductionResolvedOutputSaveData> outputs)
    {
        mutableResolvedOutputs.Clear();
        mutableResolvedOutputs.AddRange((outputs
                ?? Array.Empty<ProductionResolvedOutputSaveData>())
            .Where(output => output != null)
            .Select(output => output.Clone()));
        outputOutcomeResolved = true;
    }
    public void ClearResolvedOutputs()
    {
        if (mutableResolvedOutputs.Any(output => output != null
            && (!string.IsNullOrEmpty(output.pendingCommitId)
                || output.pendingCommitApplied
                || output.pendingOutputPublication?.phase
                    != ProductionExactOutputPublicationPhase.None)))
        {
            throw new InvalidOperationException(
                "Resolved production outputs still own pending physical publication state.");
        }
        mutableResolvedOutputs.Clear();
        outputOutcomeResolved = false;
    }

    public void ResolvePreparedOutput(
        ProductionPreparedOutputBatchSaveData resolvedBatch)
    {
        RequirePreparedPhase(ProductionPreparedOutputPhase.Unresolved);
        if (outputOutcomeResolved || mutableResolvedOutputs.Count != 0
            || resolvedBatch == null
            || resolvedBatch.phase !=
                ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace)
        {
            throw new InvalidOperationException(
                "Prepared production output cannot coexist with legacy resolved output authority.");
        }
        ProductionPreparedOutputContract.ValidateForBill(
            resolvedBatch,
            billId,
            recipeId,
            cycleSequence,
            outputDestinationId);
        preparedOutput = resolvedBatch.Clone();
    }

    public void MarkPreparedOutputPublicationPrepared(
        string admissionFingerprint)
    {
        RequirePreparedPhase(
            ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace);
        ProductionPreparedOutputBatchSaveData candidate = preparedOutput.Clone();
        candidate.phase = ProductionPreparedOutputPhase.PublicationPrepared;
        candidate.admissionFingerprint = admissionFingerprint ?? string.Empty;
        ValidateAndPublishPrepared(candidate);
    }

    public void ReturnPreparedOutputToWaitingForSpace()
    {
        RequirePreparedPhase(ProductionPreparedOutputPhase.PublicationPrepared);
        ProductionPreparedOutputBatchSaveData candidate = preparedOutput.Clone();
        candidate.phase =
            ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace;
        candidate.admissionFingerprint = string.Empty;
        candidate.physicalCandidates.Clear();
        ValidateAndPublishPrepared(candidate);
    }

    public void ReleaseUnpublishedPreparedOutput()
    {
        RequirePreparedPhase(
            ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace);
        preparedOutput = ProductionPreparedOutputBatchSaveData.Unresolved();
    }

    public void MarkPreparedOutputPhysicalBatchCommitted(
        IEnumerable<ProductionPreparedOutputPhysicalCandidateSaveData> candidates)
    {
        RequirePreparedPhase(ProductionPreparedOutputPhase.PublicationPrepared);
        ProductionPreparedOutputBatchSaveData candidate = preparedOutput.Clone();
        candidate.phase =
            ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending;
        candidate.physicalCandidates = (candidates
                ?? throw new ArgumentNullException(nameof(candidates)))
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stackId, StringComparer.Ordinal)
            .ToList();
        ValidateAndPublishPrepared(candidate);
    }

    public void MarkPreparedOutputCompleted()
    {
        RequirePreparedPhase(
            ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending);
        ProductionPreparedOutputBatchSaveData candidate = preparedOutput.Clone();
        candidate.phase = ProductionPreparedOutputPhase.Completed;
        ValidateAndPublishPrepared(candidate);
    }

    public void ClearCompletedPreparedOutput()
    {
        RequirePreparedPhase(ProductionPreparedOutputPhase.Completed);
        preparedOutput = ProductionPreparedOutputBatchSaveData.Unresolved();
    }

    private void ValidateAndPublishPrepared(
        ProductionPreparedOutputBatchSaveData candidate)
    {
        ProductionPreparedOutputContract.ValidateForBill(
            candidate,
            billId,
            recipeId,
            cycleSequence,
            outputDestinationId);
        preparedOutput = candidate.Clone();
    }

    private void RequirePreparedPhase(ProductionPreparedOutputPhase expected)
    {
        if (preparedOutput == null || preparedOutput.phase != expected)
        {
            throw new InvalidOperationException(
                $"Prepared production output phase must be {expected}.");
        }
    }
    public void BeginResolvedOutputUnit(
        string outputLineId,
        string commitId)
    {
        ProductionResolvedOutputSaveData output = mutableResolvedOutputs.Single(
            candidate => string.Equals(
                candidate.outputLineId,
                outputLineId,
                StringComparison.Ordinal));
        if (output.committedAmount >= output.amount
            || !string.IsNullOrEmpty(output.pendingCommitId)
            || output.pendingCommitApplied
            || output.pendingOutputPublication == null
            || output.pendingOutputPublication.phase
                != ProductionExactOutputPublicationPhase.None
            || string.IsNullOrEmpty(commitId)
            || !string.Equals(commitId, commitId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved output '{outputLineId}' cannot begin commit '{commitId}'.");
        }
        output.pendingCommitId = commitId;
        output.pendingCommitApplied = false;
        output.pendingOutputPublication =
            ProductionExactOutputPublicationSaveData.Empty();
    }

    public void MarkResolvedOutputUnitCommitted(
        string outputLineId,
        string commitId,
        ProductionCommittedOutputSnapshot committedOutput)
    {
        ProductionResolvedOutputSaveData output = mutableResolvedOutputs.Single(
            candidate => string.Equals(
                candidate.outputLineId,
                outputLineId,
                StringComparison.Ordinal));
        if (output.committedAmount >= output.amount
            || output.pendingCommitApplied
            || committedOutput == null
            || committedOutput.ExactMassGrams <= 0L
            || !string.Equals(
                committedOutput.CommitId,
                commitId,
                StringComparison.Ordinal)
            || !string.Equals(
                committedOutput.OutputCapabilityId,
                output.outputCapabilityId,
                StringComparison.Ordinal)
            || committedOutput.OutputCapabilityVersion
                != output.outputCapabilityVersion
            || !string.Equals(
                committedOutput.OutputComponentCodecId,
                output.outputComponentCodecId,
                StringComparison.Ordinal)
            || committedOutput.OutputComponentCodecVersion
                != output.outputComponentCodecVersion
            || !string.Equals(
                output.pendingCommitId,
                commitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved output '{outputLineId}' is already fully committed.");
        }
        output.committedAmount++;
        output.committedMassGrams = checked(
            output.committedMassGrams + committedOutput.ExactMassGrams);
        output.pendingOutputPublication =
            ProductionExactOutputPublicationSaveData.FromRuntime(
                billId.Value,
                committedOutput);
        output.pendingCommitApplied = true;
    }

    public void ClearResolvedOutputPendingCommit(
        string outputLineId,
        string commitId)
    {
        ProductionResolvedOutputSaveData output = mutableResolvedOutputs.Single(
            candidate => string.Equals(
                candidate.outputLineId,
                outputLineId,
                StringComparison.Ordinal));
        if (!output.pendingCommitApplied
            || output.pendingOutputPublication == null
            || output.pendingOutputPublication.phase
                != ProductionExactOutputPublicationPhase.Published
            || !string.Equals(
                output.pendingCommitId,
                commitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved output '{outputLineId}' cannot acknowledge commit '{commitId}'.");
        }
        output.pendingCommitId = string.Empty;
        output.pendingCommitApplied = false;
        output.pendingOutputPublication =
            ProductionExactOutputPublicationSaveData.Empty();
    }

    public void AbortUnpublishedResolvedOutputUnit(
        string outputLineId,
        string commitId)
    {
        ProductionResolvedOutputSaveData output = mutableResolvedOutputs.Single(
            candidate => string.Equals(
                candidate.outputLineId,
                outputLineId,
                StringComparison.Ordinal));
        if (output.pendingCommitApplied
            || output.pendingOutputPublication == null
            || output.pendingOutputPublication.phase
                != ProductionExactOutputPublicationPhase.None
            || !string.Equals(
                output.pendingCommitId,
                commitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Resolved output '{outputLineId}' cannot abort unpublished commit '{commitId}'.");
        }
        output.pendingCommitId = string.Empty;
    }
    public void SetProcessFluidConsumed(bool value) => processFluidConsumed = value;
    public void SetProcessFluid(ProductionProcessFluidReceipt receipt)
    {
        processCleanWaterMassGrams = receipt.CleanWaterMassGrams;
        processWastewaterMassGrams = receipt.WastewaterMassGrams;
        mutableProcessWastewaterComponents.Clear();
        mutableProcessWastewaterComponents.AddRange(
            receipt.WastewaterComponents.Select(
                ProductionWastewaterComponentSaveData.FromRuntime));
        mutableProcessManualWaterTransfers.Clear();
        mutableProcessManualWaterTransfers.AddRange(
            receipt.ManualWaterTransfers.Select(value => value.Clone()));
    }
    public ProductionProcessFluidReceipt CaptureProcessFluidReceipt() => new(
        processCleanWaterMassGrams,
        processWastewaterMassGrams,
        mutableProcessManualWaterTransfers,
        mutableProcessWastewaterComponents
            .Select(value => value.ToRuntime())
            .ToArray());
    public void ClearProcessFluid()
    {
        processFluidConsumed = false;
        processCleanWaterMassGrams = 0L;
        processWastewaterMassGrams = 0L;
        mutableProcessWastewaterComponents.Clear();
        mutableProcessManualWaterTransfers.Clear();
    }
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
    public void SetEmergencyWorker(string id) =>
        emergencyWorkerId = id?.Trim() ?? string.Empty;
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
    internal List<ProductionWipTerminalReceiptSaveData> WipTerminalReceipts { get; } =
        new();
    internal HashSet<string> InstalledStockSensorFacilityIds { get; } =
        new(StringComparer.Ordinal);
    internal HashSet<string> AcknowledgedStockSensorFacilityIds { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, ProductionStockSensorPhysicalCommitSaveData>
        PendingStockSensorInstallsByFacilityId { get; } =
            new(StringComparer.Ordinal);
    internal Dictionary<string, ProductionInstalledStockSensorSaveData>
        InstalledStockSensorsByFacilityId { get; } =
            new(StringComparer.Ordinal);
    internal Dictionary<string, ProductionStockSensorRemovalSaveData>
        PendingStockSensorRemovalsByFacilityId { get; } =
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
    public IReadOnlyList<ProductionWipTerminalReceiptSaveData> WipTerminalReceipts =>
        Current.WipTerminalReceipts;
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
    public IReadOnlyCollection<ProductionStockSensorPhysicalCommitSaveData>
        PendingStockSensorInstalls =>
            Current.PendingStockSensorInstallsByFacilityId.Values;
    public IReadOnlyCollection<ProductionInstalledStockSensorSaveData>
        InstalledStockSensors =>
            Current.InstalledStockSensorsByFacilityId.Values;
    public IReadOnlyCollection<ProductionStockSensorRemovalSaveData>
        PendingStockSensorRemovals =>
            Current.PendingStockSensorRemovalsByFacilityId.Values;

    public void AddBill(ProductionBillRecord bill) =>
        Current.Bills.Add(bill ?? throw new ArgumentNullException(nameof(bill)));
    public bool RemoveBill(ProductionBillRecord bill) => Current.Bills.Remove(bill);
    public bool AddWipTerminalReceipt(ProductionWipTerminalReceiptSaveData receipt)
    {
        if (receipt == null || string.IsNullOrEmpty(receipt.commitId))
        {
            throw new ArgumentException(
                "Production WIP terminal receipt must have a commit ID.",
                nameof(receipt));
        }
        ProductionWipTerminalReceiptSaveData existing = Current.WipTerminalReceipts
            .FirstOrDefault(candidate => string.Equals(
                candidate.commitId,
                receipt.commitId,
                StringComparison.Ordinal));
        if (existing != null)
            return WipTerminalReceiptEquals(existing, receipt);
        Current.WipTerminalReceipts.Add(receipt.Clone());
        return true;
    }
    public bool TryRemoveWipTerminalReceiptExact(
        ProductionWipTerminalReceiptSaveData expected)
    {
        if (expected == null || string.IsNullOrEmpty(expected.commitId))
            return false;
        int index = Current.WipTerminalReceipts.FindIndex(value =>
            string.Equals(value?.commitId, expected.commitId,
                StringComparison.Ordinal));
        if (index < 0 || !WipTerminalReceiptEquals(
                Current.WipTerminalReceipts[index], expected))
            return false;
        Current.WipTerminalReceipts.RemoveAt(index);
        return true;
    }
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
    public bool TryRestoreBillVersionForCheckpointGc(
        int expectedCurrentVersion,
        int restoredVersion)
    {
        if (Current.BillVersion != expectedCurrentVersion
            || restoredVersion < 0
            || restoredVersion > expectedCurrentVersion)
            return false;
        Current.BillVersion = restoredVersion;
        return true;
    }
    public bool TryRestoreStockSensorVersionForCheckpointGc(
        int expectedCurrentVersion,
        int restoredVersion)
    {
        if (Current.StockSensorVersion != expectedCurrentVersion
            || restoredVersion < 0
            || restoredVersion > expectedCurrentVersion)
            return false;
        Current.StockSensorVersion = restoredVersion;
        return true;
    }
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
    public bool TryGetPendingStockSensorInstall(
        string facilityId,
        out ProductionStockSensorPhysicalCommitSaveData owner) =>
        Current.PendingStockSensorInstallsByFacilityId.TryGetValue(
            facilityId,
            out owner);
    public void SetPendingStockSensorInstall(
        ProductionStockSensorPhysicalCommitSaveData owner)
    {
        if (owner == null || string.IsNullOrEmpty(owner.facilityId))
            throw new ArgumentException(
                "Pending stock-sensor owner must have a facility ID.",
                nameof(owner));
        Current.PendingStockSensorInstallsByFacilityId[owner.facilityId] = owner;
    }
    public bool RemovePendingStockSensorInstall(string facilityId) =>
        Current.PendingStockSensorInstallsByFacilityId.Remove(facilityId);
    public bool TryGetInstalledStockSensor(
        string facilityId,
        out ProductionInstalledStockSensorSaveData installed) =>
        Current.InstalledStockSensorsByFacilityId.TryGetValue(
            facilityId,
            out installed);
    public void SetInstalledStockSensor(
        ProductionInstalledStockSensorSaveData installed)
    {
        if (installed == null || string.IsNullOrEmpty(installed.facilityId))
            throw new ArgumentException(
                "Installed stock-sensor record must have a facility ID.",
                nameof(installed));
        Current.InstalledStockSensorsByFacilityId[installed.facilityId] =
            installed;
    }
    public bool RemoveInstalledStockSensor(string facilityId) =>
        Current.InstalledStockSensorsByFacilityId.Remove(facilityId);
    public bool TryGetPendingStockSensorRemoval(
        string facilityId,
        out ProductionStockSensorRemovalSaveData owner) =>
        Current.PendingStockSensorRemovalsByFacilityId.TryGetValue(
            facilityId,
            out owner);
    public void SetPendingStockSensorRemoval(
        ProductionStockSensorRemovalSaveData owner)
    {
        if (owner == null || string.IsNullOrEmpty(owner.facilityId))
            throw new ArgumentException(
                "Pending stock-sensor removal must have a facility ID.",
                nameof(owner));
        Current.PendingStockSensorRemovalsByFacilityId[owner.facilityId] = owner;
    }
    public bool RemovePendingStockSensorRemoval(string facilityId) =>
        Current.PendingStockSensorRemovalsByFacilityId.Remove(facilityId);

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
        foreach (ProductionStockSensorPhysicalCommitSaveData owner in
                 snapshot.pendingStockSensorInstalls)
        {
            restored.PendingStockSensorInstallsByFacilityId.Add(
                owner.facilityId,
                owner.Clone());
        }
        foreach (ProductionInstalledStockSensorSaveData installed in
                 snapshot.installedStockSensors)
        {
            restored.InstalledStockSensorsByFacilityId.Add(
                installed.facilityId,
                installed.Clone());
        }
        foreach (ProductionStockSensorRemovalSaveData owner in
                 snapshot.pendingStockSensorRemovals)
        {
            restored.PendingStockSensorRemovalsByFacilityId.Add(
                owner.facilityId,
                owner.Clone());
        }
        restored.WipTerminalReceipts.AddRange(
            snapshot.wipTerminalReceipts.Select(receipt => receipt.Clone()));
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
                cycleSequence = saved.cycleSequence,
                wipInputCommitId = saved.wipInputCommitId,
                wipInputQuantity = saved.wipInputQuantity,
                wipInputMassGrams = saved.wipInputMassGrams,
                outputOutcomeResolved = saved.outputOutcomeResolved,
                preparedOutput = saved.preparedOutput?.Clone()
                    ?? throw new InvalidOperationException(
                        $"Production bill '{saved.billId}' has no prepared-output payload."),
                processFluidConsumed = saved.processFluidConsumed,
                processCleanWaterMassGrams = saved.processCleanWaterMassGrams,
                processWastewaterMassGrams = saved.processWastewaterMassGrams,
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
                emergencyWorkerId = saved.emergencyWorkerId?.Trim()
                    ?? string.Empty,
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
            record.mutableResolvedOutputs.AddRange(
                saved.resolvedOutputs.Select(output => output.Clone()));
            record.mutableProcessWastewaterComponents.AddRange(
                (saved.processWastewaterComponents
                    ?? new List<ProductionWastewaterComponentSaveData>())
                .Select(value => value.Clone()));
            record.mutableProcessManualWaterTransfers.AddRange(
                (saved.processManualWaterTransfers
                    ?? new List<ProductionManualWaterTransferSaveData>())
                .Select(value => value.Clone()));
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

    private static bool WastewaterComponentsEqual(
        IReadOnlyList<ProductionWastewaterComponentSaveData> left,
        IReadOnlyList<ProductionWastewaterComponentSaveData> right)
    {
        left ??= Array.Empty<ProductionWastewaterComponentSaveData>();
        right ??= Array.Empty<ProductionWastewaterComponentSaveData>();
        return left.Count == right.Count
            && left.Zip(right, (a, b) => a != null && b != null
                && a.composition == b.composition
                && a.sourceKind == b.sourceKind
                && string.Equals(
                    a.sourceStableId,
                    b.sourceStableId,
                    StringComparison.Ordinal)
                && a.authoredUnits.Equals(b.authoredUnits)
                && a.massGrams == b.massGrams).All(value => value);
    }

    private static bool WipTerminalReceiptEquals(
        ProductionWipTerminalReceiptSaveData left,
        ProductionWipTerminalReceiptSaveData right) => left != null
        && right != null
        && string.Equals(left.commitId, right.commitId, StringComparison.Ordinal)
        && string.Equals(left.billId, right.billId, StringComparison.Ordinal)
        && string.Equals(left.recipeId, right.recipeId, StringComparison.Ordinal)
        && string.Equals(
            left.buildingInstanceId,
            right.buildingInstanceId,
            StringComparison.Ordinal)
        && left.cycleSequence == right.cycleSequence
        && string.Equals(
            left.inputCommitId,
            right.inputCommitId,
            StringComparison.Ordinal)
        && left.inputQuantity == right.inputQuantity
        && left.inputMassGrams == right.inputMassGrams
        && left.processCleanWaterMassGrams == right.processCleanWaterMassGrams
        && left.processWastewaterMassGrams == right.processWastewaterMassGrams
        && WastewaterComponentsEqual(
            left.wastewaterComponents,
            right.wastewaterComponents)
        && left.committedOutputMassGrams == right.committedOutputMassGrams
        && left.declaredLossMassGrams == right.declaredLossMassGrams
        && left.reason == right.reason
        && left.lossKind == right.lossKind;
}
