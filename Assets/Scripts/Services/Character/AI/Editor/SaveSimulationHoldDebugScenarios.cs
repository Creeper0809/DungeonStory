#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using DungeonStory.Foundation;
using UnityEngine;

public static class SaveSimulationHoldDebugScenarios
{
    public static void VerifyForBatch()
    {
        float originalScale = Time.timeScale, originalFixed = Time.fixedDeltaTime;
        var clock = new UnityGameTimeScaleController();
        try
        {
            clock.Scale = 2;
            float fixedAtTwo = Time.fixedDeltaTime;
            var first = clock.HoldSimulation();
            var second = clock.HoldSimulation();
            Require(clock.Scale == 0, "Nested holds did not stop simulation.");
            clock.Scale = 5;
            Require(clock.Scale == 0, "A speed command bypassed the owned hold.");
            first.Dispose(); first.Dispose();
            Require(clock.Scale == 0, "A nested hold resumed somebody else's pause.");
            second.Dispose();
            Require(clock.Scale == 5 && Math.Abs(Time.fixedDeltaTime - fixedAtTwo * 2.5f) < 0.00001f,
                "Release lost the latest speed or the original fixed-step rule.");
            using (clock.HoldSimulation()) clock.Scale = 0;
            Require(clock.Scale == 0, "Release undid a later player pause.");
            clock.Scale = 3;
            using (clock.HoldSimulation()) clock.Scale = 1;
            Require(clock.Scale == 1, "Release restored stale speed instead of later intent.");
            clock.Scale = 0;
            using (clock.HoldSimulation()) { }
            Require(clock.Scale == 0, "An already paused world was resumed.");
            Task.Run(() =>
            {
                try { clock.HoldSimulation(); }
                catch (InvalidOperationException) { return; }
                throw new InvalidOperationException("A worker acquired the main-thread simulation hold.");
            }).GetAwaiter().GetResult();
        }
        finally { Time.timeScale = originalScale; Time.fixedDeltaTime = originalFixed; }
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
#endif
