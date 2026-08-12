using System;
using UnityEngine;

public interface IInvasionIntruderContext
{
    bool TryGetGrid(out Grid grid);
    bool TryGetOwner(out CharacterActor owner);
    bool TryResolveBuilding(
        BuildingInstanceId id,
        out BuildableObject building);
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
    private readonly IBuildingWorldQuery buildingWorld;

    public InvasionIntruderContext(
        IGridSystemProvider gridSystemProvider,
        IWorldDropZoneQuery worldDropZoneQuery,
        IRunVariableRuntimeReader runVariableReader,
        ICharacterSpawnerProvider spawnerProvider,
        IOwnerRunManagerProvider ownerProvider,
        IBuildingWorldQuery buildingWorld)
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
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
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

    public bool TryResolveBuilding(
        BuildingInstanceId id,
        out BuildableObject building)
    {
        building = null;
        if (!id.IsValid)
        {
            return false;
        }

        foreach (BuildableObject candidate in buildingWorld.Buildings
                     ?? Array.Empty<BuildableObject>())
        {
            if (candidate == null || !candidate.PersistentInstanceId.Equals(id))
            {
                continue;
            }
            if (building != null && !ReferenceEquals(building, candidate))
            {
                building = null;
                return false;
            }
            building = candidate;
        }
        return building != null;
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
        return InvasionIntruderEntrySceneAdapter.TryResolve(spawner, grid, out entry);
    }

    public InvasionIntruderSettings ApplyRunVariables(InvasionIntruderSettings source)
    {
        return runVariableReader.ApplyInvasionSettings(source);
    }
}
