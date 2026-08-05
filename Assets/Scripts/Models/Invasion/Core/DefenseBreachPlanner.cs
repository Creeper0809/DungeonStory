using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct DefenseBreachTargetSnapshot
{
    public DefenseBreachTargetSnapshot(
        BuildingInstanceId targetId,
        IReadOnlyList<Vector2Int> occupiedCells,
        bool destroyed,
        float currentHitPoints,
        float maxHitPoints,
        float toughness,
        bool breachable)
    {
        TargetId = targetId;
        OccupiedCells = Array.AsReadOnly(
            (occupiedCells ?? Array.Empty<Vector2Int>()).ToArray());
        Destroyed = destroyed;
        CurrentHitPoints = Mathf.Max(0f, currentHitPoints);
        MaxHitPoints = Mathf.Max(1f, maxHitPoints);
        Toughness = Mathf.Max(0f, toughness);
        Breachable = breachable;
    }

    public BuildingInstanceId TargetId { get; }
    public IReadOnlyList<Vector2Int> OccupiedCells { get; }
    public bool Destroyed { get; }
    public float CurrentHitPoints { get; }
    public float MaxHitPoints { get; }
    public float Toughness { get; }
    public bool Breachable { get; }
}

public enum DefenseBreachDamageFailureCode
{
    None = 0,
    InvalidTarget = 1,
    TargetNotFound = 2,
    DamageRejected = 3
}

public readonly struct DefenseBreachDamageSnapshot
{
    public DefenseBreachDamageSnapshot(
        BuildingInstanceId targetId,
        bool applied,
        bool destroyed,
        float damage,
        DefenseBreachTargetSnapshot target,
        DefenseBreachDamageFailureCode failureCode)
    {
        TargetId = targetId;
        Applied = applied;
        Destroyed = destroyed;
        Damage = Mathf.Max(0f, damage);
        Target = target;
        FailureCode = failureCode;
    }

    public BuildingInstanceId TargetId { get; }
    public bool Applied { get; }
    public bool Destroyed { get; }
    public float Damage { get; }
    public DefenseBreachTargetSnapshot Target { get; }
    public DefenseBreachDamageFailureCode FailureCode { get; }
}

public interface IDefenseBreachTargetQuery
{
    bool TryGetTargetAt(
        Vector2Int position,
        out DefenseBreachTargetSnapshot target);
    bool TryGetTarget(
        BuildingInstanceId targetId,
        out DefenseBreachTargetSnapshot target);
}

public interface IDefenseBreachTargetCommand
{
    DefenseBreachDamageSnapshot ApplyDamage(
        BuildingInstanceId targetId,
        float damage);
}

public sealed class DefenseBreachPlanSnapshot
{
    public DefenseBreachPlanSnapshot(
        DefenseBreachTargetSnapshot target,
        Vector2Int attackCell,
        Queue<GridMoveStep> approachPath,
        IReadOnlyList<Vector2Int> virtualPath,
        float totalCost)
    {
        Target = target;
        AttackCell = attackCell;
        ApproachPath = approachPath ?? new Queue<GridMoveStep>();
        VirtualPath = virtualPath ?? Array.Empty<Vector2Int>();
        TotalCost = Mathf.Max(0f, totalCost);
    }

    public DefenseBreachTargetSnapshot Target { get; }
    public Vector2Int AttackCell { get; }
    public Queue<GridMoveStep> ApproachPath { get; }
    public IReadOnlyList<Vector2Int> VirtualPath { get; }
    public float TotalCost { get; }
}

public sealed class DefenseBreachPlanningRules
{
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.left,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.up
    };

    private readonly Dictionary<BuildingInstanceId, Dictionary<string, Vector2Int>>
        targetReservations = new();
    private readonly Dictionary<string, BuildingInstanceId> targetByIntruder =
        new(StringComparer.Ordinal);

    public Queue<GridMoveStep> GetRiskAwarePath(
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        IGridPathSearchBroker pathSearchBroker,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        float riskTolerance)
    {
        if (grid == null
            || pathSearchBroker == null
            || !grid.IsValidGridPos(start)
            || !grid.IsValidGridPos(destination)
            || !pathSearchBroker.TryGetSearch(
                grid,
                start,
                out GridPathSearchResult reachable))
        {
            return new Queue<GridMoveStep>();
        }

        if (start == destination || !reachable.ContainsPosition(destination))
        {
            return new Queue<GridMoveStep>();
        }

        int nodeCount = grid.width * grid.height;
        float[] costs = CreateCosts(nodeCount);
        int[] parents = CreateParents(nodeCount);
        bool[] closed = new bool[nodeCount];
        grid.TryGetCellIndex(start, out int startIndex);
        grid.TryGetCellIndex(destination, out int destinationIndex);
        MinHeap open = new();
        costs[startIndex] = 0f;
        open.Push(startIndex, 0f);
        while (open.Count > 0)
        {
            HeapNode current = open.Pop();
            if (closed[current.Index]
                || current.Cost > costs[current.Index] + 0.001f)
            {
                continue;
            }

            closed[current.Index] = true;
            if (current.Index == destinationIndex)
            {
                break;
            }

            Vector2Int position = grid.GetPositionFromCellIndex(current.Index);
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = position + direction;
                if (!grid.TryGetCellIndex(next, out int nextIndex)
                    || closed[nextIndex]
                    || !reachable.ContainsPosition(next))
                {
                    continue;
                }

                float risk = knownRisks != null
                    && knownRisks.TryGetValue(next, out float severity)
                        ? Mathf.Max(0f, severity)
                        : 0f;
                float candidate = costs[current.Index]
                    + 10f
                    + risk * Mathf.Clamp01(1f - riskTolerance) * 30f;
                if (candidate + 0.001f >= costs[nextIndex])
                {
                    continue;
                }

                costs[nextIndex] = candidate;
                parents[nextIndex] = current.Index;
                open.Push(nextIndex, candidate);
            }
        }

        if (float.IsPositiveInfinity(costs[destinationIndex]))
        {
            return new Queue<GridMoveStep>();
        }

        List<Vector2Int> reverse = new();
        for (int cursor = destinationIndex;
             cursor >= 0 && cursor != startIndex;
             cursor = parents[cursor])
        {
            reverse.Add(grid.GetPositionFromCellIndex(cursor));
        }
        reverse.Reverse();

        Queue<GridMoveStep> path = new();
        Vector2Int from = start;
        foreach (Vector2Int to in reverse)
        {
            path.Enqueue(new GridMoveStep(
                from,
                to,
                grid.GetGridCell(to)?.GetTopOccupant(),
                null,
                GridMoveType.Walk));
            from = to;
        }
        return path;
    }

    public bool TryPlan(
        string intruderId,
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        IGridPathSearchBroker pathSearchBroker,
        IDefenseBreachTargetQuery targetQuery,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        float riskTolerance,
        float estimatedStructureDamage,
        out DefenseBreachPlanSnapshot plan)
    {
        plan = null;
        if (string.IsNullOrWhiteSpace(intruderId)
            || grid == null
            || pathSearchBroker == null
            || targetQuery == null
            || !grid.IsValidGridPos(start)
            || !grid.IsValidGridPos(destination))
        {
            return false;
        }

        Queue<GridMoveStep> ordinaryPath = pathSearchBroker.GetMovePathTo(
            grid,
            start,
            destination,
            GridPathSearchPriority.Urgent);
        if (ordinaryPath == null || ordinaryPath.Count > 0 || start == destination)
        {
            return false;
        }

        if (!TryFindVirtualPath(
                grid,
                start,
                destination,
                targetQuery,
                knownRisks,
                riskTolerance,
                estimatedStructureDamage,
                out List<Vector2Int> virtualPath,
                out DefenseBreachTargetSnapshot target,
                out float cost))
        {
            ReleaseReservation(intruderId);
            return false;
        }

        if (!TryReserveAttackCell(
                intruderId,
                grid,
                start,
                target,
                pathSearchBroker,
                out Vector2Int attackCell,
                out Queue<GridMoveStep> approachPath))
        {
            return false;
        }

        plan = new DefenseBreachPlanSnapshot(
            target,
            attackCell,
            approachPath,
            virtualPath,
            cost);
        return true;
    }

    public void ReleaseReservation(string intruderId)
    {
        if (string.IsNullOrWhiteSpace(intruderId)
            || !targetByIntruder.TryGetValue(
                intruderId,
                out BuildingInstanceId targetId))
        {
            return;
        }

        targetByIntruder.Remove(intruderId);
        if (!targetReservations.TryGetValue(
                targetId,
                out Dictionary<string, Vector2Int> reservations))
        {
            return;
        }

        reservations.Remove(intruderId);
        if (reservations.Count == 0)
        {
            targetReservations.Remove(targetId);
        }
    }

    public int GetReservedAttackerCount(BuildingInstanceId targetId)
    {
        return targetId.IsValid
            && targetReservations.TryGetValue(
                targetId,
                out Dictionary<string, Vector2Int> reservations)
            ? reservations.Count
            : 0;
    }

    private bool TryFindVirtualPath(
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        IDefenseBreachTargetQuery targetQuery,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        float riskTolerance,
        float estimatedStructureDamage,
        out List<Vector2Int> path,
        out DefenseBreachTargetSnapshot firstStructure,
        out float totalCost)
    {
        path = null;
        firstStructure = default;
        totalCost = 0f;
        int nodeCount = grid.width * grid.height;
        float[] costs = CreateCosts(nodeCount);
        int[] parents = CreateParents(nodeCount);
        bool[] closed = new bool[nodeCount];
        if (!grid.TryGetCellIndex(start, out int startIndex)
            || !grid.TryGetCellIndex(destination, out int destinationIndex))
        {
            return false;
        }

        MinHeap open = new();
        costs[startIndex] = 0f;
        open.Push(startIndex, 0f);
        while (open.Count > 0)
        {
            HeapNode current = open.Pop();
            if (closed[current.Index]
                || current.Cost > costs[current.Index] + 0.001f)
            {
                continue;
            }

            closed[current.Index] = true;
            if (current.Index == destinationIndex)
            {
                break;
            }

            Vector2Int position = grid.GetPositionFromCellIndex(current.Index);
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int next = position + direction;
                if (!grid.TryGetCellIndex(next, out int nextIndex)
                    || closed[nextIndex]
                    || !TryGetTraversalCost(
                        grid,
                        next,
                        targetQuery,
                        knownRisks,
                        riskTolerance,
                        estimatedStructureDamage,
                        out float stepCost))
                {
                    continue;
                }

                float candidate = costs[current.Index] + stepCost;
                if (candidate + 0.001f >= costs[nextIndex])
                {
                    continue;
                }

                costs[nextIndex] = candidate;
                parents[nextIndex] = current.Index;
                open.Push(nextIndex, candidate);
            }
        }

        if (float.IsPositiveInfinity(costs[destinationIndex]))
        {
            return false;
        }

        List<Vector2Int> reverse = new();
        int cursor = destinationIndex;
        while (cursor >= 0)
        {
            reverse.Add(grid.GetPositionFromCellIndex(cursor));
            if (cursor == startIndex)
            {
                break;
            }
            cursor = parents[cursor];
        }

        if (reverse.Count == 0 || reverse[^1] != start)
        {
            return false;
        }

        reverse.Reverse();
        foreach (Vector2Int position in reverse)
        {
            if (TryGetStructuralTarget(targetQuery, position, out firstStructure))
            {
                break;
            }
        }

        if (!firstStructure.TargetId.IsValid)
        {
            return false;
        }

        path = reverse;
        totalCost = costs[destinationIndex];
        return true;
    }

    private bool TryReserveAttackCell(
        string intruderId,
        Grid grid,
        Vector2Int start,
        DefenseBreachTargetSnapshot target,
        IGridPathSearchBroker pathSearchBroker,
        out Vector2Int attackCell,
        out Queue<GridMoveStep> approachPath)
    {
        attackCell = start;
        approachPath = new Queue<GridMoveStep>();
        if (!target.TargetId.IsValid || target.Destroyed)
        {
            return false;
        }

        BuildingInstanceId targetId = target.TargetId;
        if (targetByIntruder.TryGetValue(
                intruderId,
                out BuildingInstanceId previousTarget)
            && !previousTarget.Equals(targetId))
        {
            ReleaseReservation(intruderId);
        }

        if (!targetReservations.TryGetValue(
                targetId,
                out Dictionary<string, Vector2Int> reservations))
        {
            reservations = new Dictionary<string, Vector2Int>(
                StringComparer.Ordinal);
            targetReservations.Add(targetId, reservations);
        }

        if (reservations.TryGetValue(intruderId, out Vector2Int existing))
        {
            attackCell = existing;
            approachPath = pathSearchBroker.GetMovePathTo(
                grid,
                start,
                existing,
                GridPathSearchPriority.Urgent)
                ?? new Queue<GridMoveStep>();
            return start == existing || approachPath.Count > 0;
        }

        HashSet<Vector2Int> occupied = new(reservations.Values);
        HashSet<Vector2Int> structureCells = new(target.OccupiedCells);
        List<Vector2Int> candidates = new();
        foreach (Vector2Int structureCell in target.OccupiedCells)
        {
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int candidate = structureCell + direction;
                if (!grid.IsValidGridPos(candidate)
                    || !grid.IsWalkable(candidate)
                    || occupied.Contains(candidate)
                    || structureCells.Contains(candidate))
                {
                    continue;
                }

                if (!candidates.Contains(candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        foreach (Vector2Int candidate in candidates
                     .OrderBy(value => Mathf.Abs(value.x - start.x)
                         + Mathf.Abs(value.y - start.y))
                     .ThenBy(value => value.y)
                     .ThenBy(value => value.x))
        {
            Queue<GridMoveStep> candidatePath = pathSearchBroker.GetMovePathTo(
                grid,
                start,
                candidate,
                GridPathSearchPriority.Urgent);
            if (candidatePath == null
                || candidatePath.Count == 0 && candidate != start)
            {
                continue;
            }

            reservations[intruderId] = candidate;
            targetByIntruder[intruderId] = targetId;
            attackCell = candidate;
            approachPath = candidatePath;
            return true;
        }

        if (reservations.Count == 0)
        {
            targetReservations.Remove(targetId);
        }
        return false;
    }

    private static bool TryGetTraversalCost(
        Grid grid,
        Vector2Int position,
        IDefenseBreachTargetQuery targetQuery,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        float riskTolerance,
        float estimatedStructureDamage,
        out float cost)
    {
        cost = 0f;
        GridCell cell = grid.GetGridCell(position);
        if (cell == null
            || cell.AreaType == GridCellAreaType.BlockedExterior
            || !cell.IsWalkableArea)
        {
            return false;
        }

        if (TryGetStructuralTarget(targetQuery, position, out DefenseBreachTargetSnapshot target))
        {
            float damage = Mathf.Max(1f, estimatedStructureDamage);
            float hitCount = Mathf.Ceil(target.CurrentHitPoints / damage);
            cost = 10f
                + target.CurrentHitPoints
                + target.Toughness * 3f
                + hitCount * 35f;
        }
        else if (grid.IsWalkable(position))
        {
            cost = 10f;
        }
        else
        {
            return false;
        }

        if (knownRisks != null
            && knownRisks.TryGetValue(position, out float severity))
        {
            cost += Mathf.Max(0f, severity)
                * Mathf.Clamp01(1f - riskTolerance)
                * 30f;
        }
        return true;
    }

    private static bool TryGetStructuralTarget(
        IDefenseBreachTargetQuery targetQuery,
        Vector2Int position,
        out DefenseBreachTargetSnapshot target)
    {
        return targetQuery.TryGetTargetAt(position, out target)
            && target.TargetId.IsValid
            && target.Breachable
            && !target.Destroyed;
    }

    private static float[] CreateCosts(int count)
    {
        float[] result = new float[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = float.PositiveInfinity;
        }
        return result;
    }

    private static int[] CreateParents(int count)
    {
        int[] result = new int[count];
        for (int index = 0; index < count; index++)
        {
            result[index] = -1;
        }
        return result;
    }

    private readonly struct HeapNode
    {
        public HeapNode(int index, float cost, int sequence)
        {
            Index = index;
            Cost = cost;
            Sequence = sequence;
        }

        public int Index { get; }
        public float Cost { get; }
        public int Sequence { get; }
    }

    private sealed class MinHeap
    {
        private readonly List<HeapNode> items = new();
        private int sequence;
        public int Count => items.Count;

        public void Push(int index, float cost)
        {
            HeapNode node = new(index, cost, sequence++);
            items.Add(node);
            int child = items.Count - 1;
            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (!Less(items[child], items[parent]))
                {
                    break;
                }
                (items[parent], items[child]) = (items[child], items[parent]);
                child = parent;
            }
        }

        public HeapNode Pop()
        {
            HeapNode result = items[0];
            int last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);
            int parent = 0;
            while (true)
            {
                int left = parent * 2 + 1;
                if (left >= items.Count)
                {
                    break;
                }
                int right = left + 1;
                int smallest = right < items.Count
                    && Less(items[right], items[left])
                    ? right
                    : left;
                if (!Less(items[smallest], items[parent]))
                {
                    break;
                }
                (items[parent], items[smallest]) =
                    (items[smallest], items[parent]);
                parent = smallest;
            }
            return result;
        }

        private static bool Less(HeapNode left, HeapNode right)
        {
            return left.Cost < right.Cost
                || Mathf.Approximately(left.Cost, right.Cost)
                && left.Sequence < right.Sequence;
        }
    }
}
