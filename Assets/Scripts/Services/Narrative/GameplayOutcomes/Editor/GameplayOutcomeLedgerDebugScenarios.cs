using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

public static class GameplayOutcomeLedgerDebugScenarios
{
    private static readonly GameplayOutcomeTypeId OutcomeType =
        new GameplayOutcomeTypeId("test.outcome");
    private static readonly GameplayEntityKindId CharacterKind =
        new GameplayEntityKindId("character");
    private static readonly GameplayRoleId ActorRole =
        new GameplayRoleId("actor");
    private static readonly GameplayMetricId AmountMetric =
        new GameplayMetricId("amount");
    private static readonly GameplayMetricUnitId CountUnit =
        new GameplayMetricUnitId("count");
    private static readonly GameplayOutcomeTagId TestTag =
        new GameplayOutcomeTagId("test");
    private static readonly GameplayOutcomeFactId DetailFact =
        new GameplayOutcomeFactId("detail");

    public static string RunAll()
    {
        RegistryFailsClosed();
        LifecycleReplayFaultAndRestore();
        OptionalWitnessFiltering();
        CompactionAndGlobalDeduplication();
        ForgettingAndTerminalReplay();
        TransactionalEvidenceAnchors();
        TransactionalInfluenceUse();
        PersistedEvidenceBindingsFailClosedWithoutCloneRepair();
        SliceAndQueuedDayDeterminism();
        MidConsolidationSaveRestartsFromExactInput();
        RetainedHistoryExceedsHotPool();
        KnownResultCapacityBackpressuresBeforeMutation();
        RetentionPromotionFailureIsExplicit();
        return "PASS GameplayOutcomeLedgerDebugScenarios";
    }

    private static void PersistedEvidenceBindingsFailClosedWithoutCloneRepair()
    {
        GameplayOutcomeEvidenceBindingSnapshot corrupt = new GameplayOutcomeEvidenceBindingSnapshot
        {
            publicFactId = "fact:corrupt",
            outcomeRunId = "run:corrupt",
            outcomeSequence = -1L,
            outcomeTypeId = "test.outcome",
            subjectKindId = "character",
            subjectId = "character:corrupt",
            anchorRevision = -2,
            status = int.MaxValue,
            subjectSalience = float.NaN,
            influenceUseCount = -3,
            influenceRevision = -4,
            canonicalFactText = "손상된 저장 근거",
            roleIds = new List<string> { "actor" },
            metricIds = new List<string>(),
            metricReferenceIds = new List<string>(),
            factIds = new List<string>(),
            semanticTags = new List<string>()
        };
        GameplayOutcomeEvidenceBindingSnapshot clone = corrupt.Clone();
        Require(
            clone.outcomeSequence == -1L
            && clone.anchorRevision == -2
            && clone.status == int.MaxValue
            && float.IsNaN(clone.subjectSalience)
            && clone.influenceUseCount == -3
            && clone.influenceRevision == -4,
            "Persisted evidence Clone must preserve raw corrupt values for validation.");
        Require(
            !GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                clone,
                out string failureCode)
            && failureCode == "binding-revision-invalid",
            "Persisted evidence corruption must fail closed before mechanic use.");

        clone.outcomeSequence = 1L;
        clone.anchorRevision = 0;
        clone.influenceUseCount = 0;
        clone.influenceRevision = 0;
        Require(
            !GameplayOutcomeEvidenceBindingAuthority.TryValidate(clone, out failureCode)
            && failureCode == "binding-salience-invalid",
            "NaN evidence salience must fail closed.");
        clone.subjectSalience = 0.5f;
        Require(
            !GameplayOutcomeEvidenceBindingAuthority.TryValidate(clone, out failureCode)
            && failureCode == "binding-status-invalid",
            "Unknown persisted outcome status must fail closed.");
    }

    private static void RegistryFailsClosed()
    {
        TestDescriptor descriptor = new TestDescriptor(NarrativeMemoryTier.Episodic, 0, false);
        TestAdapter adapter = new TestAdapter();
        RequireThrows<InvalidOperationException>(
            () => new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { descriptor },
                Array.Empty<IGameplayOutcomeAdapterRegistration>()),
            "A descriptor without an adapter must fail startup.");
        RequireThrows<InvalidOperationException>(
            () => new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { descriptor, descriptor },
                new IGameplayOutcomeAdapterRegistration[] { adapter }),
            "Duplicate outcome descriptors must fail startup.");
        RequireThrows<InvalidOperationException>(
            () => new GameplayOutcomeRegistry(
                Array.Empty<IGameplayOutcomeDescriptor>(),
                new IGameplayOutcomeAdapterRegistration[] { adapter }),
            "An adapter without a descriptor must fail startup.");
    }

    private static void LifecycleReplayFaultAndRestore()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        TestReceipt cancelled = Receipt(1, 1, 7d);
        OutcomePrepareResult prepared = fixture.Recorder.TryPrepare(cancelled, out PreparedOutcomeToken cancelledToken);
        Require(prepared.Success, "Initial preparation failed.");
        RequireThrows<InvalidOperationException>(
            () => fixture.Ledger.CaptureGameplayOutcomes(),
            "Save capture must reject an active outcome reservation.");
        fixture.Recorder.CancelPrepared(cancelledToken);

        TestReceipt receipt = Receipt(2, 2, 12.5d);
        OutcomePrepareResult prepare = fixture.Recorder.TryPrepare(receipt, out PreparedOutcomeToken token);
        Require(prepare.Success, "Receipt preparation failed.");
        OutcomeCommitResult wrongRevision = fixture.Recorder.CommitPrepared(
            token, receipt.OwnerRevision + 1L, out _);
        Require(
            wrongRevision.Code == OutcomeCommitCode.OwnerRevisionMismatch,
            "Owner revision mismatch must not commit the outbox.");
        OutcomeCommitResult commit = fixture.Recorder.CommitPrepared(
            token, receipt.OwnerRevision, out CommittedOutcomeToken committed);
        Require(commit.Code == OutcomeCommitCode.Committed, "Receipt commit failed.");

        fixture.Descriptor.ThrowOnValidate = true;
        OutcomeDeliveryResult fault = fixture.Recorder.TryDeliver(committed);
        Require(
            fault.Code == OutcomeDeliveryCode.PendingDeliveryFault
            && fixture.Ledger.GetOutboxSnapshot().Count == 1,
            "A delivery fault must remain in the durable outbox.");
        fixture.Descriptor.ThrowOnValidate = false;
        Require(
            fixture.Recorder.RetryPendingDeliveries(1) == 1,
            "Durable delivery retry did not publish and acknowledge the result.");

        GameplayOutcomeLedgerDiagnostics diagnostics = fixture.Ledger.GetDiagnostics();
        Require(
            diagnostics.FreeSmallPages == 1
            && diagnostics.DetachedExactCount == 1
            && diagnostics.PendingDeliveryCount == 0,
            "Acknowledgement must recycle the hot page and retain detached exact history.");
        Require(
            fixture.Ledger.TryGetResultIdentity(receipt.ResultKey, out GameplayOutcomeReplayIdentity identity)
            && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
            && identity.HasCanonicalPayloadHash,
            "Published replay identity was not retained.");
        Require(
            fixture.Ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
            && exact.metrics.Count == 1
            && exact.facts.Count == 1
            && exact.provenance.Count == 1
            && exact.participants[0].displayText == "민수"
            && exact.metrics[0].value == 12.5d,
            "Exact query lost committed receipt facts.");
        Require(
            fixture.Ledger.TryProject(
                identity.OutcomeId,
                new NarrativePerspectiveContext(receipt.ActorId, NarrativePerspectiveKind.Character, "ko-KR"),
                out NarrativeView narrative)
            && narrative.Text.Contains("민수", StringComparison.Ordinal)
            && narrative.Text.Contains("12.5", StringComparison.Ordinal),
            "Perspective projection did not use captured display and metric facts.");

        OutcomePrepareResult identical = fixture.Recorder.TryPrepare(receipt, out _);
        Require(
            identical.Code == OutcomePrepareCode.AlreadyPublished
            && identical.Existing.CanonicalPayloadHash == identity.CanonicalPayloadHash,
            "Identical replay must reconcile to the canonical published result.");
        TestReceipt conflicting = receipt.WithAmount(13.5d);
        OutcomePrepareResult conflict = fixture.Recorder.TryPrepare(conflicting, out _);
        Require(
            conflict.Code == OutcomePrepareCode.ConflictingResult,
            "A reused result key with a different immutable payload must fail closed.");

        GameplayOutcomeLedgerSaveData save = fixture.Ledger.CaptureGameplayOutcomes();
        Require(save.exactOutcomes.Count == 1, "Acknowledged exact history was not saved.");
        string validHash = save.exactOutcomes[0].immutablePayloadHash;
        save.exactOutcomes[0].immutablePayloadHash = FlipFirstHex(validHash);
        GameplayOutcomeLedger tamperTarget = NewLedger(fixture.Registry, fixture.Limits, "run:tamper");
        RequireThrows<InvalidOperationException>(
            () => tamperTarget.PrepareGameplayOutcomeRestore(save),
            "Tampered immutable receipt hash must be rejected before publication.");
        save.exactOutcomes[0].immutablePayloadHash = validHash;

        GameplayOutcomeLedger restored = NewLedger(fixture.Registry, fixture.Limits, "run:restore");
        GameplayOutcomeLedgerRestoreCandidate candidate = restored.PrepareGameplayOutcomeRestore(save);
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.CurrentWorldEpoch == fixture.Ledger.CurrentWorldEpoch + 1L
            && restored.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot restoredExact)
            && restoredExact.immutablePayloadHash == validHash,
            "Staged save restore did not preserve the exact result and advance world epoch.");

        GameEventBus throwingBus = new GameEventBus();
        Fixture notificationFixture = CreateFixture(
            NarrativeMemoryTier.Episodic,
            witnessLimit: 0,
            eventBus: throwingBus);
        throwingBus.Subscribe<GameplayOutcomePublishedRangeEvent>(_ =>
            throw new InvalidOperationException("injected-notification-fault"));
        Publish(notificationFixture, Receipt(3, 3, 1d));
        Require(
            notificationFixture.Ledger.GetDiagnostics().NotificationFaultCount == 1,
            "Presentation notification faults must be isolated after ledger publication.");
    }

    private static void OptionalWitnessFiltering()
    {
        Fixture fixture = CreateFixture(
            NarrativeMemoryTier.Episodic,
            witnessLimit: 0,
            acceptOptionalWitnesses: false);
        TestReceipt receipt = Receipt(10, 10, 3d).WithSecond(
            new GameplayEntityId(CharacterKind, "witness-10"),
            "유리",
            GameplayParticipationKind.OptionalWitness);
        GameplayOutcomeId outcomeId = Publish(fixture, receipt);
        Require(
            fixture.Ledger.TryGetExact(outcomeId, out GameplayOutcomeSnapshot snapshot)
            && snapshot.participants.Count == 2
            && snapshot.subjects.Count == 1
            && snapshot.subjects[0].entityId == receipt.ActorId.Value,
            "Perception filtering must keep exact participants but avoid an unperceived witness memory link.");
    }

    private static void CompactionAndGlobalDeduplication()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Compacted, witnessLimit: 0);
        TestReceipt receipt = Receipt(20, 20, 5d).WithSecond(
            new GameplayEntityId(CharacterKind, "target-20"),
            "민혁",
            GameplayParticipationKind.Direct);
        GameplayOutcomeId outcomeId = Publish(fixture, receipt);
        Schedule(fixture, 1, outcomeId.Sequence);
        Drain(fixture, 1);

        Require(!fixture.Ledger.TryGetExact(outcomeId, out _), "Fully compacted exact input must be retired.");
        Require(
            fixture.Ledger.TryGetResultIdentity(receipt.ResultKey, out GameplayOutcomeReplayIdentity identity)
            && identity.State == GameplayOutcomeReplayState.Compacted,
            "Compacted replay tombstone was not retained.");
        GameplayOutcomeQueryPage global = fixture.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(1), OutcomeFilter.All);
        Require(
            global.Items.Count == 1
            && global.Items[0].IsCompacted
            && global.Items[0].Compacted.occurrenceCount == 1,
            "Global history must deduplicate the two subject memories of one outcome.");
        Require(
            fixture.Ledger.GetForEntity(
                receipt.ActorId, OutcomeCursor.FirstPage(1), OutcomeFilter.All).Items.Count == 1
            && fixture.Ledger.GetForEntity(
                receipt.SecondId, OutcomeCursor.FirstPage(1), OutcomeFilter.All).Items.Count == 1,
            "Both direct subjects must retain their own compacted memory link.");
        GameplayOutcomeLedgerSaveData save = fixture.Ledger.CaptureGameplayOutcomes();
        Require(
            save.exactOutcomes.Count == 0
            && save.compactedMemories.Count == 2
            && save.tombstones.Count == 1,
            "Compacted save state must preserve two subject memories and one result tombstone.");
        string validCompactedHash = save.compactedMemories[0].compactedPayloadHash;
        save.compactedMemories[0].compactedPayloadHash = FlipFirstHex(validCompactedHash);
        GameplayOutcomeLedger tamperTarget = NewLedger(fixture.Registry, fixture.Limits, "run:compact-tamper");
        RequireThrows<InvalidOperationException>(
            () => tamperTarget.PrepareGameplayOutcomeRestore(save),
            "Tampered compacted payload hash must be rejected before publication.");
        save.compactedMemories[0].compactedPayloadHash = validCompactedHash;
    }

    private static void ForgettingAndTerminalReplay()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Forgotten, witnessLimit: 0);
        TestReceipt receipt = Receipt(30, 30, 2d);
        GameplayOutcomeId outcomeId = Publish(fixture, receipt);
        Schedule(fixture, 1, outcomeId.Sequence);
        Drain(fixture, 2);

        Require(
            fixture.Ledger.GetGlobal(OutcomeCursor.FirstPage(), OutcomeFilter.All).Items.Count == 0,
            "Forgotten history must not appear in global queries.");
        Require(
            fixture.Ledger.TryGetResultIdentity(receipt.ResultKey, out GameplayOutcomeReplayIdentity identity)
            && identity.State == GameplayOutcomeReplayState.Forgotten,
            "Forgotten replay tombstone was not retained.");
        Require(
            fixture.Recorder.TryPrepare(receipt, out _).Code == OutcomePrepareCode.AlreadyTerminal,
            "Identical forgotten replay must remain terminal and idempotent.");
        Require(
            fixture.Recorder.TryPrepare(receipt.WithAmount(9d), out _).Code
                == OutcomePrepareCode.ConflictingResult,
            "A forgotten key must still reject conflicting payload reuse.");
    }

    private static void TransactionalEvidenceAnchors()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Forgotten, witnessLimit: 0);
        TestReceipt receipt = Receipt(40, 40, 4d);
        GameplayOutcomeId outcomeId = Publish(fixture, receipt);
        NarrativeEvidenceReference anchor = new NarrativeEvidenceReference("skill", "skill-40");

        EvidenceAnchorPrepareResult prepared = fixture.Ledger.TryPrepareEvidenceAnchorMutation(
            outcomeId,
            receipt.ActorId,
            anchor,
            EvidenceAnchorMutationKind.Add,
            0,
            out PreparedEvidenceAnchorToken token);
        Require(prepared.Code == EvidenceAnchorPrepareCode.Prepared, "Evidence anchor prepare failed.");
        RequireThrows<InvalidOperationException>(
            () => fixture.Ledger.CaptureGameplayOutcomes(),
            "Save must reject an active evidence-anchor reservation.");
        fixture.Ledger.CancelEvidenceAnchorMutation(token);

        prepared = fixture.Ledger.TryPrepareEvidenceAnchorMutation(
            outcomeId,
            receipt.ActorId,
            anchor,
            EvidenceAnchorMutationKind.Add,
            0,
            out token);
        Require(prepared.Success, "Evidence anchor re-prepare failed.");
        EvidenceAnchorCommitResult added = fixture.Ledger.CommitEvidenceAnchorMutation(
            token, out EvidenceAnchorRollbackToken rollback);
        Require(added.Success && rollback.IsValid, "Evidence anchor commit failed.");
        GameplayOutcomeLedgerSaveData anchoredSave = fixture.Ledger.CaptureGameplayOutcomes();
        string validAnchorSubject = anchoredSave.exactOutcomes[0].anchors[0].subjectId;
        anchoredSave.exactOutcomes[0].anchors[0].subjectId = "missing-subject";
        GameplayOutcomeLedger brokenAnchorTarget = NewLedger(
            fixture.Registry,
            fixture.Limits,
            "run:anchor-tamper");
        RequireThrows<InvalidOperationException>(
            () => brokenAnchorTarget.PrepareGameplayOutcomeRestore(anchoredSave),
            "A restored anchor without a matching subject link must fail atomically.");
        anchoredSave.exactOutcomes[0].anchors[0].subjectId = validAnchorSubject;
        Schedule(fixture, 1, outcomeId.Sequence);
        Drain(fixture, 2);
        Require(
            fixture.Ledger.TryGetExact(outcomeId, out GameplayOutcomeSnapshot anchored)
            && anchored.subjects[0].tier == NarrativeMemoryTier.Core,
            "Anchored evidence must remain exact Core history.");

        EvidenceAnchorPrepareResult removal = fixture.Ledger.TryPrepareEvidenceAnchorMutation(
            outcomeId,
            receipt.ActorId,
            anchor,
            EvidenceAnchorMutationKind.Remove,
            1,
            out PreparedEvidenceAnchorToken removeToken);
        Require(removal.Success, "Evidence anchor removal prepare failed.");
        Require(
            fixture.Ledger.CommitEvidenceAnchorMutation(removeToken, out _).Success,
            "Evidence anchor removal commit failed.");
        Require(
            fixture.Ledger.TrySetPlayerPin(outcomeId, receipt.ActorId, true, out string pinFailure),
            "Player pin failed: " + pinFailure);
        Schedule(fixture, 2, outcomeId.Sequence);
        Drain(fixture, 2);
        Require(
            fixture.Ledger.TryGetExact(outcomeId, out GameplayOutcomeSnapshot pinned)
            && pinned.subjects[0].tier == NarrativeMemoryTier.Core,
            "Player pin must preserve exact Core history after the final evidence anchor is removed.");
        Require(
            fixture.Ledger.TrySetPlayerPin(outcomeId, receipt.ActorId, false, out string unpinFailure),
            "Player unpin failed: " + unpinFailure);
        Schedule(fixture, 3, outcomeId.Sequence);
        Drain(fixture, 2);
        Require(
            fixture.Ledger.TryGetResultIdentity(receipt.ResultKey, out GameplayOutcomeReplayIdentity terminal)
            && terminal.State == GameplayOutcomeReplayState.Forgotten,
            "Removing the final anchor and pin must return the memory to deterministic forgetting.");
    }

    private static void TransactionalInfluenceUse()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        TestReceipt firstReceipt = Receipt(41, 41, 4d);
        GameplayOutcomeId firstOutcome = Publish(fixture, firstReceipt);

        InfluenceUsePrepareResult cancelled = fixture.Ledger.TryPrepareInfluenceUse(
            firstOutcome,
            firstReceipt.ActorId,
            0,
            0,
            out PreparedInfluenceUseToken cancelledToken);
        Require(cancelled.Success, "Influence-use prepare failed.");
        RequireThrows<InvalidOperationException>(
            () => fixture.Ledger.CaptureGameplayOutcomes(),
            "Save must reject an active influence-use reservation.");
        fixture.Ledger.CancelInfluenceUse(cancelledToken);
        Require(
            fixture.Ledger.TryGetExact(firstOutcome, out GameplayOutcomeSnapshot afterCancel)
            && afterCancel.subjects[0].influenceUseCount == 0
            && afterCancel.subjects[0].influenceRevision == 0,
            "Cancelling influence use must not consume the narrative fact.");

        InfluenceUsePrepareResult rollbackPrepared = fixture.Ledger.TryPrepareInfluenceUse(
            firstOutcome,
            firstReceipt.ActorId,
            0,
            0,
            out PreparedInfluenceUseToken rollbackPreparedToken);
        Require(rollbackPrepared.Success, "Influence-use rollback prepare failed.");
        InfluenceUseCommitResult rollbackCommitted = fixture.Ledger.CommitInfluenceUse(
            rollbackPreparedToken,
            out InfluenceUseRollbackToken rollbackToken);
        Require(rollbackCommitted.Success && rollbackToken.IsValid,
            "Influence-use provisional commit failed.");
        RequireThrows<InvalidOperationException>(
            () => fixture.Ledger.CaptureGameplayOutcomes(),
            "Save must remain blocked between influence increment and owner terminal commit.");
        Require(fixture.Ledger.RollbackInfluenceUse(rollbackToken).Success,
            "Influence-use rollback failed.");
        Require(
            fixture.Ledger.TryGetExact(firstOutcome, out GameplayOutcomeSnapshot afterRollback)
            && afterRollback.subjects[0].influenceUseCount == 0
            && afterRollback.subjects[0].influenceRevision == 0,
            "Rolling back the owner candidate must restore influence count and revision.");

        InfluenceUsePrepareResult committedPrepared = fixture.Ledger.TryPrepareInfluenceUse(
            firstOutcome,
            firstReceipt.ActorId,
            0,
            0,
            out PreparedInfluenceUseToken committedPreparedToken);
        Require(committedPrepared.Success, "Influence-use commit prepare failed.");
        Require(
            fixture.Ledger.CommitInfluenceUse(
                committedPreparedToken,
                out InfluenceUseRollbackToken committedToken).Success,
            "Influence-use commit failed.");
        Require(
            fixture.Ledger.CompleteInfluenceUses(new[] { committedToken }).Success,
            "Influence-use terminal completion failed.");
        GameplayOutcomeLedgerSaveData oneUseSave = fixture.Ledger.CaptureGameplayOutcomes();
        Require(
            fixture.Ledger.TryGetExact(firstOutcome, out GameplayOutcomeSnapshot oneUse)
            && oneUse.subjects[0].influenceUseCount == 1
            && oneUse.subjects[0].influenceRevision == 1
            && oneUse.anchors.Count == 0,
            "Influence use must be durable and independent from retention anchors.");
        Require(
            fixture.Ledger.TryPrepareInfluenceUse(
                firstOutcome,
                firstReceipt.ActorId,
                0,
                0,
                out _).Code == InfluenceUsePrepareCode.Stale,
            "A stale influence revision must fail closed.");

        GameplayOutcomeLedger restored = NewLedger(
            fixture.Registry,
            fixture.Limits,
            "run:influence-restore");
        GameplayOutcomeLedgerRestoreCandidate restoreCandidate =
            restored.PrepareGameplayOutcomeRestore(oneUseSave);
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(restoreCandidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.TryGetExact(firstOutcome, out GameplayOutcomeSnapshot restoredUse)
            && restoredUse.subjects[0].influenceUseCount == 1
            && restoredUse.subjects[0].influenceRevision == 1,
            "Save/restore must preserve influence use separately from anchors.");

        InfluenceUsePrepareResult secondPrepared = restored.TryPrepareInfluenceUse(
            firstOutcome,
            firstReceipt.ActorId,
            1,
            1,
            out PreparedInfluenceUseToken secondPreparedToken);
        Require(secondPrepared.Success, "Second influence-use prepare failed.");
        Require(
            restored.CommitInfluenceUse(
                secondPreparedToken,
                out InfluenceUseRollbackToken secondCommittedToken).Success
            && restored.CompleteInfluenceUses(new[] { secondCommittedToken }).Success
            && restored.TryGetExact(firstOutcome, out GameplayOutcomeSnapshot twoUses)
            && twoUses.subjects[0].influenceUseCount == 2
            && twoUses.subjects[0].influenceRevision == 2,
            "Repeated confirmed use must advance influence count 0->1->2.");

        TestReceipt secondReceipt = Receipt(42, 42, 5d);
        GameplayOutcomeId secondOutcome = Publish(fixture, secondReceipt);
        Require(
            fixture.Ledger.TryPrepareInfluenceUse(
                firstOutcome,
                firstReceipt.ActorId,
                1,
                1,
                out PreparedInfluenceUseToken batchFirstPrepared).Success,
            "First batch influence prepare failed.");
        Require(
            fixture.Ledger.CommitInfluenceUse(
                batchFirstPrepared,
                out InfluenceUseRollbackToken batchFirst).Success,
            "First batch influence commit failed.");
        Require(
            fixture.Ledger.TryPrepareInfluenceUse(
                secondOutcome,
                secondReceipt.ActorId,
                0,
                0,
                out PreparedInfluenceUseToken batchSecondPrepared).Success,
            "Second batch influence prepare failed.");
        Require(
            fixture.Ledger.CommitInfluenceUse(
                batchSecondPrepared,
                out InfluenceUseRollbackToken batchSecond).Success,
            "Second batch influence commit failed.");
        Require(fixture.Ledger.RollbackInfluenceUse(batchSecond).Success,
            "Injected second-token invalidation failed.");
        Require(
            !fixture.Ledger.CompleteInfluenceUses(new[] { batchFirst, batchSecond }).Success,
            "Batch completion must reject a stale member.");
        RequireThrows<InvalidOperationException>(
            () => fixture.Ledger.CaptureGameplayOutcomes(),
            "Failed batch completion must not partially release earlier guards.");
        Require(fixture.Ledger.RollbackInfluenceUse(batchFirst).Success,
            "Atomic batch failure must leave the first token rollbackable.");
        Require(
            fixture.Ledger.TryGetExact(firstOutcome, out GameplayOutcomeSnapshot afterBatchFailure)
            && afterBatchFailure.subjects[0].influenceUseCount == 1
            && fixture.Ledger.TryGetExact(secondOutcome, out GameplayOutcomeSnapshot secondAfterFailure)
            && secondAfterFailure.subjects[0].influenceUseCount == 0,
            "Failed multi-evidence completion must consume neither provisional use.");
    }

    private static void MidConsolidationSaveRestartsFromExactInput()
    {
        Fixture source = CreateFixture(NarrativeMemoryTier.Compacted, witnessLimit: 0);
        TestReceipt receipt = Receipt(50, 50, 8d).WithSecond(
            new GameplayEntityId(CharacterKind, "target-50"),
            "강철",
            GameplayParticipationKind.Direct);
        GameplayOutcomeId outcomeId = Publish(source, receipt);
        Schedule(source, 1, outcomeId.Sequence);
        GameplayOutcomeConsolidationSliceResult partial = source.Ledger.ProcessSlice(1, 1L);
        Require(partial.HasBacklog, "Tiny time slice must leave a resumable consolidation backlog.");
        GameplayOutcomeLedgerSaveData save = source.Ledger.CaptureGameplayOutcomes();

        GameplayOutcomeLedger restored = NewLedger(source.Registry, source.Limits, "run:mid-save");
        GameplayOutcomeLedgerRestoreCandidate candidate = restored.PrepareGameplayOutcomeRestore(save);
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Drain(restored, 1);
        Require(
            restored.GetGlobal(OutcomeCursor.FirstPage(), OutcomeFilter.All).Items.Count == 1
            && restored.GetDiagnostics().CompactedCount == 2,
            "Restore during sliced consolidation must restart from exact input without partial publication.");
    }

    private static void SliceAndQueuedDayDeterminism()
    {
        Fixture oneAtATime = CreateFixture(NarrativeMemoryTier.Compacted, witnessLimit: 0);
        Fixture oneBatch = CreateFixture(NarrativeMemoryTier.Compacted, witnessLimit: 0);
        long dayOneCutoff = 0L;
        long dayTwoCutoff = 0L;
        for (int index = 1; index <= 4; index++)
        {
            TestReceipt receipt = Receipt(600 + index, 600 + index, index);
            GameplayOutcomeId first = Publish(oneAtATime, receipt);
            GameplayOutcomeId second = Publish(oneBatch, receipt);
            Require(first == second, "Identical runs must assign identical outcome IDs.");
            if (index == 2)
                dayOneCutoff = first.Sequence;
            dayTwoCutoff = first.Sequence;
        }

        // Day 2 is deliberately queued before the day-1 backlog has consumed
        // its due snapshot. Queue-head snapshotting must make backlog depth
        // irrelevant to the final result.
        Schedule(oneAtATime, 1, dayOneCutoff);
        Schedule(oneAtATime, 2, dayTwoCutoff);
        Schedule(oneBatch, 1, dayOneCutoff);
        Schedule(oneBatch, 2, dayTwoCutoff);
        Drain(oneAtATime, 1);
        Drain(oneBatch, 128);

        GameplayOutcomeLedgerSaveData sliced = oneAtATime.Ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedgerSaveData batched = oneBatch.Ledger.CaptureGameplayOutcomes();
        Require(
            sliced.exactOutcomes.Count == batched.exactOutcomes.Count
            && sliced.compactedMemories.Count == batched.compactedMemories.Count
            && sliced.tombstones.Count == batched.tombstones.Count
            && sliced.consolidationJobs.Count == 0
            && batched.consolidationJobs.Count == 0,
            "Slice sizes produced different terminal collection shapes.");
        for (int index = 0; index < sliced.compactedMemories.Count; index++)
        {
            Require(
                sliced.compactedMemories[index].compactedPayloadHash
                    == batched.compactedMemories[index].compactedPayloadHash
                && sliced.compactedMemories[index].sourceSegmentHash
                    == batched.compactedMemories[index].sourceSegmentHash,
                "Slice size changed compacted memory hashes.");
        }
        for (int index = 0; index < sliced.tombstones.Count; index++)
        {
            Require(
                sliced.tombstones[index].canonicalHash == batched.tombstones[index].canonicalHash
                && sliced.tombstones[index].sequence == batched.tombstones[index].sequence,
                "Queued-day processing changed terminal replay identity.");
        }
    }

    private static void RetainedHistoryExceedsHotPool()
    {
        Fixture fixture = CreateFixture(
            NarrativeMemoryTier.Core,
            witnessLimit: 0,
            limits: new GameplayOutcomeBufferLimits(
                smallPageCount: 1,
                largePageCount: 0,
                knownResultKeyCapacity: 512));
        long lastSequence = 0L;
        for (int index = 1; index <= 200; index++)
            lastSequence = Publish(fixture, Receipt(1000 + index, index, index)).Sequence;

        GameplayOutcomeLedgerDiagnostics before = fixture.Ledger.GetDiagnostics();
        Require(
            before.FreeSmallPages == 1
            && before.DetachedExactCount == 200
            && before.CapacityDeferredCount == 0L,
            "Two hundred retained exact rows must not exhaust the one-page hot capture pool.");
        Schedule(fixture, 200, lastSequence);
        Drain(fixture, 128);
        GameplayOutcomeLedgerDiagnostics after = fixture.Ledger.GetDiagnostics();
        Require(
            after.PublishedCount == 200
            && after.DetachedExactCount == 200
            && fixture.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(200), OutcomeFilter.All).Items.Count == 200,
            "Long-run Core retention lost exact rows after deterministic evaluation.");
    }

    private static void KnownResultCapacityBackpressuresBeforeMutation()
    {
        Fixture fixture = CreateFixture(
            NarrativeMemoryTier.Core,
            witnessLimit: 0,
            limits: new GameplayOutcomeBufferLimits(
                smallPageCount: 1,
                largePageCount: 0,
                knownResultKeyCapacity: 2,
                retainedSmallPageCount: 4,
                retainedLargePageCount: 0));
        Publish(fixture, Receipt(2001, 1, 1));
        Publish(fixture, Receipt(2002, 2, 2));

        OutcomePrepareResult saturated = fixture.Recorder.TryPrepare(
            Receipt(2003, 3, 3),
            out PreparedOutcomeToken token);
        GameplayOutcomeLedgerDiagnostics diagnostics = fixture.Ledger.GetDiagnostics();
        Require(
            !token.IsValid
            && saturated.Code == OutcomePrepareCode.CapacityDeferred
            && saturated.DetailCode == "known-result-key-capacity-deferred"
            && diagnostics.KnownResultKeyCount == 2
            && diagnostics.KnownResultKeyMaximum == 2
            && diagnostics.KnownResultKeyHighWater == 2
            && diagnostics.FreeSmallPages == 1
            && diagnostics.FreeRetainedSmallPages == 2,
            "Known-result-key saturation must fail before page/retained capacity mutation.");
    }

    private static void RetentionPromotionFailureIsExplicit()
    {
        GameplayOutcomeBufferLimits limits = new GameplayOutcomeBufferLimits(
            smallPageCount: 1,
            largePageCount: 0,
            knownResultKeyCapacity: 2,
            retainedSmallPageCount: 1,
            retainedLargePageCount: 0);
        GameplayOutcomeValuePagePool pool = new GameplayOutcomeValuePagePool(limits);
        OutcomeWriteRequirements requirements = new OutcomeWriteRequirements(
            new GameplayResultKey(
                "test-producer",
                new GameplayOperationId("promotion-failure"),
                1,
                0),
            OutcomeType,
            1,
            GameplayOutcomeStatus.Succeeded,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            0);
        Require(pool.TryRent(requirements, out int pageIndex, out GameplayOutcomeValuePage page),
            "Promotion failure fixture could not reserve a page.");
        page.State = GameplayOutcomePageState.PublishedAcknowledged;
        page.RetainedReservationSlot = -1;
        Require(
            !pool.TryPromotePublishedToDetached(
                pageIndex,
                page.Generation,
                out _,
                out _)
            && page.State == GameplayOutcomePageState.PublishedAcknowledged
            && !new OutcomeAcknowledgeResult(
                OutcomeAcknowledgeCode.RetentionPromotionDeferred).Success,
            "Retention promotion failure must remain explicit and non-successful.");
    }

    private static Fixture CreateFixture(
        NarrativeMemoryTier tier,
        int witnessLimit,
        bool acceptOptionalWitnesses = true,
        GameplayOutcomeBufferLimits limits = null,
        IGameEventBus eventBus = null)
    {
        TestDescriptor descriptor = new TestDescriptor(tier, witnessLimit, acceptOptionalWitnesses);
        TestAdapter adapter = new TestAdapter();
        GameplayOutcomeRegistry registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { descriptor },
            new IGameplayOutcomeAdapterRegistration[] { adapter });
        GameplayOutcomeBufferLimits selectedLimits = limits ?? new GameplayOutcomeBufferLimits(
            smallPageCount: 1,
            largePageCount: 0,
            knownResultKeyCapacity: 512);
        GameplayOutcomeLedger ledger = NewLedger(registry, selectedLimits, "run:test");
        GameplayOutcomeRecorder recorder = new GameplayOutcomeRecorder(
            ledger,
            registry,
            eventBus ?? new GameEventBus());
        return new Fixture(registry, descriptor, selectedLimits, ledger, recorder);
    }

    private static GameplayOutcomeLedger NewLedger(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeBufferLimits limits,
        string runId) =>
        new GameplayOutcomeLedger(registry, limits, new GameplayOutcomeRunId(runId), 1L);

    private static GameplayOutcomeId Publish(Fixture fixture, TestReceipt receipt)
    {
        OutcomePrepareResult prepared = fixture.Recorder.TryPrepare(
            receipt, out PreparedOutcomeToken preparedToken);
        Require(prepared.Success, "Publish helper could not prepare: " + prepared.DetailCode);
        OutcomeCommitResult committed = fixture.Recorder.CommitPrepared(
            preparedToken, receipt.OwnerRevision, out CommittedOutcomeToken committedToken);
        Require(committed.Success, "Publish helper could not commit: " + committed.DetailCode);
        OutcomeDeliveryResult delivered = fixture.Recorder.TryDeliver(committedToken);
        Require(delivered.Published, "Publish helper could not deliver: " + delivered.DetailCode);
        Require(fixture.Recorder.Acknowledge(committedToken).Success, "Publish helper could not acknowledge.");
        return delivered.OutcomeId;
    }

    private static void Schedule(Fixture fixture, int day, long cutoff)
    {
        Require(
            fixture.Ledger.TrySchedule(
                new GameplayOutcomeConsolidationRequest(
                    day, cutoff, fixture.Registry.ConsolidationPolicyVersion, fixture.Ledger.CurrentWorldEpoch),
                out string failure),
            "Consolidation schedule failed: " + failure);
    }

    private static void Drain(Fixture fixture, int maximumRecords) =>
        Drain(fixture.Ledger, maximumRecords);

    private static void Drain(GameplayOutcomeLedger ledger, int maximumRecords)
    {
        int guard = 100000;
        while (ledger.PendingJobCount > 0 && guard-- > 0)
            ledger.ProcessSlice(maximumRecords, 0L);
        Require(guard > 0 && ledger.PendingJobCount == 0, "Consolidation did not drain deterministically.");
    }

    private static TestReceipt Receipt(int operation, long ownerRevision, double amount)
    {
        GameplayEntityId actor = new GameplayEntityId(CharacterKind, "actor-" + operation);
        return new TestReceipt(
            new GameplayResultKey(
                "test-producer",
                new GameplayOperationId("operation-" + operation),
                ownerRevision,
                0),
            ownerRevision,
            Math.Max(0, operation),
            actor,
            Name("민수", "name-" + operation),
            amount,
            "확정된 시험 결과 " + operation,
            default,
            default,
            default);
    }

    private static KoreanNameSnapshot Name(string display, string revision) =>
        new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");

    private static string FlipFirstHex(string value)
    {
        Require(value != null && value.Length == 64, "Expected a SHA-256 hex digest.");
        return (value[0] == '0' ? "1" : "0") + value.Substring(1);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
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

    private sealed class Fixture
    {
        public Fixture(
            GameplayOutcomeRegistry registry,
            TestDescriptor descriptor,
            GameplayOutcomeBufferLimits limits,
            GameplayOutcomeLedger ledger,
            GameplayOutcomeRecorder recorder)
        {
            Registry = registry;
            Descriptor = descriptor;
            Limits = limits;
            Ledger = ledger;
            Recorder = recorder;
        }

        public GameplayOutcomeRegistry Registry { get; }
        public TestDescriptor Descriptor { get; }
        public GameplayOutcomeBufferLimits Limits { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
    }

    private readonly struct TestReceipt
    {
        public TestReceipt(
            GameplayResultKey resultKey,
            long ownerRevision,
            int day,
            GameplayEntityId actorId,
            KoreanNameSnapshot actorName,
            double amount,
            string detail,
            GameplayEntityId secondId,
            KoreanNameSnapshot secondName,
            GameplayParticipationKind secondKind)
        {
            ResultKey = resultKey;
            OwnerRevision = ownerRevision;
            Day = day;
            ActorId = actorId;
            ActorName = actorName;
            Amount = amount;
            Detail = detail;
            SecondId = secondId;
            SecondName = secondName;
            SecondKind = secondKind;
        }

        public GameplayResultKey ResultKey { get; }
        public long OwnerRevision { get; }
        public int Day { get; }
        public GameplayEntityId ActorId { get; }
        public KoreanNameSnapshot ActorName { get; }
        public double Amount { get; }
        public string Detail { get; }
        public GameplayEntityId SecondId { get; }
        public KoreanNameSnapshot SecondName { get; }
        public GameplayParticipationKind SecondKind { get; }
        public bool HasSecond => SecondId.IsValid;

        public TestReceipt WithAmount(double value) =>
            new TestReceipt(
                ResultKey, OwnerRevision, Day, ActorId, ActorName, value, Detail,
                SecondId, SecondName, SecondKind);

        public TestReceipt WithSecond(
            GameplayEntityId id,
            string name,
            GameplayParticipationKind kind) =>
            new TestReceipt(
                ResultKey,
                OwnerRevision,
                Day,
                ActorId,
                ActorName,
                Amount,
                Detail,
                id,
                Name(name, "name-" + id.Value),
                kind);
    }

    private sealed class TestAdapter : GameplayOutcomeAdapter<TestReceipt>
    {
        public override GameplayOutcomeTypeId OutcomeTypeId => OutcomeType;

        public override OutcomePrepareResult TryGetRequirements(
            in TestReceipt receipt,
            long currentWorldEpoch,
            out OutcomeWriteRequirements requirements)
        {
            requirements = default;
            if (!receipt.ResultKey.IsValid
                || !receipt.ActorId.IsValid
                || receipt.OwnerRevision < 0L
                || receipt.Day < 0
                || double.IsNaN(receipt.Amount)
                || double.IsInfinity(receipt.Amount)
                || receipt.HasSecond && (int)receipt.SecondKind < (int)GameplayParticipationKind.Direct)
            {
                return new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, "test-receipt-invalid");
            }
            int participants = receipt.HasSecond ? 2 : 1;
            requirements = new OutcomeWriteRequirements(
                receipt.ResultKey,
                OutcomeType,
                receipt.Day,
                GameplayOutcomeStatus.Succeeded,
                currentWorldEpoch,
                receipt.OwnerRevision,
                participants,
                1,
                participants,
                1,
                0,
                1,
                1);
            return OutcomePrepareResult.Prepared();
        }

        public override OutcomePrepareResult TryWrite(
            in TestReceipt receipt,
            ref OutcomeWriteBuilder builder)
        {
            if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                    receipt.ActorId,
                    ActorRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.ActorName))
                || !builder.AddMetric(new GameplayOutcomeMetric(
                    AmountMetric,
                    receipt.Amount,
                    CountUnit,
                    receipt.ActorId))
                || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                    receipt.ActorId,
                    0.75f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0))
                || !builder.AddTag(TestTag)
                || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                    "receipt",
                    receipt.ResultKey.OperationId.Value))
                || !builder.AddFact(new GameplayOutcomeFact(DetailFact, receipt.Detail))
                || !builder.SetLocation(new GameplayLocationReference("dungeon", "room-a", 2, 3))
                || !builder.SetCausation(new GameplayOutcomeCausation(
                    default,
                    receipt.ResultKey.OperationId,
                    "root")))
            {
                return new OutcomePrepareResult(OutcomePrepareCode.AdapterWriteFailed, "test-write-failed");
            }

            if (receipt.HasSecond)
            {
                bool optional = receipt.SecondKind == GameplayParticipationKind.OptionalWitness;
                if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                        receipt.SecondId,
                        ActorRole,
                        receipt.SecondKind,
                        true,
                        receipt.SecondName))
                    || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                        receipt.SecondId,
                        optional ? 0.25f : 0.7f,
                        NarrativeMemoryTier.Recent,
                        false,
                        optional,
                        0)))
                {
                    return new OutcomePrepareResult(
                        OutcomePrepareCode.AdapterWriteFailed,
                        "test-second-write-failed");
                }
            }
            return OutcomePrepareResult.Prepared();
        }
    }

    private sealed class TestDescriptor :
        IGameplayOutcomeDescriptor,
        INarrativePerspectiveProjector,
        ICompactedNarrativePerspectiveProjector,
        IOutcomeMemoryPolicy,
        IOutcomePerceptionPolicy,
        IOutcomeMemoryConsolidator,
        IOutcomeCompactionContract
    {
        private readonly NarrativeMemoryTier targetTier;
        private readonly int witnessLimit;
        private readonly bool acceptOptionalWitnesses;

        public TestDescriptor(
            NarrativeMemoryTier targetTier,
            int witnessLimit,
            bool acceptOptionalWitnesses)
        {
            this.targetTier = targetTier;
            this.witnessLimit = witnessLimit;
            this.acceptOptionalWitnesses = acceptOptionalWitnesses;
        }

        public bool ThrowOnValidate { get; set; }
        public GameplayOutcomeTypeId OutcomeTypeId => OutcomeType;
        public INarrativePerspectiveProjector PerspectiveProjector => this;
        public IOutcomeMemoryPolicy MemoryPolicy => this;
        public IOutcomePerceptionPolicy PerceptionPolicy => this;
        public IOutcomeMemoryConsolidator MemoryConsolidator => this;
        public int PolicyVersion => 1;
        public int MaximumOptionalWitnessLinks => witnessLimit;
        public bool SupportsCompaction => true;

        public bool IsKnownRole(GameplayRoleId roleId) => roleId.Equals(ActorRole);

        public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId) =>
            metricId.Equals(AmountMetric) && unitId.Equals(CountUnit);

        public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
        {
            if (ThrowOnValidate)
                throw new InvalidOperationException("injected-descriptor-fault");
            if (outcome.OutcomeTypeId != OutcomeType
                || outcome.ParticipantCount < 1
                || outcome.ParticipantCount > 2
                || outcome.SubjectCount < 1
                || outcome.SubjectCount > outcome.ParticipantCount
                || outcome.MetricCount != 1
                || outcome.TagCount != 1
                || outcome.ProvenanceCount != 1
                || outcome.FactCount != 1
                || !outcome.GetMetric(0).MetricId.Equals(AmountMetric)
                || !outcome.GetMetric(0).UnitId.Equals(CountUnit)
                || !outcome.GetTag(0).Equals(TestTag)
                || !outcome.GetFact(0).FactId.Equals(DetailFact))
            {
                return OutcomeValidationResult.Reject("test-shape-invalid");
            }
            return OutcomeValidationResult.Accepted;
        }

        public NarrativeView Project(
            in GameplayOutcomeReadView outcome,
            NarrativePerspectiveContext perspective)
        {
            GameplayOutcomeParticipant actor = outcome.GetParticipant(0);
            string text = actor.DisplayName.DisplayText + " 결과 "
                + outcome.GetMetric(0).Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new NarrativeView(
                outcome.OutcomeId,
                perspective.Kind,
                text,
                "test-renderer-v1",
                false);
        }

        public NarrativeMemoryView ProjectCompacted(
            in CompactedNarrativeMemoryReadView memory,
            NarrativePerspectiveContext perspective)
        {
            string text = memory.GetParticipant(0).DisplayName.DisplayText
                + " 결과 " + memory.OccurrenceCount;
            return new NarrativeMemoryView(
                memory.MemoryId,
                perspective.Kind,
                text,
                "test-renderer-v1",
                false);
        }

        public GameplayMemorySignature GetSignature(
            in GameplayOutcomeReadView outcome,
            GameplayEntityId subjectId) =>
            new GameplayMemorySignature("test-signature");

        public OutcomeMemoryEvaluation Evaluate(
            in GameplayOutcomeReadView outcome,
            GameplayEntityId subjectId,
            int priorMatchingCount,
            int evaluationDay) =>
            new OutcomeMemoryEvaluation(
                Math.Max(0.1f, outcome.GetSubject(0).Salience),
                targetTier,
                checked(evaluationDay + 1));

        public bool ShouldCreateOptionalWitnessLink(
            in GameplayOutcomeReadView outcome,
            in GameplayOutcomeSubjectLink candidate) =>
            acceptOptionalWitnesses && candidate.Salience >= 0.2f;

        public bool CanCompact(in GameplayOutcomeReadView outcome, GameplayEntityId subjectId) => true;

        public bool IsAdditiveMetric(GameplayMetricId metricId) => metricId.Equals(AmountMetric);

        public bool CanMerge(
            in CompactedNarrativeMemoryReadView existing,
            in GameplayOutcomeReadView incoming,
            GameplayEntityId subjectId) =>
            existing.OutcomeTypeId == incoming.OutcomeTypeId
            && existing.SubjectId == subjectId;
    }
}
