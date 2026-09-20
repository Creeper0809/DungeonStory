using System;
using System.Collections.Generic;
using System.Linq;

public enum InfrastructureCommandOutcomeKind
{
    PowerConnectionChanged = 1,
    PowerPriorityChanged = 2,
    PowerBreakerReset = 3,
    FluidBlockageCleared = 4,
    WaterTransferModeChanged = 5,
    FluidLeakRepaired = 6,
    ConveyorNodeEnabledChanged = 7,
    ConveyorDestinationChanged = 8,
    ConveyorOverflowPolicyChanged = 9,
    ConveyorFilterChanged = 10,
    ConveyorOverflowApproved = 11,
    AutomationModeChanged = 12,
    AutomationMaintained = 13
}

public readonly struct InfrastructureCommandOutcomeSource
{
    private const int MaximumSnapshotLength = 4096;

    public InfrastructureCommandOutcomeSource(
        InfrastructureCommandOutcomeKind kind,
        string targetId,
        string facilityId,
        int facilityX,
        int facilityY,
        string beforeValue,
        string afterValue)
    {
        if (!Enum.IsDefined(typeof(InfrastructureCommandOutcomeKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        TargetId = RequireStableId(targetId, nameof(targetId));
        FacilityId = RequireStableId(facilityId, nameof(facilityId));
        FacilityX = facilityX;
        FacilityY = facilityY;
        BeforeValue = RequireText(beforeValue, nameof(beforeValue));
        AfterValue = RequireText(afterValue, nameof(afterValue));
    }

    public InfrastructureCommandOutcomeKind Kind { get; }
    public string TargetId { get; }
    public string FacilityId { get; }
    public int FacilityX { get; }
    public int FacilityY { get; }
    public string BeforeValue { get; }
    public string AfterValue { get; }

    private static string RequireStableId(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0
            || normalized.Length > 256
            || !string.Equals(value, normalized, StringComparison.Ordinal)
            || normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A canonical infrastructure command identity is required.",
                parameterName);
        }
        return normalized;
    }

    private static string RequireText(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException(
                "A non-empty infrastructure command snapshot is required.",
                parameterName);
        if (normalized.Length > MaximumSnapshotLength)
            throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}

public readonly struct InfrastructureCommandOutcomeFrozenContext
{
    private const int MaximumSnapshotLength = 4096;

    public InfrastructureCommandOutcomeFrozenContext(
        string facilityDisplayText,
        string facilityDisplayRevision,
        int pronunciationMode,
        string pronunciationValue,
        int pronunciationFinalConsonant,
        string pronunciationRevision,
        string locale,
        int absoluteDay)
    {
        FacilityDisplayText = RequireText(
            facilityDisplayText,
            nameof(facilityDisplayText));
        FacilityDisplayRevision = RequireText(
            facilityDisplayRevision,
            nameof(facilityDisplayRevision));
        PronunciationMode = pronunciationMode;
        PronunciationValue = OptionalText(
            pronunciationValue,
            nameof(pronunciationValue));
        PronunciationFinalConsonant = pronunciationFinalConsonant;
        PronunciationRevision = OptionalText(
            pronunciationRevision,
            nameof(pronunciationRevision));
        Locale = RequireText(locale, nameof(locale));
        if (absoluteDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        AbsoluteDay = absoluteDay;
    }

    public string FacilityDisplayText { get; }
    public string FacilityDisplayRevision { get; }
    public int PronunciationMode { get; }
    public string PronunciationValue { get; }
    public int PronunciationFinalConsonant { get; }
    public string PronunciationRevision { get; }
    public string Locale { get; }
    public int AbsoluteDay { get; }

    private static string OptionalText(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > MaximumSnapshotLength)
            throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static string RequireText(string value, string parameterName)
    {
        string normalized = OptionalText(value, parameterName);
        if (normalized.Length == 0)
            throw new ArgumentException(
                "A frozen infrastructure command context value is required.",
                parameterName);
        return normalized;
    }
}

[Serializable]
public sealed class InfrastructureCommandOutcomeOutboxSaveData
{
    public long ownerRevision;
    public InfrastructureCommandOutcomeKind kind;
    public string targetId = string.Empty;
    public string facilityId = string.Empty;
    public int facilityX;
    public int facilityY;
    public string beforeValue = string.Empty;
    public string afterValue = string.Empty;
    public string facilityDisplayText = string.Empty;
    public string facilityDisplayRevision = string.Empty;
    public int pronunciationMode;
    public string pronunciationValue = string.Empty;
    public int pronunciationFinalConsonant;
    public string pronunciationRevision = string.Empty;
    public string locale = string.Empty;
    public int absoluteDay;

    public static InfrastructureCommandOutcomeOutboxSaveData Create(
        in InfrastructureCommandOutcomeSource source,
        long ownerRevision,
        in InfrastructureCommandOutcomeFrozenContext context)
    {
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        return new InfrastructureCommandOutcomeOutboxSaveData
        {
            ownerRevision = ownerRevision,
            kind = source.Kind,
            targetId = source.TargetId,
            facilityId = source.FacilityId,
            facilityX = source.FacilityX,
            facilityY = source.FacilityY,
            beforeValue = source.BeforeValue,
            afterValue = source.AfterValue,
            facilityDisplayText = context.FacilityDisplayText,
            facilityDisplayRevision = context.FacilityDisplayRevision,
            pronunciationMode = context.PronunciationMode,
            pronunciationValue = context.PronunciationValue,
            pronunciationFinalConsonant = context.PronunciationFinalConsonant,
            pronunciationRevision = context.PronunciationRevision,
            locale = context.Locale,
            absoluteDay = context.AbsoluteDay
        };
    }

    public InfrastructureCommandOutcomeSource ToSource() => new(
        kind,
        targetId,
        facilityId,
        facilityX,
        facilityY,
        beforeValue,
        afterValue);

    public InfrastructureCommandOutcomeFrozenContext ToFrozenContext() => new(
        facilityDisplayText,
        facilityDisplayRevision,
        pronunciationMode,
        pronunciationValue,
        pronunciationFinalConsonant,
        pronunciationRevision,
        locale,
        absoluteDay);

    public InfrastructureCommandOutcomeOutboxSaveData Clone() => new()
    {
        ownerRevision = ownerRevision,
        kind = kind,
        targetId = targetId,
        facilityId = facilityId,
        facilityX = facilityX,
        facilityY = facilityY,
        beforeValue = beforeValue,
        afterValue = afterValue,
        facilityDisplayText = facilityDisplayText,
        facilityDisplayRevision = facilityDisplayRevision,
        pronunciationMode = pronunciationMode,
        pronunciationValue = pronunciationValue,
        pronunciationFinalConsonant = pronunciationFinalConsonant,
        pronunciationRevision = pronunciationRevision,
        locale = locale,
        absoluteDay = absoluteDay
    };
}

[Serializable]
public sealed class DungeonInfrastructureCommandOutcomeSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public long nextOwnerRevision = 1L;
    public List<InfrastructureCommandOutcomeOutboxSaveData> pendingOutcomes =
        new();
}

public interface IPreparedInfrastructureCommandOutcome
{
    long OwnerRevision { get; }
    InfrastructureCommandOutcomeSource Source { get; }
    InfrastructureCommandOutcomeFrozenContext FrozenContext { get; }
}

public readonly struct InfrastructureCommandOutcomeCommitResult
{
    public InfrastructureCommandOutcomeCommitResult(
        bool durablyCommitted,
        string detailCode)
    {
        DurablyCommitted = durablyCommitted;
        DetailCode = detailCode ?? string.Empty;
    }

    public bool DurablyCommitted { get; }
    public string DetailCode { get; }
}

public interface IInfrastructureCommandOutcomeCommitter
{
    bool TryPrepare(
        in InfrastructureCommandOutcomeSource source,
        long ownerRevision,
        out IPreparedInfrastructureCommandOutcome prepared,
        out string failureReason);
    bool TryPreparePending(
        InfrastructureCommandOutcomeOutboxSaveData pending,
        out IPreparedInfrastructureCommandOutcome prepared,
        out string failureReason);
    InfrastructureCommandOutcomeCommitResult Commit(
        IPreparedInfrastructureCommandOutcome prepared);
    void Cancel(IPreparedInfrastructureCommandOutcome prepared);
}

public interface IInfrastructureCommandOutcomeTransaction
{
    bool TryPrepare(
        in InfrastructureCommandOutcomeSource source,
        out IPreparedInfrastructureCommandOutcome prepared,
        out string failureReason);
    InfrastructureCommandOutcomeCommitResult CommitReversible(
        IPreparedInfrastructureCommandOutcome prepared);
    void QueueCommitted(IPreparedInfrastructureCommandOutcome prepared);
    void DeliverQueued(
        long ownerRevision,
        IPreparedInfrastructureCommandOutcome prepared = null);
    void Cancel(IPreparedInfrastructureCommandOutcome prepared);
}

public interface IInfrastructureCommandOutcomePersistence
{
    DungeonInfrastructureCommandOutcomeSaveData Capture();
    InfrastructureCommandOutcomeRestoreCandidate PrepareRestore(
        DungeonInfrastructureCommandOutcomeSaveData snapshot);
    void Restore(InfrastructureCommandOutcomeRestoreCandidate candidate);
}

public sealed class InfrastructureCommandOutcomeRestoreCandidate
{
    internal InfrastructureCommandOutcomeRestoreCandidate(
        long nextOwnerRevision,
        IReadOnlyList<InfrastructureCommandOutcomeOutboxSaveData> pending)
    {
        NextOwnerRevision = nextOwnerRevision;
        Pending = pending ?? throw new ArgumentNullException(nameof(pending));
    }

    internal long NextOwnerRevision { get; }
    internal IReadOnlyList<InfrastructureCommandOutcomeOutboxSaveData> Pending
    {
        get;
    }
}
