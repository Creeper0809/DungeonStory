using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Canonical physical-stack state for perishable food. Production, spoilage,
/// hauling and restore use this single schema instead of reconstructing fresh
/// state after a stack has entered exact-route custody.
/// </summary>
public static class FoodFreshnessComponentCodec
{
    public const int SchemaVersion = 2;
    public const string RemainingSecondsKey = "remaining-seconds";
    public const string PreservedKey = "preserved";

    public static ItemInstanceComponentSaveData Create(
        double remainingSeconds,
        bool preserved)
    {
        if (double.IsNaN(remainingSeconds)
            || double.IsInfinity(remainingSeconds)
            || remainingSeconds < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingSeconds));
        }

        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.Freshness,
            schemaVersion = SchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new()
                {
                    key = RemainingSecondsKey,
                    kind = ItemStateValueKind.Decimal,
                    decimalValue = remainingSeconds
                },
                new()
                {
                    key = PreservedKey,
                    kind = ItemStateValueKind.Boolean,
                    booleanValue = preserved
                }
            }
        };
    }

    public static bool TryRead(
        IEnumerable<ItemInstanceComponentSaveData> components,
        out double remainingSeconds,
        out bool preserved)
    {
        remainingSeconds = 0d;
        preserved = false;
        ItemInstanceComponentSaveData[] matches = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.Freshness,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || matches[0].schemaVersion != SchemaVersion
            || !matches[0].affectsStacking
            || matches[0].values == null
            || matches[0].values.Count != 2)
        {
            return false;
        }

        ItemStateValueSaveData[] remaining = (matches[0].values
                ?? new List<ItemStateValueSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.key,
                    RemainingSecondsKey,
                    StringComparison.Ordinal))
            .ToArray();
        ItemStateValueSaveData[] preservedValues = (matches[0].values
                ?? new List<ItemStateValueSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.key,
                    PreservedKey,
                    StringComparison.Ordinal))
            .ToArray();
        if (remaining.Length != 1
            || remaining[0].kind != ItemStateValueKind.Decimal
            || double.IsNaN(remaining[0].decimalValue)
            || double.IsInfinity(remaining[0].decimalValue)
            || remaining[0].decimalValue < 0d
            || preservedValues.Length != 1
            || preservedValues[0].kind != ItemStateValueKind.Boolean)
        {
            return false;
        }

        remainingSeconds = remaining[0].decimalValue;
        preserved = preservedValues[0].booleanValue;
        return true;
    }

    public static bool IsStrictCanonical(
        ItemInstanceComponentSaveData component) =>
        component != null
        && TryRead(
            new[] { component },
            out _,
            out _);
}
