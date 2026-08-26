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
        ValidatePendingPhysicalCommit(state, byId, completed, report);
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

    private static void ValidatePendingPhysicalCommit(
        GrandProjectRuntimeState state,
        IReadOnlyDictionary<string, GrandProjectDefinition> byId,
        ISet<string> completed,
        DungeonGameRestoreReport report)
    {
        GrandProjectPhysicalCommitSaveData owner = state.pendingPhysicalCommit;
        if (owner == null || owner.sourceStackIds == null)
        {
            report.AddError(
                "Grand-project payload has no physical commit owner envelope.");
            return;
        }
        if (owner.phase == GrandProjectPhysicalCommitPhase.None)
        {
            if (!string.IsNullOrEmpty(owner.projectId)
                || !string.IsNullOrEmpty(owner.operationId)
                || !string.IsNullOrEmpty(owner.reasonCode)
                || !string.IsNullOrEmpty(owner.requestFingerprint)
                || !string.IsNullOrEmpty(owner.commitId)
                || owner.inputQuantity != 0
                || owner.inputMassGrams != 0L
                || owner.sourceStackIds.Count != 0
                || !string.IsNullOrEmpty(owner.stateBeforeFingerprint)
                || !string.IsNullOrEmpty(owner.stateAfterFingerprint))
                report.AddError(
                    "Grand-project empty physical owner contains residual data.");
            return;
        }
        if (!Enum.IsDefined(typeof(GrandProjectPhysicalCommitPhase), owner.phase)
            || !IsCanonical(owner.projectId)
            || !byId.ContainsKey(owner.projectId)
            || !string.Equals(
                owner.operationId,
                GrandProjectRuntime.BuildPhysicalOperationId(owner.projectId),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.reasonCode,
                GrandProjectRuntime.PhysicalReasonCode,
                StringComparison.Ordinal)
            || !IsCanonical(owner.requestFingerprint)
            || owner.inputQuantity <= 0
            || owner.inputMassGrams <= 0L
            || !string.Equals(
                owner.commitId,
                $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Sink}:{owner.operationId}:{owner.inputQuantity}:{owner.inputMassGrams}",
                StringComparison.Ordinal)
            || owner.sourceStackIds.Count == 0
            || owner.sourceStackIds.Any(id => !IsCanonical(id))
            || !owner.sourceStackIds.SequenceEqual(
                owner.sourceStackIds.OrderBy(id => id, StringComparer.Ordinal),
                StringComparer.Ordinal)
            || owner.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                != owner.sourceStackIds.Count
            || !IsCanonical(owner.stateBeforeFingerprint))
        {
            report.AddError("Grand-project physical owner is non-canonical or incomplete.");
            return;
        }

        string currentFingerprint = GrandProjectRuntime.CreateStateFingerprint(state);
        if (owner.phase == GrandProjectPhysicalCommitPhase.InputCommitted)
        {
            if (!string.Equals(state.activeProjectId, owner.projectId, StringComparison.Ordinal)
                || completed.Contains(owner.projectId)
                || !string.Equals(owner.stateBeforeFingerprint, currentFingerprint, StringComparison.Ordinal)
                || !string.IsNullOrEmpty(owner.stateAfterFingerprint))
                report.AddError(
                    "Grand-project input commit does not match its before-state envelope.");
            return;
        }
        if (!completed.Contains(owner.projectId)
            || !string.IsNullOrEmpty(state.activeProjectId)
            || !IsCanonical(owner.stateAfterFingerprint)
            || !string.Equals(owner.stateAfterFingerprint, currentFingerprint, StringComparison.Ordinal))
            report.AddError(
                "Grand-project published physical commit does not match its after-state envelope.");
    }
}
