using System;
using System.Collections.Generic;
using System.Linq;

public interface IOffenseSaveService
{
    DungeonOffenseSaveData Capture();
    OffenseExpeditionRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseSaveData source,
        DungeonGameRestoreReport report,
        IReadOnlyList<OffenseRegionState> restoredRegions,
        IOffenseWorldMapStateView campaignState);
    void PublishRestoreCandidate(
        OffenseExpeditionRestoreCandidate candidate);
}

public sealed class OffenseExpeditionRestoreCandidate
{
    internal OffenseRewardState Rewards { get; set; }
    internal List<OffenseExpeditionRun> ActiveExpeditions { get; set; }
    internal List<OffenseExpeditionResult> ResultHistory { get; set; }
    internal OffenseBattleRestoreCandidate Battle { get; set; }
}

[Serializable]
public sealed class DungeonOffenseSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public DungeonOffenseRewardSaveData rewards = new DungeonOffenseRewardSaveData();
    public List<DungeonOffenseExpeditionRunSaveData> activeExpeditions =
        new List<DungeonOffenseExpeditionRunSaveData>();
    public List<DungeonOffenseExpeditionResultSaveData> resultHistory =
        new List<DungeonOffenseExpeditionResultSaveData>();
    public bool hasActiveBattle;
    public OffenseBattlePersistenceState activeBattle;
}

[Serializable]
public sealed class DungeonOffenseRewardSaveData
{
    public int moneyEarned;
    public List<DungeonOffenseStockRewardSaveData> stockGranted =
        new List<DungeonOffenseStockRewardSaveData>();
    public List<int> rareFacilityBuildingIds = new List<int>();
    public List<int> acquiredBlueprintIds = new List<int>();
}

[Serializable]
public sealed class DungeonOffenseStockRewardSaveData
{
    public StockCategory category;
    public int amount;
}

[Serializable]
public sealed class DungeonOffenseExpeditionRunSaveData
{
    public const int CurrentVersion = 2;

    public int journeyVersion;
    public string expeditionId = string.Empty;
    public string targetId = string.Empty;
    public float totalPower;
    public float remainingSeconds;
    public List<string> memberPersistentIds = new List<string>();
    public List<string> protectedRescueMemberPersistentIds = new List<string>();
    public OffenseExpeditionPhase phase;
    public string currentNodeId = string.Empty;
    public float light;
    public List<string> completedNodeIds = new List<string>();
    public List<DungeonOffenseSupplySaveData> supplies = new List<DungeonOffenseSupplySaveData>();
    public List<DungeonOffenseExpeditionMemberStateSaveData> memberStates =
        new List<DungeonOffenseExpeditionMemberStateSaveData>();
    public List<DungeonOffenseStockRewardSaveData> carriedStock =
        new List<DungeonOffenseStockRewardSaveData>();
    public int supplyCapacity;
    public float startingLight;
    public float campHealRatio;
    public float campStressRecovery;
    public float medicineHealRatio;
    public int scouting;
    public List<string> preparationSources = new List<string>();
    public bool usesWorldTravel;
    public string worldSiteId = string.Empty;
    public bool worldObjectiveCompleted;
    public bool worldObjectiveBattleActive;
    public bool departureCompleted;
    public OffenseTargetDefinition worldTarget;
    public int fieldFunds;
    public bool fieldFundsReturned;
}

[Serializable]
public sealed class DungeonOffenseSupplySaveData
{
    public OffenseSupplyType type;
    public int amount;
}

[Serializable]
public sealed class DungeonOffenseExpeditionMemberStateSaveData
{
    public string persistentId = string.Empty;
    public OffenseFormationSlot formation;
    public float stress;
    public float totalDamageTaken;
}

[Serializable]
public sealed class DungeonOffenseExpeditionResultSaveData
{
    public string expeditionId = string.Empty;
    public string targetId = string.Empty;
    public string targetTitle = string.Empty;
    public bool success;
    public float totalPower;
    public float requiredPower;
    public float danger;
    public float elapsedSeconds;
    public List<DungeonOffenseExpeditionMemberResultSaveData> members =
        new List<DungeonOffenseExpeditionMemberResultSaveData>();
    public List<string> rewardSummaries = new List<string>();
}

[Serializable]
public sealed class DungeonOffenseExpeditionMemberResultSaveData
{
    public string name = string.Empty;
    public string speciesTag = string.Empty;
    public float power;
    public bool survived;
    public float damageTaken;
}

public sealed class OffenseSaveService : IOffenseSaveService
{
    private readonly IOffenseCampaignCatalog campaignDefinitions;
    private readonly OffenseRewardRuntime rewardRuntime;
    private readonly OffenseExpeditionRuntime expeditionRuntime;
    private readonly ICharacterWorldSaveService characterSaveService;
    private readonly IOffenseBattleRuntime battleRuntime;

    public OffenseSaveService(
        OffenseSceneRuntimeReferences runtimeReferences,
        IOffenseCampaignCatalog campaignDefinitions,
        ICharacterWorldSaveService characterSaveService,
        IOffenseBattleRuntime battleRuntime)
    {
        runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
        this.campaignDefinitions = campaignDefinitions
            ?? throw new ArgumentNullException(nameof(campaignDefinitions));
        rewardRuntime = runtimeReferences.Rewards
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseSaveService)} requires a loaded {nameof(OffenseRewardRuntime)}.");
        expeditionRuntime = runtimeReferences.Expedition
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseSaveService)} requires a loaded {nameof(OffenseExpeditionRuntime)}.");
        this.characterSaveService = characterSaveService
            ?? throw new ArgumentNullException(nameof(characterSaveService));
        this.battleRuntime = battleRuntime
            ?? throw new ArgumentNullException(nameof(battleRuntime));
    }

    public DungeonOffenseSaveData Capture()
    {
        DungeonOffenseSaveData result = new DungeonOffenseSaveData();
        if (rewardRuntime != null)
        {
            IOffenseRewardStateView state = rewardRuntime.State;
            result.rewards = new DungeonOffenseRewardSaveData
            {
                moneyEarned = state.MoneyEarned,
                stockGranted = state.StockGrantedByCategory
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new DungeonOffenseStockRewardSaveData
                    {
                        category = pair.Key,
                        amount = pair.Value
                    })
                    .ToList(),
                rareFacilityBuildingIds = state.RareFacilityBuildingIds.OrderBy(id => id).ToList(),
                acquiredBlueprintIds = state.AcquiredBlueprintIds.OrderBy(id => id).ToList()
            };
        }

        if (expeditionRuntime != null)
        {
            result.activeExpeditions = expeditionRuntime.ActiveExpeditions
                .Where(expedition => expedition?.Target != null)
                .Select(CaptureExpedition)
                .ToList();
            result.resultHistory = expeditionRuntime.ResultHistory
                .Where(expeditionResult => expeditionResult != null)
                .Select(CaptureResult)
                .ToList();
        }

        OffenseBattlePersistenceState activeBattle = battleRuntime.CapturePersistentState();
        result.activeBattle = activeBattle != null
            && result.activeExpeditions.Any(expedition => expedition != null
                && expedition.phase == OffenseExpeditionPhase.InBattle
                && string.Equals(expedition.expeditionId, activeBattle.expeditionId, StringComparison.Ordinal))
                ? activeBattle
                : null;
        result.hasActiveBattle = result.activeBattle != null;

        return result;
    }

    public OffenseExpeditionRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseSaveData source,
        DungeonGameRestoreReport report,
        IReadOnlyList<OffenseRegionState> restoredRegions,
        IOffenseWorldMapStateView campaignState)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (source == null
            || source.version != DungeonOffenseSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported offense expedition payload version {source?.version.ToString() ?? "null"}; expected {DungeonOffenseSaveData.CurrentVersion}.");
        }

        campaignState = campaignState
            ?? throw new ArgumentNullException(nameof(campaignState));

        DungeonOffenseRewardSaveData rewards = source.rewards;
        Dictionary<StockCategory, int> stock = rewards.stockGranted
            .ToDictionary(entry => entry.category, entry => entry.amount);
        OffenseRewardState rewardCandidate =
            OffenseRewardRuntime.PreparePersistentState(
            rewards.moneyEarned,
            stock,
            rewards.rareFacilityBuildingIds,
            rewards.acquiredBlueprintIds);

        Dictionary<string, OffenseTargetDefinition> targets = campaignDefinitions.Targets
            .Where(target => target != null && !string.IsNullOrWhiteSpace(target.id))
            .GroupBy(target => target.id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        List<OffenseExpeditionRun> activeRuns = new List<OffenseExpeditionRun>();
        foreach (DungeonOffenseExpeditionRunSaveData savedRun in source.activeExpeditions)
        {
            bool isStrategic = savedRun.usesWorldTravel
                || !string.IsNullOrWhiteSpace(savedRun.worldSiteId);
            OffenseTargetDefinition target = isStrategic
                ? savedRun.worldTarget?.CreateRuntimeCopy()
                : targets.TryGetValue(
                    savedRun.targetId,
                    out OffenseTargetDefinition campaignTarget)
                    ? campaignTarget
                    : null;
            if (target == null || !target.IsValid)
            {
                throw new InvalidOperationException(
                    $"Offense target '{savedRun.targetId}' no longer exists; "
                    + "the save cannot be restored.");
            }

            if (!isStrategic
                && (campaignState.TruthRevealed
                    || campaignState.IsTargetCompleted(target.id)))
            {
                throw new InvalidOperationException(
                    $"Expedition '{savedRun.expeditionId}' targets an already completed campaign objective.");
            }

            List<CharacterActor> members = new List<CharacterActor>();
            List<CharacterActor> protectedRescueMembers = new List<CharacterActor>();
            bool departureCompleted = !isStrategic
                || savedRun.departureCompleted;
            foreach (string persistentId in savedRun.memberPersistentIds)
            {
                if (characterSaveService.TryGetRestoredActor(persistentId, out CharacterActor actor))
                {
                    if (departureCompleted)
                    {
                        actor.BeginExpedition();
                    }
                    members.Add(actor);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Expedition member '{persistentId}' does not exist in the staged character world.");
                }
            }

            foreach (string persistentId in savedRun.protectedRescueMemberPersistentIds)
            {
                if (characterSaveService.TryGetRestoredActor(
                        persistentId,
                        out CharacterActor actor))
                {
                    if (departureCompleted)
                    {
                        actor.BeginExpedition();
                    }
                    protectedRescueMembers.Add(actor);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Protected rescue member '{persistentId}' does not exist in the staged character world.");
                }
            }

            if (members.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Expedition '{savedRun.expeditionId}' has no staged members.");
            }

            Dictionary<OffenseSupplyType, int> restoredSupplies = savedRun.supplies
                .ToDictionary(entry => entry.type, entry => entry.amount);
            OffenseExpeditionPreparation preparation =
                new OffenseExpeditionPreparation(
                    savedRun.supplyCapacity,
                    savedRun.startingLight,
                    savedRun.campHealRatio,
                    savedRun.campStressRecovery,
                    savedRun.medicineHealRatio,
                    savedRun.scouting,
                    savedRun.preparationSources,
                    savedRun.fieldFunds);
            OffenseRouteGraph route = OffenseRouteGenerator.Create(target);
            if (!route.TryGetNode(savedRun.currentNodeId, out _)
                || !savedRun.completedNodeIds.Contains(
                    route.EntranceNodeId,
                    StringComparer.Ordinal)
                || savedRun.completedNodeIds.Any(nodeId =>
                    !route.TryGetNode(nodeId, out _)))
            {
                throw new InvalidOperationException(
                    $"Expedition '{savedRun.expeditionId}' references a route node that is not present in its authored route.");
            }
            OffenseExpeditionRun restoredRun = new OffenseExpeditionRun(
                savedRun.expeditionId,
                target,
                members,
                savedRun.totalPower,
                savedRun.remainingSeconds,
                route,
                new OffenseSupplyLoadout(restoredSupplies),
                preparation);
            restoredRun.MergeProtectedRescueMembers(protectedRescueMembers);

            string currentNodeId = savedRun.currentNodeId;
            OffenseExpeditionPhase phase = savedRun.phase;
            float light = savedRun.light;
            IEnumerable<string> completedNodes = savedRun.completedNodeIds;
            Dictionary<StockCategory, int> carriedStock = savedRun.carriedStock
                .ToDictionary(entry => entry.category, entry => entry.amount);
            restoredRun.RestoreJourneyState(
                phase,
                currentNodeId,
                light,
                completedNodes,
                carriedStock);
            restoredRun.RestoreFieldFunds(
                savedRun.fieldFunds,
                savedRun.fieldFundsReturned);
            if (isStrategic)
            {
                restoredRun.RestoreStrategicJourneyState(
                    string.IsNullOrWhiteSpace(savedRun.worldSiteId)
                        ? target.id
                        : savedRun.worldSiteId,
                    savedRun.worldObjectiveCompleted,
                    savedRun.worldObjectiveBattleActive,
                    phase);
                restoredRun.RestoreDepartureState(departureCompleted);
            }

            Dictionary<string, DungeonOffenseExpeditionMemberStateSaveData> memberStateById =
                savedRun.memberStates.ToDictionary(
                    entry => entry.persistentId,
                    entry => entry,
                    StringComparer.Ordinal);
            foreach (OffenseExpeditionMemberState memberState in restoredRun.MemberStates)
            {
                if (!characterSaveService.TryGetPersistentId(memberState.Actor, out string persistentId)
                    || !memberStateById.TryGetValue(persistentId, out DungeonOffenseExpeditionMemberStateSaveData savedMember))
                {
                    throw new InvalidOperationException(
                        $"Expedition '{savedRun.expeditionId}' member state cannot be bound to a staged character.");
                }

                memberState.Restore(
                    savedMember.formation,
                    savedMember.stress,
                    savedMember.totalDamageTaken);
            }

            activeRuns.Add(restoredRun);
        }

        List<OffenseExpeditionResult> history = source.resultHistory
            .Select(RestoreResult)
            .ToList();
        OffenseBattlePersistenceState savedBattle = source.hasActiveBattle
            ? source.activeBattle
            : null;
        OffenseExpeditionRun restoredBattleRun = null;
        if (savedBattle != null)
        {
            restoredBattleRun = activeRuns.FirstOrDefault(run => string.Equals(
                run.ExpeditionId,
                savedBattle.expeditionId,
                StringComparison.Ordinal));
            if (restoredBattleRun == null)
            {
                throw new InvalidOperationException(
                    "The saved offense battle has no matching active expedition.");
            }
        }
        else
        {
            restoredBattleRun = activeRuns.FirstOrDefault();
        }

        List<OffenseExpeditionRun> restoredRuns = activeRuns;

        OffenseBattleRuntime concreteBattle = battleRuntime as OffenseBattleRuntime
            ?? throw new InvalidOperationException(
                "Offense save restore requires the canonical battle runtime.");
        OffenseBattleRestoreCandidate battleCandidate =
            concreteBattle.PrepareEmptyPersistentRestore();
        if (restoredBattleRun != null)
        {
            if (restoredBattleRun.UsesWorldTravel)
            {
                if (savedBattle != null)
                {
                    throw new InvalidOperationException(
                        "A strategic world-travel expedition cannot own a turn battle payload.");
                }
            }
            else if (restoredBattleRun.Phase == OffenseExpeditionPhase.InBattle)
            {
                if (savedBattle == null)
                {
                    throw new InvalidOperationException(
                        $"Expedition '{restoredBattleRun.ExpeditionId}' is in battle without a battle payload.");
                }
                battleCandidate = concreteBattle.PreparePersistentRestore(
                    restoredBattleRun,
                    savedBattle,
                    restoredRegions != null
                        ? OffenseRegionRuntime.CreatePressureForTarget(
                            restoredBattleRun.Target,
                            restoredRegions)
                        : (OffenseStrategicPressureSnapshot?)null);
            }
        }

        return new OffenseExpeditionRestoreCandidate
        {
            Rewards = rewardCandidate,
            ActiveExpeditions = restoredRuns,
            ResultHistory = history,
            Battle = battleCandidate
        };
    }

    public void PublishRestoreCandidate(
        OffenseExpeditionRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        rewardRuntime.PublishPersistentState(candidate.Rewards);
        expeditionRuntime.PublishPersistentState(
            candidate.ActiveExpeditions,
            candidate.ResultHistory);
        ((OffenseBattleRuntime)battleRuntime).PublishPersistentRestore(
            candidate.Battle);
    }

    private DungeonOffenseExpeditionRunSaveData CaptureExpedition(OffenseExpeditionRun expedition)
    {
        return new DungeonOffenseExpeditionRunSaveData
        {
            journeyVersion = DungeonOffenseExpeditionRunSaveData.CurrentVersion,
            expeditionId = expedition.ExpeditionId,
            targetId = expedition.Target.id,
            totalPower = expedition.TotalPower,
            remainingSeconds = expedition.RemainingSeconds,
            memberPersistentIds = expedition.MemberActors
                .Where(member => member != null)
                .Select(member => characterSaveService.TryGetPersistentId(member, out string persistentId)
                    ? persistentId
                    : string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            protectedRescueMemberPersistentIds = expedition.ProtectedRescueActors
                .Where(member => member != null)
                .Select(member => characterSaveService.TryGetPersistentId(
                    member,
                    out string persistentId)
                        ? persistentId
                        : string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            phase = expedition.Phase,
            currentNodeId = expedition.CurrentNodeId,
            light = expedition.Light,
            completedNodeIds = expedition.CompletedNodeIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            supplies = expedition.Supplies.Amounts
                .Where(pair => pair.Value > 0)
                .Select(pair => new DungeonOffenseSupplySaveData { type = pair.Key, amount = pair.Value })
                .ToList(),
            memberStates = expedition.MemberStates
                .Where(member => member?.Actor != null)
                .Select(member => new DungeonOffenseExpeditionMemberStateSaveData
                {
                    persistentId = characterSaveService.TryGetPersistentId(member.Actor, out string persistentId)
                        ? persistentId
                        : string.Empty,
                    formation = member.Formation,
                    stress = member.Stress,
                    totalDamageTaken = member.TotalDamageTaken
                })
                .Where(member => !string.IsNullOrWhiteSpace(member.persistentId))
                .ToList(),
            carriedStock = expedition.CarriedStock
                .Where(pair => pair.Value > 0)
                .Select(pair => new DungeonOffenseStockRewardSaveData
                {
                    category = pair.Key,
                    amount = pair.Value
                })
                .ToList(),
            supplyCapacity = expedition.Preparation.SupplyCapacity,
            startingLight = expedition.Preparation.StartingLight,
            campHealRatio = expedition.Preparation.CampHealRatio,
            campStressRecovery = expedition.Preparation.CampStressRecovery,
            medicineHealRatio = expedition.Preparation.MedicineHealRatio,
            scouting = expedition.Preparation.Scouting,
            preparationSources = expedition.Preparation.SourceSummaries.ToList(),
            usesWorldTravel = expedition.UsesWorldTravel,
            worldSiteId = expedition.WorldSiteId,
            worldObjectiveCompleted = expedition.WorldObjectiveCompleted,
            worldObjectiveBattleActive = expedition.WorldObjectiveBattleActive,
            departureCompleted = expedition.DepartureCompleted,
            worldTarget = expedition.UsesWorldTravel
                ? expedition.Target.CreateRuntimeCopy()
                : null,
            fieldFunds = expedition.FieldFunds,
            fieldFundsReturned = expedition.FieldFundsReturned
        };
    }

    private static DungeonOffenseExpeditionResultSaveData CaptureResult(OffenseExpeditionResult result)
    {
        return new DungeonOffenseExpeditionResultSaveData
        {
            expeditionId = result.expeditionId,
            targetId = result.targetId,
            targetTitle = result.targetTitle,
            success = result.success,
            totalPower = result.totalPower,
            requiredPower = result.requiredPower,
            danger = result.danger,
            elapsedSeconds = result.elapsedSeconds,
            members = result.members
                .Where(member => member != null)
                .Select(member => new DungeonOffenseExpeditionMemberResultSaveData
                {
                    name = member.name,
                    speciesTag = member.speciesTag,
                    power = member.power,
                    survived = member.survived,
                    damageTaken = member.damageTaken
                })
                .ToList(),
            rewardSummaries = result.rewardSummaries.ToList()
        };
    }

    private static OffenseExpeditionResult RestoreResult(DungeonOffenseExpeditionResultSaveData source)
    {
        return new OffenseExpeditionResult(
            source.expeditionId,
            source.targetId,
            source.targetTitle,
            source.success,
            source.totalPower,
            source.requiredPower,
            source.danger,
            source.elapsedSeconds,
            source.members
                .Select(member => new OffenseExpeditionMemberSnapshot(
                    member.name,
                    member.speciesTag,
                    member.power,
                    member.survived,
                    member.damageTaken))
                .ToList(),
            source.rewardSummaries);
    }
}
