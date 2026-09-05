using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

public interface IProductionStockSensorRuntime
{
    int Version { get; }
    IReadOnlyCollection<string> InstalledFacilityIds { get; }
    IReadOnlyCollection<string> AcknowledgedFacilityIds { get; }
    IReadOnlyCollection<ProductionStockSensorPhysicalCommitSaveData>
        PendingInstallations { get; }
    IReadOnlyCollection<ProductionInstalledStockSensorSaveData>
        InstalledSensors { get; }
    IReadOnlyCollection<ProductionStockSensorRemovalSaveData>
        PendingRemovals { get; }
    bool Has(ProductionFacilityHandle facility);
    bool HasOwnedPhysicalState(ProductionFacilityHandle facility);
    bool IsAcknowledged(ProductionFacilityHandle facility);
    ProductionBillCommandResult RequestInstallation(ProductionFacilityHandle facility);
    ProductionBillCommandResult Remove(ProductionFacilityHandle facility);
    ProductionBillCommandResult Acknowledge(ProductionFacilityHandle facility);
    bool TryReconcileDestinationAuthorities(out string failureReason);
    void FinalizeDeliveredSensors();
}

/// <summary>
/// Producer-side durable boundary for an installed sensor participating in a
/// facility destructive drain. The existing removal DTO is the outbox; the
/// upper journal owns when its published receipt may be acknowledged and GC'd.
/// </summary>
public interface IProductionStockSensorDestructiveDrainPort
{
    bool TryCapturePendingInstallation(
        BuildingInstanceId facilityId,
        out ProductionStockSensorPhysicalCommitSaveData pendingInstallation,
        out string failureReason);

    bool TryStabilizePendingInstallation(
        BuildingInstanceId facilityId,
        string expectedOperationId,
        string expectedRequestFingerprint,
        string expectedCommitId,
        out ProductionInstalledStockSensorSaveData installed,
        out string failureReason);

    bool TryCapture(
        BuildingInstanceId facilityId,
        out ProductionInstalledStockSensorSaveData installed,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason);

    bool TryPrepareDurable(
        BuildingInstanceId facilityId,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason);

    bool TryPublish(
        BuildingInstanceId facilityId,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason);

    bool TryAcknowledge(
        BuildingInstanceId facilityId,
        string expectedOutputCommitId,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason);

}

public interface IProductionStockSensorRemovalCheckpointGcCandidate
{
}

public interface IProductionStockSensorRemovalCheckpointGcPort
{
    bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionStockSensorRemovalSaveData> removals,
        out IProductionStockSensorRemovalCheckpointGcCandidate candidate,
        out string failureReason);

    bool TryPublishCheckpointGarbageCollection(
        IProductionStockSensorRemovalCheckpointGcCandidate candidate,
        out string failureReason);

    void RollbackCheckpointGarbageCollection(
        IProductionStockSensorRemovalCheckpointGcCandidate candidate);

    void CompleteCheckpointGarbageCollection(
        IProductionStockSensorRemovalCheckpointGcCandidate candidate);
}

/// <summary>
/// Scene-bound adapter for the permanent, one-panel physical delivery socket
/// of every stock-sensor-capable production facility. Capacity is derived from
/// the authored installation item and is therefore recomputed rather than
/// persisted by the production aggregate.
/// </summary>
public interface IProductionStockSensorDestinationAuthorityRuntime
{
    bool TryEnsure(
        ProductionFacilityHandle facility,
        out long capacityMassGrams,
        out string failureReason);

    bool TryValidate(
        ProductionFacilityHandle facility,
        out long capacityMassGrams,
        out string failureReason);

    bool TryReplaceProjected(
        IReadOnlyList<ProductionFacilityHandle> facilities,
        out string failureReason);

    bool TryRequireEmpty(
        ProductionFacilityHandle facility,
        out string failureReason);

    bool TryRevoke(
        BuildingInstanceId facilityId,
        out string failureReason);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionStockSensorRuntime :
    IProductionStockSensorRuntime,
    IProductionStockSensorDestructiveDrainPort,
    IProductionStockSensorRemovalCheckpointGcPort
{
    private const string DestinationPrefix = "production-sensor:";
    public const string PhysicalOperationPrefix =
        "production-stock-sensor-install:";
    public const string PhysicalReasonCode =
        "production-stock-sensor.infrastructure-embedded";
    public const string RemovalOperationPrefix =
        "production-stock-sensor-removal:";
    public const string RemovalReasonCode =
        "production-stock-sensor.removed-to-loose-output";

    private readonly IProductionAssemblyBridge bridge;
    private readonly IProductionStockSensorRemovalOutputGateway removalOutputs;
    private readonly IProductionStockSensorDestinationAuthorityRuntime
        destinationAuthorities;
    private readonly IProductionFacilityDestructiveDrainOpenOperationQuery
        destructiveDrains;
    private RemovalCheckpointGcCandidate activeRemovalCheckpointGcCandidate;

    private readonly ProductionAggregateStateStore stateStore;

    private IReadOnlyCollection<string> installed =>
        stateStore.InstalledStockSensorFacilityIds;
    private IReadOnlyCollection<string> acknowledged =>
        stateStore.AcknowledgedStockSensorFacilityIds;

    public ProductionStockSensorRuntime(
        IProductionAssemblyBridge bridge,
        ProductionAggregateStateStore stateStore,
        IProductionStockSensorRemovalOutputGateway removalOutputs,
        IProductionStockSensorDestinationAuthorityRuntime
            destinationAuthorities,
        IProductionFacilityDestructiveDrainOpenOperationQuery destructiveDrains)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.removalOutputs = removalOutputs
            ?? throw new ArgumentNullException(nameof(removalOutputs));
        this.destinationAuthorities = destinationAuthorities
            ?? throw new ArgumentNullException(nameof(destinationAuthorities));
        this.destructiveDrains = destructiveDrains
            ?? throw new ArgumentNullException(nameof(destructiveDrains));
    }

    public int Version => stateStore.StockSensorVersion;
    public IReadOnlyCollection<string> InstalledFacilityIds => installed
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();
    public IReadOnlyCollection<string> AcknowledgedFacilityIds => acknowledged
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();
    public IReadOnlyCollection<ProductionStockSensorPhysicalCommitSaveData>
        PendingInstallations => stateStore.PendingStockSensorInstalls
            .OrderBy(owner => owner.facilityId, StringComparer.Ordinal)
            .Select(owner => owner.Clone())
            .ToArray();
    public IReadOnlyCollection<ProductionInstalledStockSensorSaveData>
        InstalledSensors => stateStore.InstalledStockSensors
            .OrderBy(owner => owner.facilityId, StringComparer.Ordinal)
            .Select(owner => owner.Clone())
            .ToArray();
    public IReadOnlyCollection<ProductionStockSensorRemovalSaveData>
        PendingRemovals => stateStore.PendingStockSensorRemovals
            .OrderBy(owner => owner.facilityId, StringComparer.Ordinal)
            .Select(owner => owner.Clone())
            .ToArray();

    public bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionStockSensorRemovalSaveData> removals,
        out IProductionStockSensorRemovalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (activeRemovalCheckpointGcCandidate != null)
        {
            failureReason = "production-stock-sensor-checkpoint-gc-already-active";
            return false;
        }
        ProductionStockSensorRemovalSaveData[] ordered = (removals
                ?? Array.Empty<ProductionStockSensorRemovalSaveData>())
            .OrderBy(value => value?.facilityId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.facilityId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason = "production-stock-sensor-checkpoint-gc-row-invalid";
            return false;
        }
        foreach (ProductionStockSensorRemovalSaveData expected in ordered)
        {
            if (expected.phase != ProductionStockSensorRemovalPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                || installed.Contains(expected.facilityId)
                || acknowledged.Contains(expected.facilityId)
                || stateStore.TryGetInstalledStockSensor(
                    expected.facilityId, out _)
                || !stateStore.TryGetPendingStockSensorRemoval(
                    expected.facilityId,
                    out ProductionStockSensorRemovalSaveData current)
                || !RemovalEquals(current, expected))
            {
                failureReason =
                    "production-stock-sensor-checkpoint-gc-row-missing-or-conflicting:"
                    + (expected.facilityId ?? string.Empty);
                return false;
            }
        }
        activeRemovalCheckpointGcCandidate = new RemovalCheckpointGcCandidate(
            stateStore.StockSensorVersion,
            ordered);
        candidate = activeRemovalCheckpointGcCandidate;
        return true;
    }

    public bool TryPublishCheckpointGarbageCollection(
        IProductionStockSensorRemovalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        RemovalCheckpointGcCandidate exact = RequireRemovalCheckpointGcCandidate(
            candidate);
        if (exact.Published)
            return true;
        if (stateStore.StockSensorVersion != exact.ExpectedVersion
            || exact.Rows.Any(expected =>
                !stateStore.TryGetPendingStockSensorRemoval(
                    expected.facilityId,
                    out ProductionStockSensorRemovalSaveData current)
                || !RemovalEquals(current, expected)))
        {
            failureReason =
                "production-stock-sensor-checkpoint-gc-live-authority-changed";
            return false;
        }
        foreach (ProductionStockSensorRemovalSaveData expected in exact.Rows)
        {
            if (!stateStore.RemovePendingStockSensorRemoval(expected.facilityId))
                throw new InvalidOperationException(
                    "Stock-sensor checkpoint-GC exact row vanished during publish.");
        }
        if (exact.Rows.Count > 0)
            stateStore.IncrementStockSensorVersion();
        exact.Published = true;
        exact.PublishedVersion = stateStore.StockSensorVersion;
        return true;
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionStockSensorRemovalCheckpointGcCandidate candidate)
    {
        RemovalCheckpointGcCandidate exact = RequireRemovalCheckpointGcCandidate(
            candidate);
        if (!exact.Published)
            return;
        if (stateStore.StockSensorVersion != exact.PublishedVersion
            || exact.Rows.Any(expected =>
                stateStore.TryGetPendingStockSensorRemoval(
                    expected.facilityId, out _)))
        {
            throw new InvalidOperationException(
                "Stock-sensor checkpoint-GC rollback encountered live authority drift.");
        }
        foreach (ProductionStockSensorRemovalSaveData expected in exact.Rows)
            stateStore.SetPendingStockSensorRemoval(expected.Clone());
        if (exact.Rows.Count > 0
            && !stateStore.TryRestoreStockSensorVersionForCheckpointGc(
                exact.PublishedVersion,
                exact.ExpectedVersion))
        {
            throw new InvalidOperationException(
                "Stock-sensor checkpoint-GC rollback could not restore version.");
        }
        exact.Published = false;
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionStockSensorRemovalCheckpointGcCandidate candidate)
    {
        RequireRemovalCheckpointGcCandidate(candidate);
        activeRemovalCheckpointGcCandidate = null;
    }

    public bool Has(ProductionFacilityHandle facility)
    {
        if (facility == null)
            return false;
        string facilityId = GetFacilityId(facility);
        return installed.Contains(facilityId)
            && (!stateStore.TryGetPendingStockSensorRemoval(
                    facilityId,
                    out ProductionStockSensorRemovalSaveData removal)
                || removal.phase == ProductionStockSensorRemovalPhase.Prepared);
    }

    public bool HasOwnedPhysicalState(ProductionFacilityHandle facility)
    {
        if (facility == null || !facility.InstanceId.IsValid)
            return false;
        string facilityId = GetFacilityId(facility);
        return installed.Contains(facilityId)
            || acknowledged.Contains(facilityId)
            || stateStore.TryGetPendingStockSensorInstall(facilityId, out _)
            || stateStore.TryGetInstalledStockSensor(facilityId, out _)
            || stateStore.TryGetPendingStockSensorRemoval(
                facilityId,
                out ProductionStockSensorRemovalSaveData removal)
                && removal.phase != ProductionStockSensorRemovalPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc;
    }

    public bool IsAcknowledged(ProductionFacilityHandle facility)
    {
        return facility != null && acknowledged.Contains(GetFacilityId(facility));
    }

    public ProductionBillCommandResult RequestInstallation(
        ProductionFacilityHandle facility)
    {
        if (facility == null || facility.IsDestroyed)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionFacilityMissing));
        }

        string facilityId = GetFacilityId(facility);
        if (destructiveDrains.IsOpen(facility.InstanceId))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionSupportUnavailable));
        }
        string itemId = GetInstallationItemId(facility);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionSupportUnavailable));
        }
        if (!destinationAuthorities.TryEnsure(
                facility,
                out _,
                out string authorityFailure))
        {
            throw new InvalidOperationException(
                "Stock-sensor destination authority could not be published for '"
                + facilityId + "': " + authorityFailure);
        }
        if (stateStore.TryGetPendingStockSensorInstall(facilityId, out _))
        {
            return ResumePendingInstallation(facilityId)
                ? ProductionBillCommandResult.Success(
                    default,
                    ProductionBillOutcomeCode.StockSensorInstalled)
                : ProductionBillCommandResult.Failed(
                    new DomainFailure(
                        FailureCode.ItemTransferConsumptionFailed,
                        itemId));
        }
        if (installed.Contains(facilityId))
        {
            if (!stateStore.TryGetInstalledStockSensor(facilityId, out _))
                throw new InvalidOperationException(
                    "Installed stock sensor has no embedded-mass record: "
                    + facilityId);
            return ProductionBillCommandResult.Success(
                default,
                ProductionBillOutcomeCode.StockSensorInstalled);
        }

        string destinationId = DestinationPrefix + facilityId;
        if (bridge.CountDelivered(itemId, destinationId) < 1)
        {
            bridge.RequestDelivery(
                itemId,
                1,
                facility.Position,
                destinationId,
                out int requested,
                out string failureReason);
            if (requested < 1)
            {
                return ProductionBillCommandResult.Failed(
                    new DomainFailure(
                        FailureCode.ItemTransferRequestFailed,
                        itemId));
            }

            bridge.PrioritizeDestination(destinationId);
            bridge.RequestOneHaulerToReplan(forceInterrupt: false);
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionMaterialsMissing));
        }

        if (!TryBeginInstallation(
                facilityId,
                destinationId,
                itemId,
                out string consumeFailure))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed,
                    itemId));
        }

        if (!ResumePendingInstallation(facilityId))
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed,
                    itemId));
        return ProductionBillCommandResult.Success(
            default,
            ProductionBillOutcomeCode.StockSensorInstalled);
    }

    public ProductionBillCommandResult Remove(ProductionFacilityHandle facility)
    {
        if (facility == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionFacilityMissing));
        }

        string facilityId = GetFacilityId(facility);
        if (destructiveDrains.IsOpen(facility.InstanceId))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionSupportUnavailable));
        }
        if (stateStore.TryGetPendingStockSensorInstall(facilityId, out _))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed,
                    facilityId));
        }
        if (stateStore.TryGetPendingStockSensorRemoval(facilityId, out _))
        {
            return ResumeManualRemoval(facilityId)
                ? ProductionBillCommandResult.Success(
                    default,
                    ProductionBillOutcomeCode.StockSensorRemoved)
                : ProductionBillCommandResult.Failed(
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        facilityId));
        }
        if (!installed.Contains(facilityId)
            || !stateStore.TryGetInstalledStockSensor(
                facilityId,
                out ProductionInstalledStockSensorSaveData installedRecord))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionStockSensorRequired,
                    facilityId));
        }
        string itemId = GetInstallationItemId(facility);
        if (!string.Equals(
                itemId,
                installedRecord.itemId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Installed stock-sensor item conflicts with facility authoring: "
                + facilityId);
        }
        if (!TryPrepareRemoval(facility, out _, out _)
            || !ResumeManualRemoval(facilityId))
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    facilityId));
        return ProductionBillCommandResult.Success(
            default,
            ProductionBillOutcomeCode.StockSensorRemoved);
    }

    public ProductionBillCommandResult Acknowledge(ProductionFacilityHandle facility)
    {
        if (!Has(facility))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionStockSensorRequired));
        }

        string facilityId = GetFacilityId(facility);
        if (destructiveDrains.IsOpen(facility.InstanceId))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionSupportUnavailable));
        }
        stateStore.AddAcknowledgedSensor(facilityId);
        Touch();
        return ProductionBillCommandResult.Success(
            default,
            ProductionBillOutcomeCode.StockSensorAcknowledged);
    }

    public void FinalizeDeliveredSensors()
    {
        if (!TryReconcileDestinationAuthorities(out string authorityFailure))
        {
            throw new InvalidOperationException(
                "Stock-sensor destination authorities could not be reconciled: "
                + authorityFailure);
        }
        foreach (ProductionStockSensorRemovalSaveData owner in PendingRemovals)
        {
            BuildingInstanceId facilityId = (BuildingInstanceId)owner.facilityId;
            if (!destructiveDrains.IsOpen(facilityId))
                ResumeManualRemoval(owner.facilityId);
        }
        Dictionary<string, ProductionFacilityHandle> liveFacilities =
            (bridge.Facilities ?? Array.Empty<ProductionFacilityHandle>())
            .Where(facility => facility != null && !facility.IsDestroyed)
            .ToDictionary(GetFacilityId, StringComparer.Ordinal);
        foreach (ProductionStockSensorPhysicalCommitSaveData owner in
                 PendingInstallations)
        {
            if (liveFacilities.TryGetValue(owner.facilityId, out var facility)
                && !destructiveDrains.IsOpen(facility.InstanceId))
                ResumePendingInstallation(owner.facilityId);
        }
        foreach (ProductionFacilityHandle facility in bridge.Facilities
                     ?? Array.Empty<ProductionFacilityHandle>())
        {
            if (facility == null || facility.IsDestroyed)
            {
                continue;
            }

            string facilityId = GetFacilityId(facility);
            if (destructiveDrains.IsOpen(facility.InstanceId))
                continue;
            string destinationId = DestinationPrefix + facilityId;
            string itemId = GetInstallationItemId(facility);
            if (!installed.Contains(facilityId)
                && !string.IsNullOrWhiteSpace(itemId)
                && bridge.CountDelivered(itemId, destinationId) >= 1
                && TryBeginInstallation(
                    facilityId,
                    destinationId,
                    itemId,
                    out _))
            {
                ResumePendingInstallation(facilityId);
            }
        }
    }

    public bool TryReconcileDestinationAuthorities(out string failureReason) =>
        destinationAuthorities.TryReplaceProjected(
            bridge.Facilities ?? Array.Empty<ProductionFacilityHandle>(),
            out failureReason);

    private bool TryBeginInstallation(
        string facilityId,
        string destinationId,
        string itemId,
        out string failureReason)
    {
        string operationId = BuildPhysicalOperationId(
            facilityId,
            checked(stateStore.StockSensorVersion + 1));
        if (!bridge.CommitStockSensorInstallPending(
            destinationId,
            itemId,
            operationId,
            PhysicalReasonCode,
            out ProductionStockSensorPhysicalReceipt receipt,
            out failureReason)
            || !receipt.IsCommitted)
            return false;
        stateStore.SetPendingStockSensorInstall(new()
        {
            phase = ProductionStockSensorCommitPhase.InputCommitted,
            facilityId = facilityId,
            itemId = itemId,
            destinationId = destinationId,
            operationId = receipt.OperationId,
            reasonCode = receipt.ReasonCode,
            requestFingerprint = receipt.RequestFingerprint,
            commitId = receipt.CommitId,
            inputQuantity = receipt.InputQuantity,
            inputMassGrams = receipt.InputMassGrams,
            sourceStackIds = receipt.SourceStackIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList()
        });
        Touch();
        return true;
    }

    private bool ResumePendingInstallation(string facilityId)
    {
        if (!stateStore.TryGetPendingStockSensorInstall(
                facilityId,
                out ProductionStockSensorPhysicalCommitSaveData owner))
            return installed.Contains(facilityId);
        if (!bridge.TryGetPendingStockSensorInstall(
                owner.operationId,
                out ProductionStockSensorPhysicalReceipt receipt)
            || !Matches(owner, receipt))
            throw new InvalidOperationException(
                "Stock-sensor physical owner has no exact pending receipt: "
                + owner.operationId);
        if (owner.phase == ProductionStockSensorCommitPhase.InputCommitted)
        {
            if (installed.Contains(facilityId))
                throw new InvalidOperationException(
                    "Stock-sensor input owner conflicts with installed state: "
                    + facilityId);
            stateStore.AddInstalledSensor(facilityId);
            stateStore.SetInstalledStockSensor(new()
            {
                facilityId = facilityId,
                itemId = owner.itemId,
                inputOperationId = owner.operationId,
                inputCommitId = owner.commitId,
                inputSourceStackId = owner.sourceStackIds.Single(),
                embeddedMassGrams = owner.inputMassGrams
            });
            owner.phase = ProductionStockSensorCommitPhase.OutcomePublished;
            Touch();
        }
        else if (!installed.Contains(facilityId)
                 || !stateStore.TryGetInstalledStockSensor(
                     facilityId,
                     out ProductionInstalledStockSensorSaveData installedRecord)
                 || !string.Equals(
                     installedRecord.itemId,
                     owner.itemId,
                     StringComparison.Ordinal)
                 || installedRecord.embeddedMassGrams != owner.inputMassGrams
                 || !string.Equals(
                     installedRecord.inputCommitId,
                     owner.commitId,
                     StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Stock-sensor published owner has no exact installed outcome: "
                + facilityId);
        }
        if (!bridge.AcknowledgeStockSensorInstall(owner.commitId, out _))
            return true;
        stateStore.RemovePendingStockSensorInstall(facilityId);
        Touch();
        return true;
    }

    public bool TryCapture(
        BuildingInstanceId facilityId,
        out ProductionInstalledStockSensorSaveData installedRecord,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason)
    {
        installedRecord = null;
        removal = null;
        failureReason = string.Empty;
        if (!facilityId.IsValid)
        {
            failureReason = "production-stock-sensor-destructive-facility-invalid";
            return false;
        }

        bool hasInstalled = stateStore.TryGetInstalledStockSensor(
            facilityId.Value,
            out ProductionInstalledStockSensorSaveData foundInstalled);
        bool hasRemoval = stateStore.TryGetPendingStockSensorRemoval(
            facilityId.Value,
            out ProductionStockSensorRemovalSaveData foundRemoval);
        if (hasRemoval
            && foundRemoval.phase != ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc
            && (!hasInstalled || !Matches(foundRemoval, foundInstalled)))
        {
            failureReason =
                "production-stock-sensor-destructive-source-conflict";
            return false;
        }
        if (hasRemoval
            && foundRemoval.phase == ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc
            && (hasInstalled
                || installed.Contains(facilityId.Value)
                || acknowledged.Contains(facilityId.Value)))
        {
            failureReason =
                "production-stock-sensor-destructive-terminal-state-conflict";
            return false;
        }

        installedRecord = foundInstalled?.Clone();
        removal = foundRemoval?.Clone();
        return true;
    }

    public bool TryCapturePendingInstallation(
        BuildingInstanceId facilityId,
        out ProductionStockSensorPhysicalCommitSaveData pendingInstallation,
        out string failureReason)
    {
        pendingInstallation = null;
        failureReason = string.Empty;
        if (!facilityId.IsValid)
        {
            failureReason =
                "production-stock-sensor-destructive-facility-invalid";
            return false;
        }
        if (!stateStore.TryGetPendingStockSensorInstall(
                facilityId.Value,
                out ProductionStockSensorPhysicalCommitSaveData pending))
        {
            return true;
        }
        if (!string.Equals(
                pending.facilityId,
                facilityId.Value,
                StringComparison.Ordinal)
            || !IsPhysicalOperationIdForFacility(
                pending.operationId,
                facilityId.Value))
        {
            failureReason =
                "production-stock-sensor-destructive-install-invalid";
            return false;
        }
        pendingInstallation = pending.Clone();
        return true;
    }

    public bool TryStabilizePendingInstallation(
        BuildingInstanceId facilityId,
        string expectedOperationId,
        string expectedRequestFingerprint,
        string expectedCommitId,
        out ProductionInstalledStockSensorSaveData installedRecord,
        out string failureReason)
    {
        installedRecord = null;
        failureReason = string.Empty;
        if (!TryCapturePendingInstallation(
                facilityId,
                out ProductionStockSensorPhysicalCommitSaveData pending,
                out failureReason)
            || pending == null
            || !string.Equals(
                pending.operationId,
                expectedOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                pending.requestFingerprint,
                expectedRequestFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                pending.commitId,
                expectedCommitId,
                StringComparison.Ordinal))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-stock-sensor-destructive-install-drift"
                : failureReason;
            return false;
        }

        try
        {
            if (!ResumePendingInstallation(facilityId.Value))
            {
                failureReason =
                    "production-stock-sensor-destructive-install-deferred";
                return false;
            }
        }
        catch (Exception exception)
        {
            failureReason =
                "production-stock-sensor-destructive-install-failed:"
                + exception.GetType().Name;
            return false;
        }

        if (stateStore.TryGetPendingStockSensorInstall(
                facilityId.Value,
                out _)
            || !stateStore.TryGetInstalledStockSensor(
                facilityId.Value,
                out ProductionInstalledStockSensorSaveData installed)
            || !string.Equals(
                installed.inputOperationId,
                pending.operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                installed.inputCommitId,
                pending.commitId,
                StringComparison.Ordinal)
            || !string.Equals(
                installed.itemId,
                pending.itemId,
                StringComparison.Ordinal)
            || installed.embeddedMassGrams != pending.inputMassGrams
            || pending.sourceStackIds == null
            || pending.sourceStackIds.Count != 1
            || !string.Equals(
                installed.inputSourceStackId,
                pending.sourceStackIds[0],
                StringComparison.Ordinal))
        {
            failureReason =
                "production-stock-sensor-destructive-install-result-conflict";
            return false;
        }

        installedRecord = installed.Clone();
        return true;
    }

    public bool TryPrepareDurable(
        BuildingInstanceId facilityId,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason)
    {
        removal = null;
        failureReason = string.Empty;
        if (!TryResolveFacility(facilityId, out ProductionFacilityHandle facility))
        {
            failureReason =
                "production-stock-sensor-destructive-live-facility-missing";
            return false;
        }
        return TryPrepareRemoval(facility, out removal, out failureReason);
    }

    public bool TryPublish(
        BuildingInstanceId facilityId,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason)
    {
        removal = null;
        failureReason = string.Empty;
        if (!facilityId.IsValid)
        {
            failureReason = "production-stock-sensor-destructive-facility-invalid";
            return false;
        }
        return TryPublishRemoval(facilityId.Value, out removal, out failureReason);
    }

    public bool TryAcknowledge(
        BuildingInstanceId facilityId,
        string expectedOutputCommitId,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason)
    {
        removal = null;
        failureReason = string.Empty;
        if (!facilityId.IsValid
            || string.IsNullOrEmpty(expectedOutputCommitId)
            || !string.Equals(
                expectedOutputCommitId,
                expectedOutputCommitId.Trim(),
                StringComparison.Ordinal)
            || !stateStore.TryGetPendingStockSensorRemoval(
                facilityId.Value,
                out ProductionStockSensorRemovalSaveData owner))
        {
            failureReason =
                "production-stock-sensor-destructive-ack-request-invalid";
            return false;
        }

        if (owner.phase == ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
        {
            if (!string.Equals(
                    owner.outputCommitIds.SingleOrDefault(),
                    expectedOutputCommitId,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "production-stock-sensor-destructive-ack-replay-conflict";
                return false;
            }
            removal = owner.Clone();
            return true;
        }

        if (owner.phase != ProductionStockSensorRemovalPhase.OutputPublished
            || owner.outputCommitIds.Count != 1
            || !string.Equals(
                owner.outputCommitIds[0],
                expectedOutputCommitId,
                StringComparison.Ordinal)
            || !stateStore.TryGetInstalledStockSensor(
                facilityId.Value,
                out ProductionInstalledStockSensorSaveData installedRecord)
            || !Matches(owner, installedRecord))
        {
            failureReason =
                "production-stock-sensor-destructive-ack-state-conflict";
            return false;
        }

        stateStore.RemoveInstalledSensor(facilityId.Value);
        stateStore.RemoveAcknowledgedSensor(facilityId.Value);
        stateStore.RemoveInstalledStockSensor(facilityId.Value);
        owner.phase = ProductionStockSensorRemovalPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
        Touch();
        removal = owner.Clone();
        return true;
    }

    private bool TryCollectManualRemoval(
        BuildingInstanceId facilityId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!facilityId.IsValid
            || !stateStore.TryGetPendingStockSensorRemoval(
                facilityId.Value,
                out ProductionStockSensorRemovalSaveData owner)
            || owner.phase != ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc
            || installed.Contains(facilityId.Value)
            || acknowledged.Contains(facilityId.Value)
            || stateStore.TryGetInstalledStockSensor(facilityId.Value, out _))
        {
            failureReason =
                "production-stock-sensor-destructive-checkpoint-gc-invalid";
            return false;
        }

        stateStore.RemovePendingStockSensorRemoval(facilityId.Value);
        Touch();
        return true;
    }

    private bool TryPrepareRemoval(
        ProductionFacilityHandle facility,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason)
    {
        removal = null;
        failureReason = string.Empty;
        string facilityId = GetFacilityId(facility);
        if (stateStore.TryGetPendingStockSensorInstall(facilityId, out _))
        {
            failureReason =
                "production-stock-sensor-destructive-install-pending";
            return false;
        }
        if (stateStore.TryGetPendingStockSensorRemoval(facilityId, out _))
        {
            return TryCapture(
                facility.InstanceId,
                out _,
                out removal,
                out failureReason);
        }
        if (!installed.Contains(facilityId)
            || !stateStore.TryGetInstalledStockSensor(
                facilityId,
                out ProductionInstalledStockSensorSaveData installedRecord))
        {
            return true;
        }

        string itemId = GetInstallationItemId(facility);
        if (!string.Equals(itemId, installedRecord.itemId, StringComparison.Ordinal))
        {
            failureReason =
                "production-stock-sensor-destructive-authoring-conflict";
            return false;
        }
        ProductionStockSensorRemovalSaveData created = new()
        {
            phase = ProductionStockSensorRemovalPhase.Prepared,
            facilityId = facilityId,
            itemId = installedRecord.itemId,
            outputPositionX = facility.Position.x,
            outputPositionY = facility.Position.y,
            operationId = BuildRemovalOperationId(
                facilityId,
                installedRecord.inputSourceStackId),
            reasonCode = RemovalReasonCode,
            installationSourceStackId = installedRecord.inputSourceStackId,
            expectedOutputMassGrams = installedRecord.embeddedMassGrams
        };
        stateStore.SetPendingStockSensorRemoval(created);
        Touch();
        removal = created.Clone();
        return true;
    }

    private bool TryPublishRemoval(
        string facilityId,
        out ProductionStockSensorRemovalSaveData removal,
        out string failureReason)
    {
        removal = null;
        failureReason = string.Empty;
        if (!stateStore.TryGetPendingStockSensorRemoval(
                facilityId,
                out ProductionStockSensorRemovalSaveData owner))
        {
            return !installed.Contains(facilityId);
        }
        if (owner.phase == ProductionStockSensorRemovalPhase
            .OwnerAcknowledgedAwaitingCheckpointGc)
        {
            removal = owner.Clone();
            return true;
        }
        if (!installed.Contains(facilityId)
            || !stateStore.TryGetInstalledStockSensor(
                facilityId,
                out ProductionInstalledStockSensorSaveData installedRecord)
            || !Matches(owner, installedRecord))
        {
            failureReason =
                "production-stock-sensor-removal-installed-mass-conflict";
            return false;
        }

        if (!removalOutputs.TryEnsureRemovalOutput(
                owner.itemId,
                new UnityEngine.Vector2Int(
                    owner.outputPositionX,
                    owner.outputPositionY),
                owner.operationId,
                owner.reasonCode,
                out ProductionStockSensorRemovalReceipt receipt,
                out failureReason)
            || !Matches(owner, receipt))
        {
            return false;
        }

        if (owner.phase == ProductionStockSensorRemovalPhase.Prepared)
        {
            owner.outputQuantity = receipt.OutputQuantity;
            owner.outputMassGrams = receipt.OutputMassGrams;
            owner.outputCommitIds = receipt.OutputCommitIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            owner.phase = ProductionStockSensorRemovalPhase.OutputPublished;
            Touch();
        }
        else if (!owner.outputCommitIds.SequenceEqual(
                     receipt.OutputCommitIds.OrderBy(
                         value => value,
                         StringComparer.Ordinal),
                     StringComparer.Ordinal)
                 || owner.outputQuantity != receipt.OutputQuantity
                 || owner.outputMassGrams != receipt.OutputMassGrams)
        {
            failureReason =
                "production-stock-sensor-removal-output-receipt-conflict";
            return false;
        }

        removal = owner.Clone();
        return true;
    }

    private bool ResumeManualRemoval(string facilityId)
    {
        BuildingInstanceId id = (BuildingInstanceId)facilityId;
        if (!TryPublishRemoval(facilityId, out var owner, out _))
            return false;
        if (owner == null)
            return true;
        string commitId = owner.outputCommitIds.SingleOrDefault();
        if (!TryAcknowledge(id, commitId, out _, out _))
            return false;
        return TryCollectManualRemoval(id, out _);
    }

    private bool TryResolveFacility(
        BuildingInstanceId facilityId,
        out ProductionFacilityHandle facility)
    {
        facility = (bridge.Facilities ?? Array.Empty<ProductionFacilityHandle>())
            .SingleOrDefault(value => value != null
                && !value.IsDestroyed
                && value.InstanceId.Equals(facilityId));
        return facility != null;
    }

    private RemovalCheckpointGcCandidate RequireRemovalCheckpointGcCandidate(
        IProductionStockSensorRemovalCheckpointGcCandidate candidate)
    {
        if (candidate is not RemovalCheckpointGcCandidate exact
            || !ReferenceEquals(activeRemovalCheckpointGcCandidate, exact))
            throw new InvalidOperationException(
                "Stock-sensor checkpoint-GC candidate is stale or foreign.");
        return exact;
    }

    private static bool RemovalEquals(
        ProductionStockSensorRemovalSaveData left,
        ProductionStockSensorRemovalSaveData right) => left != null
        && right != null
        && string.Equals(
            UnityEngine.JsonUtility.ToJson(left),
            UnityEngine.JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private sealed class RemovalCheckpointGcCandidate :
        IProductionStockSensorRemovalCheckpointGcCandidate
    {
        internal RemovalCheckpointGcCandidate(
            int expectedVersion,
            IReadOnlyList<ProductionStockSensorRemovalSaveData> rows)
        {
            ExpectedVersion = expectedVersion;
            PublishedVersion = expectedVersion;
            Rows = (rows ?? Array.Empty<ProductionStockSensorRemovalSaveData>())
                .Select(value => value.Clone())
                .OrderBy(value => value.facilityId, StringComparer.Ordinal)
                .ToArray();
        }

        internal int ExpectedVersion { get; }
        internal int PublishedVersion { get; set; }
        internal IReadOnlyList<ProductionStockSensorRemovalSaveData> Rows
            { get; }
        internal bool Published { get; set; }
    }

    private static bool Matches(
        ProductionStockSensorRemovalSaveData owner,
        ProductionInstalledStockSensorSaveData installed) =>
        owner != null
        && installed != null
        && string.Equals(
            owner.facilityId,
            installed.facilityId,
            StringComparison.Ordinal)
        && string.Equals(owner.itemId, installed.itemId, StringComparison.Ordinal)
        && string.Equals(
            owner.installationSourceStackId,
            installed.inputSourceStackId,
            StringComparison.Ordinal)
        && owner.expectedOutputMassGrams == installed.embeddedMassGrams
        && owner.expectedOutputMassGrams > 0L;

    private static bool Matches(
        ProductionStockSensorRemovalSaveData owner,
        ProductionStockSensorRemovalReceipt receipt) =>
        receipt.IsCommitted
        && string.Equals(
            owner.operationId,
            receipt.OperationId,
            StringComparison.Ordinal)
        && string.Equals(
            owner.reasonCode,
            receipt.ReasonCode,
            StringComparison.Ordinal)
        && receipt.OutputQuantity == 1
        && receipt.OutputMassGrams == owner.expectedOutputMassGrams
        && receipt.OutputCommitIds.Count == 1
        && string.Equals(
            receipt.OutputCommitIds[0],
            BuildRemovalOutputCommitId(owner),
            StringComparison.Ordinal);

    private static bool Matches(
        ProductionStockSensorPhysicalCommitSaveData owner,
        ProductionStockSensorPhysicalReceipt receipt) =>
        receipt.IsCommitted
        && string.Equals(owner.operationId, receipt.OperationId, StringComparison.Ordinal)
        && string.Equals(owner.reasonCode, receipt.ReasonCode, StringComparison.Ordinal)
        && string.Equals(owner.requestFingerprint, receipt.RequestFingerprint, StringComparison.Ordinal)
        && string.Equals(owner.commitId, receipt.CommitId, StringComparison.Ordinal)
        && owner.inputQuantity == receipt.InputQuantity
        && owner.inputMassGrams == receipt.InputMassGrams
        && owner.sourceStackIds.SequenceEqual(
            receipt.SourceStackIds.OrderBy(id => id, StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static string GetInstallationItemId(ProductionFacilityHandle facility)
    {
        return facility?.StockSensorInstallationItemId ?? string.Empty;
    }

    private static string GetFacilityId(ProductionFacilityHandle facility)
    {
        return facility.InstanceId.Value;
    }

    public static string BuildDestinationId(string facilityId) =>
        DestinationPrefix + (facilityId ?? string.Empty);

    public static string BuildPhysicalOperationId(
        string facilityId,
        int operationSequence)
    {
        if (string.IsNullOrEmpty(facilityId)
            || operationSequence <= 0)
        {
            throw new ArgumentException(
                "A stock-sensor installation operation requires a facility and positive sequence.");
        }
        return PhysicalOperationPrefix + facilityId + ":"
            + operationSequence.ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsPhysicalOperationIdForFacility(
        string operationId,
        string facilityId)
    {
        if (string.IsNullOrEmpty(operationId)
            || string.IsNullOrEmpty(facilityId))
        {
            return false;
        }
        string prefix = PhysicalOperationPrefix + facilityId + ":";
        return operationId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(
                operationId.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int sequence)
            && sequence > 0;
    }

    public static string BuildRemovalOperationId(
        string facilityId,
        string installationSourceStackId = null) =>
        RemovalOperationPrefix + (facilityId ?? string.Empty)
        + ":" + (installationSourceStackId ?? string.Empty);

    public static string BuildRemovalOutputCommitId(
        ProductionStockSensorRemovalSaveData owner) =>
        owner == null
            ? string.Empty
            : $"physical-source:{owner.operationId}:{owner.itemId}:1:"
                + owner.expectedOutputMassGrams;

    private void Touch()
    {
        unchecked
        {
            stateStore.IncrementStockSensorVersion();
        }
    }
}
