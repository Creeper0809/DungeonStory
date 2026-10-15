using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class GameCalendarRuntime : IGameCalendar, ITickable
{
    private readonly IGameSessionStateProvider sessionStateProvider;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private bool isRunning;

    public GameCalendarRuntime(
        IGameSessionStateProvider sessionStateProvider,
        IGameEventBus gameEventBus,
        IGameClock gameClock)
    {
        this.sessionStateProvider = sessionStateProvider
            ?? throw new ArgumentNullException(nameof(sessionStateProvider));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public int Day => RequireState().day.Value;
    public int Hour => RequireState().hour.Value;
    public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
    public int Year => Current.Year;
    public int DayOfYear => Current.DayOfYear;
    public Season Season => Current.Season;
    public int DayOfSeason => Current.DayOfSeason;
    public long AbsoluteHour => Current.AbsoluteHour;
    public float ElapsedSeconds => RequireState().curTime.Value;
    public TimeOfDay TimeOfDay => RequireState().timeOfDay.Value;
    public bool IsRunning => isRunning;

    public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
        GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);

    public void Start()
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        gameEventBus.Publish(new OperatingDayStartedEvent(Day));
    }

    public void Tick()
    {
        if (!isRunning)
        {
            return;
        }

        GameSessionState state = RequireState();
        float elapsed = state.curTime.Value + Mathf.Max(0f, gameClock.DeltaTime);
        while (elapsed >= GameCalendarRules.SecondsPerDay)
        {
            int endedDay = state.day.Value;
            elapsed -= GameCalendarRules.SecondsPerDay;
            // Midnight has occurred before either notification. Ended carries
            // the completed accounting day; listeners that advance life or
            // ambition to endedDay + 1 must see that same authoritative date.
            // Publish a coherent midnight, not next-day/old-hour mixed state.
            state.curTime.Value = 0f;
            state.hour.Value = 0;
            state.timeOfDay.Value = ResolveTimeOfDay(0f);
            state.day.Value = checked(endedDay + 1);
            gameEventBus.Publish(new OperatingDayEndedEvent(endedDay));
            gameEventBus.Publish(new OperatingDayStartedEvent(state.day.Value));
        }

        state.curTime.Value = elapsed;
        state.hour.Value = Mathf.FloorToInt(
            elapsed / GameCalendarRules.SecondsPerDay * GameCalendarRules.HoursPerDay)
            % GameCalendarRules.HoursPerDay;
        state.timeOfDay.Value = ResolveTimeOfDay(elapsed);
    }

    public void SetDateTime(int day, int hour)
    {
        GameSessionState state = RequireState();
        int normalizedHour = Mathf.Clamp(hour, 0, 23);
        float elapsed = normalizedHour
            / (float)GameCalendarRules.HoursPerDay
            * GameCalendarRules.SecondsPerDay;
        state.day.Value = Mathf.Max(1, day);
        state.curTime.Value = elapsed;
        state.hour.Value = normalizedHour;
        state.timeOfDay.Value = ResolveTimeOfDay(elapsed);
    }

    private GameSessionState RequireState()
    {
        return sessionStateProvider.TryGetSessionState(out GameSessionState state)
            && state != null
            ? state
            : throw new InvalidOperationException("The game session state is not initialized.");
    }

    private static TimeOfDay ResolveTimeOfDay(float elapsed)
    {
        if (elapsed < 40f) return TimeOfDay.Night;
        if (elapsed < 50f) return TimeOfDay.Morning;
        if (elapsed < 145f) return TimeOfDay.Noon;
        if (elapsed < 155f) return TimeOfDay.Evening;
        return TimeOfDay.Night;
    }
}

public sealed class GameSpeedController : IGameSpeedController
{
    private readonly IGameSessionStateProvider sessionStateProvider;
    private readonly IGameSessionPauseAuthority pauseAuthority;
    private readonly IGameTimeScaleController timeScaleController;

    public GameSpeedController(
        IGameSessionStateProvider sessionStateProvider,
        IGameSessionPauseAuthority pauseAuthority,
        IGameTimeScaleController timeScaleController)
    {
        this.sessionStateProvider = sessionStateProvider
            ?? throw new ArgumentNullException(nameof(sessionStateProvider));
        this.pauseAuthority = pauseAuthority
            ?? throw new ArgumentNullException(nameof(pauseAuthority));
        this.timeScaleController = timeScaleController
            ?? throw new ArgumentNullException(nameof(timeScaleController));
    }

    public int Speed => RequireState().gameSpeed.Value;
    public bool IsPaused => RequireState().IsPaused;

    public void CycleSpeed()
    {
        SetSpeed(Speed % 5 + 1);
    }

    public void SetSpeed(int speed)
    {
        GameSessionState state = RequireState();
        state.gameSpeed.Value = Mathf.Clamp(speed, 1, 5);
        if (!state.IsPaused)
        {
            timeScaleController.Scale = state.gameSpeed.Value;
        }
    }

    public void TogglePause()
    {
        SetPaused(!IsPaused);
    }

    public void SetPaused(bool paused)
    {
        GameSessionState state = RequireState();
        pauseAuthority.SetPaused(paused);
        timeScaleController.Scale = paused ? 0f : state.gameSpeed.Value;
    }

    private GameSessionState RequireState()
    {
        return sessionStateProvider.TryGetSessionState(out GameSessionState state)
            && state != null
            ? state
            : throw new InvalidOperationException("The game session state is not initialized.");
    }
}
