using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DungeonStory.Factions;

/// <summary>Immutable faction relationship transition observed outside the faction domain.</summary>
public sealed class FactionTrustChangedDomainFact
{
    public FactionTrustChangedDomainFact(
        string ownerIdentity,
        int absoluteDay,
        string factionId,
        string factionDisplayName,
        int previous,
        int current,
        string reason)
    {
        OwnerIdentity = ownerIdentity?.Trim() ?? string.Empty;
        AbsoluteDay = absoluteDay;
        FactionId = factionId?.Trim() ?? string.Empty;
        FactionDisplayName = factionDisplayName?.Trim() ?? string.Empty;
        Previous = previous;
        Current = current;
        Reason = reason?.Trim() ?? string.Empty;
    }

    public string OwnerIdentity { get; }
    public int AbsoluteDay { get; }
    public string FactionId { get; }
    public string FactionDisplayName { get; }
    public int Previous { get; }
    public int Current { get; }
    public string Reason { get; }
}

public readonly struct FactionTrustTransitionFactSnapshot
{
    public FactionTrustTransitionFactSnapshot(
        string factionId,
        string factionDisplayName,
        int previous,
        int current)
    {
        FactionId = factionId?.Trim() ?? string.Empty;
        FactionDisplayName = factionDisplayName?.Trim() ?? string.Empty;
        Previous = previous;
        Current = current;
    }

    public string FactionId { get; }
    public string FactionDisplayName { get; }
    public int Previous { get; }
    public int Current { get; }
}

/// <summary>Immutable multi-faction betrayal transition and its physical loot value.</summary>
public sealed class FactionBetrayalDomainFact
{
    private readonly ReadOnlyCollection<FactionTrustTransitionFactSnapshot>
        transitions;

    public FactionBetrayalDomainFact(
        string operationId,
        int absoluteDay,
        string betrayedFactionId,
        int actualLootValue,
        IEnumerable<FactionTrustTransitionFactSnapshot> transitions)
    {
        OperationId = operationId?.Trim() ?? string.Empty;
        AbsoluteDay = absoluteDay;
        BetrayedFactionId = betrayedFactionId?.Trim() ?? string.Empty;
        ActualLootValue = actualLootValue;
        this.transitions = Array.AsReadOnly(
            (transitions ?? Array.Empty<FactionTrustTransitionFactSnapshot>())
            .ToArray());
    }

    public string OperationId { get; }
    public int AbsoluteDay { get; }
    public string BetrayedFactionId { get; }
    public int ActualLootValue { get; }
    public IReadOnlyList<FactionTrustTransitionFactSnapshot> Transitions =>
        transitions;
}

public readonly struct FactionRouteItemFactSnapshot
{
    public FactionRouteItemFactSnapshot(string itemId)
    {
        ItemId = itemId?.Trim() ?? string.Empty;
    }

    public string ItemId { get; }
}

public readonly struct FactionRouteQuoteLineFactSnapshot
{
    public FactionRouteQuoteLineFactSnapshot(
        string itemId,
        int amount,
        int unitPriceGold)
    {
        ItemId = itemId?.Trim() ?? string.Empty;
        Amount = amount;
        UnitPriceGold = unitPriceGold;
    }

    public string ItemId { get; }
    public int Amount { get; }
    public int UnitPriceGold { get; }
}

public sealed class FactionRouteFactSnapshot
{
    private readonly ReadOnlyCollection<FactionRouteItemFactSnapshot> cargo;

    private FactionRouteFactSnapshot(
        FactionRouteState route,
        string factionDisplayName)
    {
        if (route == null)
            throw new ArgumentNullException(nameof(route));
        FactionHexCoordSaveData source = route.path?.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Faction route '{route.routeId}' has no source coordinate.");
        FactionHexCoordSaveData destination = route.path?.LastOrDefault()
            ?? throw new InvalidOperationException(
                $"Faction route '{route.routeId}' has no destination coordinate.");

        RouteId = route.routeId?.Trim() ?? string.Empty;
        FactionId = route.factionId?.Trim() ?? string.Empty;
        FactionDisplayName = factionDisplayName?.Trim() ?? string.Empty;
        Kind = route.kind;
        Status = route.status;
        Strength = route.strength;
        DelaySeconds = route.delaySeconds;
        SourceQ = source.q;
        SourceR = source.r;
        DestinationQ = destination.q;
        DestinationR = destination.r;
        SettlementPublicationKey = route.settlementPublicationKey ?? string.Empty;
        cargo = Array.AsReadOnly((route.cargo ?? new List<FactionCargoLine>())
            .Where(value => value != null)
            .Select(value => new FactionRouteItemFactSnapshot(value.itemId))
            .ToArray());
    }

    public string RouteId { get; }
    public string FactionId { get; }
    public string FactionDisplayName { get; }
    public FactionRouteKind Kind { get; }
    public FactionRouteStatus Status { get; }
    public int Strength { get; }
    public float DelaySeconds { get; }
    public int SourceQ { get; }
    public int SourceR { get; }
    public int DestinationQ { get; }
    public int DestinationR { get; }
    public string SettlementPublicationKey { get; }
    public IReadOnlyList<FactionRouteItemFactSnapshot> Cargo => cargo;

    public static FactionRouteFactSnapshot Capture(
        FactionRouteState route,
        string factionDisplayName) => new(route, factionDisplayName);

    public FactionRouteFactData CaptureData() => new()
    {
        routeId = RouteId, factionId = FactionId, factionDisplayName = FactionDisplayName,
        kind = Kind, status = Status, strength = Strength, delaySeconds = DelaySeconds,
        sourceQ = SourceQ, sourceR = SourceR, destinationQ = DestinationQ, destinationR = DestinationR,
        settlementPublicationKey = SettlementPublicationKey, cargoItemIds = cargo.Select(value => value.ItemId).ToList()
    };

    public static FactionRouteFactSnapshot Restore(FactionRouteFactData data)
    {
        if (data == null || data.cargoItemIds == null || data.cargoItemIds.Any(string.IsNullOrWhiteSpace)
            || string.IsNullOrWhiteSpace(data.routeId) || string.IsNullOrWhiteSpace(data.factionId)
            || string.IsNullOrWhiteSpace(data.factionDisplayName) || data.settlementPublicationKey == null)
            throw new ArgumentException("faction-route-observation-invalid");
        return Capture(new FactionRouteState
        {
            routeId = data.routeId, factionId = data.factionId, kind = data.kind, status = data.status,
            strength = data.strength, delaySeconds = data.delaySeconds, settlementPublicationKey = data.settlementPublicationKey,
            path = new List<FactionHexCoordSaveData> { new() { q = data.sourceQ, r = data.sourceR }, new() { q = data.destinationQ, r = data.destinationR } },
            cargo = data.cargoItemIds.Select(value => new FactionCargoLine { itemId = value }).ToList()
        }, data.factionDisplayName);
    }
}

public sealed class FactionRouteSettlementFactSnapshot
{
    private readonly ReadOnlyCollection<FactionRouteQuoteLineFactSnapshot>
        quoteLines;

    private FactionRouteSettlementFactSnapshot(
        FactionRouteSettlementReceipt receipt)
    {
        if (receipt == null)
            throw new ArgumentNullException(nameof(receipt));
        State = receipt.state;
        CapabilityId = receipt.capabilityId?.Trim() ?? string.Empty;
        CargoAuthoredGold = receipt.cargoAuthoredGold;
        PaymentGold = receipt.paymentGold;
        SourceDigest = receipt.sourceDigest?.Trim() ?? string.Empty;
        QuoteDigest = receipt.quoteDigest?.Trim() ?? string.Empty;
        TransactionId = receipt.transactionId?.Trim() ?? string.Empty;
        AllianceBenefitReservationId =
            receipt.allianceBenefitReservationId?.Trim() ?? string.Empty;
        BalanceBefore = receipt.balanceBefore;
        BalanceAfter = receipt.balanceAfter;
        AllianceBenefitDebitMilliEwu =
            receipt.allianceBenefitDebitMilliEwu;
        AllianceBenefitBalanceBeforeMilliEwu =
            receipt.allianceBenefitBalanceBeforeMilliEwu;
        AllianceBenefitBalanceAfterMilliEwu =
            receipt.allianceBenefitBalanceAfterMilliEwu;
        quoteLines = Array.AsReadOnly((receipt.quoteLines
                ?? new List<FactionRouteQuoteLineReceipt>())
            .Where(value => value != null)
            .Select(value => new FactionRouteQuoteLineFactSnapshot(
                value.itemId,
                value.amount,
                value.unitPriceGold))
            .ToArray());
    }

    public FactionRouteSettlementState State { get; }
    public string CapabilityId { get; }
    public int CargoAuthoredGold { get; }
    public int PaymentGold { get; }
    public string SourceDigest { get; }
    public string QuoteDigest { get; }
    public string TransactionId { get; }
    public string AllianceBenefitReservationId { get; }
    public int BalanceBefore { get; }
    public int BalanceAfter { get; }
    public long AllianceBenefitDebitMilliEwu { get; }
    public long AllianceBenefitBalanceBeforeMilliEwu { get; }
    public long AllianceBenefitBalanceAfterMilliEwu { get; }
    public IReadOnlyList<FactionRouteQuoteLineFactSnapshot> QuoteLines =>
        quoteLines;

    public static FactionRouteSettlementFactSnapshot Capture(
        FactionRouteSettlementReceipt receipt) => new(receipt);
}

/// <summary>Immutable route creation and settlement fact.</summary>
public sealed class FactionRouteSettlementDomainFact
{
    private readonly FactionRouteSettlementReceipt receipt;
    public FactionRouteSettlementDomainFact(
        FactionRouteState route,
        string factionDisplayName,
        FactionRouteSettlementReceipt receipt,
        string settlementIdentity,
        int absoluteDay)
    {
        Route = FactionRouteFactSnapshot.Capture(route, factionDisplayName);
        Settlement = FactionRouteSettlementFactSnapshot.Capture(receipt);
        this.receipt = receipt.Clone();
        SettlementIdentity = settlementIdentity?.Trim() ?? string.Empty;
        AbsoluteDay = absoluteDay;
    }

    public FactionRouteFactSnapshot Route { get; }
    public FactionRouteSettlementFactSnapshot Settlement { get; }
    public string SettlementIdentity { get; }
    public int AbsoluteDay { get; }

    public FactionRouteSettlementDomainFact(FactionRouteObservationData data)
    {
        if (data?.settlement == null || data.absoluteDay < 1 || string.IsNullOrWhiteSpace(data.operationId) || data.before?.IsEmpty != true)
            throw new ArgumentException("faction-settlement-observation-invalid");
        Route = FactionRouteFactSnapshot.Restore(data.after);
        receipt = data.settlement.Clone();
        Settlement = FactionRouteSettlementFactSnapshot.Capture(receipt);
        SettlementIdentity = data.operationId; AbsoluteDay = data.absoluteDay;
    }

    public FactionRouteObservationData CaptureData() => new()
    { absoluteDay = AbsoluteDay, operationId = SettlementIdentity, after = Route.CaptureData(), settlement = receipt.Clone() };
}

/// <summary>Immutable route status transition captured at arrival.</summary>
public sealed class FactionRouteArrivedDomainFact
{
    public FactionRouteArrivedDomainFact(
        FactionRouteState before,
        FactionRouteState after,
        string factionDisplayName,
        int absoluteDay)
    {
        Before = FactionRouteFactSnapshot.Capture(before, factionDisplayName);
        After = FactionRouteFactSnapshot.Capture(after, factionDisplayName);
        AbsoluteDay = absoluteDay;
    }

    public FactionRouteFactSnapshot Before { get; }
    public FactionRouteFactSnapshot After { get; }
    public int AbsoluteDay { get; }

    public FactionRouteArrivedDomainFact(FactionRouteObservationData data)
    {
        if (data == null || data.absoluteDay < 1 || !data.HasEmptySettlement)
            throw new ArgumentException("faction-arrival-observation-invalid");
        Before = FactionRouteFactSnapshot.Restore(data.before);
        After = FactionRouteFactSnapshot.Restore(data.after);
        if (data.operationId != After.RouteId || Before.RouteId != After.RouteId)
            throw new ArgumentException("faction-arrival-observation-identity-invalid");
        AbsoluteDay = data.absoluteDay;
    }

    public FactionRouteObservationData CaptureData() => new()
    { absoluteDay = AbsoluteDay, operationId = After.RouteId, before = Before.CaptureData(), after = After.CaptureData() };
}
