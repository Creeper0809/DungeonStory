using System;
using System.Linq;
using UnityEngine;

internal static class SurgicalPartProductionOutputSemantics
{
    private const string OutcomeSchema = "surgical-part-output@2";
    private const string PublicationOperationPrefix =
        "surgical-part-output-publication:";

    internal static string PublicationOperationId(string commitId) =>
        PublicationOperationPrefix + (commitId ?? string.Empty);

    internal static bool TryResolveDefinition(
        string itemId,
        out string nodeId,
        out SurgicalPartKind kind)
    {
        string value = itemId ?? string.Empty;
        bool canonical = value.Length >
                SurgeryItemDefinitions.ProstheticPrefix.Length
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && value.StartsWith(
                SurgeryItemDefinitions.ProstheticPrefix,
                StringComparison.Ordinal);
        nodeId = canonical
            ? value.Substring(SurgeryItemDefinitions.ProstheticPrefix.Length)
            : string.Empty;
        kind = nodeId.StartsWith("eye:", StringComparison.Ordinal)
            ? SurgicalPartKind.Implant
            : SurgicalPartKind.Prosthetic;
        return canonical
            && nodeId.Length > 0
            && string.Equals(nodeId, nodeId.Trim(), StringComparison.Ordinal);
    }

    internal static void ResolveDefinition(
        string itemId,
        out string nodeId,
        out SurgicalPartKind kind)
    {
        if (!TryResolveDefinition(itemId, out nodeId, out kind))
        {
            throw new InvalidOperationException(
                "Surgical-part output definition ID is invalid: "
                + (itemId ?? string.Empty));
        }
    }

    internal static float ResolveQuality(float workerQuality) =>
        Mathf.Clamp(workerQuality, 0.1f, 1.75f);

    internal static string CreateOutcomeFingerprint(
        string commitId,
        string outputLineId,
        string itemId,
        string componentFingerprint,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputBufferCapacitySourceSnapshot capacity)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(OutcomeSchema);
        digest.Append(commitId);
        digest.Append(outputLineId);
        digest.Append(itemId);
        digest.Append(componentFingerprint);
        digest.Append(maximumMassProof.SourceDigest);
        digest.Append(maximumMassProof.MaximumBatchMassGrams);
        digest.Append(capacity.SourceDigest);
        digest.Append(capacity.RequiredMinimumCapacityGrams);
        return digest.ComputeSha256();
    }
}

public sealed class SurgicalPartProductionOutputRestoreCapabilityValidator :
    IProductionResolvedOutputRestoreCapabilityValidator
{
    private readonly IFacilityBufferPlannedOutputProjectionQuery plannedOutput;

    public SurgicalPartProductionOutputRestoreCapabilityValidator(
        IFacilityBufferPlannedOutputProjectionQuery plannedOutput)
    {
        this.plannedOutput = plannedOutput
            ?? throw new ArgumentNullException(nameof(plannedOutput));
    }

    public string CapabilityId =>
        SurgicalPartProductionOutputHandler.HandlerCapabilityId;
    public int ContractVersion =>
        SurgicalPartProductionOutputHandler.HandlerContractVersion;
    public string ComponentCodecId =>
        SurgicalPartProductionOutputHandler.HandlerComponentCodecId;
    public int ComponentCodecVersion =>
        SurgicalPartProductionOutputHandler.HandlerComponentCodecVersion;

    public void Validate(ProductionResolvedOutputRestoreValidationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        FacilityBufferPlannedOutputRestoreStackSnapshot stack =
            context.Physical.Stacks.Count == 1
                ? context.Physical.Stacks[0]
                : null;
        SurgicalPartProductionOutputSemantics.ResolveDefinition(
            context.Output.itemId,
            out string expectedNodeId,
            out SurgicalPartKind expectedKind);
        if (stack == null
            || context.Physical.TotalQuantity != 1
            || stack.Quantity != 1
            || !string.Equals(
                stack.OutputLineId,
                context.Output.outputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.ItemId,
                context.Output.itemId,
                StringComparison.Ordinal)
            || !SurgicalPartPreparedOutputComponentCodec.TryRead(
                stack.Components,
                out string partInstanceId,
                out string nodeId,
                out SurgicalPartKind kind,
                out float quality,
                out string commitId)
            || string.IsNullOrWhiteSpace(partInstanceId)
            || !string.Equals(nodeId, expectedNodeId, StringComparison.Ordinal)
            || kind != expectedKind
            || BitConverter.SingleToInt32Bits(quality)
                != BitConverter.SingleToInt32Bits(
                    SurgicalPartProductionOutputSemantics.ResolveQuality(
                        context.Output.workerQuality))
            || !string.Equals(
                commitId,
                context.Output.pendingCommitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Surgical exact-output restore component semantics drifted: "
                + context.Output.pendingCommitId);
        }

        ItemInstanceComponentSaveData component = stack.Components.Single(value =>
            value != null
            && string.Equals(
                value.componentTypeId,
                SurgicalPartPreparedOutputComponentCodec.ComponentTypeId,
                StringComparison.Ordinal));
        string componentFingerprint = SurgicalPartPreparedOutputComponentCodec.Hash(
            component.ToCanonicalString());
        ProductionOutputBufferCapacitySourceSnapshot capacity =
            context.FacilityCapacity.Capacity;
        string outcome =
            SurgicalPartProductionOutputSemantics.CreateOutcomeFingerprint(
                commitId,
                context.Output.outputLineId,
                context.Output.itemId,
                componentFingerprint,
                context.MaximumMassProof,
                capacity);
        string destinationId = ProductionBillRuntime.OutputDestinationPrefix
            + context.FacilityCapacity.FacilityInstanceId;
        if (!string.Equals(
                context.Physical.OutcomeFingerprint,
                outcome,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.DestinationId,
                context.Bill.outputDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                context.Bill.outputDestinationId,
                destinationId,
                StringComparison.Ordinal)
            || context.IsPendingPhysical
                && (stack.State != WorldItemStackState.FacilityOutputBuffer
                    || stack.Position != context.FacilityCapacity.FacilityPosition))
        {
            throw new InvalidOperationException(
                "Surgical exact-output restore outcome semantics drifted: "
                + context.Output.pendingCommitId);
        }

        FacilityBufferPlannedOutputRequest request = new(
            SurgicalPartProductionOutputSemantics.PublicationOperationId(commitId),
            commitId,
            outcome,
            destinationId,
            context.FacilityCapacity.FacilityPosition,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            destinationId,
            context.FacilityCapacity.FacilityInstanceId,
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision,
            new[]
            {
                new FacilityBufferPlannedOutputSlice(
                    context.Output.outputLineId,
                    new PhysicalItemMassSubject(
                        new ItemDefinitionId(context.Output.itemId),
                        stack.ItemInstanceId,
                        PhysicalItemMassSubjectKind.GenericDefinition,
                        Array.Empty<PhysicalItemComponentSnapshot>(),
                        string.Empty),
                    1,
                    new[] { component },
                    componentFingerprint)
            },
            capacity.SourceDigest,
            capacity.RequiredMinimumCapacityGrams,
            capacity.ClearanceGateDigest);
        if (!plannedOutput.TryProjectPlannedOutput(
                request,
                out FacilityBufferPlannedOutputSnapshot planned,
                out _,
                out string failure)
            || planned.TotalQuantity != context.Physical.TotalQuantity
            || planned.TotalMassGrams != context.Physical.TotalMassGrams
            || !string.Equals(
                planned.Fingerprint,
                context.Physical.PlannedOutputFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                componentFingerprint,
                stack.PreparedComponentFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Surgical exact-output restore planned fingerprint drifted: "
                + context.Output.pendingCommitId + ":" + failure);
        }
    }
}
