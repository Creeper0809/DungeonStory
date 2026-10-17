#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class WorkCompletionIdentityOutcomeDebugScenarios
{
    private static readonly CharacterId Character =
        new("character:qa-work-identity");

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyDirectSequenceAndRestore();
        VerifyReferencedRequestIdentityAndRestore();

        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new WorkCompletionIdentityOutcomeDescriptor(
                    new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new WorkCompletionIdentityOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 1,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:work-identity-editor"),
            1L);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        WorkCompletionIdentityGameplayOutcomeBridge bridge = new(
            recorder,
            ledger,
            new FixedDisplayNames(),
            ledger);
        WorkCompletionIdentityDeliveryRequest request = Request(
            "identity-event:qa-work-identity:000000",
            "qa-work-identity:stream",
            0,
            BuiltInWorkTypeIds.Harvest.Value,
            "item:qa-harvest",
            "source=passive|skill=skill:qa-harvest|module=output"
                + "|value=0.25|mechanicsSha256="
                + new string('a', 64));

        Require(
            bridge.TryPrepare(
                request,
                out PreparedWorkCompletionIdentityOutcome prepared,
                out bool capacityDeferred,
                out string prepareFailure),
            "work identity prepare failed: " + prepareFailure);
        Require(!capacityDeferred, "work identity prepare unexpectedly deferred");
        OwnerOutcomeCommitResult committed = bridge.Commit(prepared);
        Require(
            committed.DurablyCommitted,
            "work identity commit failed: " + committed.DetailCode);

        GameplayEntityId character = new(
            WorkCompletionIdentityOutcomeIds.CharacterKind,
            Character.Value);
        GameplayOutcomeQueryPage global = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage characterPage = ledger.GetForEntity(
            character,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            global.Items.Count == 1
            && characterPage.Items.Count == 1
            && global.Items[0].Exact != null,
            "work identity queries did not expose one exact outcome");
        GameplayOutcomeSnapshot exact = global.Items[0].Exact;
        Require(
            exact.outcomeTypeId
                == WorkCompletionIdentityOutcomeIds.Applied.Value
            && exact.ownerRevision == 1L
            && exact.commitRevision == 1L
            && exact.absoluteDay == 8
            && exact.participants.Count == 1
            && exact.participants[0].displayText == "가람"
            && exact.facts.Any(value =>
                value.factId
                    == WorkCompletionIdentityOutcomeIds.WorkIdFact.Value
                && value.value == BuiltInWorkTypeIds.Harvest.Value)
            && exact.facts.Any(value =>
                value.factId
                    == WorkCompletionIdentityOutcomeIds.ProductIdFact.Value
                && value.value == "item:qa-harvest")
            && exact.facts.Any(value =>
                value.factId
                    == WorkCompletionIdentityOutcomeIds.ReactionFact.Value
                && value.value == "completed-work")
            && exact.facts.Any(value =>
                value.factId
                    == WorkCompletionIdentityOutcomeIds
                        .SkillModifierProvenanceFact.Value
                && value.value == request.SkillModifierProvenance),
            "work identity outcome lost frozen receipt data");

        GameplayOutcomeId outcomeId = new(
            new GameplayOutcomeRunId(exact.runId),
            exact.sequence);
        Require(
            ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    character,
                    NarrativePerspectiveKind.Character,
                    "ko-KR"),
                out NarrativeView view)
            && view.Text.Contains("가람은", StringComparison.Ordinal)
            && view.Text.Contains("작업 완료", StringComparison.Ordinal),
            "work identity Korean projection was not stable");

        Require(
            bridge.TryPrepare(
                request,
                out PreparedWorkCompletionIdentityOutcome replay,
                out _,
                out string replayFailure),
            "work identity replay prepare failed: " + replayFailure);
        OwnerOutcomeCommitResult replayed = bridge.Commit(replay);
        Require(
            replayed.DurablyCommitted
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "work identity replay duplicated or rejected the canonical result");

        WorkCompletionIdentityDeliveryRequest drifted = Request(
            request.DeliveryId,
            request.ProducerStreamId,
            request.OperationSequence,
            "work:craft",
            request.ProductId,
            request.SkillModifierProvenance);
        Require(
            !bridge.TryPrepare(
                drifted,
                out _,
                out _,
                out _),
            "same work identity result key accepted a drifted receipt");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = new(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 1,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:work-identity-restore"),
            1L);
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.GetForEntity(
                character,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Single().Exact.immutablePayloadHash
            == exact.immutablePayloadHash,
            "work identity outcome changed across save round trip");

        GameplayOutcomeLedgerDebugScenarios.VerifyTypedEvidenceAnchorRoundTrip(
            ledger, registry, character, outcomeId,
            exact.immutablePayloadHash);

        if (logSuccess)
            Debug.Log("[Work Completion Identity Outcome] PASS");
        return true;
    }

    private static void VerifyDirectSequenceAndRestore()
    {
        WorkCompletionIdentityDeliveryLedger ledger = new();
        WorkCompletedIdentityEvent firstEvent = new(
            Character,
            "work:craft",
            string.Empty,
            CharacterCommandOrigin.DirectPlayerOrder,
            4);
        WorkCompletionIdentityDeliveryRequest first =
            ledger.CreateNextDirectRequest(firstEvent);
        Require(
            first.OperationSequence == 0
            && first.ProductId.Length == 0
            && first.ProducerStreamId
                == WorkCompletionIdentityDeliveryLedger.DirectEventStreamId,
            "direct work identity sequence did not begin canonically");
        ledger.CommitObservation(
            new WorkIdentityAppliedFact(
                first,
                "가람",
                requiresTerminalDelivery: false),
            WorkCompletionIdentityOutcomeIds.ResultKey(first).ToString(),
            "unit:work-identity-direct/1",
            new string('a', 64),
            complete: true);
        Require(
            ledger.TryGetCommittedDisposition(first, out var disposition)
            && disposition == WorkCompletionIdentityDeliveryDisposition
                .EffectsAppliedWithOutcome,
            "direct work identity cursor lost its outcome disposition");

        WorkCompletionIdentityDeliveryLedger restored = new();
        restored.Restore(ledger.Capture());
        WorkCompletionIdentityDeliveryRequest second =
            restored.CreateNextDirectRequest(firstEvent);
        Require(
            second.OperationSequence == 1
            && second.DeliveryId
                == WorkCompletionIdentityDeliveryLedger
                    .DirectEventDeliveryPrefix + "1",
            "direct work identity sequence did not survive restore");
    }

    private static void VerifyReferencedRequestIdentityAndRestore()
    {
        GameplayResultKey childKey = new(
            "medical.surgery",
            new GameplayOperationId(
                "surgery-terminal:surgery:qa-referenced"),
            1L,
            0);
        WorkCompletedIdentityEvent source = ReferencedEvent(
            Character,
            childKey.OperationId.Value,
            childKey.ToString());
        WorkCompletionIdentityDeliveryRequest request =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(
                source);
        WorkCompletionIdentityDeliveryRequest same =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(
                source);
        Require(
            request.OperationSequence == 0
            && request.DeliveryId == same.DeliveryId
            && request.ProducerStreamId == same.ProducerStreamId
            && request.PayloadFingerprint == same.PayloadFingerprint,
            "referenced work identity request was not stable for one child and worker");

        WorkCompletionIdentityDeliveryLedger ledger = new();
        ledger.CommitObservation(
            new WorkIdentityAppliedFact(
                request,
                "가람",
                requiresTerminalDelivery: true),
            WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString(),
            "unit:work-identity-referenced/1",
            new string('b', 64),
            complete: true);
        Require(
            ledger.Inspect(same, out _) ==
                WorkCompletionIdentityDeliveryStatus.AlreadyApplied,
            "same referenced work identity was not replay-safe");

        RequireReferencedConflict(
            ledger,
            request,
            ReferencedEvent(
                Character,
                childKey.OperationId.Value,
                childKey.ToString(),
                workId: "work:qa-referenced-alternate"),
            "work");
        RequireReferencedConflict(
            ledger,
            request,
            ReferencedEvent(
                Character,
                childKey.OperationId.Value,
                childKey.ToString(),
                productId: "item:qa-referenced-alternate"),
            "product");
        RequireReferencedConflict(
            ledger,
            request,
            ReferencedEvent(
                Character,
                childKey.OperationId.Value,
                childKey.ToString(),
                origin: CharacterCommandOrigin.DirectPlayerOrder),
            "origin");
        RequireReferencedConflict(
            ledger,
            request,
            ReferencedEvent(
                Character,
                childKey.OperationId.Value,
                childKey.ToString(),
                day: 9),
            "day");
        RequireReferencedConflict(
            ledger,
            request,
            ReferencedEvent(
                Character,
                childKey.OperationId.Value,
                childKey.ToString(),
                terminalOwnerId: "medical.surgery-secondary"),
            "terminal owner");
        RequireReferencedConflict(
            ledger,
            request,
            ReferencedEvent(
                Character,
                childKey.OperationId.Value,
                childKey.ToString(),
                targetEntityId: "building:qa-referenced-alternate"),
            "target");
        RequireReferencedConflict(
            ledger,
            request,
            ReferencedEvent(
                Character,
                childKey.OperationId.Value,
                childKey.ToString(),
                contributionWorkUnits: 9.5f),
            "contribution");

        GameplayResultKey otherChildKey = new(
            "medical.surgery",
            new GameplayOperationId(
                "surgery-terminal:surgery:qa-referenced-other"),
            1L,
            0);
        WorkCompletionIdentityDeliveryRequest otherChild =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(
                ReferencedEvent(
                    Character,
                    otherChildKey.OperationId.Value,
                    otherChildKey.ToString()));
        WorkCompletionIdentityDeliveryRequest otherWorker =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(
                ReferencedEvent(
                    new CharacterId("character:qa-work-identity-other"),
                    childKey.OperationId.Value,
                    childKey.ToString()));
        WorkCompletionIdentityDeliveryRequest other =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(
                ReferencedEvent(
                    new CharacterId("character:qa-work-identity-other"),
                    otherChildKey.OperationId.Value,
                    otherChildKey.ToString()));
        Require(
            otherChild.DeliveryId != request.DeliveryId
            && otherChild.ProducerStreamId != request.ProducerStreamId
            && otherWorker.DeliveryId != request.DeliveryId
            && otherWorker.ProducerStreamId != request.ProducerStreamId
            && other.DeliveryId != request.DeliveryId
            && other.ProducerStreamId != request.ProducerStreamId,
            "referenced child or worker reused a delivery identity");

        CursorEnvelope roundTrip = JsonUtility.FromJson<CursorEnvelope>(
            JsonUtility.ToJson(new CursorEnvelope
            {
                cursors = ledger.Capture().ToArray()
            }));
        WorkCompletionIdentityDeliveryLedger restored = new();
        restored.Restore(roundTrip.cursors);
        Require(
            restored.Inspect(same, out _) ==
                WorkCompletionIdentityDeliveryStatus.AlreadyApplied,
            "referenced work identity cursor changed across JSON restore");

        WorkCompletionIdentityDeliveryRequest direct =
            ledger.CreateNextDirectRequest(new WorkCompletedIdentityEvent(
                Character,
                "work:craft",
                string.Empty,
                CharacterCommandOrigin.DirectPlayerOrder,
                4));
        Require(
            direct.OperationSequence == 0
            && direct.ProducerStreamId
                == WorkCompletionIdentityDeliveryLedger.DirectEventStreamId
            && direct.DeliveryId
                == WorkCompletionIdentityDeliveryLedger
                    .DirectEventDeliveryPrefix + "0",
            "referenced work identity changed the direct delivery stream");

        RequireArgumentException(
            () => RequireReferencedChild(
                ReferencedEvent(
                    Character,
                    childKey.OperationId.Value,
                    "medical.surgery-not-a-result-key")),
            "invalid referenced child syntax was accepted");
        RequireArgumentException(
            () => RequireReferencedChild(
                ReferencedEvent(
                    Character,
                    childKey.OperationId.Value,
                    "medical.surgery|surgery-terminal:surgery:qa-referenced|01|0")),
            "noncanonical referenced child integer was accepted");
        RequireArgumentException(
            () => RequireReferencedChild(
                ReferencedEvent(
                    Character,
                    "surgery-terminal:surgery:qa-referenced-mismatch",
                    childKey.ToString())),
            "referenced child operation mismatch was accepted");
        RequireArgumentException(
            () => RequireReferencedChild(
                ReferencedEvent(
                    Character,
                    childKey.OperationId.Value,
                    childKey.ToString(),
                    terminalOwnerId:
                        WorkCompletionIdentityReferenceDefaults
                            .LegacyTerminalOwner)),
            "default referenced terminal owner was accepted");
    }

    private static GameplayResultKey RequireReferencedChild(
        WorkCompletedIdentityEvent source)
    {
        WorkCompletionIdentityDeliveryRequest request =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(
                source);
        return WorkCompletionChildReference.Require(
            request.OperationId,
            request.TerminalOwnerId,
            request.ChildResultKey);
    }

    private static WorkCompletedIdentityEvent ReferencedEvent(
        CharacterId character,
        string operationId,
        string childResultKey,
        string workId = "work:qa-referenced",
        string productId = "item:qa-referenced",
        CharacterCommandOrigin origin = CharacterCommandOrigin.Autonomous,
        int day = 8,
        string terminalOwnerId = "medical.surgery",
        string targetEntityId = "building:qa-referenced",
        float contributionWorkUnits = 8.5f) => new(
        character,
        workId,
        productId,
        origin,
        day,
        operationId,
        terminalOwnerId,
        childResultKey,
        targetEntityId,
        contributionWorkUnits);

    private static void RequireReferencedConflict(
        WorkCompletionIdentityDeliveryLedger ledger,
        WorkCompletionIdentityDeliveryRequest expected,
        WorkCompletedIdentityEvent changed,
        string changedField)
    {
        WorkCompletionIdentityDeliveryRequest request =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(
                changed);
        Require(
            request.DeliveryId == expected.DeliveryId
            && request.ProducerStreamId == expected.ProducerStreamId
            && request.OperationSequence == expected.OperationSequence
            && ledger.Inspect(request, out _)
                == WorkCompletionIdentityDeliveryStatus.Conflict,
            "referenced work identity did not reject same-child "
                + changedField + " drift");
    }

    private static void RequireArgumentException(
        Action action,
        string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static WorkCompletionIdentityDeliveryRequest Request(
        string deliveryId,
        string producerStreamId,
        int sequence,
        string workId,
        string productId,
        string skillModifierProvenance =
            WorkCompletionIdentityReferenceDefaults.None) => new(
        deliveryId,
        producerStreamId,
        sequence,
        Character,
        workId,
        productId,
        CharacterCommandOrigin.Autonomous,
        8,
        WorkCompletionIdentityReferenceDefaults.None,
        WorkCompletionIdentityReferenceDefaults.LegacyTerminalOwner,
        WorkCompletionIdentityReferenceDefaults.None,
        WorkCompletionIdentityReferenceDefaults.None,
        0f,
        skillModifierProvenance);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    [Serializable]
    private sealed class CursorEnvelope
    {
        public WorkCompletionIdentityDeliveryCursorSaveData[] cursors =
            Array.Empty<WorkCompletionIdentityDeliveryCursorSaveData>();
    }

    private sealed class FixedDisplayNames :
        IGameplayOutcomeDisplayNameQuery
    {
        public bool TryGetCurrentName(
            GameplayEntityId entityId,
            out KoreanNameSnapshot name)
        {
            if (entityId.Kind.Equals(
                    WorkCompletionIdentityOutcomeIds.CharacterKind)
                && string.Equals(
                    entityId.Value,
                    Character.Value,
                    StringComparison.Ordinal))
            {
                name = new KoreanNameSnapshot(
                    "가람",
                    "work-identity-character-v1",
                    KoreanPronunciationHint.AutoHangulDisplay(
                        "work-identity-pronunciation-v1"),
                    "ko-KR");
                return true;
            }
            name = default;
            return false;
        }
    }
}

internal sealed class WorkIdentityAppliedFactEditorFixture : IDisposable
{
    internal WorkIdentityAppliedFactEditorFixture(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder)
    {
        Facts = new DomainFactDispatcher();
        try
        {
            Mapping = new WorkIdentityAppliedFactMapping(ledger, ledger);
            Facts.Subscribe(
                WorkIdentityAppliedFactMapping.ObserverId,
                new GameplayDomainFactObserver<
                    WorkIdentityAppliedFact,
                    WorkCompletionIdentityOutcomeReceipt>(
                        Mapping.Capture,
                        registry,
                        ledger,
                        recorder,
                        new GameplayDomainFactCommitGroup(recorder, ledger)));
            DeliveryQuery = new GameplayDomainFactDeliveryQuery(recorder, ledger);
        }
        catch
        {
            Facts.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Facts { get; }
    internal WorkIdentityAppliedFactMapping Mapping { get; }
    internal GameplayDomainFactDeliveryQuery DeliveryQuery { get; }

    public void Dispose() => Facts.Dispose();
}
#endif
