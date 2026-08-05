#if UNITY_EDITOR
using System;
using UnityEngine;

internal sealed class EditorGameMoneyAccount : IGameMoneyAccount
{
    private readonly GameSessionState state;

    public EditorGameMoneyAccount(GameSessionState state)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
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
            return false;
        }

        state.holdingMoney.Value -= cost;
        reason = string.Empty;
        return true;
    }

    public void Add(int amount)
    {
        Add(amount, default);
    }

    public void Add(int amount, EconomyTransactionContext context)
    {
        state.holdingMoney.Value += Mathf.Max(0, amount);
    }

    public void SetBalance(int amount, EconomyTransactionContext context)
    {
        state.holdingMoney.Value = Mathf.Max(0, amount);
    }
}
#endif
