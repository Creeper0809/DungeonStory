#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class DurableFacilityEquipmentSlotRuntimeDebugScenarios
{
    private const string ArcaneIndexId = "record:arcane-index";
    private const string PolicyId = "policy:qa.durable-arcane-index";
    private const string LogicalOwnerDomain = "qa.durable-arcane-index";
    private static readonly BuildingInstanceId FacilityId =
        (BuildingInstanceId)"building:qa-durable-arcane-index";
    private static readonly Vector2Int DropPosition = new(12, 7);

    [MenuItem(
        "DungeonStory/Debug/V27/Run Durable Facility Equipment Slot Runtime Contracts")]
    public static void RunAll()
    {
        VerifyRegisteredRuntimeLifecycleIsExactAndExtensionClosed();

        Debug.Log(
            "[V27][PASS] Durable facility-equipment slot runtime preserves "
            + "registered policy/capacity/usability authority, exact 1300g "
            + "custody, idempotent supply, fenced drain ordering, and "
            + "sequence-scoped reopening without touching foreign owners.");
    }

    private static void VerifyRegisteredRuntimeLifecycleIsExactAndExtensionClosed()
    {
        DurableFacilityEquipmentRequirement requirement = new(
            "arcane-index",
            (ItemDefinitionId)ArcaneIndexId,
            1);
        DurableFacilityEquipmentPolicy policy = new(
            PolicyId,
            revision: 1L,
            LogicalOwnerDomain,
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
            new[] { requirement });
        DurableFacilityEquipmentPolicyRegistry policies = new(
            new IDurableFacilityEquipmentPolicySource[]
            {
                new StaticPolicySource(policy)
            });
        Require(
            policies.TryGetPolicy(PolicyId, out DurableFacilityEquipmentPolicy registered)
            && ReferenceEquals(policy, registered),
            "The registration-driven durable equipment policy was not discoverable.");

        FixedMassQuery mass = new(ArcaneIndexId, 1300L);
        DurableFacilityEquipmentCapacityProjectionRegistry capacity = new(
            new IDurableFacilityEquipmentCapacityProjector[]
            {
                new DefinitionMassDurableFacilityEquipmentCapacityProjector(mass)
            });
        DurableFacilityEquipmentUsabilityRegistry usability = new(
            new IDurableFacilityEquipmentUsabilityPolicy[]
            {
                new PositiveDurabilityComponentUsabilityPolicy()
            });
        FakePhysicalPort physical = new();
        List<string> eventLog = new();

        FacilityBufferDestinationClaim foreignClaim = new(
            "facility-input:exact:qa-foreign:00000001",
            new Vector2Int(1, 2),
            "qa.foreign-owner",
            "qa-foreign-operation:00000001",
            "building:qa-foreign",
            FacilityBufferDestinationAnchorKind.LiveFacility,
            FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);
        FacilityBufferCapacityProfile foreignProfile = new(
            foreignClaim.DestinationId,
            foreignClaim.DropPosition,
            foreignClaim.OwnerDomain,
            foreignClaim.OwnerOperationId,
            foreignClaim.OwnerFacilityId,
            new PhysicalMassGrams(777L),
            5L);
        FakeCapacityAuthority capacityAuthority = new(
            new[] { foreignProfile });
        FakeLifecycle lifecycle = new(
            capacityAuthority,
            eventLog,
            new[] { foreignClaim },
            new[] { foreignProfile });
        FakeCustodyDrain drain = new(physical, eventLog);
        drain.EnqueueAdvance(AdvanceDisposition.Deferred);
        drain.EnqueueAdvance(AdvanceDisposition.Conflict);
        drain.EnqueueAdvance(AdvanceDisposition.Commit);
        DurableFacilityEquipmentAdmissionFenceRegistry admissionFences = new();

        DurableFacilityEquipmentSlotRuntime runtime = new(
            policies,
            capacity,
            usability,
            physical,
            lifecycle,
            capacityAuthority,
            drain,
            admissionFences);
        FacilityBufferDestinationAdmissionFenceQuery fences = new(
            new IFacilityBufferDestinationAdmissionFenceSource[]
            {
                admissionFences
            });
        DurableFacilityEquipmentAssignment desired = registered.CreateAssignment(
            "slot:qa-arcane-index",
            FacilityId,
            DropPosition);

        DurableFacilityEquipmentSlotResult created = runtime.TryReconcile(desired);
        Require(
            created.Status == DurableFacilityEquipmentSlotStatus.Applied
            && created.Snapshot.AssignmentSequence == 1L
            && created.Snapshot.LifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase.Active,
            "The first registered assignment was not published as active sequence 1.");
        AssertExactAuthorities(
            created.Snapshot,
            lifecycle,
            foreignClaim,
            foreignProfile);

        int publishCallsBeforeReplay = lifecycle.PublishCalls;
        DurableFacilityEquipmentSlotResult replay = runtime.TryReconcile(desired);
        Require(
            replay.Status == DurableFacilityEquipmentSlotStatus.Replay
            && replay.Snapshot.AssignmentSequence == 1L
            && lifecycle.PublishCalls == publishCallsBeforeReplay + 1,
            "An identical active assignment did not replay sequence 1 exactly.");
        AssertExactAuthorities(
            replay.Snapshot,
            lifecycle,
            foreignClaim,
            foreignProfile);

        DurableFacilityEquipmentSlotResult firstSupply =
            runtime.TryEnsureSupply(desired.Key);
        DurableFacilityEquipmentSlotResult duplicateSupply =
            runtime.TryEnsureSupply(desired.Key);
        Require(
            firstSupply.Status == DurableFacilityEquipmentSlotStatus.Applied
            && duplicateSupply.Status == DurableFacilityEquipmentSlotStatus.Replay
            && physical.DeliveryRequestCalls == 1
            && physical.GetCommittedDeliveryQuantity(
                created.Snapshot.DestinationId,
                requirement.ItemId) == 1,
            "Repeated supply reconciliation duplicated an already committed delivery.");

        physical.SetCommittedDelivery(
            created.Snapshot.DestinationId,
            requirement.ItemId,
            0);
        physical.SetOnlyStack(CreateEquipmentStack(
            created.Snapshot.DestinationId,
            currentDurability: 80d));
        DurableFacilityEquipmentSlotResult usable =
            runtime.TryEnsureSupply(desired.Key);
        Require(
            usable.Status == DurableFacilityEquipmentSlotStatus.Replay
            && usable.Snapshot.SupplyReady
            && usable.Snapshot.Requirements.Single().BufferedUsableQuantity == 1
            && physical.DeliveryRequestCalls == 1,
            "A usable buffered tool was not recognized without another delivery.");

        physical.SetOnlyStack(CreateEquipmentStack(
            created.Snapshot.DestinationId,
            currentDurability: 0d));
        DurableFacilityEquipmentSlotResult deferred =
            runtime.TryEnsureSupply(desired.Key);
        Require(
            deferred.Status == DurableFacilityEquipmentSlotStatus.Deferred
            && deferred.Snapshot.LifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase.Draining
            && string.Equals(
                deferred.Snapshot.CloseReasonCode,
                "equipment-exhausted",
                StringComparison.Ordinal)
            && drain.PrepareCalls == 1
            && drain.AdvanceCalls == 1
            && drain.AcknowledgeCalls == 0,
            "Exhausted equipment did not enter the common drain and defer safely.");

        FacilityBufferDestinationAdmissionFenceSubject fenceSubject = new(
            deferred.Snapshot.DestinationId,
            DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
            deferred.Snapshot.OwnerOperationId,
            FacilityId.Value);
        Require(
            fences.TryCaptureOpenFence(
                fenceSubject,
                out FacilityBufferDestinationAdmissionFenceSnapshot openFence)
            && string.Equals(
                openFence.SourceId,
                DurableFacilityEquipmentAdmissionFenceRegistry.StableSourceId,
                StringComparison.Ordinal)
            && lifecycle.HasOwnerClaim(deferred.Snapshot.DestinationId),
            "A deferred common drain did not fence admission while retaining authority.");

        DurableFacilityEquipmentSlotResult conflict =
            runtime.TryAdvancePending().Single();
        Require(
            conflict.Status == DurableFacilityEquipmentSlotStatus.Conflict
            && conflict.Snapshot.LifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase.Draining
            && lifecycle.HasOwnerClaim(conflict.Snapshot.DestinationId)
            && drain.AcknowledgeCalls == 0
            && fences.TryCaptureOpenFence(fenceSubject, out _),
            "A child conflict revoked authority, acknowledged early, or opened admission.");

        DurableFacilityEquipmentSlotRecoveryRuntime recovery = new(
            runtime,
            runtime);
        recovery.OnRestoreCompleted();
        recovery.OnRestoreCompleted();
        DurableFacilityEquipmentSlotSnapshot closed = runtime.CaptureAll()
            .Single(value => value.AssignmentSequence == 1L);
        Require(
            closed.LifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase
                    .ClosedAwaitingCheckpointGc
            && closed.AuthoritiesRevoked
            && closed.Drain.OwnerAcknowledged
            && drain.AdvanceCalls == 3
            && drain.AcknowledgeCalls == 1
            && !physical.CaptureDestinationStacks(
                closed.DestinationId).Any()
            && !lifecycle.HasOwnerClaim(closed.DestinationId)
            && !fences.TryCaptureOpenFence(fenceSubject, out _),
            "Synchronous restore recovery did not clear, revoke, acknowledge, close, and replay idempotently before gameplay resumed.");
        AssertDrainOrdering(eventLog);
        AssertForeignAuthorityUnchanged(
            lifecycle,
            foreignClaim,
            foreignProfile);

        DurableFacilityEquipmentSlotResult reopened =
            runtime.TryReconcile(desired);
        Require(
            reopened.Status == DurableFacilityEquipmentSlotStatus.Applied
            && reopened.Snapshot.AssignmentSequence == 2L
            && !string.Equals(
                reopened.Snapshot.DestinationId,
                closed.DestinationId,
                StringComparison.Ordinal)
            && string.Equals(
                reopened.Snapshot.DestinationId,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    desired.Key,
                    2L),
                StringComparison.Ordinal)
            && runtime.CaptureAll().Select(value => value.AssignmentSequence)
                .SequenceEqual(new[] { 1L, 2L })
            && !fences.TryCaptureOpenFence(
                new FacilityBufferDestinationAdmissionFenceSubject(
                    reopened.Snapshot.DestinationId,
                    DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                    reopened.Snapshot.OwnerOperationId,
                    FacilityId.Value),
                out _),
            "A closed slot did not reopen under a new unfenced sequence identity.");
        AssertExactAuthorities(
            reopened.Snapshot,
            lifecycle,
            foreignClaim,
            foreignProfile);
    }

    private static void AssertExactAuthorities(
        DurableFacilityEquipmentSlotSnapshot snapshot,
        FakeLifecycle lifecycle,
        FacilityBufferDestinationClaim foreignClaim,
        FacilityBufferCapacityProfile foreignProfile)
    {
        FacilityBufferDestinationClaim claim = lifecycle.Claims.Single(value =>
            string.Equals(
                value.OwnerDomain,
                DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                StringComparison.Ordinal));
        FacilityBufferCapacityProfile profile = lifecycle.Profiles.Single(value =>
            string.Equals(
                value.OwnerDomain,
                DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                StringComparison.Ordinal));
        Require(
            lifecycle.Claims.Count == 2
            && lifecycle.Profiles.Count == 2
            && string.Equals(claim.DestinationId, snapshot.DestinationId,
                StringComparison.Ordinal)
            && claim.DropPosition == snapshot.DropPosition
            && string.Equals(claim.OwnerOperationId, snapshot.OwnerOperationId,
                StringComparison.Ordinal)
            && string.Equals(claim.OwnerFacilityId, FacilityId.Value,
                StringComparison.Ordinal)
            && claim.AdmissionPolicy ==
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            && string.Equals(profile.DestinationId, claim.DestinationId,
                StringComparison.Ordinal)
            && profile.DropPosition == claim.DropPosition
            && string.Equals(profile.OwnerOperationId, claim.OwnerOperationId,
                StringComparison.Ordinal)
            && profile.MaxMassGrams == 1300L
            && snapshot.Capacity.Value == 1300L,
            "The slot claim/profile pair was not exact, joined, and 1300g.");
        AssertForeignAuthorityUnchanged(
            lifecycle,
            foreignClaim,
            foreignProfile);
    }

    private static void AssertForeignAuthorityUnchanged(
        FakeLifecycle lifecycle,
        FacilityBufferDestinationClaim expectedClaim,
        FacilityBufferCapacityProfile expectedProfile)
    {
        Require(
            lifecycle.Claims.Any(value => ReferenceEquals(value, expectedClaim))
            && lifecycle.Profiles.Any(value => ReferenceEquals(value, expectedProfile)),
            "Owner-scoped replacement mutated or removed a foreign authority.");
    }

    private static void AssertDrainOrdering(IReadOnlyList<string> events)
    {
        int deferred = IndexOf(events, "drain:advance:deferred");
        int conflict = IndexOf(events, "drain:advance:conflict");
        int committed = IndexOf(events, "drain:advance:committed");
        int revoked = IndexOf(events, "lifecycle:publish:0", committed + 1);
        int acknowledged = IndexOf(events, "drain:acknowledge", revoked + 1);
        Require(
            deferred >= 0
            && conflict > deferred
            && committed > conflict
            && revoked > committed
            && acknowledged > revoked,
            "Child deferred/conflict/commit, authority revoke, and owner ack order drifted.");
    }

    private static int IndexOf(
        IReadOnlyList<string> values,
        string expected,
        int start = 0)
    {
        for (int index = Math.Max(0, start); index < values.Count; index++)
        {
            if (string.Equals(values[index], expected, StringComparison.Ordinal))
                return index;
        }
        return -1;
    }

    private static WorldItemStackSnapshot CreateEquipmentStack(
        string destinationId,
        double currentDurability) => new()
    {
        StackId = "stack:qa-durable-arcane-index",
        ContentRevision = 3L,
        ItemId = ArcaneIndexId,
        Quantity = 1,
        State = WorldItemStackState.FacilityBuffer,
        Position = DropPosition,
        DestinationId = destinationId,
        Components = new[]
        {
            new ItemInstanceComponentSaveData
            {
                componentTypeId = ItemInstanceComponentIds.Durability,
                schemaVersion = 1,
                values = new List<ItemStateValueSaveData>
                {
                    new()
                    {
                        key = "current",
                        kind = ItemStateValueKind.Decimal,
                        decimalValue = currentDurability
                    },
                    new()
                    {
                        key = "maximum",
                        kind = ItemStateValueKind.Decimal,
                        decimalValue = 100d
                    }
                }
            }
        }
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class StaticPolicySource :
        IDurableFacilityEquipmentPolicySource
    {
        private readonly IReadOnlyList<DurableFacilityEquipmentPolicy> policies;

        internal StaticPolicySource(DurableFacilityEquipmentPolicy policy)
        {
            policies = Array.AsReadOnly(new[] { policy });
        }

        public string SourceId => "qa.durable-slot-policy-source";
        public long Revision => 7L;
        public IReadOnlyList<DurableFacilityEquipmentPolicy>
            CapturePolicies() => policies;
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly string itemId;
        private readonly PhysicalMassGrams unitMass;

        internal FixedMassQuery(string itemId, long unitMassGrams)
        {
            this.itemId = itemId;
            unitMass = new PhysicalMassGrams(unitMassGrams);
        }

        public long AuthorityRevision => 13L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId value)
        {
            if (!string.Equals(value.Value, itemId, StringComparison.Ordinal))
                throw new InvalidOperationException("qa-mass-definition-missing");
            return unitMass;
        }

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

    private sealed class FakePhysicalPort :
        IDurableFacilityEquipmentPhysicalPort
    {
        private readonly List<WorldItemStackSnapshot> stacks = new();
        private readonly Dictionary<string, int> committed =
            new(StringComparer.Ordinal);

        internal int DeliveryRequestCalls { get; private set; }

        internal FakePhysicalPort()
        {
            WorldItemStackSnapshot source = CreateEquipmentStack(
                "warehouse:qa-durable-equipment-source",
                currentDurability: 100d);
            source.State = WorldItemStackState.Stored;
            stacks.Add(source);
        }

        public IReadOnlyList<WorldItemStackSnapshot> CaptureDestinationStacks(
            string destinationId) => stacks
            .Where(value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
            .ToArray();

        public int GetCommittedDeliveryQuantity(
            string destinationId,
            ItemDefinitionId itemId) => committed.TryGetValue(
            Key(destinationId, itemId),
            out int quantity)
                ? quantity
                : 0;

        public IReadOnlyList<WorldItemStackSnapshot> CaptureSupplyCandidates(
            ItemDefinitionId itemId) => stacks
            .Where(value => string.Equals(
                value.ItemId,
                itemId.Value,
                StringComparison.Ordinal)
                && value.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                    or WorldItemStackState.FacilityOutputBuffer)
            .ToArray();

        public bool TryRequestExactStackDelivery(
            string stackId,
            int quantity,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            WorldItemStackSnapshot source = stacks.SingleOrDefault(value =>
                string.Equals(value.StackId, stackId, StringComparison.Ordinal));
            if (source == null || quantity <= 0 || quantity > source.Quantity)
            {
                requested = 0;
                failureReason = "qa-exact-stack-unavailable";
                return false;
            }
            DeliveryRequestCalls++;
            source.DestinationId = destinationId;
            requested = quantity;
            failureReason = string.Empty;
            committed[Key(destinationId, (ItemDefinitionId)source.ItemId)] =
                quantity;
            return true;
        }

        internal void SetCommittedDelivery(
            string destinationId,
            ItemDefinitionId itemId,
            int quantity) => committed[Key(destinationId, itemId)] = quantity;

        internal void SetOnlyStack(WorldItemStackSnapshot stack)
        {
            stacks.Clear();
            stacks.Add(stack);
        }

        internal void ClearDestination(string destinationId) => stacks.RemoveAll(
            value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal));

        private static string Key(
            string destinationId,
            ItemDefinitionId itemId) => destinationId + "\n" + itemId.Value;
    }

    private sealed class FakeCapacityAuthority :
        IFacilityBufferMassCapacityQuery
    {
        private IReadOnlyList<FacilityBufferCapacityProfile> profiles;

        internal FakeCapacityAuthority(
            IEnumerable<FacilityBufferCapacityProfile> profiles)
        {
            ReplaceProfiles(profiles);
        }

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
                    && value.DropPosition == dropPosition)
                .ToArray();
            if (matches.Length != 1)
            {
                fingerprint = string.Empty;
                return false;
            }
            FacilityBufferCapacityProfile profile = matches[0];
            fingerprint = "qa-capacity-authority:"
                + profile.OwnerOperationId + ":"
                + profile.MaxMassGrams.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        internal void ReplaceProfiles(
            IEnumerable<FacilityBufferCapacityProfile> values)
        {
            profiles = Array.AsReadOnly((values
                    ?? Array.Empty<FacilityBufferCapacityProfile>())
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray());
            Revision = checked(Revision + 1L);
        }
    }

    private sealed class FakeLifecycle :
        IFacilityBufferDestinationLifecycleCommand
    {
        private readonly FakeCapacityAuthority capacity;
        private readonly List<string> eventLog;
        private IReadOnlyList<FacilityBufferDestinationClaim> claims;
        private IReadOnlyList<FacilityBufferCapacityProfile> profiles;

        internal FakeLifecycle(
            FakeCapacityAuthority capacity,
            List<string> eventLog,
            IEnumerable<FacilityBufferDestinationClaim> claims,
            IEnumerable<FacilityBufferCapacityProfile> profiles)
        {
            this.capacity = capacity;
            this.eventLog = eventLog;
            this.claims = Array.AsReadOnly(claims.ToArray());
            this.profiles = Array.AsReadOnly(profiles.ToArray());
        }

        internal IReadOnlyList<FacilityBufferDestinationClaim> Claims => claims;
        internal IReadOnlyList<FacilityBufferCapacityProfile> Profiles => profiles;
        internal int PublishCalls { get; private set; }

        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            FacilityBufferDestinationClaim[] nextClaims =
                (desiredClaims ?? Array.Empty<FacilityBufferDestinationClaim>())
                .ToArray();
            FacilityBufferCapacityProfile[] nextProfiles =
                (desiredProfiles ?? Array.Empty<FacilityBufferCapacityProfile>())
                .ToArray();
            if (nextClaims.Length != nextProfiles.Length
                || nextClaims.Any(value => !string.Equals(
                    value.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
                || nextProfiles.Any(value => !string.Equals(
                    value.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
                || nextClaims.Select(value => value.DestinationId)
                    .Distinct(StringComparer.Ordinal).Count() != nextClaims.Length
                || nextProfiles.Select(value => value.DestinationId)
                    .Distinct(StringComparer.Ordinal).Count() != nextProfiles.Length
                || nextClaims.Any(claim => !nextProfiles.Any(profile =>
                    SameAuthority(claim, profile))))
            {
                failureReason = "qa-lifecycle-authority-pair-invalid";
                return false;
            }

            FacilityBufferDestinationClaim[] retainedClaims = claims
                .Where(value => !string.Equals(
                    value.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
                .ToArray();
            FacilityBufferCapacityProfile[] retainedProfiles = profiles
                .Where(value => !string.Equals(
                    value.OwnerDomain,
                    ownerDomain,
                    StringComparison.Ordinal))
                .ToArray();
            if (nextClaims.Any(value => retainedClaims.Any(foreign =>
                    string.Equals(
                        value.DestinationId,
                        foreign.DestinationId,
                        StringComparison.Ordinal))))
            {
                failureReason = "qa-lifecycle-foreign-destination-conflict";
                return false;
            }

            claims = Array.AsReadOnly(retainedClaims.Concat(nextClaims)
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray());
            profiles = Array.AsReadOnly(retainedProfiles.Concat(nextProfiles)
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray());
            capacity.ReplaceProfiles(profiles);
            PublishCalls++;
            eventLog.Add("lifecycle:publish:" + nextClaims.Length);
            failureReason = string.Empty;
            return true;
        }

        internal bool HasOwnerClaim(string destinationId) => claims.Any(value =>
            string.Equals(value.DestinationId, destinationId,
                StringComparison.Ordinal)
            && string.Equals(
                value.OwnerDomain,
                DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                StringComparison.Ordinal));

        private static bool SameAuthority(
            FacilityBufferDestinationClaim claim,
            FacilityBufferCapacityProfile profile) =>
            string.Equals(claim.DestinationId, profile.DestinationId,
                StringComparison.Ordinal)
            && claim.DropPosition == profile.DropPosition
            && string.Equals(claim.OwnerDomain, profile.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(claim.OwnerOperationId, profile.OwnerOperationId,
                StringComparison.Ordinal)
            && string.Equals(claim.OwnerFacilityId, profile.OwnerFacilityId,
                StringComparison.Ordinal)
            && profile.MaxMassGrams > 0L;
    }

    private enum AdvanceDisposition
    {
        Deferred,
        Conflict,
        Commit
    }

    private sealed class FakeCustodyDrain :
        IFacilityBufferDestinationCustodyDrainService
    {
        private readonly FakePhysicalPort physical;
        private readonly List<string> eventLog;
        private readonly Queue<AdvanceDisposition> advances = new();
        private readonly Dictionary<string,
            FacilityBufferDestinationCustodyDrainSnapshot> byStep =
            new(StringComparer.Ordinal);

        internal FakeCustodyDrain(
            FakePhysicalPort physical,
            List<string> eventLog)
        {
            this.physical = physical;
            this.eventLog = eventLog;
        }

        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;
        internal int PrepareCalls { get; private set; }
        internal int AdvanceCalls { get; private set; }
        internal int AcknowledgeCalls { get; private set; }

        internal void EnqueueAdvance(AdvanceDisposition disposition) =>
            advances.Enqueue(disposition);

        public FacilityBufferDestinationCustodyDrainResult TryPrepare(
            FacilityBufferDestinationCustodyDrainDescriptor descriptor)
        {
            PrepareCalls++;
            if (descriptor == null
                || byStep.ContainsKey(descriptor.StepOperationId))
            {
                return Failure(
                    FacilityBufferDestinationCustodyDrainStatus.Conflict,
                    null,
                    "qa-drain-prepare-conflict");
            }
            FacilityBufferDestinationCustodyDrainSnapshot prepared = Create(
                descriptor,
                FacilityBufferDestinationCustodyDrainPhase.Prepared,
                "qa-drain-request:0001",
                string.Empty,
                string.Empty);
            byStep.Add(prepared.StepOperationId, prepared);
            eventLog.Add("drain:prepare");
            return Success(
                FacilityBufferDestinationCustodyDrainStatus.Applied,
                prepared);
        }

        public FacilityBufferDestinationCustodyDrainResult TryAdvance(
            string stepOperationId,
            string requestFingerprint)
        {
            AdvanceCalls++;
            if (!byStep.TryGetValue(stepOperationId, out var current)
                || !string.Equals(
                    current.RequestFingerprint,
                    requestFingerprint,
                    StringComparison.Ordinal))
            {
                return Failure(
                    FacilityBufferDestinationCustodyDrainStatus.Conflict,
                    current,
                    "qa-drain-request-mismatch");
            }
            AdvanceDisposition disposition = advances.Count > 0
                ? advances.Dequeue()
                : AdvanceDisposition.Commit;
            if (disposition == AdvanceDisposition.Deferred)
            {
                eventLog.Add("drain:advance:deferred");
                return Failure(
                    FacilityBufferDestinationCustodyDrainStatus.Deferred,
                    current,
                    "qa-child-deferred");
            }
            if (disposition == AdvanceDisposition.Conflict)
            {
                eventLog.Add("drain:advance:conflict");
                return Failure(
                    FacilityBufferDestinationCustodyDrainStatus.Conflict,
                    current,
                    "qa-child-conflict");
            }

            FacilityBufferDestinationCustodyDrainSnapshot committed = Copy(
                current,
                FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck,
                "qa-drain-commit:0001",
                "qa-drain-receipt:0001");
            byStep[stepOperationId] = committed;
            physical.ClearDestination(committed.SourceDestinationId);
            eventLog.Add("drain:advance:committed");
            return Success(
                FacilityBufferDestinationCustodyDrainStatus.Applied,
                committed);
        }

        public FacilityBufferDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            AcknowledgeCalls++;
            if (!byStep.TryGetValue(stepOperationId, out var current)
                || !current.EffectCommitted
                || !string.Equals(
                    current.ReceiptFingerprint,
                    receiptFingerprint,
                    StringComparison.Ordinal))
            {
                return Failure(
                    FacilityBufferDestinationCustodyDrainStatus.Conflict,
                    current,
                    "qa-drain-ack-mismatch");
            }
            FacilityBufferDestinationCustodyDrainSnapshot acknowledged = Copy(
                current,
                FacilityBufferDestinationCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
                current.CommitId,
                current.ReceiptFingerprint);
            byStep[stepOperationId] = acknowledged;
            eventLog.Add("drain:acknowledge");
            return Success(
                FacilityBufferDestinationCustodyDrainStatus.Applied,
                acknowledged);
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
            string receiptFingerprint)
        {
            bool committed = IsCommitted(phase);
            return new FacilityBufferDestinationCustodyDrainSnapshot(
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
                completedActorCount: committed ? 1 : 0,
                sourceOperationCount: 1,
                releasedOperationCount: committed ? 1 : 0,
                inputQuantity: 1,
                inputMassGrams: 1300L,
                releasedQuantity: committed ? 1 : 0,
                releasedMassGrams: committed ? 1300L : 0L,
                commitId,
                receiptFingerprint);
        }

        private static FacilityBufferDestinationCustodyDrainSnapshot Copy(
            FacilityBufferDestinationCustodyDrainSnapshot source,
            FacilityBufferDestinationCustodyDrainPhase phase,
            string commitId,
            string receiptFingerprint)
        {
            bool committed = IsCommitted(phase);
            return new FacilityBufferDestinationCustodyDrainSnapshot(
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
                committed
                    ? source.SourceActorCount
                    : source.CompletedActorCount,
                source.SourceOperationCount,
                committed
                    ? source.SourceOperationCount
                    : source.ReleasedOperationCount,
                source.InputQuantity,
                source.InputMassGrams,
                committed ? source.InputQuantity : source.ReleasedQuantity,
                committed ? source.InputMassGrams : source.ReleasedMassGrams,
                commitId,
                receiptFingerprint);
        }

        private static bool IsCommitted(
            FacilityBufferDestinationCustodyDrainPhase phase) => phase is
            FacilityBufferDestinationCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or FacilityBufferDestinationCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;

        private static FacilityBufferDestinationCustodyDrainResult Success(
            FacilityBufferDestinationCustodyDrainStatus status,
            FacilityBufferDestinationCustodyDrainSnapshot snapshot) => new(
            status,
            snapshot,
            string.Empty);

        private static FacilityBufferDestinationCustodyDrainResult Failure(
            FacilityBufferDestinationCustodyDrainStatus status,
            FacilityBufferDestinationCustodyDrainSnapshot snapshot,
            string reason) => new(status, snapshot, reason);
    }
}
#endif
