#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ProductionOutputDestinationLifecycleDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Economy/Run Production Output Destination Lifecycle")]
    public static void RunAll()
    {
        using EditorVerificationSceneFixtureScope fixtureScene = new(
            "qa:production-output-destination-lifecycle");
        VerifyCanonicalDestinationIdentity();
        VerifyGenericBillBlocksMutation();
        VerifyEquipmentAndApparelBlockMutation();
        VerifyCombinedOrderingAndFingerprintAreDeterministic();
        VerifyDurableFingerprintIgnoresVolatileRevision();
        VerifyDetachedDurableSaveProjector();
        VerifyCombinedCombatRepairProjectionContracts();
        VerifyApparelCraftAttemptOperationIdentity();
        VerifyDuplicateContributorFailsLoud();
        VerifyMutationEpochOwnership();
        VerifyEmptyFenceRejectsStaleCandidate();
        VerifyEmptyFenceRollbackRestoresExactAuthority();
        VerifyEmptyFenceCommitClosesWithoutRestore();
        VerifyStockSensorFenceRollbackRestoresExactAuthority();
        VerifyStockSensorActiveStateBlocksMutation();
        VerifyStockSensorPostPrepareOccupancyBlocksCommit();
        VerifyStockSensorRollbackWhenOutputRevokeFails();
        VerifyPlacementDemolitionUsesFenceInOrder();
        VerifyPlacementDemolitionRollsBackWhenGridRemovalFails();
        VerifyStructuralAndCoverLossUseTheSameFence();
        VerifyDestructiveLossBlockAndRollbackPreserveWorld();
        VerifyWorldRemovalPortIsExactAndIdempotent();
        VerifyIdentityChangingMutationsRequireNoAuthority();
        Debug.Log("V27_PRODUCTION_OUTPUT_DESTINATION_LIFECYCLE=PASS");
    }

    private static void VerifyCanonicalDestinationIdentity()
    {
        BuildingInstanceId facility = (BuildingInstanceId)"building:qa:lifecycle";
        ProductionOutputDestinationId destination =
            ProductionOutputDestinationId.FromFacility(facility);
        Require(destination.Value == "production-output:building:qa:lifecycle",
            "Facility destination formatting drifted.");
        Require(ProductionOutputDestinationId.TryParse(destination.Value, out var parsed)
            && parsed.Equals(destination),
            "Canonical destination failed to parse.");
        Require(!ProductionOutputDestinationId.TryParse(
                " production-output:building:qa:lifecycle",
                out _),
            "Non-canonical destination was accepted.");
    }

    private static void VerifyGenericBillBlocksMutation()
    {
        BuildingInstanceId facility = (BuildingInstanceId)"building:qa:bill-owner";
        ProductionFacilityBillLifecycleSnapshot bill = new(
            facility,
            billCount: 1,
            activeWipCount: 1,
            waitingForOutputSpaceCount: 1,
            publicationPreparedCount: 0,
            physicalCommitPendingCount: 0,
            billAuthorityRevision: 7L,
            semanticFingerprint: Fingerprint("bill"));
        ProductionBillLifecycleContributor contributor = new(new FixedBillQuery(bill));
        ProductionOutputDestinationLifecycleContribution captured = contributor.Capture(
            facility,
            ProductionOutputDestinationId.FromFacility(facility));
        Require(captured.HasAuthority && !captured.IsEmpty,
            "Owned generic bill was not a lifecycle authority.");
        Require(captured.Blocks.Any(value =>
                value.Code == ProductionOutputLifecycleBlockCode.GenericBill)
            && captured.Blocks.Any(value =>
                value.Code == ProductionOutputLifecycleBlockCode.GenericWorkInProgress)
            && captured.Blocks.Any(value =>
                value.Code == ProductionOutputLifecycleBlockCode.WaitingForOutputSpace),
            "Generic bill lifecycle did not expose typed blockers.");
    }

    private static void VerifyEquipmentAndApparelBlockMutation()
    {
        BuildingInstanceId facility = (BuildingInstanceId)"building:qa:multi-owner";
        CombatEquipmentCraftOrderSaveData equipmentOrder = new()
        {
            orderId = "equipment-order:qa",
            definitionId = "equipment:qa",
            materialId = "material:iron",
            facilityPersistentId = facility.Value,
            requiredWork = 10f,
            completedWork = 4f
        };
        CombatEquipmentRepairOrder repairOrder = new()
        {
            orderId = "repair-order:qa",
            equipmentInstanceId = "equipment-instance:qa",
            facilityBuildingId = facility.Value,
            state = CombatEquipmentRepairOrderState.InProgress,
            completedWork = 2f,
            requiredWork = 6f
        };
        CombatEquipmentCraftLifecycleContributor equipment = new(
            () => new FixedEquipmentQueue(equipmentOrder),
            () => new FixedMaintenanceOrders(repairOrder));
        ApparelWorkOrderSaveData apparelOrder = new()
        {
            orderId = "apparel-order:qa",
            kind = ApparelWorkOrderKind.Craft,
            state = ApparelWorkOrderState.InProgress,
            facilityInstanceId = facility.Value,
            requiredWork = 8f,
            completedWork = 3f
        };
        ApparelWorkOrderLifecycleContributor apparel = new(
            new FixedApparelOrders(apparelOrder));
        ProductionOutputDestinationId destination =
            ProductionOutputDestinationId.FromFacility(facility);
        ProductionOutputDestinationLifecycleContribution equipmentSnapshot =
            equipment.Capture(facility, destination);
        ProductionOutputDestinationLifecycleContribution apparelSnapshot =
            apparel.Capture(facility, destination);
        Require(equipmentSnapshot.ActiveRecordCount == 2
            && equipmentSnapshot.Blocks.Count == 2
            && equipmentSnapshot.Blocks.Any(value => value.Code ==
                ProductionOutputLifecycleBlockCode.EquipmentCraftOrder)
            && equipmentSnapshot.Blocks.Any(value => value.Code ==
                ProductionOutputLifecycleBlockCode.EquipmentRepairOrder),
            "Combined equipment crafting/repair orders did not block the facility lifecycle.");
        Require(apparelSnapshot.Blocks.Count == 1
            && apparelSnapshot.Blocks[0].Code ==
                ProductionOutputLifecycleBlockCode.ApparelWorkOrder,
            "Apparel work order did not block the facility lifecycle.");

        CombatEquipmentCraftOrderSaveData equipmentCommitChanged =
            equipmentOrder.Clone();
        equipmentCommitChanged.materialTransferCommitId = "commit:equipment:qa";
        ProductionOutputDestinationLifecycleContribution changedEquipment =
            new CombatEquipmentCraftLifecycleContributor(
                () => new FixedEquipmentQueue(equipmentCommitChanged),
                () => new FixedMaintenanceOrders(repairOrder))
            .Capture(facility, destination);
        Require(
            equipmentSnapshot.DurableSemanticFingerprint
                != changedEquipment.DurableSemanticFingerprint,
            "Equipment durable lifecycle ignored persisted material-transfer provenance.");

        ApparelWorkOrderSaveData apparelCommitChanged =
            JsonUtility.FromJson<ApparelWorkOrderSaveData>(
                JsonUtility.ToJson(apparelOrder));
        apparelCommitChanged.repairInputMassGrams = 501L;
        ProductionOutputDestinationLifecycleContribution changedApparel =
            new ApparelWorkOrderLifecycleContributor(
                new FixedApparelOrders(apparelCommitChanged))
            .Capture(facility, destination);
        Require(
            apparelSnapshot.DurableSemanticFingerprint
                != changedApparel.DurableSemanticFingerprint,
            "Apparel durable lifecycle ignored persisted repair provenance.");
    }

    private static void VerifyCombinedOrderingAndFingerprintAreDeterministic()
    {
        BuildingInstanceId facility = (BuildingInstanceId)"building:qa:combined";
        FakeContributor emptyAuthority = new(
            "z-empty-authority",
            hasAuthority: true,
            Array.Empty<ProductionOutputLifecycleBlock>());
        FakeContributor physical = new(
            "a-physical",
            hasAuthority: true,
            new[]
            {
                new ProductionOutputLifecycleBlock(
                    ProductionOutputLifecycleBlockCode.CarriedPhysicalMass,
                    0,
                    500L)
            });
        ProductionOutputDestinationLifecycleQuery left = new(
            new IProductionOutputDestinationLifecycleContributor[]
            {
                emptyAuthority,
                physical
            });
        ProductionOutputDestinationLifecycleQuery right = new(
            new IProductionOutputDestinationLifecycleContributor[]
            {
                physical,
                emptyAuthority
            });
        ProductionOutputDestinationLifecycleSnapshot first = left.Capture(facility);
        ProductionOutputDestinationLifecycleSnapshot second = right.Capture(facility);
        Require(first.HasAnyAuthority && !first.CanRevokeEmpty,
            "Combined lifecycle ignored an active physical blocker.");
        Require(first.Contributions[0].ContributorId == "a-physical"
            && first.Contributions[1].ContributorId == "z-empty-authority",
            "Combined lifecycle contributor order is not ordinal deterministic.");
        Require(first.SemanticFingerprint == second.SemanticFingerprint,
            "Contributor insertion order changed the semantic fingerprint.");

        ProductionOutputDestinationLifecycleQuery drained = new(
            new IProductionOutputDestinationLifecycleContributor[] { emptyAuthority });
        ProductionOutputDestinationLifecycleSnapshot drainedSnapshot = drained.Capture(facility);
        Require(drainedSnapshot.HasAnyAuthority && drainedSnapshot.CanRevokeEmpty,
            "A drained positive authority could not be distinguished from active ownership.");
    }

    private static void VerifyDuplicateContributorFailsLoud()
    {
        bool threw = false;
        try
        {
            _ = new ProductionOutputDestinationLifecycleQuery(
                new IProductionOutputDestinationLifecycleContributor[]
                {
                    new FakeContributor("duplicate", false, Array.Empty<ProductionOutputLifecycleBlock>()),
                    new FakeContributor("duplicate", false, Array.Empty<ProductionOutputLifecycleBlock>())
                });
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Require(threw, "Duplicate lifecycle contributors were accepted.");
    }

    private static void VerifyDurableFingerprintIgnoresVolatileRevision()
    {
        BuildingInstanceId facility =
            (BuildingInstanceId)"building:qa:durable-lifecycle";
        ProductionOutputLifecycleBlock[] blocks =
        {
            new(
                ProductionOutputLifecycleBlockCode.GenericBill,
                1,
                0L)
        };
        ProductionOutputDestinationLifecycleSnapshot first =
            new ProductionOutputDestinationLifecycleQuery(
                new IProductionOutputDestinationLifecycleContributor[]
                {
                    new FakeContributor(
                        "durable-revision-fixture",
                        true,
                        blocks,
                        authorityRevision: 1L,
                        semanticSeed: "volatile:1",
                        durableSeed: "durable:same")
                }).Capture(facility);
        ProductionOutputDestinationLifecycleSnapshot second =
            new ProductionOutputDestinationLifecycleQuery(
                new IProductionOutputDestinationLifecycleContributor[]
                {
                    new FakeContributor(
                        "durable-revision-fixture",
                        true,
                        blocks,
                        authorityRevision: 2L,
                        semanticSeed: "volatile:2",
                        durableSeed: "durable:same")
                }).Capture(facility);
        Require(
            first.SemanticFingerprint != second.SemanticFingerprint
                && first.DurableSemanticFingerprint
                    == second.DurableSemanticFingerprint,
            "durable lifecycle fingerprint retained volatile authority revision");
    }

    private static void VerifyDetachedDurableSaveProjector()
    {
        BuildingInstanceId facility =
            (BuildingInstanceId)"building:qa:detached-projector";
        CombatEquipmentCraftOrderSaveData equipmentOrder = new()
        {
            orderId = "equipment-order:detached",
            definitionId = "equipment:qa",
            materialId = "material:iron",
            facilityPersistentId = facility.Value,
            materialTransferCommitId = "commit:equipment:detached"
        };
        CombatEquipmentCraftLifecycleContributor equipment = new(
            () => new FixedEquipmentQueue(equipmentOrder),
            () => new FixedMaintenanceOrders());
        string liveEquipment = equipment.Capture(
            facility,
            ProductionOutputDestinationId.FromFacility(facility))
            .DurableSemanticFingerprint;
        string savedEquipment =
            ProductionOutputDestinationDurableSaveProjector.ProjectEquipment(
                facility,
                new DungeonCombatEquipmentSaveData
                {
                    craftOrders = new List<CombatEquipmentCraftOrderSaveData>
                    {
                        equipmentOrder
                    }
                },
                new CombatEquipmentMaintenanceSaveData());
        Require(liveEquipment == savedEquipment,
            "Detached equipment projector drifted from the live contributor.");

        ApparelWorkOrderSaveData apparelOrder = new()
        {
            orderId = "apparel-order:detached",
            kind = ApparelWorkOrderKind.Repair,
            state = ApparelWorkOrderState.WaitingForDispositionFinalization,
            facilityInstanceId = facility.Value,
            repairInputMassGrams = 700L,
            repairCommitId = "commit:apparel:detached"
        };
        ApparelWorkOrderLifecycleContributor apparel = new(
            new FixedApparelOrders(apparelOrder));
        string liveApparel = apparel.Capture(
            facility,
            ProductionOutputDestinationId.FromFacility(facility))
            .DurableSemanticFingerprint;
        string savedApparel =
            ProductionOutputDestinationDurableSaveProjector.ProjectApparel(
                facility,
                new DungeonCharacterEnvironmentSaveData
                {
                    apparelWorkOrders = new[] { apparelOrder },
                    apparelWorkOrderTerminalStates =
                        Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
                });
        Require(liveApparel == savedApparel,
            "Detached apparel projector drifted from the live contributor.");

        ProductionBillSaveData billA = new()
        {
            billId = "production-bill:detached:0002",
            buildingInstanceId = facility.Value,
            recipeId = "recipe:qa:b",
            wipInputCommitId = "commit:wip:b"
        };
        ProductionBillSaveData billB = new()
        {
            billId = "production-bill:detached:0001",
            buildingInstanceId = facility.Value,
            recipeId = "recipe:qa:a",
            wipInputCommitId = "commit:wip:a"
        };
        string ordered =
            ProductionOutputDestinationDurableSaveProjector.ProjectGenericBills(
                facility,
                new DungeonProductionBillSaveData
                {
                    bills = new List<ProductionBillSaveData> { billA, billB }
                });
        string shuffled =
            ProductionOutputDestinationDurableSaveProjector.ProjectGenericBills(
                facility,
                new DungeonProductionBillSaveData
                {
                    bills = new List<ProductionBillSaveData> { billB, billA }
                });
        Require(ordered == shuffled,
            "Detached generic-bill projector depends on save collection order.");
        billB.wipInputCommitId = "commit:wip:a:changed";
        string changed =
            ProductionOutputDestinationDurableSaveProjector.ProjectGenericBills(
                facility,
                new DungeonProductionBillSaveData
                {
                    bills = new List<ProductionBillSaveData> { billA, billB }
                });
        Require(ordered != changed,
            "Detached generic-bill projector ignored persisted WIP provenance.");

        string destination = ProductionOutputDestinationId
            .FromFacility(facility).Value;
        WorldItemStackSaveData stackA = new()
        {
            stackId = "stack:detached:0002",
            itemId = "item:qa:b",
            quantity = 2,
            state = WorldItemStackState.FacilityOutputBuffer,
            destinationId = destination
        };
        WorldItemStackSaveData stackB = new()
        {
            stackId = "stack:detached:0001",
            itemId = "item:qa:a",
            quantity = 1,
            state = WorldItemStackState.FacilityOutputBuffer,
            destinationId = destination
        };
        DungeonCharacterWorldSaveData noCarriers = new();
        string physicalOrdered =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectPhysicalCustody(
                    facility,
                    new DungeonPhysicalItemSaveData
                    {
                        stacks = new List<WorldItemStackSaveData>
                        {
                            stackA,
                            stackB
                        }
                    },
                    noCarriers);
        string physicalShuffled =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectPhysicalCustody(
                    facility,
                    new DungeonPhysicalItemSaveData
                    {
                        stacks = new List<WorldItemStackSaveData>
                        {
                            stackB,
                            stackA
                        }
                    },
                    noCarriers);
        Require(physicalOrdered == physicalShuffled,
            "Detached physical-custody projector depends on stack order.");
        stackB.recoveryOwnerOperationId = "haul-recovery:detached";
        string physicalChanged =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectPhysicalCustody(
                    facility,
                    new DungeonPhysicalItemSaveData
                    {
                        stacks = new List<WorldItemStackSaveData>
                        {
                            stackA,
                            stackB
                        }
                    },
                    noCarriers);
        Require(physicalOrdered != physicalChanged,
            "Detached physical-custody projector ignored recovery provenance.");

        ProductionPreparedOutputRouteOperationSaveData routeA = new()
        {
            routeOperationId = "route-operation:detached:0002",
            requestFingerprint = new string('a', 64),
            physicalReceiptFingerprint = new string('b', 64),
            phase = ProductionPreparedOutputRoutePhase.PhysicalPending,
            routedQuantity = 1,
            routedMassGrams = 300L,
            deliveryRevisions = new List<ProductionPreparedOutputDeliveryRevisionSaveData>
            {
                new()
                {
                    revision = 1,
                    revisionFingerprint = new string('c', 64),
                    originalPhysicalReceiptFingerprint = new string('b', 64)
                },
                new()
                {
                    revision = 0,
                    revisionFingerprint = new string('d', 64),
                    originalPhysicalReceiptFingerprint = new string('b', 64)
                }
            }
        };
        ProductionPreparedOutputRouteOperationSaveData routeB = new()
        {
            routeOperationId = "route-operation:detached:0001",
            requestFingerprint = new string('e', 64),
            physicalReceiptFingerprint = new string('f', 64),
            phase = ProductionPreparedOutputRoutePhase.ItemsAcknowledgedAwaitingCheckpointGc,
            routedQuantity = 2,
            routedMassGrams = 700L
        };
        ProductionPreparedOutputRoutingLineSaveData routingLine = new()
        {
            batchCommitId = "batch:detached",
            lineCommitId = "line:detached",
            outputLineId = "output:detached",
            itemId = "item:qa:routed",
            destinationId = destination,
            componentFingerprint = new string('9', 64),
            outputCapabilityId =
                ProductionOutputCapabilityIds.StandardDefinition,
            outputCapabilityVersion =
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
            outputComponentCodecId =
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
            outputComponentCodecVersion =
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
            outputCapabilityFingerprint =
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    "output:detached",
                    "item:qa:routed",
                    ProductionOutputCapabilityIds.StandardDefinition,
                    ProductionOutputCapabilityIds.StandardDefinitionVersion,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
            originalQuantity = 3,
            routedQuantity = 3,
            originalMassGrams = 1000L,
            routedMassGrams = 1000L,
            routeOperations = new List<ProductionPreparedOutputRouteOperationSaveData>
            {
                routeA,
                routeB
            }
        };
        ProductionPreparedOutputRoutingBatchSaveData routingBatch = new()
        {
            batchCommitId = "batch:detached",
            ownerBillId = "bill:detached",
            ownerRecipeId = "recipe:detached",
            ownerFacilityId = facility.Value,
            destinationId = destination,
            capacitySourceDigest = new string('2', 64),
            outputBufferCycleCapacity = 4,
            projectedPortfolioCapacityGrams = 4_000L,
            requiredMinimumCapacityGrams = 4_000L,
            lines = new List<ProductionPreparedOutputRoutingLineSaveData>
            {
                routingLine
            }
        };
        FacilityOutputExactRouteOutboxSaveData outbox = new()
        {
            phase = FacilityOutputExactRoutePhase.Routable,
            routeOperationId = "route-operation:detached:0001",
            requestFingerprint = new string('e', 64),
            physicalReceiptFingerprint = new string('f', 64),
            batchCommitId = "batch:detached",
            sourceDestinationId = destination,
            totalQuantity = 2,
            totalMassGrams = 700L,
            currentDeliveryRevisionFingerprint = new string('1', 64),
            slices = new List<FacilityOutputExactRouteSliceSaveData>
            {
                new()
                {
                    sourceStackId = "stack:source:0002",
                    routedStackId = "stack:routed:0002",
                    outputLineId = "output:detached",
                    lineCommitId = "line:detached",
                    itemId = "item:qa:routed",
                    sourceOffsetQuantity = 1,
                    routedOffsetQuantity = 1,
                    routedQuantity = 1,
                    routedMassGrams = 350L
                },
                new()
                {
                    sourceStackId = "stack:source:0001",
                    routedStackId = "stack:routed:0001",
                    outputLineId = "output:detached",
                    lineCommitId = "line:detached",
                    itemId = "item:qa:routed",
                    sourceOffsetQuantity = 0,
                    routedOffsetQuantity = 0,
                    routedQuantity = 1,
                    routedMassGrams = 350L
                }
            }
        };
        string routingOrdered =
            ProductionOutputDestinationDurableSaveProjector.ProjectRoutingOutbox(
                facility,
                new ProductionPreparedOutputRoutingSaveData
                {
                    batches = new List<ProductionPreparedOutputRoutingBatchSaveData>
                    {
                        routingBatch
                    }
                },
                new[] { outbox });
        routingLine.routeOperations.Reverse();
        routeA.deliveryRevisions.Reverse();
        outbox.slices.Reverse();
        string routingShuffled =
            ProductionOutputDestinationDurableSaveProjector.ProjectRoutingOutbox(
                facility,
                new ProductionPreparedOutputRoutingSaveData
                {
                    batches = new List<ProductionPreparedOutputRoutingBatchSaveData>
                    {
                        routingBatch
                    }
                },
                new[] { outbox });
        Require(routingOrdered == routingShuffled,
            "Detached routing/outbox projector depends on nested save order.");
        routeB.routedMassGrams++;
        string routingChanged =
            ProductionOutputDestinationDurableSaveProjector.ProjectRoutingOutbox(
                facility,
                new ProductionPreparedOutputRoutingSaveData
                {
                    batches = new List<ProductionPreparedOutputRoutingBatchSaveData>
                    {
                        routingBatch
                    }
                },
                new[] { outbox });
        Require(routingOrdered != routingChanged,
            "Detached routing/outbox projector ignored persisted route provenance.");
    }

    private static void VerifyCombinedCombatRepairProjectionContracts()
    {
        BuildingInstanceId facility =
            (BuildingInstanceId)"building:qa:combined-combat-repair";
        DungeonCombatEquipmentSaveData combat = new()
        {
            craftOrders = new List<CombatEquipmentCraftOrderSaveData>
            {
                new()
                {
                    orderId = "craft:z",
                    facilityPersistentId = facility.Value
                },
                new()
                {
                    orderId = "craft:a",
                    facilityPersistentId = facility.Value
                }
            }
        };
        CombatEquipmentMaintenanceSaveData maintenance = new()
        {
            orders = new List<CombatEquipmentRepairOrder>
            {
                new()
                {
                    orderId = "repair:z",
                    facilityBuildingId = facility.Value,
                    state = CombatEquipmentRepairOrderState.InProgress
                },
                new()
                {
                    orderId = "repair:a",
                    facilityBuildingId = facility.Value,
                    state = CombatEquipmentRepairOrderState.Ready
                }
            }
        };
        string first = ProductionOutputDestinationDurableSaveProjector
            .ProjectEquipment(facility, combat, maintenance);
        combat.craftOrders.Reverse();
        maintenance.orders.Reverse();
        string shuffled = ProductionOutputDestinationDurableSaveProjector
            .ProjectEquipment(facility, combat, maintenance);
        Require(string.Equals(first, shuffled, StringComparison.Ordinal),
            "Combined combat/repair projection depends on source insertion order.");

        CombatEquipmentMaintenanceSaveData missing = new() { orders = null };
        RequireThrows(
            () => ProductionOutputDestinationDurableSaveProjector
                .ProjectEquipment(facility, combat, missing),
            "repair-order collection");

        CombatEquipmentMaintenanceSaveData duplicate = new()
        {
            orders = new List<CombatEquipmentRepairOrder>
            {
                maintenance.orders[0].Clone(),
                maintenance.orders[0].Clone()
            }
        };
        RequireThrows(
            () => ProductionOutputDestinationDurableSaveProjector
                .ProjectEquipment(facility, combat, duplicate),
            "Duplicate equipment maintenance repair order identity");
    }

    private static void VerifyApparelCraftAttemptOperationIdentity()
    {
        ApparelWorkOrderSaveData order = new()
        {
            orderId = "apparel-order:attempt-identity",
            kind = ApparelWorkOrderKind.Craft,
            qualityAttemptIndex = 0
        };
        string first = ApparelWorkOrderRuntime.BuildCraftMaterialOperationId(
            order);
        order.qualityAttemptIndex = 1;
        string second = ApparelWorkOrderRuntime.BuildCraftMaterialOperationId(
            order);
        Require(
            first == "apparel-craft-material:apparel-order:attempt-identity:0000"
                && second ==
                    "apparel-craft-material:apparel-order:attempt-identity:0001"
                && first != second,
            "Apparel craft retries reused material-disposition authority.");
    }

    private static void VerifyMutationEpochOwnership()
    {
        BuildingInstanceId facility = (BuildingInstanceId)"building:qa:epoch";
        ProductionFacilityMutationEpochRuntime epoch = new();
        Require(epoch.TryBegin(facility, "mutation:qa:one", out long token, out string failure),
            "Mutation epoch did not begin: " + failure);
        Require(epoch.IsFrozen(facility)
            && epoch.IsCurrent(facility, "mutation:qa:one", token),
            "Mutation epoch did not freeze the exact facility owner.");
        Require(!epoch.TryBegin(facility, "mutation:qa:two", out _, out string conflict)
            && conflict == "production-facility-mutation-owned-by-other",
            "Concurrent mutation owner was not rejected.");
        Require(!epoch.TryEnd(facility, "mutation:qa:two", token, out _),
            "Wrong mutation owner ended an epoch.");
        Require(epoch.TryEnd(facility, "mutation:qa:one", token, out failure)
            && !epoch.IsFrozen(facility),
            "Exact mutation owner did not end its epoch: " + failure);
    }

    private static void VerifyEmptyFenceRejectsStaleCandidate()
    {
        using FenceFixture fixture = new("building:qa:fence-stale", 4_200L);
        Require(fixture.Fence.TryPrepareEmpty(
                fixture.Building,
                ProductionFacilityMutationKind.Demolition,
                "mutation:qa:stale",
                out ProductionFacilityEmptyMutationCandidate candidate,
                out string failure),
            "Empty fence candidate did not prepare: " + failure);
        fixture.Lifecycle.Revision++;
        Require(!fixture.Fence.TryCommitAuthorityRevoke(candidate, out string stale)
            && stale.StartsWith(
                "production-facility-mutation-candidate-stale:",
                StringComparison.Ordinal),
            "Changed lifecycle fingerprint did not invalidate the candidate.");
        Require(fixture.Fence.TryAbort(candidate, out failure)
            && !fixture.Epoch.IsFrozen(fixture.FacilityId)
            && fixture.Authority.RevokeCount == 0,
            "Stale candidate did not abort without revoking authority: " + failure);
    }

    private static void VerifyEmptyFenceRollbackRestoresExactAuthority()
    {
        using FenceFixture fixture = new("building:qa:fence-rollback", 7_700L);
        Require(fixture.Fence.TryPrepareEmpty(
                fixture.Building,
                ProductionFacilityMutationKind.Demolition,
                "mutation:qa:rollback",
                out ProductionFacilityEmptyMutationCandidate candidate,
                out string failure),
            "Rollback candidate did not prepare: " + failure);
        Require(fixture.Fence.TryCommitAuthorityRevoke(candidate, out failure)
            && candidate.AuthorityRevoked
            && !fixture.Lifecycle.HasAuthority,
            "Rollback candidate did not revoke authority: " + failure);
        Require(fixture.Fence.TryAbort(candidate, out failure)
            && fixture.Lifecycle.HasAuthority
            && fixture.Authority.LastEnsuredCapacity == 7_700L
            && fixture.Authority.EnsureCount == 1
            && !fixture.Epoch.IsFrozen(fixture.FacilityId),
            "Rollback did not restore the exact prior authority: " + failure);
    }

    private static void VerifyEmptyFenceCommitClosesWithoutRestore()
    {
        using FenceFixture fixture = new("building:qa:fence-commit", 5_500L);
        Require(fixture.Fence.TryPrepareEmpty(
                fixture.Building,
                ProductionFacilityMutationKind.Demolition,
                "mutation:qa:commit",
                out ProductionFacilityEmptyMutationCandidate candidate,
                out string failure)
            && fixture.Fence.TryCommitAuthorityRevoke(candidate, out failure)
            && fixture.Fence.TryComplete(candidate, out failure),
            "Empty authority commit did not complete: " + failure);
        Require(candidate.IsClosed
            && !fixture.Lifecycle.HasAuthority
            && fixture.Authority.RevokeCount == 1
            && fixture.Authority.EnsureCount == 0
            && !fixture.Epoch.IsFrozen(fixture.FacilityId),
            "Committed demolition restored or leaked authority/epoch state.");
    }

    private static void VerifyStockSensorFenceRollbackRestoresExactAuthority()
    {
        using FenceFixture fixture = new(
            "building:qa:fence-sensor-rollback",
            5_500L,
            stockSensorCapable: true);
        Require(fixture.Fence.TryPrepareEmpty(
                fixture.Building,
                ProductionFacilityMutationKind.Demolition,
                "mutation:qa:sensor-rollback",
                out ProductionFacilityEmptyMutationCandidate candidate,
                out string failure),
            "Sensor rollback candidate did not prepare: " + failure);
        Require(fixture.Fence.TryCommitAuthorityRevoke(candidate, out failure)
            && candidate.AuthorityRevoked
            && candidate.StockSensorAuthorityRevoked
            && !fixture.Lifecycle.HasAuthority
            && !fixture.SensorAuthority.HasAuthority,
            "Sensor rollback candidate did not revoke both authorities: " + failure);
        Require(fixture.Fence.TryAbort(candidate, out failure)
            && fixture.Lifecycle.HasAuthority
            && fixture.SensorAuthority.HasAuthority
            && fixture.SensorAuthority.CapacityMassGrams == 1_150L
            && !fixture.Epoch.IsFrozen(fixture.FacilityId),
            "Sensor rollback did not restore both exact authorities: " + failure);
    }

    private static void VerifyStockSensorActiveStateBlocksMutation()
    {
        using FenceFixture fixture = new(
            "building:qa:fence-sensor-state",
            0L,
            stockSensorCapable: true);
        fixture.StockSensors.HasPhysicalState = true;
        Require(!fixture.Fence.TryPrepareEmpty(
                fixture.Building,
                ProductionFacilityMutationKind.Demolition,
                "mutation:qa:sensor-state",
                out _,
                out string prepareFailure)
            && prepareFailure.Contains(
                "stock-sensor-state-active",
                StringComparison.Ordinal),
            "Installed/pending sensor state did not block demolition.");
        Require(!fixture.Fence.TryRequireNoAuthority(
                fixture.Building,
                ProductionFacilityMutationKind.Relocation,
                out string relocationFailure)
            && relocationFailure.Contains(
                "stock-sensor-state-active",
                StringComparison.Ordinal),
            "Installed/pending sensor state did not block identity mutation.");
    }

    private static void VerifyStockSensorPostPrepareOccupancyBlocksCommit()
    {
        using FenceFixture fixture = new(
            "building:qa:fence-sensor-race",
            4_200L,
            stockSensorCapable: true);
        Require(fixture.Fence.TryPrepareEmpty(
                fixture.Building,
                ProductionFacilityMutationKind.Demolition,
                "mutation:qa:sensor-race",
                out ProductionFacilityEmptyMutationCandidate candidate,
                out string failure),
            "Sensor occupancy-race candidate did not prepare: " + failure);
        fixture.SensorAuthority.IsEmpty = false;
        Require(!fixture.Fence.TryCommitAuthorityRevoke(candidate, out failure)
            && fixture.SensorAuthority.HasAuthority
            && fixture.Lifecycle.HasAuthority,
            "Post-prepare sensor occupancy partially revoked authority: " + failure);
        fixture.SensorAuthority.IsEmpty = true;
        Require(fixture.Fence.TryAbort(candidate, out failure),
            "Post-prepare sensor occupancy candidate did not abort: " + failure);
    }

    private static void VerifyStockSensorRollbackWhenOutputRevokeFails()
    {
        using FenceFixture fixture = new(
            "building:qa:fence-sensor-output-failure",
            6_400L,
            stockSensorCapable: true);
        Require(fixture.Fence.TryPrepareEmpty(
                fixture.Building,
                ProductionFacilityMutationKind.Demolition,
                "mutation:qa:sensor-output-failure",
                out ProductionFacilityEmptyMutationCandidate candidate,
                out string failure),
            "Sensor/output rollback candidate did not prepare: " + failure);
        fixture.Authority.FailRevoke = true;
        Require(!fixture.Fence.TryCommitAuthorityRevoke(candidate, out failure)
            && fixture.SensorAuthority.HasAuthority
            && !candidate.StockSensorAuthorityRevoked
            && fixture.Lifecycle.HasAuthority,
            "Output revoke failure did not restore the exact sensor authority: "
                + failure);
        fixture.Authority.FailRevoke = false;
        Require(fixture.Fence.TryAbort(candidate, out failure),
            "Sensor/output failure candidate did not abort: " + failure);
    }

    private static void VerifyPlacementDemolitionUsesFenceInOrder()
    {
        BuildingSO definition = CreateDemolitionDefinition(-9191);
        Grid grid = new Grid(4, 1);
        BuildableObject building = CreateRegisteredBuilding(
            grid,
            definition,
            (BuildingInstanceId)"building:qa:placement-commit",
            new Vector2Int(1, 0));
        GameObject buildingRoot = building.gameObject;
        try
        {
            RecordingDestructiveLossRuntime loss = new();
            RecordingBuildingFactory factory = new();
            GridBuildingPlacementService placement = new(
                grid,
                null,
                id => id == definition.id ? definition : null,
                factory,
                new BuildingPlacementValidator(),
                workOrderRuntime: null,
                onConstructionSiteCreated: null,
                warehouseLifecycle: null,
                destructiveLoss: loss);
            Require(placement.TryDestroyBuilding(building, out _, out string failure),
                "Fenced placement demolition failed: " + failure);
            Require(loss.Causes.SequenceEqual(new[]
                {
                    ProductionFacilityDestructiveDrainCause.ExplicitDemolition
                })
                && factory.DeleteCount == 0
                && grid.GetGridCell(new Vector2Int(1, 0)).GetOccupant(GridLayer.Building) == null,
                "Placement demolition did not use the typed durable drain facade.");
        }
        finally
        {
            if (buildingRoot != null)
                UnityEngine.Object.DestroyImmediate(buildingRoot);
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void VerifyPlacementDemolitionRollsBackWhenGridRemovalFails()
    {
        BuildingSO definition = CreateDemolitionDefinition(-9192);
        Grid grid = new Grid(4, 1);
        Vector2Int position = new(2, 0);
        BuildableObject building = CreateRegisteredBuilding(
            grid,
            definition,
            (BuildingInstanceId)"building:qa:placement-rollback",
            position);
        try
        {
            RecordingDestructiveLossRuntime loss = new()
            {
                Disposition = BuildingDestructiveLossDisposition.DeferredAccepted,
                FailureReason = "recording-world-removal-deferred"
            };
            RecordingBuildingFactory factory = new();
            GridBuildingPlacementService placement = new(
                grid,
                null,
                id => id == definition.id ? definition : null,
                factory,
                new BuildingPlacementValidator(),
                workOrderRuntime: null,
                onConstructionSiteCreated: null,
                warehouseLifecycle: null,
                destructiveLoss: loss);
            Require(!placement.TryDestroyBuilding(building, out _, out string failure)
                && failure.StartsWith("철거 회수·정리 작업이 진행 중", StringComparison.Ordinal),
                "Grid-removal failure did not fail the demolition transaction.");
            Require(loss.Causes.SequenceEqual(new[]
                {
                    ProductionFacilityDestructiveDrainCause.ExplicitDemolition
                })
                && factory.DeleteCount == 0
                && !building.isDestroy
                && ReferenceEquals(
                    grid.GetGridCell(position).GetOccupant(GridLayer.Building),
                    building),
                "Grid-removal failure destroyed the building or skipped authority rollback.");
        }
        finally
        {
            if (building != null)
                UnityEngine.Object.DestroyImmediate(building.gameObject);
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void VerifyIdentityChangingMutationsRequireNoAuthority()
    {
        using FenceFixture fixture = new("building:qa:identity-change", 2_400L);
        ProductionFacilityMutationKind[] kinds =
        {
            ProductionFacilityMutationKind.Relocation,
            ProductionFacilityMutationKind.Synthesis,
            ProductionFacilityMutationKind.Evolution
        };
        for (int i = 0; i < kinds.Length; i++)
        {
            Require(!fixture.Fence.TryRequireNoAuthority(
                    fixture.Building,
                    kinds[i],
                    out string failure)
                && failure.Contains("requires-no-output-authority"),
                kinds[i] + " accepted a live production output authority.");
        }
        fixture.Lifecycle.HasAuthority = false;
        fixture.Lifecycle.Revision++;
        for (int i = 0; i < kinds.Length; i++)
        {
            Require(fixture.Fence.TryRequireNoAuthority(
                    fixture.Building,
                    kinds[i],
                    out string failure),
                kinds[i] + " rejected a facility with no lifecycle authority: " + failure);
        }
    }

    private static void VerifyStructuralAndCoverLossUseTheSameFence()
    {
        GameObject textureObject = new("DestructiveLossTexture");
        GridTexture texture = textureObject.AddComponent<GridTexture>();
        BuildingSO structuralDefinition = null;
        BuildableObject structuralBuilding = null;
        GameObject structuralRoot = null;
        BuildingSO coverDefinition = null;
        BuildableObject coverBuilding = null;
        GameObject coverRoot = null;
        BuildingSO zeroCoverDefinition = null;
        BuildableObject zeroCoverBuilding = null;
        GameObject zeroCoverRoot = null;
        try
        {
            structuralDefinition = CreateDemolitionDefinition(-9193);
            Grid structuralGrid = new(4, 1);
            Vector2Int structuralPosition = new(1, 0);
            structuralBuilding = CreateRegisteredBuilding(
                structuralGrid,
                structuralDefinition,
                (BuildingInstanceId)"building:qa:structural-loss",
                structuralPosition);
            structuralRoot = structuralBuilding.gameObject;
            BuildingStructuralIntegrity.Ensure(
                structuralBuilding,
                new BuildingStructuralIntegrityAbility
                {
                    maxHitPoints = 100f,
                    toughness = 0f,
                    repairHitPointsPerWork = 1f,
                    breachable = true
                });
            RecordingDestructiveLossRuntime structuralLoss = new();
            BuildingStructuralDamageResult structuralResult =
                new BuildingStructuralIntegrityRuntime(structuralLoss)
                    .ApplyDamage(structuralBuilding, 100f);
            Require(structuralResult.Applied
                && structuralResult.Destroyed
                && structuralLoss.Causes.SequenceEqual(new[]
                    {
                        ProductionFacilityDestructiveDrainCause
                            .StructuralIntegrity
                    })
                && structuralGrid.GetGridCell(structuralPosition)
                    .GetOccupant(GridLayer.Building) == null,
                "Structural lethal loss bypassed the common empty mutation fence.");
            coverDefinition = CreateDemolitionDefinition(-9194);
            Grid coverGrid = new(4, 1);
            Vector2Int coverPosition = new(2, 0);
            coverBuilding = CreateRegisteredBuilding(
                coverGrid,
                coverDefinition,
                (BuildingInstanceId)"building:qa:cover-loss",
                coverPosition);
            coverRoot = coverBuilding.gameObject;
            RecordingDestructiveLossRuntime coverLoss = new();
            CombatCoverDurabilityRegistry coverRegistry = new(coverLoss);
            CombatCoverDurability durability = CombatCoverDurability.Ensure(
                coverBuilding,
                new BuildingCoverAbility
                {
                    coverHitPoints = 80f
                },
                coverRegistry);
            Require(coverRegistry.TryApplyDamage(durability.SourceId, 80f)
                && coverLoss.Causes.SequenceEqual(new[]
                    {
                        ProductionFacilityDestructiveDrainCause.CombatCover
                    })
                && coverGrid.GetGridCell(coverPosition)
                    .GetOccupant(GridLayer.Building) == null,
                "Cover lethal loss bypassed the common empty mutation fence.");
            zeroCoverDefinition = CreateDemolitionDefinition(-9196);
            Grid zeroCoverGrid = new(4, 1);
            zeroCoverBuilding = CreateRegisteredBuilding(
                zeroCoverGrid,
                zeroCoverDefinition,
                (BuildingInstanceId)"building:qa:zero-cover-restore",
                new Vector2Int(3, 0));
            zeroCoverRoot = zeroCoverBuilding.gameObject;
            RecordingDestructiveLossRuntime zeroCoverLoss = new();
            CombatCoverDurabilityRegistry zeroCoverRegistry = new(zeroCoverLoss);
            CombatCoverDurability zeroCover = CombatCoverDurability.Ensure(
                zeroCoverBuilding,
                new BuildingCoverAbility { coverHitPoints = 50f },
                zeroCoverRegistry);
            string zeroCoverId = zeroCover.SourceId;
            Require(zeroCover.TryRestoreState(
                    zeroCover.CurrentVersion,
                    "{\"currentHitPoints\":0}",
                    out string restoreFailure),
                "Zero-HP cover restore failed: " + restoreFailure);
            zeroCoverBuilding.gameObject.SetActive(false);
            zeroCoverBuilding.gameObject.SetActive(true);
            Require(!zeroCoverRegistry.TryApplyDamage(zeroCoverId, 1f)
                && zeroCoverLoss.Causes.Count == 0,
                "A restored zero-HP cover re-registered as a live combat target.");
        }
        finally
        {
            if (zeroCoverRoot != null)
                UnityEngine.Object.DestroyImmediate(zeroCoverRoot);
            UnityEngine.Object.DestroyImmediate(zeroCoverDefinition);
            if (coverRoot != null)
                UnityEngine.Object.DestroyImmediate(coverRoot);
            UnityEngine.Object.DestroyImmediate(coverDefinition);
            if (structuralRoot != null)
                UnityEngine.Object.DestroyImmediate(structuralRoot);
            UnityEngine.Object.DestroyImmediate(structuralDefinition);
            UnityEngine.Object.DestroyImmediate(textureObject);
        }
    }

    private static void VerifyDestructiveLossBlockAndRollbackPreserveWorld()
    {
        GameObject textureObject = new("DestructiveLossRollbackTexture");
        GridTexture texture = textureObject.AddComponent<GridTexture>();
        BuildingSO definition = CreateDemolitionDefinition(-9195);
        Grid grid = new(4, 1);
        Vector2Int position = new(1, 0);
        BuildableObject building = CreateRegisteredBuilding(
            grid,
            definition,
            (BuildingInstanceId)"building:qa:destructive-rollback",
            position);
        BuildingStructuralIntegrity integrity = BuildingStructuralIntegrity.Ensure(
            building,
            new BuildingStructuralIntegrityAbility
            {
                maxHitPoints = 100f,
                toughness = 0f,
                repairHitPointsPerWork = 1f,
                breachable = true
            });
        try
        {
            RecordingDestructiveLossRuntime blockedLoss = new()
            {
                Disposition = BuildingDestructiveLossDisposition.Conflict,
                FailureReason = "recording-destructive-loss-blocked"
            };
            BuildingStructuralDamageResult blocked =
                new BuildingStructuralIntegrityRuntime(blockedLoss)
                    .ApplyDamage(building, 100f);
            Require(!blocked.Applied
                && !blocked.Destroyed
                && Mathf.Approximately(integrity.CurrentHitPoints, 100f)
                && ReferenceEquals(
                    grid.GetGridCell(position).GetOccupant(GridLayer.Building),
                    building),
                "Blocked destructive loss mutated HP or world authority.");

            RecordingDestructiveLossRuntime rollbackLoss = new()
            {
                Disposition = BuildingDestructiveLossDisposition.DeferredAccepted,
                FailureReason = "recording-world-removal-deferred"
            };
            BuildingDestructiveLossResult rolledBack = rollbackLoss.Apply(
                building,
                ProductionFacilityDestructiveDrainCause.StructuralIntegrity);
            Require(rolledBack.Disposition ==
                    BuildingDestructiveLossDisposition.DeferredAccepted
                && rollbackLoss.Causes.SequenceEqual(new[]
                    {
                        ProductionFacilityDestructiveDrainCause
                            .StructuralIntegrity
                    })
                && !building.isDestroy
                && ReferenceEquals(
                    grid.GetGridCell(position).GetOccupant(GridLayer.Building),
                    building),
                "Grid-removal failure did not preserve the destructive-loss world.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(building.gameObject);
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(textureObject);
        }
    }

    private static void VerifyWorldRemovalPortIsExactAndIdempotent()
    {
        GameObject textureObject = new("DestructiveWorldRemovalTexture");
        GridTexture texture = textureObject.AddComponent<GridTexture>();
        BuildingSO definition = CreateDemolitionDefinition(-9197);
        Grid grid = new(4, 1);
        Vector2Int position = new(1, 0);
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa:world-removal-port";
        BuildableObject building = CreateRegisteredBuilding(
            grid,
            definition,
            facilityId,
            position);
        GameObject buildingRoot = building.gameObject;
        TrackingBuildingWorld world = new(building);
        ProductionFacilityDestructiveDrainWorldRemovalPort port = new(
            world,
            new FixedGridTextureProvider(texture));
        try
        {
            ProductionFacilityWorldRemovalResult applied =
                port.TryEnsureRemoved(facilityId);
            Require(applied.Applied
                && world.Buildings.Count == 0
                && grid.GetGridCell(position)
                    .GetOccupant(GridLayer.Building) == null,
                "Exact world-removal port did not remove grid and registry authority.");
            ProductionFacilityWorldRemovalResult replay =
                port.TryEnsureRemoved(facilityId);
            Require(replay.Disposition ==
                    ProductionFacilityWorldRemovalDisposition.AlreadyApplied,
                "Exact world-removal replay was not idempotent.");
        }
        finally
        {
            if (buildingRoot != null)
                UnityEngine.Object.DestroyImmediate(buildingRoot);
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(textureObject);
        }
    }

    private static BuildingSO CreateDemolitionDefinition(int id)
    {
        BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
        definition.id = id;
        definition.objectName = "Lifecycle Demolition Fixture";
        definition.width = 1;
        definition.height = 1;
        definition.layer = GridLayer.Building;
        definition.category = BuildingCategory.Shop;
        definition.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
        definition.unlocked = true;
        definition.ReplaceAbilities(new BuildingAbilityCollection());
        definition.Facility = new FacilityData();
        definition.Evolution = new FacilityEvolutionContributionData();
        return definition;
    }

    private static BuildableObject CreateRegisteredBuilding(
        Grid grid,
        BuildingSO definition,
        BuildingInstanceId facilityId,
        Vector2Int position)
    {
        GameObject root = new GameObject("LifecycleDemolitionFixture");
        try
        {
            BuildableObject building = root.AddComponent<BuildableObject>();
            building.ConstructDebugRules(AllowDamageRule.Instance);
            typeof(BuildableObject).GetField(
                    "facilityCandidateCache",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(building, NoopFacilityStateChange.Instance);
            building.RestorePersistentIdentity(facilityId);
            building.SetGrid(grid);
            typeof(BuildableObject).GetProperty(
                    nameof(BuildableObject.BuildingData),
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(building, definition);
            typeof(BuildableObject).GetProperty(
                    nameof(BuildableObject.id),
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(building, definition.id);
            typeof(BuildableObject).GetProperty(
                    nameof(BuildableObject.centerPos),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(building, position);
            MutableBuildPositions(building).Add(position);
            Require(grid.RegisterOccupant(
                    building,
                    GridLayer.Building,
                    new[] { position },
                    connectPositions: false),
                "Could not register demolition fixture building.");
            return building;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw;
        }
    }

    private static List<Vector2Int> MutableBuildPositions(BuildableObject building) =>
        (List<Vector2Int>)typeof(BuildableObject)
            .GetField("mutableBuildPoses", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(building)
        ?? throw new InvalidOperationException("BuildableObject position authority was not found.");

    private static string Fingerprint(string value) =>
        ProductionLifecycleFingerprint.Compute(value);

    private static void RequireThrows(Action action, string expectedToken)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(
                exception.Message.Contains(
                    expectedToken,
                    StringComparison.Ordinal),
                "Fail-loud exception did not contain expected token: "
                + expectedToken + " | " + exception.Message);
            return;
        }
        throw new InvalidOperationException(
            "Expected fail-loud exception was not thrown: " + expectedToken);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixedBillQuery : IProductionBillCoreQuery
    {
        private readonly ProductionFacilityBillLifecycleSnapshot snapshot;
        internal FixedBillQuery(ProductionFacilityBillLifecycleSnapshot snapshot) =>
            this.snapshot = snapshot;
        public int Version => checked((int)snapshot.BillAuthorityRevision);
        public IReadOnlyList<ProductionBillSnapshot> GetBills(ProductionFacilityHandle facility) =>
            Array.Empty<ProductionBillSnapshot>();
        public ProductionFacilityBillLifecycleSnapshot CaptureFacilityLifecycle(
            BuildingInstanceId facilityId) => snapshot;
        public bool HasStockSensor(ProductionFacilityHandle facility) => false;
    }

    private sealed class FixedEquipmentQueue : ICombatEquipmentCraftQueueQuery
    {
        internal FixedEquipmentQueue(params CombatEquipmentCraftOrderSaveData[] orders) =>
            CraftQueue = orders ?? Array.Empty<CombatEquipmentCraftOrderSaveData>();
        public IReadOnlyList<CombatEquipmentCraftOrderSaveData> CraftQueue { get; }
    }

    private sealed class FixedMaintenanceOrders :
        ICombatEquipmentMaintenanceOrderQuery
    {
        internal FixedMaintenanceOrders(
            params CombatEquipmentRepairOrder[] orders) =>
            Orders = orders ?? Array.Empty<CombatEquipmentRepairOrder>();

        public IReadOnlyList<CombatEquipmentRepairOrder> Orders { get; }
    }

    private sealed class FixedApparelOrders : IApparelWorkOrderQuery
    {
        internal FixedApparelOrders(params ApparelWorkOrderSaveData[] orders) =>
            Orders = orders ?? Array.Empty<ApparelWorkOrderSaveData>();
        public int Version => 3;
        public IReadOnlyList<ApparelWorkOrderSaveData> Orders { get; }
    }

    private sealed class FakeContributor : IProductionOutputDestinationLifecycleContributor
    {
        private readonly bool hasAuthority;
        private readonly IReadOnlyList<ProductionOutputLifecycleBlock> blocks;
        private readonly long authorityRevision;
        private readonly string semanticSeed;
        private readonly string durableSeed;

        internal FakeContributor(
            string contributorId,
            bool hasAuthority,
            IReadOnlyList<ProductionOutputLifecycleBlock> blocks,
            long authorityRevision = 1L,
            string semanticSeed = null,
            string durableSeed = null)
        {
            ContributorId = contributorId;
            this.hasAuthority = hasAuthority;
            this.blocks = blocks;
            this.authorityRevision = authorityRevision;
            this.semanticSeed = semanticSeed ?? contributorId;
            this.durableSeed = durableSeed ?? this.semanticSeed;
        }

        public string ContributorId { get; }

        public ProductionOutputDestinationLifecycleContribution Capture(
            BuildingInstanceId facilityId,
            ProductionOutputDestinationId destinationId) => new(
                ContributorId,
                hasAuthority,
                authorityRevision,
                blocks.Count,
                blocks.Sum(value => value.MassGrams),
                blocks,
                Fingerprint(semanticSeed),
                Fingerprint(durableSeed));
    }

    private sealed class FenceFixture : IDisposable
    {
        internal FenceFixture(
            string facilityId,
            long capacity,
            bool stockSensorCapable = false)
        {
            GameObject root = new GameObject("ProductionLifecycleFenceFixture");
            Building = root.AddComponent<BuildableObject>();
            FacilityId = (BuildingInstanceId)facilityId;
            Building.RestorePersistentIdentity(FacilityId);
            Handle = new ProductionFacilityHandle(
                Building,
                FacilityId,
                new Vector2Int(3, 4),
                isDestroyed: false,
                stockSensorInstallationItemId: stockSensorCapable
                    ? ProductionBillRuntime.StockSensorItemId
                    : string.Empty,
                allowsOverflowDump: false,
                overflowOffset: default,
                definitionId: "building-definition:qa",
                workstationTag: "workstation:qa",
                outputBufferCycleCapacity: 4,
                workstationLaneProfile:
                    ProductionFacilityWorkstationLaneCapacityProfile
                        .SingleManualWithDetachedBatchProcessors);
            Lifecycle = new MutableLifecycleQuery(FacilityId, capacity);
            Authority = new FakeOutputAuthority(Handle, Lifecycle, capacity);
            SensorAuthority = new FakeStockSensorAuthority(
                Handle,
                stockSensorCapable ? 1_150L : 0L);
            StockSensors = new FakeStockSensorRuntime();
            Epoch = new ProductionFacilityMutationEpochRuntime();
            Fence = new ProductionFacilityMutationFence(
                new FixedFacilityHandleQuery(Handle),
                Lifecycle,
                Authority,
                SensorAuthority,
                StockSensors,
                Epoch);
        }

        internal BuildableObject Building { get; }
        internal BuildingInstanceId FacilityId { get; }
        internal ProductionFacilityHandle Handle { get; }
        internal MutableLifecycleQuery Lifecycle { get; }
        internal FakeOutputAuthority Authority { get; }
        internal FakeStockSensorAuthority SensorAuthority { get; }
        internal FakeStockSensorRuntime StockSensors { get; }
        internal ProductionFacilityMutationEpochRuntime Epoch { get; }
        internal ProductionFacilityMutationFence Fence { get; }

        public void Dispose()
        {
            if (Building != null)
                UnityEngine.Object.DestroyImmediate(Building.gameObject);
        }
    }

    private sealed class FixedFacilityHandleQuery : IProductionFacilityHandleQuery
    {
        private readonly ProductionFacilityHandle handle;
        internal FixedFacilityHandleQuery(ProductionFacilityHandle handle) =>
            this.handle = handle;
        public ProductionFacilityHandle CaptureFacility(object runtimeObject) =>
            ReferenceEquals(runtimeObject, handle.RuntimeObject)
                ? handle
                : throw new InvalidOperationException("Unexpected facility runtime object.");
    }

    private sealed class MutableLifecycleQuery : IProductionOutputDestinationLifecycleQuery
    {
        private readonly BuildingInstanceId facilityId;
        internal MutableLifecycleQuery(BuildingInstanceId facilityId, long capacity)
        {
            this.facilityId = facilityId;
            HasAuthority = capacity > 0L;
        }

        internal int Revision { get; set; }
        internal bool HasAuthority { get; set; }

        public ProductionOutputDestinationLifecycleSnapshot Capture(BuildingInstanceId requested)
        {
            Require(requested.Equals(facilityId), "Lifecycle queried with the wrong facility.");
            ProductionOutputDestinationLifecycleContribution contribution = new(
                "fake-live-authority",
                HasAuthority,
                Revision,
                0,
                0L,
                Array.Empty<ProductionOutputLifecycleBlock>(),
                Fingerprint("fake-live-authority|" + Revision + "|" + HasAuthority));
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                ProductionOutputDestinationId.FromFacility(facilityId),
                new[] { contribution },
                Fingerprint("aggregate|" + Revision + "|" + HasAuthority));
        }
    }

    private sealed class FakeOutputAuthority : IProductionOutputDestinationAuthorityRuntime
    {
        private readonly ProductionFacilityHandle handle;
        private readonly MutableLifecycleQuery lifecycle;
        private readonly long initialCapacity;

        internal FakeOutputAuthority(
            ProductionFacilityHandle handle,
            MutableLifecycleQuery lifecycle,
            long initialCapacity)
        {
            this.handle = handle;
            this.lifecycle = lifecycle;
            this.initialCapacity = initialCapacity;
        }

        internal int RevokeCount { get; private set; }
        internal int EnsureCount { get; private set; }
        internal long LastEnsuredCapacity { get; private set; }
        internal bool FailRevoke { get; set; }

        public bool TryEnsure(
            ProductionFacilityHandle facility,
            long minimumMassCapacityGrams,
            out FacilityBufferCapacityProfile profile,
            out string failureReason)
        {
            failureReason = string.Empty;
            profile = null;
            if (!ReferenceEquals(facility.RuntimeObject, handle.RuntimeObject)
                || minimumMassCapacityGrams <= 0L)
            {
                failureReason = "fake-ensure-invalid";
                return false;
            }
            EnsureCount++;
            LastEnsuredCapacity = minimumMassCapacityGrams;
            lifecycle.HasAuthority = true;
            lifecycle.Revision++;
            profile = Profile(minimumMassCapacityGrams);
            return true;
        }

        public bool TryValidate(
            ProductionFacilityHandle facility,
            out FacilityBufferCapacityProfile profile,
            out string failureReason)
        {
            failureReason = string.Empty;
            profile = lifecycle.HasAuthority ? Profile(initialCapacity) : null;
            if (profile != null)
                return true;
            failureReason = "fake-authority-missing";
            return false;
        }

        public bool TryReplaceProjected(
            IReadOnlyList<ProductionFacilityHandle> facilities,
            IReadOnlyDictionary<string, long> capacityGramsByFacilityId,
            out string failureReason)
        {
            failureReason = "fake-not-supported";
            return false;
        }

        public bool TryRevoke(BuildingInstanceId facilityId, out string failureReason)
        {
            failureReason = string.Empty;
            if (FailRevoke)
            {
                failureReason = "fake-output-revoke-failure";
                return false;
            }
            if (!facilityId.Equals(handle.InstanceId) || !lifecycle.HasAuthority)
            {
                failureReason = "fake-revoke-invalid";
                return false;
            }
            RevokeCount++;
            lifecycle.HasAuthority = false;
            lifecycle.Revision++;
            return true;
        }

        private FacilityBufferCapacityProfile Profile(long capacity) => new(
            ProductionOutputDestinationId.FromFacility(handle.InstanceId).Value,
            handle.Position,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            ProductionOutputDestinationId.FromFacility(handle.InstanceId).Value,
            handle.InstanceId.Value,
            new PhysicalMassGrams(capacity),
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision);
    }

    private sealed class RecordingBuildingFactory : IGridBuildingFactory
    {
        internal int DeleteCount { get; private set; }
        public BuildableObject Create(Grid grid, BuildingSO buildingData, Vector2Int selectPos) =>
            throw new InvalidOperationException("The demolition fixture does not create buildings.");
        public BuildableObject CreateDetached(
            Grid grid,
            BuildingSO buildingData,
            Vector2Int selectPos) =>
            throw new InvalidOperationException(
                "The demolition fixture does not create detached buildings.");
        public void PublishDetached(
            BuildableObject candidate,
            BuildingSO buildingData,
            Vector2Int selectPos) =>
            throw new InvalidOperationException(
                "The demolition fixture does not publish detached buildings.");
        public void DiscardDetached(BuildableObject candidate) =>
            throw new InvalidOperationException(
                "The demolition fixture does not discard detached buildings.");
        public void DeleteVisual(BuildingSO buildingData, Vector2Int selectPos) =>
            DeleteCount++;
    }

    private sealed class RecordingMutationFence : IProductionFacilityMutationFence
    {
        internal List<string> Calls { get; } = new();
        internal bool AllowPrepare { get; set; } = true;

        public bool TryPrepareEmpty(
            BuildableObject facility,
            ProductionFacilityMutationKind kind,
            string operationId,
            out ProductionFacilityEmptyMutationCandidate candidate,
            out string failureReason)
        {
            Calls.Add("prepare");
            if (!AllowPrepare)
            {
                candidate = null;
                failureReason = "recording-fence-blocked";
                return false;
            }
            failureReason = string.Empty;
            ProductionFacilityHandle handle = new(
                facility,
                facility.PersistentInstanceId,
                facility.centerPos,
                false,
                string.Empty,
                false,
                default,
                "building-definition:qa",
                string.Empty,
                4);
            candidate = new ProductionFacilityEmptyMutationCandidate(
                kind,
                operationId,
                1L,
                handle,
                Fingerprint("placement-fence"),
                hadOutputAuthority: true,
                priorCapacityMassGrams: 1_000L,
                hadStockSensorAuthority: false,
                priorStockSensorCapacityMassGrams: 0L);
            return true;
        }

        public bool TryCommitAuthorityRevoke(
            ProductionFacilityEmptyMutationCandidate candidate,
            out string failureReason)
        {
            Calls.Add("commit");
            candidate.AuthorityRevoked = true;
            failureReason = string.Empty;
            return true;
        }

        public bool TryAbort(
            ProductionFacilityEmptyMutationCandidate candidate,
            out string failureReason)
        {
            Calls.Add("abort");
            candidate.AuthorityRevoked = false;
            candidate.IsClosed = true;
            failureReason = string.Empty;
            return true;
        }

        public bool TryComplete(
            ProductionFacilityEmptyMutationCandidate candidate,
            out string failureReason)
        {
            Calls.Add("complete");
            candidate.IsClosed = true;
            failureReason = string.Empty;
            return true;
        }

        public bool TryRequireNoAuthority(
            BuildableObject facility,
            ProductionFacilityMutationKind kind,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RecordingDestructiveLossRuntime :
        IBuildingDestructiveLossRuntime
    {
        internal List<ProductionFacilityDestructiveDrainCause> Causes { get; } =
            new();
        internal BuildingDestructiveLossDisposition Disposition { get; set; } =
            BuildingDestructiveLossDisposition.RemovedAwaitingCheckpointGc;
        internal string FailureReason { get; set; } = string.Empty;

        public BuildingDestructiveLossResult Apply(
            BuildableObject building,
            ProductionFacilityDestructiveDrainCause cause)
        {
            Causes.Add(cause);
            if (Disposition ==
                    BuildingDestructiveLossDisposition
                        .RemovedAwaitingCheckpointGc
                || Disposition ==
                    BuildingDestructiveLossDisposition
                        .CommittedWithNotificationFailure)
            {
                GridLayer layer = building.BuildingData.Placement.Layer;
                building.Grid.RemoveOccupant(
                    building,
                    layer,
                    building.buildPoses,
                    building.BuildingData.Placement.IsMovement);
                building.DestroySelf();
            }
            return new BuildingDestructiveLossResult(
                Disposition,
                FailureReason);
        }
    }

    private sealed class TrackingBuildingWorld : IBuildingWorldQuery
    {
        private readonly BuildableObject building;

        internal TrackingBuildingWorld(BuildableObject building) =>
            this.building = building;

        public int BuildingVersion => building == null || building.isDestroy
            ? 1
            : 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            building == null || building.isDestroy
                ? Array.Empty<BuildableObject>()
                : new[] { building };
    }

    private sealed class FixedGridTextureProvider : IGridTextureProvider
    {
        internal FixedGridTextureProvider(GridTexture texture) =>
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        public GridTexture Texture { get; }
    }

    private sealed class FakeStockSensorAuthority :
        IProductionStockSensorDestinationAuthorityRuntime
    {
        private readonly ProductionFacilityHandle handle;

        internal FakeStockSensorAuthority(
            ProductionFacilityHandle handle,
            long capacityMassGrams)
        {
            this.handle = handle;
            CapacityMassGrams = capacityMassGrams;
            HasAuthority = capacityMassGrams > 0L;
        }

        internal bool HasAuthority { get; private set; }
        internal bool IsEmpty { get; set; } = true;
        internal long CapacityMassGrams { get; private set; }
        internal int RevokeCount { get; private set; }
        internal int EnsureCount { get; private set; }

        public bool TryEnsure(
            ProductionFacilityHandle facility,
            out long capacityMassGrams,
            out string failureReason)
        {
            capacityMassGrams = CapacityMassGrams;
            failureReason = string.Empty;
            if (!ReferenceEquals(facility?.RuntimeObject, handle.RuntimeObject)
                || CapacityMassGrams <= 0L)
            {
                failureReason = "fake-stock-sensor-ensure-invalid";
                return false;
            }
            EnsureCount++;
            HasAuthority = true;
            return true;
        }

        public bool TryValidate(
            ProductionFacilityHandle facility,
            out long capacityMassGrams,
            out string failureReason)
        {
            capacityMassGrams = CapacityMassGrams;
            failureReason = string.Empty;
            return HasAuthority
                && ReferenceEquals(facility?.RuntimeObject, handle.RuntimeObject)
                && CapacityMassGrams > 0L;
        }

        public bool TryReplaceProjected(
            IReadOnlyList<ProductionFacilityHandle> facilities,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryRequireEmpty(
            ProductionFacilityHandle facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (HasAuthority
                && IsEmpty
                && ReferenceEquals(facility?.RuntimeObject, handle.RuntimeObject))
            {
                return true;
            }
            failureReason = "fake-stock-sensor-not-empty-or-missing";
            return false;
        }

        public bool TryRevoke(
            BuildingInstanceId facilityId,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!facilityId.Equals(handle.InstanceId)
                || !HasAuthority
                || !IsEmpty)
            {
                failureReason = "fake-stock-sensor-revoke-invalid";
                return false;
            }
            RevokeCount++;
            HasAuthority = false;
            return true;
        }
    }

    private sealed class FakeStockSensorRuntime : IProductionStockSensorRuntime
    {
        internal bool HasPhysicalState { get; set; }
        public int Version => 0;
        public IReadOnlyCollection<string> InstalledFacilityIds =>
            Array.Empty<string>();
        public IReadOnlyCollection<string> AcknowledgedFacilityIds =>
            Array.Empty<string>();
        public IReadOnlyCollection<ProductionStockSensorPhysicalCommitSaveData>
            PendingInstallations =>
            Array.Empty<ProductionStockSensorPhysicalCommitSaveData>();
        public IReadOnlyCollection<ProductionInstalledStockSensorSaveData>
            InstalledSensors =>
            Array.Empty<ProductionInstalledStockSensorSaveData>();
        public IReadOnlyCollection<ProductionStockSensorRemovalSaveData>
            PendingRemovals =>
            Array.Empty<ProductionStockSensorRemovalSaveData>();
        public bool Has(ProductionFacilityHandle facility) => HasPhysicalState;
        public bool HasOwnedPhysicalState(ProductionFacilityHandle facility) =>
            HasPhysicalState;
        public bool IsAcknowledged(ProductionFacilityHandle facility) => false;
        public ProductionBillCommandResult RequestInstallation(
            ProductionFacilityHandle facility) => Failed();
        public ProductionBillCommandResult Remove(
            ProductionFacilityHandle facility) => Failed();
        public ProductionBillCommandResult Acknowledge(
            ProductionFacilityHandle facility) => Failed();
        public bool TryReconcileDestinationAuthorities(out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
        public void FinalizeDeliveredSensors()
        {
        }

        private static ProductionBillCommandResult Failed() =>
            ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionSupportUnavailable));
    }

    private sealed class AllowDamageRule : IBuildingDamageRulePort
    {
        internal static readonly AllowDamageRule Instance = new();
        public bool ShouldBlockFacilityDamage(bool damaged) => false;
    }

    private sealed class NoopFacilityStateChange : IBuildingFacilityStateChangePort
    {
        internal static readonly NoopFacilityStateChange Instance = new();
        public void MarkDynamicStateDirty()
        {
        }
    }
}
#endif
