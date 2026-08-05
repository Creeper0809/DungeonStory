using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

[Serializable]
public sealed class InvasionCampaignSaveData
{
    public int currentDay = 1;
    public int operationSequence;
    public List<HumanInvasionBranchState> branches =
        new List<HumanInvasionBranchState>();
    public List<HumanSupportSiteState> supportSites =
        new List<HumanSupportSiteState>();
    public List<ScheduledInvasionOperationState> operations =
        new List<ScheduledInvasionOperationState>();
}

public interface IInvasionCampaignRuntime
{
    IReadOnlyList<HumanInvasionBranchState> Branches { get; }
    IReadOnlyList<HumanSupportSiteState> SupportSites { get; }
    IReadOnlyList<ScheduledInvasionOperationState> Operations { get; }
    bool TryGetBranch(string branchId, out HumanInvasionBranchState branch);
    bool TryDestroySupportSite(string siteId, out string message);
    ScheduledInvasionOperationState ScheduleNextOperation(float threat);
    float GetBranchStrengthMultiplier(string branchId);
    InvasionCampaignSaveData Capture();
    void ReplaceFromValidatedSnapshot(InvasionCampaignSaveData state);
    void PublishRestoreProjection();
    void RollbackPublishedRestoreProjection();
    void CompleteRestoreProjection();
}

public sealed class InvasionCampaignRuntime :
    IInvasionCampaignRuntime,
    IStartable,
    IDisposable
{
    private const float RecoveryPerConnectedSitePerDay = 1f;

    private readonly IOffenseWorldSimulation world;
    private readonly IGameEventBus events;
    private readonly IRandomStream random;
    private readonly InvasionAggregateStateStore aggregateStateStore;
    private IDisposable daySubscription;
    private bool syncingWorldSites;
    private bool restoreProjectionPublicationPending;
    private InvasionCampaignAggregateState State =>
        aggregateStateStore.Campaign;
    private Dictionary<string, HumanInvasionBranchState> branches =>
        State.Branches;
    private List<HumanSupportSiteState> supportSites => State.SupportSites;
    private List<ScheduledInvasionOperationState> operations => State.Operations;
    private int currentDay
    {
        get => State.CurrentDay;
        set => State.CurrentDay = value;
    }
    private int operationSequence
    {
        get => State.OperationSequence;
        set => State.OperationSequence = value;
    }

    public InvasionCampaignRuntime(
        IOffenseWorldSimulation world,
        IGameEventBus events,
        IRandomStreamProvider randomStreams,
        InvasionAggregateStateStore aggregateStateStore)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.aggregateStateStore = aggregateStateStore
            ?? throw new ArgumentNullException(nameof(aggregateStateStore));
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("invasion:campaign");
    }

    public IReadOnlyList<HumanInvasionBranchState> Branches =>
        branches.Values.OrderBy(value => value.branchId, StringComparer.Ordinal)
            .ToArray();
    public IReadOnlyList<HumanSupportSiteState> SupportSites => supportSites;
    public IReadOnlyList<ScheduledInvasionOperationState> Operations => operations;

    public void Start()
    {
        EnsureInitialized();
        world.Changed += OnWorldChanged;
        SynchronizeWorldSites();
        daySubscription = events.Subscribe<OperatingDayStartedEvent>(OnDayStarted);
    }

    public void Dispose()
    {
        world.Changed -= OnWorldChanged;
        daySubscription?.Dispose();
        daySubscription = null;
    }

    public bool TryGetBranch(
        string branchId,
        out HumanInvasionBranchState branch)
    {
        EnsureInitialized();
        return branches.TryGetValue(branchId?.Trim() ?? string.Empty, out branch);
    }

    public bool TryDestroySupportSite(string siteId, out string message)
    {
        HumanSupportSiteState site = supportSites.FirstOrDefault(value =>
            value != null
            && value.alive
            && string.Equals(value.siteId, siteId, StringComparison.Ordinal));
        if (site == null)
        {
            message = "파괴할 수 있는 인간 지원 거점이 없습니다.";
            return false;
        }

        site.alive = false;
        site.connected = false;
        site.destroyedDay = currentDay;
        if (TryGetBranch(site.branchId, out HumanInvasionBranchState branch))
        {
            branch.strength = Mathf.Max(0f, branch.strength - 25f);
            branch.operational = branch.strength > 0f;
            branch.recoveryReason = "지원 거점 파괴로 전력 25 감소";
            branch.lastRecoveryAmount = 0f;
        }

        message = $"{site.displayName} 파괴 · {BranchName(site.branchId)} 약화";
        return true;
    }

    public ScheduledInvasionOperationState ScheduleNextOperation(float threat)
    {
        EnsureInitialized();
        HumanInvasionBranchState primary = Branches
            .Where(value => value.operational)
            .OrderByDescending(value => value.strength
                + random.NextFloat() * Mathf.Clamp(threat, 0f, 100f) * 0.2f)
            .ThenBy(value => value.branchId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (primary == null)
        {
            return null;
        }

        ScheduledInvasionOperationState operation =
            new ScheduledInvasionOperationState
            {
                operationId = $"human-operation:{++operationSequence}",
                kind = ResolveOperation(primary.branchId, threat),
                primaryBranchId = primary.branchId,
                participatingBranchIds = SelectParticipants(primary.branchId),
                objectiveId = ResolveObjective(primary.branchId),
                scheduledDay = currentDay,
                intelligenceConfidence = Mathf.Clamp01(
                    0.35f + GetStrength(primary.branchId) / 200f)
            };
        operations.Add(operation);
        if (operations.Count > 20)
        {
            operations.RemoveAt(0);
        }
        return operation;
    }

    public float GetBranchStrengthMultiplier(string branchId)
    {
        return TryGetBranch(branchId, out HumanInvasionBranchState branch)
            ? Mathf.Lerp(0.35f, 1.35f, branch.strength / 100f)
            : 1f;
    }

    public InvasionCampaignSaveData Capture()
    {
        EnsureInitialized();
        return new InvasionCampaignSaveData
        {
            currentDay = currentDay,
            operationSequence = operationSequence,
            branches = Branches.Select(Clone).ToList(),
            supportSites = supportSites.Select(Clone).ToList(),
            operations = operations.Select(Clone).ToList()
        };
    }

    public void ReplaceFromValidatedSnapshot(InvasionCampaignSaveData state)
    {
        if (state?.branches == null
            || state.supportSites == null
            || state.operations == null)
        {
            throw new ArgumentException(
                "Validated invasion campaign snapshot is required.",
                nameof(state));
        }
        branches.Clear();
        supportSites.Clear();
        operations.Clear();
        currentDay = state.currentDay;
        operationSequence = state.operationSequence;
        foreach (HumanInvasionBranchState branch in state.branches)
        {
            branches.Add(branch.branchId, Clone(branch));
        }
        supportSites.AddRange(state.supportSites.Select(Clone));
        operations.AddRange(state.operations.Select(Clone));
        if (!aggregateStateStore.IsRestoreStaging)
        {
            SynchronizeWorldSites();
        }
    }

    public void PublishRestoreProjection()
    {
        if (!aggregateStateStore.IsRestoreStaging)
        {
            throw new InvalidOperationException(
                "Invasion campaign projection requires restore staging.");
        }
        if (restoreProjectionPublicationPending)
        {
            throw new InvalidOperationException(
                "An invasion campaign projection is already awaiting completion.");
        }

        restoreProjectionPublicationPending = true;
    }

    public void RollbackPublishedRestoreProjection()
    {
        restoreProjectionPublicationPending = false;
    }

    public void CompleteRestoreProjection()
    {
        if (!restoreProjectionPublicationPending)
        {
            return;
        }

        restoreProjectionPublicationPending = false;
        SynchronizeWorldSites();
    }

    private void OnDayStarted(OperatingDayStartedEvent value)
    {
        currentDay = Mathf.Max(1, value.day);
        foreach (HumanInvasionBranchState branch in branches.Values)
        {
            int connectedSites = supportSites.Count(site =>
                site.alive && site.connected && site.branchId == branch.branchId);
            float recovery = connectedSites * RecoveryPerConnectedSitePerDay;
            branch.lastRecoveryAmount = recovery;
            branch.recoveryReason = connectedSites > 0
                ? $"연결된 지원 거점 {connectedSites}개"
                : "지원 거점 없음: 자연 회복 중단";
            if (recovery > 0f)
            {
                branch.strength = Mathf.Clamp(
                    branch.strength + recovery,
                    0f,
                    100f);
                branch.operational = branch.strength > 0f;
            }
        }
    }

    private void EnsureInitialized()
    {
        if (branches.Count > 0)
        {
            return;
        }

        AddBranch(HumanInvasionBranchIds.RoyalArmy, "왕실 원정군");
        AddBranch(HumanInvasionBranchIds.PioneerSupply, "개척 보급국");
        AddBranch(HumanInvasionBranchIds.RoyalOrdnance, "왕립 병기청");
        AddBranch(HumanInvasionBranchIds.IntelligenceHunters, "첩보 사냥단");
        AddBranch(HumanInvasionBranchIds.RadiantOrder, "성광 교단");
        CreateSupportSites();
    }

    private void AddBranch(string id, string name)
    {
        branches[id] = new HumanInvasionBranchState
        {
            branchId = id,
            displayName = name,
            strength = 70f,
            operational = true
        };
    }

    private void CreateSupportSites()
    {
        HashSet<OffenseHexCoord> occupied = world.Sites
            .Where(site => site != null && site.IsActive)
            .Select(site => site.Coord)
            .ToHashSet();
        OffenseHexTileState[] candidates = world.Tiles
            .Where(tile => tile != null
                && !tile.blocked
                && tile.Coord.DistanceTo(world.DungeonCoord) >= 5
                && !occupied.Contains(tile.Coord))
            .OrderBy(tile => tile.Coord)
            .ToArray();
        int index = 0;
        foreach (HumanInvasionBranchState branch in Branches)
        {
            for (int siteIndex = 0; siteIndex < 2; siteIndex++)
            {
                OffenseHexCoord coord = candidates.Length > 0
                    ? candidates[(index++ * 7 + 3) % candidates.Length].Coord
                    : new OffenseHexCoord(index + 4, -index);
                HumanSupportSiteState supportSite = new HumanSupportSiteState
                {
                    siteId = $"human-support:{branch.branchId}:{siteIndex}",
                    branchId = branch.branchId,
                    displayName = $"{branch.displayName} 지원 거점 {siteIndex + 1}",
                    q = coord.Q,
                    r = coord.R,
                    alive = true,
                    connected = true
                };
                supportSites.Add(supportSite);
                occupied.Add(coord);
            }
        }

        SynchronizeWorldSites();
    }

    private void OnWorldChanged()
    {
        if (syncingWorldSites)
        {
            return;
        }

        foreach (HumanSupportSiteState supportSite in supportSites
                     .Where(value => value != null && value.alive)
                     .ToArray())
        {
            if (world.TryGetSite(
                    supportSite.siteId,
                    out OffenseWorldSiteStateData worldSite)
                && worldSite.state == OffenseWorldSiteState.Resolved)
            {
                TryDestroySupportSite(supportSite.siteId, out _);
            }
        }

        SynchronizeWorldSites();
    }

    private void SynchronizeWorldSites()
    {
        if (syncingWorldSites || world.Tiles == null || world.Tiles.Count == 0)
        {
            return;
        }

        syncingWorldSites = true;
        try
        {
            foreach (HumanSupportSiteState supportSite in supportSites
                         .Where(value => value != null && value.alive))
            {
                world.TryRegisterStrategicSite(
                    CreateWorldSite(supportSite));
            }
        }
        finally
        {
            syncingWorldSites = false;
        }
    }

    private OffenseWorldSiteStateData CreateWorldSite(
        HumanSupportSiteState supportSite)
    {
        return new OffenseWorldSiteStateData
        {
            siteId = supportSite.siteId,
            archetypeId = ResolveSiteArchetype(supportSite.branchId),
            displayName = supportSite.displayName,
            q = supportSite.q,
            r = supportSite.r,
            regionId = "region:human-campaign",
            factionId = OffenseRegionRuntime.HumanFactionId,
            state = OffenseWorldSiteState.Revealed,
            fixedBoss = false,
            strength = 5,
            createdDay = currentDay,
            expiresDay = int.MaxValue,
            pressureAxis = ResolvePressureAxis(supportSite.branchId),
            pressureAmount = 25f
        };
    }

    private static string ResolveSiteArchetype(string branchId)
    {
        return branchId switch
        {
            HumanInvasionBranchIds.RoyalArmy => "patrol",
            HumanInvasionBranchIds.PioneerSupply => "caravan",
            HumanInvasionBranchIds.RoyalOrdnance => "armory",
            HumanInvasionBranchIds.IntelligenceHunters => "watchtower",
            HumanInvasionBranchIds.RadiantOrder => "ritual_site",
            _ => "ruin"
        };
    }

    private static StrategicPressureAxis ResolvePressureAxis(string branchId)
    {
        return branchId switch
        {
            HumanInvasionBranchIds.PioneerSupply =>
                StrategicPressureAxis.Logistics,
            HumanInvasionBranchIds.RoyalOrdnance =>
                StrategicPressureAxis.Armament,
            HumanInvasionBranchIds.IntelligenceHunters =>
                StrategicPressureAxis.Intelligence,
            HumanInvasionBranchIds.RadiantOrder =>
                StrategicPressureAxis.Intelligence,
            _ => StrategicPressureAxis.Manpower
        };
    }

    private List<string> SelectParticipants(string primaryId)
    {
        return Branches
            .Where(value => value.operational
                && (value.branchId == primaryId || value.strength >= 65f))
            .OrderByDescending(value => value.branchId == primaryId)
            .ThenByDescending(value => value.strength)
            .Take(3)
            .Select(value => value.branchId)
            .ToList();
    }

    private InvasionOperationKind ResolveOperation(
        string branchId,
        float threat)
    {
        if (branchId == HumanInvasionBranchIds.RoyalArmy)
            return InvasionOperationKind.FrontalAssault;
        if (branchId == HumanInvasionBranchIds.PioneerSupply)
            return threat >= 80f
                ? InvasionOperationKind.Siege
                : InvasionOperationKind.Loot;
        if (branchId == HumanInvasionBranchIds.RoyalOrdnance)
            return InvasionOperationKind.Siege;
        if (branchId == HumanInvasionBranchIds.IntelligenceHunters)
            return threat >= 75f
                ? InvasionOperationKind.OwnerAssassination
                : InvasionOperationKind.FacilitySabotage;
        return InvasionOperationKind.CaptiveRescue;
    }

    private static string ResolveObjective(string branchId)
    {
        return branchId switch
        {
            HumanInvasionBranchIds.PioneerSupply => "stockpile",
            HumanInvasionBranchIds.RoyalOrdnance => "defense-facility",
            HumanInvasionBranchIds.IntelligenceHunters => "owner-or-power",
            HumanInvasionBranchIds.RadiantOrder => "captives-or-curse",
            _ => "breach-and-occupy"
        };
    }

    private float GetStrength(string branchId)
    {
        return TryGetBranch(branchId, out HumanInvasionBranchState branch)
            ? branch.strength
            : 0f;
    }

    private string BranchName(string branchId)
    {
        return TryGetBranch(branchId, out HumanInvasionBranchState branch)
            ? branch.displayName
            : branchId;
    }

    private static HumanInvasionBranchState Clone(
        HumanInvasionBranchState value) =>
        JsonUtility.FromJson<HumanInvasionBranchState>(
            JsonUtility.ToJson(value));

    private static HumanSupportSiteState Clone(
        HumanSupportSiteState value) =>
        JsonUtility.FromJson<HumanSupportSiteState>(
            JsonUtility.ToJson(value));

    private static ScheduledInvasionOperationState Clone(
        ScheduledInvasionOperationState value) =>
        JsonUtility.FromJson<ScheduledInvasionOperationState>(
            JsonUtility.ToJson(value));
}
