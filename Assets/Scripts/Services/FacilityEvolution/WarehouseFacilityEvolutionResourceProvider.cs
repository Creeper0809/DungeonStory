using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IFacilityEvolutionWarehouseInventoryQuery
{
    IReadOnlyList<IWarehouseFacility> GetWarehouses();
    bool TryGetPending(
        string operationId,
        string reasonCode,
        out FacilityEvolutionMaterialCommitReceipt receipt,
        out string failureReason);
    bool TryCommitPending(
        IReadOnlyList<FacilityEvolutionMaterialDebit> debits,
        string operationId,
        string reasonCode,
        out FacilityEvolutionMaterialCommitReceipt receipt,
        out string failureReason);
    bool Acknowledge(string commitId, out string failureReason);
}

public readonly struct FacilityEvolutionMaterialDebit
{
    public FacilityEvolutionMaterialDebit(StockCategory category, int amount)
    {
        Category = category;
        Amount = amount;
    }

    public StockCategory Category { get; }
    public int Amount { get; }
}

public sealed class RegistryFacilityEvolutionWarehouseInventoryQuery :
    IFacilityEvolutionWarehouseInventoryQuery
{
    private readonly IWarehouseWorldQuery warehouseWorld;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;

    public RegistryFacilityEvolutionWarehouseInventoryQuery(
        IWarehouseWorldQuery warehouseWorld,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService batchDispositions)
    {
        this.warehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
    }

    public bool TryGetPending(
        string operationId,
        string reasonCode,
        out FacilityEvolutionMaterialCommitReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (!batchDispositions.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt pending))
        {
            return false;
        }
        if (!string.Equals(pending.ReasonCode, reasonCode, StringComparison.Ordinal))
        {
            failureReason = "facility-evolution-material-operation-conflict:" + operationId;
            return false;
        }
        receipt = FromPhysical(pending);
        return receipt.IsCommitted;
    }

    public bool TryCommitPending(
        IReadOnlyList<FacilityEvolutionMaterialDebit> debits,
        string operationId,
        string reasonCode,
        out FacilityEvolutionMaterialCommitReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (TryGetPending(
                operationId,
                reasonCode,
                out receipt,
                out failureReason))
        {
            return true;
        }
        if (!string.IsNullOrEmpty(failureReason))
        {
            return false;
        }
        Dictionary<StockCategory, int> remainingByCategory = (debits
                ?? Array.Empty<FacilityEvolutionMaterialDebit>())
            .Where(debit => debit.Amount > 0)
            .GroupBy(debit => debit.Category)
            .ToDictionary(group => group.Key, group => group.Sum(debit => debit.Amount));
        if (remainingByCategory.Count == 0)
        {
            return true;
        }
        HashSet<string> destinations = GetWarehouses()
            .Select(WarehouseStorageIdentity.RequireDestinationId)
            .ToHashSet(StringComparer.Ordinal);
        List<PhysicalItemTransformInput> inputs = new();
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                      .Where(stack => stack != null
                          && stack.Quantity > 0
                          && stack.State == WorldItemStackState.Stored
                          && destinations.Contains(
                             string.IsNullOrWhiteSpace(
                                 stack.SourceStorageDestinationId)
                                 ? stack.DestinationId
                                 : stack.SourceStorageDestinationId))
                      .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            StockCategory category = items.CatalogProvider.GetDefinition(stack.ItemId)
                .StockCategory;
            if (!remainingByCategory.TryGetValue(category, out int remaining)
                || remaining <= 0)
            {
                continue;
            }
            int take = Math.Min(remaining, stack.Quantity);
            inputs.Add(new PhysicalItemTransformInput(stack.StackId, take));
            remaining -= take;
            remainingByCategory[category] = remaining;
            if (remainingByCategory.Values.All(value => value == 0))
            {
                break;
            }
        }
        if (remainingByCategory.Values.Any(value => value > 0))
        {
            failureReason = "facility-evolution-material-unavailable";
            return false;
        }
        if (!batchDispositions.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                reasonCode,
                out PhysicalItemBatchDispositionReceipt physicalReceipt,
                out failureReason)
            || !physicalReceipt.IsCommitted)
        {
            return false;
        }
        receipt = FromPhysical(physicalReceipt);
        return receipt.IsCommitted;
    }

    public bool Acknowledge(string commitId, out string failureReason) =>
        batchDispositions.Acknowledge(commitId, out failureReason);

    private static FacilityEvolutionMaterialCommitReceipt FromPhysical(
        PhysicalItemBatchDispositionReceipt receipt) => new(
        receipt.OperationId,
        receipt.ReasonCode,
        receipt.CommitId,
        receipt.SourceStackIds,
        receipt.Quantity,
        receipt.InputMassGrams);

    public IReadOnlyList<IWarehouseFacility> GetWarehouses()
    {
        return warehouseWorld.Warehouses
            .Where(warehouse => warehouse != null
                && warehouse.HasWarehouseInventory
                && warehouse.Inventory != null)
            .OrderBy(warehouse =>
                warehouse is BuildableObject building ? building.centerPos.y : int.MaxValue)
            .ThenBy(warehouse =>
                warehouse is BuildableObject building ? building.centerPos.x : int.MaxValue)
            .ThenBy(RequirePersistentId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RequirePersistentId(IWarehouseFacility warehouse)
    {
        BuildingInstanceId id = warehouse.PersistentInstanceId;
        if (!id.IsValid)
        {
            throw new InvalidOperationException(
                "Facility-evolution warehouse has no persistent building ID.");
        }
        return id.Value;
    }
}

public sealed class WarehouseFacilityEvolutionResourceProvider : IFacilityEvolutionResourceProvider
{
    private readonly IFacilityEvolutionWarehouseInventoryQuery inventoryQuery;

    public WarehouseFacilityEvolutionResourceProvider(
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery)
    {
        this.inventoryQuery = inventoryQuery
            ?? throw new ArgumentNullException(nameof(inventoryQuery));
    }

    public bool HasMaterial(string materialId, int amount)
    {
        if (string.IsNullOrWhiteSpace(materialId) || amount <= 0)
        {
            return true;
        }

        if (!StockCategoryPersistenceId.TryParse(materialId, out StockCategory category))
        {
            return false;
        }

        long available = 0;
        foreach (IWarehouseFacility warehouse in GetWarehouses())
        {
            available += warehouse.Inventory.GetStock(category);
            if (available >= amount)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetPendingMaterialCommit(
        string operationId,
        string reasonCode,
        out FacilityEvolutionMaterialCommitReceipt receipt,
        out string failureReason) => inventoryQuery.TryGetPending(
        operationId,
        reasonCode,
        out receipt,
        out failureReason);

    public bool TryCommitMaterialsPending(
        IReadOnlyList<FacilityEvolutionMaterialRequirement> requirements,
        string operationId,
        string reasonCode,
        out FacilityEvolutionMaterialCommitReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        FacilityEvolutionMaterialRequirement[] required = (requirements
                ?? Array.Empty<FacilityEvolutionMaterialRequirement>())
            .Where(requirement => !string.IsNullOrWhiteSpace(requirement.materialId))
            .ToArray();
        if (required.Length == 0)
        {
            return true;
        }
        List<FacilityEvolutionMaterialDebit> debits = new();
        foreach (IGrouping<string, FacilityEvolutionMaterialRequirement> group in required
                     .GroupBy(requirement => requirement.materialId, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            if (!StockCategoryPersistenceId.TryParse(group.Key, out StockCategory category))
            {
                failureReason = "facility-evolution-material-id-invalid:" + group.Key;
                return false;
            }
            debits.Add(new FacilityEvolutionMaterialDebit(
                category,
                group.Sum(requirement => Mathf.Max(1, requirement.amount))));
        }
        return inventoryQuery.TryCommitPending(
            debits,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);
    }

    public bool AcknowledgeMaterialCommit(string commitId, out string failureReason) =>
        string.IsNullOrEmpty(commitId)
            ? Succeed(out failureReason)
            : inventoryQuery.Acknowledge(commitId, out failureReason);

    private IWarehouseFacility[] GetWarehouses()
    {
        return inventoryQuery.GetWarehouses()
            .Where(warehouse => warehouse?.Inventory != null)
            .ToArray();
    }

    private static bool Succeed(out string failureReason)
    {
        failureReason = string.Empty;
        return true;
    }
}
