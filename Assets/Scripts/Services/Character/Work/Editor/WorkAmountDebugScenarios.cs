#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
        RunScenario("save V14 carries work orders", VerifySaveV12CarriesWorkOrders, errors);
        RunScenario("configured work amount fallback", VerifyConfiguredWorkAmountFallback, errors);
        RunScenario("construction order lifecycle", VerifyConstructionOrderLifecycle, errors);

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

    private static bool VerifySaveV12CarriesWorkOrders()
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
        return DungeonGameSaveData.CurrentVersion == 15
            && save.version == DungeonGameSaveData.CurrentVersion
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
                && configured.GetConstructionMaterials().TryGetValue(StockCategory.General, out int configuredMaterials)
                && configuredMaterials == 4;

            bool fallbackValid = fallback.GetRequiredWork(BuiltInWorkTypeIds.Construct) > 0f
                && fallback.GetRequiredWork(BuiltInWorkTypeIds.Repair) > 0f;
            return configuredValid && fallbackValid;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(configured);
            UnityEngine.Object.DestroyImmediate(fallback);
        }
    }

    private static bool VerifyConstructionOrderLifecycle()
    {
        BuildingSO building = CreateTestBuilding(91003, "공사 주문 테스트 시설", 2, 1, 5f, 2);
        GameObject siteObject = new GameObject("WorkAmountConstructionSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        FakeWorldItemStackRuntime itemRuntime = new FakeWorldItemStackRuntime();
        WorkOrderRuntime runtime = new WorkOrderRuntime(
            new NoGridProvider(),
            itemRuntime,
            new SingleBuildingLookup(building));
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
                && itemRuntime.Requested.TryGetValue(StockCategory.General, out int requested)
                && requested == 2;
            if (!waiting)
            {
                return false;
            }

            if (runtime.RefreshMaterialsReady(site))
            {
                return false;
            }

            itemRuntime.AddFacilityBuffer(order.MaterialDestinationId, StockCategory.General, 2);
            bool ready = runtime.RefreshMaterialsReady(site)
                && runtime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out order)
                && order.Status == WorkOrderStatus.Ready
                && order.DeliveredMaterials.TryGetValue(StockCategory.General, out int delivered)
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
            building.AbilityModules.Add(new BuildingWorkAmountAbility
            {
                constructionWorkRequired = Mathf.Max(0.1f, constructionWork),
                repairWorkRequired = 3f,
                cleanWorkRequired = 2f,
                researchWorkRequired = 6f,
                constructionMaterialCategory = StockCategory.General,
                constructionMaterialAmount = materialAmount,
                materialUnitsPerConstructionCost = 0f
            });
        }

        return building;
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

        public readonly Dictionary<StockCategory, int> Requested = new Dictionary<StockCategory, int>();

        public IDungeonItemCatalogProvider CatalogProvider => null;
        public IItemHaulingSettingsProvider HaulingSettingsProvider => null;
        public bool StoredItemMarkersVisible => false;
        public int ItemStackVersion => 0;
        public int HaulJobVersion => 0;

        public DungeonPhysicalItemSaveData Capture() => new DungeonPhysicalItemSaveData();
        public void Restore(DungeonPhysicalItemSaveData snapshot) { }
        public void SetStoredItemMarkersVisible(bool visible) { }
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
            requested = Mathf.Max(0, amount);
            failureReason = string.Empty;
            Requested[category] = Requested.TryGetValue(category, out int current)
                ? current + requested
                : requested;
            return requested > 0;
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

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();

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
        public bool PrioritizeHaul(string stackId) => false;
        public bool DeleteStack(string stackId) => false;
        public bool TryConsumeStackQuantity(
            string stackId,
            int quantity,
            out WorldItemStackSnapshot consumed)
        {
            consumed = null;
            return false;
        }

        public bool SetEmergencyButcheryAllowed(string stackId, bool allowed) => false;
        public int RemoveStacksByStateAndDestination(WorldItemStackState state, string destinationId) => 0;

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
    }
}
#endif
