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

public sealed class ProductionItemGateway : IProductionItemGateway
{
    private readonly IWorldItemStackRuntime itemRuntime;

    public ProductionItemGateway(IWorldItemStackRuntime itemRuntime)
    {
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
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

    public bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        return itemRuntime.TryRequestItemDelivery(
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);
    }

    public bool ConsumeDelivered(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason)
    {
        return itemRuntime.TryConsumeFacilityItemBuffer(
            destinationId,
            costs,
            out failureReason);
    }

    public bool SpawnOutput(string itemId, int amount, Vector2Int position)
    {
        return itemRuntime.SpawnItemAt(
            itemId,
            amount,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            out int spawned)
            && spawned == Mathf.Max(0, amount);
    }

    public void PrioritizeDestination(string destinationId)
    {
        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks())
        {
            if (stack != null
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            {
                itemRuntime.PrioritizeHaul(stack.StackId);
            }
        }
    }

    public int ReleaseDestination(
        string destinationId,
        Vector2Int releasePosition)
    {
        return itemRuntime.ReleaseStacksByDestination(
            destinationId,
            releasePosition);
    }

    public int RemoveDestination(string destinationId)
    {
        return itemRuntime.RemoveStacksByStateAndDestination(
                WorldItemStackState.Loose,
                destinationId)
            + itemRuntime.RemoveStacksByStateAndDestination(
                WorldItemStackState.FacilityBuffer,
                destinationId);
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
        return itemRuntime.GetAllStacks()
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
                && (string.IsNullOrWhiteSpace(normalizedExcludedDestination)
                    || !string.Equals(
                        stack.DestinationId,
                        normalizedExcludedDestination,
                        StringComparison.Ordinal)))
            .Sum(stack => stack.Quantity);
    }
}
