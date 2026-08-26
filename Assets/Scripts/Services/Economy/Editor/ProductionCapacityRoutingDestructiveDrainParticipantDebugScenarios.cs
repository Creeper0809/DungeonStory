using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class
    ProductionCapacityRoutingDestructiveDrainParticipantDebugScenarios
{
    private static readonly BuildingInstanceId FacilityId =
        (BuildingInstanceId)"building:qa-capacity-participant";
    private static readonly ProductionOutputDestinationId DestinationId =
        ProductionOutputDestinationId.FromFacility(FacilityId);
    private static readonly ProductionFacilityDestructiveDrainOperationId
        OperationId = ProductionFacilityDestructiveDrainOperationId
            .FromFacility(FacilityId);
    private static readonly string ContributionFingerprint = Digest('a');
    private const string CommitId = "commit:qa-capacity-participant";
    private static readonly string ReceiptFingerprint = Digest('e');

    [MenuItem(
        "DungeonStory/Debug/Economy/Run Capacity Routing Destructive Drain Participant Contracts")]
    public static void RunAll()
    {
        VerifyPrepareDeterminismAndOwnerMapping();
        VerifyDurablePrepareReplayAndOneGramDrift();
        VerifyCommitAndAcknowledgeStatusMapping();
        VerifyIncompleteTerminalResultFailsClosed();
        VerifyRecoveryMatrix();
        Debug.Log(
            "Capacity-routing destructive-drain participant contracts passed.");
    }

    private static void VerifyPrepareDeterminismAndOwnerMapping()
    {
        Fixture forward = Fixture.Create(reverse: false);
        Fixture reverse = Fixture.Create(reverse: true);
        ProductionFacilityDestructiveDrainParticipantPlan left =
            forward.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainParticipantPlan right =
            reverse.Participant.Prepare(CreatePrepareContext());

        Require(
            string.Equals(
                left.ParticipantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                StringComparison.Ordinal)
            && left.ContractVersion == 1,
            "Participant plan header unexpectedly exposes registry dependencies.");
        Require(
            string.Equals(
                left.DurableContributionFingerprint,
                ContributionFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                left.PlanFingerprint,
                right.PlanFingerprint,
                StringComparison.Ordinal)
            && left.Owners.Count == 2
            && right.Owners.Count == 2,
            "Prepare was not deterministic across reversed source catalogs.");
        Require(
            left.Owners.Select(value => value.OwnerStableId).SequenceEqual(
                new[]
                {
                    ProductionFacilityDestructiveDrainOwnerStableIds
                        .RoutingBatch("batch:qa-capacity-a"),
                    ProductionFacilityDestructiveDrainOwnerStableIds
                        .RoutingBatch("batch:qa-capacity-b")
                },
                StringComparer.Ordinal),
            "Prepare did not map batches to canonical ordinal owner IDs.");
        Require(
            left.Owners.All(value =>
                value.Disposition ==
                    ProductionFacilityDestructiveDrainDisposition.Terminalize
                && string.IsNullOrEmpty(value.TargetDestinationId)
                && IsDigest(value.RequestFingerprint))
            && left.Owners.Zip(
                    right.Owners,
                    (a, b) => string.Equals(
                        a.RequestFingerprint,
                        b.RequestFingerprint,
                        StringComparison.Ordinal))
                .All(value => value),
            "Prepare owner disposition or immutable request fingerprints drifted.");
        Require(
            forward.Participant.DependsOnParticipantIds.SequenceEqual(
                new[]
                {
                    ProductionFacilityDestructiveDrainParticipantIds
                        .ApparelWorkOrders,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CombatEquipmentCrafting,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .GenericProductionBills
                },
                StringComparer.Ordinal),
            "Capacity participant dependency contract drifted.");
    }

    private static void VerifyDurablePrepareReplayAndOneGramDrift()
    {
        Fixture fixture = Fixture.Create(reverse: false);
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainOwnerPlan owner = plan.Owners[0];
        ProductionFacilityDestructiveDrainStepContext context =
            CreateStepContext(owner, plan.DurableContributionFingerprint);

        Require(
            fixture.Participant.TryPrepareDurable(
                context,
                out string firstFailure)
            && string.IsNullOrEmpty(firstFailure)
            && fixture.Participant.TryPrepareDurable(
                context,
                out string replayFailure)
            && string.IsNullOrEmpty(replayFailure)
            && fixture.Producer.PreparedRequests.Count == 2
            && string.Equals(
                fixture.Producer.PreparedRequests[0].RequestFingerprint,
                fixture.Producer.PreparedRequests[1].RequestFingerprint,
                StringComparison.Ordinal)
            && fixture.HaulFence.CallCount == 2,
            "Durable prepare did not persist and replay the exact frozen request.");

        string batchId = BatchIdFromOwner(owner.OwnerStableId);
        fixture.ReplaceBatch(batchId, massGrams: 1001L);
        bool acceptedDrift = fixture.Participant.TryPrepareDurable(
            context,
            out string driftFailure);
        Require(
            !acceptedDrift
            && string.Equals(
                driftFailure,
                "production-capacity-routing-durable-prepare-plan-drift",
                StringComparison.Ordinal)
            && fixture.Producer.PreparedRequests.Count == 2
            && fixture.HaulFence.CallCount == 2,
            "A one-gram durable source drift reached producer or haul authority.");
    }

    private static void VerifyCommitAndAcknowledgeStatusMapping()
    {
        Fixture fixture = Fixture.Create(reverse: false);
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext planned =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);

        fixture.Executor.Next = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Deferred);
        ProductionFacilityDestructiveDrainStepResult deferred =
            fixture.Participant.TryCommit(planned);
        Require(
            deferred.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Deferred
            && string.IsNullOrEmpty(deferred.CommitId)
            && string.IsNullOrEmpty(deferred.ReceiptFingerprint),
            "An intermediate producer wait did not map to upper Deferred.");

        fixture.Executor.Next = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Conflict);
        Require(
            fixture.Participant.TryCommit(planned).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict,
            "A producer conflict did not fail closed at the upper participant.");

        fixture.Executor.Next = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Applied,
            CommitId,
            ReceiptFingerprint);
        ProductionFacilityDestructiveDrainStepResult applied =
            fixture.Participant.TryCommit(planned);
        fixture.Executor.Next = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Replay,
            CommitId,
            ReceiptFingerprint);
        ProductionFacilityDestructiveDrainStepResult replay =
            fixture.Participant.TryCommit(planned);
        Require(
            applied.Status == ProductionFacilityDestructiveDrainStepStatus.Applied
            && replay.Status == ProductionFacilityDestructiveDrainStepStatus.Replay
            && string.Equals(applied.CommitId, CommitId, StringComparison.Ordinal)
            && string.Equals(
                replay.ReceiptFingerprint,
                ReceiptFingerprint,
                StringComparison.Ordinal),
            "Terminal producer Applied/Replay receipts did not map exactly.");

        ProductionFacilityDestructiveDrainStepContext committed =
            CreateStepContext(
                plan.Owners[0],
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck,
                CommitId,
                ReceiptFingerprint);
        fixture.Producer.NextAcknowledge = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Deferred);
        ProductionFacilityDestructiveDrainStepResult acknowledgeDeferred =
            fixture.Participant.TryAcknowledge(committed);
        fixture.Producer.NextAcknowledge = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Conflict);
        ProductionFacilityDestructiveDrainStepResult acknowledgeConflict =
            fixture.Participant.TryAcknowledge(committed);
        fixture.Producer.NextAcknowledge = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Applied,
            CommitId,
            ReceiptFingerprint);
        ProductionFacilityDestructiveDrainStepResult acknowledged =
            fixture.Participant.TryAcknowledge(committed);
        fixture.Producer.NextAcknowledge = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Replay,
            CommitId,
            ReceiptFingerprint);
        ProductionFacilityDestructiveDrainStepResult acknowledgedReplay =
            fixture.Participant.TryAcknowledge(committed);
        Require(
            acknowledgeDeferred.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Deferred
            && acknowledgeConflict.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict
            && acknowledged.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied
            && acknowledgedReplay.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Replay
            && fixture.Producer.AcknowledgeCalls == 4,
            "Producer acknowledgement status mapping drifted.");
    }

    private static void VerifyIncompleteTerminalResultFailsClosed()
    {
        Fixture fixture = Fixture.Create(reverse: false);
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext planned =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);

        fixture.Executor.Next = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Applied);
        ProductionFacilityDestructiveDrainStepResult applied =
            fixture.Participant.TryCommit(planned);
        fixture.Executor.Next = CapacityResult(
            ProductionCapacityRoutingDrainStatus.Replay);
        ProductionFacilityDestructiveDrainStepResult replay =
            fixture.Participant.TryCommit(planned);

        Require(
            applied.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict
            && replay.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict,
            "An incomplete producer terminal result escaped as upper success.");
    }

    private static void VerifyRecoveryMatrix()
    {
        Fixture fixture = Fixture.Create(reverse: false);
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainOwnerPlan owner = plan.Owners[0];

        fixture.Producer.Captured = null;
        RequireRecovery(
            fixture,
            CreateStepContext(owner, plan.DurableContributionFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
            ProductionFacilityDestructiveDrainStepStatus.Deferred);
        RequireRecovery(
            fixture,
            CreateStepContext(
                owner,
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged,
                CommitId,
                ReceiptFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction
                .AlreadyAcknowledged,
            ProductionFacilityDestructiveDrainStepStatus.Replay);
        RequireRecovery(
            fixture,
            CreateStepContext(
                owner,
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck,
                CommitId,
                ReceiptFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
            ProductionFacilityDestructiveDrainStepStatus.Conflict);

        fixture.Producer.Captured = CreateProducerState(
            owner,
            ProductionCapacityRoutingDrainPhase.Prepared);
        RequireRecovery(
            fixture,
            CreateStepContext(owner, plan.DurableContributionFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
            ProductionFacilityDestructiveDrainStepStatus.Deferred);
        fixture.Producer.Captured = CreateProducerState(
            owner,
            ProductionCapacityRoutingDrainPhase.EffectCommittedAwaitingOwnerAck,
            terminal: true);
        RequireRecovery(
            fixture,
            CreateStepContext(owner, plan.DurableContributionFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
            ProductionFacilityDestructiveDrainStepStatus.Replay);
        RequireRecovery(
            fixture,
            CreateStepContext(
                owner,
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck,
                CommitId,
                ReceiptFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeAcknowledge,
            ProductionFacilityDestructiveDrainStepStatus.Replay);

        fixture.Producer.Captured = CreateProducerState(
            owner,
            ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc,
            terminal: true);
        RequireRecovery(
            fixture,
            CreateStepContext(
                owner,
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck,
                CommitId,
                ReceiptFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction
                .AlreadyAcknowledged,
            ProductionFacilityDestructiveDrainStepStatus.Replay);
        RequireRecovery(
            fixture,
            CreateStepContext(
                owner,
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged,
                CommitId,
                ReceiptFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction
                .AlreadyAcknowledged,
            ProductionFacilityDestructiveDrainStepStatus.Replay);

        ProductionCapacityRoutingDrainSaveData terminalDrift =
            CreateProducerState(
                owner,
                ProductionCapacityRoutingDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
                terminal: true);
        terminalDrift.receiptFingerprint = Digest('8');
        fixture.Producer.Captured = terminalDrift;
        RequireRecovery(
            fixture,
            CreateStepContext(
                owner,
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged,
                CommitId,
                ReceiptFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
            ProductionFacilityDestructiveDrainStepStatus.Conflict);

        fixture.Producer.Captured.requestFingerprint = Digest('9');
        RequireRecovery(
            fixture,
            CreateStepContext(owner, plan.DurableContributionFingerprint),
            ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
            ProductionFacilityDestructiveDrainStepStatus.Conflict);
    }

    private static void RequireRecovery(
        Fixture fixture,
        ProductionFacilityDestructiveDrainStepContext context,
        ProductionFacilityDestructiveDrainRecoveryAction action,
        ProductionFacilityDestructiveDrainStepStatus status)
    {
        ProductionFacilityDestructiveDrainRecoveryResult recovered =
            fixture.Participant.Recover(context);
        Require(
            recovered.Action == action && recovered.Step.Status == status,
            "Recovery matrix mismatch for owner phase "
            + context.Owner.phase
            + ": expected "
            + action
            + "/"
            + status
            + ", got "
            + recovered.Action
            + "/"
            + recovered.Step.Status);
    }

    private static ProductionFacilityDestructiveDrainPrepareContext
        CreatePrepareContext() => new(
        OperationId,
        ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
        FacilityId,
        DestinationId,
        Digest('f'));

    private static ProductionFacilityDestructiveDrainStepContext
        CreateStepContext(
            ProductionFacilityDestructiveDrainOwnerPlan owner,
            string contributionFingerprint,
            ProductionFacilityDestructiveDrainStepPhase phase =
                ProductionFacilityDestructiveDrainStepPhase.Planned,
            string commitId = "",
            string receiptFingerprint = "") => new(
        OperationId,
        FacilityId,
        ProductionFacilityDestructiveDrainParticipantIds.CapacityRoutingOutbox,
        new ProductionFacilityDestructiveDrainOwnerSaveData
        {
            ownerStableId = owner.OwnerStableId,
            disposition = owner.Disposition,
            targetDestinationId = owner.TargetDestinationId,
            stepOperationId = ProductionFacilityDestructiveDrainCanonical
                .BuildStepOperationId(
                    OperationId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CapacityRoutingOutbox,
                    owner.OwnerStableId),
            phase = phase,
            requestFingerprint = owner.RequestFingerprint,
            commitId = commitId,
            receiptFingerprint = receiptFingerprint
        },
        contributionFingerprint);

    private static ProductionCapacityRoutingDrainSaveData CreateProducerState(
        ProductionFacilityDestructiveDrainOwnerPlan owner,
        ProductionCapacityRoutingDrainPhase phase,
        bool terminal = false) => new()
    {
        stepOperationId = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(
                OperationId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                owner.OwnerStableId),
        ownerStableId = owner.OwnerStableId,
        requestFingerprint = owner.RequestFingerprint,
        phase = phase,
        commitId = terminal ? CommitId : string.Empty,
        receiptFingerprint = terminal ? ReceiptFingerprint : string.Empty
    };

    private static ProductionCapacityRoutingDrainResult CapacityResult(
        ProductionCapacityRoutingDrainStatus status,
        string commitId = "",
        string receiptFingerprint = "") => new(
        status,
        commitId,
        receiptFingerprint,
        status is ProductionCapacityRoutingDrainStatus.Deferred
            or ProductionCapacityRoutingDrainStatus.Conflict
            ? "qa-capacity-result"
            : string.Empty);

    private static string BatchIdFromOwner(string ownerStableId) =>
        ownerStableId.Substring("routing-batch:".Length);

    private static string Digest(char value) => new(value, 64);

    private static bool IsDigest(string value) => value?.Length == 64;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        private readonly FakeBatchQuery batches;
        private readonly FakePhysicalSource physical;

        private Fixture(
            ProductionCapacityRoutingDestructiveDrainParticipant participant,
            FakeBatchQuery batches,
            FakePhysicalSource physical,
            FakeProducer producer,
            FakeHaulFence haulFence,
            FakeExecutor executor)
        {
            Participant = participant;
            this.batches = batches;
            this.physical = physical;
            Producer = producer;
            HaulFence = haulFence;
            Executor = executor;
        }

        internal ProductionCapacityRoutingDestructiveDrainParticipant
            Participant { get; }
        internal FakeProducer Producer { get; }
        internal FakeHaulFence HaulFence { get; }
        internal FakeExecutor Executor { get; }

        internal static Fixture Create(bool reverse)
        {
            ProductionPreparedOutputRoutingBatchSnapshot a = CreateBatch(
                "batch:qa-capacity-a",
                "line:qa-capacity-a",
                1000L);
            ProductionPreparedOutputRoutingBatchSnapshot b = CreateBatch(
                "batch:qa-capacity-b",
                "line:qa-capacity-b",
                2000L);
            ProductionPreparedOutputRoutingBatchSnapshot[] ordered = reverse
                ? new[] { b, a }
                : new[] { a, b };
            FakeRoutingAuthority routing = new(ordered.SelectMany(
                value => value.Lines));
            FakeBatchQuery batches = new(ordered);
            FakePhysicalSource physical = new(ordered);
            FakeProducer producer = new();
            FakeHaulFence haulFence = new();
            FakeExecutor executor = new();
            ProductionCapacityRoutingDestructiveDrainParticipant participant =
                new(
                    new FakeLifecycleQuery(),
                    routing,
                    batches,
                    physical,
                    producer,
                    haulFence,
                    executor);
            return new Fixture(
                participant,
                batches,
                physical,
                producer,
                haulFence,
                executor);
        }

        internal void ReplaceBatch(string batchId, long massGrams)
        {
            ProductionPreparedOutputRoutingBatchSnapshot replacement =
                CreateBatch(batchId, "line:qa-capacity-a", massGrams);
            batches.Set(replacement);
            physical.Set(replacement);
        }
    }

    private sealed class FakeLifecycleQuery :
        IProductionOutputDestinationLifecycleQuery
    {
        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId facilityId)
        {
            ProductionOutputDestinationLifecycleContribution contribution = new(
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                true,
                1L,
                1,
                1L,
                Array.Empty<ProductionOutputLifecycleBlock>(),
                ContributionFingerprint,
                ContributionFingerprint);
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                ProductionOutputDestinationId.FromFacility(facilityId),
                new[] { contribution },
                Digest('b'),
                Digest('b'));
        }
    }

    private sealed class FakeRoutingAuthority :
        IProductionPreparedOutputRoutingAuthority
    {
        private readonly IReadOnlyList<
            ProductionPreparedOutputRoutingLineSnapshot> lines;

        internal FakeRoutingAuthority(
            IEnumerable<ProductionPreparedOutputRoutingLineSnapshot> lines) =>
            this.lines = lines.ToArray();

        public void PublishCommittedBatch(
            ProductionPreparedOutputBatchSaveData completedBatch,
            BuildingInstanceId ownerFacilityId) =>
            throw new NotSupportedException();
        public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
            CaptureAll() => lines;
        public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
            CaptureBill(ProductionBillId ownerBillId) => lines;
        public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
            CaptureDestination(string destinationId) => lines;
        public bool HasOutstandingForBill(ProductionBillId ownerBillId) => true;
        public bool CanRetireBill(ProductionBillId ownerBillId) => false;
        public ProductionPreparedOutputRouteRequestSnapshot PrepareRoute(
            string batchCommitId,
            string lineCommitId,
            string targetDestinationId,
            int targetPositionX,
            int targetPositionY,
            int routedQuantity) => throw new NotSupportedException();
        public IReadOnlyList<ProductionPreparedOutputRouteRequestSnapshot>
            CaptureRouteOperations() =>
            Array.Empty<ProductionPreparedOutputRouteRequestSnapshot>();
        public void CommitPhysicalRoute(
            ProductionPreparedOutputPhysicalRouteReceipt receipt) =>
            throw new NotSupportedException();
        public void AcknowledgePhysicalRoute(
            string routeOperationId,
            string physicalReceiptFingerprint) =>
            throw new NotSupportedException();
    }

    private sealed class FakeBatchQuery :
        IProductionPreparedOutputRoutingBatchQuery
    {
        private readonly Dictionary<string,
            ProductionPreparedOutputRoutingBatchSnapshot> values =
            new(StringComparer.Ordinal);

        internal FakeBatchQuery(
            IEnumerable<ProductionPreparedOutputRoutingBatchSnapshot> batches)
        {
            foreach (ProductionPreparedOutputRoutingBatchSnapshot batch in batches)
                Set(batch);
        }

        public bool TryCaptureBatch(
            string batchCommitId,
            out ProductionPreparedOutputRoutingBatchSnapshot snapshot) =>
            values.TryGetValue(batchCommitId, out snapshot);

        internal void Set(ProductionPreparedOutputRoutingBatchSnapshot batch) =>
            values[batch.BatchCommitId] = batch;
    }

    private sealed class FakePhysicalSource :
        IProductionCapacityRoutingPhysicalSourceQuery
    {
        private readonly Dictionary<string,
            ProductionCapacityRoutingPhysicalSourceSnapshot> values =
            new(StringComparer.Ordinal);

        internal FakePhysicalSource(
            IEnumerable<ProductionPreparedOutputRoutingBatchSnapshot> batches)
        {
            foreach (ProductionPreparedOutputRoutingBatchSnapshot batch in batches)
                Set(batch);
        }

        public bool TryCapture(
            string batchCommitId,
            string sourceDestinationId,
            out ProductionCapacityRoutingPhysicalSourceSnapshot snapshot,
            out string failureReason)
        {
            failureReason = string.Empty;
            return values.TryGetValue(batchCommitId, out snapshot)
                && string.Equals(
                    snapshot.SourceDestinationId,
                    sourceDestinationId,
                    StringComparison.Ordinal);
        }

        internal void Set(ProductionPreparedOutputRoutingBatchSnapshot batch)
        {
            int quantity = batch.Lines.Sum(value => value.OriginalQuantity);
            long mass = batch.Lines.Sum(value => value.OriginalMassGrams);
            values[batch.BatchCommitId] =
                new ProductionCapacityRoutingPhysicalSourceSnapshot(
                    batch.BatchCommitId,
                    DestinationId.Value,
                    new Vector2Int(3, 4),
                    Array.Empty<
                        ProductionCapacityRoutingDrainActorCarrySaveData>(),
                    new[] { "stack:" + batch.BatchCommitId },
                    quantity,
                    mass);
        }
    }

    private sealed class FakeProducer : IProductionCapacityRoutingDrainOutbox
    {
        internal List<ProductionCapacityRoutingDrainRequest> PreparedRequests
            { get; } = new();
        internal ProductionCapacityRoutingDrainResult NextAcknowledge { get; set; }
            = CapacityResult(ProductionCapacityRoutingDrainStatus.Replay,
                CommitId, ReceiptFingerprint);
        internal int AcknowledgeCalls { get; private set; }
        internal ProductionCapacityRoutingDrainSaveData Captured { get; set; }

        public ProductionCapacityRoutingDrainResult TryPrepare(
            ProductionCapacityRoutingDrainRequest request)
        {
            bool replay = PreparedRequests.Any(value => string.Equals(
                value.RequestFingerprint,
                request.RequestFingerprint,
                StringComparison.Ordinal));
            PreparedRequests.Add(request);
            return CapacityResult(replay
                ? ProductionCapacityRoutingDrainStatus.Replay
                : ProductionCapacityRoutingDrainStatus.Applied);
        }

        public ProductionCapacityRoutingDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            AcknowledgeCalls++;
            return NextAcknowledge;
        }

        public bool TryCapture(
            string stepOperationId,
            out ProductionCapacityRoutingDrainSaveData record)
        {
            record = Captured?.Clone();
            return record != null;
        }

        public ProductionCapacityRoutingDrainResult TryBeginRouting(
            string stepOperationId,
            string requestFingerprint) => Unsupported();
        public ProductionCapacityRoutingDrainResult TryRecordLineRouted(
            string stepOperationId,
            string lineCommitId) => Unsupported();
        public ProductionCapacityRoutingDrainResult TryBeginQuiescingActors(
            string stepOperationId,
            IEnumerable<string> finalRouteOperationIds,
            IEnumerable<string> preservedStackIds) => Unsupported();
        public ProductionCapacityRoutingDrainResult TryConfirmActorQuiesced(
            string stepOperationId,
            ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt) =>
            Unsupported();
        public ProductionCapacityRoutingDrainResult
            TryBeginReleasingOperationAuthority(string stepOperationId) =>
            Unsupported();
        public ProductionCapacityRoutingDrainResult
            TryPrepareActorAuthorityRelease(
                string stepOperationId,
                string requestFingerprint,
                ProductionCapacityRoutingActorAuthorityReleaseSaveData plan) =>
            Unsupported();
        public ProductionCapacityRoutingDrainResult TryRecordHaulIntentReleased(
            string stepOperationId,
            string operationId) => Unsupported();
        public ProductionCapacityRoutingDrainResult
            TryCommitActorAuthorityRelease(
                string stepOperationId,
                string planFingerprint,
                string effectFingerprint,
                bool actorPlanFinalized) => Unsupported();
        public ProductionCapacityRoutingDrainResult
            TryBeginAwaitingStablePhysicalState(string stepOperationId) =>
            Unsupported();
        public ProductionCapacityRoutingDrainResult TryRecordStablePhysicalStack(
            string stepOperationId,
            string stackId) => Unsupported();
        public ProductionCapacityRoutingDrainResult
            TryBeginAwaitingDurableCheckpointGc(string stepOperationId) =>
            Unsupported();
        public ProductionCapacityRoutingDrainResult TryCommitEffect(
            string stepOperationId,
            string observedRemovedBatchCommitId,
            int preservedQuantity,
            long preservedMassGrams,
            string resultFingerprint) => Unsupported();
        public ProductionCapacityRoutingDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint) => Unsupported();

        private static ProductionCapacityRoutingDrainResult Unsupported() =>
            CapacityResult(ProductionCapacityRoutingDrainStatus.Conflict);
    }

    private sealed class FakeHaulFence : IProductionCapacityRoutingHaulPlanFence
    {
        internal int CallCount { get; private set; }

        public bool TryReleaseUnpickedPlans(
            string batchCommitId,
            IReadOnlyList<ProductionCapacityRoutingDrainActorCarrySaveData>
                frozenCarries,
            out string failureReason)
        {
            CallCount++;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeExecutor :
        IProductionCapacityRoutingDrainExecutionCoordinator
    {
        internal ProductionCapacityRoutingDrainResult Next { get; set; } =
            CapacityResult(ProductionCapacityRoutingDrainStatus.Deferred);

        public ProductionCapacityRoutingDrainResult TryProgress(
            string stepOperationId,
            string requestFingerprint) => Next;
    }

    private static ProductionPreparedOutputRoutingBatchSnapshot CreateBatch(
        string batchId,
        string lineId,
        long massGrams)
    {
        ProductionPreparedOutputRoutingLineSnapshot line = new(
            batchId,
            "bill:" + batchId,
            "recipe:qa-capacity",
            FacilityId.Value,
            1,
            lineId,
            "output:" + lineId,
            ProductionOutputRole.Main,
            "resource:qa-capacity",
            DestinationId.Value,
            "component:qa-capacity",
            2,
            massGrams,
            2,
            massGrams,
            0,
            0L);
        return new ProductionPreparedOutputRoutingBatchSnapshot(
            batchId,
            "bill:" + batchId,
            "recipe:qa-capacity",
            FacilityId.Value,
            1,
            Digest('c'),
            Digest('d'),
            DestinationId.Value,
            new[] { line },
            Array.Empty<ProductionPreparedOutputRouteRequestSnapshot>(),
            Array.Empty<ProductionPreparedOutputPhysicalRouteReceipt>(),
            false);
    }
}
