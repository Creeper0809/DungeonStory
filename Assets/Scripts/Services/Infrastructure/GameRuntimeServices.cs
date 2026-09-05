using System;
using DamageNumbersPro;
using DungeonStory.Foundation;
using UnityEngine;

public interface IGameSessionStateProvider
{
    bool TryGetSessionState(out GameSessionState gameData);
}

public interface IGameSessionStateStore : IGameSessionStateProvider
{
    void Restore(GameSessionSnapshot snapshot);
}

public interface IGameSessionPersistence
{
    GameSessionSaveData CaptureSession();
    GameSessionSnapshot PrepareSessionRestore(GameSessionSaveData data);
    void StageSessionRestore(GameSessionSnapshot candidate);
}

public interface IGameSessionPauseAuthority
{
    void SetPaused(bool paused);
}

public interface IGameMoneyAccount
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
    void SetBalance(int amount, EconomyTransactionContext context);
}

public interface IIdempotentGameMoneyAccount : IGameMoneyAccount
{
    bool TrySpendOnce(
        int amount,
        EconomyTransactionContext context,
        out EconomyTransactionRecord receipt,
        out string reason);
    bool TryCreditOnce(
        int amount,
        EconomyTransactionContext context,
        out string reason);
}

public interface IFloatingNumberFeedbackService
{
    bool TryShow(NumberCondition condition, Vector3 worldPosition, float value);
}

public sealed class ScopedGameSessionStateStore :
    IGameSessionStateStore,
    IGameSessionPauseAuthority,
    IGameSessionPersistence,
    IDungeonRestoreTransactionParticipant
{
    private readonly GameSessionState state;
    private readonly IGameSessionStateMutation mutation;
    private readonly IGameTimeScaleController timeScaleController;
    private GameSessionSnapshot? restoreCandidate;
    private GameSessionSnapshot? previousSnapshot;
    private bool published;

    public ScopedGameSessionStateStore(
        DungeonSceneRuntimeReferences sceneReferences,
        IGameTimeScaleController timeScaleController)
    {
        DungeonSceneRuntimeReferences requiredReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
        GameData settings = requiredReferences.GameManager?.Settings
            ?? throw new InvalidOperationException(
                $"{nameof(ScopedGameSessionStateStore)} requires a loaded "
                + $"{nameof(GameManager)} with {nameof(GameData)} settings.");
        state = GameSessionState.Create(
            settings,
            out IGameSessionStateMutation stateMutation);
        mutation = stateMutation;
        this.timeScaleController = timeScaleController
            ?? throw new ArgumentNullException(nameof(timeScaleController));
    }

    public bool TryGetSessionState(out GameSessionState gameData)
    {
        gameData = state;
        return true;
    }

    public void Restore(GameSessionSnapshot snapshot)
    {
        mutation.Restore(snapshot);
        timeScaleController.Scale = snapshot.IsPaused
            ? 0f
            : snapshot.GameSpeed;
    }

    public void SetPaused(bool paused)
    {
        mutation.SetPaused(paused);
    }

    public GameSessionSaveData CaptureSession() =>
        GameSessionSaveData.From(state.Capture());

    public GameSessionSnapshot PrepareSessionRestore(GameSessionSaveData data) =>
        (data ?? throw new ArgumentNullException(nameof(data))).ToSnapshot();

    public void StageSessionRestore(GameSessionSnapshot candidate)
    {
        if (!previousSnapshot.HasValue || restoreCandidate.HasValue)
            throw new InvalidOperationException(
                "Foundation session restore candidate is not in an empty active transaction.");
        restoreCandidate = candidate;
    }

    public string ParticipantId => "foundation.session";

    public void BeginRestoreCandidate()
    {
        if (previousSnapshot.HasValue || restoreCandidate.HasValue)
            throw new InvalidOperationException("Foundation session restore transaction is already active.");
        previousSnapshot = state.Capture();
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreCandidate.HasValue)
            throw new InvalidOperationException("Foundation session restore candidate is missing.");
        Restore(restoreCandidate.Value);
        published = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (published && previousSnapshot.HasValue) Restore(previousSnapshot.Value);
        DiscardRestoreCandidate();
    }

    public void CompleteRestoreCandidate() => DiscardRestoreCandidate();

    public void DiscardRestoreCandidate()
    {
        restoreCandidate = null;
        previousSnapshot = null;
        published = false;
    }
}

public sealed class GameMoneyAccount : IIdempotentGameMoneyAccount
{
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IEconomyTransactionLedger transactionLedger;

    public GameMoneyAccount(
        IGameSessionStateProvider gameDataProvider,
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
                nameof(GameMoneyAccount),
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

        int balanceBefore = money.Value;
        money.Value = balanceBefore - cost;
        transactionLedger.RecordSuccess(
            context,
            -cost,
            balanceBefore,
            money.Value);

        reason = string.Empty;
        return true;
    }

    public void Add(int amount)
    {
        Add(
            amount,
            new EconomyTransactionContext(
                EconomyTransactionKind.LegacyIncome,
                nameof(GameMoneyAccount),
                description: "기존 수입"));
    }

    public void Add(int amount, EconomyTransactionContext context)
    {
        int gain = Mathf.Max(0, amount);
        if (gain > 0 && TryGetMoney(out Data<int> money))
        {
            int balanceBefore = money.Value;
            money.Value = balanceBefore + gain;
            transactionLedger.RecordSuccess(
                context,
                gain,
                balanceBefore,
                money.Value);
        }
    }

    public bool TrySpendOnce(
        int amount,
        EconomyTransactionContext context,
        out EconomyTransactionRecord receipt,
        out string reason)
    {
        receipt = null;
        if (amount <= 0)
        {
            reason = "지출액은 0보다 커야 합니다.";
            return false;
        }

        if (transactionLedger.TryGetSuccessfulBySource(
                context.kind,
                context.sourceId,
                out EconomyTransactionRecord existing))
        {
            if (existing.amount == -amount
                && existing.balanceBefore >= amount
                && existing.balanceAfter == existing.balanceBefore - amount
                && string.Equals(
                    existing.targetId,
                    context.targetId,
                    StringComparison.Ordinal))
            {
                receipt = existing.Clone();
                reason = string.Empty;
                return true;
            }

            reason = "동일 작업 ID에 다른 지출 기록이 존재합니다.";
            return false;
        }

        if (!TryGetMoney(out Data<int> money))
        {
            reason = "골드 보유 정보를 찾을 수 없습니다.";
            transactionLedger.RecordFailure(context, amount, reason, Balance);
            return false;
        }
        if (money.Value < amount)
        {
            reason = "골드가 부족합니다.";
            transactionLedger.RecordFailure(context, amount, reason, money.Value);
            return false;
        }

        int balanceBefore = money.Value;
        int balanceAfter = checked(balanceBefore - amount);
        money.Value = balanceAfter;
        try
        {
            transactionLedger.RecordSuccess(
                context,
                -amount,
                balanceBefore,
                balanceAfter);
        }
        catch
        {
            money.Value = balanceBefore;
            throw;
        }

        if (!transactionLedger.TryGetSuccessfulBySource(
                context.kind,
                context.sourceId,
                out EconomyTransactionRecord published)
            || published.amount != -amount
            || published.balanceBefore != balanceBefore
            || published.balanceAfter != balanceAfter
            || !string.Equals(
                published.targetId,
                context.targetId,
                StringComparison.Ordinal))
        {
            money.Value = balanceBefore;
            throw new InvalidOperationException(
                "The money ledger did not preserve its successful debit contract.");
        }

        receipt = published.Clone();
        reason = string.Empty;
        return true;
    }

    public bool TryCreditOnce(
        int amount,
        EconomyTransactionContext context,
        out string reason)
    {
        int gain = Mathf.Max(0, amount);
        if (gain <= 0)
        {
            reason = "입금액은 0보다 커야 합니다.";
            return false;
        }
        if (transactionLedger.TryGetSuccessfulBySource(
                context.kind,
                context.sourceId,
                out EconomyTransactionRecord existing))
        {
            if (existing.amount == gain
                && string.Equals(
                    existing.targetId,
                    context.targetId,
                    StringComparison.Ordinal))
            {
                reason = string.Empty;
                return true;
            }
            reason = "동일 작업 ID에 다른 입금 기록이 존재합니다.";
            return false;
        }
        if (!TryGetMoney(out Data<int> money))
        {
            reason = "골드 보유 정보를 찾을 수 없습니다.";
            return false;
        }

        int balanceBefore = money.Value;
        int balanceAfter;
        try
        {
            balanceAfter = checked(balanceBefore + gain);
        }
        catch (OverflowException)
        {
            reason = "골드 입금 결과가 정수 범위를 초과합니다.";
            return false;
        }

        money.Value = balanceAfter;
        try
        {
            transactionLedger.RecordSuccess(
                context,
                gain,
                balanceBefore,
                balanceAfter);
        }
        catch
        {
            money.Value = balanceBefore;
            throw;
        }

        if (!transactionLedger.TryGetSuccessfulBySource(
                context.kind,
                context.sourceId,
                out EconomyTransactionRecord published)
            || published.amount != gain
            || published.balanceBefore != balanceBefore
            || published.balanceAfter != balanceAfter
            || !string.Equals(
                published.targetId,
                context.targetId,
                StringComparison.Ordinal))
        {
            money.Value = balanceBefore;
            throw new InvalidOperationException(
                "The money ledger did not preserve its successful credit contract.");
        }

        reason = string.Empty;
        return true;
    }

    public void SetBalance(int amount, EconomyTransactionContext context)
    {
        if (!TryGetMoney(out Data<int> money))
        {
            throw new InvalidOperationException(
                "Cannot set the balance without an active game session.");
        }

        int balanceBefore = money.Value;
        int balanceAfter = Mathf.Max(0, amount);
        if (balanceAfter == balanceBefore)
        {
            return;
        }

        money.Value = balanceAfter;
        transactionLedger.RecordSuccess(
            context,
            balanceAfter - balanceBefore,
            balanceBefore,
            balanceAfter);
    }

    private bool TryGetMoney(out Data<int> money)
    {
        money = null;
        if (!gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            || gameData == null)
        {
            return false;
        }

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
