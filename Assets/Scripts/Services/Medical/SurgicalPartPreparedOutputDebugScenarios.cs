#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SurgicalPartPreparedOutputDebugScenarios
{
    private const long ExactMassGrams = 1234L;
    private static readonly Vector2Int Position = new(7, 9);

    [MenuItem("Tools/Dungeon Story/QA/Run Surgical Part Prepared Output Scenarios")]
    public static void RunAll()
    {
        VerifyFutureSameCapabilityDefinitionUsesTheRegisteredPath();
        VerifyCapacityBlockDoesNotPublish();
        VerifyCapabilityMaximumBlocksPublication();
        VerifyCapabilityMaximumReleaseFailureIsVisible();
        VerifyProofCapacityMismatchDoesNotReserve();
        VerifyPublicationFailureReleasesReservation();
        VerifyRuntimeJoinFailureRollsBackPhysicalPublication();
        VerifyAdmissionCommitFailureReversesEveryOwner();
        VerifySuccessReplayAcknowledgementAndMassJoin();
        Debug.Log("[SurgicalPartPreparedOutput] focused scenarios passed.");
    }

    private static void VerifyFutureSameCapabilityDefinitionUsesTheRegisteredPath()
    {
        string futureItemId =
            SurgeryItemDefinitions.GetProstheticItemId("arm:right");
        SurgicalPartProductionOutputMaximumMassCapability maximum = new();
        Fixture fixture = new();
        Require(
            fixture.Handler.CanHandle(futureItemId)
            && maximum.CanHandle(futureItemId)
            && SurgicalPartProductionOutputSemantics.TryResolveDefinition(
                futureItemId,
                out string nodeId,
                out SurgicalPartKind kind)
            && string.Equals(nodeId, "arm:right", StringComparison.Ordinal)
            && kind == SurgicalPartKind.Prosthetic,
            "A future surgical-part ID with existing semantics requires a handler code edit.");
    }

    private static void VerifyCapabilityMaximumReleaseFailureIsVisible()
    {
        Fixture fixture = new();
        fixture.Admission.ReservedMassGrams = ExactMassGrams + 1L;
        fixture.Admission.FailRelease = true;
        Require(
            !fixture.Publish(out DomainFailure failure)
            && failure.Parameters.ToArray().Any(value => string.Equals(
                value,
                "surgical-part-output-maximum-release-failed",
                StringComparison.Ordinal))
            && fixture.Admission.ReleaseCount == 1
            && fixture.Publication.PublishCount == 0,
            "Maximum-mass reservation release failure was not visible.");
    }

    private static void VerifyProofCapacityMismatchDoesNotReserve()
    {
        Fixture fixture = new();
        ProductionOutputBufferCapacitySourceSnapshot mismatched = new(
            2,
            ExactMassGrams,
            ExactMassGrams * 2L,
            ExactMassGrams * 2L - 1L,
            ExactMassGrams * 2L,
            Fixture.Digest("surgical-capacity-mismatch"));
        Require(
            !fixture.Publish(
                fixture.MaximumMassProof,
                mismatched,
                out _)
            && fixture.Admission.ReserveCount == 0
            && fixture.Publication.PublishCount == 0,
            "A mismatched maximum proof and capacity source reached admission.");
    }

    private static void VerifyCapabilityMaximumBlocksPublication()
    {
        Fixture fixture = new();
        fixture.Admission.ReservedMassGrams = ExactMassGrams + 1L;
        Require(
            !fixture.Publish(),
            "A surgical-part output above its capability maximum was published.");
        Require(
            fixture.Admission.ReserveCount == 1
            && fixture.Admission.ReleaseCount == 1
            && fixture.Publication.PublishCount == 0
            && fixture.Runtime.CommitCount == 0,
            "Maximum-mass rejection crossed the physical publication boundary.");
    }

    private static void VerifyCapacityBlockDoesNotPublish()
    {
        Fixture fixture = new() { RejectCapacity = true };
        Require(!fixture.Publish(), "Capacity rejection unexpectedly succeeded.");
        Require(
            fixture.Admission.ReserveCount == 1
            && fixture.Publication.PublishCount == 0
            && fixture.Runtime.CommitCount == 0
            && fixture.Admission.ReleaseCount == 0,
            "Capacity rejection crossed the physical publication boundary.");
    }

    private static void VerifyPublicationFailureReleasesReservation()
    {
        Fixture fixture = new() { FailPublication = true };
        Require(!fixture.Publish(), "Injected publication failure unexpectedly succeeded.");
        Require(
            fixture.Admission.RequestWasExact
            && fixture.Publication.PublishCount == 1
            && fixture.Runtime.CommitCount == 0
            && fixture.Publication.RollbackCount == 0
            && fixture.Admission.ReleaseCount == 1,
            "Publication failure did not release only the unpublished reservation.");
    }

    private static void VerifyRuntimeJoinFailureRollsBackPhysicalPublication()
    {
        Fixture fixture = new() { FailRuntimeJoin = true };
        Require(!fixture.Publish(), "Injected runtime join failure unexpectedly succeeded.");
        Require(
            fixture.Runtime.CommitCount == 1
            && fixture.Runtime.RollbackCount == 1
            && fixture.Publication.RollbackCount == 1
            && fixture.Admission.ReleaseCount == 1
            && !fixture.Runtime.HasOwner,
            "Runtime join failure left a physical or logical orphan.");
    }

    private static void VerifyAdmissionCommitFailureReversesEveryOwner()
    {
        Fixture fixture = new() { FailAdmissionCommit = true };
        Require(!fixture.Publish(), "Injected admission commit failure unexpectedly succeeded.");
        Require(
            fixture.Runtime.CommitCount == 1
            && fixture.Runtime.RollbackCount == 1
            && fixture.Publication.RollbackCount == 1
            && fixture.Admission.ReleaseCount == 1
            && !fixture.Runtime.HasOwner,
            "Admission failure did not reverse runtime, publication, and reservation ownership.");
    }

    private static void VerifySuccessReplayAcknowledgementAndMassJoin()
    {
        Fixture fixture = new();
        Require(fixture.Publish(), "Exact surgical-part publication failed.");
        Require(
            fixture.Admission.RequestWasExact
            && fixture.Runtime.HasOwner
            && fixture.Runtime.ItemInstanceId.Length > 0
            && fixture.Publication.PublishCount == 1
            && fixture.Admission.CommitCount == 1,
            "Successful publication did not preserve the exact unique component join.");

        Require(
            fixture.Runtime.TryPrepareCraftedOutput(
                fixture.Prepared.ItemId,
                fixture.Prepared.NodeId,
                fixture.Prepared.DisplayName,
                fixture.Prepared.Kind,
                fixture.Prepared.Quality,
                fixture.Prepared.CommitId,
                out SurgicalPartPreparedOutput replay,
                out _)
            && replay.IsReplay
            && fixture.Runtime.TryValidateCommittedCraftedOutput(
                replay.CommitId,
                false,
                out _,
                out _)
            && fixture.Publication.PublishCount == 1,
            "Idempotent replay attempted a second physical publication.");

        fixture.Publication.ReturnMismatchedCandidate = true;
        Require(
            !fixture.Handler.TryAcknowledge(fixture.Prepared.CommitId, out _)
            && fixture.Publication.AcknowledgeCount == 0
            && !fixture.Runtime.Acknowledged,
            "Acknowledgement accepted a candidate with the wrong stack/mass join.");
        fixture.Publication.ReturnMismatchedCandidate = false;
        Require(
            fixture.Handler.TryAcknowledge(fixture.Prepared.CommitId, out _)
            && fixture.Handler.TryAcknowledge(fixture.Prepared.CommitId, out _)
            && fixture.Publication.AcknowledgeCount == 1
            && fixture.Runtime.Acknowledged,
            "Exact acknowledgement was not durable and idempotent.");
        Require(
            fixture.Runtime.TryValidateCommittedCraftedOutput(
                fixture.Prepared.CommitId,
                requireAcknowledged: true,
                out SurgicalPartPublishedOutputSnapshot committed,
                out _)
            && committed.MassGrams == ExactMassGrams,
            "Committed surgical aggregate did not retain the joined physical mass.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly RuntimeFake Runtime;
        internal readonly AdmissionFake Admission;
        internal readonly PublicationFake Publication;
        internal readonly SurgicalPartProductionOutputHandler Handler;
        internal readonly SurgicalPartPreparedOutput Prepared;
        internal readonly FacilityBufferCapacityProfile Profile;
        internal readonly ProductionOutputBatchMaximumMassProof MaximumMassProof;
        internal readonly ProductionOutputBufferCapacitySourceSnapshot Capacity;

        internal Fixture()
        {
            Prepared = new SurgicalPartPreparedOutput
            {
                ItemId = SurgicalPartProductionOutputHandler.ProstheticArmOutputId,
                PhysicalItemInstanceId = "item-instance:surgical-part:41",
                PartInstanceId = "surgical-part:41",
                NodeId = "arm:left",
                DisplayName = "Focused prosthetic arm",
                Kind = SurgicalPartKind.Prosthetic,
                Quality = 1.125f,
                CommitId = "production-output:surgical-part:41",
                ExpectedSequence = 41,
                IsReplay = false
            };
            Runtime = new RuntimeFake(Prepared);
            Admission = new AdmissionFake();
            Publication = new PublicationFake(Runtime);
            Handler = new SurgicalPartProductionOutputHandler(
                Runtime,
                Admission,
                Publication);
            Profile = new FacilityBufferCapacityProfile(
                "production-output:facility:focused",
                Position,
                "economy.production-output",
                "production-output:facility:focused",
                "facility:focused",
                new PhysicalMassGrams(ExactMassGrams * 2L),
                2L);
            const string outputLineId = "output:main";
            string descriptorFingerprint =
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    outputLineId,
                    Prepared.ItemId,
                    SurgicalPartProductionOutputHandler.HandlerCapabilityId,
                    SurgicalPartProductionOutputHandler.HandlerContractVersion,
                    SurgicalPartProductionOutputHandler.HandlerComponentCodecId,
                    SurgicalPartProductionOutputHandler.HandlerComponentCodecVersion);
            ProductionOutputCapabilityDescriptor descriptor = new(
                outputLineId,
                Prepared.ItemId,
                SurgicalPartProductionOutputHandler.HandlerCapabilityId,
                SurgicalPartProductionOutputHandler.HandlerContractVersion,
                SurgicalPartProductionOutputHandler.HandlerComponentCodecId,
                SurgicalPartProductionOutputHandler.HandlerComponentCodecVersion,
                descriptorFingerprint);
            MaximumMassProof = new ProductionOutputBatchMaximumMassProof(
                new[]
                {
                    new ProductionOutputMaximumMassProjection(
                        descriptor,
                        1,
                        ExactMassGrams,
                        ExactMassGrams,
                        1L,
                        Digest("surgical-maximum"))
                });
            Capacity = new ProductionOutputBufferCapacitySourceSnapshot(
                2,
                ExactMassGrams,
                ExactMassGrams * 2L,
                ExactMassGrams * 2L,
                ExactMassGrams * 2L,
                Digest("surgical-capacity"));
            Admission.ExpectedCapacitySourceDigest = Capacity.SourceDigest;
            Admission.ExpectedOutputLineId = outputLineId;
        }

        internal bool RejectCapacity
        {
            set => Admission.RejectCapacity = value;
        }

        internal bool FailPublication
        {
            set => Publication.FailPublish = value;
        }

        internal bool FailRuntimeJoin
        {
            set => Runtime.FailCommit = value;
        }

        internal bool FailAdmissionCommit
        {
            set => Admission.FailCommit = value;
        }

        internal bool Publish() => Publish(out _);

        internal bool Publish(out DomainFailure failure) =>
            Publish(MaximumMassProof, Capacity, out failure);

        internal bool Publish(
            ProductionOutputBatchMaximumMassProof proof,
            ProductionOutputBufferCapacitySourceSnapshot capacity,
            out DomainFailure failure) =>
            Handler.TryPublishPreparedOutputForEditorTest(
            Prepared,
            Profile,
            Position,
            "output:main",
            proof,
            capacity,
            out failure);

        internal static string Digest(string value)
        {
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append(value);
            return digest.ComputeSha256();
        }
    }

    private sealed class AdmissionFake : ISurgicalPartOutputAdmissionPort
    {
        internal bool RejectCapacity;
        internal bool FailCommit;
        internal int ReserveCount;
        internal int CommitCount;
        internal int ReleaseCount;
        internal bool RequestWasExact;
        internal long ReservedMassGrams = ExactMassGrams;
        internal bool FailRelease;
        internal string ExpectedCapacitySourceDigest = string.Empty;
        internal string ExpectedOutputLineId = string.Empty;

        public bool TryReserve(
            FacilityBufferPlannedOutputRequest request,
            out FacilityBufferPlannedOutputToken token,
            out FacilityBufferMassAdmissionFailureCode failureCode,
            out string failureReason)
        {
            ReserveCount++;
            token = default;
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            if (RejectCapacity)
            {
                failureCode = FacilityBufferMassAdmissionFailureCode.CapacityUnavailable;
                failureReason = "focused-capacity-block";
                return false;
            }
            FacilityBufferPlannedOutputSlice slice = request.Slices.Single();
            ItemInstanceComponentSaveData component =
                slice.RuntimeComponents.Single().Materialize();
            RequestWasExact = slice.Quantity == 1
                && slice.RuntimeComponents.Count == 1
                && slice.RuntimeComponents[0].ComponentTypeId
                    == SurgicalPartPreparedOutputComponentCodec.ComponentTypeId
                && slice.Subject.Kind == PhysicalItemMassSubjectKind.GenericDefinition
                && request.PublicationOperationId.Length > 0
                && request.BatchCommitId.Length > 0
                && request.OutcomeFingerprint.Length == 64
                && request.CapacitySourceDigest.Length == 64
                && request.CapacitySourceDigest.All(character =>
                    character is >= '0' and <= '9'
                    || character is >= 'a' and <= 'f')
                && string.Equals(
                    request.CapacitySourceDigest,
                    ExpectedCapacitySourceDigest,
                    StringComparison.Ordinal)
                && request.ExpectedMinimumCapacityGrams
                    == ExactMassGrams * 2L
                && string.Equals(
                    slice.OutputLineId,
                    ExpectedOutputLineId,
                    StringComparison.Ordinal)
                && SurgicalPartPreparedOutputComponentCodec.TryRead(
                    new[] { component },
                    out string partId,
                    out string nodeId,
                    out SurgicalPartKind kind,
                    out float quality,
                    out string commitId)
                && partId == "surgical-part:41"
                && nodeId == "arm:left"
                && kind == SurgicalPartKind.Prosthetic
                && quality == 1.125f
                && commitId == request.BatchCommitId;
            FacilityBufferPlannedOutputSliceSnapshot snapshot = new(
                slice,
                new PhysicalMassGrams(ReservedMassGrams));
            FacilityBufferPlannedOutputSnapshot planned = new(
                "focused-planned-output-fingerprint",
                new[] { snapshot },
                1,
                new PhysicalMassGrams(ReservedMassGrams));
            token = new FacilityBufferPlannedOutputToken(
                "facility-buffer-planned-output-admission:focused",
                request,
                planned,
                1L,
                1L);
            return true;
        }

        public bool TryCommit(
            FacilityBufferPlannedOutputToken token,
            FacilityBufferPlannedOutputPublicationReceipt publication,
            out FacilityBufferPlannedOutputReceipt receipt,
            out FacilityBufferMassAdmissionFailureCode failureCode,
            out string failureReason)
        {
            CommitCount++;
            receipt = default;
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            if (FailCommit)
            {
                failureCode = FacilityBufferMassAdmissionFailureCode.TokenMismatch;
                failureReason = "focused-admission-commit-failure";
                return false;
            }
            receipt = new FacilityBufferPlannedOutputReceipt(token, publication);
            return true;
        }

        public bool TryRelease(
            FacilityBufferPlannedOutputToken token,
            out FacilityBufferMassAdmissionFailureCode failureCode,
            out string failureReason)
        {
            ReleaseCount++;
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            if (FailRelease)
            {
                failureCode = FacilityBufferMassAdmissionFailureCode.TokenMismatch;
                failureReason = "focused-release-failure";
                return false;
            }
            return true;
        }
    }

    private sealed class PublicationFake : ISurgicalPartOutputPublicationPort
    {
        private readonly RuntimeFake runtime;
        private FacilityBufferPlannedOutputPublicationReceipt receipt;
        private bool hasReceipt;
        internal bool FailPublish;
        internal bool ReturnMismatchedCandidate;
        internal int PublishCount;
        internal int RollbackCount;
        internal int AcknowledgeCount;

        internal PublicationFake(RuntimeFake runtime) => this.runtime = runtime;

        public bool TryCaptureBatch(
            string batchCommitId,
            bool allowAcknowledged,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out bool acknowledged,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            acknowledged = runtime.Acknowledged;
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            if (!hasReceipt)
            {
                candidate = null;
                failureReason = "planned-output-batch-missing:" + batchCommitId;
                return false;
            }
            return TryCapturePending(
                batchCommitId,
                out candidate,
                out failureCode,
                out failureReason);
        }

        public bool TryPublish(
            FacilityBufferPlannedOutputToken token,
            out FacilityBufferPlannedOutputPublicationReceipt result,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            PublishCount++;
            result = default;
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = string.Empty;
            if (FailPublish)
            {
                failureCode = FacilityBufferPlannedOutputPublicationFailureCode.RepositoryTransactionFailed;
                failureReason = "focused-publication-failure";
                return false;
            }
            FacilityBufferPlannedOutputSliceSnapshot slice =
                token.PlannedOutput.Slices.Single();
            receipt = new FacilityBufferPlannedOutputPublicationReceipt(
                token.TokenId,
                token.Request.BatchCommitId,
                token.Request.OutcomeFingerprint,
                token.Request.DestinationId,
                token.Request.DropPosition,
                token.Request.ExpectedOwnerDomain,
                token.Request.ExpectedOwnerOperationId,
                token.Request.ExpectedOwnerFacilityId,
                token.Request.ExpectedCapacityRevision,
                token.PlannedOutput.Fingerprint,
                new[]
                {
                    new FacilityBufferPublishedOutputStackReceipt(
                        "world-stack:surgical-part:41",
                        slice.OutputLineId,
                        slice.ItemDefinitionId,
                        1,
                        new PhysicalMassGrams(ExactMassGrams),
                        slice.Source.Subject.ItemInstanceId)
                });
            hasReceipt = true;
            result = receipt;
            return true;
        }

        public bool TryRollback(
            FacilityBufferPlannedOutputPublicationReceipt value,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            RollbackCount++;
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCapturePending(
            string batchCommitId,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = string.Empty;
            long mass = ReturnMismatchedCandidate
                ? ExactMassGrams + 1L
                : ExactMassGrams;
            string stackId = ReturnMismatchedCandidate
                ? "world-stack:wrong"
                : receipt.Stacks.Single().StackId;
            candidate = new FacilityBufferPlannedOutputRestoreBatchSnapshot(
                batchCommitId,
                receipt.OutcomeFingerprint,
                receipt.PlannedOutputFingerprint,
                1,
                mass,
                new[]
                {
                    new FacilityBufferPlannedOutputRestoreStackSnapshot(
                        batchCommitId,
                        receipt.OutcomeFingerprint,
                        receipt.PlannedOutputFingerprint,
                        receipt.Stacks.Single().OutputLineId,
                        0,
                        stackId,
                        receipt.Stacks.Single().ItemDefinitionId.Value,
                        1,
                        mass,
                        "focused-component-signature",
                        WorldItemStackState.FacilityOutputBuffer,
                        receipt.DropPosition,
                        receipt.DestinationId)
                });
            return true;
        }

        public bool TryAcknowledge(
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            AcknowledgeCount++;
            runtime.Acknowledged = true;
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RuntimeFake : ISurgicalPartPreparedOutputRuntime
    {
        private readonly SurgicalPartPreparedOutput canonical;
        internal bool FailCommit;
        internal bool HasOwner;
        internal bool Acknowledged;
        internal int CommitCount;
        internal int RollbackCount;
        internal string ItemInstanceId = string.Empty;

        internal RuntimeFake(SurgicalPartPreparedOutput canonical) =>
            this.canonical = canonical;

        public bool TryPrepareCraftedOutput(
            string itemId,
            string nodeId,
            string displayName,
            SurgicalPartKind kind,
            float quality,
            string commitId,
            out SurgicalPartPreparedOutput prepared,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            prepared = new SurgicalPartPreparedOutput
            {
                ItemId = canonical.ItemId,
                PhysicalItemInstanceId = canonical.PhysicalItemInstanceId,
                PartInstanceId = canonical.PartInstanceId,
                NodeId = canonical.NodeId,
                DisplayName = canonical.DisplayName,
                Kind = canonical.Kind,
                Quality = canonical.Quality,
                CommitId = canonical.CommitId,
                ExpectedSequence = canonical.ExpectedSequence,
                IsReplay = HasOwner
            };
            return string.Equals(commitId, canonical.CommitId, StringComparison.Ordinal);
        }

        public bool TryCommitCraftedOutput(
            SurgicalPartPreparedOutput prepared,
            FacilityBufferPlannedOutputPublicationReceipt published,
            out DomainFailure failure)
        {
            CommitCount++;
            failure = DomainFailure.None;
            if (FailCommit)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    prepared.CommitId,
                    "focused-runtime-join-failure");
                return false;
            }
            HasOwner = true;
            ItemInstanceId = "item-instance:surgical-part:41";
            return published.Stacks.Count == 1
                && published.Stacks[0].Quantity == 1
                && published.Stacks[0].MassGrams == ExactMassGrams;
        }

        public bool TryRollbackCraftedOutput(
            SurgicalPartPreparedOutput prepared,
            FacilityBufferPlannedOutputPublicationReceipt published,
            out string failureReason)
        {
            RollbackCount++;
            HasOwner = false;
            ItemInstanceId = string.Empty;
            failureReason = string.Empty;
            return true;
        }

        public bool TryValidateCommittedCraftedOutput(
            string commitId,
            bool requireAcknowledged,
            out SurgicalPartPublishedOutputSnapshot joined,
            out DomainFailure failure)
        {
            joined = default;
            failure = DomainFailure.None;
            if (!HasOwner
                || !string.Equals(commitId, canonical.CommitId, StringComparison.Ordinal)
                || requireAcknowledged && !Acknowledged)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    commitId,
                    "focused-physical-join-missing");
                return false;
            }
            joined = new SurgicalPartPublishedOutputSnapshot(
                "world-stack:surgical-part:41",
                ItemInstanceId,
                ExactMassGrams,
                Acknowledged);
            return true;
        }
    }
}
#endif
