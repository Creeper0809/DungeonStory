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
        string initialPlotId = initial.PlotId;
        string initialCropId = initial.CropId;
        KeyValuePair<string, int>[] initialRequired = initial.RequiredMaterials
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        KeyValuePair<string, int>[] initialDelivered = initial.DeliveredMaterials
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        List<string> rows = new() { "check\tresult" };
        initial.PlotId = "external:crop-query-mutation";
        initial.CropId = "external:crop-query-mutation";
        if (initial.RequiredMaterials is IDictionary<string, int> required)
        {
            required["external:nested-required"] = 998;
        }
        if (initial.DeliveredMaterials is IDictionary<string, int> delivered)
        {
            delivered["external:nested-delivered"] = 998;
        }
        initial.RequiredMaterials = new Dictionary<string, int>
        {
            ["external:required"] = 999
        };
        initial.DeliveredMaterials = new Dictionary<string, int>
        {
            ["external:delivered"] = 999
        };
        CropPlotSnapshot stable = runtime.Plots.Single(value =>
            value.PlotId == initialPlotId);
        Require(!ReferenceEquals(initial, stable)
                && stable.CropId == initialCropId
                && stable.RequiredMaterials
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .SequenceEqual(initialRequired)
                && stable.DeliveredMaterials
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .SequenceEqual(initialDelivered),
            "An external crop row or material mutation reached the next query.");
        rows.Add("unchanged inputs return detached rows and materials\tPASS");

        CropDefinitionSO alternate = fixture.RequireAlternateOutdoorCrop();
        int versionBeforeCommand = runtime.Version;
        Require(runtime.TrySetCrop(
                fixture.Plot,
                alternate.CropId,
                out string setCropFailure),
            setCropFailure);
        CropPlotSnapshot afterCommand = runtime.Plots.Single(value =>
            value.PlotId == initialPlotId);
        Require(runtime.Version > versionBeforeCommand
                && afterCommand.CropId == alternate.CropId
                && stable.CropId == initialCropId,
            "A crop owner command did not publish a detached current view.");
        rows.Add("owner command publishes current detached view\tPASS");

        DungeonCropPlotSaveData saved = runtime.Capture();
        saved.plots.Single(value => value.buildingInstanceId == initialPlotId)
            .pestLureNextAllowedDay = 7;
        CropPlotRestoreCandidate candidate = runtime.BuildRestore(saved);
        CropPlotSnapshot staged = runtime.Plots.Single(value =>
            value.PlotId == initialPlotId);
        Require(staged.PestLureNextAllowedDay == 0
                && staged.CropId == alternate.CropId,
            "Staging a crop restore changed the current view values.");
        rows.Add("staged restore preserves current view\tPASS");
        runtime.Restore(candidate);
        IReadOnlyList<CropPlotSnapshot> restoredList = runtime.Plots;
        CropPlotSnapshot restored = restoredList.Single(value =>
            value.PlotId == initialPlotId);
        Require(!ReferenceEquals(initialList, restoredList)
            && !ReferenceEquals(afterCommand, restored)
            && restored.PestLureNextAllowedDay == 7
            && afterCommand.PestLureNextAllowedDay == 0,
            "Crop view retained the replaced root or rewrote an older view.");
        rows.Add("restored root owns new cache; old view stays detached\tPASS");
        CropPlotSnapshot restoredAgain = runtime.Plots.Single(value =>
            value.PlotId == initialPlotId);
        Require(!ReferenceEquals(restored, restoredAgain)
                && restoredAgain.PestLureNextAllowedDay == 7
                && restoredAgain.CropId == alternate.CropId,
            "A restored crop query did not return detached current values.");
        rows.Add("restored view remains detached and current\tPASS");
        Require(runtime.CaptureActiveOutputOwners((BuildingInstanceId)initialPlotId).Count == 0
            && runtime.CapturePendingOutputOwners().Count == 0,
            "An inactive restored plot invented physical harvest output owners.");
        rows.Add("harvest queries read current inactive root\tPASS");
        Directory.CreateDirectory(output);
        using (FileStream file = new(Path.Combine(output, "crop-query-cache-boundary.tsv"),
                   FileMode.CreateNew, FileAccess.Write))
        using (StreamWriter writer = new(file))
            foreach (string row in rows) writer.WriteLine(row);
        Debug.Log("ARCHITECTURE_CROP_QUERY_CACHE=PASS; detached rows and materials; owner command; staged and committed current root; harvest owner query");
    }
}
#endif
