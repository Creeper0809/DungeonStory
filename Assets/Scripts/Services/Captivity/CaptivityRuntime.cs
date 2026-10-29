using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
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
    private readonly IGameCalendar gameCalendar;
    private readonly IGameEventBus gameEventBus;
    private readonly IRandomStream random;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IEmploymentStandingCommand employmentStanding;
    private readonly CharacterMoodPolicyService moodPolicy;
    private readonly IFactionCampaignCommand factionCampaign;
    private readonly ISurvivalFoodCommand survivalFood;
    private readonly ICharacterSettlementStandingQuery settlementStandings;
    private readonly CaptivityActorAccess actorAccess;
    private readonly CaptivityLaborRuntime laborRuntime;
    private readonly CaptivityDurableToolRuntime durableTools;
    private readonly CaptivityPolicyRuntime policyRuntime;
    private readonly CaptivityPerformerRuntime performerRuntime;
    private readonly CaptivePerformerMilestoneOutcomeRuntime
        performerMilestoneOutcomeRuntime;
    private readonly CaptivityInteractionRuntime interactionRuntime;
    private readonly CaptivityEscortRuntime escortRuntime;
    private readonly CaptiveEscapeOutcomeRuntime escapeOutcomeRuntime;
    private readonly CaptiveRansomOutcomeRuntime ransomOutcomeRuntime;
    private readonly IDomainFactStagingPublisher transitionFacts;
    private readonly IDomainFactRecoveryPublisher transitionRecovery;
    private bool transitionCommandActive;
    private readonly CaptivityDefectionRuntime defectionRuntime;
    private readonly CaptivityEscapeRuntime escapeRuntime;
    private readonly CaptivityRansomRuntime ransomRuntime;
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
    private float nextPerformerMilestoneRetryAt;
    private string lastPerformerMilestoneRetryFailure = string.Empty;
    private float nextEscapeOutcomeRetryAt;
    private string lastEscapeOutcomeRetryFailure = string.Empty;
    private float nextRansomOutcomeRetryAt;
    private string lastRansomOutcomeRetryFailure = string.Empty;
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
        gameCalendar = session.GameCalendar;
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
        durableTools = new CaptivityDurableToolRuntime();
        laborRuntime = new CaptivityLaborRuntime(
            actorAccess, actorRuntime, worldRegistry, bodyHealthQuery,
            itemRuntime, batchDispositions, this.careLaborInputOwner, gameClock,
            durableTools);
        CaptivityInterrogationInformationRuntime interrogationInformation =
            new CaptivityInterrogationInformationRuntime(
                narratives,
                characters.EnemyArchetypes,
                session.InterrogationCodex,
                gameEventBus);
        CaptivityUnityEffectsAdapter captivityEffects =
            new CaptivityUnityEffectsAdapter(
                FindActor,
                gameEventBus);
        policyRuntime = new CaptivityPolicyRuntime(
            actorAccess,
            new CaptivityPolicyActorDefaultPort(captivityEffects));
        ICaptivityPerformerPort performerPort =
            new CaptivityPerformerDefaultPort(
                captivityEffects,
                captivityEffects);
        performerRuntime = new CaptivityPerformerRuntime(
            FindState,
            policyRuntime.Find,
            TryRecruit,
            TryRelease,
            performerPort);
        performerMilestoneOutcomeRuntime =
            new CaptivePerformerMilestoneOutcomeRuntime(
                session.Facts,
                session.FactRecovery,
                performerPort);
        interactionRuntime = new CaptivityInteractionRuntime(
            actorAccess,
            actorRuntime,
            interactions,
            interrogationInformation,
            bodyHealthQuery,
            characters.BodyHealthMutations,
            new CaptivityInteractionOutcomeRuntime(
                session.Facts,
                session.FactRecovery,
                characters.PhysicalItemSources),
            gameCalendar,
            interactionMaterials,
            TryGetHousing);
        escortRuntime = new CaptivityEscortRuntime(
            actorAccess,
            actorRuntime,
            durableTools,
            characters,
            world,
            session);
        escapeOutcomeRuntime = new CaptiveEscapeOutcomeRuntime(
            session.Facts,
            session.FactRecovery,
            gameEventBus);
        ransomOutcomeRuntime = new CaptiveRansomOutcomeRuntime(
            session.Facts,
            session.FactRecovery,
            gameEventBus,
            worldRegistry);
        transitionFacts = session.Facts;
        transitionRecovery = session.FactRecovery;
        defectionRuntime = new CaptivityDefectionRuntime(
            actorAccess,
            actorRuntime,
            random,
            doorSubjectRegistry,
            new CaptivityPopulationStandingPort(characterPopulation),
            employmentStanding,
            gameCalendar,
            escapeOutcomeRuntime);
        stateRuntime = new CaptivityStateRuntime(
            actorAccess,
            policyRuntime,
            interactionRuntime,
            escortRuntime,
            doorSubjectRegistry,
            gameCalendar);
        escapeRuntime = new CaptivityEscapeRuntime(
            actorAccess,
            actorRuntime,
            world,
            session,
            TryTriggerBetrayal,
            escapeOutcomeRuntime);
        ransomRuntime = new CaptivityRansomRuntime(
            actorAccess,
            actorRuntime,
            policyRuntime,
            bodyHealthQuery,
            money,
            new CaptivityRansomTerminalPhysicalPort(
                TryPrepareTerminalPhysicalInputs),
            doorSubjectRegistry,
            gameCalendar,
            ransomOutcomeRuntime);
        restoreCoordinator = new CaptivityRestoreCoordinator(
            this.worldRegistry,
            this.itemRuntime,
            this.interactions,
            this.aggregateRootStore,
            actorAccess,
            doorSubjectRegistry,
            escortRuntime,
            batchDispositions,
            world.RestoreWorldCandidates);
        queryView = new CaptivityQueryView(actorAccess, policyRuntime);
    }

    public IReadOnlyList<CaptiveState> Captives => queryView.Captives;
    public IReadOnlyList<CaptivePolicyData> Policies => queryView.Policies;
    public string ParticipantId => restoreCoordinator.ParticipantId;

    public CaptivitySaveData Capture()
    {
        durableTools.ValidateBeforeCapture();
        ValidateCaptivityTransitionCapture();
        performerMilestoneOutcomeRuntime.ValidateBeforeCapture();
        escapeOutcomeRuntime.ValidateBeforeCapture();
        ransomOutcomeRuntime.ValidateBeforeCapture();
        interactionRuntime.ValidateBeforeCapture();
        RequireCareLaborInputOwner("capture");
        interactionMaterialLifecycle.ValidateBeforeCapture(Captives);
        return stateRuntime.Capture();
    }

    public CaptivityRestoreCandidate BuildRestore(CaptivitySaveData saveData)
    {
        durableTools.ValidateBeforeCapture();
        ValidateCaptivityTransitionCapture();
        performerMilestoneOutcomeRuntime.ValidateBeforeCapture();
        escapeOutcomeRuntime.ValidateBeforeCapture();
        ransomOutcomeRuntime.ValidateBeforeCapture();
        interactionRuntime.ValidateBeforeCapture();
        return restoreCoordinator.BuildRestore(saveData);
    }

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
        // A restored earlier game clock must not inherit a retry deadline from the discarded world.
        nextPerformerMilestoneRetryAt = 0f;
        lastPerformerMilestoneRetryFailure = string.Empty;
        nextEscapeOutcomeRetryAt = 0f;
        lastEscapeOutcomeRetryFailure = string.Empty;
        escapeRuntime.ClearPendingInvasionEscapes();
        nextRansomOutcomeRetryAt = 0f;
        lastRansomOutcomeRetryFailure = string.Empty;
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
                if (state != null && !laborRuntime.ReturnAssignedCaptivityTools(state))
                {
                    Debug.LogWarning($"Captive '{state.captiveId}' died while tool return is pending; the assigned instances are retained for retry.");
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
        TickCaptivityTransitionOutcomeRecovery();
        TickRansomOutcomeRecovery();
        if (!aggregateRootStore.IsRestoreStaging)
            laborRuntime.RetryDeadCaptiveTools();
        RequireCareLaborInputOwner("tick");
        interactionRuntime.RetryPendingOutcomes(captives);
        if (gameClock.IsPaused || gameClock.DeltaTime <= 0f)
        {
            return;
        }

        TickPerformerMilestoneRecovery();

        TickEscapeOutcomeRecovery();

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

            laborRuntime.Tick(state, actor);
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

    private void TickPerformerMilestoneRecovery()
    {
        if (aggregateRootStore.IsRestoreStaging
            || gameClock.Time + 0.001f < nextPerformerMilestoneRetryAt)
        {
            return;
        }

        nextPerformerMilestoneRetryAt = gameClock.Time + 5f;
        bool success = performerMilestoneOutcomeRuntime.TryCommitNextPendingMilestone(
            captives,
            CurrentAbsoluteDay,
            out _,
            out string failureReason);
        if (success)
        {
            lastPerformerMilestoneRetryFailure = string.Empty;
            return;
        }
        if (string.Equals(
                failureReason,
                lastPerformerMilestoneRetryFailure,
                StringComparison.Ordinal))
        {
            return;
        }
        lastPerformerMilestoneRetryFailure = failureReason;
        Debug.LogWarning(
            "공연자 이정표 원장 기록을 재시도합니다: " + failureReason);
    }

    private void TickEscapeOutcomeRecovery()
    {
        if (aggregateRootStore.IsRestoreStaging
            || gameClock.Time + 0.001f < nextEscapeOutcomeRetryAt)
        {
            return;
        }

        nextEscapeOutcomeRetryAt = gameClock.Time + 5f;
        bool success = escapeRuntime.TryCommitNextPendingPhysicalEscape(
            captives,
            out _,
            out string failureReason);
        if (success)
        {
            lastEscapeOutcomeRetryFailure = string.Empty;
            return;
        }
        if (string.Equals(
                failureReason,
                lastEscapeOutcomeRetryFailure,
                StringComparison.Ordinal))
        {
            return;
        }
        lastEscapeOutcomeRetryFailure = failureReason;
        Debug.LogWarning(
            "포로 탈출 원장 기록을 재시도합니다: " + failureReason);
    }

    private void TickRansomOutcomeRecovery()
    {
        if (aggregateRootStore.IsRestoreStaging
            || gameClock.Time + 0.001f < nextRansomOutcomeRetryAt)
        {
            return;
        }

        nextRansomOutcomeRetryAt = gameClock.Time + 5f;
        bool success = ransomRuntime.TryFinalizeNextPending(
            captives,
            out _,
            out string failureReason);
        if (success)
        {
            lastRansomOutcomeRetryFailure = string.Empty;
            return;
        }
        if (string.Equals(
                failureReason,
                lastRansomOutcomeRetryFailure,
                StringComparison.Ordinal))
        {
            return;
        }
        lastRansomOutcomeRetryFailure = failureReason;
        Debug.LogWarning(
            "포로 몸값 마무리를 재시도합니다: " + failureReason);
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
        CharacterActor actor, WorkTypeId workTypeId, out string reason) =>
        laborRuntime.IsWorkAllowed(actor, workTypeId, out reason);

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

        if (carrier.GetComponent<AbilityCaptiveEscort>()?.IsEscorting == true
            || carrier.GetComponent<AbilityWildlifeCaptureTransport>()?.IsTransporting == true)
        {
            failureReason = "운반자가 이미 다른 포획 대상을 운반 중입니다.";
            return false;
        }

        CaptiveState state = stateRuntime.EnsureCandidate(subject);
        if (state.status is CaptivityStatus.Stabilizing
            or CaptivityStatus.Escorting
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

        return escort.TryStartEscort(state.captiveId, out failureReason);
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
        string captiveId, CaptiveLaborPermission permissions, out string failureReason) =>
        laborRuntime.TrySetLaborPermissions(captiveId, permissions, out failureReason);

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
        if (transitionCommandActive) { failureReason = "captivity-transition-command-active"; return false; }
        transitionCommandActive = true;
        try { return TryStartRehabilitationCore(captiveId, warden, facility, out failureReason); }
        finally { transitionCommandActive = false; }
    }

    private bool TryStartRehabilitationCore(
        string captiveId,
        CharacterActor warden,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor subject = FindActor(captiveId);
        if (state?.rehabilitationTerminal?.outcomePending == true
            && !TryCommitPendingRehabilitation(state, out failureReason))
        {
            failureReason = "이전 재사회화 결과를 원장에 전달하는 중입니다: "
                + failureReason;
            return false;
        }
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

    public bool AdvanceRehabilitation(string captiveId, CharacterActor warden, float approvedWork, out string status)
    {
        if (transitionCommandActive) { status = "captivity-transition-command-active"; return false; }
        transitionCommandActive = true;
        try { return AdvanceRehabilitationCore(captiveId, warden, approvedWork, out status); }
        finally { transitionCommandActive = false; }
    }

    private bool AdvanceRehabilitationCore(
        string captiveId,
        CharacterActor warden,
        float approvedWork,
        out string status)
    {
        CaptiveState state = FindState(captiveId);
        if (state?.rehabilitationTerminal?.outcomePending == true)
        {
            bool delivered = TryCommitPendingRehabilitation(
                state,
                out status);
            if (!delivered)
                status = "재사회화 결과 원장 전달 대기: " + status;
            return delivered;
        }
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
        if (!float.IsFinite(approvedWork) || work <= 0f)
        {
            status = "승인된 작업량이 필요합니다.";
            return false;
        }

        float workBefore = state.completedRehabilitationWork;
        float acceptedWork = Mathf.Min(
            work,
            Mathf.Max(0f,
                MinionIntegrationRules.RehabilitationRequiredWork - workBefore));
        float accumulated = workBefore + acceptedWork;
        bool completesDay = accumulated + 0.001f
            >= MinionIntegrationRules.RehabilitationRequiredWork;
        long nextRevision;
        try
        {
            nextRevision = checked(state.rehabilitationOutcomeRevision + 1L);
        }
        catch (OverflowException)
        {
            status = "재사회화 결과 순번이 범위를 초과했습니다.";
            return false;
        }

        BuildableObject facility = worldRegistry.Buildings.FirstOrDefault(value =>
            value != null
            && string.Equals(
                GetHousingId(value),
                state.rehabilitationFacilityBuildingId,
                StringComparison.Ordinal));
        if (facility == null || facility.isDestroy)
        {
            status = "재사회화 시설을 다시 확인해야 합니다.";
            return false;
        }

        CaptivityRehabilitationTerminalState terminal = new()
        {
            outcomeRevision = nextRevision,
            outcomePending = true,
            captiveDisplayName = CaptureDisplayName(state.displayName, state.captiveId),
            wardenId = GetCharacterId(warden),
            wardenDisplayName = CaptureDisplayName(
                warden.Identity?.DisplayName,
                GetCharacterId(warden)),
            facilityId = GetHousingId(facility),
            facilityDisplayName = CaptureDisplayName(
                facility.BuildingData?.objectName,
                GetHousingId(facility)),
            absoluteDay = CurrentAbsoluteDay,
            completedDay = completesDay,
            approvedWork = acceptedWork,
            workBefore = workBefore,
            workAfter = completesDay ? 0f : accumulated,
            rehabilitationDaysBefore = state.rehabilitationDays,
            rehabilitationDaysAfter = state.rehabilitationDays
                + (completesDay ? 1 : 0),
            trustBefore = state.trust,
            trustAfter = completesDay
                ? ClampStat(state.trust
                    + MinionIntegrationRules.RehabilitationTrustDelta)
                : state.trust,
            grudgeBefore = state.grudge,
            grudgeAfter = completesDay
                ? ClampStat(state.grudge
                    + MinionIntegrationRules.RehabilitationGrudgeDelta)
                : state.grudge,
            corruptionBefore = state.corruption,
            corruptionAfter = completesDay
                ? ClampStat(state.corruption
                    + MinionIntegrationRules.RehabilitationCorruptionDelta)
                : state.corruption,
            foodConsumed = completesDay
                ? MinionIntegrationRules.RehabilitationFoodCost
                : 0
        };
        CaptivityRehabilitationProgressedFact fact;
        try { fact = new CaptivityRehabilitationProgressedFact(state.captiveId, terminal); }
        catch (ArgumentException exception)
        { status = "captivity-rehabilitation-result-invalid:" + exception.Message; return false; }
        if (!TryStageCaptivityTransition(fact, nextRevision, out IDomainFactStage prepared, out status))
        {
            status = "재사회화 결과 전달을 준비할 수 없습니다: " + status;
            return false;
        }
        terminal.publicationKey = prepared.OperationKey;
        try
        {
            if (completesDay
                && survivalFood.TryConsumeStoredStock(
                    StockCategory.Food,
                    MinionIntegrationRules.RehabilitationFoodCost)
                    != MinionIntegrationRules.RehabilitationFoodCost)
            {
                status = "재사회화에 쓸 음식 1개가 부족합니다.";
                return false;
            }

            state.rehabilitationTerminal = terminal;
            state.completedRehabilitationWork = terminal.workAfter;
            state.rehabilitationDays = terminal.rehabilitationDaysAfter;
            state.trust = terminal.trustAfter;
            state.grudge = terminal.grudgeAfter;
            state.corruption = terminal.corruptionAfter;
            if (completesDay)
            {
                state.rehabilitationInProgress = false;
                state.reservedWardenId = string.Empty;
                state.rehabilitationFacilityBuildingId = string.Empty;
                state.rehabilitationPosition = default;
                state.lastRehabilitationAbsoluteDay = CurrentAbsoluteDay;
            }
            status = completesDay
                ? $"재사회화 {state.rehabilitationDays}/{MinionIntegrationRules.RequiredRehabilitationDays}일 완료"
                : $"재사회화 {accumulated:0.#}/{MinionIntegrationRules.RehabilitationRequiredWork:0.#} WU";
            state.lastResult = status;

            DomainFactDelivery delivery = CommitCaptivityTransition(prepared);
            if (IsCaptivityTransitionDelivered(terminal.publicationKey, delivery))
            {
                state.rehabilitationOutcomeRevision = nextRevision;
                terminal.outcomePending = false;
            }
            else
            {
                // Food and approved work already belong to the game state. Retry delivery only.
                state.lastResult += " · 결과 전달 대기(" + delivery.Detail + ")";
            }
        }
        finally { CancelCaptivityTransitionStage(prepared); }
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
        out string failureReason) => defectionRuntime.TryBreakMinionControl(
        minionId,
        reason,
        out failureReason);

    public bool TryRansom(
        string captiveId,
        out int paidAmount,
        out string failureReason) => ransomRuntime.TryRansom(
        captiveId,
        out paidAmount,
        out failureReason);

    public bool TryRelease(string captiveId, out string failureReason)
    {
        if (transitionCommandActive) { failureReason = "captivity-transition-command-active"; return false; }
        transitionCommandActive = true;
        try { return TryReleaseCore(captiveId, out failureReason); }
        finally { transitionCommandActive = false; }
    }

    private bool TryReleaseCore(string captiveId, out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindState(captiveId);
        CharacterActor actor = FindActor(captiveId);
        if (state?.releaseTerminal?.outcomePending == true || state?.releaseTerminal?.observersPending == true)
            return TryFinalizePendingRelease(state, actor, out failureReason);
        if (state == null || actor == null || !state.IsInCustody)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }
        CharacterActor decider = worldRegistry.Characters
            .FirstOrDefault(value => value?.Identity?.IsOwner == true);
        if (!CharacterPersistentIdentity.TryGet(decider, out CharacterId deciderId))
        {
            failureReason = "석방 결정을 내린 소유자를 찾을 수 없습니다.";
            return false;
        }
        BuildableObject facility = worldRegistry.Buildings.FirstOrDefault(value =>
            value != null
            && string.Equals(
                GetHousingId(value),
                state.housingBuildingId,
                StringComparison.Ordinal));
        CaptivityReleaseTerminalState terminal = new()
        {
            outcomeRevision = 1L,
            outcomePending = true,
            observersPending = true,
            captiveDisplayName = CaptureDisplayName(state.displayName, state.captiveId),
            deciderId = deciderId.Value,
            deciderDisplayName = CaptureDisplayName(
                decider.Identity?.DisplayName,
                deciderId.Value),
            facilityId = state.housingBuildingId,
            facilityDisplayName = facility != null
                ? CaptureDisplayName(
                    facility.BuildingData?.objectName,
                    state.housingBuildingId)
                : string.Empty,
            absoluteDay = CurrentAbsoluteDay,
            statusBefore = state.status,
            statusAfter = CaptivityStatus.Released,
            inCustodyBefore = state.IsInCustody,
            inCustodyAfter = false,
            restrainedBefore = state.restrained,
            restrainedAfter = false,
            actorTypeBefore = actor.characterType.ToString(),
            actorTypeAfter = CharacterType.Intruder.ToString(),
            lifecycleBefore = actor.CurrentLifecycleState.ToString(),
            lifecycleAfter = CharacterLifecycleState.Active.ToString(),
            aiPausedBefore = actor.IsAiPaused(),
            aiPausedAfter = false,
            doorCaptiveBefore = state.IsInCustody,
            doorCaptiveAfter = false,
            physicalInputsReturned = true
        };
        CaptivityReleasedFact fact;
        try { fact = new CaptivityReleasedFact(state.captiveId, terminal); }
        catch (ArgumentException exception)
        { failureReason = "captivity-release-result-invalid:" + exception.Message; return false; }
        if (!TryStageCaptivityTransition(fact, 1L, out IDomainFactStage prepared, out failureReason))
        {
            failureReason = "포로 석방 결과 전달을 준비할 수 없습니다: " + failureReason;
            return false;
        }
        terminal.publicationKey = prepared.OperationKey;
        try
        {
            if (!TryPrepareTerminalPhysicalInputs(state, out failureReason)) return false;
            state.releaseTerminal = terminal;
            if (!TryApplyReleaseState(state, actor, out failureReason)) return false;
            terminal.effectsApplied = true;
            DomainFactDelivery delivery = CommitCaptivityTransition(prepared);
            if (!IsCaptivityTransitionDelivered(terminal.publicationKey, delivery))
            {
                failureReason = "포로 석방 결과 전달 대기: " + delivery.Detail;
                state.lastResult = "석방됨 · " + failureReason;
                return true;
            }
            CompleteReleaseOutcomeDelivery(state, actor);
            return true;
        }
        finally { CancelCaptivityTransitionStage(prepared); }
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
        int day = gameCalendar.Day;
        gameEventBus.Publish(new PrisonerDecisionEvent(
            deciderId,
            prisonerId,
            decisionId,
            CharacterCommandOrigin.DirectPlayerOrder,
            day));
    }

    private void TickCaptivityTransitionOutcomeRecovery()
    {
        if (transitionCommandActive) return;
        transitionCommandActive = true;
        try
        {
            foreach (CaptiveState state in captives
                         .Where(value => value != null)
                         .OrderBy(value => value.captiveId, StringComparer.Ordinal))
            {
                if (state.rehabilitationTerminal?.outcomePending == true
                    && !TryCommitPendingRehabilitation(
                        state,
                        out string rehabilitationFailure))
                {
                    Debug.LogWarning(
                        "재사회화 결과 원장 재전달 실패: "
                        + rehabilitationFailure);
                }
                if (state.releaseTerminal?.outcomePending == true
                    || state.releaseTerminal?.observersPending == true)
                {
                    CharacterActor actor = FindActor(state.captiveId);
                    if (!TryFinalizePendingRelease(
                            state,
                            actor,
                            out string releaseFailure))
                    {
                        Debug.LogWarning(
                            "포로 석방 결과 재전달 실패: " + releaseFailure);
                    }
                }
            }
        }
        finally { transitionCommandActive = false; }
    }

    private bool TryCommitPendingRehabilitation(CaptiveState state, out string failureReason)
    {
        failureReason = string.Empty;
        var terminal = state?.rehabilitationTerminal;
        if (state == null || terminal?.outcomePending != true) return true;
        try
        {
            var fact = new CaptivityRehabilitationProgressedFact(state.captiveId, terminal);
            if (!TryReplayCaptivityTransition(fact, terminal.publicationKey, out failureReason)) return false;
            state.rehabilitationOutcomeRevision = terminal.outcomeRevision;
            terminal.outcomePending = false;
            return true;
        }
        catch (ArgumentException exception)
        { failureReason = "captivity-rehabilitation-frozen-result-invalid:" + exception.Message; return false; }
    }

    private bool TryFinalizePendingRelease(CaptiveState state, CharacterActor actor, out string failureReason)
    {
        failureReason = string.Empty;
        var terminal = state?.releaseTerminal;
        if (state == null || terminal?.HasOutcome != true)
        { failureReason = "captivity-release-result-missing"; return false; }
        if (!terminal.effectsApplied)
        {
            if (actor == null) { failureReason = "captivity-release-actor-missing"; return false; }
            if (!TryApplyReleaseState(state, actor, out failureReason)) return false;
            terminal.effectsApplied = true;
        }
        if (terminal.outcomePending)
        {
            try
            {
                var fact = new CaptivityReleasedFact(state.captiveId, terminal);
                if (!TryReplayCaptivityTransition(fact, terminal.publicationKey, out failureReason)) return false;
            }
            catch (ArgumentException exception)
            { failureReason = "captivity-release-frozen-result-invalid:" + exception.Message; return false; }
            state.releaseOutcomeRevision = terminal.outcomeRevision;
            terminal.outcomePending = false;
        }
        if (terminal.observersPending)
        {
            if (actor == null) { failureReason = "captivity-release-notification-actor-missing"; return false; }
            DeliverReleaseObservers(state, actor);
        }
        if (terminal.observersPending) failureReason = "captivity-release-notification-pending";
        return !terminal.observersPending;
    }

    private void CompleteReleaseOutcomeDelivery(
        CaptiveState state,
        CharacterActor actor)
    {
        state.releaseOutcomeRevision = state.releaseTerminal.outcomeRevision;
        state.releaseTerminal.outcomePending = false;
        DeliverReleaseObservers(state, actor);
    }

    private bool TryApplyReleaseState(
        CaptiveState state,
        CharacterActor actor,
        out string failureReason)
    {
        try
        {
            FinalizeReleaseCaptive(state, actor, "석방됨");
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            failureReason = "포로 석방 상태 적용을 재시도합니다: "
                + exception.Message;
            return false;
        }
    }

    private void DeliverReleaseObservers(
        CaptiveState state,
        CharacterActor actor)
    {
        if (state?.releaseTerminal?.observersPending != true)
            return;
        try
        {
            CaptivityReleaseTerminalState terminal = state.releaseTerminal;
            if (!terminal.decisionPublished)
            {
                gameEventBus.Publish(new PrisonerDecisionEvent(
                    new CharacterId(terminal.deciderId),
                    new CharacterId(state.captiveId),
                    "release",
                    CharacterCommandOrigin.DirectPlayerOrder,
                    terminal.absoluteDay));
                terminal.decisionPublished = true;
            }
            actor.GetAbility<AbilityMove>()?.StartSystemExitDungeon();
            terminal.observersPending = false;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "확정된 포로 석방 관찰자 전달이 실패했습니다: " + exception);
        }
    }

    private void ValidateCaptivityTransitionCapture()
    {
        if (transitionCommandActive) throw new InvalidOperationException("captivity-transition-command-active");
    }

    private bool TryStageCaptivityTransition<TFact>(TFact fact, long revision, out IDomainFactStage stage, out string failure)
    {
        stage = null; failure = string.Empty;
        IDomainFactStage candidate = null;
        try
        {
            if (!transitionFacts.TryStage(fact, out candidate, out failure)) return false;
            if (candidate == null || !candidate.IsActive || candidate.OwnerRevision != revision
                || string.IsNullOrWhiteSpace(candidate.OperationKey) || candidate.IsReplay)
            { failure = "captivity-transition-unapplied-stage-conflict"; return false; }
            stage = candidate; candidate = null; return true;
        }
        catch (Exception exception) when (IsCaptivityTransitionRecoverable(exception))
        { failure = "captivity-transition-stage:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { CancelCaptivityTransitionStage(candidate); }
    }

    private static DomainFactDelivery CommitCaptivityTransition(IDomainFactStage stage)
    {
        try { return stage.Commit(); }
        catch (Exception exception) when (IsCaptivityTransitionRecoverable(exception))
        { return DomainFactDelivery.Uncertain("captivity-transition-delivery:" + exception.GetType().Name); }
    }

    private bool TryReplayCaptivityTransition<TFact>(TFact fact, string key, out string failure)
    {
        failure = string.Empty;
        try
        {
            var results = new DomainFactDelivery[1];
            if (!transitionRecovery.ReplayBatch(new[] { DomainFactReplay.Create(key, fact) }, results, out failure)
                || !IsCaptivityTransitionDelivered(key, results[0]))
            { failure = "captivity-transition-delivery-pending:" + failure + ":" + results[0].Detail; return false; }
            return true;
        }
        catch (Exception exception) when (IsCaptivityTransitionRecoverable(exception))
        { failure = "captivity-transition-recovery:" + exception.GetType().Name + ":" + exception.Message; return false; }
    }

    private static bool IsCaptivityTransitionDelivered(string key, DomainFactDelivery delivery)
    {
        if (!delivery.Completed || delivery.OperationKey != key || string.IsNullOrWhiteSpace(delivery.PublicationId)
            || delivery.Fingerprint?.Length != 64) return false;
        foreach (char c in delivery.Fingerprint)
            if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')) return false;
        return true;
    }
    private static bool IsCaptivityTransitionRecoverable(Exception value) => value is not OutOfMemoryException
        && value is not StackOverflowException && value is not AccessViolationException;
    private static void CancelCaptivityTransitionStage(IDomainFactStage stage)
    {
        try { stage?.Cancel(); }
        catch (Exception exception) when (IsCaptivityTransitionRecoverable(exception))
        { Debug.LogWarning("captivity-transition-cancel:" + exception); }
    }

    private static string CaptureDisplayName(string displayName, string fallback)
    {
        string value = displayName?.Trim() ?? string.Empty;
        return value.Length > 0 ? value : fallback;
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
        out string failureReason) => defectionRuntime.TryTriggerBetrayal(
        captiveId,
        trigger,
        out failureReason);

    public bool CanAssignPerformer(string captiveId, out string failureReason) =>
        performerRuntime.CanAssign(captiveId, out failureReason);

    public bool TryAssignPerformer(
        string captiveId,
        bool assigned,
        out string failureReason)
    {
        return performerRuntime.TryAssign(captiveId, assigned, out failureReason);
    }

    public bool RecordPerformance(
        string captiveId,
        float fameGain,
        float skillGain,
        bool injured,
        out string failureReason)
    {
        if (!performerRuntime.TryRecord(
                captiveId,
                fameGain,
                skillGain,
                injured,
                out failureReason))
        {
            return false;
        }
        return performerMilestoneOutcomeRuntime.TryCommitEligibleMilestones(
            FindState(captiveId),
            CurrentAbsoluteDay,
            out failureReason);
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

    public bool CompleteEscape(
        string captiveId,
        CharacterActor actor,
        string trigger,
        out string failureReason) => escapeRuntime.CompleteEscape(
        captiveId,
        actor,
        trigger,
        out failureReason);

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
    }

    private int CurrentAbsoluteDay => gameCalendar.Day;

    private bool TryPrepareTerminalPhysicalInputs(
        CaptiveState state,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!laborRuntime.ReturnAssignedCaptivityTools(state))
        {
            failureReason = "포로 작업 도구 소유권을 정리할 수 없습니다.";
            return false;
        }

        CaptivityStatus previousStatus = state.status;
        state.status = CaptivityStatus.Released;
        bool retired;
        try { retired = careLaborInputOwner.TryReconcileLive(captives, out failureReason); }
        finally { state.status = previousStatus; }
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

    private float GetHealthPercent(CharacterActor actor)
    {
        if (actor == null)
        {
            return 0f;
        }

        CharacterVitalsSnapshot vitals = bodyHealthQuery.GetVitals(actor);
        return CaptivityBodyHealthRules.GetHealthPercent(
            vitals.CurrentHealth,
            vitals.MaximumHealth);
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
