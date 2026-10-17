#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Kept in the runtime assembly under UNITY_EDITOR, like the existing power
// contract probe, so the production allocator need not become public for tests.
public static class WimPowerFlowDebugScenarios
{
    public static void Run()
    {
        var report = new List<string>();
        var series = new ElectricalFlowAllocator(new double[] { 100, 4, 100 });
        series.Connect(0, 1); series.Connect(1, 2);
        int source = series.AddSource(0, 20, false);
        Equal(series.Allocate(2, 10, 0), 4, "series-cap");
        Equal(series.UsedSource(source), 4, "series-conservation");
        report.Add("series-node-cap-and-conservation=PASS");

        var parallel = new ElectricalFlowAllocator(new double[] { 100, 4, 6, 100 });
        parallel.Connect(0, 1); parallel.Connect(0, 2);
        parallel.Connect(1, 3); parallel.Connect(2, 3);
        parallel.AddSource(0, 20, false);
        Equal(parallel.Allocate(3, 12, 0), 10, "parallel-cap");
        report.Add("parallel-capacity-sums=PASS");

        var shared = new ElectricalFlowAllocator(new double[] { 100, 6, 100, 100 });
        shared.Connect(0, 1); shared.Connect(1, 2); shared.Connect(1, 3);
        shared.AddSource(0, 20, false);
        Equal(shared.Allocate(2, 4, 1), 4, "priority-first");
        Equal(shared.Allocate(3, 4, 0), 2, "shared-bottleneck");
        report.Add("priority-and-shared-cut-no-double-spend=PASS");

        var reroute = new ElectricalFlowAllocator(new double[] { 100, 1, 1, 100, 100 });
        reroute.Connect(0, 1); reroute.Connect(0, 2);
        reroute.Connect(1, 3); reroute.Connect(2, 3); reroute.Connect(1, 4);
        reroute.AddSource(0, 2, false);
        Equal(reroute.Allocate(3, 1, 1), 1, "route-first");
        Equal(reroute.Allocate(4, 1, 1), 1, "route-reassigned");
        report.Add("residual-reroute-preserves-prior-grant=PASS");

        var rejected = new ElectricalFlowAllocator(new double[] { 100, 4, 100, 100 });
        rejected.Connect(0, 1); rejected.Connect(1, 2); rejected.Connect(1, 3);
        int battery = rejected.AddSource(0, 8, true);
        Equal(rejected.Allocate(2, 8, 1), 0, "below-minimum");
        Equal(rejected.UsedSource(battery), 0, "rejected-battery-retained");
        Equal(rejected.Allocate(3, 4, 1), 4, "released-route");
        Equal(rejected.UsedSource(battery), 4, "actual-discharge");
        report.Add("minimum-supply-rollback-retains-battery-and-capacity=PASS");

        var preferred = new ElectricalFlowAllocator(new double[] { 100, 100, 100, 100 });
        preferred.Connect(0, 1); preferred.Connect(1, 2); preferred.Connect(2, 3);
        int stored = preferred.AddSource(2, 10, true);
        int generated = preferred.AddSource(0, 10, false);
        Equal(preferred.Allocate(3, 6, 1), 6, "generation-first");
        Equal(preferred.UsedSource(stored), 0, "near-battery-unused");
        preferred.StopSource(stored);
        Equal(preferred.Allocate(2, 10, 0), 4, "remaining-generation-charge");
        Equal(preferred.UsedSource(generated), 10, "generation-budget");
        Equal(preferred.UsedSource(stored), 0, "no-self-charge");
        report.Add("generation-preferred-over-near-battery-and-charge-conservation=PASS");

        var off = new ElectricalFlowAllocator(new double[] { 100, 0, 100 });
        off.Connect(0, 1); off.Connect(1, 2); off.AddSource(0, 10, false);
        Equal(off.Allocate(2, 4, 0), 0, "disconnected");
        report.Add("disconnected-node-has-zero-flow=PASS");

        var dto = new DungeonPowerInfrastructureSaveData();
        dto.nodes.Add(new PowerNodeSaveData { buildingInstanceId = "building:wim:power", connectionState = 1 });
        IndustrialInfrastructureSaveValidation.RequireValid(dto);
        dto.nodes[0].connectionState = 2;
        var restored = JsonUtility.FromJson<DungeonPowerInfrastructureSaveData>(JsonUtility.ToJson(dto));
        IndustrialInfrastructureSaveValidation.RequireValid(restored);
        Equal(restored.nodes[0].connectionState, 2, "saved-disconnection");
        foreach (int invalid in new[] { 0, 3, -1 })
        {
            restored.nodes[0].connectionState = invalid;
            bool failed = false;
            try { IndustrialInfrastructureSaveValidation.RequireValid(restored); }
            catch (InvalidOperationException) { failed = true; }
            if (!failed) throw new InvalidOperationException("Accepted invalid connection state.");
        }
        report.Add("current-format-connection-required-and-round-trip=PASS");
        report.Add("scope=pure-flow-and-serialization-contracts-not-live-network-evidence");
        report.Add("result=PASS");
        Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        File.WriteAllLines("Artifacts/QA/wim-implementation/wim-013-flow-contracts.txt", report);
    }

    private static void Equal(double actual, double expected, string label)
    {
        if (Math.Abs(actual - expected) > 0.000001)
            throw new InvalidOperationException($"{label}: {actual} != {expected}");
    }
}
#endif
