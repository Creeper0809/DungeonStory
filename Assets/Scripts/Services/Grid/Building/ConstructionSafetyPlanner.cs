using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ConstructionSafetyReason
{
    Safe = 0,
    NotBlocking = 1,
    Forced = 2,
    MissingContext = 3,
    WorkerEscapeBlocked = 4,
    BlocksPendingConstruction = 5,
    DoorRequired = 6,
    EntranceBlocked = 7
}

public readonly struct ConstructionSafetyResult
{
    public ConstructionSafetyResult(
        bool isSafe,
        ConstructionSafetyReason reason,
        string message,
        bool isForcedWarning = false)
    {
        IsSafe = isSafe;
        Reason = reason;
        Message = message ?? string.Empty;
        IsForcedWarning = isForcedWarning;
    }

    public bool IsSafe { get; }
    public ConstructionSafetyReason Reason { get; }
    public string Message { get; }
    public bool IsForcedWarning { get; }

    public static ConstructionSafetyResult Safe(string message = "안전")
    {
        return new ConstructionSafetyResult(true, ConstructionSafetyReason.Safe, message);
    }

    public static ConstructionSafetyResult Delay(ConstructionSafetyReason reason, string message)
    {
        return new ConstructionSafetyResult(false, reason, message);
    }

    public ConstructionSafetyResult AsForcedWarning()
    {
        return new ConstructionSafetyResult(
            true,
            ConstructionSafetyReason.Forced,
            "강제 공사: 갇힘 가능",
            true);
    }
}

public static class ConstructionSafetyPlanner
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.left,
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.down
    };

    public static ConstructionSafetyResult Evaluate(
        ConstructionSite site,
        CharacterActor worker,
        bool forced = false)
    {
        ConstructionSafetyResult result = EvaluateInternal(site, worker);
        return forced && !result.IsSafe ? result.AsForcedWarning() : result;
    }

    private static ConstructionSafetyResult EvaluateInternal(ConstructionSite site, CharacterActor worker)
    {
        if (site == null || site.isDestroy || site.TargetBuilding == null || site.Grid == null)
        {
            return ConstructionSafetyResult.Delay(
                ConstructionSafetyReason.MissingContext,
                "공사 정보가 부족합니다");
        }

        BuildingSO target = site.TargetBuilding;
        if (!WillBlockMovement(target))
        {
            return new ConstructionSafetyResult(
                true,
                ConstructionSafetyReason.NotBlocking,
                "안전: 이동을 막지 않는 공사");
        }

        Grid grid = site.Grid;
        HashSet<Vector2Int> futureBlocked = new HashSet<Vector2Int>(
            target.GetGridPosList(site.GridPosition));

        if (worker != null
            && !CanWorkerReachExitAfterBuild(grid, worker.GetNowXY(), futureBlocked))
        {
            return ConstructionSafetyResult.Delay(
                ConstructionSafetyReason.WorkerEscapeBlocked,
                "대기: 작업자 퇴로 차단");
        }

        if (WouldBlockEntrance(grid, futureBlocked))
        {
            return ConstructionSafetyResult.Delay(
                ConstructionSafetyReason.EntranceBlocked,
                "대기: 입구 또는 외부 동선 차단");
        }

        if (WouldSplitWalkableSpaceWithoutDoor(grid, futureBlocked))
        {
            return ConstructionSafetyResult.Delay(
                ConstructionSafetyReason.DoorRequired,
                "대기: 문 먼저 필요");
        }

        if (WouldBlockPendingConstructionAccess(grid, site, futureBlocked))
        {
            return ConstructionSafetyResult.Delay(
                ConstructionSafetyReason.BlocksPendingConstruction,
                "대기: 남은 공사 접근 차단");
        }

        return ConstructionSafetyResult.Safe("안전");
    }

    private static bool WillBlockMovement(BuildingSO building)
    {
        return building != null && building.IsStructuralWall && !building.IsDoor;
    }

    private static bool CanWorkerReachExitAfterBuild(
        Grid grid,
        Vector2Int workerPosition,
        HashSet<Vector2Int> futureBlocked)
    {
        if (grid == null)
        {
            return false;
        }

        if (!TryResolveSafeStart(grid, workerPosition, futureBlocked, out Vector2Int start))
        {
            return false;
        }

        if (!HasAnyExitCandidate(grid, futureBlocked))
        {
            return true;
        }

        if (CanReachExitByDirectRoute(grid, start, futureBlocked))
        {
            return true;
        }

        return FloodFillUntil(
            grid,
            start,
            futureBlocked,
            position => IsExitCell(grid, position));
    }

    private static bool WouldBlockEntrance(Grid grid, HashSet<Vector2Int> futureBlocked)
    {
        if (grid == null)
        {
            return false;
        }

        return futureBlocked.Any(position =>
        {
            GridCell cell = grid.GetGridCell(position);
            return cell != null
                && (cell.AreaType == GridCellAreaType.Entrance
                    || cell.AreaType == GridCellAreaType.DropZone
                    || cell.AreaType == GridCellAreaType.ExteriorPath);
        });
    }

    private static bool WouldSplitWalkableSpaceWithoutDoor(
        Grid grid,
        HashSet<Vector2Int> futureBlocked)
    {
        foreach (Vector2Int blocked in futureBlocked)
        {
            if (HasDoorOrPendingDoorNearby(grid, blocked))
            {
                continue;
            }

            if (WouldSeparatePair(grid, blocked + Vector2Int.left, blocked + Vector2Int.right, futureBlocked)
                || WouldSeparatePair(grid, blocked + Vector2Int.up, blocked + Vector2Int.down, futureBlocked))
            {
                return true;
            }
        }

        return false;
    }

    private static bool WouldSeparatePair(
        Grid grid,
        Vector2Int a,
        Vector2Int b,
        HashSet<Vector2Int> futureBlocked)
    {
        if (!IsWalkableAfterBuild(grid, a, null)
            || !IsWalkableAfterBuild(grid, b, null))
        {
            return false;
        }

        if (HasBoundedLocalRoute(grid, a, b, futureBlocked, 4))
        {
            return false;
        }

        if (!FloodFillUntil(grid, a, null, position => position == b))
        {
            return false;
        }

        return !FloodFillUntil(grid, a, futureBlocked, position => position == b);
    }

    private static bool WouldBlockPendingConstructionAccess(
        Grid grid,
        ConstructionSite currentSite,
        HashSet<Vector2Int> futureBlocked)
    {
        IEnumerable<BuildableObject> registeredBuildings =
            currentSite.WorldRegistry?.Buildings ?? Array.Empty<BuildableObject>();
        foreach (ConstructionSite other in registeredBuildings.OfType<ConstructionSite>())
        {
            if (other == null
                || other == currentSite
                || other.isDestroy
                || other.TargetBuilding == null)
            {
                continue;
            }

            if (!HasReachableStandCellAfterBuild(grid, other, futureBlocked))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasReachableStandCellAfterBuild(
        Grid grid,
        ConstructionSite site,
        HashSet<Vector2Int> futureBlocked)
    {
        if (grid == null || site == null)
        {
            return false;
        }

        foreach (Vector2Int position in GetStandCandidates(site))
        {
            if (IsWalkableAfterBuild(grid, position, futureBlocked)
                && (!HasAnyExitCandidate(grid, futureBlocked)
                    || CanReachExitByDirectRoute(grid, position, futureBlocked)
                    || FloodFillUntil(grid, position, futureBlocked, candidate => IsExitCell(grid, candidate))))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Vector2Int> GetStandCandidates(ConstructionSite site)
    {
        foreach (Vector2Int position in site.buildPoses ?? Array.Empty<Vector2Int>())
        {
            yield return position;
            foreach (Vector2Int direction in CardinalDirections)
            {
                yield return position + direction;
            }
        }
    }

    private static bool TryResolveSafeStart(
        Grid grid,
        Vector2Int workerPosition,
        HashSet<Vector2Int> futureBlocked,
        out Vector2Int start)
    {
        if (IsWalkableAfterBuild(grid, workerPosition, futureBlocked))
        {
            start = workerPosition;
            return true;
        }

        foreach (Vector2Int direction in CardinalDirections)
        {
            Vector2Int candidate = workerPosition + direction;
            if (IsWalkableAfterBuild(grid, candidate, futureBlocked))
            {
                start = candidate;
                return true;
            }
        }

        start = default;
        return false;
    }

    private static bool HasAnyExitCandidate(Grid grid, HashSet<Vector2Int> futureBlocked)
    {
        if (grid == null
            || !grid.TryGetAnyWalkableExit(out Vector2Int cachedExit))
        {
            return false;
        }

        if (futureBlocked == null || !futureBlocked.Contains(cachedExit))
        {
            return true;
        }

        for (int y = 0; y < grid.height; y++)
        {
            Vector2Int left = new Vector2Int(0, y);
            if (IsWalkableAfterBuild(grid, left, futureBlocked))
            {
                return true;
            }

            Vector2Int right = new Vector2Int(grid.width - 1, y);
            if (IsWalkableAfterBuild(grid, right, futureBlocked))
            {
                return true;
            }
        }

        return grid.GetCells().Any(cell =>
            cell != null
            && IsExitCell(grid, cell.Position)
            && IsWalkableAfterBuild(grid, cell.Position, futureBlocked));
    }

    private static bool IsExitCell(Grid grid, Vector2Int position)
    {
        GridCell cell = grid?.GetGridCell(position);
        if (cell == null)
        {
            return false;
        }

        return cell.AreaType == GridCellAreaType.Entrance
            || cell.AreaType == GridCellAreaType.DropZone
            || cell.AreaType == GridCellAreaType.ExteriorPath
            || position.x == 0
            || position.x == grid.width - 1;
    }

    private static bool IsWalkableAfterBuild(
        Grid grid,
        Vector2Int position,
        HashSet<Vector2Int> futureBlocked)
    {
        if (grid == null || !grid.IsValidGridPos(position))
        {
            return false;
        }

        if (futureBlocked != null && futureBlocked.Contains(position))
        {
            return false;
        }

        return grid.IsWalkable(position);
    }

    private static bool FloodFillUntil(
        Grid grid,
        Vector2Int start,
        HashSet<Vector2Int> futureBlocked,
        Func<Vector2Int, bool> success)
    {
        if (!IsWalkableAfterBuild(grid, start, futureBlocked))
        {
            return false;
        }

        Queue<Vector2Int> open = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        open.Enqueue(start);
        visited.Add(start);

        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();
            if (success != null && success(current))
            {
                return true;
            }

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = current + direction;
                if (visited.Contains(next)
                    || !IsWalkableAfterBuild(grid, next, futureBlocked))
                {
                    continue;
                }

                visited.Add(next);
                open.Enqueue(next);
            }
        }

        return false;
    }

    private static bool CanReachExitByDirectRoute(
        Grid grid,
        Vector2Int start,
        HashSet<Vector2Int> futureBlocked)
    {
        if (grid == null
            || !IsWalkableAfterBuild(grid, start, futureBlocked))
        {
            return false;
        }

        Vector2Int leftExit = new Vector2Int(0, start.y);
        if (IsDirectLineWalkable(grid, start, leftExit, futureBlocked))
        {
            return true;
        }

        Vector2Int rightExit = new Vector2Int(grid.width - 1, start.y);
        if (IsDirectLineWalkable(grid, start, rightExit, futureBlocked))
        {
            return true;
        }

        if (!grid.TryGetAnyWalkableExit(out Vector2Int exit)
            || (futureBlocked != null && futureBlocked.Contains(exit)))
        {
            return false;
        }

        Vector2Int horizontalPivot = new Vector2Int(exit.x, start.y);
        if (IsDirectLineWalkable(grid, start, horizontalPivot, futureBlocked)
            && IsDirectLineWalkable(grid, horizontalPivot, exit, futureBlocked))
        {
            return true;
        }

        Vector2Int verticalPivot = new Vector2Int(start.x, exit.y);
        return IsDirectLineWalkable(grid, start, verticalPivot, futureBlocked)
            && IsDirectLineWalkable(grid, verticalPivot, exit, futureBlocked);
    }

    private static bool IsDirectLineWalkable(
        Grid grid,
        Vector2Int start,
        Vector2Int end,
        HashSet<Vector2Int> futureBlocked)
    {
        if (start.x != end.x && start.y != end.y)
        {
            return false;
        }

        int stepX = Math.Sign(end.x - start.x);
        int stepY = Math.Sign(end.y - start.y);
        Vector2Int current = start;
        while (true)
        {
            if (!IsWalkableAfterBuild(grid, current, futureBlocked))
            {
                return false;
            }

            if (current == end)
            {
                return true;
            }

            current += new Vector2Int(stepX, stepY);
        }
    }

    private static bool HasBoundedLocalRoute(
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        HashSet<Vector2Int> futureBlocked,
        int margin)
    {
        if (!IsWalkableAfterBuild(grid, start, futureBlocked)
            || !IsWalkableAfterBuild(grid, destination, futureBlocked))
        {
            return false;
        }

        int minX = Mathf.Min(start.x, destination.x) - margin;
        int maxX = Mathf.Max(start.x, destination.x) + margin;
        int minY = Mathf.Min(start.y, destination.y) - margin;
        int maxY = Mathf.Max(start.y, destination.y) + margin;
        Queue<Vector2Int> open = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        open.Enqueue(start);
        visited.Add(start);
        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();
            if (current == destination)
            {
                return true;
            }

            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = current + direction;
                if (next.x < minX
                    || next.x > maxX
                    || next.y < minY
                    || next.y > maxY
                    || !visited.Add(next)
                    || !IsWalkableAfterBuild(grid, next, futureBlocked))
                {
                    continue;
                }

                open.Enqueue(next);
            }
        }

        return false;
    }

    private static bool HasDoorOrPendingDoorNearby(Grid grid, Vector2Int center)
    {
        foreach (Vector2Int direction in CardinalDirections)
        {
            Vector2Int position = center + direction;
            GridCell cell = grid?.GetGridCell(position);
            BuildableObject building = cell?.GetOccupant(GridLayer.Building) as BuildableObject;
            if (building != null && building.BuildingData != null && building.BuildingData.IsDoor)
            {
                return true;
            }

            ConstructionSite site = cell?.GetOccupant(GridLayer.Construction) as ConstructionSite;
            if (site != null && site.TargetBuilding != null && site.TargetBuilding.IsDoor)
            {
                return true;
            }
        }

        return false;
    }
}
