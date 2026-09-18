using System;

public sealed class SocialConflictOutcomeAdapter :
    GameplayOutcomeAdapter<SocialConflictOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        SocialLifeOutcomeIds.ConflictResolved;

    public static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey resultKey,
        int absoluteDay,
        bool hasVisitorFacility,
        long worldEpoch) => new(
        resultKey,
        SocialLifeOutcomeIds.ConflictResolved,
        absoluteDay,
        GameplayOutcomeStatus.Succeeded,
        worldEpoch,
        0L,
        participantCount: hasVisitorFacility ? 4 : 2,
        metricCount: hasVisitorFacility ? 7 : 4,
        subjectCount: hasVisitorFacility ? 3 : 2,
        tagCount: hasVisitorFacility ? 3 : 2,
        anchorCount: 0,
        provenanceCount: 1);

    public override OutcomePrepareResult TryGetRequirements(
        in SocialConflictOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.AbsoluteDay,
            receipt.HasVisitorFacility,
            currentWorldEpoch);
        bool valid = receipt.OperationId.IsValid
            && receipt.Instigator.IsValid
            && receipt.Target.IsValid
            && !receipt.Instigator.Equals(receipt.Target)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.InstigatorName)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.TargetName)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.ConflictId)
            && float.IsFinite(receipt.Severity)
            && receipt.Severity >= 0f
            && float.IsFinite(receipt.CommittedMoodDelta)
            && receipt.AbsoluteDay >= 0
            && Enum.IsDefined(typeof(CharacterCommandOrigin), receipt.Origin)
            && (!receipt.HasVisitorFacility
                || receipt.Customer.IsValid
                && receipt.InstigatorCulture.IsValid
                && receipt.TargetCulture.IsValid
                && receipt.FacilityInstanceId.IsValid
                && receipt.VisitorReceiptAbsoluteDay >= 1
                && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.FacilityName));
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "social-conflict-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in SocialConflictOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId instigator = Character(receipt.Instigator);
        GameplayEntityId target = Character(receipt.Target);
        GameplayEntityId conflict = new(
            SocialLifeOutcomeIds.ConflictKind,
            receipt.ConflictId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                instigator,
                SocialLifeOutcomeIds.InstigatorRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.InstigatorName))
            && builder.AddParticipant(new GameplayOutcomeParticipant(
                target,
                SocialLifeOutcomeIds.TargetRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.TargetName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                instigator,
                Math.Clamp(0.5f + receipt.Severity * 0.03f, 0f, 0.85f),
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                target,
                Math.Clamp(0.65f + receipt.Severity * 0.035f, 0f, 0.95f),
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                SocialLifeOutcomeIds.SeverityMetric,
                receipt.Severity,
                SocialLifeOutcomeIds.PointUnit,
                conflict))
            && builder.AddMetric(new GameplayOutcomeMetric(
                SocialLifeOutcomeIds.MoodDeltaMetric,
                receipt.CommittedMoodDelta,
                SocialLifeOutcomeIds.PointUnit,
                target))
            && builder.AddMetric(new GameplayOutcomeMetric(
                SocialLifeOutcomeIds.OriginMetric,
                (int)receipt.Origin,
                SocialLifeOutcomeIds.EnumUnit,
                instigator))
            && builder.AddMetric(new GameplayOutcomeMetric(
                SocialLifeOutcomeIds.ConflictKindMetric,
                1d,
                SocialLifeOutcomeIds.BooleanUnit,
                conflict))
            && builder.AddTag(SocialLifeOutcomeIds.RelationshipTag)
            && builder.AddTag(SocialLifeOutcomeIds.MoodTag)
            && builder.AddProvenance(SocialLifeOutcomeIds.SocialConflictSource);

        if (written && receipt.HasVisitorFacility)
        {
            GameplayEntityId facility = new(
                SocialLifeOutcomeIds.FacilityKind,
                receipt.FacilityInstanceId.Value);
            GameplayEntityId customer = Character(receipt.Customer);
            written = builder.AddParticipant(new GameplayOutcomeParticipant(
                    customer,
                    SocialLifeOutcomeIds.CustomerRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.Customer.Equals(receipt.Instigator)
                        ? receipt.InstigatorName
                        : receipt.TargetName))
                && builder.AddParticipant(new GameplayOutcomeParticipant(
                    facility,
                    SocialLifeOutcomeIds.VenueRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.FacilityName))
                && builder.AddSubject(new GameplayOutcomeSubjectLink(
                    facility,
                    0.6f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0))
                && builder.AddMetric(new GameplayOutcomeMetric(
                    SocialLifeOutcomeIds.InstigatorCultureMetric,
                    1d,
                    SocialLifeOutcomeIds.BooleanUnit,
                    new GameplayEntityId(
                        SocialLifeOutcomeIds.CultureKind,
                        receipt.InstigatorCulture.Value)))
                && builder.AddMetric(new GameplayOutcomeMetric(
                    SocialLifeOutcomeIds.TargetCultureMetric,
                    1d,
                    SocialLifeOutcomeIds.BooleanUnit,
                    new GameplayEntityId(
                        SocialLifeOutcomeIds.CultureKind,
                        receipt.TargetCulture.Value)))
                && builder.AddMetric(new GameplayOutcomeMetric(
                    SocialLifeOutcomeIds.VisitorReceiptDayMetric,
                    receipt.VisitorReceiptAbsoluteDay,
                    SocialLifeOutcomeIds.DayUnit,
                    facility))
                && builder.AddTag(SocialLifeOutcomeIds.VisitorFacilityTag)
                && builder.SetLocation(new GameplayLocationReference(
                    "dungeon",
                    string.Empty,
                    receipt.Location.X,
                    receipt.Location.Y));
        }

        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "social-conflict-write-failed");
    }

    private static GameplayEntityId Character(CharacterId id) => new(
        SocialLifeOutcomeIds.CharacterKind,
        id.Value);
}

public sealed class ApologyOutcomeAdapter :
    GameplayOutcomeAdapter<ApologyOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        SocialLifeOutcomeIds.ApologyResolved;

    public static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey resultKey,
        int absoluteDay,
        long worldEpoch) => new(
        resultKey,
        SocialLifeOutcomeIds.ApologyResolved,
        absoluteDay,
        GameplayOutcomeStatus.Succeeded,
        worldEpoch,
        0L,
        participantCount: 2,
        metricCount: 3,
        subjectCount: 2,
        tagCount: 2,
        anchorCount: 0,
        provenanceCount: 1);

    public override OutcomePrepareResult TryGetRequirements(
        in ApologyOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.AbsoluteDay,
            currentWorldEpoch);
        bool valid = receipt.OperationId.IsValid
            && receipt.Offender.IsValid
            && receipt.Recipient.IsValid
            && !receipt.Offender.Equals(receipt.Recipient)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.OffenderName)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(receipt.RecipientName)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.OffenseId)
            && float.IsFinite(receipt.CommittedMoodDelta)
            && receipt.AbsoluteDay >= 0;
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "apology-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in ApologyOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId offender = Character(receipt.Offender);
        GameplayEntityId recipient = Character(receipt.Recipient);
        GameplayEntityId offense = new(
            SocialLifeOutcomeIds.OffenseKind,
            receipt.OffenseId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                offender,
                SocialLifeOutcomeIds.OffenderRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.OffenderName))
            && builder.AddParticipant(new GameplayOutcomeParticipant(
                recipient,
                SocialLifeOutcomeIds.RecipientRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.RecipientName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                offender,
                receipt.RestitutionProvided ? 0.7f : 0.6f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                recipient,
                receipt.RestitutionProvided ? 0.82f : 0.72f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                SocialLifeOutcomeIds.RestitutionMetric,
                receipt.RestitutionProvided ? 1d : 0d,
                SocialLifeOutcomeIds.BooleanUnit,
                offender))
            && builder.AddMetric(new GameplayOutcomeMetric(
                SocialLifeOutcomeIds.MoodDeltaMetric,
                receipt.CommittedMoodDelta,
                SocialLifeOutcomeIds.PointUnit,
                recipient))
            && builder.AddMetric(new GameplayOutcomeMetric(
                SocialLifeOutcomeIds.OffenseKindMetric,
                1d,
                SocialLifeOutcomeIds.BooleanUnit,
                offense))
            && builder.AddTag(SocialLifeOutcomeIds.RelationshipTag)
            && builder.AddTag(SocialLifeOutcomeIds.MoodTag)
            && builder.AddProvenance(SocialLifeOutcomeIds.ApologySource);
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "apology-write-failed");
    }

    private static GameplayEntityId Character(CharacterId id) => new(
        SocialLifeOutcomeIds.CharacterKind,
        id.Value);
}
