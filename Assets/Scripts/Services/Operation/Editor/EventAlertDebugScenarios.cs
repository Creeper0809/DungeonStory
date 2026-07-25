using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EventAlertDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Operation/Run P1 Event Alert Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 event alert scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("알림 생성과 상세 패널", VerifyAlertCreatesButtonAndDetail, errors);
        RunScenario("반복 이벤트 병합", VerifyRepeatedAlertMerge, errors);
        RunScenario("선택 이벤트", VerifyChoiceEvent, errors);
        RunScenario("운영일 정산 이벤트 로그", VerifySettlementKeepsEventLog, errors);

        RunScenario("logged event keeps an immutable count snapshot", VerifyLoggedEventSnapshotDoesNotDrift, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 event alert scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)
    {
        if (scenario()) return;

        errors.Add(name);
    }

    private static bool VerifyAlertCreatesButtonAndDetail()
    {
        EventAlertRuntime runtime = CreateRuntime(out GameObject root);
        runtime.OnTriggerEvent(new EventAlertRequestedEvent(new EventAlertRequest(
            "침입 결과",
            "가시 함정이 가장 많은 피해를 줌",
            EventAlertImportance.High,
            "침입")));

        EventAlertRecord record = runtime.EventLog.Count > 0 ? runtime.EventLog[0] : null;
        runtime.Open(record);
        bool valid = record != null
            && record.Importance == EventAlertImportance.High
            && runtime.IsDetailVisible
            && runtime.SelectedRecord == record
            && record.ToDetailText().Contains("가시 함정");

        Object.DestroyImmediate(root);
        CleanupRuntimeUi();
        return valid;
    }

    private static bool VerifyRepeatedAlertMerge()
    {
        EventAlertRuntime runtime = CreateRuntime(out GameObject root);
        EventAlertRequest request = new EventAlertRequest("직원 불만", "피로 누적", EventAlertImportance.Medium, "직원");

        runtime.OnTriggerEvent(new EventAlertRequestedEvent(request));
        runtime.OnTriggerEvent(new EventAlertRequestedEvent(request));

        bool valid = runtime.EventLog.Count == 1
            && runtime.EventLog[0].Count == 2
            && runtime.EventLog[0].ButtonText == "직원 불만 x2";

        Object.DestroyImmediate(root);
        CleanupRuntimeUi();
        return valid;
    }

    private static bool VerifyChoiceEvent()
    {
        EventAlertRuntime runtime = CreateRuntime(out GameObject root);
        int selected = 0;
        EventAlertRequest request = new EventAlertRequest(
            "방문 상인",
            "무엇을 구매할까?",
            EventAlertImportance.Low,
            "선택",
            new[]
            {
                new EventAlertChoice("구매", "돈을 지불하고 재고를 얻음", () => selected = 1),
                new EventAlertChoice("무시", "아무 일도 없음", () => selected = 2),
                new EventAlertChoice("협박", "위험한 선택", () => selected = 3),
                new EventAlertChoice("초과", "표시되지 않아야 함", () => selected = 4)
            });

        runtime.OnTriggerEvent(new EventAlertRequestedEvent(request));
        runtime.Open(runtime.EventLog[0]);
        bool executed = runtime.ExecuteChoice(1);
        bool valid = runtime.EventLog[0].Choices.Count == 3
            && executed
            && selected == 2
            && !runtime.IsDetailVisible;

        Object.DestroyImmediate(root);
        CleanupRuntimeUi();
        return valid;
    }

    private static bool VerifySettlementKeepsEventLog()
    {
        GameObject settlementObject = new GameObject("Settlement_EventLog_Test");
        OperatingDaySettlementRuntime settlement = settlementObject.AddComponent<OperatingDaySettlementRuntime>();
        CharacterAiEditorTestDependencies.Inject(settlement);
        EventAlertRecord record = new EventAlertRecord(
            1,
            new EventAlertRequest("설계도 획득", "독 웅덩이", EventAlertImportance.Medium, "설계도"));

        settlement.OnTriggerEvent(new OperatingDayStartedEvent(1));
        settlement.OnTriggerEvent(new EventAlertLoggedEvent(record));
        settlement.OnTriggerEvent(new OperatingDayEndedEvent(1));

        OperatingDayReport report = settlement.LatestReport;
        bool valid = report != null
            && report.eventLog.Count == 1
            && report.eventLog[0] == "설계도 획득";

        Object.DestroyImmediate(settlementObject);
        return valid;
    }

    private static bool VerifyLoggedEventSnapshotDoesNotDrift()
    {
        EventAlertRecord record = new EventAlertRecord(
            9,
            new EventAlertRequest("Snapshot", "Immutable", EventAlertImportance.Low));
        EventAlertLoggedEvent logged = new EventAlertLoggedEvent(record);
        record.Increment();

        return logged.record != null
            && logged.record.Count == 1
            && logged.record.ButtonText == "Snapshot"
            && record.Count == 2;
    }

    private static EventAlertRuntime CreateRuntime(out GameObject root)
    {
        root = new GameObject("EventAlertRuntime_Test");
        EventAlertRuntime runtime = root.AddComponent<EventAlertRuntime>();
        runtime.Construct(
            new TestEventAlertViewPresenterFactory(),
            new DungeonStory.Foundation.GameEventBus());
        return runtime;
    }

    private sealed class TestEventAlertViewPresenterFactory : IEventAlertViewPresenterFactory
    {
        public IEventAlertViewPresenter Create(EventAlertViewPresenterContext context)
        {
            return new TestEventAlertViewPresenter();
        }
    }

    private sealed class TestEventAlertViewPresenter : IEventAlertViewPresenter
    {
        public bool IsDetailVisible { get; private set; }

        public void EnsureRuntimeUI() { }
        public void DestroyRuntimeUI() { }
        public void CreateButton(EventAlertRecord record) { }
        public void UpdateButton(EventAlertRecord record) { }
        public void OpenDetail(EventAlertRecord record) => IsDetailVisible = record != null;
        public void CloseDetail() => IsDetailVisible = false;
    }

    private static void CleanupRuntimeUi()
    {
        string[] names =
        {
            "EventAlertRuntimeUI",
            "EventAlertButtonRoot",
            "EventAlertDetailPanel",
            "RuntimeUICanvas"
        };

        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj != null
                && !EditorUtility.IsPersistent(obj)
                && System.Array.IndexOf(names, obj.name) >= 0)
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
