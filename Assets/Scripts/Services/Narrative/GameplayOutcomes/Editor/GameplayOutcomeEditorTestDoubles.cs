using System;
using System.Collections.Generic;

internal sealed class EmptyGameplayOutcomeNarrativeEvidenceQuery :
    IGameplayOutcomeNarrativeEvidenceQuery
{
    internal static EmptyGameplayOutcomeNarrativeEvidenceQuery Instance { get; } =
        new();

    private EmptyGameplayOutcomeNarrativeEvidenceQuery()
    {
    }

    public bool TryResolveExactBinding(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        out GameplayOutcomeNarrativeEvidenceSource source)
    {
        source = default;
        return false;
    }

    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetForEntity(
        GameplayEntityId entityId,
        int maximumCount,
        float minimumSalience,
        bool includeCompacted) =>
        Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();

    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetForCharacter(
        string characterPersistentId,
        int maximumCount,
        float minimumSalience,
        bool includeCompacted) =>
        Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();
}

internal sealed class EditorNoOpOffenseOutcomeCommitter :
    IOffenseTruthRevealOutcomeCommitter
{
    internal static EditorNoOpOffenseOutcomeCommitter Instance { get; } = new();

    private EditorNoOpOffenseOutcomeCommitter()
    {
    }

    public bool TryPrepare(
        in OffenseTruthRevealOutcomeReceipt receipt,
        out PreparedExternalFactionOutcome prepared,
        out string failureReason)
    {
        prepared = new PreparedExternalFactionOutcome(
            default,
            receipt.ResultKey,
            alreadyCommitted: true);
        failureReason = string.Empty;
        return true;
    }

    public void Cancel(in PreparedExternalFactionOutcome prepared)
    {
    }

    public bool TryCommit(
        in PreparedExternalFactionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason)
    {
        failureReason = string.Empty;
        return prepared.ResultKey.IsValid && expectedOwnerRevision > 0L;
    }
}

internal sealed class EditorFixedGameCalendar : IGameCalendar
{
    internal static EditorFixedGameCalendar Instance { get; } = new();

    private EditorFixedGameCalendar()
    {
    }

    public int Day { get; private set; } = 1;
    public int Hour { get; private set; }
    public int Year => 1;
    public int DayOfYear => Day;
    public Season Season => default;
    public int DayOfSeason => Day;
    public long AbsoluteHour => ((long)Day * 24L) + Hour;
    public float ElapsedSeconds => 0f;
    public TimeOfDay TimeOfDay => default;
    public bool IsRunning { get; private set; }
    public CalendarDateTime Current => default;

    public CalendarDateTime GetRegionalTime(int utcOffsetHours) => default;

    public void Start() => IsRunning = true;

    public void SetDateTime(int day, int hour)
    {
        Day = Math.Max(1, day);
        Hour = Math.Clamp(hour, 0, 23);
    }
}

internal sealed class EditorNoOpEnvironmentOutcomeCommitter :
    IEnvironmentGameplayOutcomeCommitter
{
    internal static EditorNoOpEnvironmentOutcomeCommitter Instance { get; } =
        new();

    private EditorNoOpEnvironmentOutcomeCommitter()
    {
    }

    public bool TryReserve(
        in EnvironmentOutcomeReservationSpec spec,
        out ReservedEnvironmentOutcome reserved,
        out string failureReason)
    {
        reserved = default;
        failureReason = "editor-noop-environment-outcome";
        return false;
    }

    public bool TryWriteReserved<TReceipt>(
        in TReceipt receipt,
        in ReservedEnvironmentOutcome reserved,
        out PreparedEnvironmentOutcome prepared,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        prepared = default;
        failureReason = "editor-noop-environment-outcome";
        return false;
    }

    public bool TryPrepare<TReceipt>(
        in TReceipt receipt,
        out PreparedEnvironmentOutcome prepared,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        prepared = default;
        failureReason = "editor-noop-environment-outcome";
        return false;
    }

    public EnvironmentOutcomeCommitResult Commit(
        in PreparedEnvironmentOutcome prepared,
        long expectedOwnerRevision) => default;

    public EnvironmentOutcomeCommitResult Reconcile(GameplayResultKey resultKey) =>
        default;

    public bool IsCanonicalAcknowledgedReplay<TReceipt>(
        in TReceipt receipt,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        failureReason = "editor-noop-environment-outcome";
        return false;
    }

    public void Cancel(in PreparedEnvironmentOutcome prepared)
    {
    }

    public void Cancel(in ReservedEnvironmentOutcome reserved)
    {
    }
}
