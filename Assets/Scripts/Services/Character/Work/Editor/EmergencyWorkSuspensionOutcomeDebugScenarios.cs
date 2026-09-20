#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static class EmergencyWorkSuspensionOutcomeDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run Emergency Work Suspension Outcome Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
            Debug.LogError("Emergency work suspension outcome scenarios failed.");
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new();
        Run("publish-query-josa-replay-save", VerifyPublishQueryReplayAndSave, failures);
        Run("commit-rejection-rolls-back", VerifyCommitRejectionRollsBack, failures);
        Run("delivery-fault-retains-state", VerifyDeliveryFaultRetainsState, failures);
        if (failures.Count > 0)
        {
            foreach (string failure in failures)
                Debug.LogError(failure);
            return false;
        }
        if (logSuccess)
            Debug.Log("Emergency work suspension outcome scenarios passed.");
        return true;
    }

    private static void Run(
        string name,
        Func<bool> scenario,
        ICollection<string> failures)
    {
        try
        {
            if (scenario())
                return;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        failures.Add(name);
    }

    private static bool VerifyPublishQueryReplayAndSave()
    {
        using Fixture world = new();
        SettlementSuspendedWorkSnapshot exact = world.CreateSnapshot(2f, 10f);
        EmergencyAccountingResult recorded = world.Runtime.RecordSuspendedWork(exact);
        if (!recorded.Success)
        {
            Debug.LogError(
                "Emergency suspension normal record failed: "
                + recorded.Code + ":" + recorded.Message);
            return false;
        }

        GameplayEntityId worker = new(
            EmergencyWorkSuspensionOutcomeIds.CharacterKind,
            exact.CharacterId);
        GameplayEntityId facility = new(
            EmergencyWorkSuspensionOutcomeIds.FacilityKind,
            exact.TargetBuildingId);
        GameplayOutcomeQueryPage global = world.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage workerPage = world.Ledger.GetForEntity(
            worker,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage facilityPage = world.Ledger.GetForEntity(
            facility,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        if (global.Items.Count != 1
            || workerPage.Items.Count != 1
            || facilityPage.Items.Count != 1
            || global.Items[0].Exact == null
            || workerPage.Items[0].Exact == null
            || facilityPage.Items[0].Exact == null)
        {
            return false;
        }
        GameplayOutcomeSnapshot snapshot = global.Items[0].Exact;
        GameplayOutcomeId outcomeId = new(
            new GameplayOutcomeRunId(snapshot.runId),
            snapshot.sequence);
        bool projected = world.Ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    worker,
                    NarrativePerspectiveKind.Character,
                    "ko-KR"),
                out NarrativeView workerView)
            && world.Ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    facility,
                    NarrativePerspectiveKind.Facility,
                    "ko-KR"),
                out NarrativeView facilityView)
            && workerView.Text.Contains("대장간을", StringComparison.Ordinal)
            && facilityView.Text.Contains("민준이", StringComparison.Ordinal)
            && facilityView.Text.Contains("대장간을", StringComparison.Ordinal);
        EmergencyAccountingResult replay = world.Runtime.RecordSuspendedWork(exact);
        SettlementSuspendedWorkSnapshot conflicting = world.CreateSnapshot(3f, 10f);
        EmergencyAccountingResult conflict = world.Runtime.RecordSuspendedWork(conflicting);
        world.Runtime.TryGetSuspendedWork(
            exact.CharacterId,
            out SettlementSuspendedWorkSnapshot current);

        DungeonStory.Infrastructure.SettlementThreatAlertSaveData saved =
            world.Runtime.CaptureAlertSaveData();
        SettlementAlertRuntime restored = world.CreateRuntime();
        restored.RestoreAlertSaveData(saved);
        bool roundTrip = restored.TryGetSuspendedWork(
                exact.CharacterId,
                out SettlementSuspendedWorkSnapshot restoredRow)
            && restoredRow.OutcomeOwnerRevision == 1L;

        saved.suspendedWork[0].outcomeOwnerRevision = 0L;
        SettlementAlertRuntime legacy = world.CreateRuntime();
        legacy.RestoreAlertSaveData(saved);
        bool legacyPreserved = legacy.TryGetSuspendedWork(
                exact.CharacterId,
                out SettlementSuspendedWorkSnapshot legacyRow)
            && legacyRow.OutcomeOwnerRevision == 0L;
        saved.suspendedWork[0].outcomeOwnerRevision = -1L;
        IReadOnlyList<string> validationErrors =
            DungeonStory.Operation.EventAlertPayloadValidation.Validate(
                new DungeonStory.Infrastructure.DungeonEventAlertSaveData
                {
                    threatAlert = saved
                });
        bool negativeRejected = validationErrors.Count > 0;

        bool valid = projected
            && snapshot.sequence == workerPage.Items[0].Exact.sequence
            && snapshot.sequence == facilityPage.Items[0].Exact.sequence
            && snapshot.ownerRevision == 1L
            && snapshot.commitRevision == 1L
            && string.Equals(
                snapshot.outcomeTypeId,
                EmergencyWorkSuspensionOutcomeIds.SuspensionRecorded.Value,
                StringComparison.Ordinal)
            && replay.Success
            && !conflict.Success
            && current.InlineCompletedWork.Equals(2f)
            && world.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1
            && roundTrip
            && legacyPreserved
            && negativeRejected;
        if (!valid)
        {
            Debug.LogError(
                "Emergency suspension normal detail: "
                + $"projected={projected}; global={global.Items.Count}; "
                + $"worker={workerPage.Items.Count}; facility={facilityPage.Items.Count}; "
                + $"type={snapshot.outcomeTypeId}; owner={snapshot.ownerRevision}; "
                + $"commit={snapshot.commitRevision}; replay={replay.Success}/"
                + $"{replay.Code}; conflict={conflict.Success}/{conflict.Code}; "
                + $"current={current.InlineCompletedWork}; roundTrip={roundTrip}; "
                + $"legacy={legacyPreserved}; negativeRejected={negativeRejected}");
        }
        return valid;
    }

    private static bool VerifyCommitRejectionRollsBack()
    {
        using Fixture world = new(rejectCommit: true);
        SettlementSuspendedWorkSnapshot exact = world.CreateSnapshot(2f, 10f);
        EmergencyAccountingResult result = world.Runtime.RecordSuspendedWork(exact);
        return !result.Success
            && !world.Runtime.TryGetSuspendedWork(exact.CharacterId, out _)
            && world.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 0
            && world.Ledger.GetOutboxSnapshot().Count == 0;
    }

    private static bool VerifyDeliveryFaultRetainsState()
    {
        using Fixture world = new(rejectDelivery: true);
        SettlementSuspendedWorkSnapshot exact = world.CreateSnapshot(2f, 10f);
        EmergencyAccountingResult result = world.Runtime.RecordSuspendedWork(exact);
        IReadOnlyList<GameplayOutcomeOutboxSnapshot> outbox =
            world.Ledger.GetOutboxSnapshot();
        bool valid = result.Success
            && world.Runtime.TryGetSuspendedWork(exact.CharacterId, out _)
            && world.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 0
            && outbox.Count == 1
            && outbox[0].Lifecycle ==
                GameplayOutcomeRecordLifecycle.DeliveryFaultPending;
        if (!valid)
        {
            Debug.LogError(
                "Emergency suspension pending detail: "
                + $"result={result.Success}/{result.Code}:{result.Message}; "
                + $"state={world.Runtime.TryGetSuspendedWork(exact.CharacterId, out _)}; "
                + $"global={world.Ledger.GetGlobal(OutcomeCursor.FirstPage(10), OutcomeFilter.All).Items.Count}; "
                + $"outbox={outbox.Count}; lifecycle="
                + (outbox.Count == 0 ? "none" : outbox[0].Lifecycle.ToString())
                + "; fault="
                + (outbox.Count == 0 ? "none" : outbox[0].LastFaultCode));
        }
        return valid;
    }

    private sealed class Fixture : IDisposable
    {
        private const string WorkerId = "character:emergency-suspension-test";
        private const string FacilityId = "building:emergency-suspension-test";

        public Fixture(bool rejectCommit = false, bool rejectDelivery = false)
        {
            Calendar = new FakeCalendar();
            Events = new GameEventBus();
            Accounting = new EmergencyWorkAccountingRuntime(Events);
            EmergencyWorkSuspensionOutcomeDescriptor descriptor = new(
                new KoreanJosaFormatter());
            EmergencyWorkSuspensionOutcomeAdapter adapter = new();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    rejectDelivery
                        ? new RejectingDeliveryDescriptor(descriptor)
                        : descriptor
                },
                new IGameplayOutcomeAdapterRegistration[] { adapter });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8,
                    largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId("run:emergency-suspension-test"),
                1L);
            Recorder = new GameplayOutcomeRecorder(Ledger, Registry, Events);
            EmergencyWorkSuspensionGameplayOutcomeBridge bridge = new(
                Recorder,
                Ledger,
                new FixedDisplayNames(WorkerId, FacilityId));
            Committer = rejectCommit
                ? new RejectingCommitter(bridge)
                : bridge;
            Runtime = CreateRuntime();
            EmergencyAccountingResult incident = Runtime.PublishIncidentSignal(
                new SettlementIncidentSignal(
                    "incident:emergency-suspension-test",
                    SettlementThreatAlertLevel.Red,
                    1L,
                    "test",
                    "test emergency"));
            if (!incident.Success || Runtime.Capture().AlertEpochId <= 0L)
                throw new InvalidOperationException("The test alert could not start.");
        }

        public FakeCalendar Calendar { get; }
        public GameEventBus Events { get; }
        public EmergencyWorkAccountingRuntime Accounting { get; }
        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public IEmergencyWorkSuspensionOutcomeCommitter Committer { get; }
        public SettlementAlertRuntime Runtime { get; }

        public SettlementAlertRuntime CreateRuntime() => new(
            Calendar,
            Events,
            Accounting,
            Accounting,
            Committer);

        public SettlementSuspendedWorkSnapshot CreateSnapshot(
            float completed,
            float required) => new(
            WorkerId,
            BuiltInWorkTypeIds.Construct,
            FacilityId,
            Runtime.Capture().AlertEpochId,
            Calendar.AbsoluteHour,
            progressExternallyPersisted: false,
            inlineCompletedWork: completed,
            inlineRequiredWork: required,
            outcomeOwnerRevision: 1L);

        public void Dispose() => Accounting.Dispose();
    }

    private sealed class FixedDisplayNames : IGameplayOutcomeDisplayNameQuery
    {
        private readonly string workerId;
        private readonly string facilityId;

        public FixedDisplayNames(string workerId, string facilityId)
        {
            this.workerId = workerId;
            this.facilityId = facilityId;
        }

        public bool TryGetCurrentName(
            GameplayEntityId entityId,
            out KoreanNameSnapshot name)
        {
            if (entityId.Kind.Equals(EmergencyWorkSuspensionOutcomeIds.CharacterKind)
                && string.Equals(entityId.Value, workerId, StringComparison.Ordinal))
            {
                name = Snapshot("민준", "worker-v1");
                return true;
            }
            if (entityId.Kind.Equals(EmergencyWorkSuspensionOutcomeIds.FacilityKind)
                && string.Equals(entityId.Value, facilityId, StringComparison.Ordinal))
            {
                name = Snapshot("대장간", "facility-v1");
                return true;
            }
            name = default;
            return false;
        }

        private static KoreanNameSnapshot Snapshot(string text, string revision) =>
            new(
                text,
                revision,
                KoreanPronunciationHint.AutoHangulDisplay(
                    "emergency-suspension-pronunciation-v1:" + revision),
                "ko-KR");
    }

    private sealed class FakeCalendar : IGameCalendar
    {
        private int day = 1;
        private int hour;
        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, 23);
        }
    }

    private sealed class RejectingCommitter :
        IEmergencyWorkSuspensionOutcomeCommitter
    {
        private readonly IEmergencyWorkSuspensionOutcomeCommitter inner;

        public RejectingCommitter(
            IEmergencyWorkSuspensionOutcomeCommitter inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool TryPrepare(
            in SettlementSuspendedWorkSnapshot snapshot,
            int absoluteDay,
            out PreparedEmergencyWorkSuspensionOutcome prepared,
            out string failureReason) => inner.TryPrepare(
            snapshot,
            absoluteDay,
            out prepared,
            out failureReason);

        public OwnerOutcomeCommitResult Commit(
            in PreparedEmergencyWorkSuspensionOutcome prepared)
        {
            inner.Cancel(prepared);
            return new OwnerOutcomeCommitResult(
                OwnerOutcomeCommitPhase.Rejected,
                prepared.ResultKey,
                default,
                string.Empty,
                "injected-emergency-suspension-commit-rejection");
        }

        public void Cancel(in PreparedEmergencyWorkSuspensionOutcome prepared) =>
            inner.Cancel(prepared);
    }

    private sealed class RejectingDeliveryDescriptor : IGameplayOutcomeDescriptor
    {
        private readonly IGameplayOutcomeDescriptor inner;
        private int validationCount;

        public RejectingDeliveryDescriptor(IGameplayOutcomeDescriptor inner) =>
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
            if (!actual.Valid)
                return actual;
            validationCount++;
            return validationCount == 1
                ? actual
                : OutcomeValidationResult.Reject(
                    "injected-emergency-suspension-delivery-rejection");
        }
    }
}
#endif
