using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using UnityEngine;
using VContainer.Unity;

public sealed class FactionRuntimeApplicationAdapter :
    IFactionRuntime,
    IStartable,
    ITickable,
    IDisposable
{
    private const float SecondsPerHex = 20f;
    private readonly ResourceDungeonFactionCatalogApplicationAdapter catalog;
    private readonly FactionDomainRuntime domain;
    private readonly IOffenseWorldSimulation world;
    private readonly IWorldItemSpawner itemSpawner;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IRunCharacterCatalog characterCatalog;
    private readonly ICharacterSpawnerProvider spawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterFactory;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    [ApplicationAdapterTransientState]
    private IDisposable daySubscription;
    [ApplicationAdapterTransientState]
    private bool synchronizingWorldHomes;
    [ApplicationAdapterTransientState]
    private int projectedRestoreRevision;

    private IEnumerable<DungeonFactionState> factions => domain.FactionStates;
    private IReadOnlyList<FactionRouteState> routes => domain.Routes;
    private int currentDay => domain.CurrentDay;

    public FactionRuntimeApplicationAdapter(
        ResourceDungeonFactionCatalogApplicationAdapter catalog,
        IOffenseWorldSimulation world,
        FactionItemLogisticsDependencies itemLogistics,
        FactionCharacterSpawnDependencies characterSpawning,
        IGameClock clock,
        IGameEventBus events,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        itemLogistics = itemLogistics
            ?? throw new ArgumentNullException(nameof(itemLogistics));
        characterSpawning = characterSpawning
            ?? throw new ArgumentNullException(nameof(characterSpawning));
        itemSpawner = itemLogistics.ItemSpawner;
        itemRuntime = itemLogistics.ItemRuntime;
        dropZones = itemLogistics.DropZones;
        characterCatalog = characterSpawning.CharacterCatalog;
        spawnerProvider = characterSpawning.SpawnerProvider;
        characterFactory = characterSpawning.CharacterFactory;
        worldRegistry = characterSpawning.WorldRegistry;
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        domain = new FactionDomainRuntime(
            aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore)));
    }

    public IReadOnlyList<FactionDefinitionSnapshot> Definitions =>
        catalog.Definitions;
    public IReadOnlyList<DungeonFactionState> Factions =>
        factions.OrderBy(value => value.factionId, StringComparer.Ordinal)
            .ToArray();
    public IReadOnlyList<FactionRouteState> Routes => routes;

    public void Start()
    {
        EnsureInitialized();
        world.Changed += OnWorldChanged;
        projectedRestoreRevision = domain.PublishedRestoreRevision;
        SynchronizeWorldHomes();
        daySubscription = events.Subscribe<OperatingDayStartedEvent>(
            value => domain.SetCurrentDay(value.day));
    }

    public void Dispose()
    {
        world.Changed -= OnWorldChanged;
        daySubscription?.Dispose();
        daySubscription = null;
    }

    public void Tick()
    {
        EnsureWorldHomesProjectionCurrent();
        foreach (FactionRouteState arrived in routes.Where(value =>
                     value != null
                     && value.kind == FactionRouteKind.Reinforcement
                     && value.status == FactionRouteStatus.Arrived
                     && !value.actorsSpawned))
        {
            MaterializeReinforcements(arrived);
        }

        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        foreach (FactionRouteState route in domain.AdvanceRoutes(
                     clock.DeltaTime,
                     SecondsPerHex))
        {
            CompleteRoute(route);
        }
    }

    public bool TryGetFaction(
        string factionId,
        out DungeonFactionState faction)
    {
        EnsureInitialized();
        return domain.TryGetFaction(factionId, out faction);
    }

    public bool IsContractUnlocked(
        string factionId,
        FactionContractKind contract)
    {
        return TryGetFaction(factionId, out DungeonFactionState faction)
            && domain.IsContractUnlocked(faction, contract);
    }

    public bool TryAdjustTrust(
        string factionId,
        int amount,
        string reason,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            message = "세력을 찾을 수 없습니다.";
            return false;
        }

        if (amount > 0 && faction.NegotiationBlocked(currentDay))
        {
            message = $"배신 후 협상 봉쇄가 Day {faction.negotiationBlockedUntilDay}까지 유지됩니다.";
            return false;
        }

        FactionTrustTransition transition = domain.AdjustTrust(faction, amount);
        events.Publish(new FactionTrustChangedEvent(
            transition.FactionId,
            transition.Previous,
            transition.Current,
            reason));
        message =
            $"{DisplayName(factionId)} 신뢰 {transition.Previous} → {transition.Current}";
        return true;
    }

    public bool TryOfferGoodwill(
        string factionId,
        int physicalValue,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            message = "호의를 보낼 세력을 찾을 수 없습니다.";
            return false;
        }

        if (faction.NegotiationBlocked(currentDay))
        {
            message =
                $"배신 후 협상 봉쇄가 Day {faction.negotiationBlockedUntilDay}까지 유지됩니다.";
            return false;
        }

        int offered = Mathf.Max(0, physicalValue);
        if (offered < 50
            || !TryConsumePhysicalGoods(offered, out int consumedValue))
        {
            message = "예약되지 않은 실물 물자 가치 50 이상이 필요합니다.";
            return false;
        }

        domain.AcceptGoodwill(faction);
        int trustGain = Mathf.Clamp(consumedValue / 10, 1, 10);
        TryAdjustTrust(
            factionId,
            trustGain,
            $"호의 물자 {consumedValue}",
            out _);
        message =
            $"{DisplayName(factionId)} 호의 물자 {consumedValue} 전달 · 신뢰 +{trustGain}";
        return true;
    }

    public bool TryCompleteAllianceProject(
        string factionId,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction)
            || faction.trust < 70)
        {
            message = "신뢰 70 이상이어야 동맹 프로젝트를 완료할 수 있습니다.";
            return false;
        }

        domain.CompleteAllianceProject(faction);
        message = $"{DisplayName(factionId)} 동맹 프로젝트 완료";
        return true;
    }

    public bool TryRequestTrade(
        string factionId,
        out string routeId,
        out string message)
    {
        return TryCreateRoute(
            factionId,
            FactionContractKind.Trade,
            FactionRouteKind.TradeCaravan,
            FindDefinition(factionId)?.TradeCargo,
            100,
            out routeId,
            out message);
    }

    public bool TryRequestSupply(
        string factionId,
        out string routeId,
        out string message)
    {
        return TryCreateRoute(
            factionId,
            FactionContractKind.Supply,
            FactionRouteKind.SupplyCaravan,
            FindDefinition(factionId)?.SupplyCargo,
            100,
            out routeId,
            out message);
    }

    public bool TryRequestReinforcement(
        string factionId,
        out string routeId,
        out string message)
    {
        return TryCreateRoute(
            factionId,
            FactionContractKind.Reinforcement,
            FactionRouteKind.Reinforcement,
            Array.Empty<FactionCargoLine>(),
            100,
            out routeId,
            out message);
    }

    public bool TryApplyRouteAmbush(
        string routeId,
        int strengthLoss,
        float delaySeconds,
        out string message)
    {
        FactionRouteState route = domain.FindTravelingRoute(routeId);
        if (route == null)
        {
            message = "매복을 적용할 이동 중 경로가 없습니다.";
            return false;
        }

        domain.ApplyRouteAmbush(route, strengthLoss, delaySeconds);
        if (route.strength <= 0)
        {
            message = "상단 또는 지원군이 매복으로 전멸했습니다.";
            return true;
        }

        message = $"매복 발생 · 전력 {route.strength} · 지연 {route.delaySeconds:0}초";
        return true;
    }

    public bool TryBetray(
        string factionId,
        int stolenValue,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState target))
        {
            message = "배신할 세력을 찾을 수 없습니다.";
            return false;
        }

        if (!TrySpawnBetrayalLoot(
                factionId,
                stolenValue,
                out int actualLootValue,
                out message))
        {
            return false;
        }

        foreach (FactionTrustTransition transition in
                 domain.ApplyBetrayal(target, actualLootValue))
        {
            events.Publish(new FactionTrustChangedEvent(
                transition.FactionId,
                transition.Previous,
                transition.Current,
                string.Equals(
                    transition.FactionId,
                    factionId,
                    StringComparison.Ordinal)
                    ? "동맹 던전 약탈"
                    : "다른 던전 배신 목격"));
        }

        message =
            $"{DisplayName(factionId)} 배신 · 실물 약탈 가치 {actualLootValue} · " +
            $"협상 봉쇄 Day {target.negotiationBlockedUntilDay}까지";
        SynchronizeWorldHomes();
        return true;
    }

    public bool TryPayRestitution(
        string factionId,
        int physicalValue,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            message = "배상할 세력을 찾을 수 없습니다.";
            return false;
        }

        int required = domain.GetRestitutionRequired(faction);
        if (physicalValue < required
            || !TryConsumePhysicalGoods(required, out int consumedValue))
        {
            message = $"물리 배상 가치 {required}가 필요합니다.";
            return false;
        }

        domain.AcceptRestitution(faction);
        message =
            $"{DisplayName(factionId)} 실물 배상 {consumedValue} 접수 완료";
        return true;
    }

    private bool TrySpawnBetrayalLoot(
        string factionId,
        int requestedValue,
        out int actualValue,
        out string message)
    {
        actualValue = 0;
        message = string.Empty;
        FactionDefinitionSnapshot definition = FindDefinition(factionId);
        if (definition == null
            || !dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            message = "약탈품을 실물로 내릴 하차장을 찾을 수 없습니다.";
            return false;
        }

        FactionCargoLine[] warehouse = definition.TradeCargo
            .Concat(definition.SupplyCargo)
            .Where(line => line != null
                && !string.IsNullOrWhiteSpace(line.itemId)
                && line.amount > 0)
            .ToArray();
        int targetValue = Mathf.Max(1, requestedValue);
        foreach (FactionCargoLine line in warehouse)
        {
            DungeonItemDefinition item =
                itemRuntime.CatalogProvider.GetDefinition(line.itemId);
            int unitValue = Mathf.Max(1, item.UnitPrice);
            int remainingValue = Mathf.Max(0, targetValue - actualValue);
            int quantity = Mathf.Clamp(
                Mathf.CeilToInt(remainingValue / (float)unitValue),
                0,
                line.amount);
            if (quantity <= 0)
            {
                continue;
            }

            int spawned = itemSpawner.Spawn(
                line.itemId,
                quantity,
                dropoff,
                WorldItemStackState.Loose,
                $"faction-betrayal:{factionId}:{currentDay}");
            actualValue += spawned * unitValue;
            if (actualValue >= targetValue)
            {
                break;
            }
        }

        if (actualValue <= 0)
        {
            message = "약탈 가능한 팩션 창고 물자가 없습니다.";
            return false;
        }

        return true;
    }

    private bool TryConsumePhysicalGoods(
        int requiredValue,
        out int consumedValue)
    {
        consumedValue = 0;
        List<(WorldItemStackSnapshot stack, int quantity)> selection =
            new List<(WorldItemStackSnapshot, int)>();
        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks()
                     .Where(value => value != null
                         && !value.IsReserved
                         && !value.HasUniqueMetadata
                         && value.Quantity > 0
                         && value.UnitPrice > 0
                         && value.State is WorldItemStackState.Loose
                             or WorldItemStackState.Stored)
                     .OrderByDescending(value => value.UnitPrice)
                     .ThenBy(value => value.StackId, StringComparer.Ordinal))
        {
            int remaining = requiredValue - consumedValue;
            int quantity = Mathf.Clamp(
                Mathf.CeilToInt(remaining / (float)stack.UnitPrice),
                0,
                stack.Quantity);
            if (quantity <= 0)
            {
                continue;
            }

            selection.Add((stack, quantity));
            consumedValue += quantity * stack.UnitPrice;
            if (consumedValue >= requiredValue)
            {
                break;
            }
        }

        if (consumedValue < requiredValue)
        {
            consumedValue = 0;
            return false;
        }

        foreach ((WorldItemStackSnapshot stack, int quantity) in selection)
        {
            if (!itemRuntime.TryConsumeStackQuantity(
                    stack.StackId,
                    quantity,
                    out _))
            {
                throw new InvalidOperationException(
                    $"Restitution stack '{stack.StackId}' changed during atomic consumption.");
            }
        }

        return true;
    }

    public bool TryCompleteRecoveryEvent(
        string factionId,
        out string message)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            message = "복구 사건의 세력을 찾을 수 없습니다.";
            return false;
        }

        domain.CompleteRecoveryEvent(faction);
        message = $"{DisplayName(factionId)} 구조·방어 사건 완료";
        return true;
    }

    public void RecordReinforcementLoss(
        string factionId,
        int deaths,
        int equipmentLosses)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction))
        {
            return;
        }

        domain.RecordReinforcementLoss(faction, deaths, equipmentLosses);
    }

    public DungeonFactionSaveData Capture()
    {
        EnsureInitialized();
        return new DungeonFactionSaveData
        {
            currentDay = currentDay,
            routeSequence = domain.RouteSequence,
            factions = Factions.Select(CloneFaction).ToList(),
            routes = routes
                .Select(CloneRoute)
                .OrderBy(value => FactionPayloadValidation.RouteSequenceOf(value.routeId))
                .ToList()
        };
    }

    public FactionRestoreCandidate PrepareRestoreCandidate(
        DungeonFactionSaveData saveData)
    {
        if (saveData?.factions == null || saveData.routes == null)
        {
            throw new InvalidOperationException(
                "Faction restore payload or collections are missing.");
        }

        FactionAggregateState restored = new()
        {
            CurrentDay = saveData.currentDay,
            RouteSequence = saveData.routeSequence
        };
        foreach (DungeonFactionState savedFaction in saveData.factions)
        {
            DungeonFactionState clone = CloneFaction(savedFaction);
            restored.Factions.Add(clone.factionId, clone);
        }
        restored.Routes.AddRange(saveData.routes.Select(CloneRoute));
        return new FactionRestoreCandidate(restored, saveData);
    }

    public void PublishRestoreCandidate(FactionRestoreCandidate candidate)
    {
        domain.ReplaceState((candidate
            ?? throw new ArgumentNullException(nameof(candidate))).State);
        SynchronizeWorldHomesUnlessStaging();
    }

    public void Reset()
    {
        domain.ReplaceState(CreateDefaultState());
        SynchronizeWorldHomesUnlessStaging();
    }

    private void EnsureInitialized()
    {
        if (factions.Any())
        {
            return;
        }

        domain.ReplaceState(CreateDefaultState());
        SynchronizeWorldHomesUnlessStaging();
    }

    private void SynchronizeWorldHomesUnlessStaging()
    {
        if (!domain.IsRestoreStaging)
        {
            SynchronizeWorldHomes();
        }
    }

    private void EnsureWorldHomesProjectionCurrent()
    {
        int publishedRevision = domain.PublishedRestoreRevision;
        if (projectedRestoreRevision == publishedRevision)
        {
            return;
        }

        projectedRestoreRevision = publishedRevision;
        SynchronizeWorldHomes();
    }

    private FactionAggregateState CreateDefaultState()
    {
        FactionAggregateState created = new();

        HashSet<OffenseHexCoord> occupied = world.Sites
            .Where(site => site != null && site.IsActive)
            .Select(site => site.Coord)
            .ToHashSet();
        IReadOnlyList<OffenseHexTileState> candidates = world.Tiles
            .Where(tile => tile != null
                && !tile.blocked
                && tile.Coord.DistanceTo(world.DungeonCoord) >= 4
                && !occupied.Contains(tile.Coord))
            .OrderBy(tile => tile.Coord)
            .ToArray();
        for (int i = 0; i < catalog.Definitions.Count; i++)
        {
            FactionDefinitionSnapshot definition = catalog.Definitions[i];
            int startIndex = candidates.Count > 0
                ? StableIndex(definition.StableId, candidates.Count)
                : 0;
            OffenseHexCoord home = candidates.Count > 0
                ? FindUnusedHome(candidates, occupied, startIndex)
                : new OffenseHexCoord(i + 4, -i);
            occupied.Add(home);
            created.Factions[definition.StableId] = new DungeonFactionState
            {
                factionId = definition.StableId,
                trust = 0,
                discovered = false,
                homeQ = home.Q,
                homeR = home.R
            };
        }

        return created;
    }

    private void OnWorldChanged()
    {
        if (synchronizingWorldHomes)
        {
            return;
        }

        synchronizingWorldHomes = true;
        try
        {
            foreach (DungeonFactionState faction in factions.ToArray())
            {
                string siteId = HomeSiteId(
                    faction.factionId,
                    faction.betrayalScars);
                if (world.TryGetSite(
                        siteId,
                        out OffenseWorldSiteStateData site)
                    && site.state == OffenseWorldSiteState.Resolved)
                {
                    TryBetray(faction.factionId, 400, out _);
                }
            }
        }
        finally
        {
            synchronizingWorldHomes = false;
        }

        SynchronizeWorldHomes();
    }

    private void SynchronizeWorldHomes()
    {
        if (synchronizingWorldHomes
            || world.Tiles == null
            || world.Tiles.Count == 0)
        {
            return;
        }

        synchronizingWorldHomes = true;
        try
        {
            foreach (DungeonFactionState faction in factions)
            {
                FactionDefinitionSnapshot definition =
                    FindDefinition(faction.factionId);
                world.TryRegisterStrategicSite(new OffenseWorldSiteStateData
                {
                    siteId = HomeSiteId(
                        faction.factionId,
                        faction.betrayalScars),
                    archetypeId = HomeArchetype(definition?.SpeciesTag),
                    displayName =
                        $"{definition?.DisplayName ?? faction.factionId} 본거지",
                    q = faction.homeQ,
                    r = faction.homeR,
                    regionId = $"region:{faction.factionId}",
                    factionId = faction.factionId,
                    state = OffenseWorldSiteState.Revealed,
                    fixedBoss = false,
                    strength = Mathf.Clamp(
                        5 + faction.betrayalScars,
                        5,
                        9),
                    createdDay = currentDay,
                    expiresDay = int.MaxValue,
                    pressureAxis = StrategicPressureAxis.None,
                    pressureAmount = 0f
                });
            }
        }
        finally
        {
            synchronizingWorldHomes = false;
        }
    }

    private static OffenseHexCoord FindUnusedHome(
        IReadOnlyList<OffenseHexTileState> candidates,
        ISet<OffenseHexCoord> occupied,
        int startIndex)
    {
        for (int offset = 0; offset < candidates.Count; offset++)
        {
            OffenseHexCoord coord =
                candidates[(startIndex + offset) % candidates.Count].Coord;
            if (!occupied.Contains(coord))
            {
                return coord;
            }
        }

        return candidates[startIndex % candidates.Count].Coord;
    }

    private static string HomeSiteId(string factionId, int raidSequence) =>
        $"faction-home:{factionId}:{Mathf.Max(0, raidSequence)}";

    private static string HomeArchetype(string speciesTag)
    {
        return speciesTag?.Trim().ToLowerInvariant() switch
        {
            "beastkin" => "farm",
            "demon" => "ritual_site",
            "kobold" => "armory",
            "myconid" => "farm",
            "harpy" => "watchtower",
            "golem" => "quarry",
            _ => "ruin"
        };
    }

    private bool TryCreateRoute(
        string factionId,
        FactionContractKind requiredContract,
        FactionRouteKind kind,
        IEnumerable<FactionCargoLine> cargo,
        int strength,
        out string routeId,
        out string message)
    {
        routeId = string.Empty;
        if (!TryGetFaction(factionId, out DungeonFactionState faction)
            || !IsContractUnlocked(factionId, requiredContract))
        {
            message = $"{requiredContract} 계약의 신뢰 또는 동맹 조건이 부족합니다.";
            return false;
        }

        if (!world.TryFindPath(
                new OffenseHexCoord(faction.HomeCoord.Q, faction.HomeCoord.R),
                world.DungeonCoord,
                OffenseTravelProfile.Default,
                out IReadOnlyList<OffenseHexCoord> path,
                out _))
        {
            message = "팩션 거점에서 던전까지 도달 가능한 육각 경로가 없습니다.";
            return false;
        }

        int steps = Mathf.Max(1, path.Count - 1);
        FactionRouteState route = new FactionRouteState
        {
            factionId = factionId,
            kind = kind,
            status = FactionRouteStatus.Traveling,
            path = path.Select(value => FactionHexCoordSaveData.From(
                new FactionHexCoord(value.Q, value.R))).ToList(),
            strength = Mathf.Clamp(strength, 1, 100),
            createdDay = currentDay,
            estimatedArrivalDay =
                currentDay + Mathf.CeilToInt(steps * SecondsPerHex / 180f),
            cargo = (cargo ?? Array.Empty<FactionCargoLine>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList()
        };
        routeId = domain.AddRoute(route);
        message = $"{DisplayName(factionId)} 경로 출발 · ETA Day {route.estimatedArrivalDay}";
        return true;
    }

    private void CompleteRoute(FactionRouteState route)
    {
        if (route.kind is FactionRouteKind.TradeCaravan
            or FactionRouteKind.SupplyCaravan
            or FactionRouteKind.Restitution)
        {
            DeliverCargo(route);
        }
        else if (route.kind == FactionRouteKind.Reinforcement)
        {
            MaterializeReinforcements(route);
        }

        events.Publish(new FactionRouteArrivedEvent(
            route.routeId,
            route.factionId,
            route.kind,
            route.strength));
    }

    private void DeliverCargo(FactionRouteState route)
    {
        if (route.cargoDelivered
            || !dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            return;
        }

        foreach (FactionCargoLine line in route.cargo)
        {
            itemSpawner.Spawn(
                line.itemId,
                Mathf.Max(1, Mathf.RoundToInt(
                    line.amount * route.strength / 100f)),
                dropoff,
                WorldItemStackState.Loose,
                $"faction-delivery:{route.routeId}");
        }
        domain.MarkCargoDelivered(route);
    }

    private void MaterializeReinforcements(FactionRouteState route)
    {
        if (route.actorsSpawned
            || !spawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            || spawner == null
            || spawner.characterPrefab == null
            || !spawner.TryGetEntryGridPosition(out Vector2Int entry))
        {
            return;
        }

        FactionDefinitionSnapshot definition = FindDefinition(route.factionId);
        CharacterSO template = characterCatalog.Characters
            .Where(value => value != null)
            .Where(value => string.Equals(
                value.SpeciesTag,
                definition?.SpeciesTag,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.id)
            .FirstOrDefault();
        if (template == null)
        {
            return;
        }

        int count = Mathf.Clamp(
            Mathf.CeilToInt(route.strength / 40f),
            1,
            3);
        for (int index = 0; index < count; index++)
        {
            GameObject instance = characterFactory.CreateInactive(
                spawner.characterPrefab);
            CharacterActor actor = instance.GetComponent<CharacterActor>();
            if (actor == null)
            {
                characterFactory.Destroy(instance);
                continue;
            }

            string actorId = $"{route.routeId}:ally:{index + 1}";
            instance.name =
                $"{definition?.DisplayName ?? route.factionId} 지원군 {index + 1}";
            instance.transform.position =
                spawner.GetOutsideSpawnWorldPosition()
                + Vector3.right * (index * 0.35f);
            actor.Initialize(template);
            actor.characterType = CharacterType.NPC;
            actor.Identity?.SetCharacterType(CharacterType.NPC);
            actor.Identity?.SetPersistentId(actorId);
            actor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
            if (actor.TryGetAbility(out AbilityWork work))
            {
                work.WorkPriorities.SetPriority(
                    BuiltInWorkTypeIds.Guard,
                    WorkPriorityLevel.Priority1);
            }

            FactionReinforcementMarker marker =
                instance.GetComponent<FactionReinforcementMarker>()
                ?? instance.AddComponent<FactionReinforcementMarker>();
            marker.Configure(route.routeId, route.factionId, route.strength);
            domain.AddReinforcementActor(route, actorId);
            characterFactory.Publish(instance);

            if (actor.TryGetAbility(out AbilityMove move))
            {
                move.StartEnterDungeon(
                    spawner.GetEntryDoorWorldPosition(),
                    entry);
            }
            else if (worldRegistry.TryGetGrid(out Grid grid))
            {
                actor.transform.position = grid.GetWorldPos(entry);
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }
        }

        domain.FinishReinforcementMaterialization(route);
    }

    private FactionDefinitionSnapshot FindDefinition(string factionId)
    {
        return catalog.Definitions.FirstOrDefault(value =>
            string.Equals(value.StableId, factionId, StringComparison.Ordinal));
    }

    private string DisplayName(string factionId)
    {
        return FindDefinition(factionId)?.DisplayName ?? factionId;
    }

    private int StableIndex(string id, int count)
    {
        unchecked
        {
            int hash = world.WorldSeed;
            foreach (char character in id ?? string.Empty)
            {
                hash = hash * 31 + character;
            }
            return Mathf.Abs(hash == int.MinValue ? 0 : hash) % Mathf.Max(1, count);
        }
    }

    private static DungeonFactionState CloneFaction(DungeonFactionState value)
    {
        return JsonUtility.FromJson<DungeonFactionState>(
            JsonUtility.ToJson(value)) ?? new DungeonFactionState();
    }

    private static FactionRouteState CloneRoute(FactionRouteState value)
    {
        return JsonUtility.FromJson<FactionRouteState>(
            JsonUtility.ToJson(value)) ?? new FactionRouteState();
    }
}
