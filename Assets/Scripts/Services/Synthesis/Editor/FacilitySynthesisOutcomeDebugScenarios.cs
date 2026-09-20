#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class FacilitySynthesisOutcomeDebugScenarios
{
    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new();
        Run("publish-query-josa-replay-save", VerifyPublishQueryReplayAndSave, failures);
        Run("delivery-fault-retains-outbox", VerifyDeliveryFaultRetainsOutbox, failures);
        if (failures.Count > 0)
        {
            foreach (string failure in failures)
                Debug.LogError("Facility synthesis outcome failure: " + failure);
            return false;
        }
        if (logSuccess)
            Debug.Log("Facility synthesis outcome scenarios passed.");
        return true;
    }

    private static void Run(
        string name,
        Func<bool> scenario,
        ICollection<string> failures)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        failures.Add(name);
    }

    private static bool VerifyPublishQueryReplayAndSave()
    {
        using Fixture fixture = new();
        FacilitySynthesisOutcomeReceipt exact = CreateReceipt(2);
        if (!fixture.Bridge.TryPrepare(
                exact,
                out PreparedFacilitySynthesisOutcome prepared,
                out string prepareFailure))
        {
            Debug.LogError("Synthesis outcome prepare failed: " + prepareFailure);
            return false;
        }
        OwnerOutcomeCommitResult committed = fixture.Bridge.Commit(prepared);
        if (!committed.DurablyCommitted)
        {
            Debug.LogError("Synthesis outcome commit failed: " + committed.DetailCode);
            return false;
        }

        GameplayEntityId survivor = new(
            FacilitySynthesisOutcomeIds.FacilityKind,
            exact.SurvivorFacilityId);
        GameplayEntityId consumed = new(
            FacilitySynthesisOutcomeIds.FacilityKind,
            exact.MaterialPersistentIds[1]);
        GameplayOutcomeQueryPage global = fixture.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage survivorPage = fixture.Ledger.GetForEntity(
            survivor,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage consumedPage = fixture.Ledger.GetForEntity(
            consumed,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        if (global.Items.Count != 1
            || survivorPage.Items.Count != 1
            || consumedPage.Items.Count != 1
            || global.Items[0].Exact == null)
        {
            return false;
        }
        GameplayOutcomeSnapshot snapshot = global.Items[0].Exact;
        GameplayOutcomeId outcomeId = new(
            new GameplayOutcomeRunId(snapshot.runId),
            snapshot.sequence);
        bool projected = fixture.Ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    survivor,
                    NarrativePerspectiveKind.Facility,
                    "ko-KR"),
                out NarrativeView survivorView)
            && fixture.Ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    consumed,
                    NarrativePerspectiveKind.Facility,
                    "ko-KR"),
                out NarrativeView consumedView)
            && survivorView.Text.Contains("대장간은", StringComparison.Ordinal)
            && consumedView.Text.Contains("작업대가", StringComparison.Ordinal);

        bool replayPrepared = fixture.Bridge.TryPrepare(
            exact,
            out PreparedFacilitySynthesisOutcome replay,
            out _);
        OwnerOutcomeCommitResult replayCommit = replayPrepared
            ? fixture.Bridge.Commit(replay)
            : default;
        FacilitySynthesisOutcomeReceipt conflicting = CreateReceipt(3);
        bool conflictRejected = !fixture.Bridge.TryPrepare(
            conflicting,
            out _,
            out _);

        GameplayOutcomeLedgerSaveData save = fixture.Ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = fixture.CreateLedger("run:facility-synthesis-restored");
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(save);
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        GameplayOutcomeQueryPage restoredPage = restored.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        FacilityRuntimeState legacyState = new();
        FacilityRuntimeState invalidState = new()
        {
            synthesisOutcomeRevision = -1L
        };

        return projected
            && snapshot.ownerRevision == exact.OwnerRevision
            && snapshot.commitRevision == exact.OwnerRevision
            && string.Equals(
                snapshot.outcomeTypeId,
                FacilitySynthesisOutcomeIds.Completed.Value,
                StringComparison.Ordinal)
            && replayPrepared
            && replayCommit.DurablyCommitted
            && conflictRejected
            && fixture.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1
            && save.exactOutcomes.Count == 1
            && restoredPage.Items.Count == 1
            && restoredPage.Items[0].Exact?.immutablePayloadHash
                == snapshot.immutablePayloadHash
            && legacyState.IsValid(out _)
            && !invalidState.IsValid(out _);
    }

    private static bool VerifyDeliveryFaultRetainsOutbox()
    {
        using Fixture fixture = new(rejectDelivery: true);
        FacilitySynthesisOutcomeReceipt exact = CreateReceipt(2);
        bool prepared = fixture.Bridge.TryPrepare(
            exact,
            out PreparedFacilitySynthesisOutcome token,
            out _);
        OwnerOutcomeCommitResult committed = prepared
            ? fixture.Bridge.Commit(token)
            : default;
        IReadOnlyList<GameplayOutcomeOutboxSnapshot> outbox =
            fixture.Ledger.GetOutboxSnapshot();
        return prepared
            && committed.Phase == OwnerOutcomeCommitPhase.CommittedPendingDelivery
            && fixture.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 0
            && outbox.Count == 1
            && outbox[0].Lifecycle ==
                GameplayOutcomeRecordLifecycle.DeliveryFaultPending;
    }

    private static FacilitySynthesisOutcomeReceipt CreateReceipt(int level)
    {
        string survivor = "building:synthesis-a";
        string consumed = "building:synthesis-b";
        return new FacilitySynthesisOutcomeReceipt(
            survivor,
            1L,
            "recipe_commercial_grill",
            "상업 조리대 조합",
            "building:103",
            FacilitySynthesisOutcomeNames.Snapshot(survivor, "대장간"),
            new[] { survivor, consumed },
            new[] { "building:101", "building:102" },
            new[]
            {
                FacilitySynthesisOutcomeNames.Snapshot(survivor, "가마"),
                FacilitySynthesisOutcomeNames.Snapshot(consumed, "작업대")
            },
            level,
            3,
            4,
            7);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameplayOutcomeBufferLimits limits = new(
            smallPageCount: 8,
            largePageCount: 0,
            knownResultKeyCapacity: 128);

        internal Fixture(bool rejectDelivery = false)
        {
            Events = new GameEventBus();
            FacilitySynthesisOutcomeDescriptor descriptor = new(
                new KoreanJosaFormatter());
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    rejectDelivery
                        ? new RejectingDeliveryDescriptor(descriptor)
                        : descriptor
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new FacilitySynthesisOutcomeAdapter()
                });
            Ledger = CreateLedger("run:facility-synthesis-test");
            Recorder = new GameplayOutcomeRecorder(Ledger, Registry, Events);
            Bridge = new FacilitySynthesisGameplayOutcomeBridge(
                Recorder,
                Ledger);
        }

        internal GameEventBus Events { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal FacilitySynthesisGameplayOutcomeBridge Bridge { get; }

        internal GameplayOutcomeLedger CreateLedger(string runId) => new(
            Registry,
            limits,
            new GameplayOutcomeRunId(runId),
            1L);

        public void Dispose()
        {
        }
    }

    private sealed class RejectingDeliveryDescriptor : IGameplayOutcomeDescriptor
    {
        private readonly IGameplayOutcomeDescriptor inner;
        private int validationCount;

        internal RejectingDeliveryDescriptor(IGameplayOutcomeDescriptor inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public GameplayOutcomeTypeId OutcomeTypeId => inner.OutcomeTypeId;
        public INarrativePerspectiveProjector PerspectiveProjector =>
            inner.PerspectiveProjector;
        public IOutcomeMemoryPolicy MemoryPolicy => inner.MemoryPolicy;
        public IOutcomePerceptionPolicy PerceptionPolicy => inner.PerceptionPolicy;
        public IOutcomeMemoryConsolidator MemoryConsolidator =>
            inner.MemoryConsolidator;
        public bool IsKnownRole(GameplayRoleId roleId) => inner.IsKnownRole(roleId);
        public bool IsKnownMetric(
            GameplayMetricId metricId,
            GameplayMetricUnitId unitId) => inner.IsKnownMetric(metricId, unitId);

        public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
        {
            OutcomeValidationResult actual = inner.Validate(outcome);
            if (!actual.Valid) return actual;
            validationCount++;
            return validationCount == 1
                ? actual
                : OutcomeValidationResult.Reject(
                    "injected-facility-synthesis-delivery-rejection");
        }
    }
}
#endif
