#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityDestructiveDrainCheckpointGcDebugScenarios
{
    private static readonly string[] ExecutionOrder =
    {
        ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
        CombatEquipmentTerminalDrainCanonical.ParticipantId,
        ProductionFacilityDestructiveDrainParticipantIds.GenericProductionBills,
        ProductionFacilityDestructiveDrainParticipantIds.CapacityRoutingOutbox,
        ProductionFacilityDestructiveDrainParticipantIds
            .PhysicalCustodyCarryRecovery,
        ProductionFacilityDestructiveDrainParticipantIds
            .StockSensorEmbeddedSalvage
    };

    [MenuItem(
        "DungeonStory/Debug/Economy/Run Destructive Drain Checkpoint GC Contracts")]
    public static void RunAll()
    {
        VerifyJournalIsPublishedLastAndReplayIsIdempotent();
        VerifyEveryPublishBoundaryRollsBack();
        VerifyPrepareFailureDoesNotPublish();
        VerifyRetentionDriftDefersWithoutMutation();
        VerifyRollbackFailureIsTypedCorruption();
        VerifyWorkOrderRetentionSkipsOnlyOwnedOperation();
        VerifyMissingParticipantFailsBeforeMutation();
        Debug.Log("DESTRUCTIVE_DRAIN_CHECKPOINT_GC_ATOMIC_SUITE_PASS_V3");
    }

    private static void VerifyJournalIsPublishedLastAndReplayIsIdempotent()
    {
        List<string> calls = new();
        Fixture fixture = new(calls);
        string digest = Digest('a');
        ProductionFacilityDestructiveDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted("slot-a", digest);
        Require(result.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && result.CollectedOperationCount == 1,
            "destructive checkpoint normal transaction did not apply");
        string[] expected = ExecutionOrder.Reverse()
            .Select(value => "publish:" + value)
            .Append("publish:journal")
            .ToArray();
        Require(calls.Where(value => value.StartsWith(
                    "publish:", StringComparison.Ordinal))
                .SequenceEqual(expected, StringComparer.Ordinal),
            "destructive checkpoint publish order is not reverse-DAG journal-last");
        Require(fixture.Participants.All(value => value.RowRemoved)
                && fixture.Journal.RowRemoved,
            "destructive checkpoint left a terminal row after success");

        int callCount = calls.Count;
        ProductionFacilityDestructiveDrainCheckpointGcResult replay =
            fixture.Coordinator.OnDurableSaveCommitted("slot-a", digest);
        Require(replay.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus
                    .AlreadyApplied
            && calls.Count == callCount,
            "same digest destructive checkpoint replay mutated authority");
    }

    private static void VerifyEveryPublishBoundaryRollsBack()
    {
        for (int failureIndex = 0;
             failureIndex <= ExecutionOrder.Length;
             failureIndex++)
        {
            List<string> calls = new();
            Fixture fixture = new(calls);
            if (failureIndex < ExecutionOrder.Length)
            {
                fixture.Participants[ExecutionOrder.Length - 1 - failureIndex]
                    .FailPublish = true;
            }
            else
            {
                fixture.Journal.FailPublish = true;
            }

            ProductionFacilityDestructiveDrainCheckpointGcResult result =
                fixture.Coordinator.OnDurableSaveCommitted(
                    "fault-" + failureIndex,
                    Digest((char)('b' + failureIndex)));
            Require(result.Status !=
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
                && fixture.Participants.All(value => !value.RowRemoved)
                && !fixture.Journal.RowRemoved
                && fixture.Journal.LastConfirmedCheckpointSequence == 0L,
                "destructive checkpoint fault left partial authority at boundary "
                + failureIndex);
        }
    }

    private static void VerifyWorkOrderRetentionSkipsOnlyOwnedOperation()
    {
        List<string> calls = new();
        Fixture fixture = new(calls, workOrderOwner: true);
        ProductionFacilityDestructiveDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted("retained", Digest('1'));
        Require(result.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && result.CollectedOperationCount == 0
            && !fixture.Journal.RowRemoved
            && fixture.Participants.All(value => !value.RowRemoved)
            && fixture.Journal.LastConfirmedCheckpointSequence == 1L,
            "live WorkOrder did not retain the exact terminal journal operation");
    }

    private static void VerifyPrepareFailureDoesNotPublish()
    {
        List<string> calls = new();
        Fixture fixture = new(calls);
        fixture.Participants[2].FailPrepare = true;
        ProductionFacilityDestructiveDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted(
                "prepare-fault",
                Digest('9'));
        Require(result.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption
            && fixture.Participants.All(value => !value.RowRemoved)
            && !fixture.Journal.RowRemoved
            && calls.All(value => !value.StartsWith(
                "publish:", StringComparison.Ordinal)),
            "destructive checkpoint prepare failure published authority");
    }

    private static void VerifyRetentionDriftDefersWithoutMutation()
    {
        List<string> calls = new();
        Fixture fixture = new(calls, retentionDrift: true);
        ProductionFacilityDestructiveDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted(
                "retention-drift",
                Digest('8'));
        Require(result.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred
            && result.Reason ==
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .WorkOrderOwnerStillLive
            && fixture.Participants.All(value => !value.RowRemoved)
            && !fixture.Journal.RowRemoved,
            "WorkOrder absence proof drift mutated checkpoint authority");
    }

    private static void VerifyRollbackFailureIsTypedCorruption()
    {
        List<string> calls = new();
        Fixture fixture = new(calls);
        fixture.Participants[ExecutionOrder.Length - 1].ThrowRollback = true;
        fixture.Participants[ExecutionOrder.Length - 2].FailPublish = true;
        ProductionFacilityDestructiveDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted(
                "rollback-fault",
                Digest('7'));
        string failedParticipantId = fixture.Participants[
            ExecutionOrder.Length - 1].CheckpointGcParticipantId;
        Require(result.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption
            && result.Reason ==
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantRollbackFailed
            && fixture.Participants[ExecutionOrder.Length - 1].RowRemoved
            && !calls.Contains(
                "complete:" + failedParticipantId,
                StringComparer.Ordinal),
            "destructive checkpoint rollback failure was not typed corruption");
    }

    private static void VerifyMissingParticipantFailsBeforeMutation()
    {
        List<string> calls = new();
        Fixture fixture = new(calls, omitLastParticipant: true);
        ProductionFacilityDestructiveDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted("missing", Digest('f'));
        Require(result.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption
            && fixture.Participants.All(value => !value.RowRemoved)
            && !fixture.Journal.RowRemoved,
            "missing destructive checkpoint participant did not fail before mutation");
    }

    private sealed class Fixture
    {
        internal Fixture(
            List<string> calls,
            bool workOrderOwner = false,
            bool omitLastParticipant = false,
            bool retentionDrift = false)
        {
            FakeExecutionParticipant[] execution = ExecutionOrder
                .Select(value => new FakeExecutionParticipant(value))
                .ToArray();
            FakeRegistry registry = new(execution);
            Participants = execution
                .Select(value => new FakeGcParticipant(value.ParticipantId, calls))
                .ToList();
            IEnumerable<IProductionFacilityDestructiveDrainCheckpointGcParticipant>
                registered = omitLastParticipant
                    ? Participants.Take(Participants.Count - 1)
                    : Participants;
            Journal = new FakeJournal(calls);
            Coordinator =
                new ProductionFacilityDestructiveDrainCheckpointGcCoordinator(
                    registry,
                    registered,
                    Journal,
                    Journal,
                    new FakeRetention(workOrderOwner, retentionDrift));
        }

        internal ProductionFacilityDestructiveDrainCheckpointGcCoordinator
            Coordinator { get; }
        internal List<FakeGcParticipant> Participants { get; }
        internal FakeJournal Journal { get; }
    }

    private sealed class FakeExecutionParticipant :
        IProductionFacilityDestructiveDrainParticipant
    {
        internal FakeExecutionParticipant(string participantId)
        {
            ParticipantId = participantId;
        }

        public string ParticipantId { get; }
        public int ContractVersion => 1;
        public IReadOnlyList<string> DependsOnParticipantIds =>
            Array.Empty<string>();
        public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
            ProductionFacilityDestructiveDrainPrepareContext context) =>
            throw new NotSupportedException();
        public ProductionFacilityDestructiveDrainStepResult TryCommit(
            ProductionFacilityDestructiveDrainStepContext context) =>
            throw new NotSupportedException();
        public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
            ProductionFacilityDestructiveDrainStepContext context) =>
            throw new NotSupportedException();
        public ProductionFacilityDestructiveDrainRecoveryResult Recover(
            ProductionFacilityDestructiveDrainStepContext context) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRegistry :
        IProductionFacilityDestructiveDrainParticipantRegistry
    {
        private readonly IReadOnlyList<IProductionFacilityDestructiveDrainParticipant>
            order;

        internal FakeRegistry(
            IEnumerable<IProductionFacilityDestructiveDrainParticipant> order)
        {
            this.order = order.ToArray();
        }

        public string RegistryFingerprint => Digest('0');
        public IReadOnlyList<IProductionFacilityDestructiveDrainParticipant>
            ExecutionOrder => order;
        public bool TryGet(
            string participantId,
            out IProductionFacilityDestructiveDrainParticipant participant)
        {
            participant = order.SingleOrDefault(value => string.Equals(
                value.ParticipantId,
                participantId,
                StringComparison.Ordinal));
            return participant != null;
        }
    }

    private sealed class FakeGcParticipant :
        IProductionFacilityDestructiveDrainCheckpointGcParticipant
    {
        private readonly List<string> calls;
        private Candidate active;

        internal FakeGcParticipant(string participantId, List<string> calls)
        {
            CheckpointGcParticipantId = participantId;
            this.calls = calls;
        }

        public string CheckpointGcParticipantId { get; }
        internal bool FailPrepare { get; set; }
        internal bool FailPublish { get; set; }
        internal bool ThrowRollback { get; set; }
        internal bool RowRemoved { get; private set; }

        public ProductionFacilityDestructiveDrainCheckpointGcResult
            PrepareCheckpointGarbageCollection(
                ProductionFacilityDestructiveDrainCheckpointGcContext context,
                IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
                    entries,
                out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                    candidate)
        {
            active = new Candidate(
                CheckpointGcParticipantId,
                context,
                entries.Select(value => value.operationId).ToArray());
            candidate = active;
            calls.Add("prepare:" + CheckpointGcParticipantId);
            if (FailPrepare)
            {
                return new ProductionFacilityDestructiveDrainCheckpointGcResult(
                    ProductionFacilityDestructiveDrainCheckpointGcStatus
                        .Corruption,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantPrepareFailed,
                    context.CheckpointSequence,
                    "injected participant prepare failure");
            }
            return Applied(context, entries.Count);
        }

        public ProductionFacilityDestructiveDrainCheckpointGcResult
            PublishCheckpointGarbageCollection(
                IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
        {
            Require(ReferenceEquals(active, candidate),
                "fake participant received a foreign candidate");
            calls.Add("publish:" + CheckpointGcParticipantId);
            RowRemoved = active.OperationIds.Count > 0;
            if (FailPublish)
            {
                return new ProductionFacilityDestructiveDrainCheckpointGcResult(
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .ParticipantPublishFailed,
                    active.CheckpointSequence,
                    "injected participant publish failure");
            }
            return Applied(active.Context, active.OperationIds.Count);
        }

        public void RollbackCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
        {
            calls.Add("rollback:" + CheckpointGcParticipantId);
            if (ThrowRollback)
                throw new InvalidOperationException(
                    "injected participant rollback failure");
            RowRemoved = false;
        }

        public void CompleteCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
        {
            calls.Add("complete:" + CheckpointGcParticipantId);
            active = null;
        }
    }

    private sealed class Candidate :
        IProductionFacilityDestructiveDrainCheckpointGcCandidate
    {
        internal Candidate(
            string participantId,
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<string> operationIds)
        {
            ParticipantId = participantId;
            Context = context;
            OperationIds = operationIds;
        }

        public string ParticipantId { get; }
        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> OperationIds { get; }
        internal ProductionFacilityDestructiveDrainCheckpointGcContext Context
            { get; }
    }

    private sealed class FakeJournal :
        IProductionFacilityDestructiveDrainJournalQuery,
        IProductionFacilityDestructiveDrainCheckpointGcJournal
    {
        private readonly List<string> calls;
        private readonly ProductionFacilityDestructiveDrainEntrySaveData row;
        private JournalCandidate active;

        internal FakeJournal(List<string> calls)
        {
            this.calls = calls;
            BuildingInstanceId facility = (BuildingInstanceId)
                "building:qa-checkpoint-gc";
            row = new ProductionFacilityDestructiveDrainEntrySaveData
            {
                operationId = ProductionFacilityDestructiveDrainOperationId
                    .FromFacility(facility).Value,
                cause = ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId = facility.Value,
                phase = ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc,
                revision = 1L
            };
        }

        public int Version => 1;
        public long LastConfirmedCheckpointSequence { get; private set; }
        public string LastConfirmedSerializedByteDigest { get; private set; } =
            string.Empty;
        internal bool FailPublish { get; set; }
        internal bool RowRemoved { get; private set; }

        public IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
            CaptureOpen() => RowRemoved
                ? Array.Empty<ProductionFacilityDestructiveDrainEntrySaveData>()
                : new[] { row.Clone() };

        public bool TryGet(
            ProductionFacilityDestructiveDrainOperationId operationId,
            out ProductionFacilityDestructiveDrainEntrySaveData entry)
        {
            entry = null;
            if (RowRemoved || !string.Equals(
                    operationId.Value,
                    row.operationId,
                    StringComparison.Ordinal))
                return false;
            entry = row.Clone();
            return true;
        }

        public ProductionFacilityDestructiveDrainCheckpointGcResult
            PrepareCheckpointGarbageCollection(
                ProductionFacilityDestructiveDrainCheckpointGcContext context,
                IReadOnlyList<string> operationIds,
                out IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                    candidate)
        {
            active = new JournalCandidate(context, operationIds.ToArray());
            candidate = active;
            calls.Add("prepare:journal");
            return Applied(context, operationIds.Count);
        }

        public ProductionFacilityDestructiveDrainCheckpointGcResult
            PublishCheckpointGarbageCollection(
                IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                    candidate)
        {
            calls.Add("publish:journal");
            RowRemoved = active.OperationIds.Count > 0;
            LastConfirmedCheckpointSequence = active.CheckpointSequence;
            LastConfirmedSerializedByteDigest = active.SerializedByteDigest;
            if (FailPublish)
            {
                return new ProductionFacilityDestructiveDrainCheckpointGcResult(
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .JournalPublishFailed,
                    active.CheckpointSequence,
                    "injected journal publish failure");
            }
            return Applied(active.Context, active.OperationIds.Count);
        }

        public void RollbackCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                candidate)
        {
            calls.Add("rollback:journal");
            RowRemoved = false;
            LastConfirmedCheckpointSequence = 0L;
            LastConfirmedSerializedByteDigest = string.Empty;
        }

        public void CompleteCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                candidate)
        {
            calls.Add("complete:journal");
            active = null;
        }
    }

    private sealed class JournalCandidate :
        IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
    {
        internal JournalCandidate(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<string> operationIds)
        {
            Context = context;
            OperationIds = operationIds;
        }

        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> OperationIds { get; }
        internal ProductionFacilityDestructiveDrainCheckpointGcContext Context
            { get; }
    }

    private sealed class FakeRetention :
        IWorkOrderDestructiveDrainRetentionQuery
    {
        private readonly bool hasOwner;
        private readonly bool drift;
        private int captures;

        internal FakeRetention(bool hasOwner, bool drift)
        {
            this.hasOwner = hasOwner;
            this.drift = drift;
        }

        public bool TryCaptureRetention(
            ProductionFacilityDestructiveDrainOperationId operationId,
            out WorkOrderDestructiveDrainRetentionSnapshot snapshot,
            out string failureReason)
        {
            failureReason = string.Empty;
            string[] owners = hasOwner
                ? new[] { "work:qa-retained" }
                : Array.Empty<string>();
            captures++;
            int version = drift && captures > 1 ? 2 : 1;
            snapshot = new WorkOrderDestructiveDrainRetentionSnapshot(
                version,
                operationId.Value,
                Array.AsReadOnly(owners),
                ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                    "qa-retention:" + operationId.Value + ":" + hasOwner
                    + ":" + version));
            return true;
        }
    }

    private static ProductionFacilityDestructiveDrainCheckpointGcResult Applied(
        ProductionFacilityDestructiveDrainCheckpointGcContext context,
        int count) => new(
        ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
        ProductionFacilityDestructiveDrainCheckpointGcReason.None,
        context.CheckpointSequence,
        "fixture",
        count);

    private static string Digest(char value) => new(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
