using System;
using System.Collections.Generic;

public static class EquipmentEvolutionRestoreBuilder
{
    public static EquipmentEvolutionRestoreCandidate Build(
        EquipmentEvolutionSaveData source)
    {
        if (source == null
            || source.reforgeOrders == null
            || source.reattunementOrders == null)
        {
            throw new InvalidOperationException(
                "Equipment evolution V4 payload is missing a required collection.");
        }

        EquipmentEvolutionAggregateState restored = new();
        HashSet<string> orderIds = new(StringComparer.Ordinal);
        HashSet<string> equipmentReservations = new(StringComparer.Ordinal);
        foreach (EvolutionReforgeOrder order in source.reforgeOrders)
        {
            ValidateReforge(order, orderIds, equipmentReservations);
            restored.ReforgeOrders.Add(order.Clone());
        }
        foreach (EquipmentReattunementOrder order in source.reattunementOrders)
        {
            ValidateReattunement(order, orderIds, equipmentReservations);
            restored.ReattunementOrders.Add(order.Clone());
        }
        return new EquipmentEvolutionRestoreCandidate(restored);
    }

    private static void ValidateReforge(
        EvolutionReforgeOrder order,
        ISet<string> orderIds,
        ISet<string> equipmentReservations)
    {
        if (order == null)
        {
            throw new InvalidOperationException(
                "Equipment reforge order collection contains null.");
        }
        RequireCanonicalId(order.orderId, "reforge order");
        RequireCanonicalId(order.equipmentInstanceId, "reforge equipment");
        RequireCanonicalId(order.facilityPersistentId, "reforge facility");
        RequireCanonicalId(order.catalystItemId, "reforge catalyst item");
        RequireCanonicalId(order.catalystFamily, "reforge catalyst family");
        RequireCanonicalId(order.primaryMaterialItemId, "reforge primary material");
        RequireCanonicalId(order.bindingItemId, "reforge binding item");
        RequireCanonicalTextOrEmpty(order.stabilizerItemId, "reforge stabilizer item");
        RequireCanonicalId(order.destinationId, "reforge destination");
        RequireCanonicalId(order.lockedHistoryHash, "reforge history hash");
        RequireCanonicalTextOrEmpty(
            order.suppressedBurdenEffectId,
            "suppressed burden effect");
        if (order.catalystSourceTags == null)
        {
            throw new InvalidOperationException(
                $"Reforge order '{order.orderId}' has no catalyst source-tag collection.");
        }
        ValidateUniqueIds(order.catalystSourceTags, "catalyst source tag");
        if (!orderIds.Add(order.orderId)
            || !equipmentReservations.Add(order.equipmentInstanceId)
            || order.targetGeneration < 1
            || !Enum.IsDefined(typeof(EquipmentEvolutionDirection), order.direction)
            || !Enum.IsDefined(typeof(EquipmentEvolutionDirection), order.lockedDirection)
            || order.catalystPotency < 1
            || order.catalystPotency
                > EvolutionCatalystProgression.MaximumPotencyGrade
            || order.primaryMaterialAmount < 1
            || order.bindingAmount < 1
            || order.stabilizerAmount < 0
            || (string.IsNullOrEmpty(order.stabilizerItemId)
                != (order.stabilizerAmount == 0))
            || !IsPendingState(order.state)
            || !IsFinitePositive(order.requiredWork)
            || !IsFiniteInRange(order.completedWork, 0f, order.requiredWork, false)
            || order.precisionGoldCost < 0
            || !IsFiniteInRange(order.resultVariance, 0.01f, 0.5f, true)
            || !string.Equals(
                order.destinationId,
                $"facility-reforge:{order.orderId}",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Reforge order '{order.orderId}' has duplicate IDs, invalid references, or out-of-range state.");
        }

        ValidateReforgeMaterialTransfer(order);
    }

    private static void ValidateReattunement(
        EquipmentReattunementOrder order,
        ISet<string> orderIds,
        ISet<string> equipmentReservations)
    {
        if (order == null)
        {
            throw new InvalidOperationException(
                "Equipment reattunement order collection contains null.");
        }
        RequireCanonicalId(order.orderId, "reattunement order");
        RequireCanonicalId(order.equipmentInstanceId, "reattunement equipment");
        RequireCanonicalId(order.facilityPersistentId, "reattunement facility");
        RequireCanonicalId(order.targetNodeId, "reattunement target node");
        RequireCanonicalId(order.catalystItemId, "reattunement catalyst item");
        RequireCanonicalId(order.destinationId, "reattunement destination");
        RequireCanonicalId(order.lockedStateHash, "reattunement state hash");
        if (order.resultingActiveNodeIds == null)
        {
            throw new InvalidOperationException(
                $"Reattunement order '{order.orderId}' has no result-node collection.");
        }
        ValidateUniqueIds(order.resultingActiveNodeIds, "reattunement result node");
        if (!orderIds.Add(order.orderId)
            || !equipmentReservations.Add(order.equipmentInstanceId)
            || order.catalystPotency < 1
            || order.catalystPotency
                > EvolutionCatalystProgression.MaximumPotencyGrade
            || !IsPendingState(order.state)
            || !IsFinitePositive(order.requiredWork)
            || !IsFiniteInRange(order.completedWork, 0f, order.requiredWork, false)
            || !string.Equals(
                order.destinationId,
                $"facility-reattune:{order.orderId}",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Reattunement order '{order.orderId}' has duplicate IDs, invalid references, or out-of-range state.");
        }

        ValidateReattunementMaterialTransfer(order);
    }

    private static void ValidateReforgeMaterialTransfer(
        EvolutionReforgeOrder order)
    {
        ValidateMaterialTransfer(
            order.orderId,
            order.materialsConsumed,
            order.equipmentDelivered,
            order.materialTransferOperationId,
            order.materialTransferCommitId,
            order.materialTransferRequestFingerprint,
            order.materialTransferMassGrams,
            order.materialTransferOutcomePublished,
            order.materialTransferInputs,
            EquipmentEvolutionRules.BuildRequirements(order),
            EquipmentEvolutionMaterialOutbox.FormatReforgeOperationId(
                order.orderId),
            EquipmentEvolutionMaterialOutbox.ReforgeReasonCode,
            "reforge");
    }

    private static void ValidateReattunementMaterialTransfer(
        EquipmentReattunementOrder order)
    {
        ValidateMaterialTransfer(
            order.orderId,
            order.materialsConsumed,
            order.equipmentDelivered,
            order.materialTransferOperationId,
            order.materialTransferCommitId,
            order.materialTransferRequestFingerprint,
            order.materialTransferMassGrams,
            order.materialTransferOutcomePublished,
            order.materialTransferInputs,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [order.catalystItemId] = 1
            },
            EquipmentEvolutionMaterialOutbox.FormatReattunementOperationId(
                order.orderId),
            EquipmentEvolutionMaterialOutbox.ReattunementReasonCode,
            "reattunement");
    }

    private static void ValidateMaterialTransfer(
        string orderId,
        bool materialsConsumed,
        bool equipmentDelivered,
        string operationId,
        string commitId,
        string requestFingerprint,
        long massGrams,
        bool outcomePublished,
        IReadOnlyList<EquipmentEvolutionMaterialTransferInput> inputs,
        IReadOnlyDictionary<string, int> requirements,
        string expectedOperationId,
        string reasonCode,
        string label)
    {
        if (inputs == null)
        {
            throw new InvalidOperationException(
                $"Equipment {label} order '{orderId}' has no material input collection.");
        }

        bool hasPending = !string.IsNullOrEmpty(operationId);
        if (!hasPending)
        {
            if (!string.IsNullOrEmpty(commitId)
                || !string.IsNullOrEmpty(requestFingerprint)
                || massGrams != 0L
                || outcomePublished
                || inputs.Count != 0
                || equipmentDelivered != materialsConsumed)
            {
                throw new InvalidOperationException(
                    $"Equipment {label} order '{orderId}' has partial material provenance.");
            }
            return;
        }

        if (!string.Equals(
                operationId,
                expectedOperationId,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(commitId)
            || !string.Equals(commitId, commitId.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(requestFingerprint)
            || !string.Equals(
                requestFingerprint,
                requestFingerprint.Trim(),
                StringComparison.Ordinal)
            || massGrams <= 0L
            || !materialsConsumed
            || !equipmentDelivered
            || !outcomePublished
            || !EquipmentEvolutionMaterialOutbox.TryValidateInputs(
                requirements,
                inputs,
                out _)
            || !string.Equals(
                requestFingerprint,
                EquipmentEvolutionMaterialOutbox.CreateRequestFingerprint(
                    reasonCode,
                    inputs),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Equipment {label} order '{orderId}' has invalid material Transfer provenance.");
        }
    }

    private static bool IsPendingState(EvolutionReforgeOrderState state)
    {
        return Enum.IsDefined(typeof(EvolutionReforgeOrderState), state)
            && state is not EvolutionReforgeOrderState.Completed
                and not EvolutionReforgeOrderState.Cancelled;
    }

    private static bool IsFinitePositive(float value) =>
        float.IsFinite(value) && value > 0f;

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum,
        bool includeMaximum)
    {
        return float.IsFinite(value)
            && value >= minimum
            && (includeMaximum ? value <= maximum : value < maximum);
    }

    private static void ValidateUniqueIds(
        IEnumerable<string> source,
        string label)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string id in source)
        {
            RequireCanonicalId(id, label);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate {label} id '{id}'.");
            }
        }
    }

    private static void RequireCanonicalId(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} id must be non-empty and canonical.");
        }
    }

    private static void RequireCanonicalTextOrEmpty(string value, string label)
    {
        if (value == null
            || (!string.IsNullOrEmpty(value)
                && !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{label} must be non-null and canonical.");
        }
    }
}
