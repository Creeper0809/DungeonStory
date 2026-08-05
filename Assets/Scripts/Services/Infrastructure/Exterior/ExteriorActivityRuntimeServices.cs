using System;
using DungeonStory.Foundation;
using VContainer;

public sealed class ExteriorActivityWorldServices
{
    public ExteriorActivityWorldServices(
        IGridSystemProvider grid,
        IWorldDropZoneQuery dropZones,
        WorldSimulationSceneReferences sceneReferences,
        IObjectResolver objectResolver,
        IRuntimeBuildingArchetypeCatalog buildingArchetypes,
        IRestoreWorldCandidateQuery restoreCandidates,
        IRestoreWorldCandidatePublisher candidatePublisher,
        IWorldItemStackRuntime items)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        DropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        SceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
        ObjectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
        BuildingArchetypes = buildingArchetypes
            ?? throw new ArgumentNullException(nameof(buildingArchetypes));
        RestoreCandidates = restoreCandidates
            ?? throw new ArgumentNullException(nameof(restoreCandidates));
        CandidatePublisher = candidatePublisher
            ?? throw new ArgumentNullException(nameof(candidatePublisher));
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IGridSystemProvider Grid { get; }
    public IWorldDropZoneQuery DropZones { get; }
    public WorldSimulationSceneReferences SceneReferences { get; }
    public IObjectResolver ObjectResolver { get; }
    public IRuntimeBuildingArchetypeCatalog BuildingArchetypes { get; }
    public IRestoreWorldCandidateQuery RestoreCandidates { get; }
    public IRestoreWorldCandidatePublisher CandidatePublisher { get; }
    public IWorldItemStackRuntime Items { get; }
}

public sealed class ExteriorActivityDomainServices
{
    public ExteriorActivityDomainServices(
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterMedicalCommand medicalCommands,
        ExteriorIncidentHandlerRegistry incidentHandlers,
        ISurvivalEnvironmentQuery survival,
        IExperiencePacingRuntime experiencePacing)
    {
        BodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        MedicalCommands = medicalCommands
            ?? throw new ArgumentNullException(nameof(medicalCommands));
        IncidentHandlers = incidentHandlers
            ?? throw new ArgumentNullException(nameof(incidentHandlers));
        Survival = survival
            ?? throw new ArgumentNullException(nameof(survival));
        ExperiencePacing = experiencePacing
            ?? throw new ArgumentNullException(nameof(experiencePacing));
    }

    public ICharacterBodyHealthQuery BodyHealthQuery { get; }
    public ICharacterMedicalCommand MedicalCommands { get; }
    public ExteriorIncidentHandlerRegistry IncidentHandlers { get; }
    public ISurvivalEnvironmentQuery Survival { get; }
    public IExperiencePacingRuntime ExperiencePacing { get; }
}

public sealed class ExteriorActivityExecutionServices
{
    public ExteriorActivityExecutionServices(
        IGameClock clock,
        IGameCalendar calendar,
        IRandomStreamProvider randomStreams)
    {
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        RandomStreams = randomStreams
            ?? throw new ArgumentNullException(nameof(randomStreams));
    }

    public IGameClock Clock { get; }
    public IGameCalendar Calendar { get; }
    public IRandomStreamProvider RandomStreams { get; }
}
