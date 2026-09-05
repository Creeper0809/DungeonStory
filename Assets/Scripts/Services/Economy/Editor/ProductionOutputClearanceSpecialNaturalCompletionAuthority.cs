#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Frozen editor-only correlation between one natural-measurement action and
/// the output that the production runtime actually committed.  This is not a
/// second production receipt: callers may publish it only by joining a
/// terminal domain owner to the acknowledged physical FacilityBuffer batch.
/// </summary>
public sealed class ProductionOutputClearanceNaturalCompletedActionSnapshot
{
    internal ProductionOutputClearanceNaturalCompletedActionSnapshot(
        string actionId,
        string payloadKind,
        string runtimeFacilityId,
        string operationId,
        string batchCommitId,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        string executionIdentityDigest,
        string domainReceiptDigest,
        IReadOnlyList<ProductionOutputClearanceExecutionOutputSliceSnapshot>
            outputs)
    {
        ActionId = RequireCanonical(actionId, nameof(actionId));
        PayloadKind = RequireCanonical(payloadKind, nameof(payloadKind));
        RuntimeFacilityId = RequireCanonical(
            runtimeFacilityId,
            nameof(runtimeFacilityId));
        OperationId = RequireCanonical(operationId, nameof(operationId));
        BatchCommitId = RequireCanonical(
            batchCommitId,
            nameof(batchCommitId));
        RequireDigest(outcomeFingerprint, nameof(outcomeFingerprint));
        RequireDigest(
            plannedOutputFingerprint,
            nameof(plannedOutputFingerprint));
        RequireDigest(executionIdentityDigest, nameof(executionIdentityDigest));
        RequireDigest(domainReceiptDigest, nameof(domainReceiptDigest));
        OutcomeFingerprint = outcomeFingerprint;
        PlannedOutputFingerprint = plannedOutputFingerprint;
        ExecutionIdentityDigest = executionIdentityDigest;
        DomainReceiptDigest = domainReceiptDigest;

        ProductionOutputClearanceExecutionOutputSliceSnapshot[] ordered =
            (outputs ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value?.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value?.StackId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null)
            || ordered.Select(value => value.StackId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "A natural completion requires unique actual physical slices.");
        }
        Outputs = Array.AsReadOnly(ordered);
        ActualBatchMassGrams = checked(ordered.Sum(value => value.MassGrams));
        if (ActualBatchMassGrams <= 0L)
            throw new InvalidOperationException(
                "A natural completion requires positive physical output mass.");

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-completion@1");
        digest.Append(ActionId);
        digest.Append(PayloadKind);
        digest.Append(RuntimeFacilityId);
        digest.Append(OperationId);
        digest.Append(BatchCommitId);
        digest.Append(OutcomeFingerprint);
        digest.Append(PlannedOutputFingerprint);
        digest.Append(ExecutionIdentityDigest);
        digest.Append(DomainReceiptDigest);
        digest.Append(ActualBatchMassGrams);
        digest.Append(Outputs.Count);
        foreach (ProductionOutputClearanceExecutionOutputSliceSnapshot output in
                 Outputs)
            digest.Append(output.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string ActionId { get; }
    public string PayloadKind { get; }
    public string RuntimeFacilityId { get; }
    public string OperationId { get; }
    public string BatchCommitId { get; }
    public string OutcomeFingerprint { get; }
    public string PlannedOutputFingerprint { get; }
    public string ExecutionIdentityDigest { get; }
    public string DomainReceiptDigest { get; }
    public long ActualBatchMassGrams { get; }
    public IReadOnlyList<ProductionOutputClearanceExecutionOutputSliceSnapshot>
        Outputs { get; }
    public string SourceDigest { get; }

    private static string RequireCanonical(string value, string parameter)
    {
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            value,
            parameter);
        return value;
    }

    private static void RequireDigest(string value, string parameter)
    {
        if (!ProductionOutputClearanceProfileObservation
                .IsLowercaseSha256(value))
            throw new ArgumentException(
                "A lowercase SHA-256 digest is required.", parameter);
    }
}

public static class ProductionOutputClearanceNaturalExecutionIdentity
{
    public static string CombatCraft(
        string craftDefinitionId,
        string materialId)
    {
        CanonicalSemanticDigestBuilder digest = Begin("combat-craft");
        digest.Append(RequireCanonical(craftDefinitionId));
        digest.Append(RequireCanonical(materialId));
        return digest.ComputeSha256();
    }

    public static string Apparel(
        string apparelId,
        string materialId,
        ApparelSizeClass size,
        ApparelModificationKind modifications)
    {
        CanonicalSemanticDigestBuilder digest = Begin("apparel");
        digest.Append(RequireCanonical(apparelId));
        digest.Append(RequireCanonical(materialId));
        digest.AppendEnum(size);
        digest.AppendEnum(modifications);
        return digest.ComputeSha256();
    }

    public static string CertifiedSeed(string cropId)
    {
        CanonicalSemanticDigestBuilder digest = Begin("certified-seed");
        digest.Append(RequireCanonical(cropId));
        return digest.ComputeSha256();
    }

    private static CanonicalSemanticDigestBuilder Begin(string kind)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-identity@1");
        digest.Append(kind);
        return digest;
    }

    private static string RequireCanonical(string value)
    {
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            value,
            nameof(value));
        return value;
    }
}

public interface IProductionOutputClearanceNaturalCompletionCorrelationQuery
{
    bool TryCapture(
        string actionId,
        string payloadKind,
        out ProductionOutputClearanceNaturalCompletedActionSnapshot snapshot);
}

public interface IProductionOutputClearanceNaturalCompletionCorrelationCommand
{
    bool TryAcknowledge(
        string actionId,
        string payloadKind,
        string sourceDigest,
        out string failureReason);
}

/// <summary>
/// Per-PlayMode-run action correlation.  A driver calls one of the typed
/// Publish methods only after the real command has reached its actual terminal
/// owner.  The methods independently rejoin that owner to the acknowledged
/// physical batch before making the correlation visible to a handler.
/// </summary>
public sealed class ProductionOutputClearanceNaturalCompletionCorrelationAuthority :
    IProductionOutputClearanceNaturalCompletionCorrelationQuery,
    IProductionOutputClearanceNaturalCompletionCorrelationCommand
{
    private readonly Dictionary<string,
        ProductionOutputClearanceNaturalCompletedActionSnapshot> byAction =
        new(StringComparer.Ordinal);

    public bool TryPublishCombatCraft(
        string actionId,
        CombatEquipmentCraftOrderSaveData terminalOrder,
        IFacilityBufferPlannedOutputPublicationService publication,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (terminalOrder == null
            || !terminalOrder.attemptOutcomeResolved
            || !terminalOrder.completionEffectsPublished
            || terminalOrder.outputPublication == null
            || !terminalOrder.outputPublication.outputPublished
            || !terminalOrder.outputPublication.admissionCommitted
            || !terminalOrder.outputPublication.outputAcknowledged
            || terminalOrder.outputCapability == null
            || terminalOrder.outputCapability.IsEmpty
            || terminalOrder.outputQuantity <= 0
            || terminalOrder.acceptedCount
                < Math.Max(1, terminalOrder.requiredAcceptedCount)
            || !string.Equals(
                terminalOrder.outputPublication.ownerFacilityId,
                terminalOrder.facilityPersistentId,
                StringComparison.Ordinal)
            || !string.Equals(
                terminalOrder.outputPublication.batchCommitId,
                terminalOrder.outputCommitId,
                StringComparison.Ordinal))
        {
            failureReason = "combat-natural-terminal-owner-incomplete";
            return false;
        }

        return TryPublishJoined(
            actionId,
            "combat-craft",
            terminalOrder.facilityPersistentId,
            terminalOrder.outputPublication.publicationOperationId,
            terminalOrder.outputPublication,
            ProductionOutputClearanceNaturalExecutionIdentity.CombatCraft(
                terminalOrder.definitionId,
                terminalOrder.materialId),
            CaptureCombatDomainReceiptDigest(terminalOrder),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [terminalOrder.outputCapability.outputLineId] =
                    terminalOrder.outputCapability.fingerprint
            },
            publication,
            out failureReason);
    }

    public bool TryPublishApparel(
        string actionId,
        ApparelWorkOrderSaveData terminalOrder,
        IFacilityBufferPlannedOutputPublicationService publication,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (terminalOrder == null
            || terminalOrder.kind != ApparelWorkOrderKind.Craft
            || terminalOrder.state != ApparelWorkOrderState.Completed
            || !terminalOrder.craftOutputPublished
            || !terminalOrder.craftAdmissionCommitted
            || !terminalOrder.craftInputAcknowledged
            || !terminalOrder.craftOutputAcknowledged
            || terminalOrder.craftOutputCapability == null
            || terminalOrder.craftOutputCapability.IsEmpty
            || terminalOrder.craftOutputMassGrams <= 0L
            || terminalOrder.acceptedCount
                < Math.Max(1, terminalOrder.requiredAcceptedCount))
        {
            failureReason = "apparel-natural-terminal-owner-incomplete";
            return false;
        }

        ProductionDomainOutputPublicationSaveData owner = new()
        {
            publicationOperationId = terminalOrder.craftPublicationOperationId,
            batchCommitId = terminalOrder.craftOutputBatchCommitId,
            outcomeFingerprint = terminalOrder.craftOutcomeFingerprint,
            plannedOutputFingerprint =
                terminalOrder.craftPlannedOutputFingerprint,
            outputMassGrams = terminalOrder.craftOutputMassGrams,
            ownerFacilityId = terminalOrder.facilityInstanceId,
            outputPublished = terminalOrder.craftOutputPublished,
            admissionCommitted = terminalOrder.craftAdmissionCommitted,
            outputAcknowledged = terminalOrder.craftOutputAcknowledged
        };
        return TryPublishJoined(
            actionId,
            "apparel",
            terminalOrder.facilityInstanceId,
            terminalOrder.craftPublicationOperationId,
            owner,
            ProductionOutputClearanceNaturalExecutionIdentity.Apparel(
                terminalOrder.apparelDefinitionId,
                terminalOrder.materialDefinitionId,
                terminalOrder.targetSize,
                terminalOrder.targetModifications),
            CaptureApparelDomainReceiptDigest(terminalOrder),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [terminalOrder.craftOutputCapability.outputLineId] =
                    terminalOrder.craftOutputCapability.fingerprint
            },
            publication,
            out failureReason);
    }

    public bool TryPublishCertifiedSeed(
        CertifiedSeedPlanExecutionReceipt planReceipt,
        IFacilityBufferPlannedOutputPublicationService publication,
        IProductionOutputCapabilityRegistry capabilities,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (planReceipt == null || capabilities == null)
        {
            failureReason = "certified-seed-natural-owner-incomplete";
            return false;
        }
        if (!TryCaptureAcknowledgedBatch(
                planReceipt.OutputBatchCommitId,
                publication,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                out failureReason))
            return false;

        Dictionary<string, string> fingerprints = new(StringComparer.Ordinal);
        foreach (FacilityBufferPlannedOutputRestoreStackSnapshot stack in
                 batch.Stacks)
        {
            if (stack == null)
            {
                failureReason = "certified-seed-natural-physical-slice-null";
                return false;
            }
            ProductionOutputCapabilityDescriptor capability;
            try
            {
                capability = capabilities.CaptureDeclaredDescriptor(
                    stack.OutputLineId,
                    stack.ItemId,
                    ProductionOutputCapabilityIds.CertifiedSeed);
            }
            catch (Exception exception) when (exception is ArgumentException
                || exception is InvalidOperationException)
            {
                failureReason =
                    "certified-seed-natural-capability-capture-failed";
                return false;
            }
            if (fingerprints.TryGetValue(stack.OutputLineId, out string existing)
                && !string.Equals(existing, capability.Fingerprint,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "certified-seed-natural-capability-line-conflict";
                return false;
            }
            fingerprints[stack.OutputLineId] = capability.Fingerprint;
        }

        return TryPublishJoinedBatch(
            planReceipt.ActionId,
            "certified-seed",
            planReceipt.FacilityInstanceId,
            planReceipt.InputOperationId,
            batch,
            ProductionOutputClearanceNaturalExecutionIdentity.CertifiedSeed(
                planReceipt.CropId),
            planReceipt.SourceDigest,
            fingerprints,
            out failureReason);
    }

    public bool TryCapture(
        string actionId,
        string payloadKind,
        out ProductionOutputClearanceNaturalCompletedActionSnapshot snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(actionId)
            || string.IsNullOrWhiteSpace(payloadKind)
            || !byAction.TryGetValue(actionId, out snapshot)
            || !string.Equals(snapshot.PayloadKind, payloadKind,
                StringComparison.Ordinal))
        {
            snapshot = null;
            return false;
        }
        return true;
    }

    public bool TryAcknowledge(
        string actionId,
        string payloadKind,
        string sourceDigest,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryCapture(actionId, payloadKind, out
                ProductionOutputClearanceNaturalCompletedActionSnapshot value)
            || !string.Equals(value.SourceDigest, sourceDigest,
                StringComparison.Ordinal))
        {
            failureReason = "natural-completion-acknowledgement-owner-mismatch";
            return false;
        }
        if (!byAction.Remove(actionId))
        {
            failureReason = "natural-completion-acknowledgement-conflict";
            return false;
        }
        return true;
    }

    private bool TryPublishJoined(
        string actionId,
        string payloadKind,
        string runtimeFacilityId,
        string operationId,
        ProductionDomainOutputPublicationSaveData owner,
        string executionIdentityDigest,
        string domainReceiptDigest,
        IReadOnlyDictionary<string, string> capabilityFingerprints,
        IFacilityBufferPlannedOutputPublicationService publication,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (owner == null
            || !owner.outputPublished
            || !owner.admissionCommitted
            || !owner.outputAcknowledged
            || !string.Equals(owner.ownerFacilityId, runtimeFacilityId,
                StringComparison.Ordinal)
            || !string.Equals(owner.publicationOperationId, operationId,
                StringComparison.Ordinal)
            || owner.outputMassGrams <= 0L)
        {
            failureReason = payloadKind + "-natural-publication-owner-incomplete";
            return false;
        }
        if (!TryCaptureAcknowledgedBatch(
                owner.batchCommitId,
                publication,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                out failureReason)
            || batch.TotalMassGrams != owner.outputMassGrams
            || !string.Equals(batch.OutcomeFingerprint,
                owner.outcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(batch.PlannedOutputFingerprint,
                owner.plannedOutputFingerprint, StringComparison.Ordinal))
        {
            failureReason = payloadKind + "-natural-publication-batch-mismatch";
            return false;
        }
        return TryPublishJoinedBatch(
            actionId,
            payloadKind,
            runtimeFacilityId,
            operationId,
            batch,
            executionIdentityDigest,
            domainReceiptDigest,
            capabilityFingerprints,
            out failureReason);
    }

    private bool TryPublishJoinedBatch(
        string actionId,
        string payloadKind,
        string runtimeFacilityId,
        string operationId,
        FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
        string executionIdentityDigest,
        string domainReceiptDigest,
        IReadOnlyDictionary<string, string> capabilityFingerprints,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (batch == null
            || capabilityFingerprints == null
            || batch.Stacks.Count == 0
            || batch.TotalMassGrams <= 0L
            || batch.TotalMassGrams != batch.Stacks.Sum(value =>
                value?.MassGrams ?? 0L)
            || batch.TotalQuantity != batch.Stacks.Sum(value =>
                value?.Quantity ?? 0))
        {
            failureReason = payloadKind + "-natural-physical-batch-invalid";
            return false;
        }
        List<ProductionOutputClearanceExecutionOutputSliceSnapshot> outputs =
            new();
        foreach (FacilityBufferPlannedOutputRestoreStackSnapshot stack in
                 batch.Stacks.OrderBy(value => value?.OutputLineId,
                     StringComparer.Ordinal).ThenBy(value => value?.StackId,
                     StringComparer.Ordinal))
        {
            if (stack == null
                || !string.Equals(stack.BatchCommitId, batch.BatchCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(stack.OutcomeFingerprint,
                    batch.OutcomeFingerprint, StringComparison.Ordinal)
                || !string.Equals(stack.PlannedOutputFingerprint,
                    batch.PlannedOutputFingerprint, StringComparison.Ordinal)
                || !capabilityFingerprints.TryGetValue(
                    stack.OutputLineId,
                    out string capabilityFingerprint))
            {
                failureReason = payloadKind
                    + "-natural-physical-slice-provenance-mismatch";
                return false;
            }
            outputs.Add(
                new ProductionOutputClearanceExecutionOutputSliceSnapshot(
                    stack.OutputLineId,
                    stack.ItemId,
                    stack.ItemInstanceId,
                    stack.StackId,
                    stack.Quantity,
                    stack.MassGrams,
                    capabilityFingerprint));
        }

        ProductionOutputClearanceNaturalCompletedActionSnapshot snapshot;
        try
        {
            snapshot = new ProductionOutputClearanceNaturalCompletedActionSnapshot(
                actionId,
                payloadKind,
                runtimeFacilityId,
                operationId,
                batch.BatchCommitId,
                batch.OutcomeFingerprint,
                batch.PlannedOutputFingerprint,
                executionIdentityDigest,
                domainReceiptDigest,
                outputs);
        }
        catch (Exception exception) when (exception is ArgumentException
            || exception is InvalidOperationException
            || exception is OverflowException)
        {
            failureReason = payloadKind
                + "-natural-completion-snapshot-invalid";
            return false;
        }
        if (byAction.ContainsKey(actionId))
        {
            failureReason = "natural-completion-action-already-published";
            return false;
        }
        byAction.Add(actionId, snapshot);
        return true;
    }

    private static string CaptureCombatDomainReceiptDigest(
        CombatEquipmentCraftOrderSaveData order)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-craft-natural-terminal-owner@1");
        digest.Append(order.orderId);
        digest.Append(order.definitionId);
        digest.Append(order.materialId);
        digest.Append(order.facilityPersistentId);
        digest.Append(order.outputOperationId);
        digest.Append(order.outputPublication.publicationOperationId);
        digest.Append(order.outputPublication.batchCommitId);
        digest.Append(order.outputPublication.outcomeFingerprint);
        digest.Append(order.outputPublication.plannedOutputFingerprint);
        digest.Append(order.outputPublication.outputMassGrams);
        digest.Append(order.outputCapability.fingerprint);
        digest.Append(order.outputQuantity);
        digest.Append(order.acceptedCount);
        return digest.ComputeSha256();
    }

    private static string CaptureApparelDomainReceiptDigest(
        ApparelWorkOrderSaveData order)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("apparel-natural-terminal-owner@1");
        digest.Append(order.orderId);
        digest.Append(order.apparelDefinitionId);
        digest.Append(order.materialDefinitionId);
        digest.AppendEnum(order.targetSize);
        digest.AppendEnum(order.targetModifications);
        digest.Append(order.facilityInstanceId);
        digest.Append(order.craftPublicationOperationId);
        digest.Append(order.craftOutputBatchCommitId);
        digest.Append(order.craftOutcomeFingerprint);
        digest.Append(order.craftPlannedOutputFingerprint);
        digest.Append(order.craftOutputMassGrams);
        digest.Append(order.craftOutputCapability.fingerprint);
        return digest.ComputeSha256();
    }

    private static bool TryCaptureAcknowledgedBatch(
        string batchCommitId,
        IFacilityBufferPlannedOutputPublicationService publication,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
        out string failureReason)
    {
        batch = null;
        failureReason = string.Empty;
        if (publication == null
            || !publication.TryCaptureBatch(
                batchCommitId,
                allowAcknowledged: true,
                out batch,
                out bool acknowledged,
                out FacilityBufferPlannedOutputPublicationFailureCode _,
                out string _)
            || !acknowledged
            || batch == null)
        {
            batch = null;
            failureReason = "natural-completion-acknowledged-batch-missing";
            return false;
        }
        return true;
    }
}

public abstract class
    ProductionOutputClearanceCorrelatedNaturalMeasurementHandler<TPayload> :
    IProductionOutputClearanceNaturalMeasurementHandler
    where TPayload : class, IProductionOutputClearanceExecutablePayload
{
    private readonly IProductionOutputClearanceNaturalCompletionCorrelationQuery
        query;
    private readonly IProductionOutputClearanceNaturalCompletionCorrelationCommand
        command;

    protected ProductionOutputClearanceCorrelatedNaturalMeasurementHandler(
        string handlerId,
        int contractVersion,
        string payloadKind,
        IProductionOutputClearanceNaturalCompletionCorrelationQuery query,
        IProductionOutputClearanceNaturalCompletionCorrelationCommand command)
    {
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            handlerId,
            nameof(handlerId));
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            payloadKind,
            nameof(payloadKind));
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        HandlerId = handlerId;
        ContractVersion = contractVersion;
        PayloadKind = payloadKind;
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.command = command ?? throw new ArgumentNullException(nameof(command));
    }

    public string HandlerId { get; }
    public int ContractVersion { get; }
    public string PayloadKind { get; }

    public bool TryCaptureCompleted(
        ProductionOutputClearanceExecutableDescriptor descriptor,
        string actionId,
        out ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        out string failureReason)
    {
        receipt = null;
        failureReason = string.Empty;
        if (descriptor?.Payload is not TPayload payload
            || !string.Equals(payload.PayloadKind, PayloadKind,
                StringComparison.Ordinal))
        {
            failureReason = PayloadKind + "-natural-handler-payload-mismatch";
            return false;
        }
        if (!query.TryCapture(
                actionId,
                PayloadKind,
                out ProductionOutputClearanceNaturalCompletedActionSnapshot actual)
            || actual == null)
        {
            failureReason = PayloadKind + "-natural-handler-receipt-not-found";
            return false;
        }

        ProductionOutputClearanceExecutableOutput[] expected =
            GetOutputs(payload)
                .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
                .ToArray();
        var actualLines = actual.Outputs
            .GroupBy(value => new
            {
                value.OutputLineId,
                value.ItemId,
                value.CapabilityFingerprint
            })
            .Select(group => new
            {
                group.Key.OutputLineId,
                group.Key.ItemId,
                group.Key.CapabilityFingerprint,
                Quantity = checked(group.Sum(value => value.Quantity)),
                MassGrams = checked(group.Sum(value => value.MassGrams))
            })
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        bool exact = expected.Length == actualLines.Length;
        for (int index = 0; exact && index < expected.Length; index++)
        {
            exact = string.Equals(expected[index].OutputLineId,
                        actualLines[index].OutputLineId,
                        StringComparison.Ordinal)
                    && string.Equals(expected[index].ItemId,
                        actualLines[index].ItemId,
                        StringComparison.Ordinal)
                    && expected[index].Quantity == actualLines[index].Quantity
                    && expected[index].MassGrams == actualLines[index].MassGrams
                    && string.Equals(
                        expected[index].Descriptor.Fingerprint,
                        actualLines[index].CapabilityFingerprint,
                        StringComparison.Ordinal);
        }
        if (!exact
            || !string.Equals(
                actual.ExecutionIdentityDigest,
                CaptureExpectedExecutionIdentity(payload),
                StringComparison.Ordinal)
            || actual.ActualBatchMassGrams
                != descriptor.Plan.Winner.Source
                    .MaximumSingleCompletionMassGrams
            || actual.ActualBatchMassGrams
                != expected.Sum(value => value.MassGrams))
        {
            failureReason = PayloadKind
                + "-natural-handler-selected-output-vector-mismatch";
            return false;
        }

        CanonicalSemanticDigestBuilder vector = new();
        vector.Append("production-output-clearance-resolved-output-vector@1");
        vector.Append(descriptor.SourceDigest);
        vector.Append(actual.SourceDigest);
        vector.Append(actual.Outputs.Count);
        foreach (ProductionOutputClearanceExecutionOutputSliceSnapshot output in
                 actual.Outputs)
            vector.Append(output.SourceDigest);

        receipt = new ProductionOutputClearanceExecutionReceiptSnapshot(
            descriptor,
            actual.ActionId,
            actual.RuntimeFacilityId,
            actual.OperationId,
            actual.BatchCommitId,
            actual.OutcomeFingerprint,
            actual.PlannedOutputFingerprint,
            vector.ComputeSha256(),
            actual.ActualBatchMassGrams,
            actual.Outputs,
            actual.SourceDigest,
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
            || receipt.Descriptor.Payload is not TPayload
            || !string.Equals(receipt.HandlerId, HandlerId,
                StringComparison.Ordinal)
            || receipt.HandlerVersion != ContractVersion)
        {
            failureReason = PayloadKind
                + "-natural-handler-acknowledgement-owner-mismatch";
            return false;
        }
        return command.TryAcknowledge(
            receipt.ActionId,
            PayloadKind,
            receipt.RuntimeReceiptDigest,
            out failureReason);
    }

    protected abstract IReadOnlyList<ProductionOutputClearanceExecutableOutput>
        GetOutputs(TPayload payload);

    protected abstract string CaptureExpectedExecutionIdentity(
        TPayload payload);
}

public sealed class ProductionOutputClearanceCombatCraftNaturalMeasurementHandler :
    ProductionOutputClearanceCorrelatedNaturalMeasurementHandler<
        ProductionOutputClearanceCombatCraftExecutablePayload>
{
    public const string Id = "natural-measurement-handler:combat-craft";
    public const int Version = 1;

    public ProductionOutputClearanceCombatCraftNaturalMeasurementHandler(
        IProductionOutputClearanceNaturalCompletionCorrelationQuery query,
        IProductionOutputClearanceNaturalCompletionCorrelationCommand command)
        : base(Id, Version, "combat-craft", query, command)
    {
    }

    protected override IReadOnlyList<ProductionOutputClearanceExecutableOutput>
        GetOutputs(ProductionOutputClearanceCombatCraftExecutablePayload payload) =>
        payload.Outputs;

    protected override string CaptureExpectedExecutionIdentity(
        ProductionOutputClearanceCombatCraftExecutablePayload payload) =>
        ProductionOutputClearanceNaturalExecutionIdentity.CombatCraft(
            payload.CraftDefinitionId,
            payload.SelectedMaterialId);
}

public sealed class ProductionOutputClearanceApparelNaturalMeasurementHandler :
    ProductionOutputClearanceCorrelatedNaturalMeasurementHandler<
        ProductionOutputClearanceApparelExecutablePayload>
{
    public const string Id = "natural-measurement-handler:apparel";
    public const int Version = 1;

    public ProductionOutputClearanceApparelNaturalMeasurementHandler(
        IProductionOutputClearanceNaturalCompletionCorrelationQuery query,
        IProductionOutputClearanceNaturalCompletionCorrelationCommand command)
        : base(Id, Version, "apparel", query, command)
    {
    }

    protected override IReadOnlyList<ProductionOutputClearanceExecutableOutput>
        GetOutputs(ProductionOutputClearanceApparelExecutablePayload payload) =>
        payload.Outputs;

    protected override string CaptureExpectedExecutionIdentity(
        ProductionOutputClearanceApparelExecutablePayload payload) =>
        ProductionOutputClearanceNaturalExecutionIdentity.Apparel(
            payload.ApparelId,
            payload.SelectedMaterialId,
            payload.SelectedSize,
            payload.SelectedModifications);
}

public sealed class ProductionOutputClearanceCertifiedSeedNaturalMeasurementHandler :
    ProductionOutputClearanceCorrelatedNaturalMeasurementHandler<
        ProductionOutputClearanceCertifiedSeedExecutablePayload>
{
    public const string Id = "natural-measurement-handler:certified-seed";
    public const int Version = 1;

    public ProductionOutputClearanceCertifiedSeedNaturalMeasurementHandler(
        IProductionOutputClearanceNaturalCompletionCorrelationQuery query,
        IProductionOutputClearanceNaturalCompletionCorrelationCommand command)
        : base(Id, Version, "certified-seed", query, command)
    {
    }

    protected override IReadOnlyList<ProductionOutputClearanceExecutableOutput>
        GetOutputs(ProductionOutputClearanceCertifiedSeedExecutablePayload payload) =>
        payload.Outputs;

    protected override string CaptureExpectedExecutionIdentity(
        ProductionOutputClearanceCertifiedSeedExecutablePayload payload) =>
        ProductionOutputClearanceNaturalExecutionIdentity.CertifiedSeed(
            payload.CropId);
}
#endif
