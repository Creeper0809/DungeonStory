using System;

/// <summary>The exact policy transition retained by the reproduction aggregate.</summary>
public sealed class FamilyPlanningPolicyChangedFact
{
    public FamilyPlanningPolicyChangedFact(
        FamilyPlanningPolicy before,
        FamilyPlanningPolicy after,
        long beforeRevision,
        long afterRevision,
        string source,
        int absoluteDay)
    {
        string normalizedSource = source?.Trim() ?? string.Empty;
        if (!Enum.IsDefined(typeof(FamilyPlanningPolicy), before)
            || !Enum.IsDefined(typeof(FamilyPlanningPolicy), after)
            || before == after
            || beforeRevision < 0L
            || beforeRevision == long.MaxValue
            || afterRevision != beforeRevision + 1L
            || !ReproductionStableIdSyntax.IsValid(normalizedSource)
            || absoluteDay < 0)
        {
            throw new ArgumentException(
                "An exact family-planning policy transition is required.");
        }
        Before = before;
        After = after;
        BeforeRevision = beforeRevision;
        AfterRevision = afterRevision;
        Source = normalizedSource;
        AbsoluteDay = absoluteDay;
    }

    public FamilyPlanningPolicy Before { get; }
    public FamilyPlanningPolicy After { get; }
    public long BeforeRevision { get; }
    public long AfterRevision { get; }
    public string Source { get; }
    public int AbsoluteDay { get; }

    public FamilyPlanningPolicyPendingChangeSaveData Snapshot() => new()
    {
        before = Before,
        after = After,
        beforeRevision = BeforeRevision,
        afterRevision = AfterRevision,
        source = Source,
        absoluteDay = AbsoluteDay
    };

    public static FamilyPlanningPolicyChangedFact Restore(
        FamilyPlanningPolicyPendingChangeSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return new FamilyPlanningPolicyChangedFact(
            source.before,
            source.after,
            source.beforeRevision,
            source.afterRevision,
            source.source,
            source.absoluteDay);
    }
}
