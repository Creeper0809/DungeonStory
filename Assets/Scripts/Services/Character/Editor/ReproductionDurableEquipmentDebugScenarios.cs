using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ReproductionDurableEquipmentDebugScenarios
{
    private static readonly BuildingInstanceId FacilityId =
        new("building:qa-reproduction-facility");
    private static readonly Vector2Int Position = new(9, 4);

    [MenuItem("Tools/Dungeon Story/QA/Reproduction Durable Equipment")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("REPRODUCTION_DURABLE_EQUIPMENT_PASS");
    }

    public static void RunAll()
    {
        DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();
        VerifyExactAssignmentAndPlan();
        VerifyRejectedPlanDoesNotReportSuccess();
        VerifyMissingPolicyFailsLoudly();
    }

    private static void VerifyExactAssignmentAndPlan()
    {
        DurableFacilityEquipmentPolicyRegistry policies = new(
            new IDurableFacilityEquipmentPolicySource[]
            {
                new ReproductionDurableEquipmentPolicySource()
            });
        RecordingSlots slots = new();
        RecordingUse use = new(slots);
        ReproductionDurableEquipmentUseRuntime runtime = new(
            policies,
            slots,
            use);
        int planCalls = 0;

        bool committed = runtime.TryCommitPlan(
            FacilityId,
            Position,
            () =>
            {
                planCalls++;
                return true;
            });

        DurableFacilityEquipmentAssignment assignment = slots.Assignment;
        Require(committed && planCalls == 1,
            "The reproduction process was not committed exactly once.");
        Require(slots.ReconcileCalls == 1 && slots.EnsureCalls == 1,
            "The reproduction path did not reconcile and supply one slot.");
        Require(assignment != null
            && assignment.Key.LogicalOwnerDomain ==
                ReproductionDurableEquipmentPolicySource.LogicalOwnerDomain
            && assignment.Key.OwnerSubjectId == FacilityId.Value
            && assignment.OwnerFacilityId.Equals(FacilityId)
            && assignment.DropPosition == Position
            && assignment.Requirements.Count == 1
            && assignment.Requirements[0].RequirementId ==
                ReproductionDurableEquipmentPolicySource.RequirementId
            && assignment.Requirements[0].ItemId.Equals(
                (ItemDefinitionId)DurableToolItemRules.BreedingLedger)
            && assignment.Requirements[0].RequiredQuantity == 1,
            "The breeding-ledger assignment lost exact identity or quantity.");
        Require(use.Calls == 1
            && use.LastRequirementId ==
                ReproductionDurableEquipmentPolicySource.RequirementId
            && Math.Abs(use.LastWear
                - ReproductionDurableEquipmentUseRuntime.LedgerWearPerPlan)
                <= 0.000001d,
            "The reproduction path used a different requirement or wear value.");
    }

    private static void VerifyRejectedPlanDoesNotReportSuccess()
    {
        DurableFacilityEquipmentPolicyRegistry policies = new(
            new IDurableFacilityEquipmentPolicySource[]
            {
                new ReproductionDurableEquipmentPolicySource()
            });
        RecordingSlots slots = new();
        RecordingUse use = new(slots);
        ReproductionDurableEquipmentUseRuntime runtime = new(
            policies,
            slots,
            use);
        bool committed = runtime.TryCommitPlan(
            FacilityId,
            Position,
            () => false);

        Require(!committed
            && use.LastFailure == "reproduction-process-plan-rejected",
            "A rejected reproduction process was reported as committed.");
    }

    private static void VerifyMissingPolicyFailsLoudly()
    {
        ReproductionDurableEquipmentUseRuntime runtime = new(
            new MissingPolicies(),
            new RecordingSlots(),
            new UnexpectedUse());
        bool threw = false;
        try
        {
            runtime.TryCommitPlan(FacilityId, Position, () => true);
        }
        catch (InvalidOperationException exception)
        {
            threw = exception.Message.Contains(
                "breeding-ledger durable-equipment policy",
                StringComparison.Ordinal);
        }
        Require(threw, "A missing reproduction policy did not fail loudly.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RecordingSlots : IDurableFacilityEquipmentSlotCommand
    {
        internal int ReconcileCalls { get; private set; }
        internal int EnsureCalls { get; private set; }
        internal DurableFacilityEquipmentAssignment Assignment { get; private set; }
        internal DurableFacilityEquipmentSlotSnapshot Snapshot { get; private set; }

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired)
        {
            ReconcileCalls++;
            Assignment = desired;
            Snapshot = CreateSnapshot(desired);
            return new DurableFacilityEquipmentSlotResult(
                DurableFacilityEquipmentSlotStatus.Applied,
                Snapshot,
                string.Empty);
        }

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key)
        {
            Require(Snapshot != null && key.Equals(Snapshot.Key),
                "Reproduction supply targeted another slot.");
            EnsureCalls++;
            return new DurableFacilityEquipmentSlotResult(
                DurableFacilityEquipmentSlotStatus.Replay,
                Snapshot,
                string.Empty);
        }

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode) => throw new NotSupportedException();

        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();

        private static DurableFacilityEquipmentSlotSnapshot CreateSnapshot(
            DurableFacilityEquipmentAssignment assignment)
        {
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
                    new PhysicalMassGrams(1_000L),
                    1L,
                    new string('b', 64)),
                new[]
                {
                    new DurableFacilityEquipmentRequirementStatus(
                        assignment.Requirements[0],
                        pendingQuantity: 0,
                        bufferedUsableQuantity: 1)
                });
        }
    }

    private sealed class RecordingUse : IDurableFacilityEquipmentUseCommand
    {
        private readonly RecordingSlots slots;

        internal RecordingUse(RecordingSlots slots) => this.slots = slots;

        internal int Calls { get; private set; }
        internal string LastRequirementId { get; private set; } = string.Empty;
        internal double LastWear { get; private set; }
        internal string LastFailure { get; private set; } = string.Empty;

        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect)
        {
            Calls++;
            LastRequirementId = requirementId;
            LastWear = wearAmount;
            DurableFacilityEquipmentSlotSnapshot snapshot = slots.Snapshot;
            DurableFacilityEquipmentRequirement requirement =
                snapshot.Assignment.Requirements[0];
            DurableFacilityEquipmentUseSubject before = new(
                "stack:breeding-ledger",
                1L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            if (!effect.TryPreflight(
                    snapshot,
                    requirement,
                    before,
                    wearAmount,
                    out string failure))
            {
                LastFailure = failure;
                return Failed(snapshot, failure);
            }
            DurableFacilityEquipmentUseSubject after = new(
                "stack:breeding-ledger",
                2L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            if (!effect.TryCommit(
                    new DurableFacilityEquipmentUseContext(
                        snapshot,
                        requirement,
                        before,
                        after,
                        wearAmount),
                    out failure))
            {
                LastFailure = failure;
                return Failed(snapshot, failure);
            }
            LastFailure = string.Empty;
            return new DurableFacilityEquipmentUseResult(
                DurableFacilityEquipmentUseStatus.Applied,
                snapshot,
                "stack:breeding-ledger",
                string.Empty);
        }

        private static DurableFacilityEquipmentUseResult Failed(
            DurableFacilityEquipmentSlotSnapshot snapshot,
            string reason) => new(
            DurableFacilityEquipmentUseStatus.Deferred,
            snapshot,
            string.Empty,
            reason);
    }

    private sealed class MissingPolicies : IDurableFacilityEquipmentPolicyQuery
    {
        public long Revision => 1L;
        public bool TryGetPolicy(
            string policyId,
            out DurableFacilityEquipmentPolicy policy)
        {
            policy = null;
            return false;
        }

        public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
            Array.Empty<DurableFacilityEquipmentPolicy>();
    }

    private sealed class UnexpectedUse : IDurableFacilityEquipmentUseCommand
    {
        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect) =>
            throw new InvalidOperationException(
                "The missing-policy case reached equipment use.");
    }
}
