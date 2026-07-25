using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SampleSceneRationRuntime : IStartable, ITickable
{
    public const string SupportedSceneName = DungeonSceneNavigator.DebugSampleSceneName;
    public const string RationDestinationId = "debug:sample-scene-rations";
    public const int TargetStockPerCategory = 100;
    public const float NeedThreshold = 15f;
    public const float FoodRecovery = 70f;
    public const float WaterRecovery = 80f;

    private const float CheckIntervalSeconds = 0.5f;

    private readonly ICharacterWorldQuery characterWorld;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IGameClock gameClock;
    private float nextCheckAt;

    public SampleSceneRationRuntime(
        ICharacterWorldQuery characterWorld,
        IWorldItemStackRuntime itemStackRuntime,
        IGameClock gameClock)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.itemStackRuntime = itemStackRuntime ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public int IssuedFoodRations { get; private set; }
    public int IssuedWaterRations { get; private set; }

    public static bool SupportsScene(string sceneName)
    {
        return string.Equals(sceneName, SupportedSceneName, StringComparison.Ordinal);
    }

    public static bool ShouldIssueRation(float needValue)
    {
        return needValue < NeedThreshold;
    }

    public void Start()
    {
        nextCheckAt = 0f;
        ReplenishNow();
    }

    public void Tick()
    {
        if (gameClock.Time < nextCheckAt)
        {
            return;
        }

        nextCheckAt = gameClock.Time + CheckIntervalSeconds;
        ReplenishNow();
    }

    public void ReplenishNow()
    {
        EnsureRationStock(StockCategory.Food);
        EnsureRationStock(StockCategory.Water);

        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (!IsEligible(actor))
            {
                continue;
            }

            TryIssueRation(actor, CharacterCondition.HUNGER, StockCategory.Food, FoodRecovery);
            TryIssueRation(actor, CharacterCondition.THIRST, StockCategory.Water, WaterRecovery);
        }
    }

    private void EnsureRationStock(StockCategory category)
    {
        string itemId = DungeonItemCatalogSO.StockItemId(category);
        int current = itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.ItemId == itemId
                && stack.DestinationId == RationDestinationId)
            .Sum(stack => stack.Quantity);
        int missing = Mathf.Max(0, TargetStockPerCategory - current);
        if (missing <= 0)
        {
            return;
        }

        itemStackRuntime.SpawnStockAtDropoff(
            category,
            missing,
            "테스트 배급",
            WorldItemStackState.Stored,
            RationDestinationId,
            out _);
    }

    private void TryIssueRation(
        CharacterActor actor,
        CharacterCondition condition,
        StockCategory category,
        float recovery)
    {
        if (actor.Stats == null
            || !actor.Stats.Stats.TryGetValue(condition, out float needValue)
            || !ShouldIssueRation(needValue))
        {
            return;
        }

        string itemId = DungeonItemCatalogSO.StockItemId(category);
        WorldItemStackSnapshot ration = itemStackRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && stack.Quantity > 0
                && !stack.Forbidden
                && !stack.IsReserved
                && stack.ItemId == itemId
                && stack.DestinationId == RationDestinationId);
        if (ration == null
            || !itemStackRuntime.TryConsumeStackQuantity(ration.StackId, 1, out _))
        {
            return;
        }

        actor.ChangesStat(condition, recovery);
        if (category == StockCategory.Food)
        {
            IssuedFoodRations++;
        }
        else if (category == StockCategory.Water)
        {
            IssuedWaterRations++;
        }
    }

    private static bool IsEligible(CharacterActor actor)
    {
        return actor != null
            && actor.gameObject.scene.IsValid()
            && SupportsScene(actor.gameObject.scene.name)
            && !actor.IsDead
            && actor.characterType != CharacterType.Intruder
            && actor.CurrentLifecycleState != CharacterLifecycleState.OnExpedition
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned;
    }
}
