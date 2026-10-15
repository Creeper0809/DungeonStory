using System;

public readonly struct OwnerRunCompletionFact
{
    private readonly OwnerRunCompletionFactData data;
    public OwnerRunCompletionFact(OwnerRunCompletionFactData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        data = source.Clone();
    }
    public OwnerRunCompletionFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("owner-run-completion-fact-empty");
}
