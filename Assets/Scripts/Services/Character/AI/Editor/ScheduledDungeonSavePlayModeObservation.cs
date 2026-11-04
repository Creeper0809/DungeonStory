#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using VContainer;

// 실제 저장 슬롯과 생산 Tick을 관측한다. 응답 주입이나 수동 Advance 호출은 하지 않는다.
public static class ScheduledDungeonSavePlayModeObservation
{
    private const string Slot = "r80-extended-save-profile-final";
    private static DungeonRuntimeLifetimeScope scope;
    private static IDungeonSaveOperation operation;
    private static Dictionary<string, JObject> personas;
    private static double started;
    private static int firstFrame;
    private static readonly Dictionary<string, ProfilerRecorder> frameRecorders = new();
    private static readonly Dictionary<string, long[]> frameTotals = new();
    private static readonly JArray frameIntervals = new();
    private static readonly JArray availableMarkerNames = new();
    private static int lastProfileFrame;
    private static double lastProfileTime;
    private static int sampledFrames;
    private static JObject batchComparison;
    private static float simulationTimeAtBegin;
    private static float uiTimeAtBegin;
    private static float scaleAtBegin;
    private static int playerSpeedAtBegin;
    private static bool playerPausedAtBegin;
    private static int pausedFrameChecks;
    private static float maximumPendingGameTimeAdvance;

    public static void Begin(DungeonRuntimeLifetimeScope runtimeScope)
    {
        scope = runtimeScope;
        personas = Actors().Where(actor => actor.characterType == CharacterType.Customer && !actor.IsDead
            && actor.PersonaRuntime != null && actor.PersonaRuntime.HasGeneratedPersona)
            .ToDictionary(actor => actor.Identity.PersistentId, actor => new JObject
            {
                ["narrative"] = JObject.FromObject(actor.PersonaRuntime.CaptureGeneratedNarrative()),
                ["rules"] = Rules(actor.PersonaRuntime.Persona)
            }, StringComparer.Ordinal);
        batchComparison = VerifyPerformanceBatch();
        StartFrameProfile();
        started = EditorApplication.timeSinceStartup;
        firstFrame = Time.frameCount;
        simulationTimeAtBegin = scope.Container.Resolve<DungeonStory.Foundation.IGameClock>().Time;
        uiTimeAtBegin = scope.Container.Resolve<DungeonStory.Foundation.IUiClock>().Time;
        scaleAtBegin = scope.Container.Resolve<DungeonStory.Foundation.IGameTimeScaleController>().Scale;
        var speed = scope.Container.Resolve<IGameSpeedController>();
        playerSpeedAtBegin = speed.Speed;
        playerPausedAtBegin = speed.IsPaused;
        pausedFrameChecks = 0;
        maximumPendingGameTimeAdvance = 0;
        Require(ReferenceEquals(scope.Container.Resolve<DungeonStory.Foundation.IGameTimeScaleController>(),
            scope.Container.Resolve<DungeonStory.Foundation.IGameTimeScalePauseAuthority>()),
            "Save pause and gameplay speed have different clock owners.");
        operation = scope.Container.Resolve<IDungeonGameSaveSlotService>().BeginSave(Slot, true);
        Require(!operation.IsCompleted, "Save acceptance was reported as completion.");
        ObservePendingSimulation();
    }

    public static JObject Poll()
    {
        Require(operation != null, "Scheduled save observation was not started.");
        SampleFrameProfile();
        if (!operation.IsCompleted) { ObservePendingSimulation(); return null; }
        JObject frameProfile = FinishFrameProfile();
        Require(operation.Succeeded, "Actual scheduled save failed: " + operation.Error);
        Require(scope.Container.Resolve<DungeonStory.Foundation.IGameTimeScaleController>().Scale == scaleAtBegin,
            "Completed save did not restore the original effective game speed.");
        Require(pausedFrameChecks > 1 && maximumPendingGameTimeAdvance < 0.001f,
            "Game simulation advanced while the save owned its pause.");
        float uiElapsed = scope.Container.Resolve<DungeonStory.Foundation.IUiClock>().Time - uiTimeAtBegin;
        Require(uiElapsed > 0 && scope.Container.Resolve<IGameSpeedController>().Speed == playerSpeedAtBegin
            && scope.Container.Resolve<IGameSpeedController>().IsPaused == playerPausedAtBegin,
            "Save stopped the UI clock or changed player speed/pause preference.");
        int frames = Time.frameCount - firstFrame;
        Require(frames >= 2 && operation.MainThreadStepCount > 2,
            "Actual production save did not advance across game frames.");
        double saveSeconds = EditorApplication.timeSinceStartup - started;
        long bytes = new FileInfo(operation.Path).Length;
        Require(operation.CommitResult.Status == DungeonDurableSaveCommitStatus.Deferred,
            "Interleaved save unexpectedly collected current live receipts.");
        double restoreStarted = EditorApplication.timeSinceStartup;
        var slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
        Require(slots.TryLoad(Slot, out var report), string.Join(";", report.Errors));
        Require(report.Success && report.Warnings.Count == 0, "Scheduled save restore had warnings.");
        Require(scope.Container.Resolve<IGameSpeedController>().Speed == playerSpeedAtBegin
            && scope.Container.Resolve<IGameSpeedController>().IsPaused == playerPausedAtBegin,
            "Temporary save pause was persisted instead of player preference.");
        foreach (var pair in personas)
        {
            CharacterActor restored = Actors().SingleOrDefault(actor => actor.Identity.PersistentId == pair.Key);
            Require(restored != null && restored.PersonaRuntime != null && restored.PersonaRuntime.HasGeneratedPersona,
                "Completed Persona was lost: " + pair.Key);
            Require(JToken.DeepEquals(pair.Value["narrative"],
                    JObject.FromObject(restored.PersonaRuntime.CaptureGeneratedNarrative()))
                && JToken.DeepEquals(pair.Value["rules"], Rules(restored.PersonaRuntime.Persona)),
                "Scheduled save/restore changed Persona prose or authored mechanics: " + pair.Key);
        }
        return new JObject
        {
            ["path"] = operation.Path, ["bytes"] = bytes, ["restore"] = true,
            ["saveWallSeconds"] = saveSeconds,
            ["performanceBatchComparison"] = batchComparison,
            ["simulationPausedDuringPendingSave"] = true, ["pausedFrameChecks"] = pausedFrameChecks,
            ["maximumGameTimeAdvanceWhilePending"] = maximumPendingGameTimeAdvance,
            ["uiClockAdvanceSeconds"] = uiElapsed, ["playerPauseAndSpeedPreferencePreserved"] = true,
            ["captureMainThreadSeconds"] = operation.CaptureSeconds,
            ["longestMainThreadStepSeconds"] = operation.LongestMainThreadStepSeconds,
            ["mainThreadStepCount"] = operation.MainThreadStepCount,
            ["gameFramesDuringSave"] = frames,
            ["restoreSeconds"] = EditorApplication.timeSinceStartup - restoreStarted,
            ["restoredCharacters"] = report.RestoredCharacterCount,
            ["preservedCompletedPersonas"] = personas.Count,
            ["personaAuthoredMechanicsPreserved"] = true,
            ["durableReceiptCollection"] = operation.CommitResult.Status.ToString(),
            ["durableReceiptCollectionReason"] = operation.CommitResult.Message,
            ["manualAdvanceCalls"] = 0, ["injectedModelResponses"] = 0,
            ["instantSaveClaimed"] = false,
            ["frameProfile"] = frameProfile
        };
    }

    private static void StartFrameProfile()
    {
        FinishFrameProfile();
        frameTotals.Clear();
        frameIntervals.Clear();
        availableMarkerNames.Clear();
        sampledFrames = 0;
        var names = new HashSet<string>(StringComparer.Ordinal)
        {
            "Main Thread", "PlayerLoop", "EditorLoop", "WaitForTargetFPS", "GC.Collect",
            "Physics.Simulate", "Physics2D.Simulate", "Physics.Processing", "FixedUpdate",
            "BehaviourUpdate", "Update.ScriptRunBehaviourUpdate", "WaitForLastPresentationAndUpdateTime",
            "CharacterAiScheduler.ProcessAiBudget", "CharacterStatMaintenanceRuntime.Tick",
            "AcquiredTrait.Tick", "AcquiredTrait.Evaluate", "AcquiredTrait.StaleOwner",
            "AcquiredTrait.StaleEvidence", "AcquiredTrait.CaptureState",
            "Save.ReadPayload", "Save.ReadSnapshotCopy", "Save.ReadTokenReplay", "Save.ReadSourceMiss",
            "WorkOrderRuntime.Tick", "CharacterBodyHealthRuntime.Tick", "WildlifeRuntime.Tick",
            "Update", "PreLateUpdate", "PostLateUpdate", "EarlyUpdate", "PreUpdate",
            "FixedUpdate.ScriptRunBehaviourFixedUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate"
        };
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach (ProfilerRecorderHandle handle in handles)
        {
            if (!handle.Valid) continue;
            ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
            availableMarkerNames.Add(description.Name);
            bool selected = names.Contains(description.Name)
                || description.Name.EndsWith(".Tick", StringComparison.Ordinal)
                || description.Name.EndsWith(".LateTick", StringComparison.Ordinal)
                || description.Name.IndexOf("VContainer", StringComparison.Ordinal) >= 0;
            if (!selected || frameRecorders.ContainsKey(description.Name)) continue;
            frameRecorders.Add(description.Name, new ProfilerRecorder(handle, 1,
                ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately));
            frameTotals.Add(description.Name, new long[3]);
        }
        lastProfileFrame = Time.frameCount;
        lastProfileTime = EditorApplication.timeSinceStartup;
        EditorApplication.playModeStateChanged += ProfilePlayModeChanged;
    }

    private static void SampleFrameProfile()
    {
        if (Time.frameCount == lastProfileFrame) return;
        double now = EditorApplication.timeSinceStartup;
        frameIntervals.Add((now - lastProfileTime) * 1000);
        lastProfileTime = now;
        lastProfileFrame = Time.frameCount;
        foreach (var pair in frameRecorders)
        {
            if (!pair.Value.Valid) continue;
            long value = Math.Max(0, pair.Value.LastValue);
            long[] totals = frameTotals[pair.Key];
            totals[0]++;
            totals[1] += value;
            totals[2] = Math.Max(totals[2], value);
        }
        sampledFrames++;
        if (sampledFrames % 16 == 0) WritePartialFrameProfile();
    }

    private static JObject FinishFrameProfile()
    {
        EditorApplication.playModeStateChanged -= ProfilePlayModeChanged;
        JObject result = FrameProfileSnapshot();
        foreach (var pair in frameRecorders) pair.Value.Dispose();
        frameRecorders.Clear();
        return result;
    }

    private static JObject FrameProfileSnapshot()
    {
        var metrics = new JObject();
        foreach (var pair in frameRecorders)
        {
            long[] totals = frameTotals[pair.Key];
            metrics[pair.Key] = new JObject
            {
                ["valid"] = pair.Value.Valid, ["samples"] = totals[0],
                ["totalNanoseconds"] = totals[1], ["maximumNanoseconds"] = totals[2]
            };
        }
        return new JObject
        {
            ["markers"] = metrics, ["frameIntervalMilliseconds"] = new JArray(frameIntervals),
            ["availableMarkerNames"] = new JArray(availableMarkerNames),
            ["runInBackground"] = Application.runInBackground,
            ["targetFrameRate"] = Application.targetFrameRate,
            ["fixedDeltaTime"] = Time.fixedDeltaTime, ["maximumDeltaTime"] = Time.maximumDeltaTime,
            ["diagnosticOnly"] = true, ["samplesAreLastCompletedFrame"] = true,
            ["overlappingMarkersMustNotBeSummed"] = true
        };
    }

    private static void WritePartialFrameProfile()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-logFile");
        Require(index >= 0 && index + 1 < args.Length, "Isolated diagnostic log path is missing.");
        string root = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        Require(root.StartsWith(Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Frame diagnostic output is outside QA.");
        var snapshot = FrameProfileSnapshot();
        snapshot["sampledFrames"] = sampledFrames;
        snapshot["partialDiagnosticOnly"] = true;
        snapshot["saveCompleted"] = operation != null && operation.IsCompleted;
        using var writer = new StreamWriter(new FileStream(Path.Combine(root,
            "save-frame-partial-" + sampledFrames.ToString("D4") + ".json"), FileMode.CreateNew));
        writer.Write(snapshot.ToString());
    }

    private static void ProfilePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) FinishFrameProfile();
    }

    private static CharacterActor[] Actors() => scope.Container.Resolve<ICharacterWorldQuery>().Characters
        .Where(actor => actor != null && !actor.IsDead && actor.Identity != null).ToArray();

    private static JObject VerifyPerformanceBatch()
    {
        var performance = scope.Container.Resolve<ICharacterPerformanceQuery>();
        string[] ids = { "performance:survival:cold-exposure", "performance:survival:heat-exposure",
            "performance:survival:cold-exposure" };
        int compared = 0, traceIsolation = 0, failures = 0;
        CharacterActor[] actors = Actors();
        foreach (CharacterActor actor in actors)
        {
            var expected = ids.Select(id => performance.Evaluate(actor, id)).ToArray();
            var actual = performance.EvaluateMany(actor, ids);
            Require(actual.Count == expected.Length, "Performance batch omitted an original result.");
            for (int index = 0; index < expected.Length; index++)
            {
                Require(JToken.DeepEquals(JToken.FromObject(expected[index]), JToken.FromObject(actual[index])),
                    "Batched value, failure or complete contribution trace differs for " + actor.Identity.PersistentId);
                compared++;
            }
            Require(actual[0].Contributions.Count > 0 && actual[2].Contributions.Count > 0,
                "Actual performance fixture contains no contribution trace.");
            string previous = actual[2].Contributions[0].Detail;
            actual[0].Contributions[0].Detail = "controlled-result-copy-check";
            Require(actual[2].Contributions[0].Detail == previous,
                "Separate results share mutable capacity traces.");
            traceIsolation++;
            Require(Failure(() => performance.Evaluate(actor, "qa:missing-performance"))
                == Failure(() => performance.EvaluateMany(actor, new[] { "qa:missing-performance" })),
                "Batched missing-formula rejection changed.");
            var context = new CharacterPerformanceEvaluationContext { ContextFactor = float.NaN };
            Require(Failure(() => performance.Evaluate(actor, ids[0], context))
                == Failure(() => performance.EvaluateMany(actor, ids, context)),
                "Batched invalid-context rejection changed.");
            failures += 2;
        }
        Require(compared > 0, "No actual actor was compared.");
        return new JObject { ["actors"] = actors.Length, ["comparedResults"] = compared,
            ["fullValueAndTraceExact"] = true, ["isolatedResultTraces"] = traceIsolation,
            ["unchangedFailureComparisons"] = failures, ["stateCommandsIssued"] = 0 };
    }

    private static void ObservePendingSimulation()
    {
        var clock = scope.Container.Resolve<DungeonStory.Foundation.IGameClock>();
        Require(clock.IsPaused, "Pending production save did not hold its simulation pause.");
        Require(scope.Container.Resolve<IGameSpeedController>().Speed == playerSpeedAtBegin
            && scope.Container.Resolve<IGameSpeedController>().IsPaused == playerPausedAtBegin,
            "Temporary save hold overwrote the player's persisted preference.");
        pausedFrameChecks++;
        maximumPendingGameTimeAdvance = Mathf.Max(maximumPendingGameTimeAdvance,
            Mathf.Abs(clock.Time - simulationTimeAtBegin));
    }

    private static string Failure(Action action)
    {
        try { action(); }
        catch (Exception exception) { return exception.GetType().FullName + ":" + exception.Message; }
        throw new InvalidOperationException("Expected original query rejection.");
    }
    private static JObject Rules(CustomerPersonaData value) => new()
    {
        ["selfCare"] = value.selfCareMultiplier, ["curiosity"] = value.curiosityMultiplier,
        ["shopping"] = value.shoppingMultiplier, ["patience"] = value.patienceMultiplier,
        ["hunger"] = value.hungerCurveMultiplier, ["fun"] = value.funCurveMultiplier,
        ["mood"] = value.moodCurveMultiplier, ["facilityTags"] = new JArray(value.preferredFacilityTags)
    };
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
