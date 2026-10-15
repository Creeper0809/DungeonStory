using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Foundation.Facts;

public sealed class OffenseStockRewardCompletedFact
{
    public OffenseStockRewardCompletedFact(string expeditionId, string targetId, int grantIndex,
        int absoluteDay, OffenseStockRewardReceipt receipt)
    {
        if (!DomainFactValueSyntax.IsIdentity(expeditionId) || !DomainFactValueSyntax.IsIdentity(targetId)
            || grantIndex < 0 || absoluteDay < 0 || receipt == null)
            throw new InvalidOperationException("offense-stock-reward-fact-invalid");
        ExpeditionId = expeditionId; TargetId = targetId; GrantIndex = grantIndex; AbsoluteDay = absoluteDay;
        Receipt = receipt;
    }
    public string ExpeditionId { get; }
    public string TargetId { get; }
    public int GrantIndex { get; }
    public int AbsoluteDay { get; }
    public OffenseStockRewardReceipt Receipt { get; }
    public string OperationId => "offense-stock-reward:" + ExpeditionId + ":grant=" + GrantIndex.ToString(CultureInfo.InvariantCulture);
    public static IReadOnlyList<OffenseStockRewardCompletedFact> From(OffenseExpeditionResolvedFact parent)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));
        var values = new List<OffenseStockRewardCompletedFact>();
        int day = parent.Snapshot().absoluteDay;
        for (int index = 0; index < parent.Result.grantedRewards.Count; index++)
        {
            var receipt = parent.Result.grantedRewards[index].stockSupply;
            if (receipt != null) values.Add(new(parent.Result.expeditionId, parent.Result.targetId, index, day, receipt));
        }
        return values;
    }
}
