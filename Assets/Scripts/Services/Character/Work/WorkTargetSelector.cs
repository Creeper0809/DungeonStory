using System;
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


    private readonly AbilityWork work;
    private readonly IWorkPolicyRegistry workPolicyRegistry;
    private readonly WorkTargetEnvironmentPolicy targetEnvironment;
    private readonly WorkTargetEvaluator targetEvaluator;
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
        IWorkPolicyRegistry workPolicyRegistry,
        ICaptiveLaborQuery captiveLaborQuery,
        IEnvironmentWorkPolicy environmentWorkPolicy)
    {
        this.work = work;
        this.workPolicyRegistry = workPolicyRegistry;
        targetEnvironment = new WorkTargetEnvironmentPolicy(
            work,
            environmentWorkPolicy);
        targetEvaluator = new WorkTargetEvaluator(
            work,
            workPolicyRegistry,
            captiveLaborQuery,
            targetEnvironment,
            FindReachableWarehouses,
            BuildCandidate,
            WorkTargetSelectionRules.IsReachable);
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
            && TryAssignWorkWithLegacyType(
                searchResult,
                FacilityWorkTypeMap.GetRequired(definition));
    }

    private bool TryAssignWorkWithLegacyType(
        GridPathSearchResult searchResult,
        FacilityWorkType requestedWorkType)
    {
        if (work.HasPrioritySuppressTarget
            && WorkTargetSelectionRules.CanUseSuppressFor(requestedWorkType))
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
            if (canUsePriorityForRequest)
            {
                if (TryEvaluateWorkTarget(
                        work.PriorityWorkTarget,
                        searchResult,
                        work.PriorityWorkType,
                        forced,
                        out WorkTargetCandidate priorityCandidate))
                {
                    work.AssignWork(
                        work.PriorityWorkTarget,
                        priorityCandidate.WorkTypeId);
                    return true;
                }

                if (ShouldRetainPriorityTarget(priorityCandidate))
                {
                    // The direct order remains authoritative while its target is
                    // temporarily unable to accept this worker. Do not replace it
                    // with unrelated autonomous work or erase the player's order.
                    work.AssignWork(null, FacilityWorkType.None);
                    return false;
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
            work.AssignWork(work.assignedShop, assignedCandidate.WorkTypeId);
            return true;
        }

        if (work.assignedShop != null && CanUseAsWorkTargetWithLegacyType(work.assignedShop, requestedWorkType))
        {
            return true;
        }

        TryGetBestCandidateWithLegacyType(requestedWorkType, searchResult, out WorkTargetCandidate best);
        work.AssignWork(
            WorkTargetCandidateRuntimeAdapter.ResolveBuilding(best),
            best.WorkTypeId);
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
            && HasUrgentAvailableWorkWithLegacyType(
                searchResult,
                FacilityWorkTypeMap.GetRequired(definition));
    }

    private bool TryGetBestCandidateWithLegacyType(
        FacilityWorkType requestedWorkType,
        GridPathSearchResult searchResult,
        out WorkTargetCandidate best)
    {
        if (work.PriorityWorkTarget != null)
        {
            bool canUsePriorityForRequest = requestedWorkType == FacilityWorkType.None
                || work.PriorityWorkType == requestedWorkType;
            if (canUsePriorityForRequest)
            {
                bool forced = work.PriorityWorkType != FacilityWorkType.None;
                bool priorityFound = TryEvaluateWorkTarget(
                    work.PriorityWorkTarget,
                    searchResult,
                    work.PriorityWorkType,
                    forced,
                    out best);
                if (priorityFound)
                {
                    RecordBestWorkBreakdown(best);
                    return true;
                }

                // A direct player order owns target selection. Temporary
                // failures remain visible on that target and must not be
                // replaced by unrelated autonomous work. Permanent failures
                // are cleared by the assignment path after it emits the exact
                // cancellation reason.
                LastRejectedCandidate = best;
                return false;
            }
        }

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
        int dynamicStateVersion = facilityCache.DynamicStateVersion;
        int gridVersion = activeGrid.StructuralVersion;
        int buildingVersion = actor.WorldRegistry?.BuildingVersion ?? -1;
        int workOrderVersion =
            work.WorkOrderRuntime?.WorkOrderCandidateVersion ?? -1;

        if (!incrementalScans.TryGetValue(
                requestedWorkType,
                out IncrementalCandidateScan scan)
            || scan.CandidateIndexVersion != candidateIndexVersion
            || scan.DynamicStateVersion != dynamicStateVersion
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
                DynamicStateVersion = dynamicStateVersion,
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

        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        uint stableId = PersistentEntityId.GetStableHash32(characterId);
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
            && TryGetBestCandidateWithLegacyType(
                FacilityWorkTypeMap.GetRequired(definition),
                searchResult,
                out best);
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
            ? GetUtilityScoreWithLegacyType(
                FacilityWorkTypeMap.GetRequired(definition),
                searchResult)
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

            // GridPathSearchResult's occupant list is visitor-oriented. A
            // roles=None workstation is deliberately absent from it even when
            // an adjacent actor-specific work stand is reachable. Merge the
            // registered work targets through the separate work-access
            // authority; visitor admission remains unchanged.
            IReadOnlyList<BuildableObject> registeredRuntimeTargets =
                work.WorkerActor?.WorldRegistry?.Buildings;
            Grid reachableGrid = work.WorkGridResolver.ResolveActiveGrid(work, null);
            if (registeredRuntimeTargets != null)
            {
                for (int index = 0; index < registeredRuntimeTargets.Count; index++)
                {
                    BuildableObject runtimeTarget = registeredRuntimeTargets[index];
                    if (runtimeTarget != null
                        && !runtimeTarget.isDestroy
                        && runtimeTarget.Grid == reachableGrid
                        && WorkTargetSelectionRules.IsReachable(
                            runtimeTarget,
                            searchResult)
                        && !reachable.Contains(runtimeTarget))
                    {
                        yield return runtimeTarget;
                    }
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
        bool found = targetEvaluator.TryEvaluate(
            building,
            searchResult,
            forcedWorkType,
            ignorePriority,
            out bestCandidate,
            out WorkTargetCandidate rejectedCandidate);
        LastRejectedCandidate = rejectedCandidate;
        return found;
    }

    internal bool RequiresForcedEnvironmentConfirmation(
        BuildableObject building,
        WorkTypeId workTypeId,
        GridPathSearchResult searchResult,
        out string warning)
    {
        return targetEnvironment.RequiresForcedConfirmation(
            building,
            workTypeId,
            searchResult,
            out warning);
    }

    private WorkTargetCandidate BuildCandidate(
        BuildableObject building,
        WorkTypeDefinition definition,
        WorkPriorityLevel priority,
        GridPathSearchResult searchResult)
    {
        FacilityWorkType workType = FacilityWorkTypeMap.GetRequired(definition);
        WorkTypeId workTypeId = definition.WorkTypeId;
        CharacterActor actor = work.WorkerActor;
        float urgency;
        using (CandidateUrgencyMarker.Auto())
        {
            urgency = building.GetWorkUrgency(workTypeId);
            urgency = Mathf.Max(
                urgency,
                workPolicyRegistry?.GetAdditionalUrgency(workTypeId, actor, building) ?? 0f);
            urgency += WorkTargetExteriorRules.GetUrgency(
                building,
                actor,
                workTypeId);
        }

        float preferenceScore;
        float speedScore;
        float survivalPressure;
        using (CandidateActorStatsMarker.Auto())
        {
            ActorWorkScore actorScore = GetActorWorkScore(
                actor,
                workTypeId,
                building);
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
        float weatherPenalty = signals.WeatherPressure
            * (WorkTargetSelectionRules.IsExteriorWorkType(workType) ? 12f : 4f);
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
        score += CharacterSpeciesWorkAptitudeRules.GetAutonomousUtilityAdjustment(
            actor,
            workTypeId);
        score *= GetIdentityTargetMultiplier(
            actor,
            building,
            workTypeId,
            searchResult);

        // Candidate scans stay numeric. The selected candidate is formatted once
        // by RecordBestWorkBreakdown instead of allocating details per facility.
        bool captureDetails = false;
        string breakdownSummary = string.Empty;
        if (captureDetails)
        {
            CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
                WorkTargetSelectionRules.GetIntention(workTypeId),
                $"{WorkTargetSelectionRules.GetBuildingLabel(building)} {definition.DisplayName}",
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

    private float GetIdentityTargetMultiplier(
        CharacterActor actor,
        BuildableObject building,
        WorkTypeId workTypeId,
        GridPathSearchResult searchResult)
    {
        CharacterRuntimeProfile profile = actor?.Progression?
            .GetEffectiveRuntimeProfile();
        if (profile == null || building == null || !workTypeId.IsValid)
            return 1f;

        HashSet<string> tags = new(StringComparer.Ordinal);
        FacilityRole roles = building.Facility?.roles ?? FacilityRole.None;
        if (workTypeId == BuiltInWorkTypeIds.Guard
            && (roles & FacilityRole.Training) != 0)
            tags.Add("work:combat-training");

        if (workTypeId == BuiltInWorkTypeIds.Research)
            tags.Add("work:new-process");

        if (workTypeId == BuiltInWorkTypeIds.Craft
            && building.BuildingData?
                .GetAbility<BuildingEquipmentCraftingAbility>() != null)
        {
            tags.Add("work:inspect");
            tags.Add("work:quality-first");
            if (work.CandidateUsesSubstituteMaterial(building))
            {
                tags.Add("work:prototype");
                tags.Add("work:new-process");
            }
        }

        if (building.BuildingData?
                .GetAbility<BuildingTemperatureAbility>() != null)
            tags.Add("room:temperature-controlled");

        if (work.IsRecentRetryCandidate(workTypeId))
            tags.Add("work:prevent-repeat-failure");

        if (building.BuildingData?.ResearchFacilityCommand
            == ResearchFacilityCommandKind.MentorAcademy)
            tags.Add("work:mentoring");

        if (workTypeId == BuiltInWorkTypeIds.Restock
            && work.ResourceStockPolicies?.GetEmergencyReadiness()
                is EmergencyStockReadiness readiness
            && readiness.Configured
            && !readiness.Ready)
            tags.Add("work:emergency-check");

        if (targetEnvironment.TryAssessEstimate(
                building,
                workTypeId,
                out WorkEnvironmentAssessment assessment)
            && (assessment.Projection.Cold.RouteHighestRate > 0f
                || assessment.Projection.Cold.WorkEnd
                    > assessment.Projection.Cold.Current + 0.01f))
            tags.Add("work:cold-zone");

        if (workTypeId == BuiltInWorkTypeIds.Rescue
            && IsRoughTerrainRoute(building, searchResult))
            tags.Add("work:rough-terrain-rescue");

        return profile.GetBehaviorUtilityMultiplier(tags);
    }

    private bool IsRoughTerrainRoute(
        BuildableObject building,
        GridPathSearchResult searchResult)
    {
        if (building == null || searchResult == null)
            return false;
        Grid grid = work.WorkGridResolver.ResolveActiveGrid(work, null);
        if (grid == null)
            return false;
        return searchResult.GetMovePathTo(building.centerPos)
            .Any(step => grid.GetGridCell(step.To)?.TerrainType
                != GridCellTerrainType.Dry);
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

        BuildableObject building =
            WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate);
        if (building == lastRecordedBreakdownBuilding
            && candidate.WorkTypeId == lastRecordedBreakdownWorkTypeId)
        {
            return;
        }

        lastRecordedBreakdownBuilding = building;
        lastRecordedBreakdownWorkTypeId = candidate.WorkTypeId;
        CharacterAiUtilityBreakdown breakdown = new CharacterAiUtilityBreakdown(
            WorkTargetSelectionRules.GetIntention(candidate.WorkTypeId),
            $"{WorkTargetSelectionRules.GetBuildingLabel(building)} {candidate.DisplayName}");
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

    private static bool ShouldRetainPriorityTarget(
        WorkTargetCandidate candidate)
    {
        return candidate.FailureKind switch
        {
            AIActionFailureKind.Cooldown => true,
            AIActionFailureKind.CandidateEvaluationDeferred => true,
            AIActionFailureKind.FacilityCandidateDeferred => true,
            AIActionFailureKind.PathSearchDeferred => true,
            AIActionFailureKind.CannotStart => true,
            AIActionFailureKind.DestinationOccupied => true,
            AIActionFailureKind.NoPath => true,
            AIActionFailureKind.Unknown => true,
            _ => false
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
        WorkTypeId workTypeId,
        BuildableObject target)
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

        bool facilitySpecific =
            workTypeId == BuiltInWorkTypeIds.Operate;
        if (!facilitySpecific
            && actorWorkScoreCache.TryGetValue(
                workTypeId,
                out ActorWorkScore cached))
        {
            return cached;
        }

        float speedScore = workTypeId == BuiltInWorkTypeIds.Rest
            ? 0.5f
            : Mathf.Clamp01(
                actor.GetWorkSpeedMultiplier(workTypeId, target) / 2f);
        ActorWorkScore score = new ActorWorkScore(
            actor.GetWorkPreferenceScore(workTypeId),
            speedScore);
        if (!facilitySpecific)
            actorWorkScoreCache[workTypeId] = score;
        return score;
    }

    private float GetDistanceScore(BuildableObject building, GridPathSearchResult searchResult)
    {
        if (building == null)
        {
            return 0f;
        }

        int travelCost = int.MaxValue;
        if (WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                building,
                searchResult,
                out Vector2Int workAccess))
        {
            travelCost = searchResult.GetMoveCostTo(workAccess);
        }
        else if (searchResult != null)
        {
            travelCost = searchResult.GetMoveCostTo(building);
        }
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
            BuildingRoomOperationalSnapshot profile =
                building.GetRoomOperationalProfile();
            if (profile == null || !profile.HasRoom)
            {
                return 8f;
            }

            if (!profile.IsUsableRoom)
            {
                return building.BuildingData.RequiresRoomRole() ? -15f : 0f;
            }

            return Mathf.Lerp(8f, 28f, profile.QualityScore);
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


    private static double GetElapsedMilliseconds(long started)
    {
        return (Stopwatch.GetTimestamp() - started)
            * 1000.0
            / Stopwatch.Frequency;
    }
}
