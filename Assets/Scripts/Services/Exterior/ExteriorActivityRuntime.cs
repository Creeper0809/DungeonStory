using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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

public enum ExteriorIncidentKind
{
    None = 0,
    MerchantCart = 1,
    Informant = 2,
    Thief = 3,
    InjuredReturnee = 4,
    PredatorApproach = 5,
    CargoDamage = 6
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
}

public interface IExteriorActivityRuntime : IExteriorZoneQuery
{
    DungeonExteriorActivitySaveData Capture();
    void Restore(DungeonExteriorActivitySaveData saveData, DungeonGameRestoreReport report = null);
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
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public List<ExteriorZoneSaveData> zones = new List<ExteriorZoneSaveData>();
    public List<ExteriorIncidentSaveData> incidents = new List<ExteriorIncidentSaveData>();
    public List<ExteriorIncidentRuntimeState> incidentStates =
        new List<ExteriorIncidentRuntimeState>();
    public List<ExteriorExpeditionTransitSaveData> expeditionTransits =
        new List<ExteriorExpeditionTransitSaveData>();
}

[Serializable]
public sealed class ExteriorZoneSaveData
{
    public string zoneId = string.Empty;
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

[Serializable]
public sealed class ExteriorExpeditionTransitSaveData
{
    public string expeditionId = string.Empty;
    public string characterId = string.Empty;
    public CharacterLifecycleState state;
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

public sealed class ExteriorZoneMarker : Facility
{
    private const float CleanWorkThreshold = 75f;
    private const float ReadinessCompleteThreshold = 100f;

    private ExteriorZoneType zoneType;
    private string zoneId;
    private float cleanliness = 100f;
    private float damage;
    private float patrolReadiness = 45f;
    private float receptionReadiness = 55f;
    private int waitingVisitors;
    private float firstImpressionBonus;
    private int completedWorks;
    private ExteriorIncidentKind activeIncidentKind;
    private string activeIncidentId = string.Empty;
    private string activeIncidentText = string.Empty;
    private float incidentRemainingSeconds;
    private bool registeredOnGrid;

    public string ZoneId => zoneId;
    public ExteriorZoneType ZoneType => zoneType;
    public Vector2Int GridPosition => centerPos;
    public string DisplayName => BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.objectName)
        ? BuildingData.objectName
        : zoneType.ToString();
    public float Cleanliness => cleanliness;
    public float Damage => damage;
    public float PatrolReadiness => patrolReadiness;
    public float ReceptionReadiness => receptionReadiness;
    public int WaitingVisitorCount => waitingVisitors;
    public float FirstImpressionBonus => firstImpressionBonus;
    public bool HasActiveIncident => activeIncidentKind != ExteriorIncidentKind.None;
    public ExteriorIncidentKind ActiveIncidentKind => activeIncidentKind;
    public string ActiveIncidentId => activeIncidentId;
    public string ActiveIncidentText => activeIncidentText;
    public float IncidentRemainingSeconds => incidentRemainingSeconds;
    public bool IsOutdoorRestSpot => zoneType == ExteriorZoneType.OutdoorRestSpot;
    public bool CanRunReceptionWork =>
        (zoneType == ExteriorZoneType.ReceptionPoint || zoneType == ExteriorZoneType.IncidentPoint)
        && (receptionReadiness < ReadinessCompleteThreshold
            || waitingVisitors > 0
            || HasActiveIncident);
    public bool CanRunPatrolWork =>
        (zoneType == ExteriorZoneType.GuardPost
            || zoneType == ExteriorZoneType.PatrolPoint
            || zoneType == ExteriorZoneType.IncidentPoint)
        && (patrolReadiness < ReadinessCompleteThreshold
            || HasActiveIncident);
    public bool CanRunExteriorCleanWork => cleanliness < CleanWorkThreshold;
    public bool CanRunExteriorRepairWork => damage > 0.01f;

    public void InitializeRuntime(
        Grid grid,
        Vector2Int position,
        ExteriorZoneType type,
        GridLayer markerLayer,
        IEnumerable<WorkTypeId> supportedWorkTypeIds,
        IEnumerable<BuildingAbility> abilities,
        ExteriorZoneSaveData savedState = null)
    {
        zoneType = type;
        zoneId = savedState != null && !string.IsNullOrWhiteSpace(savedState.zoneId)
            ? savedState.zoneId
            : CreateZoneId(type, position);

        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.name = zoneId;
        data.id = CreateStableNegativeId(type, position);
        data.objectName = GetDefaultDisplayName(type);
        data.width = 1;
        data.height = 1;
        data.layer = markerLayer;
        data.category = BuildingCategory.Special;
        data.type = typeof(ExteriorZoneMarker);
        data.unlocked = true;
        data.Facility = new FacilityData
        {
            roles = FacilityRole.None,
            capacity = 0,
            useDuration = 1f,
            requiredWorkers = 1,
            disabledWhenDamaged = false
        };
        data.Facility.SetSupportedWorkTypeIds(supportedWorkTypeIds);
        data.FacilityAnchors.Add(FacilityAnchorPurposeIds.Work, Vector2.zero);
        foreach (BuildingAbility ability in abilities ?? Enumerable.Empty<BuildingAbility>())
        {
            data.AbilityModules.Add(ability);
        }

        SetGrid(grid);
        Initialization(data, position);
        transform.position = grid != null
            ? grid.GetWorldPos(position)
            : new Vector3(position.x, position.y, 0f);
        ApplySaveData(savedState);

        registeredOnGrid = grid != null
            && grid.RegisterOccupant(this, data.layer, buildPoses, false);
        if (!registeredOnGrid)
        {
            Debug.LogWarning($"Exterior zone '{zoneId}' could not register at {position}.");
        }
    }

    internal override float GetLegacyWorkUrgency(FacilityWorkType workType)
    {
        return base.GetLegacyWorkUrgency(workType);
    }

    public void ApplyReceptionWork(float readinessGain, float impressionBonus)
    {
        receptionReadiness = Mathf.Clamp(receptionReadiness + Mathf.Max(0f, readinessGain), 0f, 100f);
        firstImpressionBonus = Mathf.Clamp(firstImpressionBonus + Mathf.Max(0f, impressionBonus), 0f, 25f);
        waitingVisitors = Mathf.Max(0, waitingVisitors - 1);
        completedWorks++;
        if (HasActiveIncident
            && (activeIncidentKind == ExteriorIncidentKind.MerchantCart
                || activeIncidentKind == ExteriorIncidentKind.Informant
                || activeIncidentKind == ExteriorIncidentKind.InjuredReturnee)
            && receptionReadiness >= 80f)
        {
            ClearIncident();
        }
    }

    public void ApplyPatrolWork(float readinessGain, float detectionBonus)
    {
        patrolReadiness = Mathf.Clamp(patrolReadiness + Mathf.Max(0f, readinessGain), 0f, 100f);
        completedWorks++;
        if (HasActiveIncident
            && (activeIncidentKind == ExteriorIncidentKind.Thief
                || activeIncidentKind == ExteriorIncidentKind.Informant)
            && patrolReadiness + Mathf.Max(0f, detectionBonus) * 100f >= 80f)
        {
            ClearIncident();
        }
    }

    public void RecordOutdoorRest()
    {
        completedWorks++;
    }

    public void ApplyExteriorCleanWork(float amount)
    {
        cleanliness = Mathf.Clamp(cleanliness + Mathf.Max(0f, amount), 0f, 100f);
        SetCleanliness(cleanliness);
        completedWorks++;
    }

    public void ApplyExteriorRepairWork(float amount)
    {
        damage = Mathf.Clamp(damage - Mathf.Max(0f, amount), 0f, 100f);
        SetDamaged(damage > 0.01f);
        completedWorks++;
    }

    public void ApplyExteriorWear(float cleanlinessLoss, float damageGain)
    {
        cleanliness = Mathf.Clamp(cleanliness - Mathf.Max(0f, cleanlinessLoss), 0f, 100f);
        damage = Mathf.Clamp(damage + Mathf.Max(0f, damageGain), 0f, 100f);
        patrolReadiness = Mathf.Clamp(patrolReadiness - 0.4f, 0f, 100f);
        receptionReadiness = Mathf.Clamp(receptionReadiness - 0.25f, 0f, 100f);
        SetCleanliness(cleanliness);
        SetDamaged(damage > 0.01f);
    }

    public float GetReceptionUrgency()
    {
        float readinessNeed = 100f - receptionReadiness;
        float incidentBonus = HasActiveIncident ? 25f : 0f;
        return Mathf.Clamp(15f + readinessNeed * 0.55f + waitingVisitors * 12f + incidentBonus, 0f, 95f);
    }

    public float GetPatrolUrgency()
    {
        float incidentBonus = HasActiveIncident ? 35f : 0f;
        return Mathf.Clamp(20f + (100f - patrolReadiness) * 0.55f + damage * 0.25f + incidentBonus, 0f, 95f);
    }

    public float GetCleanUrgency()
    {
        return Mathf.Clamp((CleanWorkThreshold - cleanliness) * 1.3f, 0f, 80f);
    }

    public float GetRepairUrgency()
    {
        return Mathf.Clamp(30f + damage * 0.75f, 0f, 95f);
    }

    public void SetWaitingVisitors(int count)
    {
        waitingVisitors = Mathf.Max(0, count);
    }

    public void SetIncident(ExteriorIncidentKind kind, string incidentId, string text, float durationSeconds)
    {
        activeIncidentKind = kind;
        activeIncidentId = incidentId ?? string.Empty;
        activeIncidentText = text ?? string.Empty;
        incidentRemainingSeconds = Mathf.Max(0f, durationSeconds);
    }

    public void TickIncident(float deltaSeconds)
    {
        if (!HasActiveIncident)
        {
            return;
        }

        incidentRemainingSeconds -= Mathf.Max(0f, deltaSeconds);
        if (incidentRemainingSeconds <= 0f)
        {
            ClearIncident();
        }
    }

    public void ClearIncident()
    {
        activeIncidentKind = ExteriorIncidentKind.None;
        activeIncidentId = string.Empty;
        activeIncidentText = string.Empty;
        incidentRemainingSeconds = 0f;
    }

    public ExteriorZoneSaveData CreateSaveData()
    {
        return new ExteriorZoneSaveData
        {
            zoneId = zoneId,
            zoneType = zoneType,
            gridX = centerPos.x,
            gridY = centerPos.y,
            cleanliness = cleanliness,
            damage = damage,
            patrolReadiness = patrolReadiness,
            receptionReadiness = receptionReadiness,
            waitingVisitors = waitingVisitors,
            firstImpressionBonus = firstImpressionBonus,
            completedWorks = completedWorks
        };
    }

    public ExteriorIncidentSaveData CreateIncidentSaveData()
    {
        if (!HasActiveIncident)
        {
            return null;
        }

        return new ExteriorIncidentSaveData
        {
            incidentId = activeIncidentId,
            kind = activeIncidentKind,
            zoneId = zoneId,
            text = activeIncidentText,
            remainingSeconds = incidentRemainingSeconds
        };
    }

    public void ApplySaveData(ExteriorZoneSaveData saveData)
    {
        if (saveData == null)
        {
            SetCleanliness(cleanliness);
            SetDamaged(damage > 0.01f);
            return;
        }

        cleanliness = Mathf.Clamp(saveData.cleanliness, 0f, 100f);
        damage = Mathf.Clamp(saveData.damage, 0f, 100f);
        patrolReadiness = Mathf.Clamp(saveData.patrolReadiness, 0f, 100f);
        receptionReadiness = Mathf.Clamp(saveData.receptionReadiness, 0f, 100f);
        waitingVisitors = Mathf.Max(0, saveData.waitingVisitors);
        firstImpressionBonus = Mathf.Clamp(saveData.firstImpressionBonus, 0f, 25f);
        completedWorks = Mathf.Max(0, saveData.completedWorks);
        SetCleanliness(cleanliness);
        SetDamaged(damage > 0.01f);
    }

    protected override void OnDestroy()
    {
        if (registeredOnGrid && Grid != null && buildPoses != null && buildPoses.Count > 0)
        {
            Grid.RemoveOccupant(this, BuildingData.Placement.Layer, buildPoses, false);
            registeredOnGrid = false;
        }

        base.OnDestroy();
    }

    private static string CreateZoneId(ExteriorZoneType type, Vector2Int position)
    {
        return $"exterior:{type}:{position.x}:{position.y}";
    }

    private static int CreateStableNegativeId(ExteriorZoneType type, Vector2Int position)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (int)type;
            hash = hash * 31 + position.x;
            hash = hash * 31 + position.y;
            if (hash == int.MinValue)
            {
                hash = int.MaxValue;
            }

            return -Mathf.Abs(hash);
        }
    }

    private static string GetDefaultDisplayName(ExteriorZoneType type)
    {
        return type switch
        {
            ExteriorZoneType.Entrance => "입구",
            ExteriorZoneType.DropZone => "하차장",
            ExteriorZoneType.ReceptionPoint => "응대 지점",
            ExteriorZoneType.GuardPost => "경비 초소",
            ExteriorZoneType.PatrolPoint => "순찰 지점",
            ExteriorZoneType.OutdoorRestSpot => "외부 휴식터",
            ExteriorZoneType.ExpeditionStaging => "출정 집결지",
            ExteriorZoneType.IncidentPoint => "외부 사건 지점",
            _ => "외부 구역"
        };
    }
}

public sealed class ExteriorActivityRuntime :
    IExteriorActivityRuntime,
    IExteriorPatrolRuntime,
    IExteriorIncidentRuntime,
    IExpeditionDepartureService,
    IExpeditionReturnService,
    IStartable,
    ITickable,
    IDisposable
{
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

    private readonly List<ExteriorZoneMarker> zones = new List<ExteriorZoneMarker>();
    private readonly IReadOnlyList<ExteriorZoneMarker> zonesView;
    private readonly List<ExteriorIncidentRuntimeState> incidentStates =
        new List<ExteriorIncidentRuntimeState>();
    private readonly IReadOnlyList<ExteriorIncidentRuntimeState> incidentStatesView;
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldDropZoneQuery dropZoneQuery;
    private readonly WorldSimulationSceneReferences sceneReferences;
    private readonly IObjectResolver objectResolver;
    private readonly ICharacterBodyHealthRuntime bodyHealthRuntime;
    private readonly ICharacterMedicalRuntime medicalRuntime;
    private readonly ExteriorIncidentHandlerRegistry incidentHandlers;
    private readonly IGameClock gameClock;
    private readonly ISurvivalEnvironmentQuery survivalEnvironment;
    private readonly IRandomStream incidentRandom;
    private ExteriorActivityCoroutineHost coroutineHost;
    private float nextConditionTick;
    private float nextIncidentCheck;
    private int incidentSequence;

    public ExteriorActivityRuntime(
        IGridSystemProvider gridSystemProvider,
        IWorldDropZoneQuery dropZoneQuery,
        WorldSimulationSceneReferences sceneReferences,
        IObjectResolver objectResolver,
        ICharacterBodyHealthRuntime bodyHealthRuntime,
        ICharacterMedicalRuntime medicalRuntime,
        ExteriorIncidentHandlerRegistry incidentHandlers,
        IGameClock gameClock,
        ISurvivalEnvironmentQuery survivalEnvironment,
        IRandomStreamProvider randomStreamProvider)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.dropZoneQuery = dropZoneQuery
            ?? throw new ArgumentNullException(nameof(dropZoneQuery));
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
        this.bodyHealthRuntime = bodyHealthRuntime
            ?? throw new ArgumentNullException(nameof(bodyHealthRuntime));
        this.medicalRuntime = medicalRuntime
            ?? throw new ArgumentNullException(nameof(medicalRuntime));
        this.incidentHandlers = incidentHandlers
            ?? throw new ArgumentNullException(nameof(incidentHandlers));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.survivalEnvironment = survivalEnvironment
            ?? throw new ArgumentNullException(nameof(survivalEnvironment));
        incidentRandom = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("exterior-incidents");
        zonesView = zones.AsReadOnly();
        incidentStatesView = incidentStates.AsReadOnly();
    }

    public IReadOnlyList<ExteriorZoneMarker> Zones => zonesView;
    public IReadOnlyList<ExteriorIncidentSaveData> ActiveIncidents => zones
        .Select(zone => zone?.CreateIncidentSaveData())
        .Where(incident => incident != null)
        .ToArray();
    public IReadOnlyList<ExteriorIncidentRuntimeState> IncidentStates =>
        incidentStatesView;
    public float AveragePatrolReadiness => zones
        .Where(zone => zone != null
            && (zone.ZoneType == ExteriorZoneType.GuardPost
                || zone.ZoneType == ExteriorZoneType.PatrolPoint))
        .Select(zone => zone.PatrolReadiness)
        .DefaultIfEmpty(0f)
        .Average();

    public void Start()
    {
        EnsureRuntimeObjects();
        EnsureDefaultZones();
        nextConditionTick = gameClock.Time + ConditionTickSeconds;
        nextIncidentCheck = gameClock.Time + IncidentCheckSeconds;
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

        float now = gameClock.Time;
        if (now >= nextConditionTick)
        {
            float elapsed = Mathf.Max(ConditionTickSeconds, now - (nextConditionTick - ConditionTickSeconds));
            TickExteriorConditions(elapsed);
            nextConditionTick = now + ConditionTickSeconds;
        }

        TickIncidentStates(gameClock.DeltaTime);

        foreach (ExteriorZoneMarker zone in zones)
        {
            zone?.TickIncident(gameClock.DeltaTime);
        }

        if (now >= nextIncidentCheck)
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
        incidentStates.Clear();
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
            activeZones.Count(zone => zone.HasActiveIncident),
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
            zones = zones
                .Where(zone => zone != null)
                .Select(zone => zone.CreateSaveData())
                .ToList(),
            incidents = zones
                .Select(zone => zone?.CreateIncidentSaveData())
                .Where(incident => incident != null)
                .ToList(),
            incidentStates = incidentStates
                .Select(state => state?.Clone())
                .Where(state => state != null)
                .ToList(),
            expeditionTransits = new List<ExteriorExpeditionTransitSaveData>()
        };
    }

    public void Restore(DungeonExteriorActivitySaveData saveData, DungeonGameRestoreReport report = null)
    {
        EnsureRuntimeObjects();
        EnsureDefaultZones();
        if (saveData == null)
        {
            return;
        }

        if (saveData.version != DungeonExteriorActivitySaveData.CurrentVersion)
        {
            report?.AddWarning(
                $"Exterior activity save version {saveData.version} is not supported; regenerated exterior runtime state.");
            return;
        }

        foreach (ExteriorZoneSaveData zoneData in saveData.zones ?? new List<ExteriorZoneSaveData>())
        {
            ExteriorZoneMarker marker = zones.FirstOrDefault(zone =>
                zone != null
                && (string.Equals(zone.ZoneId, zoneData.zoneId, StringComparison.Ordinal)
                    || (zone.ZoneType == zoneData.zoneType
                        && zone.centerPos == new Vector2Int(zoneData.gridX, zoneData.gridY))));
            if (marker == null)
            {
                string warning = $"Exterior zone reservation target vanished: {zoneData.zoneId}";
                report?.AddWarning(warning);
                Debug.LogWarning(warning);
                continue;
            }

            marker.ApplySaveData(zoneData);
        }

        foreach (ExteriorZoneMarker marker in zones)
        {
            marker?.ClearIncident();
        }

        incidentStates.Clear();
        foreach (ExteriorIncidentRuntimeState incident in
                 saveData.incidentStates
                 ?? new List<ExteriorIncidentRuntimeState>())
        {
            if (incident == null
                || incident.IsTerminal
                || !incidentHandlers.TryGet(incident.kind, out IExteriorIncidentHandler handler))
            {
                continue;
            }

            ExteriorZoneMarker marker = zones.FirstOrDefault(zone =>
                zone != null
                && string.Equals(zone.ZoneId, incident.zoneId, StringComparison.Ordinal));
            if (marker == null)
            {
                string warning =
                    $"Exterior incident target vanished: {incident.incidentId}";
                report?.AddWarning(warning);
                Debug.LogWarning(warning);
                continue;
            }

            ExteriorIncidentRuntimeState restored = incident.Clone();
            restored.actorIds ??= new List<string>();
            restored.itemStackIds ??= new List<string>();
            incidentStates.Add(restored);
            marker.SetIncident(
                restored.kind,
                restored.incidentId,
                restored.text,
                restored.remainingSeconds);
            handler.Restore(restored, marker);
        }

        if (incidentStates.Count > 0)
        {
            return;
        }

        foreach (ExteriorIncidentSaveData incident in saveData.incidents ?? new List<ExteriorIncidentSaveData>())
        {
            ExteriorZoneMarker marker = zones.FirstOrDefault(zone =>
                zone != null && string.Equals(zone.ZoneId, incident.zoneId, StringComparison.Ordinal));
            if (marker == null)
            {
                string warning = $"Exterior incident target vanished: {incident.incidentId}";
                report?.AddWarning(warning);
                Debug.LogWarning(warning);
                continue;
            }

            marker.SetIncident(
                incident.kind,
                incident.incidentId,
                incident.text,
                incident.remainingSeconds);
        }
    }

    public bool TryStartIncident(ExteriorIncidentKind kind, string text = null)
    {
        if (!incidentHandlers.TryGet(kind, out IExteriorIncidentHandler handler))
        {
            return false;
        }

        ExteriorZoneMarker marker = SelectIncidentZone(kind);
        if (marker == null || marker.HasActiveIncident)
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

        incidentStates.Add(state);
        marker.SetIncident(
            state.kind,
            state.incidentId,
            state.text,
            state.remainingSeconds);
        return true;
    }

    public bool TryExecutePrimaryAction(string incidentId, out string message)
    {
        ExteriorIncidentRuntimeState state = incidentStates.FirstOrDefault(candidate =>
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

        return handler.TryExecutePrimaryAction(state, zone, out message);
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
        TryPlaceZone(grid, ExteriorZoneType.DropZone, FacilityWorkType.Clean | FacilityWorkType.Repair,
            CandidateCells(grid, entrance, GridCellAreaType.DropZone, hasDropoff ? dropoff : (Vector2Int?)null),
            new BuildingExteriorMaintenanceAbility { cleanlinessGain = 40f, damageReduction = 35f });
        TryPlaceZone(grid, ExteriorZoneType.ReceptionPoint, FacilityWorkType.Reception | FacilityWorkType.Clean,
            CandidateCells(grid, entrance, GridCellAreaType.Entrance, null)
                .Concat(CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null)),
            new BuildingReceptionAbility(),
            new BuildingExteriorMaintenanceAbility());
        TryPlaceZone(grid, ExteriorZoneType.ExpeditionStaging, FacilityWorkType.None,
            CandidateCells(grid, entrance, GridCellAreaType.Entrance, null)
                .Concat(CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null)));
        TryPlaceZone(grid, ExteriorZoneType.GuardPost, FacilityWorkType.Guard | FacilityWorkType.Repair,
            CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null),
            new BuildingPatrolPostAbility(),
            new BuildingExteriorMaintenanceAbility());
        TryPlaceZone(grid, ExteriorZoneType.PatrolPoint, FacilityWorkType.Guard,
            CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null),
            new BuildingPatrolPostAbility { patrolReadinessGain = 25f });
        TryPlaceZone(grid, ExteriorZoneType.OutdoorRestSpot, FacilityWorkType.Rest | FacilityWorkType.Clean,
            CandidateCells(grid, entrance, GridCellAreaType.DropZone, null)
                .Concat(CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null)),
            new BuildingOutdoorRestAbility(),
            new BuildingExteriorMaintenanceAbility());
        TryPlaceZone(grid, ExteriorZoneType.IncidentPoint, FacilityWorkType.Reception | FacilityWorkType.Guard,
            CandidateCells(grid, entrance, GridCellAreaType.ExteriorPath, null)
                .Concat(CandidateCells(grid, entrance, GridCellAreaType.DropZone, null)),
            new BuildingReceptionAbility { readinessGain = 45f },
            new BuildingPatrolPostAbility { patrolReadinessGain = 45f });
    }

    private bool TryPlaceZone(
        Grid grid,
        ExteriorZoneType zoneType,
        FacilityWorkType supportedWorkTypes,
        IEnumerable<Vector2Int> candidates,
        params BuildingAbility[] abilities)
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
                markerLayer,
                WorkTypeCatalog.Enumerate(supportedWorkTypes).Select(definition => definition.WorkTypeId),
                abilities);
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
        float elapsed = Mathf.Max(0f, deltaTime);
        foreach (ExteriorIncidentRuntimeState state in incidentStates.ToArray())
        {
            if (state == null || state.IsTerminal)
            {
                continue;
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
                state.stage = ExteriorIncidentStage.Failed;
                continue;
            }

            state.remainingSeconds = Mathf.Max(
                0f,
                state.remainingSeconds - elapsed);
            handler.Tick(state, zone, elapsed);
            if (state.IsTerminal)
            {
                zone.ClearIncident();
            }
        }

        const int maximumIncidentHistory = 32;
        while (incidentStates.Count > maximumIncidentHistory)
        {
            int index = incidentStates.FindIndex(state =>
                state == null || state.IsTerminal);
            if (index < 0)
            {
                break;
            }

            incidentStates.RemoveAt(index);
        }
    }

    private bool TryStartRandomIncident()
    {
        if (zones.Any(zone => zone != null && zone.HasActiveIncident))
        {
            return false;
        }

        SurvivalEnvironmentSnapshot environment =
            survivalEnvironment.GetEnvironmentSnapshot();
        float patrolReadiness = AveragePatrolReadiness;
        float incidentChance = Mathf.Clamp(
            0.18f
            + (environment.ExteriorNightDanger * 0.004f)
            + (environment.WeatherPressure01 * 0.12f)
            - (patrolReadiness * 0.0025f),
            0.08f,
            0.72f);
        if (!incidentRandom.Chance(incidentChance))
        {
            return false;
        }

        IExteriorIncidentHandler[] candidates = incidentHandlers.All.ToArray();
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

        float roll = incidentRandom.NextFloat() * totalWeight;
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
        float danger = environment.ExteriorNightDanger / 100f;
        float patrol = Mathf.Clamp01(patrolReadiness / 100f);
        return kind switch
        {
            ExteriorIncidentKind.Thief =>
                Mathf.Max(0.1f, 0.35f + (danger * 2.8f) - (patrol * 1.35f)),
            ExteriorIncidentKind.PredatorApproach =>
                Mathf.Max(0.05f, 0.15f + (danger * 2.5f) - (patrol * 1.1f)),
            ExteriorIncidentKind.CargoDamage =>
                Mathf.Max(
                    0.05f,
                    0.1f
                    + (danger * 1.35f)
                    + (environment.WeatherPressure01 * 1.8f)
                    - (patrol * 0.55f)),
            ExteriorIncidentKind.MerchantCart => 0.85f,
            ExteriorIncidentKind.Informant => 0.7f,
            ExteriorIncidentKind.InjuredReturnee => 0.55f,
            _ => 0f
        };
    }

    private ExteriorZoneMarker SelectIncidentZone(ExteriorIncidentKind kind)
    {
        if (kind is ExteriorIncidentKind.Thief
            or ExteriorIncidentKind.CargoDamage)
        {
            return zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.DropZone
                    && !zone.HasActiveIncident)
                ?? zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.IncidentPoint
                    && !zone.HasActiveIncident);
        }

        if (kind == ExteriorIncidentKind.InjuredReturnee)
        {
            return zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.Entrance
                    && !zone.HasActiveIncident)
                ?? zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.DropZone
                    && !zone.HasActiveIncident);
        }

        if (kind == ExteriorIncidentKind.PredatorApproach)
        {
            return zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.Entrance
                    && !zone.HasActiveIncident)
                ?? zones.FirstOrDefault(zone => zone != null
                    && zone.ZoneType == ExteriorZoneType.IncidentPoint
                    && !zone.HasActiveIncident);
        }

        return zones.FirstOrDefault(zone => zone != null
                && zone.ZoneType == ExteriorZoneType.IncidentPoint
                && !zone.HasActiveIncident)
            ?? zones.FirstOrDefault(zone => zone != null
                && zone.ZoneType == ExteriorZoneType.ReceptionPoint
                && !zone.HasActiveIncident);
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

        if (bodyHealthRuntime.GetSnapshot(actor).Downed)
        {
            PlaceDownedReturneeOnGrid(actor, entryPoint);
            medicalRuntime.NotifyCharacterDowned(actor);
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
