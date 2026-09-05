using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionAggregateStateStore
{
    private readonly ProductionAggregateStateSession session;

    public ProductionAggregateStateStore(
        DungeonRuntimeAggregateRootStore rootStore)
    {
        session = new ProductionAggregateStateSession(rootStore);
    }

    public IReadOnlyList<ProductionBillRecord> Bills => session.Bills;
    internal IReadOnlyList<ProductionWipTerminalReceiptSaveData> WipTerminalReceipts =>
        session.WipTerminalReceipts;
    internal int NextBillSequence
    {
        get => session.NextBillSequence;
        set => session.NextBillSequence = value;
    }
    internal int BillVersion => session.BillVersion;
    internal int StockSensorVersion => session.StockSensorVersion;
    internal IReadOnlyCollection<string> InstalledStockSensorFacilityIds =>
        session.InstalledStockSensorFacilityIds;
    internal IReadOnlyCollection<string> AcknowledgedStockSensorFacilityIds =>
        session.AcknowledgedStockSensorFacilityIds;
    internal IReadOnlyCollection<ProductionStockSensorPhysicalCommitSaveData>
        PendingStockSensorInstalls => session.PendingStockSensorInstalls;
    internal IReadOnlyCollection<ProductionInstalledStockSensorSaveData>
        InstalledStockSensors => session.InstalledStockSensors;
    internal IReadOnlyCollection<ProductionStockSensorRemovalSaveData>
        PendingStockSensorRemovals => session.PendingStockSensorRemovals;
    internal void AddBill(ProductionBillRecord bill) => session.AddBill(bill);
    internal bool RemoveBill(ProductionBillRecord bill) => session.RemoveBill(bill);
    internal bool AddWipTerminalReceipt(
        ProductionWipTerminalReceiptSaveData receipt) =>
        session.AddWipTerminalReceipt(receipt);
    internal bool TryRemoveWipTerminalReceiptExact(
        ProductionWipTerminalReceiptSaveData receipt) =>
        session.TryRemoveWipTerminalReceiptExact(receipt);
    internal void MoveBill(
        ProductionBillRecord bill,
        ProductionBillRecord anchor,
        bool insertAfter) => session.MoveBill(bill, anchor, insertAfter);
    internal void IncrementBillVersion() => session.IncrementBillVersion();
    internal void IncrementStockSensorVersion() => session.IncrementStockSensorVersion();
    internal bool TryRestoreBillVersionForCheckpointGc(
        int expectedCurrentVersion,
        int restoredVersion) => session.TryRestoreBillVersionForCheckpointGc(
        expectedCurrentVersion,
        restoredVersion);
    internal bool TryRestoreStockSensorVersionForCheckpointGc(
        int expectedCurrentVersion,
        int restoredVersion) => session
        .TryRestoreStockSensorVersionForCheckpointGc(
            expectedCurrentVersion,
            restoredVersion);
    internal bool HasInstalledSensor(string id) => session.HasInstalledSensor(id);
    internal bool HasAcknowledgedSensor(string id) => session.HasAcknowledgedSensor(id);
    internal bool AddInstalledSensor(string id) => session.AddInstalledSensor(id);
    internal bool RemoveInstalledSensor(string id) => session.RemoveInstalledSensor(id);
    internal bool AddAcknowledgedSensor(string id) => session.AddAcknowledgedSensor(id);
    internal bool RemoveAcknowledgedSensor(string id) => session.RemoveAcknowledgedSensor(id);
    internal bool TryGetPendingStockSensorInstall(
        string facilityId,
        out ProductionStockSensorPhysicalCommitSaveData owner) =>
        session.TryGetPendingStockSensorInstall(facilityId, out owner);
    internal void SetPendingStockSensorInstall(
        ProductionStockSensorPhysicalCommitSaveData owner) =>
        session.SetPendingStockSensorInstall(owner);
    internal bool RemovePendingStockSensorInstall(string facilityId) =>
        session.RemovePendingStockSensorInstall(facilityId);
    internal bool TryGetInstalledStockSensor(
        string facilityId,
        out ProductionInstalledStockSensorSaveData installed) =>
        session.TryGetInstalledStockSensor(facilityId, out installed);
    internal void SetInstalledStockSensor(
        ProductionInstalledStockSensorSaveData installed) =>
        session.SetInstalledStockSensor(installed);
    internal bool RemoveInstalledStockSensor(string facilityId) =>
        session.RemoveInstalledStockSensor(facilityId);
    internal bool TryGetPendingStockSensorRemoval(
        string facilityId,
        out ProductionStockSensorRemovalSaveData owner) =>
        session.TryGetPendingStockSensorRemoval(facilityId, out owner);
    internal void SetPendingStockSensorRemoval(
        ProductionStockSensorRemovalSaveData owner) =>
        session.SetPendingStockSensorRemoval(owner);
    internal bool RemovePendingStockSensorRemoval(string facilityId) =>
        session.RemovePendingStockSensorRemoval(facilityId);
    internal void Replace(ProductionBillRestoreCandidate candidate) =>
        session.Restore(candidate);
}

internal static class ProductionBillStateCodec
{
    private const int MaximumPrefetchBatches = 3;

    internal static DungeonProductionBillSaveData Capture(
        int nextBillSequence,
        IEnumerable<ProductionBillRecord> bills,
        IEnumerable<string> installedStockSensorFacilityIds,
        IEnumerable<string> acknowledgedStockSensorFacilityIds,
        IEnumerable<ProductionStockSensorPhysicalCommitSaveData>
            pendingStockSensorInstalls,
        IEnumerable<ProductionInstalledStockSensorSaveData>
            installedStockSensors,
        IEnumerable<ProductionStockSensorRemovalSaveData>
            pendingStockSensorRemovals,
        IEnumerable<ProductionWipTerminalReceiptSaveData> wipTerminalReceipts)
    {
        return new DungeonProductionBillSaveData
        {
            nextBillSequence = nextBillSequence,
            bills = (bills ?? Array.Empty<ProductionBillRecord>())
                .Select(ToSaveData)
                .ToList(),
            installedStockSensorFacilityIds = CanonicalIds(
                installedStockSensorFacilityIds),
            acknowledgedStockSensorFacilityIds = CanonicalIds(
                acknowledgedStockSensorFacilityIds),
            pendingStockSensorInstalls = (pendingStockSensorInstalls
                    ?? Array.Empty<ProductionStockSensorPhysicalCommitSaveData>())
                .OrderBy(owner => owner.facilityId, StringComparer.Ordinal)
                .Select(owner => owner.Clone())
                .ToList(),
            installedStockSensors = (installedStockSensors
                    ?? Array.Empty<ProductionInstalledStockSensorSaveData>())
                .OrderBy(owner => owner.facilityId, StringComparer.Ordinal)
                .Select(owner => owner.Clone())
                .ToList(),
            pendingStockSensorRemovals = (pendingStockSensorRemovals
                    ?? Array.Empty<ProductionStockSensorRemovalSaveData>())
                .OrderBy(owner => owner.facilityId, StringComparer.Ordinal)
                .Select(owner => owner.Clone())
                .ToList(),
            wipTerminalReceipts = (wipTerminalReceipts
                    ?? Array.Empty<ProductionWipTerminalReceiptSaveData>())
                .OrderBy(receipt => receipt.commitId, StringComparer.Ordinal)
                .Select(receipt => receipt.Clone())
                .ToList()
        };
    }

    internal static ProductionBillRestoreCandidate CreateRestoreCandidate(
        DungeonProductionBillSaveData snapshot,
        IResourceEconomyContentCatalog catalog,
        int nextBillVersion,
        int nextStockSensorVersion)
    {
        Validate(snapshot, catalog);
        return ProductionBillRestoreCandidate.Create(
            snapshot,
            nextBillVersion,
            nextStockSensorVersion);
    }

    internal static void Validate(
        DungeonProductionBillSaveData snapshot,
        IResourceEconomyContentCatalog catalog)
    {
        if (snapshot == null)
        {
            throw new InvalidOperationException(
                "Production-bill payload is null.");
        }
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }
        if (snapshot.version != DungeonProductionBillSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Production-bill payload version {snapshot.version} is unsupported.");
        }
        if (snapshot.nextBillSequence <= 0
            || snapshot.bills == null
            || snapshot.installedStockSensorFacilityIds == null
            || snapshot.acknowledgedStockSensorFacilityIds == null
            || snapshot.pendingStockSensorInstalls == null
            || snapshot.installedStockSensors == null
            || snapshot.pendingStockSensorRemovals == null
            || snapshot.wipTerminalReceipts == null)
        {
            throw new InvalidOperationException(
                "Production-bill payload has missing collections or an invalid next sequence.");
        }

        ValidateCanonicalBuildingIds(
            snapshot.installedStockSensorFacilityIds,
            "installed stock sensors");
        ValidateCanonicalBuildingIds(
            snapshot.acknowledgedStockSensorFacilityIds,
            "acknowledged stock sensors");
        HashSet<string> installed = new(
            snapshot.installedStockSensorFacilityIds,
            StringComparer.Ordinal);
        HashSet<string> acknowledged = new(
            snapshot.acknowledgedStockSensorFacilityIds,
            StringComparer.Ordinal);
        if (snapshot.acknowledgedStockSensorFacilityIds.Any(
                id => !installed.Contains(id)))
        {
            throw new InvalidOperationException(
                "Acknowledged stock sensors must be a subset of installed sensors.");
        }
        ValidatePendingStockSensorInstalls(
            snapshot.pendingStockSensorInstalls,
            installed);
        ValidateInstalledStockSensors(
            snapshot.installedStockSensors,
            installed);
        ValidatePendingStockSensorRemovals(
            snapshot.pendingStockSensorRemovals,
            snapshot.installedStockSensors,
            installed,
            acknowledged);
        ValidateStockSensorOwnershipCrossLinks(
            snapshot.pendingStockSensorInstalls,
            snapshot.installedStockSensors,
            snapshot.pendingStockSensorRemovals);

        HashSet<ProductionBillId> billIds = new();
        int largestSequence = 0;
        foreach (ProductionBillSaveData saved in snapshot.bills)
        {
            ValidateBill(saved, catalog, billIds, ref largestSequence);
        }
        if (snapshot.nextBillSequence <= largestSequence)
        {
            throw new InvalidOperationException(
                "Production-bill next sequence collides with a persisted bill ID.");
        }
        ValidateWipTerminalReceipts(snapshot.wipTerminalReceipts, catalog);
    }

    private static void ValidatePendingStockSensorInstalls(
        IReadOnlyList<ProductionStockSensorPhysicalCommitSaveData> owners,
        ISet<string> installed)
    {
        string previousFacilityId = string.Empty;
        foreach (ProductionStockSensorPhysicalCommitSaveData owner in owners)
        {
            bool phaseMatchesInstalled = owner != null
                && (owner.phase == ProductionStockSensorCommitPhase.InputCommitted
                    && !installed.Contains(owner.facilityId)
                    || owner.phase == ProductionStockSensorCommitPhase.OutcomePublished
                    && installed.Contains(owner.facilityId));
            string expectedDestination = owner == null
                ? string.Empty
                : ProductionStockSensorRuntime.BuildDestinationId(
                    owner.facilityId);
            bool canonicalSources = owner?.sourceStackIds != null
                && owner.sourceStackIds.Count == 1
                && owner.sourceStackIds.All(IsCanonical)
                && owner.sourceStackIds.SequenceEqual(
                    owner.sourceStackIds.OrderBy(id => id, StringComparer.Ordinal),
                    StringComparer.Ordinal)
                && owner.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                    == owner.sourceStackIds.Count;
            if (owner == null
                || !phaseMatchesInstalled
                || !IsCanonical(owner.facilityId)
                || !IsCanonical(owner.itemId)
                || !string.Equals(owner.destinationId, expectedDestination, StringComparison.Ordinal)
                || !ProductionStockSensorRuntime.IsPhysicalOperationIdForFacility(
                    owner.operationId,
                    owner.facilityId)
                || !string.Equals(
                    owner.reasonCode,
                    ProductionStockSensorRuntime.PhysicalReasonCode,
                    StringComparison.Ordinal)
                || !IsCanonical(owner.requestFingerprint)
                || owner.inputQuantity != 1
                || owner.inputMassGrams <= 0L
                || !string.Equals(
                    owner.commitId,
                    $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Sink}:{owner.operationId}:1:{owner.inputMassGrams}",
                    StringComparison.Ordinal)
                || !canonicalSources
                || previousFacilityId.Length > 0
                    && string.CompareOrdinal(previousFacilityId, owner.facilityId) >= 0)
                throw new InvalidOperationException(
                    "Production stock-sensor physical owner is invalid, unordered, or inconsistent with installed state.");
            previousFacilityId = owner.facilityId;
        }
    }

    private static void ValidateInstalledStockSensors(
        IReadOnlyList<ProductionInstalledStockSensorSaveData> records,
        ISet<string> installed)
    {
        string previousFacilityId = string.Empty;
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (ProductionInstalledStockSensorSaveData record in records)
        {
            if (record == null
                || !IsCanonical(record.facilityId)
                || !IsCanonical(record.itemId)
                || !ProductionStockSensorRuntime.IsPhysicalOperationIdForFacility(
                    record.inputOperationId,
                    record.facilityId)
                || !IsCanonical(record.inputCommitId)
                || !IsCanonical(record.inputSourceStackId)
                || !string.Equals(
                    record.inputCommitId,
                    $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Sink}:{record.inputOperationId}:1:{record.embeddedMassGrams}",
                    StringComparison.Ordinal)
                || record.embeddedMassGrams <= 0L
                || !recordIds.Add(record.facilityId)
                || previousFacilityId.Length > 0
                    && string.CompareOrdinal(
                        previousFacilityId,
                        record.facilityId) >= 0)
            {
                throw new InvalidOperationException(
                    "Installed stock-sensor mass record is invalid or unordered.");
            }
            previousFacilityId = record.facilityId;
        }
        if (recordIds.Count != installed.Count
            || installed.Any(id => !recordIds.Contains(id)))
        {
            throw new InvalidOperationException(
                "Installed stock-sensor IDs and embedded-mass records must be bijective.");
        }
    }

    private static void ValidateStockSensorOwnershipCrossLinks(
        IReadOnlyList<ProductionStockSensorPhysicalCommitSaveData> installs,
        IReadOnlyList<ProductionInstalledStockSensorSaveData> installed,
        IReadOnlyList<ProductionStockSensorRemovalSaveData> removals)
    {
        Dictionary<string, ProductionInstalledStockSensorSaveData> records =
            installed.ToDictionary(value => value.facilityId, StringComparer.Ordinal);
        HashSet<string> removalIds = removals
            .Select(value => value.facilityId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (ProductionStockSensorPhysicalCommitSaveData owner in installs)
        {
            if (removalIds.Contains(owner.facilityId))
            {
                throw new InvalidOperationException(
                    "A stock sensor cannot be pending installation and removal together.");
            }
            if (owner.phase == ProductionStockSensorCommitPhase.OutcomePublished
                && (!records.TryGetValue(
                        owner.facilityId,
                        out ProductionInstalledStockSensorSaveData record)
                    || !string.Equals(
                        record.itemId,
                        owner.itemId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        record.inputOperationId,
                        owner.operationId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        record.inputCommitId,
                        owner.commitId,
                        StringComparison.Ordinal)
                    || owner.sourceStackIds.Count != 1
                    || !string.Equals(
                        record.inputSourceStackId,
                        owner.sourceStackIds[0],
                        StringComparison.Ordinal)
                    || record.embeddedMassGrams != owner.inputMassGrams))
            {
                throw new InvalidOperationException(
                    "Published stock-sensor install has no exact embedded-mass record.");
            }
        }
    }

    private static void ValidatePendingStockSensorRemovals(
        IReadOnlyList<ProductionStockSensorRemovalSaveData> owners,
        IReadOnlyList<ProductionInstalledStockSensorSaveData> installedRecords,
        ISet<string> installed,
        ISet<string> acknowledged)
    {
        Dictionary<string, ProductionInstalledStockSensorSaveData> records =
            installedRecords.ToDictionary(
                value => value.facilityId,
                StringComparer.Ordinal);
        string previousFacilityId = string.Empty;
        foreach (ProductionStockSensorRemovalSaveData owner in owners)
        {
            if (owner == null
                || !Enum.IsDefined(
                    typeof(ProductionStockSensorRemovalPhase),
                    owner.phase))
            {
                throw new InvalidOperationException(
                    "Pending stock-sensor removal has an invalid phase.");
            }

            bool prepared = owner.phase ==
                ProductionStockSensorRemovalPhase.Prepared;
            bool terminal = owner.phase == ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
            bool canonicalCommits = owner.outputCommitIds != null
                && (prepared
                    ? owner.outputCommitIds.Count == 0
                    : owner.outputCommitIds.Count == 1
                        && owner.outputCommitIds.All(IsCanonical)
                        && owner.outputCommitIds.Distinct(
                            StringComparer.Ordinal).Count() == 1);
            bool sourceStateValid;
            if (terminal)
            {
                sourceStateValid = !installed.Contains(owner.facilityId)
                    && !acknowledged.Contains(owner.facilityId)
                    && !records.ContainsKey(owner.facilityId);
            }
            else
            {
                sourceStateValid = installed.Contains(owner.facilityId)
                    && records.TryGetValue(
                        owner.facilityId,
                        out ProductionInstalledStockSensorSaveData installedRecord)
                    && string.Equals(
                        owner.itemId,
                        installedRecord.itemId,
                        StringComparison.Ordinal)
                    && owner.expectedOutputMassGrams
                        == installedRecord.embeddedMassGrams
                    && string.Equals(
                        owner.installationSourceStackId,
                        installedRecord.inputSourceStackId,
                        StringComparison.Ordinal);
            }

            bool outputStateValid = prepared
                ? owner.outputQuantity == 0 && owner.outputMassGrams == 0L
                : owner.outputCommitIds != null
                    && owner.outputCommitIds.Count == 1
                    && owner.outputQuantity == 1
                    && owner.outputMassGrams == owner.expectedOutputMassGrams
                    && string.Equals(
                        owner.outputCommitIds[0],
                        ProductionStockSensorRuntime.BuildRemovalOutputCommitId(
                            owner),
                        StringComparison.Ordinal);
            if (!IsCanonical(owner.facilityId)
                || !IsCanonical(owner.itemId)
                || !sourceStateValid
                || !string.Equals(
                    owner.operationId,
                    ProductionStockSensorRuntime.BuildRemovalOperationId(
                        owner.facilityId,
                        owner.installationSourceStackId),
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.reasonCode,
                    ProductionStockSensorRuntime.RemovalReasonCode,
                    StringComparison.Ordinal)
                || !canonicalCommits
                || !outputStateValid
                || previousFacilityId.Length > 0
                    && string.CompareOrdinal(
                        previousFacilityId,
                        owner.facilityId) >= 0)
            {
                throw new InvalidOperationException(
                    "Pending stock-sensor removal is invalid or inconsistent with installed mass.");
            }
            previousFacilityId = owner.facilityId;
        }
    }

    private static void ValidateWipTerminalReceipts(
        IReadOnlyList<ProductionWipTerminalReceiptSaveData> receipts,
        IResourceEconomyContentCatalog catalog)
    {
        if (receipts.Count > 16384)
        {
            throw new InvalidOperationException(
                "Production WIP terminal receipt count exceeds the current-format limit.");
        }
        string previous = string.Empty;
        foreach (ProductionWipTerminalReceiptSaveData receipt in receipts)
        {
            bool hasPhysicalInput = IsCanonical(receipt?.inputCommitId)
                && receipt.inputQuantity > 0
                && receipt.inputMassGrams > 0L;
            bool hasNoPhysicalInput = receipt != null
                && string.IsNullOrEmpty(receipt.inputCommitId)
                && receipt.inputQuantity == 0
                && receipt.inputMassGrams == 0L;
            if (receipt == null
                || !IsCanonical(receipt.commitId)
                || !IsCanonical(receipt.billId)
                || !IsCanonical(receipt.recipeId)
                || !IsCanonical(receipt.buildingInstanceId)
                || receipt.cycleSequence <= 0
                || !hasPhysicalInput && !hasNoPhysicalInput
                || !HasValidTerminalMassEquation(receipt)
                || !HasValidWastewaterComponents(
                    receipt.wastewaterComponents,
                    receipt.processWastewaterMassGrams)
                || receipt.lossKind
                    != ProductionWipTerminalLossKind.ExplicitIrrecoverableProcessLoss
                || !Enum.IsDefined(typeof(ProductionWipTerminalReason), receipt.reason)
                || !catalog.TryGetRecipe(receipt.recipeId, out _)
                || !string.Equals(
                    receipt.commitId,
                    BuildWipTerminalCommitId(
                        receipt.billId,
                        receipt.cycleSequence,
                        receipt.reason),
                    StringComparison.Ordinal)
                || previous.Length > 0
                    && string.CompareOrdinal(previous, receipt.commitId) >= 0)
            {
                throw new InvalidOperationException(
                    "Production WIP terminal receipt is invalid or non-canonical.");
            }
            previous = receipt.commitId;
        }
    }

    private static bool HasValidTerminalMassEquation(
        ProductionWipTerminalReceiptSaveData receipt)
    {
        if (receipt.processCleanWaterMassGrams < 0L
            || receipt.processWastewaterMassGrams < 0L
            || receipt.committedOutputMassGrams < 0L
            || receipt.declaredLossMassGrams < 0L)
        {
            return false;
        }
        try
        {
            long available = checked(
                receipt.inputMassGrams
                + receipt.processCleanWaterMassGrams);
            long accounted = checked(
                receipt.committedOutputMassGrams
                + receipt.processWastewaterMassGrams
                + receipt.declaredLossMassGrams);
            return available > 0L && available == accounted;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static void ValidateWastewaterComponents(
        IReadOnlyList<ProductionWastewaterComponentSaveData> components,
        long expectedMassGrams,
        string owner)
    {
        if (!HasValidWastewaterComponents(components, expectedMassGrams))
        {
            throw new InvalidOperationException(
                $"{owner} has invalid wastewater composition provenance.");
        }
    }

    private static bool HasValidWastewaterComponents(
        IReadOnlyList<ProductionWastewaterComponentSaveData> components,
        long expectedMassGrams)
    {
        if (components == null || expectedMassGrams < 0L)
        {
            return false;
        }
        if ((expectedMassGrams == 0L) != (components.Count == 0))
        {
            return false;
        }

        long total = 0L;
        string previousKey = string.Empty;
        foreach (ProductionWastewaterComponentSaveData component in components)
        {
            if (component == null
                || component.composition == ProcessWastewaterComposition.None
                || !Enum.IsDefined(
                    typeof(ProcessWastewaterComposition),
                    component.composition)
                || !Enum.IsDefined(
                    typeof(ProcessWastewaterSourceKind),
                    component.sourceKind)
                || !IsCanonical(component.sourceStableId)
                || !IsFiniteNonNegative(component.authoredUnits)
                || component.authoredUnits <= 0f
                || component.massGrams <= 0L
                || component.massGrams
                    != ProductionFluidMassRules.ToMassGrams(component.authoredUnits))
            {
                return false;
            }
            string key = $"{(int)component.composition:D3}:"
                + $"{(int)component.sourceKind:D3}:{component.sourceStableId}";
            if (previousKey.Length > 0
                && string.CompareOrdinal(previousKey, key) >= 0)
            {
                return false;
            }
            previousKey = key;
            try
            {
                total = checked(total + component.massGrams);
            }
            catch (OverflowException)
            {
                return false;
            }
        }
        return total == expectedMassGrams;
    }

    internal static string BuildWipTerminalCommitId(
        string billId,
        int cycleSequence,
        ProductionWipTerminalReason reason) =>
        $"production-wip-terminal:{billId}:{cycleSequence:D8}:{reason.ToString().ToLowerInvariant()}";

    internal static ProductionBillSaveData ToSaveData(ProductionBillRecord record)
    {
        return new ProductionBillSaveData
        {
            billId = record.billId.Value,
            recipeId = record.recipeId,
            buildingInstanceId = record.buildingInstanceId.Value,
            mode = record.mode,
            remainingCycles = record.remainingCycles,
            targetStock = record.targetStock,
            minimumReserve = record.minimumReserve,
            suspended = record.suspended,
            materialsConsumed = record.materialsConsumed,
            cycleSequence = record.cycleSequence,
            wipInputCommitId = record.wipInputCommitId,
            wipInputQuantity = record.wipInputQuantity,
            wipInputMassGrams = record.wipInputMassGrams,
            outputOutcomeResolved = record.outputOutcomeResolved,
            resolvedOutputs = record.resolvedOutputs
                .OrderBy(output => output.outputLineId, StringComparer.Ordinal)
                .Select(output => output.Clone())
                .ToList(),
            preparedOutput = record.preparedOutput?.Clone()
                ?? throw new InvalidOperationException(
                    $"Production bill '{record.billId}' has no prepared-output authority."),
            processFluidConsumed = record.processFluidConsumed,
            processCleanWaterMassGrams = record.processCleanWaterMassGrams,
            processWastewaterMassGrams = record.processWastewaterMassGrams,
            processWastewaterComponents = record.processWastewaterComponents
                .OrderBy(value => (int)value.composition)
                .ThenBy(value => (int)value.sourceKind)
                .ThenBy(value => value.sourceStableId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToList(),
            processManualWaterTransfers = record.processManualWaterTransfers
                .OrderBy(value => value.operationId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToList(),
            completedWork = record.completedWork,
            batchStage = record.batchStage,
            remainingProcessingHours = record.remainingProcessingHours,
            batchIntegrity = record.batchIntegrity,
            utilityOutageHours = record.utilityOutageHours,
            temperatureOutageHours = record.temperatureOutageHours,
            occupiedSupportNodeId = record.occupiedSupportNodeId,
            blocked = CaptureFailure(record.blockedFailure),
            reservedWorkerId = string.Empty,
            materialDestinationId = record.materialDestinationId,
            prefetchBatchCount = record.prefetchBatchCount,
            estimatedDeliverySeconds = record.estimatedDeliverySeconds,
            estimatedProductionCycleSeconds =
                record.estimatedProductionCycleSeconds,
            logistics = CaptureLogistics(record.logisticsStatus),
            allowedMaterialIds = record.allowedMaterialIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            allowedWorkerIds = record.allowedWorkerIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            workerPolicy = record.workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(),
            emergencyWorkerId = record.emergencyWorkerId,
            workerContributions = record.workerContributions
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList(),
            hasPendingModeTransition = record.hasPendingModeTransition,
            pendingMode = record.pendingMode,
            outputDestinationId = record.outputDestinationId,
            outputReservations = record.outputReservations
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ProductionOutputReservationSaveData
                {
                    itemId = pair.Key,
                    amount = pair.Value
                })
                .ToList(),
            distributionMode = record.distributionMode,
            routePolicies = record.routePolicies
                .OrderBy(route => route.consumerId, StringComparer.Ordinal)
                .Select(route => route.Clone())
                .ToList(),
            selectedSupplies = record.selectedSupplies
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ProductionSelectedSupplySaveData
                {
                    supplyKey = pair.Key,
                    itemId = pair.Value
                })
                .ToList()
        };
    }

    private static void ValidateBill(
        ProductionBillSaveData saved,
        IResourceEconomyContentCatalog catalog,
        ISet<ProductionBillId> billIds,
        ref int largestSequence)
    {
        if (saved == null)
        {
            throw new InvalidOperationException(
                "Production-bill payload contains a null bill.");
        }
        string rawBillId = saved.billId ?? string.Empty;
        string rawBuildingId = saved.buildingInstanceId ?? string.Empty;
        ProductionBillId billId = (ProductionBillId)rawBillId;
        BuildingInstanceId buildingId =
            (BuildingInstanceId)rawBuildingId;
        if (!billId.IsValid
            || !string.Equals(
                billId.Value,
                rawBillId,
                StringComparison.Ordinal)
            || !TryParseSequence(billId, out int sequence)
            || !billIds.Add(billId)
            || !buildingId.IsValid
            || !string.Equals(
                buildingId.Value,
                rawBuildingId,
                StringComparison.Ordinal)
            || !IsCanonical(saved.recipeId)
            || !catalog.TryGetRecipe(saved.recipeId, out ProductionRecipeSO recipe))
        {
            throw new InvalidOperationException(
                "Production-bill payload contains an invalid/duplicate bill, building, or recipe ID.");
        }
        largestSequence = Math.Max(largestSequence, sequence);
        if (!Enum.IsDefined(typeof(ProductionOrderMode), saved.mode)
            || !Enum.IsDefined(typeof(ProductionBatchStage), saved.batchStage)
            || !Enum.IsDefined(
                typeof(ProductionDistributionMode),
                saved.distributionMode)
            || saved.remainingCycles < -1
            || saved.cycleSequence < 1
            || saved.targetStock < 0
            || saved.minimumReserve < 0
            || saved.minimumReserve > saved.targetStock
            || !IsFiniteNonNegative(saved.completedWork)
            || !IsFiniteNonNegative(saved.remainingProcessingHours)
            || !IsFiniteInRange(saved.batchIntegrity, 0f, 100f)
            || !IsFiniteNonNegative(saved.utilityOutageHours)
            || !IsFiniteNonNegative(saved.temperatureOutageHours)
            || saved.prefetchBatchCount < 1
            || saved.prefetchBatchCount > MaximumPrefetchBatches
            || !IsFinitePositive(saved.estimatedDeliverySeconds)
            || !IsFiniteNonNegative(saved.estimatedProductionCycleSeconds))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' contains invalid scalar state.");
        }
        if (!string.Equals(
                saved.materialDestinationId,
                ProductionBillRuntime.DestinationPrefix + billId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                saved.outputDestinationId,
                ProductionBillRuntime.OutputDestinationPrefix + buildingId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' contains a non-canonical destination.");
        }
        ValidateStatus(saved.blocked, failure: true, billId.Value);
        ValidateStatus(saved.logistics, failure: false, billId.Value);
        ValidateCanonicalStrings(saved.allowedMaterialIds, "allowed material IDs");
        ValidateCanonicalStrings(saved.allowedWorkerIds, "allowed worker IDs");
        ValidateWorkerPolicy(saved.workerPolicy, billId);
        if (!string.IsNullOrEmpty(saved.emergencyWorkerId)
            && (!IsCanonical(saved.emergencyWorkerId)
                || saved.workerPolicy == null
                || saved.workerPolicy.mode != WorkerSelectionMode.SpecificCharacters
                || saved.workerPolicy.specificCharacterIds == null
                || saved.workerPolicy.specificCharacterIds.Count != 1
                || !string.Equals(
                    saved.workerPolicy.specificCharacterIds[0],
                    saved.emergencyWorkerId,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has an invalid emergency worker authority.");
        }
        ValidateWorkerContributions(saved.workerContributions, billId);
        bool recipeHasPhysicalInputs = recipe.Inputs.Any(input =>
            input != null && input.Amount > 0);
        bool hasWipReceipt = IsCanonical(saved.wipInputCommitId)
            && saved.wipInputQuantity > 0
            && saved.wipInputMassGrams > 0L;
        if (saved.materialsConsumed
                ? recipeHasPhysicalInputs != hasWipReceipt
                : saved.wipInputCommitId.Length != 0
                    || saved.wipInputQuantity != 0
                    || saved.wipInputMassGrams != 0L)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has inconsistent WIP input authority.");
        }
        ValidateResolvedOutputs(saved, recipe, billId);
        ValidatePreparedOutput(saved, recipe, catalog, billId);
        if (saved.processCleanWaterMassGrams < 0L
            || saved.processWastewaterMassGrams < 0L
            || saved.processWastewaterComponents == null
            || saved.processManualWaterTransfers == null
            || !saved.processFluidConsumed
                && (saved.processCleanWaterMassGrams != 0L
                    || saved.processWastewaterMassGrams != 0L
                    || saved.processWastewaterComponents.Count != 0
                    || saved.processManualWaterTransfers.Count != 0)
            || saved.processFluidConsumed && !saved.materialsConsumed)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has inconsistent process-fluid mass authority.");
        }
        ValidateWastewaterComponents(
            saved.processWastewaterComponents,
            saved.processWastewaterMassGrams,
            $"Production bill '{billId}'");
        string previousManualOperation = string.Empty;
        foreach (ProductionManualWaterTransferSaveData transfer in
                 saved.processManualWaterTransfers)
        {
            bool hasPhysicalTransfer = transfer != null
                && transfer.transferredWaterUnits > 0
                && IsCanonical(transfer.physicalCommitId)
                && transfer.inputMassGrams > 0L
                && transfer.sourceStackIds != null
                && transfer.sourceStackIds.Count > 0;
            bool hasReserveOnlyTransfer = transfer != null
                && transfer.transferredWaterUnits == 0
                && string.IsNullOrEmpty(transfer.physicalCommitId)
                && transfer.inputMassGrams == 0L
                && (transfer.sourceStackIds?.Count ?? 0) == 0;
            if (transfer == null
                || !IsCanonical(transfer.operationId)
                || !transfer.operationId.StartsWith(
                    $"production-process-fluid:{billId.Value}:{saved.cycleSequence:D8}:manual-water:",
                    StringComparison.Ordinal)
                || !IsCanonical(transfer.destinationId)
                || !IsFiniteNonNegative(transfer.requestedWaterUnits)
                || transfer.transferredWaterUnits < 0
                || !hasPhysicalTransfer && !hasReserveOnlyTransfer
                || transfer.sourceStackIds != null
                    && (transfer.sourceStackIds.Any(value => !IsCanonical(value))
                        || transfer.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                            != transfer.sourceStackIds.Count)
                || previousManualOperation.Length > 0
                    && string.CompareOrdinal(
                        previousManualOperation,
                        transfer.operationId) >= 0)
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has invalid manual-water provenance.");
            }
            previousManualOperation = transfer.operationId;
        }
        ValidateReservations(saved.outputReservations, catalog, billId);
        ValidateRoutes(saved.routePolicies, billId);
        ValidateSupplies(saved.selectedSupplies, catalog, billId);
        ValidateProcessState(saved, recipe, billId);
    }

    private static void ValidateResolvedOutputs(
        ProductionBillSaveData saved,
        ProductionRecipeSO recipe,
        ProductionBillId billId)
    {
        if (saved.resolvedOutputs == null
            || (!saved.outputOutcomeResolved && saved.resolvedOutputs.Count != 0)
            || (saved.outputOutcomeResolved && !saved.materialsConsumed))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has inconsistent resolved output state.");
        }
        HashSet<string> authored = recipe.Outputs
            .Where(output => output != null && output.Probability > 0f)
            .Select(output => output.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        string previous = string.Empty;
        foreach (ProductionResolvedOutputSaveData output in saved.resolvedOutputs)
        {
            if (output == null
                || !ProductionOutputDefinition.IsCanonicalOutputLineId(
                    output.outputLineId)
                || !IsCanonical(output.itemId)
                || !authored.Contains(output.itemId)
                || !IsCanonical(output.outputCapabilityId)
                || output.outputCapabilityVersion <= 0
                || !IsCanonical(output.outputComponentCodecId)
                || output.outputComponentCodecVersion <= 0
                || !IsLowercaseSha256(output.outputCapabilityFingerprint)
                || output.amount <= 0
                || output.committedAmount < 0
                || output.committedAmount > output.amount
                || output.committedMassGrams < 0L
                || (output.committedAmount == 0) !=
                    (output.committedMassGrams == 0L)
                || !IsValidPendingOutputCommit(saved, billId, output)
                || !IsFiniteNonNegative(output.qualityModifier)
                || !IsFiniteInRange(output.workerQuality, 0.7f, 1.25f)
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, output.outputLineId) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has an invalid resolved output.");
            }
            previous = output.outputLineId;
        }
    }

    private static void ValidatePreparedOutput(
        ProductionBillSaveData saved,
        ProductionRecipeSO recipe,
        IResourceEconomyContentCatalog catalog,
        ProductionBillId billId)
    {
        if (saved.preparedOutput == null)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has no prepared-output payload.");
        }
        ProductionPreparedOutputContract.ValidateForBill(
            saved.preparedOutput,
            billId,
            saved.recipeId,
            saved.cycleSequence,
            saved.outputDestinationId);
        if (saved.preparedOutput.phase !=
            ProductionPreparedOutputPhase.Unresolved)
        {
            ProductionPreparedOutputMigrationScope
                .ValidateCanonicalProfileOrThrow(recipe);
            ProductionPreparedOutputMigrationScope.ValidateSavedProfileDigest(
                saved.preparedOutput,
                recipe,
                $"Production bill '{billId}'");
            if (ProductionPreparedOutputMigrationScope
                .HasLegacyOutputAuthority(saved))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has legacy output authority in the prepared-output migration scope.");
            }
        }
        if (saved.preparedOutput.phase == ProductionPreparedOutputPhase.Unresolved)
        {
            return;
        }
        ProductionPreparedOutputSourceRevisionGuard.ValidateResolvedBatch(
            saved.preparedOutput,
            recipe,
            $"Production bill '{billId}'");
        if (saved.outputOutcomeResolved || saved.resolvedOutputs.Count != 0)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has conflicting legacy and prepared output authority.");
        }

        ProductionPreparedOutputLineSaveData[] physicalLines =
            saved.preparedOutput.lines
                .Where(line => ProductionOutputRoleRules.IsPhysical(line.role))
                .ToArray();
        if (physicalLines.Any(line => !catalog.TryGetItem(line.itemId, out _)))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' prepared output references an unknown item.");
        }
        Dictionary<string, ProductionOutputDefinition> authoredLines;
        try
        {
            authoredLines = recipe.CaptureCanonicalOutputs()
                .Where(output => output != null && output.Probability > 0f)
                .ToDictionary(
                    output => output.OutputLineId,
                    output => output,
                    StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has duplicate output-line authority.",
                exception);
        }
        if (authoredLines.Values.Any(output => !output.HasCanonicalAuthoredValue))
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has a noncanonical output line.");
        }
        Dictionary<string, ProductionPreparedOutputLineSaveData> preparedLines =
            saved.preparedOutput.lines.ToDictionary(
                line => line.outputLineId,
                line => line,
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, ProductionOutputDefinition> pair in
                 authoredLines)
        {
            if (!preparedLines.TryGetValue(
                    pair.Key,
                    out ProductionPreparedOutputLineSaveData prepared)
                || prepared.role != pair.Value.Role
                || !string.Equals(
                    prepared.itemId,
                    pair.Value.ItemId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' is missing or conflicts with authored output line '{pair.Key}'.");
            }
        }
        if (physicalLines.Any(line =>
                (line.role is ProductionOutputRole.Main
                    or ProductionOutputRole.Byproduct)
                && !authoredLines.ContainsKey(line.outputLineId)))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' adds a non-authored Main/Byproduct output line.");
        }
    }

    private static bool IsValidPendingOutputCommit(
        ProductionBillSaveData saved,
        ProductionBillId billId,
        ProductionResolvedOutputSaveData output)
    {
        string pending = output.pendingCommitId ?? string.Empty;
        ProductionExactOutputPublicationSaveData publication =
            output.pendingOutputPublication;
        if (pending.Length == 0)
        {
            return !output.pendingCommitApplied
                && IsEmptyExactOutputPublication(publication);
        }
        if (!IsCanonical(pending)
            || output.committedAmount >= output.amount && !output.pendingCommitApplied)
        {
            return false;
        }
        int ordinal = output.pendingCommitApplied
            ? output.committedAmount - 1
            : output.committedAmount;
        if (ordinal < 0 || ordinal >= output.amount)
        {
            return false;
        }
        if (!string.Equals(
            pending,
            ProductionOutputCommitIdentity.Format(
                billId,
                saved.cycleSequence,
                output.outputLineId,
                output.itemId,
                ordinal),
            StringComparison.Ordinal))
        {
            return false;
        }
        return output.pendingCommitApplied
            ? IsValidExactOutputPublication(
                saved,
                billId,
                output,
                publication)
            : IsEmptyExactOutputPublication(publication);
    }

    private static bool IsEmptyExactOutputPublication(
        ProductionExactOutputPublicationSaveData publication) =>
        publication != null
        && publication.phase == ProductionExactOutputPublicationPhase.None
        && string.IsNullOrEmpty(publication.ownerStableId)
        && string.IsNullOrEmpty(publication.commitId)
        && string.IsNullOrEmpty(publication.facilityInstanceId)
        && string.IsNullOrEmpty(publication.outputCapabilityId)
        && publication.outputCapabilityVersion == 0
        && string.IsNullOrEmpty(publication.outputComponentCodecId)
        && publication.outputComponentCodecVersion == 0
        && string.IsNullOrEmpty(publication.maximumProofDigest)
        && publication.maximumMassGrams == 0L
        && string.IsNullOrEmpty(publication.capacitySourceDigest)
        && publication.requiredMinimumCapacityGrams == 0L
        && publication.exactMassGrams == 0L
        && string.IsNullOrEmpty(publication.outcomeFingerprint)
        && string.IsNullOrEmpty(publication.plannedOutputFingerprint)
        && string.IsNullOrEmpty(publication.destinationId)
        && publication.dropPositionX == 0
        && publication.dropPositionY == 0
        && string.IsNullOrEmpty(publication.ownerDomain)
        && string.IsNullOrEmpty(publication.ownerOperationId)
        && string.IsNullOrEmpty(publication.ownerFacilityId)
        && publication.capacityRevision == 0L
        && !publication.acknowledgedAtCapture
        && publication.stacks != null
        && publication.stacks.Count == 0;

    private static bool IsValidExactOutputPublication(
        ProductionBillSaveData saved,
        ProductionBillId billId,
        ProductionResolvedOutputSaveData output,
        ProductionExactOutputPublicationSaveData publication)
    {
        if (publication == null
            || publication.phase
                != ProductionExactOutputPublicationPhase.Published
            || !string.Equals(
                publication.ownerStableId,
                billId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.commitId,
                output.pendingCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.facilityInstanceId,
                saved.buildingInstanceId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.outputCapabilityId,
                output.outputCapabilityId,
                StringComparison.Ordinal)
            || publication.outputCapabilityVersion
                != output.outputCapabilityVersion
            || !string.Equals(
                publication.outputComponentCodecId,
                output.outputComponentCodecId,
                StringComparison.Ordinal)
            || publication.outputComponentCodecVersion
                != output.outputComponentCodecVersion
            || !IsLowercaseSha256(publication.maximumProofDigest)
            || publication.maximumMassGrams <= 0L
            || !IsLowercaseSha256(publication.capacitySourceDigest)
            || publication.requiredMinimumCapacityGrams <= 0L
            || publication.exactMassGrams <= 0L
            || publication.exactMassGrams > publication.maximumMassGrams
            || !IsLowercaseSha256(publication.outcomeFingerprint)
            || !IsLowercaseSha256(publication.plannedOutputFingerprint)
            || !string.Equals(
                publication.destinationId,
                saved.outputDestinationId,
                StringComparison.Ordinal)
            || !IsCanonical(publication.ownerDomain)
            || !IsCanonical(publication.ownerOperationId)
            || !string.Equals(
                publication.ownerFacilityId,
                saved.buildingInstanceId,
                StringComparison.Ordinal)
            || publication.capacityRevision < 0L
            || publication.stacks == null
            || publication.stacks.Count == 0)
        {
            return false;
        }

        long totalMass = 0L;
        string previousLine = string.Empty;
        string previousStack = string.Empty;
        try
        {
            for (int index = 0; index < publication.stacks.Count; index++)
            {
                ProductionExactOutputPublicationStackSaveData stack =
                    publication.stacks[index];
                if (stack == null
                    || stack.stackOrdinal != index
                    || !ProductionOutputDefinition.IsCanonicalOutputLineId(
                        stack.outputLineId)
                    || !IsCanonical(stack.stackId)
                    || !string.Equals(
                        stack.itemId,
                        output.itemId,
                        StringComparison.Ordinal)
                    || stack.quantity <= 0
                    || stack.massGrams <= 0L
                    || stack.componentSignature == null
                    || stack.itemInstanceId == null
                    || (previousLine.Length > 0
                        && (string.CompareOrdinal(
                                previousLine,
                                stack.outputLineId) > 0
                            || string.Equals(
                                previousLine,
                                stack.outputLineId,
                                StringComparison.Ordinal)
                            && string.CompareOrdinal(
                                previousStack,
                                stack.stackId) >= 0)))
                {
                    return false;
                }
                previousLine = stack.outputLineId;
                previousStack = stack.stackId;
                totalMass = checked(totalMass + stack.massGrams);
            }
        }
        catch (OverflowException)
        {
            return false;
        }
        return totalMass == publication.exactMassGrams;
    }

    private static void ValidateWorkerPolicy(
        WorkerSelectionPolicySaveData policy,
        ProductionBillId billId)
    {
        if (policy == null
            || !Enum.IsDefined(typeof(WorkerSelectionMode), policy.mode)
            || !Enum.IsDefined(typeof(WorkerRequirementMatchMode), policy.matchMode)
            || !Enum.IsDefined(typeof(WorkerCandidateSortMode), policy.sortMode)
            || policy.minimumSkillExperience < 0
            || policy.minimumCareerRank < 0)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has an invalid worker policy.");
        }
        ValidateCanonicalStrings(policy.specificCharacterIds, "specific worker IDs");
        ValidateCanonicalStrings(policy.excludedCharacterIds, "excluded worker IDs");
        ValidateCanonicalStrings(policy.requiredTraitIds, "required worker traits");
        ValidateCanonicalStrings(policy.excludedTraitIds, "excluded worker traits");
    }

    private static void ValidateWorkerContributions(
        IReadOnlyList<CraftContributionSaveData> values,
        ProductionBillId billId)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (CraftContributionSaveData value in values
                     ?? Array.Empty<CraftContributionSaveData>())
        {
            if (value == null || !IsCanonical(value.characterId)
                || !ids.Add(value.characterId)
                || !IsFiniteNonNegative(value.contributedWork)
                || !IsFiniteNonNegative(value.relevantSkill))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has invalid worker contributions.");
            }
        }
    }

    private static void ValidateProcessState(
        ProductionBillSaveData saved,
        ProductionRecipeSO recipe,
        ProductionBillId billId)
    {
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch
            && saved.batchStage != ProductionBatchStage.None)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has an incompatible batch stage.");
        }
        if (saved.mode == ProductionOrderMode.RepeatCount
            && saved.remainingCycles < 0)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has invalid repeat-count state.");
        }
        if (saved.mode != ProductionOrderMode.RepeatCount
            && saved.remainingCycles != -1)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has non-canonical infinite-cycle state.");
        }
    }

    private static void ValidateStatus(
        ProductionStatusSaveData status,
        bool failure,
        string billId)
    {
        if (status == null || status.parameters == null)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has a missing status payload.");
        }
        bool codeValid = failure
            ? Enum.IsDefined(typeof(FailureCode), status.code)
                && status.outcome == ProductionBillOutcomeCode.None
            : status.code == FailureCode.None
                && Enum.IsDefined(
                    typeof(ProductionBillOutcomeCode),
                    status.outcome);
        if (!codeValid
            || status.parameters.Any(value => !IsCanonical(value)))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has an invalid status code or parameter.");
        }
        if ((failure ? status.code == FailureCode.None
                : status.outcome == ProductionBillOutcomeCode.None)
            && status.parameters.Count != 0)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has parameters without a status code.");
        }
    }

    private static void ValidateReservations(
        IReadOnlyList<ProductionOutputReservationSaveData> reservations,
        IResourceEconomyContentCatalog catalog,
        ProductionBillId billId)
    {
        if (reservations == null)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has no output reservation list.");
        }
        string previous = string.Empty;
        foreach (ProductionOutputReservationSaveData reservation in reservations)
        {
            if (reservation == null
                || !IsCanonical(reservation.itemId)
                || !catalog.TryGetItem(reservation.itemId, out _)
                || reservation.amount <= 0
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, reservation.itemId) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has invalid output reservations.");
            }
            previous = reservation.itemId;
        }
    }

    private static void ValidateRoutes(
        IReadOnlyList<ProductionConsumerRoutePolicy> routes,
        ProductionBillId billId)
    {
        if (routes == null)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has no route-policy list.");
        }
        string previous = string.Empty;
        foreach (ProductionConsumerRoutePolicy route in routes)
        {
            if (route == null
                || !IsCanonical(route.consumerId)
                || route.minimumReserve < 0
                || route.targetStock < route.minimumReserve
                || route.weight < 1
                || route.weight > 10
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, route.consumerId) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has invalid route policies.");
            }
            previous = route.consumerId;
        }
    }

    private static void ValidateSupplies(
        IReadOnlyList<ProductionSelectedSupplySaveData> supplies,
        IResourceEconomyContentCatalog catalog,
        ProductionBillId billId)
    {
        if (supplies == null)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has no selected-supply list.");
        }
        string previous = string.Empty;
        foreach (ProductionSelectedSupplySaveData supply in supplies)
        {
            if (supply == null
                || !IsCanonical(supply.supplyKey)
                || !IsCanonical(supply.itemId)
                || !catalog.TryGetItem(supply.itemId, out _)
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, supply.supplyKey) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has invalid selected supplies.");
            }
            previous = supply.supplyKey;
        }
    }

    private static void ValidateCanonicalBuildingIds(
        IReadOnlyList<string> ids,
        string label)
    {
        string previous = string.Empty;
        foreach (string id in ids)
        {
            if (!((BuildingInstanceId)id).IsValid
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, id) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production-bill {label} are invalid, duplicated, or unordered.");
            }
            previous = id;
        }
    }

    private static void ValidateCanonicalStrings(
        IReadOnlyList<string> values,
        string label)
    {
        if (values == null)
        {
            throw new InvalidOperationException(
                $"Production-bill {label} list is null.");
        }
        string previous = string.Empty;
        foreach (string value in values)
        {
            if (!IsCanonical(value)
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, value) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production-bill {label} are invalid, duplicated, or unordered.");
            }
            previous = value;
        }
    }

    private static ProductionStatusSaveData CaptureFailure(
        DomainFailure failure)
    {
        return new ProductionStatusSaveData
        {
            code = failure.Code,
            outcome = ProductionBillOutcomeCode.None,
            parameters = failure.Parameters.ToArray().ToList()
        };
    }

    private static DomainFailure RestoreFailure(ProductionStatusSaveData saved) =>
        new(saved.code, saved.parameters.ToArray());

    private static ProductionStatusSaveData CaptureLogistics(
        ProductionLogisticsStatus status)
    {
        return new ProductionStatusSaveData
        {
            code = FailureCode.None,
            outcome = status.Code,
            parameters = status.Parameters.ToList()
        };
    }

    private static ProductionLogisticsStatus RestoreLogistics(
        ProductionStatusSaveData saved) =>
        new(saved.outcome, saved.parameters.ToArray());

    private static List<string> CanonicalIds(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>())
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

    private static bool TryParseSequence(
        ProductionBillId billId,
        out int sequence)
    {
        const string prefix = "production-bill:";
        return int.TryParse(
            billId.Value.AsSpan(prefix.Length),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out sequence)
            && sequence > 0
            && string.Equals(
                billId.Value,
                prefix + sequence.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        foreach (char character in value)
        {
            if (!(character is >= '0' and <= '9')
                && !(character is >= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool IsFinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    private static bool IsFiniteInRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;
}
