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
    IDungeonRestoreTransactionParticipant,
    IWildlifeRestorePort,
    ITickable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("WildlifeRuntime.Tick");

    private const int InitialWildlifeTargetCount = 7;
    private const float CarcassTickInterval = 2f;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IWildlifeEcosystemRuntime ecosystemRuntime;
    private readonly IMainCameraProvider mainCameraProvider;
    private readonly IWildlifeCarcassService carcassService;
    private readonly IGameClock gameClock;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;
    private readonly IRandomStream randomStream;
    private readonly WildlifeWorldServices worldServices;
    private readonly WildlifeCombatServices combatServices;
    private readonly WildlifeExecutionServices executionServices;
    private readonly WildlifeWorldRuntime worldRuntime;
    private readonly WildlifeRestoreCoordinator restoreCoordinator;
    private readonly WildlifeDiseaseVectorRuntime diseaseVectorRuntime;
    private WildlifeHuntRuntime huntRuntime;
    private WildlifeBehaviorRuntime behaviorRuntime;
    private WildlifePopulationState population = new WildlifePopulationState();

    private List<WildlifeActor> wildlife => population.Actors;
    private Dictionary<string, float> nextBehaviorTickByWildlifeId =>
        population.NextBehaviorTickByWildlifeId;
    private List<WildlifeFoodRaidOrderSaveData> foodRaidOrders =>
        population.FoodRaidOrders;
    private int nextSequence
    {
        get => population.NextSequence;
        set => population.NextSequence = value;
    }
    private int lastDiseaseVectorAbsoluteDay
    {
        get => population.LastDiseaseVectorAbsoluteDay;
        set => population.LastDiseaseVectorAbsoluteDay = value;
    }
    private bool initialSpawnCompleted
    {
        get => population.InitialSpawnCompleted;
        set => population.InitialSpawnCompleted = value;
    }
    private float nextCarcassTickAt
    {
        get => population.NextCarcassTickAt;
        set => population.NextCarcassTickAt = value;
    }

    public WildlifeRuntime(
        WildlifeWorldServices world,
        WildlifeCombatServices combat,
        WildlifeExecutionServices execution,
        WildlifeRestoreServices restore)
    {
        WildlifeWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        WildlifeCombatServices requiredCombat = combat
            ?? throw new ArgumentNullException(nameof(combat));
        WildlifeExecutionServices requiredExecution = execution
            ?? throw new ArgumentNullException(nameof(execution));
        worldServices = requiredWorld;
        combatServices = requiredCombat;
        executionServices = requiredExecution;
        WildlifeRestoreServices requiredRestore = restore
            ?? throw new ArgumentNullException(nameof(restore));
        gridSystemProvider = requiredWorld.Grid;
        speciesCatalog = requiredWorld.Species;
        ecosystemRuntime = requiredWorld.Ecosystem;
        mainCameraProvider = requiredWorld.MainCamera;
        carcassService = requiredCombat.Carcasses;
        gameClock = requiredExecution.Clock;
        performanceRecorder = requiredExecution.Performance;
        randomStream = requiredExecution.RandomStreams.Get("wildlife.runtime");
        diseaseVectorRuntime = new WildlifeDiseaseVectorRuntime(requiredWorld);
        worldRuntime = new WildlifeWorldRuntime(requiredWorld, requiredExecution);
        RebuildPopulationRuntimes();
        restoreCoordinator = new WildlifeRestoreCoordinator(
            this,
            requiredWorld,
            requiredCombat,
            requiredExecution,
            requiredRestore,
            worldRuntime);
    }
    WildlifePopulationState IWildlifeRestorePort.Population => population;

    void IWildlifeRestorePort.ReplacePopulation(WildlifePopulationState replacement)
    {
        population = replacement
            ?? throw new ArgumentNullException(nameof(replacement));
    }
    void IWildlifeRestorePort.RebuildPopulationRuntimes() =>
        RebuildPopulationRuntimes();

    private void RebuildPopulationRuntimes()
    {
        huntRuntime = new WildlifeHuntRuntime(
            worldServices,
            combatServices,
            executionServices,
            wildlife,
            CancelFoodRaidForActor,
            worldRuntime.DestroyActor);
        behaviorRuntime = new WildlifeBehaviorRuntime(
            worldServices,
            combatServices,
            executionServices,
            wildlife,
            nextBehaviorTickByWildlifeId,
            foodRaidOrders,
            TrySpawnArrival);
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
        if (executionServices.DebugRules.IsEnabled(DungeonDebugCheat.PauseWildlifeAi))
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

            if (!WildlifeWorldRuntime.IsValidCurrentPosition(grid, actor))
            {
                if (!worldRuntime.TryFindNearestInitialSpawnCell(
                        grid,
                        actor.GridPosition,
                        out Vector2Int safePosition))
                {
                    wildlife.RemoveAt(i);
                    nextBehaviorTickByWildlifeId.Remove(actor.WildlifeId);
                    worldRuntime.DestroyActor(actor);
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
                worldRuntime.DestroyActor(actor);
                continue;
            }

            if (!ShouldTickBehavior(actor, now, mainCamera))
            {
                continue;
            }

            behaviorRuntime.TryResolvePredatorWildlifeContact(actor);
            if (TickFoodRaid(actor, grid, now))
            {
                continue;
            }

            TickBehavior(actor, grid, now);
        }

        TryRespawnWildlife(grid, now);
        lastDiseaseVectorAbsoluteDay = diseaseVectorRuntime.PublishDailyExposure(
            wildlife,
            lastDiseaseVectorAbsoluteDay);

        if (now >= nextCarcassTickAt)
        {
            nextCarcassTickAt = now + CarcassTickInterval;
            carcassService?.TickFreshness(CarcassTickInterval);
        }
    }

    public string ParticipantId =>
        WildlifeRestoreCoordinator.RestoreParticipantId;

    public void ValidateRestorePayload(DungeonWildlifeSaveData saveData) =>
        restoreCoordinator.ValidatePayload(saveData);

    public WildlifeRestoreCandidate BuildRestoreCandidate(
        DungeonWildlifeSaveData saveData) =>
        restoreCoordinator.BuildCandidate(saveData);

    public void PublishRestoreCandidate(WildlifeRestoreCandidate candidate) =>
        restoreCoordinator.StageCandidate(candidate);

    public void BeginRestoreCandidate() => restoreCoordinator.Begin();

    public void PublishRestoreCandidate() => restoreCoordinator.Publish();

    public void RollbackPublishedRestoreCandidate() =>
        restoreCoordinator.RollbackPublished();

    public void CompleteRestoreCandidate() => restoreCoordinator.Complete();

    public void DiscardRestoreCandidate() => restoreCoordinator.Discard();

    public DungeonWildlifeSaveData Capture()
    {
        return new DungeonWildlifeSaveData
        {
            version = DungeonWildlifeSaveData.CurrentVersion,
            nextSequence = Mathf.Max(1, nextSequence),
            lastDiseaseVectorAbsoluteDay = Math.Max(
                0,
                lastDiseaseVectorAbsoluteDay),
            wildlife = wildlife
                .Where(actor => actor != null && actor.IsAlive)
                .Select(actor => actor.Capture())
                .ToList(),
            carcasses = carcassService?.CaptureFreshness().ToList()
                ?? new List<WildlifeCarcassFreshnessSaveData>(),
            ecosystem = ecosystemRuntime?.Capture() ?? new DungeonWildlifeEcosystemSaveData(),
            foodRaidOrders = foodRaidOrders
                .Select(WildlifeBehaviorRuntime.CloneFoodRaidOrder)
                .ToList()
        };
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
                : worldRuntime.FindNearbySpawnPosition(grid, anchor);
            if (!WildlifeWorldRuntime.CanInitialSpawnAt(grid, candidate))
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
        if (!WildlifeWorldRuntime.CanInitialSpawnAt(grid, spawnPosition))
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
                        if (!WildlifeWorldRuntime.CanInitialSpawnAt(grid, candidate))
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

    public IReadOnlyList<WorldItemStackSnapshot> GetReachableFoodRaidTargets() =>
        behaviorRuntime.GetReachableFoodRaidTargets();

    public IReadOnlyList<WildlifeFoodRaidOrderSnapshot> GetFoodRaidOrders() =>
        behaviorRuntime.GetFoodRaidOrders();

    public bool TryBeginFoodRaid(
        string raidId,
        int wolfCount,
        out IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders,
        out string failureReason) =>
        behaviorRuntime.TryBeginFoodRaid(
            raidId,
            wolfCount,
            out orders,
            out failureReason);

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
        worldRuntime.DestroyActor(actor);
        return true;
    }

    public int DebugDeleteAll()
    {
        int count = wildlife.Count(actor => actor != null);
        ClearWildlife();
        return count;
    }

    private void ClearWildlife()
    {
        for (int index = wildlife.Count - 1; index >= 0; index--)
        {
            WildlifeActor actor = wildlife[index];
            if (actor == null)
            {
                continue;
            }

            CancelFoodRaidForActor(
                actor.WildlifeId,
                WildlifeFoodRaidOutcomeCodes.RaidActorRemoved);
            worldRuntime.DestroyActor(actor);
        }

        wildlife.Clear();
        nextBehaviorTickByWildlifeId.Clear();
    }

    public bool HasAvailableHuntJob(CharacterActor actor) =>
        huntRuntime.HasAvailableHuntJob(actor);

    public bool TryReserveBestHuntJob(
        CharacterActor actor,
        out WildlifeHuntJob job,
        out string reason) =>
        huntRuntime.TryReserveBestHuntJob(actor, out job, out reason);

    public void ReleaseHuntReservation(
        string wildlifeId,
        CharacterActor actor) =>
        huntRuntime.ReleaseHuntReservation(wildlifeId, actor);

    public bool DesignateHunt(
        string wildlifeId,
        bool designated,
        bool priority = false) =>
        huntRuntime.DesignateHunt(wildlifeId, designated, priority);

    public bool ApplyHuntHit(
        CharacterActor hunter,
        string wildlifeId,
        out string message) =>
        huntRuntime.ApplyHuntHit(hunter, wildlifeId, out message);

    public bool CanAttackHuntTargetFrom(
        CharacterActor hunter,
        WildlifeActor target,
        Grid grid,
        Vector2Int attackerCell) =>
        huntRuntime.CanAttackHuntTargetFrom(
            hunter,
            target,
            grid,
            attackerCell);

    public bool NeedsHuntReload(CharacterActor hunter) =>
        huntRuntime.NeedsHuntReload(hunter);

    public float GetHuntReloadDuration(CharacterActor hunter) =>
        huntRuntime.GetHuntReloadDuration(hunter);

    public bool TryReloadHuntWeapon(
        CharacterActor hunter,
        out string message) =>
        huntRuntime.TryReloadHuntWeapon(hunter, out message);

    public float GetHuntAttackInterval(CharacterActor hunter) =>
        huntRuntime.GetHuntAttackInterval(hunter);

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
                butcher?.BuildingVisitor,
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

        List<Vector2Int> candidates = worldRuntime
            .GetInitialSpawnCandidates(grid)
            .ToList();
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
                    : worldRuntime.FindNearbySpawnPosition(grid, anchor);
                if (!WildlifeWorldRuntime.CanInitialSpawnAt(grid, position))
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

        List<Vector2Int> candidates = worldRuntime
            .GetInitialSpawnCandidates(grid)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        for (int attempt = 0; attempt < 24; attempt++)
        {
            Vector2Int position = candidates[randomStream.NextInt(0, candidates.Count)];
            if (!WildlifeWorldRuntime.CanInitialSpawnAt(grid, position))
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
        WildlifeActor actor = worldRuntime.CreateActor(
            grid,
            species,
            position,
            wildlifeId,
            saveData,
            detachedRestore: false);
        wildlife.Add(actor);
        return actor;
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

    private bool TickFoodRaid(WildlifeActor actor, Grid grid, float now) =>
        behaviorRuntime.TickFoodRaid(actor, grid, now);

    private void CancelFoodRaidForActor(string wildlifeId, string reason) =>
        behaviorRuntime.CancelFoodRaidForActor(wildlifeId, reason);

    private void CompleteLeavingFoodRaidForActor(string wildlifeId) =>
        behaviorRuntime.CompleteLeavingFoodRaidForActor(wildlifeId);

    private void TickBehavior(WildlifeActor actor, Grid grid, float now) =>
        behaviorRuntime.TickBehavior(actor, grid, now);

    public static bool IsInitialWildlifeSpawnCell(Grid grid, GridCell cell) =>
        WildlifeWorldRuntime.IsInitialSpawnCell(grid, cell);

    public static bool IsRaidFoodEligible(
        WorldItemStackState state,
        StockCategory category,
        int quantity) =>
        WildlifeBehaviorRuntime.IsRaidFoodEligible(state, category, quantity);

    public static bool IsOutdoorSurfaceCell(Grid grid, GridCell cell) =>
        WildlifeWorldRuntime.IsOutdoorSurfaceCell(grid, cell);

    private float NextRange(float minInclusive, float maxInclusive)
    {
        return minInclusive
            + ((maxInclusive - minInclusive) * randomStream.NextFloat());
    }
}
