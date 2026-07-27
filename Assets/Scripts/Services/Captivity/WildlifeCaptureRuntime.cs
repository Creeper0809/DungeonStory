using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class WildlifeCaptureRuntime :
    IWildlifeCaptureRuntime,
    IWildlifeCaptureTransportRuntime,
    ITickable
{
    private static readonly IReadOnlyDictionary<StockCategory, int> FoodCost =
        new Dictionary<StockCategory, int> { [StockCategory.Food] = 1 };
    private static readonly IReadOnlyDictionary<StockCategory, int> WaterCost =
        new Dictionary<StockCategory, int> { [StockCategory.Water] = 1 };

    private readonly ICharacterAiWorldRegistry world;
    private readonly IRoomLayoutCache rooms;
    private readonly IGridSystemProvider gridProvider;
    private readonly IDoorAccessCommandService doorAccessCommands;
    private readonly IDoorAccessSubjectRegistry doorSubjects;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWorldFilthQuery filth;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly Dictionary<string, CapturedWildlifeState> captured =
        new Dictionary<string, CapturedWildlifeState>(StringComparer.Ordinal);
    private readonly Dictionary<string, Transform> carriedParents =
        new Dictionary<string, Transform>(StringComparer.Ordinal);
    private readonly List<CapturedWildlifeState> tickBuffer =
        new List<CapturedWildlifeState>();

    public WildlifeCaptureRuntime(
        ICharacterAiWorldRegistry world,
        IRoomLayoutCache rooms,
        IGridSystemProvider gridProvider,
        IDoorAccessCommandService doorAccessCommands,
        IDoorAccessSubjectRegistry doorSubjects,
        IWorldItemStackRuntime itemRuntime,
        IWorldFilthQuery filth,
        IGameClock clock,
        IRandomStreamProvider randomStreamProvider)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.doorAccessCommands = doorAccessCommands
            ?? throw new ArgumentNullException(nameof(doorAccessCommands));
        this.doorSubjects = doorSubjects
            ?? throw new ArgumentNullException(nameof(doorSubjects));
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        random = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("captivity.wildlife-care");
    }

    public IReadOnlyList<CapturedWildlifeState> CapturedAnimals =>
        captured.Values.Select(item => item.Clone()).ToArray();

    public bool IsCaptured(string wildlifeId)
    {
        return !string.IsNullOrWhiteSpace(wildlifeId)
            && captured.TryGetValue(
                wildlifeId.Trim(),
                out CapturedWildlifeState state)
            && !state.escaped;
    }

    public void Tick()
    {
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        if (captured.Count == 0)
        {
            return;
        }

        tickBuffer.Clear();
        foreach (CapturedWildlifeState state in captured.Values)
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
        int occupied = captured.Values.Count(item =>
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
        captured[state.wildlifeId] = state;
        doorSubjects.SetCapturedWildlife(state.wildlifeId, true);
        AbilityWildlifeCaptureTransport transport =
            AbilityWildlifeCaptureTransport.Ensure(carrier);
        if (transport == null)
        {
            captured.Remove(state.wildlifeId);
            doorSubjects.SetCapturedWildlife(state.wildlifeId, false);
            failureReason = "동물 운반 행동을 시작할 수 없습니다.";
            return false;
        }

        transport.Configure(this);
        transport.StartTransport(state.wildlifeId);
        return true;
    }

    public bool TryGetCaptured(
        string wildlifeId,
        out CapturedWildlifeState state)
    {
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (captured.TryGetValue(id, out CapturedWildlifeState found))
        {
            state = found.Clone();
            return true;
        }

        state = null;
        return false;
    }

    public bool TryRelease(string wildlifeId, out string failureReason)
    {
        failureReason = string.Empty;
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!captured.Remove(id, out CapturedWildlifeState state))
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
        if (!captured.TryGetValue(id, out CapturedWildlifeState state))
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
        if (!captured.TryGetValue(id, out CapturedWildlifeState state)
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
        captured.TryGetValue(id, out CapturedWildlifeState found);
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
        if (!captured.Remove(id, out CapturedWildlifeState state))
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

    public void Restore(
        IEnumerable<CapturedWildlifeState> states,
        IList<string> warnings)
    {
        foreach (string id in captured.Keys)
        {
            doorSubjects.SetCapturedWildlife(id, false);
        }
        captured.Clear();
        carriedParents.Clear();
        foreach (CapturedWildlifeState source in states
                     ?? Array.Empty<CapturedWildlifeState>())
        {
            string id = source?.wildlifeId?.Trim() ?? string.Empty;
            if (id.Length == 0 || captured.ContainsKey(id))
            {
                warnings?.Add("유효하지 않거나 중복된 포획 동물 상태를 건너뛰었습니다.");
                continue;
            }

            CapturedWildlifeState restored = source.Clone();
            if (restored.transportState is CapturedWildlifeTransportState.AwaitingTransport
                or CapturedWildlifeTransportState.Transporting
                or CapturedWildlifeTransportState.MovingToShow
                or CapturedWildlifeTransportState.Performing
                or CapturedWildlifeTransportState.ReturningToPen)
            {
                restored.reservedCarrierId = string.Empty;
                restored.assignedShowOrderId = string.Empty;
                restored.transportState = CapturedWildlifeTransportState.Penned;
                warnings?.Add(
                    $"{id}: 진행 중이던 동물 운반을 우리 수용 상태로 복원했습니다.");
            }
            captured.Add(id, restored);
            doorSubjects.SetCapturedWildlife(id, !restored.escaped);
            WildlifeActor actor = FindActor(id);
            if (actor == null)
            {
                warnings?.Add($"{id}: 포획 동물 개체를 찾을 수 없습니다.");
                continue;
            }

            actor.SetCaptured(!restored.escaped);
            actor.WarpTo(
                restored.transportState == CapturedWildlifeTransportState.Escaped
                    ? restored.escapeDestination
                    : restored.penPosition);
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
        bool fed = TrySatisfyNeed(
            state,
            actor,
            StockCategory.Food,
            ability.dailyFood,
            actor.Hunger,
            FoodCost,
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
        state.escapeRisk = Mathf.Clamp(
            10f
            + (100f - ability.baseSecurity) * 0.35f
            + actor.Hunger * 28f
            + actor.Thirst * 34f
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

        if (state.escapeRisk >= 70f)
        {
            float chance = Mathf.Lerp(
                0.02f,
                0.16f,
                Mathf.InverseLerp(70f, 100f, state.escapeRisk));
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
            hasFood |= definition?.StockCategory == StockCategory.Food;
            hasWater |= definition?.StockCategory == StockCategory.Water;
        }

        state.foodDeliveryPending &= hasFood;
        state.waterDeliveryPending &= hasWater;
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
        return $"pen:{pen.id}:{pen.centerPos.x}:{pen.centerPos.y}";
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        return id.Length > 0
            ? id
            : actor != null
                ? $"character:{actor.GetInstanceID()}"
                : string.Empty;
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }
}
