using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Apparel-owned durable producer for one terminal work-order operation. The
/// producer is persisted before any lease, pending-effect or source-order
/// mutation and therefore makes every later effect replayable after a crash.
/// </summary>
public sealed class ProductionApparelOrderTerminalDrainOutbox :
    IProductionApparelOrderTerminalDrainQuery,
    IProductionApparelOrderTerminalDrainCommand,
    IProductionApparelOrderTerminalDrainCheckpointGcPort
{
    private sealed class State
    {
        internal Dictionary<string, ProductionApparelOrderTerminalDrainSaveData>
            ByStepOperationId { get; } = new(StringComparer.Ordinal);
    }

    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly IApparelLeaseAuthorityQuery leaseQuery;
    private readonly IApparelLeaseAuthorityCommand leaseCommand;
    private readonly IProductionApparelOrderTerminalEffectPort effects;
    private readonly IProductionApparelOrderSourceTerminalPort source;
    private readonly IProductionApparelTerminalStateCheckpointGcPort
        terminalStateGc;
    private CheckpointGcCandidate activeCheckpointGcCandidate;

    public ProductionApparelOrderTerminalDrainOutbox(
        DungeonRuntimeAggregateRootStore rootStore,
        IApparelLeaseAuthorityQuery leaseQuery,
        IApparelLeaseAuthorityCommand leaseCommand,
        IProductionApparelOrderTerminalEffectPort effects,
        IProductionApparelOrderSourceTerminalPort source)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
        this.leaseQuery = leaseQuery
            ?? throw new ArgumentNullException(nameof(leaseQuery));
        this.leaseCommand = leaseCommand
            ?? throw new ArgumentNullException(nameof(leaseCommand));
        this.effects = effects
            ?? throw new ArgumentNullException(nameof(effects));
        this.source = source
            ?? throw new ArgumentNullException(nameof(source));
        terminalStateGc = effects as
            IProductionApparelTerminalStateCheckpointGcPort;
    }

    private State Current => rootStore.GetOrCreate(() => new State());

    public bool TryCaptureLiveOrder(
        string orderId,
        out ApparelWorkOrderSaveData sourceOrder,
        out string sourceOrderFingerprint,
        out string failureReason)
    {
        sourceOrder = null;
        sourceOrderFingerprint = string.Empty;
        failureReason = string.Empty;
        if (!Token(orderId)
            || !source.TryCaptureLiveOrder(
                orderId,
                out ApparelWorkOrderSaveData live,
                out failureReason)
            || !ProductionApparelOrderTerminalDrainCanonical
                .IsValidSourceOrder(live)
            || !string.Equals(live.orderId, orderId, StringComparison.Ordinal))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-apparel-terminal-live-source-invalid"
                : failureReason;
            return false;
        }
        sourceOrder = ProductionApparelOrderTerminalDrainCanonical.CloneOrder(live);
        sourceOrderFingerprint = ProductionApparelOrderTerminalDrainCanonical
            .CreateSourceOrderFingerprint(sourceOrder);
        failureReason = string.Empty;
        return true;
    }

    [GameplayInternalOnly(
        "Persists one frozen apparel order before any terminal child effect may run.",
        "Apparel destructive terminal drain participant only")]
    public ProductionApparelOrderTerminalDrainResult TryPrepare(
        ProductionApparelOrderTerminalDrainRequest request)
    {
        if (request == null
            || !Token(request.StepOperationId)
            || !ProductionApparelOrderTerminalDrainCanonical
                .IsValidSourceOrder(request.SourceOrder)
            || !ProductionApparelOrderTerminalDrainCanonical.IsDigest(
                request.RequestFingerprint))
        {
            return Conflict("production-apparel-terminal-request-invalid");
        }
        if (Current.ByStepOperationId.TryGetValue(
                request.StepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData existing))
        {
            string expectedReplay = ProductionApparelOrderTerminalDrainCanonical
                .CreateRequestFingerprint(
                    request.ParentOperationId,
                    request.StepOperationId,
                    request.OwnerStableId,
                    request.SourceOrder,
                    request.HasLeaseAuthority,
                    request.LeaseAuthorityFingerprint,
                    request.PendingEffect);
            return string.Equals(expectedReplay, request.RequestFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(existing.requestFingerprint,
                    request.RequestFingerprint, StringComparison.Ordinal)
                ? Result(existing, ProductionApparelOrderTerminalDrainStatus.Replay)
                : Conflict("production-apparel-terminal-request-conflict");
        }
        if (!TryValidateRequest(request, out string failureReason))
            return Conflict(failureReason);
        if (Current.ByStepOperationId.Values.Any(value => value != null
                && string.Equals(value.orderId, request.SourceOrder.orderId,
                    StringComparison.Ordinal)))
        {
            return Conflict("production-apparel-terminal-source-already-owned");
        }

        ProductionApparelOrderTerminalDrainSaveData prepared = new()
        {
            parentOperationId = request.ParentOperationId,
            stepOperationId = request.StepOperationId,
            ownerStableId = request.OwnerStableId,
            orderId = request.SourceOrder.orderId,
            facilityId = request.SourceOrder.facilityInstanceId,
            orderKind = request.SourceOrder.kind,
            sourceOrder = ProductionApparelOrderTerminalDrainCanonical.CloneOrder(
                request.SourceOrder),
            sourceOrderFingerprint = ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceOrderFingerprint(request.SourceOrder),
            hasLeaseAuthority = request.HasLeaseAuthority,
            leaseAuthorityFingerprint = request.LeaseAuthorityFingerprint,
            pendingEffect = request.PendingEffect?.Clone(),
            requestFingerprint = request.RequestFingerprint,
            phase = ProductionApparelOrderTerminalDrainPhase
                .PreparedAwaitingLeaseAuthorityRelease
        };
        Current.ByStepOperationId.Add(prepared.stepOperationId, prepared.Clone());
        return Result(prepared, ProductionApparelOrderTerminalDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Advances one monotonic apparel terminal phase using exact child receipts.",
        "Apparel destructive terminal drain participant only")]
    public ProductionApparelOrderTerminalDrainResult TryProgress(
        string stepOperationId)
    {
        if (!TryGet(stepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData value))
            return Conflict("production-apparel-terminal-producer-missing");
        return value.phase switch
        {
            ProductionApparelOrderTerminalDrainPhase
                .PreparedAwaitingLeaseAuthorityRelease =>
                TryReleaseLeaseAuthority(value),
            ProductionApparelOrderTerminalDrainPhase
                .LeaseAuthorityReleasedAwaitingTerminalEffect =>
                TryCommitTerminalEffect(value),
            ProductionApparelOrderTerminalDrainPhase
                .TerminalEffectCommittedAwaitingSourceOrderTerminal =>
                TryCommitSourceTerminal(value),
            ProductionApparelOrderTerminalDrainPhase
                .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement or
            ProductionApparelOrderTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc =>
                Result(value, ProductionApparelOrderTerminalDrainStatus.Replay),
            _ => Conflict("production-apparel-terminal-phase-invalid")
        };
    }

    [GameplayInternalOnly(
        "Acknowledges an exact terminal receipt only after the upper destructive journal recorded it.",
        "Apparel destructive terminal drain participant only")]
    public ProductionApparelOrderTerminalDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (!TryGet(stepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData value))
            return Conflict("production-apparel-terminal-producer-missing");
        if (!string.Equals(value.receiptFingerprint, receiptFingerprint,
                StringComparison.Ordinal))
            return Conflict("production-apparel-terminal-receipt-conflict");
        if (value.phase == ProductionApparelOrderTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
            return Result(value, ProductionApparelOrderTerminalDrainStatus.Replay);
        if (value.phase != ProductionApparelOrderTerminalDrainPhase
                .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement)
            return Deferred(value, "production-apparel-terminal-not-committed");
        value.phase = ProductionApparelOrderTerminalDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
        Store(value);
        return Result(value, ProductionApparelOrderTerminalDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Removes only the producer tombstone after lower effect and source receipts were checkpoint-collected first.",
        "Destructive drain checkpoint GC only")]
    public ProductionApparelOrderTerminalDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (!TryGet(stepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData value))
        {
            return new ProductionApparelOrderTerminalDrainResult(
                ProductionApparelOrderTerminalDrainStatus.Replay,
                ProductionApparelOrderTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
                string.Empty,
                receiptFingerprint,
                string.Empty);
        }
        if (value.phase != ProductionApparelOrderTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
            return Deferred(value, "production-apparel-terminal-not-acknowledged");
        if (!string.Equals(value.receiptFingerprint, receiptFingerprint,
                StringComparison.Ordinal))
            return Conflict("production-apparel-terminal-receipt-conflict");

        // This pure foundation deliberately does not own lower receipt GC.
        // Registration must place the lower effect/source tombstones before
        // this producer in the reverse-DAG checkpoint collector.
        if (effects.TryCaptureTerminalEffectReceipt(
                value.terminalEffectReceipt.commitId, out _)
            || source.TryCaptureSourceTerminalReceipt(
                value.sourceTerminalReceipt.commitId, out _))
        {
            return Deferred(value,
                "production-apparel-terminal-child-receipt-still-live");
        }
        Current.ByStepOperationId.Remove(stepOperationId);
        return Result(value, ProductionApparelOrderTerminalDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Recovers exactly one current-format apparel terminal producer phase.",
        "Destructive drain recovery runner only")]
    public ProductionApparelOrderTerminalDrainResult TryRecover(
        string stepOperationId) => TryProgress(stepOperationId);

    public bool TryCapture(
        string stepOperationId,
        out ProductionApparelOrderTerminalDrainSaveData record)
    {
        record = null;
        if (!TryGet(stepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData value))
            return false;
        record = value.Clone();
        return true;
    }

    public IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData>
        CaptureCurrentFormat() => Current.ByStepOperationId.Values
        .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
        .Select(value => value.Clone())
        .ToArray();

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
                entries,
            out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                candidate)
    {
        candidate = null;
        if (activeCheckpointGcCandidate != null)
        {
            return GcResult(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                context,
                "production-apparel-checkpoint-gc-already-active");
        }

        ProductionFacilityDestructiveDrainEntrySaveData[] orderedEntries =
            (entries ?? Array.Empty<
                    ProductionFacilityDestructiveDrainEntrySaveData>())
            .OrderBy(value => value?.operationId, StringComparer.Ordinal)
            .ToArray();
        if (orderedEntries.Any(value => value == null)
            || orderedEntries.Select(value => value.operationId)
                .Distinct(StringComparer.Ordinal).Count()
                != orderedEntries.Length)
        {
            return GcResult(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                context,
                "production-apparel-checkpoint-gc-entry-invalid");
        }

        List<ProductionApparelOrderTerminalDrainSaveData> producers = new();
        foreach (ProductionFacilityDestructiveDrainEntrySaveData entry in
                 orderedEntries)
        {
            ProductionFacilityDestructiveDrainParticipantSaveData[] rows =
                (entry.participants ?? new List<
                    ProductionFacilityDestructiveDrainParticipantSaveData>())
                .Where(value => value != null
                    && string.Equals(
                        value.participantId,
                        ProductionFacilityDestructiveDrainParticipantIds
                            .ApparelWorkOrders,
                        StringComparison.Ordinal))
                .ToArray();
            if (rows.Length != 1)
            {
                return GcResult(
                    ProductionFacilityDestructiveDrainCheckpointGcStatus
                        .Corruption,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantTopologyMismatch,
                    context,
                    "production-apparel-checkpoint-gc-participant-row-invalid");
            }
            foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                     rows[0].owners ?? new List<
                         ProductionFacilityDestructiveDrainOwnerSaveData>())
            {
                if (owner == null
                    || owner.phase != ProductionFacilityDestructiveDrainStepPhase
                        .OwnerAcknowledged
                    || !TryCapture(
                        owner.stepOperationId,
                        out ProductionApparelOrderTerminalDrainSaveData producer)
                    || !ProducerMatchesUpper(producer, entry, owner))
                {
                    return GcResult(
                        ProductionFacilityDestructiveDrainCheckpointGcStatus
                            .Corruption,
                        ProductionFacilityDestructiveDrainCheckpointGcReason
                            .ParticipantPrepareFailed,
                        context,
                        "production-apparel-checkpoint-gc-upper-lower-gap");
                }
                producers.Add(producer.Clone());
            }
        }
        if (producers.Select(value => value.stepOperationId)
                .Distinct(StringComparer.Ordinal).Count() != producers.Count)
        {
            return GcResult(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                context,
                "production-apparel-checkpoint-gc-producer-duplicate");
        }
        if (producers.Count > 0 && terminalStateGc == null)
        {
            return GcResult(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .MissingParticipant,
                context,
                "production-apparel-checkpoint-gc-terminal-state-port-missing");
        }

        IProductionApparelTerminalStateCheckpointGcCandidate lowerCandidate =
            null;
        if (terminalStateGc != null
            && !terminalStateGc.TryPrepareCheckpointGarbageCollection(
                producers,
                out lowerCandidate,
                out string lowerFailure))
        {
            return GcResult(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPrepareFailed,
                context,
                "production-apparel-checkpoint-gc-terminal-state-prepare-failed:"
                + lowerFailure);
        }

        activeCheckpointGcCandidate = new CheckpointGcCandidate(
            context,
            orderedEntries.Select(value => value.operationId).ToArray(),
            producers,
            lowerCandidate);
        candidate = activeCheckpointGcCandidate;
        return GcResult(
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            context,
            "production-apparel-checkpoint-gc-prepared");
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.ProducersPublished)
        {
            return GcResult(
                ProductionFacilityDestructiveDrainCheckpointGcStatus
                    .AlreadyApplied,
                ProductionFacilityDestructiveDrainCheckpointGcReason.None,
                exact.Context,
                "production-apparel-checkpoint-gc-already-published",
                exact.OperationIds.Count);
        }
        if (exact.LowerCandidate != null && !exact.LowerPublished)
        {
            if (!terminalStateGc.TryPublishCheckpointGarbageCollection(
                    exact.LowerCandidate,
                    out string lowerFailure))
            {
                return GcResult(
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantPublishFailed,
                    exact.Context,
                    "production-apparel-checkpoint-gc-terminal-state-publish-failed:"
                    + lowerFailure);
            }
            exact.LowerPublished = true;
        }
        if (exact.Producers.Any(expected =>
                !Current.ByStepOperationId.TryGetValue(
                    expected.stepOperationId,
                    out ProductionApparelOrderTerminalDrainSaveData current)
                || !ProducerEquals(current, expected)))
        {
            return GcResult(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .LiveAuthorityChanged,
                exact.Context,
                "production-apparel-checkpoint-gc-producer-changed");
        }
        foreach (ProductionApparelOrderTerminalDrainSaveData expected in
                 exact.Producers)
            Current.ByStepOperationId.Remove(expected.stepOperationId);
        exact.ProducersPublished = true;
        return GcResult(
            ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            ProductionFacilityDestructiveDrainCheckpointGcReason.None,
            exact.Context,
            "production-apparel-checkpoint-gc-published",
            exact.OperationIds.Count);
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.ProducersPublished)
        {
            if (exact.Producers.Any(expected =>
                    Current.ByStepOperationId.ContainsKey(
                        expected.stepOperationId)))
            {
                throw new InvalidOperationException(
                    "Apparel checkpoint-GC producer rollback would overwrite live authority.");
            }
            foreach (ProductionApparelOrderTerminalDrainSaveData expected in
                     exact.Producers)
            {
                Current.ByStepOperationId.Add(
                    expected.stepOperationId,
                    expected.Clone());
            }
            exact.ProducersPublished = false;
        }
        if (exact.LowerPublished)
        {
            terminalStateGc.RollbackCheckpointGarbageCollection(
                exact.LowerCandidate);
            exact.LowerPublished = false;
        }
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.LowerCandidate != null)
            terminalStateGc.CompleteCheckpointGarbageCollection(
                exact.LowerCandidate);
        activeCheckpointGcCandidate = null;
    }

    [GameplayInternalOnly(
        "Atomically restores the apparel producer only after exact lower-authority join validation.",
        "Production save restore coordinator only")]
    public bool TryRestoreCurrentFormat(
        IEnumerable<ProductionApparelOrderTerminalDrainSaveData> records,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionApparelOrderTerminalDrainSaveData[] ordered = (records
                ?? Array.Empty<ProductionApparelOrderTerminalDrainSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value =>
                !ProductionApparelOrderTerminalDrainCanonical.IsValidSave(value))
            || Duplicates(ordered.Select(value => value.stepOperationId))
            || Duplicates(ordered.Select(value => value.orderId)))
        {
            failureReason = "production-apparel-terminal-restore-invalid";
            return false;
        }

        ProductionApparelOrderTerminalEffectReceipt[] effectRows =
            (effects.CaptureTerminalEffectReceipts()
                ?? Array.Empty<ProductionApparelOrderTerminalEffectReceipt>())
            .Select(value => value?.Clone())
            .ToArray();
        ProductionApparelOrderSourceTerminalReceipt[] sourceRows =
            (source.CaptureSourceTerminalReceipts()
                ?? Array.Empty<ProductionApparelOrderSourceTerminalReceipt>())
            .Select(value => value?.Clone())
            .ToArray();
        if (effectRows.Any(value => value == null)
            || sourceRows.Any(value => value == null)
            || Duplicates(effectRows.Select(value => value.commitId))
            || Duplicates(sourceRows.Select(value => value.commitId)))
        {
            failureReason =
                "production-apparel-terminal-restore-child-evidence-invalid";
            return false;
        }

        Dictionary<string, ProductionApparelOrderTerminalDrainSaveData> byStep =
            ordered.ToDictionary(value => value.stepOperationId,
                StringComparer.Ordinal);
        if (effectRows.Any(value => !byStep.ContainsKey(value.stepOperationId))
            || sourceRows.Any(value => !byStep.ContainsKey(value.stepOperationId)))
        {
            failureReason =
                "production-apparel-terminal-restore-child-or-pending-orphan";
            return false;
        }
        foreach (ProductionApparelOrderTerminalDrainSaveData value in ordered)
        {
            if (!ValidateRestoreJoin(value, effectRows, sourceRows,
                    out failureReason))
                return false;
        }

        State restored = new();
        foreach (ProductionApparelOrderTerminalDrainSaveData value in ordered)
            restored.ByStepOperationId.Add(value.stepOperationId, value.Clone());
        rootStore.Replace(restored);
        return true;
    }

    private ProductionApparelOrderTerminalDrainResult TryReleaseLeaseAuthority(
        ProductionApparelOrderTerminalDrainSaveData value)
    {
        if (value.hasLeaseAuthority)
        {
            ApparelLeaseAuthorityReleaseResult release = leaseCommand
                .TryReleaseExact(
                    value.orderId,
                    value.leaseAuthorityFingerprint,
                    ItemReservationReleaseReason.OwnerRemoved);
            if (release.Status == ApparelLeaseAuthorityReleaseStatus.Conflict)
                return Conflict("production-apparel-terminal-lease-conflict:"
                    + release.FailureReason);
        }
        else if (leaseQuery.TryCapture(
                value.orderId,
                out _,
                out string unexpectedLeaseFailure)
            || !string.Equals(unexpectedLeaseFailure,
                "apparel-lease-authority-missing:" + value.orderId,
                StringComparison.Ordinal))
        {
            return Conflict("production-apparel-terminal-unexpected-lease-authority");
        }

        value.leaseReleaseCommitId =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateLeaseReleaseCommitId(
                    value.stepOperationId, value.requestFingerprint);
        value.leaseReleaseReceiptFingerprint =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateLeaseReleaseReceiptFingerprint(
                    value.requestFingerprint,
                    value.leaseAuthorityFingerprint,
                    value.leaseReleaseCommitId);
        value.phase = ProductionApparelOrderTerminalDrainPhase
            .LeaseAuthorityReleasedAwaitingTerminalEffect;
        Store(value);
        return Result(value, ProductionApparelOrderTerminalDrainStatus.Applied);
    }

    private ProductionApparelOrderTerminalDrainResult TryCommitTerminalEffect(
        ProductionApparelOrderTerminalDrainSaveData value)
    {
        ProductionApparelOrderTerminalEffectReceipt expected =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateTerminalEffectReceipt(
                    value.stepOperationId,
                    value.sourceOrder,
                    value.sourceOrderFingerprint,
                    value.pendingEffect);
        ProductionApparelOrderTerminalEffectApplyResult applied = effects
            .TryCommitTerminalEffect(expected, value.pendingEffect);
        if (applied.Status == ProductionApparelOrderTerminalDrainStatus.Conflict)
            return Conflict("production-apparel-terminal-effect-conflict:"
                + applied.FailureReason);
        if (applied.Status == ProductionApparelOrderTerminalDrainStatus.Deferred)
            return Deferred(value, "production-apparel-terminal-effect-deferred:"
                + applied.FailureReason);
        if (!ProductionApparelOrderTerminalDrainCanonical.EffectReceiptEquals(
                applied.Receipt, expected))
            return Conflict("production-apparel-terminal-effect-receipt-conflict");
        value.terminalEffectReceipt = expected.Clone();
        value.phase = ProductionApparelOrderTerminalDrainPhase
            .TerminalEffectCommittedAwaitingSourceOrderTerminal;
        Store(value);
        return Result(value, ProductionApparelOrderTerminalDrainStatus.Applied);
    }

    private ProductionApparelOrderTerminalDrainResult TryCommitSourceTerminal(
        ProductionApparelOrderTerminalDrainSaveData value)
    {
        ProductionApparelOrderSourceTerminalReceipt expected =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceTerminalReceipt(
                    value.stepOperationId,
                    value.sourceOrder,
                    value.sourceOrderFingerprint,
                    value.terminalEffectReceipt.receiptFingerprint);
        ProductionApparelOrderSourceTerminalApplyResult applied = source
            .TryCommitSourceTerminal(expected);
        if (applied.Status == ProductionApparelOrderTerminalDrainStatus.Conflict)
            return Conflict("production-apparel-terminal-source-conflict:"
                + applied.FailureReason);
        if (applied.Status == ProductionApparelOrderTerminalDrainStatus.Deferred)
            return Deferred(value, "production-apparel-terminal-source-deferred:"
                + applied.FailureReason);
        if (!ProductionApparelOrderTerminalDrainCanonical.SourceReceiptEquals(
                applied.Receipt, expected))
            return Conflict("production-apparel-terminal-source-receipt-conflict");

        value.sourceTerminalReceipt = expected.Clone();
        value.commitId = ProductionApparelOrderTerminalDrainCanonical
            .CreateCommitId(value.stepOperationId, value.requestFingerprint);
        value.receiptFingerprint = ProductionApparelOrderTerminalDrainCanonical
            .CreateReceiptFingerprint(
                value.requestFingerprint,
                value.leaseReleaseReceiptFingerprint,
                value.terminalEffectReceipt.receiptFingerprint,
                value.sourceTerminalReceipt.receiptFingerprint,
                value.commitId);
        value.phase = ProductionApparelOrderTerminalDrainPhase
            .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement;
        Store(value);
        return Result(value, ProductionApparelOrderTerminalDrainStatus.Applied);
    }

    private bool TryValidateRequest(
        ProductionApparelOrderTerminalDrainRequest request,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (request == null
            || !Token(request.ParentOperationId)
            || !Token(request.StepOperationId)
            || !Token(request.OwnerStableId)
            || !ProductionApparelOrderTerminalDrainCanonical
                .IsValidSourceOrder(request.SourceOrder)
            || !ProductionApparelOrderTerminalDrainCanonical.IsDigest(
                request.LeaseAuthorityFingerprint)
            || !ProductionApparelOrderTerminalDrainCanonical.IsDigest(
                request.RequestFingerprint))
        {
            failureReason = "production-apparel-terminal-request-invalid";
            return false;
        }
        if (!string.Equals(request.OwnerStableId,
                ProductionFacilityDestructiveDrainOwnerStableIds
                    .ApparelWorkOrder(request.SourceOrder.orderId),
                StringComparison.Ordinal))
        {
            failureReason = "production-apparel-terminal-owner-invalid";
            return false;
        }
        if (!ProductionApparelOrderTerminalDrainCanonical
                .TryCreatePendingEffectIdentity(
                    request.SourceOrder,
                    out ProductionApparelOrderPendingEffectIdentity expectedPending,
                    out failureReason)
            || !PendingEquals(expectedPending, request.PendingEffect))
            return false;
        string expectedRequest = ProductionApparelOrderTerminalDrainCanonical
            .CreateRequestFingerprint(
                request.ParentOperationId,
                request.StepOperationId,
                request.OwnerStableId,
                request.SourceOrder,
                request.HasLeaseAuthority,
                request.LeaseAuthorityFingerprint,
                request.PendingEffect);
        if (!string.Equals(expectedRequest, request.RequestFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-apparel-terminal-request-fingerprint-invalid";
            return false;
        }
        if (!TryCaptureLiveOrder(
                request.SourceOrder.orderId,
                out ApparelWorkOrderSaveData live,
                out string liveFingerprint,
                out failureReason)
            || !string.Equals(liveFingerprint,
                ProductionApparelOrderTerminalDrainCanonical
                    .CreateSourceOrderFingerprint(request.SourceOrder),
                StringComparison.Ordinal)
            || !string.Equals(live.facilityInstanceId,
                request.SourceOrder.facilityInstanceId,
                StringComparison.Ordinal))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-apparel-terminal-live-source-conflict"
                : failureReason;
            return false;
        }
        bool liveLease = leaseQuery.TryCapture(
            request.SourceOrder.orderId,
            out ApparelLeaseAuthoritySnapshot lease,
            out string leaseFailure);
        if (request.HasLeaseAuthority)
        {
            if (!liveLease || lease == null
                || !string.Equals(lease.Fingerprint,
                    request.LeaseAuthorityFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = "production-apparel-terminal-lease-drift:"
                    + leaseFailure;
                return false;
            }
        }
        else if (liveLease
            || !string.Equals(leaseFailure,
                "apparel-lease-authority-missing:" + request.SourceOrder.orderId,
                StringComparison.Ordinal)
            || !string.Equals(request.LeaseAuthorityFingerprint,
                ProductionApparelOrderTerminalDrainCanonical
                    .CreateNoLeaseAuthorityFingerprint(
                        request.SourceOrder.orderId),
                StringComparison.Ordinal))
        {
            failureReason = "production-apparel-terminal-no-lease-proof-invalid";
            return false;
        }
        return true;
    }

    private bool ValidateRestoreJoin(
        ProductionApparelOrderTerminalDrainSaveData value,
        IReadOnlyList<ProductionApparelOrderTerminalEffectReceipt> effectRows,
        IReadOnlyList<ProductionApparelOrderSourceTerminalReceipt> sourceRows,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionApparelOrderTerminalEffectReceipt expectedEffect =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateTerminalEffectReceipt(
                    value.stepOperationId,
                    value.sourceOrder,
                    value.sourceOrderFingerprint,
                    value.pendingEffect);
        ProductionApparelOrderSourceTerminalReceipt expectedSource =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceTerminalReceipt(
                    value.stepOperationId,
                    value.sourceOrder,
                    value.sourceOrderFingerprint,
                    expectedEffect.receiptFingerprint);
        ProductionApparelOrderTerminalEffectReceipt[] effectMatches = effectRows
            .Where(row => string.Equals(row.stepOperationId,
                value.stepOperationId, StringComparison.Ordinal)).ToArray();
        ProductionApparelOrderSourceTerminalReceipt[] sourceMatches = sourceRows
            .Where(row => string.Equals(row.stepOperationId,
                value.stepOperationId, StringComparison.Ordinal)).ToArray();
        if (effectMatches.Length > 1 || sourceMatches.Length > 1)
        {
            failureReason = "production-apparel-terminal-restore-evidence-duplicate";
            return false;
        }
        bool effectAheadAllowed = value.phase ==
            ProductionApparelOrderTerminalDrainPhase
                .LeaseAuthorityReleasedAwaitingTerminalEffect;
        bool effectRequired = value.phase >=
            ProductionApparelOrderTerminalDrainPhase
                .TerminalEffectCommittedAwaitingSourceOrderTerminal;
        bool sourceAheadAllowed = value.phase ==
            ProductionApparelOrderTerminalDrainPhase
                .TerminalEffectCommittedAwaitingSourceOrderTerminal;
        bool sourceRequired = value.phase >=
            ProductionApparelOrderTerminalDrainPhase
                .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement;
        if (effectMatches.Length == 1
            && (!effectAheadAllowed && !effectRequired
                || !ProductionApparelOrderTerminalDrainCanonical
                    .EffectReceiptEquals(effectMatches[0], expectedEffect)))
        {
            failureReason = "production-apparel-terminal-restore-effect-conflict";
            return false;
        }
        if (effectRequired && effectMatches.Length != 1)
        {
            failureReason = "production-apparel-terminal-restore-effect-missing";
            return false;
        }
        if (sourceMatches.Length == 1
            && (!sourceAheadAllowed && !sourceRequired
                || effectMatches.Length != 1
                || !ProductionApparelOrderTerminalDrainCanonical
                    .SourceReceiptEquals(sourceMatches[0], expectedSource)))
        {
            failureReason = "production-apparel-terminal-restore-source-conflict";
            return false;
        }
        if (sourceRequired && sourceMatches.Length != 1)
        {
            failureReason = "production-apparel-terminal-restore-source-missing";
            return false;
        }

        bool liveSource = TryCaptureLiveOrder(
            value.orderId,
            out _,
            out string liveSourceFingerprint,
            out string liveSourceFailure);
        if (sourceMatches.Length == 0)
        {
            if (!liveSource
                || !string.Equals(liveSourceFingerprint,
                    value.sourceOrderFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "production-apparel-terminal-restore-live-source-missing-or-drifted:"
                    + liveSourceFailure;
                return false;
            }
        }
        else if (liveSource)
        {
            failureReason =
                "production-apparel-terminal-restore-terminal-receipt-with-live-source";
            return false;
        }

        bool liveLease = leaseQuery.TryCapture(
            value.orderId,
            out ApparelLeaseAuthoritySnapshot lease,
            out string leaseFailure);
        bool leaseMissing = !liveLease && string.Equals(
            leaseFailure,
            "apparel-lease-authority-missing:" + value.orderId,
            StringComparison.Ordinal);
        if (!value.hasLeaseAuthority)
        {
            if (!leaseMissing)
            {
                failureReason =
                    "production-apparel-terminal-restore-unexpected-lease";
                return false;
            }
        }
        else if (value.phase == ProductionApparelOrderTerminalDrainPhase
                     .PreparedAwaitingLeaseAuthorityRelease)
        {
            // Missing is the only legal release-ahead crash prefix because the
            // producer was already durable before TryReleaseExact was called.
            if (!leaseMissing && (!liveLease || lease == null
                || !string.Equals(lease.Fingerprint,
                    value.leaseAuthorityFingerprint,
                    StringComparison.Ordinal)))
            {
                failureReason =
                    "production-apparel-terminal-restore-lease-drift";
                return false;
            }
        }
        else if (!leaseMissing)
        {
            failureReason =
                "production-apparel-terminal-restore-released-lease-still-live";
            return false;
        }
        return true;
    }

    private bool TryGet(
        string stepOperationId,
        out ProductionApparelOrderTerminalDrainSaveData value)
    {
        value = null;
        if (!Token(stepOperationId)
            || !Current.ByStepOperationId.TryGetValue(stepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData stored))
            return false;
        value = stored.Clone();
        return true;
    }

    private void Store(ProductionApparelOrderTerminalDrainSaveData value) =>
        Current.ByStepOperationId[value.stepOperationId] = value.Clone();

    private static bool ProducerMatchesUpper(
        ProductionApparelOrderTerminalDrainSaveData producer,
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        ProductionFacilityDestructiveDrainOwnerSaveData owner) =>
        ProductionApparelOrderTerminalDrainCanonical.IsValidSave(producer)
        && producer.phase == ProductionApparelOrderTerminalDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc
        && string.Equals(
            producer.parentOperationId,
            entry.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            producer.stepOperationId,
            owner.stepOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            producer.ownerStableId,
            owner.ownerStableId,
            StringComparison.Ordinal)
        && string.Equals(
            producer.requestFingerprint,
            owner.requestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            producer.commitId,
            owner.commitId,
            StringComparison.Ordinal)
        && string.Equals(
            producer.receiptFingerprint,
            owner.receiptFingerprint,
            StringComparison.Ordinal);

    private static bool ProducerEquals(
        ProductionApparelOrderTerminalDrainSaveData left,
        ProductionApparelOrderTerminalDrainSaveData right) => left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private CheckpointGcCandidate RequireCheckpointGcCandidate(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || !ReferenceEquals(activeCheckpointGcCandidate, exact))
        {
            throw new InvalidOperationException(
                "Apparel checkpoint-GC candidate is stale or foreign.");
        }
        return exact;
    }

    private static ProductionFacilityDestructiveDrainCheckpointGcResult
        GcResult(
            ProductionFacilityDestructiveDrainCheckpointGcStatus status,
            ProductionFacilityDestructiveDrainCheckpointGcReason reason,
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            string message,
            int collectedOperationCount = 0) => new(
            status,
            reason,
            context.CheckpointSequence,
            message,
            collectedOperationCount);

    private sealed class CheckpointGcCandidate :
        IProductionFacilityDestructiveDrainCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<string> operationIds,
            IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData> producers,
            IProductionApparelTerminalStateCheckpointGcCandidate lowerCandidate)
        {
            Context = context;
            OperationIds = (operationIds ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Producers = (producers
                    ?? Array.Empty<ProductionApparelOrderTerminalDrainSaveData>())
                .Select(value => value.Clone())
                .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
                .ToArray();
            LowerCandidate = lowerCandidate;
        }

        public string ParticipantId =>
            ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders;
        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> OperationIds { get; }
        internal ProductionFacilityDestructiveDrainCheckpointGcContext Context
            { get; }
        internal IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData>
            Producers { get; }
        internal IProductionApparelTerminalStateCheckpointGcCandidate
            LowerCandidate { get; }
        internal bool LowerPublished { get; set; }
        internal bool ProducersPublished { get; set; }
    }

    private static ProductionApparelOrderTerminalDrainResult Result(
        ProductionApparelOrderTerminalDrainSaveData value,
        ProductionApparelOrderTerminalDrainStatus status) => new(
            status,
            value.phase,
            value.commitId,
            value.receiptFingerprint,
            string.Empty);

    private static ProductionApparelOrderTerminalDrainResult Deferred(
        ProductionApparelOrderTerminalDrainSaveData value,
        string failureReason) => new(
            ProductionApparelOrderTerminalDrainStatus.Deferred,
            value.phase,
            value.commitId,
            value.receiptFingerprint,
            failureReason);

    private static ProductionApparelOrderTerminalDrainResult Conflict(
        string failureReason) => new(
            ProductionApparelOrderTerminalDrainStatus.Conflict,
            ProductionApparelOrderTerminalDrainPhase
                .PreparedAwaitingLeaseAuthorityRelease,
            string.Empty,
            string.Empty,
            failureReason);

    private static bool PendingEquals(
        ProductionApparelOrderPendingEffectIdentity left,
        ProductionApparelOrderPendingEffectIdentity right)
    {
        if (left == null || right == null)
            return left == null && right == null;
        return string.Equals(left.identityFingerprint,
            right.identityFingerprint, StringComparison.Ordinal);
    }

    private static bool Duplicates(IEnumerable<string> values) =>
        values.GroupBy(value => value, StringComparer.Ordinal)
            .Any(group => group.Count() != 1);

    private static bool Token(string value) => !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
