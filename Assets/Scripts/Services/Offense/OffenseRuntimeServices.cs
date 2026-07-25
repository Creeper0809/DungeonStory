using System;
using System.Collections.Generic;
using System.Linq;

public interface IOffenseWorldMapRuntimeProvider
{
    bool TryGetRuntime(out OffenseWorldMapRuntime runtime);
}

public interface IOffenseRewardRuntimeProvider
{
    bool TryGetRuntime(out OffenseRewardRuntime runtime);
}

public interface IOffenseExpeditionRuntimeProvider
{
    bool TryGetRuntime(out OffenseExpeditionRuntime runtime);
}

public interface IOffenseExpeditionMemberQuery
{
    IReadOnlyList<CharacterActor> GetAvailableMemberActors();
}

public interface IOffenseRewardCatalog
{
    IReadOnlyCollection<BuildingSO> Buildings { get; }
    IReadOnlyCollection<FacilityBlueprintSO> Blueprints { get; }
}

public interface IOffenseRewardSelector
{
    BuildingSO SelectRareFacility(
        OffenseRewardContext context,
        IReadOnlyCollection<int> additionallyExcludedBuildingIds);
    FacilityBlueprintSO SelectBlueprint(
        OffenseBlueprintRewardSpec rewardSpec,
        OffenseRewardContext context);
}

public interface IOffenseRewardGrantHandler
{
    string RewardTypeId { get; }
    OffenseRewardGrantResult Grant(
        OffenseRewardPreview reward,
        OffenseRewardContext context,
        IOffenseRewardSelector selector);
}

public interface IOffenseRewardGrantService
{
    IReadOnlyList<OffenseRewardGrantResult> GrantRewards(
        IEnumerable<OffenseRewardPreview> rewards,
        OffenseRewardContext context);
}

public interface IOffensePanelService
{
    OffenseWorldMapPanel ShowWorldMap(OffenseWorldMapRuntime runtime);
    OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime);
}

public sealed class OffenseWorldMapRuntimeProvider :
    IOffenseWorldMapRuntimeProvider
{
    private readonly OffenseSceneRuntimeReferences runtimeReferences;

    public OffenseWorldMapRuntimeProvider(
        OffenseSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out OffenseWorldMapRuntime runtime)
    {
        runtime = runtimeReferences.WorldMap;
        return runtime != null;
    }
}

public sealed class OffenseRewardRuntimeProvider :
    IOffenseRewardRuntimeProvider
{
    private readonly OffenseSceneRuntimeReferences runtimeReferences;

    public OffenseRewardRuntimeProvider(
        OffenseSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out OffenseRewardRuntime runtime)
    {
        runtime = runtimeReferences.Rewards;
        return runtime != null;
    }
}

public sealed class OffenseExpeditionRuntimeProvider :
    IOffenseExpeditionRuntimeProvider
{
    private readonly OffenseSceneRuntimeReferences runtimeReferences;

    public OffenseExpeditionRuntimeProvider(
        OffenseSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out OffenseExpeditionRuntime runtime)
    {
        runtime = runtimeReferences.Expedition;
        return runtime != null;
    }
}

public sealed class OffenseExpeditionMemberQuery : IOffenseExpeditionMemberQuery
{
    private readonly ICharacterWorldQuery characterWorld;

    public OffenseExpeditionMemberQuery(ICharacterWorldQuery characterWorld)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public IReadOnlyList<CharacterActor> GetAvailableMemberActors()
    {
        return OffenseExpeditionService
            .GetDistinctMembers(characterWorld.Characters)
            .Where((actor) => OffenseExpeditionService.CanJoinExpedition(actor, out _))
            .OrderByDescending(OffenseExpeditionService.CalculateMemberPower)
            .ToList();
    }
}

public sealed class DataCatalogOffenseRewardCatalog : IOffenseRewardCatalog
{
    private readonly IDataCatalog catalog;

    public DataCatalogOffenseRewardCatalog(IDataCatalog catalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyCollection<BuildingSO> Buildings => catalog
        .GetData<BuildingSO>()
        .Values
        .Where((building) => building != null)
        .ToArray();

    public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => catalog
        .GetData<FacilityBlueprintSO>()
        .Values
        .Where((blueprint) => blueprint != null)
        .ToArray();
}

public sealed class OffensePanelService : IOffensePanelService
{
    private readonly OffenseSceneRuntimeReferences runtimeReferences;
    private readonly IOffenseWorldMapRuntimeProvider worldMapProvider;
    private readonly IOffensePanelFactory panelFactory;
    private readonly IOffensePanelButtonFactory buttonFactory;

    public OffensePanelService(
        OffenseSceneRuntimeReferences runtimeReferences,
        IOffenseWorldMapRuntimeProvider worldMapProvider,
        IOffensePanelFactory panelFactory,
        IOffensePanelButtonFactory buttonFactory)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
        this.worldMapProvider = worldMapProvider
            ?? throw new ArgumentNullException(nameof(worldMapProvider));
        this.panelFactory = panelFactory
            ?? throw new ArgumentNullException(nameof(panelFactory));
        this.buttonFactory = buttonFactory
            ?? throw new ArgumentNullException(nameof(buttonFactory));
    }

    public OffenseWorldMapPanel ShowWorldMap(OffenseWorldMapRuntime runtime)
    {
        if (runtime == null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        runtimeReferences.ExpeditionPanel?.Hide();
        OffenseWorldMapPanel panel = runtimeReferences.WorldMapPanel
            ?? panelFactory.CreateWorldMapPanel();
        panel.Bind(runtime, buttonFactory);
        return panel;
    }

    public OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime)
    {
        if (runtime == null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap);
        runtimeReferences.WorldMapPanel?.Hide();
        OffenseExpeditionPanel panel = runtimeReferences.ExpeditionPanel
            ?? panelFactory.CreateExpeditionPanel();
        panel.Bind(runtime, worldMap, buttonFactory);
        return panel;
    }
}
