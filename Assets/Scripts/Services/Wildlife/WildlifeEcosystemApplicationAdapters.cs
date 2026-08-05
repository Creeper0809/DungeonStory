using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class WildlifeEcosystemApplicationPorts :
    IWildlifeEcosystemWorldPort,
    IWildlifeEcosystemPresentationPort
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldWaterQuery worldWaterQuery;
    private readonly IWildlifeHabitatMarkerQuery habitatMarkerQuery;
    private readonly Dictionary<Grid, WildlifeGridPort> grids =
        new Dictionary<Grid, WildlifeGridPort>();
    private readonly WildlifeHabitatDecorationRuntime decorations;
    private readonly WildlifeHabitatOverlay overlay;

    public WildlifeEcosystemApplicationPorts(
        IGridSystemProvider gridSystemProvider,
        IWorldWaterQuery worldWaterQuery,
        IWildlifeHabitatMarkerQuery habitatMarkerQuery,
        IGameContentCatalog content,
        IWildlifeOverlayRootPort overlayRoot)
    {
        this.gridSystemProvider = gridSystemProvider;
        this.worldWaterQuery = worldWaterQuery;
        this.habitatMarkerQuery = habitatMarkerQuery
            ?? throw new ArgumentNullException(nameof(habitatMarkerQuery));
        decorations = new WildlifeHabitatDecorationRuntime(
            content ?? throw new ArgumentNullException(nameof(content)));
        overlay = new WildlifeHabitatOverlay(
            overlayRoot ?? throw new ArgumentNullException(nameof(overlayRoot)));
    }

    public WildlifeHabitatDecorationRuntime DecorationRuntime => decorations;
    public bool OverlayEnabled => overlay.Enabled;

    public bool TryGetGrid(out IWildlifeGridPort grid)
    {
        if (gridSystemProvider == null
            || !gridSystemProvider.TryGetGrid(out Grid concrete)
            || concrete == null)
        {
            grid = null;
            return false;
        }

        grid = WrapGrid(concrete);
        return true;
    }

    public IWildlifeGridPort WrapGrid(Grid grid)
    {
        if (grid == null)
        {
            return null;
        }

        if (!grids.TryGetValue(grid, out WildlifeGridPort wrapped))
        {
            wrapped = new WildlifeGridPort(grid);
            grids.Add(grid, wrapped);
        }
        return wrapped;
    }

    public IReadOnlyList<WildlifeHabitatPatch> GetMarkerPatches(
        IWildlifeGridPort grid,
        IPersistentIdGenerator persistentIds)
    {
        IReadOnlyList<WildlifeHabitatMarker> markers = habitatMarkerQuery.GetMarkers();
        List<WildlifeHabitatPatch> patches = new List<WildlifeHabitatPatch>(markers.Count);
        for (int index = 0; index < markers.Count; index++)
        {
            WildlifeHabitatMarker marker = markers[index];
            if (marker != null)
            {
                patches.Add(marker.ToPatch(
                    grid,
                    persistentIds.NewWildlifeHabitatPatchId()));
            }
        }
        return patches;
    }

    public IReadOnlyList<WildlifeWaterSourceSnapshot> GetWaterSources() =>
        (worldWaterQuery?.GetAllSources() ?? Array.Empty<WorldWaterSourceSnapshot>())
            .Select(ToWaterSnapshot)
            .ToArray();

    public bool TryGetWaterSource(
        string sourceId,
        out WildlifeWaterSourceSnapshot source)
    {
        if (worldWaterQuery != null
            && worldWaterQuery.TryGetSource(
                sourceId,
                out WorldWaterSourceSnapshot concrete))
        {
            source = ToWaterSnapshot(concrete);
            return true;
        }
        source = default;
        return false;
    }

    public bool TryDrinkWater(
        string sourceId,
        float amount,
        out float consumed) =>
        TryDrinkWorldWater(sourceId, amount, out consumed);

    private bool TryDrinkWorldWater(
        string sourceId,
        float amount,
        out float consumed)
    {
        if (worldWaterQuery != null
            && worldWaterQuery.TryDrink(sourceId, amount, out _, out consumed))
        {
            return true;
        }
        consumed = 0f;
        return false;
    }

    public void SetOverlayEnabled(bool enabled) => overlay.SetEnabled(enabled);

    public void Clear()
    {
        overlay.Clear();
        decorations.Clear();
    }

    public void Rebuild(
        IWildlifeGridPort grid,
        IReadOnlyList<WildlifeHabitatPatch> patches)
    {
        if (Application.isPlaying)
        {
            decorations.Rebuild(RequireGrid(grid), patches);
        }
    }

    public void RefreshOverlay(
        IWildlifeGridPort grid,
        IReadOnlyList<WildlifeHabitatPatch> patches) =>
        overlay.Refresh(grid, patches);

    public void RefreshPatches(IReadOnlyList<WildlifeHabitatPatch> patches) =>
        decorations.Refresh(patches);

    public void RefreshPatch(WildlifeHabitatPatch patch) =>
        decorations.RefreshPatch(patch);

    public void Dispose()
    {
        overlay.Dispose();
        decorations.Dispose();
        grids.Clear();
    }

    public IWildlifeAnimalPort WrapAnimal(WildlifeActor actor) =>
        actor == null ? null : new WildlifeAnimalPort(actor);

    public IReadOnlyList<IWildlifeAnimalPort> WrapAnimals(
        IReadOnlyList<WildlifeActor> actors) =>
        actors == null
            ? null
            : actors.Select(WrapAnimal).ToArray();

    public IReadOnlyList<WildlifeCarcassStackSnapshot> WrapItems(
        IReadOnlyList<WorldItemStackSnapshot> items) =>
        items == null
            ? null
            : items.Where(item => item != null)
                .Select(item => new WildlifeCarcassStackSnapshot(
                    item.ItemId,
                    item.Quantity,
                    item.Forbidden,
                    item.Position))
                .ToArray();

    private static WildlifeWaterSourceSnapshot ToWaterSnapshot(
        WorldWaterSourceSnapshot source) => new WildlifeWaterSourceSnapshot(
            source.SourceId,
            source.Position,
            source.TerrainType == GridCellTerrainType.DeepWater,
            source.Quality == WorldWaterQuality.Foul,
            source.Capacity,
            source.Remaining);

    private static Grid RequireGrid(IWildlifeGridPort grid) =>
        grid is WildlifeGridPort wrapped
            ? wrapped.Grid
            : throw new InvalidOperationException(
                "Wildlife ecosystem received a grid from a different adapter.");

        private sealed class WildlifeGridPort : IWildlifeGridPort
    {
        public WildlifeGridPort(Grid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public Grid Grid { get; }
        public int Width => Grid.width;
        public Vector2Int GetCellPosition(Vector3 worldPosition) =>
            Grid.GetXY(worldPosition);
        public Vector3 GetWorldPosition(Vector2Int cellPosition) =>
            Grid.GetWorldPos(cellPosition);
        public bool IsValidGridPos(Vector2Int position) => Grid.IsValidGridPos(position);
        public bool IsWalkable(Vector2Int position) => Grid.IsWalkable(position);
        public IWildlifeGridCellPort GetGridCell(Vector2Int position) =>
            WrapCell(Grid.GetGridCell(position));
        public IReadOnlyList<IWildlifeGridCellPort> GetCells() =>
            Grid.GetCells().Select(WrapCell).ToArray();

        private IWildlifeGridCellPort WrapCell(GridCell cell) =>
            cell == null ? null : new WildlifeGridCellPort(Grid, cell);
    }

    private sealed class WildlifeGridCellPort : IWildlifeGridCellPort
    {
        private readonly Grid grid;
        private readonly GridCell cell;

        public WildlifeGridCellPort(Grid grid, GridCell cell)
        {
            this.grid = grid;
            this.cell = cell;
        }

        public Vector2Int Position => cell.Position;
        public WildlifeGridAreaType AreaType => (WildlifeGridAreaType)cell.AreaType;
        public bool IsWalkable => grid.IsWalkable(cell.Position);
        public bool HasWildlifeOccupant => cell.HasOccupantInLayer(GridLayer.Wildlife);
        public bool IsOutdoorSurface => WildlifeRuntime.IsOutdoorSurfaceCell(grid, cell);
    }

    private sealed class WildlifeAnimalPort : IWildlifeAnimalPort
    {
        private readonly WildlifeActor actor;

        public WildlifeAnimalPort(WildlifeActor actor)
        {
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
        }

        public string WildlifeId => actor.WildlifeId;
        public string SpeciesId => actor.SpeciesId;
        public WildlifeSpeciesDefinition Species => actor.Species;
        public int MaxHealth => actor.MaxHealth;
        public int CurrentHealth => actor.CurrentHealth;
        public WildlifeState State => actor.State;
        public Vector2Int GridPosition => actor.GridPosition;
        public float Fear => actor.Fear;
        public float Hunger => actor.Hunger;
        public float Thirst => actor.Thirst;
        public WildlifeIntent Intent => actor.Intent;
        public Vector2Int TerritoryCenter => actor.TerritoryCenter;
        public bool HasLastThreatPosition => actor.HasLastThreatPosition;
        public Vector2Int LastThreatPosition => actor.LastThreatPosition;
        public float LastThreatAge => actor.LastThreatAge;
        public bool CanEnterDungeon => actor.CanEnterDungeon;
        public bool IsAlive => actor.IsAlive;
        public bool IsDangerous => actor.IsDangerous;
        public void SetIntent(WildlifeIntent intent, string reason) =>
            actor.SetIntent(intent, reason);
        public void ChangeHunger(float delta) => actor.ChangeHunger(delta);
        public void ChangeThirst(float delta) => actor.ChangeThirst(delta);
    }
}

public sealed class WildlifeOverlayRootPort : IWildlifeOverlayRootPort
{
    public void ParentOverlayRoot(GameObject root)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }
        DungeonRuntimeHierarchy.Parent(root, DungeonRuntimeHierarchy.Debug);
    }
}

public sealed class WildlifeEcosystemApplicationAdapter :
    IWildlifeEcosystemRuntime,
    IInitializable,
    ITickable,
    IDisposable
{
    private readonly WildlifeEcosystemRuntime core;
    private readonly WildlifeEcosystemApplicationPorts ports;

    public WildlifeEcosystemApplicationAdapter(
        WildlifeEcosystemRuntime core,
        WildlifeEcosystemApplicationPorts ports)
    {
        this.core = core ?? throw new ArgumentNullException(nameof(core));
        this.ports = ports ?? throw new ArgumentNullException(nameof(ports));
    }

    public bool OverlayEnabled => core.OverlayEnabled;
    public IReadOnlyList<WildlifeHabitatPatch> Patches => core.Patches;
    public WildlifeHabitatDecorationRuntime DecorationRuntime => ports.DecorationRuntime;

    public void Initialize() => core.Initialize();
    public void Tick() => core.Tick();
    public void Dispose() => core.Dispose();
    public DungeonWildlifeEcosystemSaveData Capture() => core.Capture();

    public WildlifeEcosystemOverview GetOverview(
        IReadOnlyList<WildlifeActor> wildlife) =>
        core.GetOverview(ports.WrapAnimals(wildlife));

    public WildlifeEcosystemRestoreCandidate PrepareRestoreCandidate(
        DungeonWildlifeEcosystemSaveData saveData,
        Grid restoreGrid) =>
        core.PrepareRestoreCandidate(saveData, ports.WrapGrid(restoreGrid));

    public void PublishRestoreCandidate(WildlifeEcosystemRestoreCandidate candidate) =>
        core.PublishRestoreCandidate(candidate);

    public WildlifeEcosystemRestoreTransaction ApplyRestoreCandidate(
        WildlifeEcosystemRestoreCandidate candidate) =>
        core.ApplyRestoreCandidate(candidate);

    public void RollbackRestore(WildlifeEcosystemRestoreTransaction transaction) =>
        core.RollbackRestore(transaction);

    public void CompleteRestore(WildlifeEcosystemRestoreTransaction transaction) =>
        core.CompleteRestore(transaction);

    public void SetOverlayEnabled(bool enabled) => core.SetOverlayEnabled(enabled);
    public void EnsureInitialized(Grid grid) => core.EnsureInitialized(ports.WrapGrid(grid));

    public void TickAnimal(WildlifeActor actor, Grid grid, float deltaTime) =>
        core.TickAnimal(ports.WrapAnimal(actor), ports.WrapGrid(grid), deltaTime);

    public bool TryChooseEcologyTarget(
        WildlifeActor actor,
        Grid grid,
        IReadOnlyList<WildlifeActor> wildlife,
        IReadOnlyList<WorldItemStackSnapshot> itemStacks,
        out Vector2Int target,
        out WildlifeIntent intent,
        out string reason) =>
        core.TryChooseEcologyTarget(
            ports.WrapAnimal(actor),
            ports.WrapGrid(grid),
            ports.WrapAnimals(wildlife),
            ports.WrapItems(itemStacks),
            out target,
            out intent,
            out reason);

    public bool TryConsumeRespawnOpportunity(
        float now,
        int aliveCount,
        IReadOnlyList<WildlifeSpeciesDefinition> species,
        out WildlifeSpeciesDefinition selectedSpecies) =>
        core.TryConsumeRespawnOpportunity(
            now,
            aliveCount,
            species,
            out selectedSpecies);

    public void NotifyWildlifeKilled(WildlifeActor actor, bool byHunt) =>
        core.NotifyWildlifeKilled(ports.WrapAnimal(actor), byHunt);

    public bool ShouldRemoveLeavingAnimal(WildlifeActor actor, Grid grid) =>
        core.ShouldRemoveLeavingAnimal(ports.WrapAnimal(actor), ports.WrapGrid(grid));
}
