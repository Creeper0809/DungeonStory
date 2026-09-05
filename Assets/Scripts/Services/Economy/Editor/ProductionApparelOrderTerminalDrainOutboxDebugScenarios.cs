#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ProductionApparelOrderTerminalDrainOutboxDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Apparel Order Terminal Drain Outbox")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_APPAREL_ORDER_TERMINAL_DRAIN_OUTBOX=PASS");
    }

    public static void RunAll()
    {
        VerifyFrozenRepairPrepareReplayAndDrift();
        VerifyRejectedDismantleIdentityPreserved();
        VerifyMonotonicTerminalReceiptsAndChildFirstGc();
        VerifyLeaseAheadCrashRecovery();
        VerifyEffectAndSourceAheadCrashRecovery();
        VerifyRestoreJoinsOrphansAndTamperRejection();
    }

    private static void VerifyLeaseAheadCrashRecovery()
    {
        Fixture fixture = new();
        ApparelWorkOrderSaveData order = CreateRepairOrder("lease-ahead");
        fixture.PublishOrder(order, withLease: true);
        Case subject = fixture.CreateCase(order, "lease-ahead");
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionApparelOrderTerminalDrainStatus.Applied,
            "Lease-ahead fixture did not prepare producer-first.");
        ProductionApparelOrderTerminalDrainSaveData prepared =
            fixture.Outbox.CaptureCurrentFormat().Single();
        Require(fixture.Leases.TryReleaseExact(
                    order.orderId,
                    subject.Request.LeaseAuthorityFingerprint,
                    ItemReservationReleaseReason.OwnerRemoved).Status ==
                ApparelLeaseAuthorityReleaseStatus.Applied,
            "Lease-ahead fixture did not inject the child effect.");

        Fixture restore = new();
        restore.Source.Publish(order);
        Require(restore.Outbox.TryRestoreCurrentFormat(
                new[] { prepared }, out _),
            "Prepared producer with an exact lease-ahead prefix did not restore.");
        Require(fixture.Outbox.TryRecover(subject.StepOperationId).Phase ==
                ProductionApparelOrderTerminalDrainPhase
                    .LeaseAuthorityReleasedAwaitingTerminalEffect,
            "Lease-ahead crash prefix did not replay from the durable producer.");

    }

    private static void VerifyFrozenRepairPrepareReplayAndDrift()
    {
        Fixture fixture = new();
        ApparelWorkOrderSaveData order = CreateRepairOrder("prepare");
        fixture.PublishOrder(order, withLease: true);
        Case subject = fixture.CreateCase(order, "prepare");

        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionApparelOrderTerminalDrainStatus.Applied,
            "Apparel producer prepare did not apply.");
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionApparelOrderTerminalDrainStatus.Replay,
            "Equivalent apparel producer prepare did not replay.");

        order.repairInputMassGrams++;
        order.repairSourceStackIds[0] = "stack:mutated";
        Require(fixture.Outbox.TryCapture(subject.StepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData frozen)
            && frozen.orderKind == ApparelWorkOrderKind.Repair
            && frozen.sourceOrder.repairCommitPhase ==
                ApparelRepairCommitPhase.MaterialCommitted
            && frozen.sourceOrder.repairInputMassGrams == 2_400L
            && frozen.sourceOrder.repairSourceStackIds.SequenceEqual(
                new[] { "stack:repair-material-a", "stack:repair-material-b" },
                StringComparer.Ordinal),
            "The full apparel source was not deep-frozen at prepare.");

        ProductionApparelOrderTerminalDrainRequest conflict = CreateRequest(
            frozen.sourceOrder,
            subject.ParentOperationId + ":changed",
            subject.StepOperationId,
            frozen.hasLeaseAuthority,
            frozen.leaseAuthorityFingerprint,
            frozen.pendingEffect);
        Require(fixture.Outbox.TryPrepare(conflict).Status ==
                ProductionApparelOrderTerminalDrainStatus.Conflict,
            "A changed request under the same producer step was accepted.");

        Fixture gramDrift = new();
        ApparelWorkOrderSaveData gramOrder = CreateRepairOrder("gram-drift");
        gramDrift.PublishOrder(gramOrder, withLease: true);
        Case gramCase = gramDrift.CreateCase(gramOrder, "gram-drift");
        gramDrift.Source.ReplaceLive(orderId: gramOrder.orderId,
            mutate: value => value.repairInputMassGrams++);
        Require(gramDrift.Outbox.TryPrepare(gramCase.Request).Status ==
                ProductionApparelOrderTerminalDrainStatus.Conflict,
            "One gram of live pending-repair drift was accepted.");

        Fixture quantityDrift = new();
        ApparelWorkOrderSaveData quantityOrder = CreateRepairOrder("quantity-drift");
        quantityDrift.PublishOrder(quantityOrder, withLease: true);
        Case quantityCase = quantityDrift.CreateCase(
            quantityOrder, "quantity-drift");
        quantityDrift.Source.ReplaceLive(orderId: quantityOrder.orderId,
            mutate: value => value.repairInputQuantity++);
        Require(quantityDrift.Outbox.TryPrepare(quantityCase.Request).Status ==
                ProductionApparelOrderTerminalDrainStatus.Conflict,
            "One unit of live pending-repair drift was accepted.");
    }

    private static void VerifyRejectedDismantleIdentityPreserved()
    {
        Fixture fixture = new();
        ApparelWorkOrderSaveData order = CreateRejectedOrder("rejected");
        fixture.PublishOrder(order, withLease: false);
        Case subject = fixture.CreateCase(order, "rejected");
        Require(subject.PendingEffect != null
            && subject.PendingEffect.kind ==
                ProductionApparelOrderPendingEffectKind
                    .RejectedOutputDismantle
            && subject.PendingEffect.sourceAlreadyConsumed
            && subject.PendingEffect.quantity == 5
            && subject.PendingEffect.completedQuantity == 0
            && subject.PendingEffect.sourceStackIds.SequenceEqual(
                new[] { "stack:rejected-output" }, StringComparer.Ordinal),
            "Rejected dismantle progress was not frozen exactly.");
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionApparelOrderTerminalDrainStatus.Applied
            && fixture.Outbox.TryCapture(subject.StepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData saved)
            && saved.sourceOrder.dismantlingRejectedOutput
            && saved.sourceOrder.rejectedOutputConsumed
            && saved.sourceOrder.rejectedMaterialAmount == 5
            && saved.sourceOrder.rejectedMaterialSpawned == 0,
            "Rejected dismantle source phase was not preserved by the producer.");
    }

    private static void VerifyMonotonicTerminalReceiptsAndChildFirstGc()
    {
        Fixture fixture = new();
        ApparelWorkOrderSaveData order = CreateRepairOrder("terminal");
        fixture.PublishOrder(order, withLease: true);
        Case subject = fixture.CreateCase(order, "terminal");
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionApparelOrderTerminalDrainStatus.Applied,
            "Terminal fixture did not prepare.");

        Require(fixture.Outbox.TryProgress(subject.StepOperationId).Phase ==
                ProductionApparelOrderTerminalDrainPhase
                    .LeaseAuthorityReleasedAwaitingTerminalEffect
            && !fixture.Leases.Has(order.orderId),
            "Exact lease authority was not released first.");
        Require(fixture.Outbox.TryProgress(subject.StepOperationId).Phase ==
                ProductionApparelOrderTerminalDrainPhase
                    .TerminalEffectCommittedAwaitingSourceOrderTerminal,
            "Terminal effect phase did not advance.");
        Require(fixture.Outbox.TryCapture(subject.StepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData afterEffect)
            && afterEffect.terminalEffectReceipt.abandonedCompletedWorkBits ==
                BitConverter.SingleToInt32Bits(order.completedWork)
            && afterEffect.terminalEffectReceipt.historicalConsumedWorkBits ==
                BitConverter.SingleToInt32Bits(order.consumedWork),
            "Exact work-loss and pending-effect receipt was not recorded.");
        ProductionApparelOrderTerminalDrainResult terminal = fixture.Outbox
            .TryProgress(subject.StepOperationId);
        bool capturedAfterTerminal = fixture.Outbox.TryCapture(
            subject.StepOperationId,
            out ProductionApparelOrderTerminalDrainSaveData afterTerminal);
        Require(terminal.Status ==
                ProductionApparelOrderTerminalDrainStatus.Applied
            && terminal.Phase == ProductionApparelOrderTerminalDrainPhase
                .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement
            && !fixture.Source.HasLive(order.orderId)
            && capturedAfterTerminal,
            "The exact source-order terminal receipt was not committed.");
        Require(fixture.Outbox.TryProgress(subject.StepOperationId).Status ==
                ProductionApparelOrderTerminalDrainStatus.Replay,
            "Terminal producer replay was not a no-op.");
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionApparelOrderTerminalDrainStatus.Replay,
            "Exact prepare replay depended on the already-retired live source.");
        Require(fixture.Outbox.TryAcknowledge(
                    subject.StepOperationId,
                    terminal.ReceiptFingerprint).Phase ==
                ProductionApparelOrderTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
            "Owner acknowledgement did not advance the producer.");
        Require(fixture.Outbox.TryGarbageCollect(
                    subject.StepOperationId,
                    terminal.ReceiptFingerprint).Status ==
                ProductionApparelOrderTerminalDrainStatus.Deferred,
            "Producer GC ran before its child receipts were collected.");
        fixture.Effects.Collect(afterEffect.terminalEffectReceipt.commitId);
        fixture.Source.Collect(afterTerminal.sourceTerminalReceipt.commitId);
        Require(fixture.Outbox.TryGarbageCollect(
                    subject.StepOperationId,
                    terminal.ReceiptFingerprint).Status ==
                ProductionApparelOrderTerminalDrainStatus.Applied
            && fixture.Outbox.TryGarbageCollect(
                    subject.StepOperationId,
                    terminal.ReceiptFingerprint).Status ==
                ProductionApparelOrderTerminalDrainStatus.Replay,
            "Child-first checkpoint GC was not monotonic and replay-safe.");
    }

    private static void VerifyEffectAndSourceAheadCrashRecovery()
    {
        Fixture effectAhead = new();
        ApparelWorkOrderSaveData effectOrder = CreateRepairOrder("effect-ahead");
        effectAhead.PublishOrder(effectOrder, withLease: true);
        Case effectCase = effectAhead.CreateCase(effectOrder, "effect-ahead");
        effectAhead.Outbox.TryPrepare(effectCase.Request);
        effectAhead.Outbox.TryProgress(effectCase.StepOperationId);
        ProductionApparelOrderTerminalEffectReceipt effectReceipt =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateTerminalEffectReceipt(
                    effectCase.StepOperationId,
                    effectCase.Request.SourceOrder,
                    ProductionApparelOrderTerminalDrainCanonical
                        .CreateSourceOrderFingerprint(
                            effectCase.Request.SourceOrder),
                    effectCase.PendingEffect);
        effectAhead.Effects.TryCommitTerminalEffect(
            effectReceipt, effectCase.PendingEffect);
        Require(effectAhead.Outbox.TryRecover(effectCase.StepOperationId).Phase ==
                ProductionApparelOrderTerminalDrainPhase
                    .TerminalEffectCommittedAwaitingSourceOrderTerminal,
            "Effect-ahead crash window did not replay exact evidence.");

        ProductionApparelOrderSourceTerminalReceipt sourceReceipt =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceTerminalReceipt(
                    effectCase.StepOperationId,
                    effectCase.Request.SourceOrder,
                    effectReceipt.sourceOrderFingerprint,
                    effectReceipt.receiptFingerprint);
        effectAhead.Source.TryCommitSourceTerminal(sourceReceipt);
        Require(effectAhead.Outbox.TryRecover(effectCase.StepOperationId).Phase ==
                ProductionApparelOrderTerminalDrainPhase
                    .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement,
            "Source-ahead crash window did not accept the exact durable receipt.");

        Fixture missingEvidence = new();
        ApparelWorkOrderSaveData missingOrder = CreateRepairOrder("missing-evidence");
        missingEvidence.PublishOrder(missingOrder, withLease: true);
        Case missingCase = missingEvidence.CreateCase(
            missingOrder, "missing-evidence");
        missingEvidence.Outbox.TryPrepare(missingCase.Request);
        missingEvidence.Outbox.TryProgress(missingCase.StepOperationId);
        missingEvidence.Outbox.TryProgress(missingCase.StepOperationId);
        missingEvidence.Source.RemoveLiveWithoutReceipt(missingOrder.orderId);
        Require(missingEvidence.Outbox.TryRecover(missingCase.StepOperationId)
                .Status == ProductionApparelOrderTerminalDrainStatus.Conflict,
            "A missing live order without exact terminal evidence was accepted.");
    }

    private static void VerifyRestoreJoinsOrphansAndTamperRejection()
    {
        Fixture sourceFixture = new();
        ApparelWorkOrderSaveData order = CreateRepairOrder("restore");
        sourceFixture.PublishOrder(order, withLease: true);
        Case subject = sourceFixture.CreateCase(order, "restore");
        sourceFixture.Outbox.TryPrepare(subject.Request);
        sourceFixture.Outbox.TryProgress(subject.StepOperationId);
        sourceFixture.Outbox.TryProgress(subject.StepOperationId);
        ProductionApparelOrderTerminalDrainSaveData producer =
            sourceFixture.Outbox.CaptureCurrentFormat().Single();

        Fixture restore = new();
        restore.PublishOrder(order, withLease: false);
        restore.Effects.Publish(producer.terminalEffectReceipt);
        Require(restore.Outbox.TryRestoreCurrentFormat(
                new[] { producer }, out _),
            "Exact producer/effect restore join was rejected.");

        Fixture childOrphan = new();
        childOrphan.Effects.Publish(producer.terminalEffectReceipt);
        Require(!childOrphan.Outbox.TryRestoreCurrentFormat(
                Array.Empty<ProductionApparelOrderTerminalDrainSaveData>(),
                out string childFailure)
            && childFailure.Contains("orphan", StringComparison.Ordinal),
            "A pending-effect-only orphan was accepted.");

        ProductionApparelOrderTerminalDrainSaveData gramTamper = producer.Clone();
        gramTamper.sourceOrder.repairInputMassGrams++;
        Require(!restore.Outbox.TryRestoreCurrentFormat(
                new[] { gramTamper }, out _),
            "One-gram source drift was accepted during restore.");
        ProductionApparelOrderTerminalDrainSaveData quantityTamper =
            producer.Clone();
        quantityTamper.sourceOrder.repairInputQuantity++;
        Require(!restore.Outbox.TryRestoreCurrentFormat(
                new[] { quantityTamper }, out _),
            "One-unit source drift was accepted during restore.");
        ProductionApparelOrderTerminalDrainSaveData fingerprintTamper =
            producer.Clone();
        fingerprintTamper.sourceOrderFingerprint = new string('0', 64);
        Require(!restore.Outbox.TryRestoreCurrentFormat(
                new[] { fingerprintTamper }, out _),
            "Source fingerprint drift was accepted during restore.");

        Fixture producerAhead = new();
        ApparelWorkOrderSaveData aheadOrder = CreateRejectedOrder("producer-ahead");
        producerAhead.PublishOrder(aheadOrder, withLease: false);
        Case ahead = producerAhead.CreateCase(aheadOrder, "producer-ahead");
        producerAhead.Outbox.TryPrepare(ahead.Request);
        ProductionApparelOrderTerminalDrainSaveData prepared =
            producerAhead.Outbox.CaptureCurrentFormat().Single();
        Fixture producerAheadRestore = new();
        producerAheadRestore.PublishOrder(aheadOrder, withLease: false);
        Require(producerAheadRestore.Outbox.TryRestoreCurrentFormat(
                new[] { prepared }, out _),
            "The valid producer-first child-missing crash window was rejected.");
    }

    private static ProductionApparelOrderTerminalDrainRequest CreateRequest(
        ApparelWorkOrderSaveData order,
        string parentOperationId,
        string stepOperationId,
        bool hasLease,
        string leaseFingerprint,
        ProductionApparelOrderPendingEffectIdentity pending)
    {
        string owner = ProductionFacilityDestructiveDrainOwnerStableIds
            .ApparelWorkOrder(order.orderId);
        string request = ProductionApparelOrderTerminalDrainCanonical
            .CreateRequestFingerprint(
                parentOperationId,
                stepOperationId,
                owner,
                order,
                hasLease,
                leaseFingerprint,
                pending);
        return new ProductionApparelOrderTerminalDrainRequest(
            parentOperationId,
            stepOperationId,
            owner,
            order,
            hasLease,
            leaseFingerprint,
            pending,
            request);
    }

    private static ApparelWorkOrderSaveData CreateRepairOrder(string suffix) =>
        new()
        {
            orderId = "apparel:repair:" + suffix,
            kind = ApparelWorkOrderKind.Repair,
            state = ApparelWorkOrderState.WaitingForDispositionFinalization,
            facilityInstanceId = "building:tailor:" + suffix,
            targetItemInstanceId = "item:apparel:" + suffix,
            requiredWork = 18.5f,
            completedWork = 7.25f,
            consumedWork = 3.5f,
            repairCommitPhase = ApparelRepairCommitPhase.MaterialCommitted,
            repairOperationId = "apparel-repair:apparel:repair:" + suffix,
            repairReasonCode = "apparel-repair-input-incorporated",
            repairCommitId = "physical-disposition:" + suffix,
            repairSourceStackIds = new List<string>
            {
                "stack:repair-material-a",
                "stack:repair-material-b"
            },
            repairInputQuantity = 3,
            repairInputMassGrams = 2_400L,
            repairTargetStackId = "stack:repair-target:" + suffix,
            repairOriginalStatePayload = "{\"durability\":40}",
            repairResolvedStatePayload = "{\"durability\":70}"
        };

    private static ApparelWorkOrderSaveData CreateRejectedOrder(string suffix)
    {
        ApparelWorkOrderSaveData order = new()
        {
            orderId = "apparel:craft:" + suffix,
            kind = ApparelWorkOrderKind.Craft,
            state = ApparelWorkOrderState.WaitingForOutputSpace,
            apparelDefinitionId = "apparel:tunic",
            materialDefinitionId = "textile:linen",
            facilityInstanceId = "building:tailor:" + suffix,
            requiredWork = 8f,
            completedWork = 2f,
            consumedWork = 14f,
            dismantlingRejectedOutput = true,
            rejectedOutputConsumed = true,
            rejectedOutputStackId = "stack:rejected-output",
            rejectedOutputInstanceId = "item:rejected-output:" + suffix,
            rejectedMaterialAmount = 5,
            rejectedMaterialSpawned = 0,
            rejectedRecoveryItemId = "material:rejected-recovery",
            rejectedDismantleInputMassGrams = 5_000L
        };
        order.rejectedDismantleOperationId = ApparelRejectedDismantleOutbox
            .FormatOperationId(order.orderId, order.qualityAttemptIndex);
        order.rejectedDismantleCommitId =
            "physical-disposition:apparel-rejected:" + suffix;
        order.rejectedDismantleRequestFingerprint =
            ApparelRejectedDismantleOutbox.CreateRequestFingerprint(
                order.rejectedOutputStackId);
        return order;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Case
    {
        public string ParentOperationId;
        public string StepOperationId;
        public ProductionApparelOrderPendingEffectIdentity PendingEffect;
        public ProductionApparelOrderTerminalDrainRequest Request;
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Leases = new FakeLeasePort();
            Effects = new FakeEffectPort();
            Source = new FakeSourcePort(Effects);
            Outbox = new ProductionApparelOrderTerminalDrainOutbox(
                new DungeonRuntimeAggregateRootStore(),
                Leases,
                Leases,
                Effects,
                Source);
        }

        public FakeLeasePort Leases { get; }
        public FakeEffectPort Effects { get; }
        public FakeSourcePort Source { get; }
        public ProductionApparelOrderTerminalDrainOutbox Outbox { get; }

        public void PublishOrder(ApparelWorkOrderSaveData order, bool withLease)
        {
            Source.Publish(order);
            if (withLease)
                Leases.Publish(order.orderId, "lease:" + order.orderId);
        }

        public Case CreateCase(ApparelWorkOrderSaveData order, string suffix)
        {
            ProductionApparelOrderTerminalDrainCanonical
                .TryCreatePendingEffectIdentity(
                    order,
                    out ProductionApparelOrderPendingEffectIdentity pending,
                    out string failure);
            Require(string.IsNullOrEmpty(failure), failure);
            bool hasLease = Leases.Has(order.orderId);
            string leaseFingerprint = hasLease
                ? Leases.Fingerprint(order.orderId)
                : ProductionApparelOrderTerminalDrainCanonical
                    .CreateNoLeaseAuthorityFingerprint(order.orderId);
            string parent = "production-facility-destructive-drain:building:"
                + suffix;
            string step = parent + ":apparel:" + suffix;
            return new Case
            {
                ParentOperationId = parent,
                StepOperationId = step,
                PendingEffect = pending,
                Request = CreateRequest(
                    order,
                    parent,
                    step,
                    hasLease,
                    leaseFingerprint,
                    pending)
            };
        }
    }

    private sealed class FakeLeasePort :
        IApparelLeaseAuthorityQuery,
        IApparelLeaseAuthorityCommand
    {
        private readonly Dictionary<string, string> fingerprints =
            new(StringComparer.Ordinal);

        public void Publish(string owner, string salt) =>
            fingerprints[owner] = ProductionApparelOrderTerminalDrainCanonical
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

        public ApparelLeaseAuthorityReleaseResult TryReleaseExact(
            string ownerOperationId,
            string expectedFingerprint,
            ItemReservationReleaseReason reason)
        {
            if (!fingerprints.TryGetValue(ownerOperationId, out string current))
            {
                return new ApparelLeaseAuthorityReleaseResult(
                    ApparelLeaseAuthorityReleaseStatus.Replay,
                    0,
                    string.Empty,
                    string.Empty);
            }
            if (!string.Equals(current, expectedFingerprint,
                    StringComparison.Ordinal))
            {
                return new ApparelLeaseAuthorityReleaseResult(
                    ApparelLeaseAuthorityReleaseStatus.Conflict,
                    0,
                    current,
                    "fixture-lease-fingerprint-conflict");
            }
            fingerprints.Remove(ownerOperationId);
            return new ApparelLeaseAuthorityReleaseResult(
                ApparelLeaseAuthorityReleaseStatus.Applied,
                1,
                current,
                string.Empty);
        }
    }

    private sealed class FakeEffectPort :
        IProductionApparelOrderTerminalEffectPort
    {
        private readonly Dictionary<string,
            ProductionApparelOrderTerminalEffectReceipt> receipts =
            new(StringComparer.Ordinal);

        public IReadOnlyList<ProductionApparelOrderTerminalEffectReceipt>
            CaptureTerminalEffectReceipts() => receipts.Values
            .OrderBy(value => value.commitId, StringComparer.Ordinal)
            .Select(value => value.Clone()).ToArray();

        public bool TryCaptureTerminalEffectReceipt(
            string commitId,
            out ProductionApparelOrderTerminalEffectReceipt receipt)
        {
            receipt = null;
            if (!receipts.TryGetValue(commitId, out var value))
                return false;
            receipt = value.Clone();
            return true;
        }

        public ProductionApparelOrderTerminalEffectApplyResult
            TryCommitTerminalEffect(
                ProductionApparelOrderTerminalEffectReceipt expectedReceipt,
                ProductionApparelOrderPendingEffectIdentity pendingEffect)
        {
            if (expectedReceipt == null)
                return Conflict("fixture-effect-null");
            if (receipts.TryGetValue(expectedReceipt.commitId, out var existing))
            {
                return new ProductionApparelOrderTerminalEffectApplyResult(
                    ProductionApparelOrderTerminalDrainCanonical
                        .EffectReceiptEquals(existing, expectedReceipt)
                        ? ProductionApparelOrderTerminalDrainStatus.Replay
                        : ProductionApparelOrderTerminalDrainStatus.Conflict,
                    existing,
                    ProductionApparelOrderTerminalDrainCanonical
                        .EffectReceiptEquals(existing, expectedReceipt)
                        ? string.Empty
                        : "fixture-effect-conflict");
            }
            receipts.Add(expectedReceipt.commitId, expectedReceipt.Clone());
            return new ProductionApparelOrderTerminalEffectApplyResult(
                ProductionApparelOrderTerminalDrainStatus.Applied,
                expectedReceipt,
                string.Empty);
        }

        public void Publish(ProductionApparelOrderTerminalEffectReceipt value) =>
            receipts[value.commitId] = value.Clone();
        public void Collect(string commitId) => receipts.Remove(commitId);

        private static ProductionApparelOrderTerminalEffectApplyResult Conflict(
            string reason) => new(
                ProductionApparelOrderTerminalDrainStatus.Conflict,
                null,
                reason);
    }

    private sealed class FakeSourcePort :
        IProductionApparelOrderSourceTerminalPort
    {
        private readonly FakeEffectPort effects;
        private readonly Dictionary<string, ApparelWorkOrderSaveData> live =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string,
            ProductionApparelOrderSourceTerminalReceipt> receipts =
            new(StringComparer.Ordinal);

        public FakeSourcePort(FakeEffectPort effects) => this.effects = effects;
        public bool HasLive(string orderId) => live.ContainsKey(orderId);
        public void Publish(ApparelWorkOrderSaveData order) =>
            live[order.orderId] =
                ProductionApparelOrderTerminalDrainCanonical.CloneOrder(order);
        public void ReplaceLive(string orderId, Action<ApparelWorkOrderSaveData> mutate)
        {
            ApparelWorkOrderSaveData value =
                ProductionApparelOrderTerminalDrainCanonical.CloneOrder(live[orderId]);
            mutate(value);
            live[orderId] = value;
        }
        public void RemoveLiveWithoutReceipt(string orderId) => live.Remove(orderId);

        public bool TryCaptureLiveOrder(
            string orderId,
            out ApparelWorkOrderSaveData sourceOrder,
            out string failureReason)
        {
            sourceOrder = null;
            if (!live.TryGetValue(orderId, out var value))
            {
                failureReason = "fixture-apparel-order-missing";
                return false;
            }
            sourceOrder = ProductionApparelOrderTerminalDrainCanonical
                .CloneOrder(value);
            failureReason = string.Empty;
            return true;
        }

        public IReadOnlyList<ProductionApparelOrderSourceTerminalReceipt>
            CaptureSourceTerminalReceipts() => receipts.Values
            .OrderBy(value => value.commitId, StringComparer.Ordinal)
            .Select(value => value.Clone()).ToArray();

        public bool TryCaptureSourceTerminalReceipt(
            string commitId,
            out ProductionApparelOrderSourceTerminalReceipt receipt)
        {
            receipt = null;
            if (!receipts.TryGetValue(commitId, out var value))
                return false;
            receipt = value.Clone();
            return true;
        }

        public ProductionApparelOrderSourceTerminalApplyResult
            TryCommitSourceTerminal(
                ProductionApparelOrderSourceTerminalReceipt expectedReceipt)
        {
            if (expectedReceipt == null)
                return Conflict("fixture-source-receipt-null");
            bool foundEffect = effects.CaptureTerminalEffectReceipts().Any(value =>
                string.Equals(value.receiptFingerprint,
                    expectedReceipt.terminalEffectReceiptFingerprint,
                    StringComparison.Ordinal));
            if (!foundEffect)
            {
                return Conflict("fixture-source-effect-missing");
            }
            if (receipts.TryGetValue(expectedReceipt.commitId, out var existing))
            {
                bool same = ProductionApparelOrderTerminalDrainCanonical
                    .SourceReceiptEquals(existing, expectedReceipt);
                return new ProductionApparelOrderSourceTerminalApplyResult(
                    same
                        ? ProductionApparelOrderTerminalDrainStatus.Replay
                        : ProductionApparelOrderTerminalDrainStatus.Conflict,
                    existing,
                    same ? string.Empty : "fixture-source-receipt-conflict");
            }
            if (!live.TryGetValue(expectedReceipt.orderId, out var order)
                || !string.Equals(
                    ProductionApparelOrderTerminalDrainCanonical
                        .CreateSourceOrderFingerprint(order),
                    expectedReceipt.sourceOrderFingerprint,
                    StringComparison.Ordinal))
                return Conflict("fixture-source-live-missing-or-drifted");
            live.Remove(expectedReceipt.orderId);
            receipts.Add(expectedReceipt.commitId, expectedReceipt.Clone());
            return new ProductionApparelOrderSourceTerminalApplyResult(
                ProductionApparelOrderTerminalDrainStatus.Applied,
                expectedReceipt,
                string.Empty);
        }

        public void Collect(string commitId) => receipts.Remove(commitId);

        private static ProductionApparelOrderSourceTerminalApplyResult Conflict(
            string reason) => new(
                ProductionApparelOrderTerminalDrainStatus.Conflict,
                null,
                reason);
    }
}
#endif
