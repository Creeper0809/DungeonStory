using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CodexTextFormatter
{
    public static string FormatEvolutionMutationTags(IReadOnlyList<string> mutationTags)
    {
        return string.Join(", ", Canonicalize(mutationTags));
    }

    public static IReadOnlyList<string> Canonicalize(IEnumerable<string> values)
    {
        return (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
