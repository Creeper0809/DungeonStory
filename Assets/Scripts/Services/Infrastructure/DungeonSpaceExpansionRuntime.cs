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
        string displayName,
        int tier,
        int targetInteriorColumns,
        int expectedPopulation)
    {
        ResearchProjectId = researchProjectId
            ?? throw new ArgumentNullException(nameof(researchProjectId));
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException(
                "A dungeon-space expansion display name is required.",
                nameof(displayName))
            : displayName.Trim();
        Tier = tier;
        TargetInteriorColumns = targetInteriorColumns;
        ExpectedPopulation = expectedPopulation;
    }

    public string ResearchProjectId { get; }
    public string DisplayName { get; }
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
        bool changed,
        long outcomeOwnerRevision = 0L)
    {
        ResearchProjectId = researchProjectId ?? string.Empty;
        Tier = tier;
        PreviousInteriorColumns = previousInteriorColumns;
        CurrentInteriorColumns = currentInteriorColumns;
        PreviousGridWidth = previousGridWidth;
        CurrentGridWidth = currentGridWidth;
        Changed = changed;
        OutcomeOwnerRevision = outcomeOwnerRevision;
    }

    public string ResearchProjectId { get; }
    public int Tier { get; }
    public int PreviousInteriorColumns { get; }
    public int CurrentInteriorColumns { get; }
    public int AddedInteriorColumns => CurrentInteriorColumns - PreviousInteriorColumns;
    public int PreviousGridWidth { get; }
    public int CurrentGridWidth { get; }
    public bool Changed { get; }
    public long OutcomeOwnerRevision { get; }
}

public static class DungeonSpaceExpansionCatalog
{
    public const string TierZeroInitializationId = "start:dungeon-space:tier-zero";
    public const string QuarryResearchId = "research:mining:quarry";
    public const string StonecuttingResearchId = "research:mining:stonecutting";
    public const string DeepMiningResearchId = "research:mining:deep";

    public const int SceneSeedInteriorColumns = 27;
    public const int InitialInteriorColumns = 29;
    public const int BasicSectorTargetColumns = 51;
    public const int SupportedSectorTargetColumns = 71;
    public const int DeepSectorTargetColumns = 87;
    public const int MaximumSupportedGridWidth = 104;
    public const int SupportedGridHeight = 3;

    private static readonly DungeonSpaceExpansionDefinition[] Definitions =
    {
        new DungeonSpaceExpansionDefinition(
            QuarryResearchId,
            "채석장",
            tier: 1,
            targetInteriorColumns: BasicSectorTargetColumns,
            expectedPopulation: 12),
        new DungeonSpaceExpansionDefinition(
            StonecuttingResearchId,
            "석재 가공",
            tier: 2,
            targetInteriorColumns: SupportedSectorTargetColumns,
            expectedPopulation: 18),
        new DungeonSpaceExpansionDefinition(
            DeepMiningResearchId,
            "심부 채굴",
            tier: 3,
            targetInteriorColumns: DeepSectorTargetColumns,
            expectedPopulation: 24)
    };

    public static IReadOnlyList<DungeonSpaceExpansionDefinition> All => Definitions;

    public static DungeonSpaceExpansionDefinition TierZeroInitialization =>
        new(
            TierZeroInitializationId,
            "초기 지하 정비",
            tier: 0,
            targetInteriorColumns: InitialInteriorColumns,
            expectedPopulation: 6);

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
    bool HasPendingExpansion { get; }
    string PendingResearchProjectId { get; }
    string PendingFailureReason { get; }
}

public interface IDungeonSpaceExpansionCommand
{
    bool TryReconcileNewRunTierZero(
        out DungeonSpaceExpansionResult result,
        out string failureReason);
}

public sealed class DungeonSpaceExpansionRuntime :
    IStartable,
    ITickable,
    IDisposable,
    IDungeonSaveRestoreCompletedHook,
    IDungeonSpaceExpansionQuery,
    IDungeonSpaceExpansionCommand
{
    private const int RetryIntervalTicks = 60;

    private readonly IGameEventBus gameEvents;
    private readonly IGridSystemProvider gridSystem;
    private readonly IGridSystemPublisher gridPublisher;
    private readonly IGameCalendar gameCalendar;
    private readonly IDungeonSpaceExpansionOutcomeCommitter outcomeCommitter;
    private readonly IBlueprintResearchStateService researchStateService;
    private IDisposable researchCompletedSubscription;
    private bool reconciliationRequested;
    private int retryTicksRemaining;

    public DungeonSpaceExpansionRuntime(
        IGameEventBus gameEvents,
        IGridSystemProvider gridSystem,
        IGridSystemPublisher gridPublisher,
        IGameCalendar gameCalendar,
        IDungeonSpaceExpansionOutcomeCommitter outcomeCommitter,
        IBlueprintResearchStateService researchStateService)
    {
        this.gameEvents = gameEvents
            ?? throw new ArgumentNullException(nameof(gameEvents));
        this.gridSystem = gridSystem
            ?? throw new ArgumentNullException(nameof(gridSystem));
        this.gridPublisher = gridPublisher
            ?? throw new ArgumentNullException(nameof(gridPublisher));
        this.gameCalendar = gameCalendar
            ?? throw new ArgumentNullException(nameof(gameCalendar));
        this.outcomeCommitter = outcomeCommitter
            ?? throw new ArgumentNullException(nameof(outcomeCommitter));
        this.researchStateService = researchStateService
            ?? throw new ArgumentNullException(nameof(researchStateService));
    }

    public IReadOnlyList<DungeonSpaceExpansionDefinition> Definitions =>
        DungeonSpaceExpansionCatalog.All;
    public DungeonSpaceExpansionResult LastResult { get; private set; }
    public bool HasPendingExpansion { get; private set; }
    public string PendingResearchProjectId { get; private set; } = string.Empty;
    public string PendingFailureReason { get; private set; } = string.Empty;

    public void Start()
    {
        if (researchCompletedSubscription != null)
        {
            return;
        }

        researchCompletedSubscription = gameEvents
            .Subscribe<BlueprintResearchCompletedEvent>(OnResearchCompleted);
    }

    public void OnRestoreCompleted()
    {
        reconciliationRequested = true;
        retryTicksRemaining = 0;
    }

    public void Tick()
    {
        if (!reconciliationRequested)
        {
            return;
        }
        if (retryTicksRemaining > 0)
        {
            retryTicksRemaining--;
            return;
        }

        if (TryReconcileCompletedResearch(out string failureReason))
        {
            reconciliationRequested = false;
            ClearPendingExpansion();
            return;
        }

        SetPendingExpansion(PendingResearchProjectId, failureReason, logFailure: false);
        retryTicksRemaining = RetryIntervalTicks;
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
                changed: false,
                outcomeOwnerRevision: current.ColumnCount);
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

        long outcomeOwnerRevision = expanded.ColumnCount;
        DungeonSpaceExpansionOutcomeReceipt outcomeReceipt =
            new DungeonSpaceExpansionOutcomeReceipt(
                definition.ResearchProjectId,
                DungeonSpaceExpansionOutcomeNames.Snapshot(
                    definition.ResearchProjectId,
                    definition.DisplayName),
                outcomeOwnerRevision,
                definition.Tier,
                current.ColumnCount,
                expanded.ColumnCount,
                liveGrid.width,
                replacement.width,
                Math.Max(0, gameCalendar.Current.AbsoluteDay),
                current.EntrancePosition.x,
                current.EntrancePosition.y);
        if (!outcomeCommitter.TryPrepare(
                outcomeReceipt,
                out PreparedDungeonSpaceExpansionOutcome preparedOutcome,
                out failureReason))
        {
            return false;
        }
        if (preparedOutcome.IsReplay)
        {
            failureReason =
                "A committed dungeon-space expansion outcome exists while the live grid is still behind its target.";
            return false;
        }

        if (!gridPublisher.TryPublishGrid(liveGrid, replacement, out failureReason))
        {
            outcomeCommitter.Cancel(preparedOutcome);
            return false;
        }

        OwnerOutcomeCommitResult outcomeCommit =
            outcomeCommitter.Commit(preparedOutcome);
        if (!outcomeCommit.DurablyCommitted)
        {
            if (!gridPublisher.TryPublishGrid(
                    replacement,
                    liveGrid,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Dungeon-space outcome commit failed and the published grid could not be rolled back: "
                    + rollbackFailure);
            }
            result = default;
            failureReason = "Dungeon-space outcome commit failed: "
                + outcomeCommit.DetailCode;
            return false;
        }

        result = new DungeonSpaceExpansionResult(
            definition.ResearchProjectId,
            definition.Tier,
            current.ColumnCount,
            expanded.ColumnCount,
            liveGrid.width,
            replacement.width,
            changed: true,
            outcomeOwnerRevision: outcomeOwnerRevision);
        LastResult = result;
        CompletePublicationSafely();
        failureReason = string.Empty;
        return true;
    }

    public bool TryReconcileNewRunTierZero(
        out DungeonSpaceExpansionResult result,
        out string failureReason)
    {
        result = default;
        if (!TryCaptureLayout(
                out DungeonInteriorLayoutSnapshot current,
                out failureReason))
        {
            return false;
        }

        if (current.ColumnCount
                != DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns
            && current.ColumnCount
                != DungeonSpaceExpansionCatalog.InitialInteriorColumns)
        {
            failureReason =
                "New-run Tier-0 reconciliation accepts only the canonical "
                + $"{DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns}-column scene seed "
                + $"or idempotent {DungeonSpaceExpansionCatalog.InitialInteriorColumns}-column layout; "
                + $"found {current.ColumnCount}.";
            return false;
        }

        return TryApply(
            DungeonSpaceExpansionCatalog.TierZeroInitialization,
            out result,
            out failureReason);
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

    private void CompletePublicationSafely()
    {
        try
        {
            gridPublisher.CompleteGridPublication();
        }
        catch (Exception exception) when (IsRecoverableObserverException(exception))
        {
            Debug.LogError(
                "Dungeon-space publication observer failed after the domain/outbox commit: "
                + exception.GetType().Name + ":" + exception.Message);
        }
    }

    private static bool IsRecoverableObserverException(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;

    private void OnResearchCompleted(BlueprintResearchCompletedEvent completed)
    {
        string projectId = completed.project != null
            ? completed.project.ProjectId.Value
            : string.Empty;
        if (!DungeonSpaceExpansionCatalog.TryGet(projectId, out var definition))
        {
            return;
        }

        if (TryApply(definition, out _, out string failureReason))
        {
            ClearPendingExpansion();
            return;
        }

        SetPendingExpansion(projectId, failureReason, logFailure: true);
        reconciliationRequested = true;
        retryTicksRemaining = RetryIntervalTicks;
    }

    private bool TryReconcileCompletedResearch(out string failureReason)
    {
        BlueprintResearchState researchState = researchStateService.GetState();
        if (researchState == null)
        {
            failureReason = "The blueprint-research state is unavailable for dungeon-space reconciliation.";
            return false;
        }

        IReadOnlyCollection<string> completed = researchState.Projects.CompletedProjectIds;
        DungeonSpaceExpansionDefinition target = default;
        bool hasTarget = false;
        foreach (DungeonSpaceExpansionDefinition definition in
                 DungeonSpaceExpansionCatalog.All)
        {
            if (!completed.Contains(definition.ResearchProjectId))
            {
                continue;
            }

            target = definition;
            hasTarget = true;
        }

        if (!hasTarget)
        {
            failureReason = string.Empty;
            return true;
        }

        PendingResearchProjectId = target.ResearchProjectId;
        if (!TryCaptureLayout(
                out DungeonInteriorLayoutSnapshot layout,
                out failureReason))
        {
            return false;
        }
        if (layout.ColumnCount >= target.TargetInteriorColumns)
        {
            failureReason = string.Empty;
            return true;
        }

        return TryApply(target, out _, out failureReason);
    }

    private void SetPendingExpansion(
        string researchProjectId,
        string failureReason,
        bool logFailure)
    {
        string normalizedProjectId = researchProjectId ?? string.Empty;
        string normalizedFailure = string.IsNullOrWhiteSpace(failureReason)
            ? "Dungeon-space reconciliation failed without a reason."
            : failureReason.Trim();
        bool changed = !HasPendingExpansion
            || !string.Equals(
                PendingResearchProjectId,
                normalizedProjectId,
                StringComparison.Ordinal)
            || !string.Equals(
                PendingFailureReason,
                normalizedFailure,
                StringComparison.Ordinal);

        HasPendingExpansion = true;
        PendingResearchProjectId = normalizedProjectId;
        PendingFailureReason = normalizedFailure;
        if (logFailure && changed)
        {
            Debug.LogError(
                $"Research '{normalizedProjectId}' completed but its dungeon expansion is pending retry: {normalizedFailure}");
        }
    }

    private void ClearPendingExpansion()
    {
        HasPendingExpansion = false;
        PendingResearchProjectId = string.Empty;
        PendingFailureReason = string.Empty;
    }
}
