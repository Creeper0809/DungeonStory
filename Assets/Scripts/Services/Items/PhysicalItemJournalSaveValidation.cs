using System;
using System.Collections.Generic;
using System.Linq;
using static PhysicalItemSaveValidationRules;

internal static class PhysicalItemJournalSaveValidation
{
    internal static void ValidateEffectReceipts(
        DungeonPhysicalItemSaveData snapshot,
        DungeonGameRestoreReport report)
    {
        try
        {
            PhysicalEffectReceiptRules.ValidateCollection(
                snapshot.physicalEffectReceiptSchemaVersion,
                snapshot.physicalEffectReceipts);
        }
        catch (Exception error) when (
            error is InvalidOperationException or OverflowException)
        {
            report.AddError(error.Message);
        }
    }

    internal static bool ValidateTransformJournal(
        IReadOnlyList<PhysicalItemTransformationSaveData> transformations,
        int maximumCount,
        DungeonGameRestoreReport report)
    {
        if (transformations == null
            || transformations.Count > maximumCount)
        {
            report.AddError(
                "Physical transform journal collection is missing or full.");
            return false;
        }

        HashSet<string> transformIds = new(StringComparer.Ordinal);
        string previousTransform = null;
        foreach (PhysicalItemTransformationSaveData transform in transformations)
        {
            try
            {
                if (transform == null)
                {
                    throw new InvalidOperationException(
                        "physical-transform-null");
                }
                transform.Validate();
                string operation = transform.fact.operationId;
                if (!transformIds.Add(operation)
                    || previousTransform != null
                        && string.CompareOrdinal(
                            previousTransform,
                            operation) >= 0)
                {
                    throw new InvalidOperationException(
                        "physical-transform-order-invalid");
                }
                previousTransform = operation;
            }
            catch (Exception exception) when (
                exception is ArgumentException
                    or InvalidOperationException
                    or OverflowException)
            {
                report.AddError(
                    "Physical transform journal: " + exception.Message);
            }
        }
        return true;
    }

    internal static void ValidateQualityRejectedMarketCustodyJoins(
        IReadOnlyList<UniqueItemInstanceSaveData> uniqueItems,
        IReadOnlyList<PhysicalItemBatchDispositionSaveData> dispositions,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, PhysicalItemBatchDispositionSaveData> byOperation =
            (dispositions ?? Array.Empty<PhysicalItemBatchDispositionSaveData>())
            .Where(value => value != null)
            .ToDictionary(value => value.operationId, StringComparer.Ordinal);
        foreach (UniqueItemInstanceSaveData unique in
                 uniqueItems ?? Array.Empty<UniqueItemInstanceSaveData>())
        {
            ItemInstanceComponentSaveData component = unique?.components?
                .FirstOrDefault(value => value != null
                    && string.Equals(
                        value.componentTypeId,
                        ItemInstanceComponentIds.Equipment,
                        StringComparison.Ordinal));
            if (!EquipmentItemStateCodec.TryDecode(
                    component,
                    out CombatEquipmentInstance equipment,
                    out _)
                || equipment.worldState
                    != CombatEquipmentWorldState.MarketSalePending)
            {
                continue;
            }
            string operation = equipment.sourceStackId ?? string.Empty;
            if (!operation.StartsWith(
                    QualityRejectedSaleOutbox.OperationPrefix,
                    StringComparison.Ordinal)
                || !byOperation.TryGetValue(
                    operation,
                    out PhysicalItemBatchDispositionSaveData receipt)
                || receipt.kind != (int)PhysicalItemDispositionKind.Transfer
                || !string.Equals(
                    receipt.reasonCode,
                    QualityRejectedSaleOutbox.TransferReason,
                    StringComparison.Ordinal)
                || receipt.quantity != 1
                || receipt.inputMassGrams <= 0L)
            {
                report.AddError(
                    $"Market-sale-pending equipment '{equipment.instanceId}' has no exact physical Transfer receipt.");
            }
        }
    }

    internal static void ValidateEquipmentModuleAppraisalJoins(
        IReadOnlyList<UniqueItemInstanceSaveData> uniqueItems,
        IReadOnlyList<PhysicalItemBatchDispositionSaveData> dispositions,
        DungeonGameRestoreReport report)
    {
        const string operationPrefix = "equipment-module-appraisal:";
        Dictionary<string, PhysicalItemBatchDispositionSaveData> byOperation =
            dispositions
                .Where(value => value != null
                    && !string.IsNullOrEmpty(value.operationId))
                .GroupBy(value => value.operationId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
        HashSet<string> ownedOperations = new(StringComparer.Ordinal);

        foreach (UniqueItemInstanceSaveData unique in
                 uniqueItems ?? Array.Empty<UniqueItemInstanceSaveData>())
        {
            if (unique == null
                || !PhysicalItemIds.IsEquipmentModule(unique.definitionId))
            {
                continue;
            }
            ItemInstanceComponentSaveData component = unique.components?
                .FirstOrDefault(value => value != null
                    && string.Equals(
                        value.componentTypeId,
                        ItemInstanceComponentIds.EquipmentModule,
                        StringComparison.Ordinal));
            if (!EquipmentModuleItemStateCodec.TryDecode(
                    component,
                    out EquipmentModuleInstance module,
                    out _))
            {
                continue;
            }

            EquipmentModuleAppraisalCommitSaveData pending =
                module.pendingAppraisal;
            EquipmentModuleAppraisalCommitPhase phase =
                (EquipmentModuleAppraisalCommitPhase)pending.phase;
            if (phase == EquipmentModuleAppraisalCommitPhase.None)
            {
                continue;
            }
            if (!ownedOperations.Add(pending.operationId))
            {
                report.AddError(
                    $"Equipment-module appraisal operation '{pending.operationId}' has multiple owners.");
                continue;
            }

            bool hasReceipt = byOperation.TryGetValue(
                pending.operationId,
                out PhysicalItemBatchDispositionSaveData disposition);
            if (!hasReceipt)
            {
                // Intent may have been captured before the item transaction. An
                // outcome may have been captured after terminal acknowledgement
                // but before the module component was cleared.
                continue;
            }

            bool common = disposition.kind
                    == (int)PhysicalItemDispositionKind.Sink
                && string.Equals(
                    disposition.reasonCode,
                    pending.reasonCode,
                    StringComparison.Ordinal)
                && disposition.quantity == pending.quantity
                && disposition.sourceStackIds != null
                && disposition.sourceStackIds.Count == 1
                && string.Equals(
                    disposition.sourceStackIds[0],
                    pending.couponStackId,
                    StringComparison.Ordinal);
            if (phase == EquipmentModuleAppraisalCommitPhase.OutcomePublished)
            {
                common = common
                    && disposition.inputMassGrams == pending.inputMassGrams
                    && string.Equals(
                        disposition.commitId,
                        pending.commitId,
                        StringComparison.Ordinal)
                    && pending.sourceStackIds.SequenceEqual(
                        disposition.sourceStackIds,
                        StringComparer.Ordinal);
            }
            if (!common)
            {
                report.AddError(
                    $"Equipment-module appraisal '{pending.operationId}' does not match its physical Sink receipt.");
            }
        }

        foreach (PhysicalItemBatchDispositionSaveData disposition in dispositions
                     .Where(value => value?.operationId?.StartsWith(
                         operationPrefix,
                         StringComparison.Ordinal) == true))
        {
            if (!ownedOperations.Contains(disposition.operationId))
            {
                report.AddError(
                    $"Physical appraisal Sink '{disposition.operationId}' has no equipment-module owner.");
            }
        }
    }

    internal static void ValidatePhysicalItemRelocations(
        IReadOnlyList<PhysicalItemRelocationSaveData> values,
        DungeonGameRestoreReport report)
    {
        HashSet<string> operations = new(StringComparer.Ordinal);
        string previousOperation = string.Empty;
        foreach (PhysicalItemRelocationSaveData value in
                 values ?? Array.Empty<PhysicalItemRelocationSaveData>())
        {
            string operation = value?.operationId ?? string.Empty;
            bool valid = operations.Add(operation);
            try { PhysicalItemRelocationService.ValidateJournal(value); }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
            { valid = false; }
            if (!valid)
            {
                report.AddError(
                    $"Invalid physical relocation journal '{operation}'.");
                continue;
            }
            if (previousOperation.Length > 0
                && string.CompareOrdinal(previousOperation, operation) >= 0)
            {
                report.AddError(
                    "Physical relocation journals are not in canonical operation order.");
            }
            previousOperation = operation;
        }
    }

    internal static void ValidatePendingBatchDispositions(
        IReadOnlyList<PhysicalItemBatchDispositionSaveData> values,
        DungeonGameRestoreReport report)
    {
        HashSet<string> operations = new(StringComparer.Ordinal);
        HashSet<string> commits = new(StringComparer.Ordinal);
        string previousOperation = string.Empty;
        foreach (PhysicalItemBatchDispositionSaveData value in values)
        {
            string operation = value?.operationId ?? string.Empty;
            if (value == null
                || (value.kind != (int)PhysicalItemDispositionKind.Transfer
                    && value.kind != (int)PhysicalItemDispositionKind.Sink)
                || !IsCanonicalNonEmpty(operation)
                || !IsCanonicalNonEmpty(value.reasonCode)
                || !IsCanonicalNonEmpty(value.requestFingerprint)
                || !IsCanonicalNonEmpty(value.commitId)
                || value.sourceStackIds == null
                || value.sourceStackIds.Count == 0
                || value.sourceStackIds.Any(id => !IsCanonicalNonEmpty(id))
                || value.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                    != value.sourceStackIds.Count
                || value.quantity <= 0
                || value.inputMassGrams <= 0L
                || !operations.Add(operation)
                || !commits.Add(value.commitId))
            {
                report.AddError(
                    $"Invalid pending physical batch disposition '{operation}'.");
                continue;
            }
            for (int index = 1; index < value.sourceStackIds.Count; index++)
            {
                if (string.CompareOrdinal(
                        value.sourceStackIds[index - 1],
                        value.sourceStackIds[index]) >= 0)
                {
                    report.AddError(
                        $"Pending physical batch disposition '{operation}' source IDs are not canonical.");
                    break;
                }
            }
            if (previousOperation.Length > 0
                && string.CompareOrdinal(previousOperation, operation) >= 0)
            {
                report.AddError(
                    "Pending physical batch dispositions are not in canonical operation order.");
            }
            previousOperation = operation;
            List<PhysicalItemDispositionSourceFact> sourceFacts = new();
            try
            {
                sourceFacts = (value.sourceFacts ?? new List<PhysicalItemDispositionSourceFactSaveData>())
                    .Select(PhysicalItemSourceFactSaveCodec.FromSave).ToList();
                bool observed = value.publicationExpected;
                bool valid = value.ownerRevision >= 0 && value.defaultObservationDay >= 0
                    && (!observed || value.ownerRevision > 0 && IsCanonicalNonEmpty(value.publicationKey))
                    && (observed || string.IsNullOrEmpty(value.publicationKey)
                        && string.IsNullOrEmpty(value.publicationId)
                        && string.IsNullOrEmpty(value.publicationFingerprint)
                        && !value.publicationDelivered && value.defaultObservationDay == 0);
                bool hasProof = !string.IsNullOrEmpty(value.publicationId)
                    || !string.IsNullOrEmpty(value.publicationFingerprint) || value.publicationDelivered;
                valid &= !hasProof || observed && IsCanonicalNonEmpty(value.publicationId)
                    && IsLowerSha256(value.publicationFingerprint);
                if (observed || sourceFacts.Count > 0)
                {
                    long factMass = 0;
                    int factQuantity = 0;
                    valid &= sourceFacts.Count == value.sourceStackIds.Count;
                    for (int index = 0; index < sourceFacts.Count; index++)
                    {
                        var fact = sourceFacts[index];
                        valid &= fact.IsValid && index < value.sourceStackIds.Count
                            && string.Equals(fact.StackId, value.sourceStackIds[index], StringComparison.Ordinal);
                        factQuantity = checked(factQuantity + fact.Quantity);
                        factMass = checked(factMass + fact.MassGrams);
                    }
                    valid &= factQuantity == value.quantity && factMass == value.inputMassGrams;
                }
                if (!valid)
                    report.AddError($"Pending physical batch disposition '{operation}' has invalid frozen facts or publication proof.");
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            {
                report.AddError($"Pending physical batch disposition '{operation}' has unreadable frozen facts.");
            }
            PhysicalItemBatchDispositionReceipt reconstructed = new(
                (PhysicalItemDispositionKind)value.kind,
                value.operationId,
                value.reasonCode,
                value.requestFingerprint,
                value.sourceStackIds,
                value.quantity,
                value.inputMassGrams,
                value.ownerRevision,
                sourceFacts);
            if (!reconstructed.IsCommitted
                || !string.Equals(
                    reconstructed.CommitId,
                    value.commitId,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Pending physical batch disposition '{operation}' has a mismatched receipt identity.");
            }
            if (value.defaultObservationDay > 0)
            {
                try { _ = new PhysicalItemDisposedFact(reconstructed, value.defaultObservationDay); }
                catch (Exception exception) when (exception is ArgumentException or OverflowException)
                { report.AddError($"Pending physical batch disposition '{operation}' has an invalid default fact: {exception.Message}"); }
            }
        }
    }
}
