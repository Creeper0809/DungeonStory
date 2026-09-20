#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class CaptivePerformerMilestoneOutcomeDebugScenarios
{
    private const string CaptiveId = "character:qa-captive-performer";

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyCanonicalLedgerPath();
        VerifyFailureBoundaries();
        VerifyCaptivitySaveCompatibility();
        if (logSuccess)
            Debug.Log("[Captive Performer Milestone Outcome] PASS");
        return true;
    }

    private static void VerifyCanonicalLedgerPath()
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new CaptivePerformerMilestoneOutcomeDescriptor(
                    new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new CaptivePerformerMilestoneOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = NewLedger(
            registry,
            "run:captive-performer-milestone");
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        CaptivePerformerMilestoneGameplayOutcomeBridge bridge = new(
            recorder,
            ledger);
        RecordingObserver observers = new();
        CaptivePerformerMilestoneOutcomeRuntime runtime = new(
            bridge,
            observers);
        CaptiveState state = PerformerState();

        Require(
            runtime.TryCommitEligibleMilestones(
                state,
                7,
                out string failure),
            "ascending milestone commit failed: " + failure);
        Require(
            state.carePriorityUnlocked
            && state.staffContractUnlocked
            && state.finalContractPending
            && state.performerMilestoneOutcomeRevision == 3L
            && state.carePriorityOutcomeRevision == 1L
            && state.staffContractOutcomeRevision == 2L
            && state.finalContractOutcomeRevision == 3L,
            "milestone state did not commit in 50/75/100 revision order");
        Require(
            observers.Thresholds.SequenceEqual(new[] { 50, 75, 100 })
            && observers.AlertCount == 3,
            "post-commit observers did not receive the ordered milestones");

        GameplayEntityId captive = new(
            CaptivePerformerMilestoneOutcomeIds.CharacterKind,
            CaptiveId);
        GameplayOutcomeQueryPage global = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage character = ledger.GetForEntity(
            captive,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            global.Items.Count == 3
            && character.Items.Count == 3
            && global.Items.All(item => item.Exact != null),
            "global and character queries did not expose three exact milestones");
        int[] thresholds = global.Items
            .Select(item => (int)Metric(
                item.Exact,
                CaptivePerformerMilestoneOutcomeIds.ThresholdMetric.Value))
            .OrderBy(value => value)
            .ToArray();
        Require(
            thresholds.SequenceEqual(new[] { 50, 75, 100 })
            && global.Items.All(item =>
                item.Exact.participants.Count == 1
                && item.Exact.participants[0].displayText == "가람"
                && item.Exact.metrics.Count == 3
                && item.Exact.tags.Single()
                    == CaptivePerformerMilestoneOutcomeIds.PerformerTag.Value),
            "exact milestone receipts lost their frozen identity or typed shape");
        foreach (GameplayOutcomeQueryItem item in global.Items)
        {
            GameplayOutcomeSnapshot exact = item.Exact;
            GameplayOutcomeId outcomeId = new(
                new GameplayOutcomeRunId(exact.runId),
                exact.sequence);
            Require(
                ledger.TryProject(
                    outcomeId,
                    new NarrativePerspectiveContext(
                        captive,
                        NarrativePerspectiveKind.Character,
                        "ko-KR"),
                    out NarrativeView view)
                && view.Text.Contains("가람이", StringComparison.Ordinal)
                && view.Text.Contains("공연 명성", StringComparison.Ordinal),
                "Korean milestone projection was not deterministic");
        }

        CaptiveState replayState = PerformerState();
        Require(
            runtime.TryCommitEligibleMilestones(
                replayState,
                7,
                out string replayFailure),
            "canonical milestone replay failed: " + replayFailure);
        Require(
            replayState.performerMilestoneOutcomeRevision == 3L
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 3
            && observers.Thresholds.Count == 3,
            "canonical replay duplicated outcomes or transient observers");

        CaptivePerformerMilestoneOutcomeReceipt drifted = new(
            CaptiveId,
            Name(),
            50,
            99f,
            1L,
            7);
        Require(
            !bridge.TryPrepare(drifted, out _, out _),
            "same milestone result key accepted a drifted receipt");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = NewLedger(
            registry,
            "run:captive-performer-milestone-restore");
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        string[] beforeHashes = character.Items
            .Select(item => item.Exact.immutablePayloadHash)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] afterHashes = restored.GetForEntity(
                captive,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All)
            .Items.Select(item => item.Exact.immutablePayloadHash)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(
            beforeHashes.SequenceEqual(afterHashes),
            "milestone outcomes changed across ledger save round trip");
    }

    private static void VerifyFailureBoundaries()
    {
        CaptiveState rejected = PerformerState(50f);
        rejected.lastResult = "공연 완료";
        ConfigurableCommitter rejectCommitter = new(
            OwnerOutcomeCommitPhase.Rejected,
            "qa-definite-rejection");
        RecordingObserver rejectObserver = new();
        CaptivePerformerMilestoneOutcomeRuntime rejectRuntime = new(
            rejectCommitter,
            rejectObserver);
        Require(
            !rejectRuntime.TryCommitEligibleMilestones(
                rejected,
                9,
                out string rejectFailure)
            && rejectFailure.Contains("qa-definite-rejection", StringComparison.Ordinal)
            && rejected.performerFame == 50f
            && !rejected.carePriorityUnlocked
            && rejected.performerMilestoneOutcomeRevision == 0L
            && rejected.carePriorityOutcomeRevision == 0L
            && rejected.lastResult == "공연 완료"
            && rejectObserver.Thresholds.Count == 0,
            "definite commit rejection did not roll back only milestone state");

        CaptiveState prepareFailed = PerformerState(50f);
        ConfigurableCommitter prepareFailure = new(
            OwnerOutcomeCommitPhase.PublishedAcknowledged,
            string.Empty,
            prepareSucceeds: false);
        Require(
            !new CaptivePerformerMilestoneOutcomeRuntime(
                    prepareFailure,
                    new RecordingObserver())
                .TryCommitEligibleMilestones(
                    prepareFailed,
                    9,
                    out _)
            && prepareFailed.performerFame == 50f
            && !prepareFailed.carePriorityUnlocked
            && prepareFailed.performerMilestoneOutcomeRevision == 0L,
            "prepare failure changed earned fame or milestone state");

        CaptiveState deliveryPending = PerformerState(50f);
        RecordingObserver pendingObserver = new();
        Require(
            new CaptivePerformerMilestoneOutcomeRuntime(
                    new ConfigurableCommitter(
                        OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                        "qa-delivery-pending"),
                    pendingObserver)
                .TryCommitEligibleMilestones(
                    deliveryPending,
                    9,
                    out string pendingFailure)
            && pendingFailure.Length == 0
            && deliveryPending.carePriorityUnlocked
            && deliveryPending.performerMilestoneOutcomeRevision == 1L
            && pendingObserver.Thresholds.SequenceEqual(new[] { 50 }),
            "durable pending delivery was treated as a rollback");

        CaptiveState observerFault = PerformerState(50f);
        RecordingObserver throwingObserver = new(throwOnNotification: true);
        Require(
            new CaptivePerformerMilestoneOutcomeRuntime(
                    new ConfigurableCommitter(
                        OwnerOutcomeCommitPhase.PublishedAcknowledged,
                        string.Empty),
                    throwingObserver)
                .TryCommitEligibleMilestones(
                    observerFault,
                    9,
                    out string observerFailure)
            && observerFailure.Length == 0
            && observerFault.carePriorityUnlocked
            && observerFault.performerMilestoneOutcomeRevision == 1L,
            "observer exception escaped or rolled back a durable milestone");
    }

    private static void VerifyCaptivitySaveCompatibility()
    {
        CaptiveState committed = PerformerState();
        committed.carePriorityUnlocked = true;
        committed.staffContractUnlocked = true;
        committed.finalContractPending = true;
        committed.performerMilestoneOutcomeRevision = 3L;
        committed.carePriorityOutcomeRevision = 1L;
        committed.staffContractOutcomeRevision = 2L;
        committed.finalContractOutcomeRevision = 3L;
        CaptivitySaveData source = Save(committed);
        CaptivitySaveData roundTrip = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(source));
        DungeonGameRestoreReport roundTripReport = new();
        CaptivitySaveValidation.Validate(roundTrip, roundTripReport);
        Require(
            roundTripReport.Success
            && roundTrip.captives.Single().performerMilestoneOutcomeRevision == 3L
            && roundTrip.captives.Single().finalContractOutcomeRevision == 3L,
            "captivity save round trip lost milestone revisions: "
            + string.Join(" | ", roundTripReport.Errors));

        CaptiveState legacy = PerformerState();
        legacy.carePriorityUnlocked = true;
        legacy.staffContractUnlocked = true;
        legacy.finalContractPending = true;
        DungeonGameRestoreReport legacyReport = new();
        CaptivitySaveValidation.Validate(Save(legacy), legacyReport);
        Require(
            legacyReport.Success,
            "legacy zero-revision milestone flags were rejected: "
            + string.Join(" | ", legacyReport.Errors));

        CaptiveState negative = committed.Clone();
        negative.carePriorityOutcomeRevision = -1L;
        DungeonGameRestoreReport negativeReport = new();
        CaptivitySaveValidation.Validate(Save(negative), negativeReport);
        Require(
            !negativeReport.Success,
            "negative milestone revision was accepted");

        CaptiveState torn = committed.Clone();
        torn.finalContractOutcomeRevision = 0L;
        DungeonGameRestoreReport tornReport = new();
        CaptivitySaveValidation.Validate(Save(torn), tornReport);
        Require(
            !tornReport.Success,
            "torn milestone high-water revision was accepted");
    }

    private static GameplayOutcomeLedger NewLedger(
        IGameplayOutcomeRegistry registry,
        string runId) => new(
        registry,
        new GameplayOutcomeBufferLimits(
            smallPageCount: 8,
            largePageCount: 0,
            knownResultKeyCapacity: 128),
        new GameplayOutcomeRunId(runId),
        1L);

    private static CaptiveState PerformerState(float fame = 100f) => new()
    {
        captiveId = CaptiveId,
        displayName = "가람",
        status = CaptivityStatus.Released,
        policyId = CaptivityPolicyIds.Standard,
        performerFame = fame,
        performerSkill = 40f,
        privilegeTier = fame >= 75f ? 2 : fame >= 50f ? 1 : 0
    };

    private static CaptivitySaveData Save(CaptiveState state) => new()
    {
        captives = new List<CaptiveState> { state },
        policies = new List<CaptivePolicyData>
        {
            new()
            {
                policyId = CaptivityPolicyIds.Standard,
                displayName = "표준 수용"
            }
        }
    };

    private static KoreanNameSnapshot Name()
    {
        string revision = "captivity-performer-display-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(CaptiveId + "|가람");
        return new KoreanNameSnapshot(
            "가람",
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(
                "captivity-performer-pronunciation-v1:" + CaptiveId),
            "ko-KR");
    }

    private static double Metric(GameplayOutcomeSnapshot snapshot, string metricId) =>
        snapshot.metrics.Single(value => value.metricId == metricId).value;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class ConfigurableCommitter :
        ICaptivePerformerMilestoneOutcomeCommitter
    {
        private readonly OwnerOutcomeCommitPhase phase;
        private readonly string detail;
        private readonly bool prepareSucceeds;

        public ConfigurableCommitter(
            OwnerOutcomeCommitPhase phase,
            string detail,
            bool prepareSucceeds = true)
        {
            this.phase = phase;
            this.detail = detail ?? string.Empty;
            this.prepareSucceeds = prepareSucceeds;
        }

        public bool TryPrepare(
            in CaptivePerformerMilestoneOutcomeReceipt receipt,
            out PreparedCaptivePerformerMilestoneOutcome prepared,
            out string failureReason)
        {
            if (!prepareSucceeds)
            {
                prepared = default;
                failureReason = "qa-prepare-rejected";
                return false;
            }
            prepared = new PreparedCaptivePerformerMilestoneOutcome(
                default,
                receipt.ResultKey,
                receipt.OwnerRevision,
                false);
            failureReason = string.Empty;
            return true;
        }

        public OwnerOutcomeCommitResult Commit(
            in PreparedCaptivePerformerMilestoneOutcome prepared) => new(
            phase,
            prepared.ResultKey,
            default,
            string.Empty,
            detail);

        public void Cancel(in PreparedCaptivePerformerMilestoneOutcome prepared)
        {
        }
    }

    private sealed class RecordingObserver : ICaptivityPerformerPort
    {
        private readonly bool throwOnNotification;

        public RecordingObserver(bool throwOnNotification = false) =>
            this.throwOnNotification = throwOnNotification;

        public List<int> Thresholds { get; } = new();
        public int AlertCount { get; private set; }
        public bool IsActorAvailable(string captiveId) => true;
        public void ApplyAssignmentState(string captiveId, bool assigned)
        {
        }

        public void Publish(CaptivePerformerMilestoneEvent gameEvent)
        {
            if (throwOnNotification)
                throw new InvalidOperationException("qa-event-observer-fault");
            Thresholds.Add(gameEvent.FameThreshold);
        }

        public void RaiseAlert(
            string title,
            string message,
            CaptivityMilestoneImportance importance,
            string category)
        {
            if (throwOnNotification)
                throw new InvalidOperationException("qa-alert-observer-fault");
            AlertCount++;
        }
    }
}
#endif
