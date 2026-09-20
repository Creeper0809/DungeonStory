#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class DungeonSpaceExpansionOutcomeDebugScenarios
{
    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new();
        Run("publish-query-josa-replay-save", VerifyPublishQueryReplayAndSave, failures);
        Run("delivery-fault-retains-outbox", VerifyDeliveryFaultRetainsOutbox, failures);
        if (failures.Count > 0)
        {
            foreach (string failure in failures)
                Debug.LogError("Dungeon-space expansion outcome failure: " + failure);
            return false;
        }
        if (logSuccess)
            Debug.Log("Dungeon-space expansion outcome scenarios passed.");
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
        DungeonSpaceExpansionOutcomeReceipt exact = CreateReceipt(29);
        if (!fixture.Bridge.TryPrepare(
                exact,
                out PreparedDungeonSpaceExpansionOutcome prepared,
                out string prepareFailure))
        {
            Debug.LogError("Dungeon-space outcome prepare failed: " + prepareFailure);
            return false;
        }
        OwnerOutcomeCommitResult committed = fixture.Bridge.Commit(prepared);
        if (!committed.DurablyCommitted)
        {
            Debug.LogError("Dungeon-space outcome commit failed: " + committed.DetailCode);
            return false;
        }

        GameplayOutcomeQueryPage global = fixture.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage dungeonPage = fixture.Ledger.GetForEntity(
            DungeonSpaceExpansionOutcomeIds.MainDungeonSpace,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        if (global.Items.Count != 1
            || dungeonPage.Items.Count != 1
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
                    default,
                    NarrativePerspectiveKind.Global,
                    "ko-KR"),
                out NarrativeView view)
            && view.Text.Contains("던전 구역은", StringComparison.Ordinal)
            && view.Text.Contains("29칸에서 51칸", StringComparison.Ordinal)
            && view.Text.Contains("22칸", StringComparison.Ordinal);

        bool replayPrepared = fixture.Bridge.TryPrepare(
            exact,
            out PreparedDungeonSpaceExpansionOutcome replay,
            out _);
        OwnerOutcomeCommitResult replayCommit = replayPrepared
            ? fixture.Bridge.Commit(replay)
            : default;
        DungeonSpaceExpansionOutcomeReceipt conflicting = CreateReceipt(27);
        bool conflictRejected = !fixture.Bridge.TryPrepare(
            conflicting,
            out _,
            out _);

        GameplayOutcomeLedgerSaveData save = fixture.Ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = fixture.CreateLedger(
            "run:dungeon-space-expansion-restored");
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(save);
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        GameplayOutcomeQueryPage restoredPage = restored.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);

        return projected
            && snapshot.ownerRevision == exact.OwnerRevision
            && snapshot.commitRevision == exact.OwnerRevision
            && string.Equals(
                snapshot.outcomeTypeId,
                DungeonSpaceExpansionOutcomeIds.Expanded.Value,
                StringComparison.Ordinal)
            && replayPrepared
            && replay.IsReplay
            && replayCommit.DurablyCommitted
            && conflictRejected
            && fixture.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1
            && save.exactOutcomes.Count == 1
            && restoredPage.Items.Count == 1
            && restoredPage.Items[0].Exact?.immutablePayloadHash
                == snapshot.immutablePayloadHash;
    }

    private static bool VerifyDeliveryFaultRetainsOutbox()
    {
        using Fixture fixture = new(rejectDelivery: true);
        DungeonSpaceExpansionOutcomeReceipt exact = CreateReceipt(29);
        bool prepared = fixture.Bridge.TryPrepare(
            exact,
            out PreparedDungeonSpaceExpansionOutcome token,
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

    private static DungeonSpaceExpansionOutcomeReceipt CreateReceipt(
        int previousColumns) => new(
        DungeonSpaceExpansionCatalog.QuarryResearchId,
        DungeonSpaceExpansionOutcomeNames.Snapshot(
            DungeonSpaceExpansionCatalog.QuarryResearchId,
            "채석장"),
        51L,
        1,
        previousColumns,
        51,
        60,
        68,
        3,
        17,
        0);

    private sealed class Fixture : IDisposable
    {
        private readonly GameplayOutcomeBufferLimits limits = new(
            smallPageCount: 8,
            largePageCount: 0,
            knownResultKeyCapacity: 128);

        internal Fixture(bool rejectDelivery = false)
        {
            Events = new GameEventBus();
            DungeonSpaceExpansionOutcomeDescriptor descriptor = new(
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
                    new DungeonSpaceExpansionOutcomeAdapter()
                });
            Ledger = CreateLedger("run:dungeon-space-expansion-test");
            Recorder = new GameplayOutcomeRecorder(Ledger, Registry, Events);
            Bridge = new DungeonSpaceExpansionGameplayOutcomeBridge(
                Recorder,
                Ledger);
        }

        internal GameEventBus Events { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal DungeonSpaceExpansionGameplayOutcomeBridge Bridge { get; }

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
                    "injected-dungeon-space-delivery-rejection");
        }
    }
}

internal sealed class EditorDungeonSpaceExpansionOutcomeCommitter :
    IDungeonSpaceExpansionOutcomeCommitter
{
    private int remainingPrepareDeferrals;

    internal EditorDungeonSpaceExpansionOutcomeCommitter(
        bool rejectCommit = false,
        int deferPrepareCount = 0)
    {
        if (deferPrepareCount < 0)
            throw new ArgumentOutOfRangeException(nameof(deferPrepareCount));
        RejectCommit = rejectCommit;
        remainingPrepareDeferrals = deferPrepareCount;
    }

    internal bool RejectCommit { get; }
    internal int PrepareCount { get; private set; }
    internal int CommitCount { get; private set; }
    internal int CancelCount { get; private set; }
    internal DungeonSpaceExpansionOutcomeReceipt LastReceipt { get; private set; }

    public bool TryPrepare(
        in DungeonSpaceExpansionOutcomeReceipt receipt,
        out PreparedDungeonSpaceExpansionOutcome prepared,
        out string failureReason)
    {
        PrepareCount++;
        LastReceipt = receipt;
        if (remainingPrepareDeferrals > 0)
        {
            remainingPrepareDeferrals--;
            prepared = default;
            failureReason = "injected-dungeon-space-prepare-deferral";
            return false;
        }
        prepared = new PreparedDungeonSpaceExpansionOutcome(
            default,
            receipt.ResultKey,
            receipt.OwnerRevision,
            false);
        failureReason = string.Empty;
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedDungeonSpaceExpansionOutcome prepared)
    {
        CommitCount++;
        return new OwnerOutcomeCommitResult(
            RejectCommit
                ? OwnerOutcomeCommitPhase.Rejected
                : OwnerOutcomeCommitPhase.PublishedAcknowledged,
            prepared.ResultKey,
            default,
            string.Empty,
            RejectCommit ? "injected-dungeon-space-commit-rejection" : string.Empty);
    }

    public void Cancel(in PreparedDungeonSpaceExpansionOutcome prepared) =>
        CancelCount++;
}

internal sealed class EditorBlueprintResearchStateService :
    IBlueprintResearchStateService
{
    private readonly BlueprintResearchState state;

    internal EditorBlueprintResearchStateService(BlueprintResearchState state = null) =>
        this.state = state ?? new BlueprintResearchState();

    public BlueprintResearchState GetState() => state;
}
#endif
