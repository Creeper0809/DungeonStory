using System;

/// <summary>
/// Receipt absoluteDay is occurrence time at day precision. Publication time is
/// observed separately and never enters the immutable receipt/deduplication hash.
/// Version zero is explicitly unobserved historical/fixture metadata, not day zero.
/// </summary>
public static class GameplayOutcomeDeliveryTime
{
    public const int CurrentVersion = 1;
    public const string CalendarClockId = "game-calendar:absolute-hour@1";

    public static bool TryReadCalendarHour(IGameCalendar calendar, out long absoluteHour)
    {
        absoluteHour = calendar.AbsoluteHour;
        return calendar.Day >= 1 && calendar.Hour >= 0 && calendar.Hour < GameCalendarRules.HoursPerDay
            && absoluteHour == OccurrenceStartHour(calendar.Day) + calendar.Hour;
    }

    public static string ClassifyDelay(int occurredDay, long recordedHour, int faultCount) =>
        faultCount > 0 ? "delivery-fault-retry"
        : recordedHour / GameCalendarRules.HoursPerDay + 1L > occurredDay ? "committed-outbox-delayed"
        : "same-day-publication";

    public static void Validate(GameplayOutcomeSnapshot record, bool published, long ledgerRevision)
    {
        if (record == null) throw new InvalidOperationException("Delivery metadata requires an outcome.");
        ValidateDelivery(record.deliveryMetadataVersion, record.deliveryClockId, record.absoluteDay,
            record.recordedAbsoluteHour, record.publishedRevision, record.deliveryFaultCount,
            record.deliveryDelayReason, published, ledgerRevision);
    }

    private static void ValidateDelivery(int version, string clockId, int day, long recordedHour,
        long publishedRevision, int faultCount, string reason, bool published, long ledgerRevision)
    {
        if (publishedRevision < 0L || publishedRevision > ledgerRevision
            || !published && publishedRevision != 0L || faultCount < 0)
            throw new InvalidOperationException("Outcome publication revision is invalid.");
        if (version == 0)
        {
            if (!string.IsNullOrEmpty(clockId) || recordedHour != 0L || !string.IsNullOrEmpty(reason))
                throw new InvalidOperationException("Unobserved delivery metadata contains fabricated partial time.");
            return;
        }
        if (version != CurrentVersion || !published || clockId != CalendarClockId || publishedRevision <= 0L
            || recordedHour < 0L || day < 1 || OccurrenceStartHour(day) > recordedHour
            || reason != ClassifyDelay(day, recordedHour, faultCount))
            throw new InvalidOperationException("Outcome delivery clock, occurrence/publication time or delay reason is invalid.");
    }

    public static void Validate(GameplayOutcomeTombstoneSnapshot record, long ledgerRevision)
    {
        if (record == null) throw new InvalidOperationException("Tombstone temporal metadata is missing.");
        if (record.temporalMetadataVersion == 0)
        {
            if (record.absoluteDay != 0 || !string.IsNullOrEmpty(record.parentRunId) || record.parentSequence != 0L
                || record.deliveryMetadataVersion != 0 || !string.IsNullOrEmpty(record.deliveryClockId)
                || record.recordedAbsoluteHour != 0L || record.publishedRevision != 0L
                || record.deliveryFaultCount != 0 || !string.IsNullOrEmpty(record.deliveryDelayReason))
                throw new InvalidOperationException("Historical tombstone contains fabricated partial temporal metadata.");
            return;
        }
        if (record.temporalMetadataVersion != CurrentVersion || record.absoluteDay < 0
            || (string.IsNullOrEmpty(record.parentRunId) ? record.parentSequence != 0L
                : record.parentSequence <= 0L || record.parentRunId != record.runId || record.parentSequence == record.sequence))
            throw new InvalidOperationException("Tombstone occurrence/parent metadata is invalid.");
        ValidateDelivery(record.deliveryMetadataVersion, record.deliveryClockId, record.absoluteDay,
            record.recordedAbsoluteHour, record.publishedRevision, record.deliveryFaultCount,
            record.deliveryDelayReason, true, ledgerRevision);
    }

    public static void RequireCapturedAt(GameplayOutcomeTombstoneSnapshot record, long capturedAbsoluteHour,
        bool requireObservedPublication)
    {
        Validate(record, long.MaxValue);
        if (capturedAbsoluteHour < 0L) throw new InvalidOperationException("Tombstone capture calendar is invalid.");
        if (requireObservedPublication && (record.temporalMetadataVersion != CurrentVersion
            || record.deliveryMetadataVersion != CurrentVersion))
            throw new InvalidOperationException("Tombstone time was not observed; it is not a clocked training source.");
        if (record.temporalMetadataVersion == CurrentVersion
            && (OccurrenceStartHour(record.absoluteDay) > capturedAbsoluteHour
                || record.deliveryMetadataVersion == CurrentVersion && record.recordedAbsoluteHour > capturedAbsoluteHour))
            throw new InvalidOperationException("Tombstone occurrence/publication is after its capture calendar.");
    }

    internal static bool TryValidateParent(GameplayOutcomeLedgerRuntimeState state, GameplayOutcomeId parent,
        int childDay, bool allowUnknownHistoricalParent, out string failure)
    {
        failure = string.Empty;
        int parentDay;
        if (state.ActiveByOutcomeId.TryGetValue(parent, out int pageIndex))
            parentDay = state.Pool.Get(pageIndex).AbsoluteDay;
        else if (state.TombstoneByOutcomeId.TryGetValue(parent, out int tombstoneIndex))
        {
            GameplayOutcomeTombstoneSnapshot tombstone = state.Tombstones[tombstoneIndex];
            if (tombstone.temporalMetadataVersion == 0)
            {
                if (allowUnknownHistoricalParent) return true;
                failure = "parent-occurrence-time-unobserved";
                return false;
            }
            parentDay = tombstone.absoluteDay;
        }
        else { failure = "parent-outcome-reference-missing"; return false; }
        if (parentDay > childDay) { failure = "parent-occurs-after-child"; return false; }
        return true;
    }

    /// <summary>Capture/export gate; do not compare a restore candidate against the pre-restore live calendar.</summary>
    public static void RequireCapturedAt(GameplayOutcomeSnapshot record, long capturedAbsoluteHour,
        bool requireObservedPublication)
    {
        if (record == null || capturedAbsoluteHour < 0L || record.absoluteDay < 0
            || OccurrenceStartHour(record.absoluteDay) > capturedAbsoluteHour)
            throw new InvalidOperationException("Outcome occurs after its capture calendar.");
        Validate(record, record.lifecycle == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
            || record.lifecycle == GameplayOutcomeRecordLifecycle.PublishedAwaitingAcknowledgement, long.MaxValue);
        if (requireObservedPublication && record.deliveryMetadataVersion != CurrentVersion)
            throw new InvalidOperationException("Outcome publication time was not observed; it is not a clocked training source.");
        if (record.deliveryMetadataVersion == CurrentVersion && record.recordedAbsoluteHour > capturedAbsoluteHour)
            throw new InvalidOperationException("Outcome was published after its capture calendar.");
    }

    public static long OccurrenceStartHour(int absoluteDay) =>
        ((long)absoluteDay - 1L) * GameCalendarRules.HoursPerDay;

    internal static long QueryTimeUpperBound(int day, int deliveryVersion, long recordedHour) =>
        Math.Max(OccurrenceStartHour(day), deliveryVersion == CurrentVersion ? recordedHour : -1L);

    public static void RequireCapturedAt(CompactedNarrativeMemorySnapshot memory, long capturedAbsoluteHour)
    {
        if (memory == null || capturedAbsoluteHour < 0L || memory.firstDay < 0
            || memory.lastDay < memory.firstDay
            || OccurrenceStartHour(memory.lastDay) > capturedAbsoluteHour)
            throw new InvalidOperationException("Compacted memory source date range is invalid or after its capture calendar.");
    }
}
