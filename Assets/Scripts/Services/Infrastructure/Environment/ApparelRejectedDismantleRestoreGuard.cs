using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Validates the detached CharacterEnvironment apparel owner against the
/// detached PhysicalItems candidate before either aggregate publishes.
/// Exact physical-ahead rows are accepted only when their deterministic
/// operation/commit identity matches one work-order attempt.
/// </summary>
public sealed class ApparelRejectedDismantleRestoreGuard
{
    private readonly IPhysicalItemRestoreCandidateQuery dispositions;
    private readonly IFacilityBufferPlannedOutputRestoreCandidateQuery outputs;

    public ApparelRejectedDismantleRestoreGuard(
        IPhysicalItemRestoreCandidateQuery dispositions,
        IFacilityBufferPlannedOutputRestoreCandidateQuery outputs)
    {
        this.dispositions = dispositions
            ?? throw new ArgumentNullException(nameof(dispositions));
        this.outputs = outputs
            ?? throw new ArgumentNullException(nameof(outputs));
    }

    public void Validate(IEnumerable<ApparelWorkOrderSaveData> source)
    {
        if (!dispositions.IsCandidateAvailable
            || !outputs.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Apparel rejected-dismantle physical restore candidate is unavailable.");
        }

        ApparelWorkOrderSaveData[] orders = (source
                ?? Enumerable.Empty<ApparelWorkOrderSaveData>())
            .Where(value => value?.dismantlingRejectedOutput == true)
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, ApparelWorkOrderSaveData> byOperation = new(
            StringComparer.Ordinal);
        HashSet<string> expectedOutputCommits = new(StringComparer.Ordinal);
        foreach (ApparelWorkOrderSaveData order in orders)
        {
            if (!ApparelRejectedDismantleOutbox.ValidateOwnerShape(
                    order,
                    out string ownerFailure))
            {
                throw new InvalidOperationException(
                    $"Apparel rejected-dismantle owner '{order?.orderId}' is invalid: {ownerFailure}");
            }
            string operationId = ApparelRejectedDismantleOutbox
                .FormatOperationId(order.orderId, order.qualityAttemptIndex);
            if (!byOperation.TryAdd(operationId, order))
            {
                throw new InvalidOperationException(
                    $"Duplicate apparel rejected-dismantle operation '{operationId}'.");
            }

            bool hasPending = dispositions.TryGetPendingBatchDisposition(
                operationId,
                out PhysicalItemRestoreCandidateDispositionSnapshot pending);
            if (hasPending && !MatchesPending(order, pending))
            {
                throw new InvalidOperationException(
                    $"Apparel rejected-dismantle pending receipt '{operationId}' conflicts with its owner.");
            }
            bool ownerStarted = !string.IsNullOrEmpty(
                order.rejectedDismantleOperationId);
            if (order.rejectedDismantleAcknowledged && hasPending)
            {
                throw new InvalidOperationException(
                    $"Acknowledged apparel rejected-dismantle '{operationId}' still has a pending receipt.");
            }
            if (ownerStarted
                && !order.rejectedDismantleAcknowledged
                && !hasPending
                && !order.rejectedRecoveryPublished)
            {
                throw new InvalidOperationException(
                    $"Apparel rejected-dismantle '{operationId}' lost its pending receipt before recovery publication.");
            }

            bool hasOutput = TryCaptureExpectedOutput(
                order,
                out string expectedCommit,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot committed);
            if (expectedCommit.Length > 0)
                expectedOutputCommits.Add(expectedCommit);
            if (hasOutput && !MatchesOutput(order, committed))
            {
                throw new InvalidOperationException(
                    $"Apparel rejected-dismantle recovery '{expectedCommit}' conflicts with its owner.");
            }
            if (order.rejectedRecoveryPublished
                && order.rejectedMaterialAmount > 0
                && !hasOutput)
            {
                throw new InvalidOperationException(
                    $"Apparel rejected-dismantle recovery '{expectedCommit}' is missing.");
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot pending in
                 dispositions.PendingBatchDispositions
                     ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (pending != null
                && pending.OperationId.StartsWith(
                    ApparelRejectedDismantleOutbox.OperationPrefix,
                    StringComparison.Ordinal)
                && !byOperation.ContainsKey(pending.OperationId))
            {
                throw new InvalidOperationException(
                    $"Orphan apparel rejected-dismantle pending receipt '{pending.OperationId}'.");
            }
        }
        string outputPrefix = "physical-source:"
            + ApparelRejectedDismantleOutbox.RecoveryOperationPrefix;
        foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot output in
                 outputs.Batches
                     ?? Array.Empty<
                         FacilityBufferPlannedOutputRestoreBatchSnapshot>())
        {
            if (output != null
                && output.BatchCommitId.StartsWith(
                    outputPrefix,
                    StringComparison.Ordinal)
                && !expectedOutputCommits.Contains(output.BatchCommitId))
            {
                throw new InvalidOperationException(
                    $"Orphan apparel rejected-dismantle recovery output '{output.BatchCommitId}'.");
            }
        }
    }

    private static bool MatchesPending(
        ApparelWorkOrderSaveData order,
        PhysicalItemRestoreCandidateDispositionSnapshot pending)
    {
        if (pending == null
            || pending.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(
                pending.OperationId,
                ApparelRejectedDismantleOutbox.FormatOperationId(
                    order.orderId,
                    order.qualityAttemptIndex),
                StringComparison.Ordinal)
            || !string.Equals(
                pending.ReasonCode,
                ApparelRejectedDismantleOutbox.ReasonCode,
                StringComparison.Ordinal)
            || !string.Equals(
                pending.RequestFingerprint,
                ApparelRejectedDismantleOutbox.CreateRequestFingerprint(
                    order.rejectedOutputStackId),
                StringComparison.Ordinal)
            || pending.SourceStackIds.Count != 1
            || !string.Equals(
                pending.SourceStackIds[0],
                order.rejectedOutputStackId,
                StringComparison.Ordinal)
            || pending.Quantity != 1
            || pending.InputMassGrams <= 0L)
        {
            return false;
        }
        bool ownerStarted = !string.IsNullOrEmpty(
            order.rejectedDismantleOperationId);
        return !ownerStarted
            || string.Equals(
                    pending.CommitId,
                    order.rejectedDismantleCommitId,
                    StringComparison.Ordinal)
                && pending.InputMassGrams
                    == order.rejectedDismantleInputMassGrams;
    }

    private bool TryCaptureExpectedOutput(
        ApparelWorkOrderSaveData order,
        out string expectedCommit,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot committed)
    {
        expectedCommit = string.Empty;
        committed = null;
        if (order.rejectedMaterialAmount == 0)
            return false;
        string operationId = ApparelRejectedDismantleOutbox
            .FormatRecoveryOperationId(
                order.orderId,
                order.qualityAttemptIndex);
        expectedCommit = ApparelRejectedDismantleOutbox
            .FormatRecoveryCommitId(
                operationId,
                order.rejectedRecoveryItemId,
                order.rejectedMaterialAmount);
        return outputs.TryGetBatch(expectedCommit, out committed);
    }

    private static bool MatchesOutput(
        ApparelWorkOrderSaveData order,
        FacilityBufferPlannedOutputRestoreBatchSnapshot committed)
    {
        FacilityBufferPlannedOutputRestoreStackSnapshot[] rows = (committed
                ?.Stacks
                ?? Array.Empty<
                    FacilityBufferPlannedOutputRestoreStackSnapshot>())
            .Where(value => value != null)
            .ToArray();
        string destinationId = ProductionBillRuntime.OutputDestinationPrefix
            + order.facilityInstanceId;
        return committed != null
            && rows.Length > 0
            && committed.TotalQuantity == order.rejectedMaterialAmount
            && committed.TotalMassGrams > 0L
            && committed.TotalMassGrams
                <= order.rejectedDismantleInputMassGrams
            && rows.Sum(value => (long)value.Quantity)
                == committed.TotalQuantity
            && rows.Sum(value => value.MassGrams)
                == committed.TotalMassGrams
            && rows.All(value => string.Equals(
                    value.ItemId,
                    order.rejectedRecoveryItemId,
                    StringComparison.Ordinal)
                && value.State == WorldItemStackState.FacilityOutputBuffer
                && string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            && (string.IsNullOrEmpty(order.rejectedRecoveryOutcomeFingerprint)
                || string.Equals(
                    committed.OutcomeFingerprint,
                    order.rejectedRecoveryOutcomeFingerprint,
                    StringComparison.Ordinal))
            && (string.IsNullOrEmpty(
                    order.rejectedRecoveryPlannedOutputFingerprint)
                || string.Equals(
                    committed.PlannedOutputFingerprint,
                    order.rejectedRecoveryPlannedOutputFingerprint,
                    StringComparison.Ordinal))
            && (!order.rejectedRecoveryPublished
                || committed.TotalMassGrams
                    == order.rejectedRecoveryOutputMassGrams);
    }
}
