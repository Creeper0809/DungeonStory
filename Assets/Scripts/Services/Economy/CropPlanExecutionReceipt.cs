using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CropPlanExecutionReceipt
{
    internal CropPlanExecutionReceipt(
        string actionId,
        CropCycleExecutionReceiptSaveData source)
    {
        RequireCanonical(actionId, nameof(actionId));
        CropPlanExecutionReceiptAuthority.ValidateTerminal(source);
        if (!string.Equals(
                actionId,
                source.correlationId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop execution action does not own this receipt.");
        }
        ActionId = actionId;
        PlotId = source.plotId;
        CropId = source.cropId;
        Status = source.status;
        FailureReasonCode = source.terminalReasonCode;
        ExplicitCorrelation = source.explicitCorrelation;
        Indoor = source.indoor;
        SowOperationId = source.sowOperationId;
        SowCommitId = source.sowCommitId;
        InputMassGrams = source.inputMassGrams;
        InputQuantity = source.inputQuantity;
        InputVectorDigest = source.inputVectorDigest;
        SowRequestFingerprint = source.sowRequestFingerprint;
        HarvestOperationId = source.harvestOperationId;
        OutputBatchCommitId = source.outputBatchCommitId;
        OutputMassGrams = source.outputMassGrams;
        OutputVectorDigest = source.outputVectorDigest;
        OutputOutcomeFingerprint = source.outputOutcomeFingerprint;
        PlannedOutputFingerprint = source.plannedOutputFingerprint;
        Inputs = Array.AsReadOnly((source.inputs
                ?? new List<CropPhysicalInputSaveData>())
            .OrderBy(input => input.itemId, StringComparer.Ordinal)
            .ThenBy(input => input.sourceStackId, StringComparer.Ordinal)
            .Select(input => new CropPlanExecutionInputWitness(
                input.itemId,
                input.sourceStackId,
                input.quantity))
            .ToArray());
        Outputs = Array.AsReadOnly((source.outputs
                ?? new List<ProductionDomainPublishedStackSaveData>())
            .OrderBy(output => output.outputLineId, StringComparer.Ordinal)
            .Select(output => new CropPlanExecutionOutputWitness(
                output.outputLineId,
                output.itemId,
                output.itemInstanceId,
                output.stackId,
                output.quantity,
                output.massGrams,
                string.Equals(
                    output.outputLineId,
                    CropHarvestOutputMaximumAuthority.HarvestOutputLineId(
                        source.cropId),
                    StringComparison.Ordinal)
                        ? source.harvestCapability.fingerprint
                        : source.seedCapability.fingerprint))
            .ToArray());
        RuntimeReceiptDigest = source.sourceDigest;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("crop-plan-execution-receipt@2");
        digest.Append(ActionId);
        digest.Append(RuntimeReceiptDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string ActionId { get; }
    public string PlotId { get; }
    public string CropId { get; }
    public CropCycleExecutionReceiptStatus Status { get; }
    public string FailureReasonCode { get; }
    public bool Succeeded => Status == CropCycleExecutionReceiptStatus.Completed;
    public bool ExplicitCorrelation { get; }
    public bool Indoor { get; }
    public string SowOperationId { get; }
    public string SowCommitId { get; }
    public long InputMassGrams { get; }
    public int InputQuantity { get; }
    public string InputVectorDigest { get; }
    public string SowRequestFingerprint { get; }
    public IReadOnlyList<CropPlanExecutionInputWitness> Inputs { get; }
    public string HarvestOperationId { get; }
    public string OutputBatchCommitId { get; }
    public long OutputMassGrams { get; }
    public string OutputVectorDigest { get; }
    public string OutputOutcomeFingerprint { get; }
    public string PlannedOutputFingerprint { get; }
    public IReadOnlyList<CropPlanExecutionOutputWitness> Outputs { get; }
    public string RuntimeReceiptDigest { get; }
    public string SourceDigest { get; }

    private static void RequireCanonical(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A canonical crop execution identifier is required.",
                name);
        }
    }
}

public readonly struct CropPlanExecutionInputWitness
{
    public CropPlanExecutionInputWitness(
        string itemId,
        string sourceStackId,
        int quantity)
    {
        ItemId = itemId ?? string.Empty;
        SourceStackId = sourceStackId ?? string.Empty;
        Quantity = quantity;
    }

    public string ItemId { get; }
    public string SourceStackId { get; }
    public int Quantity { get; }
}

public readonly struct CropPlanExecutionOutputWitness
{
    public CropPlanExecutionOutputWitness(
        string outputLineId,
        string itemId,
        string itemInstanceId,
        string stackId,
        int quantity,
        long massGrams,
        string capabilityFingerprint)
    {
        OutputLineId = outputLineId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        StackId = stackId ?? string.Empty;
        Quantity = quantity;
        MassGrams = massGrams;
        CapabilityFingerprint = capabilityFingerprint ?? string.Empty;
    }

    public string OutputLineId { get; }
    public string ItemId { get; }
    public string ItemInstanceId { get; }
    public string StackId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public string CapabilityFingerprint { get; }
}

public interface ICropPlanExecutionReceiptQuery
{
    bool TryCaptureExecutionReceipt(
        string actionId,
        out CropPlanExecutionReceipt receipt);
}

public interface ICropCycleExecutionCorrelationCommand
{
    bool TryBindNextCycle(
        string correlationId,
        string plotId,
        string cropId,
        out string failureReason);

    bool TryAcknowledgeExecutionReceipt(
        string correlationId,
        string expectedRuntimeReceiptDigest,
        out string failureReason);
}

public static class CropPlanExecutionReceiptAuthority
{
    private const string InputSchema = "crop-cycle-input-receipt@1";
    private const string OutputSchema = "crop-cycle-output-receipt@2";
    private const string ReceiptSchema = "crop-cycle-execution-receipt@2";

    public static CropPlanExecutionReceipt ProjectTerminal(
        string actionId,
        CropCycleExecutionReceiptSaveData source) => new(actionId, source);

    public static CropCycleExecutionReceiptSaveData Begin(
        string correlationId,
        bool explicitCorrelation,
        string plotId,
        bool indoor,
        CropPhysicalCommitSaveData sow)
    {
        RequireCanonical(correlationId, nameof(correlationId));
        RequireCanonical(plotId, nameof(plotId));
        if (sow == null
            || sow.phase != CropPhysicalCommitPhase.OutcomePublished
            || sow.operationSequence < 0
            || !Canonical(sow.operationId)
            || !Canonical(sow.commitId)
            || !Canonical(sow.requestFingerprint)
            || !Canonical(sow.cropId)
            || sow.inputQuantity <= 0
            || sow.inputMassGrams <= 0L
            || !sow.hasSeedLot
            || sow.seedLot == null
            || sow.inputs == null
            || sow.inputs.Count == 0)
        {
            throw new InvalidOperationException(
                "Completed crop sow provenance is invalid.");
        }

        CropCycleExecutionReceiptSaveData value = new()
        {
            plotId = plotId,
            cropId = sow.cropId,
            correlationId = correlationId,
            explicitCorrelation = explicitCorrelation,
            status = CropCycleExecutionReceiptStatus.Active,
            indoor = indoor,
            sowOperationSequence = sow.operationSequence,
            sowOperationId = sow.operationId,
            sowCommitId = sow.commitId,
            sowRequestFingerprint = sow.requestFingerprint,
            inputQuantity = sow.inputQuantity,
            inputMassGrams = sow.inputMassGrams,
            inputSeedLot = sow.seedLot.Clone(),
            inputs = sow.inputs
                .OrderBy(input => input.itemId, StringComparer.Ordinal)
                .ThenBy(input => input.sourceStackId, StringComparer.Ordinal)
                .Select(input => input.DeepClone())
                .ToList()
        };
        value.inputVectorDigest = CaptureInputVectorDigest(value);
        value.sourceDigest = CaptureReceiptDigest(value);
        Validate(value, requireCompleted: false);
        return value;
    }

    public static CropCycleExecutionReceiptSaveData Complete(
        CropCycleExecutionReceiptSaveData inputReceipt,
        CropHarvestOutputSaveData harvest)
    {
        Validate(inputReceipt, requireCompleted: false);
        if (inputReceipt.completed
            || inputReceipt.status != CropCycleExecutionReceiptStatus.Active
            || harvest == null
            || harvest.phase != CropHarvestOutputPhase
                .OutputRestoredAwaitingFinalization
            || !string.Equals(
                inputReceipt.cropId,
                harvest.cropId,
                StringComparison.Ordinal)
            || harvest.operationSequence < 0
            || !Canonical(harvest.operationId)
            || harvest.outputPublication == null
            || !harvest.outputPublication.outputAcknowledged
            || !Canonical(harvest.outputPublication.batchCommitId)
            || !Digest(harvest.outputPublication.outcomeFingerprint)
            || !Digest(harvest.outputPublication.plannedOutputFingerprint)
            || harvest.outputPublication.outputMassGrams <= 0L
            || harvest.outputPublication.stacks == null
            || harvest.outputPublication.stacks.Count != 2
            || harvest.returnedSeedLot == null
            || harvest.harvestCapability == null
            || harvest.harvestCapability.IsEmpty
            || !Digest(harvest.harvestCapability.fingerprint)
            || harvest.seedCapability == null
            || harvest.seedCapability.IsEmpty
            || !Digest(harvest.seedCapability.fingerprint))
        {
            throw new InvalidOperationException(
                "Completed crop harvest provenance is invalid.");
        }

        CropCycleExecutionReceiptSaveData value = inputReceipt.DeepClone();
        value.harvestOperationSequence = harvest.operationSequence;
        value.harvestOperationId = harvest.operationId;
        value.outputBatchCommitId =
            harvest.outputPublication.batchCommitId;
        value.outputOutcomeFingerprint =
            harvest.outputPublication.outcomeFingerprint;
        value.plannedOutputFingerprint =
            harvest.outputPublication.plannedOutputFingerprint;
        value.harvestCapability = harvest.harvestCapability.Clone();
        value.seedCapability = harvest.seedCapability.Clone();
        value.outputMassGrams = harvest.outputPublication.outputMassGrams;
        value.returnedSeedLot = harvest.returnedSeedLot.Clone();
        value.outputs = harvest.outputPublication.stacks
            .OrderBy(output => output.outputLineId, StringComparer.Ordinal)
            .Select(output => output.Clone())
            .ToList();
        value.outputVectorDigest = CaptureOutputVectorDigest(value);
        value.completed = true;
        value.status = CropCycleExecutionReceiptStatus.Completed;
        value.sourceDigest = CaptureReceiptDigest(value);
        Validate(value, requireCompleted: true);
        return value;
    }

    public static CropCycleExecutionReceiptSaveData Fail(
        CropCycleExecutionReceiptSaveData inputReceipt,
        CropCycleExecutionReceiptStatus status,
        string reasonCode)
    {
        Validate(inputReceipt, requireCompleted: false);
        if (inputReceipt == null
            || inputReceipt.IsEmpty
            || inputReceipt.status != CropCycleExecutionReceiptStatus.Active
            || status is not (CropCycleExecutionReceiptStatus.FailedCropDeath
                or CropCycleExecutionReceiptStatus.FailedPlotDestroyed)
            || !Canonical(reasonCode))
        {
            throw new InvalidOperationException(
                "Crop execution failure provenance is invalid.");
        }

        CropCycleExecutionReceiptSaveData value = inputReceipt.DeepClone();
        value.status = status;
        value.terminalReasonCode = reasonCode;
        value.completed = false;
        value.sourceDigest = CaptureReceiptDigest(value);
        ValidateTerminal(value);
        return value;
    }

    public static CropCycleExecutionReceiptSaveData FailBeforeSow(
        string correlationId,
        string plotId,
        string cropId,
        bool indoor,
        string reasonCode)
    {
        RequireCanonical(correlationId, nameof(correlationId));
        RequireCanonical(plotId, nameof(plotId));
        RequireCanonical(cropId, nameof(cropId));
        RequireCanonical(reasonCode, nameof(reasonCode));
        CropCycleExecutionReceiptSaveData value = new()
        {
            plotId = plotId,
            cropId = cropId,
            correlationId = correlationId,
            explicitCorrelation = true,
            status = CropCycleExecutionReceiptStatus.FailedPlotDestroyed,
            terminalReasonCode = reasonCode,
            indoor = indoor
        };
        value.sourceDigest = CaptureReceiptDigest(value);
        ValidateTerminal(value);
        return value;
    }

    public static void ValidateTerminal(
        CropCycleExecutionReceiptSaveData value)
    {
        Validate(value, requireCompleted: false);
        if (value == null
            || value.IsEmpty
            || value.status is CropCycleExecutionReceiptStatus.None
                or CropCycleExecutionReceiptStatus.Active)
        {
            throw new InvalidOperationException(
                "Crop execution receipt is not terminal.");
        }
    }

    public static void Validate(
        CropCycleExecutionReceiptSaveData value,
        bool requireCompleted)
    {
        if (value == null || value.IsEmpty)
        {
            if (requireCompleted)
                throw new InvalidOperationException(
                    "Crop execution receipt is missing.");
            return;
        }
        CropPhysicalInputSaveData[] inputs = (value.inputs
                ?? new List<CropPhysicalInputSaveData>())
            .OrderBy(input => input?.itemId, StringComparer.Ordinal)
            .ThenBy(input => input?.sourceStackId, StringComparer.Ordinal)
            .ToArray();
        bool preSowFailure = value.status
                == CropCycleExecutionReceiptStatus.FailedPlotDestroyed
            && value.explicitCorrelation
            && value.sowOperationSequence == 0
            && string.IsNullOrEmpty(value.sowOperationId)
            && string.IsNullOrEmpty(value.sowCommitId)
            && string.IsNullOrEmpty(value.sowRequestFingerprint)
            && value.inputQuantity == 0
            && value.inputMassGrams == 0L
            && string.IsNullOrEmpty(value.inputVectorDigest)
            && CropCycleExecutionReceiptSaveData.IsSemanticEmptySeedLot(
                value.inputSeedLot)
            && inputs.Length == 0;
        bool inputValid = value.schemaVersion
                == CropCycleExecutionReceiptSaveData.CurrentSchemaVersion
            && Canonical(value.plotId)
            && Canonical(value.cropId)
            && Canonical(value.correlationId)
            && value.status != CropCycleExecutionReceiptStatus.None
            && (preSowFailure
                || value.sowOperationSequence >= 0
                && string.Equals(
                    value.sowOperationId,
                    CropPhysicalTransactionOutbox.FormatSowOperationId(
                        value.plotId,
                        value.sowOperationSequence),
                    StringComparison.Ordinal)
                && Canonical(value.sowCommitId)
                && Canonical(value.sowRequestFingerprint)
                && value.inputQuantity > 0
                && value.inputMassGrams > 0L
                && value.inputSeedLot != null
                && inputs.Length > 0
                && inputs.All(input => input != null
                    && Canonical(input.itemId)
                    && Canonical(input.sourceStackId)
                    && input.quantity > 0)
                && inputs.Select(input => input.sourceStackId)
                    .Distinct(StringComparer.Ordinal).Count() == inputs.Length
                && inputs.Sum(input => input.quantity) == value.inputQuantity
                && Digest(value.inputVectorDigest)
                && string.Equals(
                    value.inputVectorDigest,
                    CaptureInputVectorDigest(value),
                    StringComparison.Ordinal));
        if (!inputValid)
            throw new InvalidOperationException(
                "Crop execution input receipt is invalid.");

        bool outputEmpty = value.harvestOperationSequence == 0
            && string.IsNullOrEmpty(value.harvestOperationId)
            && string.IsNullOrEmpty(value.outputBatchCommitId)
            && string.IsNullOrEmpty(value.outputOutcomeFingerprint)
            && string.IsNullOrEmpty(value.plannedOutputFingerprint)
            && (value.harvestCapability == null
                || value.harvestCapability.IsEmpty)
            && (value.seedCapability == null || value.seedCapability.IsEmpty)
            && value.outputMassGrams == 0L
            && string.IsNullOrEmpty(value.outputVectorDigest)
            && CropCycleExecutionReceiptSaveData.IsSemanticEmptySeedLot(
                value.returnedSeedLot)
            && (value.outputs == null || value.outputs.Count == 0);
        if (value.status == CropCycleExecutionReceiptStatus.Active)
        {
            if (requireCompleted
                || value.completed
                || !string.IsNullOrEmpty(value.terminalReasonCode)
                || !outputEmpty)
                throw new InvalidOperationException(
                    "Incomplete crop execution receipt contains output provenance.");
        }
        else if (value.status == CropCycleExecutionReceiptStatus.Completed)
        {
            ProductionDomainPublishedStackSaveData[] outputs = (value.outputs
                    ?? new List<ProductionDomainPublishedStackSaveData>())
                .OrderBy(output => output?.outputLineId, StringComparer.Ordinal)
                .ToArray();
            string harvestLine = CropHarvestOutputMaximumAuthority
                .HarvestOutputLineId(value.cropId);
            string seedLine = CropHarvestOutputMaximumAuthority
                .SeedOutputLineId(value.cropId);
            bool outputValid = value.completed
                && string.IsNullOrEmpty(value.terminalReasonCode)
                && value.harvestOperationSequence >= 0
                && string.Equals(
                    value.harvestOperationId,
                    CropPlotRuntime.FormatHarvestOperationId(
                        (BuildingInstanceId)value.plotId,
                        value.harvestOperationSequence),
                    StringComparison.Ordinal)
                && string.Equals(
                    value.outputBatchCommitId,
                    CropPlotRuntime.HarvestOutputBatchCommitPrefix
                        + value.harvestOperationId,
                    StringComparison.Ordinal)
                && Digest(value.outputOutcomeFingerprint)
                && Digest(value.plannedOutputFingerprint)
                && CapabilityMatches(
                    value.harvestCapability,
                    harvestLine,
                    outputs.Single(output => string.Equals(
                        output.outputLineId,
                        harvestLine,
                        StringComparison.Ordinal)).itemId)
                && CapabilityMatches(
                    value.seedCapability,
                    seedLine,
                    outputs.Single(output => string.Equals(
                        output.outputLineId,
                        seedLine,
                        StringComparison.Ordinal)).itemId)
                && value.outputMassGrams > 0L
                && value.returnedSeedLot != null
                && outputs.Length == 2
                && outputs.All(output => output != null
                    && Canonical(output.outputLineId)
                    && Canonical(output.itemId)
                    && (string.IsNullOrEmpty(output.itemInstanceId)
                        || Canonical(output.itemInstanceId))
                    && Canonical(output.stackId)
                    && output.quantity > 0
                    && output.massGrams > 0L)
                && outputs.Select(output => output.outputLineId)
                    .Distinct(StringComparer.Ordinal).Count() == outputs.Length
                && outputs.Any(output => string.Equals(
                    output.outputLineId,
                    harvestLine,
                    StringComparison.Ordinal))
                && outputs.Any(output => string.Equals(
                    output.outputLineId,
                    seedLine,
                    StringComparison.Ordinal))
                && outputs.Sum(output => output.massGrams)
                    == value.outputMassGrams
                && Digest(value.outputVectorDigest)
                && string.Equals(
                    value.outputVectorDigest,
                    CaptureOutputVectorDigest(value),
                    StringComparison.Ordinal);
            if (!outputValid)
                throw new InvalidOperationException(
                    "Crop execution output receipt is invalid.");
        }
        else if (value.status is CropCycleExecutionReceiptStatus.FailedCropDeath
            or CropCycleExecutionReceiptStatus.FailedPlotDestroyed)
        {
            if (requireCompleted
                || value.completed
                || !Canonical(value.terminalReasonCode)
                || !outputEmpty)
            {
                throw new InvalidOperationException(
                    "Failed crop execution receipt is invalid.");
            }
        }
        else
        {
            throw new InvalidOperationException(
                "Crop execution receipt contains an unknown status.");
        }

        if (!Digest(value.sourceDigest)
            || !string.Equals(
                value.sourceDigest,
                CaptureReceiptDigest(value),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop execution receipt digest drifted.");
        }
    }

    private static string CaptureInputVectorDigest(
        CropCycleExecutionReceiptSaveData value)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(InputSchema);
        digest.Append(value.plotId);
        digest.Append(value.cropId);
        digest.Append(value.correlationId);
        digest.Append(value.indoor);
        digest.Append(value.sowOperationSequence);
        digest.Append(value.sowOperationId);
        digest.Append(value.sowCommitId);
        digest.Append(value.sowRequestFingerprint);
        digest.Append(value.inputQuantity);
        digest.Append(value.inputMassGrams);
        digest.Append(SeedLotItemStateCodec.Encode(value.inputSeedLot)
            .ToCanonicalString());
        CropPhysicalInputSaveData[] inputs = value.inputs
            .OrderBy(input => input.itemId, StringComparer.Ordinal)
            .ThenBy(input => input.sourceStackId, StringComparer.Ordinal)
            .ToArray();
        digest.Append(inputs.Length);
        foreach (CropPhysicalInputSaveData input in inputs)
        {
            digest.Append(input.itemId);
            digest.Append(input.sourceStackId);
            digest.Append(input.quantity);
        }
        return digest.ComputeSha256();
    }

    private static string CaptureOutputVectorDigest(
        CropCycleExecutionReceiptSaveData value)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(OutputSchema);
        digest.Append(value.harvestOperationSequence);
        digest.Append(value.harvestOperationId);
        digest.Append(value.outputBatchCommitId);
        digest.Append(value.outputOutcomeFingerprint);
        digest.Append(value.plannedOutputFingerprint);
        digest.Append(value.harvestCapability.fingerprint);
        digest.Append(value.seedCapability.fingerprint);
        digest.Append(value.outputMassGrams);
        digest.Append(SeedLotItemStateCodec.Encode(value.returnedSeedLot)
            .ToCanonicalString());
        ProductionDomainPublishedStackSaveData[] outputs = value.outputs
            .OrderBy(output => output.outputLineId, StringComparer.Ordinal)
            .ToArray();
        digest.Append(outputs.Length);
        foreach (ProductionDomainPublishedStackSaveData output in outputs)
        {
            digest.Append(output.outputLineId);
            digest.Append(output.itemId);
            digest.Append(output.itemInstanceId);
            digest.Append(output.stackId);
            digest.Append(output.quantity);
            digest.Append(output.massGrams);
        }
        return digest.ComputeSha256();
    }

    private static string CaptureReceiptDigest(
        CropCycleExecutionReceiptSaveData value)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(ReceiptSchema);
        digest.Append(value.schemaVersion);
        digest.Append(value.explicitCorrelation);
        digest.Append((int)value.status);
        digest.Append(value.terminalReasonCode);
        digest.Append(value.inputVectorDigest);
        digest.Append(value.completed);
        digest.Append(value.outputVectorDigest);
        return digest.ComputeSha256();
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsWhiteSpace);

    private static bool CapabilityMatches(
        ProductionOutputCapabilitySaveData capability,
        string outputLineId,
        string itemId)
    {
        if (capability == null
            || capability.IsEmpty
            || !string.Equals(
                capability.outputLineId,
                outputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                capability.itemId,
                itemId,
                StringComparison.Ordinal)
            || !Canonical(capability.capabilityId)
            || capability.capabilityVersion <= 0
            || !Canonical(capability.componentCodecId)
            || capability.componentCodecVersion <= 0)
        {
            return false;
        }

        return string.Equals(
            capability.fingerprint,
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                capability.outputLineId,
                capability.itemId,
                capability.capabilityId,
                capability.capabilityVersion,
                capability.componentCodecId,
                capability.componentCodecVersion),
            StringComparison.Ordinal);
    }

    private static void RequireCanonical(string value, string name)
    {
        if (!Canonical(value))
            throw new ArgumentException(
                "A canonical crop execution identifier is required.",
                name);
    }

    private static bool Digest(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}
