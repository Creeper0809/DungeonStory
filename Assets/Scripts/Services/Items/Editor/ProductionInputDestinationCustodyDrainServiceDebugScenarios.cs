#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class ProductionInputDestinationCustodyDrainServiceDebugScenarios
{
    private const string ItemId = "item:buffer";
    private const string ParentOperationId =
        "production-facility-destructive-drain:qa:input-custody-live";
    private const string StepOperationId =
        "production-facility-destructive-drain-step:qa:input-custody-live";
    private const string OwnerStableId =
        "production-bill-owner:qa:input-custody-live";
    private const string BillId = "production-bill:qa:input-custody-live";
    private const string FacilityId =
        "building:qa:input-custody-live";
    private const string SourceDestinationId =
        "production-input:building:qa:input-custody-live";
    private const string ActorId = "character:qa:input-custody-live";
    private static readonly Vector2Int OwnerCell = new(6, 1);
    private static readonly Vector2Int ActorCell = new(2, 1);

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Input Custody Live Drain")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("PRODUCTION_INPUT_CUSTODY_LIVE_DRAIN=PASS");
    }

    public static void RunAll()
    {
        VerifyOneShotSnapshotBuildAndTamperRejection();
        VerifySynchronousCloseAndRestoreReplay();
        VerifyDropFailureRetainsExactOwnership();
    }

    internal static string RunDropFailureRecoveryPendingRetryFocused()
    {
        VerifyDropFailureRetainsExactOwnership();
        return "drop-publication-failure=recovery-pending; "
            + "retry=exact-once; acknowledgement=exact";
    }

    private static void VerifyOneShotSnapshotBuildAndTamperRejection()
    {
        using Fixture fixture = Fixture.Create();
        fixture.CreateActiveCargo();
        Require(fixture.Service.TryCaptureSource(
                SourceDestinationId,
                out ProductionInputDestinationCustodySourceSnapshot snapshot,
                out string captureFailure),
            "One-shot source capture failed: " + captureFailure);
        Require(fixture.WorldProxy.CaptureBarrierObserved,
            "Source closure was read without the reservation capture barrier.");
        Require(fixture.Service.TryBuildRequest(
                ParentOperationId,
                StepOperationId,
                OwnerStableId,
                BillId,
                FacilityId,
                OwnerCell,
                Digest('c'),
                snapshot,
                out ProductionInputDestinationCustodyDrainRequest split,
                out string buildFailure),
            "Captured source did not build an exact request: " + buildFailure);
        ProductionInputDestinationCustodyDrainRequest wrapper =
            fixture.CaptureRequest();
        Require(string.Equals(
                split.RequestFingerprint,
                wrapper.RequestFingerprint,
                StringComparison.Ordinal),
            "Split source/build did not match the compatibility capture wrapper.");

        ProductionInputDestinationCustodySourceSnapshot stale = new(
            snapshot.SourceDestinationId,
            snapshot.MassAuthorityRevision + 1L,
            snapshot.SourceOwnershipFingerprint,
            snapshot.SourceStacks,
            snapshot.SourceOperations,
            snapshot.SourceActors,
            snapshot.InputQuantity,
            snapshot.InputMassGrams);
        Require(!fixture.Service.TryBuildRequest(
                ParentOperationId,
                StepOperationId,
                OwnerStableId,
                BillId,
                FacilityId,
                OwnerCell,
                Digest('c'),
                stale,
                out _,
                out _),
            "A stale mass-authority snapshot built a request.");

        snapshot.SourceStacks[0].massGrams++;
        Require(!fixture.Service.TryBuildRequest(
                ParentOperationId,
                StepOperationId,
                OwnerStableId,
                BillId,
                FacilityId,
                OwnerCell,
                Digest('c'),
                snapshot,
                out _,
                out _),
            "A mutated source snapshot built a request.");
    }

    private static void VerifySynchronousCloseAndRestoreReplay()
    {
        using Fixture fixture = Fixture.Create();
        CargoContext cargo = fixture.CreateActiveCargo();
        ProductionInputDestinationCustodyDrainRequest request =
            fixture.CaptureRequest();
        Require(fixture.Service.RequiresImmediateRecoveryBeforeGameplayTick,
            "Live input drain did not require recovery before gameplay ticks.");
        Require(fixture.Service.TryPrepare(request).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Live input drain prepare failed.");
        fixture.RequirePhase(
            ProductionInputDestinationCustodyDrainPhase.Prepared);

        ProductionInputDestinationCustodyDrainResult began = fixture.Service
            .TryCommit(StepOperationId, request.RequestFingerprint);
        Require(began.Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Prepared drain did not synchronously enter actor release: "
            + began.FailureReason);
        fixture.RequirePhase(
            ProductionInputDestinationCustodyDrainPhase.ReleasingActors);

        DungeonPhysicalItemSaveData midDrainPhysical = fixture.Runtime.Capture();
        CharacterCarryInventorySaveData midDrainCarry =
            cargo.Inventory.Capture();
        cargo = fixture.RestoreActiveCargo(midDrainPhysical, midDrainCarry);
        Require(fixture.Service.TryPrepare(request).Status ==
                ProductionInputDestinationCustodyDrainStatus.Replay,
            "Restored prepared request did not replay idempotently.");

        bool gameplayOpportunityObserved = false;
        ProductionInputDestinationCustodyDrainResult committed = fixture
            .CommitSynchronously(request, () =>
            {
                gameplayOpportunityObserved = true;
                fixture.RequireActorAndOperationClosed(cargo);
            });
        Require(gameplayOpportunityObserved,
            "Fixture did not evaluate the post-command gameplay boundary.");
        Require(committed.Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Restored live drain did not commit: " + committed.FailureReason);
        fixture.RequireExactReleasedPhysical(request, cargo);

        DungeonPhysicalItemSaveData terminalPhysical = fixture.Runtime.Capture();
        fixture.RestoreWithoutActiveCargo(terminalPhysical);
        ProductionInputDestinationCustodyDrainResult terminalReplay = fixture
            .Service.TryCommit(StepOperationId, request.RequestFingerprint);
        Require(terminalReplay.Status ==
                    ProductionInputDestinationCustodyDrainStatus.Replay
                && string.Equals(
                    terminalReplay.CommitId,
                    committed.CommitId,
                    StringComparison.Ordinal)
                && string.Equals(
                    terminalReplay.ReceiptFingerprint,
                    committed.ReceiptFingerprint,
                    StringComparison.Ordinal),
            "Restored terminal drain did not replay its exact receipt.");

        ProductionInputDestinationCustodyDrainResult acknowledged = fixture
            .Service.TryAcknowledge(
                StepOperationId,
                committed.ReceiptFingerprint);
        Require(acknowledged.Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Terminal drain acknowledgement failed.");
        fixture.RequirePhase(
            ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc);

        DungeonPhysicalItemSaveData acknowledgedPhysical =
            fixture.Runtime.Capture();
        fixture.RestoreWithoutActiveCargo(acknowledgedPhysical);
        Require(fixture.Service.TryAcknowledge(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Replay,
            "Restored bill acknowledgement did not replay idempotently.");
        Require(fixture.Service.TryGarbageCollect(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Acknowledged drain checkpoint garbage collection failed.");
        Require(fixture.Service.TryGarbageCollect(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Replay,
            "Collected drain did not replay as an idempotent absence.");
    }

    private static void VerifyDropFailureRetainsExactOwnership()
    {
        using Fixture fixture = Fixture.Create();
        CargoContext cargo = fixture.CreateActiveCargo();
        ProductionInputDestinationCustodyDrainRequest request =
            fixture.CaptureRequest();
        Require(fixture.Service.TryPrepare(request).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Drop-failure fixture prepare failed.");
        Require(fixture.Service.TryCommit(
                    StepOperationId,
                    request.RequestFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Drop-failure fixture did not enter actor release.");

        fixture.WorldProxy.FailCarryDrops = true;
        bool logging = Debug.unityLogger.logEnabled;
        ProductionInputDestinationCustodyDrainResult deferred;
        try
        {
            Debug.unityLogger.logEnabled = false;
            deferred = fixture.Service.TryCommit(
                StepOperationId,
                request.RequestFingerprint);
        }
        finally
        {
            Debug.unityLogger.logEnabled = logging;
        }
        Require(deferred.Status ==
                ProductionInputDestinationCustodyDrainStatus.Deferred,
            "Injected carried-drop failure did not defer the drain.");
        fixture.RequireDropFailureOwnershipIntact(cargo);

        DungeonPhysicalItemSaveData pendingPhysical = fixture.Runtime.Capture();
        CharacterCarryInventorySaveData pendingCarry = cargo.Inventory.Capture();
        cargo = fixture.RestoreActiveCargo(pendingPhysical, pendingCarry);
        fixture.RequireDropFailureOwnershipIntact(cargo);

        fixture.WorldProxy.FailCarryDrops = false;
        ProductionInputDestinationCustodyDrainResult committed = fixture
            .CommitSynchronously(request, () =>
                fixture.RequireActorAndOperationClosed(cargo));
        Require(committed.Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Drop-failure retry did not roll forward to completion: "
            + committed.FailureReason);
        fixture.RequireExactReleasedPhysical(request, cargo);
        ProductionInputDestinationCustodyDrainResult acknowledged = fixture
            .Service.TryAcknowledge(
                StepOperationId,
                committed.ReceiptFingerprint);
        Require(acknowledged.Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Drop-failure recovery acknowledgement failed: "
            + acknowledged.FailureReason);
        Require(fixture.Service.TryAcknowledge(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Replay,
            "Drop-failure recovery acknowledgement was not idempotent.");
    }

    private static string Digest(char value) => new(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CharacterCarryInventoryRegistry carryRegistry;
        private readonly ItemQuantityReservationService reservations;
        private readonly WorldItemRepository repository;
        private readonly ItemTransferService transfers;
        private readonly TestCharacterWorldQuery characters = new();
        private readonly Grid grid;
        private readonly TestGridProvider gridProvider;
        private readonly TestHaulingSettings haulingSettings = new();
        private GameObject actorObject;
        private bool disposed;

        private Fixture(
            WorldItemStackRuntime runtime,
            WorldItemRepository repository,
            ItemQuantityReservationService reservations,
            ItemTransferService transfers,
            CharacterCarryInventoryRegistry carryRegistry,
            DrainWorldRuntimeProxy worldProxy,
            IDrainWorldRuntime drainWorld,
            Grid grid,
            TestGridProvider gridProvider)
        {
            Runtime = runtime;
            this.repository = repository;
            this.reservations = reservations;
            this.transfers = transfers;
            this.carryRegistry = carryRegistry;
            WorldProxy = worldProxy;
            DrainWorld = drainWorld;
            this.grid = grid;
            this.gridProvider = gridProvider;
            WorldProxy.CaptureBarrierProbe = () =>
                this.reservations.IsCaptureBarrierActive;
            RebuildService();
        }

        internal WorldItemStackRuntime Runtime { get; }
        internal DrainWorldRuntimeProxy WorldProxy { get; }
        internal IDrainWorldRuntime DrainWorld { get; }
        internal ProductionInputDestinationCustodyDrainService Service
            { get; private set; }

        internal static Fixture Create()
        {
            WorldItemStackRuntime runtime = null;
            CharacterCarryInventoryRegistry carryRegistry = null;
            try
            {
                runtime = CreatePhysicalRuntime(
                    out WorldItemRepository repository,
                    out ItemQuantityReservationService reservations,
                    out IReservedItemTransferService reservedTransfer);
                ItemTransferService transfers =
                    reservedTransfer as ItemTransferService
                    ?? throw new InvalidOperationException(
                        "Cross-domain fixture did not expose ItemTransferService.");
                carryRegistry = new CharacterCarryInventoryRegistry();
                Grid grid = CreateWalkableGrid();
                TestGridProvider gridProvider = new(grid);
                IDrainWorldRuntime drainWorld =
                    DispatchProxy.Create<IDrainWorldRuntime,
                        DrainWorldRuntimeProxy>();
                DrainWorldRuntimeProxy proxy =
                    (DrainWorldRuntimeProxy)(object)drainWorld;
                proxy.Target = runtime;
                return new Fixture(
                    runtime,
                    repository,
                    reservations,
                    transfers,
                    carryRegistry,
                    proxy,
                    drainWorld,
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

        internal CargoContext CreateActiveCargo()
        {
            CharacterActor actor = CreateActor(ActorId, ActorCell);
            CharacterCarryInventory inventory = actor.CarryInventory;
            AbilityHaul ability = actor.GetComponent<AbilityHaul>();
            string operationId = repository
                .AllocateEditorTestHaulDeliveryOperationId(ActorId);
            string sourceStackId = repository.AddEditorTestStack(
                ItemId,
                2,
                WorldItemStackState.Loose,
                position: ActorCell);
            string signature = ItemReservationSignature.Create(
                ItemId,
                Array.Empty<ItemInstanceComponentSaveData>());
            Require(reservations.TryReserve(
                    operationId,
                    ActorId,
                    ItemReservationPurpose.Hauling,
                    $"haul:{WorldItemHaulDestinationKind.FacilityBuffer}:"
                    + SourceDestinationId,
                    new ItemQuantityReservationRequest(
                        new ItemStackId(sourceStackId),
                        2,
                        signature),
                    out ItemQuantityLease lease,
                    out DomainFailure reserveFailure),
                "Fixture quantity reservation failed: " + reserveFailure);
            Require(Runtime.TryRegisterHaulDeliveryPlanForEditorTest(
                    operationId,
                    ActorId,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    SourceDestinationId,
                    OwnerCell,
                    OwnerCell,
                    out string planFailure),
                "Fixture durable haul intent failed: " + planFailure);

            WorldItemReservedStackQuantity reservation = new(
                sourceStackId,
                ItemId,
                2,
                ActorCell,
                WorldItemHaulDestinationKind.FacilityBuffer,
                SourceDestinationId,
                lease.leaseId,
                operationId);
            Require(Runtime.TryPickupReservedStackQuantity(
                    actor,
                    inventory,
                    reservation,
                    out int picked,
                    out string pickupFailure)
                && picked == 2,
                "Fixture committed pickup failed: " + pickupFailure);
            Require(Runtime.TryCommitHaulPickup(
                    operationId,
                    inventory,
                    out string commitFailure),
                "Fixture haul commitment failed: " + commitFailure);
            CharacterCarriedItemSaveData carried = inventory.Items.Single(
                value => string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal));
            BindActivePlan(actor, operationId, carried, lease.leaseId);

            string bufferStackId = repository.AddEditorTestStack(
                ItemId,
                3,
                WorldItemStackState.FacilityBuffer,
                SourceDestinationId,
                position: OwnerCell);
            return new CargoContext(
                actor,
                inventory,
                ability,
                operationId,
                carried.carriedStackId,
                bufferStackId,
                lease.leaseId);
        }

        internal ProductionInputDestinationCustodyDrainRequest CaptureRequest()
        {
            Require(Service.TryCaptureRequest(
                    ParentOperationId,
                    StepOperationId,
                    OwnerStableId,
                    BillId,
                    FacilityId,
                    SourceDestinationId,
                    OwnerCell,
                    Digest('c'),
                    out ProductionInputDestinationCustodyDrainRequest request,
                    out string failureReason),
                "Live input drain request capture failed: " + failureReason);
            Require(request.SourceActors.Count == 1
                    && request.SourceOperations.Count == 1
                    && request.SourceStacks.Count == 2,
                "Live request did not freeze the complete actor/operation/stack set.");
            return request;
        }

        internal CargoContext RestoreActiveCargo(
            DungeonPhysicalItemSaveData physical,
            CharacterCarryInventorySaveData carry)
        {
            Require(Service.TryCapture(
                    StepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData drain),
                "Mid-drain restore source did not retain its durable record.");
            string bufferStackId = drain.sourceStacks.Single(value =>
                value.state == WorldItemStackState.FacilityBuffer).stackId;
            DestroyActor();
            Runtime.Restore(physical);
            RebuildService();
            CharacterActor actor = CreateActor(ActorId, ActorCell);
            actor.CarryInventory.Restore(carry);
            CharacterCarriedItemSaveData carried = actor.CarryInventory.Items
                .Single();
            Require(reservations.TryGetLeasesByOwner(
                    carried.ownerOperationId,
                    out IReadOnlyList<ItemQuantityLease> leases)
                && leases.Count == 1,
                "Restore did not rebuild the exact operation lease authority.");
            BindActivePlan(
                actor,
                carried.ownerOperationId,
                carried,
                leases[0].leaseId);
            return new CargoContext(
                actor,
                actor.CarryInventory,
                actor.GetComponent<AbilityHaul>(),
                carried.ownerOperationId,
                carried.carriedStackId,
                bufferStackId,
                leases[0].leaseId);
        }

        internal void RestoreWithoutActiveCargo(
            DungeonPhysicalItemSaveData physical)
        {
            DestroyActor();
            Runtime.Restore(physical);
            RebuildService();
        }

        internal ProductionInputDestinationCustodyDrainResult
            CommitSynchronously(
                ProductionInputDestinationCustodyDrainRequest request,
                Action gameplayOpportunity)
        {
            List<ProductionInputDestinationCustodyDrainPhase> observed = new();
            ProductionInputDestinationCustodyDrainResult result = default;
            for (int call = 0; call < 16; call++)
            {
                Require(Service.TryCapture(
                        StepOperationId,
                        out ProductionInputDestinationCustodyDrainSaveData before),
                    "Drain disappeared before reaching its terminal receipt.");
                observed.Add(before.phase);
                result = Service.TryCommit(
                    StepOperationId,
                    request.RequestFingerprint);
                if (result.Status is
                        ProductionInputDestinationCustodyDrainStatus.Deferred
                    or ProductionInputDestinationCustodyDrainStatus.Conflict)
                {
                    break;
                }
                if (!string.IsNullOrEmpty(result.CommitId)
                    && !string.IsNullOrEmpty(result.ReceiptFingerprint))
                {
                    break;
                }
            }
            Require(result.Status is
                    ProductionInputDestinationCustodyDrainStatus.Applied
                    or ProductionInputDestinationCustodyDrainStatus.Replay
                && !string.IsNullOrEmpty(result.CommitId),
                "Synchronous recovery left the drain open: "
                + result.FailureReason);
            Require(observed.Contains(
                        ProductionInputDestinationCustodyDrainPhase
                            .ReleasingActors)
                    && observed.Contains(
                        ProductionInputDestinationCustodyDrainPhase
                            .ReleasingOperationAuthority)
                    && observed.Contains(
                        ProductionInputDestinationCustodyDrainPhase
                            .ReleasingDestination),
                "Synchronous recovery skipped a durable live-effect phase.");
            gameplayOpportunity?.Invoke();
            return result;
        }

        internal void RequireActorAndOperationClosed(CargoContext cargo)
        {
            Require(cargo.Inventory.Items.Count == 0
                    && !cargo.Ability.OwnsHaulOperation(cargo.OperationId),
                "A gameplay opportunity observed active actor custody.");
            Require(!Runtime.TryCaptureHaulDeliveryIntent(
                        cargo.OperationId,
                        out _)
                    && (!reservations.TryGetLeasesByOwner(
                            cargo.OperationId,
                            out IReadOnlyList<ItemQuantityLease> leases)
                        || leases.Count == 0),
                "A gameplay opportunity observed active operation authority.");
        }

        internal void RequireDropFailureOwnershipIntact(CargoContext cargo)
        {
            Require(cargo.Inventory.Items.Count == 1
                    && cargo.Ability.OwnsHaulOperation(cargo.OperationId),
                "Failed drop mutated actor cargo or its active operation.");
            Require(Runtime.TryCaptureHaulDeliveryIntent(
                        cargo.OperationId,
                        out _)
                    && reservations.TryGetLeasesByOwner(
                        cargo.OperationId,
                        out IReadOnlyList<ItemQuantityLease> leases)
                    && leases.Count == 1
                    && string.Equals(
                        leases[0].leaseId,
                        cargo.LeaseId,
                        StringComparison.Ordinal),
                "Failed drop released intent or lease ownership.");
            WorldItemStackSnapshot carried = Runtime.GetAllStacks().Single(value =>
                string.Equals(
                    value.StackId,
                    cargo.CarriedStackId,
                    StringComparison.Ordinal));
            Require(carried.State == WorldItemStackState.Carried
                    && carried.Position == ActorCell
                    && string.Equals(
                        carried.DestinationId,
                        ActorId,
                        StringComparison.Ordinal),
                "Failed drop moved or detached the carried physical stack.");
            Require(Service.TryCapture(
                    StepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData drain)
                && drain.phase ==
                    ProductionInputDestinationCustodyDrainPhase.ReleasingActors
                && drain.completedActorIds.Count == 0
                && drain.releasedOperationIds.Count == 0,
                "Failed drop advanced durable progress without its effect.");
        }

        internal void RequireExactReleasedPhysical(
            ProductionInputDestinationCustodyDrainRequest request,
            CargoContext cargo)
        {
            Require(Service.TryCapture(
                    StepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData drain)
                && drain.phase == ProductionInputDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingBillAck,
                "Committed live drain did not retain its terminal receipt.");
            Require(drain.releasedQuantity == request.InputQuantity
                    && drain.releasedMassGrams == request.InputMassGrams
                    && drain.releasedStackIds.OrderBy(value => value,
                            StringComparer.Ordinal)
                        .SequenceEqual(
                            request.SourceStacks.Select(value => value.stackId)
                                .OrderBy(value => value,
                                    StringComparer.Ordinal),
                            StringComparer.Ordinal),
                "Committed live drain changed exact quantity, mass, or identities.");
            WorldItemStackSnapshot carried = Runtime.GetAllStacks().Single(value =>
                string.Equals(
                    value.StackId,
                    cargo.CarriedStackId,
                    StringComparison.Ordinal));
            WorldItemStackSnapshot buffered = Runtime.GetAllStacks().Single(value =>
                string.Equals(
                    value.StackId,
                    cargo.BufferStackId,
                    StringComparison.Ordinal));
            Require(carried.State == WorldItemStackState.Loose
                    && carried.Position == ActorCell
                    && string.IsNullOrEmpty(carried.DestinationId),
                "Carried input teleported instead of dropping at the actor cell.");
            Require(buffered.State == WorldItemStackState.Loose
                    && buffered.Position == OwnerCell
                    && string.IsNullOrEmpty(buffered.DestinationId),
                "Buffered input teleported away from its owner cell.");
            int quantity = checked(carried.Quantity + buffered.Quantity);
            long mass = checked(MassOf(carried) + MassOf(buffered));
            Require(quantity == request.InputQuantity
                    && mass == request.InputMassGrams,
                "Released physical graph violated exact quantity or gram mass.");
        }

        internal void RequirePhase(
            ProductionInputDestinationCustodyDrainPhase phase)
        {
            Require(Service.TryCapture(
                    StepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData drain)
                && drain.phase == phase,
                "Unexpected live drain phase; expected " + phase + ".");
        }

        private void BindActivePlan(
            CharacterActor actor,
            string operationId,
            CharacterCarriedItemSaveData carried,
            string leaseId)
        {
            WorldItemReservedStackQuantity reservation = new(
                carried.carriedStackId,
                carried.itemId,
                carried.quantity,
                ActorCell,
                WorldItemHaulDestinationKind.FacilityBuffer,
                SourceDestinationId,
                leaseId,
                operationId);
            WorldItemHaulPlanLeg delivery = new(
                reservation,
                ActorCell,
                null,
                OwnerCell,
                OwnerCell);
            long massGrams = MassOf(Runtime.GetAllStacks().Single(value =>
                string.Equals(
                    value.StackId,
                    carried.carriedStackId,
                    StringComparison.Ordinal)));
            WorldItemHaulPlan plan = new(
                Array.Empty<WorldItemHaulPlanLeg>(),
                new[] { delivery },
                new[] { reservation },
                massGrams / 1000f,
                expectedDetourCost: 0,
                WorldItemHaulDestinationKind.FacilityBuffer,
                SourceDestinationId,
                deliveryOnlyResume: true);
            Require(actor.GetComponent<AbilityHaul>()
                    .TryBindCapacityRoutingEditorFixture(
                        DrainWorld,
                        plan,
                        new[] { leaseId },
                        out string bindFailure),
                "Fixture AbilityHaul binding failed: " + bindFailure);
        }

        private CharacterActor CreateActor(string actorId, Vector2Int position)
        {
            actorObject = new GameObject("Input Custody Drain " + actorId);
            actorObject.SetActive(false);
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterLifecycle lifecycle =
                actorObject.GetComponent<CharacterLifecycle>();
            CharacterCarryInventory inventory =
                actorObject.AddComponent<CharacterCarryInventory>();
            actorObject.AddComponent<AbilityHaul>();
            actorObject.GetComponent<CharacterIdentity>()
                .SetPersistentId(actorId);
            InjectCharacterEditorDependencies(actorObject);
            lifecycle.ConstructCharacterLifecycle(gridProvider);
            actorObject.transform.position = grid.GetWorldPos(position);
            actorObject.SetActive(true);
            actor.EnsureRuntimeState();
            actor.Initialization(RequireAuthoredCharacterDefinition());
            inventory.Configure(
                Runtime.CatalogProvider,
                Runtime.MassQuery,
                haulingSettings,
                carryRegistry);
            characters.CharactersMutable.Add(actor);
            return actor;
        }

        private long MassOf(WorldItemStackSnapshot stack)
        {
            PhysicalItemMassSubject subject =
                PhysicalItemMassSubjectAdapter.Create(
                    Runtime.MassQuery,
                    (ItemDefinitionId)stack.ItemId,
                    stack.ItemInstanceId,
                    stack.Components);
            return Runtime.MassQuery.GetQuantityMass(
                (ItemDefinitionId)stack.ItemId,
                subject,
                stack.Quantity).Value;
        }

        private static WorldItemStackRuntime CreatePhysicalRuntime(
            out WorldItemRepository repository,
            out ItemQuantityReservationService reservations,
            out IReservedItemTransferService reservedTransfer)
        {
            MethodInfo method = typeof(PhysicalItemDebugScenarios)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(value => string.Equals(
                        value.Name,
                        "CreateRuntimeForCrossDomainFixture",
                        StringComparison.Ordinal)
                    && value.GetParameters().Length == 4);
            object[] arguments = { null, null, null, null };
            WorldItemStackRuntime runtime = InvokeEditorFixture<
                WorldItemStackRuntime>(method, null, arguments);
            repository = (WorldItemRepository)arguments[0];
            reservations = (ItemQuantityReservationService)arguments[2];
            reservedTransfer = (IReservedItemTransferService)arguments[3];
            return runtime;
        }

        private static void InjectCharacterEditorDependencies(
            GameObject target)
        {
            Type type = RequireCharacterEditorDependencyType();
            MethodInfo method = type.GetMethods(
                    BindingFlags.Static | BindingFlags.Public)
                .Single(value => string.Equals(
                        value.Name,
                        "Inject",
                        StringComparison.Ordinal)
                    && value.GetParameters().Length == 1
                    && value.GetParameters()[0].ParameterType ==
                        typeof(GameObject));
            InvokeEditorFixture<object>(method, null, new object[] { target });
        }

        private static CharacterSO RequireAuthoredCharacterDefinition()
        {
            Type type = RequireCharacterEditorDependencyType();
            MethodInfo method = type.GetMethods(
                    BindingFlags.Static | BindingFlags.Public)
                .Single(value => string.Equals(
                        value.Name,
                        "RequireAuthoredCharacterDefinition",
                        StringComparison.Ordinal)
                    && value.GetParameters().Length == 2
                    && value.GetParameters()[0].ParameterType ==
                        typeof(string)
                    && value.GetParameters()[1].ParameterType ==
                        typeof(CharacterRole));
            return InvokeEditorFixture<CharacterSO>(
                method,
                null,
                new object[] { "Adventurer", CharacterRole.Regular });
        }

        private static Type RequireCharacterEditorDependencyType() =>
            typeof(PhysicalItemDebugScenarios).Assembly.GetType(
                "CharacterAiEditorTestDependencies",
                throwOnError: true);

        private static T InvokeEditorFixture<T>(
            MethodInfo method,
            object target,
            object[] arguments)
        {
            try
            {
                return (T)method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private void RebuildService()
        {
            ProductionInputDestinationCustodyDrainOutbox outbox = new(repository);
            Service = new ProductionInputDestinationCustodyDrainService(
                outbox,
                DrainWorld,
                reservations,
                characters,
                Runtime.MassQuery,
                transfers,
                reservations);
        }

        private void DestroyActor()
        {
            characters.CharactersMutable.Clear();
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            actorObject = null;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            DestroyActor();
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

    private sealed class CargoContext
    {
        internal CargoContext(
            CharacterActor actor,
            CharacterCarryInventory inventory,
            AbilityHaul ability,
            string operationId,
            string carriedStackId,
            string bufferStackId,
            string leaseId)
        {
            Actor = actor;
            Inventory = inventory;
            Ability = ability;
            OperationId = operationId;
            CarriedStackId = carriedStackId;
            BufferStackId = bufferStackId;
            LeaseId = leaseId;
        }

        internal CharacterActor Actor { get; }
        internal CharacterCarryInventory Inventory { get; }
        internal AbilityHaul Ability { get; }
        internal string OperationId { get; }
        internal string CarriedStackId { get; }
        internal string BufferStackId { get; }
        internal string LeaseId { get; }
    }

    public interface IDrainWorldRuntime :
        IWorldItemStackRuntime,
        IWorldItemCarryRecoveryRuntime,
        IWorldItemQuantityLeaseRuntime
    {
    }

    public class DrainWorldRuntimeProxy : DispatchProxy
    {
        internal WorldItemStackRuntime Target { get; set; }
        internal bool FailCarryDrops { get; set; }
        internal Func<bool> CaptureBarrierProbe { get; set; }
        internal bool CaptureBarrierObserved { get; private set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));
            if (string.Equals(
                    targetMethod.Name,
                    "GetAllStacks",
                    StringComparison.Ordinal))
            {
                CaptureBarrierObserved |= CaptureBarrierProbe?.Invoke() == true;
            }
            if (FailCarryDrops
                && string.Equals(
                    targetMethod.Name,
                    nameof(IWorldItemCarryRecoveryRuntime.TryDropCarriedItems),
                    StringComparison.Ordinal))
            {
                args[^1] = "qa-injected-carried-drop-failure";
                return false;
            }
            try
            {
                return targetMethod.Invoke(Target, args);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }

    private sealed class TestCharacterWorldQuery : ICharacterWorldQuery
    {
        internal List<CharacterActor> CharactersMutable { get; } = new();
        public int CharacterVersion => CharactersMutable.Count;
        public IReadOnlyList<CharacterActor> Characters => CharactersMutable;
    }

    private sealed class TestGridProvider : IGridSystemProvider
    {
        private readonly Grid grid;

        internal TestGridProvider(Grid grid) =>
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));

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
