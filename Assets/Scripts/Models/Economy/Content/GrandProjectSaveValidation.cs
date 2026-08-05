using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class GrandProjectSaveValidation
{
    public static void Validate(
        DungeonGrandProjectSaveData data,
        IReadOnlyList<GrandProjectDefinition> definitions,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (data == null || data.state == null)
        {
            report.AddError("Grand-project payload or runtime state is null.");
            return;
        }
        if (definitions == null)
        {
            report.AddError("Grand-project validation has no definition catalog.");
            return;
        }
        if (data.version != DungeonGrandProjectSaveData.CurrentVersion)
        {
            report.AddError(
                $"Grand-project payload version {data.version} is unsupported.");
        }

        Dictionary<string, GrandProjectDefinition> byId = definitions
            .Where(value => value != null && IsCanonical(value.ProjectId))
            .ToDictionary(
                value => value.ProjectId,
                value => value,
                StringComparer.Ordinal);
        GrandProjectRuntimeState state = data.state;
        if (state.lastStatus == null
            || !string.Equals(
                state.lastStatus,
                state.lastStatus.Trim(),
                StringComparison.Ordinal))
        {
            report.AddError("Grand-project payload has a non-canonical status.");
        }
        if (state.completedProjectIds == null)
        {
            report.AddError("Grand-project payload has no completed-project list.");
            return;
        }

        HashSet<string> completed = new(StringComparer.Ordinal);
        string previousId = string.Empty;
        foreach (string projectId in state.completedProjectIds)
        {
            if (!IsCanonical(projectId)
                || !byId.ContainsKey(projectId)
                || !completed.Add(projectId)
                || (previousId.Length > 0
                    && string.CompareOrdinal(previousId, projectId) >= 0))
            {
                report.AddError(
                    "Grand-project payload has an unknown, duplicate, unordered, or non-canonical completed project ID.");
            }
            previousId = projectId ?? string.Empty;
        }

        ValidateActiveState(state, byId, completed, report);
    }

    private static void ValidateActiveState(
        GrandProjectRuntimeState state,
        IReadOnlyDictionary<string, GrandProjectDefinition> byId,
        HashSet<string> completed,
        DungeonGameRestoreReport report)
    {
        string activeProjectId = state.activeProjectId ?? string.Empty;
        string destinationId = state.destinationId ?? string.Empty;
        if (activeProjectId.Length == 0)
        {
            if (destinationId.Length != 0 || state.completedWork != 0f)
            {
                report.AddError(
                    "Inactive grand-project state must have an empty destination and zero work.");
            }
            return;
        }

        if (!IsCanonical(activeProjectId)
            || !byId.TryGetValue(
                activeProjectId,
                out GrandProjectDefinition definition)
            || completed.Contains(activeProjectId))
        {
            report.AddError(
                "Grand-project payload has an unknown, completed, or non-canonical active project.");
            return;
        }
        string expectedDestination = $"grand-project:{activeProjectId}";
        if (!string.Equals(
            destinationId,
            expectedDestination,
            StringComparison.Ordinal))
        {
            report.AddError(
                $"Grand-project '{activeProjectId}' has a non-canonical destination.");
        }
        if (!IsFinite(state.completedWork)
            || state.completedWork < 0f
            || state.completedWork > definition.RequiredWork)
        {
            report.AddError(
                $"Grand-project '{activeProjectId}' has invalid completed work.");
        }
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
