using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class CaptivityRuntime :
    ICaptivityRuntime,
    ICaptivityRestoreStateQuery,
    ICaptivityPersistence,
    ICaptivityRestoreCandidateSource,
    ICaptiveLaborQuery,
    ICaptivityWorkReadinessQuery,
    ICaptivityCommandService,
    ICaptivityEscortRuntime,
    ICaptivityEscapeRuntime,
    IMinionSettlementCommand,
    ICharacterCarePriorityQuery,
    IDungeonRestoreTransactionParticipant,
    IStartable,
    ITickable,
    IDisposable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CaptivityRuntime.Tick");

    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly ICombatEquipmentRuntime combatEquipment;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly ICharacterPopulationService characterPopulation;
    private readonly ICharacterNarrativeQuery narratives;
    private readonly IGridSystemProvider gridProvider;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IGameMoneyAccount money;
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly IDoorAccessCommandService doorAccessCommands;
    private readonly IDoorAccessSubjectRegistry doorSubjectRegistry;
    private readonly CaptivityInteractionRegistry interactions;
    private readonly IGameClock gameClock;
    private readonly IGameEventBus gameEventBus;
    private readonly IRandomStream random;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IEmploymentStandingCommand employmentStanding;
    private readonly CharacterMoodPolicyService moodPolicy;
    private readonly IFactionCampaignCommand factionCampaign;
    private readonly ISurvivalFoodCommand survivalFood;
    private readonly ICharacterSettlementStandingQuery settlementStandings;
    private readonly CaptivityActorAccess actorAccess;
    private readonly CaptivityPolicyRuntime policyRuntime;
    private readonly CaptivityPerformerRuntime performerRuntime;
    private readonly CaptivityInteractionRuntime interactionRuntime;
    private readonly CaptivityEscortRuntime escortRuntime;
    private readonly CaptivityEscapeRuntime escapeRuntime;
    private readonly CaptivityStateRuntime stateRuntime;
    private readonly CaptivityRestoreCoordinator restoreCoordinator;
    private readonly CaptivityQueryView queryView;
    private readonly CaptivityInteractionMaterialLifecycleRuntime
        interactionMaterialLifecycle;
    private readonly ICaptivityCareLaborInputOwnerRuntime careLaborInputOwner;
    private IDisposable downedSubscription;
    private IDisposable recoveredSubscription;
    private IDisposable deathSubscription;
    private IDisposable invasionSubscription;
    private IReadOnlyList<CaptiveState> captives => actorAccess.States;

    public CaptivityRuntime(
        CaptivityCharacterContext characters,
        CaptivityWorldContext world,
        CaptivitySessionContext session,
        ICaptivityInteractionMaterialRuntime interactionMaterials,
        CaptivityInteractionMaterialLifecycleRuntime
            interactionMaterialLifecycle,
        ICaptivityCareLaborInputOwnerRuntime careLaborInputOwner)
    {
        characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        world = world ?? throw new ArgumentNullException(nameof(world));
        session = session ?? throw new ArgumentNullException(nameof(session));
        worldRegistry = characters.WorldRegistry;
        bodyHealthQuery = characters.BodyHealthQuery;
        bodyHealthCommands = characters.BodyHealthCommands;
        combatEquipment = characters.CombatEquipment;
        itemRuntime = characters.ItemRuntime;
        batchDispositions = characters.BatchDispositions;
        characterPopulation = characters.Population;
        settlementStandings = characters.Population
            as ICharacterSettlementStandingQuery
            ?? throw new InvalidOperationException(
                $"{nameof(CaptivityRuntime)} requires the population service to expose {nameof(ICharacterSettlementStandingQuery)}.");
        narratives = characters.Narratives;
        gridProvider = world.GridProvider;
        pathSearchBroker = world.PathSearchBroker;
        roomLayoutCache = world.RoomLayoutCache;
        doorAccessQuery = world.DoorAccessQuery;
        doorAccessCommands = world.DoorAccessCommands;
        doorSubjectRegistry = world.DoorSubjectRegistry;
        money = session.Money;
        interactions = session.Interactions;
        gameClock = session.GameClock;
        random = session.RandomStreamProvider
            .Get("captivity.security");
        gameEventBus = session.GameEventBus;
        aggregateRootStore = session.AggregateRootStore;
        employmentStanding = session.EmploymentStanding;
        moodPolicy = session.MoodPolicy;
        factionCampaign = session.FactionCampaign;
        survivalFood = session.SurvivalFood;
        this.interactionMaterialLifecycle = interactionMaterialLifecycle
            ?? throw new ArgumentNullException(
                nameof(interactionMaterialLifecycle));
        this.careLaborInputOwner = careLaborInputOwner
            ?? throw new ArgumentNullException(nameof(careLaborInputOwner));
        actorAccess = new CaptivityActorAccess(
            this.aggregateRootStore,
            RecalculateCaptiveState);
        CaptivityActorRuntimeLookup actorRuntime =
            new CaptivityActorRuntimeLookup(FindActor);
        CaptivityUnityEffectsAdapter captivityEffects =
            new CaptivityUnityEffectsAdapter(
                FindActor,
                gameEventBus);
        policyRuntime = new CaptivityPolicyRuntime(
            actorAccess,
            new CaptivityPolicyActorDefaultPort(captivityEffects));
        performerRuntime = new CaptivityPerformerRuntime(
            FindState,
            policyRuntime.Find,
            TryRecruit,
            TryRelease,
            new CaptivityPerformerDefaultPort(
                captivityEffects,
                captivityEffects));
        interactionRuntime = new CaptivityInteractionRuntime(
            actorAccess,
            actorRuntime,
            interactions,
            itemRuntime,
            interactionMaterials,
            TryGetHousing);
        escortRuntime = new CaptivityEscortRuntime(
            actorAccess,
            actorRuntime,
            characters,
            world,
            session);
        stateRuntime = new CaptivityStateRuntime(
            actorAccess,
            policyRuntime,
            interactionRuntime,
            escortRuntime,
            doorSubjectRegistry,
            gameClock);
        escapeRuntime = new CaptivityEscapeRuntime(
            actorAccess,
            actorRuntime,
            world,
            session,
            TryTriggerBetrayal);
        restoreCoordinator = new CaptivityRestoreCoordinator(
            this.worldRegistry,
            this.itemRuntime,
            this.interactions,
            this.aggregateRootStore,
            actorAccess,
            doorSubjectRegistry,
            escortRuntime,
            batchDispositions);
        queryView = new CaptivityQueryView(actorAccess, policyRuntime);
    }

    public IReadOnlyList<CaptiveState> Captives => queryView.Captives;
    public IReadOnlyList<CaptivePolicyData> Policies => queryView.Policies;
    public string ParticipantId => restoreCoordinator.ParticipantId;

    public CaptivitySaveData Capture()
    {
        RequireCareLaborInputOwner("capture");
        interactionMaterialLifecycle.ValidateBeforeCapture(Captives);
        return stateRuntime.Capture();
    }

    public CaptivityRestoreCandidate BuildRestore(CaptivitySaveData saveData) =>
        restoreCoordinator.BuildRestore(saveData);

    public void PublishRestoreCandidate(CaptivityRestoreCandidate candidate) =>
        restoreCoordinator.StageRestore(candidate);

    public bool TryTakePreparedRestoreCandidate(
        out CaptivityRestoreCandidate candidate) =>
        restoreCoordinator.TryTakePreparedRestoreCandidate(out candidate);

    public void BeginRestoreCandidate() =>
        restoreCoordinator.BeginRestoreCandidate();

    public void PublishRestoreCandidate()
        => restoreCoordinator.PublishRestoreCandidate();

    public void RollbackPublishedRestoreCandidate()
        => restoreCoordinator.RollbackPublishedRestoreCandidate();

    public void CompleteRestoreCandidate()
    {
        restoreCoordinator.CompleteRestoreCandidate();
        escapeRuntime.ClearPendingInvasionEscapes();
        RestoreSettlementStandingProjection();
    }

    public void DiscardRestoreCandidate() =>
        restoreCoordinator.DiscardRestoreCandidate();

    public int GetCarePriority(string persistentCharacterId) =>
        queryView.GetCarePriority(persistentCharacterId);
    public bool IsCareSubject(string persistentCharacterId) =>
        queryView.IsCareSubject(persistentCharacterId);

    public void Start()
    {
        downedSubscription =
            gameEventBus.Subscribe<CharacterBodyHealthDownedEvent>(
                gameEvent => stateRuntime.OnCharacterDowned(gameEvent.Actor));
        recoveredSubscription =
            gameEventBus.Subscribe<CharacterBodyHealthRecoveredEvent>(
                gameEvent => stateRuntime.OnCharacterRecovered(gameEvent.Actor));
        deathSubscription = gameEventBus.Subscribe<CharacterDeathEvent>(
            gameEvent =>
            {
                CaptiveState state = FindState(gameEvent.CharacterId.Value);
                if (state != null)
                {
                    ReturnAssignedCaptivityTools(state);
                }
                stateRuntime.OnCharacterDeath(gameEvent);
            });
        invasionSubscription = gameEventBus.Subscribe<InvasionStartedEvent>(
            _ => escapeRuntime.HandleInvasionStarted());

        foreach (CharacterActor actor in worldRegistry.AllCharacters)
        {
            if (IsEligibleDownedIntruder(actor))
            {
                stateRuntime.EnsureCandidate(actor);
            }
        }
    }

    public void Dispose()
    {
        downedSubscription?.Dispose();
        recoveredSubscription?.Dispose();
        deathSubscription?.Dispose();
        invasionSubscription?.Dispose();
        downedSubscription = null;
        recoveredSubscription = null;
        deathSubscription = null;
        invasionSubscription = null;
        escapeRuntime.ClearPendingInvasionEscapes();
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
        RequireCareLaborInputOwner("tick");
        if (gameClock.IsPaused || gameClock.DeltaTime <= 0f)
        {
            return;
        }

        escapeRuntime.TickPendingInvasionEscapes();

        for (int index = 0; index < captives.Count; index++)
        {
            CaptiveState state = captives[index];
            if (state == null
                || !state.IsInCustody
                || (state.status != CaptivityStatus.Confined
                    && state.status != CaptivityStatus.Labor))
            {
                continue;
            }

            CharacterActor actor = FindActor(state.captiveId);
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            state.health = EstimateHealth(actor);
            TickLaborToolPreparation(state, actor);
            TickLaborToolWear(state, actor);
            TickCarePriority(state, actor);
            RecalculateCaptiveState(state);
            if (gameClock.Time + 0.001f < state.nextSecurityCheckAt)
            {
                continue;
            }

            state.nextSecurityCheckAt = gameClock.Time + 5f;
            if (state.escapeRisk < 65f)
            {
                continue;
            }

            float chance = Mathf.Lerp(
                0.02f,
                0.18f,
                Mathf.InverseLerp(65f, 100f, state.escapeRisk));
            if (state.falseCompliance)
            {
                chance *= 1.4f;
            }

            if (random.Chance(Mathf.Clamp01(chance)))
            {
                escapeRuntime.TryBeginEscapeAttempt(
                    state,
                    state.falseCompliance
                        ? "거짓 복종 중 훔친 열쇠"
                        : "감방 탈출 시도",
                    out _);
            }
        }
    }

    private void TickCarePriority(CaptiveState state, CharacterActor actor)
    {
        if (state?.carePriorityUnlocked != true
            || actor == null
            || gameClock.Time + 0.001f < state.nextCareSupplyAt)
        {
            return;
        }

        if (!TryGetHousing(state.captiveId, out BuildableObject housing)
            || housing.BuildingData?.GetCaptiveHousingAbility()
                is not { IsValid: true })
        {
            state.nextCareSupplyAt = gameClock.Time + 5f;
            state.lastResult = "포로 특혜 배급 시설을 찾을 수 없습니다.";
            return;
        }

        string destinationId = CaptivityCareLaborInputOwnerAuthority
            .FormatCareDestinationId(state.captiveId);
        Dictionary<StockCategory, int> foodCost = new Dictionary<StockCategory, int>
        {
            [StockCategory.Food] = 1
        };
        if (itemRuntime.TryConsumeFacilityBuffer(destinationId, foodCost, out _))
        {
            actor.Stats?.RecoverNeed(
                CharacterCondition.HUNGER,
                35f,
                CharacterNeedRecoverySource.Meal);
            state.health = Mathf.Clamp(state.health + 5f, 0f, 100f);
            state.nextCareSupplyAt = gameClock.Time + 120f;
            state.lastResult = "명성 특혜 식량을 배급받았습니다.";
            return;
        }

        bool deliveryOutstanding = itemRuntime.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && stack.StockCategory == StockCategory.Food
            && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
        if (deliveryOutstanding)
        {
            state.nextCareSupplyAt = gameClock.Time + 5f;
            return;
        }

        bool deliveryRequested = itemRuntime.TryRequestFacilityDelivery(
            StockCategory.Food,
            1,
            state.housingPosition,
            destinationId,
            out int requested,
            out _)
            && requested > 0;
        state.nextCareSupplyAt = gameClock.Time + (deliveryRequested ? 15f : 5f);
    }

    private void TickLaborToolPreparation(
        CaptiveState state,
        CharacterActor actor)
    {
        if (state == null
            || actor == null
            || state.pendingLaborPermissions == CaptiveLaborPermission.None
            || string.IsNullOrWhiteSpace(state.laborToolDestinationId))
        {
            return;
        }

        if (!TryGetHousing(state.captiveId, out BuildableObject housing)
            || housing.BuildingData?.GetCaptiveHousingAbility()
                is not { IsValid: true })
        {
            if (!CancelLaborToolPreparation(state))
            {
                state.lastResult =
                    "소실된 감방의 포로 작업 도구 소유권을 정리할 수 없습니다.";
            }
            return;
        }

        if (CaptivityLaborToolAssignmentOutbox.RequiresFinalization(state))
        {
            if (CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                    state,
                    batchDispositions,
                    out _))
            {
                TryCompleteLaborToolAssignment(state, out _);
            }
            return;
        }

        WorldItemStackSnapshot delivered = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.ItemId,
                    CaptivityItemDefinitions.PrisonerWorkKitItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    state.laborToolDestinationId,
                    StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        CaptiveState assignmentProbe = state.Clone();
        if (delivered == null
            || !((ItemInstanceId)delivered.ItemInstanceId).IsValid
            || DurableToolItemRules.ReadCurrentDurability(
                delivered.ItemId,
                delivered.Components) <= 0f
            || !CaptivityDurableToolRuntime.TryAssignLaborTool(
                assignmentProbe,
                delivered))
        {
            return;
        }

        string operationId = CaptivityLaborToolAssignmentOutbox.FormatOperationId(
            state.captiveId,
            delivered.ItemInstanceId);
        state.assignedLaborToolItemId = assignmentProbe.assignedLaborToolItemId;
        state.assignedLaborToolInstanceId = assignmentProbe.assignedLaborToolInstanceId;
        state.assignedLaborToolDurability = assignmentProbe.assignedLaborToolDurability;
        state.assignedLaborToolMaximumDurability =
            assignmentProbe.assignedLaborToolMaximumDurability;
        state.laborToolAssignmentOperationId = operationId;
        state.laborToolAssignmentSourceStackId = delivered.StackId;
        state.laborToolAssignmentCommitId = string.Empty;
        state.laborToolAssignmentCompleted = false;
        if (!batchDispositions.TryCommitPending(
                new[]
                {
                    new PhysicalItemTransformInput(delivered.StackId, 1)
                },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                CaptivityLaborToolAssignmentOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt disposition,
                out _))
        {
            CaptivityLaborToolAssignmentOutbox.ClearAssignment(state);
            return;
        }

        state.laborToolAssignmentCommitId = disposition.CommitId;
        if (!CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                state,
                batchDispositions,
                out string finalizeFailure))
        {
            throw new InvalidOperationException(
                $"Validated captive labor tool '{delivered.ItemInstanceId}' could not be finalized after physical transfer: {finalizeFailure}");
        }

        if (!TryCompleteLaborToolAssignment(
                state,
                out string closeFailure))
        {
            state.lastResult =
                "포로 작업 도구 배송 종결 대기: " + closeFailure;
        }
    }

    private bool TryCompleteLaborToolAssignment(
        CaptiveState state,
        out string failureReason)
    {
        CaptiveLaborPermission permissions = state.pendingLaborPermissions;
        string destinationId = state.laborToolDestinationId;
        state.pendingLaborPermissions = CaptiveLaborPermission.None;
        state.laborToolDestinationId = string.Empty;
        if (!careLaborInputOwner.TryReconcileLive(
                captives,
                out failureReason))
        {
            state.pendingLaborPermissions = permissions;
            state.laborToolDestinationId = destinationId;
            return false;
        }
        state.nextLaborToolWearAt = gameClock.Time + 60f;
        ApplyLaborPermissions(state, permissions);
        return true;
    }

    private void TickLaborToolWear(
        CaptiveState state,
        CharacterActor actor)
    {
        if (state?.status != CaptivityStatus.Labor
            || string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
            || !state.laborToolAssignmentCompleted
            || gameClock.Time + 0.001f < state.nextLaborToolWearAt
            || actor?.GetAbility<AbilityWork>()?.isWorking != true)
        {
            return;
        }

        state.nextLaborToolWearAt = gameClock.Time + 60f;
        state.assignedLaborToolDurability = Mathf.Max(
            0f,
            state.assignedLaborToolDurability - 1f);
        if (state.assignedLaborToolDurability > 0f)
        {
            return;
        }

        ApplyLaborPermissions(state, CaptiveLaborPermission.None);
        CaptivityDurableToolRuntime.TryReturnLaborTool(
            itemRuntime,
            state,
            state.housingPosition);
        state.lastResult = "포로 작업 도구 파손으로 노역 중단";
    }

    private void ApplyLaborPermissions(
        CaptiveState state,
        CaptiveLaborPermission permissions)
    {
        CaptiveLaborPermission normalized =
            permissions & CaptiveLaborPermission.All;
        state.laborPermissions = normalized;
        state.status = normalized == CaptiveLaborPermission.None
            ? CaptivityStatus.Confined
            : CaptivityStatus.Labor;
        CharacterActor laborer = FindActor(state.captiveId);
        if (laborer != null)
        {
            laborer.characterType = normalized == CaptiveLaborPermission.None
                ? CharacterType.Intruder
                : CharacterType.NPC;
        }
        laborer?.SetAiPaused(normalized == CaptiveLaborPermission.None);
        laborer?.SetLifecycleState(
            normalized == CaptiveLaborPermission.None
                ? CharacterLifecycleState.Downed
                : CharacterLifecycleState.Active);
        state.lastResult = normalized == CaptiveLaborPermission.None
            ? "노역 해제"
            : "노역 허용";
    }

    private bool CancelLaborToolPreparation(CaptiveState state)
    {
        if (state == null)
        {
            return true;
        }

        if (CaptivityLaborToolAssignmentOutbox.RequiresFinalization(state)
            && !CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                state,
                batchDispositions,
                out _))
        {
            return false;
        }

        CaptiveLaborPermission previousPermissions =
            state.pendingLaborPermissions;
        string previousDestination = state.laborToolDestinationId;
        state.pendingLaborPermissions = CaptiveLaborPermission.None;
        state.laborToolDestinationId = string.Empty;
        if (!careLaborInputOwner.TryReconcileLive(
                captives,
                out _))
        {
            state.pendingLaborPermissions = previousPermissions;
            state.laborToolDestinationId = previousDestination;
            return false;
        }
        return true;
    }

    private bool ReturnAssignedCaptivityTools(CaptiveState state)
    {
        if (state == null)
        {
            return true;
        }

        if (!CancelLaborToolPreparation(state))
        {
            return false;
        }
        CaptivityDurableToolRuntime.TryReturnLaborTool(
            itemRuntime,
            state,
            state.housingPosition);
        CaptivityDurableToolRuntime.TryReturnRestraint(
            itemRuntime,
            state,
            state.housingPosition);
        return true;
    }

    public bool TryGetCaptive(string captiveId, out CaptiveState captive)
    {
        CaptiveState state = FindState(captiveId);
        captive = state?.Clone();
        return captive != null;
    }

    public bool IsInteractionReady(string captiveId, out string reason) =>
        interactionRuntime.IsReady(captiveId, out reason);

    public bool TryGetActor(string captiveId, out CharacterActor actor)
    {
        actor = FindActor(captiveId);
        return actor != null;
    }

    public bool TryGetHousing(string captiveId, out BuildableObject housing)
    {
        CaptiveState state = FindState(captiveId);
        string housingId = state?.housingBuildingId ?? string.Empty;
        housing = worldRegistry.Buildings.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.isDestroy
            && string.Equals(
                GetHousingId(candidate),
                housingId,
                StringComparison.Ordinal));
        return housing != null;
    }

    public bool TryGetRehabilitationFacility(
        string captiveId,
        out BuildableObject facility)
    {
        CaptiveState state = FindState(captiveId);
        string facilityId = state?.rehabilitationFacilityBuildingId
            ?? string.Empty;
        facility = worldRegistry.Buildings.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.isDestroy
            && string.Equals(
                GetHousingId(candidate),
                facilityId,
                StringComparison.Ordinal));
        return facility != null;
    }

    public bool IsCaptive(string persistentId)
    {
        CaptiveState state = FindState(persistentId);
        return state != null && state.IsInCustody;
    }

public bool IsWorkAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string reason)
    {
        reason = string.Empty;
        CaptiveState state = FindState(GetCharacterId(actor));
        if (state == null || !state.IsInCustody)
        {
            return true;
        }

        if (state.status != CaptivityStatus.Labor
            || !state.CanLabor
            || string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
            || state.assignedLaborToolDurability <= 0f)
        {
            reason = "이 포로는 현재 노역에 투입할 수 없습니다.";
            return false;
        }

        CaptiveLaborPermission required = GetLaborPermission(workTypeId);
        if (required != CaptiveLaborPermission.None
            && (state.laborPermissions & required) != 0)
        {
            return true;
        }

        reason = $"{WorkTaskCatalog.GetDisplayName(workTypeId)} 노역은 허용되지 않았습니다.";
        return false;
    }

    public bool HasSecureHousing(
        CharacterActor captive,
        out BuildableObject housing,
        out string reason)
    {
        housing = null;
        reason = "사용 가능한 감방이 없습니다.";
        if (captive == null || !gridProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        foreach (BuildableObject candidate in worldRegistry.Buildings
                     .Where(building => building != null
                         && !building.isDestroy
                         && building.BuildingData.GetCaptiveHousingAbility() != null)
                     .OrderBy(building => Manhattan(captive.GetNowXY(), building.centerPos)))
        {
            BuildingCaptiveHousingAbility ability =
                candidate.BuildingData.GetCaptiveHousingAbility();
            string housingId = GetHousingId(candidate);
            int assigned = captives.Count(state =>
                state.IsInCustody
                && string.Equals(
                    state.housingBuildingId,
                    housingId,
                    StringComparison.Ordinal));
            if (assigned >= ability.capacity)
            {
                reason = "감방 수용 인원이 가득 찼습니다.";
                continue;
            }

            if (!TryGetHousingRoom(grid, candidate, out RoomInstance room)
                || !room.IsUsable)
            {
                reason = "감방이 닫힌 정식 방이 아닙니다.";
                continue;
            }

            GridTraversalContext captiveContext =
                GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(captive));
            Door escapingDoor = room.Doors
                .OfType<Door>()
                .FirstOrDefault(door =>
                    doorAccessQuery.CanUse(door, captiveContext, out _));
            if (escapingDoor != null)
            {
                reason = "수용 대상이 감방 문을 사용할 수 있습니다.";
                continue;
            }

            if (!TryFindHousingCell(grid, room, candidate, out _))
            {
                reason = "감방 안에 포로를 둘 빈 칸이 없습니다.";
                continue;
            }

            housing = candidate;
            reason = string.Empty;
            return true;
        }

        return false;
    }

    public bool TryOrderCapture(
        CharacterActor subject,
        CharacterActor carrier,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsEligibleDownedIntruder(subject))
        {
            failureReason = "살아 있는 쓰러진 침입자만 포획할 수 있습니다.";
            return false;
        }

        if (carrier == null
            || carrier.IsDead
            || carrier.characterType != CharacterType.NPC
            || carrier.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            failureReason = "포로를 운반할 직원이나 사장이 필요합니다.";
            return false;
        }

        CaptiveState state = stateRuntime.EnsureCandidate(subject);
        if (state.status is CaptivityStatus.Escorting
            or CaptivityStatus.Confined
            or CaptivityStatus.Labor
            or CaptivityStatus.Interaction
            or CaptivityStatus.Performer)
        {
            failureReason = "이미 포획 절차가 진행 중입니다.";
            return false;
        }

        doorSubjectRegistry.SetCaptive(state.captiveId, true);
        if (!HasSecureHousing(subject, out BuildableObject housing, out failureReason)
            || !TryGetHousingRoom(
                gridProvider.TryGetGrid(out Grid grid) ? grid : null,
                housing,
                out RoomInstance room)
            || !TryFindHousingCell(grid, room, housing, out Vector2Int housingCell))
        {
            return false;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(carrier);
        bool alreadyRestrained =
            !string.IsNullOrWhiteSpace(state.assignedRestraintItemId)
            && state.assignedRestraintDurability > 0f;
        string restraintItemId = alreadyRestrained
            ? state.assignedRestraintItemId
            : ResolveCarriedRestraint(inventory);
        bool carrierHasRestraint = alreadyRestrained
            || !string.IsNullOrWhiteSpace(restraintItemId);
        WorldItemReservedStackQuantity restraintReservation = default;
        Vector2Int pickupPosition = default;
        if (!carrierHasRestraint
            && !TryReserveRestraint(
                carrier,
                out restraintReservation,
                out pickupPosition,
                out failureReason))
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "창고에 사용할 수 있는 구속구가 없습니다."
                : failureReason;
            return false;
        }

        state.status = CaptivityStatus.Stabilizing;
        state.reservedCarrierId = GetCharacterId(carrier);
        state.housingBuildingId = GetHousingId(housing);
        state.housingPosition = housingCell;
        state.capturePosition = subject.GetNowXY();
        state.restraintStackId = restraintReservation.StackId;
        state.restraintItemId = carrierHasRestraint
            ? restraintItemId
            : restraintReservation.ItemId;
        state.restraintQuantity = 1;
        state.restraintPickupPosition = pickupPosition;
        state.requiredInteractionWork = Mathf.Min(
            30f,
            8f + bodyHealthQuery.GetTotalBleeding(subject) * 40f);
        state.completedInteractionWork = 0f;
        state.lastResult = "현장 안정화 대기";

        AbilityCaptiveEscort escort =
            CaptivityAbilityAdapterFactory.EnsureEscort(
                carrier,
                escortRuntime,
                gameClock);
        if (escort == null)
        {
            FailEscort(state.captiveId, carrier, "호송 행동을 시작할 수 없습니다.");
            failureReason = state.lastResult;
            return false;
        }

        escort.StartEscort(state.captiveId);
        return true;
    }

    public bool CancelCapture(string captiveId, string reason)
    {
        CaptiveState state = FindState(captiveId);
        if (state == null || !state.IsInCustody)
        {
            return false;
        }

        CharacterActor carrier = FindActor(state.reservedCarrierId);
        FailEscort(captiveId, carrier, reason);
        state.status = CaptivityStatus.AwaitingCapture;
        return true;
    }

    public bool TrySetPolicy(
        string captiveId,
        string policyId,
        out string failureReason)
    {
        return policyRuntime.TrySetPolicy(captiveId, policyId, out failureReason);
    }

    public bool TryCreatePolicy(
        string displayName,
        out string policyId,
        out string failureReason)
    {
        return policyRuntime.TryCreatePolicy(
            displayName,
            out policyId,
            out failureReason);
    }

    public bool TryDuplicatePolicy(
        string sourcePolicyId,
        out string policyId,
        out string failureReason)
    {
        return policyRuntime.TryDuplicatePolicy(
            sourcePolicyId,
            out policyId,
            out failureReason);
    }

    public bool TryUpdatePolicy(
        CaptivePolicyData policy,
        out string failureReason)
    {
        return policyRuntime.TryUpdatePolicy(policy, out failureReason);
    }

    public bool TryDeletePolicy(
        string policyId,
        out string failureReason)
    {
        return policyRuntime.TryDeletePolicy(policyId, out failureReason);
    }

    public bool TrySetLaborPermissions(
        string captiveId,
        CaptiveLaborPermission permissions,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        if (state == null || !state.IsInCustody)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        CaptiveLaborPermission requestedPermissions =
            permissions & CaptiveLaborPermission.All;
        if (requestedPermissions != CaptiveLaborPermission.None
            && !state.CanLabor)
        {
            failureReason = "순응도 50 이상, 건강 40% 이상부터 노역을 허용할 수 있습니다.";
            return false;
        }

        CaptiveLaborPermission requested = requestedPermissions;
        if (requested == CaptiveLaborPermission.None)
        {
            if (!CancelLaborToolPreparation(state))
            {
                failureReason = "포로 작업 도구 소유권을 정리할 수 없습니다.";
                return false;
            }
            CaptivityDurableToolRuntime.TryReturnLaborTool(
                itemRuntime,
                state,
                state.housingPosition);
            ApplyLaborPermissions(state, CaptiveLaborPermission.None);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
            && state.assignedLaborToolDurability > 0f
            && state.laborToolAssignmentCompleted)
        {
            ApplyLaborPermissions(state, requested);
            return true;
        }

        string destinationId = CaptivityCareLaborInputOwnerAuthority
            .FormatLaborToolDestinationId(state.captiveId);
        CaptivityStatus previousStatus = state.status;
        string previousResult = state.lastResult;
        state.pendingLaborPermissions = requested;
        state.laborToolDestinationId = destinationId;
        state.status = CaptivityStatus.Confined;
        state.lastResult = "포로 작업 도구 운반 대기";
        if (!careLaborInputOwner.TryReconcileLive(
                captives,
                out failureReason))
        {
            state.pendingLaborPermissions = CaptiveLaborPermission.None;
            state.laborToolDestinationId = string.Empty;
            state.status = previousStatus;
            state.lastResult = previousResult;
            failureReason = "포로 작업 도구의 물리 소유권을 열 수 없습니다: "
                + failureReason;
            return false;
        }
        if (!itemRuntime.TryRequestItemDelivery(
                CaptivityItemDefinitions.PrisonerWorkKitItemId,
                1,
                state.housingPosition,
                destinationId,
                out int requestedAmount,
                out failureReason)
            || requestedAmount < 1)
        {
            state.pendingLaborPermissions = CaptiveLaborPermission.None;
            state.laborToolDestinationId = string.Empty;
            state.status = previousStatus;
            state.lastResult = previousResult;
            if (!careLaborInputOwner.TryReconcileLive(
                    captives,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Captive labor-tool delivery failed and exact owner rollback failed: "
                    + rollbackFailure);
            }
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "포로 작업 도구를 감방으로 운반할 수 없습니다."
                : failureReason;
            return false;
        }
        return true;
    }

    public bool TryStartInteraction(
        string captiveId,
        string interactionId,
        CharacterActor warden,
        BuildableObject facility,
        out string failureReason)
    {
        return interactionRuntime.TryStart(
            captiveId,
            interactionId,
            warden,
            facility,
            out failureReason);
    }

    public bool AdvanceInteraction(
        string captiveId,
        CharacterActor warden,
        float workAmount,
        out string status)
    {
        return interactionRuntime.Advance(
            captiveId,
            warden,
            workAmount,
            out status);
    }

    public bool TryRecruit(string captiveId, out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        CaptivePolicyData policy = policyRuntime.Find(state?.policyId);
        int currentDay = CurrentAbsoluteDay;
        bool eligible = state?.IsMinion == true
            ? MinionIntegrationRules.CanRecruitRehabilitated(
                state.trust,
                state.grudge,
                state.corruption,
                state.rehabilitationDays)
            : state?.IsInCustody == true
                && MinionIntegrationRules.CanRecruitDirectly(
                    state.trust,
                    state.grudge,
                    state.corruption,
                    state.capturedAbsoluteDay,
                    currentDay);
        if (state == null || actor == null || !eligible)
        {
            failureReason = state?.IsMinion == true
                ? "재사회화 15일, 신뢰 70 이상, 원한 30 이하, 타락 30 이하가 필요합니다."
                : "포획 10일, 신뢰 70 이상, 원한 30 이하, 타락 60 미만이 필요합니다.";
            return false;
        }

        if (policy?.allowRecruitment != true)
        {
            failureReason = "현재 수용 정책은 정식 영입을 허용하지 않습니다.";
            return false;
        }

        if (!TryPrepareTerminalPhysicalInputs(state, out failureReason))
        {
            return false;
        }
        if (!TryCommitSettlementStanding(
                state,
                actor,
                CharacterSettlementStanding.Resident,
                CaptivityStatus.Recruited,
                "정식 주민으로 영입됨",
                out failureReason))
        {
            return false;
        }
        PublishPrisonerDecision(actor, "recruit");
        return true;
    }

    public bool TryConvertToMinion(string captiveId, out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        CaptivePolicyData policy = policyRuntime.Find(state?.policyId);
        if (state == null
            || actor == null
            || !state.IsInCustody
            || !MinionIntegrationRules.CanConvertToMinion(
                state.corruption,
                state.capturedAbsoluteDay,
                CurrentAbsoluteDay))
        {
            failureReason = "포획 3일과 타락 80 이상이 필요합니다.";
            return false;
        }

        if (policy?.allowCorruption != true)
        {
            failureReason = "현재 수용 정책은 하수인 전환을 허용하지 않습니다.";
            return false;
        }

        if (!TryPrepareTerminalPhysicalInputs(state, out failureReason))
        {
            return false;
        }
        if (!TryCommitSettlementStanding(
                state,
                actor,
                CharacterSettlementStanding.Minion,
                CaptivityStatus.Minion,
                "하수인으로 전환됨",
                out failureReason))
        {
            return false;
        }
        ApplyMinionConversionConsequences(state, actor);
        PublishPrisonerDecision(actor, "convert-minion");
        return true;
    }

    public bool TryStartRehabilitation(
        string captiveId,
        CharacterActor warden,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor subject = FindActor(captiveId);
        if (state?.IsMinion != true || subject == null || subject.IsDead)
        {
            failureReason = "재사회화할 하수인을 찾을 수 없습니다.";
            return false;
        }
        if (state.lastRehabilitationAbsoluteDay == CurrentAbsoluteDay)
        {
            failureReason = "오늘 재사회화는 이미 끝냈습니다.";
            return false;
        }
        if (warden == null
            || warden.IsDead
            || warden.CurrentLifecycleState != CharacterLifecycleState.Active
            || !settlementStandings.IsFormalResident(warden)
            || ReferenceEquals(warden, subject)
            || !warden.TryGetAbility(out AbilityWork _))
        {
            failureReason = "재사회화를 맡을 정식 주민이 필요합니다.";
            return false;
        }
        if (facility == null
            || facility.isDestroy
            || facility.BuildingData.GetCaptiveHousingAbility()?.IsValid != true
            || !worldRegistry.Buildings.Contains(facility))
        {
            failureReason = "재사회화를 진행할 수용 시설이 필요합니다.";
            return false;
        }

        if (state.rehabilitationInProgress)
        {
            CharacterActor assigned = worldRegistry.AllCharacters
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        GetCharacterId(candidate),
                        state.reservedWardenId,
                        StringComparison.Ordinal));
            if (assigned != null
                && !assigned.IsDead
                && assigned.CurrentLifecycleState == CharacterLifecycleState.Active)
            {
                failureReason = "이미 재사회화 작업이 진행 중입니다.";
                return false;
            }
        }

        state.rehabilitationInProgress = true;
        state.reservedWardenId = GetCharacterId(warden);
        state.rehabilitationFacilityBuildingId = GetHousingId(facility);
        state.rehabilitationPosition = facility.centerPos;
        state.lastResult = state.completedRehabilitationWork > 0f
            ? $"재사회화 재개 {state.completedRehabilitationWork:0.#}/{MinionIntegrationRules.RehabilitationRequiredWork:0.#} WU"
            : "재사회화 작업 배정";
        warden.Brain?.RequestImmediateReplan(clearFailures: false);
        return true;
    }

    public bool AdvanceRehabilitation(
        string captiveId,
        CharacterActor warden,
        float approvedWork,
        out string status)
    {
        CaptiveState state = FindState(captiveId);
        if (state?.IsMinion != true
            || !state.rehabilitationInProgress
            || warden == null
            || !string.Equals(
                state.reservedWardenId,
                GetCharacterId(warden),
                StringComparison.Ordinal))
        {
            status = "배정된 재사회화 작업을 찾을 수 없습니다.";
            return false;
        }
        if (state.lastRehabilitationAbsoluteDay == CurrentAbsoluteDay)
        {
            status = "오늘 재사회화는 이미 끝냈습니다.";
            return false;
        }

        float work = Mathf.Max(0f, approvedWork);
        if (work <= 0f)
        {
            status = "승인된 작업량이 필요합니다.";
            return false;
        }

        float nextWork = state.completedRehabilitationWork + work;
        if (nextWork + 0.001f
            < MinionIntegrationRules.RehabilitationRequiredWork)
        {
            state.completedRehabilitationWork = nextWork;
            status = $"재사회화 {nextWork:0.#}/{MinionIntegrationRules.RehabilitationRequiredWork:0.#} WU";
            state.lastResult = status;
            return true;
        }

        if (survivalFood.TryConsumeStoredStock(
                StockCategory.Food,
                MinionIntegrationRules.RehabilitationFoodCost)
            < MinionIntegrationRules.RehabilitationFoodCost)
        {
            status = "재사회화에 쓸 음식 1개가 부족합니다.";
            return false;
        }

        state.completedRehabilitationWork = 0f;
        state.rehabilitationInProgress = false;
        state.reservedWardenId = string.Empty;
        state.rehabilitationFacilityBuildingId = string.Empty;
        state.rehabilitationPosition = default;
        state.lastRehabilitationAbsoluteDay = CurrentAbsoluteDay;
        state.rehabilitationDays++;
        state.trust = ClampStat(
            state.trust + MinionIntegrationRules.RehabilitationTrustDelta);
        state.grudge = ClampStat(
            state.grudge + MinionIntegrationRules.RehabilitationGrudgeDelta);
        state.corruption = ClampStat(
            state.corruption + MinionIntegrationRules.RehabilitationCorruptionDelta);
        status = $"재사회화 {state.rehabilitationDays}/{MinionIntegrationRules.RequiredRehabilitationDays}일 완료";
        state.lastResult = status;
        return true;
    }

    public bool TryBeginDailySocialEvaluation(
        string minionId,
        int absoluteDay,
        out CaptiveState state)
    {
        CaptiveState current = FindState(minionId);
        int day = Mathf.Max(0, absoluteDay);
        if (current?.IsMinion != true
            || current.lastMinionSocialAbsoluteDay == day)
        {
            state = current?.Clone();
            return false;
        }

        current.lastMinionSocialAbsoluteDay = day;
        state = current.Clone();
        return true;
    }

    public void RecordSocialConflict(string minionId, string result)
    {
        CaptiveState state = FindState(minionId);
        if (state?.IsMinion == true)
        {
            state.lastResult = string.IsNullOrWhiteSpace(result)
                ? "주민과 충돌함"
                : result.Trim();
        }
    }

    public bool TryBreakMinionControl(
        string minionId,
        string reason,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(minionId);
        CharacterActor actor = FindActor(minionId);
        if (state?.IsMinion != true || actor == null)
        {
            failureReason = "통제 이탈 대상인 하수인을 찾을 수 없습니다.";
            return false;
        }

        CharacterType previousActorType = actor.characterType;
        CharacterType previousIdentityType = actor.Identity?.CharacterType
            ?? previousActorType;
        CharacterLifecycleState previousLifecycle = actor.CurrentLifecycleState;
        bool previousAiPaused = actor.IsAiPaused();
        EmploymentStandingState employmentSnapshot =
            employmentStanding.CaptureStandingState(state.captiveId);
        CharacterSettlementStandingTransaction populationTransaction = null;
        try
        {
            populationTransaction = characterPopulation
                .BeginSettlementStandingTransition(
                    actor,
                    CharacterSettlementStanding.PreparedCandidate);
            employmentStanding.ApplyStanding(
                state.captiveId,
                CharacterSettlementStanding.PreparedCandidate);
            actor.characterType = CharacterType.Intruder;
            actor.Identity?.SetCharacterType(CharacterType.Intruder);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.SetAiPaused(false);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
            doorSubjectRegistry.SetCaptive(state.captiveId, false);
            characterPopulation.CompleteSettlementStandingTransition(
                populationTransaction);
            CaptivityStateTransitionRules.ClearRehabilitationState(state);
            state.status = CaptivityStatus.Escaped;
            state.lastResult = string.IsNullOrWhiteSpace(reason)
                ? "통제에서 벗어남"
                : reason.Trim();
            gameEventBus.Publish(new CaptiveEscapedEvent(
                state.captiveId,
                state.lastResult,
                betrayal: true));
            return true;
        }
        catch (Exception exception)
        {
            employmentStanding.RestoreStandingState(employmentSnapshot);
            if (populationTransaction?.IsActive == true)
            {
                characterPopulation.RollbackSettlementStandingTransition(
                    populationTransaction);
            }
            actor.characterType = previousActorType;
            actor.Identity?.SetCharacterType(previousIdentityType);
            actor.SetLifecycleState(previousLifecycle);
            actor.SetAiPaused(previousAiPaused);
            failureReason = "통제 이탈 상태를 적용하지 못했습니다: "
                + exception.Message;
            return false;
        }
    }

    public bool TryRansom(
        string captiveId,
        out int paidAmount,
        out string failureReason)
    {
        paidAmount = 0;
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        CaptivePolicyData policy = state != null
            ? policyRuntime.Find(state.policyId)
            : null;
        if (state == null || actor == null || !state.IsInCustody)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        if (policy?.allowRansom != true)
        {
            failureReason = "현재 수용 정책은 몸값 협상을 허용하지 않습니다.";
            return false;
        }

        if (state.status is CaptivityStatus.Escorting
            or CaptivityStatus.Interaction
            or CaptivityStatus.Performer
            or CaptivityStatus.EscapeAttempt)
        {
            failureReason = "현재 진행 중인 포로 작업을 먼저 끝내야 합니다.";
            return false;
        }

        if (!TryPrepareTerminalPhysicalInputs(state, out failureReason))
        {
            return false;
        }
        paidAmount = state.RansomValue;
        state.status = CaptivityStatus.Ransom;
        state.retaliationPressure = ClampStat(
            state.retaliationPressure + state.grudge * 0.35f);
        money.Add(
            paidAmount,
            new EconomyTransactionContext(
                EconomyTransactionKind.RansomIncome,
                state.captiveId,
                actor.Identity?.PersistentId ?? state.captiveId,
                "포로 몸값"));
        PublishPrisonerDecision(actor, "ransom");
        FinalizeReleaseCaptive(
            state,
            actor,
            $"몸값 {paidAmount:N0}을 받고 석방");
        gameEventBus.Publish(new CaptiveRansomedEvent(
            state.captiveId,
            paidAmount,
            state.retaliationPressure));
        return true;
    }

    public bool TryRelease(string captiveId, out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        if (state == null || actor == null || !state.IsInCustody)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }
        PublishPrisonerDecision(actor, "release");

        if (!TryPrepareTerminalPhysicalInputs(state, out failureReason))
        {
            return false;
        }
        FinalizeReleaseCaptive(state, actor, "석방됨");
        return true;
    }

    private void PublishPrisonerDecision(
        CharacterActor prisoner,
        string decisionId)
    {
        CharacterActor decider = worldRegistry.Characters
            .FirstOrDefault(value => value?.Identity?.IsOwner == true);
        if (!CharacterPersistentIdentity.TryGet(decider, out CharacterId deciderId)
            || !CharacterPersistentIdentity.TryGet(prisoner, out CharacterId prisonerId))
            return;
        int day = Mathf.Max(
            0,
            Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));
        gameEventBus.Publish(new PrisonerDecisionEvent(
            deciderId,
            prisonerId,
            decisionId,
            CharacterCommandOrigin.DirectPlayerOrder,
            day));
    }

    private bool TryCommitSettlementStanding(
        CaptiveState state,
        CharacterActor actor,
        CharacterSettlementStanding targetStanding,
        CaptivityStatus targetStatus,
        string result,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterType previousActorType = actor.characterType;
        CharacterType previousIdentityType = actor.Identity?.CharacterType
            ?? previousActorType;
        CharacterLifecycleState previousLifecycle = actor.CurrentLifecycleState;
        bool previousAiPaused = actor.IsAiPaused();
        bool previousDoorCaptive = state.IsInCustody;
        string captivityStateSnapshot =
            CaptivityStateTransitionRules.CaptureStateSnapshot(state);
        EmploymentStandingState employmentSnapshot =
            employmentStanding.CaptureStandingState(state.captiveId);
        CharacterSettlementStandingTransaction populationTransaction = null;
        try
        {
            populationTransaction = characterPopulation
                .BeginSettlementStandingTransition(actor, targetStanding);
            employmentStanding.ApplyStanding(state.captiveId, targetStanding);

            actor.characterType = CharacterType.NPC;
            actor.Identity?.SetCharacterType(CharacterType.NPC);
            actor.SetAiPaused(false);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            doorSubjectRegistry.SetCaptive(state.captiveId, false);
            state.status = targetStatus;
            state.lastResult = result ?? string.Empty;
            CaptivityStateTransitionRules.ClearCaptiveOnlyState(state);
            if (targetStanding != CharacterSettlementStanding.Minion)
            {
                CaptivityStateTransitionRules.ClearRehabilitationState(state);
            }
            characterPopulation.CompleteSettlementStandingTransition(
                populationTransaction);
            return true;
        }
        catch (Exception exception)
        {
            CaptivityStateTransitionRules.RestoreStateSnapshot(
                captivityStateSnapshot,
                state);
            employmentStanding.RestoreStandingState(employmentSnapshot);
            if (populationTransaction?.IsActive == true)
            {
                characterPopulation.RollbackSettlementStandingTransition(
                    populationTransaction);
            }
            actor.characterType = previousActorType;
            actor.Identity?.SetCharacterType(previousIdentityType);
            actor.SetAiPaused(previousAiPaused);
            actor.SetLifecycleState(previousLifecycle);
            doorSubjectRegistry.SetCaptive(
                state.captiveId,
                previousDoorCaptive);
            failureReason = "정착 신분 전환을 완료하지 못했습니다: "
                + exception.Message;
            return false;
        }
    }

    private void ApplyMinionConversionConsequences(
        CaptiveState state,
        CharacterActor converted)
    {
        foreach (CharacterActor resident in worldRegistry.AllCharacters
                     .Where(candidate => candidate != null
                         && candidate != converted
                         && !candidate.IsDead
                         && settlementStandings.IsFormalResident(candidate))
                     .OrderBy(
                         candidate => candidate.Identity?.PersistentId,
                         StringComparer.Ordinal))
        {
            moodPolicy.Apply(
                resident,
                "captivity:minion-conversion",
                MinionIntegrationRules.ConversionResidentMoodDelta,
                MinionIntegrationRules.ConversionResidentMoodDays,
                "하수인 전환을 지켜봄");
        }

        if (narratives.TryGet(
                new CharacterId(state.captiveId),
                out CharacterNarrativeSnapshot narrative)
            && !string.IsNullOrWhiteSpace(narrative.OriginFactionId))
        {
            factionCampaign.ApplyFactionChange(
                narrative.OriginFactionId,
                0,
                MinionIntegrationRules.OriginFactionGrievanceDelta,
                0);
        }
    }

    private void RestoreSettlementStandingProjection()
    {
        foreach (CaptiveState state in captives.Where(candidate =>
                     candidate?.IsMinion == true))
        {
            CharacterActor actor = FindActor(state.captiveId);
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            CharacterType previousActorType = actor.characterType;
            CharacterType previousIdentityType = actor.Identity?.CharacterType
                ?? previousActorType;
            CharacterLifecycleState previousLifecycle =
                actor.CurrentLifecycleState;
            bool previousAiPaused = actor.IsAiPaused();
            EmploymentStandingState employmentSnapshot =
                employmentStanding.CaptureStandingState(state.captiveId);
            CharacterSettlementStandingTransaction transition = null;
            try
            {
                transition = characterPopulation
                    .BeginSettlementStandingTransition(
                        actor,
                        CharacterSettlementStanding.Minion);
                employmentStanding.ApplyStanding(
                    state.captiveId,
                    CharacterSettlementStanding.Minion);
                actor.characterType = CharacterType.NPC;
                actor.Identity?.SetCharacterType(CharacterType.NPC);
                actor.SetAiPaused(false);
                actor.SetLifecycleState(CharacterLifecycleState.Active);
                doorSubjectRegistry.SetCaptive(state.captiveId, false);
                characterPopulation.CompleteSettlementStandingTransition(
                    transition);
                if (state.rehabilitationInProgress)
                {
                    worldRegistry.AllCharacters.FirstOrDefault(candidate =>
                            candidate != null
                            && string.Equals(
                                GetCharacterId(candidate),
                                state.reservedWardenId,
                                StringComparison.Ordinal))
                        ?.Brain?.RequestImmediateReplan(clearFailures: false);
                }
            }
            catch
            {
                employmentStanding.RestoreStandingState(employmentSnapshot);
                if (transition?.IsActive == true)
                {
                    characterPopulation.RollbackSettlementStandingTransition(
                        transition);
                }
                actor.characterType = previousActorType;
                actor.Identity?.SetCharacterType(previousIdentityType);
                actor.SetLifecycleState(previousLifecycle);
                actor.SetAiPaused(previousAiPaused);
                throw;
            }
        }
    }

    public bool TryTriggerBetrayal(
        string captiveId,
        string trigger,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        if (state == null || actor == null || !state.IsInCustody)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        if (!state.falseCompliance)
        {
            failureReason = "거짓 복종 상태가 아닙니다.";
            return false;
        }

        if (state.status is CaptivityStatus.Escorting
            or CaptivityStatus.Stabilizing
            or CaptivityStatus.AwaitingEscort
            or CaptivityStatus.Interaction
            or CaptivityStatus.EscapeAttempt)
        {
            failureReason = "현재 상태에서는 배신 행동을 시작할 수 없습니다.";
            return false;
        }

        float betrayalChance = Mathf.Clamp01(
            0.25f
            + state.grudge * 0.005f
            + state.escapeRisk * 0.003f);
        if (!random.Chance(betrayalChance))
        {
            failureReason = "배신할 기회를 엿보고 있습니다.";
            return false;
        }

        state.status = CaptivityStatus.Escaped;
        state.restrained = false;
        state.betrayalTrigger = string.IsNullOrWhiteSpace(trigger)
            ? "기회 포착"
            : trigger.Trim();
        state.retaliationPressure = ClampStat(
            state.retaliationPressure + 20f + state.grudge * 0.25f);
        state.lastResult = $"{state.betrayalTrigger} 중 복종을 깨고 배신";
        actor.characterType = CharacterType.Intruder;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.SetAiPaused(false);
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        gameEventBus.Publish(new CaptiveEscapedEvent(
            state.captiveId,
            state.betrayalTrigger,
            betrayal: true));
        return true;
    }

    public bool TryAssignPerformer(
        string captiveId,
        bool assigned,
        out string failureReason)
    {
        return performerRuntime.TryAssign(captiveId, assigned, out failureReason);
    }

    public void RecordPerformance(
        string captiveId,
        float fameGain,
        float skillGain,
        bool injured)
    {
        performerRuntime.Record(captiveId, fameGain, skillGain, injured);
    }

    public bool TryResolvePerformerMilestone(
        string captiveId,
        CaptivePerformerMilestoneChoice choice,
        out string failureReason)
    {
        return performerRuntime.TryResolveMilestone(
            captiveId,
            choice,
            out failureReason);
    }

    public bool TryGetEscortState(
        string captiveId,
        CharacterActor carrier,
        out CaptiveState captive,
        out CharacterActor subject,
        out string failureReason)
    {
        return escortRuntime.TryGetEscortState(
            captiveId,
            carrier,
            out captive,
            out subject,
            out failureReason);
    }

    public IDisposable BeginEscortPass(CharacterActor carrier, string captiveId)
    {
        return escortRuntime.BeginEscortPass(carrier, captiveId);
    }

    public bool TryPickupReservedRestraint(
        CaptiveState captive,
        CharacterActor carrier,
        out string failureReason)
    {
        return escortRuntime.TryPickupReservedRestraint(
            captive,
            carrier,
            out failureReason);
    }

    public float AdvanceStabilization(
        string captiveId,
        CharacterActor carrier,
        float workAmount)
    {
        return escortRuntime.AdvanceStabilization(captiveId, carrier, workAmount);
    }

    public bool TryBeginEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason)
    {
        return escortRuntime.TryBeginEscort(captiveId, carrier, out failureReason);
    }

    public bool TryCompleteEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason)
    {
        return escortRuntime.TryCompleteEscort(captiveId, carrier, out failureReason);
    }

    public void FailEscort(string captiveId, CharacterActor carrier, string reason)
    {
        escortRuntime.FailEscort(captiveId, carrier, reason);
    }

    public bool TryGetEscapeState(
        string captiveId,
        CharacterActor actor,
        out Vector2Int destination,
        out string failureReason)
    {
        return escapeRuntime.TryGetEscapeState(
            captiveId,
            actor,
            out destination,
            out failureReason);
    }

    public IDisposable BeginEscapePass(CharacterActor actor, string captiveId)
    {
        return escapeRuntime.BeginEscapePass(actor, captiveId);
    }

    public void CompleteEscape(string captiveId, CharacterActor actor)
    {
        escapeRuntime.CompleteEscape(captiveId, actor);
    }

    public void FailEscape(
        string captiveId,
        CharacterActor actor,
        string reason)
    {
        escapeRuntime.FailEscape(captiveId, actor, reason);
    }

    private void RecalculateCaptiveState(CaptiveState state)
    {
        if (state == null)
        {
            return;
        }

        float originLoyalty = narratives.TryGet(
                new CharacterId(state.captiveId),
                out CharacterNarrativeSnapshot narrative)
            && narrative.HasEnemyOrigin
                ? narrative.Loyalty
                : 50f;
        state.compliance = ClampStat(
            (100f - state.will) * 0.45f
            + state.fear * 0.35f
            + state.trust * 0.2f
            + (50f - originLoyalty) * 0.15f);
        state.escapeRisk = ClampStat(
            25f
            + state.grudge * 0.45f
            + (100f - state.fear) * 0.2f
            + (originLoyalty - 50f) * 0.1f
            - state.compliance * 0.25f
            - (!string.IsNullOrWhiteSpace(state.assignedRestraintItemId)
                && state.assignedRestraintDurability > 0f
                    ? 20f
                    : 0f));
        state.falseCompliance =
            state.compliance >= 50f
            && state.grudge >= 60f
            && state.trust < 35f;
        state.retaliationPressure = ClampStat(
            state.grudge * 0.6f
            + state.corruption * 0.15f
            + state.failedEscapeAttempts * 8f);
    }

    private void FinalizeReleaseCaptive(
        CaptiveState state,
        CharacterActor actor,
        string result)
    {
        state.status = CaptivityStatus.Released;
        state.restrained = false;
        state.lastResult = result ?? "석방됨";
        actor.characterType = CharacterType.Intruder;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.SetAiPaused(false);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        actor.GetAbility<AbilityMove>()?.StartSystemExitDungeon();
    }

    private int CurrentAbsoluteDay => Mathf.Max(
        0,
        Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));

    private bool TryPrepareTerminalPhysicalInputs(
        CaptiveState state,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!ReturnAssignedCaptivityTools(state))
        {
            failureReason = "포로 작업 도구 소유권을 정리할 수 없습니다.";
            return false;
        }

        CaptivityStatus previousStatus = state.status;
        state.status = CaptivityStatus.Released;
        bool retired = careLaborInputOwner.TryReconcileLive(
            captives,
            out failureReason);
        state.status = previousStatus;
        if (!retired)
        {
            failureReason = "포로 care/labor 물리 입력을 종결할 수 없습니다: "
                + failureReason;
        }
        return retired;
    }

    private bool TryGetHousingRoom(
        Grid grid,
        BuildableObject housing,
        out RoomInstance room)
    {
        room = null;
        if (grid == null || housing == null)
        {
            return false;
        }

        if (roomLayoutCache.TryGetRoom(grid, housing.centerPos, out room))
        {
            return true;
        }

        foreach (Vector2Int position in housing.buildPoses)
        {
            foreach (Vector2Int direction in CardinalDirections)
            {
                if (roomLayoutCache.TryGetRoom(grid, position + direction, out room))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryFindHousingCell(
        Grid grid,
        RoomInstance room,
        BuildableObject housing,
        out Vector2Int position)
    {
        position = default;
        if (grid == null || room == null)
        {
            return false;
        }

        HashSet<Vector2Int> occupied = housing != null
            ? new HashSet<Vector2Int>(housing.buildPoses)
            : new HashSet<Vector2Int>();
        foreach (Vector2Int candidate in room.Cells
                     .Where(cell => !occupied.Contains(cell) && grid.IsWalkable(cell))
                     .OrderBy(cell => housing != null
                         ? Manhattan(cell, housing.centerPos)
                         : 0))
        {
            position = candidate;
            return true;
        }

        return false;
    }

    private CharacterActor FindActor(string id)
    {
        string normalized = id?.Trim() ?? string.Empty;
        return worldRegistry.AllCharacters.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                GetCharacterId(actor),
                normalized,
                StringComparison.Ordinal));
    }

    private CaptiveState FindState(string id)
    {
        string normalized = id?.Trim() ?? string.Empty;
        return captives.FirstOrDefault(state =>
            state != null
            && string.Equals(
                state.captiveId,
                normalized,
                StringComparison.Ordinal));
    }

    private static bool IsEligibleDownedIntruder(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.characterType == CharacterType.Intruder
            && actor.CurrentLifecycleState == CharacterLifecycleState.Downed;
    }

    private static string ResolveCarriedRestraint(
        CharacterCarryInventory inventory)
    {
        if (inventory?.CountItem(
                CaptivityItemDefinitions.ReinforcedRestraintItemId) > 0)
        {
            return CaptivityItemDefinitions.ReinforcedRestraintItemId;
        }

        return inventory?.CountItem(CaptivityItemDefinitions.RestraintsItemId) > 0
            ? CaptivityItemDefinitions.RestraintsItemId
            : string.Empty;
    }

    private bool TryReserveRestraint(
        CharacterActor carrier,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupPosition,
        out string failureReason)
    {
        if (itemRuntime.TryReserveStoredItemForDirectPickup(
                carrier,
                CaptivityItemDefinitions.ReinforcedRestraintItemId,
                1,
                out reservation,
                out pickupPosition,
                out failureReason))
        {
            return true;
        }

        return itemRuntime.TryReserveStoredItemForDirectPickup(
            carrier,
            CaptivityItemDefinitions.RestraintsItemId,
            1,
            out reservation,
            out pickupPosition,
            out failureReason);
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor).Value
            : string.Empty;
    }

    private static string GetHousingId(BuildableObject building)
    {
        return building == null
            ? string.Empty
            : building.RequirePersistentInstanceId().Value;
    }

    private static float EstimateHealth(CharacterActor actor)
    {
        if (actor?.Stats == null)
        {
            return 0f;
        }

        return Mathf.Clamp(
            actor.Stats.CurrentHealth / Mathf.Max(1f, actor.Stats.MaxHealth) * 100f,
            0f,
            100f);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

    private static float ClampStat(float value)
    {
        return Mathf.Clamp(value, 0f, 100f);
    }

    private void RequireCareLaborInputOwner(string boundary)
    {
        if (careLaborInputOwner.TryReconcileLive(
                captives,
                out string failureReason))
        {
            return;
        }
        throw new InvalidOperationException(
            "Captivity care/labor input ownership failed at "
            + boundary + ": " + failureReason);
    }

    private static CaptiveLaborPermission GetLaborPermission(WorkTypeId workTypeId)
    {
        if (workTypeId == BuiltInWorkTypeIds.Clean)
        {
            return CaptiveLaborPermission.Clean;
        }

        if (workTypeId == BuiltInWorkTypeIds.Haul
            || workTypeId == BuiltInWorkTypeIds.Restock)
        {
            return CaptiveLaborPermission.Haul;
        }

        if (workTypeId == BuiltInWorkTypeIds.DrawWater)
        {
            return CaptiveLaborPermission.DrawWater;
        }

        if (workTypeId == BuiltInWorkTypeIds.Refuel)
        {
            return CaptiveLaborPermission.Refuel;
        }

        if (workTypeId == BuiltInWorkTypeIds.Construct)
        {
            return CaptiveLaborPermission.Construct;
        }

        if (workTypeId == BuiltInWorkTypeIds.Repair)
        {
            return CaptiveLaborPermission.Repair;
        }

        if (workTypeId == BuiltInWorkTypeIds.Butcher)
        {
            return CaptiveLaborPermission.Butcher;
        }

        if (workTypeId == BuiltInWorkTypeIds.Craft)
        {
            return CaptiveLaborPermission.CraftAssist;
        }

        return CaptiveLaborPermission.None;
    }

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.left,
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.down
    };
}

public static class CaptivityLaborToolAssignmentOutbox
{
    public const string TransferReason = "captive-labor-tool-transferred";

    public static string FormatOperationId(
        string captiveId,
        string itemInstanceId) =>
        CaptivityLaborToolAssignmentIdentity.FormatOperationId(
            captiveId,
            itemInstanceId);

    public static bool RequiresFinalization(CaptiveState state) =>
        state != null
        && !string.IsNullOrEmpty(state.laborToolAssignmentOperationId)
        && (!state.laborToolAssignmentCompleted
            || state.pendingLaborPermissions != CaptiveLaborPermission.None
            || !string.IsNullOrEmpty(state.laborToolDestinationId));

    public static bool TryFinalizePending(
        CaptiveState state,
        IPhysicalItemBatchDispositionService batchDispositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (state == null
            || batchDispositions == null
            || !IsCanonical(state.captiveId)
            || !string.Equals(
                state.assignedLaborToolItemId,
                CaptivityItemDefinitions.PrisonerWorkKitItemId,
                StringComparison.Ordinal)
            || !((ItemInstanceId)state.assignedLaborToolInstanceId).IsValid
            || state.assignedLaborToolDurability <= 0f
            || state.assignedLaborToolMaximumDurability <= 0f
            || !IsCanonical(state.laborToolAssignmentSourceStackId)
            || !string.Equals(
                state.laborToolAssignmentOperationId,
                FormatOperationId(
                    state.captiveId,
                    state.assignedLaborToolInstanceId),
                StringComparison.Ordinal)
            || !IsCanonical(state.laborToolAssignmentCommitId))
        {
            failureReason = "captive-labor-tool-assignment-invalid";
            return false;
        }

        if (!batchDispositions.TryGetPending(
                state.laborToolAssignmentOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            if (state.laborToolAssignmentCompleted)
            {
                return true;
            }
            failureReason = "captive-labor-tool-assignment-receipt-missing";
            return false;
        }

        if (receipt.Kind != PhysicalItemDispositionKind.Transfer
            || receipt.Quantity != 1
            || receipt.SourceStackIds.Count != 1
            || !string.Equals(
                receipt.OperationId,
                state.laborToolAssignmentOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                TransferReason,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.CommitId,
                state.laborToolAssignmentCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.SourceStackIds[0],
                state.laborToolAssignmentSourceStackId,
                StringComparison.Ordinal))
        {
            failureReason = "captive-labor-tool-assignment-receipt-mismatch";
            return false;
        }

        state.laborToolAssignmentCompleted = true;
        if (!batchDispositions.Acknowledge(
                state.laborToolAssignmentCommitId,
                out failureReason))
        {
            return false;
        }
        return true;
    }

    public static void ClearAssignment(CaptiveState state)
    {
        if (state == null)
        {
            return;
        }

        state.assignedLaborToolItemId = string.Empty;
        state.assignedLaborToolInstanceId = string.Empty;
        state.assignedLaborToolDurability = 0f;
        state.assignedLaborToolMaximumDurability = 0f;
        state.laborToolAssignmentOperationId = string.Empty;
        state.laborToolAssignmentCommitId = string.Empty;
        state.laborToolAssignmentSourceStackId = string.Empty;
        state.laborToolAssignmentCompleted = false;
        state.nextLaborToolWearAt = 0f;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
