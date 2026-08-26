using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IWorldItemSpawner
{
    int Spawn(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition = false,
        Vector2Int destinationPosition = default,
        string sourceCharacterId = "",
        string sourceDisplayName = "",
        string sourceSpeciesTag = "",
        string sourceDeathReason = "",
        bool emergencyButcheryAllowed = false,
        string sourceStorageDestinationId = "",
        WasteOriginKind wasteOrigin = WasteOriginKind.Unknown,
        float contamination = 0f,
        IReadOnlyList<ItemInstanceComponentSaveData> components = null);

    bool SpawnUnique(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId);
    bool SpawnUnique(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        Vector2Int destinationPosition,
        out string stackId);
    bool SpawnExistingUnique(
        string itemId,
        ItemInstanceId itemInstanceId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition,
        Vector2Int destinationPosition,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out string stackId);
}

public sealed class WorldItemSpawner : IWorldItemSpawner
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly WorldItemRepository repository;
    private readonly IItemMarkerPresenter markerPresenter;

    public WorldItemSpawner(
        IDungeonItemCatalogProvider catalogProvider,
        WorldItemRepository repository,
        IItemMarkerPresenter markerPresenter)
    {
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.markerPresenter = markerPresenter
            ?? throw new ArgumentNullException(nameof(markerPresenter));
    }

    public int Spawn(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition = false,
        Vector2Int destinationPosition = default,
        string sourceCharacterId = "",
        string sourceDisplayName = "",
        string sourceSpeciesTag = "",
        string sourceDeathReason = "",
        bool emergencyButcheryAllowed = false,
        string sourceStorageDestinationId = "",
        WasteOriginKind wasteOrigin = WasteOriginKind.Unknown,
        float contamination = 0f,
        IReadOnlyList<ItemInstanceComponentSaveData> components = null)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return 0;
        }
        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(components))
        {
            return 0;
        }

        string normalizedItemId = itemId.Trim();
        if (RequiresAuthoritativeEquipmentInstance(normalizedItemId))
        {
            return 0;
        }

        int remaining = amount;
        int spawned = 0;
        int maxStack = catalogProvider.GetDefinition(normalizedItemId).MaxStack;
        List<ItemInstanceComponentSaveData> instanceComponents =
            BuildInstanceComponents(
                normalizedItemId,
                components,
                sourceCharacterId,
                sourceSpeciesTag,
                contamination);
        string stackSignature = ItemStackSignature.Create(
            normalizedItemId,
            instanceComponents);
        while (remaining > 0)
        {
            int amountForStack = Mathf.Min(remaining, maxStack);
            WorldItemStackRecord mergeTarget = FindMergeTarget(
                normalizedItemId,
                position,
                state,
                destinationId,
                hasDestinationPosition,
                destinationPosition,
                maxStack,
                sourceCharacterId,
                sourceDisplayName,
                sourceSpeciesTag,
                sourceDeathReason,
                emergencyButcheryAllowed,
                sourceStorageDestinationId,
                wasteOrigin,
                contamination,
                stackSignature);
            if (mergeTarget != null)
            {
                int merged = Mathf.Min(
                    amountForStack,
                    maxStack - mergeTarget.quantity);
                mergeTarget.quantity += merged;
                repository.MarkChanged();
                amountForStack -= merged;
                remaining -= merged;
                spawned += merged;
            }

            if (amountForStack <= 0)
            {
                continue;
            }

            repository.Add(new WorldItemStackRecord
            {
                stackId = repository.AllocateStackId(),
                itemInstanceId = maxStack == 1
                    ? repository.AllocateItemInstanceId()
                    : string.Empty,
                itemId = normalizedItemId,
                quantity = amountForStack,
                state = state,
                position = position,
                destinationId = destinationId ?? string.Empty,
                sourceStorageDestinationId =
                    sourceStorageDestinationId ?? string.Empty,
                hasDestinationPosition = hasDestinationPosition,
                destinationPosition = destinationPosition,
                sourceCharacterId = sourceCharacterId ?? string.Empty,
                sourceDisplayName = sourceDisplayName ?? string.Empty,
                sourceSpeciesTag = sourceSpeciesTag ?? string.Empty,
                sourceDeathReason = sourceDeathReason ?? string.Empty,
                emergencyButcheryAllowed = emergencyButcheryAllowed,
                wasteOrigin = wasteOrigin,
                contamination = Mathf.Clamp(contamination, 0f, 100f),
                components = instanceComponents.Select(component => component.Clone()).ToList()
            });
            remaining -= amountForStack;
            spawned += amountForStack;
        }

        markerPresenter.RefreshAt(position);
        return spawned;
    }

    public bool SpawnUnique(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId)
    {
        return SpawnUniqueInternal(
            itemId,
            position,
            state,
            destinationId,
            false,
            default,
            out stackId);
    }

    public bool SpawnExistingUnique(
        string itemId,
        ItemInstanceId itemInstanceId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition,
        Vector2Int destinationPosition,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out string stackId)
    {
        stackId = string.Empty;
        string normalizedItemId = itemId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedItemId)
            || !itemInstanceId.IsValid
            || catalogProvider.GetDefinition(normalizedItemId).MaxStack != 1
            || repository.Records.Any(record => record != null
                && string.Equals(
                    record.itemInstanceId,
                    itemInstanceId.Value,
                    StringComparison.Ordinal)))
        {
            return false;
        }

        WorldItemStackRecord created = new WorldItemStackRecord
        {
            stackId = repository.AllocateStackId(),
            itemInstanceId = itemInstanceId.Value,
            itemId = normalizedItemId,
            quantity = 1,
            state = state,
            position = position,
            destinationId = destinationId?.Trim() ?? string.Empty,
            hasDestinationPosition = hasDestinationPosition,
            destinationPosition = destinationPosition,
            components = (components ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Where(component => component != null)
                .Select(component => component.Clone())
                .ToList()
        };
        repository.Add(created);
        markerPresenter.RefreshAt(position);
        stackId = created.stackId;
        return true;
    }

    public bool SpawnUnique(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        Vector2Int destinationPosition,
        out string stackId)
    {
        return SpawnUniqueInternal(
            itemId,
            position,
            state,
            destinationId,
            true,
            destinationPosition,
            out stackId);
    }

    private bool SpawnUniqueInternal(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition,
        Vector2Int destinationPosition,
        out string stackId)
    {
        stackId = string.Empty;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        string normalizedItemId = itemId.Trim();
        if (RequiresAuthoritativeEquipmentInstance(normalizedItemId))
        {
            return false;
        }

        HashSet<string> existingIds = repository.Records
            .Where(record => record != null)
            .Select(record => record.stackId)
            .ToHashSet(StringComparer.Ordinal);
        int spawned = Spawn(
            normalizedItemId,
            1,
            position,
            state,
            destinationId ?? string.Empty,
            hasDestinationPosition,
            destinationPosition);
        WorldItemStackRecord created = repository.Records.LastOrDefault(record =>
            record != null && !existingIds.Contains(record.stackId));
        if (spawned != 1 || created == null)
        {
            return false;
        }

        stackId = created.stackId;
        return true;
    }

    private static bool RequiresAuthoritativeEquipmentInstance(string itemId)
    {
        return PhysicalItemIds.TryGetEquipmentDefinitionId(itemId, out _)
            || PhysicalItemIds.IsEquipmentModule(itemId);
    }

    private WorldItemStackRecord FindMergeTarget(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition,
        Vector2Int destinationPosition,
        int maxStack,
        string sourceCharacterId,
        string sourceDisplayName,
        string sourceSpeciesTag,
        string sourceDeathReason,
        bool emergencyButcheryAllowed,
        string sourceStorageDestinationId,
        WasteOriginKind wasteOrigin,
        float contamination,
        string stackSignature)
    {
        if (!repository.RecordsByPosition.TryGetValue(
                position,
                out List<WorldItemStackRecord> positionStacks))
        {
            return null;
        }

        return positionStacks.FirstOrDefault(stack => stack != null
            && stack.quantity > 0
            && stack.dropDisposition == WorldItemDropDisposition.None
            && stack.quantity < maxStack
            && stack.state == state
            && string.Equals(
                stack.itemId,
                itemId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.destinationId ?? string.Empty,
                destinationId ?? string.Empty,
                StringComparison.Ordinal)
            && string.Equals(
                stack.sourceStorageDestinationId ?? string.Empty,
                sourceStorageDestinationId ?? string.Empty,
                StringComparison.Ordinal)
            && stack.reservedQuantity <= 0
            && stack.hasDestinationPosition == hasDestinationPosition
            && (!hasDestinationPosition
                || stack.destinationPosition == destinationPosition)
            && string.Equals(
                stack.sourceCharacterId ?? string.Empty,
                sourceCharacterId ?? string.Empty,
                StringComparison.Ordinal)
            && string.Equals(
                stack.sourceDisplayName ?? string.Empty,
                sourceDisplayName ?? string.Empty,
                StringComparison.Ordinal)
            && string.Equals(
                stack.sourceSpeciesTag ?? string.Empty,
                sourceSpeciesTag ?? string.Empty,
                StringComparison.Ordinal)
            && string.Equals(
                stack.sourceDeathReason ?? string.Empty,
                sourceDeathReason ?? string.Empty,
                StringComparison.Ordinal)
            && stack.emergencyButcheryAllowed == emergencyButcheryAllowed
            && stack.wasteOrigin == wasteOrigin
            && Mathf.Abs(stack.contamination - contamination) < 0.01f
            && string.Equals(
                ItemStackSignature.Create(stack.itemId, stack.components),
                stackSignature,
                StringComparison.Ordinal));
    }

    private static List<ItemInstanceComponentSaveData> BuildInstanceComponents(
        string itemId,
        IReadOnlyList<ItemInstanceComponentSaveData> authored,
        string sourceCharacterId,
        string sourceSpeciesTag,
        float contamination)
    {
        List<ItemInstanceComponentSaveData> components = (authored
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .Select(component => component.Clone())
            .ToList();

        if (DurableToolItemRules.TryGetMaximumDurability(itemId, out _)
            && components.All(component => component.componentTypeId
                != ItemInstanceComponentIds.Durability))
        {
            components.Add(DurableToolItemRules.CreateDurability(itemId));
        }

        if (contamination > 0f
            && components.All(component => component.componentTypeId
                != ItemInstanceComponentIds.Contamination))
        {
            components.Add(new ItemInstanceComponentSaveData
            {
                componentTypeId = ItemInstanceComponentIds.Contamination,
                values = new List<ItemStateValueSaveData>
                {
                    new ItemStateValueSaveData
                    {
                        key = "percent",
                        kind = ItemStateValueKind.Decimal,
                        decimalValue = Mathf.Clamp(contamination, 0f, 100f)
                    }
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(sourceCharacterId)
            && components.All(component => component.componentTypeId
                != ItemInstanceComponentIds.Provenance))
        {
            components.Add(new ItemInstanceComponentSaveData
            {
                componentTypeId = ItemInstanceComponentIds.Provenance,
                values = new List<ItemStateValueSaveData>
                {
                    new ItemStateValueSaveData
                    {
                        key = "source-character-id",
                        kind = ItemStateValueKind.String,
                        stringValue = sourceCharacterId.Trim()
                    },
                    new ItemStateValueSaveData
                    {
                        key = "species",
                        kind = ItemStateValueKind.String,
                        stringValue = sourceSpeciesTag?.Trim() ?? string.Empty
                    }
                }
            });
        }

        return components;
    }
}
