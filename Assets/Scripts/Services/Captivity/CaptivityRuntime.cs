using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class CaptivityRuntime :
    ICaptivityRuntime,
    ICaptivityPersistence,
    ICaptivityRestoreCandidateSource,
    ICaptiveLaborQuery,
    ICaptivityWorkReadinessQuery,
    ICaptivityCommandService,
    ICaptivityEscortRuntime,
    ICaptivityEscapeRuntime,
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
    private readonly CaptivityActorAccess actorAccess;
    private readonly CaptivityPolicyRuntime policyRuntime;
    private readonly CaptivityPerformerRuntime performerRuntime;
    private readonly CaptivityInteractionRuntime interactionRuntime;
    private readonly CaptivityEscortRuntime escortRuntime;
    private readonly CaptivityEscapeRuntime escapeRuntime;
    private readonly CaptivityStateRuntime stateRuntime;
    private readonly CaptivityRestoreCoordinator restoreCoordinator;
    private readonly CaptivityQueryView queryView;
    private IDisposable downedSubscription;
    private IDisposable recoveredSubscription;
    private IDisposable deathSubscription;
    private IDisposable invasionSubscription;
    private IReadOnlyList<CaptiveState> captives => actorAccess.States;

    public CaptivityRuntime(
        CaptivityCharacterContext characters,
        CaptivityWorldContext world,
        CaptivitySessionContext session)
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
        characterPopulation = characters.Population;
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
        actorAccess = new CaptivityActorAccess(
            this.aggregateRootStore,
            RecalculateCaptiveState);
        CaptivityActorRuntimeLookup actorRuntime =
            new CaptivityActorRuntimeLookup(FindActor);
        CaptivityUnityEffectsAdapter captivityEffects =
            new CaptivityUnityEffectsAdapter(
                FindActor,
                itemRuntime,
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
                captivityEffects,
                captivityEffects));
        interactionRuntime = new CaptivityInteractionRuntime(
            actorAccess,
            actorRuntime,
            interactions,
            itemRuntime,
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
            doorSubjectRegistry);
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
            escortRuntime);
        queryView = new CaptivityQueryView(actorAccess, policyRuntime);
    }

    public IReadOnlyList<CaptiveState> Captives => queryView.Captives;
    public IReadOnlyList<CaptivePolicyData> Policies => queryView.Policies;
    public string ParticipantId => restoreCoordinator.ParticipantId;

    public CaptivitySaveData Capture() => stateRuntime.Capture();

    public CaptivityRestoreCandidate BuildRestore(CaptivitySaveData saveData) =>
        restoreCoordinator.BuildRestore(saveData);

    public void PublishRestoreCandidate(CaptivityRestoreCandidate candidate) =>
        restoreCoordinator.StageRestore(candidate);

    public bool TryTakePreparedRestoreCandidate(
        out CaptivityRestoreCandidate candidate) =>
        restoreCoordinator.TryTakePreparedRestoreCandidate(out candidate);

    public void BeginRestoreCandidate() =>
        restoreCoordinator.BeginRestoreCandidate();

    public void PublishRestoreCandidate() =>
        restoreCoordinator.PublishRestoreCandidate();

    public void RollbackPublishedRestoreCandidate() =>
        restoreCoordinator.RollbackPublishedRestoreCandidate();

    public void CompleteRestoreCandidate()
    {
        restoreCoordinator.CompleteRestoreCandidate();
        escapeRuntime.ClearPendingInvasionEscapes();
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
        if (gameClock.IsPaused || gameClock.DeltaTime <= 0f)
        {
            return;
        }

        escapeRuntime.TickPendingInvasionEscapes();

        for (int index = 0; index < captives.Count; index++)
        {
            CaptiveState state = captives[index];
            if (state == null
                || !state.IsActive
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

        string destinationId = $"captive-care:{state.captiveId}";
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
        if (delivered == null
            || !((ItemInstanceId)delivered.ItemInstanceId).IsValid
            || DurableToolItemRules.ReadCurrentDurability(
                delivered.ItemId,
                delivered.Components) <= 0f
            || !itemRuntime.TryConsumeStackQuantity(
                delivered.StackId,
                1,
                out WorldItemStackSnapshot assigned)
            || !CaptivityDurableToolRuntime.TryAssignLaborTool(state, assigned))
        {
            return;
        }

        CaptiveLaborPermission permissions = state.pendingLaborPermissions;
        state.pendingLaborPermissions = CaptiveLaborPermission.None;
        state.laborToolDestinationId = string.Empty;
        state.nextLaborToolWearAt = gameClock.Time + 60f;
        ApplyLaborPermissions(state, permissions);
    }

    private void TickLaborToolWear(
        CaptiveState state,
        CharacterActor actor)
    {
        if (state?.status != CaptivityStatus.Labor
            || string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
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

    private void CancelLaborToolPreparation(CaptiveState state)
    {
        if (state == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(state.laborToolDestinationId))
        {
            itemRuntime.ReleaseStacksByDestination(
                state.laborToolDestinationId,
                state.housingPosition);
        }
        state.pendingLaborPermissions = CaptiveLaborPermission.None;
        state.laborToolDestinationId = string.Empty;
    }

    private void ReturnAssignedCaptivityTools(CaptiveState state)
    {
        if (state == null)
        {
            return;
        }

        CancelLaborToolPreparation(state);
        CaptivityDurableToolRuntime.TryReturnLaborTool(
            itemRuntime,
            state,
            state.housingPosition);
        CaptivityDurableToolRuntime.TryReturnRestraint(
            itemRuntime,
            state,
            state.housingPosition);
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

    public bool IsCaptive(string persistentId)
    {
        CaptiveState state = FindState(persistentId);
        return state != null && state.IsActive;
    }

public bool IsWorkAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string reason)
    {
        reason = string.Empty;
        CaptiveState state = FindState(GetCharacterId(actor));
        if (state == null || !state.IsActive)
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
                state.IsActive
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
        if (state == null || !state.IsActive)
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
        if (state == null || !state.IsActive)
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
            CancelLaborToolPreparation(state);
            CaptivityDurableToolRuntime.TryReturnLaborTool(
                itemRuntime,
                state,
                state.housingPosition);
            ApplyLaborPermissions(state, CaptiveLaborPermission.None);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(state.assignedLaborToolItemId)
            && state.assignedLaborToolDurability > 0f)
        {
            ApplyLaborPermissions(state, requested);
            return true;
        }

        string destinationId = $"captive-labor-tool:{state.captiveId}";
        if (!itemRuntime.TryRequestItemDelivery(
                CaptivityItemDefinitions.PrisonerWorkKitItemId,
                1,
                state.housingPosition,
                destinationId,
                out int requestedAmount,
                out failureReason)
            || requestedAmount < 1)
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "포로 작업 도구를 감방으로 운반할 수 없습니다."
                : failureReason;
            return false;
        }

        state.pendingLaborPermissions = requested;
        state.laborToolDestinationId = destinationId;
        state.status = CaptivityStatus.Confined;
        state.lastResult = "포로 작업 도구 운반 대기";
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
        if (state == null || actor == null || !state.CanRecruit)
        {
            failureReason = "신뢰 70 이상, 원한 30 이하, 타락 60 미만이 필요합니다.";
            return false;
        }

        if (policy?.allowRecruitment != true)
        {
            failureReason = "현재 수용 정책은 정식 영입을 허용하지 않습니다.";
            return false;
        }

        characterPopulation.PromoteToStaff(actor);
        ReturnAssignedCaptivityTools(state);
        state.status = CaptivityStatus.Recruited;
        state.lastResult = "정식 직원으로 영입됨";
        actor.characterType = CharacterType.NPC;
        actor.Identity?.SetCharacterType(CharacterType.NPC);
        actor.SetAiPaused(false);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        PublishPrisonerDecision(actor, "recruit");
        return true;
    }

    public bool TryConvertToMinion(string captiveId, out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        CaptivePolicyData policy = policyRuntime.Find(state?.policyId);
        if (state == null || actor == null || !state.CanBecomeMinion)
        {
            failureReason = "타락 80 이상부터 하수인으로 전환할 수 있습니다.";
            return false;
        }

        if (policy?.allowCorruption != true)
        {
            failureReason = "현재 수용 정책은 하수인 전환을 허용하지 않습니다.";
            return false;
        }

        ReturnAssignedCaptivityTools(state);
        state.status = CaptivityStatus.Minion;
        state.lastResult = "타락한 하수인으로 전환됨";
        actor.characterType = CharacterType.NPC;
        actor.SetAiPaused(false);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        PublishPrisonerDecision(actor, "convert-minion");
        return true;
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
        if (state == null || actor == null || !state.IsActive)
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
        ReleaseCaptive(
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
        if (state != null && actor != null)
            PublishPrisonerDecision(actor, "release");
        if (state == null || actor == null)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        ReleaseCaptive(state, actor, "석방됨");
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

    public bool TryTriggerBetrayal(
        string captiveId,
        string trigger,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        if (state == null || actor == null || !state.IsActive)
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

    private void ReleaseCaptive(
        CaptiveState state,
        CharacterActor actor,
        string result)
    {
        ReturnAssignedCaptivityTools(state);
        state.status = CaptivityStatus.Released;
        state.restrained = false;
        state.lastResult = result ?? "석방됨";
        actor.characterType = CharacterType.Intruder;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.SetAiPaused(false);
        doorSubjectRegistry.SetCaptive(state.captiveId, false);
        actor.GetAbility<AbilityMove>()?.StartSystemExitDungeon();
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
