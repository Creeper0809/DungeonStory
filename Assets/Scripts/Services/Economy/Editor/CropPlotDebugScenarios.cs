#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class CropPlotDebugScenarios
{
    private const string ReportPath =
        "docs/implementation-reports/crop-plot-runtime-latest.txt";
    public const string RequestPath =
        "Temp/v27-crop-plot-runtime.request";

    [MenuItem("Tools/DungeonStory/Economy/Request Crop Plot Runtime Verification")]
    public static void RequestRuntimeVerification()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, "requested");
        if (EditorApplication.isPlaying)
        {
            CropPlotDebugPlayModeRunner.StartPending();
            return;
        }
        EditorApplication.EnterPlaymode();
    }

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
        GameObject correlationConflictPlotObject = null;
        GameObject indoorPlotObject = null;
        GameObject fungalShelfObject = null;
        Facility detachedRoundTripPlot = null;
        Facility detachedRoundTripConflictPlot = null;
        try
        {
            Require(
                CropPhysicalTransactionFixture.Run(),
                "crop physical transaction fixture failed");
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
            research.State.Projects.Complete(
                new ResearchProjectId("research:forestry:fungal"));
            Require(gridProvider.TryGetGrid(out Grid grid), "Grid is missing.");

            BuildingSO outdoorPlot = LoadBuilding("P23");
            BuildingSO indoorPlot = LoadBuilding("P24");
            BuildingSO fungalShelf = LoadBuilding("RF13");
            Require(outdoorPlot != null, "P23 outdoor crop plot is missing.");
            Require(indoorPlot != null, "P24 indoor grow bed is missing.");
            Require(fungalShelf != null, "RF13 fungal shelf is missing.");
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
            BuildingCropPlotAbility fungalAbility =
                fungalShelf.GetAbility<BuildingCropPlotAbility>();
            Require(
                fungalAbility != null
                && fungalAbility.Indoor
                && fungalAbility.CompostPerCycle == 1
                && fungalAbility.CycleSupplyInputs.Count == 1
                && fungalAbility.CycleSupplyInputs[0].ItemId
                    == "supply:inoculated-log"
                && fungalAbility.CycleSupplyInputs[0].Amount == 1,
                "RF13 must consume one inoculated-log section per cycle.");
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
            correlationConflictPlotObject = new GameObject(
                "CropPlot_CorrelationConflict_Verifier");
            Facility correlationConflictPlot =
                correlationConflictPlotObject.AddComponent<Facility>();
            scope.Container.Inject(correlationConflictPlot);
            correlationConflictPlot.SetGrid(grid);
            correlationConflictPlot.Initialization(
                outdoorPlot,
                new Vector2Int(6, 0));
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            Require(
                runtime.TrySetCrop(
                    plot,
                    "crop:twilight-grain",
                    out string cropMessage),
                cropMessage);
            Require(
                runtime.TrySetCrop(
                    correlationConflictPlot,
                    "crop:twilight-grain",
                    out string correlationCropMessage),
                correlationCropMessage);
            const string CropExecutionActionId =
                "qa:crop-plan-execution:outdoor-primary";
            Require(
                runtime.TryBindNextCycle(
                    CropExecutionActionId,
                    plot.RequirePersistentInstanceId().Value,
                    crop.CropId,
                    out string bindFailure),
                "Could not bind Crop execution action: " + bindFailure);
            Require(
                !runtime.TryBindNextCycle(
                    CropExecutionActionId,
                    correlationConflictPlot.RequirePersistentInstanceId().Value,
                    crop.CropId,
                    out string duplicateBindFailure)
                && string.Equals(
                    duplicateBindFailure,
                    "crop-cycle-correlation-global-conflict",
                    StringComparison.Ordinal),
                "Duplicate Crop action correlation was not rejected globally: "
                + duplicateBindFailure);
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
            Require(
                growing.cycleExecutionReceipt.status
                    == CropCycleExecutionReceiptStatus.Active
                && growing.cycleExecutionReceipt.explicitCorrelation
                && string.Equals(
                    growing.cycleExecutionReceipt.correlationId,
                    CropExecutionActionId,
                    StringComparison.Ordinal)
                && !runtime.TryCaptureExecutionReceipt(
                    CropExecutionActionId,
                    out _),
                "Active Crop execution receipt was not durable or became observable before terminal completion.");
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
            lines.Add(VerifyHarvestOutputFacilityBufferWaitRestoreRetry(
                runtime,
                plot,
                items,
                scope.Container,
                catalog,
                crop,
                harvest,
                CropExecutionActionId,
                out int stockBefore,
                out int stockAfter));

            CropPlotSaveSection saveSection =
                scope.Container.Resolve<CropPlotSaveSection>();
            string sectionPayload = saveSection.Capture();
            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            IRestoreWorldCandidateQuery candidateQuery =
                scope.Container.Resolve<IRestoreWorldCandidateQuery>();
            IRestoreWorldCandidatePublisher candidatePublisher =
                scope.Container.Resolve<IRestoreWorldCandidatePublisher>();
            Require(
                !candidateQuery.TryGetBuildings(out _),
                "Crop-plot focused restore started with an occupied detached-world slot.");
            GameObject detachedRoundTripObject =
                new GameObject("CropPlot_Detached_RoundTrip_Verifier");
            detachedRoundTripPlot =
                detachedRoundTripObject.AddComponent<Facility>();
            detachedRoundTripPlot.PrepareForDetachedRestore();
            scope.Container.Inject(detachedRoundTripPlot);
            detachedRoundTripPlot.RestorePersistentIdentity(
                plot.RequirePersistentInstanceId());
            detachedRoundTripPlot.SetGrid(grid);
            detachedRoundTripPlot.Initialization(
                outdoorPlot,
                plot.centerPos);
            GameObject detachedConflictObject =
                new GameObject("CropPlot_Detached_Conflict_RoundTrip_Verifier");
            detachedRoundTripConflictPlot =
                detachedConflictObject.AddComponent<Facility>();
            detachedRoundTripConflictPlot.PrepareForDetachedRestore();
            scope.Container.Inject(detachedRoundTripConflictPlot);
            detachedRoundTripConflictPlot.RestorePersistentIdentity(
                correlationConflictPlot.RequirePersistentInstanceId());
            detachedRoundTripConflictPlot.SetGrid(grid);
            detachedRoundTripConflictPlot.Initialization(
                outdoorPlot,
                correlationConflictPlot.centerPos);
            bool candidatePublished = false;
            try
            {
                candidatePublisher.SetFacilityCandidate(
                    grid,
                    new BuildableObject[]
                    {
                        detachedRoundTripPlot,
                        detachedRoundTripConflictPlot
                    });
                candidatePublished = true;
                saveSection.Restore(
                    sectionPayload,
                    saveSection.SectionVersion,
                    report);
            }
            finally
            {
                if (candidatePublished)
                    candidatePublisher.ClearFacilityCandidate();
                if (detachedRoundTripPlot != null)
                {
                    detachedRoundTripPlot.DiscardDetachedRestore();
                    detachedRoundTripPlot = null;
                }
                if (detachedRoundTripConflictPlot != null)
                {
                    detachedRoundTripConflictPlot.DiscardDetachedRestore();
                    detachedRoundTripConflictPlot = null;
                }
            }
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

            Require(
                catalog.TryGetItem(
                    "supply:inoculated-log",
                    out ResourceItemDefinitionSO inoculatedLog)
                && Mathf.RoundToInt(inoculatedLog.UnitWeight * 1000f) == 700,
                "Inoculated-log authority must be exactly 700 g.");
            fungalShelfObject = new GameObject("FungalShelf_Runtime_Verifier");
            Facility fungal = fungalShelfObject.AddComponent<Facility>();
            scope.Container.Inject(fungal);
            fungal.SetGrid(grid);
            fungal.Initialization(fungalShelf, new Vector2Int(12, 0));
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            Require(
                runtime.TrySetCrop(
                    fungal,
                    "crop:cave-mushroom",
                    out string fungalCropMessage),
                fungalCropMessage);
            runtime.Tick();

            CropPlotSnapshot fungalWaiting = runtime.Plots.Single(entry =>
                entry.PlotId == fungal.RequirePersistentInstanceId().Value);
            Require(
                fungalWaiting.RequiredMaterials.TryGetValue(
                    "supply:inoculated-log",
                    out int requiredLogs)
                && requiredLogs == 1,
                "RF13 did not request exactly one inoculated-log section.");
            int inoculatedBefore = CountItem(items, "supply:inoculated-log");
            foreach (KeyValuePair<string, int> material in
                     fungalWaiting.RequiredMaterials)
            {
                Require(
                    SpawnCropMaterial(
                        items,
                        transfers,
                        indoorCrop,
                        material.Key,
                        material.Value,
                        fungal.centerPos,
                        fungalWaiting.MaterialDestinationId,
                        out int fungalMaterialSpawned)
                    && fungalMaterialSpawned == material.Value,
                    $"RF13 material delivery failed: {material.Key}");
            }

            runtime.Tick();
            Require(
                runtime.TryGetWork(
                    fungal,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot fungalSow)
                && fungalSow.Available,
                $"RF13 sowing remained blocked: {fungalSow.UnavailableReason}");
            Require(
                runtime.ApplyWork(
                    fungal,
                    BuiltInWorkTypeIds.Sow,
                    fungalSow.RequiredWork,
                    out bool fungalSowed)
                && fungalSowed,
                "RF13 sowing did not complete.");
            CropPlotSnapshot fungalGrowing = runtime.Plots.Single(entry =>
                entry.PlotId == fungalWaiting.PlotId);
            Require(
                fungalGrowing.Phase == CropPlotPhase.Growing,
                $"RF13 crop did not start growing: {fungalGrowing.Phase}");
            Require(
                CountItem(items, "supply:inoculated-log")
                    == inoculatedBefore,
                "RF13 did not consume exactly the one spawned inoculated-log section.");

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
            lines.Add($"fungalPlot={fungalWaiting.PlotId}");
            lines.Add("fungalMaterials=" + string.Join(
                ",",
                fungalWaiting.RequiredMaterials.Select(entry =>
                    $"{entry.Key}x{entry.Value}")));
            lines.Add($"inoculatedLog=700g;consumed={requiredLogs}");
            lines.Add($"fungalPhase={fungalGrowing.Phase}");
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

            if (correlationConflictPlotObject != null)
            {
                UnityEngine.Object.Destroy(correlationConflictPlotObject);
            }

            if (indoorPlotObject != null)
            {
                UnityEngine.Object.Destroy(indoorPlotObject);
            }

            if (fungalShelfObject != null)
            {
                UnityEngine.Object.Destroy(fungalShelfObject);
            }

            if (detachedRoundTripPlot != null)
            {
                detachedRoundTripPlot.DiscardDetachedRestore();
            }

            if (detachedRoundTripConflictPlot != null)
            {
                detachedRoundTripConflictPlot.DiscardDetachedRestore();
            }
        }
    }

    private static BuildingSO LoadBuilding(string code)
    {
        return AssetDatabase
            .FindAssets(
                "t:BuildingSO",
                new[]
                {
                    "Assets/Resources/SO/Building/Modular",
                    "Assets/Resources/SO/Building/ResearchOverhaul"
                })
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

    private static string VerifyHarvestOutputFacilityBufferWaitRestoreRetry(
        CropPlotRuntime runtime,
        Facility plot,
        IWorldItemStackRuntime items,
        IObjectResolver services,
        IResourceEconomyContentCatalog catalog,
        CropDefinitionSO crop,
        CropPlotWorkSnapshot harvest,
        string executionActionId,
        out int stockBefore,
        out int stockAfter)
    {
        Require(services != null, "Runtime service resolver is missing.");
        IFacilityBufferMassAdmissionService admission =
            services.Resolve<IFacilityBufferMassAdmissionService>();
        IFacilityBufferPlannedOutputPublicationService publication =
            services.Resolve<IFacilityBufferPlannedOutputPublicationService>();
        Require(admission != null, "Facility-buffer admission service is missing.");
        Require(publication != null,
            "Facility-buffer publication service is missing.");
        BuildingInstanceId plotId = plot.RequirePersistentInstanceId();
        string destinationId = ProductionOutputDestinationId
            .FromFacility(plotId)
            .Value;
        Require(catalog.TryGetItem(
                crop.HarvestItemId,
                out ResourceItemDefinitionSO harvestDefinition),
            "Crop harvest item definition is missing.");
        long harvestUnitMassGrams = Mathf.RoundToInt(
            harvestDefinition.UnitWeight * 1000f);
        Require(harvestUnitMassGrams > 0L,
            "Crop harvest item has no positive physical mass.");

        IProductionFacilityHandleQuery facilityHandles =
            services.Resolve<IProductionFacilityHandleQuery>();
        IProductionOutputCapabilityRegistry capabilities =
            services.Resolve<IProductionOutputCapabilityRegistry>();
        IProductionOutputMaximumMassRegistry maximumMass =
            services.Resolve<IProductionOutputMaximumMassRegistry>();
        IProductionOutputBufferCapacityProjector capacityProjector =
            services.Resolve<IProductionOutputBufferCapacityProjector>();
        IProductionOutputDestinationAuthorityRuntime destinations =
            services.Resolve<IProductionOutputDestinationAuthorityRuntime>();
        ICharacterPerformanceDefinitionMaximumQuery performanceMaximum =
            services.Resolve<ICharacterPerformanceDefinitionMaximumQuery>();
        IGameplayEffectResultBoundsQuery effectBounds =
            services.Resolve<IGameplayEffectResultBoundsQuery>();
        ProductionFacilityHandle facility = facilityHandles.CaptureFacility(plot);
        ProductionOutputCapabilityDescriptor harvestCapability =
            capabilities.CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.HarvestOutputLineId(crop.CropId),
                crop.HarvestItemId,
                ProductionOutputCapabilityIds.StandardDefinition);
        ProductionOutputCapabilityDescriptor seedCapability =
            capabilities.CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.SeedOutputLineId(crop.CropId),
                crop.SeedItemId,
                ProductionOutputCapabilityIds.CropHarvestSeedLot);
        ProductionOutputBatchMaximumMassProof maximumProof = new(new[]
        {
            maximumMass.CaptureDeclared(
                harvestCapability,
                CropHarvestOutputMaximumAuthority.ResolveMaximumHarvestQuantity(
                    crop,
                    indoor: false,
                    performanceMaximum)),
            maximumMass.CaptureDeclared(
                seedCapability,
                CropHarvestOutputMaximumAuthority
                    .ResolveMaximumReturnedSeedQuantity(effectBounds))
        });
        ProductionOutputBufferCapacitySourceSnapshot capacitySource =
            capacityProjector.CaptureSource(facility, maximumProof);
        Require(destinations.TryEnsure(
                facility,
                capacitySource.RequiredMinimumCapacityGrams,
                out FacilityBufferCapacityProfile authoredProfile,
                out string capacityFailure),
            "Could not publish crop output capacity authority: " + capacityFailure);
        Require(admission.TryGetCapacity(
                destinationId,
                plot.centerPos,
                out FacilityBufferMassCapacitySnapshot initialCapacity),
            "Crop output FacilityBuffer capacity authority is missing.");
        Require(initialCapacity.Profile.MaxMassGrams == authoredProfile.MaxMassGrams
            && initialCapacity.Profile.MaxMassGrams
                >= capacitySource.RequiredMinimumCapacityGrams,
            "Crop output FacilityBuffer capacity drifted from its maximum proof.");
        Require(initialCapacity.ReservedMassGrams == 0L,
            "Crop output FacilityBuffer already has reserved mass before the fixture.");
        Require(!items.GetAllStacks().Any(stack => stack != null
                && stack.State is WorldItemStackState.FacilityBuffer
                    or WorldItemStackState.FacilityOutputBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)),
            "Crop output FacilityBuffer already has physical occupancy before the fixture.");

        long fillerQuantityLong = initialCapacity.Profile.MaxMassGrams
            / harvestUnitMassGrams;
        Require(fillerQuantityLong is > 0L and <= int.MaxValue,
            "Crop output capacity cannot be saturated by the harvest item fixture.");
        int fillerQuantity = checked((int)fillerQuantityLong);
        FacilityBufferPlannedOutputRequest capacityFixture = new(
            "qa:crop-output-capacity-wait:" + plotId.Value,
            "production-output-batch:qa:crop-output-capacity-wait:"
                + plotId.Value,
            new string('a', 64),
            destinationId,
            plot.centerPos,
            initialCapacity.Profile.OwnerDomain,
            initialCapacity.Profile.OwnerOperationId,
            initialCapacity.Profile.OwnerFacilityId,
            initialCapacity.Profile.CapacityRevision,
            new[]
            {
                new FacilityBufferPlannedOutputSlice(
                    "qa:crop-output-capacity-fill:" + crop.CropId,
                    PhysicalItemMassSubject.ForDefinition(
                        (ItemDefinitionId)crop.HarvestItemId),
                    fillerQuantity)
            },
            capacitySource.SourceDigest,
            capacitySource.RequiredMinimumCapacityGrams);
        FacilityBufferPlannedOutputToken capacityToken = default;
        bool capacityReserved = false;
        stockBefore = CountItem(items, crop.HarvestItemId);
        stockAfter = stockBefore;
        int seedStockBefore = CountItem(items, crop.SeedItemId);
        try
        {
            Require(
                admission.TryReservePlannedOutput(
                    capacityFixture,
                    out capacityToken,
                    out FacilityBufferMassAdmissionFailureCode reserveFailure,
                    out string reserveReason),
                "Could not reserve the competing crop-output capacity fixture: "
                    + reserveFailure + ":" + reserveReason);
            capacityReserved = true;
            Require(admission.TryGetCapacity(
                    destinationId,
                    plot.centerPos,
                    out FacilityBufferMassCapacitySnapshot saturated)
                && saturated.ReservedMassGrams
                    + harvestUnitMassGrams > saturated.Profile.MaxMassGrams,
                "Competing reservation did not leave less than one harvest item of capacity.");

            Require(runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    harvest.RequiredWork,
                    out bool completedWhileFull)
                && !completedWhileFull,
                "Capacity-blocked crop harvest did not retain its completed work for retry.");
            DungeonCropPlotSaveData waitingCapture = runtime.Capture();
            CropPlotSaveData waiting = waitingCapture.plots.Single(entry =>
                entry.buildingInstanceId == plotId.Value);
            Require(waiting.pendingHarvest.phase == CropHarvestOutputPhase.Frozen
                && waiting.pendingHarvest.outputPublication.IsEmpty
                && waiting.pendingHarvest.harvestQuantity > 0
                && waiting.pendingHarvest.seedQuantity > 0
                && Mathf.Approximately(waiting.harvestWork, harvest.RequiredWork),
                "Capacity wait did not preserve one frozen crop-output vector "
                + "before admission ownership was available.");
            Require(CountItem(items, crop.HarvestItemId) == stockBefore
                && CountItem(items, crop.SeedItemId) == seedStockBefore,
                "Capacity wait published a partial crop-output vector.");

            string frozenOperationId = waiting.pendingHarvest.operationId;
            string frozenOutcomeFingerprint =
                waiting.pendingHarvest.ecologyOutcomeFingerprint;
            int expectedHarvestQuantity = waiting.pendingHarvest.harvestQuantity;
            int expectedSeedQuantity = waiting.pendingHarvest.seedQuantity;
            string expectedSeedCanonical = SeedLotItemStateCodec
                .Encode(waiting.pendingHarvest.returnedSeedLot)
                .ToCanonicalString();

            runtime.Restore(runtime.BuildRestore(waitingCapture));
            runtime.Tick();
            CropPlotSaveData restoredWaiting = runtime.Capture().plots.Single(entry =>
                entry.buildingInstanceId == plotId.Value);
            Require(restoredWaiting.pendingHarvest.phase
                    == CropHarvestOutputPhase.Frozen
                && string.Equals(
                    restoredWaiting.pendingHarvest.operationId,
                    frozenOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    restoredWaiting.pendingHarvest.ecologyOutcomeFingerprint,
                    frozenOutcomeFingerprint,
                    StringComparison.Ordinal)
                && restoredWaiting.pendingHarvest.harvestQuantity
                    == expectedHarvestQuantity
                && restoredWaiting.pendingHarvest.seedQuantity
                    == expectedSeedQuantity,
                "Frozen crop-output vector changed across capture and restore.");

            Require(admission.TryReleasePlannedOutput(
                    capacityToken,
                    FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                    out FacilityBufferMassAdmissionFailureCode releaseFailure,
                    out string releaseReason),
                "Could not release competing crop-output capacity: "
                    + releaseFailure + ":" + releaseReason);
            capacityReserved = false;

            Require(runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    harvest.RequiredWork,
                    out bool completedOnRetry)
                && completedOnRetry,
                "Frozen crop output did not complete after capacity was released.");
            stockAfter = CountItem(items, crop.HarvestItemId);
            int seedStockAfter = CountItem(items, crop.SeedItemId);
            Require(stockAfter == stockBefore + expectedHarvestQuantity
                && seedStockAfter == seedStockBefore + expectedSeedQuantity,
                "Crop retry did not publish the exact frozen harvest and seed quantities. "
                + $"harvest={stockBefore}->{stockAfter} expectedDelta={expectedHarvestQuantity}, "
                + $"seed={seedStockBefore}->{seedStockAfter} expectedDelta={expectedSeedQuantity}.");

            WorldItemStackSnapshot[] published = items.GetAllStacks()
                .Where(stack => stack != null
                    && stack.State == WorldItemStackState.Loose
                    && stack.Position == plot.centerPos
                    && string.IsNullOrEmpty(stack.DestinationId))
                .ToArray();
            Require(published.Sum(stack => string.Equals(
                        stack.ItemId,
                        crop.HarvestItemId,
                        StringComparison.Ordinal)
                    ? stack.Quantity
                    : 0) == expectedHarvestQuantity
                && published.Sum(stack => string.Equals(
                        stack.ItemId,
                        crop.SeedItemId,
                        StringComparison.Ordinal)
                    ? stack.Quantity
                    : 0) == expectedSeedQuantity
                && published.Any(stack => string.Equals(
                        stack.ItemId,
                        crop.SeedItemId,
                        StringComparison.Ordinal)
                    && stack.Components.Count(component => component != null
                        && string.Equals(
                            component.componentTypeId,
                            SeedLotItemStateCodec.ComponentTypeId,
                            StringComparison.Ordinal)) == 1
                    && string.Equals(
                        stack.Components.Single(component => component != null
                            && string.Equals(
                                component.componentTypeId,
                                SeedLotItemStateCodec.ComponentTypeId,
                                StringComparison.Ordinal)).ToCanonicalString(),
                        expectedSeedCanonical,
                        StringComparison.Ordinal)),
                "Released crop output lost its exact two-line quantity or seed-lot state.");

            CropPlotSaveData completed = runtime.Capture().plots.Single(entry =>
                entry.buildingInstanceId == plotId.Value);
            Require(completed.pendingHarvest.phase == CropHarvestOutputPhase.None
                && completed.nextHarvestOperationSequence
                    == waiting.nextHarvestOperationSequence + 1,
                "Completed crop retry did not retire exactly one frozen operation.");
            Require(runtime.TryCaptureExecutionReceipt(
                    executionActionId,
                    out CropPlanExecutionReceipt executionReceipt)
                && executionReceipt.Succeeded
                && executionReceipt.ExplicitCorrelation
                && string.Equals(
                    executionReceipt.PlotId,
                    plotId.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    executionReceipt.HarvestOperationId,
                    frozenOperationId,
                    StringComparison.Ordinal)
                && executionReceipt.Outputs.Count == 2
                && executionReceipt.Outputs.Sum(output => output.Quantity)
                    == expectedHarvestQuantity + expectedSeedQuantity
                && executionReceipt.Outputs.Sum(output => output.MassGrams)
                    == executionReceipt.OutputMassGrams,
                "Completed Crop action did not expose its exact terminal receipt.");
            Require(publication.TryCaptureBatch(
                    executionReceipt.OutputBatchCommitId,
                    allowAcknowledged: true,
                    out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                    out bool batchAcknowledged,
                    out FacilityBufferPlannedOutputPublicationFailureCode
                        captureFailure,
                    out string captureReason)
                && batchAcknowledged
                && batch != null
                && batch.TotalMassGrams == executionReceipt.OutputMassGrams
                && string.Equals(
                    batch.OutcomeFingerprint,
                    executionReceipt.OutputOutcomeFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    batch.PlannedOutputFingerprint,
                    executionReceipt.PlannedOutputFingerprint,
                    StringComparison.Ordinal)
                && executionReceipt.Outputs.All(output => batch.Stacks.Any(stack =>
                    string.Equals(
                        stack.OutputLineId,
                        output.OutputLineId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        output.ItemId,
                        StringComparison.Ordinal)
                    && stack.Quantity == output.Quantity
                    && stack.MassGrams == output.MassGrams)),
                "Crop receipt did not exact-join its acknowledged physical batch: "
                + captureFailure + ":" + captureReason);

            string terminalDigest = executionReceipt.RuntimeReceiptDigest;
            runtime.Tick();
            CropPlotSnapshot awaitingAck = runtime.Plots.Single(entry =>
                entry.PlotId == plotId.Value);
            Require(string.Equals(
                    awaitingAck.BlockedReason,
                    "crop-cycle-execution-receipt-awaiting-acknowledgement",
                    StringComparison.Ordinal),
                "Explicit terminal receipt did not block automatic overwrite.");
            DungeonCropPlotSaveData terminalSave = runtime.Capture();
            runtime.Restore(runtime.BuildRestore(terminalSave));
            Require(runtime.TryCaptureExecutionReceipt(
                    executionActionId,
                    out CropPlanExecutionReceipt restoredReceipt)
                && string.Equals(
                    restoredReceipt.RuntimeReceiptDigest,
                    terminalDigest,
                    StringComparison.Ordinal),
                "Crop terminal receipt drifted across save/restore.");
            Require(!runtime.TryAcknowledgeExecutionReceipt(
                    executionActionId,
                    new string('0', 64),
                    out string staleAcknowledgementFailure)
                && string.Equals(
                    staleAcknowledgementFailure,
                    "crop-cycle-execution-receipt-digest-mismatch",
                    StringComparison.Ordinal)
                && runtime.TryCaptureExecutionReceipt(
                    executionActionId,
                    out CropPlanExecutionReceipt retainedAfterStaleAck)
                && string.Equals(
                    retainedAfterStaleAck.RuntimeReceiptDigest,
                    terminalDigest,
                    StringComparison.Ordinal),
                "Stale Crop receipt acknowledgement removed or changed the live owner.");
            Require(runtime.TryAcknowledgeExecutionReceipt(
                    executionActionId,
                    executionReceipt.RuntimeReceiptDigest,
                    out string acknowledgementFailure),
                "Crop terminal receipt acknowledgement failed: "
                + acknowledgementFailure);
            Require(!runtime.TryCaptureExecutionReceipt(
                    executionActionId,
                    out _),
                "Acknowledged Crop terminal receipt remained observable.");
            Require(!runtime.TryAcknowledgeExecutionReceipt(
                    executionActionId,
                    executionReceipt.RuntimeReceiptDigest,
                    out string duplicateAcknowledgementFailure)
                && string.Equals(
                    duplicateAcknowledgementFailure,
                    "crop-cycle-execution-receipt-not-found",
                    StringComparison.Ordinal),
                "Duplicate Crop receipt acknowledgement was not rejected.");
            runtime.Tick();
            CropPlotSaveData acknowledgedSave = runtime.Capture().plots.Single(entry =>
                entry.buildingInstanceId == plotId.Value);
            Require(acknowledgedSave.cycleExecutionReceipt.IsEmpty,
                "Acknowledged Crop terminal receipt was not retired.");
            Require(!runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    harvest.RequiredWork,
                    out bool replayCompleted)
                && !replayCompleted
                && CountItem(items, crop.HarvestItemId) == stockAfter
                && CountItem(items, crop.SeedItemId) == seedStockAfter,
                "Completed crop operation replayed its physical output.");
        }
        finally
        {
            if (capacityReserved)
            {
                admission.TryReleasePlannedOutput(
                    capacityToken,
                    FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                    out _,
                    out _);
            }
        }

        return "PASS CROP_OUTPUT_FACILITY_BUFFER_WAIT_RESTORE_RETRY_EXACT_ONCE "
            + $"plot={plotId.Value};harvest={stockBefore}->{stockAfter}"
            + ";capacityWait=true;frozenRestore=true;replayDelta=0"
            + ";executionReceipt=true;physicalBatchJoin=true"
            + ";terminalRestore=true;ackRetention=true";
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

public sealed class CropPlotDebugPlayModeRunner : MonoBehaviour
{
    private const float ResolveTimeoutSeconds = 30f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => StartPending();

    internal static void StartPending()
    {
        if (!File.Exists(CropPlotDebugScenarios.RequestPath))
            return;
        File.Delete(CropPlotDebugScenarios.RequestPath);
        if (FindFirstObjectByType<CropPlotDebugPlayModeRunner>() != null)
            return;
        new GameObject(nameof(CropPlotDebugPlayModeRunner))
            .AddComponent<CropPlotDebugPlayModeRunner>();
    }

    private IEnumerator Start()
    {
        float deadline = Time.realtimeSinceStartup + ResolveTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            DungeonRuntimeLifetimeScope scope = FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>();
            if (scope?.Container != null)
                break;
            yield return null;
        }

        try
        {
            CropPlotDebugScenarios.VerifyRuntimeFromMenu();
            string reportPath = Path.GetFullPath(
                "docs/implementation-reports/crop-plot-runtime-latest.txt");
            string report = File.Exists(reportPath)
                ? File.ReadAllText(reportPath)
                : string.Empty;
            if (!report.Contains("valid=true", StringComparison.Ordinal))
            {
                Debug.LogError(
                    "CROP_PLOT_REQUESTED_PLAYMODE_VERIFICATION_FAILED");
            }
            else
            {
                Debug.Log("CROP_PLOT_REQUESTED_PLAYMODE_VERIFICATION_PASS");
            }
        }
        finally
        {
            Destroy(gameObject);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
