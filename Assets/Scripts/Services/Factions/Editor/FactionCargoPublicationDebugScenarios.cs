using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class FactionCargoPublicationDebugScenarios
{
    internal static IEnumerator RunReinforcementWorldRestoreForArchitectureBatch(
        DungeonRuntimeLifetimeScope scope, List<string> report, Action<string, string> write, string baseline)
    {
        var container = scope.Container;
        var runtime = (FactionRuntimeApplicationAdapter)container.Resolve<IFactionRuntime>();
        var domain = new FactionDomainRuntime(container.Resolve<DungeonRuntimeAggregateRootStore>());
        var roots = container.Resolve<DungeonRuntimeAggregateRootStore>();
        var world = container.Resolve<ICharacterAiWorldRegistry>();
        var catalog = container.Resolve<IRunCharacterCatalog>();
        var spawners = container.Resolve<ICharacterSpawnerProvider>();
        var saves = container.Resolve<IDungeonGameSaveService>();
        FactionDefinitionSnapshot definition = runtime.Definitions.FirstOrDefault(value =>
            catalog.Characters.Any(character => character != null && string.Equals(
                character.SpeciesTag, value.SpeciesTag, StringComparison.OrdinalIgnoreCase)));
        Require(definition != null && domain.CurrentDay >= 1, "Official reinforcement definition/day unavailable.");
        var factory = new PublicationFaultFactory(container.Resolve<ICharacterSpawnObjectFactory>());
        FieldInfo clockField = typeof(FactionRuntimeApplicationAdapter).GetField("clock", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo ownerField = typeof(FactionRuntimeApplicationAdapter).GetField("reinforcements", BindingFlags.Instance | BindingFlags.NonPublic);
        Type ownerType = typeof(FactionRuntimeApplicationAdapter).Assembly.GetType(
            "FactionReinforcementMaterializer", throwOnError: true);
        object owner = Activator.CreateInstance(ownerType, BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { domain, runtime.Definitions, catalog, spawners, factory, world }, null);
        CharacterActor Actor(string id)
        {
            CharacterActor[] matches = world.AllCharacters.Where(value => value != null && value.Identity.PersistentId == id).ToArray();
            Require(matches.Length == 1, "Current lifetime actor count=" + matches.Length + ";id=" + id + ";"
                + string.Join(",", matches.Select(value => value.GetInstanceID() + ":active=" + value.gameObject.activeInHierarchy
                    + ":published=" + value.HasBeenPublished + ":detached=" + value.IsDetachedRestoreCandidate)));
            return matches[0];
        }
        void AssertActors(FactionRouteState route, string[] ids, string phase)
        {
            FactionRouteState snapshot = runtime.Routes.Single(value => value.routeId == route.routeId);
            Require(route.actorsSpawned && route.reinforcementActorIds.SequenceEqual(ids)
                && ReferenceEquals(route, domain.Routes.Single(value => value.routeId == route.routeId))
                && !ReferenceEquals(route, snapshot)
                && JsonUtility.ToJson(route) == JsonUtility.ToJson(snapshot),
                phase + ": completion did not update the current authoritative faction route.");
            foreach (string id in ids)
            {
                CharacterActor actor = Actor(id);
                var marker = actor.GetComponent<FactionReinforcementMarker>();
                Require(actor.gameObject.activeInHierarchy && actor.characterType == CharacterType.NPC
                    && actor.TryGetAbility(out AbilityWork work)
                    && work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard) == WorkPriorityLevel.Priority1
                    && marker != null && marker.RouteId == route.routeId
                    && marker.FactionId == route.factionId && marker.RouteStrength == route.strength,
                    phase + ": restored actor composition/guard/marker missing; id=" + id
                        + ";marker=" + (marker != null) + ";life=" + actor.CurrentLifecycleState);
                Require(container.Resolve<ICharacterLifeQuery>().TryGet(new CharacterId(id), out _)
                    && container.Resolve<ICharacterNarrativeQuery>().TryGet(new CharacterId(id), out _),
                    phase + ": current life/narrative join missing.");
            }
        }
        foreach (bool responseLoss in new[] { false, true })
        {
            string scenario = responseLoss ? "response-loss-full-prefix" : "partial-prefix";
            container.Resolve<IFactionCampaignCommand>().ApplyFactionChange(definition.StableId, 100, -100, 2);
            Require(runtime.TryCompleteAllianceProject(definition.StableId, out string allianceFailure),
                scenario + ": alliance setup command failed: " + allianceFailure);
            Require(runtime.TryRequestReinforcement(definition.StableId, out string routeId, out string requestFailure),
                scenario + ": actual reinforcement request failed: " + requestFailure);
            FactionRouteState route = domain.Routes.Single(value => value.routeId == routeId);
            int expectedCount = Mathf.Clamp(Mathf.CeilToInt(route.strength / 40f), 1, 3);
            Require(expectedCount > 1, "Official reinforcement request did not provide a partial-prefix test boundary.");
            string[] ids = Enumerable.Range(1, expectedCount)
                .Select(index => CharacterId.FromStableSuffix(routeId + ":ally:" + index).Value).ToArray();
            Require(!world.AllCharacters.Any(value => value != null && ids.Contains(value.Identity.PersistentId)),
                "Official integration route IDs collide with existing actors.");
            factory.FailAfterPublish = responseLoss;
            factory.FailPublishCall = factory.PublishCalls + (responseLoss ? expectedCount : 2);
            Exception fault = null;
            var previousClock = (IGameClock)clockField.GetValue(runtime);
            object previousOwner = ownerField.GetValue(runtime);
            try
            {
                // Drive the genuine arrival reservation/ledger/terminal producer;
                // only travel time and the publisher's response are controlled.
                clockField.SetValue(runtime, new ReinforcementArrivalClock(previousClock.Time, 20f * (route.path.Count + 1)));
                ownerField.SetValue(runtime, owner);
                runtime.Tick();
            }
            catch (Exception exception) { fault = exception; }
            finally { clockField.SetValue(runtime, previousClock); ownerField.SetValue(runtime, previousOwner); }
            string[] prefix = responseLoss ? ids : ids.Take(1).ToArray();
            Require(fault?.Message == PublicationFaultFactory.Fault && !route.actorsSpawned
                && route.status == FactionRouteStatus.Arrived && !string.IsNullOrWhiteSpace(route.arrivalPublicationId)
                && route.reinforcementActorIds.SequenceEqual(prefix)
                && world.AllCharacters.Count(value => value != null && ids.Contains(value.Identity.PersistentId)) == prefix.Length,
                scenario + ": committed prefix/fault boundary is incorrect.");
            CharacterActor outgoingPrefix = Actor(ids[0]);
            FactionAggregateState outgoingRoot = roots.GetOrCreate(() => new FactionAggregateState());
            string pending = saves.ToJson(saves.Capture(), false);
            write("faction-world-" + scenario + "-pending.json", pending);
            Require(saves.TryRestore(saves.FromJson(pending), out var restored)
                && restored.Warnings.Count == 0,
                scenario + ": official pending world restore failed: " + string.Join(" | ", restored.Errors));
            FactionRouteState current = domain.Routes.Single(value => value.routeId == routeId);
            Require(!ReferenceEquals(outgoingRoot, roots.GetOrCreate(() => new FactionAggregateState()))
                && !ReferenceEquals(route, current) && !ReferenceEquals(outgoingPrefix, Actor(ids[0]))
                && !current.actorsSpawned && current.reinforcementActorIds.SequenceEqual(prefix),
                scenario + ": official restore did not publish fresh root/actors with exact pending progress.");
            int[] entryVersions = prefix.Select(id => Actor(id).GetAbility<AbilityMove>().MovementOperationVersionForDiagnostics).ToArray();
            runtime.OnRestoreCompleted();
            runtime.OnRestoreCompleted();
            Require(entryVersions.SequenceEqual(prefix.Select(id => Actor(id).GetAbility<AbilityMove>().MovementOperationVersionForDiagnostics)),
                scenario + ": repeated restore hook restarted a current entry operation.");
            factory.FailPublishCall = 0;
            factory.FailAfterPublish = false;
            runtime.Tick();
            AssertActors(current, ids, scenario + "-retry");
            Require(!route.actorsSpawned && route.reinforcementActorIds.Count == prefix.Length,
                scenario + ": retry mutated the retired route object.");
            int actualCount = world.AllCharacters.Count(value => value != null && ids.Contains(value.Identity.PersistentId));
            runtime.Tick();
            runtime.Tick();
            Require(actualCount == ids.Length
                && world.AllCharacters.Count(value => value != null && ids.Contains(value.Identity.PersistentId)) == actualCount,
                scenario + ": repeated current-root retry duplicated registered actors.");
            report.Add("PASS " + scenario + " official whole-world pending JSON -> fresh root/actors -> production Tick; exact prefix retained, actual root completed, retired route unchanged, no duplicate actors");
            UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = false;
            container.Resolve<IGameTimeScaleController>().Scale = 1;
            Time.timeScale = 1;
            float deadline = Time.realtimeSinceStartup + 20;
            while (ids.Any(id => Actor(id).CurrentLifecycleState != CharacterLifecycleState.Active)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Require(ids.All(id => Actor(id).CurrentLifecycleState == CharacterLifecycleState.Active),
                scenario + ": restored actual actors did not complete entry: "
                    + string.Join(",", ids.Select(id => id + "=" + Actor(id).CurrentLifecycleState)));
            UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
            container.Resolve<IGameTimeScaleController>().Scale = 0;
            Time.timeScale = 0;
            CharacterActor previous = Actor(ids[0]);
            string completed = saves.ToJson(saves.Capture(), false);
            write("faction-world-" + scenario + "-completed.json", completed);
            Require(saves.TryRestore(saves.FromJson(completed), out var completedRestore)
                && completedRestore.Warnings.Count == 0,
                scenario + ": official completed world restore failed: " + string.Join(" | ", completedRestore.Errors));
            current = domain.Routes.Single(value => value.routeId == routeId);
            Require(!ReferenceEquals(previous, Actor(ids[0])), scenario + ": terminal restore reused outgoing actor.");
            runtime.Tick();
            runtime.Tick();
            AssertActors(current, ids, scenario + "-completed-restore");
            Require(ids.All(id => Actor(id).CurrentLifecycleState == CharacterLifecycleState.Active),
                scenario + ": completed restore restarted entry or changed active life.");
            report.Add("PASS " + scenario + " actual entry Active; official completed JSON/fresh actor restore and production Tick keep one actor per ID and current root/guard/marker/life/narrative joins");
            Require(saves.TryRestore(saves.FromJson(baseline), out var reset) && reset.Warnings.Count == 0,
                scenario + ": between-case baseline restore failed: " + string.Join(" | ", reset.Errors));
            UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
            container.Resolve<IGameTimeScaleController>().Scale = 0;
            Time.timeScale = 0;
            int frame = Time.frameCount;
            while (Time.frameCount <= frame + 1) yield return null;
        }
    }

    private sealed class ReinforcementArrivalClock : IGameClock
    {
        internal ReinforcementArrivalClock(float time, float delta) { Time = time; DeltaTime = delta; }
        public float DeltaTime { get; }
        public float Time { get; }
        public int FrameCount => UnityEngine.Time.frameCount;
        public bool IsPaused => false;
    }

    public static IEnumerator RunReinforcementForArchitectureBatch(
        DungeonRuntimeLifetimeScope scope, List<string> report)
    {
        var container = scope.Container;
        var catalog = container.Resolve<IRunCharacterCatalog>();
        var spawners = container.Resolve<ICharacterSpawnerProvider>();
        var world = container.Resolve<ICharacterAiWorldRegistry>();
        var definitions = container.Resolve<IFactionRuntime>().Definitions;
        FactionDefinitionSnapshot definition = definitions.FirstOrDefault(value =>
            catalog.Characters.Any(character => character != null && string.Equals(
                character.SpeciesTag, value.SpeciesTag, StringComparison.OrdinalIgnoreCase)));
        Require(definition != null, "Reinforcement fixture requires a genuine authored faction/species template.");
        Require(spawners.TryGetSpawner(out var spawner) && spawner != null
            && spawner.characterPrefab != null && spawner.TryGetEntryGridPosition(out _),
            "Actual reinforcement spawner/prefab/entry is unavailable.");
        var factory = new PublicationFaultFactory(container.Resolve<ICharacterSpawnObjectFactory>());
        var domain = new FactionDomainRuntime(new DungeonRuntimeAggregateRootStore());
        domain.ReplaceState(new FactionAggregateState { RouteSequence = 910000 });
        var route = new FactionRouteState
        {
            factionId = definition.StableId, kind = FactionRouteKind.Reinforcement,
            status = FactionRouteStatus.Arrived, strength = 41, createdDay = 1, estimatedArrivalDay = 1
        };
        domain.AddRoute(route);
        string routeId = route.routeId;
        string[] expectedIds = Enumerable.Range(1, 2)
            .Select(index => CharacterId.FromStableSuffix(routeId + ":ally:" + index).Value).ToArray();
        Require(!world.Characters.Any(actor => actor != null && expectedIds.Contains(actor.Identity.PersistentId)),
            "Controlled route IDs already exist in the actual registry.");
        Type ownerType = typeof(FactionRuntimeApplicationAdapter).Assembly.GetType(
            "FactionReinforcementMaterializer", throwOnError: true);
        MethodInfo materialize = ownerType.GetMethod("MaterializeReinforcements",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reinforcement owner entry is missing.");
        object NewOwner() => Activator.CreateInstance(ownerType,
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { domain, definitions, catalog, spawners, factory, world }, null);
        void Execute(object owner, FactionRouteState value)
        {
            try { materialize.Invoke(owner, new object[] { value }); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }
        string before = JsonUtility.ToJson(route);
        try
        {
            factory.FailPublishCall = 2;
            Exception injected = null;
            try { Execute(NewOwner(), route); }
            catch (Exception exception) { injected = exception; }
            Require(injected?.Message == PublicationFaultFactory.Fault,
                "Controlled second publication fault was not reached: " + injected);
            yield return null;
            yield return null;
            int visibleAfterFailure = world.Characters.Count(actor => actor != null
                && expectedIds.Contains(actor.Identity.PersistentId));
            int leakedObjects = factory.Created.Count(value => value != null);
            report.Add("FAULT_OBSERVATION: registered=" + visibleAfterFailure
                + ";objects=" + leakedObjects + ";ids=" + route.reinforcementActorIds.Count
                + ";spawned=" + route.actorsSpawned);
            var expectedPending = JsonUtility.FromJson<FactionRouteState>(before);
            expectedPending.reinforcementActorIds.Add(expectedIds[0]);
            CharacterActor firstCommitted = world.Characters.Single(actor => actor != null
                && actor.Identity.PersistentId == expectedIds[0]);
            Require(JsonUtility.ToJson(route) == JsonUtility.ToJson(expectedPending)
                && visibleAfterFailure == 1 && leakedObjects == 1,
                "Failed reinforcement publication lost a committed child or retained an unpublished candidate/phantom receipt.");
            Require(container.Resolve<ICharacterLifeQuery>().TryGet(
                    CharacterId.FromStableSuffix(routeId + ":ally:1"), out _)
                && container.Resolve<ICharacterNarrativeQuery>().TryGet(
                    CharacterId.FromStableSuffix(routeId + ":ally:1"), out _),
                "Committed reinforcement lost its canonical life/narrative authority.");
            Require(ValidateActorReceipts(route).Count == 0,
                "Canonical incomplete reinforcement prefix is not save-valid.");
            var wrongPhase = JsonUtility.FromJson<FactionRouteState>(JsonUtility.ToJson(route));
            wrongPhase.status = FactionRouteStatus.Traveling;
            Require(ValidateActorReceipts(wrongPhase).Count > 0,
                "Save validator admitted pending reinforcement actors before route arrival.");
            report.Add("PASS failed second publication retains one committed canonical child/life/narrative receipt; unpublished candidate retired; pending prefix save-valid");
            route = JsonUtility.FromJson<FactionRouteState>(JsonUtility.ToJson(route));
            var candidate = new FactionAggregateState { RouteSequence = 910001 };
            candidate.Routes.Add(route);
            domain = new FactionDomainRuntime(new DungeonRuntimeAggregateRootStore());
            domain.ReplaceState(candidate);
            int createsBefore = factory.Created.Count;
            int publishesBefore = factory.PublishCalls;
            factory.FailPublishCall = 0;
            object restoredOwner = NewOwner();
            Execute(restoredOwner, route);
            CharacterActor[] actors = world.Characters.Where(actor => actor != null
                && expectedIds.Contains(actor.Identity.PersistentId)).ToArray();
            Require(route.actorsSpawned && route.reinforcementActorIds.SequenceEqual(expectedIds)
                && actors.Length == 2 && actors.Select(actor => actor.Identity.PersistentId).Distinct().Count() == 2
                && actors.Contains(firstCommitted)
                && factory.Created.Count == createsBefore + 1 && factory.PublishCalls == publishesBefore + 1,
                "Fresh local owner retry did not publish exactly two canonical actors.");
            foreach (CharacterActor actor in actors)
            {
                var marker = actor.GetComponent<FactionReinforcementMarker>();
                Require(actor.gameObject.activeInHierarchy && actor.characterType == CharacterType.NPC
                    && marker != null && marker.RouteId == routeId && marker.FactionId == definition.StableId
                    && marker.RouteStrength == 41
                    && actor.TryGetAbility(out AbilityWork work)
                    && work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard) == WorkPriorityLevel.Priority1,
                    "Published reinforcement lost its authored composition/marker/guard priority.");
            }
            report.Add("PASS pending route JSON/fresh domain+owner retry retain the first actor and publish only the missing canonical guard");
            UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = false;
            container.Resolve<IGameTimeScaleController>().Scale = 1;
            Time.timeScale = 1;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (actors.Any(actor => actor.CurrentLifecycleState != CharacterLifecycleState.Active)
                && Time.realtimeSinceStartup < deadline) yield return null;
            Require(actors.All(actor => actor.CurrentLifecycleState == CharacterLifecycleState.Active),
                "Actual reinforcement movement/lifecycle did not enter the dungeon.");
            UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
            container.Resolve<IGameTimeScaleController>().Scale = 0;
            Time.timeScale = 0;
            report.Add("PASS both production reinforcement entry lifecycles reach Active");
            route = JsonUtility.FromJson<FactionRouteState>(JsonUtility.ToJson(route));
            candidate = new FactionAggregateState { RouteSequence = 910001 };
            candidate.Routes.Add(route);
            domain.ReplaceState(candidate);
            createsBefore = factory.Created.Count;
            publishesBefore = factory.PublishCalls;
            Execute(NewOwner(), route);
            Execute(NewOwner(), route);
            Require(factory.Created.Count == createsBefore && factory.PublishCalls == publishesBefore
                && world.Characters.Count(actor => actor != null && expectedIds.Contains(actor.Identity.PersistentId)) == 2
                && route.reinforcementActorIds.SequenceEqual(expectedIds),
                "Completed route JSON replay duplicated a reinforcement.");
            var malformed = JsonUtility.FromJson<FactionRouteState>(JsonUtility.ToJson(route));
            malformed.reinforcementActorIds[0] = expectedIds[1];
            bool rejected = false;
            try { FactionPayloadValidation.CanonicalizeReinforcementActorIdsForRestore(malformed); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && route.reinforcementActorIds.SequenceEqual(expectedIds),
                "Noncanonical restored actor sequence was accepted or mutated the completed route.");
            Require(ValidateActorReceipts(malformed).Count > 0,
                "Save validator admitted a conflicting actor sequence.");
            malformed = JsonUtility.FromJson<FactionRouteState>(JsonUtility.ToJson(route));
            malformed.actorsSpawned = false;
            malformed.reinforcementActorIds.Add(CharacterId.FromStableSuffix(routeId + ":ally:3").Value);
            Require(ValidateActorReceipts(malformed).Count > 0,
                "Save validator admitted more pending child receipts than the strength permits.");
            malformed = JsonUtility.FromJson<FactionRouteState>(JsonUtility.ToJson(route));
            malformed.reinforcementActorIds.Clear();
            Require(ValidateActorReceipts(malformed).Count > 0,
                "Save validator admitted completed reinforcement without actors.");
            malformed = JsonUtility.FromJson<FactionRouteState>(JsonUtility.ToJson(route));
            malformed.reinforcementActorIds.RemoveAt(1);
            Require(ValidateActorReceipts(malformed).Count == 0,
                "Existing completed shorter reinforcement receipt lost read compatibility.");
            report.Add("PASS completed route JSON replay is inert; invalid canonical sequence rejects without mutation");
            report.Add("PASS pending count/empty completed negatives and existing completed-shorter receipt compatibility");

            var responseLost = new FactionRouteState
            {
                factionId = definition.StableId, kind = FactionRouteKind.Reinforcement,
                status = FactionRouteStatus.Arrived, strength = 1, createdDay = 1, estimatedArrivalDay = 1
            };
            domain.AddRoute(responseLost);
            string responseLostId = CharacterId.FromStableSuffix(responseLost.routeId + ":ally:1").Value;
            factory.FailPublishCall = factory.PublishCalls + 1;
            factory.FailAfterPublish = true;
            injected = null;
            try { Execute(NewOwner(), responseLost); }
            catch (Exception exception) { injected = exception; }
            Require(injected?.Message == PublicationFaultFactory.Fault
                && !responseLost.actorsSpawned
                && responseLost.reinforcementActorIds.SequenceEqual(new[] { responseLostId })
                && ValidateActorReceipts(responseLost).Count == 0,
                "Lost publication response did not retain the committed child as valid pending progress.");
            CharacterActor committedAfterResponseLoss = world.AllCharacters.Single(actor => actor != null
                && actor.Identity.PersistentId == responseLostId);
            createsBefore = factory.Created.Count;
            publishesBefore = factory.PublishCalls;
            factory.FailPublishCall = 0;
            responseLost = JsonUtility.FromJson<FactionRouteState>(JsonUtility.ToJson(responseLost));
            Execute(NewOwner(), responseLost);
            Execute(NewOwner(), responseLost);
            Require(responseLost.actorsSpawned && factory.Created.Count == createsBefore
                && factory.PublishCalls == publishesBefore
                && world.AllCharacters.Count(actor => actor != null && actor.Identity.PersistentId == responseLostId) == 1,
                "Lost-response retry republished a committed actor.");
            UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = false;
            container.Resolve<IGameTimeScaleController>().Scale = 1;
            Time.timeScale = 1;
            deadline = Time.realtimeSinceStartup + 20f;
            while (committedAfterResponseLoss.CurrentLifecycleState != CharacterLifecycleState.Active
                && Time.realtimeSinceStartup < deadline) yield return null;
            Require(committedAfterResponseLoss.CurrentLifecycleState == CharacterLifecycleState.Active,
                "Lost-response recovery did not resume production entry.");
            UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
            container.Resolve<IGameTimeScaleController>().Scale = 0;
            Time.timeScale = 0;
            report.Add("PASS post-publication response loss retains one exact child; JSON retry neither recreates nor republishes; normal entry reaches Active");
        }
        finally
        {
            foreach (GameObject value in factory.Created.Where(value => value != null).ToArray())
                factory.Destroy(value);
        }
    }

    private static IReadOnlyList<string> ValidateActorReceipts(FactionRouteState route)
    {
        Type errorsType = typeof(FactionPayloadValidation).GetNestedType("ValidationErrors", BindingFlags.NonPublic);
        object errors = Activator.CreateInstance(errorsType, nonPublic: true);
        typeof(FactionPayloadValidation).GetMethod("ValidateReinforcementActors", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { route, FactionPayloadValidation.RouteSequenceOf(route.routeId), errors });
        return (IReadOnlyList<string>)errorsType.GetProperty("Errors").GetValue(errors);
    }

    private sealed class PublicationFaultFactory : ICharacterSpawnObjectFactory
    {
        internal const string Fault = "qa-faction-second-publication-failure";
        private readonly ICharacterSpawnObjectFactory inner;
        internal readonly List<GameObject> Created = new();
        internal int FailPublishCall;
        internal int PublishCalls;
        internal bool FailAfterPublish;
        internal PublicationFaultFactory(ICharacterSpawnObjectFactory inner) => this.inner = inner;
        public GameObject CreateInactive(GameObject prefab) => CreateInactive(prefab, null);
        public GameObject CreateInactive(GameObject prefab, Action<GameObject> compose)
        {
            GameObject value = inner.CreateInactive(prefab, compose);
            Created.Add(value);
            return value;
        }
        public void Publish(GameObject value)
        {
            PublishCalls++;
            if (!FailAfterPublish && PublishCalls == FailPublishCall) throw new InvalidOperationException(Fault);
            inner.Publish(value);
            if (FailAfterPublish && PublishCalls == FailPublishCall) throw new InvalidOperationException(Fault);
        }
        public void Destroy(GameObject value) => inner.Destroy(value);
        public GameObject CreateDetached(GameObject prefab) => inner.CreateDetached(prefab);
        public GameObject CreateDetached(GameObject prefab, Action<GameObject> compose) => inner.CreateDetached(prefab, compose);
        public void ComposeDetached(GameObject value) => inner.ComposeDetached(value);
        public void Inject(GameObject value) => inner.Inject(value);
        public void InjectAddedAbility(CharacterAbility value) => inner.InjectAddedAbility(value);
        public void PublishDetached(GameObject value) => inner.PublishDetached(value);
        public DetachedCharacterPublication PublishDetachedInactive(GameObject value) => inner.PublishDetachedInactive(value);
        public void ValidateDetachedPublication(DetachedCharacterPublication value) => inner.ValidateDetachedPublication(value);
        public void CompleteDetachedPublication(DetachedCharacterPublication value) => inner.CompleteDetachedPublication(value);
        public void RollbackDetachedPublication(DetachedCharacterPublication value) => inner.RollbackDetachedPublication(value);
    }

    [MenuItem("Dungeon Story/QA/V27/Faction Exact Cargo Publication")]
    public static void Verify()
    {
        MethodInfo quantityMethod = typeof(FactionRouteCargoRuntime)
            .GetMethod(
                "ResolveCargoDeliveryQuantity",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Faction cargo quantity projector is missing.");
        int Q(int amount, int strength) => (int)quantityMethod.Invoke(
            null,
            new object[] { amount, strength });
        Require(Q(3, 50) == 2
            && Q(5, 50) == 2
            && Q(7, 50) == 4,
            "Faction cargo strength scaling no longer matches midpoint-to-even semantics.");

        FactionRouteState route = new()
        {
            routeId = "faction-route:17",
            strength = 100,
            cargo = new List<FactionCargoLine>
            {
                new() { itemId = "material:iron-ingot", amount = 3 },
                new() { itemId = "material:lumber", amount = 5 },
                new() { itemId = "food:preserved-ration", amount = 7 }
            }
        };
        MethodInfo planMethod = typeof(FactionRouteCargoRuntime)
            .GetMethod(
                "CreateCargoPublicationPlan",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Faction cargo exact publication planner is missing.");
        PhysicalItemExactSourcePublicationPlan plan =
            (PhysicalItemExactSourcePublicationPlan)planMethod.Invoke(
                null,
                new object[] { route, new Vector2Int(9, 4) });
        Require(plan.OwnerDomain == "faction.route-cargo"
            && plan.OwnerOperationId == route.routeId
            && plan.DropPosition == new Vector2Int(9, 4)
            && plan.Outputs.Count == 3
            && plan.Outputs.Select(value => value.OutputLineId)
                .SequenceEqual(new[]
                {
                    "cargo:0000:material:iron-ingot",
                    "cargo:0001:material:lumber",
                    "cargo:0002:food:preserved-ration"
                }),
            "Faction cargo was not planned as one deterministic whole vector.");

        string runtimePath = Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Project root is missing."),
            "Assets/Scripts/Services/Factions/FactionRouteCargoRuntime.cs");
        string source = File.ReadAllText(runtimePath);
        Require(!source.Contains("itemSpawner.Spawn", StringComparison.Ordinal)
            && source.Contains("exactSources.TryPrepare", StringComparison.Ordinal)
            && source.Contains("exactSources.TryCommitReleased", StringComparison.Ordinal)
            && source.Contains(
                "FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned",
                StringComparison.Ordinal),
            "Faction cargo publication bypassed the one-vector exact source boundary.");

        ValidateDeliveredSaveReceipt();

        Debug.Log("FACTION_EXACT_CARGO_PUBLICATION_PASS");
    }

    private static void ValidateDeliveredSaveReceipt()
    {
        IDungeonItemCatalogProvider items = EditorItemCatalogFactory.Create();
        IReadOnlyList<FactionDefinitionSnapshot> definitions = AssetDatabase
            .FindAssets(
                "t:DungeonFactionDefinitionSO",
                new[] { "Assets/Resources/SO/Factions/Dungeons" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<DungeonFactionDefinitionSO>)
            .Where(value => value != null)
            .Select(value => value.ToSnapshot())
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        FactionDefinitionSnapshot definition = definitions[0];
        FactionRouteEconomicPolicyRegistry policies = new(new[]
        {
            new PaidMarketPurchaseFactionRouteEconomicPolicy(items)
        });
        Require(policies.TryCreateQuote(
                definition,
                FactionRouteKind.TradeCaravan,
                out FactionRouteQuoteSnapshot quote,
                out string quoteFailure),
            quoteFailure);
        FactionRouteState route = new()
        {
            routeId = "faction-route:1",
            factionId = definition.StableId,
            kind = FactionRouteKind.TradeCaravan,
            status = FactionRouteStatus.Arrived,
            path = new List<FactionHexCoordSaveData>
            {
                new() { q = 4, r = -2 },
                new() { q = 3, r = -1 }
            },
            pathIndex = 1,
            strength = 100,
            createdDay = 1,
            estimatedArrivalDay = 2,
            cargo = definition.TradeCargo
                .Select(value => value.Clone())
                .OrderBy(value => value.itemId, StringComparer.Ordinal)
                .ToList(),
            settlement = new FactionRouteSettlementReceipt
            {
                state = FactionRouteSettlementState.Paid,
                capabilityId = quote.CapabilityId,
                capabilityVersion = quote.CapabilityVersion,
                operationSequence = 1,
                cargoAuthoredGold = quote.CargoAuthoredGold,
                paymentGold = quote.PaymentGold,
                quoteLines = quote.QuoteLines
                    .Select(value => value.Clone())
                    .ToList(),
                sourceDigest = quote.SourceDigest,
                quoteDigest = quote.QuoteDigest,
                transactionId = "economy-transaction:faction-cargo-fixture",
                transactionSourceId =
                    "faction-route-settlement:00000001",
                transactionTargetId = definition.StableId,
                balanceBefore = 10000,
                balanceAfter = 10000 - quote.PaymentGold
            }
        };
        MethodInfo planMethod = typeof(FactionRouteCargoRuntime)
            .GetMethod(
                "CreateCargoPublicationPlan",
                BindingFlags.Static | BindingFlags.NonPublic);
        PhysicalItemExactSourcePublicationPlan plan =
            (PhysicalItemExactSourcePublicationPlan)planMethod.Invoke(
                null,
                new object[] { route, new Vector2Int(6, 8) });
        List<ProductionDomainPublishedStackSaveData> stacks = plan.Outputs
            .Select((output, index) =>
                new ProductionDomainPublishedStackSaveData
                {
                    outputLineId = output.OutputLineId,
                    itemId = output.ItemDefinitionId.Value,
                    itemInstanceId = string.Empty,
                    stackId = $"stack:faction-cargo:{index:D4}",
                    quantity = output.Quantity,
                    massGrams = checked(output.Quantity * 100L)
                })
            .ToList();
        route.cargoDelivery = new FactionRouteCargoDeliveryReceipt
        {
            state = FactionRouteCargoDeliveryState.Delivered,
            batchCommitId = plan.BatchCommitId,
            destinationId = plan.DestinationId,
            outcomeFingerprint = plan.OutcomeFingerprint,
            deliveryX = plan.DropPosition.x,
            deliveryY = plan.DropPosition.y,
            totalMassGrams = stacks.Sum(value => value.massGrams),
            stacks = stacks
        };
        DungeonFactionSaveData payload = new()
        {
            currentDay = 3,
            routeSequence = 1,
            routeSettlementOperationSequence = 1,
            allianceBenefitBalanceMilliEwu = 39142546L,
            allianceBenefitRefillRemainder = 0L,
            allianceBenefitLastRefillDay = 3,
            allianceBenefitAuthorityDigest =
                "c539c892bb0b8355801c923c3a86da8f2a331ed459414684aa2dc60d0767fe15",
            factions = definitions.Select(value => new DungeonFactionState
            {
                factionId = value.StableId,
                homeQ = 4,
                homeR = -2
            }).ToList(),
            routes = new List<FactionRouteState> { route }
        };
        Require(FactionPayloadValidation.Validate(
                payload,
                definitions,
                _ => true).Count == 0,
            "terminal whole-vector cargo receipt was rejected");
        route.cargoDelivery.outcomeFingerprint = new string('0', 64);
        Require(FactionPayloadValidation.Validate(
                payload,
                definitions,
                _ => true).Count > 0,
            "tampered whole-vector outcome fingerprint was accepted");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
