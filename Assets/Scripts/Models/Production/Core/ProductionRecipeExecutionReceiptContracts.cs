using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// An explicit diagnostics correlation established before a generic recipe
/// cycle may complete. It is deliberately keyed by the durable bill/cycle
/// identity rather than by a recipe or facility name.
/// </summary>
public sealed class ProductionRecipeExecutionCorrelation
{
    public ProductionRecipeExecutionCorrelation(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        BuildingInstanceId facilityId)
    {
        if (!billId.IsValid)
            throw new ArgumentException("A valid production bill is required.", nameof(billId));
        if (cycleSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(cycleSequence));
        RecipeId = RequireCanonical(recipeId, nameof(recipeId));
        if (!facilityId.IsValid)
            throw new ArgumentException("A valid production facility is required.", nameof(facilityId));

        BillId = billId;
        CycleSequence = cycleSequence;
        FacilityId = facilityId;
        OperationId = "production-recipe-cycle:" + BillId.Value + ":"
            + CycleSequence.ToString("D8", CultureInfo.InvariantCulture);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-recipe-execution-correlation@1");
        digest.Append(BillId.Value);
        digest.Append(CycleSequence);
        digest.Append(RecipeId);
        digest.Append(FacilityId.Value);
        digest.Append(OperationId);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionBillId BillId { get; }
    public int CycleSequence { get; }
    public string RecipeId { get; }
    public BuildingInstanceId FacilityId { get; }
    public string OperationId { get; }
    public string SourceDigest { get; }

    private static string RequireCanonical(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A canonical production recipe identifier is required.",
                parameter);
        }
        return value;
    }
}

public enum ProductionRecipeExecutionPublicationKind
{
    PreparedBatch = 0,
    ExactCapabilityUnits = 1
}

public sealed class ProductionRecipeExecutionOutputLineReceipt
{
    public ProductionRecipeExecutionOutputLineReceipt(
        string outputLineId,
        string itemId,
        int quantity,
        long massGrams,
        string capabilityFingerprint,
        IReadOnlyList<string> commitIds)
    {
        OutputLineId = RequireCanonical(outputLineId, nameof(outputLineId));
        ItemId = RequireCanonical(itemId, nameof(itemId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (massGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(massGrams));
        CapabilityFingerprint = RequireDigest(
            capabilityFingerprint,
            nameof(capabilityFingerprint));
        string[] orderedCommitIds = (commitIds
                ?? throw new ArgumentNullException(nameof(commitIds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (orderedCommitIds.Length == 0
            || orderedCommitIds.Any(value => !IsCanonical(value))
            || orderedCommitIds.Distinct(StringComparer.Ordinal).Count()
                != orderedCommitIds.Length)
        {
            throw new ArgumentException(
                "Exact production output commit IDs are required.",
                nameof(commitIds));
        }
        Quantity = quantity;
        MassGrams = massGrams;
        CommitIds = Array.AsReadOnly(orderedCommitIds);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-recipe-execution-output-line@1");
        digest.Append(OutputLineId);
        digest.Append(ItemId);
        digest.Append(Quantity);
        digest.Append(MassGrams);
        digest.Append(CapabilityFingerprint);
        digest.Append(CommitIds.Count);
        foreach (string commitId in CommitIds)
            digest.Append(commitId);
        SourceDigest = digest.ComputeSha256();
    }

    public string OutputLineId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public string CapabilityFingerprint { get; }
    public IReadOnlyList<string> CommitIds { get; }
    public string SourceDigest { get; }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsWhiteSpace);

    private static string RequireCanonical(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A canonical production output identity is required.",
                parameter);
        }
        return value;
    }

    private static string RequireDigest(string value, string parameter)
    {
        if (value == null
            || value.Length != 64
            || value.Any(character => !((character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f'))))
        {
            throw new ArgumentException(
                "A lowercase SHA-256 digest is required.",
                parameter);
        }
        return value;
    }
}

public sealed class ProductionRecipeExecutionPhysicalSliceReceipt
{
    public ProductionRecipeExecutionPhysicalSliceReceipt(
        string outputLineId,
        string itemId,
        string stackId,
        int quantity,
        long massGrams,
        string commitId)
    {
        OutputLineId = RequireCanonical(outputLineId, nameof(outputLineId));
        ItemId = RequireCanonical(itemId, nameof(itemId));
        StackId = RequireCanonical(stackId, nameof(stackId));
        CommitId = RequireCanonical(commitId, nameof(commitId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (massGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(massGrams));
        Quantity = quantity;
        MassGrams = massGrams;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-recipe-execution-physical-slice@1");
        digest.Append(OutputLineId);
        digest.Append(ItemId);
        digest.Append(StackId);
        digest.Append(Quantity);
        digest.Append(MassGrams);
        digest.Append(CommitId);
        SourceDigest = digest.ComputeSha256();
    }

    public string OutputLineId { get; }
    public string ItemId { get; }
    public string StackId { get; }
    public int Quantity { get; }
    public long MassGrams { get; }
    public string CommitId { get; }
    public string SourceDigest { get; }

    private static string RequireCanonical(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A canonical physical output identity is required.",
                parameter);
        }
        return value;
    }
}

/// <summary>
/// Immutable one-shot witness captured from the completed generic-production
/// aggregate before it clears its WIP and prepared-output authority.
/// </summary>
public sealed class ProductionRecipeExecutionReceipt
{
    public ProductionRecipeExecutionReceipt(
        string actionId,
        ProductionRecipeExecutionCorrelation correlation,
        string wipInputCommitId,
        int wipInputQuantity,
        long wipInputMassGrams,
        ProductionRecipeExecutionPublicationKind publicationKind,
        string batchCommitId,
        IReadOnlyList<string> routeBatchCommitIds,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        IReadOnlyList<ProductionRecipeExecutionOutputLineReceipt> outputs,
        IReadOnlyList<ProductionRecipeExecutionPhysicalSliceReceipt> physicalSlices)
    {
        ActionId = RequireCanonical(actionId, nameof(actionId));
        Correlation = correlation
            ?? throw new ArgumentNullException(nameof(correlation));
        bool hasPhysicalWip = wipInputQuantity > 0
            || wipInputMassGrams > 0L
            || !string.IsNullOrEmpty(wipInputCommitId);
        if (hasPhysicalWip)
        {
            WipInputCommitId = RequireCanonical(
                wipInputCommitId,
                nameof(wipInputCommitId));
            if (wipInputQuantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(wipInputQuantity));
            if (wipInputMassGrams <= 0L)
                throw new ArgumentOutOfRangeException(nameof(wipInputMassGrams));
        }
        else
        {
            WipInputCommitId = string.Empty;
            if (wipInputQuantity != 0 || wipInputMassGrams != 0L)
            {
                throw new InvalidOperationException(
                    "A source recipe must expose an exact zero-WIP tuple.");
            }
        }
        BatchCommitId = RequireCanonical(batchCommitId, nameof(batchCommitId));
        if (!Enum.IsDefined(typeof(ProductionRecipeExecutionPublicationKind),
                publicationKind))
            throw new ArgumentOutOfRangeException(nameof(publicationKind));
        string[] orderedRouteBatchCommitIds = (routeBatchCommitIds
                ?? throw new ArgumentNullException(nameof(routeBatchCommitIds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (orderedRouteBatchCommitIds.Length == 0
            || orderedRouteBatchCommitIds.Any(value => !IsCanonical(value))
            || orderedRouteBatchCommitIds.Distinct(StringComparer.Ordinal).Count()
                != orderedRouteBatchCommitIds.Length)
        {
            throw new ArgumentException(
                "Exact route batch commit IDs are required.",
                nameof(routeBatchCommitIds));
        }
        PublicationKind = publicationKind;
        RouteBatchCommitIds = Array.AsReadOnly(orderedRouteBatchCommitIds);
        OutcomeFingerprint = RequireDigest(
            outcomeFingerprint,
            nameof(outcomeFingerprint));
        PlannedOutputFingerprint = RequireDigest(
            plannedOutputFingerprint,
            nameof(plannedOutputFingerprint));

        ProductionRecipeExecutionOutputLineReceipt[] orderedOutputs = (outputs
                ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        ProductionRecipeExecutionPhysicalSliceReceipt[] orderedSlices =
            (physicalSlices ?? throw new ArgumentNullException(nameof(physicalSlices)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value?.StackId, StringComparer.Ordinal)
            .ToArray();
        if (orderedOutputs.Length == 0
            || orderedSlices.Length == 0
            || orderedOutputs.Any(value => value == null)
            || orderedSlices.Any(value => value == null)
            || orderedOutputs.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != orderedOutputs.Length
            || orderedSlices.Select(value => value.StackId)
                .Distinct(StringComparer.Ordinal).Count() != orderedSlices.Length)
        {
            throw new InvalidOperationException(
                "A recipe execution receipt requires unique exact output lines and slices.");
        }

        Dictionary<string, ProductionRecipeExecutionOutputLineReceipt> byLine =
            orderedOutputs.ToDictionary(
                value => value.OutputLineId,
                value => value,
                StringComparer.Ordinal);
        foreach (IGrouping<string, ProductionRecipeExecutionPhysicalSliceReceipt> group
                 in orderedSlices.GroupBy(value => value.OutputLineId,
                     StringComparer.Ordinal))
        {
            if (!byLine.TryGetValue(
                    group.Key,
                    out ProductionRecipeExecutionOutputLineReceipt line)
                || group.Any(value => !string.Equals(
                    value.ItemId,
                    line.ItemId,
                    StringComparison.Ordinal))
                || group.Sum(value => value.Quantity) != line.Quantity
                || group.Sum(value => value.MassGrams) != line.MassGrams
                || !group.Select(value => value.CommitId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(line.CommitIds, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Physical output slices do not exactly conserve their completed output line.");
            }
        }
        if (orderedSlices.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != orderedOutputs.Length)
        {
            throw new InvalidOperationException(
                "Every completed physical output line requires exact physical slices.");
        }
        string[] allOutputCommitIds = orderedOutputs
            .SelectMany(value => value.CommitIds)
            .ToArray();
        if (allOutputCommitIds.Distinct(StringComparer.Ordinal).Count()
            != allOutputCommitIds.Length)
        {
            throw new InvalidOperationException(
                "An exact output commit cannot belong to multiple output lines.");
        }
        string[] outputCommitIds = allOutputCommitIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        bool routeCoverageExact = PublicationKind ==
                ProductionRecipeExecutionPublicationKind.PreparedBatch
            ? RouteBatchCommitIds.Count == 1
                && string.Equals(
                    RouteBatchCommitIds[0],
                    BatchCommitId,
                    StringComparison.Ordinal)
            : outputCommitIds.SequenceEqual(
                RouteBatchCommitIds,
                StringComparer.Ordinal);
        if (!routeCoverageExact)
        {
            throw new InvalidOperationException(
                "Route batch commits do not exactly cover completed output commits.");
        }

        WipInputQuantity = wipInputQuantity;
        WipInputMassGrams = wipInputMassGrams;
        Outputs = Array.AsReadOnly(orderedOutputs);
        PhysicalSlices = Array.AsReadOnly(orderedSlices);
        ActualBatchMassGrams = orderedOutputs.Sum(value => value.MassGrams);

        CanonicalSemanticDigestBuilder vector = new();
        vector.Append("production-recipe-execution-resolved-output-vector@1");
        vector.Append(Correlation.SourceDigest);
        vector.Append(BatchCommitId);
        vector.Append(Outputs.Count);
        foreach (ProductionRecipeExecutionOutputLineReceipt output in Outputs)
            vector.Append(output.SourceDigest);
        vector.Append(PhysicalSlices.Count);
        foreach (ProductionRecipeExecutionPhysicalSliceReceipt slice in PhysicalSlices)
            vector.Append(slice.SourceDigest);
        ResolvedOutputVectorDigest = vector.ComputeSha256();

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-recipe-execution-receipt@1");
        digest.Append(ActionId);
        digest.Append(Correlation.SourceDigest);
        digest.Append(WipInputCommitId);
        digest.Append(WipInputQuantity);
        digest.Append(WipInputMassGrams);
        digest.AppendEnum(PublicationKind);
        digest.Append(BatchCommitId);
        digest.Append(RouteBatchCommitIds.Count);
        foreach (string commitId in RouteBatchCommitIds)
            digest.Append(commitId);
        digest.Append(OutcomeFingerprint);
        digest.Append(PlannedOutputFingerprint);
        digest.Append(ResolvedOutputVectorDigest);
        digest.Append(ActualBatchMassGrams);
        RuntimeReceiptDigest = digest.ComputeSha256();
    }

    public string ActionId { get; }
    public ProductionRecipeExecutionCorrelation Correlation { get; }
    public string WipInputCommitId { get; }
    public int WipInputQuantity { get; }
    public long WipInputMassGrams { get; }
    public ProductionRecipeExecutionPublicationKind PublicationKind { get; }
    public string BatchCommitId { get; }
    public IReadOnlyList<string> RouteBatchCommitIds { get; }
    public string OutcomeFingerprint { get; }
    public string PlannedOutputFingerprint { get; }
    public IReadOnlyList<ProductionRecipeExecutionOutputLineReceipt> Outputs { get; }
    public IReadOnlyList<ProductionRecipeExecutionPhysicalSliceReceipt>
        PhysicalSlices { get; }
    public long ActualBatchMassGrams { get; }
    public string ResolvedOutputVectorDigest { get; }
    public string RuntimeReceiptDigest { get; }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsWhiteSpace);

    private static string RequireCanonical(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A canonical production execution identity is required.",
                parameter);
        }
        return value;
    }

    private static string RequireDigest(string value, string parameter)
    {
        if (value == null
            || value.Length != 64
            || value.Any(character => !((character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f'))))
        {
            throw new ArgumentException(
                "A lowercase SHA-256 digest is required.",
                parameter);
        }
        return value;
    }
}

public interface IProductionRecipeExecutionCorrelationCommand
{
    bool TryRegisterExecution(
        string actionId,
        ProductionRecipeExecutionCorrelation correlation,
        out string failureReason);

    bool TryCancelExecution(
        string actionId,
        ProductionRecipeExecutionCorrelation correlation,
        out string failureReason);

    bool TryAcknowledgeExecutionReceipt(
        string actionId,
        string runtimeReceiptDigest,
        out string failureReason);
}

public interface IProductionRecipeExecutionReceiptQuery
{
    bool TryCaptureExecutionReceipt(
        string actionId,
        out ProductionRecipeExecutionReceipt receipt);
}

/// <summary>
/// Internal producer port used only by the generic production aggregate at
/// the exact Completed-before-clear boundary. An uncorrelated normal gameplay
/// cycle is intentionally a no-op.
/// </summary>
public interface IProductionRecipeExecutionReceiptAuthority :
    IProductionRecipeExecutionCorrelationCommand,
    IProductionRecipeExecutionReceiptQuery
{
    bool RequiresExactCapture(
        ProductionBillId billId,
        int cycleSequence);

    bool TryCaptureExactCommittedUnit(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        BuildingInstanceId facilityId,
        ProductionResolvedOutputSaveData output,
        ProductionCommittedOutputSnapshot committedOutput,
        out string failureReason);

    bool TryFinalizeExactCompleted(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        BuildingInstanceId facilityId,
        string wipInputCommitId,
        int wipInputQuantity,
        long wipInputMassGrams,
        IReadOnlyList<ProductionResolvedOutputSaveData> completedOutputs,
        out string failureReason);

    bool TryPublishCompleted(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        BuildingInstanceId facilityId,
        string wipInputCommitId,
        int wipInputQuantity,
        long wipInputMassGrams,
        ProductionPreparedOutputBatchSaveData completedBatch,
        out string failureReason);
}

public sealed class EmptyProductionRecipeExecutionReceiptAuthority :
    IProductionRecipeExecutionReceiptAuthority
{
    public static readonly EmptyProductionRecipeExecutionReceiptAuthority Instance =
        new();

    private EmptyProductionRecipeExecutionReceiptAuthority()
    {
    }

    public bool RequiresExactCapture(
        ProductionBillId billId,
        int cycleSequence) => false;

    public bool TryRegisterExecution(
        string actionId,
        ProductionRecipeExecutionCorrelation correlation,
        out string failureReason)
    {
        failureReason = "production-recipe-execution-receipt-authority-missing";
        return false;
    }

    public bool TryCancelExecution(
        string actionId,
        ProductionRecipeExecutionCorrelation correlation,
        out string failureReason)
    {
        failureReason = "production-recipe-execution-receipt-authority-missing";
        return false;
    }

    public bool TryAcknowledgeExecutionReceipt(
        string actionId,
        string runtimeReceiptDigest,
        out string failureReason)
    {
        failureReason = "production-recipe-execution-receipt-authority-missing";
        return false;
    }

    public bool TryCaptureExecutionReceipt(
        string actionId,
        out ProductionRecipeExecutionReceipt receipt)
    {
        receipt = null;
        return false;
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
        return true;
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
        return true;
    }
}
