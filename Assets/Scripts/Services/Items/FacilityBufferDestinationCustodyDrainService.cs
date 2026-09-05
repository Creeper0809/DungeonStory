using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Domain-neutral facade over the existing Items durable destination-custody
/// outbox. It deliberately projects rather than copies physical state so all
/// production, medical and future owners share one stack/lease/carry authority.
/// </summary>
public sealed class FacilityBufferDestinationCustodyDrainService :
    IFacilityBufferDestinationCustodyDrainService,
    IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery,
    IFacilityBufferDestinationCustodyDrainCheckpointGcPort,
    IFacilityBufferDestinationCustodyDrainLiveQuery
{
    private readonly IProductionInputDestinationCustodyDrainService inner;
    private readonly IProductionInputDestinationCustodyDrainRestoreCandidateQuery
        restoreCandidates;

    public FacilityBufferDestinationCustodyDrainService(
        IProductionInputDestinationCustodyDrainService inner,
        IProductionInputDestinationCustodyDrainRestoreCandidateQuery
            restoreCandidates)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.restoreCandidates = restoreCandidates
            ?? throw new ArgumentNullException(nameof(restoreCandidates));
    }

    public bool RequiresImmediateRecoveryBeforeGameplayTick =>
        inner.RequiresImmediateRecoveryBeforeGameplayTick;

    public IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot> Drains
    {
        get
        {
            if (inner is not IProductionInputDestinationCustodyDrainLiveQuery
                query)
            {
                throw new InvalidOperationException(
                    "facility-buffer-custody-live-query-missing");
            }
            return (query.CaptureAll()
                    ?? Array.Empty<
                        ProductionInputDestinationCustodyDrainSaveData>())
                .Select(FacilityBufferDestinationCustodyDrainProjection
                    .ProjectValidated)
                .OrderBy(value => value.StepOperationId, StringComparer.Ordinal)
                .ToArray();
        }
    }

    [GameplayInternalOnly(
        "Freezes and persists one owner-neutral FacilityBuffer destination custody drain before any physical release.",
        "Registered domain terminal-lifecycle adapters only")]
    public FacilityBufferDestinationCustodyDrainResult TryPrepare(
        FacilityBufferDestinationCustodyDrainDescriptor descriptor)
    {
        if (descriptor == null)
            return Conflict("facility-buffer-custody-drain-descriptor-missing");
        if (!inner.TryCaptureRequest(
                descriptor.ParentOperationId,
                descriptor.StepOperationId,
                descriptor.OwnerStableId,
                descriptor.OwnerSubjectId,
                descriptor.OwnerFacilityId,
                descriptor.SourceDestinationId,
                descriptor.OwnerPosition,
                descriptor.SourceAuthorityFingerprint,
                out ProductionInputDestinationCustodyDrainRequest request,
                out string failureReason))
        {
            return Conflict(string.IsNullOrEmpty(failureReason)
                ? "facility-buffer-custody-drain-capture-failed"
                : failureReason);
        }

        ProductionInputDestinationCustodyDrainResult result =
            inner.TryPrepare(request);
        return Project(result, descriptor.StepOperationId);
    }

    [GameplayInternalOnly(
        "Advances one durable FacilityBuffer destination custody effect using the Items-owned capture barrier.",
        "Registered domain terminal-lifecycle adapters only")]
    public FacilityBufferDestinationCustodyDrainResult TryAdvance(
        string stepOperationId,
        string requestFingerprint) => Project(
        inner.TryCommit(stepOperationId, requestFingerprint),
        stepOperationId);

    [GameplayInternalOnly(
        "Acknowledges an exact owner-neutral FacilityBuffer custody receipt after the domain owner persists its join.",
        "Registered domain terminal-lifecycle adapters only")]
    public FacilityBufferDestinationCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint) => Project(
        inner.TryAcknowledge(stepOperationId, receiptFingerprint),
        stepOperationId);

    public bool TryCapture(
        string stepOperationId,
        out FacilityBufferDestinationCustodyDrainSnapshot snapshot)
    {
        if (!inner.TryCapture(
                stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData value))
        {
            snapshot = null;
            return false;
        }

        snapshot = FacilityBufferDestinationCustodyDrainProjection
            .ProjectValidated(value);
        return true;
    }

    bool IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery
        .IsCandidateAvailable => restoreCandidates.IsCandidateAvailable;

    IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
        IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery.Drains =>
        (restoreCandidates.Drains
                ?? Array.Empty<ProductionInputDestinationCustodyDrainSaveData>())
            .Select(FacilityBufferDestinationCustodyDrainProjection
                .ProjectValidated)
            .OrderBy(value => value.StepOperationId, StringComparer.Ordinal)
            .ToArray();

    bool IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery.TryGetDrain(
        string stepOperationId,
        out FacilityBufferDestinationCustodyDrainSnapshot snapshot)
    {
        if (!restoreCandidates.TryGetDrain(
                stepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData value))
        {
            snapshot = null;
            return false;
        }
        snapshot = FacilityBufferDestinationCustodyDrainProjection
            .ProjectValidated(value);
        return true;
    }

    public bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot> snapshots,
        out IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (inner is not IProductionInputDestinationCustodyDrainCheckpointGcPort
            checkpointGc)
        {
            failureReason =
                "facility-buffer-custody-checkpoint-gc-port-missing";
            return false;
        }

        FacilityBufferDestinationCustodyDrainSnapshot[] expected = (snapshots
                ?? Array.Empty<FacilityBufferDestinationCustodyDrainSnapshot>())
            .Where(value => value != null)
            .OrderBy(value => value.StepOperationId, StringComparer.Ordinal)
            .ToArray();
        if (expected.Any(value => value.Phase !=
                FacilityBufferDestinationCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc)
            || expected.Select(value => value.StepOperationId)
                .Distinct(StringComparer.Ordinal).Count() != expected.Length)
        {
            failureReason =
                "facility-buffer-custody-checkpoint-gc-input-invalid";
            return false;
        }

        List<ProductionInputDestinationCustodyDrainSaveData> rows = new(
            expected.Length);
        foreach (FacilityBufferDestinationCustodyDrainSnapshot snapshot in
                 expected)
        {
            if (!inner.TryCapture(
                    snapshot.StepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData row)
                || !FacilityBufferDestinationCustodyDrainProjection
                    .AreExactEqual(
                        snapshot,
                        FacilityBufferDestinationCustodyDrainProjection
                            .ProjectValidated(row)))
            {
                failureReason =
                    "facility-buffer-custody-checkpoint-gc-live-drift:"
                    + snapshot.StepOperationId;
                return false;
            }
            rows.Add(row);
        }

        if (!checkpointGc.TryPrepareCheckpointGarbageCollection(
                rows,
                out IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                    innerCandidate,
                out failureReason))
        {
            return false;
        }
        candidate = new CheckpointGcCandidate(innerCandidate);
        return true;
    }

    public bool TryPublishCheckpointGarbageCollection(
        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason)
    {
        if (!TryRequireCheckpointCandidate(
                candidate,
                out CheckpointGcCandidate exact,
                out IProductionInputDestinationCustodyDrainCheckpointGcPort gc,
                out failureReason))
        {
            return false;
        }
        return gc.TryPublishCheckpointGarbageCollection(
            exact.Inner,
            out failureReason);
    }

    public void RollbackCheckpointGarbageCollection(
        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate)
    {
        if (!TryRequireCheckpointCandidate(
                candidate,
                out CheckpointGcCandidate exact,
                out IProductionInputDestinationCustodyDrainCheckpointGcPort gc,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
        gc.RollbackCheckpointGarbageCollection(exact.Inner);
    }

    public void CompleteCheckpointGarbageCollection(
        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate)
    {
        if (!TryRequireCheckpointCandidate(
                candidate,
                out CheckpointGcCandidate exact,
                out IProductionInputDestinationCustodyDrainCheckpointGcPort gc,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
        gc.CompleteCheckpointGarbageCollection(exact.Inner);
        exact.Completed = true;
    }

    private bool TryRequireCheckpointCandidate(
        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate,
        out CheckpointGcCandidate exact,
        out IProductionInputDestinationCustodyDrainCheckpointGcPort gc,
        out string failureReason)
    {
        exact = candidate as CheckpointGcCandidate;
        gc = inner as IProductionInputDestinationCustodyDrainCheckpointGcPort;
        if (exact == null || exact.Completed || gc == null)
        {
            failureReason =
                "facility-buffer-custody-checkpoint-gc-candidate-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private sealed class CheckpointGcCandidate :
        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal IProductionInputDestinationCustodyDrainCheckpointGcCandidate
            Inner { get; }
        internal bool Completed { get; set; }
    }

    private FacilityBufferDestinationCustodyDrainResult Project(
        ProductionInputDestinationCustodyDrainResult result,
        string stepOperationId)
    {
        TryCapture(stepOperationId, out FacilityBufferDestinationCustodyDrainSnapshot
            snapshot);
        return new FacilityBufferDestinationCustodyDrainResult(
            result.Status switch
            {
                ProductionInputDestinationCustodyDrainStatus.Applied =>
                    FacilityBufferDestinationCustodyDrainStatus.Applied,
                ProductionInputDestinationCustodyDrainStatus.Replay =>
                    FacilityBufferDestinationCustodyDrainStatus.Replay,
                ProductionInputDestinationCustodyDrainStatus.Deferred =>
                    FacilityBufferDestinationCustodyDrainStatus.Deferred,
                _ => FacilityBufferDestinationCustodyDrainStatus.Conflict
            },
            snapshot,
            result.FailureReason);
    }

    private static FacilityBufferDestinationCustodyDrainResult Conflict(
        string failureReason) => new(
        FacilityBufferDestinationCustodyDrainStatus.Conflict,
        null,
        failureReason);
}
