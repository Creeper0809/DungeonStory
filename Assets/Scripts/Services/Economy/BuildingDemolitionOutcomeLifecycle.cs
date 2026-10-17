using System;


public static class BuildingDemolitionOutcomeLifecycle
{
    public static string ProjectContribution(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "A valid facility ID is required.",
                nameof(facilityId));
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("building-demolition-outcome-lifecycle@1");
        digest.Append(facilityId.Value);
        return digest.ComputeSha256();
    }
    internal static bool IsCanonicalOwner(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        ProductionFacilityDestructiveDrainOutcomeSnapshot snapshot = context.OutcomeSnapshot;
        string expectedOwner = snapshot.IsValid
            ? ProductionFacilityDestructiveDrainOwnerStableIds
                .BuildingDemolition(context.OperationId.Value)
            : string.Empty;
        string expectedStep = expectedOwner.Length > 0
            ? ProductionFacilityDestructiveDrainCanonical.BuildStepOperationId(
                context.OperationId,
                ProductionFacilityDestructiveDrainParticipantIds.BuildingDemolitionOutcome,
                expectedOwner)
            : string.Empty;
        string expectedContribution = BuildingDemolitionOutcomeLifecycle
            .ProjectContribution(context.FacilityId);
        if (!string.Equals(context.ParticipantId, ProductionFacilityDestructiveDrainParticipantIds.BuildingDemolitionOutcome,
                StringComparison.Ordinal)
            || !snapshot.IsValid
            || !string.Equals(context.Owner.ownerStableId, expectedOwner,
                StringComparison.Ordinal)
            || !string.Equals(context.Owner.stepOperationId, expectedStep,
                StringComparison.Ordinal)
            || !string.Equals(
                context.Owner.requestFingerprint,
                snapshot.ComputeFingerprint(context.FacilityId),
                StringComparison.Ordinal)
            || !string.Equals(
                context.ExpectedDurableContributionFingerprint,
                expectedContribution,
                StringComparison.Ordinal))
        {
            return false;
        }
        return true;
    }

    internal static ProductionFacilityDestructiveDrainStepResult CreateStepResult(
        ProductionFacilityDestructiveDrainStepContext context,
        string contribution,
        ProductionFacilityDestructiveDrainStepStatus status)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("building-demolition-destructive-receipt@1");
        digest.Append(context.Owner.requestFingerprint);
        digest.Append(context.Owner.stepOperationId);
        string fingerprint = digest.ComputeSha256();
        return new ProductionFacilityDestructiveDrainStepResult(
            status,
            "building-demolition-outcome:" + fingerprint.Substring(0, 24),
            fingerprint,
            contribution);
    }

    internal static bool MatchesJournalReceipt(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        ProductionFacilityDestructiveDrainStepResult expected = CreateStepResult(
            context,
            context.ExpectedDurableContributionFingerprint,
            ProductionFacilityDestructiveDrainStepStatus.Applied);
        return string.Equals(
                context.Owner.commitId,
                expected.CommitId,
                StringComparison.Ordinal)
            && string.Equals(
                context.Owner.receiptFingerprint,
                expected.ReceiptFingerprint,
                StringComparison.Ordinal);
    }
}
