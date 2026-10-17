using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

internal static class ApparelPhysicalTransactionSaveRules
{
    private const string LegacyOutcomeSchema = "apparel-craft-output@2";
    private const string OutcomeSchema = "apparel-craft-output@3";
    internal const string OutputLineId = "output:apparel-crafted-item";

    internal static string CreateRejectedOutcomeFingerprint(
        ApparelWorkOrderSaveData order,
        string recoveryItemId,
        int quantity,
        long outputMassGrams)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("apparel-rejected-recovery@2");
        digest.Append(order.orderId);
        digest.Append(order.qualityAttemptIndex);
        digest.Append(order.rejectedOutputStackId);
        digest.Append(order.rejectedOutputInstanceId);
        digest.Append(recoveryItemId);
        digest.Append(quantity);
        digest.Append(outputMassGrams);
        return digest.ComputeSha256();
    }

    internal static bool ValidateCraftOwnerShape(
        ApparelWorkOrderSaveData order,
        out string failureReason)
    {
        if (order == null)
        {
            failureReason = "apparel-craft-owner-null";
            return false;
        }
        bool started = order.craftPublicationAttempt != 0
            || !string.IsNullOrEmpty(order.craftPublicationOperationId)
            || !string.IsNullOrEmpty(order.craftOutputBatchCommitId)
            || !string.IsNullOrEmpty(order.craftOutcomeFingerprint)
            || !string.IsNullOrEmpty(order.craftOutputComponentFingerprint)
            || order.craftOutputCapability is { IsEmpty: false }
            || !string.IsNullOrEmpty(order.craftAdmissionTokenId)
            || !string.IsNullOrEmpty(order.craftMaximumMassProofDigest)
            || order.craftMaximumBatchMassGrams != 0L
            || !string.IsNullOrEmpty(order.craftCapacitySourceDigest)
            || order.craftRequiredMinimumCapacityGrams != 0L
            || !string.IsNullOrEmpty(order.craftPlannedOutputFingerprint)
            || !string.IsNullOrEmpty(order.craftOutputStackId)
            || !string.IsNullOrEmpty(order.craftOutputInstanceId)
            || !string.IsNullOrEmpty(order.craftInputCommitId)
            || !string.IsNullOrEmpty(order.craftInputRequestFingerprint)
            || order.craftInputMassGrams != 0L
            || order.craftOutputMassGrams != 0L
            || order.craftInputPending
            || order.craftOutputPublished
            || order.craftAdmissionCommitted
            || order.craftInputAcknowledged
            || order.craftOutputAcknowledged
            || order.craftMarketRouted;
        if (!started)
        {
            bool emptyCapability = order.craftOutputCapability == null
                || order.craftOutputCapability.IsEmpty;
            bool emptyQuality = order.craftQualityOutcomeSchemaVersion == 0
                && string.IsNullOrEmpty(order.craftMakerCharacterId)
                && string.IsNullOrEmpty(order.craftMakerDisplayName)
                && !order.craftRejectedBelowMinimum;
            failureReason = emptyCapability && emptyQuality
                ? string.Empty
                : "apparel-craft-owner-authority-without-attempt";
            return emptyCapability && emptyQuality;
        }
        ProductionOutputCapabilitySaveData capability =
            order.craftOutputCapability;
        bool capabilityShapeValid = capability != null
            && !capability.IsEmpty
            && string.Equals(
                capability.outputLineId,
                OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(
                capability.capabilityId,
                ProductionOutputCapabilityIds.ApparelWorkOrder,
                StringComparison.Ordinal)
            && Canonical(capability.itemId)
            && capability.capabilityVersion > 0
            && Canonical(capability.componentCodecId)
            && capability.componentCodecVersion > 0
            && Canonical(capability.fingerprint)
            && string.Equals(
                capability.fingerprint,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    capability.outputLineId,
                    capability.itemId,
                    capability.capabilityId,
                    capability.capabilityVersion,
                    capability.componentCodecId,
                    capability.componentCodecVersion),
                StringComparison.Ordinal);
        bool outputIdsPaired = string.IsNullOrEmpty(order.craftOutputStackId)
            == string.IsNullOrEmpty(order.craftOutputInstanceId);
        bool qualityShapeValid = order.craftQualityOutcomeSchemaVersion == 0
            || order.craftQualityOutcomeSchemaVersion == 1
                && DomainFactValueSyntax.IsIdentity(
                    order.craftMakerCharacterId)
                && !string.IsNullOrWhiteSpace(order.craftMakerDisplayName)
                && Enum.IsDefined(
                    typeof(CraftsmanshipQualityTier),
                    order.craftResolvedQuality);
        bool valid = order.kind == ApparelWorkOrderKind.Craft
            && capabilityShapeValid
            && qualityShapeValid
            && IsSha256(order.craftMaximumMassProofDigest)
            && order.craftMaximumBatchMassGrams > 0L
            && order.craftOutputMassGrams
                <= order.craftMaximumBatchMassGrams
            && order.craftPublicationAttempt >= 0
            && Canonical(order.craftOutputBatchCommitId)
            && Canonical(order.craftOutcomeFingerprint)
            && Canonical(order.craftOutputComponentFingerprint)
            && Canonical(order.craftCapacitySourceDigest)
            && Canonical(order.craftOutputInstanceId)
            && order.craftRequiredMinimumCapacityGrams > 0L
            && order.craftOutputMassGrams > 0L
            && outputIdsPaired
            && (!order.craftInputPending
                || (Canonical(order.craftInputCommitId)
                    && Canonical(order.craftInputRequestFingerprint)
                    && order.craftInputMassGrams > 0L))
            && (!order.craftOutputPublished
                || (Canonical(order.craftAdmissionTokenId)
                    && Canonical(order.craftPublicationOperationId)
                    && Canonical(order.craftPlannedOutputFingerprint)
                    && order.craftInputPending))
            && (!order.craftAdmissionCommitted
                || (order.craftOutputPublished
                    && Canonical(order.craftOutputStackId)))
            && (!order.craftInputAcknowledged
                || order.craftAdmissionCommitted)
            && (!order.craftOutputAcknowledged
                || (order.craftInputAcknowledged
                    && order.craftAdmissionCommitted))
            && (!order.craftMarketRouted || order.craftOutputAcknowledged);
        failureReason = valid
            ? string.Empty
            : "apparel-craft-owner-partial";
        return valid;
    }

    internal static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');

    internal static string CreateOutcomeFingerprint(
        ApparelWorkOrderSaveData order,
        string outputItemId,
        string instanceId,
        string componentFingerprint,
        string capabilityFingerprint,
        bool markForSale,
        long outputMassGrams,
        string makerCharacterId,
        string makerDisplayName,
        CraftsmanshipQualityTier quality,
        bool rejectedBelowMinimum)
    {
        CanonicalSemanticDigestBuilder digest = new();
        bool legacyFrozen = !string.IsNullOrEmpty(
                order.craftOutputBatchCommitId)
            && order.craftQualityOutcomeSchemaVersion == 0;
        digest.Append(legacyFrozen ? LegacyOutcomeSchema : OutcomeSchema);
        digest.Append(order.orderId);
        digest.Append(order.qualityAttemptIndex);
        digest.Append(outputItemId);
        digest.Append(instanceId);
        digest.Append(componentFingerprint);
        digest.Append(capabilityFingerprint);
        digest.Append(markForSale);
        digest.Append(outputMassGrams);
        if (!legacyFrozen)
        {
            digest.Append(makerCharacterId);
            digest.Append(makerDisplayName);
            digest.Append((int)quality);
            digest.Append(rejectedBelowMinimum);
        }
        return digest.ComputeSha256();
    }

    internal static string CreateComponentFingerprint(
        ItemInstanceComponentSaveData component)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("apparel-craft-component@1");
        digest.Append(component.ToCanonicalString());
        return digest.ComputeSha256();
    }

    internal static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
