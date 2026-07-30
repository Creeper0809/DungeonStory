using System;
using DamageNumbersPro;
using UnityEngine;

public interface IGameDataProvider
{
    bool TryGetGameData(out GameData gameData);
}

public interface IGameMoneyRuntime
{
    int Balance { get; }
    bool CanSpend(int amount);
    bool TrySpend(int amount, out string reason);
    bool TrySpend(
        int amount,
        EconomyTransactionContext context,
        out string reason);
    void Add(int amount);
    void Add(int amount, EconomyTransactionContext context);
}

public interface IFloatingNumberFeedbackService
{
    bool TryShow(NumberCondition condition, Vector3 worldPosition, float value);
}

public sealed class GameManagerGameDataProvider : IGameDataProvider
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public GameManagerGameDataProvider(DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public bool TryGetGameData(out GameData gameData)
    {
        GameManager gameManager = sceneReferences.GameManager;
        gameData = gameManager != null ? gameManager.gameData : null;
        return gameData != null;
    }
}

public sealed class GameMoneyRuntime : IGameMoneyRuntime
{
    private readonly IGameDataProvider gameDataProvider;
    private readonly IEconomyTransactionLedger transactionLedger;

    public GameMoneyRuntime(
        IGameDataProvider gameDataProvider,
        IEconomyTransactionLedger transactionLedger)
    {
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.transactionLedger = transactionLedger
            ?? throw new ArgumentNullException(nameof(transactionLedger));
    }

    public int Balance => TryGetMoney(out Data<int> money)
        ? Mathf.Max(0, money.Value)
        : 0;

    public bool CanSpend(int amount)
    {
        int cost = Mathf.Max(0, amount);
        return cost == 0 || Balance >= cost;
    }

    public bool TrySpend(int amount, out string reason)
    {
        return TrySpend(
            amount,
            new EconomyTransactionContext(
                EconomyTransactionKind.LegacyExpense,
                nameof(GameMoneyRuntime),
                description: "기존 지출"),
            out reason);
    }

    public bool TrySpend(
        int amount,
        EconomyTransactionContext context,
        out string reason)
    {
        int cost = Mathf.Max(0, amount);
        if (cost == 0)
        {
            reason = string.Empty;
            return true;
        }

        if (!TryGetMoney(out Data<int> money))
        {
            reason = "골드 보유 정보를 찾을 수 없습니다.";
            transactionLedger.RecordFailure(context, cost, reason, Balance);
            return false;
        }

        if (money.Value < cost)
        {
            reason = "골드가 부족합니다.";
            transactionLedger.RecordFailure(context, cost, reason, money.Value);
            return false;
        }

        using (transactionLedger.Begin(context))
        {
            money.Value -= cost;
        }

        reason = string.Empty;
        return true;
    }

    public void Add(int amount)
    {
        Add(
            amount,
            new EconomyTransactionContext(
                EconomyTransactionKind.LegacyIncome,
                nameof(GameMoneyRuntime),
                description: "기존 수입"));
    }

    public void Add(int amount, EconomyTransactionContext context)
    {
        int gain = Mathf.Max(0, amount);
        if (gain > 0 && TryGetMoney(out Data<int> money))
        {
            using (transactionLedger.Begin(context))
            {
                money.Value += gain;
            }
        }
    }

    private bool TryGetMoney(out Data<int> money)
    {
        money = null;
        if (!gameDataProvider.TryGetGameData(out GameData gameData)
            || gameData == null)
        {
            return false;
        }

        gameData.holdingMoney ??= new Data<int>();
        money = gameData.holdingMoney;
        return true;
    }
}

public sealed class GameManagerFloatingNumberFeedbackService :
    IFloatingNumberFeedbackService
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public GameManagerFloatingNumberFeedbackService(
        DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public bool TryShow(
        NumberCondition condition,
        Vector3 worldPosition,
        float value)
    {
        GameManager manager = ResolveGameManager();
        if (manager.numbers == null
            || !manager.numbers.TryGetValue(condition, out DamageNumber number)
            || number == null)
        {
            return false;
        }

        number.Spawn(worldPosition, value);
        return true;
    }

    private GameManager ResolveGameManager()
    {
        GameManager gameManager = sceneReferences.GameManager;
        return gameManager != null
            ? gameManager
            : throw new InvalidOperationException(
                $"{nameof(IFloatingNumberFeedbackService)} requires a registered {nameof(GameManager)}.");
    }
}
