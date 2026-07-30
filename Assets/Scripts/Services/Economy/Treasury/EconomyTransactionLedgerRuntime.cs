using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

public interface IEconomyTransactionLedger
{
    IReadOnlyList<EconomyTransactionRecord> Records { get; }
    IDisposable Begin(EconomyTransactionContext context);
    void RecordFailure(
        EconomyTransactionContext context,
        int amount,
        string reason,
        int balanceAfter);
    int SumSince(float gameTime, bool income);
    EconomyTransactionLedgerSaveData Capture();
    void Restore(EconomyTransactionLedgerSaveData saveData);
}

public sealed class EconomyTransactionLedgerRuntime :
    IEconomyTransactionLedger,
    IStartable,
    IDisposable
{
    private const int MaxRecordCount = 256;

    private sealed class ContextScope : IDisposable
    {
        private EconomyTransactionLedgerRuntime owner;

        public ContextScope(EconomyTransactionLedgerRuntime owner)
        {
            this.owner = owner;
        }

        public void Dispose()
        {
            EconomyTransactionLedgerRuntime current = owner;
            owner = null;
            current?.PopContext();
        }
    }

    private readonly IGameDataProvider gameDataProvider;
    private readonly IGameClock gameClock;
    private readonly List<EconomyTransactionRecord> records =
        new List<EconomyTransactionRecord>(MaxRecordCount);
    private readonly Stack<EconomyTransactionContext> contexts =
        new Stack<EconomyTransactionContext>();

    private Data<int> observedMoney;
    private int lastObservedBalance;
    private int nextSequence = 1;

    public EconomyTransactionLedgerRuntime(
        IGameDataProvider gameDataProvider,
        IGameClock gameClock)
    {
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public IReadOnlyList<EconomyTransactionRecord> Records => records;

    public void Start()
    {
        EnsureObserved();
    }

    public IDisposable Begin(EconomyTransactionContext context)
    {
        EnsureObserved();
        contexts.Push(Normalize(context));
        return new ContextScope(this);
    }

    public void RecordFailure(
        EconomyTransactionContext context,
        int amount,
        string reason,
        int balanceAfter)
    {
        EconomyTransactionContext normalized = Normalize(context);
        AddRecord(new EconomyTransactionRecord
        {
            transactionId = CreateTransactionId(),
            kind = normalized.kind,
            sourceId = normalized.sourceId,
            targetId = normalized.targetId,
            description = normalized.description,
            amount = -Math.Abs(amount),
            balanceAfter = Math.Max(0, balanceAfter),
            gameTime = gameClock.Time,
            succeeded = false,
            failureReason = reason ?? string.Empty
        });
    }

    public int SumSince(float gameTime, bool income)
    {
        return records
            .Where(record =>
                record != null
                && record.succeeded
                && record.gameTime >= gameTime
                && (income ? record.amount > 0 : record.amount < 0))
            .Sum(record => income ? record.amount : -record.amount);
    }

    public EconomyTransactionLedgerSaveData Capture()
    {
        return new EconomyTransactionLedgerSaveData
        {
            nextSequence = Math.Max(1, nextSequence),
            records = records
                .Where(record => record != null)
                .Select(record => record.Clone())
                .ToList()
        };
    }

    public void Restore(EconomyTransactionLedgerSaveData saveData)
    {
        records.Clear();
        foreach (EconomyTransactionRecord record in saveData?.records
                     ?? new List<EconomyTransactionRecord>())
        {
            if (record != null)
            {
                records.Add(record.Clone());
            }
        }

        if (records.Count > MaxRecordCount)
        {
            records.RemoveRange(0, records.Count - MaxRecordCount);
        }

        nextSequence = Math.Max(1, saveData?.nextSequence ?? 1);
        contexts.Clear();
        EnsureObserved();
        lastObservedBalance = observedMoney?.Value ?? 0;
    }

    public void Dispose()
    {
        if (observedMoney != null)
        {
            observedMoney.OnValueChange -= OnMoneyChanged;
            observedMoney = null;
        }

        contexts.Clear();
    }

    private void EnsureObserved()
    {
        if (!gameDataProvider.TryGetGameData(out GameData gameData)
            || gameData == null)
        {
            return;
        }

        gameData.holdingMoney ??= new Data<int>();
        if (ReferenceEquals(observedMoney, gameData.holdingMoney))
        {
            return;
        }

        if (observedMoney != null)
        {
            observedMoney.OnValueChange -= OnMoneyChanged;
        }

        observedMoney = gameData.holdingMoney;
        lastObservedBalance = Math.Max(0, observedMoney.Value);
        observedMoney.OnValueChange += OnMoneyChanged;
    }

    private void OnMoneyChanged(int newBalance)
    {
        int normalizedBalance = Math.Max(0, newBalance);
        int delta = normalizedBalance - lastObservedBalance;
        lastObservedBalance = normalizedBalance;
        if (delta == 0)
        {
            return;
        }

        EconomyTransactionContext context = contexts.Count > 0
            ? contexts.Peek()
            : new EconomyTransactionContext(
                delta > 0
                    ? EconomyTransactionKind.LegacyIncome
                    : EconomyTransactionKind.LegacyExpense,
                "legacy-direct-mutation",
                description: "기존 경제 시스템 변경");

        AddRecord(new EconomyTransactionRecord
        {
            transactionId = CreateTransactionId(),
            kind = context.kind,
            sourceId = context.sourceId,
            targetId = context.targetId,
            description = context.description,
            amount = delta,
            balanceAfter = normalizedBalance,
            gameTime = gameClock.Time,
            succeeded = true
        });
    }

    private void AddRecord(EconomyTransactionRecord record)
    {
        records.Add(record);
        if (records.Count > MaxRecordCount)
        {
            records.RemoveAt(0);
        }
    }

    private string CreateTransactionId()
    {
        return $"economy:{nextSequence++:D8}";
    }

    private void PopContext()
    {
        if (contexts.Count > 0)
        {
            contexts.Pop();
        }
    }

    private static EconomyTransactionContext Normalize(
        EconomyTransactionContext context)
    {
        context.sourceId = context.sourceId?.Trim() ?? string.Empty;
        context.targetId = context.targetId?.Trim() ?? string.Empty;
        context.description = context.description?.Trim() ?? string.Empty;
        return context;
    }
}
