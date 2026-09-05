using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Routes combat terminal operations to the aggregate that owns the exact
/// craft or repair order. Concrete dependencies are intentional: resolving an
/// enumerable of the shared interface would include this router and create a
/// composition-root cycle.
/// </summary>
public sealed class CombatEquipmentTerminalSourceAuthorityRouter :
    ICombatEquipmentTerminalSourceAuthority,
    ICombatEquipmentTerminalSourceCheckpointGcAuthority
{
    private sealed class CheckpointGcCandidate :
        ICombatEquipmentTerminalSourceCheckpointGcCandidate
    {
        internal ICombatEquipmentTerminalSourceCheckpointGcCandidate Craft;
        internal ICombatEquipmentTerminalSourceCheckpointGcCandidate Repair;
        internal bool Published;
        internal bool Completed;
    }

    private const string CraftOwnerPrefix = "craft-order:";
    private const string RepairOwnerPrefix = "repair-order:";

    private readonly CombatEquipmentCraftTerminalAuthority craft;
    private readonly CombatEquipmentRepairTerminalAuthority repair;
    private CheckpointGcCandidate activeCheckpointGcCandidate;

    public CombatEquipmentTerminalSourceAuthorityRouter(
        CombatEquipmentCraftTerminalAuthority craft,
        CombatEquipmentRepairTerminalAuthority repair)
    {
        this.craft = craft ?? throw new ArgumentNullException(nameof(craft));
        this.repair = repair ?? throw new ArgumentNullException(nameof(repair));
    }

    public bool TryCaptureLiveSourceForPreparation(
        string ownerStableId,
        out CombatEquipmentTerminalPreparedSource prepared,
        out string failureReason)
    {
        prepared = null;
        if (!TryResolve(ownerStableId, out var authority, out failureReason))
            return false;
        return authority.TryCaptureLiveSourceForPreparation(
            ownerStableId,
            out prepared,
            out failureReason);
    }

    public bool TryCaptureLiveSource(
        string ownerStableId,
        out CombatEquipmentTerminalFrozenSubject source,
        out string failureReason)
    {
        source = null;
        if (!TryResolve(ownerStableId, out var authority, out failureReason))
            return false;
        return authority.TryCaptureLiveSource(
            ownerStableId,
            out source,
            out failureReason);
    }

    public bool TryCaptureWipLossReceipt(
        string commitId,
        out CombatEquipmentTerminalWipLossReceiptSaveData receipt)
    {
        receipt = null;
        bool craftFound = craft.TryCaptureWipLossReceipt(
            commitId,
            out CombatEquipmentTerminalWipLossReceiptSaveData craftReceipt);
        bool repairFound = repair.TryCaptureWipLossReceipt(
            commitId,
            out CombatEquipmentTerminalWipLossReceiptSaveData repairReceipt);
        if (craftFound == repairFound)
            return false;
        receipt = craftFound ? craftReceipt : repairReceipt;
        return true;
    }

    public bool TryCaptureSourceRemovalReceipt(
        string commitId,
        out CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt)
    {
        receipt = null;
        bool craftFound = craft.TryCaptureSourceRemovalReceipt(
            commitId,
            out CombatEquipmentTerminalSourceRemovalReceiptSaveData craftReceipt);
        bool repairFound = repair.TryCaptureSourceRemovalReceipt(
            commitId,
            out CombatEquipmentTerminalSourceRemovalReceiptSaveData repairReceipt);
        if (craftFound == repairFound)
            return false;
        receipt = craftFound ? craftReceipt : repairReceipt;
        return true;
    }

    [GameplayInternalOnly(
        "Routes a canonical combat terminal WIP receipt to its owning aggregate.",
        "Combat equipment terminal drain outbox only")]
    public CombatEquipmentTerminalEffectResult TryPublishWipLossReceipt(
        CombatEquipmentTerminalWipLossReceiptSaveData receipt,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence)
    {
        if (receipt == null)
            return Conflict("combat-terminal-router-wip-receipt-null");
        return Resolve(receipt.sourceKind).TryPublishWipLossReceipt(
            receipt,
            inputEvidence);
    }

    [GameplayInternalOnly(
        "Routes exact combat source removal to its owning aggregate.",
        "Combat equipment terminal drain outbox only")]
    public CombatEquipmentTerminalEffectResult TryRemoveExactSource(
        CombatEquipmentTerminalFrozenSubject source,
        CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence)
    {
        if (source == null)
            return Conflict("combat-terminal-router-source-null");
        return Resolve(source.SourceKind).TryRemoveExactSource(
            source,
            receipt,
            inputEvidence);
    }

    [GameplayInternalOnly(
        "Routes combat terminal receipt GC to its owning aggregate.",
        "Combat equipment terminal drain outbox only")]
    public CombatEquipmentTerminalEffectResult TryGarbageCollectReceipts(
        CombatEquipmentTerminalFrozenSubject source,
        string wipReceiptFingerprint,
        string removalReceiptFingerprint)
    {
        if (activeCheckpointGcCandidate != null)
        {
            return Conflict(
                "combat-terminal-router-checkpoint-gc-active");
        }
        if (source == null)
            return Conflict("combat-terminal-router-gc-source-null");
        return Resolve(source.SourceKind).TryGarbageCollectReceipts(
            source,
            wipReceiptFingerprint,
            removalReceiptFingerprint);
    }

    bool ICombatEquipmentTerminalSourceCheckpointGcAuthority
        .TryPrepareCheckpointGarbageCollection(
            IReadOnlyList<CombatEquipmentTerminalReceiptGcRow> rows,
            out ICombatEquipmentTerminalSourceCheckpointGcCandidate candidate,
            out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (activeCheckpointGcCandidate != null)
        {
            failureReason = "combat-terminal-router-checkpoint-gc-already-active";
            return false;
        }

        CombatEquipmentTerminalReceiptGcRow[] ordered = (rows
                ?? Array.Empty<CombatEquipmentTerminalReceiptGcRow>())
            .OrderBy(value => value?.Source?.SourceKind)
            .ThenBy(value => value?.Source?.SourceId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value?.Source == null)
            || ordered.Select(value => value.Source.SourceId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason = "combat-terminal-router-checkpoint-gc-request-invalid";
            return false;
        }

        CheckpointGcCandidate exact = new();
        ICombatEquipmentTerminalSourceCheckpointGcAuthority craftGc = craft;
        ICombatEquipmentTerminalSourceCheckpointGcAuthority repairGc = repair;
        CombatEquipmentTerminalReceiptGcRow[] craftRows = ordered.Where(value =>
                value.Source.SourceKind ==
                    CombatEquipmentTerminalSourceKind.CraftOrder)
            .ToArray();
        CombatEquipmentTerminalReceiptGcRow[] repairRows = ordered.Where(value =>
                value.Source.SourceKind ==
                    CombatEquipmentTerminalSourceKind.RepairOrder)
            .ToArray();
        if (craftRows.Length + repairRows.Length != ordered.Length)
        {
            failureReason = "combat-terminal-router-checkpoint-gc-kind-unsupported";
            return false;
        }
        if (craftRows.Length > 0
            && !craftGc.TryPrepareCheckpointGarbageCollection(
                craftRows, out exact.Craft, out failureReason))
        {
            return false;
        }
        if (repairRows.Length > 0
            && !repairGc.TryPrepareCheckpointGarbageCollection(
                repairRows, out exact.Repair, out failureReason))
        {
            if (exact.Craft != null)
                craftGc.CompleteCheckpointGarbageCollection(exact.Craft);
            return false;
        }

        activeCheckpointGcCandidate = exact;
        candidate = exact;
        return true;
    }

    CombatEquipmentTerminalEffectResult
        ICombatEquipmentTerminalSourceCheckpointGcAuthority
            .PublishCheckpointGarbageCollection(
                ICombatEquipmentTerminalSourceCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.Published)
            return new CombatEquipmentTerminalEffectResult(
                CombatEquipmentTerminalEffectStatus.Replay,
                string.Empty,
                string.Empty);

        if (exact.Craft != null)
        {
            CombatEquipmentTerminalEffectResult result =
                ((ICombatEquipmentTerminalSourceCheckpointGcAuthority)craft)
                .PublishCheckpointGarbageCollection(exact.Craft);
            if (result.Status is CombatEquipmentTerminalEffectStatus.Conflict
                or CombatEquipmentTerminalEffectStatus.Deferred)
                return result;
        }
        if (exact.Repair != null)
        {
            CombatEquipmentTerminalEffectResult result =
                ((ICombatEquipmentTerminalSourceCheckpointGcAuthority)repair)
                .PublishCheckpointGarbageCollection(exact.Repair);
            if (result.Status is CombatEquipmentTerminalEffectStatus.Conflict
                or CombatEquipmentTerminalEffectStatus.Deferred)
                return result;
        }
        exact.Published = true;
        return new CombatEquipmentTerminalEffectResult(
            CombatEquipmentTerminalEffectStatus.Applied,
            string.Empty,
            string.Empty);
    }

    void ICombatEquipmentTerminalSourceCheckpointGcAuthority
        .RollbackCheckpointGarbageCollection(
            ICombatEquipmentTerminalSourceCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.Repair != null)
        {
            ((ICombatEquipmentTerminalSourceCheckpointGcAuthority)repair)
                .RollbackCheckpointGarbageCollection(exact.Repair);
        }
        if (exact.Craft != null)
        {
            ((ICombatEquipmentTerminalSourceCheckpointGcAuthority)craft)
                .RollbackCheckpointGarbageCollection(exact.Craft);
        }
        exact.Published = false;
    }

    void ICombatEquipmentTerminalSourceCheckpointGcAuthority
        .CompleteCheckpointGarbageCollection(
            ICombatEquipmentTerminalSourceCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.Repair != null)
        {
            ((ICombatEquipmentTerminalSourceCheckpointGcAuthority)repair)
                .CompleteCheckpointGarbageCollection(exact.Repair);
        }
        if (exact.Craft != null)
        {
            ((ICombatEquipmentTerminalSourceCheckpointGcAuthority)craft)
                .CompleteCheckpointGarbageCollection(exact.Craft);
        }
        exact.Completed = true;
        activeCheckpointGcCandidate = null;
    }

    private bool TryResolve(
        string ownerStableId,
        out ICombatEquipmentTerminalSourceAuthority authority,
        out string failureReason)
    {
        authority = null;
        failureReason = string.Empty;
        if (!CombatEquipmentTerminalDrainCanonical.IsToken(ownerStableId))
        {
            failureReason = "combat-terminal-router-owner-invalid";
            return false;
        }
        if (ownerStableId.StartsWith(CraftOwnerPrefix, StringComparison.Ordinal))
        {
            authority = craft;
            return true;
        }
        if (ownerStableId.StartsWith(RepairOwnerPrefix, StringComparison.Ordinal))
        {
            authority = repair;
            return true;
        }
        failureReason = "combat-terminal-router-owner-kind-unsupported";
        return false;
    }

    private ICombatEquipmentTerminalSourceAuthority Resolve(
        CombatEquipmentTerminalSourceKind sourceKind) => sourceKind switch
    {
        CombatEquipmentTerminalSourceKind.CraftOrder => craft,
        CombatEquipmentTerminalSourceKind.RepairOrder => repair,
        _ => throw new InvalidOperationException(
            "Unsupported combat terminal source kind: " + sourceKind)
    };

    private static CombatEquipmentTerminalEffectResult Conflict(string reason) =>
        new(CombatEquipmentTerminalEffectStatus.Conflict, string.Empty, reason);

    private CheckpointGcCandidate RequireCheckpointGcCandidate(
        ICombatEquipmentTerminalSourceCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || exact.Completed
            || !ReferenceEquals(activeCheckpointGcCandidate, exact))
        {
            throw new InvalidOperationException(
                "Combat terminal source checkpoint GC candidate is invalid.");
        }
        return exact;
    }
}
