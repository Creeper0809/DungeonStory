using System;
using DungeonStory.Foundation.Facts;

public enum SocialInteractionKind { Conflict = 1, MinionArgument = 2, Apology = 3 }

[Serializable]
public sealed class SocialInteractionFactData
{
    public SocialInteractionKind kind;
    public string operationId = string.Empty, actorId = string.Empty, actorName = string.Empty,
        targetId = string.Empty, targetName = string.Empty;
    public int absoluteDay;
    public string conflictId = string.Empty;
    public float severity, committedMoodDelta;
    public CharacterCommandOrigin origin;
    public string customerId = string.Empty, instigatorCultureId = string.Empty, targetCultureId = string.Empty,
        facilityId = string.Empty, facilityName = string.Empty;
    public int positionX, positionY;
    public string offenseId = string.Empty, offendingPublicationId = string.Empty, rootOperationId = string.Empty,
        causationRelationId = string.Empty;
    public bool restitutionProvided;
    public SocialInteractionFactData Clone() => (SocialInteractionFactData)MemberwiseClone();
    public void Validate()
    {
        if (!Enum.IsDefined(typeof(SocialInteractionKind), kind) || !Id(operationId) || !Id(actorId) || !Id(targetId)
            || actorId == targetId || !Name(actorName) || !Name(targetName) || absoluteDay < 1
            || !float.IsFinite(committedMoodDelta) || !float.IsFinite(severity) || severity < 0f
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), origin)
            || customerId == null || instigatorCultureId == null || targetCultureId == null
            || facilityId == null || facilityName == null || conflictId == null
            || offenseId == null || offendingPublicationId == null || rootOperationId == null || causationRelationId == null)
            throw new ArgumentException("social-interaction-fact-invalid");
        bool apology = kind == SocialInteractionKind.Apology;
        if (apology
            ? !Id(offenseId) || !DomainFactValueSyntax.IsText(offendingPublicationId, 1024, false)
                || !Id(rootOperationId) || !Id(causationRelationId) || conflictId.Length != 0 || severity != 0
                || facilityId.Length != 0 || origin != CharacterCommandOrigin.DirectPlayerOrder
            : !Id(conflictId) || offenseId.Length != 0 || offendingPublicationId.Length != 0
                || rootOperationId.Length != 0 || causationRelationId.Length != 0 || restitutionProvided)
            throw new ArgumentException("social-interaction-kind-fields-invalid");
        bool venue = facilityId.Length != 0;
        if (venue
            ? !Id(facilityId) || !Name(facilityName) || !Id(customerId) || customerId != actorId && customerId != targetId
                || !Id(instigatorCultureId) || !Id(targetCultureId) || instigatorCultureId == targetCultureId
            : facilityName.Length != 0 || customerId.Length != 0 || instigatorCultureId.Length != 0
                || targetCultureId.Length != 0 || positionX != 0 || positionY != 0)
            throw new ArgumentException("social-interaction-venue-invalid");
        if (kind == SocialInteractionKind.MinionArgument
            && (conflictId != "minion-argument" || severity != 2f || committedMoodDelta != 0f
                || origin != CharacterCommandOrigin.Autonomous || venue))
            throw new ArgumentException("minion-argument-fact-invalid");
    }
    private static bool Id(string value) => DomainFactValueSyntax.IsIdentity(value);
    private static bool Name(string value) => DomainFactValueSyntax.IsText(value, 256, false);
}

/// <summary>Frozen mechanical social result; no ledger key, display grammar or importance policy.</summary>
public class SocialInteractionFact
{
    private readonly SocialInteractionFactData data;
    public SocialInteractionFact(SocialInteractionFactData data, bool isProposal = false)
    {
        (data ?? throw new ArgumentNullException(nameof(data))).Validate();
        if (isProposal && data.committedMoodDelta != 0f) throw new ArgumentException("social-proposal-has-effect");
        this.data = data.Clone(); IsProposal = isProposal;
    }
    public bool IsProposal { get; }
    public SocialInteractionFactData Snapshot() => data.Clone();
    public SocialInteractionFact WithMoodDelta(float delta)
    { var result = Snapshot(); result.committedMoodDelta = delta; return new SocialInteractionFact(result); }
    public bool MatchesProposal(SocialInteractionFact proposed)
    {
        if (IsProposal || proposed?.IsProposal != true) return false;
        var b = proposed.data;
        return data.kind == b.kind && data.operationId == b.operationId && data.actorId == b.actorId
            && data.actorName == b.actorName && data.targetId == b.targetId && data.targetName == b.targetName
            && data.absoluteDay == b.absoluteDay && data.conflictId == b.conflictId && data.severity == b.severity
            && data.origin == b.origin && data.customerId == b.customerId && data.instigatorCultureId == b.instigatorCultureId
            && data.targetCultureId == b.targetCultureId && data.facilityId == b.facilityId && data.facilityName == b.facilityName
            && data.positionX == b.positionX && data.positionY == b.positionY && data.offenseId == b.offenseId
            && data.offendingPublicationId == b.offendingPublicationId && data.rootOperationId == b.rootOperationId
            && data.causationRelationId == b.causationRelationId && data.restitutionProvided == b.restitutionProvided;
    }
}

public sealed class SocialConflictFact : SocialInteractionFact
{
    public SocialConflictFact(SocialInteractionFactData data, bool isProposal = false) : base(data, isProposal)
    { if (data.kind == SocialInteractionKind.Apology) throw new ArgumentException("conflict-fact-kind-invalid"); }
}
public sealed class SocialApologyFact : SocialInteractionFact
{
    public SocialApologyFact(SocialInteractionFactData data, bool isProposal = false) : base(data, isProposal)
    { if (data.kind != SocialInteractionKind.Apology) throw new ArgumentException("apology-fact-kind-invalid"); }
}

public readonly struct SocialOffenseReference
{
    public SocialOffenseReference(string publicationId, string operationId, string kind)
    { PublicationId = publicationId; OperationId = operationId; Kind = kind; }
    public string PublicationId { get; }
    public string OperationId { get; }
    public string Kind { get; }
}
public interface ISocialOffenseHistory
{
    bool TryFind(string offenderId, string recipientId, out SocialOffenseReference cause);
    bool TryGet(string publicationId, string offenderId, string recipientId, string relationKind, out SocialOffenseReference cause);
}

public readonly struct SocialInsultAnsweredEvent
{
    public SocialInsultAnsweredEvent(string targetId, string instigatorName)
    { TargetId = targetId; InstigatorName = instigatorName; }
    public string TargetId { get; }
    public string InstigatorName { get; }
}
public readonly struct MinionSocialConflictAppliedEvent
{
    public MinionSocialConflictAppliedEvent(string minionId, string residentId, string residentName, bool brawl)
    { MinionId = minionId; ResidentId = residentId; ResidentName = residentName; Brawl = brawl; }
    public string MinionId { get; }
    public string ResidentId { get; }
    public string ResidentName { get; }
    public bool Brawl { get; }
}
