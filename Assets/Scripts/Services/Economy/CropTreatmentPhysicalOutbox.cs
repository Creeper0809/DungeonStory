using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns the item-layer half of one completed crop treatment. Work and order
/// progress remain in the crop aggregate; this outbox keeps the exact Sink,
/// package tare publication, ecology outcome and acknowledgement replay-safe.
/// </summary>
public static class CropTreatmentPhysicalOutbox
{
    public const string OperationPrefix = "crop-treatment:";
    public const string ReasonCode = "crop-treatment-applied";
    public const string DestroyedPlotLossReasonCode =
        "crop-treatment-destroyed-with-plot";

    public static string FormatOperationId(string plotId, int sequence) =>
        $"{OperationPrefix}{plotId}:{Math.Max(0, sequence):D8}";

    public static bool TryCommitOrResume(
        CropTreatmentOrderSaveData owner,
        IPhysicalFacilityItemSinkGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (owner == null || items == null || !ValidateIntent(owner))
        {
            failureReason = "crop-treatment-intent-invalid";
            return false;
        }

        bool starting = owner.phase == CropTreatmentOrderPhase.Working;
        if (starting && owner.completedWork + 0.001f < owner.requiredWork)
        {
            failureReason = "crop-treatment-work-incomplete";
            return false;
        }
        if (!starting
            && owner.phase is not CropTreatmentOrderPhase.InputCommitted
                and not CropTreatmentOrderPhase.OutcomePublished
                and not CropTreatmentOrderPhase.PlotDestroyedLossPending)
        {
            failureReason = "crop-treatment-physical-phase-invalid";
            return false;
        }

        PhysicalItemBatchDispositionReceipt receipt;
        if (starting)
        {
            if (!items.TryCommitSinkPending(
                    owner.destinationId,
                    owner.itemId,
                    owner.quantity,
                    owner.operationId,
                    owner.reasonCode,
                    out receipt,
                    out failureReason))
                return false;

            owner.phase = CropTreatmentOrderPhase.InputCommitted;
            owner.sourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            owner.inputMassGrams = receipt.InputMassGrams;
            owner.commitId = receipt.CommitId;
            owner.requestFingerprint = receipt.RequestFingerprint;
        }
        else if (!items.TryGetPending(owner.operationId, out receipt))
        {
            failureReason = "crop-treatment-physical-receipt-missing";
            return false;
        }

        return ValidateReceipt(owner, receipt, out failureReason);
    }

    public static bool EnsureTareOutputs(
        CropTreatmentOrderSaveData owner,
        Vector2Int position,
        IPackagedLotTareDispositionService tare,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (owner == null
            || tare == null
            || owner.phase != CropTreatmentOrderPhase.InputCommitted
            || !IsCanonical(owner.commitId))
        {
            failureReason = "crop-treatment-tare-owner-invalid";
            return false;
        }
        if (!tare.EnsureTerminalSinkOutputs(
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [owner.itemId] = owner.quantity
                },
                position,
                owner.commitId,
                out PackagedLotTareOutputReceipt receipt,
                out failureReason))
            return false;

        string[] commits = (receipt.OutputCommitIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        bool first = owner.tareOutputCommitIds.Count == 0
            && owner.tareOutputQuantity == 0
            && owner.tareOutputMassGrams == 0L
            && owner.destroyedTareMassGrams == 0L;
        if (first)
        {
            owner.tareOutputQuantity = receipt.OutputQuantity;
            owner.tareOutputMassGrams = receipt.OutputMassGrams;
            owner.destroyedTareMassGrams = receipt.DestroyedTareMassGrams;
            owner.tareOutputCommitIds = commits.ToList();
            return true;
        }

        bool matches = owner.tareOutputQuantity == receipt.OutputQuantity
            && owner.tareOutputMassGrams == receipt.OutputMassGrams
            && owner.destroyedTareMassGrams == receipt.DestroyedTareMassGrams
            && owner.tareOutputCommitIds.SequenceEqual(
                commits,
                StringComparer.Ordinal);
        if (!matches)
            failureReason = "crop-treatment-tare-replay-conflict";
        return matches;
    }

    public static bool TryAcknowledgeOutcome(
        CropTreatmentOrderSaveData owner,
        IPhysicalFacilityItemSinkGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (owner == null
            || owner.phase != CropTreatmentOrderPhase.OutcomePublished
            || items == null
            || !IsCanonical(owner.commitId))
        {
            failureReason = "crop-treatment-outcome-not-published";
            return false;
        }
        return items.Acknowledge(owner.commitId, out failureReason);
    }

    public static bool TryAcknowledgeDestroyedPlotLoss(
        CropTreatmentOrderSaveData owner,
        IPhysicalFacilityItemSinkGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (owner == null || items == null)
        {
            failureReason = "crop-treatment-destroyed-loss-invalid";
            return false;
        }
        if (owner.phase == CropTreatmentOrderPhase.InputCommitted)
        {
            if (!items.TryGetPending(
                    owner.operationId,
                    out PhysicalItemBatchDispositionReceipt receipt)
                || !ValidateReceipt(owner, receipt, out failureReason)
                || !string.IsNullOrEmpty(owner.ecologyAfterFingerprint))
            {
                if (failureReason.Length == 0)
                    failureReason = "crop-treatment-destroyed-owner-invalid";
                return false;
            }
            owner.phase = CropTreatmentOrderPhase.PlotDestroyedLossPending;
            owner.terminalDisposition =
                CropTreatmentTerminalDisposition.DestroyedWithPlotLoss;
            owner.terminalReasonCode = DestroyedPlotLossReasonCode;
            owner.terminalLossQuantity = owner.quantity;
            owner.terminalLossMassGrams = owner.inputMassGrams;
        }
        if (!ValidateDestroyedPlotLoss(owner, out failureReason))
            return false;
        return items.Acknowledge(owner.commitId, out failureReason);
    }

    public static bool ValidateDestroyedPlotLoss(
        CropTreatmentOrderSaveData owner,
        out string failureReason)
    {
        bool valid = owner != null
            && owner.phase == CropTreatmentOrderPhase.PlotDestroyedLossPending
            && owner.terminalDisposition
                == CropTreatmentTerminalDisposition.DestroyedWithPlotLoss
            && string.Equals(
                owner.terminalReasonCode,
                DestroyedPlotLossReasonCode,
                StringComparison.Ordinal)
            && owner.terminalLossQuantity == owner.quantity
            && owner.terminalLossQuantity > 0
            && owner.terminalLossMassGrams == owner.inputMassGrams
            && owner.terminalLossMassGrams > 0L
            && string.IsNullOrEmpty(owner.ecologyAfterFingerprint);
        failureReason = valid
            ? string.Empty
            : "crop-treatment-destroyed-owner-invalid";
        return valid;
    }

    public static bool ValidateReceipt(
        CropTreatmentOrderSaveData owner,
        PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        string[] sources = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        bool valid = owner != null
            && receipt.Kind == PhysicalItemDispositionKind.Sink
            && string.Equals(receipt.OperationId, owner.operationId, StringComparison.Ordinal)
            && string.Equals(receipt.ReasonCode, owner.reasonCode, StringComparison.Ordinal)
            && string.Equals(receipt.CommitId, owner.commitId, StringComparison.Ordinal)
            && string.Equals(
                receipt.RequestFingerprint,
                owner.requestFingerprint,
                StringComparison.Ordinal)
            && receipt.Quantity == owner.quantity
            && receipt.InputMassGrams == owner.inputMassGrams
            && owner.sourceStackIds.SequenceEqual(sources, StringComparer.Ordinal)
            && owner.inputMassGrams > 0L;
        failureReason = valid ? string.Empty : "crop-treatment-receipt-mismatch";
        return valid;
    }

    public static bool ValidateIntent(CropTreatmentOrderSaveData owner) =>
        owner != null
        && owner.phase != CropTreatmentOrderPhase.None
        && owner.operationSequence >= 0
        && string.Equals(
            owner.operationId,
            FormatOperationId(
                ExtractPlotId(owner.operationId),
                owner.operationSequence),
            StringComparison.Ordinal)
        && string.Equals(owner.reasonCode, ReasonCode, StringComparison.Ordinal)
        && IsCanonical(owner.destinationId)
        && IsCanonical(owner.itemId)
        && Enum.IsDefined(typeof(CropTreatmentKind), owner.treatmentKind)
        && owner.quantity > 0
        && IsFinitePositive(owner.requiredWork)
        && owner.completedWork >= 0f
        && owner.completedWork <= owner.requiredWork + 0.001f
        && IsFinitePositive(owner.effectAmount)
        && owner.cooldownDays >= 0
        && owner.scheduledAbsoluteDay >= 0;

    public static void Clear(CropTreatmentOrderSaveData owner)
    {
        if (owner == null) return;
        owner.phase = CropTreatmentOrderPhase.None;
        owner.operationSequence = 0;
        owner.operationId = string.Empty;
        owner.reasonCode = string.Empty;
        owner.destinationId = string.Empty;
        owner.itemId = string.Empty;
        owner.treatmentKind = default;
        owner.quantity = 0;
        owner.requiredWork = 0f;
        owner.completedWork = 0f;
        owner.effectAmount = 0f;
        owner.cooldownDays = 0;
        owner.scheduledAbsoluteDay = 0;
        owner.failureReason = string.Empty;
        owner.sourceStackIds.Clear();
        owner.inputMassGrams = 0L;
        owner.commitId = string.Empty;
        owner.requestFingerprint = string.Empty;
        owner.tareOutputQuantity = 0;
        owner.tareOutputMassGrams = 0L;
        owner.destroyedTareMassGrams = 0L;
        owner.tareOutputCommitIds.Clear();
        owner.ecologyBeforeFingerprint = string.Empty;
        owner.ecologyAfterFingerprint = string.Empty;
        owner.terminalDisposition = CropTreatmentTerminalDisposition.None;
        owner.terminalReasonCode = string.Empty;
        owner.terminalLossQuantity = 0;
        owner.terminalLossMassGrams = 0L;
    }

    private static string ExtractPlotId(string operationId)
    {
        string value = operationId ?? string.Empty;
        if (!value.StartsWith(OperationPrefix, StringComparison.Ordinal))
            return string.Empty;
        int separator = value.LastIndexOf(':');
        return separator <= OperationPrefix.Length
            ? string.Empty
            : value.Substring(OperationPrefix.Length, separator - OperationPrefix.Length);
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFinitePositive(float value) =>
        value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
