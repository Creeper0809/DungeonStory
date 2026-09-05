using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Moves one concrete craft attempt's exact FacilityBuffer lots into durable
/// work-in-progress custody. The receipt stays pending until the resolved
/// physical output and its domain effects have both been published.
/// </summary>
public static class CombatEquipmentCraftMaterialOutbox
{
    public const string ReasonCode = "combat-equipment-craft-materials-to-wip";
    public const string OperationPrefix = "combat-craft-material:";

    public static string FormatOperationId(string orderId, int attemptIndex) =>
        $"{OperationPrefix}{orderId}:{Math.Max(0, attemptIndex):D4}";

    public static bool TryCommitOrResume(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        IEquipmentPhysicalItemGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || requirements == null || stacks == null || items == null)
        {
            failureReason = "combat-craft-material-outbox-invalid";
            return false;
        }

        KeyValuePair<string, int>[] required = requirements
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        if (required.Length == 0)
        {
            return string.IsNullOrEmpty(order.materialTransferOperationId);
        }
        if (order.materialTransferAcknowledged)
        {
            return ValidateProvenance(order, requirements, out failureReason);
        }

        bool starting = string.IsNullOrEmpty(order.materialTransferOperationId);
        List<CombatEquipmentCraftMaterialTransferInput> selected;
        string operationId = FormatOperationId(
            order.orderId,
            order.qualityAttemptIndex);
        if (starting)
        {
            if (!TrySelectInputs(
                    order,
                    required,
                    stacks,
                    out selected,
                    out failureReason))
            {
                return false;
            }
        }
        else
        {
            if (!ValidateProvenance(order, requirements, out failureReason))
            {
                return false;
            }
            selected = order.materialTransferInputs
                .Select(input => input.Clone())
                .ToList();
            if (!items.TryGetPendingBatchPhysicalDisposition(operationId, out _))
            {
                failureReason = "combat-craft-material-receipt-missing";
                return false;
            }
        }

        PhysicalItemTransformInput[] physicalInputs = selected
            .OrderBy(input => input.sourceStackId, StringComparer.Ordinal)
            .Select(input => new PhysicalItemTransformInput(
                input.sourceStackId,
                input.quantity))
            .ToArray();
        if (!items.TryCommitPendingBatchPhysicalDisposition(
                physicalInputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }

        string fingerprint = CreateRequestFingerprint(selected);
        if (starting)
        {
            // Publish the domain owner immediately after the physical debit so
            // every later failure remains a recoverable WIP receipt.
            order.materialTransferOperationId = receipt.OperationId;
            order.materialTransferCommitId = receipt.CommitId;
            order.materialTransferRequestFingerprint = fingerprint;
            order.materialTransferMassGrams = receipt.InputMassGrams;
            order.materialTransferInputs = selected
                .Select(input => input.Clone())
                .ToList();
            order.materialsReady = true;
        }

        if (!ValidateReceipt(order, receipt, out failureReason)
            || !string.Equals(
                order.materialTransferRequestFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "combat-craft-material-provenance-mismatch"
                : failureReason;
            return false;
        }
        return true;
    }

    public static bool TryAcknowledgeOutcome(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements,
        IEquipmentPhysicalItemGateway items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || items == null
            || !ValidateProvenance(order, requirements, out failureReason))
        {
            return false;
        }
        if (order.materialTransferAcknowledged)
        {
            return true;
        }
        if (!order.attemptOutcomeResolved
            || !order.outputPublished
            || !order.completionEffectsPublished)
        {
            failureReason = "combat-craft-outcome-not-fully-published";
            return false;
        }
        if (!items.AcknowledgeBatchPhysicalDisposition(
                order.materialTransferCommitId,
                out failureReason))
        {
            return false;
        }
        order.materialTransferAcknowledged = true;
        return true;
    }

    public static bool ValidateProvenance(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyDictionary<string, int> requirements,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || order.materialTransferInputs == null)
        {
            failureReason = "combat-craft-material-owner-invalid";
            return false;
        }

        bool hasOperation = !string.IsNullOrEmpty(order.materialTransferOperationId);
        if (!hasOperation)
        {
            bool empty = string.IsNullOrEmpty(order.materialTransferCommitId)
                && string.IsNullOrEmpty(order.materialTransferRequestFingerprint)
                && order.materialTransferMassGrams == 0L
                && order.materialTransferInputs.Count == 0
                && !order.materialTransferAcknowledged
                && !order.attemptOutcomeResolved
                && !order.outputPublished;
            if (!empty)
            {
                failureReason = "combat-craft-material-owner-partial";
            }
            return empty;
        }

        CombatEquipmentCraftMaterialTransferInput[] inputs =
            order.materialTransferInputs
                .Where(input => input != null)
                .OrderBy(input => input.sourceStackId, StringComparer.Ordinal)
                .ToArray();
        Dictionary<string, int> actual = inputs
            .GroupBy(input => input.itemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(input => input.quantity),
                StringComparer.Ordinal);
        KeyValuePair<string, int>[] expected = (requirements
                ?? new Dictionary<string, int>())
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        bool exactRequirements = expected.Length == actual.Count
            && expected.All(pair => actual.TryGetValue(pair.Key, out int amount)
                && amount == pair.Value);
        bool valid = order.materialsReady
            && string.Equals(
                order.materialTransferOperationId,
                FormatOperationId(order.orderId, order.qualityAttemptIndex),
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(order.materialTransferCommitId)
            && order.materialTransferMassGrams > 0L
            && inputs.Length == order.materialTransferInputs.Count
            && inputs.Length > 0
            && inputs.All(input =>
                IsCanonical(input.itemId)
                && IsCanonical(input.sourceStackId)
                && input.quantity > 0)
            && inputs.Select(input => input.sourceStackId)
                .Distinct(StringComparer.Ordinal).Count() == inputs.Length
            && exactRequirements
            && string.Equals(
                order.materialTransferRequestFingerprint,
                CreateRequestFingerprint(inputs),
                StringComparison.Ordinal)
            && (!order.materialTransferAcknowledged
                || (order.attemptOutcomeResolved
                    && order.outputPublished
                    && order.completionEffectsPublished));
        if (!valid)
        {
            failureReason = "combat-craft-material-owner-invalid";
        }
        return valid;
    }

    public static string CreateRequestFingerprint(
        IReadOnlyList<CombatEquipmentCraftMaterialTransferInput> inputs) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{ReasonCode}:"
        + string.Join(",", (inputs
                ?? Array.Empty<CombatEquipmentCraftMaterialTransferInput>())
            .Where(input => input != null)
            .OrderBy(input => input.sourceStackId, StringComparer.Ordinal)
            .Select(input => $"{input.sourceStackId}={input.quantity}"));

    public static void ClearCompletedAttempt(
        CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null)
        {
            return;
        }
        order.materialsReady = false;
        order.materialTransferOperationId = string.Empty;
        order.materialTransferCommitId = string.Empty;
        order.materialTransferRequestFingerprint = string.Empty;
        order.materialTransferMassGrams = 0L;
        order.materialTransferInputs.Clear();
        order.materialTransferAcknowledged = false;
        order.attemptOutcomeResolved = false;
        order.resolvedQuality = CombatEquipmentQuality.Normal;
        order.resolvedMythicProvenance = null;
        order.resolvedMakerCharacterId = string.Empty;
        order.resolvedHadInspiration = false;
        order.completionEffectsPublished = false;
        order.outputPublished = false;
        order.outputOperationId = string.Empty;
        order.outputItemId = string.Empty;
        order.outputQuantity = 0;
        order.outputCapability = new ProductionOutputCapabilitySaveData();
        order.outputPhase = CombatEquipmentCraftOutputPhase.None;
        order.outputPublication =
            new ProductionDomainOutputPublicationSaveData();
        order.outputMarketRouted = false;
        order.outputPreparedComponent = null;
        order.outputCommitId = string.Empty;
        order.outputInstanceId = string.Empty;
        order.outputStackId = string.Empty;
    }

    private static bool TrySelectInputs(
        CombatEquipmentCraftOrderSaveData order,
        IReadOnlyList<KeyValuePair<string, int>> requirements,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        out List<CombatEquipmentCraftMaterialTransferInput> inputs,
        out string failureReason)
    {
        inputs = new List<CombatEquipmentCraftMaterialTransferInput>();
        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            int remaining = requirement.Value;
            foreach (WorldItemStackSnapshot stack in stacks
                         .Where(stack => stack != null
                             && stack.State == WorldItemStackState.FacilityBuffer
                             && string.Equals(
                                 stack.DestinationId,
                                 order.materialDestinationId,
                                 StringComparison.Ordinal)
                             && string.Equals(
                                 stack.ItemId,
                                 requirement.Key,
                                 StringComparison.Ordinal)
                             && !stack.Forbidden
                             && stack.AvailableQuantity > 0
                             && stack.ReservedQuantity == 0
                             && string.IsNullOrEmpty(
                                 stack.ReservedByPersistentId))
                         .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
            {
                if (remaining <= 0)
                {
                    break;
                }
                int quantity = Math.Min(remaining, stack.AvailableQuantity);
                inputs.Add(new CombatEquipmentCraftMaterialTransferInput
                {
                    itemId = requirement.Key,
                    sourceStackId = stack.StackId,
                    quantity = quantity
                });
                remaining -= quantity;
            }
            if (remaining > 0)
            {
                inputs.Clear();
                failureReason = "combat-craft-materials-missing:" + requirement.Key;
                return false;
            }
        }
        inputs = inputs
            .OrderBy(input => input.sourceStackId, StringComparer.Ordinal)
            .ToList();
        failureReason = string.Empty;
        return inputs.Count > 0;
    }

    private static bool ValidateReceipt(
        CombatEquipmentCraftOrderSaveData order,
        PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        string[] sourceIds = order.materialTransferInputs
            .OrderBy(input => input.sourceStackId, StringComparer.Ordinal)
            .Select(input => input.sourceStackId)
            .ToArray();
        bool valid = receipt.IsCommitted
            && receipt.Kind == PhysicalItemDispositionKind.Transfer
            && string.Equals(
                receipt.OperationId,
                order.materialTransferOperationId,
                StringComparison.Ordinal)
            && string.Equals(receipt.ReasonCode, ReasonCode, StringComparison.Ordinal)
            && string.Equals(receipt.CommitId, order.materialTransferCommitId, StringComparison.Ordinal)
            && receipt.Quantity == order.materialTransferInputs.Sum(input => input.quantity)
            && receipt.InputMassGrams == order.materialTransferMassGrams
            && receipt.SourceStackIds.SequenceEqual(sourceIds, StringComparer.Ordinal);
        if (!valid)
        {
            failureReason = "combat-craft-material-receipt-mismatch";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
