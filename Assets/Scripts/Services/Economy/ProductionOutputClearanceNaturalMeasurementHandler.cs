using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionOutputClearanceExecutionOutputSliceSnapshot
{
    public ProductionOutputClearanceExecutionOutputSliceSnapshot(
        string outputLineId,
        string itemId,
        string itemInstanceId,
        string stackId,
        int quantity,
        long massGrams,
        string capabilityFingerprint)
    {
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            outputLineId,
            nameof(outputLineId));
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            itemId,
            nameof(itemId));
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            stackId,
            nameof(stackId));
        if (!string.IsNullOrEmpty(itemInstanceId))
        {
            ProductionOutputClearanceProfileObservation.RequireCanonical(
                itemInstanceId,
                nameof(itemInstanceId));
        }
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (massGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(massGrams));
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                capabilityFingerprint))
        {
            throw new ArgumentException(
                "An output slice requires a capability SHA-256 fingerprint.",
                nameof(capabilityFingerprint));
        }

        OutputLineId = outputLineId;
        ItemId = itemId;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        StackId = stackId;
        Quantity = quantity;
        MassGrams = massGrams;
        CapabilityFingerprint = capabilityFingerprint;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-execution-output-slice@1");
        digest.Append(OutputLineId);
        digest.Append(ItemId);
        digest.Append(ItemInstanceId);
        digest.Append(StackId);
        digest.Append(Quantity);
        digest.Append(MassGrams);
        digest.Append(CapabilityFingerprint);
        SourceDigest = digest.ComputeSha256();
    }

    public string OutputLineId { get; }
    public string ItemId { get; }
    public string ItemInstanceId { get; }
    public string StackId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public string CapabilityFingerprint { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceExecutionReceiptSnapshot
{
    public ProductionOutputClearanceExecutionReceiptSnapshot(
        ProductionOutputClearanceExecutableDescriptor descriptor,
        string actionId,
        string runtimeFacilityId,
        string operationId,
        string batchCommitId,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        string resolvedOutputVectorDigest,
        long actualBatchMassGrams,
        IReadOnlyList<
            ProductionOutputClearanceExecutionOutputSliceSnapshot> outputs,
        string runtimeReceiptDigest,
        string handlerId,
        int handlerVersion,
        IReadOnlyList<string> routeBatchCommitIds = null)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        RequireCanonical(actionId, nameof(actionId));
        RequireCanonical(runtimeFacilityId, nameof(runtimeFacilityId));
        RequireCanonical(operationId, nameof(operationId));
        RequireCanonical(batchCommitId, nameof(batchCommitId));
        RequireCanonical(handlerId, nameof(handlerId));
        RequireDigest(outcomeFingerprint, nameof(outcomeFingerprint));
        RequireDigest(
            plannedOutputFingerprint,
            nameof(plannedOutputFingerprint));
        RequireDigest(resolvedOutputVectorDigest, nameof(resolvedOutputVectorDigest));
        RequireDigest(runtimeReceiptDigest, nameof(runtimeReceiptDigest));
        if (actualBatchMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(actualBatchMassGrams));
        if (handlerVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(handlerVersion));

        ProductionOutputClearanceExecutionOutputSliceSnapshot[] orderedOutputs =
            (outputs ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value?.StackId, StringComparer.Ordinal)
            .ToArray();
        if (orderedOutputs.Length == 0
            || orderedOutputs.Any(value => value == null)
            || orderedOutputs.Select(value => value.StackId)
                .Distinct(StringComparer.Ordinal).Count() != orderedOutputs.Length
            || orderedOutputs.Sum(value => value.MassGrams)
                != actualBatchMassGrams)
        {
            throw new InvalidOperationException(
                "Execution receipt outputs must be exact, unique, and mass-conserving.");
        }

        ActionId = actionId;
        RuntimeFacilityId = runtimeFacilityId;
        OperationId = operationId;
        BatchCommitId = batchCommitId;
        OutcomeFingerprint = outcomeFingerprint;
        PlannedOutputFingerprint = plannedOutputFingerprint;
        ResolvedOutputVectorDigest = resolvedOutputVectorDigest;
        ActualBatchMassGrams = actualBatchMassGrams;
        Outputs = Array.AsReadOnly(orderedOutputs);
        string[] orderedRouteBatchCommitIds = (routeBatchCommitIds
                ?? new[] { batchCommitId })
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (orderedRouteBatchCommitIds.Length == 0
            || orderedRouteBatchCommitIds.Any(value =>
                string.IsNullOrWhiteSpace(value)
                || !string.Equals(
                    value,
                    value.Trim(),
                    StringComparison.Ordinal)
                || value.Any(char.IsWhiteSpace))
            || orderedRouteBatchCommitIds.Distinct(StringComparer.Ordinal)
                .Count() != orderedRouteBatchCommitIds.Length)
        {
            throw new ArgumentException(
                "Execution receipt route batch IDs must be exact and canonical.",
                nameof(routeBatchCommitIds));
        }
        RouteBatchCommitIds = Array.AsReadOnly(orderedRouteBatchCommitIds);
        RuntimeReceiptDigest = runtimeReceiptDigest;
        HandlerId = handlerId;
        HandlerVersion = handlerVersion;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-execution-receipt@2");
        digest.Append(Descriptor.SourceDigest);
        digest.Append(Descriptor.Payload.PayloadKind);
        digest.Append(ActionId);
        digest.Append(RuntimeFacilityId);
        digest.Append(OperationId);
        digest.Append(BatchCommitId);
        digest.Append(OutcomeFingerprint);
        digest.Append(PlannedOutputFingerprint);
        digest.Append(ResolvedOutputVectorDigest);
        digest.Append(ActualBatchMassGrams);
        digest.Append(RouteBatchCommitIds.Count);
        foreach (string commitId in RouteBatchCommitIds)
            digest.Append(commitId);
        digest.Append(Outputs.Count);
        foreach (ProductionOutputClearanceExecutionOutputSliceSnapshot output in
                 Outputs)
            digest.Append(output.SourceDigest);
        digest.Append(RuntimeReceiptDigest);
        digest.Append(HandlerId);
        digest.Append(HandlerVersion);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceExecutableDescriptor Descriptor { get; }
    public string ActionId { get; }
    public string RuntimeFacilityId { get; }
    public string OperationId { get; }
    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public string PlannedOutputFingerprint { get; }
    public string ResolvedOutputVectorDigest { get; }
    public long ActualBatchMassGrams { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutionOutputSliceSnapshot>
        Outputs { get; }
    public IReadOnlyList<string> RouteBatchCommitIds { get; }
    public string RuntimeReceiptDigest { get; }
    public string HandlerId { get; }
    public int HandlerVersion { get; }
    public string SourceDigest { get; }

    private static void RequireCanonical(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A canonical execution-receipt identifier is required.", name);
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!ProductionOutputClearanceProfileObservation
                .IsLowercaseSha256(value))
        {
            throw new ArgumentException(
                "A lowercase SHA-256 digest is required.", name);
        }
    }
}

public interface IProductionOutputClearanceNaturalMeasurementHandler
{
    string HandlerId { get; }
    int ContractVersion { get; }
    string PayloadKind { get; }

    bool TryCaptureCompleted(
        ProductionOutputClearanceExecutableDescriptor descriptor,
        string actionId,
        out ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason);

    bool TryAcknowledgeAccepted(
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason);
}

public sealed class ProductionOutputClearanceNaturalMeasurementHandlerRegistry
{
    private readonly IReadOnlyDictionary<string,
        IProductionOutputClearanceNaturalMeasurementHandler> handlers;

    public ProductionOutputClearanceNaturalMeasurementHandlerRegistry(
        IEnumerable<IProductionOutputClearanceNaturalMeasurementHandler> handlers)
    {
        IProductionOutputClearanceNaturalMeasurementHandler[] ordered =
            (handlers ?? throw new ArgumentNullException(nameof(handlers)))
            .OrderBy(value => value?.PayloadKind, StringComparer.Ordinal)
            .ThenBy(value => value?.HandlerId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.PayloadKind)
                || !string.Equals(
                    value.PayloadKind,
                    value.PayloadKind.Trim(),
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(value.HandlerId)
                || !string.Equals(
                    value.HandlerId,
                    value.HandlerId.Trim(),
                    StringComparison.Ordinal)
                || value.ContractVersion <= 0)
            || ordered.Select(value => value.PayloadKind)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Natural measurement handlers must have one canonical owner per payload kind.");
        }

        this.handlers = ordered.ToDictionary(
            value => value.PayloadKind,
            value => value,
            StringComparer.Ordinal);
        PayloadKinds = Array.AsReadOnly(ordered
            .Select(value => value.PayloadKind)
            .ToArray());
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-handler-registry@1");
        digest.Append(ordered.Length);
        foreach (IProductionOutputClearanceNaturalMeasurementHandler value in ordered)
        {
            digest.Append(value.PayloadKind);
            digest.Append(value.HandlerId);
            digest.Append(value.ContractVersion);
        }
        RegistryFingerprint = digest.ComputeSha256();
    }

    public string RegistryFingerprint { get; }
    public IReadOnlyList<string> PayloadKinds { get; }

    public void RequireExactCoverage(IEnumerable<string> expectedPayloadKinds)
    {
        string[] expected = (expectedPayloadKinds
                ?? throw new ArgumentNullException(nameof(expectedPayloadKinds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actual = PayloadKinds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (expected.Length == 0
            || expected.Any(value => string.IsNullOrWhiteSpace(value))
            || expected.Distinct(StringComparer.Ordinal).Count()
                != expected.Length
            || !actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Natural measurement handler payload coverage differs from the "
                + "current executable portfolio. expected="
                + string.Join("|", expected) + ";actual="
                + string.Join("|", actual));
        }
    }

    public bool TryCaptureCompleted(
        ProductionOutputClearanceExecutableDescriptor descriptor,
        string actionId,
        out ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason)
    {
        receipt = null;
        failureReason = string.Empty;
        if (descriptor?.Payload == null
            || !handlers.TryGetValue(
                descriptor.Payload.PayloadKind,
                out IProductionOutputClearanceNaturalMeasurementHandler handler))
        {
            failureReason = "natural-measurement-handler-unregistered";
            return false;
        }
        return handler.TryCaptureCompleted(
            descriptor,
            actionId,
            out receipt,
            out failureReason);
    }

    public bool TryAcknowledgeAccepted(
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (receipt == null
            || !handlers.TryGetValue(
                receipt.Descriptor.Payload.PayloadKind,
                out IProductionOutputClearanceNaturalMeasurementHandler handler)
            || !string.Equals(
                handler.HandlerId,
                receipt.HandlerId,
                StringComparison.Ordinal)
            || handler.ContractVersion != receipt.HandlerVersion)
        {
            failureReason = "natural-measurement-handler-receipt-owner-mismatch";
            return false;
        }
        return handler.TryAcknowledgeAccepted(receipt, out failureReason);
    }
}

public sealed class ProductionOutputClearanceCropHarvestNaturalMeasurementHandler :
    IProductionOutputClearanceNaturalMeasurementHandler
{
    public const string Id = "natural-measurement-handler:crop-harvest";
    public const int Version = 1;

    private readonly ICropPlanExecutionReceiptQuery receipts;
    private readonly ICropCycleExecutionCorrelationCommand commands;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;

    public ProductionOutputClearanceCropHarvestNaturalMeasurementHandler(
        ICropPlanExecutionReceiptQuery receipts,
        ICropCycleExecutionCorrelationCommand commands,
        IFacilityBufferPlannedOutputPublicationService publication)
    {
        this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
    }

    public string HandlerId => Id;
    public int ContractVersion => Version;
    public string PayloadKind => "crop-harvest";

    public bool TryCaptureCompleted(
        ProductionOutputClearanceExecutableDescriptor descriptor,
        string actionId,
        out ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason)
    {
        receipt = null;
        failureReason = string.Empty;
        if (descriptor?.Payload is not
                ProductionOutputClearanceCropHarvestExecutablePayload payload)
        {
            failureReason = "crop-natural-handler-payload-mismatch";
            return false;
        }
        if (!receipts.TryCaptureExecutionReceipt(
                actionId,
                out CropPlanExecutionReceipt runtime)
            || runtime == null)
        {
            failureReason = "crop-natural-handler-receipt-not-found";
            return false;
        }
        if (!runtime.Succeeded
            || !runtime.ExplicitCorrelation
            || !string.Equals(runtime.CropId, payload.CropId, StringComparison.Ordinal)
            || runtime.Indoor != payload.Indoor)
        {
            failureReason = "crop-natural-handler-terminal-owner-mismatch";
            return false;
        }

        KeyValuePair<string, int>[] actualInputs = runtime.Inputs
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(group => new KeyValuePair<string, int>(
                group.Key,
                checked(group.Sum(value => value.Quantity))))
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        KeyValuePair<string, int>[] expectedInputs = payload.Inputs
            .Select(value => new KeyValuePair<string, int>(
                value.ItemId,
                value.Quantity))
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        if (runtime.Inputs.Any(value => string.IsNullOrWhiteSpace(value.SourceStackId)
                || value.Quantity <= 0)
            || !actualInputs.SequenceEqual(expectedInputs))
        {
            failureReason = "crop-natural-handler-input-vector-mismatch";
            return false;
        }

        ProductionOutputClearanceExecutableOutput[] expectedOutputs = payload.Outputs
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        CropPlanExecutionOutputWitness[] actualOutputs = runtime.Outputs
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        bool outputsExact = actualOutputs.Length == expectedOutputs.Length;
        for (int index = 0; outputsExact && index < expectedOutputs.Length; index++)
        {
            ProductionOutputClearanceExecutableOutput expected = expectedOutputs[index];
            CropPlanExecutionOutputWitness actual = actualOutputs[index];
            outputsExact = string.Equals(
                    actual.OutputLineId,
                    expected.OutputLineId,
                    StringComparison.Ordinal)
                && string.Equals(actual.ItemId, expected.ItemId, StringComparison.Ordinal)
                && actual.Quantity == expected.Quantity
                && actual.MassGrams == expected.MassGrams
                && string.Equals(
                    actual.CapabilityFingerprint,
                    expected.Descriptor.Fingerprint,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(actual.StackId);
        }
        if (!outputsExact
            || runtime.OutputMassGrams
                != descriptor.Plan.Winner.Source.MaximumSingleCompletionMassGrams
            || runtime.OutputMassGrams != expectedOutputs.Sum(value => value.MassGrams))
        {
            string expectedVector = string.Join(",", expectedOutputs.Select(
                value => value.OutputLineId + "=" + value.Quantity + "/"
                    + value.MassGrams));
            string actualVector = string.Join(",", actualOutputs.Select(
                value => value.OutputLineId + "=" + value.Quantity + "/"
                    + value.MassGrams));
            failureReason =
                "crop-natural-handler-selected-output-vector-mismatch:expected="
                + expectedVector
                + ";actual=" + actualVector
                + ";runtimeMass=" + runtime.OutputMassGrams
                + ";winnerMass=" + descriptor.Plan.Winner.Source
                    .MaximumSingleCompletionMassGrams;
            return false;
        }

        if (!publication.TryCaptureBatch(
                runtime.OutputBatchCommitId,
                allowAcknowledged: true,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                out bool batchAcknowledged,
                out FacilityBufferPlannedOutputPublicationFailureCode _,
                out string _)
            || !batchAcknowledged
            || batch == null
            || batch.TotalMassGrams != runtime.OutputMassGrams
            || batch.TotalQuantity != actualOutputs.Sum(value => value.Quantity)
            || !string.Equals(
                batch.OutcomeFingerprint,
                runtime.OutputOutcomeFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                batch.PlannedOutputFingerprint,
                runtime.PlannedOutputFingerprint,
                StringComparison.Ordinal))
        {
            failureReason = "crop-natural-handler-physical-batch-join-mismatch";
            return false;
        }
        FacilityBufferPlannedOutputRestoreStackSnapshot[] physicalOutputs = batch
            .Stacks
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value?.StackId, StringComparer.Ordinal)
            .ToArray();
        bool physicalExact = physicalOutputs.Length == actualOutputs.Length;
        for (int index = 0;
             physicalExact && index < actualOutputs.Length;
             index++)
        {
            CropPlanExecutionOutputWitness actual = actualOutputs[index];
            FacilityBufferPlannedOutputRestoreStackSnapshot physical =
                physicalOutputs[index];
            physicalExact = physical != null
                && string.Equals(
                    physical.BatchCommitId,
                    runtime.OutputBatchCommitId,
                    StringComparison.Ordinal)
                && string.Equals(
                    physical.OutputLineId,
                    actual.OutputLineId,
                    StringComparison.Ordinal)
                && string.Equals(
                    physical.ItemId,
                    actual.ItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    physical.ItemInstanceId,
                    actual.ItemInstanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    physical.StackId,
                    actual.StackId,
                    StringComparison.Ordinal)
                && physical.Quantity == actual.Quantity
                && physical.MassGrams == actual.MassGrams;
        }
        if (!physicalExact)
        {
            failureReason = "crop-natural-handler-physical-slice-join-mismatch";
            return false;
        }

        CanonicalSemanticDigestBuilder vector = new();
        vector.Append("production-output-clearance-resolved-output-vector@1");
        vector.Append(descriptor.SourceDigest);
        vector.Append(runtime.OutputVectorDigest);
        vector.Append(actualOutputs.Length);
        foreach (CropPlanExecutionOutputWitness output in actualOutputs)
        {
            vector.Append(output.OutputLineId);
            vector.Append(output.ItemId);
            vector.Append(output.ItemInstanceId);
            vector.Append(output.StackId);
            vector.Append(output.Quantity);
            vector.Append(output.MassGrams);
            vector.Append(output.CapabilityFingerprint);
        }

        receipt = new ProductionOutputClearanceExecutionReceiptSnapshot(
            descriptor,
            runtime.ActionId,
            runtime.PlotId,
            runtime.HarvestOperationId,
            runtime.OutputBatchCommitId,
            runtime.OutputOutcomeFingerprint,
            runtime.PlannedOutputFingerprint,
            vector.ComputeSha256(),
            runtime.OutputMassGrams,
            actualOutputs.Select((output, index) =>
                    new ProductionOutputClearanceExecutionOutputSliceSnapshot(
                        output.OutputLineId,
                        output.ItemId,
                        output.ItemInstanceId,
                        physicalOutputs[index].StackId,
                        output.Quantity,
                        output.MassGrams,
                        output.CapabilityFingerprint))
                .ToArray(),
            runtime.RuntimeReceiptDigest,
            HandlerId,
            ContractVersion);
        return true;
    }

    public bool TryAcknowledgeAccepted(
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (receipt == null
            || !string.Equals(receipt.HandlerId, HandlerId, StringComparison.Ordinal)
            || receipt.HandlerVersion != ContractVersion
            || receipt.Descriptor.Payload is not
                ProductionOutputClearanceCropHarvestExecutablePayload)
        {
            failureReason = "crop-natural-handler-acknowledgement-owner-mismatch";
            return false;
        }
        return commands.TryAcknowledgeExecutionReceipt(
            receipt.ActionId,
            receipt.RuntimeReceiptDigest,
            out failureReason);
    }
}

public sealed class ProductionOutputClearanceRecipeNaturalMeasurementHandler :
    IProductionOutputClearanceNaturalMeasurementHandler
{
    public const string Id = "natural-measurement-handler:recipe";
    public const int Version = 1;

    private readonly IProductionRecipeExecutionReceiptQuery receipts;
    private readonly IProductionRecipeExecutionCorrelationCommand commands;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;

    public ProductionOutputClearanceRecipeNaturalMeasurementHandler(
        IProductionRecipeExecutionReceiptQuery receipts,
        IProductionRecipeExecutionCorrelationCommand commands,
        IFacilityBufferPlannedOutputPublicationService publication)
    {
        this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
    }

    public string HandlerId => Id;
    public int ContractVersion => Version;
    public string PayloadKind => "recipe";

    public bool TryCaptureCompleted(
        ProductionOutputClearanceExecutableDescriptor descriptor,
        string actionId,
        out ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason)
    {
        receipt = null;
        failureReason = string.Empty;
        if (descriptor?.Payload is not
                ProductionOutputClearanceRecipeExecutablePayload payload)
        {
            failureReason = "recipe-natural-handler-payload-mismatch";
            return false;
        }
        if (!receipts.TryCaptureExecutionReceipt(
                actionId,
                out ProductionRecipeExecutionReceipt runtime)
            || runtime == null)
        {
            failureReason = "recipe-natural-handler-receipt-not-found";
            return false;
        }
        if (!string.Equals(
                runtime.ActionId,
                actionId,
                StringComparison.Ordinal)
            || !string.Equals(
                runtime.Correlation.RecipeId,
                payload.RecipeId,
                StringComparison.Ordinal)
            || runtime.Correlation.CycleSequence <= 0
            || !runtime.Correlation.BillId.IsValid
            || !runtime.Correlation.FacilityId.IsValid)
        {
            failureReason = "recipe-natural-handler-terminal-owner-mismatch";
            return false;
        }

        int expectedInputQuantity = checked(
            payload.Inputs.Sum(value => value.Quantity)
            + payload.Supports
                .Where(value => value.RequiresFuel)
                .GroupBy(value => value.SupportId, StringComparer.Ordinal)
                .Sum(group => Math.Max(
                    1,
                    group.Max(value => value.FuelPerCycle))));
        bool inputExact = expectedInputQuantity == 0
            ? runtime.WipInputQuantity == 0
                && runtime.WipInputMassGrams == 0L
                && runtime.WipInputCommitId.Length == 0
            : runtime.WipInputQuantity == expectedInputQuantity
                && runtime.WipInputMassGrams > 0L
                && runtime.WipInputCommitId.Length > 0;
        if (!inputExact)
        {
            failureReason = "recipe-natural-handler-wip-input-mismatch";
            return false;
        }

        ProductionOutputClearanceExecutableOutput[] expectedOutputs =
            payload.Outputs
                .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
                .ToArray();
        ProductionRecipeExecutionOutputLineReceipt[] actualOutputs =
            runtime.Outputs
                .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
                .ToArray();
        Dictionary<string, ProductionOutputClearanceExecutableOutput>
            expectedByLine = expectedOutputs.ToDictionary(
                value => value.OutputLineId,
                value => value,
                StringComparer.Ordinal);
        bool outputsExact = actualOutputs.Length > 0
            && actualOutputs.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() == actualOutputs.Length;
        for (int index = 0; outputsExact && index < actualOutputs.Length; index++)
        {
            ProductionRecipeExecutionOutputLineReceipt actual =
                actualOutputs[index];
            outputsExact = expectedByLine.TryGetValue(
                    actual.OutputLineId,
                    out ProductionOutputClearanceExecutableOutput expected)
                && string.Equals(
                    actual.ItemId,
                    expected.ItemId,
                    StringComparison.Ordinal)
                && actual.Quantity > 0
                && actual.Quantity <= expected.Quantity
                && actual.MassGrams > 0L
                && actual.MassGrams <= expected.MassGrams
                && (decimal)actual.MassGrams * expected.Quantity
                    == (decimal)expected.MassGrams * actual.Quantity
                && string.Equals(
                    actual.CapabilityFingerprint,
                    expected.Descriptor.Fingerprint,
                    StringComparison.Ordinal);
        }
        if (!outputsExact
            || runtime.ActualBatchMassGrams <= 0L
            || runtime.ActualBatchMassGrams
                > descriptor.Plan.Winner.Source.MaximumSingleCompletionMassGrams
            || runtime.ActualBatchMassGrams
                != actualOutputs.Sum(value => value.MassGrams))
        {
            failureReason =
                "recipe-natural-handler-selected-output-vector-mismatch";
            return false;
        }

        List<FacilityBufferPlannedOutputRestoreBatchSnapshot> joinedBatches =
            new();
        foreach (string routeBatchCommitId in runtime.RouteBatchCommitIds)
        {
            bool captured = publication.TryCaptureBatch(
                    routeBatchCommitId,
                    allowAcknowledged: true,
                    out FacilityBufferPlannedOutputRestoreBatchSnapshot joined,
                    out bool joinedAcknowledged,
                    out FacilityBufferPlannedOutputPublicationFailureCode joinCode,
                    out string joinDetail);
            if (!captured || !joinedAcknowledged
                || joined == null
                || !string.Equals(
                    joined.BatchCommitId,
                    routeBatchCommitId,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "recipe-natural-handler-physical-batch-join-mismatch"
                    + $";commit={routeBatchCommitId}"
                    + $";captured={captured}"
                    + $";acknowledged={joinedAcknowledged}"
                    + $";joined={(joined != null)}"
                    + $";joinedCommit={joined?.BatchCommitId ?? "none"}"
                    + $";code={joinCode}"
                    + $";detail={joinDetail ?? "none"}";
                return false;
            }
            joinedBatches.Add(joined);
        }
        bool aggregateFingerprintsExact = runtime.PublicationKind ==
                ProductionRecipeExecutionPublicationKind.PreparedBatch
            ? joinedBatches.Count == 1
                && string.Equals(
                    joinedBatches[0].OutcomeFingerprint,
                    runtime.OutcomeFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    joinedBatches[0].PlannedOutputFingerprint,
                    runtime.PlannedOutputFingerprint,
                    StringComparison.Ordinal)
            : string.Equals(
                    CaptureExactAggregateDigest(
                        "production-recipe-exact-outcome@1",
                        runtime,
                        joinedBatches,
                        value => value.OutcomeFingerprint),
                    runtime.OutcomeFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    CaptureExactAggregateDigest(
                        "production-recipe-exact-planned-output@1",
                        runtime,
                        joinedBatches,
                        value => value.PlannedOutputFingerprint),
                    runtime.PlannedOutputFingerprint,
                    StringComparison.Ordinal);
        if (joinedBatches.Sum(value => value.TotalMassGrams)
                != runtime.ActualBatchMassGrams
            || joinedBatches.Sum(value => value.TotalQuantity)
                != runtime.PhysicalSlices.Sum(value => value.Quantity)
            || !aggregateFingerprintsExact)
        {
            failureReason = "recipe-natural-handler-physical-batch-join-mismatch"
                + $";joinedMass={joinedBatches.Sum(value => value.TotalMassGrams)}"
                + $";runtimeMass={runtime.ActualBatchMassGrams}"
                + $";joinedQuantity={joinedBatches.Sum(value => value.TotalQuantity)}"
                + $";runtimeQuantity={runtime.PhysicalSlices.Sum(value => value.Quantity)}"
                + $";fingerprints={aggregateFingerprintsExact}";
            return false;
        }

        FacilityBufferPlannedOutputRestoreStackSnapshot[] physicalOutputs =
            joinedBatches.SelectMany(value => value.Stacks)
                .OrderBy(value => value?.StackId, StringComparer.Ordinal)
                .ToArray();
        ProductionRecipeExecutionPhysicalSliceReceipt[] runtimeSlices =
            runtime.PhysicalSlices
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
        bool physicalExact = physicalOutputs.Length == runtimeSlices.Length;
        for (int index = 0; physicalExact && index < runtimeSlices.Length; index++)
        {
            ProductionRecipeExecutionPhysicalSliceReceipt actual =
                runtimeSlices[index];
            FacilityBufferPlannedOutputRestoreStackSnapshot physical =
                physicalOutputs[index];
            physicalExact = physical != null
                && string.Equals(
                    physical.BatchCommitId,
                    runtime.PublicationKind ==
                        ProductionRecipeExecutionPublicationKind.PreparedBatch
                        ? runtime.BatchCommitId
                        : actual.CommitId,
                    StringComparison.Ordinal)
                // Exact-capability outputs may split one authored output line
                // into physical unit lines (":unit:NNNN").  Stack and commit
                // custody are the physical join keys; OutputLineId on the
                // execution receipt deliberately remains the authored semantic
                // line.  Prepared batches keep identical physical/semantic
                // lines and retain that stricter check.
                && (runtime.PublicationKind ==
                        ProductionRecipeExecutionPublicationKind.ExactCapabilityUnits
                    || string.Equals(
                        physical.OutputLineId,
                        actual.OutputLineId,
                        StringComparison.Ordinal))
                && string.Equals(
                    physical.ItemId,
                    actual.ItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    physical.StackId,
                    actual.StackId,
                    StringComparison.Ordinal)
                && physical.Quantity == actual.Quantity
                && physical.MassGrams == actual.MassGrams;
        }
        if (!physicalExact)
        {
            failureReason = "recipe-natural-handler-physical-slice-join-mismatch";
            return false;
        }

        Dictionary<string, string> capabilityByLine = actualOutputs
            .ToDictionary(
                value => value.OutputLineId,
                value => value.CapabilityFingerprint,
                StringComparer.Ordinal);
        CanonicalSemanticDigestBuilder vector = new();
        vector.Append("production-output-clearance-resolved-output-vector@1");
        vector.Append(descriptor.SourceDigest);
        vector.Append(runtime.ResolvedOutputVectorDigest);
        vector.Append(physicalOutputs.Length);
        for (int index = 0; index < physicalOutputs.Length; index++)
        {
            FacilityBufferPlannedOutputRestoreStackSnapshot output =
                physicalOutputs[index];
            ProductionRecipeExecutionPhysicalSliceReceipt semantic =
                runtimeSlices[index];
            vector.Append(semantic.OutputLineId);
            vector.Append(output.ItemId);
            vector.Append(output.ItemInstanceId);
            vector.Append(output.StackId);
            vector.Append(output.Quantity);
            vector.Append(output.MassGrams);
            vector.Append(capabilityByLine[semantic.OutputLineId]);
        }

        receipt = new ProductionOutputClearanceExecutionReceiptSnapshot(
            descriptor,
            runtime.ActionId,
            runtime.Correlation.FacilityId.Value,
            runtime.Correlation.OperationId,
            runtime.BatchCommitId,
            runtime.OutcomeFingerprint,
            runtime.PlannedOutputFingerprint,
            vector.ComputeSha256(),
            runtime.ActualBatchMassGrams,
            physicalOutputs.Select((output, index) =>
                    new ProductionOutputClearanceExecutionOutputSliceSnapshot(
                        runtimeSlices[index].OutputLineId,
                        output.ItemId,
                        output.ItemInstanceId,
                        output.StackId,
                        output.Quantity,
                        output.MassGrams,
                        capabilityByLine[runtimeSlices[index].OutputLineId]))
                .ToArray(),
            runtime.RuntimeReceiptDigest,
            HandlerId,
            ContractVersion,
            runtime.RouteBatchCommitIds);
        return true;
    }

    private static string CaptureExactAggregateDigest(
        string schema,
        ProductionRecipeExecutionReceipt runtime,
        IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> batches,
        Func<FacilityBufferPlannedOutputRestoreBatchSnapshot, string> selector)
    {
        FacilityBufferPlannedOutputRestoreBatchSnapshot[] ordered = batches
            .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
            .ToArray();
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(schema);
        digest.Append(runtime.Correlation.BillId.Value);
        digest.Append(runtime.Correlation.CycleSequence);
        digest.Append(ordered.Length);
        foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot batch in ordered)
        {
            digest.Append(batch.BatchCommitId);
            digest.Append(selector(batch));
        }
        return digest.ComputeSha256();
    }

    public bool TryAcknowledgeAccepted(
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (receipt == null
            || !string.Equals(receipt.HandlerId, HandlerId, StringComparison.Ordinal)
            || receipt.HandlerVersion != ContractVersion
            || receipt.Descriptor.Payload is not
                ProductionOutputClearanceRecipeExecutablePayload)
        {
            failureReason = "recipe-natural-handler-acknowledgement-owner-mismatch";
            return false;
        }
        return commands.TryAcknowledgeExecutionReceipt(
            receipt.ActionId,
            receipt.RuntimeReceiptDigest,
            out failureReason);
    }
}
