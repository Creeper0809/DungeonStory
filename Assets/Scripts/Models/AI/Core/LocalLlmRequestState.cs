using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

internal enum LocalLlmRequestAdmission
{
    Accepted,
    DuplicateCorrelation,
    Dropped,
    RejectedQuietly,
    QueueFull
}

/// <summary>
/// Owns accepted request membership and scheduling for one queue lifetime.
/// Callers deliver terminal notifications after these state transitions finish.
/// </summary>
internal sealed class LocalLlmRequestState
{
    private readonly List<LocalLlmQueuedRequest> queued = new();
    private readonly HashSet<LocalLlmQueuedRequest> running = new();
    private PrefixAffinityKey affinity;
    private int affinityBurst;

    internal int QueuedCount => queued.Count;
    internal int RunningCount => running.Count;
    internal IReadOnlyList<LocalLlmQueuedRequest> Queued => queued.AsReadOnly();
    internal IEnumerable<LocalLlmQueuedRequest> Running => running.ToArray();

    internal bool ContainsCorrelation(string correlation) =>
        !string.IsNullOrEmpty(correlation)
        && queued.Concat(running).Any(request => string.Equals(
            request.CorrelationId, correlation, StringComparison.Ordinal));

    internal LocalLlmRequestAdmission Admit(
        LocalLlmQueuedRequest request, int maximumQueued, out LocalLlmQueuedRequest evicted)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.IsCompleted) throw new InvalidOperationException("A completed LLM request cannot be admitted.");
        if (queued.Contains(request) || running.Contains(request))
            throw new InvalidOperationException("An LLM request already has a queue owner.");
        if (maximumQueued < 1) throw new ArgumentOutOfRangeException(nameof(maximumQueued));
        evicted = null;
        if (ContainsCorrelation(request.CorrelationId))
            return LocalLlmRequestAdmission.DuplicateCorrelation;
        if (queued.Count >= maximumQueued)
        {
            int victim = FindEvictionIndex();
            if (victim < 0)
                return request.Profile.QueueFullBehavior switch
                {
                    LocalLlmQueueFullBehavior.Drop => LocalLlmRequestAdmission.Dropped,
                    LocalLlmQueueFullBehavior.RejectQuietly => LocalLlmRequestAdmission.RejectedQuietly,
                    _ => LocalLlmRequestAdmission.QueueFull
                };
            evicted = queued[victim];
            queued.RemoveAt(victim);
        }
        queued.Add(request);
        return LocalLlmRequestAdmission.Accepted;
    }

    internal string PeekNextProfile(float now)
    {
        int index = ContextAwareLlmScheduler.FindNext(queued, now, affinity, affinityBurst);
        return index < 0 ? string.Empty : queued[index].Profile.Id;
    }

    internal bool TryBeginNext(float now, int maximumRunning, out LocalLlmQueuedRequest request)
    {
        request = null;
        if (running.Count >= Math.Max(1, maximumRunning)) return false;
        int index = ContextAwareLlmScheduler.FindNext(queued, now, affinity, affinityBurst);
        if (index < 0 || !ContextAwareLlmScheduler.CanDispatch(queued[index], now, queued.Count)) return false;
        request = BeginAt(index);
        if (request.Scheduling != null && request.Scheduling.AffinityKey == affinity)
            affinityBurst++;
        else
        {
            affinity = request.Scheduling?.AffinityKey ?? default;
            affinityBurst = 1;
        }
        return true;
    }

    // The explicit isolated target uses normal execution without competing with unrelated captures.
    internal bool TryBeginCorrelated(string correlation, out LocalLlmQueuedRequest request)
    {
        request = null;
        int index = queued.FindIndex(value => string.Equals(value.CorrelationId, correlation, StringComparison.Ordinal));
        if (index < 0) return false;
        request = BeginAt(index);
        return true;
    }

    private LocalLlmQueuedRequest BeginAt(int index)
    {
        LocalLlmQueuedRequest request = queued[index];
        if (request.IsCompleted || running.Contains(request))
            throw new InvalidOperationException("Queued LLM request has an invalid execution owner.");
        queued.RemoveAt(index);
        running.Add(request);
        return request;
    }

    internal bool IsRunning(LocalLlmQueuedRequest request) => request != null && running.Contains(request);
    internal void Finish(LocalLlmQueuedRequest request) => running.Remove(request);

    internal IReadOnlyList<LocalLlmQueuedRequest> CancelCorrelation(string correlation)
    {
        LocalLlmQueuedRequest[] cancelled = queued.Concat(running)
            .Where(request => string.Equals(request.CorrelationId, correlation, StringComparison.Ordinal))
            .Distinct().ToArray();
        queued.RemoveAll(request => cancelled.Contains(request));
        // Running requests keep their slot/correlation until the execution's finally releases them.
        return cancelled;
    }

    internal IReadOnlyList<LocalLlmQueuedRequest> TakeExpired(float now)
    {
        var expired = new List<LocalLlmQueuedRequest>();
        for (int index = queued.Count - 1; index >= 0; index--)
        {
            LocalLlmQueuedRequest request = queued[index];
            if (request.Profile.MaxQueueAgeSeconds <= 0f
                || now - request.EnqueuedAt <= request.Profile.MaxQueueAgeSeconds) continue;
            queued.RemoveAt(index);
            expired.Add(request);
        }
        return expired;
    }

    internal IReadOnlyList<LocalLlmQueuedRequest> CapturePending() => queued.Concat(running).Distinct().ToArray();

    internal void Clear()
    {
        queued.Clear();
        running.Clear();
        affinity = default;
        affinityBurst = 0;
    }

    private int FindEvictionIndex()
    {
        int index = -1;
        int lowestPriority = int.MaxValue;
        float oldest = float.MaxValue;
        for (int candidate = 0; candidate < queued.Count; candidate++)
        {
            LocalLlmQueuedRequest request = queued[candidate];
            if (!request.Profile.CanBeEvictedForQueuePressure || request.Profile.Priority > lowestPriority
                || request.Profile.Priority == lowestPriority && request.EnqueuedAt >= oldest) continue;
            lowestPriority = request.Profile.Priority;
            oldest = request.EnqueuedAt;
            index = candidate;
        }
        return index;
    }
}

internal sealed class LocalLlmQueuedRequest : IContextAwareLlmRequest
{
    public LocalLlmQueuedRequest(
        LocalLlmRequestProfile profile,
        string prompt,
        string originalText,
        float timeoutSeconds,
        float enqueuedAt,
        string correlationId,
        NarrativeSchedulingMetadata scheduling,
        Action<LocalLlmResult> callback,
        bool isEquipmentChoice = false,
        int candidateCount = 0)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        OriginalText = originalText ?? string.Empty;
        TimeoutSeconds = Mathf.Max(0.1f, timeoutSeconds);
        EnqueuedAt = enqueuedAt;
        CorrelationId = correlationId ?? string.Empty;
        Scheduling = scheduling ?? NarrativeSchedulingMetadata.CreateDefault(
            profile,
            prompt,
            correlationId,
            enqueuedAt);
        Callback = callback;
        IsEquipmentChoice = isEquipmentChoice;
        CandidateCount = candidateCount;
    }

    public LocalLlmRequestProfile Profile { get; }
    public string Prompt { get; }
    public string OriginalText { get; }
    public float TimeoutSeconds { get; }
    public float EnqueuedAt { get; }
    public string CorrelationId { get; }
    public NarrativeSchedulingMetadata Scheduling { get; }
    public bool IsEquipmentChoice { get; }
    public int CandidateCount { get; }
    int IContextAwareLlmRequest.Priority => Profile.Priority;
    public float StartedAt { get; private set; } = -1f;
    private Action<LocalLlmResult> Callback { get; set; }
    private UnityWebRequest ActiveWebRequest { get; set; }

    public bool IsCompleted { get; private set; }

    public bool TryTakeCallback(out Action<LocalLlmResult> callback)
    {
        if (IsCompleted)
        {
            callback = null;
            return false;
        }

        IsCompleted = true;
        callback = Callback;
        Callback = null;
        return true;
    }

    public void Attach(UnityWebRequest request, float startedAt)
    {
        ActiveWebRequest = request;
        StartedAt = startedAt;
    }

    public void Detach()
    {
        ActiveWebRequest = null;
    }

    public void Abort()
    {
        ActiveWebRequest?.Abort();
    }
}

