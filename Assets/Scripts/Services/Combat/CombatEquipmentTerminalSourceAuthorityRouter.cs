using System;

/// <summary>
/// Routes combat terminal operations to the aggregate that owns the exact
/// craft or repair order. Concrete dependencies are intentional: resolving an
/// enumerable of the shared interface would include this router and create a
/// composition-root cycle.
/// </summary>
public sealed class CombatEquipmentTerminalSourceAuthorityRouter :
    ICombatEquipmentTerminalSourceAuthority
{
    private const string CraftOwnerPrefix = "craft-order:";
    private const string RepairOwnerPrefix = "repair-order:";

    private readonly CombatEquipmentCraftTerminalAuthority craft;
    private readonly CombatEquipmentRepairTerminalAuthority repair;

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
        if (source == null)
            return Conflict("combat-terminal-router-gc-source-null");
        return Resolve(source.SourceKind).TryGarbageCollectReceipts(
            source,
            wipReceiptFingerprint,
            removalReceiptFingerprint);
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
}
