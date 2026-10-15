using System;

internal static class FacilityUseFactMapping
{
    private const string DigestSchema = "facility-use-completed-fact-v1";

    internal static MigratedDomainFactCapture Capture(
        FacilityUseCompletedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        FacilityUseCommitState data = fact.Snapshot();
        MigratedProducerOutcomeDetails details =
            CharacterFacilityUseOutcomeDetails.Create(
                fact.FacilityId,
                fact.FacilityDisplayName,
                data,
                new[]
                {
                    new GameplayOutcomeProvenanceReference(
                        "facility-use-context-sha256",
                        ComputeContextSha256(fact, data))
                });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FacilityVisitEvent,
            fact.OperationId,
            fact.AbsoluteDay,
            Status(data),
            details);
    }

    private static GameplayOutcomeStatus Status(
        FacilityUseCommitState data) =>
        ((CharacterFacilityUseOutcomeStatus)data.outcomeStatus) switch
        {
            CharacterFacilityUseOutcomeStatus.Completed =>
                GameplayOutcomeStatus.Succeeded,
            CharacterFacilityUseOutcomeStatus.Partial =>
                GameplayOutcomeStatus.PartiallySucceeded,
            CharacterFacilityUseOutcomeStatus.Cancelled =>
                GameplayOutcomeStatus.Cancelled,
            CharacterFacilityUseOutcomeStatus.Failed =>
                GameplayOutcomeStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(
                nameof(data.outcomeStatus),
                data.outcomeStatus,
                "Unknown facility-use outcome status.")
        };

    private static string ComputeContextSha256(
        FacilityUseCompletedFact fact,
        FacilityUseCommitState data)
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append(DigestSchema);
        digest.Append(fact.FacilityId);
        digest.Append(fact.FacilityDisplayName);
        digest.Append(fact.AbsoluteDay);
        digest.Append(data.operationSequence);
        digest.Append(data.operationId);
        digest.Append(data.actorId);
        digest.Append(data.actorDisplayName);
        digest.Append(data.facilityRole);
        digest.Append(data.serviceSessionId);
        digest.Append(data.outcomeStatus);
        digest.Append(data.effectsCommitted);
        digest.Append(data.failureCode);
        digest.AppendFloat(data.healthBefore);
        digest.AppendFloat(data.healthAfter);
        digest.AppendFloat(data.maximumHealthBefore);
        digest.AppendFloat(data.maximumHealthAfter);
        digest.AppendFloat(data.injuryBefore);
        digest.AppendFloat(data.injuryAfter);
        digest.AppendFloat(data.expeditionStressBefore);
        digest.AppendFloat(data.expeditionStressAfter);
        digest.AppendFloat(data.moodBefore);
        digest.AppendFloat(data.moodAfter);
        digest.AppendFloat(data.hungerBefore);
        digest.AppendFloat(data.hungerAfter);
        digest.AppendFloat(data.funBefore);
        digest.AppendFloat(data.funAfter);
        digest.AppendFloat(data.sleepBefore);
        digest.AppendFloat(data.sleepAfter);
        digest.AppendFloat(data.excretionBefore);
        digest.AppendFloat(data.excretionAfter);
        digest.AppendFloat(data.hygieneBefore);
        digest.AppendFloat(data.hygieneAfter);
        digest.Append(data.activeConditionIds.Count);
        foreach (string value in data.activeConditionIds)
            digest.Append(value);
        digest.Append(data.childResultKeys.Count);
        foreach (string value in data.childResultKeys)
            digest.Append(value);
        return digest.ComputeSha256();
    }
}
