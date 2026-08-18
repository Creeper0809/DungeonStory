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
            IItemTransferService transfers =
                scope.Container.Resolve<IItemTransferService>();
            IResourceEconomyContentCatalog catalog =
                scope.Container.Resolve<IResourceEconomyContentCatalog>();
            BlueprintResearchRuntime research = scope.Container
                .Resolve<ProgressionSceneRuntimeReferences>()
                .BlueprintResearch;
            IGridSystemProvider gridProvider =
                scope.Container.Resolve<IGridSystemProvider>();
            Require(research != null, "Research runtime is missing.");
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
            Require(
                catalog.TryGetCrop(
                    "crop:twilight-grain",
                    out CropDefinitionSO crop),
                "twilight grain definition is missing.");

            plotObject = new GameObject("CropPlot_Runtime_Verifier");
            Facility plot = plotObject.AddComponent<Facility>();
            scope.Container.Inject(plot);
            plot.SetGrid(grid);
            plot.Initialization(outdoorPlot, new Vector2Int(4, 0));
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            Require(
                runtime.TrySetCrop(
                    plot,
                    "crop:twilight-grain",
                    out string cropMessage),
                cropMessage);
            runtime.Tick();

            CropPlotSnapshot waiting = runtime.Plots.Single(entry =>
                entry.PlotId == plot.RequirePersistentInstanceId().Value);
            Require(
                waiting.Phase == CropPlotPhase.WaitingForMaterials,
                $"unexpected initial phase={waiting.Phase}");
            Require(
                waiting.RequiredMaterials.Count > 0,
                "outdoor crop plot requested no physical water.");
            foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials)
            {
                Require(
                    SpawnCropMaterial(
                        items,
                        transfers,
                        crop,
                        material.Key,
                        material.Value,
                        plot.centerPos,
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
                entry.buildingInstanceId == waiting.PlotId);
            Require(
                growing.phase == CropPlotPhase.Growing,
                $"crop did not enter growing phase: {growing.phase}");
            growing.growthHours = crop.GrowthHours;
            runtime.Restore(runtime.BuildRestore(growingSave));
            runtime.Tick();

            Require(
                runtime.TryGetWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    out CropPlotWorkSnapshot harvest)
                && harvest.Available,
                $"harvest work unavailable: {harvest.UnavailableReason}");
            lines.Add(VerifyHarvestOutputContainmentAdmission(
                runtime,
                plot,
                items,
                crop,
                harvest));
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
            VerifyStrictSaveIsolation(saveSection);

            indoorPlotObject = new GameObject("IndoorCropPlot_Runtime_Verifier");
            Facility indoor = indoorPlotObject.AddComponent<Facility>();
            scope.Container.Inject(indoor);
            indoor.SetGrid(grid);
            indoor.Initialization(indoorPlot, new Vector2Int(8, 0));
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            Require(
                runtime.TrySetCrop(
                    indoor,
                    "crop:cave-mushroom",
                    out string indoorCropMessage),
                indoorCropMessage);
            runtime.Tick();

            CropPlotSnapshot indoorWaiting = runtime.Plots.Single(entry =>
                entry.PlotId == indoor.RequirePersistentInstanceId().Value);
            const string waterItemId = "resource:clean-water";
            string fuelItemId = indoorWaiting.RequiredMaterials.Keys
                .SingleOrDefault(itemId => catalog.TryGetItem(
                        itemId,
                        out ResourceItemDefinitionSO definition)
                    && (definition.IngredientTags & ResourceIngredientTag.Fuel) != 0);
            Require(
                indoorWaiting.RequiredMaterials.ContainsKey(waterItemId)
                && indoorWaiting.RequiredMaterials.ContainsKey("material:compost")
                && !string.IsNullOrWhiteSpace(fuelItemId)
                && indoorWaiting.RequiredMaterials.ContainsKey(fuelItemId),
                "Indoor crop cycle must require water, compost, and fuel.");
            Require(
                catalog.TryGetCrop(
                    "crop:cave-mushroom",
                    out CropDefinitionSO indoorCrop),
                "cave mushroom definition is missing.");

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
                    SpawnCropMaterial(
                        items,
                        transfers,
                        indoorCrop,
                        material.Key,
                        material.Value,
                        indoor.centerPos,
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

    private static bool SpawnCropMaterial(
        IWorldItemStackRuntime items,
        IItemTransferService transfers,
        CropDefinitionSO crop,
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId,
        out int spawned)
    {
        if (!string.Equals(itemId, crop.SeedItemId, StringComparison.Ordinal))
        {
            return items.SpawnItemAt(
                itemId,
                amount,
                position,
                WorldItemStackState.FacilityBuffer,
                destinationId,
                out spawned);
        }

        spawned = 0;
        return crop.BaseGenome != null
            && transfers.TrySpawnItemWithComponents(
                itemId,
                amount,
                position,
                WorldItemStackState.FacilityBuffer,
                destinationId,
                new[]
                {
                    SeedLotItemStateCodec.Encode(new SeedLotState
                    {
                        cropId = crop.CropId,
                        cultivarGenomeId = crop.BaseGenome.GenomeId,
                        generation = 0,
                        pathogenLoad = 0f
                    })
                },
                out spawned);
    }

    private static string VerifyHarvestOutputContainmentAdmission(
        CropPlotRuntime runtime,
        Facility plot,
        IWorldItemStackRuntime items,
        CropDefinitionSO crop,
        CropPlotWorkSnapshot harvest)
    {
        CropPlotSaveData before = runtime.Capture().plots.Single(entry =>
            entry.buildingInstanceId == plot.RequirePersistentInstanceId().Value);
        string[] outputItemIds = new[]
            {
                crop.HarvestItemId,
                crop.SeedItemId
            }
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string outputItemId in outputItemIds)
        {
            HashSet<string> existingIds = items.GetAllStacks()
                .Where(stack => stack != null)
                .Select(stack => stack.StackId)
                .ToHashSet(StringComparer.Ordinal);
            WorldItemStackSnapshot fixture = null;
            try
            {
                Require(
                    items.SpawnItemAt(
                        outputItemId,
                        1,
                        plot.centerPos,
                        WorldItemStackState.Loose,
                        string.Empty,
                        out int spawned)
                    && spawned == 1,
                    $"Could not fill crop output containment for {outputItemId}.");
                fixture = items.GetAllStacks().Single(stack => stack != null
                    && !existingIds.Contains(stack.StackId)
                    && stack.Quantity == 1
                    && stack.State == WorldItemStackState.Loose
                    && string.IsNullOrWhiteSpace(stack.DestinationId)
                    && stack.Position == plot.centerPos
                    && string.Equals(
                        stack.ItemId,
                        outputItemId,
                        StringComparison.Ordinal));
                int physicalBefore = CountItem(items, outputItemId);
                Require(
                    runtime.TryGetWork(
                        plot,
                        BuiltInWorkTypeIds.Harvest,
                        out CropPlotWorkSnapshot blocked)
                    && !blocked.Available
                    && string.Equals(
                        blocked.UnavailableReason,
                        FailureCode.ProductionOutputSpaceUnavailable.ToString(),
                        StringComparison.Ordinal),
                    $"Crop harvest remained available while {outputItemId} containment was occupied.");
                Require(
                    !runtime.ApplyWork(
                        plot,
                        BuiltInWorkTypeIds.Harvest,
                        harvest.RequiredWork,
                        out bool completedWhileBlocked)
                    && !completedWhileBlocked,
                    $"Blocked crop harvest consumed work for {outputItemId}.");
                CropPlotSaveData blockedSave = runtime.Capture().plots.Single(entry =>
                    entry.buildingInstanceId == before.buildingInstanceId);
                Require(
                    blockedSave.phase == before.phase
                    && Math.Abs(blockedSave.harvestWork - before.harvestWork) < 0.0001f,
                    $"Output saturation mutated crop harvest state for {outputItemId}.");
                Require(
                    CountItem(items, outputItemId) == physicalBefore,
                    $"Blocked crop harvest created or deleted {outputItemId}.");
            }
            finally
            {
                if (fixture != null && fixture.Quantity > 0)
                    items.TryConsumeStackQuantity(
                        fixture.StackId,
                        fixture.Quantity,
                        out _);
            }

            Require(
                runtime.TryGetWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    out CropPlotWorkSnapshot recovered)
                && recovered.Available,
                $"Crop harvest did not recover after clearing {outputItemId} containment.");
        }

        return "PASS CROP_OUTPUT_CONTAINMENT_TYPED_BLOCK_RECOVERY "
            + $"plot={before.buildingInstanceId};outputs="
            + string.Join(",", outputItemIds)
            + ";workConserved=true;quantityConserved=true";
    }

    private static void VerifyStrictSaveIsolation(
        CropPlotSaveSection saveSection)
    {
        Require(
            saveSection is IDungeonSaveSectionPreflight
            && saveSection is IDungeonRollbackFreeSaveSection,
            "Crop-plot save section is not strict and rollback-free.");
        string before = saveSection.Capture();
        DungeonCropPlotSaveData invalid =
            JsonUtility.FromJson<DungeonCropPlotSaveData>(before);
        Require(invalid?.plots?.Count > 0, "Crop-plot isolation fixture is empty.");
        invalid.plots[0].sowWork = -1f;
        bool rejected = false;
        try
        {
            ((IDungeonSaveSectionPreflight)saveSection).ValidatePayload(
                JsonUtility.ToJson(invalid),
                saveSection.SectionVersion,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, "Negative crop-plot progress was accepted.");
        Require(
            string.Equals(before, saveSection.Capture(), StringComparison.Ordinal),
            "Failed crop-plot preflight mutated live state.");
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
