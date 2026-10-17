#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

/// <summary>
/// Production-live lifecycle coverage for captivity escape, wildlife capture
/// transport, and animal-care AI work.  Fixture state is authored through the
/// registered V18 save sections; terminal state is reached only by the real
/// Brain/Ability/runtime pipeline.
/// </summary>
public static class CaptivityWildlifeLifecyclePlayModeVerifier
{
    internal const string FullMode = "full";
    internal const string TransportOnlyMode = "transport-only";
    internal const string FeedColdResumeMode = "feed-cold-resume";
    internal const string HusbandryPendingColdResumeMode = "husbandry-pending-cold-resume";
    internal const string HusbandryPendingResumeWorldVariable = "DUNGEONSTORY_HUSBANDRY_PENDING_RESUME_WORLD";
    internal const string HusbandryPendingResumeHashVariable = "DUNGEONSTORY_HUSBANDRY_PENDING_RESUME_SHA256";
    internal const string FeedResumeWorldVariable = "DUNGEONSTORY_FEED_RESUME_WORLD";
    internal const string FeedResumeHashVariable = "DUNGEONSTORY_FEED_RESUME_SHA256";
    internal const string FeedObservationHashVariable = "DUNGEONSTORY_FEED_OBSERVATION_SHA256";
    private const string DefaultReportPath =
        "Artifacts/QA/captivity-wildlife-lifecycle-playmode.txt";
    internal const string E11ReportPathEnvironmentVariable =
        "DUNGEONSTORY_U0_E11_CAPTURE_REPORT_PATH";
    internal const string BackgroundNarrativeDeferralVariable =
        "DUNGEONSTORY_CAPTIVITY_BACKGROUND_LLM_DEFER";
    internal const string LiveNarrativeCaptureVariable =
        "DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE";
    public static string ReportPath
    {
        get
        {
            string configured = Environment.GetEnvironmentVariable(
                E11ReportPathEnvironmentVariable);
            return string.IsNullOrWhiteSpace(configured)
                ? DefaultReportPath
                : configured;
        }
    }
    private const string PendingPath =
        "Temp/captivity-wildlife-lifecycle-playmode.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static readonly List<string> teardownConsoleIssues = new();
    private static bool teardownConsoleCaptureArmed;
    private static bool batchExitArmed;

    [MenuItem("DungeonStory/Debug/QA/Run Captivity Wildlife Lifecycle Matrix")]
    public static void RequestRun() => RequestRun(FullMode);

    [MenuItem("DungeonStory/Debug/QA/Run Wildlife Capture Transport Only")]
    public static void RequestTransportOnlyRun() =>
        RequestRun(TransportOnlyMode);

    private static void RequestRun(string mode)
    {
        if (mode != FullMode && mode != TransportOnlyMode && mode != FeedColdResumeMode && mode != HusbandryPendingColdResumeMode)
            throw new InvalidOperationException("Unknown captivity verification mode: " + mode);
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, mode);
        if (EditorApplication.isPlaying)
        {
            StartRunner(
                exitPlayMode: false,
                transportOnly: string.Equals(
                    mode,
                    TransportOnlyMode,
                    StringComparison.Ordinal),
                feedColdResume: mode == FeedColdResumeMode,
                husbandryPendingColdResume: mode == HusbandryPendingColdResumeMode);
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    public static void RunBatch()
    {
        RunBatch(FullMode);
    }

    public static void RunTransportBatch()
    {
        RunBatch(TransportOnlyMode);
    }

    public static void RunFeedColdResumeBatch() => RunBatch(FeedColdResumeMode);
    public static void RunHusbandryPendingColdResumeBatch() => RunBatch(HusbandryPendingColdResumeMode);

    // A diagnostic of the exact scheduling/comparison expressions, not a
    // substitute for executing WildlifeCaptureRuntime or a gameplay PASS.
    public static void RunCareClockBoundaryBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (!Application.isBatchMode || EditorApplication.isPlaying
            || string.IsNullOrWhiteSpace(configured)
            || !Path.IsPathRooted(configured)
            || !Path.GetFullPath(configured).StartsWith(
                Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Care clock diagnostic requires a scoped isolated EditMode batch.");
        string report = Path.Combine(configured, "wildlife-care-clock-boundary.json");
        var observations = new JArray();
        var failures = new JArray();
        int falseRejections = 0;
        int productionAccepted = 0;
        int invalidRejected = 0;
        var validator = typeof(WildlifeCaptureRuntime).GetMethod("CaptureRemainingCareDelay",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The actual production care deadline validator is missing.");
        foreach (float time in new[] { 0.1f, 0.3f, 1.1f, 3.9f, 4.1f, 7.1f,
            9.208319f, 15.1f, 31.1f, 63.1f, 127.1f, 255.1f, 1023.1f, 65531.1f })
        {
            IGameClock probeClock = new CareClockExpressionProbe(time);
            var state = new CapturedWildlifeState
            {
                wildlifeId = "fixture:care-clock",
                nextCareAt = probeClock.Time + CapturedWildlifeState.CareIntervalSeconds,
                remainingCareSeconds = 0.25f
            };
            float capturedAt = probeClock.Time;
            float roundedMaximum = BitConverter.ToSingle(BitConverter.GetBytes(
                capturedAt + CapturedWildlifeState.CareIntervalSeconds), 0);
            bool rejected = state.nextCareAt > capturedAt + CapturedWildlifeState.CareIntervalSeconds;
            if (rejected) falseRejections++;
            bool accepted = TryProbeRemainingCareDelay(validator, state, capturedAt,
                out float remaining, out string validFailure);
            if (accepted && remaining >= 0f && remaining <= CapturedWildlifeState.CareIntervalSeconds
                && state.remainingCareSeconds == 0.25f) productionAccepted++;
            else failures.Add("legitimate clock=" + capturedAt.ToString("R") + ": " + validFailure);
            float outside = BitConverter.ToSingle(BitConverter.GetBytes(
                BitConverter.ToInt32(BitConverter.GetBytes(roundedMaximum), 0) + 1), 0);
            var invalid = state.Clone();
            invalid.nextCareAt = outside;
            bool outsideRejected = !TryProbeRemainingCareDelay(validator, invalid, capturedAt,
                out _, out string outsideFailure)
                && outsideFailure.Contains("care delay exceeds its normal interval", StringComparison.Ordinal)
                && invalid.remainingCareSeconds == 0.25f;
            if (outsideRejected) invalidRejected++;
            else failures.Add("next-representable clock=" + capturedAt.ToString("R") + ": " + outsideFailure);
            observations.Add(new JObject
            {
                ["clock"] = capturedAt,
                ["clockBits"] = BitConverter.ToInt32(BitConverter.GetBytes(capturedAt), 0),
                ["storedDeadline"] = state.nextCareAt,
                ["deadlineBits"] = BitConverter.ToInt32(BitConverter.GetBytes(state.nextCareAt), 0),
                ["roundedMaximum"] = roundedMaximum,
                ["exactMathematicalMaximum"] = (double)capturedAt + CapturedWildlifeState.CareIntervalSeconds,
                ["directExpressionRejectsLegitimateStoredDeadline"] = rejected,
                ["roundedMaximumRejects"] = state.nextCareAt > roundedMaximum,
                ["productionAccepted"] = accepted,
                ["productionRemainingCareSeconds"] = accepted ? new JValue(remaining) : JValue.CreateNull(),
                ["nextRepresentableDeadline"] = outside,
                ["nextRepresentableDeadlineRejected"] = outsideRejected
            });
        }
        foreach (float invalidDeadline in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.1f })
        {
            var invalid = new CapturedWildlifeState { wildlifeId = "fixture:care-clock:invalid", nextCareAt = invalidDeadline };
            bool invalidFailed = !TryProbeRemainingCareDelay(validator, invalid, 1f, out _, out string invalidFailure)
                && invalidFailure.Contains("invalid care deadline", StringComparison.Ordinal);
            if (invalidFailed) invalidRejected++;
            else failures.Add("invalid deadline=" + invalidDeadline.ToString("R") + ": " + invalidFailure);
        }
        var overdue = new CapturedWildlifeState { wildlifeId = "fixture:care-clock:overdue", nextCareAt = 0f };
        bool overdueReady = TryProbeRemainingCareDelay(validator, overdue, 1f, out float overdueRemaining, out string overdueFailure)
            && overdueRemaining == 0f;
        if (!overdueReady) failures.Add("overdue care: " + overdueFailure);
        Directory.CreateDirectory(configured);
        using (var stream = new FileStream(report, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream))
            writer.Write(new JObject
            {
                ["schemaVersion"] = 1,
                ["status"] = failures.Count == 0 ? "PRODUCTION_CARE_DEADLINE_BOUNDARY_PASS" : "PRODUCTION_CARE_DEADLINE_BOUNDARY_FAIL",
                ["legacyExpressionFalseRejections"] = falseRejections,
                ["productionAccepted"] = productionAccepted,
                ["invalidRejected"] = invalidRejected,
                ["overdueReadyForNormalTick"] = overdueReady,
                ["observations"] = observations,
                ["failures"] = failures,
                ["productionDeadlineValidatorExecuted"] = true,
                ["productionOwnerExecuted"] = false,
                ["gameplayPassClaimed"] = false,
                ["trainingEligible"] = false
            }.ToString());
        if (failures.Count != 0)
            throw new InvalidOperationException("Production care deadline boundary failed; inspect " + report);
        Debug.Log("CARE_CLOCK_PRODUCTION_BOUNDARY_PASS accepted=" + productionAccepted
            + "; rejectedInvalid=" + invalidRejected + "; overdueReady=" + overdueReady);
    }

    private static bool TryProbeRemainingCareDelay(System.Reflection.MethodInfo validator,
        CapturedWildlifeState state, float capturedAt, out float remaining, out string failure)
    {
        try
        {
            remaining = (float)validator.Invoke(null, new object[] { state, capturedAt });
            failure = string.Empty;
            return true;
        }
        catch (System.Reflection.TargetInvocationException error)
            when (error.InnerException is InvalidOperationException invalid)
        {
            remaining = 0f;
            failure = invalid.Message;
            return false;
        }
    }

    private sealed class CareClockExpressionProbe : IGameClock
    {
        internal CareClockExpressionProbe(float time) => Time = time;
        public float DeltaTime => 0f;
        public float Time { get; }
        public int FrameCount => 0;
        public bool IsPaused => true;
    }

    private static void RunBatch(string mode)
    {
        string configured = Environment.GetEnvironmentVariable(
            E11ReportPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured)
            && (File.Exists(ReportPath)
                || (mode == TransportOnlyMode
                    && File.Exists(Path.ChangeExtension(ReportPath, ".raw.json")))))
        {
            throw new InvalidOperationException(
                "E11 capture evidence is immutable and already exists: "
                + ReportPath);
        }
        bool deferBackgroundNarrative = Application.isBatchMode
            && (mode == FullMode || mode == TransportOnlyMode
                || mode == FeedColdResumeMode || mode == HusbandryPendingColdResumeMode);
        if (deferBackgroundNarrative)
        {
            if (EditorApplication.isPlaying
                || !DungeonPersistencePaths.IsIsolatedVerification
                || string.IsNullOrWhiteSpace(configured)
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                    LiveNarrativeCaptureVariable))
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                    BackgroundNarrativeDeferralVariable)))
            {
                throw new InvalidOperationException(
                    "Background narrative deferral requires a fresh, explicitly bound isolated batch.");
            }
            // Set before scene/Play startup so the existing capture mode never
            // starts a native host. This fixture does not supply any response.
            Environment.SetEnvironmentVariable(LiveNarrativeCaptureVariable, "1");
            Environment.SetEnvironmentVariable(BackgroundNarrativeDeferralVariable, "1");
        }
        try
        {
            if (!batchExitArmed)
            {
                batchExitArmed = true;
                EditorApplication.playModeStateChanged += ExitBatchAfterRun;
            }
            RequestRun(mode);
        }
        catch
        {
            if (deferBackgroundNarrative) ClearBackgroundNarrativeDeferral();
            throw;
        }
    }

    private static void ClearBackgroundNarrativeDeferral()
    {
        if (Environment.GetEnvironmentVariable(BackgroundNarrativeDeferralVariable) != "1")
            return;
        Environment.SetEnvironmentVariable(LiveNarrativeCaptureVariable, null);
        Environment.SetEnvironmentVariable(BackgroundNarrativeDeferralVariable, null);
    }

    private static void ExitBatchAfterRun(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode || !batchExitArmed)
            return;
        EditorApplication.playModeStateChanged -= ExitBatchAfterRun;
        batchExitArmed = false;
        // All Play queues have already torn down. No queued request is released
        // into a response-less capture dispatch while the fixture is ending.
        ClearBackgroundNarrativeDeferral();
        EditorApplication.delayCall += () =>
        {
            bool passed = File.Exists(ReportPath)
                && File.ReadLines(ReportPath).Any(line =>
                    line.StartsWith("RESULT=PASS", StringComparison.Ordinal));
            EditorApplication.Exit(passed ? 0 : 1);
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!File.Exists(PendingPath)) return;
        string mode = File.ReadAllText(PendingPath).Trim();
        if (mode != FullMode && mode != TransportOnlyMode && mode != FeedColdResumeMode && mode != HusbandryPendingColdResumeMode)
            throw new InvalidOperationException("Unknown pending captivity verification mode: " + mode);
        File.Delete(PendingPath);
        StartRunner(
            exitPlayMode: true,
            transportOnly: string.Equals(
                mode,
                TransportOnlyMode,
                StringComparison.Ordinal),
            feedColdResume: mode == FeedColdResumeMode,
            husbandryPendingColdResume: mode == HusbandryPendingColdResumeMode);
    }

    private static void StartRunner(bool exitPlayMode, bool transportOnly, bool feedColdResume,
        bool husbandryPendingColdResume)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CaptivityWildlifeLifecyclePlayModeRunner>() != null)
        {
            return;
        }

        CaptivityWildlifeLifecyclePlayModeRunner runner =
            new GameObject(nameof(CaptivityWildlifeLifecyclePlayModeRunner))
                .AddComponent<CaptivityWildlifeLifecyclePlayModeRunner>();
        runner.Configure(exitPlayMode, transportOnly, feedColdResume, husbandryPendingColdResume);
    }

    internal static void ArmTeardownConsoleCapture()
    {
        if (teardownConsoleCaptureArmed)
        {
            return;
        }
        teardownConsoleIssues.Clear();
        teardownConsoleCaptureArmed = true;
        Application.logMessageReceived += CaptureTeardownConsoleIssue;
        EditorApplication.playModeStateChanged += CompleteTeardownConsoleCapture;
    }

    private static void CaptureTeardownConsoleIssue(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Warning
            or LogType.Error
            or LogType.Exception
            or LogType.Assert)
        {
            teardownConsoleIssues.Add($"{type}:{condition}");
        }
    }

    private static void CompleteTeardownConsoleCapture(
        PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }
        Application.logMessageReceived -= CaptureTeardownConsoleIssue;
        EditorApplication.playModeStateChanged -= CompleteTeardownConsoleCapture;
        teardownConsoleCaptureArmed = false;
        if (teardownConsoleIssues.Count == 0 || !File.Exists(ReportPath))
        {
            teardownConsoleIssues.Clear();
            return;
        }

        List<string> report = File.ReadAllLines(ReportPath).ToList();
        if (report.Any(line => line.StartsWith(
                "FAIL\tTEARDOWN_CONSOLE_WARNING_ERROR_ZERO",
                StringComparison.Ordinal)))
        {
            teardownConsoleIssues.Clear();
            return;
        }
        int existingFailures = 0;
        if (report.Count > 1)
        {
            string marker = "failures=";
            int markerIndex = report[1].IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                int.TryParse(report[1].Substring(markerIndex + marker.Length), out existingFailures);
            }
            report[1] = $"RESULT=FAIL; failures={existingFailures + 1}";
        }
        string detail = string.Join(" | ", teardownConsoleIssues);
        report.Add($"FAIL\tTEARDOWN_CONSOLE_WARNING_ERROR_ZERO\t{detail}");
        report.Add($"FAILURE\tTEARDOWN_CONSOLE_WARNING_ERROR_ZERO: {detail}");
        File.WriteAllLines(ReportPath, report);
        teardownConsoleIssues.Clear();
    }
}

public sealed class CaptivityWildlifeLifecyclePlayModeRunner : MonoBehaviour
{
    private readonly List<string> rows = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly List<BuildableObject> fixtureBuildings = new();
    private readonly List<FixtureBuildingSnapshot> fixtureBuildingSnapshots =
        new();
    private readonly List<AreaSnapshot> areaSnapshots = new();
    private readonly List<PreparedOriginalBuildingSnapshot> preparedOriginalBuildings =
        new();
    private readonly List<DisplacedWildlifeSnapshot> displacedWildlife = new();
    private readonly List<MonoBehaviourState> pausedAi = new();
    private readonly List<IGridOccupant> faultWalls = new();

    private bool exitPlayMode;
    private bool transportOnly;
    private bool feedColdResume;
    private bool husbandryPendingColdResume;
    private bool IsColdResume => feedColdResume || husbandryPendingColdResume;
    private float oldTimeScale;
    private DungeonRuntimeLifetimeScope scope;
    private ICharacterAiWorldRegistry world;
    private IDungeonSaveSectionRegistry saveRegistry;
    private DungeonRuntimeAggregateRootStore aggregateStore;
    private Grid grid;
    private IRoomLayoutCache rooms;
    private IGridPathSearchBroker pathSearchBroker;
    private IGameEventBus gameEvents;
    private ICaptivityRuntime captivity;
    private ICaptivityCommandService captivityCommands;
    private ICaptivityEscapeRuntime escape;
    private ICaptivityPersistence captivityPersistence;
    private ICircusPersistence circusPersistence;
    private IWildlifeCaptureRuntime capture;
    private IWorldItemStackRuntime itemRuntime;
    private IPhysicalItemBatchDispositionService batchDispositions;
    private IResourceEconomyContentCatalog resourceCatalog;
    private WildlifeRuntime wildlife;
    private IWildlifeSpeciesCatalogProvider wildlifeSpecies;
    private IAnimalHusbandryPersistence husbandryPersistence;
    private IAnimalHusbandryQuery husbandryQuery;
    private IWorkPolicyRegistry workPolicyRegistry;
    private IFacilityCandidateCache facilityCandidateCache;
    private IEmergencyWorkAccountingService emergencyAccounting;
    private ISettlementAlertService settlementAlerts;
    private ISettlementAlertPersistence settlementAlertPersistence;
    private CharacterAlarmResponseRuntime alarmResponses;
    private IExperiencePacingRuntime experiencePacing;
    private InvasionThreatRuntime invasionThreat;
    private InvasionDirectorRuntime invasionDirector;
    private IDisposable invasionStartedProbeSubscription;
    private IDisposable invasionResolvedProbeSubscription;
    private IDisposable invasionCandidateProbeSubscription;
    private IDisposable activeIncidentsProbeSubscription;
    private int invasionStartedEventCount;
    private int invasionResolvedEventCount;
    private int invasionCandidateEventCount;
    private int activeIncidentsChangedEventCount;
    private readonly List<string> invasionEventTrace = new();
    private ICharacterProficiencyQuery proficiencies;
    private ICharacterBodyHealthQuery bodyHealthQuery;
    private ICharacterBodyHealthCommand bodyHealthCommands;
    private ICharacterMedicalQuery medicalQuery;
    private ICharacterMedicalCommand medicalCommands;
    private ICharacterDeprivationRuntime deprivationRuntime;
    private IGameCalendar calendar;
    private GameplayOutcomeLedger outcomeLedger;
    private readonly JArray e11TransportObservations = new();
    private string e11TransportWildlifeId;
    private bool e11TransportRawWritten;
    private CharacterActor worker;
    private CharacterActor captiveActor;
    private AbilityWork workerWork;
    private WorkPriorityLevel oldAnimalCarePriority;
    private CharacterType captiveOldType;
    private List<DungeonSaveSectionEnvelope> baseline;
    private IDungeonGameSaveService gameSaveService;
    private DungeonGameSaveData baselineSaveData;
    private ModularFacilityWorldSaveData baselineFacilityWorld;
    private CaptivitySaveData confinedPayload;
    private BuildableObject housing;
    private BuildableObject pen;
    private BuildableObject faultPen;
    private RoomInstance fixtureRoom;
    private Vector2Int workerCell;
    private Vector2Int captiveCell;
    private Vector2Int escapeConnectorCell;
    private Vector2Int escapeExteriorAnchorCell;
    private string fixtureStage = string.Empty;
    private string placementFailure = string.Empty;
    private bool verificationBodyCompleted;
    private bool runFinished;
    private LocalLlmRequestQueue deferredNarrativeQueue;
    private float nextMemoryObservation;
    private int memoryObservationCount;
    private string lastMemoryObservation = "memoryDiagnostic=NOT_OBSERVED";

    public void Configure(bool shouldExitPlayMode, bool runTransportOnly, bool runFeedColdResume,
        bool runHusbandryPendingColdResume)
    {
        exitPlayMode = shouldExitPlayMode;
        transportOnly = runTransportOnly;
        feedColdResume = runFeedColdResume;
        husbandryPendingColdResume = runHusbandryPendingColdResume;
    }

    private IEnumerator Start()
    {
        oldTimeScale = Time.timeScale;
        Time.timeScale = 8f;
        Application.logMessageReceived += CaptureConsoleIssue;
        try
        {
            ObserveBatchMemory();
            if (Application.isBatchMode)
            {
                // One explicit preparation collection, before fixture commands.
                // Do not collect during gameplay to conceal retained growth.
                long managedBefore = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                int collectionsBefore = GC.CollectionCount(GC.MaxGeneration);
                GC.Collect();
                rows.Add("INFO\tBATCH_STARTUP_GC_PREPARATION\t"
                    + $"managedBeforeBytes={managedBefore};"
                    + $"managedAfterBytes={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()};"
                    + $"fullCollections={collectionsBefore}->{GC.CollectionCount(GC.MaxGeneration)};"
                    + "one-preparation-only;gameplay-collection=false;trainingEligible=false");
                ObserveBatchMemory();
                WriteReport();
            }
            if (!transportOnly && !IsColdResume)
                Check(CaptiveEscapeOutcomeDebugScenarios.RunCanonicalSaveRoundTrip(),
                    "ESCAPE_OUTCOME_CANONICAL_SAVE_REPLAY_AND_TIER_REJECTION",
                    "four canonical kinds;exact hashes preserved;replay duplicate0;"
                    + "production memory schedule/slice;initial Recent/retained Episodic;"
                    + "immutable tier mutation rejected by canonical initial-row guard;"
                    + "rejection leaves live ledger unchanged;"
                    + "controlled-contract-test;trainingEligible=false");
            if (!IsColdResume) yield return VerifyPickupRetargetContract();
            if (failures.Count == 0) yield return ResolveAndBuildFixture();
            if (feedColdResume && failures.Count == 0)
                yield return VerifyCompletedFeedColdResume();
            if (husbandryPendingColdResume && failures.Count == 0)
                yield return VerifyHusbandryPendingColdResume();
            if (!transportOnly && !IsColdResume && failures.Count == 0)
                yield return EstablishConfinedCaptive();
            if (!transportOnly && !IsColdResume && failures.Count == 0)
                yield return VerifyEscapeRows();
            if (!IsColdResume && failures.Count == 0)
                yield return transportOnly ? VerifyE11TransportGuarded() : VerifyTransportRows();
            if (!transportOnly && !IsColdResume && failures.Count == 0)
                yield return VerifyAnimalCareRow();
            verificationBodyCompleted = true;
        }
        finally
        {
            if (Application.isBatchMode)
            {
                Check(deferredNarrativeQueue != null
                    && deferredNarrativeQueue.RunningCount == 0
                    && !deferredNarrativeQueue.IsBundledModelStarting
                    && !deferredNarrativeQueue.IsBundledModelRunning
                    && !deferredNarrativeQueue.HasConfiguredEndpoint,
                    "BACKGROUND_LLM_DEFERRED_THROUGH_RUN",
                    $"queued={deferredNarrativeQueue?.QueuedCount};"
                    + $"running={deferredNarrativeQueue?.RunningCount};"
                    + "requests-retained-until-normal-queue-teardown;"
                    + "no-response-provider;trainingEligible=false");
            }
            Cleanup();
            Application.logMessageReceived -= CaptureConsoleIssue;
            Check(consoleIssues.Count == 0,
                "CONSOLE_WARNING_ERROR_ZERO",
                consoleIssues.Count == 0
                    ? "0/0"
                    : string.Join(" | ", consoleIssues));
            Time.timeScale = oldTimeScale;
            runFinished = true;
            WriteReport();
            if (exitPlayMode)
            {
                CaptivityWildlifeLifecyclePlayModeVerifier
                    .ArmTeardownConsoleCapture();
            }
            Destroy(gameObject);
            if (exitPlayMode)
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlaying)
                        EditorApplication.isPlaying = false;
                };
            }
        }
    }

    private void Update()
    {
        if (!Application.isBatchMode
            || runFinished
            || Time.realtimeSinceStartup < nextMemoryObservation)
        {
            return;
        }

        ObserveBatchMemory();
        WriteReport();
    }

    private void ObserveBatchMemory(string phaseOverride = null)
    {
        if (!Application.isBatchMode) return;
        nextMemoryObservation = Time.realtimeSinceStartup + 1f;
        string phase = phaseOverride ?? (rows.Count == 0
            ? "before-first-check"
            : rows[rows.Count - 1].Split('\t')[1]);
        lastMemoryObservation =
            $"memoryDiagnostic=qa-observation-only;sample={++memoryObservationCount};"
            + $"phase={phase};frame={Time.frameCount};"
            + $"realtime={Time.realtimeSinceStartup:0.###};unityScaledTime={Time.time:0.###};"
            + $"calendarHours={calendar?.AbsoluteHour};"
            + $"managedUsedBytes={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()};"
            + $"managedHeapBytes={UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong()};"
            + $"engineAllocatedBytes={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()};"
            + $"engineReservedBytes={UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()}";
        File.AppendAllText(
            Path.ChangeExtension(
                CaptivityWildlifeLifecyclePlayModeVerifier.ReportPath,
                ".memory.tsv"),
            lastMemoryObservation + Environment.NewLine);
    }

    private IEnumerator ResolveAndBuildFixture()
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            if (scope?.Container != null) break;
            yield return null;
        }
        Check(scope?.Container != null, "LIVE_SCOPE", scope?.name ?? "missing");
        if (scope?.Container == null) yield break;

        if (Application.isBatchMode)
        {
            deferredNarrativeQueue = scope.Container
                .Resolve<ILocalLlmRuntimeProvider>().GetRequiredRuntime()
                as LocalLlmRequestQueue;
            bool prepared = DungeonPersistencePaths.IsIsolatedVerification
                && Environment.GetEnvironmentVariable(
                    CaptivityWildlifeLifecyclePlayModeVerifier.BackgroundNarrativeDeferralVariable) == "1"
                && Environment.GetEnvironmentVariable(
                    CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable) == "1"
                && deferredNarrativeQueue != null
                && deferredNarrativeQueue.RunningCount == 0
                && !deferredNarrativeQueue.IsBundledModelStarting
                && !deferredNarrativeQueue.IsBundledModelRunning
                && !deferredNarrativeQueue.HasConfiguredEndpoint
                && deferredNarrativeQueue.CaptureReadiness().State
                    == LocalLlmRuntimeReadinessState.Starting;
            Check(prepared, "BACKGROUND_LLM_DEFERRAL_PREPARATION",
                $"queuePresent={deferredNarrativeQueue != null};"
                + $"running={deferredNarrativeQueue?.RunningCount};"
                + $"hostStarting={deferredNarrativeQueue?.IsBundledModelStarting};"
                + $"hostRunning={deferredNarrativeQueue?.IsBundledModelRunning};"
                + "isolated-non-target-preparation;trainingEligible=false");
            if (!prepared) yield break;
            int queuedBeforeDeferral = deferredNarrativeQueue.QueuedCount;
            deferredNarrativeQueue.ConfigureIsolatedRequestCapture(request =>
                Check(false, "BACKGROUND_LLM_UNEXPECTED_DISPATCH",
                    "Background requests must remain queued in this fixture."));
            deferredNarrativeQueue.ConfigureIsolatedAutomaticDispatch(false);
            Check(deferredNarrativeQueue.QueuedCount == queuedBeforeDeferral
                && deferredNarrativeQueue.CaptureReadiness().State
                    == LocalLlmRuntimeReadinessState.Ready,
                "BACKGROUND_LLM_REQUESTS_RETAINED",
                $"queued={queuedBeforeDeferral}->{deferredNarrativeQueue.QueuedCount};"
                + "clear=false;abort=false;response-provider=false;trainingEligible=false");
            if (failures.Count != 0) yield break;
        }

        ObserveBatchMemory("before-persistence-dispose");
        (scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable)
            ?.Dispose();
        ObserveBatchMemory("after-dungeon-persistence-dispose");
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        ObserveBatchMemory("after-meta-persistence-dispose");

        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        proficiencies = scope.Container.Resolve<ICharacterProficiencyQuery>();
        bodyHealthQuery = scope.Container.Resolve<ICharacterBodyHealthQuery>();
        bodyHealthCommands = scope.Container.Resolve<ICharacterBodyHealthCommand>();
        medicalQuery = scope.Container.Resolve<ICharacterMedicalQuery>();
        medicalCommands = scope.Container.Resolve<ICharacterMedicalCommand>();
        deprivationRuntime = scope.Container.Resolve<ICharacterDeprivationRuntime>();
        calendar = scope.Container.Resolve<IGameCalendar>();
        outcomeLedger = scope.Container.Resolve<GameplayOutcomeLedger>();
        ObserveBatchMemory("after-character-owner-resolve");
        if (world.Characters.Count(actor =>
                actor != null
                && !actor.IsDead
                && HasCanonicalProficiencyProfile(actor)) < 2)
        {
            ObserveBatchMemory("before-start-party-commit");
            rows.Add("INFO\tSTART_PARTY\t"
                + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            ObserveBatchMemory("after-start-party-commit");
            for (int frame = 0; frame < 8; frame++) yield return null;
        }
        // Start-party setup normalizes the global simulation speed. Restore
        // this verifier's bounded acceleration after that authority has
        // finished so four alert hours do not become thirty seconds of
        // unrelated world simulation before the transport row.
        Time.timeScale = 8f;
        Check(Mathf.Approximately(Time.timeScale, 8f),
            "FIXTURE_SIMULATION_SPEED",
            $"timeScale={Time.timeScale:0.###}");

        CharacterActor[] actors = world.Characters
            .Where(actor => actor != null && !actor.IsDead)
            .Where(HasCanonicalProficiencyProfile)
            .ToArray();
        worker = actors.FirstOrDefault(actor =>
            actor.characterType == CharacterType.NPC);
        captiveActor = actors.FirstOrDefault(actor =>
            actor != worker && actor.characterType == CharacterType.NPC);
        Check(worker != null && captiveActor != null,
            "LIVE_ACTORS", $"actors={actors.Length};"
            + $"worker={DescribeActor(worker)};captive={DescribeActor(captiveActor)}");
        if (worker == null || captiveActor == null) yield break;

        saveRegistry = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        gameSaveService = scope.Container.Resolve<IDungeonGameSaveService>();
        aggregateStore = scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
        captivityPersistence = scope.Container.Resolve<ICaptivityPersistence>();
        captivity = scope.Container.Resolve<ICaptivityRuntime>();
        captivityCommands = scope.Container.Resolve<ICaptivityCommandService>();
        escape = scope.Container.Resolve<ICaptivityEscapeRuntime>();
        circusPersistence = scope.Container.Resolve<ICircusPersistence>();
        capture = scope.Container.Resolve<IWildlifeCaptureRuntime>();
        itemRuntime = scope.Container.Resolve<IWorldItemStackRuntime>();
        batchDispositions =
            scope.Container.Resolve<IPhysicalItemBatchDispositionService>();
        resourceCatalog =
            scope.Container.Resolve<IResourceEconomyContentCatalog>();
        wildlife = scope.Container.Resolve<WildlifeRuntime>();
        wildlifeSpecies = scope.Container.Resolve<IWildlifeSpeciesCatalogProvider>();
        husbandryPersistence = scope.Container.Resolve<IAnimalHusbandryPersistence>();
        husbandryQuery = scope.Container.Resolve<IAnimalHusbandryQuery>();
        workPolicyRegistry = scope.Container.Resolve<IWorkPolicyRegistry>();
        facilityCandidateCache = scope.Container.Resolve<IFacilityCandidateCache>();
        emergencyAccounting = scope.Container.Resolve<IEmergencyWorkAccountingService>();
        settlementAlerts = scope.Container.Resolve<ISettlementAlertService>();
        settlementAlertPersistence =
            scope.Container.Resolve<ISettlementAlertPersistence>();
        alarmResponses = scope.Container.Resolve<CharacterAlarmResponseRuntime>();
        experiencePacing = scope.Container.Resolve<IExperiencePacingRuntime>();
        rooms = scope.Container.Resolve<IRoomLayoutCache>();
        pathSearchBroker = scope.Container.Resolve<IGridPathSearchBroker>();
        gameEvents = scope.Container.Resolve<IGameEventBus>();
        invasionThreat = FindFirstObjectByType<InvasionThreatRuntime>(
            FindObjectsInactive.Include);
        invasionDirector = FindFirstObjectByType<InvasionDirectorRuntime>(
            FindObjectsInactive.Include);
        BeginInvasionEventDiagnostics();
        bool isolatedInvasionFixture = experiencePacing != null
            && !experiencePacing.AllowsRandomInvasion
            && invasionDirector != null
            && invasionDirector.ActiveIntruders.Count == 0;
        Check(isolatedInvasionFixture,
            "ESCAPE_INVASION_FIXTURE_ISOLATED",
            $"day={experiencePacing?.CurrentDay};"
            + $"randomAllowed={experiencePacing?.AllowsRandomInvasion};"
            + $"activeIntruders={invasionDirector?.ActiveIntruders.Count};"
            + $"threat={invasionThreat?.CurrentThreat:0.###};"
            + $"safety={invasionThreat?.SafetyRemaining:0.###};"
            + $"candidatePending={invasionThreat?.IsCandidatePending}");
        Check(world.TryGetGrid(out grid) && grid != null,
            "LIVE_GRID", grid != null ? $"{grid.width}x{grid.height}" : "missing");
        if (grid == null) yield break;

        baselineSaveData = gameSaveService.Capture();
        baseline = baselineSaveData.sections;
        baselineFacilityWorld = JsonUtility.FromJson<ModularFacilityWorldSaveData>(
            baseline.Single(value => value.sectionId == ModularFacilityWorldSaveSection.Id)
                .payloadJson);
        PauseOtherAi();
        worker.TryGetAbility(out workerWork);
        oldAnimalCarePriority = workerWork != null
            ? workerWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.AnimalCare)
            : WorkPriorityLevel.Off;
        captiveOldType = captiveActor.characterType;
        // The cold route restores the exact incoming world, not new GUID rooms.
        if (IsColdResume) yield break;

        List<ActorAiPauseState> preparationPauses = world.Characters
            .Where(actor => actor != null)
            .Select(actor => new ActorAiPauseState(actor, actor.IsAiPaused()))
            .ToList();
        try
        {
            foreach (ActorAiPauseState state in preparationPauses)
            {
                CharacterActor actor = state.Actor;
                actor.SetAiPaused(true);
                actor.Brain?.StopCurrentActionForReplan("qa-room-preparation");
                actor.GetComponent<AbilityHaul>()?.StopHauling("qa-room-preparation");
                actor.GetComponent<AbilityMove>()?.CancelActiveMovement("qa-room-preparation");
            }
            int settledFrames = 0;
            float roomPreparationDeadline = Time.realtimeSinceStartup + 3f;
            while (settledFrames < 2 && Time.realtimeSinceStartup < roomPreparationDeadline)
            {
                yield return null;
                settledFrames = preparationPauses.All(state =>
                    IsFixtureActorQuiescent(state.Actor)) ? settledFrames + 1 : 0;
            }
            Check(settledFrames >= 2, "FIXTURE_ORIGINAL_OWNERS_QUIESCENT",
                $"actors={preparationPauses.Count};settled={settledFrames}/2;"
                + "actual-cancellation-before-building-preparation;trainingEligible=false");
            if (settledFrames < 2) yield break;
            Check(CreateAuthoredRoom(), "AUTHORED_ROOM",
                $"housing={housing != null};pen={pen != null};faultPen={faultPen != null};room={fixtureRoom?.Id};stage={fixtureStage}");
        }
        finally
        {
            RestoreActorAiPauseStates(preparationPauses);
        }
    }

    private bool IsFixtureActorQuiescent(CharacterActor actor)
    {
        AbilityWork work = actor?.GetComponent<AbilityWork>();
        return actor != null
            && actor.Brain?.bestAction == null
            && actor.Brain?.IsExternallyDrivenActionActive != true
            && actor.GetComponent<AbilityHaul>()?.IsHauling != true
            && work?.isWorking != true
            && work?.HasActiveWorkRoutineForDiagnostics != true
            && actor.GetComponent<AbilityMove>()?.HasActiveMovementRoutineForDiagnostics != true
            && world.GetTransientBuildingOwnershipCount(actor.Identity.TypedPersistentId) == 0;
    }

    private IEnumerator EstablishConfinedCaptive()
    {
        bool wasPaused = worker.IsAiPaused();
        try
        {
            worker.SetLifecycleState(CharacterLifecycleState.Active);
            worker.SetAiPaused(true);
            worker.Brain?.StopCurrentActionForReplan("qa-captive-capture-preparation");
            AbilityHaul preparationHaul = worker.GetComponent<AbilityHaul>();
            AbilityMove preparationMove = worker.GetComponent<AbilityMove>();
            preparationHaul?.StopHauling("qa-captive-capture-preparation");
            preparationMove?.CancelActiveMovement("qa-captive-capture-preparation");
            // Lifecycle cleanup can close an earlier visit/movement and change
            // position synchronously. Finish it before placing either actor.
            Vector2Int captiveBeforeCleanup = captiveActor.GetNowXY();
            captiveActor.characterType = CharacterType.Intruder;
            captiveActor.SetLifecycleState(CharacterLifecycleState.Downed);
            captiveActor.SetAiPaused(true);
            AbilityMove captivePreparationMove = captiveActor.GetComponent<AbilityMove>();
            AbilityWork captivePreparationWork = captiveActor.GetComponent<AbilityWork>();
            AbilityHaul captivePreparationHaul = captiveActor.GetComponent<AbilityHaul>();
            rows.Add("INFO\tCAPTIVE_CAPTURE_LIFECYCLE_PREPARATION\t"
                + $"position={captiveBeforeCleanup}->{captiveActor.GetNowXY()};"
                + $"lifecycle={captiveActor.CurrentLifecycleState};"
                + $"moveCancel={captivePreparationMove?.LastActionMovementCancellationReasonForDiagnostics};"
                + "position-not-yet-prepared;trainingEligible=false");
            int settledFrames = 0;
            float settleDeadline = Time.realtimeSinceStartup + 3f;
            while (settledFrames < 2 && Time.realtimeSinceStartup < settleDeadline)
            {
                yield return null;
                bool settled = worker.Brain?.bestAction == null
                    && worker.Brain?.IsExternallyDrivenActionActive != true
                    && preparationHaul?.IsHauling != true
                    && workerWork?.isWorking != true
                    && workerWork?.HasActiveWorkRoutineForDiagnostics != true
                    && preparationMove?.HasActiveMovementRoutineForDiagnostics != true
                    && captiveActor.Brain?.bestAction == null
                    && captiveActor.Brain?.IsExternallyDrivenActionActive != true
                    && captivePreparationHaul?.IsHauling != true
                    && captivePreparationWork?.isWorking != true
                    && captivePreparationWork?.HasActiveWorkRoutineForDiagnostics != true
                    && captivePreparationMove?.HasActiveMovementRoutineForDiagnostics != true;
                settledFrames = settled ? settledFrames + 1 : 0;
            }
            Check(settledFrames >= 2, "CAPTIVE_CAPTURE_ATOMIC_PREPARATION",
                $"settled={settledFrames}/2;paused={worker.IsAiPaused()};"
                + $"action={worker.Brain?.bestAction?.actionset?.GetType().Name};"
                + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
                + $"haul={preparationHaul?.IsHauling};work={workerWork?.isWorking}:"
                + $"{workerWork?.HasActiveWorkRoutineForDiagnostics};"
                + $"movement={preparationMove?.HasActiveMovementRoutineForDiagnostics};"
                + $"captiveAction={captiveActor.Brain?.bestAction?.actionset?.GetType().Name};"
                + $"captiveExternal={captiveActor.Brain?.IsExternallyDrivenActionActive};"
                + $"captiveHaul={captivePreparationHaul?.IsHauling};"
                + $"captiveWork={captivePreparationWork?.isWorking}:"
                + $"{captivePreparationWork?.HasActiveWorkRoutineForDiagnostics};"
                + $"captiveMovement={captivePreparationMove?.HasActiveMovementRoutineForDiagnostics};"
                + "trainingEligible=false;previous-work-stopped-before-position-preparation");
            if (settledFrames < 2) yield break;

            // The initial Downed transition above only closes existing owners.
            // Prepare the existing test-only capture eligibility after the frame
            // barrier, before placement, with no yield before the real command.
            captiveActor.characterType = CharacterType.Intruder;
            captiveActor.SetLifecycleState(CharacterLifecycleState.Downed);
            captiveActor.SetAiPaused(true);
            worker.transform.position = grid.GetWorldPos(workerCell);
            captiveActor.transform.position = grid.GetWorldPos(captiveCell);
            CharacterLifecycle captiveLifecycle = captiveActor.GetComponent<CharacterLifecycle>();
            bool preparationPositionExact = worker.GetNowXY() == workerCell
                && captiveActor.GetNowXY() == captiveCell
                && captiveLifecycle != null
                && captiveLifecycle.transform == captiveActor.transform;
            Check(preparationPositionExact, "CAPTIVE_CAPTURE_POSITION_PREPARATION",
                $"worker={worker.GetNowXY()}/{workerCell};"
                + $"captive={captiveActor.GetNowXY()}/{captiveCell};"
                + $"actorWorld={captiveActor.transform.position};"
                + $"lifecycleWorld={captiveLifecycle?.transform.position};"
                + $"sameTransform={captiveLifecycle != null && captiveLifecycle.transform == captiveActor.transform};"
                + "position-after-lifecycle-cleanup;trainingEligible=false");
            if (!preparationPositionExact) yield break;

            bool captureEligibilityPrepared = !captiveActor.IsDead
                && captiveActor.characterType == CharacterType.Intruder
                && captiveActor.CurrentLifecycleState == CharacterLifecycleState.Downed;
            Check(captureEligibilityPrepared, "CAPTIVE_CAPTURE_ELIGIBILITY_PREPARATION",
                $"role={captiveActor.characterType};dead={captiveActor.IsDead};"
                + $"lifecycle={captiveActor.CurrentLifecycleState};"
                + $"bodyDowned={bodyHealthQuery.GetSnapshot(captiveActor).Downed};"
                + "explicit-test-only-lifecycle-preparation;natural-injury=false;trainingEligible=false");
            if (!captureEligibilityPrepared) yield break;

            IDoorAccessQuery doorAccess = scope.Container.Resolve<IDoorAccessQuery>();
            GridTraversalContext workerContext = GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(worker),
                DoorAccessOverrideKind.None);
            Vector2Int subjectCell = captiveActor.GetNowXY();
            Vector2Int legacySubjectCell = new(subjectCell.x + 2, subjectCell.y);
            GridPathSearchResult approach = grid.SearchPathTo(
                worker.GetNowXY(), subjectCell,
                position => doorAccess.CanTraverse(grid, position, workerContext, out _),
                DefaultGridTraversalCostPolicy.Instance, workerContext);
            GridPathSearchResult legacyApproach = grid.SearchPathTo(
                worker.GetNowXY(), legacySubjectCell,
                position => doorAccess.CanTraverse(grid, position, workerContext, out _),
                DefaultGridTraversalCostPolicy.Instance, workerContext);
            bool approachReady = approach?.ContainsPosition(subjectCell) == true;
            Check(approachReady, "CAPTIVE_CAPTURE_APPROACH_GEOMETRY",
                $"worker={worker.GetNowXY()};subject={subjectCell};housing={housing.centerPos};"
                + $"reachable={approachReady};legacySubject={legacySubjectCell};"
                + $"legacyReachable={legacyApproach?.ContainsPosition(legacySubjectCell) == true};"
                + "query=normal-character-traversal;actual-broker-terminal-still-required");
            if (!approachReady) yield break;

            CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(worker);
            IDungeonItemCatalogProvider catalog =
                scope.Container.Resolve<IDungeonItemCatalogProvider>();
            IItemHaulingSettingsProvider hauling =
                scope.Container.Resolve<IItemHaulingSettingsProvider>();
            bool supplied = inventory.TryAdd(
                "qa:captivity-wildlife:restraint",
                CaptivityItemDefinitions.RestraintsItemId,
                1,
                catalog,
                hauling,
                out string reason);
            bool ordered = supplied
                && captivityCommands.TryOrderCapture(captiveActor, worker, out reason);
            Check(ordered, "CAPTIVE_CAPTURE_STARTED", reason);
            if (!ordered) yield break;

            string captiveId = CharacterPersistentIdentity.Require(captiveActor).Value;
            AbilityCaptiveEscort escort = worker.GetComponent<AbilityCaptiveEscort>();
            string lastObservedState = null;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (captivity.TryGetCaptive(captiveId, out CaptiveState state))
                {
                    string observedState = $"{state?.status}|{state?.lastResult}|"
                        + $"{escort?.IsEscorting}|{worker.Brain?.ExternalIntentOwnerId}";
                    if (!string.Equals(lastObservedState, observedState, StringComparison.Ordinal))
                    {
                        rows.Add("INFO\tCAPTIVE_CAPTURE_OBSERVED\t"
                            + $"frame={Time.frameCount};worker={worker.GetNowXY()};"
                            + $"subject={captiveActor.GetNowXY()};status={state?.status};"
                            + $"result={state?.lastResult};escorting={escort?.IsEscorting};"
                            + $"paused={worker.IsAiPaused()};external={worker.Brain?.IsExternallyDrivenActionActive};"
                            + $"owner={worker.Brain?.ExternalIntentOwnerId};phase={worker.Brain?.CurrentActionPhase};"
                            + $"gridRevision={grid.TraversalVersion};"
                            + (pathSearchBroker as GridPathSearchBroker)?.DescribeExactSearchForDiagnostics(
                                grid, worker.GetNowXY(), captiveActor.GetNowXY(), workerContext));
                        lastObservedState = observedState;
                    }
                    if (state?.status == CaptivityStatus.Confined
                        || (state?.status == CaptivityStatus.AwaitingCapture
                            && escort?.IsEscorting != true))
                    {
                        // A production terminal failure is already final. Do not wait
                        // thirty seconds for an unrelated autonomous action to overwrite it.
                        break;
                    }
                }
                yield return null;
            }
            bool confined = captivity.TryGetCaptive(captiveId, out CaptiveState captured)
                && captured?.status == CaptivityStatus.Confined;
            Check(confined, "CAPTIVE_CAPTURE_TERMINAL",
                $"status={captured?.status};result={captured?.lastResult};"
                + $"worker={worker.GetNowXY()};subject={captiveActor.GetNowXY()};"
                + $"housing={housing.centerPos};phase={worker.Brain?.CurrentActionPhase};"
                + $"moveFailure={worker.GetComponent<AbilityMove>()?.LastGridMoveFailureReason}");
            if (!confined) yield break;
            confinedPayload = Clone(captivityPersistence.Capture());
        }
        finally
        {
            worker.SetAiPaused(wasPaused);
        }
    }

    private IEnumerator VerifyEscapeRows()
    {
        string captiveId = CharacterPersistentIdentity.Require(captiveActor).Value;

        // Capture deliberately leaves a physically incapacitated captive in
        // Downed.  A real invasion must not turn that state into a free heal or
        // an escape.  Keep this as a distinct pre-start gate from the later
        // running-action lifecycle interruption row.
        Check(RestoreEscapeReadyCaptivity(captiveId),
            "ESCAPE_DOWNED_PRESTART_RESTORE", captiveId);
        captivity.TryGetCaptive(captiveId, out CaptiveState state);
        EscapeStartProbe probe = new();
        int downedAttemptsBefore = state?.failedEscapeAttempts ?? -1;
        yield return TryBeginEscapeThroughInvasion(
            state,
            "qa-downed-prestart",
            probe);
        bool started = probe.Started;
        string reason = probe.Reason;
        captivity.TryGetCaptive(captiveId, out CaptiveState downedPrestart);
        AbilityCaptiveEscape ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        Check(!started
              && captiveActor.CurrentLifecycleState == CharacterLifecycleState.Downed
              && downedPrestart?.status == CaptivityStatus.Confined
              && downedPrestart.failedEscapeAttempts == downedAttemptsBefore
              && ability?.IsEscaping != true,
            "ESCAPE_DOWNED_PRESTART_DENIED",
            $"started={started};lifecycle={captiveActor.CurrentLifecycleState};"
            + $"status={downedPrestart?.status};attempts={downedPrestart?.failedEscapeAttempts};"
            + $"active={ability?.IsEscaping};reason={reason}");

        Check(RestoreEscapeReadyCaptivity(captiveId), "ESCAPE_NOPATH_RESTORE", captiveId);
        Check(RecoverCaptiveThroughMedicalAuthority(out string recoveryDetail),
            "ESCAPE_NOPATH_MEDICAL_RECOVERY", recoveryDetail);
        EscapeRouteProbe routeProbe = new();
        yield return WaitForProductionEscapeRoute(routeProbe);
        Check(routeProbe.Ready,
            "ESCAPE_NOPATH_ROUTE_PREFLIGHT", routeProbe.Detail);
        captivity.TryGetCaptive(captiveId, out state);
        probe = new EscapeStartProbe();
        AbilityMove escapeMove = captiveActor.GetComponent<AbilityMove>();
        IGridPathSearchBroker originalMoveBroker = escapeMove?
            .DebugReplacePathSearchBroker(new NoPathSearchBroker());
        yield return TryBeginEscapeThroughInvasion(state, "qa-no-path", probe);
        started = probe.Started;
        reason = probe.Reason;
        if (escapeMove != null && originalMoveBroker != null)
            escapeMove.DebugReplacePathSearchBroker(originalMoveBroker);
        ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        yield return WaitUntil(() =>
            captivity.TryGetCaptive(captiveId, out CaptiveState current)
            && current?.status == CaptivityStatus.Confined, 12f);
        captivity.TryGetCaptive(captiveId, out CaptiveState noPath);
        Check(started && noPath?.status == CaptivityStatus.Confined
              && noPath.failedEscapeAttempts == 1
              && ability?.IsEscaping != true,
            "ESCAPE_NOPATH_TERMINAL",
            $"started={started};status={noPath?.status};attempts={noPath?.failedEscapeAttempts}");

        Check(RestoreEscapeReadyCaptivity(captiveId),
            "ESCAPE_RUNNING_DOWNED_RESTORE", captiveId);
        Check(RecoverCaptiveThroughMedicalAuthority(out recoveryDetail),
            "ESCAPE_RUNNING_DOWNED_MEDICAL_RECOVERY", recoveryDetail);
        routeProbe = new EscapeRouteProbe();
        yield return WaitForProductionEscapeRoute(routeProbe);
        Check(routeProbe.Ready,
            "ESCAPE_RUNNING_DOWNED_ROUTE_PREFLIGHT", routeProbe.Detail);
        captivity.TryGetCaptive(captiveId, out state);
        int runningDownedAttemptsBefore = state?.failedEscapeAttempts ?? -1;
        probe = new EscapeStartProbe();
        yield return TryBeginEscapeThroughInvasion(state, "qa-running-downed", probe);
        started = probe.Started;
        reason = probe.Reason;
        ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        if (started) captiveActor.SetLifecycleState(CharacterLifecycleState.Downed);
        yield return null;
        yield return null;
        captivity.TryGetCaptive(captiveId, out CaptiveState downed);
        AbilityMove downedMove = captiveActor.GetComponent<AbilityMove>();
        CharacterAiRuntimeGateSnapshot downedGate =
            captiveActor.Brain.CaptureRuntimeGateSnapshot();
        int attemptsAfterFirstCleanup = downed?.failedEscapeAttempts ?? -1;
        captiveActor.SetLifecycleState(CharacterLifecycleState.Downed);
        yield return null;
        captivity.TryGetCaptive(captiveId, out CaptiveState downedAfterDuplicateCleanup);
        Check(started && downed?.status == CaptivityStatus.Confined
            && downed.failedEscapeAttempts == runningDownedAttemptsBefore + 1
            && downedAfterDuplicateCleanup?.failedEscapeAttempts
                == attemptsAfterFirstCleanup
            && ability?.IsEscaping != true
            && ability?.HasEscapePassForDiagnostics != true
            && downedMove?.HasActiveMovementRoutineForDiagnostics != true
            && downedGate.LivePathRequests == 0,
            "ESCAPE_RUNNING_DOWNED_TERMINAL",
            $"status={downed?.status};active={ability?.IsEscaping};"
            + $"pass={ability?.HasEscapePassForDiagnostics};"
            + $"attempts={runningDownedAttemptsBefore}->{attemptsAfterFirstCleanup}"
            + $"->{downedAfterDuplicateCleanup?.failedEscapeAttempts};"
            + $"movement={downedMove?.HasActiveMovementRoutineForDiagnostics};"
            + $"paths={downedGate.LivePathRequests}");

        Check(RestoreEscapeReadyCaptivity(captiveId), "ESCAPE_DISABLE_RESTORE", captiveId);
        Check(RecoverCaptiveThroughMedicalAuthority(out recoveryDetail),
            "ESCAPE_DISABLE_MEDICAL_RECOVERY", recoveryDetail);
        routeProbe = new EscapeRouteProbe();
        yield return WaitForProductionEscapeRoute(routeProbe);
        Check(routeProbe.Ready,
            "ESCAPE_DISABLE_ROUTE_PREFLIGHT", routeProbe.Detail);
        captivity.TryGetCaptive(captiveId, out state);
        probe = new EscapeStartProbe();
        yield return TryBeginEscapeThroughInvasion(state, "qa-disable", probe);
        started = probe.Started;
        reason = probe.Reason;
        ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        captivity.TryGetCaptive(captiveId, out CaptiveState beforeDisable);
        bool activeBeforeDisable = started
            && beforeDisable?.status == CaptivityStatus.EscapeAttempt
            && ability?.IsEscaping == true;
        Check(activeBeforeDisable,
            "ESCAPE_DISABLE_ACTIVE",
            $"started={started};status={beforeDisable?.status};"
            + $"active={ability?.IsEscaping};position={captiveActor.GetNowXY()};"
            + $"reason={reason}");
        if (activeBeforeDisable) ability.enabled = false;
        yield return null;
        captivity.TryGetCaptive(captiveId, out CaptiveState disabled);
        Check(activeBeforeDisable && disabled?.status == CaptivityStatus.Confined
              && ability?.IsEscaping != true,
            "ESCAPE_DISABLE_TERMINAL",
            $"status={disabled?.status};active={ability?.IsEscaping}");
        if (ability != null) ability.enabled = true;

        // Success is last because the production terminal owns an exterior
        // departure.  Preparing it through medical authority avoids reviving a
        // Downed captive with a test-only lifecycle setter.
        Check(RestoreEscapeReadyCaptivity(captiveId),
            "ESCAPE_SUCCESS_RESTORE", captiveId);
        Check(RecoverCaptiveThroughMedicalAuthority(out recoveryDetail),
            "ESCAPE_SUCCESS_MEDICAL_RECOVERY", recoveryDetail);
        routeProbe = new EscapeRouteProbe();
        yield return WaitForProductionEscapeRoute(routeProbe);
        Check(routeProbe.Ready,
            "ESCAPE_SUCCESS_ROUTE_PREFLIGHT", routeProbe.Detail);
        captivity.TryGetCaptive(captiveId, out state);
        int successInitialAttempts = state.failedEscapeAttempts;
        probe = new EscapeStartProbe();
        yield return TryBeginEscapeThroughInvasion(state, "qa-success", probe);
        started = probe.Started;
        reason = probe.Reason;
        Check(started, "ESCAPE_SUCCESS_STARTED", reason);
        AbilityMove successExitMove =
            captiveActor.GetComponent<AbilityMove>();
        ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        float escapeObservationStartedAt = Time.realtimeSinceStartup;
        float escapeObservationStartedScaledTime = Time.time;
        float escapeObservationStartedGameTime =
            successExitMove?.GameClockTimeForDiagnostics ?? -1f;
        int escapeObservationStartedFrame = Time.frameCount;
        float escapeObservationDeadline = escapeObservationStartedAt + 30f;
        float lastEscapeObservationAt = float.NegativeInfinity;
        string lastEscapeObservation = null;
        while (Time.realtimeSinceStartup < escapeObservationDeadline)
        {
            captivity.TryGetCaptive(captiveId, out CaptiveState current);
            string observation = $"{current?.status}|{current?.lastResult}|"
                + $"{current?.failedEscapeAttempts}|{captiveActor.GetNowXY()}|"
                + $"{captiveActor.CurrentLifecycleState}|{ability?.IsEscaping}|"
                + $"{successExitMove?.IsSystemMoveInProgress}|"
                + $"{successExitMove?.LastGridMoveFailureReason}|"
                + $"{successExitMove?.LastActionMovementCancellationReasonForDiagnostics}";
            if (!string.Equals(lastEscapeObservation, observation, StringComparison.Ordinal)
                || Time.realtimeSinceStartup - lastEscapeObservationAt >= 5f)
            {
                CharacterBodyHealthSnapshot health =
                    bodyHealthQuery.GetSnapshot(captiveActor);
                rows.Add("INFO\tESCAPE_SUCCESS_OBSERVED\t"
                    + $"frame={Time.frameCount};frames={Time.frameCount - escapeObservationStartedFrame};"
                    + $"realElapsed={Time.realtimeSinceStartup - escapeObservationStartedAt:0.###};"
                    + $"scaledElapsed={Time.time - escapeObservationStartedScaledTime:0.###};"
                    + $"gameTime={successExitMove?.GameClockTimeForDiagnostics};"
                    + $"gameDelta={successExitMove?.GameClockDeltaTimeForDiagnostics};"
                    + $"gameStart={escapeObservationStartedGameTime};hour={calendar.AbsoluteHour};"
                    + $"status={current?.status};result={current?.lastResult};"
                    + $"attempts={successInitialAttempts}->{current?.failedEscapeAttempts};"
                    + $"position={captiveActor.GetNowXY()};destination={current?.escapeDestination};"
                    + $"lifecycle={captiveActor.CurrentLifecycleState};health={captiveActor.CurrentHealth};"
                    + $"bodyDowned={health.Downed};escapeActive={ability?.IsEscaping};"
                    + $"pass={ability?.HasEscapePassForDiagnostics};"
                    + $"systemMove={successExitMove?.IsSystemMoveInProgress};"
                    + $"moveRoutine={successExitMove?.HasActiveMovementRoutineForDiagnostics};"
                    + $"moveFailure={successExitMove?.LastGridMoveFailureReason};"
                    + $"moveCancel={successExitMove?.LastActionMovementCancellationReasonForDiagnostics};"
                    + $"moveOwner={successExitMove?.ActiveMovementOperationOwnerForDiagnostics};"
                    + $"movePreemption={successExitMove?.LastMovementOperationPreemptionForDiagnostics};"
                    + $"phase={captiveActor.Brain?.CurrentActionPhase};gridRevision={grid.TraversalVersion}");
                lastEscapeObservation = observation;
                lastEscapeObservationAt = Time.realtimeSinceStartup;
            }
            if (current?.status == CaptivityStatus.Escaped
                || (current?.status == CaptivityStatus.Confined
                    && current.failedEscapeAttempts > successInitialAttempts
                    && ability?.IsEscaping != true))
            {
                // A real terminal failure cannot become a successful escape by
                // waiting through unrelated gameplay. Preserve its reason now.
                break;
            }
            yield return null;
        }
        bool escapeSucceeded = captivity.TryGetCaptive(
                captiveId, out CaptiveState observedEscape)
            && observedEscape?.status == CaptivityStatus.Escaped;
        if (escapeSucceeded)
        {
            yield return WaitUntil(() =>
            {
                bool movementTerminal = successExitMove == null
                    || (!successExitMove.IsSystemMoveInProgress
                        && !successExitMove.HasActiveMovementRoutineForDiagnostics);
                bool lifecycleHandoffTerminal = captiveActor == null
                    || !captiveActor.gameObject.activeInHierarchy
                    || captiveActor.CurrentLifecycleState
                        != CharacterLifecycleState.ExitingDungeon;
                return movementTerminal
                    && lifecycleHandoffTerminal
                    && captiveActor?.Brain?.IsExternallyDrivenActionActive != true;
            }, 30f);
            string completedOwnerBefore = JsonUtility.ToJson(captivityPersistence.Capture());
            CharacterLifecycleState completedLifecycle = captiveActor.CurrentLifecycleState;
            CharacterType completedType = captiveActor.characterType;
            bool completedPaused = captiveActor.IsAiPaused();
            // Replay the same production callback used by lifecycle cleanup,
            // after real arrival/completion. No terminal state is injected.
            escape.FailEscape(captiveId, captiveActor, "qa-late-exit-lifecycle-callback");
            captivity.TryGetCaptive(captiveId, out CaptiveState afterLateFailure);
            Check(afterLateFailure?.status == CaptivityStatus.Escaped
                && afterLateFailure.escapeOutcomeRevision > 0L
                && string.Equals(completedOwnerBefore,
                    JsonUtility.ToJson(captivityPersistence.Capture()), StringComparison.Ordinal)
                && captiveActor.CurrentLifecycleState == completedLifecycle
                && captiveActor.characterType == completedType
                && captiveActor.IsAiPaused() == completedPaused,
                "ESCAPE_COMPLETED_LATE_FAILURE_REPLAY_NO_MUTATION",
                $"status={afterLateFailure?.status};revision={afterLateFailure?.escapeOutcomeRevision};"
                + $"attempts={afterLateFailure?.failedEscapeAttempts};"
                + $"lifecycle={completedLifecycle}->{captiveActor.CurrentLifecycleState};"
                + "actual-arrival/owner-completion;explicit-production-callback-replay;trainingEligible=false");
        }
        captivity.TryGetCaptive(captiveId, out CaptiveState escaped);
        ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        CharacterAiRuntimeGateSnapshot exitGate =
            captiveActor.Brain.CaptureRuntimeGateSnapshot();
        bool exitHandoffTerminal =
            escaped?.status == CaptivityStatus.Escaped
            && successExitMove?.IsSystemMoveInProgress != true
            && successExitMove?.HasActiveMovementRoutineForDiagnostics
                != true
            && captiveActor.CurrentLifecycleState
                != CharacterLifecycleState.ExitingDungeon
            && captiveActor.Brain.IsExternallyDrivenActionActive != true
            && exitGate.LivePathRequests == 0;
        Check(escaped?.status == CaptivityStatus.Escaped
              && ability?.IsEscaping != true,
            "ESCAPE_SUCCESS_TERMINAL",
            $"status={escaped?.status};active={ability?.IsEscaping};"
            + $"result={escaped?.lastResult};attempts={successInitialAttempts}->{escaped?.failedEscapeAttempts};"
            + $"position={captiveActor.GetNowXY()};destination={escaped?.escapeDestination};"
            + $"lifecycle={captiveActor.CurrentLifecycleState};health={captiveActor.CurrentHealth};"
            + $"moveFailure={successExitMove?.LastGridMoveFailureReason};"
            + $"realElapsed={Time.realtimeSinceStartup - escapeObservationStartedAt:0.###};"
            + $"scaledElapsed={Time.time - escapeObservationStartedScaledTime:0.###};"
            + $"frames={Time.frameCount - escapeObservationStartedFrame}");
        Check(exitHandoffTerminal,
            "ESCAPE_SUCCESS_EXIT_HANDOFF_TERMINAL",
            $"lifecycle={captiveActor.CurrentLifecycleState};"
            + $"active={captiveActor.gameObject.activeInHierarchy};"
            + $"systemMove={successExitMove?.IsSystemMoveInProgress};"
            + "moveRoutine="
            + $"{successExitMove?.HasActiveMovementRoutineForDiagnostics};"
            + $"external={captiveActor.Brain.IsExternallyDrivenActionActive};"
            + $"paths={exitGate.LivePathRequests}");

        yield return ResolveEscapeInvasionAndWaitForGreen();
    }

    private IEnumerator ResolveEscapeInvasionAndWaitForGreen()
    {
        int activeIntrudersBeforeRestore =
            invasionDirector?.ActiveIntruders.Count ?? -1;
        bool retaliationProduced = invasionDirector != null
            && activeIntrudersBeforeRestore > 0
            && invasionCandidateEventCount > 0;
        Check(retaliationProduced, "ESCAPE_RETALIATION_PRODUCED",
            $"activeIntruders={activeIntrudersBeforeRestore};"
            + $"candidateEvents={invasionCandidateEventCount};"
            + $"startedEvents={invasionStartedEventCount}");
        if (!retaliationProduced) yield break;

        CharacterActor[] fixtureIntruders = invasionDirector.ActiveIntruders
            .Select(runtime => runtime != null ? runtime.IntruderActor : null)
            .ToArray();
        HashSet<string> departingIds = fixtureIntruders
            .Select(actor => actor.Identity.PersistentId)
            .ToHashSet(StringComparer.Ordinal);
        ICombatEquipmentRuntime equipment =
            scope.Container.Resolve<ICombatEquipmentRuntime>();
        DungeonCombatEquipmentSaveData equipmentBefore = equipment.Capture();
        CombatEquipmentInstance[] departingEquipment = equipment.Instances
            .Where(instance => departingIds.Contains(instance.ownerCharacterId))
            .Select(instance => instance.Clone())
            .ToArray();
        string otherLoadoutsBefore = string.Join("\n", equipmentBefore.loadouts
            .Where(state => !departingIds.Contains(state.characterId))
            .OrderBy(state => state.characterId, StringComparer.Ordinal)
            .Select(state => JsonUtility.ToJson(state)));
        string otherEquipmentBefore = string.Join("\n", equipment.Instances
            .Where(instance => !departingIds.Contains(instance.ownerCharacterId))
            .OrderBy(instance => instance.instanceId, StringComparer.Ordinal)
            .Select(instance => JsonUtility.ToJson(instance)));
        HashSet<string> departingEquipmentIds = departingEquipment
            .Select(instance => instance.instanceId).ToHashSet(StringComparer.Ordinal);
        int instanceCountBefore = equipment.Instances.Count;
        // The actual retaliation has already been observed. End this unrelated
        // fixture through the real withdrawal owner. Restore-only retirement
        // cannot be used outside its complete world replacement transaction.
        // This preparation is not a battle victory or natural training sample.
        int withdrawn = invasionDirector.WithdrawActiveIntrudersForFinalInvasion();
        bool invasionAuthorityClean = withdrawn == fixtureIntruders.Length
            && invasionDirector.ActiveIntruders.Count == 0
            && fixtureIntruders.All(actor => actor == null
                || (!actor.gameObject.activeInHierarchy
                    && actor.CurrentLifecycleState == CharacterLifecycleState.Despawned));
        Check(invasionAuthorityClean,
            "ESCAPE_RETALIATION_FIXTURE_INTRUDERS_REMOVED",
            $"activeIntruders={activeIntrudersBeforeRestore}"
            + $"->{invasionDirector?.ActiveIntruders.Count};"
            + $"candidateEvents={invasionCandidateEventCount};"
            + $"startedEvents={invasionStartedEventCount};"
            + $"withdrawn={withdrawn};"
            + "owner=InvasionDirectorRuntime.WithdrawActiveIntrudersForFinalInvasion;"
            + "battleVictory=false;invasionSaveRestore=NOT_RUN;trainingEligible=false");
        if (!invasionAuthorityClean)
        {
            yield break;
        }

        DungeonCombatEquipmentSaveData equipmentAfter = equipment.Capture();
        bool loadoutsRetired = !equipmentAfter.loadouts.Any(
            state => departingIds.Contains(state.characterId));
        bool ownedEquipmentRetired = departingEquipment.Length > 0
            && equipment.Instances.Count == instanceCountBefore
            && departingEquipment.All(before => equipment.Instances.Any(after =>
                after.instanceId == before.instanceId
                && after.definitionId == before.definitionId
                && after.ownerCharacterId == string.Empty
                && after.sourceStackId == string.Empty
                && after.worldState == CombatEquipmentWorldState.Lost));
        string otherLoadoutsAfter = string.Join("\n", equipmentAfter.loadouts
            .Where(state => !departingIds.Contains(state.characterId))
            .OrderBy(state => state.characterId, StringComparer.Ordinal)
            .Select(state => JsonUtility.ToJson(state)));
        string otherEquipmentAfter = string.Join("\n", equipment.Instances
            .Where(instance => !departingEquipmentIds.Contains(instance.instanceId))
            .OrderBy(instance => instance.instanceId, StringComparer.Ordinal)
            .Select(instance => JsonUtility.ToJson(instance)));
        Check(loadoutsRetired && ownedEquipmentRetired,
            "ESCAPE_WITHDRAWAL_EQUIPMENT_RETIRED",
            $"characters={departingIds.Count};equipment={departingEquipment.Length};"
            + $"loadoutsRetired={loadoutsRetired};equipmentLost={ownedEquipmentRetired};"
            + "deathEventInjected=false;trainingEligible=false");
        Check(otherLoadoutsBefore == otherLoadoutsAfter
                && otherEquipmentBefore == otherEquipmentAfter,
            "ESCAPE_WITHDRAWAL_OTHER_EQUIPMENT_UNCHANGED",
            "Other character loadouts and equipment must remain byte-exact.");
        string retiredLoadoutsBeforeReplay = JsonUtility.ToJson(equipmentAfter);
        string retiredInstancesBeforeReplay = string.Join("\n", equipment.Instances
            .OrderBy(instance => instance.instanceId, StringComparer.Ordinal)
            .Select(instance => JsonUtility.ToJson(instance)));
        string retiredModulesBeforeReplay = string.Join("\n", equipment.ModuleInstances
            .OrderBy(module => module.instanceId, StringComparer.Ordinal)
            .Select(module => JsonUtility.ToJson(module)));
        foreach (string characterId in departingIds)
            equipment.RetireCharacterEquipment(characterId);
        Check(retiredLoadoutsBeforeReplay == JsonUtility.ToJson(equipment.Capture())
                && retiredInstancesBeforeReplay == string.Join("\n", equipment.Instances
                    .OrderBy(instance => instance.instanceId, StringComparer.Ordinal)
                    .Select(instance => JsonUtility.ToJson(instance)))
                && retiredModulesBeforeReplay == string.Join("\n", equipment.ModuleInstances
                    .OrderBy(module => module.instanceId, StringComparer.Ordinal)
                    .Select(module => JsonUtility.ToJson(module))),
            "ESCAPE_WITHDRAWAL_EQUIPMENT_REPLAY_UNCHANGED",
            "Repeated retirement must change neither equipment, modules nor loadouts.");

        int startedBefore = invasionStartedEventCount;
        int resolvedBefore = invasionResolvedEventCount;
        int candidatesBefore = invasionCandidateEventCount;
        int incidentChangesBefore = activeIncidentsChangedEventCount;
        long revisionBefore = settlementAlerts.GetNextIncidentRevision(
            "incident:invasion:active");
        string incidentBefore = DescribeInvasionIncident();
        gameEvents.Publish(new InvasionResolvedEvent(true, 0f));
        SettlementAlertSnapshot immediate = settlementAlerts.Capture();
        long revisionAfter = settlementAlerts.GetNextIncidentRevision(
            "incident:invasion:active");
        string incidentAfter = DescribeInvasionIncident();
        bool immediateResolution = invasionResolvedEventCount
                == resolvedBefore + 1
            && invasionStartedEventCount == startedBefore
            && invasionCandidateEventCount == candidatesBefore
            && activeIncidentsChangedEventCount > incidentChangesBefore
            && !immediate.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && immediate.DesiredLevel == SettlementThreatAlertLevel.Green;
        Check(immediateResolution,
            "ESCAPE_INVASION_RESOLVE_COMMITTED",
            $"events=start:{startedBefore}->{invasionStartedEventCount},"
            + $"resolved:{resolvedBefore}->{invasionResolvedEventCount},"
            + $"candidate:{candidatesBefore}->{invasionCandidateEventCount},"
            + $"incidentChanged:{incidentChangesBefore}"
            + $"->{activeIncidentsChangedEventCount};"
            + $"revision={revisionBefore}->{revisionAfter};"
            + $"before={incidentBefore};after={incidentAfter};"
            + $"desired={immediate.DesiredLevel};"
            + $"committed={immediate.CommittedLevel};"
            + $"trace=[{string.Join(" | ", invasionEventTrace.TakeLast(12))}]");
        if (!immediateResolution)
        {
            yield break;
        }

        long startedHour = calendar.AbsoluteHour;
        float realtimeDeadline = Time.realtimeSinceStartup + 45f;
        SettlementAlertSnapshot snapshot = settlementAlerts.Capture();
        while (Time.realtimeSinceStartup < realtimeDeadline
               && calendar.AbsoluteHour - startedHour <= 6L)
        {
            snapshot = settlementAlerts.Capture();
            bool invasionResolved = !snapshot.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal);
            bool green = snapshot.DesiredLevel
                    == SettlementThreatAlertLevel.Green
                && snapshot.CommittedLevel
                    == SettlementThreatAlertLevel.Green;
            bool responseReleased = workerWork?.HasEmergencyResponseWorkGateForDiagnostics
                    != true
                && alarmResponses.PendingResponderCountForDiagnostics == 0
                && alarmResponses.ReturningResponderCountForDiagnostics == 0
                && alarmResponses.AssignedResponderCountForDiagnostics == 0;
            if (invasionResolved && green && responseReleased)
            {
                break;
            }
            yield return null;
        }

        snapshot = settlementAlerts.Capture();
        bool clean = !snapshot.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && snapshot.DesiredLevel == SettlementThreatAlertLevel.Green
            && snapshot.CommittedLevel == SettlementThreatAlertLevel.Green
            && workerWork?.HasEmergencyResponseWorkGateForDiagnostics != true
            && alarmResponses.PendingResponderCountForDiagnostics == 0
            && alarmResponses.ReturningResponderCountForDiagnostics == 0
            && alarmResponses.AssignedResponderCountForDiagnostics == 0
            && invasionStartedEventCount == startedBefore
            && invasionResolvedEventCount == resolvedBefore + 1
            && invasionCandidateEventCount == candidatesBefore;
        Check(clean,
            "ESCAPE_INVASION_RESPONSE_RELEASED",
            $"hours={calendar.AbsoluteHour - startedHour};"
            + $"desired={snapshot.DesiredLevel};committed={snapshot.CommittedLevel};"
            + $"incidents=[{string.Join(",", snapshot.ActiveIncidentIds)}];"
            + $"gate={workerWork?.HasEmergencyResponseWorkGateForDiagnostics}:"
            + $"{workerWork?.EmergencyResponseWorkEpochForDiagnostics}:"
            + $"{workerWork?.EmergencyResponseOnlyWorkTypeForDiagnostics};"
            + $"responses={alarmResponses.PendingResponderCountForDiagnostics}/"
            + $"{alarmResponses.ReturningResponderCountForDiagnostics}/"
            + $"{alarmResponses.AssignedResponderCountForDiagnostics};"
            + $"events=start:{startedBefore}->{invasionStartedEventCount},"
            + $"resolved:{resolvedBefore}->{invasionResolvedEventCount},"
            + $"candidate:{candidatesBefore}->{invasionCandidateEventCount};"
            + $"timeScale={Time.timeScale:0.###};"
            + $"incident={DescribeInvasionIncident()};"
            + $"trace=[{string.Join(" | ", invasionEventTrace.TakeLast(12))}]");
        if (clean)
        {
            DungeonGameSaveData savedAfterWithdrawal = gameSaveService.Capture();
            Check(savedAfterWithdrawal != null,
                "ESCAPE_WITHDRAWAL_OFFICIAL_CAPTURE",
                "Official aggregate preflight, not a filtered DTO capture.");
        }
    }

    private bool RecoverCaptiveThroughMedicalAuthority(out string detail)
    {
        detail = string.Empty;
        if (captiveActor == null
            || bodyHealthQuery == null
            || bodyHealthCommands == null
            || medicalCommands == null)
        {
            detail = "medical recovery authority missing";
            return false;
        }

        CharacterBodyHealthSnapshot before = bodyHealthQuery.GetSnapshot(captiveActor);
        CharacterVitalsSnapshot vitalsBefore = bodyHealthQuery.GetVitals(captiveActor);

        // Heal is the production anatomy/vitals mutation boundary.  The
        // explicit medical recovery notification is the production lifecycle
        // completion boundary and verifies the health query before publishing
        // Active; neither operation writes CharacterLifecycle directly.
        bodyHealthCommands.Heal(captiveActor, 10000f, stopBleeding: true);
        medicalCommands.NotifyCharacterRecovered(captiveActor);

        CharacterBodyHealthSnapshot after = bodyHealthQuery.GetSnapshot(captiveActor);
        CharacterVitalsSnapshot vitalsAfter = bodyHealthQuery.GetVitals(captiveActor);
        bool recovered = !after.Downed
            && captiveActor.CurrentLifecycleState == CharacterLifecycleState.Active;
        detail = $"beforeDowned={before.Downed};beforeHealth="
            + $"{vitalsBefore.CurrentHealth:0.##}/{vitalsBefore.MaximumHealth:0.##};"
            + $"afterDowned={after.Downed};afterHealth="
            + $"{vitalsAfter.CurrentHealth:0.##}/{vitalsAfter.MaximumHealth:0.##};"
            + $"lifecycle={captiveActor.CurrentLifecycleState}";
        return recovered;
    }

    private IEnumerator WaitForProductionEscapeRoute(EscapeRouteProbe probe)
    {
        if (probe == null
            || captiveActor == null
            || grid == null
            || pathSearchBroker == null)
        {
            if (probe != null) probe.Detail = "escape route authority missing";
            yield break;
        }

        Vector2Int start = captiveActor.GetNowXY();
        GridTraversalContext context = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(captiveActor),
            DoorAccessOverrideKind.CaptiveEscape,
            GridMovementIntent.EscapeHazard);
        float deadline = Time.realtimeSinceStartup + 5f;
        int deferred = 0;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (!pathSearchBroker.TryGetSearch(
                    grid,
                    start,
                    out GridPathSearchResult search,
                    GridPathSearchPriority.Urgent,
                    context))
            {
                deferred++;
                yield return null;
                continue;
            }

            Vector2Int[] exits = grid.GetCells()
                .Where(cell => cell != null
                    && cell.AreaType == GridCellAreaType.ExteriorPath
                    && cell.IsWalkableArea
                    && grid.IsWalkable(cell.Position)
                    && search.ContainsPosition(cell.Position))
                .Select(cell => cell.Position)
                .ToArray();
            probe.Ready = exits.Length > 0;
            string nearest = exits.Length > 0
                ? exits.OrderBy(position =>
                        Mathf.Abs(position.x - start.x)
                        + Mathf.Abs(position.y - start.y))
                    .First()
                    .ToString()
                : "none";
            probe.Detail = $"start={start};deferred={deferred};"
                + $"reachableExits={exits.Length};"
                + $"nearest={nearest};"
                + $"connector={escapeConnectorCell}:"
                + $"{search.ContainsPosition(escapeConnectorCell)}:"
                + $"{DescribeCell(escapeConnectorCell)};"
                + $"anchor={escapeExteriorAnchorCell}:"
                + $"{search.ContainsPosition(escapeExteriorAnchorCell)}:"
                + $"{DescribeCell(escapeExteriorAnchorCell)};"
                + $"lifecycle={captiveActor.CurrentLifecycleState};"
                + $"gridRevision={grid.TraversalVersion}";
            yield break;
        }

        probe.Detail = $"start={start};deferred={deferred};search-timeout;"
            + $"lifecycle={captiveActor.CurrentLifecycleState};"
            + $"gridRevision={grid.TraversalVersion}";
    }

    private IEnumerator VerifyPickupRetargetContract()
    {
        string[] scenarios = { "MOVING_TARGET_PENDING", "ADJACENT_PUBLICATION", "ARRIVAL_FAILED", "UNREACHABLE", "PENDING_OWNER_LOST" };
        foreach (string scenario in scenarios)
        {
            GameObject probeObject = new("QA Wildlife Pickup Contract");
            AbilityWildlifeCaptureTransport ability =
                probeObject.AddComponent<AbilityWildlifeCaptureTransport>();
            PickupContractPort probe = new(scenario);
            try
            {
                ability.Configure(probe);
                ability.StartTransport("qa-pickup-contract");
                bool startsBeforeEffects = ability.IsTransporting
                    && probe.Owned && probe.CarryCount == 0 && probe.CompleteCount == 0;
                if (scenario == "PENDING_OWNER_LOST")
                {
                    for (int frame = 0; frame < 4 && ability.IsTransporting
                        && ability.PickupSearchDeferralCountForDiagnostics == 0; frame++)
                        yield return null;
                    probe.Owned = false;
                }
                for (int frame = 0; frame < 12 && ability.IsTransporting; frame++)
                    yield return null;
                bool adjacent = scenario == "ADJACENT_PUBLICATION";
                bool success = scenario == "MOVING_TARGET_PENDING" || adjacent;
                Check(!ability.IsTransporting
                    && startsBeforeEffects
                    && probe.EndCount == 1 && probe.ReplanCount == 1
                    && (success
                        ? probe.FailureCount == 0 && probe.CarryCount == 1
                            && probe.CompleteCount == 1 && probe.ApproachCount == (adjacent ? 0 : 2)
                            && probe.MaximumCarryDistance == 1
                            && ability.PickupRetargetCountForDiagnostics == (adjacent ? 0 : 1)
                            && ability.PickupSearchDeferralCountForDiagnostics == (adjacent ? 0 : 1)
                        : probe.FailureCount == 1 && probe.CarryCount == 0
                            && probe.CompleteCount == 0
                            && (scenario != "PENDING_OWNER_LOST"
                                || ability.PickupSearchDeferralCountForDiagnostics > 0)
                            && !string.IsNullOrEmpty(ability.LastTerminalFailureReasonForDiagnostics)),
                    "PICKUP_CONTRACT_" + scenario,
                    $"active={ability.IsTransporting};startBeforeEffects={startsBeforeEffects};approaches={probe.ApproachCount};"
                    + $"pending={ability.PickupSearchDeferralCountForDiagnostics};"
                    + $"retargets={ability.PickupRetargetCountForDiagnostics};"
                    + $"carry={probe.CarryCount};complete={probe.CompleteCount};"
                    + $"failure={probe.FailureCount};end={probe.EndCount};"
                    + $"replan={probe.ReplanCount};distance={probe.MaximumCarryDistance};"
                    + $"reason={ability.LastTerminalFailureReasonForDiagnostics};"
                    + "controlled-port-contract;not-world-gameplay;trainingEligible=false");
            }
            finally
            {
                Destroy(probeObject);
            }
        }
    }

    private sealed class PickupContractPort : IWildlifeCaptureTransportAbilityPort
    {
        private readonly string scenario;
        private Vector2Int target = new(4, 0);
        private bool deferred;
        public PickupContractPort(string scenario)
        {
            this.scenario = scenario;
            if (scenario == "ADJACENT_PUBLICATION") target = Vector2Int.right;
        }
        public Vector2Int Position { get; private set; }
        public bool Owned;
        public int ApproachCount, CarryCount, CompleteCount, FailureCount, EndCount, ReplanCount;
        public int MaximumCarryDistance;
        public bool TryBeginActionOwnership(string id, out string reason)
        { Owned = true; reason = string.Empty; return true; }
        public bool HasActionOwnership() => Owned;
        public void EndActionOwnership(bool clearFailures)
        { if (Owned || FailureCount > EndCount) EndCount++; Owned = false; }
        public void CancelActionOwnership() { Owned = false; }
        public bool TryGetTransportState(string id, out CapturedWildlifeState state,
            out Vector2Int position, out string name, out string reason)
        {
            state = new CapturedWildlifeState { penPosition = Position };
            position = target; name = "QA contract animal"; reason = string.Empty;
            return true;
        }
        public CaptivityMovementResolution ResolvePickupMovement(Vector2Int position,
            out Vector2Int stand, out IEnumerator movement)
        {
            stand = position + Vector2Int.left; movement = null;
            if (scenario == "UNREACHABLE") return CaptivityMovementResolution.Unreachable;
            if (scenario == "PENDING_OWNER_LOST" || !deferred)
            { deferred = true; return CaptivityMovementResolution.Pending; }
            ApproachCount++;
            movement = Approach(stand);
            return CaptivityMovementResolution.Ready;
        }
        private IEnumerator Approach(Vector2Int stand)
        {
            yield return null;
            if (scenario == "ARRIVAL_FAILED") yield break;
            Position = stand;
            if (ApproachCount == 1) target = new Vector2Int(6, 0);
        }
        public bool TryValidateMovementArrival(Vector2Int destination, out string reason)
        { reason = Position == destination ? string.Empty : "qa-planned-stand-not-reached"; return Position == destination; }
        public bool TryBeginCarry(string id, out string reason)
        {
            int distance = Mathf.Abs(Position.x - target.x) + Mathf.Abs(Position.y - target.y);
            MaximumCarryDistance = distance; reason = distance <= 1 ? string.Empty : "qa-too-far";
            if (distance > 1) return false;
            CarryCount++; return true;
        }
        public WildlifeDeliveryStandResolution ResolveDeliveryStand(string id,
            out CapturedWildlifeState state, out string reason)
        {
            state = new CapturedWildlifeState { penPosition = Position };
            reason = string.Empty; return WildlifeDeliveryStandResolution.Ready;
        }
        public bool TryCreateMovement(Vector2Int destination, CaptivityAbilityAccessKind access,
            out IEnumerator movement) { movement = Empty(); return true; }
        public bool TryCreateDeliveryMovement(Vector2Int destination, out IEnumerator movement)
        { movement = Empty(); return true; }
        private static IEnumerator Empty() { yield break; }
        public IDisposable BeginTransportPass(string id) => null;
        public bool TryCompleteCarry(string id, out string reason)
        { CompleteCount++; reason = string.Empty; return true; }
        public void FailCarry(string id, string reason) { FailureCount++; }
        public void SetActionPhase(string phase, string detail) { }
        public void RequestImmediateReplan(bool clearFailures) { ReplanCount++; }
    }

    private IEnumerator VerifyTransportRows()
    {
        worker.SetLifecycleState(CharacterLifecycleState.Active);
        // Freeze autonomous arbitration before cancelling the previous action.
        // Planning the lawful source/pickup/delivery triple yields across path
        // broker frames. Keep the actor quiescent while warming the exact
        // production pickup and delivery searches, then let TryOrderCapture
        // acquire the external lease before any movement is allowed to start.
        worker.SetAiPaused(true);
        worker.transform.position = grid.GetWorldPos(workerCell);
        worker.Brain?.StopCurrentActionForReplan("qa-wildlife-transport");
        AbilityHaul transportHaul = worker.GetComponent<AbilityHaul>();
        AbilityMove preparationMove = worker.GetComponent<AbilityMove>();
        transportHaul?.StopHauling("qa-wildlife-transport");
        preparationMove?.CancelActiveMovement(
            "qa-wildlife-transport-preparation");
        int settledFrames = 0;
        float settleDeadline = Time.realtimeSinceStartup + 3f;
        while (settledFrames < 2
               && Time.realtimeSinceStartup < settleDeadline)
        {
            yield return null;
            bool settled = worker.Brain?.bestAction == null
                && worker.Brain?.IsExternallyDrivenActionActive != true
                && transportHaul?.IsHauling != true
                && workerWork?.isWorking != true
                && workerWork?.HasActiveWorkRoutineForDiagnostics != true
                && preparationMove?.HasActiveMovementRoutineForDiagnostics
                    != true;
            settledFrames = settled ? settledFrames + 1 : 0;
        }
        Check(settledFrames >= 2,
            "TRANSPORT_SUCCESS_ATOMIC_PREPARATION",
            $"settled={settledFrames}/2;paused={worker.IsAiPaused()};"
            + $"action={worker.Brain?.bestAction?.actionset?.GetType().Name};"
            + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
            + $"haul={transportHaul?.IsHauling};"
            + $"work={workerWork?.isWorking}:"
            + $"{workerWork?.HasActiveWorkRoutineForDiagnostics};"
            + $"movement={preparationMove?.HasActiveMovementRoutineForDiagnostics}");
        if (settledFrames < 2)
        {
            yield break;
        }

        TransportFixturePlanProbe transportPlan = new();
        yield return PlanLawfulTransportFixture(transportPlan);
        Check(transportPlan.Ready,
            "TRANSPORT_SUCCESS_LAWFUL_SOURCE_PLAN",
            transportPlan.Detail);
        if (!transportPlan.Ready) yield break;
        WildlifeActor successAnimal = SpawnCaptureAnimal(
            transportPlan.SourceCell);
        Check(successAnimal != null
              && successAnimal.GridPosition == transportPlan.SourceCell,
            "TRANSPORT_SUCCESS_SOURCE",
            successAnimal != null
                ? $"{successAnimal.WildlifeId}:"
                    + $"planned={transportPlan.SourceCell}:"
                    + $"actual={successAnimal.GridPosition}"
                : "missing");
        if (successAnimal == null
            || successAnimal.GridPosition != transportPlan.SourceCell)
        {
            yield break;
        }
        Vector2Int successAnimalSourceCell = successAnimal.GridPosition;
        Transform successAnimalParentBeforeTransport =
            successAnimal.transform.parent;
        // Damage establishes capture eligibility, not immobilization. Wildlife
        // may move through the preflight and actual approach; production must
        // resolve its live location rather than fake a fixed captured target.
        DamageForCapture(successAnimal);
        PickupDeliveryPreflightProbe pickupProbe = new();
        yield return PlaceWorkerAtReachablePickupAndDeliveryStand(
            successAnimal,
            transportPlan,
            pickupProbe);
        bool pickupReady = pickupProbe.Ready;
        Check(pickupReady,
            "TRANSPORT_SUCCESS_PICKUP_TO_DELIVERY_PREFLIGHT",
            pickupProbe.Detail);
        if (!pickupReady) yield break;
        int transportTerminalBefore =
            worker.Brain?.ExternalIntentTerminalCount ?? 0;
        string successWildlifeId = successAnimal.WildlifeId;
        e11TransportWildlifeId = successWildlifeId;
        ObserveE11Transport("prepared-before-order", true, "controlled eligibility/path preparation; no capture terminal injection");
        int captureOrderBefore = CountWildlifeOutcome(
            EnvironmentOutcomeIds.WildlifeActionProducer,
            "capture-order",
            successWildlifeId);
        bool ordered = capture.TryOrderCapture(successAnimal, worker, pen, out string reason);
        Check(ordered, "TRANSPORT_SUCCESS_STARTED", reason);
        Check(CountWildlifeOutcome(
                EnvironmentOutcomeIds.WildlifeActionProducer,
                "capture-order",
                successWildlifeId) == captureOrderBefore + 1,
            "TRANSPORT_CAPTURE_ORDER_PARENT_RESULT_ONCE",
            $"wildlife={successWildlifeId};before={captureOrderBefore};"
            + $"after={CountWildlifeOutcome(EnvironmentOutcomeIds.WildlifeActionProducer, "capture-order", successWildlifeId)}");
        AbilityWildlifeCaptureTransport transport =
            worker.GetComponent<AbilityWildlifeCaptureTransport>();
        capture.TryGetCaptured(
            successAnimal.WildlifeId,
            out CapturedWildlifeState ownershipState);
        string expectedTransportOwner =
            $"captivity:wildlife-transport:{successAnimal.WildlifeId}";
        bool atomicPickupOwnership = ordered
            && worker.IsAiPaused()
            && worker.GetNowXY() == pickupProbe.ApproachStart
            && Mathf.Abs(worker.GetNowXY().x - successAnimal.GridPosition.x)
                + Mathf.Abs(worker.GetNowXY().y - successAnimal.GridPosition.y)
                > 1
            && worker.Brain?.bestAction == null
            && worker.Brain?.IsExternallyDrivenActionActive == true
            && worker.Brain?.ExternalIntentOwnerId == expectedTransportOwner
            && transport?.IsTransporting == true
            && ownershipState?.transportState
                == CapturedWildlifeTransportState.AwaitingTransport
            && ownershipState.reservedCarrierId
                == CharacterPersistentIdentity.Require(worker).Value;
        Check(atomicPickupOwnership,
            "TRANSPORT_SUCCESS_ATOMIC_PICKUP_OWNERSHIP",
            $"ordered={ordered};reason={reason};paused={worker.IsAiPaused()};"
            + $"carrier={worker.GetNowXY()};approach={pickupProbe.ApproachStart};"
            + $"pickup={pickupProbe.PickupStand};animal={successAnimal.GridPosition};"
            + $"action={worker.Brain?.bestAction?.actionset?.GetType().Name};"
            + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
            + $"owner={worker.Brain?.ExternalIntentOwnerId};"
            + $"expectedOwner={expectedTransportOwner};"
            + $"transport={transport?.IsTransporting};"
            + $"state={ownershipState?.transportState};"
            + $"reservedCarrier={ownershipState?.reservedCarrierId};"
            + $"terminalFailure={transport?.LastTerminalFailureReasonForDiagnostics};"
            + $"movement={preparationMove?.HasActiveMovementRoutineForDiagnostics};"
            + $"movementOwner={preparationMove?.ActiveMovementOperationOwnerForDiagnostics};"
            + "preemption="
            + $"{preparationMove?.LastMovementOperationPreemptionForDiagnostics}");
        if (!atomicPickupOwnership)
        {
            yield break;
        }
        TransportTerminalObservation transportObservation = new();
        yield return ObserveTransportTerminal(
            successAnimal.WildlifeId,
            pickupProbe,
            transportObservation);
        capture.TryGetCaptured(successAnimal.WildlifeId, out CapturedWildlifeState penned);
        int carryStartedCount = CountWildlifeOutcome(
            EnvironmentOutcomeIds.WildlifeActionProducer,
            "capture-carry-started",
            successWildlifeId);
        int pennedCount = CountWildlifeOutcome(
            EnvironmentOutcomeIds.WildlifeActionProducer,
            "capture-penned",
            successWildlifeId);
        Check(carryStartedCount == 1 && pennedCount == 1,
            "TRANSPORT_CARRIER_PEN_PARENT_RESULTS_ONCE",
            $"wildlife={successWildlifeId};carry={carryStartedCount};penned={pennedCount};"
            + $"pending={circusPersistence.Capture().wildlifeGameplayOutcomes.Count}");
        rows.Add("INFO\tTRANSPORT_LIVE_PICKUP_PURSUIT\t"
            + $"source={successAnimalSourceCell};live={successAnimal.GridPosition};"
            + $"retargets={transport?.PickupRetargetCountForDiagnostics};"
            + $"pending={transport?.PickupSearchDeferralCountForDiagnostics};"
            + $"terminalFailure={transport?.LastTerminalFailureReasonForDiagnostics};"
            + "actual-production-approach;no-immobilization-or-capture-terminal-injection");
        AbilityMove transportMove = worker.GetComponent<AbilityMove>();
        Vector2Int deliveryPosition = penned?.penPosition ?? default;
        GridCell deliveryCell = penned != null
            ? grid.GetGridCell(deliveryPosition)
            : null;
        IGridOccupant deliveryBuilding =
            deliveryCell?.GetOccupant(GridLayer.Building);
        IGridOccupant deliveryConstruction =
            deliveryCell?.GetOccupant(GridLayer.Construction);
        IGridOccupant deliveryConveyor =
            deliveryCell?.GetOccupant(GridLayer.Conveyor);
        IGridOccupant deliveryCharacter =
            deliveryCell?.GetOccupant(GridLayer.Character);
        IGridOccupant deliveryDownedCharacter =
            deliveryCell?.GetOccupant(GridLayer.DownedCharacter);
        IGridOccupant deliveryWildlife =
            deliveryCell?.GetOccupant(GridLayer.Wildlife);
        bool deliveryCellValid = deliveryCell != null
            && grid.IsWalkable(deliveryPosition)
            && !(pen.buildPoses ?? Array.Empty<Vector2Int>())
                .Contains(deliveryPosition);
        bool deliveryBlockingLayersClear = deliveryBuilding == null
            && deliveryConstruction == null
            && deliveryConveyor == null
            && deliveryCharacter == null
            && deliveryDownedCharacter == null;
        Vector2Int[] successAnimalGridRegistrations =
            FindGridOccupantRegistrations(
                grid,
                GridLayer.Wildlife,
                successAnimal);
        IGridOccupant[] successAnimalGridIndexMatches = grid
            .FindAllOccupants(value => ReferenceEquals(value, successAnimal))
            .ToArray();
        bool successAnimalGridIndexExact =
            successAnimalGridIndexMatches.Length == 1
            && ReferenceEquals(
                successAnimalGridIndexMatches[0],
                successAnimal);
        bool deliveryWildlifeExact = ReferenceEquals(
                deliveryWildlife,
                successAnimal)
            && successAnimalGridRegistrations.Length == 1
            && successAnimalGridRegistrations[0] == deliveryPosition
            && successAnimalGridIndexExact
            && successAnimal.GridPosition == deliveryPosition
            && successAnimal.State == WildlifeState.Captured
            && successAnimal.IsAlive;
        IGridOccupant sourceWildlife = grid.GetGridCell(successAnimalSourceCell)
            ?.GetOccupant(GridLayer.Wildlife);
        bool sourceResidueClear = successAnimalSourceCell == deliveryPosition
            || !ReferenceEquals(sourceWildlife, successAnimal);
        bool parentReleased = ReferenceEquals(
                successAnimal.transform.parent,
                successAnimalParentBeforeTransport)
            && !successAnimal.transform.IsChildOf(worker.transform);
        CapturedWildlifeState[] sameCapturedStates = capture.CapturedAnimals
            .Where(value => value != null
                && string.Equals(
                    value.wildlifeId,
                    successAnimal.WildlifeId,
                    StringComparison.Ordinal))
            .ToArray();
        bool capturedStateExact = penned != null
            && sameCapturedStates.Length == 1
            && string.Equals(
                penned.wildlifeId,
                successAnimal.WildlifeId,
                StringComparison.Ordinal)
            && string.Equals(
                sameCapturedStates[0].wildlifeId,
                successAnimal.WildlifeId,
                StringComparison.Ordinal)
            && sameCapturedStates[0].penPosition == deliveryPosition
            && sameCapturedStates[0].transportState
                == CapturedWildlifeTransportState.Penned
            && string.IsNullOrWhiteSpace(
                sameCapturedStates[0].reservedCarrierId)
            && penned.transportState == CapturedWildlifeTransportState.Penned
            && string.IsNullOrWhiteSpace(penned.reservedCarrierId);
        WildlifeActor[] sameWorldWildlife = world.Wildlife
            .Where(value => value != null
                && string.Equals(
                    value.WildlifeId,
                    successAnimal.WildlifeId,
                    StringComparison.Ordinal))
            .ToArray();
        bool worldWildlifeExact = sameWorldWildlife.Length == 1
            && ReferenceEquals(sameWorldWildlife[0], successAnimal);
        CharacterActor[] liveActorsAtDelivery = world.Characters
            .Where(value => value != null
                && value.gameObject.activeInHierarchy
                && !value.IsDead
                && value.GetNowXY() == deliveryPosition)
            .ToArray();
        int deliveryWorkerCount = liveActorsAtDelivery.Count(value =>
            ReferenceEquals(value, worker));
        int deliveryOtherActorCount = liveActorsAtDelivery.Length
            - deliveryWorkerCount;
        bool liveActorColocationExact = deliveryWorkerCount == 1
            && deliveryOtherActorCount == 0;
        bool deliveryStandValid = deliveryCellValid
            && deliveryBlockingLayersClear
            && deliveryWildlifeExact
            && sourceResidueClear
            && parentReleased
            && capturedStateExact
            && worldWildlifeExact
            && liveActorColocationExact;
        string deliveryLayerDetail = DescribeGridCellLayers(deliveryCell);
        string deliveryRegistrationDetail =
            successAnimalGridRegistrations.Length == 0
                ? "none"
                : string.Join(",", successAnimalGridRegistrations);
        string deliveryLiveActorDetail =
            liveActorsAtDelivery.Length == 0
                ? "none"
                : string.Join("|", liveActorsAtDelivery.Select(DescribeLiveActor));
        Check(
            deliveryCellValid,
            "TRANSPORT_SUCCESS_DELIVERY_CELL",
            $"exists={deliveryCell != null};position={deliveryPosition};"
            + $"area={deliveryCell?.AreaType};terrain={deliveryCell?.TerrainType};"
            + $"walkable={(deliveryCell != null && grid.IsWalkable(deliveryPosition))};"
            + "insidePenFootprint="
            + $"{(pen.buildPoses ?? Array.Empty<Vector2Int>()).Contains(deliveryPosition)};"
            + $"layers={deliveryLayerDetail}");
        Check(
            deliveryBlockingLayersClear,
            "TRANSPORT_SUCCESS_DELIVERY_BLOCKING_LAYERS",
            $"building={DescribeGridOccupant(deliveryBuilding)};"
            + $"construction={DescribeGridOccupant(deliveryConstruction)};"
            + $"conveyor={DescribeGridOccupant(deliveryConveyor)};"
            + $"character={DescribeGridOccupant(deliveryCharacter)};"
            + $"downed={DescribeGridOccupant(deliveryDownedCharacter)}");
        Check(
            deliveryWildlifeExact && worldWildlifeExact,
            "TRANSPORT_SUCCESS_DELIVERY_WILDLIFE_REGISTRATION",
            $"cell={DescribeGridOccupant(deliveryWildlife)};"
            + $"expected={DescribeGridOccupant(successAnimal)};"
            + $"registrations={successAnimalGridRegistrations.Length}:"
            + $"{deliveryRegistrationDetail};"
            + $"gridIndexMatches={successAnimalGridIndexMatches.Length};"
            + $"gridIndexExact={successAnimalGridIndexExact};"
            + $"gridPosition={successAnimal.GridPosition};"
            + $"state={successAnimal.State};alive={successAnimal.IsAlive};"
            + $"worldMatches={sameWorldWildlife.Length};"
            + $"worldExact={worldWildlifeExact}");
        Check(
            sourceResidueClear,
            "TRANSPORT_SUCCESS_SOURCE_RELEASE",
            $"source={successAnimalSourceCell};delivery={deliveryPosition};"
            + $"occupant={DescribeGridOccupant(sourceWildlife)};"
            + $"sameReference={ReferenceEquals(sourceWildlife, successAnimal)}");
        Check(
            parentReleased,
            "TRANSPORT_SUCCESS_PARENT_RELEASE",
            $"before={DescribeTransform(successAnimalParentBeforeTransport)};"
            + $"after={DescribeTransform(successAnimal.transform.parent)};"
            + $"carrier={DescribeTransform(worker.transform)};"
            + $"childOfCarrier={successAnimal.transform.IsChildOf(worker.transform)}");
        Check(
            capturedStateExact,
            "TRANSPORT_SUCCESS_CAPTURE_STATE",
            $"matches={sameCapturedStates.Length};state={penned?.transportState};"
            + $"reservedCarrier={penned?.reservedCarrierId};"
            + $"wildlifeId={penned?.wildlifeId};"
            + $"expectedId={successAnimal.WildlifeId}");
        Check(
            liveActorColocationExact,
            "TRANSPORT_SUCCESS_LIVE_ACTOR_COLOCATION",
            $"position={deliveryPosition};count={liveActorsAtDelivery.Length};"
            + $"workerCount={deliveryWorkerCount};"
            + $"otherCount={deliveryOtherActorCount};"
            + $"actors={deliveryLiveActorDetail};"
            + $"worker={DescribeLiveActor(worker)}");
        Check(penned?.transportState == CapturedWildlifeTransportState.Penned
              && deliveryStandValid
              && transportMove?.LastGridMoveFailureReason
                  == GridMoveFailureReason.None
              && transportObservation.AwaitingTransportObserved
              && transportObservation.PickupApproachProgressObserved
              && transportObservation.PickupStandReached
              && transportObservation.TransportingObserved
              && !transportObservation.Stalled
              && transport?.IsTransporting != true
              && worker.Brain?.IsExternallyDrivenActionActive != true
              && worker.Brain?.ExternalIntentTerminalCount
                  == transportTerminalBefore + 1
              && worker.Brain?.LastExternalIntentTerminalKind
                  == CharacterAiActionTerminalKind.Completed,
            "TRANSPORT_SUCCESS_TERMINAL",
            $"state={penned?.transportState};active={transport?.IsTransporting};"
            + $"delivery={penned?.penPosition};stand={deliveryStandValid};"
            + $"cell={deliveryCellValid};blocking={deliveryBlockingLayersClear};"
            + $"wildlife={deliveryWildlifeExact};source={sourceResidueClear};"
            + $"parent={parentReleased};capture={capturedStateExact};"
            + $"worldWildlife={worldWildlifeExact};"
            + $"liveActors={liveActorColocationExact};"
            + $"carrier={worker.GetNowXY()};"
            + $"world={worker.transform.position};"
            + $"movement={transportMove?.LastGridMoveFailureReason};"
            + $"systemMove={transportMove?.IsSystemMoveInProgress};"
            + "systemDestination="
            + $"{transportMove?.ActiveSystemMoveDestinationForDiagnostics};"
            + "systemDestinationMatches="
            + $"{transportMove?.IsSystemMoveInProgressTo(pickupProbe.DeliveryStand)};"
            + "movementOwner="
            + $"{transportMove?.ActiveMovementOperationOwnerForDiagnostics};"
            + "movementVersion="
            + $"{transportMove?.MovementOperationVersionForDiagnostics};"
            + $"observation={transportObservation.ObservationSeconds:0.###}/"
            + $"{transportObservation.AllowedSeconds:0.###};"
            + $"stalled={transportObservation.Stalled};"
            + "secondsSinceProgress="
            + $"{transportObservation.SecondsSinceProgress:0.###};"
            + $"progressSamples={transportObservation.ProgressSamples};"
            + "pickupApproach="
            + $"{transportObservation.AwaitingTransportObserved}:"
            + $"{transportObservation.PickupApproachProgressObserved}:"
            + $"{transportObservation.PickupStandReached}:"
            + $"{transportObservation.TransportingObserved};"
            + "lastProgress="
            + $"{transportObservation.LastProgressGridPosition}:"
            + $"{transportObservation.LastProgressWorldPosition};"
            + "cancellationSource="
            + $"{transportMove?.LastMovementCancellationSourceForDiagnostics};"
            + "operationPreemption="
            + $"{transportMove?.LastMovementOperationPreemptionForDiagnostics};"
            + "rejectedOperation="
            + $"{transportMove?.LastRejectedMovementOperationOwnerForDiagnostics};"
            + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
            + $"externalOwner={worker.Brain?.ExternalIntentOwnerId};"
            + $"externalKind={worker.Brain?.ExternalIntentKind};"
            + $"externalEpoch={worker.Brain?.ExternalIntentEpoch};"
            + "deliverySearchDeferrals="
            + $"{transport?.DeliveryStandSearchDeferralCountForDiagnostics};"
            + $"terminal={transportTerminalBefore}"
            + $"->{worker.Brain?.ExternalIntentTerminalCount}:"
            + $"{worker.Brain?.LastExternalIntentTerminalKind};"
            + $"failure={transport?.LastTerminalFailureReasonForDiagnostics}");
        if (!transportOnly && failures.Count == 0)
        {
            yield return VerifyCapturedAnimalTamingCompletion(successWildlifeId);
            if (failures.Count != 0) yield break;
            yield return VerifyCapturedAnimalSlaughterCompletion(successWildlifeId);
            if (failures.Count != 0) yield break;
            successAnimal = wildlife.Wildlife.SingleOrDefault(value =>
                value != null && value.WildlifeId == successWildlifeId);
            Check(successAnimal != null,
                "TAME_RESTORED_CAPTURE_ACTOR_FOR_RELEASE", successWildlifeId);
            if (successAnimal == null) yield break;
        }
        int releaseBefore = CountWildlifeOutcome(
            EnvironmentOutcomeIds.WildlifeCommandProducer,
            "capture-release",
            successWildlifeId);
        bool released = capture.TryRelease(
            successAnimal.WildlifeId,
            out string releaseReason);
        int releaseAfter = CountWildlifeOutcome(
            EnvironmentOutcomeIds.WildlifeCommandProducer,
            "capture-release",
            successWildlifeId);
        CircusSaveData afterReleaseSave = circusPersistence.Capture();
        Check(released
              && releaseAfter == releaseBefore + 1
              && afterReleaseSave.wildlifeGameplayOutcomes.Count == 0,
            "TRANSPORT_RELEASE_PARENT_RESULT_ONCE",
            $"released={released};reason={releaseReason};before={releaseBefore};"
            + $"after={releaseAfter};pending={afterReleaseSave.wildlifeGameplayOutcomes.Count}");
        Destroy(successAnimal.gameObject);
        yield return null;

        // The E11 capture-outcome witness is intentionally bounded to the
        // complete successful transport lifecycle. The full verifier keeps
        // the independent source-loss, path-loss, lifecycle and alert rows.
        // Running those after four committed parent outcomes can exercise the
        // global ledger page-capacity policy instead of the capture contract
        // this focused entry point is meant to prove.
        if (transportOnly)
        {
            yield break;
        }

        WildlifeActor lostAnimal = SpawnCaptureAnimal(FindFarWalkable(worker.GetNowXY()));
        Check(lostAnimal != null, "TRANSPORT_SOURCE_LOSS_SOURCE", lostAnimal?.WildlifeId ?? "missing");
        if (lostAnimal != null)
        {
            DamageForCapture(lostAnimal);
            ordered = capture.TryOrderCapture(lostAnimal, worker, pen, out reason);
            string lostId = lostAnimal.WildlifeId;
            if (ordered) Destroy(lostAnimal.gameObject);
            yield return null;
            yield return null;
            yield return WaitUntil(() =>
                transport?.IsTransporting != true
                && worker.Brain?.IsExternallyDrivenActionActive != true,
                8f);
            Check(ordered && !capture.TryGetCaptured(lostId, out _)
                  && transport?.IsTransporting != true,
                "TRANSPORT_SOURCE_LOSS_TERMINAL",
                $"ordered={ordered};active={transport?.IsTransporting}");
        }

        WildlifeActor blockedAnimal = SpawnCaptureAnimal(FindFarWalkable(worker.GetNowXY()));
        Check(blockedAnimal != null, "TRANSPORT_NOPATH_SOURCE", blockedAnimal?.WildlifeId ?? "missing");
        if (blockedAnimal != null)
        {
            DamageForCapture(blockedAnimal);
            Vector2Int isolatedFaultCell = grid.GetCells()
                .Where(cell => cell != null
                    && cell.AreaType == GridCellAreaType.ExteriorPath
                    && grid.IsWalkable(cell.Position))
                .OrderByDescending(cell => Mathf.Abs(
                        cell.Position.x - blockedAnimal.GridPosition.x)
                    + Mathf.Abs(
                        cell.Position.y - blockedAnimal.GridPosition.y))
                .Select(cell => cell.Position)
                .FirstOrDefault();
            worker.transform.position = grid.GetWorldPos(isolatedFaultCell);
            RegisterFaultRing(isolatedFaultCell);
            int failedTerminalBefore =
                worker.Brain?.ExternalIntentTerminalCount ?? 0;
            ordered = capture.TryOrderCapture(blockedAnimal, worker, pen, out reason);
            yield return null;
            RemoveFaultWalls();
            yield return WaitUntil(() =>
                transport?.IsTransporting != true
                && worker.Brain?.IsExternallyDrivenActionActive != true,
                8f);
            Check(ordered && !capture.TryGetCaptured(blockedAnimal.WildlifeId, out _)
                  && transport?.IsTransporting != true
                  && worker.Brain?.ExternalIntentTerminalCount
                      == failedTerminalBefore + 1
                  && worker.Brain?.LastExternalIntentTerminalKind
                      == CharacterAiActionTerminalKind.Failed,
                "TRANSPORT_NOPATH_TERMINAL",
                $"ordered={ordered};reason={reason};active={transport?.IsTransporting};"
                + $"terminal={failedTerminalBefore}"
                + $"->{worker.Brain?.ExternalIntentTerminalCount}:"
                + $"{worker.Brain?.LastExternalIntentTerminalKind}");
            worker.transform.position = grid.GetWorldPos(workerCell);
            Destroy(blockedAnimal.gameObject);
            yield return null;
        }

        TransportChaosStartProbe downedStart = new();
        yield return StartTransportChaos("TRANSPORT_DOWNED", downedStart);
        WildlifeActor downedAnimal = downedStart.Animal;
        Check(downedStart.Live && downedAnimal != null,
            "TRANSPORT_DOWNED_SOURCE",
            $"live={downedStart.Live};animal={downedAnimal?.WildlifeId ?? "missing"}");
        if (downedStart.Live && downedAnimal != null)
        {
            CharacterBodyHealthSnapshot originalTransportBody =
                CloneBodyHealthSnapshot(bodyHealthQuery.GetSnapshot(worker));
            bool downedFixtureReady = TryCreateLawfulDownedSnapshot(
                originalTransportBody,
                out CharacterBodyHealthSnapshot downedTransportBody,
                out string downedTransportDetail);
            int downedTerminalBefore = downedStart.TerminalBefore;
            int downedReleaseAttemptsBefore =
                worker.TransientAiOwnershipReleaseAttemptCountForDiagnostics;
            bool downedOwnershipReady =
                !worker.TransientAiOwnershipReleasedForDiagnostics;
            if (downedFixtureReady)
            {
                bodyHealthCommands.ApplySnapshot(
                    worker,
                    downedTransportBody,
                    "qa-wildlife-transport-downed");
            }
            yield return null;
            yield return null;
            CharacterBodyHealthSnapshot downedTransportObserved =
                bodyHealthQuery.GetSnapshot(worker);
            Check(downedFixtureReady
                  && downedOwnershipReady
                  && downedTransportObserved.Downed
                  && worker.CurrentLifecycleState
                      == CharacterLifecycleState.Downed
                  && !capture.TryGetCaptured(downedAnimal.WildlifeId, out _)
                  && downedStart.Transport?.IsTransporting != true
                  && worker.Brain?.IsExternallyDrivenActionActive != true
                  && worker.Brain?.ExternalIntentTerminalCount
                      == downedTerminalBefore + 1
                  && worker.Brain?.LastExternalIntentTerminalKind
                      == CharacterAiActionTerminalKind.Cancelled,
                "TRANSPORT_DOWNED_TERMINAL",
                $"live={downedStart.Live};fixture={downedFixtureReady}:"
                + $"{downedTransportDetail};"
                + $"body={downedTransportObserved.Downed}/"
                + $"{downedTransportObserved.Mobility:0.###};"
                + $"lifecycle={worker.CurrentLifecycleState};"
                + $"active={downedStart.Transport?.IsTransporting};"
                + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
                + $"externalOwner={worker.Brain?.ExternalIntentOwnerId};"
                + $"externalKind={worker.Brain?.ExternalIntentKind};"
                + $"externalEpoch={worker.Brain?.ExternalIntentEpoch};"
                + "ownership="
                + $"{downedOwnershipReady}:"
                + $"{downedReleaseAttemptsBefore}"
                + $"->{worker.TransientAiOwnershipReleaseAttemptCountForDiagnostics}:"
                + $"{worker.TransientAiOwnershipReleasedForDiagnostics}:"
                + $"{worker.LastTransientAiOwnershipReleaseReasonForDiagnostics};"
                + "failure="
                + $"{downedStart.Transport?.LastTerminalFailureReasonForDiagnostics};"
                + $"terminal={downedTerminalBefore}"
                + $"->{worker.Brain?.ExternalIntentTerminalCount}:"
                + $"{worker.Brain?.LastExternalIntentTerminalKind}");
            worker.SetAiPaused(true);
            bodyHealthCommands.ApplySnapshot(
                worker,
                originalTransportBody,
                "qa-wildlife-transport-downed-recovered");
            yield return null;
            yield return null;
            Destroy(downedAnimal.gameObject);
            yield return null;
        }

        WildlifeActor penLossAnimal = SpawnCaptureAnimal(worker.GetNowXY());
        Check(penLossAnimal != null, "TRANSPORT_PEN_LOSS_SOURCE", penLossAnimal?.WildlifeId ?? "missing");
        if (penLossAnimal != null)
        {
            DamageForCapture(penLossAnimal);
            string penLossId = penLossAnimal.WildlifeId;
            int penLossOrderBefore = CountWildlifeOutcome(
                EnvironmentOutcomeIds.WildlifeActionProducer, "capture-order", penLossId);
            ordered = capture.TryOrderCapture(penLossAnimal, worker, faultPen, out reason);
            Check(ordered
                && CountWildlifeOutcome(EnvironmentOutcomeIds.WildlifeActionProducer,
                    "capture-order", penLossId) == penLossOrderBefore + 1
                && CountWildlifeOutcome(EnvironmentOutcomeIds.WildlifeActionProducer,
                    "capture-carry-started", penLossId) == 0,
                "TRANSPORT_ADJACENT_ORDER_PUBLISHED_BEFORE_CARRY",
                $"ordered={ordered};reason={reason};wildlife={penLossId};"
                + $"order={CountWildlifeOutcome(EnvironmentOutcomeIds.WildlifeActionProducer, "capture-order", penLossId)};"
                + $"carry={CountWildlifeOutcome(EnvironmentOutcomeIds.WildlifeActionProducer, "capture-carry-started", penLossId)};"
                + "real-command-return-before-async-carry;no-owner-revision-rewrite");
            if (ordered) faultPen.DestroySelf();
            yield return null;
            yield return null;
            Check(ordered && !capture.TryGetCaptured(penLossAnimal.WildlifeId, out _)
                  && transport?.IsTransporting != true,
                "TRANSPORT_PEN_DESTROY_TERMINAL",
                $"ordered={ordered};active={transport?.IsTransporting}");
            Destroy(penLossAnimal.gameObject);
            yield return null;
        }

        if (failures.Count == 0)
        {
            yield return VerifyTransportAlertCarrierDownedChaos();
        }
        if (failures.Count == 0)
        {
            yield return VerifyTransportDestinationContenderChaos();
        }
    }

    private IEnumerator VerifyTransportAlertCarrierDownedChaos()
    {
        TransportChaosStartProbe start = new();
        yield return StartTransportChaos(
            "TRANSPORT_ALERT",
            start);
        if (!start.Live)
        {
            yield break;
        }

        List<ActorAiPauseState> isolatedActors = PauseAllActorsExceptWorker();
        CharacterBodyHealthSnapshot originalBody = CloneBodyHealthSnapshot(
            bodyHealthQuery.GetSnapshot(worker));
        if (!TryCreateLawfulDownedSnapshot(
                originalBody,
                out CharacterBodyHealthSnapshot downedBody,
                out string downedFixtureDetail))
        {
            Check(false,
                "TRANSPORT_ALERT_CARRIER_DOWNED_BODY_FIXTURE",
                downedFixtureDetail);
            RestoreActorAiPauseStates(isolatedActors);
            yield break;
        }

        worker.SetAiPaused(false);
        int startedBefore = invasionStartedEventCount;
        int resolvedBefore = invasionResolvedEventCount;
        int candidateBefore = invasionCandidateEventCount;
        gameEvents.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot red = settlementAlerts.Capture();
        capture.TryGetCaptured(
            start.Animal.WildlifeId,
            out CapturedWildlifeState redState);
        bool redBoundWhileTransportLive =
            invasionStartedEventCount == startedBefore + 1
            && invasionResolvedEventCount == resolvedBefore
            && invasionCandidateEventCount == candidateBefore
            && red.DesiredLevel == SettlementThreatAlertLevel.Red
            && red.CommittedLevel == SettlementThreatAlertLevel.Red
            && red.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && workerWork.HasEmergencyResponseWorkGateForDiagnostics
            && workerWork.EmergencyResponseWorkEpochForDiagnostics
                == red.AlertEpochId
            && workerWork.EmergencyResponseOnlyWorkTypeForDiagnostics
                == BuiltInWorkTypeIds.Guard
            && start.Transport.IsTransporting
            && worker.Brain.IsExternallyDrivenActionActive
            && string.Equals(
                worker.Brain.ExternalIntentOwnerId,
                start.ExpectedOwner,
                StringComparison.Ordinal)
            && redState?.transportState
                == CapturedWildlifeTransportState.Transporting;
        Check(redBoundWhileTransportLive,
            "TRANSPORT_ALERT_EXTERNAL_LEASE_RED_BOUND",
            $"alert={red.DesiredLevel}/{red.CommittedLevel}/{red.AlertEpochId};"
            + $"events={startedBefore}->{invasionStartedEventCount}:"
            + $"{resolvedBefore}->{invasionResolvedEventCount}:"
            + $"{candidateBefore}->{invasionCandidateEventCount};"
            + $"gate={workerWork.HasEmergencyResponseWorkGateForDiagnostics}:"
            + $"{workerWork.EmergencyResponseWorkEpochForDiagnostics}:"
            + $"{workerWork.EmergencyResponseOnlyWorkTypeForDiagnostics};"
            + $"transport={start.Transport.IsTransporting};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive}:"
            + $"{worker.Brain.ExternalIntentOwnerId};"
            + $"state={redState?.transportState};"
            + $"phase={worker.Brain.CurrentActionPhase};"
            + $"movement={start.Move.IsSystemMoveInProgress}:"
            + $"{start.Move.ActiveSystemMoveDestinationForDiagnostics}");

        bool noGuardOverlap = redBoundWhileTransportLive
            && !workerWork.isWorking
            && !workerWork.HasActiveWorkRoutineForDiagnostics
            && !worker.Brain.HasRunningWorkAction
            && worker.Brain.bestAction == null;
        Check(noGuardOverlap,
            "TRANSPORT_ALERT_NO_GUARD_OVERLAP",
            $"work={workerWork.isWorking}/"
            + $"{workerWork.HasActiveWorkRoutineForDiagnostics};"
            + $"brainWork={worker.Brain.HasRunningWorkAction};"
            + $"action={worker.Brain.bestAction?.actionset?.GetType().Name};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"gate={workerWork.HasEmergencyResponseWorkGateForDiagnostics}");
        if (!redBoundWhileTransportLive || !noGuardOverlap)
        {
            gameEvents.Publish(new InvasionResolvedEvent(true, 0f));
            RestoreActorAiPauseStates(isolatedActors);
            yield break;
        }

        int terminalBefore = worker.Brain.ExternalIntentTerminalCount;
        bodyHealthCommands.ApplySnapshot(
            worker,
            downedBody,
            "qa-wildlife-transport-alert-carrier-downed");
        CharacterBodyHealthSnapshot immediateBody = bodyHealthQuery
            .GetSnapshot(worker);
        bool stateRemoved = !capture.TryGetCaptured(
            start.Animal.WildlifeId,
            out CapturedWildlifeState afterDownedState);
        Vector2Int[] immediateRegistrations = FindGridOccupantRegistrations(
            grid,
            GridLayer.Wildlife,
            start.Animal);
        bool synchronousTerminal = immediateBody.Downed
            && worker.CurrentLifecycleState == CharacterLifecycleState.Downed
            && !worker.CanRunAi
            && !start.Transport.IsTransporting
            && !worker.Brain.IsExternallyDrivenActionActive
            && stateRemoved
            && worker.Brain.ExternalIntentTerminalCount == terminalBefore + 1
            && worker.Brain.LastExternalIntentTerminalKind
                == CharacterAiActionTerminalKind.Cancelled;
        Check(synchronousTerminal,
            "TRANSPORT_ALERT_CARRIER_DOWNED_EXACT_TERMINAL",
            $"body={immediateBody.Downed}/{immediateBody.Mobility:0.###};"
            + $"lifecycle={worker.CurrentLifecycleState};"
            + $"canRun={worker.CanRunAi};"
            + $"transport={start.Transport.IsTransporting};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"captureRemoved={stateRemoved};"
            + $"state={afterDownedState?.transportState};"
            + $"terminal={terminalBefore}"
            + $"->{worker.Brain.ExternalIntentTerminalCount}:"
            + $"{worker.Brain.LastExternalIntentTerminalKind};"
            + $"failure={start.Transport.LastTerminalFailureReasonForDiagnostics};"
            + $"registrations={string.Join(",", immediateRegistrations)}");

        bool physicalRollback = immediateRegistrations.Length == 1
            && ReferenceEquals(
                grid.GetGridCell(immediateRegistrations[0])
                    ?.GetOccupant(GridLayer.Wildlife),
                start.Animal)
            && ReferenceEquals(start.Animal.transform.parent, start.OriginalParent)
            && !start.Animal.transform.IsChildOf(worker.transform)
            && start.Animal.State != WildlifeState.Captured
            && start.Animal.IsAlive;
        Check(physicalRollback,
            "TRANSPORT_ALERT_ROLLBACK_PHYSICAL_CONVERGED",
            $"registrations={immediateRegistrations.Length}:"
            + $"{string.Join(",", immediateRegistrations)};"
            + $"gridPosition={start.Animal.GridPosition};"
            + $"state={start.Animal.State};"
            + $"parent={DescribeTransform(start.Animal.transform.parent)};"
            + $"expectedParent={DescribeTransform(start.OriginalParent)};"
            + $"childOfCarrier={start.Animal.transform.IsChildOf(worker.transform)};"
            + $"alive={start.Animal.IsAlive}");

        yield return null;
        yield return null;
        bool gateRetired = !workerWork.HasEmergencyResponseWorkGateForDiagnostics
            && !worker.Brain.IsExternallyDrivenActionActive
            && !start.Transport.IsTransporting;
        Check(gateRetired,
            "TRANSPORT_ALERT_DOWNED_GATE_RETIRED",
            $"gate={workerWork.HasEmergencyResponseWorkGateForDiagnostics}:"
            + $"{workerWork.EmergencyResponseWorkEpochForDiagnostics};"
            + $"responses={alarmResponses.PendingResponderCountForDiagnostics}/"
            + $"{alarmResponses.ReturningResponderCountForDiagnostics}/"
            + $"{alarmResponses.AssignedResponderCountForDiagnostics};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"transport={start.Transport.IsTransporting}");

        worker.SetAiPaused(true);
        bodyHealthCommands.ApplySnapshot(
            worker,
            originalBody,
            "qa-wildlife-transport-alert-carrier-recovered");
        yield return null;
        yield return null;

        yield return ResolveTransportChaosInvasionAndWaitForGreen(
            "TRANSPORT_ALERT");

        CharacterBodyHealthSnapshot recovered = bodyHealthQuery
            .GetSnapshot(worker);
        bool recoveredClean = !recovered.Downed
            && worker.CurrentLifecycleState == CharacterLifecycleState.Active
            && !medicalQuery.ActiveOrders.Any(order => order != null
                && order.IsActive
                && string.Equals(
                    order.patientId,
                    worker.Identity?.PersistentId,
                    StringComparison.Ordinal))
            && !workerWork.HasEmergencyResponseWorkGateForDiagnostics
            && !worker.Brain.IsExternallyDrivenActionActive
            && !start.Transport.IsTransporting;
        Check(recoveredClean,
            "TRANSPORT_ALERT_RESPONSE_GREEN_CONVERGED",
            $"body={recovered.Downed}/{recovered.Mobility:0.###};"
            + $"lifecycle={worker.CurrentLifecycleState};"
            + $"medical={medicalQuery.ActiveOrders.Count(order => order != null && order.IsActive && string.Equals(order.patientId, worker.Identity?.PersistentId, StringComparison.Ordinal))};"
            + $"gate={workerWork.HasEmergencyResponseWorkGateForDiagnostics};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"transport={start.Transport.IsTransporting};"
            + $"alert={settlementAlerts.Capture().CommittedLevel}");
        RestoreActorAiPauseStates(isolatedActors);
        Destroy(start.Animal.gameObject);
        yield return null;
    }

    private IEnumerator VerifyTransportDestinationContenderChaos()
    {
        TransportChaosStartProbe start = new();
        yield return StartTransportChaos(
            "TRANSPORT_CONTENDER",
            start);
        if (!start.Live)
        {
            yield break;
        }

        Vector2Int destination = start.State.penPosition;
        WildlifeActor blocker = SpawnCaptureAnimal(destination);
        if (blocker != null)
        {
            blocker.SetCaptured(true);
            blocker.WarpTo(destination);
        }
        bool blockerExact = blocker != null
            && ReferenceEquals(
                grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife),
                blocker)
            && blocker.GridPosition == destination
            && blocker.State == WildlifeState.Captured;
        Check(blockerExact,
            "TRANSPORT_CONTENDER_INJECTED_AFTER_DESTINATION_RESOLVED",
            $"destination={destination};"
            + $"movement={start.Move.IsSystemMoveInProgress}:"
            + $"{start.Move.ActiveSystemMoveDestinationForDiagnostics};"
            + $"blocker={DescribeGridOccupant(blocker)};"
            + $"cell={DescribeGridOccupant(grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife))};"
            + $"state={start.State.transportState};"
            + $"carrier={worker.GetNowXY()}");
        if (!blockerExact)
        {
            yield break;
        }

        float expectedRealSeconds = start.PathWorldDistance
            / Mathf.Max(0.1f, worker.GetMoveSpeed());
        float allowedSeconds = Mathf.Clamp(
            8f + expectedRealSeconds * 2f + start.PathStepCount * 0.15f,
            20f,
            90f);
        float deadline = Time.realtimeSinceStartup + allowedSeconds;
        while (Time.realtimeSinceStartup < deadline
               && start.Transport.IsTransporting)
        {
            yield return null;
        }
        capture.TryGetCaptured(
            start.Animal.WildlifeId,
            out CapturedWildlifeState terminalState);
        Vector2Int[] targetRegistrations = FindGridOccupantRegistrations(
            grid,
            GridLayer.Wildlife,
            start.Animal);
        Vector2Int[] blockerRegistrations = FindGridOccupantRegistrations(
            grid,
            GridLayer.Wildlife,
            blocker);
        bool typedRollback = !start.Transport.IsTransporting
            && !worker.Brain.IsExternallyDrivenActionActive
            && terminalState == null
            && worker.Brain.ExternalIntentTerminalCount
                == start.TerminalBefore + 1
            && worker.Brain.LastExternalIntentTerminalKind
                == CharacterAiActionTerminalKind.Failed
            && start.Transport.LastTerminalFailureReasonForDiagnostics
                .IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0;
        Check(typedRollback,
            "TRANSPORT_CONTENDER_TYPED_ROLLBACK",
            $"transport={start.Transport.IsTransporting};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"capture={terminalState?.transportState};"
            + $"terminal={start.TerminalBefore}"
            + $"->{worker.Brain.ExternalIntentTerminalCount}:"
            + $"{worker.Brain.LastExternalIntentTerminalKind};"
            + $"failure={start.Transport.LastTerminalFailureReasonForDiagnostics};"
            + $"carrier={worker.GetNowXY()};destination={destination};"
            + $"movement={start.Move.IsSystemMoveInProgress}:"
            + $"{start.Move.ActiveSystemMoveDestinationForDiagnostics};"
            + $"allowed={allowedSeconds:0.###}");

        bool physicalConservation = targetRegistrations.Length == 1
            && blockerRegistrations.Length == 1
            && blockerRegistrations[0] == destination
            && ReferenceEquals(
                grid.GetGridCell(targetRegistrations[0])
                    ?.GetOccupant(GridLayer.Wildlife),
                start.Animal)
            && ReferenceEquals(
                grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife),
                blocker)
            && targetRegistrations[0] != destination
            && ReferenceEquals(start.Animal.transform.parent, start.OriginalParent)
            && !start.Animal.transform.IsChildOf(worker.transform)
            && start.Animal.State != WildlifeState.Captured
            && world.Wildlife.Count(value => ReferenceEquals(value, start.Animal)) == 1
            && world.Wildlife.Count(value => ReferenceEquals(value, blocker)) == 1;
        Check(physicalConservation,
            "TRANSPORT_CONTENDER_PHYSICAL_CONSERVATION",
            $"target={targetRegistrations.Length}:"
            + $"{string.Join(",", targetRegistrations)}:"
            + $"{start.Animal.GridPosition}:{start.Animal.State};"
            + $"blocker={blockerRegistrations.Length}:"
            + $"{string.Join(",", blockerRegistrations)}:"
            + $"{blocker.GridPosition}:{blocker.State};"
            + $"parent={DescribeTransform(start.Animal.transform.parent)};"
            + $"expectedParent={DescribeTransform(start.OriginalParent)};"
            + $"world={world.Wildlife.Count(value => ReferenceEquals(value, start.Animal))}/"
            + $"{world.Wildlife.Count(value => ReferenceEquals(value, blocker))}");
        Check(terminalState == null
              && !capture.CapturedAnimals.Any(value => value != null
                  && string.Equals(
                      value.wildlifeId,
                      start.Animal.WildlifeId,
                      StringComparison.Ordinal))
              && !ReferenceEquals(
                  grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife),
                  start.Animal),
            "TRANSPORT_CONTENDER_NO_PENNED_DIVERGENCE",
            $"capture={terminalState?.transportState};"
            + $"aggregateMatches={capture.CapturedAnimals.Count(value => value != null && string.Equals(value.wildlifeId, start.Animal.WildlifeId, StringComparison.Ordinal))};"
            + $"destination={DescribeGridOccupant(grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife))};"
            + $"targetRegistrations={targetRegistrations.Length}");

        if (blocker != null)
        {
            Destroy(blocker.gameObject);
            yield return null;
        }
    }

    private IEnumerator StartTransportChaos(
        string markerPrefix,
        TransportChaosStartProbe probe)
    {
        worker.SetAiPaused(true);
        worker.SetLifecycleState(CharacterLifecycleState.Active);
        worker.transform.position = grid.GetWorldPos(workerCell);
        AbilityWildlifeCaptureTransport existingTransport =
            worker.GetComponent<AbilityWildlifeCaptureTransport>();
        existingTransport?.StopForLifecycleTransition(
            markerPrefix.ToLowerInvariant() + "-prepare");
        worker.Brain?.StopCurrentActionForReplan(
            markerPrefix.ToLowerInvariant() + "-prepare");
        worker.GetComponent<AbilityHaul>()?.StopHauling(
            markerPrefix.ToLowerInvariant() + "-prepare");
        worker.GetComponent<AbilityMove>()?.CancelActiveMovement(
            markerPrefix.ToLowerInvariant() + "-prepare");
        workerWork?.ClearPriorityWorkTarget();
        yield return null;
        yield return null;

        TransportFixturePlanProbe plan = new();
        yield return PlanLawfulTransportFixture(plan);
        WildlifeActor animal = plan.Ready
            ? SpawnCaptureAnimal(plan.SourceCell)
            : null;
        if (animal != null)
        {
            DamageForCapture(animal);
        }
        PickupDeliveryPreflightProbe preflight = new();
        if (animal != null)
        {
            yield return PlaceWorkerAtReachablePickupAndDeliveryStand(
                animal,
                plan,
                preflight);
        }
        bool fixtureReady = plan.Ready
            && animal != null
            && preflight.Ready;
        Check(fixtureReady,
            markerPrefix + "_LAWFUL_FIXTURE",
            $"plan={plan.Ready}:{plan.Detail};"
            + $"animal={animal?.WildlifeId}:{animal?.GridPosition};"
            + $"preflight={preflight.Ready}:{preflight.Detail}");
        if (!fixtureReady)
        {
            yield break;
        }

        int terminalBefore = worker.Brain.ExternalIntentTerminalCount;
        Transform originalParent = animal.transform.parent;
        Vector2Int source = animal.GridPosition;
        bool ordered = capture.TryOrderCapture(
            animal,
            worker,
            pen,
            out string orderFailure);
        AbilityWildlifeCaptureTransport transport =
            worker.GetComponent<AbilityWildlifeCaptureTransport>();
        AbilityMove move = worker.GetComponent<AbilityMove>();
        float expectedRealSeconds = preflight.PathWorldDistance
            / Mathf.Max(0.1f, worker.GetMoveSpeed());
        float allowedSeconds = Mathf.Clamp(
            8f + expectedRealSeconds * 2f + preflight.PathStepCount * 0.15f,
            20f,
            90f);
        float deadline = Time.realtimeSinceStartup + allowedSeconds;
        CapturedWildlifeState state = null;
        bool live = false;
        while (ordered
               && Time.realtimeSinceStartup < deadline
               && transport?.IsTransporting == true)
        {
            capture.TryGetCaptured(animal.WildlifeId, out state);
            live = state?.transportState
                    == CapturedWildlifeTransportState.Transporting
                && animal.transform.IsChildOf(worker.transform)
                && worker.Brain.IsExternallyDrivenActionActive
                && move?.IsSystemMoveInProgress == true
                && move.IsSystemMoveInProgressTo(state.penPosition);
            if (live)
            {
                break;
            }
            yield return null;
        }

        string expectedOwner =
            $"captivity:wildlife-transport:{animal.WildlifeId}";
        live &= string.Equals(
            worker.Brain.ExternalIntentOwnerId,
            expectedOwner,
            StringComparison.Ordinal);
        Check(ordered && live,
            markerPrefix + "_PRODUCTION_CARRY_LIVE",
            $"ordered={ordered}:{orderFailure};"
            + $"state={state?.transportState};"
            + $"carrier={worker.GetNowXY()};"
            + $"animal={animal.GridPosition};"
            + $"parented={animal.transform.IsChildOf(worker.transform)};"
            + $"transport={transport?.IsTransporting};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive}:"
            + $"{worker.Brain.ExternalIntentOwnerId};"
            + $"expected={expectedOwner};"
            + $"movement={move?.IsSystemMoveInProgress}:"
            + $"{move?.ActiveSystemMoveDestinationForDiagnostics};"
            + $"planned={state?.penPosition};"
            + $"terminalFailure={transport?.LastTerminalFailureReasonForDiagnostics};"
            + $"allowed={allowedSeconds:0.###}");
        if (!ordered || !live)
        {
            yield break;
        }

        probe.Live = true;
        probe.Animal = animal;
        probe.OriginalParent = originalParent;
        probe.Source = source;
        probe.State = state;
        probe.Transport = transport;
        probe.Move = move;
        probe.ExpectedOwner = expectedOwner;
        probe.TerminalBefore = terminalBefore;
        probe.PathStepCount = preflight.PathStepCount;
        probe.PathWorldDistance = preflight.PathWorldDistance;
    }

    private IEnumerator ResolveTransportChaosInvasionAndWaitForGreen(
        string markerPrefix)
    {
        int resolvedBefore = invasionResolvedEventCount;
        int startedBefore = invasionStartedEventCount;
        int candidatesBefore = invasionCandidateEventCount;
        gameEvents.Publish(new InvasionResolvedEvent(true, 0f));
        SettlementAlertSnapshot immediate = settlementAlerts.Capture();
        bool resolved = invasionResolvedEventCount == resolvedBefore + 1
            && invasionStartedEventCount == startedBefore
            && invasionCandidateEventCount == candidatesBefore
            && immediate.DesiredLevel == SettlementThreatAlertLevel.Green
            && !immediate.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal);
        Check(resolved,
            markerPrefix + "_INVASION_RESOLVED",
            $"events={startedBefore}->{invasionStartedEventCount}:"
            + $"{resolvedBefore}->{invasionResolvedEventCount}:"
            + $"{candidatesBefore}->{invasionCandidateEventCount};"
            + $"alert={immediate.DesiredLevel}/{immediate.CommittedLevel};"
            + $"incidents=[{string.Join(",", immediate.ActiveIncidentIds)}]");

        long startedHour = calendar.AbsoluteHour;
        float deadline = Time.realtimeSinceStartup + 45f;
        SettlementAlertSnapshot current = immediate;
        while (resolved
               && Time.realtimeSinceStartup < deadline
               && calendar.AbsoluteHour - startedHour <= 6L)
        {
            current = settlementAlerts.Capture();
            if (current.DesiredLevel == SettlementThreatAlertLevel.Green
                && current.CommittedLevel == SettlementThreatAlertLevel.Green
                && !current.ActiveIncidentIds.Contains(
                    "incident:invasion:active",
                    StringComparer.Ordinal)
                && alarmResponses.PendingResponderCountForDiagnostics == 0
                && alarmResponses.ReturningResponderCountForDiagnostics == 0
                && alarmResponses.AssignedResponderCountForDiagnostics == 0
                && !world.Characters.Where(value => value != null)
                    .Select(value => value.GetComponent<AbilityWork>())
                    .Any(value => value != null
                        && value.HasEmergencyResponseWorkGateForDiagnostics))
            {
                break;
            }
            yield return null;
        }

        current = settlementAlerts.Capture();
        bool clean = resolved
            && current.DesiredLevel == SettlementThreatAlertLevel.Green
            && current.CommittedLevel == SettlementThreatAlertLevel.Green
            && !current.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && alarmResponses.PendingResponderCountForDiagnostics == 0
            && alarmResponses.ReturningResponderCountForDiagnostics == 0
            && alarmResponses.AssignedResponderCountForDiagnostics == 0
            && !world.Characters.Where(value => value != null)
                .Select(value => value.GetComponent<AbilityWork>())
                .Any(value => value != null
                    && value.HasEmergencyResponseWorkGateForDiagnostics);
        Check(clean,
            markerPrefix + "_FINAL_GREEN",
            $"hours={calendar.AbsoluteHour - startedHour};"
            + $"alert={current.DesiredLevel}/{current.CommittedLevel};"
            + $"incidents=[{string.Join(",", current.ActiveIncidentIds)}];"
            + $"responses={alarmResponses.PendingResponderCountForDiagnostics}/"
            + $"{alarmResponses.ReturningResponderCountForDiagnostics}/"
            + $"{alarmResponses.AssignedResponderCountForDiagnostics}");
    }

    private List<ActorAiPauseState> PauseAllActorsExceptWorker()
    {
        List<ActorAiPauseState> states = new();
        foreach (CharacterActor actor in world.Characters.Where(value =>
                     value != null && value != worker))
        {
            states.Add(new ActorAiPauseState(actor, actor.IsAiPaused()));
            actor.SetAiPaused(true);
        }
        return states;
    }

    private static void RestoreActorAiPauseStates(
        IEnumerable<ActorAiPauseState> states)
    {
        foreach (ActorAiPauseState state in states ??
                     Array.Empty<ActorAiPauseState>())
        {
            if (state.Actor != null)
            {
                state.Actor.SetAiPaused(state.WasPaused);
            }
        }
    }

    private static bool TryCreateLawfulDownedSnapshot(
        CharacterBodyHealthSnapshot original,
        out CharacterBodyHealthSnapshot downed,
        out string detail)
    {
        downed = default;
        detail = "missing body parts";
        if (original.Parts == null || original.Parts.Count == 0)
        {
            return false;
        }

        List<CharacterBodyPartHealthState> parts = original.Parts
            .Select(CloneBodyPart)
            .ToList();
        int legs = 0;
        foreach (CharacterBodyPartHealthState part in parts)
        {
            if (part?.bodyPart is not CombatBodyPart.LeftLeg
                and not CombatBodyPart.RightLeg)
            {
                continue;
            }
            part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
            legs++;
        }
        detail = $"legs={legs};parts={parts.Count};"
            + $"before={original.Downed}/{original.Mobility:0.###}";
        if (legs != 2)
        {
            return false;
        }

        downed = new CharacterBodyHealthSnapshot(
            parts,
            original.BloodLoss,
            original.Suppression,
            consciousness: 1f,
            manipulation: 1f,
            mobility: 0.08f,
            downed: true);
        return true;
    }

    private IEnumerator ObserveTransportTerminal(
        string wildlifeId,
        PickupDeliveryPreflightProbe preflight,
        TransportTerminalObservation observation)
    {
        if (observation == null)
        {
            yield break;
        }

        AbilityWildlifeCaptureTransport transport =
            worker.GetComponent<AbilityWildlifeCaptureTransport>();
        AbilityMove move = worker.GetComponent<AbilityMove>();
        float speed = Mathf.Max(0.1f, worker.GetMoveSpeed());
        float pathDistance = Mathf.Max(
            0f,
            preflight?.PathWorldDistance ?? 0f);
        int pathSteps = Mathf.Max(0, preflight?.PathStepCount ?? 0);
        // Ability movement may cross scaled and unscaled waits while entering
        // doors or rebuilding an incremental path. Use the slower unscaled
        // distance bound and let the no-progress watchdog catch real stalls.
        float expectedRealSeconds = pathDistance / speed;
        observation.AllowedSeconds = Mathf.Clamp(
            8f + expectedRealSeconds * 2f + pathSteps * 0.15f,
            20f,
            90f);

        float startedAt = Time.realtimeSinceStartup;
        float lastProgressAt = startedAt;
        Vector3 lastWorld = worker.transform.position;
        Vector2Int lastGrid = worker.GetNowXY();
        observation.LastProgressWorldPosition = lastWorld;
        observation.LastProgressGridPosition = lastGrid;
        const float progressEpsilon = 0.005f;
        const float movementStallSeconds = 5f;
        bool observedSystemMovement = false;

        while (Time.realtimeSinceStartup - startedAt
               < observation.AllowedSeconds)
        {
            bool hasState = capture.TryGetCaptured(
                wildlifeId,
                out CapturedWildlifeState state);
            if (hasState && state != null)
            {
                if (state.transportState
                    == CapturedWildlifeTransportState.AwaitingTransport)
                {
                    observation.AwaitingTransportObserved = true;
                    observation.PickupStandReached |=
                        worker.GetNowXY() == preflight.PickupStand;
                }
                else if (state.transportState
                    == CapturedWildlifeTransportState.Transporting)
                {
                    observation.TransportingObserved = true;
                    observation.PickupStandReached |=
                        worker.GetNowXY() == preflight.PickupStand;
                }
            }
            if (state?.transportState
                == CapturedWildlifeTransportState.Penned)
            {
                observation.Penned = true;
                break;
            }
            if (transport?.IsTransporting != true
                || worker.Brain?.IsExternallyDrivenActionActive != true)
            {
                break;
            }

            Vector3 currentWorld = worker.transform.position;
            Vector2Int currentGrid = worker.GetNowXY();
            if (Vector3.Distance(currentWorld, lastWorld) > progressEpsilon
                || currentGrid != lastGrid)
            {
                lastWorld = currentWorld;
                lastGrid = currentGrid;
                lastProgressAt = Time.realtimeSinceStartup;
                observation.LastProgressWorldPosition = currentWorld;
                observation.LastProgressGridPosition = currentGrid;
                observation.ProgressSamples++;
                if (state?.transportState
                    == CapturedWildlifeTransportState.AwaitingTransport)
                {
                    observation.PickupApproachProgressObserved = true;
                }
            }

            if (move?.IsSystemMoveInProgress == true)
            {
                observedSystemMovement = true;
            }
            if (observedSystemMovement
                && move?.IsSystemMoveInProgress == true
                && Time.realtimeSinceStartup - lastProgressAt
                    >= movementStallSeconds)
            {
                observation.Stalled = true;
                break;
            }
            yield return null;
        }

        observation.ObservationSeconds =
            Time.realtimeSinceStartup - startedAt;
        observation.SecondsSinceProgress =
            Time.realtimeSinceStartup - lastProgressAt;
    }

    private IEnumerator TryBeginEscapeThroughInvasion(
        CaptiveState state,
        string scenario,
        EscapeStartProbe probe)
    {
        if (probe == null)
        {
            yield break;
        }
        if (state == null || gameEvents == null)
        {
            probe.Reason = $"{scenario}: missing captive state or event bus";
            yield break;
        }

        // InvasionStartedEvent is the registered production command boundary.
        // The escape-ready authority was installed through the V18 restore
        // transaction; TryGetCaptive deliberately returns a read-only clone.
        int startedBefore = invasionStartedEventCount;
        int resolvedBefore = invasionResolvedEventCount;
        int candidatesBefore = invasionCandidateEventCount;
        gameEvents.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot immediateAlert = settlementAlerts.Capture();
        bool isolatedStart = invasionStartedEventCount == startedBefore + 1
            && invasionResolvedEventCount == resolvedBefore
            && invasionCandidateEventCount == candidatesBefore
            && immediateAlert.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && immediateAlert.DesiredLevel == SettlementThreatAlertLevel.Red
            && immediateAlert.CommittedLevel == SettlementThreatAlertLevel.Red;
        Check(isolatedStart,
            "ESCAPE_INVASION_START_COMMITTED",
            $"scenario={scenario};"
            + $"events=start:{startedBefore}->{invasionStartedEventCount},"
            + $"resolved:{resolvedBefore}->{invasionResolvedEventCount},"
            + $"candidate:{candidatesBefore}->{invasionCandidateEventCount};"
            + $"desired={immediateAlert.DesiredLevel};"
            + $"committed={immediateAlert.CommittedLevel};"
            + $"incident={DescribeInvasionIncident()}");
        if (!isolatedStart)
        {
            probe.Reason = scenario + ": invasion event isolation failed";
            yield break;
        }
        int initialAttempts = state.failedEscapeAttempts;
        yield return WaitUntil(() =>
        {
            if (!captivity.TryGetCaptive(
                    state.captiveId,
                    out CaptiveState observed))
            {
                return false;
            }
            probe.Started = observed.status is CaptivityStatus.EscapeAttempt
                or CaptivityStatus.Escaped
                || observed.failedEscapeAttempts > initialAttempts;
            return probe.Started;
        }, 5f);
        if (probe.Started)
        {
            probe.Reason = string.Empty;
            yield break;
        }

        captivity.TryGetCaptive(state.captiveId, out CaptiveState current);
        probe.Reason = $"{scenario}: invasion trigger did not start escape; "
            + $"status={current?.status};inCustody={current?.IsInCustody};"
            + $"falseCompliance={current?.falseCompliance};"
            + $"risk={current?.escapeRisk};result={current?.lastResult};"
            + $"actor={DescribeActor(captiveActor)};"
            + $"lifecycle={captiveActor?.CurrentLifecycleState}";
    }

    private IEnumerator VerifyAnimalCareRow()
    {
        WildlifeActor animal = SpawnCaptureAnimal(pen.centerPos);
        Check(animal != null, "ANIMAL_CARE_SOURCE", animal?.WildlifeId ?? "missing");
        if (animal == null || workerWork == null) yield break;
        animal.SetCaptured(true);
        animal.ChangeHunger(0.9f - animal.Hunger);
        animal.ChangeThirst(-animal.Thirst);
        string penId = pen.RequirePersistentInstanceId().Value;
        string feedItemId = ResolveAuthoredFeedItemId(animal);
        long expectedFeedMass = string.IsNullOrEmpty(feedItemId)
            ? 0L
            : itemRuntime.MassQuery
                .GetDefinitionUnitMass((ItemDefinitionId)feedItemId)
                .Value;
        string careAnimalId = animal.WildlifeId;
        CircusSaveData circus = Clone(circusPersistence.Capture());
        circus.capturedWildlife.RemoveAll(item =>
            item != null && item.wildlifeId == careAnimalId);
        circus.capturedWildlife.Add(new CapturedWildlifeState
        {
            wildlifeId = careAnimalId,
            speciesId = animal.SpeciesId,
            penId = penId,
            penPosition = pen.centerPos,
            capturePosition = animal.GridPosition,
            transportState = CapturedWildlifeTransportState.Penned,
            isTamed = false,
            nextCareAt = 0f,
            lastCareStatus = "qa-penned"
        });
        // First publish the explicit test roster, so the registered input owner
        // derives its real demand/claim/capacity. Do not invent a buffer owner.
        bool rosterRestored = RestoreCircus(circus);
        WildlifeActor[] preparedAnimals = world.Wildlife
            .Where(value => value != null && value.WildlifeId == careAnimalId)
            .ToArray();
        Check(rosterRestored && preparedAnimals.Length == 1,
            "ANIMAL_CARE_ROSTER_PREPARATION_RESTORE",
            $"id={careAnimalId};restored={rosterRestored};matches={preparedAnimals.Length};"
            + "explicit-test-roster;trainingEligible=false");
        if (!rosterRestored || preparedAnimals.Length != 1) yield break;
        animal = preparedAnimals[0];
        IWildlifeCareInputOwnerRuntime careOwner =
            scope.Container.Resolve<IWildlifeCareInputOwnerRuntime>();
        bool ownerReady = careOwner.TryReconcileLive(out string ownerFailure);
        string feedDestinationId = WildlifeCareInputOwnerAuthority
            .FormatDestinationId(penId);
        Check(ownerReady, "ANIMAL_CARE_FEED_OWNER_READY",
            $"destination={feedDestinationId};reason={ownerFailure};"
            + "registered-owner-command;trainingEligible=false");
        if (!ownerReady) yield break;
        int feedQuantityBefore = CountFacilityItem(feedDestinationId, feedItemId);
        bool feedSpawned = !string.IsNullOrEmpty(feedItemId)
            && itemRuntime.SpawnItemAt(
                feedItemId,
                1,
                pen.centerPos,
                WorldItemStackState.FacilityBuffer,
                feedDestinationId,
                out int spawnedFeed)
            && spawnedFeed == 1;
        WorldItemStackSnapshot feedSource = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, feedDestinationId, StringComparison.Ordinal)
                && string.Equals(stack.ItemId, feedItemId, StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(feedSpawned
              && feedSource != null
              && expectedFeedMass > 0L,
            "ANIMAL_CARE_FEED_SOURCE_PHYSICAL",
            $"item={feedItemId};spawned={feedSpawned};"
            + $"stack={feedSource?.StackId};mass={expectedFeedMass};"
            + $"destination={feedDestinationId};"
            + $"before={feedQuantityBefore};after={CountFacilityItem(feedDestinationId, feedItemId)}");
        if (!feedSpawned || feedSource == null || expectedFeedMass <= 0L)
        {
            yield break;
        }
        float feedHungerBefore = animal.Hunger;
        circus = Clone(circusPersistence.Capture());
        bool circusRestored = RestoreCircus(circus);
        Check(circusRestored, "ANIMAL_CARE_CIRCUS_V18_RESTORE", careAnimalId);
        if (!circusRestored) yield break;
        WildlifeActor[] restoredAnimals = world.Wildlife
            .Where(value => value != null && value.WildlifeId == careAnimalId)
            .ToArray();
        bool careAnimalRebound = restoredAnimals.Length == 1;
        Check(careAnimalRebound, "ANIMAL_CARE_WILDLIFE_ID_REBOUND",
            $"id={careAnimalId};matches={restoredAnimals.Length};trainingEligible=false");
        if (!careAnimalRebound) yield break;
        animal = restoredAnimals[0];
        bool preparationRestoredExactly = CountFacilityItem(feedDestinationId, feedItemId)
                == feedQuantityBefore + 1
            && itemRuntime.GetAllStacks().Any(stack => stack != null
                && stack.StackId == feedSource.StackId
                && stack.State == WorldItemStackState.FacilityBuffer
                && stack.DestinationId == feedDestinationId && stack.ItemId == feedItemId
                && stack.AvailableQuantity == stack.Quantity
                && stack.ReservedQuantity == 0 && !stack.Forbidden
                && string.IsNullOrEmpty(stack.ReservedByPersistentId))
            && Mathf.Approximately(animal.Hunger, feedHungerBefore);
        Check(preparationRestoredExactly, "ANIMAL_CARE_PREPARATION_RESTORE_EXACT",
            $"id={careAnimalId};stack={feedSource.StackId};"
            + $"destination={feedDestinationId};"
            + $"quantity={CountFacilityItem(feedDestinationId, feedItemId)};hunger={animal.Hunger:0.####};"
            + "before-runtime-feed=true;trainingEligible=false");
        if (!preparationRestoredExactly) yield break;

        string feedOperationId = CapturedWildlifeFeedOutbox.FormatOperationId(
            animal.WildlifeId,
            1);
        string expectedFeedCommitId =
            $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Sink}:"
            + $"{feedOperationId}:1:{expectedFeedMass}";
        bool feedCommitted = false;
        CapturedWildlifeState fedState = null;
        yield return WaitUntil(() =>
        {
            feedCommitted = capture.TryGetCaptured(animal.WildlifeId, out fedState)
                && fedState != null
                && fedState.nextFeedOperationSequence == 1
                && string.Equals(
                    animal.LastCaptiveFeedCommitId,
                    expectedFeedCommitId,
                    StringComparison.Ordinal)
                && !CapturedWildlifeFeedOutbox.HasPending(fedState);
            return feedCommitted;
        }, 5f);
        int feedQuantityAfter = CountFacilityItem(feedDestinationId, feedItemId);
        float expectedHungerAfter = Mathf.Clamp01(feedHungerBefore - 0.72f);
        bool physicalSinkExact = feedCommitted
            && feedQuantityAfter == feedQuantityBefore
            && !batchDispositions.TryGetPending(feedOperationId, out _)
            && Mathf.Abs(animal.Hunger - expectedHungerAfter) <= 0.03f;
        Check(physicalSinkExact,
            "ANIMAL_CARE_FEED_SINK_EXACT",
            $"item={feedItemId};quantity={feedQuantityBefore + 1}->{feedQuantityAfter};"
            + $"hunger={feedHungerBefore:0.####}->{animal.Hunger:0.####};"
            + $"expected={expectedHungerAfter:0.####};commit={animal.LastCaptiveFeedCommitId};"
            + $"pending={batchDispositions.TryGetPending(feedOperationId, out _)}");
        bool outboxClean = feedCommitted
            && fedState.nextFeedOperationSequence == 1
            && CapturedWildlifeFeedOutbox.HasEmptyProvenance(fedState)
            && string.Equals(fedState.lastFeedItemId, feedItemId, StringComparison.Ordinal)
            && Mathf.Approximately(fedState.lastFeedDiseaseChance, 0f);
        Check(outboxClean,
            "ANIMAL_CARE_FEED_OUTBOX_CLEAN",
            $"sequence={fedState?.nextFeedOperationSequence};"
            + $"phase={fedState?.pendingFeedPhase};item={fedState?.lastFeedItemId};"
            + $"disease={fedState?.lastFeedDiseaseChance:0.####}");
        CapturedWildlifeState savedFeedState = circusPersistence.Capture()
            .capturedWildlife
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.wildlifeId,
                    animal.WildlifeId,
                    StringComparison.Ordinal));
        WildlifeSaveData savedFeedActor = animal.Capture();
        Check(savedFeedState != null
              && savedFeedState.nextFeedOperationSequence == 1
              && CapturedWildlifeFeedOutbox.HasEmptyProvenance(savedFeedState)
              && string.Equals(
                  savedFeedActor.lastCaptiveFeedCommitId,
                  expectedFeedCommitId,
                  StringComparison.Ordinal),
            "ANIMAL_CARE_FEED_SAVE_EXACT",
            $"state={savedFeedState != null};"
            + $"sequence={savedFeedState?.nextFeedOperationSequence};"
            + $"phase={savedFeedState?.pendingFeedPhase};"
            + $"actorCommit={savedFeedActor.lastCaptiveFeedCommitId}");
        if (!physicalSinkExact || !outboxClean || savedFeedState == null)
        {
            yield break;
        }

        // Preserve the actual completed feed before any later animal-care
        // setup. This official snapshot is the immutable cold-process input,
        // not a hand-authored or upgraded older QA world.
        captiveActor.SetAiPaused(true);
        captiveActor.characterType = captiveOldType;
        DungeonGameSaveData completedFeedWorld = gameSaveService.Capture();
        DungeonSaveSectionEnvelope completedFeedCircusSection = completedFeedWorld.sections
            .Single(section => section.sectionId == CircusSaveSection.Id);
        CircusSaveData completedFeedCircus = JsonUtility.FromJson<CircusSaveData>(
            completedFeedCircusSection.payloadJson);
        CapturedWildlifeState completedFeedTimer = completedFeedCircus.capturedWildlife
            .Single(value => value.wildlifeId == careAnimalId);
        capture.TryGetCaptured(careAnimalId, out CapturedWildlifeState liveFeedTimer);
        float expectedCareRemaining = Math.Min(CapturedWildlifeState.CareIntervalSeconds,
            Math.Max(0f, liveFeedTimer.nextCareAt - scope.Container.Resolve<IGameClock>().Time));
        Check(completedFeedCircusSection.sectionVersion == CircusSaveData.CurrentVersion
              && completedFeedCircus.version == 8 && completedFeedTimer.nextCareAt == 0f
              && !completedFeedCircusSection.payloadJson.Contains("\"nextCareAt\"")
              && completedFeedTimer.remainingCareSeconds == expectedCareRemaining
              && completedFeedTimer.remainingCareSeconds <= CapturedWildlifeState.CareIntervalSeconds,
            "ANIMAL_CARE_REMAINING_DELAY_SAVE_EXACT",
            $"version={completedFeedCircus.version};remaining={completedFeedTimer.remainingCareSeconds:R};"
            + $"expected={expectedCareRemaining:R};runtimeDeadline={liveFeedTimer.nextCareAt:R};absoluteDeadlineSerialized=false");
        if (failures.Count != 0) yield break;
        string completedFeedJson = gameSaveService.ToJson(completedFeedWorld, true);
        CircusProgramRegistry programs = scope.Container.Resolve<CircusProgramRegistry>();
        bool invalidCareDelaysRejected = true;
        foreach (float invalidDelay in new[] { -0.1f,
                     CapturedWildlifeState.CareIntervalSeconds + 0.1f,
                     float.NaN, float.PositiveInfinity })
        {
            CircusSaveData invalidDelayPayload = JsonUtility.FromJson<CircusSaveData>(
                completedFeedCircusSection.payloadJson);
            invalidDelayPayload.capturedWildlife.Single(value => value.wildlifeId == careAnimalId)
                .remainingCareSeconds = invalidDelay;
            DungeonGameRestoreReport invalidDelayReport = new();
            CircusSaveValidation.Validate(invalidDelayPayload, programs, invalidDelayReport);
            invalidCareDelaysRejected &= !invalidDelayReport.Success
                && invalidDelayReport.Errors.Any(error => error.Contains("invalid numeric state"));
        }
        CircusSaveData legacyTimerPayload = JsonUtility.FromJson<CircusSaveData>(
            completedFeedCircusSection.payloadJson);
        legacyTimerPayload.version = 6;
        DungeonGameRestoreReport legacyTimerReport = new();
        CircusSaveValidation.Validate(legacyTimerPayload, programs, legacyTimerReport);
        Check(invalidCareDelaysRejected && !legacyTimerReport.Success
              && legacyTimerReport.Errors.Any(error => error.Contains("version 6 is invalid")),
            "ANIMAL_CARE_DELAY_INVALID_AND_OLD_VERSION_REJECTED",
            "negative/over-interval/NaN/infinity delays rejected;V6 rejected;no-live-mutation;no-migration");
        if (failures.Count != 0) yield break;
        string feedWorldPath = Path.Combine(Path.GetDirectoryName(
            CaptivityWildlifeLifecyclePlayModeVerifier.ReportPath), "completed-feed-world.json");
        string feedObservationPath = Path.ChangeExtension(feedWorldPath, ".observation.json");
        if (File.Exists(feedWorldPath) || File.Exists(feedObservationPath))
            throw new InvalidOperationException("Completed feed checkpoint evidence already exists.");
        byte[] worldBytes = new System.Text.UTF8Encoding(false).GetBytes(completedFeedJson);
        using System.Security.Cryptography.SHA256 worldHasher =
            System.Security.Cryptography.SHA256.Create();
        CompletedFeedCheckpointObservation feedObservation = new()
        {
            worldSha256 = BitConverter.ToString(worldHasher.ComputeHash(worldBytes))
                .Replace("-", string.Empty).ToLowerInvariant(),
            gameTime = scope.Container.Resolve<IGameClock>().Time,
            absoluteDay = calendar.Day,
            animalId = careAnimalId,
            penId = penId,
            feedItemId = feedItemId,
            operationId = feedOperationId,
            commitId = expectedFeedCommitId,
            consumedQuantity = 1,
            consumedMassGrams = expectedFeedMass,
            remainingQuantity = feedQuantityAfter,
            hungerBefore = feedHungerBefore,
            hungerAfter = animal.Hunger,
            healthAfter = animal.CurrentHealth,
            feedSequence = savedFeedState.nextFeedOperationSequence
        };
        using (FileStream stream = new(feedWorldPath, FileMode.CreateNew,
                   FileAccess.Write, FileShare.Read)) stream.Write(worldBytes, 0, worldBytes.Length);
        using (FileStream stream = new(feedObservationPath, FileMode.CreateNew,
                   FileAccess.Write, FileShare.Read))
        using (StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false)))
            writer.Write(JsonUtility.ToJson(feedObservation, true));
        Check(completedFeedWorld.sections.Count == baselineSaveData.sections.Count
              && savedFeedActor.lastCaptiveFeedCommitId == feedObservation.commitId
              && feedObservation.feedSequence == 1,
            "ANIMAL_CARE_COMPLETED_FEED_OFFICIAL_CHECKPOINT",
            $"sections={completedFeedWorld.sections.Count};animal={careAnimalId};"
            + $"commit={feedObservation.commitId};gameTime={feedObservation.gameTime:0.###};"
            + $"worldSha256={feedObservation.worldSha256};"
            + "actual-consumption-then-official-save;not-cold-process-resume;trainingEligible=false");
        if (failures.Count != 0) yield break;

        DungeonAnimalHusbandrySaveData husbandry = new();
        husbandry.penPolicies.Add(new AnimalPenPolicySaveData
        {
            penBuildingInstanceId = penId,
            allowedSpeciesDefinitionIds = new List<string>
            {
                animal.SpeciesId
            }
        });
        husbandry.animals.Add(new HusbandryAnimalSaveData
        {
            animalInstanceId = animal.WildlifeId,
            speciesDefinitionId = animal.SpeciesId,
            penBuildingInstanceId = penId,
            sex = AnimalSex.Female,
            ageDays = 20f,
            tamed = false,
            tamingProgress = 0f,
            pendingWorkKind = AnimalHusbandryWorkKind.Tame,
            pendingWorkCompleted = 0f,
            statusCode = AnimalHusbandryStatusCode.AwaitingTaming
        });
        int candidateRevisionBeforeRestore =
            facilityCandidateCache.DynamicStateVersion;
        Check(RestoreHusbandry(husbandry), "ANIMAL_CARE_V18_RESTORE", animal.WildlifeId);
        Check(facilityCandidateCache.DynamicStateVersion
              > candidateRevisionBeforeRestore,
            "ANIMAL_CARE_CANDIDATE_CACHE_INVALIDATED",
            $"dynamicRevision={candidateRevisionBeforeRestore}"
            + $"->{facilityCandidateCache.DynamicStateVersion}");

        worker.SetLifecycleState(CharacterLifecycleState.Active);
        worker.transform.position = grid.GetWorldPos(workerCell);
        workerWork.SetWorkPriority(BuiltInWorkTypeIds.AnimalCare, WorkPriorityLevel.Priority1);
        FacilityWorkType animalCareLegacyType =
            FacilityWorkTypeMap.GetRequired(BuiltInWorkTypeIds.AnimalCare);
        int candidateIndexStableFrames = 0;
        int candidateIndexWaitFrames = 0;
        bool candidateIndexPending = true;
        bool candidateIndexContainsPen = false;
        float candidateIndexDeadline = Time.realtimeSinceStartup + 8f;
        while (candidateIndexStableFrames < 2
               && Time.realtimeSinceStartup < candidateIndexDeadline)
        {
            candidateIndexWaitFrames++;
            candidateIndexPending = facilityCandidateCache.HasPendingIndexBuild;
            candidateIndexContainsPen = facilityCandidateCache
                .GetWorkCandidates(grid, animalCareLegacyType)
                .Contains(pen);
            candidateIndexStableFrames = !candidateIndexPending
                && candidateIndexContainsPen
                    ? candidateIndexStableFrames + 1
                    : 0;
            if (candidateIndexStableFrames < 2)
            {
                yield return null;
            }
        }
        Check(candidateIndexStableFrames >= 2,
            "ANIMAL_CARE_CANDIDATE_INDEX_READY",
            $"stable={candidateIndexStableFrames}/2;frames={candidateIndexWaitFrames};"
            + $"pending={candidateIndexPending};containsPen={candidateIndexContainsPen};"
            + $"revision={facilityCandidateCache.CandidateIndexVersion};"
            + $"dynamic={facilityCandidateCache.DynamicStateVersion}");
        if (candidateIndexStableFrames < 2)
        {
            yield break;
        }
        AnimalCareAiPreflightSnapshot preflight =
            AnimalCareAiPreflight.Capture(
                worker,
                workerWork,
                pen,
                husbandryQuery,
                workPolicyRegistry,
                facilityCandidateCache);
        string preflightDetail = preflight.Format();
        Check(preflight.AuthoredWorkType,
            "ANIMAL_CARE_PREFLIGHT_AUTHORED_WORK_TYPE",
            preflightDetail);
        Check(preflight.HusbandryAvailable,
            "ANIMAL_CARE_PREFLIGHT_HUSBANDRY_QUERY",
            preflightDetail);
        Check(preflight.SearchAvailable && preflight.WorkAccessAvailable,
            "ANIMAL_CARE_PREFLIGHT_WORK_ACCESS_PATH",
            preflightDetail);
        Check(preflight.PolicyAvailable,
            "ANIMAL_CARE_PREFLIGHT_WORK_POLICY",
            preflightDetail);
        Check(preflight.CandidateAvailable && preflight.CandidateTargetsPen,
            "ANIMAL_CARE_PREFLIGHT_TARGET_SELECTOR",
            preflightDetail);
        Check(preflight.AiWorkCatalogAvailable,
            "ANIMAL_CARE_PREFLIGHT_AIWORK_CATALOG",
            preflightDetail);
        Check(preflight.Passed,
            "ANIMAL_CARE_PREFLIGHT_EARLIEST_AUTHORITY",
            preflightDetail);
        if (!preflight.Passed)
        {
            yield break;
        }
        EmergencyReserveSnapshot ledgerBefore = emergencyAccounting.CaptureSnapshot();
        RecordAnimalCareAccounting("BEFORE_START");
        AnimalCareStartObservation initialStart = new();
        yield return ObserveAnimalCareStart(initialStart, "qa-animal-care");
        Check(initialStart.Neutralized,
            "ANIMAL_CARE_SUBJECT_NEUTRALIZED",
            initialStart.NeutralizationDetail);
        Check(initialStart.PriorityAccepted
              && initialStart.PriorityTargetMatches
              && initialStart.PriorityWorkTypeMatches
              && !initialStart.AutonomousHaulCanStart,
            "ANIMAL_CARE_PRIORITY_SUPPRESSES_AUTONOMOUS_HAUL",
            initialStart.PriorityArbitrationDetail);
        Check(initialStart.PreferredDeferredCount == 0
              || (initialStart.PreferredDeferredFallbackSuppressions
                      == initialStart.PreferredDeferredCount
                  && initialStart.Started),
            "ANIMAL_CARE_PREFERRED_DEFERRED_OWNERSHIP",
            initialStart.PriorityArbitrationDetail);
        Check(initialStart.PreferredCommitCount == 1
              && initialStart.PreferredDisposition
                  == CharacterAiPreferredActionDisposition.Selected
              && initialStart.PreferredDispositionBranch
                  == CharacterAiBranch.Work
              && initialStart.AiWorkEvaluationObserved
              && initialStart.Started,
            "ANIMAL_CARE_PREFERRED_BT_ARBITRATION",
            initialStart.PriorityArbitrationDetail);
        Check(initialStart.UnexpectedIntermediateEpochCount == 0,
            "ANIMAL_CARE_NO_INTERMEDIATE_FALLBACK_COMMIT",
            initialStart.EpochTransitions);
        Check(initialStart.Started,
            "ANIMAL_CARE_BRAIN_AIWORK_STARTED",
            initialStart.Detail);
        if (!initialStart.Started)
        {
            yield break;
        }

        AbilityMove animalCareMove = worker.GetComponent<AbilityMove>();
        AIAction animalCareAction = worker.Brain.bestAction;
        Vector2Int progressStartGrid = worker.GetNowXY();
        Vector3 progressStartWorld = worker.transform.position;
        float observationSeconds = CalculateBoundedAnimalCareObservationSeconds(
            animalCareAction,
            grid.GetWorldPos(animal.GridPosition));
        float progressDeadline = Time.realtimeSinceStartup + observationSeconds;
        float lastObservedProgressAt = Time.realtimeSinceStartup;
        Vector3 lastObservedWorld = progressStartWorld;
        long lastGameplayProgress = worker.Brain
            .CaptureRuntimeGateSnapshot()
            .GameplayProgressRevision;
        bool movementOrGameplayAdvanced = false;
        bool stalled = false;
        float progress = 0f;
        while (Time.realtimeSinceStartup < progressDeadline)
        {
            husbandryQuery.TryGetAnimal(
                new WildlifeInstanceId(animal.WildlifeId),
                out HusbandryAnimalState state);
            progress = state?.TamingProgress ?? 0f;
            if (progress > 0f) break;

            CharacterAiRuntimeGateSnapshot progressGate = worker.Brain
                .CaptureRuntimeGateSnapshot();
            Vector3 currentWorld = worker.transform.position;
            bool positionAdvanced =
                (currentWorld - lastObservedWorld).sqrMagnitude > 0.0001f;
            bool gameplayAdvanced =
                progressGate.GameplayProgressRevision > lastGameplayProgress;
            if (positionAdvanced || gameplayAdvanced)
            {
                movementOrGameplayAdvanced = true;
                lastObservedProgressAt = Time.realtimeSinceStartup;
                lastObservedWorld = currentWorld;
                lastGameplayProgress = progressGate.GameplayProgressRevision;
            }
            else if (Time.realtimeSinceStartup - lastObservedProgressAt >= 5f)
            {
                stalled = true;
                break;
            }
            yield return null;
        }
        Vector2Int progressEndGrid = worker.GetNowXY();
        Vector3 progressEndWorld = worker.transform.position;
        Check(progress > 0f && !stalled,
            "ANIMAL_CARE_PROGRESS",
            $"progress={progress:0.####};eta={observationSeconds:0.##}s;"
            + $"startGrid={progressStartGrid};endGrid={progressEndGrid};"
            + $"startWorld={progressStartWorld};endWorld={progressEndWorld};"
            + $"advanced={movementOrGameplayAdvanced};stalled={stalled};"
            + $"phase={worker.Brain.CurrentActionPhase};"
            + $"movement={animalCareMove?.LastGridMoveFailureReason}");

        int releaseAttemptsBefore = worker
            .TransientAiOwnershipReleaseAttemptCountForDiagnostics;
        int repeatedWorkCleanupBefore = worker
            .RepeatedWorkOwnershipCleanupCountForDiagnostics;
        long lifecycleTransitionBefore = worker.Lifecycle
            .LifecycleTransitionRevisionForDiagnostics;
        CharacterAiRuntimeGateSnapshot lifecycleGateBefore = worker.Brain
            .CaptureRuntimeGateSnapshot();
        CharacterBodyHealthSnapshot originalBody = CloneBodyHealthSnapshot(
            bodyHealthQuery.GetSnapshot(worker));
        List<CharacterBodyPartHealthState> downedParts = originalBody.Parts
            .Select(CloneBodyPart)
            .ToList();
        int injuredLegCount = 0;
        foreach (CharacterBodyPartHealthState part in downedParts)
        {
            if (part.bodyPart is not CombatBodyPart.LeftLeg
                and not CombatBodyPart.RightLeg)
            {
                continue;
            }

            part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
            injuredLegCount++;
        }
        Check(injuredLegCount == 2,
            "ANIMAL_CARE_DOWNED_BODY_FIXTURE",
            $"legs={injuredLegCount};parts={downedParts.Count};"
            + $"beforeDowned={originalBody.Downed};"
            + $"beforeMobility={originalBody.Mobility:0.###}");
        if (injuredLegCount != 2)
        {
            yield break;
        }

        RecordAnimalCareAccounting("BEFORE_DOWNED");
        bodyHealthCommands.ApplySnapshot(
            worker,
            new CharacterBodyHealthSnapshot(
                downedParts,
                originalBody.BloodLoss,
                originalBody.Suppression,
                consciousness: 1f,
                manipulation: 1f,
                mobility: 0.08f,
                downed: true),
            "qa-animal-care-downed");
        CharacterBodyHealthSnapshot synchronousBody = bodyHealthQuery
            .GetSnapshot(worker);
        CharacterMedicalOrder synchronousOrder = medicalQuery.ActiveOrders
            .FirstOrDefault(order => order != null
                && order.IsActive
                && string.Equals(
                    order.patientId,
                    worker.Identity?.PersistentId,
                    StringComparison.Ordinal));
        bool synchronousCleanup = synchronousBody.Downed
            && worker.CurrentLifecycleState == CharacterLifecycleState.Downed
            && synchronousOrder != null
            && !workerWork.isWorking
            && !workerWork.HasActiveWorkRoutineForDiagnostics
            && pen.WorkerReservation == null
            && pen.CurrentUserCount == 0;
        RecordAnimalCareAccounting("SYNCHRONOUS_DOWNED");
        yield return null;
        yield return null;
        husbandryQuery.TryGetAnimal(
            new WildlifeInstanceId(animal.WildlifeId),
            out HusbandryAnimalState afterDowned);
        float frozen = afterDowned?.TamingProgress ?? 0f;
        for (int frame = 0; frame < 4; frame++) yield return null;
        husbandryQuery.TryGetAnimal(
            new WildlifeInstanceId(animal.WildlifeId),
            out HusbandryAnimalState afterDelay);
        float delayed = afterDelay?.TamingProgress ?? 0f;
        CharacterAiRuntimeGateSnapshot gate = worker.Brain.CaptureRuntimeGateSnapshot();
        CharacterBodyHealthSnapshot stableDownedBody = bodyHealthQuery
            .GetSnapshot(worker);
        CharacterMedicalOrder stableDownedOrder = medicalQuery.ActiveOrders
            .FirstOrDefault(order => order != null
                && order.IsActive
                && string.Equals(
                    order.patientId,
                    worker.Identity?.PersistentId,
                    StringComparison.Ordinal));
        bool typedCancellation = worker.Brain.CaptureRuntimeDiagnostics()
            .TryGetActionTerminal(
                initialStart.Epoch,
                out CharacterAiActionTerminalKind downedTerminal)
            && downedTerminal == CharacterAiActionTerminalKind.Cancelled;
        EmergencyReserveSnapshot ledgerAfter = emergencyAccounting.CaptureSnapshot();
        RecordAnimalCareAccounting("DELAYED_DOWNED");
        Check(!workerWork.isWorking
              && !workerWork.HasActiveWorkRoutineForDiagnostics
              && synchronousCleanup
              && stableDownedBody.Downed
              && worker.CurrentLifecycleState == CharacterLifecycleState.Downed
              && stableDownedOrder != null
              && Mathf.Approximately(frozen, delayed)
              && pen.WorkerReservation == null
              && pen.CurrentUserCount == 0
              && gate.LivePathRequests == 0
              && gate.LiveReservations == 0
              && gate.ActionTerminals == lifecycleGateBefore.ActionTerminals + 1
              && typedCancellation
              && worker.TransientAiOwnershipReleaseAttemptCountForDiagnostics
                  == releaseAttemptsBefore + 1
              && worker.TransientAiOwnershipReleasedForDiagnostics
              && worker.Lifecycle.LifecycleTransitionRevisionForDiagnostics
                  == lifecycleTransitionBefore + 1
              && ledgerAfter.ActiveOperationCount <= ledgerBefore.ActiveOperationCount,
            "ANIMAL_CARE_DOWNED_EXACT_CLEANUP",
            $"synchronous={synchronousCleanup};"
            + $"bodyDowned={synchronousBody.Downed}->{stableDownedBody.Downed};"
            + $"mobility={synchronousBody.Mobility:0.###}"
            + $"->{stableDownedBody.Mobility:0.###};"
            + $"medicalOrder={synchronousOrder?.orderId}"
            + $"->{stableDownedOrder?.orderId};"
            + $"working={workerWork.isWorking};"
            + $"routine={workerWork.HasActiveWorkRoutineForDiagnostics};"
            + $"progress={frozen:0.####}->{delayed:0.####};"
            + $"worker={pen.WorkerReservation};users={pen.CurrentUserCount};"
            + $"paths={gate.LivePathRequests};reservations={gate.LiveReservations};"
            + $"actionTerminals={lifecycleGateBefore.ActionTerminals}"
            + $"->{gate.ActionTerminals};typedTerminal={downedTerminal};"
            + $"ledger={ledgerBefore.ActiveOperationCount}"
            + $"->{ledgerAfter.ActiveOperationCount};"
            + "releaseAttempts="
            + $"{releaseAttemptsBefore}"
            + $"->{worker.TransientAiOwnershipReleaseAttemptCountForDiagnostics};"
            + "repeatedWorkCleanup="
            + $"{repeatedWorkCleanupBefore}"
            + $"->{worker.RepeatedWorkOwnershipCleanupCountForDiagnostics};"
            + "releaseReason="
            + $"{worker.LastTransientAiOwnershipReleaseReasonForDiagnostics};"
            + $"released={worker.TransientAiOwnershipReleasedForDiagnostics};"
            + $"lifecycle={worker.CurrentLifecycleState};"
            + "transitionRevision="
            + $"{lifecycleTransitionBefore}"
            + $"->{worker.Lifecycle.LifecycleTransitionRevisionForDiagnostics};"
            + "transition="
            + $"{worker.Lifecycle.LastTransitionPreviousStateForDiagnostics}"
            + $"->{worker.Lifecycle.LastTransitionNextStateForDiagnostics};"
            + "transitionInProgress="
            + $"{worker.Lifecycle.LifecycleTransitionInProgressForDiagnostics}");

        worker.SetAiPaused(true);
        workerWork.ClearPriorityWorkTarget();
        bodyHealthCommands.ApplySnapshot(
            worker,
            originalBody,
            "qa-animal-care-recovered");
        yield return null;
        yield return null;
        CharacterBodyHealthSnapshot recoveredBody = bodyHealthQuery
            .GetSnapshot(worker);
        bool medicalOrderReleased = !medicalQuery.ActiveOrders.Any(order =>
            order != null
            && order.IsActive
            && string.Equals(
                order.patientId,
                worker.Identity?.PersistentId,
                StringComparison.Ordinal));
        Check(!recoveredBody.Downed
              && worker.CurrentLifecycleState == CharacterLifecycleState.Active
              && medicalOrderReleased
              && !workerWork.isWorking
              && !workerWork.HasActiveWorkRoutineForDiagnostics,
            "ANIMAL_CARE_DOWNED_BODY_RECOVERED",
            $"bodyDowned={recoveredBody.Downed};"
            + $"mobility={recoveredBody.Mobility:0.###};"
            + $"lifecycle={worker.CurrentLifecycleState};"
            + $"medicalReleased={medicalOrderReleased};"
            + $"paused={worker.IsAiPaused()};"
            + $"working={workerWork.isWorking};"
            + $"routine={workerWork.HasActiveWorkRoutineForDiagnostics}");
        Check(RestoreHusbandry(husbandry),
            "ANIMAL_CARE_TARGET_LOSS_RESTORE", animal.WildlifeId);
        workerWork.SetWorkPriority(BuiltInWorkTypeIds.AnimalCare, WorkPriorityLevel.Priority1);
        AnimalCareStartObservation targetLossStart = new();
        yield return ObserveAnimalCareStart(
            targetLossStart,
            "qa-animal-care-target-loss");
        Check(targetLossStart.Neutralized,
            "ANIMAL_CARE_TARGET_LOSS_SUBJECT_NEUTRALIZED",
            targetLossStart.NeutralizationDetail);
        bool targetRunStarted = targetLossStart.Started;
        pen.DestroySelf();
        yield return null;
        yield return null;
        gate = worker.Brain.CaptureRuntimeGateSnapshot();
        Check(targetRunStarted
              && !workerWork.isWorking
              && !workerWork.HasActiveWorkRoutineForDiagnostics
              && gate.LivePathRequests == 0
              && gate.LiveReservations == 0,
            "ANIMAL_CARE_PEN_DESTROY_EXACT_CLEANUP",
            $"started={targetRunStarted};working={workerWork.isWorking};routine={workerWork.HasActiveWorkRoutineForDiagnostics};paths={gate.LivePathRequests};reservations={gate.LiveReservations}");
    }

    private IEnumerator VerifyCapturedAnimalTamingCompletion(string animalId)
    {
        WildlifeInstanceId typedId = new(animalId);
        string penId = pen.RequirePersistentInstanceId().Value;
        string workerId = CharacterPersistentIdentity.Require(worker).Value;
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        ICharacterEnvironmentStatusQuery environment = scope.Container
            .Resolve<ICharacterEnvironmentStatusQuery>();
        float syncStart = clock.Time;
        float syncDeadline = Time.realtimeSinceStartup + 8f;
        HusbandryAnimalState initial = null;
        while (!husbandryQuery.TryGetAnimal(typedId, out initial)
               && clock.Time - syncStart < 10f
               && Time.realtimeSinceStartup < syncDeadline)
        {
            yield return null;
        }
        AnimalHusbandryWorkSnapshot work = default;
        bool realRoster = initial != null && initial.PenId.Value == penId
            && !initial.Tamed && initial.TamingProgress == 0f
            && husbandryQuery.TryGetWork(pen, worker, out work)
            && work.AnimalId.Equals(typedId)
            && work.Kind == AnimalHusbandryWorkKind.Tame
            && work.RequiredWork > 0f && work.CompletedWork == 0f;
        Check(realRoster, "TAME_REAL_CAPTURE_HUSBANDRY_REGISTERED",
            $"animal={animalId};pen={initial?.PenId.Value};tamed={initial?.Tamed};"
            + $"progress={initial?.TamingProgress};kind={work.Kind};"
            + $"required={work.RequiredWork};completed={work.CompletedWork};"
            + "production-tick-registration;roster-or-terminal-injection=false");
        if (!realRoster) yield break;

        WorkCompletionIdentityDeliveryLedger deliveries = scope.Container
            .Resolve<WorkCompletionIdentityDeliveryLedger>();
        string cursorsBefore = JsonUtility.ToJson(new TameDeliveryCursorEvidence
        {
            cursors = deliveries.Capture().ToList()
        });
        HashSet<string> identitiesBefore = outcomeLedger.CaptureGameplayOutcomes()
            .exactOutcomes.Where(value => value.producerId
                == WorkCompletionIdentityOutcomeIds.ProducerId)
            .Select(TameResultKey).ToHashSet(StringComparer.Ordinal);
        int childrenBefore = CaptureTameChildren(animalId).Length;
        AnimalHusbandryRuntime owner = husbandryQuery as AnimalHusbandryRuntime;
        IWorkCompletionIdentityDeliveryCommand actualCompletion = scope.Container
            .Resolve<IWorkCompletionIdentityDeliveryCommand>();
        CharacterIdentityEventPublisher actualEvents = scope.Container
            .Resolve<CharacterIdentityEventPublisher>();
        IDomainFactPublisher productionFacts = scope.Container
            .Resolve<IDomainFactPublisher>();
        HeldTameCompletionCommand heldCompletion = new(actualCompletion, animalId);
        Check(owner != null, "TAME_REGISTERED_COMPLETION_OWNER", "registered runtime, not injected animal state");
        if (owner == null) yield break;
        using var routedFacts = new HusbandryCompletionDomainFactTestPublisher(
            productionFacts,
            heldCompletion,
            actualEvents,
            deliveries,
            new WorkIdentityAppliedFactMapping(outcomeLedger, outcomeLedger));
        owner.ConstructDomainFacts(routedFacts);
        try
        {
        workerWork.SetWorkPriority(BuiltInWorkTypeIds.AnimalCare,
            WorkPriorityLevel.Priority1);
        AnimalCareStartObservation start = new();
        yield return ObserveAnimalCareStart(start, "qa-real-capture-tame-complete");
        Check(start.Started && start.Neutralized && start.PriorityAccepted
              && start.PriorityTargetMatches && start.PriorityWorkTypeMatches
              && start.PreferredCommitCount == 1,
            "TAME_REAL_CAPTURE_BRAIN_AIWORK_STARTED",
            start.Detail + ";" + start.PriorityArbitrationDetail);
        if (!start.Started) yield break;

        // This is a new complete-work observation, not a timeout extension of
        // the partial-care fault matrix. Non-target resident needs are explicit
        // test preparation; animal state, work rate and required work are not.
        float gameStart = clock.Time;
        float gameBudget = work.RequiredWork * 20f + 60f;
        float wallDeadline = Time.realtimeSinceStartup + 120f;
        float lastProgressGameTime = gameStart;
        float nextNeutralize = gameStart + 5f;
        float lastProgress = 0f;
        int progressSamples = 0;
        int frames = 0;
        bool stalled = false;
        bool completed = false;
        HusbandryAnimalState final = initial;
        while (clock.Time - gameStart < gameBudget
               && Time.realtimeSinceStartup < wallDeadline)
        {
            frames++;
            husbandryQuery.TryGetAnimal(typedId, out final);
            if (frames <= 128)
            {
                CharacterAiRuntimeDiagnosticsSnapshot diagnostics =
                    worker.Brain.CaptureRuntimeDiagnostics();
                CharacterAiRuntimeGateSnapshot gate = diagnostics.Gate;
                rows.Add("INFO\tTAME_REAL_CAPTURE_FRAME\t"
                    + $"frame={Time.frameCount};game={clock.Time:0.###};delta={clock.DeltaTime:0.###};"
                    + $"progress={final?.TamingProgress:0.######};kind={final?.PendingWorkKind};"
                    + $"manure={final?.ReadyManureCycles};products="
                    + string.Join(",", final?.Products?.Select(value =>
                        $"{value.ItemId.Value}:{value.ReadyCycles}") ?? Array.Empty<string>())
                    + $";tamed={final?.Tamed};action={worker.Brain.bestAction?.actionset?.GetType().Name};"
                    + $"epoch={worker.Brain.RuntimeActionEpoch};phase={worker.Brain.CurrentActionPhase};"
                    + $"phaseDetail={worker.Brain.CurrentActionPhaseDetail};failure={worker.Brain.LastActionFailure};"
                    + $"lastExecutionFailure={diagnostics.LastExecutionFailureDetail};"
                    + $"lastReplan={diagnostics.LastInterruptedReplanDetail};"
                    + $"working={workerWork.isWorking};routine={workerWork.HasActiveWorkRoutineForDiagnostics};"
                    + $"assigned={workerWork.AssignedWorkTypeId.Value};target={workerWork.assignedShop?.name};"
                    + $"cancel={workerWork.ActiveWorkCancellationCountForDiagnostics}:"
                    + $"{workerWork.LastActiveWorkCancellationReasonForDiagnostics};"
                    + $"preWu={workerWork.LastPreWuExitKindForDiagnostics}:"
                    + $"{workerWork.LastPreWuExitDetailForDiagnostics};duty={workerWork.CurrentDutyState};"
                    + $"routineNeed={workerWork.LastWorkRunInterruptedForRoutineNeed};"
                    + $"env={environment.GetPhysiologicalBand(new CharacterId(workerId))}/"
                    + $"{environment.GetVisualBand(new CharacterId(workerId))};"
                    + $"needs={DescribeAnimalCareNeeds(worker.Stats)};"
                    + $"terminals={gate.ActionCompleted}/{gate.ActionFailed}/{gate.ActionCancelled};"
                    + $"neutralizeDue={clock.Time >= nextNeutralize}");
            }
            if (final?.TamingProgress > lastProgress)
            {
                lastProgress = final.TamingProgress;
                lastProgressGameTime = clock.Time;
                progressSamples++;
            }
            if (final?.Tamed == true && final.TamingProgress == 1f
                && CaptureTameChildren(animalId).Length == childrenBefore + 1
                && !workerWork.isWorking
                && !workerWork.HasActiveWorkRoutineForDiagnostics)
            {
                // The actual effect/child/work phase ends before the held
                // identity is released. Exact identity completion remains a
                // mandatory later phase after official pending-world restore.
                completed = true;
                break;
            }
            if (clock.Time - lastProgressGameTime >= 20f)
            {
                stalled = true;
                break;
            }
            if (clock.Time >= nextNeutralize)
            {
                nextNeutralize = clock.Time + 5f;
                long epochBefore = worker.Brain.RuntimeActionEpoch;
                AIAction actionBefore = worker.Brain.bestAction;
                long cancellationBefore = workerWork.ActiveWorkCancellationCountForDiagnostics;
                // The full deprivation reset is a quiescent setup boundary
                // which intentionally cancels live actions. During execution
                // maintain test-only needs without resetting their actor owner.
                bool neutral = TryNeutralizeAnimalCareSubject(
                    out string needDetail, resetDeprivation: false);
                bool actionPreserved = worker.Brain.RuntimeActionEpoch == epochBefore
                    && ReferenceEquals(worker.Brain.bestAction, actionBefore)
                    && workerWork.ActiveWorkCancellationCountForDiagnostics == cancellationBefore;
                Check(neutral && actionPreserved,
                    "TAME_NON_TARGET_PREPARATION_PRESERVES_ACTION",
                    $"epoch={epochBefore}->{worker.Brain.RuntimeActionEpoch};"
                    + $"sameAction={ReferenceEquals(worker.Brain.bestAction, actionBefore)};"
                    + $"cancel={cancellationBefore}->{workerWork.ActiveWorkCancellationCountForDiagnostics};"
                    + needDetail + ";trainingEligible=false");
                if (!neutral || !actionPreserved)
                {
                    yield break;
                }
            }
            yield return null;
        }
        capture.TryGetCaptured(animalId, out CapturedWildlifeState captured);
        if (!completed)
        {
            rows.Add("INFO\tTAME_INTERRUPTION_RECENT_CHARACTER_LOG\t"
                + string.Join(" | ", worker.Log.TakeLast(12)));
            rows.Add("INFO\tTAME_INTERRUPTION_RUNTIME_TRACE\t"
                + worker.Brain.CaptureRuntimeDiagnostics().FormatRecentTrace());
        }
        Check(completed && captured?.isTamed == true
              && captured.transportState == CapturedWildlifeTransportState.Penned
              && final?.AnimalId.Equals(typedId) == true,
            "TAME_REAL_CAPTURE_EFFECT_AND_WORK_COMPLETE",
            $"animal={animalId};tamed={final?.Tamed}/{captured?.isTamed};"
            + $"progress={lastProgress:0.######};samples={progressSamples};frames={frames};"
            + $"gameSeconds={clock.Time - gameStart:0.###}/{gameBudget:0.###};"
            + $"stalled={stalled};phase={worker.Brain?.CurrentActionPhase};"
            + $"failure={worker.Brain?.LastActionFailure};working={workerWork.isWorking};"
            + $"routine={workerWork.HasActiveWorkRoutineForDiagnostics};"
            + "actual-Brain-AIWork-AbilityWork-WorkTaskExecutor;trainingEligible=false");
        if (!completed) yield break;

        worker.SetAiPaused(true);
        worker.Brain.StopCurrentActionForReplan("qa-completed-tame-save");
        workerWork.ClearPriorityWorkTarget();
        float pendingDeadline = Time.realtimeSinceStartup + 5f;
        while (heldCompletion.DeferredAttempts == 0 && Time.realtimeSinceStartup < pendingDeadline)
            yield return null;
        AnimalHusbandryPendingOutcomeSaveData pendingIdentity = husbandryPersistence
            .Capture().pendingOutcomes.SingleOrDefault(value => value.animalId == animalId);
        rows.Add("INFO\tTAME_DEFERRED_BOUNDARY_PENDING_EXACT\t"
            + JsonUtility.ToJson(pendingIdentity));
        foreach (GameplayOutcomeSnapshot observedIdentity in outcomeLedger.CaptureGameplayOutcomes()
                     .exactOutcomes.Where(value => value.producerId == WorkCompletionIdentityOutcomeIds.ProducerId
                         && !identitiesBefore.Contains(TameResultKey(value))))
            rows.Add("INFO\tTAME_DEFERRED_BOUNDARY_NEW_IDENTITY_EXACT\t"
                + JsonUtility.ToJson(observedIdentity));
        bool retainedPending = pendingIdentity != null && pendingIdentity.completionIdentityRequired
            && pendingIdentity.outcomeCommitted
            && NoHusbandryCompletionCursor(pendingIdentity, deliveries.Capture())
            && heldCompletion.DeferredAttempts > 0
            && CaptureTameChildren(animalId).Length == childrenBefore + 1
            && !outcomeLedger.CaptureGameplayOutcomes().exactOutcomes.Any(value =>
                value.producerId == WorkCompletionIdentityOutcomeIds.ProducerId
                && !identitiesBefore.Contains(TameResultKey(value)));
        Check(retainedPending, "TAME_IDENTITY_DEFERRED_AFTER_REAL_EFFECT",
            $"animal={animalId};heldAttempts={heldCompletion.DeferredAttempts};"
            + $"child={pendingIdentity?.publicationKey};"
            + $"approvedContribution={pendingIdentity?.completionContributionWorkUnits};"
            + "test-only-delivery-deferral;no-effect-or-child-injection");
        if (!retainedPending) yield break;

        captiveActor.SetAiPaused(true);
        captiveActor.characterType = captiveOldType;
        DungeonGameSaveData pendingWorld = gameSaveService.Capture();
        string pendingJson = gameSaveService.ToJson(pendingWorld, true);
        string pendingEvidence = Path.Combine(Path.GetDirectoryName(
            CaptivityWildlifeLifecyclePlayModeVerifier.ReportPath),
            "real-capture-tame-identity-pending-world.json");
        using (FileStream stream = new(pendingEvidence, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false))) writer.Write(pendingJson);
        string pendingFrozen = JsonUtility.ToJson(pendingIdentity);
        bool pendingRestored = RestoreCircus(null, gameSaveService.FromJson(pendingJson));
        AnimalHusbandryPendingOutcomeSaveData restoredPending = husbandryPersistence.Capture()
            .pendingOutcomes.SingleOrDefault(value => value.animalId == animalId);
        Check(pendingRestored && restoredPending != null
              && JsonUtility.ToJson(restoredPending) == pendingFrozen
              && CaptureTameChildren(animalId).Length == childrenBefore + 1,
            "TAME_PENDING_IDENTITY_OFFICIAL_WORLD_RESTORE",
            $"success={pendingRestored};sections={pendingWorld.sections.Count};"
            + $"sameChild={restoredPending?.publicationKey};frozen-request-exact;"
            + "same-process-fresh-world;not-cold-process-resume");
        if (!pendingRestored || restoredPending == null) yield break;
        AnimalHusbandryPendingOutcomeState restoredCompletion = owner
            .BuildRestore(husbandryPersistence.Capture()).State.PendingOutcomes
            .Single(value => value.AnimalId.Value == animalId).Clone();
        worker.SetAiPaused(true);
        worker.Brain.StopCurrentActionForReplan("qa-tame-pending-identity-restored");
        // The real effect/child is committed, but its canonical identity delivery
        // is still held. Validate the wrong animal before the first valid ACK,
        // then verify changed same-sequence replay separately after completion.
        WorkCompletedIdentityEvent pendingTerminal = AnimalHusbandryCompletionObservation
            .CreateCompletionEvent(restoredCompletion);
        WorkCompletedIdentityEvent pendingWrongAnimal = new(pendingTerminal.Character,
            pendingTerminal.WorkId, "tame|wild:qa-other-animal", pendingTerminal.Origin,
            pendingTerminal.AbsoluteDay, pendingTerminal.OperationId,
            pendingTerminal.TerminalOwnerId, pendingTerminal.ChildResultKey,
            pendingTerminal.TargetEntityId, pendingTerminal.ContributionWorkUnits,
            pendingTerminal.SkillModifierProvenance);
        WorkCompletionIdentityDeliveryRequest wrongChildRequest =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(pendingWrongAnimal);
        Check(!deliveries.Capture().Any(value =>
                value.producerStreamId == wrongChildRequest.ProducerStreamId),
            "TAME_WRONG_ANIMAL_CHILD_FRESH_CANONICAL_DELIVERY", wrongChildRequest.ProducerStreamId);
        string wrongChildOwnerBefore = JsonUtility.ToJson(husbandryPersistence.Capture());
        string wrongChildLedgerBefore = JsonUtility.ToJson(outcomeLedger.CaptureGameplayOutcomes());
        string wrongChildCursorsBefore = JsonUtility.ToJson(new TameDeliveryCursorEvidence
            { cursors = deliveries.Capture().ToList() });
        WorkCompletionIdentityDeliveryResult wrongChildResult = actualCompletion.EnsureApplied(wrongChildRequest);
        Check(wrongChildResult.Status == WorkCompletionIdentityDeliveryStatus.Conflict
              && wrongChildResult.FailureReason.StartsWith(
                  "work-identity-husbandry-child-payload-conflict:", StringComparison.Ordinal)
              && wrongChildOwnerBefore == JsonUtility.ToJson(husbandryPersistence.Capture())
              && wrongChildLedgerBefore == JsonUtility.ToJson(outcomeLedger.CaptureGameplayOutcomes())
              && wrongChildCursorsBefore == JsonUtility.ToJson(new TameDeliveryCursorEvidence
                  { cursors = deliveries.Capture().ToList() }),
            "TAME_WRONG_ANIMAL_CHILD_REJECTED_WITHOUT_MUTATION", wrongChildResult.FailureReason);
        heldCompletion.Hold = false;
        float identityDeadline = Time.realtimeSinceStartup + 5f;
        while (husbandryPersistence.Capture().pendingOutcomes.Any(value => value.animalId == animalId)
               && Time.realtimeSinceStartup < identityDeadline)
            yield return null;
        GameplayOutcomeSnapshot[] children = CaptureTameChildren(animalId);
        GameplayOutcomeSnapshot child = children.Length == childrenBefore + 1
            ? children.Last() : null;
        GameplayOutcomeSnapshot[] identities = outcomeLedger.CaptureGameplayOutcomes()
            .exactOutcomes.Where(value => value.producerId
                    == WorkCompletionIdentityOutcomeIds.ProducerId
                && !identitiesBefore.Contains(TameResultKey(value))).ToArray();
        GameplayOutcomeSnapshot identity = identities.Length == 1 ? identities[0] : null;
        double childWork = child?.metrics.FirstOrDefault(value => value.metricId
            == MigratedProducerOutcomeIds.CompletedWorkMetric.Value)?.value
            ?? double.NaN;
        Check(child != null && child.status == GameplayOutcomeStatus.Succeeded
              && TameFact(child, "animal-id") == animalId
              && TameFact(child, "pen-id") == penId
              && TameFact(child, "worker-id") == workerId
              && child.participants.Any(value => value.entityId == workerId
                  && value.roleId == MigratedProducerOutcomeIds.ActorRole.Value)
              && Math.Abs(childWork - Math.Round(work.RequiredWork * 1000d)) <= 1d
              && husbandryPersistence.Capture().pendingOutcomes.Count == 0,
            "TAME_CHILD_EXACT_IDENTITY_WORK_ONCE",
            $"animal={animalId};children={childrenBefore}->{children.Length};"
            + $"child={TameResultKey(child)};worker={TameFact(child, "worker-id")};"
            + $"workMilli={childWork}/{work.RequiredWork * 1000d};pending="
            + husbandryPersistence.Capture().pendingOutcomes.Count);
        Check(identity != null && identity.status == GameplayOutcomeStatus.Succeeded
              && TameFact(identity, WorkCompletionIdentityOutcomeIds.WorkIdFact.Value)
                  == BuiltInWorkTypeIds.AnimalCare.Value
              && identity.participants.Any(value => value.entityId == workerId)
              && identity.subjects.Any(value => value.entityId == penId
                  && value.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value)
              && identity.sequence > child?.sequence
              && TameFact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value)
                  == TameResultKey(child)
              && TameFact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact.Value)
                  == child?.producerId
              && cursorsBefore != JsonUtility.ToJson(new TameDeliveryCursorEvidence
                  { cursors = deliveries.Capture().ToList() }),
            "TAME_WORK_IDENTITY_ONCE",
            $"identities={identities.Length};identity={TameResultKey(identity)};"
            + $"target={penId};"
            + $"childReference={TameFact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value)}");
        if (child == null || identity == null || failures.Count != 0) yield break;

        captiveActor.SetAiPaused(true);
        captiveActor.characterType = captiveOldType;
        DungeonGameSaveData completedSave = gameSaveService.Capture();
        string json = gameSaveService.ToJson(completedSave, true);
        string evidencePath = Path.Combine(Path.GetDirectoryName(
            CaptivityWildlifeLifecyclePlayModeVerifier.ReportPath),
            "real-capture-tame-completed-world.json");
        using (FileStream stream = new(evidencePath, FileMode.CreateNew,
                   FileAccess.Write, FileShare.Read))
        using (StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false)))
            writer.Write(json);
        bool restored = RestoreCircus(null, gameSaveService.FromJson(json));
        WildlifeActor[] restoredAnimals = wildlife.Wildlife.Where(value =>
            value != null && value.WildlifeId == animalId).ToArray();
        husbandryQuery.TryGetAnimal(typedId, out HusbandryAnimalState restoredState);
        capture.TryGetCaptured(animalId, out CapturedWildlifeState restoredCaptured);
        GameplayOutcomeSnapshot[] restoredChildren = CaptureTameChildren(animalId);
        GameplayOutcomeSnapshot[] restoredIdentities = outcomeLedger.CaptureGameplayOutcomes()
            .exactOutcomes.Where(value => TameResultKey(value) == TameResultKey(identity)).ToArray();
        Check(restored && restoredAnimals.Length == 1
              && restoredState?.Tamed == true && restoredState.TamingProgress == 1f
              && restoredCaptured?.isTamed == true
              && restoredCaptured.penId == penId
              && restoredChildren.Length == children.Length
              && restoredChildren.Last().immutablePayloadHash == child.immutablePayloadHash
              && restoredIdentities.Length == 1
              && restoredIdentities[0].immutablePayloadHash == identity.immutablePayloadHash,
            "TAME_COMPLETED_OFFICIAL_WORLD_RESTORE",
            $"success={restored};sections={completedSave.sections.Count};"
            + $"animal={animalId}:{restoredAnimals.Length};tamed={restoredState?.Tamed};"
            + $"children={restoredChildren.Length};identities={restoredIdentities.Length};"
            + "same-process-fresh-world;not-cold-process-resume");
        if (!restored || worker == null || pen == null) yield break;
        worker.SetAiPaused(true);
        worker.Brain.StopCurrentActionForReplan("qa-restored-tame-retry");
        string animalBeforeRetry = JsonUtility.ToJson(husbandryPersistence.Capture());
        string ledgerBeforeRetry = JsonUtility.ToJson(outcomeLedger.CaptureGameplayOutcomes());
        bool retry = scope.Container.Resolve<IAnimalHusbandryCommand>().ApplyWork(
            pen, worker, typedId, AnimalHusbandryWorkKind.Tame,
            work.RequiredWork, out bool retryCompleted);
        Check(!retry && !retryCompleted
              && animalBeforeRetry == JsonUtility.ToJson(husbandryPersistence.Capture())
              && ledgerBeforeRetry == JsonUtility.ToJson(outcomeLedger.CaptureGameplayOutcomes()),
            "TAME_COMPLETED_RETRY_NO_EFFECT_OR_DUPLICATE",
            $"accepted={retry};completed={retryCompleted};animal={animalId};"
            + "owner-state-and-ledger-byte-exact;no-terminal-injection");
        WorkCompletedIdentityEvent terminal = AnimalHusbandryCompletionObservation.CreateCompletionEvent(
            restoredCompletion);
        string replayLedger = JsonUtility.ToJson(outcomeLedger.CaptureGameplayOutcomes());
        string replayCursors = JsonUtility.ToJson(new TameDeliveryCursorEvidence
            { cursors = deliveries.Capture().ToList() });
        WorkCompletionIdentityDeliveryResult replayIdentity = actualCompletion.EnsureApplied(
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(terminal));
        Check(replayIdentity.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
              && replayLedger == JsonUtility.ToJson(outcomeLedger.CaptureGameplayOutcomes())
              && replayCursors == JsonUtility.ToJson(new TameDeliveryCursorEvidence
                  { cursors = deliveries.Capture().ToList() }),
            "TAME_REFERENCED_IDENTITY_REPLAY_BYTE_EXACT", replayIdentity.Status.ToString());
        WorkCompletedIdentityEvent wrongAnimal = new(terminal.Character, terminal.WorkId,
            "tame|wild:qa-other-animal", terminal.Origin, terminal.AbsoluteDay,
            terminal.OperationId, terminal.TerminalOwnerId, terminal.ChildResultKey,
            terminal.TargetEntityId, terminal.ContributionWorkUnits, terminal.SkillModifierProvenance);
        WorkCompletionIdentityDeliveryResult wrongAnimalResult = actualCompletion.EnsureApplied(
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(wrongAnimal));
        Check(wrongAnimalResult.Status == WorkCompletionIdentityDeliveryStatus.Conflict
              && wrongAnimalResult.FailureReason
                  == "The completion delivery sequence has conflicting provenance."
              && animalBeforeRetry == JsonUtility.ToJson(husbandryPersistence.Capture())
              && replayLedger == JsonUtility.ToJson(outcomeLedger.CaptureGameplayOutcomes())
              && replayCursors == JsonUtility.ToJson(new TameDeliveryCursorEvidence
                  { cursors = deliveries.Capture().ToList() }),
            "TAME_CHANGED_EVENT_SEQUENCE_REJECTED_WITHOUT_MUTATION", wrongAnimalResult.FailureReason);

        }
        finally
        {
            owner.ConstructDomainFacts(productionFacts);
        }
    }

    private IEnumerator VerifyCapturedAnimalSlaughterCompletion(string animalId)
    {
        WildlifeInstanceId typedId = new(animalId);
        string penId = pen.RequirePersistentInstanceId().Value;
        string workerId = CharacterPersistentIdentity.Require(worker).Value;
        WildlifeActor target = wildlife.Wildlife.SingleOrDefault(value =>
            value != null && value.WildlifeId == animalId);
        bool valid = target != null && target.IsAlive
            && husbandryQuery.TryGetAnimal(typedId, out HusbandryAnimalState initial)
            && initial.Tamed && !initial.SlaughterDesignated;
        Check(valid, "SLAUGHTER_REAL_TAMED_SOURCE", animalId);
        if (!valid) yield break;
        string carcassItemId = target.Species.CarcassItemId;
        long unitMass = itemRuntime.MassQuery.GetDefinitionUnitMass(
            new ItemDefinitionId(carcassItemId)).Value;
        HashSet<string> originalStacks = itemRuntime.GetAllStacks()
            .Select(value => value.StackId).ToHashSet(StringComparer.Ordinal);
        string branchJson = gameSaveService.ToJson(gameSaveService.Capture(), true);
        AnimalHusbandryRuntime owner = husbandryQuery as AnimalHusbandryRuntime;
        IAnimalHusbandryCommand commands = scope.Container.Resolve<IAnimalHusbandryCommand>();
        IWorkCompletionIdentityDeliveryCommand actualCompletion = scope.Container
            .Resolve<IWorkCompletionIdentityDeliveryCommand>();
        CharacterIdentityEventPublisher events = scope.Container.Resolve<CharacterIdentityEventPublisher>();
        WorkCompletionIdentityDeliveryLedger deliveries = scope.Container
            .Resolve<WorkCompletionIdentityDeliveryLedger>();
        IDomainFactPublisher productionFacts = scope.Container
            .Resolve<IDomainFactPublisher>();
        IWildlifeCarcassService carcasses = scope.Container.Resolve<IWildlifeCarcassService>();
        HashSet<string> identitiesBefore = outcomeLedger.CaptureGameplayOutcomes().exactOutcomes
            .Where(value => value.producerId == WorkCompletionIdentityOutcomeIds.ProducerId)
            .Select(TameResultKey).ToHashSet(StringComparer.Ordinal);
        int childrenBefore = CaptureSlaughterChildren(animalId).Length;
        int releasesBefore = CountWildlifeOutcome(EnvironmentOutcomeIds.WildlifeCommandProducer,
            "capture-release", animalId);
        HeldTameCompletionCommand held = new(actualCompletion, animalId, "slaughter");
        Check(owner != null && childrenBefore == 0, "SLAUGHTER_REGISTERED_OWNER", animalId);
        if (owner == null || childrenBefore != 0) yield break;
        using var routedFacts = new HusbandryCompletionDomainFactTestPublisher(
            productionFacts,
            held,
            events,
            deliveries,
            new WorkIdentityAppliedFactMapping(outcomeLedger, outcomeLedger));
        owner.ConstructDomainFacts(routedFacts);
        try
        {
            bool designated = commands.DesignateSlaughter(typedId, true, out AnimalHusbandryFailure failure);
            AnimalHusbandryWorkSnapshot work = default;
            bool selected = husbandryQuery.TryGetWork(pen, worker, out work)
                && work.AnimalId.Equals(typedId) && work.Kind == AnimalHusbandryWorkKind.Slaughter
                && work.RequiredWork > 0f && work.CompletedWork == 0f;
            Check(designated && selected, "SLAUGHTER_NORMAL_DESIGNATION_SELECTS_WORK",
                $"accepted={designated};failure={failure};kind={work.Kind};required={work.RequiredWork};"
                + "terminal-or-work-injection=false;trainingEligible=false");
            if (!designated || !selected) yield break;
            workerWork.SetWorkPriority(BuiltInWorkTypeIds.AnimalCare, WorkPriorityLevel.Priority1);
            AnimalCareStartObservation start = new();
            yield return ObserveAnimalCareStart(start, "qa-real-capture-slaughter");
            Check(start.Started && start.Neutralized && start.PriorityAccepted
                  && start.PriorityTargetMatches && start.PriorityWorkTypeMatches
                  && start.PreferredCommitCount == 1,
                "SLAUGHTER_REAL_BRAIN_AIWORK_STARTED", start.Detail + ";" + start.PriorityArbitrationDetail);
            if (!start.Started || failures.Count != 0) yield break;
            IGameClock clock = scope.Container.Resolve<IGameClock>();
            float gameStart = clock.Time;
            float gameBudget = work.RequiredWork * 20f + 60f;
            float wallDeadline = Time.realtimeSinceStartup + 120f;
            float progressAt = gameStart;
            float nextNeutralize = gameStart + 5f;
            float lastProgress = 0f;
            int samples = 0;
            bool completed = false;
            bool stalled = false;
            while (clock.Time - gameStart < gameBudget && Time.realtimeSinceStartup < wallDeadline)
            {
                if (CaptureSlaughterChildren(animalId).Length == childrenBefore + 1
                    && !workerWork.isWorking && !workerWork.HasActiveWorkRoutineForDiagnostics)
                {
                    completed = true;
                    break;
                }
                husbandryQuery.TryGetAnimal(typedId, out HusbandryAnimalState current);
                if (current?.PendingWorkCompleted > lastProgress)
                {
                    lastProgress = current.PendingWorkCompleted;
                    progressAt = clock.Time;
                    samples++;
                }
                if (clock.Time - progressAt >= 20f) { stalled = true; break; }
                if (clock.Time >= nextNeutralize)
                {
                    nextNeutralize = clock.Time + 5f;
                    long epoch = worker.Brain.RuntimeActionEpoch;
                    AIAction action = worker.Brain.bestAction;
                    long cancellations = workerWork.ActiveWorkCancellationCountForDiagnostics;
                    bool neutral = TryNeutralizeAnimalCareSubject(out string detail, resetDeprivation: false);
                    bool preserved = epoch == worker.Brain.RuntimeActionEpoch
                        && ReferenceEquals(action, worker.Brain.bestAction)
                        && cancellations == workerWork.ActiveWorkCancellationCountForDiagnostics;
                    Check(neutral && preserved, "SLAUGHTER_NON_TARGET_PREPARATION_PRESERVES_ACTION",
                        detail + $";actionPreserved={preserved};trainingEligible=false");
                    if (!neutral || !preserved) yield break;
                }
                yield return null;
            }
            Check(completed && !capture.TryGetCaptured(animalId, out _)
                  && !husbandryQuery.TryGetAnimal(typedId, out _)
                  && !wildlife.Wildlife.Any(value => value != null && value.WildlifeId == animalId)
                  && (target == null || !target.IsAlive)
                  && CountWildlifeOutcome(EnvironmentOutcomeIds.WildlifeCommandProducer,
                      "capture-release", animalId) == releasesBefore + 1,
                "SLAUGHTER_ACTUAL_DEATH_RELEASE_REGISTRY_REMOVAL",
                $"completed={completed};animal={animalId};samples={samples};work={lastProgress};"
                + $"gameSeconds={clock.Time - gameStart}/{gameBudget};stalled={stalled};"
                + $"phase={worker.Brain.CurrentActionPhase};failure={worker.Brain.LastActionFailure};"
                + "actual-Brain-AIWork-AbilityWork-WorkTaskExecutor;trainingEligible=false");
            if (!completed || failures.Count != 0) yield break;
            worker.SetAiPaused(true);
            worker.Brain.StopCurrentActionForReplan("qa-slaughter-completed-save");
            workerWork.ClearPriorityWorkTarget();
            AnimalHusbandryPendingOutcomeSaveData pending = husbandryPersistence.Capture()
                .pendingOutcomes.SingleOrDefault(value => value.animalId == animalId);
            WorldItemStackSnapshot[] outputs = itemRuntime.GetAllStacks().Where(value =>
                !originalStacks.Contains(value.StackId) && value.ItemId == carcassItemId).ToArray();
            WorldItemStackSnapshot stack = outputs.Length == 1 ? outputs[0] : null;
            GameplayOutcomeSnapshot child = CaptureSlaughterChildren(animalId).SingleOrDefault();
            string outputFact = stack == null ? string.Empty
                : $"item={carcassItemId};quantity=1;massGrams={unitMass};stacks={stack.StackId}:1;carcass={stack.StackId}";
            double childWork = child?.metrics.FirstOrDefault(value => value.metricId
                == MigratedProducerOutcomeIds.CompletedWorkMetric.Value)?.value ?? double.NaN;
            Check(stack != null && stack.Quantity == 1 && unitMass > 0 && child != null
                  && child.status == GameplayOutcomeStatus.Succeeded
                  && TameFact(child, "physical-output") == outputFact
                  && TameFact(child, "animal-id") == animalId && TameFact(child, "pen-id") == penId
                  && TameFact(child, "worker-id") == workerId
                  && Math.Abs(childWork - Math.Round(work.RequiredWork * 1000d)) <= 1d
                  && carcasses.CaptureFreshness().Count(value => value.stackId == stack.StackId
                      && value.speciesId == TameFact(child, MigratedProducerOutcomeIds.SpeciesFact.Value)) == 1
                  && pending != null && pending.outcomeCommitted && pending.completionIdentityRequired
                  && NoHusbandryCompletionCursor(pending, deliveries.Capture()) && held.DeferredAttempts > 0
                  && pending.carcassStackId == stack.StackId
                  && pending.publicationKey == TameResultKey(child)
                  && !outcomeLedger.CaptureGameplayOutcomes().exactOutcomes.Any(value =>
                      value.producerId == WorkCompletionIdentityOutcomeIds.ProducerId
                      && !identitiesBefore.Contains(TameResultKey(value))),
                "SLAUGHTER_PHYSICAL_CHILD_AND_PENDING_IDENTITY_EXACT",
                $"output={outputFact};actual={TameFact(child, "physical-output")};workMilli={childWork};"
                + $"child={TameResultKey(child)};held={held.DeferredAttempts};"
                + "identity-only-test-deferral;effect-and-child-injection=false");
            if (failures.Count != 0) yield break;
            DungeonGameSaveData pendingWorld = gameSaveService.Capture();
            string pendingJson = gameSaveService.ToJson(pendingWorld, true);
            WriteSlaughterCheckpoint("real-capture-slaughter-identity-pending-world.json", pendingJson);
            string frozenPending = JsonUtility.ToJson(pending);
            bool restored = RestoreCircus(null, gameSaveService.FromJson(pendingJson));
            AnimalHusbandryPendingOutcomeSaveData restoredPending = husbandryPersistence.Capture()
                .pendingOutcomes.SingleOrDefault(value => value.animalId == animalId);
            Check(restored && restoredPending != null && JsonUtility.ToJson(restoredPending) == frozenPending
                  && CaptureSlaughterChildren(animalId).Single().immutablePayloadHash == child.immutablePayloadHash
                  && itemRuntime.GetAllStacks().Count(value => value.StackId == stack.StackId
                      && value.Quantity == 1 && value.ItemId == carcassItemId) == 1
                  && !capture.TryGetCaptured(animalId, out _)
                  && !wildlife.Wildlife.Any(value => value != null && value.WildlifeId == animalId),
                "SLAUGHTER_PENDING_OFFICIAL_WORLD_RESTORE",
                $"success={restored};sections={pendingWorld.sections.Count};same-request/child/physical;"
                + "same-process-fresh-world;not-cold-process-resume");
            if (!restored || restoredPending == null || failures.Count != 0) yield break;
            AnimalHusbandryPendingOutcomeState completion = owner.BuildRestore(husbandryPersistence.Capture())
                .State.PendingOutcomes.Single(value => value.AnimalId.Value == animalId).Clone();
            held.Hold = false;
            float identityDeadline = Time.realtimeSinceStartup + 5f;
            while (husbandryPersistence.Capture().pendingOutcomes.Any(value => value.animalId == animalId)
                   && Time.realtimeSinceStartup < identityDeadline) yield return null;
            GameplayOutcomeSnapshot[] identities = outcomeLedger.CaptureGameplayOutcomes().exactOutcomes
                .Where(value => value.producerId == WorkCompletionIdentityOutcomeIds.ProducerId
                    && !identitiesBefore.Contains(TameResultKey(value))).ToArray();
            GameplayOutcomeSnapshot identity = identities.Length == 1 ? identities[0] : null;
            Check(identity != null && identity.status == GameplayOutcomeStatus.Succeeded
                  && identity.sequence > child.sequence
                  && TameFact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value) == TameResultKey(child)
                  && TameFact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact.Value) == child.producerId
                  && TameFact(identity, WorkCompletionIdentityOutcomeIds.WorkIdFact.Value) == BuiltInWorkTypeIds.AnimalCare.Value
                  && identity.participants.Any(value => value.entityId == workerId)
                  && identity.subjects.Any(value => value.entityId == penId)
                  && husbandryPersistence.Capture().pendingOutcomes.Count == 0,
                "SLAUGHTER_COMPLETION_IDENTITY_EXACT_JOIN_ONCE", TameResultKey(identity));
            if (failures.Count != 0) yield break;
            string completedJson = gameSaveService.ToJson(gameSaveService.Capture(), true);
            WriteSlaughterCheckpoint("real-capture-slaughter-completed-world.json", completedJson);
            restored = RestoreCircus(null, gameSaveService.FromJson(completedJson));
            Check(restored && CaptureSlaughterChildren(animalId).Single().immutablePayloadHash == child.immutablePayloadHash
                  && outcomeLedger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                      TameResultKey(value) == TameResultKey(identity)
                      && value.immutablePayloadHash == identity.immutablePayloadHash) == 1
                  && itemRuntime.GetAllStacks().Count(value => value.StackId == stack.StackId
                      && value.ItemId == carcassItemId && value.Quantity == 1) == 1
                  && !capture.TryGetCaptured(animalId, out _) && !husbandryQuery.TryGetAnimal(typedId, out _),
                "SLAUGHTER_COMPLETED_OFFICIAL_WORLD_RESTORE", $"success={restored};carcass={stack.StackId}");
            if (!restored || failures.Count != 0) yield break;
            List<DungeonSaveSectionEnvelope> beforeReplay = saveRegistry.CaptureAll();
            WorkCompletedIdentityEvent terminal = AnimalHusbandryCompletionObservation.CreateCompletionEvent(completion);
            WorkCompletionIdentityDeliveryResult replay = actualCompletion.EnsureApplied(
                WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(terminal));
            bool retried = commands.ApplyWork(pen, worker, typedId, AnimalHusbandryWorkKind.Slaughter,
                work.RequiredWork, out bool retryCompleted);
            List<DungeonSaveSectionEnvelope> afterReplay = saveRegistry.CaptureAll();
            Check(replay.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
                  && !retried && !retryCompleted && beforeReplay.Count == afterReplay.Count
                  && beforeReplay.All(before => afterReplay.Any(after => after.sectionId == before.sectionId
                      && after.sectionVersion == before.sectionVersion && after.payloadJson == before.payloadJson)),
                "SLAUGHTER_COMPLETED_REPLAY_ALL_SECTIONS_BYTE_EXACT",
                $"identity={replay.Status};workAccepted={retried};completed={retryCompleted};sections={afterReplay.Count}");
        }
        finally { owner.ConstructDomainFacts(productionFacts); }
        if (failures.Count != 0) yield break;
        bool branchRestored = RestoreCircus(null, gameSaveService.FromJson(branchJson));
        Check(branchRestored && husbandryQuery.TryGetAnimal(typedId, out HusbandryAnimalState branchAnimal)
              && branchAnimal.Tamed && !branchAnimal.SlaughterDesignated
              && CaptureSlaughterChildren(animalId).Length == childrenBefore,
            "SLAUGHTER_TEST_BRANCH_RESTORE_FOR_EXISTING_RELEASE",
            "official-original-tame-world;test-only-branch;trainingEligible=false");
    }

    private GameplayOutcomeSnapshot[] CaptureSlaughterChildren(string animalId) =>
        outcomeLedger.CaptureGameplayOutcomes().exactOutcomes.Where(value =>
            value.outcomeTypeId == MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.AnimalHusbandryWorkResult).OutcomeTypeId.Value
            && TameFact(value, "husbandry-kind") == "slaughter"
            && TameFact(value, "animal-id") == animalId).OrderBy(value => value.sequence).ToArray();

    private static void WriteSlaughterCheckpoint(string name, string json)
    {
        string path = Path.Combine(Path.GetDirectoryName(
            CaptivityWildlifeLifecyclePlayModeVerifier.ReportPath), name);
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false));
        writer.Write(json);
    }

    private sealed class HeldTameCompletionCommand : IWorkCompletionIdentityDeliveryCommand
    {
        private readonly IWorkCompletionIdentityDeliveryCommand actual;
        private readonly string product;
        private readonly string deferredReason;
        public bool Hold = true;
        public int DeferredAttempts;
        public HeldTameCompletionCommand(IWorkCompletionIdentityDeliveryCommand actual, string animalId,
            string workKind = "tame")
        {
            this.actual = actual;
            product = workKind + "|" + animalId;
            deferredReason = "qa-test-only-held-" + workKind + "-identity";
        }
        public WorkCompletionIdentityDeliveryResult EnsureApplied(WorkCompletionIdentityDeliveryRequest request)
        {
            if (Hold && request.WorkId == BuiltInWorkTypeIds.AnimalCare.Value && request.ProductId == product)
            {
                DeferredAttempts++;
                return new(WorkCompletionIdentityDeliveryStatus.Deferred, deferredReason);
            }
            return actual.EnsureApplied(request);
        }
        public bool RetireProducerStream(string producerStreamId) => actual.RetireProducerStream(producerStreamId);
    }

    [Serializable]
    private sealed class TameDeliveryCursorEvidence
    {
        public List<WorkCompletionIdentityDeliveryCursorSaveData> cursors = new();
    }

    [Serializable]
    private sealed class CompletedFeedCheckpointObservation
    {
        public string schema = "dungeonstory-completed-feed-checkpoint@1";
        public string worldSha256;
        public float gameTime;
        public int absoluteDay;
        public string animalId;
        public string penId;
        public string feedItemId;
        public string operationId;
        public string commitId;
        public int consumedQuantity;
        public long consumedMassGrams;
        public int remainingQuantity;
        public float hungerBefore;
        public float hungerAfter;
        public int healthAfter;
        public long feedSequence;
        public bool trainingEligible = false;
        public bool coldProcessResumeExecuted = false;
    }

    private GameplayOutcomeSnapshot[] CaptureTameChildren(string animalId) =>
        outcomeLedger.CaptureGameplayOutcomes().exactOutcomes.Where(value =>
            value.outcomeTypeId == MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.AnimalHusbandryWorkResult).OutcomeTypeId.Value
            && TameFact(value, "husbandry-kind") == "tame"
            && TameFact(value, "animal-id") == animalId).OrderBy(value => value.sequence).ToArray();

    private static string TameFact(GameplayOutcomeSnapshot value, string factId) =>
        value?.facts.FirstOrDefault(fact => fact.factId == factId)?.value ?? string.Empty;

    private static string TameResultKey(GameplayOutcomeSnapshot value) =>
        value == null ? string.Empty : new GameplayResultKey(value.producerId,
            new GameplayOperationId(value.operationId), value.commitRevision,
            value.localResultIndex).ToString();

    private IEnumerator ObserveAnimalCareStart(
        AnimalCareStartObservation observation,
        string resetReason)
    {
        AIBrain brain = worker.Brain;
        AbilityHaul haul = worker.GetComponent<AbilityHaul>();
        AbilityMove move = worker.GetComponent<AbilityMove>();

        // Preparation must be atomic with respect to the production scheduler.
        // Pausing first prevents a just-cancelled haul from being selected again
        // between StopCurrentActionForReplan and the AnimalCare preference.
        worker.SetAiPaused(true);
        brain.StopCurrentActionForReplan(resetReason);
        haul?.StopHauling(resetReason);
        move?.CancelActiveMovement(resetReason);
        workerWork.ClearPriorityWorkTarget();

        observation.Neutralized = TryNeutralizeAnimalCareSubject(
            out string neutralizationDetail);
        observation.NeutralizationDetail = neutralizationDetail;
        if (!observation.Neutralized)
        {
            observation.Detail = "neutralized=false;" + neutralizationDetail;
            yield break;
        }

        float settleDeadline = Time.realtimeSinceStartup + 3f;
        int consecutiveSettledFrames = 0;
        do
        {
            observation.SettleFrames++;
            yield return null;
            CharacterAiRuntimeGateSnapshot settleGate =
                brain.CaptureRuntimeGateSnapshot();
            bool cleanFrame = brain.bestAction == null
                && !brain.IsExternallyDrivenActionActive
                && haul?.IsHauling != true
                && !workerWork.isWorking
                && !workerWork.HasActiveWorkRoutineForDiagnostics
                && move?.HasActiveMovementRoutineForDiagnostics != true
                && settleGate.LivePathRequests == 0
                && settleGate.LiveReservations == 0;
            consecutiveSettledFrames = cleanFrame
                ? consecutiveSettledFrames + 1
                : 0;
            observation.SettleConsecutiveFrames =
                consecutiveSettledFrames;
            observation.Settled = consecutiveSettledFrames >= 2;
        }
        while (!observation.Settled
               && Time.realtimeSinceStartup < settleDeadline);

        if (!observation.Settled)
        {
            CharacterAiRuntimeGateSnapshot unsettledGate =
                brain.CaptureRuntimeGateSnapshot();
            observation.Detail =
                $"settled=false;settleFrames={observation.SettleFrames};"
                + $"consecutive={observation.SettleConsecutiveFrames}/2;"
                + $"action={brain.bestAction?.actionset?.GetType().Name};"
                + $"external={brain.IsExternallyDrivenActionActive};"
                + $"hauling={haul?.IsHauling};working={workerWork.isWorking};"
                + $"workRoutine={workerWork.HasActiveWorkRoutineForDiagnostics};"
                + $"moveRoutine={move?.HasActiveMovementRoutineForDiagnostics};"
                + $"paths={unsettledGate.LivePathRequests};"
                + $"reservations={unsettledGate.LiveReservations};"
                + $"phase={brain.CurrentActionPhase};failure={brain.LastActionFailure}";
            yield break;
        }

        GridPathSearchResult prioritySearch = grid.SearchPath(worker.GetNowXY());
        observation.PriorityAccepted = workerWork.TrySetPriorityWorkTarget(
            pen,
            BuiltInWorkTypeIds.AnimalCare,
            prioritySearch,
            out string priorityFailure);
        if (!observation.PriorityAccepted)
        {
            WorkTargetCandidate firstRejected =
                workerWork.LastRejectedWorkCandidate;
            // Exterior/environment warnings use the same two-step public
            // confirmation handshake as the player priority-work command UI.
            observation.PriorityAccepted = workerWork.TrySetPriorityWorkTarget(
                pen,
                BuiltInWorkTypeIds.AnimalCare,
                prioritySearch,
                out string confirmedFailure);
            observation.PriorityDetail = observation.PriorityAccepted
                ? "confirmed-after-warning:" + priorityFailure
                    + ";firstRejected=" + firstRejected.FailureReason
                : "rejected:first=" + priorityFailure
                    + ";firstRejected=" + firstRejected.FailureReason
                    + ";confirmed=" + confirmedFailure
                    + ";confirmedRejected="
                    + workerWork.LastRejectedWorkCandidate.FailureReason;
        }
        else
        {
            observation.PriorityDetail = "accepted";
        }

        if (!observation.PriorityAccepted)
        {
            observation.Detail =
                $"settled=true;priorityAccepted=false;"
                + $"priority={observation.PriorityDetail};"
                + $"pen={pen?.RequirePersistentInstanceId().Value};"
                + $"actor={worker.GetNowXY()};phase={brain.CurrentActionPhase};"
                + $"failure={brain.LastActionFailure}";
            yield break;
        }

        observation.PriorityTargetMatches =
            workerWork.PriorityWorkTarget == pen;
        observation.PriorityWorkTypeMatches =
            workerWork.PriorityWorkTypeId == BuiltInWorkTypeIds.AnimalCare;
        observation.UrgentPriority = workerWork.HasUrgentPriorityWork;
        // Register the same production preference that the upcoming scheduler
        // decision will consume before sampling AIWork.  This keeps the
        // diagnostic on the exact AnimalCare path rather than the generic
        // fallback action.
        observation.Preferred = brain.PreferWorkActionOnNextDecision(
            BuiltInWorkTypeIds.AnimalCare,
            120f);
        AIAction haulCandidate = brain.availableActions?.FirstOrDefault(
            candidate => candidate?.actionset is AIHaul);
        observation.AutonomousHaulCanStart =
            haulCandidate?.actionset?.CanStart(worker) == true;
        AIAction aiWorkCandidate = brain.availableActions?.FirstOrDefault(
            candidate => candidate?.actionset is AIWork);
        WorkDutyStartDiagnostics workStartGates =
            workerWork.CaptureWorkStartDiagnostics();
        observation.AbilityCanStartAny =
            workStartGates.CanStartByDutyGate;
        observation.AbilityCanStartAnimalCare =
            workStartGates.CanStartByDutyGate
            && observation.PriorityTargetMatches
            && observation.PriorityWorkTypeMatches;
        observation.DutyState = workStartGates.DutyState;
        observation.RoutineNeedBlocked = workStartGates.RoutineNeedBlocked;
        observation.RestProtection = workStartGates.RestProtectionBlocked;
        observation.DiscontentBlocked = workStartGates.DiscontentBlocked;
        observation.DiscontentReason = workStartGates.DiscontentReason;
        observation.ConditionWouldTakeOffDuty =
            workStartGates.ConditionWouldTakeOffDuty;
        observation.DutyGateCanStart = workStartGates.CanStartByDutyGate;
        observation.PriorityArbitrationDetail =
            $"targetMatch={observation.PriorityTargetMatches};"
            + $"workTypeMatch={observation.PriorityWorkTypeMatches};"
            + $"priorityType={workerWork.PriorityWorkTypeId.Value};"
            + $"urgent={observation.UrgentPriority};"
            + $"haulCanStart={observation.AutonomousHaulCanStart};"
            + $"abilityAny={observation.AbilityCanStartAny};"
            + $"abilityAnimalCare={observation.AbilityCanStartAnimalCare};"
            + $"duty={observation.DutyState};"
            + $"routineNeedBlock={observation.RoutineNeedBlocked};"
            + $"restProtection={observation.RestProtection};"
            + $"discontent={observation.DiscontentBlocked}:"
            + $"{observation.DiscontentReason};"
            + $"conditionOffDuty={observation.ConditionWouldTakeOffDuty};"
            + $"dutyGateCanStart={observation.DutyGateCanStart};"
            + $"haulPriority={workerWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul)};"
            + $"target={workerWork.PriorityWorkTarget?.name};"
            + $"pen={pen?.name}";

        CharacterAiRuntimeDiagnosticsSnapshot before =
            brain.CaptureRuntimeDiagnostics();
        long startEpoch = brain.RuntimeActionEpoch;
        int wakeRequestsBefore = brain.ImmediateDecisionRequestCount;
        long committedDeferralsBefore =
            brain.RuntimeCommittedPathSearchDeferralCount;
        long preferredDeferralsBefore =
            brain.RuntimePreferredActionDeferredCount;
        long preferredFallbackSuppressionsBefore =
            brain.RuntimePreferredActionDeferredFallbackSuppressionCount;
        long preferredCommitsBefore =
            brain.RuntimePreferredActionCommitCount;
        long preferredHardFailuresBefore =
            brain.RuntimePreferredActionHardFailureCount;
        long preferredDispositionRevisionBefore =
            brain.RuntimePreferredActionDispositionRevision;

        // Let the production scheduler own path-budget rollover. Calling the
        // decision tree directly every frame runs outside that ownership and
        // can indefinitely starve an otherwise valid Deferred AIWork action.
        // Register while paused, then publish the actor and wake exactly one
        // official scheduler decision. No other action can occupy this gap.
        worker.SetAiPaused(false);
        brain.RequestImmediateReplan(clearFailures: true);

        float deadline = Time.realtimeSinceStartup + 12f;
        long lastObservedEpoch = startEpoch;
        while (Time.realtimeSinceStartup < deadline)
        {
            observation.SchedulerFrames++;
            AIAction current = brain.bestAction;
            long currentEpoch = brain.RuntimeActionEpoch;
            if (currentEpoch != lastObservedEpoch)
            {
                string actionType = current?.actionset?.GetType().Name
                    ?? "<none>";
                bool targetEpoch = current?.actionset is AIWork
                    && workerWork.AssignedWorkTypeId
                        == BuiltInWorkTypeIds.AnimalCare;
                observation.EpochTransitionCount++;
                if (!targetEpoch)
                {
                    observation.UnexpectedIntermediateEpochCount++;
                }
                observation.EpochTransitions +=
                    $"[{lastObservedEpoch}->{currentEpoch}:"
                    + $"{actionType}:assigned="
                    + $"{workerWork.AssignedWorkTypeId.Value}]";
                lastObservedEpoch = currentEpoch;
            }
            // The catalog intentionally contains one generic AIWork action.
            // Its serialized WorkTypeId is empty; production resolves the
            // preferred type through AIBrain, so exactness lives on the
            // assigned AbilityWork state rather than that asset field.
            bool exactAnimalCare = current?.actionset is AIWork
                && workerWork.AssignedWorkTypeId
                    == BuiltInWorkTypeIds.AnimalCare;
            if (observation.Preferred
                && exactAnimalCare
                && workerWork.IsAssignedWork(BuiltInWorkTypeIds.AnimalCare)
                && brain.HasRunningWorkAction)
            {
                observation.Started = true;
                observation.Epoch = brain.RuntimeActionEpoch;
                break;
            }
            yield return null;
        }

        // Read the production evaluator's published result.  This is a
        // non-mutating observation of AIWork.CanStart/AdjustScore; invoking
        // those methods again from a verifier would alter duty/candidate
        // caches and could change the decision being measured.
        observation.AiWorkAdjustedScore = aiWorkCandidate?.score ?? 0f;
        observation.PreferredDeferredCount =
            brain.RuntimePreferredActionDeferredCount
            - preferredDeferralsBefore;
        observation.PreferredDeferredFallbackSuppressions =
            brain.RuntimePreferredActionDeferredFallbackSuppressionCount
            - preferredFallbackSuppressionsBefore;
        observation.PreferredDeferredFailure =
            brain.LastPreferredActionDeferredFailure.ToString();
        observation.PreferredCommitCount =
            brain.RuntimePreferredActionCommitCount
            - preferredCommitsBefore;
        observation.PreferredHardFailureCount =
            brain.RuntimePreferredActionHardFailureCount
            - preferredHardFailuresBefore;
        observation.FirstPreferredHardFailureKind =
            brain.FirstPreferredActionHardFailure.Kind;
        observation.FirstPreferredHardFailure =
            brain.FirstPreferredActionHardFailure.ToString();
        observation.FirstPreferredHardFailureSource =
            brain.FirstPreferredActionHardFailureSource;
        observation.PreferredDisposition =
            brain.RuntimePreferredActionDisposition;
        observation.PreferredDispositionBranch =
            brain.RuntimePreferredActionDispositionBranch;
        observation.PreferredDispositionTransitions =
            brain.RuntimePreferredActionDispositionRevision
            - preferredDispositionRevisionBefore;
        string aiWorkLabel = aiWorkCandidate?.actionset?.GetDisplayLabel()
            ?? string.Empty;
        IReadOnlyList<AIActionDebugCandidate> evaluated =
            brain.LastCandidateScores;
        for (int index = evaluated.Count - 1; index >= 0; index--)
        {
            AIActionDebugCandidate candidate = evaluated[index];
            if (!string.Equals(
                    candidate.ActionLabel,
                    aiWorkLabel,
                    StringComparison.Ordinal))
            {
                continue;
            }

            observation.AiWorkEvaluationObserved = true;
            observation.AiWorkCanStart = !candidate.Failure.HasFailure;
            observation.AiWorkFailure = candidate.Failure.ToString();
            break;
        }
        if (observation.PreferredCommitCount > 0
            && observation.Started)
        {
            observation.AiWorkEvaluationObserved = true;
            observation.AiWorkCanStart = true;
        }
        observation.PriorityArbitrationDetail +=
            $";aiWorkEvaluated={observation.AiWorkEvaluationObserved}"
            + $";aiWorkCanStart={observation.AiWorkCanStart}"
            + $";aiWorkAdjusted={observation.AiWorkAdjustedScore:0.###}"
            + $";aiWorkFailure={observation.AiWorkFailure}"
            + $";preferredDeferred={observation.PreferredDeferredCount}"
            + $";preferredFallbackSuppressed="
            + $"{observation.PreferredDeferredFallbackSuppressions}"
            + $";preferredCommits={observation.PreferredCommitCount}"
            + $";preferredHardFailures="
            + $"{observation.PreferredHardFailureCount}"
            + $";firstPreferredHardFailure="
            + $"{observation.FirstPreferredHardFailureSource}:"
            + $"{observation.FirstPreferredHardFailureKind}:"
            + $"{observation.FirstPreferredHardFailure}"
            + $";preferredDisposition={observation.PreferredDisposition}:"
            + $"{observation.PreferredDispositionBranch}"
            + $";preferredDispositionTransitions="
            + $"{observation.PreferredDispositionTransitions}"
            + $";preferredDeferredFailure="
            + $"{observation.PreferredDeferredFailure}";

        CharacterAiRuntimeDiagnosticsSnapshot after =
            brain.CaptureRuntimeDiagnostics();
        CharacterAiRuntimeGateSnapshot beforeGate = before.Gate;
        CharacterAiRuntimeGateSnapshot afterGate = after.Gate;
        observation.Detail =
            $"settled={observation.Settled};settleFrames={observation.SettleFrames};"
            + $"settleConsecutive={observation.SettleConsecutiveFrames}/2;"
            + $"priorityAccepted={observation.PriorityAccepted};"
            + $"priority={observation.PriorityDetail};"
            + $"priorityArbitration={observation.PriorityArbitrationDetail};"
            + $"preferred={observation.Preferred};started={observation.Started};"
            + $"epoch={startEpoch}->{brain.RuntimeActionEpoch};"
            + $"startedEpoch={observation.Epoch};"
            + $"epochTransitions={observation.EpochTransitionCount}:"
            + $"{observation.EpochTransitions};"
            + $"schedulerFrames={observation.SchedulerFrames};"
            + $"wakeRequests={wakeRequestsBefore}->{brain.ImmediateDecisionRequestCount};"
            + $"schedulerProcesses={beforeGate.SchedulerProcesses}->{afterGate.SchedulerProcesses};"
            + $"retrySchedules={beforeGate.RetrySchedules}->{afterGate.RetrySchedules};"
            + $"retryAttempts={beforeGate.RetryAttempts}->{afterGate.RetryAttempts};"
            + $"pathRequests={beforeGate.PathRequests}->{afterGate.PathRequests};"
            + $"pathResults={beforeGate.PathResults}->{afterGate.PathResults};"
            + $"committedDeferrals={committedDeferralsBefore}"
            + $"->{brain.RuntimeCommittedPathSearchDeferralCount};"
            + $"pathDeferred={brain.IsPathSearchDeferred};"
            + $"committedPathDeferred={brain.IsCommittedPathSearchDeferred};"
            + $"brokerSearches={pathSearchBroker?.SearchesThisFrame ?? -1};"
            + $"brokerDeferrals={pathSearchBroker?.BudgetDeferralsThisFrame ?? -1};"
            + $"preferredNow={brain.IsActionPreferredForNextDecision<AIWork>()};"
            + $"action={brain.bestAction?.actionset?.GetType().Name};"
            + $"assigned={workerWork.AssignedWorkTypeId.Value};"
            + $"phase={brain.CurrentActionPhase};"
            + $"phaseDetail={brain.CurrentActionPhaseDetail};"
            + $"failure={brain.LastActionFailure}";
    }

    private bool TryNeutralizeAnimalCareSubject(out string detail,
        bool resetDeprivation = true)
    {
        CharacterStats stats = worker?.Stats;
        if (stats == null || deprivationRuntime == null)
        {
            detail = $"stats={stats != null};deprivation={deprivationRuntime != null}";
            return false;
        }

        Dictionary<CharacterCondition, float> restoredStats =
            stats.StatSnapshot.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
        restoredStats[CharacterCondition.HUNGER] = 100f;
        restoredStats[CharacterCondition.THIRST] = 100f;
        restoredStats[CharacterCondition.SLEEP] = 100f;
        restoredStats[CharacterCondition.FUN] = 100f;
        restoredStats[CharacterCondition.MOOD] = 100f;
        restoredStats[CharacterCondition.EXCRETION] = 100f;
        restoredStats[CharacterCondition.HYGIENE] = 100f;
        stats.RestorePersistentState(
            restoredStats,
            worker.CurrentHealth,
            worker.InjurySeverity,
            100f,
            Array.Empty<CharacterMoodFactorSnapshot>());

        bool deprivationReady = !resetDeprivation || deprivationRuntime
            .DebugResetForDeterministicScenario(worker);
        bool routineDrink = deprivationRuntime.NeedsRoutineDrink(
            worker,
            out string routineDrinkReason);
        bool drinkRunnerActive = deprivationRuntime
            .IsRoutineDrinkActionActive(worker);
        bool hungerEmergency = CharacterNeedAiThresholds
            .IsEmergencyOrImminentPhysicalHarm(
                worker,
                CharacterCondition.HUNGER);
        bool thirstEmergency = CharacterNeedAiThresholds
            .IsEmergencyOrImminentPhysicalHarm(
                worker,
                CharacterCondition.THIRST);
        float effectiveMood = CharacterMoodImpulseUtility.GetMood01(worker);
        bool neutral = deprivationReady
            && !routineDrink
            && !drinkRunnerActive
            && !hungerEmergency
            && !thirstEmergency
            && effectiveMood >= 0.9f;

        detail = $"resetRequested={resetDeprivation};deprivationReady={deprivationReady};"
            + $"needs={DescribeAnimalCareNeeds(stats)};"
            + $"routineDrink={routineDrink}:{routineDrinkReason};"
            + $"drinkRunner={drinkRunnerActive};"
            + $"hungerEmergency={hungerEmergency};"
            + $"thirstEmergency={thirstEmergency};"
            + $"effectiveMood={effectiveMood:0.###};"
            + $"paused={worker.IsAiPaused()};"
            + $"action={worker.Brain?.bestAction?.actionset?.GetType().Name ?? "<none>"}";
        return neutral;
    }

    private static string DescribeAnimalCareNeeds(CharacterStats stats)
    {
        if (stats == null)
        {
            return "missing";
        }

        CharacterCondition[] conditions =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.THIRST,
            CharacterCondition.SLEEP,
            CharacterCondition.FUN,
            CharacterCondition.MOOD,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE
        };
        return string.Join(",", conditions.Select(condition =>
            stats.TryGetConditionValue(condition, out float value)
                ? $"{condition}={value:0.###}"
                : $"{condition}=missing"));
    }

    private static CharacterBodyHealthSnapshot CloneBodyHealthSnapshot(
        CharacterBodyHealthSnapshot source) =>
        new(
            source.Parts.Select(CloneBodyPart).ToArray(),
            source.BloodLoss,
            source.Suppression,
            source.Consciousness,
            source.Manipulation,
            source.Mobility,
            source.Downed);

    private static CharacterBodyPartHealthState CloneBodyPart(
        CharacterBodyPartHealthState source) =>
        source == null
            ? null
            : new CharacterBodyPartHealthState
            {
                bodyPart = source.bodyPart,
                maxHealth = source.maxHealth,
                currentHealth = source.currentHealth,
                bleedingPerSecond = source.bleedingPerSecond
            };

    private float CalculateBoundedAnimalCareObservationSeconds(
        AIAction action,
        Vector3 fallbackTargetWorld)
    {
        float worldDistance = 0f;
        int pathStepCount = action?.pathSteps?.Count ?? 0;
        Vector3 previous = worker.transform.position;
        if (pathStepCount > 0)
        {
            for (int index = 0; index < pathStepCount; index++)
            {
                Vector3 next = grid.GetWorldPos(action.pathSteps[index].To);
                worldDistance += Vector3.Distance(previous, next);
                previous = next;
            }
        }
        else
        {
            worldDistance = Vector3.Distance(previous, fallbackTargetWorld);
        }

        float gameSeconds = worldDistance
            / Mathf.Max(0.1f, worker.GetMoveSpeed());
        float realSeconds = gameSeconds / Mathf.Max(0.01f, Time.timeScale);
        float observationSeconds = 4f + realSeconds * 2f
            + pathStepCount * 0.1f;
        return Mathf.Clamp(observationSeconds, 8f, 30f);
    }

    private sealed class AnimalCareStartObservation
    {
        public bool Preferred;
        public bool Started;
        public bool Settled;
        public bool Neutralized;
        public bool PriorityAccepted;
        public bool PriorityTargetMatches;
        public bool PriorityWorkTypeMatches;
        public bool UrgentPriority;
        public bool AutonomousHaulCanStart;
        public bool AiWorkCanStart;
        public bool AbilityCanStartAny;
        public bool AbilityCanStartAnimalCare;
        public bool AiWorkEvaluationObserved;
        public bool RoutineNeedBlocked;
        public bool RestProtection;
        public bool DiscontentBlocked;
        public bool ConditionWouldTakeOffDuty;
        public bool DutyGateCanStart;
        public float AiWorkAdjustedScore;
        public AbilityWork.DutyState DutyState;
        public string DiscontentReason = string.Empty;
        public string AiWorkFailure = string.Empty;
        public string PreferredDeferredFailure = string.Empty;
        public string FirstPreferredHardFailure = string.Empty;
        public int SettleFrames;
        public int SettleConsecutiveFrames;
        public int SchedulerFrames;
        public int EpochTransitionCount;
        public int UnexpectedIntermediateEpochCount;
        public long Epoch;
        public long PreferredDeferredCount;
        public long PreferredDeferredFallbackSuppressions;
        public long PreferredCommitCount;
        public long PreferredHardFailureCount;
        public long PreferredDispositionTransitions;
        public CharacterAiPreferredActionDisposition PreferredDisposition;
        public CharacterAiPreferredActionFailureSource
            FirstPreferredHardFailureSource;
        public AIActionFailureKind FirstPreferredHardFailureKind;
        public CharacterAiBranch PreferredDispositionBranch;
        public string EpochTransitions = string.Empty;
        public string PriorityDetail = string.Empty;
        public string PriorityArbitrationDetail = string.Empty;
        public string Detail = string.Empty;
        public string NeutralizationDetail = string.Empty;
    }

    private sealed class PickupDeliveryPreflightProbe
    {
        public bool Ready;
        public Vector2Int ApproachStart;
        public Vector2Int PickupStand;
        public Vector2Int DeliveryStand;
        public int PathStepCount;
        public float PathWorldDistance;
        public string Detail = string.Empty;
    }

    private sealed class TransportTerminalObservation
    {
        public bool Penned;
        public bool Stalled;
        public bool AwaitingTransportObserved;
        public bool PickupApproachProgressObserved;
        public bool PickupStandReached;
        public bool TransportingObserved;
        public float ObservationSeconds;
        public float AllowedSeconds;
        public float SecondsSinceProgress;
        public Vector3 LastProgressWorldPosition;
        public Vector2Int LastProgressGridPosition;
        public int ProgressSamples;
    }

    private sealed class TransportFixturePlanProbe
    {
        public bool Ready;
        public Vector2Int SourceCell;
        public Vector2Int PickupStand;
        public Vector2Int DeliveryStand;
        public string Detail = string.Empty;
    }

    private sealed class TransportChaosStartProbe
    {
        public bool Live;
        public WildlifeActor Animal;
        public Transform OriginalParent;
        public Vector2Int Source;
        public CapturedWildlifeState State;
        public AbilityWildlifeCaptureTransport Transport;
        public AbilityMove Move;
        public string ExpectedOwner = string.Empty;
        public int TerminalBefore;
        public int PathStepCount;
        public float PathWorldDistance;
    }

    private readonly struct ActorAiPauseState
    {
        public ActorAiPauseState(CharacterActor actor, bool wasPaused)
        {
            Actor = actor;
            WasPaused = wasPaused;
        }

        public CharacterActor Actor { get; }
        public bool WasPaused { get; }
    }

    private bool CreateAuthoredRoom()
    {
        const int roomWidth = 10;
        const int roomHeight = 3;
        fixtureStage = "capture-supported-baseline-layout";
        if (!DungeonSpaceGridLayout.TryCapture(
                grid,
                out DungeonInteriorLayoutSnapshot baselineLayout,
                out string baselineLayoutFailure))
        {
            fixtureStage = "baseline-layout-invalid:" + baselineLayoutFailure;
            return false;
        }
        if (baselineLayout.ColumnCount != DungeonSpaceExpansionCatalog.InitialInteriorColumns
            && !DungeonSpaceExpansionCatalog.All.Any(definition =>
                definition.TargetInteriorColumns == baselineLayout.ColumnCount))
        {
            fixtureStage = $"baseline-layout-unsupported:{baselineLayout.ColumnCount}";
            return false;
        }
        fixtureStage = "load-authored-assets";
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        BuildingSO stair = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Stair.asset");
        BuildingSO housingAsset = FindBuildingAsset(data =>
            data.GetCaptiveHousingAbility()?.IsValid == true);
        BuildingSO penAsset = FindBuildingAsset(data =>
            data.GetBeastPenAbility()?.IsValid == true);
        if (hallway == null || wall == null || door == null || stair == null
            || housingAsset == null || penAsset == null)
        {
            fixtureStage = $"authored-assets-missing:hallway={hallway != null};wall={wall != null};door={door != null};stair={stair != null};housing={housingAsset != null};pen={penAsset != null}";
            return false;
        }
        if (grid.width < roomWidth + 4 || grid.height < roomHeight)
        {
            fixtureStage =
                $"grid-too-small:grid={grid.width}x{grid.height};required={roomWidth + 4}x{roomHeight}";
            return false;
        }

        int start = -1;
        const int row = 0;
        HashSet<BuildableObject> selectedDisplacements = null;
        HashSet<WildlifeActor> selectedWildlifeDisplacements = null;
        int selectedWildlifeCount = int.MaxValue;
        int selectedMovementCount = int.MaxValue;
        string lastRejectedCell = "none";
        fixtureStage = "find-clear-footprint";
        for (int x = 1; x <= grid.width - roomWidth - 4; x++)
        {
            // Stay inside the existing supported interior: an arbitrary clear
            // exterior span invents an unsupported width, not a real expansion.
            IReadOnlyList<Vector2Int> candidateStairCells = stair.GetGridPosList(
                new Vector2Int(x + roomWidth + 1, row));
            if (x < baselineLayout.StartX
                || x + roomWidth > baselineLayout.EndExclusiveX
                || candidateStairCells.Any(position =>
                    position.x < baselineLayout.StartX
                    || position.x >= baselineLayout.EndExclusiveX))
            {
                continue;
            }
            bool clear = true;
            HashSet<BuildableObject> candidates = new();
            HashSet<WildlifeActor> wildlifeCandidates = new();
            for (int dy = 0; dy < roomHeight && clear; dy++)
            for (int dx = 0; dx < roomWidth && clear; dx++)
            {
                Vector2Int position = new(x + dx, row + dy);
                GridCell cell = grid.GetGridCell(position);
                if (cell == null)
                {
                    clear = false;
                    lastRejectedCell = $"missing:{position}";
                    break;
                }
                foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                {
                    IGridOccupant occupant = cell.GetOccupant(layer);
                    if (occupant == null)
                        continue;
                    if (layer == GridLayer.Hallway
                        && CanReuseFixtureHallway(occupant, position))
                    {
                        continue;
                    }
                    // Wildlife owns a real production grid layer and can cover
                    // every legal 10x3 span on the authored 60x3 map.  It is
                    // safe to remove only the animals inside the chosen bounded
                    // fixture span because the full V18 baseline was captured
                    // before this scan and is restored atomically in Cleanup.
                    // Actors and unknown occupants remain non-displaceable.
                    // Only original buildings wholly inside this test footprint
                    // can be retired by their real owner and restored from V18.
                    if (layer == GridLayer.Wildlife
                        && occupant is WildlifeActor wildlifeActor
                        && wildlifeActor.IsAlive)
                    {
                        wildlifeCandidates.Add(wildlifeActor);
                        continue;
                    }
                    if (!CanRetireFixtureBuilding(occupant, layer,
                            x, roomWidth + 3, roomHeight,
                            out BuildableObject originalBuilding))
                    {
                        clear = false;
                        lastRejectedCell =
                            $"hard-occupant:{position}:layer={layer}:type={occupant.GetType().Name}";
                        break;
                    }
                    candidates.Add(originalBuilding);
                }
            }
            if (clear)
            {
                Vector2Int connector = new(x + roomWidth, row + 1);
                Vector2Int exteriorAnchor = new(x + roomWidth + 3, row);
                GridCell anchorCell = grid.GetGridCell(exteriorAnchor);
                if (anchorCell == null
                    || anchorCell.AreaType != GridCellAreaType.ExteriorPath
                    || !anchorCell.IsWalkableArea
                    || !grid.IsWalkable(exteriorAnchor))
                {
                    clear = false;
                    lastRejectedCell =
                        $"escape-anchor-invalid:{exteriorAnchor}:area={anchorCell?.AreaType}";
                }

                Vector2Int[] escapeCells =
                {
                    connector,
                    new Vector2Int(x + roomWidth + 1, row + 1),
                    new Vector2Int(x + roomWidth + 2, row + 1),
                    new Vector2Int(x + roomWidth, row),
                    new Vector2Int(x + roomWidth + 1, row),
                    new Vector2Int(x + roomWidth + 2, row),
                    exteriorAnchor
                };
                foreach (Vector2Int position in escapeCells)
                {
                    if (!clear) break;
                    GridCell cell = grid.GetGridCell(position);
                    if (cell == null)
                    {
                        clear = false;
                        lastRejectedCell = $"escape-cell-missing:{position}";
                        break;
                    }
                    foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                    {
                        IGridOccupant occupant = cell.GetOccupant(layer);
                        if (occupant == null) continue;
                        if (layer == GridLayer.Hallway
                            && CanReuseFixtureHallway(occupant, position))
                        {
                            continue;
                        }
                        if (layer == GridLayer.Wildlife
                            && occupant is WildlifeActor wildlifeActor
                            && wildlifeActor.IsAlive)
                        {
                            wildlifeCandidates.Add(wildlifeActor);
                            continue;
                        }
                        if (!CanRetireFixtureBuilding(occupant, layer,
                                x, roomWidth + 3, roomHeight,
                                out BuildableObject originalBuilding))
                        {
                            clear = false;
                            lastRejectedCell =
                                $"escape-hard-occupant:{position}:layer={layer}:type={occupant.GetType().Name}";
                            break;
                        }
                        candidates.Add(originalBuilding);
                    }
                }
            }
            if (clear
                && (wildlifeCandidates.Count < selectedWildlifeCount
                    || wildlifeCandidates.Count == selectedWildlifeCount
                    && candidates.Count < selectedMovementCount))
            {
                start = x;
                selectedDisplacements = candidates;
                selectedWildlifeDisplacements = wildlifeCandidates;
                selectedWildlifeCount = wildlifeCandidates.Count;
                selectedMovementCount = candidates.Count;
            }
        }
        if (start < 0)
        {
            fixtureStage =
                $"clear-footprint-missing:grid={grid.width}x{grid.height};required={roomWidth}x{roomHeight};last={lastRejectedCell}";
            return false;
        }

        escapeConnectorCell = new Vector2Int(start + roomWidth, row + 1);
        escapeExteriorAnchorCell = new Vector2Int(start + roomWidth + 3, row);

        fixtureStage = "retire-bounded-original-buildings";
        foreach (BuildableObject building in selectedDisplacements
                     .OrderBy(value => value.centerPos.y)
                     .ThenBy(value => value.centerPos.x)
                     .ThenBy(value => value.RequirePersistentInstanceId().Value,
                         StringComparer.Ordinal))
        {
            string id = building.RequirePersistentInstanceId().Value;
            ModularFacilityBuildingSaveData saved = baselineFacilityWorld.buildings
                .Single(value => value.persistentInstanceId == id);
            PreparedOriginalBuildingSnapshot snapshot = new(
                building, saved, building.buildPoses.ToArray());
            // Keep the receipt before calling the real owner: subscriber errors
            // still leave cleanup responsible for restoring the original world.
            preparedOriginalBuildings.Add(snapshot);
            building.DestroySelf();
            bool removed = building.IsBuildingDestroyed
                && !world.Buildings.Any(value => ReferenceEquals(value, building))
                && snapshot.Positions.All(position => !ReferenceEquals(
                    grid.GetGridCell(position)?.GetOccupant(saved.layer), building));
            Check(removed, "FIXTURE_ORIGINAL_BUILDING_RETIRED",
                $"id={id};definition={saved.buildingId};anchor={building.centerPos};"
                + $"cells={snapshot.Positions.Count};removed={removed};"
                + "owner=DestroySelf;bounded-test-preparation;trainingEligible=false");
            if (!removed)
            {
                fixtureStage = "original-building-retirement-failed:" + id;
                return false;
            }
        }

        fixtureStage = "displace-bounded-wildlife";
        foreach (WildlifeActor wildlifeActor in selectedWildlifeDisplacements
                     .OrderBy(value => value.GridPosition.y)
                     .ThenBy(value => value.GridPosition.x)
                     .ThenBy(value => value.WildlifeId, StringComparer.Ordinal))
        {
            DisplacedWildlifeSnapshot snapshot = new(
                wildlifeActor.WildlifeId,
                wildlifeActor.SpeciesId,
                wildlifeActor.GridPosition);
            if (!wildlife.TryRemoveArrival(snapshot.WildlifeId))
            {
                fixtureStage =
                    $"wildlife-displacement-failed:{snapshot.WildlifeId}:{snapshot.Position}";
                return false;
            }
            displacedWildlife.Add(snapshot);
            if (wildlife.Wildlife.Any(value => value != null
                    && string.Equals(
                        value.WildlifeId,
                        snapshot.WildlifeId,
                        StringComparison.Ordinal))
                || ReferenceEquals(
                    grid.GetGridCell(snapshot.Position)?.GetOccupant(
                        GridLayer.Wildlife),
                    wildlifeActor))
            {
                fixtureStage =
                    $"wildlife-displacement-verification-failed:{snapshot.WildlifeId}:{snapshot.Position}";
                return false;
            }
        }

        for (int dy = 0; dy < roomHeight; dy++)
        for (int dx = 0; dx < roomWidth; dx++)
        {
            Vector2Int position = new(start + dx, row + dy);
            GridCell cell = grid.GetGridCell(position);
            areaSnapshots.Add(new AreaSnapshot(position, cell.AreaType));
            grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
            if (cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                fixtureStage =
                    $"area-mutation-failed:{position}:actual={cell.AreaType}";
                return false;
            }
        }

        Vector2Int stairAnchor = new(start + roomWidth + 1, row);
        IReadOnlyList<Vector2Int> stairFootprint = stair.GetGridPosList(stairAnchor);
        int stairFirstColumn = stairFootprint.Min(position => position.x);
        int stairLastColumn = stairFootprint.Max(position => position.x);
        // The authored stair is 3x2. Its entire columns, not only its two
        // landing rows, must extend the contiguous saved interior. Keeping an
        // upper landing Exterior also lets escape finish before interruption.
        for (int x = stairFirstColumn; x <= stairLastColumn; x++)
        for (int y = 0; y < grid.height; y++)
        {
            Vector2Int position = new(x, y);
            GridCell cell = grid.GetGridCell(position);
            if (cell == null)
            {
                fixtureStage = $"escape-stair-cell-missing:{position}";
                return false;
            }
            if (!areaSnapshots.Any(snapshot => snapshot.Position == position))
                areaSnapshots.Add(new AreaSnapshot(position, cell.AreaType));
            grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
            if (cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                fixtureStage =
                    $"escape-stair-area-mutation-failed:{position}:actual={cell.AreaType}";
                return false;
            }
        }

        // Match DungeonStoryGridBuildingController.ConfigurePlacedBuilding:
        // production placement injects the concrete building component.
        GridBuildingFactory factory = new(created =>
            scope.Container.Inject(created));
        fixtureStage = "place-perimeter";
        for (int dx = 0; dx < roomWidth; dx++)
        {
            if (Place(factory, wall, new Vector2Int(start + dx, row)) == null
                || Place(factory, wall, new Vector2Int(start + dx, row + roomHeight - 1)) == null)
            {
                fixtureStage = $"perimeter-placement-failed:offset={dx};{placementFailure}";
                return false;
            }
        }
        Door cellDoor = Place(
            factory,
            door,
            new Vector2Int(start + roomWidth - 1, row + 1)) as Door;
        if (cellDoor == null
            || Place(factory, wall, new Vector2Int(start, row + 1)) == null)
        {
            fixtureStage = $"endpoint-placement-failed:{placementFailure}";
            return false;
        }
        IDoorAccessCommandService doorCommands =
            scope.Container.Resolve<IDoorAccessCommandService>();
        if (!doorCommands.ApplyPreset(cellDoor, DoorAccessPreset.Cell))
        {
            fixtureStage = "cell-door-preset-failed";
            return false;
        }
        Stair escapeStair = Place(factory, stair, stairAnchor) as Stair;
        if (escapeStair == null)
        {
            fixtureStage = "escape-stair-placement-failed:" + placementFailure;
            return false;
        }
        // Exterior begins beyond the complete stair footprint. The lower
        // landing stays Interior and connects to a real ExteriorPath exit;
        // the production path broker and stair traversal own the departure.
        for (int x = stairLastColumn + 1; x <= escapeExteriorAnchorCell.x; x++)
        {
            Vector2Int position = new(x, row);
            GridCell cell = grid.GetGridCell(position);
            if (cell == null)
            {
                fixtureStage = $"escape-exterior-cell-missing:{position}";
                return false;
            }
            if (!areaSnapshots.Any(snapshot => snapshot.Position == position))
                areaSnapshots.Add(new AreaSnapshot(position, cell.AreaType));
            grid.SetAreaType(position, GridCellAreaType.ExteriorPath);
            if (cell.AreaType != GridCellAreaType.ExteriorPath)
            {
                fixtureStage =
                    $"escape-exterior-area-mutation-failed:{position}:actual={cell.AreaType}";
                return false;
            }
        }
        fixtureStage = "place-interior-hallway";
        int reusedInteriorHallways = 0;
        for (int dx = 1; dx < roomWidth - 1; dx++)
        {
            Vector2Int position = new(start + dx, row + 1);
            if (CanReuseFixtureHallway(
                    grid.GetGridCell(position)?.GetOccupant(GridLayer.Hallway),
                    position))
            {
                reusedInteriorHallways++;
                continue;
            }
            if (Place(factory, hallway, position) == null)
            {
                fixtureStage = $"hallway-placement-failed:offset={dx};{placementFailure}";
                return false;
            }
        }

        fixtureStage = "place-captivity-facilities";
        // CP01 is authored as 2x1 and even-width placement treats the anchor
        // as the right-hand cell (anchor - 1, anchor). CB01 is 1x1; keep all
        // three facilities on distinct cells and preserve free approach cells.
        // The captive housing is deliberately closest to the exit.  The
        // one-dimensional floor topology cannot route around facilities; with
        // pens between housing and the door a perfectly walkable connector is
        // still unreachable from a confined captive.
        housing = Place(factory, housingAsset, new Vector2Int(start + 7, row + 1));
        if (housing == null)
        {
            fixtureStage = "housing-placement-failed:" + placementFailure;
            return false;
        }
        pen = Place(factory, penAsset, new Vector2Int(start + 2, row + 1));
        if (pen == null)
        {
            fixtureStage = "pen-placement-failed:" + placementFailure;
            return false;
        }
        faultPen = Place(factory, penAsset, new Vector2Int(start + 4, row + 1));
        if (faultPen == null)
        {
            fixtureStage = "fault-pen-placement-failed:" + placementFailure;
            return false;
        }
        rooms.Clear();
        workerCell = new Vector2Int(start + 5, row + 1);
        // Separate initial subject approach from the later housing delivery.
        // Static reachability of either side does not prove that the actual
        // asynchronous approach succeeds; retain the production terminal check.
        captiveCell = new Vector2Int(start + 6, row + 1);
        RoomInstance housingRoom = null;
        RoomInstance penRoom = null;
        RoomInstance faultPenRoom = null;
        bool housingRoomResolved = housing != null
            && rooms.TryGetRoom(housing, out housingRoom);
        bool penRoomResolved = pen != null
            && rooms.TryGetRoom(pen, out penRoom);
        bool faultPenRoomResolved = faultPen != null
            && rooms.TryGetRoom(faultPen, out faultPenRoom);
        fixtureRoom = penRoom;
        if (housing == null || pen == null || faultPen == null
            || !penRoomResolved
            || fixtureRoom == null || !fixtureRoom.IsUsable
            || !housingRoomResolved
            || !faultPenRoomResolved
            || housingRoom?.Id != fixtureRoom.Id
            || faultPenRoom?.Id != fixtureRoom.Id
            || !fixtureRoom.Cells.Contains(workerCell)
            || !fixtureRoom.Cells.Contains(captiveCell)
            || !grid.IsWalkable(workerCell)
            || !grid.IsWalkable(captiveCell))
        {
            fixtureStage =
                $"facility-or-room-invalid:housing={housing != null};pen={pen != null};faultPen={faultPen != null};room={fixtureRoom?.Id};usable={fixtureRoom?.IsUsable};housingRoom={housingRoom?.Id};penRoom={penRoom?.Id};faultPenRoom={faultPenRoom?.Id};workerWalkable={grid.IsWalkable(workerCell)};captiveWalkable={grid.IsWalkable(captiveCell)};{placementFailure}";
            return false;
        }
        if (!TryValidateEscapeConnection(cellDoor, escapeStair, out string escapeFailure))
        {
            fixtureStage = "escape-connection-invalid:" + escapeFailure;
            return false;
        }
        if (!DungeonSpaceGridLayout.TryCapture(
                grid,
                out DungeonInteriorLayoutSnapshot fixtureLayout,
                out string layoutFailure))
        {
            fixtureStage = "escape-layout-not-save-valid:" + layoutFailure;
            return false;
        }
        if (fixtureLayout.StartX != baselineLayout.StartX
            || fixtureLayout.ColumnCount != baselineLayout.ColumnCount
            || fixtureLayout.EntrancePosition != baselineLayout.EntrancePosition)
        {
            fixtureStage = $"escape-layout-baseline-changed:"
                + $"columns={baselineLayout.ColumnCount}->{fixtureLayout.ColumnCount};"
                + $"start={baselineLayout.StartX}->{fixtureLayout.StartX};"
                + $"entrance={baselineLayout.EntrancePosition}->{fixtureLayout.EntrancePosition}";
            return false;
        }
        fixtureStage =
            $"ready:bounds={start},{row}..{start + roomWidth - 1},{row + roomHeight - 1};room={fixtureRoom.Id};escape={escapeConnectorCell}->{escapeExteriorAnchorCell};layoutColumns={fixtureLayout.ColumnCount};baselineUnchanged=True;reusedInteriorHallways={reusedInteriorHallways};preparedOriginalBuildings={preparedOriginalBuildings.Count};displacedWildlife={displacedWildlife.Count};areas={areaSnapshots.Count}";
        return true;
    }

    private bool CanRetireFixtureBuilding(
        IGridOccupant occupant, GridLayer layer, int start, int width, int height,
        out BuildableObject building)
    {
        building = occupant as BuildableObject;
        // Actual authored facilities also own WallFixture/CeilingFixture/etc.
        // Eligibility is their exact captured owner/definition/layer/footprint,
        // not a guessed two-layer whitelist. Unfinished construction is excluded.
        if (layer == GridLayer.Construction
            || building == null || building is Hallway
            || building.IsBuildingDestroyed || building.IsDetachedRestoreCandidate
            || !building.gameObject.activeInHierarchy
            || !ReferenceEquals(building.Grid, grid)
            || building.BuildingData == null
            || building.BuildingData.Placement.Layer != layer
            || !building.PersistentInstanceId.IsValid
            || building.buildPoses == null || building.buildPoses.Count == 0)
            return false;
        BuildableObject original = building;
        string id = original.PersistentInstanceId.Value;
        ModularFacilityBuildingSaveData[] saved = baselineFacilityWorld?.buildings
            .Where(value => value.persistentInstanceId == id).ToArray()
            ?? Array.Empty<ModularFacilityBuildingSaveData>();
        return saved.Length == 1
            && saved[0].buildingId == original.BuildingData.id
            && saved[0].centerX == original.centerPos.x
            && saved[0].centerY == original.centerPos.y
            && saved[0].layer == layer
            && saved[0].hasRuntimeLayer && !saved[0].relocationPacked
            && saved[0].runtimeLayer == layer
            && saved[0].width == original.BuildingData.Placement.Width
            && saved[0].height == original.BuildingData.Placement.Height
            && world.Buildings.Count(value => ReferenceEquals(value, original)) == 1
            && world.Buildings.Count(value => value != null
                && value.PersistentInstanceId.IsValid
                && value.PersistentInstanceId.Value == id) == 1
            && original.buildPoses.All(position =>
                position.x >= start && position.x < start + width
                && position.y >= 0 && position.y < height
                && ReferenceEquals(grid.GetGridCell(position)?.GetOccupant(layer), original));
    }

    private bool CanReuseFixtureHallway(IGridOccupant occupant, Vector2Int position)
    {
        return occupant is Hallway floor
            && !floor.IsBuildingDestroyed
            && floor.gameObject.activeInHierarchy
            && ReferenceEquals(floor.Grid, grid)
            && floor.BuildingData != null
            && floor.BuildingData.Placement.Layer == GridLayer.Hallway
            && floor.buildPoses?.Contains(position) == true
            && ReferenceEquals(
                grid.GetGridCell(position)?.GetOccupant(GridLayer.Hallway),
                floor);
    }

    private bool TryValidateEscapeConnection(
        Door cellDoor,
        Stair escapeStair,
        out string failure)
    {
        GridCell connector = grid.GetGridCell(escapeConnectorCell);
        GridCell anchor = grid.GetGridCell(escapeExteriorAnchorCell);
        IDoorAccessQuery doorAccess = scope.Container.Resolve<IDoorAccessQuery>();
        GridTraversalContext context = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(captiveActor),
            DoorAccessOverrideKind.CaptiveEscape,
            GridMovementIntent.EscapeHazard);
        bool connectorWalkable = connector?.AreaType == GridCellAreaType.DungeonInterior
            && connector.IsWalkableArea
            && grid.IsWalkable(escapeConnectorCell);
        bool anchorWalkable = anchor?.AreaType == GridCellAreaType.ExteriorPath
            && anchor.IsWalkableArea
            && grid.IsWalkable(escapeExteriorAnchorCell);
        bool stairLink = connector?.TraversalLinks.Any(link =>
            link != null
            && link.To == new Vector2Int(
                escapeConnectorCell.x,
                escapeConnectorCell.y - 1)
            && link.MoveType == GridMoveType.Stair
            && ReferenceEquals(link.Through, escapeStair)) == true;
        bool doorAllowed = doorAccess.CanTraverse(
            grid,
            cellDoor.centerPos,
            context,
            out string doorDenial);
        Vector2Int lowerLanding = new(
            escapeConnectorCell.x,
            escapeConnectorCell.y - 1);
        GridCell lowerLandingCell = grid.GetGridCell(lowerLanding);
        bool lowerLandingReady = lowerLandingCell?.AreaType
                == GridCellAreaType.DungeonInterior
            && lowerLandingCell.IsWalkableArea
            && grid.IsWalkable(lowerLanding);
        int firstExteriorColumn = escapeStair.buildPoses.Max(position => position.x) + 1;
        Vector2Int[] exteriorChain = Enumerable.Range(
                firstExteriorColumn,
                escapeExteriorAnchorCell.x - firstExteriorColumn + 1)
            .Select(x => new Vector2Int(x, lowerLanding.y))
            .ToArray();
        bool exteriorChainReady = exteriorChain.All(position =>
        {
            GridCell cell = grid.GetGridCell(position);
            return cell?.AreaType == GridCellAreaType.ExteriorPath
                && cell.IsWalkableArea
                && grid.IsWalkable(position);
        });
        GridPathSearchResult search = grid.SearchPathWithTraversalFilter(
            housing.centerPos,
            position => doorAccess.CanTraverse(
                grid,
                position,
                context,
                out _));
        bool routeReady = search != null
            && search.ContainsPosition(escapeConnectorCell)
            && search.ContainsPosition(lowerLanding)
            && search.ContainsPosition(escapeExteriorAnchorCell);
        if (connectorWalkable
            && anchorWalkable
            && stairLink
            && lowerLandingReady
            && doorAllowed
            && exteriorChainReady
            && routeReady)
        {
            failure = string.Empty;
            return true;
        }

        failure = $"connectorWalkable={connectorWalkable};"
            + $"anchorWalkable={anchorWalkable};stairLink={stairLink};"
            + $"lowerLandingReady={lowerLandingReady};"
            + $"doorAllowed={doorAllowed};doorDenial={doorDenial};"
            + $"exteriorChainReady={exteriorChainReady};routeReady={routeReady};"
            + $"connector={DescribeCell(escapeConnectorCell)};"
            + $"anchor={DescribeCell(escapeExteriorAnchorCell)};"
            + $"door={DescribeCell(cellDoor.centerPos)};"
            + "exteriorChain=["
            + string.Join("|", exteriorChain.Select(DescribeCell))
            + "]";
        return false;
    }

    private string DescribeCell(Vector2Int position)
    {
        GridCell cell = grid.GetGridCell(position);
        if (cell == null) return $"{position}:missing";
        string occupants = string.Join(",",
            Enum.GetValues(typeof(GridLayer))
                .Cast<GridLayer>()
                .Select(layer => new
                {
                    Layer = layer,
                    Occupant = cell.GetOccupant(layer)
                })
                .Where(entry => entry.Occupant != null)
                .Select(entry =>
                    $"{entry.Layer}:{entry.Occupant.GetType().Name}#{entry.Occupant.GridId}"));
        string links = string.Join(",",
            cell.TraversalLinks.Select(link =>
                $"{link.To}/{link.MoveType}/{link.Through?.GetType().Name ?? "none"}"));
        return $"{position}:area={cell.AreaType}:walkableArea={cell.IsWalkableArea}:"
            + $"walkable={grid.IsWalkable(position)}:occupants=[{occupants}]:links=[{links}]";
    }

    private BuildableObject Place(
        GridBuildingFactory factory,
        BuildingSO data,
        Vector2Int position)
    {
        placementFailure = string.Empty;
        IReadOnlyList<Vector2Int> positions = data.GetGridPosList(position);
        Vector2Int blocked = positions.FirstOrDefault(cell =>
            grid.GetGridCell(cell)?.CanOccupy(data.Placement.Layer) != true);
        if (positions.Any(cell =>
                grid.GetGridCell(cell)?.CanOccupy(data.Placement.Layer) != true))
        {
            GridCell cell = grid.GetGridCell(blocked);
            placementFailure = $"precheck:data={data.name};anchor={position};cell={blocked};layer={data.Placement.Layer};exists={cell != null};area={cell?.AreaType};occupant={cell?.GetOccupant(data.Placement.Layer)?.GetType().Name ?? "none"}";
            return null;
        }
        BuildableObject building = factory.Create(grid, data, position);
        if (building == null)
        {
            placementFailure = $"factory-null:data={data.name};anchor={position}";
            return null;
        }
        if (!building.CanApplyDamageRules)
        {
            placementFailure =
                $"factory-injection-missing:data={data.name};anchor={position};"
                + $"type={building.GetType().Name};"
                + $"instance={building.GetInstanceID()}";
            Destroy(building.gameObject);
            return null;
        }
        building.SetGrid(grid);
        building.Initialization(data, position);
        if (!grid.RegisterOccupant(
                building,
                data.Placement.Layer,
                positions,
                data.Placement.IsMovement))
        {
            placementFailure = $"register-failed:data={data.name};anchor={position};cells={string.Join(",", positions)}";
            Destroy(building.gameObject);
            return null;
        }
        world.RegisterBuilding(building);
        fixtureBuildings.Add(building);
        fixtureBuildingSnapshots.Add(new FixtureBuildingSnapshot(
            building,
            data.Placement.Layer,
            positions.ToArray(),
            data.Placement.IsMovement));
        return building;
    }

    private static BuildingSO FindBuildingAsset(Func<BuildingSO, bool> predicate) =>
        AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building/Captivity" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(data => data != null && predicate(data));

    private string ResolveAuthoredFeedItemId(WildlifeActor animal)
    {
        if (!wildlifeSpecies.TryGetSpecies(animal.SpeciesId,
                out WildlifeSpeciesDefinition species))
        {
            return string.Empty;
        }

        // Test stock must be edible under the same category and diet policy as
        // production, not a separate ingredient-tag-only approximation.
        return resourceCatalog.Items
            .Where(item => WildlifeFeedSelectionRules.IsAllowed(species.Diet, item))
            .OrderBy(item => WildlifeFeedSelectionRules.GetPreference(species.Diet, item))
            .ThenBy(item => item.UnitPrice)
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .Select(item => item.ItemId)
            .FirstOrDefault() ?? string.Empty;
    }

    private int CountFacilityItem(string destinationId, string itemId)
    {
        if (string.IsNullOrEmpty(destinationId)
            || string.IsNullOrEmpty(itemId))
        {
            return 0;
        }
        return itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
    }

    private WildlifeActor SpawnCaptureAnimal(Vector2Int near)
    {
        wildlife.Tick();
        WildlifeActor source = wildlife.Wildlife.FirstOrDefault(value =>
            value != null && value.IsAlive);
        string speciesId = source?.SpeciesId
            ?? wildlifeSpecies?.All?.FirstOrDefault()?.SpeciesId;
        if (string.IsNullOrWhiteSpace(speciesId)) return null;
        HashSet<string> existing = wildlife.Wildlife
            .Where(value => value != null)
            .Select(value => value.WildlifeId)
            .ToHashSet(StringComparer.Ordinal);
        Vector2Int position = default;
        bool didSpawn = false;
        foreach (Vector2Int candidate in grid.GetCells()
                     .Where(cell => cell != null && grid.IsWalkable(cell.Position))
                     .OrderBy(cell => Mathf.Abs(cell.Position.x - near.x)
                         + Mathf.Abs(cell.Position.y - near.y))
                     .Select(cell => cell.Position))
        {
            if (!wildlife.DebugSpawn(
                    speciesId,
                    1,
                    candidate,
                    out int spawned,
                    out _)
                || spawned != 1)
            {
                continue;
            }
            position = candidate;
            didSpawn = true;
            break;
        }
        if (!didSpawn)
            return null;
        WildlifeActor created = wildlife.Wildlife.FirstOrDefault(value =>
            value != null && !existing.Contains(value.WildlifeId));
        created?.WarpTo(position);
        return created;
    }

    private Vector2Int FindFarWalkable(Vector2Int from) =>
        grid.GetCells()
            .Where(cell => cell != null && grid.IsWalkable(cell.Position))
            .OrderByDescending(cell => Mathf.Abs(cell.Position.x - from.x)
                + Mathf.Abs(cell.Position.y - from.y))
            .Select(cell => cell.Position)
            .FirstOrDefault();

    private IEnumerator PlanLawfulTransportFixture(
        TransportFixturePlanProbe probe)
    {
        if (probe == null)
        {
            yield break;
        }
        probe.Detail =
            "missing worker, grid, room, pen, or path broker";
        if (worker == null
            || grid == null
            || fixtureRoom == null
            || pen == null
            || pathSearchBroker == null)
        {
            yield break;
        }

        HashSet<Vector2Int> penFootprint = new(
            pen.buildPoses ?? Array.Empty<Vector2Int>());
        Vector2Int[] deliveryStands = fixtureRoom.Cells
            .Where(cell => grid.IsWalkable(cell)
                && !penFootprint.Contains(cell)
                && IsUnoccupiedTransportStand(grid.GetGridCell(cell)))
            .OrderBy(cell => Mathf.Abs(cell.x - pen.centerPos.x)
                + Mathf.Abs(cell.y - pen.centerPos.y))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .ToArray();
        Vector2Int[] sourceCells = grid.GetCells()
            .Where(cell => cell != null
                && WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, cell)
                && cell.AreaType == GridCellAreaType.ExteriorPath
                && grid.IsWalkable(cell.Position)
                && IsUnoccupiedTransportStand(cell))
            .OrderBy(cell => Mathf.Abs(cell.Position.x - pen.centerPos.x)
                + Mathf.Abs(cell.Position.y - pen.centerPos.y))
            .ThenBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .Select(cell => cell.Position)
            .ToArray();
        GridTraversalContext traversal = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(worker),
            DoorAccessOverrideKind.EscortPass);
        // Use the same adjacent candidates as production ResolveAdjacentMovement.
        // A wildlife-layer occupant does not block grid movement, so the
        // target cell itself is a lawful final pickup stand.  This matters on
        // a one-cell exterior stair landing where none of the four adjacent
        // cells owns the traversal link back into the pen room.
        Vector2Int[] pickupOffsets =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.zero
        };
        float deadline = Time.realtimeSinceStartup + 8f;
        int deferred = 0;
        int pickupCandidates = 0;
        foreach (Vector2Int source in sourceCells)
        {
            foreach (Vector2Int offset in pickupOffsets)
            {
                Vector2Int pickup = source + offset;
                if (!grid.IsValidGridPos(pickup)
                    || !grid.IsWalkable(pickup))
                {
                    continue;
                }

                pickupCandidates++;
                GridPathSearchResult search = null;
                while (Time.realtimeSinceStartup < deadline
                       && !pathSearchBroker.TryGetSearch(
                           grid,
                           pickup,
                           out search,
                           GridPathSearchPriority.Urgent,
                           traversal))
                {
                    deferred++;
                    yield return null;
                }
                if (search == null)
                {
                    probe.Detail = $"sourceCandidates={sourceCells.Length};"
                        + $"pickupCandidates={pickupCandidates};"
                        + $"deliveryCandidates={deliveryStands.Length};"
                        + $"deferred={deferred};search-timeout;"
                        + $"doors={DescribeFixtureDoors()}";
                    yield break;
                }

                foreach (Vector2Int delivery in deliveryStands)
                {
                    Queue<GridMoveStep> path =
                        search.GetMovePathTo(delivery);
                    if (path == null
                        || !GridMovePathRules.TryGetPathDestination(
                            path,
                            out Vector2Int pathEnd)
                        || pathEnd != delivery)
                    {
                        continue;
                    }

                    probe.Ready = true;
                    probe.SourceCell = source;
                    probe.PickupStand = pickup;
                    probe.DeliveryStand = delivery;
                    probe.Detail = $"source={source};pickup={pickup};"
                        + $"delivery={delivery};pathEnd={pathEnd};"
                        + $"pathSteps={path.Count};deferred={deferred};"
                        + $"sourceCell={DescribeCell(source)};"
                        + $"pickupCell={DescribeCell(pickup)};"
                        + $"deliveryCell={DescribeCell(delivery)};"
                        + $"doors={DescribeFixtureDoors()}";
                    yield break;
                }
            }
        }

        probe.Detail = $"sourceCandidates={sourceCells.Length};"
            + $"pickupCandidates={pickupCandidates};"
            + $"deliveryCandidates={deliveryStands.Length};"
            + $"deferred={deferred};doors={DescribeFixtureDoors()};"
            + "no lawful source/pickup/delivery EscortPass triple";
        // Diagnose the failed production-broker preparation without replacing
        // it. This read-only search must never supply a Ready plan or command.
        if (sourceCells.Length > 0)
        {
            Vector2Int diagnosticPickup = sourceCells[0] + Vector2Int.left;
            if (grid.IsValidGridPos(diagnosticPickup)
                && grid.IsWalkable(diagnosticPickup))
            {
                IDoorAccessQuery doorAccess = scope.Container.Resolve<IDoorAccessQuery>();
                GridPathSearchResult diagnosticSearch = grid.SearchPathWithTraversalFilter(
                    diagnosticPickup,
                    position => doorAccess.CanTraverse(grid, position, traversal, out _));
                rows.Add("INFO\tTRANSPORT_LAWFUL_FAILURE_DIRECT_SEARCH\t"
                    + $"pickup={diagnosticPickup};gridRevision={grid.TraversalVersion};"
                    + "delivery=["
                    + string.Join("|", deliveryStands.Select(position =>
                        $"{position}:reachable={diagnosticSearch?.ContainsPosition(position)}"))
                    + "];diagnosticOnly=true;fallback=false;trainingEligible=false");
            }
        }
        int diagnosticFirstColumn = fixtureRoom.Cells.Min(position => position.x);
        Vector2Int[] diagnosticCells = Enumerable.Range(
                diagnosticFirstColumn,
                escapeExteriorAnchorCell.x - diagnosticFirstColumn + 1)
            .SelectMany(x => Enumerable.Range(0, grid.height)
                .Select(y => new Vector2Int(x, y)))
            .ToArray();
        CharacterBodyHealthSnapshot diagnosticBody = bodyHealthQuery.GetSnapshot(worker);
        rows.Add("INFO\tTRANSPORT_LAWFUL_FAILURE_TOPOLOGY\t"
            + $"actor={DescribeLiveActor(worker)};bodyDowned={diagnosticBody.Downed};"
            + $"mobility={diagnosticBody.Mobility};"
            + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
            + $"owner={worker.Brain?.ExternalIntentOwnerId};"
            + $"pen={pen.RequirePersistentInstanceId().Value};"
            + $"room={fixtureRoom.Id};gridRevision={grid.TraversalVersion};"
            + "cells=[" + string.Join("|", diagnosticCells.Select(DescribeCell))
            + "];diagnosticOnly=true;trainingEligible=false");
    }

    private IEnumerator PlaceWorkerAtReachablePickupAndDeliveryStand(
        WildlifeActor animal,
        TransportFixturePlanProbe expectedPlan,
        PickupDeliveryPreflightProbe probe)
    {
        if (probe == null)
        {
            yield break;
        }
        probe.Detail =
            "missing actor, animal, grid, room, pen, or path broker";
        if (worker == null
            || animal == null
            || grid == null
            || fixtureRoom == null
            || pen == null
            || pathSearchBroker == null)
        {
            yield break;
        }
        HashSet<Vector2Int> penFootprint = new(
            pen.buildPoses ?? Array.Empty<Vector2Int>());
        Vector2Int[] deliveryStands = fixtureRoom.Cells
            .Where(cell => grid.IsWalkable(cell)
                && !penFootprint.Contains(cell)
                && IsUnoccupiedTransportStand(grid.GetGridCell(cell)))
            .OrderBy(cell => expectedPlan != null
                    && cell == expectedPlan.DeliveryStand
                ? 0
                : 1)
            .ThenBy(cell => Mathf.Abs(cell.x - pen.centerPos.x)
                + Mathf.Abs(cell.y - pen.centerPos.y))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .ToArray();
        Vector2Int source = animal.GridPosition;
        Vector2Int[] pickupCandidates =
        {
            source + Vector2Int.left,
            source + Vector2Int.right,
            source + Vector2Int.up,
            source + Vector2Int.down,
            source
        };
        GridTraversalContext pickupTraversal =
            GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(worker),
                DoorAccessOverrideKind.None);
        GridTraversalContext deliveryTraversal = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(worker),
            DoorAccessOverrideKind.EscortPass);
        float deadline = Time.realtimeSinceStartup + 8f;
        int deferred = 0;
        int searchedPickupStands = 0;
        Vector2Int carrierStart = worker.GetNowXY();
        GridPathSearchResult carrierSearch = null;
        while (Time.realtimeSinceStartup < deadline
               && !pathSearchBroker.TryGetSearch(
                   grid,
                   carrierStart,
                   out carrierSearch,
                   GridPathSearchPriority.Urgent,
                   pickupTraversal))
        {
            deferred++;
            yield return null;
        }
        if (carrierSearch == null)
        {
            probe.Detail = $"carrier={carrierStart};deferred={deferred};"
                + "carrier-to-pickup production search unavailable";
            yield break;
        }

        Vector2Int selectedPickup = default;
        Queue<GridMoveStep> selectedPickupPath = null;
        bool controlledCarrierStartPlacement = false;
        foreach (Vector2Int pickup in pickupCandidates)
        {
            if (!grid.IsValidGridPos(pickup)
                || !grid.IsWalkable(pickup))
            {
                continue;
            }

            searchedPickupStands++;
            Queue<GridMoveStep> pickupPath =
                carrierSearch.GetMovePathTo(pickup);
            if (pickupPath == null
                || pickupPath.Count == 0
                || !GridMovePathRules.TryGetPathDestination(
                    pickupPath,
                    out Vector2Int pickupPathEnd)
                || pickupPathEnd != pickup)
            {
                continue;
            }
            if (selectedPickupPath == null
                || pickupPath.Count < selectedPickupPath.Count)
            {
                selectedPickup = pickup;
                selectedPickupPath = pickupPath;
            }
        }
        if (selectedPickupPath == null)
        {
            Vector2Int[] fixtureStarts = grid.GetCells()
                .Where(cell => cell != null
                    && cell.AreaType == GridCellAreaType.ExteriorPath
                    && grid.IsWalkable(cell.Position)
                    && IsUnoccupiedTransportStand(cell)
                    && Mathf.Abs(cell.Position.x - source.x)
                        + Mathf.Abs(cell.Position.y - source.y) is >= 2 and <= 8)
                .OrderBy(cell => expectedPlan != null
                        && cell.Position == expectedPlan.SourceCell
                    ? 0
                    : 1)
                .ThenBy(cell => Mathf.Abs(cell.Position.x - source.x)
                    + Mathf.Abs(cell.Position.y - source.y))
                .ThenBy(cell => cell.Position.y)
                .ThenBy(cell => cell.Position.x)
                .Select(cell => cell.Position)
                .ToArray();
            foreach (Vector2Int fixtureStart in fixtureStarts)
            {
                GridPathSearchResult fixtureSearch = null;
                while (Time.realtimeSinceStartup < deadline
                       && !pathSearchBroker.TryGetSearch(
                           grid,
                           fixtureStart,
                           out fixtureSearch,
                           GridPathSearchPriority.Urgent,
                           pickupTraversal))
                {
                    deferred++;
                    yield return null;
                }
                if (fixtureSearch == null)
                {
                    continue;
                }
                foreach (Vector2Int pickup in pickupCandidates)
                {
                    if (!grid.IsValidGridPos(pickup)
                        || !grid.IsWalkable(pickup))
                    {
                        continue;
                    }
                    Queue<GridMoveStep> pickupPath =
                        fixtureSearch.GetMovePathTo(pickup);
                    if (pickupPath == null
                        || pickupPath.Count == 0
                        || !GridMovePathRules.TryGetPathDestination(
                            pickupPath,
                            out Vector2Int pickupPathEnd)
                        || pickupPathEnd != pickup)
                    {
                        continue;
                    }
                    carrierStart = fixtureStart;
                    selectedPickup = pickup;
                    selectedPickupPath = pickupPath;
                    controlledCarrierStartPlacement = true;
                    worker.transform.position = grid.GetWorldPos(carrierStart);
                    yield return null;
                    break;
                }
                if (selectedPickupPath != null)
                {
                    break;
                }
            }
            if (selectedPickupPath == null)
            {
                probe.Detail = $"animal={source};carrier={carrierStart};"
                    + $"pickupCandidates={searchedPickupStands};"
                    + $"fixtureStarts={fixtureStarts.Length};"
                    + $"deferred={deferred};"
                    + "no production pickup approach path";
                yield break;
            }
        }

        GridPathSearchResult search = null;
        while (Time.realtimeSinceStartup < deadline
               && !pathSearchBroker.TryGetSearch(
                   grid,
                   selectedPickup,
                   out search,
                   GridPathSearchPriority.Urgent,
                   deliveryTraversal))
        {
            deferred++;
            yield return null;
        }
        if (search == null)
        {
            probe.Detail = $"animal={source};carrier={carrierStart};"
                + $"pickup={selectedPickup};deferred={deferred};"
                + "pickup-to-delivery EscortPass search unavailable";
            yield break;
        }

        foreach (Vector2Int delivery in deliveryStands)
        {
            Queue<GridMoveStep> path = search.GetMovePathTo(delivery);
            if (path == null
                || !GridMovePathRules.TryGetPathDestination(
                    path,
                    out Vector2Int pathEnd)
                || pathEnd != delivery)
            {
                continue;
            }

            probe.Ready = true;
            probe.ApproachStart = carrierStart;
            probe.PickupStand = selectedPickup;
            probe.DeliveryStand = delivery;
            probe.PathStepCount = selectedPickupPath.Count + path.Count;
            probe.PathWorldDistance = CalculatePathWorldDistance(
                    carrierStart,
                    selectedPickupPath)
                + CalculatePathWorldDistance(selectedPickup, path);
            probe.Detail = $"animal={source};carrier={carrierStart};"
                + $"pickup={selectedPickup};delivery={delivery};"
                + $"pathEnd={pathEnd};"
                + $"pickupSteps={selectedPickupPath.Count};"
                + $"deliverySteps={path.Count};"
                + $"pathWorldDistance={probe.PathWorldDistance:0.###};"
                + $"controlledCarrierStartPlacement={controlledCarrierStartPlacement};"
                + $"deferred={deferred};"
                + $"pickupCell={DescribeCell(selectedPickup)};"
                + $"deliveryCell={DescribeCell(delivery)};"
                + $"doors={DescribeFixtureDoors()}";
            yield break;
        }

        probe.Detail = $"animal={source};pickupCandidates="
            + $"{searchedPickupStands};deliveryCandidates="
            + $"{deliveryStands.Length};deferred={deferred};"
            + $"doors={DescribeFixtureDoors()};"
            + "no exact production pickup and EscortPass delivery path";
    }

    private float CalculatePathWorldDistance(
        Vector2Int start,
        IEnumerable<GridMoveStep> path)
    {
        Vector3 previous = grid.GetWorldPos(start);
        float distance = 0f;
        if (path == null)
        {
            return distance;
        }

        foreach (GridMoveStep step in path)
        {
            if (!step.IsValid)
            {
                continue;
            }

            Vector3 next = grid.GetWorldPos(step.To);
            distance += Vector3.Distance(previous, next);
            previous = next;
        }
        return distance;
    }

    private static bool IsUnoccupiedTransportStand(GridCell cell)
    {
        return cell != null
            && cell.GetOccupant(GridLayer.Building) == null
            && cell.GetOccupant(GridLayer.Construction) == null
            && cell.GetOccupant(GridLayer.Conveyor) == null
            && cell.GetOccupant(GridLayer.Character) == null
            && cell.GetOccupant(GridLayer.DownedCharacter) == null
            && cell.GetOccupant(GridLayer.Wildlife) == null;
    }

    private static Vector2Int[] FindGridOccupantRegistrations(
        Grid targetGrid,
        GridLayer layer,
        IGridOccupant expectedOccupant)
    {
        if (targetGrid == null || expectedOccupant == null)
        {
            return Array.Empty<Vector2Int>();
        }

        List<Vector2Int> positions = new();
        for (int y = 0; y < targetGrid.height; y++)
        {
            for (int x = 0; x < targetGrid.width; x++)
            {
                Vector2Int position = new(x, y);
                if (ReferenceEquals(
                        targetGrid.GetGridCell(position)?.GetOccupant(layer),
                        expectedOccupant))
                {
                    positions.Add(position);
                }
            }
        }
        return positions.ToArray();
    }

    private static string DescribeGridCellLayers(GridCell cell)
    {
        if (cell == null)
        {
            return "missing-cell";
        }

        List<string> layers = new();
        List<IGridOccupant> occupants = new();
        foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
        {
            occupants.Clear();
            cell.FillOccupantsInLayer(layer, occupants);
            string detail = occupants.Count == 0
                ? "none"
                : string.Join("&", occupants.Select(DescribeGridOccupant));
            layers.Add($"{layer}=[{detail}]");
        }
        return string.Join("|", layers);
    }

    private static string DescribeGridOccupant(IGridOccupant occupant)
    {
        if (occupant == null)
        {
            return "none";
        }

        string runtimeType = occupant.GetType().FullName;
        if (occupant is UnityEngine.Object destroyedUnityObject
            && destroyedUnityObject == null)
        {
            return $"{runtimeType}:unity-destroyed:clrReference=True";
        }

        string authority = occupant switch
        {
            WildlifeActor animal when animal != null =>
                $"wildlife:{animal.WildlifeId}:{animal.SpeciesId}:"
                + $"{animal.State}@{animal.GridPosition}",
            DownedCharacterGridOccupant downed when downed.Actor != null =>
                $"downed:{downed.Actor.Identity?.PersistentId}:"
                + $"{downed.Actor.CurrentLifecycleState}@{downed.Actor.GetNowXY()}",
            BuildableObject building when building != null =>
                $"building:{(building.PersistentInstanceId.IsValid ? building.PersistentInstanceId.Value : "missing-id")}:"
                + $"{building.BuildingData?.name}@{building.centerPos}",
            _ => "authority-unavailable"
        };
        string unityState = occupant is UnityEngine.Object unityObject
            ? unityObject == null
                ? "unity-destroyed"
                : $"unity:{unityObject.name}:{unityObject.GetInstanceID()}"
            : "non-unity";
        return $"{runtimeType}:gridId={occupant.GridId}:"
            + $"destroyed={occupant.IsGridDestroyed}:"
            + $"visitable={occupant.IsGridVisitable}:"
            + $"movement={occupant.IsGridMovement}:"
            + $"{authority}:{unityState}";
    }

    private static string DescribeLiveActor(CharacterActor actor)
    {
        if (actor == null)
        {
            return "none";
        }

        return $"{actor.Identity?.PersistentId}:{actor.name}:"
            + $"{actor.CurrentLifecycleState}:active={actor.gameObject.activeInHierarchy}:"
            + $"paused={actor.IsAiPaused()}@{actor.GetNowXY()}";
    }

    private static string DescribeTransform(Transform value)
    {
        if (value == null)
        {
            return "none";
        }
        return $"{value.name}:{value.GetInstanceID()}";
    }

    private string DescribeFixtureDoors()
    {
        string[] details = fixtureBuildings
            .Where(building => building?.BuildingData != null
                && building.BuildingData.name.IndexOf(
                    "Door",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(building => DescribeCell(building.centerPos))
            .ToArray();
        return details.Length > 0
            ? string.Join("|", details)
            : "none";
    }

    private static void DamageForCapture(WildlifeActor actor)
    {
        if (actor != null)
            actor.ApplyDamage(Mathf.CeilToInt(actor.MaxHealth * 0.7f), null);
    }

    private void RegisterFaultRing(Vector2Int center)
    {
        RemoveFaultWalls();
        Vector2Int[] positions =
        {
            center + Vector2Int.left,
            center + Vector2Int.right,
            center + Vector2Int.up,
            center + Vector2Int.down
        };
        foreach (Vector2Int position in positions.Where(grid.IsValidGridPos))
        {
            FaultWall wall = new($"qa-wall:{position}", position);
            if (grid.RegisterOccupant(
                    wall, GridLayer.Building, new[] { position }, false))
                faultWalls.Add(wall);
        }
    }

    private void RemoveFaultWalls()
    {
        foreach (FaultWall wall in faultWalls.OfType<FaultWall>())
            grid?.RemoveOccupant(
                wall, GridLayer.Building, new[] { wall.Position }, false);
        faultWalls.Clear();
    }

    private static bool NoHusbandryCompletionCursor(AnimalHusbandryPendingOutcomeSaveData pending,
        IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> cursors)
    {
        var request = AnimalHusbandryCompletionObservation.CreateSavedRequest(pending);
        return pending.completionPublicationKey == WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString()
            && !cursors.Any(value => value.producerStreamId == request.ProducerStreamId);
    }

    private IEnumerator VerifyHusbandryPendingColdResume()
    {
        string path = Environment.GetEnvironmentVariable(
            CaptivityWildlifeLifecyclePlayModeVerifier.HusbandryPendingResumeWorldVariable);
        string expectedHash = Environment.GetEnvironmentVariable(
            CaptivityWildlifeLifecyclePlayModeVerifier.HusbandryPendingResumeHashVariable);
        string qaRoot = Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar;
        bool inputBound = DungeonPersistencePaths.IsIsolatedVerification
            && !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path)
            && Path.GetFullPath(path).StartsWith(qaRoot, StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(path) == "real-capture-slaughter-identity-pending-world.json"
            && File.Exists(path) && !string.IsNullOrWhiteSpace(expectedHash);
        Check(inputBound, "HUSBANDRY_COLD_EXPLICIT_SOURCE", path ?? "missing");
        if (!inputBound) yield break;
        byte[] bytes = File.ReadAllBytes(path);
        using System.Security.Cryptography.SHA256 hasher = System.Security.Cryptography.SHA256.Create();
        string Hash(byte[] value) => BitConverter.ToString(hasher.ComputeHash(value))
            .Replace("-", string.Empty).ToLowerInvariant();
        Check(string.Equals(Hash(bytes), expectedHash, StringComparison.OrdinalIgnoreCase),
            "HUSBANDRY_COLD_SOURCE_HASH", Hash(bytes));
        if (failures.Count != 0) yield break;
        string json = System.Text.Encoding.UTF8.GetString(bytes);
        DungeonGameSaveData incoming = gameSaveService.FromJson(json);
        DungeonAnimalHusbandrySaveData source = JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(
            incoming.sections.Single(value => value.sectionId == AnimalHusbandrySaveSection.Id).payloadJson);
        GameplayOutcomeLedgerSaveData sourceLedger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
            incoming.sections.Single(value => value.sectionId == "world.gameplay-outcome-ledger").payloadJson);
        CharacterNarrativeWorldSaveData sourceNarrative = JsonUtility.FromJson<CharacterNarrativeWorldSaveData>(
            incoming.sections.Single(value => value.sectionId == CharacterNarrativeSaveSection.Id).payloadJson);
        AnimalHusbandryPendingOutcomeSaveData saved = source.pendingOutcomes.SingleOrDefault();
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        Check(clock.Time < 5f && incoming.sections.Count == baseline.Count
              && source.version == DungeonAnimalHusbandrySaveData.CurrentVersion
              && saved != null && saved.kind == AnimalHusbandryOutcomeOperationKind.Slaughter
              && saved.outcomeCommitted && saved.completionIdentityRequired
              && NoHusbandryCompletionCursor(saved, sourceNarrative.workCompletionDeliveries),
            "HUSBANDRY_COLD_FRESH_PROCESS_PENDING_STATE",
            $"pid={System.Diagnostics.Process.GetCurrentProcess().Id};freshClock={clock.Time};"
            + $"sections={incoming.sections.Count};version={source.version};animal={saved?.animalId};"
            + "original-pending-not-recreated;trainingEligible=false");
        if (failures.Count != 0) yield break;
        GameplayOutcomeSnapshot[] sourceChildren = sourceLedger.exactOutcomes.Where(value =>
            TameResultKey(value) == saved.publicationKey).ToArray();
        GameplayOutcomeSnapshot wrongKindChild = sourceLedger.exactOutcomes.SingleOrDefault(value =>
            TameFact(value, "husbandry-kind") == "tame" && TameFact(value, "animal-id") == saved.animalId);
        Check(sourceChildren.Length == 1 && wrongKindChild != null
              && TameFact(sourceChildren[0], "husbandry-kind") == "slaughter"
              && TameFact(sourceChildren[0], "animal-id") == saved.animalId,
            "HUSBANDRY_COLD_ORIGINAL_CHILD_JOIN", saved.publicationKey);
        if (failures.Count != 0) yield break;
        GameplayOutcomeSnapshot child = sourceChildren[0];
        AnimalHusbandryRuntime owner = husbandryQuery as AnimalHusbandryRuntime;
        IWorkCompletionIdentityDeliveryCommand actual = scope.Container.Resolve<IWorkCompletionIdentityDeliveryCommand>();
        CharacterIdentityEventPublisher publisher = scope.Container.Resolve<CharacterIdentityEventPublisher>();
        WorkCompletionIdentityDeliveryLedger deliveries = scope.Container
            .Resolve<WorkCompletionIdentityDeliveryLedger>();
        IDomainFactPublisher productionFacts = scope.Container
            .Resolve<IDomainFactPublisher>();
        HeldTameCompletionCommand held = new(actual, saved.animalId, "slaughter");
        List<WorkCompletedIdentityEvent> observed = new();
        using IDisposable subscription = gameEvents.Subscribe<WorkCompletedIdentityEvent>(observed.Add);
        Check(owner != null, "HUSBANDRY_COLD_REGISTERED_OWNER", "production singleton");
        if (owner == null) yield break;
        AnimalHusbandryPendingOutcomeState completion = owner.BuildRestore(source).State.PendingOutcomes.Single().Clone();
        WorkCompletedIdentityEvent terminal = AnimalHusbandryCompletionObservation.CreateCompletionEvent(completion);
        int MatchedEvents() => observed.Count(value => value.WorkId == terminal.WorkId
            && value.ProductId == terminal.ProductId && value.Character.Equals(terminal.Character));
        bool SameSections(List<DungeonSaveSectionEnvelope> before, List<DungeonSaveSectionEnvelope> after) =>
            before.Count == after.Count && before.All(left => after.Any(right => right.sectionId == left.sectionId
                && right.sectionVersion == left.sectionVersion && right.payloadJson == left.payloadJson));
        bool PhysicalAndChildExact() => CaptureSlaughterChildren(saved.animalId).Length == 1
            && CaptureSlaughterChildren(saved.animalId)[0].immutablePayloadHash == child.immutablePayloadHash
            && itemRuntime.GetAllStacks().Count(value => value.StackId == saved.carcassStackId
                && value.ItemId == saved.outputItemId && value.Quantity == saved.outputQuantity) == 1
            && saved.outputQuantity == 1 && saved.outputMassGrams == itemRuntime.MassQuery
                .GetDefinitionUnitMass(new ItemDefinitionId(saved.outputItemId)).Value
            && !capture.TryGetCaptured(saved.animalId, out _)
            && !husbandryQuery.TryGetAnimal(new WildlifeInstanceId(saved.animalId), out _)
            && !wildlife.Wildlife.Any(value => value != null && value.WildlifeId == saved.animalId);
        using var routedFacts = new HusbandryCompletionDomainFactTestPublisher(
            productionFacts,
            held,
            publisher,
            deliveries,
            new WorkIdentityAppliedFactMapping(outcomeLedger, outcomeLedger));
        owner.ConstructDomainFacts(routedFacts);
        try
        {
            CharacterActor oldWorker = worker;
            CharacterActor oldCaptive = captiveActor;
            string captiveId = captiveActor.Identity.PersistentId;
            bool restored = gameSaveService.TryRestore(incoming, out DungeonGameRestoreReport report);
            Check(restored, "HUSBANDRY_COLD_OFFICIAL_WORLD_RESTORE",
                $"sections={incoming.sections.Count};errors={string.Join(" | ", report.Errors)};no-DTO-edit");
            if (!restored) yield break;
            bool reboundGrid = world.TryGetGrid(out Grid restoredGrid) && restoredGrid != null;
            grid = restoredGrid;
            CharacterActor[] actors = CharacterActorCollection.DistinctByGameObject(world.Characters)
                .Where(value => value != null && value.HasBeenPublished && !value.IsDetachedRestoreCandidate
                    && !value.IsUnpublishedComposition && value.Identity?.TypedPersistentId.IsValid == true).ToArray();
            worker = actors.SingleOrDefault(value => value.Identity.PersistentId == saved.workerId);
            captiveActor = actors.SingleOrDefault(value => value.Identity.PersistentId == captiveId);
            workerWork = worker != null ? worker.GetComponent<AbilityWork>() : null;
            BuildableObject[] pens = world.Buildings.Where(value => value != null && !value.IsGridDestroyed
                && value.PersistentInstanceId.IsValid && value.PersistentInstanceId.Value == saved.penId).ToArray();
            pen = pens.Length == 1 ? pens[0] : null;
            Check(reboundGrid && worker != null && captiveActor != null && workerWork != null && pen != null
                  && ReferenceEquals(pen.Grid, grid)
                  && actors.GroupBy(value => value.Identity.PersistentId).All(group => group.Count() == 1)
                  && (oldWorker == null || !oldWorker.gameObject.activeInHierarchy)
                  && (oldCaptive == null || !oldCaptive.gameObject.activeInHierarchy),
                "HUSBANDRY_COLD_STABLE_WORLD_REBOUND", $"worker={saved.workerId};pen={saved.penId};captive={captiveId}");
            if (failures.Count != 0) yield break;
            PauseOtherAi();
            AnimalHusbandryPendingOutcomeSaveData current = husbandryPersistence.Capture().pendingOutcomes.SingleOrDefault();
            Check(current != null && JsonUtility.ToJson(current) == JsonUtility.ToJson(saved)
                  && PhysicalAndChildExact() && MatchedEvents() == 0,
                "HUSBANDRY_COLD_PENDING_REQUEST_EFFECT_CHILD_EXACT",
                $"pending={current?.publicationKey};carcass={saved.carcassStackId};events={MatchedEvents()};"
                + "read-only-candidate;effect/child-injection=false");
            if (failures.Count != 0) yield break;
            held.Hold = false;
            float wallDeadline = Time.realtimeSinceStartup + 30f;
            float gameDeadline = clock.Time + 8f;
            while (husbandryPersistence.Capture().pendingOutcomes.Count != 0
                   && Time.realtimeSinceStartup < wallDeadline && clock.Time < gameDeadline) yield return null;
            GameplayOutcomeSnapshot[] identities = outcomeLedger.CaptureGameplayOutcomes().exactOutcomes.Where(value =>
                value.producerId == WorkCompletionIdentityOutcomeIds.ProducerId
                && TameFact(value, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value) == TameResultKey(child)).ToArray();
            WorkCompletedIdentityEvent[] matched = observed.Where(value => value.WorkId == terminal.WorkId
                && value.ProductId == terminal.ProductId && value.Character.Equals(terminal.Character)).ToArray();
            Check(husbandryPersistence.Capture().pendingOutcomes.Count == 0 && PhysicalAndChildExact()
                  && identities.Length == 1 && identities[0].status == GameplayOutcomeStatus.Succeeded
                  && identities[0].sequence > child.sequence && matched.Length == 1
                  && matched[0].ChildResultKey == terminal.ChildResultKey
                  && matched[0].OperationId == terminal.OperationId && matched[0].TerminalOwnerId == terminal.TerminalOwnerId
                  && matched[0].TargetEntityId == terminal.TargetEntityId
                  && matched[0].ContributionWorkUnits == terminal.ContributionWorkUnits,
                "HUSBANDRY_COLD_NORMAL_TICK_IDENTITY_AND_INDEPENDENT_BUS_ONCE",
                $"clock={clock.Time};identity={identities.Length};events={matched.Length};"
                + $"carcass={saved.carcassStackId};ownerTick-only;trainingEligible=false");
            if (failures.Count != 0) yield break;
            List<DungeonSaveSectionEnvelope> beforeReplay = saveRegistry.CaptureAll();
            WorkCompletionIdentityDeliveryResult replay = actual.EnsureApplied(
                WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(terminal));
            List<DungeonSaveSectionEnvelope> afterReplay = saveRegistry.CaptureAll();
            Check(replay.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
                  && MatchedEvents() == 1 && SameSections(beforeReplay, afterReplay) && PhysicalAndChildExact(),
                "HUSBANDRY_COLD_REPLAY_ALL_SECTIONS_AND_BUS_UNCHANGED",
                $"status={replay.Status};sections={beforeReplay.Count};events={MatchedEvents()}");
            if (failures.Count != 0) yield break;
            WriteSlaughterCheckpoint("husbandry-cold-resumed-world.json",
                gameSaveService.ToJson(gameSaveService.Capture(), true));

            // Only the in-memory negative input is altered. Both child rows
            // really exist, but a tame child must not authenticate slaughter.
            DungeonGameSaveData wrongChild = gameSaveService.FromJson(json);
            DungeonSaveSectionEnvelope wrongSection = wrongChild.sections.Single(value => value.sectionId == AnimalHusbandrySaveSection.Id);
            DungeonAnimalHusbandrySaveData wrongPayload = JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(wrongSection.payloadJson);
            wrongPayload.pendingOutcomes.Single().publicationKey = TameResultKey(wrongKindChild);
            wrongSection.payloadJson = JsonUtility.ToJson(wrongPayload);
            wrongChild.manifest = DungeonSaveManifest.Capture(wrongChild.sections);
            held.Hold = true;
            List<DungeonSaveSectionEnvelope> beforeBad = saveRegistry.CaptureAll();
            CharacterActor workerBeforeBad = worker;
            Grid gridBeforeBad = grid;
            bool acceptedBad = gameSaveService.TryRestore(wrongChild, out DungeonGameRestoreReport badReport);
            List<DungeonSaveSectionEnvelope> afterBad = saveRegistry.CaptureAll();
            IRestoreWorldCandidateQuery candidates = scope.Container.Resolve<IRestoreWorldCandidateQuery>();
            string[] changed = beforeBad.Where(left => !afterBad.Any(right => right.sectionId == left.sectionId
                && right.sectionVersion == left.sectionVersion && right.payloadJson == left.payloadJson))
                .Select(value => value.sectionId).ToArray();
            Check(!acceptedBad && !badReport.Success && badReport.Errors.Count > 0
                  && badReport.Errors.Any(error => error.Contains("Husbandry completion identity restore join is invalid:"))
                  && SameSections(beforeBad, afterBad) && MatchedEvents() == 1
                  && world.Characters.Any(value => ReferenceEquals(value, workerBeforeBad))
                  && world.TryGetGrid(out Grid gridAfterBad) && ReferenceEquals(gridBeforeBad, gridAfterBad)
                  && !candidates.TryGetBuildings(out _) && !candidates.TryGetCharacters(out _),
                "HUSBANDRY_COLD_WRONG_KIND_CHILD_REJECTED_BEFORE_PUBLICATION",
                $"accepted={acceptedBad};success={badReport.Success};changed={string.Join(",", changed)};"
                + $"child={TameResultKey(wrongKindChild)};errors={string.Join(" | ", badReport.Errors)};"
                + "manifest-resealed-memory-clone;original-file-unmodified;trainingEligible=false");
            Check(Hash(File.ReadAllBytes(path)) == expectedHash.ToLowerInvariant(),
                "HUSBANDRY_COLD_ORIGINAL_FILE_UNCHANGED", Hash(bytes));
        }
        finally { owner.ConstructDomainFacts(productionFacts); }
    }

    private IEnumerator VerifyCompletedFeedColdResume()
    {
        string sourcePath = Environment.GetEnvironmentVariable(
            CaptivityWildlifeLifecyclePlayModeVerifier.FeedResumeWorldVariable);
        string sourceHash = Environment.GetEnvironmentVariable(
            CaptivityWildlifeLifecyclePlayModeVerifier.FeedResumeHashVariable);
        string observationHash = Environment.GetEnvironmentVariable(
            CaptivityWildlifeLifecyclePlayModeVerifier.FeedObservationHashVariable);
        string qaRoot = Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar;
        bool inputBound = DungeonPersistencePaths.IsIsolatedVerification
            && !string.IsNullOrWhiteSpace(sourcePath) && Path.IsPathRooted(sourcePath)
            && Path.GetFullPath(sourcePath).StartsWith(qaRoot, StringComparison.OrdinalIgnoreCase)
            && File.Exists(sourcePath) && !string.IsNullOrWhiteSpace(sourceHash)
            && !string.IsNullOrWhiteSpace(observationHash);
        Check(inputBound, "FEED_COLD_EXPLICIT_INPUT", sourcePath ?? "missing");
        if (!inputBound) yield break;
        string observationPath = Path.ChangeExtension(sourcePath, ".observation.json");
        Check(File.Exists(observationPath), "FEED_COLD_OBSERVATION_EXISTS", observationPath);
        if (!File.Exists(observationPath)) yield break;
        byte[] sourceBytes = File.ReadAllBytes(sourcePath);
        byte[] observationBytes = File.ReadAllBytes(observationPath);
        using System.Security.Cryptography.SHA256 hasher = System.Security.Cryptography.SHA256.Create();
        string Hash(byte[] bytes) => BitConverter.ToString(hasher.ComputeHash(bytes))
            .Replace("-", string.Empty).ToLowerInvariant();
        CompletedFeedCheckpointObservation observed = JsonUtility.FromJson<CompletedFeedCheckpointObservation>(
            System.Text.Encoding.UTF8.GetString(observationBytes));
        bool hashesMatch = string.Equals(Hash(sourceBytes), sourceHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Hash(observationBytes), observationHash, StringComparison.OrdinalIgnoreCase)
            && observed != null && observed.schema == "dungeonstory-completed-feed-checkpoint@1"
            && observed.worldSha256 == Hash(sourceBytes)
            && !observed.trainingEligible && !observed.coldProcessResumeExecuted
            && observed.consumedQuantity == 1 && observed.consumedMassGrams > 0
            && observed.feedSequence == 1 && !string.IsNullOrWhiteSpace(observed.commitId);
        Check(hashesMatch, "FEED_COLD_SOURCE_HASHES", $"world={Hash(sourceBytes)};observation={Hash(observationBytes)}");
        if (!hashesMatch) yield break;
        DungeonGameSaveData incoming = gameSaveService.FromJson(System.Text.Encoding.UTF8.GetString(sourceBytes));
        CircusSaveData sourceCircus = JsonUtility.FromJson<CircusSaveData>(incoming.sections
            .Single(section => section.sectionId == CircusSaveSection.Id).payloadJson);
        CapturedWildlifeState sourceState = sourceCircus.capturedWildlife.SingleOrDefault(
            value => value != null && value.wildlifeId == observed.animalId);
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        float rawClock = clock.Time;
        Check(rawClock < observed.gameTime && incoming.sections.Count == baseline.Count
              && sourceCircus.version == CircusSaveData.CurrentVersion
              && sourceState != null && sourceState.penId == observed.penId
              && sourceState.nextFeedOperationSequence == observed.feedSequence
              && CapturedWildlifeFeedOutbox.HasEmptyProvenance(sourceState),
            "FEED_COLD_FRESH_TIMELINE_AND_SAVED_STATE",
            $"rawClock={rawClock};savedClock={observed.gameTime};sections={incoming.sections.Count};animal={observed.animalId}");
        if (failures.Count != 0) yield break;
        string workerId = worker.Identity.PersistentId;
        string captiveId = captiveActor.Identity.PersistentId;
        CharacterActor oldWorker = worker;
        CharacterActor oldCaptive = captiveActor;
        IRestoreWorldCandidateQuery candidateQuery = scope.Container.Resolve<IRestoreWorldCandidateQuery>();
        IRestoreWorldCandidatePublisher candidatePublisher = scope.Container.Resolve<IRestoreWorldCandidatePublisher>();
        bool cleanCandidateBoundary = !candidateQuery.TryGetBuildings(out _)
            && !candidateQuery.TryGetCharacters(out _);
        Check(cleanCandidateBoundary, "FEED_COLD_DETACHED_INDEX_EMPTY", "before official restore");
        if (!cleanCandidateBoundary) yield break;
        List<DungeonSaveSectionEnvelope> beforePartial = saveRegistry.CaptureAll();
        bool partialRejected = false;
        try
        {
            candidatePublisher.SetFacilityCandidate(grid, world.Buildings.ToArray());
            CaptivitySaveData sourceCaptivity = JsonUtility.FromJson<CaptivitySaveData>(incoming.sections
                .Single(section => section.sectionId == CaptivitySaveSection.Id).payloadJson);
            captivityPersistence.BuildRestore(sourceCaptivity);
        }
        catch (InvalidOperationException exception)
        {
            partialRejected = exception.Message.Contains("complete detached facility and character worlds");
            rows.Add("INFO\tFEED_COLD_PARTIAL_CANDIDATE_REJECTION\t" + exception.Message);
        }
        finally
        {
            candidatePublisher.ClearFacilityCandidate();
        }
        List<DungeonSaveSectionEnvelope> afterPartial = saveRegistry.CaptureAll();
        bool partialUnchanged = beforePartial.Count == afterPartial.Count
            && beforePartial.All(before => afterPartial.Any(after => after.sectionId == before.sectionId
                && after.sectionVersion == before.sectionVersion && after.payloadJson == before.payloadJson));
        Check(partialRejected && partialUnchanged && !candidateQuery.TryGetBuildings(out _)
              && !candidateQuery.TryGetCharacters(out _),
            "FEED_COLD_PARTIAL_CANDIDATE_REJECTED_NO_LIVE_FALLBACK",
            $"rejected={partialRejected};allSectionsUnchanged={partialUnchanged};trainingEligible=false");
        if (failures.Count != 0) yield break;
        // Corrupt only an in-memory QA copy: the original checkpoint remains
        // immutable. The named building exists in incoming world but is an
        // animal pen, not valid captive housing, so DTO validation alone is
        // insufficient and the real world-bound staging join must reject it.
        DungeonGameSaveData wrongHousing = gameSaveService.FromJson(
            System.Text.Encoding.UTF8.GetString(sourceBytes));
        DungeonSaveSectionEnvelope wrongHousingSection = wrongHousing.sections
            .Single(section => section.sectionId == CaptivitySaveSection.Id);
        CaptivitySaveData wrongHousingPayload = JsonUtility.FromJson<CaptivitySaveData>(
            wrongHousingSection.payloadJson);
        wrongHousingPayload.captives.Single(value => value.captiveId == captiveId)
            .housingBuildingId = observed.penId;
        wrongHousingSection.payloadJson = JsonUtility.ToJson(wrongHousingPayload, false);
        // Seal the intentionally malformed in-memory world with its own
        // manifest so rejection must come from the cross-domain join, not a
        // stale hash. The immutable source bytes/manifest are not changed.
        wrongHousing.manifest = DungeonSaveManifest.Capture(wrongHousing.sections);
        List<DungeonSaveSectionEnvelope> beforeWrongHousing = saveRegistry.CaptureAll();
        bool wrongHousingAccepted = gameSaveService.TryRestore(wrongHousing,
            out DungeonGameRestoreReport wrongHousingReport);
        List<DungeonSaveSectionEnvelope> afterWrongHousing = saveRegistry.CaptureAll();
        bool wrongHousingUnchanged = beforeWrongHousing.Count == afterWrongHousing.Count
            && beforeWrongHousing.All(before => afterWrongHousing.Any(after =>
                after.sectionId == before.sectionId && after.sectionVersion == before.sectionVersion
                && after.payloadJson == before.payloadJson));
        Check(!wrongHousingAccepted && wrongHousingUnchanged
              && wrongHousingReport.Errors.Any(error => error.Contains("references invalid housing"))
              && ReferenceEquals(worker, oldWorker) && ReferenceEquals(captiveActor, oldCaptive)
              && world.TryGetGrid(out Grid unchangedGrid) && ReferenceEquals(grid, unchangedGrid)
              && !candidateQuery.TryGetBuildings(out _) && !candidateQuery.TryGetCharacters(out _),
            "FEED_COLD_BAD_HOUSING_STAGING_REJECTED_NO_MUTATION",
            $"accepted={wrongHousingAccepted};allSectionsUnchanged={wrongHousingUnchanged};"
            + $"errors={string.Join(" | ", wrongHousingReport.Errors)};trainingEligible=false");
        if (failures.Count != 0) yield break;
        bool restored = gameSaveService.TryRestore(incoming, out DungeonGameRestoreReport restoreReport);
        Check(restored, "FEED_COLD_OFFICIAL_WORLD_RESTORE",
            $"sections={incoming.sections.Count};errors={string.Join(" | ", restoreReport.Errors)};no-DTO-edit;trainingEligible=false");
        if (!restored) yield break;
        bool gridRebound = world.TryGetGrid(out Grid restoredGrid) && restoredGrid != null;
        CharacterActor[] actors = CharacterActorCollection.DistinctByGameObject(world.Characters)
            .Where(value => value != null && value.HasBeenPublished
                && !value.IsDetachedRestoreCandidate && !value.IsUnpublishedComposition
                && value.Identity != null && value.Identity.TypedPersistentId.IsValid).ToArray();
        worker = actors.SingleOrDefault(value => value.Identity.PersistentId == workerId);
        captiveActor = actors.SingleOrDefault(value => value.Identity.PersistentId == captiveId);
        workerWork = worker != null ? worker.GetComponent<AbilityWork>() : null;
        grid = restoredGrid;
        BuildableObject[] pens = world.Buildings.Where(value => value != null && !value.IsGridDestroyed
            && value.PersistentInstanceId.IsValid && value.PersistentInstanceId.Value == observed.penId).ToArray();
        pen = pens.Length == 1 ? pens[0] : null;
        WildlifeActor[] animals = wildlife.Wildlife.Where(value => value != null
            && value.WildlifeId == observed.animalId).ToArray();
        bool rebound = gridRebound && worker != null && captiveActor != null && workerWork != null
            && pens.Length == 1 && ReferenceEquals(pen.Grid, grid) && animals.Length == 1
            && actors.GroupBy(value => value.Identity.PersistentId).All(group => group.Count() == 1)
            && (oldWorker == null || !oldWorker.gameObject.activeInHierarchy)
            && (oldCaptive == null || !oldCaptive.gameObject.activeInHierarchy);
        Check(rebound, "FEED_COLD_STABLE_ID_REBOUND",
            $"animal={observed.animalId}:{animals.Length};pen={observed.penId}:{pens.Length};worker={workerId};captive={captiveId}");
        if (!rebound) yield break;
        PauseOtherAi();
        WildlifeActor animal = animals[0];
        capture.TryGetCaptured(observed.animalId, out CapturedWildlifeState state);
        string destination = WildlifeCareInputOwnerAuthority.FormatDestinationId(observed.penId);
        bool exact = state != null && state.nextFeedOperationSequence == observed.feedSequence
            && CapturedWildlifeFeedOutbox.HasEmptyProvenance(state)
            && state.lastFeedItemId == observed.feedItemId && state.penId == observed.penId
            && animal.LastCaptiveFeedCommitId == observed.commitId
            && animal.Hunger == observed.hungerAfter && animal.CurrentHealth == observed.healthAfter
            && CountFacilityItem(destination, observed.feedItemId) == observed.remainingQuantity
            && !batchDispositions.TryGetPending(observed.operationId, out _);
        Check(exact, "FEED_COLD_COMPLETED_CONSUMPTION_AND_EFFECT_EXACT",
            $"sequence={state?.nextFeedOperationSequence};commit={animal.LastCaptiveFeedCommitId};"
            + $"quantity={CountFacilityItem(destination, observed.feedItemId)};hunger={animal.Hunger:R};health={animal.CurrentHealth}");
        if (!exact) yield break;
        List<DungeonSaveSectionEnvelope> beforeReplay = saveRegistry.CaptureAll();
        bool accepted = animal.TryApplyCaptiveFeedOutcome(observed.commitId,
            observed.hungerAfter, observed.healthAfter, out bool applied);
        List<DungeonSaveSectionEnvelope> afterReplay = saveRegistry.CaptureAll();
        string[] changedSections = beforeReplay.Where(before => !afterReplay.Any(after =>
            after.sectionId == before.sectionId && after.sectionVersion == before.sectionVersion
            && after.payloadJson == before.payloadJson)).Select(value => value.sectionId).ToArray();
        Check(accepted && !applied && changedSections.Length == 0
              && beforeReplay.Count == afterReplay.Count,
            "FEED_COLD_SAME_COMMIT_REPLAY_NO_MUTATION",
            $"accepted={accepted};applied={applied};changedSections={string.Join(",", changedSections)};sections={beforeReplay.Count}");
        // Restoration must also resume the real periodic owner, not leave its
        // old process timestamp hundreds of seconds ahead of the fresh clock.
        float restoredDue = state.nextCareAt;
        bool careDueBounded = restoredDue >= clock.Time
            && restoredDue <= clock.Time + CapturedWildlifeState.CareIntervalSeconds + 0.001f
            && Math.Abs(restoredDue - clock.Time - sourceState.remainingCareSeconds) <= 0.001f
            && sourceState.nextCareAt == 0f;
        Check(careDueBounded, "FEED_COLD_NORMAL_CARE_CADENCE",
            $"clock={clock.Time:R};due={restoredDue:R};savedRemaining={sourceState.remainingCareSeconds:R};normalInterval=5");
        if (!careDueBounded || failures.Count != 0) yield break;
        float wallDeadline = Time.realtimeSinceStartup + 30f;
        float gameDeadline = clock.Time + 8f;
        while (state.nextCareAt <= restoredDue && clock.Time < gameDeadline
               && Time.realtimeSinceStartup < wallDeadline)
        {
            yield return null;
            if (!capture.TryGetCaptured(observed.animalId, out state) || state == null)
            {
                Check(false, "FEED_COLD_OWNER_TARGET_LOST", observed.animalId);
                yield break;
            }
        }
        Check(state.nextCareAt > restoredDue && state.nextFeedOperationSequence == observed.feedSequence
              && CapturedWildlifeFeedOutbox.HasEmptyProvenance(state)
              && animal.LastCaptiveFeedCommitId == observed.commitId
              && CountFacilityItem(destination, observed.feedItemId) == observed.remainingQuantity,
            "FEED_COLD_OWNER_TICK_WITHOUT_SECOND_CONSUMPTION",
            $"clock={clock.Time:R};due={restoredDue:R}->{state.nextCareAt:R};sequence={state.nextFeedOperationSequence}");
        Check(Hash(File.ReadAllBytes(sourcePath)) == observed.worldSha256
              && Hash(File.ReadAllBytes(observationPath)) == observationHash.ToLowerInvariant(),
            "FEED_COLD_SOURCE_FILES_UNCHANGED", "immutable original checkpoint and observation;trainingEligible=false");
    }

    private bool RestoreCaptivity(CaptivitySaveData payload) =>
        RestoreSection(
            CaptivitySaveSection.Id,
            JsonUtility.ToJson(Clone(payload)),
            captivityPersistence as IDungeonRestoreTransactionParticipant);

    private bool RestoreEscapeReadyCaptivity(string captiveId)
    {
        CaptivitySaveData payload = Clone(confinedPayload);
        CaptiveState state = payload?.captives?.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.captiveId,
                captiveId,
                StringComparison.Ordinal));
        if (state == null)
        {
            fixtureStage = "escape-ready-captive-missing:" + captiveId;
            return false;
        }

        // These values satisfy the production false-compliance formula.  The
        // persisted flag is also explicit so the invasion event can fire in
        // the same tick as publication, before the next periodic recalculation.
        state.compliance = 100f;
        state.will = 0f;
        state.fear = 100f;
        state.grudge = 100f;
        state.trust = 0f;
        state.escapeRisk = 100f;
        state.falseCompliance = true;
        return RestoreCaptivity(payload);
    }

    private bool RestoreCircus(CircusSaveData payload,
        DungeonGameSaveData completedWorld = null)
    {
        // Circus validates physical receipts against the incoming staged world.
        // Reuse every registered section/participant, not dependency ID markers.
        // This is test-only roster preparation; subsequent feed/tame effects
        // must still be produced by the normal runtime and AI paths.
        string workerId = worker.Identity.PersistentId;
        string captiveId = captiveActor.Identity.PersistentId;
        string housingId = housing.RequirePersistentInstanceId().Value;
        string penId = pen.RequirePersistentInstanceId().Value;
        string faultPenId = faultPen != null && !faultPen.IsGridDestroyed
            ? faultPen.RequirePersistentInstanceId().Value : null;
        string[] fixtureIds = fixtureBuildingSnapshots.Select(snapshot =>
                snapshot.Building != null && !snapshot.Building.IsGridDestroyed
                    ? snapshot.Building.RequirePersistentInstanceId().Value
                    : null)
            .ToArray();
        CharacterActor previousCaptive = captiveActor;
        captiveActor.SetAiPaused(true);
        captiveActor.characterType = captiveOldType;
        Check(captiveActor.characterType == captiveOldType
              && captiveActor.Identity.PersistentId == captiveId,
            "ANIMAL_CARE_CAPTIVE_ROLE_RESTORE_PREPARATION",
            $"id={captiveId};role={captiveActor.characterType};"
            + "identityUnchanged=True;activationInjected=false;trainingEligible=false");
        DungeonGameSaveData preparationSave = completedWorld ?? gameSaveService.Capture();
        List<DungeonSaveSectionEnvelope> envelopes = preparationSave.sections;
        if (completedWorld == null)
        {
            envelopes.Single(value => value.sectionId == CircusSaveSection.Id)
                .payloadJson = JsonUtility.ToJson(Clone(payload));
            preparationSave.manifest = DungeonSaveManifest.Capture(envelopes);
        }
        bool restored = gameSaveService.TryRestore(preparationSave,
            out DungeonGameRestoreReport report);
        rows.Add("INFO\tANIMAL_CARE_REGISTERED_WORLD_RESTORE\t"
            + $"sections={envelopes.Count};success={restored};"
            + "official-save-service-preflight/manifest/completed-hooks;trainingEligible=false");
        if (!restored)
        {
            rows.Add("INFO\tCIRCUS_RESTORE_ERROR\t" + string.Join(" | ", report.Errors));
            return false;
        }

        bool gridRebound = world.TryGetGrid(out Grid restoredGrid)
            && restoredGrid != null;
        if (!gridRebound)
        {
            Check(false, "ANIMAL_CARE_WORLD_ID_REBOUND", "published grid missing");
            return false;
        }
        grid = restoredGrid;
        CharacterActor[] publishedActors = CharacterActorCollection
            .DistinctByGameObject(world.Characters)
            .Where(value => value != null && value.HasBeenPublished
                && !value.IsUnpublishedComposition && !value.IsDetachedRestoreCandidate
                && !value.IsDead && value.Identity != null
                && value.Identity.TypedPersistentId.IsValid)
            .ToArray();
        CharacterActor[] workers = publishedActors
            .Where(value => value.Identity.PersistentId == workerId).ToArray();
        CharacterActor[] captives = publishedActors
            .Where(value => value.Identity.PersistentId == captiveId).ToArray();
        string[] duplicateIds = publishedActors
            .GroupBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        bool actorsRebound = workers.Length == 1 && captives.Length == 1
            && duplicateIds.Length == 0
            && (previousCaptive == null || !previousCaptive.gameObject.activeInHierarchy);
        worker = workers.Length == 1 ? workers[0] : null;
        captiveActor = captives.Length == 1 ? captives[0] : null;
        workerWork = worker != null ? worker.GetComponent<AbilityWork>() : null;

        BuildableObject[] buildings = world.Buildings
            .Where(value => value != null && !value.IsGridDestroyed).ToArray();
        bool buildingsRebound = true;
        fixtureBuildings.Clear();
        for (int index = 0; index < fixtureBuildingSnapshots.Count; index++)
        {
            FixtureBuildingSnapshot old = fixtureBuildingSnapshots[index];
            string id = fixtureIds[index];
            if (id == null) continue;
            BuildableObject[] matches = buildings.Where(value =>
                value.PersistentInstanceId.IsValid
                && value.PersistentInstanceId.Value == id).ToArray();
            BuildableObject replacement = matches.Length == 1 ? matches[0] : null;
            buildingsRebound &= replacement != null
                && ReferenceEquals(replacement.Grid, grid)
                && old.Positions.All(position =>
                    grid.GetGridCell(position)?.ContainsOccupant(
                        old.Layer, replacement) == true);
            fixtureBuildingSnapshots[index] = new FixtureBuildingSnapshot(
                replacement, old.Layer, old.Positions, old.ConnectPositions);
            if (replacement != null) fixtureBuildings.Add(replacement);
        }
        housing = buildings.SingleOrDefault(value => value.PersistentInstanceId.IsValid
            && value.PersistentInstanceId.Value == housingId);
        pen = buildings.SingleOrDefault(value => value.PersistentInstanceId.IsValid
            && value.PersistentInstanceId.Value == penId);
        // Preserve the same live fault pen when restoring before pen-loss;
        // after that branch it is absent and must not be recreated.
        faultPen = faultPenId == null ? null : buildings.SingleOrDefault(value =>
            value.PersistentInstanceId.IsValid
            && value.PersistentInstanceId.Value == faultPenId);
        rooms.Clear();
        RoomInstance penRoom = null;
        bool roomRebound = pen != null && rooms.TryGetRoom(pen, out penRoom)
            && penRoom != null && penRoom.IsUsable;
        fixtureRoom = penRoom;
        bool rebound = actorsRebound && workerWork != null && buildingsRebound
            && housing != null && pen != null && roomRebound;
        Check(rebound, "ANIMAL_CARE_WORLD_ID_REBOUND",
            $"worker={workerId}:{workers.Length};captive={captiveId}:{captives.Length};"
            + $"duplicates={string.Join(",", duplicateIds)};buildings={buildingsRebound};"
            + $"room={fixtureRoom?.Id};oldCaptiveRetired={previousCaptive == null || !previousCaptive.gameObject.activeInHierarchy};"
            + "registered-transaction=true;trainingEligible=false");
        if (!rebound) return false;
        PauseOtherAi();
        return true;
    }

    private bool RestoreHusbandry(DungeonAnimalHusbandrySaveData payload) =>
        RestoreSection(
            AnimalHusbandrySaveSection.Id,
            JsonUtility.ToJson(payload),
            participant: null);

    private void BeginInvasionEventDiagnostics()
    {
        invasionStartedProbeSubscription ??=
            gameEvents.Subscribe<InvasionStartedEvent>(_ =>
            {
                invasionStartedEventCount++;
                RecordInvasionEvent("started");
            });
        invasionResolvedProbeSubscription ??=
            gameEvents.Subscribe<InvasionResolvedEvent>(resolved =>
            {
                invasionResolvedEventCount++;
                RecordInvasionEvent(
                    $"resolved:defended={resolved.defended}:"
                    + $"risk={resolved.residualRisk:0.###}");
            });
        invasionCandidateProbeSubscription ??=
            gameEvents.Subscribe<InvasionCandidateEvent>(_ =>
            {
                invasionCandidateEventCount++;
                RecordInvasionEvent("candidate");
            });
        activeIncidentsProbeSubscription ??=
            gameEvents.Subscribe<SettlementActiveIncidentsChangedEvent>(change =>
            {
                activeIncidentsChangedEventCount++;
                RecordInvasionEvent(
                    $"incidents:epoch={change.EpochId}:"
                    + $"desired={change.DesiredLevel}:"
                    + $"committed={change.CommittedLevel}:"
                    + $"count={change.ActiveIncidentCount}");
            });
    }

    private void EndInvasionEventDiagnostics()
    {
        invasionStartedProbeSubscription?.Dispose();
        invasionResolvedProbeSubscription?.Dispose();
        invasionCandidateProbeSubscription?.Dispose();
        activeIncidentsProbeSubscription?.Dispose();
        invasionStartedProbeSubscription = null;
        invasionResolvedProbeSubscription = null;
        invasionCandidateProbeSubscription = null;
        activeIncidentsProbeSubscription = null;
    }

    private void RecordInvasionEvent(string detail)
    {
        invasionEventTrace.Add(
            $"frame={Time.frameCount};hour={calendar?.AbsoluteHour};{detail}");
        const int maximumTraceEntries = 48;
        if (invasionEventTrace.Count > maximumTraceEntries)
        {
            invasionEventTrace.RemoveRange(
                0,
                invasionEventTrace.Count - maximumTraceEntries);
        }
    }

    private string DescribeInvasionIncident()
    {
        DungeonStory.Infrastructure.SettlementThreatAlertSaveData save =
            settlementAlertPersistence?.CaptureAlertSaveData();
        DungeonStory.Infrastructure.SettlementIncidentSaveData incident =
            save?.incidents?.FirstOrDefault(value =>
                value != null
                && string.Equals(
                    value.incidentId,
                    "incident:invasion:active",
                    StringComparison.Ordinal));
        return incident == null
            ? "missing"
            : $"active={incident.active},revision={incident.revision},"
              + $"level={incident.requiredLevel},source={incident.sourceId},"
              + $"detail={incident.diagnostic}";
    }

    private bool RestoreSection(
        string sectionId,
        string payloadJson,
        IDungeonRestoreTransactionParticipant participant)
    {
        IDungeonSaveSection section = saveRegistry.OrderedSections
            .Single(candidate => candidate.SectionId == sectionId);
        List<IDungeonSaveSection> sections = section.DependsOn
            .Distinct(StringComparer.Ordinal)
            .Select(dependency =>
                (IDungeonSaveSection)new DependencyMarkerSection(dependency))
            .Append(section)
            .ToList();
        DungeonSaveSectionRegistry registry = new(
            sections,
            aggregateStore,
            participant != null
                ? new[] { participant }
                : Array.Empty<IDungeonRestoreTransactionParticipant>());
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        envelopes.Single(value => value.sectionId == sectionId).payloadJson = payloadJson;
        DungeonGameRestoreReport report = new();
        bool restored = registry.RestoreAll(envelopes, report);
        if (!restored) rows.Add($"INFO\t{sectionId}_RESTORE_ERROR\t{string.Join(" | ", report.Errors)}");
        return restored;
    }

    private static T Clone<T>(T value) =>
        JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < deadline && !condition())
            yield return null;
    }

    private void PauseOtherAi()
    {
        foreach (CharacterActor actor in world.Characters.Where(value => value != null))
        {
            AIBrain brain = actor.Brain;
            if (brain == null) continue;
            if (!pausedAi.Any(state => ReferenceEquals(state.Behaviour, brain)))
                pausedAi.Add(new MonoBehaviourState(brain, brain.enabled));
            if (actor != worker) brain.enabled = false;
        }
    }

    private void RecordAnimalCareAccounting(string stage)
    {
        EmergencyReserveSnapshot snapshot = emergencyAccounting.CaptureSnapshot();
        List<string> details = new();
        int listedOperations = 0;
        foreach (CharacterActor actor in world.Characters
                     .Where(value => value != null && value.Identity != null)
                     .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal))
        {
            string actorId = actor.Identity.PersistentId;
            bool hasOperation = emergencyAccounting.TryGetWorkerOperation(
                actorId,
                out EmergencyWorkLedgerEntry entry);
            if (hasOperation) listedOperations++;
            AbilityWork actorWork = actor.GetComponent<AbilityWork>();
            details.Add(
                $"actor={actorId};targetWorker={ReferenceEquals(actor, worker)};"
                + $"lifecycle={actor.CurrentLifecycleState};brainEnabled={actor.Brain?.enabled};"
                + $"working={actorWork?.isWorking};routine={actorWork?.HasActiveWorkRoutineForDiagnostics};"
                + $"operation={(hasOperation ? entry.OperationId : "none")};"
                + $"operationWorker={(hasOperation ? entry.WorkerId : "none")};"
                + $"type={(hasOperation ? entry.WorkTypeId.ToString() : "none")};"
                + $"remainingMilliWu={(hasOperation ? entry.RemainingMilliWu : 0)};"
                + $"sequence={(hasOperation ? entry.MutationSequence : 0)}");
        }
        rows.Add($"INFO\tANIMAL_CARE_ACCOUNTING_{stage}\t"
                 + $"frame={Time.frameCount};gameTime={Time.time:0.###};"
                 + $"globalActive={snapshot.ActiveOperationCount};listedOperations={listedOperations};"
                 + string.Join(" | ", details));
    }

    private void Cleanup()
    {
        EndInvasionEventDiagnostics();
        RemoveFaultWalls();
        if (workerWork != null)
            workerWork.SetWorkPriority(BuiltInWorkTypeIds.AnimalCare, oldAnimalCarePriority);
        foreach (MonoBehaviourState state in pausedAi)
            if (state.Behaviour != null) state.Behaviour.enabled = state.Enabled;

        bool fixtureOccupantsRemoved = true;
        for (int index = fixtureBuildingSnapshots.Count - 1; index >= 0; index--)
        {
            FixtureBuildingSnapshot snapshot = fixtureBuildingSnapshots[index];
            BuildableObject building = snapshot.Building;
            if (building != null)
            {
                world?.UnregisterBuilding(building);
                building.Grid?.RemoveOccupant(
                    building,
                    snapshot.Layer,
                    snapshot.Positions,
                    snapshot.ConnectPositions);
            }
            fixtureOccupantsRemoved &= snapshot.Positions.All(position =>
                !ReferenceEquals(
                    grid?.GetGridCell(position)?.GetOccupant(snapshot.Layer),
                    snapshot.Building));
            if (building != null)
                Destroy(building.gameObject);
        }
        Check(
            fixtureOccupantsRemoved,
            "FIXTURE_OCCUPANTS_REMOVED",
            $"count={fixtureBuildings.Count};exact={fixtureOccupantsRemoved}");
        fixtureBuildings.Clear();
        fixtureBuildingSnapshots.Clear();

        bool areasRestored = true;
        foreach (AreaSnapshot snapshot in areaSnapshots)
        {
            GridCell cell = grid?.GetGridCell(snapshot.Position);
            if (cell == null)
            {
                areasRestored = false;
                continue;
            }
            grid.SetAreaType(snapshot.Position, snapshot.AreaType);
            areasRestored &= cell.AreaType == snapshot.AreaType;
        }
        Check(
            areasRestored,
            "FIXTURE_AREA_RESTORE",
            $"count={areaSnapshots.Count};exact={areasRestored}");
        areaSnapshots.Clear();
        rooms?.Clear();

        try
        {
            if (saveRegistry != null && baseline != null)
            {
                string preparedCaptiveId = null;
                if (!transportOnly && captiveActor != null)
                {
                    preparedCaptiveId = captiveActor.Identity.PersistentId;
                    captiveActor.SetAiPaused(true);
                    // The fixture borrowed a staff actor and changed only its
                    // role. An escaped Intruder is no longer captivity-owned,
                    // so restore that preparation before the world owner finds
                    // and retires the old staff instance during baseline restore.
                    captiveActor.characterType = captiveOldType;
                    Check(captiveActor.characterType == captiveOldType
                        && captiveActor.Identity.PersistentId == preparedCaptiveId,
                        "FIXTURE_CAPTIVE_ROLE_RESTORED_FOR_BASELINE",
                        $"id={preparedCaptiveId};role={captiveActor.characterType};"
                        + "identityUnchanged=True;activationInjected=false;trainingEligible=false");
                }
                if (!gameSaveService.TryRestore(baselineSaveData,
                        out DungeonGameRestoreReport report))
                    Check(false, "BASELINE_RESTORE", string.Join(" | ", report.Errors));
                else
                {
                    CharacterActor[] publishedActors = CharacterActorCollection
                        .DistinctByGameObject(world.Characters)
                        .Where(actor => actor != null
                            && actor.HasBeenPublished
                            && !actor.IsUnpublishedComposition
                            && !actor.IsDetachedRestoreCandidate
                            && !actor.IsDead
                            && actor.Progression != null
                            && actor.Identity != null
                            && actor.Identity.TypedPersistentId.IsValid)
                        .ToArray();
                    string[] duplicateIds = publishedActors
                        .GroupBy(actor => actor.Identity.PersistentId, StringComparer.Ordinal)
                        .Where(group => group.Count() > 1)
                        .Select(group => group.Key)
                        .ToArray();
                    int restoredCaptiveCount = preparedCaptiveId == null ? 0
                        : publishedActors.Count(actor => actor.Identity.PersistentId == preparedCaptiveId);
                    bool oldCaptiveRetired = preparedCaptiveId == null
                        || captiveActor == null
                        || !captiveActor.gameObject.activeInHierarchy;
                    Check(duplicateIds.Length == 0 && oldCaptiveRetired
                        && (preparedCaptiveId == null || restoredCaptiveCount == 1),
                        "FIXTURE_CHARACTER_IDENTITY_RESTORE",
                        $"published={publishedActors.Length};duplicates={string.Join(",", duplicateIds)};"
                        + $"captive={preparedCaptiveId};restoredCount={restoredCaptiveCount};"
                        + $"oldInstanceRetired={oldCaptiveRetired}");
                    Grid restoredGrid = null;
                    bool restoredGridResolved =
                        world?.TryGetGrid(out restoredGrid) == true
                        && restoredGrid != null;
                    List<string> buildingRestoreMismatches = new();
                    foreach (PreparedOriginalBuildingSnapshot snapshot in preparedOriginalBuildings)
                    {
                        ModularFacilityBuildingSaveData saved = snapshot.Saved;
                        BuildableObject[] matching = world.Buildings.Where(value =>
                            value != null && value.PersistentInstanceId.IsValid
                            && value.PersistentInstanceId.Value == saved.persistentInstanceId)
                            .ToArray();
                        BuildableObject restored = matching.Length == 1 ? matching[0] : null;
                        bool exact = restoredGridResolved && restored != null
                            && !restored.IsBuildingDestroyed && !restored.IsDetachedRestoreCandidate
                            && restored.gameObject.activeInHierarchy
                            && !ReferenceEquals(restored, snapshot.Original)
                            && (snapshot.Original == null || snapshot.Original.IsBuildingDestroyed)
                            && ReferenceEquals(restored.Grid, restoredGrid)
                            && restored.BuildingData != null
                            && restored.BuildingData.id == saved.buildingId
                            && restored.BuildingData.GetFacilityCode() == saved.code
                            && restored.BuildingData.Placement.Layer == saved.layer
                            && restored.BuildingData.Placement.Width == saved.width
                            && restored.BuildingData.Placement.Height == saved.height
                            && restored.centerPos == new Vector2Int(saved.centerX, saved.centerY)
                            && restored.FacilityLevel == saved.facilityLevel
                            && restored.IsDamaged == saved.isDamaged
                            && (restored is not IWarehouseFacility warehouse
                                || world.Warehouses.Count(value => ReferenceEquals(value, warehouse)) == 1)
                            && (restored is not IRetailFacility retail
                                || world.RetailFacilities.Count(value => ReferenceEquals(value, retail)) == 1)
                            && restored.buildPoses.Count == snapshot.Positions.Count
                            && snapshot.Positions.All(position =>
                                restored.buildPoses.Contains(position)
                                && ReferenceEquals(restoredGrid.GetGridCell(position)
                                    ?.GetOccupant(saved.layer), restored));
                        if (!exact)
                            buildingRestoreMismatches.Add($"{saved.persistentInstanceId}:"
                                + $"definition={saved.buildingId};count={matching.Length};"
                                + $"restored={restored?.BuildingData?.id}@{restored?.centerPos}");
                    }
                    Check(restoredGridResolved && buildingRestoreMismatches.Count == 0,
                        "FIXTURE_ORIGINAL_BUILDINGS_BASELINE_RESTORE",
                        $"count={preparedOriginalBuildings.Count};"
                        + $"exact={restoredGridResolved && buildingRestoreMismatches.Count == 0};"
                        + $"mismatches=[{string.Join(" | ", buildingRestoreMismatches)}];"
                        + "identity=id+definition+placement+single-world/grid-registration;"
                        + "actual-whole-world-restore;trainingEligible=false");
                    List<string> wildlifeRestoreMismatches = new();
                    bool wildlifeRestored = restoredGridResolved
                        && displacedWildlife.All(snapshot =>
                    {
                        WildlifeActor[] matching = wildlife?.Wildlife.Where(value =>
                            value != null
                            && string.Equals(
                                value.WildlifeId,
                                snapshot.WildlifeId,
                                StringComparison.Ordinal)).ToArray()
                            ?? Array.Empty<WildlifeActor>();
                        WildlifeActor restored = matching.FirstOrDefault();
                        WildlifeActor occupant =
                            restoredGrid?.GetGridCell(snapshot.Position)?.GetOccupant(
                                GridLayer.Wildlife) as WildlifeActor;
                        bool exact = restoredGridResolved
                            && matching.Length == 1
                            && restored != null
                            && restored.GridPosition == snapshot.Position
                            && string.Equals(
                                restored.SpeciesId,
                                snapshot.SpeciesId,
                                StringComparison.Ordinal)
                            && occupant != null
                            && occupant.GridPosition == snapshot.Position
                            && string.Equals(
                                occupant.WildlifeId,
                                snapshot.WildlifeId,
                                StringComparison.Ordinal)
                            && string.Equals(
                                occupant.SpeciesId,
                                snapshot.SpeciesId,
                                StringComparison.Ordinal);
                        if (!exact)
                        {
                            wildlifeRestoreMismatches.Add(
                                $"{snapshot.WildlifeId}:{snapshot.SpeciesId}@{snapshot.Position}"
                                + $"=>population={matching.Length};restored={restored?.SpeciesId}@{restored?.GridPosition};"
                                + $"occupant={occupant?.WildlifeId}:{occupant?.SpeciesId}@{occupant?.GridPosition}");
                        }
                        return exact;
                    });
                    Check(
                        wildlifeRestored,
                        "FIXTURE_WILDLIFE_V18_RESTORE",
                        $"count={displacedWildlife.Count};exact={wildlifeRestored};"
                        + (wildlifeRestoreMismatches.Count == 0
                            ? $"grid={restoredGridResolved};identity=id+species+position+grid-occupancy"
                            : string.Join(" | ", wildlifeRestoreMismatches)));
                }
            }
        }
        catch (Exception exception)
        {
            Check(false, "BASELINE_RESTORE_EXCEPTION", exception.Message);
        }
        displacedWildlife.Clear();
        preparedOriginalBuildings.Clear();
    }

    private void Check(bool success, string id, string detail)
    {
        rows.Add($"{(success ? "PASS" : "FAIL")}\t{id}\t{detail}");
        if (!success) failures.Add(id + ": " + detail);
        if (id == "TRANSPORT_CAPTURE_ORDER_PARENT_RESULT_ONCE"
            || id == "TRANSPORT_SUCCESS_ATOMIC_PICKUP_OWNERSHIP"
            || id == "TRANSPORT_CARRIER_PEN_PARENT_RESULTS_ONCE"
            || id == "TRANSPORT_SUCCESS_CAPTURE_STATE"
            || id == "TRANSPORT_SUCCESS_TERMINAL"
            || id == "TRANSPORT_RELEASE_PARENT_RESULT_ONCE")
        {
            ObserveE11Transport(id, success, detail);
        }
        ObserveBatchMemory();
        WriteReport();
    }

    private int CountWildlifeOutcome(
        string producerId,
        string eventKind,
        string wildlifeId)
    {
        string operationPrefix = producerId ==
                EnvironmentOutcomeIds.WildlifeCommandProducer
            ? $"wildlife-command:{eventKind}:{wildlifeId}:"
            : $"wildlife-action:{eventKind}:{wildlifeId}:";
        return outcomeLedger?.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
            value != null
            && string.Equals(value.producerId, producerId, StringComparison.Ordinal)
            && value.operationId.StartsWith(
                operationPrefix,
                StringComparison.Ordinal)) ?? 0;
    }

    private bool HasCanonicalProficiencyProfile(CharacterActor candidate)
    {
        if (candidate == null
            || proficiencies == null
            || calendar == null
            || !CharacterPersistentIdentity.TryGet(
                candidate,
                out CharacterId characterId))
        {
            return false;
        }

        IReadOnlyList<CharacterProficiencySnapshot> values =
            proficiencies.GetAllProficiencies(
                characterId,
                calendar.AbsoluteHour);
        return values != null
            && values.Count == BuiltInCharacterProficiencyIds.All.Count;
    }

    private string DescribeActor(CharacterActor candidate)
    {
        if (candidate == null) return "missing";
        CharacterPersistentIdentity.TryGet(candidate, out CharacterId id);
        int count = id.IsValid && proficiencies != null && calendar != null
            ? proficiencies.GetAllProficiencies(id, calendar.AbsoluteHour).Count
            : 0;
        return $"{candidate.name}:{id.Value}:proficiencies={count}";
    }

    private void CaptureConsoleIssue(string condition, string stackTrace, LogType type)
    {
        if (type is LogType.Warning
            or LogType.Error
            or LogType.Exception
            or LogType.Assert)
        {
            consoleIssues.Add($"{type}:{condition}");
        }
    }

    private IEnumerator VerifyE11TransportGuarded()
    {
        var pending = new Stack<IEnumerator>();
        pending.Push(VerifyTransportRows());
        try
        {
            while (pending.Count > 0)
            {
                bool moved = false;
                object value = null;
                Exception failure = null;
                try
                {
                    moved = pending.Peek().MoveNext();
                    if (moved) value = pending.Peek().Current;
                }
                catch (Exception exception) { failure = exception; }
                if (failure != null)
                {
                    Check(false, "E11_TRANSPORT_UNHANDLED_FAILURE", failure.ToString());
                    yield break;
                }
                if (!moved)
                {
                    (pending.Pop() as IDisposable)?.Dispose();
                    continue;
                }
                if (value is IEnumerator nested) pending.Push(nested);
                else yield return value;
            }
        }
        finally
        {
            while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
        }
    }

    private void ObserveE11Transport(string stage, bool checkPassed, string detail)
    {
        if (!transportOnly || IsColdResume || string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(CaptivityWildlifeLifecyclePlayModeVerifier.E11ReportPathEnvironmentVariable)))
            return;
        var physical = itemRuntime as WorldItemStackRuntime
            ?? throw new InvalidOperationException("E11 capture requires the real physical-item owner.");
        WildlifeActor animal = wildlife.Wildlife.SingleOrDefault(value => value != null
            && value.WildlifeId == e11TransportWildlifeId);
        var observationClock = scope.Container.Resolve<IGameClock>();
        bool capturedStatePresent = capture.TryGetCaptured(e11TransportWildlifeId, out var capturedState);
        var observation = new JObject
        {
            ["stage"] = stage,
            ["checkPassed"] = checkPassed,
            ["detail"] = detail,
            ["wildlifeId"] = e11TransportWildlifeId,
            ["workerId"] = CharacterPersistentIdentity.Require(worker).Value,
            ["workerPosition"] = new JObject { ["x"] = worker.GetNowXY().x, ["y"] = worker.GetNowXY().y },
            ["actorPresent"] = animal != null,
            ["actorHealth"] = animal != null ? new JValue(animal.CurrentHealth) : JValue.CreateNull(),
            ["actorPosition"] = animal != null
                ? (JToken)new JObject { ["x"] = animal.GridPosition.x, ["y"] = animal.GridPosition.y }
                : JValue.CreateNull(),
            ["externalIntentActive"] = worker.Brain.IsExternallyDrivenActionActive,
            ["externalIntentOwnerId"] = worker.Brain.ExternalIntentOwnerId,
            ["externalIntentTerminalCount"] = worker.Brain.ExternalIntentTerminalCount,
            ["externalIntentTerminalKind"] = worker.Brain.LastExternalIntentTerminalKind.ToString(),
            ["gameClock"] = observationClock.Time,
            ["gameFrame"] = observationClock.FrameCount,
            ["capturedStatePresent"] = capturedStatePresent,
            ["careDeadline"] = capturedStatePresent ? new JValue(capturedState.nextCareAt) : JValue.CreateNull(),
            ["careInterval"] = CapturedWildlifeState.CareIntervalSeconds,
            ["canonicalCaptureCompleted"] = false
        };
        // Preserve the real deadline/clock and partial stage even if a strict
        // production Capture rejects it; that rejection remains a test FAIL.
        e11TransportObservations.Add(observation);
        rows.Add("INFO\tE11_CANONICAL_CAPTURE_CLOCK\t"
            + $"stage={stage};clock={observationClock.Time:R};"
            + $"deadline={(capturedStatePresent ? capturedState.nextCareAt.ToString("R") : "absent")};"
            + $"interval={CapturedWildlifeState.CareIntervalSeconds:R};canonicalCaptureCompleted=false");
        WriteReport();
        observation["circus"] = JObject.Parse(JsonUtility.ToJson(circusPersistence.Capture()));
        observation["wildlife"] = JObject.Parse(JsonUtility.ToJson(wildlife.Capture()));
        observation["physical"] = JObject.Parse(JsonUtility.ToJson(physical.Capture()));
        observation["ledger"] = JObject.Parse(JsonUtility.ToJson(outcomeLedger.CaptureGameplayOutcomes()));
        observation["canonicalCaptureCompleted"] = true;
    }

    private void WriteReport()
    {
        string reportPath =
            CaptivityWildlifeLifecyclePlayModeVerifier.ReportPath;
        Directory.CreateDirectory(
            Path.GetDirectoryName(reportPath) ?? "Artifacts/QA");
        string reportMode = husbandryPendingColdResume
            ? CaptivityWildlifeLifecyclePlayModeVerifier.HusbandryPendingColdResumeMode : feedColdResume
            ? CaptivityWildlifeLifecyclePlayModeVerifier.FeedColdResumeMode : transportOnly
            ? CaptivityWildlifeLifecyclePlayModeVerifier.TransportOnlyMode
            : CaptivityWildlifeLifecyclePlayModeVerifier.FullMode;
        List<string> report = new()
        {
            "# Captivity / Wildlife production-live lifecycle matrix",
            $"RESULT={(!runFinished ? "RUNNING" : verificationBodyCompleted && failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}",
            $"mode={reportMode}",
            $"runFinished={runFinished};verificationBodyCompleted={verificationBodyCompleted}",
            lastMemoryObservation,
            IsColdResume ? "fixture=GameplayScene+official-save-service+cold-process-immutable-checkpoint"
                : "fixture=GameplayScene+official-start-party+authored-room+V18-save-boundary",
            husbandryPendingColdResume ? "terminal-policy=pending-world-restore+normal-owner-Tick+referenced-identity;no-effect-or-child-injection"
                : feedColdResume ? "terminal-policy=completed-world-restore+same-commit-idempotency-call;no-new-terminal-injection"
                : "terminal-policy=no-direct-runtime-terminal-calls"
        };
        report.AddRange(rows);
        report.AddRange(failures.Select(value => "FAILURE\t" + value));
        File.WriteAllLines(reportPath, report);
        if (runFinished && !e11TransportRawWritten && transportOnly && !IsColdResume
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                CaptivityWildlifeLifecyclePlayModeVerifier.E11ReportPathEnvironmentVariable)))
        {
            var raw = new JObject
            {
                ["schemaVersion"] = 1,
                ["mode"] = reportMode,
                ["passed"] = verificationBodyCompleted && failures.Count == 0,
                ["trainingEligible"] = false,
                ["naturalGameplay"] = false,
                ["fixtureScope"] = "authored room/official start party/spawned eligible animal/legal path preparation; actual Ability/external-intent transport and public release",
                ["coldProcessRestore"] = false,
                ["observations"] = e11TransportObservations,
                ["checks"] = new JArray(rows),
                ["failures"] = new JArray(failures),
                ["consoleIssues"] = new JArray(consoleIssues)
            };
            using var stream = new FileStream(Path.ChangeExtension(reportPath, ".raw.json"),
                FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.Write(raw.ToString());
            e11TransportRawWritten = true;
        }
    }

    private readonly struct AreaSnapshot
    {
        public AreaSnapshot(Vector2Int position, GridCellAreaType areaType)
        {
            Position = position;
            AreaType = areaType;
        }
        public Vector2Int Position { get; }
        public GridCellAreaType AreaType { get; }
    }

    private sealed class EscapeStartProbe
    {
        public bool Started;
        public string Reason = string.Empty;
    }

    private sealed class EscapeRouteProbe
    {
        public bool Ready;
        public string Detail = string.Empty;
    }

    private readonly struct PreparedOriginalBuildingSnapshot
    {
        public PreparedOriginalBuildingSnapshot(
            BuildableObject original,
            ModularFacilityBuildingSaveData saved,
            IReadOnlyList<Vector2Int> positions)
        {
            Original = original;
            Saved = saved;
            Positions = positions;
        }

        public BuildableObject Original { get; }
        public ModularFacilityBuildingSaveData Saved { get; }
        public IReadOnlyList<Vector2Int> Positions { get; }
    }

    private readonly struct FixtureBuildingSnapshot
    {
        public FixtureBuildingSnapshot(
            BuildableObject building,
            GridLayer layer,
            IReadOnlyList<Vector2Int> positions,
            bool connectPositions)
        {
            Building = building;
            Layer = layer;
            Positions = positions;
            ConnectPositions = connectPositions;
        }

        public BuildableObject Building { get; }
        public GridLayer Layer { get; }
        public IReadOnlyList<Vector2Int> Positions { get; }
        public bool ConnectPositions { get; }
    }

    private readonly struct DisplacedWildlifeSnapshot
    {
        public DisplacedWildlifeSnapshot(
            string wildlifeId,
            string speciesId,
            Vector2Int position)
        {
            WildlifeId = wildlifeId ?? string.Empty;
            SpeciesId = speciesId ?? string.Empty;
            Position = position;
        }

        public string WildlifeId { get; }
        public string SpeciesId { get; }
        public Vector2Int Position { get; }
    }

    private readonly struct MonoBehaviourState
    {
        public MonoBehaviourState(MonoBehaviour behaviour, bool enabled)
        {
            Behaviour = behaviour;
            Enabled = enabled;
        }
        public MonoBehaviour Behaviour { get; }
        public bool Enabled { get; }
    }

    private sealed class NoPathSearchBroker : IGridPathSearchBroker
    {
        public int SearchesThisFrame => 0;
        public int UrgentOverdraftSearchesThisFrame => 0;
        public int UnboundedSearchesThisFrame => 0;
        public int CacheHitsThisFrame => 0;
        public int BudgetDeferralsThisFrame => 0;
        public double SearchMillisecondsThisFrame => 0d;

        public void BeginFrame(
            int searchBudget,
            bool enforceBudget,
            double searchTimeBudgetMilliseconds = double.PositiveInfinity)
        {
        }

        public bool TryGetSearch(
            Grid grid,
            Vector2Int start,
            out GridPathSearchResult result,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            result = null;
            return false;
        }

        public Queue<GridMoveStep> GetMovePath(
            Grid grid,
            Vector2Int start,
            Func<Vector2Int, bool> terminateEndCondition,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default) => new();

        public Queue<GridMoveStep> GetMovePathTo(
            Grid grid,
            Vector2Int start,
            Vector2Int destination,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default) => new();

        public GridPathRequestStatus RequestMovePathTo(
            Grid grid,
            Vector2Int start,
            Vector2Int destination,
            out Queue<GridMoveStep> path,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            path = new Queue<GridMoveStep>();
            return GridPathRequestStatus.Unreachable;
        }

        public void Clear()
        {
        }
    }

    private sealed class FaultWall : IGridBuildingOccupantCapability
    {
        public FaultWall(string id, Vector2Int position)
        {
            Id = id;
            Position = position;
        }
        public string Id { get; }
        public Vector2Int Position { get; }
        public int GridId => Id.GetHashCode();
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => false;
        public bool BlocksGridMovement => true;
        public bool AllowsInteriorWalkability => false;
    }

    private sealed class DependencyMarkerSection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string id;
        public DependencyMarkerSection(string id) =>
            this.id = id ?? throw new ArgumentNullException(nameof(id));
        public override string SectionId => id;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        protected override void CommitMarker(DungeonGameRestoreReport report) { }
    }
}
#endif
