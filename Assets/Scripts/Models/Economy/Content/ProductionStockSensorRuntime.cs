using System;
using System.Collections.Generic;
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
    bool IsAcknowledged(ProductionFacilityHandle facility);
    ProductionBillCommandResult RequestInstallation(ProductionFacilityHandle facility);
    ProductionBillCommandResult Remove(ProductionFacilityHandle facility);
    ProductionBillCommandResult Acknowledge(ProductionFacilityHandle facility);
    void FinalizeDeliveredSensors();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionStockSensorRuntime : IProductionStockSensorRuntime
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

    private readonly ProductionAggregateStateStore stateStore;

    private IReadOnlyCollection<string> installed =>
        stateStore.InstalledStockSensorFacilityIds;
    private IReadOnlyCollection<string> acknowledged =>
        stateStore.AcknowledgedStockSensorFacilityIds;

    public ProductionStockSensorRuntime(
        IProductionAssemblyBridge bridge,
        ProductionAggregateStateStore stateStore,
        IProductionStockSensorRemovalOutputGateway removalOutputs)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.removalOutputs = removalOutputs
            ?? throw new ArgumentNullException(nameof(removalOutputs));
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

    public bool Has(ProductionFacilityHandle facility)
    {
        return facility != null && installed.Contains(GetFacilityId(facility));
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
        string itemId = GetInstallationItemId(facility);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionSupportUnavailable));
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
        if (stateStore.TryGetPendingStockSensorInstall(facilityId, out _))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed,
                    facilityId));
        }
        if (stateStore.TryGetPendingStockSensorRemoval(facilityId, out _))
        {
            return ResumePendingRemoval(facilityId)
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
        stateStore.SetPendingStockSensorRemoval(new()
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
        });
        Touch();
        if (!ResumePendingRemoval(facilityId))
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
        stateStore.AddAcknowledgedSensor(facilityId);
        Touch();
        return ProductionBillCommandResult.Success(
            default,
            ProductionBillOutcomeCode.StockSensorAcknowledged);
    }

    public void FinalizeDeliveredSensors()
    {
        foreach (ProductionStockSensorRemovalSaveData owner in PendingRemovals)
        {
            ResumePendingRemoval(owner.facilityId);
        }
        Dictionary<string, ProductionFacilityHandle> liveFacilities =
            (bridge.Facilities ?? Array.Empty<ProductionFacilityHandle>())
            .Where(facility => facility != null && !facility.IsDestroyed)
            .ToDictionary(GetFacilityId, StringComparer.Ordinal);
        foreach (ProductionStockSensorPhysicalCommitSaveData owner in
                 PendingInstallations)
        {
            if (liveFacilities.ContainsKey(owner.facilityId))
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

    private bool TryBeginInstallation(
        string facilityId,
        string destinationId,
        string itemId,
        out string failureReason)
    {
        string operationId = BuildPhysicalOperationId(facilityId);
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

    private bool ResumePendingRemoval(string facilityId)
    {
        if (!stateStore.TryGetPendingStockSensorRemoval(
                facilityId,
                out ProductionStockSensorRemovalSaveData owner))
        {
            return !installed.Contains(facilityId);
        }
        if (!installed.Contains(facilityId)
            || !stateStore.TryGetInstalledStockSensor(
                facilityId,
                out ProductionInstalledStockSensorSaveData installedRecord)
            || !Matches(owner, installedRecord))
        {
            throw new InvalidOperationException(
                "Stock-sensor removal owner conflicts with installed mass: "
                + facilityId);
        }

        if (!removalOutputs.TryEnsureRemovalOutput(
                owner.itemId,
                new UnityEngine.Vector2Int(
                    owner.outputPositionX,
                    owner.outputPositionY),
                owner.operationId,
                owner.reasonCode,
                out ProductionStockSensorRemovalReceipt receipt,
                out _)
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
            throw new InvalidOperationException(
                "Stock-sensor removal output conflicts with persisted receipt: "
                + facilityId);
        }

        stateStore.RemoveInstalledSensor(facilityId);
        stateStore.RemoveAcknowledgedSensor(facilityId);
        stateStore.RemoveInstalledStockSensor(facilityId);
        stateStore.RemovePendingStockSensorRemoval(facilityId);
        Touch();
        return true;
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

    public static string BuildPhysicalOperationId(string facilityId) =>
        PhysicalOperationPrefix + (facilityId ?? string.Empty);

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
