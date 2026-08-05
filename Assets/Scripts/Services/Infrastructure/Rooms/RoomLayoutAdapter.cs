using System.Collections.Generic;
using UnityEngine;

public sealed class RoomLayout
{
    private readonly Dictionary<Vector2Int, RoomInstance> roomByCell;
    private readonly Dictionary<BuildableObject, RoomInstance> roomByPart;

    public RoomLayout(IReadOnlyList<RoomInstance> rooms)
    {
        Rooms = rooms != null ? new List<RoomInstance>(rooms) : new List<RoomInstance>();
        roomByCell = new Dictionary<Vector2Int, RoomInstance>();
        roomByPart = new Dictionary<BuildableObject, RoomInstance>();

        foreach (RoomInstance room in Rooms)
        {
            if (room == null)
            {
                continue;
            }

            foreach (Vector2Int cell in room.Cells)
            {
                roomByCell[cell] = room;
            }

            foreach (BuildableObject part in room.Furniture)
            {
                if (part != null)
                {
                    roomByPart[part] = room;
                }
            }
        }
    }

    public IReadOnlyList<RoomInstance> Rooms { get; }

    public bool TryGetRoom(Vector2Int cell, out RoomInstance room)
    {
        return roomByCell.TryGetValue(cell, out room);
    }

    public bool TryGetRoom(BuildableObject part, out RoomInstance room)
    {
        room = null;
        return part != null && roomByPart.TryGetValue(part, out room);
    }
}
