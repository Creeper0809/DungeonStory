using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class MetaProgressionRuntime : MonoBehaviour
{
    [SerializeField] private bool showRunResultPanel = true;

    private readonly MetaProgressionState state = new MetaProgressionState();

    private bool ended;
    private IGameClock gameClock;
    private IGameEventBus gameEventBus;
    private IDisposable ownerRunEndedSubscription;
    private IDisposable synthesisCompletedSubscription;
    private IDisposable researchCompletedSubscription;
    private IDisposable threatWarningSubscription;
    private IDisposable invasionCandidateSubscription;
    private IDisposable invasionStartedSubscription;
    private IDisposable invasionResolvedSubscription;
    private IDisposable operatingDayReportSubscription;
    private IDisposable facilityVisitSubscription;
    private IDisposable operatingDayStartedSubscription;
    private IMetaRunResultBuilder runResultBuilder;
    private IRunResultPanelService runResultPanelService;
    private MetaRunProgressTracker runProgress;

    public MetaProgressionState State => state;
    public RunResultSnapshot LatestResult { get; private set; }
    public MetaRunProgressTracker RunProgress => ResolveRunProgress();
    public bool HasEnded => ended;

    [Inject]
    public void Construct(
        IMetaRunResultBuilder runResultBuilder,
        IRunResultPanelService runResultPanelService,
        IGameClock gameClock,
        IGameEventBus gameEventBus)
    {
        this.runResultBuilder = runResultBuilder
            ?? throw new ArgumentNullException(nameof(runResultBuilder));
        this.runResultPanelService = runResultPanelService
            ?? throw new ArgumentNullException(nameof(runResultPanelService));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        runProgress ??= new MetaRunProgressTracker(gameClock);
        StartNewRun();
        SubscribeToScopedEvents();
    }

    public void SetShowRunResultPanel(bool value)
    {
        showRunResultPanel = value;
    }

    public void StartNewRun()
    {
        ResolveRunProgress().StartNewRun(ResolveGameClock().Time);
        ended = false;
        LatestResult = null;
    }

    public void RestoreRunState(bool hasEnded, RunResultSnapshot latestResult)
    {
        ended = hasEnded;
        LatestResult = latestResult;
    }

    public bool TryPurchaseUpgrade(string id, out string message)
    {
        bool success = state.TryPurchaseUpgrade(id, out message);
        if (success)
        {
            ResolveGameEventBus().Publish(new MetaUpgradePurchasedEvent(id));
            gameEventBus.RaiseAlert("계승 강화", message, EventAlertImportance.Medium, "계승");
        }

        return success;
    }

    public int GetStartingFacilityCandidateBonus()
    {
        return MetaProgressionEffects.GetIntegerBonus(
            state,
            MetaUpgradeEffectIds.StartingFacilityCandidates);
    }

    public int GetStartingOwnerTraitCandidateBonus()
    {
        return MetaProgressionEffects.GetIntegerBonus(
            state,
            MetaUpgradeEffectIds.StartingOwnerTraitCandidates);
    }

    public float GetOwnerMaxHealthMultiplier()
    {
        return MetaProgressionEffects.GetMultiplier(state, MetaUpgradeEffectIds.OwnerMaxHealth);
    }

    public float GetInvasionWarningThresholdMultiplier()
    {
        return MetaProgressionEffects.GetMultiplier(
            state,
            MetaUpgradeEffectIds.InvasionWarningThreshold);
    }

    public float GetCommerceStockCostMultiplier(StockCategory category)
    {
        if (category != StockCategory.Food && category != StockCategory.General)
        {
            return 1f;
        }

        return MetaProgressionEffects.GetMultiplier(state, MetaUpgradeEffectIds.CommerceStockCost);
    }

    public float GetFortressFacilityCostMultiplier(BuildingSO building)
    {
        return building?.Defense != null && building.Defense.IsDefenseFacility
            ? MetaProgressionEffects.GetMultiplier(state, MetaUpgradeEffectIds.FortressDefenseFacilityCost)
            : 1f;
    }

    public float GetArcaneResearchWorkMultiplier()
    {
        return MetaProgressionEffects.GetMultiplier(state, MetaUpgradeEffectIds.ArcaneResearchWork);
    }

    public bool IsRecipePreserved(string recipeId)
    {
        return !string.IsNullOrWhiteSpace(recipeId)
            && state.PreservedRecipeIds.Contains(recipeId);
    }

    public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)
    {
        int count = MetaProgressionEffects.GetIntegerBonus(
            state,
            MetaUpgradeEffectIds.BasicPurchaseEntries);
        if (count <= 0)
        {
            return Array.Empty<int>();
        }

        return buildings?
            .Where((building) => building != null
                && !building.IsGridMovement
                && !building.IsWall
                && FacilityShopService.GetBuildingStar(building) <= 1)
            .OrderBy((building) => building.id)
            .Take(count)
            .Select((building) => building.id)
            .ToArray()
            ?? Array.Empty<int>();
    }

    public void RecordOffenseSuccess()
    {
        runProgress.RecordOffenseSuccess();
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
        if (eventType.day <= 1 && ended)
        {
            StartNewRun();
        }

        runProgress.RecordOperatingDayStarted(eventType.day);
    }

    public void OnTriggerEvent(OperatingDayReportEvent eventType)
    {
        runProgress.RecordOperatingDayReport(eventType.report);
    }

    private void OnThreatWarning(InvasionThreatWarningEvent eventType)
    {
        runProgress.RecordThreat(eventType.snapshot);
    }

    private void OnInvasionCandidate(InvasionCandidateEvent eventType)
    {
        runProgress.RecordThreat(eventType.snapshot);
    }

    public void OnTriggerEvent(InvasionStartedEvent eventType)
    {
        runProgress.RecordInvasionStarted(eventType.snapshot);
    }

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        runProgress.RecordInvasionResolved(eventType.defended);
    }

    public void OnTriggerEvent(FacilityVisitEvent eventType)
    {
        runProgress.RecordFacilityVisit(eventType.facility);
    }

    public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)
    {
        runProgress.RecordBlueprintResearchCompleted(eventType.unlockResult);
    }

    public void OnTriggerEvent(FacilitySynthesisCompletedEvent eventType)
    {
        runProgress.RecordFacilitySynthesisCompleted(eventType.result);
    }

    public void OnTriggerEvent(OwnerRunEndedEvent eventType)
    {
        EndRun(eventType.OwnerActor, eventType.Reason, eventType.Outcome);
    }

    public RunResultSnapshot EndRun(CharacterActor owner, string reason)
    {
        return EndRun(owner, reason, DungeonRunOutcome.Defeat);
    }

    public RunResultSnapshot EndRun(
        CharacterActor owner,
        string reason,
        DungeonRunOutcome outcome)
    {
        if (ended && LatestResult != null)
        {
            return LatestResult;
        }

        ended = true;
        RunResultSnapshot result = ResolveRunResultBuilder().Build(
            runProgress.CreateResultContext(owner, reason, outcome));
        result = result.WithLegacyCurrency(MetaProgressionCalculator.CalculateLegacyCurrency(result));
        state.AddCurrency(result.legacyCurrency);
        state.RecordRunCompleted();
        PreserveRunRecipes();
        LatestResult = result;

        ResolveGameEventBus().Publish(new RunResultReadyEvent(result));
        gameEventBus.RaiseAlert("런 결과 정산", result.ToDetailText(), EventAlertImportance.High, "계승");
        if (showRunResultPanel)
        {
            ResolveRunResultPanelService().Show(result);
        }

        return result;
    }

    private IMetaRunResultBuilder ResolveRunResultBuilder()
    {
        return runResultBuilder
            ?? throw new InvalidOperationException($"{nameof(MetaProgressionRuntime)} requires {nameof(IMetaRunResultBuilder)} injection.");
    }

    private IRunResultPanelService ResolveRunResultPanelService()
    {
        return runResultPanelService
            ?? throw new InvalidOperationException($"{nameof(MetaProgressionRuntime)} requires {nameof(IRunResultPanelService)} injection.");
    }

    private IGameClock ResolveGameClock()
    {
        return gameClock
            ?? throw new InvalidOperationException(
                $"{nameof(MetaProgressionRuntime)} requires {nameof(IGameClock)} injection.");
    }

    private IGameEventBus ResolveGameEventBus()
    {
        return gameEventBus
            ?? throw new InvalidOperationException(
                $"{nameof(MetaProgressionRuntime)} requires {nameof(IGameEventBus)} injection.");
    }

    private MetaRunProgressTracker ResolveRunProgress()
    {
        return runProgress
            ?? throw new InvalidOperationException(
                $"{nameof(MetaProgressionRuntime)} requires {nameof(IGameClock)} injection before run progress is used.");
    }

    private void PreserveRunRecipes()
    {
        int slots = MetaProgressionEffects.GetIntegerBonus(
            state,
            MetaUpgradeEffectIds.PreservedRecipeSlots);
        state.PreserveRecipes(runProgress.UnlockedRecipeIds.OrderBy((id) => id), slots);
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        ownerRunEndedSubscription?.Dispose();
        ownerRunEndedSubscription = null;
        synthesisCompletedSubscription?.Dispose();
        synthesisCompletedSubscription = null;
        researchCompletedSubscription?.Dispose();
        researchCompletedSubscription = null;
        threatWarningSubscription?.Dispose();
        threatWarningSubscription = null;
        invasionCandidateSubscription?.Dispose();
        invasionCandidateSubscription = null;
        invasionStartedSubscription?.Dispose();
        invasionStartedSubscription = null;
        invasionResolvedSubscription?.Dispose();
        invasionResolvedSubscription = null;
        operatingDayReportSubscription?.Dispose();
        operatingDayReportSubscription = null;
        facilityVisitSubscription?.Dispose();
        facilityVisitSubscription = null;
        operatingDayStartedSubscription?.Dispose();
        operatingDayStartedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        ownerRunEndedSubscription ??= gameEventBus.Subscribe<OwnerRunEndedEvent>(OnTriggerEvent);
        synthesisCompletedSubscription ??=
            gameEventBus.Subscribe<FacilitySynthesisCompletedEvent>(OnTriggerEvent);
        researchCompletedSubscription ??=
            gameEventBus.Subscribe<BlueprintResearchCompletedEvent>(OnTriggerEvent);
        threatWarningSubscription ??=
            gameEventBus.Subscribe<InvasionThreatWarningEvent>(OnThreatWarning);
        invasionCandidateSubscription ??=
            gameEventBus.Subscribe<InvasionCandidateEvent>(OnInvasionCandidate);
        invasionStartedSubscription ??=
            gameEventBus.Subscribe<InvasionStartedEvent>(OnTriggerEvent);
        invasionResolvedSubscription ??=
            gameEventBus.Subscribe<InvasionResolvedEvent>(OnTriggerEvent);
        operatingDayReportSubscription ??=
            gameEventBus.Subscribe<OperatingDayReportEvent>(OnTriggerEvent);
        facilityVisitSubscription ??=
            gameEventBus.Subscribe<FacilityVisitEvent>(OnTriggerEvent);
        operatingDayStartedSubscription ??=
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnTriggerEvent);
    }

}
