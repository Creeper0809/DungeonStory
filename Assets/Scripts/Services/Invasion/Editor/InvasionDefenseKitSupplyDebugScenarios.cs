using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class InvasionDefenseKitSupplyDebugScenarios
{
    private static readonly BuildingInstanceId SignalPostId =
        new("building:qa-alliance-signal-post");

    [MenuItem("Tools/Dungeon Story/QA/Invasion Defense Kit Supply")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("INVASION_DEFENSE_KIT_SUPPLY_PASS");
    }

    public static void RunAll()
    {
        VerifyOnlyAllianceSignalPostIsEligible();
        VerifyExactPolicyAndPositiveAuthoredMass();
        VerifyCommonAssignmentIdentity();
        VerifyLostOwnerClosesCommonSlot();
        VerifyExecutorHasNoBespokeDeliveryRequest();
    }

    private static void VerifyOnlyAllianceSignalPostIsEligible()
    {
        BuildingSO signalPost = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/ResearchOverhaul/"
            + "RF12_동맹_신호기.asset");
        BuildingSO genericSecurity = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/G01_경비초소책상.asset");
        Require(
            AllianceSignalPostEligibility.IsEligibleDefinition(signalPost),
            "The authored RF12 alliance signal post was not eligible.");
        Require(
            !AllianceSignalPostEligibility.IsEligibleDefinition(genericSecurity),
            "A generic Security facility was accepted as an alliance signal post.");
    }

    private static void VerifyExactPolicyAndPositiveAuthoredMass()
    {
        DurableFacilityEquipmentPolicyRegistry policies = CreatePolicies();
        Require(policies.TryGetPolicy(
                InvasionDefenseKitSupplyPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy)
            && policy.LogicalOwnerDomain ==
                InvasionDefenseKitSupplyPolicySource.LogicalOwnerDomain
            && policy.CapacityPolicyKind ==
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind
            && policy.UsabilityPolicyKind ==
                InvasionDefenseKitSupplyPolicySource
                    .ConsumableUsabilityPolicyKind
            && policy.Requirements.Count == 1
            && policy.Requirements[0].RequirementId ==
                InvasionDefenseKitSupplyPolicySource.RequirementId
            && policy.Requirements[0].ItemId.Equals(
                (ItemDefinitionId)InvasionDefenseKitSupplyPolicySource.ItemId)
            && policy.Requirements[0].RequiredQuantity == 1,
            "Defense-kit policy lost exact item/quantity identity.");

        ResourceItemDefinitionSO kit =
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
                "Assets/Resources/SO/Economy/Items/ResearchOverhaul/"
                + "V3I104_동맹_신호_키트.asset");
        Require(kit != null
            && kit.StableId.Equals(
                (ItemDefinitionId)InvasionDefenseKitSupplyPolicySource.ItemId)
            && PhysicalMassGrams.FromCanonicalKilograms(kit.UnitWeight).Value
                == 1_150L,
            "Defense-kit authored definition no longer has exact positive 1,150g mass.");

        InvasionDefenseKitSupplyUsabilityPolicy usability = new();
        DurableFacilityEquipmentUseSubject subject = new(
            "stack:qa-defense-kit",
            1L,
            (ItemDefinitionId)InvasionDefenseKitSupplyPolicySource.ItemId,
            1,
            Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
        Require(usability.Evaluate(policy.Requirements[0], subject).IsUsable,
            "The exact consumable defense kit was not usable without invented durability.");
    }

    private static void VerifyCommonAssignmentIdentity()
    {
        DurableFacilityEquipmentAssignment assignment = CreateAssignment();
        Require(assignment.Key.LogicalOwnerDomain ==
                InvasionDefenseKitSupplyPolicySource.LogicalOwnerDomain
            && assignment.Key.OwnerSubjectId == SignalPostId.Value
            && assignment.OwnerFacilityId.Equals(SignalPostId)
            && assignment.Requirements.Count == 1
            && assignment.Requirements[0].RequiredQuantity == 1,
            "Defense-kit assignment lost exact live-facility ownership.");
        string destination = DurableFacilityEquipmentSlotIdentity
            .BuildDestinationId(assignment.Key, 1L);
        Require(destination.StartsWith(
                ReservedTargetDestinationIdentity.ExactFacilityInputPrefix,
                StringComparison.Ordinal),
            "Defense-kit assignment did not use a common exact destination.");
    }

    private static void VerifyLostOwnerClosesCommonSlot()
    {
        RecordingSlots slots = new();
        DurableFacilityEquipmentAssignment assignment = CreateAssignment();
        slots.TryReconcile(assignment);

        InvasionDefenseKitSupplyLifecycleRuntime lifecycle = new(
            new EmptyBuildingWorld(),
            slots,
            slots);
        lifecycle.Start();
        Require(slots.CloseCalls == 1 && slots.CaptureAll().Count == 0,
            "Lost signal post did not close the defense-kit slot exactly once.");
        lifecycle.ValidateBeforeCapture();
    }

    private static void VerifyExecutorHasNoBespokeDeliveryRequest()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string executorPath = Path.Combine(
            projectRoot,
            "Assets/Scripts/Services/Invasion/DefenseCombatExecutor.cs");
        string source = File.ReadAllText(executorPath);
        Require(!source.Contains("itemStackRuntime.TryRequestItemDelivery(",
                StringComparison.Ordinal)
            && source.Contains("defenseKitSupply.TryEnsureReady(",
                StringComparison.Ordinal)
            && source.Contains("AllianceSignalPostEligibility.SelectFirst(",
                StringComparison.Ordinal),
            "Defense combat retained bespoke delivery or generic Security selection.");
    }

    private static DurableFacilityEquipmentPolicyRegistry CreatePolicies() =>
        new(new IDurableFacilityEquipmentPolicySource[]
        {
            new InvasionDefenseKitSupplyPolicySource()
        });

    private static DurableFacilityEquipmentAssignment CreateAssignment()
    {
        DurableFacilityEquipmentPolicyRegistry policies = CreatePolicies();
        Require(policies.TryGetPolicy(
                InvasionDefenseKitSupplyPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy),
            "Defense-kit policy was unavailable for assignment fixture.");
        return policy.CreateAssignment(
            SignalPostId.Value,
            SignalPostId,
            new Vector2Int(17, 4));
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
        IDurableFacilityEquipmentSlotCommand,
        IDurableFacilityEquipmentSlotQuery
    {
        private readonly Dictionary<DurableFacilityEquipmentSlotKey,
            DurableFacilityEquipmentSlotSnapshot> snapshots = new();

        internal int CloseCalls { get; private set; }

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired)
        {
            DurableFacilityEquipmentSlotSnapshot snapshot = CreateSnapshot(desired);
            snapshots[desired.Key] = snapshot;
            return Success(DurableFacilityEquipmentSlotStatus.Applied, snapshot);
        }

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key)
        {
            Require(snapshots.TryGetValue(key, out var snapshot),
                "Defense-kit supply targeted a missing slot.");
            return Success(DurableFacilityEquipmentSlotStatus.Replay, snapshot);
        }

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode)
        {
            Require(snapshots.TryGetValue(key, out var snapshot),
                "Defense-kit close targeted a missing slot.");
            snapshots.Remove(key);
            CloseCalls++;
            return Success(DurableFacilityEquipmentSlotStatus.Applied, snapshot);
        }

        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();

        public bool TryCapture(
            DurableFacilityEquipmentSlotKey key,
            out DurableFacilityEquipmentSlotSnapshot snapshot) =>
            snapshots.TryGetValue(key, out snapshot);

        public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> CaptureAll() =>
            snapshots.Values.OrderBy(value => value.AssignmentSequence).ToArray();

        private static DurableFacilityEquipmentSlotResult Success(
            DurableFacilityEquipmentSlotStatus status,
            DurableFacilityEquipmentSlotSnapshot snapshot) =>
            new(status, snapshot, string.Empty);

        private static DurableFacilityEquipmentSlotSnapshot CreateSnapshot(
            DurableFacilityEquipmentAssignment assignment) =>
            new(
                assignment,
                1L,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    assignment.Key,
                    1L),
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    assignment.Key,
                    1L),
                DurableFacilityEquipmentFingerprint.CreateAssignment(assignment),
                new DurableFacilityEquipmentCapacityProjection(
                    DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                    new PhysicalMassGrams(1_150L),
                    1L,
                    new string('d', 64)),
                assignment.Requirements.Select(requirement =>
                    new DurableFacilityEquipmentRequirementStatus(
                        requirement,
                        pendingQuantity: 0,
                        bufferedUsableQuantity: 1)));
    }
}
