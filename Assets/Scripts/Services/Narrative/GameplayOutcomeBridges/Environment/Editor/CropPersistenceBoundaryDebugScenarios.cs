#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static partial class EnvironmentCropProductionEntryDebugScenarios
{
    public static void VerifyCropPersistenceBoundaryForBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Crop persistence verification requires isolated batch execution.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("Crop persistence verification requires an owned output directory.");
        Directory.CreateDirectory(output);
        using Fixture fixture = new(includeParallelPlot: true);
        using CropPlotRuntime runtime = fixture.CreateCropRuntime();
        runtime.Initialize();
        DungeonCropPlotSaveData initial = runtime.Capture();
        Require(initial.plots.Count == 2, "Crop persistence fixture did not discover both authored plots.");
        string initialJson = JsonUtility.ToJson(initial);
        int initialVersion = runtime.Version;
        List<string> results = new() { "check\tresult" };

        void RequireUnchanged(string check)
        {
            Require(runtime.Version == initialVersion
                && JsonUtility.ToJson(runtime.Capture()) == initialJson,
                "Crop restore candidate changed the live root: " + check);
            results.Add(check + "\tPASS");
        }

        void Reject(string check, DungeonCropPlotSaveData invalid)
        {
            bool rejected = false;
            try { runtime.BuildRestore(invalid); }
            catch (InvalidOperationException exception)
            {
                rejected = !string.IsNullOrWhiteSpace(exception.Message);
            }
            Require(rejected, "Crop restore accepted malformed input: " + check);
            RequireUnchanged(check);
        }

        Reject("null root", null);
        (string Name, Action<DungeonCropPlotSaveData> Corrupt)[] malformed =
        {
            ("schema version", data => data.version = 0),
            ("missing plot list", data => data.plots = null),
            ("missing seasonal IDs", data => data.handledSeasonalStartInstanceIds = null),
            ("null plot", data => data.plots[1] = null),
            ("duplicate plot", data => data.plots[1].buildingInstanceId = data.plots[0].buildingInstanceId),
            ("unknown crop", data => data.plots[1].cropId = "crop:missing-persistence-fixture"),
            ("unknown phase", data => data.plots[1].phase = (CropPlotPhase)int.MaxValue),
            ("nonfinite progress", data => data.plots[1].sowWork = float.NaN),
            ("missing sow owner", data => data.plots[1].pendingSow = null),
            ("empty harvest provenance", data => data.plots[1].pendingHarvest.harvestQuantity = 1),
            ("empty water provenance", data => data.plots[1].waterRefill.quantity = 1),
            ("empty treatment provenance", data => data.plots[1].treatment.quantity = 1)
        };
        foreach (var scenario in malformed)
        {
            DungeonCropPlotSaveData invalid = JsonUtility.FromJson<DungeonCropPlotSaveData>(initialJson);
            scenario.Corrupt(invalid);
            Reject(scenario.Name, invalid);
        }

        DungeonCropPlotSaveData source = JsonUtility.FromJson<DungeonCropPlotSaveData>(initialJson);
        source.plots[0].pestLureNextAllowedDay = 7;
        CropPlotRestoreCandidate candidate = runtime.BuildRestore(source);
        RequireUnchanged("valid candidate is staged only");
        source.plots[0].pestLureNextAllowedDay = 999;
        source.plots[0].treatment.quantity = 999;
        source.plots[0].waterRefill.quantity = 999;
        source.plots[0].pendingHarvest.harvestQuantity = 999;
        runtime.Restore(candidate);
        Require(runtime.Version == initialVersion + 1,
            "Crop restore did not publish the staged revision.");
        DungeonCropPlotSaveData restored = runtime.Capture();
        string restoredId = initial.plots[0].buildingInstanceId;
        CropPlotSaveData restoredPlot = restored.plots.Single(value => value.buildingInstanceId == restoredId);
        Require(restoredPlot.pestLureNextAllowedDay == 7
            && restoredPlot.treatment.quantity == 0
            && restoredPlot.waterRefill.quantity == 0
            && restoredPlot.pendingHarvest.harvestQuantity == 0,
            "Crop persistence retained the old root or aliased candidate DTOs.");
        results.Add("same runtime reads replaced root; candidate deep clone\tPASS");

        string restoredJson = JsonUtility.ToJson(restored);
        restoredPlot.pestLureNextAllowedDay = 998;
        restoredPlot.treatment.quantity = 998;
        Require(JsonUtility.ToJson(runtime.Capture()) == restoredJson,
            "Crop capture DTO mutation changed live state.");
        results.Add("capture DTO deep clone\tPASS");
        DungeonCropPlotSaveData second = JsonUtility.FromJson<DungeonCropPlotSaveData>(restoredJson);
        second.plots.Single(value => value.buildingInstanceId == restoredId).pestLureNextAllowedDay = 11;
        int versionBeforeSecond = runtime.Version;
        runtime.Restore(runtime.BuildRestore(second));
        Require(runtime.Version == versionBeforeSecond + 1
            && runtime.Capture().plots.Single(value => value.buildingInstanceId == restoredId).pestLureNextAllowedDay == 11,
            "Repeated crop restore did not use the current aggregate revision.");
        results.Add("second replacement uses current root and revision\tPASS");

        Require(CropPlotRuntime.FormatHarvestOperationId((BuildingInstanceId)restoredId, 17)
                == "crop-harvest:" + restoredId + ":000017"
            && CropPlotRuntime.FormatWaterRefillOperationId((BuildingInstanceId)restoredId, 19)
                == "crop-water-refill:" + restoredId + ":000019",
            "Public crop operation identity format changed.");
        results.Add("public operation identity contract\tPASS");
        string report = Path.Combine(output, "crop-persistence-boundary.tsv");
        using (FileStream file = new(report, FileMode.CreateNew, FileAccess.Write))
        using (StreamWriter writer = new(file))
            foreach (string result in results) writer.WriteLine(result);
        Debug.Log("ARCHITECTURE_CROP_PERSISTENCE=PASS; staged/rejected candidates; current-root replacement twice; DTO isolation; public identities");
    }
}
#endif
