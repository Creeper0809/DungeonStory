using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;

// Detached domain receipt. Native identities, metrics and narrative text belong to its observer.
public sealed class FactionCargoDeliveredFact
{
    private readonly FactionRouteCargoDeliveryReceipt receipt;
    public FactionCargoDeliveredFact(FactionRouteState route, string factionDisplayName,
        int absoluteDay, FactionRouteCargoDeliveryReceipt receipt)
    {
        if (route == null || string.IsNullOrWhiteSpace(route.routeId) || string.IsNullOrWhiteSpace(route.factionId)
            || route.path == null || route.path.Count == 0 || route.path.Any(value => value == null)
            || string.IsNullOrWhiteSpace(factionDisplayName) || absoluteDay < 1 || receipt == null
            || receipt.state != FactionRouteCargoDeliveryState.Delivered || string.IsNullOrWhiteSpace(receipt.batchCommitId)
            || string.IsNullOrWhiteSpace(receipt.destinationId) || string.IsNullOrWhiteSpace(receipt.outcomeFingerprint)
            || receipt.stacks == null || receipt.stacks.Count == 0 || receipt.stacks.Any(value => value == null)
            || route.settlement?.quoteLines == null || route.settlement.quoteLines.Any(value => value == null))
            throw new ArgumentException("faction-cargo-fact-invalid");
        RouteId = route.routeId; FactionId = route.factionId; FactionDisplayName = factionDisplayName;
        AbsoluteDay = absoluteDay; ArrivalPublicationKey = route.arrivalPublicationKey;
        SourceQ = route.path[0].q; SourceR = route.path[0].r;
        DestinationQ = route.path[route.path.Count - 1].q; DestinationR = route.path[route.path.Count - 1].r;
        this.receipt = receipt.Clone();
        QuoteLines = Array.AsReadOnly(route.settlement.quoteLines.Select(value =>
            new FactionRouteQuoteLineFactSnapshot(value.itemId, value.amount, value.unitPriceGold)).ToArray());
    }
    public string RouteId { get; }
    public string FactionId { get; }
    public string FactionDisplayName { get; }
    public int AbsoluteDay { get; }
    public int SourceQ { get; }
    public int SourceR { get; }
    public int DestinationQ { get; }
    public int DestinationR { get; }
    public string ArrivalPublicationKey { get; }
    public IReadOnlyList<FactionRouteQuoteLineFactSnapshot> QuoteLines { get; }
    public FactionRouteCargoDeliveryReceipt Receipt() => receipt.Clone();
}
