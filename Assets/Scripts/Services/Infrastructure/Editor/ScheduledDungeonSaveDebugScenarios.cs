#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonStory.Foundation;

public static class ScheduledDungeonSaveDebugScenarios
{
    public static void VerifyForBatch()
    {
        VerifyReadSessionLifetime();
        VerifyDetachedReadContinuation();
        VerifyCaptureStepsAndObserverGuard();
        VerifyValidationSlice();
        VerifyDurableCompletionAndFailure();
        VerifyDeferredCollectionBoundary();
        VerifySceneAdmissionWhileSaving();
        VerifyDetachedValidation();
    }

    private static void VerifyReadSessionLifetime()
    {
        string json = "{\"value\":7}";
        long start = DungeonSaveJson.EditorSourceParseCount;
        using var session = new DungeonSaveJson.ReadSession();
        using (session.Activate())
        {
            DungeonSaveJson.Deserialize<Payload>(json).value = 99;
            ExpectFailure(() => session.Activate());
            ExpectFailure(() => session.Dispose());
        }
        DungeonSaveJson.Deserialize<Payload>(json);
        using (DungeonSaveJson.BeginReadScope())
        {
            DungeonSaveJson.Deserialize<Payload>(json);
            using (session.Activate())
                Require(DungeonSaveJson.Deserialize<Payload>(json).value == 7,
                    "Session leaked a mutable DTO between steps.");
            DungeonSaveJson.Deserialize<Payload>(json);
        }
        Require(DungeonSaveJson.EditorSourceParseCount == start + 3,
            "Inactive session leaked its memo, or activation lost its memo/outer scope.");
        Task.Run(() => ExpectFailure(() => session.Activate())).GetAwaiter().GetResult();
        using (session.Activate())
        {
            long before = DungeonSaveJson.EditorSourceParseCount;
            string malformed = "{\"value\":\"bad\"}";
            ExpectFailure(() => DungeonSaveJson.Deserialize<Payload>(malformed));
            ExpectFailure(() => DungeonSaveJson.Deserialize<Payload>(malformed));
            Require(DungeonSaveJson.EditorSourceParseCount == before + 2,
                "Failed session read was memoized.");
        }
    }

    private static void VerifyDetachedReadContinuation()
    {
        string tokens = "{\"value\":7}";
        string snapshots = "{\"values\":[7]}";
        string scalar = "{\"name\":\"original\"}";
        var source = new DungeonSaveJson.ReadSession();
        Func<DungeonSaveJson.ReadSession> open;
        using (source.Activate())
        {
            DungeonSaveJson.Deserialize<Payload>(tokens).value = 99;
            DungeonSaveJson.Deserialize<SnapshotPayload>(snapshots).values[0] = 99;
            Require(DungeonSaveJson.ReadStrictStringProperty(scalar, "name") == "original",
                "Original scalar fixture changed.");
            open = source.PrepareDetachedContinuation();
        }
        Task.Run(() => ExpectFailure(() => source.PrepareDetachedContinuation())).GetAwaiter().GetResult();
        source.Dispose();
        ExpectFailure(() => source.PrepareDetachedContinuation());
        long parses = DungeonSaveJson.EditorSourceParseCount;
        long scalars = DungeonSaveJson.EditorStrictScalarParseCount;
        Task.Run(() =>
        {
            using var worker = open();
            ExpectFailure(() => open());
            using (worker.Activate())
            {
                Require(DungeonSaveJson.Deserialize<Payload>(tokens).value == 7,
                    "Detached token memo depended on a disposed or mutated source.");
                var first = DungeonSaveJson.Deserialize<SnapshotPayload>(snapshots);
                Require(first.values[0] == 7, "Detached snapshot retained a mutable caller value.");
                first.values[0] = 101;
                Require(DungeonSaveJson.Deserialize<SnapshotPayload>(snapshots).values[0] == 7,
                    "Detached snapshot leaked mutable references between validator reads.");
                Require(DungeonSaveJson.ReadStrictStringProperty(scalar, "name") == "original"
                    && DungeonSaveJson.EditorSourceParseCount == parses
                    && DungeonSaveJson.EditorStrictScalarParseCount == scalars,
                    "Detached continuation reparsed an already validated original source.");
                DungeonSaveJson.Deserialize<Payload>(new string(tokens.ToCharArray()));
                Require(DungeonSaveJson.EditorSourceParseCount == parses + 1,
                    "Detached continuation changed exact source-reference memo identity.");
            }
        }).GetAwaiter().GetResult();
        ExpectFailure(() => open());
    }

    private static void VerifyDetachedValidation()
    {
        int mainThread = Thread.CurrentThread.ManagedThreadId;
        int firstThread = 0, lastThread = 0, mainCalls = 0;
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var registry = new Registry();
        long sourceParses = DungeonSaveJson.EditorSourceParseCount;
        var first = new ADetachedValidator((snapshot, report) =>
        {
            firstThread = Thread.CurrentThread.ManagedThreadId;
            Require(firstThread != mainThread, "Detached validation ran on the game thread.");
            DungeonSaveJson.Deserialize<Payload>(snapshot.sections[0].payloadJson).value = 99;
            entered.Set();
            Require(release.Wait(TimeSpan.FromSeconds(10)), "Detached fixture release timed out.");
        });
        var middle = new BMainValidator((snapshot, report) =>
        {
            mainCalls++;
            Require(Thread.CurrentThread.ManagedThreadId == mainThread,
                "A main-thread dependency validator moved to a worker.");
        });
        var last = new CDetachedValidator((snapshot, report) =>
        {
            lastThread = Thread.CurrentThread.ManagedThreadId;
            Require(DungeonSaveJson.Deserialize<Payload>(snapshot.sections[0].payloadJson).value == 7,
                "Worker validation shared a mutable DTO or read later live state.");
        });
        using (var capture = Service(registry, first, middle, last).BeginCapture())
        {
            try
            {
                registry.Value = 23;
                capture.Advance();
                Require(entered.Wait(TimeSpan.FromSeconds(10)), "Detached validation did not start.");
                capture.Advance();
                Require(!capture.IsCompleted && mainCalls == 0,
                    "A blocked worker was treated as completed or the original validation order changed.");
                Task.Run(() => ExpectFailure(() => capture.Dispose())).GetAwaiter().GetResult();
                ExpectFailure(() => { _ = capture.Result; });
                release.Set();
                DrainCapture(capture);
                Require(firstThread == lastThread && lastThread != mainThread && mainCalls == 1
                    && DungeonSaveJson.EditorSourceParseCount == sourceParses + 1
                    && capture.Result.sections[0].payloadJson == "{\"value\":7}",
                    "Worker read-session lifetime, isolation or snapshot identity changed.");
            }
            finally { release.Set(); }
        }
        var ordered = Service(new Registry(),
            new ADetachedValidator((snapshot, report) => report.AddError("first")),
            new BMainValidator((snapshot, report) => report.AddError("second")),
            new CDetachedValidator((snapshot, report) => report.AddError("third")));
        using (var capture = ordered.BeginCapture())
        {
            string failure = CaptureFailure(capture);
            Require(failure.EndsWith("first | second | third", StringComparison.Ordinal),
                "Detached completion reordered the original validator errors.");
        }
        bool legacySawPriorError = false;
        using (var capture = Service(new Registry(),
            new ADetachedValidator((snapshot, report) =>
            {
                Require(Thread.CurrentThread.ManagedThreadId != mainThread, "An explicitly detached validator changed threads.");
                report.AddError("prior");
            }), new Validator((snapshot, report) =>
            {
                Require(Thread.CurrentThread.ManagedThreadId == mainThread, "An unmarked validator left its original thread.");
                legacySawPriorError = !report.Success;
            })).BeginCapture())
        {
            CaptureFailure(capture);
            Require(legacySawPriorError, "An unmarked extension lost its original report/order contract.");
        }
        Service(new Registry(), new ADetachedValidator((snapshot, report) =>
            Require(Thread.CurrentThread.ManagedThreadId == mainThread,
                "Synchronous Capture moved its validation to another thread."))).Capture();
        using var cancelReady = new ManualResetEventSlim(false);
        bool cancelledMainRan = false;
        using (var capture = Service(new Registry(),
            new ADetachedValidator((snapshot, report) => cancelReady.Set()),
            new BMainValidator((snapshot, report) => cancelledMainRan = true)).BeginCapture())
        {
            capture.Advance();
            Require(cancelReady.Wait(TimeSpan.FromSeconds(10))
                && SpinWait.SpinUntil(() => !DungeonGameSaveService.IsWaitingForCaptureWork(capture),
                    TimeSpan.FromSeconds(10)), "Worker did not reach its main-thread handoff.");
            capture.Dispose();
            var task = (Task)capture.GetType().GetField("detachedValidation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(capture);
            Require(task.IsCompleted && !cancelledMainRan,
                "Capture disposal orphaned a waiting worker or ran cancelled main-thread work.");
            ExpectFailure(() => { _ = capture.Result; });
        }
        VerifyDetachedFailureBeforeWrite();
    }

    private static void VerifyDetachedFailureBeforeWrite()
    {
        foreach (int failureKind in new[] { 0, 1, 2 })
        {
            string root = Path.Combine(Path.GetTempPath(), "DungeonStoryDetachedSave", Guid.NewGuid().ToString("N"));
            using var gate = new ManualResetEventSlim(true);
            var files = new Files(gate);
            var validator = new ADetachedValidator((snapshot, report) =>
            {
                if (failureKind == 0) throw new InvalidOperationException("worker failure");
                snapshot.sections[0].payloadJson += " ";
                if (failureKind == 2) snapshot.manifest = DungeonSaveManifest.Capture(snapshot.sections);
            });
            var catalog = new DungeonSaveSlotCatalog(root);
            Directory.CreateDirectory(root);
            string original = catalog.GetPath("slot");
            File.WriteAllText(original, "original slot bytes");
            var slots = new DungeonGameSaveSlotService(Service(new Registry(), validator), catalog,
                new DungeonDurableSaveCommitCoordinator(new[] { new Participant() }), files, null, new PauseHold());
            try
            {
                IDungeonSaveOperation operation = slots.BeginSave("slot");
                ExpectFailure(() => slots.CompletePendingSave());
                Require(!operation.Succeeded && operation.IsCompleted && files.Writes == 0
                    && File.ReadAllText(original) == "original slot bytes"
                    && operation.Error.Contains(failureKind == 0 ? "worker failure" : "integrity failed"),
                    "Detached failure changed existing bytes, started a write, or hid its original reason.");
            }
            finally
            {
                slots.Dispose();
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }

    private static void DrainCapture(IDungeonGameSaveCapture capture)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!capture.IsCompleted)
        {
            Require(DateTime.UtcNow < deadline, "Detached capture fixture timed out.");
            capture.Advance();
            Thread.Sleep(1);
        }
    }

    private static string CaptureFailure(IDungeonGameSaveCapture capture)
    {
        try { DrainCapture(capture); }
        catch (InvalidOperationException error) { return error.Message; }
        throw new InvalidOperationException("Expected detached capture failure.");
    }

    private static void VerifyCaptureStepsAndObserverGuard()
    {
        var registry = new Registry();
        int calls = 0;
        var validator = new Validator((snapshot, report) =>
        {
            calls++;
            Require(snapshot.sections[0].payloadJson == "{\"value\":7}",
                "Validation read a later live value instead of the sealed capture.");
        });
        var service = Service(registry, validator, validator);
        using (IDungeonGameSaveCapture capture = service.BeginCapture())
        {
            Require(registry.Captures == 1 && calls == 0 && !capture.IsCompleted,
                "BeginCapture did not seal a single snapshot before deferred validation.");
            registry.Value = 23;
            ExpectFailure(() => { _ = capture.Result; });
            capture.Advance();
            Require(calls == 1 && !capture.IsCompleted, "Capture ran more than one validator in a step.");
            capture.Advance();
            Require(calls == 2 && !capture.IsCompleted, "Second validator or final seal boundary was skipped.");
            capture.Advance();
            Require(capture.IsCompleted && capture.Result.sections[0].payloadJson == "{\"value\":7}",
                "Final capture did not preserve its original snapshot.");
        }
        foreach (bool reseal in new[] { false, true })
        {
            var mutator = new Validator((snapshot, report) =>
            {
                snapshot.sections[0].payloadJson += " ";
                if (reseal) snapshot.manifest = DungeonSaveManifest.Capture(snapshot.sections);
            });
            using var capture = Service(registry, mutator).BeginCapture();
            capture.Advance();
            ExpectFailure(() => capture.Advance());
        }
        var failure = new Validator((snapshot, report) => report.AddError("fixture validation failed"));
        ExpectFailure(() => Service(registry, failure).Capture());
    }

    private static void VerifyValidationSlice()
    {
        string root = Path.Combine(Path.GetTempPath(), "DungeonStorySaveSlice", Guid.NewGuid().ToString("N"));
        using var gate = new ManualResetEventSlim(true);
        int calls = 0;
        double elapsed = 0;
        var validators = new List<IDungeonCapturedSavePreflightValidator>();
        // 마지막 검사는 예산을 확실히 소모한다. 이후 검사는 다음 Tick까지 금지한다.
        for (int index = 0; index < 5; index++)
            validators.Add(new Validator((snapshot, report) => { calls++; elapsed += 0.0005; }));
        validators.Add(new Validator((snapshot, report) =>
        {
            calls++;
            elapsed += 0.016;
        }));
        validators.Add(new Validator((snapshot, report) => calls++));
        var registry = new Registry();
        var simulationPause = new PauseHold(() => Require(registry.Captures == 1,
            "Simulation stopped before the original snapshot was captured."));
        var slots = new DungeonGameSaveSlotService(Service(registry, validators.ToArray()),
            new DungeonSaveSlotCatalog(root), new DungeonDurableSaveCommitCoordinator(new[] { new Participant() }),
            new Files(gate), () => elapsed, simulationPause);
        try
        {
            IDungeonSaveOperation operation = slots.BeginSave("slice");
            Require(simulationPause.Active == 1, "An accepted save did not own its simulation hold.");
            registry.Value = 23;
            slots.AdvanceSave();
            Require(calls == 6 && operation.Status == DungeonSaveOperationStatus.Validating,
                "A validation slice did not batch short checks or started a check after exhausting its budget.");
            Require(calls == 6 && !operation.IsCompleted,
                "An indivisible long validator did not yield before the next check.");
            slots.CompletePendingSave();
            Require(calls == 7 && operation.Succeeded && registry.Captures == 1 && simulationPause.Active == 0
                && File.ReadAllText(operation.Path).Contains("\\\"value\\\":7"),
                "Batched validation skipped a check or lost the sealed snapshot.");
        }
        finally
        {
            slots.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void VerifyDurableCompletionAndFailure()
    {
        string root = Path.Combine(Path.GetTempPath(), "DungeonStoryScheduledSave", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var gate = new ManualResetEventSlim(false);
        try
        {
            var registry = new Registry();
            var participant = new Participant();
            var files = new Files(gate);
            var catalog = new DungeonSaveSlotCatalog(root);
            var simulationPause = new PauseHold();
            var slots = new DungeonGameSaveSlotService(Service(registry), catalog,
                new DungeonDurableSaveCommitCoordinator(new[] { participant }), files, simulationPause: simulationPause);
            string path = catalog.GetPath("slot");
            File.WriteAllText(path, "old bytes");
            IDungeonSaveOperation operation = slots.BeginSave("slot");
            registry.Value = 23;
            Require(slots.IsSaving && !operation.IsCompleted && File.ReadAllText(path) == "old bytes",
                "Accepted save was reported as durable before work completed.");
            ExpectFailure(() => slots.BeginSave("other"));
            ExpectFailure(() => slots.Save("other"));
            ExpectFailure(() => slots.Delete("slot"));
            Require(!slots.TryLoad("slot", out var report) && !report.Success && registry.Restores == 0,
                "Load entered the registry while a save was active.");
            Task.Run(() => ExpectFailure(() => slots.AdvanceSave())).GetAwaiter().GetResult();
            slots.AdvanceSave();
            Require(operation.Status == DungeonSaveOperationStatus.Writing && !operation.IsCompleted,
                "Validated snapshot did not wait for its worker.");
            slots.AdvanceSave();
            Require(!operation.IsCompleted && participant.Calls == 0 && File.ReadAllText(path) == "old bytes"
                && simulationPause.Active == 1,
                "Polling a blocked write completed it or collected current runtime receipts.");
            gate.Set();
            slots.CompletePendingSave();
            Require(operation.Succeeded && !slots.IsSaving && participant.Calls == 0 && simulationPause.Active == 0
                && operation.CommitResult.Status == DungeonDurableSaveCommitStatus.Deferred
                && File.ReadAllText(path).Contains("\\\"value\\\":7"),
                "Scheduled completion lost its snapshot or collected later runtime receipts.");
            slots.Save("slot");
            Require(participant.Calls == 1 && File.ReadAllText(path).Contains("\\\"value\\\":23"),
                "Synchronous final save did not retain the original durable collection boundary.");

            string original = File.ReadAllText(path);
            files.FailWrite = true;
            IDungeonSaveOperation failed = slots.BeginSave("slot");
            ExpectFailure(() => slots.CompletePendingSave());
            Require(failed.Status == DungeonSaveOperationStatus.Failed && failed.Error.Contains("fixture write")
                && File.ReadAllText(path) == original && participant.Calls == 1 && !slots.IsSaving
                && simulationPause.Active == 0,
                "Write failure altered the old slot, ran collection, or retained the busy gate.");
            files.FailWrite = false;
            IDungeonSaveOperation drained = slots.BeginSave("slot");
            slots.Dispose();
            Require(drained.Succeeded && File.Exists(path), "Disposal discarded an accepted save.");
            ExpectFailure(() => slots.BeginSave("after-dispose"));

            var rejecting = new Validator((snapshot, errors) => errors.AddError("fixture rejection"));
            var rejectedSlots = new DungeonGameSaveSlotService(Service(registry, rejecting), catalog,
                new DungeonDurableSaveCommitCoordinator(new[] { participant }), files, simulationPause: simulationPause);
            original = File.ReadAllText(path);
            int writes = files.Writes;
            IDungeonSaveOperation rejected = rejectedSlots.BeginSave("slot");
            ExpectFailure(() => rejectedSlots.CompletePendingSave());
            Require(!rejected.Succeeded && files.Writes == writes && File.ReadAllText(path) == original
                && simulationPause.Active == 0,
                "Rejected captured snapshot reached the file writer.");
            rejectedSlots.Dispose();

            var missingRegistry = new Registry();
            using var missingPort = new DungeonGameSaveSlotService(Service(missingRegistry), catalog,
                new DungeonDurableSaveCommitCoordinator(new[] { participant }), files);
            ExpectFailure(() => missingPort.BeginSave("missing-port"));
            Require(missingRegistry.Captures == 0 && !missingPort.IsSaving,
                "Missing simulation authority was hidden by an unpaused fallback.");

            files.FailWrite = true;
            simulationPause.FailReleaseOnce = true;
            var cleanupSlots = new DungeonGameSaveSlotService(Service(registry), catalog,
                new DungeonDurableSaveCommitCoordinator(new[] { participant }), files, simulationPause: simulationPause);
            var cleanupFailure = cleanupSlots.BeginSave("slot");
            ExpectFailure(() => cleanupSlots.CompletePendingSave());
            Require(cleanupFailure.Status == DungeonSaveOperationStatus.Failed && cleanupSlots.IsSaving
                && cleanupFailure.Error.Contains("fixture write") && cleanupFailure.Error.Contains("fixture pause release")
                && File.ReadAllText(path) == original && simulationPause.Active == 1,
                "Cleanup failure hid the write error or dropped its owned hold.");
            ExpectFailure(() => cleanupSlots.BeginSave("blocked"));
            cleanupSlots.Dispose();
            Require(simulationPause.Active == 0, "Disposal did not retry the retained failed release.");
        }
        finally
        {
            gate.Set();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static void VerifyDeferredCollectionBoundary()
    {
        var participant = new Participant();
        var coordinator = new DungeonDurableSaveCommitCoordinator(new[] { participant });
        var deferred = coordinator.OnDurableSaveCommitted("slot", new string('a', 64), false);
        Require(deferred.Status == DungeonDurableSaveCommitStatus.Deferred && participant.Calls == 0,
            "Deferred scheduled collection called a live participant.");
        Require(coordinator.OnDurableSaveCommitted("slot", "bad", false).Status
            == DungeonDurableSaveCommitStatus.Corruption, "Deferred mode accepted an invalid durable digest.");
        Require(coordinator.OnDurableSaveCommitted("slot", new string('a', 64)).Status
            == DungeonDurableSaveCommitStatus.Applied && participant.Calls == 1,
            "Default synchronous commit no longer runs its participant.");
    }

    private static void VerifySceneAdmissionWhileSaving()
    {
        var navigator = new DungeonSceneNavigator(new TimeScale(), new UiClock(), new BusySlots());
        Require(navigator.IsTransitioning && !navigator.LoadTitle() && !navigator.LoadGame("slot")
            && !navigator.StartNewPreparation(DungeonDifficulty.Normal),
            "A scene transition bypassed the active slot writer.");
    }

    private static DungeonGameSaveService Service(Registry registry,
        params IDungeonCapturedSavePreflightValidator[] validators) => new(registry,
        Array.Empty<IDungeonSavePreflightValidator>(), Array.Empty<IDungeonSaveCaptureGuard>(),
        validators, Array.Empty<IDungeonSaveRestoreCompletedHook>(),
        new DungeonRestoreSafetyRuntime(new Pause(), new TimeScale()));

    [Serializable] private sealed class Payload { public int value; }
    [Serializable] private sealed class SnapshotPayload : IDungeonSaveReadSnapshot
    {
        public List<int> values;
        public object CopyForSaveRead() => new SnapshotPayload
            { values = values == null ? null : new List<int>(values) };
    }
    private sealed class PauseHold : IGameTimeScalePauseAuthority
    {
        private readonly Action onHold;
        public int Active;
        public bool FailReleaseOnce;
        public PauseHold(Action onHold = null) => this.onHold = onHold;
        public IDisposable HoldSimulation()
        { onHold?.Invoke(); Active++; return new Lease(this); }
        private sealed class Lease : IDisposable
        {
            private PauseHold owner;
            public Lease(PauseHold owner) => this.owner = owner;
            public void Dispose()
            {
                if (owner == null) return;
                if (owner.FailReleaseOnce) { owner.FailReleaseOnce = false; throw new IOException("fixture pause release"); }
                owner.Active--; owner = null;
            }
        }
    }
    private sealed class Pause : IGameSessionPauseAuthority
    {
        private int holds;
        private bool paused;
        public bool IsPaused => paused || holds > 0;
        public void SetPaused(bool value) => paused = value;
        public IDisposable AcquirePause()
        {
            holds++;
            return new Lease(this);
        }
        private sealed class Lease : IDisposable
        {
            private Pause owner;
            public Lease(Pause owner) { this.owner = owner; }
            public void Dispose()
            {
                if (owner == null) return;
                owner.holds--;
                owner = null;
            }
        }
    }
    private sealed class TimeScale : IGameTimeScaleController { public float Scale { get; set; } = 1; }
    private sealed class UiClock : IUiClock { public float Time => 0; public float DeltaTime => 0; }
    private sealed class BusySlots : IDungeonGameSaveSlotService
    {
        public bool IsSaving => true;
        public IDungeonSaveOperation BeginSave(string slot, bool prettyPrint = false) => throw new NotSupportedException();
        public void AdvanceSave() => throw new NotSupportedException();
        public void CompletePendingSave() => throw new NotSupportedException();
        public string Save(string slot, bool prettyPrint = false) => throw new NotSupportedException();
        public bool TryLoad(string slot, out DungeonGameRestoreReport report) => throw new NotSupportedException();
        public bool HasSave(string slot) => throw new NotSupportedException();
        public IReadOnlyList<DungeonSaveSlotInfo> GetSlots() => throw new NotSupportedException();
        public bool Delete(string slot) => throw new NotSupportedException();
    }
    private sealed class Registry : IDungeonSaveSectionRegistry
    {
        public int Value = 7;
        public int Captures;
        public int Restores;
        public IReadOnlyList<IDungeonSaveSection> OrderedSections => Array.Empty<IDungeonSaveSection>();
        public List<DungeonSaveSectionEnvelope> CaptureAll()
        {
            Captures++;
            return new() { new DungeonSaveSectionEnvelope { sectionId = "fixture", payloadJson = "{\"value\":" + Value + "}" } };
        }
        public bool RestoreAll(IReadOnlyList<DungeonSaveSectionEnvelope> envelopes, DungeonGameRestoreReport report)
        { Restores++; return true; }
        public bool TryGetEnvelope(IReadOnlyList<DungeonSaveSectionEnvelope> envelopes, string sectionId,
            out DungeonSaveSectionEnvelope envelope) { envelope = null; return false; }
    }
    private sealed class Validator : IDungeonCapturedSavePreflightValidator
    {
        private readonly Action<DungeonGameSaveData, DungeonGameRestoreReport> action;
        public Validator(Action<DungeonGameSaveData, DungeonGameRestoreReport> action) => this.action = action;
        public void Validate(DungeonGameSaveData captured, DungeonGameRestoreReport report) => action(captured, report);
    }
    private sealed class ADetachedValidator : IDungeonDetachedCapturedSavePreflightValidator
    {
        private readonly Action<DungeonGameSaveData, DungeonGameRestoreReport> action;
        public ADetachedValidator(Action<DungeonGameSaveData, DungeonGameRestoreReport> action) => this.action = action;
        public void Validate(DungeonGameSaveData captured, DungeonGameRestoreReport report) => action(captured, report);
    }
    private sealed class BMainValidator : IDungeonCapturedSavePreflightValidator
    {
        private readonly Action<DungeonGameSaveData, DungeonGameRestoreReport> action;
        public BMainValidator(Action<DungeonGameSaveData, DungeonGameRestoreReport> action) => this.action = action;
        public void Validate(DungeonGameSaveData captured, DungeonGameRestoreReport report) => action(captured, report);
    }
    private sealed class CDetachedValidator : IDungeonDetachedCapturedSavePreflightValidator
    {
        private readonly Action<DungeonGameSaveData, DungeonGameRestoreReport> action;
        public CDetachedValidator(Action<DungeonGameSaveData, DungeonGameRestoreReport> action) => this.action = action;
        public void Validate(DungeonGameSaveData captured, DungeonGameRestoreReport report) => action(captured, report);
    }
    private sealed class Participant : IDungeonDurableSaveCommitParticipant
    {
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        public int Calls;
        public string ParticipantId => "100.fixture";
        public int Order => 100;
        public DungeonDurableSaveCommitResult OnDurableSaveCommitted(DungeonDurableSaveCommitContext context)
        {
            Require(Thread.CurrentThread.ManagedThreadId == thread, "Durable collection ran on a worker.");
            Calls++;
            return new(DungeonDurableSaveCommitStatus.Applied, ParticipantId, "fixture collected");
        }
    }
    private sealed class Files : IDungeonAtomicSaveFilePort
    {
        private readonly ManualResetEventSlim gate;
        private readonly DungeonAtomicSaveFilePort inner = new();
        public bool FailWrite;
        public int Writes;
        public Files(ManualResetEventSlim gate) => this.gate = gate;
        public void WriteTemporary(string path, byte[] bytes)
        {
            if (!gate.Wait(TimeSpan.FromSeconds(10))) throw new InvalidOperationException("fixture gate timed out");
            Interlocked.Increment(ref Writes);
            if (FailWrite) throw new IOException("fixture write failed");
            inner.WriteTemporary(path, bytes);
        }
        public void CommitTemporary(string temporary, string destination, string backup) => inner.CommitTemporary(temporary, destination, backup);
        public void TryDelete(string path) => inner.TryDelete(path);
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch { return; }
        throw new InvalidOperationException("Expected a rejected save operation.");
    }
}
#endif
