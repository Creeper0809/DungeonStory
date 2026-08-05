using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class FacilityEvolutionRules
{
    public static Dictionary<string, int> BuildRequirements(
        FacilityModificationOrder order)
    {
        Dictionary<string, int> requirements =
            new Dictionary<string, int>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(order.bindingItemId)
            && order.bindingAmount > 0)
        {
            requirements[order.bindingItemId] = order.bindingAmount;
        }

        if (!string.IsNullOrWhiteSpace(order.catalystItemId)
            && order.catalystAmount > 0)
        {
            requirements[order.catalystItemId] = order.catalystAmount;
        }

        return requirements;
    }

    public static string ResolvePrimaryModuleId(
        CompactedHistorySegment segment)
    {
        string dominant = segment.metrics
            .OrderByDescending(metric => Mathf.Abs(metric.value))
            .ThenBy(metric => metric.metricId, StringComparer.Ordinal)
            .Select(metric => metric.metricId?.ToLowerInvariant() ?? string.Empty)
            .FirstOrDefault() ?? string.Empty;
        string tags = string.Join(
            "|",
            segment.sourceTags.Select(tag => tag.ToLowerInvariant()));
        string source = dominant + "|" + tags;
        if (ContainsAny(source, "defense", "combat", "guard", "intruder"))
        {
            return "facility:defense";
        }

        if (ContainsAny(source, "research", "arcane", "mana"))
        {
            return "facility:research";
        }

        if (ContainsAny(source, "survival", "food", "water", "clean", "medical"))
        {
            return "facility:survival";
        }

        if (ContainsAny(source, "entertainment", "circus", "perform"))
        {
            return "facility:entertainment";
        }

        if (ContainsAny(source, "visit", "service", "revenue", "shop"))
        {
            return "facility:service";
        }

        return "facility:output";
    }

    public static string ResolveFacilityModuleForCatalyst(string family)
    {
        string normalized = family?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("defense") || normalized.Contains("offense"))
        {
            return "facility:defense";
        }

        if (normalized.Contains("survival"))
        {
            return "facility:survival";
        }

        if (normalized.Contains("arcane"))
        {
            return "facility:research";
        }

        if (normalized.Contains("authority"))
        {
            return "facility:service";
        }

        return "facility:output";
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        return values.Any(value =>
            source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
