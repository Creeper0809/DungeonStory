#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using VContainer;
using VContainer.Unity;

/// <summary>Exercises the generated health button against the live diet authority.</summary>
public static class WimMealQualityPlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-035-ui-playmode.txt";
    private const string PendingKey = "DungeonStory.WIM035.Pending";
    private static double deadline;

    [MenuItem("DungeonStory/QA/WIM/035 Meal Quality UI")]
    public static void RequestRun()
    {
        if (SessionState.GetBool(PendingKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the isolated UI check from Edit Mode.");
        Write("RUNNING\n");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnState;
        EditorApplication.playModeStateChanged += OnState;
    }

    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 30d;
            EditorApplication.update -= Verify;
            EditorApplication.update += Verify;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Verify;
            SessionState.SetBool(PendingKey, false);
            Write("FAIL\nInterrupted before verification.\n");
        }
    }

    private static void Verify()
    {
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value.Container != null);
        CharacterSummaryInfo view = UnityEngine.Object.FindObjectsByType<CharacterSummaryInfo>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if ((scope == null || view == null) && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Verify;
        GameObject actorObject = null;
        CharacterSO data = null;
        try
        {
            Require(scope != null && view != null, "Gameplay container or character summary UI unavailable.");
            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject("WIM035 meal policy test actor");
            scope.Container.InjectGameObject(actorObject);
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC, "WIM035 meal policy test actor", "Orc");
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(new GuidPersistentIdGenerator().NewCharacterId());
            scope.Container.Resolve<ICharacterNarrativeCommand>().Register(
                new CharacterId(actor.Identity.PersistentId), new CharacterSpeciesId(data.speciesTag),
                Array.Empty<string>(), Array.Empty<string>(),
                BuiltInCharacterProficiencyIds.All.Select(id => new CharacterStartingProficiencyExperience
                {
                    proficiencyId = id.Value, experience = 100, learningMultiplier = 1f
                }).ToArray());
            actor.RefreshAbilityCache();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            ICharacterConsumablesQuery query = scope.Container.Resolve<ICharacterConsumablesQuery>();
            Require(query.GetMealQualityLimit(actor) == CharacterMealQualityLimit.Inherit, "Initial policy changed.");
            view.OnTriggerEvent(new InfoFeedEvent(actor));
            view.ShowHealthTab();
            Transform control = view.UI.transform.Find(
                "CharacterSummaryGeneratedView/Content/HealthContent/SubstanceCommandRow/MealQuality");
            Button button = control != null ? control.GetComponent<Button>() : null;
            Require(button != null, "Generated view has no meal quality button.");
            Require(EventSystem.current != null && button.IsActive() && button.IsInteractable(),
                "Meal quality control is not reachable through the active EventSystem.");
            CharacterMealQualityLimit[] sequence =
            {
                CharacterMealQualityLimit.Poor, CharacterMealQualityLimit.Simple,
                CharacterMealQualityLimit.Decent, CharacterMealQualityLimit.Fine,
                CharacterMealQualityLimit.Lavish, CharacterMealQualityLimit.Inherit
            };
            foreach (CharacterMealQualityLimit expected in sequence)
            {
                ExecuteEvents.Execute(button.gameObject,
                    new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                    ExecuteEvents.pointerClickHandler);
                Require(query.GetMealQualityLimit(actor) == expected, "UI command did not reach policy: " + expected);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                string expectedText = CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Button.MealQuality",
                    CharacterSummaryHealthStatusTextFormatter.MealQualityLimit(expected));
                Require(label != null && label.text == expectedText, "Policy label stale: " + expected);
            }
            Require(query.GetPolicy(actor) == CharacterDietPolicyKind.Free, "Quality button changed ingredient diet.");
            view.RequestClose();
            Write("PASS\nmain-gameplay-generated-button=PASS\nevent-system-pointer-click=PASS\nall-six-policy-values=PASS\nlocalized-label=PASS\ningredient-diet-unchanged=PASS\n");
            Debug.Log("WIM-035 meal policy UI PASS: " + ReportPath);
        }
        catch (Exception exception)
        {
            Write("FAIL\n" + exception + "\n");
            Debug.LogException(exception);
        }
        finally
        {
            if (actorObject != null) UnityEngine.Object.DestroyImmediate(actorObject);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            SessionState.SetBool(PendingKey, false);
            EditorApplication.ExitPlaymode();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Write(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, text, new System.Text.UTF8Encoding(false));
    }
}
#endif
