using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public static class SampleSceneRationDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Survival/Run Sample Scene Ration Scenarios")]
    public static void RunFromMenu()
    {
        Debug.Log(RunAll());
    }

    public static bool RunAll()
    {
        try
        {
            Require(SampleSceneRationRuntime.SupportsScene("SampleScene"),
                "SampleScene must receive debug rations.");
            Require(!SampleSceneRationRuntime.SupportsScene("GameplayScene"),
                "GameplayScene must not receive debug rations.");
            Require(!SampleSceneRationRuntime.SupportsScene("CharacterAiTestScene"),
                "Specialized AI tests must not receive SampleScene rations.");
            Require(SampleSceneRationRuntime.ShouldIssueRation(14.99f),
                "A need below the safety threshold must receive a ration.");
            Require(!SampleSceneRationRuntime.ShouldIssueRation(15f),
                "A need at the safety threshold must not consume a ration.");
            Require(SampleSceneRationRuntime.TargetStockPerCategory > 0
                && SampleSceneRationRuntime.FoodRecovery > 0f
                && SampleSceneRationRuntime.WaterRecovery > 0f,
                "Ration stock and recovery values must be positive.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    public static string RunPlayModeProbe()
    {
        GameObject temporaryActorObject = null;
        CharacterSO temporaryActorData = null;
        try
        {
            Require(EditorApplication.isPlaying, "The ration probe requires PlayMode.");

            Scene activeScene = SceneManager.GetActiveScene();
            Require(SampleSceneRationRuntime.SupportsScene(activeScene.name),
                $"The active scene must be {SampleSceneRationRuntime.SupportedSceneName}.");

            DungeonRuntimeLifetimeScope scope = UnityEngine.Object
                .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.scene == activeScene);
            Require(scope != null && scope.Container != null,
                "The SampleScene runtime scope is missing.");

            SampleSceneRationRuntime rationRuntime =
                scope.Container.Resolve<SampleSceneRationRuntime>();
            IWorldItemStackRuntime itemRuntime =
                scope.Container.Resolve<IWorldItemStackRuntime>();
            CharacterActor actor = UnityEngine.Object
                .FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.scene == activeScene
                    && candidate.Stats != null
                    && !candidate.IsDead
                    && candidate.characterType != CharacterType.Intruder);
            if (actor == null)
            {
                temporaryActorObject = new GameObject("Sample Scene Ration Probe Character");
                temporaryActorObject.AddComponent<SpriteRenderer>();
                actor = temporaryActorObject.AddComponent<CharacterActor>();
                temporaryActorData = ScriptableObject.CreateInstance<CharacterSO>();
                temporaryActorData.characterName = "Ration Probe";
                temporaryActorData.characterType = CharacterType.NPC;
                temporaryActorData.baseStats = CharacterStatBlock.CreateDefault();
                actor.Initialize(temporaryActorData);
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }

            Require(rationRuntime != null, "The SampleScene ration runtime is missing.");
            Require(itemRuntime != null, "The physical item runtime is missing.");
            Require(actor != null, "No eligible SampleScene character was found.");

            rationRuntime.ReplenishNow();
            actor.Heal(actor.MaxHealth);
            actor.ChangesStat(CharacterCondition.HUNGER, -200f);
            actor.ChangesStat(CharacterCondition.THIRST, -200f);

            float healthBefore = actor.CurrentHealth;
            int foodIssuedBefore = rationRuntime.IssuedFoodRations;
            int waterIssuedBefore = rationRuntime.IssuedWaterRations;

            rationRuntime.ReplenishNow();

            float hunger = actor.Stats.Stats[CharacterCondition.HUNGER];
            float thirst = actor.Stats.Stats[CharacterCondition.THIRST];
            int foodStock = CountRationStock(itemRuntime, StockCategory.Food);
            int waterStock = CountRationStock(itemRuntime, StockCategory.Water);

            Require(hunger >= SampleSceneRationRuntime.FoodRecovery,
                $"Food ration did not recover hunger: {hunger:0.0}.");
            Require(thirst >= SampleSceneRationRuntime.WaterRecovery,
                $"Water ration did not recover thirst: {thirst:0.0}.");
            Require(Mathf.Approximately(actor.CurrentHealth, healthBefore),
                "Issuing a ration changed character health.");
            Require(rationRuntime.IssuedFoodRations == foodIssuedBefore + 1,
                "Exactly one food ration must be issued.");
            Require(rationRuntime.IssuedWaterRations == waterIssuedBefore + 1,
                "Exactly one water ration must be issued.");
            Require(foodStock >= SampleSceneRationRuntime.TargetStockPerCategory - 1,
                $"Physical food ration stock is too low: {foodStock}.");
            Require(waterStock >= SampleSceneRationRuntime.TargetStockPerCategory - 1,
                $"Physical water ration stock is too low: {waterStock}.");

            string report = "PASS"
                + $"; actor={actor.name}"
                + $"; hunger={hunger:0.0}"
                + $"; thirst={thirst:0.0}"
                + $"; health={actor.CurrentHealth:0.0}/{actor.MaxHealth:0.0}"
                + $"; stock={foodStock} food/{waterStock} water"
                + $"; issued={rationRuntime.IssuedFoodRations} food/"
                + $"{rationRuntime.IssuedWaterRations} water";
            DestroyTemporaryActor(temporaryActorObject, temporaryActorData);
            return report;
        }
        catch (Exception exception)
        {
            DestroyTemporaryActor(temporaryActorObject, temporaryActorData);
            Debug.LogException(exception);
            return $"FAIL; {exception.Message}";
        }
    }

    private static int CountRationStock(
        IWorldItemStackRuntime itemRuntime,
        StockCategory category)
    {
        string itemId = DungeonItemCatalogSO.StockItemId(category);
        return itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.ItemId == itemId
                && stack.DestinationId == SampleSceneRationRuntime.RationDestinationId)
            .Sum(stack => stack.Quantity);
    }

    private static void DestroyTemporaryActor(
        GameObject actorObject,
        CharacterSO actorData)
    {
        if (actorObject != null)
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }

        if (actorData != null)
        {
            UnityEngine.Object.DestroyImmediate(actorData);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
