#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Work;
using UnityEditor;
using UnityEngine;

public static class ResearchArcaneIndexEquipmentDebugScenarios
{
    private const float ApprovedWork = 10f;
    private const string ArcaneIndexId = DurableToolItemRules.ArcaneIndex;

    [MenuItem(
        "DungeonStory/Debug/V27/Run Research Arcane Index Equipment Contracts")]
    public static void RunAll()
    {
        GameObject facilityObject = null;
        GameObject researcherObject = null;
        try
        {
            BuildableObject facility = CreateResearchFacility(out facilityObject);
            CharacterActor researcher = CreateResearcher(out researcherObject);
            VerifyLiveResearchAdapter(facility, researcher);
            Debug.Log(
                "[V27][PASS] Research arcane-index equipment uses a sequence-scoped "
                + "1,300g common slot, requests missing supply without suppressing base "
                + "research, applies 1.1x work and 0.01 durability/WU exactly once, "
                + "rolls wear back on rejected effects, fails mutation/revision drift "
                + "without research progress, and reopens under a new sequence after "
                + "the common depletion drain.");
        }
        finally
        {
            if (researcherObject != null)
                UnityEngine.Object.DestroyImmediate(researcherObject);
            if (facilityObject != null)
                UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private static void VerifyLiveResearchAdapter(
        BuildableObject facility,
        CharacterActor researcher)
    {
        ResearchArcaneIndexEquipmentPolicySource source = new();
        ResearchDurableEquipmentWorkPolicyRegistry researchPolicies = new(
            new IResearchDurableEquipmentWorkPolicySource[] { source });
        DurableFacilityEquipmentPolicyRegistry equipmentPolicies = new(
            new IDurableFacilityEquipmentPolicySource[] { source });
        DurableFacilityEquipmentCapacityProjectionRegistry capacities = new(
            new IDurableFacilityEquipmentCapacityProjector[]
            {
                new DefinitionMassDurableFacilityEquipmentCapacityProjector(
                    new FixedMassQuery(ArcaneIndexId, 1_300L))
            });
        DurableFacilityEquipmentUsabilityRegistry usability = new(
            new IDurableFacilityEquipmentUsabilityPolicy[]
            {
                new PositiveDurabilityComponentUsabilityPolicy()
            });
        DurableFacilityEquipmentWearRegistry actualWear = new(
            new IDurableFacilityEquipmentWearPolicy[]
            {
                new PositiveDurabilityComponentWearPolicy()
            });
        SwitchableWearQuery wear = new(actualWear);
        FakePhysicalPort physical = new();
        FakeCapacityAuthority capacityAuthority = new();
        FakeLifecycle lifecycle = new(capacityAuthority);
        FakeCustodyDrain drain = new(physical);
        DurableFacilityEquipmentAdmissionFenceRegistry fences = new();
        DurableFacilityEquipmentSlotRuntime slots = new(
            equipmentPolicies,
            capacities,
            usability,
            physical,
            lifecycle,
            capacityAuthority,
            drain,
            fences);
        DurableFacilityEquipmentUseRuntime equipmentUse = new(
            slots,
            slots,
            physical,
            physical,
            usability,
            wear);
        RecordingResearchService service = new();
        DefaultResearchWorkRuntimePort runtime = new(
            service,
            UnavailableEquipmentPhysicalItemGateway.Instance,
            researchPolicies,
            equipmentPolicies,
            slots,
            slots,
            equipmentUse);

        ResearchWorkerHandle worker = new(
            researcher,
            (CharacterId)"character:qa-arcane-index-researcher");
        BuildingInstanceId facilityId = facility.RequirePersistentInstanceId();
        ResearchFacilityHandle facilityHandle = new(facility, facilityId);
        DurableFacilityEquipmentPolicy policy = equipmentPolicies
            .CapturePolicies().Single();
        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            facilityId.Value,
            facilityId,
            facility.centerPos);

        // A source item exists but is still in storage. Reconciliation must create
        // a sequence destination and request its delivery; research remains usable
        // at the unboosted rate until the physical item reaches the buffer.
        ResearchWorkProgressResult baseWork = runtime.ApplyApprovedWork(
            worker,
            facilityHandle,
            ApprovedWork);
        Require(baseWork.Succeeded, "Base research was suppressed while supply was pending.");
        RequireNear(service.TotalAppliedWu, ApprovedWork,
            "Pending equipment changed base approved research work.");
        DurableFacilityEquipmentSlotSnapshot first = slots.CaptureAll().Single();
        string expectedDestination =
            DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                assignment.Key,
                1L);
        Require(
            first.AssignmentSequence == 1L
            && first.Capacity.Value == 1_300L
            && string.Equals(first.DestinationId, expectedDestination,
                StringComparison.Ordinal)
            && !string.Equals(first.DestinationId, facilityId.Value,
                StringComparison.Ordinal)
            && physical.DeliveryRequestCalls == 1
            && physical.RequestedDestinations.SequenceEqual(
                new[] { expectedDestination }, StringComparer.Ordinal)
            && physical.NeverUsedDestination(facilityId.Value),
            "Research used a raw facility destination or lost the exact 1,300g sequence authority.");

        physical.DeliverPendingToFacilityBuffer();
        double beforeDurability = physical.CurrentDurability;
        float progressBeforeBoost = service.TotalAppliedWu;
        ResearchWorkProgressResult boosted = runtime.ApplyApprovedWork(
            worker,
            facilityHandle,
            ApprovedWork);
        Require(boosted.Succeeded, "Usable arcane index rejected boosted research.");
        RequireNear(
            service.TotalAppliedWu - progressBeforeBoost,
            ApprovedWork * 1.1f,
            "Arcane-index multiplier was not applied exactly once.");
        RequireNear(
            physical.CurrentDurability,
            beforeDurability - ApprovedWork * 0.01d,
            "Arcane-index wear was not exactly approvedWU * 0.01.");

        // Wear projection rejection must not invoke the research effect or mutate
        // the durable component.
        wear.RejectNext = true;
        AssertRejectedWithoutProgressOrWear(
            runtime,
            worker,
            facilityHandle,
            service,
            physical,
            "wear projection rejection");

        // The physical mutation boundary can reject without publishing any state.
        physical.RejectNextMutation = true;
        AssertRejectedWithoutProgressOrWear(
            runtime,
            worker,
            facilityHandle,
            service,
            physical,
            "wear mutation rejection");

        // A stale expected revision is a distinct typed mutation failure and must
        // likewise leave both authorities unchanged.
        physical.RejectNextAsRevisionDrift = true;
        AssertRejectedWithoutProgressOrWear(
            runtime,
            worker,
            facilityHandle,
            service,
            physical,
            "wear revision drift");

        // Effect rejection occurs after wear publication. The common use runtime
        // must restore the exact previous component before returning failure.
        service.RejectNext = true;
        AssertRejectedWithoutProgressOrWear(
            runtime,
            worker,
            facilityHandle,
            service,
            physical,
            "research effect rejection");
        Require(
            physical.RollbackCalls == 1,
            "Rejected research effect did not execute one exact wear rollback.");

        physical.SetDurability(0.05d, 100d);
        float beforeDepletionWork = service.TotalAppliedWu;
        ResearchWorkProgressResult depleted = runtime.ApplyApprovedWork(
            worker,
            facilityHandle,
            ApprovedWork);
        Require(
            depleted.Succeeded
            && Math.Abs(physical.CurrentDurability) <= 0.000001d
            && Math.Abs(service.TotalAppliedWu - beforeDepletionWork
                - ApprovedWork * 1.1f) <= 0.0001f,
            "Depleting use did not commit its exact boosted work and zero durability.");
        DurableFacilityEquipmentSlotSnapshot closed = slots.CaptureAll()
            .Single(value => value.AssignmentSequence == 1L);
        Require(
            closed.LifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase
                    .ClosedAwaitingCheckpointGc
            && closed.AuthoritiesRevoked
            && closed.Drain != null
            && closed.Drain.OwnerAcknowledged
            && drain.PrepareCalls == 1
            && drain.AdvanceCalls == 1
            && drain.AcknowledgeCalls == 1,
            "Depletion did not close through the common custody-drain lifecycle.");

        float beforeReplacementBase = service.TotalAppliedWu;
        ResearchWorkProgressResult replacementPending = runtime.ApplyApprovedWork(
            worker,
            facilityHandle,
            ApprovedWork);
        DurableFacilityEquipmentSlotSnapshot replacement = slots.CaptureAll()
            .Single(value => value.AssignmentSequence == 2L);
        Require(
            replacementPending.Succeeded
            && Math.Abs(service.TotalAppliedWu - beforeReplacementBase
                - ApprovedWork) <= 0.0001f
            && string.Equals(
                replacement.DestinationId,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    assignment.Key,
                    2L),
                StringComparison.Ordinal)
            && !string.Equals(
                replacement.DestinationId,
                closed.DestinationId,
                StringComparison.Ordinal)
            && physical.NeverUsedDestination(facilityId.Value),
            "A depleted slot did not reopen as sequence 2 while retaining base research.");
    }

    private static void AssertRejectedWithoutProgressOrWear(
        DefaultResearchWorkRuntimePort runtime,
        ResearchWorkerHandle worker,
        ResearchFacilityHandle facility,
        RecordingResearchService service,
        FakePhysicalPort physical,
        string scenario)
    {
        float progressBefore = service.TotalAppliedWu;
        double durabilityBefore = physical.CurrentDurability;
        ResearchWorkProgressResult result = runtime.ApplyApprovedWork(
            worker,
            facility,
            ApprovedWork);
        Require(
            !result.Succeeded
            && Math.Abs(service.TotalAppliedWu - progressBefore) <= 0.0001f
            && Math.Abs(physical.CurrentDurability - durabilityBefore)
                <= 0.000001d,
            scenario + " changed research progress or durable wear.");
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
                "No authored research BuildingSO is available for the arcane-index fixture.");
        root = new GameObject("QA Research Arcane Index Facility");
        BuildableObject facility = root.AddComponent<BuildableObject>();
        CharacterAiEditorTestDependencies.Inject(facility);
        facility.Initialization(definition, new Vector2Int(8, 6));
        Require(
            facility.SupportsWork(BuiltInWorkTypeIds.Research),
            "The selected authored facility does not support research work.");
        return facility;
    }

    private static CharacterActor CreateResearcher(out GameObject root)
    {
        root = new GameObject("QA Arcane Index Researcher");
        return root.AddComponent<CharacterActor>();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireNear(float actual, float expected, string message)
    {
        if (Math.Abs(actual - expected) > 0.0001f)
            throw new InvalidOperationException(
                message + $" expected={expected:R}, actual={actual:R}");
    }

    private static void RequireNear(double actual, double expected, string message)
    {
        if (Math.Abs(actual - expected) > 0.000001d)
            throw new InvalidOperationException(
                message + $" expected={expected:R}, actual={actual:R}");
    }

    private sealed class RecordingResearchService : IBlueprintResearchWorkService
    {
        internal float TotalAppliedWu { get; private set; }
        internal bool RejectNext { get; set; }

        public bool HasResearchWorkFor(BuildableObject facility) => facility != null;

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds) => ApplyApprovedResearchWork(
            researcher,
            researchFacility,
            seconds);

        public BlueprintResearchWorkResult ApplyApprovedResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float approvedWorkUnits)
        {
            if (RejectNext)
            {
                RejectNext = false;
                return new BlueprintResearchWorkResult(
                    false, null, 0f, TotalAppliedWu, 10_000f, false,
                    "qa-research-effect-rejected");
            }
            TotalAppliedWu += Math.Max(0f, approvedWorkUnits);
            return new BlueprintResearchWorkResult(
                true, null, approvedWorkUnits, TotalAppliedWu, 10_000f, false,
                "qa-research-progress");
        }
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly ItemDefinitionId itemId;
        private readonly PhysicalMassGrams mass;

        internal FixedMassQuery(string itemId, long grams)
        {
            this.itemId = (ItemDefinitionId)itemId;
            mass = new PhysicalMassGrams(grams);
        }

        public long AuthorityRevision => 17L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId value) =>
            value.Equals(itemId)
                ? mass
                : throw new InvalidOperationException(
                    "qa-research-equipment-mass-missing:" + value.Value);

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(subject.ItemId);

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId value,
            PhysicalItemMassSubject subject) => GetDefinitionUnitMass(value);

        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetQuantityMass(lot.Subject.ItemId, lot.Subject, lot.Quantity);

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId value,
            PhysicalItemMassSubject subject,
            int quantity) => GetDefinitionUnitMass(value).Multiply(quantity);
    }

    private sealed class SwitchableWearQuery : IDurableFacilityEquipmentWearQuery
    {
        private readonly IDurableFacilityEquipmentWearQuery inner;

        internal SwitchableWearQuery(IDurableFacilityEquipmentWearQuery inner) =>
            this.inner = inner;

        internal bool RejectNext { get; set; }

        public bool TryProject(
            string policyKind,
            DurableFacilityEquipmentRequirement requirement,
            DurableFacilityEquipmentUseSubject subject,
            double wearAmount,
            out DurableFacilityEquipmentWearProjection projection,
            out string failureReason)
        {
            if (RejectNext)
            {
                RejectNext = false;
                projection = null;
                failureReason = "qa-wear-projection-rejected";
                return false;
            }
            return inner.TryProject(
                policyKind,
                requirement,
                subject,
                wearAmount,
                out projection,
                out failureReason);
        }
    }

    private sealed class FakePhysicalPort :
        IDurableFacilityEquipmentPhysicalPort,
        IDurableFacilityEquipmentComponentMutationPort
    {
        private readonly List<WorldItemStackSnapshot> stacks = new();
        private readonly Dictionary<string, int> committed =
            new(StringComparer.Ordinal);
        private readonly List<string> requestedDestinations = new();
        private double lastRemovedDurability;

        internal FakePhysicalPort()
        {
            stacks.Add(CreateStack(
                "stack:qa-research-arcane-index",
                "warehouse:qa-research-equipment",
                WorldItemStackState.Stored,
                100d,
                100d,
                1L));
        }

        internal int DeliveryRequestCalls { get; private set; }
        internal int RollbackCalls { get; private set; }
        internal bool RejectNextMutation { get; set; }
        internal bool RejectNextAsRevisionDrift { get; set; }
        internal IReadOnlyList<string> RequestedDestinations =>
            requestedDestinations;
        internal double CurrentDurability => stacks.Count == 1
            ? ReadDurability(stacks[0], "current")
            : lastRemovedDurability;

        public IReadOnlyList<WorldItemStackSnapshot> CaptureDestinationStacks(
            string destinationId) => stacks.Where(value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal)).ToArray();

        public int GetCommittedDeliveryQuantity(
            string destinationId,
            ItemDefinitionId itemId) => committed.TryGetValue(
            Key(destinationId, itemId),
            out int quantity)
                ? quantity
                : 0;

        public IReadOnlyList<WorldItemStackSnapshot> CaptureSupplyCandidates(
            ItemDefinitionId itemId) => stacks.Where(value =>
                string.Equals(value.ItemId, itemId.Value, StringComparison.Ordinal)
                && value.Quantity > 0
                && value.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                    or WorldItemStackState.FacilityOutputBuffer).ToArray();

        public bool TryRequestDelivery(
            ItemDefinitionId itemId,
            int quantity,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = 0;
            failureReason = "qa-definition-delivery-not-supported";
            return false;
        }

        public bool TryRequestExactStackDelivery(
            string stackId,
            int quantity,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            WorldItemStackSnapshot stack = stacks.SingleOrDefault(value =>
                string.Equals(value.StackId, stackId, StringComparison.Ordinal));
            if (stack == null || quantity != 1 || stack.Quantity != 1)
            {
                requested = 0;
                failureReason = "qa-exact-stack-unavailable";
                return false;
            }
            DeliveryRequestCalls++;
            requestedDestinations.Add(destinationId);
            stack.DestinationId = destinationId;
            committed[Key(destinationId, (ItemDefinitionId)stack.ItemId)] = 1;
            requested = 1;
            failureReason = string.Empty;
            return true;
        }

        public bool TryReplaceComponentExact(
            string stackId,
            long expectedContentRevision,
            ItemInstanceComponentSaveData replacement,
            out WorldItemStackSnapshot after,
            out string failureReason)
        {
            after = null;
            if (RejectNextAsRevisionDrift)
            {
                RejectNextAsRevisionDrift = false;
                failureReason = "durable-equipment-component-revision-drift";
                return false;
            }
            if (RejectNextMutation)
            {
                RejectNextMutation = false;
                failureReason = "durable-equipment-component-mutation-rejected";
                return false;
            }
            WorldItemStackSnapshot stack = stacks.SingleOrDefault(value =>
                string.Equals(value.StackId, stackId, StringComparison.Ordinal));
            if (stack == null || stack.ContentRevision != expectedContentRevision)
            {
                failureReason = "durable-equipment-component-revision-drift";
                return false;
            }
            stack.Components = ReplaceDurability(stack.Components, replacement);
            stack.ContentRevision = checked(stack.ContentRevision + 1L);
            after = stack;
            failureReason = string.Empty;
            return true;
        }

        public bool TryRestoreComponentExact(
            string stackId,
            ItemInstanceComponentSaveData expectedCurrent,
            ItemInstanceComponentSaveData replacement,
            out WorldItemStackSnapshot after,
            out string failureReason)
        {
            after = null;
            WorldItemStackSnapshot stack = stacks.SingleOrDefault(value =>
                string.Equals(value.StackId, stackId, StringComparison.Ordinal));
            ItemInstanceComponentSaveData actual = stack?.Components
                ?.SingleOrDefault(value => string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.Durability,
                    StringComparison.Ordinal));
            if (actual == null || !string.Equals(
                    actual.ToCanonicalString(),
                    expectedCurrent.ToCanonicalString(),
                    StringComparison.Ordinal))
            {
                failureReason = "durable-equipment-component-restore-drift";
                return false;
            }
            stack.Components = ReplaceDurability(stack.Components, replacement);
            stack.ContentRevision = checked(stack.ContentRevision + 1L);
            RollbackCalls++;
            after = stack;
            failureReason = string.Empty;
            return true;
        }

        internal void DeliverPendingToFacilityBuffer()
        {
            WorldItemStackSnapshot stack = stacks.Single();
            stack.State = WorldItemStackState.FacilityBuffer;
            committed[Key(stack.DestinationId, (ItemDefinitionId)stack.ItemId)] = 0;
        }

        internal void SetDurability(double current, double maximum)
        {
            WorldItemStackSnapshot stack = stacks.Single();
            stack.Components = new[] { CreateDurability(current, maximum) };
            stack.ContentRevision = checked(stack.ContentRevision + 1L);
        }

        internal void ClearDestination(string destinationId)
        {
            WorldItemStackSnapshot[] removed = stacks.Where(value =>
                string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)).ToArray();
            if (removed.Length == 1)
                lastRemovedDurability = ReadDurability(removed[0], "current");
            stacks.RemoveAll(value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal));
        }

        internal bool NeverUsedDestination(string destinationId) =>
            !requestedDestinations.Contains(destinationId, StringComparer.Ordinal);

        private static string Key(string destinationId, ItemDefinitionId itemId) =>
            destinationId + "\n" + itemId.Value;

        private static ItemInstanceComponentSaveData[] ReplaceDurability(
            IEnumerable<ItemInstanceComponentSaveData> source,
            ItemInstanceComponentSaveData replacement) => (source
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(value => value != null && !string.Equals(
                value.componentTypeId,
                ItemInstanceComponentIds.Durability,
                StringComparison.Ordinal))
            .Concat(new[] { replacement.Clone() })
            .ToArray();
    }

    private sealed class FakeCapacityAuthority : IFacilityBufferMassCapacityQuery
    {
        private IReadOnlyList<FacilityBufferCapacityProfile> profiles =
            Array.Empty<FacilityBufferCapacityProfile>();

        public long Revision { get; private set; } = 1L;

        public bool TryGetCapacity(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferMassCapacitySnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public bool TryGetReceipt(
            string tokenId,
            out FacilityBufferMassAdmissionReceipt receipt)
        {
            receipt = default;
            return false;
        }

        public IReadOnlyList<FacilityBufferCapacityProfile> CaptureProfiles() =>
            profiles;

        public bool TryGetCapacityAuthorityFingerprint(
            string destinationId,
            Vector2Int dropPosition,
            out string fingerprint)
        {
            FacilityBufferCapacityProfile[] matches = profiles.Where(value =>
                string.Equals(value.DestinationId, destinationId,
                    StringComparison.Ordinal)
                && value.DropPosition == dropPosition).ToArray();
            if (matches.Length != 1)
            {
                fingerprint = string.Empty;
                return false;
            }
            fingerprint = "qa-research-capacity:" + matches[0].OwnerOperationId
                + ":" + matches[0].MaxMassGrams;
            return true;
        }

        internal void Replace(IEnumerable<FacilityBufferCapacityProfile> values)
        {
            profiles = Array.AsReadOnly((values
                    ?? Array.Empty<FacilityBufferCapacityProfile>())
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray());
            Revision = checked(Revision + 1L);
        }
    }

    private sealed class FakeLifecycle : IFacilityBufferDestinationLifecycleCommand
    {
        private readonly FakeCapacityAuthority capacity;

        internal FakeLifecycle(FakeCapacityAuthority capacity) =>
            this.capacity = capacity;

        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            FacilityBufferDestinationClaim[] claims =
                (desiredClaims ?? Array.Empty<FacilityBufferDestinationClaim>())
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray();
            FacilityBufferCapacityProfile[] profiles =
                (desiredProfiles ?? Array.Empty<FacilityBufferCapacityProfile>())
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray();
            bool valid = string.Equals(
                    ownerDomain,
                    DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                    StringComparison.Ordinal)
                && claims.Length == profiles.Length
                && claims.Zip(profiles, (claim, profile) =>
                    string.Equals(claim.DestinationId, profile.DestinationId,
                        StringComparison.Ordinal)
                    && claim.DropPosition == profile.DropPosition
                    && string.Equals(claim.OwnerOperationId,
                        profile.OwnerOperationId, StringComparison.Ordinal)
                    && string.Equals(claim.OwnerFacilityId,
                        profile.OwnerFacilityId, StringComparison.Ordinal)
                    && profile.MaxMassGrams == 1_300L).All(value => value);
            if (!valid)
            {
                failureReason = "qa-research-authority-pair-invalid";
                return false;
            }
            capacity.Replace(profiles);
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeCustodyDrain :
        IFacilityBufferDestinationCustodyDrainService
    {
        private readonly FakePhysicalPort physical;
        private readonly Dictionary<string,
            FacilityBufferDestinationCustodyDrainSnapshot> byStep =
            new(StringComparer.Ordinal);

        internal FakeCustodyDrain(FakePhysicalPort physical) =>
            this.physical = physical;

        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;
        internal int PrepareCalls { get; private set; }
        internal int AdvanceCalls { get; private set; }
        internal int AcknowledgeCalls { get; private set; }

        public FacilityBufferDestinationCustodyDrainResult TryPrepare(
            FacilityBufferDestinationCustodyDrainDescriptor descriptor)
        {
            PrepareCalls++;
            if (descriptor == null || byStep.ContainsKey(descriptor.StepOperationId))
                return Failure(null, "qa-research-drain-prepare-conflict");
            FacilityBufferDestinationCustodyDrainSnapshot prepared = Create(
                descriptor,
                FacilityBufferDestinationCustodyDrainPhase.Prepared,
                "qa-research-drain-request",
                string.Empty,
                string.Empty);
            byStep.Add(prepared.StepOperationId, prepared);
            return Success(prepared);
        }

        public FacilityBufferDestinationCustodyDrainResult TryAdvance(
            string stepOperationId,
            string requestFingerprint)
        {
            AdvanceCalls++;
            if (!byStep.TryGetValue(stepOperationId, out var current)
                || !string.Equals(current.RequestFingerprint,
                    requestFingerprint, StringComparison.Ordinal))
            {
                return Failure(current, "qa-research-drain-request-mismatch");
            }
            FacilityBufferDestinationCustodyDrainSnapshot committed = Copy(
                current,
                FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck,
                "qa-research-drain-commit",
                "qa-research-drain-receipt");
            byStep[stepOperationId] = committed;
            physical.ClearDestination(committed.SourceDestinationId);
            return Success(committed);
        }

        public FacilityBufferDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            AcknowledgeCalls++;
            if (!byStep.TryGetValue(stepOperationId, out var current)
                || !current.EffectCommitted
                || !string.Equals(current.ReceiptFingerprint,
                    receiptFingerprint, StringComparison.Ordinal))
            {
                return Failure(current, "qa-research-drain-ack-mismatch");
            }
            FacilityBufferDestinationCustodyDrainSnapshot acknowledged = Copy(
                current,
                FacilityBufferDestinationCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
                current.CommitId,
                current.ReceiptFingerprint);
            byStep[stepOperationId] = acknowledged;
            return Success(acknowledged);
        }

        public bool TryCapture(
            string stepOperationId,
            out FacilityBufferDestinationCustodyDrainSnapshot snapshot) =>
            byStep.TryGetValue(stepOperationId, out snapshot);

        private static FacilityBufferDestinationCustodyDrainSnapshot Create(
            FacilityBufferDestinationCustodyDrainDescriptor descriptor,
            FacilityBufferDestinationCustodyDrainPhase phase,
            string requestFingerprint,
            string commitId,
            string receiptFingerprint) => new(
            descriptor.ParentOperationId,
            descriptor.StepOperationId,
            descriptor.OwnerStableId,
            descriptor.OwnerSubjectId,
            descriptor.OwnerFacilityId,
            descriptor.SourceDestinationId,
            descriptor.SourceAuthorityFingerprint,
            requestFingerprint,
            descriptor.OwnerPosition.x,
            descriptor.OwnerPosition.y,
            phase,
            sourceActorCount: 1,
            completedActorCount: IsCommitted(phase) ? 1 : 0,
            sourceOperationCount: 1,
            releasedOperationCount: IsCommitted(phase) ? 1 : 0,
            inputQuantity: 1,
            inputMassGrams: 1_300L,
            releasedQuantity: IsCommitted(phase) ? 1 : 0,
            releasedMassGrams: IsCommitted(phase) ? 1_300L : 0L,
            commitId,
            receiptFingerprint);

        private static FacilityBufferDestinationCustodyDrainSnapshot Copy(
            FacilityBufferDestinationCustodyDrainSnapshot source,
            FacilityBufferDestinationCustodyDrainPhase phase,
            string commitId,
            string receiptFingerprint) => new(
            source.ParentOperationId,
            source.StepOperationId,
            source.OwnerStableId,
            source.OwnerSubjectId,
            source.OwnerFacilityId,
            source.SourceDestinationId,
            source.SourceAuthorityFingerprint,
            source.RequestFingerprint,
            source.OwnerGridX,
            source.OwnerGridY,
            phase,
            source.SourceActorCount,
            IsCommitted(phase) ? source.SourceActorCount
                : source.CompletedActorCount,
            source.SourceOperationCount,
            IsCommitted(phase) ? source.SourceOperationCount
                : source.ReleasedOperationCount,
            source.InputQuantity,
            source.InputMassGrams,
            IsCommitted(phase) ? source.InputQuantity
                : source.ReleasedQuantity,
            IsCommitted(phase) ? source.InputMassGrams
                : source.ReleasedMassGrams,
            commitId,
            receiptFingerprint);

        private static bool IsCommitted(
            FacilityBufferDestinationCustodyDrainPhase phase) => phase is
            FacilityBufferDestinationCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or FacilityBufferDestinationCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;

        private static FacilityBufferDestinationCustodyDrainResult Success(
            FacilityBufferDestinationCustodyDrainSnapshot snapshot) => new(
            FacilityBufferDestinationCustodyDrainStatus.Applied,
            snapshot,
            string.Empty);

        private static FacilityBufferDestinationCustodyDrainResult Failure(
            FacilityBufferDestinationCustodyDrainSnapshot snapshot,
            string reason) => new(
            FacilityBufferDestinationCustodyDrainStatus.Conflict,
            snapshot,
            reason);
    }

    private static WorldItemStackSnapshot CreateStack(
        string stackId,
        string destinationId,
        WorldItemStackState state,
        double current,
        double maximum,
        long revision) => new()
    {
        StackId = stackId,
        ContentRevision = revision,
        ItemId = ArcaneIndexId,
        Quantity = 1,
        State = state,
        Position = new Vector2Int(8, 6),
        DestinationId = destinationId,
        Components = new[] { CreateDurability(current, maximum) }
    };

    private static ItemInstanceComponentSaveData CreateDurability(
        double current,
        double maximum) => new()
    {
        componentTypeId = ItemInstanceComponentIds.Durability,
        schemaVersion = 1,
        affectsStacking = true,
        values = new List<ItemStateValueSaveData>
        {
            new()
            {
                key = "current",
                kind = ItemStateValueKind.Decimal,
                decimalValue = current
            },
            new()
            {
                key = "maximum",
                kind = ItemStateValueKind.Decimal,
                decimalValue = maximum
            }
        }
    };

    private static double ReadDurability(
        WorldItemStackSnapshot stack,
        string key) => stack.Components
        .Single(value => string.Equals(
            value.componentTypeId,
            ItemInstanceComponentIds.Durability,
            StringComparison.Ordinal))
        .values.Single(value => string.Equals(
            value.key,
            key,
            StringComparison.Ordinal)).decimalValue;
}
#endif
