#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionGenericBillTerminalDrainOutboxDebugScenarios
{
    private const int ExactInputQuantity = 3;
    private const long ExactInputMassGrams = 3_000L;

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Generic Bill Terminal Drain Outbox")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_GENERIC_BILL_TERMINAL_DRAIN_OUTBOX=PASS");
    }

    public static void RunAll()
    {
        VerifyPrepareReplayConflictAndExactChildReceipt();
        VerifyProducerAheadCrashWindow();
        VerifyWipReceiptClaimAndExactBillRemovalOrdering();
        VerifyCrashRecoveryPhaseMatrix();
        VerifyAcknowledgementAndChildFirstGarbageCollection();
        VerifyCurrentFormatRestoreTamperAndOrdering();
    }

    private static void VerifyPrepareReplayConflictAndExactChildReceipt()
    {
        Fixture fixture = new();
        BillCase subject = fixture.AddBill("prepare");
        fixture.Child.PrepareAndCommit(subject.ChildReceipt);

        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied,
            "Generic terminal drain prepare did not apply.");
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionGenericBillTerminalDrainStatus.Replay,
            "Equivalent generic terminal drain prepare did not replay.");

        ProductionGenericBillTerminalDrainRequest conflictingRequest =
            CreateGenericRequest(
                subject.ParentOperationId + ":conflict",
                subject.StepOperationId,
                subject.OwnerStableId,
                subject.SourceBill,
                subject.ChildStepOperationId,
                subject.ChildReceipt.requestFingerprint);
        Require(fixture.Outbox.TryPrepare(conflictingRequest).Status ==
                ProductionGenericBillTerminalDrainStatus.Conflict,
            "A changed request under the same producer step was accepted.");

        ProductionGenericBillTerminalDrainRequest duplicateOwnerRequest =
            CreateGenericRequest(
                subject.ParentOperationId,
                subject.StepOperationId + ":duplicate-owner",
                subject.OwnerStableId,
                subject.SourceBill,
                subject.ChildStepOperationId + ":duplicate-owner",
                subject.ChildReceipt.requestFingerprint);
        Require(fixture.Outbox.TryPrepare(duplicateOwnerRequest).Status ==
                ProductionGenericBillTerminalDrainStatus.Conflict,
            "A second producer was allowed to own the same frozen bill.");

        ProductionGenericBillTerminalDrainResult recorded = fixture.Outbox
            .TryProgress(subject.StepOperationId);
        Require(recorded.Status ==
                ProductionGenericBillTerminalDrainStatus.Applied
            && recorded.Phase == ProductionGenericBillTerminalDrainPhase
                .InputDestinationReceiptRecordedAwaitingAcknowledgement
            && fixture.Outbox.TryCapture(
                subject.StepOperationId,
                out ProductionGenericBillTerminalDrainSaveData saved)
            && saved.releasedInputQuantity == ExactInputQuantity
            && saved.releasedInputMassGrams == ExactInputMassGrams
            && string.Equals(
                saved.inputDestinationDrainCommitId,
                subject.ChildReceipt.commitId,
                StringComparison.Ordinal)
            && string.Equals(
                saved.inputDestinationDrainReceiptFingerprint,
                subject.ChildReceipt.receiptFingerprint,
                StringComparison.Ordinal),
            "The exact child receipt quantity, grams, commit, and receipt were not recorded.");

        Fixture gramDriftFixture = new();
        BillCase gramDrift = gramDriftFixture.AddBill("gram-drift");
        Require(gramDriftFixture.Outbox.TryPrepare(gramDrift.Request).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied,
            "One-gram drift fixture did not prepare.");
        ProductionInputDestinationCustodyDrainSaveData tamperedChild =
            gramDrift.ChildReceipt.Clone();
        tamperedChild.releasedMassGrams--;
        gramDriftFixture.Child.PublishUncheckedForTamperScenario(tamperedChild);
        Require(gramDriftFixture.Outbox.TryProgress(
                    gramDrift.StepOperationId).Status ==
                ProductionGenericBillTerminalDrainStatus.Conflict,
            "A one-gram child receipt drift was accepted.");
    }

    private static void VerifyProducerAheadCrashWindow()
    {
        Fixture fixture = new();
        BillCase subject = fixture.AddBill("producer-ahead");
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied,
            "Producer-ahead crash fixture did not durably prepare.");
        IReadOnlyList<ProductionGenericBillTerminalDrainSaveData> checkpoint =
            fixture.Outbox.CaptureCurrentFormat();
        string beforeDeferred = Serialize(checkpoint);
        Require(checkpoint.Count == 1
            && checkpoint[0].phase == ProductionGenericBillTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt
            && !fixture.Child.TryCapture(subject.ChildStepOperationId, out _),
            "Producer-ahead checkpoint did not contain exactly the parent-only Prepared row.");

        ProductionGenericBillTerminalDrainResult missingChild = fixture.Outbox
            .TryProgress(subject.StepOperationId);
        Require(missingChild.Status ==
                ProductionGenericBillTerminalDrainStatus.Deferred
            && missingChild.Phase == ProductionGenericBillTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt
            && string.Equals(
                missingChild.FailureReason,
                "production-generic-terminal-child-receipt-missing",
                StringComparison.Ordinal)
            && string.Equals(
                Serialize(fixture.Outbox.CaptureCurrentFormat()),
                beforeDeferred,
                StringComparison.Ordinal),
            "Missing child authority did not defer without mutating the producer checkpoint.");

        ProductionGenericBillTerminalDrainOutbox restored = new(
            fixture.RootStore,
            fixture.Persistence,
            fixture.Claims,
            fixture.Child);
        Require(restored.TryRestoreCurrentFormat(
                    checkpoint,
                    out string restoreFailure)
                && string.Equals(
                    Serialize(restored.CaptureCurrentFormat()),
                    beforeDeferred,
                    StringComparison.Ordinal),
            "Producer-ahead checkpoint did not restore exactly: " + restoreFailure);

        fixture.Child.PrepareAndCommit(subject.ChildReceipt);
        Require(restored.TryCapture(
                subject.StepOperationId,
                out ProductionGenericBillTerminalDrainSaveData prepared)
            && prepared.phase == ProductionGenericBillTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt,
            "Child terminal commit was published without its prepared producer join.");
        ProductionGenericBillTerminalDrainResult recovered = restored.TryRecover(
            subject.StepOperationId);
        Require(recovered.Status ==
                ProductionGenericBillTerminalDrainStatus.Applied
            && recovered.Phase == ProductionGenericBillTerminalDrainPhase
                .InputDestinationReceiptRecordedAwaitingAcknowledgement
            && restored.TryCapture(
                subject.StepOperationId,
                out ProductionGenericBillTerminalDrainSaveData recorded)
            && recorded.releasedInputQuantity == ExactInputQuantity
            && recorded.releasedInputMassGrams == ExactInputMassGrams
            && string.Equals(
                recorded.inputDestinationDrainReceiptFingerprint,
                subject.ChildReceipt.receiptFingerprint,
                StringComparison.Ordinal),
            "Producer-ahead recovery did not join the exact committed child receipt.");

        Fixture childOnlyFixture = new();
        BillCase childOnly = childOnlyFixture.AddBill("child-only-orphan");
        childOnlyFixture.Child.PrepareAndCommit(childOnly.ChildReceipt);
        ProductionGenericBillTerminalDrainResult orphanRecovery =
            childOnlyFixture.Outbox.TryRecover(childOnly.StepOperationId);
        Require(childOnlyFixture.Outbox.CaptureCurrentFormat().Count == 0
            && orphanRecovery.Status ==
                ProductionGenericBillTerminalDrainStatus.Conflict
            && string.Equals(
                orphanRecovery.FailureReason,
                "production-generic-terminal-producer-missing",
                StringComparison.Ordinal),
            "A child-only orphan was treated as a valid generic save join.");
    }

    private static void VerifyWipReceiptClaimAndExactBillRemovalOrdering()
    {
        Fixture fixture = new();
        BillCase subject = fixture.AddBill("terminal-order");
        BillCase sibling = fixture.AddBill("terminal-sibling");
        fixture.Child.PrepareAndCommit(subject.ChildReceipt);
        int initialBillVersion = fixture.Session.BillVersion;
        bool observedWipBeforeRemoval = false;
        fixture.Claims.OnRevokeIfPresent = record =>
        {
            observedWipBeforeRemoval = fixture.Session.WipTerminalReceipts.Any(
                    value => value != null && string.Equals(
                        value.billId,
                        subject.SourceBill.billId,
                        StringComparison.Ordinal))
                && fixture.Session.Bills.Any(value => value != null
                    && string.Equals(
                        value.billId.Value,
                        subject.SourceBill.billId,
                        StringComparison.Ordinal));
            Require(string.Equals(
                    record.billId.Value,
                    subject.SourceBill.billId,
                    StringComparison.Ordinal),
                "Claim revoke targeted a bill other than the frozen owner.");
        };

        AdvanceTo(fixture, subject, ProductionGenericBillTerminalDrainPhase
            .InputDestinationAcknowledgedAwaitingBillTerminal);
        ProductionGenericBillTerminalDrainResult terminal = fixture.Outbox
            .TryProgress(subject.StepOperationId);

        ProductionWipTerminalReceiptSaveData wip = fixture.Session
            .WipTerminalReceipts.Single(value => string.Equals(
                value.billId,
                subject.SourceBill.billId,
                StringComparison.Ordinal));
        Require(terminal.Status ==
                ProductionGenericBillTerminalDrainStatus.Applied
            && terminal.Phase == ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement
            && observedWipBeforeRemoval
            && fixture.Claims.RevokeIfPresentCount == 1
            && fixture.Session.BillVersion == initialBillVersion + 1
            && !fixture.Session.Bills.Any(value => string.Equals(
                value.billId.Value,
                subject.SourceBill.billId,
                StringComparison.Ordinal))
            && fixture.Session.Bills.Count == 1
            && string.Equals(
                fixture.Session.Bills.Single().billId.Value,
                sibling.SourceBill.billId,
                StringComparison.Ordinal)
            && wip.inputQuantity == subject.SourceBill.wipInputQuantity
            && wip.inputMassGrams == subject.SourceBill.wipInputMassGrams
            && wip.committedOutputMassGrams == 2_000L
            && wip.declaredLossMassGrams == 1_000L
            && wip.reason == ProductionWipTerminalReason.FacilityDestroyed,
            "WIP publication, claim revoke, and exact bill removal ordering was not preserved.");

        Require(fixture.Outbox.TryProgress(subject.StepOperationId).Status ==
                ProductionGenericBillTerminalDrainStatus.Replay
            && fixture.Claims.RevokeIfPresentCount == 1
            && fixture.Session.Bills.Count == 1,
            "Terminal replay repeated claim revoke or removed a sibling bill.");
    }

    private static void VerifyCrashRecoveryPhaseMatrix()
    {
        ProductionGenericBillTerminalDrainPhase[] phases =
        {
            ProductionGenericBillTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt,
            ProductionGenericBillTerminalDrainPhase
                .InputDestinationReceiptRecordedAwaitingAcknowledgement,
            ProductionGenericBillTerminalDrainPhase
                .InputDestinationAcknowledgedAwaitingBillTerminal,
            ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement,
            ProductionGenericBillTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc
        };

        foreach (ProductionGenericBillTerminalDrainPhase phase in phases)
        {
            Fixture fixture = new();
            BillCase subject = fixture.AddBill(
                "recovery-" + ((int)phase).ToString());
            AdvanceTo(fixture, subject, phase);
            ProductionGenericBillTerminalDrainSaveData checkpoint = fixture
                .Outbox.CaptureCurrentFormat().Single().Clone();

            ProductionGenericBillTerminalDrainOutbox restored = new(
                fixture.RootStore,
                fixture.Persistence,
                fixture.Claims,
                fixture.Child);
            Require(restored.TryRestoreCurrentFormat(
                    new[] { checkpoint },
                    out string restoreFailure),
                "Recovery matrix restore failed at " + phase + ": "
                + restoreFailure);
            if (phase == ProductionGenericBillTerminalDrainPhase
                    .PreparedAwaitingInputDestinationReceipt)
            {
                fixture.Child.PrepareAndCommit(subject.ChildReceipt);
            }

            ProductionGenericBillTerminalDrainResult recovered = restored
                .TryRecover(subject.StepOperationId);
            ProductionGenericBillTerminalDrainPhase expectedPhase = phase switch
            {
                ProductionGenericBillTerminalDrainPhase
                    .PreparedAwaitingInputDestinationReceipt =>
                    ProductionGenericBillTerminalDrainPhase
                        .InputDestinationReceiptRecordedAwaitingAcknowledgement,
                ProductionGenericBillTerminalDrainPhase
                    .InputDestinationReceiptRecordedAwaitingAcknowledgement =>
                    ProductionGenericBillTerminalDrainPhase
                        .InputDestinationAcknowledgedAwaitingBillTerminal,
                ProductionGenericBillTerminalDrainPhase
                    .InputDestinationAcknowledgedAwaitingBillTerminal =>
                    ProductionGenericBillTerminalDrainPhase
                        .BillTerminalCommittedAwaitingOwnerAcknowledgement,
                _ => phase
            };
            ProductionGenericBillTerminalDrainStatus expectedStatus = phase >=
                ProductionGenericBillTerminalDrainPhase
                    .BillTerminalCommittedAwaitingOwnerAcknowledgement
                ? ProductionGenericBillTerminalDrainStatus.Replay
                : ProductionGenericBillTerminalDrainStatus.Applied;
            Require(recovered.Status == expectedStatus
                && recovered.Phase == expectedPhase
                && restored.TryCapture(
                    subject.StepOperationId,
                    out ProductionGenericBillTerminalDrainSaveData live)
                && live.phase == expectedPhase
                && ProductionGenericBillTerminalDrainCanonical.IsValidSave(live),
                "Recovery matrix did not resume deterministically from " + phase
                + ".");
        }
    }

    private static void VerifyAcknowledgementAndChildFirstGarbageCollection()
    {
        Fixture fixture = new();
        BillCase subject = fixture.AddBill("ack-gc");
        AdvanceTo(fixture, subject, ProductionGenericBillTerminalDrainPhase
            .BillTerminalCommittedAwaitingOwnerAcknowledgement);
        Require(fixture.Outbox.TryCapture(
                subject.StepOperationId,
                out ProductionGenericBillTerminalDrainSaveData terminal),
            "Terminal producer was not captured for acknowledgement.");

        Require(fixture.Outbox.TryGarbageCollect(
                    subject.StepOperationId,
                    terminal.receiptFingerprint).Status ==
                ProductionGenericBillTerminalDrainStatus.Deferred,
            "Unacknowledged generic producer was garbage-collected.");
        Require(fixture.Outbox.TryAcknowledge(
                    subject.StepOperationId,
                    new string('0', 64)).Status ==
                ProductionGenericBillTerminalDrainStatus.Conflict,
            "A mismatched owner acknowledgement was accepted.");
        Require(fixture.Outbox.TryAcknowledge(
                    subject.StepOperationId,
                    terminal.receiptFingerprint).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied
            && fixture.Outbox.TryAcknowledge(
                    subject.StepOperationId,
                    terminal.receiptFingerprint).Status ==
                ProductionGenericBillTerminalDrainStatus.Replay,
            "Generic owner acknowledgement was not exact and idempotent.");
        Require(fixture.Outbox.TryGarbageCollect(
                    subject.StepOperationId,
                    new string('f', 64)).Status ==
                ProductionGenericBillTerminalDrainStatus.Conflict,
            "A mismatched GC receipt was accepted.");

        bool childFirstObserved = false;
        fixture.Child.OnGarbageCollected = childStepOperationId =>
        {
            childFirstObserved = !fixture.Child.TryCapture(
                    childStepOperationId,
                    out _)
                && fixture.Outbox.TryCapture(
                    subject.StepOperationId,
                    out ProductionGenericBillTerminalDrainSaveData parent)
                && parent.phase == ProductionGenericBillTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc;
        };
        Require(fixture.Outbox.TryGarbageCollect(
                    subject.StepOperationId,
                    terminal.receiptFingerprint).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied
            && childFirstObserved
            && fixture.Child.GarbageCollectionCount == 1
            && !fixture.Child.TryCapture(subject.ChildStepOperationId, out _)
            && !fixture.Outbox.TryCapture(subject.StepOperationId, out _),
            "GC did not delete child authority before generic producer authority.");
        Require(fixture.Outbox.TryGarbageCollect(
                    subject.StepOperationId,
                    terminal.receiptFingerprint).Status ==
                ProductionGenericBillTerminalDrainStatus.Replay
            && fixture.Child.GarbageCollectionCount == 1,
            "Generic GC replay invoked child GC more than once.");
    }

    private static void VerifyCurrentFormatRestoreTamperAndOrdering()
    {
        Fixture fixture = new();
        BillCase later = fixture.AddBill("restore-b");
        BillCase earlier = fixture.AddBill("restore-a");
        Require(fixture.Outbox.TryPrepare(later.Request).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied
            && fixture.Outbox.TryPrepare(earlier.Request).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied,
            "Deterministic restore fixtures did not prepare.");

        IReadOnlyList<ProductionGenericBillTerminalDrainSaveData> captured =
            fixture.Outbox.CaptureCurrentFormat();
        Require(captured.Select(value => value.stepOperationId).SequenceEqual(
                new[] { earlier.StepOperationId, later.StepOperationId },
                StringComparer.Ordinal),
            "Current-format capture was not ordinally deterministic.");
        string canonical = Serialize(captured);

        ProductionGenericBillTerminalDrainOutbox restored = new(
            fixture.RootStore,
            fixture.Persistence,
            fixture.Claims,
            fixture.Child);
        Require(restored.TryRestoreCurrentFormat(
                    captured.Reverse(),
                    out string restoreFailure)
                && string.Equals(
                    Serialize(restored.CaptureCurrentFormat()),
                    canonical,
                    StringComparison.Ordinal),
            "Reversed current-format input did not restore canonically: "
            + restoreFailure);

        ProductionGenericBillTerminalDrainSaveData pristineFirst =
            captured[0].Clone();
        ProductionGenericBillTerminalDrainSaveData tampered = captured[0].Clone();
        tampered.requestFingerprint = new string('f', 64);
        Require(!restored.TryRestoreCurrentFormat(
                    new[] { tampered },
                    out string tamperFailure)
            && string.Equals(
                tamperFailure,
                "production-generic-terminal-restore-invalid",
                StringComparison.Ordinal)
            && string.Equals(
                Serialize(restored.CaptureCurrentFormat()),
                canonical,
                StringComparison.Ordinal),
            "Tampered current-format restore was not rejected atomically.");

        Require(!restored.TryRestoreCurrentFormat(
                    new[] { pristineFirst, pristineFirst.Clone() },
                    out string duplicateFailure)
            && string.Equals(
                duplicateFailure,
                "production-generic-terminal-restore-invalid",
                StringComparison.Ordinal)
            && string.Equals(
                Serialize(restored.CaptureCurrentFormat()),
                canonical,
                StringComparison.Ordinal),
            "Duplicate current-format authority was not rejected atomically.");

        pristineFirst.sourceBill.recipeId = "recipe:qa:mutated-clone";
        Require(string.Equals(
                Serialize(restored.CaptureCurrentFormat()),
                canonical,
                StringComparison.Ordinal),
            "Mutating a captured clone altered live producer authority.");
    }

    private static void AdvanceTo(
        Fixture fixture,
        BillCase subject,
        ProductionGenericBillTerminalDrainPhase target)
    {
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied,
            "Advance fixture prepare failed for " + target + ".");
        if (target == ProductionGenericBillTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt)
            return;

        fixture.Child.PrepareAndCommit(subject.ChildReceipt);
        Require(fixture.Outbox.TryProgress(subject.StepOperationId).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied,
            "Advance fixture child receipt failed for " + target + ".");
        if (target == ProductionGenericBillTerminalDrainPhase
                .InputDestinationReceiptRecordedAwaitingAcknowledgement)
            return;

        Require(fixture.Outbox.TryProgress(subject.StepOperationId).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied,
            "Advance fixture child acknowledgement failed for " + target + ".");
        if (target == ProductionGenericBillTerminalDrainPhase
                .InputDestinationAcknowledgedAwaitingBillTerminal)
            return;

        ProductionGenericBillTerminalDrainResult terminal = fixture.Outbox
            .TryProgress(subject.StepOperationId);
        Require(terminal.Status ==
                ProductionGenericBillTerminalDrainStatus.Applied,
            "Advance fixture bill terminal commit failed for " + target + ".");
        if (target == ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement)
            return;

        Require(fixture.Outbox.TryAcknowledge(
                    subject.StepOperationId,
                    terminal.ReceiptFingerprint).Status ==
                ProductionGenericBillTerminalDrainStatus.Applied,
            "Advance fixture owner acknowledgement failed for " + target + ".");
    }

    private static ProductionGenericBillTerminalDrainRequest CreateGenericRequest(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        ProductionBillSaveData sourceBill,
        string childStepOperationId,
        string childRequestFingerprint)
    {
        string requestFingerprint = ProductionGenericBillTerminalDrainCanonical
            .CreateRequestFingerprint(
                parentOperationId,
                stepOperationId,
                ownerStableId,
                sourceBill,
                childStepOperationId,
                childRequestFingerprint);
        return new ProductionGenericBillTerminalDrainRequest(
            parentOperationId,
            stepOperationId,
            ownerStableId,
            sourceBill,
            childStepOperationId,
            childRequestFingerprint,
            requestFingerprint);
    }

    private static ProductionInputDestinationCustodyDrainSaveData
        CreateChildReceipt(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            ProductionBillSaveData sourceBill,
            string suffix)
    {
        ProductionInputDestinationDrainStackSaveData stack = new()
        {
            stackId = "stack:qa:generic-terminal:" + suffix,
            itemId = "material:qa:generic-terminal",
            componentFingerprint = new string('c', 64),
            quantity = ExactInputQuantity,
            massGrams = ExactInputMassGrams,
            state = WorldItemStackState.Stored,
            positionX = 2,
            positionY = 3,
            sourceStorageDestinationId = "warehouse:qa:generic-terminal",
            destinationPositionX = 7,
            destinationPositionY = 9,
            reservationRevision = 4L
        };
        string sourceClaimFingerprint = new string('a', 64);
        string sourceOwnershipFingerprint = new string('b', 64);
        string requestFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                parentOperationId,
                stepOperationId,
                ownerStableId,
                sourceBill.billId,
                sourceBill.buildingInstanceId,
                sourceBill.materialDestinationId,
                7,
                9,
                sourceClaimFingerprint,
                sourceOwnershipFingerprint,
                new[] { stack },
                Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
                Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                ExactInputQuantity,
                ExactInputMassGrams);
        string resultFingerprint = new string('d', 64);
        string commitId = ProductionInputDestinationCustodyDrainFingerprint
            .CreateCommit(stepOperationId, requestFingerprint);
        ProductionInputDestinationCustodyDrainSaveData result = new()
        {
            parentOperationId = parentOperationId,
            stepOperationId = stepOperationId,
            ownerStableId = ownerStableId,
            billId = sourceBill.billId,
            facilityId = sourceBill.buildingInstanceId,
            sourceDestinationId = sourceBill.materialDestinationId,
            ownerGridX = 7,
            ownerGridY = 9,
            sourceClaimFingerprint = sourceClaimFingerprint,
            sourceOwnershipFingerprint = sourceOwnershipFingerprint,
            requestFingerprint = requestFingerprint,
            phase = ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck,
            sourceStacks = new List<ProductionInputDestinationDrainStackSaveData>
            {
                stack.Clone()
            },
            sourceOperations = new List<
                ProductionInputDestinationDrainOperationSaveData>(),
            sourceActors = new List<
                ProductionInputDestinationDrainActorSaveData>(),
            completedActorIds = new List<string>(),
            releasedOperationIds = new List<string>(),
            releasedStackIds = new List<string> { stack.stackId },
            inputQuantity = ExactInputQuantity,
            inputMassGrams = ExactInputMassGrams,
            releasedQuantity = ExactInputQuantity,
            releasedMassGrams = ExactInputMassGrams,
            resultFingerprint = resultFingerprint,
            commitId = commitId,
            receiptFingerprint =
                ProductionInputDestinationCustodyDrainFingerprint.CreateReceipt(
                    requestFingerprint,
                    resultFingerprint,
                    ExactInputQuantity,
                    ExactInputMassGrams,
                    new[] { stack.stackId },
                    Array.Empty<string>())
        };
        Require(ProductionInputDestinationCustodyDrainContract.IsValidSave(result),
            "Fixture child receipt is not current-format valid.");
        return result;
    }

    private static string Serialize(
        IEnumerable<ProductionGenericBillTerminalDrainSaveData> records) =>
        string.Join("\n", (records ?? Array.Empty<
                ProductionGenericBillTerminalDrainSaveData>())
            .Select(JsonUtility.ToJson));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class BillCase
    {
        internal string ParentOperationId { get; set; }
        internal string StepOperationId { get; set; }
        internal string ChildStepOperationId { get; set; }
        internal string OwnerStableId { get; set; }
        internal ProductionBillSaveData SourceBill { get; set; }
        internal ProductionInputDestinationCustodyDrainSaveData ChildReceipt
        { get; set; }
        internal ProductionGenericBillTerminalDrainRequest Request { get; set; }
    }

    private sealed class Fixture
    {
        internal Fixture()
        {
            RootStore = new DungeonRuntimeAggregateRootStore();
            Session = new ProductionAggregateStateSession(RootStore);
            Persistence = new RootBackedBillPersistence(Session);
            Claims = new RecordingInputClaims();
            Child = new RecordingChildOutbox();
            Outbox = new ProductionGenericBillTerminalDrainOutbox(
                RootStore,
                Persistence,
                Claims,
                Child);
        }

        internal DungeonRuntimeAggregateRootStore RootStore { get; }
        internal ProductionAggregateStateSession Session { get; }
        internal RootBackedBillPersistence Persistence { get; }
        internal RecordingInputClaims Claims { get; }
        internal RecordingChildOutbox Child { get; }
        internal ProductionGenericBillTerminalDrainOutbox Outbox { get; }

        internal BillCase AddBill(string suffix)
        {
            string billId = "production-bill:qa:generic-terminal:" + suffix;
            string facilityId = "building:qa:generic-terminal:" + suffix;
            string destinationId = ProductionBillRuntime.DestinationPrefix + billId;
            ProductionBillSaveData source = new()
            {
                billId = billId,
                recipeId = "recipe:qa:generic-terminal",
                buildingInstanceId = facilityId,
                mode = ProductionOrderMode.RepeatCount,
                remainingCycles = 2,
                targetStock = 0,
                materialsConsumed = true,
                cycleSequence = 1,
                wipInputCommitId = "production-wip-input:qa:" + suffix,
                wipInputQuantity = ExactInputQuantity,
                wipInputMassGrams = ExactInputMassGrams,
                outputOutcomeResolved = true,
                resolvedOutputs = new List<ProductionResolvedOutputSaveData>
                {
                    new()
                    {
                        itemId = "material:qa:generic-terminal-output",
                        amount = 2,
                        committedAmount = 2,
                        committedMassGrams = 2_000L
                    }
                },
                preparedOutput = ProductionPreparedOutputBatchSaveData.Unresolved(),
                processWastewaterComponents = new List<
                    ProductionWastewaterComponentSaveData>(),
                processManualWaterTransfers = new List<
                    ProductionManualWaterTransferSaveData>(),
                materialDestinationId = destinationId,
                allowedMaterialIds = new List<string>(),
                allowedWorkerIds = new List<string>(),
                workerContributions = new List<CraftContributionSaveData>(),
                outputReservations = new List<ProductionOutputReservationSaveData>(),
                routePolicies = new List<ProductionConsumerRoutePolicy>(),
                selectedSupplies = new List<ProductionSelectedSupplySaveData>()
            };
            ProductionBillRecord record = ProductionBillRecord.Create(
                (ProductionBillId)billId,
                source.recipeId,
                (BuildingInstanceId)facilityId,
                source.mode,
                source.remainingCycles,
                source.targetStock,
                source.batchStage,
                destinationId);
            record.SetMaterialsConsumed(true);
            record.SetWipInput(new ProductionWipInputReceipt(
                source.wipInputCommitId,
                source.wipInputQuantity,
                source.wipInputMassGrams));
            record.SetResolvedOutputs(source.resolvedOutputs);
            Session.AddBill(record);
            Persistence.Track(source);

            string parentOperationId =
                "production-facility-destructive-drain:qa:generic-terminal:"
                + suffix;
            string stepOperationId =
                "production-generic-terminal-step:qa:" + suffix;
            string childStepOperationId =
                "production-input-destination-step:qa:" + suffix;
            string ownerStableId =
                ProductionFacilityDestructiveDrainOwnerStableIds.GenericBill(
                    billId);
            ProductionInputDestinationCustodyDrainSaveData child =
                CreateChildReceipt(
                    parentOperationId,
                    childStepOperationId,
                    ownerStableId,
                    source,
                    suffix);
            return new BillCase
            {
                ParentOperationId = parentOperationId,
                StepOperationId = stepOperationId,
                ChildStepOperationId = childStepOperationId,
                OwnerStableId = ownerStableId,
                SourceBill = ProductionGenericBillTerminalDrainCanonical
                    .CloneBill(source),
                ChildReceipt = child,
                Request = CreateGenericRequest(
                    parentOperationId,
                    stepOperationId,
                    ownerStableId,
                    source,
                    childStepOperationId,
                    child.requestFingerprint)
            };
        }
    }

    private sealed class RootBackedBillPersistence : IProductionBillPersistence
    {
        private readonly ProductionAggregateStateSession session;
        private readonly Dictionary<string, ProductionBillSaveData> byBillId =
            new(StringComparer.Ordinal);

        internal RootBackedBillPersistence(ProductionAggregateStateSession session) =>
            this.session = session ?? throw new ArgumentNullException(nameof(session));

        internal void Track(ProductionBillSaveData source) =>
            byBillId[source.billId] = ProductionGenericBillTerminalDrainCanonical
                .CloneBill(source);

        public DungeonProductionBillSaveData Capture() => new()
        {
            version = DungeonProductionBillSaveData.CurrentVersion,
            nextBillSequence = session.NextBillSequence,
            bills = session.Bills
                .OrderBy(value => value.billId.Value, StringComparer.Ordinal)
                .Select(value => ProductionGenericBillTerminalDrainCanonical
                    .CloneBill(byBillId[value.billId.Value]))
                .ToList(),
            wipTerminalReceipts = session.WipTerminalReceipts
                .OrderBy(value => value.commitId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToList()
        };

        public ProductionBillRestoreCandidate BuildRestore(
            DungeonProductionBillSaveData snapshot) =>
            ProductionBillRestoreCandidate.Create(
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)),
                session.BillVersion,
                session.StockSensorVersion);

        public void Restore(ProductionBillRestoreCandidate candidate) =>
            session.Restore(candidate);
    }

    private sealed class RecordingInputClaims :
        IProductionInputDestinationClaimRuntime
    {
        internal Action<ProductionBillRecord> OnRevokeIfPresent { get; set; }
        internal int RevokeIfPresentCount { get; private set; }

        public bool TryValidateClaim(
            ProductionBillRecord record,
            out string failureReason) => Succeed(out failureReason);

        public bool TryClaim(
            ProductionBillRecord record,
            ProductionFacilityHandle facility,
            long maxInputBufferMassGrams,
            out string failureReason) => Succeed(out failureReason);

        public bool TryEnsureCapacity(
            ProductionBillRecord record,
            long minimumInputBufferMassGrams,
            out string failureReason) => Succeed(out failureReason);

        public bool TryRevoke(
            ProductionBillRecord record,
            out string failureReason) => TryRevokeIfPresent(
                record,
                out failureReason);

        public bool TryRevokeIfPresent(
            ProductionBillRecord record,
            out string failureReason)
        {
            failureReason = string.Empty;
            RevokeIfPresentCount++;
            OnRevokeIfPresent?.Invoke(record);
            return true;
        }

        public bool TryReplace(
            IReadOnlyList<ProductionBillRecord> records,
            IReadOnlyList<ProductionFacilityHandle> facilities,
            IReadOnlyDictionary<string, long> inputBufferMassGramsByBillId,
            out string failureReason) => Succeed(out failureReason);

        private static bool Succeed(out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RecordingChildOutbox :
        IProductionInputDestinationCustodyDrainOutbox
    {
        private readonly Dictionary<string,
            ProductionInputDestinationCustodyDrainSaveData> records =
            new(StringComparer.Ordinal);

        internal Action<string> OnGarbageCollected { get; set; }
        internal int AcknowledgementCount { get; private set; }
        internal int GarbageCollectionCount { get; private set; }

        internal void PrepareAndCommit(
            ProductionInputDestinationCustodyDrainSaveData value)
        {
            if (!ProductionInputDestinationCustodyDrainContract.IsValidSave(value)
                || value.phase != ProductionInputDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingBillAck)
            {
                throw new InvalidOperationException(
                    "Fixture child prepare/commit requires an exact terminal receipt.");
            }
            records[value.stepOperationId] = value.Clone();
        }

        internal void PublishUncheckedForTamperScenario(
            ProductionInputDestinationCustodyDrainSaveData value) =>
            records[value.stepOperationId] = value.Clone();

        public ProductionInputDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            if (!records.TryGetValue(
                    stepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData value))
                return Conflict("fixture-child-missing");
            if (!string.Equals(
                    value.receiptFingerprint,
                    receiptFingerprint,
                    StringComparison.Ordinal))
                return Conflict("fixture-child-receipt-conflict");
            if (value.phase == ProductionInputDestinationCustodyDrainPhase
                    .BillAcknowledgedAwaitingCheckpointGc)
                return Result(value, ProductionInputDestinationCustodyDrainStatus.Replay);
            if (value.phase != ProductionInputDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingBillAck)
                return Deferred(value, "fixture-child-not-committed");

            value.phase = ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc;
            records[stepOperationId] = value.Clone();
            AcknowledgementCount++;
            return Result(value, ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint)
        {
            if (!records.TryGetValue(
                    stepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData value))
            {
                return new ProductionInputDestinationCustodyDrainResult(
                    ProductionInputDestinationCustodyDrainStatus.Replay,
                    string.Empty,
                    receiptFingerprint,
                    string.Empty);
            }
            if (!string.Equals(
                    value.receiptFingerprint,
                    receiptFingerprint,
                    StringComparison.Ordinal))
                return Conflict("fixture-child-receipt-conflict");
            if (value.phase != ProductionInputDestinationCustodyDrainPhase
                    .BillAcknowledgedAwaitingCheckpointGc)
                return Deferred(value, "fixture-child-not-acknowledged");

            records.Remove(stepOperationId);
            GarbageCollectionCount++;
            OnGarbageCollected?.Invoke(stepOperationId);
            return Result(value, ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public bool TryCapture(
            string stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData record)
        {
            record = null;
            if (!records.TryGetValue(
                    stepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData value))
                return false;
            record = value.Clone();
            return true;
        }

        public ProductionInputDestinationCustodyDrainResult TryPrepare(
            ProductionInputDestinationCustodyDrainRequest request) =>
            Unexpected(nameof(TryPrepare));

        public ProductionInputDestinationCustodyDrainResult TryBeginDraining(
            string stepOperationId,
            string requestFingerprint) => Unexpected(nameof(TryBeginDraining));

        public ProductionInputDestinationCustodyDrainResult
            TryRecordActorCompleted(string stepOperationId, string actorId) =>
            Unexpected(nameof(TryRecordActorCompleted));

        public ProductionInputDestinationCustodyDrainResult
            TryBeginReleasingOperationAuthority(string stepOperationId) =>
            Unexpected(nameof(TryBeginReleasingOperationAuthority));

        public ProductionInputDestinationCustodyDrainResult
            TryRecordOperationReleased(
                string stepOperationId,
                string operationId) => Unexpected(nameof(TryRecordOperationReleased));

        public ProductionInputDestinationCustodyDrainResult
            TryBeginReleasingDestination(string stepOperationId) =>
            Unexpected(nameof(TryBeginReleasingDestination));

        public ProductionInputDestinationCustodyDrainResult TryCommitEffect(
            string stepOperationId,
            IEnumerable<string> releasedStackIds,
            int releasedQuantity,
            long releasedMassGrams,
            string resultFingerprint) => Unexpected(nameof(TryCommitEffect));

        private static ProductionInputDestinationCustodyDrainResult Result(
            ProductionInputDestinationCustodyDrainSaveData value,
            ProductionInputDestinationCustodyDrainStatus status) => new(
            status,
            value.commitId,
            value.receiptFingerprint,
            string.Empty);

        private static ProductionInputDestinationCustodyDrainResult Deferred(
            ProductionInputDestinationCustodyDrainSaveData value,
            string reason) => new(
            ProductionInputDestinationCustodyDrainStatus.Deferred,
            value.commitId,
            value.receiptFingerprint,
            reason);

        private static ProductionInputDestinationCustodyDrainResult Conflict(
            string reason) => new(
            ProductionInputDestinationCustodyDrainStatus.Conflict,
            string.Empty,
            string.Empty,
            reason);

        private static ProductionInputDestinationCustodyDrainResult Unexpected(
            string operation) => throw new InvalidOperationException(
            "Generic bill terminal fixture unexpectedly invoked child "
            + operation + ".");
    }
}
#endif
