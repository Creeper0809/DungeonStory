using System;
using System.Collections.Generic;

// One evaluation's derived residual graph, never saved. Node capacities are
// shared by every route; residual reverse edges allow earlier routes to move
// without taking an earlier-priority consumer's granted power away.
internal sealed class ElectricalFlowAllocator
{
    private const double Epsilon = 1e-9;
    private const double Unbounded = 1e30;
    private sealed class Edge
    {
        public int From, To, Cost;
        public double Remaining;
    }

    private readonly List<Edge> edges = new List<Edge>();
    private readonly int nodeCount, source, sink;
    private readonly int[] previous;
    private readonly double[] distance;

    public ElectricalFlowAllocator(IReadOnlyList<double> capacities)
    {
        if (capacities == null) throw new ArgumentNullException(nameof(capacities));
        nodeCount = capacities.Count;
        source = nodeCount * 2;
        sink = source + 1;
        previous = new int[sink + 1];
        distance = new double[sink + 1];
        for (int i = 0; i < nodeCount; i++)
            AddEdge(i * 2, i * 2 + 1, RequireCapacity(capacities[i]), 0);
    }

    public void Connect(int left, int right)
    {
        RequireNode(left);
        RequireNode(right);
        if (left == right) throw new ArgumentException("Self power link.");
        AddEdge(left * 2 + 1, right * 2, Unbounded, 0);
        AddEdge(right * 2 + 1, left * 2, Unbounded, 0);
    }

    public int AddSource(int node, double rate, bool storedEnergy)
    {
        RequireNode(node);
        return AddEdge(source, node * 2, RequireCapacity(rate), storedEnergy ? 1 : 0);
    }

    public double UsedSource(int handle) => edges[handle ^ 1].Remaining;

    // Existing used flow remains fixed; this only prevents additional discharge
    // while charging from remaining generation later in the same evaluation.
    public void StopSource(int handle) => edges[handle].Remaining = 0;

    public double Allocate(int node, double requested, double minimumFraction)
    {
        RequireNode(node);
        RequireCapacity(requested);
        if (double.IsNaN(minimumFraction) || minimumFraction < 0 || minimumFraction > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumFraction));
        if (requested <= Epsilon) return 0;

        int terminal = AddEdge(node * 2 + 1, sink, requested, 0);
        double[] checkpoint = new double[edges.Count];
        for (int i = 0; i < edges.Count; i++) checkpoint[i] = edges[i].Remaining;
        double granted = 0;
        while (granted + Epsilon < requested && FindPath())
        {
            double amount = requested - granted;
            for (int vertex = sink; vertex != source; vertex = edges[previous[vertex]].From)
                amount = Math.Min(amount, edges[previous[vertex]].Remaining);
            for (int vertex = sink; vertex != source; vertex = edges[previous[vertex]].From)
            {
                int edge = previous[vertex];
                edges[edge].Remaining -= amount;
                edges[edge ^ 1].Remaining += amount;
            }
            granted += amount;
        }

        // Same 0.001 supply-fraction margin as the existing power consumer rule.
        if (granted / requested + 0.001 < minimumFraction)
        {
            for (int i = 0; i < edges.Count; i++) edges[i].Remaining = checkpoint[i];
            granted = 0;
        }
        // Never grant extra to an earlier sink in a later allocation.
        edges[terminal].Remaining = 0;
        return granted;
    }

    private bool FindPath()
    {
        for (int i = 0; i < distance.Length; i++)
        {
            distance[i] = double.PositiveInfinity;
            previous[i] = -1;
        }
        distance[source] = 0;
        // Stable insertion order resolves equal-cost paths deterministically.
        // Cost 0 generation is exhausted before cost 1 stored power.
        for (int pass = 0; pass < distance.Length - 1; pass++)
        {
            bool changed = false;
            for (int i = 0; i < edges.Count; i++)
            {
                Edge edge = edges[i];
                if (edge.From == sink || edge.To == source || edge.Remaining <= Epsilon
                    || double.IsPositiveInfinity(distance[edge.From])) continue;
                double candidate = distance[edge.From] + edge.Cost;
                if (candidate >= distance[edge.To]) continue;
                distance[edge.To] = candidate;
                previous[edge.To] = i;
                changed = true;
            }
            if (!changed) break;
        }
        return previous[sink] >= 0;
    }

    private int AddEdge(int from, int to, double capacity, int cost)
    {
        int index = edges.Count;
        edges.Add(new Edge { From = from, To = to, Remaining = capacity, Cost = cost });
        edges.Add(new Edge { From = to, To = from, Remaining = 0, Cost = -cost });
        return index;
    }

    private void RequireNode(int node)
    {
        if (node < 0 || node >= nodeCount) throw new ArgumentOutOfRangeException(nameof(node));
    }

    private static double RequireCapacity(double capacity)
    {
        if (double.IsNaN(capacity) || double.IsInfinity(capacity) || capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        return capacity;
    }
}
