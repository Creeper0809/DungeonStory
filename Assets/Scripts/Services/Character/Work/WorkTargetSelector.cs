using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;

public sealed class WorkTargetSelector
{
    private static readonly ProfilerMarker CandidateSelectionMarker =
        new ProfilerMarker("CharacterAi.WorkTargetSelection");
    private static readonly ProfilerMarker CandidateUrgencyMarker =
        new ProfilerMarker("CharacterAi.WorkTarget.Urgency");
    private static readonly ProfilerMarker CandidateActorStatsMarker =
        new ProfilerMarker("CharacterAi.WorkTarget.ActorStats");
    private static readonly ProfilerMarker CandidateSpatialMarker =
        new ProfilerMarker("CharacterAi.WorkTarget.Spatial");
    private static readonly ProfilerMarker CandidateDistanceMarker =
        new ProfilerMarker("CharacterAi.WorkTarget.Spatial.Distance");
    private static readonly ProfilerMarker CandidateRoomMarker =
        new ProfilerMarker("CharacterAi.WorkTarget.Spatial.Room");
    private static readonly ProfilerMarker CandidateFacilityStateMarker =
        new ProfilerMarker("CharacterAi.WorkTarget.Spatial.FacilityState");
    private static readonly ProfilerMarker CandidateRoleMarker =
        new ProfilerMarker("CharacterAi.WorkTarget.Spatial.Role");
    private static readonly ProfilerMarker CandidateWorldSignalMarker =
        new ProfilerMarker("CharacterAi.WorkTarget.WorldSignal");
    private static readonly ProfilerMarker CandidateMemoryMarker =
        new ProfilerMarker("CharacterAi.WorkTarget.Memory");
    private const float WorkUtilityScoreDivisor = 460f;
    private const double CandidateScanFallbackSliceMilliseconds = 0.2;
    private const int CandidateScanMinimumBatch = 1;
    private const int CompletedCandidateRefreshFrames = 180;

    private readonly struct CandidateCacheEntry
    {
        public CandidateCacheEntry(
            bool found,
            WorkTargetCandidate candidate,
            WorkTargetCandidate rejected)
        {
            Found = found;
            Candidate = candidate;
            Rejected = rejected;
        }

        public bool Found { get; }
        public WorkTargetCandidate Candidate { get; }
        public WorkTargetCandidate Rejected { get; }
    }

    private readonly struct ActorWorkScore
    {
        public ActorWorkScore(float preference, float speed)
        {
            Preference = preference;
            Speed = speed;
        }

        public float Preference { get; }
        public float Speed { get; }
    }

    private sealed class IncrementalCandidateScan
    {
        public IReadOnlyList<BuildableObject> Source;
        public int CandidateIndexVersion;
        public int GridVersion;
        public int BuildingVersion;
        public int WorkOrderVersion;
        public int StartOffset;
        public int EvaluatedCount;
        public int CompletedFrame = -1;
        public int LastAdvancedFrame = -1;
        public WorkTargetCandidate Best;
        public WorkTargetCandidate MostUrgent;
        public WorkTargetCandidate Rejected;
        public bool Complete;
    }

    private readonly AbilityWork work;
    private readonly IWorkPolicyRegistry workPolicyRegistry;
    private readonly ICaptiveLaborQuery captiveLaborQuery;
    private readonly Dictionary<FacilityWorkType, CandidateCacheEntry> candidateCache =
        new Dictionary<FacilityWorkType, CandidateCacheEntry>();
    private readonly Dictionary<FacilityWorkType, IncrementalCandidateScan> incrementalScans =
        new Dictionary<FacilityWorkType, IncrementalCandidateScan>();
    private readonly Dictionary<WorkTypeId, ActorWorkScore> actorWorkScoreCache =
        new Dictionary<WorkTypeId, ActorWorkScore>();
    private CharacterActor cachedContextActor;
    private CharacterAiDecisionContext cachedDecisionContext;
    private int cachedDecisionContextFrame = -1;
    private int actorWorkScoreCacheFrame = -1;
    private int candidateCacheFrame = -1;
    private int candidateCacheGridVersion = -1;
    private int candidateCacheBuildingVersion = -1;
    private int candidateCacheFacilityVersion = -1;
    private int candidateCacheWorkOrderVersion = -1;
    private BuildableObject lastRecordedBreakdownBuilding;
    private WorkTypeId lastRecordedBreakdownWorkTypeId;
    public WorkTargetCandidate LastRejectedCandidate { get; private set; }

    public WorkTargetSelector(
        AbilityWork work,
        IWorkPolicyRegistry workPolicyRegistry = null,
        ICaptiveLaborQuery captiveLaborQuery = null)
    {
        this.work = work;
        this.workPolicyRegistry = workPolicyRegistry;
        this.captiveLaborQuery = captiveLaborQuery;
    }

    public void SeedDecisionContext(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        if (actor == null)
        {
            return;
        }

        cachedContextActor = actor;
        cachedDecisionContext = context;
        cachedDecisionContextFrame = work.GameClock.FrameCount;
    }

    public bool TryAssignAnyWork(GridPathSearchResult searchResult = null)
    {
        return TryAssignWorkWithLegacyType(searchResult, FacilityWorkType.None);
    }

    public bool TryAssignWork(
        GridPathSearchResult searchResult,
        WorkTypeId requestedWorkTypeId)
    {
        return WorkTypeCatalog.TryGet(requestedWorkTypeId, out WorkTypeDefinition definition)
            && TryAssignWorkWithLegacyType(searchResult, definition.Type);
    }

    private bool TryAssignWorkWithLegacyType(
        GridPathSearchResult searchResult,
        FacilityWorkType requestedWorkType)
    {
        if (work.HasPrioritySuppressTarget && CanUseSuppressFor(requestedWorkType))
        {
            work.AssignWork(null, FacilityWorkType.Guard);
            return true;
        }

        bool canStartWork = work.CanStartWorkAction();
        if (!canStartWork && !HasUrgentAvailableWorkWithLegacyType(searchResult, requestedWorkType))
        {
            work.AssignWork(null, FacilityWorkType.None);
            work.WorkerActor?.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Blocked,
                work.IsOffDuty ? "작업 보류: 비번" : "작업 보류: 피로/기분 보호",
                reasonCode: work.IsOffDuty ? "off-duty" : "wellbeing-protection",
                sentiment: -0.2f,
                bubbleEligible: true));
            return false;
        }

        if (!canStartWork)
        {
            work.SetDutyState(AbilityWork.DutyState.OnDuty);
            work.WorkerActor?.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Responded,
                "비번 중 긴급 작업 합류",
                reasonCode: "urgent-work"));
        }

        if (work.PriorityWorkTarget != null)
        {
            bool forced = work.PriorityWorkType != FacilityWorkType.None;
            bool canUsePriorityForRequest = requestedWorkType == FacilityWorkType.None
                || work.PriorityWorkType == requestedWorkType;
            if (canUsePriorityForRequest
                && TryEvaluateWorkTarget(
                    work.PriorityWorkTarget,
                    searchResult,
                    work.PriorityWorkType,
                    forced,
                    out WorkTargetCandidate priorityCandidate))
            {
                work.AssignWork(work.PriorityWorkTarget, priorityCandidate.WorkType);
                return true;
            }

            work.WorkerActor?.AddActivity(CharacterActivityEvent.Work(
                work.PriorityWorkType,
                CharacterActivityOutcomes.Cancelled,
                "우선 작업 취소: 대상 사용 불가",
                work.PriorityWorkTarget,
                reasonCode: "target-unavailable",
                bubbleEligible: true));
            work.ClearPriorityWorkTarget();
        }

        if (work.assignedShop != null
            && requestedWorkType != FacilityWorkType.None
            && TryEvaluateWorkTarget(
                work.assignedShop,
                searchResult,
                requestedWorkType,
                false,
                out WorkTargetCandidate assignedCandidate))
        {
            work.AssignWork(work.assignedShop, assignedCandidate.WorkType);
            return true;
        }

        if (work.assignedShop != null && CanUseAsWorkTargetWithLegacyType(work.assignedShop, requestedWorkType))
        {
            return true;
        }

        TryGetBestCandidateWithLegacyType(requestedWorkType, searchResult, out WorkTargetCandidate best);
        work.AssignWork(best.Building, best.WorkType);
        return work.assignedShop != null;
    }

    public bool CanUseAsWorkTarget(BuildableObject building)
    {
        return CanUseAsWorkTargetWithLegacyType(building, FacilityWorkType.None);
    }

    private bool CanUseAsWorkTargetWithLegacyType(BuildableObject building, FacilityWorkType requestedWorkType)
    {
        return TryEvaluateWorkTarget(building, null, requestedWorkType, false, out _);
    }

    private bool HasUrgentAvailableWorkWithLegacyType(
        GridPathSearchResult searchResult,
        FacilityWorkType requestedWorkType)
    {
        if (searchResult == null && work.WorkerActor?.Brain != null)
        {
            AdvanceIncrementalCandidateScan(
                requestedWorkType,
                out _,
                out WorkTargetCandidate urgentCandidate,
                out _,
                out _);
            return urgentCandidate.IsValid
                && urgentCandidate.UrgencyScore >= 60f;
        }

        foreach (BuildableObject building in GetReachableWorkCandidates(
                     searchResult,
                     requestedWorkType))
        {
            if (TryEvaluateWorkTarget(building, searchResult, requestedWorkType, false, out WorkTargetCandidate candidate)
                && candidate.UrgencyScore >= 60f)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasUrgentAnyAvailableWork(GridPathSearchResult searchResult)
    {
        return HasUrgentAvailableWorkWithLegacyType(searchResult, FacilityWorkType.None);
    }

    public bool HasUrgentAvailableWork(
        GridPathSearchResult searchResult,
        WorkTypeId requestedWorkTypeId)
    {
        return WorkTypeCatalog.TryGet(requestedWorkTypeId, out WorkTypeDefinition definition)
            && HasUrgentAvailableWorkWithLegacyType(searchResult, definition.Type);
    }

    private bool TryGetBestCandidateWithLegacyType(
        FacilityWorkType requestedWorkType,
        GridPathSearchResult searchResult,
        out WorkTargetCandidate best)
    {
        bool useCache = searchResult == null && work.WorkerActor?.Brain != null;
        if (useCache)
        {
            PrepareCandidateCache();
            if (candidateCache.TryGetValue(
                requestedWorkType,
                out CandidateCacheEntry cached))
            {
                best = cached.Candidate;
                LastRejectedCandidate = cached.Rejected;
                return cached.Found;
            }
        }

        ICharacterAiPerformanceRecorder recorder =
            work.WorkerActor?.Brain?.PerformanceRecorder;
        bool collectPerformance =
            recorder?.DetailedCollectionEnabled == true;
        long started = collectPerformance
            ? Stopwatch.GetTimestamp()
            : 0L;
        long allocatedAtStart = collectPerformance
            ? System.GC.GetAllocatedBytesForCurrentThread()
            : 0L;
        try
        {
            using (CandidateSelectionMarker.Auto())
            {
            WorkTargetCandidate rejected = default;
            bool scanComplete = true;
            bool found = useCache
                ? AdvanceIncrementalCandidateScan(
                    requestedWorkType,
                    out best,
                    out _,
                    out rejected,
                    out scanComplete)
                : TryGetBestCandidateWithLegacyTypeCore(
                    requestedWorkType,
                    searchResult,
                    out best);
            if (useCache)
            {
                LastRejectedCandidate = rejected;
            }

            if (useCache && scanComplete)
            {
                candidateCache[requestedWorkType] = new CandidateCacheEntry(
                    found,
                    best,
                    LastRejectedCandidate);
            }

            return found;
            }
        }
        finally
        {
            if (started != 0L)
            {
                recorder.Record(
                    AiPerformanceCategory.WorkTargetSelection,
                    (Stopwatch.GetTimestamp() - started)
                    * 1000.0
                    / Stopwatch.Frequency,
                    System.Math.Max(
                        0L,
                        System.GC.GetAllocatedBytesForCurrentThread()
                            - allocatedAtStart));
            }
        }
    }

    private bool TryGetBestCandidateWithLegacyTypeCore(
        FacilityWorkType requestedWorkType,
        GridPathSearchResult searchResult,
        out WorkTargetCandidate best)
    {
        best = default;
        WorkTargetCandidate rejected = default;
        float bestScore = float.NegativeInfinity;
        int bestRejectedRelevance = int.MinValue;
        foreach (BuildableObject building in GetReachableWorkCandidates(
                     searchResult,
                     requestedWorkType))
        {
            TryEvaluateWorkTarget(
                building,
                searchResult,
                requestedWorkType,
                false,
                out WorkTargetCandidate candidate);
            if (candidate.IsValid)
            {
                if (!best.IsValid || candidate.Score > bestScore)
                {
                    best = candidate;
                    bestScore = candidate.Score;
                }

                continue;
            }

            int relevance = GetFailureRelevance(candidate);
            if (relevance > bestRejectedRelevance)
            {
                rejected = candidate;
                bestRejectedRelevance = relevance;
            }
        }

        if (best.IsValid)
        {
            LastRejectedCandidate = default;
            RecordBestWorkBreakdown(best);
            return true;
        }

        LastRejectedCandidate = rejected;
        return false;
    }

    private bool AdvanceIncrementalCandidateScan(
        FacilityWorkType requestedWorkType,
        out WorkTargetCandidate best,
        out WorkTargetCandidate mostUrgent,
        out WorkTargetCandidate rejected,
        out bool complete)
    {
        best = default;
        mostUrgent = default;
        rejected = default;
        complete = false;

        CharacterActor actor = work.WorkerActor;
        Grid activeGrid = work.WorkGridResolver.ResolveActiveGrid(work, null);
        if (actor == null || activeGrid == null)
        {
            complete = true;
            return false;
        }

        IFacilityCandidateCache facilityCache =
            work.FacilityCandidateCacheService;
        IReadOnlyList<BuildableObject> source =
            facilityCache.GetWorkCandidates(activeGrid, requestedWorkType);
        int frame = work.GameClock.FrameCount;
        int candidateIndexVersion = facilityCache.CandidateIndexVersion;
        int gridVersion = activeGrid.StructuralVersion;
        int buildingVersion = actor.WorldRegistry?.BuildingVersion ?? -1;
        int workOrderVersion =
            work.WorkOrderRuntime?.WorkOrderCandidateVersion ?? -1;

        if (!incrementalScans.TryGetValue(
                requestedWorkType,
                out IncrementalCandidateScan scan)
            || scan.CandidateIndexVersion != candidateIndexVersion
            || scan.GridVersion != gridVersion
            || scan.BuildingVersion != buildingVersion
            || scan.WorkOrderVersion != workOrderVersion
            || (scan.Complete
                && frame - scan.CompletedFrame
                    >= CompletedCandidateRefreshFrames))
        {
            scan = new IncrementalCandidateScan
            {
                Source = source,
                CandidateIndexVersion = candidateIndexVersion,
                GridVersion = gridVersion,
                BuildingVersion = buildingVersion,
                WorkOrderVersion = workOrderVersion,
                StartOffset = facilityCache.HasPendingIndexBuild
                    ? 0
                    : ResolveCandidateStartOffset(actor, source.Count)
            };
            incrementalScans[requestedWorkType] = scan;
        }
        else
        {
            scan.Source = source;
        }

        int sourceCount = scan.Source?.Count ?? 0;
        if (scan.LastAdvancedFrame != frame)
        {
            double sliceMilliseconds =
                actor.FrameWorkBudget?.GetSliceMilliseconds(
                    DynamicFrameWorkDomain.Work,
                    0.02,
                    CandidateScanFallbackSliceMilliseconds)
                ?? CandidateScanFallbackSliceMilliseconds;
            long started = Stopwatch.GetTimestamp();
            int evaluatedThisSlice = 0;
            while (scan.EvaluatedCount < sourceCount)
            {
                int sourceIndex = (scan.StartOffset + scan.EvaluatedCount)
                    % sourceCount;
                BuildableObject building = scan.Source[sourceIndex];
                scan.EvaluatedCount++;
                evaluatedThisSlice++;

                TryEvaluateWorkTarget(
                    building,
                    null,
                    requestedWorkType,
                    false,
                    out WorkTargetCandidate candidate);
                if (candidate.IsValid)
                {
                    if (!scan.Best.IsValid
                        || candidate.Score > scan.Best.Score)
                    {
                        scan.Best = candidate;
                    }

                    if (!scan.MostUrgent.IsValid
                        || candidate.UrgencyScore
                            > scan.MostUrgent.UrgencyScore
                        || (Mathf.Approximately(
                                candidate.UrgencyScore,
                                scan.MostUrgent.UrgencyScore)
                            && candidate.Score > scan.MostUrgent.Score))
                    {
                        scan.MostUrgent = candidate;
                    }
                }
                else if (GetFailureRelevance(candidate)
                         > GetFailureRelevance(scan.Rejected))
                {
                    scan.Rejected = candidate;
                }

                if (evaluatedThisSlice >= CandidateScanMinimumBatch
                    && GetElapsedMilliseconds(started)
                        >= sliceMilliseconds)
                {
                    break;
                }
            }

            scan.LastAdvancedFrame = frame;
        }

        sourceCount = scan.Source?.Count ?? 0;
        scan.Complete = scan.EvaluatedCount >= sourceCount
            && !facilityCache.HasPendingIndexBuild;
        if (scan.Complete)
        {
            scan.CompletedFrame = frame;
        }
        else
        {
            actor.FrameWorkBudget?.SetBacklog(
                DynamicFrameWorkDomain.Work,
                sourceCount - scan.EvaluatedCount);
        }

        best = scan.Best;
        mostUrgent = scan.MostUrgent;
        rejected = best.IsValid ? default : scan.Rejected;
        complete = scan.Complete;
        if (best.IsValid && complete)
        {
            RecordBestWorkBreakdown(best);
            return true;
        }

        return best.IsValid;
    }

    private static int ResolveCandidateStartOffset(
        CharacterActor actor,
        int candidateCount)
    {
        if (actor == null || candidateCount <= 1)
        {
            return 0;
        }

        uint stableId = unchecked((uint)actor.GetInstanceID());
        return (int)(stableId % (uint)candidateCount);
    }

    public bool TryGetBestAnyCandidate(
        GridPathSearchResult searchResult,
        out WorkTargetCandidate best)
    {
        return TryGetBestCandidateWithLegacyType(FacilityWorkType.None, searchResult, out best);
    }

    public bool TryGetBestCandidate(
        WorkTypeId requestedWorkTypeId,
        GridPathSearchResult searchResult,
        out WorkTargetCandidate best)
    {
        best = default;
        return WorkTypeCatalog.TryGet(requestedWorkTypeId, out WorkTypeDefinition definition)
            && TryGetBestCandidateWithLegacyType(definition.Type, searchResult, out best);
    }

    private float GetUtilityScoreWithLegacyType(FacilityWorkType requestedWorkType, GridPathSearchResult searchResult)
    {
        if (!TryGetBestCandidateWithLegacyType(requestedWorkType, searchResult, out WorkTargetCandidate candidate))
        {
            return 0f;
        }

        return Mathf.Clamp01(candidate.Score / WorkUtilityScoreDivisor);
    }

    public float GetAnyUtilityScore(GridPathSearchResult searchResult)
    {
        return GetUtilityScoreWithLegacyType(FacilityWorkType.None, searchResult);
    }

    public float GetUtilityScore(WorkTypeId requestedWorkTypeId, GridPathSearchResult searchResult)
    {
        return WorkTypeCatalog.TryGet(requestedWorkTypeId, out WorkTypeDefinition definition)
            ? GetUtilityScoreWithLegacyType(definition.Type, searchResult)
            : 0f;
    }

    public IEnumerable<BuildableObject> GetReachableBuildings(GridPathSearchResult searchResult)
    {
        if (searchResult != null)
        {
            IReadOnlyList<BuildableObject> reachable =
                searchResult.GetAllReachableBuilding();
            foreach (BuildableObject building in reachable)
            {
                if (building != null && !building.isDestroy)
                {
                    yield return building;
                }
            }

            if (work.ExteriorZoneQuery == null)
            {
                yield break;
            }

            foreach (ExteriorZoneMarker marker in work.ExteriorZoneQuery.Zones)
            {
                if (marker != null
                    && !marker.isDestroy
                    && searchResult.ContainsPosition(marker.GridPosition)
                    && !reachable.Contains(marker))
                {
                    yield return marker;
                }
            }

            yield break;
        }

        Grid activeGrid = work.WorkGridResolver.ResolveActiveGrid(work, null);
        if (activeGrid == null)
        {
            yield break;
        }

        IReadOnlyList<BuildableObject> registered =
            work.WorkerActor?.WorldRegistry?.Buildings;
        if (registered != null && registered.Count > 0)
        {
            foreach (BuildableObject building in registered)
            {
                if (building != null
                    && !building.isDestroy
                    && building.Grid == activeGrid)
                {
                    yield return building;
                }
            }

            yield break;
        }

        foreach (IGridOccupant occupant in activeGrid.FindAllOccupants(null))
        {
            if (occupant is BuildableObject building && !building.isDestroy)
            {
                yield return building;
            }
        }
    }

    private IEnumerable<BuildableObject> GetReachableWorkCandidates(
        GridPathSearchResult searchResult,
        FacilityWorkType requestedWorkType)
    {
        if (searchResult != null)
        {
            foreach (BuildableObject building in GetReachableBuildings(searchResult))
            {
                yield return building;
            }

            yield break;
        }

        Grid activeGrid = work.WorkGridResolver.ResolveActiveGrid(work, null);
        if (activeGrid == null)
        {
            yield break;
        }

        IReadOnlyList<BuildableObject> indexed =
            work.FacilityCandidateCacheService.GetWorkCandidates(
                activeGrid,
                requestedWorkType);
        for (int index = 0; index < indexed.Count; index++)
        {
            BuildableObject building = indexed[index];
            if (building != null && !building.isDestroy)
            {
                yield return building;
            }
        }
    }

    public IEnumerable<IWarehouseFacility> FindReachableWarehouses(GridPathSearchResult searchResult = null)
    {
        if (searchResult == null)
        {
            Grid activeGrid = work.WorkGridResolver.ResolveActiveGrid(work, null);
            IReadOnlyList<IWarehouseFacility> registered =
                work.WorkerActor?.WorldRegistry?.Warehouses;
            if (registered != null && registered.Count > 0)
            {
                for (int index = 0; index < registered.Count; index++)
                {
                    IWarehouseFacility warehouse = registered[index];
                    if (warehouse != null
                        && warehouse.HasWarehouseInventory
                        && warehouse is BuildableObject building
                        && building.Grid == activeGrid
                        && !building.isDestroy)
                    {
                        yield return warehouse;
                    }
                }

                yield break;
            }
        }

        foreach (IWarehouseFacility warehouse in GetReachableBuildings(searchResult)
                     .OfType<IWarehouseFacility>())
        {
            if (warehouse.HasWarehouseInventory)
            {
                yield return warehouse;
            }
        }
    }

    internal bool TryEvaluateWorkTarget(
        BuildableObject building,
        GridPathSearchResult searchResult,
        FacilityWorkType forcedWorkType,
        bool ignorePriority,
        out WorkTargetCandidate bestCandidate)
    {
        bestCandidate = WorkTargetCandidate.Invalid(
            building,
            "현재 작업할 수 없는 시설입니다",
            AIActionFailureKind.NoWork);
        WorkPriorityProfile priorities = work.WorkPriorities ?? WorkPriorityProfile.CreateDefault();

        if (building == null || building.isDestroy)
        {
            bestCandidate = WorkTargetCandidate.Invalid(
                building,
                "시설이 없습니다",
                building != null && building.isDestroy
                    ? AIActionFailureKind.Destroyed
                    : AIActionFailureKind.NoDestination);
            LastRejectedCandidate = bestCandidate;
            return false;
        }

        if (building is not IWorkableFacility workable)
        {
            bestCandidate = WorkTargetCandidate.Invalid(
                building,
                "작업 가능한 시설이 아닙니다",
                AIActionFailureKind.Unsupported);
            LastRejectedCandidate = bestCandidate;
            return false;
        }

        FacilityWorkType supportedTypes = building is ConstructionSite
            ? FacilityWorkType.Construct
            : building.Facility != null
                ? WildlifeButcherFacilityUtility.AddFallbackWorkTypes(building, building.Facility.supportedWorkTypes)
                : FacilityWorkType.None;
        supportedTypes = SurvivalFacilityUtility.AddFallbackWorkTypes(building, supportedTypes);
        supportedTypes = CombatEquipmentMaintenanceFacilityUtility.AddFallbackWorkTypes(
            building,
            supportedTypes);
        if (supportedTypes == FacilityWorkType.None)
        {
            bestCandidate = WorkTargetCandidate.Invalid(
                building,
                "지원하는 작업이 없습니다",
                AIActionFailureKind.Unsupported);
            LastRejectedCandidate = bestCandidate;
            return false;
        }

        FacilityAssignmentStatus workerStatus = workable.GetWorkerAssignmentStatus(work.WorkerActor);
        if (!workerStatus.IsAllowed)
        {
            bestCandidate = WorkTargetCandidate.Invalid(
                building,
                workerStatus.Reason,
                workerStatus.FailureKind.ToAiActionFailureKind());
            LastRejectedCandidate = bestCandidate;
            return false;
        }

        if (building is ConstructionSite safetySite)
        {
            ConstructionSafetyResult safety = safetySite.GetConstructionSafetyState(
                work.WorkerActor,
                forced: ignorePriority);
            if (!safety.IsSafe)
            {
                bestCandidate = WorkTargetCandidate.Invalid(
                    building,
                    safety.Message,
                    safety.Reason == ConstructionSafetyReason.WorkerEscapeBlocked
                        || safety.Reason == ConstructionSafetyReason.EntranceBlocked
                            ? AIActionFailureKind.NoPath
                            : AIActionFailureKind.NoWork);
                LastRejectedCandidate = bestCandidate;
                return false;
            }
        }

        if (searchResult != null && !IsReachableWorkBuilding(building, searchResult))
        {
            bestCandidate = WorkTargetCandidate.Invalid(
                building,
                "도달할 수 없는 대상입니다",
                AIActionFailureKind.NoPath);
            LastRejectedCandidate = bestCandidate;
            return false;
        }

        AIActionFailure lastWorkTypeFailure = AIActionFailure.None;
        IReadOnlyList<WorkTypeDefinition> workTypeDefinitions =
            WorkTypeCatalog.All;
        for (int definitionIndex = 0;
            definitionIndex < workTypeDefinitions.Count;
            definitionIndex++)
        {
            WorkTypeDefinition definition =
                workTypeDefinitions[definitionIndex];
            if ((supportedTypes & definition.Type) == 0)
            {
                continue;
            }

            FacilityWorkType workType = definition.Type;
            WorkTypeId workTypeId = definition.WorkTypeId;
            if (forcedWorkType != FacilityWorkType.None && workType != forcedWorkType)
            {
                continue;
            }

            if (forcedWorkType == FacilityWorkType.None && work.ShouldThrottleRoutineWork(workTypeId))
            {
                continue;
            }

            if (captiveLaborQuery != null
                && !captiveLaborQuery.IsWorkAllowed(
                    work.WorkerActor,
                    workTypeId,
                    out string captiveWorkReason))
            {
                lastWorkTypeFailure = AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    captiveWorkReason,
                    building);
                continue;
            }

            if (workType == FacilityWorkType.Restock)
            {
                if (building is not IRestockableFacility restockable)
                {
                    lastWorkTypeFailure = AIActionFailure.Create(
                        AIActionFailureKind.Unsupported,
                        "재고를 보충할 수 없는 시설입니다",
                        building);
                    continue;
                }

                if (!restockable.NeedsRestock)
                {
                    continue;
                }

                bool hasRestockSupply;
                string supplyFailureReason;
                IReadOnlyList<IWarehouseFacility> registeredWarehouses =
                    searchResult == null
                        ? work.WorkerActor?.WorldRegistry?.Warehouses
                        : null;
                if (registeredWarehouses != null)
                {
                    hasRestockSupply = restockable.HasRestockSupply(
                        registeredWarehouses,
                        out supplyFailureReason);
                }
                else
                {
                    hasRestockSupply = restockable.HasRestockSupply(
                        FindReachableWarehouses(searchResult),
                        out supplyFailureReason);
                }

                if (!hasRestockSupply)
                {
                    lastWorkTypeFailure = AIActionFailure.Create(
                        AIActionFailureKind.NoWork,
                        supplyFailureReason,
                        building);
                    continue;
                }
            }

            if (workPolicyRegistry != null
                && !workPolicyRegistry.IsAvailable(
                    workTypeId,
                    work.WorkerActor,
                    building,
                    out _))
            {
                continue;
            }

            if (HasExteriorWorkRuntime(building, workTypeId)
                && !IsExteriorWorkAvailable(building, work.WorkerActor, workTypeId))
            {
                continue;
            }

            WorkPriorityLevel priority = ignorePriority
                ? WorkPriorityLevel.Priority1
                : priorities.GetPriority(workTypeId);
            if (priority == WorkPriorityLevel.Off)
            {
                continue;
            }

            FacilityAssignmentStatus workStatus = building is ConstructionSite constructionSite
                ? constructionSite.GetConstructionWorkStatus()
                : building.GetWorkAssignmentStatus(workTypeId);
            if (!workStatus.IsAllowed)
            {
                lastWorkTypeFailure = AIActionFailure.Create(
                    workStatus.FailureKind.ToAiActionFailureKind(),
                    workStatus.Reason,
                    building);
                continue;
            }

            WorkTargetCandidate candidate = BuildCandidate(building, definition, priority, searchResult);
            if (!bestCandidate.IsValid || candidate.Score > bestCandidate.Score)
            {
                bestCandidate = candidate;
            }
        }

        if (!bestCandidate.IsValid)
        {
            string reason = forcedWorkType != FacilityWorkType.None
                ? $"{WorkTaskCatalog.GetLegacyDisplayName(forcedWorkType)} 작업을 수행할 수 없습니다"
                : "켜진 작업 우선순위가 없습니다";
            bestCandidate = WorkTargetCandidate.Invalid(
                building,
                lastWorkTypeFailure.HasFailure ? lastWorkTypeFailure.ToString() : reason,
                lastWorkTypeFailure.HasFailure ? lastWorkTypeFailure.Kind : AIActionFailureKind.NoWork);
            LastRejectedCandidate = bestCandidate;
            return false;
        }

        LastRejectedCandidate = default;
        return true;
    }

    private WorkTargetCandidate BuildCandidate(
        BuildableObject building,
        WorkTypeDefinition definition,
        WorkPriorityLevel priority,
        GridPathSearchResult searchResult)
    {
        FacilityWorkType workType = definition.Type;
        WorkTypeId workTypeId = definition.WorkTypeId;
        CharacterActor actor = work.WorkerActor;
        float urgency;
        using (CandidateUrgencyMarker.Auto())
        {
            urgency = building.GetWorkUrgency(workTypeId);
            urgency = Mathf.Max(
                urgency,
                workPolicyRegistry?.GetAdditionalUrgency(workTypeId, actor, building) ?? 0f);
            urgency += GetExteriorWorkUrgency(building, actor, workTypeId);
        }

        float preferenceScore;
        float speedScore;
        float survivalPressure;
        using (CandidateActorStatsMarker.Auto())
        {
            ActorWorkScore actorScore = GetActorWorkScore(actor, workTypeId);
            preferenceScore = actorScore.Preference;
            speedScore = actorScore.Speed;
            survivalPressure = actor != null
                ? GetDecisionContext(actor).EmergencyScore
                : 0f;
        }

        float distanceScore;
        float roomContextScore;
        float facilityStateScore;
        float roleFitScore;
        using (CandidateSpatialMarker.Auto())
        {
            using (CandidateDistanceMarker.Auto())
            {
                distanceScore = GetDistanceScore(building, searchResult);
            }

            using (CandidateRoomMarker.Auto())
            {
                roomContextScore = GetRoomContextScore(building);
            }

            using (CandidateFacilityStateMarker.Auto())
            {
                facilityStateScore = GetFacilityStateScore(building, workType);
            }

            using (CandidateRoleMarker.Auto())
            {
                roleFitScore = GetWorkRoleFitScore(building, workType);
            }
        }

        CharacterAiWorldSignalSnapshot signals;
        using (CandidateWorldSignalMarker.Auto())
        {
            signals = actor?.WorldSignalQuery?.Capture(
                    actor,
                    CharacterAiBranch.Work,
                    building,
                    searchResult)
                ?? CharacterAiWorldSignalSnapshot.Neutral;
        }

        float fatigueScale = Mathf.Lerp(
            1f,
            0.25f,
            Mathf.Clamp01(Mathf.Max(urgency / 85f, survivalPressure)));
        float fatiguePenalty;
        float targetFatiguePenalty;
        using (CandidateMemoryMarker.Auto())
        {
            fatiguePenalty = actor != null && actor.AiMemory != null
                ? actor.AiMemory.GetRepeatedWorkFatigue(workTypeId) * 18f * fatigueScale
                : 0f;
            targetFatiguePenalty = actor != null && actor.AiMemory != null
                ? actor.AiMemory.GetRecentTargetWorkFatigue(building, workTypeId) * 14f * fatigueScale
                : 0f;
        }
        float queueBonus = Mathf.Clamp01(1f - signals.QueuePressure) * 8f;
        float pathConfidenceBonus = signals.PathConfidence * 9f;
        float scheduleBonus = signals.ScheduleScore * 8f;
        float weatherPenalty = signals.WeatherPressure * (IsExteriorWorkType(workType) ? 12f : 4f);
        float failurePenalty = signals.RecentFailurePressure * 10f;
        float movementPenalty = signals.RecentMovementPressure * 8f * fatigueScale;
        float softLockBonus = actor != null
            && actor.Blackboard != null
            && actor.Blackboard.SoftLockIntent != null
            && actor.Blackboard.SoftLockIntent.Matches(CharacterAiBranch.Work, building)
                ? 16f
                : 0f;
        float score = priority.GetBaseScore()
            + urgency
            + (preferenceScore * 35f)
            + (speedScore * 25f)
            + distanceScore
            + roomContextScore
            + facilityStateScore
            + roleFitScore
            + queueBonus
            + pathConfidenceBonus
            + scheduleBonus
            + softLockBonus
            - fatiguePenalty
            - targetFatiguePenalty
            - weatherPenalty
            - failurePenalty
            - movementPenalty;

        // Candidate scans stay numeric. The selected candidate is formatted once
        // by RecordBestWorkBreakdown instead of allocating details per facility.
        bool captureDetails = false;
        string breakdownSummary = string.Empty;
        if (captureDetails)
        {
            CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
                GetWorkIntention(workType),
                $"{GetBuildingLabel(building)} {definition.DisplayName}",
                true);
            breakdown.Add(CharacterAiUtilityFactorKind.Priority, Mathf.Clamp01(priority.GetBaseScore() / 300f), 0.28f, priority.ToDisplayText());
            breakdown.Add(CharacterAiUtilityFactorKind.Need, Mathf.Clamp01(urgency / 100f), 0.22f, "긴급도");
            breakdown.Add(CharacterAiUtilityFactorKind.Personality, preferenceScore, 0.16f, "작업 적성");
            breakdown.Add(
                CharacterAiUtilityFactorKind.Momentum,
                Mathf.Clamp01((speedScore * 25f + softLockBonus) / 41f),
                0.1f,
                "작업 속도/하던 일 유지");
            breakdown.Add(CharacterAiUtilityFactorKind.Distance, Mathf.Clamp01(distanceScore / 25f), 0.08f, "거리");
            breakdown.Add(CharacterAiUtilityFactorKind.Room, Mathf.InverseLerp(-15f, 28f, roomContextScore), 0.08f, "방 환경");
            breakdown.Add(CharacterAiUtilityFactorKind.Reservation, Mathf.InverseLerp(-26f, 12f, facilityStateScore), 0.05f, "시설 상태");
            breakdown.Add(CharacterAiUtilityFactorKind.Queue, Mathf.Clamp01(queueBonus / 8f), 0.05f, "작업 혼잡");
            breakdown.Add(CharacterAiUtilityFactorKind.PathConfidence, signals.PathConfidence, 0.05f, "경로 신뢰");
            breakdown.Add(CharacterAiUtilityFactorKind.Schedule, signals.ScheduleScore, 0.04f, "근무 흐름");
            breakdown.Add(CharacterAiUtilityFactorKind.Weather, Mathf.Clamp01(1f - weatherPenalty / 12f), 0.03f, "외부 부담");
            breakdown.Add(
                CharacterAiUtilityFactorKind.Fatigue,
                Mathf.Clamp01(1f - (fatiguePenalty + targetFatiguePenalty + movementPenalty) / 40f),
                0.04f,
                "반복/이동 피로");
            breakdown.Add(CharacterAiUtilityFactorKind.Memory, Mathf.Clamp01(1f - failurePenalty / 10f), 0.03f, "최근 실패");
            breakdown.SetFinalScore(Mathf.Clamp01(score / WorkUtilityScoreDivisor));
            breakdownSummary = breakdown.ToCompactString();
        }

        return new WorkTargetCandidate(
            building,
            definition,
            priority,
            score,
            urgency,
            string.Empty,
            AIActionFailureKind.None,
            breakdownSummary);
    }

    private void RecordBestWorkBreakdown(WorkTargetCandidate candidate)
    {
        if (!candidate.IsValid || work.WorkerActor == null || work.WorkerActor.Blackboard == null)
        {
            return;
        }

        if (!work.WorkerActor.ShouldCollectDetailedAiDiagnostics)
        {
            return;
        }

        if (candidate.Building == lastRecordedBreakdownBuilding
            && candidate.WorkTypeId == lastRecordedBreakdownWorkTypeId)
        {
            return;
        }

        lastRecordedBreakdownBuilding = candidate.Building;
        lastRecordedBreakdownWorkTypeId = candidate.WorkTypeId;
        CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
            GetWorkIntention(candidate.WorkType),
            $"{GetBuildingLabel(candidate.Building)} {candidate.DisplayName}");
        breakdown.Add(CharacterAiUtilityFactorKind.Priority, Mathf.Clamp01(candidate.Priority.GetBaseScore() / 300f), 0.35f, candidate.Priority.ToDisplayText());
        breakdown.Add(CharacterAiUtilityFactorKind.Need, Mathf.Clamp01(candidate.UrgencyScore / 100f), 0.3f, "긴급도");
        breakdown.Add(CharacterAiUtilityFactorKind.Reservation, 1f, 0.2f, "예약 가능");
        breakdown.Add(CharacterAiUtilityFactorKind.Momentum, Mathf.Clamp01(candidate.Score / WorkUtilityScoreDivisor), 0.15f, "종합 점수");
        breakdown.SetFinalScore(Mathf.Clamp01(candidate.Score / WorkUtilityScoreDivisor));
        work.WorkerActor.Blackboard.RecordUtilityBreakdown(breakdown);
    }

    private static int GetFailureRelevance(WorkTargetCandidate candidate)
    {
        return candidate.FailureKind switch
        {
            AIActionFailureKind.DestinationOccupied => 50,
            AIActionFailureKind.NoPath => 40,
            AIActionFailureKind.NoWork => 30,
            AIActionFailureKind.OffDuty => 20,
            AIActionFailureKind.Unsupported => 10,
            _ => 0
        };
    }

    private CharacterAiDecisionContext GetDecisionContext(CharacterActor actor)
    {
        int frame = work.GameClock.FrameCount;
        if (cachedDecisionContextFrame == frame && cachedContextActor == actor)
        {
            return cachedDecisionContext;
        }

        cachedContextActor = actor;
        cachedDecisionContext = CharacterAiDecisionContext.Capture(actor, CharacterAiBranch.Work);
        cachedDecisionContextFrame = frame;
        return cachedDecisionContext;
    }

    private ActorWorkScore GetActorWorkScore(
        CharacterActor actor,
        WorkTypeId workTypeId)
    {
        if (actor == null)
        {
            return new ActorWorkScore(0.5f, 0.5f);
        }

        int frame = work.GameClock.FrameCount;
        if (actorWorkScoreCacheFrame != frame)
        {
            actorWorkScoreCache.Clear();
            actorWorkScoreCacheFrame = frame;
        }

        if (actorWorkScoreCache.TryGetValue(
                workTypeId,
                out ActorWorkScore cached))
        {
            return cached;
        }

        ActorWorkScore score = new ActorWorkScore(
            actor.GetWorkPreferenceScore(workTypeId),
            Mathf.Clamp01(actor.GetWorkSpeedMultiplier(workTypeId) / 2f));
        actorWorkScoreCache[workTypeId] = score;
        return score;
    }

    private float GetDistanceScore(BuildableObject building, GridPathSearchResult searchResult)
    {
        if (building == null)
        {
            return 0f;
        }

        int travelCost = searchResult?.GetMoveCostTo(building) ?? int.MaxValue;
        if (travelCost == int.MaxValue)
        {
            Vector2Int start = work.WorkerActor != null
                ? work.WorkerActor.GetNowXY()
                : building.centerPos;
            travelCost = EstimateDestinationCost(start, building);
        }

        float normalizedDistance = travelCost
            / (float)DefaultGridTraversalCostPolicy.DryWalkCost;
        return Mathf.Clamp(25f - normalizedDistance, 0f, 25f);
    }

    private static int EstimateDestinationCost(
        Vector2Int start,
        BuildableObject building)
    {
        IReadOnlyList<Vector2Int> positions = building?.buildPoses;
        if (positions == null || positions.Count == 0)
        {
            return EstimateDestinationCost(start, building != null
                ? building.centerPos
                : start);
        }

        int best = int.MaxValue;
        for (int index = 0; index < positions.Count; index++)
        {
            best = Mathf.Min(best, EstimateDestinationCost(start, positions[index]));
        }

        return best;
    }

    private static int EstimateDestinationCost(
        Vector2Int start,
        Vector2Int destination)
    {
        int horizontal = Mathf.Abs(start.x - destination.x)
            * DefaultGridTraversalCostPolicy.DryWalkCost;
        int floors = Mathf.Abs(start.y - destination.y);
        return horizontal
            + floors * DefaultGridTraversalCostPolicy.StairFallbackCost;
    }

    private void PrepareCandidateCache()
    {
        CharacterActor actor = work.WorkerActor;
        Grid activeGrid = work.WorkGridResolver.ResolveActiveGrid(work, null);
        int frame = work.GameClock.FrameCount;
        int gridVersion = activeGrid?.StructuralVersion ?? -1;
        int buildingVersion = actor?.WorldRegistry?.BuildingVersion ?? -1;
        int facilityVersion = work.FacilityCandidateCacheService.DynamicStateVersion;
        int workOrderVersion = work.WorkOrderRuntime?.WorkOrderCandidateVersion ?? -1;
        if (candidateCacheFrame == frame
            && candidateCacheGridVersion == gridVersion
            && candidateCacheBuildingVersion == buildingVersion
            && candidateCacheFacilityVersion == facilityVersion
            && candidateCacheWorkOrderVersion == workOrderVersion)
        {
            return;
        }

        candidateCache.Clear();
        candidateCacheFrame = frame;
        candidateCacheGridVersion = gridVersion;
        candidateCacheBuildingVersion = buildingVersion;
        candidateCacheFacilityVersion = facilityVersion;
        candidateCacheWorkOrderVersion = workOrderVersion;
    }

    private float GetRoomContextScore(BuildableObject building)
    {
        if (building == null)
        {
            return 0f;
        }

        if (work.RoomEnvironmentQuery != null)
        {
            return work.RoomEnvironmentQuery.GetFacilityPreferenceScore(building);
        }

        try
        {
            FacilityRoomOperationalProfile profile = building.GetRoomOperationalProfile();
            if (profile == null || profile.Room == null)
            {
                return 8f;
            }

            if (!profile.IsUsableRoom)
            {
                return building.BuildingData.RequiresRoomRole() ? -15f : 0f;
            }

            return Mathf.Lerp(8f, 28f, profile.Room.GetQualityScore());
        }
        catch (System.InvalidOperationException)
        {
            return 8f;
        }
    }

    private static float GetFacilityStateScore(BuildableObject building, FacilityWorkType workType)
    {
        if (building == null)
        {
            return 0f;
        }

        float score = Mathf.Lerp(-8f, 12f, Mathf.Clamp01(building.FacilityState.cleanliness / 100f));
        if (building.IsDamaged && workType != FacilityWorkType.Repair)
        {
            score -= 18f;
        }

        return score;
    }

    private static float GetWorkRoleFitScore(BuildableObject building, FacilityWorkType workType)
    {
        FacilityRole roles = building?.Facility?.roles ?? FacilityRole.None;
        if (roles == FacilityRole.None)
        {
            return 0f;
        }

        bool hasTraining = (roles & FacilityRole.Training) != 0;
        bool hasResearch = (roles & FacilityRole.Research) != 0;
        bool hasSecurity = (roles & FacilityRole.Security) != 0;
        bool hasMeal = (roles & FacilityRole.Meal) != 0;
        bool hasLogistics = (roles & FacilityRole.Logistics) != 0;
        bool hasHygiene = (roles & FacilityRole.Hygiene) != 0;

        return workType switch
        {
            FacilityWorkType.Guard when hasSecurity || hasTraining => 45f,
            FacilityWorkType.Guard when hasResearch || hasMeal || hasHygiene => -18f,
            FacilityWorkType.Research when hasResearch => 24f,
            FacilityWorkType.Research when hasTraining || hasSecurity => -18f,
            FacilityWorkType.Craft when hasLogistics => 16f,
            FacilityWorkType.Cook when hasMeal => 18f,
            FacilityWorkType.Butcher when hasMeal => 18f,
            FacilityWorkType.Restock when hasLogistics => 16f,
            FacilityWorkType.Clean when hasHygiene => 12f,
            _ => 0f
        };
    }

    private static CharacterAiIntentionType GetWorkIntention(FacilityWorkType workType)
    {
        return workType switch
        {
            FacilityWorkType.Haul => CharacterAiIntentionType.Logistics,
            FacilityWorkType.Hunt => CharacterAiIntentionType.Hunt,
            FacilityWorkType.Guard => CharacterAiIntentionType.Guard,
            FacilityWorkType.Rest => CharacterAiIntentionType.Recover,
            _ => CharacterAiIntentionType.Work
        };
    }

    private static bool IsExteriorWorkType(FacilityWorkType workType)
    {
        return workType == FacilityWorkType.Haul
            || workType == FacilityWorkType.Hunt
            || workType == FacilityWorkType.Guard
            || workType == FacilityWorkType.Reception
            || workType == FacilityWorkType.Clean
            || workType == FacilityWorkType.Repair;
    }

    private static string GetBuildingLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "시설 없음";
        }

        return building.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building.name;
    }

    private static bool CanUseSuppressFor(FacilityWorkType requestedWorkType)
    {
        return requestedWorkType == FacilityWorkType.None || requestedWorkType == FacilityWorkType.Guard;
    }

    private static bool IsReachableWorkBuilding(BuildableObject building, GridPathSearchResult searchResult)
    {
        if (building == null || searchResult == null)
        {
            return false;
        }

        if (searchResult.ContainsVisitableOccupant(building))
        {
            return true;
        }

        if (building.Grid == searchResult.sourceGrid)
        {
            IReadOnlyList<Vector2Int> positions = building.buildPoses;
            for (int index = 0; index < positions.Count; index++)
            {
                if (searchResult.ContainsPosition(positions[index]))
                {
                    return true;
                }
            }
        }

        return building is ExteriorZoneMarker marker
            && searchResult.ContainsPosition(marker.GridPosition);
    }

    private static bool HasExteriorWorkRuntime(BuildableObject building, WorkTypeId workTypeId)
    {
        IReadOnlyList<BuildingAbility> abilities =
            building?.BuildingData?.Abilities;
        if (abilities == null)
        {
            return false;
        }

        for (int index = 0; index < abilities.Count; index++)
        {
            if (abilities[index] is IBuildingExteriorWorkRuntimeAbility ability
                && ability.SupportsExteriorWork(workTypeId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExteriorWorkAvailable(
        BuildableObject building,
        CharacterActor actor,
        WorkTypeId workTypeId)
    {
        if (building?.BuildingData == null)
        {
            return false;
        }

        IReadOnlyList<BuildingAbility> abilities =
            building.BuildingData.Abilities;
        for (int index = 0; index < abilities.Count; index++)
        {
            if (abilities[index] is IBuildingExteriorWorkRuntimeAbility ability
                && ability.SupportsExteriorWork(workTypeId)
                && ability.IsExteriorWorkAvailable(actor, building, workTypeId))
            {
                return true;
            }
        }

        return false;
    }

    private static float GetExteriorWorkUrgency(
        BuildableObject building,
        CharacterActor actor,
        WorkTypeId workTypeId)
    {
        if (building?.BuildingData == null)
        {
            return 0f;
        }

        float urgency = 0f;
        IReadOnlyList<BuildingAbility> abilities =
            building.BuildingData.Abilities;
        for (int index = 0; index < abilities.Count; index++)
        {
            if (abilities[index] is IBuildingExteriorWorkRuntimeAbility ability
                && ability.SupportsExteriorWork(workTypeId)
                && ability.IsExteriorWorkAvailable(actor, building, workTypeId))
            {
                urgency += Mathf.Max(0f, ability.GetExteriorWorkUrgency(actor, building, workTypeId));
            }
        }

        return urgency;
    }

    private static double GetElapsedMilliseconds(long started)
    {
        return (Stopwatch.GetTimestamp() - started)
            * 1000.0
            / Stopwatch.Frequency;
    }
}
