using System;
using System.Collections.Generic;
using System.Linq;

public interface IOffenseSaveService
{
    DungeonOffenseSaveData Capture();
    void Restore(DungeonOffenseSaveData source, DungeonGameRestoreReport report);
}

[Serializable]
public sealed class DungeonOffenseSaveData
{
    public int reconLevel;
    public string selectedTargetId = string.Empty;
    public List<string> knownTargetIds = new List<string>();
    public List<string> completedTargetIds = new List<string>();
    public string revealedTruthTargetId = string.Empty;
    public DungeonOffenseRewardSaveData rewards = new DungeonOffenseRewardSaveData();
    public List<DungeonOffenseExpeditionRunSaveData> activeExpeditions =
        new List<DungeonOffenseExpeditionRunSaveData>();
    public List<DungeonOffenseExpeditionResultSaveData> resultHistory =
        new List<DungeonOffenseExpeditionResultSaveData>();
    public OffenseBattlePersistenceState activeBattle;
}

[Serializable]
public sealed class DungeonOffenseRewardSaveData
{
    public int moneyEarned;
    public int humanFactionWeakening;
    public int rivalFactionWeakening;
    public int recruitCandidateCount;
    public int prisonerCount;
    public int specialMonsterCount;
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
    public int journeyVersion;
    public string expeditionId = string.Empty;
    public string targetId = string.Empty;
    public float totalPower;
    public float remainingSeconds;
    public List<string> memberPersistentIds = new List<string>();
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
    private readonly IOffenseWorldMapRuntimeProvider worldMapProvider;
    private readonly IOffenseRewardRuntimeProvider rewardProvider;
    private readonly IOffenseExpeditionRuntimeProvider expeditionProvider;
    private readonly ICharacterWorldSaveService characterSaveService;
    private readonly IOffenseBattleRuntime battleRuntime;

    public OffenseSaveService(
        IOffenseWorldMapRuntimeProvider worldMapProvider,
        IOffenseRewardRuntimeProvider rewardProvider,
        IOffenseExpeditionRuntimeProvider expeditionProvider,
        ICharacterWorldSaveService characterSaveService,
        IOffenseBattleRuntime battleRuntime)
    {
        this.worldMapProvider = worldMapProvider
            ?? throw new ArgumentNullException(nameof(worldMapProvider));
        this.rewardProvider = rewardProvider
            ?? throw new ArgumentNullException(nameof(rewardProvider));
        this.expeditionProvider = expeditionProvider
            ?? throw new ArgumentNullException(nameof(expeditionProvider));
        this.characterSaveService = characterSaveService
            ?? throw new ArgumentNullException(nameof(characterSaveService));
        this.battleRuntime = battleRuntime
            ?? throw new ArgumentNullException(nameof(battleRuntime));
    }

    public DungeonOffenseSaveData Capture()
    {
        DungeonOffenseSaveData result = new DungeonOffenseSaveData();
        if (worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap))
        {
            result.reconLevel = worldMap.State.ReconLevel;
            result.selectedTargetId = worldMap.State.SelectedTargetId;
            result.knownTargetIds = worldMap.State.KnownTargetIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            result.completedTargetIds = worldMap.State.CompletedTargetIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            result.revealedTruthTargetId = worldMap.State.RevealedTruthTargetId;
        }

        if (rewardProvider.TryGetRuntime(out OffenseRewardRuntime rewardRuntime))
        {
            IOffenseRewardStateView state = rewardRuntime.State;
            result.rewards = new DungeonOffenseRewardSaveData
            {
                moneyEarned = state.MoneyEarned,
                humanFactionWeakening = state.HumanFactionWeakening,
                rivalFactionWeakening = state.RivalFactionWeakening,
                recruitCandidateCount = state.RecruitCandidateCount,
                prisonerCount = state.PrisonerCount,
                specialMonsterCount = state.SpecialMonsterCount,
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

        if (expeditionProvider.TryGetRuntime(out OffenseExpeditionRuntime expeditionRuntime))
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

        return result;
    }

    public void Restore(DungeonOffenseSaveData source, DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        source ??= new DungeonOffenseSaveData();
        battleRuntime.ClearForPersistentRestore();
        if (!worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap))
        {
            report.AddWarning("Offense world map runtime was not present; offense state was skipped.");
            return;
        }

        List<string> restoredCompletedTargets = (source.completedTargetIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (restoredCompletedTargets.Count == 0)
        {
            restoredCompletedTargets = (source.resultHistory
                    ?? new List<DungeonOffenseExpeditionResultSaveData>())
                .Where(result => result != null && result.success && !string.IsNullOrWhiteSpace(result.targetId))
                .Select(result => result.targetId)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        worldMap.RestorePersistentState(
            source.reconLevel,
            source.selectedTargetId,
            source.knownTargetIds ?? new List<string>(),
            restoredCompletedTargets,
            source.revealedTruthTargetId);

        if (rewardProvider.TryGetRuntime(out OffenseRewardRuntime rewardRuntime))
        {
            DungeonOffenseRewardSaveData rewards = source.rewards ?? new DungeonOffenseRewardSaveData();
            Dictionary<StockCategory, int> stock = (rewards.stockGranted
                    ?? new List<DungeonOffenseStockRewardSaveData>())
                .Where(entry => entry != null && entry.amount > 0)
                .GroupBy(entry => entry.category)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.amount));
            rewardRuntime.RestorePersistentState(
                rewards.moneyEarned,
                rewards.humanFactionWeakening,
                rewards.rivalFactionWeakening,
                rewards.recruitCandidateCount,
                rewards.prisonerCount,
                rewards.specialMonsterCount,
                stock,
                rewards.rareFacilityBuildingIds ?? new List<int>(),
                rewards.acquiredBlueprintIds ?? new List<int>());
        }
        else
        {
            report.AddWarning("Offense reward runtime was not present; reward history was skipped.");
        }

        if (!expeditionProvider.TryGetRuntime(out OffenseExpeditionRuntime expeditionRuntime))
        {
            report.AddWarning("Offense expedition runtime was not present; expeditions were skipped.");
            return;
        }

        Dictionary<string, OffenseTargetDefinition> targets = worldMap.TargetDefinitions
            .Where(target => target != null && !string.IsNullOrWhiteSpace(target.id))
            .GroupBy(target => target.id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        List<OffenseExpeditionRun> activeRuns = new List<OffenseExpeditionRun>();
        foreach (DungeonOffenseExpeditionRunSaveData savedRun in source.activeExpeditions
            ?? new List<DungeonOffenseExpeditionRunSaveData>())
        {
            if (savedRun == null || !targets.TryGetValue(savedRun.targetId, out OffenseTargetDefinition target))
            {
                report.AddWarning($"Offense target '{savedRun?.targetId}' no longer exists; its active expedition was skipped.");
                continue;
            }

            if (worldMap.State.TruthRevealed || worldMap.State.IsTargetCompleted(target.id))
            {
                report.AddWarning($"Expedition '{savedRun.expeditionId}' targeted an already completed campaign objective and was skipped.");
                continue;
            }

            List<CharacterActor> members = new List<CharacterActor>();
            foreach (string persistentId in savedRun.memberPersistentIds ?? new List<string>())
            {
                if (characterSaveService.TryGetRestoredActor(persistentId, out CharacterActor actor))
                {
                    actor.BeginExpedition();
                    members.Add(actor);
                }
                else
                {
                    report.AddWarning($"Expedition member '{persistentId}' could not be restored.");
                }
            }

            if (members.Count == 0)
            {
                report.AddWarning($"Expedition '{savedRun.expeditionId}' has no restored members and was skipped.");
                continue;
            }

            bool legacyJourney = savedRun.journeyVersion <= 0;
            Dictionary<OffenseSupplyType, int> restoredSupplies = (savedRun.supplies
                    ?? new List<DungeonOffenseSupplySaveData>())
                .Where(entry => entry != null && entry.amount > 0)
                .GroupBy(entry => entry.type)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.amount));
            OffenseExpeditionPreparation preparation = legacyJourney
                ? new OffenseExpeditionPreparation()
                : new OffenseExpeditionPreparation(
                    savedRun.supplyCapacity,
                    savedRun.startingLight,
                    savedRun.campHealRatio,
                    savedRun.campStressRecovery,
                    savedRun.medicineHealRatio,
                    savedRun.scouting,
                    savedRun.preparationSources ?? new List<string>());
            OffenseExpeditionRun restoredRun = new OffenseExpeditionRun(
                savedRun.expeditionId,
                target,
                members,
                savedRun.totalPower,
                savedRun.remainingSeconds,
                OffenseRouteGenerator.Create(target),
                new OffenseSupplyLoadout(restoredSupplies),
                preparation);

            string currentNodeId = savedRun.currentNodeId;
            OffenseExpeditionPhase phase = savedRun.phase;
            float light = savedRun.light;
            IEnumerable<string> completedNodes = savedRun.completedNodeIds;
            if (legacyJourney)
            {
                OffenseRouteNode boss = restoredRun.Route.Nodes.First(node => node.IsBoss);
                currentNodeId = boss.Id;
                phase = OffenseExpeditionPhase.InBattle;
                light = preparation.StartingLight;
                completedNodes = new[] { restoredRun.Route.EntranceNodeId };
            }

            Dictionary<StockCategory, int> carriedStock = (savedRun.carriedStock
                    ?? new List<DungeonOffenseStockRewardSaveData>())
                .Where(entry => entry != null && entry.amount > 0)
                .GroupBy(entry => entry.category)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.amount));
            restoredRun.RestoreJourneyState(
                phase,
                currentNodeId,
                light,
                completedNodes,
                carriedStock);

            Dictionary<string, DungeonOffenseExpeditionMemberStateSaveData> memberStateById =
                (savedRun.memberStates ?? new List<DungeonOffenseExpeditionMemberStateSaveData>())
                    .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.persistentId))
                    .GroupBy(entry => entry.persistentId, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (OffenseExpeditionMemberState memberState in restoredRun.MemberStates)
            {
                if (!characterSaveService.TryGetPersistentId(memberState.Actor, out string persistentId)
                    || !memberStateById.TryGetValue(persistentId, out DungeonOffenseExpeditionMemberStateSaveData savedMember))
                {
                    continue;
                }

                memberState.Restore(
                    savedMember.formation,
                    savedMember.stress,
                    savedMember.totalDamageTaken);
            }

            activeRuns.Add(restoredRun);
        }

        List<OffenseExpeditionResult> history = (source.resultHistory
                ?? new List<DungeonOffenseExpeditionResultSaveData>())
            .Where(savedResult => savedResult != null)
            .Select(RestoreResult)
            .ToList();
        OffenseBattlePersistenceState savedBattle = HasSavedBattle(source.activeBattle)
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
                report.AddWarning("The saved battle has no matching expedition and was skipped.");
                restoredBattleRun = activeRuns.FirstOrDefault();
            }
        }
        else
        {
            restoredBattleRun = activeRuns.FirstOrDefault();
        }

        foreach (OffenseExpeditionRun skipped in activeRuns.Where(run => !ReferenceEquals(run, restoredBattleRun)))
        {
            foreach (CharacterActor member in skipped.MemberActors)
            {
                member?.EndExpedition(alive: true);
            }

            report.AddWarning($"Legacy expedition '{skipped.ExpeditionId}' was skipped because turn combat supports one active battle.");
        }

        List<OffenseExpeditionRun> restoredRuns = restoredBattleRun != null
            ? new List<OffenseExpeditionRun> { restoredBattleRun }
            : new List<OffenseExpeditionRun>();
        expeditionRuntime.RestorePersistentState(restoredRuns, history);
        if (restoredBattleRun != null)
        {
            bool requiresBattle = restoredBattleRun.Phase == OffenseExpeditionPhase.InBattle;
            string battleMessage = "경로 상태 복원";
            bool restored = true;
            if (requiresBattle)
            {
                restored = savedBattle != null
                    ? battleRuntime.TryRestoreBattle(restoredBattleRun, savedBattle, out battleMessage)
                    : battleRuntime.TryStartBattle(restoredBattleRun, out battleMessage);
            }
            if (restored && requiresBattle && savedBattle == null)
            {
                battleRuntime.AdvanceToPlayerDecision();
            }
            if (!restored)
            {
                foreach (CharacterActor member in restoredBattleRun.MemberActors)
                {
                    member?.EndExpedition(alive: true);
                }

                expeditionRuntime.RestorePersistentState(Array.Empty<OffenseExpeditionRun>(), history);
                report.AddWarning($"Active offense battle could not be restored: {battleMessage}");
            }
        }

        report.RecordRestoredExpeditions(expeditionRuntime.ActiveExpeditions.Count);
    }

    private static bool HasSavedBattle(OffenseBattlePersistenceState battle)
    {
        return battle != null
            && !string.IsNullOrWhiteSpace(battle.battleId)
            && !string.IsNullOrWhiteSpace(battle.expeditionId)
            && !string.IsNullOrWhiteSpace(battle.targetId);
    }

    private DungeonOffenseExpeditionRunSaveData CaptureExpedition(OffenseExpeditionRun expedition)
    {
        return new DungeonOffenseExpeditionRunSaveData
        {
            journeyVersion = 1,
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
            preparationSources = expedition.Preparation.SourceSummaries.ToList()
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
            (source.members ?? new List<DungeonOffenseExpeditionMemberResultSaveData>())
                .Where(member => member != null)
                .Select(member => new OffenseExpeditionMemberSnapshot(
                    member.name,
                    member.speciesTag,
                    member.power,
                    member.survived,
                    member.damageTaken))
                .ToList(),
            source.rewardSummaries ?? new List<string>());
    }
}
