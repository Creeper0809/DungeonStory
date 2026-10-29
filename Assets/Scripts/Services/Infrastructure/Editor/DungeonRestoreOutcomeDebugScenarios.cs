#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

internal static class DungeonRestoreOutcomeDebugScenarios
{
    internal static IEnumerator Verify(IObjectResolver container)
    {
        var rows = new List<string> { "case\tresult" };
        VerifyUnityHostOwnership(rows);
        VerifyControlledBoundaries(rows);
        RecoveryExpectation recovery = VerifyActualContainer(container, rows);
        container = null; // Allow the outgoing gameplay container to retire.
        IEnumerator transition = VerifyActualRecovery(recovery, rows);
        while (transition.MoveNext()) yield return transition.Current;
        string directory = Path.GetDirectoryName(Application.consoleLogPath)
            ?? throw new InvalidOperationException("Unity log directory is unavailable.");
        using (var writer = new StreamWriter(new FileStream(Path.Combine(directory,
                   "restore-outcome.tsv"), FileMode.CreateNew, FileAccess.Write, FileShare.Read)))
            foreach (string row in rows) writer.WriteLine(row);
        Debug.Log("RESTORE_OUTCOME=PASS; controlled failure boundaries and actual world/UI admission");
    }

    private static void VerifyUnityHostOwnership(List<string> rows)
    {
        Type ownershipType = typeof(DungeonStoryLlmHostProcess).Assembly
            .GetType("DungeonStoryLlmHostOwnership", throwOnError: true);
        MethodInfo acquire = ownershipType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(value => value.Name == "TryAcquire" && value.GetParameters().Length == 3);
        string path = Path.Combine(Path.GetDirectoryName(Application.consoleLogPath)
            ?? throw new InvalidOperationException("QA log directory is missing."), "host-ownership-smoke.lock");
        Require(!File.Exists(path), "Create-only Unity host ownership probe already exists.");
        object[] first = { path, null, null };
        Task<bool> worker = Task.Run(() => (bool)acquire.Invoke(null, first));
        IDisposable next = null;
        try
        {
            Require(worker.Wait(TimeSpan.FromSeconds(5)) && worker.Result,
                "Unity worker could not acquire a host lease.");
            object[] competing = { path, null, null };
            Require(!(bool)acquire.Invoke(null, competing) && competing[1] == null,
                "Unity allowed two simultaneous host owners.");
            var constructor = typeof(DungeonStoryLlmHostProcess).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IDisposable) }, null)
                ?? throw new InvalidOperationException("Host ownership constructor is missing.");
            using var host = (DungeonStoryLlmHostProcess)constructor.Invoke(new[] { first[1] });
            host.Dispose(); // Unity thread releases a lease acquired on a worker.
            object[] reacquire = { path, null, null };
            Require((bool)acquire.Invoke(null, reacquire), "Unity retained the previous host lease.");
            next = (IDisposable)reacquire[1];
            host.Dispose();
            Require(!host.IsRunning && !(bool)acquire.Invoke(null, competing),
                "Duplicate host disposal released the next owner's lease.");
            Pass(rows, "actual-unity-host-ownership-worker-release-reuse");
        }
        finally
        {
            next?.Dispose();
            if (worker.IsCompleted)
            {
                (first[1] as IDisposable)?.Dispose();
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                _ = worker.ContinueWith(_ =>
                {
                    (first[1] as IDisposable)?.Dispose();
                    if (File.Exists(path)) File.Delete(path);
                }, TaskScheduler.Default);
            }
        }
    }

    private static void VerifyControlledBoundaries(List<string> rows)
    {
        var normal = new Fixture();
        DungeonGameSaveData valid = normal.Save.Capture();
        normal.SetValue(99);
        Require(normal.Save.TryRestore(valid, out var success)
                && success.Outcome == DungeonWorldRestoreOutcome.Applied
                && success.WorldPublished && !success.RequiresRecovery
                && normal.Value == 10 && normal.Root.PublishedRestoreRevision == 1
                && !normal.Safety.RequiresRecovery,
            "Normal restore did not report its publication.");
        Pass(rows, "controlled-success-applied");

        var rejected = new Fixture();
        var malformed = rejected.Save.FromJson(rejected.Save.ToJson(valid));
        malformed.sections[0].payloadJson = "99"; // Deliberately retain the old seal.
        rejected.SetValue(99);
        Require(!rejected.Save.TryRestore(malformed, out var invalid)
                && invalid.Outcome == DungeonWorldRestoreOutcome.NotApplied
                && !invalid.RequiresRecovery && rejected.Value == 99
                && rejected.Root.PublishedRestoreRevision == 0 && !rejected.Safety.RequiresRecovery,
            "Preflight rejection reported publication or changed the world.");
        Require(rejected.Save.TryRestore(valid, out var retry)
                && retry.Outcome == DungeonWorldRestoreOutcome.Applied && rejected.Value == 10,
            "A rejected request prevented a valid retry.");
        Pass(rows, "controlled-unapplied-rejection-retry");

        var publishFault = new Participant("qa.publish") { FailPublish = true };
        var unpublished = new Fixture(new[] { publishFault });
        unpublished.SetValue(99);
        Require(!unpublished.Save.TryRestore(valid, out var publishFailure)
                && publishFailure.Outcome == DungeonWorldRestoreOutcome.NotApplied
                && unpublished.Value == 99 && unpublished.Root.PublishedRestoreRevision == 0
                && publishFault.Rollbacks == 1 && !unpublished.Safety.RequiresRecovery,
            "Reversible publication failure was reported as an applied world.");
        publishFault.FailPublish = false;
        Require(unpublished.Save.TryRestore(valid, out var publishRetry)
                && publishRetry.Outcome == DungeonWorldRestoreOutcome.Applied && unpublished.Value == 10,
            "Rolled-back publication prevented the same request's retry.");
        Pass(rows, "controlled-publish-rollback-same-request-retry");

        var rollbackFault = new Participant("qa.rollback") { FailPublish = true, FailRollback = true };
        var uncertain = new Fixture(new[] { rollbackFault });
        uncertain.SetValue(99);
        Require(!uncertain.Save.TryRestore(valid, out var rollbackFailure)
                && rollbackFailure.Outcome == DungeonWorldRestoreOutcome.RollbackRecoveryRequired
                && !rollbackFailure.WorldPublished && rollbackFailure.RequiresRecovery
                && uncertain.Safety.RequiresRecovery && uncertain.Pause.Paused && uncertain.Time.Scale == 0,
            "Failed rollback was reported as a clean unapplied rejection.");
        AssertBlocked(uncertain.Save, uncertain.Safety, valid, () => uncertain.Value == 99);
        Pass(rows, "controlled-rollback-failure-requires-recovery");

        var legacyRoot = new DungeonRuntimeAggregateRootStore();
        var legacySection = new LegacyFailingSection();
        var legacySafety = new DungeonRestoreSafetyRuntime(new PauseAuthority(), new TimeScale());
        var legacySave = new DungeonGameSaveService(new DungeonSaveSectionRegistry(
                new IDungeonSaveSection[] { legacySection }, legacyRoot),
            Array.Empty<IDungeonSavePreflightValidator>(), Array.Empty<IDungeonSaveCaptureGuard>(),
            Array.Empty<IDungeonCapturedSavePreflightValidator>(),
            Array.Empty<IDungeonSaveRestoreCompletedHook>(), legacySafety);
        DungeonGameSaveData legacyImage = legacySave.Capture();
        legacySection.Value = 99;
        Require(!legacySave.TryRestore(legacyImage, out var replayFailure)
                && replayFailure.Outcome == DungeonWorldRestoreOutcome.RollbackRecoveryRequired
                && !replayFailure.WorldPublished && legacySafety.RequiresRecovery
                && legacySection.Commits == 2,
            "Failed legacy rollback-image replay did not block the uncertain world.");
        AssertBlocked(legacySave, legacySafety, legacyImage, () => legacySection.Commits == 2);
        Pass(rows, "controlled-legacy-rollback-image-failure-block");

        var remaining = new Participant("qa.a-remaining");
        var completeFault = new Participant("qa.z-fault") { FailComplete = true };
        var incomplete = new Fixture(new[] { remaining, completeFault });
        incomplete.SetValue(99);
        Require(!incomplete.Save.TryRestore(valid, out var completionFailure)
                && completionFailure.Outcome == DungeonWorldRestoreOutcome.AppliedRecoveryRequired
                && incomplete.Value == 10 && incomplete.Root.PublishedRestoreRevision == 1
                && completeFault.Completions == 1 && remaining.Completions == 1
                && incomplete.Safety.RequiresRecovery && incomplete.Pause.Paused
                && incomplete.Time.Scale == 0,
            "Completion failure lost the applied result, remaining cleanup, or progression block.");
        AssertBlocked(incomplete.Save, incomplete.Safety, valid, () => incomplete.Value == 10);
        Pass(rows, "controlled-completion-failure-all-cleanup-and-block");

        var afterFault = new CountingHook();
        var hooked = new Fixture(hooks: new IDungeonSaveRestoreCompletedHook[]
            { new ThrowingHook(), afterFault });
        Require(!hooked.Save.TryRestore(valid, out var hookFailure)
                && hookFailure.Outcome == DungeonWorldRestoreOutcome.AppliedRecoveryRequired
                && hooked.Value == 10 && afterFault.Calls == 1 && hooked.Safety.RequiresRecovery,
            "Hook failure hid publication or skipped remaining hooks.");
        AssertBlocked(hooked.Save, hooked.Safety, valid, () => hooked.Value == 10);
        Pass(rows, "controlled-hook-failure-remaining-hook-and-block");

        var fresh = new Fixture();
        fresh.SetValue(99);
        Require(fresh.Save.TryRestore(valid, out var recovered)
                && recovered.Outcome == DungeonWorldRestoreOutcome.Applied && fresh.Value == 10
                && !fresh.Safety.RequiresRecovery,
            "A fresh container could not restore the original unchanged save.");
        Pass(rows, "controlled-fresh-container-recovery");
    }

    private static RecoveryExpectation VerifyActualContainer(IObjectResolver container, List<string> rows)
    {
        IInitializable[] initializables = container.Resolve<IEnumerable<IInitializable>>().ToArray();
        Require(initializables.OfType<CharacterStateNarrativeObserver>().Count() == 1
                && initializables.OfType<GameplayOutcomeConsolidationRuntime>().Count() == 1,
            "Gameplay did not own exactly one character observer and outcome pump.");
        var resolution = container.Resolve<V20ContentResolutionService>();
        object observedLifeEvents = typeof(V20ContentResolutionService)
            .GetField("observedLifeEvents", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(resolution)
            ?? throw new InvalidOperationException("Observed life-event owner is missing.");
        var generationQuery = observedLifeEvents.GetType().GetMethod("ResolveGeneration",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Observed generation query is missing.");
        int generation = (int)generationQuery.Invoke(observedLifeEvents, Array.Empty<object>());
        float milestoneGeneration = container.Resolve<IV20MilestoneWorldSnapshotQuery>()
            .Build(container.Resolve<IGameCalendar>().Day)
            .WorldMetrics[V20WorldMetricKind.CompletedGenerations];
        Require(generation == Mathf.Max(0, Mathf.FloorToInt(milestoneGeneration)),
            "Observed life events and milestones disagree on the living population generation.");
        Pass(rows, "actual-observed-generation-matches-milestone-query");
        var safety = container.Resolve<DungeonRestoreSafetyRuntime>();
        Require(ReferenceEquals(safety, container.Resolve<IDungeonRestoreSafetyQuery>())
                && !safety.RequiresRecovery, "Actual restore safety has multiple owners or stale state.");
        var owners = container.Resolve<IOwnerRunManagerProvider>();
        Require(owners.TryGetManager(out var manager), "Actual owner manager is missing.");
        CharacterActor oldOwner = manager.CurrentOwnerActor;
        Require(oldOwner != null, "Actual starting owner is missing.");
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Actual recovery save requires isolated batch persistence.");
        const string recoverySlot = "qa-restore-outcome-recovery";
        var catalog = container.Resolve<IDungeonSaveSlotCatalog>();
        Require(!catalog.HasSave(recoverySlot), "Create-only recovery slot already exists.");
        string recoveryPath = container.Resolve<IDungeonGameSaveSlotService>().Save(recoverySlot);
        var expected = new RecoveryExpectation(safety, recoverySlot, recoveryPath,
            oldOwner.Identity.PersistentId,
            JsonUtility.ToJson(container.Resolve<IGameWorldLineageQuery>().Snapshot.Capture()));
        var timeScale = container.Resolve<IGameTimeScaleController>();
        var overlayObject = new GameObject("RestoreOutcomeControlledResultOverlay");
        var nextRun = new GameObject("NextRunButton", typeof(RectTransform), typeof(Button));
        nextRun.transform.SetParent(overlayObject.transform, false);
        var nextRunButton = nextRun.GetComponent<Button>();
        var continuation = new CountingRunTransition();
        var overlay = overlayObject.AddComponent<RunResultPanel>();
        overlay.Construct(continuation, timeScale,
            container.Resolve<IRunResultTextQuery>(), container.Resolve<IRunResultThemeQuery>(), safety);
        timeScale.Scale = 1f;
        overlay.Render(null); // Capture a nonzero previous scale before the fault.
        var defenseEvents = new GameEventBus();
        var settings = new EditorDungeonUserSettingsService();
        settings.Update(value => value.defenseTimeResponse = DungeonDefenseTimeResponse.PauseOnCritical);
        using var defense = new DungeonDefenseTimeResponseRuntime(defenseEvents, timeScale, settings, safety);
        defense.Initialize();
        timeScale.Scale = 3f;
        defenseEvents.Publish(new InvasionDungeonBreachedEvent(null, null, default));
        Require(timeScale.Scale == 0, "Controlled defense pause was not armed.");
        var remaining = new CountingHook();
        // Inject a failing hook around the production service and the real registry,
        // state owners and preflight. This is an explicit controlled fault, not gameplay AI.
        var save = new DungeonGameSaveService(
            container.Resolve<IDungeonSaveSectionRegistry>(),
            container.Resolve<IEnumerable<IDungeonSavePreflightValidator>>(),
            container.Resolve<IEnumerable<IDungeonSaveCaptureGuard>>(),
            container.Resolve<IEnumerable<IDungeonCapturedSavePreflightValidator>>(),
            container.Resolve<IEnumerable<IDungeonSaveRestoreCompletedHook>>()
                .Concat(new IDungeonSaveRestoreCompletedHook[] { new ThrowingHook(), remaining }),
            safety);
        DungeonGameSaveData baseline = save.Capture();
        Require(!save.TryRestore(baseline, out var report)
                && report.Outcome == DungeonWorldRestoreOutcome.AppliedRecoveryRequired
                && report.WorldPublished && report.RequiresRecovery && remaining.Calls == 1
                && report.Errors.Count == 1 && report.Errors[0].Contains(ThrowingHook.Fault)
                && manager.CurrentOwnerActor != null && !ReferenceEquals(manager.CurrentOwnerActor, oldOwner),
            "Actual world was not applied before the controlled post-restore failure: "
            + string.Join(" | ", report.Errors));
        CharacterActor appliedOwner = manager.CurrentOwnerActor;
        var speed = container.Resolve<IGameSpeedController>();
        Require(speed.IsPaused && container.Resolve<IGameTimeScaleController>().Scale == 0
                && Time.timeScale == 0 && safety.RequiresRecovery,
            "Actual published failure did not pause game progression.");
        Reject(() => speed.SetPaused(false));
        Reject(() => speed.SetSpeed(2));
        overlay.Hide();
        nextRunButton.onClick.Invoke();
        defenseEvents.Publish(new InvasionResolvedEvent(true, 0f));
        Require(timeScale.Scale == 0 && speed.IsPaused && continuation.Calls == 0
                && !nextRunButton.interactable,
            "Result overlay or defense completion resumed/continued a recovery-blocked world.");
        UnityEngine.Object.Destroy(overlayObject);
        AssertBlocked(container.Resolve<IDungeonGameSaveService>(), safety, baseline,
            () => ReferenceEquals(manager.CurrentOwnerActor, appliedOwner));
        Pass(rows, "actual-world-applied-hook-failure-and-single-safety-owner");
        Pass(rows, "actual-pause-speed-save-and-rerestore-blocked");
        Pass(rows, "actual-result-overlay-and-defense-cannot-resume");

        Button menu = Find<Button>("SaveMenuButton");
        menu.onClick.Invoke();
        GameObject modal = Find<Transform>("SaveModal").gameObject;
        Require(modal.activeInHierarchy
                && modal.GetComponentsInChildren<TMP_Text>(true)
                    .Single(text => text.name == "StatusText").text == safety.RecoveryMessage,
            "Actual save UI did not explain the applied-but-incomplete world.");
        Button close = modal.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == "CloseButton");
        Require(!close.interactable, "Recovery UI enabled closing the blocking modal.");
        close.onClick.Invoke(); // The command must also reject a direct UnityEvent invocation.
        Require(modal.activeInHierarchy && speed.IsPaused,
            "Recovery UI close resumed or hid the unsafe world.");
        Button[] saveAndLoad = modal.GetComponentsInChildren<Button>(true)
            .Where(button => button.name.StartsWith("SaveButton_", StringComparison.Ordinal)
                || button.name.StartsWith("LoadButton_", StringComparison.Ordinal)
                || button.name.StartsWith("DeleteButton_", StringComparison.Ordinal)).ToArray();
        Require(saveAndLoad.Length > 0 && saveAndLoad.All(button => !button.interactable),
            "Recovery UI left save, load or delete commands enabled.");
        Require(modal.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ReturnToTitleButton").interactable,
            "Recovery UI did not offer the fresh-scene recovery route.");
        Pass(rows, "actual-ui-applied-message-modal-and-disabled-commands");
        return expected;
    }

    private static IEnumerator VerifyActualRecovery(RecoveryExpectation expected, List<string> rows)
    {
        Transform modal = Find<Transform>("SaveModal");
        Button title = modal.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == "ReturnToTitleButton");
        title.onClick.Invoke();
        Require(modal.gameObject.activeInHierarchy && Time.timeScale == 0,
            "First recovery confirmation resumed the outgoing world.");
        title.onClick.Invoke();
        while (SceneManager.GetActiveScene().name != DungeonSceneNavigator.TitleSceneName)
        {
            Require(Time.timeScale == 0, "Recovery title transition resumed the outgoing world.");
            yield return null;
        }
        DungeonTitleLifetimeScope titleScope;
        while (true)
        {
            titleScope = UnityEngine.Object.FindObjectsByType<DungeonTitleLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).SingleOrDefault();
            if (titleScope?.Container != null
                && !titleScope.Container.Resolve<IDungeonSceneNavigator>().IsTransitioning
                && UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Any(value => value.name == "TitleMainScreen"
                        && value.gameObject.scene == titleScope.gameObject.scene)) break;
            yield return null;
        }
        Require(Time.timeScale == 1 && !titleScope.Container.Resolve<IGameSpeedController>().IsPaused,
            "Fresh title container did not initialize its own time scale.");
        IInitializable[] titleInitializables = titleScope.Container
            .Resolve<IEnumerable<IInitializable>>().ToArray();
        Require(!titleInitializables.OfType<CharacterStateNarrativeObserver>().Any()
                && !titleInitializables.OfType<GameplayOutcomeConsolidationRuntime>().Any(),
            "Title initialized gameplay-only observers or the outcome pump.");
        expected.RequireUnchangedFiles();
        Pass(rows, "actual-title-recovery-no-autosave-and-title-clock");
        Require(titleScope.Container.Resolve<IDungeonSceneNavigator>().LoadGame(expected.SlotId),
            "Title navigator rejected the original recovery save.");
        titleScope = null;
        IObjectResolver fresh;
        OwnerRunManager manager;
        while (true)
        {
            var scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SingleOrDefault(value => value.gameObject.scene.name == DungeonSceneNavigator.GameplaySceneName);
            fresh = scope?.Container;
            if (fresh != null && fresh.Resolve<IOwnerRunManagerProvider>().TryGetManager(out manager)
                && manager.CurrentOwnerActor != null
                && !fresh.Resolve<IDungeonSceneNavigator>().IsTransitioning) break;
            yield return null;
        }
        var safety = fresh.Resolve<IDungeonRestoreSafetyQuery>();
        Require(!ReferenceEquals(safety, expected.PreviousSafety) && !safety.RequiresRecovery
                && expected.PreviousSafety.RequiresRecovery,
            "Fresh gameplay reused or cleared the failed container's safety owner.");
        Require(manager.CurrentOwnerActor.Identity.PersistentId == expected.OwnerId
                && JsonUtility.ToJson(fresh.Resolve<IGameWorldLineageQuery>().Snapshot.Capture()) == expected.Lineage,
            "Fresh gameplay did not restore the original owner and world lineage.");
        DungeonGameSaveData recovered = fresh.Resolve<IDungeonGameSaveService>().Capture();
        Require(recovered.sections.Count > 0, "Fresh restored world still blocked valid capture.");
        expected.RequireUnchangedFiles();
        Pass(rows, "actual-fresh-gameplay-same-save-load-and-capture");
    }

    private sealed class RecoveryExpectation
    {
        internal readonly IDungeonRestoreSafetyQuery PreviousSafety;
        internal readonly string SlotId, OwnerId, Lineage;
        private readonly string directory;
        private readonly Dictionary<string, string> files;
        internal RecoveryExpectation(IDungeonRestoreSafetyQuery safety, string slotId,
            string path, string ownerId, string lineage)
        {
            PreviousSafety = safety; SlotId = slotId; OwnerId = ownerId; Lineage = lineage;
            directory = Path.GetDirectoryName(path);
            files = SnapshotFiles();
        }
        internal void RequireUnchangedFiles()
        {
            var current = SnapshotFiles();
            Require(current.Count == files.Count && files.All(pair =>
                    current.TryGetValue(pair.Key, out string digest) && digest == pair.Value),
                "Recovery transition overwrote, deleted or autosaved a recovery file.");
        }
        private Dictionary<string, string> SnapshotFiles() => Directory.GetFiles(directory, "*.json")
            .ToDictionary(path => Path.GetFileName(path), path =>
            {
                using SHA256 sha = SHA256.Create();
                return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
            }, StringComparer.Ordinal);
    }

    private static void AssertBlocked(IDungeonGameSaveService save, IDungeonRestoreSafetyQuery safety,
        DungeonGameSaveData source, Func<bool> unchanged)
    {
        Reject(() => save.Capture());
        Require(!save.TryRestore(source, out var blocked) && !blocked.Success
                && blocked.Outcome == DungeonWorldRestoreOutcome.NotApplied
                && blocked.Errors.Contains(safety.RecoveryMessage) && safety.RequiresRecovery && unchanged(),
            "The unsafe container admitted a save/re-restore or forgot its recovery requirement.");
    }

    private static T Find<T>(string name) where T : Component => UnityEngine.Object
        .FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Single(component => component.name == name);

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Unsafe command was admitted.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Pass(List<string> rows, string name) => rows.Add(name + "\tPASS");

    private sealed class Fixture
    {
        internal readonly DungeonRuntimeAggregateRootStore Root = new();
        internal readonly PauseAuthority Pause = new();
        internal readonly TimeScale Time = new();
        internal readonly DungeonRestoreSafetyRuntime Safety;
        internal readonly DungeonGameSaveService Save;
        internal int Value => Root.GetOrCreate(() => new CounterState(10)).Value;
        internal void SetValue(int value) => Root.Replace(new CounterState(value));
        internal Fixture(IDungeonRestoreTransactionParticipant[] participants = null,
            IDungeonSaveRestoreCompletedHook[] hooks = null)
        {
            Safety = new DungeonRestoreSafetyRuntime(Pause, Time);
            Save = new DungeonGameSaveService(new DungeonSaveSectionRegistry(
                    new IDungeonSaveSection[] { new CounterSection(Root) }, Root,
                    participants ?? Array.Empty<IDungeonRestoreTransactionParticipant>()),
                Array.Empty<IDungeonSavePreflightValidator>(), Array.Empty<IDungeonSaveCaptureGuard>(),
                Array.Empty<IDungeonCapturedSavePreflightValidator>(),
                hooks ?? Array.Empty<IDungeonSaveRestoreCompletedHook>(), Safety);
        }
    }

    private sealed class CounterState
    {
        internal readonly int Value;
        internal CounterState(int value) { Value = value; }
    }

    private sealed class CounterSection : IDungeonSaveSection, IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection, IDungeonRollbackFreeSaveSection
    {
        private readonly DungeonRuntimeAggregateRootStore root;
        internal CounterSection(DungeonRuntimeAggregateRootStore root) { this.root = root; }
        public string SectionId => "qa.restore-outcome";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => root.GetOrCreate(() => new CounterState(10)).Value
            .ToString(CultureInfo.InvariantCulture);
        public void ValidatePayload(string json, int version, DungeonGameRestoreReport report)
        {
            if (version != 1 || !int.TryParse(json, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                report.AddError("Invalid controlled counter payload.");
        }
        public IDungeonSaveRestoreStage StageRestore(string json, int version, DungeonGameRestoreReport report)
        {
            ValidatePayload(json, version, report);
            if (!report.Success) return null;
            var candidate = new CounterState(int.Parse(json, CultureInfo.InvariantCulture));
            return new DungeonDelegateSaveRestoreStage(SectionId, _ => root.Replace(candidate));
        }
        public void Restore(string json, int version, DungeonGameRestoreReport report) =>
            StageRestore(json, version, report)?.Commit(report);
    }

    private sealed class Participant : IDungeonRestoreTransactionParticipant
    {
        internal Participant(string id) { ParticipantId = id; }
        public string ParticipantId { get; }
        internal bool FailPublish, FailComplete, FailRollback;
        internal int Rollbacks, Completions;
        public void BeginRestoreCandidate() { }
        public void PublishRestoreCandidate()
        {
            if (FailPublish) throw new InvalidOperationException("controlled-publish-failure");
        }
        public void RollbackPublishedRestoreCandidate()
        {
            Rollbacks++;
            if (FailRollback) throw new InvalidOperationException("controlled-rollback-failure");
        }
        public void CompleteRestoreCandidate()
        {
            Completions++;
            if (FailComplete) throw new InvalidOperationException("controlled-completion-failure");
        }
        public void DiscardRestoreCandidate() { }
    }

    // Explicit legacy external state demonstrates why a failed rollback-image
    // replay cannot be treated as a harmless detached-candidate rejection.
    private sealed class LegacyFailingSection : IDungeonSaveSection, IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection
    {
        internal int Value = 10, Commits;
        public string SectionId => "qa.legacy-restore-outcome";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => Value.ToString(CultureInfo.InvariantCulture);
        public void ValidatePayload(string json, int version, DungeonGameRestoreReport report)
        {
            if (version != 1 || !int.TryParse(json, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                report.AddError("Invalid controlled legacy payload.");
        }
        public IDungeonSaveRestoreStage StageRestore(string json, int version, DungeonGameRestoreReport report)
        {
            int candidate = int.Parse(json, CultureInfo.InvariantCulture);
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                Value = candidate;
                Commits++;
                throw new InvalidOperationException("controlled-legacy-commit-failure");
            });
        }
        public void Restore(string json, int version, DungeonGameRestoreReport report) =>
            StageRestore(json, version, report).Commit(report);
    }

    private sealed class ThrowingHook : IDungeonSaveRestoreCompletedHook
    {
        internal const string Fault = "controlled-post-restore-hook-failure";
        public void OnRestoreCompleted() => throw new InvalidOperationException(Fault);
    }

    private sealed class CountingHook : IDungeonSaveRestoreCompletedHook
    {
        internal int Calls;
        public void OnRestoreCompleted() { Calls++; }
    }

    private sealed class CountingRunTransition : IDungeonRunTransitionService
    {
        internal int Calls;
        public bool IsTransitioning => false;
        public void StartNextRun() { Calls++; }
    }

    private sealed class PauseAuthority : IGameSessionPauseAuthority
    {
        internal bool Paused;
        public bool IsPaused => Paused;
        public void SetPaused(bool paused) { Paused = paused; }
        public IDisposable AcquirePause() =>
            throw new InvalidOperationException("This restore-failure fixture does not open UI pause scopes.");
    }

    private sealed class TimeScale : IGameTimeScaleController
    {
        public float Scale { get; set; } = 1f;
    }
}
#endif
