#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class
    ProductionApparelOrderTerminalDestructiveDrainParticipantDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Apparel Terminal Drain Participant")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_APPAREL_TERMINAL_DRAIN_PARTICIPANT=PASS");
    }

    public static void RunAll()
    {
        VerifyDeterministicPrepareAndExactOwnerRequest();
        VerifyDurablePrepareReplayDriftAndProducerAhead();
        VerifyCommitAcknowledgementAndRecoveryMapping();
        VerifyZeroOwnerPlan();
    }

    private static void VerifyDeterministicPrepareAndExactOwnerRequest()
    {
        Fixture fixture = new("prepare");
        ApparelWorkOrderSaveData first = CreateOrder("z-order", fixture.Facility);
        ApparelWorkOrderSaveData second = CreateOrder("a-order", fixture.Facility);
        fixture.Orders.Publish(first);
        fixture.Orders.Publish(second);
        fixture.Leases.Publish(first.orderId, "lease-z");

        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(fixture.PrepareContext());
        ProductionFacilityDestructiveDrainParticipantPlan replay =
            fixture.Participant.Prepare(fixture.PrepareContext());
        Require(plan.Owners.Count == 2
            && plan.Owners.Select(value => value.OwnerStableId)
                .SequenceEqual(plan.Owners.Select(value => value.OwnerStableId)
                    .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal)
            && string.Equals(plan.PlanFingerprint,
                replay.PlanFingerprint, StringComparison.Ordinal),
            "Apparel participant prepare was not stable and owner-sorted.");

        foreach (ApparelWorkOrderSaveData source in new[] { first, second })
        {
            string owner = ProductionFacilityDestructiveDrainOwnerStableIds
                .ApparelWorkOrder(source.orderId);
            ProductionFacilityDestructiveDrainOwnerPlan actual = plan.Owners
                .Single(value => string.Equals(
                    value.OwnerStableId, owner, StringComparison.Ordinal));
            string step = ProductionFacilityDestructiveDrainCanonical
                .BuildStepOperationId(
                    fixture.OperationId,
                    fixture.Participant.ParticipantId,
                    owner);
            bool hasLease = fixture.Leases.Has(source.orderId);
            string leaseFingerprint = hasLease
                ? fixture.Leases.Fingerprint(source.orderId)
                : ProductionApparelOrderTerminalDrainCanonical
                    .CreateNoLeaseAuthorityFingerprint(source.orderId);
            ProductionApparelOrderTerminalDrainCanonical
                .TryCreatePendingEffectIdentity(source, out var pending, out _);
            string expectedRequest = ProductionApparelOrderTerminalDrainCanonical
                .CreateRequestFingerprint(
                    fixture.OperationId.Value,
                    step,
                    owner,
                    source,
                    hasLease,
                    leaseFingerprint,
                    pending);
            Require(actual.Disposition ==
                    ProductionFacilityDestructiveDrainDisposition.Terminalize
                && string.IsNullOrEmpty(actual.TargetDestinationId)
                && string.Equals(actual.RequestFingerprint,
                    expectedRequest, StringComparison.Ordinal),
                "Owner, step, source or request fingerprint was not exact.");
        }
    }

    private static void VerifyDurablePrepareReplayDriftAndProducerAhead()
    {
        Fixture fixture = new("durable");
        ApparelWorkOrderSaveData order = CreateOrder("subject", fixture.Facility);
        fixture.Orders.Publish(order);
        fixture.Leases.Publish(order.orderId, "lease-subject");
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(fixture.PrepareContext());
        ProductionFacilityDestructiveDrainStepContext step = fixture.Step(
            plan, plan.Owners.Single());
        Require(fixture.Participant.TryPrepareDurable(step, out _)
            && fixture.Participant.TryPrepareDurable(step, out _)
            && fixture.Producer.TryCapture(
                step.Owner.stepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData state)
            && string.Equals(state.ownerStableId,
                step.Owner.ownerStableId, StringComparison.Ordinal)
            && string.Equals(state.requestFingerprint,
                step.Owner.requestFingerprint, StringComparison.Ordinal)
            && string.Equals(state.sourceOrderFingerprint,
                ProductionApparelOrderTerminalDrainCanonical
                    .CreateSourceOrderFingerprint(order),
                StringComparison.Ordinal),
            "Durable prepare did not persist/replay exact producer evidence.");

        Fixture drift = new("drift");
        ApparelWorkOrderSaveData driftOrder = CreateOrder(
            "subject", drift.Facility);
        drift.Orders.Publish(driftOrder);
        ProductionFacilityDestructiveDrainParticipantPlan driftPlan =
            drift.Participant.Prepare(drift.PrepareContext());
        ProductionFacilityDestructiveDrainStepContext driftStep = drift.Step(
            driftPlan, driftPlan.Owners.Single());
        drift.Orders.Mutate(driftOrder.orderId,
            value => value.completedWork += 0.25f);
        Require(!drift.Participant.TryPrepareDurable(
                driftStep, out string driftFailure)
            && driftFailure.Contains("contribution-drift",
                StringComparison.Ordinal),
            "A changed source/contribution was accepted after journal prepare.");

        Fixture ahead = new("producer-ahead");
        ApparelWorkOrderSaveData aheadOrder = CreateOrder(
            "subject", ahead.Facility);
        ahead.Orders.Publish(aheadOrder);
        ProductionFacilityDestructiveDrainParticipantPlan aheadPlan =
            ahead.Participant.Prepare(ahead.PrepareContext());
        ProductionFacilityDestructiveDrainStepContext aheadStep = ahead.Step(
            aheadPlan, aheadPlan.Owners.Single());
        ThrowAfterApplyProducer crash = new(ahead.Producer);
        ProductionApparelOrderTerminalDestructiveDrainParticipant crashing =
            ahead.CreateParticipant(crash);
        Require(!crashing.TryPrepareDurable(aheadStep, out _)
            && ahead.Producer.TryCapture(aheadStep.Owner.stepOperationId, out _)
            && ahead.Participant.TryPrepareDurable(aheadStep, out _),
            "Producer-ahead prepare crash was not recovered by exact replay.");
    }

    private static void VerifyCommitAcknowledgementAndRecoveryMapping()
    {
        Fixture fixture = new("commit");
        ApparelWorkOrderSaveData order = CreateOrder("subject", fixture.Facility);
        fixture.Orders.Publish(order);
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(fixture.PrepareContext());
        ProductionFacilityDestructiveDrainStepContext planned = fixture.Step(
            plan, plan.Owners.Single());
        Require(fixture.Participant.TryPrepareDurable(planned, out _),
            "Commit fixture durable prepare failed.");
        Require(fixture.Participant.Recover(planned).Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
            "Planned recovery did not resume commit.");

        ProductionFacilityDestructiveDrainStepResult committed =
            fixture.Participant.TryCommit(planned);
        Require(committed.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied
            && ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                committed.CommitId)
            && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                committed.ReceiptFingerprint)
            && !fixture.Orders.Has(order.orderId),
            "Participant did not map the exact producer terminal receipt.");
        Require(fixture.Participant.Recover(planned).Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit
            && fixture.Participant.Recover(planned).Step.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Replay,
            "Producer-terminal/journal-planned recovery was not replay mapped.");

        ProductionFacilityDestructiveDrainStepContext awaitingAck = fixture.Step(
            plan,
            plan.Owners.Single(),
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck,
            committed.CommitId,
            committed.ReceiptFingerprint);
        Require(fixture.Participant.Recover(awaitingAck).Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.ResumeAcknowledge,
            "Terminal receipt did not map to ResumeAcknowledge.");
        ProductionFacilityDestructiveDrainStepResult acknowledged =
            fixture.Participant.TryAcknowledge(awaitingAck);
        Require(acknowledged.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied,
            "Exact upper receipt acknowledgement did not apply.");
        ProductionFacilityDestructiveDrainStepContext ownerAcknowledged =
            fixture.Step(
                plan,
                plan.Owners.Single(),
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged,
                committed.CommitId,
                committed.ReceiptFingerprint);
        Require(fixture.Participant.Recover(ownerAcknowledged).Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.AlreadyAcknowledged,
            "Acknowledged producer did not map to AlreadyAcknowledged.");

        ProductionFacilityDestructiveDrainStepContext mismatch = fixture.Step(
            plan,
            plan.Owners.Single(),
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck,
            committed.CommitId,
            new string('0', 64));
        Require(fixture.Participant.TryAcknowledge(mismatch).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict,
            "Journal/producer receipt mismatch was accepted.");
    }

    private static void VerifyZeroOwnerPlan()
    {
        Fixture fixture = new("empty");
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(fixture.PrepareContext());
        Require(plan.Owners.Count == 0
            && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                plan.PlanFingerprint),
            "Zero-owner apparel plan was not deterministic and valid.");
    }

    private static ApparelWorkOrderSaveData CreateOrder(
        string suffix,
        BuildingInstanceId facility) => new()
    {
        orderId = "apparel:repair:" + suffix,
        kind = ApparelWorkOrderKind.Repair,
        state = ApparelWorkOrderState.Ready,
        facilityInstanceId = facility.Value,
        targetItemInstanceId = "item:apparel:" + suffix,
        requiredWork = 12f,
        completedWork = 3f,
        consumedWork = 1f
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        public Fixture(string suffix)
        {
            Facility = (BuildingInstanceId)("building:tailor:" + suffix);
            OperationId = ProductionFacilityDestructiveDrainOperationId
                .FromFacility(Facility);
            Orders = new FakeOrders();
            Leases = new FakeLeases();
            Lifecycle = new FakeLifecycle(Orders);
            Producer = new FakeProducer(Orders);
            Participant = CreateParticipant(Producer);
        }

        public BuildingInstanceId Facility { get; }
        public ProductionFacilityDestructiveDrainOperationId OperationId { get; }
        public FakeOrders Orders { get; }
        public FakeLeases Leases { get; }
        public FakeLifecycle Lifecycle { get; }
        public FakeProducer Producer { get; }
        public ProductionApparelOrderTerminalDestructiveDrainParticipant
            Participant { get; }

        public ProductionApparelOrderTerminalDestructiveDrainParticipant
            CreateParticipant(IProductionApparelOrderTerminalDrainCommand command) =>
            new(Lifecycle, Orders, Leases, Producer, command);

        public ProductionFacilityDestructiveDrainPrepareContext PrepareContext()
        {
            ProductionOutputDestinationLifecycleSnapshot snapshot = Lifecycle
                .Capture(Facility);
            return new ProductionFacilityDestructiveDrainPrepareContext(
                OperationId,
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                Facility,
                ProductionOutputDestinationId.FromFacility(Facility),
                snapshot.DurableSemanticFingerprint);
        }

        public ProductionFacilityDestructiveDrainStepContext Step(
            ProductionFacilityDestructiveDrainParticipantPlan plan,
            ProductionFacilityDestructiveDrainOwnerPlan owner,
            ProductionFacilityDestructiveDrainStepPhase phase =
                ProductionFacilityDestructiveDrainStepPhase.Planned,
            string commitId = "",
            string receiptFingerprint = "")
        {
            ProductionFacilityDestructiveDrainOwnerSaveData saved = new()
            {
                ownerStableId = owner.OwnerStableId,
                disposition = owner.Disposition,
                targetDestinationId = owner.TargetDestinationId,
                stepOperationId = ProductionFacilityDestructiveDrainCanonical
                    .BuildStepOperationId(
                        OperationId,
                        Participant.ParticipantId,
                        owner.OwnerStableId),
                phase = phase,
                requestFingerprint = owner.RequestFingerprint,
                commitId = commitId,
                receiptFingerprint = receiptFingerprint
            };
            return new ProductionFacilityDestructiveDrainStepContext(
                OperationId,
                Facility,
                Participant.ParticipantId,
                saved,
                plan.DurableContributionFingerprint);
        }
    }

    private sealed class FakeOrders : IApparelWorkOrderQuery
    {
        private readonly Dictionary<string, ApparelWorkOrderSaveData> values =
            new(StringComparer.Ordinal);
        public int Version { get; private set; }
        public IReadOnlyList<ApparelWorkOrderSaveData> Orders => values.Values
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .Select(ProductionApparelOrderTerminalDrainCanonical.CloneOrder)
            .ToArray();
        public void Publish(ApparelWorkOrderSaveData value)
        {
            values[value.orderId] =
                ProductionApparelOrderTerminalDrainCanonical.CloneOrder(value);
            Version++;
        }
        public bool Has(string orderId) => values.ContainsKey(orderId);
        public bool TryGet(string orderId, out ApparelWorkOrderSaveData value)
        {
            value = null;
            if (!values.TryGetValue(orderId, out var stored))
                return false;
            value = ProductionApparelOrderTerminalDrainCanonical.CloneOrder(stored);
            return true;
        }
        public void Remove(string orderId)
        {
            if (values.Remove(orderId))
                Version++;
        }
        public void Mutate(string orderId, Action<ApparelWorkOrderSaveData> change)
        {
            ApparelWorkOrderSaveData clone =
                ProductionApparelOrderTerminalDrainCanonical.CloneOrder(
                    values[orderId]);
            change(clone);
            values[orderId] = clone;
            Version++;
        }
    }

    private sealed class FakeLifecycle : IProductionOutputDestinationLifecycleQuery
    {
        private readonly FakeOrders orders;
        public FakeLifecycle(FakeOrders orders) => this.orders = orders;
        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId facilityId)
        {
            ApparelWorkOrderSaveData[] owned = orders.Orders.Where(value =>
                    string.Equals(value.facilityInstanceId,
                        facilityId.Value, StringComparison.Ordinal)
                    && value.state != ApparelWorkOrderState.Completed)
                .ToArray();
            string contributionFingerprint =
                ProductionOutputDestinationDurableSaveProjector.ProjectApparel(
                    facilityId,
                    new DungeonCharacterEnvironmentSaveData
                    {
                        apparelWorkOrders = owned,
                        apparelWorkOrderTerminalStates =
                            Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
                    });
            ProductionOutputDestinationLifecycleContribution contribution = new(
                ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
                owned.Length > 0,
                orders.Version,
                owned.Length,
                0L,
                owned.Length == 0
                    ? Array.Empty<ProductionOutputLifecycleBlock>()
                    : new[]
                    {
                        new ProductionOutputLifecycleBlock(
                            ProductionOutputLifecycleBlockCode.ApparelWorkOrder,
                            owned.Length,
                            0L)
                    },
                contributionFingerprint,
                contributionFingerprint);
            string snapshot = ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint("fixture-apparel-lifecycle@1|"
                    + facilityId.Value + "|" + contributionFingerprint);
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                ProductionOutputDestinationId.FromFacility(facilityId),
                new[] { contribution },
                snapshot,
                snapshot);
        }
    }

    private sealed class FakeLeases : IApparelLeaseAuthorityQuery
    {
        private readonly Dictionary<string, string> fingerprints =
            new(StringComparer.Ordinal);
        public void Publish(string owner, string salt) => fingerprints[owner] =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateNoLeaseAuthorityFingerprint(salt);
        public bool Has(string owner) => fingerprints.ContainsKey(owner);
        public string Fingerprint(string owner) => fingerprints[owner];
        public bool TryCapture(
            string ownerOperationId,
            out ApparelLeaseAuthoritySnapshot snapshot,
            out string failureReason)
        {
            snapshot = null;
            if (!fingerprints.TryGetValue(ownerOperationId, out string fingerprint))
            {
                failureReason = "apparel-lease-authority-missing:"
                    + ownerOperationId;
                return false;
            }
            ConstructorInfo constructor = typeof(ApparelLeaseAuthoritySnapshot)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(ApparelLeaseAuthorityRow[]),
                        typeof(string)
                    },
                    null);
            snapshot = (ApparelLeaseAuthoritySnapshot)constructor.Invoke(
                new object[]
                {
                    ownerOperationId,
                    Array.Empty<ApparelLeaseAuthorityRow>(),
                    fingerprint
                });
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeProducer :
        IProductionApparelOrderTerminalDrainQuery,
        IProductionApparelOrderTerminalDrainCommand
    {
        private readonly FakeOrders orders;
        private readonly Dictionary<string,
            ProductionApparelOrderTerminalDrainSaveData> values =
            new(StringComparer.Ordinal);
        public FakeProducer(FakeOrders orders) => this.orders = orders;

        public bool TryCaptureLiveOrder(
            string orderId,
            out ApparelWorkOrderSaveData sourceOrder,
            out string sourceOrderFingerprint,
            out string failureReason)
        {
            sourceOrderFingerprint = string.Empty;
            if (!orders.TryGet(orderId, out sourceOrder))
            {
                failureReason = "fixture-apparel-source-missing";
                return false;
            }
            sourceOrderFingerprint = ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceOrderFingerprint(sourceOrder);
            failureReason = string.Empty;
            return true;
        }

        public bool TryCapture(
            string stepOperationId,
            out ProductionApparelOrderTerminalDrainSaveData record)
        {
            record = null;
            if (!values.TryGetValue(stepOperationId, out var value))
                return false;
            record = value.Clone();
            return true;
        }

        public IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData>
            CaptureCurrentFormat() => values.Values.Select(value => value.Clone())
            .ToArray();

        public ProductionApparelOrderTerminalDrainResult TryPrepare(
            ProductionApparelOrderTerminalDrainRequest request)
        {
            if (values.TryGetValue(request.StepOperationId, out var existing))
                return Result(existing,
                    string.Equals(existing.requestFingerprint,
                        request.RequestFingerprint, StringComparison.Ordinal)
                        ? ProductionApparelOrderTerminalDrainStatus.Replay
                        : ProductionApparelOrderTerminalDrainStatus.Conflict);
            ProductionApparelOrderTerminalDrainSaveData value = new()
            {
                parentOperationId = request.ParentOperationId,
                stepOperationId = request.StepOperationId,
                ownerStableId = request.OwnerStableId,
                orderId = request.SourceOrder.orderId,
                facilityId = request.SourceOrder.facilityInstanceId,
                orderKind = request.SourceOrder.kind,
                sourceOrder = ProductionApparelOrderTerminalDrainCanonical
                    .CloneOrder(request.SourceOrder),
                sourceOrderFingerprint =
                    ProductionApparelOrderTerminalDrainCanonical
                        .CreateSourceOrderFingerprint(request.SourceOrder),
                hasLeaseAuthority = request.HasLeaseAuthority,
                leaseAuthorityFingerprint = request.LeaseAuthorityFingerprint,
                pendingEffect = request.PendingEffect?.Clone(),
                requestFingerprint = request.RequestFingerprint,
                phase = ProductionApparelOrderTerminalDrainPhase
                    .PreparedAwaitingLeaseAuthorityRelease
            };
            values.Add(value.stepOperationId, value.Clone());
            return Result(value, ProductionApparelOrderTerminalDrainStatus.Applied);
        }

        public ProductionApparelOrderTerminalDrainResult TryProgress(
            string stepOperationId)
        {
            ProductionApparelOrderTerminalDrainSaveData value =
                values[stepOperationId].Clone();
            switch (value.phase)
            {
                case ProductionApparelOrderTerminalDrainPhase
                    .PreparedAwaitingLeaseAuthorityRelease:
                    value.leaseReleaseCommitId =
                        ProductionApparelOrderTerminalDrainCanonical
                            .CreateLeaseReleaseCommitId(
                                value.stepOperationId, value.requestFingerprint);
                    value.leaseReleaseReceiptFingerprint =
                        ProductionApparelOrderTerminalDrainCanonical
                            .CreateLeaseReleaseReceiptFingerprint(
                                value.requestFingerprint,
                                value.leaseAuthorityFingerprint,
                                value.leaseReleaseCommitId);
                    value.phase = ProductionApparelOrderTerminalDrainPhase
                        .LeaseAuthorityReleasedAwaitingTerminalEffect;
                    break;
                case ProductionApparelOrderTerminalDrainPhase
                    .LeaseAuthorityReleasedAwaitingTerminalEffect:
                    value.terminalEffectReceipt =
                        ProductionApparelOrderTerminalDrainCanonical
                            .CreateTerminalEffectReceipt(
                                value.stepOperationId,
                                value.sourceOrder,
                                value.sourceOrderFingerprint,
                                value.pendingEffect);
                    value.phase = ProductionApparelOrderTerminalDrainPhase
                        .TerminalEffectCommittedAwaitingSourceOrderTerminal;
                    break;
                case ProductionApparelOrderTerminalDrainPhase
                    .TerminalEffectCommittedAwaitingSourceOrderTerminal:
                    value.sourceTerminalReceipt =
                        ProductionApparelOrderTerminalDrainCanonical
                            .CreateSourceTerminalReceipt(
                                value.stepOperationId,
                                value.sourceOrder,
                                value.sourceOrderFingerprint,
                                value.terminalEffectReceipt.receiptFingerprint);
                    value.commitId = ProductionApparelOrderTerminalDrainCanonical
                        .CreateCommitId(
                            value.stepOperationId, value.requestFingerprint);
                    value.receiptFingerprint =
                        ProductionApparelOrderTerminalDrainCanonical
                            .CreateReceiptFingerprint(
                                value.requestFingerprint,
                                value.leaseReleaseReceiptFingerprint,
                                value.terminalEffectReceipt.receiptFingerprint,
                                value.sourceTerminalReceipt.receiptFingerprint,
                                value.commitId);
                    value.phase = ProductionApparelOrderTerminalDrainPhase
                        .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement;
                    orders.Remove(value.orderId);
                    break;
            }
            values[stepOperationId] = value.Clone();
            return Result(value, ProductionApparelOrderTerminalDrainStatus.Applied);
        }

        public ProductionApparelOrderTerminalDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            ProductionApparelOrderTerminalDrainSaveData value =
                values[stepOperationId].Clone();
            if (!string.Equals(value.receiptFingerprint,
                    receiptFingerprint, StringComparison.Ordinal))
                return Result(value,
                    ProductionApparelOrderTerminalDrainStatus.Conflict);
            value.phase = ProductionApparelOrderTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
            values[stepOperationId] = value.Clone();
            return Result(value, ProductionApparelOrderTerminalDrainStatus.Applied);
        }

        public ProductionApparelOrderTerminalDrainResult TryGarbageCollect(
            string stepOperationId, string receiptFingerprint) =>
            throw new NotSupportedException();
        public ProductionApparelOrderTerminalDrainResult TryRecover(
            string stepOperationId) => TryProgress(stepOperationId);
        public bool TryRestoreCurrentFormat(
            IEnumerable<ProductionApparelOrderTerminalDrainSaveData> records,
            out string failureReason)
        {
            failureReason = "fixture-not-supported";
            return false;
        }

        private static ProductionApparelOrderTerminalDrainResult Result(
            ProductionApparelOrderTerminalDrainSaveData value,
            ProductionApparelOrderTerminalDrainStatus status) => new(
                status,
                value.phase,
                value.commitId,
                value.receiptFingerprint,
                status == ProductionApparelOrderTerminalDrainStatus.Conflict
                    ? "fixture-producer-conflict"
                    : string.Empty);
    }

    private sealed class ThrowAfterApplyProducer :
        IProductionApparelOrderTerminalDrainCommand
    {
        private readonly IProductionApparelOrderTerminalDrainCommand inner;
        private bool first = true;
        public ThrowAfterApplyProducer(
            IProductionApparelOrderTerminalDrainCommand inner) =>
            this.inner = inner;
        public ProductionApparelOrderTerminalDrainResult TryPrepare(
            ProductionApparelOrderTerminalDrainRequest request)
        {
            ProductionApparelOrderTerminalDrainResult result = inner.TryPrepare(request);
            if (first)
            {
                first = false;
                throw new InvalidOperationException("fixture-crash-after-prepare");
            }
            return result;
        }
        public ProductionApparelOrderTerminalDrainResult TryProgress(
            string stepOperationId) => inner.TryProgress(stepOperationId);
        public ProductionApparelOrderTerminalDrainResult TryAcknowledge(
            string stepOperationId, string receiptFingerprint) =>
            inner.TryAcknowledge(stepOperationId, receiptFingerprint);
        public ProductionApparelOrderTerminalDrainResult TryGarbageCollect(
            string stepOperationId, string receiptFingerprint) =>
            inner.TryGarbageCollect(stepOperationId, receiptFingerprint);
        public ProductionApparelOrderTerminalDrainResult TryRecover(
            string stepOperationId) => inner.TryRecover(stepOperationId);
        public bool TryRestoreCurrentFormat(
            IEnumerable<ProductionApparelOrderTerminalDrainSaveData> records,
            out string failureReason) =>
            inner.TryRestoreCurrentFormat(records, out failureReason);
    }
}
#endif
