using System;
using System.Collections.Generic;

namespace DungeonStory.Factions
{

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
    public int amount = 1;

    public FactionCargoLine Clone() => new FactionCargoLine
    {
        itemId = itemId ?? string.Empty,
        amount = Math.Max(1, amount)
    };
}

public readonly struct FactionHexCoord : IEquatable<FactionHexCoord>
{
    public FactionHexCoord(int q, int r)
    {
        Q = q;
        R = r;
    }

    public int Q { get; }
    public int R { get; }

    public bool Equals(FactionHexCoord other) => Q == other.Q && R == other.R;
    public override bool Equals(object obj) => obj is FactionHexCoord other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Q, R);
}

[Serializable]
public sealed class FactionHexCoordSaveData
{
    public int q;
    public int r;

    public FactionHexCoord ToCoord() => new(q, r);
    public static FactionHexCoordSaveData From(FactionHexCoord value) => new()
    {
        q = value.Q,
        r = value.R
    };
}

public sealed class FactionDefinitionSnapshot
{
    public FactionDefinitionSnapshot(
        string stableId,
        string displayName,
        string speciesTag,
        string description,
        IReadOnlyList<string> relationTags,
        IReadOnlyList<string> tradeTags,
        string reinforcementRole,
        IReadOnlyList<FactionCargoLine> tradeCargo,
        IReadOnlyList<FactionCargoLine> supplyCargo)
    {
        StableId = stableId?.Trim() ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        SpeciesTag = speciesTag ?? string.Empty;
        Description = description ?? string.Empty;
        RelationTags = relationTags ?? Array.Empty<string>();
        TradeTags = tradeTags ?? Array.Empty<string>();
        ReinforcementRole = reinforcementRole ?? string.Empty;
        TradeCargo = tradeCargo ?? Array.Empty<FactionCargoLine>();
        SupplyCargo = supplyCargo ?? Array.Empty<FactionCargoLine>();
    }

    public string StableId { get; }
    public string DisplayName { get; }
    public string SpeciesTag { get; }
    public string Description { get; }
    public IReadOnlyList<string> RelationTags { get; }
    public IReadOnlyList<string> TradeTags { get; }
    public string ReinforcementRole { get; }
    public IReadOnlyList<FactionCargoLine> TradeCargo { get; }
    public IReadOnlyList<FactionCargoLine> SupplyCargo { get; }
}

[Serializable]
public sealed class DungeonFactionState
{
    public string factionId = string.Empty;
    [NonSerialized] private int projectedRapport;

    /// <summary>
    /// Compatibility projection for legacy views and events. This value is
    /// deliberately not serialized or owned by the faction aggregate. The
    /// authoritative relationship is the V21 campaign
    /// rapport/grievance/obligation state and the application adapter refreshes
    /// this projection before returning a faction snapshot.
    /// </summary>
    public int trust
    {
        get => projectedRapport;
        set => projectedRapport = value;
    }
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

    public FactionHexCoord HomeCoord => new(homeQ, homeR);
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
    public List<FactionHexCoordSaveData> path =
        new List<FactionHexCoordSaveData>();
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

    public FactionHexCoord CurrentCoord =>
        path != null && path.Count > 0
            ? path[Math.Clamp(pathIndex, 0, path.Count - 1)].ToCoord()
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
        Strength = Math.Clamp(strength, 0, 100);
    }

    public string RouteId { get; }
    public string FactionId { get; }
    public FactionRouteKind Kind { get; }
    public int Strength { get; }
}

public interface IFactionContractQuery
{
    bool IsContractUnlocked(string factionId, FactionContractKind contract);
}

public interface IFactionRuntime : IFactionContractQuery
{
    IReadOnlyList<FactionDefinitionSnapshot> Definitions { get; }
    IReadOnlyList<DungeonFactionState> Factions { get; }
    IReadOnlyList<FactionRouteState> Routes { get; }
    bool TryGetFaction(string factionId, out DungeonFactionState faction);
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
    FactionRestoreCandidate PrepareRestoreCandidate(
        DungeonFactionSaveData saveData);
    void PublishRestoreCandidate(FactionRestoreCandidate candidate);
    void Reset();
}
}
