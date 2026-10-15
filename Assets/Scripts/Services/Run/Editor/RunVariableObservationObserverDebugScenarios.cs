#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RunVariableObservationObserverDebugScenarios
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    public static bool RunAll()
    {
        VerifyProjectionAndLifecycle();
        return true;
    }

    public static void RunForBatch()
    {
        RunAll();
        Debug.Log("B113_RUN_VARIABLE_OBSERVATION_OBSERVER=PASS");
    }

    private static void VerifyProjectionAndLifecycle()
    {
        var oldEvents = new GameEventBus();
        var newEvents = new GameEventBus();
        var oldAlerts = new List<EventAlertRequest>();
        var newAlerts = new List<EventAlertRequest>();
        using IDisposable oldAlertsSubscription = CaptureAlerts(
            oldEvents,
            oldAlerts);
        using IDisposable newAlertsSubscription = CaptureAlerts(
            newEvents,
            newAlerts);

        var startFacilities = new List<int> { 101, 102 };
        var startGuests = new List<string> { "Slime", "Orc" };
        var startBlueprints = new List<int> { 201 };
        var start = new RunStartVariableSnapshot(
            113,
            "Orc",
            DungeonDifficulty.Hard,
            startFacilities,
            startGuests,
            startBlueprints,
            77,
            "layout:debug",
            1.25f);
        startFacilities[0] = 999;
        startGuests[0] = "Changed";
        startBlueprints[0] = 999;
        Require(
            start.startingFacilityCandidateIds[0] == 101
            && start.startingGuestSpeciesCandidates[0] == "Slime"
            && start.startingBlueprintCandidateIds[0] == 201,
            "The run-start event input was not frozen before observation.");

        var operation = new RunVariableDefinition(
            "run-variable:debug:operation",
            RunVariableCategory.Operation,
            "긴 야간",
            "야간 운영이 길어진다.",
            EventAlertImportance.Medium,
            2,
            Array.Empty<IRunVariableEffect>());
        var invasion = new RunVariableDefinition(
            "run-variable:debug:invasion",
            RunVariableCategory.Invasion,
            "강습 대비",
            "침입자가 더 강해진다.",
            EventAlertImportance.High,
            1,
            Array.Empty<IRunVariableEffect>());
        var active = new ActiveRunVariable(operation, 3, 2, 11);

        var observerObject = new GameObject(
            "Run Variable Observation Observer Debug");
        RunVariableObservationObserver observer = null;
        try
        {
            observer = observerObject.AddComponent<
                RunVariableObservationObserver>();
            Require(ReadRaiseAlerts(observer),
                "Run-variable alert projection no longer defaults enabled.");
            observer.Construct(oldEvents);
            InvokeLifecycle(observer, "OnEnable");
            observer.Construct(oldEvents);
            InvokeLifecycle(observer, "OnEnable");

            oldEvents.Publish(new RunStartVariablesSelectedEvent(start));
            oldEvents.Publish(new RunVariableActivatedEvent(active));
            oldEvents.Publish(new InvasionVariableSelectedEvent(invasion));
            Require(oldAlerts.Count == 3,
                "Run-variable observation emitted a duplicate or missing alert.");
            RequireAlert(
                oldAlerts[0],
                "런 시작 변수",
                "사장 종족: Orc\n"
                + "시작 시설 후보: 101, 102\n"
                + "시작 손님층 후보: Slime, Orc\n"
                + "시작 설계도 후보: 201\n"
                + "초기 상점 시드: 77\n"
                + "초기 구조: layout:debug\n"
                + "난이도: Hard / 위협 계수 1.25",
                EventAlertImportance.Low,
                "런 변수");
            RequireAlert(
                oldAlerts[1],
                "긴 야간",
                "야간 운영이 길어진다.",
                EventAlertImportance.Medium,
                "운영 변수");
            RequireAlert(
                oldAlerts[2],
                "강습 대비",
                "침입자가 더 강해진다.",
                EventAlertImportance.High,
                "침입 변수");

            oldEvents.Publish(new RunVariableActivatedEvent(
                active,
                announce: false));
            oldEvents.Publish(new InvasionVariableSelectedEvent(
                invasion,
                announce: false));
            Require(oldAlerts.Count == 3,
                "A producer-suppressed run-variable event emitted display.");

            SetRaiseAlerts(observer, false);
            oldEvents.Publish(new RunStartVariablesSelectedEvent(start));
            oldEvents.Publish(new RunVariableActivatedEvent(active));
            oldEvents.Publish(new InvasionVariableSelectedEvent(invasion));
            Require(oldAlerts.Count == 3,
                "The global run-variable display toggle did not suppress alerts.");
            SetRaiseAlerts(observer, true);

            observer.enabled = false;
            InvokeLifecycle(observer, "OnDisable");
            oldEvents.Publish(new RunStartVariablesSelectedEvent(start));
            Require(oldAlerts.Count == 3,
                "A disabled run-variable observer remained subscribed.");

            observer.Construct(newEvents);
            oldEvents.Publish(new RunStartVariablesSelectedEvent(start));
            newEvents.Publish(new RunStartVariablesSelectedEvent(start));
            Require(oldAlerts.Count == 3 && newAlerts.Count == 0,
                "A disabled reinjected observer retained or created a subscription.");

            observer.enabled = true;
            InvokeLifecycle(observer, "OnEnable");
            observer.Construct(newEvents);
            InvokeLifecycle(observer, "OnEnable");
            oldEvents.Publish(new RunStartVariablesSelectedEvent(start));
            newEvents.Publish(new RunStartVariablesSelectedEvent(start));
            Require(oldAlerts.Count == 3 && newAlerts.Count == 1,
                "Reinjection did not move exactly one subscription to the new bus.");
            RequireAlert(
                newAlerts[0],
                "런 시작 변수",
                start.ToSummaryText(),
                EventAlertImportance.Low,
                "런 변수");

            InvokeLifecycle(observer, "OnDestroy");
            newEvents.Publish(new RunStartVariablesSelectedEvent(start));
            Require(newAlerts.Count == 1,
                "A destroyed run-variable observer remained subscribed.");
        }
        finally
        {
            if (observer != null)
                InvokeLifecycle(observer, "OnDestroy");
            Object.DestroyImmediate(observerObject);
        }
    }

    private static IDisposable CaptureAlerts(
        IGameEventBus events,
        ICollection<EventAlertRequest> alerts) =>
        events.Subscribe<EventAlertRequestedEvent>(value =>
        {
            if (value.request != null)
                alerts.Add(value.request);
        });

    private static void RequireAlert(
        EventAlertRequest actual,
        string title,
        string detail,
        EventAlertImportance importance,
        string category)
    {
        Require(
            actual != null
            && actual.Title == title
            && actual.Detail == detail
            && actual.Importance == importance
            && actual.Category == category,
            "A run-variable alert payload changed: " + title);
    }

    private static bool ReadRaiseAlerts(
        RunVariableObservationObserver observer) =>
        (bool)RequireRaiseAlertsField().GetValue(observer);

    private static void SetRaiseAlerts(
        RunVariableObservationObserver observer,
        bool value) => RequireRaiseAlertsField().SetValue(observer, value);

    private static FieldInfo RequireRaiseAlertsField()
    {
        FieldInfo field = typeof(RunVariableObservationObserver).GetField(
            "raiseAlerts",
            PrivateInstance);
        return field != null && field.FieldType == typeof(bool)
            ? field
            : throw new MissingFieldException(
                typeof(RunVariableObservationObserver).FullName,
                "raiseAlerts");
    }

    private static void InvokeLifecycle(
        RunVariableObservationObserver observer,
        string methodName)
    {
        MethodInfo method = typeof(RunVariableObservationObserver).GetMethod(
            methodName,
            PrivateInstance);
        if (method == null)
        {
            throw new MissingMethodException(
                typeof(RunVariableObservationObserver).FullName,
                methodName);
        }
        method.Invoke(observer, null);
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException(failure);
    }
}
#endif
