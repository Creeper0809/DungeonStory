using System.Collections;
using System.Linq;
using UnityEngine;
using VContainer;

public class Facility : BuildableObject, IInteractable, IWorkableFacility, IWarehouseFacility
{
    private CharacterActor worker;
    private WarehouseInventory warehouseInventory;
    private IRoomEnvironmentExperienceService roomEnvironmentExperienceService;
    private IMealConsumptionRuntime mealConsumptionRuntime;
    private IWaterFixtureUseRuntime waterFixtureUseRuntime;
    private IWastewaterNetworkRuntime wastewaterNetworkRuntime;
    private IServiceSessionRuntime serviceSessionRuntime;
    private IServiceRoomLinkRuntime serviceRoomLinkRuntime;

    public WarehouseInventory Inventory => warehouseInventory;
    public bool HasWarehouseInventory => warehouseInventory != null;

    [Inject]
    public void ConstructFacility(
        IRoomEnvironmentExperienceService roomEnvironmentExperienceService,
        IMealConsumptionRuntime mealConsumptionRuntime = null,
        IWaterFixtureUseRuntime waterFixtureUseRuntime = null,
        IWastewaterNetworkRuntime wastewaterNetworkRuntime = null,
        IServiceSessionRuntime serviceSessionRuntime = null,
        IServiceRoomLinkRuntime serviceRoomLinkRuntime = null)
    {
        this.roomEnvironmentExperienceService = roomEnvironmentExperienceService;
        this.mealConsumptionRuntime = mealConsumptionRuntime;
        this.waterFixtureUseRuntime = waterFixtureUseRuntime;
        this.wastewaterNetworkRuntime = wastewaterNetworkRuntime;
        this.serviceSessionRuntime = serviceSessionRuntime;
        this.serviceRoomLinkRuntime = serviceRoomLinkRuntime;
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
                this.GetStorageCategory(),
                restrictCategory);
            if (this.StoresAllCategories())
            {
                warehouseInventory.ApplySnapshot(
                    WarehouseInventory.CreateSeeded(storageCapacity).CreateSnapshot());
            }
        }
        else
        {
            int internalStockCapacity = BuildingData.GetInternalStockCapacity();
            warehouseInventory = Facility != null
                && Facility.SupportsRole(FacilityRole.Logistics)
                && internalStockCapacity > 0
                    ? WarehouseInventory.CreateSeeded(internalStockCapacity)
                    : null;
        }

        if (warehouseInventory != null)
        {
            RegisterStateModule(new WarehouseInventoryStateModule(this));
        }
    }

    public IEnumerator Interact(CharacterActor actor)
    {
        if (!CanVisit(actor, out string visitFailure))
        {
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.FacilityUse,
                CharacterActivityOutcomes.Failed,
                $"{objectNameOrDefault()} 이용 실패: {visitFailure}",
                this,
                reasonCode: visitFailure,
                bubbleEligible: true));
            yield break;
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
                out string serviceFailure))
        {
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.FacilityUse,
                CharacterActivityOutcomes.Failed,
                $"{objectNameOrDefault()} 이용 실패: {serviceFailure}",
                this,
                reasonCode: serviceFailure,
                bubbleEligible: true));
            yield break;
        }

        WaterFixtureUseTicket fixtureUseTicket = default;
        BuildingWaterFixtureAbility waterFixture =
            BuildingData?.GetAbility<BuildingWaterFixtureAbility>();
        if (waterFixture != null
            && wastewaterNetworkRuntime != null
            && waterFixture.wastewaterPerUse > 0f
            && !wastewaterNetworkRuntime.CanAcceptWastewater(
                this,
                waterFixture.wastewaterPerUse,
                out string drainFailure))
        {
            if (serviceSession != null)
            {
                serviceSessionRuntime.CancelSession(
                    serviceSession.SessionId,
                    drainFailure);
            }
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.FacilityUse,
                CharacterActivityOutcomes.Failed,
                $"{objectNameOrDefault()} 이용 실패: {drainFailure}",
                this,
                reasonCode: drainFailure,
                bubbleEligible: true));
            yield break;
        }
        if (waterFixtureUseRuntime != null
            && waterFixture != null
            && !waterFixtureUseRuntime.TryBeginUse(
                this,
                out fixtureUseTicket,
                out string plumbingFailure))
        {
            if (serviceSession != null)
            {
                serviceSessionRuntime.CancelSession(
                    serviceSession.SessionId,
                    plumbingFailure);
            }
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.FacilityUse,
                CharacterActivityOutcomes.Failed,
                $"{objectNameOrDefault()} 이용 실패: {plumbingFailure}",
                this,
                reasonCode: plumbingFailure,
                bubbleEligible: true));
            yield break;
        }

        if (!TryBeginUse(actor, out string failureReason))
        {
            if (serviceSession != null)
            {
                serviceSessionRuntime.CancelSession(
                    serviceSession.SessionId,
                    failureReason);
            }
            actor?.AddActivity(CharacterActivityEvent.Facility(
                CharacterActivityKinds.FacilityUse,
                CharacterActivityOutcomes.Failed,
                $"{objectNameOrDefault()} 이용 실패: {failureReason}",
                this,
                reasonCode: failureReason,
                bubbleEligible: true));
            yield break;
        }

        AbilityMove moveable = actor != null ? actor.GetAbility<AbilityMove>() : null;
        if (moveable == null)
        {
            if (serviceSession != null)
            {
                serviceSessionRuntime.CancelSession(
                    serviceSession.SessionId,
                    "이동 능력이 없어 서비스를 시작할 수 없습니다.");
            }
            EndUse(actor);
            yield break;
        }

        AIAction currentAction = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
        Vector3 usePosition = GetFacilityAnchorWorldPosition(FacilityAnchorPurposeIds.Use, actor.transform.position);
        actor?.Brain?.SetActionPhase("\uC2DC\uC124 \uC811\uADFC", this);
        yield return moveable.Move2PosBySpeed(usePosition, 0.7f, currentAction);
        actor?.Brain?.SetActionPhase("\uC790\uB9AC \uC7A1\uAE30", this);
        yield return Linger(actor, 0.12f, currentAction);
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
                actor?.Brain?.SetActionPhase("서비스 접수", this);
                yield return Linger(
                    actor,
                    serviceSession.Contract.receptionSeconds,
                    currentAction);
            }
            if ((serviceSession.Contract.activeStages
                    & ServiceProcessStageMask.Waiting) != 0)
            {
                serviceSessionRuntime.TrySetStage(
                    serviceSession.SessionId,
                    ServiceSessionStage.Waiting,
                    out _);
                actor?.Brain?.SetActionPhase("서비스 대기", this);
                yield return Linger(
                    actor,
                    serviceSession.Contract.waitingSeconds,
                    currentAction);
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
            actor?.Brain?.SetActionPhase("좌석으로 이동", seat);
            yield return moveable.Move2PosBySpeed(
                seat.GetFacilityAnchorWorldPosition(
                    FacilityAnchorPurposeIds.Use,
                    actor.transform.position),
                0.7f,
                currentAction);
        }

        float duration = serviceSession?.Contract?.serviceSeconds > 0f
            ? serviceSession.Contract.serviceSeconds
            : Facility != null
                ? Facility.useDuration
                : 1f;
        if (actor != null && actor.Stats != null)
        {
            duration *= actor.Stats.GetStayDurationMultiplier();
        }

        actor?.Brain?.SetActionPhase("\uC2DC\uC124 \uC774\uC6A9", this, $"{duration:0.#}s");
        if (serviceSession != null)
        {
            serviceSessionRuntime.TrySetStage(
                serviceSession.SessionId,
                ServiceSessionStage.Service,
                out _);
        }
        if (duration > 0f)
        {
            yield return Linger(actor, duration, currentAction);
        }

        MealConsumptionResult mealResult = default;
        if (Facility != null && Facility.SupportsRole(FacilityRole.Meal))
        {
            if (mealConsumptionRuntime == null
                || !mealConsumptionRuntime.TryConsumeMeal(
                    actor,
                    this,
                    out mealResult))
            {
                string reason = mealResult.FailureReason;
                if (serviceSession != null)
                {
                    serviceSessionRuntime.CancelSession(
                        serviceSession.SessionId,
                        string.IsNullOrWhiteSpace(reason)
                            ? "제공할 음식이 없습니다."
                            : reason);
                }
                actor?.AddActivity(CharacterActivityEvent.Facility(
                    CharacterActivityKinds.FacilityUse,
                    CharacterActivityOutcomes.Failed,
                    $"{objectNameOrDefault()} 식사 실패: "
                    + (string.IsNullOrWhiteSpace(reason) ? "메뉴 없음" : reason),
                    this,
                    reasonCode: reason,
                    bubbleEligible: true));
                EndUse(actor);
                yield break;
            }
        }
        else
        {
            ApplyConfiguredUseRecovery(actor);
        }

        waterFixtureUseRuntime?.CompleteUse(this, fixtureUseTicket);
        ModularFacilityRuntimeEffects.ApplyUseCompleted(actor, this);
        roomEnvironmentExperienceService?.Apply(new RoomEnvironmentExperienceEvent(
            actor,
            this,
            RoomExperienceActivity.FacilityUse));
        actor?.Brain?.SetActionPhase("\uC774\uC6A9 \uC815\uB9AC", this);
        if (serviceSession?.Contract != null
            && (serviceSession.Contract.activeStages
                & ServiceProcessStageMask.Payment) != 0)
        {
            serviceSessionRuntime.TrySetStage(
                serviceSession.SessionId,
                ServiceSessionStage.Payment,
                out _);
            actor?.Brain?.SetActionPhase("서비스 결제", this);
            yield return Linger(
                actor,
                serviceSession.Contract.paymentSeconds,
                currentAction);
        }
        if (serviceSession?.Contract != null
            && (serviceSession.Contract.activeStages
                & ServiceProcessStageMask.Cleanup) != 0)
        {
            serviceSessionRuntime.TrySetStage(
                serviceSession.SessionId,
                ServiceSessionStage.Cleanup,
                out _);
            actor?.Brain?.SetActionPhase("서비스 정리", this);
            yield return Linger(
                actor,
                serviceSession.Contract.cleanupSeconds,
                currentAction);
        }
        else
        {
            yield return Linger(actor, 0.12f, currentAction);
        }
        actor?.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.FacilityUse,
            CharacterActivityOutcomes.Completed,
            mealResult.Success
                ? $"{mealResult.DisplayName} 식사 완료"
                : $"{objectNameOrDefault()} 이용 완료",
            this));
        if (serviceSession != null
            && !serviceSessionRuntime.TryCompleteSession(
                serviceSession.SessionId,
                out _,
                out string completionFailure))
        {
            serviceSessionRuntime.CancelSession(
                serviceSession.SessionId,
                completionFailure);
        }
        EndUse(actor);
    }

    public FacilityAssignmentStatus GetWorkerAssignmentStatus(CharacterActor actor)
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
        foreach (WorkTypeDefinition definition in WorkTypeCatalog.Enumerate(
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

    public bool CanAssignWorker(CharacterActor actor, out string failureReason)
    {
        FacilityAssignmentStatus status = GetWorkerAssignmentStatus(actor);
        failureReason = status.Reason;
        return status.IsAllowed;
    }

    public IEnumerator AllocateWorker(CharacterActor actor)
    {
        PruneInvalidWorker();
        if (!CanAssignWorker(actor, out _))
        {
            yield break;
        }

        worker = actor;
        ReleaseWorkerReservation(actor);
        AbilityMove moveable = actor != null ? actor.GetAbility<AbilityMove>() : null;
        if (moveable == null) yield break;

        AIAction currentAction = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
        Vector3 workPosition = GetFacilityAnchorWorldPosition(FacilityAnchorPurposeIds.Work, actor.transform.position);
        actor?.Brain?.SetActionPhase("\uC791\uC5C5\uB300 \uC811\uADFC", this);
        yield return moveable.Move2PosBySpeed(workPosition, 1f, currentAction);
        actor.ChangeLayer("DungeonMiddleObject");
        yield return moveable.Move2PosBySpeed(workPosition + new Vector3(0f, 0.15f), 3f, currentAction);
        actor?.Brain?.SetActionPhase("\uC791\uC5C5 \uC790\uC138", this);
        actor.Flip(CharacterFacing.RIGHT);
    }

    public void DeallocateWorker(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        PruneInvalidWorker();
        if (worker != actor) return;

        worker = null;
        actor.Brain?.SetActionPhase("\uC2DC\uC124 \uD1F4\uC7A5", this);
        actor.transform.position -= new Vector3(0f, 0.15f);
        Vector2Int actorGridPosition = grid != null
            ? grid.GetXY(actor.transform.position)
            : centerPos;
        if (!ContainsGridPosition(actorGridPosition)
            && TryGetFacilityOccupiedWorldPosition(actor.transform.position, out Vector3 exitPosition))
        {
            actor.transform.position = exitPosition;
        }
        actor.ChangeLayer("Default");
    }

    private IEnumerator Linger(CharacterActor actor, float seconds, AIAction expectedAction)
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
                    || actor.Brain == null
                    || actor.Brain.bestAction != expectedAction
                    || actor.Brain.isBestActionEnd))
            {
                yield break;
            }

            timer += GameDeltaTime;
            yield return null;
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
            if (worker.gameObject == null
                || !worker.gameObject.scene.IsValid()
                || !worker.gameObject.activeInHierarchy)
            {
                worker = null;
            }
        }
        catch (MissingReferenceException)
        {
            worker = null;
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

    public void ApplyConfiguredUseRecovery(CharacterActor actor)
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
        CharacterActor actor,
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

        if (mood != 0f)
        {
            actor.ApplyMoodFactor(
                $"facility:{GetInstanceID()}",
                $"{objectNameOrDefault()} 이용",
                mood,
                180f,
                2);
        }

        if (actor.TryGetAbility(out AbilityWork work))
        {
            work.RecoverOffDuty(sleep, 0f, fun, hunger, excretion, hygiene);
            return;
        }

        if (sleep != 0f) actor.ChangesStat(CharacterCondition.SLEEP, sleep);
        if (fun != 0f) actor.ChangesStat(CharacterCondition.FUN, fun);
        if (hunger != 0f) actor.ChangesStat(CharacterCondition.HUNGER, hunger);
        if (excretion != 0f) actor.ChangesStat(CharacterCondition.EXCRETION, excretion);
        if (hygiene != 0f) actor.ChangesStat(CharacterCondition.HYGIENE, hygiene);
    }

    private string objectNameOrDefault()
    {
        return BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.objectName)
            ? BuildingData.objectName
            : name;
    }
}
