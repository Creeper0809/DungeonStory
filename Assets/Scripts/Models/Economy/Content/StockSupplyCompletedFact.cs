using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Foundation.Facts;

public interface IStockSupplyFactPublisher
{
    bool TryBegin(out long sequence, out string failure);
    DomainFactDelivery Publish(StockSupplyCompletedFact fact);
    void End();
}

[Serializable]
public sealed class StockSupplyCompletedFactData
{
    public long sequence;
    public int absoluteDay;
    public string itemId;
    public StockSupplyResult result;
    public StockSupplyCompletedFactData Clone() => (StockSupplyCompletedFactData)MemberwiseClone();
}

public sealed class StockSupplyCompletedFact
{
    private readonly StockSupplyCompletedFactData data;
    public StockSupplyCompletedFact(StockSupplyCompletedFactData source)
    {
        data = source?.Clone() ?? throw new ArgumentNullException(nameof(source));
        var result = data.result;
        if (data.sequence < 1 || data.sequence == long.MaxValue || data.absoluteDay < 0
            || result.requestedAmount < 0 || result.deliveredAmount < 0 || result.cost < 0
            || result.paidAmount < 0 || result.refundedAmount < 0 || result.netCost < 0
            || result.balanceBefore < 0 || result.balanceAfter < 0
            || result.deliveredAmount != result.spawnedStackQuantity
            || result.success && (result.requestedAmount == 0 || result.deliveredAmount != result.requestedAmount)
            || result.deliveredAmount > 0 && string.IsNullOrWhiteSpace(data.itemId))
            throw new InvalidOperationException("stock-supply-fact-invalid");
        foreach (string value in new[] { data.itemId, result.sourceLabel, result.reason,
                     result.spawnedStackIds, result.paymentTransactionSource, result.refundTransactionSource, result.exceptionType })
            if (value != null && value.Length > 16384)
                throw new InvalidOperationException("stock-supply-fact-text-too-long");
    }
    public long Sequence => data.sequence;
    public int AbsoluteDay => data.absoluteDay;
    public string ItemId => data.itemId;
    public string OperationId => Identity(Sequence);
    public StockSupplyResult Result => data.result;
    public StockSupplyCompletedFactData Snapshot() => data.Clone();
    public static string Identity(long sequence) => "warehouse-delivery:" + sequence.ToString(CultureInfo.InvariantCulture);
}

[Serializable]
public sealed class StockSupplyPendingFactData
{
    public string publicationKey;
    public StockSupplyCompletedFactData fact;
    public StockSupplyPendingFactData Clone() => new() { publicationKey = publicationKey, fact = fact?.Clone() };
}

[Serializable]
public sealed class StockSupplyFactSaveData
{
    public long nextSequence = 1;
    public List<StockSupplyPendingFactData> pending = new();
    public static StockSupplyFactSaveData ValidateAndClone(StockSupplyFactSaveData source)
    {
        if (source == null || source.nextSequence < 1 || source.pending == null || source.pending.Count > 1)
            throw new InvalidOperationException("stock-supply-journal-invalid");
        var copy = new StockSupplyFactSaveData { nextSequence = source.nextSequence };
        foreach (var row in source.pending)
        {
            if (row?.fact == null || !DomainFactValueSyntax.IsText(row.publicationKey, 4096, false)
                || row.fact.sequence != source.nextSequence - 1)
                throw new InvalidOperationException("stock-supply-pending-identity-conflict");
            _ = new StockSupplyCompletedFact(row.fact);
            copy.pending.Add(row.Clone());
        }
        return copy;
    }
}
