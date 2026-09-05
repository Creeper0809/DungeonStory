using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

/// <summary>
/// Converts exact crop FacilityBuffer lots to durable WIP custody. The item
/// receipt remains pending until the owning crop aggregate publishes its
/// ecology or certified-output outcome.
/// </summary>
public static class CropPhysicalTransactionOutbox
{
    public const string SowOperationPrefix = "crop-sow-input:";
    public const string CertifiedOperationPrefix = "certified-seed-input:";
    public const string SowReasonCode = "crop-sow-input-to-wip";
    public const string CertifiedReasonCode = "certified-seed-input-to-wip";
    public const string DestroyedPlotLossOperationPrefix =
        "crop-sow-wip-loss:";
    public const string DestroyedPlotLossReasonCode =
        "crop-sow-wip-destroyed-with-plot";
    public const string DestroyedFacilityLossOperationPrefix =
        "certified-seed-wip-loss:";
    public const string DestroyedFacilityLossReasonCode =
        "certified-seed-wip-destroyed-with-facility";

    public static string FormatSowOperationId(string plotId, int sequence) =>
        $"{SowOperationPrefix}{plotId}:{Math.Max(0, sequence):D8}";

    public static string FormatCertifiedOperationId(string orderId) =>
        CertifiedOperationPrefix + (orderId ?? string.Empty);

    public static string FormatDestroyedPlotLossOperationId(
        string inputOperationId) =>
        DestroyedPlotLossOperationPrefix + (inputOperationId ?? string.Empty);

    public static string FormatDestroyedFacilityLossOperationId(
        string inputOperationId) =>
        DestroyedFacilityLossOperationPrefix
        + (inputOperationId ?? string.Empty);

    public static bool TryCommitOrResume(
        CropPhysicalCommitSaveData owner,
        string operationId,
        string reasonCode,
        int operationSequence,
        string destinationId,
        IReadOnlyDictionary<string, int> requirements,
        string seedItemId,
        string cropId,
        IPhysicalSeedLotGateway items,
        out SeedLotState seedLot,
        out string failureReason)
    {
        seedLot = null;
        failureReason = string.Empty;
        if (owner == null
            || items == null
            || !IsCanonical(operationId)
            || !IsCanonical(reasonCode)
            || operationSequence < 0
            || !IsCanonical(destinationId)
            || !IsCanonical(seedItemId)
            || !IsCanonical(cropId))
        {
            failureReason = "crop-physical-transaction-invalid-request";
            return false;
        }
        KeyValuePair<string, int>[] canonical = (requirements
                ?? new Dictionary<string, int>())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        if (canonical.Length == 0
            || canonical.Any(pair => !IsCanonical(pair.Key) || pair.Value <= 0)
            || canonical.Select(pair => pair.Key)
                .Distinct(StringComparer.Ordinal).Count() != canonical.Length
            || !canonical.Any(pair => string.Equals(
                pair.Key,
                seedItemId,
                StringComparison.Ordinal) && pair.Value == 1))
        {
            failureReason = "crop-physical-requirements-invalid";
            return false;
        }

        bool starting = owner.phase == CropPhysicalCommitPhase.None;
        List<CropPhysicalInputSaveData> selected;
        if (starting)
        {
            if (!IsEmpty(owner)
                || !TrySelectInputs(
                    items.GetAllStacks(),
                    destinationId,
                    canonical,
                    seedItemId,
                    cropId,
                    out selected,
                    out seedLot,
                    out failureReason))
            {
                return false;
            }
        }
        else
        {
            if (!ValidateProvenance(
                    owner,
                    operationId,
                    reasonCode,
                    operationSequence,
                    destinationId,
                    canonical.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.Ordinal),
                    seedItemId,
                    cropId,
                    out failureReason)
                || !items.TryGetPendingBatchPhysicalDisposition(
                    owner.operationId,
                    out _))
            {
                if (failureReason.Length == 0)
                    failureReason = "crop-physical-receipt-missing";
                return false;
            }
            selected = owner.inputs.Select(value => value.DeepClone()).ToList();
            seedLot = owner.seedLot?.Clone();
        }

        PhysicalItemTransformInput[] physicalInputs = selected
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => new PhysicalItemTransformInput(
                value.sourceStackId,
                value.quantity))
            .ToArray();
        if (!items.TryCommitPendingBatchPhysicalDisposition(
                physicalInputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                reasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }
        string fingerprint = CreateRequestFingerprint(reasonCode, selected);
        if (starting)
        {
            owner.phase = CropPhysicalCommitPhase.InputCommitted;
            owner.operationSequence = operationSequence;
            owner.operationId = receipt.OperationId;
            owner.reasonCode = receipt.ReasonCode;
            owner.destinationId = destinationId;
            owner.cropId = cropId;
            owner.seedItemId = seedItemId;
            owner.inputQuantity = receipt.Quantity;
            owner.inputMassGrams = receipt.InputMassGrams;
            owner.commitId = receipt.CommitId;
            owner.requestFingerprint = fingerprint;
            owner.hasSeedLot = true;
            owner.seedLot = seedLot.Clone();
            owner.inputs = selected
                .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
                .Select(value => value.DeepClone())
                .ToList();
        }
        return ValidateReceipt(owner, receipt, fingerprint, out failureReason);
    }

    public static bool TryAcknowledgeOutcome(
        CropPhysicalCommitSaveData owner,
        IPhysicalSeedLotGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (owner == null
            || items == null
            || owner.phase != CropPhysicalCommitPhase.OutcomePublished
            || !IsCanonical(owner.commitId))
        {
            failureReason = "crop-physical-outcome-not-published";
            return false;
        }
        return items.AcknowledgeBatchPhysicalDisposition(
            owner.commitId,
            out failureReason);
    }

    public static bool TryAcknowledgeDestroyedPlotLoss(
        CropPhysicalCommitSaveData owner,
        IPhysicalSeedLotGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (owner == null || items == null)
        {
            failureReason = "crop-destroyed-wip-loss-invalid-request";
            return false;
        }

        if (owner.phase == CropPhysicalCommitPhase.InputCommitted)
        {
            if (!items.TryGetPendingBatchPhysicalDisposition(
                    owner.operationId,
                    out PhysicalItemBatchDispositionReceipt receipt)
                || !ValidateReceipt(
                    owner,
                    receipt,
                    owner.requestFingerprint,
                    out failureReason)
                || string.IsNullOrWhiteSpace(owner.ecologyBeforeFingerprint)
                || !string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                || !HasNoTerminalDisposition(owner))
            {
                if (failureReason.Length == 0)
                    failureReason = "crop-destroyed-wip-loss-owner-invalid";
                return false;
            }

            owner.phase = CropPhysicalCommitPhase.PlotDestroyedLossPending;
            owner.terminalDisposition =
                CropWipTerminalDisposition.DestroyedWithPlotLoss;
            owner.terminalOperationId =
                FormatDestroyedPlotLossOperationId(owner.operationId);
            owner.terminalReasonCode = DestroyedPlotLossReasonCode;
            owner.terminalLossQuantity = owner.inputQuantity;
            owner.terminalLossMassGrams = owner.inputMassGrams;
        }

        if (!ValidateDestroyedPlotLoss(owner, out failureReason))
            return false;

        return items.AcknowledgeBatchPhysicalDisposition(
            owner.commitId,
            out failureReason);
    }

    public static bool TryAcknowledgeDestroyedFacilityLoss(
        CropPhysicalCommitSaveData owner,
        IPhysicalSeedLotGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (owner == null || items == null)
        {
            failureReason = "certified-seed-destroyed-facility-loss-invalid-request";
            return false;
        }

        if (owner.phase == CropPhysicalCommitPhase.InputCommitted)
        {
            if (!items.TryGetPendingBatchPhysicalDisposition(
                    owner.operationId,
                    out PhysicalItemBatchDispositionReceipt receipt)
                || !ValidateReceipt(
                    owner,
                    receipt,
                    owner.requestFingerprint,
                    out failureReason)
                || !HasNoTerminalDisposition(owner))
            {
                if (failureReason.Length == 0)
                {
                    failureReason =
                        "certified-seed-destroyed-facility-loss-owner-invalid";
                }
                return false;
            }

            owner.phase = CropPhysicalCommitPhase.FacilityDestroyedLossPending;
            owner.terminalDisposition =
                CropWipTerminalDisposition.DestroyedWithFacilityLoss;
            owner.terminalOperationId =
                FormatDestroyedFacilityLossOperationId(owner.operationId);
            owner.terminalReasonCode = DestroyedFacilityLossReasonCode;
            owner.terminalLossQuantity = owner.inputQuantity;
            owner.terminalLossMassGrams = owner.inputMassGrams;
        }

        if (!ValidateDestroyedFacilityLoss(owner, out failureReason))
            return false;

        return items.AcknowledgeBatchPhysicalDisposition(
            owner.commitId,
            out failureReason);
    }

    public static bool ValidateDestroyedFacilityLoss(
        CropPhysicalCommitSaveData owner,
        out string failureReason)
    {
        bool valid = owner != null
            && owner.phase
                == CropPhysicalCommitPhase.FacilityDestroyedLossPending
            && owner.terminalDisposition
                == CropWipTerminalDisposition.DestroyedWithFacilityLoss
            && string.Equals(
                owner.terminalOperationId,
                FormatDestroyedFacilityLossOperationId(owner.operationId),
                StringComparison.Ordinal)
            && string.Equals(
                owner.terminalReasonCode,
                DestroyedFacilityLossReasonCode,
                StringComparison.Ordinal)
            && owner.terminalLossQuantity == owner.inputQuantity
            && owner.terminalLossQuantity > 0
            && owner.terminalLossMassGrams == owner.inputMassGrams
            && owner.terminalLossMassGrams > 0L
            && string.IsNullOrEmpty(owner.ecologyAfterFingerprint);
        failureReason = valid
            ? string.Empty
            : "certified-seed-destroyed-facility-loss-owner-invalid";
        return valid;
    }

    public static bool ValidateDestroyedPlotLoss(
        CropPhysicalCommitSaveData owner,
        out string failureReason)
    {
        bool valid = owner != null
            && owner.phase == CropPhysicalCommitPhase.PlotDestroyedLossPending
            && owner.terminalDisposition
                == CropWipTerminalDisposition.DestroyedWithPlotLoss
            && string.Equals(
                owner.terminalOperationId,
                FormatDestroyedPlotLossOperationId(owner.operationId),
                StringComparison.Ordinal)
            && string.Equals(
                owner.terminalReasonCode,
                DestroyedPlotLossReasonCode,
                StringComparison.Ordinal)
            && owner.terminalLossQuantity == owner.inputQuantity
            && owner.terminalLossQuantity > 0
            && owner.terminalLossMassGrams == owner.inputMassGrams
            && owner.terminalLossMassGrams > 0L
            && string.IsNullOrEmpty(owner.ecologyAfterFingerprint);
        failureReason = valid
            ? string.Empty
            : "crop-destroyed-wip-loss-owner-invalid";
        return valid;
    }

    public static bool ValidateProvenance(
        CropPhysicalCommitSaveData owner,
        string operationId,
        string reasonCode,
        int operationSequence,
        string destinationId,
        IReadOnlyDictionary<string, int> requirements,
        string seedItemId,
        string cropId,
        out string failureReason)
    {
        failureReason = string.Empty;
        KeyValuePair<string, int>[] canonical = (requirements
                ?? new Dictionary<string, int>())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        CropPhysicalInputSaveData[] inputs = (owner?.inputs
                ?? new List<CropPhysicalInputSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .ToArray();
        int expectedQuantity = canonical.Sum(pair => pair.Value);
        bool valid = owner != null
            && owner.phase is CropPhysicalCommitPhase.InputCommitted
                or CropPhysicalCommitPhase.OutcomePublished
            && owner.operationSequence == operationSequence
            && string.Equals(owner.operationId, operationId, StringComparison.Ordinal)
            && string.Equals(owner.reasonCode, reasonCode, StringComparison.Ordinal)
            && string.Equals(owner.destinationId, destinationId, StringComparison.Ordinal)
            && string.Equals(owner.cropId, cropId, StringComparison.Ordinal)
            && string.Equals(owner.seedItemId, seedItemId, StringComparison.Ordinal)
            && owner.inputQuantity == expectedQuantity
            && owner.inputMassGrams > 0L
            && IsCanonical(owner.commitId)
            && owner.hasSeedLot
            && owner.seedLot != null
            && string.Equals(owner.seedLot.cropId, cropId, StringComparison.Ordinal)
            && inputs.Length == owner.inputs.Count
            && inputs.Length > 0
            && inputs.Select(value => value.sourceStackId)
                .Distinct(StringComparer.Ordinal).Count() == inputs.Length
            && inputs.All(value => IsCanonical(value.itemId)
                && IsCanonical(value.sourceStackId)
                && value.quantity > 0)
            && canonical.All(requirement => inputs
                .Where(value => string.Equals(
                    value.itemId,
                    requirement.Key,
                    StringComparison.Ordinal))
                .Sum(value => value.quantity) == requirement.Value)
            && string.Equals(
                owner.requestFingerprint,
                CreateRequestFingerprint(reasonCode, inputs),
                StringComparison.Ordinal);
        if (!valid)
            failureReason = "crop-physical-owner-invalid";
        return valid;
    }

    public static void Clear(CropPhysicalCommitSaveData owner)
    {
        if (owner == null) return;
        owner.phase = CropPhysicalCommitPhase.None;
        owner.operationSequence = 0;
        owner.operationId = string.Empty;
        owner.reasonCode = string.Empty;
        owner.destinationId = string.Empty;
        owner.cropId = string.Empty;
        owner.seedItemId = string.Empty;
        owner.inputQuantity = 0;
        owner.inputMassGrams = 0L;
        owner.commitId = string.Empty;
        owner.requestFingerprint = string.Empty;
        owner.hasSeedLot = false;
        owner.seedLot = null;
        owner.ecologyBeforeFingerprint = string.Empty;
        owner.ecologyAfterFingerprint = string.Empty;
        owner.terminalDisposition = CropWipTerminalDisposition.None;
        owner.terminalOperationId = string.Empty;
        owner.terminalReasonCode = string.Empty;
        owner.terminalLossQuantity = 0;
        owner.terminalLossMassGrams = 0L;
        owner.inputs.Clear();
    }

    public static string CreateRequestFingerprint(
        string reasonCode,
        IEnumerable<CropPhysicalInputSaveData> inputs) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{reasonCode}:"
        + string.Join(",", (inputs ?? Array.Empty<CropPhysicalInputSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value =>
                $"{value.itemId}@{value.sourceStackId}={value.quantity}"));

    public static string CreateEcologyFingerprint(
        IReadOnlyList<CropEcologyPlotSaveData> plots,
        string plotId)
    {
        CropEcologyPlotSaveData plot = (plots
                ?? Array.Empty<CropEcologyPlotSaveData>())
            .SingleOrDefault(value => value != null
                && string.Equals(value.plotId, plotId, StringComparison.Ordinal));
        if (plot == null) return "absent";
        static string F(float value) => value.ToString(
            "R",
            CultureInfo.InvariantCulture);
        return string.Join(
            ":",
            plot.plotId,
            plot.cropId,
            plot.cultivarGenomeId,
            (int)plot.currentGroup,
            (int)plot.previousGroup,
            plot.hasPreviousGroup ? 1 : 0,
            F(plot.fertility),
            F(plot.pestPressure),
            F(plot.diseasePressure),
            (int)plot.disease,
            plot.consecutiveLethalTemperatureDays,
            plot.cropDead ? 1 : 0);
    }

    private static bool TrySelectInputs(
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        string destinationId,
        IReadOnlyList<KeyValuePair<string, int>> requirements,
        string seedItemId,
        string cropId,
        out List<CropPhysicalInputSaveData> selected,
        out SeedLotState seedLot,
        out string failureReason)
    {
        selected = new List<CropPhysicalInputSaveData>();
        seedLot = null;
        failureReason = string.Empty;
        WorldItemStackSnapshot[] available = (stacks
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && !stack.Forbidden
                && stack.AvailableQuantity > 0
                && stack.ReservedQuantity == 0
                && string.IsNullOrEmpty(stack.ReservedByPersistentId))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            int remaining = requirement.Value;
            foreach (WorldItemStackSnapshot stack in available
                         .Where(value => string.Equals(
                             value.ItemId,
                             requirement.Key,
                             StringComparison.Ordinal)))
            {
                if (remaining <= 0) break;
                int take = Math.Min(remaining, stack.AvailableQuantity);
                if (string.Equals(requirement.Key, seedItemId, StringComparison.Ordinal))
                {
                    SeedLotState decoded;
                    try
                    {
                        decoded = SeedLotItemStateCodec.Decode(stack.Components);
                    }
                    catch (Exception exception)
                    {
                        failureReason = "crop-seed-lot-component-invalid:"
                            + exception.GetType().Name;
                        return false;
                    }
                    if (!string.Equals(decoded.cropId, cropId, StringComparison.Ordinal))
                        continue;
                    seedLot = decoded.Clone();
                    take = 1;
                }
                selected.Add(new CropPhysicalInputSaveData
                {
                    itemId = requirement.Key,
                    sourceStackId = stack.StackId,
                    quantity = take
                });
                remaining -= take;
            }
            if (remaining > 0)
            {
                selected.Clear();
                seedLot = null;
                failureReason = "crop-physical-input-unavailable:"
                    + requirement.Key;
                return false;
            }
        }
        if (seedLot == null)
        {
            selected.Clear();
            failureReason = "crop-seed-lot-unavailable";
            return false;
        }
        return true;
    }

    private static bool ValidateReceipt(
        CropPhysicalCommitSaveData owner,
        PhysicalItemBatchDispositionReceipt receipt,
        string fingerprint,
        out string failureReason)
    {
        string[] sourceIds = owner.inputs
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => value.sourceStackId)
            .ToArray();
        bool valid = receipt.IsCommitted
            && receipt.Kind == PhysicalItemDispositionKind.Transfer
            && string.Equals(receipt.OperationId, owner.operationId, StringComparison.Ordinal)
            && string.Equals(receipt.ReasonCode, owner.reasonCode, StringComparison.Ordinal)
            && string.Equals(receipt.CommitId, owner.commitId, StringComparison.Ordinal)
            && receipt.Quantity == owner.inputQuantity
            && receipt.InputMassGrams == owner.inputMassGrams
            && receipt.SourceStackIds.SequenceEqual(sourceIds, StringComparer.Ordinal)
            && string.Equals(owner.requestFingerprint, fingerprint, StringComparison.Ordinal);
        failureReason = valid
            ? string.Empty
            : "crop-physical-receipt-mismatch";
        return valid;
    }

    private static bool IsEmpty(CropPhysicalCommitSaveData owner) =>
        owner != null
        && owner.phase == CropPhysicalCommitPhase.None
        && owner.operationSequence == 0
        && string.IsNullOrEmpty(owner.operationId)
        && string.IsNullOrEmpty(owner.reasonCode)
        && string.IsNullOrEmpty(owner.destinationId)
        && string.IsNullOrEmpty(owner.cropId)
        && string.IsNullOrEmpty(owner.seedItemId)
        && owner.inputQuantity == 0
        && owner.inputMassGrams == 0L
        && string.IsNullOrEmpty(owner.commitId)
        && string.IsNullOrEmpty(owner.requestFingerprint)
        && !owner.hasSeedLot
        && string.IsNullOrEmpty(owner.ecologyBeforeFingerprint)
        && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
        && HasNoTerminalDisposition(owner)
        && owner.inputs != null
        && owner.inputs.Count == 0;

    private static bool HasNoTerminalDisposition(
        CropPhysicalCommitSaveData owner) =>
        owner != null
        && owner.terminalDisposition == CropWipTerminalDisposition.None
        && string.IsNullOrEmpty(owner.terminalOperationId)
        && string.IsNullOrEmpty(owner.terminalReasonCode)
        && owner.terminalLossQuantity == 0
        && owner.terminalLossMassGrams == 0L;

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
