using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public sealed class IndustrialInfrastructureStressReport
{
    public int UtilityCellCount { get; set; }
    public int PayloadRouteCount { get; set; }
    public double TopologyMilliseconds { get; set; }
    public long TopologyAllocatedBytes { get; set; }
    public double RouteMilliseconds { get; set; }
    public long RouteAllocatedBytes { get; set; }
}

public static class IndustrialInfrastructureStressProbe
{
    public static IndustrialInfrastructureStressReport Run(
        int width = 100,
        int height = 100,
        int routeRequests = 2000)
    {
        if (width < 2 || height < 1 || routeRequests < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "스트레스 검증 크기는 2x1 이상이어야 합니다.");
        }

        List<IndustrialNodeDescriptor> nodes =
            new List<IndustrialNodeDescriptor>(width * height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool output = x == width - 1;
                nodes.Add(new IndustrialNodeDescriptor
                {
                    NodeId = $"stress:{x:D4}:{y:D4}",
                    Channels = UtilityChannel.Power
                        | UtilityChannel.CleanWater
                        | UtilityChannel.Wastewater,
                    Cells = new[] { new Vector2Int(x, y) },
                    Conveyor = output
                        ? null
                        : new BuildingConveyorSegmentAbility
                        {
                            outputDirections =
                                new[] { Vector2Int.right },
                            requiresPower = false,
                            capacity = 32
                        },
                    ConveyorPort = output
                        ? new BuildingConveyorPortAbility
                        {
                            mode = ConveyorPortMode.Output,
                            destinationId = $"stress-output:{y:D4}"
                        }
                        : null
                });
            }
        }

        long topologyAllocatedAtStart =
            GC.GetAllocatedBytesForCurrentThread();
        Stopwatch topologyTimer = Stopwatch.StartNew();
        IndustrialTopologySnapshot topology =
            IndustrialInfrastructureTopologyBuilder.BuildFromDescriptors(
                1,
                nodes);
        topologyTimer.Stop();
        long topologyAllocated = GC.GetAllocatedBytesForCurrentThread()
            - topologyAllocatedAtStart;

        Require(topology.Nodes.Count == width * height,
            "10K 기반 시설 토폴로지에서 노드가 유실됐습니다.");
        Require(topology.NodesByNetwork[UtilityChannel.Power].Count == 1
                && topology.NodesByNetwork[
                    UtilityChannel.CleanWater].Count == 1
                && topology.NodesByNetwork[
                    UtilityChannel.Wastewater].Count == 1,
            "동일 셀의 전력·상수·하수망이 예기치 않게 분리됐습니다.");
        Require(topology.ConveyorNodesByNetwork.Count == height,
            "컨베이어 행 연결망 수가 결정적으로 생성되지 않았습니다.");

        ItemTransitStackSnapshot stack = new ItemTransitStackSnapshot(
            new ItemStackId("stack:stress"),
            "material:lumber",
            1,
            false,
            0f);
        int successfulRoutes = 0;
        int firstRouteLength = -1;
        ConveyorStallReason firstFailureReason = ConveyorStallReason.None;
        long routeAllocatedAtStart =
            GC.GetAllocatedBytesForCurrentThread();
        Stopwatch routeTimer = Stopwatch.StartNew();
        for (int index = 0; index < routeRequests; index++)
        {
            int row = index % height;
            bool found = ConveyorRoutePlanner.TryFindRoute(
                    topology,
                    $"stress:0000:{row:D4}",
                    $"stress-output:{row:D4}",
                    stack,
                    (_, _) => true,
                    nodeId => topology.Nodes[nodeId]
                        .ConveyorPort?.destinationId ?? string.Empty,
                    out IReadOnlyList<string> route,
                    out ConveyorStallReason failureReason);
            if (found && route.Count == width)
            {
                successfulRoutes++;
            }
            else if (firstRouteLength < 0)
            {
                firstRouteLength = route.Count;
                firstFailureReason = failureReason;
            }
        }

        routeTimer.Stop();
        long routeAllocated = GC.GetAllocatedBytesForCurrentThread()
            - routeAllocatedAtStart;
        Require(successfulRoutes == routeRequests,
            $"화물 경로 배치가 일부 실패했습니다: "
            + $"success={successfulRoutes}/{routeRequests}, "
            + $"firstReason={firstFailureReason}, "
            + $"firstRouteLength={firstRouteLength}, expected={width}, "
            + $"firstOutgoing={topology.ConveyorOutgoing.GetValueOrDefault("stress:0000:0000", new List<string>()).Count}, "
            + $"lastIncoming={topology.ConveyorIncoming.GetValueOrDefault($"stress:{width - 1:D4}:0000", new List<string>()).Count}, "
            + $"lastPortMode={topology.Nodes[$"stress:{width - 1:D4}:0000"].ConveyorPort?.mode}, "
            + $"lastDestination='{topology.Nodes[$"stress:{width - 1:D4}:0000"].ConveyorPort?.destinationId}'.");
        Require(topologyTimer.ElapsedMilliseconds < 10000,
            "10K 기반 시설 토폴로지 생성이 10초를 넘었습니다.");
        Require(routeTimer.ElapsedMilliseconds < 5000,
            "2,000개 화물 경로 배치가 5초를 넘었습니다.");

        return new IndustrialInfrastructureStressReport
        {
            UtilityCellCount = width * height,
            PayloadRouteCount = routeRequests,
            TopologyMilliseconds =
                topologyTimer.Elapsed.TotalMilliseconds,
            TopologyAllocatedBytes = topologyAllocated,
            RouteMilliseconds = routeTimer.Elapsed.TotalMilliseconds,
            RouteAllocatedBytes = routeAllocated
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
