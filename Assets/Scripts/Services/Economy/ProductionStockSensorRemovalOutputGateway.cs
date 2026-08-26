using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProductionStockSensorRemovalOutputGateway :
    IProductionStockSensorRemovalOutputGateway
{
    private readonly IPhysicalItemSourcePublicationService sources;

    public ProductionStockSensorRemovalOutputGateway(
        IPhysicalItemSourcePublicationService sources)
    {
        this.sources = sources
            ?? throw new ArgumentNullException(nameof(sources));
    }

    public bool TryEnsureRemovalOutput(
        string itemId,
        Vector2Int outputPosition,
        string operationId,
        string reasonCode,
        out ProductionStockSensorRemovalReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (string.IsNullOrWhiteSpace(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
        {
            failureReason = "production-stock-sensor-removal-item-invalid";
            return false;
        }

        if (!sources.TryEnsureLooseOutputs(
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [itemId] = 1
                },
                outputPosition,
                operationId,
                reasonCode,
                out PhysicalItemSourcePublicationReceipt physical,
                out failureReason))
        {
            return false;
        }

        receipt = new ProductionStockSensorRemovalReceipt(
            physical.OperationId,
            physical.ReasonCode,
            physical.OutputCommitIds,
            physical.OutputQuantity,
            physical.OutputMassGrams);
        return receipt.IsCommitted;
    }
}
