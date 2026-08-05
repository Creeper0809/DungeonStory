using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

// Pure queue state and commands. Scene access is supplied by IResearchQueueRuntimePort.

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchProjectProgressState
{
    [SerializeField] private string projectId = string.Empty;
    [SerializeField] private float progress;

    public ResearchProjectProgressState(ResearchProjectId projectId)
    {
        this.projectId = projectId.Value;
    }

    public ResearchProjectId ProjectId => new ResearchProjectId(projectId);
    public float Progress => Mathf.Max(0f, progress);

    public float GetRatio(IResearchProjectDefinition project) =>
        project == null ? 0f : Mathf.Clamp01(Progress / project.RequiredWork);

    public float Add(float amount, IResearchProjectDefinition project)
    {
        if (project == null || amount <= 0f)
        {
            return 0f;
        }

        float before = progress;
        progress = Mathf.Min(project.RequiredWork, progress + amount);
        return progress - before;
    }

    public void Restore(float value, IResearchProjectDefinition project)
    {
        progress = Mathf.Clamp(value, 0f, project?.RequiredWork ?? float.MaxValue);
    }

    internal ResearchProjectProgressState DeepClone()
    {
        ResearchProjectProgressState clone =
            new ResearchProjectProgressState(ProjectId);
        clone.progress = progress;
        return clone;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchQueueEntry
{
    [SerializeField] private string projectId = string.Empty;
    [SerializeField] private string suspendedReason = string.Empty;

    public ResearchQueueEntry(ResearchProjectId projectId)
    {
        this.projectId = projectId.Value;
    }

    public ResearchProjectId ProjectId => new ResearchProjectId(projectId);
    public string SuspendedReason => suspendedReason ?? string.Empty;
    public bool IsSuspended => !string.IsNullOrWhiteSpace(SuspendedReason);

    public void SetSuspended(string reason)
    {
        suspendedReason = reason?.Trim() ?? string.Empty;
    }

    internal ResearchQueueEntry DeepClone()
    {
        ResearchQueueEntry clone = new ResearchQueueEntry(ProjectId);
        clone.suspendedReason = suspendedReason;
        return clone;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchProjectRuntimeState
{
    private readonly Dictionary<string, ResearchProjectProgressState> progressById =
        new Dictionary<string, ResearchProjectProgressState>(StringComparer.Ordinal);
    private readonly HashSet<string> completedIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ResearchQueueEntry> queue = new List<ResearchQueueEntry>();
    private readonly IReadOnlyDictionary<string, ResearchProjectProgressState> progressView;
    private readonly IReadOnlyCollection<string> completedView;
    private readonly IReadOnlyList<ResearchQueueEntry> queueView;
    private string activeProjectId = string.Empty;

    public ResearchProjectRuntimeState()
    {
        progressView = progressById;
        completedView = completedIds;
        queueView = ReadOnlyView.List(queue);
    }

    public IReadOnlyDictionary<string, ResearchProjectProgressState> ProgressById => progressView;
    public IReadOnlyCollection<string> CompletedProjectIds => completedView;
    public IReadOnlyList<ResearchQueueEntry> Queue => queueView;
    public ResearchProjectId ActiveProjectId => new ResearchProjectId(activeProjectId);

    public ResearchProjectProgressState GetProgress(ResearchProjectId projectId)
    {
        if (!progressById.TryGetValue(projectId.Value, out ResearchProjectProgressState state))
        {
            state = new ResearchProjectProgressState(projectId);
            progressById[projectId.Value] = state;
        }
        return state;
    }

    public bool IsCompleted(ResearchProjectId projectId) =>
        completedIds.Contains(projectId.Value);

    public bool ContainsInQueue(ResearchProjectId projectId) =>
        queue.Any(entry => entry.ProjectId.Equals(projectId));

    public void AddQueueEntry(ResearchProjectId projectId)
    {
        if (projectId.IsValid && !IsCompleted(projectId) && !ContainsInQueue(projectId))
        {
            queue.Add(new ResearchQueueEntry(projectId));
        }
    }

    public bool RemoveQueueEntry(ResearchProjectId projectId)
    {
        ResearchQueueEntry entry = queue.FirstOrDefault(candidate =>
            candidate.ProjectId.Equals(projectId));
        if (entry == null)
        {
            return false;
        }

        queue.Remove(entry);
        if (string.Equals(activeProjectId, projectId.Value, StringComparison.Ordinal))
        {
            activeProjectId = string.Empty;
        }
        return true;
    }

    public bool MovePending(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= queue.Count
            || toIndex < 0 || toIndex >= queue.Count
            || fromIndex == toIndex)
        {
            return false;
        }

        int activeIndex = queue.FindIndex(entry =>
            string.Equals(entry.ProjectId.Value, activeProjectId, StringComparison.Ordinal));
        if (fromIndex == activeIndex || toIndex == activeIndex)
        {
            return false;
        }

        ResearchQueueEntry moved = queue[fromIndex];
        queue.RemoveAt(fromIndex);
        queue.Insert(toIndex, moved);
        return true;
    }

    public void SetActive(ResearchProjectId projectId)
    {
        activeProjectId = projectId.Value;
        int activeIndex = queue.FindIndex(entry => entry.ProjectId.Equals(projectId));
        if (activeIndex > 0)
        {
            ResearchQueueEntry active = queue[activeIndex];
            queue.RemoveAt(activeIndex);
            queue.Insert(0, active);
        }
    }

    public void Complete(ResearchProjectId projectId)
    {
        completedIds.Add(projectId.Value);
        RemoveQueueEntry(projectId);
        if (string.Equals(activeProjectId, projectId.Value, StringComparison.Ordinal))
        {
            activeProjectId = string.Empty;
        }
    }

    public void RestoreProgress(IResearchProjectDefinition project, float progress)
    {
        GetProgress(project.ProjectId).Restore(progress, project);
    }

    public void RestoreCompleted(ResearchProjectId projectId)
    {
        if (projectId.IsValid)
        {
            completedIds.Add(projectId.Value);
            queue.RemoveAll(entry => entry.ProjectId.Equals(projectId));
            if (string.Equals(
                    activeProjectId,
                    projectId.Value,
                    StringComparison.Ordinal))
            {
                activeProjectId = string.Empty;
            }
        }
    }

    public void RestoreQueueEntry(ResearchProjectId projectId, string suspendedReason)
    {
        if (!projectId.IsValid || IsCompleted(projectId) || ContainsInQueue(projectId))
        {
            return;
        }

        ResearchQueueEntry entry = new ResearchQueueEntry(projectId);
        entry.SetSuspended(suspendedReason);
        queue.Add(entry);
    }

    public void RestoreActive(ResearchProjectId projectId)
    {
        activeProjectId = projectId.IsValid && ContainsInQueue(projectId)
            ? projectId.Value
            : string.Empty;
    }

    public ResearchProjectRuntimeState DeepClone()
    {
        ResearchProjectRuntimeState clone = new ResearchProjectRuntimeState();
        foreach (KeyValuePair<string, ResearchProjectProgressState> pair in progressById)
        {
            clone.progressById.Add(pair.Key, pair.Value.DeepClone());
        }

        clone.completedIds.UnionWith(completedIds);
        clone.queue.AddRange(queue.Select(entry => entry.DeepClone()));
        clone.activeProjectId = activeProjectId;
        return clone;
    }
}

public readonly struct ResearchQueueCommandResult
{
    public ResearchQueueCommandResult(
        bool succeeded,
        string message,
        IReadOnlyList<ResearchProjectId> affectedProjects = null)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
        AffectedProjects = affectedProjects ?? Array.Empty<ResearchProjectId>();
    }

    public bool Succeeded { get; }
    public string Message { get; }
    public IReadOnlyList<ResearchProjectId> AffectedProjects { get; }
}

public interface IResearchQueueCommandService
{
    ResearchQueueCommandResult Enqueue(ResearchProjectId projectId);
    ResearchQueueCommandResult Remove(ResearchProjectId projectId);
    ResearchQueueCommandResult Move(int fromIndex, int toIndex);
}

public interface IResearchQueueRuntimePort
{
    ResearchQueueCommandResult EnqueueProject(ResearchProjectId projectId);
    ResearchQueueCommandResult RemoveProject(ResearchProjectId projectId);
    ResearchQueueCommandResult MoveProject(int fromIndex, int toIndex);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResearchQueueCommandService : IResearchQueueCommandService
{
    private readonly IResearchQueueRuntimePort runtime;

    public ResearchQueueCommandService(IResearchQueueRuntimePort runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public ResearchQueueCommandResult Enqueue(ResearchProjectId projectId)
    {
        return runtime != null
            ? runtime.EnqueueProject(projectId)
            : new ResearchQueueCommandResult(false, "연구 런타임이 없습니다.");
    }

    public ResearchQueueCommandResult Remove(ResearchProjectId projectId)
    {
        return runtime != null
            ? runtime.RemoveProject(projectId)
            : new ResearchQueueCommandResult(false, "연구 런타임이 없습니다.");
    }

    public ResearchQueueCommandResult Move(int fromIndex, int toIndex)
    {
        return runtime != null
            ? runtime.MoveProject(fromIndex, toIndex)
            : new ResearchQueueCommandResult(false, "연구 런타임이 없습니다.");
    }
}
