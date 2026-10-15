using System;

// Frozen observations of the wildlife owner. No receipt, ledger or memory policy.
public readonly struct WildlifeCommandFact
{
    private readonly WildlifeGameplayOutcomeSaveData value;
    public WildlifeCommandFact(WildlifeGameplayOutcomeSaveData source, bool proposed = false)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate(proposed);
        if (source.profile != WildlifeGameplayOutcomeProfile.Command)
            throw new ArgumentException("wildlife-command-fact-profile-invalid");
        value = source.Clone();
    }
    public WildlifeGameplayOutcomeSaveData Snapshot() => value?.Clone()
        ?? throw new InvalidOperationException("wildlife-command-fact-uninitialized");
}

public readonly struct WildlifeActionFact
{
    private readonly WildlifeGameplayOutcomeSaveData value;
    public WildlifeActionFact(WildlifeGameplayOutcomeSaveData source, bool proposed = false)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate(proposed);
        if (source.profile != WildlifeGameplayOutcomeProfile.Action)
            throw new ArgumentException("wildlife-action-fact-profile-invalid");
        value = source.Clone();
    }
    public WildlifeGameplayOutcomeSaveData Snapshot() => value?.Clone()
        ?? throw new InvalidOperationException("wildlife-action-fact-uninitialized");
}
