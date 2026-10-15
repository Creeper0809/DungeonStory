#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Infrastructure;
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

    public static void VerifyObserverFocused()
    {
        if (!RunAll(logSuccess: false))
        {
            throw new InvalidOperationException(
                "Emergency work suspension observer core scenarios failed.");
        }
        if (!VerifyCommittedUnknownRecoveryAndFrozenFact())
        {
            throw new InvalidOperationException(
                "Emergency work suspension pending observation recovery failed.");
        }
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
        GameplayOutcomeParticipantSnapshot workerParticipant =
            snapshot.participants.Single(value => string.Equals(
                value.entityId,
                exact.CharacterId,
                StringComparison.Ordinal));
        GameplayOutcomeParticipantSnapshot facilityParticipant =
            snapshot.participants.Single(value => string.Equals(
                value.entityId,
                exact.TargetBuildingId,
                StringComparison.Ordinal));
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
        IReadOnlyList<string> validationErrors =
            DungeonStory.Operation.EventAlertPayloadValidation.Validate(
                new DungeonStory.Infrastructure.DungeonEventAlertSaveData
                {
                    threatAlert = saved
                });
        bool legacyRejected = validationErrors.Count > 0;

        bool valid = projected
            && snapshot.sequence == workerPage.Items[0].Exact.sequence
            && snapshot.sequence == facilityPage.Items[0].Exact.sequence
            && snapshot.ownerRevision == 1L
            && snapshot.commitRevision == 1L
            && workerParticipant.displayText == exact.WorkerDisplayName
            && workerParticipant.displayRevision
                == "runtime-character-current-v1:" + exact.CharacterId
            && facilityParticipant.displayText == exact.TargetDisplayName
            && facilityParticipant.displayRevision
                == "runtime-facility-current-v1:" + exact.TargetBuildingId
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
            && legacyRejected
            && EventAlertSaveSection.CurrentVersion == 6;
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
                + $"legacyRejected={legacyRejected}");
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

    private static bool VerifyCommittedUnknownRecoveryAndFrozenFact()
    {
        using Fixture world = new(
            loseReplyAfterCommit: true,
            initialLevel: SettlementThreatAlertLevel.Amber);
        SettlementSuspendedWorkSnapshot original = world.CreateSnapshot(
            2f,
            10f,
            workerDisplayName: "동결 작업자",
            targetDisplayName: "동결 작업장");
        EmergencyAccountingResult uncertain =
            world.Runtime.RecordSuspendedWork(original);
        SettlementThreatAlertSaveData firstSave =
            world.Runtime.CaptureAlertSaveData();
        EmergencyWorkSuspensionObservationSaveData firstObservation =
            firstSave.suspensionObservations.Single();
        bool retained = uncertain.Success
            && uncertain.Code.StartsWith(
                "suspended-work-recorded-observation-pending",
                StringComparison.Ordinal)
            && firstObservation.pending
            && firstObservation.fact.workerDisplayName == "동결 작업자"
            && firstObservation.fact.targetDisplayName == "동결 작업장"
            && world.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1;
        if (!retained)
            return false;

        SettlementSuspendedWorkSnapshot renamed = world.CreateSnapshot(
            2f,
            10f,
            workerDisplayName: "나중 작업자 이름",
            targetDisplayName: "나중 작업장 이름");
        EmergencyAccountingResult replayWhilePending =
            world.Runtime.RecordSuspendedWork(renamed);
        if (!replayWhilePending.Success
            || !replayWhilePending.Code.StartsWith(
                "suspended-work-recorded-observation-pending",
                StringComparison.Ordinal))
        {
            return false;
        }

        long frozenEpoch = original.AlertEpochId;
        EmergencyAccountingResult escalation = world.Runtime.PublishIncidentSignal(
            new SettlementIncidentSignal(
                "incident:emergency-suspension-test:red-escalation",
                SettlementThreatAlertLevel.Red,
                1L,
                "test",
                "escalate while the suspension observation is pending"));
        if (!escalation.Success
            || world.Runtime.Capture().AlertEpochId <= frozenEpoch
            || !world.Runtime.TryGetSuspendedWork(
                original.CharacterId,
                out SettlementSuspendedWorkSnapshot rebound)
            || rebound.AlertEpochId == frozenEpoch)
        {
            return false;
        }

        string json = JsonUtility.ToJson(world.Runtime.CaptureAlertSaveData());
        SettlementThreatAlertSaveData saved =
            JsonUtility.FromJson<SettlementThreatAlertSaveData>(json);
        EmergencyWorkSuspensionObservationSaveData frozen =
            saved.suspensionObservations.Single();
        if (!frozen.pending
            || frozen.fact.alertEpochId != frozenEpoch
            || frozen.fact.workerDisplayName != "동결 작업자"
            || frozen.fact.targetDisplayName != "동결 작업장")
        {
            return false;
        }

        SettlementThreatAlertSaveData missing = Clone(saved);
        missing.suspensionObservations.Clear();
        SettlementThreatAlertSaveData tampered = Clone(saved);
        tampered.suspensionObservations[0].fact.inlineCompletedWork = 3f;
        if (!RejectRestore(world, missing) || !RejectRestore(world, tampered))
            return false;

        SettlementAlertRuntime restored = world.CreateRuntime();
        restored.RestoreAlertSaveData(saved);
        restored.OnRestoreCompleted();
        SettlementThreatAlertSaveData recovered = restored.CaptureAlertSaveData();
        EmergencyWorkSuspensionObservationSaveData observed =
            recovered.suspensionObservations.Single();
        if (observed.pending
            || observed.fact.alertEpochId != frozenEpoch
            || observed.fact.workerDisplayName != "동결 작업자"
            || observed.fact.targetDisplayName != "동결 작업장"
            || !restored.TryGetSuspendedWork(
                original.CharacterId,
                out SettlementSuspendedWorkSnapshot restoredActive)
            || restoredActive.AlertEpochId != rebound.AlertEpochId)
        {
            return false;
        }

        EmergencyAccountingResult exactReplay =
            restored.RecordSuspendedWork(restoredActive);
        return exactReplay.Success
            && exactReplay.Code == "suspended-work-already-recorded"
            && world.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1;
    }

    private static SettlementThreatAlertSaveData Clone(
        SettlementThreatAlertSaveData source) =>
        JsonUtility.FromJson<SettlementThreatAlertSaveData>(
            JsonUtility.ToJson(source));

    private static bool RejectRestore(
        Fixture world,
        SettlementThreatAlertSaveData source)
    {
        try
        {
            world.CreateRuntime().RestoreAlertSaveData(source);
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Fixture : IDisposable
    {
        private const string WorkerId = "character:emergency-suspension-test";
        private const string FacilityId = "building:emergency-suspension-test";

        private readonly IDisposable binding;

        public Fixture(
            bool rejectCommit = false,
            bool rejectDelivery = false,
            bool loseReplyAfterCommit = false,
            SettlementThreatAlertLevel initialLevel =
                SettlementThreatAlertLevel.Red)
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
            Facts = new DomainFactDispatcher();
            binding = EmergencyWorkSuspensionFactMapping.CreateBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    new GameplayDomainFactCommitGroup(Recorder, Ledger))
                .Connect(Facts);
            IDomainFactStagingPublisher staging = rejectCommit
                ? new FaultingStagingPublisher(Facts, FaultMode.Reject)
                : loseReplyAfterCommit
                    ? new FaultingStagingPublisher(
                        Facts,
                        FaultMode.UncertainAfterCommit)
                    : Facts;
            Runtime = CreateRuntime(staging, Facts);
            EmergencyAccountingResult incident = Runtime.PublishIncidentSignal(
                new SettlementIncidentSignal(
                    "incident:emergency-suspension-test",
                    initialLevel,
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
        public DomainFactDispatcher Facts { get; }
        public SettlementAlertRuntime Runtime { get; }

        public SettlementAlertRuntime CreateRuntime() =>
            CreateRuntime(Facts, Facts);

        private SettlementAlertRuntime CreateRuntime(
            IDomainFactStagingPublisher staging,
            IDomainFactRecoveryPublisher recovery) => new(
            Calendar,
            Events,
            Accounting,
            Accounting,
            staging,
            recovery);

        public SettlementSuspendedWorkSnapshot CreateSnapshot(
            float completed,
            float required,
            string workerDisplayName = "민준",
            string targetDisplayName = "대장간") => new(
            WorkerId,
            BuiltInWorkTypeIds.Construct,
            FacilityId,
            Runtime.Capture().AlertEpochId,
            Calendar.AbsoluteHour,
            progressExternallyPersisted: false,
            inlineCompletedWork: completed,
            inlineRequiredWork: required,
            outcomeOwnerRevision: 1L,
            workerDisplayName: workerDisplayName,
            targetDisplayName: targetDisplayName);

        public void Dispose()
        {
            binding.Dispose();
            Facts.Dispose();
            Accounting.Dispose();
        }
    }

    private enum FaultMode
    {
        Reject = 0,
        UncertainAfterCommit = 1
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

    private sealed class FaultingStagingPublisher :
        IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly FaultMode mode;
        private bool injected;

        internal FaultingStagingPublisher(
            IDomainFactStagingPublisher inner,
            FaultMode mode)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.mode = mode;
        }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            if (!inner.TryStage(fact, out stage, out failure))
                return false;
            if (!injected)
            {
                injected = true;
                stage = new FaultingStage(stage, mode);
            }
            return true;
        }

        private sealed class FaultingStage : IDomainFactStage
        {
            private readonly IDomainFactStage inner;
            private readonly FaultMode mode;

            internal FaultingStage(IDomainFactStage inner, FaultMode mode)
            {
                this.inner = inner
                    ?? throw new ArgumentNullException(nameof(inner));
                this.mode = mode;
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;

            public DomainFactDelivery Commit()
            {
                if (mode == FaultMode.UncertainAfterCommit)
                {
                    DomainFactDelivery committed = inner.Commit();
                    if (!committed.Completed)
                    {
                        throw new InvalidOperationException(
                            "Emergency suspension uncertainty requires a real commit.");
                    }
                    return DomainFactDelivery.Uncertain(
                        "injected-emergency-suspension-reply-lost");
                }
                inner.Cancel();
                return DomainFactDelivery.Reject(
                    "injected-emergency-suspension-commit-rejection");
            }

            public void Cancel() => inner.Cancel();
        }
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
