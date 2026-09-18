using System;
using DungeonStory.Narrative.Korean;

public static class ExternalFactionOutcomeIds
{
    public const string TruthRevealProducerId = "offense.truth-reveal";

    public static readonly GameplayOutcomeTypeId TruthRevealed =
        new("offense.truth-revealed");
    public static readonly GameplayEntityKindId ExpeditionTargetKind =
        new("expedition-target");
    public static readonly GameplayRoleId RevealedTargetRole =
        new("revealed-target");
    public static readonly GameplayMetricId TruthRevealedMetric =
        new("truth.revealed");
    public static readonly GameplayMetricUnitId BooleanUnit = new("boolean");
    public static readonly GameplayOutcomeTagId ExpeditionTag =
        new("expedition");
}

/// <summary>
/// Exact, immutable producer-time facts for a committed campaign truth reveal.
/// Narrative text is provenance-hashed; the target title is captured as the
/// historical display snapshot used by the canonical projector.
/// </summary>
public readonly struct OffenseTruthRevealOutcomeReceipt
{
    public OffenseTruthRevealOutcomeReceipt(
        string targetId,
        long ownerRevision,
        string targetDisplayName,
        string truthTitle,
        string truthText,
        int absoluteDay)
    {
        TargetId = GameplayOutcomeStableIdSyntax.Require(
            targetId,
            nameof(targetId));
        OperationId = new GameplayOperationId(
            "offense-truth-reveal:" + TargetId);
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (string.IsNullOrWhiteSpace(targetDisplayName))
            throw new ArgumentException(
                "A producer-time target display name is required.",
                nameof(targetDisplayName));
        if (string.IsNullOrWhiteSpace(truthTitle))
            throw new ArgumentException(
                "A truth title is required.",
                nameof(truthTitle));
        if (string.IsNullOrWhiteSpace(truthText))
            throw new ArgumentException(
                "Truth narrative provenance cannot be empty.",
                nameof(truthText));
        if (absoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));

        OwnerRevision = ownerRevision;
        string normalizedDisplay = targetDisplayName.Trim();
        TargetDisplayName = new KoreanNameSnapshot(
            normalizedDisplay,
            "offense-target-display-v1:"
                + NarrativeInferenceHash.ComputeSha256Utf8(
                    normalizedDisplay),
            KoreanPronunciationHint.AutoHangulDisplay(
                "external-faction-korean-v1"),
            "ko-KR");
        TruthTitle = truthTitle.Trim();
        TruthText = truthText.Trim();
        AbsoluteDay = absoluteDay;
    }

    public string TargetId { get; }
    public long OwnerRevision { get; }
    public KoreanNameSnapshot TargetDisplayName { get; }
    public string TruthTitle { get; }
    public string TruthText { get; }
    public int AbsoluteDay { get; }
    public GameplayOperationId OperationId { get; }
    public GameplayResultKey ResultKey => new(
        ExternalFactionOutcomeIds.TruthRevealProducerId,
        OperationId,
        OwnerRevision,
        0);
}

public readonly struct PreparedExternalFactionOutcome
{
    internal PreparedExternalFactionOutcome(
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
