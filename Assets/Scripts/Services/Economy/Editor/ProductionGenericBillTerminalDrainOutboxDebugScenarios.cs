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
        VerifyFacilityDestroyedMidWipCurrentFormatIntegration();
        VerifyCrashRecoveryPhaseMatrix();
        VerifyAcknowledgementAndChildFirstGarbageCollection();
        VerifyCheckpointGarbageCollectionTransactions();
        VerifyCurrentFormatRestoreTamperAndOrdering();
    }

    private static void VerifyFacilityDestroyedMidWipCurrentFormatIntegration()
    {
        Fixture live = new();
        BillCase subject = live.AddBill(
            "facility-destroy-mid-wip",
            outputOutcomeResolved: false,
            includeInputDestinationStock: false);
        Require(subject.SourceBill.materialsConsumed
            && !subject.SourceBill.outputOutcomeResolved
            && subject.SourceBill.resolvedOutputs.Count == 0
            && subject.SourceBill.wipInputMassGrams == ExactInputMassGrams,
            "Facility-destroy fixture was not frozen after exact input consumption and before output resolution.");

        AdvanceTo(live, subject, ProductionGenericBillTerminalDrainPhase
            .InputDestinationAcknowledgedAwaitingBillTerminal);
        DungeonProductionBillSaveData beforeTerminalProduction =
            live.Persistence.Capture();
        ProductionGenericBillTerminalDrainSaveData beforeTerminalProducer =
            live.Outbox.CaptureCurrentFormat().Single().Clone();
        Require(live.Child.TryCapture(
                subject.ChildStepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData
                    beforeTerminalChild)
            && beforeTerminalChild.inputQuantity == 0
            && beforeTerminalChild.inputMassGrams == 0L
            && beforeTerminalChild.sourceStacks.Count == 0,
            "Facility-destroy fixture lost its exact input-destination child before the restore boundary.");

        Fixture restoredBeforeTerminal = Fixture.RestoreCurrentFormat(
            beforeTerminalProduction,
            new[] { beforeTerminalProducer },
            new[] { beforeTerminalChild });
        ProductionGenericBillTerminalDrainResult terminal =
            restoredBeforeTerminal.Outbox.TryProgress(subject.StepOperationId);
        ProductionWipTerminalReceiptSaveData wip = restoredBeforeTerminal
            .Session.WipTerminalReceipts.Single();
        Require(terminal.Status ==
                ProductionGenericBillTerminalDrainStatus.Applied
            && terminal.Phase == ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement
            && restoredBeforeTerminal.Session.Bills.Count == 0
            && wip.reason == ProductionWipTerminalReason.FacilityDestroyed
            && wip.inputQuantity == ExactInputQuantity
            && wip.inputMassGrams == ExactInputMassGrams
            && wip.committedOutputMassGrams == 0L
            && wip.processCleanWaterMassGrams == 0L
            && wip.processWastewaterMassGrams == 0L
            && wip.declaredLossMassGrams == ExactInputMassGrams
            && wip.inputMassGrams + wip.processCleanWaterMassGrams ==
                wip.committedOutputMassGrams
                + wip.processWastewaterMassGrams
                + wip.declaredLossMassGrams,
            "Facility destruction did not terminalize the unresolved WIP with exact quantity and gram conservation.");

        DungeonProductionBillSaveData terminalProduction =
            restoredBeforeTerminal.Persistence.Capture();
        ProductionGenericBillTerminalDrainSaveData terminalProducer =
            restoredBeforeTerminal.Outbox.CaptureCurrentFormat().Single().Clone();
        Require(restoredBeforeTerminal.Child.TryCapture(
                subject.ChildStepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData terminalChild),
            "Facility-destroy terminal child authority was not current-format capturable.");

        Fixture restoredTerminal = Fixture.RestoreCurrentFormat(
            terminalProduction,
            new[] { terminalProducer },
            new[] { terminalChild });
        string productionBeforeReplay = JsonUtility.ToJson(terminalProduction);
        ProductionGenericBillTerminalDrainResult replay = restoredTerminal
            .Outbox.TryRecover(subject.StepOperationId);
        Require(replay.Status == ProductionGenericBillTerminalDrainStatus.Replay
            && replay.Phase == ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement
            && restoredTerminal.Session.WipTerminalReceipts.Count == 1
            && string.Equals(
                JsonUtility.ToJson(restoredTerminal.Persistence.Capture()),
                productionBeforeReplay,
                StringComparison.Ordinal),
            "Current-format terminal restore replay duplicated or deleted WIP authority.");

        Debug.Log(
            "BATCH_G_FACILITY_DESTROY_MID_WIP_CURRENT_FORMAT_EXACT=PASS");
    }

    private static void VerifyCheckpointGarbageCollectionTransactions()
    {
        VerifyCheckpointPublishOrderAndRollback();
        VerifyCheckpointWipPrepareExceptionReleasesChildForRetry();
        VerifyCheckpointWipFailureRollsBackChild();
        VerifyCheckpointProducerDriftRollsBackLowerAuthorities();
        VerifyCheckpointMultiProducerRollbackPreflight();
    }

    private static void VerifyCheckpointWipPrepareExceptionReleasesChildForRetry()
    {
        Fixture fixture = CreateCheckpointFixture("gc-wip-prepare-exception",
            out BillCase bill,
            out ProductionGenericBillTerminalDrainSaveData producer);
        ProductionFacilityDestructiveDrainEntrySaveData entry =
            CreateCheckpointEntry(bill, producer);
        ProductionFacilityDestructiveDrainCheckpointGcContext context = new(
            1L,
            new string('a', 64),
            "slot:qa-generic-checkpoint-gc-prepare-exception");
        fixture.Persistence.ThrowNextCheckpointPrepare = true;
        RequireThrows(
            () => fixture.Outbox.PrepareCheckpointGarbageCollection(
                context,
                new[] { entry },
                out _),
            "Injected WIP prepare exception did not escape the participant.");

        ProductionFacilityDestructiveDrainCheckpointGcResult retry = fixture
            .Outbox.PrepareCheckpointGarbageCollection(
                context,
                new[] { entry },
                out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                    retryCandidate);
        Require(retry.Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && retryCandidate != null,
            "WIP prepare exception leaked the child candidate and blocked retry: "
            + retry.Message);
        fixture.Outbox.CompleteCheckpointGarbageCollection(retryCandidate);
    }

    private static void VerifyCheckpointPublishOrderAndRollback()
    {
        Fixture fixture = CreateCheckpointFixture("gc-order", out BillCase bill,
            out ProductionGenericBillTerminalDrainSaveData producer);
        ProductionFacilityDestructiveDrainEntrySaveData entry =
            CreateCheckpointEntry(bill, producer);
        bool childFirst = false;
        bool wipSecond = false;
        fixture.Child.OnCheckpointPublished = () => childFirst =
            fixture.Persistence.HasWip(producer)
            && fixture.Outbox.TryCapture(bill.StepOperationId, out _);
        fixture.Persistence.OnCheckpointPublished = () => wipSecond =
            !fixture.Child.TryCapture(bill.ChildStepOperationId, out _)
            && fixture.Outbox.TryCapture(bill.StepOperationId, out _);

        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate =
            PrepareCheckpoint(fixture, new[] { entry });
        Require(fixture.Outbox.PublishCheckpointGarbageCollection(candidate)
                    .Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && childFirst
            && wipSecond
            && !fixture.Child.TryCapture(bill.ChildStepOperationId, out _)
            && !fixture.Persistence.HasWip(producer)
            && !fixture.Outbox.TryCapture(bill.StepOperationId, out _),
            "Generic checkpoint GC did not publish child, WIP, then producer.");

        bool childRollbackLast = false;
        fixture.Child.OnCheckpointRolledBack = () => childRollbackLast =
            fixture.Persistence.HasWip(producer)
            && fixture.Outbox.TryCapture(bill.StepOperationId, out _);
        fixture.Outbox.RollbackCheckpointGarbageCollection(candidate);
        Require(childRollbackLast
            && fixture.Child.TryCapture(bill.ChildStepOperationId, out _)
            && fixture.Persistence.HasWip(producer)
            && fixture.Outbox.TryCapture(bill.StepOperationId, out _),
            "Generic checkpoint GC reverse rollback did not restore all rows.");
        fixture.Outbox.CompleteCheckpointGarbageCollection(candidate);
        RequireThrows(
            () => fixture.Outbox.PublishCheckpointGarbageCollection(candidate),
            "Completed generic checkpoint candidate was reusable.");
    }

    private static void VerifyCheckpointWipFailureRollsBackChild()
    {
        Fixture fixture = CreateCheckpointFixture("gc-wip-failure",
            out BillCase bill,
            out ProductionGenericBillTerminalDrainSaveData producer);
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate =
            PrepareCheckpoint(fixture,
                new[] { CreateCheckpointEntry(bill, producer) });
        fixture.Persistence.FailNextCheckpointPublish = true;
        Require(fixture.Outbox.PublishCheckpointGarbageCollection(candidate)
                    .Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred
            && !fixture.Child.TryCapture(bill.ChildStepOperationId, out _)
            && fixture.Persistence.HasWip(producer)
            && fixture.Outbox.TryCapture(bill.StepOperationId, out _),
            "Injected WIP publish failure crossed into producer authority.");
        fixture.Outbox.RollbackCheckpointGarbageCollection(candidate);
        Require(fixture.Child.TryCapture(bill.ChildStepOperationId, out _)
            && fixture.Persistence.HasWip(producer)
            && fixture.Outbox.TryCapture(bill.StepOperationId, out _),
            "WIP publish failure did not roll back the published child.");
        fixture.Outbox.CompleteCheckpointGarbageCollection(candidate);
    }

    private static void VerifyCheckpointProducerDriftRollsBackLowerAuthorities()
    {
        Fixture fixture = CreateCheckpointFixture("gc-producer-drift",
            out BillCase bill,
            out ProductionGenericBillTerminalDrainSaveData producer);
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate =
            PrepareCheckpoint(fixture,
                new[] { CreateCheckpointEntry(bill, producer) });
        Require(fixture.Outbox.TryRestoreCurrentFormat(
                Array.Empty<ProductionGenericBillTerminalDrainSaveData>(),
                out _), "Producer drift fixture could not remove the live row.");
        Require(fixture.Outbox.PublishCheckpointGarbageCollection(candidate)
                    .Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred
            && !fixture.Child.TryCapture(bill.ChildStepOperationId, out _)
            && !fixture.Persistence.HasWip(producer),
            "Producer drift was not detected after lower publication.");
        fixture.Outbox.RollbackCheckpointGarbageCollection(candidate);
        Require(fixture.Child.TryCapture(bill.ChildStepOperationId, out _)
            && fixture.Persistence.HasWip(producer)
            && !fixture.Outbox.TryCapture(bill.StepOperationId, out _),
            "Producer drift did not roll back both lower authorities exactly.");
        fixture.Outbox.CompleteCheckpointGarbageCollection(candidate);
    }

    private static void VerifyCheckpointMultiProducerRollbackPreflight()
    {
        Fixture fixture = new();
        BillCase first = fixture.AddBill("gc-multi-a");
        BillCase second = fixture.AddBill("gc-multi-b");
        AdvanceTo(fixture, first, ProductionGenericBillTerminalDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc);
        AdvanceTo(fixture, second, ProductionGenericBillTerminalDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc);
        bool capturedFirst = fixture.Outbox.TryCapture(first.StepOperationId, out var firstRow);
        bool capturedSecond = fixture.Outbox.TryCapture(second.StepOperationId, out var secondRow);
        Require(capturedFirst && capturedSecond,
            "Multi-producer checkpoint fixture was not terminal.");
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate =
            PrepareCheckpoint(fixture, new[]
            {
                CreateCheckpointEntry(first, firstRow),
                CreateCheckpointEntry(second, secondRow)
            });
        Require(fixture.Outbox.PublishCheckpointGarbageCollection(candidate)
                    .Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            "Multi-producer checkpoint publish failed.");
        Require(fixture.Outbox.TryRestoreCurrentFormat(new[] { firstRow }, out _),
            "Multi-producer rollback conflict fixture could not inject one row.");
        RequireThrows(
            () => fixture.Outbox.RollbackCheckpointGarbageCollection(candidate),
            "Multi-producer rollback did not preflight the complete producer set.");
        Require(fixture.Outbox.TryCapture(first.StepOperationId, out _)
            && !fixture.Outbox.TryCapture(second.StepOperationId, out _)
            && !fixture.Child.TryCapture(first.ChildStepOperationId, out _)
            && !fixture.Child.TryCapture(second.ChildStepOperationId, out _)
            && !fixture.Persistence.HasWip(firstRow)
            && !fixture.Persistence.HasWip(secondRow),
            "Failed multi-producer preflight partially rolled back authority.");
        Require(fixture.Outbox.TryRestoreCurrentFormat(
                Array.Empty<ProductionGenericBillTerminalDrainSaveData>(), out _),
            "Multi-producer rollback fixture could not clear injected drift.");
        fixture.Outbox.RollbackCheckpointGarbageCollection(candidate);
        Require(fixture.Outbox.TryCapture(first.StepOperationId, out _)
            && fixture.Outbox.TryCapture(second.StepOperationId, out _)
            && fixture.Child.TryCapture(first.ChildStepOperationId, out _)
            && fixture.Child.TryCapture(second.ChildStepOperationId, out _)
            && fixture.Persistence.HasWip(firstRow)
            && fixture.Persistence.HasWip(secondRow),
            "Multi-producer rollback did not restore the exact candidate set.");
        fixture.Outbox.CompleteCheckpointGarbageCollection(candidate);
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
            string suffix,
            bool includeInputDestinationStock = true)
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
        ProductionInputDestinationDrainStackSaveData[] sourceStacks =
            includeInputDestinationStock
                ? new[] { stack }
                : Array.Empty<ProductionInputDestinationDrainStackSaveData>();
        int inputQuantity = includeInputDestinationStock
            ? ExactInputQuantity
            : 0;
        long inputMassGrams = includeInputDestinationStock
            ? ExactInputMassGrams
            : 0L;
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
                sourceStacks,
                Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
                Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                inputQuantity,
                inputMassGrams);
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
            sourceStacks = sourceStacks.Select(value => value.Clone()).ToList(),
            sourceOperations = new List<
                ProductionInputDestinationDrainOperationSaveData>(),
            sourceActors = new List<
                ProductionInputDestinationDrainActorSaveData>(),
            completedActorIds = new List<string>(),
            releasedOperationIds = new List<string>(),
            releasedStackIds = sourceStacks.Select(value => value.stackId).ToList(),
            inputQuantity = inputQuantity,
            inputMassGrams = inputMassGrams,
            releasedQuantity = inputQuantity,
            releasedMassGrams = inputMassGrams,
            resultFingerprint = resultFingerprint,
            commitId = commitId,
            receiptFingerprint =
                ProductionInputDestinationCustodyDrainFingerprint.CreateReceipt(
                    requestFingerprint,
                    resultFingerprint,
                    inputQuantity,
                    inputMassGrams,
                    sourceStacks.Select(value => value.stackId),
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

    private static Fixture CreateCheckpointFixture(
        string suffix,
        out BillCase bill,
        out ProductionGenericBillTerminalDrainSaveData producer)
    {
        Fixture fixture = new();
        bill = fixture.AddBill(suffix);
        AdvanceTo(
            fixture,
            bill,
            ProductionGenericBillTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc);
        Require(fixture.Outbox.TryCapture(bill.StepOperationId, out producer),
            "Checkpoint fixture did not capture its terminal producer row.");
        return fixture;
    }

    private static ProductionFacilityDestructiveDrainEntrySaveData
        CreateCheckpointEntry(
            BillCase bill,
            ProductionGenericBillTerminalDrainSaveData producer) => new()
        {
            operationId = bill.ParentOperationId,
            cause = ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
            facilityId = bill.SourceBill.buildingInstanceId,
            phase = ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc,
            participants = new List<
                ProductionFacilityDestructiveDrainParticipantSaveData>
            {
                new()
                {
                    participantId = ProductionFacilityDestructiveDrainParticipantIds
                        .GenericProductionBills,
                    contractVersion = 1,
                    owners = new List<
                        ProductionFacilityDestructiveDrainOwnerSaveData>
                    {
                        new()
                        {
                            ownerStableId = bill.OwnerStableId,
                            disposition = ProductionFacilityDestructiveDrainDisposition
                                .Terminalize,
                            stepOperationId = bill.StepOperationId,
                            phase = ProductionFacilityDestructiveDrainStepPhase
                                .OwnerAcknowledged,
                            requestFingerprint = producer.requestFingerprint,
                            commitId = producer.commitId,
                            receiptFingerprint = producer.receiptFingerprint
                        }
                    }
                }
            }
        };

    private static IProductionFacilityDestructiveDrainCheckpointGcCandidate
        PrepareCheckpoint(
            Fixture fixture,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData> entries)
    {
        ProductionFacilityDestructiveDrainCheckpointGcContext context = new(
            1L,
            new string('a', 64),
            "slot:qa-generic-checkpoint-gc");
        ProductionFacilityDestructiveDrainCheckpointGcResult result = fixture
            .Outbox.PrepareCheckpointGarbageCollection(
                context,
                entries,
                out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                    candidate);
        Require(result.Status ==
                    ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
                && candidate != null,
            "Generic checkpoint GC prepare did not apply: " + result.Message);
        return candidate;
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

        internal BillCase AddBill(
            string suffix,
            bool outputOutcomeResolved = true,
            bool includeInputDestinationStock = true)
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
                outputOutcomeResolved = outputOutcomeResolved,
                resolvedOutputs = outputOutcomeResolved
                    ? new List<ProductionResolvedOutputSaveData>
                    {
                        new()
                        {
                        outputLineId = "output:qa-generic-terminal",
                        itemId = "material:qa:generic-terminal-output",
                        outputCapabilityId =
                            ProductionOutputCapabilityIds.StandardDefinition,
                        outputCapabilityVersion =
                            ProductionOutputCapabilityIds.StandardDefinitionVersion,
                        outputComponentCodecId =
                            ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                        outputComponentCodecVersion =
                            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
                        outputCapabilityFingerprint =
                            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                                "output:qa-generic-terminal",
                                "material:qa:generic-terminal-output",
                                ProductionOutputCapabilityIds.StandardDefinition,
                                ProductionOutputCapabilityIds.StandardDefinitionVersion,
                                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
                        amount = 2,
                        committedAmount = 2,
                            committedMassGrams = 2_000L
                        }
                    }
                    : new List<ProductionResolvedOutputSaveData>(),
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
            if (source.outputOutcomeResolved)
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
                    suffix,
                    includeInputDestinationStock);
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

        internal static Fixture RestoreCurrentFormat(
            DungeonProductionBillSaveData production,
            IEnumerable<ProductionGenericBillTerminalDrainSaveData> producers,
            IEnumerable<ProductionInputDestinationCustodyDrainSaveData> children)
        {
            Fixture restored = new();
            foreach (ProductionBillSaveData bill in production?.bills
                         ?? new List<ProductionBillSaveData>())
            {
                restored.Persistence.Track(bill);
            }
            restored.Persistence.Restore(
                restored.Persistence.BuildRestore(production));
            Require(restored.Child.TryRestoreCurrentFormat(
                    children,
                    out string childFailure),
                "Current-format child restore failed: " + childFailure);
            Require(restored.Outbox.TryRestoreCurrentFormat(
                    producers,
                    out string producerFailure),
                "Current-format producer restore failed: " + producerFailure);
            return restored;
        }
    }

    private sealed class RootBackedBillPersistence :
        IProductionBillPersistence,
        IProductionGenericBillWipTerminalCheckpointGcPort
    {
        private readonly ProductionAggregateStateSession session;
        private readonly Dictionary<string, ProductionBillSaveData> byBillId =
            new(StringComparer.Ordinal);
        private WipCheckpointGcCandidate activeCheckpointCandidate;

        internal RootBackedBillPersistence(ProductionAggregateStateSession session) =>
            this.session = session ?? throw new ArgumentNullException(nameof(session));

        internal void Track(ProductionBillSaveData source) =>
            byBillId[source.billId] = ProductionGenericBillTerminalDrainCanonical
                .CloneBill(source);

        internal Action OnCheckpointPublished { get; set; }
        internal bool ThrowNextCheckpointPrepare { get; set; }
        internal bool FailNextCheckpointPublish { get; set; }

        internal bool HasWip(
            ProductionGenericBillTerminalDrainSaveData producer)
        {
            if (producer?.sourceBill == null
                || !ProductionGenericBillTerminalDrainCanonical
                    .TryCreateWipTerminalReceipt(
                        producer.sourceBill,
                        out ProductionWipTerminalReceiptSaveData expected,
                        out _))
                return false;
            return session.WipTerminalReceipts.Count(value =>
                    ProductionGenericBillTerminalDrainCanonical.WipReceiptEquals(
                        value,
                        expected)) == 1;
        }

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

        public bool TryPrepareCheckpointGarbageCollection(
            IReadOnlyList<ProductionGenericBillTerminalDrainSaveData> producers,
            out IProductionGenericBillWipTerminalCheckpointGcCandidate candidate,
            out string failureReason)
        {
            candidate = null;
            failureReason = string.Empty;
            if (ThrowNextCheckpointPrepare)
            {
                ThrowNextCheckpointPrepare = false;
                throw new InvalidOperationException(
                    "fixture-wip-checkpoint-injected-prepare-exception");
            }
            if (activeCheckpointCandidate != null)
            {
                failureReason = "fixture-wip-checkpoint-already-active";
                return false;
            }
            ProductionGenericBillTerminalDrainSaveData[] ordered = (producers
                    ?? Array.Empty<ProductionGenericBillTerminalDrainSaveData>())
                .OrderBy(value => value?.billId, StringComparer.Ordinal)
                .ToArray();
            if (ordered.Any(value => value == null)
                || ordered.Select(value => value.billId)
                    .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            {
                failureReason = "fixture-wip-checkpoint-producer-invalid";
                return false;
            }

            List<ProductionWipTerminalReceiptSaveData> rows = new();
            foreach (ProductionGenericBillTerminalDrainSaveData producer in ordered)
            {
                if (producer.phase != ProductionGenericBillTerminalDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc
                    || !ProductionGenericBillTerminalDrainCanonical.IsValidSave(
                        producer)
                    || !ProductionGenericBillTerminalDrainCanonical
                        .TryCreateWipTerminalReceipt(
                            producer.sourceBill,
                            out ProductionWipTerminalReceiptSaveData expected,
                            out failureReason)
                    || !string.Equals(
                        producer.wipTerminalCommitId,
                        expected.commitId,
                        StringComparison.Ordinal)
                    || session.WipTerminalReceipts.Count(value =>
                        ProductionGenericBillTerminalDrainCanonical.WipReceiptEquals(
                            value,
                            expected)) != 1)
                {
                    failureReason = string.IsNullOrEmpty(failureReason)
                        ? "fixture-wip-checkpoint-row-conflict"
                        : failureReason;
                    return false;
                }
                rows.Add(expected);
            }

            activeCheckpointCandidate = new WipCheckpointGcCandidate(
                session.BillVersion,
                rows);
            candidate = activeCheckpointCandidate;
            return true;
        }

        public bool TryPublishCheckpointGarbageCollection(
            IProductionGenericBillWipTerminalCheckpointGcCandidate candidate,
            out string failureReason)
        {
            failureReason = string.Empty;
            WipCheckpointGcCandidate exact = RequireCandidate(candidate);
            if (exact.Published)
                return true;
            if (FailNextCheckpointPublish)
            {
                FailNextCheckpointPublish = false;
                failureReason = "fixture-wip-checkpoint-injected-failure";
                return false;
            }
            if (session.BillVersion != exact.ExpectedVersion
                || exact.Rows.Any(expected => session.WipTerminalReceipts.Count(
                    value => ProductionGenericBillTerminalDrainCanonical
                        .WipReceiptEquals(value, expected)) != 1))
            {
                failureReason = "fixture-wip-checkpoint-live-drift";
                return false;
            }
            foreach (ProductionWipTerminalReceiptSaveData row in exact.Rows)
            {
                if (!session.TryRemoveWipTerminalReceiptExact(row))
                    throw new InvalidOperationException(
                        "Fixture WIP checkpoint row vanished during publish.");
            }
            if (exact.Rows.Count > 0)
                session.IncrementBillVersion();
            exact.PublishedVersion = session.BillVersion;
            exact.Published = true;
            OnCheckpointPublished?.Invoke();
            return true;
        }

        public void RollbackCheckpointGarbageCollection(
            IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
        {
            WipCheckpointGcCandidate exact = RequireCandidate(candidate);
            if (!exact.Published)
                return;
            if (session.BillVersion != exact.PublishedVersion
                || exact.Rows.Any(expected => session.WipTerminalReceipts.Any(
                    value => string.Equals(value?.commitId, expected.commitId,
                        StringComparison.Ordinal))))
            {
                throw new InvalidOperationException(
                    "Fixture WIP checkpoint rollback encountered authority drift.");
            }
            foreach (ProductionWipTerminalReceiptSaveData row in exact.Rows)
            {
                if (!session.AddWipTerminalReceipt(row))
                    throw new InvalidOperationException(
                        "Fixture WIP checkpoint rollback could not restore a row.");
            }
            if (exact.Rows.Count > 0
                && !session.TryRestoreBillVersionForCheckpointGc(
                    exact.PublishedVersion,
                    exact.ExpectedVersion))
            {
                throw new InvalidOperationException(
                    "Fixture WIP checkpoint rollback could not restore its version.");
            }
            exact.Published = false;
        }

        public void CompleteCheckpointGarbageCollection(
            IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
        {
            RequireCandidate(candidate);
            activeCheckpointCandidate = null;
        }

        private WipCheckpointGcCandidate RequireCandidate(
            IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
        {
            if (candidate is not WipCheckpointGcCandidate exact
                || !ReferenceEquals(exact, activeCheckpointCandidate))
            {
                throw new InvalidOperationException(
                    "Fixture WIP checkpoint candidate is stale or foreign.");
            }
            return exact;
        }

        private sealed class WipCheckpointGcCandidate :
            IProductionGenericBillWipTerminalCheckpointGcCandidate
        {
            internal WipCheckpointGcCandidate(
                int expectedVersion,
                IReadOnlyList<ProductionWipTerminalReceiptSaveData> rows)
            {
                ExpectedVersion = expectedVersion;
                PublishedVersion = expectedVersion;
                Rows = (rows ?? Array.Empty<ProductionWipTerminalReceiptSaveData>())
                    .Select(value => value.Clone())
                    .OrderBy(value => value.commitId, StringComparer.Ordinal)
                    .ToArray();
            }

            internal int ExpectedVersion { get; }
            internal int PublishedVersion { get; set; }
            internal IReadOnlyList<ProductionWipTerminalReceiptSaveData> Rows
                { get; }
            internal bool Published { get; set; }
        }
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
        IProductionInputDestinationCustodyDrainOutbox,
        IProductionInputDestinationCustodyDrainCheckpointGcPort
    {
        private readonly Dictionary<string,
            ProductionInputDestinationCustodyDrainSaveData> records =
            new(StringComparer.Ordinal);
        private ChildCheckpointGcCandidate activeCheckpointCandidate;

        internal Action<string> OnGarbageCollected { get; set; }
        internal Action OnCheckpointPublished { get; set; }
        internal Action OnCheckpointRolledBack { get; set; }
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

        internal bool TryRestoreCurrentFormat(
            IEnumerable<ProductionInputDestinationCustodyDrainSaveData> source,
            out string failureReason)
        {
            failureReason = string.Empty;
            ProductionInputDestinationCustodyDrainSaveData[] ordered = (source
                    ?? Array.Empty<
                        ProductionInputDestinationCustodyDrainSaveData>())
                .Select(value => value?.Clone())
                .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
                .ToArray();
            if (ordered.Any(value => value == null
                    || !ProductionInputDestinationCustodyDrainContract
                        .IsValidSave(value))
                || ordered.Select(value => value.stepOperationId)
                    .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            {
                failureReason = "fixture-child-current-format-invalid";
                return false;
            }
            records.Clear();
            foreach (ProductionInputDestinationCustodyDrainSaveData value in
                     ordered)
            {
                records.Add(value.stepOperationId, value);
            }
            return true;
        }

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

        public bool TryPrepareCheckpointGarbageCollection(
            IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> source,
            out IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                candidate,
            out string failureReason)
        {
            candidate = null;
            failureReason = string.Empty;
            if (activeCheckpointCandidate != null)
            {
                failureReason = "fixture-child-checkpoint-already-active";
                return false;
            }
            ProductionInputDestinationCustodyDrainSaveData[] expected = (source
                    ?? Array.Empty<ProductionInputDestinationCustodyDrainSaveData>())
                .Select(value => value?.Clone())
                .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
                .ToArray();
            if (expected.Any(value => value == null)
                || expected.Select(value => value.stepOperationId)
                    .Distinct(StringComparer.Ordinal).Count() != expected.Length
                || expected.Any(value => value.phase !=
                        ProductionInputDestinationCustodyDrainPhase
                            .BillAcknowledgedAwaitingCheckpointGc
                    || !ProductionInputDestinationCustodyDrainContract.IsValidSave(
                        value)
                    || !records.TryGetValue(value.stepOperationId, out var live)
                    || !RowsEqual(live, value)))
            {
                failureReason = "fixture-child-checkpoint-row-conflict";
                return false;
            }
            activeCheckpointCandidate = new ChildCheckpointGcCandidate(expected);
            candidate = activeCheckpointCandidate;
            return true;
        }

        public bool TryPublishCheckpointGarbageCollection(
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate,
            out string failureReason)
        {
            failureReason = string.Empty;
            ChildCheckpointGcCandidate exact = RequireCandidate(candidate);
            if (exact.Published)
                return true;
            if (exact.Rows.Any(value => !records.TryGetValue(
                    value.stepOperationId,
                    out var live) || !RowsEqual(live, value)))
            {
                failureReason = "fixture-child-checkpoint-live-drift";
                return false;
            }
            foreach (ProductionInputDestinationCustodyDrainSaveData row in
                     exact.Rows)
                records.Remove(row.stepOperationId);
            exact.Published = true;
            OnCheckpointPublished?.Invoke();
            return true;
        }

        public void RollbackCheckpointGarbageCollection(
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate)
        {
            ChildCheckpointGcCandidate exact = RequireCandidate(candidate);
            if (!exact.Published)
                return;
            if (exact.Rows.Any(value => records.ContainsKey(value.stepOperationId)))
            {
                throw new InvalidOperationException(
                    "Fixture child checkpoint rollback encountered authority drift.");
            }
            foreach (ProductionInputDestinationCustodyDrainSaveData row in
                     exact.Rows)
                records.Add(row.stepOperationId, row.Clone());
            exact.Published = false;
            OnCheckpointRolledBack?.Invoke();
        }

        public void CompleteCheckpointGarbageCollection(
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate)
        {
            RequireCandidate(candidate);
            activeCheckpointCandidate = null;
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

        private ChildCheckpointGcCandidate RequireCandidate(
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate candidate)
        {
            if (candidate is not ChildCheckpointGcCandidate exact
                || !ReferenceEquals(exact, activeCheckpointCandidate))
            {
                throw new InvalidOperationException(
                    "Fixture child checkpoint candidate is stale or foreign.");
            }
            return exact;
        }

        private static bool RowsEqual(
            ProductionInputDestinationCustodyDrainSaveData left,
            ProductionInputDestinationCustodyDrainSaveData right) => left != null
            && right != null
            && string.Equals(
                JsonUtility.ToJson(left),
                JsonUtility.ToJson(right),
                StringComparison.Ordinal);

        private sealed class ChildCheckpointGcCandidate :
            IProductionInputDestinationCustodyDrainCheckpointGcCandidate
        {
            internal ChildCheckpointGcCandidate(
                IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> rows)
            {
                Rows = (rows
                        ?? Array.Empty<
                            ProductionInputDestinationCustodyDrainSaveData>())
                    .Select(value => value.Clone())
                    .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
                    .ToArray();
            }

            internal IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
                Rows { get; }
            internal bool Published { get; set; }
        }
    }
}
#endif
