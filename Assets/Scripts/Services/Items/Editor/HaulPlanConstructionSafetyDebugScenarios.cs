#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class HaulPlanConstructionSafetyDebugScenarios
{
    private const string ReportPath = "Temp/haul-plan-construction-safety.tsv";
    private const string LumberItemId = "material:lumber";
    private static readonly IFacilityCandidateCache FacilityCandidateCache =
        new FacilityCandidateCacheStore(
            CharacterAiEditorTestDependencies.WorldRegistry,
            frameWorkBudget: null);
    private static readonly IRoomFacilityPolicy RoomFacilityPolicy =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);

    [MenuItem("DungeonStory/Debug/Items/Run Haul Plan And Construction Safety Contracts")]
    public static void RunFromMenu()
    {
        RunAll(logSuccess: true);
    }

    public static bool RunAll(bool logSuccess)
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("multi_stack_haul_plan", VerifyMultiStackHaulPlan, lines, errors);
        Run("equipment_haul_lease_survives_world_state_transition",
            VerifyEquipmentHaulLeaseSurvivesWorldStateTransition,
            lines,
            errors);
        Run("priority_haul_seed_beats_value", VerifyPriorityHaulSeedBeatsValue, lines, errors);
        Run("partial_heavy_stack_reservation", VerifyPartialHeavyStackReservation, lines, errors);
        Run("survival_stock_transit_reserve", VerifySurvivalStockTransitReserve, lines, errors);
        Run("raw_food_harvest_is_haulable", VerifyRawFoodHarvestIsHaulable, lines, errors);
        Run("construction_safety_forced_warning", VerifyConstructionSafetyForcedWarning, lines, errors);

        File.WriteAllLines(ReportPath, lines);
        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            Debug.LogError($"Haul plan / construction safety contracts FAIL. Report: {ReportPath}");
            return false;
        }

        if (logSuccess)
        {
            Debug.Log($"Haul plan / construction safety contracts PASS. Report: {ReportPath}");
        }

        return true;
    }

    [MenuItem("DungeonStory/Debug/Items/Run Equipment Haul Lease World-State Transition")]
    public static void RunEquipmentHaulLeaseWorldStateTransition()
    {
        const string focusedReportPath =
            "Temp/equipment-haul-lease-world-state-transition.tsv";
        Directory.CreateDirectory("Temp");
        string details = VerifyEquipmentHaulLeaseSurvivesWorldStateTransition();
        File.WriteAllLines(focusedReportPath, new[]
        {
            "case\tresult\tdetails",
            $"equipment_haul_lease_survives_world_state_transition\tPASS\t{details}"
        });
        Debug.Log(
            "Equipment haul lease world-state transition PASS. Report: "
            + focusedReportPath);
    }

    private static string VerifyMultiStackHaulPlan()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        try
        {
            string stockId = LumberItemId;
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    5,
                    new Vector2Int(2, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int first)
                && first == 5,
                "first stack spawn failed");
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    5,
                    new Vector2Int(3, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int second)
                && second == 5,
                "second stack spawn failed");
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    5,
                    new Vector2Int(4, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int third)
                && third == 5,
                "third stack spawn failed");

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string failureReason),
                "haul plan failed: " + failureReason);

            int reserved = plan.ReservedStackQuantities.Sum(item => item.Quantity);
            Require(plan.PickupLegs.Count >= 2, $"expected multiple pickup legs, got {plan.PickupLegs.Count}");
            Require(plan.PrimaryDestination == WorldItemHaulDestinationKind.Warehouse,
                $"unexpected destination {plan.PrimaryDestination}");
            Require(reserved >= 10, $"expected at least two stacks reserved, got {reserved}");

            int picked = 0;
            foreach (WorldItemReservedStackQuantity reservation in plan.ReservedStackQuantities)
            {
                Require(scenario.Items.TryPickupReservedStackQuantity(
                        scenario.Actor,
                        scenario.Carry,
                        reservation,
                        out int pickedUp,
                        out string pickupReason),
                    "pickup failed: " + pickupReason);
                picked += pickedUp;
            }

            Require(picked == reserved, $"picked {picked}, reserved {reserved}");
            Require(scenario.Items.TryDepositCarriedItems(
                    scenario.Actor,
                    scenario.Carry,
                    scenario.Warehouse,
                    out string depositReason),
                "deposit failed: " + depositReason);
            Require(!scenario.Carry.HasItems, "carry inventory still has items");
            Require(scenario.Warehouse.Inventory.GetStock(StockCategory.General) >= picked,
                "warehouse did not receive hauled stock");

            return $"pickups={plan.PickupLegs.Count}; reserved={reserved}; deposited={picked}";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifyEquipmentHaulLeaseSurvivesWorldStateTransition()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        try
        {
            CombatEquipmentInstance created = scenario.Equipment.CreateInstance(
                "weapon:dagger",
                CombatEquipmentQuality.Normal,
                CombatEquipmentWorldState.Loose);
            string itemId = PhysicalItemIds.ForEquipment(created.definitionId);
            Require(scenario.Items.SpawnExistingUniqueItemAt(
                    itemId,
                    (ItemInstanceId)created.instanceId,
                    new Vector2Int(2, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out string stackId)
                && scenario.Equipment.TryLinkToWorldStack(
                    created.instanceId,
                    stackId,
                    CombatEquipmentWorldState.Loose),
                "failed to materialize the reserved unique equipment");
            Require(scenario.Equipment.TryGetInstance(
                    created.instanceId,
                    out CombatEquipmentInstance linkedBaseline),
                "linked equipment authority was missing");

            WorldItemStackSnapshot looseStack = scenario.Items.GetAllStacks()
                .Single(stack => string.Equals(
                    stack.StackId,
                    stackId,
                    StringComparison.Ordinal));
            string looseSignature = ItemReservationSignature.Create(
                looseStack.ItemId,
                looseStack.Components);
            Require(scenario.Items.PrioritizeHaul(stackId),
                "failed to prioritize the exact unique equipment stack");
            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string planFailure),
                "production haul planning failed: " + planFailure);
            WorldItemReservedStackQuantity reservation =
                plan.ReservedStackQuantities.Single(candidate =>
                    string.Equals(
                        candidate.StackId,
                        stackId,
                        StringComparison.Ordinal));
            string operationId = reservation.OwnerOperationId;
            Require(!string.IsNullOrWhiteSpace(operationId)
                    && reservation.DestinationKind
                        == WorldItemHaulDestinationKind.Warehouse
                    && string.Equals(
                        reservation.DestinationId,
                        WarehouseStorageIdentity.RequireDestinationId(
                            scenario.Warehouse),
                        StringComparison.Ordinal),
                $"production planner changed the exact equipment destination or operation: "
                + $"operation='{operationId}'; kind={reservation.DestinationKind}; "
                + $"actual='{reservation.DestinationId}'; expected='"
                + WarehouseStorageIdentity.RequireDestinationId(scenario.Warehouse)
                + "'");
            Require(scenario.QuantityReservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> plannedLeases)
                && plannedLeases.Count == 1
                && plannedLeases[0].slices.Count == 1
                && string.Equals(
                    plannedLeases[0].leaseId,
                    reservation.LeaseId,
                    StringComparison.Ordinal)
                && string.Equals(
                    plannedLeases[0].slices[0].expectedStackSignature,
                    looseSignature,
                    StringComparison.Ordinal),
                "production planner did not bind the exact equipment lease");

            Require(scenario.Items.TryPickupReservedStackQuantity(
                    scenario.Actor,
                    scenario.Carry,
                    reservation,
                    out int pickedUp,
                    out string pickupFailure)
                && pickedUp == 1,
                "unique equipment pickup failed: " + pickupFailure);
            Require(scenario.QuantityReservations.Revalidate(
                    reservation.LeaseId,
                    out ItemQuantityLease carriedLease,
                    out DomainFailure carriedFailure),
                "world-state transition invalidated the exact lease: " + carriedFailure);

            CharacterCarriedItemSaveData carried = scenario.Carry.Items.Single(item =>
                string.Equals(
                    item.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal));
            WorldItemStackSnapshot physicalCarried = scenario.Items.GetAllStacks()
                .Single(stack => string.Equals(
                    stack.StackId,
                    stackId,
                    StringComparison.Ordinal));
            Require(carriedLease.slices.Count == 1
                    && string.Equals(
                        carriedLease.ownerOperationId,
                        operationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        carriedLease.slices[0].stackId,
                        stackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        ItemReservationSignature.Create(
                            carried.itemId,
                            carried.components),
                        looseSignature,
                        StringComparison.Ordinal)
                    && string.Equals(
                        ItemReservationSignature.Create(
                            physicalCarried.ItemId,
                            physicalCarried.Components),
                        looseSignature,
                        StringComparison.Ordinal),
                "plan, lease, carry and physical equipment signatures diverged");
            Require(scenario.Equipment.TryGetInstance(
                    created.instanceId,
                    out CombatEquipmentInstance carriedEquipment)
                && carriedEquipment.worldState == CombatEquipmentWorldState.Carried,
                "equipment authority did not enter Carried state");
            Require(scenario.Items.TryCommitHaulPickup(
                    operationId,
                    scenario.Carry,
                    out string commitFailure),
                "equipment pickup intent did not commit: " + commitFailure);
            Require(scenario.Items.TryCaptureHaulDeliveryIntent(
                    operationId,
                    out HaulDeliveryIntentSaveData committedIntent)
                && committedIntent.commitments.Count == 1
                && string.Equals(
                    committedIntent.commitments[0].expectedStackSignature,
                    looseSignature,
                    StringComparison.Ordinal),
                "committed equipment intent lost its exact reservation identity");

            CombatEquipmentInstance alternateWorldState = linkedBaseline.Clone();
            alternateWorldState.worldState = CombatEquipmentWorldState.MaintenanceBuffer;
            Require(string.Equals(
                    ItemReservationSignature.Create(
                        itemId,
                        new[] { EquipmentItemStateCodec.Encode(alternateWorldState) }),
                    looseSignature,
                    StringComparison.Ordinal),
                "equipment worldState remained part of reservation identity");
            CombatEquipmentInstance damaged = linkedBaseline.Clone();
            damaged.durabilityRatio = Mathf.Max(0f, damaged.durabilityRatio - 0.25f);
            Require(!string.Equals(
                    ItemReservationSignature.Create(
                        itemId,
                        new[] { EquipmentItemStateCodec.Encode(damaged) }),
                    looseSignature,
                    StringComparison.Ordinal),
                "durability mutation was incorrectly ignored by reservation identity");
            EquipmentModuleInstance attachedModule = new()
            {
                instanceId = "item-instance:qa-haul-signature-module",
                definitionId = "module:weapon:balanced-core",
                state = EquipmentModuleProcessState.Installed,
                attachedEquipmentInstanceId = linkedBaseline.instanceId
            };
            Require(!string.Equals(
                    ItemReservationSignature.Create(
                        itemId,
                        new[]
                        {
                            EquipmentItemStateCodec.Encode(
                                linkedBaseline,
                                new[] { attachedModule })
                        }),
                    looseSignature,
                    StringComparison.Ordinal),
                "attached-module mutation was incorrectly ignored by reservation identity");

            Require(scenario.Items.TryDepositCarriedItems(
                    scenario.Actor,
                    scenario.Carry,
                    scenario.Warehouse,
                    new[] { operationId },
                    out string depositFailure),
                "unique equipment warehouse deposit failed: " + depositFailure);
            Require(scenario.Items.ReleaseHaulDeliveryIntent(operationId),
                "completed equipment delivery did not release its intent");
            Require(!scenario.Carry.HasItems
                    && !scenario.Items.TryCaptureHaulDeliveryIntent(
                        operationId,
                        out _)
                    && (!scenario.QuantityReservations.TryGetLeasesByOwner(
                            operationId,
                            out IReadOnlyList<ItemQuantityLease> remainingLeases)
                        || remainingLeases.Count == 0),
                "equipment haul left carry, intent or lease ownership behind");
            WorldItemStackSnapshot stored = scenario.Items.GetAllStacks().Single(stack =>
                string.Equals(
                    stack.ItemInstanceId,
                    created.instanceId,
                    StringComparison.Ordinal));
            Require(stored.State == WorldItemStackState.Stored
                    && scenario.Equipment.TryGetInstance(
                        created.instanceId,
                        out CombatEquipmentInstance storedEquipment)
                    && storedEquipment.worldState == CombatEquipmentWorldState.Stored,
                "equipment did not converge to stored physical authority");
            return $"operation={operationId}; lease={reservation.LeaseId}; "
                + $"stack={stackId}; state=Loose->Carried->Stored; cleanup=exact";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifyPartialHeavyStackReservation()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 10f);
        try
        {
            string stockId = LumberItemId;
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    10,
                    new Vector2Int(2, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 10,
                "heavy stack spawn failed");

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string failureReason),
                "heavy haul plan failed: " + failureReason);

            int reserved = plan.ReservedStackQuantities.Sum(item => item.Quantity);
            Require(reserved > 0 && reserved < 10, $"expected partial reservation, got {reserved}");
            Require(scenario.Items.TryPickupReservedStackQuantity(
                    scenario.Actor,
                    scenario.Carry,
                    plan.ReservedStackQuantities[0],
                    out int picked,
                    out string pickupReason),
                "partial pickup failed: " + pickupReason);
            Require(picked == reserved, $"picked {picked}, reserved {reserved}");

            int remaining = 10 - picked;
            Require(scenario.Items.GetAllStacks().Any(stack =>
                    stack.Quantity == remaining
                    && stack.ReservedQuantity == 0
                    && stack.AvailableQuantity == remaining),
                "remaining stack was not released for another hauler");

            return $"reserved={reserved}; remaining={remaining}; load={scenario.Carry.GetCurrentWeight(scenario.Items.CatalogProvider):0.##}";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifyPriorityHaulSeedBeatsValue()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        try
        {
            string stockId = LumberItemId;
            Vector2Int regularPosition = new Vector2Int(2, 1);
            Vector2Int priorityPosition = new Vector2Int(4, 1);
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    20,
                    regularPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _),
                "regular stack spawn failed");
            Require(scenario.Items.SpawnItemAt(
                    stockId,
                    1,
                    priorityPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _),
                "priority stack spawn failed");

            WorldItemStackSnapshot priority = scenario.Items
                .GetStacksAt(priorityPosition)
                .Single();
            Require(scenario.Items.PrioritizeHaul(priority.StackId),
                "priority haul flag failed");
            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string failureReason),
                "priority haul plan failed: " + failureReason);
            Require(plan.PickupLegs[0].Reservation.StackId == priority.StackId,
                $"priority stack was not the seed: {plan.PickupLegs[0].Reservation.StackId}");

            foreach (WorldItemReservedStackQuantity reservation in plan.ReservedStackQuantities)
            {
                Require(scenario.QuantityReservations.Release(
                        reservation.LeaseId,
                        ItemReservationReleaseReason.Cancelled),
                    $"failed to cancel exact lease {reservation.LeaseId}");
            }
            foreach (string ownerOperationId in plan.ReservedStackQuantities
                         .Select(value => value.OwnerOperationId)
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.Ordinal))
                scenario.Items.ReleaseHaulDeliveryIntent(ownerOperationId);

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan retriedPlan,
                    out failureReason),
                "priority haul retry failed: " + failureReason);
            Require(retriedPlan.PickupLegs[0].Reservation.StackId == priority.StackId,
                "priority was lost after a cancelled reservation");

            return $"prioritySeed={priority.StackId}; pickups={plan.PickupLegs.Count}; retryPreserved=True";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifySurvivalStockTransitReserve()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        try
        {
            const string foodId = "food:preserved-ration";
            Require(scenario.Items.SpawnItemAt(
                    foodId,
                    10,
                    new Vector2Int(2, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 10,
                "survival stock spawn failed");

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan firstPlan,
                    out string failureReason),
                "initial survival haul plan failed: " + failureReason);
            int firstReserved = firstPlan.ReservedStackQuantities.Sum(value => value.Quantity);
            Require(firstReserved == 9,
                $"one active consumer serving must remain loose, reserved={firstReserved}");

            foreach (WorldItemReservedStackQuantity reservation in firstPlan.ReservedStackQuantities)
            {
                Require(scenario.Items.TryPickupReservedStackQuantity(
                        scenario.Actor,
                        scenario.Carry,
                        reservation,
                        out _,
                        out string pickupReason),
                    "survival stock pickup failed: " + pickupReason);
            }
            Require(scenario.Items.TryDepositCarriedItems(
                    scenario.Actor,
                    scenario.Carry,
                    scenario.Warehouse,
                    out string depositReason),
                "survival stock deposit failed: " + depositReason);
            Require(scenario.Warehouse.Inventory.GetStock(StockCategory.Food) == 9,
                "warehouse did not receive protected survival stock delivery");

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan secondPlan,
                    out failureReason),
                "remaining survival stock did not unlock after delivery: " + failureReason);
            int secondReserved = secondPlan.ReservedStackQuantities.Sum(value => value.Quantity);
            Require(secondReserved == 1,
                $"remaining serving should be haulable after warehouse reserve exists, reserved={secondReserved}");

            return $"firstReserved={firstReserved}; stored=9; secondReserved={secondReserved}";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifyRawFoodHarvestIsHaulable()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        try
        {
            const string rawFoodId = "resource:twilight-grain";
            Require(scenario.Items.SpawnItemAt(
                    rawFoodId,
                    3,
                    new Vector2Int(2, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 3,
                "raw food harvest spawn failed");
            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string failureReason),
                "raw food harvest was incorrectly transit-protected: " + failureReason);
            int reserved = plan.ReservedStackQuantities.Sum(value => value.Quantity);
            Require(reserved == 3,
                $"all raw harvest must remain haulable, reserved={reserved}");
            return $"rawFood={rawFoodId};reserved={reserved};consumerReserve=0";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifyConstructionSafetyForcedWarning()
    {
        Grid grid = CreateWalkableExteriorGrid();
        GameObject actorObject = null;
        GameObject siteObject = null;
        BuildingSO wallData = null;
        try
        {
            GridProvider gridProvider = new GridProvider(grid);
            actorObject = CreateActor("ConstructionSafetyActor", gridProvider, grid, new Vector2Int(0, 1));
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();

            wallData = CreateBuildingData(99002, "테스트 벽", BuildingCategory.Wall, GridLayer.Building);
            siteObject = new GameObject("ConstructionSafetySite");
            ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
            site.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            site.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(new NoopBlueprintResearchWorkService()),
                FacilityCandidateCache,
                RoomFacilityPolicy,
                combatEquipmentRuntime: null,
                worldRegistry: null,
                worldItemStackRuntime: null,
                abilityRuntimeDispatcher: null,
                gameClock: null,
                paidFacilityContracts: null,
                evolutionState: new FacilityEvolutionStateComponentFactory());
            site.SetGrid(grid);
            site.Initialization(wallData, new Vector2Int(5, 1));
            grid.SetAreaType(new Vector2Int(5, 1), GridCellAreaType.ExteriorPath);

            ConstructionSafetyResult safety = ConstructionSafetyPlanner.Evaluate(
                site,
                actor.BuildingVisitor,
                forced: false);
            Require(!safety.IsSafe && safety.Reason == ConstructionSafetyReason.EntranceBlocked,
                $"expected exterior path block, got {safety.Reason}");

            ConstructionSafetyResult forced = ConstructionSafetyPlanner.Evaluate(
                site,
                actor.BuildingVisitor,
                forced: true);
            Require(forced.IsSafe && forced.IsForcedWarning && forced.Reason == ConstructionSafetyReason.Forced,
                "forced warning did not bypass with warning");

            return $"auto={safety.Message}; forced={forced.Message}";
        }
        finally
        {
            DestroyImmediateSafe(siteObject);
            DestroyImmediateSafe(actorObject);
            DestroyImmediateSafe(wallData);
        }
    }

    private sealed class ScenarioRuntime : IDisposable
    {
        private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

        private ScenarioRuntime(
            Grid grid,
            GridProvider gridProvider,
            WorldItemStackRuntime items,
            CombatEquipmentRuntime equipment,
            ItemQuantityReservationService quantityReservations,
            TestWarehouseBuilding warehouse,
            CharacterActor actor,
            CharacterCarryInventory carry,
            ICharacterAiWorldRegistry worldRegistry)
        {
            Grid = grid;
            GridProvider = gridProvider;
            Items = items;
            Equipment = equipment;
            QuantityReservations = quantityReservations;
            Warehouse = warehouse;
            Actor = actor;
            Carry = carry;
            WorldRegistry = worldRegistry;
        }

        public Grid Grid { get; }
        public GridProvider GridProvider { get; }
        public WorldItemStackRuntime Items { get; }
        public CombatEquipmentRuntime Equipment { get; }
        public ItemQuantityReservationService QuantityReservations { get; }
        public TestWarehouseBuilding Warehouse { get; }
        public CharacterActor Actor { get; }
        public CharacterCarryInventory Carry { get; }
        public ICharacterAiWorldRegistry WorldRegistry { get; }

        public static ScenarioRuntime Create(float lightStockWeight)
        {
            Grid grid = CreateWalkableExteriorGrid();
            GridProvider gridProvider = new GridProvider(grid);
            ScenarioRuntime scenario = null;
            try
            {
                GameObject warehouseObject = new GameObject("HaulPlanWarehouse");
                TestWarehouseBuilding warehouse = warehouseObject.AddComponent<TestWarehouseBuilding>();
                BuildingSO warehouseData = CreateBuildingData(99001, "테스트 창고", BuildingCategory.Shop, GridLayer.Building);
                warehouse.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
                warehouse.ConstructBuildableObject(
                    new BuildingResearchWorkPortAdapter(new NoopBlueprintResearchWorkService()),
                    FacilityCandidateCache,
                    RoomFacilityPolicy,
                    combatEquipmentRuntime: null,
                    worldRegistry: null,
                    worldItemStackRuntime: null,
                    abilityRuntimeDispatcher: null,
                    gameClock: null,
                    paidFacilityContracts: null,
                    evolutionState: new FacilityEvolutionStateComponentFactory());
                warehouse.SetGrid(grid);
                warehouse.Initialization(warehouseData, new Vector2Int(9, 1));
                grid.RegisterOccupant(warehouse, GridLayer.Building, warehouse.buildPoses, false);

                GameObject actorObject = CreateActor("HaulPlanActor", gridProvider, grid, new Vector2Int(0, 1));
                CharacterActor actor = actorObject.GetComponent<CharacterActor>();
                CharacterCarryInventory carry = actorObject.GetComponent<CharacterCarryInventory>();

                IGameContentCatalog gameContent = new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader());
                ICombatEquipmentCatalog combatCatalog =
                    new ResourceCombatEquipmentCatalog(gameContent);
                IDungeonItemCatalogProvider itemCatalog =
                    new TestCatalogProvider(lightStockWeight);
                IItemHaulingSettingsProvider haulingSettings =
                    new TestHaulingSettings(1.5f);
                ICharacterIdRegistry idRegistry = new TestIdRegistry();
                IGridPathSearchBroker pathBroker =
                    new GridPathSearchBroker(new UnityGameClock(), doorAccessQuery: null, performanceRecorder: null, costPolicy: null);
                ICharacterAiWorldRegistry worldRegistry =
                    CharacterAiEditorTestDependencies.WorldRegistry;
                worldRegistry.RegisterBuilding(warehouse);
                worldRegistry.RegisterCharacter(actor);
                WorldItemRepository repository = new WorldItemRepository(
                    new GuidPersistentIdGenerator(),
                    new DungeonRuntimeAggregateRootStore());
                FacilityBufferDestinationClaimRegistry destinationClaims = new();
                warehouse.BindPhysicalStock(
                    new PhysicalStockQuery(
                        repository,
                        itemCatalog,
                        new PhysicalItemMassQuery(itemCatalog)));
                ItemQuantityReservationService quantityReservations =
                    new ItemQuantityReservationService(
                        repository,
                        EditorNullItemMarkerPresenter.Instance,
                        new UnityGameClock());
                IItemReservationService reservations = new ItemReservationService(
                    repository,
                    EditorNullItemMarkerPresenter.Instance,
                    quantityReservations);
                IBufferStackAggregationService bufferAggregation =
                    new BufferStackAggregationService(
                        itemCatalog,
                        repository,
                        EditorNullItemMarkerPresenter.Instance,
                        quantityReservations,
                        quantityReservations);
                IWorldItemSpawner spawner = new WorldItemSpawner(
                    itemCatalog,
                    repository,
                    EditorNullItemMarkerPresenter.Instance);
                IPhysicalItemMassQuery massQuery =
                    new PhysicalItemMassQuery(itemCatalog);
                WorldItemQueryService query = new WorldItemQueryService(
                    itemCatalog,
                    massQuery,
                    repository,
                    EditorNullItemMarkerPresenter.Instance);
                EditorEquipmentPhysicalItemGatewayProxy equipmentItemGateway =
                    new EditorEquipmentPhysicalItemGatewayProxy();
                CombatEquipmentRuntime equipment =
                    CombatEquipmentEditorTestFactory.Create(
                        combatCatalog,
                        repository,
                        new CharacterCarryInventoryRegistry(),
                        materialCatalog: new ResourceEconomyContentCatalog(gameContent),
                        evolutionModules: EmptyEvolutionModuleRegistry.Instance,
                        researchProvider: EditorAllResearchRuntimeProvider.Instance,
                        moduleCatalog: new ResourceEquipmentModuleCatalog(gameContent),
                        itemStackRuntime: equipmentItemGateway);
                IWorldItemHaulPlanningService haulPlanning =
                    new WorldItemHaulPlanningService(
                        gridProvider,
                        itemCatalog,
                        massQuery,
                        haulingSettings,
                        idRegistry,
                        pathBroker,
                        worldRegistry,
                        repository,
                        quantityReservations,
                        destinationClaims);
                WorldItemReadServices readServices = new WorldItemReadServices(
                    itemCatalog,
                    massQuery,
                    haulingSettings,
                    query,
                    EditorNullItemMarkerPresenter.Instance,
                    new EditorCharacterAiPerformanceRecorder(),
                    DisabledDungeonDebugRuleQuery.Instance);
                IItemTransferService itemTransferService = new ItemTransferService(
                    readServices,
                    idRegistry,
                    gridProvider,
                    worldRegistry,
                    destinationClaims,
                    combatCatalog,
                    new GameEventBus(),
                    repository,
                    spawner,
                    warehouseService: new WorldItemWarehouseService(
                        itemCatalog,
                        repository,
                        worldRegistry,
                        spawner,
                        EditorNullItemMarkerPresenter.Instance,
                        gridProvider,
                        idRegistry,
                        reservations,
                        quantityReservations),
                    quantityReservations: quantityReservations,
                    quantityLeaseMutations: quantityReservations,
                    bufferAggregation: bufferAggregation);
                WorldItemStackRuntime items = WorldItemEditorTestFactory.Create(
                    gridProvider,
                    itemCatalog,
                    haulingSettings,
                    idRegistry,
                    new NoDropZoneQuery(),
                    new NoSpawnerProvider(),
                    pathBroker,
                    worldRegistry,
                    new UnityGameClock(),
                    repository,
                    reservations,
                    spawner,
                    query,
                    haulPlanning,
                    itemMarkerPresenter: EditorNullItemMarkerPresenter.Instance,
                    itemTransferService: itemTransferService,
                    performanceRecorder: new EditorCharacterAiPerformanceRecorder());
                equipmentItemGateway.Attach(items);
                items.Start();

                scenario = new ScenarioRuntime(
                    grid,
                    gridProvider,
                    items,
                    equipment,
                    quantityReservations,
                    warehouse,
                    actor,
                    carry,
                    worldRegistry);
                scenario.ownedObjects.Add(actorObject);
                scenario.ownedObjects.Add(warehouseObject);
                scenario.ownedObjects.Add(warehouseData);
                return scenario;
            }
            catch
            {
                scenario?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Items?.Dispose();
            WorldRegistry?.UnregisterCharacter(Actor);
            WorldRegistry?.UnregisterBuilding(Warehouse);
            foreach (UnityEngine.Object owned in ownedObjects)
            {
                DestroyImmediateSafe(owned);
            }
        }
    }

    private static Grid CreateWalkableExteriorGrid()
    {
        Grid grid = new Grid(12, 3);
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                grid.SetAreaType(new Vector2Int(x, y), GridCellAreaType.ExteriorPath);
            }
        }

        return grid;
    }

    private static GameObject CreateActor(
        string name,
        IGridSystemProvider gridProvider,
        Grid grid,
        Vector2Int position)
    {
        GameObject actorObject = new GameObject(name);
        actorObject.SetActive(false);
        CharacterActor actor = actorObject.AddComponent<CharacterActor>();
        CharacterLifecycle lifecycle = actorObject.GetComponent<CharacterLifecycle>();
        actorObject.AddComponent<CharacterCarryInventory>();
        actorObject.AddComponent<AbilityHaul>();
        actorObject.GetComponent<CharacterIdentity>().SetPersistentId($"character:worker:{name}");
        CharacterAiEditorTestDependencies.Inject(actorObject);
        lifecycle.ConstructCharacterLifecycle(gridProvider);
        actorObject.transform.position = grid.GetWorldPos(position);
        actorObject.SetActive(true);
        actor.EnsureRuntimeState();
        return actorObject;
    }

    private static BuildingSO CreateBuildingData(
        int id,
        string objectName,
        BuildingCategory category,
        GridLayer layer)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = id;
        building.objectName = objectName;
        building.width = 1;
        building.height = 1;
        building.layer = layer;
        building.category = category;
        building.unlocked = true;
        return building;
    }

    private static void Run(
        string name,
        Func<string> test,
        List<string> lines,
        List<string> errors)
    {
        try
        {
            string details = test();
            lines.Add($"{name}\tPASS\t{details}");
        }
        catch (Exception ex)
        {
            lines.Add($"{name}\tFAIL\t{ex.Message}");
            errors.Add($"{name}: {ex.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void DestroyImmediateSafe(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        UnityEngine.Object.DestroyImmediate(target);
    }

    private sealed class TestWarehouseBuilding : BuildableObject, IWarehouseFacility
    {
        private readonly WarehouseInventory inventory = new WarehouseInventory(200);

        public WarehouseInventory Inventory => inventory;
        public bool HasWarehouseInventory => true;

        public void BindPhysicalStock(IStockQuery stockQuery)
        {
            inventory.BindPhysicalStock(
                stockQuery,
                RequirePersistentInstanceId(),
                CharacterAiEditorTestDependencies.AuthoredGameplay);
        }
    }

    private sealed class NoopBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility)
        {
            return false;
        }

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float workSeconds)
        {
            return default;
        }

        public BlueprintResearchWorkResult ApplyApprovedResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float approvedWorkUnits) =>
            ApplyResearchWork(researcher, researchFacility, approvedWorkUnits);
    }

    private sealed class GridProvider : IGridSystemProvider
    {
        private readonly Grid grid;

        public GridProvider(Grid grid)
        {
            this.grid = grid;
        }

        public GridSystemManager Manager => null;
        public Grid Grid => grid;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid result)
        {
            result = grid;
            return result != null;
        }
    }

    private sealed class TestCatalogProvider : IDungeonItemCatalogProvider
    {
        private readonly float stockWeight;
        private readonly ResourceDungeonItemCatalogProvider authoredCatalog =
            EditorItemCatalogFactory.Create();
        private readonly IReadOnlyList<DungeonItemDefinition> all;

        public TestCatalogProvider(float stockWeight)
        {
            this.stockWeight = Mathf.Max(0.01f, stockWeight);
            all = new[]
                {
                    LumberItemId,
                    "food:preserved-ration",
                    "resource:twilight-grain",
                    PhysicalItemIds.ForEquipment("weapon:dagger"),
                    PhysicalItemIds.ForEquipmentModule()
                }
                .Select(GetDefinition)
                .OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<DungeonItemDefinition> All => all;

        public DungeonItemDefinition GetDefinition(string itemId)
        {
            if ((PhysicalItemIds.TryGetEquipmentDefinitionId(itemId, out _)
                    || PhysicalItemIds.IsEquipmentModule(itemId))
                && authoredCatalog.TryGetDefinition(
                    itemId,
                    out DungeonItemDefinition authored))
            {
                return authored;
            }
            StockCategory category = itemId switch
            {
                "food:preserved-ration" => StockCategory.Food,
                "resource:clean-water" => StockCategory.Water,
                _ => StockCategory.General
            };
            return new DungeonItemDefinition(
                itemId,
                itemId,
                string.Empty,
                category,
                1,
                null,
                stockWeight,
                75,
                resourceKind: itemId == "food:preserved-ration"
                    ? ResourceItemKind.Food
                    : ResourceItemKind.Raw);
        }

        public bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
        {
            definition = GetDefinition(itemId);
            return true;
        }
    }

    private sealed class TestHaulingSettings : IItemHaulingSettingsProvider
    {
        public TestHaulingSettings(float maxCarryMultiplier)
        {
            MaxCarryMultiplier = Mathf.Clamp(maxCarryMultiplier, 1f, 2.5f);
        }

        public float MaxCarryMultiplier { get; private set; }

        public ItemHaulingSettingsSnapshot Capture()
        {
            return new ItemHaulingSettingsSnapshot { maxCarryMultiplier = MaxCarryMultiplier };
        }

        public void Restore(ItemHaulingSettingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            MaxCarryMultiplier = Mathf.Clamp(snapshot.maxCarryMultiplier, 1f, 2.5f);
        }
    }

    private sealed class TestIdRegistry : ICharacterIdRegistry
    {
        public bool TryGetPersistentId(CharacterActor actor, out string persistentId)
        {
            persistentId = actor != null && actor.Identity != null
                ? actor.Identity.PersistentId
                : string.Empty;
            return !string.IsNullOrWhiteSpace(persistentId);
        }

        public string GetOrAssignPersistentId(CharacterActor actor)
        {
            if (TryGetPersistentId(actor, out string persistentId))
            {
                return persistentId;
            }

            return actor != null ? $"worker:{actor.GetInstanceID()}" : "worker:null";
        }
    }

    private sealed class NoSpawnerProvider : ICharacterSpawnerProvider
    {
        public bool TryGetSpawner(out CharacterSpawner spawner)
        {
            spawner = null;
            return false;
        }
    }

    private sealed class NoDropZoneQuery : IWorldDropZoneQuery
    {
        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = default;
            return false;
        }

        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = default;
            return false;
        }

        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }
}
#endif
