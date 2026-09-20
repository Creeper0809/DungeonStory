using System;
using DungeonStory.Foundation;

public sealed class CaptivityCharacterContext
{
    public CaptivityCharacterContext(
        ICharacterAiWorldRegistry worldRegistry,
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthCommand bodyHealthCommands,
        ICharacterBodyHealthMutationTransaction bodyHealthMutations,
        ICombatEquipmentRuntime combatEquipment,
        IWorldItemStackRuntime itemRuntime,
        IPhysicalItemSourcePublicationService physicalItemSources,
        IPhysicalItemBatchDispositionService batchDispositions,
        ICharacterPopulationService population,
        ICharacterNarrativeQuery narratives,
        IEnemyArchetypeCatalog enemyArchetypes)
    {
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        BodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        BodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        BodyHealthMutations = bodyHealthMutations
            ?? throw new ArgumentNullException(nameof(bodyHealthMutations));
        CombatEquipment = combatEquipment
            ?? throw new ArgumentNullException(nameof(combatEquipment));
        ItemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        PhysicalItemSources = physicalItemSources
            ?? throw new ArgumentNullException(nameof(physicalItemSources));
        BatchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
        Population = population
            ?? throw new ArgumentNullException(nameof(population));
        Narratives = narratives
            ?? throw new ArgumentNullException(nameof(narratives));
        EnemyArchetypes = enemyArchetypes
            ?? throw new ArgumentNullException(nameof(enemyArchetypes));
    }

    public ICharacterAiWorldRegistry WorldRegistry { get; }
    public ICharacterBodyHealthQuery BodyHealthQuery { get; }
    public ICharacterBodyHealthCommand BodyHealthCommands { get; }
    public ICharacterBodyHealthMutationTransaction BodyHealthMutations { get; }
    public ICombatEquipmentRuntime CombatEquipment { get; }
    public IWorldItemStackRuntime ItemRuntime { get; }
    public IPhysicalItemSourcePublicationService PhysicalItemSources { get; }
    public IPhysicalItemBatchDispositionService BatchDispositions { get; }
    public ICharacterPopulationService Population { get; }
    public ICharacterNarrativeQuery Narratives { get; }
    public IEnemyArchetypeCatalog EnemyArchetypes { get; }
}

public sealed class CaptivityWorldContext
{
    public CaptivityWorldContext(
        IGridSystemProvider gridProvider,
        IGridPathSearchBroker pathSearchBroker,
        IRoomLayoutCache roomLayoutCache,
        IDoorAccessQuery doorAccessQuery,
        IDoorAccessCommandService doorAccessCommands,
        IDoorAccessSubjectRegistry doorSubjectRegistry)
    {
        GridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        PathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        RoomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        DoorAccessQuery = doorAccessQuery
            ?? throw new ArgumentNullException(nameof(doorAccessQuery));
        DoorAccessCommands = doorAccessCommands
            ?? throw new ArgumentNullException(nameof(doorAccessCommands));
        DoorSubjectRegistry = doorSubjectRegistry
            ?? throw new ArgumentNullException(nameof(doorSubjectRegistry));
    }

    public IGridSystemProvider GridProvider { get; }
    public IGridPathSearchBroker PathSearchBroker { get; }
    public IRoomLayoutCache RoomLayoutCache { get; }
    public IDoorAccessQuery DoorAccessQuery { get; }
    public IDoorAccessCommandService DoorAccessCommands { get; }
    public IDoorAccessSubjectRegistry DoorSubjectRegistry { get; }
}

public sealed class CaptivitySessionContext
{
    public CaptivitySessionContext(
        IGameMoneyAccount money,
        CaptivityInteractionRegistry interactions,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IGameEventBus gameEventBus,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IEmploymentStandingCommand employmentStanding,
        CharacterMoodPolicyService moodPolicy,
        IFactionCampaignCommand factionCampaign,
        ISurvivalFoodCommand survivalFood,
        ICaptivityInterrogationCodexPort interrogationCodex,
        ICaptivePerformerMilestoneOutcomeCommitter performerMilestoneOutcomes,
        ICaptiveEscapeOutcomeCommitter captiveEscapeOutcomes,
        ICaptiveRansomOutcomeCommitter captiveRansomOutcomes,
        ICaptivityInteractionOutcomeCommitter captivityInteractionOutcomes)
    {
        Money = money ?? throw new ArgumentNullException(nameof(money));
        Interactions = interactions
            ?? throw new ArgumentNullException(nameof(interactions));
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        RandomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        GameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        AggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        EmploymentStanding = employmentStanding
            ?? throw new ArgumentNullException(nameof(employmentStanding));
        MoodPolicy = moodPolicy
            ?? throw new ArgumentNullException(nameof(moodPolicy));
        FactionCampaign = factionCampaign
            ?? throw new ArgumentNullException(nameof(factionCampaign));
        SurvivalFood = survivalFood
            ?? throw new ArgumentNullException(nameof(survivalFood));
        InterrogationCodex = interrogationCodex
            ?? throw new ArgumentNullException(nameof(interrogationCodex));
        PerformerMilestoneOutcomes = performerMilestoneOutcomes
            ?? throw new ArgumentNullException(nameof(performerMilestoneOutcomes));
        CaptiveEscapeOutcomes = captiveEscapeOutcomes
            ?? throw new ArgumentNullException(nameof(captiveEscapeOutcomes));
        CaptiveRansomOutcomes = captiveRansomOutcomes
            ?? throw new ArgumentNullException(nameof(captiveRansomOutcomes));
        CaptivityInteractionOutcomes = captivityInteractionOutcomes
            ?? throw new ArgumentNullException(nameof(captivityInteractionOutcomes));
    }

    public IGameMoneyAccount Money { get; }
    public CaptivityInteractionRegistry Interactions { get; }
    public IGameClock GameClock { get; }
    public IRandomStreamProvider RandomStreamProvider { get; }
    public IGameEventBus GameEventBus { get; }
    public DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
    public IEmploymentStandingCommand EmploymentStanding { get; }
    public CharacterMoodPolicyService MoodPolicy { get; }
    public IFactionCampaignCommand FactionCampaign { get; }
    public ISurvivalFoodCommand SurvivalFood { get; }
    public ICaptivityInterrogationCodexPort InterrogationCodex { get; }
    public ICaptivePerformerMilestoneOutcomeCommitter PerformerMilestoneOutcomes { get; }
    public ICaptiveEscapeOutcomeCommitter CaptiveEscapeOutcomes { get; }
    public ICaptiveRansomOutcomeCommitter CaptiveRansomOutcomes { get; }
    public ICaptivityInteractionOutcomeCommitter CaptivityInteractionOutcomes { get; }
}
