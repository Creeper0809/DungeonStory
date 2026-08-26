using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Pure current-format validation for the Production-owned generic terminal
/// producer and its Items-owned input-destination child. This validator never
/// reads or mutates live aggregate state.
/// </summary>
public sealed class ProductionGenericBillTerminalDrainSaveValidation
{
    public void ValidateOwnPayload(
        DungeonProductionGenericBillTerminalDrainSaveData payload)
    {
        if (payload == null
            || payload.version !=
                DungeonProductionGenericBillTerminalDrainSaveData.CurrentVersion
            || payload.entries == null)
        {
            throw new InvalidOperationException(
                "Generic terminal-drain payload is not current format.");
        }

        ProductionGenericBillTerminalDrainSaveData[] entries = payload.entries
            .ToArray();
        if (entries.Length > 4096
            || entries.Any(value =>
                !ProductionGenericBillTerminalDrainCanonical.IsValidSave(value)))
        {
            throw new InvalidOperationException(
                "Generic terminal-drain payload contains an invalid entry.");
        }

        RequireCanonicalUnique(entries, value => value.stepOperationId,
            "step operation");
        RequireUnique(entries, value => value.ownerStableId, "owner");
        RequireUnique(entries, value => value.billId, "bill");
        RequireUnique(entries, value => value.inputDestinationId,
            "input destination");
        RequireUnique(entries,
            value => value.inputDestinationDrainStepOperationId,
            "input-destination child step");
    }

    public void ValidateCrossAggregate(
        ProductionOutputLifecycleRestoreCandidateBundle lifecycle,
        DungeonProductionGenericBillTerminalDrainSaveData payload,
        IProductionInputDestinationCustodyDrainRestoreCandidateQuery
            inputDrainCandidates)
    {
        if (lifecycle == null)
            throw new ArgumentNullException(nameof(lifecycle));
        if (inputDrainCandidates == null
            || !inputDrainCandidates.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Generic terminal-drain restore requires the detached Items input-drain candidate.");
        }

        ValidateOwnPayload(payload);
        ProductionInputDestinationCustodyDrainSaveData[] children =
            (inputDrainCandidates.Drains ?? Array.Empty<
                ProductionInputDestinationCustodyDrainSaveData>())
            .Select(value => value?.Clone())
            .ToArray();
        if (children.Length > 4096
            || children.Any(value =>
                !ProductionInputDestinationCustodyDrainContract.IsValidSave(value)))
        {
            throw new InvalidOperationException(
                "Generic terminal-drain restore found an invalid Items child.");
        }

        ProductionInputDestinationCustodyDrainSaveData[] physicalRows =
            (lifecycle.PhysicalItems.pendingProductionInputDestinationDrains
                ?? throw new InvalidOperationException(
                    "Generic terminal-drain restore requires the current physical input-drain collection."))
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .ToArray();
        ProductionInputDestinationCustodyDrainSaveData[] projectedRows = children
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .ToArray();
        if (physicalRows.Length != projectedRows.Length
            || physicalRows.Select(JsonUtility.ToJson).Where(
                    (json, index) => !string.Equals(
                        json,
                        JsonUtility.ToJson(projectedRows[index]),
                        StringComparison.Ordinal))
                .Any())
        {
            throw new InvalidOperationException(
                "Detached Items input-drain projection differs from the seven-section physical payload.");
        }

        RequireCanonicalUnique(children, value => value.stepOperationId,
            "Items child step operation");
        RequireUnique(children, value => value.ownerStableId,
            "Items child owner");
        RequireUnique(children, value => value.billId, "Items child bill");
        RequireUnique(children, value => value.sourceDestinationId,
            "Items child source destination");

        Dictionary<string, ProductionInputDestinationCustodyDrainSaveData>
            childrenByStep = children.ToDictionary(
                value => value.stepOperationId,
                StringComparer.Ordinal);
        HashSet<string> joinedChildren = new(StringComparer.Ordinal);
        foreach (ProductionGenericBillTerminalDrainSaveData producer in
                 payload.entries)
        {
            ValidateCanonicalProducerIdentity(producer);
            bool hasChild = childrenByStep.TryGetValue(
                producer.inputDestinationDrainStepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData child);
            if (!hasChild)
            {
                if (producer.phase != ProductionGenericBillTerminalDrainPhase
                        .PreparedAwaitingInputDestinationReceipt)
                {
                    throw new InvalidOperationException(
                        "Generic terminal producer is missing its Items child: "
                        + producer.stepOperationId);
                }
            }
            else
            {
                ValidateExactChild(producer, child);
                joinedChildren.Add(child.stepOperationId);
            }

            ValidateProductionEvidence(lifecycle.Production, producer);
        }

        ProductionInputDestinationCustodyDrainSaveData orphan = children
            .FirstOrDefault(value => !joinedChildren.Contains(
                value.stepOperationId));
        if (orphan != null)
        {
            throw new InvalidOperationException(
                "Items input-destination drain has no generic terminal producer: "
                + orphan.stepOperationId);
        }
    }

    private static void ValidateCanonicalProducerIdentity(
        ProductionGenericBillTerminalDrainSaveData producer)
    {
        if (!ProductionFacilityDestructiveDrainOperationId.TryParse(
                producer.parentOperationId,
                out ProductionFacilityDestructiveDrainOperationId parent))
        {
            throw new InvalidOperationException(
                "Generic terminal producer has an invalid parent operation: "
                + producer.stepOperationId);
        }
        string expectedOwner = ProductionFacilityDestructiveDrainOwnerStableIds
            .GenericBill(producer.billId);
        string expectedStep = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(
                parent,
                ProductionFacilityDestructiveDrainParticipantIds
                    .GenericProductionBills,
                expectedOwner);
        string expectedDestination = ProductionBillRuntime.DestinationPrefix
            + producer.billId;
        if (!string.Equals(producer.ownerStableId,
                expectedOwner, StringComparison.Ordinal)
            || !string.Equals(producer.stepOperationId,
                expectedStep, StringComparison.Ordinal)
            || !string.Equals(producer.inputDestinationDrainStepOperationId,
                expectedStep + ":input-destination-custody",
                StringComparison.Ordinal)
            || !string.Equals(producer.inputDestinationId,
                expectedDestination, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Generic terminal producer does not use its canonical owner, step, child, or destination identity: "
                + producer.stepOperationId);
        }
    }

    private static void ValidateExactChild(
        ProductionGenericBillTerminalDrainSaveData producer,
        ProductionInputDestinationCustodyDrainSaveData child)
    {
        if (!string.Equals(child.parentOperationId,
                producer.parentOperationId, StringComparison.Ordinal)
            || !string.Equals(child.stepOperationId,
                producer.inputDestinationDrainStepOperationId,
                StringComparison.Ordinal)
            || !string.Equals(child.ownerStableId,
                producer.ownerStableId, StringComparison.Ordinal)
            || !string.Equals(child.billId,
                producer.billId, StringComparison.Ordinal)
            || !string.Equals(child.facilityId,
                producer.facilityId, StringComparison.Ordinal)
            || !string.Equals(child.sourceDestinationId,
                producer.inputDestinationId, StringComparison.Ordinal)
            || !string.Equals(child.sourceClaimFingerprint,
                producer.sourceBillFingerprint, StringComparison.Ordinal)
            || !string.Equals(child.requestFingerprint,
                producer.inputDestinationDrainRequestFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Generic terminal producer and Items child identity or fingerprint differs: "
                + producer.stepOperationId);
        }

        bool producerRecorded = producer.phase >=
            ProductionGenericBillTerminalDrainPhase
                .InputDestinationReceiptRecordedAwaitingAcknowledgement;
        bool producerAcknowledged = producer.phase >=
            ProductionGenericBillTerminalDrainPhase
                .InputDestinationAcknowledgedAwaitingBillTerminal;
        bool childCommitted = child.phase is
            ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck
            or ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc;
        if (producer.phase == ProductionGenericBillTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt
            && child.phase == ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc)
        {
            throw new InvalidOperationException(
                "Prepared generic producer cannot lag an acknowledged Items child: "
                + producer.stepOperationId);
        }
        if (producerRecorded
            && (!childCommitted
                || !string.Equals(producer.inputDestinationDrainCommitId,
                    child.commitId, StringComparison.Ordinal)
                || !string.Equals(
                    producer.inputDestinationDrainReceiptFingerprint,
                    child.receiptFingerprint, StringComparison.Ordinal)
                || producer.releasedInputQuantity != child.releasedQuantity
                || producer.releasedInputMassGrams != child.releasedMassGrams))
        {
            throw new InvalidOperationException(
                "Generic terminal producer has no exact committed Items child receipt: "
                + producer.stepOperationId);
        }
        if (producerAcknowledged
            && child.phase != ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc)
        {
            throw new InvalidOperationException(
                "Generic terminal producer advanced before its Items child acknowledgement: "
                + producer.stepOperationId);
        }
    }

    private static void ValidateProductionEvidence(
        DungeonProductionBillSaveData production,
        ProductionGenericBillTerminalDrainSaveData producer)
    {
        if (production?.bills == null || production.wipTerminalReceipts == null)
        {
            throw new InvalidOperationException(
                "Generic terminal-drain validation requires current Production bill and WIP receipt collections.");
        }

        ProductionBillSaveData[] liveBills = production.bills
            .Where(value => value != null && string.Equals(
                value.billId, producer.billId, StringComparison.Ordinal))
            .ToArray();
        if (liveBills.Length > 1)
        {
            throw new InvalidOperationException(
                "Generic terminal source bill is duplicated: " + producer.billId);
        }

        bool hasExactLiveBill = liveBills.Length == 1
            && string.Equals(
                ProductionGenericBillTerminalDrainCanonical
                    .CreateSourceBillFingerprint(liveBills[0]),
                producer.sourceBillFingerprint,
                StringComparison.Ordinal);
        if (liveBills.Length == 1 && !hasExactLiveBill)
        {
            throw new InvalidOperationException(
                "Generic terminal source bill fingerprint drifted: "
                + producer.billId);
        }

        bool requiresWip = ProductionGenericBillTerminalDrainCanonical
            .RequiresWipTerminalReceipt(producer.sourceBill);
        string expectedWipCommit = requiresWip
            ? ProductionGenericBillTerminalDrainCanonical
                .CreateWipTerminalCommitId(
                    producer.billId,
                    producer.sourceBill.cycleSequence)
            : string.Empty;
        ProductionWipTerminalReceiptSaveData[] wipMatches = requiresWip
            ? production.wipTerminalReceipts
                .Where(value => value != null && string.Equals(
                    value.commitId, expectedWipCommit, StringComparison.Ordinal))
                .ToArray()
            : Array.Empty<ProductionWipTerminalReceiptSaveData>();
        if (wipMatches.Length > 1
            || wipMatches.Length == 1
                && !ExactWipReceiptMatches(
                    producer.sourceBill,
                    wipMatches[0]))
        {
            throw new InvalidOperationException(
                "Generic terminal WIP receipt is duplicated or conflicts: "
                + producer.billId);
        }
        bool hasExactWip = wipMatches.Length == 1;

        bool effectCanBeAhead = producer.phase ==
            ProductionGenericBillTerminalDrainPhase
                .InputDestinationAcknowledgedAwaitingBillTerminal;
        bool producerTerminal = producer.phase >=
            ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement;
        if (!effectCanBeAhead && !producerTerminal)
        {
            if (!hasExactLiveBill || hasExactWip)
            {
                throw new InvalidOperationException(
                    "Pre-terminal generic producer lacks its exact source bill or has premature WIP evidence: "
                    + producer.billId);
            }
            return;
        }

        if (producerTerminal && hasExactLiveBill)
        {
            throw new InvalidOperationException(
                "Terminal generic producer still has a live source bill: "
                + producer.billId);
        }
        if (!hasExactLiveBill && requiresWip && !hasExactWip)
        {
            throw new InvalidOperationException(
                "Generic producer's bill effect is ahead without its exact WIP terminal receipt: "
                + producer.billId);
        }
        if (producerTerminal && requiresWip && !hasExactWip)
        {
            throw new InvalidOperationException(
                "Terminal generic producer has no exact WIP terminal receipt: "
                + producer.billId);
        }
    }

    private static bool ExactWipReceiptMatches(
        ProductionBillSaveData source,
        ProductionWipTerminalReceiptSaveData receipt)
    {
        if (source == null || receipt == null)
            return false;
        try
        {
            long outputMass = (source.resolvedOutputs
                    ?? new List<ProductionResolvedOutputSaveData>())
                .Where(value => value != null)
                .Aggregate(0L, (total, value) => checked(
                    total + value.committedMassGrams));
            long availableMass = checked(
                source.wipInputMassGrams + source.processCleanWaterMassGrams);
            long declaredLoss = checked(
                availableMass - checked(
                    outputMass + source.processWastewaterMassGrams));
            return declaredLoss >= 0L
                && string.Equals(receipt.commitId,
                    ProductionGenericBillTerminalDrainCanonical
                        .CreateWipTerminalCommitId(
                            source.billId,
                            source.cycleSequence),
                    StringComparison.Ordinal)
                && string.Equals(receipt.billId, source.billId,
                    StringComparison.Ordinal)
                && string.Equals(receipt.recipeId, source.recipeId,
                    StringComparison.Ordinal)
                && string.Equals(receipt.buildingInstanceId,
                    source.buildingInstanceId, StringComparison.Ordinal)
                && receipt.cycleSequence == source.cycleSequence
                && string.Equals(receipt.inputCommitId,
                    source.wipInputCommitId, StringComparison.Ordinal)
                && receipt.inputQuantity == source.wipInputQuantity
                && receipt.inputMassGrams == source.wipInputMassGrams
                && receipt.processCleanWaterMassGrams ==
                    source.processCleanWaterMassGrams
                && receipt.processWastewaterMassGrams ==
                    source.processWastewaterMassGrams
                && receipt.committedOutputMassGrams == outputMass
                && receipt.reason == ProductionWipTerminalReason.FacilityDestroyed
                && receipt.lossKind == ProductionWipTerminalLossKind
                    .ExplicitIrrecoverableProcessLoss
                && receipt.declaredLossMassGrams == declaredLoss
                && WastewaterEquals(
                    receipt.wastewaterComponents,
                    source.processWastewaterComponents);
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool WastewaterEquals(
        IEnumerable<ProductionWastewaterComponentSaveData> actual,
        IEnumerable<ProductionWastewaterComponentSaveData> expected)
    {
        ProductionWastewaterComponentSaveData[] left = (actual
                ?? Array.Empty<ProductionWastewaterComponentSaveData>())
            .OrderBy(value => value == null ? -1 : (int)value.composition)
            .ThenBy(value => value == null ? -1 : (int)value.sourceKind)
            .ThenBy(value => value?.sourceStableId, StringComparer.Ordinal)
            .ToArray();
        ProductionWastewaterComponentSaveData[] right = (expected
                ?? Array.Empty<ProductionWastewaterComponentSaveData>())
            .OrderBy(value => value == null ? -1 : (int)value.composition)
            .ThenBy(value => value == null ? -1 : (int)value.sourceKind)
            .ThenBy(value => value?.sourceStableId, StringComparer.Ordinal)
            .ToArray();
        return left.Length == right.Length
            && left.Select((value, index) => ComponentEquals(
                    value,
                    right[index]))
                .All(value => value);
    }

    private static bool ComponentEquals(
        ProductionWastewaterComponentSaveData left,
        ProductionWastewaterComponentSaveData right) =>
        left != null && right != null
        && left.composition == right.composition
        && left.sourceKind == right.sourceKind
        && string.Equals(left.sourceStableId, right.sourceStableId,
            StringComparison.Ordinal)
        && left.authoredUnits.Equals(right.authoredUnits)
        && left.massGrams == right.massGrams;

    private static void RequireCanonicalUnique<T>(
        IReadOnlyList<T> rows,
        Func<T, string> key,
        string label)
    {
        RequireUnique(rows, key, label);
        string[] values = rows.Select(key).ToArray();
        if (!values.SequenceEqual(
                values.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Generic terminal-drain " + label
                + " rows are not in canonical ordinal order.");
        }
    }

    private static void RequireUnique<T>(
        IReadOnlyList<T> rows,
        Func<T, string> key,
        string label)
    {
        string[] values = rows.Select(key).ToArray();
        if (values.Any(string.IsNullOrEmpty)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new InvalidOperationException(
                "Generic terminal-drain " + label
                + " identity is invalid or duplicated.");
        }
    }
}
