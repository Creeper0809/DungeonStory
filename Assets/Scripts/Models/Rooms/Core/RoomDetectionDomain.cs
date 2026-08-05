using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.Rooms
{
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "RoomDetector")]
    public static class RoomDetector
    {
        private static readonly Vector2Int[] Neighbors =
        {
            Vector2Int.left,
            Vector2Int.right
        };

        public static RoomLayout Build(RoomOccupancySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new RoomLayout(Array.Empty<RoomInstance>());
            }

            HashSet<Vector2Int> visited = new();
            List<RoomInstance> rooms = new();
            int nextId = 1;
            foreach (Vector2Int cell in snapshot.Cells.OrderBy(cell => cell.y).ThenBy(cell => cell.x))
            {
                if (visited.Contains(cell) || !IsInteriorCell(snapshot, cell))
                {
                    continue;
                }

                List<Vector2Int> cells = CollectConnectedCells(snapshot, cell, visited);
                Boundary boundary = AnalyzeBoundary(snapshot, cells);
                RoomPartSnapshot[] furniture = cells
                    .SelectMany(snapshot.GetParts)
                    .Where(part => !part.IsDoor && !part.IsWall)
                    .Distinct()
                    .ToArray();
                FacilityRole roles = furniture.Aggregate(FacilityRole.None, (current, part) => current | part.Roles);
                rooms.Add(new RoomInstance(
                    new RoomId(nextId++),
                    cells,
                    furniture.Select(part => part.Id).ToArray(),
                    boundary.Doors.Select(part => part.Id).ToArray(),
                    boundary.Walls.Select(part => part.Id).ToArray(),
                    roles,
                    boundary.SolidCount,
                    boundary.OpenCount));
            }

            foreach (RoomPartSnapshot part in snapshot.Parts.Where(part =>
                         part.IsSelfContainedRoom && !part.IsDoor && !part.IsWall))
            {
                if (rooms.Any(room => !room.IsSelfContained && room.ContainsPart(part.Id))
                    || part.Cells.Count == 0)
                {
                    continue;
                }

                rooms.Add(new RoomInstance(
                    new RoomId(nextId++),
                    part.Cells,
                    new[] { part.Id },
                    Array.Empty<BuildingInstanceId>(),
                    Array.Empty<BuildingInstanceId>(),
                    part.Roles,
                    part.Cells.Count * 2,
                    0,
                    selfContained: true));
            }

            return new RoomLayout(rooms, snapshot.StructuralVersion);
        }

        private static bool IsInteriorCell(RoomOccupancySnapshot snapshot, Vector2Int cell) =>
            snapshot.IsValid(cell)
            && snapshot.IsInterior(cell)
            && snapshot.IsWalkable(cell)
            && !snapshot.GetParts(cell).Any(part => part.IsDoor || part.IsWall);

        private static List<Vector2Int> CollectConnectedCells(
            RoomOccupancySnapshot snapshot,
            Vector2Int start,
            ISet<Vector2Int> visited)
        {
            Queue<Vector2Int> queue = new();
            List<Vector2Int> cells = new();
            visited.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                cells.Add(current);
                foreach (Vector2Int direction in Neighbors)
                {
                    Vector2Int next = current + direction;
                    if (visited.Contains(next) || !IsInteriorCell(snapshot, next))
                    {
                        continue;
                    }

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return cells;
        }

        private static Boundary AnalyzeBoundary(
            RoomOccupancySnapshot snapshot,
            IReadOnlyList<Vector2Int> cells)
        {
            Boundary boundary = new();
            HashSet<Vector2Int> roomCells = new(cells);
            foreach (Vector2Int cell in cells)
            {
                foreach (Vector2Int direction in Neighbors)
                {
                    Vector2Int neighbor = cell + direction;
                    if (roomCells.Contains(neighbor))
                    {
                        continue;
                    }

                    if (!snapshot.IsValid(neighbor))
                    {
                        boundary.SolidCount++;
                        continue;
                    }

                    IReadOnlyList<RoomPartSnapshot> parts = snapshot.GetParts(neighbor);
                    RoomPartSnapshot door = parts.FirstOrDefault(part => part.IsDoor);
                    if (door != null)
                    {
                        boundary.Doors.Add(door);
                        continue;
                    }

                    RoomPartSnapshot wall = parts.FirstOrDefault(part => part.IsWall);
                    if (wall != null || !snapshot.IsWalkable(neighbor))
                    {
                        if (wall != null)
                        {
                            boundary.Walls.Add(wall);
                        }

                        boundary.SolidCount++;
                        continue;
                    }

                    boundary.OpenCount++;
                }
            }

            return boundary;
        }

        private sealed class Boundary
        {
            public readonly HashSet<RoomPartSnapshot> Doors = new();
            public readonly HashSet<RoomPartSnapshot> Walls = new();
            public int SolidCount;
            public int OpenCount;
        }
    }
}
