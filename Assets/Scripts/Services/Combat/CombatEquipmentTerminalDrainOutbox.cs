using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Durable producer for craft and repair order terminalization. The producer
/// freezes the complete source before asking the Items-owned child to release
/// input custody, then records deterministic WIP/loss and exact source-removal
/// receipts. Runtime registration is valid only together with the exact source,
/// child, effect and upper-journal restore join.
/// </summary>
public sealed class CombatEquipmentTerminalDrainOutbox :
    ICombatEquipmentTerminalDrainQuery,
    ICombatEquipmentTerminalDrainCommand,
    ICombatEquipmentTerminalDrainCheckpointGcAuthority
{
    private sealed class State
    {
        internal Dictionary<string, CombatEquipmentTerminalDrainSaveData>
            ByStepOperationId { get; } = new(StringComparer.Ordinal);

        internal State Clone()
        {
            State clone = new();
            foreach (KeyValuePair<string, CombatEquipmentTerminalDrainSaveData> pair
                     in ByStepOperationId)
            {
                clone.ByStepOperationId.Add(pair.Key, pair.Value?.Clone());
            }
            return clone;
        }
    }

    private sealed class CheckpointGcCandidate :
        ICombatEquipmentTerminalDrainCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            IReadOnlyList<CombatEquipmentTerminalDrainSaveData> rows)
        {
            Rows = Array.AsReadOnly((rows
                    ?? Array.Empty<CombatEquipmentTerminalDrainSaveData>())
                .Select(value => value?.Clone())
                .ToArray());
        }

        internal IReadOnlyList<CombatEquipmentTerminalDrainSaveData> Rows { get; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }

    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly ICombatEquipmentTerminalSourceAuthority sourceAuthority;
    private readonly IProductionInputDestinationCustodyDrainOutbox inputDrain;
    private CheckpointGcCandidate activeCheckpointGcCandidate;

    public CombatEquipmentTerminalDrainOutbox(
        DungeonRuntimeAggregateRootStore rootStore,
        ICombatEquipmentTerminalSourceAuthority sourceAuthority,
        IProductionInputDestinationCustodyDrainOutbox inputDrain)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
        this.sourceAuthority = sourceAuthority
            ?? throw new ArgumentNullException(nameof(sourceAuthority));
        this.inputDrain = inputDrain
            ?? throw new ArgumentNullException(nameof(inputDrain));
    }

    private State Current => rootStore.GetOrCreate(() => new State());

    ICombatEquipmentTerminalSourceCheckpointGcAuthority
        ICombatEquipmentTerminalDrainCheckpointGcAuthority
            .SourceCheckpointGcAuthority =>
        sourceAuthority as ICombatEquipmentTerminalSourceCheckpointGcAuthority;

    public bool TryCaptureLiveSourceForPreparation(
        string ownerStableId,
        out CombatEquipmentTerminalPreparedSource prepared,
        out string failureReason)
    {
        prepared = null;
        if (!sourceAuthority.TryCaptureLiveSourceForPreparation(
                ownerStableId,
                out CombatEquipmentTerminalPreparedSource candidate,
                out failureReason)
            || candidate == null)
        {
            return false;
        }
        CombatEquipmentTerminalFrozenSubject source = candidate.Source;
        if (source == null
            || !CombatEquipmentTerminalDrainCanonical.IsValidFrozenSource(
                source.CaptureFrozen())
            || !CombatEquipmentTerminalPreparedSource.TryCreate(
                source,
                candidate.Custody,
                out prepared,
                out failureReason))
        {
            prepared = null;
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "combat-equipment-terminal-live-preparation-invalid"
                : failureReason;
            return false;
        }
        return true;
    }

    public bool TryCaptureLiveSource(
        string ownerStableId,
        out CombatEquipmentTerminalFrozenSubject source,
        out string sourceFingerprint,
        out string failureReason)
    {
        sourceFingerprint = string.Empty;
        if (!sourceAuthority.TryCaptureLiveSource(
                ownerStableId,
                out source,
                out failureReason))
        {
            return false;
        }
        if (source == null
            || !CombatEquipmentTerminalDrainCanonical.IsValidFrozenSource(
                source.CaptureFrozen()))
        {
            source = null;
            failureReason = "combat-equipment-terminal-live-source-invalid";
            return false;
        }
        sourceFingerprint = source.SourceFingerprint;
        return true;
    }

    [GameplayInternalOnly(
        "Persists one frozen craft/repair producer only after the upper destructive journal owner exists.",
        "Combat equipment destructive-drain participant only")]
    public CombatEquipmentTerminalDrainResult TryPrepare(
        CombatEquipmentTerminalDrainRequest request)
    {
        if (activeCheckpointGcCandidate != null)
            return Conflict("combat-equipment-terminal-checkpoint-gc-active");
        if (!TryValidateRequest(request, out string failureReason))
            return Conflict(failureReason);

        if (Current.ByStepOperationId.TryGetValue(
                request.StepOperationId,
                out CombatEquipmentTerminalDrainSaveData existing))
        {
            return string.Equals(
                    existing.requestFingerprint,
                    request.RequestFingerprint,
                    StringComparison.Ordinal)
                ? Result(existing, CombatEquipmentTerminalDrainStatus.Replay)
                : Conflict("combat-equipment-terminal-request-conflict");
        }
        if (Current.ByStepOperationId.Values.Any(value => value != null
                && (string.Equals(
                        value.source.ownerStableId,
                        request.Source.OwnerStableId,
                        StringComparison.Ordinal)
                    || (!string.IsNullOrEmpty(
                            request.InputDestinationDrainStepOperationId)
                        && string.Equals(
                            value.inputDestinationDrainStepOperationId,
                            request.InputDestinationDrainStepOperationId,
                            StringComparison.Ordinal)))))
        {
            return Conflict("combat-equipment-terminal-source-already-owned");
        }

        CombatEquipmentTerminalDrainSaveData prepared = new()
        {
            parentOperationId = request.ParentOperationId,
            stepOperationId = request.StepOperationId,
            source = request.Source.CaptureFrozen(),
            inputDestinationDrainStepOperationId =
                request.InputDestinationDrainStepOperationId,
            inputDestinationDrainRequestFingerprint =
                request.InputDestinationDrainRequestFingerprint,
            requestFingerprint = request.RequestFingerprint,
            phase = CombatEquipmentTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt
        };
        Current.ByStepOperationId.Add(
            prepared.stepOperationId,
            prepared.Clone());
        return Result(prepared, CombatEquipmentTerminalDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Advances one replay-safe combat producer through child acknowledgement and exact terminal effects.",
        "Combat equipment destructive-drain participant or recovery runner only")]
    public CombatEquipmentTerminalDrainResult TryProgress(
        string stepOperationId)
    {
        if (!TryGet(stepOperationId, out CombatEquipmentTerminalDrainSaveData value))
            return Conflict("combat-equipment-terminal-producer-missing");

        return value.phase switch
        {
            CombatEquipmentTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt =>
                TryRecordOrSkipInputDestinationReceipt(value),
            CombatEquipmentTerminalDrainPhase
                .InputDestinationReceiptRecordedAwaitingAcknowledgement =>
                TryAcknowledgeInputDestination(value),
            CombatEquipmentTerminalDrainPhase
                .InputDestinationAcknowledgedAwaitingTerminalEffects =>
                TryCommitTerminalEffects(value),
            CombatEquipmentTerminalDrainPhase
                .TerminalEffectsCommittedAwaitingOwnerAcknowledgement or
            CombatEquipmentTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc =>
                Result(value, CombatEquipmentTerminalDrainStatus.Replay),
            _ => Conflict("combat-equipment-terminal-phase-invalid")
        };
    }

    [GameplayInternalOnly(
        "Acknowledges the combat terminal receipt only after the upper journal stores the exact receipt.",
        "Combat equipment destructive-drain participant only")]
    public CombatEquipmentTerminalDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (!TryGet(stepOperationId, out CombatEquipmentTerminalDrainSaveData value))
            return Conflict("combat-equipment-terminal-producer-missing");
        if (!string.Equals(value.receiptFingerprint, receiptFingerprint,
                StringComparison.Ordinal))
            return Conflict("combat-equipment-terminal-receipt-conflict");
        if (value.phase == CombatEquipmentTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
            return Result(value, CombatEquipmentTerminalDrainStatus.Replay);
        if (value.phase != CombatEquipmentTerminalDrainPhase
                .TerminalEffectsCommittedAwaitingOwnerAcknowledgement)
            return Deferred(value, "combat-equipment-terminal-effect-not-committed");

        value.phase = CombatEquipmentTerminalDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
        Store(value);
        return Result(value, CombatEquipmentTerminalDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Garbage-collects the Items child before the acknowledged combat producer tombstone.",
        "Destructive-drain checkpoint GC only")]
    public CombatEquipmentTerminalDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (activeCheckpointGcCandidate != null)
            return Conflict("combat-equipment-terminal-checkpoint-gc-active");
        if (!TryGet(stepOperationId, out CombatEquipmentTerminalDrainSaveData value))
        {
            return new CombatEquipmentTerminalDrainResult(
                CombatEquipmentTerminalDrainStatus.Replay,
                CombatEquipmentTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
                string.Empty,
                receiptFingerprint,
                string.Empty);
        }
        if (value.phase != CombatEquipmentTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
            return Deferred(value, "combat-equipment-terminal-not-acknowledged");
        if (!string.Equals(value.receiptFingerprint, receiptFingerprint,
                StringComparison.Ordinal))
            return Conflict("combat-equipment-terminal-receipt-conflict");

        if (HasChild(value))
        {
            ProductionInputDestinationCustodyDrainResult child = inputDrain
                .TryGarbageCollect(
                    value.inputDestinationDrainStepOperationId,
                    value.inputDestinationDrainReceiptFingerprint);
            if (child.Status == ProductionInputDestinationCustodyDrainStatus.Conflict)
                return Conflict("combat-equipment-terminal-child-gc-conflict:"
                    + child.FailureReason);
            if (child.Status == ProductionInputDestinationCustodyDrainStatus.Deferred)
                return Deferred(value,
                    "combat-equipment-terminal-child-gc-deferred:"
                    + child.FailureReason);
        }

        CombatEquipmentTerminalFrozenSubject source =
            CombatEquipmentTerminalFrozenSubject.FromSave(value.source);
        CombatEquipmentTerminalEffectResult receiptGc = sourceAuthority
            .TryGarbageCollectReceipts(
                source,
                value.wipLossReceiptFingerprint,
                value.sourceRemovalReceiptFingerprint);
        if (receiptGc.Status == CombatEquipmentTerminalEffectStatus.Conflict)
            return Conflict("combat-equipment-terminal-receipt-gc-conflict:"
                + receiptGc.FailureReason);
        if (receiptGc.Status == CombatEquipmentTerminalEffectStatus.Deferred)
            return Deferred(value,
                "combat-equipment-terminal-receipt-gc-deferred:"
                + receiptGc.FailureReason);

        Current.ByStepOperationId.Remove(stepOperationId);
        return Result(value, CombatEquipmentTerminalDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Recovers exactly one combat producer phase from current-format durable authority.",
        "Destructive-drain recovery runner only")]
    public CombatEquipmentTerminalDrainResult TryRecover(
        string stepOperationId) => TryProgress(stepOperationId);

    public bool TryCapture(
        string stepOperationId,
        out CombatEquipmentTerminalDrainSaveData record)
    {
        record = null;
        if (!TryGet(stepOperationId, out CombatEquipmentTerminalDrainSaveData value))
            return false;
        record = value.Clone();
        return true;
    }

    public IReadOnlyList<CombatEquipmentTerminalDrainSaveData>
        CaptureCurrentFormat() => Current.ByStepOperationId.Values
        .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
        .Select(value => value.Clone())
        .ToArray();

    [GameplayInternalOnly(
        "Atomically replaces combat producer state after exact source, child, and effect join validation.",
        "Combat terminal save restore coordinator only")]
    public bool TryRestoreCurrentFormat(
        IEnumerable<CombatEquipmentTerminalDrainSaveData> records,
        IEnumerable<ProductionInputDestinationCustodyDrainSaveData> childRecords,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (activeCheckpointGcCandidate != null)
        {
            failureReason = "combat-equipment-terminal-checkpoint-gc-active";
            return false;
        }
        CombatEquipmentTerminalDrainSaveData[] ordered = (records
                ?? Array.Empty<CombatEquipmentTerminalDrainSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .ToArray();
        ProductionInputDestinationCustodyDrainSaveData[] children = (childRecords
                ?? Array.Empty<ProductionInputDestinationCustodyDrainSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .ToArray();

        if (ordered.Any(value =>
                !CombatEquipmentTerminalDrainCanonical.IsValidSave(value))
            || HasDuplicates(ordered.Select(value => value.stepOperationId), false)
            || HasDuplicates(ordered.Select(value => value.source.ownerStableId), false)
            || HasDuplicates(ordered.Select(value => value.source.sourceId), false)
            || HasDuplicates(
                ordered.Select(value =>
                    value.inputDestinationDrainStepOperationId),
                allowEmpty: true)
            || children.Any(value =>
                !ProductionInputDestinationCustodyDrainContract.IsValidSave(value))
            || HasDuplicates(children.Select(value => value.stepOperationId), false)
            || !TryValidateRestoreJoins(ordered, children, out failureReason))
        {
            if (string.IsNullOrEmpty(failureReason))
                failureReason = "combat-equipment-terminal-restore-invalid";
            return false;
        }

        State restored = new();
        foreach (CombatEquipmentTerminalDrainSaveData value in ordered)
            restored.ByStepOperationId.Add(value.stepOperationId, value.Clone());
        rootStore.Replace(restored);
        return true;
    }

    bool ICombatEquipmentTerminalDrainCheckpointGcAuthority
        .TryPrepareCheckpointGarbageCollection(
            IReadOnlyList<CombatEquipmentTerminalDrainSaveData> rows,
            out ICombatEquipmentTerminalDrainCheckpointGcCandidate candidate,
            out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        CombatEquipmentTerminalDrainSaveData[] ordered = (rows
                ?? Array.Empty<CombatEquipmentTerminalDrainSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .ToArray();
        if (activeCheckpointGcCandidate != null)
        {
            failureReason = "combat-equipment-terminal-checkpoint-gc-already-active";
            return false;
        }
        if (ordered.Any(value => value == null
                || value.phase != CombatEquipmentTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                || !CombatEquipmentTerminalDrainCanonical.IsValidSave(value))
            || HasDuplicates(ordered.Select(value => value.stepOperationId), false))
        {
            failureReason = "combat-equipment-terminal-checkpoint-gc-row-invalid";
            return false;
        }
        foreach (CombatEquipmentTerminalDrainSaveData row in ordered)
        {
            if (!Current.ByStepOperationId.TryGetValue(row.stepOperationId,
                    out CombatEquipmentTerminalDrainSaveData live)
                || !ExactCheckpointGcRow(live, row))
            {
                failureReason =
                    "combat-equipment-terminal-checkpoint-gc-live-row-changed";
                return false;
            }
        }

        CheckpointGcCandidate exact = new(ordered);
        activeCheckpointGcCandidate = exact;
        candidate = exact;
        return true;
    }

    CombatEquipmentTerminalDrainResult
        ICombatEquipmentTerminalDrainCheckpointGcAuthority
            .PublishCheckpointGarbageCollection(
                ICombatEquipmentTerminalDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.Published)
            return CheckpointGcResult(exact, CombatEquipmentTerminalDrainStatus.Replay);

        State current = Current;
        foreach (CombatEquipmentTerminalDrainSaveData row in exact.Rows)
        {
            if (!current.ByStepOperationId.TryGetValue(row.stepOperationId,
                    out CombatEquipmentTerminalDrainSaveData live)
                || !ExactCheckpointGcRow(live, row))
            {
                return new CombatEquipmentTerminalDrainResult(
                    CombatEquipmentTerminalDrainStatus.Deferred,
                    CombatEquipmentTerminalDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
                    string.Empty,
                    string.Empty,
                    "combat-equipment-terminal-checkpoint-gc-live-row-changed");
            }
        }

        State next = current.Clone();
        foreach (CombatEquipmentTerminalDrainSaveData row in exact.Rows)
            next.ByStepOperationId.Remove(row.stepOperationId);
        rootStore.Replace(next);
        exact.Published = true;
        return CheckpointGcResult(exact, CombatEquipmentTerminalDrainStatus.Applied);
    }

    void ICombatEquipmentTerminalDrainCheckpointGcAuthority
        .RollbackCheckpointGarbageCollection(
            ICombatEquipmentTerminalDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (!exact.Published)
            return;

        State next = Current.Clone();
        foreach (CombatEquipmentTerminalDrainSaveData row in exact.Rows)
        {
            if (next.ByStepOperationId.ContainsKey(row.stepOperationId)
                || next.ByStepOperationId.Values.Any(value => value != null
                    && (string.Equals(value.source?.ownerStableId,
                            row.source?.ownerStableId, StringComparison.Ordinal)
                        || string.Equals(value.source?.sourceId,
                            row.source?.sourceId, StringComparison.Ordinal)
                        || HasChild(row) && string.Equals(
                            value.inputDestinationDrainStepOperationId,
                            row.inputDestinationDrainStepOperationId,
                            StringComparison.Ordinal))))
            {
                throw new InvalidOperationException(
                    "combat-equipment-terminal-checkpoint-gc-rollback-conflict");
            }
            next.ByStepOperationId.Add(row.stepOperationId, row.Clone());
        }
        rootStore.Replace(next);
        exact.Published = false;
    }

    void ICombatEquipmentTerminalDrainCheckpointGcAuthority
        .CompleteCheckpointGarbageCollection(
            ICombatEquipmentTerminalDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        exact.Completed = true;
        activeCheckpointGcCandidate = null;
    }

    private CombatEquipmentTerminalDrainResult
        TryRecordOrSkipInputDestinationReceipt(
            CombatEquipmentTerminalDrainSaveData value)
    {
        if (!HasChild(value))
        {
            value.phase = CombatEquipmentTerminalDrainPhase
                .InputDestinationAcknowledgedAwaitingTerminalEffects;
            Store(value);
            return Result(value, CombatEquipmentTerminalDrainStatus.Applied);
        }
        if (!inputDrain.TryCapture(
                value.inputDestinationDrainStepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData child))
        {
            return Deferred(value,
                "combat-equipment-terminal-child-receipt-missing");
        }
        if (!ChildMatches(value, child)
            || child.phase < ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck)
        {
            return Conflict("combat-equipment-terminal-child-receipt-conflict");
        }

        value.inputDestinationDrainCommitId = child.commitId;
        value.inputDestinationDrainReceiptFingerprint = child.receiptFingerprint;
        value.releasedInputQuantity = child.releasedQuantity;
        value.releasedInputMassGrams = child.releasedMassGrams;
        value.phase = CombatEquipmentTerminalDrainPhase
            .InputDestinationReceiptRecordedAwaitingAcknowledgement;
        Store(value);
        return Result(value, CombatEquipmentTerminalDrainStatus.Applied);
    }

    private CombatEquipmentTerminalDrainResult TryAcknowledgeInputDestination(
        CombatEquipmentTerminalDrainSaveData value)
    {
        ProductionInputDestinationCustodyDrainResult child = inputDrain
            .TryAcknowledge(
                value.inputDestinationDrainStepOperationId,
                value.inputDestinationDrainReceiptFingerprint);
        if (child.Status == ProductionInputDestinationCustodyDrainStatus.Conflict)
            return Conflict("combat-equipment-terminal-child-ack-conflict:"
                + child.FailureReason);
        if (child.Status == ProductionInputDestinationCustodyDrainStatus.Deferred)
            return Deferred(value,
                "combat-equipment-terminal-child-ack-deferred:"
                + child.FailureReason);

        value.phase = CombatEquipmentTerminalDrainPhase
            .InputDestinationAcknowledgedAwaitingTerminalEffects;
        Store(value);
        return Result(value, CombatEquipmentTerminalDrainStatus.Applied);
    }

    private CombatEquipmentTerminalDrainResult TryCommitTerminalEffects(
        CombatEquipmentTerminalDrainSaveData value)
    {
        CombatEquipmentTerminalFrozenSubject source =
            CombatEquipmentTerminalFrozenSubject.FromSave(value.source);
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence =
            CreateInputEvidence(value);
        if (!inputEvidence.IsValidFor(source))
            return Conflict(
                "combat-equipment-terminal-input-evidence-invalid");
        CombatEquipmentTerminalWipLossReceiptSaveData expectedWip =
            CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(source);
        if (expectedWip != null
            && !TryEnsureWipReceipt(
                expectedWip,
                inputEvidence,
                out CombatEquipmentTerminalDrainStatus wipStatus,
                out string wipFailure))
        {
            return wipStatus == CombatEquipmentTerminalDrainStatus.Deferred
                ? Deferred(value, wipFailure)
                : Conflict(wipFailure);
        }

        CombatEquipmentTerminalSourceRemovalReceiptSaveData expectedRemoval =
            CombatEquipmentTerminalDrainCanonical
                .CreateSourceRemovalReceipt(source);
        if (!TryEnsureSourceRemoval(
                source,
                expectedRemoval,
                inputEvidence,
                out CombatEquipmentTerminalDrainStatus removalStatus,
                out string removalFailure))
        {
            return removalStatus == CombatEquipmentTerminalDrainStatus.Deferred
                ? Deferred(value, removalFailure)
                : Conflict(removalFailure);
        }

        value.wipLossCommitId = expectedWip?.commitId ?? string.Empty;
        value.wipLossReceiptFingerprint =
            expectedWip?.receiptFingerprint ?? string.Empty;
        value.sourceRemovalCommitId = expectedRemoval.commitId;
        value.sourceRemovalReceiptFingerprint =
            expectedRemoval.receiptFingerprint;
        value.terminalEffectFingerprint = CombatEquipmentTerminalDrainCanonical
            .CreateTerminalEffectFingerprint(
                value.requestFingerprint,
                value.inputDestinationDrainReceiptFingerprint,
                value.wipLossReceiptFingerprint,
                value.sourceRemovalReceiptFingerprint);
        value.commitId = CombatEquipmentTerminalDrainCanonical.CreateCommitId(
            value.stepOperationId,
            value.requestFingerprint);
        value.receiptFingerprint = CombatEquipmentTerminalDrainCanonical
            .CreateReceiptFingerprint(
                value.requestFingerprint,
                value.terminalEffectFingerprint,
                value.commitId);
        value.phase = CombatEquipmentTerminalDrainPhase
            .TerminalEffectsCommittedAwaitingOwnerAcknowledgement;
        Store(value);
        return Result(value, CombatEquipmentTerminalDrainStatus.Applied);
    }

    private bool TryEnsureWipReceipt(
        CombatEquipmentTerminalWipLossReceiptSaveData expected,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence,
        out CombatEquipmentTerminalDrainStatus status,
        out string failureReason)
    {
        status = CombatEquipmentTerminalDrainStatus.Conflict;
        failureReason = string.Empty;
        if (sourceAuthority.TryCaptureWipLossReceipt(
                expected.commitId,
                out CombatEquipmentTerminalWipLossReceiptSaveData existing))
        {
            if (CombatEquipmentTerminalDrainCanonical.WipReceiptEquals(
                    existing,
                    expected))
                return true;
            failureReason = "combat-equipment-terminal-wip-receipt-conflict";
            return false;
        }

        CombatEquipmentTerminalEffectResult published = sourceAuthority
            .TryPublishWipLossReceipt(expected.Clone(), inputEvidence);
        if (published.Status == CombatEquipmentTerminalEffectStatus.Deferred)
        {
            status = CombatEquipmentTerminalDrainStatus.Deferred;
            failureReason = "combat-equipment-terminal-wip-receipt-deferred:"
                + published.FailureReason;
            return false;
        }
        if (published.Status == CombatEquipmentTerminalEffectStatus.Conflict
            || !sourceAuthority.TryCaptureWipLossReceipt(
                expected.commitId,
                out existing)
            || !CombatEquipmentTerminalDrainCanonical.WipReceiptEquals(
                existing,
                expected))
        {
            failureReason = "combat-equipment-terminal-wip-receipt-conflict:"
                + published.FailureReason;
            return false;
        }
        return true;
    }

    private bool TryEnsureSourceRemoval(
        CombatEquipmentTerminalFrozenSubject source,
        CombatEquipmentTerminalSourceRemovalReceiptSaveData expected,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence,
        out CombatEquipmentTerminalDrainStatus status,
        out string failureReason)
    {
        status = CombatEquipmentTerminalDrainStatus.Conflict;
        failureReason = string.Empty;
        if (sourceAuthority.TryCaptureSourceRemovalReceipt(
                expected.commitId,
                out CombatEquipmentTerminalSourceRemovalReceiptSaveData existing))
        {
            if (!CombatEquipmentTerminalDrainCanonical.RemovalReceiptEquals(
                    existing,
                    expected)
                || sourceAuthority.TryCaptureLiveSource(
                    source.OwnerStableId,
                    out _,
                    out _))
            {
                failureReason =
                    "combat-equipment-terminal-removal-receipt-conflict";
                return false;
            }
            return true;
        }

        if (!TryCaptureLiveSource(
                source.OwnerStableId,
                out _,
                out string liveFingerprint,
                out string liveFailure)
            || !string.Equals(
                liveFingerprint,
                source.SourceFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "combat-equipment-terminal-live-source-drift:" + liveFailure;
            return false;
        }

        CombatEquipmentTerminalEffectResult removed = sourceAuthority
            .TryRemoveExactSource(source, expected.Clone(), inputEvidence);
        if (removed.Status == CombatEquipmentTerminalEffectStatus.Deferred)
        {
            status = CombatEquipmentTerminalDrainStatus.Deferred;
            failureReason = "combat-equipment-terminal-removal-deferred:"
                + removed.FailureReason;
            return false;
        }
        if (removed.Status == CombatEquipmentTerminalEffectStatus.Conflict
            || !sourceAuthority.TryCaptureSourceRemovalReceipt(
                expected.commitId,
                out existing)
            || !CombatEquipmentTerminalDrainCanonical.RemovalReceiptEquals(
                existing,
                expected)
            || sourceAuthority.TryCaptureLiveSource(
                source.OwnerStableId,
                out _,
                out _))
        {
            failureReason = "combat-equipment-terminal-removal-conflict:"
                + removed.FailureReason;
            return false;
        }
        return true;
    }

    private bool TryValidateRequest(
        CombatEquipmentTerminalDrainRequest request,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (request == null
            || request.Source == null
            || !Token(request.ParentOperationId)
            || !Token(request.StepOperationId)
            || !CombatEquipmentTerminalDrainCanonical.IsValidFrozenSource(
                request.Source.CaptureFrozen())
            || !CombatEquipmentTerminalDrainCanonical.IsDigest(
                request.RequestFingerprint))
        {
            failureReason = "combat-equipment-terminal-request-invalid";
            return false;
        }

        bool hasPendingInput = request.Source.PendingInputQuantity > 0
            && request.Source.PendingInputMassGrams > 0L;
        bool hasChild = Token(request.InputDestinationDrainStepOperationId)
            && CombatEquipmentTerminalDrainCanonical.IsDigest(
                request.InputDestinationDrainRequestFingerprint);
        if (hasPendingInput != hasChild
            || hasChild != (request.ChildRequest != null)
            || (!hasChild
                && (!string.IsNullOrEmpty(
                        request.InputDestinationDrainStepOperationId)
                    || !string.IsNullOrEmpty(
                        request.InputDestinationDrainRequestFingerprint))))
        {
            failureReason =
                "combat-equipment-terminal-child-identity-invalid";
            return false;
        }

        ProductionInputDestinationCustodyDrainRequest child =
            request.ChildRequest;
        if (hasChild
            && (!ProductionInputDestinationCustodyDrainContract
                    .IsValidRequest(child)
                || !string.Equals(child.ParentOperationId,
                    request.ParentOperationId, StringComparison.Ordinal)
                || !string.Equals(child.StepOperationId,
                    request.InputDestinationDrainStepOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(child.OwnerStableId,
                    request.Source.OwnerStableId, StringComparison.Ordinal)
                || !string.Equals(child.BillId,
                    request.Source.SourceId, StringComparison.Ordinal)
                || !string.Equals(child.FacilityId,
                    request.Source.FacilityId, StringComparison.Ordinal)
                || !string.Equals(child.SourceDestinationId,
                    request.Source.InputDestinationId, StringComparison.Ordinal)
                || !string.Equals(child.SourceClaimFingerprint,
                    request.Source.SourceFingerprint, StringComparison.Ordinal)
                || child.InputQuantity != request.Source.PendingInputQuantity
                || child.InputMassGrams !=
                    request.Source.PendingInputMassGrams
                || !string.Equals(child.RequestFingerprint,
                    request.InputDestinationDrainRequestFingerprint,
                    StringComparison.Ordinal)))
        {
            failureReason =
                "combat-equipment-terminal-child-request-drift";
            return false;
        }

        string expected = CombatEquipmentTerminalDrainCanonical
            .CreateRequestFingerprint(
                request.ParentOperationId,
                request.StepOperationId,
                request.Source,
                request.InputDestinationDrainStepOperationId,
                request.InputDestinationDrainRequestFingerprint);
        if (!string.Equals(expected, request.RequestFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "combat-equipment-terminal-request-fingerprint-invalid";
            return false;
        }

        if (!TryCaptureLiveSource(
                request.Source.OwnerStableId,
                out CombatEquipmentTerminalFrozenSubject live,
                out string liveFingerprint,
                out failureReason)
            || live.SourceKind != request.Source.SourceKind
            || !string.Equals(
                liveFingerprint,
                request.Source.SourceFingerprint,
                StringComparison.Ordinal))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "combat-equipment-terminal-live-source-conflict"
                : failureReason;
            return false;
        }
        return true;
    }

    private bool TryValidateRestoreJoins(
        IReadOnlyList<CombatEquipmentTerminalDrainSaveData> records,
        IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> children,
        out string failureReason)
    {
        failureReason = string.Empty;
        Dictionary<string, ProductionInputDestinationCustodyDrainSaveData>
            childByStep = children.ToDictionary(
                value => value.stepOperationId,
                value => value,
                StringComparer.Ordinal);
        HashSet<string> expectedChildSteps = new(StringComparer.Ordinal);

        foreach (CombatEquipmentTerminalDrainSaveData value in records)
        {
            bool hasChild = HasChild(value);
            if (hasChild)
                expectedChildSteps.Add(value.inputDestinationDrainStepOperationId);
            if (hasChild
                && childByStep.TryGetValue(
                    value.inputDestinationDrainStepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData child))
            {
                if (!ChildMatches(value, child)
                    || !ChildPhaseMatches(value.phase, child.phase))
                {
                    failureReason =
                        "combat-equipment-terminal-restore-child-conflict";
                    return false;
                }
            }
            else if (hasChild
                && value.phase != CombatEquipmentTerminalDrainPhase
                    .PreparedAwaitingInputDestinationReceipt)
            {
                failureReason =
                    "combat-equipment-terminal-restore-child-missing";
                return false;
            }

            CombatEquipmentTerminalFrozenSubject source =
                CombatEquipmentTerminalFrozenSubject.FromSave(value.source);
            CombatEquipmentTerminalWipLossReceiptSaveData expectedWip =
                CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(source);
            CombatEquipmentTerminalSourceRemovalReceiptSaveData expectedRemoval =
                CombatEquipmentTerminalDrainCanonical
                    .CreateSourceRemovalReceipt(source);
            CombatEquipmentTerminalWipLossReceiptSaveData actualWip = null;
            bool hasWip = expectedWip != null
                && sourceAuthority.TryCaptureWipLossReceipt(
                    expectedWip.commitId,
                    out actualWip);
            bool hasRemoval = sourceAuthority.TryCaptureSourceRemovalReceipt(
                expectedRemoval.commitId,
                out CombatEquipmentTerminalSourceRemovalReceiptSaveData
                    actualRemoval);
            bool hasLive = sourceAuthority.TryCaptureLiveSource(
                source.OwnerStableId,
                out CombatEquipmentTerminalFrozenSubject live,
                out _);
            bool terminal = value.phase >= CombatEquipmentTerminalDrainPhase
                .TerminalEffectsCommittedAwaitingOwnerAcknowledgement;
            bool effectAheadAllowed = value.phase ==
                CombatEquipmentTerminalDrainPhase
                    .InputDestinationAcknowledgedAwaitingTerminalEffects;

            if ((hasWip && !CombatEquipmentTerminalDrainCanonical
                    .WipReceiptEquals(actualWip, expectedWip))
                || (hasRemoval && !CombatEquipmentTerminalDrainCanonical
                    .RemovalReceiptEquals(actualRemoval, expectedRemoval))
                || (hasLive && !string.Equals(
                    live.SourceFingerprint,
                    source.SourceFingerprint,
                    StringComparison.Ordinal))
                || (terminal && (!hasRemoval || hasLive
                    || (expectedWip != null && !hasWip)))
                || (!terminal && !effectAheadAllowed && (hasWip || hasRemoval))
                || (!terminal && !hasRemoval && !hasLive)
                || (hasRemoval && expectedWip != null && !hasWip))
            {
                failureReason =
                    "combat-equipment-terminal-restore-effect-or-source-conflict";
                return false;
            }
        }

        foreach (ProductionInputDestinationCustodyDrainSaveData child in children)
        {
            if (IsCombatOwner(child.ownerStableId)
                && !expectedChildSteps.Contains(child.stepOperationId))
            {
                failureReason =
                    "combat-equipment-terminal-restore-child-orphan";
                return false;
            }
        }
        return true;
    }

    private static bool ChildMatches(
        CombatEquipmentTerminalDrainSaveData value,
        ProductionInputDestinationCustodyDrainSaveData child) =>
        ProductionInputDestinationCustodyDrainContract.IsValidSave(child)
        && string.Equals(child.parentOperationId, value.parentOperationId,
            StringComparison.Ordinal)
        && string.Equals(child.stepOperationId,
            value.inputDestinationDrainStepOperationId,
            StringComparison.Ordinal)
        && string.Equals(child.ownerStableId, value.source.ownerStableId,
            StringComparison.Ordinal)
        && string.Equals(child.billId, value.source.sourceId,
            StringComparison.Ordinal)
        && string.Equals(child.facilityId, value.source.facilityId,
            StringComparison.Ordinal)
        && string.Equals(child.sourceDestinationId,
            value.source.inputDestinationId, StringComparison.Ordinal)
        && string.Equals(child.sourceClaimFingerprint,
            value.source.sourceFingerprint, StringComparison.Ordinal)
        && string.Equals(child.requestFingerprint,
            value.inputDestinationDrainRequestFingerprint,
            StringComparison.Ordinal)
        && child.inputQuantity == value.source.pendingInputQuantity
        && child.inputMassGrams == value.source.pendingInputMassGrams
        && (child.phase < ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck
            ? child.releasedQuantity == 0
                && child.releasedMassGrams == 0L
            : child.releasedQuantity == value.source.pendingInputQuantity
                && child.releasedMassGrams == value.source.pendingInputMassGrams);

    private static bool ChildPhaseMatches(
        CombatEquipmentTerminalDrainPhase producer,
        ProductionInputDestinationCustodyDrainPhase child) => producer switch
    {
        CombatEquipmentTerminalDrainPhase
            .PreparedAwaitingInputDestinationReceipt =>
            child <= ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck,
        CombatEquipmentTerminalDrainPhase
            .InputDestinationReceiptRecordedAwaitingAcknowledgement =>
            child == ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck,
        _ => child == ProductionInputDestinationCustodyDrainPhase
            .BillAcknowledgedAwaitingCheckpointGc
    };

    private static bool HasChild(CombatEquipmentTerminalDrainSaveData value) =>
        !string.IsNullOrEmpty(value?.inputDestinationDrainStepOperationId);

    private static CombatEquipmentTerminalInputDispositionEvidence
        CreateInputEvidence(CombatEquipmentTerminalDrainSaveData value)
    {
        if (!HasChild(value))
        {
            return new CombatEquipmentTerminalInputDispositionEvidence(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0L);
        }

        return new CombatEquipmentTerminalInputDispositionEvidence(
            value.inputDestinationDrainStepOperationId,
            value.inputDestinationDrainRequestFingerprint,
            value.inputDestinationDrainCommitId,
            value.inputDestinationDrainReceiptFingerprint,
            value.releasedInputQuantity,
            value.releasedInputMassGrams);
    }

    private static bool IsCombatOwner(string ownerStableId) =>
        ownerStableId != null
        && (ownerStableId.StartsWith("craft-order:", StringComparison.Ordinal)
            || ownerStableId.StartsWith(
                "repair-order:",
                StringComparison.Ordinal));

    private bool TryGet(
        string stepOperationId,
        out CombatEquipmentTerminalDrainSaveData value)
    {
        value = null;
        if (!Token(stepOperationId)
            || !Current.ByStepOperationId.TryGetValue(
                stepOperationId,
                out CombatEquipmentTerminalDrainSaveData stored))
            return false;
        value = stored.Clone();
        return true;
    }

    private void Store(CombatEquipmentTerminalDrainSaveData value)
    {
        if (activeCheckpointGcCandidate != null)
        {
            throw new InvalidOperationException(
                "Combat equipment terminal checkpoint GC is active.");
        }
        if (!CombatEquipmentTerminalDrainCanonical.IsValidSave(value))
        {
            throw new InvalidOperationException(
                "Combat equipment terminal outbox refused invalid state.");
        }
        Current.ByStepOperationId[value.stepOperationId] = value.Clone();
    }

    private CheckpointGcCandidate RequireCheckpointGcCandidate(
        ICombatEquipmentTerminalDrainCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || exact.Completed
            || !ReferenceEquals(activeCheckpointGcCandidate, exact))
        {
            throw new InvalidOperationException(
                "Combat equipment terminal checkpoint GC candidate is invalid.");
        }
        return exact;
    }

    private static bool ExactCheckpointGcRow(
        CombatEquipmentTerminalDrainSaveData live,
        CombatEquipmentTerminalDrainSaveData expected) =>
        live != null
        && expected != null
        && CombatEquipmentTerminalDrainCanonical.IsValidSave(live)
        && CombatEquipmentTerminalDrainCanonical.IsValidSave(expected)
        && live.phase == expected.phase
        && string.Equals(live.parentOperationId, expected.parentOperationId,
            StringComparison.Ordinal)
        && string.Equals(live.stepOperationId, expected.stepOperationId,
            StringComparison.Ordinal)
        && string.Equals(live.requestFingerprint, expected.requestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(live.receiptFingerprint, expected.receiptFingerprint,
            StringComparison.Ordinal)
        && string.Equals(live.source?.sourceFingerprint,
            expected.source?.sourceFingerprint, StringComparison.Ordinal)
        && string.Equals(live.inputDestinationDrainReceiptFingerprint,
            expected.inputDestinationDrainReceiptFingerprint,
            StringComparison.Ordinal)
        && string.Equals(live.wipLossReceiptFingerprint,
            expected.wipLossReceiptFingerprint, StringComparison.Ordinal)
        && string.Equals(live.sourceRemovalReceiptFingerprint,
            expected.sourceRemovalReceiptFingerprint, StringComparison.Ordinal);

    private static CombatEquipmentTerminalDrainResult CheckpointGcResult(
        CheckpointGcCandidate candidate,
        CombatEquipmentTerminalDrainStatus status)
    {
        CombatEquipmentTerminalDrainSaveData row = candidate.Rows.Count == 0
            ? null
            : candidate.Rows[candidate.Rows.Count - 1];
        return new CombatEquipmentTerminalDrainResult(
            status,
            CombatEquipmentTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc,
            row?.commitId ?? string.Empty,
            row?.receiptFingerprint ?? string.Empty,
            string.Empty);
    }

    private static bool HasDuplicates(
        IEnumerable<string> source,
        bool allowEmpty)
    {
        string[] values = (source ?? Array.Empty<string>())
            .Where(value => !allowEmpty || !string.IsNullOrEmpty(value))
            .ToArray();
        return values.Any(value => !Token(value))
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length;
    }

    private static bool Token(string value) => !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static CombatEquipmentTerminalDrainResult Result(
        CombatEquipmentTerminalDrainSaveData value,
        CombatEquipmentTerminalDrainStatus status) => new(
        status,
        value.phase,
        value.commitId,
        value.receiptFingerprint,
        string.Empty);

    private static CombatEquipmentTerminalDrainResult Deferred(
        CombatEquipmentTerminalDrainSaveData value,
        string failureReason) => new(
        CombatEquipmentTerminalDrainStatus.Deferred,
        value.phase,
        value.commitId,
        value.receiptFingerprint,
        failureReason);

    private static CombatEquipmentTerminalDrainResult Conflict(
        string failureReason) => new(
        CombatEquipmentTerminalDrainStatus.Conflict,
        default,
        string.Empty,
        string.Empty,
        failureReason);
}
