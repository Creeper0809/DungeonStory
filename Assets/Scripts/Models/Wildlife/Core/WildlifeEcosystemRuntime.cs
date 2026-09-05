using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed partial class WildlifeEcosystemRuntime :
    IDisposable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("WildlifeEcosystemRuntime.Tick");

    private const int DefaultDesiredWildlifeCount = 8;
    private const float PatchTickInterval = 1f;
    private const float OverlayRefreshInterval = 0.45f;
    private const float GlobalRespawnCooldownSeconds = 45f;
    private const float HuntedRespawnCooldownSeconds = 120f;
    private const float NaturalRespawnCooldownSeconds = 75f;

    private readonly IWildlifeEcosystemWorldPort world;
    private readonly IWildlifeEcosystemPresentationPort presentation;
    private readonly IGameClock gameClock;
    private readonly IPersistentIdGenerator persistentIds;
    private readonly IRandomStream randomStream;
    private readonly IGameCalendar calendar;
    private List<WildlifeHabitatPatch> patches = new List<WildlifeHabitatPatch>();
    private Dictionary<string, double> speciesRespawnAt =
        new Dictionary<string, double>(StringComparer.Ordinal);

    private IWildlifeGridPort initializedGrid;
    private float nextPatchTickAt;
    private float nextOverlayRefreshAt;
    private double nextGlobalRespawnAt;
    private float recentHuntPressure;
    private float recentPredationPressure;
    private bool initialized;
    private bool derivedPresentationDirty;

    public WildlifeEcosystemRuntime(
        IWildlifeEcosystemWorldPort world,
        IWildlifeEcosystemPresentationPort presentation,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IPersistentIdGenerator persistentIds,
        IGameCalendar calendar = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.persistentIds = persistentIds
            ?? throw new ArgumentNullException(nameof(persistentIds));
        this.calendar = calendar;
        randomStream = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("wildlife-ecosystem");
    }

    public bool OverlayEnabled => presentation.OverlayEnabled;
    public IReadOnlyList<WildlifeHabitatPatch> Patches => patches;

    public void Initialize()
    {
    }

    public void Dispose()
    {
        presentation.Dispose();
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
        if (!world.TryGetGrid(out IWildlifeGridPort grid))
        {
            return;
        }

        EnsureInitialized(grid);
        RebuildDerivedPresentationIfNeeded(grid);
        float now = gameClock.Time;
        if (now >= nextPatchTickAt)
        {
            float delta = nextPatchTickAt <= 0f ? PatchTickInterval : Mathf.Max(0f, now - (nextPatchTickAt - PatchTickInterval));
            nextPatchTickAt = now + PatchTickInterval;
            TickPatches(delta);
        }

        recentHuntPressure = Mathf.Max(0f, recentHuntPressure - gameClock.DeltaTime / 180f);
        recentPredationPressure = Mathf.Max(0f, recentPredationPressure - gameClock.DeltaTime / 180f);

        if (presentation.OverlayEnabled && now >= nextOverlayRefreshAt)
        {
            nextOverlayRefreshAt = now + OverlayRefreshInterval;
            presentation.RefreshOverlay(grid, patches);
        }
    }

    public void EnsureInitialized(IWildlifeGridPort grid)
    {
        if (grid == null)
        {
            return;
        }

        if (initialized && initializedGrid == grid)
        {
            return;
        }

        initialized = true;
        initializedGrid = grid;
        patches.Clear();
        LoadSceneMarkers(grid);

        if (patches.Count == 0)
        {
            GenerateDefaultPatches(grid);
        }
        else
        {
            ReplaceWaterPatchesWithWorldSources(grid);
        }
        EnsureForagePatches(grid);

        derivedPresentationDirty = true;
        RebuildDerivedPresentationIfNeeded(grid);
    }

    public DungeonWildlifeEcosystemSaveData Capture()
    {
        return new DungeonWildlifeEcosystemSaveData
        {
            version = DungeonWildlifeEcosystemSaveData.CurrentVersion,
            recentHuntPressure = Mathf.Max(0f, recentHuntPressure),
            recentPredationPressure = Mathf.Max(0f, recentPredationPressure),
            globalRespawnRemainingSeconds =
                CanonicalizeRespawnRemainingSeconds(
                    nextGlobalRespawnAt - (double)gameClock.Time),
            speciesRespawns = speciesRespawnAt
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new WildlifeSpeciesRespawnSaveData
                {
                    speciesId = pair.Key,
                    remainingSeconds = CanonicalizeRespawnRemainingSeconds(
                        pair.Value - (double)gameClock.Time)
                })
                .ToList(),
            patches = patches.Select(patch => patch.Capture()).ToList()
        };
    }

    internal static float CanonicalizeRespawnRemainingSeconds(double seconds)
    {
        if (double.IsNaN(seconds)
            || double.IsInfinity(seconds)
            || seconds > float.MaxValue)
        {
            throw new InvalidOperationException(
                "Wildlife respawn remaining time must be finite.");
        }

        if (seconds <= 0f)
            return 0f;

        // The internal deadline is double while both the clock and save token
        // are floats. Every float is exactly representable as a double, so an
        // immediate restore/capture preserves the authored float payload
        // without imposing a lossy decimal quantization.
        return (float)seconds;
    }

    public WildlifeEcosystemOverview GetOverview(
        IReadOnlyList<IWildlifeAnimalPort> wildlife)
    {
        int alive = wildlife?.Count(actor => actor != null && actor.IsAlive) ?? 0;
        int desired = CalculateDesiredWildlifeCount();
        float food = AverageResource(WildlifeHabitatType.Grass, WildlifeHabitatType.Brush);
        float water = AverageResource(WildlifeHabitatType.Water);
        float predatorDanger = Mathf.Clamp01(
            (wildlife?.Count(actor => actor != null
                && actor.IsAlive
                && actor.Species != null
                && (actor.Species.Diet == WildlifeDietType.Carnivore || actor.Species.IsPredator)) ?? 0) / 4f
            + recentPredationPressure * 0.35f);
        float crowding = desired <= 0 ? 0f : Mathf.Clamp01(alive / (float)Mathf.Max(1, desired));
        return new WildlifeEcosystemOverview(
            patches.Count,
            patches.Count(patch => patch.HabitatType is WildlifeHabitatType.Grass or WildlifeHabitatType.Brush),
            patches.Count(patch => patch.HabitatType == WildlifeHabitatType.Water),
            food,
            water,
            predatorDanger,
            crowding,
            desired,
            alive,
            CanonicalizeRespawnRemainingSeconds(
                nextGlobalRespawnAt - (double)gameClock.Time));
    }

    public void SetOverlayEnabled(bool enabled)
    {
        presentation.SetOverlayEnabled(enabled);
        if (!presentation.OverlayEnabled)
        {
            return;
        }

        if (world.TryGetGrid(out IWildlifeGridPort grid))
        {
            EnsureInitialized(grid);
            presentation.RefreshOverlay(grid, patches);
        }
    }

    public void TickAnimal(
        IWildlifeAnimalPort actor,
        IWildlifeGridPort grid,
        float deltaTime)
    {
        if (actor == null || !actor.IsAlive || grid == null)
        {
            return;
        }

        if (calendar != null && actor.Species != null
            && !actor.Species.IsActiveIn(calendar.Season))
        {
            actor.SetIntent(WildlifeIntent.LeaveMap, "비활동 계절 이동");
            return;
        }

        if (actor.State is WildlifeState.Captured
            or WildlifeState.Hunted
            or WildlifeState.Fleeing
            or WildlifeState.Retaliating)
        {
            return;
        }

        EnsureInitialized(grid);
        WildlifeHabitatPatch currentPatch = null;
        int nearestDistance = int.MaxValue;
        for (int index = 0; index < patches.Count; index++)
        {
            WildlifeHabitatPatch candidate = patches[index];
            if (candidate == null
                || !candidate.Contains(actor.GridPosition)
                || !PatchMatchesIntent(candidate, actor.Intent))
            {
                continue;
            }

            int distance =
                Mathf.Abs(candidate.Center.x - actor.GridPosition.x)
                + Mathf.Abs(candidate.Center.y - actor.GridPosition.y);
            if (distance >= nearestDistance)
            {
                continue;
            }

            currentPatch = candidate;
            nearestDistance = distance;
        }

        if (currentPatch == null)
        {
            return;
        }

        float needScale = Mathf.Max(0.1f, deltaTime);
        switch (currentPatch.HabitatType)
        {
            case WildlifeHabitatType.Water:
                if (actor.Thirst > 0.05f)
                {
                    float requested = needScale * 0.12f;
                    float consumed;
                    if (!string.IsNullOrWhiteSpace(currentPatch.LinkedWaterSourceId)
                        && world.TryDrinkWater(
                            currentPatch.LinkedWaterSourceId,
                            requested,
                            out float sharedConsumed))
                    {
                        consumed = sharedConsumed;
                        if (world.TryGetWaterSource(
                            currentPatch.LinkedWaterSourceId,
                            out WildlifeWaterSourceSnapshot sharedSource))
                        {
                            currentPatch.SynchronizeResource(sharedSource.Capacity, sharedSource.Remaining);
                        }
                    }
                    else
                    {
                        consumed = currentPatch.Consume(requested);
                    }
                    actor.ChangeThirst(-consumed * 0.8f);
                    actor.SetIntent(WildlifeIntent.Drink, "물가에서 목을 축이는 중");
                }
                break;
            case WildlifeHabitatType.Grass:
            case WildlifeHabitatType.Brush:
                if (CanForageFromPatch(actor.Species, currentPatch) && actor.Hunger > 0.05f)
                {
                    float consumed = currentPatch.Consume(needScale * 0.08f);
                    actor.ChangeHunger(-consumed * 0.65f);
                    presentation.RefreshPatch(currentPatch);
                    actor.SetIntent(WildlifeIntent.Forage, currentPatch.HabitatType == WildlifeHabitatType.Brush
                        ? "덤불 사이에서 먹이를 찾는 중"
                        : "풀을 뜯는 중");
                }
                break;
            case WildlifeHabitatType.Burrow:
            case WildlifeHabitatType.Lair:
                if (actor.Hunger < 0.6f && actor.Thirst < 0.65f && actor.Fear < 3f)
                {
                    actor.SetIntent(WildlifeIntent.Rest, "은신처에서 쉬는 중");
                }
                break;
        }
    }

    public bool TryChooseEcologyTarget(
        IWildlifeAnimalPort actor,
        IWildlifeGridPort grid,
        IReadOnlyList<IWildlifeAnimalPort> wildlife,
        IReadOnlyList<WildlifeCarcassStackSnapshot> itemStacks,
        out Vector2Int target,
        out WildlifeIntent intent,
        out string reason)
    {
        target = actor != null ? actor.GridPosition : Vector2Int.zero;
        intent = WildlifeIntent.Wander;
        reason = string.Empty;
        if (actor == null || !actor.IsAlive || grid == null)
        {
            return false;
        }

        EnsureInitialized(grid);
        if (actor.Fear >= 4f || (actor.HasLastThreatPosition && actor.LastThreatAge < 14f))
        {
            target = ChooseFleeSurfaceTarget(actor, grid);
            intent = WildlifeIntent.Flee;
            reason = "위협을 피해 도망";
            return target != actor.GridPosition;
        }

        if (actor.Thirst >= 0.52f
            && TryFindPatchTarget(actor, grid, patch => patch.HabitatType == WildlifeHabitatType.Water && !patch.IsDepleted, out target))
        {
            intent = WildlifeIntent.Drink;
            reason = "물가로 이동";
            return true;
        }

        if (actor.Hunger >= 0.55f)
        {
            if (actor.Species != null
                && (actor.Species.Diet == WildlifeDietType.Carnivore
                    || actor.Species.Diet == WildlifeDietType.Scavenger)
                && TryFindCarcassTarget(actor, grid, itemStacks, out target))
            {
                intent = WildlifeIntent.Forage;
                reason = "사체 냄새를 따라감";
                return true;
            }

            if (actor.Species != null
                && actor.Species.Diet == WildlifeDietType.Carnivore
                && TryFindPreyTarget(actor, grid, wildlife, out target))
            {
                intent = WildlifeIntent.HuntPrey;
                reason = "작은 먹잇감을 추적";
                return true;
            }

            if (CanForage(actor.Species)
                && TryFindPatchTarget(actor, grid, patch =>
                        (patch.HabitatType == WildlifeHabitatType.Grass || patch.HabitatType == WildlifeHabitatType.Brush)
                        && !patch.IsDepleted
                        && patch.IsPreferredBy(actor.Species),
                    out target))
            {
                intent = WildlifeIntent.Forage;
                reason = "먹이 패치로 이동";
                return true;
            }
        }

        int territoryDistance = Mathf.Abs(actor.GridPosition.x - actor.TerritoryCenter.x)
            + Mathf.Abs(actor.GridPosition.y - actor.TerritoryCenter.y);
        if (actor.Species != null
            && territoryDistance > Mathf.CeilToInt(actor.Species.TerritoryRadius)
            && TryFindSurfaceNear(grid, actor, actor.TerritoryCenter, out target))
        {
            intent = WildlifeIntent.ReturnToTerritory;
            reason = "자기 영역으로 돌아가는 중";
            return true;
        }

        if ((actor.Hunger >= 0.9f || actor.Thirst >= 0.9f)
            && TryFindMapExitTarget(actor, grid, out target))
        {
            intent = WildlifeIntent.LeaveMap;
            reason = "먹이와 물을 찾아 지역을 떠남";
            return true;
        }

        float restChance = actor.Species != null ? actor.Species.RestPreference * 0.28f : 0.12f;
        if (actor.Hunger < 0.65f
            && actor.Thirst < 0.7f
            && randomStream.Chance(restChance)
            && TryFindPatchTarget(actor, grid, patch =>
                    patch.HabitatType is WildlifeHabitatType.Burrow or WildlifeHabitatType.Brush or WildlifeHabitatType.Lair
                    && patch.IsPreferredBy(actor.Species),
                out target))
        {
            intent = WildlifeIntent.Rest;
            reason = "은신처로 이동";
            return true;
        }

        if (TryFindPatchTarget(actor, grid, patch => patch.IsPreferredBy(actor.Species), out target))
        {
            if (target == actor.GridPosition && randomStream.Chance(0.7f))
            {
                return false;
            }

            intent = WildlifeIntent.Wander;
            reason = "영역 안을 배회";
            return true;
        }

        return false;
    }

    public bool TryConsumeRespawnOpportunity(
        float now,
        int aliveCount,
        IReadOnlyList<WildlifeSpeciesDefinition> species,
        out WildlifeSpeciesDefinition selectedSpecies)
    {
        selectedSpecies = null;
        if (species == null || species.Count == 0 || aliveCount >= CalculateDesiredWildlifeCount())
        {
            return false;
        }

        if (now < nextGlobalRespawnAt)
        {
            return false;
        }

        float food = AverageResource(WildlifeHabitatType.Grass, WildlifeHabitatType.Brush);
        float water = AverageResource(WildlifeHabitatType.Water);
        if (food < 0.25f || water < 0.2f)
        {
            nextGlobalRespawnAt = now + 30f;
            return false;
        }

        List<WildlifeSpeciesDefinition> candidates = species
            .Where(definition => definition != null
                && definition.SpawnWeight > 0f
                && (calendar == null || definition.IsActiveIn(calendar.Season))
                && (!speciesRespawnAt.TryGetValue(
                        definition.SpeciesId,
                        out double speciesAt)
                    || now >= speciesAt))
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        float totalWeight = candidates.Sum(candidate => ScoreRespawnWeight(
            candidate,
            food,
            water,
            calendar?.Season));
        if (totalWeight <= 0f)
        {
            return false;
        }

        float roll = randomStream.NextFloat() * totalWeight;
        foreach (WildlifeSpeciesDefinition candidate in candidates)
        {
            roll -= ScoreRespawnWeight(
                candidate,
                food,
                water,
                calendar?.Season);
            if (roll <= 0f)
            {
                selectedSpecies = candidate;
                break;
            }
        }

        selectedSpecies ??= candidates[candidates.Count - 1];
        nextGlobalRespawnAt = now + GlobalRespawnCooldownSeconds;
        speciesRespawnAt[selectedSpecies.SpeciesId] = now
            + Mathf.Lerp(45f, 95f, randomStream.NextFloat());
        return true;
    }

    public void NotifyWildlifeKilled(IWildlifeAnimalPort actor, bool byHunt)
    {
        if (actor == null || actor.Species == null)
        {
            return;
        }

        float now = gameClock.Time;
        if (byHunt)
        {
            recentHuntPressure = Mathf.Clamp01(recentHuntPressure + 0.22f);
            speciesRespawnAt[actor.SpeciesId] = now + HuntedRespawnCooldownSeconds;
            nextGlobalRespawnAt = Math.Max(
                nextGlobalRespawnAt,
                now + GlobalRespawnCooldownSeconds);
        }
        else
        {
            recentPredationPressure = Mathf.Clamp01(recentPredationPressure + 0.18f);
            speciesRespawnAt[actor.SpeciesId] = now + NaturalRespawnCooldownSeconds;
        }
    }

    public bool ShouldRemoveLeavingAnimal(
        IWildlifeAnimalPort actor,
        IWildlifeGridPort grid)
    {
        if (actor == null || grid == null || !actor.IsAlive)
        {
            return false;
        }

        if (actor.State != WildlifeState.Leaving && actor.Intent != WildlifeIntent.LeaveMap)
        {
            return false;
        }

        return actor.GridPosition.x <= 0 || actor.GridPosition.x >= grid.Width - 1;
    }

    private void TickPatches(float deltaTime)
    {
        foreach (WildlifeHabitatPatch patch in patches)
        {
            if (!string.IsNullOrWhiteSpace(patch.LinkedWaterSourceId)
                && world.TryGetWaterSource(
                    patch.LinkedWaterSourceId,
                    out WildlifeWaterSourceSnapshot source))
            {
                patch.SynchronizeResource(source.Capacity, source.Remaining);
            }
            else
            {
                patch.Tick(deltaTime);
            }
        }

        presentation.RefreshPatches(patches);
    }

    private static bool PatchMatchesIntent(WildlifeHabitatPatch patch, WildlifeIntent intent)
    {
        if (patch == null)
        {
            return false;
        }

        return intent switch
        {
            WildlifeIntent.Drink => patch.HabitatType == WildlifeHabitatType.Water,
            WildlifeIntent.Forage => patch.HabitatType is WildlifeHabitatType.Grass or WildlifeHabitatType.Brush,
            WildlifeIntent.Rest => patch.HabitatType is WildlifeHabitatType.Burrow
                or WildlifeHabitatType.Brush
                or WildlifeHabitatType.Lair,
            _ => true
        };
    }

    private void LoadSceneMarkers(IWildlifeGridPort grid)
    {
        IReadOnlyList<WildlifeHabitatPatch> markers =
            world.GetMarkerPatches(grid, persistentIds);
        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i] == null)
            {
                continue;
            }

            WildlifeHabitatPatch patch = markers[i];
            if (IsPatchOnUsableExterior(grid, patch))
            {
                patches.Add(patch);
            }
        }
    }

    private void GenerateDefaultPatches(IWildlifeGridPort grid)
    {
        List<Vector2Int> cells = grid.GetCells()
            .Where(cell => IsHabitatCell(grid, cell))
            .Select(cell => cell.Position)
            .OrderBy(position => position.x)
            .ToList();
        if (cells.Count == 0)
        {
            return;
        }

        AddDefaultPatch(grid, cells, 0.12f, WildlifeHabitatType.Brush, 4, 5f, 0.018f, 0.08f);
        AddDefaultPatch(grid, cells, 0.24f, WildlifeHabitatType.Grass, 5, 10f, 0.04f, 0.02f);
        if (!AddWorldWaterPatches(grid))
        {
            AddDefaultPatch(grid, cells, 0.42f, WildlifeHabitatType.Water, 3, 8f, 0.025f, 0.02f);
        }
        AddDefaultPatch(grid, cells, 0.58f, WildlifeHabitatType.Grass, 5, 10f, 0.04f, 0.02f);
        AddDefaultPatch(grid, cells, 0.72f, WildlifeHabitatType.Burrow, 3, 4f, 0.012f, 0.04f);
        AddDefaultPatch(grid, cells, 0.88f, WildlifeHabitatType.Lair, 4, 5f, 0.015f, 0.22f);
    }

    private void EnsureForagePatches(IWildlifeGridPort grid)
    {
        int forageCount = patches.Count(patch =>
            patch != null
            && patch.HabitatType is WildlifeHabitatType.Grass or WildlifeHabitatType.Brush);
        if (forageCount >= 2)
        {
            return;
        }

        List<Vector2Int> cells = grid.GetCells()
            .Where(cell => IsHabitatCell(grid, cell))
            .Select(cell => cell.Position)
            .OrderBy(position => position.x)
            .ThenBy(position => position.y)
            .ToList();
        if (cells.Count == 0)
        {
            return;
        }

        if (forageCount == 0)
        {
            AddDefaultPatch(
                grid,
                cells,
                0.25f,
                WildlifeHabitatType.Brush,
                4,
                5f,
                0.018f,
                0.08f);
            forageCount++;
        }

        if (forageCount < 2)
        {
            AddDefaultPatch(
                grid,
                cells,
                0.62f,
                WildlifeHabitatType.Grass,
                5,
                10f,
                0.04f,
                0.02f);
        }
    }

    private void ReplaceWaterPatchesWithWorldSources(IWildlifeGridPort grid)
    {
        if (world.GetWaterSources().Count == 0)
        {
            return;
        }

        patches.RemoveAll(patch => patch.HabitatType == WildlifeHabitatType.Water);
        AddWorldWaterPatches(grid);
    }

    private bool AddWorldWaterPatches(IWildlifeGridPort grid)
    {
        bool added = false;
        foreach (WildlifeWaterSourceSnapshot source in world.GetWaterSources())
        {
            WildlifeHabitatPatch patch = new WildlifeHabitatPatch(
                BuildWaterPatchId(source.SourceId),
                WildlifeHabitatType.Water,
                source.Position,
                source.DeepWater ? 2 : 3,
                source.Capacity,
                source.Remaining,
                0f,
                source.Foul ? 0.25f : 0.05f,
                linkedWaterSourceId: source.SourceId);
            if (IsPatchOnUsableExterior(grid, patch))
            {
                patches.Add(patch);
                added = true;
            }
        }

        return added;
    }

    private void AddDefaultPatch(
        IWildlifeGridPort grid,
        List<Vector2Int> cells,
        float normalizedIndex,
        WildlifeHabitatType type,
        int radius,
        float capacity,
        float regen,
        float danger)
    {
        if (cells == null || cells.Count == 0)
        {
            return;
        }

        int index = Mathf.Clamp(Mathf.RoundToInt((cells.Count - 1) * normalizedIndex), 0, cells.Count - 1);
        Vector2Int center = cells[index];
        WildlifeHabitatPatch patch = new WildlifeHabitatPatch(
            persistentIds.NewWildlifeHabitatPatchId().Value,
            type,
            center,
            radius,
            capacity,
            capacity * Mathf.Lerp(0.65f, 1f, randomStream.NextFloat()),
            regen,
            danger);
        if (IsPatchOnUsableExterior(grid, patch))
        {
            patches.Add(patch);
        }
    }

    private int CalculateDesiredWildlifeCount()
    {
        float food = AverageResource(WildlifeHabitatType.Grass, WildlifeHabitatType.Brush);
        float water = AverageResource(WildlifeHabitatType.Water);
        float pressurePenalty = Mathf.Clamp01((recentHuntPressure * 0.7f) + (recentPredationPressure * 0.35f));
        int desired = Mathf.RoundToInt(DefaultDesiredWildlifeCount * Mathf.Lerp(0.35f, 1.25f, (food + water) * 0.5f));
        desired -= Mathf.RoundToInt(pressurePenalty * 3f);
        return Mathf.Clamp(desired, 2, 14);
    }

    private float AverageResource(params WildlifeHabitatType[] habitatTypes)
    {
        if (habitatTypes == null || habitatTypes.Length == 0)
        {
            return 0f;
        }

        List<WildlifeHabitatPatch> matching = patches
            .Where(patch => habitatTypes.Contains(patch.HabitatType))
            .ToList();
        return matching.Count == 0
            ? 0f
            : Mathf.Clamp01(matching.Average(patch => patch.Resource01));
    }

    private static float ScoreRespawnWeight(
        WildlifeSpeciesDefinition species,
        float food,
        float water,
        Season? season = null)
    {
        if (species == null)
        {
            return 0f;
        }

        float foodFit = species.Diet == WildlifeDietType.Carnivore || species.Diet == WildlifeDietType.Scavenger
            ? Mathf.Lerp(0.35f, 1f, food)
            : food;
        float waterFit = Mathf.Lerp(0.35f, 1f, water);
        float predatorPenalty = species.Diet == WildlifeDietType.Carnivore ? 0.75f : 1f;
        float breedingMultiplier = season.HasValue
            && season.Value == species.BreedingSeason
                ? 1.65f
                : 1f;
        return Mathf.Max(0f, species.SpawnWeight)
            * foodFit
            * waterFit
            * predatorPenalty
            * breedingMultiplier;
    }

    private bool TryFindPatchTarget(
        IWildlifeAnimalPort actor,
        IWildlifeGridPort grid,
        Func<WildlifeHabitatPatch, bool> predicate,
        out Vector2Int target)
    {
        target = actor != null ? actor.GridPosition : Vector2Int.zero;
        WildlifeHabitatPatch bestPatch = null;
        float bestScore = float.NegativeInfinity;
        foreach (WildlifeHabitatPatch patch in patches)
        {
            if (patch == null || predicate == null || !predicate(patch))
            {
                continue;
            }

            if (!TryFindSurfaceNear(grid, actor, patch.Center, out Vector2Int patchTarget))
            {
                continue;
            }

            int distance = Mathf.Abs(actor.GridPosition.x - patchTarget.x)
                + Mathf.Abs(actor.GridPosition.y - patchTarget.y);
            float score = patch.Resource01 * 10f
                - distance * 0.35f
                - patch.Danger * (actor.Species != null && actor.Species.IsPredator ? 1.2f : 4f);
            if (patch.IsPreferredBy(actor.Species))
            {
                score += 2f;
            }

            if (bestPatch == null || score > bestScore)
            {
                bestPatch = patch;
                target = patchTarget;
                bestScore = score;
            }
        }

        return bestPatch != null;
    }

    private bool TryFindSurfaceNear(
        IWildlifeGridPort grid,
        IWildlifeAnimalPort actor,
        Vector2Int center,
        out Vector2Int target)
    {
        target = center;
        if (grid == null || actor == null)
        {
            return false;
        }

        float bestScore = float.NegativeInfinity;
        bool found = false;
        int maxRadius = Mathf.Max(1, actor.Species != null ? Mathf.CeilToInt(actor.Species.TerritoryRadius) : 6);
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

                    Vector2Int candidate = center + new Vector2Int(dx, dy);
                    if (!CanAnimalUseCell(grid, actor, candidate))
                    {
                        continue;
                    }

                    int distanceFromActor = Mathf.Abs(candidate.x - actor.GridPosition.x)
                        + Mathf.Abs(candidate.y - actor.GridPosition.y);
                    float score = -distanceFromActor - Mathf.Abs(dx) * 0.1f - Mathf.Abs(dy) * 0.1f;
                    if (found && score <= bestScore)
                    {
                        continue;
                    }

                    found = true;
                    bestScore = score;
                    target = candidate;
                }
            }

            if (found)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindCarcassTarget(
        IWildlifeAnimalPort actor,
        IWildlifeGridPort grid,
        IReadOnlyList<WildlifeCarcassStackSnapshot> itemStacks,
        out Vector2Int target)
    {
        target = actor.GridPosition;
        if (itemStacks == null)
        {
            return false;
        }

        WildlifeCarcassStackSnapshot? best = null;
        float bestScore = float.NegativeInfinity;
        foreach (WildlifeCarcassStackSnapshot stack in itemStacks)
        {
            if (stack.Quantity <= 0
                || stack.Forbidden
                || !WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(stack.ItemId, out _)
                || !CanAnimalUseCell(grid, actor, stack.Position))
            {
                continue;
            }

            int distance = Mathf.Abs(stack.Position.x - actor.GridPosition.x)
                + Mathf.Abs(stack.Position.y - actor.GridPosition.y);
            float score = 12f - distance + actor.Hunger * 8f;
            if (best == null || score > bestScore)
            {
                best = stack;
                bestScore = score;
                target = stack.Position;
            }
        }

        return best != null;
    }

    private bool TryFindPreyTarget(
        IWildlifeAnimalPort predator,
        IWildlifeGridPort grid,
        IReadOnlyList<IWildlifeAnimalPort> wildlife,
        out Vector2Int target)
    {
        target = predator.GridPosition;
        if (wildlife == null)
        {
            return false;
        }

        IWildlifeAnimalPort best = null;
        Vector2Int bestStand = predator.GridPosition;
        float bestScore = float.NegativeInfinity;
        foreach (IWildlifeAnimalPort prey in wildlife)
        {
            if (prey == null
                || string.Equals(
                    prey.WildlifeId,
                    predator.WildlifeId,
                    StringComparison.Ordinal)
                || !prey.IsAlive
                || prey.Species == null
                || !predator.Species.Hunts(prey.SpeciesId)
                || prey.Species.PredatorSpeciesIds.Count > 0
                    && !prey.Species.PredatorSpeciesIds.Contains(
                        predator.SpeciesId)
                || prey.Species.Diet == WildlifeDietType.Carnivore
                || prey.MaxHealth > predator.MaxHealth + 10
                || !TryFindAdjacentOpenCell(grid, predator, prey.GridPosition, out Vector2Int stand))
            {
                continue;
            }

            int distance = Mathf.Abs(prey.GridPosition.x - predator.GridPosition.x)
                + Mathf.Abs(prey.GridPosition.y - predator.GridPosition.y);
            if (distance > 12)
            {
                continue;
            }

            float weakness = prey.MaxHealth > 0 ? 1f - (prey.CurrentHealth / (float)prey.MaxHealth) : 0f;
            float score = 14f
                - distance
                + weakness * 8f
                + predator.Hunger * 8f
                - (prey.IsDangerous ? 6f : 0f);
            if (best == null || score > bestScore)
            {
                best = prey;
                bestStand = stand;
                bestScore = score;
            }
        }

        target = bestStand;
        return best != null;
    }

    private bool TryFindAdjacentOpenCell(
        IWildlifeGridPort grid,
        IWildlifeAnimalPort actor,
        Vector2Int center,
        out Vector2Int target)
    {
        Vector2Int[] candidates =
        {
            new Vector2Int(center.x - 1, center.y),
            new Vector2Int(center.x + 1, center.y),
            new Vector2Int(center.x, center.y - 1),
            new Vector2Int(center.x, center.y + 1)
        };
        target = actor.GridPosition;
        bool found = false;
        int bestDistance = int.MaxValue;
        foreach (Vector2Int candidate in candidates)
        {
            if (!CanAnimalUseCell(grid, actor, candidate))
            {
                continue;
            }

            int distance = Mathf.Abs(candidate.x - actor.GridPosition.x)
                + Mathf.Abs(candidate.y - actor.GridPosition.y);
            if (found && distance >= bestDistance)
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            target = candidate;
        }

        return found;
    }

    private Vector2Int ChooseFleeSurfaceTarget(
        IWildlifeAnimalPort actor,
        IWildlifeGridPort grid)
    {
        Vector2Int threat = actor.HasLastThreatPosition
            ? actor.LastThreatPosition
            : actor.GridPosition;
        Vector2Int best = actor.GridPosition;
        float bestScore = float.NegativeInfinity;
        foreach (IWildlifeGridCellPort cell in grid.GetCells())
        {
            if (!CanAnimalUseCell(grid, actor, cell.Position))
            {
                continue;
            }

            int threatDistance = Mathf.Abs(cell.Position.x - threat.x) + Mathf.Abs(cell.Position.y - threat.y);
            int actorDistance = Mathf.Abs(cell.Position.x - actor.GridPosition.x) + Mathf.Abs(cell.Position.y - actor.GridPosition.y);
            if (actorDistance > 10)
            {
                continue;
            }

            float score = threatDistance * 3f - actorDistance;
            if (score > bestScore)
            {
                best = cell.Position;
                bestScore = score;
            }
        }

        return best;
    }

    private bool TryFindMapExitTarget(
        IWildlifeAnimalPort actor,
        IWildlifeGridPort grid,
        out Vector2Int target)
    {
        target = actor.GridPosition;
        Vector2Int left = new Vector2Int(0, actor.GridPosition.y);
        Vector2Int right = new Vector2Int(grid.Width - 1, actor.GridPosition.y);
        Vector2Int preferred = actor.GridPosition.x < grid.Width * 0.5f ? left : right;
        if (CanAnimalUseCell(grid, actor, preferred))
        {
            target = preferred;
            return true;
        }

        return TryFindSurfaceNear(grid, actor, preferred, out target);
    }

    private static bool CanForage(WildlifeSpeciesDefinition species)
    {
        return species == null
            || species.Diet == WildlifeDietType.Herbivore
            || species.Diet == WildlifeDietType.Omnivore;
    }

    private static bool CanForageFromPatch(WildlifeSpeciesDefinition species, WildlifeHabitatPatch patch)
    {
        if (patch == null || patch.IsDepleted)
        {
            return false;
        }

        return CanForage(species)
            && (patch.HabitatType == WildlifeHabitatType.Grass || patch.HabitatType == WildlifeHabitatType.Brush);
    }

    private static bool CanAnimalUseCell(
        IWildlifeGridPort grid,
        IWildlifeAnimalPort actor,
        Vector2Int position)
    {
        if (grid == null || actor == null || !grid.IsValidGridPos(position) || !grid.IsWalkable(position))
        {
            return false;
        }

        IWildlifeGridCellPort cell = grid.GetGridCell(position);
        if (cell == null
            || cell.AreaType == WildlifeGridAreaType.BlockedExterior
            || cell.HasWildlifeOccupant)
        {
            return false;
        }

        if (cell.AreaType == WildlifeGridAreaType.ExteriorPath
            && !cell.IsOutdoorSurface)
        {
            return false;
        }

        return actor.CanEnterDungeon
            || cell.AreaType != WildlifeGridAreaType.DungeonInterior;
    }

    private static bool IsHabitatCell(
        IWildlifeGridPort grid,
        IWildlifeGridCellPort cell)
    {
        return cell != null
            && grid != null
            && cell.AreaType == WildlifeGridAreaType.ExteriorPath
            && cell.IsWalkable
            && cell.IsOutdoorSurface;
    }

    private static bool IsPatchOnUsableExterior(
        IWildlifeGridPort grid,
        WildlifeHabitatPatch patch)
    {
        if (grid == null || patch == null)
        {
            return false;
        }

        return grid.GetCells().Any(cell => IsHabitatCell(grid, cell) && patch.Contains(cell.Position));
    }

    private static string BuildWaterPatchId(string sourceId)
    {
        string normalized = sourceId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException(
                "World water source requires a persistent ID before wildlife habitat projection.");
        }

        return "wildlife-habitat:water:" + normalized;
    }

}
