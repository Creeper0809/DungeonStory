#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Infrastructure;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:emergency-work-suspension";
    internal const string FirstSuspensionActionId =
        "action:work-emergency-suspension-first-recorded";
    internal const string SecondSuspensionActionId =
        "action:work-emergency-suspension-second-recorded";

    private const string CausalOwnerKind =
        "owner-kind:emergency-work-suspension";
    private const string CausalStateSchema =
        "dungeonstory-causal-emergency-work-suspension-owner-state@1";
    private const string IncidentId =
        "incident:causal:emergency-work-suspension";

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "An emergency-suspension checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalEmergencySuspensionContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                FirstSuspensionActionId,
                "work",
                EmergencyWorkSuspensionOutcomeIds.SuspensionRecorded,
                context.CaptureState,
                context.RecordFirstSuspension,
                context.RequireFirstOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                SecondSuspensionActionId,
                "work",
                EmergencyWorkSuspensionOutcomeIds.SuspensionRecorded,
                () => new Dictionary<string, object>(),
                context.RecordSecondSuspension,
                context.RequireSecondOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string ownerId = "settlement:causal-emergency-suspension:focused";
        JObject firstCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-emergency-suspension:first",
                   ownerId)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstSuspensionActionId);
            IReadOnlyDictionary<string, object> before = first.CaptureState();
            Require(
                (int)before["emergencySuspension.stage"] == 0
                && (int)before["emergencySuspension.suspendedWorkCount"] == 0
                && (int)before["emergencySuspension.exactOutcomeCount"] == 0,
                "Emergency-suspension owner did not start empty.");
            Require(first.Execute(),
                "The first real suspension journal command was rejected.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first suspension has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["emergencySuspension.stage"] == 1
                && (int)after["emergencySuspension.suspendedWorkCount"] == 1
                && (bool)after["emergencySuspension.firstRecorded"]
                && !(bool)after["emergencySuspension.secondRecorded"]
                && (int)after["emergencySuspension.exactOutcomeCount"] == 1,
                "The first suspension did not retain journal and ledger state.");
            firstCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-emergency-suspension:second",
                   ownerId,
                   firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstSuspensionActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                SecondSuspensionActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            Require(
                (int)restored["emergencySuspension.stage"] == 1
                && (bool)restored["emergencySuspension.firstRecorded"]
                && !first.Execute(),
                "Fresh composition restore lost or replayed the first suspension.");
            Require(second.Execute(),
                "The second real suspension journal command was rejected.");
            Require(second.RequireExactOutcome().HasCanonicalPayloadHash,
                "The second suspension has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["emergencySuspension.stage"] == 2
                && (int)after["emergencySuspension.suspendedWorkCount"] == 2
                && (bool)after["emergencySuspension.firstRecorded"]
                && (bool)after["emergencySuspension.secondRecorded"]
                && (int)after["emergencySuspension.exactOutcomeCount"] == 2,
                "The second suspension did not preserve the first journal row.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)second)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-emergency-suspension:terminal",
                   ownerId,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstSuspensionActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                SecondSuspensionActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            Require(!first.Execute() && !second.Execute(),
                "Terminal emergency-suspension owner was not idempotent.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected suspension replay mutated the owner checkpoint.");
        }

        JObject identityTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)identityTamper["domainState"])["firstWorkerId"] =
            "character:causal-emergency-suspension:tampered";
        Require(RejectCheckpoint(ownerId, identityTamper, "identity-tamper"),
            "A tampered suspension worker identity was accepted.");

        JObject journalTamper = (JObject)firstCheckpoint.DeepClone();
        JObject journalState = (JObject)journalTamper["domainState"];
        JObject alertState = (JObject)journalState["alertState"];
        ((JArray)alertState["suspendedWork"]).Clear();
        Require(RejectCheckpoint(ownerId, journalTamper, "journal-tamper"),
            "A suspension journal/ledger mismatch was accepted.");

        if (log)
        {
            Debug.Log(
                "Emergency work suspension first record -> restored second record causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Emergency-suspension causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-emergency-suspension:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class CausalEmergencySuspensionContext : IDisposable
    {
        private readonly string ownerId;
        private readonly string firstWorkerId;
        private readonly string secondWorkerId;
        private readonly string facilityId;
        private readonly World world;
        private bool disposed;

        internal CausalEmergencySuspensionContext(
            string causalRunId,
            string ownerId,
            JObject ownerCheckpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence
                .Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            firstWorkerId = "character:causal-emergency-first:" + scope;
            secondWorkerId = "character:causal-emergency-second:" + scope;
            facilityId = "building:causal-emergency-work:" + scope;
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    CausalOwnerKind)
                : causalRunId + ":emergency-suspension-ledger";
            world = new World(ledgerRunId);

            try
            {
                if (ownerCheckpoint == null)
                    PrepareInitialState();
                else
                    RestoreOwnerCheckpoint(ownerCheckpoint);
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string OwnerId => ownerId;
        internal string OwnerKind => HasPersistentOwner
            ? CausalOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            SettlementAlertSnapshot alert = world.Runtime.Capture();
            bool firstRecorded = world.Runtime.TryGetSuspendedWork(
                firstWorkerId,
                out SettlementSuspendedWorkSnapshot first);
            bool secondRecorded = world.Runtime.TryGetSuspendedWork(
                secondWorkerId,
                out SettlementSuspendedWorkSnapshot second);
            return new Dictionary<string, object>
            {
                ["emergencySuspension.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "settlement:ephemeral-emergency-suspension",
                ["emergencySuspension.firstWorkerId"] = firstWorkerId,
                ["emergencySuspension.secondWorkerId"] = secondWorkerId,
                ["emergencySuspension.facilityId"] = facilityId,
                ["emergencySuspension.stage"] = Stage,
                ["emergencySuspension.alertEpoch"] = alert.AlertEpochId,
                ["emergencySuspension.calendarAbsoluteHour"] =
                    world.Calendar.AbsoluteHour,
                ["emergencySuspension.suspendedWorkCount"] =
                    alert.SuspendedWork.Count,
                ["emergencySuspension.firstRecorded"] = firstRecorded,
                ["emergencySuspension.firstCompletedWork"] =
                    firstRecorded ? first.InlineCompletedWork : 0f,
                ["emergencySuspension.firstRequiredWork"] =
                    firstRecorded ? first.InlineRequiredWork : 0f,
                ["emergencySuspension.secondRecorded"] = secondRecorded,
                ["emergencySuspension.secondCompletedWork"] =
                    secondRecorded ? second.InlineCompletedWork : 0f,
                ["emergencySuspension.secondRequiredWork"] =
                    secondRecorded ? second.InlineRequiredWork : 0f,
                ["emergencySuspension.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool RecordFirstSuspension()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            world.Calendar.SetDateTime(14, 8);
            EmergencyAccountingResult result = world.Runtime.RecordSuspendedWork(
                CreateSnapshot(
                    firstWorkerId,
                    BuiltInWorkTypeIds.Construct,
                    completed: 2f,
                    required: 10f));
            if (!result.Success
                || !RequireOutcome(firstWorkerId, 1L, throwOnFailure: false)
                    .HasCanonicalPayloadHash)
            {
                return false;
            }
            ValidateState();
            return true;
        }

        internal bool RecordSecondSuspension()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            world.Calendar.SetDateTime(15, 9);
            SettlementSuspendedWorkSnapshot snapshot = CreateSnapshot(
                secondWorkerId,
                BuiltInWorkTypeIds.Clean,
                completed: 4f,
                required: 12f);
            EmergencyAccountingResult result;
            try
            {
                result = world.Runtime.RecordSuspendedWork(snapshot);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "The second suspension owner command threw before returning a result.",
                    exception);
            }
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    "The second suspension owner command failed: "
                    + result.Code + ":" + result.Message);
            }
            GameplayOutcomeReplayIdentity identity = RequireOutcome(
                secondWorkerId,
                1L,
                throwOnFailure: true);
            ValidateState();
            return true;
        }

        internal GameplayOutcomeReplayIdentity RequireFirstOutcome() =>
            RequireOutcome(firstWorkerId, 1L, throwOnFailure: true);

        internal GameplayOutcomeReplayIdentity RequireSecondOutcome() =>
            RequireOutcome(secondWorkerId, 1L, throwOnFailure: true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(world.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral emergency-suspension fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CausalOwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = CausalStateSchema,
                    ["firstWorkerId"] = firstWorkerId,
                    ["secondWorkerId"] = secondWorkerId,
                    ["facilityId"] = facilityId,
                    ["incidentId"] = IncidentId,
                    ["stage"] = Stage,
                    ["calendarDay"] = world.Calendar.Day,
                    ["calendarHour"] = world.Calendar.Hour,
                    ["alertState"] = JObject.FromObject(
                        world.Runtime.CaptureAlertSaveData())
                },
                world.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            world?.Dispose();
        }

        private int Stage
        {
            get
            {
                bool first = world.Runtime.TryGetSuspendedWork(
                    firstWorkerId, out _);
                bool second = world.Runtime.TryGetSuspendedWork(
                    secondWorkerId, out _);
                if (second && !first)
                {
                    throw new InvalidOperationException(
                        "Second suspension cannot precede the first suspension.");
                }
                return second ? 2 : first ? 1 : 0;
            }
        }

        private void PrepareInitialState()
        {
            world.Calendar.SetDateTime(14, 7);
            EmergencyAccountingResult incident = world.Runtime.PublishIncidentSignal(
                new SettlementIncidentSignal(
                    IncidentId,
                    SettlementThreatAlertLevel.Red,
                    1L,
                    "causal-fixture",
                    "two workers preserve active assignments during one emergency"));
            if (!incident.Success || world.Runtime.Capture().AlertEpochId <= 0L)
            {
                throw new InvalidOperationException(
                    "The causal emergency alert could not start: "
                    + incident.Code + ":" + incident.Message);
            }
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                CausalOwnerKind,
                CausalOwnerRestoreKind,
                world.Ledger);
            RequireFields(
                state,
                "causal emergency-suspension owner state",
                "schemaVersion",
                "schema",
                "firstWorkerId",
                "secondWorkerId",
                "facilityId",
                "incidentId",
                "stage",
                "calendarDay",
                "calendarHour",
                "alertState");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    CausalStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["firstWorkerId"],
                    firstWorkerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["secondWorkerId"],
                    secondWorkerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["facilityId"],
                    facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["incidentId"],
                    IncidentId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Causal emergency-suspension identities changed.");
            }

            int day = (int)state["calendarDay"];
            int hour = (int)state["calendarHour"];
            if (day <= 0 || hour < 0 || hour > 23)
            {
                throw new InvalidOperationException(
                    "Causal emergency-suspension calendar is invalid.");
            }
            SettlementThreatAlertSaveData alertState = state["alertState"]
                ?.ToObject<SettlementThreatAlertSaveData>()
                ?? throw new InvalidOperationException(
                    "Causal emergency-suspension alert state is missing.");
            world.Calendar.SetDateTime(day, hour);
            world.Runtime.RestoreAlertSaveData(alertState);
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Causal emergency-suspension stage disagrees with the journal.");
            }
        }

        private SettlementSuspendedWorkSnapshot CreateSnapshot(
            string workerId,
            WorkTypeId workTypeId,
            float completed,
            float required) => new(
            workerId,
            workTypeId,
            facilityId,
            world.Runtime.Capture().AlertEpochId,
            world.Calendar.AbsoluteHour,
            progressExternallyPersisted: false,
            inlineCompletedWork: completed,
            inlineRequiredWork: required,
            outcomeOwnerRevision: 1L,
            workerDisplayName: string.Equals(
                workerId,
                firstWorkerId,
                StringComparison.Ordinal)
                ? "건설 작업자 연화"
                : "정비 작업자 서린",
            targetDisplayName: "중앙 작업장");

        private void ValidateState()
        {
            SettlementAlertSnapshot alert = world.Runtime.Capture();
            int stage = Stage;
            if (alert.CommittedLevel != SettlementThreatAlertLevel.Red
                || alert.DesiredLevel != SettlementThreatAlertLevel.Red
                || alert.AlertEpochId <= 0L
                || !alert.ActiveIncidentIds.Contains(IncidentId)
                || alert.SuspendedWork.Count != stage
                || ExactOutcomeCount() != stage)
            {
                throw new InvalidOperationException(
                    "Emergency-suspension alert, journal, and ledger diverged. "
                    + $"stage={stage}; desired={alert.DesiredLevel}; "
                    + $"committed={alert.CommittedLevel}; epoch={alert.AlertEpochId}; "
                    + $"journal={alert.SuspendedWork.Count}; "
                    + $"outcomes={ExactOutcomeCount()}.");
            }

            if (stage >= 1)
            {
                ValidateSnapshot(
                    firstWorkerId,
                    BuiltInWorkTypeIds.Construct,
                    2f,
                    10f);
                _ = RequireOutcome(firstWorkerId, 1L, throwOnFailure: true);
            }
            if (stage >= 2)
            {
                ValidateSnapshot(
                    secondWorkerId,
                    BuiltInWorkTypeIds.Clean,
                    4f,
                    12f);
                _ = RequireOutcome(secondWorkerId, 1L, throwOnFailure: true);
            }
        }

        private void ValidateSnapshot(
            string workerId,
            WorkTypeId workTypeId,
            float completed,
            float required)
        {
            if (!world.Runtime.TryGetSuspendedWork(
                    workerId,
                    out SettlementSuspendedWorkSnapshot value)
                || value.WorkTypeId != workTypeId
                || !string.Equals(
                    value.TargetBuildingId,
                    facilityId,
                    StringComparison.Ordinal)
                || value.AlertEpochId != world.Runtime.Capture().AlertEpochId
                || value.ProgressExternallyPersisted
                || !value.HasInlineProgress
                || !Mathf.Approximately(value.InlineCompletedWork, completed)
                || !Mathf.Approximately(value.InlineRequiredWork, required)
                || value.OutcomeOwnerRevision != 1L
                || value.WorkerDisplayName != (string.Equals(
                    workerId,
                    firstWorkerId,
                    StringComparison.Ordinal)
                    ? "건설 작업자 연화"
                    : "정비 작업자 서린")
                || value.TargetDisplayName != "중앙 작업장")
            {
                throw new InvalidOperationException(
                    "Causal emergency-suspension journal row changed: "
                    + workerId);
            }
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            string workerId,
            long expectedCommitRevision,
            bool throwOnFailure)
        {
            GameplayResultKey key = EmergencyWorkSuspensionOutcomeIds.ResultKey(
                workerId,
                world.Runtime.Capture().AlertEpochId,
                ownerRevision: 1L);
            bool foundIdentity = world.Ledger.TryGetResultIdentity(
                key,
                out GameplayOutcomeReplayIdentity identity);
            GameplayOutcomeSnapshot exact = null;
            bool foundExact = foundIdentity
                && world.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out exact);
            bool valid = foundIdentity
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.CanonicalPayloadHash?.Length == 64
                && foundExact
                && exact != null
                && string.Equals(
                    exact.producerId,
                    EmergencyWorkSuspensionOutcomeIds.ProducerId,
                    StringComparison.Ordinal)
                && string.Equals(
                    exact.outcomeTypeId,
                    EmergencyWorkSuspensionOutcomeIds.SuspensionRecorded.Value,
                    StringComparison.Ordinal)
                && exact.status == GameplayOutcomeStatus.Succeeded
                && exact.ownerRevision == 1L
                && exact.commitRevision == expectedCommitRevision;
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal emergency suspension did not publish exact worker outcome: "
                    + workerId
                    + $"; foundIdentity={foundIdentity}; state={identity.State}; "
                    + $"payloadHashLength={identity.CanonicalPayloadHash?.Length ?? 0}; "
                    + $"foundExact={foundExact}; producer={exact?.producerId}; "
                    + $"type={exact?.outcomeTypeId}; status={exact?.status}; "
                    + $"ownerRevision={exact?.ownerRevision}; "
                    + $"commitRevision={exact?.commitRevision}; "
                    + $"expectedCommitRevision={expectedCommitRevision}.");
            }
            return valid ? identity : default;
        }

        private int ExactOutcomeCount() => world.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(8), OutcomeFilter.All).Items.Count;

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class World : IDisposable
    {
        private readonly EmergencyWorkAccountingRuntime accounting;
        private readonly IDisposable binding;

        internal World(string ledgerRunId)
        {
            Calendar = new FakeCalendar();
            Events = new GameEventBus();
            accounting = new EmergencyWorkAccountingRuntime(Events);
            var descriptor = new EmergencyWorkSuspensionOutcomeDescriptor(
                new KoreanJosaFormatter());
            var adapter = new EmergencyWorkSuspensionOutcomeAdapter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { descriptor },
                new IGameplayOutcomeAdapterRegistration[] { adapter });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8,
                    largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            Recorder = new GameplayOutcomeRecorder(Ledger, Registry, Events);
            Facts = new DomainFactDispatcher();
            binding = EmergencyWorkSuspensionFactMapping.CreateBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    new GameplayDomainFactCommitGroup(Recorder, Ledger))
                .Connect(Facts);
            Runtime = new SettlementAlertRuntime(
                Calendar,
                Events,
                accounting,
                accounting,
                Facts,
                Facts);
        }

        internal FakeCalendar Calendar { get; }
        internal GameEventBus Events { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal DomainFactDispatcher Facts { get; }
        internal SettlementAlertRuntime Runtime { get; }

        public void Dispose()
        {
            binding.Dispose();
            Facts.Dispose();
            accounting.Dispose();
        }
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

    private sealed class FixtureSet : IDisposable
    {
        private readonly Dictionary<string, IProductionCausalActionFixture>
            fixtures;

        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> source)
        {
            fixtures = (source
                    ?? throw new ArgumentNullException(nameof(source)))
                .ToDictionary(value => value.ActionId, StringComparer.Ordinal);
        }

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.TryGetValue(
                actionId,
                out IProductionCausalActionFixture value)
                ? value
                : throw new InvalidOperationException(
                    "Missing emergency-suspension causal fixture: " + actionId);

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures.Values)
                fixture.Dispose();
        }
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] fields)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expected = fields
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", expected));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
