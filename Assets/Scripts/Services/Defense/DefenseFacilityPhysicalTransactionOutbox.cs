using System;
using System.Collections.Generic;
using System.Linq;

public interface IDefenseFacilityPhysicalItemGateway
{
    IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();

    bool TryCommitPendingBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason);

    bool TryGetPendingBatchPhysicalDisposition(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt);

    bool AcknowledgeBatchPhysicalDisposition(
        string commitId,
        out string failureReason);
}

public sealed class DefenseFacilityPhysicalItemGateway :
    IDefenseFacilityPhysicalItemGateway
{
    private readonly IWorldItemStackRuntime items;

    public DefenseFacilityPhysicalItemGateway(IWorldItemStackRuntime items)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
        items.GetAllStacks();

    public bool TryCommitPendingBatchPhysicalDisposition(
        IReadOnlyList<PhysicalItemTransformInput> inputs,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason) =>
        items.TryCommitPendingBatchPhysicalDisposition(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

    public bool TryGetPendingBatchPhysicalDisposition(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt) =>
        items.TryGetPendingBatchPhysicalDisposition(operationId, out receipt);

    public bool AcknowledgeBatchPhysicalDisposition(
        string commitId,
        out string failureReason) =>
        items.AcknowledgeBatchPhysicalDisposition(commitId, out failureReason);
}

/// <summary>
/// Converts exact FacilityBuffer lots into either terminal maintenance use or
/// durable internal defense-facility supply custody. The physical receipt is
/// kept pending until the domain state has published its matching outcome.
/// </summary>
public static class DefenseFacilityPhysicalTransactionOutbox
{
    public const string MaintenanceOperationPrefix =
        "defense-maintenance-consume:";
    public const string SupplyOperationPrefix = "defense-supply-load:";
    public const string MaintenanceItemId = "material:iron-ingot";
    public const string MaintenanceReasonCode =
        "defense-maintenance-part-sink";
    public const string SupplyReasonCode =
        "defense-supply-to-internal-custody";

    public static string FormatOperationId(
        DefenseFacilityPhysicalCommitKind kind,
        string facilityPersistentId,
        int sequence) => kind switch
        {
            DefenseFacilityPhysicalCommitKind.MaintenanceSink =>
                $"{MaintenanceOperationPrefix}{facilityPersistentId}:{Math.Max(0, sequence):D8}",
            DefenseFacilityPhysicalCommitKind.SupplyTransfer =>
                $"{SupplyOperationPrefix}{facilityPersistentId}:{Math.Max(0, sequence):D8}",
            _ => string.Empty
        };

    public static bool TryCommitOrResume(
        DefenseFacilityPhysicalCommitSaveData pending,
        DefenseFacilityPhysicalCommitKind kind,
        string facilityPersistentId,
        int operationSequence,
        string destinationId,
        string itemId,
        int inputQuantity,
        int supplyBefore,
        int supplyUnitsGranted,
        IDefenseFacilityPhysicalItemGateway items,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (pending == null
            || items == null
            || !IsCanonical(facilityPersistentId)
            || operationSequence < 0
            || !IsCanonical(destinationId)
            || !IsCanonical(itemId)
            || inputQuantity <= 0
            || supplyBefore < 0
            || kind is not (DefenseFacilityPhysicalCommitKind.MaintenanceSink
                or DefenseFacilityPhysicalCommitKind.SupplyTransfer)
            || kind == DefenseFacilityPhysicalCommitKind.MaintenanceSink
                && supplyUnitsGranted != 0
            || kind == DefenseFacilityPhysicalCommitKind.SupplyTransfer
                && supplyUnitsGranted <= 0)
        {
            failureReason = "defense-physical-transaction-invalid-request";
            return false;
        }

        bool starting = pending.phase == DefenseFacilityPhysicalCommitPhase.None;
        List<DefenseFacilityPhysicalInputSaveData> selected;
        if (starting)
        {
            if (!IsEmpty(pending)
                || !TrySelectInputs(
                    items.GetAllStacks(),
                    destinationId,
                    itemId,
                    inputQuantity,
                    out selected))
            {
                failureReason = "defense-physical-input-unavailable";
                return false;
            }
        }
        else
        {
            if (!ValidateProvenance(
                    pending,
                    kind,
                    facilityPersistentId,
                    operationSequence,
                    destinationId,
                    itemId,
                    inputQuantity,
                    supplyBefore,
                    supplyUnitsGranted,
                    out failureReason))
            {
                return false;
            }
            selected = pending.inputs
                .Select(value => value.DeepClone())
                .ToList();
            if (!items.TryGetPendingBatchPhysicalDisposition(
                    pending.operationId,
                    out _))
            {
                failureReason = "defense-physical-receipt-missing";
                return false;
            }
        }

        PhysicalItemDispositionKind dispositionKind =
            kind == DefenseFacilityPhysicalCommitKind.MaintenanceSink
                ? PhysicalItemDispositionKind.Sink
                : PhysicalItemDispositionKind.Transfer;
        string reasonCode =
            kind == DefenseFacilityPhysicalCommitKind.MaintenanceSink
                ? MaintenanceReasonCode
                : SupplyReasonCode;
        string operationId = FormatOperationId(
            kind,
            facilityPersistentId,
            operationSequence);
        PhysicalItemTransformInput[] physicalInputs = selected
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => new PhysicalItemTransformInput(
                value.sourceStackId,
                value.quantity))
            .ToArray();
        if (!items.TryCommitPendingBatchPhysicalDisposition(
                physicalInputs,
                dispositionKind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason))
        {
            return false;
        }

        string fingerprint = CreateRequestFingerprint(
            dispositionKind,
            reasonCode,
            selected);
        if (starting)
        {
            pending.phase = DefenseFacilityPhysicalCommitPhase.IntentRecorded;
            pending.kind = kind;
            pending.operationSequence = operationSequence;
            pending.operationId = receipt.OperationId;
            pending.reasonCode = receipt.ReasonCode;
            pending.destinationId = destinationId;
            pending.itemId = itemId;
            pending.inputQuantity = inputQuantity;
            pending.inputMassGrams = receipt.InputMassGrams;
            pending.commitId = receipt.CommitId;
            pending.requestFingerprint = fingerprint;
            pending.supplyBefore = supplyBefore;
            pending.supplyUnitsGranted = supplyUnitsGranted;
            pending.supplyAfter = checked(supplyBefore + supplyUnitsGranted);
            pending.inputs = selected
                .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
                .Select(value => value.DeepClone())
                .ToList();
        }

        return ValidateReceipt(pending, receipt, fingerprint, out failureReason);
    }

    public static bool TryAcknowledgeOutcome(
        DefenseFacilityPhysicalCommitSaveData pending,
        IDefenseFacilityPhysicalItemGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (pending == null
            || items == null
            || pending.phase != DefenseFacilityPhysicalCommitPhase.OutcomePublished
            || !IsCanonical(pending.commitId))
        {
            failureReason = "defense-physical-outcome-not-published";
            return false;
        }
        return items.AcknowledgeBatchPhysicalDisposition(
            pending.commitId,
            out failureReason);
    }

    public static bool ValidateProvenance(
        DefenseFacilityPhysicalCommitSaveData pending,
        DefenseFacilityPhysicalCommitKind kind,
        string facilityPersistentId,
        int operationSequence,
        string destinationId,
        string itemId,
        int inputQuantity,
        int supplyBefore,
        int supplyUnitsGranted,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (pending == null || pending.inputs == null)
        {
            failureReason = "defense-physical-owner-invalid";
            return false;
        }
        PhysicalItemDispositionKind dispositionKind =
            kind == DefenseFacilityPhysicalCommitKind.MaintenanceSink
                ? PhysicalItemDispositionKind.Sink
                : PhysicalItemDispositionKind.Transfer;
        string reasonCode =
            kind == DefenseFacilityPhysicalCommitKind.MaintenanceSink
                ? MaintenanceReasonCode
                : SupplyReasonCode;
        DefenseFacilityPhysicalInputSaveData[] inputs = pending.inputs
            .Where(value => value != null)
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .ToArray();
        long expectedSupplyAfter = (long)supplyBefore + supplyUnitsGranted;
        bool valid = pending.phase is DefenseFacilityPhysicalCommitPhase.IntentRecorded
                or DefenseFacilityPhysicalCommitPhase.OutcomePublished
            && pending.kind == kind
            && pending.operationSequence == operationSequence
            && string.Equals(
                pending.operationId,
                FormatOperationId(kind, facilityPersistentId, operationSequence),
                StringComparison.Ordinal)
            && string.Equals(pending.reasonCode, reasonCode, StringComparison.Ordinal)
            && string.Equals(pending.destinationId, destinationId, StringComparison.Ordinal)
            && string.Equals(pending.itemId, itemId, StringComparison.Ordinal)
            && pending.inputQuantity == inputQuantity
            && pending.inputMassGrams > 0L
            && IsCanonical(pending.commitId)
            && pending.supplyBefore == supplyBefore
            && pending.supplyUnitsGranted == supplyUnitsGranted
            && expectedSupplyAfter <= int.MaxValue
            && pending.supplyAfter == expectedSupplyAfter
            && inputs.Length == pending.inputs.Count
            && inputs.Length > 0
            && inputs.All(value =>
                string.Equals(value.itemId, itemId, StringComparison.Ordinal)
                && IsCanonical(value.sourceStackId)
                && value.quantity > 0)
            && inputs.Select(value => value.sourceStackId)
                .Distinct(StringComparer.Ordinal).Count() == inputs.Length
            && inputs.Sum(value => (long)value.quantity) == inputQuantity
            && string.Equals(
                pending.requestFingerprint,
                CreateRequestFingerprint(dispositionKind, reasonCode, inputs),
                StringComparison.Ordinal);
        if (!valid)
        {
            failureReason = "defense-physical-owner-invalid";
        }
        return valid;
    }

    public static void Clear(DefenseFacilityPhysicalCommitSaveData pending)
    {
        if (pending == null)
        {
            return;
        }
        pending.phase = DefenseFacilityPhysicalCommitPhase.None;
        pending.kind = DefenseFacilityPhysicalCommitKind.None;
        pending.operationSequence = 0;
        pending.operationId = string.Empty;
        pending.reasonCode = string.Empty;
        pending.destinationId = string.Empty;
        pending.itemId = string.Empty;
        pending.inputQuantity = 0;
        pending.inputMassGrams = 0L;
        pending.commitId = string.Empty;
        pending.requestFingerprint = string.Empty;
        pending.supplyBefore = 0;
        pending.supplyAfter = 0;
        pending.supplyUnitsGranted = 0;
        pending.inputs.Clear();
    }

    public static string CreateRequestFingerprint(
        PhysicalItemDispositionKind kind,
        string reasonCode,
        IEnumerable<DefenseFacilityPhysicalInputSaveData> inputs) =>
        $"{(int)kind}:{reasonCode}:"
        + string.Join(",", (inputs
                ?? Array.Empty<DefenseFacilityPhysicalInputSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => $"{value.sourceStackId}={value.quantity}"));

    private static bool TrySelectInputs(
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        string destinationId,
        string itemId,
        int quantity,
        out List<DefenseFacilityPhysicalInputSaveData> selected)
    {
        selected = new List<DefenseFacilityPhysicalInputSaveData>();
        int remaining = quantity;
        foreach (WorldItemStackSnapshot stack in
                 (stacks ?? Array.Empty<WorldItemStackSnapshot>())
                 .Where(stack => stack != null
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)
                    && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                    && !stack.Forbidden
                    && stack.AvailableQuantity > 0
                    && stack.ReservedQuantity == 0
                    && string.IsNullOrEmpty(stack.ReservedByPersistentId))
                 .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            if (remaining <= 0)
            {
                break;
            }
            int amount = Math.Min(remaining, stack.AvailableQuantity);
            selected.Add(new DefenseFacilityPhysicalInputSaveData
            {
                itemId = itemId,
                sourceStackId = stack.StackId,
                quantity = amount
            });
            remaining -= amount;
        }
        if (remaining == 0)
        {
            return true;
        }
        selected.Clear();
        return false;
    }

    private static bool ValidateReceipt(
        DefenseFacilityPhysicalCommitSaveData pending,
        PhysicalItemBatchDispositionReceipt receipt,
        string fingerprint,
        out string failureReason)
    {
        string[] sourceIds = pending.inputs
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => value.sourceStackId)
            .ToArray();
        PhysicalItemDispositionKind expectedKind =
            pending.kind == DefenseFacilityPhysicalCommitKind.MaintenanceSink
                ? PhysicalItemDispositionKind.Sink
                : PhysicalItemDispositionKind.Transfer;
        bool valid = receipt.IsCommitted
            && receipt.Kind == expectedKind
            && string.Equals(receipt.OperationId, pending.operationId, StringComparison.Ordinal)
            && string.Equals(receipt.ReasonCode, pending.reasonCode, StringComparison.Ordinal)
            && string.Equals(receipt.CommitId, pending.commitId, StringComparison.Ordinal)
            && receipt.Quantity == pending.inputQuantity
            && receipt.InputMassGrams == pending.inputMassGrams
            && receipt.SourceStackIds.SequenceEqual(sourceIds, StringComparer.Ordinal)
            && string.Equals(
                pending.requestFingerprint,
                fingerprint,
                StringComparison.Ordinal);
        failureReason = valid
            ? string.Empty
            : "defense-physical-receipt-mismatch";
        return valid;
    }

    private static bool IsEmpty(DefenseFacilityPhysicalCommitSaveData pending) =>
        pending != null
        && pending.phase == DefenseFacilityPhysicalCommitPhase.None
        && pending.kind == DefenseFacilityPhysicalCommitKind.None
        && pending.operationSequence == 0
        && string.IsNullOrEmpty(pending.operationId)
        && string.IsNullOrEmpty(pending.reasonCode)
        && string.IsNullOrEmpty(pending.destinationId)
        && string.IsNullOrEmpty(pending.itemId)
        && pending.inputQuantity == 0
        && pending.inputMassGrams == 0L
        && string.IsNullOrEmpty(pending.commitId)
        && string.IsNullOrEmpty(pending.requestFingerprint)
        && pending.supplyBefore == 0
        && pending.supplyAfter == 0
        && pending.supplyUnitsGranted == 0
        && pending.inputs != null
        && pending.inputs.Count == 0;

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
