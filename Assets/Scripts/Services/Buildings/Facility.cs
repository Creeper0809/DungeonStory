using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class Facility : BuildableObject,
    IInteractable,
    IWorkableFacility,
    IAllocatedWorkerOccupancyQuery,
    IWarehouseFacility
{
    private BuildingWorkerAllocation workerAllocation;
    private IBuildingVisitorPort worker => workerAllocation?.Actor;
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
    public bool HasAllocatedWorker
    {
        get
        {
            PruneInvalidWorker();
            return worker != null;
        }
    }

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

        long storageMassCapacityGrams = this.GetStorageMassCapacityGrams();
        if (storageMassCapacityGrams > 0L)
        {
            bool restrictCategory = !this.StoresAllCategories();
            warehouseInventory = new WarehouseInventory(
                storageMassCapacityGrams,
                this.GetStorageCategory(),
                restrictCategory);
        }
        else
        {
            warehouseInventory = null;
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
        FacilityUseExecutionState restoredUse = FindActiveUse(actor?.BuildingCharacterId.Value);
        bool resuming = restoredUse != null;
        string visitFailure;
        if (!resuming && !CanQueueVisit(actor, out visitFailure))
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

        if (!resuming && !CanVisit(actor, out _))
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
        if (resuming && !string.IsNullOrEmpty(restoredUse.serviceSessionId)
            && (serviceSessionRuntime == null
                || !serviceSessionRuntime.TryGetSessionSnapshot(restoredUse.serviceSessionId, out serviceSession)
                || !serviceSession.IsActive
                || serviceSession.ActorId != restoredUse.actorId
                || serviceSession.HubId != RequirePersistentInstanceId().Value))
            throw new InvalidOperationException("Restored facility execution lost its service owner.");
        if (!resuming && serviceHub != null
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

        string failureReason;
        bool useAdmitted = resuming
            ? TryRestoreActiveUse(actor, out failureReason)
            : TryBeginUse(actor, out failureReason);
        if (!useAdmitted)
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

        var facilityUseAttempt = new FacilityUseAttemptContext(this,
            restoredUse ?? BeginExecution(actor, serviceSession));

        if (actor == null || !actor.VisitorSnapshot.CanMove)
        {
            AbortInteraction(
                actor,
                interactionFacilityLabel,
                serviceSession,
                useStarted: true,
                BuildingInteractionFailureKind.ActorUnavailable,
                "actor-cannot-move",
                facilityUseAttempt);
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
        if (facilityUseAttempt.State.Enter(FacilityUseExecutionPhase.Approach))
        {
            yield return actor.MoveTo(usePosition, 0.7f, currentAction);
            if (!TryContinueInteraction(actor, currentAction,
                    out BuildingInteractionFailureKind approachFailure, out string approachDetail))
            {
                AbortInteraction(actor, interactionFacilityLabel, serviceSession,
                    useStarted: true, approachFailure, approachDetail, facilityUseAttempt);
                yield break;
            }
            facilityUseAttempt.State.Advance(FacilityUseExecutionPhase.Approach);
        }
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
                interactionFailureDetail,
                facilityUseAttempt);
            yield break;
        }
        actor.SetActionPhase("\uC790\uB9AC \uC7A1\uAE30", this);
        yield return Linger(actor, 0.12f, currentAction, facilityUseAttempt,
            FacilityUseExecutionPhase.Settle);
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
                interactionFailureDetail,
                facilityUseAttempt);
            yield break;
        }
        if (serviceSession != null
            && serviceSession.Contract != null)
        {
            if ((serviceSession.Contract.activeStages
                    & ServiceProcessStageMask.Reception) != 0)
            {
                EnterServiceStage(facilityUseAttempt, serviceSession, ServiceSessionStage.Reception,
                    FacilityUseExecutionPhase.Reception);
                actor.SetActionPhase("서비스 접수", this);
                yield return Linger(
                    actor,
                    serviceSession.Contract.receptionSeconds,
                    currentAction, facilityUseAttempt, FacilityUseExecutionPhase.Reception);
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
                        interactionFailureDetail,
                        facilityUseAttempt);
                    yield break;
                }
            }
            if ((serviceSession.Contract.activeStages
                    & ServiceProcessStageMask.Waiting) != 0)
            {
                EnterServiceStage(facilityUseAttempt, serviceSession, ServiceSessionStage.Waiting,
                    FacilityUseExecutionPhase.Waiting);
                actor.SetActionPhase("서비스 대기", this);
                yield return Linger(
                    actor,
                    serviceSession.Contract.waitingSeconds,
                    currentAction, facilityUseAttempt, FacilityUseExecutionPhase.Waiting);
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
                        interactionFailureDetail,
                        facilityUseAttempt);
                    yield break;
                }
            }
        }
        if (facilityUseAttempt.State.Enter(FacilityUseExecutionPhase.SeatApproach)
            && serviceHub?.serviceCategory == ServiceCategory.Dining
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
                    interactionFailureDetail,
                    facilityUseAttempt);
                yield break;
            }
        }
        facilityUseAttempt.State.Advance(FacilityUseExecutionPhase.SeatApproach);

        bool usesPhysicalMealAction = FacilityUseEffectsExecution.UsesPhysicalMeal(this);
        float duration = facilityUseAttempt.State.serviceSeconds;

        actor.SetActionPhase("\uC2DC\uC124 \uC774\uC6A9", this, $"{duration:0.#}s");
        if (serviceSession != null)
        {
            EnterServiceStage(facilityUseAttempt, serviceSession, ServiceSessionStage.Service,
                FacilityUseExecutionPhase.Service);
        }
        if (duration > 0f && !usesPhysicalMealAction)
        {
            yield return Linger(actor, duration, currentAction, facilityUseAttempt,
                FacilityUseExecutionPhase.Service);
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
                    interactionFailureDetail,
                    facilityUseAttempt);
                yield break;
            }
        }

        BuildingMealUseSnapshot mealResult = facilityUseAttempt.State.mealSucceeded
            ? new BuildingMealUseSnapshot(true, string.Empty,
                facilityUseAttempt.State.mealDisplayName, 0)
            : default;
        if (facilityUseAttempt.State.Enter(FacilityUseExecutionPhase.Effects))
        {
            var effects = new FacilityUseEffectsExecution(this, actor,
                () => facilityUseAttempt.State, mealConsumptionRuntime, waterFixtureUseRuntime,
                roomEnvironmentExperienceService, GameClock, interactionFacilityLabel);
            IEnumerator execution = effects.Apply();
            try
            {
                while (execution.MoveNext())
                {
                    yield return execution.Current;
                    if (!TryContinueInteraction(actor, currentAction,
                            out interactionFailure, out interactionFailureDetail))
                    {
                        AbortInteraction(actor, interactionFacilityLabel, serviceSession, useStarted: true,
                            interactionFailure, interactionFailureDetail, facilityUseAttempt);
                        yield break;
                    }
                }
            }
            finally { (execution as IDisposable)?.Dispose(); }
            mealResult = effects.Meal;
            if (!effects.Succeeded)
            {
                if (effects.ResourceFailure)
                    AbortForResourceFailure(actor, interactionFacilityLabel, serviceSession,
                        effects.FailureCode, facilityUseAttempt);
                else
                {
                    CancelServiceSessionAndCaptureChild(serviceSession, effects.FailureCode, facilityUseAttempt);
                    actor.RecordActivity(this, new BuildingActivitySnapshot(BuildingActivityKinds.FacilityUse,
                        effects.Cancelled ? BuildingActivityOutcomes.Cancelled : BuildingActivityOutcomes.Failed,
                        effects.ActivityDetail, reasonCode: effects.FailureCode, bubbleEligible: !effects.Cancelled));
                    CommitTerminalFacilityUse(actor, facilityUseAttempt,
                        effects.Cancelled ? CharacterFacilityUseOutcomeStatus.Cancelled : CharacterFacilityUseOutcomeStatus.Failed,
                        effects.FailureCode);
                    if (effects.Cancelled) SetVisitOutcome(actor, this, BuildingVisitOutcome.Abandoned);
                    else actor.ReportInteractionFailure(BuildingInteractionFailureKind.ConsumptionFailed,
                        string.IsNullOrWhiteSpace(effects.FailureDetail)
                            ? $"{interactionFacilityLabel}: {effects.FailureCode}"
                            : $"{interactionFacilityLabel}: {effects.FailureCode}:{effects.FailureDetail}", this);
                }
                yield break;
            }
        facilityUseAttempt.State.Advance(FacilityUseExecutionPhase.Effects);
        }
        actor.SetActionPhase("\uC774\uC6A9 \uC815\uB9AC", this);
        if (serviceSession?.Contract != null
            && (serviceSession.Contract.activeStages
                & ServiceProcessStageMask.Payment) != 0)
        {
            EnterServiceStage(facilityUseAttempt, serviceSession, ServiceSessionStage.Payment,
                FacilityUseExecutionPhase.Payment);
            actor.SetActionPhase("서비스 결제", this);
            yield return Linger(
                actor,
                serviceSession.Contract.paymentSeconds,
                currentAction, facilityUseAttempt, FacilityUseExecutionPhase.Payment);
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
                    interactionFailureDetail,
                    facilityUseAttempt);
                yield break;
            }
        }
        if (serviceSession?.Contract != null
            && (serviceSession.Contract.activeStages
                & ServiceProcessStageMask.Cleanup) != 0)
        {
            EnterServiceStage(facilityUseAttempt, serviceSession, ServiceSessionStage.Cleanup,
                FacilityUseExecutionPhase.Cleanup);
            actor.SetActionPhase("서비스 정리", this);
            yield return Linger(
                actor,
                serviceSession.Contract.cleanupSeconds,
                currentAction, facilityUseAttempt, FacilityUseExecutionPhase.Cleanup);
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
                    interactionFailureDetail,
                    facilityUseAttempt);
                yield break;
            }
        }
        else
        {
            yield return Linger(actor, 0.12f, currentAction, facilityUseAttempt,
                FacilityUseExecutionPhase.Exit);
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
                    interactionFailureDetail,
                    facilityUseAttempt);
                yield break;
            }
        }
        CharacterFacilityUseOutcomeStatus finalStatus =
            CharacterFacilityUseOutcomeStatus.Completed;
        string finalFailureCode = string.Empty;
        ServiceSessionSnapshot completedServiceSession = null;
        if (serviceSession != null
            && !serviceSessionRuntime.TryCompleteSession(
                serviceSession.SessionId,
                out completedServiceSession,
                out DomainFailure completionFailure))
        {
            finalStatus = CharacterFacilityUseOutcomeStatus.Partial;
            finalFailureCode = completionFailure.Code.ToString();
            CancelServiceSessionAndCaptureChild(
                serviceSession,
                finalFailureCode,
                facilityUseAttempt);
        }
        if (!string.IsNullOrWhiteSpace(
                completedServiceSession?.TerminalPublicationKey))
        {
            facilityUseAttempt.ChildResultKeys.Add(
                completedServiceSession.TerminalPublicationKey);
        }
        CharacterFacilityUseOutcomeReceipt facilityUseReceipt =
            facilityUseAttempt.CreateReceipt(
                actor,
                finalStatus,
                finalFailureCode);
        if (!CompleteUse(
                actor,
                facilityUseReceipt,
                out FacilityVisitCommitResult facilityUseCommit))
        {
            throw new InvalidOperationException(
                $"Facility completion lost its active occupancy: "
                + $"facility={RequirePersistentInstanceId().Value}; "
                + $"actor={actor?.BuildingCharacterId.Value ?? "<missing>"}.");
        }
        // Test/presentation ports can commit synchronously without using the
        // persistent outbox. Its production owner removes this atomically too.
        FacilityState.activeUses.RemoveAll(value =>
            value.operationSequence == facilityUseReceipt.OperationSequence);
        if (!facilityUseCommit.DurablyCommitted)
        {
            actor.ReportInteractionFailure(
                BuildingInteractionFailureKind.ServiceUnavailable,
                interactionFacilityLabel
                    + ": facility-use-parent-pending:"
                    + facilityUseCommit.DetailCode,
                this);
        }
        bool completed = finalStatus ==
            CharacterFacilityUseOutcomeStatus.Completed;
        actor.RecordActivity(this, new BuildingActivitySnapshot(
            BuildingActivityKinds.FacilityUse,
            completed
                ? BuildingActivityOutcomes.Completed
                : BuildingActivityOutcomes.Failed,
            completed
                ? mealResult.Success
                    ? $"{mealResult.DisplayName} 식사 완료"
                    : $"{interactionFacilityLabel} 이용 완료"
                : $"{interactionFacilityLabel} 일부 효과 적용 후 종료: "
                    + finalFailureCode,
            reasonCode: finalFailureCode));
        SetVisitOutcome(
            actor,
            this,
            completed
                ? BuildingVisitOutcome.Completed
                : BuildingVisitOutcome.Failed);
    }

    public override void ReleaseTransientCharacterOwnership(
        IBuildingVisitorPort actor,
        string reason)
    {
        if (actor == null)
        {
            return;
        }

        // Old scene instances are torn down after a replacement world has
        // published. They must not cancel the new world's same-ID execution.
        if (serviceSessionRuntime?.IsReplacedFacility(this) == true)
        {
            base.ReleaseTransientCharacterOwnership(actor, reason);
            return;
        }
        FacilityUseExecutionState interrupted = FindActiveUse(actor.BuildingCharacterId.Value);
        if (interrupted != null)
        {
            var attempt = new FacilityUseAttemptContext(this, interrupted);
            ServiceSessionSnapshot ownedSession = null;
            if (!string.IsNullOrEmpty(interrupted.serviceSessionId))
                serviceSessionRuntime?.TryGetSessionSnapshot(interrupted.serviceSessionId, out ownedSession);
            string detail = string.IsNullOrWhiteSpace(reason) ? "character-lifecycle-ended" : reason;
            CancelServiceSessionAndCaptureChild(ownedSession, detail, attempt);
            CommitTerminalFacilityUse(actor, attempt,
                interrupted.effectsCommitted ? CharacterFacilityUseOutcomeStatus.Partial
                    : CharacterFacilityUseOutcomeStatus.Cancelled, detail);
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
        if (TryAllocateWorker(actor, actor?.CurrentActionToken, null,
                out IBuildingWorkerAllocation allocation, out _))
            yield return allocation.Approach();
    }

    public bool TryAllocateWorker(IBuildingVisitorPort actor, object expectedAction,
        Func<bool> isRequestCurrent, out IBuildingWorkerAllocation allocation,
        out string failureReason)
    {
        allocation = null;
        PruneInvalidWorker();
        if (actor == null || !actor.VisitorSnapshot.IsRuntimeActive)
        {
            failureReason = "worker-unavailable";
            return false;
        }
        if (workerAllocation != null)
        {
            failureReason = "worker-already-allocated";
            return false;
        }
        if (!CanAssignWorker(actor, out failureReason))
            return false;
        if (expectedAction != null && (!actor.IsCurrentAction(expectedAction)
                || actor.IsCurrentActionEnded)
            || isRequestCurrent != null && !isRequestCurrent())
        {
            failureReason = "worker-allocation-request-replaced";
            return false;
        }
        var admitted = new BuildingWorkerAllocation(this, actor, expectedAction,
            isRequestCurrent, value => ReferenceEquals(workerAllocation, value),
            ReleaseWorkerAllocation,
            GetFacilityAnchorWorldPosition(FacilityAnchorPurposeIds.Work,
                actor.VisitorSnapshot.Position), new Vector3(0f, 0.15f),
            ResolveWorkerExitPosition, "작업대 접근", "작업 자세");
        workerAllocation = admitted;
        allocation = admitted;
        try
        {
            TrackAllocatedWorkerOwnership(actor);
            ReleaseWorkerReservation(actor);
            failureReason = string.Empty;
            return true;
        }
        catch
        {
            admitted.Dispose();
            throw;
        }
    }

    public void DeallocateWorker(IBuildingVisitorPort actor)
    {
        if (actor != null && ReferenceEquals(worker, actor))
            workerAllocation.Dispose();
    }

    private void ReleaseWorkerAllocation(BuildingWorkerAllocation allocation)
    {
        if (!ReferenceEquals(workerAllocation, allocation))
            return;
        workerAllocation = null;
        UntrackTransientOwnership(allocation.CharacterId,
            BuildingTransientOwnershipKind.AllocatedWorker);
    }

    private Vector3 ResolveWorkerExitPosition(Vector3 position)
    {
        Vector2Int actorGridPosition = grid != null ? grid.GetXY(position) : centerPos;
        return !ContainsGridPosition(actorGridPosition)
            && TryGetFacilityOccupiedWorldPosition(position, out Vector3 exit)
                ? exit : position;
    }

    internal FacilityUseExecutionState FindActiveUse(string actorId) =>
        FacilityState.activeUses.SingleOrDefault(value => value.actorId == actorId);

    private FacilityUseExecutionState BeginExecution(
        IBuildingVisitorPort actor, ServiceSessionSnapshot session)
    {
        long sequence = AllocateFacilityUseOperationSequence();
        AIAction action = actor?.CurrentActionToken as AIAction;
        BuildingVisitorSnapshot before = actor.VisitorSnapshot;
        var state = new FacilityUseExecutionState
        {
            operationSequence = sequence,
            operationId = "facility-visit:facility=" + RequirePersistentInstanceId().Value + ":use=" + sequence,
            actorId = actor.BuildingCharacterId.Value,
            actorDisplayName = actor.BuildingDisplayName,
            facilityRole = Facility?.roles.ToString() ?? FacilityRole.None.ToString(),
            serviceSessionId = session?.SessionId ?? string.Empty,
            actionType = action?.actionset?.GetType().FullName ?? string.Empty,
            actionBranch = (int)(action?.actionset?.Branch ?? CharacterAiBranch.None),
            before = FacilityUseVisitorState.Capture(before),
            serviceSeconds = (session?.Contract?.serviceSeconds > 0f
                ? session.Contract.serviceSeconds : Facility != null ? Facility.useDuration : 1f)
                * before.StayDurationMultiplier
        };
        FacilityState.activeUses.Add(state);
        MarkFacilityDynamicStateDirty();
        return state;
    }

    private void EnterServiceStage(FacilityUseAttemptContext attempt,
        ServiceSessionSnapshot session, ServiceSessionStage stage,
        FacilityUseExecutionPhase phase)
    {
        if (attempt.State.phase > phase) return;
        if (!serviceSessionRuntime.TryGetSessionSnapshot(session.SessionId, out var current))
            throw new InvalidOperationException("Facility service session disappeared.");
        if (current.Stage == stage) return;
        if (!serviceSessionRuntime.TrySetStage(session.SessionId, stage, out var failure))
            throw new InvalidOperationException("Facility service stage rejected: " + failure.Code);
    }

    private IEnumerator Linger(IBuildingVisitorPort actor, float seconds, object expectedAction,
        FacilityUseAttemptContext attempt, FacilityUseExecutionPhase phase)
    {
        if (!attempt.State.Enter(phase)) yield break;
        if (!attempt.State.timerInitialized)
        {
            if (!FacilityUseExecutionState.Finite(seconds) || seconds < 0f)
                throw new InvalidOperationException("Invalid facility stage duration.");
            attempt.State.remainingSeconds = seconds;
            attempt.State.timerInitialized = true;
        }
        while (true)
        {
            if (!TryContinueInteraction(actor, expectedAction, out _, out _))
                yield break;
            if (attempt.State.remainingSeconds <= 0f) break;

            actor?.NotifyFacilityServiceHeartbeat();
            attempt.State.remainingSeconds = Mathf.Max(0f,
                attempt.State.remainingSeconds - GameDeltaTime);
            yield return null;
        }
        attempt.State.Advance(phase);
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
        string failureDetail,
        FacilityUseAttemptContext attempt = null)
    {
        string detail = string.IsNullOrWhiteSpace(failureDetail)
            ? failureKind.ToString()
            : failureDetail;
        // Lifecycle release can durably terminate this use before its suspended
        // iterator observes cancellation. Do not read/recommit that retired
        // execution. A still-current action with missing state still fails at
        // State; this is not a substitute execution or a completion fallback.
        bool attemptStillOwned = attempt == null || attempt.HasActiveOwner;
        if (attemptStillOwned)
            CancelServiceSessionAndCaptureChild(serviceSession, detail, attempt);

        bool facilityAlive = this != null && !isDestroy;
        if (actor != null)
        {
            if (facilityAlive && useStarted)
            {
                if (attempt != null && attemptStillOwned)
                {
                    CommitTerminalFacilityUse(
                        actor,
                        attempt,
                        attempt.EffectsCommitted
                            ? CharacterFacilityUseOutcomeStatus.Partial
                            : failureKind == BuildingInteractionFailureKind.ActionReplaced
                                ? CharacterFacilityUseOutcomeStatus.Cancelled
                                : CharacterFacilityUseOutcomeStatus.Failed,
                        detail);
                }
                else if (attempt == null)
                {
                    EndUse(actor);
                }
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
        string failureCode,
        FacilityUseAttemptContext attempt)
    {
        string code = string.IsNullOrWhiteSpace(failureCode)
            ? "facility-resource-unavailable"
            : failureCode;
        CancelServiceSessionAndCaptureChild(serviceSession, code, attempt);
        if (actor != null)
        {
            CommitTerminalFacilityUse(
                actor,
                attempt,
                attempt?.EffectsCommitted == true
                    ? CharacterFacilityUseOutcomeStatus.Partial
                    : CharacterFacilityUseOutcomeStatus.Failed,
                code);
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

    private void CancelServiceSessionAndCaptureChild(
        ServiceSessionSnapshot serviceSession,
        string reason,
        FacilityUseAttemptContext attempt)
    {
        if (serviceSession == null || serviceSessionRuntime == null)
            return;
        serviceSessionRuntime.CancelSession(serviceSession.SessionId, reason);
        if (attempt != null
            && serviceSessionRuntime.TryGetSessionSnapshot(
                serviceSession.SessionId,
                out ServiceSessionSnapshot terminal)
            && !string.IsNullOrWhiteSpace(terminal?.TerminalPublicationKey))
        {
            attempt.ChildResultKeys.Add(terminal.TerminalPublicationKey);
        }
    }

    private FacilityVisitCommitResult CommitTerminalFacilityUse(
        IBuildingVisitorPort actor,
        FacilityUseAttemptContext attempt,
        CharacterFacilityUseOutcomeStatus status,
        string failureCode)
    {
        if (attempt == null)
            return FacilityVisitCommitResult.Rejected(
                "facility-use-attempt-missing");
        CharacterFacilityUseOutcomeReceipt receipt = attempt.CreateReceipt(
            actor,
            status,
            failureCode);
        FacilityVisitCommitResult result;
        if (attempt.EffectsCommitted)
        {
            if (!CompleteUse(actor, receipt, out result))
                result = RecordFacilityUse(actor, receipt);
        }
        else
        {
            EndUse(actor);
            result = RecordFacilityUse(actor, receipt);
        }
        FacilityState.activeUses.RemoveAll(value => value.operationSequence == receipt.OperationSequence);
        if (!result.DurablyCommitted)
        {
            actor?.ReportInteractionFailure(
                BuildingInteractionFailureKind.ServiceUnavailable,
                objectNameOrDefault() + ": facility-use-parent-pending:"
                    + result.DetailCode,
                this);
        }
        return result;
    }

    private sealed class FacilityUseAttemptContext
    {
        private readonly Facility owner;
        internal FacilityUseAttemptContext(Facility owner, FacilityUseExecutionState state)
        {
            this.owner = owner;
            OperationSequence = state.operationSequence;
        }

        // Other visits can publish a cloned FacilityState while this coroutine
        // is suspended. Always resolve the current owner; never mutate a stale clone.
        internal FacilityUseExecutionState State => owner.FacilityState.activeUses
            .Single(value => value.operationSequence == OperationSequence);
        internal bool HasActiveOwner => owner.FacilityState.activeUses
            .Any(value => value.operationSequence == OperationSequence);
        internal long OperationSequence { get; }
        internal string OperationId => State.operationId;
        internal string ActorId => State.actorId;
        internal string ActorDisplayName => State.actorDisplayName;
        internal string FacilityRole => State.facilityRole;
        internal string ServiceSessionId => State.serviceSessionId;
        internal BuildingVisitorSnapshot Before => State.before.ToSnapshot();
        internal bool EffectsCommitted { get => State.effectsCommitted; set => State.effectsCommitted = value; }
        internal List<string> ChildResultKeys => State.childResultKeys;

        internal CharacterFacilityUseOutcomeReceipt CreateReceipt(
            IBuildingVisitorPort actor,
            CharacterFacilityUseOutcomeStatus status,
            string failureCode) => new()
        {
            OperationId = OperationId,
            OperationSequence = OperationSequence,
            ActorId = ActorId,
            ActorDisplayName = ActorDisplayName,
            FacilityRole = FacilityRole,
            ServiceSessionId = ServiceSessionId,
            Status = status,
            EffectsCommitted = EffectsCommitted,
            FailureCode = failureCode ?? string.Empty,
            Before = Before,
            After = SnapshotOrDefault(actor, Before),
            ChildResultKeys = ChildResultKeys
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()
        };

        private static BuildingVisitorSnapshot SnapshotOrDefault(
            IBuildingVisitorPort actor,
            BuildingVisitorSnapshot fallback = default)
        {
            if (actor == null)
                return fallback;
            try
            {
                return actor.VisitorSnapshot;
            }
            catch (MissingReferenceException)
            {
                return fallback;
            }
        }
    }

    private void PruneInvalidWorker()
    {
        if (worker == null)
            return;
        try
        {
            if (!worker.VisitorSnapshot.IsRuntimeActive)
                workerAllocation.Dispose();
        }
        catch (MissingReferenceException)
        {
            workerAllocation.Dispose();
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

    public IReadOnlyList<string> ApplyConfiguredUseRecovery(IBuildingVisitorPort actor) =>
        FacilityUseEffectsExecution.ApplyConfiguredRecovery(this, actor,
            roomEnvironmentExperienceService, objectNameOrDefault());

    private string objectNameOrDefault()
    {
        return BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.objectName)
            ? BuildingData.objectName
            : name;
    }
}
