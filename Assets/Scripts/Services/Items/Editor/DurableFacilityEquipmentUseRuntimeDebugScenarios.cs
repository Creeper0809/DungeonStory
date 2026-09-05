#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class DurableFacilityEquipmentUseRuntimeDebugScenarios
{
    private const string ItemId = "record:qa-durable-index";
    private const string RequirementId = "qa-durable-index";
    private const string PolicyId = "policy:qa.durable-use";
    private const string OwnerDomain = "qa.durable-use";
    private static readonly BuildingInstanceId FacilityId =
        (BuildingInstanceId)"building:qa-durable-use";
    private static readonly Vector2Int DropPosition = new(18, 9);

    [MenuItem(
        "DungeonStory/Debug/V27/Run Durable Facility Equipment Use Runtime Contracts")]
    public static void RunAll()
    {
        VerifyComponentAwareSelectionAndMissingComponentFailure();
        VerifyWearAndEffectAreOneAtomicUse();
        VerifyMutationDriftCannotCommitEffect();
        VerifyEffectPreflightCannotWearEquipment();
        VerifyFailedAndThrownEffectsRollbackAcrossUnrelatedRevisionChanges();
        VerifyExhaustionDelegatesToTheCommonSlotDrain();
        VerifyAssignedDeliveryAndCommitmentAreNotDoubleCounted();

        Debug.Log(
            "[V27][PASS] Durable facility-equipment use runtime selects the "
            + "exact component-usable stack, fails loud on malformed custody, "
            + "commits wear before one effect, rolls wear back exactly across "
            + "unrelated revision changes, delegates exhaustion to the common "
            + "slot drain, and does not double-count one routed delivery.");
    }

    private static void VerifyComponentAwareSelectionAndMissingComponentFailure()
    {
        Fixture fixture = CreateFixture(
            CreateStack("stack:00-exhausted", 0d),
            CreateStack("stack:01-usable", 72d));
        RecordingEffect effect = new();

        DurableFacilityEquipmentUseResult result = fixture.Runtime
            .TryApplyWearAndEffect(
                fixture.Slot.Snapshot.Key,
                RequirementId,
                7d,
                effect);

        Require(
            result.Status == DurableFacilityEquipmentUseStatus.Applied
            && string.Equals(result.StackId, "stack:01-usable",
                StringComparison.Ordinal)
            && Nearly(ReadDurability(fixture.Physical, "stack:00-exhausted"), 0d)
            && Nearly(ReadDurability(fixture.Physical, "stack:01-usable"), 65d)
            && effect.PreflightCalls == 1
            && effect.CommitCalls == 1,
            "An exhausted stack sorted first prevented exact usable-stack selection.");

        Fixture malformed = CreateFixture(
            CreateStackWithoutDurability("stack:00-malformed"),
            CreateStack("stack:01-otherwise-usable", 80d));
        RecordingEffect blockedEffect = new();
        DurableFacilityEquipmentUseResult blocked = malformed.Runtime
            .TryApplyWearAndEffect(
                malformed.Slot.Snapshot.Key,
                RequirementId,
                5d,
                blockedEffect);

        Require(
            blocked.Status == DurableFacilityEquipmentUseStatus.Conflict
            && string.Equals(
                blocked.FailureReason,
                "durable-equipment-use-incompatible-buffered-item",
                StringComparison.Ordinal)
            && malformed.Physical.ReplaceCalls == 0
            && blockedEffect.PreflightCalls == 0
            && blockedEffect.CommitCalls == 0
            && Nearly(ReadDurability(
                malformed.Physical,
                "stack:01-otherwise-usable"), 80d),
            "A missing durability component was silently skipped or mutated another stack.");
    }

    private static void VerifyWearAndEffectAreOneAtomicUse()
    {
        Fixture fixture = CreateFixture(CreateStack("stack:atomic", 40d));
        RecordingEffect effect = new()
        {
            OnCommit = context =>
            {
                Require(
                    string.Equals(context.Before.StackId, "stack:atomic",
                        StringComparison.Ordinal)
                    && Nearly(ReadDurability(context.Before), 40d)
                    && Nearly(ReadDurability(context.After), 34d)
                    && Nearly(context.WearAmount, 6d),
                    "The effect observed a non-exact wear publication context.");
            }
        };

        DurableFacilityEquipmentUseResult result = fixture.Runtime
            .TryApplyWearAndEffect(
                fixture.Slot.Snapshot.Key,
                RequirementId,
                6d,
                effect);

        Require(
            result.Status == DurableFacilityEquipmentUseStatus.Applied
            && fixture.Physical.ReplaceCalls == 1
            && fixture.Physical.RestoreCalls == 0
            && effect.PreflightCalls == 1
            && effect.CommitCalls == 1
            && Nearly(ReadDurability(fixture.Physical, "stack:atomic"), 34d),
            "A successful use did not publish one exact wear and one exact effect.");
    }

    private static void VerifyMutationDriftCannotCommitEffect()
    {
        Fixture fixture = CreateFixture(CreateStack("stack:drift", 55d));
        fixture.Physical.ForceRevisionDriftOnNextReplace = true;
        RecordingEffect effect = new();

        DurableFacilityEquipmentUseResult result = fixture.Runtime
            .TryApplyWearAndEffect(
                fixture.Slot.Snapshot.Key,
                RequirementId,
                4d,
                effect);

        Require(
            result.Status == DurableFacilityEquipmentUseStatus.Deferred
            && string.Equals(
                result.FailureReason,
                "qa-component-revision-drift",
                StringComparison.Ordinal)
            && fixture.Physical.ReplaceCalls == 1
            && fixture.Physical.RestoreCalls == 0
            && effect.PreflightCalls == 1
            && effect.CommitCalls == 0
            && Nearly(ReadDurability(fixture.Physical, "stack:drift"), 55d),
            "Component revision drift committed an effect or changed durability.");
    }

    private static void VerifyEffectPreflightCannotWearEquipment()
    {
        Fixture fixture = CreateFixture(CreateStack("stack:preflight", 63d));
        RecordingEffect effect = new()
        {
            PreflightSucceeds = false
        };

        DurableFacilityEquipmentUseResult result = fixture.Runtime
            .TryApplyWearAndEffect(
                fixture.Slot.Snapshot.Key,
                RequirementId,
                9d,
                effect);

        Require(
            result.Status == DurableFacilityEquipmentUseStatus.Deferred
            && string.Equals(result.FailureReason, "qa-effect-preflight-blocked",
                StringComparison.Ordinal)
            && effect.PreflightCalls == 1
            && effect.CommitCalls == 0
            && fixture.Physical.ReplaceCalls == 0
            && fixture.Physical.RestoreCalls == 0
            && Nearly(ReadDurability(fixture.Physical, "stack:preflight"), 63d),
            "A rejected effect preflight consumed equipment durability.");
    }

    private static void VerifyFailedAndThrownEffectsRollbackAcrossUnrelatedRevisionChanges()
    {
        VerifyEffectRollback(EffectDisposition.ReturnFalse);
        VerifyEffectRollback(EffectDisposition.Throw);
    }

    private static void VerifyEffectRollback(EffectDisposition disposition)
    {
        string suffix = disposition == EffectDisposition.Throw
            ? "throw"
            : "false";
        Fixture fixture = CreateFixture(CreateStack("stack:" + suffix, 48d));
        RecordingEffect effect = new()
        {
            CommitDisposition = disposition,
            OnCommit = _ => fixture.Physical.AdvanceUnrelatedGlobalRevision()
        };

        DurableFacilityEquipmentUseResult result = fixture.Runtime
            .TryApplyWearAndEffect(
                fixture.Slot.Snapshot.Key,
                RequirementId,
                8d,
                effect);

        DurableFacilityEquipmentUseStatus expected =
            disposition == EffectDisposition.Throw
                ? DurableFacilityEquipmentUseStatus.Conflict
                : DurableFacilityEquipmentUseStatus.Deferred;
        Require(
            result.Status == expected
            && fixture.Physical.ReplaceCalls == 1
            && fixture.Physical.RestoreCalls == 1
            && fixture.Physical.UnrelatedGlobalRevisionAdvances == 1
            && effect.PreflightCalls == 1
            && effect.CommitCalls == 1
            && Nearly(ReadDurability(fixture.Physical, "stack:" + suffix), 48d),
            "A failed effect did not restore exact durability after unrelated global revision drift: "
            + disposition);
    }

    private static void VerifyExhaustionDelegatesToTheCommonSlotDrain()
    {
        Fixture fixture = CreateFixture(CreateStack("stack:last-use", 5d));
        RecordingEffect effect = new();

        DurableFacilityEquipmentUseResult result = fixture.Runtime
            .TryApplyWearAndEffect(
                fixture.Slot.Snapshot.Key,
                RequirementId,
                5d,
                effect);

        Require(
            result.Status ==
                DurableFacilityEquipmentUseStatus.AppliedDrainPending
            && fixture.Slot.CloseCalls == 1
            && string.Equals(
                fixture.Slot.LastCloseReason,
                "equipment-exhausted",
                StringComparison.Ordinal)
            && effect.CommitCalls == 1
            && Nearly(ReadDurability(fixture.Physical, "stack:last-use"), 0d),
            "Wear-to-zero did not commit the effect and enter the common slot drain exactly once.");
    }

    private static void VerifyAssignedDeliveryAndCommitmentAreNotDoubleCounted()
    {
        DurableFacilityEquipmentRequirement requirement = CreateRequirement();
        DurableFacilityEquipmentPolicy policy = CreatePolicy(requirement);
        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            "slot:double-count",
            FacilityId,
            DropPosition);
        string destinationId = DurableFacilityEquipmentSlotIdentity
            .BuildDestinationId(assignment.Key, 1L);
        FakePhysicalPort physical = new(
            CreateStack(
                "stack:routed-and-committed",
                90d,
                destinationId,
                WorldItemStackState.InTransit));
        physical.SetCommitted(destinationId, requirement.ItemId, 1);
        DurableFacilityEquipmentUsabilityRegistry usability =
            CreateUsabilityRegistry();
        DurableFacilityEquipmentSlotRuntime slots = new(
            CreatePolicyRegistry(policy),
            new FixedCapacityProjectionQuery(),
            usability,
            physical,
            AlwaysAcceptLifecycle.Instance,
            EmptyCapacityQuery.Instance,
            UnusedDrainService.Instance,
            new DurableFacilityEquipmentAdmissionFenceRegistry());

        DurableFacilityEquipmentSlotResult created = slots.TryReconcile(assignment);
        DurableFacilityEquipmentSlotResult ensured =
            slots.TryEnsureSupply(assignment.Key);
        DurableFacilityEquipmentRequirementStatus status = ensured.Snapshot
            .Requirements.Single();

        Require(
            created.Succeeded
            && ensured.Status == DurableFacilityEquipmentSlotStatus.Replay
            && status.PendingQuantity == 1
            && status.BufferedUsableQuantity == 0
            && physical.DeliveryRequestCalls == 0,
            "One destination-assigned stack plus its matching commitment was counted as two pending units.");
    }

    private static Fixture CreateFixture(params WorldItemStackSnapshot[] stacks)
    {
        DurableFacilityEquipmentRequirement requirement = CreateRequirement();
        DurableFacilityEquipmentSlotSnapshot snapshot = CreateSlotSnapshot(requirement);
        FixedSlotAuthority slot = new(snapshot);
        FakePhysicalPort physical = new(stacks.Select(value =>
        {
            value.DestinationId = snapshot.DestinationId;
            return value;
        }).ToArray());
        DurableFacilityEquipmentUseRuntime runtime = new(
            slot,
            slot,
            physical,
            physical,
            CreateUsabilityRegistry(),
            new DurableFacilityEquipmentWearRegistry(
                new IDurableFacilityEquipmentWearPolicy[]
                {
                    new PositiveDurabilityComponentWearPolicy()
                }));
        return new Fixture(runtime, slot, physical);
    }

    private static DurableFacilityEquipmentRequirement CreateRequirement() =>
        new(RequirementId, (ItemDefinitionId)ItemId, 1);

    private static DurableFacilityEquipmentPolicy CreatePolicy(
        DurableFacilityEquipmentRequirement requirement) => new(
        PolicyId,
        1L,
        OwnerDomain,
        DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
        DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
        new[] { requirement });

    private static DurableFacilityEquipmentPolicyRegistry CreatePolicyRegistry(
        DurableFacilityEquipmentPolicy policy) => new(
        new IDurableFacilityEquipmentPolicySource[]
        {
            new StaticPolicySource(policy)
        });

    private static DurableFacilityEquipmentUsabilityRegistry
        CreateUsabilityRegistry() => new(
        new IDurableFacilityEquipmentUsabilityPolicy[]
        {
            new PositiveDurabilityComponentUsabilityPolicy()
        });

    private static DurableFacilityEquipmentSlotSnapshot CreateSlotSnapshot(
        DurableFacilityEquipmentRequirement requirement)
    {
        DurableFacilityEquipmentAssignment assignment = CreatePolicy(requirement)
            .CreateAssignment("slot:use", FacilityId, DropPosition);
        long sequence = 1L;
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
            new[]
            {
                new DurableFacilityEquipmentRequirementStatus(
                    requirement,
                    0,
                    1)
            });
    }

    private static WorldItemStackSnapshot CreateStack(
        string stackId,
        double current,
        string destinationId = "destination:fixture",
        WorldItemStackState state = WorldItemStackState.FacilityBuffer) => new()
    {
        StackId = stackId,
        ContentRevision = 7L,
        ItemId = ItemId,
        Quantity = 1,
        State = state,
        Position = DropPosition,
        DestinationId = destinationId,
        Components = new[] { CreateDurability(current) }
    };

    private static WorldItemStackSnapshot CreateStackWithoutDurability(
        string stackId) => new()
    {
        StackId = stackId,
        ContentRevision = 7L,
        ItemId = ItemId,
        Quantity = 1,
        State = WorldItemStackState.FacilityBuffer,
        Position = DropPosition,
        DestinationId = "destination:fixture",
        Components = Array.Empty<ItemInstanceComponentSaveData>()
    };

    private static ItemInstanceComponentSaveData CreateDurability(
        double current) => new()
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
                decimalValue = 100d
            }
        }
    };

    private static double ReadDurability(
        FakePhysicalPort physical,
        string stackId) => ReadDurability(
        DurableFacilityEquipmentUseSubjectCapture.Capture(
            physical.CaptureStack(stackId)));

    private static double ReadDurability(
        DurableFacilityEquipmentUseSubject subject) => subject.Components
        .Single(value => string.Equals(
            value.ComponentTypeId,
            ItemInstanceComponentIds.Durability,
            StringComparison.Ordinal))
        .Values.Single(value => string.Equals(
            value.Key,
            "current",
            StringComparison.Ordinal)).DecimalValue;

    private static bool Nearly(double left, double right) =>
        Math.Abs(left - right) <= 0.000001d;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct Fixture
    {
        internal Fixture(
            DurableFacilityEquipmentUseRuntime runtime,
            FixedSlotAuthority slot,
            FakePhysicalPort physical)
        {
            Runtime = runtime;
            Slot = slot;
            Physical = physical;
        }

        internal DurableFacilityEquipmentUseRuntime Runtime { get; }
        internal FixedSlotAuthority Slot { get; }
        internal FakePhysicalPort Physical { get; }
    }

    private enum EffectDisposition
    {
        Commit,
        ReturnFalse,
        Throw
    }

    private sealed class RecordingEffect : IDurableFacilityEquipmentEffectCommit
    {
        internal bool PreflightSucceeds { get; set; } = true;
        internal EffectDisposition CommitDisposition { get; set; } =
            EffectDisposition.Commit;
        internal Action<DurableFacilityEquipmentUseContext> OnCommit { get; set; }
        internal int PreflightCalls { get; private set; }
        internal int CommitCalls { get; private set; }

        public string EffectKind => "qa-effect";

        public bool TryPreflight(
            DurableFacilityEquipmentSlotSnapshot slot,
            DurableFacilityEquipmentRequirement requirement,
            DurableFacilityEquipmentUseSubject subject,
            double wearAmount,
            out string failureReason)
        {
            PreflightCalls++;
            failureReason = PreflightSucceeds
                ? string.Empty
                : "qa-effect-preflight-blocked";
            return PreflightSucceeds;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            CommitCalls++;
            OnCommit?.Invoke(context);
            if (CommitDisposition == EffectDisposition.Throw)
                throw new InvalidOperationException("qa-effect-threw");
            if (CommitDisposition == EffectDisposition.ReturnFalse)
            {
                failureReason = "qa-effect-commit-rejected";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FixedSlotAuthority :
        IDurableFacilityEquipmentSlotQuery,
        IDurableFacilityEquipmentSlotCommand
    {
        internal FixedSlotAuthority(
            DurableFacilityEquipmentSlotSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        internal DurableFacilityEquipmentSlotSnapshot Snapshot { get; }
        internal int CloseCalls { get; private set; }
        internal string LastCloseReason { get; private set; } = string.Empty;

        public bool TryCapture(
            DurableFacilityEquipmentSlotKey key,
            out DurableFacilityEquipmentSlotSnapshot snapshot)
        {
            snapshot = key.Equals(Snapshot.Key) ? Snapshot : null;
            return snapshot != null;
        }

        public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> CaptureAll() =>
            new[] { Snapshot };

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired) =>
            throw new NotSupportedException();

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode)
        {
            Require(key.Equals(Snapshot.Key), "The use runtime closed another slot.");
            CloseCalls++;
            LastCloseReason = reasonCode;
            return new DurableFacilityEquipmentSlotResult(
                DurableFacilityEquipmentSlotStatus.Applied,
                Snapshot,
                string.Empty);
        }

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key) =>
            throw new NotSupportedException();

        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();
    }

    private sealed class FakePhysicalPort :
        IDurableFacilityEquipmentPhysicalPort,
        IDurableFacilityEquipmentComponentMutationPort
    {
        private readonly List<WorldItemStackSnapshot> stacks;
        private readonly Dictionary<string, int> commitments =
            new(StringComparer.Ordinal);

        internal FakePhysicalPort(params WorldItemStackSnapshot[] stacks)
        {
            this.stacks = (stacks ?? Array.Empty<WorldItemStackSnapshot>())
                .Select(CloneStack)
                .ToList();
        }

        internal bool ForceRevisionDriftOnNextReplace { get; set; }
        internal int ReplaceCalls { get; private set; }
        internal int RestoreCalls { get; private set; }
        internal int DeliveryRequestCalls { get; private set; }
        internal long GlobalRevision { get; private set; } = 1L;
        internal int UnrelatedGlobalRevisionAdvances { get; private set; }

        public IReadOnlyList<WorldItemStackSnapshot> CaptureDestinationStacks(
            string destinationId) => stacks
            .Where(value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(CloneStack)
            .ToArray();

        public int GetCommittedDeliveryQuantity(
            string destinationId,
            ItemDefinitionId itemId) => commitments.TryGetValue(
            CommitmentKey(destinationId, itemId),
            out int quantity)
                ? quantity
                : 0;

        public IReadOnlyList<WorldItemStackSnapshot> CaptureSupplyCandidates(
            ItemDefinitionId itemId) => stacks
            .Where(value => string.Equals(value.ItemId, itemId.Value,
                    StringComparison.Ordinal)
                && value.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                    or WorldItemStackState.FacilityOutputBuffer)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(CloneStack)
            .ToArray();

        public bool TryRequestDelivery(
            ItemDefinitionId itemId,
            int quantity,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            DeliveryRequestCalls++;
            requested = 0;
            failureReason = "qa-category-delivery-not-supported";
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
            DeliveryRequestCalls++;
            requested = 0;
            failureReason = "qa-unexpected-exact-delivery-request";
            return false;
        }

        public bool TryReplaceComponentExact(
            string stackId,
            long expectedContentRevision,
            ItemInstanceComponentSaveData replacement,
            out WorldItemStackSnapshot after,
            out string failureReason)
        {
            ReplaceCalls++;
            WorldItemStackSnapshot current = RequireStack(stackId);
            if (ForceRevisionDriftOnNextReplace)
            {
                ForceRevisionDriftOnNextReplace = false;
                current.ContentRevision = checked(current.ContentRevision + 1L);
                GlobalRevision = checked(GlobalRevision + 1L);
            }
            if (current.ContentRevision != expectedContentRevision)
            {
                after = null;
                failureReason = "qa-component-revision-drift";
                return false;
            }
            ReplaceComponent(current, replacement);
            current.ContentRevision = checked(current.ContentRevision + 1L);
            GlobalRevision = checked(GlobalRevision + 1L);
            after = CloneStack(current);
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
            RestoreCalls++;
            WorldItemStackSnapshot current = RequireStack(stackId);
            ItemInstanceComponentSaveData actual = current.Components
                .SingleOrDefault(value => value != null && string.Equals(
                    value.componentTypeId,
                    expectedCurrent.componentTypeId,
                    StringComparison.Ordinal));
            if (actual == null
                || !string.Equals(
                    actual.ToCanonicalString(),
                    expectedCurrent.ToCanonicalString(),
                    StringComparison.Ordinal))
            {
                after = null;
                failureReason = "qa-component-restore-drift";
                return false;
            }
            ReplaceComponent(current, replacement);
            current.ContentRevision = checked(current.ContentRevision + 1L);
            GlobalRevision = checked(GlobalRevision + 1L);
            after = CloneStack(current);
            failureReason = string.Empty;
            return true;
        }

        internal void AdvanceUnrelatedGlobalRevision()
        {
            GlobalRevision = checked(GlobalRevision + 1L);
            UnrelatedGlobalRevisionAdvances++;
        }

        internal void SetCommitted(
            string destinationId,
            ItemDefinitionId itemId,
            int quantity) => commitments[
            CommitmentKey(destinationId, itemId)] = quantity;

        internal WorldItemStackSnapshot CaptureStack(string stackId) =>
            CloneStack(RequireStack(stackId));

        private WorldItemStackSnapshot RequireStack(string stackId) => stacks
            .Single(value => string.Equals(
                value.StackId,
                stackId,
                StringComparison.Ordinal));

        private static void ReplaceComponent(
            WorldItemStackSnapshot stack,
            ItemInstanceComponentSaveData replacement)
        {
            List<ItemInstanceComponentSaveData> next = (stack.Components
                    ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Where(value => value != null && !string.Equals(
                    value.componentTypeId,
                    replacement.componentTypeId,
                    StringComparison.Ordinal))
                .Select(value => value.Clone())
                .ToList();
            next.Add(replacement.Clone());
            stack.Components = next
                .OrderBy(value => value.componentTypeId, StringComparer.Ordinal)
                .ToArray();
        }

        private static WorldItemStackSnapshot CloneStack(
            WorldItemStackSnapshot source) => new()
        {
            StackId = source.StackId,
            ContentRevision = source.ContentRevision,
            ItemId = source.ItemId,
            Quantity = source.Quantity,
            State = source.State,
            Position = source.Position,
            DestinationId = source.DestinationId,
            Components = (source.Components
                    ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToArray()
        };

        private static string CommitmentKey(
            string destinationId,
            ItemDefinitionId itemId) => destinationId + "\n" + itemId.Value;
    }

    private sealed class StaticPolicySource :
        IDurableFacilityEquipmentPolicySource
    {
        private readonly IReadOnlyList<DurableFacilityEquipmentPolicy> policies;

        internal StaticPolicySource(DurableFacilityEquipmentPolicy policy)
        {
            policies = Array.AsReadOnly(new[] { policy });
        }

        public string SourceId => "qa.durable-use-policy-source";
        public long Revision => 1L;
        public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
            policies;
    }

    private sealed class FixedCapacityProjectionQuery :
        IDurableFacilityEquipmentCapacityProjectionQuery
    {
        public bool TryProjectMaximumMass(
            DurableFacilityEquipmentAssignment assignment,
            out DurableFacilityEquipmentCapacityProjection projection,
            out string failureReason)
        {
            projection = new DurableFacilityEquipmentCapacityProjection(
                assignment.CapacityPolicyKind,
                new PhysicalMassGrams(1300L),
                1L,
                new string('b', 64));
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class AlwaysAcceptLifecycle :
        IFacilityBufferDestinationLifecycleCommand
    {
        internal static readonly AlwaysAcceptLifecycle Instance = new();

        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class EmptyCapacityQuery : IFacilityBufferMassCapacityQuery
    {
        internal static readonly EmptyCapacityQuery Instance = new();
        public long Revision => 1L;

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
            Array.Empty<FacilityBufferCapacityProfile>();

        public bool TryGetCapacityAuthorityFingerprint(
            string destinationId,
            Vector2Int dropPosition,
            out string fingerprint)
        {
            fingerprint = string.Empty;
            return false;
        }
    }

    private sealed class UnusedDrainService :
        IFacilityBufferDestinationCustodyDrainService
    {
        internal static readonly UnusedDrainService Instance = new();
        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;

        public FacilityBufferDestinationCustodyDrainResult TryPrepare(
            FacilityBufferDestinationCustodyDrainDescriptor descriptor) =>
            throw new NotSupportedException();

        public FacilityBufferDestinationCustodyDrainResult TryAdvance(
            string stepOperationId,
            string requestFingerprint) => throw new NotSupportedException();

        public FacilityBufferDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint) => throw new NotSupportedException();

        public bool TryCapture(
            string stepOperationId,
            out FacilityBufferDestinationCustodyDrainSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
    }
}
#endif
