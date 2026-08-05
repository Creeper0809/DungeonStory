using System;
using DungeonStory.Operation;
using DungeonStory.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
        RunScenario("right click dismisses alert without deleting history", VerifyRightClickDismissesAlert, errors);
        RunScenario("invalid alert payload fails preflight", VerifyInvalidPayloadFailsPreflight, errors);
        RunScenario("discarded alert candidate preserves live state and UI", VerifyDiscardedCandidatePreservesLiveState, errors);

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
        EventAlertRuntime runtime = CreateRuntime(out GameObject root);
        EventAlertRequest request = new EventAlertRequest(
            "Snapshot",
            "Immutable",
            EventAlertImportance.Low);
        runtime.OnTriggerEvent(new EventAlertRequestedEvent(request));
        EventAlertRecord record = runtime.EventLog[0];
        EventAlertLoggedEvent logged = new EventAlertLoggedEvent(record);
        runtime.OnTriggerEvent(new EventAlertRequestedEvent(request));

        bool valid = logged.record != null
            && logged.record.Count == 1
            && logged.record.ButtonText == "Snapshot"
            && record.Count == 2;
        Object.DestroyImmediate(root);
        CleanupRuntimeUi();
        return valid;
    }

    private static bool VerifyRightClickDismissesAlert()
    {
        EventAlertRuntime runtime = CreateRuntime(out GameObject runtimeRoot);
        EventAlertRequest request = new EventAlertRequest(
            "우클릭 테스트",
            "알림만 닫고 기록은 유지한다.",
            EventAlertImportance.Low,
            "QA");
        runtime.OnTriggerEvent(new EventAlertRequestedEvent(request));

        EventAlertRecord record = runtime.EventLog[0];
        runtime.Open(record);
        bool dismissed = runtime.Dismiss(record);
        bool historyPreserved = dismissed
            && runtime.IsDismissed(record)
            && runtime.EventLog.Count == 1
            && runtime.SelectedRecord == null
            && !runtime.IsDetailVisible;

        runtime.OnTriggerEvent(new EventAlertRequestedEvent(request));
        bool repeatedAlertReturns = !runtime.IsDismissed(record)
            && record.Count == 2;

        int rightClickCount = 0;
        int leftClickCount = 0;
        GameObject uiRoot = new GameObject("EventAlertRightClick_Test");
        GameObject eventSystemObject = new GameObject(
            "EventAlertRightClickEventSystem_Test",
            typeof(EventSystem));
        Button button = EventAlertUiFactory.CreateAlertButton(
            uiRoot.transform,
            record,
            () => leftClickCount++,
            () => rightClickCount++,
            new TestTmpKoreanFontService());
        PointerEventData pointer = new PointerEventData(eventSystemObject.GetComponent<EventSystem>())
        {
            button = PointerEventData.InputButton.Right
        };
        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerClickHandler);

        bool pointerHandled = rightClickCount == 1 && leftClickCount == 0;
        Object.DestroyImmediate(eventSystemObject);
        Object.DestroyImmediate(uiRoot);
        Object.DestroyImmediate(runtimeRoot);
        CleanupRuntimeUi();
        return historyPreserved && repeatedAlertReturns && pointerHandled;
    }

    private static bool VerifyInvalidPayloadFailsPreflight()
    {
        TestEventAlertSaveService saveService = new TestEventAlertSaveService();
        EventAlertSaveSection section = new EventAlertSaveSection(saveService);
        DungeonEventAlertSaveData payload = new DungeonEventAlertSaveData
        {
            records = new List<DungeonEventAlertRecordSaveData>
            {
                new DungeonEventAlertRecordSaveData
                {
                    id = 4,
                    title = "Valid",
                    count = 1
                },
                new DungeonEventAlertRecordSaveData
                {
                    id = 4,
                    title = string.Empty,
                    count = 0,
                    importance = (EventAlertImportance)99,
                    choices = new List<DungeonEventAlertChoiceSaveData>
                    {
                        new DungeonEventAlertChoiceSaveData { label = string.Empty }
                    }
                }
            }
        };
        bool invalidRejected = ThrowsInvalidOperation(() =>
            section.ValidatePayload(
                JsonUtility.ToJson(payload),
                section.SectionVersion,
                new DungeonGameRestoreReport()));
        bool legacyRejected = ThrowsInvalidOperation(() =>
            section.ValidatePayload(
                JsonUtility.ToJson(new DungeonEventAlertSaveData()),
                section.SectionVersion - 1,
                new DungeonGameRestoreReport()));
        bool emptyRejected = ThrowsInvalidOperation(() =>
            section.ValidatePayload(
                string.Empty,
                section.SectionVersion,
                new DungeonGameRestoreReport()));

        return invalidRejected
            && legacyRejected
            && emptyRejected
            && saveService.RestoreCount == 0;
    }

    private static bool ThrowsInvalidOperation(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool VerifyDiscardedCandidatePreservesLiveState()
    {
        DungeonRuntimeAggregateRootStore sourceRoot =
            new DungeonRuntimeAggregateRootStore();
        DungeonRuntimeAggregateRootStore targetRoot =
            new DungeonRuntimeAggregateRootStore();
        GameObject sourceObject = new GameObject("EventAlert_Discard_Source");
        GameObject targetObject = new GameObject("EventAlert_Discard_Target");
        try
        {
            EventAlertRuntime source = CreateRuntime(
                sourceObject,
                sourceRoot,
                new TestEventAlertViewPresenterFactory());
            source.OnTriggerEvent(new EventAlertRequestedEvent(
                new EventAlertRequest(
                    "Candidate only",
                    "Must never reach the live alert projection.",
                    EventAlertImportance.High,
                    "restore")));
            source.Dismiss(source.EventLog[0]);
            string candidatePayload = CreateSaveSection(source).Capture();

            TrackingEventAlertViewPresenterFactory targetFactory =
                new TrackingEventAlertViewPresenterFactory();
            EventAlertRuntime target = CreateRuntime(
                targetObject,
                targetRoot,
                targetFactory);
            target.OnTriggerEvent(new EventAlertRequestedEvent(
                new EventAlertRequest(
                    "Live alert",
                    "Must survive candidate discard.",
                    EventAlertImportance.Low,
                    "live")));
            targetFactory.Presenter.ResetMutationCounts();

            EventAlertSaveSection targetSection = CreateSaveSection(target);
            EventAlertFailureSection lateFailure = new EventAlertFailureSection
            {
                RemainingCommitFailures = 1
            };
            EventAlertDiscardObserver observer =
                new EventAlertDiscardObserver(target);
            DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
                new IDungeonSaveSection[]
                {
                    new EventAlertDependencySection(
                        OperatingDaySettlementSaveSection.Id,
                        DungeonSaveRestorePhase.LateRuntimeState),
                    new EventAlertDependencySection(
                        InvasionSaveSection.Id,
                        DungeonSaveRestorePhase.RuntimeState),
                    new EventAlertDependencySection(
                        OffenseAggregateSaveSection.Id,
                        DungeonSaveRestorePhase.RuntimeState),
                    targetSection,
                    lateFailure
                },
                targetRoot,
                new IDungeonRestoreTransactionParticipant[] { observer });
            List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
            envelopes.First(envelope => string.Equals(
                    envelope.sectionId,
                    EventAlertSaveSection.Id,
                    StringComparison.Ordinal))
                .payloadJson = candidatePayload;

            bool restored = registry.RestoreAll(
                envelopes,
                new DungeonGameRestoreReport());
            IReadOnlyList<EventAlertRecord> liveRecords = target.EventLog;
            return !restored
                && observer.DiscardCount == 1
                && observer.ObservedOnlyLiveAlert
                && liveRecords.Count == 1
                && liveRecords[0].Title == "Live alert"
                && !target.IsDismissed(liveRecords[0])
                && targetFactory.Presenter.DestroyCount == 0
                && targetFactory.Presenter.CreateCount == 0
                && targetRoot.PublishedRestoreRevision == 0;
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
            CleanupRuntimeUi();
        }
    }

    private static EventAlertRuntime CreateRuntime(out GameObject root)
    {
        root = new GameObject("EventAlertRuntime_Test");
        return CreateRuntime(
            root,
            new DungeonRuntimeAggregateRootStore(),
            new TestEventAlertViewPresenterFactory());
    }

    private static EventAlertRuntime CreateRuntime(
        GameObject root,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IEventAlertViewPresenterFactory presenterFactory)
    {
        EventAlertRuntime runtime = root.AddComponent<EventAlertRuntime>();
        runtime.Construct(
            presenterFactory,
            new DungeonStory.Foundation.GameEventBus(),
            aggregateRootStore);
        return runtime;
    }

    private static EventAlertSaveSection CreateSaveSection(
        EventAlertRuntime runtime)
    {
        DungeonSceneRuntimeReferences references =
            new DungeonSceneRuntimeReferences(
                new DungeonSceneServiceReferences(null, null, runtime, null),
                new DungeonSceneViewReferences(
                    null, null, null, null, null, null, null, null));
        return new EventAlertSaveSection(
            new EventAlertSaveService(references));
    }

    private sealed class TestEventAlertSaveService : IEventAlertSaveService
    {
        public int RestoreCount { get; private set; }

        public DungeonEventAlertSaveData Capture() =>
            new DungeonEventAlertSaveData();

        public EventAlertRestoreCandidate PrepareRestore(
            DungeonEventAlertSaveData source)
        {
            return new EventAlertRestoreCandidate(
                new EventAlertAggregateState());
        }

        public void PublishRestore(EventAlertRestoreCandidate candidate)
        {
            RestoreCount++;
        }
    }

    private sealed class EventAlertDependencySection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public EventAlertDependencySection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase)
        {
            SectionId = sectionId;
            RestorePhase = restorePhase;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new InvalidOperationException(
                    $"Unexpected dependency section version {sectionVersion}.");
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
        }
    }

    private sealed class EventAlertFailureSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public string SectionId => "operation.event-alerts.debug.failure";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Presentation;
        public IReadOnlyList<string> DependsOn =>
            new[] { EventAlertSaveSection.Id };
        public int RemainingCommitFailures { get; set; }
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new InvalidOperationException(
                    $"Unexpected failure section version {sectionVersion}.");
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                if (RemainingCommitFailures <= 0)
                {
                    return;
                }

                RemainingCommitFailures--;
                throw new InvalidOperationException(
                    "Injected event-alert restore failure.");
            });
        }
    }

    private sealed class EventAlertDiscardObserver :
        IDungeonRestoreTransactionParticipant
    {
        private readonly EventAlertRuntime runtime;
        private bool hasCandidate;

        public EventAlertDiscardObserver(EventAlertRuntime runtime)
        {
            this.runtime = runtime;
        }

        public string ParticipantId =>
            "operation.event-alerts.debug.discard-observer";
        public int DiscardCount { get; private set; }
        public bool ObservedOnlyLiveAlert { get; private set; }

        public void BeginRestoreCandidate()
        {
            hasCandidate = true;
        }

        public void PublishRestoreCandidate()
        {
            hasCandidate = false;
        }

        public void DiscardRestoreCandidate()
        {
            if (!hasCandidate)
            {
                return;
            }

            hasCandidate = false;
            DiscardCount++;
            IReadOnlyList<EventAlertRecord> records = runtime.EventLog;
            ObservedOnlyLiveAlert = records.Count == 1
                && records[0].Title == "Live alert"
                && !runtime.IsDismissed(records[0]);
        }
    }

    private sealed class TrackingEventAlertViewPresenterFactory :
        IEventAlertViewPresenterFactory
    {
        public TrackingEventAlertViewPresenter Presenter { get; } =
            new TrackingEventAlertViewPresenter();

        public IEventAlertViewPresenter Create(
            EventAlertViewPresenterContext context)
        {
            return Presenter;
        }
    }

    private sealed class TrackingEventAlertViewPresenter :
        IEventAlertViewPresenter
    {
        public bool IsDetailVisible { get; private set; }
        public int CreateCount { get; private set; }
        public int DestroyCount { get; private set; }

        public void ResetMutationCounts()
        {
            CreateCount = 0;
            DestroyCount = 0;
        }

        public void EnsureRuntimeUI() { }
        public void DestroyRuntimeUI() => DestroyCount++;
        public void CreateButton(EventAlertRecord record) => CreateCount++;
        public void UpdateButton(EventAlertRecord record) { }
        public void RemoveButton(EventAlertRecord record) { }
        public void OpenDetail(EventAlertRecord record) =>
            IsDetailVisible = record != null;
        public void CloseDetail() => IsDetailVisible = false;
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
        public void RemoveButton(EventAlertRecord record) { }
        public void OpenDetail(EventAlertRecord record) => IsDetailVisible = record != null;
        public void CloseDetail() => IsDetailVisible = false;
    }

    private sealed class TestTmpKoreanFontService : ITmpKoreanFontService
    {
        public TMP_FontAsset Resolve() => null;
        public void Apply(TMP_Text text) { }
        public void ApplyToChildren(Transform root, bool includeInactive = true) { }
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
