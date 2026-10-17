using System;
using System.Collections.Generic;
using System.Linq;

internal static class PhysicalItemSaveValidationRules
{
    internal static bool AreCanonicalSortedUnique(
        IReadOnlyList<string> values)
    {
        if (values == null)
            return false;
        string previous = string.Empty;
        for (int index = 0; index < values.Count; index++)
        {
            string current = values[index];
            if (!IsCanonicalNonEmpty(current)
                || index > 0
                    && string.CompareOrdinal(previous, current) >= 0)
            {
                return false;
            }
            previous = current;
        }
        return true;
    }

    internal static bool IsSubset(
        IReadOnlyList<string> subset,
        IReadOnlyList<string> superset)
    {
        HashSet<string> allowed = new(
            superset ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        return (subset ?? Array.Empty<string>()).All(allowed.Contains);
    }

    internal static bool IsPrefix(
        IReadOnlyList<string> prefix,
        IReadOnlyList<string> full)
    {
        if (prefix == null || full == null || prefix.Count > full.Count)
            return false;
        for (int index = 0; index < prefix.Count; index++)
        {
            if (!string.Equals(
                    prefix[index],
                    full[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    internal static bool IsCanonicalNonEmpty(string value)
    {
        return !string.IsNullOrEmpty(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    internal static bool IsCanonicalText(string value)
    {
        return value != null
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    internal static bool IsLowerSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if ((current < '0' || current > '9')
                && (current < 'a' || current > 'f'))
            {
                return false;
            }
        }
        return true;
    }
}
