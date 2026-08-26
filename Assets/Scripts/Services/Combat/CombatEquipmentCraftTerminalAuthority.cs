using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Same-aggregate terminal authority for combat craft orders. The durable
/// terminal-effect row is published before any pending physical WIP receipt is
/// acknowledged, and exact order removal is committed with the removal receipt
/// in one combat aggregate replacement.
/// </summary>
public sealed class CombatEquipmentCraftTerminalAuthority :
    ICombatEquipmentTerminalSourceAuthority
{
    private readonly CombatEquipmentRuntimeStateStore stateStore;
    private readonly IProductionInputDestinationCustodyDrainService inputDrain;
    private readonly IEquipmentPhysicalItemGateway physicalItems;
    private readonly IPhysicalItemMassQuery massQuery;

    public CombatEquipmentCraftTerminalAuthority(
        CombatEquipmentRuntimeStateStore stateStore,
        IProductionInputDestinationCustodyDrainService inputDrain,
        IEquipmentPhysicalItemGateway physicalItems,
        IPhysicalItemMassQuery massQuery)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.inputDrain = inputDrain
            ?? throw new ArgumentNullException(nameof(inputDrain));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
    }

    public bool TryCaptureLiveSourceForPreparation(
        string ownerStableId,
        out CombatEquipmentTerminalPreparedSource prepared,
        out string failureReason)
    {
        prepared = null;
        if (!TryCaptureExactOrder(ownerStableId, out var before, out failureReason))
            return false;
        if (stateStore.Current.CraftTerminalEffects.ContainsKey(before.orderId))
        {
            failureReason = "combat-craft-terminal-effect-already-exists";
            return false;
        }

        ProductionInputDestinationCustodySourceSnapshot custody = null;
        if (!string.IsNullOrEmpty(before.materialDestinationId)
            && !inputDrain.TryCaptureSource(
                before.materialDestinationId,
                out custody,
                out failureReason))
        {
            failureReason = "combat-craft-terminal-custody-capture-failed:"
                + failureReason;
            return false;
        }

        if (!TryCaptureExactOrder(ownerStableId, out var after, out failureReason)
            || !string.Equals(
                JsonUtility.ToJson(before),
                JsonUtility.ToJson(after),
                StringComparison.Ordinal))
        {
            failureReason = "combat-craft-terminal-order-changed-during-capture";
            return false;
        }

        int pendingQuantity = custody?.InputQuantity ?? 0;
        long pendingMassGrams = custody?.InputMassGrams ?? 0L;
        if (!TryCreateFrozen(
                after,
                pendingQuantity,
                pendingMassGrams,
                out CombatEquipmentTerminalFrozenSubject source,
                out failureReason))
        {
            return false;
        }
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
        CombatEquipmentCraftTerminalEffectSaveData[] rows = stateStore.Current
            .CraftTerminalEffects.Values.Where(value => value != null
                && string.Equals(
                    value.ownerStableId,
                    ownerStableId,
                    StringComparison.Ordinal)).ToArray();
        if (rows.Length > 1)
        {
            failureReason = "combat-craft-terminal-source-row-duplicate";
            return false;
        }
        if (rows.Length == 1)
        {
            CombatEquipmentCraftTerminalEffectSaveData row = rows[0];
            if (row.phase == CombatEquipmentCraftTerminalEffectPhase.SourceRemoved
                || !TryCaptureOrderById(row.sourceId, out _))
            {
                failureReason = "combat-craft-terminal-source-removed";
                return false;
            }
            return TryCreateFrozenFromRow(row, out source, out failureReason);
        }

        if (!TryCaptureExactOrder(ownerStableId, out var order, out failureReason))
            return false;
        ProductionInputDestinationCustodySourceSnapshot custody = null;
        if (!string.IsNullOrEmpty(order.materialDestinationId)
            && !inputDrain.TryCaptureSource(
                order.materialDestinationId,
                out custody,
                out failureReason))
        {
            failureReason = "combat-craft-terminal-live-custody-capture-failed:"
                + failureReason;
            return false;
        }
        return TryCreateFrozen(
            order,
            custody?.InputQuantity ?? 0,
            custody?.InputMassGrams ?? 0L,
            out source,
            out failureReason);
    }

    public bool TryCaptureWipLossReceipt(
        string commitId,
        out CombatEquipmentTerminalWipLossReceiptSaveData receipt)
    {
        receipt = null;
        CombatEquipmentCraftTerminalEffectSaveData[] matches = stateStore.Current
            .CraftTerminalEffects.Values.Where(value => value != null
                && string.Equals(
                    value.wipLossCommitId,
                    commitId,
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
        CombatEquipmentCraftTerminalEffectSaveData[] matches = stateStore.Current
            .CraftTerminalEffects.Values.Where(value => value != null
                && value.phase == CombatEquipmentCraftTerminalEffectPhase
                    .SourceRemoved
                && string.Equals(
                    value.sourceRemovalCommitId,
                    commitId,
                    StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            return false;
        receipt = ProjectRemovalReceipt(matches[0]);
        return CombatEquipmentTerminalDrainCanonical
            .IsValidSourceRemovalReceipt(receipt);
    }

    [GameplayInternalOnly(
        "Publishes one combat-craft WIP terminal row before physical WIP acknowledgement.",
        "Combat equipment terminal drain outbox only")]
    public CombatEquipmentTerminalEffectResult TryPublishWipLossReceipt(
        CombatEquipmentTerminalWipLossReceiptSaveData receipt,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence)
    {
        if (!CombatEquipmentTerminalDrainCanonical.IsValidWipLossReceipt(receipt)
            || receipt.sourceKind != CombatEquipmentTerminalSourceKind.CraftOrder
            || inputEvidence == null)
        {
            return Conflict("combat-craft-terminal-wip-receipt-invalid");
        }
        if (!TryCaptureExactOrder(
                receipt.ownerStableId,
                out CombatEquipmentCraftOrderSaveData order,
                out string failureReason)
            || !TryCreateFrozen(
                order,
                inputEvidence.ReleasedQuantity,
                inputEvidence.ReleasedMassGrams,
                out CombatEquipmentTerminalFrozenSubject source,
                out failureReason)
            || !inputEvidence.IsValidFor(source)
            || !string.Equals(
                source.SourceFingerprint,
                receipt.sourceFingerprint,
                StringComparison.Ordinal)
            || !CombatEquipmentTerminalDrainCanonical.WipReceiptEquals(
                receipt,
                CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(source)))
        {
            return Conflict("combat-craft-terminal-wip-source-conflict:"
                + failureReason);
        }

        if (stateStore.Current.CraftTerminalEffects.TryGetValue(
                source.SourceId,
                out CombatEquipmentCraftTerminalEffectSaveData existing))
        {
            return RowMatches(existing, source, receipt, null, inputEvidence)
                ? Replay(receipt.receiptFingerprint)
                : Conflict("combat-craft-terminal-wip-row-conflict");
        }

        CombatEquipmentRuntimeState next = stateStore.Current.Clone();
        if (next.CraftTerminalEffects.Values.Any(value => value != null
                && (string.Equals(
                        value.sourceFingerprint,
                        source.SourceFingerprint,
                        StringComparison.Ordinal)
                    || string.Equals(
                        value.wipLossCommitId,
                        receipt.commitId,
                        StringComparison.Ordinal))))
        {
            return Conflict("combat-craft-terminal-wip-row-duplicate");
        }
        next.CraftTerminalEffects.Add(
            source.SourceId,
            CreateRow(source, receipt, inputEvidence));
        stateStore.Replace(next);
        return Applied(receipt.receiptFingerprint);
    }

    [GameplayInternalOnly(
        "Acknowledges exact craft WIP custody and removes one exact order with its terminal receipt.",
        "Combat equipment terminal drain outbox only")]
    public CombatEquipmentTerminalEffectResult TryRemoveExactSource(
        CombatEquipmentTerminalFrozenSubject source,
        CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt,
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence)
    {
        if (source == null
            || source.SourceKind != CombatEquipmentTerminalSourceKind.CraftOrder
            || !CombatEquipmentTerminalDrainCanonical
                .IsValidSourceRemovalReceipt(receipt)
            || inputEvidence == null
            || !inputEvidence.IsValidFor(source)
            || !string.Equals(
                receipt.sourceFingerprint,
                source.SourceFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("combat-craft-terminal-removal-request-invalid");
        }

        CombatEquipmentCraftTerminalEffectSaveData row;
        if (!stateStore.Current.CraftTerminalEffects.TryGetValue(
                source.SourceId,
                out row))
        {
            if (source.WipInputMassGrams > 0L)
                return Conflict("combat-craft-terminal-wip-row-missing");
            if (!TryCaptureExactOrder(
                    source.OwnerStableId,
                    out CombatEquipmentCraftOrderSaveData live,
                    out string missingFailure)
                || !string.Equals(
                    JsonUtility.ToJson(live),
                    source.SourcePayload,
                    StringComparison.Ordinal))
            {
                return Conflict("combat-craft-terminal-zero-wip-source-conflict:"
                    + missingFailure);
            }
            CombatEquipmentRuntimeState create = stateStore.Current.Clone();
            create.CraftTerminalEffects.Add(
                source.SourceId,
                CreateRow(source, null, inputEvidence));
            stateStore.Replace(create);
            row = stateStore.Current.CraftTerminalEffects[source.SourceId];
        }

        CombatEquipmentTerminalWipLossReceiptSaveData expectedWip =
            CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(source);
        if (!RowMatches(row, source, expectedWip, null, inputEvidence))
            return Conflict("combat-craft-terminal-removal-row-conflict");
        if (row.phase == CombatEquipmentCraftTerminalEffectPhase.SourceRemoved)
        {
            return CombatEquipmentTerminalDrainCanonical.RemovalReceiptEquals(
                    ProjectRemovalReceipt(row),
                    receipt)
                ? Replay(receipt.receiptFingerprint)
                : Conflict("combat-craft-terminal-removal-replay-conflict");
        }

        if (row.phase == CombatEquipmentCraftTerminalEffectPhase
                .WipPreparedAwaitingInputDispositionAcknowledgement)
        {
            if (!TryAcknowledgeFrozenWip(source, out string acknowledgeFailure))
                return Deferred("combat-craft-terminal-wip-ack-deferred:"
                    + acknowledgeFailure);
            CombatEquipmentRuntimeState acknowledged = stateStore.Current.Clone();
            acknowledged.CraftTerminalEffects[source.SourceId].phase =
                CombatEquipmentCraftTerminalEffectPhase
                    .InputDispositionAcknowledgedAwaitingDestinationClose;
            stateStore.Replace(acknowledged);
            row = stateStore.Current.CraftTerminalEffects[source.SourceId];
        }

        if (row.phase == CombatEquipmentCraftTerminalEffectPhase
                .InputDispositionAcknowledgedAwaitingDestinationClose)
        {
            CombatEquipmentRuntimeState closed = stateStore.Current.Clone();
            closed.CraftTerminalEffects[source.SourceId].phase =
                CombatEquipmentCraftTerminalEffectPhase
                    .DestinationClosedAwaitingSourceRemoval;
            stateStore.Replace(closed);
            row = stateStore.Current.CraftTerminalEffects[source.SourceId];
        }

        if (row.phase != CombatEquipmentCraftTerminalEffectPhase
                .DestinationClosedAwaitingSourceRemoval)
            return Conflict("combat-craft-terminal-removal-phase-invalid");

        CombatEquipmentRuntimeState removed = stateStore.Current.Clone();
        CombatEquipmentCraftOrderSaveData[] matchingOrders = removed.CraftOrders
            .Where(value => value != null && string.Equals(
                value.orderId,
                source.SourceId,
                StringComparison.Ordinal)).ToArray();
        if (matchingOrders.Length != 1
            || !string.Equals(
                JsonUtility.ToJson(matchingOrders[0]),
                source.SourcePayload,
                StringComparison.Ordinal))
        {
            return Conflict("combat-craft-terminal-removal-source-drift");
        }
        removed.CraftOrders.Remove(matchingOrders[0]);
        CombatEquipmentCraftTerminalEffectSaveData removedRow =
            removed.CraftTerminalEffects[source.SourceId];
        removedRow.sourceRemovalCommitId = receipt.commitId;
        removedRow.sourceRemovalReceiptFingerprint = receipt.receiptFingerprint;
        removedRow.phase = CombatEquipmentCraftTerminalEffectPhase.SourceRemoved;
        stateStore.Replace(removed);
        return Applied(receipt.receiptFingerprint);
    }

    [GameplayInternalOnly(
        "Garbage-collects one exact combat-craft terminal row after checkpoint publication.",
        "Combat equipment terminal drain outbox only")]
    public CombatEquipmentTerminalEffectResult TryGarbageCollectReceipts(
        CombatEquipmentTerminalFrozenSubject source,
        string wipReceiptFingerprint,
        string removalReceiptFingerprint)
    {
        if (source == null
            || !stateStore.Current.CraftTerminalEffects.TryGetValue(
                source.SourceId,
                out CombatEquipmentCraftTerminalEffectSaveData row)
            || row.phase != CombatEquipmentCraftTerminalEffectPhase.SourceRemoved
            || !string.Equals(
                row.sourceFingerprint,
                source.SourceFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                row.wipLossReceiptFingerprint,
                wipReceiptFingerprint ?? string.Empty,
                StringComparison.Ordinal)
            || !string.Equals(
                row.sourceRemovalReceiptFingerprint,
                removalReceiptFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("combat-craft-terminal-gc-conflict");
        }
        CombatEquipmentRuntimeState next = stateStore.Current.Clone();
        next.CraftTerminalEffects.Remove(source.SourceId);
        stateStore.Replace(next);
        return Applied(removalReceiptFingerprint);
    }

    private bool TryCaptureExactOrder(
        string ownerStableId,
        out CombatEquipmentCraftOrderSaveData order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        CombatEquipmentCraftOrderSaveData[] matches = stateStore.Current.CraftOrders
            .Where(value => value != null && string.Equals(
                ProductionFacilityDestructiveDrainOwnerStableIds
                    .CombatCraftOrder(value.orderId),
                ownerStableId,
                StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            failureReason = matches.Length == 0
                ? "combat-craft-terminal-source-missing"
                : "combat-craft-terminal-source-duplicate";
            return false;
        }
        order = matches[0].Clone();
        return true;
    }

    private bool TryCaptureOrderById(
        string sourceId,
        out CombatEquipmentCraftOrderSaveData order)
    {
        order = null;
        CombatEquipmentCraftOrderSaveData[] matches = stateStore.Current.CraftOrders
            .Where(value => value != null && string.Equals(
                value.orderId,
                sourceId,
                StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            return false;
        order = matches[0].Clone();
        return true;
    }

    private bool TryCreateFrozen(
        CombatEquipmentCraftOrderSaveData order,
        int pendingQuantity,
        long pendingMassGrams,
        out CombatEquipmentTerminalFrozenSubject source,
        out string failureReason)
    {
        source = null;
        if (!TryCalculateWipMass(
                order,
                out int wipQuantity,
                out long wipMassGrams,
                out long outputMassGrams,
                out failureReason))
        {
            return false;
        }
        long lossMassGrams;
        try
        {
            lossMassGrams = checked(wipMassGrams - outputMassGrams);
        }
        catch (OverflowException)
        {
            failureReason = "combat-craft-terminal-wip-mass-overflow";
            return false;
        }
        if (lossMassGrams < 0L)
        {
            failureReason = "combat-craft-terminal-output-exceeds-wip-mass";
            return false;
        }
        return CombatEquipmentTerminalFrozenSubject.TryCreateCraftOrder(
            order,
            new CombatEquipmentTerminalMassAccounting(
                pendingQuantity,
                pendingMassGrams,
                wipQuantity,
                wipMassGrams,
                outputMassGrams,
                lossMassGrams),
            out source,
            out failureReason);
    }

    private bool TryCalculateWipMass(
        CombatEquipmentCraftOrderSaveData order,
        out int inputQuantity,
        out long inputMassGrams,
        out long outputMassGrams,
        out string failureReason)
    {
        inputQuantity = 0;
        inputMassGrams = 0L;
        outputMassGrams = 0L;
        failureReason = string.Empty;
        bool materialWip = !string.IsNullOrEmpty(order.materialTransferOperationId);
        bool rejectedWip = !string.IsNullOrEmpty(
            order.rejectedDismantleOperationId);
        if (materialWip && rejectedWip)
        {
            failureReason = "combat-craft-terminal-multiple-wip-owners";
            return false;
        }
        try
        {
            if (materialWip)
            {
                if (order.materialTransferInputs == null
                    || order.materialTransferInputs.Any(value => value == null
                        || value.quantity <= 0
                        || !Token(value.itemId)
                        || !Token(value.sourceStackId))
                    || order.materialTransferInputs.Select(value =>
                            value.sourceStackId)
                        .Distinct(StringComparer.Ordinal).Count()
                        != order.materialTransferInputs.Count
                    || order.materialTransferMassGrams <= 0L
                    || !order.materialsReady
                    || !string.Equals(
                        order.materialTransferOperationId,
                        CombatEquipmentCraftMaterialOutbox.FormatOperationId(
                            order.orderId,
                            order.qualityAttemptIndex),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        order.materialTransferRequestFingerprint,
                        CombatEquipmentCraftMaterialOutbox
                            .CreateRequestFingerprint(
                                order.materialTransferInputs),
                        StringComparison.Ordinal))
                {
                    failureReason = "combat-craft-terminal-material-wip-invalid";
                    return false;
                }
                inputQuantity = checked(order.materialTransferInputs
                    .Sum(value => value.quantity));
                inputMassGrams = order.materialTransferMassGrams;
                if (!string.Equals(
                    order.materialTransferCommitId,
                    PhysicalDispositionCommitId(
                        order.materialTransferOperationId,
                        inputQuantity,
                        inputMassGrams),
                    StringComparison.Ordinal))
                {
                    failureReason =
                        "combat-craft-terminal-material-wip-commit-invalid";
                    return false;
                }
                return TryCalculateNormalOutputMass(
                    order,
                    out outputMassGrams,
                    out failureReason);
            }
            if (rejectedWip)
            {
                if (order.rejectedDismantleInputMassGrams <= 0L
                    || !order.dismantlingRejectedOutput
                    || !order.rejectedOutputConsumed
                    || !Token(order.rejectedStackId)
                    || !string.Equals(
                        order.rejectedDismantleOperationId,
                        CombatEquipmentRejectedDismantleOutbox
                            .FormatOperationId(
                                order.orderId,
                                order.qualityAttemptIndex),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        order.rejectedDismantleRequestFingerprint,
                        CombatEquipmentRejectedDismantleOutbox
                            .CreateRequestFingerprint(order.rejectedStackId),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        order.rejectedDismantleCommitId,
                        PhysicalDispositionCommitId(
                            order.rejectedDismantleOperationId,
                            1,
                            order.rejectedDismantleInputMassGrams),
                        StringComparison.Ordinal))
                {
                    failureReason = "combat-craft-terminal-rejected-wip-invalid";
                    return false;
                }
                inputQuantity = 1;
                inputMassGrams = order.rejectedDismantleInputMassGrams;
                return TryCalculateRecoveryOutputMass(
                    order,
                    out outputMassGrams,
                    out failureReason);
            }
            return true;
        }
        catch (Exception exception) when (exception is OverflowException
            or InvalidOperationException or ArgumentException)
        {
            failureReason = "combat-craft-terminal-mass-query-failed:"
                + exception.GetType().Name;
            return false;
        }
    }

    private bool TryCalculateNormalOutputMass(
        CombatEquipmentCraftOrderSaveData order,
        out long outputMassGrams,
        out string failureReason)
    {
        outputMassGrams = 0L;
        failureReason = string.Empty;
        if (!order.attemptOutcomeResolved
            || string.IsNullOrEmpty(order.outputOperationId)
            || string.IsNullOrEmpty(order.outputItemId)
            || order.outputQuantity <= 0)
        {
            if (order.outputPublished || !string.IsNullOrEmpty(order.outputCommitId))
            {
                failureReason = "combat-craft-terminal-output-owner-partial";
                return false;
            }
            return true;
        }

        string expectedCommit = !string.IsNullOrEmpty(order.outputInstanceId)
            ? "physical-source:" + order.outputOperationId + ":"
                + order.outputInstanceId
            : CombatEquipmentCraftOutputOutbox.FormatCommitId(
                order.outputOperationId,
                order.outputItemId,
                order.outputQuantity);
        WorldItemStackSnapshot[] outputs = FindCommittedOutputs(expectedCommit);
        if (outputs.Length == 0)
        {
            if (order.outputPublished || !string.IsNullOrEmpty(order.outputCommitId))
            {
                failureReason = "combat-craft-terminal-output-missing";
                return false;
            }
            return true;
        }
        if (outputs.Any(value => !string.Equals(
                    value.ItemId,
                    order.outputItemId,
                    StringComparison.Ordinal))
            || outputs.Sum(value => (long)value.Quantity) != order.outputQuantity
            || !string.IsNullOrEmpty(order.outputInstanceId)
                && (outputs.Length != 1
                    || !string.Equals(
                        outputs[0].ItemInstanceId,
                        order.outputInstanceId,
                        StringComparison.Ordinal))
            || !string.IsNullOrEmpty(order.outputCommitId)
                && !string.Equals(
                order.outputCommitId,
                expectedCommit,
                StringComparison.Ordinal))
        {
            failureReason = "combat-craft-terminal-output-conflict";
            return false;
        }
        return TrySumOutputMass(outputs, out outputMassGrams, out failureReason);
    }

    private bool TryCalculateRecoveryOutputMass(
        CombatEquipmentCraftOrderSaveData order,
        out long outputMassGrams,
        out string failureReason)
    {
        outputMassGrams = 0L;
        failureReason = string.Empty;
        if (order.recoveryOutputs == null || order.spawnedRecoveryAmounts == null)
        {
            failureReason = "combat-craft-terminal-recovery-owner-invalid";
            return false;
        }
        for (int index = 0; index < order.recoveryOutputs.Count; index++)
        {
            CombatCraftRecoveryOutputSaveData output = order.recoveryOutputs[index];
            if (output == null || output.amount <= 0
                || string.IsNullOrEmpty(output.itemId))
            {
                failureReason = "combat-craft-terminal-recovery-output-invalid";
                return false;
            }
            string operation = CombatEquipmentRejectedDismantleOutbox
                .FormatRecoveryOperationId(
                    order.orderId,
                    order.qualityAttemptIndex,
                    index);
            string commit = CombatEquipmentCraftOutputOutbox.FormatCommitId(
                operation,
                output.itemId,
                output.amount);
            WorldItemStackSnapshot[] stacks = FindCommittedOutputs(commit);
            int recorded = index < order.spawnedRecoveryAmounts.Count
                ? order.spawnedRecoveryAmounts[index]
                : 0;
            if (recorded != 0 && recorded != output.amount
                || recorded == output.amount && stacks.Length == 0
                || stacks.Length > 0
                    && (stacks.Any(value => !string.Equals(
                            value.ItemId,
                            output.itemId,
                            StringComparison.Ordinal))
                        || stacks.Sum(value => (long)value.Quantity)
                            != output.amount))
            {
                failureReason = "combat-craft-terminal-recovery-output-conflict";
                return false;
            }
            if (!TrySumOutputMass(
                    stacks,
                    out long outputMass,
                    out failureReason))
                return false;
            outputMassGrams = checked(outputMassGrams + outputMass);
        }
        if (order.rejectedRecoveryPublished
            && (order.recoveryOutputs.Count == 0
                || order.spawnedRecoveryAmounts.Count
                    != order.recoveryOutputs.Count
                || order.recoveryOutputs.Select((output, index) =>
                        order.spawnedRecoveryAmounts[index] == output.amount)
                    .Any(value => !value)))
        {
            failureReason = "combat-craft-terminal-recovery-publication-partial";
            return false;
        }
        return true;
    }

    private WorldItemStackSnapshot[] FindCommittedOutputs(string commitId) =>
        (physicalItems.GetAllStacks() ?? Array.Empty<WorldItemStackSnapshot>())
        .Where(value => value != null && value.Quantity > 0
            && ProductionOutputCommitComponentCodec.Matches(
                value.Components,
                commitId))
        .OrderBy(value => value.StackId, StringComparer.Ordinal)
        .ToArray();

    private bool TrySumOutputMass(
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        out long massGrams,
        out string failureReason)
    {
        massGrams = 0L;
        failureReason = string.Empty;
        try
        {
            foreach (WorldItemStackSnapshot stack in stacks
                         ?? Array.Empty<WorldItemStackSnapshot>())
            {
                PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter
                    .Create(
                        massQuery,
                        (ItemDefinitionId)stack.ItemId,
                        stack.ItemInstanceId,
                        stack.Components);
                massGrams = checked(massGrams + massQuery.GetQuantityMass(
                    (ItemDefinitionId)stack.ItemId,
                    subject,
                    stack.Quantity).Value);
            }
            return true;
        }
        catch (Exception exception) when (exception is OverflowException
            or InvalidOperationException or ArgumentException)
        {
            failureReason = "combat-craft-terminal-output-mass-invalid:"
                + exception.GetType().Name;
            return false;
        }
    }

    private bool TryAcknowledgeFrozenWip(
        CombatEquipmentTerminalFrozenSubject source,
        out string failureReason)
    {
        failureReason = string.Empty;
        CombatEquipmentCraftOrderSaveData order;
        try
        {
            order = JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                source.SourcePayload);
        }
        catch (Exception exception)
        {
            failureReason = "combat-craft-terminal-frozen-payload-invalid:"
                + exception.GetType().Name;
            return false;
        }
        string operationId;
        string commitId;
        string reasonCode;
        int quantity;
        long inputMass;
        string[] sourceStackIds;
        bool alreadyAcknowledged;
        if (!string.IsNullOrEmpty(order.materialTransferOperationId))
        {
            operationId = order.materialTransferOperationId;
            commitId = order.materialTransferCommitId;
            reasonCode = CombatEquipmentCraftMaterialOutbox.ReasonCode;
            quantity = order.materialTransferInputs.Sum(value => value.quantity);
            inputMass = order.materialTransferMassGrams;
            sourceStackIds = order.materialTransferInputs
                .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
                .Select(value => value.sourceStackId).ToArray();
            alreadyAcknowledged = order.materialTransferAcknowledged;
        }
        else if (!string.IsNullOrEmpty(order.rejectedDismantleOperationId))
        {
            operationId = order.rejectedDismantleOperationId;
            commitId = order.rejectedDismantleCommitId;
            reasonCode = CombatEquipmentRejectedDismantleOutbox.ReasonCode;
            quantity = 1;
            inputMass = order.rejectedDismantleInputMassGrams;
            sourceStackIds = new[] { order.rejectedStackId };
            alreadyAcknowledged = order.rejectedDismantleAcknowledged;
        }
        else
        {
            return source.WipInputMassGrams == 0L;
        }

        bool hasPending = physicalItems.TryGetPendingBatchPhysicalDisposition(
            operationId,
            out PhysicalItemBatchDispositionReceipt pending);
        if (alreadyAcknowledged)
        {
            if (hasPending)
                failureReason = "combat-craft-terminal-acknowledged-wip-still-pending";
            return !hasPending;
        }
        if (!hasPending)
        {
            // The same-aggregate WIP row was durably published first. Absence
            // here is the single legal crash-ahead state: acknowledgement
            // succeeded immediately before the phase update.
            return true;
        }
        if (!pending.IsCommitted
            || pending.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(pending.OperationId, operationId,
                StringComparison.Ordinal)
            || !string.Equals(pending.CommitId, commitId,
                StringComparison.Ordinal)
            || !string.Equals(pending.ReasonCode, reasonCode,
                StringComparison.Ordinal)
            || pending.Quantity != quantity
            || pending.InputMassGrams != inputMass
            || !pending.SourceStackIds.SequenceEqual(
                sourceStackIds,
                StringComparer.Ordinal))
        {
            failureReason = "combat-craft-terminal-pending-wip-conflict";
            return false;
        }
        return physicalItems.AcknowledgeBatchPhysicalDisposition(
            commitId,
            out failureReason);
    }

    private static CombatEquipmentCraftTerminalEffectSaveData CreateRow(
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
        terminalReason = (int)ProductionWipTerminalReason.FacilityDestroyed,
        lossKind = (int)ProductionWipTerminalLossKind
            .ExplicitIrrecoverableProcessLoss,
        phase = CombatEquipmentCraftTerminalEffectPhase
            .WipPreparedAwaitingInputDispositionAcknowledgement
    };

    private static bool RowMatches(
        CombatEquipmentCraftTerminalEffectSaveData row,
        CombatEquipmentTerminalFrozenSubject source,
        CombatEquipmentTerminalWipLossReceiptSaveData wip,
        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal,
        CombatEquipmentTerminalInputDispositionEvidence input)
    {
        if (row == null || source == null || input == null
            || row.schemaVersion !=
                CombatEquipmentCraftTerminalEffectSaveData.CurrentSchemaVersion
            || !Enum.IsDefined(
                typeof(CombatEquipmentCraftTerminalEffectPhase), row.phase)
            || row.terminalReason !=
                (int)ProductionWipTerminalReason.FacilityDestroyed
            || row.lossKind != (int)ProductionWipTerminalLossKind
                .ExplicitIrrecoverableProcessLoss
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
                StringComparison.Ordinal))
        {
            return false;
        }
        return removal == null || string.Equals(
                row.sourceRemovalCommitId,
                removal.commitId,
                StringComparison.Ordinal)
            && string.Equals(
                row.sourceRemovalReceiptFingerprint,
                removal.receiptFingerprint,
                StringComparison.Ordinal);
    }

    private static CombatEquipmentTerminalWipLossReceiptSaveData ProjectWipReceipt(
        CombatEquipmentCraftTerminalEffectSaveData row) => new()
    {
        commitId = row.wipLossCommitId,
        sourceKind = CombatEquipmentTerminalSourceKind.CraftOrder,
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
        ProjectRemovalReceipt(CombatEquipmentCraftTerminalEffectSaveData row) =>
        new()
        {
            commitId = row.sourceRemovalCommitId,
            sourceKind = CombatEquipmentTerminalSourceKind.CraftOrder,
            ownerStableId = row.ownerStableId,
            sourceId = row.sourceId,
            facilityId = row.facilityId,
            sourceFingerprint = row.sourceFingerprint,
            receiptFingerprint = row.sourceRemovalReceiptFingerprint
        };

    private static bool TryCreateFrozenFromRow(
        CombatEquipmentCraftTerminalEffectSaveData row,
        out CombatEquipmentTerminalFrozenSubject source,
        out string failureReason)
    {
        source = null;
        failureReason = string.Empty;
        CombatEquipmentCraftOrderSaveData order;
        try
        {
            order = JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                row.frozenSourcePayload);
        }
        catch (Exception exception)
        {
            failureReason = "combat-craft-terminal-row-payload-invalid:"
                + exception.GetType().Name;
            return false;
        }
        if (!CombatEquipmentTerminalFrozenSubject.TryCreateCraftOrder(
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
            || !string.Equals(
                source.SourceFingerprint,
                row.sourceFingerprint,
                StringComparison.Ordinal))
        {
            failureReason = "combat-craft-terminal-row-source-drift:"
                + failureReason;
            source = null;
            return false;
        }
        return true;
    }

    private static CombatEquipmentTerminalEffectResult Applied(string fingerprint) =>
        new(CombatEquipmentTerminalEffectStatus.Applied, fingerprint, string.Empty);

    private static CombatEquipmentTerminalEffectResult Replay(string fingerprint) =>
        new(CombatEquipmentTerminalEffectStatus.Replay, fingerprint, string.Empty);

    private static CombatEquipmentTerminalEffectResult Deferred(string reason) =>
        new(CombatEquipmentTerminalEffectStatus.Deferred, string.Empty, reason);

    private static CombatEquipmentTerminalEffectResult Conflict(string reason) =>
        new(CombatEquipmentTerminalEffectStatus.Conflict, string.Empty, reason);

    private static string PhysicalDispositionCommitId(
        string operationId,
        int quantity,
        long inputMassGrams) =>
        $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Transfer}:"
        + $"{operationId}:{quantity}:{inputMassGrams}";

    private static bool Token(string value) => !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
