#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static partial class EnvironmentCropProductionEntryDebugScenarios
{
    public static void VerifyCropQueryCacheBoundaryForBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Crop query verification requires isolated batch execution.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("Crop query verification requires an owned output directory.");
        using Fixture fixture = new(includeParallelPlot: true);
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        _ = runtime.Plots;
        IReadOnlyList<CropPlotSnapshot> initialList = runtime.Plots;
        CropPlotSnapshot initial = initialList.First();
        List<string> rows = new() { "check\tresult" };
        Require(ReferenceEquals(initial, runtime.Plots.First()),
            "Stable crop queries rebuilt their derived snapshot cache.");
        rows.Add("unchanged inputs retain snapshot cache\tPASS");

        Require(runtime.TryBindNextCycle("action:qa-crop-query-cache",
                initial.PlotId, initial.CropId, out string bindFailure), bindFailure);
        CropPlotSnapshot afterCommand = runtime.Plots.Single(value => value.PlotId == initial.PlotId);
        Require(!ReferenceEquals(initial, afterCommand),
            "A real crop command did not invalidate the snapshot cache.");
        rows.Add("command invalidates derived cache\tPASS");
        Require(ReferenceEquals(afterCommand,
                runtime.Plots.Single(value => value.PlotId == initial.PlotId)),
            "A refreshed crop snapshot did not settle to a cache hit.");
        rows.Add("refreshed inputs settle to cache hit\tPASS");

        DungeonCropPlotSaveData saved = runtime.Capture();
        saved.plots.Single(value => value.buildingInstanceId == initial.PlotId).pestLureNextAllowedDay = 7;
        CropPlotRestoreCandidate candidate = runtime.BuildRestore(saved);
        Require(ReferenceEquals(afterCommand,
                runtime.Plots.Single(value => value.PlotId == initial.PlotId)),
            "Staging a crop restore changed the current view.");
        rows.Add("staged restore preserves current view\tPASS");
        runtime.Restore(candidate);
        IReadOnlyList<CropPlotSnapshot> restoredList = runtime.Plots;
        CropPlotSnapshot restored = restoredList.Single(value => value.PlotId == initial.PlotId);
        Require(!ReferenceEquals(initialList, restoredList)
            && !ReferenceEquals(afterCommand, restored)
            && restored.PestLureNextAllowedDay == 7
            && afterCommand.PestLureNextAllowedDay == 0,
            "Crop view retained the replaced root or rewrote an older view.");
        rows.Add("restored root owns new cache; old view stays detached\tPASS");
        Require(ReferenceEquals(restored,
                runtime.Plots.Single(value => value.PlotId == initial.PlotId)),
            "Restored crop cache did not retain its computed snapshot.");
        rows.Add("restored view cache hit\tPASS");
        Require(runtime.CaptureActiveOutputOwners((BuildingInstanceId)initial.PlotId).Count == 0
            && runtime.CapturePendingOutputOwners().Count == 0,
            "An inactive restored plot invented physical harvest output owners.");
        rows.Add("harvest queries read current inactive root\tPASS");
        Directory.CreateDirectory(output);
        using (FileStream file = new(Path.Combine(output, "crop-query-cache-boundary.tsv"),
                   FileMode.CreateNew, FileAccess.Write))
        using (StreamWriter writer = new(file))
            foreach (string row in rows) writer.WriteLine(row);
        Debug.Log("ARCHITECTURE_CROP_QUERY_CACHE=PASS; stable inputs; command invalidation; staged/committed current root; detached old view; harvest owner query");
    }
}
#endif
