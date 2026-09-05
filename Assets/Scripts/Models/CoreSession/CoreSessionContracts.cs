using System;
using System.Collections.Generic;

public enum TimeOfDay
{
    None = 0,
    Morning = 1,
    Noon = 2,
    Evening = 3,
    Night = 4
}

public enum Season
{
    Spring = 0,
    Summer = 1,
    Autumn = 2,
    Winter = 3
}

public static class GameCalendarRules
{
    public const int HoursPerDay = GameSimulationTimeRules.HoursPerDay;
    public const int DaysPerSeason = 30;
    public const int SeasonsPerYear = 4;
    public const int DaysPerYear = DaysPerSeason * SeasonsPerYear;
    public const float SecondsPerDay = GameSimulationTimeRules.SecondsPerDay;
    public const float SecondsPerGameHour =
        GameSimulationTimeRules.SecondsPerGameHour;

    public static CalendarDateTime Project(int absoluteDay, int hour)
    {
        int day = Math.Max(1, absoluteDay);
        int normalizedHour = Math.Clamp(hour, 0, HoursPerDay - 1);
        int zeroBasedDay = day - 1;
        int dayOfYear = zeroBasedDay % DaysPerYear + 1;
        int zeroBasedSeason = (dayOfYear - 1) / DaysPerSeason;
        return new CalendarDateTime(
            day,
            zeroBasedDay / DaysPerYear + 1,
            dayOfYear,
            (Season)zeroBasedSeason,
            (dayOfYear - 1) % DaysPerSeason + 1,
            normalizedHour);
    }

    public static CalendarDateTime ProjectRegional(
        int absoluteDay,
        int hour,
        int utcOffsetHours)
    {
        int offset = Math.Clamp(utcOffsetHours, -6, 6);
        long absoluteHour = ((long)Math.Max(1, absoluteDay) - 1L)
            * HoursPerDay
            + Math.Clamp(hour, 0, HoursPerDay - 1)
            + offset;
        long regionalDayIndex = Math.Max(0L, absoluteHour / HoursPerDay);
        int regionalHour = (int)(absoluteHour % HoursPerDay);
        if (regionalHour < 0)
        {
            regionalHour += HoursPerDay;
            regionalDayIndex = Math.Max(0L, regionalDayIndex - 1L);
        }

        return Project(
            checked((int)Math.Min(int.MaxValue, regionalDayIndex + 1L)),
            regionalHour);
    }
}

public readonly struct CalendarDateTime : IEquatable<CalendarDateTime>
{
    public CalendarDateTime(
        int absoluteDay,
        int year,
        int dayOfYear,
        Season season,
        int dayOfSeason,
        int hour)
    {
        AbsoluteDay = absoluteDay;
        Year = year;
        DayOfYear = dayOfYear;
        Season = season;
        DayOfSeason = dayOfSeason;
        Hour = hour;
    }

    public int AbsoluteDay { get; }
    public int Year { get; }
    public int DayOfYear { get; }
    public Season Season { get; }
    public int DayOfSeason { get; }
    public int Hour { get; }
    public long AbsoluteHour => ((long)AbsoluteDay - 1L)
        * GameCalendarRules.HoursPerDay
        + Hour;

    public bool Equals(CalendarDateTime other) =>
        AbsoluteDay == other.AbsoluteDay && Hour == other.Hour;

    public override bool Equals(object obj) =>
        obj is CalendarDateTime other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(AbsoluteDay, Hour);
}

public interface IGameCalendar
{
    int Day { get; }
    int Hour { get; }
    int Year { get; }
    int DayOfYear { get; }
    Season Season { get; }
    int DayOfSeason { get; }
    long AbsoluteHour { get; }
    float ElapsedSeconds { get; }
    TimeOfDay TimeOfDay { get; }
    bool IsRunning { get; }
    CalendarDateTime Current { get; }
    CalendarDateTime GetRegionalTime(int utcOffsetHours);
    void Start();
    void SetDateTime(int day, int hour);
}

public interface IGameSpeedController
{
    int Speed { get; }
    bool IsPaused { get; }
    void CycleSpeed();
    void SetSpeed(int speed);
    void TogglePause();
    void SetPaused(bool paused);
}

public enum ExperienceEventConcept
{
    GuestService = 0,
    StockConsumption = 1,
    Cleanliness = 2,
    Production = 3,
    Hauling = 4,
    WorkPriority = 5,
    MinorInjury = 6,
    FacilityWear = 7,
    Defense = 8,
    ResearchCapacity = 9,
    MultiStageProduction = 10,
    Industry = 11,
    Assassination = 12,
    Sabotage = 13,
    Disease = 14,
    Fire = 15
}

public enum ExperienceEventRiskTier
{
    None = 0,
    Recoverable = 1,
    Serious = 2,
    Lethal = 3
}

public readonly struct RehearsalInvasionProfile
{
    public RehearsalInvasionProfile(
        int day,
        float powerMultiplier,
        float ownerDamageMultiplier,
        float retreatHealthRatio)
    {
        Day = day;
        PowerMultiplier = Math.Clamp(powerMultiplier, 0.05f, 1f);
        OwnerDamageMultiplier = Math.Clamp(ownerDamageMultiplier, 0f, 1f);
        RetreatHealthRatio = Math.Clamp(retreatHealthRatio, 0f, 1f);
    }

    public int Day { get; }
    public float PowerMultiplier { get; }
    public float OwnerDamageMultiplier { get; }
    public float RetreatHealthRatio { get; }
}

[Serializable]
public sealed class DungeonExperiencePacingSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int currentDay = 1;
    public int scheduledRehearsalMask;
    public int completedRehearsalMask;
    public int activeRehearsalDay;
    public List<int> introducedConcepts = new();
}

public interface IExperiencePacingRuntime
{
    int CurrentDay { get; }
    bool AllowsRandomInvasion { get; }
    int MaximumConcurrentExternalProblems { get; }
    bool IsRehearsalActive { get; }
    int ActiveRehearsalDay { get; }
    void AdvanceToDay(int day);
    bool TryBeginRehearsal(int day, out RehearsalInvasionProfile profile);
    void ResolveRehearsal();
    bool CanStartExteriorIncident(ExteriorIncidentKind kind);
    void MarkExteriorIncidentStarted(ExteriorIncidentKind kind);
    DungeonExperiencePacingSaveData Capture();
    ExperiencePacingAggregateState PrepareRestoreCandidate(
        DungeonExperiencePacingSaveData data);
    void PublishRestoreCandidate(ExperiencePacingAggregateState candidate);
}

public enum DungeonRunPhase
{
    Preparation = 0,
    Growth = 1,
    Escalation = 2,
    EndlessDefense = 3,
    [Obsolete("Use EndlessDefense.")]
    FinalChallenge = EndlessDefense,
    Finished = 4,
    [Obsolete("Truth is offense progress, not a defense phase.")]
    TruthHunt = 5
}

public enum DungeonRunOutcome
{
    None,
    Victory,
    Defeat
}

public interface IDungeonRunFlowRuntime
{
    DungeonRunPhase Phase { get; }
    DungeonRunOutcome Outcome { get; }
    int CurrentDay { get; }
    int BossCycle { get; }
    bool IsBossArmed { get; }
    bool IsBossActive { get; }
    void RestoreState(
        DungeonRunPhase phase,
        DungeonRunOutcome outcome,
        int currentDay,
        bool bossArmed,
        bool bossActive,
        int bossCycle);
}

public interface IDungeonRunFlowRestorePublisher
{
    void PublishRestoreState(DungeonRunFlowAggregateState candidate);
}

public enum DungeonDebugCheat
{
    FriendlyInvincible,
    FacilityInvincible,
    FreezeNeeds,
    PreventBreakdowns,
    NoMoneyOrItemCost,
    FreeConstruction,
    IgnorePlacementRules,
    InstantConstruction,
    InstantWork,
    IgnoreUnlocks,
    PauseHumanoidAi,
    PauseWildlifeAi
}

public enum DungeonDebugOverlayKind
{
    Grid,
    GridOccupancy,
    Rooms,
    BuildingRanges,
    Lighting,
    CharacterAi,
    Hauling,
    Wildlife,
    WaterAndFilth,
    ExteriorZones,
    Defense
}

public enum DungeonDebugOverlayScope
{
    SelectedOnly,
    VisibleWorld
}

[Serializable]
public sealed class DungeonDebugCommandHistorySaveData
{
    public string gameTime = string.Empty;
    public string commandId = string.Empty;
    public string target = string.Empty;
    public string result = string.Empty;
}

[Serializable]
public sealed class DungeonDebugRunSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public bool debugModified;
    public List<DungeonDebugCommandHistorySaveData> recentCommands = new();
}

public readonly struct DungeonDebugCommandResult
{
    private DungeonDebugCommandResult(bool success, string message)
    {
        Success = success;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public string Message { get; }

    public static DungeonDebugCommandResult Succeeded(string message) =>
        new(true, message);

    public static DungeonDebugCommandResult Failed(string message) =>
        new(false, message);
}

public interface IDungeonDebugModeService
{
    bool IsDeveloperModeEnabled { get; }
    bool IsDebugModified { get; }
    DungeonDebugOverlayScope OverlayScope { get; }
    IReadOnlyList<DungeonDebugCommandHistorySaveData> RecentCommands { get; }
    event Action StateChanged;
    bool IsCheatEnabled(DungeonDebugCheat cheat);
    bool IsOverlayEnabled(DungeonDebugOverlayKind overlay);
    void SetCheat(DungeonDebugCheat cheat, bool enabled);
    void SetOverlay(DungeonDebugOverlayKind overlay, bool enabled);
    void SetOverlayScope(DungeonDebugOverlayScope scope);
    void MarkMutation(
        string commandId,
        string target,
        DungeonDebugCommandResult result);
    DungeonDebugRunSaveData Capture();
    DungeonDebugRestoreCandidate PrepareRestoreCandidate(
        DungeonDebugRunSaveData data);
    void PublishRestoreCandidate(DungeonDebugRestoreCandidate candidate);
    void ResetTransientState();
}
