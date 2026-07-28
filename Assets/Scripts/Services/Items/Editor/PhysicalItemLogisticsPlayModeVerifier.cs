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

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
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

        if (change != PlayModeStateChange.EnteredPlayMode || runnerCreated || !File.Exists(RequestPath))
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
    private const string DaggerId = "weapon:dagger";
    private const string BreastplateId = "armor:breastplate";
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
    private bool originalBrainEnabled;
    private AIBrain disabledBrain;
    private float originalTimeScale;
    private IWorldItemStackRuntime itemRuntime;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
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
        Check(grid != null, "GRID_READY", "grid resolved");
        if (scope == null
            || itemRuntime == null
            || workOrderRuntime == null
            || equipment == null
            || equipmentMaintenance == null
            || economyCatalog == null
            || preparation == null
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
        DisableBrainForDeterministicHauling(hauler);

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
            Check(warehouse.Inventory.Deposit(StockCategory.General, 20) == 20
                    && warehouse.Inventory.Deposit(StockCategory.Weapon, 20) == 20
                    && warehouse.Inventory.Deposit(StockCategory.Food, 5) == 5,
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
            warehouse.Inventory.Withdraw(StockCategory.Food, 5);

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
                scope,
                grid,
                hauler,
                warehouse,
                warehouse.centerPos);
            VerifyExpeditionPacking(preparation, itemRuntime, warehouse);
            yield return VerifyCarryUi(itemRuntime, hauler);
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
            DungeonItemCatalogSO.StockItemId(StockCategory.Food),
            3,
            itemPosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int amount);
        Check(spawned && amount == 3, "LOOSE_STACK_SPAWNED", $"pos={itemPosition}; amount={amount}");

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            Check(action.CanStart(hauler), "AI_HAUL_CAN_START_WAREHOUSE", DescribeHaulState(itemRuntime, hauler));
            yield return RunHaul(action, hauler, () =>
                GetTotalWarehouseStock(StockCategory.Food) >= before + 3
                && !itemRuntime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.ItemId, DungeonItemCatalogSO.StockItemId(StockCategory.Food), StringComparison.Ordinal)));
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
            yield return RunHaul(action, hauler, () => itemRuntime.GetAllStacks().Any(stack =>
                stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                && stack.Quantity >= 2));
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
        building.AbilityModules.Add(new BuildingWorkAmountAbility
        {
            constructionWorkRequired = 5f,
            repairWorkRequired = 3f,
            cleanWorkRequired = 2f,
            researchWorkRequired = 6f,
            constructionMaterialCategory = StockCategory.General,
            constructionMaterialAmount = materialAmount,
            materialUnitsPerConstructionCost = 0f
        });

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
        try
        {
            int generalBefore = warehouse.Inventory.GetStock(StockCategory.General);
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
            Check(warehouse.Inventory.GetStock(StockCategory.General) == generalBefore,
                "CONSTRUCTION_STOCK_HELD_UNTIL_PICKUP",
                $"general={generalBefore}->{warehouse.Inventory.GetStock(StockCategory.General)}");
            Check(!itemRuntime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)),
                "CONSTRUCTION_REQUEST_NO_LOOSE_PILE",
                DescribeStacks(itemRuntime));
            Check(itemRuntime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Stored
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
                    && deliveredOrder.DeliveredMaterials.TryGetValue(
                        StockCategory.General,
                        out int deliveredAmount)
                    && deliveredAmount >= materialAmount);
            }
            finally
            {
                Destroy(action);
            }

            Check(warehouse.Inventory.GetStock(StockCategory.General) == generalBefore - materialAmount,
                "CONSTRUCTION_STOCK_WITHDRAWN_ON_PICKUP",
                $"general={generalBefore}->{warehouse.Inventory.GetStock(StockCategory.General)}");
            Check(workOrderRuntime.RefreshMaterialsReady(site)
                    && workOrderRuntime.TryGetOrderFor(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        out order)
                    && order.Status == WorkOrderStatus.Ready
                    && order.DeliveredMaterials.TryGetValue(StockCategory.General, out int delivered)
                    && delivered == materialAmount,
                "CONSTRUCTION_READY_AFTER_PHYSICAL_DELIVERY",
                order != null
                    ? $"status={order.Status}; delivered={order.DeliveredMaterials.GetValueOrDefault(StockCategory.General)}"
                    : "order missing");
        }
        finally
        {
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
            ModularFacilityRuntimeEffects.ApplyWorkCompleted(hauler, bench, BuiltInWorkTypeIds.Craft);
            yield return null;
        }

        string outputItemId = DungeonItemCatalogSO.EquipmentItemId(DaggerId);
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
                BreastplateId,
                CombatEquipmentQuality.Normal,
                CombatEquipmentWorldState.Stored,
                "material:blacksteel");
            string warehouseDestinationId =
                $"{WorldItemStackRuntime.WarehouseStorageDestinationPrefix}"
                + $"{warehouse.GridId}:{warehouse.centerPos.x}:{warehouse.centerPos.y}";
            bool stackSpawned = itemRuntime.SpawnUniqueItemAt(
                DungeonItemCatalogSO.EquipmentItemId(BreastplateId),
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
                        order.facilityDestinationId,
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
                        order.facilityDestinationId,
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
                                        BreastplateId,
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

    private void VerifyExpeditionPacking(
        IOffensePreparationService preparation,
        IWorldItemStackRuntime itemRuntime,
        Facility warehouse)
    {
        warehouse.Inventory.Deposit(StockCategory.Food, 4);
        OffenseSupplyLoadout loadout = new OffenseSupplyLoadout();
        loadout.Add(OffenseSupplyType.Rations, 2);
        string packageId = "qa-package-" + Guid.NewGuid().ToString("N");
        bool committed = preparation.TryCommitLoadout(
            loadout,
            new OffenseExpeditionPreparation(supplyCapacity: 6),
            packageId,
            out string message);
        Check(committed,
            "EXPEDITION_SUPPLIES_PACKED",
            $"message={message}; stacks={DescribeStacks(itemRuntime)}");
        int packed = itemRuntime.GetAllStacks().Where(stack =>
                stack.State == WorldItemStackState.ExpeditionPacked
                && string.Equals(stack.DestinationId, packageId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        Check(packed == 2,
            "EXPEDITION_PACKED_STACK_VISIBLE",
            $"packed={packed}; package={packageId}");
        preparation.ConsumePackedSupplies(packageId);
        bool removed = !itemRuntime.GetAllStacks().Any(stack =>
            stack.State == WorldItemStackState.ExpeditionPacked
            && string.Equals(stack.DestinationId, packageId, StringComparison.Ordinal));
        Check(removed, "EXPEDITION_PACKED_STACK_CONSUMED", DescribeStacks(itemRuntime));
    }

    private IEnumerator VerifyCarryUi(IWorldItemStackRuntime itemRuntime, CharacterActor hauler)
    {
        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(hauler);
        string failure = string.Empty;
        Check(carry != null
                && carry.TryAdd(
                    "qa-carry-ui",
                    DungeonItemCatalogSO.StockItemId(StockCategory.Weapon),
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
        Check(sample.Contains("kg", StringComparison.OrdinalIgnoreCase)
                && sample.Contains("/", StringComparison.Ordinal),
            "CARRY_UI_WEIGHT_VISIBLE",
            sample);
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
                yield return null;
                if (completed())
                {
                    Check(true, "AI_HAUL_COMPLETED", $"elapsed={Time.realtimeSinceStartup - startedAt:0.0}s");
                    yield break;
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
        CharacterSummeryInfo summary = UnityEngine.Object.FindFirstObjectByType<CharacterSummeryInfo>(
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
            equipment.Restore(equipmentSnapshot);
        }
    }

    private void DisableBrainForDeterministicHauling(CharacterActor hauler)
    {
        disabledBrain = hauler != null ? hauler.Brain : null;
        if (disabledBrain == null)
        {
            return;
        }

        originalBrainEnabled = disabledBrain.enabled;
        disabledBrain.enabled = false;
    }

    private void RestoreBrain()
    {
        if (disabledBrain != null)
        {
            disabledBrain.enabled = originalBrainEnabled;
        }
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

        string destinationId =
            $"{WorldItemStackRuntime.WarehouseStorageDestinationPrefix}"
            + $"{warehouse.GridId}:{warehouse.centerPos.x}:{warehouse.centerPos.y}";
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
            inventory.Withdraw(pair.Key, pair.Value);
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

    private static string DescribeHaulState(IWorldItemStackRuntime itemRuntime, CharacterActor hauler)
    {
        AbilityHaul haul = hauler != null ? hauler.GetComponent<AbilityHaul>() : null;
        return $"actor={hauler?.name}; pos={hauler?.GetNowXY().ToString() ?? "<none>"}; "
            + $"phase={hauler?.Brain?.CurrentActionPhase ?? "<none>"}"
            + $"/{hauler?.Brain?.CurrentActionPhaseDetail ?? "<none>"}; "
            + $"haul={haul?.CurrentPlanSummary ?? "<none>"}; "
            + $"unload={haul?.CurrentUnloadReason ?? "<none>"}; "
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
        File.WriteAllText(PhysicalItemLogisticsPlayModeVerifier.ReportPath, string.Join("\n", report));
        File.Delete(PhysicalItemLogisticsPlayModeVerifier.RequestPath);

        if (passed)
        {
            Debug.Log("Physical item logistics PlayMode verification passed. "
                + PhysicalItemLogisticsPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Physical item logistics PlayMode verification failed. "
                + PhysicalItemLogisticsPlayModeVerifier.ReportPath);
        }

        EditorApplication.ExitPlaymode();
        Destroy(gameObject);
    }

    private void Cleanup()
    {
        RestoreBrain();
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
