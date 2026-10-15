using System;
using UnityEngine;

[Serializable]
public struct StockSupplyResult
{
    public bool success;
    public StockCategory category;
    public int requestedAmount;
    public int deliveredAmount;
    public int cost;
    public string sourceLabel;
    public string reason;
    public int paidAmount;
    public int refundedAmount;
    public int netCost;
    public int balanceBefore;
    public int balanceAfter;
    public int spawnedStackQuantity;
    public string spawnedStackIds;
    public string paymentTransactionSource;
    public string refundTransactionSource;
    public string exceptionType;

    public StockSupplyResult(
        bool success,
        StockCategory category,
        int requestedAmount,
        int deliveredAmount,
        int cost,
        string sourceLabel,
        string reason,
        int paidAmount = 0,
        int refundedAmount = 0,
        int netCost = -1,
        int balanceBefore = 0,
        int balanceAfter = 0,
        string paymentTransactionSource = "",
        string refundTransactionSource = "",
        string exceptionType = "")
    {
        this.success = success;
        this.category = category;
        this.requestedAmount = Mathf.Max(0, requestedAmount);
        this.deliveredAmount = Mathf.Max(0, deliveredAmount);
        this.cost = Mathf.Max(0, cost);
        this.sourceLabel = sourceLabel;
        this.reason = reason;
        this.paidAmount = Mathf.Max(0, paidAmount);
        this.refundedAmount = Mathf.Max(0, refundedAmount);
        this.netCost = Mathf.Max(0, netCost < 0 ? cost : netCost);
        this.balanceBefore = Mathf.Max(0, balanceBefore);
        this.balanceAfter = Mathf.Max(0, balanceAfter);
        spawnedStackQuantity = 0;
        spawnedStackIds = string.Empty;
        this.paymentTransactionSource = paymentTransactionSource ?? string.Empty;
        this.refundTransactionSource = refundTransactionSource ?? string.Empty;
        this.exceptionType = exceptionType ?? string.Empty;
    }

    public void AttachSpawnedStackEvidence(
        int quantity,
        string stackIds)
    {
        spawnedStackQuantity = Mathf.Max(0, quantity);
        spawnedStackIds = stackIds?.Trim() ?? string.Empty;
    }

    public string ToSummaryText()
    {
        string label = string.IsNullOrWhiteSpace(sourceLabel) ? "재고 수급" : sourceLabel;
        if (success)
        {
            string costText = netCost > 0 ? $" / 비용 {netCost}" : string.Empty;
            return $"{label}: {category} {deliveredAmount}개 입고{costText}";
        }

        return $"{label}: {category} {requestedAmount}개 입고 실패 - {reason}";
    }
}
