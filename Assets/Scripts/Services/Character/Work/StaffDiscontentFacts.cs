using System;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

/// <summary>Immutable historical staff state, independent of log schemas, memory scores, and current actors.</summary>
public abstract class StaffDiscontentResolvedFact
{
    private readonly StaffDiscontentFactData value;
    protected StaffDiscontentResolvedFact(StaffDiscontentFactData source, StaffDiscontentFactKind kind)
    {
        if (source == null || source.kind != kind || source.revision <= 0L || source.absoluteDay < 0
            || source.before == null || source.after == null || source.before.staffId != source.after.staffId
            || source.responderId == null || source.responderDisplayName == null
            || float.IsNaN(source.resolvedMoodDelta) || float.IsInfinity(source.resolvedMoodDelta)
            || !source.moodApplied && source.resolvedMoodDelta != 0f)
            throw new ArgumentException("staff-discontent-fact-invalid");
        _ = StaffDiscontentFactValues.Restore(source.before);
        _ = StaffDiscontentFactValues.Restore(source.after);
        if (kind == StaffDiscontentFactKind.DailyTransition)
        {
            if (source.transition == StaffDiscontentOutcome.None || !Enum.IsDefined(typeof(StaffDiscontentOutcome), source.transition)
                || source.before.outcome != StaffDiscontentOutcome.None || source.after.outcome != source.transition
                || source.after.lastProcessedAbsoluteDay != source.absoluteDay
                || source.before.lastProcessedAbsoluteDay >= source.absoluteDay
                || source.responderId.Length != 0 || source.responderDisplayName.Length != 0
                || source.responseType != default || source.moodApplied)
                throw new ArgumentException("staff-discontent-transition-fact-incoherent");
        }
        else if (!Enum.IsDefined(typeof(StaffRebellionResponseType), source.responseType)
            || source.responseType == StaffRebellionResponseType.AutoSuppress
            || source.responseType != StaffRebellionResponseType.Calm && source.moodApplied
            || source.transition != StaffDiscontentOutcome.None
            || source.before.outcome != StaffDiscontentOutcome.None || source.after.outcome != StaffDiscontentOutcome.None
            || (source.responderId.Length == 0 ? source.responderDisplayName.Length != 0
                : !DomainFactValueSyntax.IsIdentity(source.responderId) || source.responderId == source.before.staffId
                    || !StaffDiscontentFactValues.Name(source.responderDisplayName)))
            throw new ArgumentException("staff-discontent-response-fact-incoherent");
        value = source.Clone();
    }
    public long OwnerRevision => value.revision;
    public string StaffId => value.after.staffId;
    public StaffDiscontentFactData Snapshot() => value.Clone();
    public string ComputeSemanticDigest()
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append("staff-discontent-fact-v1"); digest.AppendEnum(value.kind);
        digest.Append(value.revision); digest.Append(value.absoluteDay); digest.AppendEnum(value.transition);
        digest.AppendEnum(value.responseType); digest.Append(value.responderId); digest.Append(value.responderDisplayName);
        digest.Append(value.moodApplied); digest.AppendFloat(value.resolvedMoodDelta);
        Append(value.before); Append(value.after);
        return digest.ComputeSha256();
        void Append(DungeonStaffDiscontentRecordSaveData record)
        {
            digest.Append(record.staffId); digest.Append(record.displayName);
            digest.AppendEnum(record.stage); digest.AppendEnum(record.outcome); digest.AppendFloat(record.mood);
            digest.Append(record.lowMoodDays); digest.Append(record.localRebellionDays); digest.Append(record.lastProcessedAbsoluteDay);
            digest.Append(record.permanentLoss); digest.Append(record.departed); digest.Append(record.localRebellion);
            digest.Append(record.ownerThreat); digest.Append(record.isolated); digest.Append(record.suppressed);
        }
    }
}

public sealed class StaffDiscontentTransitionedFact : StaffDiscontentResolvedFact
{
    public StaffDiscontentTransitionedFact(StaffDiscontentFactData source) : base(source, StaffDiscontentFactKind.DailyTransition) { }
}
public sealed class StaffRebellionResponseResolvedFact : StaffDiscontentResolvedFact
{
    public StaffRebellionResponseResolvedFact(StaffDiscontentFactData source) : base(source, StaffDiscontentFactKind.Response) { }
}
public readonly struct StaffRebellionGuardsAssignedFact
{
    public StaffRebellionGuardsAssignedFact(string staffName, int count) { StaffName = staffName; Count = count; }
    public string StaffName { get; }
    public int Count { get; }
}

public static class StaffDiscontentFactValues
{
    public static bool SameState(DungeonStaffDiscontentRecordSaveData a, DungeonStaffDiscontentRecordSaveData b) => a != null && b != null
        && a.staffId == b.staffId && a.displayName == b.displayName && a.stage == b.stage && a.mood == b.mood
        && a.lowMoodDays == b.lowMoodDays && a.localRebellionDays == b.localRebellionDays
        && a.lastProcessedAbsoluteDay == b.lastProcessedAbsoluteDay && a.permanentLoss == b.permanentLoss
        && a.departed == b.departed && a.localRebellion == b.localRebellion && a.ownerThreat == b.ownerThreat
        && a.isolated == b.isolated && a.suppressed == b.suppressed;

    public static void ValidateObservation(StaffDiscontentObservationData value, StaffDiscontentFactKind kind, long revision)
    {
        if (value == null || value.publicationKey == null || revision < 0L)
            throw new ArgumentException("staff-discontent-observation-invalid");
        if (revision == 0L)
        {
            if (!IsEmpty(value.fact) || value.pending || value.notificationPending || value.publicationKey.Length > 0)
                throw new ArgumentException("staff-discontent-empty-observation-invalid");
            return;
        }
        if (kind == StaffDiscontentFactKind.DailyTransition) _ = new StaffDiscontentTransitionedFact(value.fact);
        else _ = new StaffRebellionResponseResolvedFact(value.fact);
        if (value.fact.revision != revision || !DomainFactValueSyntax.IsText(value.publicationKey, 1024, false)
            || value.pending && !value.notificationPending)
            throw new ArgumentException("staff-discontent-observation-incoherent");
    }
    public static bool IsEmpty(StaffDiscontentFactData fact) => fact != null
        && fact.kind == default && fact.revision == 0L && fact.absoluteDay == 0
        && fact.transition == default && fact.responseType == default
        && fact.responderId == string.Empty && fact.responderDisplayName == string.Empty
        && !fact.moodApplied && fact.resolvedMoodDelta == 0f
        && EmptyRecord(fact.before) && EmptyRecord(fact.after);

    private static bool EmptyRecord(DungeonStaffDiscontentRecordSaveData value) => value != null
        && value.staffId == string.Empty && value.displayName == string.Empty
        && value.stage == default && value.outcome == default && value.mood == 100f
        && value.lowMoodDays == 0 && value.localRebellionDays == 0 && value.lastProcessedAbsoluteDay == -1
        && !value.permanentLoss && !value.departed && !value.localRebellion && !value.ownerThreat
        && !value.isolated && !value.suppressed;
    public static DungeonStaffDiscontentRecordSaveData Capture(StaffDiscontentSnapshot value) => value == null
        ? throw new ArgumentNullException(nameof(value))
        : new DungeonStaffDiscontentRecordSaveData
        {
            staffId = value.staffId, displayName = value.displayName, stage = value.stage, outcome = value.outcome,
            mood = value.mood, lowMoodDays = value.lowMoodDays, localRebellionDays = value.localRebellionDays,
            lastProcessedAbsoluteDay = value.lastProcessedAbsoluteDay, permanentLoss = value.permanentLoss,
            departed = value.departed, localRebellion = value.localRebellion, ownerThreat = value.ownerThreat,
            isolated = value.isolated, suppressed = value.suppressed
        };
    public static StaffDiscontentSnapshot Restore(DungeonStaffDiscontentRecordSaveData value)
    {
        if (value == null || !DomainFactValueSyntax.IsIdentity(value.staffId) || !Name(value.displayName))
            throw new ArgumentException("staff-discontent-snapshot-invalid");
        return new StaffDiscontentSnapshot(value.staffId, value.displayName, value.stage, value.outcome,
            value.mood, value.lowMoodDays, value.permanentLoss, value.departed, value.localRebellion,
            value.ownerThreat, value.isolated, value.suppressed, value.lastProcessedAbsoluteDay, value.localRebellionDays);
    }
    internal static bool Name(string value) => DomainFactValueSyntax.IsText(value, DomainFactValueSyntax.MaximumNameCharacters, false)
        && value == value.Trim();
}
