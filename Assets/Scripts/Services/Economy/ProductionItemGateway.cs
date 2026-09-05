using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IProductionItemGateway
{
    bool TryGetStockCategory(string itemId, out StockCategory category);
    int CountDelivered(string itemId, string destinationId);
    int CountPending(string itemId, string destinationId);
    long CountPendingMassGrams(string destinationId);
    long GetDefinitionQuantityMassGrams(string itemId, int quantity);
    int CountAvailableStock(string itemId, string excludedDestinationId);
    bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
    bool ConsumeDeliveredToWip(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        string operationId,
        out ProductionWipInputReceipt receipt,
        out string failureReason);
    bool AcknowledgeWipInput(
        string commitId,
        out string failureReason);
    bool CanSpawnOutput(
        string itemId,
        int amount,
        Vector2Int position,
        out DomainFailure failure);
    bool SpawnOutput(string itemId, int amount, Vector2Int position);
    void PrioritizeDestination(string destinationId);
    int ReleaseDestination(string destinationId, Vector2Int releasePosition);
    bool TryReleaseDestinationAtomically(
        string destinationId,
        Vector2Int releasePosition,
        out int released,
        out string failureReason);
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
    bool TryCommitBufferedOutput(
        string commitId,
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId,
        out DomainFailure failure);
    bool AcknowledgeBufferedOutput(
        string commitId,
        out DomainFailure failure);
    bool TryGetBufferedOutputCommitMassGrams(
        string commitId,
        out long massGrams,
        out DomainFailure failure);
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

public interface IProductionStockSensorPhysicalGateway
{
    bool CommitPending(
        string destinationId,
        string itemId,
        string operationId,
        string reasonCode,
        out ProductionStockSensorPhysicalReceipt receipt,
        out string failureReason);
    bool TryGetPending(
        string operationId,
        out ProductionStockSensorPhysicalReceipt receipt);
    bool Acknowledge(string commitId, out string failureReason);
}

public sealed class ProductionStockSensorPhysicalGateway :
    IProductionStockSensorPhysicalGateway
{
    private readonly IPhysicalFacilityItemSinkGateway physical;

    public ProductionStockSensorPhysicalGateway(
        IPhysicalFacilityItemSinkGateway physical)
    {
        this.physical = physical
            ?? throw new ArgumentNullException(nameof(physical));
    }

    public bool CommitPending(
        string destinationId,
        string itemId,
        string operationId,
        string reasonCode,
        out ProductionStockSensorPhysicalReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (!physical.TryCommitSinkPending(
                destinationId,
                itemId,
                1,
                operationId,
                reasonCode,
                out PhysicalItemBatchDispositionReceipt committed,
                out failureReason))
            return false;
        receipt = Map(committed);
        return receipt.IsCommitted;
    }

    public bool TryGetPending(
        string operationId,
        out ProductionStockSensorPhysicalReceipt receipt)
    {
        receipt = default;
        if (!physical.TryGetPending(operationId, out var committed))
            return false;
        receipt = Map(committed);
        return receipt.IsCommitted;
    }

    public bool Acknowledge(string commitId, out string failureReason) =>
        physical.Acknowledge(commitId, out failureReason);

    private static ProductionStockSensorPhysicalReceipt Map(
        PhysicalItemBatchDispositionReceipt value) => new(
        value.OperationId,
        value.ReasonCode,
        value.RequestFingerprint,
        value.CommitId,
        value.Quantity,
        value.InputMassGrams,
        value.SourceStackIds);
}

public sealed class ProductionItemGateway :
    IProductionItemGateway,
    IProductionOutputBufferGateway,
    IProductionSupplyInventoryGateway
{
    private readonly IStockQuery stock;
    private readonly IItemTransferService transfers;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IDungeonItemCatalogProvider itemCatalog;
    private readonly IFacilityBufferDestinationReleaseService destinationRelease;

    public ProductionItemGateway(
        IStockQuery stock,
        IItemTransferService transfers,
        IWorldItemStackRuntime worldItems,
        IDungeonItemCatalogProvider itemCatalog,
        IFacilityBufferDestinationReleaseService destinationRelease)
    {
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.itemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        this.destinationRelease = destinationRelease
            ?? throw new ArgumentNullException(nameof(destinationRelease));
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
        int worldQuantity = Count(
            itemId,
            destinationId,
            requiredState: null,
            excludeCarried: true,
            excludedDestinationId: string.Empty);
        int carriedQuantity = worldItems.GetCommittedHaulDeliveryQuantity(
            destinationId,
            itemId);
        return checked(worldQuantity + carriedQuantity);
    }

    public bool TryGetStockCategory(
        string itemId,
        out StockCategory category)
    {
        category = default;
        if (string.IsNullOrWhiteSpace(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || !itemCatalog.TryGetDefinition(
                itemId,
                out DungeonItemDefinition definition))
        {
            return false;
        }
        category = definition.StockCategory;
        return true;
    }

    public long CountPendingMassGrams(string destinationId)
    {
        string destination = destinationId ?? string.Empty;
        if (destination.Length == 0
            || !string.Equals(
                destination,
                destination.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical production destination ID is required.",
                nameof(destinationId));
        }

        long totalMassGrams = 0L;
        foreach (WorldItemStackSnapshot stack in worldItems.GetAllStacks()
                     .Where(stack => stack != null
                         && stack.Quantity > 0
                         && stack.State != WorldItemStackState.Carried
                         && string.Equals(
                             stack.DestinationId,
                             destination,
                             StringComparison.Ordinal))
                     .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            ItemDefinitionId itemId = (ItemDefinitionId)stack.ItemId;
            PhysicalItemMassSubject subject =
                PhysicalItemMassSubjectAdapter.Create(
                    worldItems.MassQuery,
                    itemId,
                    stack.ItemInstanceId,
                    stack.Components);
            totalMassGrams = checked(totalMassGrams
                + worldItems.MassQuery.GetQuantityMass(
                    itemId,
                    subject,
                    stack.Quantity).Value);
        }

        return checked(totalMassGrams
            + worldItems.GetCommittedHaulDeliveryMassGrams(destination));
    }

    public long GetDefinitionQuantityMassGrams(string itemId, int quantity)
    {
        string canonicalItemId = itemId ?? string.Empty;
        if (canonicalItemId.Length == 0
            || quantity <= 0
            || !string.Equals(
                canonicalItemId,
                canonicalItemId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical item ID and positive quantity are required.");
        }

        ItemDefinitionId definitionId = (ItemDefinitionId)canonicalItemId;
        return worldItems.MassQuery.GetQuantityMass(
            definitionId,
            PhysicalItemMassSubject.ForDefinition(definitionId),
            quantity).Value;
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
                && stack.AvailableQuantity > 0
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

    public bool SpawnOutput(string itemId, int amount, Vector2Int position)
    {
        return transfers.TrySpawnItem(
            itemId,
            amount,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            out int spawned)
            && spawned == amount;
    }

    public bool ConsumeDeliveredToWip(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        string operationId,
        out ProductionWipInputReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        string destination = destinationId ?? string.Empty;
        string operation = operationId ?? string.Empty;
        if (destination.Length == 0
            || operation.Length == 0
            || !string.Equals(destination, destination.Trim(), StringComparison.Ordinal)
            || !string.Equals(operation, operation.Trim(), StringComparison.Ordinal)
            || costs == null
            || costs.Count == 0
            || costs.Any(cost => string.IsNullOrWhiteSpace(cost.Key)
                || cost.Value <= 0))
        {
            failureReason = "production-wip-input-invalid-request";
            return false;
        }

        List<PhysicalItemTransformInput> inputs = new();
        foreach (KeyValuePair<string, int> cost in costs
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            int remaining = cost.Value;
            foreach (WorldItemStackSnapshot stack in stock.GetAllStacks()
                         .Where(stack => stack != null
                             && stack.State == WorldItemStackState.FacilityBuffer
                             && string.Equals(stack.DestinationId, destination,
                                 StringComparison.Ordinal)
                              && string.Equals(stack.ItemId, cost.Key,
                                  StringComparison.Ordinal)
                              && stack.AvailableQuantity > 0
                              && stack.ReservedQuantity == 0
                              && !stack.Forbidden)
                         .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
            {
                int selected = Math.Min(remaining, stack.AvailableQuantity);
                inputs.Add(new PhysicalItemTransformInput(stack.StackId, selected));
                remaining -= selected;
                if (remaining == 0)
                {
                    break;
                }
            }
            if (remaining != 0)
            {
                failureReason = "production-wip-input-missing:" + cost.Key;
                return false;
            }
        }

        if (!worldItems.TryCommitPendingBatchPhysicalDisposition(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operation,
                "production.inputs-to-wip",
                out PhysicalItemBatchDispositionReceipt physicalReceipt,
                out failureReason))
        {
            return false;
        }

        receipt = new ProductionWipInputReceipt(
            physicalReceipt.CommitId,
            physicalReceipt.Quantity,
            physicalReceipt.InputMassGrams);
        return receipt.IsCommitted;
    }

    public bool AcknowledgeWipInput(
        string commitId,
        out string failureReason) => worldItems
        .AcknowledgeBatchPhysicalDisposition(commitId, out failureReason);

    public bool CanSpawnOutput(
        string itemId,
        int amount,
        Vector2Int position,
        out DomainFailure failure)
    {
        string normalizedItemId = itemId?.Trim() ?? string.Empty;
        if (amount <= 0
            || string.IsNullOrWhiteSpace(normalizedItemId)
            || !itemCatalog.TryGetDefinition(normalizedItemId, out _))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                normalizedItemId,
                amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return false;
        }

        bool sourceContainmentOccupied = stock.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && stack.State == WorldItemStackState.Loose
            && string.IsNullOrWhiteSpace(stack.DestinationId)
            && stack.Position == position
            && string.Equals(
                stack.ItemId,
                normalizedItemId,
                StringComparison.Ordinal));
        if (sourceContainmentOccupied)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputSpaceUnavailable,
                normalizedItemId,
                position.x.ToString(System.Globalization.CultureInfo.InvariantCulture),
                position.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return false;
        }

        failure = DomainFailure.None;
        return true;
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

    public bool TryCommitBufferedOutput(
        string commitId,
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string commit = commitId ?? string.Empty;
        string item = itemId ?? string.Empty;
        string destination = destinationId ?? string.Empty;
        if (commit.Length == 0
            || item.Length == 0
            || destination.Length == 0
            || amount <= 0
            || !string.Equals(commit, commit.Trim(), StringComparison.Ordinal)
            || !string.Equals(item, item.Trim(), StringComparison.Ordinal)
            || !string.Equals(destination, destination.Trim(), StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                item,
                "commit-invalid");
            return false;
        }

        WorldItemStackSnapshot[] existing = worldItems.GetAllStacks()
            .Where(stack => stack != null
                && ProductionOutputCommitComponentCodec.Matches(
                    stack.Components,
                    commit))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        if (existing.Length > 0)
        {
            bool exact = existing.All(stack =>
                    string.Equals(stack.ItemId, item, StringComparison.Ordinal)
                    && stack.State == WorldItemStackState.FacilityOutputBuffer
                    && stack.Position == position
                    && string.Equals(
                        stack.DestinationId,
                        destination,
                        StringComparison.Ordinal))
                && existing.Sum(stack => stack.Quantity) == amount;
            if (!exact)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    item,
                    "commit-conflict");
            }
            return exact;
        }

        if (!worldItems.SpawnItemAtWithComponents(
                item,
                amount,
                position,
                WorldItemStackState.FacilityOutputBuffer,
                destination,
                new[] { ProductionOutputCommitComponentCodec.Create(commit) },
                out int spawned)
            || spawned != amount)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                item,
                "commit-spawn-failed");
            return false;
        }
        return true;
    }

    public bool AcknowledgeBufferedOutput(
        string commitId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string commit = commitId ?? string.Empty;
        if (commit.Length == 0
            || !string.Equals(commit, commit.Trim(), StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                "commit-ack-invalid");
            return false;
        }
        foreach (WorldItemStackSnapshot stack in worldItems.GetAllStacks()
                     .Where(stack => stack != null
                         && ProductionOutputCommitComponentCodec.Matches(
                             stack.Components,
                             commit))
                     .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (!worldItems.TryRemoveInstanceComponent(
                    stack.StackId,
                    ItemInstanceComponentIds.ProductionOutputCommit))
            {
                failure = new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    commit,
                    "commit-ack-failed");
                return false;
            }
        }
        return true;
    }

    public bool TryGetBufferedOutputCommitMassGrams(
        string commitId,
        out long massGrams,
        out DomainFailure failure)
    {
        massGrams = 0L;
        failure = DomainFailure.None;
        string commit = commitId ?? string.Empty;
        if (commit.Length == 0
            || !string.Equals(commit, commit.Trim(), StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                commit,
                "commit-invalid");
            return false;
        }
        WorldItemStackSnapshot[] stacks = worldItems.GetAllStacks()
            .Where(stack => stack != null
                && ProductionOutputCommitComponentCodec.Matches(
                    stack.Components,
                    commit))
            .ToArray();
        if (stacks.Length == 0)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                commit,
                "commit-missing");
            return false;
        }
        try
        {
            foreach (WorldItemStackSnapshot stack in stacks)
            {
                massGrams = checked(
                    massGrams
                    + PhysicalMassGrams.FromCanonicalKilograms(stack.UnitWeight)
                        .Multiply(stack.Quantity).Value);
            }
        }
        catch (Exception)
        {
            massGrams = 0L;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                commit,
                "commit-mass-invalid");
            return false;
        }
        return massGrams > 0L;
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

    public bool TryReleaseDestinationAtomically(
        string destinationId,
        Vector2Int releasePosition,
        out int released,
        out string failureReason) => destinationRelease.TryReleaseAtOwnerPosition(
            destinationId,
            releasePosition,
            "production-input-destination-cancelled",
            out released,
            out failureReason);

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
