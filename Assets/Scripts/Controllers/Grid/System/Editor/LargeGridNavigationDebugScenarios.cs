using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class LargeGridNavigationDebugScenarios
{
    private const string DefaultReportPath =
        "docs/implementation-reports/navigation-large-grid-baseline.json";

    [Serializable]
    private sealed class BenchmarkSuite
    {
        public bool valid;
        public string utc;
        public string processor;
        public int processorCount;
        public int systemMemoryMb;
        public List<SizeResult> sizes = new List<SizeResult>();
    }

    [Serializable]
    private sealed class SizeResult
    {
        public int width;
        public int height;
        public int cellCount;
        public bool valid;
        public double constructionMs;
        public double walkableSetupMs;
        public double managedMemoryMb;
        public SearchResult local;
        public SearchResult horizontal;
        public SearchResult vertical;
        public SearchResult weightedDetour;
        public SearchResult obstacleDetour;
        public SearchResult unreachable;
        public SearchResult repeatedAverage;
        public CacheResult cache;
        public BatchResult burst500;
        public BatchResult budgeted500;
        public string failure;
    }

    [Serializable]
    private sealed class SearchResult
    {
        public bool reachable;
        public bool costValid;
        public int pathLength;
        public int moveCost;
        public int expandedNodes;
        public double elapsedMs;
    }

    [Serializable]
    private sealed class CacheResult
    {
        public bool valid;
        public int initialSearches;
        public int repeatCacheHits;
        public int invalidatedSearches;
        public int originalCost;
        public int changedCost;
    }

    [Serializable]
    private sealed class BatchResult
    {
        public bool valid;
        public int requests;
        public int frames;
        public int maxSearchesPerFrame;
        public int averageExpandedNodes;
        public int maxExpandedNodes;
        public double totalMs;
        public double averageMs;
        public double p95Ms;
        public double maxMs;
    }

    private sealed class BenchmarkClock : IGameClock
    {
        public float DeltaTime => 1f / 60f;
        public float Time => FrameCount * DeltaTime;
        public int FrameCount { get; set; }
        public bool IsPaused => false;
    }

    public static void RunBaselineBatch()
    {
        int[] sizes = ParseSizes(
            Environment.GetEnvironmentVariable("DUNGEON_NAV_BENCH_SIZES"));
        string reportPath =
            Environment.GetEnvironmentVariable("DUNGEON_NAV_BENCH_REPORT");
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            reportPath = DefaultReportPath;
        }

        BenchmarkSuite suite = Run(sizes);
        string fullPath = Path.IsPathRooted(reportPath)
            ? reportPath
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), reportPath));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());
        File.WriteAllText(fullPath, JsonUtility.ToJson(suite, true));
        Debug.Log(
            $"Large Grid navigation baseline valid={suite.valid}; "
            + $"sizes={string.Join(",", sizes)}; report={fullPath}");
        AssetDatabase.Refresh();
        EditorApplication.Exit(suite.valid ? 0 : 1);
    }

    private static BenchmarkSuite Run(IReadOnlyList<int> sizes)
    {
        BenchmarkSuite suite = new BenchmarkSuite
        {
            valid = true,
            utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            processor = SystemInfo.processorType,
            processorCount = SystemInfo.processorCount,
            systemMemoryMb = SystemInfo.systemMemorySize
        };

        for (int index = 0; index < sizes.Count; index++)
        {
            SizeResult result = RunSize(sizes[index]);
            suite.sizes.Add(result);
            suite.valid &= result.valid;
            Debug.Log(Format(result));
        }

        return suite;
    }

    private static SizeResult RunSize(int size)
    {
        SizeResult result = new SizeResult
        {
            width = size,
            height = size,
            cellCount = checked(size * size)
        };

        Grid grid = null;
        try
        {
            ForceCollection();
            long memoryBefore = GC.GetTotalMemory(true);
            Stopwatch stopwatch = Stopwatch.StartNew();
            grid = new Grid(size, size);
            stopwatch.Stop();
            result.constructionMs = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            MakeWalkableAndAddFloorLinks(grid);
            grid.RefreshTraversalHeuristicMetadata();
            stopwatch.Stop();
            result.walkableSetupMs = stopwatch.Elapsed.TotalMilliseconds;
            ForceCollection();
            result.managedMemoryMb =
                (GC.GetTotalMemory(true) - memoryBefore) / (1024.0 * 1024.0);

            int middleY = size / 2;
            result.local = Measure(
                grid,
                new Vector2Int(0, middleY),
                new Vector2Int(Mathf.Min(size - 1, 31), middleY));
            result.horizontal = Measure(
                grid,
                new Vector2Int(0, middleY),
                new Vector2Int(size - 1, middleY));
            result.vertical = Measure(
                grid,
                new Vector2Int(0, 0),
                new Vector2Int(0, size - 1));
            result.unreachable = MeasureUnreachable(grid, middleY);
            result.cache = MeasureCacheAndInvalidation(grid, middleY);
            result.weightedDetour = MeasureWeightedDetour(grid, middleY);
            result.obstacleDetour = MeasureObstacleDetour(grid, middleY);
            result.repeatedAverage = MeasureRepeated(
                grid,
                Mathf.Clamp(16, 1, size - 1));
            result.burst500 = MeasureBurst500(grid);
            result.budgeted500 = MeasureBudgeted500(grid);

            result.valid = result.local.reachable
                && result.horizontal.reachable
                && result.vertical.reachable
                && result.weightedDetour.reachable
                && result.weightedDetour.costValid
                && result.obstacleDetour.reachable
                && result.obstacleDetour.costValid
                && !result.unreachable.reachable
                && result.unreachable.costValid
                && result.repeatedAverage.reachable
                && result.cache.valid
                && result.burst500.valid
                && result.budgeted500.valid;
        }
        catch (Exception exception)
        {
            result.valid = false;
            result.failure = exception.ToString();
            Debug.LogException(exception);
        }
        finally
        {
            grid = null;
            Grid.ReleaseRetainedSearchMemoryForDiagnostics();
            ForceCollection();
        }

        return result;
    }

    private static void MakeWalkableAndAddFloorLinks(Grid grid)
    {
        int lastY = grid.height - 1;
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                grid.GetGridCell(new Vector2Int(x, y))
                    .SetAreaType(GridCellAreaType.ExteriorPath);
            }

            List<GridTraversalLink> links = new List<GridTraversalLink>(2);
            if (y > 0)
            {
                links.Add(new GridTraversalLink(
                    new Vector2Int(0, y - 1),
                    null,
                    GridMoveType.Stair));
            }

            if (y < lastY)
            {
                links.Add(new GridTraversalLink(
                    new Vector2Int(0, y + 1),
                    null,
                    GridMoveType.Stair));
            }

            grid.GetGridCell(new Vector2Int(0, y)).SetTraversalLinks(links);
        }
    }

    private static SearchResult Measure(
        Grid grid,
        Vector2Int start,
        Vector2Int destination)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        GridPathSearchResult search = grid.SearchPathTo(start, destination);
        Queue<GridMoveStep> path = search.GetMovePathTo(destination);
        stopwatch.Stop();
        int cost = search.GetMoveCostTo(destination);
        return new SearchResult
        {
            reachable = cost != int.MaxValue,
            costValid = true,
            pathLength = path?.Count ?? 0,
            moveCost = cost,
            expandedNodes = search.ExpandedNodeCount,
            elapsedMs = stopwatch.Elapsed.TotalMilliseconds
        };
    }

    private static SearchResult MeasureWeightedDetour(Grid grid, int row)
    {
        int startX = Mathf.Max(0, grid.width / 4);
        int endX = Mathf.Min(grid.width - 1, grid.width * 3 / 4);
        if (endX - startX < 8 || row + 1 >= grid.height)
        {
            return Measure(
                grid,
                new Vector2Int(0, row),
                new Vector2Int(grid.width - 1, row));
        }

        for (int x = startX + 1; x < endX; x++)
        {
            grid.GetGridCell(new Vector2Int(x, row))
                .SetTerrainType(GridCellTerrainType.ShallowWater);
        }

        AddBidirectionalStair(grid, new Vector2Int(startX, row));
        AddBidirectionalStair(grid, new Vector2Int(endX, row));
        grid.RefreshTraversalHeuristicMetadata();
        SearchResult result = Measure(
            grid,
            new Vector2Int(0, row),
            new Vector2Int(grid.width - 1, row));
        int directDryCost = (grid.width - 1) * DefaultGridTraversalCostPolicy.DryWalkCost;
        int directWetCost = directDryCost
            + (endX - startX - 1)
            * (Mathf.CeilToInt(
                    DefaultGridTraversalCostPolicy.DryWalkCost / 0.65f)
                - DefaultGridTraversalCostPolicy.DryWalkCost);
        result.costValid = result.moveCost < directWetCost
            && result.moveCost >= directDryCost;
        return result;
    }

    private static SearchResult MeasureObstacleDetour(Grid grid, int row)
    {
        int startX = Mathf.Max(0, grid.width / 4);
        int endX = Mathf.Min(grid.width - 1, grid.width * 3 / 4);
        Vector2Int start = new Vector2Int(0, row);
        Vector2Int destination = new Vector2Int(grid.width - 1, row);
        Stopwatch stopwatch = Stopwatch.StartNew();
        GridPathSearchResult search = grid.SearchPathTo(
            start,
            destination,
            position => position.y != row
                || position.x <= startX
                || position.x >= endX);
        Queue<GridMoveStep> path = search.GetMovePathTo(destination);
        stopwatch.Stop();
        int cost = search.GetMoveCostTo(destination);
        return new SearchResult
        {
            reachable = cost != int.MaxValue,
            costValid = path.Count > grid.width - 1,
            pathLength = path.Count,
            moveCost = cost,
            expandedNodes = search.ExpandedNodeCount,
            elapsedMs = stopwatch.Elapsed.TotalMilliseconds
        };
    }

    private static SearchResult MeasureUnreachable(Grid grid, int row)
    {
        int blockedX = grid.width / 2;
        Vector2Int start = new Vector2Int(1, row);
        Vector2Int destination = new Vector2Int(grid.width - 2, row);
        Stopwatch stopwatch = Stopwatch.StartNew();
        GridPathSearchResult search = grid.SearchPathTo(
            start,
            destination,
            position => position.y == row && position.x != blockedX);
        Queue<GridMoveStep> path = search.GetMovePathTo(destination);
        stopwatch.Stop();
        int cost = search.GetMoveCostTo(destination);
        return new SearchResult
        {
            reachable = cost != int.MaxValue,
            costValid = cost == int.MaxValue && path.Count == 0,
            pathLength = path.Count,
            moveCost = cost,
            expandedNodes = search.ExpandedNodeCount,
            elapsedMs = stopwatch.Elapsed.TotalMilliseconds
        };
    }

    private static CacheResult MeasureCacheAndInvalidation(Grid grid, int row)
    {
        BenchmarkClock clock = new BenchmarkClock();
        GridPathSearchBroker broker = new GridPathSearchBroker(clock);
        Vector2Int start = new Vector2Int(0, row);
        Vector2Int destination = new Vector2Int(grid.width - 1, row);

        broker.BeginFrame(8, true);
        Queue<GridMoveStep> first = broker.GetMovePathTo(grid, start, destination);
        int initialSearches = broker.SearchesThisFrame;
        int originalCost = grid.SearchPathTo(start, destination).GetMoveCostTo(destination);

        Queue<GridMoveStep> repeated = broker.GetMovePathTo(grid, start, destination);
        int repeatCacheHits = broker.CacheHitsThisFrame;

        Vector2Int changed = new Vector2Int(grid.width / 2, row);
        grid.SetTerrainType(changed, GridCellTerrainType.ShallowWater);
        clock.FrameCount++;
        broker.BeginFrame(8, true);
        Queue<GridMoveStep> invalidated = broker.GetMovePathTo(grid, start, destination);
        int invalidatedSearches = broker.SearchesThisFrame;
        int changedCost = grid.SearchPathTo(start, destination).GetMoveCostTo(destination);
        grid.SetTerrainType(changed, GridCellTerrainType.Dry);
        broker.Clear();

        return new CacheResult
        {
            valid = first != null
                && repeated != null
                && invalidated != null
                && initialSearches == 1
                && repeatCacheHits == 1
                && invalidatedSearches == 1
                && changedCost > originalCost,
            initialSearches = initialSearches,
            repeatCacheHits = repeatCacheHits,
            invalidatedSearches = invalidatedSearches,
            originalCost = originalCost,
            changedCost = changedCost
        };
    }

    private static void AddBidirectionalStair(Grid grid, Vector2Int lower)
    {
        Vector2Int upper = lower + Vector2Int.up;
        AppendLink(
            grid.GetGridCell(lower),
            new GridTraversalLink(upper, null, GridMoveType.Stair));
        AppendLink(
            grid.GetGridCell(upper),
            new GridTraversalLink(lower, null, GridMoveType.Stair));
    }

    private static void AppendLink(GridCell cell, GridTraversalLink link)
    {
        List<GridTraversalLink> links = new List<GridTraversalLink>(cell.TraversalLinks.Count + 1);
        for (int index = 0; index < cell.TraversalLinks.Count; index++)
        {
            links.Add(cell.TraversalLinks[index]);
        }

        links.Add(link);
        cell.SetTraversalLinks(links);
    }

    private static SearchResult MeasureRepeated(Grid grid, int requestCount)
    {
        int row = Mathf.Max(0, grid.height / 3);
        long totalTicks = 0;
        int totalExpanded = 0;
        int totalLength = 0;
        int lastCost = int.MaxValue;
        for (int index = 0; index < requestCount; index++)
        {
            int startX = index % Mathf.Max(1, grid.width / 4);
            int distance = Mathf.Min(
                grid.width - 1 - startX,
                32 + (index % 64));
            Vector2Int start = new Vector2Int(startX, row);
            Vector2Int destination = new Vector2Int(startX + distance, row);
            long started = Stopwatch.GetTimestamp();
            GridPathSearchResult search = grid.SearchPathTo(start, destination);
            Queue<GridMoveStep> path = search.GetMovePathTo(destination);
            totalTicks += Stopwatch.GetTimestamp() - started;
            totalExpanded += search.ExpandedNodeCount;
            totalLength += path.Count;
            lastCost = search.GetMoveCostTo(destination);
        }

        return new SearchResult
        {
            reachable = lastCost != int.MaxValue,
            costValid = true,
            pathLength = requestCount > 0 ? totalLength / requestCount : 0,
            moveCost = lastCost,
            expandedNodes = requestCount > 0 ? totalExpanded / requestCount : 0,
            elapsedMs = requestCount > 0
                ? totalTicks * 1000.0 / Stopwatch.Frequency / requestCount
                : 0.0
        };
    }

    private static BatchResult MeasureBurst500(Grid grid)
    {
        const int requestCount = 500;
        double[] elapsed = new double[requestCount];
        int expandedTotal = 0;
        int expandedMax = 0;
        bool valid = true;
        Stopwatch total = Stopwatch.StartNew();
        for (int index = 0; index < requestCount; index++)
        {
            ResolveRequest(grid, index, out Vector2Int start, out Vector2Int destination);
            long started = Stopwatch.GetTimestamp();
            GridPathSearchResult search = grid.SearchPathTo(start, destination);
            Queue<GridMoveStep> path = search.GetMovePathTo(destination);
            elapsed[index] =
                (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            int expanded = search.ExpandedNodeCount;
            expandedTotal += expanded;
            expandedMax = Mathf.Max(expandedMax, expanded);
            valid &= path.Count > 0 && search.GetMoveCostTo(destination) != int.MaxValue;
        }

        total.Stop();
        Array.Sort(elapsed);
        return new BatchResult
        {
            valid = valid,
            requests = requestCount,
            frames = 1,
            maxSearchesPerFrame = requestCount,
            averageExpandedNodes = expandedTotal / requestCount,
            maxExpandedNodes = expandedMax,
            totalMs = total.Elapsed.TotalMilliseconds,
            averageMs = Average(elapsed),
            p95Ms = Percentile(elapsed, 0.95),
            maxMs = elapsed[elapsed.Length - 1]
        };
    }

    private static BatchResult MeasureBudgeted500(Grid grid)
    {
        const int requestCount = 500;
        BenchmarkClock clock = new BenchmarkClock();
        GridPathSearchBroker broker = new GridPathSearchBroker(clock);
        List<double> frameTimes = new List<double>(80);
        int completed = 0;
        int maxSearches = 0;
        bool valid = true;
        while (completed < requestCount && clock.FrameCount < 256)
        {
            broker.BeginFrame(8, true);
            Stopwatch frame = Stopwatch.StartNew();
            while (completed < requestCount)
            {
                ResolveRequest(
                    grid,
                    completed,
                    out Vector2Int start,
                    out Vector2Int destination);
                Queue<GridMoveStep> path = broker.GetMovePathTo(
                    grid,
                    start,
                    destination);
                if (path == null)
                {
                    break;
                }

                valid &= path.Count > 0;
                completed++;
            }

            frame.Stop();
            frameTimes.Add(frame.Elapsed.TotalMilliseconds);
            maxSearches = Mathf.Max(maxSearches, broker.SearchesThisFrame);
            clock.FrameCount++;
        }

        frameTimes.Sort();
        double[] samples = frameTimes.ToArray();
        broker.Clear();
        return new BatchResult
        {
            valid = valid && completed == requestCount,
            requests = completed,
            frames = frameTimes.Count,
            maxSearchesPerFrame = maxSearches,
            averageExpandedNodes = 0,
            maxExpandedNodes = 0,
            totalMs = Sum(samples),
            averageMs = Average(samples),
            p95Ms = Percentile(samples, 0.95),
            maxMs = samples.Length > 0 ? samples[samples.Length - 1] : 0.0
        };
    }

    private static void ResolveRequest(
        Grid grid,
        int index,
        out Vector2Int start,
        out Vector2Int destination)
    {
        if (index % 5 == 0)
        {
            int startY = index % Mathf.Max(1, grid.height / 2);
            int distance = Mathf.Max(
                1,
                Mathf.Min(grid.height - 1 - startY, 64 + (index * 17) % 960));
            start = new Vector2Int(0, startY);
            destination = new Vector2Int(0, startY + distance);
            return;
        }

        int row = (index * 37) % grid.height;
        int startX = (index * 13) % Mathf.Max(1, grid.width / 4);
        int distanceX = Mathf.Max(
            1,
            Mathf.Min(grid.width - 1 - startX, 64 + (index * 29) % 960));
        start = new Vector2Int(startX, row);
        destination = new Vector2Int(startX + distanceX, row);
    }

    private static double Average(IReadOnlyList<double> values)
    {
        return values.Count > 0 ? Sum(values) / values.Count : 0.0;
    }

    private static double Sum(IReadOnlyList<double> values)
    {
        double total = 0.0;
        for (int index = 0; index < values.Count; index++)
        {
            total += values[index];
        }

        return total;
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0.0;
        }

        int index = Mathf.Clamp(
            Mathf.CeilToInt((float)(sortedValues.Count * percentile)) - 1,
            0,
            sortedValues.Count - 1);
        return sortedValues[index];
    }

    private static string Format(SizeResult result)
    {
        return $"Large Grid {result.width}x{result.height} valid={result.valid}; "
            + $"construct={result.constructionMs:F1}ms; "
            + $"setup={result.walkableSetupMs:F1}ms; "
            + $"managed={result.managedMemoryMb:F1}MB; "
            + $"local={Format(result.local)}; "
            + $"horizontal={Format(result.horizontal)}; "
            + $"vertical={Format(result.vertical)}; "
            + $"weighted={Format(result.weightedDetour)}; "
            + $"obstacle={Format(result.obstacleDetour)}; "
            + $"unreachable={Format(result.unreachable)}; "
            + $"repeatAvg={Format(result.repeatedAverage)}; "
            + $"burst500={Format(result.burst500)}; "
            + $"budgeted500={Format(result.budgeted500)}";
    }

    private static string Format(SearchResult result)
    {
        return result == null
            ? "n/a"
            : $"{result.elapsedMs:F3}ms/{result.expandedNodes} nodes/"
                + $"{result.pathLength} steps/{result.moveCost} cost";
    }

    private static string Format(BatchResult result)
    {
        return result == null
            ? "n/a"
            : $"{result.totalMs:F1}ms total/{result.averageMs:F3}ms avg/"
                + $"{result.p95Ms:F3}ms p95/{result.maxMs:F3}ms max/"
                + $"{result.frames} frames";
    }

    private static int[] ParseSizes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new[] { 128, 256, 512, 1024 };
        }

        string[] tokens = value.Split(',');
        List<int> sizes = new List<int>(tokens.Length);
        for (int index = 0; index < tokens.Length; index++)
        {
            if (int.TryParse(
                    tokens[index].Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsed)
                && parsed >= 8
                && parsed <= 1024)
            {
                sizes.Add(parsed);
            }
        }

        return sizes.Count > 0 ? sizes.ToArray() : new[] { 128, 256, 512, 1024 };
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
