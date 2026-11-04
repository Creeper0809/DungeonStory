#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using VContainer;

// 격리된 실제 게임 검증이다. 모델 응답이나 전투 결과를 주입하지 않는다.
public static class R80ExtendedPlayModeVerifier
{
    private const string Key = "DungeonStory.R80Extended.";
    private const string Seed = "Artifacts/QA/R80Integration-20261027-r1/play-r20/committed-world.json";
    private const string FacilitySeed = "Artifacts/QA/R80ExtendedPlay-20261027-r1/facility-hostile-r3/unity-evidence/persistent-data/Saves/r80-extended-profiles-final.json";
    private const string LateTerminalSeed = "Artifacts/QA/R80ExtendedPlay-20261027-r1/soak-r9/unity-evidence/persistent-data/Saves/r80-extended-natural-run-end-1.json";
    private const string HistoryRecoverySeed = "Artifacts/QA/R80Latency-20261028-r1/soak-r13/unity-evidence/persistent-data/Saves/r80-extended-soak-final.json";
    private static LocalLlmRequestQueue queue;
    private static IDisposable traitDiagnosticSubscription;
    private static int traitDiagnosticSequence;
    private static DungeonRuntimeLifetimeScope scope;
    private static CharacterActor owner, target;
    private static AiDirectorRuntime director;
    private static ICombatEquipmentRuntime equipment;
    private static IEquipmentEvolutionRuntime evolution;
    private static ICharacterCombatCommandRuntime commands;
    private static string weaponId;
    private static int stage, sequence, sample, lastBucket;
    private static double stageStart, soakStart, gameStart, lastRequest;
    private static double facilitySaveBoundaryWaitStarted = -1;
    private static double scheduledSaveBoundaryStarted = -1, terminalAutosaveWaitStarted = -1;
    private static int scheduledSaveBusyFrame = -1, scheduledSaveWaitSequence;
    private static string lastTerminalAutosaveSha = string.Empty;
    private static float healthBefore, healthLowest;
    private static long framesAtStart, memoryAtStart;
    private static JArray failures = new JArray(), errors = new JArray();
    private static HashSet<string> profiles = new HashSet<string>(StringComparer.Ordinal);
    private static bool soak;
    private static bool combatStopped;
    private static string facilityId;
    private static bool facilityCommandDone;
    private static JObject observedBubble;
    private static double lastBubbleProbe;
    private static double moodAdmissionRetryAt;
    private static InvasionIntruderRuntime hostile;
    private static bool auxiliaryPaused;
    private static Camera auxiliaryCamera;
    private static Vector3 auxiliaryCameraPosition;
    private static bool remainingOnly;
    private static bool facilityTargetedOnly;
    private static bool historyRecoveryOnly;
    private static bool saveProfileOnly;
    private static bool traitAnchorRecoveryOnly;
    private static bool skillAnchorRecoveryOnly;
    private static StreamWriter savePhaseWriter;
    private static long saveTraceStart;
    private static JArray lockedHistoryRequests = new();
    private static bool hostileOnly;
    private static FacilityRuntimeState facilityOperationsAtEvolutionRequest;
    private static HashSet<int> deferredSaveBoundaries = new();
    private static JArray naturalRunCompletions = new();
    private static int runEndObservedFrame;
    private static double lastGameClock, accumulatedGameSeconds;
    private static JArray periodicMacroAdmissions = new();
    private static JArray requestTimings = new();
    private static JArray officialSaveWindows = new();
    private static int timingSequence;
    private static float soakUiStart;
    private static Dictionary<int, int[]> observedBubbleProducers = new();
    private static readonly HashSet<string> rejectedTraitTargets = new(new[] { 9, 10, 11, 12, 13, 15, 16, 17, 18 }
        .Select(value => "character:world:1:" + value.ToString("D6", System.Globalization.CultureInfo.InvariantCulture)), StringComparer.Ordinal);
    private static Dictionary<string, HashSet<string>> rejectedTraitInitialInstances = new(StringComparer.Ordinal);
    private static HashSet<string> rejectedTraitOfferedActors = new(StringComparer.Ordinal);
    private static JObject rejectedTraitScheduledSave;
    private static double lastRejectedTraitPoll;
    private static int rejectedTraitProgressSequence;
    private static string rejectedTraitProgressSummary;

    public static void VerifyFacilityDamageSourceContract()
    {
        foreach (var item in new[] {
            (name: "간이침대", expected: "간이침대를 파손했다"),
            (name: "식탁", expected: "식탁을 파손했다"),
            (name: "회복실", expected: "회복실을 파손했다"),
            (name: "QX", expected: "시설 파손: QX") })
        {
            string source = CharacterActivityFormatter.FacilityDamage(item.name);
            Require(source == item.expected, "Facility damage source particle or neutral frame changed.");
            string sentence = "Ian이 " + source + ".";
            Require(CharacterLogNarrativeService.TryParseNarrativeLine(
                JsonUtility.ToJson(new CharacterRecordJsonDto { line = sentence }), source, "Ian이",
                out string parsed, out string error), "Facility source sentence rejected: " + error);
            Require(parsed == sentence, "Facility source sentence was repaired.");
            Require(!CharacterLogNarrativeService.TryParseNarrativeLine(
                JsonUtility.ToJson(new CharacterRecordJsonDto { line = "Ian이 " + source + " 뒤 체력이 떨어졌다." }),
                source, "Ian이", out _, out _), "Invented health loss was accepted.");
        }

        bool missingRejected = false;
        try { CharacterActivityFormatter.FacilityDamage(" "); }
        catch (ArgumentException) { missingRejected = true; }
        Require(missingRejected, "Missing facility name was replaced with a fake target.");
        const string oldSource = "간이침대을 파손함";
        const string badNative = "Ian이 간이침대을 파손함으로 인해 체력이 떨어진다.";
        Require(!CharacterLogNarrativeService.TryParseNarrativeLine(
            JsonUtility.ToJson(new CharacterRecordJsonDto { line = badNative }), oldSource, "Ian이",
            out _, out _), "R49 native invented health loss bypassed the original factual check.");
    }

    public static void RequestProfilesForBatch() => Start(false);
    public static void RequestSoakForBatch() => Start(true);
    public static void RequestHistoryRecoveryForBatch() => Start(false, historyRecovery: true);
    public static void RequestSaveProfileForBatch() => Start(false, saveProfile: true);
    public static void RequestTraitAnchorRecoveryForBatch() => Start(false, saveProfile: true, traitAnchorRecovery: true);
    public static void RequestSkillAnchorRecoveryForBatch() => Start(false, saveProfile: true, skillAnchorRecovery: true);
    internal static void VerifyDecimalRecordContract()
    {
        const string copulaSource = "피해: 출혈: 12.25";
        foreach (string suffix in new[] { "이다", "이며 잠시 멈췄다", "이고 잠시 멈췄다" })
        {
            string sentence = "도린이 출혈로 받은 피해는 12.25" + suffix + ".";
            Require(CharacterLogNarrativeService.TryParseNarrativeLine(
                JsonUtility.ToJson(new CharacterRecordJsonDto { line = sentence }), copulaSource,
                "도린이", out string parsed, out string predicateError), "Exact numeric predicate rejected: " + predicateError);
            Require(parsed == sentence, "Numeric predicate was rewritten by the validator.");
        }
        foreach (string invented in new[] { "2", "12", "25", "12.2", "12.250", "999" })
        {
            string sentence = "도린이 출혈 피해 12.25를 받고 피해 " + invented + "를 받았다.";
            Require(!CharacterLogNarrativeService.TryParseNarrativeLine(
                JsonUtility.ToJson(new CharacterRecordJsonDto { line = sentence }), copulaSource,
                "도린이", out _, out string numberError) && numberError.Contains("invented a numeric fact"),
                "Extra numeric fact or source substring accepted: " + invented + "; " + numberError);
        }
        Require(!CharacterLogNarrativeService.TryParseNarrativeLine(
            JsonUtility.ToJson(new CharacterRecordJsonDto { line = "도린이 출혈로 받은 피해는 12.25화재이다." }),
            copulaSource, "도린이", out _, out _), "Invented consequence hidden behind a number accepted.");
        Require(CharacterLogNarrativeService.TryParseNarrativeLine(
            JsonUtility.ToJson(new CharacterRecordJsonDto { line = "Yuna가 Arin의 공격으로 피해 3.5를 받았다." }),
            "피해 3.5: Arin의 공격", "Yuna가", out _, out string reason),
            "Player-facing combat cause rejected: " + reason);
        foreach (string subject in new[] { "일꾼 2가", "화재지킴이가" })
        {
            string faithful = subject + " 출혈 피해 12.25를 받았다.";
            Require(CharacterLogNarrativeService.TryParseNarrativeLine(
                JsonUtility.ToJson(new CharacterRecordJsonDto { line = faithful }), "피해 12.25: 출혈", subject,
                out string unchanged, out string subjectError) && unchanged == faithful,
                "Trusted subject was treated as a new event: " + subjectError);
            foreach (string extra in new[] { "피해 2를 받았다", "화재를 겪었다" })
                Require(!CharacterLogNarrativeService.TryParseNarrativeLine(
                    JsonUtility.ToJson(new CharacterRecordJsonDto { line = subject + " 출혈 피해 12.25와 " + extra + "." }),
                    "피해 12.25: 출혈", subject, out _, out _), "Trusted name exempted an invented body fact.");
        }
        const string faithfulMove = "Yuna가 저지하러 이동했다.";
        Require(CharacterLogNarrativeService.TryParseNarrativeLine(
            JsonUtility.ToJson(new CharacterRecordJsonDto { line = faithfulMove }), "저지하러 이동", "Yuna가",
            out string unchangedMove, out string moveError) && unchangedMove == faithfulMove,
            "Original action conjugation rejected: " + moveError);
        foreach (var invalid in new[] {
            (source: "저지 대기", line: "Yuna가 저지 대기 중 이동했다."),
            (source: "이동량", line: "Yuna가 이동량을 확인하고 이동했다."),
            (source: "저지하러 이동", line: "Yuna가 저지하러 이동 후 치료했다.") })
            Require(!CharacterLogNarrativeService.TryParseNarrativeLine(
                JsonUtility.ToJson(new CharacterRecordJsonDto { line = invalid.line }), invalid.source, "Yuna가",
                out _, out _), "Absent action or partial source word accepted: " + invalid.line);
        foreach (string number in new[] { "3.5", "12.25", "35" })
        {
            string source = "피해 " + number + ": 출혈";
            var anchors = CharacterLogNarrativeService.ExtractRequiredAnchors(source);
            Require(anchors.Contains(number), "Source number changed: " + string.Join(",", anchors));
            string sentence = "Arin이 출혈 피해 " + number + "에 멈칫하더니 엉뚱한 방향으로 움직였다가 슬쩍 되돌아왔다.";
            string json = JsonUtility.ToJson(new CharacterRecordJsonDto { line = sentence });
            Require(CharacterLogNarrativeService.TryParseNarrativeLine(json, source, "Arin이", out _, out string error),
                "Exact decimal record rejected: " + error);
            Require(!CharacterLogNarrativeService.TryParseNarrativeLine(JsonUtility.ToJson(new CharacterRecordJsonDto {
                line = sentence.Replace(number, "999") }), source, "Arin이", out _, out _), "Changed source number accepted.");
            Require(!CharacterLogNarrativeService.TryParseNarrativeLine(JsonUtility.ToJson(new CharacterRecordJsonDto {
                line = sentence + " 다시 돌아왔다." }), source, "Arin이", out _, out _), "Two sentences accepted.");
        }
    }

    private static void Start(bool longRun, bool historyRecovery = false, bool saveProfile = false,
        bool traitAnchorRecovery = false, bool skillAnchorRecovery = false)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Private isolated batch and persistence root required.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Play already active.");
        string rejectedWorld = Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_REJECTED_TRAIT_WORLD");
        Require(rejectedWorld == null || rejectedWorld == "1" && saveProfile
            && !traitAnchorRecovery && !skillAnchorRecovery && !historyRecovery && !longRun,
            "Rejected-trait world replay requires its isolated save-profile mode.");
        Require(Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE") != "1", "Capture mode forbidden.");
        string root = Environment.GetEnvironmentVariable("DUNGEONSTORY_LLM_STREAMING_ROOT");
        Require(!string.IsNullOrEmpty(root) && Path.GetFullPath(root) == Path.GetFullPath(
            DungeonStoryLlmHostProcess.ResolveDefaultRoot(Application.streamingAssetsPath)), "Activated R80 required.");
        var manifest = JsonUtility.FromJson<DungeonStoryLlmHostManifest>(File.ReadAllText(Path.Combine(root, "DungeonStoryLlm/manifest.json")));
        Require(manifest.hostKind == DungeonStoryLlmHostProcess.R80BackendKind
            && manifest.modelSha256 == "2894ec5261b3a8352a375eb6319bbc8dd8c68c9b7b64fba255d51ce1bc2eb9b2", "Wrong model.");
        FacilityEvolutionDebugScenarios.VerifyOperationalStateForBatch();
        OperatingDayFacilityDisplayIdentityDebugScenarios.VerifyForBatch();
        InstanceEvolutionDebugScenarios.VerifyCapabilityAlignedGenerationForBatch();
        CarriedFreshnessRecoveryDebugScenarios.VerifyForBatch();
        StaffDiscontentDebugScenarios.VerifyPsychosocialDepartureForBatch();
        AiDirectorSingleFlightDebugScenarios.VerifyForBatch();
        ActiveEvacuationRestoreJoinDebugScenarios.VerifyForBatch();
        ResponsiveLlmLaneDebugScenarios.VerifyForBatch();
        Write("facility-operational-fixture.json", new JObject { ["status"] = "PASS",
            ["operatingDayDisplayTransition"] = "PASS",
            ["capabilityAlignedGeneration"] = "PASS",
            ["carriedFreshnessRecovery"] = "PASS",
            ["staffDeparturePsychosocialRollback"] = "PASS",
            ["directorSingleFlight"] = "PASS",
            ["activeEvacuationRestoreJoin"] = "PASS",
            ["responsiveLlmLanes"] = "PASS",
            ["controlledFixture"] = true, ["actualModelInferenceClaimed"] = false });
        SessionState.SetBool(Key + "pending", true);
        SessionState.SetBool(Key + "soak", longRun);
        SessionState.SetBool(Key + "historyRecovery", historyRecovery);
        SessionState.SetBool(Key + "saveProfile", saveProfile);
        SessionState.SetBool(Key + "traitAnchorRecovery", traitAnchorRecovery);
        SessionState.SetBool(Key + "skillAnchorRecovery", skillAnchorRecovery);
        if (historyRecovery)
            Require(Hex(File.ReadAllBytes(HistoryRecoverySeed)) == "713f918c75a26b6f36aabc369ea34a02bf9f8f1a13ea17504bea7609e71e195d",
                "Actual pending history save changed.");
        bool facilityTargeted = Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_FACILITY_TARGETED_ONLY") == "1";
        Require(!facilityTargeted || (!longRun && !historyRecovery && !saveProfile
            && Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_REMAINING_FEATURES_ONLY") != "1"
            && Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_HOSTILE_ONLY") != "1"),
            "Targeted facility verification cannot inherit or combine other modes.");
        SessionState.SetBool(Key + "facilityTargeted", facilityTargeted);
        bool remaining = !longRun && Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_REMAINING_FEATURES_ONLY") == "1";
        bool hostileFollowup = remaining && Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_HOSTILE_ONLY") == "1";
        if (hostileFollowup || longRun)
            Require(Hex(File.ReadAllBytes(FacilitySeed)) == "1e9f55621dc89a501a343068558b190bab9d46a2411ed9e8aa1f20eddddebc40",
                "Actual evolved facility seed changed.");
        if (longRun)
            Require(Hex(File.ReadAllBytes(LateTerminalSeed)) == "6d8a8209f55fd15a8d4aa435f0fe6c8b7ff4fe6ce08c7713fa691377383766f5",
                "Actual late terminal save changed.");
        if (remaining)
        {
            Require(Hex(File.ReadAllBytes("Artifacts/QA/R80ExtendedPlay-20261027-r1/profiles-r16/auxiliary-consumers.json"))
                == "a44c2020d526fa6b2f76463dc5e3d60fc1183bf1c19c0989addb1ff462e3216b", "Partial parent evidence changed.");
            var pins = JObject.Parse(File.ReadAllText("Artifacts/QA/R80ExtendedPlay-20261027-r1/frozen-r16.json"));
            foreach (var pin in pins.Properties())
                if (!pin.Name.Replace('\\', '/').EndsWith("/R80ExtendedPlayModeVerifier.cs", StringComparison.Ordinal))
                    Require(Hex(File.ReadAllBytes(pin.Name)) == pin.Value.ToString(), "Protected parent source changed: " + pin.Name);
        }
        SessionState.SetBool(Key + "remaining", remaining);
        SessionState.SetBool(Key + "hostile", hostileFollowup);
        SessionState.SetString(Key + "start", EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
        Reset();
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (SessionState.GetBool(Key + "pending", false)) Reset();
        if (SessionState.GetBool(Key + "exit", false))
        {
            EditorApplication.playModeStateChanged += AfterStop;
            EditorApplication.delayCall += Exit;
        }
    }
    private static void Reset()
    {
        queue = null; scope = null; owner = null; target = null; director = null;
        equipment = null; evolution = null; commands = null; weaponId = null;
        stage = sequence = sample = 0; lastBucket = -1; stageStart = soakStart = lastRequest = 0;
        facilitySaveBoundaryWaitStarted = -1;
        scheduledSaveBoundaryStarted = terminalAutosaveWaitStarted = -1;
        scheduledSaveBusyFrame = -1; scheduledSaveWaitSequence = 0;
        lastTerminalAutosaveSha = string.Empty;
        failures = new JArray(); errors = new JArray(); profiles = new HashSet<string>(StringComparer.Ordinal);
        combatStopped = false;
        facilityId = null; facilityCommandDone = false;
        facilityOperationsAtEvolutionRequest = null;
        deferredSaveBoundaries = new();
        naturalRunCompletions = new(); runEndObservedFrame = -1;
        lastGameClock = accumulatedGameSeconds = 0;
        periodicMacroAdmissions = new();
        requestTimings = new(); officialSaveWindows = new(); timingSequence = 0; soakUiStart = 0;
        rejectedTraitInitialInstances.Clear(); rejectedTraitOfferedActors.Clear();
        rejectedTraitScheduledSave = null; lastRejectedTraitPoll = 0;
        rejectedTraitProgressSequence = 0; rejectedTraitProgressSummary = null;
        observedBubbleProducers = new();
        observedBubble = null;
        lastBubbleProbe = 0; hostile = null;
        auxiliaryPaused = false;
        auxiliaryCamera = null;
        soak = SessionState.GetBool(Key + "soak", false);
        remainingOnly = SessionState.GetBool(Key + "remaining", false);
        facilityTargetedOnly = SessionState.GetBool(Key + "facilityTargeted", false);
        historyRecoveryOnly = SessionState.GetBool(Key + "historyRecovery", false);
        saveProfileOnly = SessionState.GetBool(Key + "saveProfile", false);
        traitAnchorRecoveryOnly = SessionState.GetBool(Key + "traitAnchorRecovery", false);
        skillAnchorRecoveryOnly = SessionState.GetBool(Key + "skillAnchorRecovery", false);
        lockedHistoryRequests = new JArray();
        hostileOnly = SessionState.GetBool(Key + "hostile", false);
        Application.logMessageReceived -= Log;
        Application.logMessageReceived += Log;
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
    }
    private static void Log(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(new JObject { ["message"] = text, ["stack"] = stack, ["frame"] = Time.frameCount });
    }
    private static void Pump()
    {
        if (!SessionState.GetBool(Key + "pending", false)) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            double elapsed = now - double.Parse(SessionState.GetString(Key + "start", "0"), CultureInfo.InvariantCulture);
            Require(elapsed < (soak ? 2280 : 720), "Bounded actual Play test expired at stage " + stage);
            Require(errors.Count == 0, "Actual Unity error; inspect unity-errors.json.");
            // 거절된 응답을 적용하지 않는 것은 정상 방어 경로다. 원 실패를
            // 기록하며 게임 지속과 실제 소비자 성공을 별도로 확인한다.
            if (!EditorApplication.isPlaying) return;
            if (scope == null) scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value.Container != null);
            if (scope == null) return;
            if (traitDiagnosticSubscription == null)
                traitDiagnosticSubscription = scope.Container.Resolve<IGameEventBus>()
                    .Subscribe<CharacterAcquiredTraitManifestationDiagnosticEvent>(value =>
                    {
                        var diagnostic = value.Diagnostic;
                        if (diagnostic.Status == CharacterAcquiredTraitManifestationStatus.SubmissionSucceeded
                            && rejectedTraitTargets.Contains(diagnostic.TargetPersistentId))
                            rejectedTraitOfferedActors.Add(diagnostic.TargetPersistentId);
                        Write("trait-diagnostic-" + (++traitDiagnosticSequence).ToString("D4") + ".json",
                            new JObject {
                                ["status"] = diagnostic.Status.ToString(),
                                ["targetPersistentId"] = diagnostic.TargetPersistentId,
                                ["requestId"] = diagnostic.RequestId,
                                ["requestKey"] = diagnostic.RequestKey,
                                ["candidatePacketHash"] = diagnostic.CandidatePacketHash,
                                ["attempt"] = diagnostic.Attempt,
                                ["issueCode"] = diagnostic.IssueCode.ToString(),
                                ["transportStatus"] = diagnostic.TransportStatus?.ToString(),
                                ["detail"] = diagnostic.Detail,
                                ["frame"] = Time.frameCount });
                    });
            if (queue == null)
            {
                queue = UnityEngine.Object.FindFirstObjectByType<LocalLlmRequestQueue>();
                if (queue == null) return;
                queue.ActualRequestCompletedForDiagnostics += Completed;
                queue.ActualRequestTimingForDiagnostics += TimedCompleted;
            }
            foreach (CharacterActor actor in Actors())
                if (actor.TryGetComponent(out CharacterDialogueRuntime dialogue))
                    observedBubbleProducers[actor.GetInstanceID()] = new[] { dialogue.IssuedBubbleCount,
                        dialogue.SuppressedPendingBubbleCount, dialogue.SuppressedOffscreenBubbleCount,
                        dialogue.SuppressedCapacityBubbleCount };
            int bucket = (int)elapsed / 30;
            if (bucket != lastBucket)
            {
                lastBucket = bucket;
                Write("status-" + bucket.ToString("D3") + ".json", Snapshot(now, elapsed));
            }
            if (queue.IsBundledModelStarting) return;
            Require(queue.IsBundledModelRunning, "Actual R80 host unavailable: " + queue.LastError);
            if (stage == 0)
            {
                if (saveProfileOnly)
                {
                    StartSavePhaseTrace();
                    scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
                    Require(StartPartyPreparationPlayModeVerifier.TryReconcileTierZeroForDirectGameplayFixture(scope, out string prepared), prepared);
                    bool rejectedTraitWorld = Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_REJECTED_TRAIT_WORLD") == "1";
                    string source = rejectedTraitWorld
                        ? "Artifacts/QA/R80PrototypePolish-20261028-r1/soak-polish-r46-r1/unity-evidence/persistent-data/Saves/r80-extended-soak-final.json"
                        : skillAnchorRecoveryOnly
                        ? "Artifacts/QA/R80PrototypePolish-20261028-r1/soak-polish-r8/unity-evidence/persistent-data/Saves/r80-extended-soak-checkpoint-1.json"
                        : traitAnchorRecoveryOnly
                        ? "Artifacts/QA/R80PrototypePolish-20261028-r1/soak-polish-r1/unity-evidence/persistent-data/Saves/r80-extended-soak-checkpoint-1.json"
                        : "Artifacts/QA/R80Latency-20261028-r1/soak-r17/unity-evidence/persistent-data/Saves/r80-extended-soak-final.json";
                    string sourceSha256 = rejectedTraitWorld
                        ? "5972f7f480bd31aa64531ddead14d183af4616fc714002b89466a3f51c468257"
                        : skillAnchorRecoveryOnly
                        ? "6f1aa19b21e926d329bdd360f253958121566273ed43622e8007931a31a9619b"
                        : traitAnchorRecoveryOnly
                        ? "e45bbd23b9be5f5d52e7a29a22683920ea565a3c01cd45cc8a7ef90226bd48d5"
                        : "5d0200731608c937656c1b7f7befd15a56bd95d43c8c7a3f7f04f55c41d741e9";
                    byte[] original = File.ReadAllBytes(source);
                    Require(Hex(original) == sourceSha256, "Actual save profile source changed.");
                    Write("save-profile-source.json", new JObject { ["source"] = source, ["sha256"] = Hex(original), ["bytes"] = original.Length });
                    var sourceService = scope.Container.Resolve<IDungeonGameSaveService>();
                    if (rejectedTraitWorld)
                    {
                        rejectedTraitInitialInstances.Clear();
                        rejectedTraitOfferedActors.Clear();
                    }
                    Require(sourceService.TryRestore(sourceService.FromJson(Encoding.UTF8.GetString(original)), out var loaded), string.Join(";", loaded.Errors));
                    if (rejectedTraitWorld) WriteRejectedTraitWorldObservation("after-original-restore");
                    stage = 31; stageStart = now;
                    return;
                }
                RestoredCarrySubsetReleaseDebugScenarios.VerifyCarriedDecayDeathForPlayMode();
                Write("carried-decay-death-fixture.json", new JObject {
                    ["status"] = "PASS", ["controlledFixture"] = true,
                    ["actualActorDieLifecycle"] = true, ["injectedModelResponse"] = false });
                scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
                Require(StartPartyPreparationPlayModeVerifier.TryReconcileTierZeroForDirectGameplayFixture(scope, out string layout), layout);
                var saves = scope.Container.Resolve<IDungeonGameSaveService>();
                string seed = historyRecoveryOnly ? HistoryRecoverySeed : hostileOnly || soak ? FacilitySeed : Seed;
                byte[] bytes = File.ReadAllBytes(seed);
                Require(saves.TryRestore(saves.FromJson(Encoding.UTF8.GetString(bytes)), out var report), string.Join(";", report.Errors));
                if (soak)
                {
                    VerifyActualStaffDepartureSave();
                    Require(saves.TryRestore(saves.FromJson(Encoding.UTF8.GetString(bytes)), out report),
                        "Original seed restore after controlled departure: " + string.Join(";", report.Errors));
                }
                Write("restored-seed.json", new JObject { ["source"] = seed, ["sha256"] = Hex(bytes),
                    ["actualPriorR80ModelOutput"] = true, ["injectedModelResponses"] = 0, ["layout"] = layout });
                director = UnityEngine.Object.FindFirstObjectByType<AiDirectorRuntime>();
                owner = Actors().First(value => value.CanRunAi && value.Blackboard != null);
                var speed = scope.Container.Resolve<IGameSpeedController>();
                scope.Container.Resolve<IGameTimeScaleController>().Scale = 1;
                speed.SetSpeed(1); speed.SetPaused(false);
                if (historyRecoveryOnly)
                {
                    // 원 실패 월드의 요청과 실제 서비스만 재개한다. 일시 정지는
                    // 이 표적 소비자 검사에만 적용하며 연속 플레이로 세지 않는다.
                    scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
                    lockedHistoryRequests = HistoryRecoveryObservation();
                    Require(lockedHistoryRequests.Count == 3
                        && lockedHistoryRequests.All(row => !(bool)row["completed"]),
                        "Expected three actual unfinished facility history requests.");
                    Write("history-recovery-baseline.json", lockedHistoryRequests);
                    stage = 30; stageStart = now;
                    return;
                }
                if (soak)
                {
                    var actualOwner = Actors().Single(value => value.IsOwner);
                    scope.Container.Resolve<ICharacterBodyHealthCommand>().Kill(actualOwner,
                        CharacterDeathCauseCode.Unknown, "qa:controlled-terminal-reload");
                    runEndObservedFrame = Time.frameCount;
                    stage = 11;
                    return;
                }
                if (facilityTargetedOnly)
                {
                    // 원래 시설/저장/전투 본문을 새 월드에서 실행. 과거 프로필 통과를 승계하지 않음.
                    stage = 20; stageStart = now;
                    Write("targeted-facility-scope.json", new JObject {
                        ["freshGameplayWorld"] = true, ["inheritedProfileCoverage"] = false,
                        ["scope"] = "actual facility use/evolution, official save restore and hostile engagement",
                        ["fullProfilesPassClaimed"] = false });
                    return;
                }
                if (remainingOnly)
                {
                    stage = hostileOnly ? 6 : 20; stageStart = now;
                    Write("reused-partial-evidence.json", new JObject {
                        ["parent"] = "profiles-r16", ["completeParentPassClaimed"] = false,
                        ["protectedSourceUnchanged"] = true,
                        ["facilitySuccessParent"] = hostileOnly ? "facility-hostile-r3" : null,
                        ["scope"] = hostileOnly ? "actual evolved facility save restore and hostile engagement"
                            : "facility use/evolution, official save restore, hostile engagement" });
                    return;
                }
                Require(director.RequestMoodImpulse(owner), "Actual mood producer rejected: " + director.LastError);
                Next(now);
                return;
            }
            if (stage == 11)
            {
                var owners = scope.Container.Resolve<IOwnerRunManagerProvider>();
                Require(owners.TryGetManager(out var manager) && manager.IsRunEnded
                    && manager.CurrentOwnerActor != null && manager.CurrentOwnerActor.IsDead,
                    "Controlled actual owner death did not complete the run.");
                if (Time.frameCount - runEndObservedFrame < 2) return;
                if (!TryRestoreCompletedTerminalAutosave(now, "controlled-terminal-run")) return;
                RestoreActualSavedRunAfterTerminal();
                Write("terminal-world-reload-fixture.json", new JObject {
                    ["status"] = "PASS", ["controlledActualOwnerDeath"] = true,
                    ["officialTerminalSaveRestored"] = true, ["officialLivingSavedWorldReloaded"] = true,
                    ["includedInRealSoakSeconds"] = false, ["injectedModelResponse"] = false });
                VerifyLateTerminalReload();
                stage = 12;
                return;
            }
            if (stage == 12)
            {
                if (now < moodAdmissionRetryAt) return;
                moodAdmissionRetryAt = now + 0.5;
                if (!director.RequestMoodImpulse(owner)) return;
                stage = 1;
                return;
            }
            if (stage == 1)
            {
                if (!profiles.Contains(LocalLlmRequestProfiles.MoodImpulse.Id)) return;
                if (soak && (!AtVisitorSaveBoundary() || !AtScheduledSaveBoundary(now, "soak-start"))) return;
                Require(director.LastAppliedMoodImpulseDebug.StartsWith("narrative-only suggested=", StringComparison.Ordinal)
                    && string.IsNullOrEmpty(director.LastError), "Mood consumer rejected: " + director.LastError);
                Write("mood-consumer.json", new JObject { ["status"] = "PASS", ["consumer"] = director.LastAppliedMoodImpulseDebug });
                if (soak)
                {
                    Save("soak-start", false);
                    soakStart = now; gameStart = scope.Container.Resolve<IGameClock>().Time;
                    soakUiStart = scope.Container.Resolve<IUiClock>().Time;
                    lastGameClock = gameStart;
                    framesAtStart = Time.frameCount; memoryAtStart = Profiler.GetTotalAllocatedMemoryLong();
                    stage = 10; return;
                }
                PrepareCombat(); Next(now); return;
            }
            if (stage == 2)
            {
                float health = scope.Container.Resolve<ICharacterBodyHealthQuery>().GetVitals(target).CurrentHealth;
                healthLowest = Mathf.Min(healthLowest, health);
                var state = evolution.GetState(weaponId);
                var pending = state.presentationRequests.FirstOrDefault(value => value != null);
                if (pending != null && pending.state == EquipmentEvolutionPresentationState.AwaitingNarrativeRetry)
                    throw new InvalidOperationException("Actual equipment consumer rejected: " + pending.lastFailureReason);
                if (pending != null || state.moduleSelectionRevision > 0)
                {
                    if (!combatStopped)
                    {
                        Write("combat-threshold.json", JObject.Parse(JsonUtility.ToJson(state)));
                        combatStopped = true;
                    }
                    // 사용 숙련은 빗나간 공격으로도 쌓인다. 실제 적중과 피해는
                    // 별도로 관측하며, 숙련 요청만으로 피해를 통과시키지 않는다.
                    if (healthLowest >= healthBefore)
                    {
                        Require(now - stageStart < 120, "No real damage after projectile observation window.");
                        if (equipment.TryGetActiveWeapon(owner.Identity.PersistentId, out var waitingWeapon) && waitingWeapon.LoadedAmmo <= 0)
                            Require(equipment.TryLoadExternalAmmunition(weaponId, "ammo:arrow", 1), "Authored ammunition reload rejected.");
                        return;
                    }
                    commands.CancelCommand(owner, "qa:actual-damage-and-threshold-observed");
                    Require(commands.SetCombatStance(owner, false, out string message), message);
                    scope.Container.Resolve<IGameSpeedController>().SetSpeed(1);
                    Write("combat-damage.json", new JObject { ["status"] = "PASS", ["publicAttackCommand"] = true,
                        ["controlledTarget"] = true, ["initialHealth"] = healthBefore, ["lowestHealth"] = healthLowest,
                        ["equipmentState"] = JObject.Parse(JsonUtility.ToJson(state)) });
                    // 큰 체력은 적중을 기다리기 위한 통제 조건이다. 검증 후 신체 권위의
                    // 치료 명령으로 출혈을 정리하며 NPC 의료 행동으로 집계하지 않는다.
                    scope.Container.Resolve<ICharacterBodyHealthCommand>().Heal(target, healthBefore, true);
                    Write("combat-fixture-cleanup.json", new JObject {
                        ["controlledHealthCommand"] = true, ["npcTreatmentClaimed"] = false,
                        ["healthAfter"] = scope.Container.Resolve<ICharacterBodyHealthQuery>().GetVitals(target).CurrentHealth });
                    target.SetAiPaused(false); Next(now); return;
                }
                Require(now - stageStart < 240, "Actual attacks did not create equipment evolution request.");
                if (equipment.TryGetActiveWeapon(owner.Identity.PersistentId, out var weapon) && weapon.LoadedAmmo <= 0)
                    Require(equipment.TryLoadExternalAmmunition(weaponId, "ammo:arrow", 1), "Fixture ammunition reload rejected.");
                return;
            }
            if (stage == 3)
            {
                var state = evolution.GetState(weaponId);
                Require(!state.presentationRequests.Any(value => value.state == EquipmentEvolutionPresentationState.AwaitingNarrativeRetry),
                    "Actual equipment callback needs retry: " + JsonUtility.ToJson(state));
                if (state.moduleSelectionRevision <= 0) return;
                Require(profiles.Contains(LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id), "Equipment commit without observed actual model response.");
                Write("equipment-consumer.json", JObject.Parse(JsonUtility.ToJson(state)));
                var spawner = UnityEngine.Object.FindFirstObjectByType<CharacterSpawner>();
                var attempts = new JArray();
                if (!Actors().Any(actor => actor.characterType == CharacterType.Customer) && spawner != null)
                    foreach (var definition in spawner.characters.Where(value => value != null))
                    {
                        bool spawned = spawner.TrySpawnCharacter(definition.id, out var rejection);
                        attempts.Add(new JObject { ["definitionId"] = definition.id, ["spawned"] = spawned, ["rejection"] = rejection.ToString() });
                        if (spawned) break;
                    }
                Write("customer-spawn-attempts.json", attempts);
                Next(now); return;
            }
            if (stage == 4)
            {
                var customers = UnityEngine.Object.FindObjectsByType<CustomerPersonaRuntime>(FindObjectsSortMode.None);
                if (!auxiliaryPaused && customers.Any(value => value.HasGeneratedPersona
                    && value.GetComponent<CharacterActor>().CurrentLifecycleState == CharacterLifecycleState.Active))
                {
                    // 일시 대사는 만료가 빠르므로 통제 시험에서만 게임 시계를
                    // 잠깐 멈추고 이미 접수된 요청을 끝낸 뒤 실제 발행자를 호출한다.
                    scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
                    Require(scope.Container.Resolve<IMainCameraProvider>().TryGetCamera(out auxiliaryCamera),
                        "Actual feedback camera missing.");
                    auxiliaryCameraPosition = auxiliaryCamera.transform.position;
                    auxiliaryCamera.transform.position = new Vector3(target.transform.position.x,
                        target.transform.position.y, auxiliaryCameraPosition.z);
                    auxiliaryPaused = true;
                    Write("auxiliary-clock-pause.json", new JObject { ["controlledQueueDrain"] = true,
                        ["realTimeSoakClaimed"] = false, ["controlledInGameCameraOnly"] = true,
                        ["userDesktopInput"] = false, ["gameClock"] = scope.Container.Resolve<IGameClock>().Time });
                }
                if (observedBubble == null && queue.RunningCount == 0 && queue.QueuedCount == 0
                    && now - lastBubbleProbe > 10)
                {
                    lastBubbleProbe = now;
                    float current = scope.Container.Resolve<ICharacterBodyHealthQuery>().GetVitals(target).CurrentHealth;
                    Require(current == healthBefore, "Controlled treatment state changed before observation.");
                    target.LogComponent.AddActivity(CharacterActivityEvent.Create(CharacterActivityKinds.Health,
                        CharacterActivityOutcomes.Changed, "치료 후 체력이 회복된 것을 확인했다.",
                        actionId: "qa:actual-treated-state-observation", value: current,
                        narrativeEligible: false, bubbleEligible: true));
                }
                foreach (var customer in customers)
                    if (!customer.HasGeneratedPersona && !customer.PersonaRequestInProgress) customer.RequestPersonaIfNeeded();
                var records = scope.Container.Resolve<ICharacterLogNarrativeService>() as CharacterLogNarrativeService;
                if (!profiles.Contains(LocalLlmRequestProfiles.CharacterRecord.Id)
                    || !profiles.Contains(LocalLlmRequestProfiles.BubbleLine.Id)
                    || !profiles.Contains(LocalLlmRequestProfiles.Persona.Id)
                    || !customers.Any(value => value.HasGeneratedPersona)
                    || observedBubble == null
                    || records == null || (records.AppliedCount - records.ControlledFallbackCount <= 0
                        && records.RejectedOriginalDisplayCount <= 0)) return;
                scope.Container.Resolve<IGameTimeScaleController>().Scale = 1;
                if (auxiliaryCamera != null) auxiliaryCamera.transform.position = auxiliaryCameraPosition;
                Write("auxiliary-consumers.json", new JObject {
                    ["status"] = records.AppliedCount - records.ControlledFallbackCount > 0 ? "MODEL_RECORD_APPLIED"
                        : "MODEL_RECORD_REJECTED_ORIGINAL_FACT_VISIBLE",
                    ["characterRecordModelApplied"] = records.AppliedCount - records.ControlledFallbackCount,
                    ["characterRecordControlledFallback"] = records.ControlledFallbackCount,
                    ["characterRecordRejectedOriginalVisible"] = records.RejectedOriginalDisplayCount,
                    ["lastRetainedOriginal"] = records.LastRetainedOriginalLine,
                    ["currentTargetVisibleRecords"] = new JArray(target.LogComponent.Entries),
                    ["characterRecordLastError"] = records.LastError, ["bubble"] = observedBubble,
                    ["customers"] = new JArray(customers.Where(value => value.HasGeneratedPersona).Select(value =>
                        JObject.Parse(JsonUtility.ToJson(value.Persona)))), ["bubbleObservedAtActualCallback"] = true });
                new GameObject("R80 isolated facility test").AddComponent<R80ExtendedCoroutineHost>().StartCoroutine(FacilityTrial());
                Next(now); return;
            }
            if (stage == 5)
            {
                if (!facilityCommandDone) return;
                var building = scope.Container.Resolve<IBuildingWorldQuery>().Buildings.Single(value => value != null
                    && !value.isDestroy && value.PersistentInstanceId.Value == facilityId);
                var state = building.GetComponent<FacilityEvolutionStateComponent>();
                if (state.PendingFormulaPresentation != null) return;
                Require(profiles.Contains(LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id), "No actual facility model completion.");
                if (!AtVisitorSaveBoundary())
                {
                    if (facilitySaveBoundaryWaitStarted < 0) facilitySaveBoundaryWaitStarted = now;
                    Require(now - facilitySaveBoundaryWaitStarted < 60,
                        "Facility observation did not reach the supported visitor save boundary.");
                    ObserveSaveBoundaryWait(5, now - stageStart);
                    return;
                }
                Write("facility-consumer.json", JObject.Parse(JsonUtility.ToJson(state.CreateSnapshot())));
                Require(facilityOperationsAtEvolutionRequest != null
                    && building.FacilityState.nextUseOperationSequence >= facilityOperationsAtEvolutionRequest.nextUseOperationSequence
                    && building.FacilityState.completedUses >= facilityOperationsAtEvolutionRequest.completedUses
                    && facilityOperationsAtEvolutionRequest.acknowledgedUseRanges.All(before =>
                        building.FacilityState.acknowledgedUseRanges.Any(after => after.first <= before.first && after.last >= before.last)),
                    "Completed evolution lost previously acknowledged use operations.");
                Write("facility-operational-transfer.json", new JObject { ["status"] = "PASS",
                    ["beforeRequest"] = JObject.Parse(JsonUtility.ToJson(facilityOperationsAtEvolutionRequest)),
                    ["afterActualEvolution"] = JObject.Parse(JsonUtility.ToJson(building.FacilityState)),
                    ["gameplayContinuedWhileModelPending"] = true });
                var operations = building.FacilityState.Clone();
                Save("profiles-final", true);
                var restored = scope.Container.Resolve<IBuildingWorldQuery>().Buildings.Single(value => value != null
                    && !value.isDestroy && value.PersistentInstanceId.Value == facilityId);
                Require(restored.FacilityState.nextUseOperationSequence == operations.nextUseOperationSequence
                    && restored.FacilityState.completedUses == operations.completedUses
                    && JToken.DeepEquals(JArray.FromObject(restored.FacilityState.acknowledgedUseRanges),
                        JArray.FromObject(operations.acknowledgedUseRanges)), "Facility use receipts changed across official restore.");
                Write("facility-operational-restore.json", new JObject { ["status"] = "PASS",
                    ["beforeSave"] = JObject.Parse(JsonUtility.ToJson(operations)),
                    ["afterRestore"] = JObject.Parse(JsonUtility.ToJson(restored.FacilityState)) });
                Next(now); return;
            }
            if (stage == 6)
            {
                if (hostile == null)
                {
                    var invasion = UnityEngine.Object.FindFirstObjectByType<InvasionDirectorRuntime>();
                    Require(invasion != null, "Actual invasion director missing.");
                    var threat = new InvasionThreatSnapshot(125f, InvasionThreatStage.Candidate,
                        new InvasionThreatFactors(6f, 4f, 3f, 1f), 0f, 0f);
                    Require(invasion.TrySpawnIntruder(threat, out var spawned) && spawned != null,
                        "Actual hostile spawn command rejected.");
                    Require(spawned.TryGetComponent(out hostile), "Spawned hostile runtime missing.");
                    Write("hostile-spawn.json", new JObject { ["characterId"] = spawned.Identity.PersistentId,
                        ["publicSpawnCommand"] = true, ["forcedDamage"] = false });
                    return;
                }
                var defense = scope.Container.Resolve<IDefenseEngagementRuntime>();
                if (!defense.TryGetEngagement(hostile, out var engagement))
                { Require(now - stageStart < 180, "No automatic hostile engagement."); return; }
                var query = scope.Container.Resolve<IGameplayOutcomeQuery>();
                var damage = new JArray();
                for (int i = 1; i <= engagement.ExchangeCount; i++)
                    foreach (string kind in new[] { ":exchange:", ":ranged:" })
                        foreach (var item in query.GetForOperation(new GameplayOperationId(engagement.Id + kind + i)).Items)
                        {
                            var exact = item.Exact;
                            if (exact != null && exact.producerId == ProductionCombatOutcomeIds.CombatProducerId
                                && exact.outcomeTypeId == ProductionCombatOutcomeIds.CombatDamageResolved.Value
                                && exact.status == GameplayOutcomeStatus.Succeeded
                                && exact.lifecycle == GameplayOutcomeRecordLifecycle.PublishedAcknowledged)
                                damage.Add(JObject.Parse(JsonUtility.ToJson(exact)));
                        }
                if (damage.Count == 0)
                { Require(now - stageStart < 180, "No acknowledged hostile combat damage."); return; }
                Write("hostile-combat.json", new JObject { ["automaticEngagement"] = engagement.Id,
                    ["exchanges"] = engagement.ExchangeCount, ["actualDamageOutcomes"] = damage,
                    ["naturalVictoryClaimed"] = false });
                Write("result.json", Result(now)); Finish(0); return;
            }
            if (stage == 20)
            {
                if (!Actors().Any(value => value.characterType == CharacterType.Customer
                    && value.CurrentLifecycleState == CharacterLifecycleState.Active)) return;
                new GameObject("R80 isolated facility followup").AddComponent<R80ExtendedCoroutineHost>().StartCoroutine(FacilityTrial());
                stage = 5; stageStart = now; return;
            }
            if (stage == 31)
            {
                Require(saveProfileOnly, "Save profiling stage requires its exact entry.");
                if (!skillAnchorRecoveryOnly && !traitAnchorRecoveryOnly)
                {
                    ScheduledDungeonSavePlayModeObservation.Begin(scope);
                    stage = 32;
                    stageStart = now;
                    return;
                }
                long scalarReads = DungeonSaveJson.EditorStrictScalarParseCount;
                long scalarReplays = DungeonSaveJson.EditorStrictScalarReplayCount;
                long scalarStreams = DungeonSaveJson.EditorStrictScalarStreamingParseCount;
                long scalarDoms = DungeonSaveJson.EditorStrictScalarDomParseCount;
                DungeonSaveEditorTrace.Record("measured-roundtrip.before");
                Save(skillAnchorRecoveryOnly ? "producer-anchor-recovery-final"
                    : traitAnchorRecoveryOnly ? "trait-anchor-recovery-final" : "save-profile-final", true);
                DungeonSaveEditorTrace.Record("measured-roundtrip.after");
                Write("result.json", new JObject { ["status"] = skillAnchorRecoveryOnly
                        ? "ACTUAL_PRODUCER_ANCHOR_RECOVERY_COMPLETED" : traitAnchorRecoveryOnly
                        ? "ACTUAL_TRAIT_ANCHOR_RECOVERY_COMPLETED" : "ACTUAL_SAVE_PROFILE_ROUNDTRIP_COMPLETED",
                    ["mode"] = "controlled-actual-save-replay", ["officialSaveWindows"] = officialSaveWindows,
                    ["strictScalarReads"] = new JObject
                    {
                        ["sourceReads"] = DungeonSaveJson.EditorStrictScalarParseCount - scalarReads,
                        ["memoReplays"] = DungeonSaveJson.EditorStrictScalarReplayCount - scalarReplays,
                        ["streamingScans"] = DungeonSaveJson.EditorStrictScalarStreamingParseCount - scalarStreams,
                        ["originalDomDispatches"] = DungeonSaveJson.EditorStrictScalarDomParseCount - scalarDoms
                    },
                    ["gameClockPaused"] = scope.Container.Resolve<IGameTimeScaleController>().Scale == 0,
                    ["continuousSoakClaimed"] = false,
                    ["injectedModelResponses"] = 0, ["unityErrors"] = errors.Count });
                Finish(0); return;
            }
            if (stage == 32)
            {
                Require(now - stageStart < 120, "Actual scheduled save did not finish in the bounded observation.");
                JObject observation = ScheduledDungeonSavePlayModeObservation.Poll();
                if (observation == null) return;
                Write("save-profile-final-save.json", observation);
                if (Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_REJECTED_TRAIT_WORLD") == "1")
                    WriteRejectedTraitWorldObservation("after-scheduled-restore");
                var roundtrip = new JObject
                {
                    ["status"] = "ACTUAL_SCHEDULED_SAVE_AND_OFFICIAL_RESTORE_COMPLETED",
                    ["mode"] = "controlled-actual-save-replay",
                    ["scheduledSave"] = observation,
                    ["gameClockPaused"] = scope.Container.Resolve<IGameTimeScaleController>().Scale == 0,
                    ["continuousSoakClaimed"] = false,
                    ["injectedModelResponses"] = 0, ["unityErrors"] = errors.Count
                };
                if (Environment.GetEnvironmentVariable("DUNGEONSTORY_R80_REJECTED_TRAIT_WORLD") == "1")
                {
                    Write("save-profile-roundtrip-result.json", roundtrip);
                    rejectedTraitScheduledSave = observation;
                    scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
                    stage = 33; stageStart = EditorApplication.timeSinceStartup;
                    return;
                }
                Write("result.json", roundtrip);
                Finish(0); return;
            }
            if (stage == 33)
            {
                // 기존 90초는 첫 1명 확인용이다. 전체 9명은 실제 배경 2개 lane과 최대 약 57초 응답에 맞춰 관측한다.
                if (now - stageStart >= 360)
                {
                    Write("trait-generation-last-state.json", ObserveRejectedTraitGeneration());
                    Require(false, "All nine original rejected actors did not complete actual generation within360s.");
                }
                if (now - lastRejectedTraitPoll < 0.5) return;
                lastRejectedTraitPoll = now;
                JObject generation = ObserveRejectedTraitGeneration();
                string summary = generation.Value<int>("offeredActors") + ":"
                    + generation.Value<int>("newAcceptedActors") + ":" + generation.Value<int>("pendingActors");
                if (summary != rejectedTraitProgressSummary)
                {
                    rejectedTraitProgressSummary = summary;
                    Write("trait-generation-progress-" + (++rejectedTraitProgressSequence).ToString("D3") + ".json",
                        new JObject { ["offeredActors"] = generation["offeredActors"],
                            ["newAcceptedActors"] = generation["newAcceptedActors"],
                            ["pendingActors"] = generation["pendingActors"], ["frame"] = Time.frameCount });
                }
                if (generation.Value<int>("offeredActors") != rejectedTraitTargets.Count
                    || generation.Value<int>("newAcceptedActors") != rejectedTraitTargets.Count) return;
                Write("result.json", new JObject
                {
                    ["status"] = "ACTUAL_ALL_ORIGINAL_TRAIT_GENERATIONS_OBSERVED",
                    ["mode"] = "original-rejected-world-controlled-generation",
                    ["scheduledSave"] = rejectedTraitScheduledSave.DeepClone(),
                    ["generation"] = generation,
                    ["simulationPausedForControlledGeneration"] = true,
                    ["allNineGenerationsCompleteClaimed"] = generation.Value<int>("newAcceptedActors") == rejectedTraitTargets.Count,
                    ["injectedModelResponses"] = 0, ["outputRepair"] = false,
                    ["wholeModelQualityAccepted"] = false, ["unityErrors"] = errors.Count
                });
                Finish(0); return;
            }
            if (stage == 30)
            {
                JArray current = HistoryRecoveryObservation();
                if (current.Count == 3 && current.All(row => (bool)row["completed"]
                        && (bool)row["playerVisible"] && !string.IsNullOrWhiteSpace((string)row["displayName"])
                        && !string.IsNullOrWhiteSpace((string)row["description"])))
                {
                    foreach (JObject expected in lockedHistoryRequests)
                    {
                        JObject applied = current.OfType<JObject>().Single(row =>
                            (string)row["requestKey"] == (string)expected["requestKey"]);
                        Require(JToken.DeepEquals(expected["frozen"], applied["frozen"]),
                            "Actual history application changed the frozen facts or locked effect.");
                        Require(JToken.DeepEquals(expected["nodeEvidence"], applied["nodeEvidence"])
                                && JToken.DeepEquals(applied["frozen"]["evidence"], applied["nodeEvidence"]),
                            "Actual history application changed the node's original evidence IDs.");
                    }
                    Write("history-recovery-applied.json", current);
                    Save("history-recovery-final", true);
                    JArray restored = HistoryRecoveryObservation();
                    Require(JToken.DeepEquals(current, restored), "Official restore changed the applied facility history.");
                    Write("history-recovery-restored.json", restored);
                    Write("result.json", new JObject { ["status"] = "ACTUAL_HISTORY_APPLIED_AND_RESTORED",
                        ["mode"] = "controlled-real-model-consumer-recovery", ["historyApplied"] = 3,
                        ["realSeconds"] = now - stageStart, ["gameClockPaused"] = true,
                        ["continuousSoakClaimed"] = false, ["injectedModelResponses"] = 0,
                        ["unityErrors"] = errors.Count, ["failedRequests"] = failures.Count });
                    Finish(0); return;
                }
                if (now - stageStart >= 300)
                {
                    Write("history-recovery-unresolved.json", current);
                    throw new InvalidOperationException("Actual facility history did not apply within the bounded consumer trial.");
                }
                return;
            }
            if (stage == 10)
            {
                double duration = now - soakStart;
                double clock = scope.Container.Resolve<IGameClock>().Time;
                Require(!double.IsNaN(clock) && !double.IsInfinity(clock), "Invalid gameplay clock during soak.");
                accumulatedGameSeconds += Math.Max(0, clock - lastGameClock);
                lastGameClock = clock;
                var owners = scope.Container.Resolve<IOwnerRunManagerProvider>();
                if (owners.TryGetManager(out var manager) && manager.IsRunEnded)
                {
                    Require(manager.CurrentOwnerActor != null && manager.CurrentOwnerActor.IsDead,
                        "Unexpected terminal run without observed owner death.");
                    if (runEndObservedFrame < 0) runEndObservedFrame = Time.frameCount;
                    if (Time.frameCount - runEndObservedFrame < 2) return;
                    int end = naturalRunCompletions.Count + 1;
                    if (!TryRestoreCompletedTerminalAutosave(now, "natural-run-end-" + end)) return;
                    naturalRunCompletions.Add(new JObject { ["realSoakSeconds"] = duration,
                        ["ownerDeathObserved"] = true, ["officialTerminalSaveRestored"] = true,
                        ["continuation"] = "reload-original-actual-saved-world",
                        ["invulnerabilityOrDifficultyChange"] = false });
                    RestoreActualSavedRunAfterTerminal();
                    Write("natural-run-continuation-" + end + ".json", naturalRunCompletions[end - 1]);
                    return;
                }
                // 생산 저장이 소유한 일시정지만 원래 저장 한도 안에서 기다린다.
                if (!AtScheduledSaveBoundary(now, "continuous-soak")) return;
                if (duration >= (sample + 1) * 300 && sample < 5)
                {
                    if (!AtVisitorSaveBoundary() || !AtScheduledSaveBoundary(now, "soak-checkpoint-" + (sample + 1)))
                    {
                        Require(duration < (sample + 1) * 300 + 60, "Visitor traversal did not reach the supported save boundary.");
                        ObserveSaveBoundaryWait(sample + 1, duration);
                        return;
                    }
                    Save("soak-checkpoint-" + (++sample), true);
                    owner = Actors().FirstOrDefault(value => value.CanRunAi && value.Blackboard != null);
                    Require(owner != null, "No living AI actor after official load.");
                }
                if (duration - lastRequest >= 120)
                {
                    lastRequest = duration;
                    owner = Actors().FirstOrDefault(value => value.CanRunAi && value.Blackboard != null);
                    Require(owner != null, "No living AI actor during soak.");
                    bool eligible = director.ShouldRequestMacroGoal(owner);
                    bool accepted = eligible && director.RequestMacroGoal(owner);
                    periodicMacroAdmissions.Add(new JObject { ["realSoakSeconds"] = duration,
                        ["actorId"] = owner.Identity.PersistentId, ["eligible"] = eligible, ["accepted"] = accepted,
                        ["reason"] = eligible ? director.LastError : "producer-not-eligible",
                        ["queue"] = queue.GetRequestDiagnosticsForDebug() });
                }
                Require(Time.timeScale > 0 && !double.IsNaN(scope.Container.Resolve<IGameClock>().Time), "Gameplay paused or invalid clock.");
                if (duration < 1800) return;
                if (!AtVisitorSaveBoundary() || !AtScheduledSaveBoundary(now, "soak-final"))
                {
                    Require(duration < 1860, "Final visitor traversal did not reach the supported save boundary.");
                    ObserveSaveBoundaryWait(6, duration);
                    return;
                }
                Require(Time.frameCount - framesAtStart > 1000 && accumulatedGameSeconds > 0,
                    "Wall time elapsed without simulation progress.");
                Require(profiles.Contains(LocalLlmRequestProfiles.MacroGoal.Id), "No actual macro model success during the continuous test.");
                Save("soak-final", true);
                Write("result.json", Result(now)); Finish(0);
            }
        }
        catch (Exception error)
        {
            Write("failure.txt", error.ToString());
            Write("unity-errors.json", errors); Write("failed-requests.json", failures);
            Finish(1);
        }
    }
    private static CharacterActor[] Actors() => scope.Container.Resolve<ICharacterWorldQuery>().Characters
        .Where(value => value != null && !value.IsDead && value.Identity != null).ToArray();
    private static IEnumerator FacilityTrial()
    {
        var buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        var runtime = scope.Container.Resolve<FacilityFeatureSceneRuntimeReferences>().Evolution;
        var candidates = buildings.Buildings.Where(value => value is Facility && !value.isDestroy && value.isActiveAndEnabled)
            .SelectMany(building => runtime.GetCandidates(building, true, false).Select(candidate => (building, candidate))).ToArray();
        Write("facility-candidates.json", new JArray(candidates.Select(value => new JObject {
            ["facilityId"] = value.building.PersistentInstanceId.Value, ["recipe"] = value.candidate.Recipe.EffectiveId,
            ["approved"] = value.candidate.Approved, ["reason"] = value.candidate.Validation.ToMessage() })));
        var chosen = candidates.FirstOrDefault(value => value.candidate.Approved);
        if (chosen.building == null)
        {
            chosen = candidates.Single(value => value.candidate.Recipe.EffectiveId == "evolve_shop_display_to_secure_display");
            var profile = scope.Container.Resolve<IRoomProfileProvider>().Build(chosen.building);
            var missing = chosen.candidate.Recipe.requiredUniqueFixtures.Where(definition =>
                !profile.Fixtures.Any(value => value.BuildingData == definition)).ToArray();
            Require(missing.Length == 1 && missing[0].id == 1070 && profile.Room != null && profile.Room.IsUsable,
                "Authored facility fixture prerequisites differ; inspect actual candidate reasons.");
            var definition = missing[0];
            var grid = scope.Container.Resolve<IGridSystemProvider>().Grid;
            var cells = profile.Room.Cells.Where(cell => definition.GetGridPosList(cell).All(footprint => profile.Room.ContainsCell(footprint)
                && grid.GetGridCell(footprint) is GridCell actual && actual.CanBuildInArea(definition)
                && actual.CanOccupy(definition.Placement.Layer))).ToArray();
            Require(cells.Length > 0, "No legal candle fixture cell.");
            var furnishing = scope.Container.Resolve<IGridBuildingObjectFactory>().Create(grid, definition, cells[0]);
            Require(furnishing != null, "Actual building factory rejected candle fixture.");
            foreach (var component in furnishing.GetComponentsInChildren<MonoBehaviour>(true)) if (component != null) scope.Container.Inject(component);
            furnishing.SetGrid(grid); furnishing.Initialization(definition, cells[0]);
            Require(grid.RegisterOccupant(furnishing, definition.Placement.Layer, definition.GetGridPosList(cells[0]), definition.Placement.IsMovement)
                && buildings.Buildings.Contains(furnishing), "Candle is not registered in actual building authority.");
            Write("facility-support-fixture.json", new JObject { ["definitionId"] = definition.id,
                ["instanceId"] = furnishing.PersistentInstanceId.Value, ["constructionGameplayClaimed"] = false,
                ["purpose"] = "One authored support furnishing for controlled actual facility AI trial", ["fabricatedUsage"] = false });
            chosen = (chosen.building, runtime.GetCandidates(chosen.building, true, false).Single(value => value.Recipe == chosen.candidate.Recipe));
        }
        // 원래 30초 접수 예산 안에서 현재 이용 종료도 기다림. 이용 중 교체 검사는 유지.
        double admissionStart = EditorApplication.timeSinceStartup;
        int usersAtBoundary = chosen.building.CurrentUserCount;
        int activeUsesAtBoundary = chosen.building.FacilityState.activeUses?.Count ?? 0;
        while (chosen.building.CurrentUserCount > 0 || chosen.building.FacilityState.activeUses?.Count > 0)
        {
            Require(EditorApplication.timeSinceStartup - admissionStart < 30,
                "Current facility use did not finish within the shared actor admission window.");
            yield return null;
        }
        chosen = (chosen.building, runtime.GetCandidates(chosen.building, true, false)
            .Single(value => value.Recipe == chosen.candidate.Recipe));
        Write("facility-idle-boundary.json", new JObject {
            ["usersAtEntry"] = usersAtBoundary, ["activeUsesAtEntry"] = activeUsesAtBoundary,
            ["usersAfterWait"] = chosen.building.CurrentUserCount,
            ["activeUsesAfterWait"] = chosen.building.FacilityState.activeUses?.Count ?? 0,
            ["waitSeconds"] = EditorApplication.timeSinceStartup - admissionStart,
            ["sharedActorAdmissionBudgetSeconds"] = 30, ["guardBypassed"] = false });
        Require(chosen.candidate.Approved, "Facility not eligible: " + chosen.candidate.Validation.ToMessage());
        facilityId = chosen.building.PersistentInstanceId.Value;
        var facility = (Facility)chosen.building;
        CharacterActor actor = null;
        while (actor == null && EditorApplication.timeSinceStartup - admissionStart < 30)
        {
            actor = Actors().FirstOrDefault(value => value.Brain != null && value.CurrentLifecycleState == CharacterLifecycleState.Active
                && facility.SupportsFacilityRole(CharacterVisitPolicy.GetInterestRoles(value))
                && CharacterVisitPolicy.CanVisitBuilding(value, facility, false, out _));
            if (actor == null) yield return null;
        }
        Require(actor != null, "No actual actor is eligible to use selected facility.");
        int beforeUses = facility.FacilityState.completedUses;
        Require(actor.Brain.TryBeginExternallyDrivenAction("qa:r80-facility-use", CharacterActionIntentKind.RoutineNeed,
            "시설 이용", "이동 및 이용", "controlled-production-command", out var lease), "Actual facility action arbitration rejected.");
        try { yield return facility.Interact(actor.BuildingVisitor); }
        finally { actor.Brain.EndExternallyDrivenAction(lease); }
        Require(facility.FacilityState.completedUses > beforeUses, "Actual facility use did not complete.");
        Write("facility-use.json", new JObject { ["actor"] = actor.Identity.PersistentId, ["beforeUses"] = beforeUses,
            ["afterUses"] = facility.FacilityState.completedUses, ["publicInteraction"] = true });
        var live = buildings.Buildings.Single(value => value != null && !value.isDestroy && value.PersistentInstanceId.Value == facilityId);
        facilityOperationsAtEvolutionRequest = live.FacilityState.Clone();
        Require(runtime.TryEvolve(live, chosen.candidate.Recipe, out var result), "Actual evolution command rejected: " + result.Message);
        Write("facility-operational-before-evolution.json", JObject.Parse(JsonUtility.ToJson(facilityOperationsAtEvolutionRequest)));
        var pending = live.GetComponent<FacilityEvolutionStateComponent>().PendingFormulaPresentation;
        Require(pending != null, "Actual evolution command did not persist its presentation request.");
        Write("facility-pending.json", JObject.Parse(JsonUtility.ToJson(pending)));
        facilityCommandDone = true;
    }
    private static JObject ObserveRejectedTraitGeneration()
    {
        Require(rejectedTraitInitialInstances.Count == rejectedTraitTargets.Count,
            "Original rejected actor baseline is incomplete.");
        var actors = Actors().Where(actor => rejectedTraitTargets.Contains(actor.Identity.PersistentId))
            .ToDictionary(actor => actor.Identity.PersistentId, StringComparer.Ordinal);
        Require(actors.Count == rejectedTraitTargets.Count, "An original rejected actor disappeared.");
        var rows = new JArray();
        int acceptedActors = 0, pendingActors = 0;
        foreach (string id in rejectedTraitTargets.OrderBy(value => value, StringComparer.Ordinal))
        {
            var state = actors[id].GetComponent<CharacterProgression>().CaptureAcquiredTraitState();
            var accepted = state.instances.Where(value => value != null && !value.erased
                && !rejectedTraitInitialInstances[id].Contains(value.instanceId)).ToArray();
            if (accepted.Length > 0) acceptedActors++;
            if (state.pendingRequests.Count > 0) pendingActors++;
            rows.Add(new JObject
            {
                ["actorId"] = id, ["offerSubmitted"] = rejectedTraitOfferedActors.Contains(id),
                ["newAcceptedTraits"] = JToken.FromObject(accepted),
                ["pendingRequestIds"] = JToken.FromObject(state.pendingRequests.Select(value => value.requestId))
            });
        }
        return new JObject { ["offeredActors"] = rejectedTraitOfferedActors.Count,
            ["newAcceptedActors"] = acceptedActors, ["pendingActors"] = pendingActors, ["actors"] = rows };
    }

    private static void WriteRejectedTraitWorldObservation(string phase)
    {
        var query = scope.Container.Resolve<IGameplayOutcomeNarrativeEvidenceQuery>();
        var actors = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .Where(actor => actor != null && !actor.IsDead && actor.Identity != null)
            .OrderBy(actor => actor.Identity.PersistentId, StringComparer.Ordinal);
        var rows = new JArray();
        foreach (CharacterActor actor in actors)
        {
            var progression = actor.GetComponent<CharacterProgression>();
            Require(progression != null, "Restored actor lacks its progression authority.");
            var traitState = progression.CaptureAcquiredTraitState();
            if (phase == "after-original-restore" && rejectedTraitTargets.Contains(actor.Identity.PersistentId))
                rejectedTraitInitialInstances[actor.Identity.PersistentId] = traitState.instances
                    .Where(value => value != null).Select(value => value.instanceId).ToHashSet(StringComparer.Ordinal);
            var exact = GameplayOutcomeEvidenceFormulaProjection.Capture(query.GetForCharacter(
                actor.Identity.PersistentId, maximumCount: CharacterAcquiredTraitEvidenceWindow.GetExactEvidenceCapacity(
                    progression.NarrativeLedger),
                minimumSalience: 0f, includeCompacted: true));
            rows.Add(new JObject
            {
                ["actorId"] = actor.Identity.PersistentId,
                ["traitState"] = JToken.FromObject(traitState),
                ["legacyLedger"] = JToken.FromObject(progression.NarrativeLedger),
                ["exactBindings"] = JToken.FromObject(exact),
                ["selectedLegacyFactIds"] = JToken.FromObject(CharacterAcquiredTraitEvidenceWindow.SelectLegacyFactIds(
                    progression.NarrativeLedger, exact))
            });
        }
        Write("rejected-trait-world-" + phase + ".json", new JObject
        {
            ["phase"] = phase, ["frame"] = Time.frameCount, ["actors"] = rows,
            ["stateCommandsIssuedByObservation"] = 0, ["historicalRequestsReconstructed"] = false
        });
    }

    private static void PrepareCombat()
    {
        target = Actors().First(value => value != owner && value.CurrentLifecycleState == CharacterLifecycleState.Active);
        target.Brain?.StopCurrentActionForReplan("qa:controlled-real-combat");
        target.GetComponent<AbilityMove>()?.CancelActiveMovement(); target.SetAiPaused(true);
        var grid = scope.Container.Resolve<IGridSystemProvider>().Grid;
        var cell = owner.GetNowXY();
        var cells = new[] { 3, -3, 2, -2, 1, -1 }.Select(offset => new Vector2Int(cell.x + offset, cell.y))
            .Where(value => grid.IsValidGridPos(value) && grid.IsWalkable(value)
                && !Actors().Any(actor => actor != owner && actor != target && actor.GetNowXY() == value)).ToArray();
        Require(cells.Length > 0, "No clear authored combat cell.");
        target.transform.position = grid.GetWorldPos(cells[0]); Physics2D.SyncTransforms();
        scope.Container.Resolve<ICharacterBodyHealthCommand>().ConfigureVitals(target, 10000, true);
        healthBefore = healthLowest = scope.Container.Resolve<ICharacterBodyHealthQuery>().GetVitals(target).CurrentHealth;
        equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        evolution = scope.Container.Resolve<IEquipmentEvolutionRuntime>();
        commands = scope.Container.Resolve<ICharacterCombatCommandRuntime>();
        var item = equipment.CreateExternalInstance("weapon:shortbow", CombatEquipmentQuality.Normal, string.Empty);
        Require(item != null, "Authored shortbow fixture missing."); weaponId = item.instanceId;
        Require(equipment.TryAssignToCharacter(owner.Identity.PersistentId, weaponId, out string message), message);
        Require(equipment.TrySetActiveWeapon(owner.Identity.PersistentId, weaponId, out message), message);
        Require(equipment.TryLoadExternalAmmunition(weaponId, "ammo:arrow", 1), "Authored ammunition rejected.");
        Require(commands.SetCombatStance(owner, true, out message), message);
        Require(commands.TrySetFireMode(owner, CombatFireMode.Aimed, out message), message);
        Require(commands.TryIssueAttack(owner, new CombatParticipantRef(target), true, out message), message);
        Write("combat-prepared.json", new JObject { ["attackerId"] = owner.Identity.PersistentId,
            ["targetId"] = target.Identity.PersistentId, ["weaponId"] = weaponId, ["healthBefore"] = healthBefore,
            ["ownerCell"] = owner.GetNowXY().ToString(), ["targetCell"] = target.GetNowXY().ToString(),
            ["state"] = JObject.Parse(JsonUtility.ToJson(evolution.GetState(weaponId))) });
        scope.Container.Resolve<IGameSpeedController>().SetSpeed(5);
    }
    private static void VerifyLateTerminalReload()
    {
        CharacterActor guest = Actors().Single(value => value.Identity.PersistentId == "character:world:1:000001");
        scope.Container.Resolve<ICharacterBodyHealthCommand>().Kill(guest,
            CharacterDeathCauseCode.Unknown, "qa:late-terminal-customer-alias");
        Require(guest.IsDead, "Controlled customer death did not complete.");
        CharacterActor oldGuest = guest;
        GameObject oldGuestObject = guest.gameObject;
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        Require(saves.TryRestore(saves.FromJson(File.ReadAllText(LateTerminalSeed)), out var report),
            "Actual late terminal save replay failed: " + string.Join(";", report.Errors));
        var world = scope.Container.Resolve<ICharacterWorldQuery>();
        var lifetime = scope.Container.Resolve<ICharacterLifetimeQuery>();
        bool oldTerminalVisitorRetired = oldGuestObject != null && !oldGuestObject.activeInHierarchy;
        bool oldReferenceAbsentFromWorldAndLifetime =
            !world.Characters.Any(value => ReferenceEquals(value, oldGuest))
            && !lifetime.AllCharacters.Any(value => ReferenceEquals(value, oldGuest));
        Require(oldTerminalVisitorRetired,
            "Late terminal restore did not retire the original guest GameObject.");
        Require(oldReferenceAbsentFromWorldAndLifetime,
            "Late terminal restore retained the original guest reference in world or lifetime.");
        int deadAliases = world.Characters.Count(value => value != null && value.IsDead
            && value.Identity?.PersistentId == "character:world:1:000001");
        RestoreActualSavedRunAfterTerminal();
        Require(world.Characters.Count(value => value != null && !value.IsDead
            && value.gameObject.activeInHierarchy && value.CurrentLifecycleState == CharacterLifecycleState.Active
            && value.Identity?.PersistentId == "character:world:1:000001") == 1,
            "Official living checkpoint reload did not publish exactly one living guest.");
        Save("late-terminal-reloaded", true);
        owner = Actors().First(value => value.CanRunAi && value.Blackboard != null);
        director = UnityEngine.Object.FindFirstObjectByType<AiDirectorRuntime>();
        Write("late-terminal-reload-fixture.json", new JObject {
            ["status"] = "PASS", ["source"] = LateTerminalSeed,
            ["sourceSha256"] = "6d8a8209f55fd15a8d4aa435f0fe6c8b7ff4fe6ce08c7713fa691377383766f5",
            ["actualLateTerminalSnapshot"] = true, ["controlledCustomerDeath"] = true,
            ["oldTerminalVisitorRetired"] = oldTerminalVisitorRetired,
            ["oldReferenceAbsentFromWorldAndLifetime"] = oldReferenceAbsentFromWorldAndLifetime,
            ["retainedDeadAliasCountBeforeLivingReload"] = deadAliases,
            ["livingPublishedGuestCount"] = 1, ["officialSaveRestore"] = true,
            ["includedInRealSoakSeconds"] = false, ["injectedModelResponse"] = false });
    }

    private static void RestoreActualSavedRunAfterTerminal()
    {
        var ui = scope.Container.Resolve<IUiClock>();
        double startedAt = EditorApplication.timeSinceStartup;
        var window = new JObject { ["label"] = "reload-original-actual-saved-world",
            ["uiStart"] = ui.Time, ["uiEnd"] = null, ["blocksGameplayThread"] = true };
        officialSaveWindows.Add(window);
        try
        {
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        Require(saves.TryRestore(saves.FromJson(File.ReadAllText(FacilitySeed)), out var report),
            "Saved-world reload after terminal run failed: " + string.Join(";", report.Errors));
        var owners = scope.Container.Resolve<IOwnerRunManagerProvider>();
        Require(owners.TryGetManager(out var manager) && !manager.IsRunEnded
            && manager.CurrentOwnerActor != null && !manager.CurrentOwnerActor.IsDead,
            "Official saved-world reload did not resume a living run.");
        owner = Actors().First(value => value.CanRunAi && value.Blackboard != null);
        director = UnityEngine.Object.FindFirstObjectByType<AiDirectorRuntime>();
        lastGameClock = scope.Container.Resolve<IGameClock>().Time;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 1;
        var speed = scope.Container.Resolve<IGameSpeedController>();
        speed.SetSpeed(1); speed.SetPaused(false);
        runEndObservedFrame = -1;
        }
        finally
        {
            window["uiEnd"] = ui.Time;
            window["wallSeconds"] = EditorApplication.timeSinceStartup - startedAt;
        }
    }

    private static bool AtVisitorSaveBoundary() => !Actors().Any(value => value.characterType == CharacterType.Customer
        && value.CurrentLifecycleState is CharacterLifecycleState.SpawningOutside
            or CharacterLifecycleState.EnteringDungeon or CharacterLifecycleState.ExitingDungeon);

    private static void ObserveSaveBoundaryWait(int checkpoint, double duration)
    {
        if (!deferredSaveBoundaries.Add(checkpoint)) return;
        Write("save-boundary-wait-" + checkpoint + ".json", new JObject {
            ["realSoakSeconds"] = duration, ["maximumDeferralSeconds"] = 60,
            ["unsupportedTraversalRestoreClaimed"] = false,
            ["actors"] = new JArray(Actors().Where(value => value.characterType == CharacterType.Customer)
                .Select(value => new JObject { ["id"] = value.Identity.PersistentId,
                    ["state"] = value.CurrentLifecycleState.ToString() })) });
    }

    private static void VerifyActualStaffDepartureSave()
    {
        CharacterActor staff = Actors().First(value => !value.IsOwner && !value.IsDead
            && value.CurrentLifecycleState == CharacterLifecycleState.Active
            && value.Identity.CharacterType == CharacterType.NPC);
        CharacterId id = CharacterPersistentIdentity.Require(staff);
        IGriefTraumaService grief = scope.Container.Resolve<IGriefTraumaService>();
        grief.Require(id);
        grief.Require(CharacterId.Owner);
        string survivorBefore = JsonUtility.ToJson(grief.Require(CharacterId.Owner).Capture());
        staff.stats[CharacterCondition.MOOD] = 10f;
        var runtime = UnityEngine.Object.FindFirstObjectByType<StaffDiscontentRuntime>();
        Require(runtime != null, "Actual staff discontent owner missing.");
        var record = runtime.ProcessStaff(staff, out var outcome);
        Require(record != null && record.IsDeparted && outcome == StaffDiscontentOutcome.PermanentDeparture
            && staff.CurrentLifecycleState == CharacterLifecycleState.Despawned && !grief.TryGet(id, out _),
            "Actual permanent departure retained active psychosocial state.");
        Save("controlled-staff-departure", true);
        Require(!grief.TryGet(id, out _) && JsonUtility.ToJson(grief.Require(CharacterId.Owner).Capture()) == survivorBefore,
            "Official departure save restore lost retirement or changed unrelated psychosocial state.");
        Write("staff-departure-world-fixture.json", new JObject {
            ["status"] = "PASS", ["controlledMood"] = 10, ["departedCharacterId"] = id.Value,
            ["actualPermanentDepartureCommand"] = true, ["officialWholeWorldRestore"] = true,
            ["unrelatedPsychosocialPreserved"] = true, ["injectedModelResponse"] = false });
    }

    private static bool AtScheduledSaveBoundary(double now, string label)
    {
        var slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
        if (!slots.IsSaving && scheduledSaveBusyFrame < 0) return true;
        if (scheduledSaveBoundaryStarted < 0)
        {
            scheduledSaveBoundaryStarted = now;
            Write("scheduled-save-wait-" + (++scheduledSaveWaitSequence).ToString("D3") + ".json", new JObject {
                ["checkpoint"] = label, ["maximumDeferralSeconds"] = 60,
                ["slotServiceWasBusy"] = slots.IsSaving, ["forcedSynchronousDrain"] = false });
        }
        Require(now - scheduledSaveBoundaryStarted < 60,
            "Scheduled save did not complete before " + label + ".");
        if (slots.IsSaving) { scheduledSaveBusyFrame = Time.frameCount; return false; }
        // 실패 완료를 처리하는 생산 LateTick까지 관측한 뒤 다음 저장/복원으로 진행한다.
        if (Time.frameCount - scheduledSaveBusyFrame < 2) return false;
        Require(errors.Count == 0, "Actual scheduled save failed; inspect unity-errors.json.");
        scheduledSaveBoundaryStarted = -1; scheduledSaveBusyFrame = -1;
        return true;
    }

    private static bool TryRestoreCompletedTerminalAutosave(double now, string label)
    {
        if (terminalAutosaveWaitStarted < 0) terminalAutosaveWaitStarted = now;
        Require(now - terminalAutosaveWaitStarted < 60,
            "Actual terminal autosave was not completed before " + label + ".");
        if (!AtScheduledSaveBoundary(now, label)) return false;
        var slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
        if (!slots.HasSave(DungeonGameSaveSlotService.AutoSaveSlot)) return false;
        string path = scope.Container.Resolve<IDungeonSaveSlotCatalog>().GetPath(DungeonGameSaveSlotService.AutoSaveSlot);
        byte[] bytes = File.ReadAllBytes(path);
        string digest = Hex(bytes);
        if (digest == lastTerminalAutosaveSha) return false;
        var ui = scope.Container.Resolve<IUiClock>();
        double startedAt = EditorApplication.timeSinceStartup;
        var window = new JObject { ["label"] = label + "-production-autosave-restore",
            ["uiStart"] = ui.Time, ["uiEnd"] = null, ["blocksGameplayThread"] = true };
        officialSaveWindows.Add(window);
        try
        {
            Require(slots.TryLoad(DungeonGameSaveSlotService.AutoSaveSlot, out var report)
                && report.Success && report.Warnings.Count == 0,
                "Actual terminal autosave restore failed: " + string.Join(";", report.Errors));
            var owners = scope.Container.Resolve<IOwnerRunManagerProvider>();
            Require(owners.TryGetManager(out var manager) && manager.IsRunEnded
                && manager.CurrentOwnerActor != null && manager.CurrentOwnerActor.IsDead,
                "Actual completed autosave did not contain the terminal owner state.");
            Write(label + "-save.json", new JObject { ["path"] = path, ["bytes"] = bytes.Length,
                ["sha256"] = digest, ["restore"] = true, ["actualProductionFinalAutosave"] = true,
                ["verifierWroteReplacementSave"] = false, ["terminalOwnerRestored"] = true,
                ["observedWaitSeconds"] = now - terminalAutosaveWaitStarted });
            lastTerminalAutosaveSha = digest; terminalAutosaveWaitStarted = -1;
            return true;
        }
        finally
        {
            window["uiEnd"] = ui.Time;
            window["wallSeconds"] = EditorApplication.timeSinceStartup - startedAt;
        }
    }

    private static void Save(string label, bool restore)
    {
        var ui = scope.Container.Resolve<IUiClock>();
        double startedAt = EditorApplication.timeSinceStartup;
        var window = new JObject { ["label"] = label, ["uiStart"] = ui.Time,
            ["uiEnd"] = null, ["blocksGameplayThread"] = true };
        officialSaveWindows.Add(window);
        try
        {
        var slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
        var personas = Actors().Where(actor => actor.characterType == CharacterType.Customer && !actor.IsDead
                && actor.PersonaRuntime != null && actor.PersonaRuntime.HasGeneratedPersona)
            .ToDictionary(actor => actor.Identity.PersistentId, actor => new JObject
            {
                ["narrative"] = JObject.FromObject(actor.PersonaRuntime.CaptureGeneratedNarrative()),
                ["rules"] = PersonaRules(actor.PersonaRuntime.Persona)
            }, StringComparer.Ordinal);
        string path = slots.Save("r80-extended-" + label, true);
        var result = new JObject { ["path"] = path, ["bytes"] = new FileInfo(path).Length, ["restore"] = restore };
        if (restore)
        {
            Require(slots.TryLoad("r80-extended-" + label, out var report), string.Join(";", report.Errors));
            Require(report.Success && report.Warnings.Count == 0, "Save restore warning: " + string.Join(";", report.Warnings));
            result["restoredCharacters"] = report.RestoredCharacterCount;
            foreach (var pair in personas)
            {
                CharacterActor restored = Actors().SingleOrDefault(actor => actor.Identity.PersistentId == pair.Key);
                Require(restored != null && restored.PersonaRuntime != null && restored.PersonaRuntime.HasGeneratedPersona,
                    "Completed Persona was lost at official restore: " + pair.Key);
                Require(JToken.DeepEquals(pair.Value["narrative"],
                        JObject.FromObject(restored.PersonaRuntime.CaptureGeneratedNarrative()))
                    && JToken.DeepEquals(pair.Value["rules"], PersonaRules(restored.PersonaRuntime.Persona)),
                    "Official Persona restore changed its prose, trace or authored mechanics: " + pair.Key);
            }
            result["preservedCompletedPersonas"] = personas.Count;
            result["personaAuthoredMechanicsPreserved"] = true;
        }
        Write(label + "-save.json", result);
        }
        catch
        {
            try
            {
                Write(label + "-species-at-save-failure.json", JsonUtility.ToJson(
                    scope.Container.Resolve<ICharacterSpeciesPersistence>().Capture(), true));
            }
            catch (Exception diagnosticFailure)
            {
                Debug.LogWarning("Species failure evidence could not be captured: " + diagnosticFailure.Message);
            }
            throw;
        }
        finally
        {
            window["uiEnd"] = ui.Time;
            window["wallSeconds"] = EditorApplication.timeSinceStartup - startedAt;
        }
    }
    private static JObject PersonaRules(CustomerPersonaData value) => new JObject
    {
        ["selfCare"] = value.selfCareMultiplier, ["curiosity"] = value.curiosityMultiplier,
        ["shopping"] = value.shoppingMultiplier, ["patience"] = value.patienceMultiplier,
        ["hunger"] = value.hungerCurveMultiplier, ["fun"] = value.funCurveMultiplier,
        ["mood"] = value.moodCurveMultiplier, ["facilityTags"] = new JArray(value.preferredFacilityTags)
    };
    private static void Next(double now) { stage++; stageStart = now; }
    private static JObject Result(double now) => new JObject {
        ["status"] = failures.Count == 0 && !HasRecordQualityIssues() ? "PASS" : "RUNTIME_COMPLETED_WITH_MODEL_REJECTIONS",
        ["mode"] = facilityTargetedOnly ? "targeted-facility-save-and-hostile"
            : soak ? "continuous-real-time-soak" : hostileOnly ? "evolved-world-and-hostile-followup"
            : remainingOnly ? "facility-and-hostile-followup" : "actual-combat-and-profiles",
        ["realSoakSeconds"] = soak ? now - soakStart : 0, ["simulationFrames"] = soak ? Time.frameCount - framesAtStart : Time.frameCount,
        ["gameSeconds"] = soak ? accumulatedGameSeconds : 0,
        ["naturalRunCompletions"] = naturalRunCompletions,
        ["periodicMacroAdmissions"] = periodicMacroAdmissions,
        ["latency"] = LatencyObservation(),
        ["officialSaveWindows"] = officialSaveWindows,
        ["memoryGrowthBytes"] = soak ? Profiler.GetTotalAllocatedMemoryLong() - memoryAtStart : 0,
        ["successfulProfiles"] = new JArray(profiles.OrderBy(value => value, StringComparer.Ordinal)),
        ["failedRequests"] = failures.Count, ["unityErrors"] = errors.Count, ["injectedModelResponses"] = 0,
        ["recordConsumers"] = AuxiliaryObservation(),
        ["allLegacyInterfacesClaimedAsLiveFeatures"] = false };
    private static bool HasRecordQualityIssues()
    {
        var records = scope.Container.Resolve<ICharacterLogNarrativeService>() as CharacterLogNarrativeService;
        return records != null && (records.RejectedOriginalDisplayCount > 0 || records.ControlledFallbackCount > 0);
    }
    private static JObject Snapshot(double now, double elapsed) => new JObject { ["elapsed"] = elapsed, ["stage"] = stage,
        ["realSoakSeconds"] = soakStart == 0 ? 0 : now - soakStart, ["frame"] = Time.frameCount,
        ["timeScale"] = Time.timeScale, ["allocatedBytes"] = Profiler.GetTotalAllocatedMemoryLong(),
        ["latency"] = LatencyObservation(),
        ["hostStarting"] = queue != null && queue.IsBundledModelStarting,
        ["hostError"] = queue == null ? "missing" : queue.LastError,
        ["queue"] = queue == null ? "missing" : queue.GetRequestDiagnosticsForDebug(),
        ["equipment"] = EquipmentObservation(),
        ["historyRecovery"] = historyRecoveryOnly ? HistoryRecoveryObservation() : null,
        ["auxiliary"] = AuxiliaryObservation(),
        ["profiles"] = new JArray(profiles), ["actors"] = scope == null ? new JArray() : new JArray(Actors().Select(actor => new JObject {
            ["id"] = actor.Identity.PersistentId, ["cell"] = actor.GetNowXY().ToString(), ["canRunAi"] = actor.CanRunAi,
            ["state"] = actor.CurrentLifecycleState.ToString() })) };
    private static JObject AuxiliaryObservation()
    {
        if (scope == null) return null;
        var records = scope.Container.Resolve<ICharacterLogNarrativeService>() as CharacterLogNarrativeService;
        var personas = UnityEngine.Object.FindObjectsByType<CustomerPersonaRuntime>(FindObjectsSortMode.None);
        return new JObject { ["recordApplied"] = records?.AppliedCount ?? 0,
            ["recordControlledFallback"] = records?.ControlledFallbackCount ?? 0,
            ["recordRejectedOriginalVisible"] = records?.RejectedOriginalDisplayCount ?? 0,
            ["recordLastError"] = records?.LastError, ["bubbleApplied"] = observedBubble,
            ["personasApplied"] = personas.Count(value => value.HasGeneratedPersona),
            ["personasPending"] = personas.Count(value => value.PersonaRequestInProgress) };
    }
    private static JArray HistoryRecoveryObservation()
    {
        var rows = new List<JObject>();
        foreach (var component in UnityEngine.Object.FindObjectsByType<FacilityEvolutionStateComponent>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            FacilityEvolutionState state = component.CreateSnapshot().instanceEvolution;
            foreach (var request in state.narrativeRequests.Where(value => value.frozenHistoryCaptured && !value.cancelled))
            {
                EvolutionNode node = state.evolutionNodes.Single(value => value.nodeId == request.nodeId);
                rows.Add(new JObject { ["requestKey"] = request.requestKey, ["completed"] = request.completed,
                    ["attemptCount"] = request.attemptCount, ["playerVisible"] = node.playerVisible,
                    ["displayName"] = node.displayName, ["description"] = node.description,
                    ["schema"] = node.narrativeSchemaId, ["schemaHash"] = node.narrativeSchemaHash,
                    ["verdict"] = node.narrativePassVerdict, ["nodeEvidence"] = JArray.FromObject(node.evidenceIds),
                    ["frozen"] = new JObject { ["target"] = request.targetPersistentId,
                        ["node"] = request.nodeId, ["parent"] = request.parentNodeId, ["effect"] = request.effectId,
                        ["effectBudget"] = request.effectBudget, ["historyHash"] = request.historyHash,
                        ["evidence"] = JArray.FromObject(request.evidenceIds),
                        ["events"] = JArray.FromObject(request.frozenCurrentEvents),
                        ["mechanicallyUnlocked"] = node.mechanicallyUnlocked, ["nodeEffect"] = node.effectId } });
            }
        }
        return new JArray(rows.OrderBy(row => (string)row["requestKey"], StringComparer.Ordinal));
    }

    private static JObject EquipmentObservation()
    {
        if (equipment == null || string.IsNullOrEmpty(weaponId)) return null;
        var dispatch = scope.Container.Resolve<IEvolutionHistoryNarrativeRuntime>();
        var state = evolution.GetState(weaponId);
        var visible = equipment.Instances.SingleOrDefault(value => value.instanceId == weaponId);
        var nextScan = typeof(EvolutionHistoryNarrativeRuntime).GetField("nextScanAt",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return new JObject { ["id"] = weaponId, ["queryVisible"] = visible != null,
            ["queryPendingCount"] = visible?.evolution?.presentationRequests?.Count ?? -1,
            ["moduleRevision"] = state.moduleSelectionRevision, ["dispatcherPendingCount"] = dispatch.PendingCount,
            ["uiClockTime"] = scope.Container.Resolve<IUiClock>().Time,
            ["nextScanAt"] = nextScan == null ? -1f : (float)nextScan.GetValue(dispatch),
            ["pending"] = new JArray(state.presentationRequests.Select(value => new JObject {
                ["id"] = value.presentationId, ["state"] = value.state.ToString(), ["failures"] = value.failureCount,
                ["reason"] = value.lastFailureReason })) };
    }
    private static void TimedCompleted(string profile, string correlation, LocalLlmResult result,
        float queueSeconds, float serviceSeconds, float endToEndSeconds)
    {
        bool displayed = false;
        if (profile == LocalLlmRequestProfiles.BubbleLine.Id && result.IsSuccess)
        {
            string line = JObject.Parse(result.Content).Value<string>("line");
            displayed = Actors().Any(actor => actor.TryGetComponent(out CharacterDialogueRuntime dialogue)
                && dialogue.LastBubbleLine == line && dialogue.LastGeneratedBubbleLine == line);
        }
        float completedAt = scope.Container.Resolve<IUiClock>().Time;
        bool crossedSave = officialSaveWindows.OfType<JObject>().Any(window =>
            completedAt >= window.Value<float>("uiStart")
            && completedAt - endToEndSeconds <= (window["uiEnd"].Type == JTokenType.Null
                ? float.PositiveInfinity : window.Value<float>("uiEnd")));
        var row = new JObject { ["profile"] = profile, ["correlation"] = correlation,
            ["status"] = result.Status.ToString(), ["queueSeconds"] = queueSeconds,
            ["serviceSeconds"] = serviceSeconds, ["endToEndSeconds"] = endToEndSeconds,
            ["admittedDuringSoak"] = soakUiStart > 0 && completedAt - endToEndSeconds >= soakUiStart,
            ["crossedOfficialSaveRestore"] = crossedSave,
            ["bubbleDisplayed"] = displayed, ["frame"] = Time.frameCount };
        requestTimings.Add(row);
        Write("timing-" + (++timingSequence).ToString("D4") + ".json", row);
    }

    private static JObject LatencyObservation()
    {
        var measured = requestTimings.OfType<JObject>().Where(value => value.Value<bool>("admittedDuringSoak")).ToArray();
        var bubble = measured.Where(value => value.Value<string>("profile") == LocalLlmRequestProfiles.BubbleLine.Id).ToArray();
        var success = bubble.Where(value => value.Value<string>("status") == "Succeeded").ToArray();
        var interactive = measured.Where(value => new[] { "Persona", "MoodImpulse", "MacroGoal", "SocialRumor", "CharacterRecord", "BubbleLine" }
            .Contains(value.Value<string>("profile")) && value.Value<string>("status") == "Succeeded").ToArray();
        var normalBubble = success.Where(value => !value.Value<bool>("crossedOfficialSaveRestore")).ToArray();
        var normalInteractive = interactive.Where(value => !value.Value<bool>("crossedOfficialSaveRestore")).ToArray();
        return new JObject { ["measuredTerminalRequests"] = measured.Length,
            ["normalGameplayBubbleP95"] = Percentile(normalBubble, "endToEndSeconds", .95),
            ["normalGameplayInteractiveP95"] = Percentile(normalInteractive, "endToEndSeconds", .95),
            ["normalGameplayInteractiveQueueP95"] = Percentile(normalInteractive, "queueSeconds", .95),
            ["crossedSaveRestoreRequests"] = measured.Count(value => value.Value<bool>("crossedOfficialSaveRestore")),
            ["bubbleSucceeded"] = success.Length, ["bubbleDisplayed"] = success.Count(value => value.Value<bool>("bubbleDisplayed")),
            ["bubbleDropped"] = bubble.Count(value => value.Value<string>("status") == "Dropped"),
            ["bubbleCancelled"] = bubble.Count(value => value.Value<string>("status") == "Cancelled"),
            ["bubbleEndToEndP95"] = Percentile(success, "endToEndSeconds", .95),
            ["interactiveEndToEndP95"] = Percentile(interactive, "endToEndSeconds", .95),
            ["interactiveQueueP95"] = Percentile(interactive, "queueSeconds", .95),
            ["allCompletedQueueMax"] = measured.Length == 0 ? 0 : measured.Max(value => value.Value<float>("queueSeconds")),
            ["pendingAtObservation"] = queue != null ? queue.QueuedCount + queue.RunningCount : 0,
            ["queueAllPhasesLatencyAdmissionRejected"] = queue != null ? queue.LatencyAdmissionRejectedCount : 0,
            ["observedProducerIssued"] = observedBubbleProducers.Values.Sum(value => value[0]),
            ["observedProducerPendingSuppressed"] = observedBubbleProducers.Values.Sum(value => value[1]),
            ["observedProducerOffscreenSuppressed"] = observedBubbleProducers.Values.Sum(value => value[2]),
            ["observedProducerCapacitySuppressed"] = observedBubbleProducers.Values.Sum(value => value[3]) };
    }

    private static double Percentile(JObject[] rows, string field, double percentile)
    {
        if (rows.Length == 0) return 0;
        var values = rows.Select(value => value.Value<double>(field)).OrderBy(value => value).ToArray();
        return values[Math.Max(0, (int)Math.Ceiling(values.Length * percentile) - 1)];
    }

    private static void Completed(string profile, string correlation, LocalLlmResult result)
    {
        if (result.IsSuccess && profile == LocalLlmRequestProfiles.BubbleLine.Id && scope != null)
        {
            string line = JObject.Parse(result.Content).Value<string>("line");
            var actor = Actors().FirstOrDefault(value => value.TryGetComponent(out CharacterDialogueRuntime dialogue)
                && !string.IsNullOrWhiteSpace(line) && dialogue.LastGeneratedBubbleLine == line);
            if (actor != null)
                observedBubble = new JObject { ["actorId"] = actor.Identity.PersistentId,
                    ["line"] = line, ["frame"] = Time.frameCount, ["correlation"] = correlation };
        }
        Write("response-" + (++sequence).ToString("D4") + ".json", new JObject { ["profile"] = profile, ["correlation"] = correlation,
            ["status"] = result.Status.ToString(), ["rawModelContent"] = result.RawModelContent,
            ["content"] = result.Content, ["error"] = result.Error, ["frame"] = Time.frameCount });
        if (result.IsSuccess) profiles.Add(profile);
        if (result.Status == LocalLlmRequestStatus.Failed || result.Status == LocalLlmRequestStatus.TimedOut)
            failures.Add(new JObject { ["profile"] = profile, ["status"] = result.Status.ToString(), ["error"] = result.Error });
    }
    private static void StartSavePhaseTrace()
    {
        Require(DungeonSaveEditorTrace.Observer == null && savePhaseWriter == null, "Save observer already owned.");
        string[] args = Environment.GetCommandLineArgs();
        int log = Array.IndexOf(args, "-logFile");
        Require(log >= 0 && log + 1 < args.Length, "Isolated evidence log required.");
        string root = Path.GetDirectoryName(Path.GetFullPath(args[log + 1]));
        Require(root.StartsWith(Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Save evidence outside QA.");
        savePhaseWriter = new StreamWriter(new FileStream(Path.Combine(root, "save-phase-trace.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        saveTraceStart = System.Diagnostics.Stopwatch.GetTimestamp();
        DungeonSaveEditorTrace.Observer = phase => savePhaseWriter.WriteLine(new JObject {
            ["milliseconds"] = (System.Diagnostics.Stopwatch.GetTimestamp() - saveTraceStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency,
            ["phase"] = phase, ["frame"] = Time.frameCount }.ToString(Newtonsoft.Json.Formatting.None));
    }

    private static void Finish(int code)
    {
        traitDiagnosticSubscription?.Dispose();
        traitDiagnosticSubscription = null;
        if (savePhaseWriter != null)
        {
            DungeonSaveEditorTrace.Observer = null;
            savePhaseWriter.Dispose();
            savePhaseWriter = null;
        }
        if (queue != null)
        {
            queue.ActualRequestCompletedForDiagnostics -= Completed;
            queue.ActualRequestTimingForDiagnostics -= TimedCompleted;
        }
        Application.logMessageReceived -= Log;
        SessionState.SetBool(Key + "pending", false); SessionState.SetBool(Key + "exit", true); SessionState.SetInt(Key + "code", code);
        EditorApplication.update -= Pump; EditorApplication.playModeStateChanged += AfterStop;
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else EditorApplication.delayCall += Exit;
    }
    private static void AfterStop(PlayModeStateChange state) { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Exit; }
    private static void Exit()
    {
        if (!SessionState.GetBool(Key + "exit", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Key + "exit", false); EditorApplication.playModeStateChanged -= AfterStop;
        EditorApplication.Exit(SessionState.GetInt(Key + "code", 1));
    }
    private static string Hex(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static void Write(string name, object content)
    {
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-logFile");
        Require(index >= 0 && index + 1 < args.Length, "Isolated evidence argument missing.");
        string root = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        Require(root.StartsWith(Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Evidence outside QA.");
        using var writer = new StreamWriter(new FileStream(Path.Combine(root, name), FileMode.CreateNew), new UTF8Encoding(false));
        writer.Write(content.ToString());
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
public sealed class R80ExtendedCoroutineHost : MonoBehaviour { }
#endif
