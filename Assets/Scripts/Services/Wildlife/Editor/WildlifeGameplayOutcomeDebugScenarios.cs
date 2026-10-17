#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static class WildlifeGameplayOutcomeDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/e11-wildlife-gameplay-outcome-focused.txt";

    [MenuItem("DungeonStory/Debug/Wildlife/Run E11 Gameplay Outcome Focused")]
    public static void RunFocused()
    {
        Directory.CreateDirectory("Artifacts/QA");
        var checks = new List<string>();
        try
        {
            RunChecks(checks);
            File.WriteAllLines(ReportPath, new[]
            {
                "E11_WILDLIFE_GAMEPLAY_OUTCOME_FOCUSED",
                $"checks={checks.Count}; failures=0",
                "case\tresult\tdetail"
            }.Concat(checks).Concat(new[] { "RESULT=PASS; failures=0" }));
            Debug.Log("E11_WILDLIFE_GAMEPLAY_OUTCOME_FOCUSED=PASS");
        }
        catch (Exception exception)
        {
            File.WriteAllLines(ReportPath, new[]
            {
                "E11_WILDLIFE_GAMEPLAY_OUTCOME_FOCUSED",
                $"checks={checks.Count}; failures=1",
                "case\tresult\tdetail"
            }.Concat(checks).Concat(new[]
            {
                "RESULT=FAIL; failures=1",
                exception.ToString()
            }));
            throw;
        }
    }

    public static bool RunRegression()
    {
        var checks = new List<string>();
        RunChecks(checks);
        return checks.Count == 4;
    }

    public static void RunObserverFocusedForBatchMode()
    {
        Require(
            RunRegression(),
            "Wildlife fact observer regression did not complete all cases.");
        VerifyInDoubtBeforeCommitRecovery();
        VerifyInDoubtAfterCommitRecovery();
        Debug.Log("B75_WILDLIFE_FACT_OBSERVER_FOCUSED=PASS");
    }

    private static void RunChecks(ICollection<string> checks)
    {
        VerifyReservationFailureDoesNotAdvance(checks);
        VerifyCommandExactlyOnce(checks);
        VerifyActionWriteFailureRoundTripAndRetry(checks);
        VerifyCircusCaptureWriteFailureRoundTripAndRetry(checks);
    }

    private static void VerifyInDoubtBeforeCommitRecovery()
    {
        using Fixture fixture = new("run:e11-wildlife-before-commit-uncertain");
        WildlifePopulationState state = new();
        var runtime = new WildlifeGameplayOutcomeRuntime(
            state,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        WildlifeGameplayOutcomeSaveData command = CreateCommand(
            runtime,
            "wild:9110");
        Require(runtime.TryReserve(
                command,
                out DomainFactLease lease,
                out string reserveFailure),
            "Before-commit uncertainty reservation failed: "
                + reserveFailure);
        string publicationKey = lease.PublicationKey;
        runtime.RecordMutation(command);
        fixture.Faulting.InDoubtBeforeNextPublish = true;
        DomainFactDelivery uncertain = runtime.CommitReserved(
            command,
            lease,
            out _);
        Require(uncertain.Phase == DomainFactDeliveryPhase.InDoubt
                && state.GameplayOutcomes.Count == 1
                && fixture.Count(
                    EnvironmentOutcomeIds.WildlifeCommandProducer) == 0,
            "Before-commit uncertainty did not retain one unpublished owner row.");

        DungeonWildlifeSaveData restoredSave = RoundTrip(state);
        WildlifeFactPreflight.Validate(
            restoredSave,
            new CircusSaveData(),
            fixture.Ledger.CaptureGameplayOutcomes());
        WildlifePopulationState restored = RestoreOwner(restoredSave);
        Require(restored.NextGameplayOutcomeSequence == 1
                && restored.GameplayOutcomes.Single().publicationKey
                    == publicationKey,
            "Before-commit uncertainty JSON restore changed owner sequence or identity.");
        var recovery = new WildlifeGameplayOutcomeRuntime(
            restored,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        recovery.AdvancePending();
        recovery.AdvancePending();
        Require(restored.NextGameplayOutcomeSequence == 1
                && restored.GameplayOutcomes.Count == 0
                && fixture.Count(
                    EnvironmentOutcomeIds.WildlifeCommandProducer) == 1,
            "Before-commit uncertainty recovery did not converge exactly once.");
    }

    private static void VerifyInDoubtAfterCommitRecovery()
    {
        using Fixture fixture = new("run:e11-wildlife-after-commit-uncertain");
        WildlifePopulationState state = new();
        var runtime = new WildlifeGameplayOutcomeRuntime(
            state,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        WildlifeGameplayOutcomeSaveData action = CreateAction(
            runtime,
            "wild:9111");
        Require(runtime.TryReserve(
                action,
                out DomainFactLease lease,
                out string reserveFailure),
            "After-commit uncertainty reservation failed: "
                + reserveFailure);
        string publicationKey = lease.PublicationKey;
        runtime.RecordMutation(action);
        fixture.Faulting.InDoubtAfterNextPublish = true;
        DomainFactDelivery uncertain = runtime.CommitReserved(
            action,
            lease,
            out _);
        Require(uncertain.Phase == DomainFactDeliveryPhase.InDoubt
                && state.GameplayOutcomes.Count == 1
                && fixture.Count(
                    EnvironmentOutcomeIds.WildlifeActionProducer) == 1,
            "After-commit uncertainty did not retain one canonically published row.");

        DungeonWildlifeSaveData restoredSave = RoundTrip(state);
        WildlifeFactPreflight.Validate(
            restoredSave,
            new CircusSaveData(),
            fixture.Ledger.CaptureGameplayOutcomes());
        WildlifePopulationState restored = RestoreOwner(restoredSave);
        Require(restored.NextGameplayOutcomeSequence == 1
                && restored.GameplayOutcomes.Single().publicationKey
                    == publicationKey,
            "After-commit uncertainty JSON restore changed owner sequence or identity.");
        var recovery = new WildlifeGameplayOutcomeRuntime(
            restored,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        recovery.AdvancePending();
        recovery.AdvancePending();
        Require(restored.NextGameplayOutcomeSequence == 1
                && restored.GameplayOutcomes.Count == 0
                && fixture.Count(
                    EnvironmentOutcomeIds.WildlifeActionProducer) == 1,
            "After-commit uncertainty replay duplicated or lost its canonical outcome.");
    }

    private static DungeonWildlifeSaveData RoundTrip(
        WildlifePopulationState state) =>
        JsonUtility.FromJson<DungeonWildlifeSaveData>(
            JsonUtility.ToJson(new DungeonWildlifeSaveData
            {
                nextGameplayOutcomeSequence =
                    state.NextGameplayOutcomeSequence,
                gameplayOutcomes = state.GameplayOutcomes
                    .Select(WildlifeGameplayOutcomeRuntime.Clone)
                    .ToList()
            }));

    private static WildlifePopulationState RestoreOwner(
        DungeonWildlifeSaveData save)
    {
        var state = new WildlifePopulationState
        {
            NextGameplayOutcomeSequence =
                save.nextGameplayOutcomeSequence
        };
        state.GameplayOutcomes.AddRange(
            save.gameplayOutcomes.Select(
                WildlifeGameplayOutcomeRuntime.Clone));
        return state;
    }

    private static void VerifyReservationFailureDoesNotAdvance(
        ICollection<string> checks)
    {
        using Fixture fixture = new("run:e11-wildlife-reserve-failure");
        WildlifePopulationState state = new();
        var runtime = new WildlifeGameplayOutcomeRuntime(
            state,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        WildlifeGameplayOutcomeSaveData draft = CreateCommand(runtime, "wild:9101");
        fixture.Faulting.RejectNextReserve = true;
        Require(!runtime.TryReserve(draft, out _, out string failure),
            "Forced reservation rejection unexpectedly succeeded.");
        Require(state.NextGameplayOutcomeSequence == 0
                && state.GameplayOutcomes.Count == 0
                && fixture.Count(EnvironmentOutcomeIds.WildlifeCommandProducer) == 0,
            "Reservation rejection mutated owner state or published an outcome.");
        checks.Add("RESERVE_FAILURE_NO_MUTATION\tPASS\t" + failure);
    }

    private static void VerifyCommandExactlyOnce(ICollection<string> checks)
    {
        using Fixture fixture = new("run:e11-wildlife-command-success");
        WildlifePopulationState state = new();
        var runtime = new WildlifeGameplayOutcomeRuntime(
            state,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        WildlifeGameplayOutcomeSaveData command = CreateCommand(runtime, "wild:9102");
        Require(runtime.TryReserve(
                command,
                out DomainFactLease reserved,
                out string reserveFailure),
            "Command reservation failed: " + reserveFailure);
        string publicationKey = reserved.PublicationKey;
        GameplayOutcomeLedgerDiagnostics reservationState = fixture.Ledger.GetDiagnostics();
        Require(publicationKey.Length > 0
                && command.publicationKey == publicationKey
                && reservationState.ReservedCount == 1
                && reservationState.PendingDeliveryCount == 0
                && reservationState.PublishedCount == 0,
            "Command reservation did not freeze an opaque unpublished identity.");
        runtime.RecordMutation(command);
        DomainFactDelivery commit = runtime.CommitReserved(
            command,
            reserved,
            out string commitFailure);
        Require(commit.Completed
                && commit.OperationKey == publicationKey
                && state.GameplayOutcomes.Count == 0
                && state.NextGameplayOutcomeSequence == 1
                && fixture.Count(EnvironmentOutcomeIds.WildlifeCommandProducer) == 1
                && fixture.IsAcknowledged(
                    EnvironmentOutcomeIds.WildlifeCommandProducer),
            "Command did not commit and clear exactly once: " + commitFailure);
        runtime.AdvancePending();
        Require(fixture.Count(EnvironmentOutcomeIds.WildlifeCommandProducer) == 1,
            "Command replay duplicated the exact outcome.");
        checks.Add("COMMAND_EXACTLY_ONCE\tPASS\trevision=1; exact=1");
    }

    private static void VerifyActionWriteFailureRoundTripAndRetry(
        ICollection<string> checks)
    {
        using Fixture fixture = new("run:e11-wildlife-action-retry");
        WildlifePopulationState state = new();
        var runtime = new WildlifeGameplayOutcomeRuntime(
            state,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        WildlifeGameplayOutcomeSaveData action = CreateAction(runtime, "wild:9103");
        action.hit = false;
        action.healthAfter = action.healthBefore;
        action.damage = 0;
        Require(runtime.TryReserve(
                action,
                out DomainFactLease reserved,
                out string reserveFailure),
            "Action reservation failed: " + reserveFailure);
        string publicationKey = reserved.PublicationKey;
        action.hit = true;
        action.healthAfter = 4;
        action.damage = 6;
        runtime.RecordMutation(action);
        fixture.Faulting.RejectNextWrite = true;
        DomainFactDelivery rejected = runtime.CommitReserved(
            action,
            reserved,
            out string writeFailure);
        Require(!rejected.MayBeCommitted
                && state.GameplayOutcomes.Count == 1
                && state.GameplayOutcomes[0].publicationKey == publicationKey
                && fixture.Count(EnvironmentOutcomeIds.WildlifeActionProducer) == 0,
            "Write rejection did not preserve one owner pending row.");

        DungeonWildlifeSaveData save = new()
        {
            nextGameplayOutcomeSequence = state.NextGameplayOutcomeSequence,
            gameplayOutcomes = state.GameplayOutcomes
                .Select(WildlifeGameplayOutcomeRuntime.Clone)
                .ToList()
        };
        DungeonWildlifeSaveData restored = JsonUtility.FromJson<DungeonWildlifeSaveData>(
            JsonUtility.ToJson(save));
        WildlifePopulationState restoredState = new()
        {
            NextGameplayOutcomeSequence = restored.nextGameplayOutcomeSequence
        };
        restoredState.GameplayOutcomes.AddRange(
            restored.gameplayOutcomes.Select(WildlifeGameplayOutcomeRuntime.Clone));
        Require(restoredState.GameplayOutcomes.Single().publicationKey
                == publicationKey,
            "Action JSON restore changed the opaque publication identity.");
        var restoredRuntime = new WildlifeGameplayOutcomeRuntime(
            restoredState,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        restoredRuntime.AdvancePending();
        restoredRuntime.AdvancePending();
        Require(restoredState.GameplayOutcomes.Count == 0
                && fixture.Count(EnvironmentOutcomeIds.WildlifeActionProducer) == 1
                && fixture.IsAcknowledged(
                    EnvironmentOutcomeIds.WildlifeActionProducer),
            "Restored action pending row was lost or duplicated: " + writeFailure);
        checks.Add("ACTION_SAVE_RESTORE_RETRY\tPASS\twrite-fault; exact=1");
    }

    private static WildlifeGameplayOutcomeSaveData CreateCommand(
        WildlifeGameplayOutcomeRuntime runtime,
        string wildlifeId)
    {
        long revision = runtime.NextOwnerRevision;
        var value = new WildlifeGameplayOutcomeSaveData
        {
            profile = WildlifeGameplayOutcomeProfile.Command,
            eventKind = "hunt-designation",
            wildlifeId = wildlifeId,
            wildlifeSpeciesId = WildlifeTestFixtures.CaveRat.SpeciesId,
            wildlifeDisplayName = WildlifeTestFixtures.CaveRat.DisplayName,
            stateBefore = (int)WildlifeState.Idle,
            stateAfter = (int)WildlifeState.Hunted,
            flagAfter = true,
            priorityAfter = true,
            reasonCode = "priority-hunt-designated",
            absoluteDay = runtime.AbsoluteDay,
            ownerRevision = revision
        };
        value.operationId = runtime.CreateOperationId(
            value.profile,
            value.eventKind,
            value.wildlifeId,
            revision);
        return value;
    }

    private static void VerifyCircusCaptureWriteFailureRoundTripAndRetry(
        ICollection<string> checks)
    {
        using Fixture fixture = new("run:e11-circus-capture-retry");
        TestOutcomeOwner owner = new();
        var runtime = new WildlifeGameplayOutcomeRuntime(
            owner,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        long revision = runtime.NextOwnerRevision;
        var action = new WildlifeGameplayOutcomeSaveData
        {
            profile = WildlifeGameplayOutcomeProfile.Action,
            eventKind = "capture-penned",
            wildlifeId = "wild:9104",
            wildlifeSpeciesId = WildlifeTestFixtures.CaveRat.SpeciesId,
            wildlifeDisplayName = WildlifeTestFixtures.CaveRat.DisplayName,
            actorId = "character:e11-carrier",
            actorDisplayName = "검수 운반자",
            facilityId = "building:e11-beast-pen",
            stateBefore = (int)CapturedWildlifeTransportState.Transporting,
            stateAfter = (int)CapturedWildlifeTransportState.Penned,
            reasonCode = "capture-transport-completed",
            absoluteDay = runtime.AbsoluteDay,
            ownerRevision = revision
        };
        action.operationId = runtime.CreateOperationId(
            action.profile,
            action.eventKind,
            action.wildlifeId,
            revision);
        Require(runtime.TryReserve(
                action,
                out DomainFactLease reserved,
                out string reserveFailure),
            "Circus capture reservation failed: " + reserveFailure);
        string publicationKey = reserved.PublicationKey;
        runtime.RecordMutation(action);
        fixture.Faulting.RejectNextWrite = true;
        DomainFactDelivery rejected = runtime.CommitReserved(
            action,
            reserved,
            out string writeFailure);
        Require(!rejected.MayBeCommitted
                && owner.GameplayOutcomes.Count == 1
                && owner.GameplayOutcomes[0].publicationKey == publicationKey,
            "Circus capture write rejection did not retain its pending row.");

        var save = new CircusSaveData
        {
            nextWildlifeGameplayOutcomeSequence =
                owner.NextGameplayOutcomeSequence,
            wildlifeGameplayOutcomes = owner.GameplayOutcomes
                .Select(WildlifeGameplayOutcomeRuntime.Clone)
                .ToList()
        };
        CircusSaveData restored = JsonUtility.FromJson<CircusSaveData>(
            JsonUtility.ToJson(save));
        TestOutcomeOwner restoredOwner = new()
        {
            NextGameplayOutcomeSequence =
                restored.nextWildlifeGameplayOutcomeSequence
        };
        restoredOwner.GameplayOutcomes.AddRange(
            restored.wildlifeGameplayOutcomes.Select(
                WildlifeGameplayOutcomeRuntime.Clone));
        Require(restoredOwner.GameplayOutcomes.Single().publicationKey
                == publicationKey,
            "Circus JSON restore changed the opaque publication identity.");
        var restoredRuntime = new WildlifeGameplayOutcomeRuntime(
            restoredOwner,
            fixture.Faulting,
            fixture.Faulting,
            EditorFixedGameCalendar.Instance);
        restoredRuntime.AdvancePending();
        restoredRuntime.AdvancePending();
        Require(restoredOwner.GameplayOutcomes.Count == 0
                && fixture.Count(
                    EnvironmentOutcomeIds.WildlifeActionProducer) == 1
                && fixture.IsAcknowledged(
                    EnvironmentOutcomeIds.WildlifeActionProducer),
            "Circus capture pending row was lost or duplicated after retry: "
                + writeFailure);
        checks.Add(
            "CIRCUS_CAPTURE_SAVE_RESTORE_RETRY\tPASS\twrite-fault; exact=1");
    }

    private static WildlifeGameplayOutcomeSaveData CreateAction(
        WildlifeGameplayOutcomeRuntime runtime,
        string wildlifeId)
    {
        long revision = runtime.NextOwnerRevision;
        var value = new WildlifeGameplayOutcomeSaveData
        {
            profile = WildlifeGameplayOutcomeProfile.Action,
            eventKind = "hunt-attack",
            wildlifeId = wildlifeId,
            wildlifeSpeciesId = WildlifeTestFixtures.CaveRat.SpeciesId,
            wildlifeDisplayName = WildlifeTestFixtures.CaveRat.DisplayName,
            actorId = "character:e11-hunter",
            actorDisplayName = "검수 사냥꾼",
            stateBefore = (int)WildlifeState.Hunted,
            stateAfter = (int)WildlifeState.Fleeing,
            healthBefore = 10,
            healthAfter = 4,
            damage = 6,
            hit = true,
            weaponDefinitionId = "weapon:e11-test",
            weaponInstanceId = "weapon-instance:e11-test",
            ammunitionItemId = "ammo:e11-test",
            ammunitionBefore = 3,
            ammunitionConsumed = 1,
            ammunitionAfter = 2,
            bodyPart = CombatBodyPart.Torso.ToString(),
            reasonCode = "hunt-hit",
            absoluteDay = runtime.AbsoluteDay,
            ownerRevision = revision
        };
        value.operationId = runtime.CreateOperationId(
            value.profile,
            value.eventKind,
            value.wildlifeId,
            revision);
        return value;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DomainFactDispatcher facts;
        private readonly IDisposable commandBinding;
        private readonly IDisposable actionBinding;

        internal Fixture(string runId)
        {
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new WildlifeCommandOutcomeDescriptor(new KoreanJosaFormatter()),
                    new WildlifeActionOutcomeDescriptor(new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new WildlifeCommandOutcomeAdapter(),
                    new WildlifeActionOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                registry,
                new GameEventBus());
            facts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            commandBinding = WildlifeFactMapping.CreateCommandBinding(
                registry,
                Ledger,
                Recorder,
                commits).Connect(facts);
            actionBinding = WildlifeFactMapping.CreateActionBinding(
                registry,
                Ledger,
                Recorder,
                commits).Connect(facts);
            Faulting = new FaultingPublisher(facts);
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal FaultingPublisher Faulting { get; }

        internal int Count(string producer) => Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .Count(value => value?.producerId == producer);

        internal bool IsAcknowledged(string producer) => Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .Any(value => value?.producerId == producer
                && value.lifecycle
                    == GameplayOutcomeRecordLifecycle.PublishedAcknowledged);

        public void Dispose()
        {
            actionBinding.Dispose();
            commandBinding.Dispose();
            facts.Dispose();
        }
    }

    private sealed class TestOutcomeOwner : IWildlifeGameplayOutcomeOwner
    {
        public List<WildlifeGameplayOutcomeSaveData> GameplayOutcomes
            { get; } = new();
        public int NextGameplayOutcomeSequence { get; set; }
    }

    private sealed class FaultingPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        internal FaultingPublisher(DomainFactDispatcher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool RejectNextReserve { get; set; }
        internal bool RejectNextWrite { get; set; }
        internal bool InDoubtBeforeNextPublish { get; set; }
        internal bool InDoubtAfterNextPublish { get; set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failureReason)
        {
            if (RejectNextReserve)
            {
                RejectNextReserve = false;
                lease = default;
                failureReason = "qa-e11-reserve-rejected";
                return false;
            }
            return inner.TryReserve(
                proposed,
                out lease,
                out failureReason);
        }

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (InDoubtBeforeNextPublish)
            {
                InDoubtBeforeNextPublish = false;
                string operationKey = lease.PublicationKey;
                inner.Cancel(lease);
                return new DomainFactDelivery(
                    DomainFactDeliveryPhase.InDoubt,
                    operationKey,
                    string.Empty,
                    string.Empty,
                    "qa-e11-before-commit-response-uncertain");
            }
            if (RejectNextWrite)
            {
                RejectNextWrite = false;
                inner.Cancel(lease);
                return DomainFactDelivery.Reject(
                    "qa-e11-write-rejected");
            }
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (!InDoubtAfterNextPublish)
                return delivered;
            InDoubtAfterNextPublish = false;
            return new DomainFactDelivery(
                DomainFactDeliveryPhase.InDoubt,
                delivered.OperationKey,
                delivered.PublicationId,
                delivered.Fingerprint,
                "qa-e11-after-commit-response-uncertain");
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(
                facts,
                results,
                out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) =>
            inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.ReplayBatch(
                facts,
                results,
                out failure);
    }
}
#endif
