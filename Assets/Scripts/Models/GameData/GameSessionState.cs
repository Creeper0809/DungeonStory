using System;

public interface IGameSessionStateMutation
{
    void Reset(GameData settings);
    void Reset(int startingMoney, int startingDay, int startingGameSpeed);
    void SetPaused(bool paused);
    void Restore(GameSessionSnapshot snapshot);
}

public readonly struct GameSessionSnapshot
{
    public GameSessionSnapshot(
        int money,
        int day,
        int gameSpeed,
        float elapsedSeconds,
        int hour,
        TimeOfDay timeOfDay,
        bool isPaused)
    {
        Money = Math.Max(0, money);
        Day = Math.Max(1, day);
        GameSpeed = Math.Clamp(gameSpeed, 1, 5);
        ElapsedSeconds = Math.Max(0f, elapsedSeconds);
        Hour = Math.Clamp(hour, 0, 23);
        TimeOfDay = timeOfDay;
        IsPaused = isPaused;
    }

    public int Money { get; }
    public int Day { get; }
    public int GameSpeed { get; }
    public float ElapsedSeconds { get; }
    public int Hour { get; }
    public TimeOfDay TimeOfDay { get; }
    public bool IsPaused { get; }
}

/// <summary>
/// Mutable state for one dungeon run. This is never an asset and is owned by
/// the run-scoped composition root.
/// </summary>
public sealed class GameSessionState
{
    public GameSessionState()
        : this(startingMoney: 0, startingDay: 1, startingGameSpeed: 1)
    {
    }

    public GameSessionState(
        int startingMoney,
        int startingDay = 1,
        int startingGameSpeed = 1)
    {
        gameSpeed = new Data<int>();
        holdingMoney = new Data<int>();
        day = new Data<int>();
        curTime = new Data<float>();
        hour = new Data<int>();
        timeOfDay = new Data<TimeOfDay>();
        ApplyReset(startingMoney, startingDay, startingGameSpeed);
    }

    public static GameSessionState Create(
        GameData settings,
        out IGameSessionStateMutation mutation)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        GameSessionState state = new GameSessionState(
            settings.StartingMoney,
            settings.StartingDay,
            settings.StartingGameSpeed);
        mutation = new Mutation(state);
        return state;
    }

    // Reactive values remain exposed for presentation compatibility during the
    // V18 cutover. Runtime writers are restricted by architecture validation to
    // the money, calendar and speed authority services.
    public Data<int> gameSpeed { get; }
    public Data<int> holdingMoney { get; }
    public Data<int> day { get; }
    public Data<float> curTime { get; }
    public Data<int> hour { get; }
    public Data<TimeOfDay> timeOfDay { get; }

    public bool IsPaused { get; private set; }

    private void ApplyReset(GameData settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        ApplyReset(
            settings.StartingMoney,
            settings.StartingDay,
            settings.StartingGameSpeed);
    }

    private void ApplyReset(
        int startingMoney,
        int startingDay,
        int startingGameSpeed)
    {
        holdingMoney.Initialize(Math.Max(0, startingMoney));
        day.Initialize(Math.Max(1, startingDay));
        gameSpeed.Initialize(Math.Clamp(startingGameSpeed, 1, 5));
        curTime.Initialize(0f);
        hour.Initialize(0);
        timeOfDay.Initialize(TimeOfDay.Night);
        IsPaused = false;
    }

    public GameSessionSnapshot Capture()
    {
        return new GameSessionSnapshot(
            holdingMoney.Value,
            day.Value,
            gameSpeed.Value,
            curTime.Value,
            hour.Value,
            timeOfDay.Value,
            IsPaused);
    }

    private void ApplyRestore(GameSessionSnapshot snapshot)
    {
        holdingMoney.Initialize(snapshot.Money);
        day.Initialize(snapshot.Day);
        gameSpeed.Initialize(snapshot.GameSpeed);
        curTime.Initialize(snapshot.ElapsedSeconds);
        hour.Initialize(snapshot.Hour);
        timeOfDay.Initialize(snapshot.TimeOfDay);
        IsPaused = snapshot.IsPaused;
    }

    private sealed class Mutation : IGameSessionStateMutation
    {
        private readonly GameSessionState state;

        internal Mutation(GameSessionState state)
        {
            this.state = state
                ?? throw new ArgumentNullException(nameof(state));
        }

        public void Reset(GameData settings) => state.ApplyReset(settings);

        public void Reset(
            int startingMoney,
            int startingDay,
            int startingGameSpeed) =>
            state.ApplyReset(startingMoney, startingDay, startingGameSpeed);

        public void SetPaused(bool paused) => state.IsPaused = paused;

        public void Restore(GameSessionSnapshot snapshot) =>
            state.ApplyRestore(snapshot);
    }
}
