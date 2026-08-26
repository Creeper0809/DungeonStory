using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public sealed class OffenseExpeditionRuntimeRestoreCandidate
{
    internal OffenseExpeditionRuntimeRestoreCandidate(
        List<OffenseExpeditionRun> activeExpeditions,
        List<OffenseExpeditionResult> resultHistory)
    {
        ActiveExpeditions = activeExpeditions
            ?? throw new ArgumentNullException(nameof(activeExpeditions));
        ResultHistory = resultHistory
            ?? throw new ArgumentNullException(nameof(resultHistory));
    }

    internal List<OffenseExpeditionRun> ActiveExpeditions { get; }
    internal List<OffenseExpeditionResult> ResultHistory { get; }
}

public class OffenseExpeditionRuntime :
    MonoBehaviour,
    IOffenseStrategicExpeditionHost
{
    private const int MaxResultHistory = 20;
    private readonly OffenseExpeditionLaunchService launchService = new();
    private readonly OffenseExpeditionDecisionService decisionService = new();
    private static readonly OffenseExpeditionExperienceRules ExperienceRules = new();

    private List<OffenseExpeditionRun> activeExpeditions = new List<OffenseExpeditionRun>();
    private List<OffenseExpeditionResult> resultHistory = new List<OffenseExpeditionResult>();
    private IReadOnlyList<OffenseExpeditionRun> activeExpeditionsView;
    private IReadOnlyList<OffenseExpeditionResult> resultHistoryView;
    private bool resumeRestoredWorldStatePending;
    private IOffenseExpeditionMemberQuery memberQuery;
    private OffenseWorldMapRuntime worldMap;
    private IOffenseExpeditionResultFinalizer resultFinalizer;
    private IOffensePanelService panelService;
    private IOffenseBattleRuntime battleRuntime;
    private IOffensePreparationService preparationService;
    private ICombatEquipmentRuntime equipmentRuntime;
    private ICombatEquipmentPickupRuntime equipmentPickupRuntime;
    private IExpeditionDepartureService departureService;
    private IOffenseExpeditionReturnCoordinator returnCoordinator;
    private IGameEventBus gameEventBus;
    private IOffenseWorldSimulation strategicWorld;
    private IOffenseTravelRuntime strategicTravel;
    private IOffenseDecisionRuntime strategicDecisions;
    private IOffenseDecisionEffectExecutor strategicDecisionEffects;
    private IOffenseReturnSafetyRuntime strategicReturnSafety;
    private IOffenseStrategicTargetService strategicTargets;
    private IOffenseStrategicBattleLauncher strategicBattleLauncher;
    private IOffenseStrategicTravelEventHandler strategicTravelEvents;
    private IOffenseExpeditionBattleCompletionHandler battleCompletionHandler;
    private IGameMoneyAccount gameMoney;
    private IOffenseFieldMedicalRuntime fieldMedical;
    private IOffenseFieldMobilityService fieldMobility;
    private ICharacterPerformanceQuery performance;
    private BlueprintResearchRuntime expeditionResearchRuntime;
    private BlueprintResearchState expeditionResearchState;
    private bool enforceExpeditionAccess;

    public IReadOnlyList<OffenseExpeditionRun> ActiveExpeditions =>
        activeExpeditionsView ??= activeExpeditions.AsReadOnly();
    public IReadOnlyList<OffenseExpeditionResult> ResultHistory =>
        resultHistoryView ??= resultHistory.AsReadOnly();
    public event Action StateChanged;

    public void Construct(
        IOffenseExpeditionMemberQuery memberQuery,
        OffenseSceneRuntimeReferences offenseRuntimes,
        IOffensePanelService panelService,
        IGameEventBus gameEventBus,
        IOffenseExpeditionResultFinalizer resultFinalizer)
    {
        this.memberQuery = memberQuery
            ?? throw new ArgumentNullException(nameof(memberQuery));
        worldMap = (offenseRuntimes
                ?? throw new ArgumentNullException(nameof(offenseRuntimes)))
            .WorldMap
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseExpeditionRuntime)} requires a loaded {nameof(OffenseWorldMapRuntime)}.");
        this.panelService = panelService
            ?? throw new ArgumentNullException(nameof(panelService));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.resultFinalizer = resultFinalizer
            ?? throw new ArgumentNullException(nameof(resultFinalizer));
    }

    public void Construct(
        IOffenseExpeditionMemberQuery memberQuery,
        OffenseSceneRuntimeReferences offenseRuntimes,
        IOffensePanelService panelService,
        IGameEventBus gameEventBus,
        IOffenseBattleRuntime battleRuntime,
        IOffenseExpeditionResultFinalizer resultFinalizer,
        IOffenseExpeditionReturnCoordinator returnCoordinator)
    {
        Construct(
            memberQuery,
            offenseRuntimes,
            panelService,
            gameEventBus,
            resultFinalizer);
        if (this.battleRuntime != null)
        {
            this.battleRuntime.BattleCompleted -= OnBattleCompleted;
        }

        this.battleRuntime = battleRuntime
            ?? throw new ArgumentNullException(nameof(battleRuntime));
        this.returnCoordinator = returnCoordinator
            ?? throw new ArgumentNullException(nameof(returnCoordinator));
        this.battleRuntime.BattleCompleted += OnBattleCompleted;
    }

    [Inject]
    public void Construct(
        IOffenseExpeditionMemberQuery memberQuery,
        OffenseSceneRuntimeReferences offenseRuntimes,
        IOffensePanelService panelService,
        IOffenseBattleRuntime battleRuntime,
        IOffenseExpeditionResultFinalizer resultFinalizer,
        IOffenseExpeditionReturnCoordinator returnCoordinator,
        IOffensePreparationService preparationService,
        ICombatEquipmentRuntime equipmentRuntime,
        IGameEventBus gameEventBus,
        IOffenseWorldSimulation strategicWorld,
        IOffenseTravelRuntime strategicTravel,
        IOffenseDecisionRuntime strategicDecisions,
        IOffenseDecisionEffectExecutor strategicDecisionEffects,
        IOffenseReturnSafetyRuntime strategicReturnSafety,
        IOffenseStrategicTargetService strategicTargets,
        IOffenseStrategicBattleLauncher strategicBattleLauncher,
        IOffenseStrategicTravelEventHandler strategicTravelEvents,
        IOffenseExpeditionBattleCompletionHandler battleCompletionHandler,
        IGameMoneyAccount gameMoney,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IExpeditionDepartureService departureService,
        ICombatEquipmentPickupRuntime equipmentPickupRuntime,
        IOffenseFieldMedicalRuntime fieldMedical,
        IOffenseFieldMobilityService fieldMobility,
        ICharacterPerformanceQuery performance = null)
    {
        Construct(
            memberQuery,
            offenseRuntimes,
            panelService,
            gameEventBus,
            battleRuntime,
            resultFinalizer,
            returnCoordinator);
        this.preparationService = preparationService
            ?? throw new ArgumentNullException(nameof(preparationService));
        this.equipmentRuntime = equipmentRuntime
            ?? throw new ArgumentNullException(nameof(equipmentRuntime));
        this.departureService = departureService;
        this.equipmentPickupRuntime = equipmentPickupRuntime;
        this.fieldMedical = fieldMedical
            ?? throw new ArgumentNullException(nameof(fieldMedical));
        this.fieldMobility = fieldMobility
            ?? throw new ArgumentNullException(nameof(fieldMobility));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.strategicDecisionEffects = strategicDecisionEffects
            ?? throw new ArgumentNullException(nameof(strategicDecisionEffects));
        this.strategicTargets = strategicTargets
            ?? throw new ArgumentNullException(nameof(strategicTargets));
        this.strategicBattleLauncher = strategicBattleLauncher
            ?? throw new ArgumentNullException(nameof(strategicBattleLauncher));
        this.strategicTravelEvents = strategicTravelEvents
            ?? throw new ArgumentNullException(nameof(strategicTravelEvents));
        this.battleCompletionHandler = battleCompletionHandler
            ?? throw new ArgumentNullException(nameof(battleCompletionHandler));
        this.gameMoney = gameMoney
            ?? throw new ArgumentNullException(nameof(gameMoney));
        expeditionResearchState = OffenseExpeditionAccessRules.RequireState(
            progressionRuntimes,
            nameof(OffenseExpeditionRuntime));
        expeditionResearchRuntime = progressionRuntimes.BlueprintResearch;
        enforceExpeditionAccess = true;
        this.strategicWorld = strategicWorld
            ?? throw new ArgumentNullException(nameof(strategicWorld));
        this.strategicTravel = strategicTravel
            ?? throw new ArgumentNullException(nameof(strategicTravel));
        this.strategicDecisions = strategicDecisions
            ?? throw new ArgumentNullException(nameof(strategicDecisions));
        this.strategicReturnSafety = strategicReturnSafety
            ?? throw new ArgumentNullException(nameof(strategicReturnSafety));
        this.strategicTravel.StepCompleted += OnStrategicTravelStepCompleted;
        this.strategicTravel.DecisionRequired += OnStrategicDecisionRequired;
        this.strategicTravel.SiteReached += OnStrategicSiteReached;
    }

    private void OnDestroy()
    {
        if (battleRuntime != null)
        {
            battleRuntime.BattleCompleted -= OnBattleCompleted;
        }

        if (strategicTravel != null)
        {
            strategicTravel.StepCompleted -= OnStrategicTravelStepCompleted;
            strategicTravel.DecisionRequired -= OnStrategicDecisionRequired;
            strategicTravel.SiteReached -= OnStrategicSiteReached;
        }
    }

    public IReadOnlyList<CharacterActor> GetAvailableMemberActors()
    {
        return ResolveMemberQuery().GetAvailableMemberActors();
    }

    public OffensePreparationSnapshot GetPreparationSnapshot()
    {
        return preparationService?.Evaluate()
            ?? new OffensePreparationSnapshot(
                new OffenseExpeditionPreparation(),
                new Dictionary<OffenseSupplyType, int>());
    }

    public IReadOnlyList<CombatEquipmentDefinitionSO> GetEquipmentDefinitions()
    {
        return equipmentRuntime?.Definitions ?? Array.Empty<CombatEquipmentDefinitionSO>();
    }

    public float CalculatePartyPower(IEnumerable<CharacterActor> members)
    {
        if (equipmentRuntime == null)
        {
            throw new InvalidOperationException(
                $"{nameof(OffenseExpeditionRuntime)} requires {nameof(ICombatEquipmentRuntime)} before calculating expedition power.");
        }

        return OffenseExpeditionService.CalculatePartyPower(
            members,
            equipmentRuntime,
            performance);
    }

    public IReadOnlyDictionary<string, int> GetEquipmentInventory()
    {
        return equipmentRuntime?.Instances
            .Where(instance => instance != null
                && instance.worldState is not (
                    CombatEquipmentWorldState.Lost
                    or CombatEquipmentWorldState.RetailStock))
            .GroupBy(instance => instance.definitionId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
    }

    public IReadOnlyList<CombatEquipmentCraftOrderSaveData> GetEquipmentCraftQueue()
    {
        return equipmentRuntime?.CraftQueue ?? Array.Empty<CombatEquipmentCraftOrderSaveData>();
    }

    public int GetAvailableEquipmentCount(string equipmentId)
    {
        return equipmentRuntime?.GetAvailableCount(equipmentId) ?? 0;
    }

    public bool TryGetEquippedEquipment(
        CharacterActor actor,
        CombatEquipmentLoadoutSlot slot,
        out CombatEquipmentDefinitionSO definition)
    {
        definition = null;
        string characterId = GetPersistentCharacterId(actor);
        if (string.IsNullOrWhiteSpace(characterId) || equipmentRuntime == null)
        {
            return false;
        }

        CharacterCombatLoadoutProfile profile =
            equipmentRuntime.GetActiveProfileSnapshot(characterId);
        string instanceId = slot == CombatEquipmentLoadoutSlot.Weapon
            ? profile?.activeWeaponInstanceId
            : profile?.armorInstanceIds?.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(instanceId)
            && equipmentRuntime.TryGetInstance(instanceId, out CombatEquipmentInstance instance)
            && equipmentRuntime.TryGetDefinition(instance.definitionId, out definition);
    }

    public bool TryEquipEquipment(CharacterActor actor, string equipmentId, out string message)
    {
        string characterId = GetPersistentCharacterId(actor);
        if (string.IsNullOrWhiteSpace(characterId) || equipmentRuntime == null)
        {
            message = "equipment-runtime-missing";
            return false;
        }

        if (equipmentPickupRuntime == null)
        {
            message = "장비 수령 시스템을 사용할 수 없습니다.";
            return false;
        }

        bool pickupStarted = equipmentPickupRuntime.TryRequestEquipmentPickup(
            actor,
            equipmentId,
            out message);
        if (pickupStarted)
        {
            StateChanged?.Invoke();
        }

        return pickupStarted;
    }

    public bool TryUnequipEquipment(
        CharacterActor actor,
        CombatEquipmentLoadoutSlot slot,
        out string message)
    {
        string characterId = GetPersistentCharacterId(actor);
        if (string.IsNullOrWhiteSpace(characterId) || equipmentRuntime == null)
        {
            message = "equipment-runtime-missing";
            return false;
        }

        if (equipmentPickupRuntime == null)
        {
            message = "장비 해제 시스템을 사용할 수 없습니다.";
            return false;
        }

        if (!equipmentPickupRuntime.TryUnequipToWorld(actor, slot, out message))
        {
            return false;
        }

        StateChanged?.Invoke();
        return true;
    }

    public bool TryQueueEquipmentCraft(string equipmentId, out string message)
    {
        if (equipmentRuntime == null)
        {
            message = "equipment-runtime-missing";
            return false;
        }

        message = "장비 제작은 대장작업대에서 주문해야 합니다.";
        bool queued = false;
        if (queued)
        {
            StateChanged?.Invoke();
        }

        return queued;
    }

    public bool TryStartExpedition(
        string targetId,
        IEnumerable<CharacterActor> members,
        out OffenseExpeditionRun expedition,
        out string message)
    {
        return TryStartExpedition(
            targetId,
            members,
            new OffenseSupplyLoadout(),
            new OffenseExpeditionPreparation(),
            out expedition,
            out message);
    }

    public bool TryStartExpedition(
        string targetId,
        IEnumerable<CharacterActor> members,
        OffenseSupplyLoadout supplies,
        OffenseExpeditionPreparation preparation,
        out OffenseExpeditionRun expedition,
        out string message)
    {
        BlueprintResearchState currentResearchState =
            expeditionResearchRuntime != null
                ? expeditionResearchRuntime.State
                : expeditionResearchState;
        if (enforceExpeditionAccess
            && !OffenseExpeditionAccessRules.IsUnlocked(currentResearchState))
        {
            expedition = null;
            message = OffenseExpeditionAccessRules.BlockerMessage;
            return false;
        }

        return launchService.TryStart(
            new OffenseExpeditionLaunchDomain(
                worldMap,
                strategicTargets,
                fieldMedical,
                battleRuntime,
                preparationService,
                equipmentRuntime,
                performance),
            new OffenseExpeditionLaunchInfrastructure(
                gameMoney,
                strategicTravel,
                departureService,
                gameEventBus),
            ActiveExpeditions,
            targetId,
            members,
            supplies,
            preparation,
            created => activeExpeditions.Add(created),
            () => StateChanged?.Invoke(),
            out expedition,
            out message);
    }

    public bool TryChooseRouteNode(string expeditionId, string nodeId, out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        if (!expedition.TryEnterNode(nodeId, out message))
        {
            return false;
        }

        if (expedition.Phase == OffenseExpeditionPhase.InBattle)
        {
            if (!battleRuntime.TryStartBattle(expedition, out message))
            {
                expedition.Retreat(out _);
                CompleteExpedition(expedition, success: false, "전투를 시작하지 못해 철수했습니다.");
                return false;
            }

            battleRuntime.AdvanceToPlayerDecision();
        }

        StateChanged?.Invoke();
        return true;
    }

    public bool TryResolveCurrentNode(
        string expeditionId,
        bool useSupply,
        out OffenseExpeditionNodeResult result,
        out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            result = null;
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        OffenseRouteNode resolvedNode = expedition.CurrentNode;
        bool resolved = expedition.TryResolveCurrentNode(useSupply, out result, out message);
        if (resolved)
        {
            ExperienceRules.AwardNodeExperience(expedition, resolvedNode);
            StateChanged?.Invoke();
        }

        return resolved;
    }

    public bool TryUseSupply(
        string expeditionId,
        OffenseSupplyType type,
        int memberIndex,
        out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        bool used = expedition.TryUseSupply(type, memberIndex, out message);
        if (used) StateChanged?.Invoke();
        return used;
    }

    public bool TrySwapFormation(
        string expeditionId,
        int firstIndex,
        int secondIndex,
        out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        bool swapped = expedition.TrySwapFormation(firstIndex, secondIndex, out message);
        if (swapped) StateChanged?.Invoke();
        return swapped;
    }

    public bool TryRetreat(string expeditionId, out string message)
    {
        OffenseExpeditionRun expedition = FindActiveExpedition(expeditionId);
        if (expedition == null)
        {
            message = "진행 중인 원정을 찾을 수 없습니다.";
            return false;
        }

        if (expedition.Phase == OffenseExpeditionPhase.InBattle)
        {
            message = "전투 중에는 행동 메뉴의 후퇴 명령을 사용해야 합니다.";
            return false;
        }

        if (!expedition.Retreat(out message))
        {
            return false;
        }

        CompleteExpedition(expedition, success: false, message);
        return true;
    }

    internal void PublishPersistentState(
        List<OffenseExpeditionRun> activeCandidate,
        List<OffenseExpeditionResult> historyCandidate)
    {
        activeExpeditions = activeCandidate
            ?? throw new ArgumentNullException(nameof(activeCandidate));
        resultHistory = historyCandidate
            ?? throw new ArgumentNullException(nameof(historyCandidate));
        activeExpeditionsView = null;
        resultHistoryView = null;
        resumeRestoredWorldStatePending = activeExpeditions.Any(run =>
            run != null && run.UsesWorldTravel);
    }

    public OffenseExpeditionRuntimeRestoreCandidate BuildRestoreCandidate(
        IEnumerable<OffenseExpeditionRun> restoredActiveExpeditions,
        IEnumerable<OffenseExpeditionResult> restoredResultHistory)
    {
        if (restoredActiveExpeditions == null)
        {
            throw new ArgumentNullException(nameof(restoredActiveExpeditions));
        }
        if (restoredResultHistory == null)
        {
            throw new ArgumentNullException(nameof(restoredResultHistory));
        }

        List<OffenseExpeditionRun> activeCandidate =
            restoredActiveExpeditions.ToList();
        List<OffenseExpeditionResult> historyCandidate =
            restoredResultHistory.ToList();
        if (activeCandidate.Any(expedition => expedition == null
                || expedition.Target == null)
            || historyCandidate.Any(result => result == null)
            || historyCandidate.Count > MaxResultHistory)
        {
            throw new InvalidOperationException(
                "Offense expedition restore candidate is invalid or exceeds history capacity.");
        }

        return new OffenseExpeditionRuntimeRestoreCandidate(
            activeCandidate,
            historyCandidate);
    }

    public void PublishRestoreCandidate(
        OffenseExpeditionRuntimeRestoreCandidate candidate)
    {
        candidate = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        PublishPersistentState(
            candidate.ActiveExpeditions,
            candidate.ResultHistory);
    }

    private void Update()
    {
        if (!resumeRestoredWorldStatePending)
        {
            return;
        }

        resumeRestoredWorldStatePending = false;
        ResumeRestoredWorldState();
    }

    public void ResumeRestoredWorldState()
    {
        foreach (OffenseExpeditionRun expedition in activeExpeditions
                     .Where(run => run != null && run.UsesWorldTravel))
        {
            if (expedition.DepartureCompleted)
            {
                if (expedition.Phase is OffenseExpeditionPhase.Traveling
                    or OffenseExpeditionPhase.Returning)
                {
                    strategicTravel?.TryResumeAfterBattle(expedition.ExpeditionId);
                }

                continue;
            }

            if (departureService == null)
            {
                gameEventBus?.RaiseAlert(
                    "출정 복원 실패",
                    "출정 집결 서비스를 찾을 수 없어 원정대가 대기합니다.",
                    EventAlertImportance.High,
                    "expedition");
                continue;
            }

            OffenseExpeditionRun restoredExpedition = expedition;
            if (!departureService.TryBeginDeparture(
                    restoredExpedition,
                    restoredExpedition.MemberActors,
                    () => preparationService == null
                        || preparationService.IsPackageReady(
                            restoredExpedition.ExpeditionId),
                    () =>
                    {
                        if (preparationService != null
                            && !preparationService.TryConsumePackedSupplies(
                                restoredExpedition.ExpeditionId,
                                out string packingMessage))
                        {
                            gameEventBus?.RaiseAlert(
                                "출정 보급 오류",
                                packingMessage,
                                EventAlertImportance.High,
                                "expedition");
                            return;
                        }

                        restoredExpedition.MarkDepartureCompleted();
                        strategicTravel?.TryResumeAfterBattle(
                            restoredExpedition.ExpeditionId);
                        StateChanged?.Invoke();
                    },
                    out string departureMessage))
            {
                gameEventBus?.RaiseAlert(
                    "출정 복원 실패",
                    departureMessage,
                    EventAlertImportance.High,
                    "expedition");
            }
        }
    }

    public bool TryResolveDecision(
        string expeditionId,
        string choiceId,
        out string message)
    {
        return decisionService.TryResolve(
            FindActiveExpedition(expeditionId),
            choiceId,
            new OffenseExpeditionDecisionDomain(
                strategicDecisions,
                strategicTravel,
                strategicDecisionEffects,
                fieldMobility),
            new OffenseExpeditionDecisionEffects(
                strategicWorld,
                gameMoney,
                equipmentRuntime,
                strategicReturnSafety,
                strategicBattleLauncher),
            () => StateChanged?.Invoke(),
            out message);
    }

    public bool TryRedirectExpedition(
        string expeditionId,
        OffenseHexCoord destination,
        string siteId,
        bool startsSiteAttack,
        out string message)
    {
        bool redirected = strategicTargets.TryRedirect(
            FindActiveExpedition(expeditionId),
            destination,
            siteId,
            startsSiteAttack,
            out message);
        if (redirected)
        {
            StateChanged?.Invoke();
        }

        return redirected;
    }

    private void OnStrategicTravelStepCompleted(
        string expeditionId,
        OffenseTravelStepResult step)
    {
        strategicTravelEvents.HandleStepCompleted(this, expeditionId, step);
    }

    private void OnStrategicDecisionRequired(string expeditionId)
    {
        strategicTravelEvents.HandleDecisionRequired(this, expeditionId);
    }

    private void OnStrategicSiteReached(string expeditionId, string siteId)
    {
        strategicTravelEvents.HandleSiteReached(this, expeditionId, siteId);
    }

    private void OnBattleCompleted(OffenseBattleSession session)
    {
        battleCompletionHandler.Handle(this, session);
    }

    private OffenseExpeditionRun FindActiveExpedition(string expeditionId)
    {
        return activeExpeditions.FirstOrDefault(expedition => string.Equals(
            expedition?.ExpeditionId,
            expeditionId,
            StringComparison.Ordinal));
    }

    public static int CalculateNodeExperience(OffenseRouteNode node, int stage)
    {
        return ExperienceRules.CalculateNodeExperience(node, stage);
    }

    public static int CalculateSuccessfulReturnExperience(OffenseExpeditionRun expedition)
    {
        return ExperienceRules.CalculateSuccessfulReturnExperience(expedition);
    }

    public static int CalculateSuccessfulReturnExperience(int stage)
    {
        return ExperienceRules.CalculateSuccessfulReturnExperience(stage);
    }

    private void CompleteExpedition(
        OffenseExpeditionRun expedition,
        bool success,
        string message)
    {
        if (expedition == null)
        {
            return;
        }

        strategicTravel?.TryRemove(expedition.ExpeditionId);
        activeExpeditions.Remove(expedition);
        returnCoordinator.Complete(
            expedition,
            success,
            message,
            resultHistory,
            () => StateChanged?.Invoke());
    }

    public OffenseExpeditionPanel ShowExpeditionPanel()
    {
        return ResolvePanelService().ShowExpedition(this);
    }

    private static string GetPersistentCharacterId(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        return actor?.Identity?.PersistentId ?? string.Empty;
    }

    private IOffenseExpeditionMemberQuery ResolveMemberQuery()
    {
        return memberQuery
            ?? throw new InvalidOperationException($"{nameof(OffenseExpeditionRuntime)} requires {nameof(IOffenseExpeditionMemberQuery)} injection.");
    }

    private OffenseWorldMapRuntime ResolveWorldMap()
    {
        return worldMap
            ?? throw new InvalidOperationException($"{nameof(OffenseExpeditionRuntime)} requires a loaded {nameof(OffenseWorldMapRuntime)}.");
    }

    private IOffensePanelService ResolvePanelService()
    {
        return panelService
            ?? throw new InvalidOperationException($"{nameof(OffenseExpeditionRuntime)} requires {nameof(IOffensePanelService)} injection.");
    }

    OffenseExpeditionRun IOffenseStrategicExpeditionHost.FindActiveExpedition(
        string expeditionId)
    {
        return FindActiveExpedition(expeditionId);
    }

    void IOffenseStrategicExpeditionHost.RemoveActiveExpedition(
        OffenseExpeditionRun expedition)
    {
        activeExpeditions.Remove(expedition);
    }

    void IOffenseStrategicExpeditionHost.CompleteExpedition(
        OffenseExpeditionRun expedition,
        bool success,
        string message)
    {
        CompleteExpedition(expedition, success, message);
    }

    void IOffenseStrategicExpeditionHost.NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
