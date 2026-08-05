using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Owns research-project queue mutation and active-project selection. The scene
/// runtime remains responsible for work execution, rewards, and world projection.
/// </summary>
public sealed class BlueprintResearchProjectCoordinator
{
    public BlueprintResearchProjectCoordinator(
        IResearchProjectCatalog projectCatalog,
        IResearchBlueprintArchiveQuery blueprintArchiveQuery,
        IResearchFacilityCapacityQuery researchFacilityCapacity)
    {
        ProjectCatalog = projectCatalog
            ?? throw new ArgumentNullException(nameof(projectCatalog));
        BlueprintArchiveQuery = blueprintArchiveQuery
            ?? throw new ArgumentNullException(nameof(blueprintArchiveQuery));
        ResearchFacilityCapacity = researchFacilityCapacity
            ?? throw new ArgumentNullException(nameof(researchFacilityCapacity));
    }

    public IResearchProjectCatalog ProjectCatalog { get; }
    public IResearchBlueprintArchiveQuery BlueprintArchiveQuery { get; }
    public IResearchFacilityCapacityQuery ResearchFacilityCapacity { get; }

    public ResearchQueueCommandResult Enqueue(
        BlueprintResearchState state,
        ResearchProjectId projectId)
    {
        RequireState(state);
        if (!ProjectCatalog.TryGet(projectId, out ResearchProjectSO project))
        {
            return new ResearchQueueCommandResult(
                false,
                "연구 프로젝트를 찾을 수 없습니다.");
        }
        if (state.Projects.IsCompleted(projectId))
        {
            return new ResearchQueueCommandResult(
                false,
                "이미 완료된 연구입니다.");
        }
        if (project.BlueprintRule == ResearchBlueprintRule.Required
            && !HasArchivedBlueprint(project, out string blueprintBlocker))
        {
            return new ResearchQueueCommandResult(false, blueprintBlocker);
        }

        List<ResearchProjectSO> ordered = new List<ResearchProjectSO>();
        CollectQueueDependencies(
            project,
            ordered,
            new HashSet<string>(StringComparer.Ordinal));
        ResearchProjectSO blockedDependency = ordered.FirstOrDefault(candidate =>
            candidate.BlueprintRule == ResearchBlueprintRule.Required
            && !HasArchivedBlueprint(candidate, out _));
        if (blockedDependency != null)
        {
            HasArchivedBlueprint(blockedDependency, out string dependencyBlocker);
            return new ResearchQueueCommandResult(
                false,
                $"{blockedDependency.DisplayName}: {dependencyBlocker}");
        }

        List<ResearchProjectId> added = new List<ResearchProjectId>();
        foreach (ResearchProjectSO candidate in ordered)
        {
            if (state.Projects.IsCompleted(candidate.ProjectId)
                || state.Projects.ContainsInQueue(candidate.ProjectId))
            {
                continue;
            }
            state.Projects.AddQueueEntry(candidate.ProjectId);
            added.Add(candidate.ProjectId);
        }

        if (added.Count == 0)
        {
            return new ResearchQueueCommandResult(
                false,
                "이미 연구 대기열에 등록되어 있습니다.");
        }

        TryResolveActive(state, out _, out _);
        return new ResearchQueueCommandResult(
            true,
            $"{project.DisplayName} 연구 경로를 대기열에 등록했습니다.",
            added);
    }

    public ResearchQueueCommandResult Remove(
        BlueprintResearchState state,
        ResearchProjectId projectId)
    {
        RequireState(state);
        bool removed = state.Projects.RemoveQueueEntry(projectId);
        if (removed)
        {
            TryResolveActive(state, out _, out _);
        }
        return new ResearchQueueCommandResult(
            removed,
            removed
                ? "연구 대기열에서 제거했습니다. 진행률은 보존됩니다."
                : "대기열에 등록된 연구가 아닙니다.");
    }

    public ResearchQueueCommandResult Move(
        BlueprintResearchState state,
        int fromIndex,
        int toIndex)
    {
        RequireState(state);
        ResearchQueueEntry[] before = state.Projects.Queue.ToArray();
        if (!state.Projects.MovePending(fromIndex, toIndex))
        {
            return new ResearchQueueCommandResult(
                false,
                "활성 연구는 이동할 수 없습니다.");
        }

        if (!IsQueueOrderValid(state))
        {
            int currentIndex = state.Projects.Queue
                .Select((entry, index) => (entry, index))
                .First(pair => pair.entry == before[fromIndex])
                .index;
            state.Projects.MovePending(currentIndex, fromIndex);
            return new ResearchQueueCommandResult(
                false,
                "선행 연구보다 앞으로 이동할 수 없습니다.");
        }

        return new ResearchQueueCommandResult(
            true,
            "연구 대기 순서를 변경했습니다.");
    }

    public bool TryResolveActive(
        BlueprintResearchState state,
        out ResearchProjectSO project,
        out string blocker)
    {
        RequireState(state);
        project = null;
        blocker = string.Empty;
        if (ProjectCatalog.Projects.Count == 0)
        {
            return false;
        }

        ResearchProjectId currentId = state.Projects.ActiveProjectId;
        if (currentId.IsValid
            && ProjectCatalog.TryGet(currentId, out ResearchProjectSO current))
        {
            blocker = GetExecutionBlocker(state, current);
            ResearchQueueEntry currentEntry = state.Projects.Queue
                .FirstOrDefault(entry => entry.ProjectId.Equals(currentId));
            currentEntry?.SetSuspended(blocker);
            if (string.IsNullOrWhiteSpace(blocker))
            {
                project = current;
                return true;
            }
            state.Projects.SetActive(default);
        }

        foreach (ResearchQueueEntry entry in state.Projects.Queue)
        {
            if (!ProjectCatalog.TryGet(
                    entry.ProjectId,
                    out ResearchProjectSO candidate))
            {
                entry.SetSuspended("연구 정의가 사라졌습니다.");
                continue;
            }

            string candidateBlocker = GetExecutionBlocker(state, candidate);
            entry.SetSuspended(candidateBlocker);
            if (!string.IsNullOrWhiteSpace(candidateBlocker))
            {
                continue;
            }

            state.Projects.SetActive(candidate.ProjectId);
            project = candidate;
            return true;
        }

        state.Projects.SetActive(default);
        blocker = state.Projects.Queue.FirstOrDefault()?.SuspendedReason
            ?? "실행 가능한 연구가 없습니다.";
        return false;
    }

    public string GetExecutionBlocker(
        BlueprintResearchState state,
        ResearchProjectSO project)
    {
        RequireState(state);
        if (project == null)
        {
            return "연구 정의가 없습니다.";
        }
        bool archived = HasArchivedBlueprint(project, out string blueprintBlocker);
        if (project.BlueprintRule == ResearchBlueprintRule.Shortcut && archived)
        {
            return !ResearchFacilityCapacity.MeetsRequirements(
                    project,
                    out string shortcutFacilityBlocker)
                ? shortcutFacilityBlocker
                : string.Empty;
        }
        ResearchProjectSO missing = project.Prerequisites
            .FirstOrDefault(required =>
                !state.Projects.IsCompleted(required.ProjectId));
        if (missing != null)
        {
            return $"선행 연구 대기: {missing.DisplayName}";
        }
        if (project.BlueprintRule == ResearchBlueprintRule.Required && !archived)
        {
            return blueprintBlocker;
        }
        if (!ResearchFacilityCapacity.MeetsRequirements(
                project,
                out string facilityBlocker))
        {
            return facilityBlocker;
        }
        return string.Empty;
    }

    public bool ArePrerequisitesCompleted(
        BlueprintResearchState state,
        ResearchProjectSO project)
    {
        RequireState(state);
        return ResearchProjectCoordinatorRules.ArePrerequisitesCompleted(
            state.Projects,
            project);
    }

    public ResearchNodeState EvaluateNodeState(
        BlueprintResearchState state,
        ResearchProjectSO project,
        out string blocker)
    {
        RequireState(state);
        if (project == null)
        {
            ResearchNodeEvaluation missingProject =
                ResearchProjectCoordinatorRules.EvaluateNodeState(
                    projectExists: false,
                    completed: false,
                    active: false,
                    queued: false,
                    queueSuspended: false,
                    queueBlocker: string.Empty,
                    blueprintRule: ResearchBlueprintRule.None,
                    blueprintArchived: false,
                    blueprintInTransit: false,
                    blueprintBlocker: string.Empty,
                    prerequisitesComplete: false,
                    prerequisiteBlocker: "Research project definition is missing.",
                    facilityRequirementsMet: false,
                    facilityBlocker: string.Empty);
            blocker = missingProject.Blocker;
            return missingProject.State;
        }

        bool completed = state.Projects.IsCompleted(project.ProjectId);
        bool active = state.Projects.ActiveProjectId.Equals(project.ProjectId);
        ResearchQueueEntry queuedEntry = state.Projects.Queue
            .FirstOrDefault(entry => entry.ProjectId.Equals(project.ProjectId));
        if (completed || active || queuedEntry != null)
        {
            ResearchNodeEvaluation current =
                ResearchProjectCoordinatorRules.EvaluateNodeState(
                    projectExists: true,
                    completed: completed,
                    active: active,
                    queued: queuedEntry != null,
                    queueSuspended: queuedEntry?.IsSuspended ?? false,
                    queueBlocker: queuedEntry?.SuspendedReason,
                    blueprintRule: project.BlueprintRule,
                    blueprintArchived: false,
                    blueprintInTransit: false,
                    blueprintBlocker: string.Empty,
                    prerequisitesComplete: false,
                    prerequisiteBlocker: string.Empty,
                    facilityRequirementsMet: false,
                    facilityBlocker: string.Empty);
            blocker = current.Blocker;
            return current.State;
        }

        bool archived = HasArchivedBlueprint(
            project,
            out string blueprintBlocker);
        bool prerequisitesComplete =
            ArePrerequisitesCompleted(state, project);
        string prerequisiteBlocker = string.Empty;
        if (!prerequisitesComplete)
        {
            string[] missingNames = project.Prerequisites
                .Where(required =>
                    !state.Projects.IsCompleted(required.ProjectId))
                .Select(required => required.DisplayName)
                .ToArray();
            prerequisiteBlocker =
                $"Missing prerequisite research: {string.Join(", ", missingNames)}";
        }

        bool blueprintInTransit = project.BlueprintRule ==
                ResearchBlueprintRule.Required
            && !archived
            && BlueprintArchiveQuery.GetStatus(project.Blueprint).IsInTransit;
        bool requiresFacilityCheck =
            project.BlueprintRule == ResearchBlueprintRule.Shortcut && archived
            || prerequisitesComplete
            && !(project.BlueprintRule == ResearchBlueprintRule.Required
                 && !archived);
        bool facilityRequirementsMet = false;
        string facilityBlocker = string.Empty;
        if (requiresFacilityCheck)
        {
            facilityRequirementsMet = ResearchFacilityCapacity.MeetsRequirements(
                project,
                out facilityBlocker);
        }

        ResearchNodeEvaluation evaluation =
            ResearchProjectCoordinatorRules.EvaluateNodeState(
                projectExists: true,
                completed: false,
                active: false,
                queued: false,
                queueSuspended: false,
                queueBlocker: string.Empty,
                blueprintRule: project.BlueprintRule,
                blueprintArchived: archived,
                blueprintInTransit: blueprintInTransit,
                blueprintBlocker: blueprintBlocker,
                prerequisitesComplete: prerequisitesComplete,
                prerequisiteBlocker: prerequisiteBlocker,
                facilityRequirementsMet: facilityRequirementsMet,
                facilityBlocker: facilityBlocker);
        blocker = evaluation.Blocker;
        return evaluation.State;
    }

    public bool HasArchivedBlueprint(
        ResearchProjectSO project,
        out string blocker)
    {
        if (project == null || project.BlueprintRule == ResearchBlueprintRule.None)
        {
            blocker = string.Empty;
            return true;
        }
        ResearchBlueprintArchiveStatus status =
            BlueprintArchiveQuery.GetStatus(project.Blueprint);
        blocker = status.Blocker;
        return status.IsArchived;
    }

    private void CollectQueueDependencies(
        ResearchProjectSO project,
        ICollection<ResearchProjectSO> ordered,
        ISet<string> visited)
    {
        if (project == null || !visited.Add(project.ProjectId.Value))
        {
            return;
        }

        bool shortcutActive = project.BlueprintRule ==
                ResearchBlueprintRule.Shortcut
            && HasArchivedBlueprint(project, out _);
        if (!shortcutActive)
        {
            foreach (ResearchProjectSO prerequisite in project.Prerequisites
                         .OrderBy(candidate => candidate.ProjectId.Value,
                             StringComparer.Ordinal))
            {
                CollectQueueDependencies(prerequisite, ordered, visited);
            }
        }
        ordered.Add(project);
    }

    private bool IsQueueOrderValid(BlueprintResearchState state)
    {
        Dictionary<string, int> indexById = state.Projects.Queue
            .Select((entry, index) => (entry, index))
            .ToDictionary(
                pair => pair.entry.ProjectId.Value,
                pair => pair.index,
                StringComparer.Ordinal);
        foreach (ResearchQueueEntry entry in state.Projects.Queue)
        {
            if (!ProjectCatalog.TryGet(
                    entry.ProjectId,
                    out ResearchProjectSO project))
            {
                return false;
            }
            bool shortcutActive = project.BlueprintRule ==
                    ResearchBlueprintRule.Shortcut
                && HasArchivedBlueprint(project, out _);
            if (shortcutActive)
            {
                continue;
            }
            foreach (ResearchProjectSO prerequisite in project.Prerequisites)
            {
                if (state.Projects.IsCompleted(prerequisite.ProjectId))
                {
                    continue;
                }
                if (!indexById.TryGetValue(
                        prerequisite.ProjectId.Value,
                        out int prerequisiteIndex)
                    || prerequisiteIndex >= indexById[project.ProjectId.Value])
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void RequireState(BlueprintResearchState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
    }
}

public sealed class UnavailableResearchBlueprintArchiveQuery :
    IResearchBlueprintArchiveQuery
{
    public static readonly UnavailableResearchBlueprintArchiveQuery Instance =
        new UnavailableResearchBlueprintArchiveQuery();

    private UnavailableResearchBlueprintArchiveQuery() { }

    public int Version => 0;

    public ResearchBlueprintArchiveStatus GetStatus(
        FacilityBlueprintSO blueprint) => new ResearchBlueprintArchiveStatus(
        false,
        false,
        string.Empty,
        "연구 설계도 보관 상태를 확인할 수 없습니다.");

    public IReadOnlyList<BuildableObject> GetValidArchives() =>
        Array.Empty<BuildableObject>();

    public bool TryGetPreferredArchive(
        FacilityBlueprintSO blueprint,
        out BuildableObject archive,
        out string destinationId)
    {
        archive = null;
        destinationId = string.Empty;
        return false;
    }
}

public sealed class UnrestrictedResearchFacilityCapacityQuery :
    IResearchFacilityCapacityQuery
{
    public static readonly UnrestrictedResearchFacilityCapacityQuery Instance =
        new UnrestrictedResearchFacilityCapacityQuery();

    private UnrestrictedResearchFacilityCapacityQuery() { }

    public int Version => 0;
    public int GetAvailable(ResearchFacilityCapabilityId capability) =>
        int.MaxValue;
    public bool MeetsRequirements(
        ResearchProjectSO project,
        out string blocker)
    {
        blocker = string.Empty;
        return project != null;
    }
    public string FormatRequirements(ResearchProjectSO project) => string.Empty;
}
