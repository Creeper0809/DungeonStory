using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Diagnostics-only correlation and one-shot receipt authority for completed
/// generic recipe cycles. Normal gameplay cycles that have no registered
/// action remain allocation-free apart from the caller's existing work.
/// </summary>
public sealed class ProductionRecipeExecutionReceiptAuthority :
    IProductionRecipeExecutionReceiptAuthority
{
    private readonly Dictionary<string, ProductionRecipeExecutionCorrelation>
        correlationByAction = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> actionByCycle =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProductionRecipeExecutionReceipt>
        receiptByAction = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExactCycleStaging> exactByAction =
        new(StringComparer.Ordinal);

    public bool RequiresExactCapture(
        ProductionBillId billId,
        int cycleSequence)
    {
        if (actionByCycle.Count == 0
            || !billId.IsValid
            || cycleSequence <= 0)
        {
            return false;
        }
        return actionByCycle.ContainsKey(CycleKey(billId, cycleSequence));
    }

    public bool TryRegisterExecution(
        string actionId,
        ProductionRecipeExecutionCorrelation correlation,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsCanonical(actionId) || correlation == null)
        {
            failureReason = "recipe-execution-correlation-invalid";
            return false;
        }

        string cycleKey = CycleKey(
            correlation.BillId,
            correlation.CycleSequence);
        if (correlationByAction.TryGetValue(
                actionId,
                out ProductionRecipeExecutionCorrelation existing))
        {
            if (string.Equals(
                    existing.SourceDigest,
                    correlation.SourceDigest,
                    StringComparison.Ordinal)
                && !receiptByAction.ContainsKey(actionId))
            {
                return true;
            }
            failureReason = "recipe-execution-action-already-owned";
            return false;
        }
        if (actionByCycle.TryGetValue(cycleKey, out string existingAction))
        {
            failureReason = "recipe-execution-cycle-already-correlated:"
                + existingAction;
            return false;
        }

        correlationByAction.Add(actionId, correlation);
        actionByCycle.Add(cycleKey, actionId);
        return true;
    }

    public bool TryCancelExecution(
        string actionId,
        ProductionRecipeExecutionCorrelation correlation,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsCanonical(actionId)
            || correlation == null
            || !correlationByAction.TryGetValue(
                actionId,
                out ProductionRecipeExecutionCorrelation existing)
            || !string.Equals(
                existing.SourceDigest,
                correlation.SourceDigest,
                StringComparison.Ordinal))
        {
            failureReason = "recipe-execution-correlation-cancel-mismatch";
            return false;
        }
        receiptByAction.Remove(actionId);
        correlationByAction.Remove(actionId);
        actionByCycle.Remove(CycleKey(
            correlation.BillId,
            correlation.CycleSequence));
        exactByAction.Remove(actionId);
        return true;
    }

    public bool TryCaptureExecutionReceipt(
        string actionId,
        out ProductionRecipeExecutionReceipt receipt)
    {
        receipt = null;
        return IsCanonical(actionId)
            && receiptByAction.TryGetValue(actionId, out receipt)
            && receipt != null;
    }

    public bool TryAcknowledgeExecutionReceipt(
        string actionId,
        string runtimeReceiptDigest,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsCanonical(actionId)
            || !IsDigest(runtimeReceiptDigest)
            || !receiptByAction.TryGetValue(
                actionId,
                out ProductionRecipeExecutionReceipt receipt)
            || !string.Equals(
                receipt.RuntimeReceiptDigest,
                runtimeReceiptDigest,
                StringComparison.Ordinal)
            || !correlationByAction.TryGetValue(
                actionId,
                out ProductionRecipeExecutionCorrelation correlation))
        {
            failureReason = "recipe-execution-receipt-acknowledgement-mismatch";
            return false;
        }

        receiptByAction.Remove(actionId);
        correlationByAction.Remove(actionId);
        actionByCycle.Remove(CycleKey(
            correlation.BillId,
            correlation.CycleSequence));
        exactByAction.Remove(actionId);
        return true;
    }

    public bool TryPublishCompleted(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        BuildingInstanceId facilityId,
        string wipInputCommitId,
        int wipInputQuantity,
        long wipInputMassGrams,
        ProductionPreparedOutputBatchSaveData completedBatch,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actionByCycle.Count == 0)
        {
            // Keep the normal gameplay completion path allocation-free while
            // no diagnostics action owns a generic cycle.
            return true;
        }
        string cycleKey = CycleKey(billId, cycleSequence);
        if (!actionByCycle.TryGetValue(cycleKey, out string actionId))
        {
            // Receipt capture is opt-in diagnostics. Uncorrelated gameplay is
            // deliberately untouched and must not acquire a hidden owner.
            return true;
        }
        if (!correlationByAction.TryGetValue(
                actionId,
                out ProductionRecipeExecutionCorrelation correlation)
            || !string.Equals(
                correlation.RecipeId,
                recipeId,
                StringComparison.Ordinal)
            || !correlation.FacilityId.Equals(facilityId)
            || exactByAction.ContainsKey(actionId)
            || completedBatch == null
            || completedBatch.phase != ProductionPreparedOutputPhase.Completed
            || completedBatch.lines == null
            || completedBatch.physicalCandidates == null
            || !string.Equals(
                completedBatch.billId,
                billId.Value,
                StringComparison.Ordinal)
            || completedBatch.cycleSequence != cycleSequence
            || !string.Equals(
                completedBatch.recipeId,
                recipeId,
                StringComparison.Ordinal)
            || !string.Equals(
                completedBatch.destinationId,
                ProductionOutputDestinationId.FromFacility(facilityId).Value,
                StringComparison.Ordinal))
        {
            failureReason = "recipe-execution-completed-owner-mismatch";
            return false;
        }
        if (completedBatch.lines.Any(value => value == null)
            || completedBatch.physicalCandidates.Any(value => value == null
                || value.state != ProductionPreparedPhysicalCandidateState
                    .FacilityOutputBuffer
                || !string.Equals(
                    value.batchCommitId,
                    completedBatch.batchCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    value.destinationId,
                    completedBatch.destinationId,
                    StringComparison.Ordinal)))
        {
            failureReason = "recipe-execution-completed-physical-slice-invalid";
            return false;
        }

        try
        {
            ProductionRecipeExecutionOutputLineReceipt[] outputs =
                completedBatch.lines
                    .Where(value => value != null
                        && ProductionOutputRoleRules.IsPhysical(value.role)
                        && value.rollSucceeded
                        && value.quantity > 0
                        && value.exactMassGrams > 0L)
                    .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                    .Select(value => new ProductionRecipeExecutionOutputLineReceipt(
                        value.outputLineId,
                        value.itemId,
                        value.quantity,
                        value.exactMassGrams,
                        value.outputCapabilityFingerprint,
                        new[] { value.lineCommitId }))
                    .ToArray();
            ProductionRecipeExecutionPhysicalSliceReceipt[] slices =
                completedBatch.physicalCandidates
                    .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                    .ThenBy(value => value.stackId, StringComparer.Ordinal)
                    .Select(value => new
                        ProductionRecipeExecutionPhysicalSliceReceipt(
                            value.outputLineId,
                            value.itemId,
                            value.stackId,
                            value.quantity,
                            value.massGrams,
                            value.lineCommitId))
                    .ToArray();
            ProductionRecipeExecutionReceipt candidate = new(
                actionId,
                correlation,
                wipInputCommitId,
                wipInputQuantity,
                wipInputMassGrams,
                ProductionRecipeExecutionPublicationKind.PreparedBatch,
                completedBatch.batchCommitId,
                new[] { completedBatch.batchCommitId },
                completedBatch.outcomeFingerprint,
                completedBatch.admissionFingerprint,
                outputs,
                slices);

            if (candidate.ActualBatchMassGrams
                    != completedBatch.totalPhysicalMassGrams
                || outputs.Sum(value => value.MassGrams)
                    != completedBatch.totalPhysicalMassGrams)
            {
                failureReason = "recipe-execution-completed-mass-mismatch";
                return false;
            }
            if (receiptByAction.TryGetValue(
                    actionId,
                    out ProductionRecipeExecutionReceipt existing))
            {
                if (string.Equals(
                    existing.RuntimeReceiptDigest,
                    candidate.RuntimeReceiptDigest,
                    StringComparison.Ordinal))
                {
                    return true;
                }
                failureReason = "recipe-execution-completed-replay-conflict";
                return false;
            }
            receiptByAction.Add(actionId, candidate);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "recipe-execution-completed-receipt-invalid:"
                + exception.Message;
            return false;
        }
    }

    public bool TryCaptureExactCommittedUnit(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        BuildingInstanceId facilityId,
        ProductionResolvedOutputSaveData output,
        ProductionCommittedOutputSnapshot committedOutput,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actionByCycle.Count == 0)
            return true;
        string cycleKey = CycleKey(billId, cycleSequence);
        if (!actionByCycle.TryGetValue(cycleKey, out string actionId))
            return true;
        if (!TryRequireCorrelation(
                actionId,
                recipeId,
                facilityId,
                out ProductionRecipeExecutionCorrelation correlation,
                out failureReason))
        {
            return false;
        }
        if (receiptByAction.ContainsKey(actionId))
        {
            failureReason = "recipe-execution-exact-unit-after-finalize";
            return false;
        }
        if (!TryCreateExactUnit(
                output,
                committedOutput,
                correlation,
                out ExactCommittedUnit unit,
                out failureReason))
        {
            return false;
        }

        if (!exactByAction.TryGetValue(
                actionId,
                out ExactCycleStaging staging))
        {
            staging = new ExactCycleStaging(correlation.SourceDigest);
            exactByAction.Add(actionId, staging);
        }
        if (!string.Equals(
                staging.CorrelationDigest,
                correlation.SourceDigest,
                StringComparison.Ordinal))
        {
            failureReason = "recipe-execution-exact-staging-owner-mismatch";
            return false;
        }
        if (!staging.TryAdd(unit, out failureReason))
            return false;
        return true;
    }

    public bool TryFinalizeExactCompleted(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        BuildingInstanceId facilityId,
        string wipInputCommitId,
        int wipInputQuantity,
        long wipInputMassGrams,
        IReadOnlyList<ProductionResolvedOutputSaveData> completedOutputs,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actionByCycle.Count == 0)
            return true;
        string cycleKey = CycleKey(billId, cycleSequence);
        if (!actionByCycle.TryGetValue(cycleKey, out string actionId))
            return true;
        if (!TryRequireCorrelation(
                actionId,
                recipeId,
                facilityId,
                out ProductionRecipeExecutionCorrelation correlation,
                out failureReason))
        {
            return false;
        }
        if (receiptByAction.ContainsKey(actionId))
        {
            failureReason = "recipe-execution-exact-finalize-replay";
            return false;
        }
        if (!exactByAction.TryGetValue(actionId, out ExactCycleStaging staging)
            || !string.Equals(
                staging.CorrelationDigest,
                correlation.SourceDigest,
                StringComparison.Ordinal))
        {
            failureReason = "recipe-execution-exact-staging-missing";
            return false;
        }

        ProductionResolvedOutputSaveData[] outputs = (completedOutputs
                ?? Array.Empty<ProductionResolvedOutputSaveData>())
            .OrderBy(value => value?.outputLineId, StringComparer.Ordinal)
            .ToArray();
        if (outputs.Length == 0
            || outputs.Any(value => value == null
                || value.amount <= 0
                || value.committedAmount != value.amount
                || value.committedMassGrams <= 0L
                || !string.IsNullOrEmpty(value.pendingCommitId)
                || value.pendingCommitApplied
                || value.pendingOutputPublication == null
                || value.pendingOutputPublication.phase !=
                    ProductionExactOutputPublicationPhase.None)
            || outputs.Select(value => value.outputLineId)
                .Distinct(StringComparer.Ordinal).Count() != outputs.Length)
        {
            failureReason = "recipe-execution-exact-output-state-incomplete";
            return false;
        }

        try
        {
            ExactCommittedUnit[] units = staging.Units
                .OrderBy(value => value.CommitId, StringComparer.Ordinal)
                .ToArray();
            foreach (ProductionResolvedOutputSaveData output in outputs)
            {
                ExactCommittedUnit[] lineUnits = units
                    .Where(value => string.Equals(
                        value.OutputLineId,
                        output.outputLineId,
                        StringComparison.Ordinal))
                    .ToArray();
                if (lineUnits.Length != output.amount
                    || lineUnits.Any(value => !string.Equals(
                        value.ItemId,
                        output.itemId,
                        StringComparison.Ordinal)
                        || !string.Equals(
                            value.CapabilityFingerprint,
                            output.outputCapabilityFingerprint,
                            StringComparison.Ordinal))
                    || lineUnits.Sum(value => value.MassGrams)
                        != output.committedMassGrams)
                {
                    failureReason =
                        "recipe-execution-exact-output-aggregate-mismatch";
                    return false;
                }
            }
            if (units.Any(unit => !outputs.Any(output => string.Equals(
                    output.outputLineId,
                    unit.OutputLineId,
                    StringComparison.Ordinal))))
            {
                failureReason = "recipe-execution-exact-orphan-unit";
                return false;
            }

            string[] commitIds = units
                .Select(value => value.CommitId)
                .ToArray();
            string aggregateBatchCommitId = BuildExactBatchCommitId(
                billId,
                cycleSequence,
                commitIds);
            string outcomeFingerprint = BuildExactAggregateDigest(
                "production-recipe-exact-outcome@1",
                billId,
                cycleSequence,
                units,
                value => value.OutcomeFingerprint);
            string plannedOutputFingerprint = BuildExactAggregateDigest(
                "production-recipe-exact-planned-output@1",
                billId,
                cycleSequence,
                units,
                value => value.PlannedOutputFingerprint);
            ProductionRecipeExecutionOutputLineReceipt[] lineReceipts = outputs
                .Select(output =>
                {
                    ExactCommittedUnit[] lineUnits = units
                        .Where(value => string.Equals(
                            value.OutputLineId,
                            output.outputLineId,
                            StringComparison.Ordinal))
                        .OrderBy(value => value.CommitId, StringComparer.Ordinal)
                        .ToArray();
                    return new ProductionRecipeExecutionOutputLineReceipt(
                        output.outputLineId,
                        output.itemId,
                        lineUnits.Sum(value => value.Quantity),
                        lineUnits.Sum(value => value.MassGrams),
                        output.outputCapabilityFingerprint,
                        lineUnits.Select(value => value.CommitId).ToArray());
                })
                .ToArray();
            ProductionRecipeExecutionPhysicalSliceReceipt[] slices = units
                .SelectMany(unit => unit.Stacks.Select(stack =>
                    new ProductionRecipeExecutionPhysicalSliceReceipt(
                        unit.OutputLineId,
                        unit.ItemId,
                        stack.StackId,
                        stack.Quantity,
                        stack.MassGrams,
                        unit.CommitId)))
                .ToArray();
            ProductionRecipeExecutionReceipt candidate = new(
                actionId,
                correlation,
                wipInputCommitId,
                wipInputQuantity,
                wipInputMassGrams,
                ProductionRecipeExecutionPublicationKind.ExactCapabilityUnits,
                aggregateBatchCommitId,
                commitIds,
                outcomeFingerprint,
                plannedOutputFingerprint,
                lineReceipts,
                slices);
            receiptByAction.Add(actionId, candidate);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "recipe-execution-exact-finalize-invalid:"
                + exception.Message;
            return false;
        }
    }

    private bool TryRequireCorrelation(
        string actionId,
        string recipeId,
        BuildingInstanceId facilityId,
        out ProductionRecipeExecutionCorrelation correlation,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!correlationByAction.TryGetValue(actionId, out correlation)
            || !string.Equals(
                correlation.RecipeId,
                recipeId,
                StringComparison.Ordinal)
            || !correlation.FacilityId.Equals(facilityId))
        {
            failureReason = "recipe-execution-exact-owner-mismatch";
            return false;
        }
        return true;
    }

    private static bool TryCreateExactUnit(
        ProductionResolvedOutputSaveData output,
        ProductionCommittedOutputSnapshot committedOutput,
        ProductionRecipeExecutionCorrelation correlation,
        out ExactCommittedUnit unit,
        out string failureReason)
    {
        unit = null;
        failureReason = string.Empty;
        if (output == null
            || committedOutput == null
            || !output.pendingCommitApplied
            || string.IsNullOrEmpty(output.pendingCommitId)
            || !string.Equals(
                output.pendingCommitId,
                committedOutput.CommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                committedOutput.FacilityInstanceId,
                correlation.FacilityId.Value,
                StringComparison.Ordinal)
            || committedOutput.AcknowledgedAtCapture
            || committedOutput.ExactMassGrams <= 0L
            || !IsDigest(committedOutput.OutcomeFingerprint)
            || !IsDigest(committedOutput.PlannedOutputFingerprint)
            || !IsDigest(output.outputCapabilityFingerprint))
        {
            failureReason = "recipe-execution-exact-unit-invalid";
            return false;
        }
        ProductionCommittedOutputStackSnapshot[] stacks = committedOutput.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (stacks.Length == 0
            || stacks.Any(value => !string.Equals(
                    value.OutputLineId,
                    output.outputLineId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    value.ItemId,
                    output.itemId,
                    StringComparison.Ordinal)
                || !IsCanonical(value.StackId)
                || value.Quantity <= 0
                || value.MassGrams <= 0L)
            || stacks.Select(value => value.StackId)
                .Distinct(StringComparer.Ordinal).Count() != stacks.Length
            || stacks.Sum(value => value.MassGrams)
                != committedOutput.ExactMassGrams)
        {
            failureReason = "recipe-execution-exact-unit-stack-invalid";
            return false;
        }
        unit = new ExactCommittedUnit(
            output.outputLineId,
            output.itemId,
            output.outputCapabilityFingerprint,
            committedOutput.CommitId,
            committedOutput.OutcomeFingerprint,
            committedOutput.PlannedOutputFingerprint,
            stacks);
        return true;
    }

    private static string BuildExactBatchCommitId(
        ProductionBillId billId,
        int cycleSequence,
        IReadOnlyList<string> commitIds)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-recipe-exact-batch-identity@1");
        digest.Append(billId.Value);
        digest.Append(cycleSequence);
        digest.Append(commitIds.Count);
        foreach (string commitId in commitIds)
            digest.Append(commitId);
        return "production-output-batch-exact:" + digest.ComputeSha256();
    }

    private static string BuildExactAggregateDigest(
        string schema,
        ProductionBillId billId,
        int cycleSequence,
        IReadOnlyList<ExactCommittedUnit> units,
        Func<ExactCommittedUnit, string> selector)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(schema);
        digest.Append(billId.Value);
        digest.Append(cycleSequence);
        digest.Append(units.Count);
        foreach (ExactCommittedUnit unit in units)
        {
            digest.Append(unit.CommitId);
            digest.Append(selector(unit));
        }
        return digest.ComputeSha256();
    }

    private sealed class ExactCycleStaging
    {
        private readonly Dictionary<string, ExactCommittedUnit> unitsByCommit =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> stackIds = new(StringComparer.Ordinal);

        internal ExactCycleStaging(string correlationDigest) =>
            CorrelationDigest = correlationDigest;

        internal string CorrelationDigest { get; }
        internal IReadOnlyCollection<ExactCommittedUnit> Units =>
            unitsByCommit.Values;

        internal bool TryAdd(
            ExactCommittedUnit unit,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (unit == null)
            {
                failureReason = "recipe-execution-exact-unit-invalid";
                return false;
            }
            if (unitsByCommit.TryGetValue(
                    unit.CommitId,
                    out ExactCommittedUnit existing))
            {
                if (string.Equals(
                        existing.SourceDigest,
                        unit.SourceDigest,
                        StringComparison.Ordinal))
                {
                    return true;
                }
                failureReason = "recipe-execution-exact-unit-replay-conflict";
                return false;
            }
            if (unit.Stacks.Any(value => stackIds.Contains(value.StackId)))
            {
                failureReason = "recipe-execution-exact-stack-reused";
                return false;
            }
            unitsByCommit.Add(unit.CommitId, unit);
            foreach (ProductionCommittedOutputStackSnapshot stack in unit.Stacks)
                stackIds.Add(stack.StackId);
            return true;
        }
    }

    private sealed class ExactCommittedUnit
    {
        internal ExactCommittedUnit(
            string outputLineId,
            string itemId,
            string capabilityFingerprint,
            string commitId,
            string outcomeFingerprint,
            string plannedOutputFingerprint,
            IReadOnlyList<ProductionCommittedOutputStackSnapshot> stacks)
        {
            OutputLineId = outputLineId;
            ItemId = itemId;
            CapabilityFingerprint = capabilityFingerprint;
            CommitId = commitId;
            OutcomeFingerprint = outcomeFingerprint;
            PlannedOutputFingerprint = plannedOutputFingerprint;
            Stacks = Array.AsReadOnly(stacks.ToArray());
            Quantity = Stacks.Sum(value => value.Quantity);
            MassGrams = Stacks.Sum(value => value.MassGrams);
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append("production-recipe-exact-unit@1");
            digest.Append(OutputLineId);
            digest.Append(ItemId);
            digest.Append(CapabilityFingerprint);
            digest.Append(CommitId);
            digest.Append(OutcomeFingerprint);
            digest.Append(PlannedOutputFingerprint);
            digest.Append(Stacks.Count);
            foreach (ProductionCommittedOutputStackSnapshot stack in Stacks)
            {
                digest.Append(stack.OutputLineId);
                digest.Append(stack.StackId);
                digest.Append(stack.ItemId);
                digest.Append(stack.Quantity);
                digest.Append(stack.MassGrams);
                digest.Append(stack.ComponentSignature);
                digest.Append(stack.ItemInstanceId);
            }
            SourceDigest = digest.ComputeSha256();
        }

        internal string OutputLineId { get; }
        internal string ItemId { get; }
        internal string CapabilityFingerprint { get; }
        internal string CommitId { get; }
        internal string OutcomeFingerprint { get; }
        internal string PlannedOutputFingerprint { get; }
        internal IReadOnlyList<ProductionCommittedOutputStackSnapshot> Stacks
            { get; }
        internal int Quantity { get; }
        internal long MassGrams { get; }
        internal string SourceDigest { get; }
    }

    private static string CycleKey(ProductionBillId billId, int cycleSequence) =>
        billId.Value + ":" + cycleSequence.ToString(
            "D8",
            CultureInfo.InvariantCulture);

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsWhiteSpace);

    private static bool IsDigest(string value) => value != null
        && value.Length == 64
        && value.All(character => (character >= '0' && character <= '9')
            || (character >= 'a' && character <= 'f'));
}
