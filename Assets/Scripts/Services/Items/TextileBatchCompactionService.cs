using System;
using System.Collections.Generic;
using System.Linq;

public interface ITextileBatchCompactionService
{
    int CompactDestination(string destinationId);
}

/// <summary>
/// Bounds V22 raw-fiber, yarn and fabric entity counts. Only the authored
/// quality/condition signature participates in stacking; provenance history
/// is deliberately kept in the bounded ledger instead of the stack key.
/// </summary>
public sealed class TextileBatchCompactionService :
    ITextileBatchCompactionService
{
    private readonly WorldItemRepository repository;
    private readonly IDungeonItemCatalogProvider items;
    private readonly ITextileMaterialCatalog materials;

    public TextileBatchCompactionService(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider items,
        ITextileMaterialCatalog materials)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.materials = materials
            ?? throw new ArgumentNullException(nameof(materials));
    }

    public int CompactDestination(string destinationId)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            return 0;
        }

        int removed = 0;
        WorldItemStackRecord[] candidates = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && string.IsNullOrWhiteSpace(record.itemInstanceId)
                && record.reservedQuantity <= 0
                && string.Equals(
                    record.destinationId,
                    destination,
                    StringComparison.Ordinal)
                && IsTextileBatch(record))
            .OrderBy(record => record.stackId, StringComparer.Ordinal)
            .ToArray();
        foreach (IGrouping<string, WorldItemStackRecord> group in candidates
                     .GroupBy(StackingKey, StringComparer.Ordinal))
        {
            WorldItemStackRecord[] stacks = group
                .OrderBy(value => value.stackId, StringComparer.Ordinal)
                .ToArray();
            if (stacks.Length < 2
                || !items.TryGetDefinition(
                    stacks[0].itemId,
                    out DungeonItemDefinition definition))
            {
                continue;
            }
            int maxStack = Math.Max(1, definition.MaxStack);
            int targetIndex = 0;
            for (int sourceIndex = 1; sourceIndex < stacks.Length; sourceIndex++)
            {
                WorldItemStackRecord source = stacks[sourceIndex];
                while (source.quantity > 0 && targetIndex < sourceIndex)
                {
                    WorldItemStackRecord target = stacks[targetIndex];
                    int capacity = maxStack - target.quantity;
                    if (capacity <= 0)
                    {
                        targetIndex++;
                        continue;
                    }
                    int moved = Math.Min(capacity, source.quantity);
                    target.quantity += moved;
                    source.quantity -= moved;
                }
                if (source.quantity == 0)
                {
                    repository.Remove(source);
                    removed++;
                }
            }
        }
        if (removed > 0)
        {
            repository.MarkChanged();
        }
        return removed;
    }

    private bool IsTextileBatch(WorldItemStackRecord record)
    {
        if (record.itemId.StartsWith("fiber:", StringComparison.Ordinal)
            || record.itemId.StartsWith("yarn:", StringComparison.Ordinal)
            || string.Equals(record.itemId, "resource:wool", StringComparison.Ordinal)
            || string.Equals(record.itemId, "resource:shade-fiber", StringComparison.Ordinal))
        {
            return true;
        }
        return materials.TryGetByItemId(record.itemId, out _);
    }

    private static string StackingKey(WorldItemStackRecord record) =>
        string.Concat(
            record.itemId,
            "\u001f",
            record.state.ToString(),
            "\u001f",
            ItemStackSignature.Create(record.itemId, record.components));
}
