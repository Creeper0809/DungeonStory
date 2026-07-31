using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class WildlifeRuntime :
    IWildlifeRuntime,
    ITickable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("WildlifeRuntime.Tick");

    private const int InitialWildlifeTargetCount = 7;
    private const float CarcassTickInterval = 2f;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IGameDataProvider gameDataProvider;
    private readonly IWildlifeEcosystemRuntime ecosystemRuntime;
    private readonly ICombatResolutionService combatResolution;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly ICharacterBodyHealthRuntime bodyHealthRuntime;
    private readonly ICombatLineOfSightService lineOfSightService;
    private readonly ICombatCoverQuery coverQuery;
    private readonly ICombatAmmoResupplyRuntime ammoResupplyRuntime;
    private readonly IMainCameraProvider mainCameraProvider;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWildlifeCarcassService carcassService;
    private readonly IGameClock gameClock;
    private readonly IRandomStreamProvider randomStreamProvider;
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;
    private readonly IRandomStream randomStream;
    private readonly List<WildlifeActor> wildlife = new List<WildlifeActor>();
    private readonly Dictionary<string, float> nextBehaviorTickByWildlifeId =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly List<WildlifeFoodRaidOrderSaveData> foodRaidOrders =
        new List<WildlifeFoodRaidOrderSaveData>();
    private WorldItemStackSnapshot[] cachedItemStacks = Array.Empty<WorldItemStackSnapshot>();
    private int cachedItemStackVersion = -1;
    private int nextSequence = 1;
    private bool initialSpawnCompleted;
    private float nextCarcassTickAt;

    public WildlifeRuntime(
        IGridSystemProvider gridSystemProvider,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IGameDataProvider gameDataProvider = null,
        IWildlifeEcosystemRuntime ecosystemRuntime = null,
        ICombatResolutionService combatResolution = null,
        ICombatEquipmentRuntime combatEquipmentRuntime = null,
        ICharacterBodyHealthRuntime bodyHealthRuntime = null,
        ICombatLineOfSightService lineOfSightService = null,
        ICombatCoverQuery coverQuery = null,
        ICombatAmmoResupplyRuntime ammoResupplyRuntime = null,
        IMainCameraProvider mainCameraProvider = null,
        IGridPathSearchBroker pathSearchBroker = null,
        ICharacterAiWorldRegistry worldRegistry = null,
        IWorldItemStackRuntime itemStackRuntime = null,
        IWildlifeCarcassService carcassService = null,
        IGameClock gameClock = null,
        IRandomStreamProvider randomStreamProvider = null,
        IDoorAccessQuery doorAccessQuery = null,
        ICharacterAiPerformanceRecorder performanceRecorder = null)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.speciesCatalog = speciesCatalog ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.gameDataProvider = gameDataProvider;
        this.ecosystemRuntime = ecosystemRuntime;
        this.combatResolution = combatResolution
            ?? new CombatResolutionService(
                new UnityCombatRandomSource(new RandomStreamProvider(1)));
        this.combatEquipmentRuntime = combatEquipmentRuntime;
        this.bodyHealthRuntime = bodyHealthRuntime;
        this.lineOfSightService = lineOfSightService ?? new GridCombatLineOfSightService();
        this.coverQuery = coverQuery ?? new GridCombatCoverQuery();
        this.ammoResupplyRuntime = ammoResupplyRuntime;
        this.mainCameraProvider = mainCameraProvider;
        this.pathSearchBroker = pathSearchBroker;
        this.worldRegistry = worldRegistry;
        this.itemStackRuntime = itemStackRuntime;
        this.carcassService = carcassService;
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.randomStreamProvider = randomStreamProvider ?? new RandomStreamProvider(1);
        this.doorAccessQuery = doorAccessQuery;
        this.performanceRecorder = performanceRecorder;
        randomStream = this.randomStreamProvider.Get("wildlife.runtime");
    }

    public IReadOnlyList<WildlifeActor> Wildlife => wildlife;

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            long started = performanceRecorder?.DetailedCollectionEnabled == true
                ? Stopwatch.GetTimestamp()
                : 0L;
            try
            {
                TickRuntime();
            }
            finally
            {
                if (started != 0L)
                {
                    performanceRecorder.Record(
                        AiPerformanceCategory.Wildlife,
                        (Stopwatch.GetTimestamp() - started)
                            * 1000.0
                            / Stopwatch.Frequency);
                }
            }
        }
    }

    private void TickRuntime()
    {
        if (DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.PauseWildlifeAi))
        {
            return;
        }

        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        if (!initialSpawnCompleted)
        {
            ecosystemRuntime?.EnsureInitialized(grid);
            SpawnInitialWildlife(grid);
            initialSpawnCompleted = true;
        }
        else
        {
            ecosystemRuntime?.EnsureInitialized(grid);
        }

        float now = gameClock.Time;
        Camera mainCamera = mainCameraProvider != null ? mainCameraProvider.Camera : null;
        for (int i = wildlife.Count - 1; i >= 0; i--)
        {
            WildlifeActor actor = wildlife[i];
            if (actor == null || !actor.IsAlive)
            {
                CancelFoodRaidForActor(
                    actor?.WildlifeId,
                    "습격 개체가 처치되어 도난이 취소되었습니다.");
                wildlife.RemoveAt(i);
                if (actor != null)
                {
                    nextBehaviorTickByWildlifeId.Remove(actor.WildlifeId);
                }
                continue;
            }

            if (!IsValidCurrentWildlifePosition(grid, actor))
            {
                if (!TryFindNearestInitialSpawnCell(grid, actor.GridPosition, out Vector2Int safePosition))
                {
                    wildlife.RemoveAt(i);
                    nextBehaviorTickByWildlifeId.Remove(actor.WildlifeId);
                    DestroyWildlifeActor(actor);
                    continue;
                }

                actor.WarpTo(safePosition);
                actor.SetTerritoryCenter(safePosition);
                actor.SetHerdAnchor(safePosition);
            }

            actor.Tick(gameClock.DeltaTime);
            ecosystemRuntime?.TickAnimal(actor, grid, gameClock.DeltaTime);
            if (ecosystemRuntime != null && ecosystemRuntime.ShouldRemoveLeavingAnimal(actor, grid))
            {
                CompleteLeavingFoodRaidForActor(actor.WildlifeId);
                wildlife.RemoveAt(i);
                nextBehaviorTickByWildlifeId.Remove(actor.WildlifeId);
                DestroyWildlifeActor(actor);
                continue;
            }

            if (!ShouldTickBehavior(actor, now, mainCamera))
            {
                continue;
            }

            TryResolvePredatorWildlifeContact(actor);
            if (TickFoodRaid(actor, grid, now))
            {
                continue;
            }

            TickBehavior(actor, grid, now);
        }

        TryRespawnWildlife(grid, now);

        if (now >= nextCarcassTickAt)
        {
            nextCarcassTickAt = now + CarcassTickInterval;
            carcassService?.TickFreshness(CarcassTickInterval);
        }
    }

    public DungeonWildlifeSaveData Capture()
    {
        return new DungeonWildlifeSaveData
        {
            version = DungeonWildlifeSaveData.CurrentVersion,
            nextSequence = Mathf.Max(1, nextSequence),
            wildlife = wildlife
                .Where(actor => actor != null && actor.IsAlive)
                .Select(actor => actor.Capture())
                .ToList(),
            carcasses = carcassService?.CaptureFreshness().ToList()
                ?? new List<WildlifeCarcassFreshnessSaveData>(),
            ecosystem = ecosystemRuntime?.Capture() ?? new DungeonWildlifeEcosystemSaveData(),
            foodRaidOrders = foodRaidOrders
                .Select(CloneFoodRaidOrder)
                .ToList()
        };
    }

    public void Restore(DungeonWildlifeSaveData saveData, DungeonGameRestoreReport report = null)
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            report?.AddWarning("Wildlife runtime could not restore because the grid is not ready.");
            return;
        }

        ClearWildlife();
        DungeonWildlifeSaveData source = saveData ?? new DungeonWildlifeSaveData();
        ecosystemRuntime?.Restore(source.ecosystem ?? new DungeonWildlifeEcosystemSaveData());
        nextSequence = Mathf.Max(1, source.nextSequence);
        foreach (WildlifeSaveData entry in source.wildlife ?? Enumerable.Empty<WildlifeSaveData>())
        {
            if (entry == null
                || !speciesCatalog.TryGetSpecies(entry.speciesId, out WildlifeSpeciesDefinition species))
            {
                continue;
            }

            Vector2Int position = new Vector2Int(entry.gridX, entry.gridY);
            if (!CanSpawnAt(grid, position, species.CanEnterDungeon))
            {
                report?.AddWarning($"Wildlife {entry.wildlifeId} had an invalid saved position and was skipped.");
                continue;
            }

            SpawnActor(grid, species, position, entry.wildlifeId, entry);
        }

        carcassService?.RestoreFreshness(source.carcasses);
        foodRaidOrders.Clear();
        foreach (WildlifeFoodRaidOrderSaveData order in
                 source.foodRaidOrders
                 ?? Enumerable.Empty<WildlifeFoodRaidOrderSaveData>())
        {
            if (order == null || string.IsNullOrWhiteSpace(order.wildlifeId))
            {
                continue;
            }

            WildlifeFoodRaidOrderSaveData restored = CloneFoodRaidOrder(order);
            if (restored.state == WildlifeFoodRaidOrderState.Approaching
                && wildlife.All(actor => actor == null
                    || !string.Equals(
                        actor.WildlifeId,
                        restored.wildlifeId,
                        StringComparison.Ordinal)))
            {
                restored.state = WildlifeFoodRaidOrderState.Cancelled;
                restored.outcomeReason =
                    "저장 복원 시 습격 개체가 없어 도난이 취소되었습니다.";
            }

            foodRaidOrders.Add(restored);
        }

        initialSpawnCompleted = true;
    }

    public bool DebugSpawn(
        string speciesId,
        int amount,
        Vector2Int position,
        out int spawned,
        out string message)
    {
        spawned = 0;
        message = string.Empty;
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            message = "그리드가 준비되지 않았습니다.";
            return false;
        }

        if (!speciesCatalog.TryGetSpecies(speciesId, out WildlifeSpeciesDefinition species))
        {
            message = "야생동물 종을 찾을 수 없습니다.";
            return false;
        }

        int requested = Mathf.Clamp(amount, 1, 50);
        Vector2Int anchor = position;
        for (int index = 0; index < requested; index++)
        {
            Vector2Int candidate = index == 0
                ? anchor
                : FindNearbySpawnPosition(grid, anchor);
            if (!CanInitialSpawnAt(grid, candidate))
            {
                continue;
            }

            SpawnActor(grid, species, candidate, NextWildlifeId(), null);
            spawned++;
        }

        message = spawned > 0
            ? $"{species.DisplayName} {spawned}마리를 소환했습니다."
            : "선택 칸 주변에 유효한 외부 스폰 칸이 없습니다.";
        return spawned > 0;
    }

    public bool TrySpawnArrival(
        string speciesId,
        Vector2Int position,
        out WildlifeActor actor,
        out string message)
    {
        actor = null;
        message = string.Empty;
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            message = "그리드가 준비되지 않았습니다.";
            return false;
        }

        if (!speciesCatalog.TryGetSpecies(speciesId, out WildlifeSpeciesDefinition species))
        {
            message = "귀환시킬 야생동물 종을 찾지 못했습니다.";
            return false;
        }

        Vector2Int spawnPosition = position;
        if (!CanInitialSpawnAt(grid, spawnPosition))
        {
            bool found = false;
            for (int radius = 1; radius <= 6 && !found; radius++)
            {
                for (int y = -radius; y <= radius && !found; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                        {
                            continue;
                        }

                        Vector2Int candidate = position + new Vector2Int(x, y);
                        if (!CanInitialSpawnAt(grid, candidate))
                        {
                            continue;
                        }

                        spawnPosition = candidate;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                message = "하차장 주변에 동물을 내릴 수 있는 외부 칸이 없습니다.";
                return false;
            }
        }

        actor = SpawnActor(grid, species, spawnPosition, NextWildlifeId(), null);
        actor.SetIntent(WildlifeIntent.Rest, "원정대가 운반 상자에서 내려놓았습니다.");
        message = $"{species.DisplayName}이 하차장에 도착했습니다.";
        return true;
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetReachableFoodRaidTargets()
    {
        if (itemStackRuntime == null
            || !gridSystemProvider.TryGetGrid(out Grid grid)
            || !TryFindFoodRaidEntry(grid, out Vector2Int entry))
        {
            return Array.Empty<WorldItemStackSnapshot>();
        }

        return itemStackRuntime.GetAllStacks()
            .Where(stack => IsLooseRaidFood(stack)
                && IsReachableFoodRaidTarget(grid, entry, stack.Position))
            .OrderBy(stack => Manhattan(entry, stack.Position))
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<WildlifeFoodRaidOrderSnapshot> GetFoodRaidOrders()
    {
        return foodRaidOrders.Select(ToFoodRaidSnapshot).ToArray();
    }

    public bool TryBeginFoodRaid(
        string raidId,
        int wolfCount,
        out IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders,
        out string failureReason)
    {
        orders = Array.Empty<WildlifeFoodRaidOrderSnapshot>();
        string normalizedRaidId = raidId?.Trim() ?? string.Empty;
        if (normalizedRaidId.Length == 0)
        {
            failureReason = "습격 ID가 필요합니다.";
            return false;
        }

        if (foodRaidOrders.Any(order =>
                order != null
                && !IsFoodRaidTerminal(order.state)))
        {
            failureReason = "이미 진행 중인 식량 습격이 있습니다.";
            return false;
        }

        IReadOnlyList<WorldItemStackSnapshot> targets =
            GetReachableFoodRaidTargets();
        if (!gridSystemProvider.TryGetGrid(out Grid grid)
            || !TryFindFoodRaidEntry(grid, out Vector2Int entry))
        {
            failureReason = "외부 진입로에 유효한 늑대 출현 지점이 없습니다.";
            return false;
        }

        foodRaidOrders.Clear();
        int requested = Mathf.Max(1, wolfCount);
        string lastSpawnFailure = string.Empty;
        for (int index = 0; index < requested; index++)
        {
            if (!TrySpawnArrival(
                    "shadow_wolf",
                    entry,
                    out WildlifeActor wolf,
                    out lastSpawnFailure))
            {
                continue;
            }

            WorldItemStackSnapshot target = targets.Count > 0
                ? targets[index % targets.Count]
                : null;
            foodRaidOrders.Add(new WildlifeFoodRaidOrderSaveData
            {
                raidId = normalizedRaidId,
                wildlifeId = wolf.WildlifeId,
                targetStackId = target?.StackId ?? string.Empty,
                state = WildlifeFoodRaidOrderState.Approaching,
                stolenQuantity = 0,
                outcomeReason = string.Empty
            });
            wolf.SetIntent(
                WildlifeIntent.Forage,
                target != null
                    ? $"노출 식량 {target.DisplayName}을 노리는 중"
                    : "노출 식량을 찾는 중");
        }

        orders = GetFoodRaidOrders();
        if (orders.Count == 0)
        {
            failureReason = string.IsNullOrWhiteSpace(lastSpawnFailure)
                ? "습격 늑대를 출현시키지 못했습니다."
                : lastSpawnFailure;
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public bool TrySpawnDomesticBirth(
        string speciesId,
        Vector2Int position,
        out WildlifeActor actor,
        out string message)
    {
        actor = null;
        message = string.Empty;
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            message = "그리드가 준비되지 않았습니다.";
            return false;
        }

        if (!speciesCatalog.TryGetSpecies(
                speciesId,
                out WildlifeSpeciesDefinition species))
        {
            message = "태어날 동물 종을 찾을 수 없습니다.";
            return false;
        }

        Vector2Int spawnPosition = position;
        bool found = false;
        for (int radius = 0; radius <= 3 && !found; radius++)
        {
            for (int y = -radius; y <= radius && !found; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (radius > 0
                        && Mathf.Abs(x) != radius
                        && Mathf.Abs(y) != radius)
                    {
                        continue;
                    }

                    Vector2Int candidate = position + new Vector2Int(x, y);
                    GridCell cell = grid.GetGridCell(candidate);
                    if (cell == null
                        || !grid.IsWalkable(candidate)
                        || cell.HasOccupantInLayer(GridLayer.Wildlife)
                        || cell.AreaType == GridCellAreaType.BlockedExterior)
                    {
                        continue;
                    }

                    spawnPosition = candidate;
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            message = "우리 안에 새끼가 태어날 빈 칸이 없습니다.";
            return false;
        }

        actor = SpawnActor(
            grid,
            species,
            spawnPosition,
            NextWildlifeId(),
            null);
        actor.SetIntent(WildlifeIntent.Rest, "우리에서 막 태어남");
        message = $"{species.DisplayName} 새끼가 태어났습니다.";
        return true;
    }

    public bool DebugDelete(string wildlifeId)
    {
        return TryRemoveArrival(wildlifeId);
    }

    public bool TryRemoveArrival(string wildlifeId)
    {
        WildlifeActor actor = wildlife.FirstOrDefault(candidate => candidate != null
            && string.Equals(candidate.WildlifeId, wildlifeId, StringComparison.Ordinal));
        if (actor == null)
        {
            return false;
        }

        CancelFoodRaidForActor(
            actor.WildlifeId,
            "습격 개체가 제거되어 도난이 취소되었습니다.");
        wildlife.Remove(actor);
        nextBehaviorTickByWildlifeId.Remove(actor.WildlifeId);
        DestroyWildlifeActor(actor);
        return true;
    }

    public int DebugDeleteAll()
    {
        int count = wildlife.Count(actor => actor != null);
        ClearWildlife();
        return count;
    }

    public bool HasAvailableHuntJob(CharacterActor actor)
    {
        return TryFindBestHuntTarget(actor, out _);
    }

    public bool TryReserveBestHuntJob(
        CharacterActor actor,
        out WildlifeHuntJob job,
        out string reason)
    {
        job = default;
        reason = string.Empty;
        if (actor == null)
        {
            reason = "사냥할 직원이 없습니다.";
            return false;
        }

        if (!TryFindBestHuntTarget(actor, out WildlifeActor target))
        {
            reason = "지정된 사냥감이 없습니다.";
            return false;
        }

        if (!target.TryReserve(actor))
        {
            reason = "이미 다른 사냥꾼이 추적 중입니다.";
            return false;
        }

        job = new WildlifeHuntJob(target);
        return true;
    }

    public void ReleaseHuntReservation(string wildlifeId, CharacterActor actor)
    {
        if (TryGetWildlife(wildlifeId, out WildlifeActor target))
        {
            target.ReleaseReservation(actor);
        }
    }

    public bool DesignateHunt(string wildlifeId, bool designated, bool priority = false)
    {
        if (!TryGetWildlife(wildlifeId, out WildlifeActor target))
        {
            return false;
        }

        target.SetHuntDesignation(designated, priority);
        return true;
    }

    public bool ApplyHuntHit(CharacterActor hunter, string wildlifeId, out string message)
    {
        return ApplyHuntHitWithCombatCore(hunter, wildlifeId, out message);
    }

    public bool CanAttackHuntTargetFrom(
        CharacterActor hunter,
        WildlifeActor target,
        Grid grid,
        Vector2Int attackerCell)
    {
        if (hunter == null || target == null || !target.IsAlive || grid == null)
        {
            return false;
        }

        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        CombatWeaponSnapshot weapon = CombatWeaponSnapshot.CreateUnarmed();
        if (equipment != null)
        {
            equipment.TryGetActiveWeapon(GetCharacterId(hunter), out weapon);
        }
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();

        int distance = Manhattan(attackerCell, target.GridPosition);
        if (!weapon.IsRanged)
        {
            return attackerCell.y == target.GridPosition.y
                && Mathf.Abs(attackerCell.x - target.GridPosition.x) == 1;
        }

        if (distance <= 0 || distance > weapon.MaximumRange)
        {
            return false;
        }

        CombatRangeBand band = CombatRangeRules.GetBand(distance);
        if (weapon.GetAccuracyMultiplier(band) <= 0f
            || weapon.GetDamageMultiplier(band) <= 0f)
        {
            return false;
        }

        CombatLineOfSightResult sight = lineOfSightService.Evaluate(
            grid,
            attackerCell,
            target.GridPosition,
            GetCharacterId(hunter),
            "wildlife:" + target.WildlifeId);
        return sight.HasLineOfSight && !sight.FriendlyFireRisk;
    }

    public bool NeedsHuntReload(CharacterActor hunter)
    {
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        return hunter != null
            && equipment != null
            && equipment.TryGetActiveWeapon(GetCharacterId(hunter), out CombatWeaponSnapshot weapon)
            && weapon != null
            && weapon.RequiresAmmo
            && weapon.LoadedAmmo <= 0;
    }

    public float GetHuntReloadDuration(CharacterActor hunter)
    {
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        if (hunter == null
            || equipment == null
            || !equipment.TryGetActiveWeapon(GetCharacterId(hunter), out CombatWeaponSnapshot weapon)
            || weapon == null)
        {
            return 0f;
        }

        CharacterBodyHealthSnapshot body =
            bodyHealthRuntime?.GetSnapshot(hunter)
            ?? CreateHealthyBodySnapshot();
        return combatResolution.CalculateReloadTime(
            CreateHunterCombatStats(hunter, body),
            weapon);
    }

    public bool TryReloadHuntWeapon(CharacterActor hunter, out string message)
    {
        message = string.Empty;
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        if (hunter == null
            || equipment == null
            || !equipment.TryGetActiveWeapon(GetCharacterId(hunter), out CombatWeaponSnapshot weapon)
            || weapon == null
            || !weapon.RequiresAmmo)
        {
            return true;
        }

        if (weapon.LoadedAmmo > 0)
        {
            return true;
        }

        if (!equipment.TryReloadFromCharacterInventory(
                GetCharacterId(hunter),
                weapon.InstanceId,
                out int consumed)
            || consumed <= 0)
        {
            if (ammoResupplyRuntime?.TryRequestAmmoResupply(hunter, out string resupplyMessage)
                == true)
            {
                message = string.IsNullOrWhiteSpace(resupplyMessage)
                    ? "창고 탄약 재보급을 시작합니다."
                    : resupplyMessage;
                return false;
            }

            message = $"{weapon.AmmunitionItemId} 탄약이 없습니다.";
            return false;
        }

        message = $"{consumed}발 장전";
        return true;
    }

    public float GetHuntAttackInterval(CharacterActor hunter)
    {
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        CombatWeaponSnapshot weapon = CombatWeaponSnapshot.CreateUnarmed();
        CharacterCombatLoadoutProfile profile = null;
        if (hunter != null && equipment != null)
        {
            string hunterId = GetCharacterId(hunter);
            equipment.TryGetActiveWeapon(hunterId, out weapon);
            profile = equipment.GetActiveProfileSnapshot(hunterId);
        }
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();

        CharacterBodyHealthSnapshot body =
            bodyHealthRuntime?.GetSnapshot(hunter)
            ?? CreateHealthyBodySnapshot();
        return combatResolution.CalculateAttackInterval(
            CreateHunterCombatStats(hunter, body),
            weapon,
            ResolveSupportedFireMode(weapon, profile?.fireMode ?? CombatFireMode.Aimed));
    }

    private bool ApplyHuntHitWithCombatCore(
        CharacterActor hunter,
        string wildlifeId,
        out string message)
    {
        message = string.Empty;
        if (hunter == null
            || !TryGetWildlife(wildlifeId, out WildlifeActor target)
            || !target.IsAlive)
        {
            message = "사냥 대상이 사라졌습니다.";
            return false;
        }

        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            message = "전투 격자를 찾지 못했습니다.";
            return false;
        }

        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        ICharacterBodyHealthRuntime health = bodyHealthRuntime;
        string hunterId = GetCharacterId(hunter);
        CombatWeaponSnapshot weapon = CombatWeaponSnapshot.CreateUnarmed();
        if (equipment != null)
        {
            equipment.TryGetActiveWeapon(hunterId, out weapon);
        }
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();

        CharacterCombatLoadoutProfile profile = equipment?.GetActiveProfileSnapshot(hunterId);
        if (weapon.IsRanged && profile?.holdFire == true)
        {
            message = "사격 중지 상태입니다.";
            return false;
        }

        int distance = Manhattan(hunter.GetNowXY(), target.GridPosition);
        if (!weapon.IsRanged
            && (hunter.GetNowXY().y != target.GridPosition.y
                || Mathf.Abs(hunter.GetNowXY().x - target.GridPosition.x) != 1))
        {
            message = "근접 공격은 같은 층의 바로 옆 칸에서만 가능합니다.";
            return false;
        }

        CombatLineOfSightResult sight = weapon.IsRanged
            ? lineOfSightService.Evaluate(
                grid,
                hunter.GetNowXY(),
                target.GridPosition,
                hunterId,
                "wildlife:" + target.WildlifeId)
            : new CombatLineOfSightResult(
                true,
                false,
                default,
                Array.Empty<Vector2Int>(),
                string.Empty);
        CombatFireMode fireMode = ResolveSupportedFireMode(
            weapon,
            profile?.fireMode ?? CombatFireMode.Aimed);
        CharacterBodyHealthSnapshot hunterBody = health?.GetSnapshot(hunter)
            ?? CreateHealthyBodySnapshot();
        CombatAttackResult result = combatResolution.Resolve(new CombatAttackRequest(
            $"hunt:{hunterId}:{target.WildlifeId}:{gameClock.FrameCount}",
            hunterId,
            "wildlife:" + target.WildlifeId,
            CreateHunterCombatStats(hunter, hunterBody),
            CreateWildlifeCombatStats(target),
            weapon,
            distance,
            fireMode,
            weapon.IsRanged
                ? coverQuery.GetCover(grid, hunter.GetNowXY(), target.GridPosition)
                : default,
            hasLineOfSight: sight.HasLineOfSight,
            friendlyFireRisk: sight.FriendlyFireRisk,
            defenderMeleeLocked: distance <= 1,
            attackerSuppression: hunterBody.Suppression,
            attackPowerMultiplier: hunter.GetCombatPowerMultiplier()));
        if (!result.Executed)
        {
            message = ResolveHuntFailureMessage(weapon, distance, sight);
            return false;
        }

        PresentHuntAttack(hunter, target, weapon);
        ConsumeHuntWeapon(equipment, weapon, target.GridPosition);
        if (result.CoverBlocked)
        {
            CombatCoverDurability.TryApplyDamage(result.CoverSourceId, result.CoverDamage);
        }

        target.RegisterThreat(hunter.GetNowXY(), result.Hit ? 0.75f : 0.35f);
        target.SetHuntDesignation(true, target.PriorityHunt);
        int applied = result.Hit ? target.ApplyCombatDamage(result, hunter) : 0;
        bool killed = !target.IsAlive;
        hunter.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            killed ? CharacterActivityOutcomes.Completed : CharacterActivityOutcomes.Progress,
            killed
                ? $"{GetCharacterDisplayName(hunter)}이(가) {target.DisplayName} 사냥을 끝냈다."
                : result.Hit
                    ? $"{GetCharacterDisplayName(hunter)}이(가) {target.DisplayName}의 {GetBodyPartName(result.BodyPart)}에 {applied} 피해를 입혔다."
                    : result.CoverBlocked
                        ? $"{GetCharacterDisplayName(hunter)}의 공격이 엄폐물에 막혔다."
                        : $"{GetCharacterDisplayName(hunter)}의 공격을 {target.DisplayName}이(가) 피했다.",
            actionId: "survival/hunt",
            targetId: "wildlife:" + target.WildlifeId,
            targetName: target.DisplayName,
            value: applied,
            sentiment: killed ? 0.45f : result.Hit ? 0.1f : -0.1f,
            bubbleEligible: true));

        if (target.RetaliationDamage > 0
            && !killed
            && target.Aggression > 0.45f
            && distance <= 1)
        {
            ApplyWildlifeRetaliation(target, hunter, equipment, health);
        }

        if (killed)
        {
            CancelFoodRaidForActor(
                target.WildlifeId,
                "습격 늑대가 처치되어 도난이 취소되었습니다.");
            ecosystemRuntime?.NotifyWildlifeKilled(target, byHunt: true);
            hunter.Progression?.AddExperience(target.IsDangerous ? 20 : 10);
            RecordHuntNarrative(hunter, target);
            carcassService?.SpawnCarcass(target);
            wildlife.Remove(target);
            if (target != null)
            {
                DestroyWildlifeActor(target);
            }
        }

        message = killed
            ? "사냥감 처치"
            : result.Hit
                ? $"{GetBodyPartName(result.BodyPart)} 명중"
                : result.CoverBlocked
                    ? "엄폐물에 막힘"
                    : result.Evaded
                        ? "사냥감이 회피"
                        : "빗나감";
        return true;
    }

    private void ApplyWildlifeRetaliation(
        WildlifeActor wildlifeActor,
        CharacterActor hunter,
        ICombatEquipmentRuntime equipment,
        ICharacterBodyHealthRuntime health)
    {
        if (wildlifeActor == null || hunter == null || hunter.IsDead)
        {
            return;
        }

        string hunterId = GetCharacterId(hunter);
        CharacterBodyHealthSnapshot hunterBody = health?.GetSnapshot(hunter)
            ?? CreateHealthyBodySnapshot();
        CombatWeaponSnapshot naturalWeapon = CreateWildlifeNaturalWeapon(wildlifeActor);
        CombatAttackResult retaliation = combatResolution.Resolve(new CombatAttackRequest(
            $"wildlife-retaliation:{wildlifeActor.WildlifeId}:{hunterId}:{gameClock.FrameCount}",
            "wildlife:" + wildlifeActor.WildlifeId,
            hunterId,
            CreateWildlifeCombatStats(wildlifeActor),
            CreateHunterCombatStats(hunter, hunterBody),
            naturalWeapon,
            1,
            CombatFireMode.Aimed,
            default,
            defenderDowned: hunterBody.Downed,
            defenderMeleeLocked: true,
            defenderSuppression: hunterBody.Suppression,
            defenderArmor: equipment?.GetArmor(hunterId),
            defenderShield: equipment?.GetShield(hunterId) ?? default));
        if (!retaliation.Executed)
        {
            return;
        }

        DefenseCombatPresentation.Ensure(hunter)?.PlayHit(retaliation.AppliedDamage);
        if (retaliation.Hit)
        {
            if (health != null)
            {
                health.ApplyCombatResult(
                    hunter,
                    retaliation,
                    $"{wildlifeActor.DisplayName}의 반격");
            }
            else
            {
                hunter.ApplyDamage(retaliation.AppliedDamage, wildlifeActor.DisplayName + "의 반격");
            }

            ApplyArmorDurabilityDamage(equipment, retaliation);
            hunter.ApplyMoodFactor(
                "survival:hunt:retaliation",
                $"{wildlifeActor.DisplayName}에게 반격당함",
                -4f,
                180f,
                1);
        }
        else if (health != null)
        {
            health.AddSuppression(hunter, retaliation.Suppression);
        }
    }

    private static CombatWeaponSnapshot CreateWildlifeNaturalWeapon(WildlifeActor actor)
    {
        float baseDamage = Mathf.Max(2f, actor?.RetaliationDamage ?? 2);
        return new CombatWeaponSnapshot(
            "combat:wildlife-natural",
            string.Empty,
            CombatEquipmentKind.MeleeWeapon,
            new MeleeStrikeVerb
            {
                attackTime = 1.05f,
                baseDamage = baseDamage,
                penetration = Mathf.Max(0f, baseDamage * 0.2f),
                damageType = CombatDamageType.Pierce,
                tracking = 0.08f
            },
            new[]
            {
                new CombatRangeProfile
                {
                    band = CombatRangeBand.Contact,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                }
            },
            1,
            CombatEquipmentQuality.Normal,
            string.Empty,
            0,
            0,
            0f,
            false,
            false,
            false);
    }

    private static CombatStatSnapshot CreateHunterCombatStats(
        CharacterActor hunter,
        CharacterBodyHealthSnapshot body)
    {
        if (hunter == null)
        {
            return default;
        }

        float healthRatio = Mathf.Clamp01(hunter.CurrentHealth / Mathf.Max(1f, hunter.MaxHealth));
        float bodyEfficiency = Mathf.Min(
            body.Consciousness,
            Mathf.Lerp(0.5f, 1f, body.Manipulation));
        return new CombatStatSnapshot(
            hunter.GetCharacterStat(CharacterStatType.Attack),
            hunter.GetCharacterStat(CharacterStatType.Shooting),
            hunter.GetCharacterStat(CharacterStatType.Evasion),
            hunter.GetCharacterStat(CharacterStatType.MoveSpeed) * body.Mobility,
            hunter.GetCharacterStat(CharacterStatType.Strength),
            hunter.GetCharacterStat(CharacterStatType.Toughness),
            hunter.GetCharacterStat(CharacterStatType.Dexterity) * body.Manipulation,
            healthRatio * bodyEfficiency);
    }

    private static CombatStatSnapshot CreateWildlifeCombatStats(WildlifeActor actor)
    {
        if (actor == null)
        {
            return default;
        }

        float speed = Mathf.Max(0.5f, actor.Species?.MoveSpeed ?? 1f);
        float mobility = actor.CombatMobility;
        float health = Mathf.Clamp01(actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
        return new CombatStatSnapshot(
            melee: Mathf.Clamp(3f + actor.RetaliationDamage * 0.45f, 2f, 14f),
            shooting: 0f,
            evasion: Mathf.Clamp(2f + speed * 3f, 2f, 14f) * mobility,
            moveSpeed: Mathf.Clamp(3f + speed * 3f, 3f, 14f) * mobility,
            strength: Mathf.Clamp(2f + actor.RetaliationDamage * 0.5f, 2f, 15f),
            toughness: Mathf.Clamp(actor.MaxHealth * 0.12f, 1f, 16f),
            dexterity: Mathf.Clamp(2f + speed * 2.5f, 2f, 14f) * mobility,
            healthMultiplier: health);
    }

    private static CharacterBodyHealthSnapshot CreateHealthyBodySnapshot()
    {
        return new CharacterBodyHealthSnapshot(
            Array.Empty<CharacterBodyPartHealthState>(),
            0f,
            0f,
            1f,
            1f,
            1f,
            false);
    }

    private static CombatFireMode ResolveSupportedFireMode(
        CombatWeaponSnapshot weapon,
        CombatFireMode requested)
    {
        if (weapon == null)
        {
            return CombatFireMode.Aimed;
        }

        return requested switch
        {
            CombatFireMode.Rapid when weapon.SupportsRapid => CombatFireMode.Rapid,
            CombatFireMode.Suppressive when weapon.SupportsSuppressive => CombatFireMode.Suppressive,
            _ => CombatFireMode.Aimed
        };
    }

    private static string ResolveHuntFailureMessage(
        CombatWeaponSnapshot weapon,
        int distance,
        CombatLineOfSightResult sight)
    {
        if (weapon == null)
        {
            return "사용할 무기가 없습니다.";
        }

        if (distance > weapon.MaximumRange || (!weapon.IsRanged && distance > 1))
        {
            return "무기 사거리 밖입니다.";
        }

        if (weapon.IsRanged && !sight.HasLineOfSight)
        {
            return "사선이 막혔습니다.";
        }

        if (weapon.IsRanged && sight.FriendlyFireRisk)
        {
            return "아군이 사선에 있어 사격을 보류합니다.";
        }

        if (weapon.RequiresAmmo && weapon.LoadedAmmo <= 0)
        {
            return "장전된 탄약이 없습니다.";
        }

        return "공격할 수 없습니다.";
    }

    private void PresentHuntAttack(
        CharacterActor hunter,
        WildlifeActor target,
        CombatWeaponSnapshot weapon)
    {
        if (hunter == null || target == null)
        {
            return;
        }

        DefenseCombatPresentation.Ensure(hunter)?.PlayAttack(target.transform.position);
        if (!weapon.IsRanged)
        {
            return;
        }

        float projectileSpeed = weapon.Verb switch
        {
            ProjectileVerb projectile => projectile.projectileSpeed,
            RecoverableThrowVerb recoverable => recoverable.projectileSpeed,
            _ => 12f
        };
        CombatProjectilePresentation.Launch(
            hunter.transform.position,
            target.transform.position,
            projectileSpeed,
            weapon.Verb?.damageType ?? CombatDamageType.Pierce,
            arcing: false,
            gameClock: gameClock);
    }

    private void ConsumeHuntWeapon(
        ICombatEquipmentRuntime equipment,
        CombatWeaponSnapshot weapon,
        Vector2Int impactPosition)
    {
        if (equipment == null || weapon == null)
        {
            return;
        }

        if (weapon.RequiresAmmo && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            equipment.TryConsumeLoadedAmmo(weapon.InstanceId);
            return;
        }

        if (weapon.Verb?.DropsWeaponOnUse != true
            || string.IsNullOrWhiteSpace(weapon.InstanceId)
            || string.IsNullOrWhiteSpace(weapon.DefinitionId)
            || itemStackRuntime == null
            || !itemStackRuntime.SpawnUniqueItemAt(
                DungeonItemCatalogSO.EquipmentItemId(weapon.DefinitionId),
                impactPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out string stackId))
        {
            return;
        }

        equipment.TryLinkToWorldStack(
            weapon.InstanceId,
            stackId,
            CombatEquipmentWorldState.Loose);
    }

    private static void ApplyArmorDurabilityDamage(
        ICombatEquipmentRuntime equipment,
        CombatAttackResult result)
    {
        if (equipment == null)
        {
            return;
        }

        if (result.ArmorDurabilityHits.Count > 0)
        {
            for (int i = 0; i < result.ArmorDurabilityHits.Count; i++)
            {
                CombatArmorDurabilityHit hit = result.ArmorDurabilityHits[i];
                equipment.TryApplyDurabilityDamage(hit.InstanceId, hit.Damage);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ArmorInstanceId))
        {
            equipment.TryApplyDurabilityDamage(
                result.ArmorInstanceId,
                result.ArmorDurabilityDamage);
        }
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        string persistentId = actor?.Identity?.PersistentId;
        return !string.IsNullOrWhiteSpace(persistentId)
            ? persistentId
            : $"scene-actor:{actor?.GetInstanceID() ?? 0}";
    }

    private static string GetCharacterDisplayName(CharacterActor actor)
    {
        string displayName = actor?.Identity?.DisplayName;
        return !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : actor != null ? actor.name : "사냥꾼";
    }

    private static string GetBodyPartName(CombatBodyPart bodyPart)
    {
        return bodyPart switch
        {
            CombatBodyPart.Head => "머리",
            CombatBodyPart.Torso => "몸통",
            CombatBodyPart.LeftArm => "왼앞다리",
            CombatBodyPart.RightArm => "오른앞다리",
            CombatBodyPart.LeftLeg => "왼뒷다리",
            CombatBodyPart.RightLeg => "오른뒷다리",
            _ => "몸"
        };
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    public bool TryButcherNextCarcass(
        CharacterActor butcher,
        BuildableObject building,
        out int produced,
        out string message)
    {
        if (carcassService != null)
        {
            return carcassService.TryButcherNextCarcass(
                butcher,
                building,
                out produced,
                out message);
        }

        produced = 0;
        message = "사체 서비스가 준비되지 않았습니다.";
        return false;
    }

    public bool HasButcherWorkAvailable(BuildableObject building)
    {
        return carcassService?.HasButcherWorkAvailable(building) == true;
    }

    public float GetButcherWorkUrgency()
    {
        return carcassService?.GetButcherWorkUrgency() ?? 0f;
    }

    private void SpawnInitialWildlife(Grid grid)
    {
        if (wildlife.Count >= InitialWildlifeTargetCount)
        {
            return;
        }

        List<Vector2Int> candidates = GetSpawnCandidates(grid).ToList();
        int attempts = 0;
        while (wildlife.Count < InitialWildlifeTargetCount && candidates.Count > 0 && attempts < 80)
        {
            attempts++;
            WildlifeSpeciesDefinition species =
                speciesCatalog.GetRandomSpecies(randomStream);
            int herdCount = Mathf.Clamp(species.HerdSize, 1, InitialWildlifeTargetCount - wildlife.Count);
            Vector2Int anchor = candidates[randomStream.NextInt(0, candidates.Count)];
            for (int i = 0; i < herdCount && wildlife.Count < InitialWildlifeTargetCount; i++)
            {
                Vector2Int position = i == 0
                    ? anchor
                    : FindNearbySpawnPosition(grid, anchor);
                if (!CanInitialSpawnAt(grid, position))
                {
                    continue;
                }

                SpawnActor(grid, species, position, NextWildlifeId(), null);
            }
        }
    }

    private void TryRespawnWildlife(Grid grid, float now)
    {
        if (ecosystemRuntime == null
            || wildlife.Count >= InitialWildlifeTargetCount + 6
            || !ecosystemRuntime.TryConsumeRespawnOpportunity(
                now,
                wildlife.Count(actor => actor != null && actor.IsAlive),
                speciesCatalog.All,
                out WildlifeSpeciesDefinition species))
        {
            return;
        }

        List<Vector2Int> candidates = GetSpawnCandidates(grid).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2Int position = candidates[randomStream.NextInt(0, candidates.Count)];
            if (!CanInitialSpawnAt(grid, position))
            {
                continue;
            }

            WildlifeActor actor = SpawnActor(grid, species, position, NextWildlifeId(), null);
            actor.SetIntent(WildlifeIntent.ReturnToTerritory, "서식지가 회복되어 돌아옴");
            return;
        }
    }

    private WildlifeActor SpawnActor(
        Grid grid,
        WildlifeSpeciesDefinition species,
        Vector2Int position,
        string wildlifeId,
        WildlifeSaveData saveData)
    {
        GameObject gameObject = new GameObject("Wildlife");
        DungeonRuntimeHierarchy.Parent(gameObject, DungeonRuntimeHierarchy.Wildlife);
        WildlifeActor actor = gameObject.AddComponent<WildlifeActor>();
        if (pathSearchBroker != null && worldRegistry != null)
        {
            actor.ConfigureRuntimeServices(
                pathSearchBroker,
                worldRegistry,
                gameClock,
                randomStreamProvider,
                doorAccessQuery);
        }
        actor.Initialize(grid, species, wildlifeId, position, saveData);
        wildlife.Add(actor);
        return actor;
    }

    private void ClearWildlife()
    {
        for (int i = wildlife.Count - 1; i >= 0; i--)
        {
            WildlifeActor actor = wildlife[i];
            if (actor != null)
            {
                CancelFoodRaidForActor(
                    actor.WildlifeId,
                    "습격 개체가 제거되어 도난이 취소되었습니다.");
                DestroyWildlifeActor(actor);
            }
        }

        wildlife.Clear();
        nextBehaviorTickByWildlifeId.Clear();
    }

    private static void DestroyWildlifeActor(WildlifeActor actor)
    {
        if (actor == null)
        {
            return;
        }

        actor.PrepareForDespawn();
        UnityEngine.Object.Destroy(actor.gameObject);
    }

    private bool ShouldTickBehavior(WildlifeActor actor, float now, Camera mainCamera)
    {
        if (actor == null || !actor.IsAlive)
        {
            return false;
        }

        if (actor.State == WildlifeState.Captured)
        {
            return false;
        }

        string id = actor.WildlifeId;
        if (string.IsNullOrWhiteSpace(id))
        {
            return true;
        }

        bool urgent = actor.HuntDesignated
            || actor.State == WildlifeState.Hunted
            || actor.State == WildlifeState.Fleeing
            || actor.State == WildlifeState.Retaliating
            || actor.State == WildlifeState.PredatorStalking
            || actor.IsDangerous;
        bool visible = IsVisible(mainCamera, actor);
        float interval = urgent
            ? 0.25f
            : visible ? 0.75f : NextRange(1.5f, 3.5f);

        if (nextBehaviorTickByWildlifeId.TryGetValue(id, out float nextTickAt)
            && now < nextTickAt)
        {
            return false;
        }

        nextBehaviorTickByWildlifeId[id] = now + interval;
        return true;
    }

    private static bool IsVisible(Camera camera, WildlifeActor actor)
    {
        if (camera == null || actor == null)
        {
            return true;
        }

        Vector3 viewport = camera.WorldToViewportPoint(actor.transform.position);
        return viewport.z > 0f
            && viewport.x >= -0.1f
            && viewport.x <= 1.1f
            && viewport.y >= -0.1f
            && viewport.y <= 1.1f;
    }

    private string NextWildlifeId()
    {
        return "wild:" + nextSequence++;
    }

    private bool TryFindBestHuntTarget(CharacterActor hunter, out WildlifeActor target)
    {
        target = null;
        if (hunter == null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        Vector2Int start = hunter.GetNowXY();

        string hunterId = hunter.Identity != null ? hunter.Identity.PersistentId : hunter.name;
        int bestPriority = -1;
        int bestDistance = int.MaxValue;
        bool bestDangerous = false;
        foreach (WildlifeActor candidate in wildlife)
        {
            Vector2Int candidatePosition = candidate != null
                ? grid.GetXY(candidate.transform.position)
                : default;
            if (candidate == null
                || !candidate.IsAlive
                || !candidate.HuntDesignated
                || (!string.IsNullOrWhiteSpace(candidate.ReservedByPersistentId)
                    && candidate.ReservedByPersistentId != hunterId))
            {
                continue;
            }

            int priority = candidate.PriorityHunt ? 1 : 0;
            int distance = Manhattan(start, candidatePosition);
            bool dangerous = candidate.IsDangerous;
            if (target == null
                || priority > bestPriority
                || (priority == bestPriority && distance < bestDistance)
                || (priority == bestPriority && distance == bestDistance && dangerous && !bestDangerous))
            {
                target = candidate;
                bestPriority = priority;
                bestDistance = distance;
                bestDangerous = dangerous;
            }
        }

        return target != null;
    }

    private bool TryGetWildlife(string wildlifeId, out WildlifeActor target)
    {
        string normalized = wildlifeId?.Trim() ?? string.Empty;
        target = wildlife.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.WildlifeId, normalized, StringComparison.Ordinal));
        return target != null;
    }

    private bool TickFoodRaid(WildlifeActor actor, Grid grid, float now)
    {
        WildlifeFoodRaidOrderSaveData order = foodRaidOrders.FirstOrDefault(
            candidate => candidate != null
                && candidate.state == WildlifeFoodRaidOrderState.Approaching
                && string.Equals(
                    candidate.wildlifeId,
                    actor?.WildlifeId,
                    StringComparison.Ordinal));
        if (order == null)
        {
            return false;
        }

        if (actor == null || !actor.IsAlive)
        {
            order.state = WildlifeFoodRaidOrderState.Cancelled;
            order.outcomeReason =
                "습격 개체가 처치되어 도난이 취소되었습니다.";
            return true;
        }

        WorldItemStackSnapshot target = itemStackRuntime?.GetAllStacks()
            .FirstOrDefault(stack => IsLooseRaidFood(stack)
                && string.Equals(
                    stack.StackId,
                    order.targetStackId,
                    StringComparison.Ordinal));
        if (target == null)
        {
            target = FindReachableFoodRaidTarget(actor, grid, now);
            if (target == null)
            {
                order.state = WildlifeFoodRaidOrderState.Failed;
                order.outcomeReason =
                    "도달 가능한 노출 식량이 없어 아무것도 훔치지 못했습니다.";
                actor.MarkLeaving();
                return true;
            }

            order.targetStackId = target.StackId;
        }

        actor.SetIntent(
            WildlifeIntent.Forage,
            $"노출 식량 {target.DisplayName}을 노리는 중");
        if (actor.GridPosition == target.Position)
        {
            if (itemStackRuntime.TryConsumeStackQuantity(
                    target.StackId,
                    1,
                    out WorldItemStackSnapshot consumed))
            {
                order.stolenQuantity = consumed?.Quantity ?? 0;
                order.state = WildlifeFoodRaidOrderState.Stolen;
                order.outcomeReason =
                    order.stolenQuantity > 0
                        ? "늑대가 식량에 도달해 1개를 훔쳤습니다."
                        : "식량이 먼저 사라져 아무것도 훔치지 못했습니다.";
            }
            else
            {
                order.state = WildlifeFoodRaidOrderState.Failed;
                order.outcomeReason =
                    "식량이 먼저 사라져 아무것도 훔치지 못했습니다.";
            }

            actor.MarkLeaving();
            return true;
        }

        if (!actor.CanRepath(now))
        {
            return true;
        }

        if (actor.TrySetPath(target.Position, now))
        {
            return true;
        }

        WorldItemStackSnapshot replacement =
            FindReachableFoodRaidTarget(actor, grid, now);
        if (replacement == null
            || !actor.TrySetPath(replacement.Position, now))
        {
            order.state = WildlifeFoodRaidOrderState.Failed;
            order.outcomeReason =
                "문 또는 지형에 막혀 도달 가능한 노출 식량이 없습니다.";
            actor.MarkLeaving();
            return true;
        }

        order.targetStackId = replacement.StackId;
        return true;
    }

    private WorldItemStackSnapshot FindReachableFoodRaidTarget(
        WildlifeActor actor,
        Grid grid,
        float now)
    {
        if (actor == null || grid == null || itemStackRuntime == null)
        {
            return null;
        }

        foreach (WorldItemStackSnapshot candidate in itemStackRuntime
                     .GetAllStacks()
                     .Where(IsLooseRaidFood)
                     .OrderBy(stack =>
                         Manhattan(actor.GridPosition, stack.Position))
                     .ThenBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            if (candidate.Position == actor.GridPosition)
            {
                return candidate;
            }

            Queue<GridMoveStep> path = pathSearchBroker?.GetMovePathTo(
                grid,
                actor.GridPosition,
                candidate.Position,
                GridPathSearchPriority.Urgent,
                GridTraversalContext.ForWildlife(actor));
            path ??= grid.GetMovePathTo(
                actor.GridPosition,
                candidate.Position);
            if (path != null && path.Count > 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private void CancelFoodRaidForActor(
        string wildlifeId,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(wildlifeId))
        {
            return;
        }

        foreach (WildlifeFoodRaidOrderSaveData order in foodRaidOrders)
        {
            if (order == null
                || IsFoodRaidTerminal(order.state)
                || !string.Equals(
                    order.wildlifeId,
                    wildlifeId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            order.state = WildlifeFoodRaidOrderState.Cancelled;
            order.outcomeReason = reason ?? string.Empty;
        }
    }

    private void CompleteLeavingFoodRaidForActor(string wildlifeId)
    {
        WildlifeFoodRaidOrderSaveData order = foodRaidOrders.FirstOrDefault(
            candidate => candidate != null
                && candidate.state == WildlifeFoodRaidOrderState.Leaving
                && string.Equals(
                    candidate.wildlifeId,
                    wildlifeId,
                    StringComparison.Ordinal));
        if (order != null)
        {
            order.state = order.stolenQuantity > 0
                ? WildlifeFoodRaidOrderState.Stolen
                : WildlifeFoodRaidOrderState.Failed;
        }
    }

    private void TickBehavior(WildlifeActor actor, Grid grid, float now)
    {
        if (actor == null || !actor.IsAlive || !actor.CanRepath(now))
        {
            return;
        }

        Vector2Int target = actor.State switch
        {
            WildlifeState.Fleeing => ChooseFleePosition(actor, grid),
            WildlifeState.Hunted => ChooseHuntedMovePosition(actor, grid),
            WildlifeState.Retaliating => ChooseHuntedMovePosition(actor, grid),
            WildlifeState.PredatorStalking => ChooseEcologyOrPredatorPosition(actor, grid),
            WildlifeState.Leaving => ChooseLeavingPosition(actor, grid),
            _ => ChooseEcologyOrWanderPosition(actor, grid)
        };

        actor.TrySetPath(target, now);
    }

    private Vector2Int ChooseEcologyOrPredatorPosition(WildlifeActor actor, Grid grid)
    {
        if (ecosystemRuntime != null
            && ecosystemRuntime.TryChooseEcologyTarget(
                actor,
                grid,
                wildlife,
                GetCachedItemStacks(),
                out Vector2Int target,
                out WildlifeIntent intent,
                out string reason))
        {
            actor.SetIntent(intent, reason);
            if (intent == WildlifeIntent.LeaveMap)
            {
                actor.MarkLeaving();
            }
            else if (intent != WildlifeIntent.HuntPrey)
            {
                actor.SetGrazing();
            }

            return target;
        }

        return ChoosePredatorPosition(actor, grid);
    }

    private Vector2Int ChooseLeavingPosition(WildlifeActor actor, Grid grid)
    {
        int exitX = actor.GridPosition.x < grid.width * 0.5f ? 0 : grid.width - 1;
        Vector2Int target = new Vector2Int(exitX, actor.GridPosition.y);
        if (CanWildlifeRoamTargetAt(grid, target, actor.CanEnterDungeon))
        {
            actor.SetIntent(WildlifeIntent.LeaveMap, "먹이와 물을 찾아 지역을 떠남");
            return target;
        }

        return ChooseReachablePosition(actor, grid, minDistance: 4, maxDistance: 10, preferAwayFrom: actor.TerritoryCenter);
    }

    private Vector2Int ChooseEcologyOrWanderPosition(WildlifeActor actor, Grid grid)
    {
        if (ecosystemRuntime != null
            && ecosystemRuntime.TryChooseEcologyTarget(
                actor,
                grid,
                wildlife,
                GetCachedItemStacks(),
                out Vector2Int target,
                out WildlifeIntent intent,
                out string reason))
        {
            actor.SetIntent(intent, reason);
            switch (intent)
            {
                case WildlifeIntent.Forage:
                case WildlifeIntent.Drink:
                    actor.SetGrazing();
                    break;
                case WildlifeIntent.HuntPrey:
                    actor.SetPredatorStalking();
                    break;
                case WildlifeIntent.LeaveMap:
                    actor.MarkLeaving();
                    break;
                default:
                    actor.SetIdle();
                    break;
            }

            return target;
        }

        return ChooseWanderPosition(actor, grid);
    }

    private Vector2Int ChooseWanderPosition(WildlifeActor actor, Grid grid)
    {
        if (actor.Fear >= 4f || (actor.HasLastThreatPosition && actor.LastThreatAge < 12f))
        {
            actor.SetIntent(WildlifeIntent.Flee, "위협을 피해 도망");
            return ChooseFleePosition(actor, grid);
        }

        if (actor.Species != null
            && actor.Species.IsPredator
            && (actor.Hunger >= 0.55f || randomStream.Chance(actor.Species.Aggression * 0.18f)))
        {
            actor.SetIntent(WildlifeIntent.HuntPrey, "먹잇감을 찾는 중");
            actor.SetPredatorStalking();
            return ChoosePredatorPosition(actor, grid);
        }

        actor.SetIntent(WildlifeIntent.Wander, "영역 안을 배회");
        actor.SetGrazing();
        return ChooseReachablePosition(actor, grid, minDistance: 2, maxDistance: 6, preferAwayFrom: null);
    }

    private Vector2Int ChooseHuntedMovePosition(WildlifeActor actor, Grid grid)
    {
        CharacterActor hunter = FindCharacterByPersistentId(actor.ReservedByPersistentId);
        if (hunter != null)
        {
            return ChooseReachablePosition(actor, grid, minDistance: 3, maxDistance: 8, preferAwayFrom: hunter.GetNowXY());
        }

        return ChooseFleePosition(actor, grid);
    }

    private Vector2Int ChooseFleePosition(WildlifeActor actor, Grid grid)
    {
        CharacterActor nearest = FindNearestWorker(actor.GridPosition);
        Vector2Int? awayFrom = actor.HasLastThreatPosition && actor.LastThreatAge < 20f
            ? actor.LastThreatPosition
            : nearest != null ? nearest.GetNowXY() : null;
        return ChooseReachablePosition(actor, grid, minDistance: 4, maxDistance: 10, preferAwayFrom: awayFrom);
    }

    private Vector2Int ChoosePredatorPosition(WildlifeActor actor, Grid grid)
    {
        CharacterActor target = FindBestPredatorTarget(actor);
        if (target == null)
        {
            return ChooseReachablePosition(actor, grid, minDistance: 2, maxDistance: 6, preferAwayFrom: null);
        }

        return target.GetNowXY();
    }

    private Vector2Int ChooseReachablePosition(
        WildlifeActor actor,
        Grid grid,
        int minDistance,
        int maxDistance,
        Vector2Int? preferAwayFrom)
    {
        Vector2Int origin = actor.GridPosition;
        Vector2Int best = origin;
        float bestScore = float.NegativeInfinity;
        int samples = 0;
        int clampedMin = Mathf.Max(1, minDistance);
        int clampedMax = Mathf.Max(clampedMin, maxDistance);
        for (int distance = clampedMin; distance <= clampedMax; distance++)
        {
            for (int direction = -1; direction <= 1; direction += 2)
            {
                Vector2Int candidate = new Vector2Int(origin.x + direction * distance, origin.y);
                if (!CanWildlifeRoamTargetAt(grid, candidate, actor.CanEnterDungeon))
                {
                    continue;
                }

                float score = ScoreWildlifeMovePosition(actor, grid, candidate, preferAwayFrom);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }

                samples++;
            }
        }

        if (samples <= 0)
        {
            return origin;
        }

        Vector2Int selected = best;
        float accumulatedWeight = 0f;
        const float viableScoreRange = 5f;
        for (int distance = clampedMin; distance <= clampedMax; distance++)
        {
            for (int direction = -1; direction <= 1; direction += 2)
            {
                Vector2Int candidate = new Vector2Int(origin.x + direction * distance, origin.y);
                if (!CanWildlifeRoamTargetAt(grid, candidate, actor.CanEnterDungeon))
                {
                    continue;
                }

                float score = ScoreWildlifeMovePosition(actor, grid, candidate, preferAwayFrom);
                if (score < bestScore - viableScoreRange)
                {
                    continue;
                }

                float weight = Mathf.Exp((score - bestScore) * 0.55f);
                accumulatedWeight += weight;
                if (randomStream.NextFloat() * accumulatedWeight <= weight)
                {
                    selected = candidate;
                }
            }
        }

        return selected;
    }

    private float ScoreWildlifeMovePosition(
        WildlifeActor actor,
        Grid grid,
        Vector2Int position,
        Vector2Int? preferAwayFrom)
    {
        float score = 0f;
        GridCell cell = grid.GetGridCell(position);
        GridCellAreaType areaType = cell != null ? cell.AreaType : GridCellAreaType.BlockedExterior;
        if (preferAwayFrom.HasValue)
        {
            Vector2Int threat = preferAwayFrom.Value;
            int distanceFromThreat = Mathf.Abs(position.x - threat.x) + Mathf.Abs(position.y - threat.y);
            score += distanceFromThreat * 4f;
        }
        else
        {
            int territoryDistance = Mathf.Abs(position.x - actor.TerritoryCenter.x)
                + Mathf.Abs(position.y - actor.TerritoryCenter.y);
            int herdDistance = Mathf.Abs(position.x - actor.HerdAnchorPosition.x)
                + Mathf.Abs(position.y - actor.HerdAnchorPosition.y);
            score += Mathf.Clamp(12f - territoryDistance, -8f, 12f);
            score += Mathf.Clamp(7f - herdDistance, -4f, 7f);
            score += actor.Hunger * (areaType == GridCellAreaType.ExteriorPath ? 4f : 1f);

            int direction = Mathf.RoundToInt(Mathf.Sign(position.x - actor.GridPosition.x));
            if (direction != 0 && actor.LastHorizontalDirection != 0)
            {
                score += direction == actor.LastHorizontalDirection ? 2.6f : -2.1f;
            }
        }

        if (areaType == GridCellAreaType.Entrance)
        {
            score -= actor.CanEnterDungeon ? 1.5f : 7f;
        }
        else if (areaType == GridCellAreaType.DropZone)
        {
            score -= 2f;
        }
        else if (areaType == GridCellAreaType.DungeonInterior && !actor.CanEnterDungeon)
        {
            score -= 30f;
        }

        score -= CountNearbyCharacters(position, 3) * (actor.Species != null && actor.Species.IsPredator ? 0.8f : 2.6f);
        return score;
    }

    private IEnumerable<Vector2Int> GetSpawnCandidates(Grid grid)
    {
        return grid.GetCells()
            .Where(cell => IsInitialWildlifeSpawnCell(grid, cell))
            .Select(cell => cell.Position);
    }

    private Vector2Int FindNearbySpawnPosition(Grid grid, Vector2Int anchor)
    {
        for (int radius = 1; radius <= 4; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > radius)
                    {
                        continue;
                    }

                    Vector2Int candidate = anchor + new Vector2Int(dx, dy);
                    if (CanInitialSpawnAt(grid, candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return CanInitialSpawnAt(grid, anchor)
            ? anchor
            : GetSpawnCandidates(grid).FirstOrDefault();
    }

    public static bool IsInitialWildlifeSpawnCell(Grid grid, GridCell cell)
    {
        return cell != null
            && grid != null
            && cell.AreaType == GridCellAreaType.ExteriorPath
            && grid.IsWalkable(cell.Position)
            && IsOutdoorSurfaceCell(grid, cell)
            && !cell.HasOccupantInLayer(GridLayer.Wildlife);
    }

    private bool CanInitialSpawnAt(Grid grid, Vector2Int position)
    {
        return IsInitialWildlifeSpawnCell(grid, grid?.GetGridCell(position));
    }

    private bool TryFindFoodRaidEntry(
        Grid grid,
        out Vector2Int entryPosition)
    {
        entryPosition = default;
        if (grid == null)
        {
            return false;
        }

        GridCell entrance = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.Entrance)
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        if (entrance != null
            && TryFindNearestInitialSpawnCell(
                grid,
                entrance.Position,
                out entryPosition))
        {
            return true;
        }

        GridCell exterior = grid.GetCells()
            .FirstOrDefault(cell => IsInitialWildlifeSpawnCell(grid, cell));
        if (exterior == null)
        {
            return false;
        }

        entryPosition = exterior.Position;
        return true;
    }

    private static bool IsReachableFoodRaidTarget(
        Grid grid,
        Vector2Int entry,
        Vector2Int target)
    {
        if (grid == null || !grid.IsValidGridPos(target))
        {
            return false;
        }

        return entry == target
            || grid.GetMovePathTo(entry, target)?.Count > 0;
    }

    private static bool IsLooseRaidFood(WorldItemStackSnapshot stack)
    {
        return stack != null
            && IsRaidFoodEligible(
                stack.State,
                stack.StockCategory,
                stack.Quantity);
    }

    public static bool IsRaidFoodEligible(
        WorldItemStackState state,
        StockCategory category,
        int quantity)
    {
        return state == WorldItemStackState.Loose
            && category == StockCategory.Food
            && quantity > 0;
    }

    private static bool IsFoodRaidTerminal(
        WildlifeFoodRaidOrderState state)
    {
        return state == WildlifeFoodRaidOrderState.Stolen
            || state == WildlifeFoodRaidOrderState.Cancelled
            || state == WildlifeFoodRaidOrderState.Failed;
    }

    private static WildlifeFoodRaidOrderSaveData CloneFoodRaidOrder(
        WildlifeFoodRaidOrderSaveData source)
    {
        return source == null
            ? new WildlifeFoodRaidOrderSaveData()
            : new WildlifeFoodRaidOrderSaveData
            {
                raidId = source.raidId ?? string.Empty,
                wildlifeId = source.wildlifeId ?? string.Empty,
                targetStackId = source.targetStackId ?? string.Empty,
                state = source.state,
                stolenQuantity = Mathf.Max(0, source.stolenQuantity),
                outcomeReason = source.outcomeReason ?? string.Empty
            };
    }

    private static WildlifeFoodRaidOrderSnapshot ToFoodRaidSnapshot(
        WildlifeFoodRaidOrderSaveData source)
    {
        WildlifeFoodRaidOrderSaveData value =
            source ?? new WildlifeFoodRaidOrderSaveData();
        return new WildlifeFoodRaidOrderSnapshot(
            value.raidId,
            value.wildlifeId,
            value.targetStackId,
            value.state,
            value.stolenQuantity,
            value.outcomeReason);
    }

    private static bool IsValidCurrentWildlifePosition(Grid grid, WildlifeActor actor)
    {
        if (grid == null || actor == null || !grid.IsWalkable(actor.GridPosition))
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(actor.GridPosition);
        if (cell == null || cell.AreaType == GridCellAreaType.BlockedExterior)
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.ExteriorPath
            && !IsOutdoorSurfaceCell(grid, cell))
        {
            return false;
        }

        return actor.CanEnterDungeon || cell.AreaType != GridCellAreaType.DungeonInterior;
    }

    private bool TryFindNearestInitialSpawnCell(Grid grid, Vector2Int origin, out Vector2Int position)
    {
        position = default;
        if (grid == null)
        {
            return false;
        }

        int maxRadius = Mathf.Max(grid.width, grid.height);
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != radius)
                    {
                        continue;
                    }

                    Vector2Int candidate = origin + new Vector2Int(dx, dy);
                    if (!grid.IsValidGridPos(candidate) || !CanInitialSpawnAt(grid, candidate))
                    {
                        continue;
                    }

                    position = candidate;
                    return true;
                }
            }
        }

        GridCell fallback = grid.GetCells().FirstOrDefault(cell => IsInitialWildlifeSpawnCell(grid, cell));
        if (fallback == null)
        {
            return false;
        }

        position = fallback.Position;
        return true;
    }

    private bool CanWildlifeRoamTargetAt(Grid grid, Vector2Int position, bool canEnterDungeon)
    {
        GridCell cell = grid?.GetGridCell(position);
        if (cell == null
            || cell.AreaType == GridCellAreaType.DropZone
            || cell.AreaType == GridCellAreaType.Entrance)
        {
            return false;
        }

        return CanSpawnAt(grid, position, canEnterDungeon);
    }

    private bool CanSpawnAt(Grid grid, Vector2Int position, bool canEnterDungeon)
    {
        GridCell cell = grid?.GetGridCell(position);
        if (cell == null || !grid.IsWalkable(position) || cell.HasOccupantInLayer(GridLayer.Wildlife))
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.BlockedExterior)
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.ExteriorPath
            && !IsOutdoorSurfaceCell(grid, cell))
        {
            return false;
        }

        return canEnterDungeon || cell.AreaType != GridCellAreaType.DungeonInterior;
    }

    public static bool IsOutdoorSurfaceCell(Grid grid, GridCell cell)
    {
        if (grid == null || cell == null || cell.AreaType != GridCellAreaType.ExteriorPath)
        {
            return false;
        }

        if (cell.Position.y > 0)
        {
            return false;
        }

        Vector2Int belowPosition = new Vector2Int(cell.Position.x, cell.Position.y - 1);
        GridCell below = grid.GetGridCell(belowPosition);
        return below == null || below.AreaType == GridCellAreaType.BlockedExterior;
    }

    private void RecordHuntNarrative(CharacterActor hunter, WildlifeActor target)
    {
        int day = 0;
        if (gameDataProvider != null && gameDataProvider.TryGetGameData(out GameData data))
        {
            day = data.day != null ? data.day.Value : 0;
        }

        hunter.Progression?.RecordNarrative(
            CharacterNarrativeDomain.Survival,
            "survival/hunt",
            target != null ? "wildlife:" + target.SpeciesId : "wildlife",
            target != null && target.IsDangerous ? "dangerous-hunt" : "hunt",
            target != null ? target.MaxHealth : 0f,
            day);
    }

    private CharacterActor FindCharacterByPersistentId(string persistentId)
    {
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return null;
        }

        IReadOnlyList<CharacterActor> actors = worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor != null
                && actor.Identity != null
                && string.Equals(actor.Identity.PersistentId, persistentId, StringComparison.Ordinal))
            {
                return actor;
            }
        }

        return null;
    }

    private CharacterActor FindNearestWorker(Vector2Int position)
    {
        CharacterActor best = null;
        int bestDistance = int.MaxValue;
        IReadOnlyList<CharacterActor> actors = worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor == null || actor.IsDead || !CharacterWorkRoleUtility.TryGetWork(actor, out _))
            {
                continue;
            }

            Vector2Int actorPosition = actor.GetNowXY();
            int distance = Mathf.Abs(actorPosition.x - position.x) + Mathf.Abs(actorPosition.y - position.y);
            if (best != null && distance >= bestDistance)
            {
                continue;
            }

            best = actor;
            bestDistance = distance;
        }

        return best;
    }

    private CharacterActor FindBestPredatorTarget(WildlifeActor predator)
    {
        if (predator == null)
        {
            return null;
        }

        CharacterActor best = null;
        float bestScore = float.MinValue;
        IReadOnlyList<CharacterActor> actors = worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            Vector2Int actorPosition = actor.GetNowXY();
            int distance = Mathf.Abs(actorPosition.x - predator.GridPosition.x)
                + Mathf.Abs(actorPosition.y - predator.GridPosition.y);
            if (distance > 10)
            {
                continue;
            }

            float healthWeakness = actor.MaxHealth > 0
                ? Mathf.Clamp01(1f - actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth))
                : 0f;
            float workerPenalty = CharacterWorkRoleUtility.TryGetWork(actor, out _) ? 0.2f : 0f;
            float score = healthWeakness * 5f
                + Mathf.Clamp(10f - distance, 0f, 10f) * 0.45f
                + predator.Hunger * 3f
                - workerPenalty;
            if (best == null || score > bestScore)
            {
                best = actor;
                bestScore = score;
            }
        }

        return best;
    }

    private bool TryResolvePredatorWildlifeContact(WildlifeActor predator)
    {
        if (predator == null
            || !predator.IsAlive
            || predator.Species == null
            || predator.Species.Diet != WildlifeDietType.Carnivore
            || predator.Hunger < 0.45f)
        {
            return false;
        }

        WildlifeActor prey = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < wildlife.Count; i++)
        {
            WildlifeActor candidate = wildlife[i];
            if (candidate == null
                || candidate == predator
                || !candidate.IsAlive
                || candidate.Species == null
                || candidate.Species.Diet == WildlifeDietType.Carnivore
                || !IsAdjacentCell(predator.GridPosition, candidate.GridPosition))
            {
                continue;
            }

            float weakness = candidate.MaxHealth > 0
                ? 1f - (candidate.CurrentHealth / (float)candidate.MaxHealth)
                : 0f;
            float score = weakness * 5f
                + Mathf.Clamp(predator.MaxHealth - candidate.MaxHealth, -8f, 12f)
                - (candidate.IsDangerous ? 6f : 0f);
            if (prey == null || score > bestScore)
            {
                prey = candidate;
                bestScore = score;
            }
        }

        if (prey == null)
        {
            return false;
        }

        int damage = Mathf.Max(
            1,
            Mathf.RoundToInt((predator.RetaliationDamage * 0.75f) + (predator.MaxHealth * 0.12f)));
        prey.RegisterThreat(predator.GridPosition, 0.65f);
        prey.ApplyDamage(damage, null);
        predator.SetIntent(WildlifeIntent.HuntPrey, prey.IsAlive ? "먹잇감을 몰아붙이는 중" : "먹잇감을 쓰러뜨림");
        predator.ChangeHunger(-0.18f);
        if (prey.IsAlive)
        {
            return true;
        }

        CancelFoodRaidForActor(
            prey.WildlifeId,
            "습격 늑대가 처치되어 도난이 취소되었습니다.");
        ecosystemRuntime?.NotifyWildlifeKilled(prey, byHunt: false);
        carcassService?.SpawnCarcass(prey);
        wildlife.Remove(prey);
        nextBehaviorTickByWildlifeId.Remove(prey.WildlifeId);
        UnityEngine.Object.Destroy(prey.gameObject);
        predator.ChangeHunger(-0.45f);
        return true;
    }

    private static bool IsAdjacentCell(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) <= 1
            && a != b;
    }

    private IReadOnlyList<WorldItemStackSnapshot> GetCachedItemStacks()
    {
        IWorldItemStackRuntime runtime = itemStackRuntime;
        if (runtime == null)
        {
            cachedItemStackVersion = -1;
            cachedItemStacks = Array.Empty<WorldItemStackSnapshot>();
            return cachedItemStacks;
        }

        if (cachedItemStackVersion == runtime.ItemStackVersion)
        {
            return cachedItemStacks;
        }

        cachedItemStackVersion = runtime.ItemStackVersion;
        cachedItemStacks = runtime.GetAllStacks()
            .Where(stack => stack != null)
            .ToArray();
        return cachedItemStacks;
    }

    private int CountNearbyCharacters(Vector2Int position, int radius)
    {
        int count = 0;
        IReadOnlyList<CharacterActor> actors = worldRegistry?.Characters ?? Array.Empty<CharacterActor>();
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            Vector2Int actorPosition = actor.GetNowXY();
            int distance = Mathf.Abs(actorPosition.x - position.x) + Mathf.Abs(actorPosition.y - position.y);
            if (distance <= radius)
            {
                count++;
            }
        }

        return count;
    }

    private float NextRange(float minInclusive, float maxInclusive)
    {
        return Mathf.Lerp(minInclusive, maxInclusive, randomStream.NextFloat());
    }
}
