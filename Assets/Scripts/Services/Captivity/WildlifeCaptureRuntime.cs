using System;
using DungeonStory.Foundation.Facts;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class WildlifeCaptureWorldContext
{
    public WildlifeCaptureWorldContext(
        ICharacterAiWorldRegistry world,
        IRoomLayoutCache rooms,
        IGridSystemProvider gridProvider,
        IGridPathSearchBroker pathSearchBroker,
        IDoorAccessCommandService doorAccessCommands,
        IDoorAccessSubjectRegistry doorSubjects)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        GridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        PathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        DoorAccessCommands = doorAccessCommands
            ?? throw new ArgumentNullException(nameof(doorAccessCommands));
        DoorSubjects = doorSubjects
            ?? throw new ArgumentNullException(nameof(doorSubjects));
    }

    public ICharacterAiWorldRegistry World { get; }
    public IRoomLayoutCache Rooms { get; }
    public IGridSystemProvider GridProvider { get; }
    public IGridPathSearchBroker PathSearchBroker { get; }
    public IDoorAccessCommandService DoorAccessCommands { get; }
    public IDoorAccessSubjectRegistry DoorSubjects { get; }
}

public sealed class WildlifeCaptureCareContext
{
    public WildlifeCaptureCareContext(
        IWorldItemStackRuntime itemRuntime,
        IWorldFilthQuery filth,
        IResourceEconomyContentCatalog contentCatalog,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IWasteFeedCommand wasteProcessing,
        IWasteFeedCandidateQuery wasteFeedCandidates,
        IPhysicalItemBatchDispositionService batchDispositions,
        IWildlifeHaulItemRuntime wildlifeHaulItems)
    {
        ItemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        Filth = filth ?? throw new ArgumentNullException(nameof(filth));
        ContentCatalog = contentCatalog
            ?? throw new ArgumentNullException(nameof(contentCatalog));
        SpeciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
        WasteProcessing = wasteProcessing
            ?? throw new ArgumentNullException(nameof(wasteProcessing));
        WasteFeedCandidates = wasteFeedCandidates
            ?? throw new ArgumentNullException(nameof(wasteFeedCandidates));
        BatchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
        WildlifeHaulItems = wildlifeHaulItems
            ?? throw new ArgumentNullException(nameof(wildlifeHaulItems));
    }

    public IWorldItemStackRuntime ItemRuntime { get; }
    public IWorldFilthQuery Filth { get; }
    public IResourceEconomyContentCatalog ContentCatalog { get; }
    public IWildlifeSpeciesCatalogProvider SpeciesCatalog { get; }
    public IWasteFeedCommand WasteProcessing { get; }
    public IWasteFeedCandidateQuery WasteFeedCandidates { get; }
    public IPhysicalItemBatchDispositionService BatchDispositions { get; }
    public IWildlifeHaulItemRuntime WildlifeHaulItems { get; }
}

public sealed class WildlifeCaptureSessionContext
{
    public WildlifeCaptureSessionContext(
        IGameClock clock,
        IGameCalendar calendar,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher factRecovery,
        IRandomStreamProvider randomStreamProvider,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        Facts = facts ?? throw new ArgumentNullException(nameof(facts));
        FactRecovery = factRecovery ?? throw new ArgumentNullException(nameof(factRecovery));
        RandomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        AggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IGameClock Clock { get; }
    public IGameCalendar Calendar { get; }
    public IDomainFactPublisher Facts { get; }
    public IDomainFactRecoveryPublisher FactRecovery { get; }
    public IRandomStreamProvider RandomStreamProvider { get; }
    public DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
}

public sealed partial class WildlifeCaptureRuntime :
    IWildlifeCaptureRuntime,
    IWildlifeCaptureTransportRuntime,
    IWildlifeCompanionRoleQuery,
    IWildlifeCompanionRoleCommand,
    IWildlifeCompanionAffiliationQuery,
    IWildlifeCompanionRoleStateCommand,
    IWildlifeHaulRoleQuery,
    IWildlifeHaulRoleCommand,
    IWildlifeHaulRoleStateCommand,
    ITickable
{

    private readonly ICharacterAiWorldRegistry world;
    private readonly IRoomLayoutCache rooms;
    private readonly IGridSystemProvider gridProvider;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly IDoorAccessCommandService doorAccessCommands;
    private readonly IDoorAccessSubjectRegistry doorSubjects;
    private readonly IWorldFilthQuery filth;
    private readonly IResourceEconomyContentCatalog contentCatalog;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IWildlifeHaulItemRuntime wildlifeHaulItems;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly DungeonRuntimeAggregateRootStore sessionAggregateRootStore;
    private readonly CapturedWildlifeStateSession stateSession;
    private readonly CapturedWildlifeFeedingRuntime feeding;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher factRecovery;
    private readonly IGameCalendar calendar;
    private IWildlifeGameplayOutcomeOwner gameplayOutcomeOwner;
    private WildlifeGameplayOutcomeRuntime gameplayOutcomes;
    private readonly Dictionary<string, Transform> carriedParents =
        new Dictionary<string, Transform>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> nextPenReturnPathAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly List<CapturedWildlifeState> tickBuffer =
        new List<CapturedWildlifeState>();

    public WildlifeCaptureRuntime(
        WildlifeCaptureWorldContext worldContext,
        WildlifeCaptureCareContext care,
        WildlifeCaptureSessionContext session)
    {
        worldContext = worldContext
            ?? throw new ArgumentNullException(nameof(worldContext));
        care = care ?? throw new ArgumentNullException(nameof(care));
        session = session ?? throw new ArgumentNullException(nameof(session));
        world = worldContext.World;
        rooms = worldContext.Rooms;
        gridProvider = worldContext.GridProvider;
        pathSearchBroker = worldContext.PathSearchBroker;
        doorAccessCommands = worldContext.DoorAccessCommands;
        doorSubjects = worldContext.DoorSubjects;
        filth = care.Filth;
        contentCatalog = care.ContentCatalog;
        speciesCatalog = care.SpeciesCatalog;
        wildlifeHaulItems = care.WildlifeHaulItems;
        clock = session.Clock;
        calendar = session.Calendar;
        facts = session.Facts;
        factRecovery = session.FactRecovery;
        sessionAggregateRootStore = session.AggregateRootStore;
        stateSession = new CapturedWildlifeStateSession(sessionAggregateRootStore);
        RebindGameplayOutcomeRuntime();
        random = session.RandomStreamProvider
            .Get("captivity.wildlife-care");
        feeding = new CapturedWildlifeFeedingRuntime(stateSession, world, care, random);
    }

    public IReadOnlyList<CapturedWildlifeState> CapturedAnimals =>
        stateSession.Capture();

    public int NextGameplayOutcomeSequence =>
        stateSession.NextGameplayOutcomeSequence;

    public IReadOnlyList<WildlifeGameplayOutcomeSaveData>
        CaptureGameplayOutcomes() => stateSession.CaptureGameplayOutcomes();

    public void CopyCapturedAnimalReferences(
        List<CapturedWildlifeState> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        foreach (CapturedWildlifeState state in stateSession.Values)
        {
            destination.Add(state);
        }
    }

    public bool IsCaptured(string wildlifeId)
    {
        return !string.IsNullOrWhiteSpace(wildlifeId)
            && stateSession.TryGet(
                wildlifeId.Trim(),
                out CapturedWildlifeState state)
            && !state.escaped;
    }

    public void Tick()
    {
        EnsureProjectionCurrent();
        EnsureGameplayOutcomeRuntimeCurrent();
        gameplayOutcomes.AdvancePending();
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        if (stateSession.Count == 0)
        {
            nextPenReturnPathAt.Clear();
            return;
        }

        tickBuffer.Clear();
        foreach (CapturedWildlifeState state in stateSession.Values)
        {
            tickBuffer.Add(state);
        }
        foreach (string stale in nextPenReturnPathAt.Keys
                     .Where(id => !stateSession.TryGet(id, out _))
                     .ToArray())
        {
            nextPenReturnPathAt.Remove(stale);
        }

        for (int index = 0; index < tickBuffer.Count; index++)
        {
            CapturedWildlifeState state = tickBuffer[index];
            WildlifeActor actor = FindActor(state.wildlifeId);
            if (actor == null || !actor.IsAlive)
            {
                nextPenReturnPathAt.Remove(state.wildlifeId);
                continue;
            }

            if (state.transportState == CapturedWildlifeTransportState.Escaped)
            {
                nextPenReturnPathAt.Remove(state.wildlifeId);
                TickEscapingAnimal(state, actor);
                continue;
            }

            if (state.transportState != CapturedWildlifeTransportState.Penned)
            {
                nextPenReturnPathAt.Remove(state.wildlifeId);
                continue;
            }

            if (!TryReadRole(state, out CapturedWildlifeRoleId roleId))
            {
                throw new InvalidOperationException(
                    $"Captured wildlife '{state.wildlifeId}' has an invalid role state.");
            }
            bool returningToPen = roleId.Equals(CapturedWildlifeRoleIds.None)
                && actor.GridPosition != state.penPosition;
            if (returningToPen)
            {
                if (!nextPenReturnPathAt.TryGetValue(
                        state.wildlifeId,
                        out float nextPathAt)
                    || clock.Time + 0.0001f >= nextPathAt)
                {
                    nextPenReturnPathAt[state.wildlifeId] = clock.Time + 0.5f;
                    actor.RequestManagedCaptivePath(
                        state.penPosition,
                        clock.Time);
                }
            }
            else
            {
                nextPenReturnPathAt.Remove(state.wildlifeId);
            }

            actor.AdvanceCaptiveNeeds(
                clock.DeltaTime,
                hungerPerSecond: 1f / 300f,
                thirstPerSecond: 1f / 220f);
            if (clock.Time + 0.001f < state.nextCareAt)
            {
                continue;
            }

            state.nextCareAt = clock.Time + CapturedWildlifeState.CareIntervalSeconds;
            TickAnimalCare(state, actor);
        }
    }

    public bool TryCapture(
        WildlifeActor wildlife,
        BuildableObject pen,
        out string failureReason)
    {
        CharacterActor carrier = world.Characters
            .Where(actor =>
                actor != null
                && !actor.IsDead
                && actor.characterType == CharacterType.NPC
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                && actor.TryGetAbility(out AbilityMove _))
            .OrderBy(actor => wildlife != null
                ? Manhattan(actor.GetNowXY(), wildlife.GridPosition)
                : int.MaxValue)
            .FirstOrDefault();
        if (carrier == null)
        {
            failureReason = "생포한 동물을 운반할 직원이 없습니다.";
            return false;
        }

        return TryOrderCapture(wildlife, carrier, pen, out failureReason);
    }

    public bool TryOrderCapture(
        WildlifeActor wildlife,
        CharacterActor carrier,
        BuildableObject pen,
        out string failureReason)
    {
        failureReason = string.Empty;
        BuildingBeastPenAbility penAbility = pen?.BuildingData.GetBeastPenAbility();
        if (wildlife == null || !wildlife.IsAlive)
        {
            failureReason = "살아 있는 야생동물이 아닙니다.";
            return false;
        }

        if (wildlife.CurrentHealth > wildlife.MaxHealth * 0.35f)
        {
            failureReason = "생포하려면 야생동물을 먼저 제압해야 합니다.";
            return false;
        }

        if (carrier == null
            || carrier.IsDead
            || carrier.characterType != CharacterType.NPC
            || carrier.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            failureReason = "생포한 동물을 운반할 직원이 필요합니다.";
            return false;
        }

        if (pen == null || penAbility == null || !penAbility.IsValid)
        {
            failureReason = "유효한 야수 우리가 아닙니다.";
            return false;
        }

        string penId = GetPenId(pen);
        int occupied = stateSession.Values.Count(item =>
            !item.escaped
            && string.Equals(item.penId, penId, StringComparison.Ordinal));
        if (occupied >= penAbility.capacity)
        {
            failureReason = "야수 우리의 수용량이 가득 찼습니다.";
            return false;
        }

        if (!rooms.TryGetRoom(pen, out RoomInstance room) || !room.IsUsable)
        {
            failureReason = "야수 우리는 닫힌 정식 방 안에 있어야 합니다.";
            return false;
        }

        if (room.Doors.OfType<Door>().Any(CaptiveWildlifeCanUse))
        {
            failureReason = "포획 동물이 사용할 수 있는 문이 있어 안전한 우리가 아닙니다.";
            return false;
        }

        if (!gridProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "그리드를 찾을 수 없습니다.";
            return false;
        }

        HashSet<Vector2Int> penFootprint = new HashSet<Vector2Int>(
            pen.buildPoses ?? Array.Empty<Vector2Int>());
        List<Vector2Int> destinations = room.Cells
            .Where(cell => grid.IsWalkable(cell)
                && !penFootprint.Contains(cell)
                && IsUnoccupiedDeliveryStand(grid.GetGridCell(cell)))
            .OrderBy(cell => Manhattan(cell, pen.centerPos))
            .ToList();
        if (destinations.Count == 0)
        {
            failureReason = "우리 안에 동물을 놓을 수 있는 칸이 없습니다.";
            return false;
        }

        Vector2Int destination = destinations[0];
        CapturedWildlifeState state = new CapturedWildlifeState
        {
            wildlifeId = wildlife.WildlifeId,
            speciesId = wildlife.SpeciesId,
            penId = penId,
            penPosition = destination,
            capturePosition = wildlife.GridPosition,
            reservedCarrierId = GetCharacterId(carrier),
            transportState = CapturedWildlifeTransportState.AwaitingTransport
            ,
            nextCareAt = clock.Time + CapturedWildlifeState.CareIntervalSeconds,
            lastCareStatus = "우리 수용 준비"
        };
        AbilityWildlifeCaptureTransport transport =
            CaptivityAbilityAdapterFactory.EnsureWildlifeTransport(
                carrier,
                this);
        if (transport == null)
        {
            failureReason = "동물 운반 행동을 시작할 수 없습니다.";
            return false;
        }

        if (!TryReserveCaptureAction(
                state,
                wildlife,
                carrier,
                "capture-order",
                stateBefore: (int)CapturedWildlifeTransportState.AwaitingTransport,
                stateAfter: (int)CapturedWildlifeTransportState.AwaitingTransport,
                reasonCode: "capture-transport-ordered",
                out WildlifeGameplayOutcomeSaveData outcome,
                out DomainFactLease reserved,
                out failureReason))
        {
            failureReason = "생포 명령 결과 관찰을 예약하지 못했습니다: "
                + failureReason;
            return false;
        }

        try
        {
            stateSession.Set(state);
            doorSubjects.SetCapturedWildlife(state.wildlifeId, true);
            transport.StartTransport(state.wildlifeId);
        }
        catch (Exception exception)
        {
            stateSession.Remove(state.wildlifeId);
            doorSubjects.SetCapturedWildlife(state.wildlifeId, false);
            gameplayOutcomes.Cancel(reserved);
            failureReason = "동물 운반 행동을 시작할 수 없습니다: "
                + exception.GetType().Name;
            return false;
        }

        outcome.flagBefore = false;
        outcome.flagAfter = true;
        CommitCaptureOutcome(outcome, reserved);
        return true;
    }

    public bool TryGetCaptured(
        string wildlifeId,
        out CapturedWildlifeState state)
    {
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (stateSession.TryGet(id, out CapturedWildlifeState found))
        {
            state = found.Clone();
            return true;
        }

        state = null;
        return false;
    }

    public bool TrySetTamed(
        string wildlifeId,
        bool tamed,
        out string failureReason)
    {
        failureReason = string.Empty;
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!stateSession.TryGet(id, out CapturedWildlifeState state))
        {
            failureReason = "우리에서 관리 중인 동물을 찾을 수 없습니다.";
            return false;
        }

        state.isTamed = tamed;
        if (tamed)
        {
            state.escapeRisk = Mathf.Min(state.escapeRisk, 12f);
            state.lastCareStatus = "길들임 완료";
        }
        else if (TryReadRole(state, out CapturedWildlifeRoleId roleId)
            && roleId.Equals(CapturedWildlifeRoleIds.Companion))
        {
            ClearCompanionRole(state, "길들임 해제로 동행 역할 취소");
        }

        return true;
    }

    public bool TryGetPenCapacity(string penId, out int capacity)
    {
        BuildingBeastPenAbility ability = FindPen(penId)
            ?.BuildingData
            .GetBeastPenAbility();
        capacity = ability != null && ability.IsValid
            ? Mathf.Max(1, ability.capacity)
            : 0;
        return capacity > 0;
    }

    public bool TryRegisterPenBorn(
        WildlifeActor wildlife,
        string penId,
        Vector2Int penPosition,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedPenId = penId?.Trim() ?? string.Empty;
        BuildableObject pen = FindPen(normalizedPenId);
        BuildingBeastPenAbility penAbility =
            pen?.BuildingData.GetBeastPenAbility();
        if (wildlife == null || !wildlife.IsAlive)
        {
            failureReason = "태어난 동물 개체가 유효하지 않습니다.";
            return false;
        }

        if (pen == null || penAbility == null || !penAbility.IsValid)
        {
            failureReason = "새끼를 수용할 유효한 우리가 없습니다.";
            return false;
        }

        int occupied = stateSession.Values.Count(state =>
            !state.escaped
            && string.Equals(state.penId, normalizedPenId, StringComparison.Ordinal));
        if (occupied >= penAbility.capacity)
        {
            failureReason = "우리 수용량이 부족해 새끼를 등록할 수 없습니다.";
            return false;
        }

        CapturedWildlifeState state = new CapturedWildlifeState
        {
            wildlifeId = wildlife.WildlifeId,
            speciesId = wildlife.SpeciesId,
            penId = normalizedPenId,
            penPosition = penPosition,
            capturePosition = penPosition,
            transportState = CapturedWildlifeTransportState.Penned,
            nextCareAt = clock.Time + CapturedWildlifeState.CareIntervalSeconds,
            isTamed = true,
            lastCareStatus = "우리에서 태어난 새끼"
        };
        stateSession.Set(state);
        doorSubjects.SetCapturedWildlife(state.wildlifeId, true);
        wildlife.WarpTo(penPosition);
        wildlife.SetCaptured(true);
        return true;
    }

    public bool TryRelease(string wildlifeId, out string failureReason)
    {
        failureReason = string.Empty;
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!stateSession.TryGet(id, out CapturedWildlifeState state))
        {
            failureReason = "포획 동물을 찾을 수 없습니다.";
            return false;
        }
        if (TryGetHaul(id, out WildlifeHaulAssignmentSnapshot haul))
        {
            if (haul.Phase is CapturedWildlifeHaulPhase.CargoOwned
                    or CapturedWildlifeHaulPhase.ReleasePending)
            {
                TryClearHaul(id, out _);
                failureReason = "보유 화물을 배송한 뒤 방생할 수 있습니다.";
                return false;
            }
        }

        CharacterActor relatedActor = null;
        if (CapturedWildlifeCapabilityStateCodec.TryReadCompanion(
                state.capabilityState,
                out CharacterId ownerId,
                out _,
                out _))
        {
            relatedActor = FindOwner(ownerId);
        }
        if (!TryReserveCaptureCommand(
                state,
                "capture-release",
                flagBefore: true,
                flagAfter: false,
                reasonCode: "captured-wildlife-released",
                relatedActor: relatedActor,
                out WildlifeGameplayOutcomeSaveData outcome,
                out DomainFactLease reserved,
                out failureReason))
        {
            failureReason = "방생 결과 관찰을 예약하지 못했습니다: "
                + failureReason;
            return false;
        }
        outcome.stateAfter = (int)CapturedWildlifeTransportState.Released;
        if (TryGetHaul(id, out WildlifeHaulAssignmentSnapshot reservedHaul)
            && reservedHaul.Phase == CapturedWildlifeHaulPhase.Reserved)
        {
            wildlifeHaulItems.ReleaseUnpicked(reservedHaul);
        }
        if (!stateSession.Remove(id, out state))
        {
            gameplayOutcomes.Cancel(reserved);
            failureReason = "포획 동물 상태가 변경되었습니다.";
            return false;
        }

        doorSubjects.SetCapturedWildlife(id, false);
        WildlifeActor actor = FindActor(id);
        if (actor != null && carriedParents.Remove(id, out Transform parent))
        {
            actor.EndManagedCarry(actor.GridPosition, parent);
        }
        actor?.SetCaptured(false);
        if (actor != null)
        {
            actor.SatisfyCaptiveNeeds(0.15f, 0.15f);
        }
        outcome.stateAfter = (int)CapturedWildlifeTransportState.Released;
        CommitCaptureOutcome(outcome, reserved);
        return true;
    }

    public bool TryAssignToShow(
        string wildlifeId,
        string orderId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!stateSession.TryGet(id, out CapturedWildlifeState state))
        {
            failureReason = "포획 동물을 찾을 수 없습니다.";
            return false;
        }

        if (state.transportState != CapturedWildlifeTransportState.Penned)
        {
            failureReason = "우리 수용이 끝난 동물만 공연에 편성할 수 있습니다.";
            return false;
        }

        if (!TryReadRole(state, out CapturedWildlifeRoleId roleId)
            || !roleId.Equals(CapturedWildlifeRoleIds.None))
        {
            failureReason = "역할 수행 중인 동물은 공연에 편성할 수 없습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(state.assignedShowOrderId)
            && !string.Equals(
                state.assignedShowOrderId,
                orderId,
                StringComparison.Ordinal))
        {
            failureReason = "이미 다른 공연에 편성된 동물입니다.";
            return false;
        }

        state.assignedShowOrderId = orderId?.Trim() ?? string.Empty;
        state.transportState = CapturedWildlifeTransportState.MovingToShow;
        return true;
    }

    public void CompleteShowAssignment(string wildlifeId, string orderId)
    {
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!stateSession.TryGet(id, out CapturedWildlifeState state)
            || !string.Equals(
                state.assignedShowOrderId,
                orderId?.Trim(),
                StringComparison.Ordinal))
        {
            return;
        }

        state.assignedShowOrderId = string.Empty;
        state.transportState = CapturedWildlifeTransportState.Penned;
    }

    public bool TryGetTransportState(
        string wildlifeId,
        CharacterActor carrier,
        out CapturedWildlifeState state,
        out WildlifeActor wildlife,
        out string failureReason)
    {
        string id = wildlifeId?.Trim() ?? string.Empty;
        stateSession.TryGet(id, out CapturedWildlifeState found);
        wildlife = FindActor(id);
        state = found;
        failureReason = string.Empty;
        string carrierId = GetCharacterId(carrier);
        bool reservationMatches = found != null
            && string.Equals(
                found.reservedCarrierId,
                carrierId,
                StringComparison.Ordinal);
        if (found == null
            || wildlife == null
            || !wildlife.IsAlive
            || carrier == null
            || carrier.IsDead
            || !carrier.isActiveAndEnabled
            || carrier.CurrentLifecycleState != CharacterLifecycleState.Active
            || !reservationMatches)
        {
            failureReason = "동물 운반 예약이 유효하지 않습니다."
                + $" state={found != null};wildlife={wildlife != null};"
                + $"alive={wildlife?.IsAlive};carrier={carrier != null};"
                + $"dead={carrier?.IsDead};active={carrier?.isActiveAndEnabled};"
                + $"lifecycle={carrier?.CurrentLifecycleState};"
                + $"reserved={found?.reservedCarrierId ?? string.Empty};"
                + $"actual={carrierId}";
            return false;
        }

        BuildableObject pen = FindPen(found.penId);
        BuildingBeastPenAbility penAbility =
            pen?.BuildingData.GetBeastPenAbility();
        if (pen == null
            || pen.isDestroy
            || penAbility == null
            || !penAbility.IsValid)
        {
            failureReason = "reserved wildlife pen is no longer available";
            return false;
        }

        return true;
    }

    public IDisposable BeginTransportPass(
        CharacterActor carrier,
        string wildlifeId)
    {
        DoorAccessSubjectRef subject = new DoorAccessSubjectRef(
            GetCharacterId(carrier),
            carrier != null && carrier.IsOwner
                ? DoorAccessGroup.Owner
                : DoorAccessGroup.Staff,
            character: carrier);
        return doorAccessCommands.BeginTemporaryOverride(
            subject,
            DoorAccessOverrideKind.EscortPass,
            $"wildlife-transport:{wildlifeId?.Trim() ?? string.Empty}");
    }

    public bool TryBeginCarry(
        string wildlifeId,
        CharacterActor carrier,
        out string failureReason)
    {
        if (!TryGetTransportState(
                wildlifeId,
                carrier,
                out CapturedWildlifeState state,
                out WildlifeActor wildlife,
                out failureReason))
        {
            return false;
        }

        if (state.transportState != CapturedWildlifeTransportState.AwaitingTransport)
        {
            failureReason =
                $"Wildlife transport cannot begin carry from {state.transportState}.";
            return false;
        }

        if (Manhattan(carrier.GetNowXY(), wildlife.GridPosition) > 1)
        {
            failureReason = "생포할 동물과 너무 멀리 떨어져 있습니다.";
            return false;
        }

        if (!TryReserveCaptureAction(
                state,
                wildlife,
                carrier,
                "capture-carry-started",
                (int)CapturedWildlifeTransportState.AwaitingTransport,
                (int)CapturedWildlifeTransportState.Transporting,
                "capture-carrier-picked-up",
                out WildlifeGameplayOutcomeSaveData outcome,
                out DomainFactLease reserved,
                out failureReason))
        {
            failureReason = "생포 운반 시작 결과 관찰을 예약하지 못했습니다: "
                + failureReason;
            return false;
        }

        try
        {
            carriedParents[state.wildlifeId] = wildlife.transform.parent;
            wildlife.BeginManagedCarry(carrier.transform);
            state.transportState = CapturedWildlifeTransportState.Transporting;
        }
        catch
        {
            carriedParents.Remove(state.wildlifeId);
            gameplayOutcomes.Cancel(reserved);
            throw;
        }
        outcome.stateAfter = (int)state.transportState;
        CommitCaptureOutcome(outcome, reserved);
        return true;
    }

    public WildlifeDeliveryStandResolution ResolveDeliveryStand(
        string wildlifeId,
        CharacterActor carrier,
        out CapturedWildlifeState state,
        out Queue<GridMoveStep> deliveryPath,
        out string failureReason)
    {
        state = null;
        deliveryPath = null;
        if (!TryGetTransportState(
                wildlifeId,
                carrier,
                out CapturedWildlifeState found,
                out _,
                out failureReason))
        {
            return WildlifeDeliveryStandResolution.Failed;
        }
        if (found.transportState != CapturedWildlifeTransportState.Transporting)
        {
            state = found;
            failureReason =
                $"Wildlife delivery cannot be resolved from {found.transportState}.";
            return WildlifeDeliveryStandResolution.Failed;
        }
        BuildableObject pen = FindPen(found.penId);
        if (pen == null
            || !rooms.TryGetRoom(pen, out RoomInstance room)
            || !room.IsUsable
            || !gridProvider.TryGetGrid(out Grid grid))
        {
            failureReason =
                "The reserved wildlife pen has no usable delivery room.";
            return WildlifeDeliveryStandResolution.Failed;
        }

        HashSet<Vector2Int> penFootprint = new HashSet<Vector2Int>(
            pen.buildPoses ?? Array.Empty<Vector2Int>());
        GridTraversalContext traversal = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(carrier),
            DoorAccessOverrideKind.EscortPass);
        Vector2Int start = carrier.GetNowXY();
        if (!pathSearchBroker.TryGetSearch(
                grid,
                start,
                out GridPathSearchResult reachable,
                GridPathSearchPriority.Urgent,
                traversal))
        {
            state = found;
            failureReason =
                $"Wildlife delivery path search is pending from {start}.";
            return WildlifeDeliveryStandResolution.Pending;
        }

        foreach (Vector2Int candidate in room.Cells
                     .Where(cell => grid.IsWalkable(cell)
                         && !penFootprint.Contains(cell)
                         && IsUnoccupiedDeliveryStand(grid.GetGridCell(cell)))
                     .OrderBy(cell => Manhattan(cell, pen.centerPos))
                     .ThenBy(cell => cell.y)
                     .ThenBy(cell => cell.x))
        {
            if (candidate == start)
            {
                found.penPosition = candidate;
                state = found;
                deliveryPath = new Queue<GridMoveStep>();
                return WildlifeDeliveryStandResolution.Ready;
            }

            Queue<GridMoveStep> path = reachable.GetMovePathTo(candidate);
            if (path == null
                || !GridMovePathRules.TryGetPathDestination(
                    path,
                    out Vector2Int pathEnd)
                || pathEnd != candidate)
            {
                continue;
            }

            found.penPosition = candidate;
            state = found;
            deliveryPath = path;
            return WildlifeDeliveryStandResolution.Ready;
        }

        failureReason =
            $"No exact reachable wildlife delivery stand from {start}.";
        return WildlifeDeliveryStandResolution.Failed;
    }

    public bool TryCompleteCarry(
        string wildlifeId,
        CharacterActor carrier,
        out string failureReason)
    {
        if (!TryGetTransportState(
                wildlifeId,
                carrier,
                out CapturedWildlifeState state,
                out WildlifeActor wildlife,
                out failureReason))
        {
            return false;
        }

        if (state.transportState != CapturedWildlifeTransportState.Transporting)
        {
            failureReason =
                $"Wildlife carry cannot complete from {state.transportState}.";
            return false;
        }

        if (carrier.GetNowXY() != state.penPosition)
        {
            failureReason =
                "야수 우리 수용 칸에 도착하지 못했습니다. "
                + $"planned={state.penPosition};"
                + $"pathEnd={state.penPosition};"
                + $"carrier={carrier.GetNowXY()}";
            return false;
        }

        if (!carriedParents.TryGetValue(
                state.wildlifeId,
                out Transform parent))
        {
            failureReason = "Wildlife carry parent authority is unavailable.";
            return false;
        }
        if (!TryReserveCaptureAction(
                state,
                wildlife,
                carrier,
                "capture-penned",
                (int)CapturedWildlifeTransportState.Transporting,
                (int)CapturedWildlifeTransportState.Penned,
                "capture-transport-completed",
                out WildlifeGameplayOutcomeSaveData outcome,
                out DomainFactLease reserved,
                out failureReason))
        {
            failureReason = "우리 수용 결과 관찰을 예약하지 못했습니다: "
                + failureReason;
            return false;
        }
        if (!wildlife.TryEndManagedCarry(
                state.penPosition,
                carrier.transform,
                parent,
                out failureReason))
        {
            gameplayOutcomes.Cancel(reserved);
            return false;
        }

        carriedParents.Remove(state.wildlifeId);
        state.reservedCarrierId = string.Empty;
        state.transportState = CapturedWildlifeTransportState.Penned;
        state.nextCareAt = clock.Time + 1f;
        state.lastCareStatus = "우리 수용 완료";
        outcome.stateAfter = (int)state.transportState;
        CommitCaptureOutcome(outcome, reserved);
        return true;
    }

    public void FailCarry(
        string wildlifeId,
        CharacterActor carrier,
        string reason)
    {
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!stateSession.TryGet(id, out CapturedWildlifeState state))
        {
            return;
        }

        WildlifeActor wildlife = FindActor(id);
        if (wildlife != null)
        {
            if (carriedParents.TryGetValue(id, out Transform parent)
                && !TryReleaseFailedCarry(
                    state,
                    wildlife,
                    carrier,
                    parent,
                    out Vector2Int releasePosition,
                    out string releaseFailure))
            {
                throw new InvalidOperationException(
                    $"Wildlife carry rollback failed for {id}: "
                    + $"reason={reason ?? string.Empty}; "
                    + $"release={releaseFailure}; "
                    + $"capture={state.capturePosition}; "
                    + $"planned={state.penPosition}; "
                    + $"carrier={carrier?.GetNowXY()}");
            }
            carriedParents.Remove(id);
            wildlife.SetCaptured(false);
        }

        stateSession.Remove(id);
        doorSubjects.SetCapturedWildlife(id, false);
    }

    private bool TryReleaseFailedCarry(
        CapturedWildlifeState state,
        WildlifeActor wildlife,
        CharacterActor carrier,
        Transform parent,
        out Vector2Int releasePosition,
        out string failureReason)
    {
        releasePosition = state?.capturePosition ?? default;
        failureReason = string.Empty;
        if (state == null || wildlife == null)
        {
            failureReason = "Wildlife rollback state or actor is unavailable.";
            return false;
        }
        if (!gridProvider.TryGetGrid(out Grid grid) || grid == null)
        {
            failureReason = "Wildlife rollback grid authority is unavailable.";
            return false;
        }

        Vector2Int carrierPosition = carrier != null
            ? carrier.GetNowXY()
            : state.capturePosition;
        List<Vector2Int> candidates = new List<Vector2Int>
        {
            carrierPosition,
            state.capturePosition
        };
        candidates.AddRange(grid.GetCells()
            .Where(cell => cell != null
                && grid.IsWalkable(cell.Position)
                && IsWildlifeReleaseCellAvailable(cell, wildlife))
            .OrderBy(cell => Manhattan(cell.Position, carrierPosition))
            .ThenBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .Select(cell => cell.Position));

        string lastFailure = string.Empty;
        foreach (Vector2Int candidate in candidates.Distinct())
        {
            GridCell cell = grid.GetGridCell(candidate);
            if (cell == null
                || !grid.IsWalkable(candidate)
                || !IsWildlifeReleaseCellAvailable(cell, wildlife))
            {
                continue;
            }
            if (!wildlife.TryEndManagedCarry(
                    candidate,
                    carrier?.transform,
                    parent,
                    out lastFailure))
            {
                continue;
            }

            releasePosition = candidate;
            return true;
        }

        failureReason = string.IsNullOrWhiteSpace(lastFailure)
            ? $"No lawful empty Wildlife cell exists near {carrierPosition}."
            : lastFailure;
        return false;
    }

    private static bool IsWildlifeReleaseCellAvailable(
        GridCell cell,
        WildlifeActor wildlife)
    {
        IGridOccupant occupant = cell?.GetOccupant(GridLayer.Wildlife);
        return cell != null
            && (occupant == null || ReferenceEquals(occupant, wildlife));
    }

    public IReadOnlyList<CapturedWildlifeState> Capture()
    {
        feeding.ValidateBeforeCapture();
        float capturedAt = clock.Time;
        if (float.IsNaN(capturedAt) || float.IsInfinity(capturedAt)
            || capturedAt < 0f)
            throw new InvalidOperationException("Captured wildlife requires a finite non-negative save clock.");

        IReadOnlyList<CapturedWildlifeState> saved = stateSession.Capture();
        foreach (CapturedWildlifeState state in saved)
        {
            state.remainingCareSeconds = CaptureRemainingCareDelay(state, capturedAt);
        }
        return saved;
    }

    private static float CaptureRemainingCareDelay(CapturedWildlifeState state, float capturedAt)
    {
        if (float.IsNaN(state.nextCareAt) || float.IsInfinity(state.nextCareAt)
            || state.nextCareAt < 0f)
            throw new InvalidOperationException(
                $"Captured wildlife '{state.wildlifeId}' has an invalid care deadline.");
        // A scheduled deadline is stored as float32. Explicitly round the
        // same scheduling bound: Mono may otherwise compare it with a wider
        // intermediate and reject its own legitimate (now + interval) value.
        float maximumCareAt = (float)(capturedAt + CapturedWildlifeState.CareIntervalSeconds);
        if (state.nextCareAt > maximumCareAt)
            throw new InvalidOperationException(
                $"Captured wildlife '{state.wildlifeId}' care delay exceeds its normal interval; "
                + $"deadline={state.nextCareAt:R};clock={capturedAt:R};"
                + $"maximum={maximumCareAt:R};"
                + $"delay64={(double)state.nextCareAt - capturedAt:R}.");
        // Overdue care is ready for normal Tick, not offline catch-up. The
        // existing DTO bound applies only after the strict deadline check.
        return Math.Min(CapturedWildlifeState.CareIntervalSeconds,
            Math.Max(0f, state.nextCareAt - capturedAt));
    }

    public void ValidateRestore(
        CircusSaveData saveData,
        DungeonGameRestoreReport report)
    {
        WildlifeCaptureRestoreValidator.Validate(
            saveData,
            new WildlifeCaptureRestoreWorldAdapter(
                world,
                rooms,
                gridProvider,
                contentCatalog,
                speciesCatalog),
            report);
    }

    public void StageRestore(CircusRestoreCandidate candidate)
    {
        feeding.ValidateBeforeCapture();
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (!sessionAggregateRootStore.IsRestoreStaging)
        {
            throw new InvalidOperationException(
                "Captured wildlife restore requires the V18 aggregate staging boundary.");
        }

        float restoredAt = clock.Time;
        if (float.IsNaN(restoredAt) || float.IsInfinity(restoredAt)
            || restoredAt < 0f)
            throw new InvalidOperationException("Captured wildlife requires a finite non-negative restore clock.");
        foreach (CapturedWildlifeState state in candidate.CapturedWildlifeStates)
        {
            if (float.IsNaN(state.remainingCareSeconds)
                || float.IsInfinity(state.remainingCareSeconds)
                || state.remainingCareSeconds < 0f
                || state.remainingCareSeconds > CapturedWildlifeState.CareIntervalSeconds)
                throw new InvalidOperationException(
                    $"Captured wildlife '{state.wildlifeId}' has an invalid saved care delay.");
        }
        stateSession.Stage(candidate);
        foreach (CapturedWildlifeState state in stateSession.Values)
        {
            // Only the detached staged root is changed. Publication failure
            // discards this root and preserves the prior live deadline.
            state.nextCareAt = restoredAt + state.remainingCareSeconds;
        }
    }

    public void PublishRestoreProjection()
    {
        if (sessionAggregateRootStore.IsRestoreStaging)
        {
            throw new InvalidOperationException(
                "Captured wildlife projection cannot publish while restore staging is active.");
        }
        WildlifeCaptureProjectionPublication publication =
            BeginRestoreProjectionPublication();
        CompleteRestoreProjection(publication);
    }

    public WildlifeCaptureProjectionPublication
        BeginRestoreProjectionPublication()
    {
        feeding.ValidateBeforeCapture();
        CapturedWildlifeProjectionPublication projection =
            stateSession.BeginProjectionPublication();
        WildlifeGameplayOutcomeRuntime candidateOutcomes =
            CreateGameplayOutcomeRuntime(stateSession.OutcomeOwner);
        try
        {
            candidateOutcomes.AdvancePending();
            feeding.ReconcilePendingFeedsOrThrow();
        }
        catch
        {
            stateSession.RollbackProjectionPublication(projection);
            throw;
        }
        return new WildlifeCaptureProjectionPublication(
            this,
            rollback: () =>
                stateSession.RollbackProjectionPublication(projection),
            complete: () =>
            {
                gameplayOutcomeOwner = stateSession.OutcomeOwner;
                gameplayOutcomes = candidateOutcomes;
                if (projection.Changed)
                {
                    FinalizeProjectionBestEffort();
                }
                stateSession.CompleteProjectionPublication(projection);
            });
    }

    public void RollbackRestoreProjection(
        WildlifeCaptureProjectionPublication publication)
    {
        feeding.ValidateBeforeCapture();
        (publication ?? throw new ArgumentNullException(nameof(publication)))
            .Rollback(this);
    }

    public void CompleteRestoreProjection(
        WildlifeCaptureProjectionPublication publication)
    {
        feeding.ValidateBeforeCapture();
        (publication ?? throw new ArgumentNullException(nameof(publication)))
            .Complete(this);
    }

    private void EnsureProjectionCurrent()
    {
        PublishRestoreProjection();
    }

    private WildlifeGameplayOutcomeRuntime CreateGameplayOutcomeRuntime(
        IWildlifeGameplayOutcomeOwner owner) => new(
        owner,
        facts,
        factRecovery,
        calendar);

    private void RebindGameplayOutcomeRuntime()
    {
        gameplayOutcomeOwner = stateSession.OutcomeOwner;
        gameplayOutcomes = CreateGameplayOutcomeRuntime(gameplayOutcomeOwner);
    }

    private void EnsureGameplayOutcomeRuntimeCurrent()
    {
        if (!ReferenceEquals(gameplayOutcomeOwner, stateSession.OutcomeOwner))
            RebindGameplayOutcomeRuntime();
    }

    private void FinalizeProjectionBestEffort()
    {
        foreach (KeyValuePair<string, Transform> carried in
                 carriedParents.ToArray())
        {
            try
            {
                WildlifeActor carriedActor = FindActor(carried.Key);
                carriedActor?.EndManagedCarry(
                    carriedActor.GridPosition,
                    carried.Value);
            }
            catch
            {
                // Carry projection cleanup is best effort after aggregate commit.
            }
        }
        carriedParents.Clear();

        HashSet<string> activelyCapturedIds = stateSession.Values
            .Where(state => state != null
                && !state.escaped
                && state.transportState
                    != CapturedWildlifeTransportState.Released)
            .Select(state => state.wildlifeId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (WildlifeActor actor in world.Wildlife.ToArray())
        {
            try
            {
                if (actor != null
                    && actor.State == WildlifeState.Captured
                    && !activelyCapturedIds.Contains(actor.WildlifeId))
                {
                    actor.SetCaptured(false);
                }
            }
            catch
            {
                // Actor presentation cannot invalidate a committed restore.
            }
        }

        foreach (CapturedWildlifeState restored in stateSession.Values.ToArray())
        {
            WildlifeActor actor = FindActor(restored.wildlifeId);
            if (actor == null)
            {
                continue;
            }

            try
            {
                bool shouldBeCaptured = !restored.escaped
                    && restored.transportState
                        != CapturedWildlifeTransportState.Released;
                if ((actor.State == WildlifeState.Captured) != shouldBeCaptured)
                {
                    actor.SetCaptured(shouldBeCaptured);
                }

                Vector2Int target = restored.transportState
                    == CapturedWildlifeTransportState.Escaped
                        ? restored.escapeDestination
                        : TryReadRole(restored, out CapturedWildlifeRoleId roleId)
                            && (roleId.Equals(CapturedWildlifeRoleIds.Companion)
                                || roleId.Equals(CapturedWildlifeRoleIds.Haul))
                                ? actor.GridPosition
                                : restored.penPosition;
                if (actor.GridPosition != target)
                {
                    actor.WarpTo(target);
                }
            }
            catch
            {
                // Actor presentation cannot invalidate a committed restore.
            }
        }

        try
        {
            doorSubjects.ReplaceCapturedWildlifeSubjects(
                stateSession.Values
                    .Where(state => state != null
                        && !state.escaped
                        && state.transportState
                            != CapturedWildlifeTransportState.Released)
                    .Select(state => state.wildlifeId));
        }
        catch
        {
            // Door-access projection is derived from the committed aggregate.
        }
    }

    private void TickAnimalCare(
        CapturedWildlifeState state,
        WildlifeActor actor)
    {
        BuildableObject pen = FindPen(state.penId);
        BuildingBeastPenAbility ability =
            pen?.BuildingData.GetBeastPenAbility();
        if (pen == null || ability == null)
        {
            state.lastCareStatus = "우리 소실";
            state.escapeRisk = 100f;
            TryBeginEscape(state, actor);
            return;
        }

        string careDestinationId = WildlifeCareInputOwnerAuthority
            .FormatDestinationId(state.penId);
        Vector2Int carePosition = state.penPosition;
        bool companionReturningForCare =
            TryReadRole(state, out CapturedWildlifeRoleId roleId)
            && roleId.Equals(CapturedWildlifeRoleIds.Companion)
            && (actor.Hunger >= 0.45f || actor.Thirst >= 0.45f)
            && actor.GridPosition != carePosition;
        bool awayFromCarePosition = actor.GridPosition != carePosition;
        feeding.SatisfyNeeds(state, actor, ability.dailyFood, ability.dailyWater,
            carePosition, careDestinationId, !awayFromCarePosition,
            out bool fed, out bool watered);
        bool insecure = !rooms.TryGetRoom(pen, out RoomInstance room)
            || !room.IsUsable
            || room.Doors.OfType<Door>().Any(CaptiveWildlifeCanUse);
        float filthPenalty = filth.GetCleanlinessPenalty(state.penPosition, 1);
        float baseRisk = state.isTamed ? 1f : 10f;
        float securityMultiplier = state.isTamed ? 0.08f : 0.35f;
        float deprivationMultiplier = state.isTamed ? 12f : 28f;
        state.escapeRisk = Mathf.Clamp(
            baseRisk
            + (100f - ability.baseSecurity) * securityMultiplier
            + actor.Hunger * deprivationMultiplier
            + actor.Thirst * deprivationMultiplier
            + filthPenalty * 0.3f
            + (insecure ? 45f : 0f),
            0f,
            100f);
        string returnStatus = companionReturningForCare
            ? "동행 중단 · 우리로 복귀 중 · "
            : awayFromCarePosition
                ? "우리로 복귀 중 · "
                : string.Empty;
        state.lastCareStatus = returnStatus
            + $"{(fed ? "먹이 섭취" : "먹이 대기")} · "
            + $"{(watered ? "급수" : "물 대기")} · "
            + $"탈출 위험 {state.escapeRisk:0}";

        if (actor.Hunger >= 0.98f || actor.Thirst >= 0.98f)
        {
            actor.ApplyDamage(1, null);
        }

        float escapeThreshold = state.isTamed ? 92f : 70f;
        if (state.escapeRisk >= escapeThreshold)
        {
            float chance = Mathf.Lerp(
                0.02f,
                0.16f,
                Mathf.InverseLerp(escapeThreshold, 100f, state.escapeRisk));
            if (random.Chance(chance))
            {
                TryBeginEscape(state, actor);
            }
        }
    }

    private void TryBeginEscape(
        CapturedWildlifeState state,
        WildlifeActor actor)
    {
        if (!gridProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        List<Vector2Int> exits = new List<Vector2Int>();
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int position = new Vector2Int(x, y);
                GridCell cell = grid.GetGridCell(position);
                if (cell?.AreaType == GridCellAreaType.ExteriorPath
                    && grid.IsWalkable(position))
                {
                    exits.Add(position);
                }
            }
        }

        foreach (Vector2Int destination in exits
                     .OrderBy(position => Manhattan(
                         actor.GridPosition,
                         position)))
        {
            if (!actor.TrySetManagedCaptivePath(destination, clock.Time))
            {
                continue;
            }

            state.escapeDestination = destination;
            if (TryReadRole(state, out CapturedWildlifeRoleId roleId)
                && roleId.Equals(CapturedWildlifeRoleIds.Companion))
            {
                ClearCompanionRole(state, "도주로 동행 역할 취소");
            }
            else if (roleId.Equals(CapturedWildlifeRoleIds.Haul)
                && TryGetHaul(
                    state.wildlifeId,
                    out WildlifeHaulAssignmentSnapshot haul))
            {
                if (haul.Phase == CapturedWildlifeHaulPhase.Reserved)
                {
                    wildlifeHaulItems.ReleaseUnpicked(haul);
                }
                else if (haul.Phase is CapturedWildlifeHaulPhase.CargoOwned
                        or CapturedWildlifeHaulPhase.ReleasePending)
                {
                    if (!wildlifeHaulItems.TryCommitRecoveryPending(
                            haul,
                            actor.GridPosition,
                            WorldItemCarryInterruptionKind.Disabled,
                            out string recoveryFailure))
                    {
                        state.lastCareStatus =
                            "탈출 화물 회수 준비 실패 · " + recoveryFailure;
                        return;
                    }
                }
                state.capabilityState =
                    CapturedWildlifeCapabilityStateCodec.CreateNone();
            }
            state.transportState = CapturedWildlifeTransportState.Escaped;
            state.escaped = true;
            state.lastCareStatus = "우리 문을 빠져나가 도주 중";
            doorSubjects.SetCapturedWildlife(state.wildlifeId, false);
            return;
        }

        state.lastCareStatus = "탈출을 시도했지만 잠긴 문에 막힘";
    }

    private void TickEscapingAnimal(
        CapturedWildlifeState state,
        WildlifeActor actor)
    {
        if (actor.GridPosition == state.escapeDestination)
        {
            actor.SetCaptured(false);
            state.lastCareStatus = "외부로 탈출";
            return;
        }

        if (!actor.IsMoving)
        {
            actor.TrySetManagedCaptivePath(
                state.escapeDestination,
                clock.Time);
        }
    }

    private BuildableObject FindPen(string penId)
    {
        return world.Buildings.FirstOrDefault(building =>
            building != null
            && string.Equals(
                GetPenId(building),
                penId,
                StringComparison.Ordinal));
    }

    private WildlifeActor FindActor(string wildlifeId)
    {
        return world.Wildlife.FirstOrDefault(actor =>
            actor != null
            && string.Equals(actor.WildlifeId, wildlifeId, StringComparison.Ordinal));
    }

    private static bool CaptiveWildlifeCanUse(Door door)
    {
        DoorAccessPolicyState policy = door?.AccessPolicy;
        if (policy == null)
        {
            return true;
        }

        return policy.IsGroupAllowed(DoorAccessGroup.CaptiveWildlife);
    }

    private static string GetPenId(BuildableObject pen)
    {
        return pen != null
            ? pen.RequirePersistentInstanceId().Value
            : string.Empty;
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor).Value
            : string.Empty;
    }

    private static bool IsUnoccupiedDeliveryStand(GridCell cell)
    {
        return cell != null
            && cell.GetOccupant(GridLayer.Building) == null
            && cell.GetOccupant(GridLayer.Construction) == null
            && cell.GetOccupant(GridLayer.Conveyor) == null
            && cell.GetOccupant(GridLayer.Character) == null
            && cell.GetOccupant(GridLayer.DownedCharacter) == null
            && cell.GetOccupant(GridLayer.Wildlife) == null;
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

}
