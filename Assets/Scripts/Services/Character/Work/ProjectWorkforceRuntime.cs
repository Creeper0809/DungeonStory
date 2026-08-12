using System;
using System.Collections.Generic;

public readonly struct ProjectWorkerRegistration
{
    public ProjectWorkerRegistration(
        string projectId,
        string workerId,
        ProjectScale scale,
        int maximumWorkers,
        long joinSequence)
    {
        ProjectId = projectId;
        WorkerId = workerId;
        Scale = scale;
        MaximumWorkers = maximumWorkers;
        JoinSequence = joinSequence;
    }

    public string ProjectId { get; }
    public string WorkerId { get; }
    public ProjectScale Scale { get; }
    public int MaximumWorkers { get; }
    public long JoinSequence { get; }
}

public readonly struct ProjectWorkforceSnapshot
{
    public ProjectWorkforceSnapshot(
        string projectId,
        ProjectScale scale,
        int activeWorkers,
        int maximumWorkers,
        int defaultAutomaticWorkerLimit,
        float effectiveWorkerCount,
        float nextWorkerContribution,
        float effectiveWuPerSecond,
        float referenceWorkerWuPerSecond)
    {
        ProjectId = projectId ?? string.Empty;
        Scale = scale;
        ActiveWorkers = activeWorkers;
        MaximumWorkers = maximumWorkers;
        DefaultAutomaticWorkerLimit = defaultAutomaticWorkerLimit;
        EffectiveWorkerCount = effectiveWorkerCount;
        NextWorkerContribution = nextWorkerContribution;
        EffectiveWuPerSecond = effectiveWuPerSecond;
        ReferenceWorkerWuPerSecond = referenceWorkerWuPerSecond;
    }

    public string ProjectId { get; }
    public ProjectScale Scale { get; }
    public int ActiveWorkers { get; }
    public int MaximumWorkers { get; }
    public int DefaultAutomaticWorkerLimit { get; }
    public float EffectiveWorkerCount { get; }
    public float NextWorkerContribution { get; }
    public float EffectiveWuPerSecond { get; }
    public float ReferenceWorkerWuPerSecond { get; }
}

public interface IProjectWorkforceRuntime
{
    bool CanJoin(string projectId, string workerId, int maximumWorkers);
    bool TryJoin(
        string projectId,
        string workerId,
        ProjectScale scale,
        int maximumWorkers,
        out ProjectWorkerLease lease,
        out string failureReason);
    float GetContributionMultiplier(string projectId, string workerId);
    bool UpdateWorkerRate(string projectId, string workerId, float wuPerSecond);
    int GetActiveWorkerCount(string projectId);
    bool TryCapture(string projectId, out ProjectWorkforceSnapshot snapshot);
}

public sealed class ProjectWorkerLease : IDisposable
{
    private ProjectWorkforceRuntime owner;

    internal ProjectWorkerLease(
        ProjectWorkforceRuntime owner,
        ProjectWorkerRegistration registration)
    {
        this.owner = owner;
        Registration = registration;
    }

    public ProjectWorkerRegistration Registration { get; }

    public void Dispose()
    {
        ProjectWorkforceRuntime current = owner;
        owner = null;
        current?.Leave(Registration.ProjectId, Registration.WorkerId);
    }
}

public sealed class ProjectWorkforceRuntime : IProjectWorkforceRuntime
{
    private readonly Dictionary<string, List<ProjectWorkerRegistration>> workersByProject =
        new Dictionary<string, List<ProjectWorkerRegistration>>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> projectByWorker =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> rateByWorker =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private long nextJoinSequence;

    public bool CanJoin(string projectId, string workerId, int maximumWorkers)
    {
        string project = projectId?.Trim() ?? string.Empty;
        string worker = workerId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(project)
            || string.IsNullOrWhiteSpace(worker)
            || maximumWorkers < 1
            || maximumWorkers > 8)
        {
            return false;
        }

        if (projectByWorker.TryGetValue(worker, out string existingProject))
        {
            return false;
        }

        return !workersByProject.TryGetValue(project, out List<ProjectWorkerRegistration> workers)
            || workers.Count < maximumWorkers;
    }

    public bool TryJoin(
        string projectId,
        string workerId,
        ProjectScale scale,
        int maximumWorkers,
        out ProjectWorkerLease lease,
        out string failureReason)
    {
        lease = null;
        failureReason = string.Empty;
        string project = projectId?.Trim() ?? string.Empty;
        string worker = workerId?.Trim() ?? string.Empty;
        int authoredMaximum = SettlementLaborBalanceRules.GetMaximumWorkers(scale);
        if (maximumWorkers != authoredMaximum)
        {
            failureReason =
                $"Project '{project}' maximum {maximumWorkers} does not match {scale} authority {authoredMaximum}.";
            return false;
        }

        if (!CanJoin(project, worker, maximumWorkers))
        {
            failureReason = $"Project '{project}' has no free worker slot for '{worker}'.";
            return false;
        }

        if (projectByWorker.TryGetValue(worker, out string existingProject))
        {
            failureReason =
                $"Worker '{worker}' already joined project '{existingProject}'.";
            return false;
        }

        if (!workersByProject.TryGetValue(project, out List<ProjectWorkerRegistration> workers))
        {
            workers = new List<ProjectWorkerRegistration>(maximumWorkers);
            workersByProject.Add(project, workers);
        }

        ProjectWorkerRegistration registration = new ProjectWorkerRegistration(
            project,
            worker,
            scale,
            maximumWorkers,
            checked(++nextJoinSequence));
        workers.Add(registration);
        projectByWorker.Add(worker, project);
        lease = new ProjectWorkerLease(this, registration);
        return true;
    }

    public float GetContributionMultiplier(string projectId, string workerId)
    {
        string project = projectId?.Trim() ?? string.Empty;
        string worker = workerId?.Trim() ?? string.Empty;
        if (!workersByProject.TryGetValue(project, out List<ProjectWorkerRegistration> workers))
        {
            return 0f;
        }

        for (int index = 0; index < workers.Count; index++)
        {
            if (string.Equals(workers[index].WorkerId, worker, StringComparison.Ordinal))
            {
                return SettlementLaborBalanceRules.GetWorkerContribution(
                    workers[index].Scale,
                    index);
            }
        }
        return 0f;
    }

    public int GetActiveWorkerCount(string projectId)
    {
        string project = projectId?.Trim() ?? string.Empty;
        return workersByProject.TryGetValue(project, out List<ProjectWorkerRegistration> workers)
            ? workers.Count
            : 0;
    }

    public bool UpdateWorkerRate(
        string projectId,
        string workerId,
        float wuPerSecond)
    {
        string project = projectId?.Trim() ?? string.Empty;
        string worker = workerId?.Trim() ?? string.Empty;
        if (float.IsNaN(wuPerSecond)
            || float.IsInfinity(wuPerSecond)
            || wuPerSecond < 0f
            || !projectByWorker.TryGetValue(worker, out string registeredProject)
            || !string.Equals(project, registeredProject, StringComparison.Ordinal))
        {
            return false;
        }
        rateByWorker[worker] = wuPerSecond;
        return true;
    }

    public bool TryCapture(
        string projectId,
        out ProjectWorkforceSnapshot snapshot)
    {
        string project = projectId?.Trim() ?? string.Empty;
        if (!workersByProject.TryGetValue(
                project,
                out List<ProjectWorkerRegistration> workers)
            || workers.Count == 0)
        {
            snapshot = default;
            return false;
        }

        ProjectScale scale = workers[0].Scale;
        int maximum = workers[0].MaximumWorkers;
        float effective = 0f;
        float effectiveRate = 0f;
        float rawRate = 0f;
        for (int index = 0; index < workers.Count; index++)
        {
            if (workers[index].Scale != scale
                || workers[index].MaximumWorkers != maximum)
            {
                throw new InvalidOperationException(
                    $"Project '{project}' contains inconsistent workforce authority.");
            }
            float contribution = SettlementLaborBalanceRules.GetWorkerContribution(
                scale,
                index);
            effective += contribution;
            float workerRate = rateByWorker.TryGetValue(
                    workers[index].WorkerId,
                    out float capturedRate)
                ? capturedRate
                : 0f;
            rawRate += workerRate;
            effectiveRate += workerRate * contribution;
        }

        snapshot = new ProjectWorkforceSnapshot(
            project,
            scale,
            workers.Count,
            maximum,
            SettlementLaborBalanceRules.GetDefaultAutomaticWorkerLimit(scale),
            effective,
            workers.Count < maximum
                ? SettlementLaborBalanceRules.GetWorkerContribution(
                    scale,
                    workers.Count)
                : 0f,
            effectiveRate,
            workers.Count > 0 ? rawRate / workers.Count : 0f);
        return true;
    }

    internal void Leave(string projectId, string workerId)
    {
        if (!workersByProject.TryGetValue(projectId, out List<ProjectWorkerRegistration> workers))
        {
            return;
        }

        for (int index = 0; index < workers.Count; index++)
        {
            if (!string.Equals(workers[index].WorkerId, workerId, StringComparison.Ordinal))
            {
                continue;
            }
            workers.RemoveAt(index);
            projectByWorker.Remove(workerId);
            rateByWorker.Remove(workerId);
            if (workers.Count == 0)
            {
                workersByProject.Remove(projectId);
            }
            return;
        }
    }
}
