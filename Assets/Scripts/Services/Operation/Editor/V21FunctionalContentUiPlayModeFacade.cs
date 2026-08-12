#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class V21FunctionalContentUiPlayModeFacade
{
    public const string RequestPath = "Temp/v21-functional-content-ui.request";
    public const string ReportPath = "Artifacts/QA/v21-functional-content-ui-report.txt";

    static V21FunctionalContentUiPlayModeFacade()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    [MenuItem("DungeonStory/QA/Request V21 Functional Content UI")]
    public static void RequestRun()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.Ticks.ToString());
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlaying)
        {
            if (UnityEngine.Object.FindFirstObjectByType<
                    V21FunctionalContentUiPlayModeRunner>() == null)
            {
                new GameObject("V21 Functional Content UI Runner")
                    .AddComponent<V21FunctionalContentUiPlayModeRunner>();
            }
            return;
        }
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.EnterPlaymode();
        }
    }

    internal static void Finish(bool passed, string detail)
    {
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllLines(ReportPath, new[]
        {
            passed ? "RESULT=PASS" : "RESULT=FAIL",
            "target=V21_FUNCTIONAL_CONTENT_UI",
            detail ?? string.Empty
        });
        File.Delete(RequestPath);
        if (passed) Debug.Log("V21_FUNCTIONAL_CONTENT_UI=PASS; " + detail);
        else Debug.LogError("V21_FUNCTIONAL_CONTENT_UI=FAIL; " + detail);
    }
}

public sealed class V21FunctionalContentUiPlayModeRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;
        bool passed = false;
        string detail;
        try
        {
            detail = V21FunctionalContentUiPlayModeScenario.Run();
            passed = true;
        }
        catch (Exception exception)
        {
            detail = exception.ToString();
        }

        V21FunctionalContentUiPlayModeFacade.Finish(passed, detail);
        yield return null;
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode)
        {
            EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}

internal static class V21FunctionalContentUiPlayModeScenario
{
    public static string Run()
    {
        RecordingDomainCommands commands = new();
        GameEventBus eventBus = new();
        V21ContentAlertChoiceActionDispatcher dispatcher = new(
            new UnusedContentResolutionService(),
            new EmptyMilestoneWorldQuery(),
            new FixedCalendar(),
            eventBus,
            commands,
            commands,
            commands,
            commands,
            commands,
            commands,
            commands);
        GameObject root = new("V21 Functional Event Alert Runtime");
        EventAlertRuntime runtime = root.AddComponent<EventAlertRuntime>();
        runtime.Construct(
            new HeadlessPresenterFactory(),
            eventBus,
            new DungeonRuntimeAggregateRootStore(),
            dispatcher);

        CharacterId subject = new("character:qa-content-ui");
        Execute(runtime, "번식", V21ContentAlertActionIds.ReproductionStart("reproduction:qa"));
        Execute(runtime, "축제", V21ContentAlertActionIds.Festival("festival:sprout"));
        Execute(runtime, "장례", V21ContentAlertActionIds.Funeral(subject));
        Execute(runtime, "상담", V21ContentAlertActionIds.Counseling(subject));
        Execute(runtime, "치료", V21ContentAlertActionIds.AgeTreatment(
            subject,
            AgeTreatmentKind.BloodRejuvenation,
            "building-instance:qa"));

        Require(commands.ReproductionStarts == 1, "Reproduction UI action was not dispatched.");
        Require(commands.FestivalSchedules == 1 && commands.FestivalResolutions == 1,
            "Festival UI action did not schedule and resolve.");
        Require(commands.Funerals == 1, "Funeral UI action was not dispatched.");
        Require(commands.Counseling == 1, "Counseling UI action was not dispatched.");
        Require(commands.AgeTreatments == 1, "Age-treatment UI action was not dispatched.");
        Require(runtime.EventLog.Count == 5
                && Array.TrueForAll(
                    System.Linq.Enumerable.ToArray(runtime.EventLog),
                    runtime.IsDismissed),
            "Successful functional choices did not persist and dismiss their alerts.");
        UnityEngine.Object.Destroy(root);
        return "reproduction=1; festival=1/1; funeral=1; counseling=1; ageTreatment=1; alerts=5";
    }

    private static void Execute(EventAlertRuntime runtime, string title, string actionId)
    {
        runtime.OnTriggerEvent(new EventAlertRequestedEvent(new EventAlertRequest(
            title,
            "V21 기능 UI 수직 경로 검증",
            EventAlertImportance.High,
            "V21 QA",
            new[] { new EventAlertChoice("실행", "실제 typed command 실행", actionId) },
            "qa:" + actionId)));
        EventAlertRecord record = runtime.EventLog[runtime.EventLog.Count - 1];
        runtime.Open(record);
        Require(runtime.ExecuteChoice(0), $"Functional UI choice failed: {title}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class RecordingDomainCommands :
        IReproductionCommand,
        IFestivalCommand,
        IAgeTreatmentCommand,
        ISocialCareCommand,
        IDiseaseFieldResponseCommand,
        ICertifiedSeedCommand,
        ITraitAnalysisCommand
    {
        public int ReproductionStarts { get; private set; }
        public int FestivalSchedules { get; private set; }
        public int FestivalResolutions { get; private set; }
        public int Funerals { get; private set; }
        public int Counseling { get; private set; }
        public int AgeTreatments { get; private set; }

        public bool TryPlan(ReproductionPlanRequest request, out string processId, out DomainFailure failure)
        { processId = "reproduction:qa"; failure = DomainFailure.None; return true; }
        public bool TryStart(string processId, out DomainFailure failure) =>
            TryStart(processId, false, out failure);
        public bool TryStart(string processId, bool useFertilityTreatment, out DomainFailure failure)
        { ReproductionStarts++; failure = DomainFailure.None; return true; }
        public bool Schedule(FestivalScheduleRequest request, out FestivalPreparedOrder order, out DomainFailure failure)
        { FestivalSchedules++; order = new FestivalPreparedOrder(); failure = DomainFailure.None; return true; }
        public bool Resolve(FestivalPreparedOrder order, out DomainFailure failure)
        { FestivalResolutions++; failure = DomainFailure.None; return true; }
        public bool TryCreateOrder(AgeTreatmentOrderRequest request, out SurgeryOrder order, out DomainFailure failure)
        { AgeTreatments++; order = null; failure = DomainFailure.None; return true; }
        public bool TryHoldFuneral(string actionId, CharacterId deceasedId, IReadOnlyCollection<CharacterId> participantIds, string facilityInstanceId, out DomainFailure failure)
        { Funerals++; failure = DomainFailure.None; return true; }
        public bool TryHoldJointMemorial(string actionId, IReadOnlyCollection<CharacterId> deceasedIds, IReadOnlyCollection<CharacterId> participantIds, string facilityInstanceId, out DomainFailure failure)
        { failure = DomainFailure.None; return true; }
        public bool TryCounsel(string actionId, CharacterId patientId, out DomainFailure failure)
        { Counseling++; failure = DomainFailure.None; return true; }
        public bool TryApply(CharacterId characterId, string diseaseId, string responseId, string facilityInstanceId, out DomainFailure failure)
        { failure = DomainFailure.None; return true; }
        public bool TryPlan(string actionId, string cropId, string facilityInstanceId, out DomainFailure failure)
        { failure = DomainFailure.None; return true; }
        public int CompleteDeliveredPlans() => 0;
        public bool TryAnalyze(CharacterId characterId, out IReadOnlyList<string> revealedLatentTraitIds, out DomainFailure failure)
        { revealedLatentTraitIds = Array.Empty<string>(); failure = DomainFailure.None; return true; }
    }

    private sealed class EmptyMilestoneWorldQuery : IV20MilestoneWorldSnapshotQuery
    {
        public IReadOnlyList<CharacterActor> LivingCharacters => Array.Empty<CharacterActor>();
        public RunMilestoneEvaluationSnapshot Build(int absoluteDay) => new() { AbsoluteDay = absoluteDay };
    }

    private sealed class UnusedContentResolutionService : IContentResolutionService
    {
        public bool TryExecute(ContentResolutionRequest request, out ContentResolutionResult result, out DomainFailure failure)
        { result = null; failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable); return false; }
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        public int Day => 10;
        public int Hour => 12;
        public int Year => 1;
        public int DayOfYear => 10;
        public Season Season => Season.Spring;
        public int DayOfSeason => 10;
        public long AbsoluteHour => 228;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning => true;
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) => GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() { }
        public void SetDateTime(int day, int hour) { }
    }

    private sealed class HeadlessPresenterFactory : IEventAlertViewPresenterFactory
    {
        public IEventAlertViewPresenter Create(EventAlertViewPresenterContext context) => new HeadlessPresenter();
    }

    private sealed class HeadlessPresenter : IEventAlertViewPresenter
    {
        public bool IsDetailVisible { get; private set; }
        public void EnsureRuntimeUI() { }
        public void DestroyRuntimeUI(bool immediate = false) { }
        public void CreateButton(EventAlertRecord record) { }
        public void UpdateButton(EventAlertRecord record) { }
        public void RemoveButton(EventAlertRecord record) { }
        public void OpenDetail(EventAlertRecord record) => IsDetailVisible = true;
        public void CloseDetail() => IsDetailVisible = false;
    }
}
#endif
