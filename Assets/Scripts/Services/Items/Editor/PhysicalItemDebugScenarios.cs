#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class PhysicalItemDebugScenarios
{
    private sealed class MutableGameClock : IGameClock
    {
        public float CurrentTime { get; set; }
        public float DeltaTime => 0f;
        public float Time => CurrentTime;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private const string ReportPath = "Temp/physical-item-contracts.tsv";
    private const string CarryCapacityReportPath =
        "Artifacts/QA/v27-physical-mass-carry-capacity.txt";
    private const string LumberItemId = "material:lumber";
    private const string PreservedRationItemId = "food:preserved-ration";
    private const string AppraisalFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF42_부품_감정대.asset";
    private const string RestorationFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF43_부품_복원_작업대.asset";
    private const string PrecisionFittingFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF44_정밀_장착대.asset";
    private const string LineageArchiveFacilityPath =
        "Assets/Resources/SO/Building/Industrial/I18_계보_기록실.asset";
    private const string HaulingHarnessWorkwearPath =
        "Assets/Resources/SO/Environment/Workwear/HaulingHarness.asset";

    [MenuItem("DungeonStory/Debug/Items/Run Physical Item Contracts")]
    public static void RunAll()
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("catalog_authored_stock_definition", VerifyCatalogAuthoredStockDefinition, lines, errors);
        Run("catalog_equipment_fallback", VerifyCatalogEquipmentFallback, lines, errors);
        Run("carry_target_band_authority", VerifyCarryTargetBandAuthority, lines, errors);
        Run("carry_weight_penalty", VerifyCarryWeightPenalty, lines, errors);
        Run("equipped_hauling_harness_mass_single_authority",
            VerifyEquippedHaulingHarnessMassSingleAuthority,
            lines,
            errors);
        Run("pile_sort_and_detail", VerifyPileSortAndDetail, lines, errors);
        Run("facility_delivery_buffer", VerifyFacilityDeliveryBuffer, lines, errors);
        Run("loose_material_delivery_request", VerifyLooseMaterialDeliveryRequest, lines, errors);
        Run("physical_craft_material_gate", VerifyPhysicalCraftMaterialGate, lines, errors);
        Run("customer_floor_theft", VerifyCustomerFloorTheft, lines, errors);
        Run("stack_delete_fallback", VerifyStackDeleteFallback, lines, errors);
        Run("warehouse_aggregate_view", VerifyWarehouseAggregateView, lines, errors);
        Run("warehouse_stored_physical_stack", VerifyWarehouseStoredPhysicalStack, lines, errors);
        Run("warehouse_stored_stack_consumption", VerifyWarehouseStoredStackConsumption, lines, errors);
        Run("transient_reservation_persistence", VerifyTransientReservationPersistence, lines, errors);
        Run("quantity_lease_ten_of_ten", VerifyQuantityLeaseTenOfTen, lines, errors);
        Run("quantity_lease_progress_heartbeat",
            VerifyQuantityLeaseProgressHeartbeat,
            lines,
            errors);
        Run("quantity_lease_survives_freshness_decay", VerifyQuantityLeaseSurvivesFreshnessDecay, lines, errors);
        Run("quantity_stack_removal_invalidates_lease", VerifyStackRemovalInvalidatesQuantityLease, lines, errors);
        Run("quantity_batch_atomic_rollback", VerifyQuantityBatchAtomicRollback, lines, errors);
        Run("quantity_exact_atomic_consume", VerifyQuantityExactAtomicConsume, lines, errors);
        Run("direct_consume_respects_foreign_lease", VerifyDirectConsumeRespectsForeignLease, lines, errors);
        Run("typed_physical_disposition",
            VerifyTypedPhysicalDisposition,
            lines,
            errors);
        Run("apparel_repair_pending_outbox_restore_exact",
            ApparelRepairOutboxDebugScenarios.VerifyRepairPendingOutboxAndRestore,
            lines,
            errors);
        Run("manual_water_exact_lot_pending_transfer",
            VerifyManualWaterExactLotPendingTransfer,
            lines,
            errors);
        Run("facility_buffer_respects_foreign_leases", VerifyFacilityBufferRespectsForeignLeases, lines, errors);
        Run("quantity_partial_extraction", VerifyQuantityPartialExtraction, lines, errors);
        Run("quantity_lease_transport_aggregation", VerifyQuantityLeaseTransportAggregation, lines, errors);
        Run("quantity_hundred_transport_aggregation", VerifyQuantityHundredTransportAggregation, lines, errors);
        Run("buffer_child_stack_aggregation", VerifyBufferChildStackAggregation, lines, errors);
        Run("reservation_grandfather_restore", VerifyReservationGrandfatherRestore, lines, errors);
        Run("reservation_carried_grandfather_restore", VerifyCarriedReservationGrandfatherRestore, lines, errors);
        Run("reservation_expired_committed_carry_restore",
            VerifyExpiredCommittedCarryRestore,
            lines,
            errors);
        Run("reservation_capture_restore_gate", VerifyReservationCaptureRestoreGate, lines, errors);
        Run("cancelled_destination_releases_materials", VerifyCancelledDestinationReleasesMaterials, lines, errors);
        Run("typed_persistent_item_ids", VerifyTypedPersistentItemIds, lines, errors);
        Run("equipment_instance_physical_authority",
            VerifyEquipmentInstancePhysicalAuthority,
            lines,
            errors);
        Run("equipment_unique_retail_transfer_commit_and_rollback",
            VerifyUniqueRetailTransferCommitAndRollback,
            lines,
            errors);
        Run("equipment_existing_instance_atomic_drop_capture_24",
            VerifyExistingEquipmentAtomicDropCapture,
            lines,
            errors);
        Run("quality_rejected_unique_delivery_identity",
            VerifyQualityRejectedUniqueDeliveryIdentity,
            lines,
            errors);
        Run("equipment_module_physical_authority",
            VerifyEquipmentModulePhysicalAuthority,
            lines,
            errors);
        Run("equipment_module_appraisal_ack_restore",
            VerifyEquipmentModuleAppraisalAcknowledgementRecovery,
            lines,
            errors);
        Run("regional_supply_transfer_outbox",
            RegionalSupplyContractTransferOutboxDebugScenarios.Verify,
            lines,
            errors);
        Run("resource_stock_policy_sale_outbox",
            ResourceStockPolicySaleOutboxDebugScenarios.Verify,
            lines,
            errors);
        Run("equipment_lineage_transfer_physical_authority",
            VerifyEquipmentLineageTransferPhysicalAuthority,
            lines,
            errors);
        Run("equipment_identity_across_carry_and_storage",
            VerifyEquipmentIdentityAcrossCarryAndStorage,
            lines,
            errors);
        Run("save_v19_contract", VerifySaveV19Contract, lines, errors);

        File.WriteAllLines(ReportPath, lines);
        if (errors.Count == 0)
        {
            Debug.Log($"Physical item contracts PASS. Report: {ReportPath}");
        }
        else
        {
            Debug.LogError(
                $"Physical item contracts FAIL ({errors.Count}): {string.Join(" | ", errors)}. "
                + $"Report: {ReportPath}");
            throw new InvalidOperationException(
                $"Physical item contracts failed ({errors.Count}). See {ReportPath}.");
        }
    }

    [MenuItem("DungeonStory/Debug/Items/Run Existing Equipment Atomic Drop Capture")]
    public static void RunExistingEquipmentAtomicDropCapture()
    {
        const string focusedReportPath =
            "Temp/equipment-existing-instance-atomic-drop.tsv";
        Directory.CreateDirectory("Temp");
        string details = VerifyExistingEquipmentAtomicDropCapture();
        File.WriteAllLines(focusedReportPath, new[]
        {
            "case\tresult\tdetails",
            $"equipment_existing_instance_atomic_drop_capture_24\tPASS\t{details}"
        });
        Debug.Log(
            "Existing equipment atomic drop capture PASS. Report: "
            + focusedReportPath);
    }

    private static string VerifyCatalogAuthoredStockDefinition()
    {
        ResourceDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        DungeonItemDefinition food = catalog.GetDefinition(PreservedRationItemId);
        Require(food.StockCategory == StockCategory.Food,
            $"authored item category was {food.StockCategory}");
        Require(food.UnitWeight > 0f && food.MaxStack > 0,
            "authored stock item had invalid physical data");
        return $"itemId={food.ItemId}; weight={food.UnitWeight:0.##}; maxStack={food.MaxStack}";
    }

    private static string VerifyCatalogEquipmentFallback()
    {
        string itemId = PhysicalItemIds.ForEquipment("weapon:dagger");
        Require(PhysicalItemIds.TryGetEquipmentDefinitionId(itemId, out string equipmentId),
            "equipment item id did not parse");
        Require(equipmentId == "weapon:dagger", $"parsed equipment id was {equipmentId}");
        DungeonItemDefinition definition =
            EditorItemCatalogFactory.Create().GetDefinition(itemId);
        Require(definition.ItemId == itemId, "equipment definition used the wrong item id");
        Require(definition.MaxStack == 1 && definition.UnitWeight > 0f, "equipment fallback physical data invalid");
        return $"itemId={itemId}; equipmentId={equipmentId}; maxStack={definition.MaxStack}";
    }

    private static string VerifyCarryWeightPenalty()
    {
        GameObject carrier = new GameObject("PhysicalItemCarryTest");
        try
        {
            CharacterActor actor = InitializeFixtureActor(carrier);
            CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor)
                ?? carrier.AddComponent<CharacterCarryInventory>();
            TestCatalogProvider catalog = new TestCatalogProvider();
            TestHaulingSettings settings = new TestHaulingSettings(1.5f);
            inventory.Configure(
                catalog,
                new PhysicalItemMassQuery(catalog),
                settings,
                new CharacterCarryInventoryRegistry());
            bool added = inventory.TryAdd("test:heavy", "item:heavy", 12, catalog, settings, out string failure);
            Require(added, $"expected full carry add, failure={failure}");

            float baseLimit = inventory.GetBaseCarryLimit();
            float current = inventory.GetCurrentWeight(catalog);
            float max = inventory.GetMaxAllowedWeight(settings);
            float speed = inventory.GetMoveSpeedMultiplier(catalog, settings);
            Require(current > baseLimit, $"expected over base limit: current={current} base={baseLimit}");
            Require(current <= max, $"expected below max allowed: current={current} max={max}");
            Require(speed < 1f && speed >= 0.45f, $"unexpected speed penalty {speed}");

            CharacterCarryInventorySaveData snapshot = inventory.Capture();
            inventory.RemoveAllItems();
            inventory.Restore(snapshot);
            Require(inventory.GetCurrentWeight(catalog) == current, "carry inventory did not round-trip");
            return $"weight={current:0.##}/{baseLimit:0.##}/{max:0.##}; speed={speed:0.##}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(carrier);
        }
    }

    [MenuItem("DungeonStory/Debug/Items/Run Warehouse Stored Consumption Focused")]
    public static void RunWarehouseStoredConsumptionFocused()
    {
        Debug.Log("Warehouse stored consumption PASS. "
            + VerifyWarehouseStoredStackConsumption());
    }

    public static string RunManualWaterExactLotFocused() =>
        VerifyManualWaterExactLotPendingTransfer();

    [MenuItem("DungeonStory/Debug/Items/Run V27 Carry Capacity Target")]
    public static void RunCarryCapacityTargetFromMenu()
    {
        Directory.CreateDirectory("Artifacts/QA");
        List<string> lines = new()
        {
            "case\tresult\tdetails"
        };
        List<string> errors = new();
        Run("carry_target_band_authority", VerifyCarryTargetBandAuthority, lines, errors);
        Run("carry_weight_penalty", VerifyCarryWeightPenalty, lines, errors);
        Run("equipped_hauling_harness_mass_single_authority",
            VerifyEquippedHaulingHarnessMassSingleAuthority,
            lines,
            errors);
        lines.Insert(
            0,
            errors.Count == 0
                ? "RESULT=PASS; cases=3; failed=0"
                : $"RESULT=FAIL; cases=3; failed={errors.Count}");
        File.WriteAllLines(CarryCapacityReportPath, lines);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"V27 carry capacity target failed ({errors.Count}): "
                + string.Join(" | ", errors));
        }

        Debug.Log(
            "V27 carry capacity target PASS. Report: "
            + CarryCapacityReportPath);
    }

    private static string VerifyCarryTargetBandAuthority()
    {
        const float representativePerformance = 0.76f;
        float ordinarySoft = CharacterCarryTuning.ResolveSoftCapacityKilograms(
            representativePerformance,
            haulingHarnessEquipped: false);
        float ordinaryHard = CharacterCarryTuning.ResolveHardCapacityKilograms(
            representativePerformance,
            haulingHarnessEquipped: false,
            CharacterCarryTuning.DefaultMaxCarryMultiplier);
        float harnessSoft = CharacterCarryTuning.ResolveSoftCapacityKilograms(
            representativePerformance,
            haulingHarnessEquipped: true);
        float harnessHard = CharacterCarryTuning.ResolveHardCapacityKilograms(
            representativePerformance,
            haulingHarnessEquipped: true,
            CharacterCarryTuning.DefaultMaxCarryMultiplier);

        Require(Mathf.Approximately(ordinarySoft, 19f),
            $"ordinary soft target was {ordinarySoft:0.###}kg");
        Require(Mathf.Approximately(ordinaryHard, 28.5f),
            $"ordinary hard target was {ordinaryHard:0.###}kg");
        Require(Mathf.Approximately(harnessSoft, 23.75f),
            $"harness soft target was {harnessSoft:0.###}kg");
        Require(Mathf.Approximately(harnessHard, 35.625f),
            $"harness hard target was {harnessHard:0.###}kg");

        float minimumStress = CharacterCarryTuning.ResolveHardCapacityKilograms(
            representativePerformance,
            haulingHarnessEquipped: false,
            CharacterCarryTuning.MinimumMaxCarryMultiplier);
        float defaultStress = CharacterCarryTuning.ResolveHardCapacityKilograms(
            representativePerformance,
            haulingHarnessEquipped: false,
            CharacterCarryTuning.DefaultMaxCarryMultiplier);
        float maximumStress = CharacterCarryTuning.ResolveHardCapacityKilograms(
            representativePerformance,
            haulingHarnessEquipped: false,
            CharacterCarryTuning.MaximumMaxCarryMultiplier);
        Require(Mathf.Approximately(minimumStress, 19f)
                && Mathf.Approximately(defaultStress, 28.5f)
                && Mathf.Approximately(maximumStress, 47.5f),
            $"stress band drifted: {minimumStress:0.###}/{defaultStress:0.###}/{maximumStress:0.###}kg");

        GameObject carrier = new GameObject("PhysicalItemCarryTargetBandTest");
        try
        {
            CharacterActor actor = InitializeFixtureActor(carrier);
            CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor)
                ?? carrier.AddComponent<CharacterCarryInventory>();
            float livePerformance = actor.Stats.EvaluatePerformance(
                "performance:survival:haul-capacity").Value;
            float expectedLiveSoft = CharacterCarryTuning.ResolveSoftCapacityKilograms(
                livePerformance,
                haulingHarnessEquipped: false);
            float liveSoft = inventory.GetBaseCarryLimit();
            Require(Mathf.Approximately(liveSoft, expectedLiveSoft),
                $"live inventory bypassed tuning authority: actual={liveSoft:0.###} expected={expectedLiveSoft:0.###}");

            TestHaulingSettings liveSettings = new TestHaulingSettings(
                CharacterCarryTuning.DefaultMaxCarryMultiplier);
            float liveHard = inventory.GetMaxAllowedWeight(liveSettings);
            Require(Mathf.Approximately(
                    liveHard,
                    expectedLiveSoft * CharacterCarryTuning.DefaultMaxCarryMultiplier),
                $"live hard limit drifted: actual={liveHard:0.###} soft={expectedLiveSoft:0.###}");

            return $"representative={ordinarySoft:0.###}/{ordinaryHard:0.###}kg; "
                + $"harness={harnessSoft:0.###}/{harnessHard:0.###}kg; "
                + $"stress={minimumStress:0.###}/{defaultStress:0.###}/{maximumStress:0.###}kg; "
                + $"live={liveSoft:0.###}/{liveHard:0.###}kg; performance={livePerformance:0.####}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(carrier);
        }
    }

    private static string VerifyEquippedHaulingHarnessMassSingleAuthority()
    {
        EnvironmentalWorkwearSO harness =
            AssetDatabase.LoadAssetAtPath<EnvironmentalWorkwearSO>(
                HaulingHarnessWorkwearPath);
        Require(harness != null
                && string.Equals(
                    harness.ItemDefinitionId,
                    DurableToolItemRules.HaulingHarness,
                    StringComparison.Ordinal),
            "Authored hauling harness workwear authority is missing.");

        GameObject carrier = new("PhysicalItemHarnessMassTest");
        try
        {
            CharacterActor actor = InitializeFixtureActor(carrier);
            CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor)
                ?? carrier.AddComponent<CharacterCarryInventory>();
            TestCatalogProvider catalog = new();
            IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(
                new IPhysicalItemMassProjector[]
                {
                    new GenericDefinitionPhysicalItemMassProjector(catalog),
                    new ApparelPhysicalItemMassProjector()
                });
            TestHaulingSettings settings = new(
                CharacterCarryTuning.DefaultMaxCarryMultiplier);
            inventory.Configure(
                catalog,
                massQuery,
                settings,
                new CharacterCarryInventoryRegistry());
            inventory.ConstructHaulingHarness(
                new FixedEnvironmentalWorkwearQuery(harness),
                NoEnvironmentalWorkwearCommand.Instance);
            inventory.ConstructEquippedApparelMass(
                new FixedEquippedApparelMassQuery(1150L));

            float performance = actor.Stats.EvaluatePerformance(
                "performance:survival:haul-capacity").Value;
            float expectedSoft = CharacterCarryTuning.ResolveSoftCapacityKilograms(
                performance,
                haulingHarnessEquipped: true);
            Require(Mathf.Approximately(inventory.GetBaseCarryLimit(), expectedSoft),
                "Equipped hauling harness did not project its capacity multiplier.");
            Require(Mathf.Approximately(inventory.GetCurrentWeight(catalog), 1.15f),
                "Equipped hauling harness was not counted as exact 1,150g burden.");
            Require(Mathf.Approximately(inventory.GetCurrentWeight(catalog), 1.15f),
                "Repeated burden query double-counted equipped hauling harness mass.");

            bool added = inventory.TryAdd(
                "test:harness-heavy",
                "item:heavy",
                5,
                catalog,
                settings,
                out string failure);
            Require(added, $"Harness burden fixture cargo failed: {failure}");
            float expectedTotal = 1.15f + 5f * 2.25f;
            Require(Mathf.Approximately(
                    inventory.GetCurrentWeight(catalog),
                    expectedTotal),
                "Cargo and equipped apparel did not share one exact burden total.");

            return "V27_HAULING_HARNESS_1150G_COUNTED_ONCE=PASS; "
                + "V27_EQUIPPED_APPAREL_AND_CARGO_BURDEN_EXACT=PASS; "
                + $"mass=1.15kg; total={expectedTotal:0.###}kg; "
                + $"soft={expectedSoft:0.###}kg";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(carrier);
        }
    }

    private static string VerifyPileSortAndDetail()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        try
        {
            runtime.Restore(CreatePileSnapshot());
            Require(runtime.TryGetPileAt(new Vector2Int(2, 1), out WorldItemPileSnapshot pile),
                "expected visible pile");
            Require(pile.Stacks.Count == 3, $"stored stack leaked into default pile view: {pile.Stacks.Count}");
            Require(pile.Representative != null && pile.Representative.ItemId == "item:rich",
                $"wrong representative {pile.Representative?.ItemId}");

            IReadOnlyList<WorldItemStackSnapshot> withStored =
                runtime.GetStacksAt(new Vector2Int(2, 1), includeStored: true);
            Require(withStored.Count == 4, $"expected stored stack in detail query, got {withStored.Count}");
            Require(withStored[0].State == WorldItemStackState.Loose, "loose stack should sort first");
            Require(withStored[0].ItemId == "item:rich", "highest value loose item should sort first");
            Require(runtime.TryGetPileTargetAt(new Vector2Int(2, 1), out ItemPileInfoTarget target, out UnityEngine.Object marker)
                && target != null
                && marker == null, "pile target should resolve without a scene marker in editor contract");
            return $"visible={pile.Stacks.Count}; storedDetail={withStored.Count}; representative={pile.Representative.DisplayName}";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyFacilityDeliveryBuffer()
    {
        GameObject warehouseObject = new GameObject("PhysicalItemDeliveryWarehouse");
        GameObject carrierObject = new GameObject("PhysicalItemDeliveryCarrier");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        FacilityBufferDestinationClaimRegistry destinationClaims = null;
        FacilityBufferDestinationClaim destinationClaim = null;
        try
        {
            Grid deliveryGrid = new Grid(8, 4);
            for (int y = 0; y < deliveryGrid.height; y++)
            {
                for (int x = 0; x < deliveryGrid.width; x++)
                {
                    deliveryGrid.SetAreaType(
                        new Vector2Int(x, y),
                        GridCellAreaType.ExteriorPath);
                }
            }

            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            runtime = CreateRuntime(
                out WorldItemRepository repository,
                out _,
                out _,
                out _,
                out destinationClaims,
                new TestGridProvider(deliveryGrid));
            warehouse.BindPhysicalStock(new PhysicalStockQuery(
                repository,
                runtime.CatalogProvider,
                new PhysicalItemMassQuery(runtime.CatalogProvider)));
            runtime.Start();
            Require(runtime.SpawnStockInWarehouse(
                    warehouse,
                    StockCategory.General,
                    8,
                    out int seeded)
                    && seeded == 8,
                "warehouse physical stock seed failed");

            string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + "delivery-test";
            destinationClaim = new FacilityBufferDestinationClaim(
                destinationId,
                new Vector2Int(4, 1),
                "qa.physical-item",
                "qa.physical-item:facility-delivery-buffer",
                ownerFacilityId: null,
                anchorKind: FacilityBufferDestinationAnchorKind.ReservedTarget);
            Require(destinationClaims.TryClaim(
                    destinationClaim,
                    out FacilityBufferDestinationClaimFailureCode claimFailure,
                    out string claimReason),
                $"facility delivery destination claim failed: {claimFailure}; {claimReason}");
            bool requested = runtime.TryRequestFacilityDelivery(
                StockCategory.General,
                3,
                new Vector2Int(4, 1),
                destinationId,
                out int requestedAmount,
                out string requestReason);
            Require(requested && requestedAmount == 3, $"delivery request failed: {requestReason}; amount={requestedAmount}");
            Require(warehouse.Inventory.GetStock(StockCategory.General) == 8,
                "warehouse stock changed before worker pickup");
            Require(!runtime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)),
                "warehouse material was dropped as a loose pile at request time");
            WorldItemStackSnapshot[] outboundStored = runtime.GetAllStacks()
                .Where(stack => stack.State == WorldItemStackState.Stored
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                .ToArray();
            Require(outboundStored.Length > 0
                    && outboundStored.Sum(stack => stack.Quantity) == 3
                    && outboundStored.All(stack =>
                        !string.IsNullOrWhiteSpace(
                            stack.SourceStorageDestinationId)
                        && stack.HasDestinationPosition
                        && stack.DestinationPosition == new Vector2Int(4, 1)),
                "stored delivery reservation did not preserve source and destination");

            CharacterActor actor = InitializeFixtureActor(carrierObject);
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? carrierObject.AddComponent<CharacterCarryInventory>();
            carry.Configure(
                runtime.CatalogProvider,
                runtime.MassQuery,
                runtime.HaulingSettingsProvider,
                new CharacterCarryInventoryRegistry());
            Require(carry.TryAdd(
                    "test:delivery",
                    LumberItemId,
                    3,
                    runtime.CatalogProvider,
                    runtime.HaulingSettingsProvider,
                    out string carryReason),
                $"could not seed carry inventory: {carryReason}");
            Require(runtime.TryDepositCarriedItemsToFacility(
                    actor,
                    carry,
                    new Vector2Int(4, 1),
                    destinationId,
                    out string depositReason),
                $"facility deposit failed: {depositReason}");
            Require(runtime.TryConsumeFacilityBuffer(
                    destinationId,
                    new Dictionary<StockCategory, int> { [StockCategory.General] = 3 },
                    out string consumeReason),
                $"facility buffer consume failed: {consumeReason}");
            Require(!runtime.GetAllStacks().Any(stack => stack.State == WorldItemStackState.FacilityBuffer
                    && stack.DestinationId == destinationId),
                "facility input buffer was not consumed");
            return $"requested={requestedAmount}; warehouseHeld=8; outboundStacks={outboundStored.Length}";
        }
        finally
        {
            if (destinationClaim != null && destinationClaims != null)
            {
                destinationClaims.TryRevoke(
                    destinationClaim,
                    out _,
                    out _);
            }
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(carrierObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifyPhysicalCraftMaterialGate()
    {
        GameObject warehouseObject = new GameObject("PhysicalCraftWarehouse");
        GameObject facilityObject = new GameObject("PhysicalCraftFacility");
        WorldItemStackRuntime itemRuntime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            itemRuntime = CreateRuntime(
                out WorldItemRepository repository,
                out CombatEquipmentRuntime equipmentRuntime);
            warehouse.BindPhysicalStock(
                new PhysicalStockQuery(
                    repository,
                    itemRuntime.CatalogProvider,
                    new PhysicalItemMassQuery(itemRuntime.CatalogProvider)));
            itemRuntime.Start();
            string materialStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                "material:iron-ingot",
                20,
                WorldItemStackState.Stored,
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix
                    + warehouse.PersistentInstanceId.Value);
            Require(!string.IsNullOrWhiteSpace(materialStackId),
                "physical craft material seed failed");
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());

            Require(equipmentRuntime.TryQueueCraft("weapon:dagger", facility, out string queueReason),
                $"physical craft queue failed: {queueReason}");
            Require(equipmentRuntime.CraftQueue.Count == 1, "craft order missing");
            CombatEquipmentCraftOrderSaveData order = equipmentRuntime.CraftQueue[0];
            Require(!order.materialsReady
                    && order.materialDestinationId.StartsWith(WorldItemStackRuntime.FacilityInputDestinationPrefix, StringComparison.Ordinal),
                "physical craft order did not wait for materials");
            Require(!equipmentRuntime.HasPendingCraftWork(new[] { "weapon:dagger" }),
                "craft work became available before materials arrived");

            foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks().ToArray())
            {
                itemRuntime.SpawnItemAt(
                    stack.ItemId,
                    stack.Quantity,
                    new Vector2Int(order.destinationX, order.destinationY),
                    WorldItemStackState.FacilityBuffer,
                    order.materialDestinationId,
                    out _);
                itemRuntime.DeleteStack(stack.StackId);
            }

            Require(equipmentRuntime.HasPendingCraftWork(new[] { "weapon:dagger" }),
                "craft work did not become available after materials arrived");
            int completed = equipmentRuntime.ApplyCraftWork(
                new[] { "weapon:dagger" },
                999f,
                out string completedEquipmentId);
            Require(completed == 1
                    && completedEquipmentId == "weapon:dagger"
                    && equipmentRuntime.CraftQueue.Count == 0,
                "physical craft order did not complete through the common work queue");
            return $"order={order.orderId}; completed={completedEquipmentId}";
        }
        finally
        {
            itemRuntime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifyCustomerFloorTheft()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        GameObject customerObject = new GameObject("PhysicalItemTheftCustomer");
        try
        {
            runtime.Restore(new DungeonPhysicalItemSaveData
            {
                version = DungeonPhysicalItemSaveData.CurrentVersion,
                haulingSettings = new ItemHaulingSettingsSnapshot { maxCarryMultiplier = 1.5f },
                stacks = new List<WorldItemStackSaveData>
                {
                    new WorldItemStackSaveData
                    {
                        stackId = "stack:stealable",
                        itemId = "item:rich",
                        quantity = 2,
                        state = WorldItemStackState.Loose,
                        gridX = 0,
                        gridY = 0
                    }
                }
            });
            runtime.Start();
            CharacterActor customer = InitializeFixtureActor(customerObject);
            customer.characterType = CharacterType.Customer;
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(customer)
                ?? customerObject.AddComponent<CharacterCarryInventory>();
            carry.Configure(
                runtime.CatalogProvider,
                runtime.MassQuery,
                runtime.HaulingSettingsProvider,
                new CharacterCarryInventoryRegistry());
            Require(runtime.TryStealLooseItem(customer, 0, out WorldItemStackSnapshot stolen, out string reason),
                $"floor theft failed: {reason}");
            carry = customer.GetComponent<CharacterCarryInventory>();
            Require(stolen != null && stolen.ItemId == "item:rich", "wrong stolen item");
            Require(carry.Items.Sum(item => item.quantity) == 1, "stolen item did not enter carry inventory");
            Require(runtime.GetAllStacks().Single(stack => stack.StackId == "stack:stealable").Quantity == 1,
                "world stack quantity was not reduced");
            return $"stolen={stolen.DisplayName}; carried={carry.Items.Sum(item => item.quantity)}";
        }
        finally
        {
            runtime.Dispose();
            UnityEngine.Object.DestroyImmediate(customerObject);
        }
    }

    private static string VerifyStackDeleteFallback()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        try
        {
            runtime.Restore(CreatePileSnapshot());
            Require(runtime.DeleteStack("stack:rich"), "failed to delete selected stack");
            Require(runtime.TryGetPileAt(new Vector2Int(2, 1), out WorldItemPileSnapshot pile),
                "pile disappeared after deleting one stack");
            Require(pile.Representative.ItemId == "item:cheap",
                $"panel should fall back to list representative, got {pile.Representative.ItemId}");
            return $"remaining={pile.Stacks.Count}; representative={pile.Representative.ItemId}";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyWarehouseAggregateView()
    {
        GameObject warehouseObject = new GameObject("PhysicalStockQueryWarehouse");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            runtime = CreateRuntime(out WorldItemRepository repository, out _);
            warehouse.BindPhysicalStock(new PhysicalStockQuery(
                repository,
                runtime.CatalogProvider,
                new PhysicalItemMassQuery(runtime.CatalogProvider)));
            Require(runtime.SpawnStockInWarehouse(
                    warehouse,
                    StockCategory.Food,
                    6,
                    out int spawned)
                    && spawned == 6,
                "physical warehouse stock seed failed");
            Require(warehouse.Inventory.TotalStock == 6,
                "warehouse query did not derive physical stock");

            WarehouseInventorySnapshot snapshot = warehouse.Inventory.CreateSnapshot();
            Require(snapshot.version == WarehouseInventorySnapshot.CurrentVersion,
                "warehouse policy snapshot was not captured");
            Require(typeof(WarehouseInventorySnapshot).GetField("maxCapacity") == null,
                "warehouse policy snapshot still duplicates immutable capacity");
            Require(typeof(WarehouseInventorySnapshot).GetField("stocks") == null,
                "warehouse policy snapshot still owns stock quantities");
            return $"derivedStock={warehouse.Inventory.TotalStock}; capacity={warehouse.Inventory.MaxCapacity}";
        }
        finally
        {
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifyWarehouseStoredPhysicalStack()
    {
        GameObject warehouseObject = new GameObject("PhysicalItemStoredMirrorWarehouse");
        GameObject carrierObject = new GameObject("PhysicalItemStoredMirrorCarrier");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);
            runtime = CreateRuntime(out WorldItemRepository repository, out _);
            warehouse.BindPhysicalStock(new PhysicalStockQuery(
                repository,
                runtime.CatalogProvider,
                new PhysicalItemMassQuery(runtime.CatalogProvider)));
            runtime.Start();

            CharacterActor actor = InitializeFixtureActor(carrierObject);
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? carrierObject.AddComponent<CharacterCarryInventory>();
            carry.Configure(
                runtime.CatalogProvider,
                runtime.MassQuery,
                runtime.HaulingSettingsProvider,
                new CharacterCarryInventoryRegistry());
            string foodItemId = PreservedRationItemId;
            Require(carry.TryAdd(
                    "mirror:food",
                    foodItemId,
                    5,
                    runtime.CatalogProvider,
                    runtime.HaulingSettingsProvider,
                    out string carryReason),
                $"could not seed carried stock: {carryReason}");
            Require(runtime.TryDepositCarriedItems(actor, carry, warehouse, out string depositReason),
                $"warehouse deposit failed: {depositReason}");
            Require(warehouse.Inventory.GetStock(StockCategory.Food) == 5,
                "warehouse aggregate did not receive carried stock");
            Require(!runtime.TryGetPileAt(Vector2Int.zero, out _),
                "stored warehouse stack leaked into default world marker view");

            IReadOnlyList<WorldItemStackSnapshot> storedHidden =
                runtime.GetStacksAt(Vector2Int.zero, includeStored: true);
            Require(storedHidden.Count == 1
                    && storedHidden[0].State == WorldItemStackState.Stored
                    && storedHidden[0].Quantity == 5,
                $"stored physical stack was not created correctly: {storedHidden.Count}");

            runtime.SetStoredItemMarkersVisible(true);
            Require(runtime.TryGetPileAt(Vector2Int.zero, out WorldItemPileSnapshot visiblePile)
                    && visiblePile.Stacks.Any(stack => stack.State == WorldItemStackState.Stored),
                "stored stack did not appear when item view was enabled");

            string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + "stored-mirror";
            Require(runtime.TryRequestFacilityDelivery(
                    StockCategory.Food,
                    3,
                    new Vector2Int(2, 0),
                    destinationId,
                    out int requested,
                    out string requestReason)
                    && requested == 3,
                $"delivery request failed: {requestReason}; requested={requested}");
            WorldItemStackSnapshot storedAfter = runtime
                .GetStacksAt(Vector2Int.zero, includeStored: true)
                .FirstOrDefault(stack => stack.State == WorldItemStackState.Stored
                    && string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId));
            WorldItemStackSnapshot outboundStored = runtime
                .GetStacksAt(Vector2Int.zero, includeStored: true)
                .SingleOrDefault(stack => stack.State == WorldItemStackState.Stored
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
            Require(warehouse.Inventory.GetStock(StockCategory.Food) == 5,
                "warehouse aggregate changed before physical pickup");
            Require(storedAfter != null && storedAfter.Quantity == 2,
                $"unassigned stored remainder was wrong: {storedAfter?.Quantity}");
            Require(outboundStored != null
                    && outboundStored.Quantity == 3
                    && !string.IsNullOrWhiteSpace(outboundStored.SourceStorageDestinationId),
                "outbound stored reservation was not created");
            Require(!runtime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)),
                "stored material became a visible loose pile");

            DungeonPhysicalItemSaveData captured = runtime.Capture();
            runtime.Restore(captured);
            Require(warehouse.Inventory.GetStock(StockCategory.Food) == 5,
                $"restore did not make warehouse aggregate follow stored stacks: {warehouse.Inventory.GetStock(StockCategory.Food)}");

            return $"stored=5; reserved=3; available=2; warehouse={warehouse.Inventory.GetStock(StockCategory.Food)}";
        }
        finally
        {
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(carrierObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifyWarehouseStoredStackConsumption()
    {
        GameObject warehouseObject =
            new GameObject("PhysicalItemStoredConsumptionWarehouse");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(
                warehouse);
            runtime = CreateRuntime(out WorldItemRepository repository, out _);
            warehouse.BindPhysicalStock(new PhysicalStockQuery(
                repository,
                runtime.CatalogProvider,
                new PhysicalItemMassQuery(runtime.CatalogProvider)));
            runtime.Start();

            Require(
                runtime.SpawnStockInWarehouse(
                    warehouse,
                    StockCategory.Water,
                    10,
                    out int spawned)
                && spawned == 10,
                $"warehouse water seed failed: spawned={spawned}");
            WorldItemStackSnapshot stored = runtime
                .GetAllStacks()
                .SingleOrDefault(stack =>
                    stack.State == WorldItemStackState.Stored
                    && runtime.CatalogProvider.TryGetDefinition(
                        stack.ItemId,
                        out DungeonItemDefinition definition)
                    && definition.StockCategory == StockCategory.Water);
            Require(
                stored != null && stored.Quantity == 10,
                "stored Water-category mirror was missing; stacks="
                + string.Join(",", runtime.GetAllStacks().Select(value =>
                    $"{value.StackId}:{value.ItemId}:{value.State}:{value.Quantity}:"
                    + value.DestinationId))
                + $"; aggregate={warehouse.Inventory.GetStock(StockCategory.Water)}");
            Require(
                warehouse.Inventory.GetStock(StockCategory.Water) == 10,
                "warehouse water aggregate was not seeded");

            Require(
                runtime.TryConsumeStackQuantity(
                    stored.StackId,
                    1,
                    out WorldItemStackSnapshot consumed)
                && consumed != null
                && consumed.Quantity == 1,
                "stored water consumption failed");
            WorldItemStackSnapshot remaining = runtime
                .GetAllStacks()
                .SingleOrDefault(stack =>
                    string.Equals(
                        stack.StackId,
                        stored.StackId,
                        StringComparison.Ordinal));
            Require(
                remaining != null && remaining.Quantity == 9,
                $"stored water mirror did not decrement: {remaining?.Quantity}");
            Require(
                warehouse.Inventory.GetStock(StockCategory.Water) == 9,
                "warehouse aggregate did not decrement with stored water");
            return "seeded=10; consumed=1; remaining=9";
        }
        finally
        {
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(
                warehouse);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static string VerifySaveV19Contract()
    {
        Require(DungeonGameSaveData.CurrentVersion == 24, $"save version is {DungeonGameSaveData.CurrentVersion}");
        DungeonGameSaveData save = new DungeonGameSaveData();
        DungeonPhysicalItemSaveData physicalItems = CreatePileSnapshot();
        DungeonCharacterWorldSaveData characters = new DungeonCharacterWorldSaveData();
        characters.actors.Add(new DungeonCharacterSaveData
        {
            persistentId = "carry-test",
            dataId = 1,
            displayName = "Carry Test",
            carryInventory = new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new CharacterCarriedItemSaveData
                    {
                        sourceStackId = "stack:carried",
                        itemId = "item:food",
                        quantity = 3
                    }
                }
            }
        });
        DungeonSaveSectionPayload.Write(
            save,
            PhysicalItemsSaveSection.Id,
            DungeonPhysicalItemSaveData.CurrentVersion,
            DungeonSaveRestorePhase.Items,
            physicalItems);
        DungeonSaveSectionPayload.Write(
            save,
            CharacterWorldSaveSection.Id,
            1,
            DungeonSaveRestorePhase.Characters,
            characters);
        DungeonSaveSectionPayload.Write(
            save,
            ExteriorActivitySaveSection.Id,
            DungeonExteriorActivitySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            new DungeonExteriorActivitySaveData());

        string json = JsonUtility.ToJson(save);
        DungeonGameSaveData restored = JsonUtility.FromJson<DungeonGameSaveData>(json);
        Require(
            DungeonSaveSectionPayload.TryRead(
                restored,
                PhysicalItemsSaveSection.Id,
                out DungeonPhysicalItemSaveData restoredItems),
            "physical item save section failed json round-trip");
        Require(
            DungeonSaveSectionPayload.TryRead(
                restored,
                CharacterWorldSaveSection.Id,
                out DungeonCharacterWorldSaveData restoredCharacters),
            "character save section failed json round-trip");
        Require(
            DungeonSaveSectionPayload.TryRead(
                restored,
                ExteriorActivitySaveSection.Id,
                out DungeonExteriorActivitySaveData restoredExterior),
            "exterior activity save section failed json round-trip");
        Require(restoredItems.stacks.Count == 4, $"expected 4 physical stacks, got {restoredItems.stacks.Count}");
        Require(restoredCharacters.actors?.Count == 1, "character save section failed json round-trip");
        Require(restoredCharacters.actors[0].carryInventory?.items?.Count == 1,
            "carried item save section failed json round-trip");
        Require(restoredCharacters.actors[0].carryInventory.items[0].quantity == 3,
            "carried item quantity changed during json round-trip");
        Require(
            restoredExterior.version == DungeonExteriorActivitySaveData.CurrentVersion,
            "exterior activity save version changed during json round-trip");
        return $"version={DungeonGameSaveData.CurrentVersion}; stacks={restoredItems.stacks.Count}; carried=3";
    }

    private static string VerifyLooseMaterialDeliveryRequest()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        runtime.Start();
        try
        {
            string itemId = LumberItemId;
            Require(runtime.SpawnItemAt(
                    itemId,
                    5,
                    new Vector2Int(2, 0),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == 5,
                $"spawned={spawned}");

            string destinationId = WorkOrderRuntime.ConstructionDestinationPrefix + "test";
            bool requested = runtime.TryRequestFacilityDelivery(
                StockCategory.General,
                3,
                new Vector2Int(6, 0),
                destinationId,
                out int requestedAmount,
                out string failure);
            Require(requested, $"request failed: {failure}");
            Require(requestedAmount == 3, $"requested={requestedAmount}");

            IReadOnlyList<WorldItemStackSnapshot> stacks = runtime.GetAllStacks();
            WorldItemStackSnapshot delivery = stacks.SingleOrDefault(stack =>
                stack.State == WorldItemStackState.Loose
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
            Require(delivery != null, "destination loose stack missing");
            Require(delivery.Quantity == 3, $"delivery quantity={delivery.Quantity}");
            Require(delivery.HasDestinationPosition && delivery.DestinationPosition == new Vector2Int(6, 0),
                $"destination position={delivery.DestinationPosition}");

            WorldItemStackSnapshot remainder = stacks.SingleOrDefault(stack =>
                stack.State == WorldItemStackState.Loose
                && string.IsNullOrWhiteSpace(stack.DestinationId));
            Require(remainder != null && remainder.Quantity == 2,
                remainder != null ? $"remainder={remainder.Quantity}" : "remainder missing");
            return $"requested={requestedAmount}; delivery={delivery.Quantity}; remainder={remainder.Quantity}";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static DungeonPhysicalItemSaveData CreatePileSnapshot()
    {
        return new DungeonPhysicalItemSaveData
        {
            version = DungeonPhysicalItemSaveData.CurrentVersion,
            haulingSettings = new ItemHaulingSettingsSnapshot { maxCarryMultiplier = 1.5f },
            stacks = new List<WorldItemStackSaveData>
            {
                new WorldItemStackSaveData
                {
                    stackId = "stack:buffer",
                    itemId = "item:buffer",
                    quantity = 5,
                    state = WorldItemStackState.FacilityBuffer,
                    destinationId = "shop:1",
                    gridX = 2,
                    gridY = 1
                },
                new WorldItemStackSaveData
                {
                    stackId = "stack:cheap",
                    itemId = "item:cheap",
                    quantity = 18,
                    state = WorldItemStackState.Loose,
                    gridX = 2,
                    gridY = 1
                },
                new WorldItemStackSaveData
                {
                    stackId = "stack:rich",
                    itemId = "item:rich",
                    quantity = 2,
                    state = WorldItemStackState.Loose,
                    gridX = 2,
                    gridY = 1
                },
                new WorldItemStackSaveData
                {
                    stackId = "stack:stored",
                    itemId = "item:stored",
                    quantity = 9,
                    state = WorldItemStackState.Stored,
                    destinationId = "warehouse:building:test-pile",
                    gridX = 2,
                    gridY = 1
                }
            }
        };
    }

    private static string VerifyTransientReservationPersistence()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out WorldItemRepository repository,
            out _);
        try
        {
            runtime.Restore(CreatePileSnapshot());
            ItemReservationService reservations = new ItemReservationService(
                repository,
                EditorNullItemMarkerPresenter.Instance,
                new ItemQuantityReservationService(
                    repository,
                    EditorNullItemMarkerPresenter.Instance,
                    new UnityGameClock()));
            Require(reservations.TryReserve(
                    new[] { "stack:buffer" },
                    CharacterId.Owner.Value),
                "production reservation service did not reserve the fixture stack");
            Require(runtime.GetAllStacks()
                    .Single(stack => stack.StackId == "stack:buffer")
                    .IsFullyReserved,
                "live stack did not contain the transient quantity reservation");

            DungeonPhysicalItemSaveData captured = runtime.Capture();
            WorldItemStackSaveData capturedBuffer = captured.stacks
                .Single(stack => stack.stackId == "stack:buffer");
            Require(capturedBuffer.reservedByPersistentId == string.Empty,
                "capture persisted a transient reservation");
            Require(capturedBuffer.destinationId == "shop:1",
                $"capture changed durable destination '{capturedBuffer.destinationId}'");

            string beforeInvalidRestore = JsonUtility.ToJson(captured);
            DungeonPhysicalItemSaveData invalid =
                JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                    beforeInvalidRestore);
            invalid.stacks[0].reservedByPersistentId = "worker:missing";
            bool rejected = false;
            try
            {
                runtime.Restore(invalid);
            }
            catch (InvalidOperationException ex)
            {
                rejected = ex.Message.Contains(
                    "transient reservation",
                    StringComparison.OrdinalIgnoreCase);
            }

            Require(rejected,
                "non-canonical saved reservation was not rejected");
            Require(JsonUtility.ToJson(runtime.Capture()) == beforeInvalidRestore,
                "invalid reservation preflight changed live physical items");
            return "live reservation omitted; invalid saved reservation rejected without mutation";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyQuantityLeaseProgressHeartbeat()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        MutableGameClock clock = new();
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            clock);
        string stackId = repository.AddEditorTestStack(
            "item:lease-heartbeat",
            1,
            WorldItemStackState.Loose);
        string signature = ItemReservationSignature.Create(
            "item:lease-heartbeat",
            Array.Empty<ItemInstanceComponentSaveData>());
        Require(reservations.TryReserve(
                "operation:lease-heartbeat",
                "character:lease-heartbeat",
                ItemReservationPurpose.Hauling,
                "haul:test",
                new ItemQuantityReservationRequest(
                    new ItemStackId(stackId),
                    1,
                    signature),
                out ItemQuantityLease lease,
                out DomainFailure reserveFailure),
            "heartbeat lease reserve failed: " + reserveFailure);
        Require(lease.expiresAtGameSeconds == 15d
                && lease.maximumExpiresAtGameSeconds == 45d,
            $"unexpected initial lease window {lease.expiresAtGameSeconds}/"
            + lease.maximumExpiresAtGameSeconds);

        clock.CurrentTime = 10f;
        Require(reservations.Renew(lease.leaseId, 55d, out DomainFailure firstRenew),
            "first heartbeat failed: " + firstRenew);
        clock.CurrentTime = 50f;
        Require(reservations.Renew(lease.leaseId, 95d, out DomainFailure secondRenew),
            "second heartbeat failed: " + secondRenew);
        clock.CurrentTime = 90f;
        Require(reservations.Revalidate(
                lease.leaseId,
                out ItemQuantityLease active,
                out DomainFailure activeFailure),
            "progressing lease expired: " + activeFailure);
        Require(active.expiresAtGameSeconds == 95d
                && active.maximumExpiresAtGameSeconds == 95d,
            $"heartbeat window did not slide {active.expiresAtGameSeconds}/"
            + active.maximumExpiresAtGameSeconds);

        clock.CurrentTime = 96f;
        Require(!reservations.Revalidate(
                lease.leaseId,
                out _,
                out DomainFailure expiredFailure)
                && expiredFailure.Code == FailureCode.ItemReservationLeaseExpired,
            "inactive lease did not expire after its bounded heartbeat window");
        return "progress heartbeats extended 15->55->95; idle lease expired at 96";
    }

    private static string VerifyCancelledDestinationReleasesMaterials()
    {
        WorldItemStackRuntime runtime = CreateRuntime();
        try
        {
            const string destinationId = "construction:test";
            DungeonPhysicalItemSaveData snapshot = new DungeonPhysicalItemSaveData
            {
                version = DungeonPhysicalItemSaveData.CurrentVersion,
                haulingSettings = new ItemHaulingSettingsSnapshot
                {
                    maxCarryMultiplier = 1.5f
                },
                stacks = new List<WorldItemStackSaveData>
                {
                    new WorldItemStackSaveData
                    {
                        stackId = "stack:reserved-source",
                        itemId = LumberItemId,
                        quantity = 3,
                        state = WorldItemStackState.Loose,
                        destinationId = destinationId,
                        gridX = 2,
                        gridY = 0
                    },
                    new WorldItemStackSaveData
                    {
                        stackId = "stack:delivered-buffer",
                        itemId = LumberItemId,
                        quantity = 2,
                        state = WorldItemStackState.FacilityBuffer,
                        destinationId = destinationId,
                        gridX = 7,
                        gridY = 0
                    }
                }
            };

            runtime.Restore(snapshot);
            Vector2Int releasePosition = new Vector2Int(7, 0);
            int released = runtime.ReleaseStacksByDestination(
                destinationId,
                releasePosition);
            WorldItemStackSnapshot[] stacks = runtime.GetAllStacks().ToArray();
            Require(released == 5, $"released={released}");
            Require(stacks.Sum(stack => stack.Quantity) == 5,
                $"quantity after release={stacks.Sum(stack => stack.Quantity)}");
            Require(stacks.All(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.IsNullOrWhiteSpace(stack.DestinationId)),
                "released materials were not loose and unassigned");
            Require(stacks.Any(stack =>
                    stack.Position == new Vector2Int(2, 0)
                    && stack.Quantity == 3),
                "source reservation did not return to its original cell");
            Require(stacks.Any(stack =>
                    stack.Position == releasePosition
                    && stack.Quantity == 2),
                "delivered buffer did not return at the construction cell");
            return "released=5; conserved=5; source=3; site=2";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyQuantityLeaseTenOfTen()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string stackId = repository.AddEditorTestStack(
            "item:buffer",
            10,
            WorldItemStackState.Loose);
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new UnityGameClock());
        string signature = ItemStackSignature.Create(
            "item:buffer",
            Array.Empty<ItemInstanceComponentSaveData>());
        for (int index = 0; index < 10; index++)
        {
            bool reserved = reservations.TryReserve(
                $"meal:test:{index}",
                $"character:{index}",
                ItemReservationPurpose.Meal,
                "meal:test-dining:item-buffer:decent",
                new ItemQuantityReservationRequest(
                    new ItemStackId(stackId),
                    1,
                    signature),
                out _,
                out DomainFailure failure);
            Require(reserved, $"lease {index} failed: {failure}");
        }
        Require(reservations.GetReservedQuantity(new ItemStackId(stackId)) == 10,
            "ten leases did not reserve exactly ten units");
        Require(reservations.GetAvailableQuantity(new ItemStackId(stackId)) == 0,
            "fully reserved stack still exposed available quantity");
        bool eleventh = reservations.TryReserve(
            "meal:test:10",
            "character:10",
            ItemReservationPurpose.Meal,
            "meal:test-dining:item-buffer:decent",
            new ItemQuantityReservationRequest(
                new ItemStackId(stackId),
                1,
                signature),
            out _,
            out DomainFailure eleventhFailure);
        Require(!eleventh, "eleventh lease unexpectedly succeeded");
        return $"reserved=10; available=0; eleventh={eleventhFailure.Code}";
    }

    private static string VerifyQuantityBatchAtomicRollback()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string first = repository.AddEditorTestStack(
            "item:buffer", 2, WorldItemStackState.Loose);
        string second = repository.AddEditorTestStack(
            "item:buffer", 1, WorldItemStackState.Loose);
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new UnityGameClock());
        string signature = ItemStackSignature.Create(
            "item:buffer",
            Array.Empty<ItemInstanceComponentSaveData>());
        bool result = reservations.TryReserveBatch(
            "production:test:atomic",
            "character:builder",
            ItemReservationPurpose.ProductionInput,
            "production:test:bill:slot:item-buffer",
            new[]
            {
                new ItemQuantityReservationRequest(
                    new ItemStackId(first), 1, signature),
                new ItemQuantityReservationRequest(
                    new ItemStackId(second), 2, signature)
            },
            out IReadOnlyList<ItemQuantityLease> leases,
            out DomainFailure failure);
        Require(!result, "invalid batch unexpectedly succeeded");
        Require(leases.Count == 0, "failed batch returned leases");
        Require(reservations.GetReservedQuantity(new ItemStackId(first)) == 0
            && reservations.GetReservedQuantity(new ItemStackId(second)) == 0,
            "failed batch left partial reservations");
        return $"failure={failure.Code}; first=0; second=0";
    }

    private static string VerifyQuantityLeaseSurvivesFreshnessDecay()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        List<ItemInstanceComponentSaveData> components = new()
        {
            CreateFreshnessComponent(120d),
            CreateQualityComponent("decent")
        };
        string stackId = repository.AddEditorTestStack(
            PreservedRationItemId,
            1,
            WorldItemStackState.FacilityBuffer,
            "facility:qa:meal-buffer",
            components: components);

        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new UnityGameClock());
        string reservationSignature = ItemReservationSignature.Create(
            PreservedRationItemId,
            components);
        Require(reservations.TryReserve(
                "meal:qa:freshness-decay",
                "character:qa:freshness-decay",
                ItemReservationPurpose.Meal,
                "meal:facility:qa:meal-buffer:preserved-ration",
                new ItemQuantityReservationRequest(
                    new ItemStackId(stackId),
                    1,
                    reservationSignature),
                out ItemQuantityLease lease,
                out DomainFailure reserveFailure),
            $"freshness lease failed: {reserveFailure}");

        // Normal aging changes the physical stack signature but must not make the
        // reservation owner lose its own quantity while walking or eating.
        Require(repository.SetEditorTestComponent(
                stackId,
                CreateFreshnessComponent(115d)),
            "failed to age the reserved editor-test stack");
        Require(reservations.Revalidate(
                lease.leaseId,
                out _,
                out DomainFailure freshnessFailure),
            $"normal freshness decay invalidated lease: {freshnessFailure}");

        WorldItemPersistenceService persistence = new(
            new TestCatalogProvider(),
            new TestHaulingSettings(1f),
            repository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance,
            reservations,
            reservations);
        DungeonPhysicalItemSaveData saved = persistence.Capture();
        ItemReservationClaimHintSaveData savedHint = saved.reservationIntents
            .Single()
            .reservationHints
            .Single();
        WorldItemStackSaveData savedStack = saved.stacks.Single();
        Require(string.Equals(
                savedHint.expectedStackSignature,
                ItemReservationSignature.Create(savedStack.itemId, savedStack.components),
                StringComparison.Ordinal),
            "freshness-aged lease did not persist its reservation identity");
        Require(!string.Equals(
                savedHint.expectedStackSignature,
                savedStack.GetStackSignature(),
                StringComparison.Ordinal),
            "test fixture did not prove reservation identity differs from mutable stack freshness");

        // A quality mutation is not time passage and must still invalidate the
        // lease rather than silently substituting a materially different item.
        Require(repository.SetEditorTestComponent(
                stackId,
                CreateQualityComponent("poor")),
            "failed to mutate the reserved editor-test stack quality");
        Require(!reservations.Revalidate(
                lease.leaseId,
                out _,
                out DomainFailure qualityFailure)
            && qualityFailure.Code == FailureCode.ItemReservationSliceInvalid,
            $"quality mutation did not invalidate lease: {qualityFailure}");

        return $"lease={lease.leaseId}; freshness=120->115; capture=valid; qualityFailure={qualityFailure.Code}";
    }

    private static ItemInstanceComponentSaveData CreateFreshnessComponent(double remainingSeconds)
    {
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.Freshness,
            schemaVersion = 1,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new()
                {
                    key = "remaining-seconds",
                    kind = ItemStateValueKind.Decimal,
                    decimalValue = remainingSeconds
                }
            }
        };
    }

    private static ItemInstanceComponentSaveData CreateQualityComponent(string quality)
    {
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.Quality,
            schemaVersion = 1,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new()
                {
                    key = "tier",
                    kind = ItemStateValueKind.String,
                    stringValue = quality
                }
            }
        };
    }

    private static string VerifyQuantityPartialExtraction()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out WorldItemRepository repository,
            out _,
            out ItemQuantityReservationService reservations,
            out IReservedItemTransferService transfer);
        try
        {
            string stackId = repository.AddEditorTestStack(
                "item:buffer", 10, WorldItemStackState.Loose);
            string signature = ItemStackSignature.Create(
                "item:buffer",
                Array.Empty<ItemInstanceComponentSaveData>());
            Require(reservations.TryReserve(
                    "haul:test:partial",
                    "character:carrier",
                    ItemReservationPurpose.Hauling,
                    "haul:test",
                    new ItemQuantityReservationRequest(
                        new ItemStackId(stackId), 1, signature),
                    out ItemQuantityLease lease,
                    out DomainFailure reserveFailure),
                $"partial reserve failed: {reserveFailure}");
            Require(transfer.TryExtractReservedQuantity(
                    lease.leaseId,
                    1,
                    new ItemTransitDestination(
                        WorldItemStackState.InTransit,
                        Vector2Int.zero,
                        "character:carrier"),
                    out ItemExtractionReceipt receipt,
                    out DomainFailure extractionFailure),
                $"partial extraction failed: {extractionFailure}");
            WorldItemStackSnapshot source = runtime.GetAllStacks()
                .SingleOrDefault(stack => stack != null
                    && string.Equals(stack.StackId, stackId, StringComparison.Ordinal));
            Require(source != null && source.Quantity == 9,
                "source quantity was not reduced from 10 to 9");
            Require(receipt.ExtractedQuantity == 1
                && receipt.ExtractedStackId != stackId,
                "partial extraction did not create one unique transport identity");
            Require(source.Quantity + receipt.ExtractedQuantity == 10,
                "partial extraction violated quantity conservation");
            return $"source={source.Quantity}; child={receipt.ExtractedQuantity}; childId={receipt.ExtractedStackId}";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyFacilityBufferRespectsForeignLeases()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out _,
            out _,
            out ItemQuantityReservationService reservations,
            out IReservedItemTransferService transfer);
        try
        {
            const string destination = "facility:lease-protected";
            Require(runtime.SpawnItemAt(
                    "item:buffer",
                    3,
                    Vector2Int.zero,
                    WorldItemStackState.FacilityBuffer,
                    destination,
                    out int spawned)
                && spawned == 3,
                "failed to seed the protected facility buffer");
            WorldItemStackSnapshot stack = runtime.GetAllStacks().Single();
            Require(reservations.TryReserve(
                    "meal:protected-owner",
                    "character:protected",
                    ItemReservationPurpose.Meal,
                    "meal:protected:item-buffer:decent",
                    new ItemQuantityReservationRequest(
                        new ItemStackId(stack.StackId),
                        2,
                        stack.StackSignature),
                    out ItemQuantityLease protectedLease,
                    out DomainFailure reserveFailure),
                $"protected reserve failed: {reserveFailure}");
            Require(!runtime.TryConsumeFacilityItemBuffer(
                    destination,
                    new Dictionary<string, int> { ["item:buffer"] = 2 },
                    out _),
                "facility consumer stole another operation's reserved quantity");
            WorldItemStackSnapshot afterRejected = runtime.GetAllStacks().Single();
            Require(afterRejected.Quantity == 3
                && afterRejected.ReservedQuantity == 2
                && afterRejected.AvailableQuantity == 1,
                "rejected facility consume changed physical or reserved quantity");
            Require(runtime.TryConsumeFacilityItemBuffer(
                    destination,
                    new Dictionary<string, int> { ["item:buffer"] = 1 },
                    out string consumeFailure),
                $"facility consumer could not use the one available unit: {consumeFailure}");
            WorldItemStackSnapshot afterAvailable = runtime.GetAllStacks().Single();
            Require(afterAvailable.Quantity == 2
                && afterAvailable.ReservedQuantity == 2
                && afterAvailable.AvailableQuantity == 0,
                "facility consumer did not consume exactly the available unit");
            Require(transfer.TryConsumeReservedQuantity(
                    protectedLease.leaseId,
                    2,
                    out DomainFailure protectedFailure),
                $"protected owner could not consume its remaining quantity: {protectedFailure}");
            Require(runtime.GetAllStacks().Count == 0,
                "protected facility buffer left residual stock after exact consumption");
            return "total=3; foreign-reserved=2; stolen=0; available-consumed=1; owner-consumed=2";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyQuantityExactAtomicConsume()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out WorldItemRepository repository,
            out _,
            out ItemQuantityReservationService quantityReservations,
            out _);
        try
        {
            string stackId = repository.AddEditorTestStack(
                "item:buffer", 10, WorldItemStackState.Loose);
            ItemReservationService compatibility = new(
                repository,
                EditorNullItemMarkerPresenter.Instance,
                quantityReservations);
            const string owner = "medical:test:exact-one";
            Require(compatibility.TryReserveQuantities(
                    new[] { new ReservedItemConsumption(stackId, 1) },
                    owner,
                    ItemReservationPurpose.Medical,
                    "medical:test:input"),
                "exact quantity compatibility reservation failed");
            Require(quantityReservations.GetReservedQuantity(
                    new ItemStackId(stackId)) == 1,
                "exact quantity compatibility path reserved the whole stack");
            AtomicItemConsumptionService atomic = new(
                repository,
                EditorNullItemMarkerPresenter.Instance,
                quantityReservations,
                quantityReservations);
            Require(atomic.TryConsumeReserved(
                    new[] { new ReservedItemConsumption(stackId, 1) },
                    owner,
                    out DomainFailure failure),
                $"exact atomic consumption failed: {failure}");
            WorldItemStackSnapshot remaining = runtime.GetAllStacks()
                .SingleOrDefault(stack => stack != null
                    && string.Equals(stack.StackId, stackId, StringComparison.Ordinal));
            Require(remaining != null
                && remaining.Quantity == 9
                && remaining.ReservedQuantity == 0,
                "exact atomic consumption did not leave 9 unreserved units");
            Require(compatibility.TryReserveQuantities(
                    new[] { new ReservedItemConsumption(stackId, 1) },
                    owner,
                    ItemReservationPurpose.Medical,
                    "medical:test:input"),
                "completed operation left a stale compatibility reservation key");
            Require(quantityReservations.GetReservedQuantity(
                    new ItemStackId(stackId)) == 1,
                "same operation ID could not reserve again after completion");
            compatibility.Release(stackId, owner);
            return "reserved=1; consumed=1; remaining=9; owner-reuse=pass";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyDirectConsumeRespectsForeignLease()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out WorldItemRepository repository,
            out _,
            out ItemQuantityReservationService reservations,
            out IReservedItemTransferService transfers);
        try
        {
            string stackId = repository.AddEditorTestStack(
                "item:buffer", 3, WorldItemStackState.Loose);
            string signature = ItemStackSignature.Create(
                "item:buffer",
                Array.Empty<ItemInstanceComponentSaveData>());
            Require(reservations.TryReserve(
                    "medical:foreign-direct-consume",
                    "character:foreign-direct-consume",
                    ItemReservationPurpose.Medical,
                    "medical:test:foreign",
                    new ItemQuantityReservationRequest(
                        new ItemStackId(stackId), 2, signature),
                    out ItemQuantityLease foreignLease,
                    out DomainFailure reserveFailure),
                $"foreign direct-consume lease failed: {reserveFailure}");
            Require(runtime.TryConsumeStackQuantity(stackId, 1, out _),
                "direct consumer could not consume the one available unit");
            WorldItemStackSnapshot protectedStack = runtime.GetAllStacks().Single();
            Require(protectedStack.Quantity == 2
                && protectedStack.ReservedQuantity == 2
                && protectedStack.AvailableQuantity == 0,
                "direct consumer changed the foreign reserved quantity");
            Require(!runtime.TryConsumeStackQuantity(stackId, 1, out _),
                "direct consumer stole a foreign reserved unit");
            Require(transfers.TryConsumeReservedQuantity(
                    foreignLease.leaseId,
                    2,
                    out DomainFailure consumeFailure),
                $"foreign owner could not consume its protected units: {consumeFailure}");
            Require(runtime.GetAllStacks().Count == 0,
                "foreign owner exact consumption left residual stock");
            return "total=3; foreign=2; direct=1; stolen=0; remaining=0";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyQuantityLeaseTransportAggregation()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out WorldItemRepository repository,
            out _,
            out ItemQuantityReservationService reservations,
            out IReservedItemTransferService transfer);
        try
        {
            const string destination = "facility:test-dining";
            const string cohort = "meal:facility-test:item-buffer:decent";
            string sourceId = repository.AddEditorTestStack(
                "item:buffer", 10, WorldItemStackState.Loose);
            BufferStackAggregationService aggregation = new(
                new TestCatalogProvider(),
                repository,
                EditorNullItemMarkerPresenter.Instance,
                reservations,
                reservations);
            Require(aggregation.TryDepositAndAggregate(
                    new CharacterCarriedItemSaveData
                    {
                        carriedStackId = "item-stack:seed-buffer",
                        sourceStackId = "item-stack:seed-source",
                        ownerOperationId = "meal:test:seed",
                        itemId = "item:buffer",
                        quantity = 5,
                        components = new List<ItemInstanceComponentSaveData>()
                    },
                    ItemReservationPurpose.Meal,
                    cohort,
                    destination,
                    new Vector2Int(4, 7),
                    out BufferAggregationReceipt seedReceipt,
                    out DomainFailure seedFailure),
                $"seed buffer aggregation failed: {seedFailure}");
            string bufferId = seedReceipt.CanonicalStackId;
            string signature = ItemStackSignature.Create(
                "item:buffer",
                Array.Empty<ItemInstanceComponentSaveData>());
            Require(reservations.TryReserve(
                    "meal:test:transport",
                    "character:carrier",
                    ItemReservationPurpose.Meal,
                    cohort,
                    new ItemQuantityReservationRequest(
                        new ItemStackId(sourceId), 1, signature),
                    out ItemQuantityLease lease,
                    out DomainFailure reserveFailure),
                $"transport reserve failed: {reserveFailure}");
            Require(transfer.TryExtractReservedQuantity(
                    lease.leaseId,
                    1,
                    new ItemTransitDestination(
                        WorldItemStackState.Carried,
                        Vector2Int.zero,
                        "character:carrier"),
                    out ItemExtractionReceipt extraction,
                    out DomainFailure extractionFailure),
                $"transport extraction failed: {extractionFailure}");
            Require(reservations.Revalidate(
                    lease.leaseId,
                    out ItemQuantityLease carriedLease,
                    out DomainFailure carriedFailure),
                $"lease was consumed at pickup: {carriedFailure}");
            Require(carriedLease.slices.Count == 1
                && carriedLease.slices[0].stackId == extraction.ExtractedStackId,
                "lease slice did not follow the carried child stack");
            Require(reservations.GetReservedQuantity(new ItemStackId(sourceId)) == 0
                && reservations.GetReservedQuantity(
                    new ItemStackId(extraction.ExtractedStackId)) == 1,
                "reservation quantity did not move from source to carried child");

            WorldItemStackSnapshot carried = runtime.GetAllStacks().Single(stack =>
                string.Equals(
                    stack.StackId,
                    extraction.ExtractedStackId,
                    StringComparison.Ordinal));
            CharacterCarriedItemSaveData carriedSave = new()
            {
                carriedStackId = carried.StackId,
                sourceStackId = sourceId,
                ownerOperationId = "meal:test:transport",
                itemId = carried.ItemId,
                quantity = carried.Quantity,
                contamination = carried.Contamination,
                components = carried.Components.Select(value => value.Clone()).ToList()
            };
            Require(aggregation.TryDepositAndAggregate(
                    carriedSave,
                    ItemReservationPurpose.Meal,
                    cohort,
                    destination,
                    new Vector2Int(4, 7),
                    out BufferAggregationReceipt aggregationReceipt,
                    out DomainFailure aggregationFailure),
                $"transport aggregation failed: {aggregationFailure}");
            Require(!runtime.GetAllStacks().Any(stack => string.Equals(
                    stack.StackId,
                    extraction.ExtractedStackId,
                    StringComparison.Ordinal)),
                "merged transport child remained as a dust stack");
            Require(reservations.Revalidate(
                    lease.leaseId,
                    out ItemQuantityLease bufferLease,
                    out DomainFailure bufferFailure),
                $"lease was lost during aggregation: {bufferFailure}");
            WorldItemStackSnapshot aggregated = runtime.GetAllStacks().Single(stack =>
                string.Equals(stack.StackId, bufferId, StringComparison.Ordinal));
            Require(bufferLease.slices.Count == 1
                && bufferLease.slices[0].stackId == bufferId
                && bufferLease.slices[0].originStackId == sourceId
                && aggregated.Quantity == 6
                && aggregated.ReservedQuantity == 1,
                "lease slice did not retarget to the aggregated buffer stack");
            Require(transfer.TryConsumeReservedQuantity(
                    lease.leaseId,
                    1,
                    out DomainFailure consumeFailure),
                $"buffer lease consumption failed: {consumeFailure}");
            WorldItemStackSnapshot consumed = runtime.GetAllStacks().Single(stack =>
                string.Equals(stack.StackId, bufferId, StringComparison.Ordinal));
            Require(consumed.Quantity == 5
                && consumed.ReservedQuantity == 0,
                "lease consumption did not remove exactly one buffer unit");
            return $"child={extraction.ExtractedStackId}; canonical={aggregationReceipt.CanonicalStackId}; source=9; buffer=5";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyBufferChildStackAggregation()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        TestCatalogProvider catalog = new();
        ItemQuantityReservationService quantityReservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new UnityGameClock());
        BufferStackAggregationService aggregation = new(
            catalog,
            repository,
            EditorNullItemMarkerPresenter.Instance,
            quantityReservations,
            quantityReservations);
        const string destination = "facility:test-dining";
        const string cohort = "meal:facility-test:item-buffer:decent";
        for (int index = 0; index < 100; index++)
        {
            CharacterCarriedItemSaveData carried = new()
            {
                carriedStackId = $"item-stack:transport:{index:D3}",
                sourceStackId = "item-stack:source",
                ownerOperationId = $"meal:test:{index}",
                itemId = "item:buffer",
                quantity = 1,
                components = new List<ItemInstanceComponentSaveData>()
            };
            Require(aggregation.TryDepositAndAggregate(
                    carried,
                    ItemReservationPurpose.Meal,
                    cohort,
                    destination,
                    new Vector2Int(4, 7),
                    out _,
                    out DomainFailure failure),
                $"buffer deposit {index} failed: {failure}");
        }
        Require(aggregation.PendingAggregationCount == 36,
            $"expected 36 deferred child stacks, got {aggregation.PendingAggregationCount}");
        Require(aggregation.ProcessPending(64, beginNewTick: true) == 36
            && aggregation.PendingAggregationCount == 0,
            "deferred child stacks did not drain on the next aggregation tick");
        DungeonItemDefinition definition = catalog.GetDefinition("item:buffer");
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(catalog);
        WorldItemQueryService query = new(
            catalog,
            massQuery,
            repository,
            EditorNullItemMarkerPresenter.Instance);
        WorldItemStackSnapshot[] stacks = query.GetAllStacks()
            .Where(record => record != null
                && record.State == WorldItemStackState.FacilityBuffer
                && string.Equals(record.DestinationId, destination, StringComparison.Ordinal)
                && string.Equals(record.AggregationCohortId, cohort, StringComparison.Ordinal))
            .OrderByDescending(record => record.Quantity)
            .ToArray();
        int expected = Mathf.CeilToInt(100f / definition.MaxStack);
        Require(stacks.Length == expected,
            $"expected {expected} physical stacks, got {stacks.Length}");
        Require(stacks.Sum(record => record.Quantity) == 100,
            "buffer aggregation changed total quantity");
        Require(stacks.All(record => record.Quantity <= definition.MaxStack),
            "buffer aggregation exceeded MaxStack");
        return $"quantity=100; maxStack={definition.MaxStack}; physical={stacks.Length}";
    }

    private static string VerifyQuantityHundredTransportAggregation()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out WorldItemRepository repository,
            out _,
            out ItemQuantityReservationService reservations,
            out IReservedItemTransferService transfer);
        try
        {
            const string destination = "facility:stress-dining";
            const string cohort = "meal:facility-stress:item-buffer:decent";
            string sourceId = repository.AddEditorTestStack(
                "item:buffer", 100, WorldItemStackState.Loose);
            string signature = ItemStackSignature.Create(
                "item:buffer",
                Array.Empty<ItemInstanceComponentSaveData>());
            List<ItemQuantityLease> leases = new();
            for (int index = 0; index < 100; index++)
            {
                Require(reservations.TryReserve(
                        $"meal:stress:{index:D3}",
                        $"character:stress:{index:D3}",
                        ItemReservationPurpose.Meal,
                        cohort,
                        new ItemQuantityReservationRequest(
                            new ItemStackId(sourceId), 1, signature),
                        out ItemQuantityLease lease,
                        out DomainFailure reserveFailure),
                    $"stress reserve {index} failed: {reserveFailure}");
                leases.Add(lease);
            }
            BufferStackAggregationService aggregation = new(
                new TestCatalogProvider(),
                repository,
                EditorNullItemMarkerPresenter.Instance,
                reservations,
                reservations);
            for (int index = 0; index < leases.Count; index++)
            {
                ItemQuantityLease lease = leases[index];
                Require(transfer.TryExtractReservedQuantity(
                        lease.leaseId,
                        1,
                        new ItemTransitDestination(
                            WorldItemStackState.Carried,
                            Vector2Int.zero,
                            $"character:stress:{index:D3}"),
                        out ItemExtractionReceipt extraction,
                        out DomainFailure extractionFailure),
                    $"stress extraction {index} failed: {extractionFailure}");
                WorldItemStackSnapshot carried = runtime.GetAllStacks().Single(stack =>
                    string.Equals(
                        stack.StackId,
                        extraction.ExtractedStackId,
                        StringComparison.Ordinal));
                CharacterCarriedItemSaveData carriedSave = new()
                {
                    carriedStackId = carried.StackId,
                    sourceStackId = sourceId,
                    ownerOperationId = $"meal:stress:{index:D3}",
                    itemId = carried.ItemId,
                    quantity = carried.Quantity,
                    contamination = carried.Contamination,
                    components = carried.Components
                        .Select(value => value.Clone())
                        .ToList()
                };
                Require(aggregation.TryDepositAndAggregate(
                        carriedSave,
                        ItemReservationPurpose.Meal,
                        cohort,
                        destination,
                        new Vector2Int(8, 8),
                        out _,
                        out DomainFailure aggregationFailure),
                    $"stress aggregation {index} failed: {aggregationFailure}");
            }

            Require(aggregation.PendingAggregationCount == 36,
                $"expected 36 overflow aggregations, got {aggregation.PendingAggregationCount}");
            int deferredProcessed = aggregation.ProcessPending(
                maxOperations: 64,
                beginNewTick: true);
            Require(deferredProcessed == 36
                && aggregation.PendingAggregationCount == 0,
                $"deferred aggregation drain mismatch: processed={deferredProcessed}; pending={aggregation.PendingAggregationCount}");

            DungeonItemDefinition definition =
                new TestCatalogProvider().GetDefinition("item:buffer");
            WorldItemStackSnapshot[] buffers = runtime.GetAllStacks()
                .Where(stack => stack.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(
                        stack.DestinationId,
                        destination,
                        StringComparison.Ordinal))
                .ToArray();
            int expectedPhysical = Mathf.CeilToInt(100f / definition.MaxStack);
            Require(buffers.Length == expectedPhysical,
                $"100 active leases produced {buffers.Length} stacks instead of {expectedPhysical}");
            Require(buffers.Sum(stack => stack.Quantity) == 100
                && buffers.Sum(stack => stack.ReservedQuantity) == 100,
                "100 active leases changed total or reserved quantity");
            Require(!runtime.GetAllStacks().Any(stack =>
                    stack.State is WorldItemStackState.Carried
                        or WorldItemStackState.InTransit),
                "transport dust stacks remained after stress aggregation");
            for (int index = 0; index < leases.Count; index++)
            {
                Require(transfer.TryConsumeReservedQuantity(
                        leases[index].leaseId,
                        1,
                        out DomainFailure consumeFailure),
                    $"stress consume {index} failed: {consumeFailure}");
            }
            Require(runtime.GetAllStacks().Count == 0,
                "completed stress meal leases left physical or dust stacks");
            return $"leases=100; immediate=64; deferred={deferredProcessed}; maxStack={definition.MaxStack}; physical={expectedPhysical}; completed=100";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyStackRemovalInvalidatesQuantityLease()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string stackId = repository.AddEditorTestStack(
            "item:lease-removal", 2, WorldItemStackState.Loose);
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new UnityGameClock());
        Require(reservations.TryReserve(
                "haul:character:lease-removal",
                "character:lease-removal",
                ItemReservationPurpose.Hauling,
                "haul:test:lease-removal",
                new ItemQuantityReservationRequest(
                    new ItemStackId(stackId),
                    1,
                    ItemStackSignature.Create(
                        "item:lease-removal",
                        Array.Empty<ItemInstanceComponentSaveData>())),
                out ItemQuantityLease lease,
                out DomainFailure reserveFailure),
            $"stack-removal lease setup failed: {reserveFailure}");

        repository.RemoveEditorTestStack(stackId);

        Require(!reservations.Revalidate(
                lease.leaseId,
                out _,
                out DomainFailure invalidatedFailure)
            && invalidatedFailure.Code == FailureCode.ItemReservationLeaseMissing,
            $"removed stack left a usable lease: {invalidatedFailure}");
        Require(reservations.CaptureReservationIntents().Count == 0,
            "removed stack left a persisted reservation intent");
        Require(reservations.GetReservedQuantity(new ItemStackId(stackId)) == 0,
            "removed stack left cached reserved quantity");
        return $"stack={stackId}; lease={lease.leaseId}; intents=0; reserved=0";
    }

    private static string VerifyReservationGrandfatherRestore()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string stackId = repository.AddEditorTestStack(
            "item:buffer", 3, WorldItemStackState.FacilityBuffer);
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new UnityGameClock());
        string signature = ItemStackSignature.Create(
            "item:buffer",
            Array.Empty<ItemInstanceComponentSaveData>());
        for (int index = 0; index < 2; index++)
        {
            Require(reservations.TryReserve(
                    $"production:grandfather:{index}",
                    $"character:{index}",
                    ItemReservationPurpose.ProductionInput,
                    "production:test:bill:slot:item-buffer",
                    new ItemQuantityReservationRequest(
                        new ItemStackId(stackId), 1, signature),
                    out _,
                    out DomainFailure failure),
                $"grandfather source lease failed: {failure}");
        }
        IReadOnlyList<ItemReservationIntentSaveData> intents =
            reservations.CaptureReservationIntents();
        Require(intents.Count == 2, "reservation intents were not captured");
        reservations.ResetTransientLedger();
        Require(reservations.GetReservedQuantity(new ItemStackId(stackId)) == 0,
            "ledger reset left reservation quantity");
        Require(reservations.TryRestoreGrandfathered(intents, out DomainFailure restoreFailure),
            $"grandfather restore failed: {restoreFailure}");
        Require(reservations.GetReservedQuantity(new ItemStackId(stackId)) == 2,
            "grandfather restore changed reserved total");
        ItemReservationRestoreDiagnostics diagnostics =
            reservations.LastRestoreDiagnostics;
        Require(diagnostics.GrandfatherOperationCount == 2
            && diagnostics.RestoredLeaseCount == 2
            && diagnostics.ClaimedStackCount == 1
            && diagnostics.RestoredQuantity == 2,
            "grandfather restore diagnostics did not match restored ownership");
        Require(reservations.TryGetLeasesByOwner(
                "production:grandfather:0",
                out IReadOnlyList<ItemQuantityLease> restored)
            && restored.Count == 1
            && restored[0].slices[0].stackId == stackId,
            "grandfather restore changed owner or preferred physical stack");
        return $"intents={intents.Count}; restored=2; stack={stackId}";
    }

    private static string VerifyCarriedReservationGrandfatherRestore()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out WorldItemRepository repository,
            out _,
            out ItemQuantityReservationService reservations,
            out IReservedItemTransferService transfer);
        try
        {
            const string owner = "meal:save-carried";
            string sourceId = repository.AddEditorTestStack(
                "item:buffer", 3, WorldItemStackState.Loose);
            string signature = ItemStackSignature.Create(
                "item:buffer",
                Array.Empty<ItemInstanceComponentSaveData>());
            Require(reservations.TryReserve(
                    owner,
                    "character:save-carrier",
                    ItemReservationPurpose.Meal,
                    "meal:save-facility:item-buffer:decent",
                    new ItemQuantityReservationRequest(
                        new ItemStackId(sourceId), 1, signature),
                    out ItemQuantityLease lease,
                    out DomainFailure reserveFailure),
                $"carried save reserve failed: {reserveFailure}");
            Require(transfer.TryExtractReservedQuantity(
                    lease.leaseId,
                    1,
                    new ItemTransitDestination(
                        WorldItemStackState.Carried,
                        new Vector2Int(2, 3),
                        "character:save-carrier"),
                    out ItemExtractionReceipt extraction,
                    out DomainFailure extractionFailure),
                $"carried save extraction failed: {extractionFailure}");

            DungeonPhysicalItemSaveData snapshot = runtime.Capture();
            ItemReservationIntentSaveData intent = snapshot.reservationIntents
                .Single(value => string.Equals(
                    value.ownerOperationId,
                    owner,
                    StringComparison.Ordinal));
            Require(intent.hadActiveItemReservation
                && intent.reservationHints.Count == 1
                && intent.reservationHints[0].originStackId == sourceId
                && intent.reservationHints[0].preferredPhysicalStackId
                    == extraction.ExtractedStackId,
                "save hint did not point at the carried physical child");
            runtime.Restore(snapshot);

            Require(reservations.TryGetLeasesByOwner(
                    owner,
                    out IReadOnlyList<ItemQuantityLease> restored)
                && restored.Count == 1
                && restored[0].slices.Count == 1
                && restored[0].slices[0].stackId == extraction.ExtractedStackId
                && restored[0].slices[0].originStackId == sourceId,
                "load changed carried reservation ownership or physical target");
            WorldItemStackSnapshot carried = runtime.GetAllStacks().Single(stack =>
                string.Equals(
                    stack.StackId,
                    extraction.ExtractedStackId,
                    StringComparison.Ordinal));
            Require(carried.State == WorldItemStackState.Carried
                && carried.Quantity == 1
                && carried.ReservedQuantity == 1,
                "load did not restore the reserved carried physical stack");
            Require(transfer.TryConsumeReservedQuantity(
                    restored[0].leaseId,
                    1,
                    out DomainFailure consumeFailure),
                $"restored carried lease was not consumable: {consumeFailure}");
            Require(runtime.GetAllStacks().Single().Quantity == 2,
                "restored carried consumption changed the source remainder");
            return $"owner={owner}; carried={extraction.ExtractedStackId}; restored=1; source=2";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyReservationCaptureRestoreGate()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string stackId = repository.AddEditorTestStack(
            "item:buffer", 2, WorldItemStackState.Loose);
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new UnityGameClock());
        ItemQuantityReservationRequest request = new(
            new ItemStackId(stackId),
            1,
            ItemStackSignature.Create(
                "item:buffer",
                Array.Empty<ItemInstanceComponentSaveData>()));

        using (reservations.EnterCaptureBarrier())
        {
            Require(reservations.BlocksNewReservations
                && reservations.IsCaptureBarrierActive,
                "capture barrier did not expose its active state");
            Require(!reservations.TryReserve(
                    "meal:gate:capture",
                    "character:gate",
                    ItemReservationPurpose.Meal,
                    "meal:gate:item-buffer:decent",
                    request,
                    out _,
                    out DomainFailure captureFailure)
                && captureFailure.Code == FailureCode.ItemReservationRestoreConflict,
                "capture barrier allowed a new item claim");
        }
        Require(reservations.TryReserve(
                "meal:gate:existing",
                "character:gate-a",
                ItemReservationPurpose.Meal,
                "meal:gate:item-buffer:decent",
                request,
                out _,
                out DomainFailure firstFailure),
            $"post-capture reservation failed: {firstFailure}");
        using (reservations.EnterRestoreBarrier())
        {
            Require(reservations.BlocksNewReservations
                && reservations.IsRestoreBarrierActive,
                "restore barrier did not expose its active state");
            Require(!reservations.TryReserve(
                    "medical:gate:steal",
                    "character:gate-b",
                    ItemReservationPurpose.Medical,
                    "medical:gate:item-buffer",
                    request,
                    out _,
                    out DomainFailure restoreFailure)
                && restoreFailure.Code == FailureCode.ItemReservationRestoreConflict,
                "restore barrier allowed a priority claim to steal quantity");
            Require(reservations.GetReservedQuantity(new ItemStackId(stackId)) == 1,
                "blocked restore claim changed the grandfather quantity");
        }
        Require(reservations.TryReserve(
                "medical:gate:after",
                "character:gate-b",
                ItemReservationPurpose.Medical,
                "medical:gate:item-buffer",
                request,
                out _,
                out DomainFailure afterFailure),
            $"post-restore reservation failed: {afterFailure}");
        Require(reservations.GetReservedQuantity(new ItemStackId(stackId)) == 2,
            "post-restore reservation did not resume normally");
        return "capture-blocked=1; restore-priority-steal=0; grandfather=1; after=2";
    }

    private static WorldItemStackRuntime CreateRuntime()
    {
        return CreateRuntime(out _, out _);
    }

    internal static WorldItemStackRuntime CreateRuntimeForCrossDomainFixture()
    {
        return CreateRuntime(out _, out _);
    }

    internal static WorldItemStackRuntime CreateRuntimeForCrossDomainFixture(
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime)
    {
        return CreateRuntime(out repository, out equipmentRuntime);
    }

    internal static WorldItemStackRuntime CreateRuntimeForCrossDomainFixture(
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime,
        out ItemQuantityReservationService quantityReservations,
        out IReservedItemTransferService reservedTransfer)
    {
        return CreateRuntime(
            out repository,
            out equipmentRuntime,
            out quantityReservations,
            out reservedTransfer);
    }

    private static string VerifyExpiredCommittedCarryRestore()
    {
        MutableGameClock clock = new MutableGameClock();
        WorldItemStackRuntime runtime = CreateRuntime(
            out WorldItemRepository repository,
            out _,
            out ItemQuantityReservationService reservations,
            out IReservedItemTransferService transfer,
            out _,
            gameClock: clock);
        GameObject carrierObject = new GameObject(
            "Expired Committed Carry Restore Carrier");
        CharacterCarryInventory inventory =
            carrierObject.AddComponent<CharacterCarryInventory>();
        TestCatalogProvider inventoryCatalog = new TestCatalogProvider();
        TestHaulingSettings inventoryHauling =
            new TestHaulingSettings(1.5f);
        inventory.Configure(
            inventoryCatalog,
            new PhysicalItemMassQuery(inventoryCatalog),
            inventoryHauling,
            new CharacterCarryInventoryRegistry());
        try
        {
            const string characterId = "character:expired-carry";
            const string operationId =
                "haul:character:expired-carry:000000000001";
            const string destinationId = "facility-buffer:expired-carry";
            string sourceId = repository.AddEditorTestStack(
                "item:buffer",
                2,
                WorldItemStackState.Loose);
            string signature = ItemStackSignature.Create(
                "item:buffer",
                Array.Empty<ItemInstanceComponentSaveData>());
            Require(reservations.TryReserve(
                    operationId,
                    characterId,
                    ItemReservationPurpose.Hauling,
                    $"haul:{WorldItemHaulDestinationKind.FacilityBuffer}:{destinationId}",
                    new ItemQuantityReservationRequest(
                        new ItemStackId(sourceId),
                        1,
                        signature),
                    out ItemQuantityLease lease,
                    out DomainFailure reserveFailure),
                "expired committed carry reserve failed: " + reserveFailure);
            Require(runtime.TryRegisterHaulDeliveryPlanForEditorTest(
                    operationId,
                    characterId,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    destinationId,
                    new Vector2Int(2, 3),
                    new Vector2Int(2, 3),
                    out string planFailure),
                "expired committed carry intent failed: " + planFailure);
            Require(transfer.TryExtractReservedQuantity(
                    lease.leaseId,
                    1,
                    new ItemTransitDestination(
                        WorldItemStackState.Carried,
                        new Vector2Int(2, 3),
                        characterId),
                    out ItemExtractionReceipt extraction,
                    out DomainFailure extractionFailure),
                "expired committed carry extraction failed: "
                + extractionFailure);
            inventory.Restore(new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new CharacterCarriedItemSaveData
                    {
                        carriedStackId = extraction.ExtractedStackId,
                        sourceStackId = extraction.SourceStackId,
                        ownerOperationId = operationId,
                        itemId = "item:buffer",
                        quantity = 1,
                        wasteOrigin = WasteOriginKind.Unknown,
                        components = new List<ItemInstanceComponentSaveData>()
                    }
                }
            });
            Require(inventory.Items.Count == 1
                    && inventory.Items[0].quantity == 1,
                "expired committed carry inventory restore failed");
            Require(runtime.TryCommitHaulPickup(
                    operationId,
                    inventory,
                    out string commitFailure),
                "expired committed carry commit failed: " + commitFailure);

            clock.CurrentTime = 100f;
            Require(reservations.CaptureReservationIntents().Count == 0,
                "expired scheduling lease unexpectedly remained active");
            DungeonPhysicalItemSaveData snapshot = runtime.Capture();
            ItemReservationIntentSaveData durable = snapshot.reservationIntents
                .Single(intent => string.Equals(
                    intent.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal));
            Require(durable.reservationHints.Count == 1
                    && durable.reservationHints[0].purpose
                        == ItemReservationPurpose.Hauling
                    && durable.reservationHints[0].preferredPhysicalStackId
                        == extraction.ExtractedStackId
                    && durable.reservationHints[0].quantity == 1,
                "committed physical carry did not rebuild its durable lease projection");

            runtime.Restore(snapshot);
            Require(reservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> restored)
                && restored.Count == 1
                && restored[0].slices.Count == 1
                && restored[0].slices[0].stackId
                    == extraction.ExtractedStackId,
                "expired committed carry did not restore exact destination ownership");
            return $"operation={operationId}; carried={extraction.ExtractedStackId}; durable=1; restored=1";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(carrierObject);
            runtime.Dispose();
        }
    }

    internal static WorldItemStackRuntime CreateRuntimeForCrossDomainFixture(
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime,
        out ItemQuantityReservationService quantityReservations,
        out IReservedItemTransferService reservedTransfer,
        out IReservedPhysicalItemBatchDispositionService reservedBatch,
        out IPhysicalItemBatchDispositionService batch)
    {
        return CreateRuntimeForCrossDomainFixture(
            new TestCatalogProvider(),
            out repository,
            out equipmentRuntime,
            out quantityReservations,
            out reservedTransfer,
            out reservedBatch,
            out batch);
    }

    internal static WorldItemStackRuntime CreateRuntimeForCrossDomainFixture(
        IDungeonItemCatalogProvider itemCatalog,
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime,
        out ItemQuantityReservationService quantityReservations,
        out IReservedItemTransferService reservedTransfer,
        out IReservedPhysicalItemBatchDispositionService reservedBatch,
        out IPhysicalItemBatchDispositionService batch)
    {
        return CreateRuntimeForCrossDomainFixture(
            itemCatalog,
            aggregateRootStore: null,
            out repository,
            out equipmentRuntime,
            out quantityReservations,
            out reservedTransfer,
            out reservedBatch,
            out batch);
    }

    internal static WorldItemStackRuntime CreateRuntimeForCrossDomainFixture(
        IDungeonItemCatalogProvider itemCatalog,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime,
        out ItemQuantityReservationService quantityReservations,
        out IReservedItemTransferService reservedTransfer,
        out IReservedPhysicalItemBatchDispositionService reservedBatch,
        out IPhysicalItemBatchDispositionService batch)
    {
        if (itemCatalog == null)
        {
            throw new ArgumentNullException(nameof(itemCatalog));
        }
        if (aggregateRootStore == null)
        {
            aggregateRootStore = new DungeonRuntimeAggregateRootStore();
        }
        WorldItemStackRuntime runtime = CreateRuntime(
            out repository,
            out equipmentRuntime,
            out quantityReservations,
            out reservedTransfer,
            out _,
            itemCatalogOverride: itemCatalog,
            aggregateRootStoreOverride: aggregateRootStore);
        PhysicalItemBatchDispositionService service = new(
            repository,
            runtime.MassQuery,
            EditorNullItemMarkerPresenter.Instance,
            quantityReservations);
        reservedBatch = service;
        batch = service;
        return runtime;
    }

    private static WorldItemStackRuntime CreateRuntime(
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime)
    {
        return CreateRuntime(
            out repository,
            out equipmentRuntime,
            out _,
            out _);
    }

    private static WorldItemStackRuntime CreateRuntime(
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime,
        out ItemQuantityReservationService quantityReservations,
        out IReservedItemTransferService reservedTransfer)
    {
        return CreateRuntime(
            out repository,
            out equipmentRuntime,
            out quantityReservations,
            out reservedTransfer,
            out _);
    }

    private static WorldItemStackRuntime CreateRuntime(
        out WorldItemRepository repository,
        out CombatEquipmentRuntime equipmentRuntime,
        out ItemQuantityReservationService quantityReservations,
        out IReservedItemTransferService reservedTransfer,
        out FacilityBufferDestinationClaimRegistry destinationClaims,
        IGridSystemProvider gridProvider = null,
        IGameClock gameClock = null,
        EditorEquipmentPhysicalItemGatewayProxy equipmentGatewayOverride = null,
        IDungeonItemCatalogProvider itemCatalogOverride = null,
        DungeonRuntimeAggregateRootStore aggregateRootStoreOverride = null)
    {
        IGameContentCatalog gameContent = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        ICombatEquipmentCatalog combatCatalog =
            new ResourceCombatEquipmentCatalog(gameContent);
        gridProvider ??= new NoGridProvider();
        gameClock ??= new UnityGameClock();
        IDungeonItemCatalogProvider itemCatalog =
            itemCatalogOverride ?? new TestCatalogProvider();
        IItemHaulingSettingsProvider haulingSettings =
            new TestHaulingSettings(1.5f);
        ICharacterIdRegistry idRegistry = new TestIdRegistry();
        IGridPathSearchBroker pathBroker =
            new GridPathSearchBroker(gameClock, doorAccessQuery: null, performanceRecorder: null, costPolicy: null);
        ICharacterAiWorldRegistry worldRegistry =
            CharacterAiEditorTestDependencies.WorldRegistry;
        repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            aggregateRootStoreOverride
            ?? new DungeonRuntimeAggregateRootStore());
        destinationClaims = new FacilityBufferDestinationClaimRegistry();
        quantityReservations =
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
        IPhysicalItemMassQuery massQuery =
            new PhysicalItemMassQuery(itemCatalog);
        WorldItemQueryService query = new WorldItemQueryService(
            itemCatalog,
            massQuery,
            repository,
            EditorNullItemMarkerPresenter.Instance);
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
        EditorEquipmentPhysicalItemGatewayProxy equipmentItemGateway =
            equipmentGatewayOverride
            ?? new EditorEquipmentPhysicalItemGatewayProxy();
        equipmentRuntime = CombatEquipmentEditorTestFactory.Create(
            combatCatalog,
            repository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: new ResourceEquipmentModuleCatalog(gameContent),
            materialCatalog: new ResourceEconomyContentCatalog(gameContent),
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            itemStackRuntime: equipmentItemGateway);
        IRetailStockPhysicalRuntime retailStockPhysical =
            new RetailStockPhysicalRuntime(
                new CombatEquipmentRuntimeRetailAuthorityAdapter(equipmentRuntime));
        WorldItemReadServices readServices = new WorldItemReadServices(
            itemCatalog,
            massQuery,
            haulingSettings,
            query,
            EditorNullItemMarkerPresenter.Instance,
            new EditorCharacterAiPerformanceRecorder(),
            DisabledDungeonDebugRuleQuery.Instance);
        ItemTransferService itemTransferService = new ItemTransferService(
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
            bufferAggregation: bufferAggregation,
            warehouseMassAdmission: null,
            retailStockPhysical: retailStockPhysical);
        reservedTransfer = itemTransferService;
        WorldItemStackRuntime runtime = WorldItemEditorTestFactory.Create(
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
        equipmentItemGateway.Attach(runtime);
        return runtime;
    }

    private static string VerifyTypedPersistentItemIds()
    {
        WorldItemStackRuntime source = CreateRuntime(
            out _,
            out CombatEquipmentRuntime equipment);
        string itemId = PhysicalItemIds.ForEquipment("weapon:dagger");
        CombatEquipmentInstance equipmentInstance = equipment.CreateInstance(
            "weapon:dagger",
            CombatEquipmentQuality.Normal,
            CombatEquipmentWorldState.Loose);
        Require(source.SpawnExistingUniqueItemAt(
                itemId,
                (ItemInstanceId)equipmentInstance.instanceId,
                new Vector2Int(3, 4),
                WorldItemStackState.Loose,
                string.Empty,
                out string stackId)
                && equipment.TryLinkToWorldStack(
                    equipmentInstance.instanceId,
                    stackId,
                    CombatEquipmentWorldState.Loose),
            "failed to spawn the unique equipment item");

        WorldItemStackSnapshot created = source.GetAllStacks()
            .Single(stack => stack.StackId == stackId);
        Require(((ItemStackId)created.StackId).IsValid,
            "spawned stack did not receive a typed stack ID");
        Require(((ItemInstanceId)created.ItemInstanceId).IsValid,
            "unique equipment did not receive an item-instance ID");

        DungeonPhysicalItemSaveData save = source.Capture();
        WorldItemStackRuntime restoredRuntime = CreateRuntime(
            out _,
            out CombatEquipmentRuntime restoredEquipment);
        restoredRuntime.Restore(save);
        restoredEquipment.PublishRestoreCandidate(
            restoredEquipment.BuildRestoreCandidate(equipment.Capture()));
        WorldItemStackSnapshot restored = restoredRuntime.GetAllStacks()
            .Single(stack => stack.StackId == created.StackId);
        Require(restored.ItemInstanceId == created.ItemInstanceId,
            "item-instance ID changed during save round-trip");

        return $"stack={created.StackId}; instance={created.ItemInstanceId}";
    }

    private static string VerifyEquipmentInstancePhysicalAuthority()
    {
        WorldItemStackRuntime source = CreateRuntime(
            out WorldItemRepository sourceRepository,
            out CombatEquipmentRuntime sourceEquipment);
        CombatEquipmentInstance created = sourceEquipment.CreateInstance(
            "weapon:greatsword",
            CombatEquipmentQuality.Good,
            CombatEquipmentWorldState.Stored);
        Require(((ItemInstanceId)created.instanceId).IsValid,
            "equipment did not receive a typed physical item-instance ID");
        string physicalItemId = PhysicalItemIds.ForEquipment(created.definitionId);
        Require(source.SpawnExistingUniqueItemAt(
                physicalItemId,
                (ItemInstanceId)created.instanceId,
                new Vector2Int(2, 2),
                WorldItemStackState.Stored,
                "warehouse:building:physical-authority",
                out string sourceStackId)
            && sourceEquipment.TryLinkToWorldStack(
                created.instanceId,
                sourceStackId,
                CombatEquipmentWorldState.Stored),
            "equipment fixture did not create its authoritative physical stack");

        const string ModuleId = "item-instance:physical-authority-module";
        sourceRepository.EquipmentModules[ModuleId] = new EquipmentModuleInstance
        {
            instanceId = ModuleId,
            definitionId = "module:weapon:balanced-core",
            grade = 2,
            condition = 0.91f,
            identified = true,
            state = EquipmentModuleProcessState.Installed,
            attachedEquipmentInstanceId = created.instanceId
        };
        sourceRepository.EquipmentInstances[created.instanceId].moduleSlots =
            new List<EquipmentModuleSlotState>
            {
                new EquipmentModuleSlotState
                {
                    slotIndex = 0,
                    moduleInstanceId = ModuleId
                }
            };

        DungeonPhysicalItemSaveData physicalSave = source.Capture();
        DungeonCombatEquipmentSaveData combatSave = sourceEquipment.Capture();
        string combatJson = JsonUtility.ToJson(combatSave);
        Require(!combatJson.Contains("\"instances\"", StringComparison.Ordinal)
                && !combatJson.Contains("\"moduleInstances\"", StringComparison.Ordinal),
            "combat save still writes duplicate equipment or module instance authority");
        Require(physicalSave.version == DungeonPhysicalItemSaveData.CurrentVersion
                && physicalSave.uniqueItems.Count == 1,
            "physical save did not capture exactly one unique equipment item");

        WorldItemStackRuntime restoredItems = CreateRuntime(
            out _,
            out CombatEquipmentRuntime restoredEquipment);
        restoredItems.Restore(physicalSave);
        restoredEquipment.PublishRestoreCandidate(
            restoredEquipment.BuildRestoreCandidate(combatSave));
        Require(restoredEquipment.TryGetInstance(
                created.instanceId,
                out CombatEquipmentInstance restored)
                && restored.quality == CombatEquipmentQuality.Good
                && restored.moduleSlots.Any(slot => slot != null
                    && slot.moduleInstanceId == ModuleId),
            "equipment state did not round-trip through the physical item section");
        Require(restoredEquipment.ModuleInstances.Any(module => module != null
                && module.instanceId == ModuleId
                && Mathf.Approximately(module.condition, 0.91f)),
            "installed module did not round-trip through its owning equipment item");

        source.Dispose();
        restoredItems.Dispose();
        return $"itemInstance={created.instanceId}; physicalVersion={physicalSave.version}";
    }

    private static string VerifyExistingEquipmentAtomicDropCapture()
    {
        WorldItemStackRuntime runtime = CreateRuntime(
            out _,
            out CombatEquipmentRuntime equipment);
        try
        {
            const int repetitionCount = 24;
            string equipmentItemId = PhysicalItemIds.ForEquipment("weapon:dagger");
            Require(!runtime.SpawnItemAt(
                    equipmentItemId,
                    12,
                    Vector2Int.zero,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int genericEquipmentSpawned)
                    && genericEquipmentSpawned == 0,
                "generic SpawnItemAt accepted equipment without authoritative instances");
            Require(!runtime.SpawnItemAt(
                    PhysicalItemIds.ForEquipmentModule(),
                    12,
                    Vector2Int.zero,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int genericModuleSpawned)
                    && genericModuleSpawned == 0,
                "generic SpawnItemAt accepted equipment modules without authoritative instances");
            Require(runtime.SpawnItemAt(
                    LumberItemId,
                    2,
                    Vector2Int.zero,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int genericMaterialSpawned)
                    && genericMaterialSpawned == 2,
                "generic SpawnItemAt rejected a normal non-equipment item");

            HashSet<string> instanceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < repetitionCount; index++)
            {
                CombatEquipmentInstance created = equipment.CreateInstance(
                    "weapon:dagger",
                    CombatEquipmentQuality.Normal,
                    CombatEquipmentWorldState.Loose);
                Require(equipment.TryDropExistingEquipmentToWorld(
                        created.instanceId,
                        new Vector2Int(index, 2),
                        out string stackId,
                        out string failureReason),
                    $"atomic equipment drop {index} failed: {failureReason}");
                WorldItemStackSnapshot stack = runtime.GetAllStacks()
                    .Single(candidate => candidate.StackId == stackId);
                Require(stack.ItemId == PhysicalItemIds.ForEquipment(created.definitionId),
                    $"atomic equipment drop {index} used item '{stack.ItemId}'");
                Require(stack.ItemInstanceId == created.instanceId,
                    $"atomic equipment drop {index} changed instance identity");
                Require(instanceIds.Add(stack.ItemInstanceId),
                    $"atomic equipment drop {index} duplicated instance identity");
            }

            DungeonPhysicalItemSaveData captured = runtime.Capture();
            Require(captured.version == DungeonPhysicalItemSaveData.CurrentVersion,
                $"capture version was {captured.version}");
            Require(captured.stacks.Count(stack => stack != null
                    && instanceIds.Contains(stack.itemInstanceId)) == repetitionCount,
                "canonical capture did not preserve all dropped equipment stacks");
            Require(captured.uniqueItems.Count(item => item != null
                    && instanceIds.Contains(item.itemInstanceId)) == repetitionCount,
                "canonical capture did not preserve all authoritative equipment instances");
            Require(instanceIds.All(instanceId => captured.stacks.Count(stack => stack != null
                        && stack.itemInstanceId == instanceId) == 1
                    && captured.uniqueItems.Count(item => item != null
                        && item.itemInstanceId == instanceId) == 1),
                "canonical capture did not preserve a one-to-one stack/instance mapping");

            Require(!runtime.SpawnUniqueItemAt(
                    equipmentItemId,
                    Vector2Int.zero,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _),
                "generic unique spawn accepted an equipment item without an authoritative instance");

            return $"genericEquipment={genericEquipmentSpawned}; "
                + $"genericModule={genericModuleSpawned}; "
                + $"genericMaterial={genericMaterialSpawned}; "
                + $"drops={repetitionCount}; stacks={captured.stacks.Count}; "
                + $"uniqueItems={captured.uniqueItems.Count}; "
                + $"captureVersion={captured.version}";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyQualityRejectedUniqueDeliveryIdentity()
    {
        WorldItemStackRuntime items = CreateRuntime(
            out _,
            out CombatEquipmentRuntime equipment);
        try
        {
            CombatEquipmentInstance created = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Good,
                CombatEquipmentWorldState.Loose);
            string physicalItemId = PhysicalItemIds.ForEquipment(
                created.definitionId);
            Require(items.SpawnExistingUniqueItemAt(
                    physicalItemId,
                    (ItemInstanceId)created.instanceId,
                    new Vector2Int(2, 2),
                    WorldItemStackState.FacilityOutputBuffer,
                    QualityRejectedOutputRules.MarketDestinationId,
                    out string stackId)
                && equipment.TryLinkToWorldStack(
                    created.instanceId,
                    stackId,
                    CombatEquipmentWorldState.Loose),
                "failed to materialize the quality-rejected equipment output");
            WorldItemStackSnapshot before = items.GetAllStacks()
                .Single(stack => stack.StackId == stackId);
            string signature = before.StackSignature;
            Vector2Int saleDropoff = new(11, 4);

            Require(items.TryRequestStackDelivery(
                    stackId,
                    1,
                    saleDropoff,
                    QualityRejectedOutputRules.MarketDestinationId,
                    out int requested,
                    out string deliveryFailure)
                && requested == 1,
                $"quality-rejected delivery request failed: {deliveryFailure}");
            WorldItemStackSnapshot outbound = items.GetAllStacks()
                .Single(stack => stack.StackId == stackId);
            Require(outbound.ItemInstanceId == before.ItemInstanceId,
                "quality-rejected delivery changed the item-instance ID");
            Require(outbound.StackSignature == signature,
                "quality-rejected delivery changed the instance components");
            Require(outbound.State == WorldItemStackState.Loose
                    && outbound.DestinationId
                        == QualityRejectedOutputRules.MarketDestinationId
                    && outbound.HasDestinationPosition
                    && outbound.DestinationPosition == saleDropoff,
                "quality-rejected output did not become an outbound physical haul");
            Require(equipment.TryGetInstance(
                    created.instanceId,
                    out CombatEquipmentInstance linked)
                    && linked.sourceStackId == stackId,
                "equipment source-stack authority changed during market routing");
            Require(!equipment.TryConsumeForMarketSale(
                    stackId,
                    out _,
                    out _),
                "market command consumed equipment before physical delivery");
            Require(items.GetAllStacks().Any(stack => stack.StackId == stackId)
                    && equipment.TryGetInstance(created.instanceId, out _),
                "failed market precondition mutated the equipment or stack");
            Require(items.TryRouteStackToDestination(
                    stackId,
                    WorldItemStackState.FacilityBuffer,
                    QualityRejectedOutputRules.MarketDestinationId,
                    saleDropoff,
                    out string routeFailure),
                $"quality-rejected output could not enter the market buffer: {routeFailure}");
            Require(equipment.TryConsumeForMarketSale(
                    stackId,
                    out CombatEquipmentInstance sold,
                    out string saleFailure),
                $"market command could not consume the delivered equipment: {saleFailure}");
            Require(sold.instanceId == created.instanceId
                    && sold.definitionId == created.definitionId
                    && sold.quality == created.quality,
                "market command returned the wrong equipment instance");
            Require(!items.GetAllStacks().Any(stack => stack.StackId == stackId)
                    && !equipment.TryGetInstance(created.instanceId, out _),
                "market settlement did not remove both physical and equipment authority");
            return $"stack={stackId}; instance={created.instanceId}; destination={saleDropoff}; consumed=true";
        }
        finally
        {
            items.Dispose();
        }
    }

    private static string VerifyEquipmentModulePhysicalAuthority()
    {
        List<GameObject> facilityObjects = new List<GameObject>();
        WorldItemStackRuntime items = CreateRuntime(
            out WorldItemRepository repository,
            out CombatEquipmentRuntime equipment);
        WorldItemStackRuntime restoredItems = null;
        try
        {
            BuildableObject appraisal = CreateEquipmentFacility(
                AppraisalFacilityPath,
                "PhysicalModuleAppraisal",
                new Vector2Int(10, 10),
                EquipmentProgressionWorkstationTags.Appraisal,
                facilityObjects);
            BuildableObject restoration = CreateEquipmentFacility(
                RestorationFacilityPath,
                "PhysicalModuleRestoration",
                new Vector2Int(12, 10),
                EquipmentProgressionWorkstationTags.Restoration,
                facilityObjects);
            BuildableObject precision = CreateEquipmentFacility(
                PrecisionFittingFacilityPath,
                "PhysicalModulePrecision",
                new Vector2Int(14, 10),
                EquipmentProgressionWorkstationTags.PrecisionFitting,
                facilityObjects);

            string appraisalDestination = EquipmentProgressionFacilityContract
                .GetLocalBufferDestinationId(appraisal);
            EquipmentModuleInstance module = equipment.CreateExpeditionModule(
                "module:weapon:balanced-core",
                3,
                appraisal.centerPos,
                WorldItemStackState.FacilityBuffer,
                appraisalDestination);
            WorldItemStackSnapshot moduleStack = items.GetAllStacks().Single(stack =>
                stack.StackId == module.sourceStackId);
            Require(moduleStack.ItemId == PhysicalItemIds.ForEquipmentModule()
                    && moduleStack.ItemInstanceId == module.instanceId
                    && moduleStack.State == WorldItemStackState.FacilityBuffer
                    && moduleStack.DestinationId == appraisalDestination
                    && moduleStack.Components.Any(component => component != null
                        && component.componentTypeId
                        == ItemInstanceComponentIds.EquipmentModule),
                "expedition module was not materialized as one authored physical item");

            foreach (string supplyItemId in new[]
                     {
                         "component:material-test-coupon",
                         DurableToolItemRules.InspectionGauge,
                         DurableToolItemRules.RuneIdentificationLens
                     })
            {
                Require(items.SpawnItemAt(
                        supplyItemId,
                        1,
                        appraisal.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        appraisalDestination,
                        out int spawnedSupply)
                    && spawnedSupply == 1,
                    $"failed to supply module appraisal item: {supplyItemId}");
            }

            WorldItemStackSnapshot couponBefore = items.GetAllStacks().Single(stack =>
                stack.ItemId == "component:material-test-coupon"
                && stack.DestinationId == appraisalDestination);
            WorldItemStackSnapshot gaugeBefore = items.GetAllStacks().Single(stack =>
                stack.ItemId == DurableToolItemRules.InspectionGauge
                && stack.DestinationId == appraisalDestination);
            WorldItemStackSnapshot lensBefore = items.GetAllStacks().Single(stack =>
                stack.ItemId == DurableToolItemRules.RuneIdentificationLens
                && stack.DestinationId == appraisalDestination);
            float gaugeDurabilityBefore = DurableToolItemRules.ReadCurrentDurability(
                gaugeBefore.ItemId,
                gaugeBefore.Components);
            float lensDurabilityBefore = DurableToolItemRules.ReadCurrentDurability(
                lensBefore.ItemId,
                lensBefore.Components);

            Require(!equipment.TryAppraiseModule(
                    module.instanceId,
                    restoration,
                    out DomainFailure wrongFacilityFailure)
                    && wrongFacilityFailure.Code
                    == FailureCode.EquipmentProgressionFacilityUnavailable,
                "module appraisal accepted a restoration workstation");
            Require(equipment.TryAppraiseModule(
                    module.instanceId,
                    appraisal,
                    out DomainFailure appraiseFailure),
                $"module appraisal failed: {appraiseFailure.Code}");
            EquipmentModuleInstance appraised = equipment.ModuleInstances
                .Single(candidate => candidate.instanceId == module.instanceId);
            Require(appraised.identified
                    && appraised.state
                        == EquipmentModuleProcessState.IdentifiedDamaged
                    && appraised.nextAppraisalOperationSequence == 2
                    && (EquipmentModuleAppraisalCommitPhase)
                        appraised.pendingAppraisal.phase
                        == EquipmentModuleAppraisalCommitPhase.None
                    && items.GetAllStacks().All(stack =>
                        stack.StackId != couponBefore.StackId)
                    && Mathf.Approximately(
                        DurableToolItemRules.ReadCurrentDurability(
                            gaugeBefore.ItemId,
                            items.GetAllStacks().Single(stack =>
                                stack.StackId == gaugeBefore.StackId).Components),
                        Mathf.Max(0f, gaugeDurabilityBefore - 1f))
                    && Mathf.Approximately(
                        DurableToolItemRules.ReadCurrentDurability(
                            lensBefore.ItemId,
                            items.GetAllStacks().Single(stack =>
                                stack.StackId == lensBefore.StackId).Components),
                        Mathf.Max(0f, lensDurabilityBefore - 2f)),
                "appraisal did not commit one coupon, exact tool wear, and a cleared outbox");

            string restorationDestination = EquipmentProgressionFacilityContract
                .GetLocalBufferDestinationId(restoration);
            Require(items.TryRouteStackToDestination(
                    module.sourceStackId,
                    WorldItemStackState.FacilityBuffer,
                    restorationDestination,
                    restoration.centerPos,
                    out string routeRestoreFailure),
                $"module could not enter the restoration local buffer: {routeRestoreFailure}");
            Require(equipment.TryRestoreModule(
                    module.instanceId,
                    restoration,
                    out DomainFailure restoreFailure),
                $"module restoration failed: {restoreFailure.Code}");

            CombatEquipmentInstance weapon = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Good,
                CombatEquipmentWorldState.MaintenanceBuffer,
                "material:steel");
            string precisionDestination = EquipmentProgressionFacilityContract
                .GetLocalBufferDestinationId(precision);
            Require(items.SpawnExistingUniqueItemAt(
                    PhysicalItemIds.ForEquipment(weapon.definitionId),
                    (ItemInstanceId)weapon.instanceId,
                    precision.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    precisionDestination,
                    out string weaponStackId)
                    && equipment.TryLinkToWorldStack(
                        weapon.instanceId,
                        weaponStackId,
                        CombatEquipmentWorldState.MaintenanceBuffer),
                "equipment could not enter the precision-fitting local buffer");
            Require(items.TryRouteStackToDestination(
                    module.sourceStackId,
                    WorldItemStackState.FacilityBuffer,
                    precisionDestination,
                    precision.centerPos,
                    out string routePrecisionFailure),
                $"module could not enter the precision-fitting local buffer: {routePrecisionFailure}");
            Require(equipment.TryInstallModule(
                    weapon.instanceId,
                    module.instanceId,
                    0,
                    precision,
                    out DomainFailure installFailure),
                $"module installation failed: {installFailure.Code}");
            Require(items.GetAllStacks().All(stack =>
                    stack.ItemInstanceId != module.instanceId)
                    && repository.EquipmentModules.TryGetValue(
                        module.instanceId,
                        out EquipmentModuleInstance installedModule)
                    && installedModule.state == EquipmentModuleProcessState.Installed
                    && string.IsNullOrWhiteSpace(installedModule.sourceStackId),
                "installed module retained a duplicate independent physical stack");

            Require(equipment.TryRemoveModule(
                    weapon.instanceId,
                    0,
                    precision,
                    out EquipmentModuleInstance removed,
                    out DomainFailure removeFailure),
                $"module removal failed: {removeFailure.Code}");
            Require(removed.state == EquipmentModuleProcessState.IdentifiedDamaged
                    && removed.condition <= 0.7f
                    && !string.IsNullOrWhiteSpace(removed.sourceStackId),
                "removed module was not returned as a damaged physical item");
            WorldItemStackSnapshot returnedStack = items.GetAllStacks().Single(stack =>
                stack.StackId == removed.sourceStackId);
            Require(returnedStack.State == WorldItemStackState.FacilityBuffer
                    && returnedStack.DestinationId == precisionDestination,
                "removed module was not returned to the precision facility local buffer");

            DungeonPhysicalItemSaveData physicalSave = items.Capture();
            restoredItems = CreateRuntime(
                out WorldItemRepository restoredRepository,
                out CombatEquipmentRuntime restoredEquipment);
            restoredItems.Restore(physicalSave);
            EquipmentModuleInstance restoredModule = restoredEquipment.ModuleInstances
                .Single(candidate => candidate != null
                    && candidate.instanceId == module.instanceId);
            Require(restoredModule.sourceStackId == removed.sourceStackId
                    && restoredModule.state
                    == EquipmentModuleProcessState.IdentifiedDamaged
                    && restoredItems.GetAllStacks().Any(stack =>
                        stack.StackId == restoredModule.sourceStackId
                        && stack.ItemInstanceId == restoredModule.instanceId),
                "independent module did not round-trip with its physical stack linkage");

            Require(restoredItems.DeleteStack(restoredModule.sourceStackId),
                "restored independent module stack could not be destroyed");
            Require(restoredRepository.EquipmentModules.TryGetValue(
                    restoredModule.instanceId,
                    out EquipmentModuleInstance lostModule)
                    && lostModule.state == EquipmentModuleProcessState.Lost
                    && Mathf.Approximately(lostModule.condition, 0f)
                    && string.IsNullOrWhiteSpace(lostModule.sourceStackId),
                "destructive stack removal did not mark the independent module lost");

            restoredItems.Restore(physicalSave);
            EquipmentModuleInstance consumedModule = restoredEquipment.ModuleInstances
                .Single(candidate => candidate != null
                    && candidate.instanceId == module.instanceId);
            Require(restoredItems.TryConsumeStackQuantity(
                    consumedModule.sourceStackId,
                    1,
                    out WorldItemStackSnapshot consumedStack)
                    && consumedStack.ItemInstanceId == consumedModule.instanceId
                    && restoredRepository.EquipmentModules.TryGetValue(
                        consumedModule.instanceId,
                        out EquipmentModuleInstance consumedLostModule)
                    && consumedLostModule.state == EquipmentModuleProcessState.Lost
                    && Mathf.Approximately(consumedLostModule.condition, 0f)
                    && string.IsNullOrWhiteSpace(consumedLostModule.sourceStackId),
                "full stack consumption did not mark the independent module lost");

            return $"module={module.instanceId}; stack={removed.sourceStackId}; "
                + "wrongFacilityRejected=true; saveRoundTrip=true; lost=delete+consume";
        }
        finally
        {
            restoredItems?.Dispose();
            items.Dispose();
            foreach (GameObject facilityObject in facilityObjects)
            {
                UnityEngine.Object.DestroyImmediate(facilityObject);
            }
        }
    }

    private static string VerifyEquipmentModuleAppraisalAcknowledgementRecovery()
    {
        List<GameObject> facilityObjects = new List<GameObject>();
        EditorEquipmentPhysicalItemGatewayProxy gateway =
            new EditorEquipmentPhysicalItemGatewayProxy();
        WorldItemStackRuntime items = CreateRuntime(
            out _,
            out CombatEquipmentRuntime equipment,
            out _,
            out _,
            out _,
            equipmentGatewayOverride: gateway);
        WorldItemStackRuntime restoredItems = null;
        WorldItemStackRuntime invalidRestoreItems = null;
        try
        {
            BuildableObject appraisal = CreateEquipmentFacility(
                AppraisalFacilityPath,
                "PhysicalModuleAppraisalAckRestore",
                new Vector2Int(16, 10),
                EquipmentProgressionWorkstationTags.Appraisal,
                facilityObjects);
            string destinationId = EquipmentProgressionFacilityContract
                .GetLocalBufferDestinationId(appraisal);
            EquipmentModuleInstance module = equipment.CreateExpeditionModule(
                "module:weapon:balanced-core",
                3,
                appraisal.centerPos,
                WorldItemStackState.FacilityBuffer,
                destinationId);

            string[] supplyItemIds =
            {
                "component:material-test-coupon",
                DurableToolItemRules.InspectionGauge,
                DurableToolItemRules.RuneIdentificationLens
            };
            foreach (string itemId in supplyItemIds)
            {
                Require(items.SpawnItemAt(
                        itemId,
                        1,
                        appraisal.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        destinationId,
                        out int spawned)
                    && spawned == 1,
                    $"failed to seed appraisal recovery supply '{itemId}'");
            }

            WorldItemStackSnapshot couponBefore = items.GetAllStacks().Single(stack =>
                stack.ItemId == "component:material-test-coupon"
                && stack.DestinationId == destinationId);
            WorldItemStackSnapshot gaugeBefore = items.GetAllStacks().Single(stack =>
                stack.ItemId == DurableToolItemRules.InspectionGauge
                && stack.DestinationId == destinationId);
            WorldItemStackSnapshot lensBefore = items.GetAllStacks().Single(stack =>
                stack.ItemId == DurableToolItemRules.RuneIdentificationLens
                && stack.DestinationId == destinationId);
            float gaugeDurabilityBefore = DurableToolItemRules.ReadCurrentDurability(
                gaugeBefore.ItemId,
                gaugeBefore.Components);
            float lensDurabilityBefore = DurableToolItemRules.ReadCurrentDurability(
                lensBefore.ItemId,
                lensBefore.Components);

            gateway.FailNextAcknowledgement = true;
            Require(!equipment.TryAppraiseModule(
                    module.instanceId,
                    appraisal,
                    out DomainFailure injectedFailure)
                && injectedFailure.Code == FailureCode.EquipmentModuleMissing,
                "injected acknowledgement failure did not interrupt appraisal after publication");
            Require(gateway.AcknowledgementAttempts == 1
                    && gateway.SuccessfulAcknowledgements == 0,
                "injected acknowledgement attempt counters drifted");

            EquipmentModuleInstance pendingModule = equipment.ModuleInstances
                .Single(candidate => candidate.instanceId == module.instanceId);
            Require(pendingModule.identified
                    && pendingModule.state
                        == EquipmentModuleProcessState.IdentifiedDamaged
                    && pendingModule.nextAppraisalOperationSequence == 1
                    && (EquipmentModuleAppraisalCommitPhase)
                        pendingModule.pendingAppraisal.phase
                        == EquipmentModuleAppraisalCommitPhase.OutcomePublished,
                "failed appraisal acknowledgement did not retain the published outbox");
            CombatEquipmentInstance pendingHost = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Good,
                CombatEquipmentWorldState.MaintenanceBuffer,
                "material:steel");
            Require(items.SpawnExistingUniqueItemAt(
                    PhysicalItemIds.ForEquipment(pendingHost.definitionId),
                    (ItemInstanceId)pendingHost.instanceId,
                    appraisal.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    destinationId,
                    out string pendingHostStackId)
                && equipment.TryLinkToWorldStack(
                    pendingHost.instanceId,
                    pendingHostStackId,
                    CombatEquipmentWorldState.MaintenanceBuffer),
                "appraisal codec-negative host did not receive its authoritative physical stack");
            EquipmentModuleInstance invalidAttached = pendingModule.Clone();
            invalidAttached.state = EquipmentModuleProcessState.Installed;
            invalidAttached.sourceStackId = string.Empty;
            invalidAttached.attachedEquipmentInstanceId = pendingHost.instanceId;
            bool rejectedPendingAttachment = false;
            try
            {
                EquipmentItemStateCodec.Encode(
                    pendingHost,
                    new[] { invalidAttached });
            }
            catch (ArgumentException)
            {
                rejectedPendingAttachment = true;
            }
            Require(rejectedPendingAttachment,
                "equipment codec accepted an attached module that still owned an appraisal outbox");
            Require(items.TryGetPendingBatchPhysicalDisposition(
                    pendingModule.pendingAppraisal.operationId,
                    out PhysicalItemBatchDispositionReceipt pendingReceipt)
                && pendingReceipt.Kind == PhysicalItemDispositionKind.Sink
                && pendingReceipt.SourceStackIds.Count == 1
                && pendingReceipt.SourceStackIds[0] == couponBefore.StackId
                && pendingReceipt.Quantity == 1
                && pendingReceipt.InputMassGrams > 0L,
                "published appraisal did not retain the exact coupon Sink receipt");
            Require(items.GetAllStacks().All(stack =>
                    stack.StackId != couponBefore.StackId),
                "appraisal coupon survived its committed physical Sink");

            WorldItemStackSnapshot gaugeAfterFailure = items.GetAllStacks()
                .Single(stack => stack.StackId == gaugeBefore.StackId);
            WorldItemStackSnapshot lensAfterFailure = items.GetAllStacks()
                .Single(stack => stack.StackId == lensBefore.StackId);
            float expectedGaugeAfter = Mathf.Max(0f, gaugeDurabilityBefore - 1f);
            float expectedLensAfter = Mathf.Max(0f, lensDurabilityBefore - 2f);
            Require(Mathf.Approximately(
                        DurableToolItemRules.ReadCurrentDurability(
                            gaugeAfterFailure.ItemId,
                            gaugeAfterFailure.Components),
                        expectedGaugeAfter)
                    && Mathf.Approximately(
                        DurableToolItemRules.ReadCurrentDurability(
                            lensAfterFailure.ItemId,
                            lensAfterFailure.Components),
                        expectedLensAfter),
                "appraisal tools did not publish their exact wear envelope once");

            DungeonPhysicalItemSaveData save = items.Capture();
            DungeonPhysicalItemSaveData invalidJoin =
                JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                    JsonUtility.ToJson(save));
            invalidJoin.pendingBatchDispositions.Single(disposition =>
                    disposition.operationId
                        == pendingModule.pendingAppraisal.operationId)
                .reasonCode = "equipment-module-appraisal-wrong-reason";
            invalidRestoreItems = CreateRuntime(
                out _,
                out _,
                out _,
                out _,
                out _);
            bool invalidJoinRejected = false;
            try
            {
                invalidRestoreItems.Restore(invalidJoin);
            }
            catch (InvalidOperationException)
            {
                invalidJoinRejected = true;
            }
            Require(invalidJoinRejected,
                "physical restore accepted an appraisal owner/receipt reason mismatch");
            invalidRestoreItems.Dispose();
            invalidRestoreItems = null;

            EditorEquipmentPhysicalItemGatewayProxy restoredGateway =
                new EditorEquipmentPhysicalItemGatewayProxy();
            restoredItems = CreateRuntime(
                out _,
                out CombatEquipmentRuntime restoredEquipment,
                out _,
                out _,
                out _,
                equipmentGatewayOverride: restoredGateway);
            restoredItems.Restore(save);
            Require(restoredEquipment.TryAppraiseModule(
                    module.instanceId,
                    appraisal,
                    out DomainFailure recoveryFailure),
                $"restored appraisal did not finish acknowledgement-only recovery: {recoveryFailure.Code}");

            EquipmentModuleInstance recovered = restoredEquipment.ModuleInstances
                .Single(candidate => candidate.instanceId == module.instanceId);
            Require(recovered.identified
                    && recovered.state == EquipmentModuleProcessState.IdentifiedDamaged
                    && recovered.nextAppraisalOperationSequence == 2
                    && (EquipmentModuleAppraisalCommitPhase)
                        recovered.pendingAppraisal.phase
                        == EquipmentModuleAppraisalCommitPhase.None,
                "restored appraisal did not clear the outbox and advance exactly once");
            Require(restoredGateway.AcknowledgementAttempts == 1
                    && restoredGateway.SuccessfulAcknowledgements == 1
                    && !restoredItems.TryGetPendingBatchPhysicalDisposition(
                        pendingModule.pendingAppraisal.operationId,
                        out _),
                "restored appraisal did not acknowledge exactly one pending receipt");

            WorldItemStackSnapshot restoredGauge = restoredItems.GetAllStacks()
                .Single(stack => stack.StackId == gaugeBefore.StackId);
            WorldItemStackSnapshot restoredLens = restoredItems.GetAllStacks()
                .Single(stack => stack.StackId == lensBefore.StackId);
            Require(Mathf.Approximately(
                        DurableToolItemRules.ReadCurrentDurability(
                            restoredGauge.ItemId,
                            restoredGauge.Components),
                        expectedGaugeAfter)
                    && Mathf.Approximately(
                        DurableToolItemRules.ReadCurrentDurability(
                            restoredLens.ItemId,
                            restoredLens.Components),
                        expectedLensAfter)
                    && restoredItems.GetAllStacks().All(stack =>
                        stack.StackId != couponBefore.StackId),
                "restore recovery replayed coupon debit or tool wear");

            Require(!restoredEquipment.TryAppraiseModule(
                    module.instanceId,
                    appraisal,
                    out DomainFailure replayFailure)
                && replayFailure.Code == FailureCode.ModuleNotUnidentified
                && restoredGateway.AcknowledgementAttempts == 1,
                "terminal appraisal replay was not rejected without a second acknowledgement");

            return $"module={module.instanceId}; coupon={couponBefore.StackId}; "
                + $"grams={pendingReceipt.InputMassGrams}; ack=0+1; sequence=2; replay=0";
        }
        finally
        {
            invalidRestoreItems?.Dispose();
            restoredItems?.Dispose();
            items.Dispose();
            foreach (GameObject facilityObject in facilityObjects)
            {
                UnityEngine.Object.DestroyImmediate(facilityObject);
            }
        }
    }

    private static string VerifyEquipmentLineageTransferPhysicalAuthority()
    {
        List<GameObject> facilityObjects = new List<GameObject>();
        WorldItemStackRuntime items = CreateRuntime(
            out WorldItemRepository repository,
            out CombatEquipmentRuntime equipment);
        try
        {
            BuildableObject lineageFacility = CreateEquipmentFacility(
                LineageArchiveFacilityPath,
                "PhysicalLineageArchive",
                new Vector2Int(20, 10),
                EquipmentProgressionWorkstationTags.LineageArchive,
                facilityObjects);
            string lineageDestination = EquipmentProgressionFacilityContract
                .GetLocalBufferDestinationId(lineageFacility);
            CombatEquipmentInstance source = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Good,
                CombatEquipmentWorldState.MaintenanceBuffer,
                "material:steel");
            CombatEquipmentInstance target = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Masterwork,
                CombatEquipmentWorldState.MaintenanceBuffer,
                "material:blacksteel");
            repository.EquipmentInstances[source.instanceId].durabilityRatio = 0.43f;
            repository.EquipmentInstances[target.instanceId].durabilityRatio = 0.87f;

            string physicalItemId = PhysicalItemIds.ForEquipment("weapon:greatsword");
            Require(items.SpawnExistingUniqueItemAt(
                    physicalItemId,
                    (ItemInstanceId)source.instanceId,
                    lineageFacility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    lineageDestination,
                    out string sourceStackId)
                && equipment.TryLinkToWorldStack(
                    source.instanceId,
                    sourceStackId,
                    CombatEquipmentWorldState.MaintenanceBuffer),
                "lineage source was not materialized in the archive local buffer");
            Require(items.SpawnExistingUniqueItemAt(
                    physicalItemId,
                    (ItemInstanceId)target.instanceId,
                    lineageFacility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    lineageDestination,
                    out string targetStackId)
                && equipment.TryLinkToWorldStack(
                    target.instanceId,
                    targetStackId,
                    CombatEquipmentWorldState.MaintenanceBuffer),
                "lineage target was not materialized in the archive local buffer");

            EquipmentEvolutionState inherited = new EquipmentEvolutionState
            {
                generation = 3,
                mastery = 42f,
                pendingDirection = EquipmentEvolutionDirection.Protection,
                pendingHistoryHash = "history:lineage-source",
                reforgeReady = true,
                activeHistoricalNodeIds = new List<string>
                {
                    "history-node:first-owner"
                }
            };
            Require(equipment.TryUpdateEvolutionState(source.instanceId, inherited),
                "lineage source evolution could not be seeded");

            Require(items.SpawnItemAt(
                    EquipmentProgressionItemIds.LineageSeal,
                    2,
                    lineageFacility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    lineageDestination,
                    out int sealCount)
                && sealCount == 2,
                "lineage seal could not enter the archive local buffer");
            WorldItemStackSnapshot seal = items.GetAllStacks().Single(stack =>
                stack.ItemId == EquipmentProgressionItemIds.LineageSeal);

            Require(equipment.TryQueueHistoryTransfer(
                    source.instanceId,
                    target.instanceId,
                    seal.StackId,
                    lineageFacility,
                    out EquipmentHistoryTransferOrder order,
                    out DomainFailure queueFailure),
                $"lineage transfer queue failed: {queueFailure.Code}");
            Require(equipment.ApplyHistoryTransferWork(
                    order.orderId,
                    order.requiredWork,
                    lineageFacility,
                    out bool completed,
                    out DomainFailure workFailure)
                && completed,
                $"lineage transfer work failed: {workFailure.Code}");

            WorldItemStackSnapshot remainingSeal = items.GetAllStacks()
                .SingleOrDefault(stack => stack.StackId == seal.StackId);
            Require(!equipment.TryGetInstance(source.instanceId, out _)
                    && items.GetAllStacks().All(stack =>
                        stack.StackId != sourceStackId)
                    && remainingSeal != null
                    && remainingSeal.ItemId
                        == EquipmentProgressionItemIds.LineageSeal
                    && remainingSeal.Quantity == 1,
                "lineage transfer did not consume the source equipment and exactly one seal");
            Require(equipment.TryGetInstance(
                    target.instanceId,
                    out CombatEquipmentInstance transferred),
                "lineage target disappeared after transfer");
            Require(transferred.definitionId == "weapon:greatsword"
                    && transferred.materialId == "material:blacksteel"
                    && transferred.quality == CombatEquipmentQuality.Masterwork
                    && Mathf.Approximately(transferred.durabilityRatio, 0.87f)
                    && transferred.sourceStackId == targetStackId,
                "lineage target did not retain its authored physical properties");
            Require(transferred.evolution.generation == inherited.generation
                    && Mathf.Approximately(transferred.evolution.mastery, inherited.mastery)
                    && transferred.evolution.pendingHistoryHash == inherited.pendingHistoryHash
                    && transferred.evolution.activeHistoricalNodeIds.SequenceEqual(
                        inherited.activeHistoricalNodeIds),
                "lineage target did not receive the source evolution history");
            return $"sourceConsumed={source.instanceId}; target={target.instanceId}; "
                + $"sealStack={seal.StackId}; sealRemaining={remainingSeal.Quantity}; "
                + $"facility={lineageFacility.PersistentInstanceId.Value}";
        }
        finally
        {
            items.Dispose();
            foreach (GameObject facilityObject in facilityObjects)
            {
                UnityEngine.Object.DestroyImmediate(facilityObject);
            }
        }
    }

    private static string VerifyEquipmentIdentityAcrossCarryAndStorage()
    {
        GameObject actorObject = new GameObject("UniqueEquipmentCarrier");
        GameObject warehouseObject = new GameObject("UniqueEquipmentWarehouse");
        WorldItemStackRuntime runtime = null;
        TestWarehouseFacility warehouse = null;
        try
        {
            runtime = CreateRuntime(
                out WorldItemRepository repository,
                out CombatEquipmentRuntime equipment);
            warehouse = warehouseObject.AddComponent<TestWarehouseFacility>();
            warehouse.BindPhysicalStock(
                new PhysicalStockQuery(
                    repository,
                    runtime.CatalogProvider,
                    new PhysicalItemMassQuery(runtime.CatalogProvider)));
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterWarehouse(warehouse);

            CombatEquipmentInstance created = equipment.CreateInstance(
                "weapon:greatsword",
                CombatEquipmentQuality.Good,
                CombatEquipmentWorldState.Loose);
            string physicalItemId = PhysicalItemIds.ForEquipment(created.definitionId);
            Require(runtime.SpawnExistingUniqueItemAt(
                    physicalItemId,
                    (ItemInstanceId)created.instanceId,
                    Vector2Int.zero,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out string sourceStackId)
                    && equipment.TryLinkToWorldStack(
                        created.instanceId,
                        sourceStackId,
                        CombatEquipmentWorldState.Loose),
                "failed to materialize the repository equipment instance");
            WorldItemStackSnapshot source = runtime.GetAllStacks()
                .Single(stack => stack.StackId == sourceStackId);

            CharacterActor actor = InitializeFixtureActor(actorObject);
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? actorObject.AddComponent<CharacterCarryInventory>();
            carry.Configure(
                runtime.CatalogProvider,
                runtime.MassQuery,
                runtime.HaulingSettingsProvider,
                new CharacterCarryInventoryRegistry());
            Require(carry.TryAddPartialStack(
                    source.StackId,
                    source.ItemInstanceId,
                    source.ItemId,
                    1,
                    runtime.CatalogProvider,
                    runtime.HaulingSettingsProvider,
                    source.WasteOrigin,
                    source.Contamination,
                    source.Components,
                    out int accepted,
                    out string carryFailure)
                    && accepted == 1,
                $"failed to carry unique equipment: {carryFailure}");
            Require(equipment.TrySetWorldStateBySourceStack(
                    sourceStackId,
                    CombatEquipmentWorldState.Carried)
                    && runtime.DeleteStack(sourceStackId),
                "failed to remove the picked-up physical stack");
            Require(carry.Items.Single().itemInstanceId == created.instanceId,
                "carry inventory changed the equipment item-instance ID");

            Require(runtime.TryDepositCarriedItems(
                    actor,
                    carry,
                    warehouse,
                    out string depositFailure),
                $"failed to store carried equipment: {depositFailure}");
            WorldItemStackSnapshot stored = runtime.GetAllStacks()
                .Single(stack => stack.State == WorldItemStackState.Stored
                    && stack.ItemId == physicalItemId);
            Require(stored.ItemInstanceId == created.instanceId
                    && repository.EquipmentInstances.Count == 1
                    && equipment.TryGetInstance(created.instanceId, out CombatEquipmentInstance restored)
                    && restored.sourceStackId == stored.StackId,
                "equipment identity forked during carry/storage transfer");
            return $"instance={stored.ItemInstanceId}; source={sourceStackId}; stored={stored.StackId}";
        }
        finally
        {
            runtime?.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterWarehouse(warehouse);
            UnityEngine.Object.DestroyImmediate(actorObject);
            UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    private static BuildableObject CreateEquipmentFacility(
        string assetPath,
        string objectName,
        Vector2Int position,
        string expectedWorkstationTag,
        ICollection<GameObject> createdObjects)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
        Require(building != null, $"equipment facility asset missing: {assetPath}");
        Require(building.GetAbility<BuildingProductionBufferAbility>() != null,
            $"equipment facility has no local production buffer: {assetPath}");
        Require(string.Equals(
                building.GetAbility<BuildingProductionWorkstationAbility>()?
                    .WorkstationTag,
                expectedWorkstationTag,
                StringComparison.Ordinal),
            $"equipment facility workstation tag mismatch: {assetPath}");

        GameObject facilityObject = new GameObject(objectName);
        createdObjects?.Add(facilityObject);
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        facility.ConstructBuildableObject(
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingResearchWorkPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingFacilityStateChangePort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingRoomPolicyPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingEquipmentCraftingRuntimePort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingWorldRegistryPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingItemStackPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingAbilityRuntimeDispatcher>(),
            new UnityGameClock(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingPaidFacilityContractPort>(),
            new FacilityEvolutionStateComponentFactory());
        facility.Initialization(building, position);
        Require(EquipmentProgressionFacilityContract.Matches(
                facility,
                expectedWorkstationTag),
            $"equipment facility runtime contract mismatch: {assetPath}");
        return facility;
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

    private static CharacterActor InitializeFixtureActor(GameObject actorObject)
    {
        CharacterActor actor = actorObject.AddComponent<CharacterActor>();
        if (actorObject.GetComponent<CharacterAiMemoryRuntime>() == null)
        {
            actorObject.AddComponent<CharacterAiMemoryRuntime>();
        }
        actor.EnsureRuntimeState();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actor.Initialization(
            CharacterAiEditorTestDependencies.RequireAuthoredCharacterDefinition(
                "Adventurer"));
        return actor;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestCatalogProvider : IDungeonItemCatalogProvider
    {
        private readonly Dictionary<string, DungeonItemDefinition> definitions =
            new Dictionary<string, DungeonItemDefinition>(StringComparer.Ordinal);

        public TestCatalogProvider()
        {
            foreach (DungeonItemDefinition definition in EditorItemCatalogFactory.Create().All)
            {
                definitions[definition.ItemId] = definition;
            }

            definitions["item:cheap"] = new DungeonItemDefinition("item:cheap", "Cheap Ore", "Cheap test item", StockCategory.General, 1, null, 0.5f, 75);
            definitions["item:rich"] = new DungeonItemDefinition("item:rich", "Rich Gem", "Rich test item", StockCategory.General, 50, null, 0.2f, 75);
            definitions["item:buffer"] = new DungeonItemDefinition("item:buffer", "Buffer Item", "Buffer test item", StockCategory.General, 5, null, 1f, 75);
            definitions["item:stored"] = new DungeonItemDefinition("item:stored", "Stored Item", "Stored test item", StockCategory.General, 100, null, 1f, 75);
            definitions["item:heavy"] = new DungeonItemDefinition("item:heavy", "Heavy Ingot", "Heavy test item", StockCategory.Weapon, 3, null, 2.25f, 75);
            definitions["item:test-water-decoy"] = new DungeonItemDefinition(
                "item:test-water-decoy",
                "Water Category Decoy",
                "Same-category exact-lot regression item",
                StockCategory.Water,
                1,
                null,
                0.5f,
                75);
        }

        public IReadOnlyList<DungeonItemDefinition> All => definitions.Values.ToArray();

        public DungeonItemDefinition GetDefinition(string itemId)
        {
            return TryGetDefinition(itemId, out DungeonItemDefinition definition)
                ? definition
                : throw new KeyNotFoundException($"Unknown test item '{itemId}'.");
        }

        public bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
        {
            return definitions.TryGetValue(itemId ?? string.Empty, out definition);
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

            snapshot.Normalize();
            MaxCarryMultiplier = snapshot.maxCarryMultiplier;
        }
    }

    private static string VerifyManualWaterExactLotPendingTransfer()
    {
        const string destinationId =
            "plumbing:process-water:building:test-manual-water:work:craft";
        const string operationId =
            "production-process-fluid:production-bill:99810:00000001:manual-water:0000:building:test-manual-water";
        WorldItemStackRuntime items = CreateRuntime(
            out WorldItemRepository repository,
            out _,
            out ItemQuantityReservationService quantityReservations,
            out _);
        IPhysicalItemBatchDispositionService innerDispositions =
            new PhysicalItemBatchDispositionService(
                repository,
                items.MassQuery,
                EditorNullItemMarkerPresenter.Instance,
                quantityReservations);
        var dispositions = new FailOnceBatchDispositionService(
            innerDispositions);
        Require(items.SpawnItemAt(
                "item:test-water-decoy",
                1,
                new Vector2Int(4, 4),
                WorldItemStackState.FacilityBuffer,
                destinationId,
                out int decoySpawned)
            && decoySpawned == 1,
            "manual-water fixture could not spawn its same-category decoy");
        Require(items.SpawnItemAt(
                "resource:clean-water",
                1,
                new Vector2Int(4, 4),
                WorldItemStackState.FacilityBuffer,
                destinationId,
                out int waterSpawned)
            && waterSpawned == 1,
            "manual-water fixture could not spawn clean water");

        GameObject host = new GameObject("Manual Water Exact Lot Fixture");
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            data.id = 99810;
            data.objectName = "수동 식수 exact lot 시험 시설";
            var abilities = new BuildingAbilityCollection();
            abilities.Add(new BuildingUtilityConnectionAbility
            {
                channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater
            });
            abilities.Add(new BuildingWaterStorageAbility
            {
                channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater,
                cleanWaterCapacity = 10f,
                wastewaterCapacity = 10f
            });
            data.ReplaceAbilities(abilities);
            BuildableObject facility = host.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            typeof(BuildableObject).GetProperty(nameof(BuildableObject.BuildingData))
                ?.SetValue(facility, data);
            typeof(BuildableObject).GetProperty(nameof(BuildableObject.centerPos))
                ?.SetValue(facility, new Vector2Int(4, 4));
            facility.RestorePersistentIdentity(
                new BuildingInstanceId("building:test-manual-water"));

            object runtime = CreateFluidRuntimeForPhysicalFixture(
                items,
                dispositions,
                facility);
            IFluidInfrastructureTransaction directManual =
                (IFluidInfrastructureTransaction)runtime;
            IFluidInfrastructurePersistence directPersistence =
                (IFluidInfrastructurePersistence)runtime;
            dispositions.FailNextAcknowledgement = true;
            Require(!directManual.TryConsumeManualContainer(
                    facility,
                    destinationId,
                    0.2f,
                    out DomainFailure directFailure)
                && directFailure.IsFailure,
                "direct manual-water acknowledgement fault did not fail loud");
            FluidNodeSaveData faultedImmediate = directPersistence.Capture()
                .nodes.Single(node => node.buildingInstanceId
                    == "building:test-manual-water");
            Require(faultedImmediate.nextImmediateManualWaterOperationSequence == 1
                && faultedImmediate.pendingManualWaterTransfers.Count == 1
                && faultedImmediate.pendingManualWaterTransfers[0]
                    .immediateConsumption
                && faultedImmediate.pendingManualWaterTransfers[0]
                    .fluidStateApplied
                && Mathf.Approximately(faultedImmediate.manualWaterReserve, 0.8f),
                "direct manual-water acknowledgement fault lost its durable outcome owner");
            Require(directManual.TryConsumeManualContainer(
                    facility,
                    destinationId,
                    0.2f,
                    out DomainFailure directRetryFailure)
                && !directRetryFailure.IsFailure,
                "direct manual-water acknowledgement-only retry failed");
            FluidNodeSaveData recoveredImmediate = directPersistence.Capture()
                .nodes.Single(node => node.buildingInstanceId
                    == "building:test-manual-water");
            Require(recoveredImmediate.nextImmediateManualWaterOperationSequence == 2
                && recoveredImmediate.pendingManualWaterTransfers.Count == 0
                && Mathf.Approximately(recoveredImmediate.manualWaterReserve, 0.8f),
                "direct manual-water retry debited or applied its fluid outcome twice");
            Require(items.GetAllStacks().Count(stack =>
                    stack.ItemId == "resource:clean-water") == 0
                && items.GetAllStacks().Count(stack =>
                    stack.ItemId == "item:test-water-decoy") == 1,
                "direct manual-water fallback consumed a same-category decoy");
            Require(items.SpawnItemAt(
                    "resource:clean-water",
                    1,
                    facility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    destinationId,
                    out int stagedWaterSpawned)
                && stagedWaterSpawned == 1,
                "manual-water fixture could not reseed its staged exact lot");
            // Staged production ownership uses an independent current-format
            // transaction so the direct fallback's fluid reserve cannot satisfy
            // this second assertion without a physical item transfer.
            runtime = CreateFluidRuntimeForPhysicalFixture(
                items,
                dispositions,
                facility);
            IManualWaterTransferTransaction manual =
                (IManualWaterTransferTransaction)runtime;
            IFluidInfrastructurePersistence persistence =
                (IFluidInfrastructurePersistence)runtime;
            Require(manual.TryStageManualWaterTransfer(
                    facility,
                    destinationId,
                    0.2f,
                    operationId,
                    out ManualWaterTransferReceipt staged,
                    out DomainFailure stageFailure)
                && !stageFailure.IsFailure
                && staged.IsValid
                && staged.TransferredWaterUnits == 1
                && staged.InputMassGrams == 500L
                && staged.SourceStackIds.Count == 1,
                "manual-water exact physical lot was not staged with 500g provenance");
            Require(items.GetAllStacks().Count(stack =>
                    stack.ItemId == "resource:clean-water") == 0
                && items.GetAllStacks().Count(stack =>
                    stack.ItemId == "item:test-water-decoy") == 1,
                "manual-water stage consumed the wrong Water-category item");

            Require(manual.TryStageManualWaterTransfer(
                    facility,
                    destinationId,
                    0.2f,
                    operationId,
                    out ManualWaterTransferReceipt replayed,
                    out _)
                && replayed.PhysicalCommitId == staged.PhysicalCommitId
                && replayed.SourceStackIds.SequenceEqual(staged.SourceStackIds),
                "manual-water pending transfer did not replay idempotently");
            Require(!manual.TryStageManualWaterTransfer(
                    facility,
                    destinationId,
                    0.3f,
                    operationId,
                    out _,
                    out DomainFailure conflict)
                && conflict.IsFailure,
                "manual-water operation accepted conflicting retry data");

            Require(manual.TryApplyStagedManualWaterTransfer(
                    facility,
                    operationId,
                    out ManualWaterTransferReceipt applied,
                    out DomainFailure applyFailure)
                && !applyFailure.IsFailure
                && applied.FluidStateApplied,
                "manual-water staged transfer could not apply its reserve exactly once");
            DungeonFluidInfrastructureSaveData appliedSave = persistence.Capture();
            FluidNodeSaveData appliedNode = appliedSave.nodes.Single(node =>
                node.buildingInstanceId == "building:test-manual-water");
            Require(Mathf.Approximately(appliedNode.manualWaterReserve, 0.8f)
                && appliedNode.pendingManualWaterTransfers.Count == 1
                && appliedNode.pendingManualWaterTransfers[0].fluidStateApplied
                && appliedNode.pendingManualWaterTransfers[0]
                    .requestFingerprint.Length > 0,
                "manual-water reserve/provenance was not captured in current V6 format");
            var exactCandidate = new PhysicalItemRestoreCandidateDispositionSnapshot(
                PhysicalItemDispositionKind.Transfer,
                staged.OperationId,
                FluidPhysicalOperationIdentity.ManualReserveReasonCode,
                staged.RequestFingerprint,
                staged.SourceStackIds,
                staged.TransferredWaterUnits,
                staged.InputMassGrams,
                staged.PhysicalCommitId);
            FluidInfrastructureSaveSection.ValidatePhysicalRestoreCandidate(
                appliedSave,
                new PhysicalRestoreCandidateFixture(exactCandidate));
            Require(RejectsInvalidOperation(() =>
                    FluidInfrastructureSaveSection
                        .ValidatePhysicalRestoreCandidate(
                            appliedSave,
                            new PhysicalRestoreCandidateFixture())),
                "manual-water restore accepted a missing incoming receipt");
            var orphanCandidate = new PhysicalItemRestoreCandidateDispositionSnapshot(
                PhysicalItemDispositionKind.Transfer,
                "manual-water-orphan:test:00000001",
                FluidPhysicalOperationIdentity.ManualReserveReasonCode,
                "fingerprint:orphan",
                new[] { "stack:orphan" },
                1,
                500L,
                "physical-batch-disposition:1:manual-water-orphan:test:00000001:1:500");
            Require(RejectsInvalidOperation(() =>
                    FluidInfrastructureSaveSection
                        .ValidatePhysicalRestoreCandidate(
                            appliedSave,
                            new PhysicalRestoreCandidateFixture(
                                exactCandidate,
                                orphanCandidate))),
                "manual-water restore accepted an orphan incoming receipt");
            Require(manual.TryApplyStagedManualWaterTransfer(
                    facility,
                    operationId,
                    out _,
                    out _)
                && Mathf.Approximately(
                    persistence.Capture().nodes.Single(node =>
                        node.buildingInstanceId == "building:test-manual-water")
                    .manualWaterReserve,
                    0.8f),
                "manual-water retry credited reserve twice");

            persistence.Restore(persistence.PrepareRestore(appliedSave));
            Require(manual.AcknowledgeManualWaterTransfer(
                    operationId,
                    out DomainFailure acknowledgeFailure)
                && !acknowledgeFailure.IsFailure
                && persistence.Capture().nodes.Single(node =>
                    node.buildingInstanceId == "building:test-manual-water")
                    .pendingManualWaterTransfers.Count == 0,
                "manual-water restore could not acknowledge exact pending custody");

            Require(items.SpawnItemAt(
                    "resource:clean-water",
                    1,
                    facility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    destinationId,
                    out int batchWaterSpawned)
                && batchWaterSpawned == 1,
                "manual-water batch fixture could not seed its second exact lot");
            var processFluids = new ProcessFluidUseRuntime(
                (IFluidInfrastructureTransaction)runtime,
                (IFluidWastewaterTransaction)runtime,
                items);
            Require(processFluids.TryConsumeBatch(
                    new[]
                    {
                        new ProcessFluidCycleDemand(
                            facility,
                            BuiltInWorkTypeIds.Craft,
                            1f,
                            0f,
                            true)
                    },
                    "production-process-fluid:production-bill:99810:00000002",
                    out IReadOnlyList<ManualWaterTransferReceipt> batchTransfers,
                    out IReadOnlyList<ProcessWastewaterComponent>
                        batchWastewaterComponents,
                    out DomainFailure batchFailure)
                && !batchFailure.IsFailure
                && batchTransfers.Count == 1
                && batchTransfers[0].InputMassGrams == 500L
                && batchWastewaterComponents.Count == 0
                && batchTransfers[0].FluidStateApplied
                && processFluids.AcknowledgeManualTransfers(
                    batchTransfers.Select(value => value.OperationId).ToArray(),
                    out DomainFailure batchAcknowledgeFailure)
                && !batchAcknowledgeFailure.IsFailure,
                "process-fluid batch did not commit/acknowledge exact manual-water custody");

            FluidNodeSaveData beforeWastewater = persistence.Capture().nodes
                .Single(node => node.buildingInstanceId
                    == "building:test-manual-water");
            var invalidComponents = new[]
            {
                new ProcessWastewaterComponent(
                    ProcessWastewaterComposition.Whey,
                    ProcessWastewaterSourceKind.Recipe,
                    "recipe:test-curd",
                    0.2f)
            };
            Require(!processFluids.TryConsumeBatch(
                    new[]
                    {
                        new ProcessFluidCycleDemand(
                            facility,
                            BuiltInWorkTypeIds.Craft,
                            0f,
                            0.3f,
                            false,
                            invalidComponents)
                    },
                    "production-process-fluid:production-bill:99810:invalid-wastewater",
                    out _,
                    out _,
                    out DomainFailure invalidWastewaterFailure)
                && invalidWastewaterFailure.IsFailure,
                "process-fluid batch accepted a mismatched wastewater composition");
            FluidNodeSaveData afterInvalidWastewater = persistence.Capture().nodes
                .Single(node => node.buildingInstanceId
                    == "building:test-manual-water");
            Require(Mathf.Approximately(
                    beforeWastewater.wastewater,
                    afterInvalidWastewater.wastewater)
                && beforeWastewater.pendingManualWaterTransfers.Count
                    == afterInvalidWastewater.pendingManualWaterTransfers.Count,
                "invalid wastewater composition mutated fluid or transfer state");

            var exactComponents = new[]
            {
                new ProcessWastewaterComponent(
                    ProcessWastewaterComposition.Whey,
                    ProcessWastewaterSourceKind.Recipe,
                    "recipe:test-curd",
                    0.2f),
                new ProcessWastewaterComponent(
                    ProcessWastewaterComposition.SanitaryWashwater,
                    ProcessWastewaterSourceKind.Facility,
                    "building:test-manual-water",
                    0.1f)
            };
            Require(processFluids.TryConsumeBatch(
                    new[]
                    {
                        new ProcessFluidCycleDemand(
                            facility,
                            BuiltInWorkTypeIds.Craft,
                            0f,
                            0.3f,
                            false,
                            exactComponents)
                    },
                    "production-process-fluid:production-bill:99810:typed-wastewater",
                    out IReadOnlyList<ManualWaterTransferReceipt>
                        wastewaterManualTransfers,
                    out IReadOnlyList<ProcessWastewaterComponent>
                        committedWastewaterComponents,
                    out DomainFailure exactWastewaterFailure)
                && !exactWastewaterFailure.IsFailure
                && wastewaterManualTransfers.Count == 0
                && committedWastewaterComponents.Count == 2
                && committedWastewaterComponents.Sum(value => value.MassGrams)
                    == 150L
                && committedWastewaterComponents[0].Composition
                    == ProcessWastewaterComposition.SanitaryWashwater
                && committedWastewaterComponents[1].Composition
                    == ProcessWastewaterComposition.Whey,
                "process-fluid batch did not preserve exact mixed wastewater provenance");
            FluidNodeSaveData afterExactWastewater = persistence.Capture().nodes
                .Single(node => node.buildingInstanceId
                    == "building:test-manual-water");
            Require(Mathf.Approximately(
                    afterExactWastewater.wastewater,
                    beforeWastewater.wastewater + 0.3f),
                "process-fluid batch did not commit exact aggregate wastewater");
            return "cleanWaterInput=500g; processUse=100g; reserve=400g; decoyPreserved=1; replayDelta=0; pendingAfterAck=0; processBatchExact=1; wastewaterTyped=150g; wastewaterInvalidDelta=0";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static object CreateFluidRuntimeForPhysicalFixture(
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService physicalDispositions,
        BuildableObject facility)
    {
        Type runtimeType = typeof(IFluidInfrastructureQuery).Assembly
            .GetType("FluidNetworkRuntime", throwOnError: true);
        Type topologyType = typeof(IFluidInfrastructureQuery).Assembly
            .GetType("IndustrialInfrastructureTopologyRuntime", throwOnError: true);
        object topology = Activator.CreateInstance(
            topologyType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { new FixtureBuildingWorldQuery(facility) },
            culture: null);
        return Activator.CreateInstance(
            runtimeType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[]
            {
                topology,
                CreateNullProxy<IPowerInfrastructureQuery>(),
                items,
                physicalDispositions,
                CreateNullProxy<IWorldFilthQuery>(),
                CreateNullProxy<IGameClock>(),
                CreateNullProxy<IFacilityCapabilityQuery>(),
                CreateNullProxy<IBuildingFacilityStateChangePort>(),
                new DungeonRuntimeAggregateRootStore()
            },
            culture: null);
    }

    private static bool RejectsInvalidOperation(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class PhysicalRestoreCandidateFixture :
        IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> values;

        internal PhysicalRestoreCandidateFixture(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values)
        {
            this.values = values ?? Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => values;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = values.SingleOrDefault(value => string.Equals(
                value.OperationId,
                operationId,
                StringComparison.Ordinal));
            return disposition != null;
        }
    }

    private sealed class FailOnceBatchDispositionService :
        IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;

        internal FailOnceBatchDispositionService(
            IPhysicalItemBatchDispositionService inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal bool FailNextAcknowledgement { get; set; }

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) =>
            inner.TryCommit(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) =>
            inner.TryCommitPending(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);

        public bool Acknowledge(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "fixture manual-water acknowledgement fault";
                return false;
            }
            return inner.Acknowledge(commitId, out failureReason);
        }
    }

    private static T CreateNullProxy<T>() where T : class =>
        DispatchProxy.Create<T, PhysicalFixtureNullDispatchProxy>();

    private static string VerifyTypedPhysicalDisposition()
    {
        WorldItemStackRuntime runtime = CreateRuntime(out WorldItemRepository repository, out _);
        try
        {
            string stackId = repository.AddEditorTestStack(
                "item:buffer",
                2,
                WorldItemStackState.Loose);
            Require(
                !runtime.TryCommitPhysicalDisposition(
                    stackId,
                    1,
                    PhysicalItemDispositionKind.Transform,
                    $"qa:typed-transform-bypass:{stackId}",
                    "qa-transform-bypass",
                    out _,
                    out string rejectedReason)
                && string.Equals(
                    rejectedReason,
                    "physical-disposition-invalid-request",
                    StringComparison.Ordinal)
                && runtime.GetAllStacks().Single().Quantity == 2,
                "Transform bypassed IPhysicalItemTransformService through the terminal disposition API");
            Require(
                runtime.TryCommitPhysicalDisposition(
                    stackId,
                    1,
                    PhysicalItemDispositionKind.Sink,
                    $"qa:typed-sink:{stackId}",
                    "qa-terminal-consumption",
                    out PhysicalItemDispositionReceipt receipt,
                    out string failureReason),
                $"typed Sink failed: {failureReason}");
            Require(
                receipt.IsCommitted
                && receipt.Kind == PhysicalItemDispositionKind.Sink
                && receipt.Quantity == 1
                && receipt.InputMassGrams == 1000L
                && runtime.GetAllStacks().Single().Quantity == 1,
                "typed Sink receipt did not preserve exact quantity and gram authority");
            return "V27_TYPED_SINK_TRANSFER_DISPOSITION_EXACT=PASS; "
                + "V27_TRANSFORM_CANNOT_BYPASS_TRANSFORM_SERVICE=PASS; "
                + $"commit={receipt.CommitId}; mass={receipt.InputMassGrams}g";
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static string VerifyUniqueRetailTransferCommitAndRollback()
    {
        GameObject actorObject = new GameObject("UniqueRetailTransferCarrier");
        WorldItemStackRuntime runtime = null;
        try
        {
            runtime = CreateRuntime(
                out WorldItemRepository repository,
                out CombatEquipmentRuntime equipment,
                out ItemQuantityReservationService reservations,
                out IReservedItemTransferService reservedTransfer);
            IReservedRetailStockTransferService retailTransfers =
                reservedTransfer as IReservedRetailStockTransferService;
            Require(
                retailTransfers != null,
                "retail exact-lot transfer service was not composed");
            IRetailStockPhysicalRuntime retailPhysical =
                new RetailStockPhysicalRuntime(
                    new CombatEquipmentRuntimeRetailAuthorityAdapter(equipment));
            CharacterActor actor = InitializeFixtureActor(actorObject);
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor)
                ?? actorObject.AddComponent<CharacterCarryInventory>();
            carry.Configure(
                runtime.CatalogProvider,
                runtime.MassQuery,
                runtime.HaulingSettingsProvider,
                new CharacterCarryInventoryRegistry());
            string actorId = $"test:{actor.GetInstanceID()}";

            ReservedRetailStockTransferReceipt TakeOne(
                int ordinal,
                out CombatEquipmentInstance created,
                out string physicalItemId,
                out string sourceStackId)
            {
                created = equipment.CreateInstance(
                    "weapon:dagger",
                    CombatEquipmentQuality.Normal,
                    CombatEquipmentWorldState.Loose);
                physicalItemId = PhysicalItemIds.ForEquipment(created.definitionId);
                Require(runtime.SpawnExistingUniqueItemAt(
                        physicalItemId,
                        (ItemInstanceId)created.instanceId,
                        new Vector2Int(ordinal, 0),
                        WorldItemStackState.Loose,
                        string.Empty,
                        out string createdStackId)
                    && equipment.TryLinkToWorldStack(
                        created.instanceId,
                        createdStackId,
                        CombatEquipmentWorldState.Loose),
                    "failed to materialize unique retail transfer source");
                sourceStackId = createdStackId;
                WorldItemStackSnapshot source = runtime.GetAllStacks()
                    .Single(stack => stack.StackId == createdStackId);
                string operationId = $"retail-restock:test:{ordinal}";
                string signature = ItemReservationSignature.Create(
                    source.ItemId,
                    source.Components);
                Require(reservations.TryReserve(
                        operationId,
                        actorId,
                        ItemReservationPurpose.FacilityBuffer,
                        $"retail:test:{ordinal}",
                        new ItemQuantityReservationRequest(
                            new ItemStackId(sourceStackId),
                            1,
                            signature),
                        out ItemQuantityLease lease,
                        out DomainFailure reserveFailure),
                    $"unique retail source reservation failed: {reserveFailure}");
                WorldItemReservedStackQuantity reservation =
                    new WorldItemReservedStackQuantity(
                        sourceStackId,
                        physicalItemId,
                        1,
                        source.Position,
                        WorldItemHaulDestinationKind.Warehouse,
                        string.Empty,
                        lease.leaseId,
                        operationId);
                Require(runtime.TryPickupReservedStackQuantity(
                        actor,
                        carry,
                        reservation,
                        out int pickedUp,
                        out string pickupFailure)
                    && pickedUp == 1,
                    $"unique retail pickup failed: {pickupFailure}");
                Require(retailTransfers.TryTakeReservedRetailLots(
                        lease.leaseId,
                        1,
                        9100 + ordinal,
                        physicalItemId,
                        operationId,
                        carry,
                        out ReservedRetailStockTransferReceipt receipt,
                        out DomainFailure transferFailure),
                    $"unique retail transfer failed: {transferFailure}");
                return receipt;
            }

            ReservedRetailStockTransferReceipt committed = TakeOne(
                1,
                out CombatEquipmentInstance committedInstance,
                out _,
                out string committedSourceStackId);
            RetailStockLotSnapshot committedLot = committed.Lots.Single();
            bool hasRetailBoundInstance = equipment.TryGetInstance(
                committedInstance.instanceId,
                out CombatEquipmentInstance retailBound);
            Require(committedLot.itemInstanceId == committedInstance.instanceId
                    && !runtime.GetAllStacks().Any(stack =>
                        stack.StackId == committedSourceStackId)
                    && carry.Items.Count == 0
                    && hasRetailBoundInstance
                    && retailBound.worldState == CombatEquipmentWorldState.RetailStock
                    && retailBound.sourceStackId == committedLot.sourceOperationId,
                "unique retail transfer did not publish one exact retail owner");
            Require(retailPhysical.TryCommitExternalSink(
                    committedLot,
                    out string sinkFailure)
                && !equipment.TryGetInstance(committedInstance.instanceId, out _),
                $"unique retail external sink failed: {sinkFailure}");

            ReservedRetailStockTransferReceipt rolledBack = TakeOne(
                2,
                out CombatEquipmentInstance rollbackInstance,
                out _,
                out string rollbackSourceStackId);
            Require(retailTransfers.TryRollbackRetailTransfer(
                    rolledBack,
                    out DomainFailure rollbackFailure),
                $"unique retail rollback failed: {rollbackFailure}");
            WorldItemStackSnapshot restoredRecord = runtime.GetAllStacks()
                .SingleOrDefault(stack => stack.StackId == rollbackSourceStackId);
            bool hasRestoredInstance = equipment.TryGetInstance(
                rollbackInstance.instanceId,
                out CombatEquipmentInstance restoredInstance);
            Require(restoredRecord != null
                    && restoredRecord.State == WorldItemStackState.Carried
                    && carry.Items.Single().itemInstanceId == rollbackInstance.instanceId
                    && hasRestoredInstance
                    && restoredInstance.worldState == CombatEquipmentWorldState.Carried
                    && restoredInstance.sourceStackId == rollbackSourceStackId,
                "unique retail rollback did not restore physical stack, carry, and equipment authority");
            return $"committed={committedInstance.instanceId}; "
                + $"rolledBack={rollbackInstance.instanceId}; grams={committedLot.unitMassGrams}";
        }
        finally
        {
            runtime?.Dispose();
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private sealed class FixedEquippedApparelMassQuery :
        IEquippedApparelPhysicalMassQuery
    {
        private readonly long grams;

        public FixedEquippedApparelMassQuery(long grams)
        {
            this.grams = grams;
        }

        public long GetEquippedMassGrams(CharacterId characterId) =>
            characterId.IsValid ? grams : 0L;
    }

    private sealed class FixedEnvironmentalWorkwearQuery :
        IEnvironmentalWorkwearQuery
    {
        private readonly EnvironmentalWorkwearSO workwear;

        public FixedEnvironmentalWorkwearQuery(EnvironmentalWorkwearSO workwear)
        {
            this.workwear = workwear;
        }

        public int Version => 1;

        public bool TryGetEquipped(
            CharacterId characterId,
            out EnvironmentalWorkwearSO equipped)
        {
            equipped = characterId.IsValid ? workwear : null;
            return equipped != null;
        }

        public bool TryGetEquippedItemInstance(
            CharacterId characterId,
            out ItemInstanceId itemInstanceId,
            out EnvironmentalWorkwearSO equipped)
        {
            bool found = TryGetEquipped(characterId, out equipped);
            itemInstanceId = found
                ? (ItemInstanceId)"apparel-instance:qa-hauling-harness"
                : default;
            return found;
        }

        public int GetAvailableStock(string workwearId) => 0;
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

    private sealed class FixtureBuildingWorldQuery : IBuildingWorldQuery
    {
        internal FixtureBuildingWorldQuery(params BuildableObject[] buildings)
        {
            Buildings = buildings ?? Array.Empty<BuildableObject>();
        }

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    public class PhysicalFixtureNullDispatchProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (!parameterType.IsByRef)
                {
                    continue;
                }
                Type elementType = parameterType.GetElementType();
                args[i] = elementType.IsValueType
                    ? Activator.CreateInstance(elementType)
                    : null;
            }
            Type returnType = targetMethod.ReturnType;
            return returnType == typeof(void)
                ? null
                : returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
        }
    }

    private sealed class TestGridProvider : IGridSystemProvider
    {
        private readonly Grid grid;

        public TestGridProvider(Grid grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
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
            return true;
        }
    }

    private sealed class TestWarehouseFacility : MonoBehaviour, IWarehouseFacility
    {
        private readonly WarehouseInventory inventory = new WarehouseInventory(200);

        public WarehouseInventory Inventory => inventory;
        public BuildingInstanceId PersistentInstanceId =>
            (BuildingInstanceId)"building:test-physical-item-warehouse";
        public bool HasWarehouseInventory => true;

        public void BindPhysicalStock(IStockQuery stockQuery)
        {
            inventory.BindPhysicalStock(
                stockQuery,
                PersistentInstanceId,
                CharacterAiEditorTestDependencies.AuthoredGameplay);
        }
    }

    private sealed class TestIdRegistry : ICharacterIdRegistry
    {
        public bool TryGetPersistentId(CharacterActor actor, out string persistentId)
        {
            persistentId = actor != null ? $"test:{actor.GetInstanceID()}" : string.Empty;
            return actor != null;
        }

        public string GetOrAssignPersistentId(CharacterActor actor)
        {
            return actor != null ? $"test:{actor.GetInstanceID()}" : "test:null";
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
