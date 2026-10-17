using System;

[Serializable]
public sealed class OffenseStockRewardReceiptData
{
    public StockCategory category;
    public string itemId = string.Empty, sourceLabel = string.Empty, reason = string.Empty;
    public int requestedAmount, deliveredAmount;
    public bool success;
    public OffenseStockRewardReceiptData Clone() => (OffenseStockRewardReceiptData)MemberwiseClone();
}

// The grant owner returns immutable delivery evidence, never a logging descriptor.
public sealed class OffenseStockRewardReceipt
{
    private readonly OffenseStockRewardReceiptData data;
    public OffenseStockRewardReceipt(OffenseStockRewardReceiptData source)
    {
        if (source == null || !Enum.IsDefined(typeof(StockCategory), source.category)
            || source.requestedAmount < 1 || source.deliveredAmount < 0 || source.deliveredAmount > source.requestedAmount
            || source.success && source.deliveredAmount != source.requestedAmount
            || source.itemId == null || source.sourceLabel == null || source.reason == null
            || source.deliveredAmount > 0 && string.IsNullOrWhiteSpace(source.itemId)
            || source.itemId.Length > 1024 || source.sourceLabel.Length > 4096 || source.reason.Length > 4096)
            throw new InvalidOperationException("offense-stock-reward-receipt-invalid");
        data = source.Clone();
    }
    public OffenseStockRewardReceiptData Snapshot() => data.Clone();
}
