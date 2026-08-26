using System;
using System.Collections.Generic;

internal enum FacilityOutputExactRouteDestructiveRetireStatus
{
    Ready = 0,
    Empty = 1,
    Applied = 2,
    AlreadyApplied = 3,
    Deferred = 4,
    Conflict = 5
}

internal readonly struct FacilityOutputExactRouteDestructiveRetireResult
{
    internal FacilityOutputExactRouteDestructiveRetireResult(
        FacilityOutputExactRouteDestructiveRetireStatus status,
        string sourceDestinationId,
        string candidateFingerprint,
        int routeCount,
        int stackCount,
        string reason)
    {
        Status = status;
        SourceDestinationId = sourceDestinationId ?? string.Empty;
        CandidateFingerprint = candidateFingerprint ?? string.Empty;
        RouteCount = Math.Max(0, routeCount);
        StackCount = Math.Max(0, stackCount);
        Reason = reason ?? string.Empty;
    }

    internal FacilityOutputExactRouteDestructiveRetireStatus Status { get; }
    internal string SourceDestinationId { get; }
    internal string CandidateFingerprint { get; }
    internal int RouteCount { get; }
    internal int StackCount { get; }
    internal string Reason { get; }
    internal bool IsReady =>
        Status == FacilityOutputExactRouteDestructiveRetireStatus.Ready;
    internal bool IsApplied => Status is
        FacilityOutputExactRouteDestructiveRetireStatus.Applied
        or FacilityOutputExactRouteDestructiveRetireStatus.AlreadyApplied
        or FacilityOutputExactRouteDestructiveRetireStatus.Empty;
}

internal interface IFacilityOutputExactRouteDestructiveRetireCandidate
{
    string SourceDestinationId { get; }
    string BatchCommitId { get; }
    string CandidateFingerprint { get; }
    IReadOnlyList<string> RouteOperationIds { get; }
    IReadOnlyList<string> PhysicalStackIds { get; }
}

/// <summary>
/// Internal-only destructive-drain boundary. Normal gameplay must continue to
/// fail closed through IFacilityOutputExactRoutePort.TryForgetRoutable.
/// </summary>
internal interface IFacilityOutputExactRouteDestructiveRetirePort
{
    FacilityOutputExactRouteDestructiveRetireResult PrepareDestructiveRetire(
        string sourceDestinationId,
        string batchCommitId,
        out IFacilityOutputExactRouteDestructiveRetireCandidate candidate);

    FacilityOutputExactRouteDestructiveRetireResult PublishDestructiveRetire(
        IFacilityOutputExactRouteDestructiveRetireCandidate candidate);

    void RollbackDestructiveRetire(
        IFacilityOutputExactRouteDestructiveRetireCandidate candidate);

    void CompleteDestructiveRetire(
        IFacilityOutputExactRouteDestructiveRetireCandidate candidate);
}
