using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.Rooms
{
    public readonly struct RoomId : IEquatable<RoomId>
    {
        public RoomId(int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(RoomId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RoomId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }

    public sealed class RoomPartSnapshot
    {
        private readonly HashSet<Vector2Int> occupiedCells;

        public RoomPartSnapshot(
            BuildingInstanceId id,
            IReadOnlyList<Vector2Int> cells,
            FacilityRole roles,
            bool isDoor,
            bool isWall,
            bool isDestroyed = false,
            bool isSelfContainedRoom = false,
            int seatCapacity = 0,
            int tableCapacity = 0,
            int serviceCapacity = 0)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A canonical building instance id is required.", nameof(id));
            }

            Id = id;
            Cells = cells?.Distinct().ToArray() ?? Array.Empty<Vector2Int>();
            Roles = roles;
            IsDoor = isDoor;
            IsWall = isWall;
            IsDestroyed = isDestroyed;
            IsSelfContainedRoom = isSelfContainedRoom;
            SeatCapacity = Math.Max(0, seatCapacity);
            TableCapacity = Math.Max(0, tableCapacity);
            ServiceCapacity = Math.Max(0, serviceCapacity);
            occupiedCells = new HashSet<Vector2Int>(Cells);
        }

        public BuildingInstanceId Id { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }
        public FacilityRole Roles { get; }
        public bool IsDoor { get; }
        public bool IsWall { get; }
        public bool IsDestroyed { get; }
        public bool IsSelfContainedRoom { get; }
        public int SeatCapacity { get; }
        public int TableCapacity { get; }
        public int ServiceCapacity { get; }
        public bool Occupies(Vector2Int cell) => occupiedCells.Contains(cell);
    }

    public sealed class RoomOccupancySnapshot
    {
        private readonly HashSet<Vector2Int> validCells;
        private readonly HashSet<Vector2Int> interiorCells;
        private readonly HashSet<Vector2Int> walkableCells;
        private readonly Dictionary<Vector2Int, IReadOnlyList<RoomPartSnapshot>> partsByCell;

        public RoomOccupancySnapshot(
            IReadOnlyCollection<Vector2Int> validCells,
            IReadOnlyCollection<Vector2Int> interiorCells,
            IReadOnlyCollection<Vector2Int> walkableCells,
            IReadOnlyList<RoomPartSnapshot> parts,
            int structuralVersion)
        {
            this.validCells = new HashSet<Vector2Int>(validCells ?? Array.Empty<Vector2Int>());
            this.interiorCells = new HashSet<Vector2Int>(interiorCells ?? Array.Empty<Vector2Int>());
            this.walkableCells = new HashSet<Vector2Int>(walkableCells ?? Array.Empty<Vector2Int>());
            Parts = parts?.Where(part => part != null && !part.IsDestroyed).ToArray()
                ?? Array.Empty<RoomPartSnapshot>();
            StructuralVersion = structuralVersion;

            Dictionary<Vector2Int, List<RoomPartSnapshot>> mutable = new();
            foreach (RoomPartSnapshot part in Parts)
            {
                foreach (Vector2Int cell in part.Cells)
                {
                    if (!mutable.TryGetValue(cell, out List<RoomPartSnapshot> occupants))
                    {
                        occupants = new List<RoomPartSnapshot>();
                        mutable[cell] = occupants;
                    }

                    occupants.Add(part);
                }
            }

            partsByCell = mutable.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<RoomPartSnapshot>)pair.Value.ToArray());
        }

        public IReadOnlyList<RoomPartSnapshot> Parts { get; }
        public int StructuralVersion { get; }
        public IEnumerable<Vector2Int> Cells => validCells;
        public bool IsValid(Vector2Int cell) => validCells.Contains(cell);
        public bool IsInterior(Vector2Int cell) => interiorCells.Contains(cell);
        public bool IsWalkable(Vector2Int cell) => walkableCells.Contains(cell);
        public IReadOnlyList<RoomPartSnapshot> GetParts(Vector2Int cell) =>
            partsByCell.TryGetValue(cell, out IReadOnlyList<RoomPartSnapshot> parts)
                ? parts
                : Array.Empty<RoomPartSnapshot>();
    }

    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "RoomInstance")]
    public sealed class RoomInstance
    {
        private readonly HashSet<Vector2Int> cellSet;
        private readonly HashSet<BuildingInstanceId> partSet;

        public RoomInstance(
            RoomId id,
            IReadOnlyList<Vector2Int> cells,
            IReadOnlyList<BuildingInstanceId> furniture,
            IReadOnlyList<BuildingInstanceId> doors,
            IReadOnlyList<BuildingInstanceId> walls,
            FacilityRole roles,
            int solidBoundaryCount,
            int openBoundaryCount,
            bool selfContained = false)
        {
            Id = id;
            Cells = cells?.Distinct().ToArray() ?? Array.Empty<Vector2Int>();
            Furniture = furniture?.Distinct().ToArray() ?? Array.Empty<BuildingInstanceId>();
            Doors = doors?.Distinct().ToArray() ?? Array.Empty<BuildingInstanceId>();
            Walls = walls?.Distinct().ToArray() ?? Array.Empty<BuildingInstanceId>();
            Roles = roles;
            SolidBoundaryCount = Math.Max(0, solidBoundaryCount);
            OpenBoundaryCount = Math.Max(0, openBoundaryCount);
            IsSelfContained = selfContained;
            cellSet = new HashSet<Vector2Int>(Cells);
            partSet = new HashSet<BuildingInstanceId>(Furniture);
            Bounds = CalculateBounds(Cells);
        }

        public RoomId Id { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }
        public IReadOnlyList<BuildingInstanceId> Furniture { get; }
        public IReadOnlyList<BuildingInstanceId> Doors { get; }
        public IReadOnlyList<BuildingInstanceId> Walls { get; }
        public FacilityRole Roles { get; }
        public int SolidBoundaryCount { get; }
        public int OpenBoundaryCount { get; }
        public bool IsSelfContained { get; }
        public RectInt Bounds { get; }
        public bool HasDoor => Doors.Count > 0 || IsSelfContained;
        public bool IsClosed => Cells.Count > 0 && OpenBoundaryCount == 0;
        public bool IsUsable => IsClosed && HasDoor;
        public bool ContainsCell(Vector2Int cell) => cellSet.Contains(cell);
        public bool ContainsPart(BuildingInstanceId part) => part.IsValid && partSet.Contains(part);
        public bool Supports(FacilityRole role) =>
            role != FacilityRole.None && IsUsable && (Roles & role) != 0;

        public float GetQualityScore()
        {
            if (!IsUsable)
            {
                return 0f;
            }

            float areaScore = Mathf.Clamp01(Cells.Count / 8f);
            float doorScore = Mathf.Clamp01(Doors.Count / 2f);
            float furnitureScore = Mathf.Clamp01(Furniture.Count / 4f);
            return Mathf.Clamp01(0.5f + areaScore * 0.25f + doorScore * 0.15f + furnitureScore * 0.1f);
        }

        private static RectInt CalculateBounds(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return new RectInt();
            }

            int minX = cells[0].x;
            int maxX = cells[0].x;
            int minY = cells[0].y;
            int maxY = cells[0].y;
            for (int index = 1; index < cells.Count; index++)
            {
                Vector2Int cell = cells[index];
                minX = Math.Min(minX, cell.x);
                maxX = Math.Max(maxX, cell.x);
                minY = Math.Min(minY, cell.y);
                maxY = Math.Max(maxY, cell.y);
            }

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }

    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "RoomLayout")]
    public sealed class RoomLayout
    {
        private readonly Dictionary<Vector2Int, RoomInstance> roomsByCell = new();
        private readonly Dictionary<BuildingInstanceId, RoomInstance> roomsByPart = new();

        public RoomLayout(IReadOnlyList<RoomInstance> rooms, int structuralVersion = 0)
        {
            Rooms = rooms?.Where(room => room != null).ToArray() ?? Array.Empty<RoomInstance>();
            StructuralVersion = structuralVersion;
            foreach (RoomInstance room in Rooms)
            {
                foreach (Vector2Int cell in room.Cells)
                {
                    roomsByCell[cell] = room;
                }

                foreach (BuildingInstanceId part in room.Furniture)
                {
                    roomsByPart[part] = room;
                }
            }
        }

        public IReadOnlyList<RoomInstance> Rooms { get; }
        public int StructuralVersion { get; }
        public bool TryGetRoom(Vector2Int cell, out RoomInstance room) => roomsByCell.TryGetValue(cell, out room);
        public bool TryGetRoom(BuildingInstanceId part, out RoomInstance room)
        {
            room = null;
            return part.IsValid && roomsByPart.TryGetValue(part, out room);
        }
    }
}
