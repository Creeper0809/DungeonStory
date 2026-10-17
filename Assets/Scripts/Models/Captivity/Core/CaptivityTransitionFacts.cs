using System;
using DungeonStory.Foundation.Facts;

/// <summary>Frozen rehabilitation progress after its owner applies work, food, and social-state changes.</summary>
public sealed class CaptivityRehabilitationProgressedFact
{
    private readonly CaptivityRehabilitationTerminalState value;
    public string CaptiveId { get; }
    public long OwnerRevision => value.outcomeRevision;
    public CaptivityRehabilitationProgressedFact(string captiveId, CaptivityRehabilitationTerminalState source)
    {
        if (!DomainFactValueSyntax.IsIdentity(captiveId) || source == null || !source.HasOutcome
            || !DomainFactValueSyntax.IsIdentity(source.wardenId) || !DomainFactValueSyntax.IsIdentity(source.facilityId)
            || !CaptivityTransitionFactRules.Name(source.captiveDisplayName)
            || !CaptivityTransitionFactRules.Name(source.wardenDisplayName)
            || !CaptivityTransitionFactRules.Name(source.facilityDisplayName) || source.absoluteDay < 0
            || !float.IsFinite(source.approvedWork) || source.approvedWork <= 0f
            || !CaptivityTransitionFactRules.NonNegative(source.workBefore) || !CaptivityTransitionFactRules.NonNegative(source.workAfter)
            || source.rehabilitationDaysBefore < 0 || source.rehabilitationDaysAfter < 0 || source.foodConsumed < 0
            || !CaptivityTransitionFactRules.Percentage(source.trustBefore) || !CaptivityTransitionFactRules.Percentage(source.trustAfter)
            || !CaptivityTransitionFactRules.Percentage(source.grudgeBefore) || !CaptivityTransitionFactRules.Percentage(source.grudgeAfter)
            || !CaptivityTransitionFactRules.Percentage(source.corruptionBefore) || !CaptivityTransitionFactRules.Percentage(source.corruptionAfter))
            throw new ArgumentException("captivity-rehabilitation-fact-invalid");
        float accumulated = source.workBefore + source.approvedWork;
        bool coherent = source.completedDay
            ? source.foodConsumed == CaptivityStateTransitionRules.RehabilitationFoodCost
                && source.workAfter == 0f && (long)source.rehabilitationDaysAfter == (long)source.rehabilitationDaysBefore + 1L
                && CaptivityTransitionFactRules.Near(accumulated, CaptivityStateTransitionRules.RehabilitationRequiredWork)
                && source.trustAfter >= source.trustBefore && source.grudgeAfter <= source.grudgeBefore
                && source.corruptionAfter <= source.corruptionBefore
            : source.foodConsumed == 0 && source.rehabilitationDaysAfter == source.rehabilitationDaysBefore
                && CaptivityTransitionFactRules.Near(source.workAfter, accumulated)
                && source.workAfter + 0.001f < CaptivityStateTransitionRules.RehabilitationRequiredWork
                && source.trustAfter == source.trustBefore && source.grudgeAfter == source.grudgeBefore
                && source.corruptionAfter == source.corruptionBefore;
        if (!coherent) throw new ArgumentException("captivity-rehabilitation-fact-incoherent");
        CaptiveId = captiveId; value = source.Clone();
        value.publicationKey = string.Empty; value.outcomePending = false;
    }
    public CaptivityRehabilitationTerminalState Snapshot() => value.Clone();
}

/// <summary>Frozen release facts; command progress and notification acknowledgments remain with the owner.</summary>
public sealed class CaptivityReleasedFact
{
    private readonly CaptivityReleaseTerminalState value;
    public string CaptiveId { get; }
    public long OwnerRevision => value.outcomeRevision;
    public CaptivityReleasedFact(string captiveId, CaptivityReleaseTerminalState source)
    {
        if (!DomainFactValueSyntax.IsIdentity(captiveId) || source == null || source.outcomeRevision != 1L
            || !DomainFactValueSyntax.IsIdentity(source.deciderId) || !CaptivityTransitionFactRules.Name(source.captiveDisplayName)
            || !CaptivityTransitionFactRules.Name(source.deciderDisplayName)
            || (source.facilityId == string.Empty ? source.facilityDisplayName != string.Empty
                : !DomainFactValueSyntax.IsIdentity(source.facilityId) || !CaptivityTransitionFactRules.Name(source.facilityDisplayName))
            || source.absoluteDay < 0 || !source.inCustodyBefore || !CaptivityTransitionFactRules.InCustody(source.statusBefore)
            || source.statusAfter != CaptivityStatus.Released || source.inCustodyAfter || source.restrainedAfter
            || !DomainFactValueSyntax.IsIdentity(source.actorTypeBefore) || source.actorTypeAfter != "Intruder"
            || !DomainFactValueSyntax.IsIdentity(source.lifecycleBefore) || source.lifecycleAfter != "Active"
            || source.aiPausedAfter || source.doorCaptiveAfter || !source.physicalInputsReturned)
            throw new ArgumentException("captivity-release-fact-invalid");
        CaptiveId = captiveId; value = source.Clone();
        value.publicationKey = string.Empty; value.outcomePending = false;
        value.effectsApplied = false; value.observersPending = false; value.decisionPublished = false;
    }
    public CaptivityReleaseTerminalState Snapshot() => value.Clone();
}

internal static class CaptivityTransitionFactRules
{
    internal static bool Name(string value) => DomainFactValueSyntax.IsText(value, DomainFactValueSyntax.MaximumNameCharacters, false)
        && value == value.Trim();
    internal static bool NonNegative(float value) => float.IsFinite(value) && value >= 0f;
    internal static bool Percentage(float value) => NonNegative(value) && value <= 100f;
    internal static bool Near(float left, float right) => float.IsFinite(left) && Math.Abs(left - right) <= 0.001f;
    internal static bool InCustody(CaptivityStatus status) => status is CaptivityStatus.Stabilizing
        or CaptivityStatus.AwaitingEscort or CaptivityStatus.Escorting or CaptivityStatus.Confined
        or CaptivityStatus.Labor or CaptivityStatus.Interaction or CaptivityStatus.Performer
        or CaptivityStatus.EscapeAttempt or CaptivityStatus.Ransom;
}
