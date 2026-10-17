using System;

public enum OwnerRunObservationKind
{
    InvalidCandidateIndex,
    MissingOwnerData,
    NotOwnerCandidate,
    OwnerSelected,
    RunCompleted
}

/// <summary>
/// A frozen owner-run transition for presentation observers. Owner selection
/// and run completion remain owned by the run manager.
/// </summary>
public readonly struct OwnerRunObservation
{
    public OwnerRunObservation(
        OwnerRunObservationKind kind,
        string displayName = "",
        string reason = "",
        DungeonRunOutcome outcome = DungeonRunOutcome.None)
    {
        if (!Enum.IsDefined(typeof(OwnerRunObservationKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
        if (!Enum.IsDefined(typeof(DungeonRunOutcome), outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
        }

        displayName ??= string.Empty;
        reason ??= string.Empty;
        if (kind == OwnerRunObservationKind.RunCompleted)
        {
            if (outcome == DungeonRunOutcome.None
                || string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException(
                    "A completed run requires an outcome and reason.");
            }
        }
        else if (outcome != DungeonRunOutcome.None || reason.Length != 0)
        {
            throw new ArgumentException(
                "Only completed runs may carry an outcome or reason.");
        }

        bool requiresName = kind == OwnerRunObservationKind.NotOwnerCandidate
            || kind == OwnerRunObservationKind.OwnerSelected;
        if (requiresName != !string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "The owner observation display-name shape is invalid.",
                nameof(displayName));
        }

        Kind = kind;
        DisplayName = displayName;
        Reason = reason;
        Outcome = outcome;
    }

    public OwnerRunObservationKind Kind { get; }
    public string DisplayName { get; }
    public string Reason { get; }
    public DungeonRunOutcome Outcome { get; }
}
