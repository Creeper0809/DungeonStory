#if UNITY_EDITOR
using System;
using UnityEngine;

internal sealed class EditorGameMoneyAccount : IGameMoneyAccount
{
    private readonly GameSessionState state;
    private readonly IEconomyTransactionLedger transactionLedger;

    public EditorGameMoneyAccount(
        GameSessionState state,
        IEconomyTransactionLedger transactionLedger = null)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.transactionLedger = transactionLedger;
    }

    public int Balance => Mathf.Max(0, state.holdingMoney.Value);
    public bool CanSpend(int amount) => Balance >= Mathf.Max(0, amount);

    public bool TrySpend(int amount, out string reason)
    {
        return TrySpend(amount, default, out reason);
    }

    public bool TrySpend(
        int amount,
        EconomyTransactionContext context,
        out string reason)
    {
        int cost = Mathf.Max(0, amount);
        if (!CanSpend(cost))
        {
            reason = "insufficient funds";
            transactionLedger?.RecordFailure(context, cost, reason, Balance);
            return false;
        }

        int before = Balance;
        state.holdingMoney.Value = before - cost;
        if (cost > 0)
        {
            transactionLedger?.RecordSuccess(
                context, -cost, before, state.holdingMoney.Value);
        }
        reason = string.Empty;
        return true;
    }

    public void Add(int amount)
    {
        Add(amount, default);
    }

    public void Add(int amount, EconomyTransactionContext context)
    {
        int gain = Mathf.Max(0, amount);
        int before = Balance;
        state.holdingMoney.Value = before + gain;
        if (gain > 0)
            transactionLedger?.RecordSuccess(
                context, gain, before, state.holdingMoney.Value);
    }

    public void SetBalance(int amount, EconomyTransactionContext context)
    {
        state.holdingMoney.Value = Mathf.Max(0, amount);
    }
}
#endif
