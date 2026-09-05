using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public static class CircusPerformanceSupplyDebugScenarios
{
    private static readonly BuildingInstanceId StageId =
        new("building:qa-circus-stage");

    public static string Run()
    {
        // These common fixtures are the executable evidence for effect
        // rollback, exact claim/profile save joins and carried-aware close.
        DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();
        DurableFacilityEquipmentSaveDebugScenarios.RunAll();
        VerifyPolicyAndAuthoredMass();
        VerifyMixedUsabilityAndWear();
        VerifyLostStageClosesOwnedSlot();
        return "circus exact 1,950g prop Sink + 3,150g durable cart ownership is closed";
    }

    private static void VerifyPolicyAndAuthoredMass()
    {
        DurableFacilityEquipmentPolicyRegistry registry = new(
            new IDurableFacilityEquipmentPolicySource[]
            {
                new CircusPerformanceSupplyPolicySource()
            });
        Require(registry.TryGetPolicy(
                CircusPerformanceSupplyPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy)
            && policy.LogicalOwnerDomain ==
                CircusPerformanceSupplyPolicySource.LogicalOwnerDomain
            && policy.Requirements.Count == 2
            && policy.Requirements.Any(value =>
                value.RequirementId ==
                    CircusPerformanceSupplyPolicySource.PropBoxRequirementId
                && value.ItemId.Equals(
                    (ItemDefinitionId)CircusPerformanceSupplyContracts
                        .PerformancePropBoxItemId)
                && value.RequiredQuantity == 1)
            && policy.Requirements.Any(value =>
                value.RequirementId ==
                    CircusPerformanceSupplyPolicySource.BanquetCartRequirementId
                && value.ItemId.Equals(
                    (ItemDefinitionId)CircusPerformanceSupplyContracts
                        .BanquetCartItemId)
                && value.RequiredQuantity == 1),
            "Circus performance-supply policy lost its exact pair.");

        VerifyMass(
            "Assets/Resources/SO/Economy/Items/ResearchOverhaul/"
            + "V3I101_공연_소품_상자.asset",
            CircusPerformanceSupplyContracts.PerformancePropBoxItemId,
            CircusPerformanceSupplyContracts.PerformancePropBoxMassGrams,
            expectedMaxStack: 50);
        VerifyMass(
            "Assets/Resources/SO/Economy/Items/ResearchOverhaul/"
            + "V3I102_연회_운반_수레.asset",
            CircusPerformanceSupplyContracts.BanquetCartItemId,
            CircusPerformanceSupplyContracts.BanquetCartMassGrams,
            expectedMaxStack: 1);
    }

    private static void VerifyMixedUsabilityAndWear()
    {
        CircusPerformanceSupplyUsabilityPolicy usability = new();
        DurableFacilityEquipmentRequirement prop = new(
            CircusPerformanceSupplyPolicySource.PropBoxRequirementId,
            (ItemDefinitionId)CircusPerformanceSupplyContracts
                .PerformancePropBoxItemId,
            1);
        DurableFacilityEquipmentUseSubject propSubject = new(
            "stack:qa-prop",
            1L,
            prop.ItemId,
            1,
            Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
        Require(usability.Evaluate(prop, propSubject).IsUsable,
            "The exact prop box was not usable without fake durability.");

        DurableFacilityEquipmentRequirement cart = new(
            CircusPerformanceSupplyPolicySource.BanquetCartRequirementId,
            (ItemDefinitionId)CircusPerformanceSupplyContracts
                .BanquetCartItemId,
            1);
        ItemInstanceComponentSaveData durability =
            DurableToolItemRules.CreateDurability(
                CircusPerformanceSupplyContracts.BanquetCartItemId,
                100f);
        DurableFacilityEquipmentUseSubject cartSubject =
            DurableFacilityEquipmentUseSubjectCapture.Capture(
                new WorldItemStackSnapshot
                {
                    StackId = "stack:qa-cart",
                    ContentRevision = 1L,
                    ItemId = CircusPerformanceSupplyContracts.BanquetCartItemId,
                    Quantity = 1,
                    Components = new[] { durability }
                });
        Require(usability.Evaluate(cart, cartSubject).IsUsable,
            "A positive-durability banquet cart was not usable.");
        DurableFacilityEquipmentWearProjection wear =
            new CircusPerformanceSupplyWearPolicy().Project(
                cart,
                cartSubject,
                CircusPerformanceSupplyContracts.BanquetCartWearPerShow);
        Require(Math.Abs(wear.CurrentBefore - 100d) <= 0.000001d
            && Math.Abs(wear.CurrentAfter - 96d) <= 0.000001d
            && wear.PolicyKind ==
                CircusPerformanceSupplyPolicySource.MixedUsabilityPolicyKind,
            "Banquet-cart exact four-point wear projection drifted.");
    }

    private static void VerifyLostStageClosesOwnedSlot()
    {
        RecordingSlots slots = new();
        CircusPerformanceSupplyLifecycleRuntime lifecycle = new(
            new EmptyBuildingWorld(),
            slots,
            slots);
        lifecycle.Start();
        Require(slots.CloseCalls == 1
            && slots.LastCloseReason == "circus-performance-stage-lost",
            "A lost circus stage did not close its exact slot once.");
        lifecycle.ValidateBeforeCapture();
    }

    private static void VerifyMass(
        string path,
        string itemId,
        long grams,
        int expectedMaxStack)
    {
        ResourceItemDefinitionSO item =
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(path);
        Require(item != null
            && item.StableId.Equals((ItemDefinitionId)itemId)
            && PhysicalMassGrams.FromCanonicalKilograms(item.UnitWeight).Value
                == grams
            && item.MaxStack == expectedMaxStack,
            "Circus authored mass/max-stack drifted: " + itemId);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    private sealed class RecordingSlots :
        IDurableFacilityEquipmentSlotQuery,
        IDurableFacilityEquipmentSlotCommand
    {
        private DurableFacilityEquipmentSlotSnapshot snapshot = CreateSnapshot();

        internal int CloseCalls { get; private set; }
        internal string LastCloseReason { get; private set; } = string.Empty;

        public bool TryCapture(
            DurableFacilityEquipmentSlotKey key,
            out DurableFacilityEquipmentSlotSnapshot value)
        {
            value = snapshot != null && snapshot.Key.Equals(key)
                ? snapshot
                : null;
            return value != null;
        }

        public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> CaptureAll() =>
            snapshot == null
                ? Array.Empty<DurableFacilityEquipmentSlotSnapshot>()
                : new[] { snapshot };

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired) =>
            throw new NotSupportedException();

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key) =>
            throw new NotSupportedException();

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode)
        {
            Require(snapshot != null && snapshot.Key.Equals(key),
                "Circus lifecycle closed another slot.");
            DurableFacilityEquipmentSlotSnapshot closed = snapshot;
            snapshot = null;
            CloseCalls++;
            LastCloseReason = reasonCode;
            return new DurableFacilityEquipmentSlotResult(
                DurableFacilityEquipmentSlotStatus.Applied,
                closed,
                string.Empty);
        }

        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();

        private static DurableFacilityEquipmentSlotSnapshot CreateSnapshot()
        {
            DurableFacilityEquipmentPolicy policy =
                new CircusPerformanceSupplyPolicySource()
                    .CapturePolicies().Single();
            DurableFacilityEquipmentAssignment assignment =
                policy.CreateAssignment(
                    StageId.Value,
                    StageId,
                    new UnityEngine.Vector2Int(8, 4));
            const long sequence = 1L;
            return new DurableFacilityEquipmentSlotSnapshot(
                assignment,
                sequence,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    assignment.Key,
                    sequence),
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    assignment.Key,
                    sequence),
                DurableFacilityEquipmentFingerprint.CreateAssignment(assignment),
                new DurableFacilityEquipmentCapacityProjection(
                    DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                    new PhysicalMassGrams(
                        CircusPerformanceSupplyContracts
                            .PerformancePropBoxMassGrams
                        + CircusPerformanceSupplyContracts
                            .BanquetCartMassGrams),
                    1L,
                    new string('d', 64)),
                assignment.Requirements.Select(requirement =>
                    new DurableFacilityEquipmentRequirementStatus(
                        requirement,
                        pendingQuantity: 0,
                        bufferedUsableQuantity: 1)));
        }
    }
}
