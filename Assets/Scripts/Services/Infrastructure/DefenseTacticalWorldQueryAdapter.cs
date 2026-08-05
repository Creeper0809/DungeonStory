using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DefenseTacticalWorldQueryAdapter : IDefenseTacticalWorldQuery
{
    private readonly IGridSystemProvider gridProvider;
    private readonly ICharacterAiWorldRegistry worldRegistry;

    public DefenseTacticalWorldQueryAdapter(
        IGridSystemProvider gridProvider,
        ICharacterAiWorldRegistry worldRegistry)
    {
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
    }

    public bool HasRestoreGrid =>
        worldRegistry.TryGetGrid(out Grid grid) && grid != null;

    public bool IsOperationalCellWalkable(Vector2Int cell)
    {
        return gridProvider.TryGetGrid(out Grid grid)
            && grid != null
            && grid.IsValidGridPos(cell)
            && grid.IsWalkable(cell);
    }

    public bool IsRestoreCellWalkable(Vector2Int cell)
    {
        return worldRegistry.TryGetGrid(out Grid grid)
            && grid != null
            && grid.IsValidGridPos(cell)
            && grid.IsWalkable(cell);
    }

    public IReadOnlyList<DefenseTacticalActorSnapshot> CaptureActors()
    {
        return worldRegistry.Characters
            .Where(actor => actor != null)
            .Select(actor => new DefenseTacticalActorSnapshot(
                CharacterPersistentIdentity.Require(actor).Value,
                !actor.IsDead
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active))
            .ToArray();
    }

    public IReadOnlyCollection<string> CaptureTargetIds()
    {
        HashSet<string> targets = new(StringComparer.Ordinal);
        foreach (DefenseTacticalActorSnapshot actor in CaptureActors())
        {
            targets.Add(actor.ActorId);
        }
        foreach (WildlifeActor actor in worldRegistry.Wildlife.Where(actor => actor != null))
        {
            targets.Add(actor.WildlifeId);
        }

        return targets;
    }
}
