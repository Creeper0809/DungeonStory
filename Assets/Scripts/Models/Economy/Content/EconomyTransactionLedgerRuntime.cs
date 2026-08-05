using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IEconomyTransactionLedger
{
    IReadOnlyList<EconomyTransactionRecord> Records { get; }
    void RecordSuccess(
        EconomyTransactionContext context,
        int amount,
        int balanceBefore,
        int balanceAfter);
    void RecordFailure(
        EconomyTransactionContext context,
        int amount,
        string reason,
        int balanceAfter);
    int SumSince(float gameTime, bool income);
    EconomyTransactionLedgerSaveData Capture();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class EconomyTransactionLedgerState
{
    public List<EconomyTransactionRecord> Records { get; } = new(256);
    public int NextSequence { get; set; } = 1;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class TreasuryEconomyAggregateState
{
    public EconomyTransactionLedgerState Ledger { get; set; } = new();
    public Dictionary<string, EmployeeWageState> Wages { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, MercenaryContract> Mercenaries { get; } =
        new(StringComparer.Ordinal);
    public List<PaidFacilityContractState> FacilityContracts { get; } = new();
    public HashSet<string> ChargedFacilityOrderKeys { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, string> FacilityFailures { get; } =
        new(StringComparer.Ordinal);
    public List<AutoProcurementRule> StockRules { get; } = new();
    public List<ProcurementWishlistRule> WishlistRules { get; } = new();
    public List<AutoProcurementResult> ProcurementResults { get; } = new();
    public HashSet<string> ProcessedOfferKeys { get; } =
        new(StringComparer.Ordinal);
    public int DailyBudget { get; set; } = 500;
    public int MinimumReserve { get; set; }
    public int LastProcessedDay { get; set; }
    public Dictionary<string, OverclockState> OverclockStates { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, TreasuryDefensePolicy> DefensePolicies { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, TreasuryDefenseInvasionSpendState> DefenseSpending { get; } =
        new(StringComparer.Ordinal);
    public Dictionary<string, string> DefenseFailures { get; } =
        new(StringComparer.Ordinal);

    public TreasuryEconomyAggregateState Copy()
    {
        TreasuryEconomyAggregateState copy = new()
        {
            DailyBudget = DailyBudget,
            MinimumReserve = MinimumReserve,
            LastProcessedDay = LastProcessedDay,
            Ledger = new EconomyTransactionLedgerState
            {
                NextSequence = Ledger.NextSequence
            }
        };
        copy.Ledger.Records.AddRange(Ledger.Records
            .Where(record => record != null)
            .Select(record => record.Clone()));
        foreach (KeyValuePair<string, EmployeeWageState> pair in Wages)
            copy.Wages.Add(pair.Key, pair.Value.Clone());
        foreach (KeyValuePair<string, MercenaryContract> pair in Mercenaries)
            copy.Mercenaries.Add(pair.Key, pair.Value.Clone());
        copy.FacilityContracts.AddRange(FacilityContracts
            .Where(contract => contract != null)
            .Select(contract => new PaidFacilityContractState
            {
                contractId = contract.contractId,
                facilityPersistentId = contract.facilityPersistentId,
                dailyCost = contract.dailyCost,
                active = contract.active,
                lastSettledDay = contract.lastSettledDay
            }));
        copy.ChargedFacilityOrderKeys.UnionWith(ChargedFacilityOrderKeys);
        foreach (KeyValuePair<string, string> pair in FacilityFailures)
            copy.FacilityFailures.Add(pair.Key, pair.Value);
        copy.StockRules.AddRange(StockRules.Select(rule => rule.Clone()));
        copy.WishlistRules.AddRange(WishlistRules.Select(rule => rule.Clone()));
        copy.ProcurementResults.AddRange(ProcurementResults.Select(result =>
            new AutoProcurementResult
            {
                day = result.day,
                ruleId = result.ruleId,
                itemLabel = result.itemLabel,
                quantity = result.quantity,
                cost = result.cost,
                purchased = result.purchased,
                reason = result.reason
            }));
        copy.ProcessedOfferKeys.UnionWith(ProcessedOfferKeys);
        foreach (KeyValuePair<string, OverclockState> pair in OverclockStates)
            copy.OverclockStates.Add(pair.Key, pair.Value.Clone());
        foreach (KeyValuePair<string, TreasuryDefensePolicy> pair in DefensePolicies)
            copy.DefensePolicies.Add(pair.Key, pair.Value.Clone());
        foreach (KeyValuePair<string, TreasuryDefenseInvasionSpendState> pair in DefenseSpending)
            copy.DefenseSpending.Add(pair.Key, pair.Value.Clone());
        foreach (KeyValuePair<string, string> pair in DefenseFailures)
            copy.DefenseFailures.Add(pair.Key, pair.Value);
        return copy;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class TreasuryEconomyAggregateStateStore
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;

    public TreasuryEconomyAggregateStateStore(
        DungeonRuntimeAggregateRootStore rootStore)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
    }

    public TreasuryEconomyAggregateState Current =>
        rootStore.GetOrCreate(() => new TreasuryEconomyAggregateState());

    public void Replace(TreasuryEconomyAggregateState restored)
    {
        rootStore.Replace(
            restored ?? throw new ArgumentNullException(nameof(restored)));
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class EconomyTransactionLedgerRuntime :
    IEconomyTransactionLedger,
    IStartable
{
    private const int MaxRecordCount = 256;

    private readonly IGameClock gameClock;
    private readonly TreasuryEconomyAggregateStateStore stateStore;

    private EconomyTransactionLedgerState state => stateStore.Current.Ledger;

    private List<EconomyTransactionRecord> records => state.Records;

    public EconomyTransactionLedgerRuntime(
        IGameClock gameClock,
        TreasuryEconomyAggregateStateStore stateStore)
    {
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public IReadOnlyList<EconomyTransactionRecord> Records => records;

    public void Start()
    {
    }

    public void RecordSuccess(
        EconomyTransactionContext context,
        int amount,
        int balanceBefore,
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
            amount = amount,
            balanceBefore = Math.Max(0, balanceBefore),
            balanceAfter = Math.Max(0, balanceAfter),
            gameTime = gameClock.Time,
            succeeded = true
        });
    }

    public void RecordFailure(
        EconomyTransactionContext context,
        int amount,
        string reason,
        int balanceAfter)
    {
        EconomyTransactionContext normalized = Normalize(context);
        int balance = Math.Max(0, balanceAfter);
        AddRecord(new EconomyTransactionRecord
        {
            transactionId = CreateTransactionId(),
            kind = normalized.kind,
            sourceId = normalized.sourceId,
            targetId = normalized.targetId,
            description = normalized.description,
            amount = -Math.Abs(amount),
            balanceBefore = balance,
            balanceAfter = balance,
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
            nextSequence = Math.Max(1, state.NextSequence),
            records = records
                .Where(record => record != null)
                .Select(record => record.Clone())
                .ToList()
        };
    }

    public void PopulateRestoreState(
        TreasuryEconomyAggregateState target,
        EconomyTransactionLedgerSaveData saveData)
    {
        EconomyTransactionLedgerState restored = new()
        {
            NextSequence = Math.Max(1, saveData?.nextSequence ?? 1)
        };
        foreach (EconomyTransactionRecord record in saveData?.records
                     ?? new List<EconomyTransactionRecord>())
        {
            if (record != null)
            {
                restored.Records.Add(record.Clone());
            }
        }

        if (restored.Records.Count > MaxRecordCount)
        {
            restored.Records.RemoveRange(
                0,
                restored.Records.Count - MaxRecordCount);
        }

        (target ?? throw new ArgumentNullException(nameof(target))).Ledger = restored;
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
        return $"economy:{state.NextSequence++:D8}";
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
