#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class
    ProductionGenericBillTerminalDestructiveDrainParticipantDebugScenarios
{
    private static readonly BuildingInstanceId FacilityId =
        (BuildingInstanceId)"building:qa-generic-terminal-participant";
    private static readonly ProductionOutputDestinationId DestinationId =
        ProductionOutputDestinationId.FromFacility(FacilityId);
    private static readonly ProductionFacilityDestructiveDrainOperationId
        OperationId = ProductionFacilityDestructiveDrainOperationId
            .FromFacility(FacilityId);
    private static readonly string ContributionFingerprint = Digest('a');

    [MenuItem(
        "DungeonStory/Debug/Economy/Run Generic Bill Terminal Destructive Drain Participant Contracts")]
    public static void RunFromMenu() => RunAll();

    public static void RunAll()
    {
        VerifyMutationFreeDeterministicMultiBillPrepare();
        VerifyZeroOwnerFacility();
        VerifyLifecycleAndSourceDriftFailClosed();
        VerifyProducerFirstDurablePrepareRetry();
        VerifyChildOnlyOrphanFailsClosed();
        VerifySynchronousChildDriveAndTerminalMapping();
        VerifyAcknowledgementAndRecoveryMatrix();
        VerifyMismatchAndTamperFailClosed();
        Debug.Log(
            "Generic-bill terminal destructive-drain participant contracts passed.");
    }

    private static void VerifyMutationFreeDeterministicMultiBillPrepare()
    {
        Fixture fixture = Fixture.Create("b", "a");
        ProductionFacilityDestructiveDrainParticipantPlan first =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainParticipantPlan second =
            fixture.Participant.Prepare(CreatePrepareContext());

        string[] owners = first.Owners
            .Select(value => value.OwnerStableId)
            .ToArray();
        Require(
            first.ParticipantId == ProductionFacilityDestructiveDrainParticipantIds
                .GenericProductionBills
            && first.ContractVersion == 1
            && string.Equals(
                first.PlanFingerprint,
                second.PlanFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                first.DurableContributionFingerprint,
                second.DurableContributionFingerprint,
                StringComparison.Ordinal)
            && first.Owners.Count == 2
            && owners.SequenceEqual(
                owners.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            && fixture.Producer.PrepareCalls == 0
            && fixture.Input.PrepareCalls == 0
            && fixture.Input.CommitCalls == 0
            && fixture.Producer.ProgressCalls == 0,
            "Prepare mutated a producer or did not produce deterministic owner ordering.");
        Require(
            fixture.Producer.LiveCaptureCalls == 4
            && fixture.Input.CaptureRequestCalls == 4,
            "Two deterministic prepares did not remain capture-only.");
    }

    private static void VerifyZeroOwnerFacility()
    {
        Fixture fixture = Fixture.Create();
        fixture.Lifecycle.HasAuthority = false;
        ProductionFacilityDestructiveDrainParticipantPlan first =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainParticipantPlan second =
            fixture.Participant.Prepare(CreatePrepareContext());
        Require(
            first.Owners.Count == 0
            && second.Owners.Count == 0
            && string.Equals(first.PlanFingerprint, second.PlanFingerprint,
                StringComparison.Ordinal)
            && fixture.Producer.LiveCaptureCalls == 0
            && fixture.Input.CaptureRequestCalls == 0,
            "A zero-owner facility synthesized a bill or touched child custody.");
    }

    private static void VerifyLifecycleAndSourceDriftFailClosed()
    {
        Fixture lifecycleFixture = Fixture.Create("lifecycle");
        ProductionFacilityDestructiveDrainParticipantPlan lifecyclePlan =
            lifecycleFixture.Participant.Prepare(CreatePrepareContext());
        lifecycleFixture.Lifecycle.DurableFingerprint = Digest('b');
        Require(
            !lifecycleFixture.Participant.TryPrepareDurable(
                CreateStepContext(lifecyclePlan.Owners[0],
                    lifecyclePlan.DurableContributionFingerprint),
                out string lifecycleFailure)
            && lifecycleFailure ==
                "production-generic-terminal-durable-contribution-drift"
            && lifecycleFixture.Producer.PrepareCalls == 0,
            "Lifecycle drift reached producer durable prepare.");

        Fixture sourceFixture = Fixture.Create("source");
        ProductionFacilityDestructiveDrainParticipantPlan sourcePlan =
            sourceFixture.Participant.Prepare(CreatePrepareContext());
        sourceFixture.Producer.MutateLiveBill("source", bill =>
            bill.targetStock = checked(bill.targetStock + 1));
        Require(
            !sourceFixture.Participant.TryPrepareDurable(
                CreateStepContext(sourcePlan.Owners[0],
                    sourcePlan.DurableContributionFingerprint),
                out string sourceFailure)
            && sourceFailure ==
                "production-generic-terminal-durable-prepare-plan-drift"
            && sourceFixture.Producer.PrepareCalls == 0,
            "Frozen live-bill source drift reached producer durable prepare.");

        Fixture mismatch = Fixture.Create();
        mismatch.Lifecycle.HasAuthority = true;
        RequireThrows(
            () => mismatch.Participant.Prepare(CreatePrepareContext()),
            "Lifecycle authority without a source owner was accepted.");
    }

    private static void VerifyProducerFirstDurablePrepareRetry()
    {
        Fixture fixture = Fixture.Create("retry");
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext context =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);
        fixture.Events.Clear();
        fixture.Input.DeferredPrepareAttempts = 1;

        Require(
            !fixture.Participant.TryPrepareDurable(context, out string firstFailure)
            && firstFailure == "fixture-child-prepare-deferred"
            && fixture.Producer.HasState(context.Owner.stepOperationId)
            && !fixture.Input.HasState(
                context.Owner.stepOperationId +
                ":input-destination-custody")
            && fixture.Events.SequenceEqual(
                new[] { "producer.prepare", "child.prepare" },
                StringComparer.Ordinal),
            "Producer-ahead/child-missing durable retry boundary was not preserved.");

        fixture.Events.Clear();
        Require(
            fixture.Participant.TryPrepareDurable(context, out string retryFailure)
            && string.IsNullOrEmpty(retryFailure)
            && fixture.Producer.PrepareCalls == 2
            && fixture.Input.PrepareCalls == 2
            && fixture.Events.SequenceEqual(
                new[] { "producer.prepare", "child.prepare" },
                StringComparer.Ordinal),
            "A valid producer-ahead/child-missing state did not retry idempotently.");
    }

    private static void VerifySynchronousChildDriveAndTerminalMapping()
    {
        Fixture fixture = Fixture.Create("commit");
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext context =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);
        Require(fixture.Participant.TryPrepareDurable(context, out _),
            "Commit fixture could not durably prepare.");

        int gameplayEpoch = 0;
        fixture.Input.OnCommitStep = () => Require(
            gameplayEpoch == 0,
            "Gameplay regained an opportunity while child recovery was open.");
        ProductionFacilityDestructiveDrainStepResult committed =
            fixture.Participant.TryCommit(context);
        gameplayEpoch++;

        Require(
            committed.Status == ProductionFacilityDestructiveDrainStepStatus.Applied
            && IsDigest(committed.ReceiptFingerprint)
            && committed.CommitId.StartsWith(
                ProductionGenericBillTerminalDrainCanonical.CommitPrefix,
                StringComparison.Ordinal)
            && fixture.Input.CommitCalls == 4
            && fixture.Producer.ProgressCalls == 3
            && fixture.Input.CapturedPhase(context.Owner.stepOperationId
                    + ":input-destination-custody") ==
                ProductionInputDestinationCustodyDrainPhase
                    .BillAcknowledgedAwaitingCheckpointGc,
            "Child recovery did not finish synchronously before terminal mapping.");

        ProductionFacilityDestructiveDrainStepResult replay =
            fixture.Participant.TryCommit(context);
        Require(
            replay.Status == ProductionFacilityDestructiveDrainStepStatus.Replay
            && replay.CommitId == committed.CommitId
            && replay.ReceiptFingerprint == committed.ReceiptFingerprint,
            "Terminal producer replay did not preserve the exact upper receipt.");
    }

    private static void VerifyChildOnlyOrphanFailsClosed()
    {
        Fixture fixture = Fixture.Create("child-orphan");
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext context =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);
        Require(fixture.Participant.TryPrepareDurable(context, out _),
            "Child-orphan fixture could not prepare both durable records.");
        fixture.Producer.Remove(context.Owner.stepOperationId);

        Require(
            fixture.Input.HasState(
                context.Owner.stepOperationId
                + ":input-destination-custody")
            && fixture.Participant.TryCommit(context).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict
            && fixture.Participant.Recover(context).Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
            "A child-only durable orphan was accepted as a missing-producer retry.");
    }

    private static void VerifyAcknowledgementAndRecoveryMatrix()
    {
        Fixture missing = Fixture.Create("missing");
        ProductionFacilityDestructiveDrainParticipantPlan missingPlan =
            missing.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext missingPlanned =
            CreateStepContext(missingPlan.Owners[0],
                missingPlan.DurableContributionFingerprint);
        RequireRecovery(
            missing.Participant.Recover(missingPlanned),
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
            ProductionFacilityDestructiveDrainStepStatus.Deferred,
            "Missing producer at Planned was not recoverable.");

        Fixture fixture = Fixture.Create("matrix");
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext planned =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);
        Require(fixture.Participant.TryPrepareDurable(planned, out _),
            "Recovery matrix fixture could not prepare.");
        RequireRecovery(
            fixture.Participant.Recover(planned),
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
            ProductionFacilityDestructiveDrainStepStatus.Deferred,
            "Prepared producer did not resume commit.");

        ProductionFacilityDestructiveDrainStepResult committed =
            fixture.Participant.TryCommit(planned);
        RequireRecovery(
            fixture.Participant.Recover(planned),
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
            ProductionFacilityDestructiveDrainStepStatus.Replay,
            "Producer-ahead terminal receipt did not replay into Planned journal.");

        ProductionFacilityDestructiveDrainStepContext awaitingAck =
            CreateStepContext(
                plan.Owners[0],
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck,
                committed.CommitId,
                committed.ReceiptFingerprint);
        RequireRecovery(
            fixture.Participant.Recover(awaitingAck),
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeAcknowledge,
            ProductionFacilityDestructiveDrainStepStatus.Replay,
            "Committed producer did not resume acknowledgement.");

        ProductionFacilityDestructiveDrainStepResult acknowledged =
            fixture.Participant.TryAcknowledge(awaitingAck);
        Require(
            acknowledged.Status == ProductionFacilityDestructiveDrainStepStatus.Applied
            && acknowledged.CommitId == committed.CommitId
            && acknowledged.ReceiptFingerprint == committed.ReceiptFingerprint,
            "Exact journal receipt did not acknowledge the producer.");
        RequireRecovery(
            fixture.Participant.Recover(awaitingAck),
            ProductionFacilityDestructiveDrainRecoveryAction.AlreadyAcknowledged,
            ProductionFacilityDestructiveDrainStepStatus.Replay,
            "Producer-ahead acknowledgement was not recognized.");

        ProductionFacilityDestructiveDrainStepContext ownerAcknowledged =
            CreateStepContext(
                plan.Owners[0],
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged,
                committed.CommitId,
                committed.ReceiptFingerprint);
        RequireRecovery(
            fixture.Participant.Recover(ownerAcknowledged),
            ProductionFacilityDestructiveDrainRecoveryAction.AlreadyAcknowledged,
            ProductionFacilityDestructiveDrainStepStatus.Replay,
            "Exact dual acknowledgement did not recover as terminal.");
        fixture.Producer.Remove(ownerAcknowledged.Owner.stepOperationId);
        RequireRecovery(
            fixture.Participant.Recover(ownerAcknowledged),
            ProductionFacilityDestructiveDrainRecoveryAction.AlreadyAcknowledged,
            ProductionFacilityDestructiveDrainStepStatus.Replay,
            "Checkpoint-collected producer was not accepted after owner acknowledgement.");
    }

    private static void VerifyMismatchAndTamperFailClosed()
    {
        Fixture childTamper = Fixture.Create("child-tamper");
        ProductionFacilityDestructiveDrainParticipantPlan childPlan =
            childTamper.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext childContext =
            CreateStepContext(childPlan.Owners[0],
                childPlan.DurableContributionFingerprint);
        Require(childTamper.Participant.TryPrepareDurable(childContext, out _),
            "Child-tamper fixture could not prepare.");
        childTamper.Input.TamperRequestFingerprint(
            childContext.Owner.stepOperationId
            + ":input-destination-custody",
            Digest('c'));
        Require(
            childTamper.Participant.TryCommit(childContext).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict
            && childTamper.Input.CommitCalls == 0,
            "Tampered child evidence reached a physical commit.");

        Fixture receiptMismatch = Fixture.Create("receipt-mismatch");
        ProductionFacilityDestructiveDrainParticipantPlan mismatchPlan =
            receiptMismatch.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext mismatchPlanned =
            CreateStepContext(mismatchPlan.Owners[0],
                mismatchPlan.DurableContributionFingerprint);
        Require(receiptMismatch.Participant.TryPrepareDurable(
                mismatchPlanned, out _),
            "Receipt mismatch fixture could not prepare.");
        ProductionFacilityDestructiveDrainStepResult terminal =
            receiptMismatch.Participant.TryCommit(mismatchPlanned);
        ProductionFacilityDestructiveDrainStepContext wrongReceipt =
            CreateStepContext(
                mismatchPlan.Owners[0],
                mismatchPlan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck,
                terminal.CommitId,
                Digest('d'));
        Require(
            receiptMismatch.Participant.TryAcknowledge(wrongReceipt).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict
            && receiptMismatch.Participant.Recover(wrongReceipt).Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
            "Mismatched journal receipt did not fail closed.");

        Fixture sourceTamper = Fixture.Create("producer-tamper");
        ProductionFacilityDestructiveDrainParticipantPlan sourcePlan =
            sourceTamper.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext sourceContext =
            CreateStepContext(sourcePlan.Owners[0],
                sourcePlan.DurableContributionFingerprint);
        Require(sourceTamper.Participant.TryPrepareDurable(sourceContext, out _),
            "Producer-tamper fixture could not prepare.");
        sourceTamper.Producer.TamperRequestFingerprint(
            sourceContext.Owner.stepOperationId,
            Digest('e'));
        Require(
            sourceTamper.Participant.TryCommit(sourceContext).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict
            && sourceTamper.Producer.ProgressCalls == 0,
            "Tampered producer evidence reached a bill mutation.");
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
        ProductionFacilityDestructiveDrainParticipantIds.GenericProductionBills,
        new ProductionFacilityDestructiveDrainOwnerSaveData
        {
            ownerStableId = owner.OwnerStableId,
            disposition = owner.Disposition,
            targetDestinationId = owner.TargetDestinationId,
            stepOperationId = ProductionFacilityDestructiveDrainCanonical
                .BuildStepOperationId(
                    OperationId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .GenericProductionBills,
                    owner.OwnerStableId),
            phase = phase,
            requestFingerprint = owner.RequestFingerprint,
            commitId = commitId,
            receiptFingerprint = receiptFingerprint
        },
        contributionFingerprint);

    private static void RequireRecovery(
        ProductionFacilityDestructiveDrainRecoveryResult actual,
        ProductionFacilityDestructiveDrainRecoveryAction action,
        ProductionFacilityDestructiveDrainStepStatus status,
        string message) => Require(
        actual.Action == action && actual.Step.Status == status,
        message);

    private static ProductionBillSaveData CreateBill(string suffix)
    {
        string billId = "production-bill:qa-generic-participant:" + suffix;
        return new ProductionBillSaveData
        {
            billId = billId,
            recipeId = "recipe:qa-generic-participant",
            buildingInstanceId = FacilityId.Value,
            mode = ProductionOrderMode.RepeatCount,
            remainingCycles = 1,
            targetStock = 0,
            materialsConsumed = false,
            cycleSequence = 1,
            wipInputCommitId = string.Empty,
            wipInputQuantity = 0,
            wipInputMassGrams = 0L,
            outputOutcomeResolved = false,
            resolvedOutputs = new List<ProductionResolvedOutputSaveData>(),
            preparedOutput = ProductionPreparedOutputBatchSaveData.Unresolved(),
            processWastewaterComponents =
                new List<ProductionWastewaterComponentSaveData>(),
            processManualWaterTransfers =
                new List<ProductionManualWaterTransferSaveData>(),
            materialDestinationId = ProductionBillRuntime.DestinationPrefix
                + billId,
            outputDestinationId = DestinationId.Value,
            allowedMaterialIds = new List<string>(),
            allowedWorkerIds = new List<string>(),
            workerContributions = new List<CraftContributionSaveData>(),
            outputReservations = new List<ProductionOutputReservationSaveData>(),
            routePolicies = new List<ProductionConsumerRoutePolicy>(),
            selectedSupplies = new List<ProductionSelectedSupplySaveData>()
        };
    }

    private static ProductionFacilityDestructiveDrainPreparedOutputOwner
        CreateOwner(ProductionBillSaveData bill) => new(
        (ProductionBillId)bill.billId,
        FacilityId,
        bill.recipeId,
        bill.cycleSequence,
        bill.outputDestinationId,
        ProductionPreparedOutputPhase.Unresolved,
        string.Empty,
        string.Empty);

    private static string Digest(char value) => new(value, 64);
    private static bool IsDigest(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

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

    private sealed class Fixture
    {
        private Fixture(
            FakeLifecycleQuery lifecycle,
            FakePreparedOwnerQuery owners,
            FakeProducer producer,
            FakeInputDrain input,
            ProductionGenericBillTerminalDestructiveDrainParticipant participant,
            List<string> events)
        {
            Lifecycle = lifecycle;
            Owners = owners;
            Producer = producer;
            Input = input;
            Participant = participant;
            Events = events;
        }

        internal FakeLifecycleQuery Lifecycle { get; }
        internal FakePreparedOwnerQuery Owners { get; }
        internal FakeProducer Producer { get; }
        internal FakeInputDrain Input { get; }
        internal ProductionGenericBillTerminalDestructiveDrainParticipant
            Participant { get; }
        internal List<string> Events { get; }

        internal static Fixture Create(params string[] suffixes)
        {
            List<string> events = new();
            ProductionBillSaveData[] bills = (suffixes ?? Array.Empty<string>())
                .Select(CreateBill)
                .ToArray();
            FakeLifecycleQuery lifecycle = new()
            {
                HasAuthority = bills.Length > 0,
                DurableFingerprint = ContributionFingerprint,
                ActiveRecordCount = bills.Length
            };
            FakePreparedOwnerQuery owners = new(bills
                .Select(CreateOwner)
                .Reverse()
                .ToArray());
            FakeInputDrain input = new(events);
            FakeProducer producer = new(bills, input, events);
            ProductionFacilityHandle facility = new(
                new object(),
                FacilityId,
                new Vector2Int(7, 9),
                false,
                string.Empty,
                false,
                Vector2Int.zero,
                "building-definition:qa-generic-participant",
                "workstation:qa-generic-participant",
                2);
            IProductionAssemblyBridge bridge = BridgeProxy.Create(facility);
            ProductionGenericBillTerminalDestructiveDrainParticipant participant =
                new(
                    lifecycle,
                    owners,
                    producer,
                    producer,
                    input,
                    bridge);
            return new Fixture(
                lifecycle,
                owners,
                producer,
                input,
                participant,
                events);
        }
    }

    private sealed class FakeLifecycleQuery :
        IProductionOutputDestinationLifecycleQuery
    {
        internal bool HasAuthority { get; set; }
        internal int ActiveRecordCount { get; set; }
        internal string DurableFingerprint { get; set; } =
            ContributionFingerprint;

        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId facilityId)
        {
            ProductionOutputDestinationLifecycleContribution contribution = new(
                ProductionFacilityDestructiveDrainParticipantIds
                    .GenericProductionBills,
                HasAuthority,
                1L,
                ActiveRecordCount,
                0L,
                Array.Empty<ProductionOutputLifecycleBlock>(),
                DurableFingerprint,
                DurableFingerprint);
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                ProductionOutputDestinationId.FromFacility(facilityId),
                new[] { contribution },
                Digest('1'),
                Digest('1'));
        }
    }

    private sealed class FakePreparedOwnerQuery :
        IProductionFacilityDestructiveDrainPreparedOutputQuery
    {
        private readonly List<
            ProductionFacilityDestructiveDrainPreparedOutputOwner> owners;

        internal FakePreparedOwnerQuery(IEnumerable<
            ProductionFacilityDestructiveDrainPreparedOutputOwner> owners) =>
            this.owners = (owners ?? Array.Empty<
                    ProductionFacilityDestructiveDrainPreparedOutputOwner>())
                .ToList();

        public IReadOnlyList<
            ProductionFacilityDestructiveDrainPreparedOutputOwner>
            CapturePreparedOutputOwners(BuildingInstanceId facilityId) =>
            owners.ToArray();
    }

    private sealed class FakeProducer :
        IProductionGenericBillTerminalDrainQuery,
        IProductionGenericBillTerminalDrainCommand
    {
        private readonly Dictionary<string, ProductionBillSaveData> live =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string,
            ProductionGenericBillTerminalDrainSaveData> states =
            new(StringComparer.Ordinal);
        private readonly FakeInputDrain input;
        private readonly List<string> events;

        internal FakeProducer(
            IEnumerable<ProductionBillSaveData> bills,
            FakeInputDrain input,
            List<string> events)
        {
            foreach (ProductionBillSaveData bill in bills)
            {
                live[bill.billId] = ProductionGenericBillTerminalDrainCanonical
                    .CloneBill(bill);
            }
            this.input = input;
            this.events = events;
        }

        internal int LiveCaptureCalls { get; private set; }
        internal int PrepareCalls { get; private set; }
        internal int ProgressCalls { get; private set; }

        internal bool HasState(string stepOperationId) =>
            states.ContainsKey(stepOperationId);

        internal void Remove(string stepOperationId) =>
            states.Remove(stepOperationId);

        internal void MutateLiveBill(
            string suffix,
            Action<ProductionBillSaveData> mutation)
        {
            string id = "production-bill:qa-generic-participant:" + suffix;
            mutation(live[id]);
        }

        internal void TamperRequestFingerprint(
            string stepOperationId,
            string fingerprint) =>
            states[stepOperationId].requestFingerprint = fingerprint;

        public bool TryCaptureLiveBill(
            ProductionBillId billId,
            out ProductionBillSaveData sourceBill,
            out string sourceBillFingerprint,
            out string failureReason)
        {
            LiveCaptureCalls++;
            failureReason = string.Empty;
            sourceBillFingerprint = string.Empty;
            sourceBill = null;
            if (!live.TryGetValue(billId.Value, out ProductionBillSaveData value))
            {
                failureReason = "fixture-live-bill-missing";
                return false;
            }
            sourceBill = ProductionGenericBillTerminalDrainCanonical.CloneBill(value);
            sourceBillFingerprint = ProductionGenericBillTerminalDrainCanonical
                .CreateSourceBillFingerprint(sourceBill);
            return true;
        }

        public bool TryCapture(
            string stepOperationId,
            out ProductionGenericBillTerminalDrainSaveData record)
        {
            record = null;
            if (!states.TryGetValue(
                    stepOperationId,
                    out ProductionGenericBillTerminalDrainSaveData state))
                return false;
            record = state.Clone();
            return true;
        }

        public IReadOnlyList<ProductionGenericBillTerminalDrainSaveData>
            CaptureCurrentFormat() => states.Values
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();

        public ProductionGenericBillTerminalDrainResult TryPrepare(
            ProductionGenericBillTerminalDrainRequest request)
        {
            events.Add("producer.prepare");
            PrepareCalls++;
            if (states.TryGetValue(request.StepOperationId, out var existing))
            {
                return ExactRequest(existing, request)
                    ? Result(existing,
                        ProductionGenericBillTerminalDrainStatus.Replay)
                    : Conflict(existing.phase, "fixture-producer-request-conflict");
            }
            ProductionGenericBillTerminalDrainSaveData state = new()
            {
                parentOperationId = request.ParentOperationId,
                stepOperationId = request.StepOperationId,
                ownerStableId = request.OwnerStableId,
                billId = request.SourceBill.billId,
                facilityId = request.SourceBill.buildingInstanceId,
                inputDestinationId = request.SourceBill.materialDestinationId,
                sourceBill = ProductionGenericBillTerminalDrainCanonical
                    .CloneBill(request.SourceBill),
                sourceBillFingerprint =
                    ProductionGenericBillTerminalDrainCanonical
                        .CreateSourceBillFingerprint(request.SourceBill),
                inputDestinationDrainStepOperationId =
                    request.InputDestinationDrainStepOperationId,
                inputDestinationDrainRequestFingerprint =
                    request.InputDestinationDrainRequestFingerprint,
                requestFingerprint = request.RequestFingerprint,
                phase = ProductionGenericBillTerminalDrainPhase
                    .PreparedAwaitingInputDestinationReceipt
            };
            Require(ProductionGenericBillTerminalDrainCanonical.IsValidSave(state),
                "Fixture produced an invalid prepared producer state.");
            states[state.stepOperationId] = state;
            return Result(state, ProductionGenericBillTerminalDrainStatus.Applied);
        }

        public ProductionGenericBillTerminalDrainResult TryProgress(
            string stepOperationId)
        {
            ProgressCalls++;
            if (!states.TryGetValue(stepOperationId, out var state))
            {
                return Conflict(
                    ProductionGenericBillTerminalDrainPhase
                        .PreparedAwaitingInputDestinationReceipt,
                    "fixture-producer-missing");
            }
            switch (state.phase)
            {
                case ProductionGenericBillTerminalDrainPhase
                    .PreparedAwaitingInputDestinationReceipt:
                    if (!input.TryCapture(
                            state.inputDestinationDrainStepOperationId,
                            out ProductionInputDestinationCustodyDrainSaveData child)
                        || child.phase !=
                            ProductionInputDestinationCustodyDrainPhase
                                .EffectCommittedAwaitingBillAck)
                    {
                        return Deferred(state, "fixture-child-not-terminal");
                    }
                    state.inputDestinationDrainCommitId = child.commitId;
                    state.inputDestinationDrainReceiptFingerprint =
                        child.receiptFingerprint;
                    state.releasedInputQuantity = child.releasedQuantity;
                    state.releasedInputMassGrams = child.releasedMassGrams;
                    state.phase = ProductionGenericBillTerminalDrainPhase
                        .InputDestinationReceiptRecordedAwaitingAcknowledgement;
                    break;

                case ProductionGenericBillTerminalDrainPhase
                    .InputDestinationReceiptRecordedAwaitingAcknowledgement:
                    ProductionInputDestinationCustodyDrainResult acknowledged =
                        input.TryAcknowledge(
                            state.inputDestinationDrainStepOperationId,
                            state.inputDestinationDrainReceiptFingerprint);
                    if (acknowledged.Status is
                        ProductionInputDestinationCustodyDrainStatus.Deferred or
                        ProductionInputDestinationCustodyDrainStatus.Conflict)
                    {
                        return Deferred(state, "fixture-child-ack-rejected");
                    }
                    state.phase = ProductionGenericBillTerminalDrainPhase
                        .InputDestinationAcknowledgedAwaitingBillTerminal;
                    break;

                case ProductionGenericBillTerminalDrainPhase
                    .InputDestinationAcknowledgedAwaitingBillTerminal:
                    state.wipTerminalCommitId =
                        ProductionGenericBillTerminalDrainCanonical
                            .RequiresWipTerminalReceipt(state.sourceBill)
                            ? ProductionGenericBillTerminalDrainCanonical
                                .CreateWipTerminalCommitId(
                                    state.billId,
                                    state.sourceBill.cycleSequence)
                            : string.Empty;
                    state.billTerminalEffectFingerprint =
                        ProductionGenericBillTerminalDrainCanonical
                            .CreateBillTerminalEffectFingerprint(
                                state.requestFingerprint,
                                state.inputDestinationDrainReceiptFingerprint,
                                state.wipTerminalCommitId);
                    state.commitId = ProductionGenericBillTerminalDrainCanonical
                        .CreateCommitId(
                            state.stepOperationId,
                            state.requestFingerprint);
                    state.receiptFingerprint =
                        ProductionGenericBillTerminalDrainCanonical
                            .CreateReceiptFingerprint(
                                state.requestFingerprint,
                                state.inputDestinationDrainReceiptFingerprint,
                                state.billTerminalEffectFingerprint,
                                state.commitId);
                    state.phase = ProductionGenericBillTerminalDrainPhase
                        .BillTerminalCommittedAwaitingOwnerAcknowledgement;
                    break;

                default:
                    return Result(
                        state,
                        ProductionGenericBillTerminalDrainStatus.Replay);
            }
            Require(ProductionGenericBillTerminalDrainCanonical.IsValidSave(state),
                "Fixture producer phase transition became invalid.");
            states[stepOperationId] = state;
            return Result(state, ProductionGenericBillTerminalDrainStatus.Applied);
        }

        public ProductionGenericBillTerminalDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            if (!states.TryGetValue(stepOperationId, out var state))
            {
                return Conflict(
                    ProductionGenericBillTerminalDrainPhase
                        .PreparedAwaitingInputDestinationReceipt,
                    "fixture-producer-missing");
            }
            if (state.receiptFingerprint != receiptFingerprint)
                return Conflict(state.phase, "fixture-producer-receipt-conflict");
            if (state.phase == ProductionGenericBillTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc)
            {
                return Result(state,
                    ProductionGenericBillTerminalDrainStatus.Replay);
            }
            if (state.phase != ProductionGenericBillTerminalDrainPhase
                    .BillTerminalCommittedAwaitingOwnerAcknowledgement)
                return Deferred(state, "fixture-producer-not-terminal");
            state.phase = ProductionGenericBillTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
            states[stepOperationId] = state;
            return Result(state, ProductionGenericBillTerminalDrainStatus.Applied);
        }

        public ProductionGenericBillTerminalDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint) =>
            throw new InvalidOperationException("Not used by participant fixture.");

        public ProductionGenericBillTerminalDrainResult TryRecover(
            string stepOperationId) =>
            throw new InvalidOperationException("Not used by participant fixture.");

        public bool TryRestoreCurrentFormat(
            IEnumerable<ProductionGenericBillTerminalDrainSaveData> records,
            out string failureReason) =>
            throw new InvalidOperationException("Not used by participant fixture.");

        private static bool ExactRequest(
            ProductionGenericBillTerminalDrainSaveData state,
            ProductionGenericBillTerminalDrainRequest request) =>
            state.parentOperationId == request.ParentOperationId
            && state.stepOperationId == request.StepOperationId
            && state.ownerStableId == request.OwnerStableId
            && state.requestFingerprint == request.RequestFingerprint
            && state.inputDestinationDrainStepOperationId ==
                request.InputDestinationDrainStepOperationId
            && state.inputDestinationDrainRequestFingerprint ==
                request.InputDestinationDrainRequestFingerprint;

        private static ProductionGenericBillTerminalDrainResult Result(
            ProductionGenericBillTerminalDrainSaveData state,
            ProductionGenericBillTerminalDrainStatus status) => new(
            status,
            state.phase,
            state.commitId,
            state.receiptFingerprint,
            string.Empty);

        private static ProductionGenericBillTerminalDrainResult Deferred(
            ProductionGenericBillTerminalDrainSaveData state,
            string reason) => new(
            ProductionGenericBillTerminalDrainStatus.Deferred,
            state.phase,
            state.commitId,
            state.receiptFingerprint,
            reason);

        private static ProductionGenericBillTerminalDrainResult Conflict(
            ProductionGenericBillTerminalDrainPhase phase,
            string reason) => new(
            ProductionGenericBillTerminalDrainStatus.Conflict,
            phase,
            string.Empty,
            string.Empty,
            reason);
    }

    private sealed class FakeInputDrain :
        IProductionInputDestinationCustodyDrainService
    {
        private readonly Dictionary<string,
            ProductionInputDestinationCustodyDrainSaveData> states =
            new(StringComparer.Ordinal);
        private readonly List<string> events;

        internal FakeInputDrain(List<string> events) => this.events = events;

        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;
        internal int CaptureRequestCalls { get; private set; }
        internal int PrepareCalls { get; private set; }
        internal int CommitCalls { get; private set; }
        internal int DeferredPrepareAttempts { get; set; }
        internal Action OnCommitStep { get; set; }

        internal bool HasState(string stepOperationId) =>
            states.ContainsKey(stepOperationId);

        internal ProductionInputDestinationCustodyDrainPhase CapturedPhase(
            string stepOperationId) => states[stepOperationId].phase;

        internal void TamperRequestFingerprint(
            string stepOperationId,
            string fingerprint) =>
            states[stepOperationId].requestFingerprint = fingerprint;

        public bool TryCaptureSource(
            string sourceDestinationId,
            out ProductionInputDestinationCustodySourceSnapshot snapshot,
            out string failureReason)
        {
            failureReason = string.Empty;
            snapshot = new ProductionInputDestinationCustodySourceSnapshot(
                sourceDestinationId,
                1L,
                Digest('2'),
                Array.Empty<ProductionInputDestinationDrainStackSaveData>(),
                Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
                Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                0,
                0L);
            return true;
        }

        public bool TryBuildRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            ProductionInputDestinationCustodySourceSnapshot snapshot,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (snapshot == null)
            {
                request = null;
                failureReason = "fixture-source-snapshot-missing";
                return false;
            }
            string fingerprint =
                ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                    parentOperationId,
                    stepOperationId,
                    ownerStableId,
                    billId,
                    facilityId,
                    snapshot.SourceDestinationId,
                    ownerPosition.x,
                    ownerPosition.y,
                    sourceClaimFingerprint,
                    snapshot.SourceOwnershipFingerprint,
                    snapshot.SourceStacks,
                    snapshot.SourceOperations,
                    snapshot.SourceActors,
                    snapshot.InputQuantity,
                    snapshot.InputMassGrams);
            request = new ProductionInputDestinationCustodyDrainRequest(
                parentOperationId,
                stepOperationId,
                ownerStableId,
                billId,
                facilityId,
                snapshot.SourceDestinationId,
                ownerPosition.x,
                ownerPosition.y,
                sourceClaimFingerprint,
                snapshot.SourceOwnershipFingerprint,
                snapshot.SourceStacks,
                snapshot.SourceOperations,
                snapshot.SourceActors,
                snapshot.InputQuantity,
                snapshot.InputMassGrams,
                fingerprint);
            return true;
        }

        public bool TryCaptureRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            string sourceDestinationId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            CaptureRequestCalls++;
            failureReason = string.Empty;
            string ownership = Digest('2');
            string fingerprint =
                ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                    parentOperationId,
                    stepOperationId,
                    ownerStableId,
                    billId,
                    facilityId,
                    sourceDestinationId,
                    ownerPosition.x,
                    ownerPosition.y,
                    sourceClaimFingerprint,
                    ownership,
                    Array.Empty<ProductionInputDestinationDrainStackSaveData>(),
                    Array.Empty<
                        ProductionInputDestinationDrainOperationSaveData>(),
                    Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                    0,
                    0L);
            request = new ProductionInputDestinationCustodyDrainRequest(
                parentOperationId,
                stepOperationId,
                ownerStableId,
                billId,
                facilityId,
                sourceDestinationId,
                ownerPosition.x,
                ownerPosition.y,
                sourceClaimFingerprint,
                ownership,
                Array.Empty<ProductionInputDestinationDrainStackSaveData>(),
                Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
                Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                0,
                0L,
                fingerprint);
            return true;
        }

        public ProductionInputDestinationCustodyDrainResult TryPrepare(
            ProductionInputDestinationCustodyDrainRequest request)
        {
            events.Add("child.prepare");
            PrepareCalls++;
            if (DeferredPrepareAttempts > 0)
            {
                DeferredPrepareAttempts--;
                return new ProductionInputDestinationCustodyDrainResult(
                    ProductionInputDestinationCustodyDrainStatus.Deferred,
                    string.Empty,
                    string.Empty,
                    "fixture-child-prepare-deferred");
            }
            if (states.TryGetValue(request.StepOperationId, out var existing))
            {
                return existing.requestFingerprint == request.RequestFingerprint
                    ? Result(existing,
                        ProductionInputDestinationCustodyDrainStatus.Replay)
                    : Conflict("fixture-child-request-conflict");
            }
            ProductionInputDestinationCustodyDrainSaveData state = new()
            {
                parentOperationId = request.ParentOperationId,
                stepOperationId = request.StepOperationId,
                ownerStableId = request.OwnerStableId,
                billId = request.BillId,
                facilityId = request.FacilityId,
                sourceDestinationId = request.SourceDestinationId,
                ownerGridX = request.OwnerGridX,
                ownerGridY = request.OwnerGridY,
                sourceClaimFingerprint = request.SourceClaimFingerprint,
                sourceOwnershipFingerprint = request.SourceOwnershipFingerprint,
                requestFingerprint = request.RequestFingerprint,
                phase = ProductionInputDestinationCustodyDrainPhase.Prepared,
                sourceStacks = request.SourceStacks.Select(value => value.Clone())
                    .ToList(),
                sourceOperations = request.SourceOperations
                    .Select(value => value.Clone()).ToList(),
                sourceActors = request.SourceActors.Select(value => value.Clone())
                    .ToList(),
                completedActorIds = new List<string>(),
                releasedOperationIds = new List<string>(),
                releasedStackIds = new List<string>(),
                inputQuantity = request.InputQuantity,
                inputMassGrams = request.InputMassGrams
            };
            Require(ProductionInputDestinationCustodyDrainContract.IsValidSave(state),
                "Fixture produced an invalid prepared child state.");
            states[state.stepOperationId] = state;
            return Result(state, ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public ProductionInputDestinationCustodyDrainResult TryCommit(
            string stepOperationId,
            string requestFingerprint)
        {
            CommitCalls++;
            OnCommitStep?.Invoke();
            if (!states.TryGetValue(stepOperationId, out var state)
                || state.requestFingerprint != requestFingerprint)
                return Conflict("fixture-child-commit-conflict");
            switch (state.phase)
            {
                case ProductionInputDestinationCustodyDrainPhase.Prepared:
                    state.phase = ProductionInputDestinationCustodyDrainPhase
                        .ReleasingActors;
                    break;
                case ProductionInputDestinationCustodyDrainPhase.ReleasingActors:
                    state.phase = ProductionInputDestinationCustodyDrainPhase
                        .ReleasingOperationAuthority;
                    break;
                case ProductionInputDestinationCustodyDrainPhase
                    .ReleasingOperationAuthority:
                    state.phase = ProductionInputDestinationCustodyDrainPhase
                        .ReleasingDestination;
                    break;
                case ProductionInputDestinationCustodyDrainPhase
                    .ReleasingDestination:
                    string resultFingerprint = Digest('3');
                    state.resultFingerprint = resultFingerprint;
                    state.commitId =
                        ProductionInputDestinationCustodyDrainFingerprint
                            .CreateCommit(
                                state.stepOperationId,
                                state.requestFingerprint);
                    state.receiptFingerprint =
                        ProductionInputDestinationCustodyDrainFingerprint
                            .CreateReceipt(
                                state.requestFingerprint,
                                resultFingerprint,
                                0,
                                0L,
                                Array.Empty<string>(),
                                Array.Empty<string>());
                    state.phase = ProductionInputDestinationCustodyDrainPhase
                        .EffectCommittedAwaitingBillAck;
                    break;
                default:
                    return Result(
                        state,
                        ProductionInputDestinationCustodyDrainStatus.Replay);
            }
            Require(ProductionInputDestinationCustodyDrainContract.IsValidSave(state),
                "Fixture child phase transition became invalid.");
            states[stepOperationId] = state;
            return Result(state, ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public ProductionInputDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            if (!states.TryGetValue(stepOperationId, out var state)
                || state.receiptFingerprint != receiptFingerprint)
                return Conflict("fixture-child-ack-conflict");
            if (state.phase == ProductionInputDestinationCustodyDrainPhase
                    .BillAcknowledgedAwaitingCheckpointGc)
            {
                return Result(state,
                    ProductionInputDestinationCustodyDrainStatus.Replay);
            }
            if (state.phase != ProductionInputDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingBillAck)
                return Conflict("fixture-child-ack-phase-conflict");
            state.phase = ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc;
            states[stepOperationId] = state;
            return Result(state, ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint) =>
            throw new InvalidOperationException("Not used by participant fixture.");

        public bool TryCapture(
            string stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData record)
        {
            record = null;
            if (!states.TryGetValue(
                    stepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData state))
                return false;
            record = state.Clone();
            return true;
        }

        private static ProductionInputDestinationCustodyDrainResult Result(
            ProductionInputDestinationCustodyDrainSaveData state,
            ProductionInputDestinationCustodyDrainStatus status) => new(
            status,
            state.commitId,
            state.receiptFingerprint,
            string.Empty);

        private static ProductionInputDestinationCustodyDrainResult Conflict(
            string reason) => new(
            ProductionInputDestinationCustodyDrainStatus.Conflict,
            string.Empty,
            string.Empty,
            reason);
    }

    public class BridgeProxy : DispatchProxy
    {
        private ProductionFacilityHandle handle;

        internal static IProductionAssemblyBridge Create(
            ProductionFacilityHandle handle)
        {
            IProductionAssemblyBridge result =
                DispatchProxy.Create<IProductionAssemblyBridge, BridgeProxy>();
            ((BridgeProxy)result).handle = handle;
            return result;
        }

        protected override object Invoke(
            MethodInfo targetMethod,
            object[] args)
        {
            if (targetMethod.Name == "get_Facilities")
                return new[] { handle };
            if (targetMethod.Name == nameof(IProductionAssemblyBridge
                    .CaptureFacility))
            {
                if (args.Length == 1
                    && ReferenceEquals(args[0], handle.RuntimeObject))
                    return handle;
                throw new InvalidOperationException("Unknown fixture facility.");
            }
            throw new InvalidOperationException(
                "Unexpected production bridge call: " + targetMethod.Name);
        }
    }
}
#endif
