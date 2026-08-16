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
    public const string CarryCapturePath = "Artifacts/QA/physical-item-carry-ui.png";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;

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

    private static void OnEditorUpdate()
    {
        if ((!File.Exists(RequestPath) && !File.Exists(ConstructionRequestPath))
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode
            || runnerCreated
            || !File.Exists(RequestPath) && !File.Exists(ConstructionRequestPath))
        {
            return;
        }

        runnerCreated = true;
        new GameObject("Physical Item Logistics PlayMode Verification Runner")
            .AddComponent<PhysicalItemLogisticsPlayModeVerificationRunner>();
    }
}

public sealed class PhysicalItemLogisticsPlayModeVerificationRunner : MonoBehaviour
{
    private const string PreservedRationItemId = "food:preserved-ration";
    private const string DaggerItemId = "equipment-item:weapon:dagger";
    private const string DaggerId = "weapon:dagger";
    private const string RepairEquipmentId = "shield:wood";
    private const float HaulTimeoutSeconds = 18f;

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
    private IDungeonDebugModeService debugMode;
    private bool originalFreezeNeeds;
    private bool originalFriendlyInvincible;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        constructionOnly = File.Exists(PhysicalItemLogisticsPlayModeVerifier.ConstructionRequestPath);
        Application.logMessageReceived += OnLogMessageReceived;
        EnsureEventSystem();
        SetupInput();
        originalTimeScale = Time.timeScale;
        Time.timeScale = 8f;

        yield return null;
        yield return null;

        DungeonRuntimeLifetimeScope scope = FindScope();
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

        try
        {
            itemRuntime.Restore(new DungeonPhysicalItemSaveData());
            CharacterCarryInventory.Ensure(hauler)?.RemoveAllItems();

            Vector2Int actorPos = hauler.GetNowXY();
            IReadOnlyList<Vector2Int> positions = FindReachableCells(grid, actorPos, 8);
            Check(positions.Count >= 3, "REACHABLE_TEST_CELLS", $"count={positions.Count}; actor={actorPos}");
            if (positions.Count < 3)
            {
                Finish();
                yield break;
            }

            BuildingSO warehouseAsset = FindWarehouseAsset();
            BuildingSO benchAsset = FindCraftBenchAsset();
            Facility warehouse = CreateInjectedFacility(scope, grid, warehouseAsset, positions[0], "QA_Physical_Logistics_Warehouse");
            Facility bench = CreateInjectedFacility(scope, grid, benchAsset, positions[1], "QA_Physical_Logistics_Bench");
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
                    positions[2]);
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
            yield return VerifyLooseStackToWarehouse(itemRuntime, grid, hauler, warehouse, positions[2]);
            yield return VerifyFacilityInputDelivery(itemRuntime, hauler, warehouse, bench);
            yield return VerifyConstructionMaterialDelivery(
                itemRuntime,
                workOrderRuntime,
                scope,
                grid,
                hauler,
                warehouse,
                positions[2]);
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
            yield return VerifyCarryUi(itemRuntime, hauler);
            }
        }
        finally
        {
            RestoreRuntimeState(itemRuntime, equipment);
        }

        yield return null;
        Finish();
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
            bool repairPlanReady = haulPlanning != null
                && haulPlanning.TryPreviewBestPlan(
                    hauler,
                    out repairPreview,
                    out repairPreviewFailure)
                && repairPreview != null
                && repairPreview.IsValid
                && string.Equals(
                    repairPreview.PrimaryDestinationId,
                    order.FacilityDestinationId,
                    StringComparison.Ordinal);
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
                    DaggerItemId,
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
        if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
        {
            string fastCommit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            report.Add("[INFO] FAST_PARTY_COMMIT " + fastCommit);
            for (int i = 0; i < 8; i++)
            {
                yield return null;
            }
        }

        ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "RUN_READY",
            ownerManager != null && ownerManager.CurrentOwnerActor != null
                ? $"owner={ownerManager.CurrentOwnerActor.name}"
                : "owner missing");
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
        string objectName)
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
        Vector3 world = grid != null ? grid.GetWorldPos(position) : (Vector3)(Vector2)position;
        obj.transform.position = new Vector3(world.x, world.y, obj.transform.position.z);
        return facility;
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

    private static string DescribeHaulState(IWorldItemStackRuntime itemRuntime, CharacterActor hauler)
    {
        AbilityHaul haul = hauler != null ? hauler.GetComponent<AbilityHaul>() : null;
        return $"actor={hauler?.name}; pos={hauler?.GetNowXY().ToString() ?? "<none>"}; "
            + $"phase={hauler?.Brain?.CurrentActionPhase ?? "<none>"}"
            + $"/{hauler?.Brain?.CurrentActionPhaseDetail ?? "<none>"}; "
            + $"haul={haul?.CurrentPlanSummary ?? "<none>"}; "
            + $"unload={haul?.CurrentUnloadReason ?? "<none>"}; "
            + $"haulFailure={haul?.LastFailureReason ?? "<none>"}; "
            + $"path={haul?.ActivePathDebug ?? "<none>"}; "
            + $"brainFailure={hauler?.Brain?.LastActionFailure.ToString() ?? "<none>"}; "
            + $"carry={DescribeCarry(hauler, itemRuntime)}; "
            + $"stacks={DescribeStacks(itemRuntime)}";
    }

    private static string DescribeCarry(CharacterActor hauler, IWorldItemStackRuntime itemRuntime)
    {
        CharacterCarryInventory carry = hauler != null ? CharacterCarryInventory.Ensure(hauler) : null;
        if (carry == null)
        {
            return "none";
        }

        return $"{carry.GetCurrentWeight(itemRuntime?.CatalogProvider):0.##}/"
            + $"{carry.GetBaseCarryLimit():0.##}/"
            + $"{carry.GetMaxAllowedWeight(itemRuntime?.HaulingSettingsProvider):0.##}kg "
            + string.Join(",", carry.Items.Select(item => $"{item.itemId}x{item.quantity}"));
    }

    private static string DescribeStacks(IWorldItemStackRuntime itemRuntime)
    {
        if (itemRuntime == null)
        {
            return "no runtime";
        }

        return string.Join(" | ", itemRuntime.GetAllStacks()
            .Take(12)
            .Select(stack => $"{stack.StackId}:{stack.ItemId}x{stack.Quantity}:{stack.State}:dest={stack.DestinationId}:pos={stack.Position}"));
    }

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

    private static T Resolve<T>(DungeonRuntimeLifetimeScope scope) where T : class
    {
        try
        {
            return scope != null && scope.Container != null ? scope.Container.Resolve<T>() : null;
        }
        catch
        {
            return null;
        }
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private bool Check(bool condition, string key, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {key} {detail}");
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
        string reportPath = constructionOnly
            ? PhysicalItemLogisticsPlayModeVerifier.ConstructionReportPath
            : PhysicalItemLogisticsPlayModeVerifier.ReportPath;
        File.WriteAllText(reportPath, string.Join("\n", report));
        File.Delete(PhysicalItemLogisticsPlayModeVerifier.RequestPath);
        File.Delete(PhysicalItemLogisticsPlayModeVerifier.ConstructionRequestPath);

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
