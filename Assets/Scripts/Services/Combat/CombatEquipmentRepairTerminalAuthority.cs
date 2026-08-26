using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// Maintenance-aggregate terminal authority for repair orders. The repair
/// material WIP receipt is persisted before its physical acknowledgement, and
/// the exact order removal is committed with the removal receipt in one
/// maintenance aggregate replacement.
/// </summary>
public sealed class CombatEquipmentRepairTerminalAuthority :
    ICombatEquipmentTerminalSourceAuthority
{
    private readonly EquipmentMaintenancePolicyRuntime runtime;
    private readonly IProductionInputDestinationCustodyDrainService inputDrain;

    public CombatEquipmentRepairTerminalAuthority(
        EquipmentMaintenancePolicyRuntime runtime,
        IProductionInputDestinationCustodyDrainService inputDrain)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.inputDrain = inputDrain
            ?? throw new ArgumentNullException(nameof(inputDrain));
    }

    public bool TryCaptureLiveSourceForPreparation(
        string ownerStableId,
        out CombatEquipmentTerminalPreparedSource prepared,
        out string failureReason)
    {
        prepared = null;
        if (!TryCaptureExactOrder(ownerStableId, out CombatEquipmentRepairOrder before,
                out failureReason))
            return false;
        EquipmentMaintenanceAggregateState state = runtime.CaptureTerminalState();
        if (state.TerminalEffects.ContainsKey(before.orderId))
        {
            failureReason = "combat-repair-terminal-effect-already-exists";
            return false;
        }

        if (!inputDrain.TryCaptureSource(
                before.FacilityDestinationId,
                out ProductionInputDestinationCustodySourceSnapshot custody,
                out failureReason))
        {
            failureReason = "combat-repair-terminal-custody-capture-failed:"
                + failureReason;
            return false;
        }
        if (!TryCaptureExactOrder(ownerStableId, out CombatEquipmentRepairOrder after,
                out failureReason)
            || !SameOrder(before, after))
        {
            failureReason =
                "combat-repair-terminal-order-changed-during-capture";
            return false;
        }
        if (!TryCreateFrozen(
                after,
                custody.InputQuantity,
                custody.InputMassGrams,
                out CombatEquipmentTerminalFrozenSubject source,
                out failureReason))
            return false;
        return CombatEquipmentTerminalPreparedSource.TryCreate(
            source,
            custody,
            out prepared,
            out failureReason);
    }

    public bool TryCaptureLiveSource(
        string ownerStableId,
        out CombatEquipmentTerminalFrozenSubject source,
        out string failureReason)
    {
        source = null;
        failureReason = string.Empty;
        EquipmentMaintenanceAggregateState state = runtime.CaptureTerminalState();
        CombatEquipmentRepairTerminalEffectSaveData[] rows = state
            .TerminalEffects.Values.Where(value => value != null
                && string.Equals(value.ownerStableId, ownerStableId,
                    StringComparison.Ordinal)).ToArray();
        if (rows.Length > 1)
        {
            failureReason = "combat-repair-terminal-source-row-duplicate";
            return false;
        }
        if (rows.Length == 1)
        {
            CombatEquipmentRepairTerminalEffectSaveData row = rows[0];
            if (row.phase == CombatEquipmentRepairTerminalEffectPhase.SourceRemoved
                || !state.Orders.TryGetValue(row.sourceId,
                    out CombatEquipmentRepairOrder live)
                || !string.Equals(JsonUtility.ToJson(live),
                    row.frozenSourcePayload, StringComparison.Ordinal))
            {
                failureReason = "combat-repair-terminal-source-removed-or-drifted";
                return false;
            }
            return TryCreateFrozenFromRow(row, out source, out failureReason);
        }

        if (!TryCaptureExactOrder(ownerStableId, out CombatEquipmentRepairOrder order,
                out failureReason)
            || !inputDrain.TryCaptureSource(
                order.FacilityDestinationId,
                out ProductionInputDestinationCustodySourceSnapshot custody,
                out failureReason))
        {
            failureReason = "combat-repair-terminal-live-capture-failed:"
                + failureReason;
            return false;
        }
        return TryCreateFrozen(
            order,
            custody.InputQuantity,
            custody.InputMassGrams,
            out source,
            out failureReason);
    }

    public bool TryCaptureWipLossReceipt(
        string commitId,
        out CombatEquipmentTerminalWipLossReceiptSaveData receipt)
    {
        receipt = null;
        CombatEquipmentRepairTerminalEffectSaveData[] matches = runtime
            .CaptureTerminalState().TerminalEffects.Values
            .Where(value => value != null
                && string.Equals(value.wipLossCommitId, commitId,
                    StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1 || matches[0].wipInputMassGrams <= 0L)
            return false;
        receipt = ProjectWipReceipt(matches[0]);
        return CombatEquipmentTerminalDrainCanonical.IsValidWipLossReceipt(
            receipt);
    }

    public bool TryCaptureSourceRemovalReceipt(
        string commitId,
        out CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt)
    {
        receipt = null;
        CombatEquipmentRepairTerminalEffectSaveData[] matches = runtime
            .CaptureTerminalState().TerminalEffects.Values
            .Where(value => value != null
                && value.phase == CombatEquipmentRepairTerminalEffectPhase
                    .SourceRemoved
                && string.Equals(value.sourceRemovalCommitId, commitId,
                    StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            return false;
        receipt = ProjectRemovalReceipt(matches[0]);
        return CombatEquipmentTerminalDrainCanonical
            .IsValidSourceRemovalReceipt(receipt);
    }

    [GameplayInternalOnly(
        "Publishes one repair-material WIP terminal row before physical acknowledgement.",
        "Combat equipment terminal drain outbox only")]
    public CombatEquipmentTerminalEffectResult TryPublishWipLossReceipt(
        CombatEquipmentTerminalWipLossReceiptSaveData receipt,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence)
    {
        string failureReason = string.Empty;
        if (!CombatEquipmentTerminalDrainCanonical.IsValidWipLossReceipt(receipt)
            || receipt.sourceKind != CombatEquipmentTerminalSourceKind.RepairOrder
            || inputEvidence == null
            || !TryCaptureExactOrder(receipt.ownerStableId,
                out CombatEquipmentRepairOrder order, out failureReason)
            || !TryCreateFrozen(order, inputEvidence.ReleasedQuantity,
                inputEvidence.ReleasedMassGrams,
                out CombatEquipmentTerminalFrozenSubject source,
                out failureReason)
            || !inputEvidence.IsValidFor(source)
            || !string.Equals(source.SourceFingerprint,
                receipt.sourceFingerprint, StringComparison.Ordinal)
            || !CombatEquipmentTerminalDrainCanonical.WipReceiptEquals(
                receipt,
                CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(source)))
        {
            return Conflict("combat-repair-terminal-wip-source-conflict:"
                + failureReason);
        }

        EquipmentMaintenanceAggregateState current = runtime.CaptureTerminalState();
        if (current.TerminalEffects.TryGetValue(source.SourceId,
                out CombatEquipmentRepairTerminalEffectSaveData existing))
        {
            return RowMatches(existing, source, receipt, null, inputEvidence)
                ? Replay(receipt.receiptFingerprint)
                : Conflict("combat-repair-terminal-wip-row-conflict");
        }
        if (current.TerminalEffects.Values.Any(value => value != null
                && (string.Equals(value.sourceFingerprint,
                        source.SourceFingerprint, StringComparison.Ordinal)
                    || string.Equals(value.wipLossCommitId, receipt.commitId,
                        StringComparison.Ordinal))))
        {
            return Conflict("combat-repair-terminal-wip-row-duplicate");
        }
        current.TerminalEffects.Add(
            source.SourceId,
            CreateRow(source, receipt, inputEvidence));
        return runtime.TryPublishTerminalState(current, out failureReason)
            ? Applied(receipt.receiptFingerprint)
            : Deferred("combat-repair-terminal-wip-publication-deferred:"
                + failureReason);
    }

    [GameplayInternalOnly(
        "Acknowledges repair-material WIP, closes its destination and removes one exact repair order.",
        "Combat equipment terminal drain outbox only")]
    public CombatEquipmentTerminalEffectResult TryRemoveExactSource(
        CombatEquipmentTerminalFrozenSubject source,
        CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence)
    {
        if (source == null
            || source.SourceKind != CombatEquipmentTerminalSourceKind.RepairOrder
            || !CombatEquipmentTerminalDrainCanonical
                .IsValidSourceRemovalReceipt(receipt)
            || inputEvidence == null
            || !inputEvidence.IsValidFor(source)
            || !string.Equals(receipt.sourceFingerprint,
                source.SourceFingerprint, StringComparison.Ordinal))
        {
            return Conflict("combat-repair-terminal-removal-request-invalid");
        }

        EquipmentMaintenanceAggregateState state = runtime.CaptureTerminalState();
        if (!state.TerminalEffects.TryGetValue(source.SourceId,
                out CombatEquipmentRepairTerminalEffectSaveData row))
        {
            string missingFailure = string.Empty;
            if (source.WipInputMassGrams > 0L
                || !TryCaptureExactOrder(source.OwnerStableId,
                    out CombatEquipmentRepairOrder live,
                    out missingFailure)
                || !string.Equals(JsonUtility.ToJson(live),
                    source.SourcePayload, StringComparison.Ordinal))
            {
                return Conflict("combat-repair-terminal-zero-wip-source-conflict:"
                    + missingFailure);
            }
            state.TerminalEffects.Add(
                source.SourceId,
                CreateRow(source, null, inputEvidence));
            if (!runtime.TryPublishTerminalState(state,
                    out string createFailure))
            {
                return Deferred(
                    "combat-repair-terminal-zero-wip-row-deferred:"
                    + createFailure);
            }
            state = runtime.CaptureTerminalState();
            row = state.TerminalEffects[source.SourceId];
        }

        CombatEquipmentTerminalWipLossReceiptSaveData expectedWip =
            CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(source);
        if (!RowMatches(row, source, expectedWip, null, inputEvidence))
            return Conflict("combat-repair-terminal-removal-row-conflict");
        if (row.phase == CombatEquipmentRepairTerminalEffectPhase.SourceRemoved)
        {
            return CombatEquipmentTerminalDrainCanonical.RemovalReceiptEquals(
                    ProjectRemovalReceipt(row), receipt)
                ? Replay(receipt.receiptFingerprint)
                : Conflict("combat-repair-terminal-removal-replay-conflict");
        }

        if (row.phase == CombatEquipmentRepairTerminalEffectPhase
                .WipPreparedAwaitingOwnerDispositionAcknowledgement)
        {
            if (!TryParseFrozenOrder(source, out CombatEquipmentRepairOrder frozen,
                    out string parseFailure))
                return Deferred("combat-repair-terminal-wip-parse-deferred:"
                    + parseFailure);
            if (!runtime.TryAcknowledgeTerminalMaterial(
                    frozen,
                    out string acknowledgeFailure))
                return Deferred("combat-repair-terminal-wip-ack-deferred:"
                    + acknowledgeFailure);
            state = runtime.CaptureTerminalState();
            state.TerminalEffects[source.SourceId].phase =
                CombatEquipmentRepairTerminalEffectPhase
                    .OwnerDispositionAcknowledgedAwaitingDestinationClose;
            if (!runtime.TryPublishTerminalState(state,
                    out string publishFailure))
            {
                return Deferred("combat-repair-terminal-wip-phase-deferred:"
                    + publishFailure);
            }
            state = runtime.CaptureTerminalState();
            row = state.TerminalEffects[source.SourceId];
        }

        if (row.phase == CombatEquipmentRepairTerminalEffectPhase
                .OwnerDispositionAcknowledgedAwaitingDestinationClose)
        {
            state.TerminalEffects[source.SourceId].phase =
                CombatEquipmentRepairTerminalEffectPhase
                    .DestinationClosedAwaitingSourceRemoval;
            if (!runtime.TryPublishTerminalState(state,
                    out string closeFailure))
            {
                return Deferred("combat-repair-terminal-buffer-close-deferred:"
                    + closeFailure);
            }
            state = runtime.CaptureTerminalState();
            row = state.TerminalEffects[source.SourceId];
        }

        if (row.phase != CombatEquipmentRepairTerminalEffectPhase
                .DestinationClosedAwaitingSourceRemoval)
            return Conflict("combat-repair-terminal-removal-phase-invalid");
        if (!state.Orders.TryGetValue(source.SourceId,
                out CombatEquipmentRepairOrder exact)
            || !string.Equals(JsonUtility.ToJson(exact),
                source.SourcePayload, StringComparison.Ordinal))
        {
            return Conflict("combat-repair-terminal-removal-source-drift");
        }
        state.Orders.Remove(source.SourceId);
        row = state.TerminalEffects[source.SourceId];
        row.sourceRemovalCommitId = receipt.commitId;
        row.sourceRemovalReceiptFingerprint = receipt.receiptFingerprint;
        row.phase = CombatEquipmentRepairTerminalEffectPhase.SourceRemoved;
        return runtime.TryPublishTerminalState(state, out string removalFailure)
            ? Applied(receipt.receiptFingerprint)
            : Deferred("combat-repair-terminal-removal-deferred:"
                + removalFailure);
    }

    [GameplayInternalOnly(
        "Garbage-collects one exact repair terminal receipt row after checkpoint publication.",
        "Combat equipment terminal drain outbox only")]
    public CombatEquipmentTerminalEffectResult TryGarbageCollectReceipts(
        CombatEquipmentTerminalFrozenSubject source,
        string wipReceiptFingerprint,
        string removalReceiptFingerprint)
    {
        EquipmentMaintenanceAggregateState state = runtime.CaptureTerminalState();
        if (source == null
            || source.SourceKind != CombatEquipmentTerminalSourceKind.RepairOrder
            || !state.TerminalEffects.TryGetValue(source.SourceId,
                out CombatEquipmentRepairTerminalEffectSaveData row)
            || row.phase != CombatEquipmentRepairTerminalEffectPhase.SourceRemoved
            || state.Orders.ContainsKey(source.SourceId)
            || !string.Equals(row.sourceFingerprint, source.SourceFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(row.wipLossReceiptFingerprint,
                wipReceiptFingerprint ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(row.sourceRemovalReceiptFingerprint,
                removalReceiptFingerprint, StringComparison.Ordinal))
        {
            return Conflict("combat-repair-terminal-gc-conflict");
        }
        state.TerminalEffects.Remove(source.SourceId);
        return runtime.TryPublishTerminalState(state, out string failureReason)
            ? Applied(removalReceiptFingerprint)
            : Deferred("combat-repair-terminal-gc-deferred:" + failureReason);
    }

    private bool TryCaptureExactOrder(
        string ownerStableId,
        out CombatEquipmentRepairOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        CombatEquipmentRepairOrder[] matches = runtime.CaptureTerminalState()
            .Orders.Values.Where(value => value != null
                && string.Equals(
                    ProductionFacilityDestructiveDrainOwnerStableIds
                        .EquipmentRepairOrder(value.orderId),
                    ownerStableId,
                    StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            failureReason = matches.Length == 0
                ? "combat-repair-terminal-order-missing"
                : "combat-repair-terminal-order-duplicate";
            return false;
        }
        order = matches[0].Clone();
        return true;
    }

    private static bool TryCreateFrozen(
        CombatEquipmentRepairOrder order,
        int pendingQuantity,
        long pendingMassGrams,
        out CombatEquipmentTerminalFrozenSubject source,
        out string failureReason)
    {
        source = null;
        if (!EquipmentRepairMaterialOutbox.ValidateProvenance(
                order,
                out failureReason))
            return false;
        int wipQuantity = order.materialsConsumed
            ? order.requiredMaterialAmount
            : 0;
        long wipMass = order.materialsConsumed
            ? order.materialTransferMassGrams
            : 0L;
        if (pendingQuantity < 0 || pendingMassGrams < 0L
            || (pendingQuantity == 0) != (pendingMassGrams == 0L)
            || (wipQuantity == 0) != (wipMass == 0L))
        {
            failureReason = "combat-repair-terminal-mass-invalid";
            return false;
        }
        return CombatEquipmentTerminalFrozenSubject.TryCreateRepairOrder(
            order,
            new CombatEquipmentTerminalMassAccounting(
                pendingQuantity,
                pendingMassGrams,
                wipQuantity,
                wipMass,
                0L,
                wipMass),
            out source,
            out failureReason);
    }

    private static CombatEquipmentRepairTerminalEffectSaveData CreateRow(
        CombatEquipmentTerminalFrozenSubject source,
        CombatEquipmentTerminalWipLossReceiptSaveData wip,
        CombatEquipmentTerminalInputDispositionEvidence input) => new()
    {
        ownerStableId = source.OwnerStableId,
        sourceId = source.SourceId,
        facilityId = source.FacilityId,
        frozenSourcePayload = source.SourcePayload,
        sourceFingerprint = source.SourceFingerprint,
        inputDispositionStepOperationId = input.StepOperationId,
        inputDispositionRequestFingerprint = input.RequestFingerprint,
        inputDispositionCommitId = input.CommitId,
        inputDispositionReceiptFingerprint = input.ReceiptFingerprint,
        releasedInputQuantity = input.ReleasedQuantity,
        releasedInputMassGrams = input.ReleasedMassGrams,
        wipLossCommitId = wip?.commitId ?? string.Empty,
        wipLossReceiptFingerprint = wip?.receiptFingerprint ?? string.Empty,
        wipInputQuantity = source.WipInputQuantity,
        wipInputMassGrams = source.WipInputMassGrams,
        committedOutputMassGrams = source.CommittedOutputMassGrams,
        declaredLossMassGrams = source.DeclaredLossMassGrams,
        terminalReason = wip == null ? 0 : (int)wip.reason,
        lossKind = wip == null ? 0 : (int)wip.lossKind,
        phase = CombatEquipmentRepairTerminalEffectPhase
            .WipPreparedAwaitingOwnerDispositionAcknowledgement
    };

    private static bool RowMatches(
        CombatEquipmentRepairTerminalEffectSaveData row,
        CombatEquipmentTerminalFrozenSubject source,
        CombatEquipmentTerminalWipLossReceiptSaveData wip,
        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal,
        CombatEquipmentTerminalInputDispositionEvidence input)
    {
        if (row == null || source == null || input == null
            || row.schemaVersion !=
                CombatEquipmentRepairTerminalEffectSaveData.CurrentSchemaVersion
            || !Enum.IsDefined(
                typeof(CombatEquipmentRepairTerminalEffectPhase), row.phase)
            || !string.Equals(row.ownerStableId, source.OwnerStableId,
                StringComparison.Ordinal)
            || !string.Equals(row.sourceId, source.SourceId,
                StringComparison.Ordinal)
            || !string.Equals(row.facilityId, source.FacilityId,
                StringComparison.Ordinal)
            || !string.Equals(row.frozenSourcePayload, source.SourcePayload,
                StringComparison.Ordinal)
            || !string.Equals(row.sourceFingerprint, source.SourceFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(row.inputDispositionStepOperationId,
                input.StepOperationId, StringComparison.Ordinal)
            || !string.Equals(row.inputDispositionRequestFingerprint,
                input.RequestFingerprint, StringComparison.Ordinal)
            || !string.Equals(row.inputDispositionCommitId,
                input.CommitId, StringComparison.Ordinal)
            || !string.Equals(row.inputDispositionReceiptFingerprint,
                input.ReceiptFingerprint, StringComparison.Ordinal)
            || row.releasedInputQuantity != input.ReleasedQuantity
            || row.releasedInputMassGrams != input.ReleasedMassGrams
            || row.wipInputQuantity != source.WipInputQuantity
            || row.wipInputMassGrams != source.WipInputMassGrams
            || row.committedOutputMassGrams != source.CommittedOutputMassGrams
            || row.declaredLossMassGrams != source.DeclaredLossMassGrams
            || !string.Equals(row.wipLossCommitId,
                wip?.commitId ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(row.wipLossReceiptFingerprint,
                wip?.receiptFingerprint ?? string.Empty,
                StringComparison.Ordinal)
            || row.terminalReason != (wip == null ? 0 : (int)wip.reason)
            || row.lossKind != (wip == null ? 0 : (int)wip.lossKind))
        {
            return false;
        }
        return removal == null || string.Equals(row.sourceRemovalCommitId,
                removal.commitId, StringComparison.Ordinal)
            && string.Equals(row.sourceRemovalReceiptFingerprint,
                removal.receiptFingerprint, StringComparison.Ordinal);
    }

    private static CombatEquipmentTerminalWipLossReceiptSaveData
        ProjectWipReceipt(CombatEquipmentRepairTerminalEffectSaveData row) => new()
        {
            commitId = row.wipLossCommitId,
            sourceKind = CombatEquipmentTerminalSourceKind.RepairOrder,
            ownerStableId = row.ownerStableId,
            sourceId = row.sourceId,
            facilityId = row.facilityId,
            sourceFingerprint = row.sourceFingerprint,
            inputQuantity = row.wipInputQuantity,
            inputMassGrams = row.wipInputMassGrams,
            committedOutputMassGrams = row.committedOutputMassGrams,
            declaredLossMassGrams = row.declaredLossMassGrams,
            reason = (ProductionWipTerminalReason)row.terminalReason,
            lossKind = (ProductionWipTerminalLossKind)row.lossKind,
            receiptFingerprint = row.wipLossReceiptFingerprint
        };

    private static CombatEquipmentTerminalSourceRemovalReceiptSaveData
        ProjectRemovalReceipt(CombatEquipmentRepairTerminalEffectSaveData row) =>
        new()
        {
            commitId = row.sourceRemovalCommitId,
            sourceKind = CombatEquipmentTerminalSourceKind.RepairOrder,
            ownerStableId = row.ownerStableId,
            sourceId = row.sourceId,
            facilityId = row.facilityId,
            sourceFingerprint = row.sourceFingerprint,
            receiptFingerprint = row.sourceRemovalReceiptFingerprint
        };

    private static bool TryCreateFrozenFromRow(
        CombatEquipmentRepairTerminalEffectSaveData row,
        out CombatEquipmentTerminalFrozenSubject source,
        out string failureReason)
    {
        source = null;
        if (!TryParseFrozenOrder(row?.frozenSourcePayload,
                out CombatEquipmentRepairOrder order,
                out failureReason)
            || !CombatEquipmentTerminalFrozenSubject.TryCreateRepairOrder(
                order,
                new CombatEquipmentTerminalMassAccounting(
                    row.releasedInputQuantity,
                    row.releasedInputMassGrams,
                    row.wipInputQuantity,
                    row.wipInputMassGrams,
                    row.committedOutputMassGrams,
                    row.declaredLossMassGrams),
                out source,
                out failureReason)
            || !string.Equals(source.SourceFingerprint,
                row.sourceFingerprint, StringComparison.Ordinal))
        {
            source = null;
            failureReason = "combat-repair-terminal-row-source-drift:"
                + failureReason;
            return false;
        }
        return true;
    }

    private static bool TryParseFrozenOrder(
        CombatEquipmentTerminalFrozenSubject source,
        out CombatEquipmentRepairOrder order,
        out string failureReason) => TryParseFrozenOrder(
            source?.SourcePayload,
            out order,
            out failureReason);

    private static bool TryParseFrozenOrder(
        string payload,
        out CombatEquipmentRepairOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        try
        {
            order = JsonUtility.FromJson<CombatEquipmentRepairOrder>(payload);
        }
        catch (Exception exception)
        {
            failureReason = "combat-repair-terminal-frozen-payload-invalid:"
                + exception.GetType().Name;
            return false;
        }
        if (order == null
            || !string.Equals(JsonUtility.ToJson(order), payload,
                StringComparison.Ordinal))
        {
            failureReason = "combat-repair-terminal-frozen-payload-drift";
            order = null;
            return false;
        }
        return true;
    }

    private static bool SameOrder(
        CombatEquipmentRepairOrder left,
        CombatEquipmentRepairOrder right) => left != null && right != null
        && string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private static CombatEquipmentTerminalEffectResult Applied(
        string fingerprint) => new(
        CombatEquipmentTerminalEffectStatus.Applied,
        fingerprint,
        string.Empty);

    private static CombatEquipmentTerminalEffectResult Replay(
        string fingerprint) => new(
        CombatEquipmentTerminalEffectStatus.Replay,
        fingerprint,
        string.Empty);

    private static CombatEquipmentTerminalEffectResult Deferred(string reason) =>
        new(CombatEquipmentTerminalEffectStatus.Deferred, string.Empty, reason);

    private static CombatEquipmentTerminalEffectResult Conflict(string reason) =>
        new(CombatEquipmentTerminalEffectStatus.Conflict, string.Empty, reason);
}
