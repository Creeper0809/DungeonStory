using System;
using System.Collections.Generic;
using System.Linq;

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
    OffenseWorldMapPanel ShowWorldMap();
    OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime);
}

public sealed class OffenseExpeditionMemberQuery : IOffenseExpeditionMemberQuery
{
    private readonly ICharacterWorldQuery characterWorld;
    private readonly ICharacterPerformanceQuery performance;
    private readonly ICharacterSettlementStandingQuery settlementStandings;

    public OffenseExpeditionMemberQuery(
        ICharacterWorldQuery characterWorld,
        ICharacterPerformanceQuery performance,
        ICharacterSettlementStandingQuery settlementStandings)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.settlementStandings = settlementStandings
            ?? throw new ArgumentNullException(nameof(settlementStandings));
    }

    public IReadOnlyList<CharacterActor> GetAvailableMemberActors()
    {
        return OffenseExpeditionService
            .GetDistinctMembers(characterWorld.Characters)
            .Where((actor) => OffenseExpeditionService.CanJoinExpedition(actor, out _))
            .Where((actor) => settlementStandings.CanJoinExpedition(actor, out _))
            .OrderByDescending(actor =>
                OffenseExpeditionService.CalculateMemberPower(actor, performance))
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
    private readonly IOffenseCampaignQuery campaign;
    private readonly IOffenseCampaignCommands campaignCommands;
    private readonly IOffensePanelFactory panelFactory;
    private readonly IOffensePanelButtonFactory buttonFactory;

    public OffensePanelService(
        OffenseSceneRuntimeReferences runtimeReferences,
        IOffensePanelFactory panelFactory,
        IOffensePanelButtonFactory buttonFactory,
        IOffenseCampaignQuery campaign,
        IOffenseCampaignCommands campaignCommands)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
        this.campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        this.campaignCommands = campaignCommands
            ?? throw new ArgumentNullException(nameof(campaignCommands));
        this.panelFactory = panelFactory
            ?? throw new ArgumentNullException(nameof(panelFactory));
        this.buttonFactory = buttonFactory
            ?? throw new ArgumentNullException(nameof(buttonFactory));
    }

    public OffenseWorldMapPanel ShowWorldMap()
    {
        runtimeReferences.ExpeditionPanel?.Hide();
        OffenseWorldMapPanel panel = runtimeReferences.WorldMapPanel
            ?? panelFactory.CreateWorldMapPanel();
        panel.Bind(campaign, campaignCommands, buttonFactory);
        return panel;
    }

    public OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime)
    {
        if (runtime == null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        runtimeReferences.WorldMapPanel?.Hide();
        OffenseExpeditionPanel panel = runtimeReferences.ExpeditionPanel
            ?? panelFactory.CreateExpeditionPanel();
        panel.Bind(runtime, campaign, buttonFactory);
        return panel;
    }
}
