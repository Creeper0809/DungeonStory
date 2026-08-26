#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class ProductionCapacityRoutingActorTransitionDebugScenarios
{
    private const string ItemId = "item:buffer";
    private const string BatchCommitId =
        "batch:qa:capacity-routing-actor-transition";
    private const string StepOperationId =
        "production-facility-destructive-drain-step:qa:actor-transition";
    private const string FacilityId =
        "building:qa:capacity-routing-actor-transition-origin";
    private const string SourceDestinationId =
        "production-output:building:qa:capacity-routing-actor-transition-origin";
    private const string OutputLineId =
        "output-line:qa:capacity-routing-actor-transition";
    private const string LineCommitId =
        "line-commit:qa:capacity-routing-actor-transition";

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Capacity Actor Transition")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("CAPACITY_ROUTING_ACTOR_TRANSITION=PASS");
    }

    public static void RunAll()
    {
        using Fixture fixture = Fixture.Create();
        CargoContext actorA = fixture.CreateCommittedCargo(
            "character:capacity-routing:a",
            "route:capacity-routing:a",
            new Vector2Int(1, 1),
            originalStackOrdinal: 0,
            sourceOffsetQuantity: 0);
        CargoContext actorB = fixture.CreateCommittedCargo(
            "character:capacity-routing:b",
            "route:capacity-routing:b",
            new Vector2Int(3, 1),
            originalStackOrdinal: 1,
            sourceOffsetQuantity: 1);
        ProductionCapacityRoutingDrainRequest request =
            fixture.PrepareDrain(actorA, actorB);

        fixture.Coordinator.DebugFailBeforeAuthorityRowMutation =
            (operationId, _) => string.Equals(
                operationId,
                actorB.OperationId,
                StringComparison.Ordinal);
        ProductionCapacityRoutingDrainResult interrupted = fixture.Coordinator
            .TryQuiesceAndReleaseAllActors(
                StepOperationId,
                request.RequestFingerprint);
        Require(interrupted.Status ==
                ProductionCapacityRoutingDrainStatus.Deferred,
            "Injected actor-B release fault did not defer the transaction: "
            + interrupted.FailureReason);
        Require(fixture.Outbox.TryCapture(
                StepOperationId,
                out ProductionCapacityRoutingDrainSaveData interruptedDrain)
            && interruptedDrain.phase ==
                ProductionCapacityRoutingDrainPhase
                    .ReleasingOperationAuthority,
            "Interrupted actor transition did not remain in its non-saveable phase.");
        Require(interruptedDrain.actorQuiesceReceipts.Count == 2,
            "Actor-set physical quiescence did not publish both receipts.");
        Require(interruptedDrain.actorAuthorityReleases.Count == 2,
            "Interrupted actor transition did not preserve both prepared plans.");
        RequireCommittedRelease(interruptedDrain, actorA.ActorId, true);
        RequireCommittedRelease(interruptedDrain, actorB.ActorId, false);
        fixture.RequireQuiescedPhysical(actorA);
        fixture.RequireQuiescedPhysical(actorB);
        fixture.RequireAuthorityReleased(actorA);
        fixture.RequireAuthorityLive(actorB);
        Require(!actorA.Ability.IsCapacityRoutingQuiescenceFrozen,
            "Committed actor A retained its frozen haul plan.");
        Require(actorB.Ability.IsCapacityRoutingQuiescenceFrozen,
            "Interrupted actor B lost its frozen replay plan.");

        bool unstableCaptureRejected = false;
        try
        {
            fixture.Runtime.Capture();
        }
        catch (InvalidOperationException)
        {
            unstableCaptureRejected = true;
        }
        Require(unstableCaptureRejected,
            "A partial actor authority release was accepted as a V16 save.");

        string committedActorAReceipt = interruptedDrain
            .actorAuthorityReleases.Single(value => string.Equals(
                value.actorPersistentId,
                actorA.ActorId,
                StringComparison.Ordinal)).receiptFingerprint;
        fixture.Coordinator.DebugFailBeforeAuthorityRowMutation = null;
        ProductionCapacityRoutingDrainResult resumed = fixture.Coordinator
            .TryQuiesceAndReleaseAllActors(
                StepOperationId,
                request.RequestFingerprint);
        Require(resumed.Status == ProductionCapacityRoutingDrainStatus.Applied,
            "Actor transition replay did not complete: "
            + resumed.FailureReason);
        Require(fixture.Outbox.TryCapture(
                StepOperationId,
                out ProductionCapacityRoutingDrainSaveData stableDrain)
            && stableDrain.phase ==
                ProductionCapacityRoutingDrainPhase
                    .AwaitingStablePhysicalState,
            "Actor transition did not reach stable-physical verification.");
        RequireCommittedRelease(stableDrain, actorA.ActorId, true);
        RequireCommittedRelease(stableDrain, actorB.ActorId, true);
        Require(string.Equals(
                committedActorAReceipt,
                stableDrain.actorAuthorityReleases.Single(value =>
                    string.Equals(
                        value.actorPersistentId,
                        actorA.ActorId,
                        StringComparison.Ordinal)).receiptFingerprint,
                StringComparison.Ordinal),
            "Actor A's committed release changed while actor B replayed.");
        fixture.RequireQuiescedPhysical(actorA);
        fixture.RequireQuiescedPhysical(actorB);
        fixture.RequireAuthorityReleased(actorA);
        fixture.RequireAuthorityReleased(actorB);
        Require(!actorA.Ability.IsCapacityRoutingQuiescenceFrozen
                && !actorB.Ability.IsCapacityRoutingQuiescenceFrozen,
            "Completed actor transition retained a frozen AbilityHaul plan.");

        DungeonPhysicalItemSaveData stableSnapshot = fixture.Runtime.Capture();
        string stableJson = JsonUtility.ToJson(stableSnapshot);
        ProductionCapacityRoutingDrainResult replay = fixture.Coordinator
            .TryQuiesceAndReleaseAllActors(
                StepOperationId,
                request.RequestFingerprint);
        Require(replay.Status == ProductionCapacityRoutingDrainStatus.Replay,
            "Completed actor transition did not replay idempotently.");
        Require(string.Equals(
                stableJson,
                JsonUtility.ToJson(fixture.Runtime.Capture()),
                StringComparison.Ordinal),
            "No-op actor transition replay changed the physical save graph.");
    }

    private static void RequireCommittedRelease(
        ProductionCapacityRoutingDrainSaveData drain,
        string actorId,
        bool expectedCommitted)
    {
        ProductionCapacityRoutingActorAuthorityReleaseSaveData release =
            drain.actorAuthorityReleases.Single(value => string.Equals(
                value.actorPersistentId,
                actorId,
                StringComparison.Ordinal));
        Require(release.effectsCommitted == expectedCommitted
                && release.actorPlanFinalized == expectedCommitted,
            $"Actor release commit state mismatch: {actorId}; "
            + $"committed={release.effectsCommitted}; "
            + $"finalized={release.actorPlanFinalized}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> actorObjects = new();
        private readonly TestWarehouseFacility warehouse;
        private readonly CharacterCarryInventoryRegistry carryRegistry;
        private readonly ItemQuantityReservationService reservations;
        private readonly WarehouseMassAdmissionService admissions;
        private readonly WorldItemRepository repository;
        private readonly Grid grid;
        private readonly TestGridProvider gridProvider;
        private readonly TestHaulingSettings haulingSettings = new();
        private bool disposed;

        private Fixture(
            WorldItemStackRuntime runtime,
            WorldItemRepository repository,
            ItemQuantityReservationService reservations,
            WarehouseMassAdmissionService admissions,
            ProductionCapacityRoutingDrainOutbox outbox,
            ProductionCapacityRoutingOperationAuthorityReleaseCoordinator
                coordinator,
            TestWarehouseFacility warehouse,
            CharacterCarryInventoryRegistry carryRegistry,
            Grid grid,
            TestGridProvider gridProvider)
        {
            Runtime = runtime;
            this.repository = repository;
            this.reservations = reservations;
            this.admissions = admissions;
            Outbox = outbox;
            Coordinator = coordinator;
            this.warehouse = warehouse;
            this.carryRegistry = carryRegistry;
            this.grid = grid;
            this.gridProvider = gridProvider;
        }

        internal WorldItemStackRuntime Runtime { get; }
        internal ProductionCapacityRoutingDrainOutbox Outbox { get; }
        internal ProductionCapacityRoutingOperationAuthorityReleaseCoordinator
            Coordinator { get; }

        internal static Fixture Create()
        {
            WorldItemStackRuntime runtime = null;
            CharacterCarryInventoryRegistry carryRegistry = null;
            try
            {
                runtime = PhysicalItemDebugScenarios
                    .CreateRuntimeForCrossDomainFixture(
                        out WorldItemRepository repository,
                        out _,
                        out ItemQuantityReservationService reservations,
                        out IReservedItemTransferService reservedTransfer);
                ItemTransferService transfer = reservedTransfer as ItemTransferService
                    ?? throw new InvalidOperationException(
                        "Cross-domain fixture did not expose ItemTransferService.");
                Grid grid = CreateWalkableGrid();
                TestGridProvider gridProvider = new(grid);
                PhysicalStockQuery stock = new(
                    repository,
                    runtime.CatalogProvider,
                    runtime.MassQuery);
                BuildingInstanceId warehouseId =
                    (BuildingInstanceId)
                    "building:qa:capacity-routing-actor-transition-warehouse";
                WarehouseInventory inventory = new(
                    200,
                    100_000L,
                    StockCategory.General,
                    restrictCategory: false);
                inventory.BindPhysicalStock(
                    stock,
                    warehouseId,
                    CharacterAiEditorTestDependencies.AuthoredGameplay);
                TestWarehouseFacility warehouse = new(warehouseId, inventory);
                WarehouseMassAdmissionService admissions = new(
                    runtime.CatalogProvider,
                    runtime.MassQuery,
                    stock,
                    new TestWarehouseWorldQuery(warehouse),
                    new TestClock(),
                    repository);
                carryRegistry = new CharacterCarryInventoryRegistry();
                ProductionCapacityRoutingDrainOutbox outbox = new(repository);
                ProductionCapacityRoutingOperationAuthorityReleaseCoordinator
                    coordinator = new(
                        repository,
                        outbox,
                        carryRegistry,
                        reservations,
                        admissions,
                        transfer,
                        reservations);
                return new Fixture(
                    runtime,
                    repository,
                    reservations,
                    admissions,
                    outbox,
                    coordinator,
                    warehouse,
                    carryRegistry,
                    grid,
                    gridProvider);
            }
            catch
            {
                carryRegistry?.Dispose();
                runtime?.Dispose();
                throw;
            }
        }

        internal CargoContext CreateCommittedCargo(
            string actorId,
            string routeOperationId,
            Vector2Int actorCell,
            int originalStackOrdinal,
            int sourceOffsetQuantity)
        {
            ActorContext actor = CreateActor(actorId, actorCell);
            string operationId = repository
                .AllocateEditorTestHaulDeliveryOperationId(actorId);
            string destinationId =
                WarehouseStorageIdentity.RequireDestinationId(warehouse);
            Vector2Int targetCell = new(7, 1);
            string sourceStackId = repository.AddEditorTestStack(
                ItemId,
                1,
                WorldItemStackState.Loose,
                position: actorCell);
            string lotFingerprint = ItemReservationSignature.Create(
                ItemId,
                Array.Empty<ItemInstanceComponentSaveData>());
            Require(reservations.TryReserve(
                    operationId,
                    actorId,
                    ItemReservationPurpose.Hauling,
                    $"haul:{WorldItemHaulDestinationKind.Warehouse}:{destinationId}",
                    new ItemQuantityReservationRequest(
                        new ItemStackId(sourceStackId),
                        1,
                        lotFingerprint),
                    out ItemQuantityLease lease,
                    out DomainFailure leaseFailure),
                "Fixture quantity lease failed: " + leaseFailure);

            string admissionOperationId =
                operationId + ":warehouse-admission:00";
            PhysicalItemMassSubject massSubject =
                PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)ItemId);
            WarehouseMassAdmissionRequest admissionRequest = new(
                warehouse.PersistentInstanceId,
                admissionOperationId,
                (ItemDefinitionId)ItemId,
                string.Empty,
                lotFingerprint,
                1,
                admissions.GetWarehouseCapacityRevision(
                    warehouse.PersistentInstanceId),
                admissions.CatalogRevision,
                repository.ItemStackVersion,
                massSubject);
            Require(admissions.TryReserve(
                    admissionRequest,
                    out WarehouseMassAdmissionToken admissionToken,
                    out DomainFailure admissionFailure)
                && admissionToken.AcceptedQuantity == 1,
                "Fixture gram admission failed: " + admissionFailure);
            WarehouseHaulAdmissionSaveData admissionProjection = new()
            {
                tokenId = admissionToken.TokenId,
                ownerAdmissionOperationId = admissionOperationId,
                warehouseId = admissionToken.WarehouseId.Value,
                sourceStackId = sourceStackId,
                itemId = admissionToken.ItemId.Value,
                itemInstanceId = admissionToken.ItemInstanceId,
                lotFingerprint = admissionToken.LotFingerprint,
                quantity = admissionToken.AcceptedQuantity,
                reservedMassGrams = admissionToken.ReservedMassGrams,
                catalogRevision = admissionToken.CatalogRevision,
                sourceRevision = admissionToken.SourceRevision
            };
            Require(Runtime.TryRegisterHaulDeliveryPlanForEditorTest(
                    operationId,
                    actorId,
                    WorldItemHaulDestinationKind.Warehouse,
                    destinationId,
                    targetCell,
                    targetCell,
                    new[] { admissionProjection },
                    out string intentFailure),
                "Fixture haul intent failed: " + intentFailure);

            WorldItemReservedStackQuantity reservation = new(
                sourceStackId,
                ItemId,
                1,
                actorCell,
                WorldItemHaulDestinationKind.Warehouse,
                destinationId,
                lease.leaseId,
                operationId);
            Require(Runtime.TryPickupReservedStackQuantity(
                    actor.Actor,
                    actor.Inventory,
                    reservation,
                    out int pickedUp,
                    out string pickupFailure)
                && pickedUp == 1,
                "Fixture pickup failed: " + pickupFailure);
            CharacterCarriedItemSaveData picked = actor.Inventory.Items.Single(
                value => string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal));
            long unitMassGrams = Runtime.MassQuery.GetQuantityMass(
                (ItemDefinitionId)ItemId,
                massSubject,
                1).Value;
            Require(repository.ConfigureEditorCapacityRoutingCarriedStack(
                    picked.carriedStackId,
                    actorId,
                    actorCell,
                    BatchCommitId,
                    OutputLineId,
                    LineCommitId,
                    originalStackOrdinal,
                    originalBatchStackCount: 2,
                    originalBatchQuantity: 2,
                    originalBatchMassGrams: checked(unitMassGrams * 2L),
                    routeOperationId,
                    picked.sourceStackId,
                    sourceOffsetQuantity,
                    destinationId,
                    targetCell,
                    1,
                    unitMassGrams),
                "Fixture exact-route custody authoring failed.");
            WorldItemStackSnapshot carriedStack = Runtime.GetAllStacks().Single(
                value => string.Equals(
                    value.StackId,
                    picked.carriedStackId,
                    StringComparison.Ordinal));
            actor.Inventory.Restore(new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new()
                    {
                        carriedStackId = carriedStack.StackId,
                        sourceStackId = picked.sourceStackId,
                        ownerOperationId = operationId,
                        itemInstanceId = carriedStack.ItemInstanceId,
                        itemId = carriedStack.ItemId,
                        quantity = 1,
                        wasteOrigin = carriedStack.WasteOrigin,
                        contamination = carriedStack.Contamination,
                        components = carriedStack.Components
                            .Select(value => value.Clone()).ToList()
                    }
                }
            });
            Require(Runtime.TryCommitHaulPickup(
                    operationId,
                    actor.Inventory,
                    out string commitFailure),
                "Fixture pickup commit failed: " + commitFailure);

            WorldItemHaulPlanLeg deliveryLeg = new(
                reservation,
                actorCell,
                warehouse,
                targetCell,
                targetCell);
            WorldItemHaulPlan deliveryPlan = new(
                Array.Empty<WorldItemHaulPlanLeg>(),
                new[] { deliveryLeg },
                new[] { reservation },
                unitMassGrams / 1000f,
                expectedDetourCost: 0,
                WorldItemHaulDestinationKind.Warehouse,
                destinationId,
                deliveryOnlyResume: true);
            Require(actor.Ability.TryBindCapacityRoutingEditorFixture(
                    Runtime,
                    deliveryPlan,
                    new[] { lease.leaseId },
                    out string bindFailure),
                "Fixture AbilityHaul bind failed: " + bindFailure);
            return new CargoContext(
                actor,
                operationId,
                routeOperationId,
                sourceStackId,
                carriedStack,
                lease.leaseId,
                admissionToken.TokenId,
                unitMassGrams,
                sourceOffsetQuantity,
                targetCell,
                destinationId);
        }

        internal ProductionCapacityRoutingDrainRequest PrepareDrain(
            params CargoContext[] cargo)
        {
            CargoContext[] ordered = cargo
                .OrderBy(value => value.RouteOperationId, StringComparer.Ordinal)
                .ToArray();
            long totalMass = ordered.Sum(value => value.MassGrams);
            string componentFingerprint =
                ProductionCapacityRoutingDrainFingerprint
                    .CreateActorCarryStackSignature(
                        ItemId,
                        string.Empty,
                        ordered[0].CarriedStack.Components);
            ProductionCapacityRoutingDrainLineSaveData[] lines =
            {
                new()
                {
                    lineCommitId = LineCommitId,
                    outputLineId = OutputLineId,
                    itemId = ItemId,
                    componentFingerprint = componentFingerprint,
                    originalQuantity = ordered.Length,
                    originalMassGrams = totalMass,
                    routedQuantity = ordered.Length,
                    routedMassGrams = totalMass
                }
            };
            ProductionCapacityRoutingDrainRouteSaveData[] routes = ordered
                .Select(value => new ProductionCapacityRoutingDrainRouteSaveData
                {
                    routeOperationId = value.RouteOperationId,
                    requestFingerprint = new string('c', 64),
                    physicalReceiptFingerprint = new string('d', 64),
                    phase = 3,
                    currentDeliveryRevision = 0L,
                    currentDeliveryRevisionFingerprint = new string('e', 64),
                    currentTargetDestinationId = value.DestinationId,
                    currentTargetAuthorityFingerprint = new string('f', 64)
                }).ToArray();
            ProductionCapacityRoutingDrainSliceSaveData[] slices = ordered
                .Select(value => new ProductionCapacityRoutingDrainSliceSaveData
                {
                    routeOperationId = value.RouteOperationId,
                    sourceStackId = value.SourceStackId,
                    routedStackId = value.CarriedStack.StackId,
                    outputLineId = OutputLineId,
                    lineCommitId = LineCommitId,
                    itemId = ItemId,
                    sourceOffsetQuantity = value.SourceOffsetQuantity,
                    routedOffsetQuantity = value.SourceOffsetQuantity,
                    routedQuantity = 1,
                    routedMassGrams = value.MassGrams,
                    componentFingerprint = componentFingerprint
                }).ToArray();
            ProductionCapacityRoutingDrainActorCarrySaveData[] carries = ordered
                .Select(value => new ProductionCapacityRoutingDrainActorCarrySaveData
                {
                    actorPersistentId = value.ActorId,
                    haulIntentOperationId = value.OperationId,
                    routeOperationId = value.RouteOperationId,
                    carriedStackId = value.CarriedStack.StackId,
                    sourceStackId = value.SourceStackId,
                    quantity = 1,
                    massGrams = value.MassGrams,
                    stackSignature = ProductionCapacityRoutingDrainFingerprint
                        .CreateActorCarryStackSignature(
                            value.CarriedStack.ItemId,
                            value.CarriedStack.ItemInstanceId,
                            value.CarriedStack.Components)
                }).ToArray();
            string[] custodyStackIds = ordered
                .Select(value => value.CarriedStack.StackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string ownerStableId = "routing-batch:" + BatchCommitId;
            string requestFingerprint =
                ProductionCapacityRoutingDrainFingerprint.CreateRequest(
                    StepOperationId,
                    ownerStableId,
                    FacilityId,
                    SourceDestinationId,
                    BatchCommitId,
                    new string('1', 64),
                    new string('2', 64),
                    new string('3', 64),
                    lines,
                    routes,
                    slices,
                    carries,
                    custodyStackIds,
                    ordered.Length,
                    totalMass);
            ProductionCapacityRoutingDrainRequest request = new(
                StepOperationId,
                ownerStableId,
                FacilityId,
                SourceDestinationId,
                BatchCommitId,
                new string('1', 64),
                new string('2', 64),
                new string('3', 64),
                lines,
                routes,
                slices,
                carries,
                custodyStackIds,
                ordered.Length,
                totalMass,
                requestFingerprint);
            Require(Outbox.TryPrepare(request).Status ==
                    ProductionCapacityRoutingDrainStatus.Applied,
                "Fixture drain prepare failed.");
            Require(Outbox.TryBeginRouting(
                    StepOperationId,
                    requestFingerprint).Status ==
                    ProductionCapacityRoutingDrainStatus.Applied,
                "Fixture drain did not begin routing.");
            Require(Outbox.TryRecordLineRouted(
                    StepOperationId,
                    LineCommitId).Status ==
                    ProductionCapacityRoutingDrainStatus.Applied,
                "Fixture drain did not record routed line.");
            Require(Outbox.TryBeginQuiescingActors(
                    StepOperationId,
                    routes.Select(value => value.routeOperationId),
                    custodyStackIds).Status ==
                    ProductionCapacityRoutingDrainStatus.Applied,
                "Fixture drain did not enter actor quiescence.");
            return request;
        }

        internal void RequireQuiescedPhysical(CargoContext cargo)
        {
            Require(cargo.Inventory.Items.Count == 0,
                "Quiesced actor retained carried inventory: " + cargo.ActorId);
            WorldItemStackSnapshot stack = Runtime.GetAllStacks().Single(value =>
                string.Equals(
                    value.StackId,
                    cargo.CarriedStack.StackId,
                    StringComparison.Ordinal));
            Require(stack.State == WorldItemStackState.Loose
                    && stack.Position == cargo.ActorCell
                    && string.Equals(
                        stack.DestinationId,
                        cargo.DestinationId,
                        StringComparison.Ordinal)
                    && stack.HasDestinationPosition
                    && stack.DestinationPosition == cargo.TargetCell,
                "Quiesced physical stack lost cell or exact destination: "
                + cargo.ActorId);
        }

        internal void RequireAuthorityReleased(CargoContext cargo)
        {
            Require(!reservations.TryGetLeasesByOwner(
                        cargo.OperationId,
                        out IReadOnlyList<ItemQuantityLease> leases)
                    || leases.Count == 0,
                "Released actor retained a quantity lease: " + cargo.ActorId);
            Require(!Runtime.TryCaptureHaulDeliveryIntent(
                    cargo.OperationId,
                    out _),
                "Released actor retained a haul intent: " + cargo.ActorId);
            Require(admissions.TryGetStatus(
                    cargo.AdmissionTokenId,
                    out WarehouseMassAdmissionStatusSnapshot status)
                && status.Status == WarehouseMassAdmissionTokenStatus.Released
                && status.ReleaseReason ==
                    WarehouseMassAdmissionReleaseReason.DestinationInvalidated,
                "Released actor retained a live warehouse admission: "
                + cargo.ActorId);
        }

        internal void RequireAuthorityLive(CargoContext cargo)
        {
            Require(reservations.TryGetLeasesByOwner(
                    cargo.OperationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                && leases.Count == 1
                && string.Equals(
                    leases[0].leaseId,
                    cargo.LeaseId,
                    StringComparison.Ordinal),
                "Interrupted actor lost its exact quantity lease: "
                + cargo.ActorId);
            Require(Runtime.TryCaptureHaulDeliveryIntent(
                    cargo.OperationId,
                    out _),
                "Interrupted actor lost its durable haul intent: "
                + cargo.ActorId);
            Require(admissions.TryGetStatus(
                    cargo.AdmissionTokenId,
                    out WarehouseMassAdmissionStatusSnapshot status)
                && status.Status == WarehouseMassAdmissionTokenStatus.Reserved,
                "Interrupted actor lost its warehouse admission: "
                + cargo.ActorId);
        }

        private ActorContext CreateActor(string actorId, Vector2Int position)
        {
            GameObject actorObject = new("Capacity Routing " + actorId);
            actorObjects.Add(actorObject);
            actorObject.SetActive(false);
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterLifecycle lifecycle =
                actorObject.GetComponent<CharacterLifecycle>();
            CharacterCarryInventory inventory =
                actorObject.AddComponent<CharacterCarryInventory>();
            AbilityHaul ability = actorObject.AddComponent<AbilityHaul>();
            actorObject.GetComponent<CharacterIdentity>()
                .SetPersistentId(actorId);
            CharacterAiEditorTestDependencies.Inject(actorObject);
            lifecycle.ConstructCharacterLifecycle(gridProvider);
            actorObject.transform.position = grid.GetWorldPos(position);
            actorObject.SetActive(true);
            actor.EnsureRuntimeState();
            actor.Initialization(
                CharacterAiEditorTestDependencies
                    .RequireAuthoredCharacterDefinition("Adventurer"));
            inventory.Configure(
                Runtime.CatalogProvider,
                Runtime.MassQuery,
                haulingSettings,
                carryRegistry);
            return new ActorContext(actor, inventory, ability, position);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            foreach (GameObject actorObject in actorObjects)
            {
                if (actorObject != null)
                    UnityEngine.Object.DestroyImmediate(actorObject);
            }
            carryRegistry.Dispose();
            Runtime.Dispose();
        }

        private static Grid CreateWalkableGrid()
        {
            Grid value = new(10, 3);
            for (int y = 0; y < value.height; y++)
            {
                for (int x = 0; x < value.width; x++)
                {
                    value.SetAreaType(
                        new Vector2Int(x, y),
                        GridCellAreaType.ExteriorPath);
                }
            }
            return value;
        }
    }

    private sealed class ActorContext
    {
        internal ActorContext(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            AbilityHaul ability,
            Vector2Int cell)
        {
            Actor = actor;
            Inventory = inventory;
            Ability = ability;
            Cell = cell;
        }

        internal CharacterActor Actor { get; }
        internal CharacterCarryInventory Inventory { get; }
        internal AbilityHaul Ability { get; }
        internal Vector2Int Cell { get; }
    }

    private sealed class CargoContext
    {
        internal CargoContext(
            ActorContext actor,
            string operationId,
            string routeOperationId,
            string sourceStackId,
            WorldItemStackSnapshot carriedStack,
            string leaseId,
            string admissionTokenId,
            long massGrams,
            int sourceOffsetQuantity,
            Vector2Int targetCell,
            string destinationId)
        {
            Actor = actor.Actor;
            Inventory = actor.Inventory;
            Ability = actor.Ability;
            ActorCell = actor.Cell;
            ActorId = actor.Actor.Identity.PersistentId;
            OperationId = operationId;
            RouteOperationId = routeOperationId;
            SourceStackId = sourceStackId;
            CarriedStack = carriedStack;
            LeaseId = leaseId;
            AdmissionTokenId = admissionTokenId;
            MassGrams = massGrams;
            SourceOffsetQuantity = sourceOffsetQuantity;
            TargetCell = targetCell;
            DestinationId = destinationId;
        }

        internal CharacterActor Actor { get; }
        internal CharacterCarryInventory Inventory { get; }
        internal AbilityHaul Ability { get; }
        internal Vector2Int ActorCell { get; }
        internal string ActorId { get; }
        internal string OperationId { get; }
        internal string RouteOperationId { get; }
        internal string SourceStackId { get; }
        internal WorldItemStackSnapshot CarriedStack { get; }
        internal string LeaseId { get; }
        internal string AdmissionTokenId { get; }
        internal long MassGrams { get; }
        internal int SourceOffsetQuantity { get; }
        internal Vector2Int TargetCell { get; }
        internal string DestinationId { get; }
    }

    private sealed class TestWarehouseFacility : IWarehouseFacility
    {
        internal TestWarehouseFacility(
            BuildingInstanceId id,
            WarehouseInventory inventory)
        {
            PersistentInstanceId = id;
            Inventory = inventory;
        }

        public BuildingInstanceId PersistentInstanceId { get; }
        public WarehouseInventory Inventory { get; }
        public bool HasWarehouseInventory => true;
    }

    private sealed class TestWarehouseWorldQuery : IWarehouseWorldQuery
    {
        private readonly IReadOnlyList<IWarehouseFacility> warehouses;

        internal TestWarehouseWorldQuery(params IWarehouseFacility[] warehouses)
        {
            this.warehouses = warehouses ?? Array.Empty<IWarehouseFacility>();
        }

        public int WarehouseVersion => 1;
        public IReadOnlyList<IWarehouseFacility> Warehouses => warehouses;
    }

    private sealed class TestClock : IGameClock
    {
        public float CurrentTime => 0f;
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class TestGridProvider : IGridSystemProvider
    {
        private readonly Grid grid;

        internal TestGridProvider(Grid grid)
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

        public bool TryGetGrid(out Grid value)
        {
            value = grid;
            return true;
        }
    }

    private sealed class TestHaulingSettings : IItemHaulingSettingsProvider
    {
        public float MaxCarryMultiplier { get; private set; } = 1.5f;

        public ItemHaulingSettingsSnapshot Capture() => new()
        {
            maxCarryMultiplier = MaxCarryMultiplier
        };

        public void Restore(ItemHaulingSettingsSnapshot snapshot)
        {
            MaxCarryMultiplier = CharacterCarryTuning.ClampMaxCarryMultiplier(
                snapshot?.maxCarryMultiplier ?? 1.5f);
        }
    }
}
#endif
