using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects complete survival observations into the legacy aggregate alert feed.
/// It owns presentation deduplication only; deprivation and weather remain domain state.
/// </summary>
public sealed class SurvivalStateAlertObserver : IInitializable, IDisposable
{
    private const float WarningThreshold = 40f;
    private const float DangerThreshold = 70f;

    private readonly IGameEventBus events;
    private readonly Dictionary<CharacterId, int> alertLevels = new();
    private IDisposable deprivationSubscription;
    private IDisposable resetSubscription;
    private IDisposable weatherSubscription;
    private int observedRestoreRevision = -1;
    private bool hasObservedWeatherFront;
    private string observedWeatherFrontId = string.Empty;

    public SurvivalStateAlertObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize()
    {
        if (deprivationSubscription != null)
        {
            return;
        }

        deprivationSubscription =
            events.Subscribe<DeprivationBurdenSnapshotEvent>(Observe);
        resetSubscription =
            events.Subscribe<DeprivationCharacterResetEvent>(Observe);
        weatherSubscription =
            events.Subscribe<SurvivalWeatherSampledEvent>(Observe);
    }

    public void Dispose()
    {
        deprivationSubscription?.Dispose();
        deprivationSubscription = null;
        resetSubscription?.Dispose();
        resetSubscription = null;
        weatherSubscription?.Dispose();
        weatherSubscription = null;
        ClearCaches();
    }

    private void Observe(DeprivationBurdenSnapshotEvent value)
    {
        if (observedRestoreRevision != value.RestoreRevision)
        {
            alertLevels.Clear();
            observedRestoreRevision = value.RestoreRevision;
        }

        HashSet<CharacterId> currentIds = new();
        int warningCount = 0;
        int dangerCount = 0;
        for (int index = 0; index < value.Samples.Count; index++)
        {
            DeprivationBurdenSample sample = value.Samples[index];
            currentIds.Add(sample.CharacterId);
            int level = sample.HighestBurden >= DangerThreshold
                ? 2
                : sample.HighestBurden >= WarningThreshold ? 1 : 0;
            alertLevels.TryGetValue(sample.CharacterId, out int previous);
            alertLevels[sample.CharacterId] = level;
            if (level <= previous)
            {
                continue;
            }

            if (level >= 2)
            {
                dangerCount++;
            }
            else
            {
                warningCount++;
            }
        }

        List<CharacterId> absentIds = null;
        foreach (CharacterId characterId in alertLevels.Keys)
        {
            if (!currentIds.Contains(characterId))
            {
                absentIds ??= new List<CharacterId>();
                absentIds.Add(characterId);
            }
        }

        if (absentIds != null)
        {
            for (int index = 0; index < absentIds.Count; index++)
            {
                alertLevels.Remove(absentIds[index]);
            }
        }

        List<Exception> failures = null;
        if (dangerCount > 0)
        {
            TryRaise(
                () => events.RaiseAlert(
                    $"{dangerCount}명이 결핍으로 붕괴 위험에 빠졌습니다",
                    "건강 탭에서 가장 심한 결핍을 확인하고 음식, 물, 화장실, 휴식 시설을 확보하세요.",
                    EventAlertImportance.High,
                    "생존"),
                ref failures);
        }

        if (warningCount > 0)
        {
            TryRaise(
                () => events.RaiseAlert(
                    $"{warningCount}명의 건강에 결핍 부담이 쌓이고 있습니다",
                    "결핍 원인을 해결하지 않으면 건강 이상과 돌발 행동으로 이어질 수 있습니다.",
                    EventAlertImportance.Medium,
                    "생존"),
                ref failures);
        }

        if (failures != null)
        {
            throw new AggregateException(
                "One or more survival deprivation alerts failed.",
                failures);
        }
    }

    private void Observe(DeprivationCharacterResetEvent value) =>
        alertLevels.Remove(value.CharacterId);

    private void Observe(SurvivalWeatherSampledEvent value)
    {
        if (hasObservedWeatherFront
            && string.Equals(
                observedWeatherFrontId,
                value.FrontId,
                StringComparison.Ordinal))
        {
            return;
        }

        hasObservedWeatherFront = true;
        observedWeatherFrontId = value.FrontId;
        if (value.Weather is not SurvivalWeatherType.ColdSnap
            and not SurvivalWeatherType.HeatWave
            and not SurvivalWeatherType.Storm)
        {
            return;
        }

        events.RaiseAlert(
            "날씨가 위험해집니다",
            $"{SurvivalFacilityWorkRules.FormatWeather(value.Weather)} 예보입니다. 연료, 조명, 외부 작업 상태를 확인하세요.",
            EventAlertImportance.Medium,
            "생존");
    }

    private static void TryRaise(Action raise, ref List<Exception> failures)
    {
        try
        {
            raise();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            failures ??= new List<Exception>();
            failures.Add(exception);
        }
    }

    private void ClearCaches()
    {
        alertLevels.Clear();
        observedRestoreRevision = -1;
        hasObservedWeatherFront = false;
        observedWeatherFrontId = string.Empty;
    }
}
