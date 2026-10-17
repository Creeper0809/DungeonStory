using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class IdleSocialInteractionFactData
{
    public string operationId = string.Empty;
    public string actorId = string.Empty;
    public string actorName = string.Empty;
    public string targetId = string.Empty;
    public string targetName = string.Empty;
    public string behaviorId = string.Empty;
    public string reasonId = string.Empty;
    public int absoluteDay;
    public float actorRelationshipBefore;
    public float actorRelationshipAfter;
    public float targetRelationshipBefore;
    public float targetRelationshipAfter;
    public float moodBefore;
    public float moodAfter;
    public IdleSocialInteractionFactData Clone() => (IdleSocialInteractionFactData)MemberwiseClone();
}

/// <summary>Historical social changes, independent of current mood, actors, or narrative policy.</summary>
public sealed class IdleSocialInteractionResolvedFact
{
    private readonly IdleSocialInteractionFactData data;
    public IdleSocialInteractionResolvedFact(IdleSocialInteractionFactData data, bool isProposal = false)
    {
        if (data == null || !DomainFactValueSyntax.IsIdentity(data.operationId)
            || !DomainFactValueSyntax.IsIdentity(data.actorId) || !DomainFactValueSyntax.IsIdentity(data.targetId)
            || data.actorId == data.targetId || !Name(data.actorName) || !Name(data.targetName)
            || ReasonFor(data.behaviorId).Length == 0 || data.reasonId != ReasonFor(data.behaviorId)
            || data.absoluteDay < 0 || !Sentiment(data.actorRelationshipBefore) || !Sentiment(data.actorRelationshipAfter)
            || !Sentiment(data.targetRelationshipBefore) || !Sentiment(data.targetRelationshipAfter)
            || !Mood(data.moodBefore) || !Mood(data.moodAfter)
            || data.behaviorId == "social:encourage" && (data.actorRelationshipBefore != data.actorRelationshipAfter
                || data.targetRelationshipBefore != data.targetRelationshipAfter)
            || data.behaviorId == "social:formal-etiquette" && data.actorRelationshipBefore != data.actorRelationshipAfter
            || data.behaviorId != "social:encourage" && data.moodBefore != data.moodAfter
            || isProposal && (data.actorRelationshipBefore != data.actorRelationshipAfter
                || data.targetRelationshipBefore != data.targetRelationshipAfter || data.moodBefore != data.moodAfter))
            throw new ArgumentException("idle-social-fact-invalid");
        this.data = data.Clone(); IsProposal = isProposal;
    }
    public bool IsProposal { get; }
    public string OperationId => data.operationId;
    public string ActorId => data.actorId;
    public string TargetId => data.targetId;
    public int AbsoluteDay => data.absoluteDay;
    public IdleSocialInteractionFactData Snapshot() => data.Clone();
    public IdleSocialInteractionResolvedFact WithResult(float actorRelationship, float targetRelationship, float mood)
    {
        var actual = Snapshot(); actual.actorRelationshipAfter = actorRelationship;
        actual.targetRelationshipAfter = targetRelationship; actual.moodAfter = mood;
        return new IdleSocialInteractionResolvedFact(actual);
    }
    public bool MatchesProposal(IdleSocialInteractionResolvedFact proposal)
    {
        if (IsProposal || proposal?.IsProposal != true) return false;
        var other = proposal.data;
        return data.operationId == other.operationId && data.actorId == other.actorId && data.actorName == other.actorName
            && data.targetId == other.targetId && data.targetName == other.targetName && data.behaviorId == other.behaviorId
            && data.reasonId == other.reasonId && data.absoluteDay == other.absoluteDay
            && data.actorRelationshipBefore == other.actorRelationshipBefore
            && data.targetRelationshipBefore == other.targetRelationshipBefore && data.moodBefore == other.moodBefore;
    }
    public static string ReasonFor(string behavior) => behavior switch
    {
        "social:reconcile" => "strained-relationship",
        "social:encourage" => "low-mood",
        "social:formal-etiquette" => "owner-etiquette",
        _ => string.Empty
    };
    private static bool Sentiment(float value) => float.IsFinite(value) && value >= -1f && value <= 1f;
    private static bool Mood(float value) => float.IsFinite(value) && value >= 0f && value <= 100f;
    private static bool Name(string value) => DomainFactValueSyntax.IsText(value, DomainFactValueSyntax.MaximumNameCharacters, false);
}
