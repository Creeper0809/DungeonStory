using System;
using System.Collections.Generic;
using UnityEngine;

public static class DungeonFactionIds
{
    public const string Beastkin = "faction:dungeon:beastkin";
    public const string Demon = "faction:dungeon:demon";
    public const string Kobold = "faction:dungeon:kobold";
    public const string Myconid = "faction:dungeon:myconid";
    public const string Harpy = "faction:dungeon:harpy";
    public const string Golem = "faction:dungeon:golem";

    public static readonly string[] All =
    {
        Beastkin,
        Demon,
        Kobold,
        Myconid,
        Harpy,
        Golem
    };
}

public enum FactionContractKind
{
    Trade = 0,
    Recruitment = 1,
    Supply = 2,
    Reinforcement = 3
}

public enum FactionRouteKind
{
    TradeCaravan = 0,
    SupplyCaravan = 1,
    Reinforcement = 2,
    Restitution = 3
}

public enum FactionRouteStatus
{
    Traveling = 0,
    Delayed = 1,
    Arrived = 2,
    Returning = 3,
    Lost = 4
}

[Serializable]
public sealed class FactionCargoLine
{
    public string itemId = string.Empty;
    [Min(1)] public int amount = 1;

    public FactionCargoLine Clone() => new FactionCargoLine
    {
        itemId = itemId ?? string.Empty,
        amount = Mathf.Max(1, amount)
    };
}

[Serializable]
public sealed class DungeonFactionState
{
    public string factionId = string.Empty;
    public int trust;
    public int betrayalScars;
    public int negotiationBlockedUntilDay;
    public bool discovered;
    public bool allianceProjectCompleted;
    public bool restitutionPaid;
    public bool recoveryEventCompleted;
    public int lastBetrayalLootValue;
    public int restitutionRequiredValue;
    public int homeQ;
    public int homeR;
    public int unpaidContractCount;
    public int reinforcementDeaths;
    public int equipmentLosses;

    public OffenseHexCoord HomeCoord => new OffenseHexCoord(homeQ, homeR);
    public bool NegotiationBlocked(int day) =>
        day < negotiationBlockedUntilDay;
}

[Serializable]
public sealed class FactionRouteState
{
    public string routeId = string.Empty;
    public string factionId = string.Empty;
    public FactionRouteKind kind;
    public FactionRouteStatus status;
    public List<OffenseHexCoordSaveData> path =
        new List<OffenseHexCoordSaveData>();
    public int pathIndex;
    public float segmentProgress;
    public float delaySeconds;
    public int strength = 100;
    public int createdDay;
    public int estimatedArrivalDay;
    public bool ambushed;
    public bool cargoDelivered;
    public bool actorsSpawned;
    public List<string> reinforcementActorIds = new List<string>();
    public List<FactionCargoLine> cargo = new List<FactionCargoLine>();

    public OffenseHexCoord CurrentCoord =>
        path != null && path.Count > 0
            ? path[Mathf.Clamp(pathIndex, 0, path.Count - 1)].ToCoord()
            : default;
}

[Serializable]
public sealed class DungeonFactionSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int currentDay = 1;
    public int routeSequence;
    public List<DungeonFactionState> factions =
        new List<DungeonFactionState>();
    public List<FactionRouteState> routes =
        new List<FactionRouteState>();
}

public readonly struct FactionTrustChangedEvent
{
    public FactionTrustChangedEvent(
        string factionId,
        int previous,
        int current,
        string reason)
    {
        FactionId = factionId ?? string.Empty;
        Previous = previous;
        Current = current;
        Reason = reason ?? string.Empty;
    }

    public string FactionId { get; }
    public int Previous { get; }
    public int Current { get; }
    public string Reason { get; }
}

public readonly struct FactionRouteArrivedEvent
{
    public FactionRouteArrivedEvent(
        string routeId,
        string factionId,
        FactionRouteKind kind,
        int strength)
    {
        RouteId = routeId ?? string.Empty;
        FactionId = factionId ?? string.Empty;
        Kind = kind;
        Strength = Mathf.Clamp(strength, 0, 100);
    }

    public string RouteId { get; }
    public string FactionId { get; }
    public FactionRouteKind Kind { get; }
    public int Strength { get; }
}

public interface IFactionRuntime
{
    IReadOnlyList<DungeonFactionDefinitionSO> Definitions { get; }
    IReadOnlyList<DungeonFactionState> Factions { get; }
    IReadOnlyList<FactionRouteState> Routes { get; }
    bool TryGetFaction(string factionId, out DungeonFactionState faction);
    bool IsContractUnlocked(string factionId, FactionContractKind contract);
    bool TryAdjustTrust(
        string factionId,
        int amount,
        string reason,
        out string message);
    bool TryOfferGoodwill(
        string factionId,
        int physicalValue,
        out string message);
    bool TryCompleteAllianceProject(string factionId, out string message);
    bool TryRequestTrade(string factionId, out string routeId, out string message);
    bool TryRequestSupply(string factionId, out string routeId, out string message);
    bool TryRequestReinforcement(
        string factionId,
        out string routeId,
        out string message);
    bool TryApplyRouteAmbush(
        string routeId,
        int strengthLoss,
        float delaySeconds,
        out string message);
    bool TryBetray(string factionId, int stolenValue, out string message);
    bool TryPayRestitution(string factionId, int physicalValue, out string message);
    bool TryCompleteRecoveryEvent(string factionId, out string message);
    void RecordReinforcementLoss(
        string factionId,
        int deaths,
        int equipmentLosses);
    DungeonFactionSaveData Capture();
    void Restore(DungeonFactionSaveData saveData);
    void Reset();
}

public interface IFactionRuntimeProvider
{
    bool TryGetRuntime(out IFactionRuntime runtime);
}

public sealed class FactionRuntimeProvider : IFactionRuntimeProvider
{
    private IFactionRuntime runtime;

    public void Bind(IFactionRuntime value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (runtime != null && !ReferenceEquals(runtime, value))
        {
            throw new InvalidOperationException(
                "FactionRuntimeProvider is already bound to another runtime.");
        }

        runtime = value;
    }

    public bool TryGetRuntime(out IFactionRuntime resolvedRuntime)
    {
        resolvedRuntime = runtime;
        return resolvedRuntime != null;
    }
}
