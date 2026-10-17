#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

internal sealed class GameplayModuleSelectionRuntimeReplayResult
{
    internal GameplayModuleSelectionRuntimeReplayResult(
        JObject beforeCheckpoint,
        JObject appliedCheckpoint,
        JObject restoredCheckpoint,
        IEnumerable<string> resultFormulaIds,
        IEnumerable<string> evidenceInfluenceFactIds)
    {
        BeforeCheckpoint = Clone(beforeCheckpoint, nameof(beforeCheckpoint));
        AppliedCheckpoint = Clone(appliedCheckpoint, nameof(appliedCheckpoint));
        RestoredCheckpoint = Clone(restoredCheckpoint, nameof(restoredCheckpoint));
        ResultFormulaIds = RequireUnique(
            resultFormulaIds,
            nameof(resultFormulaIds));
        EvidenceInfluenceFactIds = RequireUnique(
            evidenceInfluenceFactIds,
            nameof(evidenceInfluenceFactIds));
    }

    internal JObject BeforeCheckpoint { get; }
    internal JObject AppliedCheckpoint { get; }
    internal JObject RestoredCheckpoint { get; }
    internal IReadOnlyList<string> ResultFormulaIds { get; }
    internal IReadOnlyList<string> EvidenceInfluenceFactIds { get; }

    private static JObject Clone(JObject value, string name) =>
        (JObject)(value ?? throw new ArgumentNullException(name)).DeepClone();

    private static string[] RequireUnique(
        IEnumerable<string> values,
        string name)
    {
        string[] result = (values ?? throw new ArgumentNullException(name))
            .Select(value => value?.Trim() ?? string.Empty)
            .ToArray();
        if (result.Length == 0
            || result.Any(string.IsNullOrWhiteSpace)
            || result.Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new InvalidOperationException(
                $"{name} must contain distinct non-empty identities.");
        }
        return result;
    }
}
#endif
