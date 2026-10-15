using System;

// A gameplay participant freezes downstream state before terminal publication.
// It owns no log identities, descriptors, scoring or formatting.
public interface IRunCompletionPreparation
{
    IRunCompletionPreparedState Prepare(OwnerRunCompletionFactData fact);
}

public interface IRunCompletionPreparedState
{
    void Publish();
    void Cancel();
}

[Serializable]
public sealed class OwnerRunCompletionFactData
{
    public int runSeed;
    public string ownerId;
    public string displayName;
    public DungeonRunOutcome outcome;
    public int absoluteDay;
    public string reason;

    public OwnerRunCompletionFactData Clone() => (OwnerRunCompletionFactData)MemberwiseClone();

    public void Validate()
    {
        if (runSeed == 0 || absoluteDay < 1
            || string.IsNullOrWhiteSpace(ownerId) || ownerId != ownerId.Trim()
            || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(reason)
            || outcome is not (DungeonRunOutcome.Victory or DungeonRunOutcome.Defeat))
            throw new InvalidOperationException("owner-run-completion-fact-invalid");
    }
}

[Serializable]
public sealed class OwnerRunCompletionPendingData
{
    public string publicationKey;
    public OwnerRunCompletionFactData fact;

    public OwnerRunCompletionPendingData Clone() => new()
    { publicationKey = publicationKey, fact = fact?.Clone() };

    public void Validate(DungeonRunOutcome outcome, int absoluteDay)
    {
        if (string.IsNullOrWhiteSpace(publicationKey) || fact == null)
            throw new InvalidOperationException("owner-run-completion-pending-invalid");
        fact.Validate();
        if (fact.outcome != outcome || fact.absoluteDay != absoluteDay)
            throw new InvalidOperationException("owner-run-completion-state-conflict");
    }
}
