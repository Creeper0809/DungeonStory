#if UNITY_EDITOR
using System;
using System.Collections;
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

    public static void RunBatchModeAndExit()
    {
        int exitCode = 1;
        try
        {
            string output = Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                if (Directory.Exists(output) || File.Exists(output))
                {
                    throw new InvalidOperationException(
                        "A new create-only haul regression output directory is required.");
                }
                Directory.CreateDirectory(output);
            }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/GameplayScene.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            bool passed = RunAll(logSuccess: true);
            if (!string.IsNullOrWhiteSpace(output))
            {
                File.Copy(
                    ReportPath,
                    Path.Combine(output, "haul-construction-destination.tsv"),
                    overwrite: false);
            }
            exitCode = passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        EditorApplication.Exit(exitCode);
    }

    public static bool RunAll(bool logSuccess)
    {
        using EditorVerificationSceneFixtureScope fixtureScene = new(
            "qa:haul-plan-construction-safety");
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("sequential_exact_warehouse_lots",
            VerifySequentialExactWarehouseLots,
            lines,
            errors);
        Run("equipment_haul_lease_survives_world_state_transition",
            VerifyEquipmentHaulLeaseSurvivesWorldStateTransition,
            lines,
            errors);
        Run("priority_haul_seed_beats_value", VerifyPriorityHaulSeedBeatsValue, lines, errors);
        Run("availability_requires_reachable_warehouse",
            VerifyAvailabilityRequiresReachableWarehouse,
            lines,
            errors);
        Run("seed_lot_reachable_fallback_delivery",
            VerifySeedLotReachableFallbackDelivery,
            lines,
            errors);
        Run("committed_delivery_beats_generic_priority",
            VerifyCommittedDeliveryBeatsGenericPriority,
            lines,
            errors);
        Run("partial_heavy_stack_reservation", VerifyPartialHeavyStackReservation, lines, errors);
        Run("survival_stock_transit_reserve", VerifySurvivalStockTransitReserve, lines, errors);
        Run("raw_food_harvest_is_haulable", VerifyRawFoodHarvestIsHaulable, lines, errors);
        Run("construction_destination_work_order_identity",
            VerifyConstructionDestinationWorkOrderIdentity,
            lines,
            errors);
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

    public static bool RunHaulDeliveryPathFailureSnapshotFocused()
    {
        (string Name, HaulDeliveryPathFailureSnapshot Snapshot, bool Expected)[] cases =
        {
            ("default", default, false),
            ("empty", new HaulDeliveryPathFailureSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                AIActionFailureKind.NoPath), false),
            ("missing_source", new HaulDeliveryPathFailureSnapshot(
                "operation",
                string.Empty,
                "destination",
                "item",
                AIActionFailureKind.NoPath), false),
            ("missing_destination", new HaulDeliveryPathFailureSnapshot(
                "operation",
                "stack",
                string.Empty,
                "item",
                AIActionFailureKind.NoPath), false),
            ("missing_item", new HaulDeliveryPathFailureSnapshot(
                "operation",
                "stack",
                "destination",
                string.Empty,
                AIActionFailureKind.NoPath), false),
            ("complete_no_path", new HaulDeliveryPathFailureSnapshot(
                "operation",
                "stack",
                "destination",
                "item",
                AIActionFailureKind.NoPath), true),
            ("complete_other_failure", new HaulDeliveryPathFailureSnapshot(
                "operation",
                "stack",
                "destination",
                "item",
                AIActionFailureKind.NoGrid), false)
        };

        foreach (var testCase in cases)
        {
            if (testCase.Snapshot.IsValid == testCase.Expected)
                continue;

            Debug.LogError(
                "Haul delivery path-failure snapshot contract failed: "
                + testCase.Name
                + "; expected=" + testCase.Expected
                + "; actual=" + testCase.Snapshot.IsValid + ".");
            return false;
        }

        Debug.Log("Haul delivery path-failure snapshot contract PASS.");
        return true;
    }

    internal static string RunMultiStackHaulFocused() =>
        VerifySequentialExactWarehouseLots();

    internal static string RunPartialPickupFocused() =>
        VerifyPartialHeavyStackReservation();

    internal static IEnumerator RunWholePickupAndMidHaulRestoreFocused(
        ICollection<string> evidence)
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException(
                "Whole-pickup and mid-haul restore evidence requires PlayMode.");
        if (evidence == null)
            throw new ArgumentNullException(nameof(evidence));

        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        float previousTimeScale = Time.timeScale;
        try
        {
            // This focused PlayMode route exercises production movement rather
            // than teleporting the restored cargo to its destination.  A bare
            // CharacterActor has only the defensive 0.1 movement fallback, so
            // initialize the normal authored runtime profile while keeping the
            // autonomous brain paused; AbilityHaul remains the sole action
            // owner for this fixture.
            scenario.Actor.Initialization(
                CharacterAiEditorTestDependencies
                    .RequireAuthoredCharacterDefinition("Slime"));
            scenario.Actor.SetAiPaused(true);
            Require(scenario.Actor.GetMoveSpeed() > 0.1f,
                "Whole-pickup restore fixture has no authored movement speed.");
            Time.timeScale = 8f;

            const int sourceQuantity = 4;
            Require(scenario.Items.SpawnItemAt(
                    LumberItemId,
                    sourceQuantity,
                    new Vector2Int(2, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == sourceQuantity,
                "Whole-pickup restore fixture could not spawn its source lot.");
            long massBefore = ExactItemMassGrams(scenario, LumberItemId);
            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string planFailure),
                "Whole-pickup restore fixture could not reserve a live plan: "
                + planFailure);
            Require(plan.ReservedStackQuantities.Count == 1
                    && plan.ReservedStackQuantities[0].Quantity
                        == sourceQuantity,
                "Whole-pickup restore fixture did not reserve the whole source lot.");
            WorldItemReservedStackQuantity reservation =
                plan.ReservedStackQuantities[0];
            string operationId = reservation.OwnerOperationId;
            Require(scenario.Items.TryPickupReservedStackQuantity(
                    scenario.Actor,
                    scenario.Carry,
                    reservation,
                    out int picked,
                    out string pickupFailure)
                && picked == sourceQuantity,
                "Whole-pickup restore fixture pickup failed: " + pickupFailure);
            Require(scenario.Items.TryCommitHaulPickup(
                    operationId,
                    scenario.Carry,
                    out string commitFailure),
                "Whole-pickup restore fixture could not commit pickup: "
                + commitFailure);

            AbilityHaul haul = scenario.Actor.GetComponent<AbilityHaul>();
            Require(haul != null,
                "Whole-pickup restore fixture has no AbilityHaul.");
            Require(haul.TryBindCapacityRoutingEditorFixture(
                    scenario.Items,
                    plan,
                    new[] { reservation.LeaseId },
                    out string bindFailure),
                "Whole-pickup restore fixture could not bind AbilityHaul: "
                + bindFailure);
            HaulDeliveryIntentSaveData intent =
                haul.CaptureDeliveryIntentForSave();
            Require(intent?.HasCommittedPickup == true
                    && string.Equals(
                        intent.operationId,
                        operationId,
                        StringComparison.Ordinal),
                "Whole-pickup restore fixture lost its committed intent.");
            DungeonPhysicalItemSaveData physicalBefore =
                scenario.Items.Capture();
            CharacterCarryInventorySaveData carryBefore =
                scenario.Carry.Capture();
            string carriedStackId = scenario.Carry.Items.Single().carriedStackId;
            long carriedMassBefore = scenario.Items.GetAllStacks()
                .Where(value => string.Equals(
                    value.StackId,
                    carriedStackId,
                    StringComparison.Ordinal))
                .Sum(value => ExactStackMassGrams(scenario, value));
            Require(carriedMassBefore == massBefore,
                "Whole pickup changed exact gram custody before restore.");

            haul.ClearRestoredDeliveryIntentBinding();
            scenario.Items.Restore(physicalBefore);
            scenario.Carry.Restore(carryBefore);
            Require(scenario.QuantityReservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> restoredLeases)
                    && restoredLeases.Count == 1,
                "Current-format restore lost the exact quantity lease.");
            Require(haul.TryRebindRestoredDeliveryIntent(
                    intent,
                    restoredLeases,
                    scenario.DestinationClaims,
                    out string restoreFailure),
                "Current-format mid-haul intent rebind failed: "
                + restoreFailure);
            Require(haul.CanStartHauling(out string canStartFailure)
                    && haul.CaptureActiveHaulOperationIds().SequenceEqual(
                        new[] { operationId },
                        StringComparer.Ordinal)
                    && scenario.Carry.Items.Sum(value => value.quantity)
                        == sourceQuantity
                    && ExactItemMassGrams(scenario, LumberItemId)
                        == massBefore,
                "Restored mid-haul authority was not resumable or exact: "
                + canStartFailure);
            evidence.Add(
                "ABILITY_HAUL_MID_HAUL_CURRENT_FORMAT_RESTORE_EXACT=PASS");
            evidence.Add(
                "ABILITY_HAUL_MID_HAUL_RESUME_AUTHORITY_JOINED=PASS");

            haul.StartHauling();
            int remainingFrames = 1200;
            while (remainingFrames-- > 0
                && (scenario.Carry.HasItems
                    || scenario.Items.TryCaptureHaulDeliveryIntent(
                        operationId,
                        out _)))
            {
                yield return null;
            }

            bool intentRemains = scenario.Items.TryCaptureHaulDeliveryIntent(
                operationId,
                out _);
            bool leasesRemain = scenario.QuantityReservations
                .TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> remainingLeases);
            AbilityMove movement = scenario.Actor.GetComponent<AbilityMove>();
            Require(!scenario.Carry.HasItems
                    && !intentRemains
                    && (!leasesRemain || remainingLeases.Count == 0),
                "Resumed whole pickup retained carry, intent, or lease authority: "
                + $"framesLeft={remainingFrames};carry={scenario.Carry.Items.Sum(value => value.quantity)};"
                + $"intent={intentRemains};leases={(leasesRemain ? remainingLeases.Count : 0)};"
                + $"actor={scenario.Actor.GetNowXY()};delivery=({intent.deliveryGridX},{intent.deliveryGridY});"
                + $"drop=({intent.dropGridX},{intent.dropGridY});hauling={haul.IsHauling};"
                + $"stage={haul.CurrentExecutionStage};unload={haul.CurrentUnloadReason};"
                + $"failure={haul.LastFailureReason};terminal={haul.LastTerminalDiagnostics};"
                + $"heartbeat={haul.RoutineHeartbeat};moveFailure={movement?.LastGridMoveFailureReason};"
                + $"moveActive={movement?.HasActiveMovementRoutineForDiagnostics};"
                + $"moveSpeed={scenario.Actor.GetMoveSpeed():0.###};"
                + $"timeScale={Time.timeScale:0.###};deltaTime={Time.deltaTime:0.######}.");
            Require(scenario.Items.GetAllStacks().Any(value =>
                        string.Equals(
                            value.ItemId,
                            LumberItemId,
                            StringComparison.Ordinal)
                        && value.State == WorldItemStackState.Stored
                        && value.Quantity == sourceQuantity)
                    && ExactItemMassGrams(scenario, LumberItemId)
                        == massBefore,
                "Resumed whole pickup did not reach exact stored custody.");

            DungeonPhysicalItemSaveData storedSnapshot =
                scenario.Items.Capture();
            scenario.Items.Restore(storedSnapshot);
            Require(scenario.Items.GetAllStacks().Any(value =>
                        string.Equals(
                            value.ItemId,
                            LumberItemId,
                            StringComparison.Ordinal)
                        && value.State == WorldItemStackState.Stored
                        && value.Quantity == sourceQuantity)
                    && ExactItemMassGrams(scenario, LumberItemId)
                        == massBefore,
                "Stored whole-pickup receipt did not round-trip current format.");
            evidence.Add("ABILITY_HAUL_WHOLE_PICKUP_DELIVERY_EXACT=PASS");
            evidence.Add(
                "ABILITY_HAUL_WHOLE_PICKUP_CURRENT_FORMAT_RESTORE_EXACT=PASS");
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            scenario.Dispose();
        }
    }

    [MenuItem("DungeonStory/Debug/Items/Run Equipment Haul Lease World-State Transition")]
    public static void RunEquipmentHaulLeaseWorldStateTransition()
    {
        using EditorVerificationSceneFixtureScope fixtureScene = new(
            "qa:equipment-haul-lease-world-state-transition");
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

    private static string VerifySequentialExactWarehouseLots()
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

            long massBefore = ExactItemMassGrams(scenario, stockId);
            Require(massBefore == 15_000L,
                $"three exact five-unit lots must total 15000g, got {massBefore}g");

            HashSet<string> operationIds = new(StringComparer.Ordinal);
            int deposited = 0;
            for (int lot = 1; lot <= 3; lot++)
            {
                Require(scenario.Items.TryReserveBestHaulPlan(
                        scenario.Actor,
                        out WorldItemHaulPlan plan,
                        out string failureReason),
                    $"sequential exact warehouse lot {lot} plan failed: "
                        + failureReason);
                Require(plan.PickupLegs.Count == 1
                        && plan.ReservedStackQuantities.Count == 1
                        && plan.ReservedStackQuantities[0].Quantity == 5,
                    $"warehouse lot {lot} was not one exact admitted five-unit lot: "
                        + $"pickups={plan.PickupLegs.Count}; "
                        + $"reservations={plan.ReservedStackQuantities.Count}; "
                        + $"quantity={plan.ReservedStackQuantities.Sum(value => value.Quantity)}");

                string operationId = CompleteExactWarehouseHaul(
                    scenario,
                    plan,
                    expectedQuantity: 5,
                    context: $"sequential warehouse lot {lot}");
                Require(operationIds.Add(operationId),
                    "sequential warehouse lots reused an operation identity: "
                        + operationId);
                deposited += 5;
                Require(scenario.Warehouse.Inventory.GetStock(
                            StockCategory.General) == deposited,
                    $"warehouse total after lot {lot} was not {deposited}");
                long massAfterLot = ExactItemMassGrams(scenario, stockId);
                Require(massAfterLot == massBefore,
                    $"warehouse lot {lot} changed exact physical mass: "
                        + $"before={massBefore}; after={massAfterLot}");
            }

            Require(operationIds.Count == 3,
                "sequential warehouse lots did not retain three unique operations");
            Require(!scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "a fourth warehouse haul job remained after all three lots");
            Require(!scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out _,
                    out _),
                "planner reserved a fourth warehouse haul job after all three lots");
            Require(!scenario.Carry.HasItems
                    && ExactItemMassGrams(scenario, stockId) == massBefore,
                "sequential warehouse completion leaked carry or physical mass");

            return "SEQUENTIAL_EXACT_WAREHOUSE_LOTS_PASS;"
                + $" operations={operationIds.Count}; deposited={deposited};"
                + $" totals=5,10,15; massGrams={massBefore}; fourthJob=False";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string CompleteExactWarehouseHaul(
        ScenarioRuntime scenario,
        WorldItemHaulPlan plan,
        int expectedQuantity,
        string context)
    {
        string warehouseDestination =
            WarehouseStorageIdentity.RequireDestinationId(scenario.Warehouse);
        Require(plan != null
                && plan.IsValid
                && plan.PrimaryDestination
                    == WorldItemHaulDestinationKind.Warehouse
                && string.Equals(
                    plan.PrimaryDestinationId,
                    warehouseDestination,
                    StringComparison.Ordinal)
                && plan.PickupLegs.Count == 1
                && plan.DeliveryLegs.Count == 1
                && plan.ReservedStackQuantities.Count == 1,
            context + " did not retain one exact warehouse lot");

        WorldItemReservedStackQuantity reservation =
            plan.ReservedStackQuantities[0];
        string operationId = reservation.OwnerOperationId;
        Require(expectedQuantity > 0
                && reservation.Quantity == expectedQuantity
                && reservation.DestinationKind
                    == WorldItemHaulDestinationKind.Warehouse
                && string.Equals(
                    reservation.DestinationId,
                    warehouseDestination,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(operationId),
            context + " changed quantity, destination, or operation identity");
        Require(scenario.Items.TryPickupReservedStackQuantity(
                scenario.Actor,
                scenario.Carry,
                reservation,
                out int picked,
                out string pickupFailure)
            && picked == expectedQuantity,
            context + " pickup failed: " + pickupFailure);
        Require(scenario.Items.TryCommitHaulPickup(
                operationId,
                scenario.Carry,
                out string commitFailure),
            context + " pickup commit failed: " + commitFailure);
        Require(scenario.Items.TryDepositCarriedItems(
                scenario.Actor,
                scenario.Carry,
                scenario.Warehouse,
                new[] { operationId },
                out string depositFailure),
            context + " owner-scoped deposit failed: " + depositFailure);
        Require(scenario.Items.ReleaseHaulDeliveryIntent(operationId),
            context + " did not release its committed delivery intent");
        bool hasLeases = scenario.QuantityReservations.TryGetLeasesByOwner(
            operationId,
            out IReadOnlyList<ItemQuantityLease> remainingLeases);
        Require(!scenario.Carry.HasItems
                && !scenario.Items.TryCaptureHaulDeliveryIntent(
                    operationId,
                    out _)
                && (!hasLeases || remainingLeases.Count == 0),
            context + " leaked carry, delivery intent, or quantity lease");
        return operationId;
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
            PhysicalItemMassSubject looseMassSubject =
                PhysicalItemMassSubjectAdapter.Create(
                    scenario.MassQuery,
                    (ItemDefinitionId)looseStack.ItemId,
                    looseStack.ItemInstanceId,
                    looseStack.Components);
            float exactLooseMassKg = scenario.MassQuery
                .GetStackUnitMass(
                    (ItemDefinitionId)looseStack.ItemId,
                    looseMassSubject)
                .Value / 1000f;
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
            Require(
                Mathf.Abs(plan.TotalWeight - exactLooseMassKg) < 0.0001f,
                $"haul planner used definition mass instead of the exact stack subject: "
                + $"planned={plan.TotalWeight}; exact={exactLooseMassKg}");
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

            long massBefore = ExactItemMassGrams(scenario, stockId);

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

            long massAfter = ExactItemMassGrams(scenario, stockId);
            Require(massAfter == massBefore,
                $"partial pickup changed physical mass: before={massBefore}; "
                + $"after={massAfter}");
            long carriedMass = scenario.Items.GetAllStacks()
                .Where(stack => string.Equals(
                        stack.ItemId,
                        stockId,
                        StringComparison.Ordinal)
                    && stack.State == WorldItemStackState.Carried)
                .Sum(stack => ExactStackMassGrams(scenario, stack));
            long sourceRemainderMass = scenario.Items.GetAllStacks()
                .Where(stack => string.Equals(
                        stack.ItemId,
                        stockId,
                        StringComparison.Ordinal)
                    && stack.State == WorldItemStackState.Loose)
                .Sum(stack => ExactStackMassGrams(scenario, stack));
            Require(carriedMass > 0L
                    && sourceRemainderMass > 0L
                    && checked(carriedMass + sourceRemainderMass) == massAfter,
                "partial pickup did not preserve exact carried/source mass custody");

            return $"reserved={reserved}; remaining={remaining}; "
                + $"carriedGrams={carriedMass}; sourceGrams={sourceRemainderMass}";
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
            long massBefore = ExactItemMassGrams(scenario, foodId);

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan firstPlan,
                    out string failureReason),
                "initial survival haul plan failed: " + failureReason);
            int firstReserved = firstPlan.ReservedStackQuantities.Sum(value => value.Quantity);
            Require(firstReserved == 9,
                $"one active consumer serving must remain loose, reserved={firstReserved}");
            string firstOperationId = CompleteExactWarehouseHaul(
                scenario,
                firstPlan,
                expectedQuantity: 9,
                context: "initial survival stock lot");
            Require(scenario.Warehouse.Inventory.GetStock(StockCategory.Food) == 9,
                "warehouse did not receive protected survival stock delivery");
            Require(ExactItemMassGrams(scenario, foodId) == massBefore,
                "initial survival stock delivery changed physical mass");

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan secondPlan,
                    out failureReason),
                "remaining survival stock did not unlock after delivery: " + failureReason);
            int secondReserved = secondPlan.ReservedStackQuantities.Sum(value => value.Quantity);
            Require(secondReserved == 1,
                $"remaining serving should be haulable after warehouse reserve exists, reserved={secondReserved}");
            string secondOperationId = CompleteExactWarehouseHaul(
                scenario,
                secondPlan,
                expectedQuantity: 1,
                context: "unlocked survival stock lot");
            Require(!string.Equals(
                    firstOperationId,
                    secondOperationId,
                    StringComparison.Ordinal),
                "survival stock lots reused one haul operation identity");
            Require(scenario.Warehouse.Inventory.GetStock(StockCategory.Food) == 10
                    && ExactItemMassGrams(scenario, foodId) == massBefore
                    && !scenario.Carry.HasItems,
                "survival stock completion changed quantity, mass, or carry custody");

            return $"firstReserved={firstReserved}; storedAfterFirst=9; "
                + $"secondReserved={secondReserved}; storedFinal=10; "
                + $"massGrams={massBefore}; operations=unique";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    public static void RunR31AvailabilityCacheContracts()
    {
        string occupiedJson;
        string emptyJson;
        using (ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f))
        {
            Require(scenario.Items.SpawnItemAt(LumberItemId, 4, new Vector2Int(2, 1),
                    WorldItemStackState.Loose, string.Empty, out int spawned) && spawned == 4,
                "R31 haul fixture could not create its exact source stock.");
            Require(scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "R31 haul fixture did not have a reachable initial plan.");
            int haulVersion = scenario.Items.HaulJobVersion;
            int warehouseVersion = scenario.WorldRegistry.WarehouseVersion;
            int traversalVersion = scenario.Grid.TraversalVersion;
            long claims = scenario.DestinationClaims.Revision;
            emptyJson = JsonUtility.ToJson(scenario.Carry.Capture());
            float unitKg = ExactItemMassGrams(scenario, LumberItemId) / 4000f;
            int occupiedQuantity = Mathf.CeilToInt(
                scenario.Carry.GetMaxAllowedWeight(new TestHaulingSettings(1.5f)) / unitKg) + 1;
            // 운반 DTO만 복원하여 물품 저장소와 지형 revision을 고정한다.
            scenario.Carry.Restore(new CharacterCarryInventorySaveData { items = new()
            {
                new() { itemId = LumberItemId, quantity = occupiedQuantity }
            }});
            occupiedJson = JsonUtility.ToJson(scenario.Carry.Capture());
            Require(!scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "A previously available plan ignored restored carry weight.");
            scenario.Carry.Restore(JsonUtility.FromJson<CharacterCarryInventorySaveData>(emptyJson));
            Require(scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "Clearing restored carry weight did not restore haul availability.");

            var performanceField = typeof(CharacterStats).GetField("performance",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ICharacterPerformanceQuery original = (ICharacterPerformanceQuery)performanceField.GetValue(scenario.Actor.Stats);
            R31CarryPerformance performance = new(original);
            performanceField.SetValue(scenario.Actor.Stats, performance);
            performance.Unavailable = true;
            Require(!scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "A required functional-capacity loss reused a positive haul cache.");

            WorldItemHaulPlanningService planner = (WorldItemHaulPlanningService)scenario.Reachability;
            bool rejectMass = true;
            IPhysicalItemMassQuery mass = R31StructureRemediationBatch.StrictProxy<IPhysicalItemMassQuery>(
                (method, args) =>
                {
                    if (method.Name == "get_AuthorityRevision") return scenario.MassQuery.AuthorityRevision + 1;
                    if (method.Name == "GetStackUnitMass" && rejectMass)
                    {
                        rejectMass = false;
                        throw new InvalidOperationException("r31-haul-mass-rejected");
                    }
                    return method.Invoke(scenario.MassQuery, args);
                });
            typeof(WorldItemHaulPlanningService).GetField("massQuery",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(planner, mass);
            performance.Unavailable = false;
            bool rejected = false;
            try { scenario.Items.HasAvailableHaulJob(scenario.Actor); }
            catch (InvalidOperationException error) when (error.Message == "r31-haul-mass-rejected")
            { rejected = true; }
            Require(rejected && !rejectMass, "R31 haul calculation failure was not observed.");
            Require(scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "Failed haul calculation published a new key with its old false value.");
            Require(scenario.Items.HaulJobVersion == haulVersion
                && scenario.WorldRegistry.WarehouseVersion == warehouseVersion
                && scenario.Grid.TraversalVersion == traversalVersion
                && scenario.DestinationClaims.Revision == claims,
                "The haul cache counterexample changed an unrelated key.");
        }
        using (ScenarioRuntime fresh = ScenarioRuntime.Create(lightStockWeight: 1f))
        {
            Require(fresh.Items.SpawnItemAt(LumberItemId, 4, new Vector2Int(2, 1),
                    WorldItemStackState.Loose, string.Empty, out int spawned) && spawned == 4,
                "Fresh haul fixture could not restore its independent source stock.");
            fresh.Carry.Restore(JsonUtility.FromJson<CharacterCarryInventorySaveData>(occupiedJson));
            Require(!fresh.Items.HasAvailableHaulJob(fresh.Actor),
                "Fresh owner ignored restored carry weight.");
            fresh.Carry.Restore(JsonUtility.FromJson<CharacterCarryInventorySaveData>(emptyJson));
            Require(fresh.Items.HasAvailableHaulJob(fresh.Actor),
                "Fresh owner did not recompute after carry JSON restore retry.");
        }
    }

    private sealed class R31CarryPerformance : ICharacterPerformanceQuery
    {
        private readonly ICharacterPerformanceQuery original;
        internal R31CarryPerformance(ICharacterPerformanceQuery original) => this.original = original;
        internal bool Unavailable { get; set; }
        private CharacterPerformanceSnapshot Apply(string id, Func<CharacterPerformanceSnapshot> calculate) =>
            Unavailable && id == "performance:survival:haul-capacity"
                ? new CharacterPerformanceSnapshot
                {
                    FormulaId = id, IsApplicable = false, Value = 0f,
                    Failure = new CharacterPerformanceFailure
                    {
                        Code = "RequiredFunctionalCapacityBelowThreshold",
                        CapacityId = "capacity:physical-power", CurrentValue = 0f, RequiredValue = 0.1f
                    }
                }
                : calculate();
        public CharacterFunctionalCapacitySnapshot GetFunctionalCapacities(CharacterActor actor) => original.GetFunctionalCapacities(actor);
        public CharacterPerformanceSnapshot Evaluate(CharacterActor actor, string id, float contextFactor = 1f, GameplayEffectContext effectContext = null) =>
            Apply(id, () => original.Evaluate(actor, id, contextFactor, effectContext));
        public CharacterPerformanceSnapshot Evaluate(CharacterActor actor, string id, CharacterPerformanceEvaluationContext context) =>
            Apply(id, () => original.Evaluate(actor, id, context));
        public CharacterPerformanceSnapshot EvaluateWork(CharacterActor actor, WorkTypeId id, CharacterPerformanceResultChannel channel, CharacterPerformanceEvaluationContext context) =>
            original.EvaluateWork(actor, id, channel, context);
        public IReadOnlyList<CharacterPerformanceSnapshot> EvaluateDomain(CharacterActor actor, CharacterPerformanceFormulaDomain domain) => original.EvaluateDomain(actor, domain);
    }

    private static string VerifyAvailabilityRequiresReachableWarehouse()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        try
        {
            const int quantity = 4;
            Vector2Int stackPosition = new Vector2Int(2, 1);
            Require(scenario.Items.SpawnItemAt(
                    LumberItemId,
                    quantity,
                    stackPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == quantity,
                "reachability stack spawn failed");

            WorldItemStackSnapshot stack = scenario.Items
                .GetStacksAt(stackPosition)
                .Single(candidate => string.Equals(
                    candidate.ItemId,
                    LumberItemId,
                    StringComparison.Ordinal));
            Require(scenario.Items.PrioritizeHaul(stack.StackId),
                "reachability stack priority failed");

            ItemStackId stackId = new ItemStackId(stack.StackId);
            int worldCountBefore = scenario.Items.GetAllStacks()
                .Where(candidate => string.Equals(
                    candidate.ItemId,
                    LumberItemId,
                    StringComparison.Ordinal))
                .Sum(candidate => candidate.Quantity);
            long worldMassBefore = ExactItemMassGrams(scenario, LumberItemId);
            int availableBefore = scenario.QuantityReservations
                .GetAvailableQuantity(stackId);
            int leaseCountBefore = scenario.QuantityReservations
                .GetLeasesForStack(stackId)
                .Count;
            int cargoCountBefore = scenario.Carry.Items
                .Where(candidate => candidate != null)
                .Sum(candidate => candidate.quantity);

            Require(scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "connected warehouse should be available before the barrier");

            const int barrierX = 6;
            for (int y = 0; y < scenario.Grid.height; y++)
            {
                Require(scenario.Grid.SetAreaType(
                        new Vector2Int(barrierX, y),
                        GridCellAreaType.BlockedExterior),
                    $"failed to close reachability barrier at y={y}");
            }

            Require(!scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "availability reported an unreachable warehouse");
            Require(!scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out _,
                    out string unreachableReason),
                "planner reserved a haul plan for an unreachable warehouse");
            Require(unreachableReason.Contains(
                    "no reachable destination",
                    StringComparison.Ordinal),
                "unexpected unreachable failure: " + unreachableReason);

            string exactWarehouseDestination =
                WarehouseStorageIdentity.RequireDestinationId(
                    scenario.Warehouse);
            Require(scenario.Items.TryRequestStackDelivery(
                    stack.StackId,
                    quantity,
                    scenario.Warehouse.centerPos,
                    exactWarehouseDestination,
                    out int exactRequested,
                    out string exactRequestFailure)
                && exactRequested == quantity,
                "exact warehouse route request failed: "
                    + exactRequestFailure);
            Require(!scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "availability reported an unreachable explicit warehouse");
            Require(!scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out _,
                    out string explicitUnreachableReason),
                "planner reserved an explicit unreachable warehouse route");
            Require(explicitUnreachableReason.Contains(
                    "explicit destination is unreachable",
                    StringComparison.Ordinal),
                "unexpected explicit unreachable failure: "
                    + explicitUnreachableReason);

            int worldCountAfterFailure = scenario.Items.GetAllStacks()
                .Where(candidate => string.Equals(
                    candidate.ItemId,
                    LumberItemId,
                    StringComparison.Ordinal))
                .Sum(candidate => candidate.Quantity);
            long worldMassAfterFailure = ExactItemMassGrams(
                scenario,
                LumberItemId);
            Require(worldCountAfterFailure == worldCountBefore,
                $"unreachable planning changed count: before={worldCountBefore}; "
                + $"after={worldCountAfterFailure}");
            Require(worldMassAfterFailure == worldMassBefore,
                $"unreachable planning changed mass: before={worldMassBefore}; "
                + $"after={worldMassAfterFailure}");
            Require(scenario.QuantityReservations.GetAvailableQuantity(stackId)
                    == availableBefore,
                "unreachable planning changed available quantity");
            Require(scenario.QuantityReservations.GetLeasesForStack(stackId).Count
                    == leaseCountBefore,
                "unreachable planning created or removed a quantity lease");
            Require(scenario.Carry.Items
                    .Where(candidate => candidate != null)
                    .Sum(candidate => candidate.quantity)
                    == cargoCountBefore
                && !scenario.Carry.HasItems,
                "unreachable planning changed carried cargo");

            for (int y = 0; y < scenario.Grid.height; y++)
            {
                Require(scenario.Grid.SetAreaType(
                        new Vector2Int(barrierX, y),
                        GridCellAreaType.ExteriorPath),
                    $"failed to reopen reachability barrier at y={y}");
            }

            Require(scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "availability did not recover after reopening the barrier");
            Require(ExactItemMassGrams(scenario, LumberItemId) == worldMassBefore
                && scenario.QuantityReservations.GetLeasesForStack(stackId).Count
                    == leaseCountBefore
                && !scenario.Carry.HasItems,
                "availability recovery mutated mass, leases, or cargo");

            return "HAUL_AVAILABILITY_REACHABLE_DESTINATION_PARITY_PASS;"
                + $" count={worldCountBefore}; massGrams={worldMassBefore};"
                + $" leases={leaseCountBefore}; cargo={cargoCountBefore};"
                + $" genericFailure={unreachableReason};"
                + $" explicitFailure={explicitUnreachableReason}";
        }
        finally
        {
            scenario.Dispose();
        }
    }

    private static string VerifySeedLotReachableFallbackDelivery()
    {
        const string cropId = "crop:qa-haul-seed-fallback";
        const string destinationId = "qa:seed-fallback-destination";
        const string ownerDomain = "qa.seed-fallback";
        Vector2Int highPosition = new(8, 1);
        Vector2Int lowPosition = new(2, 1);
        Vector2Int destinationPosition = new(4, 1);
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        FacilityBufferDestinationClaim claim = null;
        try
        {
            claim = new FacilityBufferDestinationClaim(
                destinationId,
                destinationPosition,
                ownerDomain,
                "qa:seed-fallback-operation",
                ownerFacilityId: null,
                FacilityBufferDestinationAnchorKind.ReservedTarget);
            Require(scenario.DestinationClaims.TryClaim(
                    claim,
                    out FacilityBufferDestinationClaimFailureCode claimFailure,
                    out string claimReason),
                $"seed fallback destination claim failed: {claimFailure}:{claimReason}");

            Require(scenario.Transfers.TrySpawnItemWithComponents(
                    LumberItemId,
                    1,
                    highPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    new[]
                    {
                        SeedLotItemStateCodec.Encode(new SeedLotState
                        {
                            cropId = cropId,
                            cultivarGenomeId = "genome:qa:high",
                            generation = 4,
                            pathogenLoad = 1f
                        })
                    },
                    out int highSpawned)
                && highSpawned == 1,
                "inaccessible high-quality seed spawn failed");
            Require(scenario.Transfers.TrySpawnItemWithComponents(
                    LumberItemId,
                    1,
                    lowPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    new[]
                    {
                        SeedLotItemStateCodec.Encode(new SeedLotState
                        {
                            cropId = cropId,
                            cultivarGenomeId = "genome:qa:low",
                            generation = 1,
                            pathogenLoad = 20f
                        })
                    },
                    out int lowSpawned)
                && lowSpawned == 1,
                "reachable lower-quality seed spawn failed");

            WorldItemStackSnapshot high = scenario.Items.GetStacksAt(highPosition)
                .Single(value => string.Equals(
                    value.ItemId,
                    LumberItemId,
                    StringComparison.Ordinal));
            WorldItemStackSnapshot low = scenario.Items.GetStacksAt(lowPosition)
                .Single(value => string.Equals(
                    value.ItemId,
                    LumberItemId,
                    StringComparison.Ordinal));
            string highSignature = high.StackSignature;
            string lowSignature = low.StackSignature;
            int totalCountBefore = scenario.Items.GetAllStacks()
                .Where(value => string.Equals(
                    value.ItemId,
                    LumberItemId,
                    StringComparison.Ordinal))
                .Sum(value => value.Quantity);
            long totalMassBefore = ExactItemMassGrams(scenario, LumberItemId);

            const int barrierX = 6;
            for (int y = 0; y < scenario.Grid.height; y++)
            {
                Require(scenario.Grid.SetAreaType(
                        new Vector2Int(barrierX, y),
                        GridCellAreaType.BlockedExterior),
                    $"failed to close seed fallback barrier at y={y}");
            }

            WorldItemDeliveryReachabilityStatus highReachability = scenario.Reachability
                .AssessExactStackDelivery(
                    (ItemStackId)high.StackId,
                    1,
                    destinationPosition,
                    destinationId,
                    out string highReachabilityReason);
            Require(highReachability == WorldItemDeliveryReachabilityStatus.Unreachable,
                "high-quality seed was not classified unreachable: "
                + highReachability + ":" + highReachabilityReason);
            WorldItemDeliveryReachabilityStatus lowReachability = scenario.Reachability
                .AssessExactStackDelivery(
                    (ItemStackId)low.StackId,
                    1,
                    destinationPosition,
                    destinationId,
                    out string lowReachabilityReason);
            Require(lowReachability == WorldItemDeliveryReachabilityStatus.Reachable,
                "lower-quality seed was not classified reachable: "
                + lowReachability + ":" + lowReachabilityReason);

            PhysicalSeedLotGateway gateway = new(
                scenario.Stock,
                scenario.Transfers,
                scenario.Items,
                scenario.Reachability,
                scenario.DestinationRelease);
            Require(gateway.RequestBestSeedLot(
                    LumberItemId,
                    cropId,
                    destinationPosition,
                    destinationId,
                    out int requested,
                    out DomainFailure requestFailure)
                && requested == 1
                && !requestFailure.IsFailure,
                "seed fallback request failed: " + requestFailure.Code);

            WorldItemStackSnapshot highAfterRequest = scenario.Items.GetAllStacks()
                .Single(value => string.Equals(
                    value.StackId,
                    high.StackId,
                    StringComparison.Ordinal));
            WorldItemStackSnapshot lowAfterRequest = scenario.Items.GetAllStacks()
                .Single(value => string.Equals(
                    value.StackId,
                    low.StackId,
                    StringComparison.Ordinal));
            Require(highAfterRequest.Position == highPosition
                    && highAfterRequest.Quantity == 1
                    && highAfterRequest.State == WorldItemStackState.Loose
                    && string.IsNullOrEmpty(highAfterRequest.DestinationId)
                    && string.Equals(
                        highAfterRequest.StackSignature,
                        highSignature,
                        StringComparison.Ordinal),
                "inaccessible high-quality seed ownership or components changed");
            Require(string.Equals(
                    lowAfterRequest.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && lowAfterRequest.DestinationPosition == destinationPosition,
                "reachable seed was not retargeted to the exact destination");
            Require(scenario.QuantityReservations
                    .GetLeasesForStack((ItemStackId)high.StackId).Count == 0
                && scenario.QuantityReservations
                    .GetLeasesForStack((ItemStackId)low.StackId).Count == 0
                && !scenario.Carry.HasItems,
                "seed selector mutated leases or cargo before planning");

            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string planFailure),
                "reachable seed haul plan failed: " + planFailure);
            Require(plan.ReservedStackQuantities.Count == 1
                && plan.PrimaryDestination
                    == WorldItemHaulDestinationKind.FacilityBuffer
                && string.Equals(
                    plan.ReservedStackQuantities[0].StackId,
                    low.StackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    plan.PrimaryDestinationId,
                    destinationId,
                    StringComparison.Ordinal),
                "haul planner did not reserve the reachable exact seed route");
            foreach (WorldItemReservedStackQuantity reservation in
                     plan.ReservedStackQuantities)
            {
                Require(scenario.Items.TryPickupReservedStackQuantity(
                        scenario.Actor,
                        scenario.Carry,
                        reservation,
                        out int pickedUp,
                        out string pickupFailure)
                    && pickedUp == reservation.Quantity,
                    "reachable seed pickup failed: " + pickupFailure);
            }
            string operationId = plan.ReservedStackQuantities[0].OwnerOperationId;
            Require(!string.IsNullOrEmpty(operationId),
                "reachable seed reservation had no operation authority");
            Require(scenario.Items.TryCommitHaulPickup(
                    operationId,
                    scenario.Carry,
                    out string commitFailure),
                "reachable seed pickup commit failed: " + commitFailure);
            Require(scenario.Items.TryDepositCarriedItemsToFacility(
                    scenario.Actor,
                    scenario.Carry,
                    destinationPosition,
                    destinationId,
                    new[] { operationId },
                    out string depositFailure),
                "reachable seed deposit failed: " + depositFailure);
            Require(scenario.QuantityReservations.ReleaseByOwner(
                        operationId,
                        ItemReservationReleaseReason.Completed) > 0,
                "completed reachable seed delivery did not release its lease");
            Require(scenario.Items.ReleaseHaulDeliveryIntent(operationId),
                "completed reachable seed delivery did not release its intent");

            WorldItemStackSnapshot delivered = scenario.Items.GetAllStacks()
                .Single(value => value.State == WorldItemStackState.FacilityBuffer
                    && value.Position == destinationPosition
                    && string.Equals(
                        value.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.ItemId,
                        LumberItemId,
                        StringComparison.Ordinal));
            SeedLotState deliveredSeed = SeedLotItemStateCodec.Decode(
                delivered.Components);
            WorldItemStackSnapshot highAfterDelivery = scenario.Items.GetAllStacks()
                .Single(value => string.Equals(
                    value.StackId,
                    high.StackId,
                    StringComparison.Ordinal));
            Require(delivered.State == WorldItemStackState.FacilityBuffer
                    && delivered.Position == destinationPosition
                    && string.Equals(
                        delivered.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        delivered.StackSignature,
                        lowSignature,
                        StringComparison.Ordinal)
                    && string.Equals(
                        deliveredSeed.cultivarGenomeId,
                        "genome:qa:low",
                        StringComparison.Ordinal)
                    && deliveredSeed.generation == 1
                    && Mathf.Approximately(deliveredSeed.pathogenLoad, 20f),
                "reachable seed did not finish exact facility delivery");
            Require(highAfterDelivery.Position == highPosition
                    && highAfterDelivery.Quantity == 1
                    && highAfterDelivery.State == WorldItemStackState.Loose
                    && string.IsNullOrEmpty(highAfterDelivery.DestinationId)
                    && string.Equals(
                        highAfterDelivery.StackSignature,
                        highSignature,
                        StringComparison.Ordinal),
                "high-quality inaccessible seed changed after fallback delivery");
            Require(!scenario.Carry.HasItems
                && scenario.Items.CaptureHaulDeliveryIntentsByDestination(
                    destinationId).Count == 0
                && (!scenario.QuantityReservations.TryGetLeasesByOwner(
                        operationId,
                        out IReadOnlyList<ItemQuantityLease> terminalLeases)
                    || terminalLeases.Count == 0)
                && scenario.Items.GetAllStacks()
                    .Where(value => string.Equals(
                        value.ItemId,
                        LumberItemId,
                        StringComparison.Ordinal))
                    .Sum(value => value.Quantity) == totalCountBefore
                && ExactItemMassGrams(scenario, LumberItemId) == totalMassBefore,
                "fallback delivery leaked cargo, intent, quantity, or mass");
            return "SEED_REACHABLE_FALLBACK_EXACT_DELIVERY_PASS;"
                + $" high={high.StackId}; low={low.StackId};"
                + $" count={totalCountBefore}; massGrams={totalMassBefore}";
        }
        finally
        {
            if (claim != null)
            {
                scenario.DestinationClaims.TryRevoke(
                    claim,
                    out _,
                    out _);
            }
            scenario.Dispose();
        }
    }

    private static string VerifyCommittedDeliveryBeatsGenericPriority()
    {
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        try
        {
            const string destinationId = "qa:committed-haul-destination";
            const string ownerDomain = "qa.haul-priority";
            Vector2Int regularPosition = new(2, 1);
            Vector2Int committedPosition = new(4, 1);
            Vector2Int destinationPosition = new(8, 1);
            Require(scenario.DestinationClaims.TryClaim(
                    new FacilityBufferDestinationClaim(
                        destinationId,
                        destinationPosition,
                        ownerDomain,
                        "qa:committed-haul-operation",
                        ownerFacilityId: null,
                        FacilityBufferDestinationAnchorKind.ReservedTarget),
                    out FacilityBufferDestinationClaimFailureCode claimFailure,
                    out string claimReason),
                $"destination claim failed: {claimFailure}:{claimReason}");
            Require(scenario.Items.SpawnItemAt(
                    LumberItemId,
                    20,
                    regularPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _),
                "generic priority stack spawn failed");
            Require(scenario.Items.SpawnItemAt(
                    LumberItemId,
                    1,
                    committedPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _),
                "committed stack spawn failed");

            WorldItemStackSnapshot regular = scenario.Items
                .GetStacksAt(regularPosition)
                .Single();
            WorldItemStackSnapshot committed = scenario.Items
                .GetStacksAt(committedPosition)
                .Single();
            Require(scenario.Items.PrioritizeHaul(regular.StackId),
                "generic priority flag failed");
            Require(scenario.Items.TryRequestStackDelivery(
                    committed.StackId,
                    1,
                    destinationPosition,
                    destinationId,
                    out int requested,
                    out string requestFailure)
                && requested == 1,
                "committed delivery request failed: " + requestFailure);
            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string planFailure),
                "committed delivery plan failed: " + planFailure);
            WorldItemReservedStackQuantity seed =
                plan.PickupLegs[0].Reservation;
            Require(string.Equals(
                        seed.StackId,
                        committed.StackId,
                        StringComparison.Ordinal)
                    && seed.DestinationKind
                        == WorldItemHaulDestinationKind.FacilityBuffer
                    && string.Equals(
                        seed.DestinationId,
                        destinationId,
                        StringComparison.Ordinal),
                $"generic priority starved committed delivery: seed={seed.StackId}; "
                + $"expected={committed.StackId}; destination={seed.DestinationId}");

            return $"committedSeed={seed.StackId}; genericPriority={regular.StackId}; "
                + $"destination={seed.DestinationId}";
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

    private static string VerifyConstructionDestinationWorkOrderIdentity()
    {
        const string orderId = "work:qa-haul-construction-destination";
        Vector2Int sitePosition = new(5, 1);
        ScenarioRuntime scenario = ScenarioRuntime.Create(lightStockWeight: 1f);
        GameObject siteObject = null;
        GameObject duplicateSiteObject = null;
        BuildingSO wallData = null;
        ConstructionSite site = null;
        ConstructionSite duplicateSite = null;
        try
        {
            wallData = CreateBuildingData(
                99003,
                "운반 목적지 테스트 벽",
                BuildingCategory.Wall,
                GridLayer.Building);
            siteObject = new GameObject("HaulConstructionDestinationSite");
            site = siteObject.AddComponent<ConstructionSite>();
            site.Construct(new DungeonStory.Foundation.UnityUiClock());
            site.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            site.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(
                    new NoopBlueprintResearchWorkService()),
                FacilityCandidateCache,
                RoomFacilityPolicy,
                combatEquipmentRuntime: null,
                worldRegistry: null,
                worldItemStackRuntime: null,
                abilityRuntimeDispatcher: CharacterAiEditorTestDependencies.BuildingAbilityRuntimeDispatcher,
                gameClock: null,
                paidFacilityContracts: null,
                evolutionState: new FacilityEvolutionStateComponentFactory());
            site.SetGrid(scenario.Grid);
            site.Initialization(wallData, sitePosition);
            site.ConfigureSite(orderId, () => true, () => { });
            scenario.WorldRegistry.RegisterBuilding(site);

            string canonicalDestination =
                WorkConstructionInputOwnerAuthority.DestinationFor(orderId);
            Require(WorldItemHaulDestinationAuthority.TryResolve(
                    scenario.Grid,
                    scenario.WorldRegistry,
                    scenario.DestinationClaims,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    canonicalDestination,
                    sitePosition,
                    out WorldItemHaulDestinationAuthority.Resolution resolution,
                    out string canonicalFailure),
                "canonical construction destination did not resolve through the "
                    + "normal no-work-query authority path: " + canonicalFailure);
            Require(resolution.Kind
                        == WorldItemHaulDestinationKind.FacilityBuffer
                    && string.Equals(
                        resolution.DestinationId,
                        canonicalDestination,
                        StringComparison.Ordinal)
                    && resolution.Warehouse == null
                    && resolution.DeliveryPosition == sitePosition
                    && resolution.DropPosition == sitePosition,
                "canonical construction destination changed kind, identity, or "
                    + $"position: kind={resolution.Kind}; id={resolution.DestinationId}; "
                    + $"delivery={resolution.DeliveryPosition}; drop={resolution.DropPosition}");

            string legacyCoordinateDestination =
                WorkOrderRuntime.ConstructionDestinationPrefix
                + $"{wallData.id}:{sitePosition.x}:{sitePosition.y}";
            Require(!WorldItemHaulDestinationAuthority.TryResolve(
                    scenario.Grid,
                    scenario.WorldRegistry,
                    scenario.DestinationClaims,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    legacyCoordinateDestination,
                    sitePosition,
                    out _,
                    out string legacyFailure)
                && string.Equals(
                    legacyFailure,
                    "haul-destination-construction-order-missing-or-ambiguous:"
                        + legacyCoordinateDestination,
                    StringComparison.Ordinal),
                "legacy coordinate construction destination was accepted or lost "
                    + "its strict rejection: " + legacyFailure);

            string wrongOrderDestination =
                WorkConstructionInputOwnerAuthority.DestinationFor(
                    "work:qa-haul-construction-destination-other");
            Require(!WorldItemHaulDestinationAuthority.TryResolve(
                    scenario.Grid,
                    scenario.WorldRegistry,
                    scenario.DestinationClaims,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    wrongOrderDestination,
                    sitePosition,
                    out _,
                    out string wrongOrderFailure)
                && string.Equals(
                    wrongOrderFailure,
                    "haul-destination-construction-order-missing-or-ambiguous:"
                        + wrongOrderDestination,
                    StringComparison.Ordinal),
                "wrong construction order identity was accepted or lost its "
                    + "strict rejection: " + wrongOrderFailure);

            Vector2Int wrongPosition = new(sitePosition.x + 1, sitePosition.y);
            Require(!WorldItemHaulDestinationAuthority.TryResolve(
                    scenario.Grid,
                    scenario.WorldRegistry,
                    scenario.DestinationClaims,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    canonicalDestination,
                    wrongPosition,
                    out _,
                    out string wrongPositionFailure)
                && string.Equals(
                    wrongPositionFailure,
                    "haul-destination-construction-order-missing-or-ambiguous:"
                        + canonicalDestination,
                    StringComparison.Ordinal),
                "construction destination resolved at the wrong position or lost "
                    + "its strict rejection: " + wrongPositionFailure);

            scenario.WorldRegistry.UnregisterBuilding(site);
            Require(!WorldItemHaulDestinationAuthority.TryResolve(
                    scenario.Grid,
                    scenario.WorldRegistry,
                    scenario.DestinationClaims,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    canonicalDestination,
                    sitePosition,
                    out _,
                    out string missingSiteFailure)
                && string.Equals(
                    missingSiteFailure,
                    "haul-destination-construction-order-missing-or-ambiguous:"
                        + canonicalDestination,
                    StringComparison.Ordinal),
                "missing construction site was accepted or lost its strict "
                    + "rejection: " + missingSiteFailure);
            scenario.WorldRegistry.RegisterBuilding(site);

            duplicateSiteObject = new GameObject(
                "HaulConstructionDestinationDuplicateSite");
            duplicateSite = duplicateSiteObject.AddComponent<ConstructionSite>();
            duplicateSite.Construct(new DungeonStory.Foundation.UnityUiClock());
            duplicateSite.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            duplicateSite.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(
                    new NoopBlueprintResearchWorkService()),
                FacilityCandidateCache,
                RoomFacilityPolicy,
                combatEquipmentRuntime: null,
                worldRegistry: null,
                worldItemStackRuntime: null,
                abilityRuntimeDispatcher: CharacterAiEditorTestDependencies.BuildingAbilityRuntimeDispatcher,
                gameClock: null,
                paidFacilityContracts: null,
                evolutionState: new FacilityEvolutionStateComponentFactory());
            duplicateSite.SetGrid(scenario.Grid);
            duplicateSite.Initialization(wallData, sitePosition);
            duplicateSite.ConfigureSite(orderId, () => true, () => { });
            scenario.WorldRegistry.RegisterBuilding(duplicateSite);
            Require(!WorldItemHaulDestinationAuthority.TryResolve(
                    scenario.Grid,
                    scenario.WorldRegistry,
                    scenario.DestinationClaims,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    canonicalDestination,
                    sitePosition,
                    out _,
                    out string duplicateFailure)
                && string.Equals(
                    duplicateFailure,
                    "haul-destination-construction-order-missing-or-ambiguous:"
                        + canonicalDestination,
                    StringComparison.Ordinal),
                "duplicate same-order construction sites were accepted or lost "
                    + "their strict rejection: " + duplicateFailure);
            scenario.WorldRegistry.UnregisterBuilding(duplicateSite);

            Require(scenario.Items.SpawnItemAt(
                    LumberItemId,
                    2,
                    new Vector2Int(2, 1),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 2,
                "construction destination plan source spawn failed");
            WorldItemStackSnapshot source = scenario.Items
                .GetStacksAt(new Vector2Int(2, 1))
                .Single(candidate => string.Equals(
                    candidate.ItemId,
                    LumberItemId,
                    StringComparison.Ordinal));
            Require(scenario.Items.TryRequestStackDelivery(
                    source.StackId,
                    2,
                    sitePosition,
                    canonicalDestination,
                    out int requested,
                    out string requestFailure)
                && requested == 2,
                "construction destination delivery request failed: "
                    + requestFailure);
            Require(scenario.Items.HasAvailableHaulJob(scenario.Actor),
                "normal planner preview did not expose the canonical construction "
                    + "destination");
            Require(scenario.Items.TryReserveBestHaulPlan(
                    scenario.Actor,
                    out WorldItemHaulPlan plan,
                    out string planFailure),
                "normal planner could not reserve the canonical construction route: "
                    + planFailure);
            Require(plan.IsValid
                    && plan.PrimaryDestination
                        == WorldItemHaulDestinationKind.FacilityBuffer
                    && string.Equals(
                        plan.PrimaryDestinationId,
                        canonicalDestination,
                        StringComparison.Ordinal)
                    && plan.DeliveryLegs.Count == 1
                    && plan.DeliveryLegs[0].DeliveryPosition == sitePosition
                    && plan.DeliveryLegs[0].DropPosition == sitePosition
                    && plan.ReservedStackQuantities.Count == 1
                    && string.Equals(
                        plan.ReservedStackQuantities[0].DestinationId,
                        canonicalDestination,
                        StringComparison.Ordinal),
                "normal planner reservation changed canonical construction identity, "
                    + "kind, or exact position");

            return "CONSTRUCTION_DESTINATION_WORK_ORDER_IDENTITY_PASS;"
                + $" order={orderId}; destination={canonicalDestination};"
                + " negatives=legacy,wrong-order,missing-site,wrong-position,duplicate;"
                + $" reserved={plan.ReservedStackQuantities[0].Quantity}";
        }
        finally
        {
            if (duplicateSite != null)
                scenario.WorldRegistry.UnregisterBuilding(duplicateSite);
            if (site != null)
                scenario.WorldRegistry.UnregisterBuilding(site);
            DestroyImmediateSafe(duplicateSiteObject);
            DestroyImmediateSafe(siteObject);
            DestroyImmediateSafe(wallData);
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
            site.Construct(new DungeonStory.Foundation.UnityUiClock());
            site.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            site.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(new NoopBlueprintResearchWorkService()),
                FacilityCandidateCache,
                RoomFacilityPolicy,
                combatEquipmentRuntime: null,
                worldRegistry: null,
                worldItemStackRuntime: null,
                abilityRuntimeDispatcher: CharacterAiEditorTestDependencies.BuildingAbilityRuntimeDispatcher,
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
        private readonly Grid previousWorldGrid;

        private ScenarioRuntime(
            Grid grid,
            GridProvider gridProvider,
            WorldItemStackRuntime items,
            CombatEquipmentRuntime equipment,
            ItemQuantityReservationService quantityReservations,
            TestWarehouseBuilding warehouse,
            CharacterActor actor,
            CharacterCarryInventory carry,
            ICharacterAiWorldRegistry worldRegistry,
            Grid previousWorldGrid,
            IPhysicalItemMassQuery massQuery,
            FacilityBufferDestinationClaimRegistry destinationClaims,
            IStockQuery stock,
            IItemTransferService transfers,
            IWorldItemDeliveryReachabilityQuery reachability,
            IFacilityBufferDestinationReleaseService destinationRelease)
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
            this.previousWorldGrid = previousWorldGrid;
            MassQuery = massQuery;
            DestinationClaims = destinationClaims;
            Stock = stock;
            Transfers = transfers;
            Reachability = reachability;
            DestinationRelease = destinationRelease;
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
        public IPhysicalItemMassQuery MassQuery { get; }
        public FacilityBufferDestinationClaimRegistry DestinationClaims { get; }
        public IStockQuery Stock { get; }
        public IItemTransferService Transfers { get; }
        public IWorldItemDeliveryReachabilityQuery Reachability { get; }
        public IFacilityBufferDestinationReleaseService DestinationRelease { get; }

        public static ScenarioRuntime Create(float lightStockWeight)
        {
            Grid grid = CreateWalkableExteriorGrid();
            GridProvider gridProvider = new GridProvider(grid);
            ScenarioRuntime scenario = null;
            GameObject warehouseObject = null;
            GameObject actorObject = null;
            BuildingSO warehouseData = null;
            ICharacterAiWorldRegistry worldRegistry = null;
            Grid previousWorldGrid = null;
            bool worldGridRebound = false;
            WorldItemStackRuntime items = null;
            try
            {
                warehouseObject = new GameObject("HaulPlanWarehouse");
                TestWarehouseBuilding warehouse = warehouseObject.AddComponent<TestWarehouseBuilding>();
                warehouseData = CreateBuildingData(99001, "테스트 창고", BuildingCategory.Shop, GridLayer.Building);
                warehouse.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
                warehouse.ConstructBuildableObject(
                    new BuildingResearchWorkPortAdapter(new NoopBlueprintResearchWorkService()),
                    FacilityCandidateCache,
                    RoomFacilityPolicy,
                    combatEquipmentRuntime: null,
                    worldRegistry: null,
                    worldItemStackRuntime: null,
                    abilityRuntimeDispatcher: CharacterAiEditorTestDependencies.BuildingAbilityRuntimeDispatcher,
                    gameClock: null,
                    paidFacilityContracts: null,
                    evolutionState: new FacilityEvolutionStateComponentFactory());
                warehouse.SetGrid(grid);
                warehouse.Initialization(warehouseData, new Vector2Int(9, 1));
                grid.RegisterOccupant(warehouse, GridLayer.Building, warehouse.buildPoses, false);

                actorObject = CreateActor("HaulPlanActor", gridProvider, grid, new Vector2Int(0, 1));
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
                IGameClock gameClock = new UnityGameClock();
                IGridPathSearchBroker pathBroker =
                    new GridPathSearchBroker(gameClock, doorAccessQuery: null, performanceRecorder: null, costPolicy: null);
                worldRegistry =
                    CharacterAiEditorTestDependencies.WorldRegistry;
                worldRegistry.TryGetGrid(out previousWorldGrid);
                worldRegistry.SetGrid(grid);
                worldGridRebound = true;
                worldRegistry.RegisterBuilding(warehouse);
                worldRegistry.RegisterCharacter(actor);
                WorldItemRepository repository = new WorldItemRepository(
                    new GuidPersistentIdGenerator(),
                    new DungeonRuntimeAggregateRootStore());
                FacilityBufferDestinationClaimRegistry destinationClaims = new();
                IPhysicalItemMassQuery massQuery =
                    new PhysicalItemMassQuery(itemCatalog);
                IStockQuery stock = new PhysicalStockQuery(
                    repository,
                    itemCatalog,
                    massQuery);
                warehouse.BindPhysicalStock(stock);
                WarehouseMassAdmissionService warehouseMassAdmission = new(
                    itemCatalog,
                    massQuery,
                    stock,
                    worldRegistry,
                    gameClock,
                    repository);
                ItemQuantityReservationService quantityReservations =
                    new ItemQuantityReservationService(
                        repository,
                        EditorNullItemMarkerPresenter.Instance,
                        gameClock);
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
                        itemStackRuntime: equipmentItemGateway,
                        calendar: new EditorFixedGameCalendar(137, 9));
                WorldItemHaulPlanningService haulPlanning =
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
                        destinationClaims,
                        warehouseMassAdmission: warehouseMassAdmission);
                WorldItemReadServices readServices = new WorldItemReadServices(
                    itemCatalog,
                    massQuery,
                    haulingSettings,
                    query,
                    EditorNullItemMarkerPresenter.Instance,
                    new EditorCharacterAiPerformanceRecorder(),
                    DisabledDungeonDebugRuleQuery.Instance,
                    new FacilityOutputClearanceTelemetryRuntime());
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
                        quantityReservations: quantityReservations,
                        massAdmission: warehouseMassAdmission),
                    quantityReservations: quantityReservations,
                    quantityLeaseMutations: quantityReservations,
                    bufferAggregation: bufferAggregation,
                    physicalItemRelocations:
                        new DeterministicPhysicalItemRelocationOutcomeFixture()
                            .CreateService(
                                repository,
                                massQuery,
                                itemCatalog,
                                EditorNullItemMarkerPresenter.Instance),
                    warehouseMassAdmission: warehouseMassAdmission);
                items = WorldItemEditorTestFactory.Create(
                    gridProvider,
                    itemCatalog,
                    haulingSettings,
                    idRegistry,
                    new NoDropZoneQuery(),
                    new NoSpawnerProvider(),
                    pathBroker,
                    worldRegistry,
                    gameClock,
                    repository,
                    reservations,
                    spawner,
                    query,
                    haulPlanning,
                     itemMarkerPresenter: EditorNullItemMarkerPresenter.Instance,
                     itemTransferService: itemTransferService,
                     performanceRecorder: new EditorCharacterAiPerformanceRecorder(),
                     reservationPersistence: quantityReservations);
                equipmentItemGateway.Attach(items);
                items.Start();
                IFacilityBufferDestinationReleaseService destinationRelease =
                    new FacilityBufferDestinationReleaseService(
                        items,
                        itemTransferService,
                        worldRegistry);

                scenario = new ScenarioRuntime(
                    grid,
                    gridProvider,
                    items,
                    equipment,
                    quantityReservations,
                    warehouse,
                    actor,
                    carry,
                    worldRegistry,
                    previousWorldGrid,
                    massQuery,
                    destinationClaims,
                    stock,
                    itemTransferService,
                    haulPlanning,
                    destinationRelease);
                scenario.ownedObjects.Add(actorObject);
                scenario.ownedObjects.Add(warehouseObject);
                scenario.ownedObjects.Add(warehouseData);
                return scenario;
            }
            catch
            {
                if (scenario != null)
                {
                    scenario.Dispose();
                }
                else
                {
                    items?.Dispose();
                    CharacterActor actor = actorObject != null
                        ? actorObject.GetComponent<CharacterActor>()
                        : null;
                    TestWarehouseBuilding warehouse = warehouseObject != null
                        ? warehouseObject.GetComponent<TestWarehouseBuilding>()
                        : null;
                    if (worldRegistry != null)
                    {
                        if (actor != null)
                            worldRegistry.UnregisterCharacter(actor);
                        if (warehouse != null)
                            worldRegistry.UnregisterBuilding(warehouse);
                        if (worldGridRebound)
                            worldRegistry.SetGrid(previousWorldGrid);
                    }
                    DestroyImmediateSafe(actorObject);
                    DestroyImmediateSafe(warehouseObject);
                    DestroyImmediateSafe(warehouseData);
                }
                throw;
            }
        }

        public void Dispose()
        {
            Items?.Dispose();
            WorldRegistry?.UnregisterCharacter(Actor);
            WorldRegistry?.UnregisterBuilding(Warehouse);
            WorldRegistry?.SetGrid(previousWorldGrid);
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
        try
        {
            actorObject.SetActive(false);
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterLifecycle lifecycle = actorObject.GetComponent<CharacterLifecycle>();
            actorObject.AddComponent<CharacterCarryInventory>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AbilityHaul>();
            actorObject.GetComponent<CharacterIdentity>().SetPersistentId($"character:worker:{name}");
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorObject.GetComponent<AbilityMove>()
                .ConstructCharacterAbility(gridProvider);
            lifecycle.ConstructCharacterLifecycle(gridProvider);
            actorObject.transform.position = grid.GetWorldPos(position);
            actorObject.SetActive(true);
            actor.EnsureRuntimeState();
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            return actorObject;
        }
        catch
        {
            DestroyImmediateSafe(actorObject);
            throw;
        }
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

    private static long ExactItemMassGrams(
        ScenarioRuntime scenario,
        string itemId) =>
        scenario.Items.GetAllStacks()
            .Where(stack => string.Equals(
                stack.ItemId,
                itemId,
                StringComparison.Ordinal))
            .Sum(stack => ExactStackMassGrams(scenario, stack));

    private static long ExactStackMassGrams(
        ScenarioRuntime scenario,
        WorldItemStackSnapshot stack)
    {
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            scenario.MassQuery,
            (ItemDefinitionId)stack.ItemId,
            stack.ItemInstanceId,
            stack.Components);
        return scenario.MassQuery.GetQuantityMass(
            (ItemDefinitionId)stack.ItemId,
            subject,
            stack.Quantity).Value;
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
        private readonly WarehouseInventory inventory = new WarehouseInventory(
            200_000L, StockCategory.General, restrictCategory: false);

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
            float approvedWorkUnits,
            DurableFacilityEquipmentUseContext equipment = null) =>
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
