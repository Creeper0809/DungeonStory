public sealed class OffenseTruthRevealOutcomeAdapter :
    GameplayOutcomeAdapter<OffenseTruthRevealOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        ExternalFactionOutcomeIds.TruthRevealed;

    public override OutcomePrepareResult TryGetRequirements(
        in OffenseTruthRevealOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 1,
            metricCount: 1,
            subjectCount: 1,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2);
        bool valid = receipt.OperationId.IsValid
            && receipt.OwnerRevision > 0L
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.TargetId)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(
                receipt.TargetDisplayName)
            && !string.IsNullOrWhiteSpace(receipt.TruthTitle)
            && !string.IsNullOrWhiteSpace(receipt.TruthText)
            && receipt.AbsoluteDay >= 0;
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "offense-truth-reveal-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in OffenseTruthRevealOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId target = new(
            ExternalFactionOutcomeIds.ExpeditionTargetKind,
            receipt.TargetId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                target,
                ExternalFactionOutcomeIds.RevealedTargetRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.TargetDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                target,
                1f,
                NarrativeMemoryTier.Core,
                true,
                false,
                0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                ExternalFactionOutcomeIds.TruthRevealedMetric,
                1d,
                ExternalFactionOutcomeIds.BooleanUnit,
                target))
            && builder.AddTag(ExternalFactionOutcomeIds.ExpeditionTag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "truth-title-sha256",
                NarrativeInferenceHash.ComputeSha256Utf8(
                    receipt.TruthTitle)))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "truth-text-sha256",
                NarrativeInferenceHash.ComputeSha256Utf8(
                    receipt.TruthText)));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "offense-truth-reveal-write-failed");
    }
}
