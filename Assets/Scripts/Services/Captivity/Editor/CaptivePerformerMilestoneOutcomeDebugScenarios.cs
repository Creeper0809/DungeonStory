#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
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
        RecordingObserver observers = new();
        using MilestoneFixture fixture = new(
            "run:captive-performer-milestone",
            observers);
        CaptivePerformerMilestoneOutcomeRuntime runtime = fixture.Runtime;
        GameplayOutcomeLedger ledger = fixture.Ledger;
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
            && state.finalContractOutcomeRevision == 3L
            && state.performerMilestones.Count == 3
            && state.performerMilestones.All(value => !value.pending),
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
            && replayState.performerMilestones.Count == 3
            && replayState.performerMilestones.All(value => !value.pending)
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 3
            && observers.Thresholds.Count == 3,
            "canonical replay duplicated outcomes or transient observers");

        CaptivePerformerMilestoneUnlockedFact drifted = new(
            new CaptivePerformerMilestoneFactData
            {
                captiveId = CaptiveId,
                displayName = "가람",
                threshold = 50,
                fame = 99f,
                revision = 1L,
                absoluteDay = 7
            });
        Require(
            !fixture.Facts.Replay(drifted).MayBeCommitted,
            "same milestone result key accepted a drifted receipt");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = NewLedger(
            fixture.Registry,
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
        RecordingObserver rejectObserver = new();
        using (MilestoneFixture fixture = new(
                   "run:captive-performer-milestone-reject",
                   rejectObserver,
                   StageBehavior.RejectCommit))
        {
            Require(
                !fixture.Runtime.TryCommitEligibleMilestones(
                    rejected,
                    9,
                    out string rejectFailure)
                && rejectFailure.Contains(
                    "qa-definite-rejection",
                    StringComparison.Ordinal)
                && rejected.performerFame == 50f
                && !rejected.carePriorityUnlocked
                && rejected.performerMilestoneOutcomeRevision == 0L
                && rejected.carePriorityOutcomeRevision == 0L
                && rejected.performerMilestones.Count == 0
                && rejected.lastResult == "공연 완료"
                && rejectObserver.Thresholds.Count == 0,
                "definite commit rejection did not roll back only milestone state");
        }

        CaptiveState prepareFailed = PerformerState(50f);
        using (MilestoneFixture fixture = new(
                   "run:captive-performer-milestone-prepare-reject",
                   new RecordingObserver(),
                   StageBehavior.RejectPrepare))
        {
            Require(
                !fixture.Runtime.TryCommitEligibleMilestones(
                    prepareFailed,
                    9,
                    out _)
                && prepareFailed.performerFame == 50f
                && !prepareFailed.carePriorityUnlocked
                && prepareFailed.performerMilestoneOutcomeRevision == 0L
                && prepareFailed.performerMilestones.Count == 0,
                "prepare failure changed earned fame or milestone state");
        }

        CaptiveState deliveryPending = PerformerState(50f);
        RecordingObserver pendingObserver = new();
        using (MilestoneFixture fixture = new(
                   "run:captive-performer-milestone-pending",
                   pendingObserver,
                   StageBehavior.PendingDelivery))
        {
            Require(
                fixture.Runtime.TryCommitEligibleMilestones(
                    deliveryPending,
                    9,
                    out string pendingFailure)
                && pendingFailure.Contains(
                    "captive-milestone-delivery-pending",
                    StringComparison.Ordinal)
                && deliveryPending.carePriorityUnlocked
                && deliveryPending.performerMilestoneOutcomeRevision == 1L
                && deliveryPending.performerMilestones.Count == 1
                && deliveryPending.performerMilestones[0].pending
                && deliveryPending.performerMilestones[0].observersPending
                && pendingObserver.Thresholds.Count == 0,
                "durable pending delivery was treated as a rollback or notified early");
            Require(
                fixture.Runtime.TryCommitNextPendingMilestone(
                    new[] { deliveryPending },
                    9,
                    out bool attempted,
                    out string recoveryFailure)
                && attempted
                && recoveryFailure.Length == 0
                && !deliveryPending.performerMilestones[0].pending
                && !deliveryPending.performerMilestones[0].observersPending
                && pendingObserver.Thresholds.SequenceEqual(new[] { 50 })
                && fixture.Runtime.TryCommitNextPendingMilestone(
                    new[] { deliveryPending },
                    9,
                    out bool repeated,
                    out _)
                && !repeated
                && pendingObserver.Thresholds.SequenceEqual(new[] { 50 }),
                "pending milestone recovery did not notify exactly once");
        }

        CaptiveState observerFault = PerformerState(50f);
        RecordingObserver throwingObserver = new(throwOnNotification: true);
        using (MilestoneFixture fixture = new(
                   "run:captive-performer-milestone-observer-fault",
                   throwingObserver))
        {
            Require(
                fixture.Runtime.TryCommitEligibleMilestones(
                    observerFault,
                    9,
                    out string observerFailure)
                && observerFailure.Length == 0
                && observerFault.carePriorityUnlocked
                && observerFault.performerMilestoneOutcomeRevision == 1L
                && !observerFault.performerMilestones.Single().pending,
                "observer exception escaped or rolled back a durable milestone");
        }
    }

    private static void VerifyCaptivitySaveCompatibility()
    {
        CaptiveState committed = PerformerState();
        using (MilestoneFixture fixture = new(
                   "run:captive-performer-milestone-save",
                   new RecordingObserver()))
        {
            Require(
                fixture.Runtime.TryCommitEligibleMilestones(
                    committed,
                    7,
                    out string commitFailure),
                "save fixture could not commit milestone observations: "
                + commitFailure);
        }
        CaptivitySaveData source = Save(committed);
        CaptivitySaveData roundTrip = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(source));
        DungeonGameRestoreReport roundTripReport = new();
        CaptivitySaveValidation.Validate(roundTrip, roundTripReport);
        Require(
            roundTripReport.Success
            && roundTrip.captives.Single().performerMilestoneOutcomeRevision == 3L
            && roundTrip.captives.Single().finalContractOutcomeRevision == 3L
            && roundTrip.captives.Single().performerMilestones.Count == 3,
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

    private static double Metric(GameplayOutcomeSnapshot snapshot, string metricId) =>
        snapshot.metrics.Single(value => value.metricId == metricId).value;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private enum StageBehavior
    {
        None,
        RejectPrepare,
        RejectCommit,
        PendingDelivery
    }

    private sealed class MilestoneFixture : IDisposable
    {
        private readonly IDisposable observation;

        public MilestoneFixture(
            string runId,
            RecordingObserver observers,
            StageBehavior behavior = StageBehavior.None)
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptivePerformerMilestoneOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptivePerformerMilestoneOutcomeAdapter()
                });
            Ledger = NewLedger(Registry, runId);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                Registry,
                new GameEventBus());
            Facts = new DomainFactDispatcher();
            observation = Facts.Subscribe(
                "captive-performer-milestone:fixture",
                new GameplayDomainFactObserver<
                    CaptivePerformerMilestoneUnlockedFact,
                    CaptivePerformerMilestoneOutcomeReceipt>(
                    CaptivePerformerMilestoneFactMapping.Capture,
                    Registry,
                    Ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, Ledger)));
            IDomainFactStagingPublisher publisher = behavior == StageBehavior.None
                ? Facts
                : new ConfigurableStagingPublisher(Facts, behavior);
            Runtime = new CaptivePerformerMilestoneOutcomeRuntime(
                publisher,
                Facts,
                observers ?? throw new ArgumentNullException(nameof(observers)));
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public DomainFactDispatcher Facts { get; }
        public CaptivePerformerMilestoneOutcomeRuntime Runtime { get; }

        public void Dispose()
        {
            observation.Dispose();
            Facts.Dispose();
        }
    }

    private sealed class ConfigurableStagingPublisher :
        IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly StageBehavior behavior;

        public ConfigurableStagingPublisher(
            IDomainFactStagingPublisher inner,
            StageBehavior behavior)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.behavior = behavior;
        }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            if (behavior == StageBehavior.RejectPrepare)
            {
                stage = null;
                failureReason = "qa-prepare-rejected";
                return false;
            }
            if (!inner.TryStage(fact, out stage, out failureReason))
                return false;
            stage = new ConfigurableStage(stage, behavior);
            return true;
        }
    }

    private sealed class ConfigurableStage : IDomainFactStage
    {
        private readonly IDomainFactStage inner;
        private readonly StageBehavior behavior;

        public ConfigurableStage(
            IDomainFactStage inner,
            StageBehavior behavior)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.behavior = behavior;
        }

        public string OperationKey => inner.OperationKey;
        public long OwnerRevision => inner.OwnerRevision;
        public bool IsReplay => inner.IsReplay;
        public bool IsActive => inner.IsActive;

        public DomainFactDelivery Commit()
        {
            if (behavior == StageBehavior.RejectCommit)
            {
                inner.Cancel();
                return DomainFactDelivery.Reject("qa-definite-rejection");
            }
            DomainFactDelivery delivery = inner.Commit();
            return behavior == StageBehavior.PendingDelivery
                && delivery.MayBeCommitted
                    ? new DomainFactDelivery(
                        DomainFactDeliveryPhase.AcceptedPendingDelivery,
                        delivery.OperationKey,
                        delivery.PublicationId,
                        delivery.Fingerprint,
                        "qa-delivery-pending")
                    : delivery;
        }

        public void Cancel() => inner.Cancel();
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
