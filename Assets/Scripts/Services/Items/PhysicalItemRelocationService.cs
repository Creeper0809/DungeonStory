using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public readonly struct PhysicalItemRelocationReceipt
{
    internal PhysicalItemRelocationReceipt(
        string operationId,
        string reasonCode,
        string sourceStackId,
        string destinationStackId,
        string itemId,
        int quantity,
        long massGrams,
        Vector2Int sourcePosition,
        Vector2Int destinationPosition)
    {
        OperationId = operationId;
        ReasonCode = reasonCode;
        SourceStackId = sourceStackId;
        DestinationStackId = destinationStackId;
        ItemId = itemId;
        Quantity = quantity;
        MassGrams = massGrams;
        SourcePosition = sourcePosition;
        DestinationPosition = destinationPosition;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public string SourceStackId { get; }
    public string DestinationStackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public Vector2Int SourcePosition { get; }
    public Vector2Int DestinationPosition { get; }
    public bool IsCommitted => OperationId?.Length > 0
        && ReasonCode?.Length > 0
        && SourceStackId?.Length > 0
        && DestinationStackId?.Length > 0
        && ItemId?.Length > 0
        && Quantity > 0
        && MassGrams > 0L;
}

public interface IPhysicalItemRelocationService
{
    bool TryRelocateQuantity(
        string sourceStackId,
        int quantity,
        Vector2Int destinationPosition,
        WorldItemStackState destinationState,
        string destinationId,
        string operationId,
        string reasonCode,
        out PhysicalItemRelocationReceipt receipt,
        out string failureReason);
}

/// <summary>
/// Exact identity-preserving movement inside the physical world. This is not
/// a Transform: input and output item identity, quantity, components, and gram
/// mass must remain equal.
/// </summary>
public sealed class PhysicalItemRelocationService : IPhysicalItemRelocationService
{
    private readonly WorldItemRepository repository;
    private readonly IWorldItemSpawner spawner;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemMarkerPresenter markers;

    public PhysicalItemRelocationService(
        WorldItemRepository repository,
        IWorldItemSpawner spawner,
        IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
    }

    public bool TryRelocateQuantity(
        string sourceStackId,
        int quantity,
        Vector2Int destinationPosition,
        WorldItemStackState destinationState,
        string destinationId,
        string operationId,
        string reasonCode,
        out PhysicalItemRelocationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        string sourceId = sourceStackId ?? string.Empty;
        string targetId = destinationId ?? string.Empty;
        string operation = operationId ?? string.Empty;
        string reason = reasonCode ?? string.Empty;
        if (quantity <= 0
            || !IsCanonicalRequired(sourceId)
            || !IsCanonicalRequired(operation)
            || !IsCanonicalRequired(reason)
            || !IsCanonicalOptional(targetId)
            || destinationState is WorldItemStackState.Carried
                or WorldItemStackState.InTransit)
        {
            failureReason = "physical-relocation-invalid-request";
            return false;
        }
        if (!repository.RecordsById.TryGetValue(sourceId, out WorldItemStackRecord source)
            || source == null
            || source.quantity < quantity
            || source.quantity - source.reservedQuantity < quantity
            || source.reservedQuantity > 0
            || !string.IsNullOrEmpty(source.reservedByPersistentId)
            || source.state is WorldItemStackState.Carried
                or WorldItemStackState.InTransit)
        {
            failureReason = "physical-relocation-source-unavailable";
            return false;
        }
        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                source.components))
        {
            failureReason =
                "physical-relocation-prepared-output-route-protected:"
                + FacilityOutputExactRouteFailureCode.ProtectedRouteBypass;
            return false;
        }
        if (quantity < source.quantity
            && (!string.IsNullOrEmpty(source.itemInstanceId)
                || source.components?.Count > 0))
        {
            failureReason = "physical-relocation-unique-partial-forbidden";
            return false;
        }

        Vector2Int sourcePosition = source.position;
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)source.itemId,
            source.itemInstanceId,
            source.components);
        long massGrams = massQuery.GetQuantityMass(
            (ItemDefinitionId)source.itemId,
            subject,
            quantity).Value;

        if (quantity == source.quantity)
        {
            WorldItemStackState oldState = source.state;
            string oldDestinationId = source.destinationId;
            string oldStorageDestinationId = source.sourceStorageDestinationId;
            bool oldHasDestinationPosition = source.hasDestinationPosition;
            Vector2Int oldDestinationPosition = source.destinationPosition;
            try
            {
                repository.Relocate(source, destinationPosition);
                source.state = destinationState;
                source.destinationId = targetId;
                source.sourceStorageDestinationId = string.Empty;
                source.hasDestinationPosition = targetId.Length > 0;
                source.destinationPosition = destinationPosition;
                repository.MarkChanged();
                markers.RefreshAt(sourcePosition);
                markers.RefreshAt(destinationPosition);
            }
            catch (Exception exception)
            {
                repository.Relocate(source, sourcePosition);
                source.state = oldState;
                source.destinationId = oldDestinationId;
                source.sourceStorageDestinationId = oldStorageDestinationId;
                source.hasDestinationPosition = oldHasDestinationPosition;
                source.destinationPosition = oldDestinationPosition;
                repository.MarkChanged();
                failureReason = "physical-relocation-rollback:" + exception.Message;
                return false;
            }

            receipt = new PhysicalItemRelocationReceipt(
                operation,
                reason,
                source.stackId,
                source.stackId,
                source.itemId,
                quantity,
                massGrams,
                sourcePosition,
                destinationPosition);
            return true;
        }

        Dictionary<string, int> destinationBefore = repository.RecordsByPosition
            .TryGetValue(destinationPosition, out List<WorldItemStackRecord> records)
                ? records.Where(record => record != null)
                    .ToDictionary(record => record.stackId, record => record.quantity,
                        StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal);
        int spawned = spawner.Spawn(
            source.itemId,
            quantity,
            destinationPosition,
            destinationState,
            targetId,
            hasDestinationPosition: targetId.Length > 0,
            destinationPosition: destinationPosition,
            sourceStorageDestinationId: string.Empty,
            components: source.components);
        if (spawned != quantity)
        {
            RollbackDestination(destinationPosition, destinationBefore);
            failureReason = "physical-relocation-output-commit-failed";
            return false;
        }

        try
        {
            source.quantity = checked(source.quantity - quantity);
            repository.MarkChanged();
            markers.RefreshAt(sourcePosition);
            markers.RefreshAt(destinationPosition);
        }
        catch (Exception exception)
        {
            RollbackDestination(destinationPosition, destinationBefore);
            source.quantity = checked(source.quantity + quantity);
            repository.MarkChanged();
            failureReason = "physical-relocation-rollback:" + exception.Message;
            return false;
        }

        WorldItemStackRecord destination = repository.RecordsByPosition[destinationPosition]
            .Where(record => record != null
                && string.Equals(record.itemId, source.itemId, StringComparison.Ordinal)
                && (!destinationBefore.TryGetValue(record.stackId, out int before)
                    || record.quantity > before))
            .OrderBy(record => record.stackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (destination == null)
        {
            throw new InvalidOperationException(
                $"Relocation '{operation}' committed without a destination stack.");
        }
        receipt = new PhysicalItemRelocationReceipt(
            operation,
            reason,
            source.stackId,
            destination.stackId,
            source.itemId,
            quantity,
            massGrams,
            sourcePosition,
            destinationPosition);
        return true;
    }

    private void RollbackDestination(
        Vector2Int position,
        IReadOnlyDictionary<string, int> quantitiesBefore)
    {
        WorldItemStackRecord[] current = repository.RecordsByPosition
            .TryGetValue(position, out List<WorldItemStackRecord> records)
                ? records.Where(record => record != null).ToArray()
                : Array.Empty<WorldItemStackRecord>();
        foreach (WorldItemStackRecord record in current)
        {
            if (quantitiesBefore.TryGetValue(record.stackId, out int quantity))
            {
                record.quantity = quantity;
            }
            else
            {
                repository.Remove(record);
            }
        }
        repository.MarkChanged();
        markers.RefreshAt(position);
    }

    private static bool IsCanonicalRequired(string value) =>
        value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalOptional(string value) =>
        string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
