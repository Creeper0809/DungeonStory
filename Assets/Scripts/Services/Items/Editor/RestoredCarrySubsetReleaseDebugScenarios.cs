#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class RestoredCarrySubsetReleaseDebugScenarios
{
    private const string ItemId = "item:buffer";
    private const string ActorId = "character:qa:restored-carry-release";
    private const string DestinationId =
        "production-input:building:qa:restored-carry-release";
    private static readonly Vector2Int ActorCell = new(3, 1);
    private static readonly Vector2Int DestinationCell = new(8, 1);

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Restored Carry Subset Release")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("RESTORED_CARRY_SUBSET_RELEASE=PASS");
    }

    public static void RunAll()
    {
        VerifyExactDetachedRestoreDropsAtDownedCell();
        VerifyMixedOwnerFailsWithoutMutation();
        VerifyUnownedCargoFailsWithoutMutation();
        VerifyForeignOwnerFailsWithoutMutation();
        VerifyActivePlanDelegatesToExistingStopPath();
    }

    /// <summary>
    /// Exercises Unity lifecycle callbacks against a real AbilityHaul instance.
    /// This must run in PlayMode because the production OnDisable guard is
    /// intentionally inert while editing assets.
    /// </summary>
    public static IEnumerator VerifyLifecycleRecoveryFaultRows(
        ICollection<string> evidence)
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException(
                "AbilityHaul lifecycle recovery requires PlayMode.");
        if (evidence == null)
            throw new ArgumentNullException(nameof(evidence));

        using (Fixture fixture = Fixture.Create())
        {
            Cargo cargo = fixture.CreateCommittedCargo(quantity: 3);
            fixture.BindActivePlan(cargo);
            long expectedMass = fixture.MassOf(cargo.CarriedStackId);

            fixture.Ability.enabled = false;
            yield return null;

            Require(fixture.Inventory.Items.Count == 0,
                "Component disable retained logical carried cargo.");
            fixture.RequireOperationAuthorityAbsent(cargo.OperationId);
            WorldItemStackSnapshot dropped = fixture.RequireStack(
                cargo.CarriedStackId);
            Require(dropped.State == WorldItemStackState.Loose
                    && dropped.Position == ActorCell
                    && dropped.Quantity == cargo.Quantity
                    && fixture.MassOf(dropped.StackId) == expectedMass,
                "Component disable changed drop cell, quantity, or gram mass.");
            fixture.RequirePhysicalRestoreExact(cargo, expectedMass);
            evidence.Add(
                "ABILITY_HAUL_COMPONENT_DISABLE_CURRENT_CELL_RECOVERY=PASS");
            evidence.Add(
                "ABILITY_HAUL_COMPONENT_DISABLE_AUTHORITY_RELEASED=PASS");
            evidence.Add(
                "ABILITY_HAUL_COMPONENT_DISABLE_CURRENT_FORMAT_RESTORE_EXACT=PASS");
        }

        using (Fixture fixture = Fixture.Create())
        {
            Cargo cargo = fixture.CreateCommittedCargo(quantity: 2);
            fixture.BindActivePlan(cargo);
            long expectedMass = fixture.MassOf(cargo.CarriedStackId);

            UnityEngine.Object.Destroy(fixture.Actor.gameObject);
            yield return null;

            fixture.RequireOperationAuthorityAbsent(cargo.OperationId);
            WorldItemStackSnapshot dropped = fixture.RequireStack(
                cargo.CarriedStackId);
            Require(dropped.State == WorldItemStackState.Loose
                    && dropped.Position == ActorCell
                    && dropped.Quantity == cargo.Quantity
                    && fixture.MassOf(dropped.StackId) == expectedMass,
                "GameObject destroy changed drop cell, quantity, or gram mass.");
            fixture.RequirePhysicalRestoreExact(cargo, expectedMass);
            evidence.Add(
                "ABILITY_HAUL_GAMEOBJECT_DESTROY_CURRENT_CELL_RECOVERY=PASS");
            evidence.Add(
                "ABILITY_HAUL_GAMEOBJECT_DESTROY_AUTHORITY_RELEASED=PASS");
            evidence.Add(
                "ABILITY_HAUL_GAMEOBJECT_DESTROY_CURRENT_FORMAT_RESTORE_EXACT=PASS");
        }
    }

    public static void VerifyAdditionalHaulFaultRows(
        ICollection<string> evidence)
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException(
                "Additional AbilityHaul fault evidence requires PlayMode.");
        if (evidence == null)
            throw new ArgumentNullException(nameof(evidence));

        VerifyActiveReplanRetainsExactOwnership();
        evidence.Add("ABILITY_HAUL_ACTIVE_REPLAN_EXACT_OWNERSHIP=PASS");

        VerifyActivePlanDelegatesToExistingStopPath();
        evidence.Add("ABILITY_HAUL_CARRIED_CANCEL_CURRENT_CELL_RECOVERY=PASS");
        evidence.Add("ABILITY_HAUL_CARRIED_CANCEL_AUTHORITY_RELEASED=PASS");
        evidence.Add("ABILITY_HAUL_CARRIED_CANCEL_CURRENT_FORMAT_RESTORE_EXACT=PASS");

        VerifyExactDetachedRestoreDropsAtDownedCell();
        evidence.Add("ABILITY_HAUL_DOWNED_CURRENT_CELL_RECOVERY=PASS");
        evidence.Add("ABILITY_HAUL_DOWNED_AUTHORITY_RELEASED=PASS");
        evidence.Add("ABILITY_HAUL_DOWNED_PHYSICAL_SAVE_EXACT=PASS");
        evidence.Add("ABILITY_HAUL_DOWNED_CURRENT_FORMAT_RESTORE_EXACT=PASS");

        string oneGram = FacilityBufferMassAdmissionDebugScenarios
            .RunOneGramClearanceFocused();
        Require(!string.IsNullOrWhiteSpace(oneGram),
            "One-gram facility-buffer focused evidence was empty.");
        evidence.Add("FACILITY_BUFFER_ONE_GRAM_TYPED_BOUNDARY=PASS");
    }

    private static void VerifyExactDetachedRestoreDropsAtDownedCell()
    {
        using Fixture fixture = Fixture.Create();
        fixture.PrimeEditorRuntimeBinding();
        Cargo cargo = fixture.CreateCommittedCargo(quantity: 3);
        fixture.RestoreDetachedCarry();
        fixture.Actor.SetLifecycleState(CharacterLifecycleState.Downed);

        long expectedMass = fixture.MassOf(cargo.CarriedStackId);
        fixture.RequireOperationAuthorityPresent(cargo);
        Require(fixture.Ability.ActiveReservationsForDiagnostics.Count == 0
                && !fixture.Ability.OwnsHaulOperation(cargo.OperationId),
            "Detached restore unexpectedly retained an active haul plan.");
        Require(fixture.Ability
                .TryStopHaulingOrReleaseRestoredCarryIfOperationsSubsetOf(
                    new[] { cargo.OperationId },
                    "qa-restored-carry-destination-drain",
                    HaulInterruptionDisposition
                        .ReleaseUnpickedAndDropCarriedAtActor,
                    out string failureReason),
            "Exact detached restore cargo was not released: " + failureReason);

        Require(fixture.Inventory.Items.Count == 0,
            "Exact detached restore release left logical carried cargo behind.");
        WorldItemStackSnapshot dropped = fixture.RequireStack(
            cargo.CarriedStackId);
        Require(dropped.State == WorldItemStackState.Loose
                && dropped.Position == ActorCell
                && dropped.Quantity == cargo.Quantity
                && fixture.MassOf(dropped.StackId) == expectedMass,
            "Detached restore release changed position, quantity, or gram mass.");
        Require(dropped.DropDisposition ==
                    WorldItemDropDisposition.TransientCarryRecoveryDrop
                && string.Equals(
                    dropped.RecoveryOwnerOperationId,
                    cargo.OperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    dropped.RecoverySourceStackId,
                    cargo.SourceStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    dropped.RecoveryCarrierPersistentId,
                    ActorId,
                    StringComparison.Ordinal)
                && dropped.RecoveryInterruptionKind ==
                    WorldItemCarryInterruptionKind.Downed
                && dropped.RecoveryDeadlineGameTime >
                    dropped.DroppedAtGameTime,
            "Downed detached restore release lost typed recovery provenance.");
        fixture.RequireOperationAuthorityAbsent(cargo.OperationId);
        fixture.RequirePhysicalRestoreExact(cargo, expectedMass);
    }

    private static void VerifyActiveReplanRetainsExactOwnership()
    {
        using Fixture fixture = Fixture.Create();
        Cargo cargo = fixture.CreateCommittedCargo(quantity: 2);
        fixture.BindActivePlan(cargo);
        long expectedMass = fixture.MassOf(cargo.CarriedStackId);

        Require(fixture.Ability.TryStopHauling(
                "qa-active-replan-exact-ownership",
                HaulInterruptionDisposition
                    .ReleaseUnpickedAndRetainCarriedForReplan,
                out string failureReason),
            "Active replan did not retain carried ownership: "
            + failureReason);
        CharacterCarriedItemSaveData carried = fixture.Inventory.Items.Single(
            value => string.Equals(
                value.ownerOperationId,
                cargo.OperationId,
                StringComparison.Ordinal));
        WorldItemStackSnapshot physical = fixture.RequireStack(
            cargo.CarriedStackId);
        Require(carried.quantity == cargo.Quantity
                && fixture.Ability.OwnsHaulOperation(cargo.OperationId)
                && fixture.Ability.HasBoundDeliveryIntent
                && fixture.Ability.LastInterruptionDisposition ==
                    HaulInterruptionDisposition
                        .ReleaseUnpickedAndRetainCarriedForReplan
                && physical.State == WorldItemStackState.Carried
                && physical.Position == ActorCell
                && physical.Quantity == cargo.Quantity
                && fixture.MassOf(physical.StackId) == expectedMass,
            "Active replan changed logical cargo, physical quantity, mass, or intent ownership.");
        fixture.RequireOperationAuthorityPresent(cargo);
    }

    private static void VerifyMixedOwnerFailsWithoutMutation()
    {
        using Fixture fixture = Fixture.Create();
        fixture.PrimeEditorRuntimeBinding();
        Cargo allowed = fixture.CreateCommittedCargo(quantity: 1);
        Cargo foreign = fixture.CreateCommittedCargo(quantity: 2);
        fixture.RestoreDetachedCarry();
        fixture.RequireRejectedWithoutMutation(
            new[] { allowed.OperationId },
            new[] { allowed, foreign },
            "mixed-or-unowned-restored-carry:");
    }

    private static void VerifyUnownedCargoFailsWithoutMutation()
    {
        using Fixture fixture = Fixture.Create();
        fixture.PrimeEditorRuntimeBinding();
        Cargo cargo = fixture.CreateCommittedCargo(quantity: 2);
        fixture.RestoreDetachedCarry();
        CharacterCarryInventorySaveData carry = fixture.Inventory.Capture();
        carry.items.Single().ownerOperationId = string.Empty;
        fixture.Inventory.Restore(carry);

        fixture.RequireRejectedWithoutMutation(
            new[] { cargo.OperationId },
            new[] { cargo },
            "mixed-or-unowned-restored-carry:");
    }

    private static void VerifyForeignOwnerFailsWithoutMutation()
    {
        using Fixture fixture = Fixture.Create();
        fixture.PrimeEditorRuntimeBinding();
        Cargo foreign = fixture.CreateCommittedCargo(quantity: 2);
        fixture.RestoreDetachedCarry();
        fixture.RequireRejectedWithoutMutation(
            new[] { "haul:character:qa:other:000000000001" },
            new[] { foreign },
            "mixed-or-unowned-restored-carry:");
    }

    private static void VerifyActivePlanDelegatesToExistingStopPath()
    {
        using Fixture fixture = Fixture.Create();
        Cargo cargo = fixture.CreateCommittedCargo(quantity: 2);
        fixture.BindActivePlan(cargo);
        long expectedMass = fixture.MassOf(cargo.CarriedStackId);

        Require(fixture.Ability
                .TryStopHaulingOrReleaseRestoredCarryIfOperationsSubsetOf(
                    new[] { cargo.OperationId },
                    "qa-active-plan-regression",
                    HaulInterruptionDisposition
                        .ReleaseUnpickedAndDropCarriedAtActor,
                    out string failureReason),
            "Active-plan subset stop regressed: " + failureReason);
        Require(fixture.Inventory.Items.Count == 0
                && !fixture.Ability.OwnsHaulOperation(cargo.OperationId)
                && fixture.Ability.ActiveReservationsForDiagnostics.Count == 0,
            "Active-plan subset stop retained actor haul authority.");
        fixture.RequireOperationAuthorityAbsent(cargo.OperationId);
        WorldItemStackSnapshot dropped = fixture.RequireStack(
            cargo.CarriedStackId);
        Require(dropped.State == WorldItemStackState.Loose
                && dropped.Position == ActorCell
                && dropped.Quantity == cargo.Quantity
                && fixture.MassOf(dropped.StackId) == expectedMass,
            "Active-plan stop changed physical quantity, mass, or drop cell.");
        fixture.RequirePhysicalRestoreExact(cargo, expectedMass);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly WorldItemRepository repository;
        private readonly ItemQuantityReservationService reservations;
        private readonly CharacterCarryInventoryRegistry carryRegistry;
        private readonly Grid grid;
        private readonly TestGridProvider gridProvider;
        private GameObject actorObject;
        private bool disposed;

        private Fixture(
            WorldItemStackRuntime runtime,
            WorldItemRepository repository,
            ItemQuantityReservationService reservations,
            CharacterCarryInventoryRegistry carryRegistry,
            Grid grid,
            TestGridProvider gridProvider,
            GameObject actorObject,
            CharacterActor actor,
            CharacterCarryInventory inventory,
            AbilityHaul ability)
        {
            Runtime = runtime;
            this.repository = repository;
            this.reservations = reservations;
            this.carryRegistry = carryRegistry;
            this.grid = grid;
            this.gridProvider = gridProvider;
            this.actorObject = actorObject;
            Actor = actor;
            Inventory = inventory;
            Ability = ability;
        }

        internal WorldItemStackRuntime Runtime { get; }
        internal CharacterActor Actor { get; }
        internal CharacterCarryInventory Inventory { get; }
        internal AbilityHaul Ability { get; }

        internal static Fixture Create()
        {
            WorldItemStackRuntime runtime = null;
            CharacterCarryInventoryRegistry carryRegistry = null;
            GameObject actorObject = null;
            try
            {
                Grid grid = CreateWalkableGrid();
                TestGridProvider gridProvider = new(grid);
                runtime = PhysicalItemDebugScenarios
                    .CreateRuntimeForCrossDomainFixture(
                        gridProvider,
                        out WorldItemRepository repository,
                        out _,
                        out ItemQuantityReservationService reservations,
                        out _);
                carryRegistry = new CharacterCarryInventoryRegistry();
                actorObject = CreateActorObject(
                    runtime,
                    carryRegistry,
                    grid,
                    gridProvider,
                    out CharacterActor actor,
                    out CharacterCarryInventory inventory,
                    out AbilityHaul ability);
                return new Fixture(
                    runtime,
                    repository,
                    reservations,
                    carryRegistry,
                    grid,
                    gridProvider,
                    actorObject,
                    actor,
                    inventory,
                    ability);
            }
            catch
            {
                if (actorObject != null)
                    UnityEngine.Object.DestroyImmediate(actorObject);
                carryRegistry?.Dispose();
                runtime?.Dispose();
                throw;
            }
        }

        internal Cargo CreateCommittedCargo(int quantity)
        {
            string operationId = repository
                .AllocateEditorTestHaulDeliveryOperationId(ActorId);
            string sourceStackId = repository.AddEditorTestStack(
                ItemId,
                quantity,
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
                    + DestinationId,
                    new ItemQuantityReservationRequest(
                        new ItemStackId(sourceStackId),
                        quantity,
                        signature),
                    out ItemQuantityLease lease,
                    out DomainFailure reserveFailure),
                "Cargo reservation failed: " + reserveFailure);
            Require(Runtime.TryRegisterHaulDeliveryPlanForEditorTest(
                    operationId,
                    ActorId,
                    WorldItemHaulDestinationKind.FacilityBuffer,
                    DestinationId,
                    DestinationCell,
                    DestinationCell,
                    out string intentFailure),
                "Cargo durable intent failed: " + intentFailure);
            WorldItemReservedStackQuantity reservation = new(
                sourceStackId,
                ItemId,
                quantity,
                ActorCell,
                WorldItemHaulDestinationKind.FacilityBuffer,
                DestinationId,
                lease.leaseId,
                operationId);
            Require(Runtime.TryPickupReservedStackQuantity(
                    Actor,
                    Inventory,
                    reservation,
                    out int picked,
                    out string pickupFailure)
                && picked == quantity,
                "Cargo pickup failed: " + pickupFailure);
            Require(Runtime.TryCommitHaulPickup(
                    operationId,
                    Inventory,
                    out string commitFailure),
                "Cargo pickup commit failed: " + commitFailure);
            CharacterCarriedItemSaveData carried = Inventory.Items.Single(
                value => string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal));
            return new Cargo(
                operationId,
                sourceStackId,
                carried.carriedStackId,
                lease.leaseId,
                quantity);
        }

        internal void PrimeEditorRuntimeBinding()
        {
            Cargo primer = CreateCommittedCargo(quantity: 1);
            BindActivePlan(primer);
            Require(Ability
                    .TryStopHaulingOrReleaseRestoredCarryIfOperationsSubsetOf(
                        new[] { primer.OperationId },
                        "qa-prime-editor-runtime-binding",
                        HaulInterruptionDisposition
                            .ReleaseUnpickedAndDropCarriedAtActor,
                        out string failureReason),
                "Editor runtime binding primer did not close: " + failureReason);
            Require(Inventory.Items.Count == 0
                    && Ability.ActiveReservationsForDiagnostics.Count == 0,
                "Editor runtime binding primer retained haul authority.");
        }

        internal void RestoreDetachedCarry()
        {
            CharacterCarryInventorySaveData carry = Inventory.Capture();
            DungeonPhysicalItemSaveData physical = Runtime.Capture();
            Runtime.Restore(physical);
            Inventory.Restore(carry);
            Require(Ability.ActiveReservationsForDiagnostics.Count == 0,
                "Detached carry restore unexpectedly reconstructed a live plan.");
        }

        internal void BindActivePlan(Cargo cargo)
        {
            CharacterCarriedItemSaveData carried = Inventory.Items.Single(
                value => string.Equals(
                    value.ownerOperationId,
                    cargo.OperationId,
                    StringComparison.Ordinal));
            WorldItemReservedStackQuantity reservation = new(
                carried.carriedStackId,
                carried.itemId,
                carried.quantity,
                ActorCell,
                WorldItemHaulDestinationKind.FacilityBuffer,
                DestinationId,
                cargo.LeaseId,
                cargo.OperationId);
            WorldItemHaulPlanLeg delivery = new(
                reservation,
                ActorCell,
                null,
                DestinationCell,
                DestinationCell);
            WorldItemHaulPlan plan = new(
                Array.Empty<WorldItemHaulPlanLeg>(),
                new[] { delivery },
                new[] { reservation },
                MassOf(cargo.CarriedStackId) / 1000f,
                expectedDetourCost: 0,
                WorldItemHaulDestinationKind.FacilityBuffer,
                DestinationId,
                deliveryOnlyResume: true);
            Require(Ability.TryBindCapacityRoutingEditorFixture(
                    Runtime,
                    plan,
                    new[] { cargo.LeaseId },
                    out string failureReason),
                "AbilityHaul active-plan fixture binding failed: "
                + failureReason);
        }

        internal void RequireRejectedWithoutMutation(
            IReadOnlyCollection<string> allowedOperationIds,
            IReadOnlyList<Cargo> cargos,
            string expectedFailurePrefix)
        {
            string carryBefore = CaptureCarrySignature();
            string authorityBefore = CaptureAuthoritySignature(cargos);
            string physicalBefore = CapturePhysicalSignature(cargos);

            Require(!Ability
                    .TryStopHaulingOrReleaseRestoredCarryIfOperationsSubsetOf(
                        allowedOperationIds,
                        "qa-restored-carry-rejection",
                        HaulInterruptionDisposition
                            .ReleaseUnpickedAndDropCarriedAtActor,
                        out string failureReason),
                "Invalid detached restore cargo was silently released.");
            Require(!string.IsNullOrWhiteSpace(failureReason)
                    && failureReason.StartsWith(
                        expectedFailurePrefix,
                        StringComparison.Ordinal),
                "Invalid detached restore cargo did not fail loudly: "
                + failureReason);
            Require(string.Equals(
                    CaptureCarrySignature(),
                    carryBefore,
                    StringComparison.Ordinal),
                "Rejected detached restore cargo mutated logical carry state.");
            Require(string.Equals(
                    CaptureAuthoritySignature(cargos),
                    authorityBefore,
                    StringComparison.Ordinal),
                "Rejected detached restore cargo released lease or intent authority.");
            Require(string.Equals(
                    CapturePhysicalSignature(cargos),
                    physicalBefore,
                    StringComparison.Ordinal),
                "Rejected detached restore cargo mutated its physical stack.");
        }

        internal WorldItemStackSnapshot RequireStack(string stackId) =>
            Runtime.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                stackId,
                StringComparison.Ordinal));

        internal long MassOf(string stackId)
        {
            WorldItemStackSnapshot stack = RequireStack(stackId);
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

        internal void RequireOperationAuthorityAbsent(string operationId)
        {
            Require(!Runtime.TryCaptureHaulDeliveryIntent(operationId, out _)
                    && (!reservations.TryGetLeasesByOwner(
                            operationId,
                            out IReadOnlyList<ItemQuantityLease> leases)
                        || leases.Count == 0),
                "Active-plan stop retained lease or intent authority.");
        }

        internal void RequireOperationAuthorityPresent(Cargo cargo)
        {
            string signature = ItemReservationSignature.Create(
                ItemId,
                Array.Empty<ItemInstanceComponentSaveData>());
            Require(Runtime.TryCaptureHaulDeliveryIntent(
                        cargo.OperationId,
                        out HaulDeliveryIntentSaveData intent)
                    && intent != null
                    && intent.HasCommittedPickup
                    && string.Equals(
                        intent.ownerCharacterId,
                        ActorId,
                        StringComparison.Ordinal)
                    && intent.commitments != null
                    && intent.commitments.Count == 1
                    && string.Equals(
                        intent.commitments[0].carriedStackId,
                        cargo.CarriedStackId,
                        StringComparison.Ordinal)
                    && intent.commitments[0].quantity == cargo.Quantity
                    && string.Equals(
                        intent.commitments[0].expectedStackSignature,
                        signature,
                        StringComparison.Ordinal)
                    && reservations.TryGetLeasesByOwner(
                        cargo.OperationId,
                        out IReadOnlyList<ItemQuantityLease> leases)
                    && leases.Count == 1
                    && leases[0].purpose == ItemReservationPurpose.Hauling
                    && string.Equals(
                        leases[0].ownerCharacterId,
                        ActorId,
                        StringComparison.Ordinal)
                    && leases[0].remainingQuantity == cargo.Quantity
                    && leases[0].slices != null
                    && leases[0].slices.Count == 1
                    && string.Equals(
                        leases[0].slices[0].stackId,
                        cargo.CarriedStackId,
                        StringComparison.Ordinal)
                    && leases[0].slices[0].quantity == cargo.Quantity
                    && string.Equals(
                        leases[0].slices[0].expectedStackSignature,
                        signature,
                        StringComparison.Ordinal),
                "Committed carried cargo lost its exact lease/intent projection.");
        }

        internal void RequirePhysicalRestoreExact(
            Cargo cargo,
            long expectedMass)
        {
            DungeonPhysicalItemSaveData snapshot = Runtime.Capture();
            Runtime.Restore(snapshot);
            WorldItemStackSnapshot restored = RequireStack(cargo.CarriedStackId);
            Require(restored.State == WorldItemStackState.Loose
                    && restored.Position == ActorCell
                    && restored.Quantity == cargo.Quantity
                    && MassOf(restored.StackId) == expectedMass,
                "Lifecycle recovery drop did not round-trip current-format physical state.");
            RequireOperationAuthorityAbsent(cargo.OperationId);
        }

        private string CaptureCarrySignature() => string.Join(
            "|",
            Inventory.Items
                .Where(value => value != null && value.quantity > 0)
                .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
                .Select(value => string.Join(
                    ":",
                    value.carriedStackId,
                    value.sourceStackId,
                    value.ownerOperationId,
                    value.itemId,
                    value.quantity)));

        private string CaptureAuthoritySignature(IEnumerable<Cargo> cargos) =>
            string.Join(
                "|",
                cargos.OrderBy(value => value.OperationId, StringComparer.Ordinal)
                    .Select(value =>
                    {
                        bool hasIntent = Runtime.TryCaptureHaulDeliveryIntent(
                            value.OperationId,
                            out HaulDeliveryIntentSaveData intent);
                        bool hasLeases = reservations.TryGetLeasesByOwner(
                            value.OperationId,
                            out IReadOnlyList<ItemQuantityLease> leases);
                        string leaseIds = hasLeases
                            ? string.Join(",", leases
                                .Select(lease => lease.leaseId)
                                .OrderBy(id => id, StringComparer.Ordinal))
                            : string.Empty;
                        return string.Join(
                            ":",
                            value.OperationId,
                            hasIntent,
                            intent?.ownerCharacterId ?? string.Empty,
                            intent?.destinationId ?? string.Empty,
                            leaseIds);
                    }));

        private string CapturePhysicalSignature(IEnumerable<Cargo> cargos) =>
            string.Join(
                "|",
                cargos.OrderBy(value => value.CarriedStackId,
                        StringComparer.Ordinal)
                    .Select(value =>
                    {
                        WorldItemStackSnapshot stack = RequireStack(
                            value.CarriedStackId);
                        return string.Join(
                            ":",
                            stack.StackId,
                            stack.State,
                            stack.Quantity,
                            stack.Position.x,
                            stack.Position.y,
                            stack.DestinationId,
                            stack.DropDisposition);
                    }));

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            actorObject = null;
            carryRegistry.Dispose();
            Runtime.Dispose();
        }

        private static GameObject CreateActorObject(
            WorldItemStackRuntime runtime,
            CharacterCarryInventoryRegistry carryRegistry,
            Grid grid,
            TestGridProvider gridProvider,
            out CharacterActor actor,
            out CharacterCarryInventory inventory,
            out AbilityHaul ability)
        {
            GameObject target = new("Restored Carry Subset Release Actor");
            target.SetActive(false);
            actor = target.AddComponent<CharacterActor>();
            CharacterLifecycle lifecycle =
                target.GetComponent<CharacterLifecycle>();
            inventory = target.AddComponent<CharacterCarryInventory>();
            ability = target.AddComponent<AbilityHaul>();
            target.GetComponent<CharacterIdentity>().SetPersistentId(ActorId);
            CharacterAiEditorTestDependencies.Inject(target);
            lifecycle.ConstructCharacterLifecycle(gridProvider);
            target.transform.position = grid.GetWorldPos(ActorCell);
            target.SetActive(true);
            actor.EnsureRuntimeState();
            actor.Initialization(
                CharacterAiEditorTestDependencies
                    .RequireAuthoredCharacterDefinition(
                        "Adventurer",
                        CharacterRole.Regular));
            inventory.Configure(
                runtime.CatalogProvider,
                runtime.MassQuery,
                runtime.HaulingSettingsProvider,
                carryRegistry);
            return target;
        }

        private static Grid CreateWalkableGrid()
        {
            Grid value = new(12, 3);
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

    private readonly struct Cargo
    {
        internal Cargo(
            string operationId,
            string sourceStackId,
            string carriedStackId,
            string leaseId,
            int quantity)
        {
            OperationId = operationId;
            SourceStackId = sourceStackId;
            CarriedStackId = carriedStackId;
            LeaseId = leaseId;
            Quantity = quantity;
        }

        internal string OperationId { get; }
        internal string SourceStackId { get; }
        internal string CarriedStackId { get; }
        internal string LeaseId { get; }
        internal int Quantity { get; }
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
}
#endif
