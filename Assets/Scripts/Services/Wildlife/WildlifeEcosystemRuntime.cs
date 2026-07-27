using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class WildlifeHabitatPatch
{
    private readonly List<string> preferredSpeciesTags;

    public WildlifeHabitatPatch(
        string patchId,
        WildlifeHabitatType habitatType,
        Vector2Int center,
        int radius,
        float resourceCapacity,
        float currentResource,
        float regenPerSecond,
        float danger,
        IEnumerable<string> preferredSpeciesTags = null,
        string linkedWaterSourceId = "")
    {
        PatchId = string.IsNullOrWhiteSpace(patchId)
            ? "habitat:" + Guid.NewGuid().ToString("N")
            : patchId.Trim();
        HabitatType = habitatType;
        Center = center;
        Radius = Mathf.Clamp(radius, 0, 12);
        ResourceCapacity = Mathf.Max(0.1f, resourceCapacity);
        CurrentResource = Mathf.Clamp(currentResource, 0f, ResourceCapacity);
        RegenPerSecond = Mathf.Max(0f, regenPerSecond);
        Danger = Mathf.Clamp01(danger);
        LinkedWaterSourceId = linkedWaterSourceId ?? string.Empty;
        this.preferredSpeciesTags = (preferredSpeciesTags ?? Enumerable.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public string PatchId { get; }
    public WildlifeHabitatType HabitatType { get; }
    public Vector2Int Center { get; }
    public int Radius { get; }
    public float ResourceCapacity { get; private set; }
    public float CurrentResource { get; private set; }
    public float RegenPerSecond { get; }
    public float Danger { get; }
    public string LinkedWaterSourceId { get; }
    public IReadOnlyList<string> PreferredSpeciesTags => preferredSpeciesTags;
    public float Resource01 => Mathf.Clamp01(CurrentResource / Mathf.Max(0.1f, ResourceCapacity));
    public bool IsDepleted => CurrentResource <= ResourceCapacity * 0.06f;

    public bool Contains(Vector2Int position)
    {
        return Mathf.Abs(position.x - Center.x) + Mathf.Abs(position.y - Center.y) <= Radius;
    }

    public bool IsPreferredBy(WildlifeSpeciesDefinition species)
    {
        if (species == null || preferredSpeciesTags.Count == 0)
        {
            return true;
        }

        return preferredSpeciesTags.Any(tag =>
            string.Equals(tag, species.SpeciesId, StringComparison.Ordinal)
            || string.Equals(tag, species.DisplayName, StringComparison.Ordinal));
    }

    public void Tick(float deltaTime)
    {
        if (RegenPerSecond <= 0f || CurrentResource >= ResourceCapacity)
        {
            return;
        }

        CurrentResource = Mathf.Min(ResourceCapacity, CurrentResource + RegenPerSecond * Mathf.Max(0f, deltaTime));
    }

    public float Consume(float amount)
    {
        float consumed = Mathf.Min(Mathf.Max(0f, amount), CurrentResource);
        CurrentResource -= consumed;
        return consumed;
    }

    public void SynchronizeResource(float capacity, float current)
    {
        ResourceCapacity = Mathf.Max(0.1f, capacity);
        CurrentResource = Mathf.Clamp(current, 0f, ResourceCapacity);
    }

    public DungeonWildlifeEcosystemSaveData CaptureStandalone()
    {
        DungeonWildlifeEcosystemSaveData data = new DungeonWildlifeEcosystemSaveData();
        data.patches.Add(Capture());
        return data;
    }

    public WildlifeHabitatPatchSaveData Capture()
    {
        return new WildlifeHabitatPatchSaveData
        {
            patchId = PatchId,
            linkedWaterSourceId = LinkedWaterSourceId,
            habitatType = HabitatType,
            gridX = Center.x,
            gridY = Center.y,
            radius = Radius,
            resourceCapacity = ResourceCapacity,
            currentResource = CurrentResource,
            regenPerSecond = RegenPerSecond,
            danger = Danger,
            preferredSpeciesTags = preferredSpeciesTags.ToList()
        };
    }

    public static WildlifeHabitatPatch FromSave(WildlifeHabitatPatchSaveData saveData)
    {
        if (saveData == null)
        {
            return null;
        }

        return new WildlifeHabitatPatch(
            saveData.patchId,
            saveData.habitatType,
            new Vector2Int(saveData.gridX, saveData.gridY),
            saveData.radius,
            saveData.resourceCapacity,
            saveData.currentResource,
            saveData.regenPerSecond,
            saveData.danger,
            saveData.preferredSpeciesTags,
            saveData.linkedWaterSourceId);
    }
}

public interface IWildlifeHabitatMarkerQuery
{
    int Version { get; }
    IReadOnlyList<WildlifeHabitatMarker> GetMarkers();
}

public interface IWildlifeHabitatMarkerRegistry : IWildlifeHabitatMarkerQuery
{
    bool Register(WildlifeHabitatMarker marker);
    bool Unregister(WildlifeHabitatMarker marker);
}

[DisallowMultipleComponent]
public sealed class WildlifeHabitatMarker : MonoBehaviour
{
    [SerializeField] private WildlifeHabitatType habitatType = WildlifeHabitatType.Grass;
    [SerializeField, Min(0)] private int radius = 4;
    [SerializeField, Min(0.1f)] private float resourceCapacity = 8f;
    [SerializeField, Min(0f)] private float regenPerSecond = 0.025f;
    [SerializeField, Range(0f, 1f)] private float danger;
    [SerializeField] private List<string> preferredSpeciesTags = new List<string>();

    public WildlifeHabitatType HabitatType => habitatType;

    public WildlifeHabitatPatch ToPatch(Grid grid, int sequence)
    {
        Vector2Int center = grid != null
            ? grid.GetXY(transform.position)
            : Vector2Int.zero;
        float capacity = resourceCapacity;
        float regen = regenPerSecond;
        if (habitatType is WildlifeHabitatType.Burrow or WildlifeHabitatType.Brush or WildlifeHabitatType.Lair)
        {
            capacity = Mathf.Max(1f, capacity);
            regen = Mathf.Max(0.005f, regen);
        }

        return new WildlifeHabitatPatch(
            "marker:" + gameObject.GetInstanceID() + ":" + sequence,
            habitatType,
            center,
            radius,
            capacity,
            capacity,
            regen,
            danger,
            preferredSpeciesTags);
    }
}

public sealed class WildlifeEcosystemRuntime :
    IWildlifeEcosystemRuntime,
    IInitializable,
    ITickable,
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

    private Sprite overlaySprite;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldWaterQuery worldWaterQuery;
    private readonly IWildlifeHabitatMarkerQuery habitatMarkerQuery;
    private readonly IGameClock gameClock;
    private readonly IRandomStream randomStream;
    private readonly List<WildlifeHabitatPatch> patches = new List<WildlifeHabitatPatch>();
    private readonly WildlifeHabitatDecorationRuntime decorationRuntime;
    private readonly Dictionary<string, float> speciesRespawnAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly List<SpriteRenderer> overlayRenderers = new List<SpriteRenderer>();

    private DungeonWildlifeEcosystemSaveData pendingSaveData;
    private Grid initializedGrid;
    private GameObject overlayRoot;
    private float nextPatchTickAt;
    private float nextOverlayRefreshAt;
    private float nextGlobalRespawnAt;
    private float recentHuntPressure;
    private float recentPredationPressure;
    private int generatedPatchSequence;
    private bool initialized;
    private bool overlayEnabled;

    public WildlifeEcosystemRuntime(
        IGridSystemProvider gridSystemProvider,
        IWorldWaterQuery worldWaterQuery,
        IWildlifeHabitatMarkerQuery habitatMarkerQuery,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IResourcesAssetLoader resourcesAssetLoader = null)
    {
        this.gridSystemProvider = gridSystemProvider;
        this.worldWaterQuery = worldWaterQuery;
        this.habitatMarkerQuery = habitatMarkerQuery
            ?? throw new ArgumentNullException(nameof(habitatMarkerQuery));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        randomStream = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("wildlife-ecosystem");
        decorationRuntime = new WildlifeHabitatDecorationRuntime(resourcesAssetLoader);
    }

    public bool OverlayEnabled => overlayEnabled;
    public IReadOnlyList<WildlifeHabitatPatch> Patches => patches;
    public WildlifeHabitatDecorationRuntime DecorationRuntime => decorationRuntime;

    public void Initialize()
    {
    }

    public void Dispose()
    {
        ClearOverlay();
        decorationRuntime.Dispose();
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
        if (gridSystemProvider == null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        EnsureInitialized(grid);
        float now = gameClock.Time;
        if (now >= nextPatchTickAt)
        {
            float delta = nextPatchTickAt <= 0f ? PatchTickInterval : Mathf.Max(0f, now - (nextPatchTickAt - PatchTickInterval));
            nextPatchTickAt = now + PatchTickInterval;
            TickPatches(delta);
        }

        recentHuntPressure = Mathf.Max(0f, recentHuntPressure - gameClock.DeltaTime / 180f);
        recentPredationPressure = Mathf.Max(0f, recentPredationPressure - gameClock.DeltaTime / 180f);

        if (overlayEnabled && now >= nextOverlayRefreshAt)
        {
            nextOverlayRefreshAt = now + OverlayRefreshInterval;
            RefreshOverlay(grid);
        }
    }

    public void EnsureInitialized(Grid grid)
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
        if (pendingSaveData != null && pendingSaveData.patches != null && pendingSaveData.patches.Count > 0)
        {
            foreach (WildlifeHabitatPatchSaveData entry in pendingSaveData.patches)
            {
                WildlifeHabitatPatch patch = WildlifeHabitatPatch.FromSave(entry);
                if (patch != null && IsPatchOnUsableExterior(grid, patch))
                {
                    patches.Add(patch);
                }
            }
        }
        else
        {
            LoadSceneMarkers(grid);
        }

        if (patches.Count == 0)
        {
            GenerateDefaultPatches(grid);
        }
        else
        {
            ReplaceWaterPatchesWithWorldSources(grid);
        }

        ApplyPendingRespawns();
        if (Application.isPlaying)
        {
            decorationRuntime.Rebuild(grid, patches);
        }
        if (overlayEnabled)
        {
            RefreshOverlay(grid);
        }
    }

    public void Restore(DungeonWildlifeEcosystemSaveData saveData)
    {
        DungeonWildlifeEcosystemSaveData source = saveData ?? new DungeonWildlifeEcosystemSaveData();
        pendingSaveData = source;
        recentHuntPressure = Mathf.Max(0f, source.recentHuntPressure);
        recentPredationPressure = Mathf.Max(0f, source.recentPredationPressure);
        nextGlobalRespawnAt = gameClock.Time + Mathf.Max(0f, source.globalRespawnRemainingSeconds);
        speciesRespawnAt.Clear();
        foreach (WildlifeSpeciesRespawnSaveData entry in source.speciesRespawns ?? Enumerable.Empty<WildlifeSpeciesRespawnSaveData>())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.speciesId))
            {
                continue;
            }

            speciesRespawnAt[entry.speciesId.Trim()] = gameClock.Time + Mathf.Max(0f, entry.remainingSeconds);
        }

        initialized = false;
        initializedGrid = null;
        ClearOverlay();
        decorationRuntime.Clear();
    }

    public DungeonWildlifeEcosystemSaveData Capture()
    {
        return new DungeonWildlifeEcosystemSaveData
        {
            version = DungeonWildlifeEcosystemSaveData.CurrentVersion,
            recentHuntPressure = Mathf.Max(0f, recentHuntPressure),
            recentPredationPressure = Mathf.Max(0f, recentPredationPressure),
            globalRespawnRemainingSeconds = Mathf.Max(0f, nextGlobalRespawnAt - gameClock.Time),
            speciesRespawns = speciesRespawnAt
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .Select(pair => new WildlifeSpeciesRespawnSaveData
                {
                    speciesId = pair.Key,
                    remainingSeconds = Mathf.Max(0f, pair.Value - gameClock.Time)
                })
                .ToList(),
            patches = patches.Select(patch => patch.Capture()).ToList()
        };
    }

    public WildlifeEcosystemOverview GetOverview(IReadOnlyList<WildlifeActor> wildlife)
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
            Mathf.Max(0f, nextGlobalRespawnAt - gameClock.Time));
    }

    public void SetOverlayEnabled(bool enabled)
    {
        overlayEnabled = enabled;
        if (!overlayEnabled)
        {
            ClearOverlay();
            return;
        }

        if (gridSystemProvider != null && gridSystemProvider.TryGetGrid(out Grid grid))
        {
            EnsureInitialized(grid);
            RefreshOverlay(grid);
        }
    }

    public void TickAnimal(WildlifeActor actor, Grid grid, float deltaTime)
    {
        if (actor == null || !actor.IsAlive || grid == null)
        {
            return;
        }

        if (actor.State is WildlifeState.Hunted or WildlifeState.Fleeing or WildlifeState.Retaliating)
        {
            return;
        }

        EnsureInitialized(grid);
        WildlifeHabitatPatch currentPatch = patches
            .Where(patch => patch.Contains(actor.GridPosition) && PatchMatchesIntent(patch, actor.Intent))
            .OrderBy(patch => Mathf.Abs(patch.Center.x - actor.GridPosition.x) + Mathf.Abs(patch.Center.y - actor.GridPosition.y))
            .FirstOrDefault();
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
                        && worldWaterQuery != null
                        && worldWaterQuery.TryDrink(
                            currentPatch.LinkedWaterSourceId,
                            requested,
                            out _,
                            out float sharedConsumed))
                    {
                        consumed = sharedConsumed;
                        if (worldWaterQuery.TryGetSource(
                            currentPatch.LinkedWaterSourceId,
                            out WorldWaterSourceSnapshot sharedSource))
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
                    decorationRuntime.RefreshPatch(currentPatch);
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
        WildlifeActor actor,
        Grid grid,
        IReadOnlyList<WildlifeActor> wildlife,
        IReadOnlyList<WorldItemStackSnapshot> itemStacks,
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
                && (!speciesRespawnAt.TryGetValue(definition.SpeciesId, out float speciesAt) || now >= speciesAt))
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        float totalWeight = candidates.Sum(candidate => ScoreRespawnWeight(candidate, food, water));
        if (totalWeight <= 0f)
        {
            return false;
        }

        float roll = randomStream.NextFloat() * totalWeight;
        foreach (WildlifeSpeciesDefinition candidate in candidates)
        {
            roll -= ScoreRespawnWeight(candidate, food, water);
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

    public void NotifyWildlifeKilled(WildlifeActor actor, bool byHunt)
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
            nextGlobalRespawnAt = Mathf.Max(nextGlobalRespawnAt, now + GlobalRespawnCooldownSeconds);
        }
        else
        {
            recentPredationPressure = Mathf.Clamp01(recentPredationPressure + 0.18f);
            speciesRespawnAt[actor.SpeciesId] = now + NaturalRespawnCooldownSeconds;
        }
    }

    public bool ShouldRemoveLeavingAnimal(WildlifeActor actor, Grid grid)
    {
        if (actor == null || grid == null || !actor.IsAlive)
        {
            return false;
        }

        if (actor.State != WildlifeState.Leaving && actor.Intent != WildlifeIntent.LeaveMap)
        {
            return false;
        }

        return actor.GridPosition.x <= 0 || actor.GridPosition.x >= grid.width - 1;
    }

    private void TickPatches(float deltaTime)
    {
        foreach (WildlifeHabitatPatch patch in patches)
        {
            if (!string.IsNullOrWhiteSpace(patch.LinkedWaterSourceId)
                && worldWaterQuery != null
                && worldWaterQuery.TryGetSource(patch.LinkedWaterSourceId, out WorldWaterSourceSnapshot source))
            {
                patch.SynchronizeResource(source.Capacity, source.Remaining);
            }
            else
            {
                patch.Tick(deltaTime);
            }
        }

        decorationRuntime.Refresh(patches);
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

    private void LoadSceneMarkers(Grid grid)
    {
        IReadOnlyList<WildlifeHabitatMarker> markers =
            habitatMarkerQuery.GetMarkers();
        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i] == null)
            {
                continue;
            }

            WildlifeHabitatPatch patch = markers[i].ToPatch(grid, generatedPatchSequence++);
            if (IsPatchOnUsableExterior(grid, patch))
            {
                patches.Add(patch);
            }
        }
    }

    private void GenerateDefaultPatches(Grid grid)
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

    private void ReplaceWaterPatchesWithWorldSources(Grid grid)
    {
        if (worldWaterQuery == null || worldWaterQuery.GetAllSources().Count == 0)
        {
            return;
        }

        patches.RemoveAll(patch => patch.HabitatType == WildlifeHabitatType.Water);
        AddWorldWaterPatches(grid);
    }

    private bool AddWorldWaterPatches(Grid grid)
    {
        if (worldWaterQuery == null)
        {
            return false;
        }

        bool added = false;
        foreach (WorldWaterSourceSnapshot source in worldWaterQuery.GetAllSources())
        {
            WildlifeHabitatPatch patch = new WildlifeHabitatPatch(
                "water:" + source.SourceId,
                WildlifeHabitatType.Water,
                source.Position,
                source.TerrainType == GridCellTerrainType.DeepWater ? 2 : 3,
                source.Capacity,
                source.Remaining,
                0f,
                source.Quality == WorldWaterQuality.Foul ? 0.25f : 0.05f,
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
        Grid grid,
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
            "auto:" + type + ":" + generatedPatchSequence++,
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

    private void ApplyPendingRespawns()
    {
        if (pendingSaveData == null)
        {
            return;
        }

        if (pendingSaveData.version != DungeonWildlifeEcosystemSaveData.CurrentVersion)
        {
            pendingSaveData.version = DungeonWildlifeEcosystemSaveData.CurrentVersion;
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

    private static float ScoreRespawnWeight(WildlifeSpeciesDefinition species, float food, float water)
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
        return Mathf.Max(0f, species.SpawnWeight) * foodFit * waterFit * predatorPenalty;
    }

    private bool TryFindPatchTarget(
        WildlifeActor actor,
        Grid grid,
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

    private bool TryFindSurfaceNear(Grid grid, WildlifeActor actor, Vector2Int center, out Vector2Int target)
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
        WildlifeActor actor,
        Grid grid,
        IReadOnlyList<WorldItemStackSnapshot> itemStacks,
        out Vector2Int target)
    {
        target = actor.GridPosition;
        if (itemStacks == null)
        {
            return false;
        }

        WorldItemStackSnapshot best = null;
        float bestScore = float.NegativeInfinity;
        foreach (WorldItemStackSnapshot stack in itemStacks)
        {
            if (stack == null
                || stack.Quantity <= 0
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
        WildlifeActor predator,
        Grid grid,
        IReadOnlyList<WildlifeActor> wildlife,
        out Vector2Int target)
    {
        target = predator.GridPosition;
        if (wildlife == null)
        {
            return false;
        }

        WildlifeActor best = null;
        Vector2Int bestStand = predator.GridPosition;
        float bestScore = float.NegativeInfinity;
        foreach (WildlifeActor prey in wildlife)
        {
            if (prey == null
                || prey == predator
                || !prey.IsAlive
                || prey.Species == null
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

    private bool TryFindAdjacentOpenCell(Grid grid, WildlifeActor actor, Vector2Int center, out Vector2Int target)
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

    private Vector2Int ChooseFleeSurfaceTarget(WildlifeActor actor, Grid grid)
    {
        Vector2Int threat = actor.HasLastThreatPosition
            ? actor.LastThreatPosition
            : actor.GridPosition;
        Vector2Int best = actor.GridPosition;
        float bestScore = float.NegativeInfinity;
        foreach (GridCell cell in grid.GetCells())
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

    private bool TryFindMapExitTarget(WildlifeActor actor, Grid grid, out Vector2Int target)
    {
        target = actor.GridPosition;
        Vector2Int left = new Vector2Int(0, actor.GridPosition.y);
        Vector2Int right = new Vector2Int(grid.width - 1, actor.GridPosition.y);
        Vector2Int preferred = actor.GridPosition.x < grid.width * 0.5f ? left : right;
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

    private static bool CanAnimalUseCell(Grid grid, WildlifeActor actor, Vector2Int position)
    {
        if (grid == null || actor == null || !grid.IsValidGridPos(position) || !grid.IsWalkable(position))
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(position);
        if (cell == null
            || cell.AreaType == GridCellAreaType.BlockedExterior
            || cell.HasOccupantInLayer(GridLayer.Wildlife))
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.ExteriorPath && !WildlifeRuntime.IsOutdoorSurfaceCell(grid, cell))
        {
            return false;
        }

        return actor.CanEnterDungeon || cell.AreaType != GridCellAreaType.DungeonInterior;
    }

    private static bool IsHabitatCell(Grid grid, GridCell cell)
    {
        return cell != null
            && grid != null
            && cell.AreaType == GridCellAreaType.ExteriorPath
            && grid.IsWalkable(cell.Position)
            && WildlifeRuntime.IsOutdoorSurfaceCell(grid, cell);
    }

    private static bool IsPatchOnUsableExterior(Grid grid, WildlifeHabitatPatch patch)
    {
        if (grid == null || patch == null)
        {
            return false;
        }

        return grid.GetCells().Any(cell => IsHabitatCell(grid, cell) && patch.Contains(cell.Position));
    }

    private void RefreshOverlay(Grid grid)
    {
        if (!overlayEnabled || grid == null)
        {
            ClearOverlay();
            return;
        }

        EnsureOverlayRoot();
        while (overlayRenderers.Count < patches.Count)
        {
            GameObject entry = new GameObject("HabitatPatchOverlay");
            entry.transform.SetParent(overlayRoot.transform, false);
            SpriteRenderer renderer = entry.AddComponent<SpriteRenderer>();
            renderer.sprite = GetOverlaySprite();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 78;
            overlayRenderers.Add(renderer);
        }

        for (int i = 0; i < overlayRenderers.Count; i++)
        {
            SpriteRenderer renderer = overlayRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            bool active = i < patches.Count;
            renderer.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            WildlifeHabitatPatch patch = patches[i];
            Vector3 world = grid.GetWorldPos(patch.Center);
            renderer.transform.position = new Vector3(world.x, world.y + 0.06f, -0.04f);
            renderer.transform.localScale = new Vector3(Mathf.Max(1f, patch.Radius * 2f + 1f), 0.85f, 1f);
            renderer.color = ResolveOverlayColor(patch);
            renderer.gameObject.name = "HabitatPatch_" + patch.HabitatType + "_" + patch.PatchId;
        }
    }

    private void EnsureOverlayRoot()
    {
        if (overlayRoot != null)
        {
            return;
        }

        overlayRoot = new GameObject("WildlifeHabitatOverlay");
        DungeonRuntimeHierarchy.Parent(overlayRoot, DungeonRuntimeHierarchy.Debug);
    }

    private void ClearOverlay()
    {
        for (int i = overlayRenderers.Count - 1; i >= 0; i--)
        {
            SpriteRenderer renderer = overlayRenderers[i];
            if (renderer != null)
            {
                UnityEngine.Object.Destroy(renderer.gameObject);
            }
        }

        overlayRenderers.Clear();
        if (overlayRoot != null)
        {
            UnityEngine.Object.Destroy(overlayRoot);
            overlayRoot = null;
        }
    }

    private static Color ResolveOverlayColor(WildlifeHabitatPatch patch)
    {
        float alpha = Mathf.Lerp(0.12f, 0.28f, patch.Resource01);
        return patch.HabitatType switch
        {
            WildlifeHabitatType.Water => new Color(0.2f, 0.55f, 1f, alpha),
            WildlifeHabitatType.Burrow => new Color(0.85f, 0.58f, 0.18f, alpha),
            WildlifeHabitatType.Brush => new Color(0.12f, 0.72f, 0.48f, alpha),
            WildlifeHabitatType.Lair => new Color(0.9f, 0.16f, 0.12f, alpha),
            _ => new Color(0.18f, 0.85f, 0.28f, alpha)
        };
    }

    private Sprite GetOverlaySprite()
    {
        if (overlaySprite != null)
        {
            return overlaySprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        overlaySprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        overlaySprite.name = "WildlifeHabitatOverlaySprite";
        return overlaySprite;
    }
}
