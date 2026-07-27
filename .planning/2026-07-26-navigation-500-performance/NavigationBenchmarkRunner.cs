using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

internal static class NavigationBenchmarkRunner
{
    private const int Width = 96;
    private const int Height = 3;
    private const int QueryCount = 500;
    private const int IterationCount = 20;

    public static int Main()
    {
        Grid grid = CreateGrid();

        for (int index = 0; index < QueryCount; index++)
        {
            RunQuery(grid, index);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long beforeMemory = GC.GetTotalMemory(true);
        long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();
        long checksum = 0;
        for (int iteration = 0; iteration < IterationCount; iteration++)
        {
            for (int index = 0; index < QueryCount; index++)
            {
                checksum += RunQuery(grid, index + iteration * 17);
            }
        }

        stopwatch.Stop();
        long afterAllocated = GC.GetAllocatedBytesForCurrentThread();
        long afterMemory = GC.GetTotalMemory(false);
        double totalQueries = QueryCount * IterationCount;
        double microsecondsPerQuery = stopwatch.Elapsed.TotalMilliseconds * 1000d / totalQueries;

        GridPathSearchResult weighted = grid.SearchPathTo(
            new Vector2Int(0, 1),
            new Vector2Int(Width - 1, 1));
        int weightedCost = weighted.GetMoveCostTo(new Vector2Int(Width - 1, 1));
        int expectedDryCost = (Width - 1) * DefaultGridTraversalCostPolicy.DryWalkCost;
        bool weightedCostIncludesTerrain = weightedCost > expectedDryCost;
        bool weightedDetourValid = VerifyWeightedDetour();

        Console.WriteLine($"queries={totalQueries:0}");
        Console.WriteLine($"elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:0.###}");
        Console.WriteLine($"microseconds_per_query={microsecondsPerQuery:0.###}");
        Console.WriteLine($"managed_delta_bytes={afterMemory - beforeMemory}");
        Console.WriteLine($"allocated_bytes_per_query={(afterAllocated - beforeAllocated) / totalQueries:0.###}");
        Console.WriteLine($"weighted_cost={weightedCost}");
        Console.WriteLine($"expected_all_dry_cost={expectedDryCost}");
        Console.WriteLine($"weighted_cost_includes_terrain={weightedCostIncludesTerrain}");
        Console.WriteLine($"weighted_detour_valid={weightedDetourValid}");
        Console.WriteLine($"checksum={checksum}");

        return weightedCostIncludesTerrain && weightedDetourValid && checksum > 0 ? 0 : 1;
    }

    private static Grid CreateGrid()
    {
        Grid grid = new Grid(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                grid.SetAreaType(new Vector2Int(x, y), GridCellAreaType.ExteriorPath);
            }
        }

        for (int x = 24; x < 72; x++)
        {
            grid.SetTerrainType(new Vector2Int(x, 1), GridCellTerrainType.ShallowWater);
        }

        return grid;
    }

    private static int RunQuery(Grid grid, int seed)
    {
        int floor = seed % Height;
        int startX = seed * 37 % Width;
        int endX = seed * 61 + 17;
        endX %= Width;
        if (startX == endX)
        {
            endX = (endX + Width / 2) % Width;
        }

        Vector2Int start = new Vector2Int(startX, floor);
        Vector2Int end = new Vector2Int(endX, floor);
        GridPathSearchResult result = grid.SearchPathTo(start, end);
        return result.GetMoveCostTo(end) + result.GetMovePathTo(end).Count;
    }

    private static bool VerifyWeightedDetour()
    {
        Grid grid = new Grid(7, 2);
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                grid.RegisterOccupant(
                    new TestOccupant(false, GridMoveType.Instant),
                    GridLayer.Hallway,
                    new[] { new Vector2Int(x, y) },
                    false);
            }
        }

        for (int x = 1; x < 6; x++)
        {
            grid.SetTerrainType(new Vector2Int(x, 0), GridCellTerrainType.ShallowWater);
        }

        AddMovement(grid, new Vector2Int(0, 0), new Vector2Int(0, 1));
        AddMovement(grid, new Vector2Int(6, 0), new Vector2Int(6, 1));

        Vector2Int start = new Vector2Int(0, 0);
        Vector2Int destination = new Vector2Int(6, 0);
        GridPathSearchResult result = grid.SearchPathTo(
            start,
            destination,
            costPolicy: new FastStairCostPolicy());
        Queue<GridMoveStep> path = result.GetMovePath(position => position == destination);
        return path.Count == 8
            && path.Count(step => step.MoveType == GridMoveType.Stair) == 2
            && path.All(step =>
                step.MoveType != GridMoveType.Walk
                || grid.GetGridCell(step.To)?.TerrainType != GridCellTerrainType.ShallowWater)
            && result.GetMoveCostTo(destination) == 800;
    }

    private static void AddMovement(Grid grid, Vector2Int from, Vector2Int to)
    {
        grid.RegisterOccupant(
            new TestOccupant(true, GridMoveType.Stair),
            GridLayer.Building,
            new[] { from, to },
            true);
    }

    private sealed class TestOccupant : IGridOccupant, IGridMovementOccupant
    {
        private static int nextId;

        public TestOccupant(bool isMovement, GridMoveType moveType)
        {
            GridId = ++nextId;
            IsGridMovement = isMovement;
            GridMoveType = moveType;
        }

        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement { get; }
        public GridMoveType GridMoveType { get; }
    }

    private sealed class FastStairCostPolicy : IGridTraversalCostPolicy
    {
        public int Version => 1;
        public int MinimumHorizontalCost => 100;

        public int GetTraversalCost(
            Grid grid,
            in GridTraversalStepData step,
            GridTraversalContext traversalContext)
        {
            if (step.MoveType == GridMoveType.Stair)
            {
                return 100;
            }

            return grid.GetGridCell(step.To)?.TerrainType == GridCellTerrainType.ShallowWater
                ? 300
                : 100;
        }
    }
}
