using System;
using DungeonStory.Foundation;

public sealed class OffenseReturnArrivalWorldServices
{
    public OffenseReturnArrivalWorldServices(
        IGridSystemProvider grid,
        IWorldDropZoneQuery dropZones,
        ICharacterSpawnerProvider spawners,
        ICharacterSpawnObjectFactory characterFactory,
        IInvasionIntruderDataProvider intruderData,
        ICharacterAiWorldRegistry worldRegistry,
        IBuildingWorldQuery buildings,
        DungeonRuntimeAggregateRootStore aggregateRoots)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        DropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        Spawners = spawners
            ?? throw new ArgumentNullException(nameof(spawners));
        CharacterFactory = characterFactory
            ?? throw new ArgumentNullException(nameof(characterFactory));
        IntruderData = intruderData
            ?? throw new ArgumentNullException(nameof(intruderData));
        WorldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        Buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        AggregateRoots = aggregateRoots
            ?? throw new ArgumentNullException(nameof(aggregateRoots));
    }

    public IGridSystemProvider Grid { get; }
    public IWorldDropZoneQuery DropZones { get; }
    public ICharacterSpawnerProvider Spawners { get; }
    public ICharacterSpawnObjectFactory CharacterFactory { get; }
    public IInvasionIntruderDataProvider IntruderData { get; }
    public ICharacterAiWorldRegistry WorldRegistry { get; }
    public IBuildingWorldQuery Buildings { get; }
    public DungeonRuntimeAggregateRootStore AggregateRoots { get; }
}

public sealed class OffenseReturnArrivalDomainServices
{
    public OffenseReturnArrivalDomainServices(
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthCommand bodyHealthCommands,
        ICaptivityRuntime captivity,
        ICaptivityCommandService captivityCommands,
        IWildlifeRuntime wildlife,
        IWildlifeCaptureRuntime wildlifeCapture,
        IEnemyArchetypeCatalog enemyArchetypes,
        IEnemyIndividualFactory enemyIndividuals,
        IGameClock clock,
        IGameEventBus eventBus)
    {
        BodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        BodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        Captivity = captivity
            ?? throw new ArgumentNullException(nameof(captivity));
        CaptivityCommands = captivityCommands
            ?? throw new ArgumentNullException(nameof(captivityCommands));
        Wildlife = wildlife
            ?? throw new ArgumentNullException(nameof(wildlife));
        WildlifeCapture = wildlifeCapture
            ?? throw new ArgumentNullException(nameof(wildlifeCapture));
        EnemyArchetypes = enemyArchetypes
            ?? throw new ArgumentNullException(nameof(enemyArchetypes));
        EnemyIndividuals = enemyIndividuals
            ?? throw new ArgumentNullException(nameof(enemyIndividuals));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        EventBus = eventBus
            ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public ICharacterBodyHealthQuery BodyHealthQuery { get; }
    public ICharacterBodyHealthCommand BodyHealthCommands { get; }
    public ICaptivityRuntime Captivity { get; }
    public ICaptivityCommandService CaptivityCommands { get; }
    public IWildlifeRuntime Wildlife { get; }
    public IWildlifeCaptureRuntime WildlifeCapture { get; }
    public IEnemyArchetypeCatalog EnemyArchetypes { get; }
    public IEnemyIndividualFactory EnemyIndividuals { get; }
    public IGameClock Clock { get; }
    public IGameEventBus EventBus { get; }
}
