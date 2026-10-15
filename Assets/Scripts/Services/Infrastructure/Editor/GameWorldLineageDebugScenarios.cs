#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public static class GameWorldLineageDebugScenarios
{
    private const string PendingKey = "DungeonStory.WorldLineage.Pending";
    private const string StartedKey = "DungeonStory.WorldLineage.Started";
    private const string PreparedKey = "DungeonStory.WorldLineage.Prepared";
    private const string WarmupKey = "DungeonStory.WorldLineage.Warmup";
    private static IEnumerator verification;

    public static void RequestActualContainerForBatch()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "World lineage test requires isolated batch execution.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "PlayMode already active.");
        Require(!File.Exists(EvidencePath("world-lineage-report.json")), "Create-only output exists.");
        SessionState.SetBool(PendingKey, true);
        verification = null;
        SessionState.SetBool(PreparedKey, false);
        SessionState.SetInt(WarmupKey, 0);
        SessionState.SetString(StartedKey, EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayScene.unity", OpenSceneMode.Single);
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
    }

    private static void Pump()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        try
        {
            double started = double.Parse(SessionState.GetString(StartedKey, ""), CultureInfo.InvariantCulture);
            if (EditorApplication.timeSinceStartup - started > 60)
                throw new InvalidOperationException("Actual lineage container was not ready within 60 seconds.");
            if (!EditorApplication.isPlaying) return;
            var scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(s => s?.Container != null);
            if (scope == null) return;
            var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Require(game != null, "Actual game manager missing.");
            game.isPause = true;
            scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
            Time.timeScale = 0;
            var owners = scope.Container.Resolve<IOwnerRunManagerProvider>();
            Require(owners.TryGetManager(out var manager), "Official owner manager missing.");
            if (!SessionState.GetBool(PreparedKey, false))
            {
                string preparation = manager.CurrentOwnerActor == null
                    ? StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug() : "owner-already-present";
                Require(manager.CurrentOwnerActor != null && (preparation == "owner-already-present"
                    || preparation.StartsWith("committed=True;", StringComparison.Ordinal)),
                    "Official party preparation failed: " + preparation);
                WriteNew("test-preparation.txt", "Controlled official starting-party command; not a training event.\n" + preparation);
                SessionState.SetBool(PreparedKey, true);
                SessionState.SetInt(WarmupKey, 5);
                return;
            }
            int warmup = SessionState.GetInt(WarmupKey, 0);
            if (warmup > 0) { SessionState.SetInt(WarmupKey, warmup - 1); return; }
            Require(manager.CurrentOwnerActor != null, "Prepared owner disappeared.");
            verification ??= Verify(scope.Container);
            if (!verification.MoveNext()) Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try { WriteNew("world-lineage-failure.txt", exception.ToString()); }
            finally { Finish(1); }
        }
    }

    private static IEnumerator Verify(IObjectResolver container)
    {
        using var trace = new MemoryTrace();
        var query = container.Resolve<IGameWorldLineageQuery>();
        var commands = container.Resolve<IGameWorldLineageCommands>();
        var owner = container.Resolve<GameWorldLineageRuntime>();
        Require(ReferenceEquals(query, owner) && ReferenceEquals(commands, owner)
            && container.Resolve<IEnumerable<IDungeonRestoreTransactionParticipant>>()
                .Count(p => ReferenceEquals(p, owner)) == 1, "Lineage has multiple owners or missing participant.");
        var persistence = container.Resolve<IGameplayOutcomePersistence>();
        var service = container.Resolve<IDungeonGameSaveService>();
        string initial = JsonUtility.ToJson(query.Snapshot.Capture());
        Require(query.Snapshot.LedgerRunId == persistence.CaptureGameplayOutcomes().runId,
            "New game lineage is detached from the live ledger.");
        var independent = new GameWorldLineageRuntime(persistence);
        Require(independent.Snapshot.WorldInstanceId != query.Snapshot.WorldInstanceId
            && independent.Snapshot.CampaignInstanceId != query.Snapshot.CampaignInstanceId
            && independent.Snapshot.RunInstanceId != query.Snapshot.RunInstanceId
            && independent.Snapshot.BranchInstanceId != query.Snapshot.BranchInstanceId,
            "Independent owner reused lineage IDs.");
        var detached = query.Snapshot.Capture();
        detached.worldInstanceId = "mutated";
        Require(JsonUtility.ToJson(query.Snapshot.Capture()) == initial, "DTO mutation reached live owner.");

        DungeonGameSaveData baseline = service.Capture();
        string baselineJson = service.ToJson(baseline, true);
        WriteNew("official-world-before.json", baselineJson);
        JObject exported = GameplayModuleSelectionContinuousSourceV9Exporter.ExtractWorldLineage(JObject.Parse(baselineJson));
        Require(JToken.DeepEquals(exported, JObject.Parse(initial)), "Exporter changed the saved lineage.");
        Require(LineageJson(service.Capture()) == initial, "Repeated capture regenerated lineage.");
        Debug.Log("B140_LINEAGE_ACTUAL_OWNER_CAPTURE_EXPORT=PASS");

        commands.ForkBranch(query.Snapshot.BranchInstanceId);
        GameWorldLineageSaveData fork = query.Snapshot.Capture();
        var original = JsonUtility.FromJson<GameWorldLineageSaveData>(initial);
        Require(fork.branchInstanceId != original.branchInstanceId && fork.branchAncestors.Length == 1
            && fork.branchAncestors[0].branchInstanceId == original.branchInstanceId
            && fork.worldInstanceId == original.worldInstanceId && fork.campaignInstanceId == original.campaignInstanceId
            && fork.runInstanceId == original.runInstanceId && fork.ledgerRunId == original.ledgerRunId,
            "Explicit fork changed unrelated identity or lost ancestry.");
        string forkJson = JsonUtility.ToJson(fork);
        Reject(() => commands.ForkBranch(original.branchInstanceId));
        Require(JsonUtility.ToJson(query.Snapshot.Capture()) == forkJson, "Stale fork command mutated owner.");
        DungeonGameSaveData forkSave = service.FromJson(service.ToJson(service.Capture()));
        trace.Record("case.restore-baseline.begin");
        Require(service.TryRestore(service.FromJson(baselineJson), out var restore),
            "Official baseline restore failed: " + string.Join(" | ", restore.Errors));
        Require(LineageJson(service.Capture()) == initial, "Official restore regenerated or lost lineage.");
        // Real gameplay retires destroyed world objects at the end of the frame.
        // Do not stack multiple full-world replacements in a single editor update.
        for (int retireAfter = Time.frameCount + 2; Time.frameCount < retireAfter;) yield return null;
        GC.Collect(); // Test-only: reclaim detached validation snapshots before the next full-world replacement.
        trace.Record("case.restore-baseline.retired");
        trace.Record("case.restore-fork.begin");
        Require(service.TryRestore(forkSave, out var forkRestore),
            "Official fork restore failed: " + string.Join(" | ", forkRestore.Errors));
        Require(LineageJson(service.Capture()) == forkJson, "Fork ancestry did not survive official JSON restore.");
        for (int retireAfter = Time.frameCount + 2; Time.frameCount < retireAfter;) yield return null;
        GC.Collect();
        trace.Record("case.restore-fork.retired");
        Debug.Log("B140_LINEAGE_OFFICIAL_WORLD_ROUNDTRIP=PASS");

        DungeonGameSaveData mismatch = service.FromJson(service.ToJson(forkSave));
        JObject wrong = JObject.Parse(LineageJson(mismatch));
        wrong["ledgerRunId"] = "run:" + Guid.NewGuid().ToString("N");
        mismatch.sections.Single(s => s.sectionId == GameWorldLineageSaveSection.Id).payloadJson = wrong.ToString();
        mismatch.manifest = DungeonSaveManifest.Capture(mismatch.sections); // controlled invalid save, not a real capture
        Require(!service.TryRestore(mismatch, out var invalid) && invalid.Errors.Any(e => e.Contains("world-lineage-ledger-run-mismatch"))
            && LineageJson(service.Capture()) == forkJson, "Mismatched lineage was accepted or partly published.");
        Reject(() => GameplayModuleSelectionContinuousSourceV9Exporter.ExtractWorldLineage(JObject.Parse(service.ToJson(mismatch))));
        DungeonGameSaveData missing = service.FromJson(service.ToJson(forkSave));
        missing.sections.RemoveAll(s => s.sectionId == GameWorldLineageSaveSection.Id);
        missing.manifest = DungeonSaveManifest.Capture(missing.sections);
        Require(!service.TryRestore(missing, out var absent) && absent.Errors.Any(e => e.Contains("world-lineage-or-ledger-section-missing"))
            && LineageJson(service.Capture()) == forkJson, "Missing lineage was silently generated on restore.");
        Reject(() => GameplayModuleSelectionContinuousSourceV9Exporter.ExtractWorldLineage(JObject.Parse(service.ToJson(missing))));
        Debug.Log("B140_LINEAGE_MISMATCH_MISSING_NO_MUTATION=PASS");

        VerifyTransactionalFailure(persistence);
        VerifyMalformedPayloads(forkJson);
        VerifyEmptyStaffObservation();
        Require(StaffDiscontentDebugScenarios.RunStrictSaveBoundary(
            container.Resolve<ICharacterAiWorldRegistry>(), container.Resolve<ICharacterSettlementStandingQuery>()),
            "Existing populated staff save boundary failed.");
        Debug.Log("B140_STAFF_POPULATED_SAVE_REGRESSION=PASS");
        trace.Record("case.restore-cleanup.begin");
        Require(service.TryRestore(service.FromJson(baselineJson), out var cleanup),
            "Baseline cleanup failed: " + string.Join(" | ", cleanup.Errors));
        Require(LineageJson(service.Capture()) == initial, "Final baseline identity differs.");
        for (int retireAfter = Time.frameCount + 2; Time.frameCount < retireAfter;) yield return null;
        GC.Collect();
        trace.Record("case.restore-cleanup.retired");
        WriteNew("world-lineage-report.json", new JObject
        {
            ["status"] = "PASS", ["scope"] = "actual-gameplay-container-official-save-and-controlled-faults",
            ["worldLineage"] = exported, ["sectionCount"] = baseline.sections.Count,
            ["officialRestoreCount"] = 3, ["malformedCases"] = 9,
            ["staffEmptyAndPopulatedSaveRegression"] = true,
            ["worldRestoreWarnings"] = new JArray(restore.Warnings.Concat(forkRestore.Warnings).Concat(cleanup.Warnings)),
            ["trainingEligible"] = false, ["freshSource9Package"] = false,
            ["modelInference"] = false, ["humanApprovalClaimed"] = false
        }.ToString());
    }

    private static void VerifyTransactionalFailure(IGameplayOutcomePersistence persistence)
    {
        var owner = new GameWorldLineageRuntime(persistence);
        var section = new GameWorldLineageSaveSection(owner);
        var fault = new LatePublishFault(owner);
        var registry = new DungeonSaveSectionRegistry(new IDungeonSaveSection[] { section },
            new DungeonRuntimeAggregateRootStore(), new IDungeonRestoreTransactionParticipant[] { owner, fault });
        List<DungeonSaveSectionEnvelope> before = registry.CaptureAll();
        owner.ForkBranch(owner.Snapshot.BranchInstanceId);
        string live = section.Capture();
        fault.ExpectedPublishedBranch = JsonUtility.FromJson<GameWorldLineageSaveData>(before[0].payloadJson).branchInstanceId;
        var failure = new DungeonGameRestoreReport();
        Require(!registry.RestoreAll(before, failure) && fault.ObservedCandidate
            && failure.Errors.Any(e => e.Contains("b140-controlled-late-publish-failure")) && section.Capture() == live,
            "Late participant failure did not roll back lineage publication.");
        fault.Fail = false;
        var retry = new DungeonGameRestoreReport();
        Require(registry.RestoreAll(before, retry) && section.Capture() == before[0].payloadJson,
            "Failed restore left a candidate behind or prevented retry: " + string.Join(" | ", retry.Errors));
        owner.BeginRestoreCandidate();
        try { Reject(() => owner.ForkBranch(owner.Snapshot.BranchInstanceId)); }
        finally { owner.DiscardRestoreCandidate(); }
        Debug.Log("B140_LINEAGE_PUBLISH_FAILURE_ROLLBACK_RETRY=PASS");
    }

    private static void VerifyMalformedPayloads(string valid)
    {
        Action<JObject>[] mutations =
        {
            j => j.Remove("version"), j => j["worldInstanceId"] = "package:invented-world",
            j => j["branchAncestors"] = null, j => j["branchInstanceId"] = j["branchInstanceId"].ToString().ToUpperInvariant(),
            j => j["unexpected"] = true,
            j => j["branchAncestors"][0]["branchInstanceId"] = j["branchInstanceId"].ToString(),
            j => ((JArray)j["branchAncestors"]).Add(j["branchAncestors"][0].DeepClone()),
            j => j["branchAncestors"][0]["worldInstanceId"] = "world:" + Guid.NewGuid().ToString("N")
        };
        foreach (var mutate in mutations)
        {
            JObject bad = JObject.Parse(valid); mutate(bad);
            Reject(() => GameWorldLineageSaveSection.ReadSnapshot(bad.ToString()));
        }
        Reject(() => GameWorldLineageSaveSection.ReadSnapshot(valid.Insert(1, "\"version\":1,")));
        Debug.Log("B140_LINEAGE_REQUIRED_SHAPE_9_NEGATIVES=PASS");
    }

    private static void VerifyEmptyStaffObservation()
    {
        string json = JsonUtility.ToJson(new DungeonStaffDiscontentSaveData());
        var roundtrip = JsonUtility.FromJson<DungeonStaffDiscontentSaveData>(json);
        var report = new DungeonGameRestoreReport();
        StaffDiscontentSaveSection.ValidatePayload(roundtrip, report);
        Require(report.Success && JsonUtility.ToJson(roundtrip) == json,
            "No-event staff observation did not survive JSON roundtrip.");
        Action<StaffDiscontentObservationData>[] mutations =
        {
            o => o.fact = null, o => o.fact.before = null, o => o.fact.revision = 1,
            o => o.pending = true, o => o.notificationPending = true, o => o.publicationKey = "invented",
            o => o.fact.responderId = "character:invented", o => o.fact.kind = StaffDiscontentFactKind.Response,
            o => o.fact.before.mood = 0, o => o.fact.after.staffId = "character:invented"
        };
        foreach (var mutate in mutations)
        {
            var bad = JsonUtility.FromJson<DungeonStaffDiscontentSaveData>(json);
            mutate(bad.responseObservation);
            var invalid = new DungeonGameRestoreReport();
            StaffDiscontentSaveSection.ValidatePayload(bad, invalid);
            Require(!invalid.Success, "Nonempty or pending fact passed as an empty staff observation.");
        }
        Debug.Log("B140_STAFF_EMPTY_JSON_10_NEGATIVES=PASS");
    }

    private sealed class LatePublishFault : IDungeonRestoreTransactionParticipant
    {
        private readonly GameWorldLineageRuntime owner;
        public LatePublishFault(GameWorldLineageRuntime owner) { this.owner = owner; }
        public string ParticipantId => "zz.b140-controlled-fault";
        public bool Fail = true, ObservedCandidate;
        public string ExpectedPublishedBranch;
        public void BeginRestoreCandidate() { }
        public void PublishRestoreCandidate()
        {
            ObservedCandidate = owner.Snapshot.BranchInstanceId == ExpectedPublishedBranch;
            if (Fail) throw new InvalidOperationException("b140-controlled-late-publish-failure");
        }
        public void RollbackPublishedRestoreCandidate() { }
        public void CompleteRestoreCandidate() { }
        public void DiscardRestoreCandidate() { }
    }

    private static string LineageJson(DungeonGameSaveData save) =>
        JsonUtility.ToJson(GameWorldLineageSaveSection.ReadSnapshot(save.sections.Single(
            s => s.sectionId == GameWorldLineageSaveSection.Id).payloadJson).Capture());

    private sealed class MemoryTrace : IDisposable
    {
        private readonly StreamWriter writer;
        private int sequence;
        public MemoryTrace()
        {
            Require(DungeonSaveEditorTrace.Observer == null, "Another save diagnostic observer is active.");
            writer = new StreamWriter(new FileStream(EvidencePath("restore-memory-trace.jsonl"),
                FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
            DungeonSaveEditorTrace.Observer = Record;
            Record("case.begin");
        }
        public void Record(string phase)
        {
            // Unity's Mono Process counters returned zero on this Windows Editor.
            // Read the current process directly; never report an unavailable counter as zero usage.
            var process = new ProcessMemoryCounters
                { Size = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
            var system = new SystemMemoryStatus
                { Length = (uint)Marshal.SizeOf<SystemMemoryStatus>() };
            Require(GetProcessMemoryInfo(new IntPtr(-1), ref process, process.Size)
                && GlobalMemoryStatusEx(ref system), "Windows memory counters unavailable.");
            Require(process.WorkingSetSize.ToUInt64() > 0 && process.PrivateUsage.ToUInt64() > 0,
                "Windows process memory counters are empty.");
            writer.WriteLine(new JObject
            {
                ["sequence"] = sequence++, ["phase"] = phase, ["frame"] = Time.frameCount,
                ["managedUsed"] = GC.GetTotalMemory(false),
                ["monoHeap"] = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong(),
                ["unityAllocated"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                ["unityReserved"] = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(),
                ["processPrivate"] = process.PrivateUsage.ToUInt64(),
                ["processWorkingSet"] = process.WorkingSetSize.ToUInt64(),
                ["availablePhysical"] = system.AvailablePhysical,
                ["availableCommit"] = system.AvailablePageFile
            }.ToString(Newtonsoft.Json.Formatting.None));
        }
        public void Dispose()
        {
            DungeonSaveEditorTrace.Observer = null;
            writer.Dispose();
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public uint Size, PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage,
                QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage,
                PagefileUsage, PeakPagefileUsage, PrivateUsage;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct SystemMemoryStatus
        {
            public uint Length, Load;
            public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
                TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
        }
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr process,
            ref ProcessMemoryCounters counters, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref SystemMemoryStatus status);
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        catch (Newtonsoft.Json.JsonException) { return; }
        throw new InvalidOperationException("Malformed lineage unexpectedly accepted.");
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Finish(int code)
    {
        (verification as IDisposable)?.Dispose();
        verification = null;
        SessionState.SetBool(PendingKey, false);
        EditorApplication.update -= Pump;
        EditorApplication.Exit(code);
    }
    private static string EvidencePath(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-logFile");
        Require(index >= 0 && index + 1 < args.Length && Path.IsPathRooted(args[index + 1]), "Isolated log path missing.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        Require(directory.StartsWith(Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Evidence outside project QA root.");
        return Path.Combine(directory, name);
    }
    private static void WriteNew(string name, string text)
    {
        using var stream = new FileStream(EvidencePath(name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        writer.Write(text);
    }
}
#endif
