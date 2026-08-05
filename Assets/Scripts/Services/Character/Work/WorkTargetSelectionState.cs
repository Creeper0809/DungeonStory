using System.Collections.Generic;

internal readonly struct CandidateCacheEntry
{
    public CandidateCacheEntry(
        bool found,
        WorkTargetCandidate candidate,
        WorkTargetCandidate rejected)
    {
        Found = found;
        Candidate = candidate;
        Rejected = rejected;
    }

    public bool Found { get; }
    public WorkTargetCandidate Candidate { get; }
    public WorkTargetCandidate Rejected { get; }
}

internal readonly struct ActorWorkScore
{
    public ActorWorkScore(float preference, float speed)
    {
        Preference = preference;
        Speed = speed;
    }

    public float Preference { get; }
    public float Speed { get; }
}

internal sealed class IncrementalCandidateScan
{
    public IReadOnlyList<BuildableObject> Source;
    public int CandidateIndexVersion;
    public int DynamicStateVersion;
    public int GridVersion;
    public int BuildingVersion;
    public int WorkOrderVersion;
    public int StartOffset;
    public int EvaluatedCount;
    public int CompletedFrame = -1;
    public int LastAdvancedFrame = -1;
    public WorkTargetCandidate Best;
    public WorkTargetCandidate MostUrgent;
    public WorkTargetCandidate Rejected;
    public bool Complete;
}

public static class WorkTargetCandidateRuntimeAdapter
{
    public static BuildableObject ResolveBuilding(WorkTargetCandidate candidate)
    {
        return candidate.Building as BuildableObject;
    }

    public static TBuilding ResolveBuilding<TBuilding>(
        WorkTargetCandidate candidate)
        where TBuilding : BuildableObject
    {
        return ResolveBuilding(candidate) as TBuilding;
    }
}
