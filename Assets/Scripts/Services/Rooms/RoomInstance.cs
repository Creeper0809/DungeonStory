using System.Collections.Generic;
using UnityEngine;

public sealed class RoomInstance
{
    private readonly HashSet<Vector2Int> cellSet;
    private readonly HashSet<BuildableObject> partSet;

    public RoomInstance(
        int id,
        IReadOnlyList<Vector2Int> cells,
        IReadOnlyList<BuildableObject> furniture,
        IReadOnlyList<BuildableObject> doors,
        IReadOnlyList<BuildableObject> walls,
        int solidBoundaryCount,
        int openBoundaryCount,
        bool selfContained = false)
    {
        Id = id;
        Cells = cells != null ? new List<Vector2Int>(cells) : new List<Vector2Int>();
        Furniture = furniture != null ? new List<BuildableObject>(furniture) : new List<BuildableObject>();
        Doors = doors != null ? new List<BuildableObject>(doors) : new List<BuildableObject>();
        Walls = walls != null ? new List<BuildableObject>(walls) : new List<BuildableObject>();
        SolidBoundaryCount = Mathf.Max(0, solidBoundaryCount);
        OpenBoundaryCount = Mathf.Max(0, openBoundaryCount);
        IsSelfContained = selfContained;

        cellSet = new HashSet<Vector2Int>(Cells);
        partSet = new HashSet<BuildableObject>();
        foreach (BuildableObject part in Furniture)
        {
            if (part != null)
            {
                partSet.Add(part);
            }
        }

        FacilityRoles = BuildFacilityRoles(Furniture);
        Bounds = CalculateBounds(Cells);
    }

    public int Id { get; }
    public IReadOnlyList<Vector2Int> Cells { get; }
    public IReadOnlyList<BuildableObject> Furniture { get; }
    public IReadOnlyList<BuildableObject> Doors { get; }
    public IReadOnlyList<BuildableObject> Walls { get; }
    public int SolidBoundaryCount { get; }
    public int OpenBoundaryCount { get; }
    public FacilityRole FacilityRoles { get; }
    public FacilityRole Roles => FacilityRoles;
    public RectInt Bounds { get; }
    public bool IsSelfContained { get; }

    public bool HasDoor => Doors.Count > 0 || IsSelfContained;
    public bool IsClosed => Cells.Count > 0 && OpenBoundaryCount == 0;
    public bool IsUsable => IsClosed && HasDoor;

    public bool ContainsCell(Vector2Int position)
    {
        return cellSet.Contains(position);
    }

    public bool ContainsPart(BuildableObject part)
    {
        return part != null && partSet.Contains(part);
    }

    public bool SupportsFacilityRole(FacilityRole role)
    {
        return role != FacilityRole.None
            && IsUsable
            && (FacilityRoles & role) != 0;
    }

    public float GetQualityScore()
    {
        if (!IsUsable)
        {
            return 0f;
        }

        float areaScore = Mathf.Clamp01(Cells.Count / 8f);
        float doorScore = Mathf.Clamp01(Doors.Count / 2f);
        float furnitureScore = Mathf.Clamp01(Furniture.Count / 4f);
        return Mathf.Clamp01(0.5f + (areaScore * 0.25f) + (doorScore * 0.15f) + (furnitureScore * 0.1f));
    }

    private static FacilityRole BuildFacilityRoles(IReadOnlyList<BuildableObject> furniture)
    {
        FacilityRole roles = FacilityRole.None;
        if (furniture == null)
        {
            return roles;
        }

        foreach (BuildableObject part in furniture)
        {
            if (part != null && part.Facility != null)
            {
                roles |= part.Facility.roles;
            }
        }

        return roles;
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
        for (int i = 1; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        return new RectInt(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
    }
}
