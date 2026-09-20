#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class CaptiveEscapeOutcomeDebugScenarios
{
    private const string CaptiveIdPrefix = "character:qa-captive-escape-";

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyCanonicalKinds();
        VerifyProductionOwners();
        VerifyCommitBoundaries();
        VerifyCaptivitySaveCompatibility();
        if (logSuccess)
            Debug.Log("[Captive Escape Outcome] PASS");
        return true;
    }

    private static void VerifyCanonicalKinds()
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new CaptiveEscapeOutcomeDescriptor(new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new CaptiveEscapeOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = NewLedger(registry, "run:captive-escape");
        CaptiveEscapeGameplayOutcomeBridge bridge = new(
            new GameplayOutcomeRecorder(ledger, registry, new GameEventBus()),
            ledger);
        GameEventBus eventBus = new();
        int legacyEventCount = 0;
        using IDisposable subscription = eventBus.Subscribe<CaptiveEscapedEvent>(
            _ => legacyEventCount++);
        CaptiveEscapeOutcomeRuntime runtime = new(bridge, eventBus);
        EscapeCase[] cases =
        {
            new(
                "physical",
                CaptiveEscapeOutcomeKind.PhysicalEscape,
                CaptivityStatus.EscapeAttempt,
                "감방 외벽 통과",
                42f),
            new(
                "arrival",
                CaptiveEscapeOutcomeKind.ArrivalCustodyLoss,
                CaptivityStatus.AwaitingCapture,
                "귀환 수용 전 이탈",
                37f),
            new(
                "betrayal",
                CaptiveEscapeOutcomeKind.FalseComplianceBetrayal,
                CaptivityStatus.Labor,
                "침공의 혼란",
                66f),
            new(
                "minion",
                CaptiveEscapeOutcomeKind.MinionControlBreak,
                CaptivityStatus.Minion,
                "정착지 통제 거부",
                58f)
        };

        foreach (EscapeCase escapeCase in cases)
        {
            CaptiveState state = State(escapeCase.Suffix, escapeCase.PreviousStatus);
            Require(
                runtime.TryPrepare(
                    state,
                    escapeCase.Kind,
                    "  " + escapeCase.Trigger + "  ",
                    escapeCase.PreviousStatus,
                    escapeCase.Pressure,
                    12,
                    out PreparedCaptiveEscapeOutcome prepared,
                    out long revision,
                    out string trigger,
                    out string failure),
                escapeCase.Suffix + " prepare failed: " + failure);
            Require(
                revision == 1L
                && trigger == escapeCase.Trigger
                && !prepared.IsReplay,
                escapeCase.Suffix + " prepare did not freeze canonical inputs");
            state.status = CaptivityStatus.Escaped;
            state.escapeOutcomeRevision = revision;
            OwnerOutcomeCommitResult commit = runtime.Commit(prepared);
            Require(
                commit.DurablyCommitted,
                escapeCase.Suffix + " commit was not durable: " + commit.DetailCode);
            runtime.NotifyCommitted(
                state,
                trigger,
                CaptiveEscapeOutcomeIds.IsBetrayal(escapeCase.Kind));
        }

        Require(legacyEventCount == cases.Length, "legacy escape observer count drifted");
        GameplayOutcomeQueryPage global = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            global.Items.Count == cases.Length
            && global.Items.All(item => item.Exact != null),
            "global query did not expose four exact escape outcomes");
        foreach (EscapeCase escapeCase in cases)
        {
            GameplayEntityId captive = new(
                CaptiveEscapeOutcomeIds.CharacterKind,
                Id(escapeCase.Suffix));
            GameplayOutcomeQueryPage character = ledger.GetForEntity(
                captive,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All);
            Require(
                character.Items.Count == 1
                && character.Items[0].Exact.participants.Single().displayText == "가람"
                && Fact(
                    character.Items[0].Exact,
                    CaptiveEscapeOutcomeIds.KindFact.Value)
                    == CaptiveEscapeOutcomeIds.KindId(escapeCase.Kind)
                && Fact(
                    character.Items[0].Exact,
                    CaptiveEscapeOutcomeIds.TriggerFact.Value)
                    == escapeCase.Trigger,
                escapeCase.Suffix + " exact receipt shape drifted");
            GameplayOutcomeSnapshot exact = character.Items[0].Exact;
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
                && view.Text.Contains(escapeCase.Trigger, StringComparison.Ordinal),
                escapeCase.Suffix + " Korean projection was not deterministic");
        }

        EscapeCase replayCase = cases[0];
        CaptiveState replayState = State(
            replayCase.Suffix,
            replayCase.PreviousStatus);
        Require(
            runtime.TryPrepare(
                replayState,
                replayCase.Kind,
                replayCase.Trigger,
                replayCase.PreviousStatus,
                replayCase.Pressure,
                12,
                out PreparedCaptiveEscapeOutcome replay,
                out _,
                out _,
                out string replayFailure)
            && replay.IsReplay
            && runtime.Commit(replay).DurablyCommitted
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == cases.Length,
            "canonical escape replay failed or duplicated: " + replayFailure);

        CaptiveEscapeOutcomeReceipt drifted = new(
            Id(replayCase.Suffix),
            Name(replayCase.Suffix),
            replayCase.Kind,
            "다른 탈출 원인",
            false,
            replayCase.PreviousStatus,
            replayCase.Pressure,
            1L,
            12);
        Require(
            !bridge.TryPrepare(drifted, out _, out _),
            "same escape result key accepted a drifted receipt");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = NewLedger(
            registry,
            "run:captive-escape-restore");
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        string[] beforeHashes = global.Items
            .Select(item => item.Exact.immutablePayloadHash)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] afterHashes = restored.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All)
            .Items.Select(item => item.Exact.immutablePayloadHash)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(
            beforeHashes.SequenceEqual(afterHashes),
            "escape outcomes changed across ledger save round trip");
    }

    private static void VerifyCommitBoundaries()
    {
        CaptiveState prepareFailed = State(
            "prepare-failed",
            CaptivityStatus.EscapeAttempt);
        CaptiveEscapeOutcomeRuntime prepareRuntime = new(
            new ConfigurableCommitter(
                OwnerOutcomeCommitPhase.PublishedAcknowledged,
                string.Empty,
                prepareSucceeds: false),
            new GameEventBus());
        Require(
            !prepareRuntime.TryPrepare(
                prepareFailed,
                CaptiveEscapeOutcomeKind.PhysicalEscape,
                "외벽 통과",
                CaptivityStatus.EscapeAttempt,
                20f,
                3,
                out _,
                out _,
                out _,
                out string prepareFailure)
            && prepareFailure == "qa-prepare-rejected"
            && prepareFailed.status == CaptivityStatus.EscapeAttempt
            && prepareFailed.escapeOutcomeRevision == 0L,
            "prepare failure changed captive state");

        CaptiveState rejected = State("rejected", CaptivityStatus.Labor);
        CaptiveEscapeOutcomeRuntime rejectRuntime = new(
            new ConfigurableCommitter(
                OwnerOutcomeCommitPhase.Rejected,
                "qa-definite-rejection"),
            new GameEventBus());
        Require(
            rejectRuntime.TryPrepare(
                rejected,
                CaptiveEscapeOutcomeKind.FalseComplianceBetrayal,
                "배신 기회",
                CaptivityStatus.Labor,
                55f,
                4,
                out PreparedCaptiveEscapeOutcome rejectedPrepared,
                out _,
                out _,
                out string rejectPrepareFailure),
            "rejection scenario prepare failed: " + rejectPrepareFailure);
        OwnerOutcomeCommitResult rejectedCommit = rejectRuntime.Commit(
            rejectedPrepared);
        Require(
            !rejectedCommit.DurablyCommitted
            && rejectedCommit.DetailCode == "qa-definite-rejection"
            && rejected.status == CaptivityStatus.Labor
            && rejected.escapeOutcomeRevision == 0L,
            "definite rejection was reported as durable or mutated state");

        GameEventBus pendingBus = new();
        int pendingEvents = 0;
        using IDisposable pendingSubscription =
            pendingBus.Subscribe<CaptiveEscapedEvent>(_ => pendingEvents++);
        CaptiveState pending = State("pending", CaptivityStatus.Minion);
        CaptiveEscapeOutcomeRuntime pendingRuntime = new(
            new ConfigurableCommitter(
                OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                "qa-delivery-pending"),
            pendingBus);
        Require(
            pendingRuntime.TryPrepare(
                pending,
                CaptiveEscapeOutcomeKind.MinionControlBreak,
                "통제 거부",
                CaptivityStatus.Minion,
                48f,
                5,
                out PreparedCaptiveEscapeOutcome pendingPrepared,
                out long pendingRevision,
                out string pendingTrigger,
                out string pendingFailure),
            "pending scenario prepare failed: " + pendingFailure);
        OwnerOutcomeCommitResult pendingCommit = pendingRuntime.Commit(
            pendingPrepared);
        Require(
            pendingCommit.DurablyCommitted
            && pendingCommit.Phase
                == OwnerOutcomeCommitPhase.CommittedPendingDelivery,
            "durable pending delivery was treated as rejection");
        pending.status = CaptivityStatus.Escaped;
        pending.escapeOutcomeRevision = pendingRevision;
        pendingRuntime.NotifyCommitted(pending, pendingTrigger, betrayal: true);
        Require(pendingEvents == 1, "pending delivery suppressed post-commit observer");

        CaptiveState observerFault = State(
            "observer-fault",
            CaptivityStatus.AwaitingCapture);
        CaptiveEscapeOutcomeRuntime observerRuntime = new(
            new ConfigurableCommitter(
                OwnerOutcomeCommitPhase.PublishedAcknowledged,
                string.Empty),
            new ThrowingEventBus());
        Require(
            observerRuntime.TryPrepare(
                observerFault,
                CaptiveEscapeOutcomeKind.ArrivalCustodyLoss,
                "귀환 수용 전 이탈",
                CaptivityStatus.AwaitingCapture,
                33f,
                6,
                out PreparedCaptiveEscapeOutcome observerPrepared,
                out long observerRevision,
                out string observerTrigger,
                out string observerPrepareFailure),
            "observer scenario prepare failed: " + observerPrepareFailure);
        Require(
            observerRuntime.Commit(observerPrepared).DurablyCommitted,
            "observer scenario did not commit");
        observerFault.status = CaptivityStatus.Escaped;
        observerFault.escapeOutcomeRevision = observerRevision;
        observerRuntime.NotifyCommitted(
            observerFault,
            observerTrigger,
            betrayal: false);
        Require(
            observerFault.escapeOutcomeRevision == 1L,
            "observer exception escaped or rolled back a durable result");
    }

    private static void VerifyProductionOwners()
    {
        using (OwnerFixture physical = new(
                   "owner-physical",
                   CaptivityStatus.EscapeAttempt,
                   OwnerOutcomeCommitPhase.Rejected,
                   "qa-physical-rejected"))
        {
            physical.State.restrained = true;
            physical.State.grudge = 25f;
            physical.Actor.SetAiPaused(true);
            physical.Doors.SetCaptive(physical.Id, true);
            string before = JsonUtility.ToJson(physical.State);
            Require(
                !physical.Completion.CompleteEscape(
                    physical.Id,
                    physical.Actor,
                    "외벽 통과",
                    out string failure)
                && failure == "qa-physical-rejected"
                && JsonUtility.ToJson(physical.State) == before
                && physical.Actor.characterType == CharacterType.NPC
                && physical.Actor.IsAiPaused()
                && physical.Doors.IsCaptive(physical.Id)
                && physical.EventCount == 0,
                "physical escape rejection did not restore exact owner state");

            ConfigurableCommitter pending = new(
                OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                "qa-physical-pending");
            CaptivityEscapeCompletionRuntime retry = physical.NewCompletion(pending);
            Require(
                retry.CompleteEscape(
                    physical.Id,
                    physical.Actor,
                    "외벽 통과",
                    out string retryFailure)
                && retryFailure.Length == 0
                && physical.State.status == CaptivityStatus.Escaped
                && physical.State.escapeOutcomeRevision == 1L
                && physical.Actor.characterType == CharacterType.Intruder
                && !physical.Actor.IsAiPaused()
                && !physical.Doors.IsCaptive(physical.Id)
                && physical.EventCount == 1
                && pending.LastReceipt.Kind
                    == CaptiveEscapeOutcomeKind.PhysicalEscape,
                "persisted physical escape did not retry into one durable result");
        }

        using (OwnerFixture arrival = new(
                   "owner-arrival",
                   CaptivityStatus.AwaitingCapture,
                   OwnerOutcomeCommitPhase.PublishedAcknowledged,
                   string.Empty))
        {
            Require(
                arrival.Completion.CompleteEscape(
                    arrival.Id,
                    arrival.Actor,
                    "귀환 수용 전 이탈",
                    out string failure)
                && failure.Length == 0
                && arrival.Committer.LastReceipt.Kind
                    == CaptiveEscapeOutcomeKind.ArrivalCustodyLoss,
                "arrival custody loss did not use its distinct receipt kind");
        }

        using (OwnerFixture betrayalRejected = new(
                   "owner-betrayal-rejected",
                   CaptivityStatus.Labor,
                   OwnerOutcomeCommitPhase.Rejected,
                   "qa-betrayal-rejected"))
        {
            betrayalRejected.State.falseCompliance = true;
            betrayalRejected.State.restrained = true;
            betrayalRejected.State.grudge = 40f;
            betrayalRejected.Actor.SetAiPaused(true);
            betrayalRejected.Doors.SetCaptive(betrayalRejected.Id, true);
            string before = JsonUtility.ToJson(betrayalRejected.State);
            Require(
                !betrayalRejected.Defection.TryTriggerBetrayal(
                    betrayalRejected.Id,
                    "침공의 혼란",
                    out string failure)
                && failure == "qa-betrayal-rejected"
                && JsonUtility.ToJson(betrayalRejected.State) == before
                && betrayalRejected.Actor.characterType == CharacterType.NPC
                && betrayalRejected.Actor.IsAiPaused()
                && betrayalRejected.Doors.IsCaptive(betrayalRejected.Id)
                && betrayalRejected.EventCount == 0,
                "betrayal rejection did not restore captive/actor/door owners");
        }

        using (OwnerFixture betrayalPending = new(
                   "owner-betrayal-pending",
                   CaptivityStatus.Performer,
                   OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                   "qa-betrayal-pending"))
        {
            betrayalPending.State.falseCompliance = true;
            betrayalPending.State.restrained = true;
            Require(
                betrayalPending.Defection.TryTriggerBetrayal(
                    betrayalPending.Id,
                    "무대 뒤 혼란",
                    out string failure)
                && failure.Length == 0
                && betrayalPending.State.status == CaptivityStatus.Escaped
                && betrayalPending.State.escapeOutcomeRevision == 1L
                && betrayalPending.Committer.LastReceipt.Kind
                    == CaptiveEscapeOutcomeKind.FalseComplianceBetrayal
                && betrayalPending.EventCount == 1,
                "durable pending betrayal did not remain committed");
        }

        using (OwnerFixture minionRejected = new(
                   "owner-minion-rejected",
                   CaptivityStatus.Minion,
                   OwnerOutcomeCommitPhase.Rejected,
                   "qa-minion-rejected"))
        {
            minionRejected.Population.Standing = CharacterSettlementStanding.Minion;
            minionRejected.Employment.Standing = CharacterSettlementStanding.Minion;
            minionRejected.State.rehabilitationInProgress = true;
            minionRejected.State.reservedWardenId = "character:qa-warden";
            string before = JsonUtility.ToJson(minionRejected.State);
            Require(
                !minionRejected.Defection.TryBreakMinionControl(
                    minionRejected.Id,
                    "통제 거부",
                    out string failure)
                && failure == "qa-minion-rejected"
                && JsonUtility.ToJson(minionRejected.State) == before
                && minionRejected.Actor.characterType == CharacterType.NPC
                && minionRejected.Actor.Identity.CharacterType == CharacterType.NPC
                && minionRejected.Population.Standing
                    == CharacterSettlementStanding.Minion
                && minionRejected.Population.RollbackCount == 1
                && minionRejected.Population.CompleteCount == 0
                && minionRejected.Employment.Standing
                    == CharacterSettlementStanding.Minion
                && minionRejected.EventCount == 0,
                "minion rejection did not restore every reversible owner");
        }

        using (OwnerFixture minionPending = new(
                   "owner-minion-pending",
                   CaptivityStatus.Minion,
                   OwnerOutcomeCommitPhase.CommittedPendingDelivery,
                   "qa-minion-pending"))
        {
            minionPending.Population.Standing = CharacterSettlementStanding.Minion;
            minionPending.Employment.Standing = CharacterSettlementStanding.Minion;
            Require(
                minionPending.Defection.TryBreakMinionControl(
                    minionPending.Id,
                    "통제에서 벗어남",
                    out string failure)
                && failure.Length == 0
                && minionPending.State.status == CaptivityStatus.Escaped
                && minionPending.State.escapeOutcomeRevision == 1L
                && minionPending.Actor.characterType == CharacterType.Intruder
                && minionPending.Actor.Identity.CharacterType
                    == CharacterType.Intruder
                && minionPending.Population.Standing
                    == CharacterSettlementStanding.PreparedCandidate
                && minionPending.Population.CompleteCount == 1
                && minionPending.Population.RollbackCount == 0
                && minionPending.Employment.Standing
                    == CharacterSettlementStanding.PreparedCandidate
                && minionPending.Committer.LastReceipt.Kind
                    == CaptiveEscapeOutcomeKind.MinionControlBreak
                && minionPending.EventCount == 1,
                "durable pending minion defection did not commit every owner");
        }
    }

    private static void VerifyCaptivitySaveCompatibility()
    {
        CaptiveState committed = State("save", CaptivityStatus.Escaped);
        committed.escapeOutcomeRevision = 1L;
        CaptivitySaveData source = Save(committed);
        CaptivitySaveData roundTrip = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(source));
        DungeonGameRestoreReport roundTripReport = new();
        CaptivitySaveValidation.Validate(roundTrip, roundTripReport);
        Require(
            roundTripReport.Success
            && roundTrip.captives.Single().escapeOutcomeRevision == 1L,
            "captivity save round trip lost escape revision: "
            + string.Join(" | ", roundTripReport.Errors));

        CaptiveState legacy = State("legacy", CaptivityStatus.Escaped);
        DungeonGameRestoreReport legacyReport = new();
        CaptivitySaveValidation.Validate(Save(legacy), legacyReport);
        Require(
            legacyReport.Success,
            "legacy escaped state with revision zero was rejected: "
            + string.Join(" | ", legacyReport.Errors));

        CaptiveState negative = committed.Clone();
        negative.escapeOutcomeRevision = -1L;
        DungeonGameRestoreReport negativeReport = new();
        CaptivitySaveValidation.Validate(Save(negative), negativeReport);
        Require(!negativeReport.Success, "negative escape revision was accepted");

        CaptiveState torn = committed.Clone();
        torn.status = CaptivityStatus.Confined;
        DungeonGameRestoreReport tornReport = new();
        CaptivitySaveValidation.Validate(Save(torn), tornReport);
        Require(
            !tornReport.Success,
            "positive escape revision on a non-escaped state was accepted");
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

    private static CaptiveState State(
        string suffix,
        CaptivityStatus status) => new()
    {
        captiveId = Id(suffix),
        displayName = "가람",
        status = status,
        policyId = CaptivityPolicyIds.Standard,
        restrained = status is CaptivityStatus.Confined
            or CaptivityStatus.Labor
            or CaptivityStatus.Performer
            or CaptivityStatus.EscapeAttempt
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

    private static string Id(string suffix) => CaptiveIdPrefix + suffix;

    private static KoreanNameSnapshot Name(string suffix)
    {
        string id = Id(suffix);
        return new KoreanNameSnapshot(
            "가람",
            "captivity-escape-display-v1:"
                + NarrativeInferenceHash.ComputeSha256Utf8(id + "|가람"),
            KoreanPronunciationHint.AutoHangulDisplay(
                "captivity-escape-pronunciation-v1:" + id),
            "ko-KR");
    }

    private static string Fact(GameplayOutcomeSnapshot snapshot, string factId) =>
        snapshot.facts.Single(value => value.factId == factId).value;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct EscapeCase
    {
        public EscapeCase(
            string suffix,
            CaptiveEscapeOutcomeKind kind,
            CaptivityStatus previousStatus,
            string trigger,
            float pressure)
        {
            Suffix = suffix;
            Kind = kind;
            PreviousStatus = previousStatus;
            Trigger = trigger;
            Pressure = pressure;
        }

        public string Suffix { get; }
        public CaptiveEscapeOutcomeKind Kind { get; }
        public CaptivityStatus PreviousStatus { get; }
        public string Trigger { get; }
        public float Pressure { get; }
    }

    private sealed class OwnerFixture : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly IDisposable subscription;
        private readonly CaptivityActorAccess actors;
        private readonly CaptivityActorRuntimeLookup actorRuntime;
        private readonly FakeClock clock = new();

        public OwnerFixture(
            string suffix,
            CaptivityStatus status,
            OwnerOutcomeCommitPhase phase,
            string detail)
        {
            Id = CaptiveIdPrefix + suffix;
            Actor = CreateActor(suffix);
            State = State(suffix, status);
            actors = new CaptivityActorAccess(
                new DungeonRuntimeAggregateRootStore(),
                _ => { });
            actors.AddState(State);
            actorRuntime = new CaptivityActorRuntimeLookup(id =>
                string.Equals(id, Id, StringComparison.Ordinal) ? Actor : null);
            Doors = new FakeDoorSubjects();
            Doors.SetCaptive(Id, State.IsInCustody);
            Population = new FakePopulationStandingPort();
            Employment = new FakeEmploymentStanding();
            Committer = new ConfigurableCommitter(phase, detail);
            Events = new GameEventBus();
            subscription = Events.Subscribe<CaptiveEscapedEvent>(_ => EventCount++);
            Completion = NewCompletion(Committer);
            Defection = new CaptivityDefectionRuntime(
                actors,
                actorRuntime,
                new AlwaysChanceRandom(),
                Doors,
                Population,
                Employment,
                clock,
                new CaptiveEscapeOutcomeRuntime(Committer, Events));
        }

        public string Id { get; }
        public CharacterActor Actor { get; }
        public CaptiveState State { get; }
        public FakeDoorSubjects Doors { get; }
        public FakePopulationStandingPort Population { get; }
        public FakeEmploymentStanding Employment { get; }
        public ConfigurableCommitter Committer { get; }
        public GameEventBus Events { get; }
        public CaptivityEscapeCompletionRuntime Completion { get; }
        public CaptivityDefectionRuntime Defection { get; }
        public int EventCount { get; private set; }

        public CaptivityEscapeCompletionRuntime NewCompletion(
            ICaptiveEscapeOutcomeCommitter committer) => new(
            actors,
            actorRuntime,
            Doors,
            clock,
            new CaptiveEscapeOutcomeRuntime(committer, Events));

        public void Dispose()
        {
            subscription?.Dispose();
            foreach (UnityEngine.Object value in cleanup.Where(value => value != null))
                UnityEngine.Object.DestroyImmediate(value);
        }

        private CharacterActor CreateActor(string suffix)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    "가람",
                    "human");
            cleanup.Add(data);
            GameObject actorObject = new("Captive Escape " + suffix);
            cleanup.Add(actorObject);
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.data = data;
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId(Id);
            actor.Identity.SetCharacterType(CharacterType.NPC);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats = new Dictionary<CharacterCondition, float>
            {
                { CharacterCondition.SLEEP, 100f },
                { CharacterCondition.HUNGER, 100f },
                { CharacterCondition.FUN, 100f },
                { CharacterCondition.MOOD, 100f },
                { CharacterCondition.EXCRETION, 100f },
                { CharacterCondition.HYGIENE, 100f }
            };
            return actor;
        }
    }

    private sealed class FakeDoorSubjects : IDoorAccessSubjectRegistry
    {
        private readonly HashSet<string> captives = new(StringComparer.Ordinal);

        public bool IsCaptive(string id) => captives.Contains(id);

        public void SetCaptive(string persistentId, bool captive)
        {
            if (captive)
                captives.Add(persistentId);
            else
                captives.Remove(persistentId);
        }

        public void SetCapturedWildlife(string wildlifeId, bool captured)
        {
        }

        public void ReplaceCaptiveSubjects(IEnumerable<string> persistentIds)
        {
            captives.Clear();
            foreach (string id in persistentIds ?? Array.Empty<string>())
                captives.Add(id);
        }

        public void ReplaceCapturedWildlifeSubjects(IEnumerable<string> wildlifeIds)
        {
        }
    }

    private sealed class FakeClock : IGameClock
    {
        public float DeltaTime => 0.02f;
        public float Time => GameCalendarRules.SecondsPerDay * 9f;
        public int FrameCount => 100;
        public bool IsPaused => false;
    }

    private sealed class AlwaysChanceRandom : IRandomStream
    {
        public ulong State => 1UL;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() => 0f;
        public bool Chance(float probability) => true;
        public void Restore(ulong state)
        {
        }
    }

    private sealed class FakePopulationStandingPort :
        ICaptivityPopulationStandingPort
    {
        public CharacterSettlementStanding Standing { get; set; }
        public int RollbackCount { get; private set; }
        public int CompleteCount { get; private set; }

        public ICaptivityPopulationStandingTransition Begin(
            CharacterActor actor,
            CharacterSettlementStanding standing)
        {
            CharacterSettlementStanding previous = Standing;
            Standing = standing;
            return new Transition(this, previous);
        }

        private sealed class Transition : ICaptivityPopulationStandingTransition
        {
            private readonly FakePopulationStandingPort owner;
            private readonly CharacterSettlementStanding previous;
            private bool active = true;

            public Transition(
                FakePopulationStandingPort owner,
                CharacterSettlementStanding previous)
            {
                this.owner = owner;
                this.previous = previous;
            }

            public void Rollback()
            {
                if (!active)
                    return;
                owner.Standing = previous;
                owner.RollbackCount++;
                active = false;
            }

            public void Complete()
            {
                if (!active)
                    throw new InvalidOperationException("qa-transition-inactive");
                owner.CompleteCount++;
                active = false;
            }
        }
    }

    private sealed class FakeEmploymentStanding : IEmploymentStandingCommand
    {
        private readonly Dictionary<string, CharacterSettlementStanding> captured =
            new(StringComparer.Ordinal);

        public CharacterSettlementStanding Standing { get; set; }

        public EmploymentStandingState CaptureStandingState(string characterId)
        {
            captured[characterId] = Standing;
            return new EmploymentStandingState(characterId, null, null);
        }

        public void ApplyStanding(
            string characterId,
            CharacterSettlementStanding standing) => Standing = standing;

        public void RestoreStandingState(EmploymentStandingState snapshot)
        {
            if (captured.TryGetValue(snapshot.CharacterId, out var standing))
                Standing = standing;
        }
    }

    private sealed class ConfigurableCommitter : ICaptiveEscapeOutcomeCommitter
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

        public CaptiveEscapeOutcomeReceipt LastReceipt { get; private set; }

        public bool TryPrepare(
            in CaptiveEscapeOutcomeReceipt receipt,
            out PreparedCaptiveEscapeOutcome prepared,
            out string failureReason)
        {
            if (!prepareSucceeds)
            {
                prepared = default;
                failureReason = "qa-prepare-rejected";
                return false;
            }
            LastReceipt = receipt;
            prepared = new PreparedCaptiveEscapeOutcome(
                default,
                receipt.ResultKey,
                receipt.OwnerRevision,
                false);
            failureReason = string.Empty;
            return true;
        }

        public OwnerOutcomeCommitResult Commit(
            in PreparedCaptiveEscapeOutcome prepared) => new(
            phase,
            prepared.ResultKey,
            default,
            string.Empty,
            detail);

        public void Cancel(in PreparedCaptiveEscapeOutcome prepared)
        {
        }
    }

    private sealed class ThrowingEventBus : IGameEventBus
    {
        public IDisposable Subscribe<TEvent>(Action<TEvent> listener) =>
            throw new NotSupportedException();

        public void Publish<TEvent>(TEvent gameEvent) =>
            throw new InvalidOperationException("qa-event-observer-fault");

        public void Clear()
        {
        }
    }
}
#endif
