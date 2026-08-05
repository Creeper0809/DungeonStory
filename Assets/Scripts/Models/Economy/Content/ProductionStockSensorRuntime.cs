using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

public interface IProductionStockSensorRuntime
{
    int Version { get; }
    IReadOnlyCollection<string> InstalledFacilityIds { get; }
    IReadOnlyCollection<string> AcknowledgedFacilityIds { get; }
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

    private readonly IProductionAssemblyBridge bridge;

    private readonly ProductionAggregateStateStore stateStore;

    private IReadOnlyCollection<string> installed =>
        stateStore.InstalledStockSensorFacilityIds;
    private IReadOnlyCollection<string> acknowledged =>
        stateStore.AcknowledgedStockSensorFacilityIds;

    public ProductionStockSensorRuntime(
        IProductionAssemblyBridge bridge,
        ProductionAggregateStateStore stateStore)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public int Version => stateStore.StockSensorVersion;
    public IReadOnlyCollection<string> InstalledFacilityIds => installed
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToArray();
    public IReadOnlyCollection<string> AcknowledgedFacilityIds => acknowledged
        .OrderBy(id => id, StringComparer.Ordinal)
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
        if (installed.Contains(facilityId))
        {
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
                    FailureCode.ProductionMaterialsMissing,
                    itemId));
        }

        if (!TryConsume(destinationId, itemId, out string consumeFailure))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed,
                    itemId));
        }

        stateStore.AddInstalledSensor(facilityId);
        Touch();
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
        if (!stateStore.RemoveInstalledSensor(facilityId))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionStockSensorRequired,
                    facilityId));
        }

        stateStore.RemoveAcknowledgedSensor(facilityId);
        string itemId = GetInstallationItemId(facility);
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            bridge.SpawnOutput(itemId, 1, facility.Position);
        }
        Touch();
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
                && TryConsume(destinationId, itemId, out _))
            {
                stateStore.AddInstalledSensor(facilityId);
                Touch();
            }
        }
    }

    private bool TryConsume(
        string destinationId,
        string itemId,
        out string failureReason)
    {
        return bridge.ConsumeDelivered(
            destinationId,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [itemId] = 1
            },
            out failureReason);
    }

    private static string GetInstallationItemId(ProductionFacilityHandle facility)
    {
        return facility?.StockSensorInstallationItemId ?? string.Empty;
    }

    private static string GetFacilityId(ProductionFacilityHandle facility)
    {
        return facility.InstanceId.Value;
    }

    private void Touch()
    {
        unchecked
        {
            stateStore.IncrementStockSensorVersion();
        }
    }
}
