using System;
using System.Collections.Generic;

internal sealed class WorkTargetEvaluator
{
    private readonly AbilityWork work;
    private readonly IWorkPolicyRegistry workPolicyRegistry;
    private readonly ICaptiveLaborQuery captiveLaborQuery;
    private readonly ICharacterSettlementStandingQuery settlementStandings;
    private readonly WorkTargetEnvironmentPolicy environment;
    private readonly Func<GridPathSearchResult, IEnumerable<IWarehouseFacility>>
        findWarehouses;
    private readonly Func<
        BuildableObject,
        WorkTypeDefinition,
        WorkPriorityLevel,
        GridPathSearchResult,
        WorkTargetCandidate> buildCandidate;
    private readonly Func<BuildableObject, GridPathSearchResult, bool> isReachable;

    public WorkTargetEvaluator(
        AbilityWork work,
        IWorkPolicyRegistry workPolicyRegistry,
        ICaptiveLaborQuery captiveLaborQuery,
        ICharacterSettlementStandingQuery settlementStandings,
        WorkTargetEnvironmentPolicy environment,
        Func<GridPathSearchResult, IEnumerable<IWarehouseFacility>> findWarehouses,
        Func<BuildableObject, WorkTypeDefinition, WorkPriorityLevel,
            GridPathSearchResult, WorkTargetCandidate> buildCandidate,
        Func<BuildableObject, GridPathSearchResult, bool> isReachable)
    {
        this.work = work ?? throw new ArgumentNullException(nameof(work));
        this.workPolicyRegistry = workPolicyRegistry;
        this.captiveLaborQuery = captiveLaborQuery;
        this.settlementStandings = settlementStandings;
        this.environment = environment
            ?? throw new ArgumentNullException(nameof(environment));
        this.findWarehouses = findWarehouses
            ?? throw new ArgumentNullException(nameof(findWarehouses));
        this.buildCandidate = buildCandidate
            ?? throw new ArgumentNullException(nameof(buildCandidate));
        this.isReachable = isReachable
            ?? throw new ArgumentNullException(nameof(isReachable));
    }

    public bool TryEvaluate(
        BuildableObject building,
        GridPathSearchResult searchResult,
        FacilityWorkType forcedWorkType,
        bool ignorePriority,
        out WorkTargetCandidate bestCandidate,
        out WorkTargetCandidate rejectedCandidate)
    {
        bestCandidate = WorkTargetCandidate.Invalid(
            building,
            "현재 작업할 수 없는 시설입니다",
            AIActionFailureKind.NoWork);
        rejectedCandidate = bestCandidate;
        WorkPriorityProfile priorities = work.WorkPriorities
            ?? WorkPriorityProfile.CreateDefault();

        if (building == null
            || building.isDestroy
            || !building.gameObject.activeInHierarchy)
        {
            return Reject(
                building,
                "시설이 없습니다",
                building != null
                    && (building.isDestroy
                        || !building.gameObject.activeInHierarchy)
                    ? AIActionFailureKind.Destroyed
                    : AIActionFailureKind.NoDestination,
                out bestCandidate,
                out rejectedCandidate);
        }

        if (building is not IWorkableFacility workable)
        {
            return Reject(
                building,
                "작업 가능한 시설이 아닙니다",
                AIActionFailureKind.Unsupported,
                out bestCandidate,
                out rejectedCandidate);
        }

        FacilityWorkType supportedTypes = ResolveSupportedTypes(building);
        if (supportedTypes == FacilityWorkType.None)
        {
            return Reject(
                building,
                "지원하는 작업이 없습니다",
                AIActionFailureKind.Unsupported,
                out bestCandidate,
                out rejectedCandidate);
        }

        IBuildingVisitorPort workerVisitor = work.WorkerActor?.BuildingVisitor;
        FacilityAssignmentStatus workerStatus = workable.GetWorkerAssignmentStatus(
            workerVisitor);
        if (!workerStatus.IsAllowed)
        {
            return Reject(
                building,
                workerStatus.Reason,
                workerStatus.FailureKind.ToAiActionFailureKind(),
                out bestCandidate,
                out rejectedCandidate);
        }

        if (building is ConstructionSite safetySite)
        {
            ConstructionSafetyResult safety = safetySite.GetConstructionSafetyState(
                workerVisitor,
                forced: ignorePriority);
            if (!safety.IsSafe)
            {
                return Reject(
                    building,
                    safety.Message,
                    safety.Reason == ConstructionSafetyReason.WorkerEscapeBlocked
                        || safety.Reason == ConstructionSafetyReason.EntranceBlocked
                            ? AIActionFailureKind.NoPath
                            : AIActionFailureKind.NoWork,
                    out bestCandidate,
                    out rejectedCandidate);
            }
        }

        if (searchResult != null && !isReachable(building, searchResult))
        {
            return Reject(
                building,
                "도달할 수 없는 대상입니다",
                AIActionFailureKind.NoPath,
                out bestCandidate,
                out rejectedCandidate);
        }

        AIActionFailure lastWorkTypeFailure = AIActionFailure.None;
        IReadOnlyList<WorkTypeDefinition> definitions = WorkTypeCatalog.All;
        for (int index = 0; index < definitions.Count; index++)
        {
            WorkTypeDefinition definition = definitions[index];
            FacilityWorkType legacyType = FacilityWorkTypeMap.GetRequired(definition);
            if ((supportedTypes & legacyType) == 0
                || forcedWorkType != FacilityWorkType.None
                    && legacyType != forcedWorkType
                || forcedWorkType == FacilityWorkType.None
                    && work.ShouldThrottleRoutineWork(definition.WorkTypeId))
            {
                continue;
            }

            FacilityWorkType workType = legacyType;
            WorkTypeId workTypeId = definition.WorkTypeId;
            if (workTypeId == BuiltInWorkTypeIds.Rest)
            {
                lastWorkTypeFailure = AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    "Rest is owned by AIRest and cannot run through AIWork.",
                    building);
                continue;
            }
            if (captiveLaborQuery != null
                && !captiveLaborQuery.IsWorkAllowed(
                    work.WorkerActor,
                    workTypeId,
                    out string captiveReason))
            {
                lastWorkTypeFailure = AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    captiveReason,
                    building);
                continue;
            }
            if (settlementStandings != null
                && !settlementStandings.IsWorkAllowed(
                    work.WorkerActor,
                    workTypeId,
                    out string standingReason))
            {
                lastWorkTypeFailure = AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    standingReason,
                    building);
                continue;
            }

            if (workType == FacilityWorkType.Restock
                && !CanRestock(building, searchResult, out lastWorkTypeFailure))
            {
                continue;
            }

            if (!ignorePriority
                && !CharacterAutonomousWorkPolicy.IsAllowed(
                    work.WorkerActor,
                    workTypeId,
                    out string identityFailureReason))
            {
                lastWorkTypeFailure = AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    identityFailureReason,
                    building);
                continue;
            }

            if (workPolicyRegistry != null
                && !workPolicyRegistry.IsAvailable(
                    workTypeId,
                    work.WorkerActor,
                    building,
                    out string policyFailureReason))
            {
                lastWorkTypeFailure = AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    string.IsNullOrWhiteSpace(policyFailureReason)
                        ? $"Work policy rejected '{workTypeId.Value}'."
                        : policyFailureReason,
                    building);
                continue;
            }

            if (WorkTargetExteriorRules.HasRuntime(building, workTypeId)
                && !WorkTargetExteriorRules.IsAvailable(
                    building,
                    work.WorkerActor,
                    workTypeId))
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

            FacilityAssignmentStatus workStatus = building is ConstructionSite site
                ? site.GetConstructionWorkStatus()
                : building.GetWorkAssignmentStatus(workTypeId);
            if (!workStatus.IsAllowed)
            {
                lastWorkTypeFailure = AIActionFailure.Create(
                    workStatus.FailureKind.ToAiActionFailureKind(),
                    workStatus.Reason,
                    building);
                continue;
            }

            if (!environment.CanStartEstimate(
                    building,
                    workTypeId,
                    ignorePriority,
                    out string environmentReason))
            {
                lastWorkTypeFailure = AIActionFailure.Create(
                    AIActionFailureKind.NoWork,
                    environmentReason,
                    building);
                continue;
            }

            WorkTargetCandidate candidate = buildCandidate(
                building,
                definition,
                priority,
                searchResult);
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
                lastWorkTypeFailure.HasFailure
                    ? lastWorkTypeFailure.ToString()
                    : reason,
                lastWorkTypeFailure.HasFailure
                    ? lastWorkTypeFailure.Kind
                    : AIActionFailureKind.NoWork);
            rejectedCandidate = bestCandidate;
            return false;
        }

        if (!environment.CanStartRoute(
                WorkTargetCandidateRuntimeAdapter.ResolveBuilding(
                    bestCandidate),
                bestCandidate.WorkTypeId,
                searchResult,
                ignorePriority,
                out string preciseReason))
        {
            bestCandidate = WorkTargetCandidate.Invalid(
                building,
                preciseReason,
                AIActionFailureKind.NoWork);
            rejectedCandidate = bestCandidate;
            return false;
        }

        rejectedCandidate = default;
        return true;
    }

    private bool CanRestock(
        BuildableObject building,
        GridPathSearchResult searchResult,
        out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        if (building is not IRestockableFacility restockable)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.Unsupported,
                "재고를 보충할 수 없는 시설입니다",
                building);
            return false;
        }

        if (!restockable.NeedsRestock)
        {
            return false;
        }

        IReadOnlyList<IWarehouseFacility> registeredWarehouses = searchResult == null
            ? work.WorkerActor?.WorldRegistry?.Warehouses
            : null;
        bool hasSupply;
        string supplyFailureReason;
        if (registeredWarehouses != null)
        {
            hasSupply = restockable.HasRestockSupply(
                registeredWarehouses,
                out supplyFailureReason);
        }
        else
        {
            hasSupply = restockable.HasRestockSupply(
                new List<IWarehouseFacility>(findWarehouses(searchResult)),
                out supplyFailureReason);
        }

        if (!hasSupply)
        {
            failure = AIActionFailure.Create(
                AIActionFailureKind.NoWork,
                supplyFailureReason,
                building);
        }

        return hasSupply;
    }

    private static FacilityWorkType ResolveSupportedTypes(BuildableObject building)
    {
        FacilityWorkType supported = building is ConstructionSite
            ? FacilityWorkType.Construct
            : building.Facility != null
                ? WildlifeButcherFacilityUtility.AddFallbackWorkTypes(
                    building,
                    building.Facility.supportedWorkTypes)
                : FacilityWorkType.None;
        supported = SurvivalFacilityUtility.AddFallbackWorkTypes(building, supported);
        supported = CombatEquipmentMaintenanceFacilityUtility.AddFallbackWorkTypes(
            building,
            supported);
        supported = FacilityEvolutionWorkUtility.AddFallbackWorkTypes(building, supported);
        return RuntimeWorkCapabilityUtility.AddFallbackWorkTypes(building, supported);
    }

    private static bool Reject(
        BuildableObject building,
        string reason,
        AIActionFailureKind failureKind,
        out WorkTargetCandidate candidate,
        out WorkTargetCandidate rejected)
    {
        candidate = WorkTargetCandidate.Invalid(building, reason, failureKind);
        rejected = candidate;
        return false;
    }
}
