using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IProductionItemGateway
{
    int CountDelivered(string itemId, string destinationId);
    int CountPending(string itemId, string destinationId);
    int CountAvailableStock(string itemId, string excludedDestinationId);
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
    void PrioritizeDestination(string destinationId);
    int ReleaseDestination(string destinationId, Vector2Int releasePosition);
    int RemoveDestination(string destinationId);
}

public interface IProductionOutputBufferGateway
{
    int CountBufferedOutput(string itemId);
    int CountBufferedOutput(string itemId, string destinationId);
    bool SpawnBufferedOutput(
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId);
    int ReleaseBufferedOutput(string destinationId, Vector2Int releasePosition);
    bool TryRouteBufferedOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure);
}

public interface IProductionSupplyInventoryGateway
{
    string GetOldestAvailableStackId(string itemId, string excludedDestinationId);
}

public sealed class ProductionItemGateway :
    IProductionItemGateway,
    IProductionOutputBufferGateway,
    IProductionSupplyInventoryGateway
{
    private readonly IStockQuery stock;
    private readonly IItemTransferService transfers;

    public ProductionItemGateway(
        IStockQuery stock,
        IItemTransferService transfers)
    {
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
    }

    public int CountDelivered(string itemId, string destinationId)
    {
        return Count(
            itemId,
            destinationId,
            WorldItemStackState.FacilityBuffer,
            excludeCarried: false,
            excludedDestinationId: string.Empty);
    }

    public int CountPending(string itemId, string destinationId)
    {
        return Count(
            itemId,
            destinationId,
            requiredState: null,
            excludeCarried: false,
            excludedDestinationId: string.Empty);
    }

    public int CountAvailableStock(
        string itemId,
        string excludedDestinationId)
    {
        return Count(
            itemId,
            destinationId: string.Empty,
            requiredState: null,
            excludeCarried: true,
            excludedDestinationId);
    }

    public string GetOldestAvailableStackId(
        string itemId,
        string excludedDestinationId)
    {
        return stock.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && !stack.IsReserved
                && !stack.Forbidden
                && stack.Quantity > 0
                && stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                && !string.Equals(
                    stack.DestinationId,
                    excludedDestinationId,
                    StringComparison.Ordinal))
            .Select(stack => stack.StackId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault() ?? string.Empty;
    }

    public bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        bool succeeded = transfers.TryRequestItemDelivery(
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out DomainFailure failure);
        failureReason = failure.IsFailure
            ? failure.Code.ToString()
            : string.Empty;
        return succeeded;
    }

    public bool ConsumeDelivered(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason)
    {
        return transfers.TryConsumeFacilityItemBuffer(
            destinationId,
            costs,
            out failureReason);
    }

    public bool SpawnOutput(string itemId, int amount, Vector2Int position)
    {
        return transfers.TrySpawnItem(
            itemId,
            amount,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            out int spawned);
    }

    public int CountBufferedOutput(string itemId, string destinationId)
    {
        return Count(
            itemId,
            destinationId,
            WorldItemStackState.FacilityOutputBuffer,
            excludeCarried: false,
            excludedDestinationId: string.Empty);
    }

    public int CountBufferedOutput(string itemId)
    {
        return Count(
            itemId,
            destinationId: string.Empty,
            WorldItemStackState.FacilityOutputBuffer,
            excludeCarried: false,
            excludedDestinationId: string.Empty);
    }

    public bool SpawnBufferedOutput(
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId)
    {
        return transfers.TrySpawnItem(
            itemId,
            amount,
            position,
            WorldItemStackState.FacilityOutputBuffer,
            destinationId?.Trim() ?? string.Empty,
            out int spawned);
    }

    public int ReleaseBufferedOutput(
        string destinationId,
        Vector2Int releasePosition)
    {
        return transfers.ReleaseDestination(
            destinationId,
            releasePosition);
    }

    public bool TryRouteBufferedOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure)
    {
        return transfers.TryRouteFacilityOutput(
            sourceDestinationId,
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out routed,
            out failure);
    }

    public void PrioritizeDestination(string destinationId)
    {
        transfers.PrioritizeDestination(destinationId);
    }

    public int ReleaseDestination(
        string destinationId,
        Vector2Int releasePosition)
    {
        return transfers.ReleaseDestination(
            destinationId,
            releasePosition);
    }

    public int RemoveDestination(string destinationId)
    {
        return transfers.RemoveDestination(
            destinationId,
            WorldItemStackState.Loose,
            WorldItemStackState.FacilityBuffer);
    }

    private int Count(
        string itemId,
        string destinationId,
        WorldItemStackState? requiredState,
        bool excludeCarried,
        string excludedDestinationId)
    {
        string normalizedItemId = itemId?.Trim() ?? string.Empty;
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        string normalizedExcludedDestination =
            excludedDestinationId?.Trim() ?? string.Empty;
        return stock.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && string.Equals(
                    stack.ItemId,
                    normalizedItemId,
                    StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(normalizedDestination)
                    || string.Equals(
                        stack.DestinationId,
                        normalizedDestination,
                        StringComparison.Ordinal))
                && (!requiredState.HasValue
                    || stack.State == requiredState.Value)
                && (!excludeCarried
                    || stack.State != WorldItemStackState.Carried)
                && (!excludeCarried
                    || !string.IsNullOrWhiteSpace(normalizedDestination)
                    || stack.State == WorldItemStackState.Loose
                    || stack.State == WorldItemStackState.Stored
                    )
                && (string.IsNullOrWhiteSpace(normalizedExcludedDestination)
                    || !string.Equals(
                        stack.DestinationId,
                        normalizedExcludedDestination,
                        StringComparison.Ordinal)))
            .Sum(stack => stack.Quantity);
    }
}
