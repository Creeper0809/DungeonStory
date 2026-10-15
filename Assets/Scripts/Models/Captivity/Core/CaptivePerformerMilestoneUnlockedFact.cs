using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class CaptivePerformerMilestoneFactData
{
    public string captiveId;
    public string displayName;
    public int threshold;
    public float fame;
    public long revision;
    public int absoluteDay;
    public CaptivePerformerMilestoneFactData Clone() => (CaptivePerformerMilestoneFactData)MemberwiseClone();
}

/// <summary>The earned privilege as it happened; no log identity, score, or display policy.</summary>
public sealed class CaptivePerformerMilestoneUnlockedFact
{
    private readonly CaptivePerformerMilestoneFactData value;
    public CaptivePerformerMilestoneUnlockedFact(CaptivePerformerMilestoneFactData source)
    {
        if (source == null || !DomainFactValueSyntax.IsIdentity(source.captiveId)
            || !DomainFactValueSyntax.IsText(source.displayName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || source.displayName != source.displayName.Trim()
            || source.threshold is not (50 or 75 or 100)
            || float.IsNaN(source.fame) || float.IsInfinity(source.fame)
            || source.fame < source.threshold || source.fame > 100f
            || source.revision <= 0L || source.absoluteDay < 0)
            throw new ArgumentException("captive-performer-milestone-fact-invalid");
        value = source.Clone();
    }
    public CaptivePerformerMilestoneFactData Snapshot() => value.Clone();
}

[Serializable]
public sealed class CaptivePerformerMilestoneObservationData
{
    public CaptivePerformerMilestoneFactData fact;
    public string publicationKey;
    public bool pending;
    public bool observersPending;
    public CaptivePerformerMilestoneObservationData Clone() => new()
    {
        fact = fact?.Clone(), publicationKey = publicationKey,
        pending = pending, observersPending = observersPending
    };
}
