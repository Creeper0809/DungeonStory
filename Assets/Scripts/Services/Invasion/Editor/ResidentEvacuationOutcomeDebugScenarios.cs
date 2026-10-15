#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;

public static class ResidentEvacuationOutcomeDebugScenarios
{
    public static void RunResidentEvacuationObserversForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qa = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Artifacts",
            "QA")) + Path.DirectorySeparatorChar;
        Require(Application.isBatchMode
            && !string.IsNullOrWhiteSpace(output)
            && Path.IsPathRooted(output)
            && Path.GetFullPath(output).StartsWith(
                qa,
                StringComparison.OrdinalIgnoreCase)
            && !Directory.Exists(output),
            "Requires fresh project-local isolated evidence.");
        string probe = File.ReadAllText(Path.Combine(
            Directory.GetParent(Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
            && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true")
            && probe.Contains("inJob=true"),
            "Missing isolation probe.");
        string result = RunAll(false);
        Require(result.StartsWith("PASS", StringComparison.Ordinal),
            "Resident evacuation observer checks failed: " + result);
        Debug.Log("B96_RESIDENT_EVACUATION_OBSERVERS_FOCUSED=PASS");
        VerifyDiagnosticActivityObserver();
        StaffDutyDebugScenarios.VerifyActionObserverFocused();
        EquipmentItemStateV18DebugScenarios.RunAll();
        Debug.Log("B88_EQUIPMENT_ASSESSMENT_FIXTURE_REGRESSION=PASS");
    }

    public static void RunBatch()
    {
        string relative = ReadArgument("-residentEvacuationReport")
            ?? "Artifacts/QA/ResidentEvacuationOutcomeFocused/report.txt";
        string absolute = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            relative));
        if (File.Exists(absolute))
        {
            throw new InvalidOperationException(
                "Resident evacuation evidence is immutable: " + relative);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)
            ?? throw new InvalidOperationException("Report directory is missing."));
        string result = RunAll(false);
        File.WriteAllText(absolute, result + Environment.NewLine);
        bool passed = result.StartsWith("PASS", StringComparison.Ordinal);
        Debug.Log(result);
        EditorApplication.Exit(passed ? 0 : 1);
    }

    public static string RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyEveryTerminalAndReplay();
            VerifyPrepareAndDeliveryFailuresRemainExplicit();
            VerifyObserverLifecycle();
            VerifyOwnerPendingRestore();
            return "PASS: RESIDENT_EVACUATION_OUTCOME terminals=8 replay=1 prepare-rejected=1 delivery-resumed=1 lifecycle=1 owner-restore=1";
        }
        catch (Exception exception)
        {
            if (throwOnFailure)
            {
                throw;
            }
            return "FAIL: RESIDENT_EVACUATION_OUTCOME "
                + exception.GetType().Name + ":" + exception.Message;
        }
    }

    private static void VerifyEveryTerminalAndReplay()
    {
        using Fixture fixture = new("qa:resident-evacuation:terminals");
        ResidentEvacuationTerminalKind[] terminals =
        {
            ResidentEvacuationTerminalKind.Arrived,
            ResidentEvacuationTerminalKind.NoCapacity,
            ResidentEvacuationTerminalKind.Unreachable,
            ResidentEvacuationTerminalKind.ThreatEnded,
            ResidentEvacuationTerminalKind.ZoneCleared,
            ResidentEvacuationTerminalKind.ZoneLost,
            ResidentEvacuationTerminalKind.ReplacedByRequest,
            ResidentEvacuationTerminalKind.ResidentUnavailable
        };
        for (int index = 0; index < terminals.Length; index++)
        {
            ResidentEvacuationTerminalFact fact = Fact(
                index + 1,
                terminals[index],
                ResidentEvacuationThreatKind.InvasionAndFire);
            ResidentEvacuationOutcomeReceipt receipt =
                ResidentEvacuationFactObserver.Capture(fact);
            Require(fixture.Dispatcher.TryStage(
                    fact,
                    out IDomainFactStage prepared,
                    out string failure),
                "prepare rejected terminal " + terminals[index] + ":" + failure);
            Require(
                prepared.OperationKey
                    == ResidentEvacuationFactObserver.Key(fact).ToString(),
                "typed fact reserved a different native result key");
            DomainFactDelivery result = prepared.Commit();
            Require(result.Completed,
                "commit rejected terminal " + terminals[index] + ":"
                + result.Detail);
            GameplayOutcomeQueryPage page = fixture.Ledger.GetForOperation(
                receipt.OperationId);
            Require(page.Items.Count == 1
                && page.Items[0].Exact != null,
                "terminal did not publish exactly one exact outcome");
            GameplayOutcomeSnapshot exact = page.Items[0].Exact;
            Require(exact.outcomeTypeId
                    == ResidentEvacuationOutcomeIds.Terminal.Value
                && exact.status == receipt.Status
                && exact.participants.Count == 1
                && exact.participants[0].entityId == receipt.CharacterId
                && exact.facts.Count == 10
                && exact.facts[0].value == receipt.RequestGroupId
                && exact.facts[2].value == receipt.ThreatKind.ToString()
                && exact.facts[3].value == terminals[index].ToString()
                && exact.facts[4].value
                    == "invasion:alpha,invasion:beta"
                && exact.facts[5].value == "-2,3|4,5"
                && exact.anchors.Count == 1,
                "terminal exact payload lost cause/subject/status facts");

            Require(fixture.Dispatcher.TryStage(
                    fact,
                    out IDomainFactStage replay,
                    out failure)
                && replay.IsReplay
                && replay.Commit().Completed
                && fixture.Ledger.GetForOperation(receipt.OperationId)
                    .Items.Count == 1,
                "terminal replay created a duplicate or was rejected");
        }
    }

    private static void VerifyPrepareAndDeliveryFailuresRemainExplicit()
    {
        using Fixture fixture = new("qa:resident-evacuation:faults");
        ResidentEvacuationTerminalFact rejected = Fact(
            90,
            ResidentEvacuationTerminalKind.NoCapacity,
            ResidentEvacuationThreatKind.Invasion);
        ResidentEvacuationOutcomeReceipt rejectedReceipt =
            ResidentEvacuationFactObserver.Capture(rejected);
        fixture.Faults.RejectNextPrepare = true;
        Require(!fixture.Dispatcher.TryStage(
                rejected,
                out _,
                out string failure)
            && failure.Contains("CapacityDeferred", StringComparison.Ordinal)
            && fixture.Ledger.GetForOperation(rejectedReceipt.OperationId)
                .Items.Count == 0,
            "prepare rejection was hidden or mutated the ledger");

        ResidentEvacuationTerminalFact pending = Fact(
            91,
            ResidentEvacuationTerminalKind.ThreatEnded,
            ResidentEvacuationThreatKind.Fire);
        ResidentEvacuationOutcomeReceipt pendingReceipt =
            ResidentEvacuationFactObserver.Capture(pending);
        Require(fixture.Dispatcher.TryStage(
                pending,
                out IDomainFactStage prepared,
                out failure),
            "delivery-fault receipt did not prepare:" + failure);
        fixture.Faults.RejectNextDelivery = true;
        DomainFactDelivery first = prepared.Commit();
        Require(first.Phase
                == DomainFactDeliveryPhase.AcceptedPendingDelivery,
            "delivery fault was treated as rollback");
        DomainFactDelivery resumed = fixture.Dispatcher.Replay(pending);
        Require(resumed.Completed
            && fixture.Ledger.GetForOperation(pendingReceipt.OperationId)
                .Items.Count == 1,
            "pending delivery did not resume to one exact result");
    }

    private static void VerifyObserverLifecycle()
    {
        ResidentEvacuationTerminalFact fact = Fact(
            101,
            ResidentEvacuationTerminalKind.Arrived,
            ResidentEvacuationThreatKind.Invasion);
        using (var missing = new DomainFactDispatcher())
        {
            Require(!missing.TryStage(fact, out _, out string missingFailure)
                && missingFailure.StartsWith(
                    "domain-fact-staging-observer-missing:",
                    StringComparison.Ordinal),
                "Missing resident-evacuation observer was accepted.");
        }

        using Fixture fixture = new("qa:resident-evacuation:lifecycle");
        ExpectInvalid(
            fixture.Connect,
            "A duplicate resident-evacuation observer was accepted.");
        fixture.Disconnect();
        Require(!fixture.Dispatcher.TryStage(
                fact,
                out _,
                out string disconnectedFailure)
            && disconnectedFailure.StartsWith(
                "domain-fact-staging-observer-missing:",
                StringComparison.Ordinal),
            "Disposed resident-evacuation observer remained active.");
        fixture.Connect();
        Require(fixture.Dispatcher.TryStage(
                fact,
                out IDomainFactStage cancelled,
                out string failure),
            "Reconnected resident-evacuation observer did not stage: "
                + failure);
        cancelled.Cancel();
        Require(!cancelled.IsActive
            && fixture.Ledger.GetForOperation(
                    ResidentEvacuationFactObserver.Capture(fact).OperationId)
                .Items.Count == 0,
            "Cancelled resident-evacuation stage changed the ledger.");
        Require(fixture.Dispatcher.TryStage(
                fact,
                out IDomainFactStage stage,
                out failure)
            && stage.Commit().Completed
            && fixture.Ledger.GetForOperation(
                    ResidentEvacuationFactObserver.Capture(fact).OperationId)
                .Items.Count == 1,
            "Reconnected resident-evacuation observer did not publish once: "
                + failure);
    }

    private static void VerifyDiagnosticActivityObserver()
    {
        var host = new GameObject("B97DiagnosticActor");
        host.SetActive(false);
        try
        {
            var actor = host.AddComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            var events = new GameEventBus();
            CharacterAiEditorTestDependencies.InjectWithDomainEvents(host, events);
            Require(actor.Brain == null, "Diagnostic fixture must exercise the brain-independent actor port.");
            using var observer = new CharacterAiDiagnosticActivityObserver(events);
            var observed = new List<CharacterActivityEvent>();
            actor.LogComponent.OnLogAdded += value => observed.Add(value.Activity);
            observer.Initialize(); observer.Initialize();
            var fact = new CharacterAiDiagnosticEvent(actor,
                CharacterAiDiagnosticTransition.DuplicateExecutionSuppressed,
                "duplicate-execution-suppressed", "qa-action", 4);
            actor.PublishAiDiagnostic(fact);
            Require(observed.Count == 1 && !observed[0].NarrativeEligible
                && observed[0].FactText == "AI duplicate execution suppressed: action=qa-action; count=4",
                "AI diagnostic observer changed its internal-only payload or subscribed twice.");
            observer.Dispose(); actor.PublishAiDiagnostic(fact);
            Require(observed.Count == 1, "Disposed AI diagnostic observer still receives facts.");
            observer.Initialize(); actor.PublishAiDiagnostic(fact);
            Require(observed.Count == 2, "Reconnected AI diagnostic observer did not publish once.");
            foreach (CharacterAiDiagnosticTransition transition in Enum.GetValues(typeof(CharacterAiDiagnosticTransition)))
            {
                var projected = CharacterAiDiagnosticActivityObserver.Project(
                    new CharacterAiDiagnosticEvent(actor, transition, "qa-reason", "qa-detail", 2));
                Require(projected != null && projected.ReasonCode == "qa-reason"
                    && projected.FactText.Contains("qa-detail"), "Diagnostic projection dropped a detail: " + transition);
            }
            Require(EvacuationActivityObserver.Project(new ResidentEvacuationArrivedEvent(actor)).FactText
                    == "주민 대피 구역 도착"
                && EvacuationActivityObserver.Project(new OwnerEvacuationArrivedEvent(actor, true)).FactText
                    == "사장실 대피 완료"
                && EvacuationActivityObserver.Project(new OwnerEvacuationArrivedEvent(actor, false)).FactText
                    == "임시 대피 완료", "Evacuation arrival projection changed.");
            var evidence = CharacterAiUtilityBreakdownEvidence.Capture(null, 0.5f);
            var distant = CharacterAiDiagnosticActivityObserver.Project(new CharacterAiDistantFacilitySelectionEvent(
                actor, default, "far", 6, 2, 0.5f, 9f, evidence,
                "near", 1, 0, 0.3f, 1f, evidence, 8f));
            Require(!distant.NarrativeEligible && distant.ReasonCode == "facility-distant-selection"
                && distant.FactText.Contains("selected=far@(6, 2):score=0.5:travel=9")
                && distant.FactText.Contains("extraTravel=8; selectedWhy=score=0.5; nearestWhy=score=0.5"),
                "Distant facility raw evidence projection changed its original diagnostic payload.");
            Debug.Log("B97_AI_DIAGNOSTIC_PROJECTION_AND_LIFETIME=PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void VerifyOwnerPendingRestore()
    {
        var fact = Fact(201, ResidentEvacuationTerminalKind.Arrived,
            ResidentEvacuationThreatKind.InvasionAndFire);
        var pending = new OwnerEvacuationSaveSnapshot
        {
            residentContractVersion = OwnerEvacuationSaveSnapshot.ResidentContractVersion,
            nextResidentEvacuationRequestSequence = 202L,
            pendingResidentOutcomes = new List<ResidentEvacuationTerminalSaveData> { fact.Snapshot() }
        };
        string before = DungeonCharacterWorldSaveCodec.SerializeFields(pending);
        using var original = new Fixture("qa:resident-evacuation:owner-restore");
        using var owner = new PendingOwnerFixture(new HideAckPublisher(original.Dispatcher));
        owner.Restore(pending);
        var captured = owner.Service.Capture();
        Require(captured.pendingResidentOutcomes.Count == 1
            && captured.nextResidentEvacuationRequestSequence == 202L,
            "Actual owner lost its pending fact after a hidden commit acknowledgement.");
        var key = ResidentEvacuationFactObserver.Key(fact);
        Require(original.Ledger.TryGetResultIdentity(key, out var identity),
            "Hidden acknowledgement did not follow a real ledger commit.");
        string json = DungeonCharacterWorldSaveCodec.SerializeFields(captured);
        var saved = DungeonSaveJson.Deserialize<OwnerEvacuationSaveSnapshot>(json);
        Require(DungeonCharacterWorldSaveCodec.SerializeFields(saved.pendingResidentOutcomes[0])
                == DungeonCharacterWorldSaveCodec.SerializeFields(fact.Snapshot()),
            "Owner save changed the frozen terminal fact.");
        var canonical = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(
            DungeonCharacterWorldSaveCodec.SerializeFields(original.Ledger.CaptureGameplayOutcomes()));
        using var fresh = new Fixture("qa:resident-evacuation:fresh");
        var candidate = fresh.Ledger.PrepareGameplayOutcomeRestore(canonical);
        fresh.Ledger.BeginRestoreCandidate(); fresh.Ledger.PublishGameplayOutcomeRestore(candidate);
        fresh.Ledger.PublishRestoreCandidate(); fresh.Ledger.CompleteRestoreCandidate();
        using var recovered = new PendingOwnerFixture(fresh.Dispatcher);
        recovered.Restore(saved);
        Require(recovered.Service.Capture().pendingResidentOutcomes.Count == 0
            && fresh.Ledger.TryGetResultIdentity(key, out var replayed)
            && replayed.OutcomeId == identity.OutcomeId
            && replayed.CanonicalPayloadHash == identity.CanonicalPayloadHash
            && fresh.Ledger.CaptureGameplayOutcomes().nextSequence == canonical.nextSequence,
            "Fresh owner restore duplicated or changed the committed terminal.");
        recovered.Restore(saved);
        Require(recovered.Service.Capture().pendingResidentOutcomes.Count == 0
            && fresh.Ledger.CaptureGameplayOutcomes().nextSequence == canonical.nextSequence
            && DungeonCharacterWorldSaveCodec.SerializeFields(pending) == before,
            "Repeated restore changed the source fact or native identity.");
        Debug.Log("B96_EVACUATION_OWNER_PENDING_RESTORE=PASS");
    }

    // Uses the real owner and its public restore-completed hook. World ports are
    // deliberately empty and throw on movement/room/AI queries: this tests only
    // delivery of an already completed evacuation, not a natural evacuation.
    private sealed class PendingOwnerFixture : IDisposable
    {
        private readonly GameObject host = new("B96PendingEvacuationOwner");
        private readonly Grid grid = new(4, 4);
        private bool staging;
        internal PendingOwnerFixture(IDomainFactStagingPublisher facts)
        {
            host.SetActive(false);
            var director = host.AddComponent<InvasionDirectorRuntime>();
            var context = Port<IInvasionIntruderContext>((method, args) =>
            {
                if (method.Name == "TryGetGrid") { args[0] = grid; return true; }
                throw new InvalidOperationException("Unexpected world query: " + method.Name);
            });
            var candidates = Port<IRestoreWorldCandidateQuery>((method, args) =>
            {
                if (method.Name == "TryGetGrid") { args[0] = staging ? grid : null; return staging; }
                if (method.Name == "TryGetCharacters")
                { args[0] = staging ? Array.Empty<CharacterActor>() : null; return staging; }
                throw new InvalidOperationException("Unexpected candidate query: " + method.Name);
            });
            var world = Port<ICharacterWorldQuery>((method, _) => method.Name == "get_Characters"
                ? Array.Empty<CharacterActor>() : throw new InvalidOperationException(method.Name));
            Service = new InvasionOwnerEvacuationService(context,
                new InvasionSceneRuntimeReferences(null, director, null), Port<IRoomLayoutCache>(),
                new GameEventBus(), world, candidates, Port<ICharacterSettlementStandingQuery>(),
                Port<ICharacterCombatStanceQuery>(), Port<IBuildingWorldQuery>(),
                Port<IDefenseEngagementStore>(), Port<IEnvironmentalFireQuery>(),
                Port<IWorldHazardZoneQuery>(), facts, Port<IGameCalendar>());
        }
        internal InvasionOwnerEvacuationService Service { get; }
        internal void Restore(OwnerEvacuationSaveSnapshot snapshot)
        {
            staging = true;
            var report = new DungeonGameRestoreReport();
            Service.PrepareRestoreCandidate(snapshot, report);
            Require(report.Success, "Owner candidate rejected: " + string.Join(";", report.Errors));
            Service.PublishRestoreCandidate(); Service.CompleteRestoreCandidate();
            Require(Service.Capture().pendingResidentOutcomes.Count == snapshot.pendingResidentOutcomes.Count,
                "Pending facts were delivered before the world restore completed.");
            staging = false;
            Service.OnRestoreCompleted();
        }
        public void Dispose() { Service.Dispose(); UnityEngine.Object.DestroyImmediate(host); }
    }

    private static T Port<T>(Func<MethodInfo, object[], object> handler = null) where T : class
    {
        T port = DispatchProxy.Create<T, B96PortProxy>();
        ((B96PortProxy)(object)port).Handler = handler;
        return port;
    }
    public class B96PortProxy : DispatchProxy
    {
        internal Func<MethodInfo, object[], object> Handler;
        protected override object Invoke(MethodInfo method, object[] args) => Handler != null
            ? Handler(method, args) : throw new InvalidOperationException("Unexpected port query: " + method.Name);
    }
    private sealed class HideAckPublisher : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        internal HideAckPublisher(IDomainFactStagingPublisher inner) => this.inner = inner;
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            bool prepared = inner.TryStage(fact, out stage, out failure);
            if (prepared) stage = new HiddenAckStage(stage);
            return prepared;
        }
        private sealed class HiddenAckStage : IDomainFactStage
        {
            private readonly IDomainFactStage inner;
            internal HiddenAckStage(IDomainFactStage inner) => this.inner = inner;
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public void Cancel() => inner.Cancel();
            public DomainFactDelivery Commit()
            {
                var committed = inner.Commit();
                Require(committed.Completed, "Real commit failed before hiding its acknowledgement.");
                return DomainFactDelivery.Uncertain("qa-hidden-evacuation-ack");
            }
        }
    }

    private static ResidentEvacuationTerminalFact Fact(
        int sequence,
        ResidentEvacuationTerminalKind terminal,
        ResidentEvacuationThreatKind threat)
    {
        IReadOnlyList<string> intruders = threat is
            ResidentEvacuationThreatKind.Invasion
            or ResidentEvacuationThreatKind.InvasionAndFire
                ? new[] { "invasion:alpha", "invasion:beta" }
                : Array.Empty<string>();
        IReadOnlyList<string> fires = threat is
            ResidentEvacuationThreatKind.Fire
            or ResidentEvacuationThreatKind.InvasionAndFire
                ? new[] { "-2,3", "4,5" }
                : Array.Empty<string>();
        bool hasTarget = terminal != ResidentEvacuationTerminalKind.NoCapacity;
        return new ResidentEvacuationTerminalFact(
            new ResidentEvacuationTerminalSaveData
            {
                operationId = "resident-evacuation:qa:"
                    + sequence.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                requestGroupId = "resident-evacuation-request:qa:"
                    + sequence.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                ownerRevision = 1L,
                characterId = "character:resident-alpha",
                characterDisplayName = "연화",
                initiationKind = ResidentEvacuationInitiationKind.Manual,
                threatKind = threat,
                terminalKind = terminal,
                intruderRuntimeIds = intruders.ToList(),
                fireCells = fires.ToList(),
                requestedAbsoluteDay = 12,
                terminalAbsoluteDay = 12,
                originX = 1,
                originY = 2,
                hasTarget = hasTarget,
                targetX = hasTarget ? 8 : 0,
                targetY = hasTarget ? 9 : 0,
                finalX = 5,
                finalY = 6
            });
    }

    private static void ExpectInvalid(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string ReadArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int index = 0; index + 1 < args.Length; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }
        return null;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IDomainFactObserverBinding binding;
        private IDisposable connection;

        internal Fixture(string runId)
        {
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new ResidentEvacuationOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new ResidentEvacuationOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId(runId),
                1L);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                registry,
                new GameEventBus());
            Faults = new FaultingRecorder(recorder);
            Dispatcher = new DomainFactDispatcher();
            binding = ResidentEvacuationFactObserver.CreateBinding(
                registry,
                Ledger,
                Faults,
                new GameplayDomainFactCommitGroup(Faults, Ledger));
            Connect();
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal FaultingRecorder Faults { get; }
        internal DomainFactDispatcher Dispatcher { get; }

        internal void Connect()
        {
            IDisposable candidate = binding.Connect(Dispatcher);
            connection = candidate;
        }

        internal void Disconnect()
        {
            connection?.Dispose();
            connection = null;
        }

        public void Dispose()
        {
            Disconnect();
            Dispatcher.Dispose();
        }
    }

    private sealed class FaultingRecorder : IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;

        internal FaultingRecorder(IGameplayOutcomeRecorder inner) =>
            this.inner = inner;

        internal bool RejectNextPrepare { get; set; }
        internal bool RejectNextDelivery { get; set; }

        public OutcomePrepareResult TryPrepare<TReceipt>(
            in TReceipt receipt,
            out PreparedOutcomeToken prepared)
        {
            if (RejectNextPrepare)
            {
                RejectNextPrepare = false;
                prepared = default;
                return new OutcomePrepareResult(
                    OutcomePrepareCode.CapacityDeferred,
                    "injected-prepare-fault");
            }
            return inner.TryPrepare(receipt, out prepared);
        }

        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation) =>
            inner.TryReserve(requirements, out reservation);
        public OutcomePrepareResult TryWriteReserved<TReceipt>(
            in TReceipt receipt,
            in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken prepared) =>
            inner.TryWriteReserved(receipt, reservation, out prepared);
        public void CancelReservation(in PreparedOutcomeReservation reservation) =>
            inner.CancelReservation(reservation);
        public void CancelPrepared(in PreparedOutcomeToken prepared) =>
            inner.CancelPrepared(prepared);
        public OutcomeCommitResult CommitPrepared(
            in PreparedOutcomeToken prepared,
            long expectedOwnerRevision,
            out CommittedOutcomeToken committed) =>
            inner.CommitPrepared(prepared, expectedOwnerRevision, out committed);
        public OutcomeCommitResult CommitPreparedBatch(
            PreparedOutcomeToken[] prepared,
            long[] expectedOwnerRevisions,
            CommittedOutcomeToken[] committed) =>
            inner.CommitPreparedBatch(
                prepared,
                expectedOwnerRevisions,
                committed);
        public OutcomeDeliveryResult TryDeliver(
            in CommittedOutcomeToken committed)
        {
            if (RejectNextDelivery)
            {
                RejectNextDelivery = false;
                return new OutcomeDeliveryResult(
                    OutcomeDeliveryCode.PendingDeliveryFault,
                    committed.OutcomeId,
                    "injected-delivery-fault");
            }
            return inner.TryDeliver(committed);
        }
        public OutcomeAcknowledgeResult Acknowledge(
            in CommittedOutcomeToken committed) =>
            inner.Acknowledge(committed);
        public int RetryPendingDeliveries(int maximumCount) =>
            inner.RetryPendingDeliveries(maximumCount);
    }
}
#endif
