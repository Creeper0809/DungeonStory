using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RunInvasionDurableEquipmentDebugScenarios
{
    private static readonly BuildingInstanceId OfficeId =
        new("building:qa-administration-office");
    private static readonly BuildingInstanceId SignalPostId =
        new("building:qa-security-post");

    [MenuItem("Tools/Dungeon Story/QA/Run Invasion Durable Equipment")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("RUN_INVASION_DURABLE_EQUIPMENT_PASS");
    }

    public static void RunAll()
    {
        DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();
        DurableFacilityEquipmentSaveDebugScenarios.RunAll();
        VerifyExactPoliciesAndAuthoredMass();
        VerifyAdministrativeResolutionTransaction();
        VerifySignalHornRallyTransaction();
        VerifyLostOwnersCloseBothSlots();
    }

    private static void VerifyExactPoliciesAndAuthoredMass()
    {
        DurableFacilityEquipmentPolicyRegistry policies = CreatePolicies();
        VerifyPolicy(
            policies,
            RunAdministrativeSealDurableEquipmentPolicySource.PolicyId,
            RunAdministrativeSealDurableEquipmentPolicySource.LogicalOwnerDomain,
            RunAdministrativeSealDurableEquipmentPolicySource.RequirementId,
            DurableToolItemRules.AdministrativeSeal);
        VerifyPolicy(
            policies,
            InvasionSignalHornDurableEquipmentPolicySource.PolicyId,
            InvasionSignalHornDurableEquipmentPolicySource.LogicalOwnerDomain,
            InvasionSignalHornDurableEquipmentPolicySource.RequirementId,
            DurableToolItemRules.WatchSignalHorn);

        VerifyAuthoredMass(
            "Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I92_행정_인장.asset",
            DurableToolItemRules.AdministrativeSeal);
        VerifyAuthoredMass(
            "Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I108_경계_신호_나팔.asset",
            DurableToolItemRules.WatchSignalHorn);
    }

    private static void VerifyAdministrativeResolutionTransaction()
    {
        RecordingSlots slots = new();
        RecordingUse use = new(slots);
        RunAdministrativeSealDurableEquipmentRuntime runtime = new(
            CreatePolicies(),
            slots,
            use);
        int commits = 0;
        Require(runtime.TryCommitResolution(
                OfficeId,
                new Vector2Int(11, 4),
                () =>
                {
                    commits++;
                    return true;
                },
                out string failure)
            && commits == 1
            && failure.Length == 0,
            "Administrative seal resolution did not commit exactly once.");
        VerifyLastAssignment(
            slots,
            RunAdministrativeSealDurableEquipmentPolicySource.LogicalOwnerDomain,
            OfficeId,
            DurableToolItemRules.AdministrativeSeal);
        Require(use.LastRequirementId ==
                RunAdministrativeSealDurableEquipmentPolicySource.RequirementId
            && Math.Abs(use.LastWear
                - RunAdministrativeSealDurableEquipmentRuntime.WearPerResolution)
                <= 0.000001d,
            "Administrative seal wear contract drifted.");

        Require(!runtime.TryCommitResolution(
                OfficeId,
                new Vector2Int(11, 4),
                () => false,
                out failure)
            && failure == "administrative-seal-resolution-rejected",
            "A rejected administration effect was reported as committed.");
    }

    private static void VerifySignalHornRallyTransaction()
    {
        RecordingSlots slots = new();
        RecordingUse use = new(slots);
        InvasionSignalHornDurableEquipmentRuntime runtime = new(
            CreatePolicies(),
            slots,
            use);
        Vector2Int position = new(19, 5);
        Require(runtime.TryEnsureReady(SignalPostId, position, out _),
            "Signal-horn supply did not become ready.");
        int commits = 0;
        Require(runtime.TryCommitRally(
                SignalPostId,
                position,
                () =>
                {
                    commits++;
                    return true;
                },
                out string failure)
            && commits == 1
            && failure.Length == 0,
            "Signal-horn rally did not commit exactly once.");
        VerifyLastAssignment(
            slots,
            InvasionSignalHornDurableEquipmentPolicySource.LogicalOwnerDomain,
            SignalPostId,
            DurableToolItemRules.WatchSignalHorn);
        Require(use.LastRequirementId ==
                InvasionSignalHornDurableEquipmentPolicySource.RequirementId
            && Math.Abs(use.LastWear
                - InvasionSignalHornDurableEquipmentRuntime.WearPerRally)
                <= 0.000001d,
            "Signal-horn wear contract drifted.");

        Require(!runtime.TryCommitRally(
                SignalPostId,
                position,
                () => false,
                out failure)
            && failure == "invasion-signal-horn-rally-rejected",
            "A rejected signal-horn rally was reported as committed.");
    }

    private static void VerifyLostOwnersCloseBothSlots()
    {
        RecordingSlots slots = new();
        RunAdministrativeSealDurableEquipmentRuntime administration = new(
            CreatePolicies(),
            slots,
            new RecordingUse(slots));
        InvasionSignalHornDurableEquipmentRuntime invasion = new(
            CreatePolicies(),
            slots,
            new RecordingUse(slots));
        Require(administration.TryCommitResolution(
                OfficeId,
                new Vector2Int(11, 4),
                () => true,
                out _),
            "Lifecycle fixture could not create the administration slot.");
        Require(invasion.TryEnsureReady(
                SignalPostId,
                new Vector2Int(19, 5),
                out _),
            "Lifecycle fixture could not create the invasion slot.");

        RunInvasionDurableEquipmentLifecycleRuntime lifecycle = new(
            new EmptyBuildingWorld(),
            slots,
            slots);
        lifecycle.Start();
        Require(slots.CloseCalls == 2
            && slots.CaptureAll().Count == 0,
            "Lost event-tool facilities did not close both slots exactly once.");
        lifecycle.ValidateBeforeCapture();
    }

    private static DurableFacilityEquipmentPolicyRegistry CreatePolicies() =>
        new(new IDurableFacilityEquipmentPolicySource[]
        {
            new RunAdministrativeSealDurableEquipmentPolicySource(),
            new InvasionSignalHornDurableEquipmentPolicySource()
        });

    private static void VerifyPolicy(
        IDurableFacilityEquipmentPolicyQuery policies,
        string policyId,
        string ownerDomain,
        string requirementId,
        string itemId)
    {
        Require(policies.TryGetPolicy(
                policyId,
                out DurableFacilityEquipmentPolicy policy)
            && policy.LogicalOwnerDomain == ownerDomain
            && policy.Requirements.Count == 1
            && policy.Requirements[0].RequirementId == requirementId
            && policy.Requirements[0].ItemId.Equals((ItemDefinitionId)itemId)
            && policy.Requirements[0].RequiredQuantity == 1,
            "Event-tool durable-equipment policy lost exact identity: " + policyId);
    }

    private static void VerifyAuthoredMass(string path, string itemId)
    {
        ResourceItemDefinitionSO item =
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(path);
        Require(item != null
            && item.StableId.Equals((ItemDefinitionId)itemId)
            && PhysicalMassGrams.FromCanonicalKilograms(item.UnitWeight).Value
                == 2_350L
            && item.MaxStack == 1,
            "Event-tool authored mass/max-stack drifted: " + itemId);
    }

    private static void VerifyLastAssignment(
        RecordingSlots slots,
        string ownerDomain,
        BuildingInstanceId facilityId,
        string itemId)
    {
        DurableFacilityEquipmentAssignment assignment = slots.LastAssignment;
        Require(assignment != null
            && assignment.Key.LogicalOwnerDomain == ownerDomain
            && assignment.Key.OwnerSubjectId == facilityId.Value
            && assignment.OwnerFacilityId.Equals(facilityId)
            && assignment.Requirements.Count == 1
            && assignment.Requirements[0].ItemId.Equals((ItemDefinitionId)itemId)
            && assignment.Requirements[0].RequiredQuantity == 1,
            "Event-tool assignment lost exact facility/item/quantity identity.");
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

        internal DurableFacilityEquipmentAssignment LastAssignment { get; private set; }
        internal int CloseCalls { get; private set; }

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired)
        {
            LastAssignment = desired;
            DurableFacilityEquipmentSlotSnapshot snapshot = CreateSnapshot(desired);
            snapshots[desired.Key] = snapshot;
            return Success(DurableFacilityEquipmentSlotStatus.Applied, snapshot);
        }

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key)
        {
            Require(snapshots.TryGetValue(key, out var snapshot),
                "Event-tool supply targeted a missing slot.");
            return Success(DurableFacilityEquipmentSlotStatus.Replay, snapshot);
        }

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode)
        {
            Require(snapshots.TryGetValue(key, out var snapshot),
                "Event-tool close targeted a missing slot.");
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
            DurableFacilityEquipmentAssignment assignment)
        {
            long sequence = assignment.Key.LogicalOwnerDomain ==
                RunAdministrativeSealDurableEquipmentPolicySource.LogicalOwnerDomain
                    ? 1L
                    : 2L;
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
                    new PhysicalMassGrams(2_350L),
                    1L,
                    new string('c', 64)),
                assignment.Requirements.Select(requirement =>
                    new DurableFacilityEquipmentRequirementStatus(
                        requirement,
                        pendingQuantity: 0,
                        bufferedUsableQuantity: 1)));
        }
    }

    private sealed class RecordingUse : IDurableFacilityEquipmentUseCommand
    {
        private readonly RecordingSlots slots;

        internal RecordingUse(RecordingSlots slots)
        {
            this.slots = slots;
        }

        internal string LastRequirementId { get; private set; } = string.Empty;
        internal double LastWear { get; private set; }

        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect)
        {
            Require(slots.TryCapture(key, out var slot),
                "Event-tool use targeted a missing slot.");
            DurableFacilityEquipmentRequirement requirement = slot.Assignment
                .Requirements.Single(value => value.RequirementId == requirementId);
            LastRequirementId = requirementId;
            LastWear = wearAmount;
            DurableFacilityEquipmentUseSubject before = new(
                "stack:" + requirementId,
                1L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            if (!effect.TryPreflight(
                    slot,
                    requirement,
                    before,
                    wearAmount,
                    out string failure))
            {
                return Failure(slot, failure);
            }
            DurableFacilityEquipmentUseSubject after = new(
                "stack:" + requirementId,
                2L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            if (!effect.TryCommit(
                    new DurableFacilityEquipmentUseContext(
                        slot,
                        requirement,
                        before,
                        after,
                        wearAmount),
                    out failure))
            {
                return Failure(slot, failure);
            }
            return new DurableFacilityEquipmentUseResult(
                DurableFacilityEquipmentUseStatus.Applied,
                slot,
                before.StackId,
                string.Empty);
        }

        private static DurableFacilityEquipmentUseResult Failure(
            DurableFacilityEquipmentSlotSnapshot slot,
            string reason) => new(
            DurableFacilityEquipmentUseStatus.Deferred,
            slot,
            string.Empty,
            reason);
    }
}
