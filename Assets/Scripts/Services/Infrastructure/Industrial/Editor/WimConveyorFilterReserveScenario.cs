#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

// Runs inside the industrial verifier's existing atomic world checkpoint/cleanup.
// Settings use actual main UI; the stalled overflow lot is an explicit fault checkpoint.
internal static class WimConveyorFilterReserveScenario
{
    internal static IEnumerator Run(DungeonRuntimeLifetimeScope scope, BuildableObject input,
        BuildableObject output, BuildableObject overflow, string outputDestination, List<string> report)
    {
        var query = scope.Container.Resolve<IConveyorInfrastructureQuery>();
        var commands = scope.Container.Resolve<IConveyorInfrastructureCommand>();
        var persistence = scope.Container.Resolve<IConveyorInfrastructurePersistence>();
        var catalog = scope.Container.Resolve<IDungeonItemCatalogProvider>();
        var transfers = scope.Container.Resolve<IItemTransferService>();
        var clock = scope.Container.Resolve<IGameClock>();
        string inputId = input.RequirePersistentInstanceId().Value;
        string overflowId = overflow.RequirePersistentInstanceId().Value;
        string prefix = "ConveyorChoices_" + inputId + "_";
        ConveyorNodeSaveData InputState() => persistence.Capture().nodes.Single(node => node.buildingInstanceId == inputId);
        Require(query.GetItemFilterChoices().Count == catalog.All.Count, "Item catalog coverage differs.");
        Require(query.GetStockCategoryFilterChoices().Count == catalog.All.Select(item => item.StockCategory).Distinct().Count(),
            "Category catalog coverage differs.");
        Require(query.GetMaterialFilterChoices().Count > 0, "Material choices missing.");
        var tabs = UnityEngine.Object.FindFirstObjectByType<UITabManager>();
        Require(tabs != null, "Main tab manager missing.");
        tabs.ToggleSelectButton(10);
        yield return null;
        Click(prefix + "Lists_Item");
        yield return null;
        string itemId = query.GetItemFilterChoices()[0].Id;
        Click(prefix + "Item_" + itemId);
        yield return null;
        Require(InputState().filter.itemIds.Contains(itemId), "Main item selection not persisted.");
        Click(prefix + "Lists_Category");
        yield return null;
        StockCategory category = query.GetStockCategoryFilterChoices()[0];
        Click(prefix + "Category_" + category);
        yield return null;
        Require(InputState().filter.stockCategories.Contains((int)category), "Main category selection not persisted.");
        Click(prefix + "Lists_Material");
        yield return null;
        string materialId = query.GetMaterialFilterChoices()[0].Id;
        Click(prefix + "Material_" + materialId);
        yield return null;
        Require(InputState().filter.materialIds.Contains(materialId), "Main material selection not persisted.");
        report.Add("main-EventSystem-item-category-material-selection=PASS");
        report.Add($"catalog-choices={query.GetItemFilterChoices().Count}/{query.GetStockCategoryFilterChoices().Count}/{query.GetMaterialFilterChoices().Count}");

        string configured = JsonUtility.ToJson(persistence.Capture());
        Require(commands.SetAdvancedFilter(input, new ConveyorFilterCriteria()).Succeeded, "Cannot clear filter.");
        persistence.Restore(persistence.PrepareRestore(JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(configured)));
        Require(JsonUtility.ToJson(persistence.Capture()) == configured, "Serialized filter round trip changed state.");
        var invalid = new ConveyorFilterCriteria { itemIds = new List<string> { "missing:wim-item" } };
        Require(!commands.SetAdvancedFilter(input, invalid).Succeeded
            && configured == JsonUtility.ToJson(persistence.Capture()), "Invalid command partially changed state.");
        var invalidSave = JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(configured);
        invalidSave.nodes.Single(node => node.buildingInstanceId == inputId).filter.materialIds.Add("missing:wim-material");
        bool rejected = false;
        try { persistence.PrepareRestore(invalidSave); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected && configured == JsonUtility.ToJson(persistence.Capture()), "Invalid restore changed live state.");
        report.Add("filter-serialized-restore-and-invalid-command-restore-atomicity=PASS");

        yield return VerifyPhysicalFilters(scope, input, output, outputDestination, report);

        var warehouses = scope.Container.Resolve<IWarehouseWorldQuery>();
        var physical = scope.Container.Resolve<IWorldItemStackRuntime>();
        long lumberMass = physical.MassQuery.GetQuantityMass((ItemDefinitionId)"material:lumber",
            PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)"material:lumber"), 1).Value;
        var reserve = query.GetReserveWarehouseChoices().FirstOrDefault(choice => warehouses.Warehouses.Any(owner =>
            owner.PersistentInstanceId.Value == choice.FacilityId && owner.Inventory.Accepts(catalog.GetDefinition("material:lumber").StockCategory)
            && owner.Inventory.RemainingMassGrams >= lumberMass));
        Require(!string.IsNullOrEmpty(reserve.DestinationId), "No existing compatible reserve warehouse for physical lumber fixture.");
        Require(commands.SetOverflowPolicy(overflow, ConveyorOverflowPolicy.ReserveWarehouseThenLoose, string.Empty).Succeeded,
            "Cannot select warehouse-wait policy.");
        Refresh();
        yield return null;
        string reservePrefix = "ConveyorChoices_" + overflowId + "_Reserve";
        Click("ConveyorChoices_" + overflowId + "_Lists_Reserve");
        yield return null;
        int reservePage = query.GetReserveWarehouseChoices().ToList().FindIndex(choice => choice.DestinationId == reserve.DestinationId) / 8;
        // The fixture picks a current main-UI option; normal page steppers remain the access path.
        for (int page = 0; page < reservePage; page++)
        {
            Click(reservePrefix + "Page_" + reservePrefix + "Paging_Increase");
            yield return null;
        }
        Click(reservePrefix + "_" + reserve.DestinationId);
        yield return null;
        Require(persistence.Capture().nodes.Single(node => node.buildingInstanceId == overflowId).reserveWarehouseId == reserve.DestinationId,
            "Main reserve choice not persisted.");
        configured = JsonUtility.ToJson(persistence.Capture());
        Click(reservePrefix + "Clear");
        persistence.Restore(persistence.PrepareRestore(JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(configured)));
        Require(configured == JsonUtility.ToJson(persistence.Capture()), "Reserve serialized restore mismatch.");
        Require(commands.SetOverflowPolicy(overflow, ConveyorOverflowPolicy.ReserveWarehouseThenLoose, string.Empty).Succeeded,
            "Cannot remove reserve for failure checkpoint.");
        report.Add("main-reserve-selection-and-serialized-round-trip=PASS");

        Require(physical.SpawnItemAt("material:lumber", 1, overflow.centerPos, WorldItemStackState.Loose,
            string.Empty, out int spawned) && spawned == 1, "Cannot spawn fault checkpoint lot.");
        var stack = physical.GetStacksAt(overflow.centerPos).Last(value => value.ItemId == "material:lumber"
            && value.State == WorldItemStackState.Loose);
        var stackId = new ItemStackId(stack.StackId);
        const string payloadId = "qa:wim008:stalled-overflow";
        Require(transfers.TryBeginTransit(stackId, overflow.centerPos, payloadId, out _, out _), "Cannot own physical fault checkpoint lot.");
        var fault = persistence.Capture();
        fault.payloads.Add(new ConveyorPayloadSaveData
        {
            payloadId = payloadId, itemStackId = stack.StackId, segmentBuildingInstanceId = overflowId,
            destinationId = "qa:wim008:unreachable", stallReason = ConveyorStallReason.NoRoute
        });
        persistence.Restore(persistence.PrepareRestore(fault));
        yield return Wait(() => query.Networks.SelectMany(network => network.Payloads).Any(payload =>
            payload.PayloadId == payloadId && payload.StallReason == ConveyorStallReason.OverflowBlocked),
            "Missing reserve did not become retained OverflowBlocked.");
        Require(transfers.TryGetTransitStack(stackId, payloadId, out var retained) && retained.Quantity == 1,
            "Missing reserve lost/spilled the owned physical lot.");
        float start = clock.Time;
        yield return Wait(() => clock.Time - start >= 2f, "Game clock stopped.");
        Require(transfers.TryGetTransitStack(stackId, payloadId, out retained) && retained.Quantity == 1,
            "Repeated reserve retries spilled the lot.");
        report.Add("fault-checkpoint-missing-reserve-retries-retained-exact-lot=PASS");

        var fullOwner = warehouses.Warehouses.Single(owner => owner.PersistentInstanceId.Value == reserve.FacilityId);
        var fullBuilding = (BuildableObject)fullOwner;
        int fillCount = checked((int)(fullOwner.Inventory.RemainingMassGrams / lumberMass));
        Require(physical.SpawnItemAt("material:lumber", fillCount, fullBuilding.centerPos, WorldItemStackState.Stored,
            reserve.DestinationId, out int filled) && filled == fillCount
            && fullOwner.Inventory.RemainingMassGrams < lumberMass, "Could not physically fill reserve via exact admission.");
        Require(commands.SetOverflowPolicy(overflow, ConveyorOverflowPolicy.ReserveWarehouseThenLoose, reserve.DestinationId).Succeeded,
            "Cannot set physically full reserve.");
        start = clock.Time;
        yield return Wait(() => clock.Time - start >= 2f, "Full-reserve game clock stopped.");
        Require(transfers.TryGetTransitStack(stackId, payloadId, out retained) && retained.Quantity == 1
            && fullOwner.Inventory.RemainingMassGrams < lumberMass, "Full reserve lost/spilled the owned lot.");
        report.Add("physically-full-reserve-exact-admission-rejection-retains-lot=PASS");
        var recoveryReserve = query.GetReserveWarehouseChoices().FirstOrDefault(choice => choice.DestinationId != reserve.DestinationId
            && warehouses.Warehouses.Any(owner => owner.PersistentInstanceId.Value == choice.FacilityId
                && owner.Inventory.Accepts(catalog.GetDefinition("material:lumber").StockCategory)
                && owner.Inventory.RemainingMassGrams >= lumberMass));
        Require(!string.IsNullOrEmpty(recoveryReserve.DestinationId), "No second compatible warehouse for full-reserve recovery.");
        Refresh();
        yield return null;
        Click(reservePrefix + "_" + recoveryReserve.DestinationId);
        yield return Wait(() => physical.GetAllStacks().Any(value => value.StackId == stack.StackId
            && value.State == WorldItemStackState.Stored && value.Quantity == 1), "Reserve recovery did not store same lot.");
        Require(!query.Networks.SelectMany(network => network.Payloads).Any(payload => payload.PayloadId == payloadId),
            "Successful reserve delivery retained a duplicate payload owner.");
        report.Add("main-reserve-recovery-runtime-exact-gram-same-lot-delivery=PASS");
        report.Add("fault-checkpoint=real-physical-transit-owner-at-existing-overflow-node");
    }

    private static IEnumerator VerifyPhysicalFilters(DungeonRuntimeLifetimeScope scope, BuildableObject input,
        BuildableObject output, string destination, List<string> report)
    {
        var commands = scope.Container.Resolve<IConveyorInfrastructureCommand>();
        var routing = scope.Container.Resolve<IConveyorRoutingService>();
        var query = scope.Container.Resolve<IConveyorInfrastructureQuery>();
        var physical = scope.Container.Resolve<IWorldItemStackRuntime>();
        var transfers = scope.Container.Resolve<IItemTransferService>();
        var catalog = scope.Container.Resolve<IDungeonItemCatalogProvider>();
        var equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        string material = equipment.GetAllowedMaterials("weapon:dagger").First().MaterialId;
        var instance = equipment.CreateExternalInstance("weapon:dagger", CombatEquipmentQuality.Normal, material);
        string itemId = PhysicalItemIds.ForEquipment(instance.definitionId);
        Require(physical.SpawnExistingUniqueItemAt(itemId, new ItemInstanceId(instance.instanceId), input.centerPos,
            WorldItemStackState.Loose, string.Empty, out string spawnedId), "Cannot publish real unique equipment lot.");
        Require(equipment.TryLinkToWorldStack(instance.instanceId, spawnedId, CombatEquipmentWorldState.Loose)
            && equipment.TryGetInstanceBySourceStack(spawnedId, out var linked) && linked.materialId == material,
            "Equipment fixture omitted the existing physical stack linkage contract.");
        var stackId = new ItemStackId(spawnedId);
        bool Route() => routing.TryFindRoute(input.RequirePersistentInstanceId(), destination, stackId, out _, out _);
        string otherItem = catalog.All.First(value => value.ItemId != itemId).ItemId;
        StockCategory ownCategory = catalog.GetDefinition(itemId).StockCategory;
        StockCategory otherCategory = catalog.All.First(value => value.StockCategory != ownCategory).StockCategory;
        var criteria = new ConveyorFilterCriteria { itemIds = new List<string> { otherItem } };
        Require(commands.SetAdvancedFilter(input, criteria).Succeeded, "Cannot set input mismatch.");
        Require(!scope.Container.Resolve<IConveyorPayloadTransaction>().TryLoadStack(stackId, input, destination,
            out _, out _) && physical.GetAllStacks().Any(value => value.StackId == spawnedId
                && value.State == WorldItemStackState.Loose && value.Quantity == 1),
            "Input filter mismatch mutated physical cargo.");
        Require(commands.SetAdvancedFilter(input, new ConveyorFilterCriteria()).Succeeded, "Cannot clear input filter.");
        // Routing starts after its current node. Initial entry is owned by TryLoadStack;
        // test path filters at the downstream output, not the already occupied source.
        Require(commands.SetAdvancedFilter(output, criteria).Succeeded && !Route(), "Wrong item filter admitted unique equipment.");
        criteria.stockCategories.Add(ownCategory);
        Require(commands.SetAdvancedFilter(output, criteria).Succeeded && Route(), "Item/category OR rule failed.");
        criteria.stockCategories.Clear(); criteria.stockCategories.Add(otherCategory);
        Require(commands.SetAdvancedFilter(output, criteria).Succeeded && !Route(), "Wrong item and category allowed route.");
        criteria.itemIds.Clear(); criteria.itemIds.Add(itemId); criteria.stockCategories.Clear();
        criteria.materialIds.Add(material);
        Require(commands.SetAdvancedFilter(output, criteria).Succeeded && Route(), "Actual matching equipment material rejected.");
        criteria.materialIds.Clear();
        criteria.materialIds.Add(query.GetMaterialFilterChoices().First(value => value.Id != material).Id);
        Require(commands.SetAdvancedFilter(output, criteria).Succeeded && !Route(), "Different equipment material admitted.");
        criteria.materialIds.Clear(); criteria.materialIds.Add(material);
        Require(commands.SetAdvancedFilter(output, criteria).Succeeded && Route(), "Matching material did not resume route.");
        Require(scope.Container.Resolve<IConveyorPayloadTransaction>().TryLoadStack(stackId, input, destination,
            out string payloadId, out _), "Matching physical equipment load failed.");
        var block = new ConveyorFilterCriteria { itemIds = new List<string> { otherItem } };
        Require(commands.SetAdvancedFilter(output, block).Succeeded, "Cannot block downstream filter during transit.");
        yield return Wait(() => query.Networks.SelectMany(network => network.Payloads).Any(payload => payload.PayloadId == payloadId
            && payload.StallReason == ConveyorStallReason.FilterMismatch), "Live cargo did not stop for changed downstream filter.");
        Require(transfers.TryGetTransitStack(stackId, payloadId, out var retained) && retained.Quantity == 1,
            "Changing filter lost unique transit cargo.");
        Require(commands.SetAdvancedFilter(output, new ConveyorFilterCriteria()).Succeeded, "Cannot reopen downstream filter.");
        yield return Wait(() => physical.GetAllStacks().Any(value => value.StackId == spawnedId
            && value.State == WorldItemStackState.FacilityBuffer && value.Quantity == 1
            && value.DestinationId == destination), "Same equipment lot did not arrive after filter reopened.");
        report.Add("physical-item-category-OR-material-AND-routes=PASS");
        report.Add("live-inflight-filter-change-retains-unique-cargo-and-delivers-same-lot=PASS");
    }

    private static void Refresh() => UnityEngine.Object.FindObjectsByType<P0FeatureSurfacePanel>(FindObjectsSortMode.None)
        .First(panel => panel.gameObject.activeInHierarchy).Refresh();

    private static void Click(string name)
    {
        var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .SingleOrDefault(value => value.name == name && value.gameObject.activeInHierarchy);
        Require(button != null && button.IsInteractable() && EventSystem.current != null, "Main UI action missing: " + name);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    private static IEnumerator Wait(Func<bool> predicate, string message)
    {
        float started = Time.realtimeSinceStartup;
        while (!predicate() && Time.realtimeSinceStartup - started < 30f) yield return null;
        Require(predicate(), message);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
