#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PhysicalItemExactSourcePublicationDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Items/Run Physical Exact Source Publication Contracts")]
    public static void RunAll()
    {
        VerifyAtomicMultiLineRetainedPublication();
        VerifyPreparedRollbackRemovesBatchTokenAndAuthority();
        VerifySecondLineFailureLeavesNoPublishedPrefix();
        VerifyRestoreReplacementClearsAbsentOwnerDomain();
        VerifyReleasedCommitRetriesAuthorityRetirementWithoutReacknowledging();
        Debug.Log("V27_PHYSICAL_EXACT_SOURCE_PUBLICATION_PASS");
    }

    private static void VerifyAtomicMultiLineRetainedPublication()
    {
        Fixture fixture = new();
        PhysicalItemExactSourcePublicationPlan plan = fixture.CreatePlan(
            "retained");
        Require(
            fixture.Service.TryPrepare(
                plan,
                out PhysicalItemExactSourcePublicationTransaction transaction,
                out string prepareFailure),
            "Exact source prepare failed: " + prepareFailure);
        Require(
            fixture.Query.GetAllStacks().Count == 4
            && fixture.Query.GetAllStacks().All(value =>
                value.State == WorldItemStackState.FacilityOutputBuffer
                && string.Equals(
                    value.DestinationId,
                    plan.DestinationId,
                    StringComparison.Ordinal)),
            "Prepared exact source did not publish one atomic FacilityOutputBuffer vector.");
        RequireThrows(
            fixture.Service.ValidateBeforeCapture,
            "Prepared exact source did not block save capture.");
        Require(
            fixture.Service.TryCommitRetained(
                transaction,
                out PhysicalItemExactSourcePublicationReceipt receipt,
                out string commitFailure),
            "Exact source retained commit failed: " + commitFailure);
        Require(
            receipt.IsRetained
            && receipt.Stacks.Count == 4
            && receipt.Stacks.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() == 2
            && receipt.TotalMassGrams == 6_000L
            && fixture.Claims.CaptureClaims().Single().AdmissionPolicy
                == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            && fixture.Admission.CaptureProfiles().Single().MaxMassGrams
                == 6_000L,
            "Retained exact source receipt or gram authority drifted.");
        fixture.Service.ValidateBeforeCapture();
    }

    private static void VerifyPreparedRollbackRemovesBatchTokenAndAuthority()
    {
        Fixture fixture = new();
        PhysicalItemExactSourcePublicationPlan plan = fixture.CreatePlan(
            "rollback");
        Require(
            fixture.Service.TryPrepare(
                plan,
                out PhysicalItemExactSourcePublicationTransaction transaction,
                out string prepareFailure),
            "Rollback fixture prepare failed: " + prepareFailure);
        Require(
            fixture.Service.TryRollback(
                transaction,
                "focused-test-rollback",
                out string rollbackFailure),
            "Prepared exact source rollback failed: " + rollbackFailure);
        Require(
            fixture.Query.GetAllStacks().Count == 0
            && fixture.Claims.CaptureClaims().Count == 0
            && fixture.Admission.CaptureProfiles().Count == 0,
            "Prepared rollback leaked physical stacks or destination authority.");
        fixture.Service.ValidateBeforeCapture();
    }

    private static void VerifySecondLineFailureLeavesNoPublishedPrefix()
    {
        Fixture fixture = new(new FailAtStackIndex(3));
        PhysicalItemExactSourcePublicationPlan plan = fixture.CreatePlan(
            "fault-second-line");
        Require(
            !fixture.Service.TryPrepare(plan, out _, out _),
            "Second-line publication fault unexpectedly prepared a batch.");
        Require(
            fixture.Query.GetAllStacks().Count == 0
            && fixture.Claims.CaptureClaims().Count == 0
            && fixture.Admission.CaptureProfiles().Count == 0,
            "Second-line fault left a published prefix or authority residue.");
        fixture.Service.ValidateBeforeCapture();
    }

    private static void VerifyRestoreReplacementClearsAbsentOwnerDomain()
    {
        Fixture fixture = new();
        PhysicalItemExactSourcePublicationPlan plan = fixture.CreatePlan(
            "restore");
        Require(
            fixture.Service.TryPrepare(
                plan,
                out PhysicalItemExactSourcePublicationTransaction transaction,
                out _),
            "Restore fixture could not prepare an exact batch.");
        Require(
            fixture.Service.TryCommitRetained(
                transaction,
                out PhysicalItemExactSourcePublicationReceipt receipt,
                out _),
            "Restore fixture could not commit an exact batch.");
        Require(
            fixture.Publication.TryCaptureBatch(
                plan.BatchCommitId,
                allowAcknowledged: true,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                out bool acknowledged,
                out _,
                out _)
            && acknowledged,
            "Restore fixture could not capture an acknowledged exact batch.");
        fixture.RestoreCandidates.Set(batch);
        PhysicalItemExactSourceRestoreDescriptor descriptor = new(
            plan,
            receipt.Stacks.Select(value => value.StackId).ToArray());
        Require(
            fixture.Service.TryReplaceRestoreAuthorities(
                new[] { plan.OwnerDomain },
                new[] { descriptor },
                out string restoreFailure),
            "Exact source restore authority replacement failed: " + restoreFailure);
        Require(
            fixture.Claims.CaptureClaims().Count == 1
            && fixture.Admission.CaptureProfiles().Single().MaxMassGrams
                == receipt.TotalMassGrams,
            "Exact source restore did not reconstruct its exact gram authority.");
        Require(
            fixture.Service.TryReplaceRestoreAuthorities(
                new[] { plan.OwnerDomain },
                Array.Empty<PhysicalItemExactSourceRestoreDescriptor>(),
                out string clearFailure),
            "Absent exact source owner-domain clear failed: " + clearFailure);
        Require(
            fixture.Claims.CaptureClaims().Count == 0
            && fixture.Admission.CaptureProfiles().Count == 0,
            "Absent exact source owner-domain retained stale authority.");
    }

    private static void
        VerifyReleasedCommitRetriesAuthorityRetirementWithoutReacknowledging()
    {
        Fixture fixture = new(failFirstAuthorityRetirement: true);
        PhysicalItemExactSourcePublicationPlan plan = fixture.CreatePlan(
            "retirement-retry");
        Require(
            fixture.Service.TryPrepare(
                plan,
                out PhysicalItemExactSourcePublicationTransaction transaction,
                out string prepareFailure),
            "Retirement retry fixture prepare failed: " + prepareFailure);
        Require(
            !fixture.Service.TryCommitReleased(
                transaction,
                new Vector2Int(7, 11),
                "qa-retirement-retry",
                out _,
                out string firstFailure)
            && firstFailure.Contains(
                "physical-exact-source-authority-retirement",
                StringComparison.Ordinal),
            "The first authority-retirement fault was not retained for retry: "
            + firstFailure);
        RequireThrows(
            fixture.Service.ValidateBeforeCapture,
            "An acknowledged-but-unretired exact source did not block save capture.");
        Require(
            fixture.AcknowledgementProbe.MutationCount == 4,
            "The first released acknowledgement did not mutate each stack exactly once.");
        Require(
            !fixture.Service.TryCommitRetained(
                transaction,
                out _,
                out string retainedMismatch)
            && retainedMismatch.Contains(
                "acknowledgement-retry-mismatch",
                StringComparison.Ordinal),
            "A released transaction accepted a retained-mode retry: "
            + retainedMismatch);
        Require(
            !fixture.Service.TryCommitReleased(
                transaction,
                new FacilityBufferAcknowledgedOutputReleaseTarget(
                    "warehouse:qa:other",
                    new Vector2Int(9, 13)),
                "qa-retirement-retry",
                out _,
                out string targetMismatch)
            && targetMismatch.Contains(
                "acknowledgement-retry-mismatch",
                StringComparison.Ordinal),
            "A released transaction accepted a different release target: "
            + targetMismatch);
        Require(
            fixture.Service.TryCommitReleased(
                transaction,
                new Vector2Int(7, 11),
                "qa-retirement-retry",
                out PhysicalItemExactSourcePublicationReceipt receipt,
                out string retryFailure),
            "Authority-retirement forward retry failed: " + retryFailure);
        Require(
            !receipt.IsRetained
            && receipt.Stacks.Count == 4
            && fixture.AcknowledgementProbe.MutationCount == 4
            && fixture.Claims.CaptureClaims().Count == 0
            && fixture.Admission.CaptureProfiles().Count == 0,
            "Forward retry did not retire only authority while preserving output.");
        fixture.Service.ValidateBeforeCapture();
    }

    private sealed class Fixture
    {
        private static readonly Vector2Int Position = new(7, 11);
        private readonly FacilityBufferPlannedOutputPublicationDebugScenarios
            .FakeMassQuery mass = new();

        internal Fixture(
            IFacilityBufferPlannedOutputPublicationFaultInjector fault = null,
            bool failFirstAuthorityRetirement = false)
        {
            var catalog = new FacilityBufferPlannedOutputPublicationDebugScenarios
                .FakeCatalog();
            WorldItemRepository repository = new(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            Query = new WorldItemQueryService(
                catalog,
                mass,
                repository,
                EditorNullItemMarkerPresenter.Instance);
            Claims = new FacilityBufferDestinationClaimRegistry();
            Admission = new FacilityBufferMassAdmissionService(
                Claims,
                new FacilityBufferPlannedOutputPublicationDebugScenarios
                    .EmptyOccupancy(),
                mass);
            IFacilityBufferDestinationLifecycleCommand lifecycle = new
                FacilityBufferDestinationLifecycleService(
                Claims,
                Claims,
                Admission,
                Admission);
            if (failFirstAuthorityRetirement)
                lifecycle = new FailFirstAuthorityRetirement(lifecycle);
            AcknowledgementProbe = new CountingAcknowledgementProbe();
            FacilityBufferPlannedOutputPublicationService publication = new(
                repository,
                catalog,
                mass,
                Admission,
                fault,
                AcknowledgementProbe);
            Publication = publication;
            RestoreCandidates = new MutableAcknowledgedCandidates();
            Service = new PhysicalItemExactSourcePublicationService(
                mass,
                lifecycle,
                Claims,
                Admission,
                Admission,
                publication,
                new UnusedRelease(),
                new UnusedDisposition(),
                RestoreCandidates);
        }

        internal WorldItemQueryService Query { get; }
        internal FacilityBufferDestinationClaimRegistry Claims { get; }
        internal FacilityBufferMassAdmissionService Admission { get; }
        internal CountingAcknowledgementProbe AcknowledgementProbe { get; }
        internal FacilityBufferPlannedOutputPublicationService Publication { get; }
        internal MutableAcknowledgedCandidates RestoreCandidates { get; }
        internal PhysicalItemExactSourcePublicationService Service { get; }

        internal PhysicalItemExactSourcePublicationPlan CreatePlan(string operation)
        {
            return new PhysicalItemExactSourcePublicationPlan(
                "qa.exact-source",
                operation,
                Position,
                new[]
                {
                    new FacilityBufferPlannedOutputSlice(
                        "line:a",
                        PhysicalItemMassSubject.ForDefinition(
                            (ItemDefinitionId)"item:qa:a"),
                        5),
                    new FacilityBufferPlannedOutputSlice(
                        "line:b",
                        PhysicalItemMassSubject.ForDefinition(
                            (ItemDefinitionId)"item:qa:b"),
                        2)
                });
        }
    }

    internal sealed class CountingAcknowledgementProbe :
        IFacilityBufferPlannedOutputAcknowledgementFaultInjector
    {
        internal int MutationCount { get; private set; }

        public bool FailBeforeRepositoryMutation(int zeroBasedStackIndex)
        {
            MutationCount++;
            return false;
        }
    }

    private sealed class FailFirstAuthorityRetirement :
        IFacilityBufferDestinationLifecycleCommand
    {
        private readonly IFacilityBufferDestinationLifecycleCommand inner;
        private bool pendingFailure = true;

        internal FailFirstAuthorityRetirement(
            IFacilityBufferDestinationLifecycleCommand inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            if (pendingFailure
                && desiredClaims != null
                && desiredProfiles != null
                && desiredClaims.Count == 0
                && desiredProfiles.Count == 0)
            {
                pendingFailure = false;
                failureReason = "qa-first-retirement-fault";
                return false;
            }
            return inner.TryReplaceOwnedAuthorities(
                ownerDomain,
                desiredClaims,
                desiredProfiles,
                out failureReason);
        }
    }

    internal sealed class MutableAcknowledgedCandidates :
        IFacilityBufferAcknowledgedOutputRestoreCandidateQuery
    {
        private FacilityBufferPlannedOutputRestoreBatchSnapshot batch;
        internal void Set(FacilityBufferPlannedOutputRestoreBatchSnapshot value) =>
            batch = value;
        public bool IsCandidateAvailable => batch != null;
        public IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
            Batches => batch == null
                ? Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>()
                : new[] { batch };
        public bool TryGetBatch(
            string batchCommitId,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot value)
        {
            value = batch;
            return batch != null && string.Equals(
                batch.BatchCommitId,
                batchCommitId,
                StringComparison.Ordinal);
        }
    }

    private sealed class FailAtStackIndex :
        IFacilityBufferPlannedOutputPublicationFaultInjector
    {
        private readonly int index;
        internal FailAtStackIndex(int index) => this.index = index;
        public bool FailBeforeRepositoryAdd(int zeroBasedStackIndex) =>
            zeroBasedStackIndex == index;
    }

    private sealed class UnusedRelease : IFacilityBufferDestinationReleaseService
    {
        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            releasedQuantity = 0;
            failureReason = "unused";
            return false;
        }
    }

    private sealed class UnusedDisposition : IPhysicalItemBatchDispositionService
    {
        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => Unsupported(out receipt, out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => Unsupported(out receipt, out failureReason);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            failureReason = "unused";
            return false;
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            receipt = default;
            return false;
        }

        private static bool Unsupported(
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "unused";
            return false;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
#endif
