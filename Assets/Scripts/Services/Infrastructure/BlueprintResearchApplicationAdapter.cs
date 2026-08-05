using System;
using DungeonStory.Foundation;

/// <summary>
/// Owns Foundation-facing composition for the scene research adapter. Research
/// state and rules remain exposed through the named Research contracts.
/// </summary>
public sealed class BlueprintResearchApplicationAdapter
{
    private readonly IGameEventBus gameEventBus;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly IUiClock uiClock;

    public BlueprintResearchApplicationAdapter(
        IGameEventBus gameEventBus,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IDungeonDebugRuleQuery debugRules,
        IUiClock uiClock)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.debugRules = debugRules
            ?? throw new ArgumentNullException(nameof(debugRules));
        this.uiClock = uiClock
            ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public BlueprintResearchState CreateState() =>
        new BlueprintResearchState(aggregateRootStore);

    public int PublishedRestoreRevision =>
        aggregateRootStore.PublishedRestoreRevision;

    public bool IsInstantWorkEnabled =>
        debugRules.IsEnabled(DungeonDebugCheat.InstantWork);

    public float UnscaledTime => uiClock.Time;

    public IDisposable SubscribeToShopPurchased(
        Action<FacilityShopPurchasedEvent> handler)
    {
        return gameEventBus.Subscribe(
            handler ?? throw new ArgumentNullException(nameof(handler)));
    }

    public void Publish<TEvent>(TEvent eventData)
    {
        gameEventBus.Publish(eventData);
    }

    public void RaiseLowAlert(
        string title,
        string message,
        string category)
    {
        gameEventBus.RaiseAlert(
            title,
            message,
            EventAlertImportance.Low,
            category);
    }

    public void RaiseMediumAlert(
        string title,
        string message,
        string category)
    {
        gameEventBus.RaiseAlert(
            title,
            message,
            EventAlertImportance.Medium,
            category);
    }

    public void RaiseHighAlert(
        string title,
        string message,
        string category)
    {
        gameEventBus.RaiseAlert(
            title,
            message,
            EventAlertImportance.High,
            category);
    }
}

/// <summary>
/// Retains the editor/debug construction surface while production composition
/// injects the Foundation adapter as a single required dependency.
/// </summary>
public static class BlueprintResearchConstructionApplicationAdapter
{
    public static void Construct(
        this BlueprintResearchRuntime runtime,
        IFacilityShopUnlockStateService shopUnlockStateService,
        IFacilityShopCatalog facilityShopCatalog,
        IFacilityCandidateCache facilityCandidateCache,
        IWorkforceReplanService workforceReplanService,
        IGameEventBus gameEventBus,
        IWorldItemStackRuntime itemStackRuntime,
        BlueprintResearchProjectCoordinator projectCoordinator,
        IWorldDropZoneQuery worldDropZoneQuery,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IDungeonDebugRuleQuery debugRules,
        IUiClock uiClock)
    {
        if (runtime == null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        runtime.ConstructRuntime(
            shopUnlockStateService,
            facilityShopCatalog,
            facilityCandidateCache,
            workforceReplanService,
            itemStackRuntime,
            projectCoordinator,
            worldDropZoneQuery,
            new BlueprintResearchApplicationAdapter(
                gameEventBus,
                aggregateRootStore,
                debugRules,
                uiClock));
    }
}
