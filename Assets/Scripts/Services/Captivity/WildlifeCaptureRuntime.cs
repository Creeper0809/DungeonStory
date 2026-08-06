using System;
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
        IDoorAccessCommandService doorAccessCommands,
        IDoorAccessSubjectRegistry doorSubjects)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        GridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        DoorAccessCommands = doorAccessCommands
            ?? throw new ArgumentNullException(nameof(doorAccessCommands));
        DoorSubjects = doorSubjects
            ?? throw new ArgumentNullException(nameof(doorSubjects));
    }

    public ICharacterAiWorldRegistry World { get; }
    public IRoomLayoutCache Rooms { get; }
    public IGridSystemProvider GridProvider { get; }
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
        IWasteFeedCommand wasteProcessing)
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
    }

    public IWorldItemStackRuntime ItemRuntime { get; }
    public IWorldFilthQuery Filth { get; }
    public IResourceEconomyContentCatalog ContentCatalog { get; }
    public IWildlifeSpeciesCatalogProvider SpeciesCatalog { get; }
    public IWasteFeedCommand WasteProcessing { get; }
}

public sealed class WildlifeCaptureSessionContext
{
    public WildlifeCaptureSessionContext(
        IGameClock clock,
        IRandomStreamProvider randomStreamProvider,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        RandomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        AggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IGameClock Clock { get; }
    public IRandomStreamProvider RandomStreamProvider { get; }
    public DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
}

public sealed partial class WildlifeCaptureRuntime :
    IWildlifeCaptureRuntime,
    IWildlifeCaptureTransportRuntime,
    ITickable
{
    private static readonly IReadOnlyDictionary<StockCategory, int> WaterCost =
        new Dictionary<StockCategory, int> { [StockCategory.Water] = 1 };

    private readonly ICharacterAiWorldRegistry world;
    private readonly IRoomLayoutCache rooms;
    private readonly IGridSystemProvider gridProvider;
    private readonly IDoorAccessCommandService doorAccessCommands;
    private readonly IDoorAccessSubjectRegistry doorSubjects;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWorldFilthQuery filth;
    private readonly IResourceEconomyContentCatalog contentCatalog;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IWasteFeedCommand wasteProcessing;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly DungeonRuntimeAggregateRootStore sessionAggregateRootStore;
    private readonly CapturedWildlifeStateSession stateSession;
    private readonly Dictionary<string, Transform> carriedParents =
        new Dictionary<string, Transform>(StringComparer.Ordinal);
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
        doorAccessCommands = worldContext.DoorAccessCommands;
        doorSubjects = worldContext.DoorSubjects;
        itemRuntime = care.ItemRuntime;
        filth = care.Filth;
        contentCatalog = care.ContentCatalog;
        speciesCatalog = care.SpeciesCatalog;
        wasteProcessing = care.WasteProcessing;
        clock = session.Clock;
        sessionAggregateRootStore = session.AggregateRootStore;
        stateSession = new CapturedWildlifeStateSession(sessionAggregateRootStore);
        random = session.RandomStreamProvider
            .Get("captivity.wildlife-care");
    }

    public IReadOnlyList<CapturedWildlifeState> CapturedAnimals =>
        stateSession.Capture();

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
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        if (stateSession.Count == 0)
        {
            return;
        }

        tickBuffer.Clear();
        foreach (CapturedWildlifeState state in stateSession.Values)
        {
            tickBuffer.Add(state);
        }

        for (int index = 0; index < tickBuffer.Count; index++)
        {
            CapturedWildlifeState state = tickBuffer[index];
            WildlifeActor actor = FindActor(state.wildlifeId);
            if (actor == null || !actor.IsAlive)
            {
                continue;
            }

            if (state.transportState == CapturedWildlifeTransportState.Escaped)
            {
                TickEscapingAnimal(state, actor);
                continue;
            }

            if (state.transportState != CapturedWildlifeTransportState.Penned)
            {
                continue;
            }

            actor.AdvanceCaptiveNeeds(
                clock.DeltaTime,
                hungerPerSecond: 1f / 300f,
                thirstPerSecond: 1f / 220f);
            if (clock.Time + 0.001f < state.nextCareAt)
            {
                continue;
            }

            state.nextCareAt = clock.Time + 5f;
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

        List<Vector2Int> destinations = room.Cells
            .Where(grid.IsWalkable)
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
            nextCareAt = clock.Time + 5f,
            lastCareStatus = "우리 수용 준비"
        };
        stateSession.Set(state);
        doorSubjects.SetCapturedWildlife(state.wildlifeId, true);
        AbilityWildlifeCaptureTransport transport =
            CaptivityAbilityAdapterFactory.EnsureWildlifeTransport(
                carrier,
                this);
        if (transport == null)
        {
            stateSession.Remove(state.wildlifeId);
            doorSubjects.SetCapturedWildlife(state.wildlifeId, false);
            failureReason = "동물 운반 행동을 시작할 수 없습니다.";
            return false;
        }

        transport.StartTransport(state.wildlifeId);
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
            nextCareAt = clock.Time + 5f,
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
        if (!stateSession.Remove(id, out CapturedWildlifeState state))
        {
            failureReason = "포획 동물을 찾을 수 없습니다.";
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
        if (found == null
            || wildlife == null
            || carrier == null
            || !string.Equals(
                found.reservedCarrierId,
                GetCharacterId(carrier),
                StringComparison.Ordinal))
        {
            failureReason = "동물 운반 예약이 유효하지 않습니다.";
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

        if (Manhattan(carrier.GetNowXY(), wildlife.GridPosition) > 1)
        {
            failureReason = "생포할 동물과 너무 멀리 떨어져 있습니다.";
            return false;
        }

        carriedParents[state.wildlifeId] = wildlife.transform.parent;
        wildlife.BeginManagedCarry(carrier.transform);
        state.transportState = CapturedWildlifeTransportState.Transporting;
        return true;
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

        if (carrier.GetNowXY() != state.penPosition)
        {
            failureReason = "야수 우리 수용 칸에 도착하지 못했습니다.";
            return false;
        }

        carriedParents.Remove(state.wildlifeId, out Transform parent);
        wildlife.EndManagedCarry(state.penPosition, parent);
        state.reservedCarrierId = string.Empty;
        state.transportState = CapturedWildlifeTransportState.Penned;
        state.nextCareAt = clock.Time + 1f;
        state.lastCareStatus = "우리 수용 완료";
        return true;
    }

    public void FailCarry(
        string wildlifeId,
        CharacterActor carrier,
        string reason)
    {
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!stateSession.Remove(id, out CapturedWildlifeState state))
        {
            return;
        }

        doorSubjects.SetCapturedWildlife(id, false);
        WildlifeActor wildlife = FindActor(id);
        if (wildlife != null)
        {
            carriedParents.Remove(id, out Transform parent);
            Vector2Int releasePosition = carrier != null
                ? carrier.GetNowXY()
                : state.capturePosition;
            wildlife.EndManagedCarry(releasePosition, parent);
            wildlife.SetCaptured(false);
        }
    }

    public IReadOnlyList<CapturedWildlifeState> Capture()
    {
        return CapturedAnimals;
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
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (!sessionAggregateRootStore.IsRestoreStaging)
        {
            throw new InvalidOperationException(
                "Captured wildlife restore requires the V18 aggregate staging boundary.");
        }

        stateSession.Stage(candidate);
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
        CapturedWildlifeProjectionPublication projection =
            stateSession.BeginProjectionPublication();
        return new WildlifeCaptureProjectionPublication(
            this,
            rollback: () =>
                stateSession.RollbackProjectionPublication(projection),
            complete: () =>
            {
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
        (publication ?? throw new ArgumentNullException(nameof(publication)))
            .Rollback(this);
    }

    public void CompleteRestoreProjection(
        WildlifeCaptureProjectionPublication publication)
    {
        (publication ?? throw new ArgumentNullException(nameof(publication)))
            .Complete(this);
    }

    private void EnsureProjectionCurrent()
    {
        PublishRestoreProjection();
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

        RefreshDeliveryPending(state);
        state.feedSicknessSeverity = Mathf.Max(
            0f,
            state.feedSicknessSeverity - 1.5f);
        bool fed = TrySatisfyFoodNeed(
            state,
            actor,
            ability.dailyFood,
            actor.Hunger,
            ref state.foodDeliveryPending);
        bool watered = TrySatisfyNeed(
            state,
            actor,
            StockCategory.Water,
            ability.dailyWater,
            actor.Thirst,
            WaterCost,
            ref state.waterDeliveryPending);
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
        state.lastCareStatus =
            $"{(fed ? "먹이 섭취" : "먹이 대기")} · "
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

    private bool TrySatisfyNeed(
        CapturedWildlifeState state,
        WildlifeActor actor,
        StockCategory category,
        float dailyNeed,
        float currentNeed,
        IReadOnlyDictionary<StockCategory, int> cost,
        ref bool deliveryPending)
    {
        if (dailyNeed <= 0f || currentNeed < 0.45f)
        {
            return currentNeed < 0.45f;
        }

        if (itemRuntime.TryConsumeFacilityBuffer(
                state.penId,
                cost,
                out _))
        {
            if (category == StockCategory.Food)
            {
                actor.SatisfyCaptiveNeeds(0.72f, 0f);
            }
            else
            {
                actor.SatisfyCaptiveNeeds(0f, 0.8f);
            }

            deliveryPending = false;
            return true;
        }

        if (!deliveryPending)
        {
            int amount = Mathf.Max(1, Mathf.CeilToInt(dailyNeed));
            deliveryPending = itemRuntime.TryRequestFacilityDelivery(
                category,
                amount,
                state.penPosition,
                state.penId,
                out int requested,
                out _)
                && requested > 0;
        }

        return false;
    }

    private bool TrySatisfyFoodNeed(
        CapturedWildlifeState state,
        WildlifeActor actor,
        float dailyNeed,
        float currentNeed,
        ref bool deliveryPending)
    {
        if (dailyNeed <= 0f || currentNeed < 0.45f)
        {
            return currentNeed < 0.45f;
        }

        WildlifeDietType diet = speciesCatalog.TryGetSpecies(
                state.speciesId,
                out WildlifeSpeciesDefinition species)
            ? species.Diet
            : WildlifeDietType.Omnivore;
        ResourceItemDefinitionSO[] candidates = contentCatalog.Items
            .Where(item => item != null
                && item.StockCategory == StockCategory.Food
                && IsFoodAllowed(diet, item.IngredientTags))
            .OrderBy(item => GetFeedPreference(diet, item))
            .ThenBy(item => item.UnitPrice)
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();

        foreach (ResourceItemDefinitionSO candidate in candidates)
        {
            IReadOnlyDictionary<string, int> cost =
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [candidate.ItemId] = 1
                };
            if (!itemRuntime.TryConsumeFacilityItemBuffer(
                    state.penId,
                    cost,
                    out _))
            {
                continue;
            }

            actor.SatisfyCaptiveNeeds(0.72f, 0f);
            state.lastFeedItemId = candidate.ItemId;
            deliveryPending = false;
            return true;
        }

        WasteFeedResult wasteFeed = wasteProcessing.ConsumeDirectFeed(
            diet,
            state.penId);
        if (wasteFeed.Succeeded)
        {
            actor.SatisfyCaptiveNeeds(
                Mathf.Clamp(wasteFeed.Nutrition * 0.9f, 0.35f, 0.8f),
                0f);
            state.lastFeedItemId = wasteFeed.ItemId;
            state.lastFeedDiseaseChance = wasteFeed.DiseaseChance;
            if (random.Chance(wasteFeed.DiseaseChance))
            {
                state.feedSicknessSeverity = Mathf.Clamp(
                    state.feedSicknessSeverity + 25f,
                    0f,
                    100f);
                actor.ApplyDamage(1, null);
            }

            state.lastCareStatus = wasteFeed.Outcome.ToString();
            deliveryPending = false;
            return true;
        }

        if (!deliveryPending)
        {
            int amount = Mathf.Max(1, Mathf.CeilToInt(dailyNeed));
            foreach (ResourceItemDefinitionSO candidate in candidates
                         .Where(candidate => GetFeedPreference(diet, candidate) <= 1))
            {
                if (!itemRuntime.TryRequestItemDelivery(
                        candidate.ItemId,
                        amount,
                        state.penPosition,
                        state.penId,
                        out int requested,
                        out _)
                    || requested <= 0)
                {
                    continue;
                }

                state.lastFeedItemId = candidate.ItemId;
                deliveryPending = true;
                break;
            }

            if (!deliveryPending)
            {
                WasteFeedRequestResult wasteRequest =
                    wasteProcessing.RequestDirectFeed(
                        diet,
                        state.penPosition,
                        state.penId);
                if (wasteRequest.Succeeded)
                {
                    state.lastFeedItemId = wasteRequest.ItemId;
                    deliveryPending = true;
                }
            }

            if (!deliveryPending)
            {
                foreach (ResourceItemDefinitionSO candidate in candidates
                             .Where(candidate => GetFeedPreference(diet, candidate) > 1))
                {
                    if (!itemRuntime.TryRequestItemDelivery(
                            candidate.ItemId,
                            amount,
                            state.penPosition,
                            state.penId,
                            out int requested,
                            out _)
                        || requested <= 0)
                    {
                        continue;
                    }

                    state.lastFeedItemId = candidate.ItemId;
                    deliveryPending = true;
                    break;
                }
            }
        }

        return false;
    }

    private void RefreshDeliveryPending(CapturedWildlifeState state)
    {
        bool hasFood = false;
        bool hasWater = false;
        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks())
        {
            if (!string.Equals(
                    stack.DestinationId,
                    state.penId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            DungeonItemDefinition definition =
                itemRuntime.CatalogProvider.GetDefinition(stack.ItemId);
            hasFood |= string.IsNullOrWhiteSpace(state.lastFeedItemId)
                ? definition?.StockCategory == StockCategory.Food
                : string.Equals(
                    stack.ItemId,
                    state.lastFeedItemId,
                    StringComparison.Ordinal);
            hasWater |= definition?.StockCategory == StockCategory.Water;
        }

        state.foodDeliveryPending &= hasFood;
        state.waterDeliveryPending &= hasWater;
    }

    private static bool IsFoodAllowed(
        WildlifeDietType diet,
        ResourceIngredientTag tags)
    {
        bool plant = (tags & (ResourceIngredientTag.Plant
            | ResourceIngredientTag.Fungus)) != 0;
        bool animal = (tags & (ResourceIngredientTag.Meat
            | ResourceIngredientTag.Blood
            | ResourceIngredientTag.Fat
            | ResourceIngredientTag.Egg
            | ResourceIngredientTag.Milk)) != 0;
        bool spoiled = (tags & ResourceIngredientTag.Spoiled) != 0;
        return diet switch
        {
            WildlifeDietType.Herbivore => plant && !animal,
            WildlifeDietType.Carnivore => animal,
            WildlifeDietType.Scavenger => animal || spoiled,
            _ => plant || animal
        };
    }

    private static int GetFeedPreference(
        WildlifeDietType diet,
        ResourceItemDefinitionSO item)
    {
        if (diet == WildlifeDietType.Herbivore
            && string.Equals(item.ItemId, "feed:hay", StringComparison.Ordinal))
        {
            return 0;
        }

        if (diet is WildlifeDietType.Carnivore
                or WildlifeDietType.Omnivore
                or WildlifeDietType.Scavenger
            && string.Equals(
                item.ItemId,
                "feed:dog-food",
                StringComparison.Ordinal))
        {
            return 0;
        }

        if (item.Kind == ResourceItemKind.FinishedGood)
        {
            return 1;
        }

        return item.Kind == ResourceItemKind.Food ? 3 : 2;
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

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

}
