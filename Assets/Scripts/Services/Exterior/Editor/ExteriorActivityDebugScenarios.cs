#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class ExteriorActivityDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Exterior/Run Exterior Activity Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Exterior activity scenarios failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/Exterior/Run Exterior Activity PlayMode Snapshot")]
    public static void RunPlayModeSnapshotFromMenu()
    {
        bool success = RunPlayModeRuntimeSnapshot(true);
        if (!success)
        {
            Debug.LogError("Exterior activity PlayMode snapshot failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("save V10 contains exterior snapshot", VerifySaveV10ContainsExteriorSnapshot, errors);
        RunScenario("reception work type is registered", VerifyReceptionWorkTypeIsRegistered, errors);
        RunScenario("exterior runtime state is not a ScriptableObject", VerifyRuntimeStateIsNotScriptableObject, errors);
        RunScenario("exterior ability contracts expose expected work types", VerifyExteriorAbilityContracts, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Exterior activity scenarios passed.");
        }

        return true;
    }

    public static bool RunPlayModeRuntimeSnapshot(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("playmode runtime services and zones", VerifyPlayModeRuntimeServicesAndZones, errors);
        RunScenario("playmode reception work candidate", VerifyPlayModeReceptionWorkCandidate, errors);
        RunScenario("playmode incident and save capture", VerifyPlayModeIncidentAndSaveCapture, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Exterior activity PlayMode snapshot passed.");
        }

        return true;
    }

    private static bool VerifySaveV10ContainsExteriorSnapshot()
    {
        DungeonGameSaveData save = new DungeonGameSaveData();
        DungeonSaveSectionPayload.Write(
            save,
            ExteriorActivitySaveSection.Id,
            DungeonExteriorActivitySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            new DungeonExteriorActivitySaveData());
        DungeonExteriorActivitySaveData exterior =
            DungeonSaveSectionPayload.ReadOrNew<DungeonExteriorActivitySaveData>(
                save,
                ExteriorActivitySaveSection.Id);
        return DungeonGameSaveData.CurrentVersion == 15
            && save.version == DungeonGameSaveData.CurrentVersion
            && exterior.version == DungeonExteriorActivitySaveData.CurrentVersion;
    }

    private static bool VerifyReceptionWorkTypeIsRegistered()
    {
        bool resolved = WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Reception, out WorkTypeDefinition task);
        WorkPriorityProfile priorities = WorkPriorityProfile.CreateDefault();
        return resolved
            && task != null
            && task.WorkTypeId == BuiltInWorkTypeIds.Reception
            && priorities.GetPriority(BuiltInWorkTypeIds.Reception) == WorkPriorityLevel.Priority2;
    }

    private static bool VerifyRuntimeStateIsNotScriptableObject()
    {
        return !typeof(ExteriorActivityRuntime).IsSubclassOf(typeof(ScriptableObject))
            && !typeof(ExteriorZoneMarker).IsSubclassOf(typeof(ScriptableObject))
            && typeof(MonoBehaviour).IsAssignableFrom(typeof(ExteriorZoneMarker));
    }

    private static bool VerifyExteriorAbilityContracts()
    {
        IBuildingExteriorWorkRuntimeAbility reception = new BuildingReceptionAbility();
        IBuildingExteriorWorkRuntimeAbility patrol = new BuildingPatrolPostAbility();
        IBuildingExteriorWorkRuntimeAbility rest = new BuildingOutdoorRestAbility();
        IBuildingExteriorWorkRuntimeAbility maintenance = new BuildingExteriorMaintenanceAbility();
        return reception.SupportsExteriorWork(BuiltInWorkTypeIds.Reception)
            && patrol.SupportsExteriorWork(BuiltInWorkTypeIds.Guard)
            && rest.SupportsExteriorWork(BuiltInWorkTypeIds.Rest)
            && maintenance.SupportsExteriorWork(BuiltInWorkTypeIds.Clean)
            && maintenance.SupportsExteriorWork(BuiltInWorkTypeIds.Repair)
            && !maintenance.SupportsExteriorWork(BuiltInWorkTypeIds.Reception);
    }

    private static bool VerifyPlayModeRuntimeServicesAndZones()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        IExteriorZoneQuery query = scope.Container.Resolve<IExteriorZoneQuery>();
        ExteriorActivityOverviewSnapshot overview = query.GetOverview();
        HashSet<ExteriorZoneType> types = query.Zones.Select(zone => zone.ZoneType).ToHashSet();
        return overview.ZoneCount >= 7
            && types.Contains(ExteriorZoneType.DropZone)
            && types.Contains(ExteriorZoneType.ReceptionPoint)
            && types.Contains(ExteriorZoneType.GuardPost)
            && types.Contains(ExteriorZoneType.PatrolPoint)
            && types.Contains(ExteriorZoneType.OutdoorRestSpot)
            && types.Contains(ExteriorZoneType.ExpeditionStaging)
            && types.Contains(ExteriorZoneType.IncidentPoint);
    }

    private static bool VerifyPlayModeIncidentAndSaveCapture()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        IExteriorIncidentRuntime incidentRuntime = scope.Container.Resolve<IExteriorIncidentRuntime>();
        IExteriorActivityRuntime exteriorRuntime = scope.Container.Resolve<IExteriorActivityRuntime>();
        IDungeonGameSaveService saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        bool started = incidentRuntime.TryStartIncident(
            ExteriorIncidentKind.Thief,
            "수상한 그림자가 하차장 근처를 맴돕니다.");
        DungeonExteriorActivitySaveData exterior = exteriorRuntime.Capture();
        DungeonGameSaveData save = saveService.Capture();
        DungeonExteriorActivitySaveData savedExterior =
            DungeonSaveSectionPayload.ReadOrNew<DungeonExteriorActivitySaveData>(
                save,
                ExteriorActivitySaveSection.Id);
        return started
            && exterior.zones.Count >= 7
            && exterior.incidents.Count >= 1
            && save.version == DungeonGameSaveData.CurrentVersion
            && savedExterior.zones.Count >= 7
            && savedExterior.incidents.Count >= 1;
    }

    private static bool VerifyPlayModeReceptionWorkCandidate()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        IExteriorZoneQuery zoneQuery = scope.Container.Resolve<IExteriorZoneQuery>();
        ExteriorZoneMarker reception = zoneQuery.GetZones(ExteriorZoneType.ReceptionPoint).FirstOrDefault();
        if (reception == null || !reception.CanRunReceptionWork)
        {
            Debug.LogWarning($"Reception marker unavailable. marker={reception != null}, canRun={reception != null && reception.CanRunReceptionWork}");
            return false;
        }

        AbilityWork[] workers = UnityEngine.Object.FindObjectsByType<AbilityWork>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(work => work != null
                && work.WorkerActor != null)
            .ToArray();
        GameObject temporaryWorkerObject = null;
        if (workers.Length == 0
            && TryCreateTemporaryReceptionWorker(zoneQuery, reception, out AbilityWork temporaryWorker, out temporaryWorkerObject))
        {
            workers = new[] { temporaryWorker };
        }

        Debug.Log($"Reception candidate workers={workers.Length}");
        try
        {
            foreach (AbilityWork work in workers)
            {
                work.EnsureWorkReferences();
                Grid grid = work.CachedGrid;
                if (grid == null)
                {
                    Debug.LogWarning($"Reception candidate skipped actor={work.WorkerActor.name} reason=no-grid");
                    continue;
                }

                GridPathSearchResult search = grid.SearchPath(work.WorkerActor.GetNowXY());
                work.SetWorkPriority(BuiltInWorkTypeIds.Reception, WorkPriorityLevel.Priority1, search);
                bool canStart = work.CanStartWorkAction(BuiltInWorkTypeIds.Reception, search);
                bool found = work.TryGetBestWorkCandidate(
                    BuiltInWorkTypeIds.Reception,
                    search,
                    out WorkTargetCandidate candidate);
                bool positionReachable = search.GetReachablePositions().Contains(reception.GridPosition);
                bool buildingReachable = search.GetAllReachableBuilding().Contains(reception);
                Debug.Log(
                    $"Reception candidate probe actor={work.WorkerActor.name} "
                    + $"pos={work.WorkerActor.GetNowXY()} "
                    + $"hasExteriorQuery={work.HasExteriorZoneQuery} "
                    + $"receptionPos={reception.GridPosition} "
                    + $"positionReachable={positionReachable} "
                    + $"buildingReachable={buildingReachable} "
                    + $"canStart={canStart} found={found} "
                    + $"candidateValid={candidate.IsValid} "
                    + $"candidateType={candidate.WorkTypeId} "
                    + $"candidateBuilding={(candidate.Building != null ? candidate.Building.name : "null")} "
                    + $"sameReception={candidate.Building == reception} "
                    + $"lastRejected={work.LastRejectedWorkCandidate.FailureKind}");
                if (canStart
                    && found
                    && candidate.IsValid
                    && candidate.WorkTypeId == BuiltInWorkTypeIds.Reception
                    && candidate.Building == reception)
                {
                    return true;
                }
            }
        }
        finally
        {
            if (temporaryWorkerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryWorkerObject);
            }
        }

        return false;
    }

    private static bool TryCreateTemporaryReceptionWorker(
        IExteriorZoneQuery zoneQuery,
        ExteriorZoneMarker reception,
        out AbilityWork work,
        out GameObject workerObject)
    {
        work = null;
        workerObject = null;
        if (zoneQuery == null || reception == null)
        {
            return false;
        }

        GridSystemManager manager = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = manager != null ? manager.grid : null;
        if (grid == null || !grid.TryFindNearestWalkablePosition(reception.GridPosition, out Vector2Int spawnPosition))
        {
            return false;
        }

        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Owners/Owner_Slime.asset");
        if (data == null)
        {
            return false;
        }

        workerObject = new GameObject("ExteriorReceptionCandidateWorker");
        workerObject.AddComponent<SpriteRenderer>();
        workerObject.AddComponent<CharacterActor>();
        workerObject.AddComponent<AbilityMove>();
        work = workerObject.AddComponent<AbilityWork>();
        workerObject.AddComponent<AIBrain>();
        workerObject.transform.position = grid.GetWorldPos(spawnPosition);

        CharacterAiEditorTestDependencies.Inject(workerObject);
        CharacterActor character = workerObject.GetComponent<CharacterActor>();
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(character, null);
        character.RefreshAbilityCache();
        character.Initialization(data);
        character.SetLifecycleState(CharacterLifecycleState.Active);
        work = workerObject.GetComponent<AbilityWork>();
        typeof(AbilityWork)
            .GetField("exteriorZoneQuery", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(work, zoneQuery);
        work.EnsureWorkReferences();
        return work != null;
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario())
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }
}
#endif
