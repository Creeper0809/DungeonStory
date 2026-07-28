#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class CropPlotDebugScenarios
{
    private const string ReportPath =
        "docs/implementation-reports/crop-plot-runtime-latest.txt";

    [MenuItem("Tools/DungeonStory/Economy/Verify Crop Plot Runtime")]
    public static void VerifyRuntimeFromMenu()
    {
        List<string> lines = new List<string>
        {
            "# Crop Plot Runtime Verification",
            $"utc={DateTime.UtcNow:O}",
            $"playMode={Application.isPlaying}"
        };
        GameObject plotObject = null;
        GameObject indoorPlotObject = null;
        try
        {
            Require(Application.isPlaying, "Play Mode is required.");
            DungeonRuntimeLifetimeScope scope =
                UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
            Require(scope != null && scope.Container != null, "Runtime scope is missing.");

            CropPlotRuntime runtime = scope.Container.Resolve<CropPlotRuntime>();
            IWorldItemStackRuntime items =
                scope.Container.Resolve<IWorldItemStackRuntime>();
            IBlueprintResearchRuntimeProvider researchProvider =
                scope.Container.Resolve<IBlueprintResearchRuntimeProvider>();
            IGridSystemProvider gridProvider =
                scope.Container.Resolve<IGridSystemProvider>();
            Require(
                researchProvider.TryGetRuntime(out BlueprintResearchRuntime research),
                "Research runtime is missing.");
            research.State.Projects.Complete(
                new ResearchProjectId("research:agriculture:field"));
            research.State.Projects.Complete(
                new ResearchProjectId("research:agriculture:gathering"));
            research.State.Projects.Complete(
                new ResearchProjectId("research:agriculture:indoor"));
            Require(gridProvider.TryGetGrid(out Grid grid), "Grid is missing.");

            BuildingSO outdoorPlot = LoadBuilding("P23");
            BuildingSO indoorPlot = LoadBuilding("P24");
            Require(outdoorPlot != null, "P23 outdoor crop plot is missing.");
            Require(indoorPlot != null, "P24 indoor grow bed is missing.");
            Require(
                outdoorPlot.GetAbility<BuildingCropPlotAbility>() is { Indoor: false },
                "P23 crop plot ability is invalid.");
            Require(
                indoorPlot.GetAbility<BuildingCropPlotAbility>() is
                {
                    Indoor: true,
                    CompostPerCycle: 1,
                    FuelPerCycle: 1
                },
                "P24 crop plot ability is invalid.");
            Require(
                outdoorPlot.Facility.SupportsWork(BuiltInWorkTypeIds.Sow)
                && outdoorPlot.Facility.SupportsWork(BuiltInWorkTypeIds.Harvest),
                "P23 does not expose sow and harvest work.");

            plotObject = new GameObject("CropPlot_Runtime_Verifier");
            Facility plot = plotObject.AddComponent<Facility>();
            scope.Container.Inject(plot);
            plot.SetGrid(grid);
            plot.Initialization(outdoorPlot, new Vector2Int(4, 0));
            runtime.Restore(runtime.Capture());
            Require(
                runtime.TrySetCrop(
                    plot,
                    "crop:twilight-grain",
                    out string cropMessage),
                cropMessage);
            runtime.Tick();

            CropPlotSnapshot waiting = runtime.Plots.Single(entry =>
                entry.PlotId == $"crop-plot:{plot.id}:4:0");
            Require(
                waiting.Phase == CropPlotPhase.WaitingForMaterials,
                $"unexpected initial phase={waiting.Phase}");
            Require(
                waiting.RequiredMaterials.Count > 0,
                "outdoor crop plot requested no physical water.");
            foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials)
            {
                Require(
                    items.SpawnItemAt(
                        material.Key,
                        material.Value,
                        plot.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        waiting.MaterialDestinationId,
                        out int spawned)
                    && spawned == material.Value,
                    $"could not deliver {material.Key}");
            }

            runtime.Tick();
            Require(
                runtime.TryGetWork(
                    plot,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot sow)
                && sow.Available,
                $"sow work unavailable: {sow.UnavailableReason}");
            Require(
                runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Sow,
                    sow.RequiredWork,
                    out bool sowed)
                && sowed,
                "sowing did not complete.");

            DungeonCropPlotSaveData growingSave = runtime.Capture();
            CropPlotSaveData growing = growingSave.plots.Single(entry =>
                entry.plotId == waiting.PlotId);
            Require(
                growing.phase == CropPlotPhase.Growing,
                $"crop did not enter growing phase: {growing.phase}");
            growing.growthHours = 999f;
            runtime.Restore(growingSave);
            runtime.Tick();

            Require(
                runtime.TryGetWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    out CropPlotWorkSnapshot harvest)
                && harvest.Available,
                $"harvest work unavailable: {harvest.UnavailableReason}");
            IResourceEconomyContentCatalog catalog =
                scope.Container.Resolve<IResourceEconomyContentCatalog>();
            Require(
                catalog.TryGetCrop(
                    "crop:twilight-grain",
                    out CropDefinitionSO crop),
                "twilight grain definition is missing.");
            int stockBefore = CountItem(items, crop.HarvestItemId);
            Require(
                runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    harvest.RequiredWork,
                    out bool harvested)
                && harvested,
                "harvest did not complete.");
            int stockAfter = CountItem(items, crop.HarvestItemId);
            Require(
                stockAfter >= stockBefore + crop.Yield,
                $"physical harvest missing: {stockBefore}->{stockAfter}");

            CropPlotSaveSection saveSection =
                new CropPlotSaveSection(runtime);
            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            saveSection.Restore(
                saveSection.Capture(),
                saveSection.SectionVersion,
                report);
            Require(
                report.Success,
                string.Join(" / ", report.Errors));

            indoorPlotObject = new GameObject("IndoorCropPlot_Runtime_Verifier");
            Facility indoor = indoorPlotObject.AddComponent<Facility>();
            scope.Container.Inject(indoor);
            indoor.SetGrid(grid);
            indoor.Initialization(indoorPlot, new Vector2Int(8, 0));
            runtime.Restore(runtime.Capture());
            Require(
                runtime.TrySetCrop(
                    indoor,
                    "crop:cave-mushroom",
                    out string indoorCropMessage),
                indoorCropMessage);
            runtime.Tick();

            CropPlotSnapshot indoorWaiting = runtime.Plots.Single(entry =>
                entry.PlotId == $"crop-plot:{indoor.id}:8:0");
            string waterItemId =
                DungeonItemCatalogSO.StockItemId(StockCategory.Water);
            string fuelItemId =
                DungeonItemCatalogSO.StockItemId(StockCategory.Fuel);
            Require(
                indoorWaiting.RequiredMaterials.ContainsKey(waterItemId)
                && indoorWaiting.RequiredMaterials.ContainsKey("material:compost")
                && indoorWaiting.RequiredMaterials.ContainsKey(fuelItemId),
                "Indoor crop cycle must require water, compost, and fuel.");

            int waterRequired = indoorWaiting.RequiredMaterials[waterItemId];
            Require(
                items.SpawnItemAt(
                    waterItemId,
                    waterRequired,
                    indoor.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    indoorWaiting.MaterialDestinationId,
                    out int indoorWaterSpawned)
                && indoorWaterSpawned == waterRequired,
                "Indoor water delivery failed.");
            runtime.Tick();
            Require(
                runtime.TryGetWork(
                    indoor,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot blockedIndoorSow)
                && !blockedIndoorSow.Available,
                "Indoor sowing opened before compost and fuel arrived.");

            foreach (KeyValuePair<string, int> material in
                     indoorWaiting.RequiredMaterials.Where(entry =>
                         !string.Equals(
                             entry.Key,
                             waterItemId,
                             StringComparison.Ordinal)))
            {
                Require(
                    items.SpawnItemAt(
                        material.Key,
                        material.Value,
                        indoor.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        indoorWaiting.MaterialDestinationId,
                        out int indoorMaterialSpawned)
                    && indoorMaterialSpawned == material.Value,
                    $"Indoor material delivery failed: {material.Key}");
            }

            runtime.Tick();
            Require(
                runtime.TryGetWork(
                    indoor,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot indoorSow)
                && indoorSow.Available,
                $"Indoor sowing remained blocked: {indoorSow.UnavailableReason}");
            Require(
                runtime.ApplyWork(
                    indoor,
                    BuiltInWorkTypeIds.Sow,
                    indoorSow.RequiredWork,
                    out bool indoorSowed)
                && indoorSowed,
                "Indoor sowing did not complete.");
            CropPlotSnapshot indoorGrowing = runtime.Plots.Single(entry =>
                entry.PlotId == indoorWaiting.PlotId);
            Require(
                indoorGrowing.Phase == CropPlotPhase.Growing,
                $"Indoor crop did not start growing: {indoorGrowing.Phase}");

            lines.Add($"plot={waiting.PlotId}");
            lines.Add($"crop={crop.CropId}");
            lines.Add("materials=" + string.Join(
                ",",
                waiting.RequiredMaterials.Select(entry =>
                    $"{entry.Key}x{entry.Value}")));
            lines.Add($"harvest={stockBefore}->{stockAfter}");
            lines.Add($"indoorPlot={indoorWaiting.PlotId}");
            lines.Add("indoorMaterials=" + string.Join(
                ",",
                indoorWaiting.RequiredMaterials.Select(entry =>
                    $"{entry.Key}x{entry.Value}")));
            lines.Add($"indoorPhase={indoorGrowing.Phase}");
            lines.Add("valid=true");
            WriteReport(lines);
            Debug.Log(string.Join(Environment.NewLine, lines));
        }
        catch (Exception exception)
        {
            lines.Add("valid=false");
            lines.Add("error=" + exception);
            WriteReport(lines);
            Debug.LogException(exception);
        }
        finally
        {
            if (plotObject != null)
            {
                UnityEngine.Object.Destroy(plotObject);
            }

            if (indoorPlotObject != null)
            {
                UnityEngine.Object.Destroy(indoorPlotObject);
            }
        }
    }

    private static BuildingSO LoadBuilding(string code)
    {
        return AssetDatabase
            .FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building/Modular" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(building =>
                building?.GetAbility<BuildingFacilityPartAbility>()?.code
                == code);
    }

    private static int CountItem(
        IWorldItemStackRuntime items,
        string itemId)
    {
        return items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    itemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void WriteReport(IEnumerable<string> lines)
    {
        string absolutePath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ".");
        File.WriteAllLines(absolutePath, lines);
    }
}
#endif
