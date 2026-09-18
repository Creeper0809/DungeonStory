using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;
public sealed class SeasonalWildlifeVisitorApplicationAdapter :
    IStartable,
    IDisposable
{
    private const string AlertCategory = "V20 계절 야생동물";
    private const string CargoAlertCategory = "V20 계절 표류 화물";

    private readonly ISeasonalEventQuery seasonal;
    private readonly ISeasonalWildlifeArrivalCommand commands;
    private readonly IWildlifeExternalArrivalRuntime arrivals;
    private readonly IWildlifeSpeciesCatalogProvider species;
    private readonly IWildlifeEcosystemRuntime ecosystem;
    private readonly IGridSystemProvider grids;
    private readonly IGridPathSearchBroker pathSearch;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameCalendar calendar;
    private readonly IWorldEventCatalog catalog;
    private readonly V20CampaignRuntime campaign;
    private readonly IWorldItemStackRuntime items;
    private readonly IGameEventBus events;
    private IDisposable dayStartedSubscription;
    private IDisposable contentResolvedSubscription;

    public SeasonalWildlifeVisitorApplicationAdapter(
        ISeasonalEventQuery seasonal,
        ISeasonalWildlifeArrivalCommand commands,
        IWildlifeExternalArrivalRuntime arrivals,
        IWorldEventCatalog catalog,
        V20CampaignRuntime campaign,
        WildlifeWorldServices world)
    {
        WildlifeWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        this.seasonal = seasonal ?? throw new ArgumentNullException(nameof(seasonal));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.arrivals = arrivals ?? throw new ArgumentNullException(nameof(arrivals));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        species = requiredWorld.Species;
        ecosystem = requiredWorld.Ecosystem;
        grids = requiredWorld.Grid;
        pathSearch = requiredWorld.PathSearch;
        worldRegistry = requiredWorld.WorldRegistry;
        calendar = requiredWorld.Calendar;
        events = requiredWorld.Events;
        items = requiredWorld.Items;
    }

    public void Start()
    {
        dayStartedSubscription ??= events.Subscribe<OperatingDayStartedEvent>(
            OnDayStarted);
        contentResolvedSubscription ??=
            events.Subscribe<V20ContentEffectsResolvedEvent>(
                OnContentResolved);
        ProcessActiveSeasonalContent(Math.Max(1, calendar.Day));
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        contentResolvedSubscription?.Dispose();
        dayStartedSubscription = null;
        contentResolvedSubscription = null;
    }

    private void OnDayStarted(OperatingDayStartedEvent started) =>
        ProcessActiveSeasonalContent(Math.Max(1, started.day));

    private void OnContentResolved(V20ContentEffectsResolvedEvent resolved)
    {
        int absoluteDay = Math.Max(1, calendar.Day);
        if (string.Equals(
                resolved.ResolutionId,
                "completed",
                StringComparison.Ordinal)
            && !string.IsNullOrEmpty(resolved.OccurrenceInstanceId))
        {
            PublishResolvedAlert(resolved, absoluteDay);
            return;
        }
        ProcessActiveSeasonalContent(absoluteDay);
    }

    private void ProcessActiveSeasonalContent(int absoluteDay)
    {
        ProcessActiveArrivals(absoluteDay);
        ProcessActiveDriftCargo(absoluteDay);
    }

    private void ProcessActiveArrivals(int absoluteDay)
    {
        string[] occurrenceIds = seasonal.ActiveSeasonalEvents
            .Where(value =>
                value?.seasonalWildlifeArrival?.configured == true
                && value.deadlineAbsoluteDay >= absoluteDay)
            .Select(value => value.instanceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string occurrenceId in occurrenceIds)
        {
            ProcessArrival(occurrenceId, absoluteDay);
        }
    }

    private void ProcessArrival(string occurrenceId, int absoluteDay)
    {
        V20ActiveEventSaveData occurrence = FindActive(occurrenceId);
        if (occurrence == null
            || occurrence.deadlineAbsoluteDay < absoluteDay)
        {
            return;
        }
        SeasonalWildlifeArrivalSaveData arrival =
            occurrence.seasonalWildlifeArrival;
        if (!species.TryGetSpecies(
                arrival.speciesId,
                out WildlifeSpeciesDefinition definition))
        {
            commands.TryRecordSeasonalWildlifeArrivalFailure(
                occurrenceId,
                "방문 야생동물 종을 찾지 못했습니다.");
            PublishActiveAlert(FindActive(occurrenceId), absoluteDay, null);
            return;
        }

        if (arrival.members.Count == 0)
        {
            TryCommitPlan(occurrence, definition);
            occurrence = FindActive(occurrenceId);
            if (occurrence == null)
            {
                return;
            }
            arrival = occurrence.seasonalWildlifeArrival;
        }

        SeasonalWildlifeArrivalMemberSaveData[] pending = arrival.members
            .Where(value => value != null && !value.spawned)
            .Select(SeasonalWildlifeArrivalRules.CloneMember)
            .ToArray();
        foreach (SeasonalWildlifeArrivalMemberSaveData member in pending)
        {
            ExternalWildlifeArrivalDisposition disposition =
                arrivals.TrySpawnExternalArrival(
                    arrival.speciesId,
                    member.wildlifeId,
                    new Vector2Int(member.positionX, member.positionY),
                    out _,
                    out string spawnMessage);
            if (disposition is ExternalWildlifeArrivalDisposition.Created
                or ExternalWildlifeArrivalDisposition.ExactReplay)
            {
                if (!commands.TryMarkSeasonalWildlifeArrivalSpawned(
                        occurrenceId,
                        member.wildlifeId,
                        out string markFailure))
                {
                    commands.TryRecordSeasonalWildlifeArrivalFailure(
                        occurrenceId,
                        markFailure);
                }
            }
            else
            {
                commands.TryRecordSeasonalWildlifeArrivalFailure(
                    occurrenceId,
                    spawnMessage);
            }
        }

        PublishActiveAlert(FindActive(occurrenceId), absoluteDay, definition);
    }

    private void TryCommitPlan(
        V20ActiveEventSaveData occurrence,
        WildlifeSpeciesDefinition definition)
    {
        SeasonalWildlifeArrivalSaveData arrival =
            occurrence.seasonalWildlifeArrival;
        if (!grids.TryGetGrid(out Grid grid))
        {
            commands.TryRecordSeasonalWildlifeArrivalFailure(
                occurrence.instanceId,
                "그리드가 준비되지 않아 방문 위치를 확정하지 못했습니다.");
            return;
        }
        if (!SeasonalWildlifeArrivalRules.TryParseHabitat(
                arrival.requiredHabitatId,
                out WildlifeHabitatType habitat))
        {
            commands.TryRecordSeasonalWildlifeArrivalFailure(
                occurrence.instanceId,
                "작성된 방문 서식지를 해석하지 못했습니다.");
            return;
        }

        ecosystem.EnsureInitialized(grid);
        WildlifeHabitatPatch[] patches = ecosystem.Patches
            .Where(value =>
                value != null
                && value.HabitatType == habitat
                && value.IsPreferredBy(definition))
            .ToArray();
        Vector2Int[] positions = grid.GetCells()
            .Where(cell => WildlifeRuntime.IsInitialWildlifeSpawnCell(
                grid,
                cell))
            .Select(cell => cell.Position)
            .Where(position => patches.Any(patch => patch.Contains(position)))
            .Distinct()
            .OrderBy(position => PersistentEntityId.GetStableHash32(
                $"{occurrence.instanceId}:{position.x}:{position.y}"))
            .ThenBy(position => position.x)
            .ThenBy(position => position.y)
            .Take(arrival.exactCount)
            .ToArray();
        if (positions.Length != arrival.exactCount)
        {
            commands.TryRecordSeasonalWildlifeArrivalFailure(
                occurrence.instanceId,
                $"합법 외부 {arrival.requiredHabitatId} 칸이 {arrival.exactCount}개보다 적습니다.");
            return;
        }

        IReadOnlyList<string> wildlifeIds =
            arrivals.ReserveExternalArrivalIds(arrival.exactCount);
        SeasonalWildlifeArrivalMemberSaveData[] members = positions
            .Select((position, index) =>
                new SeasonalWildlifeArrivalMemberSaveData
                {
                    wildlifeId = wildlifeIds[index],
                    positionX = position.x,
                    positionY = position.y
                })
            .ToArray();
        commands.TryRecordSeasonalWildlifeArrivalPlan(
            occurrence.instanceId,
            members,
            out _);
    }

    private void PublishActiveAlert(
        V20ActiveEventSaveData occurrence,
        int absoluteDay,
        WildlifeSpeciesDefinition definition)
    {
        if (occurrence?.seasonalWildlifeArrival?.configured != true)
        {
            return;
        }
        SeasonalWildlifeArrivalSaveData arrival =
            occurrence.seasonalWildlifeArrival;
        int spawned = arrival.members.Count(value => value?.spawned == true);
        int remaining = Math.Max(0, arrival.exactCount - spawned);
        string speciesName = definition?.DisplayName ?? arrival.speciesId;
        string locations = arrival.members.Count == 0
            ? "미확정"
            : string.Join(", ", arrival.members.Select(value =>
                value == null
                    ? "?"
                    : $"({value.positionX},{value.positionY})"));
        string failure = string.IsNullOrEmpty(arrival.lastFailureReason)
            ? string.Empty
            : $"\n현재 지연: {arrival.lastFailureReason}";
        SeasonalWorldEventDefinitionSO authored = catalog.Require(
            occurrence.definitionId);
        events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            authored.DisplayName,
            $"대상: {speciesName} {arrival.exactCount}마리\n"
                + $"도착 확정: {spawned}마리 / 남은 생성: {remaining}마리\n"
                + $"외부 위치: {locations}\n"
                + $"기한: {occurrence.deadlineAbsoluteDay}일 "
                + $"(남은 {Math.Max(0, occurrence.deadlineAbsoluteDay - absoluteDay)}일)"
                + failure,
            arrival.qualification
                == SeasonalWildlifeArrivalQualification.Predatory
                    ? EventAlertImportance.High
                    : EventAlertImportance.Medium,
            AlertCategory,
            Array.Empty<EventAlertChoice>(),
            occurrence.instanceId)));
    }

    private void PublishResolvedAlert(
        V20ContentEffectsResolvedEvent resolved,
        int absoluteDay)
    {
        SeasonalWorldEventDefinitionSO definition;
        try
        {
            definition = catalog.Require(resolved.DefinitionId);
        }
        catch (KeyNotFoundException)
        {
            return;
        }
        if (definition.wildlifeArrivalProfile?.IsConfigured != true)
        {
            return;
        }
        events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            definition.DisplayName,
            $"계절 방문 기한이 {absoluteDay}일에 종료되었습니다. 이미 도착한 개체는 유지되고 미생성 잔여만 취소됩니다.",
            definition.wildlifeArrivalProfile.qualification
                == SeasonalWildlifeArrivalQualification.Predatory
                    ? EventAlertImportance.High
                    : EventAlertImportance.Medium,
            AlertCategory,
            Array.Empty<EventAlertChoice>(),
            resolved.OccurrenceInstanceId,
            isResolved: true,
            resultSummary: "기한 종료: 미생성 잔여 취소, 기존 개체 유지")));
    }

    private void ProcessActiveDriftCargo(int absoluteDay)
    {
        string[] occurrenceIds = seasonal.ActiveSeasonalEvents
            .Where(value => value?.seasonalDriftCargo?.configured == true
                && !value.seasonalDriftCargo.expirationCompleted)
            .Select(value => value.instanceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string occurrenceId in occurrenceIds)
            ProcessDriftCargo(occurrenceId, absoluteDay);
    }

    private void ProcessDriftCargo(string occurrenceId, int absoluteDay)
    {
        V20ActiveEventSaveData occurrence = FindActive(occurrenceId);
        SeasonalDriftCargoSaveData cargo = occurrence?.seasonalDriftCargo;
        if (cargo?.configured != true)
            return;

        if (!cargo.spawned
            && absoluteDay > occurrence.deadlineAbsoluteDay)
        {
            if (!campaign.TryExpireSeasonalDriftCargo(
                    occurrenceId,
                    Array.Empty<PhysicalItemTransformInput>(),
                    out string expirationFailure))
            {
                RecordCargoFailure(occurrenceId, expirationFailure);
                PublishCargoAlert(
                    FindActive(occurrenceId),
                    absoluteDay,
                    null,
                    0L,
                    0);
                return;
            }
            PublishCargoExpiration(
                FindActive(occurrenceId),
                null,
                absoluteDay);
            return;
        }

        if (!items.CatalogProvider.TryGetDefinition(
                cargo.itemId,
                out DungeonItemDefinition itemDefinition))
        {
            RecordCargoFailure(occurrenceId, "작성된 표류 화물 품목을 찾지 못했습니다.");
            PublishCargoAlert(FindActive(occurrenceId), absoluteDay, null, 0L, 0);
            return;
        }
        long unitMassGrams = items.MassQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)cargo.itemId).Value;
        long authoredMassGrams = checked(unitMassGrams * cargo.exactQuantity);
        if (authoredMassGrams is < 6000L or > 11000L)
        {
            RecordCargoFailure(
                occurrenceId,
                $"표류 화물 작성 중량 {authoredMassGrams}g이 허용 범위 6000~11000g 밖입니다.");
            PublishCargoAlert(
                FindActive(occurrenceId),
                absoluteDay,
                itemDefinition,
                unitMassGrams,
                0);
            return;
        }

        if (!cargo.positionFrozen)
        {
            if (!TrySelectExteriorCargoCell(occurrenceId, out Vector2Int position))
            {
                RecordCargoFailure(
                    occurrenceId,
                    "합법적인 접근 가능 외곽 화물 위치가 없어 이번 화물은 생성되지 않습니다.");
                PublishCargoAlert(
                    FindActive(occurrenceId),
                    absoluteDay,
                    itemDefinition,
                    unitMassGrams,
                    0);
                return;
            }
            if (!campaign.TryRecordSeasonalDriftCargoPlan(
                    occurrenceId,
                    position.x,
                    position.y,
                    out string planFailure))
            {
                RecordCargoFailure(occurrenceId, planFailure);
                return;
            }
            occurrence = FindActive(occurrenceId);
            cargo = occurrence.seasonalDriftCargo;
        }

        WorldItemStackSnapshot[] owned = FindCargoStacks(occurrenceId);
        if (!cargo.spawned)
        {
            if (owned.Length == 0)
            {
                bool spawned = items.SpawnItemAtWithComponents(
                    cargo.itemId,
                    cargo.exactQuantity,
                    new Vector2Int(cargo.positionX, cargo.positionY),
                    WorldItemStackState.Loose,
                    string.Empty,
                    new[]
                    {
                        SeasonalDriftCargoRules.CreateComponent(
                            occurrenceId,
                            secured: false)
                    },
                    out int spawnedQuantity);
                if (!spawned || spawnedQuantity != cargo.exactQuantity)
                {
                    RecordCargoFailure(
                        occurrenceId,
                        $"표류 화물 실물 생성 실패 ({spawnedQuantity}/{cargo.exactQuantity}).");
                    return;
                }
                owned = FindCargoStacks(occurrenceId);
            }
            int physicalQuantity = owned.Sum(value => value.Quantity);
            if (physicalQuantity != cargo.exactQuantity
                || owned.Any(value => value.ItemId != cargo.itemId
                    || value.State != WorldItemStackState.Loose
                    || value.Position != new Vector2Int(cargo.positionX, cargo.positionY)))
            {
                RecordCargoFailure(
                    occurrenceId,
                    "표류 화물 원본 lot가 동결 수량·품목·위치와 일치하지 않습니다.");
                return;
            }
            if (!campaign.TryMarkSeasonalDriftCargoSpawned(
                    occurrenceId,
                    owned.Select(value => value.StackId).ToArray(),
                    out string spawnFailure))
            {
                RecordCargoFailure(occurrenceId, spawnFailure);
                return;
            }
            occurrence = FindActive(occurrenceId);
            cargo = occurrence.seasonalDriftCargo;
        }

        if (!TryMarkActuallyPickedCargoSecured(
                occurrenceId,
                cargo,
                out string markerFailure))
        {
            RecordCargoFailure(occurrenceId, markerFailure);
            return;
        }
        owned = FindCargoStacks(occurrenceId);
        WorldItemStackSnapshot[] unpicked = owned
            .Where(value => IsUnpickedSource(value, cargo))
            .ToArray();
        int remainingQuantity = unpicked.Sum(value => value.Quantity);
        int securedQuantity = Math.Max(
            cargo.securedQuantity,
            cargo.exactQuantity - cargo.expiredQuantity - remainingQuantity);
        if (!campaign.TryRecordSeasonalDriftCargoSecured(
                occurrenceId,
                securedQuantity,
                out string progressFailure))
        {
            RecordCargoFailure(occurrenceId, progressFailure);
            return;
        }

        occurrence = FindActive(occurrenceId);
        cargo = occurrence.seasonalDriftCargo;
        if (absoluteDay > occurrence.deadlineAbsoluteDay)
        {
            PhysicalItemTransformInput[] inputs = unpicked
                .Select(value => new PhysicalItemTransformInput(
                    value.StackId,
                    value.Quantity))
                .ToArray();
            if (!campaign.TryExpireSeasonalDriftCargo(
                    occurrenceId,
                    inputs,
                    out string expirationFailure))
            {
                RecordCargoFailure(occurrenceId, expirationFailure);
                PublishCargoAlert(
                    FindActive(occurrenceId),
                    absoluteDay,
                    itemDefinition,
                    unitMassGrams,
                    remainingQuantity);
                return;
            }
            PublishCargoExpiration(
                FindActive(occurrenceId),
                itemDefinition,
                absoluteDay);
            return;
        }
        PublishCargoAlert(
            FindActive(occurrenceId),
            absoluteDay,
            itemDefinition,
            unitMassGrams,
            remainingQuantity);
    }

    private bool TrySelectExteriorCargoCell(
        string occurrenceId,
        out Vector2Int position)
    {
        if (!grids.TryGetGrid(out Grid grid))
        {
            position = default;
            return false;
        }

        GridPathSearchResult[] reachableByHauler =
            (worldRegistry.Characters ?? Array.Empty<CharacterActor>())
            .Where(value => value != null
                && value.CurrentLifecycleState == CharacterLifecycleState.Active
                && value.CarryInventory != null)
            .OrderBy(
                value => value.BuildingCharacterId.Value,
                StringComparer.Ordinal)
            .Select(value => TryGetHaulerSearch(
                    grid,
                    value,
                    out GridPathSearchResult reachable)
                ? reachable
                : null)
            .Where(value => value != null)
            .ToArray();
        if (reachableByHauler.Length == 0)
        {
            position = default;
            return false;
        }

        GridCell selected = grid.GetCells()
            .Where(value => value != null
                && (value.AreaType is GridCellAreaType.Entrance
                    or GridCellAreaType.DropZone
                    or GridCellAreaType.ExteriorPath)
                && value.AllowsItemDrop
                && grid.IsWalkable(value.Position))
            .OrderBy(value => PersistentEntityId.GetStableHash32(
                $"{occurrenceId}:drift-cargo:{value.Position.x}:{value.Position.y}"))
            .ThenBy(value => value.Position.y)
            .ThenBy(value => value.Position.x)
            .FirstOrDefault(value => reachableByHauler.Any(reachable =>
                HasReachablePickupStand(grid, reachable, value.Position)));
        position = selected?.Position ?? default;
        return selected != null;
    }

    private bool TryGetHaulerSearch(
        Grid grid,
        CharacterActor actor,
        out GridPathSearchResult reachable)
    {
        reachable = null;
        return grid != null
            && actor != null
            && pathSearch.TryGetSearch(
                grid,
                actor.GetNowXY(),
                out reachable,
                GridPathSearchPriority.Normal,
                GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(actor)));
    }

    private static bool HasReachablePickupStand(
        Grid grid,
        GridPathSearchResult reachable,
        Vector2Int itemPosition)
    {
        if (grid == null || reachable == null)
            return false;

        Vector2Int[] candidates =
        {
            itemPosition,
            itemPosition + Vector2Int.left,
            itemPosition + Vector2Int.right
        };
        for (int index = 0; index < candidates.Length; index++)
        {
            Vector2Int candidate = candidates[index];
            if (grid.IsValidGridPos(candidate)
                && grid.IsWalkable(candidate)
                && reachable.GetMoveCostTo(candidate) != int.MaxValue)
            {
                return true;
            }
        }
        return false;
    }

    private WorldItemStackSnapshot[] FindCargoStacks(string occurrenceId) =>
        items.GetAllStacks()
            .Where(value => SeasonalDriftCargoRules.TryReadComponent(
                    value,
                    out string owner,
                    out _)
                && string.Equals(owner, occurrenceId, StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();

    private bool TryMarkActuallyPickedCargoSecured(
        string occurrenceId,
        SeasonalDriftCargoSaveData cargo,
        out string failureReason)
    {
        failureReason = string.Empty;
        foreach (WorldItemStackSnapshot stack in FindCargoStacks(occurrenceId))
        {
            if (!SeasonalDriftCargoRules.TryReadComponent(
                    stack,
                    out _,
                    out bool secured)
                || secured
                || IsUnpickedSource(stack, cargo))
            {
                continue;
            }
            if (!items.TrySetInstanceComponent(
                    stack.StackId,
                    SeasonalDriftCargoRules.CreateComponent(
                        occurrenceId,
                        secured: true)))
            {
                failureReason =
                    $"실제 픽업 화물 '{stack.StackId}'의 소유권 표식을 저장하지 못했습니다.";
                return false;
            }
        }
        return true;
    }

    private static bool IsUnpickedSource(
        WorldItemStackSnapshot stack,
        SeasonalDriftCargoSaveData cargo) =>
        stack != null
        && cargo != null
        && SeasonalDriftCargoRules.TryReadComponent(
            stack,
            out _,
            out bool secured)
        && !secured
        && cargo.sourceStackIds.Contains(stack.StackId, StringComparer.Ordinal)
        && stack.State == WorldItemStackState.Loose
        && stack.DropDisposition == WorldItemDropDisposition.None
        && stack.Position == new Vector2Int(cargo.positionX, cargo.positionY);

    private void RecordCargoFailure(string occurrenceId, string failure) =>
        campaign.TryRecordSeasonalDriftCargoFailure(
            occurrenceId,
            failure);

    private void PublishCargoAlert(
        V20ActiveEventSaveData occurrence,
        int absoluteDay,
        DungeonItemDefinition item,
        long unitMassGrams,
        int remainingQuantity)
    {
        SeasonalDriftCargoSaveData cargo = occurrence?.seasonalDriftCargo;
        if (cargo?.configured != true)
            return;
        string itemName = item?.DisplayName ?? cargo.itemId;
        string location = cargo.positionFrozen
            ? $"({cargo.positionX},{cargo.positionY})"
            : "미확정";
        int available = FindCargoStacks(occurrence.instanceId)
            .Where(value => IsUnpickedSource(value, cargo))
            .Sum(value => value.AvailableQuantity);
        string failure = string.IsNullOrEmpty(cargo.lastFailureReason)
            ? string.Empty
            : $"\n현재 지연: {cargo.lastFailureReason}";
        SeasonalWorldEventDefinitionSO authored = catalog.Require(
            occurrence.definitionId);
        events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            authored.DisplayName,
            $"대상: {itemName} {cargo.exactQuantity}개\n"
                + $"외곽 위치: {location}\n"
                + $"미회수: {remainingQuantity}개 / "
                + $"{remainingQuantity * unitMassGrams / 1000f:0.###}kg\n"
                + $"실제 픽업 확보: {cargo.securedQuantity}개 / "
                + $"현재 픽업 가능: {available}개\n"
                + $"기한: {occurrence.deadlineAbsoluteDay}일 "
                + $"(남은 {Math.Max(0, occurrence.deadlineAbsoluteDay - absoluteDay)}일)"
                + failure,
            EventAlertImportance.Medium,
            CargoAlertCategory,
            Array.Empty<EventAlertChoice>(),
            occurrence.instanceId)));
    }

    private void PublishCargoExpiration(
        V20ActiveEventSaveData occurrence,
        DungeonItemDefinition item,
        int absoluteDay)
    {
        SeasonalDriftCargoSaveData cargo = occurrence?.seasonalDriftCargo;
        if (cargo?.expirationCompleted != true)
            return;
        SeasonalWorldEventDefinitionSO authored = catalog.Require(
            occurrence.definitionId);
        string itemName = item?.DisplayName ?? cargo.itemId;
        events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            authored.DisplayName,
            $"회수 기한이 {absoluteDay}일에 끝났습니다. "
                + $"{itemName} 확보 {cargo.securedQuantity}개, "
                + $"미픽업 원본 유실 {cargo.expiredQuantity}개 "
                + $"({cargo.expiredMassGrams / 1000f:0.###}kg). "
                + "운반 중·회수 드롭·일반 재고는 유지됩니다.",
            EventAlertImportance.Medium,
            CargoAlertCategory,
            Array.Empty<EventAlertChoice>(),
            occurrence.instanceId,
            isResolved: true,
            resultSummary:
                $"확보 {cargo.securedQuantity} / 원본 유실 {cargo.expiredQuantity}")));
    }

    private V20ActiveEventSaveData FindActive(string occurrenceId) =>
        seasonal.ActiveSeasonalEvents.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.instanceId,
                occurrenceId,
                StringComparison.Ordinal));
}
