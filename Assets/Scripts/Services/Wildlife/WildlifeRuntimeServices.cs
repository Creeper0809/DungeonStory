using System;
using DungeonStory.Foundation;

public sealed class WildlifeWorldServices
{
    public WildlifeWorldServices(
        IGridSystemProvider grid,
        IWildlifeSpeciesCatalogProvider species,
        IGameSessionStateProvider session,
        IWildlifeEcosystemRuntime ecosystem,
        IMainCameraProvider mainCamera,
        IGridPathSearchBroker pathSearch,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldItemStackRuntime items,
        IGameCalendar calendar,
        IGameEventBus events,
        IDiseaseDefinitionCatalog diseases)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        Species = species ?? throw new ArgumentNullException(nameof(species));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Ecosystem = ecosystem ?? throw new ArgumentNullException(nameof(ecosystem));
        MainCamera = mainCamera ?? throw new ArgumentNullException(nameof(mainCamera));
        PathSearch = pathSearch ?? throw new ArgumentNullException(nameof(pathSearch));
        WorldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        Diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
    }

    public IGridSystemProvider Grid { get; }
    public IWildlifeSpeciesCatalogProvider Species { get; }
    public IGameSessionStateProvider Session { get; }
    public IWildlifeEcosystemRuntime Ecosystem { get; }
    public IMainCameraProvider MainCamera { get; }
    public IGridPathSearchBroker PathSearch { get; }
    public ICharacterAiWorldRegistry WorldRegistry { get; }
    public IWorldItemStackRuntime Items { get; }
    public IGameCalendar Calendar { get; }
    public IGameEventBus Events { get; }
    public IDiseaseDefinitionCatalog Diseases { get; }
}

public sealed class WildlifeCombatServices
{
    public WildlifeCombatServices(
        ICombatResolutionService resolution,
        ICombatEquipmentRuntime equipment,
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthCommand bodyHealthCommands,
        ICombatLineOfSightService lineOfSight,
        CombatCoverServices coverServices,
        ICombatAmmoResupplyRuntime ammoResupply,
        IWildlifeCarcassService carcasses)
    {
        Resolution = resolution ?? throw new ArgumentNullException(nameof(resolution));
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        BodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        BodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        LineOfSight = lineOfSight ?? throw new ArgumentNullException(nameof(lineOfSight));
        CoverServices = coverServices
            ?? throw new ArgumentNullException(nameof(coverServices));
        AmmoResupply = ammoResupply ?? throw new ArgumentNullException(nameof(ammoResupply));
        Carcasses = carcasses ?? throw new ArgumentNullException(nameof(carcasses));
    }

    public ICombatResolutionService Resolution { get; }
    public ICombatEquipmentRuntime Equipment { get; }
    public ICharacterBodyHealthQuery BodyHealthQuery { get; }
    public ICharacterBodyHealthCommand BodyHealthCommands { get; }
    public ICombatLineOfSightService LineOfSight { get; }
    public CombatCoverServices CoverServices { get; }
    public ICombatCoverQuery Cover => CoverServices.Query;
    public ICombatCoverDurabilityRegistry CoverDurability =>
        CoverServices.Durability;
    public ICombatAmmoResupplyRuntime AmmoResupply { get; }
    public IWildlifeCarcassService Carcasses { get; }
}

public sealed class WildlifeExecutionServices
{
    public WildlifeExecutionServices(
        IGameClock clock,
        IRandomStreamProvider randomStreams,
        IDoorAccessQuery doors,
        ICharacterAiPerformanceRecorder performance,
        IDungeonDebugRuleQuery debugRules,
        IWorldUiHierarchy worldUiHierarchy)
    {
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        RandomStreams = randomStreams ?? throw new ArgumentNullException(nameof(randomStreams));
        Doors = doors ?? throw new ArgumentNullException(nameof(doors));
        Performance = performance ?? throw new ArgumentNullException(nameof(performance));
        DebugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        WorldUiHierarchy = worldUiHierarchy
            ?? throw new ArgumentNullException(nameof(worldUiHierarchy));
    }

    public IGameClock Clock { get; }
    public IRandomStreamProvider RandomStreams { get; }
    public IDoorAccessQuery Doors { get; }
    public ICharacterAiPerformanceRecorder Performance { get; }
    public IDungeonDebugRuleQuery DebugRules { get; }
    public IWorldUiHierarchy WorldUiHierarchy { get; }
}

public sealed class WildlifeRestoreServices
{
    public WildlifeRestoreServices(
        IRestoreWorldCandidateQuery worldCandidates,
        IRestoreWorldCandidatePublisher candidatePublisher)
    {
        WorldCandidates = worldCandidates
            ?? throw new ArgumentNullException(nameof(worldCandidates));
        CandidatePublisher = candidatePublisher
            ?? throw new ArgumentNullException(nameof(candidatePublisher));
    }

    public IRestoreWorldCandidateQuery WorldCandidates { get; }
    public IRestoreWorldCandidatePublisher CandidatePublisher { get; }
}
