using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Owns one admitted worker's approach and presentation lifetime. The facility
/// retains capacity/slot authority; a stale handle cannot release a newer grant.
/// </summary>
internal sealed class BuildingWorkerAllocation : IBuildingWorkerAllocation
{
    private readonly BuildableObject owner;
    private readonly object expectedAction;
    private readonly Func<bool> isRequestCurrent;
    private readonly Func<BuildingWorkerAllocation, bool> ownsGrant;
    private readonly Action<BuildingWorkerAllocation> releaseGrant;
    private readonly Vector3 workPosition;
    private readonly Vector3 standOffset;
    private readonly Func<Vector3, Vector3> resolveExit;
    private readonly string approachPhase;
    private readonly string workPhase;
    private bool disposed;
    private bool approaching;
    private bool ready;
    private bool hasPose;

    internal BuildingWorkerAllocation(BuildableObject owner, IBuildingVisitorPort actor,
        object expectedAction, Func<bool> isRequestCurrent,
        Func<BuildingWorkerAllocation, bool> ownsGrant,
        Action<BuildingWorkerAllocation> releaseGrant,
        Vector3 workPosition, Vector3 standOffset, Func<Vector3, Vector3> resolveExit,
        string approachPhase, string workPhase)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        this.expectedAction = expectedAction;
        this.isRequestCurrent = isRequestCurrent;
        this.ownsGrant = ownsGrant ?? throw new ArgumentNullException(nameof(ownsGrant));
        this.releaseGrant = releaseGrant ?? throw new ArgumentNullException(nameof(releaseGrant));
        this.workPosition = workPosition;
        this.standOffset = standOffset;
        this.resolveExit = resolveExit;
        this.approachPhase = approachPhase;
        this.workPhase = workPhase;
        CharacterId = actor.BuildingCharacterId;
    }

    internal IBuildingVisitorPort Actor { get; }
    internal CharacterId CharacterId { get; }
    public bool IsReady => ready && IsCurrent;

    private bool IsActorActive
    {
        get
        {
            try { return Actor.VisitorSnapshot.IsRuntimeActive; }
            catch (MissingReferenceException) { return false; }
        }
    }

    private bool IsCurrent
    {
        get
        {
            if (disposed || !ownsGrant(this) || owner == null
                || owner.isDestroy || !owner.isActiveAndEnabled)
                return false;
            try
            {
                return Actor.VisitorSnapshot.IsRuntimeActive
                    && (expectedAction == null || Actor.IsCurrentAction(expectedAction)
                        && !Actor.IsCurrentActionEnded)
                    && (isRequestCurrent == null || isRequestCurrent());
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }
    }

    public IEnumerator Approach()
    {
        if (approaching)
            throw new InvalidOperationException("A worker allocation approach is already running.");
        if (ready || disposed)
            yield break;
        approaching = true;
        try
        {
            if (!IsCurrent)
                yield break;
            // Stationary worker fixtures and capabilities retain their existing
            // admission semantics without acquiring a movement/presentation pose.
            if (!Actor.VisitorSnapshot.CanMove)
            {
                ready = true;
                yield break;
            }

            Actor.SetActionPhase(approachPhase, owner);
            if (!IsCurrent)
                yield break;
            bool completed = false;
            string failure = null;
            yield return Actor.MoveToWithResult(workPosition, 1f, expectedAction,
                value => { failure = value; completed = true; });
            if (!IsCurrent)
                yield break;
            if (!completed || failure != string.Empty)
            {
                ReportMovementFailure(completed ? failure ?? "worker-movement-result-null" : "worker-approach-result-missing");
                yield break;
            }

            hasPose = true;
            Actor.BeginWorkerPose(this);
            if (!IsCurrent)
                yield break;
            completed = false;
            failure = null;
            yield return Actor.MoveToWithResult(workPosition + standOffset, 3f, expectedAction,
                value => { failure = value; completed = true; });
            if (!IsCurrent)
                yield break;
            if (!completed || failure != string.Empty)
            {
                ReportMovementFailure(completed ? failure ?? "worker-movement-result-null" : "worker-pose-result-missing");
                yield break;
            }

            ready = true;
            Actor.SetActionPhase(workPhase, owner);
            if (IsCurrent)
                Actor.FaceRight();
        }
        finally
        {
            approaching = false;
            if (!IsReady)
                Dispose();
        }
    }

    private void ReportMovementFailure(string reason) => Actor.ReportInteractionFailure(
        BuildingInteractionFailureKind.ResourceUnavailable,
        "worker-allocation-movement-failed:" + reason, owner);

    public void Dispose()
    {
        if (disposed)
            return;
        bool owns = ownsGrant(this);
        bool restorePosition = hasPose && IsActorActive;
        disposed = true;
        try
        {
            Vector3? position = null;
            if (restorePosition)
            {
                Vector3 exit = ready
                    ? Actor.VisitorSnapshot.Position - standOffset
                    : workPosition;
                position = resolveExit != null ? resolveExit(exit) : exit;
            }
            // The actor owns the pose token. A newer grant or any explicit
            // layer/position command invalidates this handle's presentation claim.
            Actor.EndWorkerPose(this, position);
        }
        finally
        {
            if (owns)
                releaseGrant(this);
        }
    }
}
