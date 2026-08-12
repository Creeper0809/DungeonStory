#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Diagnostics;

public static class WorkAmountDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Work/Run Work Amount Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Work amount scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("save V18 carries work orders", VerifySaveV18CarriesWorkOrders, errors);
        RunScenario(
            "authored construction material projection",
            VerifyConfiguredWorkAmountFallback,
            errors);
        RunScenario(
            "all construction materials are concrete catalog items",
            VerifyAuthoredConstructionMaterialCatalog,
            errors);
        RunScenario("construction order lifecycle", VerifyConstructionOrderLifecycle, errors);
        RunScenario(
            "construction site quantity-aware worker capacity",
            VerifyConstructionSiteParallelWorkerCapacity,
            errors);
        RunScenario(
            "purchased facility kit delivery",
            VerifyPurchasedFacilityKitDelivery,
            errors);
        RunScenario("construction cancellation refunds materials", VerifyConstructionCancellationRefund, errors);
        RunScenario("orphan construction auto-recovers materials", VerifyOrphanConstructionRecovery, errors);
        RunScenario(
            "work-order preflight preserves live state",
            VerifyInvalidRestorePreflightPreservesLiveState,
            errors);
        RunScenario(
            "work-order registry publishes detached construction site",
            VerifyRegistryPublishesDetachedConstructionSite,
            errors);
        RunScenario(
            "work-order late participant failure restores live construction site",
            VerifyLateParticipantFailureRestoresLiveConstructionSite,
            errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Work amount scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifySaveV18CarriesWorkOrders()
    {
        DungeonGameSaveData save = new DungeonGameSaveData();
        DungeonSaveSectionPayload.Write(
            save,
            WorkOrdersSaveSection.Id,
            DungeonWorkOrderSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState,
            new DungeonWorkOrderSaveData());
        DungeonWorkOrderSaveData workOrders =
            DungeonSaveSectionPayload.ReadOrNew<DungeonWorkOrderSaveData>(
                save,
                WorkOrdersSaveSection.Id);
        return save.version == DungeonGameSaveData.CurrentVersion
            && workOrders.version == DungeonWorkOrderSaveData.CurrentVersion;
    }

    private static bool VerifyConfiguredWorkAmountFallback()
    {
        BuildingSO configured = CreateTestBuilding(91001, "작업량 테스트 시설", 2, 1, 12f, 4);
        BuildingSO fallback = CreateTestBuilding(91002, "기본 작업량 테스트 시설", 3, 1, 0f, 0, addWorkAbility: false);
        try
        {
            bool configuredValid = Mathf.Approximately(
                    configured.GetRequiredWork(BuiltInWorkTypeIds.Construct),
                    12f)
                && Mathf.Approximately(configured.GetRequiredWork(BuiltInWorkTypeIds.Research), 6f)
                && configured.GetConstructionMaterials().Count == 1
                && configured.GetConstructionMaterials()[0].ItemId == "material:lumber"
                && configured.GetConstructionMaterials()[0].Amount == 4;

            bool fallbackValid = fallback.GetRequiredWork(BuiltInWorkTypeIds.Construct) > 0f
                && fallback.GetRequiredWork(BuiltInWorkTypeIds.Repair) > 0f
                && fallback.GetConstructionMaterials().Count == 0;
            return configuredValid && fallbackValid;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(configured);
            UnityEngine.Object.DestroyImmediate(fallback);
        }
    }

    private static bool VerifyAuthoredConstructionMaterialCatalog()
    {
        GameContentCatalogSO root = AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>(
            "Assets/Resources/SO/GameContentCatalog.asset");
        ItemDefinitionCatalogSO itemDefinitions =
            root?.GetItemDefinitions<ItemDefinitionCatalogSO>();
        if (itemDefinitions == null)
        {
            return false;
        }

        ResourceItemDefinitionCatalog catalog =
            new ResourceItemDefinitionCatalog(itemDefinitions.Definitions);
        GameDomainContentCatalogSO domainCatalog = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .SingleOrDefault();
        BuildingSO[] authoredBuildings = domainCatalog?.GetAll<BuildingSO>()
            .Where(building => building != null)
            .Distinct()
            .ToArray();
        if (authoredBuildings == null || authoredBuildings.Length == 0)
        {
            return false;
        }

        foreach (BuildingSO building in authoredBuildings)
        {
            BuildingWorkAmountAbility ability =
                building.GetAbility<BuildingWorkAmountAbility>();
            if (ability == null || ability.ConstructionMaterials.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Cataloged building '{building.id}' at "
                    + $"'{AssetDatabase.GetAssetPath(building)}' has no concrete construction materials.");
            }

            ability.ValidateConstructionMaterialsOrThrow(itemId =>
                catalog.TryGet((ItemDefinitionId)itemId, out _));
        }

        if (typeof(BuildingWorkAmountAbility).GetField(
                "constructionMaterialCategory") != null
            || typeof(BuildingWorkAmountAbility).GetField(
                "constructionMaterialAmount") != null
            || typeof(BuildingWorkAmountAbility).GetField(
                "materialUnitsPerConstructionCost") != null)
        {
            return false;
        }

        return RejectsConstructionMaterials(new[]
            {
                new ItemAmountDefinition("material:lumber", 1),
                new ItemAmountDefinition("material:lumber", 2)
            })
            && RejectsConstructionMaterials(Array.Empty<ItemAmountDefinition>())
            && RejectsConstructionMaterials(new[]
            {
                new ItemAmountDefinition("stock-item:general", 1)
            })
            && RejectsConstructionMaterials(new[]
            {
                new ItemAmountDefinition(string.Empty, 1)
            });
    }

    private static bool RejectsConstructionMaterials(
        IEnumerable<ItemAmountDefinition> materials)
    {
        try
        {
            BuildingWorkAmountAbility ability = new BuildingWorkAmountAbility();
            ability.SetConstructionMaterials(materials);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool VerifyConstructionOrderLifecycle()
    {
        BuildingSO building = CreateTestBuilding(91003, "공사 주문 테스트 시설", 2, 1, 5f, 2);
        GameObject siteObject = new GameObject("WorkAmountConstructionSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-amount-construction");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime itemRuntime = new FakeWorldItemStackRuntime();
        TrackingWorkforceReplanService workforceReplan = new TrackingWorkforceReplanService();
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            itemRuntime,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(workforceReplan),
            CreateStateStore());
        bool placed = false;
        bool removed = false;
        try
        {
            site.Initialization(building, new Vector2Int(3, 0));
            bool created = runtime.TryCreateConstructionOrder(
                site,
                building,
                site.centerPos,
                out string orderId,
                out string failureReason);
            if (!created)
            {
                Debug.LogError($"Could not create construction order: {failureReason}");
                return false;
            }

            site.ConfigureSite(
                orderId,
                () =>
                {
                    placed = true;
                    return true;
                },
                () => removed = true);

            bool waiting = runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order)
                && order.Status == WorkOrderStatus.WaitingForMaterials
                && itemRuntime.RequestedItems.TryGetValue("material:lumber", out int requested)
                && requested == 2
                && itemRuntime.PrioritizedStackIds.Count == 1
                && workforceReplan.HaulReplans == 1;
            if (!waiting)
            {
                return false;
            }

            if (runtime.RefreshMaterialsReady(site))
            {
                return false;
            }

            itemRuntime.AddFacilityItemBuffer(
                order.MaterialDestinationId,
                "material:lumber",
                2);
            bool ready = runtime.RefreshMaterialsReady(site)
                && runtime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out order)
                && order.Status == WorkOrderStatus.Ready
                && order.DeliveredItemMaterials.TryGetValue("material:lumber", out int delivered)
                && delivered == 2;
            if (!ready)
            {
                return false;
            }

            bool firstWork = runtime.ApplyWork(
                    null,
                    site,
                    BuiltInWorkTypeIds.Construct,
                    2f,
                    out bool completed,
                    out bool appliedEffects,
                    out _)
                && !completed
                && !appliedEffects
                && runtime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out order)
                && Mathf.Approximately(order.CompletedWork, 2f)
                && order.Status == WorkOrderStatus.InProgress;
            if (!firstWork)
            {
                return false;
            }

            bool finalWork = runtime.ApplyWork(
                    null,
                    site,
                    BuiltInWorkTypeIds.Construct,
                    10f,
                    out completed,
                    out appliedEffects,
                    out _)
                && completed
                && appliedEffects
                && placed
                && removed
                && !runtime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out _)
                && runtime.Capture().orders.Count == 0;
            return finalWork;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(siteObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyConstructionCancellationRefund()
    {
        BuildingSO building = CreateTestBuilding(
            91004,
            "Construction cancellation refund",
            1,
            1,
            5f,
            2);
        GameObject siteObject = new GameObject("WorkAmountCancellationSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-amount-cancellation");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime itemRuntime = new FakeWorldItemStackRuntime();
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            itemRuntime,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore());
        try
        {
            site.Initialization(building, new Vector2Int(5, 0));
            if (!runtime.TryCreateConstructionOrder(
                    site,
                    building,
                    site.centerPos,
                    out string orderId,
                    out _))
            {
                return false;
            }

            return runtime.CancelOrder(orderId, refundDeliveredMaterials: true)
                && itemRuntime.ReleasedQuantity == 2
                && !runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out _);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(siteObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyPurchasedFacilityKitDelivery()
    {
        BuildingSO building = CreateTestBuilding(
            91006,
            "설치 키트 테스트 시설",
            1,
            1,
            5f,
            4);
        GameObject siteObject =
            new GameObject("WorkAmountInstallationKitSite");
        ConstructionSite site =
            siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-amount-installation-kit");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime itemRuntime =
            new FakeWorldItemStackRuntime();
        string kitItemId =
            FacilityInstallationKitItemIds.ForBuilding(building);
        itemRuntime.AddAvailableItem(kitItemId, 1);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            itemRuntime,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore());
        try
        {
            site.Initialization(building, new Vector2Int(4, 0));
            if (!runtime.TryCreateConstructionOrder(
                    site,
                    building,
                    site.centerPos,
                    out _,
                    out _)
                || !runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order)
                || !order.ItemMaterialRequirements.TryGetValue(
                    kitItemId,
                    out int required)
                || required != 1
                || !itemRuntime.RequestedItems.TryGetValue(
                    kitItemId,
                    out int requested)
                || requested != 1)
            {
                return false;
            }

            itemRuntime.AddFacilityItemBuffer(
                order.MaterialDestinationId,
                kitItemId,
                1);
            bool ready = runtime.RefreshMaterialsReady(site)
                && runtime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out order)
                && order.Status == WorkOrderStatus.Ready
                && order.DeliveredItemMaterials.TryGetValue(
                    kitItemId,
                    out int delivered)
                && delivered == 1;
            DungeonWorkOrderSaveData save = runtime.Capture();
            return ready
                && save.version == DungeonWorkOrderSaveData.CurrentVersion
                && save.orders.Count == 1
                && save.orders[0].itemMaterials.Count == 1
                && string.Equals(
                    save.orders[0].itemMaterials[0].itemId,
                    kitItemId,
                    StringComparison.Ordinal)
                && save.orders[0].itemMaterials[0].delivered == 1;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(siteObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyOrphanConstructionRecovery()
    {
        BuildingSO building = CreateTestBuilding(
            91005,
            "Orphan construction recovery",
            1,
            1,
            5f,
            2);
        GameObject siteObject = new GameObject("WorkAmountOrphanSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-amount-orphan");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime itemRuntime = new FakeWorldItemStackRuntime();
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            itemRuntime,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore());
        try
        {
            site.Initialization(building, new Vector2Int(6, 0));
            if (!runtime.TryCreateConstructionOrder(
                    site,
                    building,
                    site.centerPos,
                    out _,
                    out _))
            {
                return false;
            }

            UnityEngine.Object.DestroyImmediate(siteObject);
            runtime.Tick();
            return runtime.Capture().orders.Count == 0
                && itemRuntime.ReleasedQuantity == 2;
        }
        finally
        {
            if (siteObject != null)
            {
                UnityEngine.Object.DestroyImmediate(siteObject);
            }
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyInvalidRestorePreflightPreservesLiveState()
    {
        BuildingSO building = CreateTestBuilding(
            91007,
            "Work-order invalid restore",
            1,
            1,
            5f,
            1);
        GameObject siteObject = new GameObject(
            "WorkAmountInvalidRestoreLiveSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        site.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-order-preflight");
        CharacterAiEditorTestDependencies.Inject(site);
        FakeWorldItemStackRuntime items = new FakeWorldItemStackRuntime();
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            items,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            CreateStateStore());
        try
        {
            site.Initialization(building, new Vector2Int(2, 0));
            if (!runtime.TryCreateConstructionOrder(
                    site,
                    building,
                    site.centerPos,
                    out string liveOrderId,
                    out _))
            {
                return false;
            }

            DungeonWorkOrderSaveData invalid = runtime.Capture();
            invalid.nextOrderSequence = 0;
            invalid.orders.Add(invalid.orders[0]);
            WorkOrdersSaveSection section = new WorkOrdersSaveSection(runtime);
            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            bool rejected = false;
            try
            {
                section.StageRestore(
                    JsonUtility.ToJson(invalid),
                    DungeonWorkOrderSaveData.CurrentVersion,
                    report);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            DungeonWorkOrderSaveData after = runtime.Capture();
            return rejected
                && site != null
                && after.orders.Count == 1
                && string.Equals(
                    after.orders[0].workOrderId,
                    liveOrderId,
                    StringComparison.Ordinal);
        }
        finally
        {
            if (siteObject != null)
            {
                UnityEngine.Object.DestroyImmediate(siteObject);
            }

            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static bool VerifyRegistryPublishesDetachedConstructionSite()
    {
        BuildingSO building = CreateTestBuilding(
            91008,
            "Detached construction restore",
            1,
            1,
            5f,
            1);
        GameObject liveSiteObject = new GameObject(
            "WorkAmountRegistryLiveSite");
        ConstructionSite liveSite =
            liveSiteObject.AddComponent<ConstructionSite>();
        liveSite.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-order-registry-live");
        CharacterAiEditorTestDependencies.Inject(liveSite);
        FakeWorldItemStackRuntime items = new FakeWorldItemStackRuntime();
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        RestoreWorldCandidateIndex candidateIndex =
            new RestoreWorldCandidateIndex();
        Grid liveGrid = new Grid(12, 4);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            items,
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            new WorkOrderAggregateStateStore(rootStore, candidateIndex));
        CandidateFacilitySection facilitySection =
            new CandidateFacilitySection(candidateIndex, new Grid(12, 4));
        PassiveSaveSection physicalItems = new PassiveSaveSection(
            PhysicalItemsSaveSection.Id,
            DungeonSaveRestorePhase.Items);
        WorkOrdersSaveSection workOrders = new WorkOrdersSaveSection(runtime);
        WorkOrderRestoreLifecycleProbe successProbe =
            new WorkOrderRestoreLifecycleProbe(
                failAfterPublish: false,
                verifyPublishedState: () =>
                {
                    ConstructionSite incoming = Resources
                        .FindObjectsOfTypeAll<ConstructionSite>()
                        .FirstOrDefault(candidate => candidate != null
                            && candidate.WorkOrderId == "work:000002");
                    return liveSite != null
                        && liveSite.gameObject.activeSelf
                        && incoming != null
                        && incoming.IsDetachedRestoreCandidate
                        && !incoming.gameObject.activeSelf;
                });
        ConstructionSite restoredSite = null;
        try
        {
            liveSite.SetGrid(liveGrid);
            liveSite.Initialization(building, new Vector2Int(1, 0));
            if (!liveGrid.RegisterOccupant(
                    liveSite,
                    GridLayer.Construction,
                    liveSite.buildPoses,
                    connectPositions: false))
            {
                return false;
            }
            if (!runtime.TryCreateConstructionOrder(
                    liveSite,
                    building,
                    liveSite.centerPos,
                    out _,
                    out _))
            {
                return false;
            }

            DungeonSaveSectionRegistry registry =
                new DungeonSaveSectionRegistry(
                    new IDungeonSaveSection[]
                    {
                        facilitySection,
                        physicalItems,
                        workOrders
                    },
                    rootStore,
                    new IDungeonRestoreTransactionParticipant[]
                    {
                        facilitySection,
                        runtime,
                        successProbe
                    });
            List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
            DungeonWorkOrderSaveData incoming = CreateRestoredOrder(
                building,
                "work:000002",
                3,
                new Vector2Int(4, 0));
            string expectedJson = JsonUtility.ToJson(incoming);
            DungeonSaveSectionEnvelope workEnvelope = envelopes.Single(
                envelope => string.Equals(
                    envelope.sectionId,
                    WorkOrdersSaveSection.Id,
                    StringComparison.Ordinal));
            workEnvelope.payloadJson = JsonUtility.ToJson(incoming);

            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            bool restored = registry.RestoreAll(envelopes, report);
            restoredSite = Resources.FindObjectsOfTypeAll<ConstructionSite>()
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.WorkOrderId,
                        "work:000002",
                        StringComparison.Ordinal));
            DungeonWorkOrderSaveData captured = runtime.Capture();
            string capturedJson = JsonUtility.ToJson(captured);
            bool passed = restored
                && report.Success
                && liveSite == null
                && restoredSite != null
                && restoredSite.gameObject.activeSelf
                && !restoredSite.IsDetachedRestoreCandidate
                && runtime.TryGetOrderFor(
                    restoredSite,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order)
                && order.Status == WorkOrderStatus.Ready
                && captured.nextOrderSequence == 3
                && captured.orders.Count == 1
                && captured.orders[0].workOrderId == "work:000002"
                && string.Equals(
                    expectedJson,
                    capturedJson,
                    StringComparison.Ordinal)
                && successProbe.PublishCount == 1
                && successProbe.PublishedStateWasReversible
                && successProbe.RollbackCount == 0
                && successProbe.CompleteCount == 1
                && rootStore.PublishedRestoreRevision == 1
                && CountDetachedConstructionSites() == 0
                && report.Warnings.Count == 0;
            if (!passed)
            {
                Debug.LogWarning(
                    "Work-order publish diagnostic "
                    + $"restored={restored} success={report.Success} "
                    + $"errors={string.Join(" | ", report.Errors)} "
                    + $"warnings={string.Join(" | ", report.Warnings)} "
                    + $"liveDestroyed={liveSite == null} "
                    + $"restoredSite={restoredSite != null} "
                    + $"active={restoredSite != null && restoredSite.gameObject.activeSelf} "
                    + $"detached={restoredSite != null && restoredSite.IsDetachedRestoreCandidate} "
                    + $"probePublish={successProbe.PublishCount} "
                    + $"probeReversible={successProbe.PublishedStateWasReversible} "
                    + $"probeRollback={successProbe.RollbackCount} "
                    + $"probeComplete={successProbe.CompleteCount} "
                    + $"revision={rootStore.PublishedRestoreRevision} "
                    + $"expected={expectedJson} actual={capturedJson} "
                    + $"detachedCount={CountDetachedConstructionSites()}");
            }

            return passed;
        }
        finally
        {
            if (restoredSite != null)
            {
                UnityEngine.Object.DestroyImmediate(restoredSite.gameObject);
            }

            if (liveSiteObject != null)
            {
                UnityEngine.Object.DestroyImmediate(liveSiteObject);
            }

            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static DungeonWorkOrderSaveData CreateRestoredOrder(
        BuildingSO building,
        string orderId,
        int nextSequence,
        Vector2Int position)
    {
        return new DungeonWorkOrderSaveData
        {
            version = DungeonWorkOrderSaveData.CurrentVersion,
            nextOrderSequence = nextSequence,
            orders = new List<WorkOrderSaveData>
            {
                new WorkOrderSaveData
                {
                    workOrderId = orderId,
                    workTypeId = BuiltInWorkTypeIds.Construct.Value,
                    targetBuildingId = building.id,
                    gridX = position.x,
                    gridY = position.y,
                    requiredWork = 5f,
                    completedWork = 1f,
                    materialDestinationId =
                        $"{WorkOrderRuntime.ConstructionDestinationPrefix}{building.id}:{position.x}:{position.y}",
                    reservedWorkerPersistentId = string.Empty,
                    qualityRoll = new CraftQualityRollSaveData
                    {
                        attemptIndex = 0,
                        randomA = 0,
                        randomB = 0,
                        randomC = 0
                    },
                    qualityAttemptIndex = 0,
                    status = WorkOrderStatus.Ready,
                    itemMaterials = new List<WorkOrderItemMaterialSaveData>(),
                    recoveryOutputs = new List<WorkOrderItemMaterialSaveData>()
                }
            }
        };
    }

    private static bool VerifyLateParticipantFailureRestoresLiveConstructionSite()
    {
        BuildingSO building = CreateTestBuilding(
            91009,
            "Discarded construction restore",
            1,
            1,
            5f,
            1);
        GameObject liveSiteObject = new GameObject(
            "WorkAmountDiscardLiveSite");
        ConstructionSite liveSite =
            liveSiteObject.AddComponent<ConstructionSite>();
        liveSite.RestorePersistentIdentity(
            (BuildingInstanceId)"building:test:work-order-discard-live");
        CharacterAiEditorTestDependencies.Inject(liveSite);
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        RestoreWorldCandidateIndex candidateIndex =
            new RestoreWorldCandidateIndex();
        Grid liveGrid = new Grid(12, 4);
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            new FakeWorldItemStackRuntime(),
            new SingleBuildingLookup(building),
            new ScenarioObjectResolver(),
            CreateExecutionServices(new TrackingWorkforceReplanService()),
            new WorkOrderAggregateStateStore(rootStore, candidateIndex));
        CandidateFacilitySection facilitySection =
            new CandidateFacilitySection(candidateIndex, new Grid(12, 4));
        PassiveSaveSection physicalItems = new PassiveSaveSection(
            PhysicalItemsSaveSection.Id,
            DungeonSaveRestorePhase.Items);
        WorkOrdersSaveSection workOrders = new WorkOrdersSaveSection(runtime);
        WorkOrderRestoreLifecycleProbe lateProbe =
            new WorkOrderRestoreLifecycleProbe(
                failAfterPublish: true,
                verifyPublishedState: () =>
                {
                    ConstructionSite incoming = Resources
                        .FindObjectsOfTypeAll<ConstructionSite>()
                        .FirstOrDefault(candidate => candidate != null
                            && candidate.WorkOrderId == "work:000002");
                    return liveSite != null
                        && liveSite.gameObject.activeSelf
                        && incoming != null
                        && incoming.IsDetachedRestoreCandidate
                        && !incoming.gameObject.activeSelf
                        && runtime.TryGetOrderFor(
                            incoming,
                            BuiltInWorkTypeIds.Construct,
                            out WorkOrderProgressState publishedOrder)
                        && publishedOrder.WorkOrderId == "work:000002";
                });
        ConstructionSite survivingSite = null;
        try
        {
            liveSite.SetGrid(liveGrid);
            liveSite.Initialization(building, new Vector2Int(1, 0));
            if (!liveGrid.RegisterOccupant(
                    liveSite,
                    GridLayer.Construction,
                    liveSite.buildPoses,
                    connectPositions: false))
            {
                return false;
            }
            if (!runtime.TryCreateConstructionOrder(
                    liveSite,
                    building,
                    liveSite.centerPos,
                    out string liveOrderId,
                    out _))
            {
                return false;
            }

            DungeonSaveSectionRegistry registry =
                new DungeonSaveSectionRegistry(
                    new IDungeonSaveSection[]
                    {
                        facilitySection,
                        physicalItems,
                        workOrders
                    },
                    rootStore,
                    new IDungeonRestoreTransactionParticipant[]
                    {
                        facilitySection,
                        runtime,
                        lateProbe
                    });
            List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
            DungeonSaveSectionEnvelope workEnvelope = envelopes.Single(
                envelope => envelope.sectionId == WorkOrdersSaveSection.Id);
            workEnvelope.payloadJson = JsonUtility.ToJson(
                CreateRestoredOrder(
                    building,
                    "work:000002",
                    3,
                    new Vector2Int(4, 0)));

            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            string expectedLiveJson = JsonUtility.ToJson(runtime.Capture());
            int detachedBefore = CountDetachedConstructionSites();
            bool restored = registry.RestoreAll(envelopes, report);
            ConstructionSite[] allSites =
                Resources.FindObjectsOfTypeAll<ConstructionSite>();
            bool incomingLeaked = allSites.Any(candidate => candidate != null
                && candidate.WorkOrderId == "work:000002");
            survivingSite = liveSite;
            DungeonWorkOrderSaveData captured = runtime.Capture();
            bool candidateIndexClear = !candidateIndex.TryGetGrid(out _)
                && !candidateIndex.TryGetBuildings(out _)
                && !candidateIndex.TryGetExteriorZones(out _);
            bool liveOrderPreserved = liveSite != null
                && runtime.TryGetOrderFor(
                    liveSite,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState liveOrder)
                && string.Equals(
                    liveOrder.WorkOrderId,
                    liveOrderId,
                    StringComparison.Ordinal);
            bool passed = !restored
                && !report.Success
                && lateProbe.PublishCount == 1
                && lateProbe.PublishedStateWasReversible
                && lateProbe.RollbackCount == 1
                && lateProbe.CompleteCount == 0
                && !incomingLeaked
                && ReferenceEquals(survivingSite, liveSite)
                && liveOrderPreserved
                && liveSiteObject.activeSelf
                && liveGrid.GetGridCell(liveSite.centerPos)
                    ?.ContainsOccupant(
                        GridLayer.Construction,
                        liveSite) == true
                && captured.orders.Count == 1
                && captured.orders[0].workOrderId == liveOrderId
                && string.Equals(
                    expectedLiveJson,
                    JsonUtility.ToJson(captured),
                    StringComparison.Ordinal)
                && candidateIndexClear
                && !rootStore.IsRestoreStaging
                && rootStore.PublishedRestoreRevision == 0
                && CountDetachedConstructionSites() == detachedBefore;
            if (!passed)
            {
                Debug.LogWarning(
                    "Work-order late-failure diagnostic "
                    + $"restored={restored} success={report.Success} "
                    + $"errors={string.Join(" | ", report.Errors)} "
                    + $"publishes={lateProbe.PublishCount} incomingLeaked={incomingLeaked} "
                    + $"reversible={lateProbe.PublishedStateWasReversible} "
                    + $"rollbacks={lateProbe.RollbackCount} "
                    + $"completes={lateProbe.CompleteCount} "
                    + $"sameLive={ReferenceEquals(survivingSite, liveSite)} "
                    + $"liveOrder={liveOrderPreserved} "
                    + $"liveActive={liveSiteObject.activeSelf} "
                    + $"liveRegistered={liveGrid.GetGridCell(liveSite.centerPos)?.ContainsOccupant(GridLayer.Construction, liveSite) == true} "
                    + $"expected={expectedLiveJson} actual={JsonUtility.ToJson(captured)} "
                    + $"candidateClear={candidateIndexClear} "
                    + $"staging={rootStore.IsRestoreStaging} "
                    + $"revision={rootStore.PublishedRestoreRevision} "
                    + $"detachedBefore={detachedBefore} "
                    + $"detachedAfter={CountDetachedConstructionSites()}");
            }

            return passed;
        }
        finally
        {
            if (survivingSite != null)
            {
                UnityEngine.Object.DestroyImmediate(survivingSite.gameObject);
            }

            if (liveSiteObject != null)
            {
                UnityEngine.Object.DestroyImmediate(liveSiteObject);
            }

            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static WorkOrderAggregateStateStore CreateStateStore()
    {
        return new WorkOrderAggregateStateStore(
            new DungeonRuntimeAggregateRootStore(),
            new RestoreWorldCandidateIndex());
    }

    private static WorkOrderExecutionServices CreateExecutionServices(
        IWorkforceReplanService workforce)
    {
        return new WorkOrderExecutionServices(
            workforce,
            new FixedGameClock(),
            new FixedUiClock(),
            DisabledDungeonDebugRuleQuery.Instance);
    }

    private sealed class FixedGameClock :
        DungeonStory.Foundation.IGameClock
    {
        public float DeltaTime => 0.02f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FixedUiClock :
        DungeonStory.Foundation.IUiClock
    {
        public float DeltaTime => 0.02f;
        public float Time => 0f;
    }

    private static int CountDetachedConstructionSites()
    {
        return Resources.FindObjectsOfTypeAll<ConstructionSite>()
            .Count(site => site != null && site.IsDetachedRestoreCandidate);
    }

    private static bool VerifyConstructionSiteParallelWorkerCapacity()
    {
        BuildingSO building = CreateTestBuilding(
            991_204,
            "Industrial capacity audit",
            2,
            2,
            constructionWork: 100f,
            materialAmount: 1);
        building.GetAbility<BuildingWorkAmountAbility>()
            .SetConstructionProjectScale(ProjectScale.IndustrialFacility);
        GameObject siteObject = new GameObject("WorkAmountParallelConstructionSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        CharacterAiEditorTestDependencies.Inject(site);
        TestBuildingCharacterPort[] workers = Enumerable.Range(0, 5)
            .Select(index => new TestBuildingCharacterPort($"character:test:builder:{index}"))
            .ToArray();
        TestBuildingCharacterPort sameCharacterAdapter =
            new TestBuildingCharacterPort(workers[0].BuildingCharacterId.Value);
        try
        {
            site.Initialization(building, Vector2Int.zero);
            if (site.MaximumWorkers != 4)
                return false;

            if (!site.TryReserveWorker(
                    workers[0],
                    out FacilityAssignmentStatus firstIdentity,
                    seconds: 30f)
                || !firstIdentity.IsAllowed
                || !site.TryReserveWorker(
                    sameCharacterAdapter,
                    out FacilityAssignmentStatus adapterIdentity,
                    seconds: 30f)
                || !adapterIdentity.IsAllowed
                || site.OccupiedWorkerSlotCount != 1)
            {
                return false;
            }

            site.ReleaseWorkerReservation(sameCharacterAdapter);
            if (site.OccupiedWorkerSlotCount != 0)
            {
                return false;
            }

            for (int index = 0; index < 4; index++)
            {
                if (!site.TryReserveWorker(
                        workers[index],
                        out FacilityAssignmentStatus status,
                        seconds: 30f)
                    || !status.IsAllowed)
                {
                    return false;
                }
            }
            if (site.OccupiedWorkerSlotCount != 4
                || site.TryReserveWorker(
                    workers[4],
                    out FacilityAssignmentStatus overflow,
                    seconds: 30f)
                || overflow.IsAllowed)
            {
                return false;
            }

            site.ReleaseWorkerReservation(workers[1]);
            return site.OccupiedWorkerSlotCount == 3
                && site.TryReserveWorker(
                    workers[4],
                    out FacilityAssignmentStatus replacement,
                    seconds: 30f)
                && replacement.IsAllowed
                && site.OccupiedWorkerSlotCount == 4;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(siteObject);
            UnityEngine.Object.DestroyImmediate(building);
        }
    }

    private static BuildingSO CreateTestBuilding(
        int id,
        string objectName,
        int width,
        int height,
        float constructionWork,
        int materialAmount,
        bool addWorkAbility = true)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = id;
        building.objectName = objectName;
        building.width = width;
        building.height = height;
        building.layer = GridLayer.Building;
        building.category = BuildingCategory.Shop;
        building.unlocked = true;
        if (addWorkAbility)
        {
            BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
            {
                constructionWorkRequired = Mathf.Max(0.1f, constructionWork),
                repairWorkRequired = 3f,
                cleanWorkRequired = 2f,
                researchWorkRequired = 6f
            };
            workAmount.SetConstructionMaterials(
                materialAmount > 0
                    ? new[]
                    {
                        new ItemAmountDefinition(
                            "material:lumber",
                            materialAmount)
                    }
                    : Array.Empty<ItemAmountDefinition>());
            building.AbilityModules.Add(workAmount);
        }

        return building;
    }

    private sealed class TestBuildingCharacterPort : IBuildingCharacterPort
    {
        public TestBuildingCharacterPort(string characterId)
        {
            BuildingCharacterId = (CharacterId)characterId;
        }

        public CharacterId BuildingCharacterId { get; }
        public string BuildingDisplayName => BuildingCharacterId.Value;
        public bool IsBuildingInteractionAvailable => true;
    }

    private sealed class CandidateFacilitySection :
        IDungeonSaveSection,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection,
        IDungeonRestoreTransactionParticipant
    {
        private readonly IRestoreWorldCandidatePublisher publisher;
        private readonly Grid candidateGrid;
        private bool transactionActive;
        private bool candidateReady;

        public CandidateFacilitySection(
            IRestoreWorldCandidatePublisher publisher,
            Grid candidateGrid)
        {
            this.publisher = publisher;
            this.candidateGrid = candidateGrid;
        }

        public string SectionId => ModularFacilityWorldSaveSection.Id;
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.World;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string ParticipantId => "100.world.facilities.test";

        public string Capture() => "{}";

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (transactionActive || candidateReady)
            {
                throw new InvalidOperationException(
                    "Facility test candidate staging is already active.");
            }

            publisher.SetFacilityCandidate(
                candidateGrid,
                Array.Empty<BuildableObject>());
            candidateReady = true;
            return new DungeonCandidateSaveRestoreStage<
                CandidateFacilityRestoreCandidate>(
                SectionId,
                new CandidateFacilityRestoreCandidate(this),
                StagePreparedCandidate);
        }

        private void StagePreparedCandidate(
            CandidateFacilityRestoreCandidate candidate)
        {
            if (!transactionActive || !candidateReady)
            {
                throw new InvalidOperationException(
                    "Facility test candidate transaction is inactive or unprepared.");
            }

            candidate.Take(this);
        }

        private void DiscardPreparedCandidate()
        {
            publisher.ClearFacilityCandidate();
            candidateReady = false;
        }

        public void BeginRestoreCandidate()
        {
            transactionActive = true;
        }

        public void PublishRestoreCandidate()
        {
            if (!transactionActive || !candidateReady)
            {
                throw new InvalidOperationException(
                    "Facility test candidate is not ready.");
            }

            publisher.ClearFacilityCandidate();
            transactionActive = false;
            candidateReady = false;
        }

        public void DiscardRestoreCandidate()
        {
            publisher.ClearFacilityCandidate();
            transactionActive = false;
            candidateReady = false;
        }

        private sealed class CandidateFacilityRestoreCandidate :
            IDungeonDiscardableRestoreCandidate
        {
            private CandidateFacilitySection owner;

            internal CandidateFacilityRestoreCandidate(
                CandidateFacilitySection owner)
            {
                this.owner = owner
                    ?? throw new ArgumentNullException(nameof(owner));
            }

            internal void Take(CandidateFacilitySection expectedOwner)
            {
                if (!ReferenceEquals(owner, expectedOwner))
                {
                    throw new InvalidOperationException(
                        "Facility test candidate has the wrong owner or was already consumed.");
                }

                owner = null;
            }

            public void Discard()
            {
                CandidateFacilitySection current = owner;
                owner = null;
                current?.DiscardPreparedCandidate();
            }
        }
    }

    private sealed class PassiveSaveSection :
        IDungeonSaveSection,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string sectionId;
        private readonly DungeonSaveRestorePhase restorePhase;

        public PassiveSaveSection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase)
        {
            this.sectionId = sectionId;
            this.restorePhase = restorePhase;
        }

        public string SectionId => sectionId;
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase => restorePhase;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            return new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
        }
    }

    private sealed class WorkOrderRestoreLifecycleProbe :
        IDungeonRestoreTransactionParticipant
    {
        private readonly bool failAfterPublish;
        private readonly Func<bool> verifyPublishedState;
        private bool transactionActive;

        internal WorkOrderRestoreLifecycleProbe(
            bool failAfterPublish,
            Func<bool> verifyPublishedState)
        {
            this.failAfterPublish = failAfterPublish;
            this.verifyPublishedState = verifyPublishedState
                ?? throw new ArgumentNullException(nameof(verifyPublishedState));
        }

        public string ParticipantId => "999.test.work-orders.lifecycle-probe";
        public int PublishCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int CompleteCount { get; private set; }
        public bool PublishedStateWasReversible { get; private set; }

        public void BeginRestoreCandidate()
        {
            transactionActive = true;
        }

        public void PublishRestoreCandidate()
        {
            if (!transactionActive)
            {
                throw new InvalidOperationException(
                    "Injected participant was not started.");
            }

            PublishCount++;
            PublishedStateWasReversible = verifyPublishedState();
            if (failAfterPublish)
            {
                throw new InvalidOperationException(
                    "Injected failure after work-order publication.");
            }
        }

        public void RollbackPublishedRestoreCandidate()
        {
            if (!transactionActive)
            {
                throw new InvalidOperationException(
                    "Injected participant has no publication to roll back.");
            }

            RollbackCount++;
            transactionActive = false;
        }

        public void CompleteRestoreCandidate()
        {
            if (!transactionActive)
            {
                throw new InvalidOperationException(
                    "Injected participant has no publication to complete.");
            }

            CompleteCount++;
            transactionActive = false;
        }

        public void DiscardRestoreCandidate()
        {
            transactionActive = false;
        }
    }

    private sealed class SingleBuildingLookup : IBuildingDefinitionLookup
    {
        private readonly BuildingSO building;

        public SingleBuildingLookup(BuildingSO building)
        {
            this.building = building;
        }

        public BuildingSO GetBuilding(int id)
        {
            return building != null && building.id == id ? building : null;
        }
    }

    private sealed class ScenarioObjectResolver : IObjectResolver
    {
        public object ApplicationOrigin => null;
        public DiagnosticsCollector Diagnostics { get; set; }

        public object Resolve(Type type, object key = null)
        {
            throw new InvalidOperationException(
                $"Work-order scenario cannot resolve {type?.Name ?? "null"}.");
        }

        public bool TryResolve(
            Type type,
            out object resolved,
            object key = null)
        {
            resolved = null;
            return false;
        }

        public object Resolve(Registration registration)
        {
            throw new InvalidOperationException(
                "Work-order scenario does not resolve registrations.");
        }

        public IScopedObjectResolver CreateScope(
            Action<IContainerBuilder> installation = null)
        {
            throw new InvalidOperationException(
                "Work-order scenario does not create scopes.");
        }

        public void Inject(object instance)
        {
            if (instance is BuildableObject building)
            {
                CharacterAiEditorTestDependencies.Inject(building);
            }
        }

        public bool TryGetRegistration(
            Type type,
            out Registration registration,
            object key = null)
        {
            registration = null;
            return false;
        }

        public void Dispose()
        {
        }
    }

    private sealed class NoGridProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
    }

    private sealed class FakeWorldItemStackRuntime : IWorldItemStackRuntime
    {
        private readonly Dictionary<string, Dictionary<StockCategory, int>> buffers =
            new Dictionary<string, Dictionary<StockCategory, int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, int>> itemBuffers =
            new Dictionary<string, Dictionary<string, int>>(
                StringComparer.Ordinal);
        private readonly List<WorldItemStackSnapshot> stacks =
            new List<WorldItemStackSnapshot>();

        public readonly Dictionary<StockCategory, int> Requested = new Dictionary<StockCategory, int>();
        public readonly Dictionary<string, int> RequestedItems =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public readonly HashSet<string> PrioritizedStackIds =
            new HashSet<string>(StringComparer.Ordinal);
        public int ReleasedQuantity { get; private set; }

        public IDungeonItemCatalogProvider CatalogProvider => null;
        public IItemHaulingSettingsProvider HaulingSettingsProvider => null;
        public bool StoredItemMarkersVisible => false;
        public int ItemStackVersion => 0;
        public int HaulJobVersion => 0;

        public DungeonPhysicalItemSaveData Capture() => new DungeonPhysicalItemSaveData();
        public void Restore(DungeonPhysicalItemSaveData snapshot) { }
        public void SetStoredItemMarkersVisible(bool visible) { }
        public bool SpawnItemAtDropoff(string itemId, int amount, string sourceLabel, out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnStockAtDropoff(StockCategory category, int amount, string sourceLabel, out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnStockAtDropoff(
            StockCategory category,
            int amount,
            string sourceLabel,
            WorldItemStackState state,
            string destinationId,
            out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnStockInWarehouse(
            IWarehouseFacility warehouse,
            StockCategory category,
            int amount,
            out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnItemAt(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnWasteAt(
            string itemId,
            int amount,
            Vector2Int position,
            WasteOriginKind wasteOrigin,
            float contamination,
            out int spawned)
        {
            spawned = Mathf.Max(0, amount);
            if (spawned <= 0)
            {
                return false;
            }

            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"fake-waste:{stacks.Count + 1}",
                ItemId = itemId ?? string.Empty,
                Quantity = spawned,
                State = WorldItemStackState.Loose,
                Position = position,
                WasteOrigin = wasteOrigin,
                Contamination = Mathf.Clamp(contamination, 0f, 100f)
            });
            return true;
        }

        public bool SpawnUniqueItemAt(
            string itemId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out string stackId)
        {
            stackId = string.Empty;
            return false;
        }

        public bool SpawnExistingUniqueItemAt(
            string itemId,
            ItemInstanceId itemInstanceId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out string stackId)
        {
            stackId = string.Empty;
            return false;
        }

        public bool TryAbsorbUniqueItemStack(
            string stackId,
            ItemInstanceId expectedInstanceId) => false;

        public bool SpawnUniqueItemAt(
            string itemId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            Vector2Int destinationPosition,
            out string stackId)
        {
            stackId = string.Empty;
            return false;
        }

        public bool SpawnHumanoidCorpse(
            CharacterActor source,
            Vector2Int position,
            string deathReason,
            out string stackId)
        {
            stackId = string.Empty;
            return false;
        }

        public bool TryRequestFacilityDelivery(
            StockCategory category,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = 0;
            failureReason =
                $"The work-order fixture requires an explicit item definition ID; category '{category}' is not a deliverable item.";
            return false;
        }

        public bool TryRequestItemDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = Mathf.Max(0, amount);
            failureReason = string.Empty;
            if (requested <= 0)
            {
                return false;
            }

            RequestedItems[itemId ?? string.Empty] =
                RequestedItems.TryGetValue(
                    itemId ?? string.Empty,
                    out int current)
                    ? current + requested
                    : requested;
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"fake-request:{stacks.Count + 1}",
                ItemId = itemId ?? string.Empty,
                StockCategory = StockCategory.Blueprint,
                Quantity = requested,
                State = WorldItemStackState.Loose,
                Position = Vector2Int.zero,
                DestinationId = destinationId ?? string.Empty,
                HasDestinationPosition = true,
                DestinationPosition = destinationPosition
            });
            return true;
        }

        public bool TryRequestStackDelivery(
            string stackId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = 0;
            failureReason = "fake stack not found";
            WorldItemStackSnapshot stack = stacks.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.StackId,
                    stackId,
                    StringComparison.Ordinal));
            if (stack == null)
            {
                return false;
            }

            requested = Mathf.Min(Mathf.Max(0, amount), stack.Quantity);
            if (requested <= 0)
            {
                return false;
            }

            stack.DestinationId = destinationId ?? string.Empty;
            stack.HasDestinationPosition = true;
            stack.DestinationPosition = destinationPosition;
            failureReason = string.Empty;
            return true;
        }

        public bool TryGetPileAt(Vector2Int position, out WorldItemPileSnapshot pile)
        {
            pile = null;
            return false;
        }

        public bool TryGetPileTargetAt(
            Vector2Int position,
            out ItemPileInfoTarget target,
            out UnityEngine.Object markerObject)
        {
            target = null;
            markerObject = null;
            return false;
        }

        public IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(Vector2Int position, bool includeStored = false) =>
            Array.Empty<WorldItemStackSnapshot>();

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => stacks;
        public bool TryFindNearestAvailableStock(
            Vector2Int origin,
            StockCategory category,
            bool preferStored,
            out WorldItemStackSnapshot stack)
        {
            stack = null;
            return false;
        }

        public void CopyAvailableStockCandidates(
            StockCategory category,
            List<WorldItemStockCandidate> destination)
        {
            destination?.Clear();
        }

        public bool TryFindBestAvailableStack(
            Vector2Int origin,
            Func<string, int> rankSelector,
            out WorldItemStackSnapshot stack)
        {
            stack = null;
            return false;
        }

        public bool HasAvailableHaulJob(CharacterActor actor) => false;
        public bool TryReserveBestHaulPlan(CharacterActor actor, out WorldItemHaulPlan plan, out string failureReason)
        {
            plan = null;
            failureReason = "no fake haul";
            return false;
        }

        public bool TryReserveStoredItemForDirectPickup(
            CharacterActor actor,
            string itemId,
            int quantity,
            out WorldItemReservedStackQuantity reservation,
            out Vector2Int pickupStandPosition,
            out string failureReason)
        {
            reservation = default;
            pickupStandPosition = default;
            failureReason = "no fake direct pickup";
            return false;
        }

        public bool TryReserveBestHaulJob(CharacterActor actor, out WorldItemHaulJob job, out string failureReason)
        {
            job = default;
            failureReason = "no fake haul";
            return false;
        }

        public bool TryPickupReservedStackQuantity(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            WorldItemReservedStackQuantity reservation,
            out int pickedUp,
            out string failureReason)
        {
            pickedUp = 0;
            failureReason = "no fake pickup";
            return false;
        }

        public bool TryPickupReservedStack(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            WorldItemHaulJob job,
            out string failureReason)
        {
            failureReason = "no fake pickup";
            return false;
        }

        public bool TryDepositCarriedItems(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            IWarehouseFacility warehouse,
            out string failureReason)
        {
            failureReason = "no fake deposit";
            return false;
        }

        public bool TryDepositCarriedItemsToFacility(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            Vector2Int destinationPosition,
            string destinationId,
            out string failureReason)
        {
            failureReason = "no fake facility deposit";
            return false;
        }

        public bool TryConsumeFacilityBuffer(
            string destinationId,
            IReadOnlyDictionary<StockCategory, int> costs,
            out string failureReason)
        {
            failureReason = string.Empty;
            string normalizedDestination = destinationId ?? string.Empty;
            if (!buffers.TryGetValue(normalizedDestination, out Dictionary<StockCategory, int> byCategory))
            {
                failureReason = "buffer missing";
                return false;
            }

            foreach (KeyValuePair<StockCategory, int> pair in costs ?? new Dictionary<StockCategory, int>())
            {
                if (!byCategory.TryGetValue(pair.Key, out int available) || available < pair.Value)
                {
                    failureReason = "buffer shortage";
                    return false;
                }
            }

            foreach (KeyValuePair<StockCategory, int> pair in costs ?? new Dictionary<StockCategory, int>())
            {
                byCategory[pair.Key] -= pair.Value;
            }

            return true;
        }

        public bool TryConsumeFacilityItemBuffer(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            out string failureReason)
        {
            failureReason = string.Empty;
            string normalizedDestination = destinationId ?? string.Empty;
            if (!itemBuffers.TryGetValue(
                    normalizedDestination,
                    out Dictionary<string, int> byItem))
            {
                failureReason = "item buffer missing";
                return false;
            }

            foreach (KeyValuePair<string, int> pair
                     in costs ?? new Dictionary<string, int>())
            {
                if (!byItem.TryGetValue(pair.Key, out int available)
                    || available < pair.Value)
                {
                    failureReason = "item buffer shortage";
                    return false;
                }
            }

            foreach (KeyValuePair<string, int> pair
                     in costs ?? new Dictionary<string, int>())
            {
                byItem[pair.Key] -= pair.Value;
            }

            return true;
        }

        public bool TryStealLooseItem(
            CharacterActor actor,
            int searchRadius,
            out WorldItemStackSnapshot stolenItem,
            out string failureReason)
        {
            stolenItem = null;
            failureReason = "no fake theft";
            return false;
        }

        public void ReleaseReservation(string stackId, string persistentId) { }
        public bool TryClearReservation(string stackId) => false;
        public bool SetForbidden(string stackId, bool forbidden) => false;
        public bool PrioritizeHaul(string stackId) =>
            !string.IsNullOrWhiteSpace(stackId) && PrioritizedStackIds.Add(stackId);
        public bool TryRouteStackToDestination(
            string stackId,
            WorldItemStackState state,
            string destinationId,
            Vector2Int destinationPosition,
            out string failureReason)
        {
            failureReason = string.Empty;
            return false;
        }
        public bool DeleteStack(string stackId) => false;
        public bool TryConsumeStackQuantity(
            string stackId,
            int quantity,
            out WorldItemStackSnapshot consumed)
        {
            consumed = null;
            return false;
        }

        public bool TrySetInstanceComponent(
            string stackId,
            ItemInstanceComponentSaveData component) => false;
        public bool SetEmergencyButcheryAllowed(string stackId, bool allowed) => false;
        public int RemoveStacksByStateAndDestination(WorldItemStackState state, string destinationId) => 0;
        public int ReleaseStacksByDestination(
            string destinationId,
            Vector2Int releasePosition)
        {
            int released = stacks
                .Where(stack => string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            ReleasedQuantity += released;
            stacks.RemoveAll(stack => string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal));
            return released;
        }

        public void AddFacilityBuffer(string destinationId, StockCategory category, int amount)
        {
            string normalizedDestination = destinationId ?? string.Empty;
            if (!buffers.TryGetValue(normalizedDestination, out Dictionary<StockCategory, int> byCategory))
            {
                byCategory = new Dictionary<StockCategory, int>();
                buffers[normalizedDestination] = byCategory;
            }

            byCategory[category] = byCategory.TryGetValue(category, out int current)
                ? current + amount
                : amount;
        }

        public void AddAvailableItem(string itemId, int amount)
        {
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"fake-item:{stacks.Count + 1}",
                ItemId = itemId ?? string.Empty,
                StockCategory = StockCategory.General,
                Quantity = Mathf.Max(0, amount),
                State = WorldItemStackState.Loose,
                Position = Vector2Int.zero
            });
        }

        public void AddFacilityItemBuffer(
            string destinationId,
            string itemId,
            int amount)
        {
            string normalizedDestination = destinationId ?? string.Empty;
            if (!itemBuffers.TryGetValue(
                    normalizedDestination,
                    out Dictionary<string, int> byItem))
            {
                byItem = new Dictionary<string, int>(StringComparer.Ordinal);
                itemBuffers[normalizedDestination] = byItem;
            }

            string normalizedItemId = itemId ?? string.Empty;
            byItem[normalizedItemId] = byItem.TryGetValue(
                normalizedItemId,
                out int current)
                ? current + amount
                : amount;
        }
    }

    private sealed class TrackingWorkforceReplanService : IWorkforceReplanService
    {
        public int HaulReplans { get; private set; }

        public void RequestIdleWorkersToReplan(bool clearFailures = true)
        {
        }

        public void RequestOneWorkerToReplanFor(
            WorkTypeId workTypeId,
            bool clearFailures = true,
            bool forceInterrupt = false)
        {
        }

        public void RequestOneHaulerToReplan(
            bool clearFailures = true,
            bool forceInterrupt = false)
        {
            HaulReplans++;
        }
    }
}
#endif
