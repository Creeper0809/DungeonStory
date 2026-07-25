using System;
using UnityEngine;

public readonly struct InvasionIntruderEntry
{
    public InvasionIntruderEntry(
        Vector2Int gridPosition,
        Vector3 outsidePosition,
        Vector3 doorPosition)
    {
        GridPosition = gridPosition;
        OutsidePosition = outsidePosition;
        DoorPosition = doorPosition;
    }

    public Vector2Int GridPosition { get; }
    public Vector3 OutsidePosition { get; }
    public Vector3 DoorPosition { get; }
}

public interface IInvasionIntruderContext
{
    bool TryGetGrid(out Grid grid);
    bool TryGetOwner(out CharacterActor owner);
    bool TryResolveEntry(out InvasionIntruderEntry entry);
    InvasionIntruderSettings ApplyRunVariables(InvasionIntruderSettings source);
}

public sealed class InvasionIntruderContext : IInvasionIntruderContext
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IWorldDropZoneQuery worldDropZoneQuery;
    private readonly IRunVariableRuntimeReader runVariableReader;
    private readonly ICharacterSpawnerProvider spawnerProvider;
    private readonly IOwnerRunManagerProvider ownerProvider;

    public InvasionIntruderContext(
        IGridSystemProvider gridSystemProvider,
        IWorldDropZoneQuery worldDropZoneQuery,
        IRunVariableRuntimeReader runVariableReader,
        ICharacterSpawnerProvider spawnerProvider,
        IOwnerRunManagerProvider ownerProvider)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.worldDropZoneQuery = worldDropZoneQuery
            ?? throw new ArgumentNullException(nameof(worldDropZoneQuery));
        this.runVariableReader = runVariableReader
            ?? throw new ArgumentNullException(nameof(runVariableReader));
        this.spawnerProvider = spawnerProvider
            ?? throw new ArgumentNullException(nameof(spawnerProvider));
        this.ownerProvider = ownerProvider
            ?? throw new ArgumentNullException(nameof(ownerProvider));
    }

    public bool TryGetGrid(out Grid grid)
    {
        grid = null;
        try
        {
            grid = gridSystemProvider.Grid;
            return grid != null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public bool TryGetOwner(out CharacterActor owner)
    {
        ownerProvider.TryGetManager(out OwnerRunManager ownerRunManager);
        owner = ownerRunManager != null ? ownerRunManager.CurrentOwnerActor : null;
        return owner != null;
    }

    public bool TryResolveEntry(out InvasionIntruderEntry entry)
    {
        if (worldDropZoneQuery.TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint))
        {
            entry = new InvasionIntruderEntry(
                entryPoint.GridPosition,
                entryPoint.OutsidePosition,
                entryPoint.DoorPosition);
            return true;
        }

        TryGetGrid(out Grid grid);
        spawnerProvider.TryGetSpawner(out CharacterSpawner spawner);
        return InvasionIntruderEntryResolver.TryResolve(spawner, grid, out entry);
    }

    public InvasionIntruderSettings ApplyRunVariables(InvasionIntruderSettings source)
    {
        return runVariableReader.ApplyInvasionSettings(source);
    }
}
