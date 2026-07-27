using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseExpeditionPhase
{
    ChoosingRoute,
    ResolvingNode,
    InBattle,
    Completed,
    Retreated,
    Defeated
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseRouteNodeKind
{
    Entrance,
    Battle,
    Event,
    Camp,
    Cache,
    Boss
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseSupplyType
{
    Rations,
    Medicine,
    Tools,
    ManaLantern
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseFormationSlot
{
    Front,
    Middle,
    Rear
}

[Flags]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseFormationMask
{
    None = 0,
    Front = 1 << 0,
    Middle = 1 << 1,
    Rear = 1 << 2,
    Any = Front | Middle | Rear
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseRouteNode
{
    private readonly IReadOnlyList<string> nextNodeIds;

    public OffenseRouteNode(
        string id,
        int depth,
        int lane,
        OffenseRouteNodeKind kind,
        string title,
        string description,
        float dangerMultiplier,
        IEnumerable<string> nextNodeIds)
    {
        Id = id ?? string.Empty;
        Depth = Mathf.Max(0, depth);
        Lane = Mathf.Max(0, lane);
        Kind = kind;
        Title = title ?? string.Empty;
        Description = description ?? string.Empty;
        DangerMultiplier = Mathf.Max(0.1f, dangerMultiplier);
        this.nextNodeIds = (nextNodeIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public string Id { get; }
    public int Depth { get; }
    public int Lane { get; }
    public OffenseRouteNodeKind Kind { get; }
    public string Title { get; }
    public string Description { get; }
    public float DangerMultiplier { get; }
    public IReadOnlyList<string> NextNodeIds => nextNodeIds;
    public bool StartsBattle =>
        Kind is OffenseRouteNodeKind.Battle or OffenseRouteNodeKind.Boss;
    public bool IsBoss => Kind == OffenseRouteNodeKind.Boss;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseRouteGraph
{
    private readonly Dictionary<string, OffenseRouteNode> nodeById;
    private readonly IReadOnlyList<OffenseRouteNode> nodes;

    public OffenseRouteGraph(
        IEnumerable<OffenseRouteNode> nodes,
        string entranceNodeId)
    {
        OffenseRouteNode[] safeNodes =
            (nodes ?? Array.Empty<OffenseRouteNode>())
            .Where(node => node != null && !string.IsNullOrWhiteSpace(node.Id))
            .OrderBy(node => node.Depth)
            .ThenBy(node => node.Lane)
            .ToArray();
        this.nodes = safeNodes;
        nodeById = safeNodes.ToDictionary(
            node => node.Id,
            StringComparer.Ordinal);
        EntranceNodeId = entranceNodeId ?? string.Empty;
        if (!nodeById.ContainsKey(EntranceNodeId))
        {
            throw new ArgumentException(
                "The route entrance node is missing.",
                nameof(entranceNodeId));
        }
    }

    public string EntranceNodeId { get; }
    public IReadOnlyList<OffenseRouteNode> Nodes => nodes;

    public bool TryGetNode(string nodeId, out OffenseRouteNode node)
    {
        return nodeById.TryGetValue(nodeId ?? string.Empty, out node);
    }

    public IReadOnlyList<OffenseRouteNode> GetNextNodes(string currentNodeId)
    {
        if (!TryGetNode(currentNodeId, out OffenseRouteNode current))
        {
            return Array.Empty<OffenseRouteNode>();
        }

        return current.NextNodeIds
            .Select(id => nodeById.TryGetValue(
                id,
                out OffenseRouteNode node)
                ? node
                : null)
            .Where(node => node != null)
            .OrderBy(node => node.Lane)
            .ToArray();
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseSupplyLoadout
{
    private readonly Dictionary<OffenseSupplyType, int> amounts;
    private readonly IReadOnlyDictionary<OffenseSupplyType, int> view;

    public OffenseSupplyLoadout(
        IReadOnlyDictionary<OffenseSupplyType, int> initial = null)
    {
        amounts = Enum.GetValues(typeof(OffenseSupplyType))
            .Cast<OffenseSupplyType>()
            .ToDictionary(type => type, _ => 0);
        if (initial != null)
        {
            foreach (KeyValuePair<OffenseSupplyType, int> pair in initial)
            {
                amounts[pair.Key] = Mathf.Max(0, pair.Value);
            }
        }

        view = new ReadOnlyDictionary<OffenseSupplyType, int>(amounts);
    }

    public IReadOnlyDictionary<OffenseSupplyType, int> Amounts => view;
    public int TotalCount => amounts.Values.Sum();

    public int Get(OffenseSupplyType type)
    {
        return amounts.TryGetValue(type, out int value) ? value : 0;
    }

    public void Add(OffenseSupplyType type, int amount)
    {
        if (amount > 0)
        {
            amounts[type] = Get(type) + amount;
        }
    }

    public bool TryConsume(OffenseSupplyType type, int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount == 0)
        {
            return true;
        }

        if (Get(type) < safeAmount)
        {
            return false;
        }

        amounts[type] -= safeAmount;
        return true;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseExpeditionPreparation
{
    public OffenseExpeditionPreparation(
        int supplyCapacity = 7,
        float startingLight = 45f,
        float campHealRatio = 0.12f,
        float campStressRecovery = 12f,
        float medicineHealRatio = 0.25f,
        int scouting = 0,
        IEnumerable<string> sourceSummaries = null)
    {
        SupplyCapacity = Mathf.Max(0, supplyCapacity);
        StartingLight = Mathf.Clamp(startingLight, 0f, 100f);
        CampHealRatio = Mathf.Clamp01(campHealRatio);
        CampStressRecovery = Mathf.Max(0f, campStressRecovery);
        MedicineHealRatio = Mathf.Clamp01(medicineHealRatio);
        Scouting = Mathf.Max(0, scouting);
        SourceSummaries = Array.AsReadOnly(
            (sourceSummaries ?? Array.Empty<string>()).ToArray());
    }

    public int SupplyCapacity { get; }
    public float StartingLight { get; }
    public float CampHealRatio { get; }
    public float CampStressRecovery { get; }
    public float MedicineHealRatio { get; }
    public int Scouting { get; }
    public IReadOnlyList<string> SourceSummaries { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseExpeditionNodeResult
{
    public OffenseExpeditionNodeResult(
        string message,
        bool usedSupply,
        bool gainedLoot)
    {
        Message = message ?? string.Empty;
        UsedSupply = usedSupply;
        GainedLoot = gainedLoot;
    }

    public string Message { get; }
    public bool UsedSupply { get; }
    public bool GainedLoot { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseTargetKind
{
    RivalDungeon,
    HumanOutpost,
    ResourceSite,
    SpecialEvent
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseRewardCategory
{
    Money,
    Stock,
    RareFacility,
    Blueprint,
    StrategicPressure,
    RecruitCandidate,
    Prisoner
}

public static class OffenseRewardTypeIds
{
    public const string Money = "offense.reward.money";
    public const string Stock = "offense.reward.stock";
    public const string RareFacility = "offense.reward.rare-facility";
    public const string Blueprint = "offense.reward.blueprint";
    public const string RegionalPressure =
        "offense.reward.regional-pressure";
    public const string RecruitCandidate =
        "offense.reward.candidate.recruit";
    public const string Prisoner = "offense.reward.candidate.prisoner";
    public const string SpecialMonster =
        "offense.reward.candidate.special-monster";
}

public static class OffenseStrategyBlueprintIds
{
    public const int CommerceLogistics = 6191;
    public const int FortressDefense = 6192;
    public const int ArcaneResearch = 6193;
}
