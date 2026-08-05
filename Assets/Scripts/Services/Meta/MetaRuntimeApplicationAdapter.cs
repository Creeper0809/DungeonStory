using System;
using System.Collections.Generic;
using DungeonStory.Foundation;

public sealed class MetaRuntimeApplicationAdapter : IMetaRuntimeApplicationPort
{
    private readonly IGameEventBus gameEventBus;
    private readonly InvasionThreatRuntime threatRuntime;
    private readonly IRunVariableRuntimeReader runVariables;
    private readonly IRunResultPanelService panelService;
    private readonly List<IDisposable> subscriptions = new List<IDisposable>();
    private IMetaRuntimeEventSink runtime;

    public MetaRuntimeApplicationAdapter(
        IGameEventBus gameEventBus,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IRunVariableRuntimeReader runVariables,
        IRunResultPanelService panelService)
    {
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        threatRuntime = (invasionRuntimes ?? throw new ArgumentNullException(nameof(invasionRuntimes))).Threat
            ?? throw new InvalidOperationException("Meta runtime requires an invasion threat runtime.");
        this.runVariables = runVariables;
        this.panelService = panelService ?? throw new ArgumentNullException(nameof(panelService));
    }

    public void Bind(IMetaRuntimeEventSink target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (ReferenceEquals(runtime, target) && subscriptions.Count > 0) return;
        if (runtime != null) throw new InvalidOperationException("Meta adapter already bound.");
        runtime = target;
        subscriptions.Add(gameEventBus.Subscribe<OwnerRunEndedEvent>(OnOwnerRunEnded));
        subscriptions.Add(gameEventBus.Subscribe<FacilitySynthesisCompletedEvent>(OnSynthesis));
        subscriptions.Add(gameEventBus.Subscribe<BlueprintResearchCompletedEvent>(OnResearch));
        subscriptions.Add(gameEventBus.Subscribe<InvasionThreatWarningEvent>(OnThreatWarning));
        subscriptions.Add(gameEventBus.Subscribe<InvasionCandidateEvent>(OnCandidate));
        subscriptions.Add(gameEventBus.Subscribe<InvasionStartedEvent>(OnInvasionStarted));
        subscriptions.Add(gameEventBus.Subscribe<InvasionResolvedEvent>(OnInvasionResolved));
        subscriptions.Add(gameEventBus.Subscribe<OperatingDayReportEvent>(OnDayReport));
        subscriptions.Add(gameEventBus.Subscribe<FacilityVisitEvent>(OnFacilityVisit));
        subscriptions.Add(gameEventBus.Subscribe<OperatingDayStartedEvent>(OnDayStarted));
    }

    public void Unbind(IMetaRuntimeEventSink target)
    {
        if (!ReferenceEquals(runtime, target)) return;
        foreach (IDisposable subscription in subscriptions) subscription?.Dispose();
        subscriptions.Clear(); runtime = null;
    }

    public MetaRunEnvironmentSnapshot CaptureRunEnvironment()
    {
        float multiplier = 1f;
        DungeonDifficulty difficulty = DungeonDifficulty.Normal;
        if (threatRuntime.Settings != null)
        {
            multiplier = threatRuntime.Settings.GetDifficultyMultiplier();
            difficulty = DungeonDifficultyRules.FromLegacy(threatRuntime.Settings.difficulty);
        }
        return new MetaRunEnvironmentSnapshot(multiplier, difficulty,
            runVariables?.GetSurvivalPressure() ?? DungeonSurvivalPressure.Standard);
    }

    public void PublishUpgradePurchased(MetaUpgradePurchasedEvent purchasedEvent, string message)
    {
        gameEventBus.Publish(purchasedEvent);
        gameEventBus.RaiseAlert("계승 강화", message, EventAlertImportance.Medium, "계승");
    }

    public void PublishRunResult(RunResultReadyEvent readyEvent)
    {
        gameEventBus.Publish(readyEvent);
        gameEventBus.RaiseAlert("런 결과 정산", readyEvent.result?.ToDetailText() ?? string.Empty, EventAlertImportance.High, "계승");
    }

    public void ShowRunResult(RunResultSnapshot result) => panelService.Show(result);

    public static string GetOwnerName(CharacterActor owner)
    {
        CharacterIdentity identity = owner != null ? owner.Identity : null;
        return identity != null ? identity.DisplayName : owner != null ? owner.name : "사장";
    }

    private IMetaRuntimeEventSink Required => runtime ?? throw new InvalidOperationException("Meta adapter is not bound.");
    private void OnOwnerRunEnded(OwnerRunEndedEvent e) => Required.EndRun(GetOwnerName(e.OwnerActor), e.Reason, e.Outcome);
    private void OnSynthesis(FacilitySynthesisCompletedEvent e)
    {
        BuildingSO building = e.result.ResultBuilding != null ? e.result.ResultBuilding.BuildingData : e.result.Recipe?.resultBuilding;
        Required.RecordSynthesis(e.result.Recipe != null ? e.result.Recipe.recipeId : string.Empty, building != null ? building.id : -1);
    }
    private void OnResearch(BlueprintResearchCompletedEvent e) => Required.RecordResearchRecipes(e.unlockResult.UnlockedRecipes);
    private void OnThreatWarning(InvasionThreatWarningEvent e) => Required.RecordThreat(e.snapshot.stage, e.snapshot.threat);
    private void OnCandidate(InvasionCandidateEvent e) => Required.RecordThreat(e.snapshot.stage, e.snapshot.threat);
    private void OnInvasionStarted(InvasionStartedEvent e) => Required.RecordThreat(e.snapshot.stage, e.snapshot.threat);
    private void OnInvasionResolved(InvasionResolvedEvent e) => Required.RecordInvasionResolved(e.defended);
    private void OnDayReport(OperatingDayReportEvent e) { if (e.report != null) Required.RecordOperatingDayReport(e.report.day); }
    private void OnFacilityVisit(FacilityVisitEvent e) { if (e.facility?.BuildingData != null) Required.RecordFacilityDiscovery(e.facility.BuildingData.id); }
    private void OnDayStarted(OperatingDayStartedEvent e) => Required.RecordOperatingDayStarted(e.day);
}

public sealed class MetaRunSceneTransitionAdapter : IMetaRunSceneTransitionPort
{
    private readonly IDungeonSceneNavigator navigator;
    public MetaRunSceneTransitionAdapter(IDungeonSceneNavigator navigator)
    {
        this.navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
    }
    public bool IsTransitioning => navigator.IsTransitioning;
    public void StartNewRun() => navigator.StartNewGame();
}

/// <summary>
/// Default-assembly Unity/settings adapter. Presentation owns the palette and
/// asks this port to attach the settings-aware runtime to generated canvases.
/// </summary>
public sealed class RunResultThemeApplicationAdapter : IRunResultThemeQuery
{
    private readonly ITmpKoreanFontService fontService;
    private readonly IUiClock uiClock;
    private readonly IDungeonUserSettingsService userSettings;

    public RunResultThemeApplicationAdapter(
        ITmpKoreanFontService fontService,
        IUiClock uiClock,
        IDungeonUserSettingsService userSettings)
    {
        this.fontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.userSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
    }

    private bool HighContrast => userSettings.Current.highContrast;

    public UnityEngine.Color ResultScrim =>
        DungeonUiThemePalette.ResultScrim(HighContrast);
    public UnityEngine.Color Panel =>
        DungeonUiThemePalette.Panel(HighContrast);
    public UnityEngine.Color TextPrimary =>
        DungeonUiThemePalette.TextPrimary(HighContrast);

    public void StylePrimaryButton(UnityEngine.UI.Button button) =>
        DungeonUiThemePalette.StyleButton(
            button,
            HighContrast,
            userSettings.Current.reducedMotion,
            selected: true);

    public void Apply(UnityEngine.Canvas canvas)
    {
        DungeonUiThemeRuntime.Ensure(canvas, fontService, uiClock, userSettings)
            .ApplyNow();
    }
}
