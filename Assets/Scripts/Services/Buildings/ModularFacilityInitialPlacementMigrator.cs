using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ModularFacilityInitialPlacementMigrator
{
    private const int FirstModularId = 1000;
    private const int InitialRoomWallId = 7;
    private const int InitialRoomDoorId = 8;

    private static readonly IReadOnlyDictionary<string, PartPlacement[]> Recipes =
        new Dictionary<string, PartPlacement[]>(StringComparer.Ordinal)
        {
            ["HamburgerStore"] = Recipe(P("D07", -2), P("D01", -1), P("D04", 1), P("E01", 0)),
            ["WeaponStore"] = Recipe(P("S01", -2), P("S02", 0), P("S06", 1), P("S07", 2), P("S05", 0)),
            ["LordBedroom"] = Recipe(P("R07", -3), P("R02", -1), P("R06", 0), P("R08", 1), P("R09", 2), P("R10", 3), P("E03", -2), P("E04", 0), P("E06", 2)),

            ["P1_LowFoodShop"] = Recipe(P("D01", -1), P("D04", 1), P("E01", 0)),
            ["P1_MeatRestaurant"] = Recipe(P("D02", -1), P("D03", 1), P("D11", 0)),
            ["P1_PremiumMeatRestaurant"] = Recipe(P("D02", -1), P("D12", 0), P("D08", 1), P("E03", 0), P("E04", 0)),
            ["P1_BattleDining"] = Recipe(P("D02", -1), P("D05", 1), P("G05", 0)),
            ["P1_BattlefieldDining"] = Recipe(P("D02", -1), P("G04", 1), P("G05", 0), P("G06", 1)),
            ["P1_NobleDining"] = Recipe(P("D06", -1), P("D12", 1), P("E03", 0), P("E06", 1)),
            ["P1_GeneralStore"] = Recipe(P("S01", 0), P("S04", 1), P("E09", 1)),
            ["P1_WeaponShop"] = Recipe(P("S01", 0), P("S06", 1), P("S05", 0)),
            ["P1_RestRoom"] = Recipe(P("R01", 0), P("R05", 1), P("E04", 0), P("E01", 1)),
            ["P1_TrainingRoom"] = Recipe(P("T01", -1), P("T02", 0), P("T03", 1), P("T04", 0), P("E05", 1)),
            ["P1_GuardRoom"] = Recipe(P("G01", 0), P("S06", 1), P("G02", 0)),
            ["P1_Barracks"] = Recipe(P("R03", -1), P("G01", 1), P("G05", 0)),
            ["P1_WarBarracks"] = Recipe(P("R03", -1), P("G04", 1), P("G05", 0), P("G06", 1)),
            ["P1_ResearchLab"] = Recipe(P("Q01", -1), P("Q03", 0), P("Q04", 1), P("Q06", 0)),
            ["P1_ManaStorage"] = Recipe(P("M02", 0), P("M03", 1), P("E05", 0)),
            ["P1_Warehouse"] = Recipe(P("L01", -1), P("L02", 0), P("L03", 1), P("E09", 0)),
            ["P1_Toilet"] = Recipe(P("H01", -1), P("H02", 0), P("H07", -1)),
            ["P1_Washroom"] = Recipe(P("H03", -1), P("H06", 0), P("H05", 0), P("H07", -1))
        };

    private static readonly IReadOnlyDictionary<string, int> ModularIdByCode = BuildModularIdMap();

    public static IReadOnlyList<InitialBuildInfo> Expand(
        IEnumerable<InitialBuildInfo> placements,
        Func<int, BuildingSO> findBuildingData)
    {
        List<InitialBuildInfo> result = new List<InitialBuildInfo>();
        foreach (InitialBuildInfo placement in placements ?? Enumerable.Empty<InitialBuildInfo>())
        {
            if (!TryExpand(placement, findBuildingData, out IReadOnlyList<InitialBuildInfo> expanded))
            {
                if (placement != null)
                {
                    result.Add(placement);
                }
                continue;
            }

            result.AddRange(expanded);
        }

        return result;
    }

    public static IReadOnlyList<InitialBuildInfo> ExpandInitialRooms(
        IEnumerable<InitialBuildInfo> placements,
        Func<int, BuildingSO> findBuildingData)
    {
        List<InitialBuildInfo> source = placements != null
            ? placements.Where(placement => placement != null).ToList()
            : new List<InitialBuildInfo>();
        Dictionary<int, RowConnectionLayout> connectionsByFloor = CreateRowConnectionLayout(source);
        List<InitialBuildInfo> result = new List<InitialBuildInfo>();
        foreach (InitialBuildInfo placement in source)
        {
            if (!TryExpand(placement, findBuildingData, out IReadOnlyList<InitialBuildInfo> expanded))
            {
                result.Add(placement);
                continue;
            }

            result.AddRange(CreateInitialRoomBoundary(placement, findBuildingData, connectionsByFloor));
            result.AddRange(expanded);
        }

        return result;
    }

    public static bool TryExpand(
        InitialBuildInfo placement,
        Func<int, BuildingSO> findBuildingData,
        out IReadOnlyList<InitialBuildInfo> expanded)
    {
        expanded = Array.Empty<InitialBuildInfo>();
        if (placement?.Building == null
            || findBuildingData == null
            || !Recipes.TryGetValue(placement.Building.name, out PartPlacement[] recipe))
        {
            return false;
        }

        List<InitialBuildInfo> result = new List<InitialBuildInfo>(recipe.Length);
        foreach (PartPlacement part in recipe)
        {
            if (!ModularIdByCode.TryGetValue(part.Code, out int id))
            {
                return false;
            }

            BuildingSO data = findBuildingData(id);
            if (data == null)
            {
                return false;
            }

            result.Add(new InitialBuildInfo
            {
                Position = placement.Position + new Vector2Int(part.OffsetX, 0),
                Building = data
            });
        }

        expanded = result;
        return true;
    }

    public static bool IsLegacyMonolith(BuildingSO building)
    {
        return building != null && Recipes.ContainsKey(building.name);
    }

    private static IEnumerable<InitialBuildInfo> CreateInitialRoomBoundary(
        InitialBuildInfo placement,
        Func<int, BuildingSO> findBuildingData,
        IReadOnlyDictionary<int, RowConnectionLayout> connectionsByFloor)
    {
        if (placement?.Building == null || findBuildingData == null)
        {
            yield break;
        }

        BuildingSO wall = findBuildingData(InitialRoomWallId);
        BuildingSO door = findBuildingData(InitialRoomDoorId);
        if (wall == null || door == null)
        {
            yield break;
        }

        int startX = placement.Position.x - (placement.Building.width / 2);
        int endX = startX + Mathf.Max(1, placement.Building.width) - 1;
        int leftBoundaryX = startX - 1;
        int rightBoundaryX = endX + 1;
        RowConnectionLayout connections = connectionsByFloor != null
            && connectionsByFloor.TryGetValue(placement.Position.y, out RowConnectionLayout rowConnections)
                ? rowConnections
                : RowConnectionLayout.Empty;
        BuildingSO leftBoundary = ShouldOpenBoundary(leftBoundaryX, connections) ? door : wall;
        BuildingSO rightBoundary = ShouldOpenBoundary(rightBoundaryX, connections) ? door : wall;

        yield return new InitialBuildInfo
        {
            Position = new Vector2Int(leftBoundaryX, placement.Position.y),
            Building = leftBoundary
        };
        yield return new InitialBuildInfo
        {
            Position = new Vector2Int(rightBoundaryX, placement.Position.y),
            Building = rightBoundary
        };
    }

    private static Dictionary<int, RowConnectionLayout> CreateRowConnectionLayout(
        IReadOnlyCollection<InitialBuildInfo> placements)
    {
        return placements
            .Where(placement => placement?.Building != null && IsConnectionContent(placement.Building))
            .SelectMany(CreateConnectionSpans)
            .GroupBy(span => span.Y)
            .ToDictionary(
                group => group.Key,
                group => new RowConnectionLayout(group.ToArray()));
    }

    private static IEnumerable<ConnectionSpan> CreateConnectionSpans(InitialBuildInfo placement)
    {
        foreach (IGrouping<int, Vector2Int> row in placement.Building.GetGridPosList(placement.Position)
            .GroupBy(position => position.y))
        {
            yield return new ConnectionSpan(
                row.Key,
                row.Min(position => position.x),
                row.Max(position => position.x));
        }
    }

    private static bool IsConnectionContent(BuildingSO building)
    {
        return IsLegacyMonolith(building)
            || building.Placement.IsMovement
            || building.IsDoor;
    }

    private static bool ShouldOpenBoundary(int boundaryX, RowConnectionLayout connections)
    {
        return connections.HasContentLeftOf(boundaryX)
            && connections.HasContentRightOf(boundaryX);
    }

    private static IReadOnlyDictionary<string, int> BuildModularIdMap()
    {
        string[] groups =
        {
            "D01,D02,D03,D04,D05,D06,D07,D08,D09,D10,D11,D12",
            "S01,S02,S03,S04,S05,S06,S07,S08",
            "R01,R02,R03,R04,R05,R06,R07,R08,R09,R10",
            "Q01,Q02,Q03,Q04,Q05,Q06",
            "M01,M02,M03,M04",
            "T01,T02,T03,T04",
            "G01,G02,G03,G04,G05,G06",
            "L01,L02,L03,L04,L05,L06,L07",
            "H01,H02,H03,H04,H05,H06,H07",
            "E01,E02,E03,E04,E05,E06,E07,E08,E09"
        };
        string[] codes = groups.SelectMany(group => group.Split(',')).ToArray();
        return codes
            .Select((code, index) => new KeyValuePair<string, int>(code, FirstModularId + index))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static PartPlacement P(string code, int offsetX)
    {
        return new PartPlacement(code, offsetX);
    }

    private static PartPlacement[] Recipe(params PartPlacement[] parts)
    {
        return parts ?? Array.Empty<PartPlacement>();
    }

    private readonly struct PartPlacement
    {
        public PartPlacement(string code, int offsetX)
        {
            Code = code;
            OffsetX = offsetX;
        }

        public string Code { get; }
        public int OffsetX { get; }
    }

    private readonly struct ConnectionSpan
    {
        public ConnectionSpan(int y, int minX, int maxX)
        {
            Y = y;
            MinX = minX;
            MaxX = maxX;
        }

        public int Y { get; }
        public int MinX { get; }
        public int MaxX { get; }
    }

    private readonly struct RowConnectionLayout
    {
        public static readonly RowConnectionLayout Empty = new RowConnectionLayout(Array.Empty<ConnectionSpan>());

        private readonly ConnectionSpan[] spans;

        public RowConnectionLayout(ConnectionSpan[] spans)
        {
            this.spans = spans ?? Array.Empty<ConnectionSpan>();
        }

        public bool HasContentLeftOf(int boundaryX)
        {
            return spans.Any(span => span.MaxX < boundaryX);
        }

        public bool HasContentRightOf(int boundaryX)
        {
            return spans.Any(span => span.MinX > boundaryX);
        }
    }
}
