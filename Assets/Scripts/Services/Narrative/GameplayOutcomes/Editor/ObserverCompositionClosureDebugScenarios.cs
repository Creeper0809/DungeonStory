#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

// Fast compiled capability check, then a separate actual gameplay-container snapshot.
// This never drives user desktop input or writes a scene/prefab.
public static class ObserverCompositionClosureDebugScenarios
{
    private const string PendingKey = "DungeonStory.ObserverComposition.Pending";
    private const string StartedKey = "DungeonStory.ObserverComposition.Started";

    public static void RunForBatch()
    {
        Require(Application.isBatchMode, "Isolated batch execution required.");
        var builder = new ContainerBuilder();
        builder.Register(_ => new KoreanJosaResolutionCache(KoreanJosaResolutionCache.DefaultCapacity),
            Lifetime.Singleton).AsSelf();
        builder.Register<KoreanJosaFormatter>(Lifetime.Singleton).As<IKoreanJosaFormatter>();
        builder.Register<MigratedProducerOutcomeAdapter>(Lifetime.Singleton).AsSelf();
        foreach (string group in GameplayActionLogCatalog.Definitions.Select(value => value.Group).Distinct())
            builder.RegisterGameplayActionLogs(group);
        using var container = builder.Build();
        var modules = container.Resolve<IEnumerable<IGameplayActionLogModule>>().ToArray();
        GameplayOutcomeRegistry registry = GameplayOutcomeRegistry.FromModules(modules);
        Require(modules.Length == 149 && registry.RegisteredOutcomeTypes.Count == 149
            && modules.Select(value => value.OutcomeTypeId).Distinct().Count() == 149,
            "Compiled action module identities are missing or duplicated.");
        Require(new HashSet<string>(registry.RegisteredOutcomeTypes.Select(value => value.Value))
            .SetEquals(GameplayActionLogCatalog.Definitions.SelectMany(value => value.ExperienceObserver.OutcomeTypes)),
            "Compiled catalog/descriptor/experience identities differ.");
        Debug.Log(GameplayActionLogModuleSmokeScenarios.Run(registry));
        VerifyBindingLifecycle();
        Debug.Log("B131_COMPILED_MODULES_149=PASS");
    }

    private static void VerifyBindingLifecycle()
    {
        var observer = new CountingObserver();
        var binding = new CountingBinding(observer, typeof(int));
        var dispatcher = new DomainFactDispatcher(new[] { binding });
        Require(dispatcher.SubscribedFactTypes.SequenceEqual(new[] { typeof(int) }),
            "A declared binding was not connected exactly.");
        Require(dispatcher.TryReserve(1, out var lease, out _), "Binding reservation failed.");
        dispatcher.Dispose(); dispatcher.Dispose();
        Require(binding.DisposeCount == 1 && observer.CancelCount == 1
            && dispatcher.ActiveLeaseCount == 0 && dispatcher.SubscribedFactTypes.Count == 0,
            "Dispatcher disposal leaked a binding or pending reservation.");
        RequireThrows(() => dispatcher.TryReserve(1, out _, out _));
        RequireThrows(() => new DomainFactDispatcher(new[]
        {
            new CountingBinding(new CountingObserver(), typeof(string))
        }));
        RequireThrows(() => new DomainFactDispatcher(new[]
        {
            new CountingBinding(new CountingObserver(), typeof(int)),
            new CountingBinding(new CountingObserver(), typeof(int))
        }));
        var lateObserver = new CountingObserver();
        int created = 0;
        var late = new DeferredDomainFactObserverBinding<int>(value =>
        {
            created++;
            return value.Subscribe("b131-late", lateObserver);
        });
        using var deferred = new DomainFactDispatcher(new[] { late });
        Require(created == 0 && deferred.SubscribedFactTypes.Count == 0,
            "Deferred reaction owner was constructed inside the dispatcher constructor.");
        deferred.ConnectDeferredBindings(); deferred.ConnectDeferredBindings();
        Require(created == 1 && deferred.SubscribedFactTypes.SequenceEqual(new[] { typeof(int) }),
            "Deferred observer connection was omitted or duplicated.");
        deferred.Dispose();
        Require(deferred.SubscribedFactTypes.Count == 0, "Deferred connection was not disposed.");
        Debug.Log("B131_BINDING_DECLARATION_LIFETIME=PASS");
    }

    public static void RequestActualContainerForBatch()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Actual container snapshot requires isolated batch execution.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "PlayMode is already active.");
        string output = EvidencePath("observer-composition-current.json");
        Require(!File.Exists(output), "Composition report must be create-only.");
        SessionState.SetBool(PendingKey, true);
        SessionState.SetString(StartedKey, EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayScene.unity", OpenSceneMode.Single);
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (SessionState.GetBool(PendingKey, false))
        {
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
        }
    }

    private static void Pump()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        try
        {
            double started = double.Parse(SessionState.GetString(StartedKey, ""), CultureInfo.InvariantCulture);
            if (EditorApplication.timeSinceStartup - started > 60)
                throw new InvalidOperationException("Actual gameplay container did not become ready within 60 seconds.");
            if (!EditorApplication.isPlaying) return;
            var scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value?.Container != null);
            if (scope == null) return;
            GameManager game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Require(game != null, "Actual gameplay manager is missing.");
            game.isPause = true;
            scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
            Time.timeScale = 0;

            IGameplayOutcomeRegistry registry = scope.Container.Resolve<IGameplayOutcomeRegistry>();
            var modules = scope.Container.Resolve<IEnumerable<IGameplayActionLogModule>>().ToArray();
            var bindings = scope.Container.Resolve<IEnumerable<IDomainFactObserverBinding>>().ToArray();
            var declared = bindings.SelectMany(value => value.FactTypes).ToArray();
            var dispatcher = scope.Container.Resolve<DomainFactDispatcher>();
            Require(ReferenceEquals(scope.Container.Resolve<IDomainFactPublisher>(), dispatcher)
                && ReferenceEquals(scope.Container.Resolve<IDomainFactRecoveryPublisher>(), dispatcher)
                && scope.Container.Resolve<ISocialRitualFactPublisher>() is SocialRitualFactJournal,
                "A specialized journal replaced the shared domain dispatcher contracts.");
            Require(modules.Length == 149 && registry.RegisteredOutcomeTypes.Count == 149,
                "Actual gameplay container does not contain all149 modules.");
            var inventory = new CompositionInventory
            {
                modules = modules.Length, bindings = bindings.Length, declarations = declared.Length,
                connected = dispatcher.SubscribedFactTypes.Count,
                declaredTypes = declared.Select(value => value.FullName).OrderBy(value => value).ToArray(),
                connectedTypes = dispatcher.SubscribedFactTypes.Select(value => value.FullName).OrderBy(value => value).ToArray(),
                duplicateDeclarations = declared.GroupBy(value => value).Where(value => value.Count() > 1)
                    .Select(value => value.Key.FullName).OrderBy(value => value).ToArray(),
                missing = declared.Except(dispatcher.SubscribedFactTypes).Select(value => value.FullName).ToArray(),
                unexpected = dispatcher.SubscribedFactTypes.Except(declared).Select(value => value.FullName).ToArray()
            };
            WriteNew(EvidencePath("observer-composition-inventory.json"), JsonUtility.ToJson(inventory, true));
            Require(bindings.Length > 0 && declared.Length > 0 && declared.Distinct().Count() == declared.Length
                && new HashSet<Type>(declared).SetEquals(dispatcher.SubscribedFactTypes),
                "Actual observer declarations and connected fact types differ.");
            string moduleReport = GameplayActionLogModuleSmokeScenarios.Run(registry);
            var report = new CompositionReport
            {
                status = "PASS", moduleCount = modules.Length, bindingCount = bindings.Length,
                factTypeCount = declared.Length,
                outcomeTypes = registry.RegisteredOutcomeTypes.Select(value => value.Value).OrderBy(value => value).ToArray(),
                factTypes = dispatcher.SubscribedFactTypes.Select(value => value.FullName).OrderBy(value => value).ToArray(),
                moduleContracts = moduleReport
            };
            WriteNew(EvidencePath("observer-composition-current.json"), JsonUtility.ToJson(report, true));
            Debug.Log("B131_ACTUAL_CONTAINER_OBSERVER_EXACT_SET=PASS; modules=" + modules.Length
                + "; bindings=" + bindings.Length + "; facts=" + declared.Length);
            Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try { WriteNew(EvidencePath("observer-composition-failure.txt"), exception.ToString()); }
            catch (Exception reportFailure) { Debug.LogException(reportFailure); }
            finally { Finish(1); }
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(PendingKey, false);
        EditorApplication.update -= Pump;
        EditorApplication.Exit(code);
    }

    private static string EvidencePath(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-logFile");
        Require(index >= 0 && index + 1 < args.Length && Path.IsPathRooted(args[index + 1]),
            "Explicit isolated log path is missing.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        string qaRoot = Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar;
        Require(directory.StartsWith(qaRoot, StringComparison.OrdinalIgnoreCase),
            "Evidence directory is outside this project's QA tree.");
        return Path.Combine(directory, name);
    }

    private static void WriteNew(string path, string value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        writer.Write(value);
    }

    [Serializable]
    private sealed class CompositionInventory
    {
        public int modules, bindings, declarations, connected;
        public string[] declaredTypes, connectedTypes, duplicateDeclarations, missing, unexpected;
    }

    [Serializable]
    private sealed class CompositionReport
    {
        public string status;
        public int moduleCount, bindingCount, factTypeCount;
        public string[] outcomeTypes, factTypes;
        public string moduleContracts;
    }

    private sealed class CountingBinding : IDomainFactObserverBinding
    {
        private readonly CountingObserver observer;
        public CountingBinding(CountingObserver observer, Type declared)
        { this.observer = observer; FactTypes = Array.AsReadOnly(new[] { declared }); }
        public IReadOnlyCollection<Type> FactTypes { get; }
        public int DisposeCount { get; private set; }
        public IDisposable Connect(DomainFactDispatcher dispatcher) =>
            new Connection(dispatcher.Subscribe("b131-binding", observer), () => DisposeCount++);
        private sealed class Connection : IDisposable
        {
            private IDisposable subscription;
            private readonly Action disposed;
            public Connection(IDisposable subscription, Action disposed)
            { this.subscription = subscription; this.disposed = disposed; }
            public void Dispose()
            {
                if (subscription == null) return;
                subscription.Dispose(); subscription = null; disposed();
            }
        }
    }

    private sealed class CountingObserver : IDurableDomainFactObserver<int>
    {
        public int CancelCount { get; private set; }
        public bool TryReserve(in int fact, out object reservation, out string failure)
        { reservation = new object(); failure = ""; return true; }
        public bool TryPrepare(in int fact, object reservation, out IDomainFactPreparedPublication prepared, out string failure)
        { prepared = null; failure = "unused-commit"; return false; }
        public DomainFactDelivery Replay(in int fact) => DomainFactDelivery.Reject("unused-replay");
        public void Cancel(object reservation) => CancelCount++;
    }

    private static void RequireThrows(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid binding/lifetime operation was accepted.");
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
#endif
