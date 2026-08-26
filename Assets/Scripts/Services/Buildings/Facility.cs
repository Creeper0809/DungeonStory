using System;
using System.Collections;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class Facility : BuildableObject, IInteractable, IWorkableFacility, IWarehouseFacility
{
    private IBuildingVisitorPort worker;
    private CharacterId workerCharacterId;
    private WarehouseInventory warehouseInventory;
    private IRoomEnvironmentExperienceService roomEnvironmentExperienceService;
    private IMealConsumptionRuntime mealConsumptionRuntime;
    private IWaterFixtureUseRuntime waterFixtureUseRuntime;
    private IServiceSessionRuntime serviceSessionRuntime;
    private IServiceRoomLinkRuntime serviceRoomLinkRuntime;
    private IStockQuery stockQuery;
    private IStockCategoryDefinitionCatalog stockCategoryCatalog;

    private static void SetVisitOutcome(
        IBuildingVisitorPort actor,
        Facility facility,
        BuildingVisitOutcome outcome)
    {
        actor?.Shopping?.SetVisitOutcome(facility, outcome);
    }

    public WarehouseInventory Inventory => warehouseInventory;
    public IWarehouseInventoryPort InventoryPort => warehouseInventory;
    public bool HasWarehouseInventory => warehouseInventory != null;

    public bool HasMealAvailableFor(
        CharacterActor actor,
        out CharacterConsumablesFailure failure)
    {
        if (mealConsumptionRuntime == null)
        {
            failure = new CharacterConsumablesFailure(
                CharacterConsumablesFailureCode.InvalidCommand,
                "Meal consumption runtime is unavailable.");
            return false;
        }

        return mealConsumptionRuntime.HasMealAvailable(actor, this, out failure);
    }

    [Inject]
    public void ConstructFacility(
        IRoomEnvironmentExperienceService roomEnvironmentExperienceService,
        IStockQuery stockQuery,
        IMealConsumptionRuntime mealConsumptionRuntime,
        IWaterFixtureUseRuntime waterFixtureUseRuntime,
        IFluidWastewaterTransaction wastewaterNetworkRuntime,
        IServiceSessionRuntime serviceSessionRuntime,
        IServiceRoomLinkRuntime serviceRoomLinkRuntime,
        IStockCategoryDefinitionCatalog stockCategoryCatalog)
    {
        this.roomEnvironmentExperienceService = roomEnvironmentExperienceService;
        this.stockQuery = stockQuery ?? throw new System.ArgumentNullException(nameof(stockQuery));
        this.mealConsumptionRuntime = mealConsumptionRuntime;
        this.waterFixtureUseRuntime = waterFixtureUseRuntime;
        _ = wastewaterNetworkRuntime
            ?? throw new System.ArgumentNullException(
                nameof(wastewaterNetworkRuntime));
        this.serviceSessionRuntime = serviceSessionRuntime;
        this.serviceRoomLinkRuntime = serviceRoomLinkRuntime;
        this.stockCategoryCatalog = stockCategoryCatalog
            ?? throw new System.ArgumentNullException(nameof(stockCategoryCatalog));
    }

    public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)
    {
        base.Initialization(buildingSO, buildPos);

        int storageCapacity = this.GetStorageCapacity();
        if (storageCapacity > 0)
        {
            bool restrictCategory = !this.StoresAllCategories();
            warehouseInventory = new WarehouseInventory(
                storageCapacity,
                this.GetStorageMassCapacityGrams(),
                this.GetStorageCategory(),
                restrictCategory);
        }
        else
        {
            int internalStockCapacity = BuildingData.GetInternalStockCapacity();
            warehouseInventory = Facility != null
                && Facility.SupportsRole(FacilityRole.Logistics)
                && internalStockCapacity > 0
                    ? new WarehouseInventory(internalStockCapacity)
                    : null;
        }

        if (warehouseInventory != null)
        {
            warehouseInventory.BindPhysicalStock(
                stockQuery ?? throw new System.InvalidOperationException(
                    "Facility warehouse requires IStockQuery before initialization."),
                RequirePersistentInstanceId(),
                stockCategoryCatalog ?? throw new System.InvalidOperationException(
                    "Facility warehouse requires the authored stock-category catalog."));
            RegisterStateModule(new WarehouseInventoryStateModule(this));
        }
    }

    public IEnumerator Interact(IBuildingVisitorPort actor)
    {
        // Capture presentation text before the first yield. A coroutine can
        // resume while its facility is being demolished or its scene unloads;
        // accessing UnityEngine.Object.name after destruction throws.
        string interactionFacilityLabel = objectNameOrDefault();
        object currentAction = actor?.CurrentActionToken;
        if (!CanQueueVisit(actor, out string visitFailure))
        {
            BuildingInteractionFailureKind failureKind = this == null || isDestroy
                ? BuildingInteractionFailureKind.FacilityDestroyed
                : BuildingInteractionFailureKind.AdmissionRejected;
            SetVisitOutcome(actor, this, BuildingVisitOutcome.Failed);
            actor?.RecordActivity(this, new BuildingActivitySnapshot(
                BuildingActivityKinds.FacilityUse,
                BuildingActivityOutcomes.Failed,
                $"{interactionFacilityLabel} 이용 실패: {visitFailure}",
                reasonCode: visitFailure,
                bubbleEligible: true));
            actor?.ReportInteractionFailure(
                failureKind,
                $"{interactionFacilityLabel}: {visitFailure}",
                this);
            yield break;
        }

        if (!CanVisit(actor, out _))
        {
            yield return WaitForVisitAdmission(
                actor,
                currentAction,
                interactionFacilityLabel);
            if (actor == null
                || !actor.IsCurrentAction(currentAction)
                || actor.IsCurrentActionEnded)
            {
                AbortInteraction(
                    actor,
                    interactionFacilityLabel,
                    serviceSession: null,
                    useStarted: false,
                    BuildingInteractionFailureKind.ActionReplaced,
                    "action-replaced-while-waiting-for-facility");
                yield break;
            }

            if (!CanVisit(actor, out visitFailure))
            {
                BuildingInteractionFailureKind failureKind = this == null || isDestroy
                    ? BuildingInteractionFailureKind.FacilityDestroyed
                    : BuildingInteractionFailureKind.AdmissionRejected;
                SetVisitOutcome(actor, this, BuildingVisitOutcome.Abandoned);
                actor.RecordActivity(this, new BuildingActivitySnapshot(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Cancelled,
                    $"{interactionFacilityLabel} 대기 종료: {visitFailure}",
                    actionId: "facility:queue",
                    reasonCode: "queue-not-admitted"));
                actor.ReportInteractionFailure(
                    failureKind,
                    $"{interactionFacilityLabel}: {visitFailure}",
                    this);
                yield break;
            }
        }

        ServiceSessionSnapshot serviceSession = null;
        BuildingServiceHubAbility serviceHub = this.GetServiceHubAbility();
        if (serviceHub != null
            && serviceSessionRuntime != null
            && !serviceSessionRuntime.TryBeginSession(
                new ServiceSessionRequest
                {
                    Hub = this,
                    Actor = actor,
                    ProcessId = serviceHub.supportedProcessIds?
                        .FirstOrDefault() ?? string.Empty,
                    IsInternalActor = Shop.IsInternalStaffUse(actor),
                    AdvertisedDemand = !Shop.IsInternalStaffUse(actor)
                },
                out serviceSession,
                out DomainFailure serviceFailure))
        {
            string serviceFailureCode = serviceFailure.Code.ToString();
            actor?.RecordActivity(this, new BuildingActivitySnapshot(
                BuildingActivityKinds.FacilityUse,
                BuildingActivityOutcomes.Failed,
                $"{interactionFacilityLabel} 이용 실패",
                reasonCode: serviceFailureCode,
                bubbleEligible: true));
            actor?.ReportInteractionFailure(
                BuildingInteractionFailureKind.ServiceUnavailable,
                $"{interactionFacilityLabel}: {serviceFailureCode}",
                this);
            yield break;
        }

        WaterFixtureUseTicket fixtureUseTicket = default;
        BuildingWaterFixtureAbility waterFixture =
            BuildingData?.GetAbility<BuildingWaterFixtureAbility>();
        if (!TryBeginUse(actor, out string failureReason))
        {
            if (serviceSession != null)
            {
                serviceSessionRuntime.CancelSession(
                    serviceSession.SessionId,
                    failureReason);
            }
            actor?.RecordActivity(this, new BuildingActivitySnapshot(
                BuildingActivityKinds.FacilityUse,
                BuildingActivityOutcomes.Failed,
                $"{interactionFacilityLabel} 이용 실패: {failureReason}",
                reasonCode: failureReason,
                bubbleEligible: true));
            actor?.ReportInteractionFailure(
                BuildingInteractionFailureKind.AdmissionRejected,
                $"{interactionFacilityLabel}: {failureReason}",
                this);
            yield break;
        }

        if (actor == null || !actor.VisitorSnapshot.CanMove)
        {
            if (serviceSession != null)
            {
                serviceSessionRuntime.CancelSession(
                    serviceSession.SessionId,
                    "이동 능력이 없어 서비스를 시작할 수 없습니다.");
            }
            EndUse(actor);
            actor?.ReportInteractionFailure(
                BuildingInteractionFailureKind.ActorUnavailable,
                $"{interactionFacilityLabel}: actor-cannot-move",
                this);
            yield break;
        }

        Vector3 usePosition = GetFacilityAnchorWorldPosition(
            FacilityAnchorPurposeIds.Use,
            actor.VisitorSnapshot.Position);
        actor.SetActionPhase("\uC2DC\uC124 \uC811\uADFC", this);
        yield return actor.MoveTo(usePosition, 0.7f, currentAction);
        if (!TryContinueInteraction(
                actor,
                currentAction,
                out BuildingInteractionFailureKind interactionFailure,
                out string interactionFailureDetail))
        {
            AbortInteraction(
                actor,
                interactionFacilityLabel,
                serviceSession,
                useStarted: true,
                interactionFailure,
                interactionFailureDetail);
            yield break;
        }
        actor.SetActionPhase("\uC790\uB9AC \uC7A1\uAE30", this);
        yield return Linger(actor, 0.12f, currentAction);
        if (!TryContinueInteraction(
                actor,
                currentAction,
                out interactionFailure,
                out interactionFailureDetail))
        {
            AbortInteraction(
                actor,
                interactionFacilityLabel,
                serviceSession,
                useStarted: true,
                interactionFailure,
                interactionFailureDetail);
            yield break;
        }
        if (serviceSession != null
            && serviceSession.Contract != null)
        {
            if ((serviceSession.Contract.activeStages
                    & ServiceProcessStageMask.Reception) != 0)
            {
                serviceSessionRuntime.TrySetStage(
                    serviceSession.SessionId,
                    ServiceSessionStage.Reception,
                    out _);
                actor.SetActionPhase("서비스 접수", this);
                yield return Linger(
                    actor,
                    serviceSession.Contract.receptionSeconds,
                    currentAction);
                if (!TryContinueInteraction(
                        actor,
                        currentAction,
                        out interactionFailure,
                        out interactionFailureDetail))
                {
                    AbortInteraction(
                        actor,
                        interactionFacilityLabel,
                        serviceSession,
                        useStarted: true,
                        interactionFailure,
                        interactionFailureDetail);
                    yield break;
                }
            }
            if ((serviceSession.Contract.activeStages
                    & ServiceProcessStageMask.Waiting) != 0)
            {
                serviceSessionRuntime.TrySetStage(
                    serviceSession.SessionId,
                    ServiceSessionStage.Waiting,
                    out _);
                actor.SetActionPhase("서비스 대기", this);
                yield return Linger(
                    actor,
                    serviceSession.Contract.waitingSeconds,
                    currentAction);
                if (!TryContinueInteraction(
                        actor,
                        currentAction,
                        out interactionFailure,
                        out interactionFailureDetail))
                {
                    AbortInteraction(
                        actor,
                        interactionFacilityLabel,
                        serviceSession,
                        useStarted: true,
                        interactionFailure,
                        interactionFailureDetail);
                    yield break;
                }
            }
        }
        if (serviceHub?.serviceCategory == ServiceCategory.Dining
            && serviceRoomLinkRuntime != null
            && serviceRoomLinkRuntime.TryResolveFeature(
                this,
                "service:seat",
                out BuildableObject seat,
                out _))
        {
            actor.SetActionPhase("좌석으로 이동", seat);
            yield return actor.MoveTo(
                seat.GetFacilityAnchorWorldPosition(
                    FacilityAnchorPurposeIds.Use,
                    actor.VisitorSnapshot.Position),
                0.7f,
                currentAction);
            if (!TryContinueInteraction(
                    actor,
                    currentAction,
                    out interactionFailure,
                    out interactionFailureDetail))
            {
                AbortInteraction(
                    actor,
                    interactionFacilityLabel,
                    serviceSession,
                    useStarted: true,
                    interactionFailure,
                    interactionFailureDetail);
                yield break;
            }
        }

        BuildingRecreationalSubstanceServiceAbility recreation =
            BuildingData?.GetAbility<BuildingRecreationalSubstanceServiceAbility>();
        bool usesPhysicalMealAction = recreation?.IsValid != true
            && Facility != null
            && Facility.SupportsRole(FacilityRole.Meal);
        float duration = serviceSession?.Contract?.serviceSeconds > 0f
            ? serviceSession.Contract.serviceSeconds
            : Facility != null
                ? Facility.useDuration
                : 1f;
        duration *= actor.VisitorSnapshot.StayDurationMultiplier;

        actor.SetActionPhase("\uC2DC\uC124 \uC774\uC6A9", this, $"{duration:0.#}s");
        if (serviceSession != null)
        {
            serviceSessionRuntime.TrySetStage(
                serviceSession.SessionId,
                ServiceSessionStage.Service,
                out _);
        }
        if (duration > 0f && !usesPhysicalMealAction)
        {
            yield return Linger(actor, duration, currentAction);
            if (!TryContinueInteraction(
                    actor,
                    currentAction,
                    out interactionFailure,
                    out interactionFailureDetail))
            {
                AbortInteraction(
                    actor,
                    interactionFacilityLabel,
                    serviceSession,
                    useStarted: true,
                    interactionFailure,
                    interactionFailureDetail);
                yield break;
            }
        }

        // Water supply and wastewater fallback are one atomic authority in
        // IWaterFixtureUseRuntime.  In particular, dry/manual fixtures can
        // legitimately emit sewage or a physical waste item when no pipe is
        // available; a separate drain precheck here would reject those authored
        // fallback modes before the runtime can issue its use ticket.
        if (waterFixtureUseRuntime != null
            && waterFixture != null
            && !waterFixtureUseRuntime.TryBeginUse(
                this,
                actor?.BuildingCharacterId ?? default,
                out fixtureUseTicket,
                out DomainFailure plumbingFailure))
        {
            AbortForResourceFailure(
                actor,
                interactionFacilityLabel,
                serviceSession,
                plumbingFailure.Code.ToString());
            yield break;
        }

        BuildingMealUseSnapshot mealResult = default;
        if (recreation?.IsValid == true)
        {
            if (!actor.TryConsumeRecreationalSubstance(
                    this,
                    out BuildingRecreationalSubstanceUseSnapshot substanceResult))
            {
                string failureCode = string.IsNullOrWhiteSpace(substanceResult.FailureCode)
                    ? CharacterConsumablesFailureCode.InvalidCommand.ToString()
                    : substanceResult.FailureCode;
                if (serviceSession != null)
                {
                    serviceSessionRuntime.CancelSession(
                        serviceSession.SessionId,
                        failureCode);
                }
                actor.RecordActivity(this, new BuildingActivitySnapshot(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Failed,
                    failureCode,
                    reasonCode: failureCode,
                    bubbleEligible: true));
                EndUse(actor);
                actor.ReportInteractionFailure(
                    BuildingInteractionFailureKind.ConsumptionFailed,
                    $"{interactionFacilityLabel}: {failureCode}",
                    this);
                yield break;
            }

            ApplyRecovery(actor, 0f, 0f, recreation.funRecovery, 0f, 0f, 0f);
            float sentiment = recreation.facilitySentiment;
            string socialDetail = $"{substanceResult.DisplayName}을(를) 즐김";
            actor.RememberFacilityExperience(this, sentiment, socialDetail);
            actor.RecordActivity(this, new BuildingActivitySnapshot(
                BuildingActivityKinds.Social,
                BuildingActivityOutcomes.Completed,
                socialDetail,
                actionId: "social:recreational-drink",
                reasonCode: substanceResult.Overdosed
                    ? "recreational-drink-overdose"
                    : substanceResult.BecameAddicted
                        ? "recreational-drink-addiction"
                        : "recreational-drink",
                value: recreation.funRecovery,
                sentiment: substanceResult.Overdosed ? -0.5f : sentiment,
                bubbleEligible: true));
        }
        else if (Facility != null && Facility.SupportsRole(FacilityRole.Meal))
        {
            bool mealConsumed = mealConsumptionRuntime != null
                && actor.TryConsumeMeal(
                    mealConsumptionRuntime,
                    this,
                    out mealResult);
            if (!mealConsumed && mealResult.AcceptedPending)
            {
                actor.SetActionPhase("식사 중", this, "4s");
                float deadline = Time.realtimeSinceStartup + 15f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    if (!TryContinueInteraction(
                            actor,
                            currentAction,
                            out interactionFailure,
                            out interactionFailureDetail))
                    {
                        AbortInteraction(
                            actor,
                            interactionFacilityLabel,
                            serviceSession,
                            useStarted: true,
                            interactionFailure,
                            interactionFailureDetail);
                        yield break;
                    }
                    if (!actor.TryGetMealConsumptionResult(
                            mealConsumptionRuntime,
                            mealResult.OperationId,
                            out BuildingMealUseSnapshot currentMeal))
                    {
                        mealResult = currentMeal;
                        break;
                    }
                    mealResult = currentMeal;
                    if (mealResult.Success)
                    {
                        mealConsumed = true;
                        break;
                    }
                    if (!mealResult.AcceptedPending)
                    {
                        break;
                    }
                }
                if (!mealConsumed && mealResult.AcceptedPending)
                {
                    mealResult = new BuildingMealUseSnapshot(
                        false,
                        CharacterConsumablesFailureCode.PhysicalConsumptionFailed.ToString(),
                        string.Empty,
                        0,
                        failureDetail: "meal-action-timeout");
                }
            }
            if (!mealConsumed)
            {
                if (mealResult.IsRetryableUnavailable)
                {
                    const string cancellationReason = "meal-supply-retry";
                    if (serviceSession != null)
                    {
                        serviceSessionRuntime.CancelSession(
                            serviceSession.SessionId,
                            cancellationReason);
                    }
                    actor.RecordActivity(this, new BuildingActivitySnapshot(
                        BuildingActivityKinds.FacilityUse,
                        BuildingActivityOutcomes.Cancelled,
                        $"{interactionFacilityLabel} meal supply changed before commit; retrying selection.",
                        reasonCode: cancellationReason));
                    EndUse(actor);
                    SetVisitOutcome(actor, this, BuildingVisitOutcome.Abandoned);
                    yield break;
                }

                if (mealResult.IsNoLongerNeeded)
                {
                    const string cancellationReason = "meal-no-longer-needed";
                    if (serviceSession != null)
                    {
                        serviceSessionRuntime.CancelSession(
                            serviceSession.SessionId,
                            cancellationReason);
                    }
                    actor.RecordActivity(this, new BuildingActivitySnapshot(
                        BuildingActivityKinds.FacilityUse,
                        BuildingActivityOutcomes.Cancelled,
                        $"{interactionFacilityLabel} meal plan retired because the need was already satisfied.",
                        reasonCode: cancellationReason));
                    EndUse(actor);
                    SetVisitOutcome(actor, this, BuildingVisitOutcome.Abandoned);
                    yield break;
                }

                string failureCode = mealConsumptionRuntime == null
                    ? "InvalidCommand"
                    : string.IsNullOrWhiteSpace(mealResult.FailureCode)
                        ? CharacterConsumablesFailureCode.PhysicalConsumptionFailed.ToString()
                        : mealResult.FailureCode;
                if (serviceSession != null)
                {
                    serviceSessionRuntime.CancelSession(
                        serviceSession.SessionId,
                        failureCode);
                }
                actor.RecordActivity(this, new BuildingActivitySnapshot(
                    BuildingActivityKinds.FacilityUse,
                    BuildingActivityOutcomes.Failed,
                    string.IsNullOrWhiteSpace(mealResult.FailureDetail)
                        ? failureCode
                        : $"{failureCode}:{mealResult.FailureDetail}",
                    reasonCode: failureCode,
                    bubbleEligible: true));
                EndUse(actor);
                actor.ReportInteractionFailure(
                    BuildingInteractionFailureKind.ConsumptionFailed,
                    string.IsNullOrWhiteSpace(mealResult.FailureDetail)
                        ? $"{interactionFacilityLabel}: {failureCode}"
                        : $"{interactionFacilityLabel}: {failureCode}:{mealResult.FailureDetail}",
                    this);
                yield break;
            }
        }
        else
        {
            ApplyConfiguredUseRecovery(actor);
        }

        waterFixtureUseRuntime?.CompleteUse(this, fixtureUseTicket);
        actor.ApplyFacilityUseCompleted(this);
        actor.ApplyRoomExperience(roomEnvironmentExperienceService, this, "facility-use");
        actor.SetActionPhase("\uC774\uC6A9 \uC815\uB9AC", this);
        if (serviceSession?.Contract != null
            && (serviceSession.Contract.activeStages
                & ServiceProcessStageMask.Payment) != 0)
        {
            serviceSessionRuntime.TrySetStage(
                serviceSession.SessionId,
                ServiceSessionStage.Payment,
                out _);
            actor.SetActionPhase("서비스 결제", this);
            yield return Linger(
                actor,
                serviceSession.Contract.paymentSeconds,
                currentAction);
            if (!TryContinueInteraction(
                    actor,
                    currentAction,
                    out interactionFailure,
                    out interactionFailureDetail))
            {
                AbortInteraction(
                    actor,
                    interactionFacilityLabel,
                    serviceSession,
                    useStarted: true,
                    interactionFailure,
                    interactionFailureDetail);
                yield break;
            }
        }
        if (serviceSession?.Contract != null
            && (serviceSession.Contract.activeStages
                & ServiceProcessStageMask.Cleanup) != 0)
        {
            serviceSessionRuntime.TrySetStage(
                serviceSession.SessionId,
                ServiceSessionStage.Cleanup,
                out _);
            actor.SetActionPhase("서비스 정리", this);
            yield return Linger(
                actor,
                serviceSession.Contract.cleanupSeconds,
                currentAction);
            if (!TryContinueInteraction(
                    actor,
                    currentAction,
                    out interactionFailure,
                    out interactionFailureDetail))
            {
                AbortInteraction(
                    actor,
                    interactionFacilityLabel,
                    serviceSession,
                    useStarted: true,
                    interactionFailure,
                    interactionFailureDetail);
                yield break;
            }
        }
        else
        {
            yield return Linger(actor, 0.12f, currentAction);
            if (!TryContinueInteraction(
                    actor,
                    currentAction,
                    out interactionFailure,
                    out interactionFailureDetail))
            {
                AbortInteraction(
                    actor,
                    interactionFacilityLabel,
                    serviceSession,
                    useStarted: true,
                    interactionFailure,
                    interactionFailureDetail);
                yield break;
            }
        }
        actor.RecordActivity(this, new BuildingActivitySnapshot(
            BuildingActivityKinds.FacilityUse,
            BuildingActivityOutcomes.Completed,
            mealResult.Success
                ? $"{mealResult.DisplayName} 식사 완료"
                : $"{interactionFacilityLabel} 이용 완료"));
        SetVisitOutcome(actor, this, BuildingVisitOutcome.Completed);
        if (serviceSession != null
            && !serviceSessionRuntime.TryCompleteSession(
                serviceSession.SessionId,
                out _,
                out DomainFailure completionFailure))
        {
            serviceSessionRuntime.CancelSession(
                serviceSession.SessionId,
                completionFailure.Code.ToString());
        }
        if (!CompleteUse(actor))
        {
            throw new InvalidOperationException(
                $"Facility completion lost its active occupancy: "
                + $"facility={RequirePersistentInstanceId().Value}; "
                + $"actor={actor?.BuildingCharacterId.Value ?? "<missing>"}.");
        }
    }

    public override void ReleaseTransientCharacterOwnership(
        IBuildingVisitorPort actor,
        string reason)
    {
        if (actor == null)
        {
            return;
        }

        string actorId = actor.BuildingCharacterId.Value;
        string hubId = PersistentInstanceId.IsValid
            ? PersistentInstanceId.Value
            : string.Empty;
        string[] sessions = serviceSessionRuntime?.ActiveSessions?
            .Where(session => session != null
                && session.IsActive
                && string.Equals(session.ActorId, actorId, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(hubId)
                    || string.Equals(session.HubId, hubId, StringComparison.Ordinal)))
            .Select(session => session.SessionId)
            .ToArray() ?? Array.Empty<string>();

        base.ReleaseTransientCharacterOwnership(actor, reason);
        for (int index = 0; index < sessions.Length; index++)
        {
            serviceSessionRuntime.CancelSession(
                sessions[index],
                string.IsNullOrWhiteSpace(reason)
                    ? "character-lifecycle-ended"
                    : reason);
        }

        // CharacterActor owns actor-wide meal cancellation once per lifecycle
        // transition.  A facility only releases ownership scoped to itself.
    }

    public FacilityAssignmentStatus GetWorkerAssignmentStatus(IBuildingVisitorPort actor)
    {
        PruneInvalidWorker();
        FacilityAssignmentStatus workStatus = FacilityAssignmentStatus.Rejected(
            FacilityAssignmentFailureKind.UnsupportedWork,
            "지원하지 않는 작업");
        FacilityWorkType supported =
            FacilityEvolutionWorkUtility.AddFallbackWorkTypes(
                this,
                Facility != null
                    ? Facility.supportedWorkTypes
                    : FacilityWorkType.None);
        supported = RuntimeWorkCapabilityUtility.AddFallbackWorkTypes(
            this,
            supported);
        foreach (WorkTypeDefinition definition in FacilityWorkTypeMap.Enumerate(
                     supported))
        {
            workStatus = GetWorkAssignmentStatus(definition.WorkTypeId);
            if (workStatus.IsAllowed)
            {
                break;
            }
        }

        if (!workStatus.IsAllowed)
        {
            return workStatus;
        }

        if (worker != null && worker != actor)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Occupied,
                "이미 근무자가 있음");
        }

        if (HasWorkerReservationForOther(actor))
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Reserved,
                "이미 작업 예약됨");
        }

        return FacilityAssignmentStatus.Allowed();
    }

    public bool CanAssignWorker(IBuildingVisitorPort actor, out string failureReason)
    {
        FacilityAssignmentStatus status = GetWorkerAssignmentStatus(actor);
        failureReason = status.Reason;
        return status.IsAllowed;
    }

    public IEnumerator AllocateWorker(IBuildingVisitorPort actor)
    {
        PruneInvalidWorker();
        if (!CanAssignWorker(actor, out _))
        {
            yield break;
        }

        worker = actor;
        workerCharacterId = actor?.BuildingCharacterId ?? default;
        TrackAllocatedWorkerOwnership(actor);
        ReleaseWorkerReservation(actor);
        if (actor == null || !actor.VisitorSnapshot.CanMove) yield break;

        object currentAction = actor.CurrentActionToken;
        Vector3 workPosition = GetFacilityAnchorWorldPosition(
            FacilityAnchorPurposeIds.Work,
            actor.VisitorSnapshot.Position);
        actor.SetActionPhase("\uC791\uC5C5\uB300 \uC811\uADFC", this);
        yield return actor.MoveTo(workPosition, 1f, currentAction);
        actor.ChangeLayer("DungeonMiddleObject");
        yield return actor.MoveTo(
            workPosition + new Vector3(0f, 0.15f),
            3f,
            currentAction);
        actor.SetActionPhase("\uC791\uC5C5 \uC790\uC138", this);
        actor.FaceRight();
    }

    public void DeallocateWorker(IBuildingVisitorPort actor)
    {
        if (actor == null)
        {
            return;
        }

        PruneInvalidWorker();
        if (worker != actor) return;

        worker = null;
        workerCharacterId = default;
        UntrackAllocatedWorkerOwnership(actor);
        actor.SetActionPhase("\uC2DC\uC124 \uD1F4\uC7A5", this);
        Vector3 actorPosition = actor.VisitorSnapshot.Position - new Vector3(0f, 0.15f);
        actor.SetWorldPosition(actorPosition);
        Vector2Int actorGridPosition = grid != null
            ? grid.GetXY(actorPosition)
            : centerPos;
        if (!ContainsGridPosition(actorGridPosition)
            && TryGetFacilityOccupiedWorldPosition(actorPosition, out Vector3 exitPosition))
        {
            actor.SetWorldPosition(exitPosition);
        }
        actor.ChangeLayer("Default");
    }

    private IEnumerator Linger(IBuildingVisitorPort actor, float seconds, object expectedAction)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        float timer = 0f;
        while (timer < seconds)
        {
            if (expectedAction != null
                && (actor == null
                    || !actor.IsCurrentAction(expectedAction)
                    || actor.IsCurrentActionEnded))
            {
                yield break;
            }

            actor?.NotifyFacilityServiceHeartbeat();
            timer += GameDeltaTime;
            yield return null;
        }
    }

    private bool TryContinueInteraction(
        IBuildingVisitorPort actor,
        object expectedAction,
        out BuildingInteractionFailureKind failureKind,
        out string failureDetail)
    {
        if (this == null || isDestroy)
        {
            failureKind = BuildingInteractionFailureKind.FacilityDestroyed;
            failureDetail = "facility-destroyed-during-interaction";
            return false;
        }

        if (actor == null)
        {
            failureKind = BuildingInteractionFailureKind.ActorUnavailable;
            failureDetail = "actor-missing-during-interaction";
            return false;
        }

        try
        {
            if (!actor.VisitorSnapshot.IsRuntimeActive)
            {
                failureKind = BuildingInteractionFailureKind.ActorUnavailable;
                failureDetail = "actor-inactive-during-interaction";
                return false;
            }
        }
        catch (MissingReferenceException)
        {
            failureKind = BuildingInteractionFailureKind.ActorUnavailable;
            failureDetail = "actor-destroyed-during-interaction";
            return false;
        }

        if (expectedAction != null
            && (!actor.IsCurrentAction(expectedAction)
                || actor.IsCurrentActionEnded))
        {
            failureKind = BuildingInteractionFailureKind.ActionReplaced;
            failureDetail = "action-replaced-during-interaction";
            return false;
        }

        failureKind = BuildingInteractionFailureKind.None;
        failureDetail = string.Empty;
        return true;
    }

    private void AbortInteraction(
        IBuildingVisitorPort actor,
        string facilityLabel,
        ServiceSessionSnapshot serviceSession,
        bool useStarted,
        BuildingInteractionFailureKind failureKind,
        string failureDetail)
    {
        string detail = string.IsNullOrWhiteSpace(failureDetail)
            ? failureKind.ToString()
            : failureDetail;
        if (serviceSession != null)
        {
            serviceSessionRuntime?.CancelSession(serviceSession.SessionId, detail);
        }

        bool facilityAlive = this != null && !isDestroy;
        if (actor != null)
        {
            if (facilityAlive && useStarted)
            {
                EndUse(actor);
            }
            else if (facilityAlive)
            {
                ReleaseVisitReservation(actor);
            }

            SetVisitOutcome(
                actor,
                facilityAlive ? this : null,
                BuildingVisitOutcome.Abandoned);
            actor.RecordActivity(facilityAlive ? this : null, new BuildingActivitySnapshot(
                BuildingActivityKinds.FacilityUse,
                BuildingActivityOutcomes.Cancelled,
                $"{facilityLabel} interaction aborted: {detail}",
                actionId: "facility:interaction",
                reasonCode: detail));
        }

        if (failureKind == BuildingInteractionFailureKind.ActionReplaced)
        {
            actor?.ReportInteractionCancellation(
                failureKind,
                $"{facilityLabel}: {detail}",
                facilityAlive ? this : null);
        }
        else
        {
            actor?.ReportInteractionFailure(
                failureKind,
                $"{facilityLabel}: {detail}",
                facilityAlive ? this : null);
        }
    }

    private void AbortForResourceFailure(
        IBuildingVisitorPort actor,
        string facilityLabel,
        ServiceSessionSnapshot serviceSession,
        string failureCode)
    {
        string code = string.IsNullOrWhiteSpace(failureCode)
            ? "facility-resource-unavailable"
            : failureCode;
        if (serviceSession != null)
        {
            serviceSessionRuntime?.CancelSession(serviceSession.SessionId, code);
        }
        if (actor != null)
        {
            EndUse(actor);
            SetVisitOutcome(actor, this, BuildingVisitOutcome.Failed);
            actor.RecordActivity(this, new BuildingActivitySnapshot(
                BuildingActivityKinds.FacilityUse,
                BuildingActivityOutcomes.Failed,
                $"{facilityLabel} use failed: {code}",
                reasonCode: code,
                bubbleEligible: true));
            actor.ReportInteractionFailure(
                BuildingInteractionFailureKind.ResourceUnavailable,
                $"{facilityLabel}: {code}",
                this);
        }
    }

    private void PruneInvalidWorker()
    {
        if (worker == null)
        {
            return;
        }

        try
        {
            if (!worker.VisitorSnapshot.IsRuntimeActive)
            {
                UntrackTransientOwnership(
                    workerCharacterId,
                    BuildingTransientOwnershipKind.AllocatedWorker);
                worker = null;
                workerCharacterId = default;
            }
        }
        catch (MissingReferenceException)
        {
            UntrackTransientOwnership(
                workerCharacterId,
                BuildingTransientOwnershipKind.AllocatedWorker);
            worker = null;
            workerCharacterId = default;
        }
    }

    private Vector2 GetWorkerPosition()
    {
        if (grid == null || buildPoses == null || buildPoses.Count == 0)
        {
            return transform.position;
        }

        float endX = buildPoses.Max((pos) => pos.x) - 0.2f;
        return grid.GetWorldPos(new Vector2(endX, centerPos.y));
    }

    public void ApplyConfiguredUseRecovery(IBuildingVisitorPort actor)
    {
        if (actor == null || Facility == null)
        {
            return;
        }

        FacilityNeedRecoveryData configured = BuildingData != null
            ? BuildingData.GetNeedRecovery()
            : default;
        float sleep = configured.sleep;
        float mood = configured.mood;
        float fun = configured.fun;
        float hunger = configured.hunger;
        float excretion = configured.excretion;
        float hygiene = configured.hygiene;

        if (configured.HasEffect)
        {
            ApplyRecovery(actor, sleep, mood, fun, hunger, excretion, hygiene);
            return;
        }

        if (Facility.SupportsRole(FacilityRole.Rest))
        {
            sleep += 35f;
            mood += 12f;
        }

        if (Facility.SupportsRole(FacilityRole.Training))
        {
            fun += 15f;
            mood += 5f;
        }

        if (Facility.SupportsRole(FacilityRole.Research))
        {
            fun += 10f;
            mood += 8f;
        }

        if (Facility.SupportsRole(FacilityRole.Mana))
        {
            mood += 10f;
        }

        if (Facility.SupportsRole(FacilityRole.Logistics))
        {
            mood += 3f;
        }

        if (Facility.SupportsRole(FacilityRole.Meal))
        {
            hunger += 35f;
            mood += 5f;
        }

        if (Facility.SupportsRole(FacilityRole.Toilet))
        {
            excretion += 70f;
            mood += 2f;
        }

        if (Facility.SupportsRole(FacilityRole.Hygiene))
        {
            hygiene += 60f;
            mood += 4f;
        }

        ApplyRecovery(actor, sleep, mood, fun, hunger, excretion, hygiene);
    }

    private void ApplyRecovery(
        IBuildingVisitorPort actor,
        float sleep,
        float mood,
        float fun,
        float hunger,
        float excretion,
        float hygiene)
    {
        if (sleep == 0f
            && mood == 0f
            && fun == 0f
            && hunger == 0f
            && excretion == 0f
            && hygiene == 0f)
        {
            return;
        }

        actor.ApplyNeedRecovery(new BuildingNeedRecoverySnapshot(
            sleep,
            mood,
            fun,
            hunger,
            excretion,
            hygiene,
            $"facility:{RequirePersistentInstanceId().Value}",
            roomEnvironmentExperienceService?.GetActiveConditionIds(this),
            $"{objectNameOrDefault()} 이용"));
    }

    private string objectNameOrDefault()
    {
        return BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.objectName)
            ? BuildingData.objectName
            : name;
    }
}
