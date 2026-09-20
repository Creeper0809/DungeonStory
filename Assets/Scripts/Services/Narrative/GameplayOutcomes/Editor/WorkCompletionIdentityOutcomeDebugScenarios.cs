#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class WorkCompletionIdentityOutcomeDebugScenarios
{
    private static readonly CharacterId Character =
        new("character:qa-work-identity");

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyDirectSequenceAndRestore();

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
                largePageCount: 0,
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
            new FixedDisplayNames());
        WorkCompletionIdentityDeliveryRequest request = Request(
            "identity-event:qa-work-identity:000000",
            "qa-work-identity:stream",
            0,
            BuiltInWorkTypeIds.Harvest.Value,
            "item:qa-harvest");

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
                && value.value == "completed-work"),
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
            request.ProductId);
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
                largePageCount: 0,
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
        Require(
            ledger.Commit(
                first,
                out string commitFailure,
                WorkCompletionIdentityDeliveryDisposition
                    .EffectsAppliedWithOutcome)
                == WorkCompletionIdentityDeliveryStatus.Applied,
            "direct work identity cursor commit failed: " + commitFailure);
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

    private static WorkCompletionIdentityDeliveryRequest Request(
        string deliveryId,
        string producerStreamId,
        int sequence,
        string workId,
        string productId) => new(
        deliveryId,
        producerStreamId,
        sequence,
        Character,
        workId,
        productId,
        CharacterCommandOrigin.Autonomous,
        8);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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
#endif
