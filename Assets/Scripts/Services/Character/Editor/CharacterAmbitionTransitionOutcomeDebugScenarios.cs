#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterAmbitionTransitionOutcomeDebugScenarios
{
    private const string CharacterIdValue =
        "character:qa:ambition-transition-owner";
    private const string CharacterName = "연화";
    private const int StartDay = 5;

    [MenuItem("Dungeon Story/QA/V27/Character Ambition Transition Outcome")]
    public static void RunFromMenu() => RunOrThrow();

    public static void RunCN01FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_CN01_AMBITION_TRANSITION_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:N01 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:N01 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:N01 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:N01",
            ["passed"] = false,
            ["activeStateNoOutcome"] = false,
            ["dueFailureExact"] = false,
            ["sameDayRedeliveryNoOp"] = false,
            ["saveRestoreRoundTrip"] = false,
            ["prepareFailureRolledBack"] = false,
            ["commitFailureRolledBack"] = false,
            ["realCalendarEntryExact"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Services/Character/CharacterNarrativeRuntime.cs",
                "Assets/Scripts/Services/Character/Editor/CharacterAmbitionTransitionOutcomeDebugScenarios.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CharacterAmbitionTransitionOutcomeDefinition.cs"
            }
        };

        try
        {
            RunCore(out AmbitionTransitionEvidence evidence);
            report["activeStateNoOutcome"] = true;
            report["dueFailureExact"] = true;
            report["sameDayRedeliveryNoOp"] = true;
            report["saveRestoreRoundTrip"] = true;
            report["prepareFailureRolledBack"] = true;
            report["commitFailureRolledBack"] = true;
            report["realCalendarEntryExact"] = true;
            report["activationExample"] = JObject.FromObject(
                evidence.ActivationOutcome);
            report["failureExample"] = JObject.FromObject(
                evidence.FailureOutcome);
            report["activeSavedCharacter"] = JObject.FromObject(
                evidence.ActiveSave.characters.Single());
            report["failedSavedCharacter"] = JObject.FromObject(
                evidence.FailedSave.characters.Single());
            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:N01] PASS start=1; active-noop=1; "
                + "due-failure=1; same-day-duplicate=0; save-restore=2");
        }
        catch (Exception exception)
        {
            report["failure"] = exception.GetType().Name
                + ": "
                + exception.Message;
            if (!File.Exists(reportPath))
                WriteCreateOnlyJson(reportPath, report);
            Debug.LogException(exception);
            throw;
        }
    }

    public static bool RunOrThrow() => RunCore(out _);

    public static void RunObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Artifacts", "QA"))
            + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || !Path.IsPathRooted(output)
            || !Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Ambition-transition observer checks require new project-local batch evidence.");
        }
        string probe = File.ReadAllText(Path.Combine(
            Directory.GetParent(Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
            && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true")
            && probe.Contains("inJob=true"),
            "Ambition-transition observer checks require the noninteractive desktop probe.");
        Require(RunOrThrow(), "Ambition-transition focused observer checks failed.");
        Require(TraitAnalysisRecoveryDebugScenarios.RunAll(),
            "Trait-analysis shared narrative-save regression failed.");
        Debug.Log(
            "B81_AMBITION_TRANSITION_OBSERVER_FOCUSED=PASS "
            + "trait-analysis-regression=true");
    }

    private static bool RunCore(out AmbitionTransitionEvidence evidence)
    {
        evidence = null;
        VerifyCalendarRolloverNotifications();
        VerifyClockBoundAmbition();
        VerifyObserverLifecycleAndRecovery();
        using Fixture fixture = new();

        fixture.Outcomes.FailNextReserve = true;
        RequireThrows(
            () => fixture.PublishEndedDay(StartDay - 1),
            "observation reserve failed");
        fixture.RequireState(
            CharacterAmbitionStatus.None,
            startDay: 0,
            dueDay: 0,
            progress: 0,
            nextAllowedDay: 0);
        fixture.RequireOutcomeCount(0);

        fixture.PublishEndedDay(StartDay - 1);
        fixture.RequireState(
            CharacterAmbitionStatus.Active,
            StartDay,
            fixture.DueDay,
            progress: 0,
            nextAllowedDay: 0);
        GameplayOutcomeSnapshot activationOutcome =
            fixture.RequireTransitionOutcome(
                CharacterAmbitionStatus.Active,
                CharacterAmbitionTransitionOutcomeIds.ActivationReason,
                StartDay);
        fixture.RequireOutcomeCount(1);

        fixture.PublishEndedDay(StartDay - 1);
        fixture.RequireOutcomeCount(1);

        CharacterNarrativeWorldSaveData activeSave = fixture.Capture();
        Require(
            activeSave.characters.Single().ambitionStartedAbsoluteDay
                == StartDay,
            "Active ambition start day was not persisted.");
        VerifyNegativeStartDayRejected(fixture, activeSave);
        fixture.Restart(activeSave);
        fixture.PublishEndedDay(StartDay - 1);
        fixture.RequireState(
            CharacterAmbitionStatus.Active,
            StartDay,
            fixture.DueDay,
            progress: 0,
            nextAllowedDay: 0);
        fixture.RequireOutcomeCount(1);

        fixture.Requirements.Mode = RequirementMode.Disabled;
        fixture.Outcomes.RejectNextPublish = true;
        RequireThrows(
            () => fixture.PublishEndedDay(fixture.DueDay - 1),
            "observation incomplete");
        fixture.RequireState(
            CharacterAmbitionStatus.Active,
            StartDay,
            fixture.DueDay,
            progress: 0,
            nextAllowedDay: 0);
        fixture.RequireOutcomeCount(1);

        fixture.PublishEndedDay(fixture.DueDay - 1);
        fixture.RequireState(
            CharacterAmbitionStatus.Failed,
            startDay: 0,
            dueDay: 0,
            progress: 0,
            nextAllowedDay: fixture.DueDay + 10);
        GameplayOutcomeSnapshot failureOutcome =
            fixture.RequireTransitionOutcome(
                CharacterAmbitionStatus.Failed,
                CharacterAmbitionTransitionOutcomeIds.DeadlineReason,
                fixture.DueDay);
        fixture.RequireOutcomeCount(2);

        CharacterNarrativeWorldSaveData failedSave = fixture.Capture();
        Require(
            failedSave.characters.Single().ambitionStartedAbsoluteDay == 0,
            "Closed ambition retained a stale start day.");
        fixture.Restart(failedSave);
        fixture.PublishEndedDay(fixture.DueDay - 1);
        fixture.RequireState(
            CharacterAmbitionStatus.Failed,
            startDay: 0,
            dueDay: 0,
            progress: 0,
            nextAllowedDay: fixture.DueDay + 10);
        fixture.RequireOutcomeCount(2);

        evidence = new AmbitionTransitionEvidence(
            activationOutcome,
            failureOutcome,
            activeSave,
            failedSave);

        Debug.Log(
            "CHARACTER_AMBITION_TRANSITION_OUTCOME=PASS "
            + "real-day-ended-entry=1 start=1 active-noop=1 failure=1 "
            + "prepare-reject-rollback=1 commit-reject-rollback=1 "
            + "save-restore=2 same-day-replay-noop=2 exact-outcomes=2 "
            + "negative-schedule-rejected=1 observer-recovery=1 "
            + "deadline-authority=scriptable-object");
        return true;
    }

    private static void VerifyObserverLifecycleAndRecovery()
    {
        using Fixture fixture = new();
        RequireThrows(
            fixture.ConnectDuplicateObserver,
            "Duplicate durable domain observer");

        fixture.DisconnectObserver();
        RequireThrows(
            () => fixture.PublishEndedDay(StartDay - 1),
            "domain-fact-observer-missing");
        fixture.RequireState(
            CharacterAmbitionStatus.None,
            startDay: 0,
            dueDay: 0,
            progress: 0,
            nextAllowedDay: 0);
        fixture.RequireOutcomeCount(0);
        fixture.ConnectObserver();

        fixture.Outcomes.ThrowNextPublishBeforeCommit = true;
        RequireThrows(
            () => fixture.PublishEndedDay(StartDay - 1),
            "injected-ambition-precommit-exception");
        fixture.RequireState(
            CharacterAmbitionStatus.None,
            startDay: 0,
            dueDay: 0,
            progress: 0,
            nextAllowedDay: 0);
        fixture.RequireOutcomeCount(0);

        fixture.Outcomes.HideNextCommittedAcknowledgement = true;
        RequireThrows(
            () => fixture.PublishEndedDay(StartDay - 1),
            "Ambition transition observation incomplete");
        fixture.RequireState(
            CharacterAmbitionStatus.Active,
            StartDay,
            fixture.DueDay,
            progress: 0,
            nextAllowedDay: 0);
        fixture.RequireOutcomeCount(1);

        CharacterNarrativeWorldSaveData saved = fixture.Capture();
        CharacterAmbitionTransitionObservationSaveData pending =
            saved.ambitionTransitionObservations.Single();
        Require(
            pending.fact.characterId == CharacterIdValue
            && pending.fact.characterDisplayName == CharacterName
            && pending.fact.transitionDay == StartDay
            && !string.IsNullOrWhiteSpace(pending.publicationKey)
            && string.IsNullOrEmpty(pending.publicationId)
            && string.IsNullOrEmpty(pending.publicationFingerprint),
            "Unknown acknowledgement did not preserve the exact frozen transition fact.");
        Require(!fixture.Narrative.TryPreviewAmbitionProgress(
                new CharacterId(CharacterIdValue),
                amount: 1,
                out _),
            "Pending ambition transition unexpectedly exposed a progress preview.");
        RequireThrows(
            () => fixture.Narrative.AddAmbitionProgress(
                new CharacterId(CharacterIdValue),
                amount: 1,
                absoluteDay: StartDay),
            "observation is pending");
        fixture.RequireState(
            CharacterAmbitionStatus.Active,
            StartDay,
            fixture.DueDay,
            progress: 0,
            nextAllowedDay: 0);

        fixture.ValidatePendingObservationAndTamper(saved);
        CharacterNarrativeWorldSaveData roundTripped =
            JsonUtility.FromJson<CharacterNarrativeWorldSaveData>(
                JsonUtility.ToJson(saved));
        CharacterAmbitionTransitionObservationSaveData clonedPending =
            roundTripped.ambitionTransitionObservations.Single();
        Require(
            clonedPending.fact.characterId == pending.fact.characterId
            && clonedPending.fact.characterDisplayName
                == pending.fact.characterDisplayName
            && clonedPending.fact.transitionDay == pending.fact.transitionDay
            && clonedPending.publicationKey == pending.publicationKey,
            "Ambition transition JSON round trip changed its frozen replay authority.");
        fixture.Restart(roundTripped);
        fixture.RequireState(
            CharacterAmbitionStatus.Active,
            StartDay,
            fixture.DueDay,
            progress: 0,
            nextAllowedDay: 0);
        Require(fixture.Capture().ambitionTransitionObservations.Count == 0,
            "Restored ambition transition observation was not cleared after exact replay.");
        fixture.RequireOutcomeCount(1);
        Require(fixture.Outcomes.CommittedPublishCalls == 1
                && fixture.Outcomes.ReplayBatchCalls == 1,
            "Ambition transition recovery repeated the effect or skipped exact observer replay.");
        fixture.PublishEndedDay(StartDay - 1);
        fixture.RequireOutcomeCount(1);

        fixture.DisconnectObserver();
        fixture.ConnectObserver();
    }

    private static void WriteCreateOnlyJson(string path, JObject value)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(value.ToString());
    }

    private static void VerifyCalendarRolloverNotifications()
    {
        var session = new CalendarSession();
        var clock = new CalendarClock();
        var events = new GameEventBus();
        var calendar = new GameCalendarRuntime(session, events, clock);
        calendar.SetDateTime(4, 23);
        calendar.Start();
        var notifications = new List<string>();
        void RequireMidnight(int currentDay)
        {
            GameSessionSnapshot saved = session.State.Capture();
            Require(calendar.Day == currentDay && calendar.Hour == 0
                    && calendar.ElapsedSeconds == 0f && calendar.TimeOfDay == TimeOfDay.Night
                    && saved.Day == currentDay && saved.Hour == 0 && saved.ElapsedSeconds == 0f,
                "Calendar callback observed a stale or mixed midnight/save state.");
        }
        using IDisposable ended = events.Subscribe<OperatingDayEndedEvent>(value =>
        {
            RequireMidnight(value.day + 1);
            notifications.Add("ended:" + value.day);
        });
        using IDisposable started = events.Subscribe<OperatingDayStartedEvent>(value =>
        {
            RequireMidnight(value.day);
            notifications.Add("started:" + value.day);
        });
        clock.DeltaTime = GameCalendarRules.SecondsPerDay / GameCalendarRules.HoursPerDay
            + GameCalendarRules.SecondsPerDay * 2f + 1f;
        calendar.Tick();
        Require(notifications.SequenceEqual(new[]
            { "ended:4", "started:5", "ended:5", "started:6", "ended:6", "started:7" }),
            "Multi-day Tick skipped, duplicated or reordered boundary notifications.");
        Require(calendar.Day == 7 && calendar.Hour == 0 && Mathf.Abs(calendar.ElapsedSeconds - 1f) < 0.001f,
            "Calendar lost the elapsed remainder after midnight notifications.");
        clock.DeltaTime = 0f;
        calendar.Tick();
        Require(notifications.Count == 6, "A zero-time Tick repeated an already completed day.");
    }

    private static void VerifyClockBoundAmbition()
    {
        using Fixture fixture = new(useRealCalendar: true);
        fixture.TickAcrossMidnight(StartDay - 1);
        fixture.RequireState(CharacterAmbitionStatus.Active, StartDay, fixture.DueDay, 0, 0);
        fixture.RequireTransitionOutcome(CharacterAmbitionStatus.Active,
            CharacterAmbitionTransitionOutcomeIds.ActivationReason, StartDay);
        fixture.RequireOutcomeCount(1);
        fixture.TickWithoutElapsedTime();
        fixture.RequireOutcomeCount(1);
        fixture.Requirements.Mode = RequirementMode.Disabled;
        Require(fixture.DueDay > StartDay, "Clock-bound deadline fixture must have a future due date.");
        RequireThrows(() => fixture.PublishEndedDay(fixture.DueDay - 1),
            "outcome-occurs-after-current-calendar");
        fixture.RequireState(CharacterAmbitionStatus.Active, StartDay, fixture.DueDay, 0, 0);
        fixture.RequireOutcomeCount(1);
        fixture.TickAcrossMidnight(fixture.DueDay - 1);
        fixture.RequireState(CharacterAmbitionStatus.Failed, 0, 0, 0, fixture.DueDay + 10);
        fixture.RequireTransitionOutcome(CharacterAmbitionStatus.Failed,
            CharacterAmbitionTransitionOutcomeIds.DeadlineReason, fixture.DueDay);
        fixture.RequireOutcomeCount(2);
        fixture.TickWithoutElapsedTime();
        fixture.RequireOutcomeCount(2);
        Debug.Log("CLOCK_BOUND_AMBITION=PASS midnight-start=1 midnight-deadline=1 future-rejected-unchanged=1 zero-time-no-replay=2");
    }

    private sealed class CalendarSession : IGameSessionStateProvider
    {
        internal readonly GameSessionState State = new();
        public bool TryGetSessionState(out GameSessionState value) { value = State; return true; }
    }

    private sealed class CalendarClock : IGameClock
    {
        public float DeltaTime { get; set; }
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private static void VerifyNegativeStartDayRejected(
        Fixture fixture,
        CharacterNarrativeWorldSaveData source)
    {
        CharacterNarrativeWorldSaveData tampered = JsonUtility.FromJson<
            CharacterNarrativeWorldSaveData>(JsonUtility.ToJson(source));
        tampered.characters.Single().ambitionStartedAbsoluteDay = -1;
        RequireThrows(
            () => fixture.Narrative.PrepareRestore(tampered),
            "invalid ambition schedule");
    }

    private static void RequireThrows(Action action, string fragment)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            if (exception.ToString().IndexOf(
                    fragment,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }
            throw new InvalidOperationException(
                "Expected failure fragment was missing: " + fragment,
                exception);
        }
        throw new InvalidOperationException(
            "Expected operation to fail: " + fragment);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class AmbitionTransitionEvidence
    {
        internal AmbitionTransitionEvidence(
            GameplayOutcomeSnapshot activationOutcome,
            GameplayOutcomeSnapshot failureOutcome,
            CharacterNarrativeWorldSaveData activeSave,
            CharacterNarrativeWorldSaveData failedSave)
        {
            ActivationOutcome = activationOutcome
                ?? throw new ArgumentNullException(nameof(activationOutcome));
            FailureOutcome = failureOutcome
                ?? throw new ArgumentNullException(nameof(failureOutcome));
            ActiveSave = activeSave
                ?? throw new ArgumentNullException(nameof(activeSave));
            FailedSave = failedSave
                ?? throw new ArgumentNullException(nameof(failedSave));
        }

        internal GameplayOutcomeSnapshot ActivationOutcome { get; }
        internal GameplayOutcomeSnapshot FailureOutcome { get; }
        internal CharacterNarrativeWorldSaveData ActiveSave { get; }
        internal CharacterNarrativeWorldSaveData FailedSave { get; }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CharacterNarrativeCatalog catalog;
        private readonly CharacterAmbitionDefinitionSO ambition;
        private readonly CharacterId characterId = new(CharacterIdValue);
        private readonly GameObject actorObject;
        private readonly CharacterSO actorData;
        private readonly FixedLifeQuery life;
        private readonly FixedCharacterWorld characterWorld;
        private readonly FixedWorldSnapshot world;
        private readonly EvolutionGameplayOutcomeEditorFixture evolution;
        private readonly DomainFactDispatcher facts;
        private readonly GameEventBus events = new();
        private readonly CalendarClock clock;
        private readonly GameCalendarRuntime calendar;
        private IDisposable observerConnection;
        private CharacterNarrativeApplicationAdapter adapter;

        internal Fixture(bool useRealCalendar = false)
        {
            if (useRealCalendar)
            {
                clock = new CalendarClock();
                calendar = new GameCalendarRuntime(new CalendarSession(), events, clock);
                calendar.SetDateTime(StartDay - 1, 23);
            }
            ResourceGameContentCatalog content = new(
                new UnityGameContentRootLoader());
            catalog = new CharacterNarrativeCatalog(content);
            ambition = catalog.Ambitions[0];
            SpeciesCultureDefinitionSO culture = catalog.Cultures[0];
            CharacterSpeciesId speciesId = new(culture.defaultSpeciesId);

            actorData = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    CharacterName,
                    culture.defaultSpeciesId);
            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "Ambition Transition Subject");
            CharacterAiEditorTestDependencies.Inject(actorObject);
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(characterId);
            actor.Initialization(actorData);
            characterWorld = new FixedCharacterWorld(actor);
            world = new FixedWorldSnapshot(actor);

            SpeciesLifeHistoryDefinition history = new(
                speciesId,
                infantEndAgeYears: 2,
                adolescentStartAgeYears: 10,
                adultAgeYears: 16,
                elderAgeYears: 60,
                untreatedExpectedLifeYears: 80f,
                construct: false);
            CharacterLifeRecord lifeRecord = new(
                characterId,
                speciesId,
                chronologicalAgeDays: 20 * GameCalendarRules.DaysPerYear,
                biologicalAgeDayUnits: 20 * GameCalendarRules.DaysPerYear,
                birthdayDayOfYear: 1,
                lifeHistory: history);
            life = new FixedLifeQuery(lifeRecord);
            Requirements = new FixedRequirements(ambition);
            evolution = new EvolutionGameplayOutcomeEditorFixture(
                "run:qa:character-ambition-transition", calendar: calendar);
            facts = new DomainFactDispatcher();
            Outcomes = new FaultingFactPublisher(facts);
            ConnectObserver();
            Narrative = CreateNarrative();
            adapter = CreateAdapter(Narrative);
            adapter.Start();
        }

        internal CharacterNarrativeRuntime Narrative { get; private set; }
        internal FixedRequirements Requirements { get; }
        internal FaultingFactPublisher Outcomes { get; }
        internal int DueDay => checked(StartDay + ambition.deadlineDays);

        internal CharacterNarrativeWorldSaveData Capture() =>
            Narrative.Capture();

        internal void PublishEndedDay(int endedDay) =>
            events.Publish(new OperatingDayEndedEvent(endedDay));

        internal void TickAcrossMidnight(int endedDay)
        {
            calendar.SetDateTime(endedDay, 23);
            calendar.Start();
            clock.DeltaTime = GameCalendarRules.SecondsPerDay / GameCalendarRules.HoursPerDay;
            calendar.Tick();
            Require(calendar.Day == endedDay + 1 && calendar.Hour == 0,
                "Real calendar did not cross the intended midnight.");
        }

        internal void TickWithoutElapsedTime()
        {
            clock.DeltaTime = 0f;
            calendar.Tick();
        }

        internal void Restart(CharacterNarrativeWorldSaveData save)
        {
            adapter.Dispose();
            CharacterNarrativeRuntime restored = new(
                new DungeonRuntimeAggregateRootStore(),
                catalog);
            restored.PublishRestore(restored.PrepareRestore(save));
            Narrative = restored;
            adapter = CreateAdapter(restored);
            adapter.Start();
        }

        internal void ValidatePendingObservationAndTamper(
            CharacterNarrativeWorldSaveData owner)
        {
            CharacterAmbitionTransitionObservationSaveData pending =
                owner.ambitionTransitionObservations.Single();
            GameplayOutcomeLedgerSaveData ledger =
                evolution.Ledger.CaptureGameplayOutcomes();
            CharacterAmbitionTransitionFactPreflight.ValidateObservation(
                pending,
                ledger);

            CharacterAmbitionTransitionObservationSaveData keyTamper =
                pending.Clone();
            keyTamper.publicationKey += ":tampered";
            RequireThrows(
                () => CharacterAmbitionTransitionFactPreflight
                    .ValidateObservation(keyTamper, ledger),
                "key-conflict");

            CharacterAmbitionTransitionObservationSaveData proofTamper =
                pending.Clone();
            proofTamper.publicationId = "outcome:tampered";
            proofTamper.publicationFingerprint = "tampered-fingerprint";
            RequireThrows(
                () => CharacterAmbitionTransitionFactPreflight
                    .ValidateObservation(proofTamper, ledger),
                "canonical-proof-conflict");

            CharacterAmbitionTransitionObservationSaveData dayTamper =
                pending.Clone();
            dayTamper.fact.transitionDay++;
            dayTamper.fact.startDayAfter++;
            dayTamper.fact.dueDayAfter++;
            RequireThrows(
                () => CharacterAmbitionTransitionFactPreflight
                    .ValidateObservation(dayTamper, ledger),
                "key-conflict");

            CharacterAmbitionTransitionObservationSaveData nameTamper =
                pending.Clone();
            nameTamper.fact.characterDisplayName = "변조된 이름";
            RequireThrows(
                () => CharacterAmbitionTransitionFactPreflight
                    .ValidateObservation(nameTamper, ledger),
                "canonical-fact-conflict");

            CharacterNarrativeWorldSaveData oldSchema = Clone(owner);
            oldSchema.version = 12;
            RequireThrows(
                () => Narrative.PrepareRestore(oldSchema),
                "save version is invalid");

            CharacterNarrativeWorldSaveData missingPendingCollection =
                Clone(owner);
            missingPendingCollection.ambitionTransitionObservations = null;
            RequireThrows(
                () => Narrative.PrepareRestore(missingPendingCollection),
                "pending collection is missing");

            CharacterNarrativeWorldSaveData changedAfterImage = Clone(owner);
            changedAfterImage.characters.Single().ambitionProgress = 1;
            RequireThrows(
                () => Narrative.PrepareRestore(changedAfterImage),
                "frozen fact");
        }

        private static CharacterNarrativeWorldSaveData Clone(
            CharacterNarrativeWorldSaveData source) =>
            JsonUtility.FromJson<CharacterNarrativeWorldSaveData>(
                JsonUtility.ToJson(source));

        internal void ConnectObserver()
        {
            if (observerConnection != null)
                throw new InvalidOperationException(
                    "Ambition transition observer is already connected.");
            observerConnection = CreateObserverBinding().Connect(facts);
        }

        internal void ConnectDuplicateObserver()
        {
            using IDisposable duplicate = CreateObserverBinding().Connect(facts);
        }

        internal void DisconnectObserver()
        {
            observerConnection?.Dispose();
            observerConnection = null;
        }

        internal void RequireState(
            CharacterAmbitionStatus status,
            int startDay,
            int dueDay,
            int progress,
            int nextAllowedDay)
        {
            Require(
                Narrative.TryGet(characterId, out CharacterNarrativeSnapshot row),
                "Narrative character is missing.");
            Require(row.AmbitionStatus == status,
                $"Expected ambition status {status}, got {row.AmbitionStatus}.");
            Require(row.AmbitionStartedAbsoluteDay == startDay,
                "Ambition start day differs from the expected state.");
            Require(row.AmbitionDueAbsoluteDay == dueDay,
                "Ambition due day differs from the expected state.");
            Require(row.AmbitionProgress == progress,
                "Ambition progress differs from the expected state.");
            Require(row.NextAmbitionAllowedAbsoluteDay == nextAllowedDay,
                "Ambition cooldown differs from the expected state.");
            Require(
                status == CharacterAmbitionStatus.Active
                    ? row.ActiveAmbitionId.Value == ambition.StableId
                    : !row.ActiveAmbitionId.IsValid,
                "Active ambition identity differs from the expected state.");
        }

        internal void RequireOutcomeCount(int expected)
        {
            int actual = evolution.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(),
                    OutcomeFilter.All)
                .Items.Count;
            Require(actual == expected,
                $"Expected {expected} ambition outcomes, got {actual}.");
        }

        internal GameplayOutcomeSnapshot RequireTransitionOutcome(
            CharacterAmbitionStatus afterStatus,
            string reason,
            int day)
        {
            GameplayOutcomeSnapshot row = evolution.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(),
                    OutcomeFilter.All)
                .Items
                .Select(value => value.Exact)
                .Single(value => value != null
                    && value.absoluteDay == day
                    && value.outcomeTypeId
                        == CharacterAmbitionTransitionOutcomeIds
                            .Transitioned.Value);
            Require(
                row.producerId
                    == CharacterAmbitionTransitionOutcomeIds.ProducerId
                && row.ownerRevision == 1L
                && row.status == GameplayOutcomeStatus.Succeeded
                && row.facts.Count == 6
                && row.metrics.Count == 8,
                "Ambition transition outcome shape is invalid.");
            Require(
                row.facts[0].factId
                    == CharacterAmbitionTransitionOutcomeIds.AmbitionId.Value
                && row.facts[0].value == ambition.StableId
                && row.facts[1].value == ambition.DisplayName
                && row.facts[3].value == afterStatus.ToString()
                && row.facts[4].value == reason,
                "Ambition transition facts are not authoritative.");
            Require(
                row.metrics[3].value
                    == (afterStatus == CharacterAmbitionStatus.Active
                        ? day
                        : 0)
                && row.metrics[5].value
                    == (afterStatus == CharacterAmbitionStatus.Active
                        ? DueDay
                        : 0)
                && row.metrics[7].value
                    == (afterStatus == CharacterAmbitionStatus.Failed
                        ? day + 10
                        : 0),
                "Ambition transition state metrics are invalid.");
            return row;
        }

        private CharacterNarrativeRuntime CreateNarrative()
        {
            CharacterNarrativeRuntime runtime = new(
                new DungeonRuntimeAggregateRootStore(),
                catalog);
            CharacterNarrativeSnapshot registered = runtime.Register(
                characterId,
                life.Record.PhenotypeSpeciesId,
                Array.Empty<string>(),
                Array.Empty<string>());
            Require(
                runtime.TryInitializeBackground(
                    characterId,
                    absoluteDay: 1,
                    out _),
                "Narrative background did not initialize.");
            Require(registered.AmbitionStatus == CharacterAmbitionStatus.None,
                "New narrative character unexpectedly has an ambition.");
            return runtime;
        }

        private CharacterNarrativeApplicationAdapter CreateAdapter(
            CharacterNarrativeRuntime runtime) => new(
            runtime,
            runtime,
            catalog,
            life,
            events,
            Requirements,
            world,
            characterWorld,
            runtime,
            Outcomes,
            Outcomes);

        private GameplayDomainFactObserverBinding<
            CharacterAmbitionTransitionFact,
            CharacterAmbitionTransitionOutcomeReceipt> CreateObserverBinding() =>
            CharacterAmbitionTransitionFactMapping.CreateBinding(
                evolution.Registry,
                evolution.Ledger,
                evolution.Recorder,
                new GameplayDomainFactCommitGroup(
                    evolution.Recorder,
                    evolution.Ledger));

        public void Dispose()
        {
            adapter?.Dispose();
            DisconnectObserver();
            facts.Dispose();
            evolution.Dispose();
            events.Clear();
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
        }
    }

    private enum RequirementMode
    {
        Activation,
        Disabled
    }

    private sealed class FixedRequirements : IContentRequirementEvaluator
    {
        private readonly CharacterAmbitionDefinitionSO ambition;

        internal FixedRequirements(CharacterAmbitionDefinitionSO ambition) =>
            this.ambition = ambition
                ?? throw new ArgumentNullException(nameof(ambition));

        internal RequirementMode Mode { get; set; }

        public bool TryEvaluate(
            V20ContentRequirementSet requirements,
            RunMilestoneEvaluationSnapshot world,
            IReadOnlyList<string> participantCharacterIds,
            out DomainFailure failure)
        {
            bool accepted = Mode == RequirementMode.Activation
                && ReferenceEquals(
                    requirements,
                    ambition.activationRequirements);
            failure = accepted
                ? DomainFailure.None
                : new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return accepted;
        }

        public bool IsLivingCharacterAtStage(
            CharacterId characterId,
            CharacterLifeStage lifeStage) => true;
    }

    private sealed class FixedLifeQuery : ICharacterLifeQuery
    {
        internal FixedLifeQuery(CharacterLifeRecord record) =>
            Record = record ?? throw new ArgumentNullException(nameof(record));

        internal CharacterLifeRecord Record { get; }
        public int Version => 1;
        public IReadOnlyCollection<CharacterLifeRecord> Records =>
            new[] { Record };
        public bool TryGet(
            CharacterId characterId,
            out CharacterLifeRecord record)
        {
            record = Record.CharacterId.Equals(characterId) ? Record : null;
            return record != null;
        }
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery
    {
        private readonly CharacterActor[] actors;

        internal FixedCharacterWorld(params CharacterActor[] actors) =>
            this.actors = actors ?? Array.Empty<CharacterActor>();

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
    }

    private sealed class FixedWorldSnapshot : IV20MilestoneWorldSnapshotQuery
    {
        private readonly CharacterActor[] actors;

        internal FixedWorldSnapshot(params CharacterActor[] actors) =>
            this.actors = actors ?? Array.Empty<CharacterActor>();

        public IReadOnlyList<CharacterActor> LivingCharacters => actors;
        public RunMilestoneEvaluationSnapshot Build(int absoluteDay) => new()
        {
            AbsoluteDay = absoluteDay,
            EligibleCharacterCount = actors.Length
        };
    }

    private sealed class FaultingFactPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        internal FaultingFactPublisher(DomainFactDispatcher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool FailNextReserve { get; set; }
        internal bool RejectNextPublish { get; set; }
        internal bool ThrowNextPublishBeforeCommit { get; set; }
        internal bool HideNextCommittedAcknowledgement { get; set; }
        internal int PublishCalls { get; private set; }
        internal int CommittedPublishCalls { get; private set; }
        internal int ReplayBatchCalls { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure)
        {
            if (FailNextReserve)
            {
                FailNextReserve = false;
                lease = default;
                failure = "injected-ambition-reserve-failure";
                return false;
            }
            return inner.TryReserve(proposed, out lease, out failure);
        }

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            PublishCalls++;
            if (ThrowNextPublishBeforeCommit)
            {
                ThrowNextPublishBeforeCommit = false;
                throw new InvalidOperationException(
                    "injected-ambition-precommit-exception");
            }
            if (RejectNextPublish)
            {
                RejectNextPublish = false;
                return DomainFactDelivery.Reject(
                    "injected-ambition-publication-rejection");
            }
            DomainFactDelivery delivery = inner.Publish(lease, actual);
            CommittedPublishCalls++;
            if (HideNextCommittedAcknowledgement)
            {
                HideNextCommittedAcknowledgement = false;
                return DomainFactDelivery.Uncertain(
                    "injected-ambition-acknowledgement-loss-after-real-commit");
            }
            return delivery;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> emissions,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(
                emissions,
                results,
                out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> replays,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayBatchCalls++;
            return inner.ReplayBatch(replays, results, out failure);
        }
    }
}
#endif
