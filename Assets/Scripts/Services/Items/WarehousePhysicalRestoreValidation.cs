using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class WarehousePhysicalRestoreAssessment
{
    internal static readonly WarehousePhysicalRestoreAssessment Empty =
        new(Array.Empty<string>());

    internal WarehousePhysicalRestoreAssessment(
        IReadOnlyList<string> overCapacityWarehouseIds)
    {
        OverCapacityWarehouseIds = overCapacityWarehouseIds
            ?? throw new ArgumentNullException(nameof(overCapacityWarehouseIds));
    }

    internal IReadOnlyList<string> OverCapacityWarehouseIds { get; }
}

public interface IWarehouseOverCapacityEvacuationQuery
{
    int Revision { get; }
    IReadOnlyList<string> CapturePendingWarehouseIds();
    bool IsPending(string warehouseDestinationId);
}

internal static class WarehousePhysicalRestoreValidation
{
    private const string MissingCandidateCode =
        "items.restore.warehouse_owner_missing";
    private const string PositionMismatchCode =
        "items.restore.warehouse_position_mismatch";
    private const string CategoryMismatchCode =
        "items.restore.warehouse_category_mismatch";

    internal static WarehousePhysicalRestoreAssessment Validate(
        IReadOnlyList<WorldItemStackRecord> records,
        IReadOnlyList<BuildableObject> candidateBuildings,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery)
    {
        if (records == null)
        {
            throw new ArgumentNullException(nameof(records));
        }
        if (candidateBuildings == null)
        {
            throw new ArgumentNullException(nameof(candidateBuildings));
        }
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }
        if (massQuery == null)
        {
            throw new ArgumentNullException(nameof(massQuery));
        }

        Dictionary<string, WarehouseOwner> owners = BuildOwners(candidateBuildings);
        Dictionary<string, long> storedMassByOwner = new(StringComparer.Ordinal);
        foreach (WorldItemStackRecord record in records)
        {
            if (record == null || record.quantity <= 0)
            {
                continue;
            }

            DungeonItemDefinition definition = catalog.GetDefinition(record.itemId);
            string sourceStorageId = record.sourceStorageDestinationId ?? string.Empty;
            string destinationId = record.destinationId ?? string.Empty;
            bool hasSourceWarehouse = TryRequireWarehouseOwner(
                sourceStorageId,
                owners,
                out WarehouseOwner sourceOwner);
            bool hasDestinationWarehouse = TryRequireWarehouseOwner(
                destinationId,
                owners,
                out WarehouseOwner destinationOwner);

            if (sourceStorageId.StartsWith(
                    WarehouseStorageIdentity.DestinationPrefix,
                    StringComparison.Ordinal))
            {
                RequireCategory(sourceOwner, definition, record.stackId);
                RequirePosition(
                    sourceOwner,
                    record.position,
                    record.stackId,
                    "source-storage");
            }

            if (destinationId.StartsWith(
                    WarehouseStorageIdentity.DestinationPrefix,
                    StringComparison.Ordinal))
            {
                RequireCategory(destinationOwner, definition, record.stackId);
                bool storedAtHome = record.state == WorldItemStackState.Stored
                    && string.IsNullOrEmpty(sourceStorageId);
                if (!storedAtHome && !record.hasDestinationPosition)
                {
                    throw new InvalidOperationException(
                        $"{PositionMismatchCode}: stack '{record.stackId}' has no warehouse destination position for '{destinationOwner.DestinationId}'.");
                }
                if (record.hasDestinationPosition)
                {
                    RequirePosition(
                        destinationOwner,
                        record.destinationPosition,
                        record.stackId,
                        "destination");
                }
            }

            if (record.state != WorldItemStackState.Stored)
            {
                continue;
            }

            WarehouseOwner storageOwner;
            if (hasSourceWarehouse)
            {
                storageOwner = sourceOwner;
            }
            else if (hasDestinationWarehouse)
            {
                storageOwner = destinationOwner;
                RequirePosition(
                    storageOwner,
                    record.position,
                    record.stackId,
                    "stored-stack");
            }
            else
            {
                throw new InvalidOperationException(
                    $"{MissingCandidateCode}: Stored stack '{record.stackId}' has no exact warehouse owner.");
            }

            long stackMass = massQuery
                .GetDefinitionUnitMass((ItemDefinitionId)record.itemId)
                .Multiply(record.quantity)
                .Value;
            storedMassByOwner.TryGetValue(storageOwner.DestinationId, out long existing);
            storedMassByOwner[storageOwner.DestinationId] = checked(existing + stackMass);
        }

        // A valid warehouse may restore above its authored capacity. The physical
        // stock remains authoritative; RemainingMassGrams becomes zero and blocks
        // subsequent reservations until ordinary hauling relieves the excess.
        List<string> overCapacityWarehouseIds = new();
        foreach (KeyValuePair<string, long> stored in storedMassByOwner
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            WarehouseOwner owner = owners[stored.Key];
            if (owner.Inventory.HasMassCapacityAuthority
                && stored.Value > owner.Inventory.MaxMassGrams)
            {
                overCapacityWarehouseIds.Add(stored.Key);
            }
        }
        return overCapacityWarehouseIds.Count == 0
            ? WarehousePhysicalRestoreAssessment.Empty
            : new WarehousePhysicalRestoreAssessment(
                overCapacityWarehouseIds.AsReadOnly());
    }

    private static Dictionary<string, WarehouseOwner> BuildOwners(
        IReadOnlyList<BuildableObject> candidateBuildings)
    {
        Dictionary<string, WarehouseOwner> owners = new(StringComparer.Ordinal);
        foreach (BuildableObject building in candidateBuildings
                     .Where(candidate => candidate != null)
                     .OrderBy(candidate => candidate.PersistentInstanceId.Value,
                         StringComparer.Ordinal))
        {
            if (building is not IWarehouseFacility warehouse
                || !warehouse.HasWarehouseInventory
                || warehouse.Inventory == null)
            {
                continue;
            }

            BuildingInstanceId ownerId = building.PersistentInstanceId;
            if (!ownerId.IsValid)
            {
                throw new InvalidOperationException(
                    $"{MissingCandidateCode}: candidate warehouse has no persistent owner ID.");
            }

            string destinationId = WarehouseStorageIdentity.DestinationPrefix
                + ownerId.Value;
            if (!owners.TryAdd(
                    destinationId,
                    new WarehouseOwner(
                        destinationId,
                        building.centerPos,
                        warehouse.Inventory)))
            {
                throw new InvalidOperationException(
                    $"{MissingCandidateCode}: duplicate candidate warehouse '{destinationId}'.");
            }
        }

        return owners;
    }

    private static bool TryRequireWarehouseOwner(
        string destinationId,
        IReadOnlyDictionary<string, WarehouseOwner> owners,
        out WarehouseOwner owner)
    {
        owner = default;
        if (!destinationId.StartsWith(
                WarehouseStorageIdentity.DestinationPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }
        if (!owners.TryGetValue(destinationId, out owner))
        {
            throw new InvalidOperationException(
                $"{MissingCandidateCode}: '{destinationId}' does not resolve to the detached facility candidate.");
        }
        return true;
    }

    private static void RequireCategory(
        WarehouseOwner owner,
        DungeonItemDefinition definition,
        string stackId)
    {
        if (!owner.Inventory.Accepts(definition.StockCategory))
        {
            throw new InvalidOperationException(
                $"{CategoryMismatchCode}: stack '{stackId}' category '{definition.StockCategory}' is not accepted by '{owner.DestinationId}'.");
        }
    }

    private static void RequirePosition(
        WarehouseOwner owner,
        Vector2Int actual,
        string stackId,
        string field)
    {
        if (actual != owner.Position)
        {
            throw new InvalidOperationException(
                $"{PositionMismatchCode}: stack '{stackId}' {field}={actual} expected={owner.Position} for '{owner.DestinationId}'.");
        }
    }

    private readonly struct WarehouseOwner
    {
        internal WarehouseOwner(
            string destinationId,
            Vector2Int position,
            WarehouseInventory inventory)
        {
            DestinationId = destinationId;
            Position = position;
            Inventory = inventory;
        }

        internal string DestinationId { get; }
        internal Vector2Int Position { get; }
        internal WarehouseInventory Inventory { get; }
    }
}
