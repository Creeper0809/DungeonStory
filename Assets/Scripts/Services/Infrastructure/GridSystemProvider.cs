using System;

public interface IGridSystemProvider
{
    GridSystemManager Manager { get; }
    Grid Grid { get; }
    bool TryGetManager(out GridSystemManager manager);
    bool TryGetGrid(out Grid grid);
}

public interface IGridSystemPublisher
{
    bool TryPublishGrid(
        Grid expectedCurrent,
        Grid replacement,
        out string failureReason);
    void CompleteGridPublication();
}

public sealed class GridSystemProvider : IGridSystemProvider, IGridSystemPublisher
{
    private readonly DungeonSceneRuntimeReferences runtimeReferences;

    public GridSystemProvider(DungeonSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public GridSystemManager Manager
    {
        get
        {
            if (!TryGetManager(out GridSystemManager resolvedManager))
            {
                throw new InvalidOperationException($"{nameof(IGridSystemProvider)} requires a loaded {nameof(GridSystemManager)}.");
            }

            return resolvedManager;
        }
    }

    public Grid Grid
    {
        get
        {
            if (!TryGetGrid(out Grid grid))
            {
                throw new InvalidOperationException($"{nameof(GridSystemManager)} did not initialize its {nameof(Grid)}.");
            }

            return grid;
        }
    }

    public bool TryGetManager(out GridSystemManager resolvedManager)
    {
        GridSystemManager manager = runtimeReferences.GridSystemManager;
        if (manager == null)
        {
            resolvedManager = null;
            return false;
        }

        manager.EnsureGridInitialized();
        resolvedManager = manager;
        return true;
    }

    public bool TryGetGrid(out Grid grid)
    {
        if (!TryGetManager(out GridSystemManager resolvedManager) || resolvedManager.grid == null)
        {
            grid = null;
            return false;
        }

        grid = resolvedManager.grid;
        return true;
    }

    public bool TryPublishGrid(
        Grid expectedCurrent,
        Grid replacement,
        out string failureReason)
    {
        if (!TryGetManager(out GridSystemManager manager))
        {
            failureReason = "The dungeon grid manager is not loaded.";
            return false;
        }

        return manager.TryPublishGrid(
            expectedCurrent,
            replacement,
            out failureReason);
    }

    public void CompleteGridPublication()
    {
        Manager.CompleteGridPublication();
    }
}
