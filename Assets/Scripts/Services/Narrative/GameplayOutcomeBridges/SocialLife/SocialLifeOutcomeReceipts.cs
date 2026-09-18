using System;
using DungeonStory.Narrative.Korean;

public static class SocialLifeOutcomeIds
{
    public const string ConflictProducerId = "social.conflict-resolution";
    public const string ApologyProducerId = "social.apology-resolution";

    public static readonly GameplayOutcomeTypeId ConflictResolved =
        new("social.conflict-resolved");
    public static readonly GameplayOutcomeTypeId ApologyResolved =
        new("social.apology-resolved");

    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayEntityKindId ConflictKind = new("conflict-kind");
    public static readonly GameplayEntityKindId CultureKind = new("culture");
    public static readonly GameplayEntityKindId OffenseKind = new("offense-kind");

    public static readonly GameplayRoleId InstigatorRole = new("instigator");
    public static readonly GameplayRoleId TargetRole = new("target");
    public static readonly GameplayRoleId CustomerRole = new("customer");
    public static readonly GameplayRoleId VenueRole = new("venue");
    public static readonly GameplayRoleId OffenderRole = new("offender");
    public static readonly GameplayRoleId RecipientRole = new("recipient");

    public static readonly GameplayMetricId SeverityMetric = new("relationship.severity");
    public static readonly GameplayMetricId MoodDeltaMetric = new("mood.delta");
    public static readonly GameplayMetricId OriginMetric = new("command.origin");
    public static readonly GameplayMetricId ConflictKindMetric = new("relationship.conflict-kind");
    public static readonly GameplayMetricId InstigatorCultureMetric = new("identity.instigator-culture");
    public static readonly GameplayMetricId TargetCultureMetric = new("identity.target-culture");
    public static readonly GameplayMetricId VisitorReceiptDayMetric = new("relationship.visitor-receipt-day");
    public static readonly GameplayMetricId RestitutionMetric = new("relationship.restitution-provided");
    public static readonly GameplayMetricId OffenseKindMetric = new("relationship.offense-kind");

    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId EnumUnit = new("enum");
    public static readonly GameplayMetricUnitId BooleanUnit = new("boolean");
    public static readonly GameplayMetricUnitId DayUnit = new("day");

    public static readonly GameplayOutcomeTagId RelationshipTag = new("relationship");
    public static readonly GameplayOutcomeTagId MoodTag = new("mood");
    public static readonly GameplayOutcomeTagId VisitorFacilityTag = new("visitor-facility");

    public static readonly GameplayOutcomeProvenanceReference SocialConflictSource =
        new("source-receipt", "social-conflict-event");
    public static readonly GameplayOutcomeProvenanceReference CommittedConflictSource =
        new("source-receipt", "social-conflict-committed-receipt-event");
    public static readonly GameplayOutcomeProvenanceReference ApologySource =
        new("source-receipt", "apology-event");
}

public readonly struct SocialConflictOutcomeReceipt
{
    public SocialConflictOutcomeReceipt(
        string operationId,
        CharacterId instigator,
        KoreanNameSnapshot instigatorName,
        CharacterId target,
        KoreanNameSnapshot targetName,
        string conflictId,
        float severity,
        CharacterCommandOrigin origin,
        float committedMoodDelta,
        int absoluteDay,
        CharacterId customer = default,
        SpeciesCultureId instigatorCulture = default,
        SpeciesCultureId targetCulture = default,
        BuildingInstanceId facilityInstanceId = default,
        KoreanNameSnapshot facilityName = default,
        CoreGridCell location = default)
    {
        OperationId = new GameplayOperationId(operationId);
        if (!instigator.IsValid || !target.IsValid || instigator.Equals(target))
            throw new ArgumentException("Distinct social-conflict participants are required.");
        if (!GameplayOutcomeLedger.IsValidDisplayNameSnapshot(instigatorName)
            || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(targetName))
            throw new ArgumentException("Historical participant names are required.");
        ConflictId = GameplayOutcomeStableIdSyntax.Require(conflictId, nameof(conflictId));
        if (!float.IsFinite(severity) || severity < 0f
            || !float.IsFinite(committedMoodDelta)
            || absoluteDay < 0
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), origin))
            throw new ArgumentException("Social-conflict result values are invalid.");

        bool hasVenue = facilityInstanceId.IsValid;
        if (hasVenue != customer.IsValid
            || hasVenue != instigatorCulture.IsValid
            || hasVenue != targetCulture.IsValid
            || hasVenue && instigatorCulture.Equals(targetCulture)
            || hasVenue && !customer.Equals(instigator) && !customer.Equals(target)
            || hasVenue && !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(facilityName))
            throw new ArgumentException("Visitor-facility conflict provenance is incomplete.");

        Instigator = instigator;
        InstigatorName = instigatorName;
        Target = target;
        TargetName = targetName;
        Severity = severity;
        Origin = origin;
        CommittedMoodDelta = committedMoodDelta;
        AbsoluteDay = absoluteDay;
        Customer = customer;
        InstigatorCulture = instigatorCulture;
        TargetCulture = targetCulture;
        FacilityInstanceId = facilityInstanceId;
        FacilityName = facilityName;
        Location = location;
        VisitorReceiptAbsoluteDay = hasVenue ? Math.Max(1, absoluteDay) : 0;
    }

    public GameplayOperationId OperationId { get; }
    public CharacterId Instigator { get; }
    public KoreanNameSnapshot InstigatorName { get; }
    public CharacterId Target { get; }
    public KoreanNameSnapshot TargetName { get; }
    public string ConflictId { get; }
    public float Severity { get; }
    public CharacterCommandOrigin Origin { get; }
    public float CommittedMoodDelta { get; }
    public int AbsoluteDay { get; }
    public CharacterId Customer { get; }
    public SpeciesCultureId InstigatorCulture { get; }
    public SpeciesCultureId TargetCulture { get; }
    public BuildingInstanceId FacilityInstanceId { get; }
    public KoreanNameSnapshot FacilityName { get; }
    public CoreGridCell Location { get; }
    public int VisitorReceiptAbsoluteDay { get; }
    public bool HasVisitorFacility => FacilityInstanceId.IsValid;
    public GameplayResultKey ResultKey => new(
        SocialLifeOutcomeIds.ConflictProducerId,
        OperationId,
        0L,
        0);
}

public readonly struct ApologyOutcomeReceipt
{
    public ApologyOutcomeReceipt(
        string operationId,
        CharacterId offender,
        KoreanNameSnapshot offenderName,
        CharacterId recipient,
        KoreanNameSnapshot recipientName,
        string offenseId,
        bool restitutionProvided,
        float committedMoodDelta,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        if (!offender.IsValid || !recipient.IsValid || offender.Equals(recipient))
            throw new ArgumentException("Distinct apology participants are required.");
        if (!GameplayOutcomeLedger.IsValidDisplayNameSnapshot(offenderName)
            || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(recipientName))
            throw new ArgumentException("Historical participant names are required.");
        OffenseId = GameplayOutcomeStableIdSyntax.Require(offenseId, nameof(offenseId));
        if (!float.IsFinite(committedMoodDelta) || absoluteDay < 0)
            throw new ArgumentException("Apology result values are invalid.");

        Offender = offender;
        OffenderName = offenderName;
        Recipient = recipient;
        RecipientName = recipientName;
        RestitutionProvided = restitutionProvided;
        CommittedMoodDelta = committedMoodDelta;
        AbsoluteDay = absoluteDay;
    }

    public GameplayOperationId OperationId { get; }
    public CharacterId Offender { get; }
    public KoreanNameSnapshot OffenderName { get; }
    public CharacterId Recipient { get; }
    public KoreanNameSnapshot RecipientName { get; }
    public string OffenseId { get; }
    public bool RestitutionProvided { get; }
    public float CommittedMoodDelta { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey => new(
        SocialLifeOutcomeIds.ApologyProducerId,
        OperationId,
        0L,
        0);
}

public readonly struct ReservedSocialLifeOutcome
{
    internal ReservedSocialLifeOutcome(
        PreparedOutcomeReservation reservation,
        GameplayResultKey resultKey,
        bool alreadyCommitted)
    {
        Reservation = reservation;
        ResultKey = resultKey;
        AlreadyCommitted = alreadyCommitted;
    }

    internal PreparedOutcomeReservation Reservation { get; }
    public GameplayResultKey ResultKey { get; }
    public bool AlreadyCommitted { get; }
    public bool IsValid => AlreadyCommitted && ResultKey.IsValid || Reservation.IsValid;
}

public readonly struct PreparedSocialLifeOutcome
{
    internal PreparedSocialLifeOutcome(
        PreparedOutcomeToken token,
        GameplayResultKey resultKey,
        bool alreadyCommitted)
    {
        Token = token;
        ResultKey = resultKey;
        AlreadyCommitted = alreadyCommitted;
    }

    internal PreparedOutcomeToken Token { get; }
    public GameplayResultKey ResultKey { get; }
    public bool AlreadyCommitted { get; }
    public bool IsValid => AlreadyCommitted && ResultKey.IsValid || Token.IsValid;
}
