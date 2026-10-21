using System;
using JsonUtility = UnityEngine.JsonUtility;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;
using System.Linq;
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

    public static string RunAll() => RunAll(includeUsageFormulaBudgets: true);

    // The storage refactor also runs typed evidence replay and tamper rejection.
    // The legacy usage-formula fixture needs real registered experience receipts;
    // keep that separate full-suite check intact instead of granting test.outcome
    // invented importance through the global production policy.
    public static string RunLedgerStorageContractsForBatch() =>
        RunAll(includeUsageFormulaBudgets: false);

    private static string RunAll(bool includeUsageFormulaBudgets)
    {
        RegistryFailsClosed();
        LifecycleReplayFaultAndRestore();
        RunReservationFinalStatusContracts();
        StagedRestoreIdentityQueryIsolation();
        _ = RunRestoreCandidateLifetimeContracts();
        FloatingPointReceiptRoundTrip();
        VerifyDelayedPublicationTime();
        OptionalWitnessFiltering();
        CompactionAndGlobalDeduplication();
        DelayedCompactionPreservesSourceBounds();
        RetiredParentsPreserveTemporalCausation();
        ForgettingAndTerminalReplay();
        TransactionalEvidenceAnchors();
        TransactionalInfluenceUse();
        PersistedEvidenceBindingsFailClosedWithoutCloneRepair();
        TypedEvidenceBindingRoundTripAndTamperRejection();
        PublicEvidenceOriginAndCausalBoundary();
        CompactedPublicEvidencePreservesCountsAndOrigins();
        MixedLegacyAggregateBudgetPreservesDistinctSources();
        if (includeUsageFormulaBudgets)
            UsageFormulaBudgetsPreserveDistinctSources();
        SliceAndQueuedDayDeterminism();
        MidConsolidationSaveRestartsFromExactInput();
        RetainedHistoryExceedsHotPool();
        RoutineDetailedRowsUseSmallRetention();
        KnownResultCapacityBackpressuresBeforeMutation();
        RetentionPromotionFailureIsExplicit();
        ExpeditionPresentationUsesOperationAndSameOutcomeId();
        return includeUsageFormulaBudgets
            ? "PASS GameplayOutcomeLedgerDebugScenarios"
            : "PASS GameplayOutcomeLedger storage/query/retention/restore contracts";
    }

    public static string RunRestoreCandidateLifetimeContracts()
    {
        Fixture source = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        TestReceipt receipt = Receipt(9751, 9751, 17d);
        GameplayOutcomeId expectedId = Publish(source, receipt);
        GameplayOutcomeLedgerSaveData saved = source.Ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger target = NewLedger(source.Registry, source.Limits, "run:candidate-target");
        GameplayOutcomeLedgerRestoreCandidate candidate = target.PrepareGameplayOutcomeRestore(saved);
        RequireThrows<InvalidOperationException>(() => target.PublishGameplayOutcomeRestore(candidate),
            "A ledger accepted a candidate before transaction admission.");
        Require(candidate.State != null && target.CurrentWorldEpoch == 1L,
            "Rejected pre-admission publication consumed the candidate or changed the live root.");
        target.BeginRestoreCandidate();
        target.PublishGameplayOutcomeRestore(candidate);
        target.PublishRestoreCandidate();
        Require(candidate.State == null && target.TryGetResultIdentity(receipt.ResultKey, out var identity)
                && identity.OutcomeId == expectedId,
            "Same candidate could not retry after rejected pre-admission publication.");
        target.RollbackPublishedRestoreCandidate();

        GameplayOutcomeLedgerRestoreCandidate first = target.PrepareGameplayOutcomeRestore(saved);
        GameplayOutcomeLedgerRestoreCandidate next = target.PrepareGameplayOutcomeRestore(saved);
        target.BeginRestoreCandidate();
        target.PublishGameplayOutcomeRestore(first);
        RequireThrows<InvalidOperationException>(() => target.PublishGameplayOutcomeRestore(next),
            "A staged ledger candidate was overwritten.");
        Require(first.State == null && next.State != null,
            "Rejected duplicate staging consumed a later candidate.");
        target.DiscardRestoreCandidate();
        target.BeginRestoreCandidate();
        target.PublishGameplayOutcomeRestore(next);
        target.PublishRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate afterPublication = target.PrepareGameplayOutcomeRestore(saved);
        RequireThrows<InvalidOperationException>(() => target.PublishGameplayOutcomeRestore(afterPublication),
            "A published ledger candidate was overwritten.");
        Require(afterPublication.State != null,
            "Rejected post-publication staging consumed a retryable candidate.");
        target.RollbackPublishedRestoreCandidate();
        target.BeginRestoreCandidate();
        target.PublishGameplayOutcomeRestore(afterPublication);
        target.PublishRestoreCandidate();
        target.CompleteRestoreCandidate();
        Require(target.TryGetResultIdentity(receipt.ResultKey, out identity) && identity.OutcomeId == expectedId,
            "Rollback and candidate retry lost the exact result identity.");

        GameplayOutcomeLedger foreign = NewLedger(source.Registry, source.Limits, "run:candidate-foreign");
        GameplayOutcomeLedgerRestoreCandidate owned = target.PrepareGameplayOutcomeRestore(saved);
        foreign.BeginRestoreCandidate();
        RequireThrows<InvalidOperationException>(() => foreign.PublishGameplayOutcomeRestore(owned),
            "A different ledger accepted a foreign prevalidated runtime root.");
        Require(owned.State != null && !foreign.HasStagedGameplayOutcomeRestore
                && foreign.CaptureGameplayOutcomes().runId == "run:candidate-foreign",
            "Foreign publication consumed the candidate or changed the wrong ledger.");
        foreign.DiscardRestoreCandidate();
        target.BeginRestoreCandidate();
        target.PublishGameplayOutcomeRestore(owned);
        target.PublishRestoreCandidate();
        target.CompleteRestoreCandidate();

        GameplayOutcomeLedgerRestoreCandidate discarded = target.PrepareGameplayOutcomeRestore(saved);
        discarded.Discard();
        discarded.Discard();
        target.BeginRestoreCandidate();
        RequireThrows<InvalidOperationException>(() => target.PublishGameplayOutcomeRestore(discarded),
            "A discarded candidate was staged.");
        Require(!target.HasStagedGameplayOutcomeRestore, "A discarded token left a partial staged root.");
        target.DiscardRestoreCandidate();
        target.BeginRestoreCandidate();
        RequireThrows<InvalidOperationException>(() => target.PublishGameplayOutcomeRestore(owned),
            "A consumed candidate was staged twice.");
        Require(!target.HasStagedGameplayOutcomeRestore, "A consumed token left a partial staged root.");
        target.DiscardRestoreCandidate();

        Fixture reserved = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        GameplayOutcomeLedgerRestoreCandidate waiting = reserved.Ledger.PrepareGameplayOutcomeRestore(saved);
        TestReceipt pendingReceipt = Receipt(9752, 9752, 18d);
        Require(new TestAdapter().TryGetRequirements(pendingReceipt,
                reserved.Ledger.CurrentWorldEpoch, out OutcomeWriteRequirements preview).Success,
            "Candidate admission fixture could not measure the pending receipt.");
        Require(reserved.Recorder.TryReserve(preview, out PreparedOutcomeReservation reservation).Success,
            "Candidate admission fixture could not reserve an active outcome.");
        RequireThrows<InvalidOperationException>(() => reserved.Ledger.BeginRestoreCandidate(),
            "Restore began while an outcome reservation was active.");
        RequireThrows<InvalidOperationException>(() => reserved.Ledger.PublishGameplayOutcomeRestore(waiting),
            "An active reservation bypassed restore admission.");
        Require(waiting.State != null && reserved.Ledger.GetDiagnostics().ReservedCount == 1,
            "Failed restore admission consumed the candidate or changed the reservation.");
        reserved.Recorder.CancelReservation(reservation);
        reserved.Ledger.BeginRestoreCandidate();
        reserved.Ledger.PublishGameplayOutcomeRestore(waiting);
        reserved.Ledger.PublishRestoreCandidate();
        reserved.Ledger.CompleteRestoreCandidate();
        Require(reserved.Ledger.TryGetResultIdentity(receipt.ResultKey, out identity) && identity.OutcomeId == expectedId,
            "Cancelled outcome reservation did not permit same-candidate restore retry.");
        return "PASS ledger candidate lifetime: pre-admission/staged/published reject preserves retry; "
            + "foreign owner rejected; discard/consume once; rollback and outcome-cancel retry preserve exact identity";
    }

    public static void RunReservationFinalStatusContracts()
    {
        AssertFinalStatusAuthority(
            previewStatus: GameplayOutcomeStatus.Succeeded,
            finalStatus: GameplayOutcomeStatus.Failed,
            operation: 9401);
        AssertFinalStatusAuthority(
            previewStatus: GameplayOutcomeStatus.Failed,
            finalStatus: GameplayOutcomeStatus.Succeeded,
            operation: 9402);
        AssertInvalidFinalStatusRejected();

        void AssertFinalStatusAuthority(
            GameplayOutcomeStatus previewStatus,
            GameplayOutcomeStatus finalStatus,
            int operation)
        {
            Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
            TestReceipt finalReceipt = Receipt(operation, operation, 9.4d)
                .WithStatus(finalStatus);
            OutcomeWriteRequirements preview = PreviewRequirements(
                fixture,
                finalReceipt,
                previewStatus);
            Require(
                fixture.Recorder.TryReserve(preview, out PreparedOutcomeReservation reservation)
                    .Success,
                "Final-status fixture could not reserve preview capacity.");
            Require(
                fixture.Ledger.GetDiagnostics().ReservedCount == 1,
                "Preview reservation was not recorded.");
            Require(
                fixture.Recorder.TryWriteReserved(
                    finalReceipt,
                    reservation,
                    out PreparedOutcomeToken prepared).Success,
                "Final-status receipt could not write its reserved outcome.");
            Require(
                fixture.Recorder.CommitPrepared(
                    prepared,
                    finalReceipt.OwnerRevision,
                    out CommittedOutcomeToken committed).Success,
                "Final-status receipt could not commit.");
            Require(
                fixture.Recorder.TryDeliver(committed).Published
                && fixture.Recorder.Acknowledge(committed).Success
                && fixture.Ledger.GetDiagnostics().ReservedCount == 0,
                "Final-status receipt did not finish delivery and acknowledgement.");

            Require(
                fixture.Ledger.TryGetResultIdentity(
                    finalReceipt.ResultKey,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.HasCanonicalPayloadHash
                && fixture.Ledger.TryGetExact(
                    committed.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact.status == finalStatus
                && string.Equals(
                    exact.immutablePayloadHash,
                    identity.CanonicalPayloadHash,
                    StringComparison.Ordinal),
                "Exact outcome did not retain the final receipt status and canonical hash.");

            Fixture canonicalFixture = CreateFixture(
                NarrativeMemoryTier.Episodic,
                witnessLimit: 0);
            GameplayOutcomeId canonicalOutcomeId = Publish(canonicalFixture, finalReceipt);
            Require(
                canonicalFixture.Ledger.TryGetResultIdentity(
                    finalReceipt.ResultKey,
                    out GameplayOutcomeReplayIdentity canonicalIdentity)
                && canonicalOutcomeId == committed.OutcomeId
                && string.Equals(
                    canonicalIdentity.CanonicalPayloadHash,
                    identity.CanonicalPayloadHash,
                    StringComparison.Ordinal),
                "Capacity-preview status changed the canonical final receipt hash.");

            GameplayOutcomeLedgerSaveData saved = fixture.Ledger.CaptureGameplayOutcomes();
            GameplayOutcomeSnapshot savedExact = saved.exactOutcomes.Single();
            Require(
                saved.exactOutcomes.Count == 1
                && savedExact.status == finalStatus
                && string.Equals(
                    savedExact.immutablePayloadHash,
                    identity.CanonicalPayloadHash,
                    StringComparison.Ordinal),
                "Official ledger save lost final receipt status or canonical hash.");
            GameplayOutcomeLedger restored = NewLedger(
                fixture.Registry,
                fixture.Limits,
                "run:reservation-final-status:" + operation);
            RestoreLedger(restored, saved);
            GameplayOutcomeSnapshot restoredExact = restored.CaptureGameplayOutcomes()
                .exactOutcomes.Single();
            Require(
                restoredExact.status == finalStatus
                && string.Equals(
                    restoredExact.immutablePayloadHash,
                    identity.CanonicalPayloadHash,
                    StringComparison.Ordinal),
                "Fresh ledger restore lost final receipt status or canonical hash.");

            OutcomePrepareResult replay = fixture.Recorder.TryPrepare(finalReceipt, out _);
            Require(
                replay.Code == OutcomePrepareCode.AlreadyPublished
                && string.Equals(
                    replay.Existing.CanonicalPayloadHash,
                    identity.CanonicalPayloadHash,
                    StringComparison.Ordinal)
                && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
                "Identical final receipt replay created a duplicate outcome.");

            TestReceipt conflicting = finalReceipt.WithStatus(
                finalStatus == GameplayOutcomeStatus.Succeeded
                    ? GameplayOutcomeStatus.Failed
                    : GameplayOutcomeStatus.Succeeded);
            OutcomePrepareResult conflict = fixture.Recorder.TryPrepare(conflicting, out _);
            Require(
                conflict.Code == OutcomePrepareCode.ConflictingResult
                && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
                "A final receipt with the same key and different status did not conflict.");
        }

        void AssertInvalidFinalStatusRejected()
        {
            Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
            TestReceipt validPreviewReceipt = Receipt(9403, 9403, 9.4d);
            Require(
                fixture.Recorder.TryReserve(
                    PreviewRequirements(
                        fixture,
                        validPreviewReceipt,
                        GameplayOutcomeStatus.Succeeded),
                    out PreparedOutcomeReservation reservation).Success,
                "Invalid-final-status fixture could not reserve valid preview capacity.");
            TestReceipt invalidFinalReceipt = validPreviewReceipt.WithStatus(
                (GameplayOutcomeStatus)int.MaxValue);
            OutcomePrepareResult rejected = fixture.Recorder.TryWriteReserved(
                invalidFinalReceipt,
                reservation,
                out PreparedOutcomeToken rejectedPrepared);
            Require(
                rejected.Code == OutcomePrepareCode.InvalidReceipt
                && rejected.DetailCode == "outcome-status-invalid"
                && !rejectedPrepared.IsValid
                && fixture.Recorder.CommitPrepared(
                    rejectedPrepared,
                    invalidFinalReceipt.OwnerRevision,
                    out _).Code == OutcomeCommitCode.InvalidPreparedToken,
                "Invalid final status was prepared or committed instead of failing validation.");
            fixture.Recorder.CancelReservation(reservation);
            GameplayOutcomeLedgerSaveData cancelled = fixture.Ledger.CaptureGameplayOutcomes();
            Require(
                fixture.Ledger.GetDiagnostics().ReservedCount == 0
                && cancelled.exactOutcomes.Count == 0
                && cancelled.outbox.Count == 0,
                "Cancelling rejected final status did not release its reservation.");
        }

        OutcomeWriteRequirements PreviewRequirements(
            Fixture fixture,
            TestReceipt receipt,
            GameplayOutcomeStatus status)
        {
            int participants = receipt.HasSecond ? 2 : 1;
            return new OutcomeWriteRequirements(
                receipt.ResultKey,
                OutcomeType,
                receipt.Day,
                status,
                fixture.Ledger.CurrentWorldEpoch,
                receipt.OwnerRevision,
                participants,
                1,
                participants,
                1,
                0,
                1,
                1);
        }
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
            && failureCode == "binding-source-identity-invalid",
            "Invalid exact source identity must fail before mechanic use.");

        clone.outcomeSequence = 1L;
        Require(
            !GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                clone,
                out failureCode)
            && failureCode == "binding-revision-invalid",
            "Persisted evidence revision corruption must fail closed.");
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

    private static void TypedEvidenceBindingRoundTripAndTamperRejection()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        TestReceipt receipt = Receipt(901, 901, 12.5d).WithSecond(
            new GameplayEntityId(CharacterKind, "target-901"),
            "유리",
            GameplayParticipationKind.Direct);
        Publish(fixture, receipt);
        GameplayOutcomeNarrativeEvidenceQuery query = new(fixture.Ledger);
        GameplayOutcomeNarrativeEvidenceSource source = query.GetForEntity(
                receipt.ActorId, 24, 0f, includeCompacted: true)
            .Single();
        GameplayOutcomeEvidenceBindingSnapshot binding =
            GameplayOutcomeEvidenceFormulaProjection.Capture(new[] { source }).Single();
        Require(
            binding.schemaVersion == 4
            && binding.schemaVersion == GameplayOutcomeEvidenceFormulaProjection.CurrentBindingSchemaVersion
            && binding.sourceKind == "exact"
            && binding.participants.Count == 2
            && binding.metrics.Count == 1
            && binding.metrics[0].value == 12.5d
            && binding.metrics[0].unitId == "count"
            && binding.facts.Count == 1
            && binding.provenance.Count == 1
            && binding.firstDay == receipt.Day
            && binding.lastDay == receipt.Day
            && binding.locationId == "dungeon"
            && binding.operationId == receipt.ResultKey.OperationId.Value
            && query.TryResolveExactBinding(binding, out var restored)
            && restored.SourceId == source.SourceId,
            "Typed exact evidence did not preserve participant/value/time/location/causal provenance.");
        CharacterNarrativeFact legacy = new()
        {
            factId = "legacy:901",
            lastEventContext = new GameplayNarrativeEventContext
            {
                eventInstanceId = binding.operationId
            }
        };
        Require(
            GameplayOutcomeEvidenceFormulaProjection.HasCommonOrigin(binding, legacy),
            "Exact and legacy evidence with one operation origin must be deduplicatable.");
        UsageLedgerEvent legacyUsage = new()
        {
            evidenceId = "usage:901",
            narrativeContext = new GameplayNarrativeEventContext
            {
                chainId = binding.rootOperationId,
                eventInstanceId = binding.operationId
            }
        };
        Require(
            GameplayOutcomeEvidenceFormulaProjection.HasCommonOrigin(
                binding, legacyUsage),
            "Exact and usage-ledger evidence with one operation origin must be deduplicatable.");
        string originalHash = GameplayOutcomeEvidenceBindingAuthority
            .ComputeCanonicalHash(binding);

        GameplayOutcomeEvidenceBindingSnapshot tampered = binding.Clone();
        tampered.metrics[0].value = 99d;
        Require(
            GameplayOutcomeEvidenceBindingAuthority.TryValidate(tampered, out _)
            && GameplayOutcomeEvidenceBindingAuthority.ComputeCanonicalHash(tampered)
                != originalHash
            && !query.TryResolveExactBinding(tampered, out _),
            "A reserialized typed metric forgery must fail source replay.");
        tampered = binding.Clone();
        tampered.participants[0].roleId = "target";
        Require(
            !GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                tampered, out string failureCode)
            && failureCode == "binding-flattened-typed-mismatch",
            "A role forgery must fail before mechanic use.");

        Fixture compactedFixture = CreateFixture(
            NarrativeMemoryTier.Compacted, witnessLimit: 0);
        TestReceipt compactedReceipt = Receipt(902, 902, 3d);
        GameplayOutcomeId compactedId = Publish(compactedFixture, compactedReceipt);
        Schedule(compactedFixture, 1, compactedId.Sequence);
        Drain(compactedFixture, 1);
        GameplayOutcomeNarrativeEvidenceQuery compactedQuery =
            new(compactedFixture.Ledger);
        GameplayOutcomeNarrativeEvidenceSource compactedSource = compactedQuery
            .GetForEntity(compactedReceipt.ActorId, 24, 0f, includeCompacted: true)
            .Single();
        GameplayOutcomeEvidenceBindingSnapshot compactedBinding =
            GameplayOutcomeEvidenceFormulaProjection.Capture(
                new[] { compactedSource }).Single();
        Require(
            compactedBinding.sourceKind == "compacted"
            && compactedBinding.compactedMemoryId.Length > 0
            && compactedBinding.outcomeRunId.Length == 0
            && compactedBinding.occurrenceCount == 1
            && compactedQuery.TryResolveExactBinding(compactedBinding, out _),
            "Compacted long-term evidence did not round-trip through the same binding authority.");
    }

    private static void PublicEvidenceOriginAndCausalBoundary()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        TestReceipt hiddenParent = Receipt(1, 1, 1d);
        GameplayOutcomeId hiddenParentId = Publish(fixture, hiddenParent);
        TestReceipt visibleParent = Receipt(2, 2, 2d).WithParent(hiddenParentId);
        GameplayOutcomeId visibleParentId = Publish(fixture, visibleParent);
        TestReceipt child = new(Receipt(3, 3, 3d).ResultKey, 3, 3,
            visibleParent.ActorId, visibleParent.ActorName, 3d, "공개 후속 결과",
            default, default, default, visibleParentId);
        Publish(fixture, child);
        GameplayOutcomeNarrativeEvidenceQuery query = new(fixture.Ledger);
        List<GameplayOutcomeEvidenceBindingSnapshot> bindings =
            GameplayOutcomeEvidenceFormulaProjection.Capture(
                query.GetForEntity(visibleParent.ActorId, 24, 0f, includeCompacted: true));
        Require(bindings.Count == 2, "A subject must not acquire its hidden ancestor as public evidence.");
        GameplayOutcomeEvidenceBindingSnapshot parentBinding = bindings.Single(
            value => value.operationId == visibleParent.ResultKey.OperationId.Value);
        GameplayOutcomeEvidenceBindingSnapshot childBinding = bindings.Single(
            value => value.operationId == child.ResultKey.OperationId.Value);
        string before = JsonUtility.ToJson(parentBinding);
        string Packet(IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> input)
        {
            var builder = new System.Text.StringBuilder();
            GameplayOutcomeEvidenceFormulaProjection.AppendPromptFacts(builder, input);
            return builder.ToString();
        }
        string packet = Packet(bindings);
        Require(packet.Contains("; cause=" + parentBinding.publicFactId)
            && !packet.Contains(hiddenParentId.ToString())
            && !packet.Contains(visibleParentId.ToString())
            && parentBinding.parentOutcomeId == hiddenParentId.ToString()
            && JsonUtility.ToJson(parentBinding) == before,
            "Public causes must use included public fact IDs without changing private binding lineage.");
        Require(!Packet(new[] { childBinding }).Contains("; cause="),
            "A parent outside the public evidence window must not be referenced.");
        GameplayOutcomeEvidenceBindingSnapshot otherSubjectParent =
            GameplayOutcomeEvidenceFormulaProjection.Capture(
                query.GetForEntity(hiddenParent.ActorId, 24, 0f, includeCompacted: true)).Single();
        Require(!Packet(new[] { otherSubjectParent, parentBinding }).Contains("; cause="),
            "A different subject's binding must not authorize a public causal reference.");

        GameplayOutcomeEvidenceBindingSnapshot identity = parentBinding.Clone();
        identity.rootOperationId = "shared-root";
        identity.provenance.Add(new GameplayOutcomeEvidenceProvenanceBindingSnapshot
            { kindId = "room-facility", value = "shared-facility" });
        identity.provenance.Add(new GameplayOutcomeEvidenceProvenanceBindingSnapshot
            { kindId = "domain-commit", value = "receipt-operation-alias" });
        identity.facts.Add(new GameplayOutcomeEvidenceFactBindingSnapshot
            { factId = "operation-id", value = "fact-operation-alias" });
        CharacterNarrativeFact Legacy(string eventId, string chainId) => new()
        {
            factId = "legacy-different",
            lastEventContext = new GameplayNarrativeEventContext
                { eventInstanceId = eventId, chainId = chainId }
        };
        UsageLedgerEvent Usage(string eventId, string chainId) => new()
        {
            evidenceId = "usage-different",
            narrativeContext = new GameplayNarrativeEventContext
                { eventInstanceId = eventId, chainId = chainId }
        };
        foreach (string origin in new[] { identity.operationId, identity.publicFactId,
                     visibleParentId.ToString(), "receipt-operation-alias", "fact-operation-alias" })
            Require(GameplayOutcomeEvidenceFormulaProjection.HasCommonOrigin(identity, Legacy(origin, ""))
                && GameplayOutcomeEvidenceFormulaProjection.HasCommonOrigin(identity, Usage(origin, "")),
                "An explicit event identity must deduplicate both legacy consumers: " + origin);
        foreach (string related in new[] { "unrelated-operation", identity.rootOperationId,
                     identity.parentOutcomeId, "shared-facility" })
            Require(!GameplayOutcomeEvidenceFormulaProjection.HasCommonOrigin(identity,
                    Legacy(related, identity.operationId))
                && !GameplayOutcomeEvidenceFormulaProjection.HasCommonOrigin(identity,
                    Usage(related, identity.operationId)),
                "Shared causal or facility provenance must not remove a distinct event: " + related);
        Debug.Log("PASS PUBLIC_EVIDENCE_CAUSAL_BOUNDARY_AND_ORIGIN_IDENTITY");
    }

    private static void CompactedPublicEvidencePreservesCountsAndOrigins()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Compacted, witnessLimit: 0);
        TestReceipt first = Receipt(1, 1, 3d);
        TestReceipt second = new(Receipt(2, 2, 7d).ResultKey, 2, 2,
            first.ActorId, first.ActorName, 7d, first.Detail, default, default, default);
        Publish(fixture, first);
        GameplayOutcomeId last = Publish(fixture, second);
        Schedule(fixture, 3, last.Sequence);
        Drain(fixture, 1);
        GameplayOutcomeNarrativeEvidenceQuery query = new(fixture.Ledger);
        GameplayOutcomeEvidenceBindingSnapshot binding = GameplayOutcomeEvidenceFormulaProjection.Capture(
            query.GetForEntity(first.ActorId, 24, 0f, includeCompacted: true)).Single();
        Require(binding.sourceKind == "compacted" && binding.occurrenceCount == 2
            && binding.metrics.Single().value == 10d && binding.metrics.Single().sampleCount == 2
            && query.TryResolveExactBinding(binding, out _)
            && GameplayOutcomeEvidenceFormulaProjection.ToFormulaEvidence(binding, null)
                .AttainedMilestoneCount == 2,
            "Merged public evidence must preserve both source occurrences and the additive metric.");
        foreach (TestReceipt receipt in new[] { first, second })
            Require(GameplayOutcomeEvidenceFormulaProjection.HasCommonOrigin(binding,
                new CharacterNarrativeFact
                {
                    factId = "legacy-source",
                    lastEventContext = new GameplayNarrativeEventContext
                        { eventInstanceId = receipt.ResultKey.OperationId.Value }
                }), "Compaction must retain explicit receipt identities for legacy deduplication.");
        var builder = new System.Text.StringBuilder();
        GameplayOutcomeEvidenceFormulaProjection.AppendPromptFacts(builder, new[] { binding });
        Require(builder.ToString().Contains("amount=10 count")
            && builder.ToString().Contains("결과 2")
            && builder.ToString().Contains("selectable=false-context-only"),
            "The compacted prompt must retain aggregate count/value and context-only status.");
        Debug.Log("PASS COMPACTED_PUBLIC_EVIDENCE_COUNT_VALUE_AND_RECEIPT_ORIGINS");
    }

    private static void MixedLegacyAggregateBudgetPreservesDistinctSources()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        Fixture compactedFixture = CreateFixture(NarrativeMemoryTier.Compacted, witnessLimit: 0);
        TestReceipt first = Receipt(1, 1, 1d);
        GameplayEntityId subject = new(CharacterKind, "character:qa-mixed-origin");
        List<string> operations = new();
        for (int index = 1; index <= 3; index++)
        {
            TestReceipt receipt = new(Receipt(index, index, 1d).ResultKey, index, index,
                subject, first.ActorName, 1d, "같은 작업의 별개 완료",
                default, default, default);
            Publish(fixture, receipt);
            Publish(compactedFixture, receipt);
            operations.Add(receipt.ResultKey.OperationId.Value);
        }
        GameplayOutcomeNarrativeEvidenceQuery query = new(fixture.Ledger);
        List<GameplayOutcomeEvidenceBindingSnapshot> bindings = GameplayOutcomeEvidenceFormulaProjection.Capture(
            query.GetForEntity(subject, 24, 0f, includeCompacted: true));
        Schedule(compactedFixture, 4, 3);
        Drain(compactedFixture, 1);
        GameplayOutcomeNarrativeEvidenceQuery compactedQuery = new(compactedFixture.Ledger);
        GameplayOutcomeEvidenceBindingSnapshot compacted = GameplayOutcomeEvidenceFormulaProjection.Capture(
            compactedQuery.GetForEntity(subject, 24, 0f, includeCompacted: true)).Single();
        Require(compacted.sourceKind == "compacted" && compacted.occurrenceCount == 3
            && compactedQuery.TryResolveExactBinding(compacted, out _),
            "Mixed-origin compacted input must resolve to three actual published receipts.");
        CharacterProgressionDebugScenarios.VerifyMixedLegacyAggregateBudget(bindings, operations, compacted);
    }

    private static void UsageFormulaBudgetsPreserveDistinctSources()
    {
        var observations = new Newtonsoft.Json.Linq.JArray();
        foreach (string kind in new[] { "equipment", "facility" })
        {
            Fixture exactFixture = CreateFixture(NarrativeMemoryTier.Episodic, 0);
            Fixture compactedFixture = CreateFixture(NarrativeMemoryTier.Compacted, 0);
            GameplayEntityId subject = new(new GameplayEntityKindId(kind), kind + ":qa-origin");
            List<string> operations = new();
            for (int index = 1; index <= 3; index++)
            {
                TestReceipt receipt = new(Receipt(index, index, 1d).ResultKey, index, index,
                    subject, Name("사용 기록 검증 대상", "usage-origin-name-v1"), 1d,
                    "동일 대상의 별개 사용", default, default, default);
                Publish(exactFixture, receipt);
                Publish(compactedFixture, receipt);
                operations.Add(receipt.ResultKey.OperationId.Value);
            }
            GameplayOutcomeNarrativeEvidenceQuery query = new(exactFixture.Ledger);
            var bindings = GameplayOutcomeEvidenceFormulaProjection.Capture(
                query.GetForEntity(subject, 24, 0f, true));
            Schedule(compactedFixture, 4, 3);
            Drain(compactedFixture, 1);
            GameplayOutcomeNarrativeEvidenceQuery compactedQuery = new(compactedFixture.Ledger);
            var compacted = GameplayOutcomeEvidenceFormulaProjection.Capture(
                compactedQuery.GetForEntity(subject, 24, 0f, true)).Single();
            Require(bindings.Count == 3 && bindings.All(value => query.TryResolveExactBinding(value, out _))
                && compacted.occurrenceCount == 3 && compactedQuery.TryResolveExactBinding(compacted, out _),
                "Usage formula inputs must resolve to the actual subject's core receipts.");
            observations.Add(VerifyUsageFormulaOriginBudget(kind, subject.Value, operations, bindings, compacted));
            Require(bindings.All(value => query.TryResolveExactBinding(value, out _))
                && compactedQuery.TryResolveExactBinding(compacted, out _),
                "Formula preparation/freeze consumed or changed a source authority anchor.");
        }
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            var result = new Newtonsoft.Json.Linq.JObject
            {
                ["test"] = "equipment-facility-usage-origin-budgets", ["passed"] = true,
                ["profiles"] = observations, ["trainingEligible"] = false,
                ["scope"] = "Controlled core receipts and actual formula authorities/catalogs; not natural gameplay, live eligibility or mechanic commit.",
                ["managedGcHeapCount"] = Environment.GetEnvironmentVariable("DOTNET_GCHeapCount"),
                ["managedGcHeapCountLegacyAlias"] = Environment.GetEnvironmentVariable("COMPlus_GCHeapCount")
            };
            using var stream = new System.IO.FileStream(System.IO.Path.Combine(output, "usage-origin-budgets.json"),
                System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.Read);
            using var writer = new System.IO.StreamWriter(stream, new System.Text.UTF8Encoding(false));
            writer.Write(result.ToString());
        }
        Debug.Log("PASS EQUIPMENT_FACILITY_USAGE_ORIGIN_BUDGETS_AND_SELECTION_FREEZE");
    }

    private static Newtonsoft.Json.Linq.JObject VerifyUsageFormulaOriginBudget(
        string kind, string subjectId, IReadOnlyList<string> operations,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings,
        GameplayOutcomeEvidenceBindingSnapshot compacted)
    {
        UsageLedgerCompactor recorder = new();
        UsageLedgerEvent Record(UsageLedger ledger, int index) => recorder.Record(
            ledger, kind == "equipment" ? "combat:hit" : "facility-use", 1f,
            "character:qa-origin-user", subjectId, evidenceId: "usage:" + kind + ":" + index,
            outcomeId: "completed", narrativeContext: new GameplayNarrativeEventContext
            {
                eventInstanceId = operations[index], chainId = "usage:shared-chain",
                usedObjectId = subjectId, actorId = "character:qa-origin-user"
            });
        Action<int> record;
        Func<IEnumerable<GameplayOutcomeEvidenceBindingSnapshot>, EvolutionNode> prepare;
        Func<EvolutionNode, EvolutionNode> freeze;
        Func<string> snapshot;
        Action restoreJson;
        Func<int> rawCount;
        Func<bool> unconsumed;
        Func<int, string> localId;
        string contentId;
        if (kind == "equipment")
        {
            EquipmentEvolutionState state = new();
            var catalog = new EquipmentEvolutionFormulaCatalogs(
                CharacterAiEditorTestDependencies.ContentDefinitions)
                .RequireCurrent();
            var definition = UnityEngine.Resources.LoadAll<CombatEquipmentDefinitionSO>("SO/Combat/Equipment")
                .Where(value => value != null && value.Kind == CombatEquipmentKind.MeleeWeapon)
                .OrderBy(value => value.EquipmentId, StringComparer.Ordinal).First();
            EquipmentFormulaTargetContext target = new(definition);
            contentId = definition.EquipmentId;
            record = index => EquipmentEvolutionRules.RecordFormulaEvidence(
                state, Record(state.usageLedger, index), catalog);
            prepare = exact => EquipmentEvolutionRules.BuildFormulaNode(state, catalog, target,
                subjectId, "equipment-node:qa-origin", string.Empty, "history:qa-origin",
                EquipmentEvolutionDirection.Melee, string.Empty, 0, true,
                "attunement:character:qa-origin-user:1", exact);
            freeze = node => EquipmentEvolutionRules.FreezeSelectedModule(node, state, catalog, subjectId,
                new NarrativeFormulaModuleSelectionChoice(node.moduleSelectionId,
                    new[] { "equipment:force" }, Array.Empty<string>(), node.evidenceIds));
            snapshot = () => JsonUtility.ToJson(state);
            restoreJson = () => state = JsonUtility.FromJson<EquipmentEvolutionState>(snapshot());
            rawCount = () => state.usageLedger.currentGenerationEvents.Count;
            unconsumed = () => state.formulaEvidence.All(value => value.influenceUseCount == 0);
            localId = index => state.formulaEvidence.Single(value =>
                value.originalEvent.narrativeContext.eventInstanceId == operations[index]).evidenceId;
        }
        else
        {
            FacilityEvolutionState state = new() { facilityPersistentId = subjectId };
            var recipe = UnityEngine.Resources.Load<FacilityEvolutionRecipeSO>(
                "SO/FacilityEvolution/P1/EV_TacticalTable");
            Require(recipe != null, "The authored tactical-table recipe is missing.");
            contentId = recipe.EffectiveId;
            record = index => Record(state.usageLedger, index);
            prepare = exact =>
            {
                Require(FacilityFormulaEvolutionAuthority.TryPrepare(state, recipe,
                    out var pending, out string failure, exact), "Usage-origin facility preparation: " + failure);
                return pending.node;
            };
            freeze = node => FacilityFormulaEvolutionAuthority.FreezeSelectedModule(state, recipe, node,
                new NarrativeFormulaModuleSelectionChoice(node.moduleSelectionId,
                    new[] { node.moduleSelectionOffers.Single(value =>
                        value.polarity == EvolutionModuleOfferPolarity.Positive).moduleId },
                    Array.Empty<string>(), node.evidenceIds));
            snapshot = () => JsonUtility.ToJson(state);
            restoreJson = () => state = JsonUtility.FromJson<FacilityEvolutionState>(snapshot());
            rawCount = () => state.usageLedger.currentGenerationEvents.Count;
            unconsumed = () => state.formulaEvidence.All(value => value.influenceUseCount == 0);
            localId = index => state.formulaEvidence.Single(value =>
                value.originalEvent.narrativeContext.eventInstanceId == operations[index]).evidenceId;
        }
        var latest = bindings.Single(value => value.operationId == operations[2]);
        record(0);
        record(1);
        EvolutionNode before = prepare(new[] { latest });
        EvolutionNode selectedBefore = freeze(before);
        record(2);
        EvolutionNode after = prepare(new[] { latest });
        EvolutionNode selectedAfter = freeze(after);
        Require(before.formulaBudget == after.formulaBudget && before.evidenceIds.SequenceEqual(after.evidenceIds)
            && selectedBefore.formulaBudget == selectedAfter.formulaBudget
            && selectedBefore.calculatedCost == selectedAfter.calculatedCost
            && selectedBefore.calculatedCost > 0
            && JsonUtility.ToJson(selectedBefore) == JsonUtility.ToJson(selectedAfter),
            kind + " double-counted or dropped distinct usage when adding a redundant legacy occurrence.");
        var first = bindings.Single(value => value.operationId == operations[0]);
        EvolutionNode firstOverlap = prepare(new[] { first });
        EvolutionNode noOverlap = prepare(Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>());
        EvolutionNode allOverlap = prepare(bindings);
        EvolutionNode compactedOverlap = prepare(new[] { compacted });
        Require(after.evidenceIds.ToHashSet(StringComparer.Ordinal).SetEquals(
                new[] { localId(0), localId(1), latest.publicFactId })
            && firstOverlap.evidenceIds.ToHashSet(StringComparer.Ordinal).SetEquals(
                new[] { localId(1), localId(2), first.publicFactId })
            && firstOverlap.gameplayOutcomeEvidence.Count == 1
            && noOverlap.evidenceIds.ToHashSet(StringComparer.Ordinal).SetEquals(
                new[] { localId(0), localId(1), localId(2) })
            && noOverlap.gameplayOutcomeEvidence.Count == 0
            && allOverlap.evidenceIds.ToHashSet(StringComparer.Ordinal).SetEquals(bindings.Select(value => value.publicFactId))
            && compactedOverlap.evidenceIds.SequenceEqual(new[] { compacted.publicFactId }),
            kind + " failed first/full/no/compacted source overlap filtering.");
        string persisted = snapshot();
        restoreJson();
        Require(snapshot() == persisted && rawCount() == 3 && unconsumed()
            && prepare(new[] { latest }).formulaBudget == after.formulaBudget,
            kind + " JSON restart changed usage origins/budget or preparation consumed influence.");
        return new Newtonsoft.Json.Linq.JObject
        {
            ["profile"] = kind, ["contentId"] = contentId, ["subjectId"] = subjectId,
            ["budgetBefore"] = before.formulaBudget, ["budgetAfter"] = after.formulaBudget,
            ["selectedBudgetBefore"] = selectedBefore.formulaBudget,
            ["selectedBudgetAfter"] = selectedAfter.formulaBudget,
            ["selectedCost"] = selectedAfter.calculatedCost,
            ["exactEvidenceId"] = latest.publicFactId,
            ["selectedEvidenceIds"] = new Newtonsoft.Json.Linq.JArray(after.evidenceIds),
            ["authoritativeUsageCount"] = rawCount(), ["preparedInfluenceConsumption"] = 0,
            ["persistedStateJson"] = persisted
        };
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

    private static void FloatingPointReceiptRoundTrip()
    {
        // Real facility needs contain fractional floats promoted to double.
        // Integral fixture metrics cannot detect lost precision at the save boundary.
        double[] values = { 97.32261657714844d, 97.05488586425781d, 0.48f,
            0.1d, -0.1d, 1d / 3d, double.Epsilon, double.MaxValue };
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        var state = (GameplayOutcomeLedgerRuntimeState)typeof(GameplayOutcomeLedger)
            .GetField("state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(fixture.Ledger);
        for (int index = 0; index < values.Length; index++)
        {
            GameplayOutcomeId id = Publish(fixture,
                Receipt(2100 + index, index + 1, values[index]).WithSalience(0.48f));
            GameplayOutcomeValuePage page = state.Pool.Get(state.ActiveByOutcomeId[id]);
            Require(GameplayOutcomeCanonicalHash.HashExact(new GameplayOutcomeReadView(page))
                == page.MaterializeImmutablePayloadHash(),
                "Hot/cold immutable hash differs for fractional metric index " + index);
        }
        GameplayOutcomeLedgerSaveData original = fixture.Ledger.CaptureGameplayOutcomes();
        var section = new GameplayOutcomeLedgerSaveSection(fixture.Ledger);
        string json = section.Capture();
        section.ValidatePayload(json, section.SectionVersion, new DungeonGameRestoreReport());
        var envelope = new DungeonGameSaveData();
        envelope.sections.Add(new DungeonSaveSectionEnvelope
        {
            sectionId = section.SectionId, sectionVersion = section.SectionVersion,
            restorePhase = section.RestorePhase, payloadJson = json
        });
        Require(DungeonSaveSectionPayload.TryRead(envelope, section.SectionId,
            out GameplayOutcomeLedgerSaveData parsed),
            "The aggregate preflight reader lost the ledger payload.");
        for (int index = 0; index < values.Length; index++)
        {
            double before = original.exactOutcomes[index].metrics[0].value;
            double after = parsed.exactOutcomes[index].metrics[0].value;
            Require(BitConverter.DoubleToInt64Bits(before) == BitConverter.DoubleToInt64Bits(after),
                "Official JSON changed metric bits at index " + index + ": "
                + before.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " -> "
                + after.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Require(BitConverter.SingleToInt32Bits(original.exactOutcomes[index].initialSubjects[0].salience)
                == BitConverter.SingleToInt32Bits(parsed.exactOutcomes[index].initialSubjects[0].salience),
                "Official JSON changed subject salience bits.");
        }
        GameplayOutcomeLedger restored = NewLedger(fixture.Registry, fixture.Limits, "run:fractional-restore");
        RestoreLedger(restored, parsed);
        Require(restored.CaptureGameplayOutcomes().exactOutcomes.Count == values.Length,
            "Fractional receipt round trip lost exact rows.");
        parsed.exactOutcomes[0].metrics[0].value += 1d;
        RequireThrows<InvalidOperationException>(() => restored.PrepareGameplayOutcomeRestore(parsed),
            "Lossless numeric reader must not bypass immutable metric integrity checks.");
        RequireThrows<Newtonsoft.Json.JsonException>(() =>
                DungeonSaveSectionPayload.Deserialize<GameplayOutcomeLedgerSaveData>(json + " {}"),
            "Ledger JSON reader accepted trailing content.");

        var character = new DungeonCharacterSaveData();
        var draft = new CharacterSkillDraft();
        character.growth.drafts.Add(draft);
        var binding = new GameplayOutcomeEvidenceBindingSnapshot();
        draft.outcomeEvidenceBindings.Add(binding);
        foreach (double value in values)
            binding.metrics.Add(new GameplayOutcomeEvidenceMetricBindingSnapshot
                { metricId = "fractional", value = value, minimum = value, maximum = value });
        var characterRoundTrip = DungeonSaveSectionPayload.Deserialize<DungeonCharacterSaveData>(
            UnityEngine.JsonUtility.ToJson(character));
        List<GameplayOutcomeEvidenceMetricBindingSnapshot> metrics =
            characterRoundTrip.growth.drafts[0].outcomeEvidenceBindings[0].metrics;
        for (int index = 0; index < values.Length; index++)
            Require(new[] { metrics[index].value, metrics[index].minimum, metrics[index].maximum }
                    .All(value => BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(values[index])),
                "Nested character request evidence lost numeric precision.");
    }

    internal static void VerifyTypedEvidenceAnchorRoundTrip(
        GameplayOutcomeLedger ledger,
        GameplayOutcomeRegistry registry,
        GameplayEntityId character,
        GameplayOutcomeId outcomeId,
        string immutableHash,
        string anchorType = "acquired-trait")
    {
        NarrativeEvidenceReference anchor = new(anchorType,
            "presentation:" + anchorType + ":qa-evidence:" + outcomeId.Sequence);
        Require(ledger.TryPrepareEvidenceAnchorMutation(outcomeId, character,
                anchor, EvidenceAnchorMutationKind.Add, 0,
                out PreparedEvidenceAnchorToken prepared).Success,
            "work evidence anchor prepare failed");
        Require(ledger.CommitEvidenceAnchorMutation(prepared, out _).Success,
            "work evidence anchor commit failed");
        Require(ledger.TryPrepareInfluenceUse(outcomeId, character, 0, 0,
                out PreparedInfluenceUseToken influence).Success,
            "work evidence influence prepare failed");
        Require(ledger.CommitInfluenceUse(influence,
                out InfluenceUseRollbackToken committedUse).Success,
            "work evidence influence commit failed");
        Require(ledger.CompleteInfluenceUses(new[] { committedUse }).Success,
            "work evidence owner-commit seal failed");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeSnapshot exact = saved.exactOutcomes.Single(value => value.runId == outcomeId.RunId.Value
            && value.sequence == outcomeId.Sequence);
        Require(exact.anchors.Count == 1 && exact.initialAnchors.Count == 0
                && exact.immutablePayloadHash == immutableHash
                && exact.subjects.Single(value => value.entityKindId == character.Kind.Value
                    && value.entityId == character.Value).influenceUseCount == 1,
            "work evidence use changed its immutable receipt or lost its use");
        string savedJson = JsonUtility.ToJson(saved);
        GameplayOutcomeLedger restored = new(registry,
            GameplayOutcomeBufferLimits.Default,
            new GameplayOutcomeRunId("run:work-anchor-restore"), 1L);
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(
            restored.PrepareGameplayOutcomeRestore(
                JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(savedJson)));
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(restored.TryGetExact(outcomeId, out GameplayOutcomeSnapshot after)
                && JsonUtility.ToJson(after) == JsonUtility.ToJson(exact)
                && restored.CaptureGameplayOutcomes().worldEpoch
                    == checked(saved.worldEpoch + 1L),
            "anchored work receipt changed across JSON restore");
        string beforeDuplicate = JsonUtility.ToJson(restored.CaptureGameplayOutcomes());
        Require(restored.TryPrepareEvidenceAnchorMutation(outcomeId, character,
                anchor, EvidenceAnchorMutationKind.Add, after.anchorRevision,
                out _).Code == EvidenceAnchorPrepareCode.AlreadyApplied
                && !restored.TryPrepareInfluenceUse(outcomeId, character, 0, 0,
                    out _).Success
                && JsonUtility.ToJson(restored.CaptureGameplayOutcomes()) == beforeDuplicate,
            "restored work evidence duplicated an anchor or accepted stale use");

        VerifyInvalidAnchor(value => value.anchors[0].subjectId =
            "character:foreign", "foreign subject");
        VerifyInvalidAnchor(value => value.anchors[0].anchorTypeId = "",
            "malformed anchor type");
        VerifyInvalidAnchor(value => value.anchors[0].anchorId = "",
            "malformed anchor identity");
        VerifyInvalidAnchor(value => value.anchors.Add(value.anchors[0]),
            "duplicate anchor");
        VerifyInvalidAnchor(value => value.subjects.Single(link => link.entityKindId == character.Kind.Value
                && link.entityId == character.Value).tier =
            NarrativeMemoryTier.Recent, "non-core anchored subject");
        VerifyInvalidAnchor(value => value.initialAnchors.Add(value.anchors[0]),
            "forged initial producer anchor");
        Debug.Log("[Typed Evidence Anchor Restore] PASS; type=" + exact.outcomeTypeId
            + "; normal-anchor=1; "
            + "influence-use=1; immutable-receipt=exact; duplicate=unchanged; "
            + "invalid-anchor-atomic-reject=6");

        void VerifyInvalidAnchor(Action<GameplayOutcomeSnapshot> mutate,
            string label)
        {
            GameplayOutcomeLedgerSaveData invalid =
                JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(savedJson);
            mutate(invalid.exactOutcomes.Single(value => value.runId == outcomeId.RunId.Value
                && value.sequence == outcomeId.Sequence));
            string before = JsonUtility.ToJson(restored.CaptureGameplayOutcomes());
            bool rejected = false;
            try { restored.PrepareGameplayOutcomeRestore(invalid); }
            catch (ArgumentException) { rejected = true; }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "work restore accepted " + label);
            Require(JsonUtility.ToJson(restored.CaptureGameplayOutcomes()) == before,
                "work restore rejection partially mutated live state: " + label);
        }
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

    private static void StagedRestoreIdentityQueryIsolation()
    {
        Fixture live = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        TestReceipt liveReceipt = Receipt(91, 91, 11d);
        Publish(live, liveReceipt);
        Require(live.Ledger.TryGetResultIdentity(
                liveReceipt.ResultKey,
                out GameplayOutcomeReplayIdentity liveIdentity),
            "Live restore-query fixture did not publish its original identity.");

        Fixture incoming = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        TestReceipt incomingReceipt = Receipt(92, 92, 22d);
        Publish(incoming, incomingReceipt);
        Require(incoming.Ledger.TryGetResultIdentity(
                incomingReceipt.ResultKey,
                out GameplayOutcomeReplayIdentity incomingIdentity),
            "Incoming restore-query fixture did not publish its identity.");
        GameplayOutcomeLedgerSaveData incomingSave = incoming.Ledger.CaptureGameplayOutcomes();
        GameplayResultKey missingKey = Receipt(93, 93, 33d).ResultKey;
        IGameplayOutcomeRestoreCandidateQuery stagedQuery = live.Ledger;

        bool SameIdentity(
            GameplayOutcomeReplayIdentity left,
            GameplayOutcomeReplayIdentity right) =>
            left.OutcomeId == right.OutcomeId
            && string.Equals(
                left.CanonicalPayloadHash,
                right.CanonicalPayloadHash,
                StringComparison.Ordinal);

        live.Ledger.BeginRestoreCandidate();
        Require(stagedQuery.IsGameplayOutcomeRestoreTransactionActive
                && !stagedQuery.HasStagedGameplayOutcomeRestore
                && !stagedQuery.TryGetStagedResultIdentity(liveReceipt.ResultKey, out _)
                && live.Ledger.TryGetResultIdentity(
                    liveReceipt.ResultKey,
                    out GameplayOutcomeReplayIdentity liveDuringEmptyStage)
                && SameIdentity(liveDuringEmptyStage, liveIdentity),
            "Beginning restore must activate staged lookup without changing or exposing the live ledger.");
        GameplayOutcomeLedgerRestoreCandidate candidate = live.Ledger
            .PrepareGameplayOutcomeRestore(incomingSave);
        live.Ledger.PublishGameplayOutcomeRestore(candidate);
        Require(stagedQuery.IsGameplayOutcomeRestoreTransactionActive
                && stagedQuery.HasStagedGameplayOutcomeRestore,
            "Publishing a gameplay outcome restore candidate did not expose staged-query state.");
        Require(live.Ledger.TryGetResultIdentity(
                    liveReceipt.ResultKey,
                    out GameplayOutcomeReplayIdentity liveWhileStaged)
                && SameIdentity(liveWhileStaged, liveIdentity)
                && !live.Ledger.TryGetResultIdentity(incomingReceipt.ResultKey, out _),
            "Staging incoming outcomes changed or replaced the live identity query.");
        Require(stagedQuery.TryGetStagedResultIdentity(
                    incomingReceipt.ResultKey,
                    out GameplayOutcomeReplayIdentity stagedIdentity)
                && SameIdentity(stagedIdentity, incomingIdentity)
                && !stagedQuery.TryGetStagedResultIdentity(liveReceipt.ResultKey, out _)
                && !stagedQuery.TryGetStagedResultIdentity(missingKey, out _),
            "Staged identity lookup leaked live/missing keys or changed the incoming identity.");

        live.Ledger.DiscardRestoreCandidate();
        Require(!stagedQuery.IsGameplayOutcomeRestoreTransactionActive
                && !stagedQuery.HasStagedGameplayOutcomeRestore
                && !stagedQuery.TryGetStagedResultIdentity(incomingReceipt.ResultKey, out _)
                && live.Ledger.TryGetResultIdentity(
                    liveReceipt.ResultKey,
                    out GameplayOutcomeReplayIdentity liveAfterDiscard)
                && SameIdentity(liveAfterDiscard, liveIdentity)
                && !live.Ledger.TryGetResultIdentity(incomingReceipt.ResultKey, out _),
            "Discarding a staged outcome restore did not preserve the original live ledger.");

        live.Ledger.BeginRestoreCandidate();
        candidate = live.Ledger.PrepareGameplayOutcomeRestore(incomingSave);
        live.Ledger.PublishGameplayOutcomeRestore(candidate);
        live.Ledger.PublishRestoreCandidate();
        Require(stagedQuery.IsGameplayOutcomeRestoreTransactionActive
                && !stagedQuery.HasStagedGameplayOutcomeRestore
                && !stagedQuery.TryGetStagedResultIdentity(incomingReceipt.ResultKey, out _)
                && live.Ledger.TryGetResultIdentity(
                    incomingReceipt.ResultKey,
                    out GameplayOutcomeReplayIdentity liveAfterPublish)
                && SameIdentity(liveAfterPublish, incomingIdentity)
                && !live.Ledger.TryGetResultIdentity(liveReceipt.ResultKey, out _),
            "Publishing the aggregate restore did not atomically expose only the incoming live ledger.");

        live.Ledger.RollbackPublishedRestoreCandidate();
        Require(!stagedQuery.IsGameplayOutcomeRestoreTransactionActive
                && !stagedQuery.HasStagedGameplayOutcomeRestore
                && !stagedQuery.TryGetStagedResultIdentity(incomingReceipt.ResultKey, out _)
                && live.Ledger.TryGetResultIdentity(
                    liveReceipt.ResultKey,
                    out GameplayOutcomeReplayIdentity liveAfterRollback)
                && SameIdentity(liveAfterRollback, liveIdentity)
                && !live.Ledger.TryGetResultIdentity(incomingReceipt.ResultKey, out _),
            "Rolling back a published outcome restore did not recover only the original live ledger.");
    }

    public static void VerifyDelayedPublicationTime()
    {
        IGameCalendar calendar = EditorFixedGameCalendar.Instance;
        int originalDay = calendar.Day;
        int originalHour = calendar.Hour;
        try
        {
            calendar.SetDateTime(2, 5);
            Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, 0,
                limits: new GameplayOutcomeBufferLimits(smallPageCount: 2, largePageCount: 0,
                    knownResultKeyCapacity: 512), calendar: calendar);
            TestReceipt parent = Receipt(2, 2, 1d);
            GameplayOutcomeId parentId = Publish(fixture, parent);
            calendar.SetDateTime(3, 6);
            TestReceipt child = Receipt(3, 3, 2d).WithParent(parentId);
            Require(fixture.Recorder.TryPrepare(child, out PreparedOutcomeToken token).Success,
                "Clocked child preparation failed.");
            Require(fixture.Recorder.CommitPrepared(token, child.OwnerRevision,
                    out CommittedOutcomeToken committed).Success, "Clocked child commit failed.");
            fixture.Descriptor.ThrowOnValidate = true;
            Require(fixture.Recorder.TryDeliver(committed).Code == OutcomeDeliveryCode.PendingDeliveryFault,
                "Delayed delivery fault was not injected.");
            fixture.Descriptor.ThrowOnValidate = false;
            GameplayOutcomeLedgerSaveData pendingSave = fixture.Ledger.CaptureGameplayOutcomes();
            GameplayOutcomeSnapshot pending = pendingSave.outbox.Single();
            Require(pending.absoluteDay == 3 && pending.deliveryMetadataVersion == 0
                    && pending.publishedRevision == 0 && pending.deliveryFaultCount == 1,
                "Pending outbox invented a publication timestamp.");

            // Restore the durable outbox before advancing its same calendar, then publish
            // a later occurrence first. Preparation sequence is deliberately not delivery order.
            GameplayOutcomeLedger restored = new(fixture.Registry, fixture.Limits,
                new GameplayOutcomeRunId("run:time-restore"), 1L, calendar);
            RestoreLedger(restored, UnityEngine.JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
                UnityEngine.JsonUtility.ToJson(pendingSave)));
            GameplayOutcomeRecorder recorder = new(restored, fixture.Registry, new GameEventBus());
            Fixture resumed = new(fixture.Registry, fixture.Descriptor, fixture.Limits, restored, recorder);
            calendar.SetDateTime(4, 8);
            GameplayOutcomeId laterId = Publish(resumed, Receipt(4, 4, 3d));
            calendar.SetDateTime(5, 9);
            Require(recorder.RetryPendingDeliveries(1) == 1, "Restored delayed outbox did not publish exactly once.");
            Require(restored.TryGetExact(committed.OutcomeId, out GameplayOutcomeSnapshot delayed)
                    && restored.TryGetExact(laterId, out GameplayOutcomeSnapshot later)
                    && delayed.absoluteDay == 3 && delayed.recordedAbsoluteHour == 105L
                    && delayed.deliveryMetadataVersion == 1
                    && delayed.deliveryClockId == GameplayOutcomeDeliveryTime.CalendarClockId
                    && delayed.deliveryDelayReason == "delivery-fault-retry"
                    && delayed.sequence < later.sequence && delayed.publishedRevision > later.publishedRevision
                    && delayed.parentSequence == parentId.Sequence
                    && delayed.immutablePayloadHash == pending.immutablePayloadHash,
                "Delay changed original occurrence/causation/hash or confused preparation and publication order.");
            string exactBeforeReplay = UnityEngine.JsonUtility.ToJson(delayed);
            Require(recorder.RetryPendingDeliveries(10) == 0
                    && recorder.TryPrepare(child, out _).Code == OutcomePrepareCode.AlreadyPublished
                    && restored.TryGetExact(committed.OutcomeId, out GameplayOutcomeSnapshot duplicate)
                    && UnityEngine.JsonUtility.ToJson(duplicate) == exactBeforeReplay,
                "Duplicate delivery changed the original publication metadata.");
            GameplayOutcomeDeliveryTime.RequireCapturedAt(delayed, calendar.AbsoluteHour, true);
            GameplayOutcomeLedgerSaveData finalSave = restored.CaptureGameplayOutcomes();
            DungeonGameRestoreReport timeJoin = new();
            DungeonAggregateReferencePreflight.ValidateOutcomeCalendar(
                new GameSessionSaveData { absoluteDay = 5, hour = 9 }, finalSave, timeJoin);
            Require(timeJoin.Errors.Count == 0, "Official save preflight rejected a valid delayed ledger/calendar join.");
            foreach (GameSessionSaveData staleCalendar in new[]
                {
                    new GameSessionSaveData { absoluteDay = 5, hour = 8 },
                    new GameSessionSaveData { absoluteDay = 2, hour = 8 },
                    new GameSessionSaveData { absoluteDay = 0, hour = 8 }
                })
            {
                DungeonGameRestoreReport invalidJoin = new();
                DungeonAggregateReferencePreflight.ValidateOutcomeCalendar(staleCalendar, finalSave, invalidJoin);
                Require(invalidJoin.Errors.Count > 0, "Official save preflight accepted a future occurrence/publication or invalid calendar.");
            }
            GameplayOutcomeLedger roundTrip = new(fixture.Registry, fixture.Limits,
                new GameplayOutcomeRunId("run:time-roundtrip"), 1L, calendar);
            RestoreLedger(roundTrip, UnityEngine.JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
                UnityEngine.JsonUtility.ToJson(finalSave)));
            Require(roundTrip.TryGetExact(committed.OutcomeId, out GameplayOutcomeSnapshot exactAfterRestore)
                    && UnityEngine.JsonUtility.ToJson(exactAfterRestore) == exactBeforeReplay,
                "Recorded time/revision was lost during detached retention or JSON restore.");

            TestReceipt impossibleChild = Receipt(1, 7, 2d).WithParent(laterId);
            OutcomePrepareResult rejected = recorder.TryPrepare(impossibleChild, out _);
            Require(rejected.Code == OutcomePrepareCode.InvalidReceipt && rejected.DetailCode == "parent-occurs-after-child",
                "Live parent-after-child receipt was accepted.");
            Require(recorder.TryPrepare(Receipt(6, 6, 3d), out _).DetailCode == "outcome-occurs-after-current-calendar",
                "Future receipt was accepted before its domain commit.");
            RequireThrows<InvalidOperationException>(() => GameplayOutcomeDeliveryTime.RequireCapturedAt(delayed, 104L, true),
                "Publication after capture time was accepted.");
            RequireThrows<InvalidOperationException>(() => GameplayOutcomeDeliveryTime.RequireCapturedAt(delayed, 47L, true),
                "Occurrence after capture time was accepted.");
            Fixture unclocked = CreateFixture(NarrativeMemoryTier.Episodic, 0);
            GameplayOutcomeId unclockedId = Publish(unclocked, Receipt(1, 1, 1d));
            Require(unclocked.Ledger.TryGetExact(unclockedId, out GameplayOutcomeSnapshot unknown)
                    && unknown.deliveryMetadataVersion == 0, "Unclocked fixture claimed observed game time.");
            RequireThrows<InvalidOperationException>(() => GameplayOutcomeDeliveryTime.RequireCapturedAt(unknown, 129L, true),
                "Unclocked historical record entered a clocked training source.");
            Fixture wrongClock = CreateFixture(NarrativeMemoryTier.Episodic, 0,
                calendar: new OffsetCalendar(calendar));
            Require(wrongClock.Recorder.TryPrepare(Receipt(1, 1, 1d), out _).DetailCode
                    == "outcome-calendar-clock-inconsistent",
                "A day-shifted clock was accepted as the production calendar.");
            RequireThrows<InvalidOperationException>(() => wrongClock.Ledger.GetGlobal(OutcomeCursor.FirstPage(), OutcomeFilter.All),
                "A query accepted an inconsistent production calendar.");

            Action<GameplayOutcomeSnapshot>[] corruptions =
            {
                value => value.deliveryMetadataVersion = 2,
                value => value.deliveryClockId = "wall-clock",
                value => value.recordedAbsoluteHour = 47L,
                value => value.publishedRevision = 0L,
                value => value.publishedRevision = finalSave.revision + 1L,
                value => value.deliveryDelayReason = "same-day-publication",
                value => value.deliveryMetadataVersion = 0
            };
            foreach (Action<GameplayOutcomeSnapshot> corrupt in corruptions)
            {
                GameplayOutcomeLedgerSaveData tampered = UnityEngine.JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
                    UnityEngine.JsonUtility.ToJson(finalSave));
                corrupt(tampered.exactOutcomes.Single(value => value.sequence == delayed.sequence));
                RequireThrows<InvalidOperationException>(() => roundTrip.PrepareGameplayOutcomeRestore(tampered),
                    "Malformed publication metadata was accepted during restore.");
                Require(roundTrip.TryGetExact(committed.OutcomeId, out GameplayOutcomeSnapshot unchanged)
                        && UnityEngine.JsonUtility.ToJson(unchanged) == exactBeforeReplay,
                    "Rejected restore changed published state.");
            }
            // Recompute a forged immutable payload so this negative probe reaches the
            // temporal reference check rather than merely failing its hash check.
            GameplayOutcomeLedgerRuntimeState liveState = (GameplayOutcomeLedgerRuntimeState)typeof(GameplayOutcomeLedger)
                .GetField("state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(restored);
            GameplayOutcomeValuePage liveChild = liveState.Pool.Get(liveState.ActiveByOutcomeId[committed.OutcomeId]);
            GameplayOutcomeCausation originalCause = liveChild.Causation;
            try
            {
                liveChild.Causation = new GameplayOutcomeCausation(laterId, originalCause.RootOperationId, "caused-by");
                GameplayOutcomeLedgerSaveData forged = UnityEngine.JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
                    UnityEngine.JsonUtility.ToJson(finalSave));
                GameplayOutcomeSnapshot forgedChild = forged.exactOutcomes.Single(value => value.sequence == delayed.sequence);
                forgedChild.parentRunId = laterId.RunId.Value;
                forgedChild.parentSequence = laterId.Sequence;
                forgedChild.causationRelationId = "caused-by";
                forgedChild.immutablePayloadHash = GameplayOutcomeCanonicalHash.HashExact(new GameplayOutcomeReadView(liveChild));
                bool temporalRejected = false;
                try { roundTrip.PrepareGameplayOutcomeRestore(forged); }
                catch (InvalidOperationException exception) { temporalRejected = exception.Message.Contains("parent occurs after its child"); }
                Require(temporalRejected, "Hash-valid parent-after-child restore escaped temporal validation.");
            }
            finally { liveChild.Causation = originalCause; }
        }
        finally { calendar.SetDateTime(originalDay, originalHour); }
    }

    private static void RestoreLedger(GameplayOutcomeLedger ledger, GameplayOutcomeLedgerSaveData saved)
    {
        GameplayOutcomeLedgerRestoreCandidate candidate = ledger.PrepareGameplayOutcomeRestore(saved);
        ledger.BeginRestoreCandidate();
        ledger.PublishGameplayOutcomeRestore(candidate);
        ledger.PublishRestoreCandidate();
        ledger.CompleteRestoreCandidate();
    }

    private sealed class OffsetCalendar : IGameCalendar
    {
        private readonly IGameCalendar inner;
        public OffsetCalendar(IGameCalendar inner) => this.inner = inner;
        public int Day => inner.Day;
        public int Hour => inner.Hour;
        public int Year => inner.Year;
        public int DayOfYear => inner.DayOfYear;
        public Season Season => inner.Season;
        public int DayOfSeason => inner.DayOfSeason;
        public long AbsoluteHour => inner.AbsoluteHour + 24L;
        public float ElapsedSeconds => inner.ElapsedSeconds;
        public TimeOfDay TimeOfDay => inner.TimeOfDay;
        public bool IsRunning => inner.IsRunning;
        public CalendarDateTime Current => inner.Current;
        public CalendarDateTime GetRegionalTime(int offset) => inner.GetRegionalTime(offset);
        public void Start() => inner.Start();
        public void SetDateTime(int day, int hour) => inner.SetDateTime(day, hour);
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

    private static void DelayedCompactionPreservesSourceBounds()
    {
        var calendar = new EditorFixedGameCalendar(2, 9);
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Compacted, 0,
            limits: new GameplayOutcomeBufferLimits(smallPageCount: 2, largePageCount: 0, knownResultKeyCapacity: 64),
            calendar: calendar);
        var actor = new GameplayEntityId(CharacterKind, "actor:delayed-compaction");
        TestReceipt Row(int operation, int day, double amount) => new(
            new GameplayResultKey("test-producer", new GameplayOperationId("compact-delay:" + operation), operation, 0),
            operation, day, actor, Name("민수", "delayed-compaction-name"), amount, "시간 범위 검증",
            default, default, default);
        TestReceipt earlier = Row(1, 2, 3d);
        Require(fixture.Recorder.TryPrepare(earlier, out PreparedOutcomeToken prepared).Success,
            "Delayed compaction input could not prepare.");
        Require(fixture.Recorder.CommitPrepared(prepared, earlier.OwnerRevision, out CommittedOutcomeToken committed).Success,
            "Delayed compaction input could not commit.");
        fixture.Descriptor.ThrowOnValidate = true;
        Require(fixture.Recorder.TryDeliver(committed).Code == OutcomeDeliveryCode.PendingDeliveryFault,
            "Delayed compaction input must remain in outbox.");
        fixture.Descriptor.ThrowOnValidate = false;
        calendar.SetDateTime(4, 9);
        GameplayOutcomeId newer = Publish(fixture, Row(2, 4, 7d));
        Schedule(fixture, 4, newer.Sequence);
        Drain(fixture, 1);
        GameplayOutcomeLedgerSaveData first = fixture.Ledger.CaptureGameplayOutcomes();
        Require(first.outbox.Count == 1 && first.compactedMemories.Count == 1
            && first.compactedMemories[0].firstDay == 4, "Newer source must compact before delayed source.");
        RestoreLedger(fixture.Ledger, UnityEngine.JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
            UnityEngine.JsonUtility.ToJson(first)));
        calendar.SetDateTime(6, 9);
        Require(fixture.Recorder.RetryPendingDeliveries(1) == 1, "Restored delayed source did not publish.");
        Schedule(fixture, 6, newer.Sequence);
        Drain(fixture, 1);
        GameplayOutcomeLedgerSaveData complete = fixture.Ledger.CaptureGameplayOutcomes();
        CompactedNarrativeMemorySnapshot memory = complete.compactedMemories.Single();
        Require(memory.firstSequence == 1L && memory.lastSequence == 2L
            && memory.firstDay == 2 && memory.lastDay == 4 && memory.occurrenceCount == 2
            && memory.metrics.Single().sum == 10d && complete.tombstones.Count == 2,
            "Late earlier source must expand min/max sequence/date bounds without losing count or metrics.");
        string saved = UnityEngine.JsonUtility.ToJson(complete);
        RestoreLedger(fixture.Ledger, UnityEngine.JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(saved));
        GameplayOutcomeLedgerSaveData expectedRestored = UnityEngine.JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(saved);
        // Official restore deliberately advances the epoch to invalidate old
        // tokens. No exact records/jobs remain here; every other byte must match.
        Require(complete.exactOutcomes.Count == 0 && complete.outbox.Count == 0 && complete.consolidationJobs.Count == 0,
            "Delayed compaction fixture unexpectedly retained live source work.");
        expectedRestored.worldEpoch = checked(complete.worldEpoch + 1L);
        string restoredSave = UnityEngine.JsonUtility.ToJson(expectedRestored);
        Require(UnityEngine.JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes()) == restoredSave,
            "Delayed compacted sources changed beyond the required restore epoch advance.");
        var report = new DungeonGameRestoreReport();
        DungeonAggregateReferencePreflight.ValidateOutcomeCalendar(new GameSessionSaveData { absoluteDay = 3, hour = 9 },
            complete, report);
        Require(report.Errors.Count > 0, "Future compacted sources must fail the official session time join.");
        calendar.SetDateTime(3, 9);
        RequireThrows<InvalidOperationException>(() => fixture.Ledger.CaptureGameplayOutcomes(),
            "Clocked capture must reject future compacted memory even with no exact records.");
        calendar.SetDateTime(6, 9);
        Require(UnityEngine.JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes()) == restoredSave,
            "Failed compacted capture changed the live ledger.");
    }

    private static void RetiredParentsPreserveTemporalCausation()
    {
        foreach (NarrativeMemoryTier tier in new[] { NarrativeMemoryTier.Compacted, NarrativeMemoryTier.Forgotten })
        {
            var calendar = new EditorFixedGameCalendar(2, 9);
            Fixture fixture = CreateFixture(tier, 0, calendar: calendar,
                limits: new GameplayOutcomeBufferLimits(smallPageCount: 2, largePageCount: 0, knownResultKeyCapacity: 64));
            TestReceipt parent = Receipt(2, 2, 3d);
            Require(fixture.Recorder.TryPrepare(parent, out PreparedOutcomeToken prepared).Success,
                "Retired parent preparation failed.");
            Require(fixture.Recorder.CommitPrepared(prepared, parent.OwnerRevision, out CommittedOutcomeToken committed).Success,
                "Retired parent commit failed.");
            fixture.Descriptor.ThrowOnValidate = true;
            Require(fixture.Recorder.TryDeliver(committed).Code == OutcomeDeliveryCode.PendingDeliveryFault,
                "Retired parent delay fault was not injected.");
            fixture.Descriptor.ThrowOnValidate = false;
            calendar.SetDateTime(6, 9);
            Require(fixture.Recorder.RetryPendingDeliveries(1) == 1, "Delayed parent did not publish.");
            Require(fixture.Ledger.TryGetExact(committed.OutcomeId, out GameplayOutcomeSnapshot exactParent),
                "Published parent was not available before retirement.");
            calendar.SetDateTime(6, 8);
            RequireFutureQueriesRejected(fixture, parent, committed.OutcomeId, "memory:query-calendar-probe");
            calendar.SetDateTime(6, 9);
            Schedule(fixture, 6, committed.OutcomeId.Sequence);
            Drain(fixture, 1);
            GameplayOutcomeLedgerSaveData saved = fixture.Ledger.CaptureGameplayOutcomes();
            GameplayOutcomeTombstoneSnapshot retired = saved.tombstones.Single();
            Require(retired.temporalMetadataVersion == 1 && retired.absoluteDay == 2
                && retired.terminalTier == tier && retired.deliveryMetadataVersion == 1
                && retired.recordedAbsoluteHour == exactParent.recordedAbsoluteHour
                && retired.publishedRevision == exactParent.publishedRevision
                && retired.deliveryFaultCount == 1 && retired.deliveryDelayReason == "delivery-fault-retry"
                && retired.canonicalHash == exactParent.immutablePayloadHash,
                "Retirement lost original time/hash or delayed publication metadata.");
            GameplayOutcomeDeliveryTime.RequireCapturedAt(retired, calendar.AbsoluteHour, true);
            string retiredBytes = UnityEngine.JsonUtility.ToJson(retired);
            RestoreLedger(fixture.Ledger, Clone(saved));
            Require(UnityEngine.JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes().tombstones.Single()) == retiredBytes,
                "Retired time was not preserved through official ledger restore.");
            calendar.SetDateTime(6, 8);
            RequireFutureQueriesRejected(fixture, parent, committed.OutcomeId,
                saved.compactedMemories.FirstOrDefault()?.memoryId ?? "memory:query-calendar-probe");
            calendar.SetDateTime(6, 9);
            calendar.SetDateTime(7, 9);
            GameplayOutcomeId child = Publish(fixture, Receipt(7, 7, 1d).WithParent(committed.OutcomeId));
            saved = fixture.Ledger.CaptureGameplayOutcomes();
            RestoreLedger(fixture.Ledger, Clone(saved));
            GameplayOutcomeLedgerSaveData expectedRejected = fixture.Ledger.CaptureGameplayOutcomes();
            // Reservation diagnostics count attempted work even when the receipt is
            // rejected. No identity, revision, committed row or owner state may change.
            expectedRejected.knownResultKeyHighWater = Math.Max(expectedRejected.knownResultKeyHighWater, 3);
            // The acknowledged child is in detached retention, not the hot pool.
            expectedRejected.exactPageUseHighWater = Math.Max(expectedRejected.exactPageUseHighWater, 1);
            string beforeFailure = UnityEngine.JsonUtility.ToJson(expectedRejected);
            Require(fixture.Recorder.TryPrepare(Receipt(1, 99, 1d).WithParent(committed.OutcomeId), out _).DetailCode
                == "parent-occurs-after-child", "A new child earlier than a retired parent was accepted.");
            Require(UnityEngine.JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes()) == beforeFailure,
                "Rejected retired parent reference mutated live state.");

            GameplayOutcomeLedgerSaveData forged = Clone(saved);
            MoveParentAfterChild(forged.tombstones.Single());
            RequireThrows<InvalidOperationException>(() => fixture.Ledger.PrepareGameplayOutcomeRestore(forged),
                "An exact child restored with a later retired parent.");
            Schedule(fixture, 7, child.Sequence);
            Drain(fixture, 1);
            GameplayOutcomeLedgerSaveData bothRetired = fixture.Ledger.CaptureGameplayOutcomes();
            GameplayOutcomeTombstoneSnapshot retiredChild = bothRetired.tombstones.Single(value => value.sequence == child.Sequence);
            Require(retiredChild.parentRunId == committed.OutcomeId.RunId.Value
                && retiredChild.parentSequence == committed.OutcomeId.Sequence && retiredChild.absoluteDay == 7,
                "Retiring the child lost its parent edge.");
            RestoreLedger(fixture.Ledger, Clone(bothRetired));
            beforeFailure = UnityEngine.JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes());
            Action<GameplayOutcomeTombstoneSnapshot>[] corruptions =
            {
                MoveParentAfterChild,
                value => value.temporalMetadataVersion = 2,
                value => value.temporalMetadataVersion = 0,
                value => value.absoluteDay = -1,
                value => value.deliveryMetadataVersion = 2,
                value => value.deliveryClockId = "wall-clock",
                value => value.recordedAbsoluteHour = 0,
                value => value.publishedRevision = bothRetired.revision + 1,
                value => value.deliveryFaultCount = -1,
                value => value.deliveryDelayReason = "same-day-publication",
                value => value.parentSequence = 99,
                value => { value.parentRunId = value.runId; value.parentSequence = 99; },
                value => { value.parentRunId = "run:foreign"; value.parentSequence = 1; }
            };
            foreach (Action<GameplayOutcomeTombstoneSnapshot> corrupt in corruptions)
            {
                GameplayOutcomeLedgerSaveData invalid = Clone(bothRetired);
                corrupt(invalid.tombstones.Single(value => value.sequence == committed.OutcomeId.Sequence));
                RequireThrows<InvalidOperationException>(() => fixture.Ledger.PrepareGameplayOutcomeRestore(invalid),
                    "Invalid archived temporal metadata/parent graph escaped detached validation.");
                Require(UnityEngine.JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes()) == beforeFailure,
                    "Rejected archived restore changed live state.");
            }
            var report = new DungeonGameRestoreReport();
            DungeonAggregateReferencePreflight.ValidateOutcomeCalendar(new GameSessionSaveData { absoluteDay = 7, hour = 8 },
                bothRetired, report);
            Require(report.Errors.Any(value => value.Contains("outcome-calendar-tombstone-invalid")),
                "Official session join missed a future tombstone publication.");
            calendar.SetDateTime(7, 8);
            RequireThrows<InvalidOperationException>(() => fixture.Ledger.CaptureGameplayOutcomes(),
                "Clocked capture accepted a future tombstone publication.");
            calendar.SetDateTime(7, 9);
            Require(UnityEngine.JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes()) == beforeFailure,
                "Failed tombstone capture changed live state.");

            GameplayOutcomeLedgerSaveData legacy = Clone(bothRetired);
            GameplayOutcomeTombstoneSnapshot oldParent = legacy.tombstones.Single(value => value.sequence == committed.OutcomeId.Sequence);
            oldParent.temporalMetadataVersion = 0;
            oldParent.absoluteDay = 0;
            oldParent.deliveryMetadataVersion = 0;
            oldParent.deliveryClockId = oldParent.deliveryDelayReason = string.Empty;
            oldParent.recordedAbsoluteHour = oldParent.publishedRevision = 0;
            oldParent.deliveryFaultCount = 0;
            RestoreLedger(fixture.Ledger, legacy);
            Require(fixture.Ledger.CaptureGameplayOutcomes().tombstones.Single(value => value.sequence == committed.OutcomeId.Sequence)
                .temporalMetadataVersion == 0, "Legacy unknown time was silently fabricated.");
            Require(fixture.Recorder.TryPrepare(Receipt(7, 100, 1d).WithParent(committed.OutcomeId), out _).DetailCode
                == "parent-occurrence-time-unobserved", "New causation accepted an unknown historical parent time.");
            RequireThrows<InvalidOperationException>(() => GameplayOutcomeDeliveryTime.RequireCapturedAt(oldParent,
                calendar.AbsoluteHour, true), "Unknown historical tombstone became observed training evidence.");

            void MoveParentAfterChild(GameplayOutcomeTombstoneSnapshot value)
            {
                value.absoluteDay = 8;
                value.recordedAbsoluteHour = GameplayOutcomeDeliveryTime.OccurrenceStartHour(8) + 9;
            }
        }

        static GameplayOutcomeLedgerSaveData Clone(GameplayOutcomeLedgerSaveData value) =>
            UnityEngine.JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(UnityEngine.JsonUtility.ToJson(value));
    }

    private static void RequireFutureQueriesRejected(Fixture fixture, TestReceipt receipt,
        GameplayOutcomeId outcomeId, string memoryId)
    {
        NarrativePerspectiveContext perspective = new(receipt.ActorId, NarrativePerspectiveKind.Character, "ko-KR");
        Action[] queries =
        {
            () => fixture.Ledger.GetGlobal(OutcomeCursor.FirstPage(), OutcomeFilter.All),
            () => fixture.Ledger.GetForEntity(receipt.ActorId, OutcomeCursor.FirstPage(), OutcomeFilter.All),
            () => fixture.Ledger.GetForOperation(receipt.ResultKey.OperationId),
            () => fixture.Ledger.GetNarrativeEvidence(new NarrativeEvidenceQuery(receipt.ActorId, 10, 0, true, default)),
            () => fixture.Ledger.TryGetExact(outcomeId, out _),
            () => fixture.Ledger.TryGetCompacted(new NarrativeMemoryId(memoryId), out _),
            () => fixture.Ledger.TryProject(outcomeId, perspective, out _),
            () => fixture.Ledger.TryProjectMemory(new NarrativeMemoryId(memoryId), perspective, out _)
        };
        foreach (Action query in queries)
            RequireThrows<InvalidOperationException>(query, "A query exposed a future publication after clock rewind/restore/retirement.");
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

    private static void RoutineDetailedRowsUseSmallRetention()
    {
        var limits = new GameplayOutcomeBufferLimits(smallPageCount: 1, largePageCount: 1,
            retainedSmallPageCount: 16, retainedLargePageCount: 1);
        var pool = new GameplayOutcomeValuePagePool(limits);
        for (int index = 0; index < 16; index++)
        {
            var requirements = new OutcomeWriteRequirements(
                new GameplayResultKey("routine-detail", new GameplayOperationId("routine:" + index), 1, 0),
                OutcomeType, 1, GameplayOutcomeStatus.Succeeded, 1, 1,
                2, index % 2 == 0 ? 16 : 2, 2, 1, 0, 2,
                new[] { 9, 12, 22, GameplayOutcomeBufferLimits.AbsoluteMaximumFactsPerOutcome }[index % 4]);
            Require(pool.TryRent(requirements, out int pageIndex, out GameplayOutcomeValuePage page)
                && !page.IsLarge, "Routine detailed records must not consume giant slabs.");
            int generation = page.Generation;
            page.State = GameplayOutcomePageState.PublishedAcknowledged;
            Require(pool.TryPromotePublishedToDetached(pageIndex, generation, out _, out GameplayOutcomeValuePage retained)
                && !retained.IsLarge, "Routine detailed history must retain its small storage class.");
            pool.Return(pageIndex, generation);
            Require(pool.PooledInUseCount == 0 && pool.FreeRetainedLargeCount == 1
                && pool.FreeRetainedSmallCount == 15 - index,
                "Routine acknowledgement leaked hot slots or consumed giant retention.");
        }
        var exhausted = new OutcomeWriteRequirements(
            new GameplayResultKey("routine-detail", new GameplayOperationId("routine:full"), 1, 0),
            OutcomeType, 1, GameplayOutcomeStatus.Succeeded, 1, 1, 2, 16, 2, 1, 0, 2, 12);
        Require(!pool.TryRent(exhausted, out _, out _) && pool.PooledInUseCount == 0
            && pool.FreeRetainedLargeCount == 1,
            "Routine retention exhaustion must remain bounded and explicit; no silent allocation or giant fallback.");
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
        IGameEventBus eventBus = null,
        IGameCalendar calendar = null)
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
        GameplayOutcomeLedger ledger = new(registry, selectedLimits, new GameplayOutcomeRunId("run:test"), 1L, calendar);
        GameplayOutcomeRecorder recorder = new GameplayOutcomeRecorder(
            ledger,
            registry,
            eventBus ?? new GameEventBus());
        return new Fixture(registry, descriptor, selectedLimits, ledger, recorder);
    }

    private static void ExpeditionPresentationUsesOperationAndSameOutcomeId()
    {
        Fixture fixture = CreateFixture(NarrativeMemoryTier.Episodic, witnessLimit: 0);
        GameplayEntityId expedition = new(
            new GameplayEntityKindId("expedition"),
            "expedition-test-1");
        GameplayOperationId operation = new(expedition.Value);
        TestReceipt receipt = new(
            new GameplayResultKey("test-producer", operation, 1L, 0),
            1L,
            1,
            expedition,
            Name("시험 원정대", "expedition-test-name-v1"),
            7d,
            "원정 관점 시험",
            default,
            default,
            default);
        GameplayOutcomeId outcomeId = Publish(fixture, receipt);
        GameplayOutcomePresentationQuery presentation = new(
            fixture.Ledger,
            new GameplayOutcomeDisplayNameQuery(
                Array.Empty<IGameplayOutcomeEntityNameResolver>()),
            new KoreanJosaFormatter());

        GameplayOutcomePresentationPage expeditionPage = presentation.GetExpeditionPage(
            expedition,
            operation,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomePresentationPage globalPage = presentation.GetGlobalPage(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            expeditionPage.Rows.Count == 1
            && globalPage.Rows.Count == 1
            && expeditionPage.Rows[0].SourceId == outcomeId.ToString()
            && globalPage.Rows[0].SourceId == outcomeId.ToString()
            && fixture.Descriptor.LastPerspectiveKind == NarrativePerspectiveKind.Global,
            "Global and expedition pages must reference the same outcome ID.");

        presentation.GetExpeditionPage(
            expedition,
            operation,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            fixture.Descriptor.LastPerspectiveKind == NarrativePerspectiveKind.Expedition,
            "Expedition history must use the Expedition perspective, not Global.");
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
            GameplayParticipationKind secondKind,
            GameplayOutcomeId parentId = default,
            float salience = 0.75f,
            GameplayOutcomeStatus status = GameplayOutcomeStatus.Succeeded)
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
            ParentId = parentId;
            Salience = salience;
            Status = status;
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
        public GameplayOutcomeId ParentId { get; }
        public float Salience { get; }
        public GameplayOutcomeStatus Status { get; }

        public TestReceipt WithSalience(float value) => new(ResultKey, OwnerRevision, Day,
            ActorId, ActorName, Amount, Detail, SecondId, SecondName, SecondKind, ParentId, value, Status);

        public TestReceipt WithParent(GameplayOutcomeId parent) => new(ResultKey, OwnerRevision, Day,
            ActorId, ActorName, Amount, Detail, SecondId, SecondName, SecondKind, parent, Salience, Status);

        public TestReceipt WithAmount(double value) =>
            new TestReceipt(
                ResultKey, OwnerRevision, Day, ActorId, ActorName, value, Detail,
                SecondId, SecondName, SecondKind, ParentId, Salience, Status);

        public TestReceipt WithStatus(GameplayOutcomeStatus status) => new(ResultKey, OwnerRevision, Day,
            ActorId, ActorName, Amount, Detail, SecondId, SecondName, SecondKind, ParentId, Salience, status);

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
                kind, ParentId, Salience, Status);
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
                receipt.Status,
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
                    receipt.Salience,
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
                    receipt.ParentId,
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
        public NarrativePerspectiveKind LastPerspectiveKind { get; private set; }
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
            LastPerspectiveKind = perspective.Kind;
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
            LastPerspectiveKind = perspective.Kind;
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
