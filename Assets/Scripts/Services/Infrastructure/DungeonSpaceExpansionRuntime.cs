using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public readonly struct DungeonSpaceExpansionDefinition
{
    public DungeonSpaceExpansionDefinition(
        string researchProjectId,
        int tier,
        int targetInteriorColumns,
        int expectedPopulation)
    {
        ResearchProjectId = researchProjectId
            ?? throw new ArgumentNullException(nameof(researchProjectId));
        Tier = tier;
        TargetInteriorColumns = targetInteriorColumns;
        ExpectedPopulation = expectedPopulation;
    }

    public string ResearchProjectId { get; }
    public int Tier { get; }
    public int TargetInteriorColumns { get; }
    public int ExpectedPopulation { get; }
}

public readonly struct DungeonInteriorLayoutSnapshot
{
    public DungeonInteriorLayoutSnapshot(
        int startX,
        int columnCount,
        Vector2Int entrancePosition)
    {
        StartX = startX;
        ColumnCount = columnCount;
        EntrancePosition = entrancePosition;
    }

    public int StartX { get; }
    public int ColumnCount { get; }
    public int EndExclusiveX => StartX + ColumnCount;
    public Vector2Int EntrancePosition { get; }
}

public readonly struct DungeonSpaceExpansionResult
{
    public DungeonSpaceExpansionResult(
        string researchProjectId,
        int tier,
        int previousInteriorColumns,
        int currentInteriorColumns,
        int previousGridWidth,
        int currentGridWidth,
        bool changed)
    {
        ResearchProjectId = researchProjectId ?? string.Empty;
        Tier = tier;
        PreviousInteriorColumns = previousInteriorColumns;
        CurrentInteriorColumns = currentInteriorColumns;
        PreviousGridWidth = previousGridWidth;
        CurrentGridWidth = currentGridWidth;
        Changed = changed;
    }

    public string ResearchProjectId { get; }
    public int Tier { get; }
    public int PreviousInteriorColumns { get; }
    public int CurrentInteriorColumns { get; }
    public int AddedInteriorColumns => CurrentInteriorColumns - PreviousInteriorColumns;
    public int PreviousGridWidth { get; }
    public int CurrentGridWidth { get; }
    public bool Changed { get; }
}

public static class DungeonSpaceExpansionCatalog
{
    public const string QuarryResearchId = "research:mining:quarry";
    public const string StonecuttingResearchId = "research:mining:stonecutting";
    public const string DeepMiningResearchId = "research:mining:deep";

    public const int InitialInteriorColumns = 27;
    public const int BasicSectorTargetColumns = 49;
    public const int SupportedSectorTargetColumns = 65;
    public const int DeepSectorTargetColumns = 81;
    public const int MaximumSupportedGridWidth = 104;
    public const int SupportedGridHeight = 3;

    private static readonly DungeonSpaceExpansionDefinition[] Definitions =
    {
        new DungeonSpaceExpansionDefinition(
            QuarryResearchId,
            tier: 1,
            targetInteriorColumns: BasicSectorTargetColumns,
            expectedPopulation: 12),
        new DungeonSpaceExpansionDefinition(
            StonecuttingResearchId,
            tier: 2,
            targetInteriorColumns: SupportedSectorTargetColumns,
            expectedPopulation: 18),
        new DungeonSpaceExpansionDefinition(
            DeepMiningResearchId,
            tier: 3,
            targetInteriorColumns: DeepSectorTargetColumns,
            expectedPopulation: 24)
    };

    public static IReadOnlyList<DungeonSpaceExpansionDefinition> All => Definitions;

    public static bool TryGet(
        string researchProjectId,
        out DungeonSpaceExpansionDefinition definition)
    {
        for (int index = 0; index < Definitions.Length; index++)
        {
            if (string.Equals(
                    Definitions[index].ResearchProjectId,
                    researchProjectId,
                    StringComparison.Ordinal))
            {
                definition = Definitions[index];
                return true;
            }
        }

        definition = default;
        return false;
    }

    public static bool TryGetForInteriorColumns(
        int interiorColumns,
        out DungeonSpaceExpansionDefinition definition)
    {
        for (int index = Definitions.Length - 1; index >= 0; index--)
        {
            if (interiorColumns >= Definitions[index].TargetInteriorColumns)
            {
                definition = Definitions[index];
                return true;
            }
        }

        definition = default;
        return false;
    }

    public static int ResolveExpectedInteriorColumns(
        IEnumerable<string> completedProjectIds)
    {
        HashSet<string> completed = new HashSet<string>(
            completedProjectIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        if (completed.Contains(DeepMiningResearchId))
        {
            return DeepSectorTargetColumns;
        }
        if (completed.Contains(StonecuttingResearchId))
        {
            return SupportedSectorTargetColumns;
        }
        return completed.Contains(QuarryResearchId)
            ? BasicSectorTargetColumns
            : InitialInteriorColumns;
    }
}

public static class DungeonSpaceGridLayout
{
    public static bool TryCapture(
        ModularFacilityWorldSaveData save,
        out DungeonInteriorLayoutSnapshot snapshot,
        out string failureReason)
    {
        snapshot = default;
        if (save == null
            || save.gridWidth < 1
            || save.gridHeight < 1
            || save.gridCells == null
            || (long)save.gridCells.Count
                != (long)save.gridWidth * save.gridHeight)
        {
            failureReason = "The facility save does not contain a complete grid layout.";
            return false;
        }

        Grid detached = new Grid(save.gridWidth, save.gridHeight);
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        foreach (ModularFacilityGridCellSaveData savedCell in save.gridCells)
        {
            if (savedCell == null)
            {
                failureReason = "The facility save grid layout contains a null cell.";
                return false;
            }

            Vector2Int position = new Vector2Int(savedCell.x, savedCell.y);
            if (!detached.IsValidGridPos(position) || !seen.Add(position))
            {
                failureReason = $"The facility save grid layout has an invalid or duplicate cell {position}.";
                return false;
            }

            if (detached.GetGridCell(position).AreaType != savedCell.areaType)
            {
                detached.SetAreaType(position, savedCell.areaType);
            }
            if (detached.GetGridCell(position).TerrainType != savedCell.terrainType)
            {
                detached.SetTerrainType(position, savedCell.terrainType);
            }
        }

        return TryCapture(detached, out snapshot, out failureReason);
    }

    public static bool TryCapture(
        Grid grid,
        out DungeonInteriorLayoutSnapshot snapshot,
        out string failureReason)
    {
        snapshot = default;
        if (grid == null)
        {
            failureReason = "The dungeon grid is unavailable.";
            return false;
        }

        GridCell[] entrances = grid.GetCells()
            .Where(cell => cell != null && cell.AreaType == GridCellAreaType.Entrance)
            .OrderBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .ToArray();
        if (entrances.Length != 1)
        {
            failureReason =
                $"The dungeon layout requires exactly one entrance; found {entrances.Length}.";
            return false;
        }

        Vector2Int entrance = entrances[0].Position;
        int startX = entrance.x;
        int endExclusive = startX;
        while (endExclusive < grid.width
               && IsInteriorColumn(grid, endExclusive, entrance))
        {
            endExclusive++;
        }

        int columns = endExclusive - startX;
        if (columns < 1)
        {
            failureReason = "The entrance is not the left edge of a contiguous dungeon interior.";
            return false;
        }

        for (int x = 0; x < grid.width; x++)
        {
            if (x >= startX && x < endExclusive)
            {
                continue;
            }

            for (int y = 0; y < grid.height; y++)
            {
                GridCell cell = grid.GetGridCell(new Vector2Int(x, y));
                if (cell != null && cell.AreaType == GridCellAreaType.DungeonInterior)
                {
                    failureReason =
                        $"Dungeon interior cell ({x},{y}) is outside the contiguous entrance range.";
                    return false;
                }
            }
        }

        snapshot = new DungeonInteriorLayoutSnapshot(startX, columns, entrance);
        failureReason = string.Empty;
        return true;
    }

    public static bool IsInteriorColumn(
        Grid grid,
        int x,
        Vector2Int entrancePosition)
    {
        if (grid == null || x < 0 || x >= grid.width)
        {
            return false;
        }

        for (int y = 0; y < grid.height; y++)
        {
            GridCell cell = grid.GetGridCell(new Vector2Int(x, y));
            if (cell == null)
            {
                return false;
            }

            bool entranceCell = x == entrancePosition.x && y == entrancePosition.y;
            if (entranceCell)
            {
                if (cell.AreaType != GridCellAreaType.Entrance)
                {
                    return false;
                }
            }
            else if (cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                return false;
            }
        }

        return true;
    }
}

public interface IDungeonSpaceExpansionQuery
{
    bool TryCaptureLayout(
        out DungeonInteriorLayoutSnapshot snapshot,
        out string failureReason);
    IReadOnlyList<DungeonSpaceExpansionDefinition> Definitions { get; }
    DungeonSpaceExpansionResult LastResult { get; }
}

public sealed class DungeonSpaceExpansionRuntime :
    IStartable,
    IDisposable,
    IDungeonSpaceExpansionQuery
{
    private readonly IGameEventBus gameEvents;
    private readonly IGridSystemProvider gridSystem;
    private readonly IGridSystemPublisher gridPublisher;
    private IDisposable researchCompletedSubscription;

    public DungeonSpaceExpansionRuntime(
        IGameEventBus gameEvents,
        IGridSystemProvider gridSystem,
        IGridSystemPublisher gridPublisher)
    {
        this.gameEvents = gameEvents
            ?? throw new ArgumentNullException(nameof(gameEvents));
        this.gridSystem = gridSystem
            ?? throw new ArgumentNullException(nameof(gridSystem));
        this.gridPublisher = gridPublisher
            ?? throw new ArgumentNullException(nameof(gridPublisher));
    }

    public IReadOnlyList<DungeonSpaceExpansionDefinition> Definitions =>
        DungeonSpaceExpansionCatalog.All;
    public DungeonSpaceExpansionResult LastResult { get; private set; }

    public void Start()
    {
        if (researchCompletedSubscription != null)
        {
            return;
        }

        researchCompletedSubscription = gameEvents
            .Subscribe<BlueprintResearchCompletedEvent>(OnResearchCompleted);
    }

    public void Dispose()
    {
        researchCompletedSubscription?.Dispose();
        researchCompletedSubscription = null;
    }

    public bool TryCaptureLayout(
        out DungeonInteriorLayoutSnapshot snapshot,
        out string failureReason)
    {
        if (!gridSystem.TryGetGrid(out Grid grid))
        {
            snapshot = default;
            failureReason = "The live dungeon grid is unavailable.";
            return false;
        }

        return DungeonSpaceGridLayout.TryCapture(
            grid,
            out snapshot,
            out failureReason);
    }

    public bool TryApply(
        DungeonSpaceExpansionDefinition definition,
        out DungeonSpaceExpansionResult result,
        out string failureReason)
    {
        result = default;
        if (!gridSystem.TryGetGrid(out Grid liveGrid))
        {
            failureReason = "The live dungeon grid is unavailable.";
            return false;
        }

        if (liveGrid.height != DungeonSpaceExpansionCatalog.SupportedGridHeight)
        {
            failureReason =
                $"Research expansion requires grid height {DungeonSpaceExpansionCatalog.SupportedGridHeight}; found {liveGrid.height}.";
            return false;
        }

        if (!DungeonSpaceGridLayout.TryCapture(
                liveGrid,
                out DungeonInteriorLayoutSnapshot current,
                out failureReason))
        {
            return false;
        }

        if (current.ColumnCount >= definition.TargetInteriorColumns)
        {
            result = new DungeonSpaceExpansionResult(
                definition.ResearchProjectId,
                definition.Tier,
                current.ColumnCount,
                current.ColumnCount,
                liveGrid.width,
                liveGrid.width,
                changed: false);
            LastResult = result;
            failureReason = string.Empty;
            return true;
        }

        int targetEndExclusive = current.StartX + definition.TargetInteriorColumns;
        int targetGridWidth = Mathf.Max(liveGrid.width, targetEndExclusive);
        if (targetGridWidth > DungeonSpaceExpansionCatalog.MaximumSupportedGridWidth)
        {
            failureReason =
                $"Expansion requires grid width {targetGridWidth}, beyond supported width {DungeonSpaceExpansionCatalog.MaximumSupportedGridWidth}.";
            return false;
        }

        int existingConversionEnd = Mathf.Min(targetEndExclusive, liveGrid.width);
        for (int x = current.EndExclusiveX; x < existingConversionEnd; x++)
        {
            for (int y = 0; y < liveGrid.height; y++)
            {
                GridCell cell = liveGrid.GetGridCell(new Vector2Int(x, y));
                if (HasBlockingExpansionOccupant(cell, out string occupantSummary))
                {
                    failureReason =
                        $"Expansion cell ({x},{y}) contains blocking occupants "
                        + $"({occupantSummary}) and cannot be converted to dungeon interior.";
                    return false;
                }
            }
        }

        int widthDelta = targetGridWidth - liveGrid.width;
        Grid replacement = widthDelta > 0
            ? liveGrid.TryExpandGrid(widthDelta, 0)
            : liveGrid.TryExpandGrid(0, 0);
        if (replacement == null)
        {
            failureReason = "The dungeon grid could not allocate the expansion candidate.";
            return false;
        }

        for (int x = current.EndExclusiveX; x < targetEndExclusive; x++)
        {
            for (int y = 0; y < replacement.height; y++)
            {
                Vector2Int position = new Vector2Int(x, y);
                GridCell cell = replacement.GetGridCell(position);
                if (cell == null)
                {
                    failureReason = $"Expansion candidate is missing cell {position}.";
                    return false;
                }

                if (cell.AreaType != GridCellAreaType.DungeonInterior)
                {
                    replacement.SetAreaType(position, GridCellAreaType.DungeonInterior);
                }
            }
        }

        if (!DungeonSpaceGridLayout.TryCapture(
                replacement,
                out DungeonInteriorLayoutSnapshot expanded,
                out failureReason)
            || expanded.StartX != current.StartX
            || expanded.ColumnCount != definition.TargetInteriorColumns
            || expanded.EntrancePosition != current.EntrancePosition)
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "The expansion candidate did not preserve the exact entrance/interior contract."
                : failureReason;
            return false;
        }

        if (!gridPublisher.TryPublishGrid(liveGrid, replacement, out failureReason))
        {
            return false;
        }

        gridPublisher.CompleteGridPublication();
        result = new DungeonSpaceExpansionResult(
            definition.ResearchProjectId,
            definition.Tier,
            current.ColumnCount,
            expanded.ColumnCount,
            liveGrid.width,
            replacement.width,
            changed: true);
        LastResult = result;
        failureReason = string.Empty;
        return true;
    }

    private static bool HasBlockingExpansionOccupant(
        GridCell cell,
        out string occupantSummary)
    {
        occupantSummary = string.Empty;
        if (cell?.HasOccupant() != true)
        {
            return false;
        }

        List<IGridOccupant> occupants = new();
        cell.FillAllOccupants(occupants);
        IGridOccupant[] blocking = occupants
            .Where(value => value != null
                && value is not IWorldResourceNodeHost
                && value is not WildlifeActor)
            .Distinct()
            .OrderBy(value => value.GetType().FullName, StringComparer.Ordinal)
            .ThenBy(value => value.GridId)
            .ToArray();
        occupantSummary = string.Join(",", blocking.Select(value =>
            value.GetType().Name + ":" + value.GridId));
        return blocking.Length > 0;
    }

    private void OnResearchCompleted(BlueprintResearchCompletedEvent completed)
    {
        string projectId = completed.project != null
            ? completed.project.ProjectId.Value
            : string.Empty;
        if (!DungeonSpaceExpansionCatalog.TryGet(projectId, out var definition))
        {
            return;
        }

        if (!TryApply(definition, out _, out string failureReason))
        {
            throw new InvalidOperationException(
                $"Research '{projectId}' completed but its dungeon expansion failed: {failureReason}");
        }
    }
}
