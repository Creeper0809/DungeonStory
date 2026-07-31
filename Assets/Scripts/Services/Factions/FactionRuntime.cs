using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class ResourceDungeonFactionCatalog
{
    private readonly IReadOnlyList<DungeonFactionDefinitionSO> definitions;

    public ResourceDungeonFactionCatalog(IResourcesAssetLoader resources)
    {
        List<DungeonFactionDefinitionSO> loaded =
            (resources?.LoadAllOptional<DungeonFactionDefinitionSO>(
                    DungeonFactionDefinitionSO.ResourcePath)
                ?? Array.Empty<DungeonFactionDefinitionSO>())
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.StableId))
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToList();
        if (loaded.Count == 0)
        {
            loaded.AddRange(CreateFallbackDefinitions());
        }

        definitions = loaded;
    }

    public IReadOnlyList<DungeonFactionDefinitionSO> Definitions => definitions;

    private static IEnumerable<DungeonFactionDefinitionSO>
        CreateFallbackDefinitions()
    {
        yield return Create(
            "faction:dungeon:beastkin", "붉은발 역참", "Beastkin",
            "빠른 교역·정찰·긴급 운반 지원",
            new[] { "야외", "무리", "소음" },
            StockCategory.Food, StockCategory.Biological);
        yield return Create(
            "faction:dungeon:demon", "잿불 계약정", "Demon",
            "마력 촉매와 화염·저주 전투 지원",
            new[] { "화염", "마력", "고급" },
            StockCategory.Mana, StockCategory.General);
        yield return Create(
            "faction:dungeon:kobold", "심층 톱니굴", "Kobold",
            "함정 재장전·수리와 광물·탄약 지원",
            new[] { "질서", "협소", "기계" },
            StockCategory.General, StockCategory.Ammunition);
        yield return Create(
            "faction:dungeon:myconid", "균사 심림", "Myconid",
            "치료·제독과 약품·퇴비 지원",
            new[] { "습기", "오염", "저온" },
            StockCategory.Medicine, StockCategory.Biological);
        yield return Create(
            "faction:dungeon:harpy", "폭풍 둥지", "Harpy",
            "정보·원거리 탄약과 외부 고지 엄호",
            new[] { "야외", "청정", "개방" },
            StockCategory.Knowledge, StockCategory.Ammunition);
        yield return Create(
            "faction:dungeon:golem", "석맥 주조소", "Golem",
            "장갑판·동력핵과 방패벽·시설 복구 지원",
            new[] { "질서", "마력", "기계" },
            StockCategory.General, StockCategory.Mana);
    }

    private static DungeonFactionDefinitionSO Create(
        string id,
        string name,
        string species,
        string description,
        string[] tags,
        StockCategory primary,
        StockCategory secondary)
    {
        DungeonFactionDefinitionSO value =
            ScriptableObject.CreateInstance<DungeonFactionDefinitionSO>();
        value.factionId = id;
        value.displayName = name;
        value.speciesTag = species;
        value.description = description;
        value.relationTags = tags;
        value.tradeTags = tags;
        value.reinforcementRole = description;
        value.tradeCargo = new List<FactionCargoLine>
        {
            Cargo(primary, 8),
            Cargo(secondary, 5)
        };
        value.supplyCargo = new List<FactionCargoLine>
        {
            Cargo(primary, 14),
            Cargo(secondary, 10)
        };
        return value;
    }

    private static FactionCargoLine Cargo(StockCategory category, int amount)
    {
        return new FactionCargoLine
        {
            itemId = DungeonItemCatalogSO.StockItemId(category),
            amount = amount
        };
    }
}

public sealed class FactionRuntime :
    IFactionRuntime,
    IStartable,
    ITickable,
    IDisposable
{
    private const float SecondsPerHex = 20f;
    private const int BetrayalEmbargoDays = 10;

    private readonly ResourceDungeonFactionCatalog catalog;
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
    private readonly Dictionary<string, DungeonFactionState> factions =
        new Dictionary<string, DungeonFactionState>(StringComparer.Ordinal);
    private readonly List<FactionRouteState> routes =
        new List<FactionRouteState>();
    private IDisposable daySubscription;
    private bool synchronizingWorldHomes;
    private int currentDay = 1;
    private int routeSequence;

    public FactionRuntime(
        ResourceDungeonFactionCatalog catalog,
        FactionRuntimeProvider runtimeProvider,
        IOffenseWorldSimulation world,
        IWorldItemSpawner itemSpawner,
        IWorldItemStackRuntime itemRuntime,
        IWorldDropZoneQuery dropZones,
        IRunCharacterCatalog characterCatalog,
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterSpawnObjectFactory characterFactory,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock clock,
        IGameEventBus events)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        (runtimeProvider ?? throw new ArgumentNullException(nameof(runtimeProvider)))
            .Bind(this);
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.itemSpawner = itemSpawner
            ?? throw new ArgumentNullException(nameof(itemSpawner));
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.dropZones = dropZones ?? throw new ArgumentNullException(nameof(dropZones));
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
        this.spawnerProvider = spawnerProvider
            ?? throw new ArgumentNullException(nameof(spawnerProvider));
        this.characterFactory = characterFactory
            ?? throw new ArgumentNullException(nameof(characterFactory));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public IReadOnlyList<DungeonFactionDefinitionSO> Definitions =>
        catalog.Definitions;
    public IReadOnlyList<DungeonFactionState> Factions =>
        factions.Values.OrderBy(value => value.factionId, StringComparer.Ordinal)
            .ToArray();
    public IReadOnlyList<FactionRouteState> Routes => routes;

    public void Start()
    {
        EnsureInitialized();
        world.Changed += OnWorldChanged;
        SynchronizeWorldHomes();
        daySubscription = events.Subscribe<OperatingDayStartedEvent>(
            value => currentDay = Mathf.Max(1, value.day));
    }

    public void Dispose()
    {
        world.Changed -= OnWorldChanged;
        daySubscription?.Dispose();
        daySubscription = null;
    }

    public void Tick()
    {
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

        foreach (FactionRouteState route in routes
                     .Where(value => value != null
                         && value.status is FactionRouteStatus.Traveling
                             or FactionRouteStatus.Delayed)
                     .ToArray())
        {
            if (route.delaySeconds > 0f)
            {
                route.status = FactionRouteStatus.Delayed;
                route.delaySeconds = Mathf.Max(
                    0f,
                    route.delaySeconds - clock.DeltaTime);
                if (route.delaySeconds > 0f)
                {
                    continue;
                }
                route.status = FactionRouteStatus.Traveling;
            }

            route.segmentProgress += clock.DeltaTime / SecondsPerHex;
            while (route.segmentProgress >= 1f
                && route.pathIndex < route.path.Count - 1)
            {
                route.segmentProgress -= 1f;
                route.pathIndex++;
            }

            if (route.pathIndex >= route.path.Count - 1)
            {
                CompleteRoute(route);
            }
        }
    }

    public bool TryGetFaction(
        string factionId,
        out DungeonFactionState faction)
    {
        EnsureInitialized();
        return factions.TryGetValue(factionId?.Trim() ?? string.Empty, out faction);
    }

    public bool IsContractUnlocked(
        string factionId,
        FactionContractKind contract)
    {
        if (!TryGetFaction(factionId, out DungeonFactionState faction)
            || faction.NegotiationBlocked(currentDay))
        {
            return false;
        }

        return contract switch
        {
            FactionContractKind.Trade => faction.trust >= 20,
            FactionContractKind.Recruitment => faction.trust >= 35,
            FactionContractKind.Supply => faction.trust >= 50,
            FactionContractKind.Reinforcement =>
                faction.trust >= 70 && faction.allianceProjectCompleted,
            _ => false
        };
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

        int adjusted = amount > 0
            ? Mathf.Max(1, Mathf.RoundToInt(amount
                * Mathf.Pow(0.85f, faction.betrayalScars)))
            : amount;
        int previous = faction.trust;
        faction.trust = Mathf.Clamp(faction.trust + adjusted, -100, 100);
        events.Publish(new FactionTrustChangedEvent(
            factionId,
            previous,
            faction.trust,
            reason));
        message = $"{DisplayName(factionId)} 신뢰 {previous} → {faction.trust}";
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

        faction.discovered = true;
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

        faction.allianceProjectCompleted = true;
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
            FindDefinition(factionId)?.tradeCargo,
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
            FindDefinition(factionId)?.supplyCargo,
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
        FactionRouteState route = routes.FirstOrDefault(value =>
            value != null
            && string.Equals(value.routeId, routeId, StringComparison.Ordinal)
            && value.status is FactionRouteStatus.Traveling
                or FactionRouteStatus.Delayed);
        if (route == null)
        {
            message = "매복을 적용할 이동 중 경로가 없습니다.";
            return false;
        }

        route.ambushed = true;
        route.strength = Mathf.Max(0, route.strength - Mathf.Max(0, strengthLoss));
        route.delaySeconds += Mathf.Max(0f, delaySeconds);
        if (route.strength <= 0)
        {
            route.status = FactionRouteStatus.Lost;
            message = "상단 또는 지원군이 매복으로 전멸했습니다.";
            return true;
        }

        route.status = FactionRouteStatus.Delayed;
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

        int previousTargetTrust = target.trust;
        target.trust = -100;
        target.betrayalScars++;
        target.negotiationBlockedUntilDay = currentDay + BetrayalEmbargoDays;
        target.restitutionPaid = false;
        target.recoveryEventCompleted = false;
        target.lastBetrayalLootValue = actualLootValue;
        target.restitutionRequiredValue =
            Mathf.CeilToInt(actualLootValue * 1.5f);
        target.discovered = true;
        events.Publish(new FactionTrustChangedEvent(
            factionId,
            previousTargetTrust,
            target.trust,
            "동맹 던전 약탈"));
        foreach (DungeonFactionState peer in factions.Values
                     .Where(value => !ReferenceEquals(value, target)))
        {
            int previousPeerTrust = peer.trust;
            peer.trust = Mathf.Max(-100, peer.trust - 15);
            events.Publish(new FactionTrustChangedEvent(
                peer.factionId,
                previousPeerTrust,
                peer.trust,
                "다른 던전 배신 목격"));
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

        int required = Mathf.Max(
            1,
            faction.restitutionRequiredValue > 0
                ? faction.restitutionRequiredValue
                : 150 * Mathf.Max(1, faction.betrayalScars));
        if (physicalValue < required
            || !TryConsumePhysicalGoods(required, out int consumedValue))
        {
            message = $"물리 배상 가치 {required}가 필요합니다.";
            return false;
        }

        faction.restitutionPaid = true;
        TryFinishRecovery(faction);
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
        DungeonFactionDefinitionSO definition = FindDefinition(factionId);
        if (definition == null
            || !dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            message = "약탈품을 실물로 내릴 하차장을 찾을 수 없습니다.";
            return false;
        }

        FactionCargoLine[] warehouse = definition.tradeCargo
            .Concat(definition.supplyCargo)
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

        faction.recoveryEventCompleted = true;
        TryFinishRecovery(faction);
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

        int dead = Mathf.Max(0, deaths);
        int lost = Mathf.Max(0, equipmentLosses);
        faction.reinforcementDeaths += dead;
        faction.equipmentLosses += lost;
        faction.trust = Mathf.Clamp(
            faction.trust - dead * 4 - lost,
            -100,
            100);
    }

    public DungeonFactionSaveData Capture()
    {
        EnsureInitialized();
        return new DungeonFactionSaveData
        {
            currentDay = currentDay,
            routeSequence = routeSequence,
            factions = Factions.Select(CloneFaction).ToList(),
            routes = routes.Select(CloneRoute).ToList()
        };
    }

    public void Restore(DungeonFactionSaveData saveData)
    {
        Reset();
        if (saveData == null)
        {
            return;
        }

        currentDay = Mathf.Max(1, saveData.currentDay);
        routeSequence = Mathf.Max(0, saveData.routeSequence);
        foreach (DungeonFactionState state in saveData.factions
                     ?? new List<DungeonFactionState>())
        {
            if (state != null && factions.ContainsKey(state.factionId))
            {
                factions[state.factionId] = CloneFaction(state);
            }
        }

        routes.AddRange((saveData.routes ?? new List<FactionRouteState>())
            .Where(value => value != null)
            .Select(CloneRoute));
        SynchronizeWorldHomes();
    }

    public void Reset()
    {
        factions.Clear();
        routes.Clear();
        currentDay = 1;
        routeSequence = 0;
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (factions.Count > 0)
        {
            return;
        }

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
            DungeonFactionDefinitionSO definition = catalog.Definitions[i];
            int startIndex = candidates.Count > 0
                ? StableIndex(definition.StableId, candidates.Count)
                : 0;
            OffenseHexCoord home = candidates.Count > 0
                ? FindUnusedHome(candidates, occupied, startIndex)
                : new OffenseHexCoord(i + 4, -i);
            occupied.Add(home);
            factions[definition.StableId] = new DungeonFactionState
            {
                factionId = definition.StableId,
                trust = 0,
                discovered = false,
                homeQ = home.Q,
                homeR = home.R
            };
        }

        SynchronizeWorldHomes();
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
            foreach (DungeonFactionState faction in factions.Values.ToArray())
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
            foreach (DungeonFactionState faction in factions.Values)
            {
                DungeonFactionDefinitionSO definition =
                    FindDefinition(faction.factionId);
                world.TryRegisterStrategicSite(new OffenseWorldSiteStateData
                {
                    siteId = HomeSiteId(
                        faction.factionId,
                        faction.betrayalScars),
                    archetypeId = HomeArchetype(definition?.speciesTag),
                    displayName =
                        $"{definition?.displayName ?? faction.factionId} 본거지",
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
                faction.HomeCoord,
                world.DungeonCoord,
                OffenseTravelProfile.Default,
                out IReadOnlyList<OffenseHexCoord> path,
                out _))
        {
            message = "팩션 거점에서 던전까지 도달 가능한 육각 경로가 없습니다.";
            return false;
        }

        routeId = $"faction-route:{++routeSequence}";
        int steps = Mathf.Max(1, path.Count - 1);
        routes.Add(new FactionRouteState
        {
            routeId = routeId,
            factionId = factionId,
            kind = kind,
            status = FactionRouteStatus.Traveling,
            path = path.Select(OffenseHexCoordSaveData.From).ToList(),
            strength = Mathf.Clamp(strength, 1, 100),
            createdDay = currentDay,
            estimatedArrivalDay =
                currentDay + Mathf.CeilToInt(steps * SecondsPerHex / 180f),
            cargo = (cargo ?? Array.Empty<FactionCargoLine>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList()
        });
        message = $"{DisplayName(factionId)} 경로 출발 · ETA Day {routes[^1].estimatedArrivalDay}";
        return true;
    }

    private void CompleteRoute(FactionRouteState route)
    {
        route.status = FactionRouteStatus.Arrived;
        route.segmentProgress = 0f;
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
        route.cargoDelivered = true;
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

        DungeonFactionDefinitionSO definition = FindDefinition(route.factionId);
        CharacterSO template = characterCatalog.Characters
            .Where(value => value != null)
            .Where(value => string.Equals(
                value.SpeciesTag,
                definition?.speciesTag,
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
        route.reinforcementActorIds ??= new List<string>();
        for (int index = 0; index < count; index++)
        {
            GameObject instance = characterFactory.Create(spawner.characterPrefab);
            characterFactory.Inject(instance);
            CharacterActor actor = instance.GetComponent<CharacterActor>();
            if (actor == null)
            {
                characterFactory.Destroy(instance);
                continue;
            }

            string actorId = $"{route.routeId}:ally:{index + 1}";
            instance.name =
                $"{definition?.displayName ?? route.factionId} 지원군 {index + 1}";
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
            worldRegistry.RegisterCharacter(actor);
            worldRegistry.RegisterCharacterLifetime(actor);
            route.reinforcementActorIds.Add(actorId);

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

        route.actorsSpawned = route.reinforcementActorIds.Count > 0;
    }

    private void TryFinishRecovery(DungeonFactionState faction)
    {
        if (currentDay >= faction.negotiationBlockedUntilDay
            && faction.restitutionPaid
            && faction.recoveryEventCompleted)
        {
            faction.trust = 0;
        }
    }

    private DungeonFactionDefinitionSO FindDefinition(string factionId)
    {
        return catalog.Definitions.FirstOrDefault(value =>
            string.Equals(value.StableId, factionId, StringComparison.Ordinal));
    }

    private string DisplayName(string factionId)
    {
        return FindDefinition(factionId)?.displayName ?? factionId;
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
