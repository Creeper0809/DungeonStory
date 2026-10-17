#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>Checks the live stats consumers against the existing disease authority.</summary>
public static class WimDiseasePerformancePlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-005-disease-performance.txt";
    private const string PendingKey = "DungeonStory.WIM005.Pending";
    private static double deadline;

    [MenuItem("DungeonStory/QA/WIM/005 Disease Performance")]
    public static void RequestRun()
    {
        // A deferred editor request can arrive after the explicit request has
        // already started this same run. It must not schedule another run.
        if (SessionState.GetBool(PendingKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start this isolated test from Edit Mode.");
        WriteReport("RUNNING\n");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 30d;
            EditorApplication.update -= TryRun;
            EditorApplication.update += TryRun;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= TryRun;
            SessionState.SetBool(PendingKey, false);
            WriteReport("FAIL\nRun interrupted before completion.\n");
        }
    }

    private static void TryRun()
    {
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(value => value.Container != null);
        CharacterActor actor = UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value.Stats != null
                && value.CurrentLifecycleState == CharacterLifecycleState.Active
                && !value.IsDead
                && CharacterPersistentIdentity.TryGet(value, out _));
        if (scope?.Container == null && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= TryRun;
        GameObject fixtureObject = null;
        CharacterSO fixtureData = null;
        try
        {
            Require(scope?.Container != null,
                "Main gameplay runtime container unavailable within 30 seconds.");
            // Opening GameplayScene directly need not start a prepared party.
            // Use the project's existing explicit test-actor fixture, with the
            // live container injected, instead of treating an empty party as a failure.
            if (actor == null)
            {
                fixtureObject = CharacterAiPlanDebugFixtures.CreateActorObject("WIM005 disease test actor");
                scope.Container.InjectGameObject(fixtureObject);
                fixtureData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                    CharacterType.NPC, "WIM005 disease test actor", "Orc");
                actor = fixtureObject.GetComponent<CharacterActor>();
                actor.EnsureRuntimeState();
                actor.Identity.SetPersistentId(new GuidPersistentIdGenerator().NewCharacterId());
                scope.Container.Resolve<ICharacterNarrativeCommand>().Register(
                    new CharacterId(actor.Identity.PersistentId),
                    new CharacterSpeciesId(fixtureData.speciesTag),
                    Array.Empty<string>(), Array.Empty<string>(),
                    BuiltInCharacterProficiencyIds.All.Select(id => new CharacterStartingProficiencyExperience
                    {
                        proficiencyId = id.Value,
                        experience = 100,
                        learningMultiplier = 1f
                    }).ToArray());
                actor.RefreshAbilityCache();
                actor.Initialize(fixtureData);
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }
            string report = Verify(scope, actor);
            WriteReport(report);
            Debug.Log("WIM-005 disease performance PASS: " + ReportPath);
        }
        catch (Exception exception)
        {
            WriteReport("FAIL\n" + exception + "\n");
            Debug.LogException(exception);
        }
        finally
        {
            if (fixtureObject != null) UnityEngine.Object.DestroyImmediate(fixtureObject);
            if (fixtureData != null) UnityEngine.Object.DestroyImmediate(fixtureData);
            SessionState.SetBool(PendingKey, false);
            EditorApplication.ExitPlaymode();
        }
    }

    private static string Verify(DungeonRuntimeLifetimeScope scope, CharacterActor actor)
    {
        IPopulationHealthPersistence persistence = scope.Container.Resolve<IPopulationHealthPersistence>();
        IDiseaseSymptomEffectQuery symptoms = scope.Container.Resolve<IDiseaseSymptomEffectQuery>();
        IDiseaseDefinitionCatalog catalog = scope.Container.Resolve<IDiseaseDefinitionCatalog>();
        ICharacterBodyHealthPersistence body = scope.Container.Resolve<ICharacterBodyHealthPersistence>();
        PopulationHealthWorldSaveData original = persistence.Capture();
        string originalJson = JsonUtility.ToJson(original);
        string bodyBefore = JsonUtility.ToJson(body.Capture());
        CharacterId actorId = new(actor.Identity.PersistentId);
        List<string> checks = new();
        try
        {
            PopulationHealthWorldSaveData healthy = Clone(original);
            CharacterPopulationHealthSaveData subject = FindOrAdd(healthy, actorId.Value);
            subject.activeDiseases.Clear();
            Publish(healthy);
            float healthyWork = actor.Stats.GetWorkContextMultiplier(BuiltInWorkTypeIds.Clean);
            float healthyFinalWork = actor.Stats.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Clean);
            float healthyMove = actor.Stats.GetMoveSpeed();
            Require(healthyWork > 0f && healthyFinalWork > 0f && healthyMove > 0f,
                "Baseline actor must be able to work and move.");
            Near(symptoms.GetWorkSpeedMultiplier(actorId), 1f, "healthy work");
            Near(symptoms.GetMoveSpeedMultiplier(actorId), 1f, "healthy movement");
            int day = healthy.currentAbsoluteDay;
            DiseaseDefinition[] definitions = catalog.Definitions
                .OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
            Require(definitions.Length > 0, "Disease catalog is empty.");
            foreach (DiseaseDefinition disease in definitions)
            {
                PopulationHealthWorldSaveData active = Clone(healthy);
                FindOrAdd(active, actorId.Value).activeDiseases.Add(
                    Active(disease, day, day + 2));
                Publish(active);
                VerifyCurrent(disease.Id);
                string captured = JsonUtility.ToJson(persistence.Capture());
                Publish(Clone(persistence.Capture()));
                Require(captured == JsonUtility.ToJson(persistence.Capture()),
                    disease.Id + " save round-trip changed disease state.");
                VerifyCurrent(disease.Id + ":restored");

                PopulationHealthWorldSaveData latent = Clone(active);
                FindOrAdd(latent, actorId.Value).activeDiseases[0].symptomDay = day + 1;
                Publish(latent);
                VerifyNeutral(disease.Id + ":incubating");

                PopulationHealthWorldSaveData recovered = Clone(active);
                FindOrAdd(recovered, actorId.Value).activeDiseases[0].recoveryDay = day;
                Publish(recovered);
                VerifyNeutral(disease.Id + ":recovery-boundary");
            }

            PopulationHealthWorldSaveData combined = Clone(healthy);
            FindOrAdd(combined, actorId.Value).activeDiseases.AddRange(
                definitions.Select(value => Active(value, day, day + 2)));
            Publish(combined);
            VerifyCurrent("combined");
            CharacterId unrelated = new("character:wim005:unregistered");
            Near(symptoms.GetWorkSpeedMultiplier(unrelated), 1f, "unrelated work");
            Near(symptoms.GetMoveSpeedMultiplier(unrelated), 1f, "unrelated movement");
            Require(bodyBefore == JsonUtility.ToJson(body.Capture()),
                "Direct symptom projection unexpectedly changed body authority.");
            checks.Add("body-authority-unchanged=PASS");
            checks.Add("definition-count=" + definitions.Length);
            checks.Add("consumers=CharacterStats.GetWorkContextMultiplier/GetWorkSpeedMultiplier/GetMoveSpeed");

            void Publish(PopulationHealthWorldSaveData payload) =>
                persistence.PublishRestore(persistence.PrepareRestore(payload));

            void VerifyNeutral(string label)
            {
                Near(symptoms.GetWorkSpeedMultiplier(actorId), 1f, label + " query work");
                Near(symptoms.GetMoveSpeedMultiplier(actorId), 1f, label + " query move");
                VerifyCurrent(label);
            }

            void VerifyCurrent(string label)
            {
                float work = symptoms.GetWorkSpeedMultiplier(actorId);
                float movement = symptoms.GetMoveSpeedMultiplier(actorId);
                Near(actor.Stats.GetWorkContextMultiplier(BuiltInWorkTypeIds.Clean),
                    healthyWork * work, label + " work context once");
                Near(actor.Stats.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Clean),
                    healthyFinalWork * work, label + " final work once");
                Near(actor.Stats.GetMoveSpeed(), healthyMove * movement,
                    label + " final movement once");
                checks.Add(label + " work=" + work.ToString("R", CultureInfo.InvariantCulture)
                    + " move=" + movement.ToString("R", CultureInfo.InvariantCulture) + " PASS");
            }
        }
        finally
        {
            persistence.PublishRestore(persistence.PrepareRestore(original));
            Require(originalJson == JsonUtility.ToJson(persistence.Capture()),
                "Test cleanup did not restore original population health.");
        }
        return "PASS\n" + string.Join("\n", checks) + "\nrestore-original=PASS\n";
    }

    private static ActiveDiseaseSaveData Active(DiseaseDefinition disease, int day, int recovery) => new()
    {
        diseaseId = disease.Id,
        infectionDay = day,
        symptomDay = day,
        recoveryDay = recovery,
        severity = disease.BaseSeverity,
        diagnosed = true
    };

    private static PopulationHealthWorldSaveData Clone(PopulationHealthWorldSaveData value) =>
        JsonUtility.FromJson<PopulationHealthWorldSaveData>(JsonUtility.ToJson(value));

    private static CharacterPopulationHealthSaveData FindOrAdd(PopulationHealthWorldSaveData world, string id)
    {
        CharacterPopulationHealthSaveData found = world.characters.FirstOrDefault(value => value.characterId == id);
        if (found != null) return found;
        found = new CharacterPopulationHealthSaveData { characterId = id };
        world.characters.Add(found);
        return found;
    }

    private static void Near(float actual, float expected, string reason) =>
        Require(!float.IsNaN(actual) && !float.IsInfinity(actual)
            && Math.Abs(actual - expected) <= Math.Max(0.00001f, Math.Abs(expected) * 0.0001f),
            reason + ": actual=" + actual + " expected=" + expected);

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static void WriteReport(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, content, new System.Text.UTF8Encoding(false));
    }
}
#endif
