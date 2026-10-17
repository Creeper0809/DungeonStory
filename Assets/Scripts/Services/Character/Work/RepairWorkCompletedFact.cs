using System;

/// <summary>Immutable applied repair values; publication identities are not business facts.</summary>
public sealed class RepairWorkCompletedFact
{
    private readonly RepairWorkOutcomeSaveData frozen;

    public RepairWorkCompletedFact(RepairWorkOutcomeSaveData owner)
    {
        if (!RepairWorkOutcomeValidation.IsValid(owner, true, out string failure))
            throw new ArgumentException(failure, nameof(owner));
        frozen = owner.Clone();
        frozen.publicationKey = string.Empty;
        frozen.publicationConfirmed = false;
        frozen.completionPublicationKey = string.Empty;
    }

    public RepairWorkOutcomeSaveData Snapshot() => frozen.Clone();
}

/// <summary>Accepted repair work awaiting its mandatory worker completion reaction.</summary>
public sealed class RepairWorkCompletionFact
{
    public const string TerminalOwnerId = "repair-work-outcome";
    private readonly RepairWorkCompletedFact completed;

    public RepairWorkCompletionFact(RepairWorkOutcomeSaveData owner)
    {
        completed = new RepairWorkCompletedFact(owner);
        if (!owner.publicationConfirmed || string.IsNullOrWhiteSpace(owner.publicationKey))
            throw new ArgumentException("repair-completion-requires-confirmed-publication", nameof(owner));
        ParentPublicationKey = owner.publicationKey;
    }

    public string ParentPublicationKey { get; }
    public RepairWorkOutcomeSaveData Snapshot() => completed.Snapshot();
}
