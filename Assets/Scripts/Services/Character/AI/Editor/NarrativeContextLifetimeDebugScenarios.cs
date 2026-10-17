#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

public static class NarrativeContextLifetimeDebugScenarios
{
    private const string Pending = "DungeonStory.NarrativeContextLifetime.Pending";
    private const string Started = "DungeonStory.NarrativeContextLifetime.Started";
    private static IEnumerator preparationVerification;
    private static int preparedRequestIndex;

    public static void RequestActualContainerForBatch()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Narrative context verification requires isolated batch execution.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "PlayMode is already active.");
        Require(!File.Exists(EvidencePath("narrative-context-lifetime.json")), "Report already exists.");
        preparedRequestIndex = 0;
        preparationVerification = null;
        SessionState.SetBool(Pending, true);
        SessionState.SetString(Started, EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayScene.unity", OpenSceneMode.Single);
        Resume();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
    }

    private static void Pump()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        try
        {
            double started = double.Parse(SessionState.GetString(Started, ""), CultureInfo.InvariantCulture);
            Require(EditorApplication.timeSinceStartup - started <= 60, "Gameplay container readiness timed out.");
            if (!EditorApplication.isPlaying) return;
            if (preparationVerification != null)
            {
                if (!preparationVerification.MoveNext()) Complete();
                return;
            }
            var scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value?.Container != null);
            if (scope == null) return;
            var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Require(game != null, "Actual GameplayScene manager is missing.");
            game.isPause = true;
            scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
            Time.timeScale = 0;
            Verify(scope.Container);
            preparationVerification = VerifyPreparation(scope.Container);
            if (!preparationVerification.MoveNext()) Complete();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try { WriteNew("narrative-context-lifetime-failure.txt", exception.ToString()); }
            finally { Finish(1); }
        }
    }

    private static void Complete()
    {
        WriteNew("narrative-context-lifetime.json", "{\"status\":\"PASS\",\"actualGameplayContainer\":true,"
            + "\"independentContainers\":3,\"missingAuthoritiesRejected\":5,"
            + "\"disposedRequestPathsRejected\":4,\"frozenSnapshotPreserved\":true,"
            + "\"actualPreparationUi\":true,\"preparedRequestJson\":true,\"rerollCancelRetry\":true,"
            + "\"queueLifecycleContracts\":7}");
        Debug.Log("NARRATIVE_CONTEXT_LIFETIME=PASS; gameplay=1; independent=3; missing-authorities=5; actual-preparation-ui=1; reroll-cancel-retry=1");
        Finish(0);
    }

    private static IEnumerator VerifyPreparation(IObjectResolver gameplay)
    {
        var queueRows = new List<string> { "contract\tstatus" };
        IEnumerator queueVerification = LocalLlmRequestQueueLifecycleFixture.Verify(queueRows.Add);
        using (queueVerification as IDisposable)
            while (queueVerification.MoveNext()) yield return null;
        WriteNew("llm-request-queue-lifecycle.tsv", string.Join("\n", queueRows) + "\n");
        Debug.Log("LLM_REQUEST_QUEUE_LIFECYCLE=PASS; contracts=7; actual-coroutine-cancel-retry; binary-admission-only");

        var previousContext = gameplay.Resolve<NarrativeRequestContextBuilder>();
        var previousSkillContext = gameplay.Resolve<ICharacterSkillNarrativeContext>();
        Require(previousSkillContext is GameplayCharacterSkillNarrativeContext,
            "Gameplay skill generation lost its gameplay authority context.");
        Require(gameplay.Resolve<IDungeonSceneNavigator>().StartNewPreparation(DungeonDifficulty.Normal),
            "Actual navigation rejected preparation.");
        gameplay = null;
        DungeonPreparationLifetimeScope scope;
        while (true)
        {
            scope = UnityEngine.Object.FindObjectsByType<DungeonPreparationLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value?.Container != null);
            if (scope != null && SceneManager.GetActiveScene().name == DungeonSceneNavigator.PreparationSceneName
                && FindButton("PreparationOwnerNextButton") != null
                && !scope.Container.Resolve<IDungeonSceneNavigator>().IsTransitioning) break;
            yield return null;
        }
        Reject<ObjectDisposedException>(() => previousContext.ForActor("skill", null, true, true));
        Reject<ObjectDisposedException>(() => previousSkillContext.GetOutcomeEvidence(null));
        Reject<ObjectDisposedException>(() => previousSkillContext.TryResolveExactBinding(null));
        Reject<ObjectDisposedException>(() => previousSkillContext.BuildSkillMaterialWithRequiredFacts(null, null));
        IObjectResolver preparationContainer = scope.Container;
        var context = preparationContainer.Resolve<ICharacterSkillNarrativeContext>();
        Require(context is PreparedCharacterSkillNarrativeContext
                && ReferenceEquals(context, preparationContainer.Resolve<ICharacterSkillNarrativeContext>()),
            "Preparation did not own one prepared skill context.");
        var service = preparationContainer.Resolve<IStartPartyPreparationService>();
        var diagnostics = preparationContainer.Resolve<ICharacterSkillGenerationDiagnostics>();
        Button next = FindButton("PreparationOwnerNextButton");
        Require(next.interactable, "Actual preparation owner selection is not ready.");
        next.onClick.Invoke();
        Require(service.IsPreparing && service.Members.Count > 1, "Actual preparation UI did not begin the party.");
        StartPartyMemberPreparation member = service.Members.First(value => !value.IsOwner && !value.IsReserve);
        CharacterProgression previous = member.Progression;
        CharacterSkillDraft draft = RequirePreparedRequest(context, previous);
        string frozen = JsonUtility.ToJson(draft.requestContext);
        string oldId = previous.Actor.Identity.PersistentId;
        CharacterSkillDraft[] oldDrafts = previous.Drafts.ToArray();
        Require(service.TryPartialReroll(member.Index, StartPartyRerollGroup.Identity, out string rerollReason),
            "Actual candidate replacement failed: " + rerollReason);
        Require(member.Progression != previous && member.Progression.Actor.Identity.PersistentId != oldId
                && oldDrafts.All(value => !value.requestSubmitted),
            "Candidate replacement retained an old owner or live request.");
        RequirePreparedRequest(context, member.Progression);
        Require(JsonUtility.ToJson(draft.requestContext) == frozen, "Replacement changed an already-frozen request.");
        var owner = service.Members.Single(value => value.IsOwner).CharacterData;
        service.Cancel();
        Require(!service.IsPreparing && diagnostics.PendingRequestCount == 0, "Preparation cancellation left generation requests.");
        Require(service.Begin(owner, out string restartReason), "Preparation could not retry after cancellation: " + restartReason);
        member = service.Members.First(value => !value.IsOwner && !value.IsReserve);
        RequirePreparedRequest(context, member.Progression);
        Require(diagnostics.PendingRequestCount > 0, "Retry did not enter the real generator.");
        CharacterProgression retainedCandidate = member.Progression;
        service.Cancel();
        Require(diagnostics.PendingRequestCount == 0, "Retry cleanup left generation requests.");
        Require(preparationContainer.Resolve<IDungeonSceneNavigator>().LoadTitle(), "Preparation could not return to Title.");
        while (SceneManager.GetActiveScene().name != DungeonSceneNavigator.TitleSceneName
               || UnityEngine.Object.FindObjectsByType<DungeonTitleLifetimeScope>(
                   FindObjectsInactive.Include, FindObjectsSortMode.None).All(value => value?.Container == null))
            yield return null;
        Reject<ObjectDisposedException>(() => context.GetOutcomeEvidence(retainedCandidate));
        Require(JsonUtility.ToJson(draft.requestContext) == frozen, "Scene disposal changed the frozen request.");
        WriteNew("preparation-context-boundary.tsv", "case\tresult\nactual-preparation-container-ui\tPASS\n"
            + "actual-prepared-draft-frozen-json\tPASS\nactual-candidate-replacement-cancels-owner\tPASS\n"
            + "actual-preparation-cancel-retry\tPASS\nactual-scene-disposal-rejects-context\tPASS\n");
    }

    private static CharacterSkillDraft RequirePreparedRequest(
        ICharacterSkillNarrativeContext context, CharacterProgression progression)
    {
        CharacterSkillDraft draft = progression.Drafts.First(value => value.kind == CharacterSkillKind.Active);
        Require(draft.requestSubmitted && draft.requestContextVersion == 1 && draft.requestContext != null,
            "Real preparation generator did not freeze its request: " + draft.presentationCorrection);
        NarrativePublicContextMaterial material = draft.RestoreRequestPublicContext();
        Require(material != null && material.SubjectId == progression.Actor.Identity.PersistentId
                && material.PublicFacts.Any(value => value.OriginalFactId == "prepared-biological-age")
                && material.PublicFacts.All(value => value.Domain != "Need")
                && material.PublicFacts.All(value => !value.OriginalFactId.StartsWith("background:", StringComparison.Ordinal)
                    && !value.OriginalFactId.StartsWith("culture:", StringComparison.Ordinal)),
            "Prepared context invented gameplay state or lost its selected profile.");
        CharacterSkillDraft restored = JsonUtility.FromJson<CharacterSkillDraft>(JsonUtility.ToJson(draft));
        Require(restored.RestoreRequestPublicContext().SemanticHash == material.SemanticHash,
            "Prepared request JSON did not preserve its frozen public context.");
        Require(context.GetOutcomeEvidence(progression).Count == 0
                && !context.TryResolveExactBinding(new GameplayOutcomeEvidenceBindingSnapshot()),
            "Preparation accepted gameplay run evidence.");
        WriteNew("prepared-request-" + (++preparedRequestIndex).ToString("D2", CultureInfo.InvariantCulture) + "-" + progression.Actor.Identity.PersistentId.Replace(':', '_') + ".json", JsonUtility.ToJson(draft, true));
        return draft;
    }

    private static Button FindButton(string name) => UnityEngine.Object.FindObjectsByType<Button>(
        FindObjectsInactive.Exclude, FindObjectsSortMode.None).SingleOrDefault(value => value.name == name
            && value.gameObject.scene.name == DungeonSceneNavigator.PreparationSceneName);

    private static void Verify(IObjectResolver gameplay)
    {
        using var subject = new ActorFixture("character:context-subject", "Context subject");
        using var worldA = new ActorFixture("character:context-world", "World A");
        using var lifetimeA = new ActorFixture("character:context-lifetime", "Lifetime A");
        using var worldB = new ActorFixture("character:context-world", "World B");
        using var lifetimeB = new ActorFixture("character:context-lifetime", "Lifetime B");
        foreach (string id in new[] { worldA.Actor.Identity.PersistentId, lifetimeA.Actor.Identity.PersistentId })
            subject.Actor.Progression.NarrativeLedger.facts.Add(new CharacterNarrativeFact
            {
                domain = CharacterNarrativeDomain.Relationship, factId = "relationship:context-contact",
                subjectId = id, outcome = "met", count = 1, milestoneCount = 1, lastDay = 1
            });

        // Real owners and authored definitions in independent VContainer roots.
        // No Start callback is needed before the first request.
        using var first = new AuthorityScope(subject.Actor, "Human", 12, worldA.Actor, lifetimeA.Actor);
        using var second = new AuthorityScope(subject.Actor, "Orc", 34, worldB.Actor, lifetimeB.Actor);
        NarrativePublicContextMaterial a = CharacterSkillPromptBuilder.BuildPublicMaterial(first.Context, subject.Actor.Progression);
        string frozenA = JsonUtility.ToJson(a.CaptureSnapshot());
        NarrativePublicContextMaterial b = CharacterSkillPromptBuilder.BuildPublicMaterial(second.Context, subject.Actor.Progression);
        string frozenB = JsonUtility.ToJson(b.CaptureSnapshot());
        Require(a.SemanticHash != b.SemanticHash, "Independent authority roots produced identical context.");
        RequireAuthorityFacts(a, first, "12.0", "World A", "Lifetime A");
        RequireAuthorityFacts(b, second, "34.0", "World B", "Lifetime B");
        Require(JsonUtility.ToJson(CharacterSkillPromptBuilder.BuildPublicMaterial(first.Context,
            subject.Actor.Progression).CaptureSnapshot()) == frozenA, "Second root replaced the first root's authorities.");
        Require(ReferenceEquals(first.Context, first.Container.Resolve<NarrativeRequestContextBuilder>()),
            "Context was not owned once by its container.");

        Reject<ArgumentNullException>(() => new NarrativeRequestContextBuilder(null, first.Life, first.Catalog, first.World, first.World));
        Reject<ArgumentNullException>(() => new NarrativeRequestContextBuilder(first.Narratives, null, first.Catalog, first.World, first.World));
        Reject<ArgumentNullException>(() => new NarrativeRequestContextBuilder(first.Narratives, first.Life, null, first.World, first.World));
        Reject<ArgumentNullException>(() => new NarrativeRequestContextBuilder(first.Narratives, first.Life, first.Catalog, null, first.World));
        Reject<ArgumentNullException>(() => new NarrativeRequestContextBuilder(first.Narratives, first.Life, first.Catalog, first.World, null));

        first.Dispose(); // Container disposal, not a direct manual service disposal.
        Reject<ObjectDisposedException>(() => first.Context.ForActor(LocalLlmRequestProfiles.CharacterRecord.Id,
            subject.Actor, false, false));
        Reject<ObjectDisposedException>(() => CharacterSkillPromptBuilder.BuildPublicMaterial(first.Context, subject.Actor.Progression));
        Reject<ObjectDisposedException>(() => CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(
            first.Context, subject.Actor.Progression, Array.Empty<string>()));
        Reject<ObjectDisposedException>(() => CustomerPersonaPromptBuilder.BuildPublicMaterial(first.Context, subject.Actor));
        Require(JsonUtility.ToJson(a.CaptureSnapshot()) == frozenA, "Disposal changed an already-frozen request.");
        Require(JsonUtility.ToJson(CharacterSkillPromptBuilder.BuildPublicMaterial(second.Context,
            subject.Actor.Progression).CaptureSnapshot()) == frozenB, "Disposing one root damaged another root.");
        first.Context.Dispose(); // Idempotent even if a retained caller disposes again.
        using var recreated = new AuthorityScope(subject.Actor, "Human", 12, worldA.Actor, lifetimeA.Actor);
        Require(JsonUtility.ToJson(CharacterSkillPromptBuilder.BuildPublicMaterial(recreated.Context,
            subject.Actor.Progression).CaptureSnapshot()) == frozenA, "New session inherited stale context.");

        // Resolve the production composition and exercise the same public material path.
        var live = gameplay.Resolve<NarrativeRequestContextBuilder>();
        Require(ReferenceEquals(live, gameplay.Resolve<NarrativeRequestContextBuilder>()), "Gameplay context registration is unstable.");
        var actor = gameplay.Resolve<ICharacterWorldQuery>().Characters.FirstOrDefault(value =>
            value?.Identity?.Data != null && value.Progression != null) ?? subject.Actor;
        NarrativePublicContextMaterial liveMaterial = CharacterSkillPromptBuilder.BuildPublicMaterial(live, actor.Progression);
        Require(liveMaterial.SubjectId == actor.Identity.PersistentId, "Gameplay context used a different subject.");
        var expected = new NarrativeRequestContextBuilder(gameplay.Resolve<ICharacterNarrativeQuery>(),
            gameplay.Resolve<ICharacterLifeQuery>(), gameplay.Resolve<ICharacterNarrativeCatalog>(),
            gameplay.Resolve<ICharacterWorldQuery>(), gameplay.Resolve<ICharacterLifetimeQuery>());
        using (expected)
            Require(CharacterSkillPromptBuilder.BuildPublicMaterial(expected, actor.Progression).SemanticHash
                == liveMaterial.SemanticHash, "Gameplay context is not connected to its own five authorities.");
        var persona = CustomerPersonaPromptBuilder.BuildPublicMaterial(live, actor);
        Require(CustomerPersonaPromptBuilder.BuildEnvelope(actor, persona).Material.SemanticHash == persona.SemanticHash,
            "Persona envelope changed its owned public context.");
        WriteNew("narrative-context-gameplay-material.json", JsonUtility.ToJson(liveMaterial.CaptureSnapshot(), true));
        WriteNew("narrative-context-fixture-a.json", frozenA);
        WriteNew("narrative-context-fixture-b.json", frozenB);
        WriteNew("narrative-context-actor-origin.txt", ReferenceEquals(actor, subject.Actor)
            ? "Controlled actor through actual GameplayScene context service."
            : "Existing GameplayScene actor through actual GameplayScene context service.");
    }

    private static void RequireAuthorityFacts(NarrativePublicContextMaterial material, AuthorityScope scope,
        string age, string world, string lifetime)
    {
        string text = string.Join("\n", material.PublicFacts.Select(value => value.Text));
        Require(text.Contains("Chronological age: " + age + " years"), "Life authority crossed containers.");
        Require(text.Contains(scope.Catalog.Require(scope.Narrative.CultureId).DisplayName), "Narrative/catalog authority crossed containers.");
        Require(text.Contains("target: " + world) && text.Contains("target: " + lifetime), "World/lifetime authority crossed containers.");
    }

    private sealed class AuthorityScope : IDisposable
    {
        internal readonly IObjectResolver Container;
        internal readonly NarrativeRequestContextBuilder Context;
        internal readonly CharacterNarrativeRuntime Narratives;
        internal readonly CharacterLifeRuntime Life;
        internal readonly CharacterNarrativeCatalog Catalog;
        internal readonly CharacterNarrativeSnapshot Narrative;
        internal readonly WorldView World;
        private bool disposed;
        internal AuthorityScope(CharacterActor subject, string species, int years, CharacterActor world, CharacterActor lifetime)
        {
            var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
            var roots = new DungeonRuntimeAggregateRootStore();
            Catalog = new CharacterNarrativeCatalog(content);
            Narratives = new CharacterNarrativeRuntime(roots, Catalog);
            Life = new CharacterLifeRuntime(roots, new ResourceCharacterSpeciesCatalog(content), new RandomStreamProvider(7431));
            CharacterId id = subject.BuildingCharacterId;
            var speciesId = new CharacterSpeciesId(species);
            Narrative = Narratives.Register(id, speciesId, Array.Empty<string>(), Array.Empty<string>());
            Life.Register(id, speciesId, years * GameCalendarRules.DaysPerYear, years * GameCalendarRules.DaysPerYear, 1);
            World = new WorldView(world, lifetime);
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ICharacterNarrativeQuery>(Narratives);
            builder.RegisterInstance<ICharacterLifeQuery>(Life);
            builder.RegisterInstance<ICharacterNarrativeCatalog>(Catalog);
            builder.RegisterInstance<ICharacterWorldQuery>(World);
            builder.RegisterInstance<ICharacterLifetimeQuery>(World);
            builder.Register<NarrativeRequestContextBuilder>(Lifetime.Singleton);
            Container = builder.Build();
            Context = Container.Resolve<NarrativeRequestContextBuilder>();
        }
        public void Dispose() { if (disposed) return; disposed = true; Container.Dispose(); }
    }

    private sealed class WorldView : ICharacterWorldQuery, ICharacterLifetimeQuery
    {
        internal WorldView(CharacterActor world, CharacterActor lifetime)
        { Characters = new[] { world }; AllCharacters = new[] { lifetime }; }
        public int CharacterVersion => 1;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
        public IReadOnlyList<CharacterActor> AllCharacters { get; }
    }

    private sealed class ActorFixture : IDisposable
    {
        internal readonly CharacterActor Actor;
        private readonly CharacterSO data;
        internal ActorFixture(string id, string name)
        {
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(CharacterType.NPC, name, "human");
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();
            var gameObject = new GameObject("NarrativeContext_" + name);
            gameObject.SetActive(false);
            gameObject.AddComponent<SpriteRenderer>();
            Actor = gameObject.AddComponent<CharacterActor>();
            gameObject.AddComponent<AbilityMove>();
            gameObject.AddComponent<AbilityWork>();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(gameObject);
            Actor.RefreshAbilityCache();
            Actor.Identity.SetPersistentId(id);
            Actor.Initialization(data);
        }
        public void Dispose()
        {
            if (Actor != null) UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static string EvidencePath(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-logFile");
        Require(index >= 0 && index + 1 < args.Length && Path.IsPathRooted(args[index + 1]), "Explicit log path required.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        Require(directory.StartsWith(Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Evidence must stay inside project QA.");
        return Path.Combine(directory, name);
    }
    private static void WriteNew(string name, string text)
    { using var writer = new StreamWriter(new FileStream(EvidencePath(name), FileMode.CreateNew)); writer.Write(text); }
    private static void Finish(int code)
    {
        (preparationVerification as IDisposable)?.Dispose();
        preparationVerification = null;
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Pump;
        GameWorldLineageDebugScenarios.Finish(code);
    }
}
#endif
