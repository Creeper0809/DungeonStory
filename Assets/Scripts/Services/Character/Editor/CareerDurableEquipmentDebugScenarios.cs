using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CareerDurableEquipmentDebugScenarios
{
    private static readonly BuildingInstanceId AcademyId =
        new("building:qa-career-academy");
    private static readonly Vector2Int AcademyPosition = new(17, 3);

    [MenuItem("Tools/Dungeon Story/QA/Career Durable Equipment")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("CAREER_DURABLE_EQUIPMENT_PASS");
    }

    public static void RunAll()
    {
        // The shared runtime owns component rollback. Keep its focused contract
        // green beside the career-specific effect/assignment seam.
        DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();
        VerifyExactAssignmentAndAward();
        VerifyRejectedAwardDoesNotReportSuccess();
        VerifyMissingPolicyFailsLoudly();
    }

    private static void VerifyExactAssignmentAndAward()
    {
        DurableFacilityEquipmentPolicyRegistry policies = new(
            new IDurableFacilityEquipmentPolicySource[]
            {
                new CareerDurableEquipmentPolicySource()
            });
        RecordingSlotCommands slots = new();
        RecordingUse use = new(slots);
        CareerDurableEquipmentAwardRuntime runtime = new(
            policies,
            slots,
            use);
        int awardCalls = 0;

        bool committed = runtime.TryCommitAward(
            AcademyId,
            AcademyPosition,
            () =>
            {
                awardCalls++;
                return true;
            });

        Require(committed, "The exact career-ledger award did not commit.");
        Require(awardCalls == 1, "The career award was not committed exactly once.");
        Require(slots.ReconcileCalls == 1 && slots.EnsureCalls == 1,
            "The career path did not reconcile and supply exactly one slot.");
        DurableFacilityEquipmentAssignment assignment = slots.LastAssignment;
        Require(assignment != null
            && assignment.Key.LogicalOwnerDomain ==
                CareerDurableEquipmentPolicySource.LogicalOwnerDomain
            && assignment.Key.OwnerSubjectId == AcademyId.Value
            && assignment.OwnerFacilityId.Equals(AcademyId)
            && assignment.DropPosition == AcademyPosition
            && assignment.Requirements.Count == 1
            && assignment.Requirements[0].RequirementId ==
                CareerDurableEquipmentPolicySource.RequirementId
            && assignment.Requirements[0].ItemId.Equals(
                (ItemDefinitionId)DurableToolItemRules.CareerLedger)
            && assignment.Requirements[0].RequiredQuantity == 1,
            "The career-ledger assignment lost exact owner/item/quantity identity.");
        Require(use.Calls == 1
            && use.LastKey.Equals(assignment.Key)
            && use.LastRequirementId ==
                CareerDurableEquipmentPolicySource.RequirementId
            && Math.Abs(use.LastWear
                - CareerDurableEquipmentAwardRuntime.LedgerWearPerAward)
                <= 0.000001d,
            "The career path did not use the exact slot requirement/wear contract.");
    }

    private static void VerifyRejectedAwardDoesNotReportSuccess()
    {
        DurableFacilityEquipmentPolicyRegistry policies = new(
            new IDurableFacilityEquipmentPolicySource[]
            {
                new CareerDurableEquipmentPolicySource()
            });
        RecordingSlotCommands slots = new();
        RecordingUse use = new(slots);
        CareerDurableEquipmentAwardRuntime runtime = new(
            policies,
            slots,
            use);
        int awardCalls = 0;

        bool committed = runtime.TryCommitAward(
            AcademyId,
            AcademyPosition,
            () =>
            {
                awardCalls++;
                return false;
            });

        Require(!committed && awardCalls == 1,
            "A rejected career award was reported as committed.");
        Require(use.LastFailure == "career-mentorship-award-rejected",
            "The rejected award did not preserve its typed rollback reason.");
    }

    private static void VerifyMissingPolicyFailsLoudly()
    {
        CareerDurableEquipmentAwardRuntime runtime = new(
            new MissingPolicyQuery(),
            new RecordingSlotCommands(),
            new RejectUnexpectedUse());
        bool threw = false;
        try
        {
            runtime.TryCommitAward(AcademyId, AcademyPosition, () => true);
        }
        catch (InvalidOperationException exception)
        {
            threw = exception.Message.Contains(
                "career-ledger durable-equipment policy",
                StringComparison.Ordinal);
        }
        Require(threw, "A missing career policy did not fail loudly.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RecordingSlotCommands :
        IDurableFacilityEquipmentSlotCommand
    {
        internal int ReconcileCalls { get; private set; }
        internal int EnsureCalls { get; private set; }
        internal DurableFacilityEquipmentAssignment LastAssignment { get; private set; }
        internal DurableFacilityEquipmentSlotSnapshot Snapshot { get; private set; }

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired)
        {
            ReconcileCalls++;
            LastAssignment = desired;
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
                "Career supply targeted another slot.");
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
                    new string('a', 64)),
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
        private readonly RecordingSlotCommands slots;

        internal RecordingUse(RecordingSlotCommands slots)
        {
            this.slots = slots;
        }

        internal int Calls { get; private set; }
        internal DurableFacilityEquipmentSlotKey LastKey { get; private set; }
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
            LastKey = key;
            LastRequirementId = requirementId;
            LastWear = wearAmount;
            DurableFacilityEquipmentSlotSnapshot snapshot = slots.Snapshot;
            DurableFacilityEquipmentRequirement requirement =
                snapshot.Assignment.Requirements[0];
            DurableFacilityEquipmentUseSubject before = new(
                "stack:career-ledger",
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
                return new DurableFacilityEquipmentUseResult(
                    DurableFacilityEquipmentUseStatus.Deferred,
                    snapshot,
                    string.Empty,
                    failure);
            }
            DurableFacilityEquipmentUseSubject after = new(
                "stack:career-ledger",
                2L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            DurableFacilityEquipmentUseContext context = new(
                snapshot,
                requirement,
                before,
                after,
                wearAmount);
            if (!effect.TryCommit(context, out failure))
            {
                LastFailure = failure;
                return new DurableFacilityEquipmentUseResult(
                    DurableFacilityEquipmentUseStatus.Deferred,
                    snapshot,
                    string.Empty,
                    failure);
            }
            LastFailure = string.Empty;
            return new DurableFacilityEquipmentUseResult(
                DurableFacilityEquipmentUseStatus.Applied,
                snapshot,
                "stack:career-ledger",
                string.Empty);
        }
    }

    private sealed class MissingPolicyQuery :
        IDurableFacilityEquipmentPolicyQuery
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

    private sealed class RejectUnexpectedUse :
        IDurableFacilityEquipmentUseCommand
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
