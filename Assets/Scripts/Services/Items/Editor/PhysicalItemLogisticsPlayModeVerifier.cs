#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class PhysicalItemLogisticsPlayModeVerifier
{
    public const string RequestPath = "Temp/physical-item-logistics-playmode.request";
    public const string ReportPath = "Artifacts/QA/physical-item-logistics-playmode-report.txt";
    public const string ConstructionRequestPath = "Temp/construction-project-playmode.request";
    public const string ConstructionReportPath = "Artifacts/QA/construction-project-playmode-report.txt";
    public const string L02RequestPath = "Temp/l02-mass-admission-playmode.request";
    public const string L02ReportPath = "Artifacts/QA/l02-mass-admission-playmode-report.txt";
    public const string ProductionInputMassRequestPath =
        "Temp/production-input-buffer-mass-playmode.request";
    public const string ProductionInputMassReportPath =
        "Artifacts/QA/production-input-buffer-mass-playmode-report.txt";
    public const string EquipmentRepairRequestPath =
        "Temp/equipment-repair-buffer-mass-playmode.request";
    public const string EquipmentRepairReportPath =
        "Artifacts/QA/equipment-repair-buffer-mass-playmode-report.txt";
    public const string PreparedOutputWarehouseRequestPath =
        "Temp/prepared-output-warehouse-live-playmode.request";
    public const string PreparedOutputWarehouseReportPath =
        "Artifacts/QA/prepared-output-warehouse-live-playmode-report.txt";
    public const string CarryCapturePath = "Artifacts/QA/physical-item-carry-ui.png";
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    private const string PersistenceSnapshotId =
        "physical-item-logistics-playmode";
    private static bool runnerCreated;
    private static bool persistenceCaptured;

    static PhysicalItemLogisticsPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Physical Item Logistics Verification")]
    public static void RequestRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request Construction Project Verification")]
    public static void RequestConstructionRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ConstructionReportPath);
        File.WriteAllText(ConstructionRequestPath, DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request L02 Mass Admission Verification")]
    public static void RequestL02RunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(L02ReportPath);
        File.WriteAllText(L02RequestPath, DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request Production Input Buffer Mass Verification")]
    public static void RequestProductionInputMassRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ProductionInputMassReportPath);
        File.WriteAllText(
            ProductionInputMassRequestPath,
            DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request Equipment Repair Buffer Mass Verification")]
    public static void RequestEquipmentRepairRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(EquipmentRepairReportPath);
        File.WriteAllText(
            EquipmentRepairRequestPath,
            DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request Prepared Output Warehouse Live Verification")]
    public static void RequestPreparedOutputWarehouseRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(PreparedOutputWarehouseReportPath);
        File.WriteAllText(
            PreparedOutputWarehouseRequestPath,
            DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if ((!File.Exists(RequestPath)
                && !File.Exists(ConstructionRequestPath)
                && !File.Exists(L02RequestPath)
                && !File.Exists(ProductionInputMassRequestPath)
                && !File.Exists(EquipmentRepairRequestPath)
                && !File.Exists(PreparedOutputWarehouseRequestPath))
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!persistenceCaptured
            && !DungeonFinalPlayModeAcceptanceRequestFacade
                .IsPersistenceCoordinatorActive)
        {
            try
            {
                PlayModeVerificationPersistenceSnapshot.CaptureCurrent(
                    PersistenceSnapshotId);
                persistenceCaptured = true;
            }
            catch (Exception exception)
            {
                FailBeforePlay("PERSISTENCE_SNAPSHOT_FAILED: " + exception);
                return;
            }
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                TitleScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            Scene dirty = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .FirstOrDefault(scene => scene.IsValid() && scene.isDirty);
            if (dirty.IsValid())
            {
                FailBeforePlay(
                    "EDITOR_SCENE_DIRTY: verification refused to unload '"
                    + dirty.path + "'.");
                return;
            }
            EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            // Opening a scene and entering PlayMode in the same editor update can
            // expose a partially built VContainer graph. The next update observes
            // the authored scene before requesting the mode transition.
            return;
        }

        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            persistenceCaptured = false;
            PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode
            || runnerCreated
            || !File.Exists(RequestPath)
                && !File.Exists(ConstructionRequestPath)
                && !File.Exists(L02RequestPath)
                && !File.Exists(ProductionInputMassRequestPath)
                && !File.Exists(EquipmentRepairRequestPath)
                && !File.Exists(PreparedOutputWarehouseRequestPath))
        {
            return;
        }

        runnerCreated = true;
        GameObject runner = new(
            "Physical Item Logistics PlayMode Verification Runner");
        UnityEngine.Object.DontDestroyOnLoad(runner);
        runner.AddComponent<PhysicalItemLogisticsPlayModeVerificationRunner>();
    }

    private static void FailBeforePlay(string detail)
    {
        Directory.CreateDirectory("Artifacts/QA");
        string reportPath = File.Exists(ConstructionRequestPath)
            ? ConstructionReportPath
            : File.Exists(L02RequestPath)
                ? L02ReportPath
                : File.Exists(ProductionInputMassRequestPath)
                    ? ProductionInputMassReportPath
                    : File.Exists(EquipmentRepairRequestPath)
                        ? EquipmentRepairReportPath
                        : File.Exists(PreparedOutputWarehouseRequestPath)
                            ? PreparedOutputWarehouseReportPath
                            : ReportPath;
        File.WriteAllText(
            reportPath,
            "Physical Item Logistics PlayMode Verification\n"
            + "[FAIL] EDITOR_BOOT_GUARD: " + detail + "\n"
            + "RESULT=FAIL; failures=1\n");
        File.Delete(RequestPath);
        File.Delete(ConstructionRequestPath);
        File.Delete(L02RequestPath);
        File.Delete(ProductionInputMassRequestPath);
        File.Delete(EquipmentRepairRequestPath);
        File.Delete(PreparedOutputWarehouseRequestPath);
        if (persistenceCaptured
            && !DungeonFinalPlayModeAcceptanceRequestFacade
                .IsPersistenceCoordinatorActive)
        {
            PlayModeVerificationPersistenceSnapshot.Restore(
                PersistenceSnapshotId);
        }
        persistenceCaptured = false;
        Debug.LogError(detail);
    }
}

public sealed class PhysicalItemLogisticsPlayModeVerificationRunner : MonoBehaviour
{
    private const string PreservedRationItemId = "food:preserved-ration";
    private const string DaggerItemId = "equipment-item:weapon:dagger";
    private const string DaggerId = "weapon:dagger";
    private const string RepairEquipmentId = "shield:wood";
    private const string InoculatedLogItemId = "supply:inoculated-log";
    private const string PreparedOutputCustodyComponentTypeId =
        "item-state:prepared-output-route-slice";
    private const float HaulTimeoutSeconds = 30f;
    private const float RuntimeReadyTimeoutSeconds = 45f;
    private const float PartyReadyTimeoutSeconds = 20f;

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();
    private readonly List<GameObject> temporaryObjects = new List<GameObject>();
    private readonly Dictionary<WarehouseInventory, WarehouseInventorySnapshot> warehouseSnapshots =
        new Dictionary<WarehouseInventory, WarehouseInventorySnapshot>();

    private DungeonPhysicalItemSaveData physicalSnapshot;
    private DungeonCombatEquipmentSaveData equipmentSnapshot;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private int verificationMouseSerial;
    private readonly Dictionary<CharacterActor, bool> isolatedAiPauseStates =
        new Dictionary<CharacterActor, bool>();
    private readonly List<CharacterActor> verificationActors = new List<CharacterActor>();
    private float originalTimeScale;
    private IWorldItemStackRuntime itemRuntime;
    private bool constructionOnly;
    private bool l02Only;
    private bool productionInputMassOnly;
    private bool equipmentRepairOnly;
    private bool preparedOutputWarehouseOnly;
    private IDungeonDebugModeService debugMode;
    private bool originalFreezeNeeds;
    private bool originalFriendlyInvincible;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        constructionOnly = File.Exists(PhysicalItemLogisticsPlayModeVerifier.ConstructionRequestPath);
        l02Only = File.Exists(PhysicalItemLogisticsPlayModeVerifier.L02RequestPath);
        productionInputMassOnly = File.Exists(
            PhysicalItemLogisticsPlayModeVerifier.ProductionInputMassRequestPath);
        equipmentRepairOnly = File.Exists(
            PhysicalItemLogisticsPlayModeVerifier.EquipmentRepairRequestPath);
        preparedOutputWarehouseOnly = File.Exists(
            PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseRequestPath);
        Application.logMessageReceived += OnLogMessageReceived;
        EnsureEventSystem();
        SetupInput();
        originalTimeScale = Time.timeScale;
        Time.timeScale = 8f;

        yield return EnsureProductBoot();

        DungeonRuntimeLifetimeScope scope = null;
        OwnerRunManager authoredOwnerManager = null;
        string compositionDetail = "runtime composition was not observed";
        float compositionDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < compositionDeadline)
        {
            if (TryFindReadyComposition(
                    out scope,
                    out authoredOwnerManager,
                    out compositionDetail))
            {
                break;
            }
            yield return null;
        }
        Check(
            scope != null && authoredOwnerManager != null,
            "RUNTIME_COMPOSITION_READY",
            compositionDetail);

        itemRuntime = Resolve<IWorldItemStackRuntime>(scope);
        IWorkOrderRuntime workOrderRuntime = Resolve<IWorkOrderRuntime>(scope);
        ICombatEquipmentRuntime equipment = Resolve<ICombatEquipmentRuntime>(scope);
        ICombatEquipmentMaintenanceRuntime equipmentMaintenance =
            Resolve<ICombatEquipmentMaintenanceRuntime>(scope);
        IResourceEconomyContentCatalog economyCatalog =
            Resolve<IResourceEconomyContentCatalog>(scope);
        IOffensePreparationService preparation = Resolve<IOffensePreparationService>(scope);
        IFacilityBufferDestinationClaimQuery destinationClaims =
            Resolve<IFacilityBufferDestinationClaimQuery>(scope);
        IWarehouseMassAdmissionService warehouseMassAdmission =
            Resolve<IWarehouseMassAdmissionService>(scope);
        debugMode = Resolve<IDungeonDebugModeService>(scope);
        GridSystemManager gridSystem = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = gridSystem != null ? gridSystem.grid : null;

        Check(scope != null && scope.Container != null, "SCOPE_READY", "gameplay LifetimeScope resolved");
        Check(itemRuntime != null, "ITEM_RUNTIME_READY", "world item runtime resolved");
        Check(workOrderRuntime != null, "WORK_ORDER_RUNTIME_READY", "work order runtime resolved");
        Check(equipment != null, "EQUIPMENT_RUNTIME_READY", "common combat equipment runtime resolved");
        Check(equipmentMaintenance != null,
            "EQUIPMENT_MAINTENANCE_READY",
            "equipment maintenance runtime resolved");
        Check(economyCatalog != null, "ECONOMY_CATALOG_READY", "resource economy catalog resolved");
        Check(preparation != null, "PREPARATION_RUNTIME_READY", "offense preparation service resolved");
        Check(destinationClaims != null,
            "DESTINATION_CLAIM_RUNTIME_READY",
            "haul destination claim authority resolved");
        Check(warehouseMassAdmission != null,
            "WAREHOUSE_MASS_ADMISSION_RUNTIME_READY",
            "warehouse gram admission authority resolved");
        Check(debugMode != null, "DEBUG_MODE_READY", "debug mode service resolved");
        Check(grid != null, "GRID_READY", "grid resolved");
        if (scope == null
            || itemRuntime == null
            || workOrderRuntime == null
            || equipment == null
            || equipmentMaintenance == null
            || economyCatalog == null
            || preparation == null
            || destinationClaims == null
            || warehouseMassAdmission == null
            || debugMode == null
            || grid == null)
        {
            Finish();
            yield break;
        }

        yield return EnsurePlayableRun();
        CharacterActor hauler = FindHauler();
        Check(hauler != null, "HAULER_READY", hauler != null ? hauler.name : "no staff/owner hauler");
        if (hauler == null)
        {
            Finish();
            yield break;
        }

        CaptureRuntimeState(itemRuntime, equipment);
        ConfigureVerificationDebugMode();
        DisableBrainForDeterministicHauling(hauler);
        yield return null;
        yield return null;
        Check(verificationActors
                .Where(actor => actor != null && !actor.IsDead)
                .All(actor => actor.IsAiPaused()
                    && actor.GetComponent<AbilityMove>()
                        ?.HasActiveMovementRoutineForDiagnostics != true
                    && actor.GetComponent<AbilityHaul>()?.IsHauling != true),
            "HAUL_FIXTURE_AI_OWNERSHIP_ISOLATED",
            $"actors={verificationActors.Count}; "
            + string.Join(",", verificationActors
                .Where(actor => actor != null)
                .Select(actor =>
                    $"{actor.BuildingCharacterId}:paused={actor.IsAiPaused()}:"
                    + $"move={actor.GetComponent<AbilityMove>()?.HasActiveMovementRoutineForDiagnostics == true}:"
                    + $"haul={actor.GetComponent<AbilityHaul>()?.IsHauling == true}")));

        if (l02Only)
        {
            try
            {
                itemRuntime.Restore(new DungeonPhysicalItemSaveData());
                CharacterCarryInventory.Ensure(hauler)?.RemoveAllItems();
                yield return VerifyL02MassAdmissionAndPickupRejection(
                    scope,
                    itemRuntime,
                    warehouseMassAdmission,
                    destinationClaims,
                    grid,
                    hauler);
            }
            finally
            {
                RestoreRuntimeState(itemRuntime, equipment);
            }

            yield return null;
            Finish();
            yield break;
        }


        if (productionInputMassOnly)
        {
            try
            {
                itemRuntime.Restore(new DungeonPhysicalItemSaveData());
                CharacterCarryInventory.Ensure(hauler)?.RemoveAllItems();
                yield return VerifyProductionInputBufferMassAdmission(
                    scope,
                    itemRuntime,
                    grid,
                    hauler);
            }
            finally
            {
                RestoreRuntimeState(itemRuntime, equipment);
            }

            yield return null;
            Finish();
            yield break;
        }

        if (preparedOutputWarehouseOnly)
        {
            WorldItemStackSnapshot[] preexistingStacks = itemRuntime.GetAllStacks()
                .Where(value => value != null)
                .ToArray();
            IFacilityOutputExactRouteOutboxQuery exactRoutes =
                Resolve<IFacilityOutputExactRouteOutboxQuery>(scope);
            CharacterActor[] fixtureActors = CharacterActorCollection
                .DistinctByGameObject(
                    UnityEngine.Object.FindObjectsByType<CharacterActor>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None))
                .Where(actor => actor != null && !actor.IsDead)
                .ToArray();
            int custodyStackCount = preexistingStacks.Count(stack =>
                (stack.Components ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Any(component => component != null
                    && string.Equals(
                        component.componentTypeId,
                        PreparedOutputCustodyComponentTypeId,
                        StringComparison.Ordinal)));
            int pendingRouteCount = exactRoutes?.CapturePendingRoutes()?.Count ?? -1;
            int committedHaulCount = fixtureActors.Count(actor =>
                actor.GetComponent<AbilityHaul>()?.CaptureDeliveryIntentForSave()
                    != null);
            int activeHaulerCount = fixtureActors.Count(actor =>
                actor.GetComponent<AbilityHaul>()?.IsHauling == true);
            bool fixtureBoundaryClear = exactRoutes != null
                && custodyStackCount == 0
                && pendingRouteCount == 0
                && committedHaulCount == 0
                && activeHaulerCount == 0;
            Check(fixtureBoundaryClear,
                "PREPARED_OUTPUT_FIXTURE_BOUNDARY_CLEAR",
                $"stacks={preexistingStacks.Length}; custody={custodyStackCount}; "
                + $"routes={pendingRouteCount}; committedHauls={committedHaulCount}; "
                + $"activeHaulers={activeHaulerCount}");
            if (!fixtureBoundaryClear)
            {
                Finish();
                yield break;
            }

            int quarantineFailures = 0;
            foreach (WorldItemStackSnapshot existing in preexistingStacks)
            {
                if (!itemRuntime.SetForbidden(existing.StackId, true))
                {
                    quarantineFailures++;
                }
            }
            Check(quarantineFailures == 0,
                "PREPARED_OUTPUT_FIXTURE_EXISTING_STACKS_QUARANTINED",
                $"stacks={preexistingStacks.Length}; failures={quarantineFailures}");
            if (quarantineFailures != 0)
            {
                Finish();
                yield break;
            }

            QuiesceHaulingBeforeDirectStateFixture();
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(hauler);
            Check(carry?.Items.Count == 0,
                "PREPARED_OUTPUT_FIXTURE_CARRY_EMPTY",
                $"carried={carry?.Items.Count ?? -1}");
            if (carry?.Items.Count != 0)
            {
                Finish();
                yield break;
            }
            yield return VerifyPreparedOutputWarehouseLiveRoute(
                scope,
                itemRuntime,
                grid,
                hauler,
                warehouseMassAdmission);
            yield return null;
            Finish();
            yield break;
        }

        try
        {
            itemRuntime.Restore(new DungeonPhysicalItemSaveData());
            CharacterCarryInventory.Ensure(hauler)?.RemoveAllItems();

            Vector2Int actorPos = hauler.GetNowXY();
            IReadOnlyList<Vector2Int> positions = FindReachableCells(grid, actorPos, 48);
            Check(positions.Count >= 3, "REACHABLE_TEST_CELLS", $"count={positions.Count}; actor={actorPos}");
            if (positions.Count < 3)
            {
                Finish();
                yield break;
            }

            BuildingSO warehouseAsset = FindWarehouseAsset();
            BuildingSO benchAsset = FindCraftBenchAsset();
            bool warehouseCellFound = TryFindRegisterablePosition(
                grid,
                warehouseAsset,
                positions,
                out Vector2Int warehousePosition);
            HashSet<Vector2Int> warehouseFootprint = warehouseCellFound
                ? warehouseAsset.GetGridPosList(warehousePosition).ToHashSet()
                : new HashSet<Vector2Int>();
            Vector2Int[] remainingPositions = positions
                .Where(position => !warehouseFootprint.Contains(position))
                .ToArray();
            Vector2Int benchPosition = remainingPositions.FirstOrDefault();
            Vector2Int testPosition = remainingPositions.Skip(1).FirstOrDefault();
            Facility warehouse = CreateInjectedFacility(
                scope,
                grid,
                warehouseAsset,
                warehousePosition,
                "QA_Physical_Logistics_Warehouse",
                registerOnGrid: true);
            Facility bench = CreateInjectedFacility(
                scope,
                grid,
                benchAsset,
                benchPosition,
                "QA_Physical_Logistics_Bench");
            Check(warehouseCellFound && remainingPositions.Length >= 2,
                "TEMP_WAREHOUSE_GRID_CELL_READY",
                $"found={warehouseCellFound}; remaining={remainingPositions.Length}; warehouse={warehousePosition}");
            Check(warehouse != null && warehouse.Inventory != null,
                "TEMP_WAREHOUSE_READY",
                warehouse != null ? warehouse.name : "missing warehouse");
            Check(bench != null && bench.BuildingData != null
                    && bench.BuildingData.GetAbility<BuildingEquipmentCraftingAbility>() != null,
                "TEMP_CRAFT_BENCH_READY",
                bench != null ? bench.name : "missing bench");
            if (warehouse == null || warehouse.Inventory == null || bench == null)
            {
                Finish();
                yield break;
            }

            ClearInventory(warehouse.Inventory);
            if (equipmentRepairOnly)
            {
                Check(SeedStoredCraftMaterial(
                        itemRuntime,
                        economyCatalog,
                        warehouse,
                        "material:blacksteel",
                        8,
                        out string repairMaterialSeedDetails),
                    "MATERIAL_REPAIR_STOCK_SEEDED",
                    repairMaterialSeedDetails);
                yield return VerifyMaterialRepairAndSalvage(
                    itemRuntime,
                    equipment,
                    equipmentMaintenance,
                    economyCatalog,
                    destinationClaims,
                    scope,
                    grid,
                    hauler,
                    warehouse,
                    warehouse.centerPos);
                yield return null;
                Finish();
                yield break;
            }

            long warehouseRevisionBeforeSeed =
                warehouseMassAdmission.GetWarehouseCapacityRevision(
                    warehouse.PersistentInstanceId);
            if (constructionOnly)
            {
                Check(SeedStoredCraftMaterial(
                        itemRuntime,
                        economyCatalog,
                        warehouse,
                        "material:wood",
                        4,
                        out string woodSeedDetails),
                    "CONSTRUCTION_WOOD_SEEDED",
                    woodSeedDetails);
                yield return VerifyConstructionMaterialDelivery(
                    itemRuntime,
                    workOrderRuntime,
                    scope,
                    grid,
                    hauler,
                    warehouse,
                    testPosition);
            }
            else
            {
            Check(itemRuntime.SpawnStockInWarehouse(
                        warehouse,
                        StockCategory.General,
                        4,
                        out int seededGeneral)
                    && itemRuntime.SpawnStockInWarehouse(
                        warehouse,
                        StockCategory.Food,
                        5,
                        out int seededFood)
                    && seededGeneral == 4
                    && seededFood == 5,
                "TEMP_WAREHOUSE_SEEDED",
                $"food={warehouse.Inventory.GetStock(StockCategory.Food)}; general={warehouse.Inventory.GetStock(StockCategory.General)}; weapon={warehouse.Inventory.GetStock(StockCategory.Weapon)}");
            Check(
                warehouseMassAdmission.GetWarehouseCapacityRevision(
                    warehouse.PersistentInstanceId) > warehouseRevisionBeforeSeed
                && warehouse.Inventory.StoredMassGrams > 0L
                && warehouse.Inventory.ReservedInboundMassGrams == 0L,
                "WAREHOUSE_MASS_ADMISSION_PRODUCTION_INGRESS_COMMITTED",
                $"revision={warehouseRevisionBeforeSeed}->"
                + $"{warehouseMassAdmission.GetWarehouseCapacityRevision(warehouse.PersistentInstanceId)}; "
                + $"stored={warehouse.Inventory.StoredMassGrams}; "
                + $"reserved={warehouse.Inventory.ReservedInboundMassGrams}");
            IBuildingSummaryFormatter buildingSummary =
                scope.Container.Resolve<IBuildingSummaryFormatter>();
            BuildingSummaryPresentation warehousePresentation =
                buildingSummary.Format(warehouse);
            Check(warehousePresentation.StockText.Contains(
                        "12kg/25kg",
                        StringComparison.Ordinal)
                    && !warehousePresentation.StockText.Contains(
                        "/60",
                        StringComparison.Ordinal),
                "WAREHOUSE_MASS_UI_PRODUCTION_EXACT_KG",
                warehousePresentation.StockText.Replace('\n', ' '));
            IDungeonGridBuildingControllerProvider buildingControllerProvider =
                scope.Container.Resolve<IDungeonGridBuildingControllerProvider>();
            bool nonEmptyWarehouseDestroyed =
                buildingControllerProvider.Controller.TryDestroyBuilding(
                    warehouse,
                    out string nonEmptyDestroyFailure);
            Check(!nonEmptyWarehouseDestroyed
                    && !warehouse.isDestroy
                    && warehouse.Inventory.StoredMassGrams > 0L
                    && nonEmptyDestroyFailure.Contains(
                        "warehouse-lifecycle-not-empty",
                        StringComparison.Ordinal),
                "WAREHOUSE_NONEMPTY_DEMOLITION_REJECTED",
                $"destroyed={nonEmptyWarehouseDestroyed}; "
                + $"stored={warehouse.Inventory.StoredMassGrams}; "
                + $"failure={nonEmptyDestroyFailure}");
            IFacilityRelocationWorldService relocationWorld =
                scope.Container.Resolve<IFacilityRelocationWorldService>();
            bool nonEmptyWarehouseRelocatable = relocationWorld.CanRelocate(
                warehouse,
                testPosition,
                out string nonEmptyRelocationFailure);
            Check(!nonEmptyWarehouseRelocatable
                    && !warehouse.isDestroy
                    && warehouse.Inventory.StoredMassGrams > 0L
                    && nonEmptyRelocationFailure.Contains(
                        "warehouse-lifecycle-not-empty",
                        StringComparison.Ordinal),
                "WAREHOUSE_NONEMPTY_RELOCATION_REJECTED",
                $"relocatable={nonEmptyWarehouseRelocatable}; "
                + $"stored={warehouse.Inventory.StoredMassGrams}; "
                + $"failure={nonEmptyRelocationFailure}");
            Check(SeedStoredCraftMaterial(
                    itemRuntime,
                    economyCatalog,
                    warehouse,
                    "material:iron",
                    6,
                    out string materialSeedDetails),
                "TEMP_WAREHOUSE_IRON_SEEDED",
                materialSeedDetails);
            Check(SeedStoredCraftMaterial(
                    itemRuntime,
                    economyCatalog,
                    warehouse,
                    "material:wood",
                    4,
                    out string woodSeedDetails),
                "TEMP_WAREHOUSE_WOOD_SEEDED",
                woodSeedDetails);
            yield return VerifyLooseStackToWarehouse(itemRuntime, grid, hauler, warehouse, testPosition);
            yield return VerifyFacilityInputDelivery(itemRuntime, hauler, warehouse, bench);
            yield return VerifyConstructionMaterialDelivery(
                itemRuntime,
                workOrderRuntime,
                scope,
                grid,
                hauler,
                warehouse,
                testPosition);
            yield return VerifyCraftMaterialsOutputAndEquipmentDeposit(itemRuntime, equipment, hauler, warehouse, bench);
            yield return VerifyMaterialRepairAndSalvage(
                itemRuntime,
                equipment,
                equipmentMaintenance,
                economyCatalog,
                destinationClaims,
                scope,
                grid,
                hauler,
                warehouse,
                warehouse.centerPos);
            yield return VerifyExpeditionPacking(
                preparation,
                itemRuntime,
                destinationClaims,
                warehouse,
                hauler);
            QuiesceHaulingBeforeDirectStateFixture();
            yield return null;
            yield return VerifyL02MassAdmissionAndPickupRejection(
                scope,
                itemRuntime,
                warehouseMassAdmission,
                destinationClaims,
                grid,
                hauler);
            QuiesceHaulingBeforeDirectStateFixture();
            yield return null;
            yield return VerifyCarryUi(itemRuntime, hauler);
            VerifyWarehouseTransactionalRestoreBoundary(
                scope,
                itemRuntime,
                warehouse);
            yield return VerifyWarehouseOfficialRestoreBoundary(
                scope,
                itemRuntime,
                warehouse,
                hauler);
            }
        }
        finally
        {
            RestoreRuntimeState(itemRuntime, equipment);
        }

        yield return null;
        Finish();
    }

    private void VerifyWarehouseTransactionalRestoreBoundary(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Facility warehouse)
    {
        DungeonPhysicalItemSaveData baseline = runtime.Capture();
        string baselineJson = JsonUtility.ToJson(baseline);
        ICharacterAiWorldRegistry world =
            Resolve<ICharacterAiWorldRegistry>(scope);
        StaticRestoreWorldCandidates candidates = new(
            (world?.Warehouses ?? Array.Empty<IWarehouseFacility>())
                .OfType<BuildableObject>()
                .ToArray());
        IPhysicalItemRestoreStaging staging = runtime as IPhysicalItemRestoreStaging;
        Check(staging != null,
            "WAREHOUSE_RESTORE_TRANSACTIONAL_STAGING_AVAILABLE",
            staging != null ? "resolved" : "missing");
        if (staging == null)
        {
            return;
        }

        WorldItemStackSaveData stored = baseline.stacks
            .FirstOrDefault(stack => stack != null
                && stack.state == WorldItemStackState.Stored
                && string.Equals(
                    string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                        ? stack.destinationId
                        : stack.sourceStorageDestinationId,
                    WarehouseStorageIdentity.RequireDestinationId(warehouse),
                    StringComparison.Ordinal));
        Check(stored != null,
            "WAREHOUSE_RESTORE_FIXTURE_STORED_STACK_AVAILABLE",
            stored != null ? $"stack={stored.stackId}; item={stored.itemId}" : "missing");
        if (stored == null)
        {
            return;
        }

        DungeonPhysicalItemSaveData orphaned = ClonePhysicalSnapshot(baseline);
        WorldItemStackSaveData orphanedStack = orphaned.stacks.Single(
            stack => string.Equals(stack.stackId, stored.stackId, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(orphanedStack.sourceStorageDestinationId))
        {
            orphanedStack.destinationId = "warehouse:building:qa-orphan";
        }
        else
        {
            orphanedStack.sourceStorageDestinationId =
                "warehouse:building:qa-orphan";
        }
        bool orphanRejected = TryStageExpectedWarehouseRestoreFailure(
            staging,
            orphaned,
            candidates,
            "items.restore.warehouse_owner_missing",
            out string orphanFailure);
        Check(orphanRejected
                && string.Equals(
                    JsonUtility.ToJson(runtime.Capture()),
                    baselineJson,
                    StringComparison.Ordinal),
            "WAREHOUSE_RESTORE_INVALID_DESTINATION_ATOMIC",
            $"rejected={orphanRejected}; failure={orphanFailure}");

        DungeonPhysicalItemSaveData shifted = ClonePhysicalSnapshot(baseline);
        string storageDestination = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        foreach (WorldItemStackSaveData shiftedStack in shifted.stacks.Where(
                     stack => stack != null
                         && stack.state == WorldItemStackState.Stored
                         && string.Equals(
                             string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                                 ? stack.destinationId
                                 : stack.sourceStorageDestinationId,
                             storageDestination,
                             StringComparison.Ordinal)))
        {
            shiftedStack.gridX += 1;
        }
        SortPhysicalStacks(shifted);
        bool positionRejected = TryStageExpectedWarehouseRestoreFailure(
            staging,
            shifted,
            candidates,
            "items.restore.warehouse_position_mismatch",
            out string positionFailure);
        Check(positionRejected
                && string.Equals(
                    JsonUtility.ToJson(runtime.Capture()),
                    baselineJson,
                    StringComparison.Ordinal),
            "WAREHOUSE_RESTORE_POSITION_MISMATCH_ATOMIC",
            $"rejected={positionRejected}; failure={positionFailure}");

        DungeonPhysicalItemSaveData overCapacity = ClonePhysicalSnapshot(baseline);
        WorldItemStackSaveData overCapacityStack = overCapacity.stacks.Single(
            stack => string.Equals(stack.stackId, stored.stackId, StringComparison.Ordinal));
        DungeonItemDefinition definition = runtime.CatalogProvider.GetDefinition(
            overCapacityStack.itemId);
        long unitMass = runtime.MassQuery
            .GetDefinitionUnitMass((ItemDefinitionId)overCapacityStack.itemId)
            .Value;
        overCapacityStack.quantity = definition.MaxStack;
        long expectedStoredMass = CalculateStoredMass(
            overCapacity,
            runtime,
            storageDestination);
        if (expectedStoredMass <= warehouse.Inventory.MaxMassGrams)
        {
            WorldItemStackSaveData extra =
                JsonUtility.FromJson<WorldItemStackSaveData>(
                    JsonUtility.ToJson(overCapacityStack));
            extra.stackId = "stack:qa-over-capacity-0001";
            long deficit = checked(
                warehouse.Inventory.MaxMassGrams - expectedStoredMass + 1L);
            extra.quantity = checked((int)Math.Min(
                definition.MaxStack,
                (deficit + unitMass - 1L) / unitMass));
            overCapacity.stacks.Add(extra);
            SortPhysicalStacks(overCapacity);
            expectedStoredMass = CalculateStoredMass(
                overCapacity,
                runtime,
                storageDestination);
        }
        bool fixtureCanExceed = expectedStoredMass
            > warehouse.Inventory.MaxMassGrams;
        Check(fixtureCanExceed,
            "WAREHOUSE_RESTORE_OVER_CAPACITY_FIXTURE_VALID",
            $"item={overCapacityStack.itemId}; unit={unitMass}; expected={expectedStoredMass}; max={warehouse.Inventory.MaxMassGrams}");
        if (!fixtureCanExceed)
        {
            return;
        }
        try
        {
            staging.StageTransactionalRestore(overCapacity, candidates)
                .Commit(new DungeonGameRestoreReport());
            long restoredMass = warehouse.Inventory.StoredMassGrams;
            Check(restoredMass > warehouse.Inventory.MaxMassGrams
                    && warehouse.Inventory.RemainingMassGrams == 0L,
                "WAREHOUSE_RESTORE_OVER_CAPACITY_PRESERVED",
                $"stored={restoredMass}; max={warehouse.Inventory.MaxMassGrams}; remaining={warehouse.Inventory.RemainingMassGrams}");

            bool admitted = runtime.SpawnStockInWarehouse(
                warehouse,
                definition.StockCategory,
                1,
                out int spawned);
            Check(!admitted
                    && spawned == 0
                    && warehouse.Inventory.StoredMassGrams == restoredMass,
                "WAREHOUSE_RESTORE_OVER_CAPACITY_ADMISSION_BLOCKED",
                $"admitted={admitted}; spawned={spawned}; stored={warehouse.Inventory.StoredMassGrams}");
        }
        finally
        {
            runtime.Restore(baseline);
        }
    }

    private static bool TryStageExpectedWarehouseRestoreFailure(
        IPhysicalItemRestoreStaging staging,
        DungeonPhysicalItemSaveData snapshot,
        IRestoreWorldCandidateQuery candidates,
        string expectedCode,
        out string failure)
    {
        try
        {
            staging.StageTransactionalRestore(snapshot, candidates);
            failure = "accepted";
            return false;
        }
        catch (InvalidOperationException exception)
        {
            failure = exception.Message;
            return exception.Message.Contains(
                expectedCode,
                StringComparison.Ordinal);
        }
    }

    private IEnumerator VerifyWarehouseOfficialRestoreBoundary(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Facility warehouse,
        CharacterActor originalHauler)
    {
        IDungeonSaveSectionRegistry registry =
            Resolve<IDungeonSaveSectionRegistry>(scope);
        IWarehouseOverCapacityEvacuationQuery evacuation =
            Resolve<IWarehouseOverCapacityEvacuationQuery>(scope);
        Check(registry != null && evacuation != null,
            "WAREHOUSE_RESTORE_OFFICIAL_RUNTIME_READY",
            $"registry={registry != null}; evacuation={evacuation != null}");
        if (registry == null || evacuation == null)
        {
            yield break;
        }

        Grid grid = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>()?.grid;
        BuildingSO warehouseAsset = FindWarehouseAsset();
        IReadOnlyList<Vector2Int> targetCandidates = grid == null
            ? Array.Empty<Vector2Int>()
            : FindReachableCells(grid, warehouse.centerPos, 96);
        Vector2Int targetPosition = default;
        bool targetCellFound = grid != null
            && TryFindRegisterablePosition(
                grid,
                warehouseAsset,
                targetCandidates,
                out targetPosition);
        Facility targetWarehouse = targetCellFound
            ? CreateInjectedFacility(
                scope,
                grid,
                warehouseAsset,
                targetPosition,
                "QA_Physical_Logistics_Evacuation_Warehouse",
                registerOnGrid: true)
            : null;
        Check(targetWarehouse?.Inventory?.HasMassCapacityAuthority == true,
            "WAREHOUSE_EVACUATION_TARGET_READY",
            targetWarehouse == null
                ? $"found={targetCellFound}"
                : $"id={targetWarehouse.PersistentInstanceId.Value}; max={targetWarehouse.Inventory.MaxMassGrams}");
        if (targetWarehouse?.Inventory?.HasMassCapacityAuthority != true)
        {
            yield break;
        }
        ClearInventory(targetWarehouse.Inventory);
        IWarehouseLifecycleOccupancyQuery lifecycleOccupancy =
            Resolve<IWarehouseLifecycleOccupancyQuery>(scope);
        WarehouseLifecycleOccupancySnapshot emptyOccupancy = default;
        string emptyFailure = "lifecycle query missing";
        bool emptyTargetAccepted = lifecycleOccupancy != null
            && lifecycleOccupancy.TryRequireEmpty(
                targetWarehouse,
                out emptyOccupancy,
                out emptyFailure);
        Check(emptyTargetAccepted,
            "WAREHOUSE_EMPTY_LIFECYCLE_GATE_OPEN",
            lifecycleOccupancy == null
                ? "lifecycle query missing"
                : $"stored={emptyOccupancy.StoredMassGrams}; "
                    + $"reserved={emptyOccupancy.ReservedInboundMassGrams}; "
                    + $"stacks={emptyOccupancy.ReferencedPhysicalStackCount}; "
                    + $"intents={emptyOccupancy.ActiveHaulIntentCount}; "
                    + $"failure={emptyFailure}");
        string targetWarehouseOwnerId = targetWarehouse.PersistentInstanceId.Value;

        List<DungeonSaveSectionEnvelope> baseline = registry.CaptureAll();
        DungeonSaveSectionEnvelope physicalEnvelope = baseline.SingleOrDefault(
            envelope => string.Equals(
                envelope.sectionId,
                PhysicalItemsSaveSection.Id,
                StringComparison.Ordinal));
        if (physicalEnvelope == null)
        {
            Check(false,
                "WAREHOUSE_RESTORE_OFFICIAL_ENVELOPE_AVAILABLE",
                "items.physical missing");
            yield break;
        }

        string destinationId = WarehouseStorageIdentity.RequireDestinationId(
            warehouse);
        string warehouseOwnerId = warehouse.PersistentInstanceId.Value;
        DungeonPhysicalItemSaveData overCapacity =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                physicalEnvelope.payloadJson);
        WorldItemStackSaveData stored = overCapacity.stacks
            .Where(stack => stack != null
                && stack.state == WorldItemStackState.Stored
                && string.Equals(
                    string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                        ? stack.destinationId
                        : stack.sourceStorageDestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .FirstOrDefault(stack => runtime.CatalogProvider
                .GetDefinition(stack.itemId).MaxStack > 1);
        Check(stored != null,
            "WAREHOUSE_RESTORE_OFFICIAL_ENVELOPE_AVAILABLE",
            stored != null ? $"stack={stored.stackId}" : "generic stored stack missing");
        if (stored == null)
        {
            yield break;
        }

        DungeonItemDefinition definition = runtime.CatalogProvider.GetDefinition(
            stored.itemId);
        long unitMass = runtime.MassQuery
            .GetDefinitionUnitMass((ItemDefinitionId)stored.itemId)
            .Value;
        stored.quantity = definition.MaxStack;
        long expectedMass = CalculateStoredMass(
            overCapacity,
            runtime,
            destinationId);
        int extraOrdinal = 1;
        while (expectedMass <= warehouse.Inventory.MaxMassGrams)
        {
            WorldItemStackSaveData extra =
                JsonUtility.FromJson<WorldItemStackSaveData>(
                    JsonUtility.ToJson(stored));
            extra.stackId =
                $"stack:qa-official-over-capacity-{extraOrdinal++:D4}";
            long deficit = checked(
                warehouse.Inventory.MaxMassGrams - expectedMass + 1L);
            extra.quantity = checked((int)Math.Min(
                definition.MaxStack,
                Math.Max(1L, (deficit + unitMass - 1L) / unitMass)));
            overCapacity.stacks.Add(extra);
            expectedMass = checked(
                expectedMass + unitMass * extra.quantity);
        }
        SortPhysicalStacks(overCapacity);

        List<DungeonSaveSectionEnvelope> modified = baseline
            .Select(envelope => new DungeonSaveSectionEnvelope
            {
                sectionId = envelope.sectionId,
                sectionVersion = envelope.sectionVersion,
                restorePhase = envelope.restorePhase,
                optional = envelope.optional,
                payloadJson = string.Equals(
                    envelope.sectionId,
                    PhysicalItemsSaveSection.Id,
                    StringComparison.Ordinal)
                        ? JsonUtility.ToJson(overCapacity)
                        : envelope.payloadJson
            })
            .ToList();

        bool restored = false;
        string cleanupErrors = string.Empty;
        try
        {
            DungeonGameRestoreReport report = new();
            restored = registry.RestoreAll(modified, report) && report.Success;
            if (restored)
            {
                // Restore publishes replacement actor objects. Re-establish
                // verifier isolation before the next frame so autonomous AI
                // cannot reserve an unrelated destination gram token.
                QuiesceHaulingBeforeDirectStateFixture();
            }
            ICharacterAiWorldRegistry world =
                Resolve<ICharacterAiWorldRegistry>(scope);
            IWarehouseFacility restoredWarehouse = world?.Warehouses
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.PersistentInstanceId.Value,
                        warehouseOwnerId,
                        StringComparison.Ordinal));
            IWarehouseFacility restoredTargetWarehouse = world?.Warehouses
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.PersistentInstanceId.Value,
                        targetWarehouseOwnerId,
                        StringComparison.Ordinal));
            Check(restored,
                "WAREHOUSE_RESTORE_OFFICIAL_FULL_ROUNDTRIP",
                $"restored={restored}; errors={string.Join(" | ", report.Errors)}");
            Check(restored
                    && restoredWarehouse?.Inventory != null
                    && restoredWarehouse.Inventory.StoredMassGrams == expectedMass
                    && restoredWarehouse.Inventory.RemainingMassGrams == 0L,
                "WAREHOUSE_RESTORE_OFFICIAL_OVER_CAPACITY_PRESERVED",
                $"expected={expectedMass}; actual={restoredWarehouse?.Inventory?.StoredMassGrams ?? -1L}; remaining={restoredWarehouse?.Inventory?.RemainingMassGrams ?? -1L}");
            Check(restored
                    && evacuation.IsPending(destinationId)
                    && evacuation.CapturePendingWarehouseIds().Count(id =>
                        string.Equals(id, destinationId, StringComparison.Ordinal)) == 1,
                "WAREHOUSE_RESTORE_EVACUATION_PUBLISHED_AFTER_ROOT_SWAP",
                $"revision={evacuation.Revision}; pending={string.Join(",", evacuation.CapturePendingWarehouseIds())}");

            long targetMassBeforeEvacuation =
                restoredTargetWarehouse?.Inventory?.StoredMassGrams ?? 0L;
            Vector2Int restoredSourcePosition =
                (restoredWarehouse as BuildableObject)?.centerPos ?? default;
            Vector2Int restoredTargetPosition =
                (restoredTargetWarehouse as BuildableObject)?.centerPos ?? default;
            int sourceItemQuantityBeforeEvacuation = GetStoredItemQuantity(
                runtime,
                stored.itemId,
                restoredSourcePosition);
            int targetItemQuantityBeforeEvacuation = GetStoredItemQuantity(
                runtime,
                stored.itemId,
                restoredTargetPosition);
            CharacterActor restoredHauler = FindHauler();
            Check(restored
                    && restoredHauler != null
                    && restoredTargetWarehouse?.Inventory != null,
                "WAREHOUSE_EVACUATION_LIVE_FIXTURE_READY",
                $"hauler={restoredHauler?.BuildingCharacterId}; "
                + $"source={DescribeWarehouse(restoredWarehouse)}; "
                + $"target={DescribeWarehouse(restoredTargetWarehouse)}");
            if (restored
                && restoredHauler != null
                && restoredWarehouse?.Inventory != null
                && restoredTargetWarehouse?.Inventory != null)
            {
                yield return RunRepeatedHaul(
                    restoredHauler,
                    () => restoredWarehouse.Inventory.StoredMassGrams
                            <= restoredWarehouse.Inventory.MaxMassGrams
                        && restoredTargetWarehouse.Inventory.StoredMassGrams
                            > targetMassBeforeEvacuation
                        && !evacuation.IsPending(destinationId));
                int sourceItemQuantityAfterEvacuation = GetStoredItemQuantity(
                    runtime,
                    stored.itemId,
                    restoredSourcePosition);
                int targetItemQuantityAfterEvacuation = GetStoredItemQuantity(
                    runtime,
                    stored.itemId,
                    restoredTargetPosition);
                int sourceQuantityMoved = sourceItemQuantityBeforeEvacuation
                    - sourceItemQuantityAfterEvacuation;
                int targetQuantityReceived = targetItemQuantityAfterEvacuation
                    - targetItemQuantityBeforeEvacuation;
                Check(restoredWarehouse.Inventory.StoredMassGrams
                            <= restoredWarehouse.Inventory.MaxMassGrams
                        && restoredTargetWarehouse.Inventory.StoredMassGrams
                            > targetMassBeforeEvacuation,
                    "WAREHOUSE_EVACUATION_AI_HAUL_COMPLETED",
                    $"source={restoredWarehouse.Inventory.StoredMassGrams}/{restoredWarehouse.Inventory.MaxMassGrams}; "
                    + $"target={restoredTargetWarehouse.Inventory.StoredMassGrams}/{restoredTargetWarehouse.Inventory.MaxMassGrams}");
                Check(sourceQuantityMoved > 0
                        && targetQuantityReceived == sourceQuantityMoved
                        && restoredWarehouse.Inventory.ReservedInboundMassGrams == 0L
                        && restoredTargetWarehouse.Inventory.ReservedInboundMassGrams == 0L
                        && !evacuation.IsPending(destinationId),
                    "WAREHOUSE_EVACUATION_GRAM_TOKEN_CONSERVATION_EXACT",
                    $"item={stored.itemId}; quantity={sourceQuantityMoved}->{targetQuantityReceived}; "
                    + $"mass={unitMass * sourceQuantityMoved}->{unitMass * targetQuantityReceived}; "
                    + $"sourceReserved={restoredWarehouse.Inventory.ReservedInboundMassGrams}; "
                    + $"targetReserved={restoredTargetWarehouse.Inventory.ReservedInboundMassGrams}; "
                    + $"pending={string.Join(",", evacuation.CapturePendingWarehouseIds())}");
            }
        }
        finally
        {
            DungeonGameRestoreReport cleanup = new();
            bool cleaned = registry.RestoreAll(baseline, cleanup)
                && cleanup.Success;
            cleanupErrors = string.Join(" | ", cleanup.Errors);
            Check(cleaned
                    && !evacuation.IsPending(destinationId),
                "WAREHOUSE_RESTORE_EVACUATION_CLEANUP_EXACT",
                $"restored={cleaned}; pending={string.Join(",", evacuation.CapturePendingWarehouseIds())}; errors={cleanupErrors}");
        }
    }

    private static DungeonPhysicalItemSaveData ClonePhysicalSnapshot(
        DungeonPhysicalItemSaveData snapshot) =>
        JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
            JsonUtility.ToJson(snapshot));

    private static long CalculateStoredMass(
        DungeonPhysicalItemSaveData snapshot,
        IWorldItemStackRuntime runtime,
        string storageDestination)
    {
        long total = 0L;
        foreach (WorldItemStackSaveData stack in snapshot.stacks.Where(
                     stack => stack != null
                         && stack.quantity > 0
                         && stack.state == WorldItemStackState.Stored
                         && string.Equals(
                             string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                                 ? stack.destinationId
                                 : stack.sourceStorageDestinationId,
                             storageDestination,
                             StringComparison.Ordinal)))
        {
            total = checked(total + runtime.MassQuery
                .GetDefinitionUnitMass((ItemDefinitionId)stack.itemId)
                .Multiply(stack.quantity)
                .Value);
        }
        return total;
    }

    private static void SortPhysicalStacks(DungeonPhysicalItemSaveData snapshot)
    {
        snapshot.stacks = snapshot.stacks
            .OrderBy(stack => stack.gridY)
            .ThenBy(stack => stack.gridX)
            .ThenBy(stack => stack.itemId, StringComparer.Ordinal)
            .ThenBy(stack => stack.stackId, StringComparer.Ordinal)
            .ToList();
    }

    private sealed class StaticRestoreWorldCandidates :
        IRestoreWorldCandidateQuery
    {
        private readonly IReadOnlyList<BuildableObject> buildings;

        internal StaticRestoreWorldCandidates(params BuildableObject[] buildings)
        {
            this.buildings = (buildings ?? Array.Empty<BuildableObject>())
                .Where(building => building != null)
                .ToArray();
        }

        public int Revision => 1;
        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
        public bool TryGetBuildings(out IReadOnlyList<BuildableObject> value)
        {
            value = buildings;
            return true;
        }
        public bool TryGetCharacters(out IReadOnlyList<CharacterActor> characters)
        {
            characters = null;
            return false;
        }
        public bool TryGetWildlife(out IReadOnlyList<WildlifeActor> wildlife)
        {
            wildlife = null;
            return false;
        }
        public bool TryGetExteriorZones(out IReadOnlyList<ExteriorZoneMarker> zones)
        {
            zones = null;
            return false;
        }
    }

    private IEnumerator VerifyLooseStackToWarehouse(
        IWorldItemStackRuntime itemRuntime,
        Grid grid,
        CharacterActor hauler,
        Facility warehouse,
        Vector2Int itemPosition)
    {
        int before = GetTotalWarehouseStock(StockCategory.Food);
        int targetBefore = warehouse.Inventory.GetStock(StockCategory.Food);
        bool spawned = itemRuntime.SpawnItemAt(
            PreservedRationItemId,
            3,
            itemPosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int amount);
        Check(spawned && amount == 3, "LOOSE_STACK_SPAWNED", $"pos={itemPosition}; amount={amount}");
        WorldItemStackSnapshot looseTarget = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && stack.Position == itemPosition
                && string.Equals(
                    stack.ItemId,
                    PreservedRationItemId,
                    StringComparison.Ordinal))
            .OrderByDescending(stack => stack.Quantity)
            .FirstOrDefault();
        Check(looseTarget != null
                && itemRuntime.PrioritizeHaul(looseTarget.StackId),
            "LOOSE_STACK_PRIORITIZED",
            looseTarget != null ? looseTarget.StackId : "missing loose target");

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            Check(action.CanStart(hauler), "AI_HAUL_CAN_START_WAREHOUSE", DescribeHaulState(itemRuntime, hauler));
            yield return RunHaul(action, hauler, () =>
                GetTotalWarehouseStock(StockCategory.Food) >= before + 3
                && !itemRuntime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.ItemId, PreservedRationItemId, StringComparison.Ordinal)));
        }
        finally
        {
            Destroy(action);
        }

        int after = GetTotalWarehouseStock(StockCategory.Food);
        int targetAfter = warehouse.Inventory.GetStock(StockCategory.Food);
        Check(after == before + 3,
            "AI_HAUL_DEPOSITED_TO_WAREHOUSE",
            $"totalFood={before}->{after}; testWarehouseFood={targetBefore}->{targetAfter}; carry={DescribeCarry(hauler, itemRuntime)}");
    }

    private IEnumerator VerifyFacilityInputDelivery(
        IWorldItemStackRuntime itemRuntime,
        CharacterActor hauler,
        Facility warehouse,
        Facility bench)
    {
        string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + "qa-logistics-input";
        int generalBefore = warehouse.Inventory.GetStock(StockCategory.General);
        bool requested = itemRuntime.TryRequestFacilityDelivery(
            StockCategory.General,
            2,
            bench.centerPos,
            destinationId,
            out int requestedAmount,
            out string reason);
        Check(requested && requestedAmount == 2,
            "FACILITY_DELIVERY_REQUESTED",
            $"requested={requestedAmount}; reason={reason}; general={generalBefore}->{warehouse.Inventory.GetStock(StockCategory.General)}");
        Check(warehouse.Inventory.GetStock(StockCategory.General) == generalBefore,
            "FACILITY_STOCK_HELD_UNTIL_PICKUP",
            $"general={generalBefore}->{warehouse.Inventory.GetStock(StockCategory.General)}");
        Check(!itemRuntime.GetAllStacks().Any(stack =>
                stack.State == WorldItemStackState.Loose
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)),
            "FACILITY_REQUEST_NO_LOOSE_PILE",
            DescribeStacks(itemRuntime));
        Check(itemRuntime.GetAllStacks().Any(stack =>
                stack.State == WorldItemStackState.Stored
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId)),
            "FACILITY_REQUEST_RESERVED_IN_STORAGE",
            DescribeStacks(itemRuntime));

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            Check(action.CanStart(hauler), "AI_HAUL_CAN_START_FACILITY", DescribeHaulState(itemRuntime, hauler));
            yield return RunHaul(action, hauler, () => itemRuntime.GetAllStacks()
                .Where(stack =>
                    stack.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity) >= 2);
        }
        finally
        {
            Destroy(action);
        }

        bool bufferReady = itemRuntime.GetAllStacks().Any(stack =>
            stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
            && stack.Position == bench.centerPos);
        Check(bufferReady, "AI_HAUL_DEPOSITED_TO_FACILITY_BUFFER", DescribeStacks(itemRuntime));
        Check(warehouse.Inventory.GetStock(StockCategory.General) == generalBefore - 2,
            "FACILITY_STOCK_WITHDRAWN_ON_PICKUP",
            $"general={generalBefore}->{warehouse.Inventory.GetStock(StockCategory.General)}");
        Check(itemRuntime.TryConsumeFacilityBuffer(
                destinationId,
                new Dictionary<StockCategory, int> { [StockCategory.General] = 2 },
                out string consumeReason),
            "FACILITY_BUFFER_CONSUMED",
            consumeReason);
    }

    private IEnumerator VerifyConstructionMaterialDelivery(
        IWorldItemStackRuntime itemRuntime,
        IWorkOrderRuntime workOrderRuntime,
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        CharacterActor hauler,
        Facility warehouse,
        Vector2Int sitePosition)
    {
        const int materialAmount = 2;
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = 99121;
        building.objectName = "QA 건설 자재 운반 시설";
        building.width = 1;
        building.height = 1;
        building.layer = GridLayer.Building;
        building.category = BuildingCategory.Shop;
        building.unlocked = true;
        BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
        {
            constructionWorkRequired = 5f,
            repairWorkRequired = 3f,
            cleanWorkRequired = 2f,
            researchWorkRequired = 6f
        };
        workAmount.SetConstructionProjectScale(ProjectScale.IndustrialFacility);
        workAmount.SetConstructionMaterials(new[]
        {
            new ItemAmountDefinition("material:lumber", materialAmount)
        });
        building.AbilityModules.Add(workAmount);

        GameObject siteObject = new GameObject("QA_Physical_Logistics_ConstructionSite");
        temporaryObjects.Add(siteObject);
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        InjectGameObject(scope, siteObject);
        site.SetGrid(grid);
        site.Initialization(building, sitePosition);
        siteObject.transform.position = grid.GetWorldPos(sitePosition);
        bool registered = grid.RegisterOccupant(
            site,
            GridLayer.Construction,
            building.GetGridPosList(sitePosition),
            false);
        Check(registered, "CONSTRUCTION_SITE_REGISTERED", $"pos={sitePosition}");

        string orderId = string.Empty;
        List<ProjectWorkerLease> projectWorkerLeases = new List<ProjectWorkerLease>();
        try
        {
            const string materialItemId = "material:lumber";
            int materialBefore = GetStoredItemQuantity(
                itemRuntime,
                materialItemId,
                warehouse.centerPos);
            string failureReason = string.Empty;
            bool created = registered
                && workOrderRuntime.TryCreateConstructionOrder(
                    site,
                    building,
                    sitePosition,
                    out orderId,
                    out failureReason);
            Check(created,
                "CONSTRUCTION_ORDER_CREATED",
                created ? $"order={orderId}" : failureReason);
            if (created)
            {
                site.ConfigureSite(orderId, () => true, () => { });
                Check(string.Equals(
                        site.WorkOrderId,
                        orderId,
                        StringComparison.Ordinal),
                    "CONSTRUCTION_SITE_ORDER_AUTHORITY_PUBLISHED",
                    $"siteOrder={site.WorkOrderId}; order={orderId}");
            }
            if (!created
                || !workOrderRuntime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order))
            {
                yield break;
            }

            string destinationId = order.MaterialDestinationId;
            Check(order.Status == WorkOrderStatus.WaitingForMaterials,
                "CONSTRUCTION_WAITS_FOR_MATERIALS",
                $"status={order.Status}; destination={destinationId}");
            Check(GetStoredItemQuantity(itemRuntime, materialItemId, warehouse.centerPos) == materialBefore,
                "CONSTRUCTION_STOCK_HELD_UNTIL_PICKUP",
                $"item={materialItemId}; quantity={materialBefore}->{GetStoredItemQuantity(itemRuntime, materialItemId, warehouse.centerPos)}");
            Check(!itemRuntime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)),
                "CONSTRUCTION_REQUEST_NO_LOOSE_PILE",
                DescribeStacks(itemRuntime));
            Check(itemRuntime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Stored
                    && string.Equals(stack.ItemId, materialItemId, StringComparison.Ordinal)
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId)
                    && stack.Quantity >= materialAmount),
                "CONSTRUCTION_MATERIAL_RESERVED_IN_STORAGE",
                DescribeStacks(itemRuntime));

            AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
            try
            {
                Check(action.CanStart(hauler),
                    "AI_HAUL_CAN_START_CONSTRUCTION",
                    DescribeHaulState(itemRuntime, hauler));
                yield return RunHaul(action, hauler, () =>
                    itemRuntime.GetAllStacks().Any(stack =>
                        stack.State == WorldItemStackState.FacilityBuffer
                        && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                        && stack.Position == sitePosition
                        && stack.Quantity >= materialAmount)
                    || workOrderRuntime.TryGetOrderFor(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        out WorkOrderProgressState deliveredOrder)
                    && deliveredOrder.DeliveredItemMaterials.TryGetValue(
                        "material:lumber",
                        out int deliveredAmount)
                    && deliveredAmount >= materialAmount);
            }
            finally
            {
                Destroy(action);
            }

            Check(GetStoredItemQuantity(itemRuntime, materialItemId, warehouse.centerPos)
                    == materialBefore - materialAmount,
                "CONSTRUCTION_STOCK_WITHDRAWN_ON_PICKUP",
                $"item={materialItemId}; quantity={materialBefore}->{GetStoredItemQuantity(itemRuntime, materialItemId, warehouse.centerPos)}");
            Check(workOrderRuntime.RefreshMaterialsReady(site)
                    && workOrderRuntime.TryGetOrderFor(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        out order)
                    && order.Status == WorkOrderStatus.Ready
                    && order.DeliveredItemMaterials.TryGetValue("material:lumber", out int delivered)
                    && delivered == materialAmount,
                "CONSTRUCTION_READY_AFTER_PHYSICAL_DELIVERY",
                order != null
                    ? $"status={order.Status}; delivered={order.DeliveredItemMaterials.GetValueOrDefault("material:lumber")}"
                    : "order missing");

            VerifyLiveConstructionProjectContribution(
                workOrderRuntime,
                site,
                hauler,
                projectWorkerLeases);
        }
        finally
        {
            for (int index = projectWorkerLeases.Count - 1; index >= 0; index--)
            {
                projectWorkerLeases[index]?.Dispose();
            }

            if (!string.IsNullOrWhiteSpace(orderId))
            {
                workOrderRuntime.CancelOrder(orderId, refundDeliveredMaterials: false);
            }

            if (registered)
            {
                grid.RemoveOccupant(
                    site,
                    GridLayer.Construction,
                    building.GetGridPosList(sitePosition),
                    false);
            }

            Destroy(building);
        }
    }

    private void VerifyLiveConstructionProjectContribution(
        IWorkOrderRuntime workOrderRuntime,
        ConstructionSite site,
        CharacterActor preferredWorker,
        List<ProjectWorkerLease> leases)
    {
        IConstructionProjectWorkforceRuntime workforce =
            workOrderRuntime as IConstructionProjectWorkforceRuntime;
        Check(workforce != null,
            "CONSTRUCTION_PROJECT_WORKFORCE_READY",
            workforce != null ? "runtime resolved" : "runtime missing");
        Check(site != null && site.MaximumWorkers == 4,
            "CONSTRUCTION_INDUSTRIAL_WORKER_CAP",
            site != null ? $"maximum={site.MaximumWorkers}" : "site missing");
        if (workforce == null || site == null)
        {
            return;
        }

        CharacterActor[] candidates = verificationActors
            .Where(actor => actor != null
                && !actor.IsDead
                && CharacterPersistentIdentity.TryGet(actor, out _)
                && (actor.TryGetAbility(out AbilityWork _)
                    || actor.Identity != null && actor.Identity.Role == CharacterRole.Owner))
            .OrderBy(actor => ReferenceEquals(actor, preferredWorker) ? 0 : 1)
            .ThenBy(actor => actor.Identity?.PersistentId, StringComparer.Ordinal)
            .Take(site.MaximumWorkers)
            .ToArray();
        Check(candidates.Length >= 3,
            "CONSTRUCTION_LIVE_WORKER_SAMPLE",
            $"workers={candidates.Length}; required>=3; maximum={site.MaximumWorkers}");
        if (candidates.Length == 0)
        {
            return;
        }

        for (int index = 0; index < candidates.Length; index++)
        {
            bool joined = workforce.TryJoinConstructionProject(
                site,
                candidates[index],
                out ProjectWorkerLease lease,
                out string failureReason);
            Check(joined,
                $"CONSTRUCTION_PROJECT_WORKER_{index + 1}_JOINED",
                joined
                    ? $"worker={candidates[index].Identity?.PersistentId}"
                    : failureReason);
            if (!joined)
            {
                continue;
            }

            leases.Add(lease);
            Check(workforce.UpdateConstructionWorkerRate(site, candidates[index], 1f),
                $"CONSTRUCTION_PROJECT_WORKER_{index + 1}_RATE",
                "authored=1.00 WU/s");
        }

        int joinedCount = leases.Count;
        float expectedEffectiveWorkers = 0f;
        for (int index = 0; index < joinedCount; index++)
        {
            expectedEffectiveWorkers += SettlementLaborBalanceRules.GetWorkerContribution(
                ProjectScale.IndustrialFacility,
                index);
        }

        bool captured = workforce.TryCaptureConstructionProject(site, out ProjectWorkforceSnapshot snapshot);
        Check(captured
                && snapshot.ActiveWorkers == joinedCount
                && snapshot.MaximumWorkers == 4
                && snapshot.DefaultAutomaticWorkerLimit == 4
                && Mathf.Abs(snapshot.EffectiveWorkerCount - expectedEffectiveWorkers) <= 0.0001f
                && Mathf.Abs(snapshot.EffectiveWuPerSecond - expectedEffectiveWorkers) <= 0.0001f,
            "CONSTRUCTION_PROJECT_LIVE_SNAPSHOT",
            captured
                ? $"active={snapshot.ActiveWorkers}; maximum={snapshot.MaximumWorkers}; automatic={snapshot.DefaultAutomaticWorkerLimit}; effectiveWorkers={snapshot.EffectiveWorkerCount:0.00}; effectiveRate={snapshot.EffectiveWuPerSecond:0.00}"
                : "snapshot unavailable");

        if (!workOrderRuntime.TryGetOrderFor(
                site,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState before))
        {
            Check(false, "CONSTRUCTION_PROJECT_PROGRESS_BASELINE", "order missing");
            return;
        }

        float expectedAcceptedWork = 0f;
        for (int index = 0; index < joinedCount; index++)
        {
            float multiplier = workforce.GetConstructionContributionMultiplier(
                site,
                candidates[index]);
            expectedAcceptedWork += multiplier;
            bool applied = workOrderRuntime.ApplyWork(
                candidates[index],
                site,
                BuiltInWorkTypeIds.Construct,
                multiplier,
                out bool completed,
                out _,
                out string message);
            Check(applied && !completed,
                $"CONSTRUCTION_PROJECT_WORKER_{index + 1}_PROGRESS",
                $"multiplier={multiplier:0.00}; completed={completed}; message={message}");
        }

        bool progressCaptured = workOrderRuntime.TryGetOrderFor(
            site,
            BuiltInWorkTypeIds.Construct,
            out WorkOrderProgressState after);
        float actualAcceptedWork = progressCaptured
            ? after.CompletedWork - before.CompletedWork
            : float.NaN;
        Check(progressCaptured
                && Mathf.Abs(actualAcceptedWork - expectedAcceptedWork) <= 0.001f,
            "CONSTRUCTION_PROJECT_DIMINISHING_PROGRESS_APPLIED",
            progressCaptured
                ? $"rawWorkers={joinedCount}; accepted={actualAcceptedWork:0.00}; expected={expectedAcceptedWork:0.00}"
                : "order missing after progress");
    }

    private IEnumerator VerifyCraftMaterialsOutputAndEquipmentDeposit(
        IWorldItemStackRuntime itemRuntime,
        ICombatEquipmentRuntime equipment,
        CharacterActor hauler,
        Facility warehouse,
        Facility bench)
    {
        int inventoryBefore = equipment.GetAvailableCount(DaggerId);
        Check(equipment.TryQueueCraft(DaggerId, bench, out string queueMessage),
            "CRAFT_QUEUE_REQUESTED_PHYSICAL_MATERIALS",
            queueMessage);

        CombatEquipmentCraftOrderSaveData order = equipment.CraftQueue
            .FirstOrDefault(item => item != null
                && string.Equals(item.definitionId, DaggerId, StringComparison.Ordinal)
                && !item.materialsReady);
        Check(order != null
                && string.Equals(order.materialId, "material:iron", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(order.materialDestinationId)
                && itemRuntime.GetAllStacks().Any(stack =>
                    string.Equals(stack.ItemId, "material:iron-ingot", StringComparison.Ordinal)
                    &&
                    stack.HasDestinationPosition
                    && string.Equals(stack.DestinationId, order.materialDestinationId, StringComparison.Ordinal)),
            "CRAFT_MATERIAL_STACK_CREATED",
            order != null ? $"destination={order.materialDestinationId}" : "missing order");

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            Check(action.CanStart(hauler), "AI_HAUL_CAN_START_CRAFT_MATERIALS", DescribeHaulState(itemRuntime, hauler));
            yield return RunHaul(action, hauler, () => order != null && itemRuntime.GetAllStacks().Any(stack =>
                stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, order.materialDestinationId, StringComparison.Ordinal)));
        }
        finally
        {
            Destroy(action);
        }

        Check(equipment.HasPendingCraftWork(new[] { DaggerId }),
            "CRAFT_MATERIALS_READY_AFTER_HAUL",
            order != null ? $"ready={order.materialsReady}" : "missing order");

        int guard = 0;
        while (equipment.CraftQueue.Any(item => item != null
                   && string.Equals(item.definitionId, DaggerId, StringComparison.Ordinal))
               && guard++ < 40)
        {
            ModularFacilityRuntimeEffects.ApplyWorkCompleted(
                hauler.BuildingVisitor,
                bench,
                BuiltInWorkTypeIds.Craft);
            yield return null;
        }

        string outputItemId = PhysicalItemIds.ForEquipment(DaggerId);
        WorldItemStackSnapshot output = itemRuntime.GetAllStacks().FirstOrDefault(stack =>
            stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(stack.ItemId, outputItemId, StringComparison.Ordinal));
        Check(output != null,
            "CRAFT_OUTPUT_WORLD_STACK_CREATED",
            output != null ? $"stack={output.StackId}; pos={output.Position}" : "missing output stack");
        Check(output != null
                && equipment.TryGetInstanceBySourceStack(
                    output.StackId,
                    out CombatEquipmentInstance crafted)
                && string.Equals(
                    crafted.materialId,
                    "material:iron",
                    StringComparison.Ordinal),
            "CRAFT_OUTPUT_RETAINED_SELECTED_MATERIAL",
            output != null ? $"stack={output.StackId}" : "missing output stack");

        action = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            Check(action.CanStart(hauler), "AI_HAUL_CAN_START_CRAFT_OUTPUT", DescribeHaulState(itemRuntime, hauler));
            yield return RunHaul(action, hauler, () => equipment.GetAvailableCount(DaggerId) >= inventoryBefore + 1);
        }
        finally
        {
            Destroy(action);
        }

        int inventoryAfter = equipment.GetAvailableCount(DaggerId);
        Check(inventoryAfter == inventoryBefore + 1,
            "AI_HAUL_DEPOSITED_EQUIPMENT_TO_INVENTORY",
            $"Dagger={inventoryBefore}->{inventoryAfter}; warehouseWeapon={warehouse.Inventory.GetStock(StockCategory.Weapon)}");

        try
        {
            WorldItemStackSnapshot deposited = output == null
                ? null
                : itemRuntime.GetAllStacks().FirstOrDefault(stack =>
                    stack != null
                    && string.Equals(
                        stack.StackId,
                        output.StackId,
                        StringComparison.Ordinal));
            IPhysicalItemMassQuery massQuery =
                Resolve<IPhysicalItemMassQuery>(FindScope());
            PhysicalItemMassSubject subject = deposited == null
                ? default
                : PhysicalItemMassSubjectAdapter.Create(
                    massQuery,
                    (ItemDefinitionId)deposited.ItemId,
                    deposited.ItemInstanceId,
                    deposited.Components);
            long projectedMass = deposited == null || massQuery == null
                ? 0L
                : massQuery.GetStackUnitMass(
                    (ItemDefinitionId)deposited.ItemId,
                    subject).Value;
            long baseMass = massQuery?.GetDefinitionUnitMass(
                (ItemDefinitionId)outputItemId).Value ?? 0L;
            Check(deposited != null
                    && deposited.State == WorldItemStackState.Stored
                    && projectedMass == baseMass
                    && Mathf.Approximately(
                        deposited.UnitWeight,
                        projectedMass / 1000f)
                    && warehouse.Inventory.ReservedInboundMassGrams == 0L,
                "COMBAT_EQUIPMENT_STATEFUL_WAREHOUSE_MASS_EXACT",
                $"stack={deposited?.StackId}; state={deposited?.State}; "
                + $"projected={projectedMass}; base={baseMass}; "
                + $"unitKg={deposited?.UnitWeight}; reserved={warehouse.Inventory.ReservedInboundMassGrams}");
        }
        catch (Exception exception)
        {
            Check(false,
                "COMBAT_EQUIPMENT_STATEFUL_WAREHOUSE_MASS_EXACT",
                exception.Message);
        }
    }

    private IEnumerator VerifyL02MassAdmissionAndPickupRejection(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        IWarehouseMassAdmissionService warehouseMassAdmission,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        Grid grid,
        CharacterActor hauler)
    {
        BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/L02_상자더미.asset");
        IReadOnlyList<Vector2Int> candidates = FindReachableCells(
            grid,
            hauler.GetNowXY(),
            96);
        bool cellFound = TryFindRegisterablePosition(
            grid,
            asset,
            candidates,
            out Vector2Int position);
        Facility l02 = cellFound
            ? CreateInjectedFacility(
                scope,
                grid,
                asset,
                position,
                "QA_L02_Mass_Warehouse",
                registerOnGrid: true)
            : null;
        Check(l02?.Inventory?.HasMassCapacityAuthority == true
                && l02.Inventory.MaxMassGrams == 12_500L
                && l02.Inventory.MaxCapacity == 16,
            "L02_PLAYMODE_MASS_AUTHORITY_READY",
            l02 == null
                ? $"cellFound={cellFound}; position={position}"
                : $"id={l02.PersistentInstanceId.Value}; max={l02.Inventory.MaxMassGrams}; legacy={l02.Inventory.MaxCapacity}");
        if (l02?.Inventory?.HasMassCapacityAuthority != true)
        {
            yield break;
        }

        WorldItemWarehouseService warehouseService =
            Resolve<WorldItemWarehouseService>(scope);
        Check(warehouseService != null,
            "L02_PLAYMODE_EXACT_INGRESS_SERVICE_READY",
            warehouseService != null ? "resolved" : "missing");
        if (warehouseService == null)
        {
            yield break;
        }

        string ingressOperationId =
            "qa:l02-playmode:inoculated-log:ingress";
        bool stored = warehouseService.SpawnItemStock(
            l02,
            InoculatedLogItemId,
            17,
            ingressOperationId,
            "generic:supply:inoculated-log",
            out int spawned,
            out WarehouseMassAdmissionReceipt receipt,
            out DomainFailure ingressFailure);
        Check(stored
                && spawned == 17
                && receipt.CommittedQuantity == 17
                && receipt.CommittedMassGrams == 11_900L
                && l02.Inventory.StoredMassGrams == 11_900L
                && l02.Inventory.RemainingMassGrams == 600L
                && l02.Inventory.GetAcceptableQuantity(
                    InoculatedLogItemId,
                    1) == 0,
            "L02_PLAYMODE_17X700G_INGRESS_EXACT",
            $"stored={stored}; spawned={spawned}; committed={receipt.CommittedQuantity}x{receipt.CommittedMassGrams}g; "
            + $"mass={l02.Inventory.StoredMassGrams}/{l02.Inventory.MaxMassGrams}; remaining={l02.Inventory.RemainingMassGrams}; failure={ingressFailure.Code}");
        if (!stored)
        {
            yield break;
        }

        string destinationId = WarehouseStorageIdentity.RequireDestinationId(l02);
        DungeonPhysicalItemSaveData checkpoint = runtime.Capture();
        string[] storedStackIds = checkpoint.stacks
            .Where(stack => stack != null
                && stack.state == WorldItemStackState.Stored
                && string.Equals(
                    string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                        ? stack.destinationId
                        : stack.sourceStorageDestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.itemId,
                    InoculatedLogItemId,
                    StringComparison.Ordinal))
            .Select(stack => stack.stackId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        runtime.Restore(checkpoint);
        yield return null;
        WorldItemStackSnapshot[] restoredStacks = runtime.GetAllStacks()
            .Where(stack => stack != null
                && storedStackIds.Contains(stack.StackId, StringComparer.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        Check(restoredStacks.Length == storedStackIds.Length
                && restoredStacks.Sum(stack => stack.Quantity) == 17
                && restoredStacks.All(stack => stack.State == WorldItemStackState.Stored
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                && l02.Inventory.StoredMassGrams == 11_900L
                && l02.Inventory.RemainingMassGrams == 600L,
            "L02_PLAYMODE_CURRENT_FORMAT_RESTORE_EXACT",
            $"ids={storedStackIds.Length}->{restoredStacks.Length}; quantity={restoredStacks.Sum(stack => stack.Quantity)}; "
            + $"mass={l02.Inventory.StoredMassGrams}; remaining={l02.Inventory.RemainingMassGrams}");

        Vector2Int loosePosition = candidates.FirstOrDefault(candidate =>
            grid.GetGridCell(candidate)?.CanOccupy(GridLayer.FloorOverlay) == true
            && candidate != position);
        bool looseSpawned = runtime.SpawnItemAt(
            InoculatedLogItemId,
            1,
            loosePosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int looseAmount);
        WorldItemStackSnapshot loose = runtime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && stack.Position == loosePosition
                && string.Equals(
                    stack.ItemId,
                    InoculatedLogItemId,
                    StringComparison.Ordinal));
        Check(looseSpawned && looseAmount == 1 && loose != null,
            "L02_PLAYMODE_OVERFILL_LOOSE_STACK_READY",
            $"spawned={looseSpawned}; amount={looseAmount}; position={loosePosition}; stack={loose?.StackId ?? "missing"}");
        if (loose == null)
        {
            yield break;
        }
        runtime.PrioritizeHaul(loose.StackId);

        ICharacterAiWorldRegistry liveWorld =
            Resolve<ICharacterAiWorldRegistry>(scope);
        IGridSystemProvider gridProvider = Resolve<IGridSystemProvider>(scope);
        IDungeonItemCatalogProvider catalog =
            Resolve<IDungeonItemCatalogProvider>(scope);
        IPhysicalItemMassQuery massQuery =
            Resolve<IPhysicalItemMassQuery>(scope);
        IItemHaulingSettingsProvider haulingSettings =
            Resolve<IItemHaulingSettingsProvider>(scope);
        ICharacterIdRegistry characterIds =
            Resolve<ICharacterIdRegistry>(scope);
        WorldItemRepository repository = Resolve<WorldItemRepository>(scope);
        IItemQuantityReservationService reservations =
            Resolve<IItemQuantityReservationService>(scope);
        IGridPathSearchBroker pathBroker = hauler.PathSearchBroker;
        bool dependenciesReady = liveWorld != null
            && gridProvider != null
            && catalog != null
            && massQuery != null
            && haulingSettings != null
            && characterIds != null
            && repository != null
            && reservations != null
            && destinationClaims != null
            && warehouseMassAdmission != null
            && pathBroker != null;
        Check(dependenciesReady,
            "L02_PLAYMODE_ISOLATED_PRODUCTION_PLANNER_READY",
            $"world={liveWorld != null}; grid={gridProvider != null}; catalog={catalog != null}; mass={massQuery != null}; "
            + $"settings={haulingSettings != null}; ids={characterIds != null}; repository={repository != null}; "
            + $"reservations={reservations != null}; claims={destinationClaims != null}; admission={warehouseMassAdmission != null}; path={pathBroker != null}");
        if (!dependenciesReady)
        {
            yield break;
        }

        WorldItemHaulPlanningService isolatedPlanner = new(
            gridProvider,
            catalog,
            massQuery,
            haulingSettings,
            characterIds,
            pathBroker,
            new SingleWarehouseWorldRegistry(liveWorld, l02),
            repository,
            reservations,
            destinationClaims,
            warehouseMassAdmission);
        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(hauler);
        int carryBefore = carry?.Items.Count ?? 0;
        bool previewed = isolatedPlanner.TryPreviewBestPlan(
            hauler,
            out _,
            out string previewFailure);
        bool reserved = isolatedPlanner.TryReserveBestPlan(
            hauler,
            out _,
            out string reserveFailure);
        WorldItemStackSnapshot after = runtime.GetAllStacks()
            .FirstOrDefault(stack => string.Equals(
                stack.StackId,
                loose.StackId,
                StringComparison.Ordinal));
        Check(!previewed
                && !reserved
                && after != null
                && after.State == WorldItemStackState.Loose
                && after.Quantity == 1
                && after.ReservedQuantity == 0
                && (carry?.Items.Count ?? 0) == carryBefore
                && l02.Inventory.ReservedInboundMassGrams == 0L
                && l02.Inventory.StoredMassGrams == 11_900L,
            "L02_PLAYMODE_OVERFILL_REJECTED_BEFORE_PICKUP",
            $"preview={previewed}:{previewFailure}; reserve={reserved}:{reserveFailure}; "
            + $"stack={after?.State.ToString() ?? "missing"}x{after?.Quantity ?? 0}; reservedQuantity={after?.ReservedQuantity ?? -1}; "
            + $"carry={carryBefore}->{carry?.Items.Count ?? 0}; inbound={l02.Inventory.ReservedInboundMassGrams}; stored={l02.Inventory.StoredMassGrams}");
    }

    private IEnumerator VerifyProductionInputBufferMassAdmission(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Grid grid,
        CharacterActor hauler)
    {
        const string treatedLumber = "material:treated-lumber";
        const string caveMushroom = "resource:cave-mushroom";
        const string destination =
            "production:production-bill:qa-input-buffer-mass";
        const long capacityGrams = 4_200L;

        IProductionItemGateway gateway = Resolve<IProductionItemGateway>(scope);
        IFacilityBufferDestinationLifecycleCommand lifecycle =
            Resolve<IFacilityBufferDestinationLifecycleCommand>(scope);
        IFacilityBufferMassCapacityQuery capacities =
            Resolve<IFacilityBufferMassCapacityQuery>(scope);
        IFacilityBufferDestinationClaimAuthorityQuery claimAuthority =
            Resolve<IFacilityBufferDestinationClaimAuthorityQuery>(scope);
        IFacilityBufferMassCapacityAuthorityQuery capacityAuthority =
            Resolve<IFacilityBufferMassCapacityAuthorityQuery>(scope);
        IReadOnlyList<Vector2Int> positions = FindReachableCells(
            grid,
            hauler.GetNowXY(),
            48);
        Vector2Int sourcePosition = positions.FirstOrDefault();
        Vector2Int destinationPosition = positions
            .FirstOrDefault(value => value != sourcePosition);
        BuildingSO productionAsset = FindBuildingAsset(asset =>
            asset?.Facility != null
            && asset.GetAbility<BuildingProductionWorkstationAbility>() != null);
        Facility productionFacility = CreateInjectedFacility(
            scope,
            grid,
            productionAsset,
            destinationPosition,
            "QA_Production_Input_Buffer_Facility");
        string facilityId = productionFacility?.PersistentInstanceId.IsValid == true
            ? productionFacility.PersistentInstanceId.Value
            : string.Empty;
        Check(gateway != null
                && lifecycle != null
                && capacities != null
                && claimAuthority != null
                && capacityAuthority != null
                && productionFacility != null
                && facilityId.Length > 0
                && positions.Count >= 2,
            "PRODUCTION_INPUT_BUFFER_MASS_RUNTIME_READY",
            $"gateway={gateway != null}; lifecycle={lifecycle != null}; "
            + $"capacities={capacities != null}; "
            + $"authority={claimAuthority != null}/{capacityAuthority != null}; "
            + $"facility={facilityId}; "
            + $"positions={positions.Count}");
        if (gateway == null
            || lifecycle == null
            || capacities == null
            || claimAuthority == null
            || capacityAuthority == null
            || productionFacility == null
            || facilityId.Length == 0
            || positions.Count < 2)
        {
            yield break;
        }

        destinationPosition = productionFacility.centerPos;

        FacilityBufferDestinationClaim destinationClaim = new(
            destination,
            destinationPosition,
            ProductionInputDestinationClaimRuntime.OwnerDomain,
            "production-bill:qa-input-buffer-mass",
            facilityId,
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile capacityProfile = new(
            destination,
            destinationPosition,
            ProductionInputDestinationClaimRuntime.OwnerDomain,
            "production-bill:qa-input-buffer-mass",
            facilityId,
            new PhysicalMassGrams(capacityGrams),
            ProductionInputDestinationClaimRuntime
                .InputBufferCapacitySchemaRevision);
        FacilityBufferDestinationClaim[] previousClaims = claimAuthority
            .CaptureAuthorityClaims()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ProductionInputDestinationClaimRuntime.OwnerDomain,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] previousProfiles = capacityAuthority
            .CaptureAuthorityProfiles()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ProductionInputDestinationClaimRuntime.OwnerDomain,
                StringComparison.Ordinal))
            .ToArray();
        bool claimed = lifecycle.TryReplaceOwnedAuthorities(
            ProductionInputDestinationClaimRuntime.OwnerDomain,
            previousClaims.Append(destinationClaim).ToArray(),
            previousProfiles.Append(capacityProfile).ToArray(),
            out string claimFailure);
        Check(claimed,
            "PRODUCTION_INPUT_BUFFER_EXACT_AUTHORITY_PUBLISHED",
            $"claimed={claimed}; failure={claimFailure}");
        if (!claimed)
            yield break;

        bool lumberSpawned = runtime.SpawnItemAt(
            treatedLumber,
            4,
            sourcePosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int lumberAmount);
        bool mushroomSpawned = runtime.SpawnItemAt(
            caveMushroom,
            4,
            sourcePosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int mushroomAmount);
        Check(lumberSpawned && lumberAmount == 4
                && mushroomSpawned && mushroomAmount == 4,
            "PRODUCTION_INPUT_BUFFER_MASS_SOURCES_READY",
            $"lumber={lumberSpawned}:{lumberAmount}; mushroom={mushroomSpawned}:{mushroomAmount}");
        if (!lumberSpawned || !mushroomSpawned)
        {
            yield break;
        }

        bool lumberRequested = gateway.RequestDelivery(
            treatedLumber,
            3,
            destinationPosition,
            destination,
            out int requestedLumber,
            out string lumberFailure);
        bool mushroomRequested = gateway.RequestDelivery(
            caveMushroom,
            3,
            destinationPosition,
            destination,
            out int requestedMushroom,
            out string mushroomFailure);
        long fullMass = gateway.CountPendingMassGrams(destination);
        Check(lumberRequested && requestedLumber == 3
                && mushroomRequested && requestedMushroom == 3
                && fullMass == capacityGrams
                && capacities.TryGetCapacity(
                    destination,
                    destinationPosition,
                    out FacilityBufferMassCapacitySnapshot fullCapacity)
                && fullCapacity.ReservedMassGrams == 0L,
            "PRODUCTION_INPUT_BUFFER_EXACT_TOKEN_4200G_ADMITTED",
            $"lumber={lumberRequested}:{requestedLumber}:{lumberFailure}; "
            + $"mushroom={mushroomRequested}:{requestedMushroom}:{mushroomFailure}; mass={fullMass}");

        WorldItemStackSnapshot unboundBefore = runtime.GetAllStacks()
            .Single(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && string.IsNullOrWhiteSpace(stack.DestinationId)
                && string.Equals(stack.ItemId, caveMushroom, StringComparison.Ordinal));
        bool overflowAccepted = gateway.RequestDelivery(
            caveMushroom,
            1,
            destinationPosition,
            destination,
            out int overflowRequested,
            out string overflowFailure);
        WorldItemStackSnapshot unboundAfter = runtime.GetAllStacks()
            .Single(stack => string.Equals(
                stack.StackId,
                unboundBefore.StackId,
                StringComparison.Ordinal));
        Check(!overflowAccepted
                && overflowRequested == 0
                && string.Equals(
                    overflowFailure,
                    FailureCode.ItemTransferRequestFailed.ToString(),
                    StringComparison.Ordinal)
                && unboundAfter.State == WorldItemStackState.Loose
                && string.IsNullOrWhiteSpace(unboundAfter.DestinationId)
                && unboundAfter.Quantity == 1
                && unboundAfter.ReservedQuantity == 0
                && gateway.CountPendingMassGrams(destination) == capacityGrams,
            "PRODUCTION_INPUT_BUFFER_MASS_OVERFLOW_REJECTED_BEFORE_PICKUP",
            $"accepted={overflowAccepted}; requested={overflowRequested}; failure={overflowFailure}; "
            + $"source={unboundAfter.State}:{unboundAfter.Quantity}:{unboundAfter.DestinationId}; "
            + $"reserved={unboundAfter.ReservedQuantity}; mass={gateway.CountPendingMassGrams(destination)}");

        DungeonPhysicalItemSaveData checkpoint = runtime.Capture();
        runtime.Restore(checkpoint);
        yield return null;
        Check(gateway.CountPendingMassGrams(destination) == capacityGrams,
            "PRODUCTION_INPUT_BUFFER_MASS_CURRENT_FORMAT_RESTORE_EXACT",
            $"mass={gateway.CountPendingMassGrams(destination)}");

        foreach (WorldItemStackSnapshot unbound in runtime.GetAllStacks()
                     .Where(stack => stack != null
                         && stack.State == WorldItemStackState.Loose
                         && string.IsNullOrWhiteSpace(stack.DestinationId)))
        {
            runtime.SetForbidden(unbound.StackId, true);
        }

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        HaulDeliveryIntentSaveData committedIntent = null;
        try
        {
            bool canStart = action.CanStart(hauler);
            Check(canStart,
                "PRODUCTION_INPUT_BUFFER_ACTUAL_AIHAUL_CAN_START",
                DescribeHaulState(runtime, hauler));
            if (canStart)
            {
                action.Execute(hauler);
                float startedAt = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - startedAt < HaulTimeoutSeconds)
                {
                    EnsureVerificationTimeScale();
                    committedIntent = runtime
                        .CaptureHaulDeliveryIntentsByDestination(destination)
                        .FirstOrDefault(intent => intent.HasCommittedPickup);
                    if (committedIntent != null)
                        break;
                    yield return null;
                }
            }

            long carriedMass = gateway.CountPendingMassGrams(destination);
            Vector2Int cancellationPosition = hauler.GetNowXY();
            string[] committedStackIds = committedIntent?.commitments
                .Select(commitment => commitment.carriedStackId)
                .ToArray() ?? Array.Empty<string>();
            Check(committedIntent != null && carriedMass == capacityGrams,
                "PRODUCTION_INPUT_BUFFER_PICKUP_MASS_IDENTITY",
                $"intent={committedIntent?.operationId ?? "missing"}; mass={carriedMass}; "
                + DescribeHaulState(runtime, hauler));

            bool releasedAtomically = gateway.TryReleaseDestinationAtomically(
                destination,
                destinationPosition,
                out int released,
                out string releaseFailure);
            WorldItemStackSnapshot[] recoveryDrops = runtime.GetAllStacks()
                .Where(stack => committedStackIds.Contains(
                    stack.StackId,
                    StringComparer.Ordinal))
                .ToArray();
            int totalQuantity = runtime.GetAllStacks()
                .Where(stack => string.Equals(stack.ItemId, treatedLumber, StringComparison.Ordinal)
                    || string.Equals(stack.ItemId, caveMushroom, StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            Check(committedIntent != null
                    && releasedAtomically
                    && gateway.CountPendingMassGrams(destination) == 0L
                    && runtime.CaptureHaulDeliveryIntentsByDestination(destination).Count == 0
                    && recoveryDrops.Length == committedStackIds.Length
                    && recoveryDrops.All(stack => stack.State == WorldItemStackState.Loose
                        && stack.Position == cancellationPosition)
                    && hauler.CarryInventory.Items.All(item => !string.Equals(
                        item.ownerOperationId,
                        committedIntent?.operationId,
                        StringComparison.Ordinal))
                    && totalQuantity == 8,
                "PRODUCTION_INPUT_BUFFER_PICKUP_CANCEL_PHYSICAL_RECOVERY",
                $"released={releasedAtomically}:{released}:{releaseFailure}; "
                + $"mass={gateway.CountPendingMassGrams(destination)}; "
                + $"drops={recoveryDrops.Length}/{committedStackIds.Length}@{cancellationPosition}; "
                + $"quantity={totalQuantity}");

            DungeonPhysicalItemSaveData afterCancel = runtime.Capture();
            runtime.Restore(afterCancel);
            yield return null;
            Check(gateway.CountPendingMassGrams(destination) == 0L
                    && runtime.CaptureHaulDeliveryIntentsByDestination(destination).Count == 0
                    && runtime.GetAllStacks().Where(stack => stack != null)
                        .All(stack => !string.Equals(
                            stack.DestinationId,
                            destination,
                            StringComparison.Ordinal)),
                "PRODUCTION_INPUT_BUFFER_CANCEL_SAVE_RESTORE_NO_ORPHAN",
                $"mass={gateway.CountPendingMassGrams(destination)}; "
                + $"intents={runtime.CaptureHaulDeliveryIntentsByDestination(destination).Count}");

            bool wipLumberSpawned = runtime.SpawnItemAt(
                treatedLumber,
                1,
                destinationPosition,
                WorldItemStackState.FacilityBuffer,
                destination,
                out int wipLumberAmount);
            bool wipMushroomSpawned = runtime.SpawnItemAt(
                caveMushroom,
                1,
                destinationPosition,
                WorldItemStackState.FacilityBuffer,
                destination,
                out int wipMushroomAmount);
            const string wipOperation =
                "production-wip-input:qa-input-buffer-mass:cycle-1";
            bool wipCommitted = gateway.ConsumeDeliveredToWip(
                destination,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [treatedLumber] = 1,
                    [caveMushroom] = 1
                },
                wipOperation,
                out ProductionWipInputReceipt wipReceipt,
                out string wipFailure);
            DungeonPhysicalItemSaveData pendingWipSave = runtime.Capture();
            runtime.Restore(pendingWipSave);
            yield return null;
            bool wipAcknowledged = gateway.AcknowledgeWipInput(
                wipReceipt.CommitId,
                out string acknowledgeFailure);
            int afterWipQuantity = runtime.GetAllStacks()
                .Where(stack => string.Equals(stack.ItemId, treatedLumber, StringComparison.Ordinal)
                    || string.Equals(stack.ItemId, caveMushroom, StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            Check(wipLumberSpawned && wipLumberAmount == 1
                    && wipMushroomSpawned && wipMushroomAmount == 1
                    && wipCommitted
                    && wipReceipt.IsCommitted
                    && wipReceipt.Quantity == 2
                    && wipReceipt.InputMassGrams == 1_400L
                    && gateway.CountPendingMassGrams(destination) == 0L
                    && wipAcknowledged
                    && afterWipQuantity == 8,
                "PRODUCTION_INPUT_BUFFER_WIP_CONSUME_RESTORE_EXACT",
                $"spawn={wipLumberSpawned}:{wipLumberAmount}/{wipMushroomSpawned}:{wipMushroomAmount}; "
                + $"commit={wipCommitted}:{wipReceipt.CommitId}:{wipReceipt.Quantity}:{wipReceipt.InputMassGrams}:{wipFailure}; "
                + $"ack={wipAcknowledged}:{acknowledgeFailure}; mass={gateway.CountPendingMassGrams(destination)}; "
                + $"quantity={afterWipQuantity}");
        }
        finally
        {
            Destroy(action);
            if (!lifecycle.TryReplaceOwnedAuthorities(
                    ProductionInputDestinationClaimRuntime.OwnerDomain,
                    previousClaims,
                    previousProfiles,
                    out string cleanupFailure))
            {
                Debug.LogError(
                    "Production input-buffer verifier authority cleanup failed: "
                    + cleanupFailure);
            }
        }
    }

    private IEnumerator VerifyPreparedOutputWarehouseLiveRoute(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Grid grid,
        CharacterActor hauler,
        IWarehouseMassAdmissionService warehouseMassAdmission)
    {
        const string recipeId = "recipe:hay-feed";
        IResourceEconomyContentCatalog content =
            Resolve<IResourceEconomyContentCatalog>(scope);
        IProductionBillOrderCommand orders =
            Resolve<IProductionBillOrderCommand>(scope);
        IProductionBillQuery bills = Resolve<IProductionBillQuery>(scope);
        IProductionBillWorkExecution work =
            Resolve<IProductionBillWorkExecution>(scope);
        IProductionDistributionQuery distributionQuery =
            Resolve<IProductionDistributionQuery>(scope);
        IProductionPreparedOutputRoutingAuthority routing =
            Resolve<IProductionPreparedOutputRoutingAuthority>(scope);
        IFacilityOutputExactRouteOutboxQuery exactRoutes =
            Resolve<IFacilityOutputExactRouteOutboxQuery>(scope);
        IProductionPreparedOutputDeliveryCoordinator delivery =
            Resolve<IProductionPreparedOutputDeliveryCoordinator>(scope);
        IFacilityBufferMassCapacityQuery outputCapacities =
            Resolve<IFacilityBufferMassCapacityQuery>(scope);
        IWorldItemHaulPlanningService planning =
            Resolve<IWorldItemHaulPlanningService>(scope);
        ProgressionSceneRuntimeReferences progression =
            Resolve<ProgressionSceneRuntimeReferences>(scope);
        IGameClock gameClock = Resolve<IGameClock>(scope);
        ProductionDistributionRuntime distribution =
            distributionQuery as ProductionDistributionRuntime;
        ProductionRecipeSO recipe = null;
        bool recipeReady = content != null
            && content.TryGetRecipe(recipeId, out recipe)
            && recipe != null;
        Check(recipeReady
                && orders != null
                && bills != null
                && work != null
                && distribution != null
                && routing != null
                && exactRoutes != null
                && delivery != null
                && outputCapacities != null
                && planning != null
                && gameClock != null
                && progression?.BlueprintResearch != null,
            "PREPARED_OUTPUT_LIVE_RUNTIME_READY",
            $"recipe={recipeReady}; orders={orders != null}; bills={bills != null}; "
            + $"work={work != null}; distribution={distribution != null}; "
            + $"routing={routing != null}; routes={exactRoutes != null}; "
            + $"delivery={delivery != null}; planning={planning != null}; "
            + $"capacity={outputCapacities != null}; "
            + $"clock={gameClock != null}; research={progression?.BlueprintResearch != null}");
        if (!recipeReady
            || orders == null
            || bills == null
            || work == null
            || distribution == null
            || routing == null
            || exactRoutes == null
            || delivery == null
            || outputCapacities == null
            || planning == null
            || gameClock == null
            || progression?.BlueprintResearch == null)
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(recipe.RequiredResearchId))
        {
            progression.BlueprintResearch.State.Projects.RestoreCompleted(
                new ResearchProjectId(recipe.RequiredResearchId));
        }
        IReadOnlyList<Vector2Int> positions = FindReachableCells(
            grid,
            hauler.GetNowXY(),
            64);
        BuildingSO warehouseAsset = FindWarehouseAsset();
        BuildingSO feedbenchAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/P17_사료배합대.asset");
        bool warehousePositionReady = TryFindRegisterablePosition(
            grid,
            warehouseAsset,
            positions,
            out Vector2Int warehousePosition);
        HashSet<Vector2Int> warehouseCells = warehousePositionReady
            ? warehouseAsset.GetGridPosList(warehousePosition).ToHashSet()
            : new HashSet<Vector2Int>();
        Vector2Int feedbenchPosition = positions.FirstOrDefault(value =>
            !warehouseCells.Contains(value));
        Facility warehouse = CreateInjectedFacility(
            scope,
            grid,
            warehouseAsset,
            warehousePosition,
            "QA_Prepared_Output_Warehouse",
            registerOnGrid: true);
        Facility feedbench = CreateInjectedFacility(
            scope,
            grid,
            feedbenchAsset,
            feedbenchPosition,
            "QA_Prepared_Output_Feedbench");
        Check(warehousePositionReady
                && warehouse?.Inventory?.HasMassCapacityAuthority == true
                && feedbench != null
                && feedbench.MatchesProductionWorkstation(recipe),
            "PREPARED_OUTPUT_LIVE_FACILITIES_READY",
            $"warehouse={DescribeWarehouse(warehouse)}; feedbench="
            + $"{feedbench?.PersistentInstanceId.Value ?? "missing"}@{feedbenchPosition}");
        if (!warehousePositionReady
            || warehouse?.Inventory?.HasMassCapacityAuthority != true
            || feedbench == null
            || !feedbench.MatchesProductionWorkstation(recipe))
        {
            yield break;
        }
        ClearInventory(warehouse.Inventory);

        string feedbenchOutputDestination =
            ProductionBillRuntime.OutputDestinationPrefix
            + feedbench.PersistentInstanceId.Value;
        FacilityBufferMassCapacitySnapshot preBillCapacity = default;
        float preBillCapacityDeadline = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < preBillCapacityDeadline
            && (!outputCapacities.TryGetCapacity(
                    feedbenchOutputDestination,
                    feedbench.centerPos,
                    out preBillCapacity)
                || preBillCapacity.Profile.MaxMassGrams != 4_200L))
        {
            yield return null;
        }
        bool noBillCapacityReady = preBillCapacity.Profile != null
            && preBillCapacity.Profile.MaxMassGrams == 4_200L
            && preBillCapacity.ReservedMassGrams == 0L;
        Check(noBillCapacityReady,
            "PREPARED_OUTPUT_LIVE_FEEDBENCH_NO_BILL_CAPACITY_4200G",
            noBillCapacityReady
                ? $"destination={feedbenchOutputDestination}; max="
                    + $"{preBillCapacity.Profile.MaxMassGrams}; reserved="
                    + preBillCapacity.ReservedMassGrams
                : $"destination={feedbenchOutputDestination}; capacity=missing-or-nonexact");
        if (!noBillCapacityReady)
            yield break;

        ProductionBillCommandResult added = orders.AddBill(
            feedbench,
            recipeId,
            ProductionOrderMode.RepeatCount,
            1);
        ProductionBillSnapshot bill = added.Succeeded
            ? bills.GetBills(feedbench).SingleOrDefault(value =>
                value.BillId == added.BillId)
            : null;
        Check(added.Succeeded && bill != null,
            "PREPARED_OUTPUT_LIVE_BILL_CREATED",
            $"result={added.Outcome}; failure={added.Failure}; bill={added.BillId.Value}");
        if (!added.Succeeded || bill == null)
            yield break;

        bool inputsReady = true;
        foreach (ItemAmountDefinition input in bill.Inputs)
        {
            bool spawned = runtime.SpawnItemAt(
                input.ItemId,
                input.Amount,
                feedbench.centerPos,
                WorldItemStackState.FacilityBuffer,
                bill.MaterialDestinationId,
                out int amount);
            inputsReady &= spawned && amount == input.Amount;
        }
        Check(inputsReady,
            "PREPARED_OUTPUT_LIVE_INPUTS_PHYSICAL",
            $"destination={bill.MaterialDestinationId}; inputs="
            + string.Join(",", bill.Inputs.Select(value =>
                $"{value.ItemId}x{value.Amount}")));
        if (!inputsReady)
            yield break;

        ProductionWorkAvailabilityResult available = work.CheckWorkAvailability(
            feedbench,
            recipe.WorkTypeId);
        ProductionWorkBeginResult begun = available.Available
            ? work.BeginWork(hauler, feedbench, recipe.WorkTypeId)
            : default;
        ProductionWorkExecutionResult completed = begun.Succeeded
            ? work.ExecuteWork(
                hauler,
                feedbench,
                bill.BillId,
                recipe.RequiredWork + 1f)
            : default;
        Check(available.Available
                && begun.Succeeded
                && completed.Succeeded
                && completed.CycleCompleted,
            "PREPARED_OUTPUT_LIVE_BATCH_COMPLETED",
            $"available={available.Available}:{available.Failure}; "
            + $"begun={begun.Succeeded}:{begun.Failure}; "
            + $"completed={completed.Succeeded}/{completed.CycleCompleted}:"
            + completed.Failure);
        if (!completed.Succeeded || !completed.CycleCompleted)
            yield break;

        bool exactFacilityCapacity = outputCapacities.TryGetCapacity(
                bill.OutputDestinationId,
                feedbench.centerPos,
                out FacilityBufferMassCapacitySnapshot outputCapacity)
            && outputCapacity.Profile.MaxMassGrams == 4_200L
            && outputCapacity.ReservedMassGrams == 0L;
        Check(exactFacilityCapacity,
            "PREPARED_OUTPUT_LIVE_FEEDBENCH_MAX_BRANCH_CAPACITY_4200G",
            exactFacilityCapacity
                ? $"destination={bill.OutputDestinationId}; max="
                    + $"{outputCapacity.Profile.MaxMassGrams}; reserved="
                    + outputCapacity.ReservedMassGrams
                : $"destination={bill.OutputDestinationId}; capacity=missing-or-nonexact");
        if (!exactFacilityCapacity)
            yield break;

        FacilityOutputExactRoutePendingSnapshot route = null;
        WorldItemStackSnapshot routedStack = null;
        float routeDeadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < routeDeadline)
        {
            distribution.Tick();
            route = exactRoutes.CapturePendingRoutes()
                .Where(value => value?.Receipt != null
                    && value.Phase == FacilityOutputExactRoutePhase.Routable
                    && string.Equals(
                        value.DeliveryRevision.TargetDestinationId,
                        WarehouseStorageIdentity.RequireDestinationId(warehouse),
                        StringComparison.Ordinal))
                .OrderBy(value => value.Receipt.RouteOperationId,
                    StringComparer.Ordinal)
                .FirstOrDefault();
            if (route != null)
            {
                routedStack = runtime.GetAllStacks().FirstOrDefault(stack =>
                    stack != null
                    && stack.State == WorldItemStackState.Loose
                    && string.Equals(
                        stack.DestinationId,
                        route.DeliveryRevision.TargetDestinationId,
                        StringComparison.Ordinal)
                    && stack.Components.Any(component =>
                        component != null
                        && string.Equals(
                            component.componentTypeId,
                            PreparedOutputCustodyComponentTypeId,
                            StringComparison.Ordinal)));
            }
            if (routedStack != null)
                break;
            yield return null;
        }
        ProductionPreparedOutputRoutingLineSnapshot[] routingLines = routing
            .CaptureBill(bill.BillId)
            .OrderBy(value => value.LineCommitId, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> liveLineIds = routingLines
            .Select(value => value.LineCommitId)
            .ToHashSet(StringComparer.Ordinal);
        ProductionPreparedOutputRouteRequestSnapshot[] routeOperations = routing
            .CaptureRouteOperations()
            .Where(value => liveLineIds.Contains(value.LineCommitId))
            .OrderBy(value => value.RouteOperationId, StringComparer.Ordinal)
            .ToArray();
        FacilityOutputExactRoutePendingSnapshot[] physicalRoutes = exactRoutes
            .CapturePendingRoutes()
            .OrderBy(value => value.Receipt.RouteOperationId, StringComparer.Ordinal)
            .ToArray();
        Check(route != null && routedStack != null,
            "PREPARED_OUTPUT_LIVE_EXACT_WAREHOUSE_TARGET",
            $"route={route?.Receipt?.RouteOperationId ?? "missing"}; stack="
            + $"{routedStack?.StackId ?? "missing"}; warehouse="
            + WarehouseStorageIdentity.RequireDestinationId(warehouse)
            + $"; clock={gameClock.IsPaused}/{gameClock.DeltaTime:0.###}; "
            + $"lines={routingLines.Length}; operations="
            + string.Join(",", routeOperations.Select(value =>
                $"{value.RouteOperationId}:{value.Phase}:r{value.CurrentDeliveryRevision}:"
                + $"{value.CurrentTargetDestinationId}"))
            + "; physical="
            + string.Join(",", physicalRoutes.Select(value =>
                $"{value.Receipt.RouteOperationId}:{value.Phase}:"
                + $"{value.DeliveryRevision.TargetDestinationId}")));
        if (route == null || routedStack == null)
            yield break;

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        HaulDeliveryIntentSaveData committedIntent = null;
        string currentTargetDestinationId = route.DeliveryRevision.TargetDestinationId;
        try
        {
            bool canStart = action.CanStart(hauler);
            Check(canStart,
                "PREPARED_OUTPUT_LIVE_AIHAUL_CAN_START",
                DescribeHaulState(runtime, hauler));
            if (!canStart)
                yield break;
            action.Execute(hauler);
            float deadline = Time.realtimeSinceStartup + HaulTimeoutSeconds;
            int expectedQuantity = route.Receipt.TotalQuantity;
            while (Time.realtimeSinceStartup < deadline)
            {
                EnsureVerificationTimeScale();
                committedIntent ??= runtime
                    .CaptureHaulDeliveryIntentsByDestination(
                        currentTargetDestinationId)
                    .FirstOrDefault(intent => intent.HasCommittedPickup);
                if (GetStoredItemQuantity(
                        runtime,
                        route.Receipt.Slices[0].ItemId,
                        warehouse.centerPos) >= expectedQuantity)
                {
                    break;
                }
                yield return null;
            }

            int stored = GetStoredItemQuantity(
                runtime,
                route.Receipt.Slices[0].ItemId,
                warehouse.centerPos);
            WarehouseHaulAdmissionSaveData admission = committedIntent?
                .warehouseAdmissions?
                .SingleOrDefault();
            Check(stored == expectedQuantity
                    && admission != null
                    && admission.quantity == expectedQuantity
                    && admission.reservedMassGrams == route.Receipt.TotalMassGrams
                    && warehouse.Inventory.ReservedInboundMassGrams == 0L,
                "PREPARED_OUTPUT_LIVE_STORED_WITH_DURABLE_ADMISSION",
                $"stored={stored}/{expectedQuantity}; intent="
                + $"{committedIntent?.operationId ?? "missing"}; admission="
                + $"{admission?.tokenId ?? "missing"}:"
                + $"{admission?.reservedMassGrams ?? 0}; routeMass="
                + $"{route.Receipt.TotalMassGrams}; inbound="
                + warehouse.Inventory.ReservedInboundMassGrams
                + "; haulState=" + DescribeHaulState(runtime, hauler));
        }
        finally
        {
            Destroy(action);
        }
    }

    private IEnumerator VerifyMaterialRepairAndSalvage(
        IWorldItemStackRuntime itemRuntime,
        ICombatEquipmentRuntime equipment,
        ICombatEquipmentMaintenanceRuntime maintenance,
        IResourceEconomyContentCatalog economyCatalog,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        CharacterActor hauler,
        Facility warehouse,
        Vector2Int facilityPosition)
    {
        BuildingSO maintenanceAsset = CreateMaintenanceAsset();
        Facility maintenanceFacility = CreateInjectedFacility(
            scope,
            grid,
            maintenanceAsset,
            facilityPosition,
            "QA_Material_Equipment_Maintenance");
        try
        {
            Check(maintenanceFacility != null
                    && CombatEquipmentMaintenanceFacilityUtility.IsMaintenanceFacility(
                        maintenanceFacility),
                "MATERIAL_REPAIR_FACILITY_READY",
                maintenanceFacility != null
                    ? $"pos={maintenanceFacility.centerPos}"
                    : "missing maintenance facility");
            if (maintenanceFacility == null
                || !economyCatalog.TryGetMaterial(
                    "material:blacksteel",
                    out CraftMaterialDefinitionSO blacksteel))
            {
                yield break;
            }

            Check(SeedStoredCraftMaterial(
                    itemRuntime,
                    economyCatalog,
                    warehouse,
                    "material:blacksteel",
                    8,
                    out string seedDetails),
                "MATERIAL_REPAIR_STOCK_SEEDED",
                seedDetails);

            CombatEquipmentInstance armor = equipment.CreateInstance(
                RepairEquipmentId,
                CombatEquipmentQuality.Normal,
                CombatEquipmentWorldState.Stored,
                "material:blacksteel");
            string warehouseDestinationId =
                WarehouseStorageIdentity.RequireDestinationId(warehouse);
            bool stackSpawned = itemRuntime.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipment(RepairEquipmentId),
                (ItemInstanceId)armor.instanceId,
                warehouse.centerPos,
                WorldItemStackState.Stored,
                warehouseDestinationId,
                out string armorStackId);
            Check(stackSpawned
                    && equipment.TryLinkToWorldStack(
                        armor.instanceId,
                        armorStackId,
                        CombatEquipmentWorldState.Stored)
                    && equipment.TryGetDerivedStats(
                        armor.instanceId,
                        out CombatEquipmentDerivedStats armorStats)
                    && equipment.TryApplyDurabilityDamage(
                        armor.instanceId,
                        armorStats.MaxDurability * 0.6f),
                "MATERIAL_REPAIR_ARMOR_DAMAGED",
                $"instance={armor.instanceId}; material={armor.materialId}");

            Check(maintenance.TryRequestManualRepair(
                    armor.instanceId,
                    out string repairMessage),
                "MATERIAL_REPAIR_ORDER_CREATED",
                repairMessage);
            CombatEquipmentRepairOrder order = maintenance.Orders.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.equipmentInstanceId,
                    armor.instanceId,
                    StringComparison.Ordinal));
            Check(order != null
                    && string.Equals(
                        order.materialItemId,
                        blacksteel.ItemId,
                        StringComparison.Ordinal)
                    && order.requiredMaterialAmount > 0,
                "MATERIAL_REPAIR_REQUIRES_ORIGINAL_MATERIAL",
                order != null
                    ? $"item={order.materialItemId}; amount={order.requiredMaterialAmount}"
                    : "missing repair order");
            if (order == null)
            {
                yield break;
            }

            bool repairClaimExact = destinationClaims.TryGetClaim(
                    order.FacilityDestinationId,
                    maintenanceFacility.centerPos,
                    out FacilityBufferDestinationClaim repairClaim)
                && repairClaim.AnchorKind
                    == FacilityBufferDestinationAnchorKind.LiveFacility
                && string.Equals(
                    repairClaim.OwnerDomain,
                    "combat.equipment-maintenance",
                    StringComparison.Ordinal)
                && string.Equals(
                    repairClaim.OwnerOperationId,
                    order.orderId,
                    StringComparison.Ordinal)
                && string.Equals(
                    repairClaim.OwnerFacilityId,
                    order.facilityBuildingId,
                    StringComparison.Ordinal);
            Check(repairClaimExact,
                "MATERIAL_REPAIR_DESTINATION_CLAIM_EXACT",
                $"destination={order.FacilityDestinationId}; "
                + $"facility={order.facilityBuildingId}; "
                + $"drop={maintenanceFacility.centerPos}");

            IFacilityBufferMassCapacityQuery repairCapacities =
                Resolve<IFacilityBufferMassCapacityQuery>(scope);
            bool repairProfileExact = repairCapacities != null
                && repairCapacities.TryGetCapacity(
                    order.FacilityDestinationId,
                    maintenanceFacility.centerPos,
                    out FacilityBufferMassCapacitySnapshot repairCapacity)
                && repairCapacity.Profile.MaxMassGrams > 0L
                && repairCapacity.Profile.CapacityRevision == 1L
                && string.Equals(
                    repairCapacity.Profile.OwnerDomain,
                    "combat.equipment-maintenance",
                    StringComparison.Ordinal)
                && string.Equals(
                    repairCapacity.Profile.OwnerOperationId,
                    order.orderId,
                    StringComparison.Ordinal)
                && string.Equals(
                    repairCapacity.Profile.OwnerFacilityId,
                    order.facilityBuildingId,
                    StringComparison.Ordinal);
            Check(repairProfileExact,
                "MATERIAL_REPAIR_POSITIVE_GRAM_PROFILE_EXACT",
                repairCapacities != null
                    && repairCapacities.TryGetCapacity(
                        order.FacilityDestinationId,
                        maintenanceFacility.centerPos,
                        out FacilityBufferMassCapacitySnapshot detailCapacity)
                    ? $"mass={detailCapacity.Profile.MaxMassGrams}; "
                        + $"revision={detailCapacity.Profile.CapacityRevision}; "
                        + $"owner={detailCapacity.Profile.OwnerOperationId}"
                    : "capacity profile missing");

            bool repairEquipmentDestinationReady =
                equipment.TryGetInstance(
                    armor.instanceId,
                    out CombatEquipmentInstance repairInstance)
                && itemRuntime.GetAllStacks().Any(stack =>
                    stack != null
                    && string.Equals(
                        stack.StackId,
                        repairInstance.sourceStackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.DestinationId,
                        order.FacilityDestinationId,
                        StringComparison.Ordinal)
                    && stack.HasDestinationPosition
                    && stack.DestinationPosition == maintenanceFacility.centerPos);
            Check(repairEquipmentDestinationReady,
                "MATERIAL_REPAIR_EQUIPMENT_DESTINATION_READY",
                equipment.TryGetInstance(
                    armor.instanceId,
                    out CombatEquipmentInstance currentRepairInstance)
                    ? $"stack={currentRepairInstance.sourceStackId}; "
                        + DescribeStacks(itemRuntime)
                    : "repair equipment missing");

            IWorldItemHaulPlanningService haulPlanning =
                Resolve<IWorldItemHaulPlanningService>(scope);
            WorldItemHaulPlan repairPreview = null;
            string repairPreviewFailure = "haul planning service missing";
            bool repairPlanReady = false;
            for (int attempt = 0; haulPlanning != null && attempt < 16; attempt++)
            {
                repairPlanReady = haulPlanning.TryPreviewBestPlan(
                        hauler,
                        out repairPreview,
                        out repairPreviewFailure)
                    && repairPreview != null
                    && repairPreview.IsValid
                    && string.Equals(
                        repairPreview.PrimaryDestinationId,
                        order.FacilityDestinationId,
                        StringComparison.Ordinal);
                if (repairPlanReady
                    || !string.Equals(
                        repairPreviewFailure,
                        "path search deferred",
                        StringComparison.Ordinal))
                {
                    break;
                }
                yield return null;
            }
            Check(repairPlanReady,
                "MATERIAL_REPAIR_HAUL_PLAN_PREFLIGHT",
                haulPlanning == null
                    ? "haul planning service missing"
                    : repairPlanReady
                        ? $"destination={repairPreview.PrimaryDestinationId}; "
                            + $"pickups={repairPreview.PickupLegs.Count}; "
                            + $"delivery={repairPreview.DeliveryLegs[0].DeliveryPosition}"
                        : $"failure={repairPreviewFailure}; "
                            + $"previewDestination={repairPreview?.PrimaryDestinationId ?? "<none>"}");
            if (!repairPlanReady)
            {
                yield break;
            }

            yield return RunRepeatedHaul(
                hauler,
                () => IsRepairOrderReady(maintenance, order.orderId));
            Check(IsRepairOrderReady(maintenance, order.orderId),
                "MATERIAL_REPAIR_INPUTS_DELIVERED",
                DescribeStacks(itemRuntime));

            WorldItemStackSnapshot[] repairMaterialStacks = itemRuntime.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.DestinationId,
                        order.FacilityDestinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        order.materialItemId,
                        StringComparison.Ordinal))
                .ToArray();
            int deliveredRepairMaterial = repairMaterialStacks
                .Where(stack => stack.State == WorldItemStackState.FacilityBuffer)
                .Sum(stack => stack.Quantity);
            int totalRequestedRepairMaterial = repairMaterialStacks
                .Sum(stack => stack.Quantity);
            Check(
                deliveredRepairMaterial == order.requiredMaterialAmount
                    && totalRequestedRepairMaterial == order.requiredMaterialAmount,
                "MATERIAL_REPAIR_NO_DUPLICATE_REQUEST",
                $"required={order.requiredMaterialAmount}; "
                    + $"delivered={deliveredRepairMaterial}; "
                    + $"destinationTotal={totalRequestedRepairMaterial}; "
                    + DescribeStacks(itemRuntime));

            bool repaired = false;
            bool repairCompleted = false;
            string applyMessage = string.Empty;
            int repairAttempts = Mathf.Max(1, maintenance.Orders.Count + 1);
            for (int attempt = 0; attempt < repairAttempts; attempt++)
            {
                bool applied = maintenance.TryApplyRepairWork(
                    hauler,
                    maintenanceFacility,
                    100f,
                    out bool completedAttempt,
                    out string attemptMessage);
                applyMessage = attemptMessage;
                repaired |= applied;
                if (equipment.TryGetInstance(
                        armor.instanceId,
                        out CombatEquipmentInstance updatedArmor)
                    && updatedArmor.durabilityRatio + 0.001f >= order.targetDurability)
                {
                    repairCompleted = true;
                    break;
                }

                if (!applied && !completedAttempt)
                {
                    break;
                }
            }
            Check(repaired
                    && repairCompleted
                    && equipment.TryGetInstance(
                        armor.instanceId,
                        out CombatEquipmentInstance repairedArmor)
                    && string.Equals(
                        repairedArmor.materialId,
                        "material:blacksteel",
                        StringComparison.Ordinal)
                    && repairedArmor.durabilityRatio + 0.001f >= order.targetDurability,
                "MATERIAL_REPAIR_PRESERVES_INSTANCE_AND_MATERIAL",
                applyMessage);
            Check(!destinationClaims.TryGetClaim(
                    order.FacilityDestinationId,
                    maintenanceFacility.centerPos,
                    out _),
                "MATERIAL_REPAIR_DESTINATION_CLAIM_REVOKED_AFTER_COMPLETE",
                $"destination={order.FacilityDestinationId}; completed={repairCompleted}");
            Check(repairCapacities != null
                    && !repairCapacities.TryGetCapacity(
                        order.FacilityDestinationId,
                        maintenanceFacility.centerPos,
                        out _),
                "MATERIAL_REPAIR_CAPACITY_PROFILE_ZERO_AFTER_COMPLETE",
                $"destination={order.FacilityDestinationId}; completed={repairCompleted}");

            Check(equipment.TrySalvage(
                    armor.instanceId,
                    maintenanceFacility.centerPos,
                    out string recoveredItemId,
                    out int recoveredAmount,
                    out string salvageReason)
                    && string.Equals(
                        recoveredItemId,
                        blacksteel.ItemId,
                        StringComparison.Ordinal)
                    && recoveredAmount > 0
                    && recoveredAmount
                        <= Mathf.FloorToInt(
                            equipment.Definitions
                                .First(definition =>
                                    string.Equals(
                                        definition.EquipmentId,
                                        RepairEquipmentId,
                                        StringComparison.Ordinal))
                                .PrimaryMaterialAmount
                            * 0.5f)
                    && !equipment.TryGetInstance(armor.instanceId, out _)
                    && itemRuntime.GetAllStacks().Any(stack =>
                        stack != null
                        && stack.State == WorldItemStackState.Loose
                        && string.Equals(
                            stack.ItemId,
                            blacksteel.ItemId,
                            StringComparison.Ordinal)
                        && stack.Position == maintenanceFacility.centerPos),
                "MATERIAL_SALVAGE_RETURNS_ORIGINAL_MATERIAL",
                $"item={recoveredItemId}; amount={recoveredAmount}; reason={salvageReason}");
        }
        finally
        {
            if (maintenanceFacility != null)
            {
                maintenanceFacility.DestroySelf();
            }

            Destroy(maintenanceAsset);
        }
    }

    private static bool IsRepairOrderReady(
        ICombatEquipmentMaintenanceRuntime maintenance,
        string orderId)
    {
        return maintenance != null
            && maintenance.Orders.Any(candidate =>
                candidate != null
                && string.Equals(
                    candidate.orderId,
                    orderId,
                    StringComparison.Ordinal)
                && candidate.state is CombatEquipmentRepairOrderState.Ready
                    or CombatEquipmentRepairOrderState.InProgress);
    }

    private IEnumerator VerifyExpeditionPacking(
        IOffensePreparationService preparation,
        IWorldItemStackRuntime itemRuntime,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        Facility warehouse,
        CharacterActor hauler)
    {
        const string rationItemId = "food:preserved-ration";
        QuiesceHaulingBeforeDirectStateFixture();
        yield return null;
        int rationBefore = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        OffenseSupplyLoadout loadout = new OffenseSupplyLoadout();
        loadout.Add(OffenseSupplyType.Rations, 2);
        string packageId = "qa-package-" + Guid.NewGuid().ToString("N");
        bool committed = preparation.TryCommitLoadout(
            loadout,
            new OffenseExpeditionPreparation(supplyCapacity: 6),
            packageId,
            out string message);
        Check(committed,
            "EXPEDITION_SUPPLY_DELIVERY_COMMITTED",
            $"message={message}; stacks={DescribeStacks(itemRuntime)}");
        if (!committed)
        {
            yield break;
        }

        OffenseSupplyPackingStateData package = preparation.CapturePackingState()
            .SingleOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.packageId,
                    packageId,
                    StringComparison.Ordinal));
        string destinationId = $"expedition:{packageId}";
        bool exactClaim = package != null
            && destinationClaims.TryGetClaim(
                destinationId,
                package.StagingPosition,
                out FacilityBufferDestinationClaim claim)
            && claim.AnchorKind
                == FacilityBufferDestinationAnchorKind.ReservedTarget
            && string.Equals(
                claim.OwnerDomain,
                "offense.expedition-supply",
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerOperationId,
                packageId,
                StringComparison.Ordinal)
            && claim.OwnerFacilityId == null;
        Check(exactClaim,
            "EXPEDITION_RESERVED_TARGET_CLAIM_EXACT",
            package == null
                ? "package state missing"
                : $"destination={destinationId}; staging={package.StagingPosition}");
        if (!exactClaim)
            yield break;

        yield return RunRepeatedHaul(
            hauler,
            () => preparation.IsPackageReady(packageId));

        OffenseSupplyPackingSnapshot packing = preparation.GetPackingSnapshot(packageId);
        Check(packing.IsReady,
            "EXPEDITION_SUPPLIES_PACKED",
            $"delivered={packing.Delivered}/{packing.Required}; stacks={DescribeStacks(itemRuntime)}");
        int packed = itemRuntime.GetAllStacks().Where(stack =>
                stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        Check(packed == 2,
            "EXPEDITION_PACKED_STACK_VISIBLE",
            $"packed={packed}; destination={destinationId}");
        int committedInTransit = itemRuntime.GetCommittedHaulDeliveryQuantity(
            destinationId,
            rationItemId);
        int routedTotal = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity)
            + committedInTransit;
        Check(routedTotal == 2,
            "EXPEDITION_REPEATED_READY_POLL_NO_DUPLICATE",
            $"routed={routedTotal}; committedInTransit={committedInTransit}; "
            + $"destination={destinationId}");
        bool consumed = preparation.TryConsumePackedSupplies(packageId, out string consumeMessage);
        Check(consumed,
            "EXPEDITION_PACKED_STACK_CONSUME_COMMITTED",
            consumeMessage);
        bool removed = consumed
            && !itemRuntime.GetAllStacks().Any(stack =>
                string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
        Check(removed,
            "EXPEDITION_PACKED_STACK_CONSUMED",
            $"consumed={consumed}; stacks={DescribeStacks(itemRuntime)}");
        bool claimRevoked = consumed
            && !destinationClaims.TryGetClaim(
                destinationId,
                package.StagingPosition,
                out _);
        Check(claimRevoked,
            "EXPEDITION_RESERVED_TARGET_CLAIM_REVOKED_AFTER_CONSUME",
            $"consumed={consumed}; destination={destinationId}");
        int rationAfter = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        Check(consumed && rationBefore - rationAfter == 2,
            "EXPEDITION_SUPPLY_CONSUME_QUANTITY_CONSERVED",
            $"before={rationBefore}; after={rationAfter}; consumed={consumed}");

        string cancelPackageId = "qa-cancel-package-"
            + Guid.NewGuid().ToString("N");
        string warehouseDestinationId =
            WarehouseStorageIdentity.RequireDestinationId(warehouse);
        bool cancelStockSeeded = itemRuntime.SpawnItemAt(
            rationItemId,
            2,
            warehouse.centerPos,
            WorldItemStackState.Stored,
            warehouseDestinationId,
            out int cancelSeededAmount);
        int cancelBefore = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        Check(cancelStockSeeded && cancelSeededAmount == 2,
            "EXPEDITION_CANCEL_STOCK_SEEDED",
            $"spawned={cancelSeededAmount}; total={cancelBefore}");
        bool cancelCommitted = preparation.TryCommitLoadout(
            loadout,
            new OffenseExpeditionPreparation(supplyCapacity: 6),
            cancelPackageId,
            out string cancelMessage);
        OffenseSupplyPackingStateData cancelPackage = preparation
            .CapturePackingState()
            .SingleOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.packageId,
                    cancelPackageId,
                    StringComparison.Ordinal));
        string cancelDestinationId = $"expedition:{cancelPackageId}";
        bool cancelClaimExact = cancelCommitted
            && cancelPackage != null
            && destinationClaims.TryGetClaim(
                cancelDestinationId,
                cancelPackage.StagingPosition,
                out FacilityBufferDestinationClaim cancelClaim)
            && cancelClaim.AnchorKind
                == FacilityBufferDestinationAnchorKind.ReservedTarget
            && string.Equals(
                cancelClaim.OwnerOperationId,
                cancelPackageId,
                StringComparison.Ordinal)
            && string.Equals(
                cancelClaim.OwnerDomain,
                "offense.expedition-supply",
                StringComparison.Ordinal)
            && cancelClaim.OwnerFacilityId == null;
        if (cancelCommitted)
        {
            preparation.ReturnSupplies(loadout, cancelPackageId);
        }
        int cancelAfter = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        bool cancelConserved = cancelClaimExact
            && cancelBefore == cancelAfter
            && !preparation.GetPackingSnapshot(cancelPackageId).Exists
            && !itemRuntime.GetAllStacks().Any(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    cancelDestinationId,
                    StringComparison.Ordinal))
            && !destinationClaims.TryGetClaim(
                cancelDestinationId,
                    cancelPackage.StagingPosition,
                out _)
            && itemRuntime.GetCommittedHaulDeliveryQuantity(
                cancelDestinationId,
                rationItemId) == 0;
        Check(cancelCommitted && cancelConserved,
            "EXPEDITION_CANCEL_RELEASE_CONSERVED",
            $"committed={cancelCommitted}; claim={cancelClaimExact}; "
            + $"before={cancelBefore}; after={cancelAfter}; message={cancelMessage}");

        preparation.ReturnSupplies(loadout, cancelPackageId);
        int duplicateReturnAfter = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        Check(cancelCommitted && duplicateReturnAfter == cancelAfter,
            "EXPEDITION_UNKNOWN_OR_DUPLICATE_RETURN_NO_MINT",
            $"before={cancelAfter}; after={duplicateReturnAfter}; package={cancelPackageId}");
    }

    private IEnumerator VerifyCarryUi(IWorldItemStackRuntime itemRuntime, CharacterActor hauler)
    {
        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(hauler);
        string failure = string.Empty;
        Check(carry != null
                && carry.TryAdd(
                    "qa-carry-ui",
                    "material:lumber",
                    2,
                    itemRuntime.CatalogProvider,
                    itemRuntime.HaulingSettingsProvider,
                    out failure),
            "CARRY_UI_ITEM_SEEDED",
            carry != null ? $"failure={failure}; {DescribeCarry(hauler, itemRuntime)}" : "missing carry");

        Resolve<DungeonStory.Foundation.IGameEventBus>(FindScope())?.ShowInfo(hauler);
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        string sample = GetVisibleTextSample();
        string weightText = Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(text.text))
            .Select(text => Compact(text.text))
            .FirstOrDefault(text =>
                text.Contains("kg", StringComparison.OrdinalIgnoreCase)
                && text.Contains("/", StringComparison.Ordinal));
        Check(!string.IsNullOrWhiteSpace(weightText),
            "CARRY_UI_WEIGHT_VISIBLE",
            string.IsNullOrWhiteSpace(weightText) ? sample : weightText);
        yield return CaptureScreen(PhysicalItemLogisticsPlayModeVerifier.CarryCapturePath);
        carry?.RemoveAllItems();
    }

    private static void QuiesceHaulingBeforeDirectStateFixture()
    {
        foreach (CharacterActor actor in CharacterActorCollection.DistinctByGameObject(
                     UnityEngine.Object.FindObjectsByType<CharacterActor>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None)))
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }
            actor.SetAiPaused(true);
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "qa-direct-state-fixture-boundary");
        }
    }

    private IEnumerator RunHaul(AIHaul action, CharacterActor hauler, Func<bool> completed)
    {
        AbilityHaul ability = AbilityHaul.Ensure(hauler);
        action.Execute(hauler);
        float startedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startedAt < HaulTimeoutSeconds)
        {
            EnsureVerificationTimeScale();
            if (completed())
            {
                yield return null;
                Check(true, "AI_HAUL_COMPLETED", $"elapsed={Time.realtimeSinceStartup - startedAt:0.0}s");
                yield break;
            }

            if (ability == null || !ability.IsHauling)
            {
                for (int settleFrame = 0; settleFrame < 4; settleFrame++)
                {
                    yield return null;
                    if (completed())
                    {
                        Check(true, "AI_HAUL_COMPLETED", $"elapsed={Time.realtimeSinceStartup - startedAt:0.0}s");
                        yield break;
                    }
                }

                break;
            }

            yield return null;
        }

        Check(false, "AI_HAUL_COMPLETED", DescribeHaulState(itemRuntime, hauler));
    }

    private IEnumerator RunRepeatedHaul(
        CharacterActor hauler,
        Func<bool> completed)
    {
        float startedAt = Time.realtimeSinceStartup;
        while (!completed()
            && Time.realtimeSinceStartup - startedAt < HaulTimeoutSeconds)
        {
            EnsureVerificationTimeScale();
            AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
            try
            {
                if (!action.CanStart(hauler))
                {
                    yield return null;
                    continue;
                }

                AbilityHaul ability = AbilityHaul.Ensure(hauler);
                action.Execute(hauler);
                while (!completed()
                    && ability != null
                    && ability.IsHauling
                    && Time.realtimeSinceStartup - startedAt < HaulTimeoutSeconds)
                {
                    EnsureVerificationTimeScale();
                    yield return null;
                }
            }
            finally
            {
                Destroy(action);
            }

            yield return null;
        }

        Check(
            completed(),
            "AI_REPEATED_HAUL_COMPLETED",
            $"elapsed={Time.realtimeSinceStartup - startedAt:0.0}s; "
            + DescribeHaulState(itemRuntime, hauler));
    }

    private static void EnsureVerificationTimeScale()
    {
        if (Time.timeScale < 0.1f)
        {
            Time.timeScale = 8f;
        }
    }

    private IEnumerator EnsurePlayableRun()
    {
        OwnerRunManager ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        float deadline = Time.realtimeSinceStartup + PartyReadyTimeoutSeconds;
        int staffCount = 0;
        while (Time.realtimeSinceStartup < deadline)
        {
            ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
            staffCount = CountPreparedStaff();
            if (ownerManager?.CurrentOwnerActor != null && staffCount == 2)
                break;
            yield return null;
        }

        bool ready = ownerManager?.CurrentOwnerActor != null && staffCount == 2;
        Check(ready,
            "RUN_READY",
            ready
                ? $"owner={ownerManager.CurrentOwnerActor.name}; staff={staffCount}"
                : $"owner={(ownerManager?.CurrentOwnerActor != null ? ownerManager.CurrentOwnerActor.name : "missing")}; staff={staffCount}");
    }

    private IEnumerator EnsureProductBoot()
    {
        float titleDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        DungeonTitleLifetimeScope titleScope = null;
        IDungeonSceneNavigator navigator = null;
        while (Time.realtimeSinceStartup < titleDeadline)
        {
            titleScope = UnityEngine.Object.FindFirstObjectByType<
                DungeonTitleLifetimeScope>(FindObjectsInactive.Include);
            if (titleScope?.Container != null)
            {
                try
                {
                    navigator = titleScope.Container.Resolve<
                        IDungeonSceneNavigator>();
                }
                catch (Exception exception)
                {
                    capturedErrors.Add("[BOOT-DI-ERROR] " + exception);
                }
            }
            if (navigator != null
                && string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.TitleSceneName,
                    StringComparison.Ordinal))
            {
                break;
            }
            yield return null;
        }

        bool titleReady = navigator != null;
        Check(
            titleReady,
            "BOOT_TITLE_READY",
            titleReady
                ? "Title scope and production scene navigator are ready."
                : "Title scope or production scene navigator was not ready.");
        if (!titleReady
            || !navigator.StartNewGame(
                DungeonDifficulty.Normal,
                DungeonSurvivalPressure.Standard))
        {
            Check(false, "BOOT_PREPARATION_REQUESTED",
                "Production StartNewGame request was rejected.");
            yield break;
        }

        float preparationDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        Button owner = null;
        Button next = null;
        while (Time.realtimeSinceStartup < preparationDeadline)
        {
            owner = Resources.FindObjectsOfTypeAll<Button>()
                .Where(candidate => candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.activeInHierarchy
                    && candidate.interactable
                    && candidate.name.StartsWith(
                        "OwnerCandidate_",
                        StringComparison.Ordinal))
                .OrderBy(candidate => candidate.name, StringComparer.Ordinal)
                .FirstOrDefault();
            next = StartPartyPlayModeTestDriver.FindButton(
                "PreparationOwnerNextButton",
                requireInteractable: false);
            if (owner != null
                && next != null
                && string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.PreparationSceneName,
                    StringComparison.Ordinal))
            {
                break;
            }
            yield return null;
        }

        bool preparationReady = owner != null && next != null;
        Check(preparationReady, "BOOT_PREPARATION_READY",
            preparationReady
                ? "Preparation owner selection is ready."
                : "Preparation owner selection did not become ready.");
        if (!preparationReady)
            yield break;

        ClickButton(owner);
        yield return null;
        next = StartPartyPlayModeTestDriver.FindButton(
            "PreparationOwnerNextButton",
            requireInteractable: true);
        if (next == null)
        {
            Check(false, "BOOT_PREPARATION_OWNER_SELECTED",
                "Owner selection did not enable the next command.");
            yield break;
        }
        ClickButton(next);

        float startDeadline = Time.realtimeSinceStartup
            + PartyReadyTimeoutSeconds;
        Button start = null;
        while (Time.realtimeSinceStartup < startDeadline)
        {
            start = StartPartyPlayModeTestDriver.FindButton(
                "PreparationStartRunButton",
                requireInteractable: true);
            if (start != null)
                break;
            yield return null;
        }
        Check(start != null, "BOOT_PREPARED_START_READY",
            start != null
                ? "Prepared start command is interactable."
                : "Prepared start command did not become interactable.");
        if (start == null)
            yield break;

        yield return StartPartyPlayModeTestDriver.CompleteIfVisible(
            RuntimeReadyTimeoutSeconds);
        Check(true, "BOOT_PREPARED_START_REQUESTED",
            "PreparedNewRun was dispatched through the production preparation UI.");

        float gameplayDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < gameplayDeadline)
        {
            if (string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.GameplaySceneName,
                    StringComparison.Ordinal)
                && UnityEngine.Object.FindFirstObjectByType<
                    DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include)
                    ?.Container != null)
            {
                Check(true, "BOOT_GAMEPLAY_READY",
                    "PreparedNewRun reached Gameplay with a runtime container.");
                yield break;
            }
            yield return null;
        }
        Check(false, "BOOT_GAMEPLAY_READY",
            "PreparedNewRun did not reach Gameplay before timeout.");
    }

    private static void ClickButton(Button button)
    {
        RectTransform rect = button?.transform as RectTransform;
        Vector2 position = rect != null
            ? RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center))
            : Vector2.zero;
        if (button == null
            || !PlayModeVerificationFrameWait.DispatchPointerClick(
                button.gameObject,
                position))
        {
            throw new InvalidOperationException(
                "Verification button click could not be dispatched.");
        }
    }

    private void CaptureRuntimeState(IWorldItemStackRuntime itemRuntime, ICombatEquipmentRuntime equipment)
    {
        physicalSnapshot = itemRuntime.Capture();
        equipmentSnapshot = equipment.Capture();
        warehouseSnapshots.Clear();
        foreach (WarehouseInventory inventory in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None)
                 .OfType<IWarehouseFacility>()
                 .Where(facility => facility != null && facility.Inventory != null)
                 .Select(facility => facility.Inventory)
                 .Distinct())
        {
            warehouseSnapshots[inventory] = inventory.CreateSnapshot();
        }
    }

    private void RestoreRuntimeState(IWorldItemStackRuntime itemRuntime, ICombatEquipmentRuntime equipment)
    {
        CharacterSummaryInfo summary = UnityEngine.Object.FindFirstObjectByType<CharacterSummaryInfo>(
            FindObjectsInactive.Include);
        summary?.OnClose();

        foreach (KeyValuePair<WarehouseInventory, WarehouseInventorySnapshot> pair in warehouseSnapshots)
        {
            if (pair.Key != null && pair.Value != null)
            {
                pair.Key.ApplySnapshot(pair.Value);
            }
        }

        if (physicalSnapshot != null)
        {
            itemRuntime.Restore(physicalSnapshot);
        }

        if (equipmentSnapshot != null)
        {
            equipment.PublishRestoreCandidate(
                equipment.BuildRestoreCandidate(equipmentSnapshot));
        }
    }

    private void DisableBrainForDeterministicHauling(CharacterActor hauler)
    {
        isolatedAiPauseStates.Clear();
        verificationActors.Clear();
        foreach (CharacterActor actor in CharacterActorCollection.DistinctByGameObject(
                     UnityEngine.Object.FindObjectsByType<CharacterActor>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None)))
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            verificationActors.Add(actor);
            isolatedAiPauseStates.Add(actor, actor.IsAiPaused());
            actor.SetAiPaused(true);
            AIBrain brain = actor != null ? actor.Brain : null;
            brain?.StopAllAiForLifecycleTransition(
                "qa-physical-logistics-isolation");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
            actor.GetComponent<AbilityShopping>()?.StopShopping(
                "qa-physical-logistics-isolation");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "qa-physical-logistics-isolation");
        }
    }

    private void RestoreBrain()
    {
        foreach (KeyValuePair<CharacterActor, bool> pair in isolatedAiPauseStates)
        {
            if (pair.Key != null)
            {
                pair.Key.SetAiPaused(pair.Value);
            }
        }
        isolatedAiPauseStates.Clear();
        verificationActors.Clear();
    }

    private void ConfigureVerificationDebugMode()
    {
        originalFreezeNeeds = debugMode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds);
        originalFriendlyInvincible = debugMode.IsCheatEnabled(DungeonDebugCheat.FriendlyInvincible);
        debugMode.SetCheat(DungeonDebugCheat.FreezeNeeds, true);
        debugMode.SetCheat(DungeonDebugCheat.FriendlyInvincible, true);
    }

    private void RestoreVerificationDebugMode()
    {
        if (debugMode == null)
        {
            return;
        }

        debugMode.SetCheat(DungeonDebugCheat.FreezeNeeds, originalFreezeNeeds);
        debugMode.SetCheat(DungeonDebugCheat.FriendlyInvincible, originalFriendlyInvincible);
    }

    private Facility CreateInjectedFacility(
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        BuildingSO asset,
        Vector2Int position,
        string objectName,
        bool registerOnGrid = false)
    {
        if (asset == null)
        {
            return null;
        }

        GameObject obj = new GameObject(objectName);
        temporaryObjects.Add(obj);
        Facility facility = obj.AddComponent<Facility>();
        InjectGameObject(scope, obj);
        facility.SetGrid(grid);
        facility.Initialization(asset, position);
        if (registerOnGrid
            && (grid == null
                || !grid.RegisterOccupant(
                    facility,
                    asset.Placement.Layer,
                    asset.GetGridPosList(position),
                    asset.Placement.IsMovement)))
        {
            temporaryObjects.Remove(obj);
            Destroy(obj);
            return null;
        }
        Vector3 world = grid != null ? grid.GetWorldPos(position) : (Vector3)(Vector2)position;
        obj.transform.position = new Vector3(world.x, world.y, obj.transform.position.z);
        return facility;
    }

    private static bool TryFindRegisterablePosition(
        Grid grid,
        BuildingSO asset,
        IReadOnlyList<Vector2Int> candidates,
        out Vector2Int position)
    {
        position = default;
        if (grid == null || asset == null || candidates == null)
        {
            return false;
        }

        foreach (Vector2Int candidate in candidates)
        {
            IReadOnlyList<Vector2Int> footprint = asset.GetGridPosList(candidate);
            if (footprint.All(cell => grid.GetGridCell(cell)?.CanOccupy(
                    asset.Placement.Layer) == true))
            {
                position = candidate;
                return true;
            }
        }
        return false;
    }

    private static BuildingSO FindWarehouseAsset()
    {
        return FindBuildingAsset(asset => asset.GetStorageCapacity() > 0 && asset.StoresAllCategories())
            ?? AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/P1/P1_Warehouse.asset");
    }

    private static BuildingSO FindCraftBenchAsset()
    {
        return FindBuildingAsset(asset =>
        {
            BuildingEquipmentCraftingAbility ability = asset.GetAbility<BuildingEquipmentCraftingAbility>();
            return ability != null && ability.CraftableEquipmentIds.Contains(DaggerId, StringComparer.Ordinal);
        });
    }

    private static BuildingSO CreateMaintenanceAsset()
    {
        BuildingSO asset = ScriptableObject.CreateInstance<BuildingSO>();
        asset.id = 99122;
        asset.objectName = "QA 장비 수리대";
        asset.width = 1;
        asset.height = 1;
        asset.layer = GridLayer.Building;
        asset.category = BuildingCategory.Production;
        asset.unlocked = true;
        asset.Facility = new FacilityData
        {
            roles = FacilityRole.Logistics,
            capacity = 1,
            useDuration = 1.5f,
            requiredWorkers = 1,
            disabledWhenDamaged = true
        };
        asset.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Repair });
        asset.AbilityModules.Add(new BuildingEquipmentMaintenanceAbility
        {
            workSpeedMultiplier = 1f,
            simultaneousRepairSlots = 1
        });
        return asset;
    }

    private static bool SeedStoredCraftMaterial(
        IWorldItemStackRuntime itemRuntime,
        IResourceEconomyContentCatalog economyCatalog,
        Facility warehouse,
        string materialId,
        int amount,
        out string details)
    {
        details = string.Empty;
        if (itemRuntime == null
            || economyCatalog == null
            || warehouse == null
            || !economyCatalog.TryGetMaterial(
                materialId,
                out CraftMaterialDefinitionSO material))
        {
            details = $"material missing: {materialId}";
            return false;
        }

        string destinationId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        bool spawned = itemRuntime.SpawnItemAt(
            material.ItemId,
            amount,
            warehouse.centerPos,
            WorldItemStackState.Stored,
            destinationId,
            out int spawnedAmount);
        details = $"item={material.ItemId}; amount={spawnedAmount}; destination={destinationId}";
        return spawned && spawnedAmount == amount;
    }

    private static BuildingSO FindBuildingAsset(Func<BuildingSO, bool> predicate)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            if (asset != null && predicate(asset))
            {
                return asset;
            }
        }

        return null;
    }

    private static IReadOnlyList<Vector2Int> FindReachableCells(Grid grid, Vector2Int actorPos, int count)
    {
        return grid.SearchPath(actorPos)
            .GetReachablePositions()
            .Where(pos => grid.IsValidGridPos(pos) && grid.IsWalkable(pos))
            .Where(pos => Mathf.Abs(pos.x - actorPos.x) + Mathf.Abs(pos.y - actorPos.y) <= 12)
            .Distinct()
            .OrderBy(pos => Mathf.Abs(pos.x - actorPos.x) + Mathf.Abs(pos.y - actorPos.y))
            .Skip(1)
            .Take(count)
            .ToArray();
    }

    private static CharacterActor FindHauler()
    {
        return CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null && !actor.IsDead)
            .OrderByDescending(actor => actor.TryGetAbility(out AbilityWork _))
            .ThenBy(actor => actor.Identity != null && actor.Identity.Role == CharacterRole.Owner ? 1 : 0)
            .FirstOrDefault(actor =>
                actor.TryGetAbility(out AbilityMove _)
                && (actor.TryGetAbility(out AbilityWork _)
                    || actor.Identity != null && actor.Identity.Role == CharacterRole.Owner));
    }

    private static void ClearInventory(WarehouseInventory inventory)
    {
        if (inventory == null)
        {
            return;
        }

        foreach (KeyValuePair<StockCategory, int> pair in inventory.EnumerateStock().ToArray())
        {
            inventory.ConsumePhysicalStockForTest(pair.Key, pair.Value);
        }
    }

    private static int GetTotalWarehouseStock(StockCategory category)
    {
        return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .OfType<IWarehouseFacility>()
            .Where(facility => facility != null && facility.Inventory != null)
            .Select(facility => facility.Inventory)
            .Distinct()
            .Sum(inventory => inventory.GetStock(category));
    }

    private static int GetStoredItemQuantity(
        IWorldItemStackRuntime itemRuntime,
        string itemId,
        Vector2Int warehousePosition)
    {
        return itemRuntime?.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Stored
                && stack.Position == warehousePosition
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => Mathf.Max(0, stack.Quantity)) ?? 0;
    }

    private string DescribeHaulState(IWorldItemStackRuntime itemRuntime, CharacterActor hauler)
    {
        AbilityHaul haul = hauler != null ? hauler.GetComponent<AbilityHaul>() : null;
        IWorldItemHaulPlanningService planning = Resolve<IWorldItemHaulPlanningService>(FindScope());
        string preview = "unavailable";
        if (planning != null && hauler != null)
        {
            bool available = planning.TryPreviewBestPlan(
                hauler,
                out WorldItemHaulPlan previewPlan,
                out string previewFailure);
            preview = available
                ? DescribePreviewPlan(previewPlan)
                : previewFailure;
        }
        string canStartReason = "unavailable";
        bool canStart = haul != null && haul.CanStartHauling(out canStartReason);
        bool runtimeAvailable = hauler != null && itemRuntime?.HasAvailableHaulJob(hauler) == true;
        return $"actor={hauler?.name}; pos={hauler?.GetNowXY().ToString() ?? "<none>"}; "
            + $"phase={hauler?.Brain?.CurrentActionPhase ?? "<none>"}"
            + $"/{hauler?.Brain?.CurrentActionPhaseDetail ?? "<none>"}; "
            + $"haul={haul?.CurrentPlanSummary ?? "<none>"}; "
            + $"unload={haul?.CurrentUnloadReason ?? "<none>"}; "
            + $"haulFailure={haul?.LastFailureReason ?? "<none>"}; "
            + $"path={haul?.ActivePathDebug ?? "<none>"}; "
            + $"preview={preview}; "
            + $"runtimeAvailable={runtimeAvailable}; canStart={canStart}:{canStartReason}; "
            + $"brainFailure={hauler?.Brain?.LastActionFailure.ToString() ?? "<none>"}; "
            + $"carry={DescribeCarry(hauler, itemRuntime)}; "
            + $"stacks={DescribeStacks(itemRuntime)}";
    }

    private static string DescribePreviewPlan(WorldItemHaulPlan plan)
    {
        if (plan == null)
        {
            return "null-plan";
        }

        string reservations = string.Join(
            ",",
            plan.ReservedStackQuantities.Select(reservation =>
                $"{reservation.StackId}:{reservation.ItemId}x{reservation.Quantity}"
                + $"->{reservation.DestinationKind}:{reservation.DestinationId}"));
        return $"valid={plan.IsValid},priority={plan.IsPriority},weight={plan.TotalWeight:0.###},"
            + $"destination={plan.PrimaryDestination}:{plan.PrimaryDestinationId},"
            + $"reservations=[{reservations}]";
    }

    private static string DescribeCarry(CharacterActor hauler, IWorldItemStackRuntime itemRuntime)
    {
        CharacterCarryInventory carry = hauler != null ? CharacterCarryInventory.Ensure(hauler) : null;
        if (carry == null)
        {
            return "none";
        }

        string itemSummary = string.Join(",", carry.Items.Select(
            item => $"{item.itemId}x{item.quantity}"));
        return $"{carry.GetCurrentWeight(itemRuntime?.CatalogProvider):0.##}/"
            + $"{carry.GetBaseCarryLimit():0.##}/"
            + $"{carry.GetMaxAllowedWeight(itemRuntime?.HaulingSettingsProvider):0.##}kg"
            + (itemSummary.Length > 0 ? " " + itemSummary : string.Empty);
    }

    private static string DescribeStacks(IWorldItemStackRuntime itemRuntime)
    {
        if (itemRuntime == null)
        {
            return "no runtime";
        }

        return string.Join(" | ", itemRuntime.GetAllStacks()
            .Take(12)
            .Select(stack => $"{stack.StackId}:{stack.ItemId}x{stack.Quantity}:{stack.State}:"
                + $"dest={stack.DestinationId}:src={stack.SourceStorageDestinationId}:pos={stack.Position}"));
    }

    private static string DescribeWarehouse(IWarehouseFacility warehouse) =>
        warehouse is BuildableObject building
            ? $"{warehouse.PersistentInstanceId.Value}@{building.centerPos}"
            : warehouse?.PersistentInstanceId.Value ?? "missing";

    private IEnumerator CaptureScreen(string path)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        if (capture == null)
        {
            Check(false, "SCREEN_CAPTURE", "capture returned null");
            yield break;
        }

        byte[] bytes = capture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Check(bytes.Length > 1000, "SCREEN_CAPTURE_NONBLANK", $"{path}; bytes={bytes.Length}");
        Destroy(capture);
    }

    private void SetupInput()
    {
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        CreateVerificationMouse();
    }

    private void CreateVerificationMouse()
    {
        verificationMouse = InputSystem.AddDevice<Mouse>($"PhysicalItemLogisticsVerificationMouse{++verificationMouseSerial}");
        InputSystem.EnableDevice(verificationMouse);
        verificationMouse.MakeCurrent();
        InputState.Change(verificationMouse, new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) });
        InputSystem.Update();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("QA_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static string GetVisibleTextSample()
    {
        return string.Join(" || ", Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(text.text))
            .Select(text => Compact(text.text))
            .Take(16));
    }

    private static void InjectGameObject(DungeonRuntimeLifetimeScope scope, GameObject target)
    {
        if (scope == null || scope.Container == null || target == null)
        {
            return;
        }

        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
    }

    private sealed class SingleWarehouseWorldRegistry : ICharacterAiWorldRegistry
    {
        private readonly ICharacterAiWorldRegistry inner;
        private readonly IReadOnlyList<IWarehouseFacility> warehouses;

        internal SingleWarehouseWorldRegistry(
            ICharacterAiWorldRegistry inner,
            IWarehouseFacility warehouse)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            warehouses = warehouse == null
                ? Array.Empty<IWarehouseFacility>()
                : new[] { warehouse };
        }

        public int Version => inner.Version;
        public int CharacterVersion => inner.CharacterVersion;
        public int LifetimeCharacterVersion => inner.LifetimeCharacterVersion;
        public int WildlifeVersion => inner.WildlifeVersion;
        public int BuildingVersion => inner.BuildingVersion;
        public int WarehouseVersion => unchecked(inner.WarehouseVersion + 1);
        public int RetailVersion => inner.RetailVersion;
        public IReadOnlyList<CharacterActor> Characters => inner.Characters;
        public IReadOnlyList<CharacterActor> AllCharacters => inner.AllCharacters;
        public IReadOnlyList<WildlifeActor> Wildlife => inner.Wildlife;
        public IReadOnlyList<BuildableObject> Buildings => inner.Buildings;
        public IReadOnlyList<IWarehouseFacility> Warehouses => warehouses;
        public IReadOnlyList<IRetailFacility> RetailFacilities =>
            inner.RetailFacilities;

        public void RegisterCharacter(CharacterActor actor) =>
            inner.RegisterCharacter(actor);
        public void UnregisterCharacter(CharacterActor actor) =>
            inner.UnregisterCharacter(actor);
        public void RegisterCharacterLifetime(CharacterActor actor) =>
            inner.RegisterCharacterLifetime(actor);
        public void UnregisterCharacterLifetime(CharacterActor actor) =>
            inner.UnregisterCharacterLifetime(actor);
        public void RegisterWildlife(WildlifeActor actor) =>
            inner.RegisterWildlife(actor);
        public void UnregisterWildlife(WildlifeActor actor) =>
            inner.UnregisterWildlife(actor);
        public void RegisterBuilding(BuildableObject building) =>
            inner.RegisterBuilding(building);
        public void UnregisterBuilding(BuildableObject building) =>
            inner.UnregisterBuilding(building);
        public int ReleaseTransientBuildingOwnership(
            IBuildingVisitorPort visitor,
            string reason) =>
            inner.ReleaseTransientBuildingOwnership(visitor, reason);
        public int GetTransientBuildingOwnershipCount(CharacterId characterId) =>
            inner.GetTransientBuildingOwnershipCount(characterId);
        public void RegisterWarehouse(IWarehouseFacility warehouse) =>
            inner.RegisterWarehouse(warehouse);
        public void UnregisterWarehouse(IWarehouseFacility warehouse) =>
            inner.UnregisterWarehouse(warehouse);
        public void SetGrid(Grid grid) => inner.SetGrid(grid);
        public bool TryGetGrid(out Grid grid) => inner.TryGetGrid(out grid);
        public bool TryGetSessionState(out GameSessionState data) =>
            inner.TryGetSessionState(out data);
        public void Clear() => inner.Clear();
    }

    private T Resolve<T>(DungeonRuntimeLifetimeScope scope) where T : class
    {
        try
        {
            return scope != null && scope.Container != null ? scope.Container.Resolve<T>() : null;
        }
        catch (Exception exception)
        {
            report.Add(
                $"[DI-ERROR] RESOLVE {typeof(T).FullName}: "
                + $"{exception.GetType().Name}: {exception.Message}");
            return null;
        }
    }

    private static bool TryFindReadyComposition(
        out DungeonRuntimeLifetimeScope scope,
        out OwnerRunManager ownerManager,
        out string detail)
    {
        scope = FindScope();
        ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (scope == null || scope.Container == null)
        {
            detail = "gameplay LifetimeScope/container is not ready";
            return false;
        }
        if (ownerManager == null)
        {
            detail = "authored OwnerRunManager is not ready";
            return false;
        }

        try
        {
            IOwnerRunManagerProvider provider =
                scope.Container.Resolve<IOwnerRunManagerProvider>();
            if (provider == null
                || !provider.TryGetManager(out OwnerRunManager provided)
                || !ReferenceEquals(ownerManager, provided))
            {
                detail = "owner provider does not expose the authored manager";
                return false;
            }
        }
        catch (Exception exception)
        {
            detail = $"owner provider resolve pending: {exception.GetType().Name}: {exception.Message}";
            return false;
        }

        detail = "scope/container and authored owner provider match";
        return true;
    }

    private static int CountPreparedStaff() => CharacterActorCollection
        .DistinctByGameObject(UnityEngine.Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None))
        .Count(actor => actor != null
            && !actor.IsDead
            && actor.Identity != null
            && actor.Identity.PersistentId.StartsWith(
                "character:staff:",
                StringComparison.Ordinal));

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private bool Check(bool condition, string key, string detail)
    {
        string prefix = $"[{(condition ? "PASS" : "FAIL")}] {key}";
        report.Add(string.IsNullOrEmpty(detail) ? prefix : $"{prefix} {detail}");
        if (!condition)
        {
            failures.Add($"{key}: {detail}");
        }

        return condition;
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace)
                ? condition
                : condition + "\n" + stackTrace);
        }
    }

    private void Finish()
    {
        Cleanup();
        Application.logMessageReceived -= OnLogMessageReceived;
        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        bool passed = failures.Count == 0 && capturedErrors.Count == 0 && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        string reportPath = l02Only
            ? PhysicalItemLogisticsPlayModeVerifier.L02ReportPath
            : productionInputMassOnly
                ? PhysicalItemLogisticsPlayModeVerifier.ProductionInputMassReportPath
            : preparedOutputWarehouseOnly
                ? PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseReportPath
            : equipmentRepairOnly
                ? PhysicalItemLogisticsPlayModeVerifier.EquipmentRepairReportPath
            : constructionOnly
                ? PhysicalItemLogisticsPlayModeVerifier.ConstructionReportPath
                : PhysicalItemLogisticsPlayModeVerifier.ReportPath;
        File.WriteAllText(reportPath, string.Join("\n", report));
        File.Delete(PhysicalItemLogisticsPlayModeVerifier.RequestPath);
        File.Delete(PhysicalItemLogisticsPlayModeVerifier.ConstructionRequestPath);
        File.Delete(PhysicalItemLogisticsPlayModeVerifier.L02RequestPath);
        File.Delete(
            PhysicalItemLogisticsPlayModeVerifier.ProductionInputMassRequestPath);
        File.Delete(
            PhysicalItemLogisticsPlayModeVerifier.EquipmentRepairRequestPath);
        File.Delete(
            PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseRequestPath);

        if (passed)
        {
            Debug.Log("Physical item logistics PlayMode verification passed. "
                + reportPath);
        }
        else
        {
            Debug.LogError("Physical item logistics PlayMode verification failed. "
                + reportPath);
        }

        EditorApplication.ExitPlaymode();
        Destroy(gameObject);
    }

    private void Cleanup()
    {
        RestoreVerificationDebugMode();
        foreach (GameObject obj in temporaryObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        temporaryObjects.Clear();
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
        }

        Time.timeScale = originalTimeScale;
        RestoreBrain();
    }

    private static string Compact(IEnumerable<string> values)
    {
        return Compact(string.Join(" | ", values ?? Array.Empty<string>()));
    }

    private static string Compact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<none>";
        }

        return value.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
#endif
