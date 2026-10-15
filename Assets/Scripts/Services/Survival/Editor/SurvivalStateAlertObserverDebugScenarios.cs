#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;

public static class SurvivalStateAlertObserverDebugScenarios
{
    private const string WarningDetail =
        "결핍 원인을 해결하지 않으면 건강 이상과 돌발 행동으로 이어질 수 있습니다.";
    private const string DangerDetail =
        "건강 탭에서 가장 심한 결핍을 확인하고 음식, 물, 화장실, 휴식 시설을 확보하세요.";
    private const string WeatherTitle = "날씨가 위험해집니다";

    public static void RunOrThrow()
    {
        VerifyThresholdsSnapshotsAndLifecycle();
        VerifyAlertFailureIsolation();
    }

    private static void VerifyThresholdsSnapshotsAndLifecycle()
    {
        CharacterId first = new CharacterId("character:survival-alert:first");
        CharacterId second = new CharacterId("character:survival-alert:second");
        DeprivationBurdenSample[] mutable =
        {
            new DeprivationBurdenSample(first, 40f)
        };
        var frozen = new DeprivationBurdenSnapshotEvent(0, mutable);
        mutable[0] = new DeprivationBurdenSample(second, 70f);
        Require(
            frozen.Samples.Count == 1
            && frozen.Samples[0].CharacterId.Equals(first)
            && frozen.Samples[0].HighestBurden == 40f,
            "The deprivation snapshot retained its mutable input array.");

        var events = new GameEventBus();
        var alerts = new List<EventAlertRequest>();
        using IDisposable alertSubscription =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                if (value.request != null)
                    alerts.Add(value.request);
            });
        var observer = new SurvivalStateAlertObserver(events);
        try
        {
            observer.Initialize();
            observer.Initialize();

            events.Publish(Snapshot(0, (first, 39f)));
            Require(alerts.Count == 0,
                "A burden below 40 emitted a survival alert.");

            events.Publish(frozen);
            Require(alerts.Count == 1,
                "A burden of 40 did not emit one warning.");
            RequireAlert(
                alerts[0],
                "1명의 건강에 결핍 부담이 쌓이고 있습니다",
                WarningDetail,
                EventAlertImportance.Medium);

            events.Publish(frozen);
            Require(alerts.Count == 1,
                "A stable deprivation snapshot emitted a duplicate warning.");

            events.Publish(Snapshot(0, (first, 70f)));
            Require(alerts.Count == 2,
                "A burden of 70 did not emit one danger alert.");
            RequireAlert(
                alerts[1],
                "1명이 결핍으로 붕괴 위험에 빠졌습니다",
                DangerDetail,
                EventAlertImportance.High);

            events.Publish(Snapshot(0, (first, 20f)));
            Require(alerts.Count == 2,
                "Lowering a deprivation baseline emitted an alert.");
            events.Publish(Snapshot(0, (first, 40f)));
            Require(alerts.Count == 3,
                "Rising after a lower baseline did not re-emit a warning.");
            RequireAlert(
                alerts[2],
                "1명의 건강에 결핍 부담이 쌓이고 있습니다",
                WarningDetail,
                EventAlertImportance.Medium);

            events.Publish(Snapshot(0, (second, 40f)));
            Require(alerts.Count == 4,
                "A newly observed character did not emit its warning.");
            events.Publish(Snapshot(0));
            events.Publish(Snapshot(0, (second, 40f)));
            Require(alerts.Count == 5,
                "An absent character was not pruned from alert baselines.");

            events.Publish(Snapshot(1, (second, 40f)));
            Require(alerts.Count == 6,
                "A restore revision change did not clear deprivation baselines.");
            events.Publish(Snapshot(1, (first, 40f), (second, 40f)));
            Require(alerts.Count == 7,
                "The restored baseline did not retain the stable character while adding one warning.");
            events.Publish(new DeprivationCharacterResetEvent(first));
            events.Publish(Snapshot(1, (first, 40f), (second, 40f)));
            Require(alerts.Count == 8,
                "A character reset did not clear exactly one alert baseline.");

            int beforeWeather = alerts.Count;
            events.Publish(new SurvivalWeatherSampledEvent(
                "front:cold",
                SurvivalWeatherType.ColdSnap));
            Require(alerts.Count == beforeWeather + 1,
                "A cold snap did not emit one weather alert.");
            RequireWeatherAlert(alerts[^1], SurvivalWeatherType.ColdSnap);
            events.Publish(new SurvivalWeatherSampledEvent(
                "front:cold",
                SurvivalWeatherType.HeatWave));
            Require(alerts.Count == beforeWeather + 1,
                "The same weather front emitted a duplicate alert.");
            events.Publish(new SurvivalWeatherSampledEvent(
                "front:heat",
                SurvivalWeatherType.HeatWave));
            Require(alerts.Count == beforeWeather + 2,
                "A changed heat-wave front did not emit an alert.");
            RequireWeatherAlert(alerts[^1], SurvivalWeatherType.HeatWave);
            events.Publish(new SurvivalWeatherSampledEvent(
                "front:clear",
                SurvivalWeatherType.Clear));
            events.Publish(new SurvivalWeatherSampledEvent(
                "front:clear",
                SurvivalWeatherType.Storm));
            Require(alerts.Count == beforeWeather + 2,
                "A clear front emitted an alert or failed to update weather deduplication.");
            events.Publish(new SurvivalWeatherSampledEvent(
                "front:storm",
                SurvivalWeatherType.Storm));
            Require(alerts.Count == beforeWeather + 3,
                "A changed storm front did not emit an alert.");
            RequireWeatherAlert(alerts[^1], SurvivalWeatherType.Storm);

            int beforeDispose = alerts.Count;
            observer.Dispose();
            events.Publish(Snapshot(1, (second, 40f)));
            events.Publish(new SurvivalWeatherSampledEvent(
                "front:storm-after-dispose",
                SurvivalWeatherType.Storm));
            Require(alerts.Count == beforeDispose,
                "A disposed survival alert observer remained subscribed.");

            observer.Initialize();
            observer.Initialize();
            events.Publish(Snapshot(1, (second, 40f)));
            events.Publish(new SurvivalWeatherSampledEvent(
                "front:storm",
                SurvivalWeatherType.Storm));
            Require(alerts.Count == beforeDispose + 2,
                "Reinitialization did not subscribe once with cleared caches.");
            RequireAlert(
                alerts[beforeDispose],
                "1명의 건강에 결핍 부담이 쌓이고 있습니다",
                WarningDetail,
                EventAlertImportance.Medium);
            RequireWeatherAlert(
                alerts[beforeDispose + 1],
                SurvivalWeatherType.Storm);
        }
        finally
        {
            observer.Dispose();
        }
    }

    private static void VerifyAlertFailureIsolation()
    {
        var events = new GameEventBus();
        var alerts = new List<EventAlertRequest>();
        using IDisposable capture =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                if (value.request != null)
                    alerts.Add(value.request);
            });
        using IDisposable failure =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                if (value.request?.Importance == EventAlertImportance.High)
                    throw new InvalidOperationException(
                        "qa-survival-danger-alert-consumer-failed");
            });
        var observer = new SurvivalStateAlertObserver(events);
        observer.Initialize();
        try
        {
            bool aggregated = false;
            try
            {
                events.Publish(Snapshot(
                    0,
                    (new CharacterId("character:survival-alert:danger"), 70f),
                    (new CharacterId("character:survival-alert:warning"), 40f)));
            }
            catch (AggregateException exception)
            {
                aggregated = exception.InnerExceptions.Count == 1
                    && exception.InnerExceptions[0].Message
                        == "qa-survival-danger-alert-consumer-failed";
            }

            Require(
                aggregated
                && alerts.Count == 2,
                "A failed danger-alert consumer suppressed the warning alert or lost the aggregate failure.");
            RequireAlert(
                alerts[0],
                "1명이 결핍으로 붕괴 위험에 빠졌습니다",
                DangerDetail,
                EventAlertImportance.High);
            RequireAlert(
                alerts[1],
                "1명의 건강에 결핍 부담이 쌓이고 있습니다",
                WarningDetail,
                EventAlertImportance.Medium);

            events.Publish(Snapshot(
                0,
                (new CharacterId("character:survival-alert:danger"), 70f),
                (new CharacterId("character:survival-alert:warning"), 40f)));
            Require(alerts.Count == 2,
                "Alert delivery failure prevented the observer from updating deduplication baselines.");
        }
        finally
        {
            observer.Dispose();
        }
    }

    private static DeprivationBurdenSnapshotEvent Snapshot(
        int restoreRevision,
        params (CharacterId CharacterId, float Burden)[] values)
    {
        var samples = new DeprivationBurdenSample[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            samples[index] = new DeprivationBurdenSample(
                values[index].CharacterId,
                values[index].Burden);
        }
        return new DeprivationBurdenSnapshotEvent(
            restoreRevision,
            samples);
    }

    private static void RequireWeatherAlert(
        EventAlertRequest request,
        SurvivalWeatherType weather) => RequireAlert(
        request,
        WeatherTitle,
        $"{SurvivalFacilityWorkRules.FormatWeather(weather)} 예보입니다. 연료, 조명, 외부 작업 상태를 확인하세요.",
        EventAlertImportance.Medium);

    private static void RequireAlert(
        EventAlertRequest request,
        string title,
        string detail,
        EventAlertImportance importance)
    {
        Require(
            request != null
            && request.Title == title
            && request.Detail == detail
            && request.Importance == importance
            && request.Category == "생존",
            "A survival alert payload changed from the original presentation contract.");
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException(failure);
    }
}
#endif
