#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class WorkCompletionIdentityCausalProductionEntryDebugScenarios
{
    // Shares the actual identity owner with focused crop producer tests. The
    // supplied crop ledger contains the real terminal child, not a synthetic one.
    internal static WorkIdentityEventAdapter CreateCropCompletionCommand(CharacterActor actor,
        GameplayOutcomeLedger ledger, IGameplayOutcomeRecorder recorder, GameEventBus bus,
        WorkCompletionIdentityDeliveryLedger deliveries, out CharacterIdentityStateStore states)
    {
        states = new CharacterIdentityStateStore();
        var world = new FixedWorld(actor);
        var needs = new CharacterPersistentNeedRuntime(states, new EditorFixedGameCalendar(23));
        var moods = new CharacterMoodPolicyService(new CharacterIdentityRuleRouter(), needs);
        var observations = new WorkIdentityAppliedFactEditorFixture(
            new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new WorkCompletionIdentityOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new WorkCompletionIdentityOutcomeAdapter()
                }),
            ledger,
            recorder);
        var command = new WorkIdentityEventAdapter(bus, world, moods,
            new CharacterDirectOrderCostPreviewService(new CharacterIdentityRuleRouter(), moods),
            states, new StableEnvironment(), needs, deliveries, world,
            observations.Facts,
            observations.Facts,
            observations.DeliveryQuery);
        command.Start();
        return command;
    }

    internal static JObject CaptureCropCompletionStats(CharacterActor actor) => CaptureStats(actor);
    // Controlled domain fact, real observer/identity effects/ledger/cursor. The
    // husbandry physical owner is exercised separately by its focused scenarios.
    public static string VerifyHusbandryCompletionFactFocused()
    {
        TraceHusbandryCompletion("begin");
        string key;
        JObject saved;
        using (var context = new HusbandryCompletionContext())
        {
            var fact = new AnimalHusbandryCompletionFact(context.Pending);
            Require(context.Facts.TryReserve(fact, out var cancelled, out _), "Completion reserve failed.");
            context.Facts.Cancel(cancelled);
            Require(context.Facts.Publish(cancelled, fact).Phase == DomainFactDeliveryPhase.Rejected
                && context.Deliveries.Capture().Count == 0, "Cancelled completion changed identity.");
            Require(context.Facts.TryReserve(fact, out var drift, out _), "Drift reserve failed.");
            var changed = context.Pending.Clone();
            changed.CompletionContributionWorkUnits = 1f;
            Require(context.Facts.Publish(drift, new AnimalHusbandryCompletionFact(changed)).Phase
                == DomainFactDeliveryPhase.Rejected && context.Deliveries.Capture().Count == 0,
                "Reserved completion accepted changed labor.");
            var wrongParent = context.Pending.Clone();
            wrongParent.OperationId = AnimalHusbandryDomainOperationIdentity.Format(
                wrongParent.Kind, wrongParent.AnimalId.Value, 2L);
            bool parentRejected = false;
            try { _ = AnimalHusbandryCompletionObservation.CreateRequest(new(wrongParent)); }
            catch (ArgumentException) { parentRejected = true; }
            Require(parentRejected, "Completion accepted another operation's parent key.");

            Require(context.Facts.TryReserve(fact, out var lease, out _), "Completion reserve failed.");
            key = lease.PublicationKey;
            context.Pending.CompletionPublicationKey = key;
            context.Command.Defer = true;
            Require(context.Facts.Publish(lease, fact).Phase == DomainFactDeliveryPhase.InDoubt
                && context.Deliveries.Capture().Count == 0 && context.EventsObserved == 0,
                "Deferred completion changed identity or notification.");
            context.Command.Defer = false;
            context.Command.HideAppliedAck = true;
            TraceHusbandryCompletion("before-hidden-ack");
            DomainFactDelivery hiddenAck = context.Replay(key);
            TraceHusbandryCompletion("after-hidden-ack:" + hiddenAck.Phase + ":" + hiddenAck.Detail);
            Require(hiddenAck.Phase == DomainFactDeliveryPhase.InDoubt
                && context.Deliveries.Capture().Single().disposition
                    == WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome
                && !context.Deliveries.Capture().Single().notificationAttempted
                && context.EventsObserved == 0
                && context.Actor.Actor.Mood.Factors.Any(value => value.Kind == CharacterMoodFactorKind.Interaction)
                && context.IdentityStates.Capture().Sum(value => value.rules.Count) > 0,
                "Hidden ACK did not retain actual effects, cursor and unattempted notification. phase="
                    + hiddenAck.Phase + ";detail=" + hiddenAck.Detail
                    + ";cursorCount=" + context.Deliveries.Capture().Count + ";eventCount=" + context.EventsObserved);
            saved = JObject.Parse(context.Capture().ToString(Formatting.None));
            TraceHusbandryCompletion("checkpoint-created");
        }
        using (var restored = new HusbandryCompletionContext(saved))
        {
            TraceHusbandryCompletion("hidden-ack-restored");
            JObject effectsBefore = restored.Capture();
            Require(restored.Replay(key + "-wrong").Phase == DomainFactDeliveryPhase.Rejected
                && JToken.DeepEquals(effectsBefore, restored.Capture()), "Wrong completion recovery key mutated state.");
            Require(restored.Replay(key).Phase == DomainFactDeliveryPhase.Delivered
                && restored.EventsObserved == 1 && restored.Deliveries.Capture().Single().notificationAttempted,
                "Fresh owner did not finalize the original completion.");
            JObject after = restored.Capture();
            Require(JToken.DeepEquals(effectsBefore["identity"], after["identity"])
                && JToken.DeepEquals(effectsBefore["stats"], after["stats"])
                && JToken.DeepEquals(effectsBefore["ledger"], after["ledger"]),
                "Recovered completion repeated identity effects or ledger outcomes.");
            Require(restored.Replay(key).Phase == DomainFactDeliveryPhase.Delivered
                && restored.EventsObserved == 1 && JToken.DeepEquals(after, restored.Capture()),
                "Same-key completion duplicated notification/effects.");
            saved = JObject.Parse(after.ToString(Formatting.None));
        }
        using (var restored = new HusbandryCompletionContext(saved))
        {
            TraceHusbandryCompletion("notification-restored");
            JObject before = restored.Capture();
            Require(restored.Replay(key).Phase == DomainFactDeliveryPhase.Delivered
                && restored.EventsObserved == 0 && JToken.DeepEquals(before, restored.Capture()),
                "Saved notification-attempt marker did not prevent a duplicate event.");
        }
        return "husbandry-completion/typed-observer/real-identity-effects-ledger/cancel-drift-parent-reject=PASS\n"
            + "husbandry-completion/deferred-hidden-ack/json-fresh-composition/no-effect-or-event-duplicate=PASS\n"
            + "husbandry-completion/input-fact=CONTROLLED;identity-effects-and-save-authorities=PRODUCTION;natural-ai=NOT-RUN";
    }

    private static void TraceHusbandryCompletion(string phase) => Console.WriteLine(
        "HUSBANDRY_COMPLETION " + phase + ";managedBytes=" + GC.GetTotalMemory(false)
        + ";workingSet=" + Environment.WorkingSet);

    private sealed class HusbandryCompletionContext : IDisposable
    {
        internal readonly ActorFixture Actor = new("character:qa-husbandry-observer", "목동", 8271);
        internal readonly CharacterIdentityStateStore IdentityStates = new();
        internal readonly WorkCompletionIdentityDeliveryLedger Deliveries = new();
        internal readonly DomainFactDispatcher Facts = new();
        internal readonly AnimalHusbandryPendingOutcomeState Pending;
        internal readonly HeldCompletion Command;
        internal int EventsObserved;
        private readonly GameplayOutcomeLedger ledger;
        private readonly WorkIdentityEventAdapter adapter;
        private readonly WorkIdentityAppliedFactEditorFixture observations;
        private readonly IDisposable eventSubscription;

        internal HusbandryCompletionContext(JObject saved = null)
        {
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new WorkCompletionIdentityOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new WorkCompletionIdentityOutcomeAdapter(), new MigratedProducerOutcomeAdapter() },
                new IGameplayOutcomeDescriptorCatalog[] { new MigratedProducerOutcomeDescriptorCatalog(new KoreanJosaFormatter()) });
            ledger = new GameplayOutcomeLedger(registry,
                new GameplayOutcomeBufferLimits(smallPageCount: 8, largePageCount: 1, knownResultKeyCapacity: 64),
                new GameplayOutcomeRunId("run:husbandry-completion-observer"), 1L);
            var eventBus = new GameEventBus();
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var world = new FixedWorld(Actor.Actor);
            var needs = new CharacterPersistentNeedRuntime(IdentityStates, new EditorFixedGameCalendar(10));
            var moods = new CharacterMoodPolicyService(new CharacterIdentityRuleRouter(), needs);
            observations = new WorkIdentityAppliedFactEditorFixture(
                registry,
                ledger,
                recorder);
            adapter = new WorkIdentityEventAdapter(eventBus, world, moods,
                new CharacterDirectOrderCostPreviewService(new CharacterIdentityRuleRouter(), moods),
                IdentityStates, new StableEnvironment(), needs, Deliveries, world,
                observations.Facts,
                observations.Facts,
                observations.DeliveryQuery);
            adapter.Start();
            Command = new HeldCompletion(adapter);
            eventSubscription = eventBus.Subscribe<WorkCompletedIdentityEvent>(_ => EventsObserved++);
            Facts.Subscribe("test:husbandry-completion", new WorkCompletionDomainFactObserver<AnimalHusbandryCompletionFact>(
                AnimalHusbandryCompletionObservation.CreateRequest, Command, Deliveries, new CharacterIdentityEventPublisher(eventBus),
                WorkCompletionNotificationMode.ReferencedEvent,
                observations.Mapping));
            var transaction = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            Facts.Subscribe("test:husbandry-work", new MigratedDomainFactObserver<AnimalHusbandryWorkFact>(
                AnimalHusbandryWorkFactMapping.Capture, transaction, new GameplayDomainFactCommitGroup(recorder, ledger)));
            if (saved != null)
            {
                ledger.BeginRestoreCandidate();
                ledger.PublishGameplayOutcomeRestore(ledger.PrepareGameplayOutcomeRestore(saved["ledger"].ToObject<GameplayOutcomeLedgerSaveData>()));
                ledger.PublishRestoreCandidate();
                ledger.CompleteRestoreCandidate();
                IdentityStates.RestoreTrustedTransactionSnapshot(saved["identity"].ToObject<List<CharacterIdentityRuntimeStateSaveData>>());
                Deliveries.Restore(saved["cursors"].ToObject<List<WorkCompletionIdentityDeliveryCursorSaveData>>());
                RestoreStats(Actor.Actor, saved["stats"]);
                Pending = AnimalHusbandryStateCodec.BuildRestore(
                    saved["husbandry"].ToObject<DungeonAnimalHusbandrySaveData>(), 0f,
                    id => id.Value == "species:qa-goat" ? Array.Empty<ItemDefinitionId>() : null,
                    _ => false).State.PendingOutcomes.Single();
                return;
            }
            var before = new HusbandryAnimalState
            {
                AnimalId = new("wild:completion-observer"), PenId = new("building:completion-observer"),
                SpeciesId = new("species:qa-goat"), Sex = AnimalSex.Female, AgeDays = 20f,
                Products = new(), StatusParameters = new(), PendingWorkContributions = new()
            };
            var after = before.Clone();
            after.Tamed = true;
            after.TamingProgress = 1f;
            Pending = new AnimalHusbandryPendingOutcomeState
            {
                OperationId = AnimalHusbandryDomainOperationIdentity.Format(AnimalHusbandryOutcomeOperationKind.Tame, before.AnimalId.Value, 1L),
                Kind = AnimalHusbandryOutcomeOperationKind.Tame, AbsoluteDay = 10,
                AnimalId = before.AnimalId, PenId = before.PenId, SpeciesId = before.SpeciesId,
                WorkerId = Actor.Actor.Identity.PersistentId, WorkerDisplayName = "목동", CompletedWorkMilliUnits = 12000,
                AnimalStatesBefore = new() { before }, AnimalStatesAfter = new() { after },
                Contributions = new() { new() { characterId = Actor.Actor.Identity.PersistentId, contributedWork = 12f, relevantSkill = 1f } },
                CompletionIdentityRequired = true, CompletionContributionWorkUnits = 12f,
                CompletionSkillModifierProvenance = "none", OutcomeCommitted = true
            };
            var work = new AnimalHusbandryWorkFact(Pending);
            Require(Facts.TryReserve(work, out var lease, out _), "Fixture work reserve failed.");
            Pending.PublicationKey = lease.PublicationKey;
            Require(Facts.Publish(lease, work).Phase == DomainFactDeliveryPhase.Delivered, "Fixture work commit failed.");
        }

        internal DomainFactDelivery Replay(string key)
        {
            var results = new DomainFactDelivery[1];
            return Facts.ReplayBatch(new[] { DomainFactReplay.Create(key, new AnimalHusbandryCompletionFact(Pending)) },
                results, out string failure) ? results[0] : DomainFactDelivery.Reject(failure);
        }
        internal JObject Capture()
        {
            TraceHusbandryCompletion("capture-start");
            var state = new AnimalHusbandryAggregateState { NextOutcomeSequence = 2L };
            state.Policies.Add(Pending.PenId, new AnimalPenPolicyData { PenId = Pending.PenId });
            state.Animals.Add(Pending.AnimalId, Pending.AnimalStatesAfter[0].Clone());
            state.PendingOutcomes.Add(Pending.Clone());
            var ledgerJson = JObject.Parse(JsonUtility.ToJson(ledger.CaptureGameplayOutcomes()));
            TraceHusbandryCompletion("capture-ledger");
            var identityJson = JObject.Parse(JsonUtility.ToJson(new CharacterNarrativeWorldSaveData
            { identityStates = IdentityStates.Capture().ToList(), workCompletionDeliveries = Deliveries.Capture().ToList() }));
            TraceHusbandryCompletion("capture-identity");
            var statsJson = CaptureStats(Actor.Actor);
            TraceHusbandryCompletion("capture-stats");
            var husbandryJson = JObject.Parse(JsonUtility.ToJson(AnimalHusbandryStateCodec.Capture(state)));
            TraceHusbandryCompletion("capture-husbandry");
            return new JObject
            {
                ["ledger"] = ledgerJson, ["identity"] = identityJson["identityStates"],
                ["cursors"] = identityJson["workCompletionDeliveries"], ["stats"] = statsJson,
                ["husbandry"] = husbandryJson
            };
        }
        public void Dispose()
        {
            adapter.Dispose();
            Facts.Dispose();
            observations.Dispose();
            eventSubscription.Dispose();
            Actor.Dispose();
        }
    }

    private sealed class HeldCompletion : IWorkCompletionIdentityDeliveryCommand
    {
        private readonly IWorkCompletionIdentityDeliveryCommand actual;
        internal bool Defer, HideAppliedAck;
        internal HeldCompletion(IWorkCompletionIdentityDeliveryCommand actual) => this.actual = actual;
        public WorkCompletionIdentityDeliveryResult EnsureApplied(WorkCompletionIdentityDeliveryRequest request)
        {
            if (Defer) return new(WorkCompletionIdentityDeliveryStatus.Deferred, "test-only-deferral");
            var result = actual.EnsureApplied(request);
            return HideAppliedAck && result.IsApplied
                ? new(WorkCompletionIdentityDeliveryStatus.Deferred, "test-only-hidden-ack") : result;
        }
        public bool RetireProducerStream(string stream) => actual.RetireProducerStream(stream);
    }

    internal const string CausalOwnerRestoreKind =
        "owner-family:work-completion-identity";
    internal const string FirstHarvestActionId =
        "action:character-work-identity-first-harvest-completed";
    internal const string NextDayHarvestActionId =
        "action:character-work-identity-next-day-harvest-completed";

    private const string OwnerKind = "owner-kind:work-completion-identity";
    private const string DomainStateSchema =
        "dungeonstory-causal-work-completion-identity-owner-state@1";
    private const string DisplayName = "수확 담당 연화";
    private const int FirstDay = 12;
    private const int SecondDay = 13;

    public static void RunBatch()
    {
        if (!RunCausalOwnerFamily(log: true))
        {
            throw new InvalidOperationException(
                "Focused causal work-completion identity scenario returned false.");
        }
        Debug.Log("W01_CAUSAL_WORK_COMPLETION_IDENTITY=PASS");
    }

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
                "A work-identity checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                FirstHarvestActionId,
                "character",
                WorkCompletionIdentityOutcomeIds.Applied,
                context.CaptureState,
                context.ApplyFirstHarvest,
                context.RequireFirstOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                NextDayHarvestActionId,
                "character",
                WorkCompletionIdentityOutcomeIds.Applied,
                () => new Dictionary<string, object>(),
                context.ApplyNextDayHarvest,
                context.RequireSecondOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string ownerId = "character-owner:causal-work-identity:focused";
        using (var uncommitted = new Context(
                   "run:causal-work-identity:uncommitted", ownerId, null))
            uncommitted.VerifyUncommittedChildRejected();
        JObject firstCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-work-identity:first",
                   ownerId)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstHarvestActionId);
            IReadOnlyDictionary<string, object> before = first.CaptureState();
            Require(
                (int)before["workIdentity.stage"] == 0
                && (int)before["workIdentity.deliveryCursorCount"] == 0
                && (int)before["workIdentity.identityRuleStateCount"] == 0
                && (int)before["workIdentity.exactOutcomeCount"] == 0,
                "Work-identity owner did not start empty.");
            Require(first.Execute(),
                "The first production work-identity delivery was rejected.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first work-identity outcome has no canonical hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["workIdentity.stage"] == 1
                && (int)after["workIdentity.lastAppliedDay"] == FirstDay
                && (int)after["workIdentity.deliveryOperationSequence"] == 0
                && (int)after["workIdentity.identityRuleStateCount"] == 2
                && (bool)after["workIdentity.firstProcessRecorded"]
                && (bool)after["workIdentity.firstApplied"]
                && !(bool)after["workIdentity.secondApplied"]
                && (int)after["workIdentity.exactOutcomeCount"] == 1
                && (int)after["workIdentity.cropChildCount"] == 1
                && (int)after["workIdentity.totalExactOutcomeCount"] == 3,
                "The first harvest did not retain its identity reaction state.");
            firstCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-work-identity:second",
                   ownerId,
                   firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstHarvestActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                NextDayHarvestActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            Require(
                (int)restored["workIdentity.stage"] == 1
                && (int)restored["workIdentity.lastAppliedDay"] == FirstDay
                && (bool)restored["workIdentity.firstProcessRecorded"]
                && !first.Execute(),
                "Fresh composition restore lost or replayed the first harvest.");
            Require(second.Execute(),
                "The next-day production work-identity delivery was rejected.");
            Require(second.RequireExactOutcome().HasCanonicalPayloadHash,
                "The next-day work-identity outcome has no canonical hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["workIdentity.stage"] == 2
                && (int)after["workIdentity.lastAppliedDay"] == SecondDay
                && (int)after["workIdentity.deliveryOperationSequence"] == 1
                && (int)after["workIdentity.identityRuleStateCount"] == 2
                && (bool)after["workIdentity.firstProcessRecorded"]
                && (bool)after["workIdentity.firstApplied"]
                && (bool)after["workIdentity.secondApplied"]
                && (int)after["workIdentity.exactOutcomeCount"] == 2
                && (int)after["workIdentity.cropChildCount"] == 2
                && (int)after["workIdentity.totalExactOutcomeCount"] == 6,
                "The next-day harvest did not preserve the restored identity owner.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)second)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-work-identity:terminal",
                   ownerId,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstHarvestActionId);
            IProductionCausalActionFixture second = fixtures.Get(
                NextDayHarvestActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            Require(!first.Execute() && !second.Execute(),
                "Terminal work-identity owner was not idempotent.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected work-identity replay mutated the owner checkpoint.");
        }

        JObject characterTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)characterTamper["domainState"])["characterId"] =
            "character:causal-work-identity:tampered";
        Require(RejectCheckpoint(ownerId, characterTamper, "character-tamper"),
            "A work-identity checkpoint with a changed character was accepted.");

        JObject cursorTamper = (JObject)firstCheckpoint.DeepClone();
        JObject narrative = (JObject)
            ((JObject)cursorTamper["domainState"])["narrative"];
        ((JObject)((JArray)narrative["workCompletionDeliveries"])[0])[
            "operationSequence"] = 1;
        Require(RejectCheckpoint(ownerId, cursorTamper, "cursor-tamper"),
            "A non-contiguous work-identity cursor was accepted.");

        JObject missingChild = (JObject)firstCheckpoint.DeepClone();
        ((JArray)missingChild["outcomeLedger"]["exactOutcomes"])
            .Single(value => (string)value["producerId"] == EnvironmentOutcomeIds.CropPlanProducer)
            .Remove();
        Require(RejectCheckpoint(ownerId, missingChild, "missing-crop-child"),
            "An identity owner with its committed crop child removed was accepted.");

        if (log)
        {
            Debug.Log(
                "Work identity first harvest -> restored next-day harvest causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Work-completion identity causal owner family returned false.");
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
                "run:causal-work-identity:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed partial class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly string characterId;
        private readonly string producerStreamId;
        private readonly string firstDeliveryId;
        private readonly string secondDeliveryId;
        private readonly ActorFixture actorFixture;
        private readonly CharacterIdentityStateStore identityStates;
        private readonly WorkCompletionIdentityDeliveryLedger deliveries;
        private readonly EditorFixedGameCalendar calendar;
        private readonly GameplayOutcomeLedger ledger;
        private readonly GameplayOutcomeRegistry registry;
        private readonly WorkIdentityEventAdapter adapter;
        private readonly WorkIdentityAppliedFactEditorFixture observations;
        private bool disposed;

        internal Context(
            string causalRunId,
            string ownerId,
            JObject checkpoint,
            Func<IDomainFactStagingPublisher, IDomainFactStagingPublisher> wrapStaging = null)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence
                .Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            characterId = "character:causal-work-identity:" + scope;
            producerStreamId = "crop-harvest:causal-work-identity:" + scope;
            firstDeliveryId = "identity-event:causal-work-identity:"
                + scope + ":000000";
            secondDeliveryId = "identity-event:causal-work-identity:"
                + scope + ":000001";
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":work-identity-ledger";

            actorFixture = new ActorFixture(characterId, DisplayName, 8217);
            identityStates = new CharacterIdentityStateStore();
            deliveries = new WorkCompletionIdentityDeliveryLedger();
            // Delivery requests retain the day authority across checkpoint restore.
            calendar = new EditorFixedGameCalendar(FirstDay);
            var persistentNeeds = new CharacterPersistentNeedRuntime(
                identityStates,
                calendar);
            var moodPolicy = new CharacterMoodPolicyService(
                new CharacterIdentityRuleRouter(),
                persistentNeeds);
            var directOrders = new CharacterDirectOrderCostPreviewService(
                new CharacterIdentityRuleRouter(),
                moodPolicy);
            var world = new FixedWorld(actorFixture.Actor);
            var environment = new StableEnvironment();
            registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new WorkCompletionIdentityOutcomeDescriptor(
                        new KoreanJosaFormatter()),
                    new CropPlanOutcomeDescriptor(new KoreanJosaFormatter()),
                    new CropIrrigationSupplyOutcomeDescriptor(new KoreanJosaFormatter()),
                    new CropSelectionCommandOutcomeDescriptor(new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new WorkCompletionIdentityOutcomeAdapter(),
                    new CropPlanGameplayOutcomeAdapter(),
                    new CropIrrigationSupplyOutcomeAdapter(),
                    new CropSelectionCommandOutcomeAdapter()
                });
            ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8,
                    largePageCount: 1,
                    knownResultKeyCapacity: 64),
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            var outcomeEvents = new GameEventBus();
            var recorder = new GameplayOutcomeRecorder(
                ledger,
                registry,
                outcomeEvents);
            observations = new WorkIdentityAppliedFactEditorFixture(
                registry,
                ledger,
                recorder);
            adapter = new WorkIdentityEventAdapter(
                new GameEventBus(),
                world,
                moodPolicy,
                directOrders,
                identityStates,
                environment,
                persistentNeeds,
                deliveries,
                world,
                wrapStaging == null ? observations.Facts : wrapStaging(observations.Facts),
                observations.Facts,
                observations.DeliveryQuery);

            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? OwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            WorkHistoryPayload smallSuccess = ReadHistory(
                "character-trait:217",
                "mood:small-success");
            WorkHistoryPayload firstProcess = ReadHistory(
                "character-trait:230",
                "mood:first-process-success");
            WorkCompletionIdentityDeliveryCursorSaveData cursor = deliveries
                .Capture()
                .SingleOrDefault();
            CharacterMoodSnapshot mood = actorFixture.Actor.Mood;
            return new Dictionary<string, object>
            {
                ["workIdentity.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "character-owner:ephemeral-work-identity",
                ["workIdentity.characterId"] = characterId,
                ["workIdentity.stage"] = Stage,
                ["workIdentity.lastAppliedDay"] =
                    smallSuccess?.lastSmallSuccessDay ?? -1,
                ["workIdentity.deliveryCursorCount"] =
                    deliveries.Capture().Count,
                ["workIdentity.deliveryOperationSequence"] =
                    cursor?.operationSequence ?? -1,
                ["workIdentity.identityRuleStateCount"] = identityStates
                    .Capture()
                    .Sum(value => value.rules?.Count ?? 0),
                ["workIdentity.firstProcessRecorded"] = firstProcess != null
                    && firstProcess.completedProcessIds.Contains(
                        BuiltInWorkTypeIds.Harvest.Value,
                        StringComparer.Ordinal),
                ["workIdentity.moodValue"] = mood.Value,
                ["workIdentity.moodFactorCount"] = mood.Factors.Count,
                ["workIdentity.firstApplied"] = Stage >= 1,
                ["workIdentity.secondApplied"] = Stage >= 2,
                ["workIdentity.lastProductId"] = Stage == 0 ? "none" : CreateRequest(Stage - 1).ProductId,
                ["workIdentity.exactOutcomeCount"] = ExactOutcomeCount(),
                ["workIdentity.cropChildCount"] = ExactTypeCount(EnvironmentOutcomeIds.CropPlanTerminal),
                ["workIdentity.totalExactOutcomeCount"] = ledger.CaptureGameplayOutcomes().exactOutcomes.Count
            };
        }

        internal bool ApplyFirstHarvest() => Apply(
            expectedStage: 0,
            index: 0);

        internal bool ApplyNextDayHarvest() => Apply(
            expectedStage: 1,
            index: 1);

        internal void VerifyUncommittedChildRejected()
        {
            JObject before = CaptureOwnerCheckpoint();
            var request = new WorkCompletionIdentityDeliveryRequest(
                firstDeliveryId, producerStreamId, 0, actorFixture.Actor.Identity.TypedPersistentId,
                BuiltInWorkTypeIds.Harvest.Value, "crop:uncommitted-fixture",
                CharacterCommandOrigin.Autonomous, FirstDay, ChildKey(0).OperationId.Value,
                EnvironmentOutcomeIds.CropPlanProducer, ChildKey(0).ToString(), PlotId(0), 1f);
            WorkCompletionIdentityDeliveryResult result = adapter.EnsureApplied(request);
            Require(result.Status == WorkCompletionIdentityDeliveryStatus.Conflict
                    && result.FailureReason.StartsWith("work-identity-child-not-committed:", StringComparison.Ordinal)
                    && JToken.DeepEquals(before, CaptureOwnerCheckpoint()),
                "A canonical but uncommitted crop child changed the identity owner.");
        }

        internal GameplayOutcomeReplayIdentity RequireFirstOutcome() =>
            RequireOutcome(CreateRequest(0), expectedTypeCount: Stage);

        internal GameplayOutcomeReplayIdentity RequireSecondOutcome() =>
            RequireOutcome(CreateRequest(1), expectedTypeCount: Stage);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral work-identity fixtures have no checkpoint.");
            }
            ValidateState();
            ValidateObservationSave(new CharacterNarrativeWorldSaveData
            {
                identityStates = identityStates.Capture().ToList(),
                workCompletionDeliveries = deliveries.Capture().ToList()
            }, ledger.CaptureGameplayOutcomes());
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["characterId"] = characterId,
                    ["producerStreamId"] = producerStreamId,
                    ["firstDeliveryId"] = firstDeliveryId,
                    ["secondDeliveryId"] = secondDeliveryId,
                    ["stage"] = Stage,
                    ["narrative"] = JObject.FromObject(
                        new CharacterNarrativeWorldSaveData
                        {
                            identityStates = identityStates.Capture().ToList(),
                            workCompletionDeliveries = deliveries.Capture().ToList()
                        }),
                    ["actorStats"] = CaptureStats(actorFixture.Actor)
                },
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            adapter.Dispose();
            observations.Dispose();
            actorFixture.Dispose();
        }

        private int Stage
        {
            get
            {
                IReadOnlyList<WorkCompletionIdentityDeliveryCursorSaveData>
                    captured = deliveries.Capture();
                if (captured.Count == 0)
                    return 0;
                if (captured.Count != 1
                    || !string.Equals(
                        captured[0].producerStreamId,
                        producerStreamId,
                        StringComparison.Ordinal))
                {
                    return -1;
                }
                return captured[0].operationSequence + 1;
            }
        }

        private WorkCompletionIdentityDeliveryRequest CreateRequest(int index)
        {
            if (index is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(index));
            GameplayOutcomeSnapshot child = RequireCropChild(index);
            string contribution = child.facts.Single(value =>
                value.factId == EnvironmentOutcomeIds.HarvestContributorsFact.Value).value;
            string prefix = characterId + "=";
            Require(contribution.StartsWith(prefix, StringComparison.Ordinal),
                "The committed crop child names a different contribution owner.");
            Require(float.TryParse(contribution.Substring(prefix.Length),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out float workUnits)
                    && float.IsFinite(workUnits) && workUnits > 0f,
                "The committed crop child lost its single worker's approved contribution.");
            // Derive the frozen reaction payload from the authoritative child,
            // including after restore. There is no second writable witness DTO.
            return new WorkCompletionIdentityDeliveryRequest(
                index == 0 ? firstDeliveryId : secondDeliveryId,
                producerStreamId,
                index,
                actorFixture.Actor.Identity.TypedPersistentId,
                BuiltInWorkTypeIds.Harvest.Value,
                child.participants.Single(value =>
                    value.entityKindId == EnvironmentOutcomeIds.CropDefinitionKind.Value).entityId,
                CharacterCommandOrigin.Autonomous,
                index == 0 ? FirstDay : SecondDay,
                ChildKey(index).OperationId.Value,
                EnvironmentOutcomeIds.CropPlanProducer,
                ChildKey(index).ToString(),
                PlotId(index),
                workUnits);
        }

        private string PlotId(int index) => "building:" + producerStreamId + ":" + index;

        private GameplayResultKey ChildKey(int index) => new(
            EnvironmentOutcomeIds.CropPlanProducer,
            new GameplayOperationId(producerStreamId + ":cycle:" + index), 1L, 0);

        private GameplayOutcomeSnapshot RequireCropChild(int index)
        {
            Require(ledger.TryGetResultIdentity(ChildKey(index), out GameplayOutcomeReplayIdentity identity)
                    && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                    && identity.HasCanonicalPayloadHash,
                "The identity owner is missing its canonical committed crop child.");
            Require(ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot child)
                    && child.outcomeTypeId == EnvironmentOutcomeIds.CropPlanTerminal.Value
                    && child.status == GameplayOutcomeStatus.Succeeded
                    && child.absoluteDay == (index == 0 ? FirstDay : SecondDay)
                    && child.participants.Any(value =>
                        value.entityKindId == EnvironmentOutcomeIds.FacilityKind.Value
                        && value.entityId == PlotId(index))
                    && child.facts.Any(value => value.factId == EnvironmentOutcomeIds.HarvesterFact.Value
                        && value.value == characterId),
                "The committed crop child disagrees with its worker, plot or frozen date.");
            return child;
        }

        private bool Apply(
            int expectedStage,
            int index)
        {
            ThrowIfDisposed();
            if (Stage != expectedStage)
                return false;
            if (!ledger.TryGetResultIdentity(ChildKey(index), out _))
                EnvironmentCropProductionEntryDebugScenarios.RunHarvestForWorkIdentity(
                    registry, ledger, ChildKey(index).OperationId.Value, PlotId(index),
                    actorFixture.Actor, index == 0 ? FirstDay : SecondDay);
            WorkCompletionIdentityDeliveryRequest request = CreateRequest(index);
            calendar.SetDateTime(request.AbsoluteDay, 0);
            WorkCompletionIdentityDeliveryResult result =
                adapter.EnsureApplied(request);
            bool applied = result.Status
                    == WorkCompletionIdentityDeliveryStatus.Applied
                && Stage == expectedStage + 1;
            if (!applied)
            {
                throw new InvalidOperationException(
                    "Work-identity delivery did not apply: status="
                    + result.Status
                    + "; failure=" + result.FailureReason
                    + "; stage=" + Stage
                    + "; expectedStage=" + (expectedStage + 1));
            }
            GameplayOutcomeReplayIdentity exact = applied
                ? RequireOutcome(
                    request,
                    expectedTypeCount: expectedStage + 1,
                    throwOnFailure: true)
                : default;
            bool valid = applied && exact.HasCanonicalPayloadHash;
            if (valid)
                ValidateState();
            return valid;
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            WorkCompletionIdentityDeliveryRequest request,
            int expectedTypeCount,
            bool throwOnFailure = true)
        {
            GameplayResultKey key = WorkCompletionIdentityOutcomeIds.ResultKey(
                request);
            GameplayOutcomeReplayIdentity identity = default;
            bool valid = OutcomeCount() == expectedTypeCount
                && ledger.TryGetResultIdentity(key, out identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && ValidateExactOutcome(exact, request);
            if (!valid && throwOnFailure)
            {
                GameplayOutcomeSnapshot snapshot = null;
                if (identity.OutcomeId.IsValid)
                    ledger.TryGetExact(identity.OutcomeId, out snapshot);
                throw new InvalidOperationException(
                    "Causal work-identity production entry did not publish its exact outcome: "
                    + request.DeliveryId
                    + "; outcomeCount=" + OutcomeCount()
                    + "; expected=" + expectedTypeCount
                    + "; identityState=" + identity.State
                    + "; hasHash=" + (identity.CanonicalPayloadHash?.Length == 64)
                    + "; snapshot=" + (snapshot == null
                        ? "missing"
                        : JsonUtility.ToJson(snapshot)));
            }
            return valid ? identity : default;
        }

        private bool ValidateExactOutcome(
            GameplayOutcomeSnapshot outcome,
            WorkCompletionIdentityDeliveryRequest request)
        {
            if (outcome == null
                || !string.Equals(
                    outcome.producerId,
                    WorkCompletionIdentityOutcomeIds.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    outcome.operationId,
                    request.DeliveryId,
                    StringComparison.Ordinal)
                || outcome.commitRevision != request.OperationSequence + 1L
                || outcome.localResultIndex != 0
                || !string.Equals(
                    outcome.outcomeTypeId,
                    WorkCompletionIdentityOutcomeIds.Applied.Value,
                    StringComparison.Ordinal)
                || outcome.absoluteDay != request.AbsoluteDay
                || outcome.status != GameplayOutcomeStatus.Succeeded
                || outcome.ownerRevision != request.OperationSequence + 1L
                || outcome.participants?.Count != 1
                || outcome.subjects?.Count != 2
                || outcome.metrics?.Count != 2
                || outcome.tags?.Count != 1
                || outcome.provenance?.Count != 2
                || outcome.facts?.Count != 9)
            {
                return false;
            }

            GameplayOutcomeParticipantSnapshot participant =
                outcome.participants[0];
            GameplayOutcomeSubjectSnapshot subject = outcome.subjects.Single(
                value => string.Equals(
                    value.entityKindId,
                    WorkCompletionIdentityOutcomeIds.CharacterKind.Value,
                    StringComparison.Ordinal));
            GameplayOutcomeSubjectSnapshot target = outcome.subjects.Single(
                value => string.Equals(
                    value.entityKindId,
                    WorkCompletionIdentityOutcomeIds.BuildingKind.Value,
                    StringComparison.Ordinal));
            GameplayOutcomeMetricSnapshot metric = outcome.metrics[0];
            GameplayOutcomeMetricSnapshot contribution = outcome.metrics[1];
            Dictionary<string, string> facts = outcome.facts.ToDictionary(
                value => value.factId,
                value => value.value,
                StringComparer.Ordinal);
            Dictionary<string, string> provenance = outcome.provenance
                .ToDictionary(
                    value => value.kindId,
                    value => value.value,
                    StringComparer.Ordinal);
            return string.Equals(
                    participant.entityKindId,
                    WorkCompletionIdentityOutcomeIds.CharacterKind.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    participant.entityId,
                    characterId,
                    StringComparison.Ordinal)
                && string.Equals(
                    participant.roleId,
                    WorkCompletionIdentityOutcomeIds.WorkerRole.Value,
                    StringComparison.Ordinal)
                && string.Equals(subject.entityId, characterId,
                    StringComparison.Ordinal)
                && string.Equals(
                    target.entityKindId,
                    WorkCompletionIdentityOutcomeIds.BuildingKind.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    target.entityId,
                    request.TargetEntityId,
                    StringComparison.Ordinal)
                && string.Equals(
                    metric.metricId,
                    WorkCompletionIdentityOutcomeIds.OwnerRevisionMetric.Value,
                    StringComparison.Ordinal)
                && Math.Abs(metric.value - (request.OperationSequence + 1d))
                    < 0.00001d
                && string.Equals(
                    contribution.metricId,
                    WorkCompletionIdentityOutcomeIds.ContributionMetric.Value,
                    StringComparison.Ordinal)
                && Math.Abs(
                    contribution.value - request.ContributionWorkUnits)
                    < 0.00001d
                && string.Equals(
                    outcome.tags[0],
                    WorkCompletionIdentityOutcomeIds.WorkIdentityTag.Value,
                    StringComparison.Ordinal)
                && facts.TryGetValue(
                    WorkCompletionIdentityOutcomeIds.WorkIdFact.Value,
                    out string workId)
                && string.Equals(workId, request.WorkId,
                    StringComparison.Ordinal)
                && facts.TryGetValue(
                    WorkCompletionIdentityOutcomeIds.ProductIdFact.Value,
                    out string productId)
                && string.Equals(productId, request.ProductId,
                    StringComparison.Ordinal)
                && facts.TryGetValue(
                    WorkCompletionIdentityOutcomeIds.OriginFact.Value,
                    out string origin)
                && string.Equals(origin, "autonomous",
                    StringComparison.Ordinal)
                && facts.TryGetValue(
                    WorkCompletionIdentityOutcomeIds.ReactionFact.Value,
                    out string reaction)
                && string.Equals(reaction, "completed-work",
                    StringComparison.Ordinal)
                && facts.TryGetValue(
                    WorkCompletionIdentityOutcomeIds.ProducerStreamFact.Value,
                    out string stream)
                && string.Equals(stream, producerStreamId,
                    StringComparison.Ordinal)
                && facts.TryGetValue(
                    WorkCompletionIdentityOutcomeIds.OperationIdFact.Value,
                    out string operationId)
                && string.Equals(
                    operationId,
                    request.OperationId,
                    StringComparison.Ordinal)
                && facts.TryGetValue(
                    WorkCompletionIdentityOutcomeIds.TerminalOwnerFact.Value,
                    out string terminalOwner)
                && string.Equals(
                    terminalOwner,
                    request.TerminalOwnerId,
                    StringComparison.Ordinal)
                && facts.TryGetValue(
                    WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value,
                    out string childResultKey)
                && string.Equals(
                    childResultKey,
                    request.ChildResultKey,
                    StringComparison.Ordinal)
                && facts.TryGetValue(
                    WorkCompletionIdentityOutcomeIds
                        .SkillModifierProvenanceFact.Value,
                    out string skillModifierProvenance)
                && string.Equals(
                    skillModifierProvenance,
                    request.SkillModifierProvenance,
                    StringComparison.Ordinal)
                && provenance.TryGetValue(
                    "identity-delivery",
                    out string delivery)
                && string.Equals(delivery, request.DeliveryId,
                    StringComparison.Ordinal)
                && provenance.TryGetValue(
                    "identity-payload-fingerprint",
                    out string fingerprint)
                && string.Equals(
                    fingerprint,
                    request.PayloadFingerprint,
                    StringComparison.Ordinal);
        }

        private int ExactOutcomeCount() => ExactTypeCount(WorkCompletionIdentityOutcomeIds.Applied);

        private int ExactTypeCount(GameplayOutcomeTypeId type) =>
            ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                value != null && value.outcomeTypeId == type.Value);

        private int OutcomeCount() =>
            ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                value != null
                && string.Equals(
                    value.outcomeTypeId,
                    WorkCompletionIdentityOutcomeIds.Applied.Value,
                    StringComparison.Ordinal));

        private WorkHistoryPayload ReadHistory(
            string traitDefinitionId,
            string ruleId)
        {
            if (!identityStates.TryGet(
                    characterId,
                    traitDefinitionId,
                    ruleId,
                    out CharacterIdentityRuleStateSaveData state))
            {
                return null;
            }
            if (state.revision != 1
                || string.IsNullOrWhiteSpace(state.statePayload))
            {
                throw new InvalidOperationException(
                    "Work-identity rule state is invalid: "
                    + traitDefinitionId + ":" + ruleId);
            }
            return JsonUtility.FromJson<WorkHistoryPayload>(state.statePayload)
                ?? throw new InvalidOperationException(
                    "Work-identity rule-state payload is invalid.");
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                ledger);
            RequireFields(
                state,
                "work-identity owner state",
                "schemaVersion",
                "schema",
                "characterId",
                "producerStreamId",
                "firstDeliveryId",
                "secondDeliveryId",
                "stage",
                "narrative",
                "actorStats");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["characterId"],
                    characterId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["producerStreamId"],
                    producerStreamId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["firstDeliveryId"],
                    firstDeliveryId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["secondDeliveryId"],
                    secondDeliveryId,
                    StringComparison.Ordinal)
                || state["narrative"] is not JObject narrativeJson)
            {
                throw new InvalidOperationException(
                    "Work-identity checkpoint identity changed.");
            }

            CharacterNarrativeWorldSaveData narrative = narrativeJson
                    .ToObject<CharacterNarrativeWorldSaveData>()
                ?? throw new InvalidOperationException(
                    "Work-identity narrative save section is missing.");
            if (narrative.version != CharacterNarrativeWorldSaveData.CurrentVersion
                || narrative.characters == null
                || narrative.characters.Count != 0
                || narrative.identityStates == null
                || narrative.workCompletionDeliveries == null)
            {
                throw new InvalidOperationException(
                    "Work-identity narrative save section is incompatible.");
            }
            ValidateObservationSave(narrative, ledger.CaptureGameplayOutcomes());
            identityStates.RestoreTrustedTransactionSnapshot(
                narrative.identityStates);
            deliveries.Restore(narrative.workCompletionDeliveries);
            RestoreStats(actorFixture.Actor, state["actorStats"]);
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Work-identity checkpoint stage disagrees with its cursor.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            IReadOnlyList<CharacterIdentityRuntimeStateSaveData> stateRows =
                identityStates.Capture();
            IReadOnlyList<WorkCompletionIdentityDeliveryCursorSaveData> cursors =
                deliveries.Capture();
            WorkHistoryPayload small = ReadHistory(
                "character-trait:217",
                "mood:small-success");
            WorkHistoryPayload process = ReadHistory(
                "character-trait:230",
                "mood:first-process-success");
            bool valid = stage is >= 0 and <= 2
                && string.Equals(
                    actorFixture.Actor.Identity.PersistentId,
                    characterId,
                    StringComparison.Ordinal)
                && ExactOutcomeCount() == stage
                && OutcomeCount() == stage
                && ExactTypeCount(EnvironmentOutcomeIds.CropPlanTerminal) == stage
                && ExactTypeCount(EnvironmentOutcomeIds.CropIrrigationSupplied) == stage
                && ExactTypeCount(EnvironmentOutcomeIds.CropSelectionChanged) == 0
                && ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 3 * stage
                && cursors.Count == (stage == 0 ? 0 : 1)
                && stateRows.Count == (stage == 0 ? 0 : 1)
                && stateRows.Sum(value => value.rules?.Count ?? 0)
                    == (stage == 0 ? 0 : 2);
            if (stage == 0)
            {
                valid &= small == null && process == null;
            }
            else
            {
                WorkCompletionIdentityDeliveryCursorSaveData cursor =
                    cursors.Single();
                WorkCompletionIdentityDeliveryRequest latest =
                    CreateRequest(stage - 1);
                valid &= small != null
                    && small.lastSmallSuccessDay
                        == (stage == 1 ? FirstDay : SecondDay)
                    && process != null
                    && process.completedProcessIds.SequenceEqual(
                        new[] { BuiltInWorkTypeIds.Harvest.Value },
                        StringComparer.Ordinal)
                    && string.Equals(
                        cursor.producerStreamId,
                        producerStreamId,
                        StringComparison.Ordinal)
                    && cursor.operationSequence == stage - 1
                    && string.Equals(
                        cursor.deliveryId,
                        latest.DeliveryId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        cursor.payloadFingerprint,
                        latest.PayloadFingerprint,
                        StringComparison.Ordinal)
                    && cursor.disposition
                        == WorkCompletionIdentityDeliveryDisposition
                            .EffectsAppliedWithOutcome;
                for (int index = 0; index < stage; index++)
                {
                    valid &= RequireOutcome(
                            CreateRequest(index),
                            expectedTypeCount: stage,
                            throwOnFailure: false)
                        .HasCanonicalPayloadHash;
                }
            }
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Work-identity state, delivery cursor and outcome ledger diverged."
                    + "; stage=" + stage
                    + "; outcomes=" + string.Join(",", ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Select(value => value.outcomeTypeId + "@" + value.absoluteDay))
                    + "; states=" + JArray.FromObject(stateRows).ToString(Formatting.None)
                    + "; cursors=" + JArray.FromObject(cursors).ToString(Formatting.None));
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    [Serializable]
    private sealed class WorkHistoryPayload
    {
        public List<string> completedProcessIds = new();
        public int lastSmallSuccessDay = -1;
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly GameObject actorObject;
        private readonly CharacterSO data;

        internal ActorFixture(
            string persistentId,
            string displayName,
            int seed)
        {
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                displayName,
                "human");
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();
            actorObject = new GameObject(
                "WorkCompletionIdentityCausalProductionEntry_" + seed);
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.Identity.SetPersistentId(persistentId);
            Actor.Initialization(data);
            Actor.Progression.ApplyPreparedIdentity(
                displayName,
                "work-identity-causal-production-entry",
                new[] { 217, 230 },
                CharacterPotentialGrade.Ordinary,
                seed,
                autoChooseDrafts: false,
                maximumTraitCount: 4,
                ensureInitialDrafts: false);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            Actor.Stats.Stats[CharacterCondition.MOOD] = 70f;
            _ = Actor.Mood;
        }

        internal CharacterActor Actor { get; }

        public void Dispose()
        {
            if (actorObject != null)
                Object.DestroyImmediate(actorObject);
            if (data != null)
                Object.DestroyImmediate(data);
        }
    }

    private sealed class FixedWorld :
        ICharacterWorldQuery,
        ICharacterLifetimeQuery
    {
        private readonly CharacterActor[] actors;

        internal FixedWorld(CharacterActor actor) =>
            actors = new[] { actor ?? throw new ArgumentNullException(nameof(actor)) };

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters => actors;
    }

    private sealed class StableEnvironment : ICharacterEnvironmentStatusQuery
    {
        public CharacterEnvironmentExposure GetExposure(CharacterId characterId) =>
            new() { characterId = characterId.Value };

        public bool TryGetLightAdaptation(
            CharacterId characterId,
            out CharacterLightAdaptationSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public EnvironmentalExposureBand GetPhysiologicalBand(
            CharacterId characterId) => EnvironmentalExposureBand.Stable;
        public EnvironmentalExposureBand GetVisualBand(
            CharacterId characterId) => EnvironmentalExposureBand.Stable;
        public float GetWorkSpeedMultiplier(CharacterId characterId) => 1f;
        public float GetPrecisionWorkSpeedMultiplier(CharacterId characterId) => 1f;
        public float GetMoveSpeedMultiplier(CharacterId characterId) => 1f;
        public float GetAccuracyPenaltyPoints(CharacterId characterId) => 0f;
    }

    private sealed class FixedGameClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures
                ?? throw new ArgumentNullException(nameof(fixtures));

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                actionId,
                StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static JObject CaptureStats(CharacterActor actor)
    {
        CharacterMoodSnapshot mood = actor?.Mood
            ?? throw new InvalidOperationException(
                "Causal work-identity actor mood is unavailable.");
        return new JObject
        {
            ["conditions"] = new JArray(actor.Stats.StatSnapshot
                .OrderBy(value => (int)value.Key)
                .Select(value => new JObject
                {
                    ["condition"] = value.Key.ToString(),
                    ["value"] = value.Value
                })),
            ["currentHealth"] = actor.Stats.CurrentHealth,
            ["injurySeverity"] = actor.Stats.InjurySeverity,
            ["baseMood"] = mood.BaseValue,
            ["moodFactors"] = new JArray(mood.Factors
                .Where(value => value != null
                    && value.Kind == CharacterMoodFactorKind.Interaction)
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .Select(value => new JObject
                {
                    ["id"] = value.Id,
                    ["label"] = value.Label,
                    ["value"] = value.Value,
                    ["remainingSeconds"] = value.RemainingSeconds
                }))
        };
    }

    private static void RestoreStats(CharacterActor actor, JToken token)
    {
        if (actor == null || token is not JObject value)
            throw new InvalidOperationException(
                "Causal work-identity actor stats are missing.");
        RequireFields(
            value,
            "causal work-identity actor stats",
            "conditions",
            "currentHealth",
            "injurySeverity",
            "baseMood",
            "moodFactors");
        if (value["conditions"] is not JArray conditions
            || value["moodFactors"] is not JArray factors)
        {
            throw new InvalidOperationException(
                "Causal work-identity stat arrays are invalid.");
        }

        var restoredStats = new Dictionary<CharacterCondition, float>();
        foreach (JObject row in conditions.Cast<JObject>())
        {
            RequireFields(
                row,
                "causal work-identity condition",
                "condition",
                "value");
            if (!Enum.TryParse(
                    (string)row["condition"],
                    ignoreCase: false,
                    out CharacterCondition condition)
                || !restoredStats.TryAdd(condition, (float)row["value"]))
            {
                throw new InvalidOperationException(
                    "Causal work-identity condition is unknown or duplicated.");
            }
        }

        var restoredFactors = new List<CharacterMoodFactorSnapshot>();
        string previousFactor = string.Empty;
        foreach (JObject row in factors.Cast<JObject>())
        {
            RequireFields(
                row,
                "causal work-identity mood factor",
                "id",
                "label",
                "value",
                "remainingSeconds");
            string id = (string)row["id"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)
                || previousFactor.Length > 0
                && string.CompareOrdinal(previousFactor, id) >= 0)
            {
                throw new InvalidOperationException(
                    "Causal work-identity mood factors are invalid or unordered.");
            }
            previousFactor = id;
            restoredFactors.Add(new CharacterMoodFactorSnapshot(
                id,
                (string)row["label"] ?? string.Empty,
                (float)row["value"],
                CharacterMoodFactorKind.Interaction,
                (float)row["remainingSeconds"]));
        }
        actor.Stats.RestorePersistentState(
            restoredStats,
            (float)value["currentHealth"],
            (float)value["injurySeverity"],
            (float)value["baseMood"],
            restoredFactors);
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        if (value == null)
            throw new InvalidOperationException(label + " must be an object.");
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
