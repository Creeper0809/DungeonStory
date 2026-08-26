using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public interface IModularFacilityWorldSaveService
{
    ModularFacilityWorldSaveData CreateSnapshot(Grid grid);
    ModularFacilityWorldRestoreReport ValidateRestore(
        Grid grid,
        ModularFacilityWorldSaveData snapshot);
    string ToJson(ModularFacilityWorldSaveData snapshot, bool prettyPrint = false);
    ModularFacilityWorldSaveData FromJson(string json);
    ModularFacilityWorldRestoreCandidate PrepareRestoreCandidate(
        Grid grid,
        ModularFacilityWorldSaveData snapshot);
    void StageRestoreCandidate(ModularFacilityWorldRestoreCandidate candidate);
}

public sealed class ModularFacilityWorldRestoreCandidate :
    IDungeonDiscardableRestoreCandidate,
    IDungeonRestoreReportContributor
{
    private readonly ModularFacilityWorldSaveService owner;
    private ModularFacilityWorldSaveService.DetachedFacilityWorldCandidate world;

    internal ModularFacilityWorldRestoreCandidate(
        ModularFacilityWorldSaveService owner,
        ModularFacilityWorldSaveService.DetachedFacilityWorldCandidate world,
        int restoredCount)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        RestoredCount = restoredCount;
    }

    public int RestoredCount { get; }

    internal ModularFacilityWorldSaveService.DetachedFacilityWorldCandidate Take(
        ModularFacilityWorldSaveService expectedOwner)
    {
        if (!ReferenceEquals(owner, expectedOwner) || world == null)
        {
            throw new InvalidOperationException(
                "Facility-world restore candidate has the wrong owner or was already consumed.");
        }
        ModularFacilityWorldSaveService.DetachedFacilityWorldCandidate result = world;
        world = null;
        return result;
    }

    public void Discard()
    {
        if (world == null)
        {
            return;
        }
        owner.DiscardPreparedCandidate(world);
        world = null;
    }

    public void RecordRestoreResult(DungeonGameRestoreReport report)
    {
        (report ?? throw new ArgumentNullException(nameof(report)))
            .RecordRestoredBuildings(RestoredCount);
    }
}

public sealed class ModularFacilityWorldSaveService :
    IModularFacilityWorldSaveService,
    IDungeonRestoreTransactionParticipant
{
    public const int CurrentVersion = 5;

    private readonly Func<int, BuildingSO> findBuildingData;
    private readonly IGridBuildingObjectFactory objectFactory;
    private readonly IObjectResolver objectResolver;
    private readonly IGridTextureProvider gridTextureProvider;
    private readonly IFacilityRelocationWorldService facilityRelocationWorldService;
    private readonly IGridSystemPublisher gridSystemPublisher;
    private readonly IRestoreWorldCandidatePublisher restoreWorldCandidates;
    private readonly Action<BuildableObject> injectBuilding;
    private IGridBuildingFactory buildingFactory;
    private bool restoreTransactionActive;
    private DetachedFacilityWorldCandidate stagedCandidate;
    private PublishedFacilityWorldCandidate publishedCandidate;

    public string ParticipantId => "100.world.facilities";

    [Inject]
    public ModularFacilityWorldSaveService(
        IBuildingDefinitionLookup buildingLookup,
        IGridBuildingObjectFactory objectFactory,
        IObjectResolver objectResolver,
        IGridTextureProvider gridTextureProvider,
        IFacilityRelocationWorldService facilityRelocationWorldService,
        IGridSystemPublisher gridSystemPublisher,
        IRestoreWorldCandidatePublisher restoreWorldCandidates)
    {
        findBuildingData = CreateBuildingLookup(buildingLookup);
        this.objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
        this.objectResolver = objectResolver ?? throw new ArgumentNullException(nameof(objectResolver));
        this.gridTextureProvider = gridTextureProvider ?? throw new ArgumentNullException(nameof(gridTextureProvider));
        this.facilityRelocationWorldService = facilityRelocationWorldService
            ?? throw new ArgumentNullException(nameof(facilityRelocationWorldService));
        this.gridSystemPublisher = gridSystemPublisher
            ?? throw new ArgumentNullException(nameof(gridSystemPublisher));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        injectBuilding = objectResolver.Inject;
    }

#if UNITY_EDITOR
    public ModularFacilityWorldSaveService(
        Func<int, BuildingSO> findBuildingData,
        IGridBuildingObjectFactory objectFactory,
        Action<BuildableObject> injectBuilding,
        IGridTextureProvider gridTextureProvider,
        IFacilityRelocationWorldService facilityRelocationWorldService,
        IGameSessionStateStore sessionStateStore,
        IGridSystemPublisher gridSystemPublisher,
        IRestoreWorldCandidatePublisher restoreWorldCandidates)
    {
        this.findBuildingData = findBuildingData
            ?? throw new ArgumentNullException(nameof(findBuildingData));
        this.objectFactory = objectFactory
            ?? throw new ArgumentNullException(nameof(objectFactory));
        this.injectBuilding = injectBuilding
            ?? throw new ArgumentNullException(nameof(injectBuilding));
        buildingFactory = new GridBuildingFactory(
            buildingVisual: null,
            onBuildingCreated: this.injectBuilding,
            objectFactory: this.objectFactory);
        this.gridTextureProvider = gridTextureProvider
            ?? throw new ArgumentNullException(nameof(gridTextureProvider));
        this.facilityRelocationWorldService = facilityRelocationWorldService
            ?? throw new ArgumentNullException(nameof(facilityRelocationWorldService));
        _ = sessionStateStore
            ?? throw new ArgumentNullException(nameof(sessionStateStore));
        this.gridSystemPublisher = gridSystemPublisher
            ?? throw new ArgumentNullException(nameof(gridSystemPublisher));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
    }
#endif

    private static Func<int, BuildingSO> CreateBuildingLookup(IBuildingDefinitionLookup buildingLookup)
    {
        if (buildingLookup == null)
        {
            throw new ArgumentNullException(nameof(buildingLookup));
        }

        return buildingLookup.GetBuilding;
    }

    private static IGridBuildingFactory CreateBuildingFactory(
        IGridBuildingObjectFactory objectFactory,
        IObjectResolver objectResolver,
        IGridTextureProvider gridTextureProvider)
    {
        if (objectFactory == null)
        {
            throw new ArgumentNullException(nameof(objectFactory));
        }

        if (objectResolver == null)
        {
            throw new ArgumentNullException(nameof(objectResolver));
        }

        if (gridTextureProvider == null)
        {
            throw new ArgumentNullException(nameof(gridTextureProvider));
        }

        return new GridBuildingFactory(
            gridTextureProvider.Texture,
            objectResolver.Inject,
            objectFactory);
    }

    public ModularFacilityWorldSaveData CreateSnapshot(Grid grid)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        return new ModularFacilityWorldSaveData
        {
            version = CurrentVersion,
            gridWidth = grid.width,
            gridHeight = grid.height,
            gridCells = grid.GetCells()
                .Where(cell => cell != null)
                .OrderBy(cell => cell.Position.y)
                .ThenBy(cell => cell.Position.x)
                .Select(ModularFacilityGridCellSaveData.From)
                .ToList(),
            buildings = grid.FindAllOccupants(null)
                .OfType<BuildableObject>()
                .Where(IsPersistentWorldBuilding)
                .OrderBy(building => (int)building.BuildingData.Placement.Layer)
                .ThenBy(building => building.centerPos.y)
                .ThenBy(building => building.centerPos.x)
                .ThenBy(building => building.id)
                .Select(ModularFacilityBuildingSaveData.From)
                .ToList()
        };
    }

#if UNITY_EDITOR
    [Obsolete("V19 persists session state in foundation.session, not world.facilities.")]
    public ModularFacilityWorldSaveData CreateSnapshot(
        Grid grid,
        GameSessionState legacyEditorSession)
    {
        ModularFacilityWorldSaveData snapshot = CreateSnapshot(grid);
        snapshot.gameData = ModularFacilityGameDataSaveData.From(legacyEditorSession);
        return snapshot;
    }
#endif

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive || publishedCandidate != null)
        {
            throw new InvalidOperationException(
                "A modular facility restore candidate is already active.");
        }

        restoreTransactionActive = true;
        stagedCandidate = null;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive
            || stagedCandidate == null
            || publishedCandidate != null)
        {
            throw new InvalidOperationException(
                "No modular facility restore candidate is ready to publish.");
        }

        DetachedFacilityWorldCandidate candidate = stagedCandidate;
        PublishedFacilityWorldCandidate publication =
            new PublishedFacilityWorldCandidate(candidate);
        publishedCandidate = publication;
        stagedCandidate = null;

        if (!gridSystemPublisher.TryPublishGrid(
                candidate.LiveGrid,
                candidate.CandidateGrid,
                out string publishFailure))
        {
            throw new InvalidOperationException(publishFailure);
        }

        publication.GridPublished = true;
        restoreTransactionActive = false;
    }

    public void DiscardRestoreCandidate()
    {
        restoreWorldCandidates?.ClearFacilityCandidate();
        if (stagedCandidate != null)
        {
            DestroyDetachedCandidates(stagedCandidate.Buildings);
        }

        stagedCandidate = null;
        restoreTransactionActive = false;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        PublishedFacilityWorldCandidate publication = publishedCandidate;
        if (publication == null)
        {
            DiscardRestoreCandidate();
            return;
        }

        Exception rollbackFailure = null;
        try
        {
            if (publication.GridPublished
                && !gridSystemPublisher.TryPublishGrid(
                    publication.World.CandidateGrid,
                    publication.World.LiveGrid,
                    out string publishFailure))
            {
                rollbackFailure = new InvalidOperationException(publishFailure);
            }

        }
        finally
        {
            restoreWorldCandidates.ClearFacilityCandidate();
            DestroyDetachedCandidates(publication.World.Buildings);
            publishedCandidate = null;
            stagedCandidate = null;
            restoreTransactionActive = false;
        }

        if (rollbackFailure != null)
        {
            throw new InvalidOperationException(
                "Facility-world publication rollback could not restore the prior live state: "
                + rollbackFailure.Message,
                rollbackFailure);
        }
    }

    public void CompleteRestoreCandidate()
    {
        PublishedFacilityWorldCandidate publication = publishedCandidate;
        if (publication == null)
        {
            return;
        }

        CompletePublishedCandidate(publication.World);
        restoreWorldCandidates.ClearFacilityCandidate();
        publishedCandidate = null;
        stagedCandidate = null;
        restoreTransactionActive = false;
    }

    public string ToJson(ModularFacilityWorldSaveData snapshot, bool prettyPrint = false)
    {
        return ModularFacilityWorldSaveCodec.Serialize(snapshot, prettyPrint);
    }

    public ModularFacilityWorldSaveData FromJson(string json)
    {
        return ModularFacilityWorldSaveCodec.Deserialize(json);
    }

    public ModularFacilityWorldRestoreCandidate PrepareRestoreCandidate(
        Grid grid,
        ModularFacilityWorldSaveData snapshot)
    {
        ModularFacilityWorldRestoreReport report = ValidateRestore(grid, snapshot);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Facility-world restore candidate is invalid: "
                + string.Join(" | ", report.errors));
        }

        if (!TryBuildDetachedCandidate(
                grid,
                snapshot,
                report,
                out DetachedFacilityWorldCandidate world))
        {
            throw new InvalidOperationException(
                "Facility-world restore candidate preparation failed: "
                + string.Join(" | ", report.errors));
        }

        try
        {
            restoreWorldCandidates.SetFacilityCandidate(
                world.CandidateGrid,
                world.BuildingView);
        }
        catch
        {
            DestroyDetachedCandidates(world.Buildings);
            throw;
        }
        return new ModularFacilityWorldRestoreCandidate(
            this,
            world,
            report.restoredCount);
    }

#if UNITY_EDITOR
    [Obsolete("V19 restores session state through foundation.session.")]
    public ModularFacilityWorldRestoreCandidate PrepareRestoreCandidate(
        Grid grid,
        GameSessionState legacyEditorSession,
        ModularFacilityWorldSaveData snapshot) =>
        PrepareRestoreCandidate(grid, snapshot);
#endif

    public void StageRestoreCandidate(
        ModularFacilityWorldRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (!restoreTransactionActive || stagedCandidate != null)
        {
            throw new InvalidOperationException(
                "Facility-world candidate publication requires an active, empty V18 transaction slot.");
        }
        stagedCandidate = candidate.Take(this);
    }

    public ModularFacilityWorldRestoreReport ValidateRestore(
        Grid grid,
        ModularFacilityWorldSaveData snapshot)
    {
        ModularFacilityWorldRestoreReport report =
            new ModularFacilityWorldRestoreReport();
        if (grid == null)
        {
            report.AddError("Grid is null.");
            return report;
        }

        if (snapshot == null)
        {
            report.AddError("Snapshot is null.");
            return report;
        }

        if (snapshot.version > CurrentVersion)
        {
            report.AddError($"Unsupported save version {snapshot.version}.");
            return report;
        }

        if (snapshot.version < CurrentVersion)
        {
            report.AddError(
                $"Save version {snapshot.version} must be migrated through {nameof(FromJson)} before restore.");
            return report;
        }

        if (snapshot.gridWidth < 1
            || snapshot.gridWidth > DungeonSpaceExpansionCatalog.MaximumSupportedGridWidth
            || snapshot.gridHeight != DungeonSpaceExpansionCatalog.SupportedGridHeight)
        {
            report.AddError(
                $"Saved grid dimensions {snapshot.gridWidth}x{snapshot.gridHeight} are invalid.");
            return report;
        }

        ValidateGridLayout(snapshot, report);

        List<ModularFacilityBuildingSaveData> entries =
            (snapshot.buildings ?? new List<ModularFacilityBuildingSaveData>())
            .Where(entry => entry != null)
            .ToList();
        int nullEntryCount = (snapshot.buildings
                ?? new List<ModularFacilityBuildingSaveData>())
            .Count(entry => entry == null);
        if (nullEntryCount > 0)
        {
            report.AddError(
                $"Facility save contains {nullEntryCount} null building entr{(nullEntryCount == 1 ? "y" : "ies")}.");
        }
        foreach (ModularFacilityBuildingSaveData entry in entries)
        {
            if (!((BuildingInstanceId)entry.persistentInstanceId).IsValid)
            {
                report.AddError(
                    $"Building id={entry.buildingId} has no valid persistent instance ID.");
            }

            if (!Enum.IsDefined(typeof(GridLayer), entry.layer)
                || (entry.hasRuntimeLayer
                    && !Enum.IsDefined(
                        typeof(GridLayer),
                        entry.runtimeLayer)))
            {
                report.AddError(
                    $"Building id={entry.buildingId} contains an unknown grid layer.");
            }

            if (entry.facilityLevel < 1)
            {
                report.AddError(
                    $"Building id={entry.buildingId} has invalid facility level {entry.facilityLevel}.");
            }

            ValidateStateModules(entry, report);
        }

        foreach (IGrouping<string, ModularFacilityBuildingSaveData> duplicate in
                 entries.GroupBy(
                     entry => entry.persistentInstanceId,
                     StringComparer.Ordinal)
                 .Where(group => group.Count() > 1))
        {
            report.AddError(
                $"Duplicate building persistent ID '{duplicate.Key}'.");
        }

        if (report.errors.Count > 0)
        {
            return report;
        }

        Grid validationGrid = CreateGridFromSnapshot(grid, snapshot);
        if (validationGrid == null)
        {
            report.AddError("The saved grid layout could not be materialized.");
            return report;
        }
        int reservationId = 1;
        foreach (ModularFacilityBuildingSaveData entry in
                 SortForRestore(snapshot.buildings))
        {
            if (!TryResolveBuildingRestore(
                    validationGrid,
                    entry,
                    report,
                    out BuildingSO data,
                    out _,
                    out IReadOnlyList<Vector2Int> positions,
                    out GridLayer runtimeLayer,
                    validateAuthoredLayer: true))
            {
                continue;
            }

            RestoreFootprintReservation reservation =
                new RestoreFootprintReservation(
                    reservationId++,
                    data.Placement.IsMovement);
            if (!validationGrid.RegisterOccupant(
                    reservation,
                    runtimeLayer,
                    positions,
                    runtimeLayer == data.Placement.Layer
                        && data.Placement.IsMovement))
            {
                report.AddError(
                    $"Building id={entry.buildingId} overlaps another restored footprint.");
            }
        }

        return report;
    }

    private static void ValidateStateModules(
        ModularFacilityBuildingSaveData entry,
        ModularFacilityWorldRestoreReport report)
    {
        List<BuildingStateModuleSaveData> modules = entry.stateModules
            ?? new List<BuildingStateModuleSaveData>();
        if (modules.Any(module => module == null))
        {
            report.AddError(
                $"Building id={entry.buildingId} contains a null state module.");
        }

        HashSet<string> moduleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (BuildingStateModuleSaveData module in
                 modules.Where(module => module != null))
        {
            string moduleId = module.moduleId?.Trim() ?? string.Empty;
            if (moduleId.Length == 0)
            {
                report.AddError(
                    $"Building id={entry.buildingId} contains a state module with no ID.");
            }
            else if (!moduleIds.Add(moduleId))
            {
                report.AddError(
                    $"Building id={entry.buildingId} repeats state module '{moduleId}'.");
            }

            if (module.version <= 0
                || string.IsNullOrWhiteSpace(module.payload))
            {
                report.AddError(
                    $"Building id={entry.buildingId} state module '{moduleId}' has an invalid version or payload.");
            }
        }
    }

    private static void ValidateGridLayout(
        ModularFacilityWorldSaveData snapshot,
        ModularFacilityWorldRestoreReport report)
    {
        List<ModularFacilityGridCellSaveData> cells =
            snapshot.gridCells ?? new List<ModularFacilityGridCellSaveData>();
        long expectedCount = (long)snapshot.gridWidth * snapshot.gridHeight;
        if (cells.Count != expectedCount)
        {
            report.AddError(
                $"Facility save grid layout has {cells.Count} cells; expected {expectedCount}.");
            return;
        }

        HashSet<Vector2Int> positions = new HashSet<Vector2Int>();
        foreach (ModularFacilityGridCellSaveData cell in cells)
        {
            if (cell == null)
            {
                report.AddError("Facility save grid layout contains a null cell.");
                continue;
            }

            Vector2Int position = new Vector2Int(cell.x, cell.y);
            if (cell.x < 0
                || cell.y < 0
                || cell.x >= snapshot.gridWidth
                || cell.y >= snapshot.gridHeight)
            {
                report.AddError($"Facility save grid cell {position} is out of bounds.");
            }
            else if (!positions.Add(position))
            {
                report.AddError($"Facility save grid cell {position} is duplicated.");
            }

            if (!Enum.IsDefined(typeof(GridCellAreaType), cell.areaType))
            {
                report.AddError($"Facility save grid cell {position} has an unknown area type.");
            }
            if (!Enum.IsDefined(typeof(GridCellTerrainType), cell.terrainType))
            {
                report.AddError($"Facility save grid cell {position} has an unknown terrain type.");
            }
        }

        if (report.errors.Count > 0)
        {
            return;
        }

        Grid validation = CreateGridFromSnapshot(
            new Grid(
                snapshot.gridWidth,
                snapshot.gridHeight,
                Vector3.zero),
            snapshot);
        if (!DungeonSpaceGridLayout.TryCapture(
                validation,
                out DungeonInteriorLayoutSnapshot layout,
                out string layoutFailure))
        {
            report.AddError($"Facility save grid layout is invalid: {layoutFailure}");
            return;
        }

        bool validColumns = layout.ColumnCount == DungeonSpaceExpansionCatalog.InitialInteriorColumns
            || DungeonSpaceExpansionCatalog.All.Any(
                definition => definition.TargetInteriorColumns == layout.ColumnCount);
        if (!validColumns)
        {
            report.AddError(
                $"Facility save has unsupported dungeon-interior width {layout.ColumnCount}.");
        }
    }

    private static Grid CreateGridFromSnapshot(
        Grid coordinateSource,
        ModularFacilityWorldSaveData snapshot)
    {
        if (coordinateSource == null || snapshot == null)
        {
            return null;
        }

        Grid restored = new Grid(
            snapshot.gridWidth,
            snapshot.gridHeight,
            coordinateSource.OriginPosition,
            coordinateSource.CellWorldHeight);
        foreach (ModularFacilityGridCellSaveData savedCell in
                 snapshot.gridCells ?? new List<ModularFacilityGridCellSaveData>())
        {
            if (savedCell == null)
            {
                return null;
            }

            Vector2Int position = new Vector2Int(savedCell.x, savedCell.y);
            if (!restored.IsValidGridPos(position))
            {
                return null;
            }

            if (restored.GetGridCell(position).AreaType != savedCell.areaType)
            {
                restored.SetAreaType(position, savedCell.areaType);
            }
            if (restored.GetGridCell(position).TerrainType != savedCell.terrainType)
            {
                restored.SetTerrainType(position, savedCell.terrainType);
            }
        }

        return restored;
    }

    private bool TryBuildDetachedCandidate(
        Grid liveGrid,
        ModularFacilityWorldSaveData snapshot,
        ModularFacilityWorldRestoreReport report,
        out DetachedFacilityWorldCandidate worldCandidate)
    {
        worldCandidate = null;
        if (!ValidateExistingBuildingsCanClear(liveGrid, report))
        {
            return false;
        }

        Grid candidateGrid = CreateGridFromSnapshot(liveGrid, snapshot);
        if (candidateGrid == null)
        {
            report.AddError("The saved grid layout could not be materialized for restore.");
            return false;
        }
        List<DetachedBuildingRestoreCandidate> candidates =
            new List<DetachedBuildingRestoreCandidate>();
        foreach (ModularFacilityBuildingSaveData entry in SortForRestore(snapshot.buildings))
        {
            if (!TryCreateDetachedBuilding(
                    candidateGrid,
                    entry,
                    report,
                    out DetachedBuildingRestoreCandidate candidate))
            {
                DestroyDetachedCandidates(candidates);
                return false;
            }

            candidates.Add(candidate);
        }

        worldCandidate =
            new DetachedFacilityWorldCandidate(
                liveGrid,
                candidateGrid,
                candidates);
        report.restoredBuildings.AddRange(
            candidates.Select(candidate => candidate.SaveData));
        report.restoredCount = report.restoredBuildings.Count;
        return true;
    }

    internal void DiscardPreparedCandidate(
        DetachedFacilityWorldCandidate candidate)
    {
        restoreWorldCandidates.ClearFacilityCandidate();
        if (candidate != null)
        {
            DestroyDetachedCandidates(candidate.Buildings);
        }
    }

    private void CompletePublishedCandidate(
        DetachedFacilityWorldCandidate worldCandidate)
    {
        if (!ValidateExistingBuildingsCanClear(
                worldCandidate.LiveGrid,
                new ModularFacilityWorldRestoreReport()))
        {
            throw new InvalidOperationException(
                "The preserved facility world changed before publication completed.");
        }

        foreach (DetachedBuildingRestoreCandidate candidate in
                 worldCandidate.Buildings)
        {
            candidate.Building.gameObject.SetActive(true);
        }

        ModularFacilityWorldRestoreReport publicationReport =
            new ModularFacilityWorldRestoreReport();
        ClearExistingBuildings(
            worldCandidate.LiveGrid,
            publicationReport);
        if (!publicationReport.Success)
        {
            throw new InvalidOperationException(
                string.Join(" | ", publicationReport.errors));
        }

        foreach (DetachedBuildingRestoreCandidate candidate in
                 worldCandidate.Buildings)
        {
            candidate.Building.PublishDetachedRestore();
            if (!candidate.SaveData.relocationPacked)
            {
                gridTextureProvider.Texture.DrawBuilding(
                    candidate.Building.BuildingData,
                    candidate.Building.centerPos);
            }
        }

        gridSystemPublisher.CompleteGridPublication();
    }

    private static bool ValidateExistingBuildingsCanClear(
        Grid grid,
        ModularFacilityWorldRestoreReport report)
    {
        foreach (BuildableObject building in grid.FindAllOccupants(null)
                     .OfType<BuildableObject>()
                     .Where(IsPersistentWorldBuilding)
                     .Distinct())
        {
            GridLayer layer = ResolveRegisteredLayer(building);
            foreach (Vector2Int position in building.buildPoses.Distinct())
            {
                GridCell cell = grid.GetGridCell(position);
                if (cell != null
                    && cell.ContainsOccupant(layer, building))
                {
                    continue;
                }

                report.AddError(
                    $"Existing building id={building.id} has inconsistent {layer} occupancy at {position}.");
            }
        }

        return report.errors.Count == 0;
    }

    private bool TryCreateDetachedBuilding(
        Grid candidateGrid,
        ModularFacilityBuildingSaveData entry,
        ModularFacilityWorldRestoreReport report,
        out DetachedBuildingRestoreCandidate candidate)
    {
        candidate = null;
        if (!TryResolveBuildingRestore(
                candidateGrid,
                entry,
                report,
                out BuildingSO data,
                out Vector2Int center,
                out IReadOnlyList<Vector2Int> positions,
                out GridLayer runtimeLayer,
                validateAuthoredLayer: true))
        {
            return false;
        }

        BuildableObject building = objectFactory.CreateDetached(
            candidateGrid,
            data,
            center);
        if (building == null)
        {
            report.AddError(
                $"Building id={entry.buildingId} detached object creation failed at {center}.");
            return false;
        }

        try
        {
            building.PrepareForDetachedRestore();
            injectBuilding(building);
            building.RestorePersistentIdentity(
                (BuildingInstanceId)entry.persistentInstanceId);
            building.SetGrid(candidateGrid);
            building.Initialization(data, center);
            if (!candidateGrid.RegisterOccupant(
                    building,
                    runtimeLayer,
                    positions,
                    runtimeLayer == data.Placement.Layer
                        && data.Placement.IsMovement))
            {
                report.AddError(
                    $"Building id={entry.buildingId} candidate registration failed at {center}.");
                building.DiscardDetachedRestore();
                return false;
            }

            building.SetDamaged(entry.isDamaged);
            building.SetFacilityLevel(entry.facilityLevel);
            BuildingStateModuleRestoreResult stateResult =
                BuildingStateModulePersistence.Restore(
                    building,
                    entry.stateModules);
            foreach (string error in stateResult.errors)
            {
                report.AddError(error);
            }

            if (!stateResult.Success)
            {
                building.DiscardDetachedRestore();
                return false;
            }

            if (entry.relocationPacked)
            {
                facilityRelocationWorldService.RestorePackedPresentation(building);
            }

            candidate = new DetachedBuildingRestoreCandidate(
                building,
                entry);
            return true;
        }
        catch (Exception exception)
        {
            if (building != null && !building.IsGridDestroyed)
            {
                if (building.IsDetachedRestoreCandidate)
                {
                    building.DiscardDetachedRestore();
                }
                else if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(building.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(building.gameObject);
                }
            }
            report.AddError(
                $"Building id={entry.buildingId} candidate preparation failed: {exception.Message}");
            return false;
        }
    }

    private bool TryResolveBuildingRestore(
        Grid grid,
        ModularFacilityBuildingSaveData entry,
        ModularFacilityWorldRestoreReport report,
        out BuildingSO data,
        out Vector2Int center,
        out IReadOnlyList<Vector2Int> positions,
        out GridLayer runtimeLayer,
        bool validateAuthoredLayer)
    {
        data = null;
        center = default;
        positions = Array.Empty<Vector2Int>();
        runtimeLayer = GridLayer.Building;
        if (entry == null)
        {
            report.AddError("Encountered a null building save entry.");
            return false;
        }

        try
        {
            data = findBuildingData(entry.buildingId);
        }
        catch (Exception exception)
        {
            report.AddError(
                $"Building id={entry.buildingId} lookup failed: {exception.Message}");
            return false;
        }

        if (data == null)
        {
            report.AddError($"Building id={entry.buildingId} was not found.");
            return false;
        }

        if (validateAuthoredLayer && data.Placement.Layer != entry.layer)
        {
            report.AddError(
                $"Building id={entry.buildingId} layer changed from saved {entry.layer} to asset {data.Placement.Layer}.");
            return false;
        }

        center = new Vector2Int(entry.centerX, entry.centerY);
        positions = data.GetGridPosList(center);
        runtimeLayer = entry.hasRuntimeLayer
            ? entry.runtimeLayer
            : data.Placement.Layer;
        if (CanRegister(grid, runtimeLayer, positions))
        {
            return true;
        }

        report.AddError(
            $"Building id={entry.buildingId} cannot occupy {runtimeLayer} at {center}.");
        return false;
    }

    private static void DestroyDetachedCandidates(
        IEnumerable<DetachedBuildingRestoreCandidate> candidates)
    {
        foreach (DetachedBuildingRestoreCandidate candidate in
                 candidates ?? Enumerable.Empty<DetachedBuildingRestoreCandidate>())
        {
            if (candidate?.Building != null
                && !candidate.Building.IsGridDestroyed)
            {
                candidate.Building.DiscardDetachedRestore();
            }
        }
    }

    private void ClearExistingBuildings(Grid grid, ModularFacilityWorldRestoreReport report)
    {
        List<BuildableObject> existing = grid.FindAllOccupants(null)
            .OfType<BuildableObject>()
            .Where(IsPersistentWorldBuilding)
            .Distinct()
            .OrderByDescending(building => (int)building.BuildingData.Placement.Layer)
            .ThenByDescending(building => building.centerPos.y)
            .ThenByDescending(building => building.centerPos.x)
            .ToList();

        foreach (BuildableObject building in existing)
        {
            BuildingSO data = building.BuildingData;
            GridLayer runtimeLayer = ResolveRegisteredLayer(building);
            bool removed = grid.RemoveOccupant(
                building,
                runtimeLayer,
                building.buildPoses,
                runtimeLayer == data.Placement.Layer
                    && data.Placement.IsMovement);
            if (!removed)
            {
                report.AddError($"Failed to remove existing building id={building.id} at {building.centerPos}.");
                continue;
            }

            ResolveBuildingFactory().DeleteVisual(data, building.centerPos);
            building.RetireForWorldReplacement();
            report.clearedCount++;
        }
    }

    private static bool IsPersistentWorldBuilding(BuildableObject building)
    {
        return building != null
            && !building.IsGridDestroyed
            && building.BuildingData != null
            && building.BuildingData.id >= 0
            && building is not ConstructionSite
            && building is not ExteriorZoneMarker;
    }

    private static GridLayer ResolveRegisteredLayer(BuildableObject building)
    {
        if (building?.Grid != null
            && building.buildPoses.Any(position =>
                ReferenceEquals(
                    building.Grid.GetGridCell(position)
                        ?.GetOccupant(GridLayer.Construction),
                    building)))
        {
            return GridLayer.Construction;
        }

        return building?.BuildingData?.Placement.Layer
            ?? GridLayer.Building;
    }

    private IGridBuildingFactory ResolveBuildingFactory()
    {
        buildingFactory ??= CreateBuildingFactory(objectFactory, objectResolver, gridTextureProvider);
        return buildingFactory;
    }

    private static bool CanRegister(
        Grid grid,
        GridLayer layer,
        IReadOnlyList<Vector2Int> positions)
    {
        if (grid == null || positions == null || positions.Count == 0)
        {
            return false;
        }

        foreach (Vector2Int position in positions.Distinct())
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell == null || !cell.CanOccupy(layer))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<ModularFacilityBuildingSaveData> SortForRestore(
        IEnumerable<ModularFacilityBuildingSaveData> buildings)
    {
        return (buildings ?? Enumerable.Empty<ModularFacilityBuildingSaveData>())
            .Where(entry => entry != null)
            .OrderBy(entry => entry.layer == GridLayer.Hallway ? 0 : 1)
            .ThenBy(entry => entry.layer == GridLayer.Building ? 0 : 1)
            .ThenBy(entry => (int)entry.layer)
            .ThenBy(entry => entry.centerY)
            .ThenBy(entry => entry.centerX)
            .ThenBy(entry => entry.buildingId);
    }

    internal sealed class DetachedBuildingRestoreCandidate
    {
        public DetachedBuildingRestoreCandidate(
            BuildableObject building,
            ModularFacilityBuildingSaveData saveData)
        {
            Building = building ?? throw new ArgumentNullException(nameof(building));
            SaveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        }

        public BuildableObject Building { get; }
        public ModularFacilityBuildingSaveData SaveData { get; }
    }

    internal sealed class DetachedFacilityWorldCandidate
    {
        public DetachedFacilityWorldCandidate(
            Grid liveGrid,
            Grid candidateGrid,
            IReadOnlyList<DetachedBuildingRestoreCandidate> buildings)
        {
            LiveGrid = liveGrid ?? throw new ArgumentNullException(nameof(liveGrid));
            CandidateGrid = candidateGrid
                ?? throw new ArgumentNullException(nameof(candidateGrid));
            Buildings = buildings
                ?? throw new ArgumentNullException(nameof(buildings));
            BuildingView = buildings
                .Select(candidate => candidate.Building)
                .ToArray();
        }

        public Grid LiveGrid { get; }
        public Grid CandidateGrid { get; }
        public IReadOnlyList<DetachedBuildingRestoreCandidate> Buildings { get; }
        public IReadOnlyList<BuildableObject> BuildingView { get; }
    }

    private sealed class PublishedFacilityWorldCandidate
    {
        public PublishedFacilityWorldCandidate(
            DetachedFacilityWorldCandidate world)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
        }

        public DetachedFacilityWorldCandidate World { get; }
        public bool GridPublished { get; set; }
    }

    private sealed class RestoreFootprintReservation :
        IGridOccupant,
        IGridMovementOccupant
    {
        public RestoreFootprintReservation(int gridId, bool movement)
        {
            GridId = gridId;
            IsGridMovement = movement;
        }

        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement { get; }
        public GridMoveType GridMoveType => IsGridMovement
            ? GridMoveType.Instant
            : GridMoveType.Walk;
    }
}

[Serializable]
public sealed class ModularFacilityWorldSaveData
{
    public int version = ModularFacilityWorldSaveService.CurrentVersion;
    public int gridWidth;
    public int gridHeight;
    public List<ModularFacilityGridCellSaveData> gridCells =
        new List<ModularFacilityGridCellSaveData>();
#if UNITY_EDITOR
    [NonSerialized, Obsolete("V19 session state is stored in foundation.session.")]
    public ModularFacilityGameDataSaveData gameData = new ModularFacilityGameDataSaveData();
#endif
    public List<ModularFacilityBuildingSaveData> buildings = new List<ModularFacilityBuildingSaveData>();
}

[Serializable]
public sealed class ModularFacilityGridCellSaveData
{
    public int x;
    public int y;
    public GridCellAreaType areaType;
    public GridCellTerrainType terrainType;

    public static ModularFacilityGridCellSaveData From(GridCell cell)
    {
        if (cell == null)
        {
            throw new ArgumentNullException(nameof(cell));
        }

        return new ModularFacilityGridCellSaveData
        {
            x = cell.Position.x,
            y = cell.Position.y,
            areaType = cell.AreaType,
            terrainType = cell.TerrainType
        };
    }
}

[Serializable]
public sealed class ModularFacilityGameDataSaveData
{
    public bool hasGameSpeed;
    public int gameSpeed;
    public bool hasHoldingMoney;
    public int holdingMoney;
    public bool hasDay;
    public int day;
    public bool hasCurTime;
    public float curTime;
    public bool hasHour;
    public int hour;
    public bool hasTimeOfDay;
    public TimeOfDay timeOfDay;

    public static ModularFacilityGameDataSaveData From(GameSessionState gameData)
    {
        if (gameData == null)
        {
            return new ModularFacilityGameDataSaveData();
        }

        return new ModularFacilityGameDataSaveData
        {
            hasGameSpeed = gameData.gameSpeed != null,
            gameSpeed = gameData.gameSpeed != null ? gameData.gameSpeed.Value : 0,
            hasHoldingMoney = gameData.holdingMoney != null,
            holdingMoney = gameData.holdingMoney != null ? gameData.holdingMoney.Value : 0,
            hasDay = gameData.day != null,
            day = gameData.day != null ? gameData.day.Value : 1,
            hasCurTime = gameData.curTime != null,
            curTime = gameData.curTime != null ? gameData.curTime.Value : 0f,
            hasHour = gameData.hour != null,
            hour = gameData.hour != null ? gameData.hour.Value : 0,
            hasTimeOfDay = gameData.timeOfDay != null,
            timeOfDay = gameData.timeOfDay != null ? gameData.timeOfDay.Value : TimeOfDay.Morning
        };
    }
}

[Serializable]
public sealed class ModularFacilityBuildingSaveData
{
    public string persistentInstanceId;
    public int buildingId;
    public string code;
    public string objectName;
    public GridLayer layer;
    public bool hasRuntimeLayer;
    public GridLayer runtimeLayer;
    public bool relocationPacked;
    public int centerX;
    public int centerY;
    public int width;
    public int height;
    public bool isDamaged;
    public int facilityLevel = 1;
    public List<BuildingStateModuleSaveData> stateModules = new List<BuildingStateModuleSaveData>();

    public static ModularFacilityBuildingSaveData From(BuildableObject building)
    {
        BuildingSO data = building.BuildingData;
        ModularFacilityBuildingSaveData result = new ModularFacilityBuildingSaveData
        {
            persistentInstanceId = building.PersistentInstanceId.IsValid
                ? building.PersistentInstanceId.Value
                : throw new InvalidOperationException(
                    $"Building '{building.name}' has no persistent instance ID."),
            buildingId = data.id,
            code = data.GetFacilityCode(),
            objectName = data.objectName,
            layer = data.Placement.Layer,
            hasRuntimeLayer = true,
            runtimeLayer = ResolveRuntimeLayer(building),
            relocationPacked = IsPackedRelocation(building),
            centerX = building.centerPos.x,
            centerY = building.centerPos.y,
            width = data.Placement.Width,
            height = data.Placement.Height,
            isDamaged = building.IsDamaged,
            facilityLevel = building.FacilityLevel,
            stateModules = BuildingStateModulePersistence.Capture(building)
        };

        return result;
    }

    private static GridLayer ResolveRuntimeLayer(BuildableObject building)
    {
        if (building?.Grid != null
            && building.buildPoses.Any(position =>
                ReferenceEquals(
                    building.Grid.GetGridCell(position)
                        ?.GetOccupant(GridLayer.Construction),
                    building)))
        {
            return GridLayer.Construction;
        }

        return building?.BuildingData?.Placement.Layer
            ?? GridLayer.Building;
    }

    private static bool IsPackedRelocation(BuildableObject building)
    {
        if (ResolveRuntimeLayer(building) != GridLayer.Construction)
        {
            return false;
        }

        FacilityEvolutionStateComponent component =
            building.GetComponent<FacilityEvolutionStateComponent>();
        FacilityRelocationOrder order =
            component?.InstanceEvolution?.relocationOrder;
        return order != null
            && order.phase != FacilityRelocationPhase.Dismantling;
    }
}

[Serializable]
internal sealed class ModularFacilitySaveVersionHeader
{
    public int version;
}

public sealed class ModularFacilityWorldRestoreReport
{
    public int clearedCount;
    public int restoredCount;
    public readonly List<ModularFacilityBuildingSaveData> restoredBuildings =
        new List<ModularFacilityBuildingSaveData>();
    public readonly List<string> errors = new List<string>();

    public bool Success => errors.Count == 0;

    public void AddError(string message)
    {
        errors.Add(string.IsNullOrWhiteSpace(message) ? "Unknown restore error." : message);
    }
}
