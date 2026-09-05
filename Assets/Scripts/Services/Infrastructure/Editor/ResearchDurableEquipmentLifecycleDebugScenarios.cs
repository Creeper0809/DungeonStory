#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResearchDurableEquipmentLifecycleDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/V27/Run Research Durable Equipment Lifecycle Contracts")]
    public static void RunAll()
    {
        VerifyDestroyedFacilityClosesTheActiveSlot();
        VerifyWorldLossAndCapabilityRemovalCloseTheActiveSlot();
        VerifyUnresolvedCloseConflictBlocksSaveCapture();

        Debug.Log(
            "[V27][PASS] Research durable-equipment lifecycle closes sequence-scoped "
            + "slots on destruction events, world loss, and capability removal, and "
            + "blocks save capture while a close conflict remains unresolved.");
    }

    private static void VerifyDestroyedFacilityClosesTheActiveSlot()
    {
        BuildableObject facility = CreateResearchFacility(out GameObject root);
        try
        {
            MutableBuildingWorld world = new(facility);
            MutableResearchPolicyQuery policies = new();
            FixedSlotStore slots = new(CreateActiveSlot(facility));
            RecordingSlotCommand commands = new(slots);
            ResearchDurableEquipmentLifecycleRuntime runtime = new(
                world,
                policies,
                slots,
                commands);

            runtime.Start();
            facility.DestroySelf();
            root = null;

            Require(
                commands.CloseCalls == 1
                && string.Equals(
                    commands.LastReason,
                    "research-facility-destroyed",
                    StringComparison.Ordinal),
                "A research facility destruction event did not close its durable slot exactly once.");
            runtime.ValidateBeforeCapture();
        }
        finally
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyWorldLossAndCapabilityRemovalCloseTheActiveSlot()
    {
        VerifyReconciledClose(
            (world, policies) => world.RemoveAll(),
            "research-facility-lost");
        VerifyReconciledClose(
            (world, policies) => policies.Applicable = false,
            "research-facility-capability-removed");
    }

    private static void VerifyReconciledClose(
        Action<MutableBuildingWorld, MutableResearchPolicyQuery> mutate,
        string expectedReason)
    {
        BuildableObject facility = CreateResearchFacility(out GameObject root);
        try
        {
            MutableBuildingWorld world = new(facility);
            MutableResearchPolicyQuery policies = new();
            FixedSlotStore slots = new(CreateActiveSlot(facility));
            RecordingSlotCommand commands = new(slots);
            ResearchDurableEquipmentLifecycleRuntime runtime = new(
                world,
                policies,
                slots,
                commands);

            runtime.Start();
            mutate(world, policies);
            world.AdvanceVersion();
            runtime.Tick();

            Require(
                commands.CloseCalls == 1
                && string.Equals(
                    commands.LastReason,
                    expectedReason,
                    StringComparison.Ordinal),
                "Research durable-equipment reconciliation used the wrong close reason: "
                + expectedReason);
            runtime.ValidateBeforeCapture();
        }
        finally
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyUnresolvedCloseConflictBlocksSaveCapture()
    {
        BuildableObject facility = CreateResearchFacility(out GameObject root);
        try
        {
            MutableBuildingWorld world = new(facility);
            MutableResearchPolicyQuery policies = new();
            FixedSlotStore slots = new(CreateActiveSlot(facility));
            RecordingSlotCommand commands = new(slots)
            {
                RejectClose = true
            };
            ResearchDurableEquipmentLifecycleRuntime runtime = new(
                world,
                policies,
                slots,
                commands);

            runtime.Start();
            world.RemoveAll();
            world.AdvanceVersion();
            runtime.Tick();

            RequireThrows<InvalidOperationException>(
                runtime.ValidateBeforeCapture,
                "An unresolved research durable-equipment close conflict allowed save capture.");
        }
        finally
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static DurableFacilityEquipmentSlotSnapshot CreateActiveSlot(
        BuildableObject facility)
    {
        DurableFacilityEquipmentPolicy policy =
            new ResearchArcaneIndexEquipmentPolicySource()
                .CapturePolicies()
                .Single();
        BuildingInstanceId facilityId = facility.RequirePersistentInstanceId();
        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            facilityId.Value,
            facilityId,
            facility.centerPos);
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
                new PhysicalMassGrams(1300L),
                1L,
                new string('a', 64)),
            assignment.Requirements.Select(requirement =>
                new DurableFacilityEquipmentRequirementStatus(
                    requirement,
                    pendingQuantity: 0,
                    bufferedUsableQuantity: requirement.RequiredQuantity)));
    }

    private static BuildableObject CreateResearchFacility(out GameObject root)
    {
        BuildingSO definition = AssetDatabase.FindAssets("t:BuildingSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value != null)
            .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal)
            .FirstOrDefault(value =>
                value.Facility?.SupportsWork(BuiltInWorkTypeIds.Research) == true)
            ?? throw new InvalidOperationException(
                "No authored research BuildingSO is available for the lifecycle fixture.");
        root = new GameObject("QA Research Durable Equipment Lifecycle Facility");
        BuildableObject facility = root.AddComponent<BuildableObject>();
        CharacterAiEditorTestDependencies.Inject(facility);
        facility.Initialization(definition, new Vector2Int(11, 7));
        return facility;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class MutableBuildingWorld : IBuildingWorldQuery
    {
        private readonly List<BuildableObject> buildings = new();

        internal MutableBuildingWorld(BuildableObject building)
        {
            buildings.Add(building);
        }

        public int BuildingVersion { get; private set; } = 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings;

        internal void RemoveAll() => buildings.Clear();
        internal void AdvanceVersion() => BuildingVersion++;
    }

    private sealed class MutableResearchPolicyQuery :
        IResearchDurableEquipmentWorkPolicyQuery
    {
        internal bool Applicable { get; set; } = true;

        public bool TryResolve(
            BuildableObject facility,
            out ResearchDurableEquipmentWorkPolicy policy,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!Applicable || facility == null)
            {
                policy = null;
                failureReason = "qa-research-policy-not-applicable";
                return false;
            }
            policy = new ResearchDurableEquipmentWorkPolicy(
                ResearchArcaneIndexEquipmentPolicySource.PolicyId,
                ResearchArcaneIndexEquipmentPolicySource.RequirementId,
                DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
                "research-approved-work-multiplier",
                1.1d,
                0.01d);
            return true;
        }

        public bool IsRegisteredEquipmentPolicy(string policyId) =>
            string.Equals(
                policyId,
                ResearchArcaneIndexEquipmentPolicySource.PolicyId,
                StringComparison.Ordinal);
    }

    private sealed class FixedSlotStore : IDurableFacilityEquipmentSlotQuery
    {
        private readonly DurableFacilityEquipmentSlotSnapshot snapshot;

        internal FixedSlotStore(DurableFacilityEquipmentSlotSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public bool TryCapture(
            DurableFacilityEquipmentSlotKey key,
            out DurableFacilityEquipmentSlotSnapshot value)
        {
            value = key.Equals(snapshot.Key) ? snapshot : null;
            return value != null;
        }

        public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> CaptureAll() =>
            new[] { snapshot };
    }

    private sealed class RecordingSlotCommand :
        IDurableFacilityEquipmentSlotCommand
    {
        private readonly FixedSlotStore slots;

        internal RecordingSlotCommand(FixedSlotStore slots)
        {
            this.slots = slots;
        }

        internal int CloseCalls { get; private set; }
        internal string LastReason { get; private set; } = string.Empty;
        internal bool RejectClose { get; set; }

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode)
        {
            CloseCalls++;
            LastReason = reasonCode;
            if (RejectClose)
            {
                return new DurableFacilityEquipmentSlotResult(
                    DurableFacilityEquipmentSlotStatus.Conflict,
                    null,
                    "qa-research-lifecycle-close-conflict");
            }
            slots.TryCapture(key, out DurableFacilityEquipmentSlotSnapshot snapshot);
            return new DurableFacilityEquipmentSlotResult(
                DurableFacilityEquipmentSlotStatus.Applied,
                snapshot,
                string.Empty);
        }

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired) =>
            throw new NotSupportedException();

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key) =>
            throw new NotSupportedException();

        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();
    }
}
#endif
