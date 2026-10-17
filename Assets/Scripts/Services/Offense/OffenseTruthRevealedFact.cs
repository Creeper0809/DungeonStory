using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class OffenseTruthFactData
{
    public string targetId = string.Empty;
    public long ownerRevision;
    public string targetDisplayName = string.Empty;
    public string truthTitle = string.Empty;
    public string truthText = string.Empty;
    public int absoluteDay;
    public OffenseTruthFactData Clone() => (OffenseTruthFactData)MemberwiseClone();
}

/// <summary>Historical campaign fact; no ledger, presentation, or memory-policy dependency.</summary>
public sealed class OffenseTruthRevealedFact
{
    private readonly OffenseTruthFactData value;
    public OffenseTruthRevealedFact(OffenseTruthFactData source)
    {
        if (source == null || !DomainFactValueSyntax.IsIdentity(source.targetId)
            || source.ownerRevision <= 0 || source.absoluteDay < 0
            || !Text(source.targetDisplayName, 256) || !Text(source.truthTitle, 1024)
            || !Text(source.truthText, 16384))
            throw new ArgumentException("offense-truth-fact-invalid");
        value = source.Clone();
    }
    public OffenseTruthFactData Snapshot() => value.Clone();
    public long OwnerRevision => value.ownerRevision;
    private static bool Text(string value, int maximum) => !string.IsNullOrWhiteSpace(value)
        && value == value.Trim() && value.Length <= maximum && value.IndexOf('\0') < 0;
}

[Serializable]
public sealed class OffenseTruthObservationData
{
    // Explicit presence avoids Unity JSON's null-class materialization.
    public bool hasFact;
    public OffenseTruthFactData fact = new();
    public string publicationKey = string.Empty;
    public bool pending;
    public bool notificationPending;
    public OffenseTruthObservationData Clone() => new()
    { hasFact = hasFact, fact = fact?.Clone(), publicationKey = publicationKey, pending = pending, notificationPending = notificationPending };

    public void Validate(string revealedTarget, int completedTargetCount)
    {
        if (fact == null || publicationKey == null || revealedTarget == null)
            throw new ArgumentException("offense-truth-observation-null");
        if (!hasFact)
        {
            if (revealedTarget.Length != 0 || pending || notificationPending || publicationKey.Length != 0
                || fact.targetId != string.Empty || fact.ownerRevision != 0 || fact.absoluteDay != 0
                || fact.targetDisplayName != string.Empty || fact.truthTitle != string.Empty || fact.truthText != string.Empty)
                throw new ArgumentException("offense-truth-observation-missing");
            return;
        }
        _ = new OffenseTruthRevealedFact(fact);
        if (fact.targetId != revealedTarget || fact.ownerRevision > completedTargetCount
            || !DomainFactValueSyntax.IsText(publicationKey, 1024, false)
            || pending && !notificationPending)
            throw new ArgumentException("offense-truth-observation-incoherent");
    }
}
