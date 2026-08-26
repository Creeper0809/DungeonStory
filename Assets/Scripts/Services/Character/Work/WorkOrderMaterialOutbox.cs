using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class WorkOrderMaterialOutbox
{
    internal const string OperationPrefix = "work-order-materials:";
    internal const string TransferReasonCode =
        "work-order-materials-to-construction-wip";
    internal const string RestitutionOperationPrefix =
        "work-order-material-restitution:";
    internal const string RestitutionReasonCode =
        "work-order-cancelled-material-restitution";

    internal static bool TryCommitOrResume(
        WorkOrderRecord order,
        IWorldItemStackRuntime items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || items == null)
        {
            failureReason = "work-order-material-authority-missing";
            return false;
        }
        if (order.requiredItemMaterials.Count == 0)
        {
            return order.materialTransfer.Phase ==
                WorkOrderMaterialTransferPhase.None;
        }

        WorkOrderMaterialTransferState owner = order.materialTransfer;
        if (owner.Phase == WorkOrderMaterialTransferPhase.Acknowledged)
        {
            return HasExactDeliveredOutcome(order);
        }
        if (owner.Phase == WorkOrderMaterialTransferPhase.RestitutionPending)
        {
            failureReason = "work-order-material-restitution-pending";
            return false;
        }

        if (owner.Phase == WorkOrderMaterialTransferPhase.None)
        {
            if (!TrySelectExactInputs(
                    order,
                    items.GetAllStacks(),
                    out PhysicalItemTransformInput[] inputs,
                    out WorkOrderMaterialSourceState[] sources,
                    out failureReason))
            {
                return false;
            }

            string operationId = OperationPrefix + order.workOrderId;
            if (!items.TryCommitPendingBatchPhysicalDisposition(
                    inputs,
                    PhysicalItemDispositionKind.Transfer,
                    operationId,
                    TransferReasonCode,
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out failureReason)
                || !receipt.IsCommitted)
            {
                return false;
            }

            owner.Phase = WorkOrderMaterialTransferPhase.InputCommitted;
            owner.OperationId = receipt.OperationId;
            owner.ReasonCode = receipt.ReasonCode;
            owner.RequestFingerprint = receipt.RequestFingerprint;
            owner.CommitId = receipt.CommitId;
            owner.InputQuantity = receipt.Quantity;
            owner.InputMassGrams = receipt.InputMassGrams;
            owner.Sources.Clear();
            owner.Sources.AddRange(sources);
        }

        if (!items.TryGetPendingBatchPhysicalDisposition(
                owner.OperationId,
                out PhysicalItemBatchDispositionReceipt pending)
            || !Matches(owner, pending, order))
        {
            failureReason = "work-order-material-pending-receipt-mismatch:"
                + owner.OperationId;
            return false;
        }

        if (owner.Phase == WorkOrderMaterialTransferPhase.InputCommitted)
        {
            foreach (KeyValuePair<string, int> required in
                     order.requiredItemMaterials)
            {
                order.deliveredItemMaterials[required.Key] = required.Value;
            }
            owner.Phase = WorkOrderMaterialTransferPhase.CustodyPublished;
        }

        if (owner.Phase != WorkOrderMaterialTransferPhase.CustodyPublished
            || !HasExactDeliveredOutcome(order))
        {
            failureReason = "work-order-material-custody-outcome-invalid";
            return false;
        }
        if (!items.AcknowledgeBatchPhysicalDisposition(
                owner.CommitId,
                out failureReason))
        {
            return false;
        }

        owner.Phase = WorkOrderMaterialTransferPhase.Acknowledged;
        return true;
    }

    internal static bool TryPublishRestitution(
        WorkOrderRecord order,
        IPhysicalItemSourcePublicationService sources,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || sources == null)
        {
            failureReason = "work-order-material-restitution-authority-missing";
            return false;
        }
        WorkOrderMaterialTransferState owner = order.materialTransfer;
        if (owner.Phase == WorkOrderMaterialTransferPhase.Acknowledged)
        {
            owner.Phase = WorkOrderMaterialTransferPhase.RestitutionPending;
            owner.RestitutionOperationId =
                RestitutionOperationPrefix + order.workOrderId;
        }
        if (owner.Phase != WorkOrderMaterialTransferPhase.RestitutionPending
            || !HasExactDeliveredOutcome(order))
        {
            failureReason = "work-order-material-restitution-owner-invalid";
            return false;
        }

        if (!sources.TryEnsureLooseOutputs(
                order.requiredItemMaterials,
                order.position,
                owner.RestitutionOperationId,
                RestitutionReasonCode,
                out PhysicalItemSourcePublicationReceipt receipt,
                out failureReason)
            || !receipt.IsCommitted
            || receipt.OutputQuantity != owner.InputQuantity
            || receipt.OutputMassGrams != owner.InputMassGrams)
        {
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                failureReason =
                    "work-order-material-restitution-mass-mismatch";
            }
            return false;
        }
        return true;
    }

    internal static bool HasAcknowledgedCustody(WorkOrderRecord order) =>
        order != null
        && (order.requiredItemMaterials.Count == 0
            || (order.materialTransfer.Phase ==
                    WorkOrderMaterialTransferPhase.Acknowledged
                && HasExactDeliveredOutcome(order)));

    internal static WorkOrderMaterialTransferSaveData ToSaveData(
        WorkOrderMaterialTransferState state) => new()
    {
        phase = state?.Phase ?? WorkOrderMaterialTransferPhase.None,
        operationId = state?.OperationId ?? string.Empty,
        reasonCode = state?.ReasonCode ?? string.Empty,
        requestFingerprint = state?.RequestFingerprint ?? string.Empty,
        commitId = state?.CommitId ?? string.Empty,
        inputQuantity = state?.InputQuantity ?? 0,
        inputMassGrams = state?.InputMassGrams ?? 0L,
        sources = (state?.Sources ?? new List<WorkOrderMaterialSourceState>())
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => new WorkOrderMaterialSourceSaveData
            {
                itemId = value.ItemId,
                stackId = value.StackId,
                quantity = value.Quantity
            })
            .ToList(),
        restitutionOperationId = state?.RestitutionOperationId ?? string.Empty
    };

    internal static WorkOrderMaterialTransferState FromSaveData(
        WorkOrderMaterialTransferSaveData saved)
    {
        WorkOrderMaterialTransferState state = new()
        {
            Phase = saved?.phase ?? WorkOrderMaterialTransferPhase.None,
            OperationId = saved?.operationId ?? string.Empty,
            ReasonCode = saved?.reasonCode ?? string.Empty,
            RequestFingerprint = saved?.requestFingerprint ?? string.Empty,
            CommitId = saved?.commitId ?? string.Empty,
            InputQuantity = saved?.inputQuantity ?? 0,
            InputMassGrams = saved?.inputMassGrams ?? 0L,
            RestitutionOperationId = saved?.restitutionOperationId
                ?? string.Empty
        };
        state.Sources.AddRange((saved?.sources
                ?? new List<WorkOrderMaterialSourceSaveData>())
            .Select(value => new WorkOrderMaterialSourceState
            {
                ItemId = value.itemId ?? string.Empty,
                StackId = value.stackId ?? string.Empty,
                Quantity = value.quantity
            }));
        return state;
    }

    internal static bool Matches(
        WorkOrderMaterialTransferState owner,
        PhysicalItemBatchDispositionReceipt receipt,
        WorkOrderRecord order) =>
        owner != null
        && receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(
            owner.OperationId,
            receipt.OperationId,
            StringComparison.Ordinal)
        && string.Equals(
            owner.ReasonCode,
            receipt.ReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            owner.RequestFingerprint,
            receipt.RequestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            owner.CommitId,
            receipt.CommitId,
            StringComparison.Ordinal)
        && owner.InputQuantity == receipt.Quantity
        && owner.InputMassGrams == receipt.InputMassGrams
        && receipt.SourceStackIds.SequenceEqual(
            owner.Sources.Select(value => value.StackId)
                .OrderBy(value => value, StringComparer.Ordinal),
            StringComparer.Ordinal)
        && HasExactSourceRequirements(owner, order);

    private static bool TrySelectExactInputs(
        WorkOrderRecord order,
        IReadOnlyList<WorldItemStackSnapshot> world,
        out PhysicalItemTransformInput[] inputs,
        out WorkOrderMaterialSourceState[] sources,
        out string failureReason)
    {
        List<PhysicalItemTransformInput> selectedInputs = new();
        List<WorkOrderMaterialSourceState> selectedSources = new();
        foreach (KeyValuePair<string, int> required in
                 order.requiredItemMaterials.OrderBy(
                     value => value.Key,
                     StringComparer.Ordinal))
        {
            int remaining = required.Value;
            foreach (WorldItemStackSnapshot stack in (world
                         ?? Array.Empty<WorldItemStackSnapshot>())
                     .Where(value => value != null
                         && !value.Forbidden
                         && value.State == WorldItemStackState.FacilityBuffer
                         && value.ReservedQuantity == 0
                         && string.Equals(
                             value.DestinationId,
                             order.materialDestinationId,
                             StringComparison.Ordinal)
                         && string.Equals(
                             value.ItemId,
                             required.Key,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
            {
                int quantity = Mathf.Min(remaining, stack.Quantity);
                if (quantity <= 0)
                {
                    continue;
                }
                selectedInputs.Add(new PhysicalItemTransformInput(
                    stack.StackId,
                    quantity));
                selectedSources.Add(new WorkOrderMaterialSourceState
                {
                    ItemId = required.Key,
                    StackId = stack.StackId,
                    Quantity = quantity
                });
                remaining -= quantity;
                if (remaining == 0)
                {
                    break;
                }
            }
            if (remaining > 0)
            {
                inputs = Array.Empty<PhysicalItemTransformInput>();
                sources = Array.Empty<WorkOrderMaterialSourceState>();
                failureReason = "work-order-material-missing:" + required.Key;
                return false;
            }
        }

        inputs = selectedInputs
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        sources = selectedSources
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        failureReason = string.Empty;
        return inputs.Length > 0;
    }

    private static bool HasExactDeliveredOutcome(WorkOrderRecord order) =>
        order != null
        && order.requiredItemMaterials.Count ==
            order.deliveredItemMaterials.Count
        && order.requiredItemMaterials.All(required =>
            order.deliveredItemMaterials.TryGetValue(
                required.Key,
                out int delivered)
            && delivered == required.Value);

    private static bool HasExactSourceRequirements(
        WorkOrderMaterialTransferState owner,
        WorkOrderRecord order) =>
        owner.Sources.Count > 0
        && owner.Sources.All(value => value != null
            && value.Quantity > 0
            && !string.IsNullOrWhiteSpace(value.ItemId)
            && !string.IsNullOrWhiteSpace(value.StackId))
        && owner.Sources.GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.Quantity),
                StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .SequenceEqual(
                order.requiredItemMaterials.OrderBy(
                    value => value.Key,
                    StringComparer.Ordinal));
}

#if UNITY_EDITOR
public static class WorkOrderMaterialDebugContract
{
    public static string RestitutionOperationId(string orderId) =>
        WorkOrderMaterialOutbox.RestitutionOperationPrefix + orderId;

    public static PhysicalItemBatchDispositionReceipt CreateReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        string requestFingerprint,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams) => new(
            kind,
            operationId,
            reasonCode,
            requestFingerprint,
            sourceStackIds,
            quantity,
            inputMassGrams);
}
#endif
