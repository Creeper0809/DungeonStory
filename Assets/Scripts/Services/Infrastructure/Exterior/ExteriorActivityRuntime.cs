using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public enum ExteriorZoneType
{
    Entrance = 0,
    DropZone = 1,
    ReceptionPoint = 2,
    GuardPost = 3,
    PatrolPoint = 4,
    OutdoorRestSpot = 5,
    ExpeditionStaging = 6,
    IncidentPoint = 7
}

public interface IExteriorZoneQuery
{
    IReadOnlyList<ExteriorZoneMarker> Zones { get; }
    IEnumerable<ExteriorZoneMarker> GetZones(ExteriorZoneType zoneType);
    bool TryGetZone(ExteriorZoneType zoneType, out ExteriorZoneMarker marker);
    ExteriorActivityOverviewSnapshot GetOverview();
}

public interface IExteriorPatrolRuntime
{
    float AveragePatrolReadiness { get; }
}

public interface IExteriorIncidentRuntime
{
    IReadOnlyList<ExteriorIncidentSaveData> ActiveIncidents { get; }
    IReadOnlyList<ExteriorIncidentRuntimeState> IncidentStates { get; }
    bool TryStartIncident(ExteriorIncidentKind kind, string text = null);
    bool TryExecutePrimaryAction(string incidentId, out string message);
    bool AutomaticIncidentChecksSuspended { get; }
    void SetAutomaticIncidentChecksSuspended(bool suspended);
}

public interface IExteriorActivityRuntime
{
    DungeonExteriorActivitySaveData Capture();
    void ValidateRestorePayload(DungeonExteriorActivitySaveData saveData);
    ExteriorActivityWorldRestoreCandidate BuildRestoreCandidate(
        DungeonExteriorActivitySaveData saveData);
    void PublishRestoreCandidate(ExteriorActivityWorldRestoreCandidate candidate);
}

public interface IExpeditionDepartureService
{
    bool TryBeginDeparture(
        OffenseExpeditionRun expedition,
        IReadOnlyList<CharacterActor> members,
        Func<bool> departureReady,
        Action completed,
        out string message);
}

public interface IExpeditionReturnService
{
    bool TryBeginReturn(CharacterActor actor, bool alive, Action completed, out string message);
}

[Serializable]
public sealed class DungeonExteriorActivitySaveData
{
    public const int CurrentVersion = 3;

    public int version = CurrentVersion;
    public int nextIncidentSequence = 1;
    public List<ExteriorZoneSaveData> zones = new List<ExteriorZoneSaveData>();
    public List<ExteriorIncidentRuntimeState> incidentStates =
        new List<ExteriorIncidentRuntimeState>();
}

[Serializable]
public sealed class ExteriorZoneSaveData
{
    public string zoneId = string.Empty;
    public string buildingInstanceId = string.Empty;
    public ExteriorZoneType zoneType;
    public int gridX;
    public int gridY;
    public float cleanliness = 100f;
    public float damage;
    public float patrolReadiness;
    public float receptionReadiness;
    public int waitingVisitors;
    public float firstImpressionBonus;
    public int completedWorks;
}

[Serializable]
public sealed class ExteriorIncidentSaveData
{
    public string incidentId = string.Empty;
    public ExteriorIncidentKind kind;
    public string zoneId = string.Empty;
    public string text = string.Empty;
    public float remainingSeconds;
}

public readonly struct ExteriorActivityOverviewSnapshot
{
    public ExteriorActivityOverviewSnapshot(
        int zoneCount,
        int dropZoneCount,
        int incidentCount,
        float averageCleanliness,
        float averageDamage,
        float averagePatrolReadiness,
        float averageReceptionReadiness)
    {
        ZoneCount = zoneCount;
        DropZoneCount = dropZoneCount;
        IncidentCount = incidentCount;
        AverageCleanliness = averageCleanliness;
        AverageDamage = averageDamage;
        AveragePatrolReadiness = averagePatrolReadiness;
        AverageReceptionReadiness = averageReceptionReadiness;
    }

    public int ZoneCount { get; }
    public int DropZoneCount { get; }
    public int IncidentCount { get; }
    public float AverageCleanliness { get; }
    public float AverageDamage { get; }
    public float AveragePatrolReadiness { get; }
    public float AverageReceptionReadiness { get; }
}

public sealed class ExteriorActivityRuntime :
    IExteriorActivityRuntime,
    IExteriorPatrolRuntime,
    IExteriorIncidentRuntime,
    IExpeditionDepartureService,
    IExpeditionReturnService,
    IDungeonRestoreTransactionParticipant,
    IStartable,
    ITickable,
    IDisposable
{
    private const string RestoreParticipantId =
        "300.world.exterior-zones";
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("ExteriorActivityRuntime.Tick");

    private const float ConditionTickSeconds = 20f;
    private const float IncidentCheckSeconds = 180f;
    private static readonly GridLayer[] MarkerLayers =
    {
        GridLayer.FloorOverlay,
        GridLayer.WallFixture,
        GridLayer.CeilingFixture,
        GridLayer.Building,
        GridLayer.Hallway
    };

    private List<ExteriorZoneMarker> zones = new List<ExteriorZoneMarker>();
    private IReadOnlyList<ExteriorZoneMarker> zonesView;
    private DungeonStory.Exterior.ExteriorIncidentAggregate<
        ExteriorIncidentRuntimeState> incidentAggregate =
            CreateIncidentAggregate();
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldDropZoneQuery dropZoneQuery;
    private readonly WorldSimulationSceneReferences sceneReferences;
    private readonly IObjectResolver objectResolver;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterMedicalCommand medicalCommands;
    private readonly ExteriorIncidentHandlerRegistry incidentHandlers;
    private readonly ExteriorActivityApplicationAdapter applicationAdapter;
    private readonly IExperiencePacingRuntime experiencePacing;
    private readonly IRuntimeBuildingArchetypeCatalog buildingArchetypes;
    private readonly ExteriorActivityRestoreCoordinator restoreCoordinator;
    private ExteriorActivityCoroutineHost coroutineHost;
    private float nextConditionTick;
    private float nextIncidentCheck;
    private int incidentSequence;
    private ExteriorActivityPublication activePublication;
    private bool automaticIncidentChecksSuspended;

    private static DungeonStory.Exterior.ExteriorIncidentAggregate<
        ExteriorIncidentRuntimeState> CreateIncidentAggregate() => new(
            state => state.IsTerminal,
            state => state.remainingSeconds,
            (state, remainingSeconds) =>
                state.remainingSeconds = remainingSeconds);

    public ExteriorActivityRuntime(
        ExteriorActivityWorldServices world,
        ExteriorActivityDomainServices domain,
        ExteriorActivityExecutionServices execution)
    {
        ExteriorActivityWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        ExteriorActivityDomainServices requiredDomain = domain
            ?? throw new ArgumentNullException(nameof(domain));
        ExteriorActivityExecutionServices requiredExecution = execution
            ?? throw new ArgumentNullException(nameof(execution));
        gridSystemProvider = requiredWorld.Grid;
        dropZoneQuery = requiredWorld.DropZones;
        sceneReferences = requiredWorld.SceneReferences;
        objectResolver = requiredWorld.ObjectResolver;
        buildingArchetypes = requiredWorld.BuildingArchetypes;
        bodyHealthQuery = requiredDomain.BodyHealthQuery;
        medicalCommands = requiredDomain.MedicalCommands;
        incidentHandlers = requiredDomain.IncidentHandlers;
        experiencePacing = requiredDomain.ExperiencePacing;
        applicationAdapter = new ExteriorActivityApplicationAdapter(
            requiredExecution.Clock,
            requiredExecution.Calendar,
            requiredExecution.RandomStreams.Get("exterior-incidents"),
            requiredDomain.Survival);
        restoreCoordinator = new ExteriorActivityRestoreCoordinator(
            requiredWorld,
            requiredDomain);
        zonesView = zones.AsReadOnly();
    }

    public IReadOnlyList<ExteriorZoneMarker> Zones => zonesView;
    public IReadOnlyList<ExteriorIncidentSaveData> ActiveIncidents =>
        incidentAggregate.States
        .Where(state => state != null && !state.IsTerminal)
        .Select(CreateIncidentSaveData)
        .ToArray();
    public IReadOnlyList<ExteriorIncidentRuntimeState> IncidentStates =>
        incidentAggregate.States;
    public string ParticipantId => RestoreParticipantId;
    public float AveragePatrolReadiness => zones
        .Where(zone => zone != null
            && (zone.ZoneType == ExteriorZoneType.GuardPost
                || zone.ZoneType == ExteriorZoneType.PatrolPoint))
        .Select(zone => zone.PatrolReadiness)
        .DefaultIfEmpty(0f)
        .Average();
    public bool AutomaticIncidentChecksSuspended =>
        automaticIncidentChecksSuspended;

    public void SetAutomaticIncidentChecksSuspended(bool suspended)
    {
        automaticIncidentChecksSuspended = suspended;
        if (!suspended)
        {
            nextIncidentCheck = applicationAdapter.Time + IncidentCheckSeconds;
        }
    }

    public void Start()
    {
        EnsureRuntimeObjects();
        EnsureDefaultZones();
        nextConditionTick = applicationAdapter.Time + ConditionTickSeconds;
        nextIncidentCheck = applicationAdapter.Time + IncidentCheckSeconds;
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        if (!Application.isPlaying || zones.Count == 0)
        {
            return;
        }

        float now = applicationAdapter.Time;
        if (now >= nextConditionTick)
        {
            float elapsed = Mathf.Max(ConditionTickSeconds, now - (nextConditionTick - ConditionTickSeconds));
            TickExteriorConditions(elapsed);
            nextConditionTick = now + ConditionTickSeconds;
        }

        TickIncidentStates(applicationAdapter.DeltaTime);

        if (!automaticIncidentChecksSuspended && now >= nextIncidentCheck)
        {
            TryStartRandomIncident();
            nextIncidentCheck = now + IncidentCheckSeconds;
        }
    }

    public void Dispose()
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            ExteriorZoneMarker zone = zones[i];
            if (zone != null && zone.gameObject != null)
            {
                UnityEngine.Object.Destroy(zone.gameObject);
            }
        }

        zones.Clear();
        incidentAggregate.Clear();
        if (coroutineHost != null && coroutineHost.gameObject != null)
        {
            UnityEngine.Object.Destroy(coroutineHost.gameObject);
        }
    }

    public IEnumerable<ExteriorZoneMarker> GetZones(ExteriorZoneType zoneType)
    {
        return zones.Where(zone => zone != null && zone.ZoneType == zoneType);
    }

    public bool TryGetZone(ExteriorZoneType zoneType, out ExteriorZoneMarker marker)
    {
        marker = zones.FirstOrDefault(zone => zone != null && zone.ZoneType == zoneType);
        return marker != null;
    }

    public ExteriorActivityOverviewSnapshot GetOverview()
    {
        ExteriorZoneMarker[] activeZones = zones.Where(zone => zone != null).ToArray();
        return new ExteriorActivityOverviewSnapshot(
            activeZones.Length,
            activeZones.Count(zone => zone.ZoneType == ExteriorZoneType.DropZone),
            incidentAggregate.ActiveCount,
            activeZones.Select(zone => zone.Cleanliness).DefaultIfEmpty(100f).Average(),
            activeZones.Select(zone => zone.Damage).DefaultIfEmpty(0f).Average(),
            activeZones.Select(zone => zone.PatrolReadiness).DefaultIfEmpty(0f).Average(),
            activeZones.Select(zone => zone.ReceptionReadiness).DefaultIfEmpty(0f).Average());
    }

    public DungeonExteriorActivitySaveData Capture()
    {
        return new DungeonExteriorActivitySaveData
        {
            version = DungeonExteriorActivitySaveData.CurrentVersion,
            nextIncidentSequence = Mathf.Max(1, incidentSequence + 1),
            zones = zones
                .Where(zone => zone != null)
                .Select(zone => zone.CreateSaveData())
                .ToList(),
            incidentStates = incidentAggregate.States
                .Select(state => state?.Clone())
                .Where(state => state != null)
                .ToList()
        };
    }

    public void ValidateRestorePayload(DungeonExteriorActivitySaveData saveData)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        restoreCoordinator.Validate(saveData, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Exterior activity payload is invalid: "
                + string.Join(" | ", report.Errors));
        }
    }

    public ExteriorActivityWorldRestoreCandidate BuildRestoreCandidate(
        DungeonExteriorActivitySaveData saveData) =>
        restoreCoordinator.Build(saveData);

    public void PublishRestoreCandidate(
        ExteriorActivityWorldRestoreCandidate candidate) =>
        restoreCoordinator.Adopt(candidate);

    public void BeginRestoreCandidate()
    {
        restoreCoordinator.Begin();
    }

    public void PublishRestoreCandidate()
    {
        if (activePublication != null)
        {
            throw new InvalidOperationException(
                "An exterior publication is already pending completion.");
        }

        ExteriorActivityWorldRestoreCandidate candidate =
            restoreCoordinator.Publish();
        DungeonStory.Exterior.ExteriorIncidentAggregate<
            ExteriorIncidentRuntimeState> restoredIncidents =
                CreateIncidentAggregate();
        restoredIncidents.ReplaceAll(candidate.IncidentStates);
        ExteriorActivityPublication publication =
            new ExteriorActivityPublication(
                candidate,
                zones,
                zonesView,
                incidentAggregate,
                incidentSequence,
                nextConditionTick,
                nextIncidentCheck);
        activePublication = publication;
        zones = candidate.Zones;
        zonesView = zones.AsReadOnly();
        incidentAggregate = restoredIncidents;
        incidentSequence = Math.Max(0, candidate.NextIncidentSequence - 1);
        nextConditionTick = applicationAdapter.Time + ConditionTickSeconds;
        nextIncidentCheck = applicationAdapter.Time + IncidentCheckSeconds;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        ExteriorActivityPublication publication = activePublication
            ?? throw new InvalidOperationException(
                "No exterior publication is pending rollback.");
        zones = publication.PreviousZones;
        zonesView = publication.PreviousZonesView;
        incidentAggregate = publication.PreviousIncidents;
        incidentSequence = publication.PreviousIncidentSequence;
        nextConditionTick = publication.PreviousConditionTick;
        nextIncidentCheck = publication.PreviousIncidentCheck;
        try
        {
            restoreCoordinator.RollbackPublished();
        }
        finally
        {
            activePublication = null;
        }
    }

    public void CompleteRestoreCandidate()
    {
        ExteriorActivityPublication publication = activePublication
            ?? throw new InvalidOperationException(
                "No exterior publication is pending completion.");

        foreach (ExteriorZoneMarker oldZone in publication.PreviousZones)
        {
            if (oldZone != null)
            {
                oldZone.gameObject.SetActive(false);
            }
        }
        foreach (ExteriorZoneMarker zone in zones)
        {
            zone.PublishDetachedRestore();
            zone.gameObject.SetActive(true);
        }
        foreach (ExteriorZoneMarker oldZone in publication.PreviousZones)
        {
            if (oldZone != null)
            {
                oldZone.RetireForWorldReplacement();
            }
        }

        ProjectAllIncidentStates();
        restoreCoordinator.CompletePublished();
        activePublication = null;
    }

    public void DiscardRestoreCandidate()
    {
        if (activePublication != null)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        restoreCoordinator.Discard();
    }

    private sealed class ExteriorActivityPublication
    {
        public ExteriorActivityPublication(
            ExteriorActivityWorldRestoreCandidate candidate,
            List<ExteriorZoneMarker> previousZones,
            IReadOnlyList<ExteriorZoneMarker> previousZonesView,
            DungeonStory.Exterior.ExteriorIncidentAggregate<
                ExteriorIncidentRuntimeState> previousIncidents,
            int previousIncidentSequence,
            float previousConditionTick,
            float previousIncidentCheck)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            PreviousZones = previousZones ?? throw new ArgumentNullException(nameof(previousZones));
            PreviousZonesView = previousZonesView ?? throw new ArgumentNullException(nameof(previousZonesView));
            PreviousIncidents = previousIncidents ?? throw new ArgumentNullException(nameof(previousIncidents));
            PreviousIncidentSequence = previousIncidentSequence;
            PreviousConditionTick = previousConditionTick;
            PreviousIncidentCheck = previousIncidentCheck;
        }

        public ExteriorActivityWorldRestoreCandidate Candidate { get; }
        public List<ExteriorZoneMarker> PreviousZones { get; }
        public IReadOnlyList<ExteriorZoneMarker> PreviousZonesView { get; }
        public DungeonStory.Exterior.ExteriorIncidentAggregate<
            ExteriorIncidentRuntimeState> PreviousIncidents { get; }
        public int PreviousIncidentSequence { get; }
        public float PreviousConditionTick { get; }
        public float PreviousIncidentCheck { get; }
    }

    public bool TryStartIncident(ExteriorIncidentKind kind, string text = null)
    {
        if (experiencePacing != null
            && !experiencePacing.CanStartExteriorIncident(kind))
        {
            return false;
        }
        if (experiencePacing != null)
        {
            int activeProblems = incidentAggregate.ActiveCount;
            if (experiencePacing.IsRehearsalActive)
            {
                activeProblems++;
            }
            if (activeProblems >= experiencePacing.MaximumConcurrentExternalProblems)
            {
                return false;
            }
        }
        if (!incidentHandlers.TryGet(kind, out IExteriorIncidentHandler handler))
        {
            return false;
        }

        ExteriorZoneMarker marker = SelectIncidentZone(kind);
        if (marker == null
            || incidentAggregate.AnyActive(state =>
                string.Equals(
                    state.zoneId,
                    marker.ZoneId,
                    StringComparison.Ordinal)))
        {
            return false;
        }

        string incidentId = $"incident:{kind}:{++incidentSequence}";
        ExteriorIncidentRuntimeState state = new ExteriorIncidentRuntimeState
        {
            incidentId = incidentId,
            kind = kind,
            zoneId = marker.ZoneId,
            text = string.IsNullOrWhiteSpace(text) ? handler.DefaultText : text,
            stage = ExteriorIncidentStage.Preparing,
            durationSeconds = handler.DurationSeconds,
            remainingSeconds = handler.DurationSeconds
        };
        if (!handler.TryBegin(state, marker, out string failureReason))
        {
            Debug.LogWarning(
                $"Exterior incident '{kind}' could not start: {failureReason}");
            return false;
        }

        ApplyIncidentTransition(incidentAggregate.Add(state));
        experiencePacing?.MarkExteriorIncidentStarted(kind);
        return true;
    }

    public bool TryExecutePrimaryAction(string incidentId, out string message)
    {
        ExteriorIncidentRuntimeState state = incidentAggregate.Find(candidate =>
            candidate != null
            && string.Equals(
                candidate.incidentId,
                incidentId,
                StringComparison.Ordinal));
        if (state == null)
        {
            message = "외부 사건을 찾을 수 없습니다.";
            return false;
        }

        ExteriorZoneMarker zone = zones.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.ZoneId,
                state.zoneId,
                StringComparison.Ordinal));
        if (zone == null
            || !incidentHandlers.TryGet(
                state.kind,
                out IExteriorIncidentHandler handler))
        {
            message = "외부 사건 대상 또는 처리기를 찾을 수 없습니다.";
            return false;
        }

        bool succeeded = false;
        string actionMessage = string.Empty;
        DungeonStory.Exterior.ExteriorIncidentTransition<
            ExteriorIncidentRuntimeState> transition = incidentAggregate.Mutate(
                state,
                current => succeeded = handler.TryExecutePrimaryAction(
                    current,
                    zone,
                    out actionMessage));
        ApplyIncidentTransition(transition);
        message = actionMessage;
        return succeeded;
    }

    public bool TryBeginDeparture(
        OffenseExpeditionRun expedition,
        IReadOnlyList<CharacterActor> members,
        Func<bool> departureReady,
        Action completed,
        out string message)
    {
        message = string.Empty;
        if (expedition == null || members == null || members.Count == 0)
        {
            message = "expedition-missing";
            return false;
        }

        if (!ResolveDeparturePoints(out ExteriorZoneMarker staging, out WorldGridEntryPoint entryPoint))
        {
            message = "expedition-staging-missing";
            return false;
        }

        EnsureRuntimeObjects();
        coroutineHost.StartCoroutine(DepartureRoutine(
            expedition,
            members,
            staging,
            entryPoint,
            departureReady,
            completed));
        message = "expedition-departure-started";
        return true;
    }

    public bool TryBeginReturn(CharacterActor actor, bool alive, Action completed, out string message)
    {
        message = string.Empty;
        if (actor == null || !alive)
        {
            completed?.Invoke();
            message = "return-skipped";
            return false;
        }

        if (!ResolveEntryPoint(out WorldGridEntryPoint entryPoint))
        {
            message = "return-entry-missing";
            return false;
        }

        EnsureRuntimeObjects();
        coroutineHost.StartCoroutine(ReturnRoutine(actor, entryPoint, completed));
        message = "expedition-return-started";
        return true;
    }

    private void EnsureRuntimeObjects()
    {
        if (coroutineHost != null)
        {
            return;
        }

        GameObject hostObject = new GameObject(nameof(ExteriorActivityCoroutineHost));
        UnityEngine.Object.DontDestroyOnLoad(hostObject);
        coroutineHost = hostObject.AddComponent<ExteriorActivityCoroutineHost>();
    }

    private void EnsureDefaultZones()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        foreach (ExteriorZoneMarker sceneMarker in sceneReferences.ExteriorZones)
        {
            if (sceneMarker != null && !zones.Contains(sceneMarker))
            {
                if (Application.isPlaying && sceneMarker.transform.parent == null)
                {
                    DungeonRuntimeHierarchy.Parent(sceneMarker.gameObject, DungeonRuntimeHierarchy.Exterior);
                }

                zones.Add(sceneMarker);
            }
        }

        if (zones.Any(zone => zone != null))
        {
            return;
        }

        ResolveEntryPoint(out WorldGridEntryPoint entryPoint);
        Vector2Int entrance = entryPoint.GridPosition;
        if (entrance == default && gridSystemProvider.TryGetManager(out GridSystemManager manager))
        {
            manager.TryGetEntranceGridPosition(out entrance);
        }

        Vector2Int dropoff = default;
        bool hasDropoff = dropZoneQuery.TryGetDeliveryDropoff(out dropoff);
        TryPlaceZone(grid, ExteriorZoneType.DropZone,
            CandidateCells(grid, entrance, GridCellAreaType.DropZone, hasDropoff ? dropoff : (Vector2Int?)null));
        TryPlaceZone(grid, ExteriorZoneType.ReceptionPoint,
            CandidateCells(grid, entrance, GridCellAreaType.Entrance, null)
                .Concat(CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null)));
        TryPlaceZone(grid, ExteriorZoneType.ExpeditionStaging,
            CandidateCells(grid, entrance, GridCellAreaType.Entrance, null)
                .Concat(CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null)));
        TryPlaceZone(grid, ExteriorZoneType.GuardPost,
            CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null));
        TryPlaceZone(grid, ExteriorZoneType.PatrolPoint,
            CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null));
        TryPlaceZone(grid, ExteriorZoneType.OutdoorRestSpot,
            CandidateCells(grid, entrance, GridCellAreaType.DropZone, null)
                .Concat(CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null)));
        TryPlaceZone(grid, ExteriorZoneType.IncidentPoint,
            CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null)
                .Concat(CandidateCells(grid, entrance, GridCellAreaType.DropZone, null)));
    }

    private bool TryPlaceZone(
        Grid grid,
        ExteriorZoneType zoneType,
        IEnumerable<Vector2Int> candidates)
    {
        if (zones.Any(zone => zone != null && zone.ZoneType == zoneType))
        {
            return false;
        }

        foreach (Vector2Int position in candidates ?? Enumerable.Empty<Vector2Int>())
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell == null || !grid.IsWalkable(position))
            {
                continue;
            }

            if (!TryGetFreeMarkerLayer(cell, out GridLayer markerLayer))
            {
                continue;
            }

            GameObject zoneObject = new GameObject($"ExteriorZone_{zoneType}_{position.x}_{position.y}");
            DungeonRuntimeHierarchy.Parent(zoneObject, DungeonRuntimeHierarchy.Exterior);
            ExteriorZoneMarker marker = zoneObject.AddComponent<ExteriorZoneMarker>();
            objectResolver.InjectGameObject(zoneObject);
            marker.InitializeRuntime(
                grid,
                position,
                zoneType,
                buildingArchetypes.RequireExteriorZone(zoneType, markerLayer));
            if (!zones.Contains(marker))
            {
                zones.Add(marker);
            }

            return true;
        }

        return false;
    }

    private static bool TryGetFreeMarkerLayer(GridCell cell, out GridLayer layer)
    {
        foreach (GridLayer candidate in MarkerLayers)
        {
            if (cell.CanOccupy(candidate))
            {
                layer = candidate;
                return true;
            }
        }

        layer = GridLayer.FloorOverlay;
        return false;
    }

    private static IEnumerable<Vector2Int> CandidateCells(
        Grid grid,
        Vector2Int entrance,
        GridCellAreaType areaType,
        Vector2Int? preferred)
    {
        if (grid == null)
        {
            yield break;
        }

        if (preferred.HasValue)
        {
            yield return preferred.Value;
        }

        foreach (GridCell cell in grid.GetCells()
                     .Where(cell => cell != null && cell.AreaType == areaType)
                     .OrderBy(cell => Distance(cell.Position, entrance))
                     .ThenBy(cell => cell.Position.y)
                     .ThenBy(cell => cell.Position.x))
        {
            if (preferred.HasValue && cell.Position == preferred.Value)
            {
                continue;
            }

            yield return cell.Position;
        }
    }

    private void TickExteriorConditions(float elapsedSeconds)
    {
        float steps = Mathf.Max(1f, elapsedSeconds / ConditionTickSeconds);
        foreach (ExteriorZoneMarker zone in zones)
        {
            if (zone == null
                || zone.ZoneType == ExteriorZoneType.Entrance
                || zone.ZoneType == ExteriorZoneType.ExpeditionStaging)
            {
                continue;
            }

            zone.ApplyExteriorWear(0.7f * steps, 0.08f * steps);
        }
    }

    private void TickIncidentStates(float deltaTime)
    {
        IReadOnlyList<DungeonStory.Exterior.ExteriorIncidentTransition<
            ExteriorIncidentRuntimeState>> transitions =
            incidentAggregate.Tick(deltaTime, TickIncidentState);
        foreach (DungeonStory.Exterior.ExteriorIncidentTransition<
                 ExteriorIncidentRuntimeState> transition in transitions)
        {
            ApplyIncidentTransition(transition);
        }
    }

    private void TickIncidentState(
        ExteriorIncidentRuntimeState state,
        float elapsed)
    {
        ExteriorZoneMarker zone = FindIncidentZone(state?.zoneId);
        if (state == null
            || zone == null
            || !incidentHandlers.TryGet(
                state.kind,
                out IExteriorIncidentHandler handler))
        {
            if (state != null)
            {
                state.stage = ExteriorIncidentStage.Failed;
            }
            return;
        }

        handler.Tick(state, zone, elapsed);
    }

    private void ApplyIncidentTransition(
        DungeonStory.Exterior.ExteriorIncidentTransition<
            ExteriorIncidentRuntimeState> transition)
    {
        ExteriorIncidentRuntimeState state = transition.State;
        ExteriorZoneMarker zone = FindIncidentZone(state?.zoneId);
        if (zone == null || state == null)
        {
            return;
        }

        if (transition.IsTerminal)
        {
            if (string.Equals(
                    zone.ActiveIncidentId,
                    state.incidentId,
                    StringComparison.Ordinal))
            {
                zone.ClearIncidentProjection();
            }
            return;
        }

        zone.ProjectIncident(
            state.kind,
            state.incidentId,
            state.text,
            transition.RemainingSeconds);
    }

    private void ProjectAllIncidentStates()
    {
        foreach (ExteriorZoneMarker zone in zones)
        {
            zone?.ClearIncidentProjection();
        }

        foreach (ExteriorIncidentRuntimeState state in
                 incidentAggregate.States.Where(value =>
                     value != null && !value.IsTerminal))
        {
            ExteriorZoneMarker zone = FindIncidentZone(state.zoneId);
            zone?.ProjectIncident(
                state.kind,
                state.incidentId,
                state.text,
                state.remainingSeconds);
        }
    }

    private ExteriorZoneMarker FindIncidentZone(string zoneId)
    {
        return zones.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.ZoneId,
                zoneId,
                StringComparison.Ordinal));
    }

    private static ExteriorIncidentSaveData CreateIncidentSaveData(
        ExteriorIncidentRuntimeState state)
    {
        return new ExteriorIncidentSaveData
        {
            incidentId = state.incidentId,
            kind = state.kind,
            zoneId = state.zoneId,
            text = state.text,
            remainingSeconds = state.remainingSeconds
        };
    }

    private bool TryStartRandomIncident()
    {
        if (incidentAggregate.ActiveCount > 0)
        {
            return false;
        }

        SurvivalEnvironmentSnapshot environment =
            applicationAdapter.Environment;
        float patrolReadiness = AveragePatrolReadiness;
        float incidentChance = ExteriorActivityApplicationAdapter
            .CalculateIncidentChance(
                environment,
                patrolReadiness,
                applicationAdapter.IsNight);
        if (!applicationAdapter.Chance(incidentChance))
        {
            return false;
        }

        IExteriorIncidentHandler[] candidates = incidentHandlers.All
            .Where(candidate => experiencePacing == null
                || experiencePacing.CanStartExteriorIncident(candidate.Kind))
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        float totalWeight = candidates.Sum(candidate =>
            GetIncidentSelectionWeight(
                candidate.Kind,
                environment,
                patrolReadiness));
        if (totalWeight <= 0.001f)
        {
            return false;
        }

        float roll = applicationAdapter.NextFloat() * totalWeight;
        IExteriorIncidentHandler selected = candidates[candidates.Length - 1];
        foreach (IExteriorIncidentHandler candidate in candidates)
        {
            roll -= GetIncidentSelectionWeight(
                candidate.Kind,
                environment,
                patrolReadiness);
            if (roll <= 0f)
            {
                selected = candidate;
                break;
            }
        }

        return TryStartIncident(selected.Kind);
    }

    public static float GetIncidentSelectionWeight(
        ExteriorIncidentKind kind,
        SurvivalEnvironmentSnapshot environment,
        float patrolReadiness)
    {
        return ExteriorActivityApplicationAdapter.GetIncidentSelectionWeight(
            kind,
            environment,
            patrolReadiness);
    }

    private ExteriorZoneMarker SelectIncidentZone(ExteriorIncidentKind kind)
    {
        if (kind is ExteriorIncidentKind.Thief
            or ExteriorIncidentKind.CargoDamage)
        {
            return zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.DropZone
                    && IsIncidentZoneAvailable(zone))
                ?? zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.IncidentPoint
                    && IsIncidentZoneAvailable(zone));
        }

        if (kind == ExteriorIncidentKind.InjuredReturnee)
        {
            return zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.Entrance
                    && IsIncidentZoneAvailable(zone))
                ?? zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.DropZone
                    && IsIncidentZoneAvailable(zone));
        }

        if (kind == ExteriorIncidentKind.PredatorApproach)
        {
            return zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.Entrance
                    && IsIncidentZoneAvailable(zone))
                ?? zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.IncidentPoint
                    && IsIncidentZoneAvailable(zone));
        }

        return zones.FirstOrDefault(zone => zone != null
                && zone.ZoneType == ExteriorZoneType.IncidentPoint
                && IsIncidentZoneAvailable(zone))
            ?? zones.FirstOrDefault(zone => zone != null
                && zone.ZoneType == ExteriorZoneType.ReceptionPoint
                && IsIncidentZoneAvailable(zone));
    }

    private bool IsIncidentZoneAvailable(ExteriorZoneMarker zone)
    {
        return zone != null
            && !incidentAggregate.AnyActive(state =>
                string.Equals(
                    state.zoneId,
                    zone.ZoneId,
                    StringComparison.Ordinal));
    }

    private bool ResolveDeparturePoints(out ExteriorZoneMarker staging, out WorldGridEntryPoint entryPoint)
    {
        staging = null;
        if (!ResolveEntryPoint(out entryPoint))
        {
            return false;
        }

        return TryGetZone(ExteriorZoneType.ExpeditionStaging, out staging)
            || TryGetZone(ExteriorZoneType.Entrance, out staging)
            || TryGetZone(ExteriorZoneType.ReceptionPoint, out staging);
    }

    private bool ResolveEntryPoint(out WorldGridEntryPoint entryPoint)
    {
        return dropZoneQuery.TryGetVisitorEntryPoint(out entryPoint);
    }

    private IEnumerator DepartureRoutine(
        OffenseExpeditionRun expedition,
        IReadOnlyList<CharacterActor> members,
        ExteriorZoneMarker staging,
        WorldGridEntryPoint entryPoint,
        Func<bool> departureReady,
        Action completed)
    {
        while (departureReady != null && !departureReady())
        {
            yield return null;
        }

        foreach (CharacterActor member in members)
        {
            if (member == null || member.IsDead)
            {
                continue;
            }

            member.SetLifecycleState(CharacterLifecycleState.PreparingExpedition);
            yield return MoveActorToGrid(member, staging.centerPos);
        }

        foreach (CharacterActor member in members)
        {
            if (member == null || member.IsDead)
            {
                continue;
            }

            member.SetLifecycleState(CharacterLifecycleState.DepartingExpedition);
            yield return MoveActorToGrid(member, entryPoint.GridPosition);
            yield return MoveActorToWorld(member, entryPoint.DoorPosition);
            yield return MoveActorToWorld(member, entryPoint.OutsidePosition);
            member.BeginExpedition();
        }

        completed?.Invoke();
    }

    private IEnumerator ReturnRoutine(CharacterActor actor, WorldGridEntryPoint entryPoint, Action completed)
    {
        actor.transform.position = entryPoint.OutsidePosition;
        actor.EndExpedition(alive: true);

        if (bodyHealthQuery.GetSnapshot(actor).Downed)
        {
            PlaceDownedReturneeOnGrid(actor, entryPoint);
            medicalCommands.NotifyCharacterDowned(actor);
            DefenseCombatPresentation.Ensure(actor)?.SetStatus("귀환 직후 구조 필요", combatActive: true);
            completed?.Invoke();
            yield break;
        }

        actor.SetLifecycleState(CharacterLifecycleState.ReturningExpedition);
        yield return MoveActorToWorld(actor, entryPoint.DoorPosition);
        yield return MoveActorToGrid(actor, entryPoint.GridPosition);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        completed?.Invoke();
    }

    private void PlaceDownedReturneeOnGrid(CharacterActor actor, WorldGridEntryPoint entryPoint)
    {
        if (actor == null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        Vector2Int returnCell = grid.GetXY(entryPoint.OutsidePosition);
        GridCell cell = grid.GetGridCell(returnCell);
        if (cell == null || !cell.IsWalkableArea || !grid.IsWalkable(returnCell))
        {
            if (!grid.TryFindNearestWalkablePosition(returnCell, out returnCell))
            {
                returnCell = entryPoint.GridPosition;
            }
        }

        actor.transform.position = grid.GetWorldPos(returnCell);
    }

    private IEnumerator MoveActorToGrid(CharacterActor actor, Vector2Int target)
    {
        if (actor == null)
        {
            yield break;
        }

        AbilityMove move = actor.GetAbility<AbilityMove>();
        if (move == null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            yield break;
        }

        Vector2Int start = grid.GetXY(actor.transform.position);
        Queue<GridMoveStep> path = grid.GetMovePathTo(start, target);
        if (path != null && path.Count > 0)
        {
            yield return move.MoveByPath(path);
            yield break;
        }

        yield return move.Move2PosBySpeed(grid.GetWorldPos(target), 0.9f);
    }

    private static IEnumerator MoveActorToWorld(CharacterActor actor, Vector3 position)
    {
        AbilityMove move = actor != null ? actor.GetAbility<AbilityMove>() : null;
        if (move == null)
        {
            yield break;
        }

        yield return move.Move2PosBySpeed(position, 0.9f);
    }

    private static int Distance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

}

public sealed class ExteriorActivityCoroutineHost : MonoBehaviour
{
}
