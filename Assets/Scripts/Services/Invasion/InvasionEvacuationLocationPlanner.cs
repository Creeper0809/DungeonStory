using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

// Queries evacuation locations; movement leases, restore transactions and outcomes stay with the command owner.
internal sealed class InvasionEvacuationLocationPlanner
{
    private const int ResidentTargetCandidateLimit = 3;
    private readonly IInvasionIntruderContext invasionContext;
    private readonly InvasionDirectorRuntime director;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IWorldHazardZoneQuery worldHazards;

    internal InvasionEvacuationLocationPlanner(
        IInvasionIntruderContext invasionContext,
        InvasionDirectorRuntime director,
        IRoomLayoutCache roomLayoutCache,
        ICharacterWorldQuery characterWorld,
        IBuildingWorldQuery buildingWorld,
        IWorldHazardZoneQuery worldHazards)
    {
        this.invasionContext = invasionContext;
        this.director = director;
        this.roomLayoutCache = roomLayoutCache;
        this.characterWorld = characterWorld;
        this.buildingWorld = buildingWorld;
        this.worldHazards = worldHazards;
    }

    internal bool TryFindMatchingRoom(
        Grid grid,
        ResidentEvacuationZoneSaveData zone,
        out RoomInstance room)
    {
        room = null;
        if (grid == null || zone == null) return false;
        room = roomLayoutCache.GetLayout(grid).Rooms.FirstOrDefault(candidate =>
            candidate != null && candidate.IsUsable && !candidate.IsSelfContained
            && RoomMatchesZone(candidate, zone));
        return room != null;
    }

    internal Vector2Int[] BuildResidentTargetCandidates(
        CharacterActor actor,
        Grid grid,
        RoomInstance room,
        Vector2Int? previousTarget,
        HashSet<Vector2Int> claimedTargets)
    {
        if (actor == null || grid == null || room == null)
        {
            return Array.Empty<Vector2Int>();
        }

        HashSet<Vector2Int> unavailable = BuildUnavailableCells(
            grid,
            room,
            actor);
        Vector2Int current = actor.GetNowXY();
        return room.Cells
            .Distinct()
            .Where(cell => IsValidHoldingCell(grid, room, cell)
                && !unavailable.Contains(cell)
                && (claimedTargets == null || !claimedTargets.Contains(cell)))
            .OrderBy(cell => previousTarget.HasValue
                && cell == previousTarget.Value ? 0
                : cell == current ? 1 : 2)
            .ThenBy(cell => Manhattan(current, cell))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .Take(ResidentTargetCandidateLimit)
            .ToArray();
    }

    internal HashSet<Vector2Int> BuildUnavailableCells(
        Grid grid,
        RoomInstance room,
        CharacterActor subject)
    {
        HashSet<Vector2Int> unavailable =
            BuildStructurallyUnavailableCells(grid, room);
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (actor != null && !actor.IsDead && actor != subject)
            {
                unavailable.Add(actor.GetNowXY());
            }
        }
        foreach (InvasionIntruderRuntime intruder in director.ActiveIntruders)
        {
            if (intruder?.IntruderActor != null && !intruder.IntruderActor.IsDead)
            {
                unavailable.Add(intruder.IntruderActor.GetNowXY());
            }
        }
        CharacterId subjectId = CharacterPersistentIdentity.Require(subject);
        foreach (Vector2Int cell in room.Cells)
        {
            if ((worldHazards.GetHazard(subjectId, cell).Flags
                    & WorldHazardFlags.Fire) != 0)
            {
                unavailable.Add(cell);
            }
        }
        return unavailable;
    }

    internal HashSet<Vector2Int> BuildStructurallyUnavailableCells(
        Grid grid,
        RoomInstance room)
    {
        HashSet<Vector2Int> unavailable = new();
        foreach (BuildableObject door in room.Doors)
        {
            if (door == null || door.isDestroy) continue;
            foreach (Vector2Int cell in door.buildPoses) unavailable.Add(cell);
            unavailable.Add(door.centerPos);
        }
        if (invasionContext.TryResolveEntry(out InvasionIntruderEntry entry))
            unavailable.Add(entry.GridPosition);

        foreach (BuildableObject building in buildingWorld.Buildings)
        {
            if (building == null || building.isDestroy || building.Grid != grid
                || building.BuildingData == null) continue;
            IReadOnlyList<Vector2Int> footprint = building.buildPoses.Count > 0
                ? building.buildPoses
                : building.BuildingData.GetGridPosList(building.centerPos);

            // Movement surfaces and decorative/non-operational fixtures do not
            // reserve an entire room. Only truly exclusive walkable facility
            // footprints and one deterministic operational access cell do.
            if (building.BlocksGridMovement
                || building.Facility != null
                    && building.AllowsInteriorWalkability
                    && !building.IsGridMovement)
            {
                foreach (Vector2Int occupied in footprint)
                {
                    unavailable.Add(occupied);
                }
            }

            bool hasPrimaryOperation = building.Facility?.SupportedWorkTypeIds
                .Any(workType => workType != BuiltInWorkTypeIds.Clean
                    && workType != BuiltInWorkTypeIds.Repair) == true;
            if (!hasPrimaryOperation)
            {
                continue;
            }

            Vector2Int? operationalAccess = BuildingWorkAccessRules
                .EnumerateCandidates(footprint, building.IsGridMovement)
                .Where(cell => room.ContainsCell(cell)
                    && grid.IsValidGridPos(cell)
                    && grid.IsWalkable(cell))
                .OrderBy(cell => Manhattan(cell, building.centerPos))
                .ThenBy(cell => cell.y)
                .ThenBy(cell => cell.x)
                .Cast<Vector2Int?>()
                .FirstOrDefault();
            if (operationalAccess.HasValue)
            {
                unavailable.Add(operationalAccess.Value);
            }
        }
        return unavailable;
    }

    internal static bool IsValidHoldingCell(Grid grid, RoomInstance room, Vector2Int cell) =>
        room.ContainsCell(cell) && grid.IsValidGridPos(cell) && grid.IsWalkable(cell)
        && grid.GetGridCell(cell)?.AreaType == GridCellAreaType.DungeonInterior;

    internal static ResidentEvacuationZoneSaveData BuildZone(RoomInstance room)
    {
        Vector2Int[] cells = room.Cells.Distinct()
            .OrderBy(cell => cell.y).ThenBy(cell => cell.x).ToArray();
        ResidentEvacuationZoneSaveData result = new()
        {
            status = ResidentEvacuationZoneStatus.Active,
            anchorX = cells[0].x,
            anchorY = cells[0].y
        };
        int index = 0;
        while (index < cells.Length)
        {
            int y = cells[index].y;
            int start = cells[index].x;
            int end = start;
            index++;
            while (index < cells.Length && cells[index].y == y && cells[index].x == end + 1)
            {
                end = cells[index].x;
                index++;
            }
            result.cellSpans.Add(new ResidentEvacuationRoomCellSpanSaveData
            {
                y = y,
                startX = start,
                length = checked(end - start + 1)
            });
        }
        return result;
    }

    internal static ResidentEvacuationZoneSaveData CloneZone(ResidentEvacuationZoneSaveData source)
    {
        source ??= new ResidentEvacuationZoneSaveData();
        return new ResidentEvacuationZoneSaveData
        {
            status = source.status,
            anchorX = source.anchorX,
            anchorY = source.anchorY,
            cellSpans = source.cellSpans?.Where(span => span != null)
                .Select(span => new ResidentEvacuationRoomCellSpanSaveData
                {
                    y = span.y,
                    startX = span.startX,
                    length = span.length
                }).ToList() ?? new List<ResidentEvacuationRoomCellSpanSaveData>()
        };
    }

    internal static bool RoomMatchesZone(RoomInstance room, ResidentEvacuationZoneSaveData zone)
    {
        if (room == null || zone?.cellSpans == null) return false;
        ResidentEvacuationZoneSaveData current = BuildZone(room);
        if (current.anchorX != zone.anchorX || current.anchorY != zone.anchorY
            || current.cellSpans.Count != zone.cellSpans.Count) return false;
        for (int index = 0; index < current.cellSpans.Count; index++)
        {
            ResidentEvacuationRoomCellSpanSaveData left = current.cellSpans[index];
            ResidentEvacuationRoomCellSpanSaveData right = zone.cellSpans[index];
            if (right == null || left.y != right.y || left.startX != right.startX
                || left.length != right.length) return false;
        }
        return true;
    }

    internal bool TryResolveEvacuationCell(
        Grid grid, CharacterActor targetOwner, out Vector2Int target, out bool administration)
    {
        target = default;
        administration = false;
        GridPathSearchResult search = grid.SearchPath(targetOwner.GetNowXY());
        RoomInstance room = roomLayoutCache.GetLayout(grid).Rooms
            .Where(candidate => candidate != null && candidate.IsUsable
                && (candidate.Roles & FacilityRole.Administration) != 0)
            .OrderByDescending(candidate => candidate.Cells.Count)
            .FirstOrDefault(candidate => candidate.Cells.Any(search.ContainsPosition));
        if (room != null)
        {
            Vector2Int[] doors = room.Doors.Where(door => door != null && !door.isDestroy)
                .Select(door => door.centerPos).ToArray();
            Vector2Int? roomTarget = room.Cells
                .Where(cell => grid.IsWalkable(cell) && search.ContainsPosition(cell))
                .OrderByDescending(cell => DistanceFromNearestDoor(cell, doors))
                .ThenByDescending(cell => cell.y).Cast<Vector2Int?>().FirstOrDefault();
            if (roomTarget.HasValue)
            {
                target = roomTarget.Value;
                administration = true;
                return true;
            }
        }
        Vector2Int entry = Vector2Int.zero;
        if (invasionContext.TryResolveEntry(out InvasionIntruderEntry resolvedEntry))
            entry = resolvedEntry.GridPosition;
        GridCell fallback = search.GetReachablePositions().Select(grid.GetGridCell)
            .Where(cell => cell != null && cell.AreaType == GridCellAreaType.DungeonInterior
                && grid.IsWalkable(cell.Position))
            .OrderByDescending(cell => Manhattan(cell.Position, entry))
            .ThenByDescending(cell => cell.Position.y).FirstOrDefault();
        if (fallback == null) return false;
        target = fallback.Position;
        return true;
    }

    private static int DistanceFromNearestDoor(Vector2Int cell, IReadOnlyList<Vector2Int> doors)
    {
        if (doors == null || doors.Count == 0) return 0;
        int minimum = int.MaxValue;
        for (int index = 0; index < doors.Count; index++)
            minimum = Mathf.Min(minimum, Manhattan(cell, doors[index]));
        return minimum;
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
}
