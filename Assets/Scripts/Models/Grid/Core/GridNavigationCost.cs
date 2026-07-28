using System;
using UnityEngine;

public interface IGridTraversalCostPolicy
{
    int Version { get; }
    int MinimumHorizontalCost { get; }

    int GetTraversalCost(
        Grid grid,
        in GridTraversalStepData step,
        GridTraversalContext traversalContext);
}

public interface IGridTraversalCostProvider
{
    int GetTraversalCostUnits();
}

public sealed class DefaultGridTraversalCostPolicy : IGridTraversalCostPolicy
{
    public const int DryWalkCost = 100;
    public const int StairFallbackCost = 800;
    public const int ElevatorFallbackCost = 500;
    public const int TeleportFallbackCost = 100;
    public const int InstantFallbackCost = 100;

    public static readonly DefaultGridTraversalCostPolicy Instance =
        new DefaultGridTraversalCostPolicy();

    private DefaultGridTraversalCostPolicy()
    {
    }

    public int Version => 1;
    public int MinimumHorizontalCost => DryWalkCost;

    public int GetTraversalCost(
        Grid grid,
        in GridTraversalStepData step,
        GridTraversalContext traversalContext)
    {
        if (grid == null)
        {
            return int.MaxValue;
        }

        if (step.MovementOccupant is IGridTraversalCostProvider provider)
        {
            return Mathf.Max(1, provider.GetTraversalCostUnits());
        }

        return step.MoveType switch
        {
            GridMoveType.Walk => GetTerrainWalkCost(grid.GetGridCell(step.To)),
            GridMoveType.Stair => StairFallbackCost,
            GridMoveType.Elevator => ElevatorFallbackCost,
            GridMoveType.Teleport => TeleportFallbackCost,
            GridMoveType.Instant => InstantFallbackCost,
            _ => DryWalkCost
        };
    }

    private static int GetTerrainWalkCost(GridCell destination)
    {
        if (destination == null || !destination.IsWalkableArea)
        {
            return int.MaxValue;
        }

        float speedMultiplier = Mathf.Max(0.01f, destination.TerrainMoveSpeedMultiplier);
        return Mathf.Max(1, Mathf.CeilToInt(DryWalkCost / speedMultiplier));
    }
}

public readonly struct GridTraversalStepData
{
    public GridTraversalStepData(
        Vector2Int from,
        Vector2Int to,
        IGridOccupant movementOccupant,
        GridMoveType moveType)
    {
        From = from;
        To = to;
        MovementOccupant = movementOccupant;
        MoveType = moveType;
    }

    public Vector2Int From { get; }
    public Vector2Int To { get; }
    public IGridOccupant MovementOccupant { get; }
    public GridMoveType MoveType { get; }
}

internal readonly struct GridSearchQueueNode
{
    public GridSearchQueueNode(int cellIndex, int cost, int priority, int sequence)
    {
        CellIndex = cellIndex;
        Cost = cost;
        Priority = priority;
        Sequence = sequence;
    }

    public int CellIndex { get; }
    public int Cost { get; }
    public int Priority { get; }
    public int Sequence { get; }
}

internal sealed class GridSearchPriorityQueue
{
    private GridSearchQueueNode[] nodes;

    public GridSearchPriorityQueue(int initialCapacity = 256)
    {
        nodes = new GridSearchQueueNode[
            Mathf.NextPowerOfTwo(Mathf.Max(1, initialCapacity))];
    }

    public int Count { get; private set; }

    public void Enqueue(GridSearchQueueNode node)
    {
        EnsureCapacity(Count + 1);
        int index = Count++;
        nodes[index] = node;
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            if (!ComesBefore(nodes[index], nodes[parent]))
            {
                break;
            }

            (nodes[index], nodes[parent]) = (nodes[parent], nodes[index]);
            index = parent;
        }
    }

    public GridSearchQueueNode Dequeue()
    {
        if (Count <= 0)
        {
            throw new InvalidOperationException("Cannot dequeue from an empty navigation queue.");
        }

        GridSearchQueueNode result = nodes[0];
        Count--;
        if (Count == 0)
        {
            return result;
        }

        nodes[0] = nodes[Count];
        int index = 0;
        while (true)
        {
            int left = index * 2 + 1;
            if (left >= Count)
            {
                break;
            }

            int right = left + 1;
            int best = right < Count && ComesBefore(nodes[right], nodes[left])
                ? right
                : left;
            if (!ComesBefore(nodes[best], nodes[index]))
            {
                break;
            }

            (nodes[index], nodes[best]) = (nodes[best], nodes[index]);
            index = best;
        }

        return result;
    }

    public void Clear()
    {
        Count = 0;
    }

    private void EnsureCapacity(int capacity)
    {
        if (capacity <= nodes.Length)
        {
            return;
        }

        int nextCapacity = Mathf.NextPowerOfTwo(capacity);
        Array.Resize(ref nodes, Mathf.Max(256, nextCapacity));
    }

    private static bool ComesBefore(GridSearchQueueNode left, GridSearchQueueNode right)
    {
        return left.Priority < right.Priority
            || (left.Priority == right.Priority && left.Cost < right.Cost)
            || (left.Priority == right.Priority
                && left.Cost == right.Cost
                && left.Sequence < right.Sequence);
    }
}
