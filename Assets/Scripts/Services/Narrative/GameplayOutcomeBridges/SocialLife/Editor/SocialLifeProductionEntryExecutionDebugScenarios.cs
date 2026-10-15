#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class SocialLifeProductionEntryExecutionDebugScenarios
{
    private const int AbsoluteDay = 23;

    public static void RunObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        Require(Application.isBatchMode && !string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output)
            && Path.GetFullPath(output).StartsWith(qa, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(output),
            "Requires fresh project-local isolated evidence.");
        string probe = File.ReadAllText(Path.Combine(Directory.GetParent(Path.GetFullPath(output)).FullName, "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE") && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true") && probe.Contains("inJob=true"), "Missing isolation probe.");
        VerifySocialDurability();
        VerifyObserverBoundary();
        Require(RunAll(true), "Social production entries failed.");
        SocialLifeCausalProductionEntryDebugScenarios.VerifyObserverCheckpoint();
        Debug.Log("B92_SOCIAL_OBSERVER_FOCUSED=PASS");
    }

    private static void VerifySocialDurability()
    {
        using var actor = new ActorFixture("character:b92-actor", "서린", 9301);
        using var target = new ActorFixture("character:b92-target", "다온", 9302, 218);
        using var f = new SocialLifeFixture("run:b92-source", actor.Actor, target.Actor);
        var conflict = new SocialConflictEvent(CharacterPersistentIdentity.Require(actor.Actor),
            CharacterPersistentIdentity.Require(target.Actor), "betrayal-or-assault", 4f,
            CharacterCommandOrigin.Autonomous, AbsoluteDay, "social:b92-conflict");
        string before = OwnerState(f, target.Actor);
        f.Fault.RejectNext = true;
        ExpectInvalid(() => f.Events.Publish(conflict));
        Require(before == OwnerState(f, target.Actor) && f.Social.Capture().observations.Count == 0,
            "Confirmed rejection did not restore mood, identity memory and social memory.");
        f.Fault.HideNext = true;
        f.Events.Publish(conflict);
        Require(f.Social.Capture().observations.Single().pending
            && f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1, "Lost ACK did not retain actual result.");
        string applied = OwnerState(f, target.Actor);
        ExpectInvalid(() => f.Events.Publish(conflict));
        Require(applied == OwnerState(f, target.Actor), "Duplicate conflict repeated effects.");
        string json = DungeonCharacterWorldSaveCodec.SerializeFields(f.Social.Capture());
        SocialInteractionPublicationSaveSection.ValidateJson(json);
        var saved = DungeonSaveJson.Deserialize<SocialInteractionPublicationSaveData>(json);
        var canonical = f.Ledger.CaptureGameplayOutcomes();
        SocialInteractionFactPreflight.ValidateJournal(saved, canonical);
        var tampered = DungeonSaveJson.Deserialize<SocialInteractionPublicationSaveData>(json);
        tampered.observations[0].fact.targetName += "변조";
        ExpectInvalid(() => SocialInteractionFactPreflight.ValidateJournal(tampered, canonical));
        var raw = JObject.Parse(json);
        ((JObject)raw["observations"][0]["fact"]).Remove("absoluteDay");
        ExpectInvalid(() => SocialInteractionPublicationSaveSection.ValidateJson(raw.ToString()));
        using var restored = new SocialLifeFixture("run:b92-new", actor.Actor, target.Actor);
        var candidate = restored.Ledger.PrepareGameplayOutcomeRestore(canonical);
        restored.Ledger.BeginRestoreCandidate(); restored.Ledger.PublishGameplayOutcomeRestore(candidate);
        restored.Ledger.PublishRestoreCandidate(); restored.Ledger.CompleteRestoreCandidate();
        restored.States.RestoreTrustedTransactionSnapshot(f.States.Capture());
        restored.Social.Restore(saved);
        Require(restored.Social.RecoverPending(out var failure), failure);
        Require(!restored.Social.Capture().observations.Single().pending && applied == OwnerState(restored, target.Actor)
            && restored.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1, "Replay repeated gameplay or ledger result.");
        ExpectInvalid(() => restored.Events.Publish(conflict));
        var command = new CharacterApologyCommandRuntime(restored.Memories, restored.Events, restored.Calendar,
            new SocialOffenseHistory(restored.Ledger), () => "social:b92-apology");
        restored.Fault.RejectNext = true;
        ExpectInvalid(() => command.TryApologize(actor.Actor, target.Actor, true, out _));
        Require(applied == OwnerState(restored, target.Actor), "Rejected apology altered owner state.");
        restored.Fault.HideNext = true;
        Require(command.TryApologize(actor.Actor, target.Actor, true, out failure), failure);
        string forgiven = OwnerState(restored, target.Actor);
        Require(!command.TryApologize(actor.Actor, target.Actor, true, out _), "Duplicate apology accepted.");
        Require(restored.Social.RecoverPending(out failure) && forgiven == OwnerState(restored, target.Actor), failure);
        var current = restored.Social.Capture();
        SocialInteractionFactPreflight.ValidateJournal(current, restored.Ledger.CaptureGameplayOutcomes());
        current.observations.RemoveAll(row => row.fact.kind == SocialInteractionKind.Apology);
        ExpectInvalid(() => SocialInteractionFactPreflight.ValidateJournal(current, restored.Ledger.CaptureGameplayOutcomes()));
        Debug.Log("B92_SOCIAL_RECOVERY=PASS");
    }

    private static string OwnerState(SocialLifeFixture f, CharacterActor actor) =>
        Newtonsoft.Json.JsonConvert.SerializeObject(new
        { states = f.States.Capture(), mood = actor.Stats.Mood, memory = actor.SocialMemory.CaptureSnapshot() });

    private static void VerifyObserverBoundary()
    {
        using var actor = new ActorFixture("character:b92-boundary-a", "서린", 9311);
        using var target = new ActorFixture("character:b92-boundary-b", "다온", 9312);
        using var f = new SocialLifeFixture("run:b92-boundary", actor.Actor, target.Actor);
        var data = new SocialInteractionFactData
        {
            kind = SocialInteractionKind.MinionArgument, operationId = "social:b92-argument",
            actorId = actor.Actor.Identity.PersistentId, actorName = actor.Actor.Identity.DisplayName,
            targetId = target.Actor.Identity.PersistentId, targetName = target.Actor.Identity.DisplayName,
            absoluteDay = AbsoluteDay, conflictId = "minion-argument", severity = 2,
            origin = CharacterCommandOrigin.Autonomous
        };
        var proposed = new SocialConflictFact(data, true);
        using var missing = new DomainFactDispatcher();
        Require(!missing.TryReserve(proposed, out _, out _), "Missing observer accepted a fact.");
        Require(f.Facts.TryReserve(proposed, out var lease, out var failure), failure);
        var drift = data.Clone(); drift.targetName += "변경";
        Require(!f.Facts.Publish(lease, new SocialConflictFact(drift)).MayBeCommitted, "Reserved participant drift was accepted.");
        f.Facts.Cancel(lease);
        using var separate = new DomainFactDispatcher();
        var binding = SocialInteractionFactMapping.CreateConflictBinding(f.Registry, f.Ledger, f.Recorder,
            new GameplayDomainFactCommitGroup(f.Recorder, f.Ledger));
        var connection = binding.Connect(separate);
        ExpectInvalid(() => binding.Connect(separate));
        connection.Dispose();
        Require(!separate.TryReserve(proposed, out _, out _), "Disposed observer stayed connected.");
        using var reconnected = binding.Connect(separate);
        var owner = new SocialInteractionPublicationRuntime(separate, separate);
        Require(owner.TryPrepare(new SocialInteractionFact(data, true), out var prepared, out failure), failure);
        using (prepared) Require(prepared.PublishApplied().Completed, "Minion argument observation failed.");
        SocialInteractionFactPreflight.ValidateJournal(owner.Capture(), f.Ledger.CaptureGameplayOutcomes());
        using var activities = new SocialInteractionActivityObserver(f.Events, f.World);
        int a = actor.Actor.LogComponent.ActivityEntries.Count, b = target.Actor.LogComponent.ActivityEntries.Count;
        activities.Initialize(); activities.Initialize();
        f.Events.Publish(new MinionSocialConflictAppliedEvent(data.actorId, data.targetId, data.targetName, false));
        Require(actor.Actor.LogComponent.ActivityEntries.Count == a + 1 && target.Actor.LogComponent.ActivityEntries.Count == b + 1,
            "Minion activity duplicated on Initialize.");
        activities.Dispose();
        f.Events.Publish(new SocialInsultAnsweredEvent(data.targetId, data.actorName));
        Require(target.Actor.LogComponent.ActivityEntries.Count == b + 1, "Disposed activity observer emitted.");
        activities.Initialize();
        f.Events.Publish(new SocialInsultAnsweredEvent(data.targetId, data.actorName));
        Require(target.Actor.LogComponent.ActivityEntries.Count == b + 2, "Reconnected activity observer missing.");
    }

    private static void ExpectInvalid(Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { return; }
        throw new Exception("Expected explicit rejection.");
    }

    public static void RunFocused()
    {
        if (!RunAll(log: true))
        {
            throw new InvalidOperationException(
                "Social-life focused production-entry verification failed.");
        }
    }

    public static bool RunAll(bool log = false)
    {
        string stage = "conflict-production-owner";
        try
        {
            VerifyConflictProductionEntry();
            stage = "apology-production-owner";
            VerifyApologyProductionEntry();
            stage = "idle-social-reconcile";
            VerifyIdleSocialInteraction("social:reconcile", 2305);
            stage = "idle-social-encourage";
            VerifyIdleSocialInteraction("social:encourage", 2307);
            stage = "idle-social-formal-etiquette";
            VerifyIdleSocialInteraction("social:formal-etiquette", 2309);
            stage = "idle-social-rejection-rollback";
            VerifyIdleSocialRejectionRollback();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Social-life production-entry scenario failed at "
                + stage + ": " + exception);
            return false;
        }

        if (log)
            Debug.Log("[Social Life Production Entry Execution] PASS");
        return true;
    }

    private static void VerifyIdleSocialInteraction(
        string behaviorId,
        int seed)
    {
        using ActorFixture actor = new(
            "character:qa-idle-social-actor:" + seed,
            "대화 인물 " + seed,
            seed);
        using ActorFixture target = new(
            "character:qa-idle-social-target:" + seed,
            "대화 대상 " + seed,
            seed + 1);
        using SocialLifeFixture fixture = new(
            "run:idle-social:" + seed,
            actor.Actor,
            target.Actor);
        if (behaviorId == "social:reconcile")
        {
            actor.Actor.SocialMemory.RememberCharacterExperience(
                target.Actor, -1f, "strained", 0f);
            target.Actor.SocialMemory.RememberCharacterExperience(
                actor.Actor, -1f, "strained", 0f);
        }
        if (behaviorId == "social:encourage")
        {
            target.Actor.ApplyMoodFactor(
                "qa:discouraged",
                "낙담",
                -60f,
                GameCalendarRules.SecondsPerDay,
                1);
        }
        float actorRelationshipBefore = actor.Actor.SocialMemory
            .GetRelationshipSentiment(target.Actor);
        float targetRelationshipBefore = target.Actor.SocialMemory
            .GetRelationshipSentiment(actor.Actor);
        float moodBefore = target.Actor.Stats.Mood;
        IdleSocialInteractionCommandRuntime command =
            fixture.CreateIdleSocialCommand();
        actor.Actor.ConfigureIdleSocialInteractionCommand(command);
        Require(
            ShortChatIdleBehavior.TryRunInteraction(
                actor.Actor,
                target.Actor,
                behaviorId,
                out string failureReason),
            "idle social behavior failed: " + failureReason);

        GameplayOutcomeSnapshot outcome = RequirePublishedOutcome(
            fixture.Ledger,
            IdleSocialInteractionOutcomeIds.ProducerId,
            IdleSocialInteractionOutcomeIds.OutcomeType);
        Require(
            outcome.participants.Count == 2
            && outcome.subjects.Count == 2
            && outcome.metrics.Count == 8
            && Metric(outcome, IdleSocialInteractionOutcomeIds.ActorRelationshipBefore)
                == actorRelationshipBefore
            && Metric(outcome, IdleSocialInteractionOutcomeIds.ActorRelationshipAfter)
                == actor.Actor.SocialMemory.GetRelationshipSentiment(target.Actor)
            && Metric(outcome, IdleSocialInteractionOutcomeIds.TargetRelationshipBefore)
                == targetRelationshipBefore
            && Metric(outcome, IdleSocialInteractionOutcomeIds.TargetRelationshipAfter)
                == target.Actor.SocialMemory.GetRelationshipSentiment(actor.Actor)
            && Metric(outcome, IdleSocialInteractionOutcomeIds.MoodBefore)
                == moodBefore
            && Metric(outcome, IdleSocialInteractionOutcomeIds.MoodAfter)
                == target.Actor.Stats.Mood
            && outcome.metrics.Any(value => value.metricId
                == IdleSocialInteractionOutcomeIds.Behavior.Value
                && value.referenceId == behaviorId),
            "idle social outcome did not match the authoritative deltas");
    }

    private static void VerifyIdleSocialRejectionRollback()
    {
        using ActorFixture actor = new(
            "character:qa-idle-social-reject-actor",
            "거절 인물",
            2311);
        using ActorFixture target = new(
            "character:qa-idle-social-reject-target",
            "거절 대상",
            2312);
        using SocialLifeFixture fixture = new(
            "run:idle-social:reject",
            actor.Actor,
            target.Actor);
        actor.Actor.SocialMemory.RememberCharacterExperience(
            target.Actor, -1f, "strained", 0f);
        target.Actor.SocialMemory.RememberCharacterExperience(
            actor.Actor, -1f, "strained", 0f);
        string actorBefore = JsonUtility.ToJson(
            actor.Actor.SocialMemory.CaptureSnapshot());
        string targetBefore = JsonUtility.ToJson(
            target.Actor.SocialMemory.CaptureSnapshot());
        float moodBefore = target.Actor.Stats.Mood;
        var command = new IdleSocialInteractionCommandRuntime(
            new RejectIdleSocialPublisher(fixture.Facts),
            fixture.Facts,
            fixture.Roots,
            fixture.Calendar,
            () => "idle-social:reject-operation");
        Require(
            !command.TryExecute(
                actor.Actor,
                target.Actor,
                "social:reconcile",
                out _)
            && JsonUtility.ToJson(actor.Actor.SocialMemory.CaptureSnapshot())
                == actorBefore
            && JsonUtility.ToJson(target.Actor.SocialMemory.CaptureSnapshot())
                == targetBefore
            && target.Actor.Stats.Mood == moodBefore
            && fixture.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 0,
            "idle social commit rejection did not roll back exactly");
    }

    private static float Metric(
        GameplayOutcomeSnapshot outcome,
        GameplayMetricId id) => (float)outcome.metrics.Single(
            value => value.metricId == id.Value).value;

    private static void VerifyConflictProductionEntry()
    {
        using ActorFixture instigator = new(
            "character:qa-social-conflict-instigator",
            "불만 인물",
            2301);
        using ActorFixture target = new(
            "character:qa-social-conflict-target",
            "갈등 대상",
            2302);
        using SocialLifeFixture fixture = new(
            "run:social-conflict-production-entry",
            instigator.Actor,
            target.Actor);

        instigator.Actor.SocialMemory.RememberCharacterExperience(
            target.Actor,
            -1f,
            "production-entry-conflict-hostility",
            durationSeconds: 0f);
        Require(
            instigator.Actor.SocialMemory.GetRelationshipSentiment(target.Actor)
                < -0.2f,
            "conflict setup did not establish hostile relationship state");

        float targetMoodBefore = target.Actor.Mood.Value;
        int activityCountBefore = instigator.Actor.LogComponent.ActivityEntries.Count;
        var goal = new CharacterMacroGoal
        {
            type = CharacterMacroGoalType.Complain,
            reason = "production-entry-social-conflict",
            validUntil = 0f,
            source = "editor-production-entry-fixture"
        };
        instigator.Actor.Blackboard.SetMacroGoal(goal);

        CharacterAiDecisionPipeline pipeline = new(
            NoCharacterDeprivationBoundary.Instance,
            NoCharacterDeprivationBoundary.Instance,
            fixture.World,
            new CharacterIdentityEventPublisher(fixture.Events),
            fixture.Calendar);
        CharacterAiDecisionTickResult result = pipeline.RunComplainMacro(
            instigator.Actor,
            instigator.Actor.Blackboard,
            instigator.Actor.Blackboard.ActiveMacroGoal);

        Require(result.Handled, "real Complain macro path was not handled");
        Require(
            !instigator.Actor.Blackboard.HasActiveMacroGoal(),
            "real Complain macro path did not clear its macro goal");
        Require(
            instigator.Actor.LogComponent.ActivityEntries.Count
                == activityCountBefore + 1
            && instigator.Actor.LogComponent.ActivityEntries
                .Any(value => string.Equals(
                    value.ActionId,
                    "macro:complain",
                    StringComparison.Ordinal)),
            "real Complain macro path did not commit its activity state");
        Require(
            target.Actor.Mood.Value < targetMoodBefore,
            "hostile Complain path did not commit target mood loss");

        GameplayOutcomeSnapshot outcome = RequirePublishedOutcome(
            fixture.Ledger,
            SocialLifeOutcomeIds.ConflictProducerId,
            SocialLifeOutcomeIds.ConflictResolved);
        Require(
            outcome.metrics.Any(value =>
                value != null
                && value.metricId == SocialLifeOutcomeIds.MoodDeltaMetric.Value
                && value.value < 0d),
            "conflict outcome did not retain the committed mood delta");
        RequireHistoricalName(
            outcome,
            SocialLifeOutcomeIds.InstigatorRole.Value,
            instigator.Actor.Identity.DisplayName);
    }

    private static void VerifyApologyProductionEntry()
    {
        using ActorFixture offender = new(
            "character:qa-social-apology-offender",
            "사과 인물",
            2303);
        using ActorFixture recipient = new(
            "character:qa-social-apology-recipient",
            "사과 대상",
            2304,
            218);
        using SocialLifeFixture fixture = new(
            "run:social-apology-production-entry",
            offender.Actor,
            recipient.Actor);

        fixture.Events.Publish(new SocialConflictEvent(
            CharacterPersistentIdentity.Require(offender.Actor),
            CharacterPersistentIdentity.Require(recipient.Actor),
            "betrayal-or-assault",
            4f,
            CharacterCommandOrigin.Autonomous,
            fixture.Calendar.Day,
            "social-conflict:qa-apology-source"));
        Require(
            fixture.Memories.CanForgive(
                recipient.Actor,
                offender.Actor,
                restitutionProvided: true),
            "apology setup did not create a forgivable relationship memory");

        CharacterApologyCommandRuntime commands = new(
            fixture.Memories,
            fixture.Events,
            fixture.Calendar,
            new SocialOffenseHistory(fixture.Ledger));
        Require(
            commands.TryApologize(
                offender.Actor,
                recipient.Actor,
                restitutionProvided: true,
                out string failureReason),
            "real apology command failed: " + failureReason);
        Require(
            !fixture.Memories.CanForgive(
                recipient.Actor,
                offender.Actor,
                restitutionProvided: true),
            "real apology command did not clear the authoritative relationship memory");

        GameplayOutcomeSnapshot outcome = RequirePublishedOutcome(
            fixture.Ledger,
            SocialLifeOutcomeIds.ApologyProducerId,
            SocialLifeOutcomeIds.ApologyResolved);
        Require(
            outcome.metrics.Any(value =>
                value != null
                && value.metricId == SocialLifeOutcomeIds.RestitutionMetric.Value
                && value.value == 1d),
            "apology outcome did not retain the restitution decision");
        RequireHistoricalName(
            outcome,
            SocialLifeOutcomeIds.OffenderRole.Value,
            offender.Actor.Identity.DisplayName);
        Require(
            !string.IsNullOrWhiteSpace(outcome.parentRunId)
            && outcome.parentSequence > 0L
            && string.Equals(
                outcome.causationRelationId,
                "reconciles",
                StringComparison.Ordinal)
            && fixture.Ledger.TryGetExact(
                new GameplayOutcomeId(
                    new GameplayOutcomeRunId(outcome.parentRunId),
                    outcome.parentSequence),
                out GameplayOutcomeSnapshot offense)
            && string.Equals(
                offense.outcomeTypeId,
                SocialLifeOutcomeIds.ConflictResolved.Value,
                StringComparison.Ordinal),
            "apology outcome did not reverse-query its exact offending conflict");
    }

    private static GameplayOutcomeSnapshot RequirePublishedOutcome(
        GameplayOutcomeLedger ledger,
        string expectedProducerId,
        GameplayOutcomeTypeId expectedType)
    {
        GameplayOutcomeQueryPage page = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeSnapshot[] matches = page.Items
            .Where(value => value.Exact != null
                && string.Equals(
                    value.Exact.outcomeTypeId,
                    expectedType.Value,
                    StringComparison.Ordinal))
            .Select(value => value.Exact)
            .ToArray();
        Require(matches.Length == 1, $"expected one exact {expectedType.Value} outcome");

        GameplayOutcomeSnapshot outcome = matches[0];
        Require(
            string.Equals(
                outcome.producerId,
                expectedProducerId,
                StringComparison.Ordinal)
            && string.Equals(
                outcome.outcomeTypeId,
                expectedType.Value,
                StringComparison.Ordinal)
            && outcome.lifecycle
                == GameplayOutcomeRecordLifecycle.PublishedAcknowledged,
            $"{expectedType.Value} outcome identity/lifecycle was not canonical");

        GameplayResultKey resultKey = new(
            outcome.producerId,
            new GameplayOperationId(outcome.operationId),
            outcome.ownerRevision,
            outcome.localResultIndex);
        Require(
            ledger.TryGetResultIdentity(
                resultKey,
                out GameplayOutcomeReplayIdentity identity)
            && identity.State
                == GameplayOutcomeReplayState.PublishedAcknowledged
            && identity.HasCanonicalPayloadHash,
            $"{expectedType.Value} replay identity was not PublishedAcknowledged");
        return outcome;
    }

    private static void RequireHistoricalName(
        GameplayOutcomeSnapshot outcome,
        string roleId,
        string expectedDisplay)
    {
        GameplayOutcomeParticipantSnapshot participant = outcome?.participants?
            .SingleOrDefault(value => value != null
                && string.Equals(value.roleId, roleId, StringComparison.Ordinal));
        Require(
            participant != null
            && string.Equals(
                participant.displayText,
                expectedDisplay,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(participant.displayRevision)
            && participant.displayRevision.StartsWith(
                "social-life-character-v1:",
                StringComparison.Ordinal)
            && !participant.displayRevision.Contains(
                expectedDisplay,
                StringComparison.Ordinal),
            "social-life outcome did not freeze the actor's actual display "
            + "name behind a canonical immutable revision");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class SocialLifeFixture : IDisposable
    {
        private readonly SocialIdentityEventAdapter adapter;
        private readonly IDisposable idleObservation, conflictObservation, apologyObservation;
        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public SocialInteractionPublicationRuntime Social { get; }
        public CharacterIdentityStateStore States { get; }
        public FaultSocialPublisher Fault { get; }

        public SocialLifeFixture(
            string runId,
            params CharacterActor[] actors)
        {
            Events = new GameEventBus();
            World = new FixedCharacterWorld(actors);
            Calendar = new FixedCalendar(AbsoluteDay);

            IKoreanJosaFormatter josa = new KoreanJosaFormatter();
            GameplayOutcomeRegistry registry = Registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new SocialConflictOutcomeDescriptor(josa),
                    new ApologyOutcomeDescriptor(josa),
                    new IdleSocialInteractionOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new SocialConflictOutcomeAdapter(),
                    new ApologyOutcomeAdapter(),
                    new IdleSocialInteractionOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8,
                    largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId(runId),
                1L);
            GameplayOutcomeRecorder recorder = Recorder = new(Ledger, registry, Events);
            Roots = new DungeonRuntimeAggregateRootStore();
            Facts = new DomainFactDispatcher();
            var group = new GameplayDomainFactCommitGroup(recorder, Ledger);
            conflictObservation = SocialInteractionFactMapping.CreateConflictBinding(registry, Ledger, recorder, group).Connect(Facts);
            apologyObservation = SocialInteractionFactMapping.CreateApologyBinding(registry, Ledger, recorder, group).Connect(Facts);
            Fault = new FaultSocialPublisher(Facts);
            Social = new SocialInteractionPublicationRuntime(Fault, Facts);
            idleObservation = Facts.Subscribe(
                "idle-social-interaction:fixture",
                new GameplayDomainFactObserver<
                    IdleSocialInteractionResolvedFact,
                    IdleSocialInteractionOutcomeReceipt>(
                    IdleSocialInteractionFactMapping.Capture,
                    registry,
                    Ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, Ledger),
                    IdleSocialInteractionFactMapping.ReserveShape));

            CharacterIdentityStateStore states = States = new();
            CharacterIdentityRuleRouter router = new();
            Memories = new CharacterRelationshipMemoryService(router, states);
            CharacterMoodPolicyService moods = new(
                router,
                new CharacterPersistentNeedRuntime(
                    states,
                    Calendar));
            adapter = new SocialIdentityEventAdapter(
                Events,
                World,
                moods,
                Memories,
                states,
                Social,
                EmptyNarrativeQuery.Instance,
                EmptyBuildingWorld.Instance,
                new RoomFacilityPolicyService(new RoomLayoutCache()));
            adapter.Start();
        }

        public GameEventBus Events { get; }
        public FixedCharacterWorld World { get; }
        public FixedCalendar Calendar { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public CharacterRelationshipMemoryService Memories { get; }
        public DomainFactDispatcher Facts { get; }
        public DungeonRuntimeAggregateRootStore Roots { get; }

        public IdleSocialInteractionCommandRuntime CreateIdleSocialCommand() =>
            new(
                Facts,
                Facts,
                Roots,
                Calendar,
                () => "idle-social:test:" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            adapter.Dispose();
            idleObservation.Dispose();
            conflictObservation.Dispose(); apologyObservation.Dispose();
            Facts.Dispose();
        }
    }

    private sealed class FaultSocialPublisher : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;
        public bool RejectNext, HideNext;
        public FaultSocialPublisher(IDomainFactPublisher inner) => this.inner = inner;
        public bool TryReserve<T>(in T proposed, out DomainFactLease lease, out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T actual)
        {
            if (RejectNext) { RejectNext = false; inner.Cancel(lease); return DomainFactDelivery.Reject("qa:social-rejected"); }
            var result = inner.Publish(lease, actual);
            if (HideNext) { HideNext = false; Require(result.Completed, "ACK fault requires a real commit."); return DomainFactDelivery.Uncertain("qa:social-lost-ack"); }
            return result;
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class RejectIdleSocialPublisher : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;

        public RejectIdleSocialPublisher(IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failureReason) => inner.TryReserve(
            proposed,
            out lease,
            out failureReason);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            inner.Cancel(lease);
            return DomainFactDelivery.Reject(
                "injected-idle-social-rejection");
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failureReason) => inner.PublishBatch(
            facts,
            results,
            out failureReason);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly GameObject actorObject;
        private readonly CharacterSO data;

        public ActorFixture(
            string persistentId,
            string displayName,
            int seed,
            params int[] traitIds)
        {
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                displayName,
                "human");
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            actorObject = new GameObject("SocialLifeProductionEntry_" + seed);
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.Identity.SetPersistentId(persistentId);
            Actor.Initialization(data);
            Actor.Progression.ApplyPreparedIdentity(
                displayName,
                "social-life-production-entry-fixture",
                traitIds ?? Array.Empty<int>(),
                CharacterPotentialGrade.Ordinary,
                seed,
                autoChooseDrafts: false,
                maximumTraitCount: 4,
                ensureInitialDrafts: false);
            Require(
                string.Equals(
                    Actor.Identity.DisplayName,
                    displayName,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(Actor.Identity.DisplayName),
                "social-life actor display-name fixture did not expose the "
                + "authored runtime identity");
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            Actor.Stats.Stats[CharacterCondition.MOOD] = 70f;
        }

        public CharacterActor Actor { get; }

        public void Dispose()
        {
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            if (data != null)
                UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery, ICharacterLifetimeQuery
    {
        private readonly CharacterActor[] actors;

        public FixedCharacterWorld(IEnumerable<CharacterActor> actors) =>
            this.actors = (actors ?? Array.Empty<CharacterActor>())
                .Where(value => value != null)
                .ToArray();

        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters => actors;
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        public static EmptyBuildingWorld Instance { get; } = new();

        private EmptyBuildingWorld()
        {
        }

        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    private sealed class EmptyNarrativeQuery : ICharacterNarrativeQuery
    {
        public static EmptyNarrativeQuery Instance { get; } = new();

        private EmptyNarrativeQuery()
        {
        }

        public int Version => 0;
        public IReadOnlyCollection<CharacterNarrativeSnapshot> All =>
            Array.Empty<CharacterNarrativeSnapshot>();

        public bool TryGet(
            CharacterId characterId,
            out CharacterNarrativeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }

        public bool CanPerformPractice(
            CharacterId characterId,
            string practiceId,
            int absoluteDay,
            out int nextAllowedAbsoluteDay)
        {
            nextAllowedAbsoluteDay = 0;
            return false;
        }

        public bool TryPreviewAmbitionProgress(
            CharacterId characterId,
            int amount,
            out AmbitionProgressPreview preview)
        {
            preview = default;
            return false;
        }
    }

    private sealed class FixedGameClock : IGameClock
    {
        public FixedGameClock(int absoluteDay) =>
            Time = Math.Max(0, absoluteDay) * GameCalendarRules.SecondsPerDay;

        public float DeltaTime => 0.02f;
        public float Time { get; }
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        public FixedCalendar(int day)
        {
            Day = Math.Max(1, day);
        }

        public int Day { get; private set; }
        public int Hour { get; private set; }
        public int Year => 1;
        public int DayOfYear => Day;
        public Season Season => default;
        public int DayOfSeason => Day;
        public long AbsoluteHour => (long)Day * 24L + Hour;
        public float ElapsedSeconds =>
            Day * GameCalendarRules.SecondsPerDay
            + Hour * (GameCalendarRules.SecondsPerDay / 24f);
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);

        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;

        public void SetDateTime(int day, int hour)
        {
            Day = Math.Max(1, day);
            Hour = Math.Clamp(hour, 0, 23);
        }
    }
}
#endif
