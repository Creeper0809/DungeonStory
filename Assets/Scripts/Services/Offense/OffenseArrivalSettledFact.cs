using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class OffenseArrivalSettlementFactData
{
    public string arrivalId = string.Empty, expeditionId = string.Empty, targetId = string.Empty;
    public OffenseReturnArrivalKind kind;
    public int absoluteDay, requestedAmount, materializedAmount, securedAmount, escapedAmount;
    public List<string> materializedIds = new(), escapedIds = new();
    public OffenseArrivalSettlementFactData Clone() => new()
    {
        arrivalId = arrivalId, expeditionId = expeditionId, targetId = targetId, kind = kind,
        absoluteDay = absoluteDay, requestedAmount = requestedAmount, materializedAmount = materializedAmount,
        securedAmount = securedAmount, escapedAmount = escapedAmount,
        materializedIds = new(materializedIds), escapedIds = new(escapedIds)
    };
}

/// <summary>Immutable actual containment/escape settlement; no actors, logging or presentation policy.</summary>
public sealed class OffenseArrivalSettledFact
{
    private readonly OffenseArrivalSettlementFactData data;
    public OffenseArrivalSettledFact(OffenseArrivalSettlementFactData source)
    {
        if (source == null || !DomainFactValueSyntax.IsIdentity(source.arrivalId)
            || !DomainFactValueSyntax.IsIdentity(source.expeditionId) || !DomainFactValueSyntax.IsIdentity(source.targetId)
            || !Enum.IsDefined(typeof(OffenseReturnArrivalKind), source.kind) || source.absoluteDay < 1
            || source.requestedAmount < 1 || source.requestedAmount > 10000
            || source.materializedAmount != source.requestedAmount || source.securedAmount < 0 || source.escapedAmount < 0
            || (long)source.securedAmount + source.escapedAmount != source.materializedAmount
            || source.materializedIds == null || source.escapedIds == null
            || source.materializedIds.Count != source.materializedAmount || source.escapedIds.Count != source.escapedAmount
            || source.materializedIds.Any(value => !DomainFactValueSyntax.IsIdentity(value))
            || source.materializedIds.Distinct(StringComparer.Ordinal).Count() != source.materializedIds.Count
            || source.escapedIds.Distinct(StringComparer.Ordinal).Count() != source.escapedIds.Count
            || source.escapedIds.Any(value => !source.materializedIds.Contains(value, StringComparer.Ordinal)))
            throw new ArgumentException("offense-arrival-settlement-fact-invalid");
        data = source.Clone();
    }
    public OffenseArrivalSettlementFactData Snapshot() => data.Clone();
    public OffenseExpeditionArrivalReceipt Receipt => new(data.arrivalId, data.kind.ToString(), data.requestedAmount,
        data.materializedAmount, data.securedAmount, data.escapedAmount,
        data.escapedAmount == 0 ? OffenseExpeditionArrivalResolution.Secured : OffenseExpeditionArrivalResolution.Escaped);
}

public enum OffenseArrivalActivity { Materialized, Escaped }
public readonly struct OffenseArrivalActivityFact
{
    public OffenseArrivalActivityFact(string arrivalId, OffenseReturnArrivalKind kind,
        OffenseArrivalActivity activity, int count, string message)
    {
        if (!DomainFactValueSyntax.IsIdentity(arrivalId) || !Enum.IsDefined(typeof(OffenseReturnArrivalKind), kind)
            || !Enum.IsDefined(typeof(OffenseArrivalActivity), activity) || count < 1 || message == null)
            throw new ArgumentException("offense-arrival-activity-fact-invalid");
        ArrivalId = arrivalId; Kind = kind; Activity = activity; Count = count; Message = message;
    }
    public string ArrivalId { get; }
    public OffenseReturnArrivalKind Kind { get; }
    public OffenseArrivalActivity Activity { get; }
    public int Count { get; }
    public string Message { get; }
}
