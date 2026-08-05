using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonStory.Rooms
{
    [Serializable]
    public sealed class RoomStateRecord
    {
        public int roomId;
        public Vector2Int[] cells = Array.Empty<Vector2Int>();
        public string[] furnitureIds = Array.Empty<string>();
        public string[] doorIds = Array.Empty<string>();
        public string[] wallIds = Array.Empty<string>();
        public int roles;
        public int solidBoundaryCount;
        public int openBoundaryCount;
        public bool selfContained;
    }

    public sealed class RoomLayoutRestoreCandidate
    {
        private RoomLayoutRestoreCandidate(RoomLayout layout)
        {
            Layout = layout;
        }

        public RoomLayout Layout { get; }

        public static bool TryCreate(
            IReadOnlyList<RoomStateRecord> records,
            int structuralVersion,
            out RoomLayoutRestoreCandidate candidate,
            out string reason)
        {
            candidate = null;
            reason = string.Empty;
            if (records == null || structuralVersion < 0)
            {
                reason = "invalid room restore header";
                return false;
            }

            HashSet<int> roomIds = new();
            HashSet<Vector2Int> claimedCells = new();
            List<RoomInstance> rooms = new();
            foreach (RoomStateRecord record in records)
            {
                if (record == null
                    || record.roomId <= 0
                    || !roomIds.Add(record.roomId)
                    || record.cells == null
                    || record.furnitureIds == null
                    || record.doorIds == null
                    || record.wallIds == null
                    || record.solidBoundaryCount < 0
                    || record.openBoundaryCount < 0
                    || !record.cells.All(claimedCells.Add)
                    || !TryParseIds(record.furnitureIds, out BuildingInstanceId[] furniture)
                    || !TryParseIds(record.doorIds, out BuildingInstanceId[] doors)
                    || !TryParseIds(record.wallIds, out BuildingInstanceId[] walls))
                {
                    reason = "invalid or overlapping room restore record";
                    return false;
                }

                rooms.Add(new RoomInstance(
                    new RoomId(record.roomId),
                    record.cells,
                    furniture,
                    doors,
                    walls,
                    (FacilityRole)record.roles,
                    record.solidBoundaryCount,
                    record.openBoundaryCount,
                    record.selfContained));
            }

            candidate = new RoomLayoutRestoreCandidate(new RoomLayout(rooms, structuralVersion));
            return true;
        }

        private static bool TryParseIds(string[] values, out BuildingInstanceId[] ids)
        {
            ids = new BuildingInstanceId[values.Length];
            HashSet<BuildingInstanceId> unique = new();
            for (int index = 0; index < values.Length; index++)
            {
                BuildingInstanceId id = new(values[index]);
                if (!id.IsValid || !unique.Add(id))
                {
                    ids = Array.Empty<BuildingInstanceId>();
                    return false;
                }

                ids[index] = id;
            }

            return true;
        }
    }

    public sealed class RoomLayoutStateStore
    {
        public RoomLayoutStateStore(RoomLayout initial = null)
        {
            Current = initial ?? new RoomLayout(Array.Empty<RoomInstance>());
        }

        public RoomLayout Current { get; private set; }

        public bool TryRestore(
            IReadOnlyList<RoomStateRecord> records,
            int structuralVersion,
            out string reason)
        {
            if (!RoomLayoutRestoreCandidate.TryCreate(
                    records,
                    structuralVersion,
                    out RoomLayoutRestoreCandidate candidate,
                    out reason))
            {
                return false;
            }

            Current = candidate.Layout;
            return true;
        }
    }
}
