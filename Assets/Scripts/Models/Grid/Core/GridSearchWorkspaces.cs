using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class GridSearchWorkspace
{
    public int[] ParentIndex { get; private set; } = Array.Empty<int>();
    public IGridOccupant[] ParentMovementOccupant { get; private set; } =
        Array.Empty<IGridOccupant>();
    public GridMoveType[] ParentMoveType { get; private set; } = Array.Empty<GridMoveType>();
    public int[] SearchOrder { get; private set; } = Array.Empty<int>();
    public int[] MoveCost { get; private set; } = Array.Empty<int>();
    private int[] nodeGeneration = Array.Empty<int>();
    private int[] touchedIndices = Array.Empty<int>();
    private int currentGeneration;
    private int touchedCount;

    public void Prepare(int cellCount)
    {
        int capacity = Mathf.NextPowerOfTwo(Mathf.Max(1, cellCount));
        if (ParentIndex.Length < cellCount)
        {
            ParentIndex = new int[capacity];
            ParentMovementOccupant = new IGridOccupant[capacity];
            ParentMoveType = new GridMoveType[capacity];
            SearchOrder = new int[capacity];
            MoveCost = new int[capacity];
            nodeGeneration = new int[capacity];
            touchedIndices = new int[capacity];
            currentGeneration = 0;
            touchedCount = 0;
        }

        currentGeneration++;
        if (currentGeneration == int.MaxValue)
        {
            Array.Clear(nodeGeneration, 0, nodeGeneration.Length);
            currentGeneration = 1;
        }

        touchedCount = 0;
    }

    public int GetMoveCost(int index)
    {
        return nodeGeneration[index] == currentGeneration
            ? MoveCost[index]
            : int.MaxValue;
    }

    public void SetStart(int index)
    {
        MarkTouched(index);
        ParentIndex[index] = -1;
        ParentMovementOccupant[index] = null;
        ParentMoveType[index] = default;
        MoveCost[index] = 0;
    }

    public void SetNode(
        int index,
        int cost,
        int parentIndex,
        IGridOccupant movementOccupant,
        GridMoveType moveType)
    {
        MarkTouched(index);
        ParentIndex[index] = parentIndex;
        ParentMovementOccupant[index] = movementOccupant;
        ParentMoveType[index] = moveType;
        MoveCost[index] = cost;
    }

    public void ReleaseReferences()
    {
        for (int index = 0; index < touchedCount; index++)
        {
            ParentMovementOccupant[touchedIndices[index]] = null;
        }
    }

    private void MarkTouched(int index)
    {
        if (nodeGeneration[index] == currentGeneration)
        {
            return;
        }

        nodeGeneration[index] = currentGeneration;
        touchedIndices[touchedCount++] = index;
    }
}

internal sealed class SparseGridSearchWorkspace
{
    private const int InitialCapacity = 512;
    private const int LoadFactorNumerator = 7;
    private const int LoadFactorDenominator = 10;

    private int[] keys = new int[InitialCapacity];
    private int[] generations = new int[InitialCapacity];
    private int[] parentIndices = new int[InitialCapacity];
    private int[] moveCosts = new int[InitialCapacity];
    private IGridOccupant[] parentMovementOccupants =
        new IGridOccupant[InitialCapacity];
    private GridMoveType[] parentMoveTypes =
        new GridMoveType[InitialCapacity];
    private int[] occupiedSlots = new int[InitialCapacity];
    private int currentGeneration;
    private int count;

    public void Prepare()
    {
        currentGeneration++;
        if (currentGeneration == int.MaxValue)
        {
            Array.Clear(generations, 0, generations.Length);
            currentGeneration = 1;
        }

        count = 0;
    }

    public int GetMoveCost(int cellIndex)
    {
        int slot = FindExistingSlot(cellIndex);
        return slot >= 0 ? moveCosts[slot] : int.MaxValue;
    }

    public int GetParentIndex(int cellIndex)
    {
        int slot = FindExistingSlot(cellIndex);
        return slot >= 0 ? parentIndices[slot] : -1;
    }

    public IGridOccupant GetParentMovementOccupant(int cellIndex)
    {
        int slot = FindExistingSlot(cellIndex);
        return slot >= 0 ? parentMovementOccupants[slot] : null;
    }

    public GridMoveType GetParentMoveType(int cellIndex)
    {
        int slot = FindExistingSlot(cellIndex);
        return slot >= 0 ? parentMoveTypes[slot] : default;
    }

    public void SetStart(int cellIndex)
    {
        int slot = FindOrCreateSlot(cellIndex);
        parentIndices[slot] = -1;
        parentMovementOccupants[slot] = null;
        parentMoveTypes[slot] = default;
        moveCosts[slot] = 0;
    }

    public void SetNode(
        int cellIndex,
        int cost,
        int parentIndex,
        IGridOccupant movementOccupant,
        GridMoveType moveType)
    {
        int slot = FindOrCreateSlot(cellIndex);
        parentIndices[slot] = parentIndex;
        parentMovementOccupants[slot] = movementOccupant;
        parentMoveTypes[slot] = moveType;
        moveCosts[slot] = cost;
    }

    public void ReleaseReferences()
    {
        for (int index = 0; index < count; index++)
        {
            parentMovementOccupants[occupiedSlots[index]] = null;
        }
    }

    private int FindExistingSlot(int cellIndex)
    {
        int mask = keys.Length - 1;
        int slot = Hash(cellIndex) & mask;
        while (generations[slot] == currentGeneration)
        {
            if (keys[slot] == cellIndex)
            {
                return slot;
            }

            slot = (slot + 1) & mask;
        }

        return -1;
    }

    private int FindOrCreateSlot(int cellIndex)
    {
        int existing = FindExistingSlot(cellIndex);
        if (existing >= 0)
        {
            return existing;
        }

        EnsureInsertCapacity();
        int mask = keys.Length - 1;
        int slot = Hash(cellIndex) & mask;
        while (generations[slot] == currentGeneration)
        {
            slot = (slot + 1) & mask;
        }

        generations[slot] = currentGeneration;
        keys[slot] = cellIndex;
        parentIndices[slot] = -1;
        moveCosts[slot] = int.MaxValue;
        parentMovementOccupants[slot] = null;
        parentMoveTypes[slot] = default;
        occupiedSlots[count++] = slot;
        return slot;
    }

    private void EnsureInsertCapacity()
    {
        if ((count + 1) * LoadFactorDenominator
            < keys.Length * LoadFactorNumerator)
        {
            return;
        }

        Grow(keys.Length << 1);
    }

    private void Grow(int capacity)
    {
        int[] oldKeys = keys;
        int[] oldGenerations = generations;
        int[] oldParentIndices = parentIndices;
        int[] oldMoveCosts = moveCosts;
        IGridOccupant[] oldParentMovementOccupants =
            parentMovementOccupants;
        GridMoveType[] oldParentMoveTypes = parentMoveTypes;
        int oldGeneration = currentGeneration;

        keys = new int[capacity];
        generations = new int[capacity];
        parentIndices = new int[capacity];
        moveCosts = new int[capacity];
        parentMovementOccupants = new IGridOccupant[capacity];
        parentMoveTypes = new GridMoveType[capacity];
        occupiedSlots = new int[capacity];
        currentGeneration = 1;
        count = 0;

        int mask = capacity - 1;
        for (int oldSlot = 0; oldSlot < oldKeys.Length; oldSlot++)
        {
            if (oldGenerations[oldSlot] != oldGeneration)
            {
                continue;
            }

            int cellIndex = oldKeys[oldSlot];
            int slot = Hash(cellIndex) & mask;
            while (generations[slot] == currentGeneration)
            {
                slot = (slot + 1) & mask;
            }

            generations[slot] = currentGeneration;
            keys[slot] = cellIndex;
            parentIndices[slot] = oldParentIndices[oldSlot];
            moveCosts[slot] = oldMoveCosts[oldSlot];
            parentMovementOccupants[slot] =
                oldParentMovementOccupants[oldSlot];
            parentMoveTypes[slot] = oldParentMoveTypes[oldSlot];
            occupiedSlots[count++] = slot;
        }
    }

    private static int Hash(int value)
    {
        unchecked
        {
            uint hash = (uint)value;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            return (int)hash;
        }
    }
}

internal static class GridSearchScratch
{
    private static readonly Stack<Queue<Vector2Int>> PositionQueues = new Stack<Queue<Vector2Int>>();
    private static readonly Stack<List<Vector2Int>> PositionLists = new Stack<List<Vector2Int>>();
    private static readonly Stack<List<GridTraversalStepData>> TraversalStepLists =
        new Stack<List<GridTraversalStepData>>();
    private static readonly Stack<List<IGridOccupant>> OccupantLists = new Stack<List<IGridOccupant>>();
    private static readonly Stack<HashSet<IGridOccupant>> OccupantSets = new Stack<HashSet<IGridOccupant>>();
    private static readonly Stack<Queue<GridMoveStep>> MovePaths =
        new Stack<Queue<GridMoveStep>>();
    private static readonly Stack<GridSearchPriorityQueue> PriorityQueues =
        new Stack<GridSearchPriorityQueue>();
    private static readonly Stack<GridSearchWorkspace> Workspaces =
        new Stack<GridSearchWorkspace>();
    private static readonly Stack<SparseGridSearchWorkspace> SparseWorkspaces =
        new Stack<SparseGridSearchWorkspace>();

    [ThreadStatic] private static List<IGridOccupant> sharedOccupants;

    public static List<IGridOccupant> SharedOccupants =>
        sharedOccupants ??= new List<IGridOccupant>(8);

    public static Queue<Vector2Int> RentPositionQueue()
    {
        return PositionQueues.Count > 0 ? PositionQueues.Pop() : new Queue<Vector2Int>(128);
    }

    public static Queue<GridMoveStep> RentMovePath(int capacity = 0)
    {
        return MovePaths.Count > 0
            ? MovePaths.Pop()
            : new Queue<GridMoveStep>(Mathf.Max(4, capacity));
    }

    public static List<GridTraversalStepData> RentTraversalStepList()
    {
        return TraversalStepLists.Count > 0
            ? TraversalStepLists.Pop()
            : new List<GridTraversalStepData>(8);
    }

    public static List<Vector2Int> RentPositionList()
    {
        return PositionLists.Count > 0 ? PositionLists.Pop() : new List<Vector2Int>(128);
    }

    public static List<IGridOccupant> RentOccupantList()
    {
        return OccupantLists.Count > 0 ? OccupantLists.Pop() : new List<IGridOccupant>(8);
    }

    public static HashSet<IGridOccupant> RentOccupantSet()
    {
        return OccupantSets.Count > 0
            ? OccupantSets.Pop()
            : new HashSet<IGridOccupant>(GridOccupantReferenceComparer.Instance);
    }

    public static GridSearchPriorityQueue RentPriorityQueue()
    {
        return PriorityQueues.Count > 0
            ? PriorityQueues.Pop()
            : new GridSearchPriorityQueue();
    }

    public static GridSearchWorkspace RentWorkspace(int cellCount)
    {
        GridSearchWorkspace workspace = Workspaces.Count > 0
            ? Workspaces.Pop()
            : new GridSearchWorkspace();
        workspace.Prepare(cellCount);
        return workspace;
    }

    public static SparseGridSearchWorkspace RentSparseWorkspace()
    {
        SparseGridSearchWorkspace workspace =
            SparseWorkspaces.Count > 0
                ? SparseWorkspaces.Pop()
                : new SparseGridSearchWorkspace();
        workspace.Prepare();
        return workspace;
    }

    public static void EnsureIncrementalSearchCapacity(int count)
    {
        int target = Mathf.Max(0, count);
        while (SparseWorkspaces.Count < target)
        {
            SparseWorkspaces.Push(new SparseGridSearchWorkspace());
        }

        while (PriorityQueues.Count < target)
        {
            PriorityQueues.Push(new GridSearchPriorityQueue());
        }
    }

    public static void Return(Queue<Vector2Int> queue)
    {
        if (queue == null) return;

        queue.Clear();
        PositionQueues.Push(queue);
    }

    public static void ReturnMovePath(Queue<GridMoveStep> path)
    {
        if (path == null)
        {
            return;
        }

        path.Clear();
        MovePaths.Push(path);
    }

    public static void ReturnTraversalStepList(List<GridTraversalStepData> list)
    {
        if (list == null) return;

        list.Clear();
        TraversalStepLists.Push(list);
    }

    public static void ReturnPositionList(List<Vector2Int> list)
    {
        if (list == null) return;

        list.Clear();
        PositionLists.Push(list);
    }

    public static void ReturnOccupantList(List<IGridOccupant> list)
    {
        if (list == null) return;

        list.Clear();
        OccupantLists.Push(list);
    }

    public static void Return(HashSet<IGridOccupant> set)
    {
        if (set == null) return;

        set.Clear();
        OccupantSets.Push(set);
    }

    public static void Return(GridSearchPriorityQueue queue)
    {
        if (queue == null) return;

        queue.Clear();
        PriorityQueues.Push(queue);
    }

    public static void Return(GridSearchWorkspace workspace)
    {
        if (workspace == null)
        {
            return;
        }

        workspace.ReleaseReferences();
        Workspaces.Push(workspace);
    }

    public static void Return(SparseGridSearchWorkspace workspace)
    {
        if (workspace == null)
        {
            return;
        }

        workspace.ReleaseReferences();
        SparseWorkspaces.Push(workspace);
    }

    internal static void ClearRetainedMemory()
    {
        PositionQueues.Clear();
        PositionLists.Clear();
        TraversalStepLists.Clear();
        OccupantLists.Clear();
        OccupantSets.Clear();
        MovePaths.Clear();
        PriorityQueues.Clear();
        Workspaces.Clear();
        SparseWorkspaces.Clear();
        sharedOccupants = null;
    }
}

internal sealed class GridOccupantReferenceComparer : IEqualityComparer<IGridOccupant>
{
    internal static readonly GridOccupantReferenceComparer Instance = new();

    public bool Equals(IGridOccupant left, IGridOccupant right) =>
        ReferenceEquals(left, right);

    public int GetHashCode(IGridOccupant value) =>
        value == null ? 0 : RuntimeHelpers.GetHashCode(value);
}
