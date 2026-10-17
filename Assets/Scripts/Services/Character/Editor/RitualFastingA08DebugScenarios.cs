#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class RitualFastingA08DebugScenarios
{
    private const string ActorId = "character:qa-b0-con";
    // CharacterStats.GetConditionMaximum permits hunger above the routine 100.
    private const float HungerMaximum = 115f;

    private sealed class World : ICharacterWorldQuery
    {
        internal CharacterActor Actor;
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters =>
            Actor == null ? Array.Empty<CharacterActor>() : new[] { Actor };
    }

    private sealed class EmptyFestivals : IFestivalDefinitionCatalog
    {
        public IReadOnlyList<FestivalDefinitionSO> All =>
            Array.Empty<FestivalDefinitionSO>();

        public FestivalDefinitionSO Require(string festivalId) =>
            throw new InvalidOperationException(
                "A08 fixture has no authored festivals.");
    }

    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A08 requires a new create-only output directory.");
        }

        Directory.CreateDirectory(output);
        try
        {
            JObject result = VerifyRitualFastLifecycleAndMeals();
            result["observerRecovery"] = VerifyObserverRecovery();
            WriteReport(output, new JObject
            {
                ["schema"] = "a08-ritual-fasting@1",
                ["passed"] = true,
                ["rowId"] = "A08",
                ["controlledIntegration"] = true,
                ["actualGameplayScene"] = false,
                ["coverage"] = new JObject
                {
                    ["unchangedTickExcluded"] = true,
                    ["sameDayBoundaryRejectedWithoutMutation"] = true,
                    ["directMealBreaksActiveFast"] = true,
                    ["completionAwaitsPostFastMeal"] = true,
                    ["actualMealClearsPostFastPhase"] = true,
                    ["moodAndFastStateKeysSeparated"] = true,
                    ["saveRestore"] = true,
                    ["mealReplayNoDuplicate"] = true,
                    ["authoredEffectSources"] = true,
                    ["productionPhysicalMealAndHunger"] = true,
                    ["productionTimedDecayConsumer"] = true,
                    ["autonomousMealRejectedWithoutMutation"] = true,
                    ["manualCancellationWithoutMeal"] = true,
                    ["narrativeOwnerSaveRestore"] = true,
                    ["canonicalStartCompleteInterrupt"] = true,
                    ["exactCommittedMealCausation"] = true,
                    ["transitionRejectionRollback"] = true,
                    ["pendingMealConsequenceSaveRetry"] = true,
                    ["canonicalLedgerFreshOwnerRestore"] = true,
                    ["observerReservationRejection"] = true,
                    ["observerUnknownBeforeAndAfterCommit"] = true,
                    ["savedFactExactKeyRetry"] = true,
                    ["pendingDeathRetention"] = true
                },
                ["result"] = result,
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            Debug.Log("A08 ritual fasting scenarios: PASS");
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["schema"] = "a08-ritual-fasting@1",
                ["passed"] = false,
                ["rowId"] = "A08",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
    }

    private static JObject VerifyRitualFastLifecycleAndMeals()
    {
        using Fixture fixture = new();
        CharacterActor actor = fixture.Actor;
        {
            actor.Progression.ApplyPreparedIdentity(
                "A08 Ritual Fasting",
                "audit",
                new[] { 213 },
                CharacterPotentialGrade.Ordinary,
                8008,
                autoChooseDrafts: false);
            CharacterTraitSO trait = actor.Progression.ResolveSelectedTraits()
                .Single(value => value != null && value.id == 213);
            GameplayEffectBinding fastingEffect = trait.effects.Single(value =>
                value != null && value.bindingId == "trait:213:effect:0");
            GameplayEffectBinding postFastEffect = trait.effects.Single(value =>
                value != null && value.bindingId == "trait:213:effect:1");
            Require(fastingEffect.condition?.ConditionId
                    == "state:ritual-fasting"
                && Mathf.Approximately(fastingEffect.value, 0f)
                && postFastEffect.condition?.ConditionId
                    == "state:ritual-fast-ended"
                && Mathf.Approximately(postFastEffect.value, 1.15f),
                "Authored ritual-fast effect sources are not exact.");

            EditorFixedGameCalendar calendar = fixture.Calendar;
            CharacterIdentityStateStore store = fixture.Store;
            CharacterRitualFastingRuntime fasting = fixture.Fasting;
            float baselineConsumption = actor.GetConsumptionMultiplier();
            Require(baselineConsumption > 0f, "Inactive consumption must be positive.");

            fasting.Tick();
            string unchangedTick = JsonConvert.SerializeObject(store.Capture());
            fasting.Tick();
            Require(unchangedTick == JsonConvert.SerializeObject(store.Capture())
                && fasting.GetStatus(actor).Phase
                    == CharacterRitualFastPhase.Inactive,
                "No-festival tick created a ritual-fast state.");

            fixture.RequireTransitionRejection(CharacterRitualFastingOutcomeIds.Started);
            Require(fasting.TryBegin(actor, out string beginFailure),
                "Ritual fast did not begin: " + beginFailure);
            JObject activeDecay = fixture.RequireDecayMultiplier(0f, baselineConsumption);
            string beforeBoundary = JsonConvert.SerializeObject(store.Capture());
            Require(!fasting.TryComplete(actor, out _)
                && beforeBoundary == JsonConvert.SerializeObject(store.Capture())
                && fasting.GetStatus(actor).Phase
                    == CharacterRitualFastPhase.Fasting,
                "Same-day completion changed the fasting stage.");

            fixture.RequireTransitionRejection(CharacterRitualFastingOutcomeIds.Interrupted);
            string beforeCancellation = fixture.MealState();
            Require(fasting.TryBreak(actor, out string cancelFailure)
                && fasting.GetStatus(actor).Phase == CharacterRitualFastPhase.Inactive
                && HasMood(actor, "identity:ritual:fast-broken", -3f)
                && fixture.MealState() == beforeCancellation,
                "Manual fast cancellation changed food, hunger, or meal outcomes: " + cancelFailure);
            fixture.RequireDecayMultiplier(1f, baselineConsumption);
            Require(fasting.TryBegin(actor, out _), "Fast did not restart after manual cancellation.");
            fixture.RoundTripNarrative();
            store = fixture.Store;
            fasting = fixture.Fasting;
            Require(fasting.GetStatus(actor).Phase
                    == CharacterRitualFastPhase.Fasting,
                "Active fast did not survive Narrative owner restore.");
            fixture.RequireDecayMultiplier(0f, baselineConsumption);
            fixture.RequireAutonomousMealRejected();

            JObject breakMeal = fixture.ConsumeAndReplay("consumable-operation:a08-break-fast");
            store = fixture.Store;
            fasting = fixture.Fasting;
            Require(fasting.GetStatus(actor).Phase
                    == CharacterRitualFastPhase.Inactive
                && HasMood(actor, "identity:ritual:fast-broken", -3f),
                "Direct committed meal did not break the active fast with mood.");
            fixture.RequireDecayMultiplier(1f, baselineConsumption);

            Require(fasting.TryBegin(actor, out string restartFailure),
                "Ritual fast did not restart after break: " + restartFailure);
            calendar.SetDateTime(11, 8);
            fixture.RequireTransitionRejection(CharacterRitualFastingOutcomeIds.Completed);
            Require(fasting.TryComplete(actor, out string completeFailure),
                "Ritual fast did not complete: " + completeFailure);
            Require(fasting.GetStatus(actor).Phase
                    == CharacterRitualFastPhase.AwaitingPostFastMeal
                && HasMood(actor, "identity:ritual:fast-completed", 3f),
                "Completion mood overwrote the post-fast meal stage.");
            RequireSeparatedStateKeys(store, trait, awaitingPostFastMeal: true);
            JObject postFastDecay = fixture.RequireDecayMultiplier(1.15f, baselineConsumption);

            fixture.RoundTripNarrative();
            store = fixture.Store;
            fasting = fixture.Fasting;
            Require(fasting.GetStatus(actor).Phase
                    == CharacterRitualFastPhase.AwaitingPostFastMeal,
                "Post-fast meal stage did not survive Narrative owner restore.");
            fixture.RequireDecayMultiplier(1.15f, baselineConsumption);
            JObject postFastMeal = fixture.ConsumeAndReplay("consumable-operation:a08-post-fast-meal");
            store = fixture.Store;
            fasting = fixture.Fasting;
            Require(fasting.GetStatus(actor).Phase
                    == CharacterRitualFastPhase.Inactive,
                "Committed post-fast meal did not clear the awaiting stage.");
            RequireSeparatedStateKeys(store, trait, awaitingPostFastMeal: false);
            fixture.RequireDecayMultiplier(1f, baselineConsumption);
            fixture.RoundTripNarrative();
            store = fixture.Store;
            fasting = fixture.Fasting;
            fixture.RequireDecayMultiplier(1f, baselineConsumption);

            string beforeFinalTick = JsonConvert.SerializeObject(store.Capture());
            fasting.Tick();
            Require(beforeFinalTick == JsonConvert.SerializeObject(store.Capture()),
                "No-festival terminal tick changed restored ritual state.");
            JObject transitions = fixture.RequireCanonicalTransitions();

            return new JObject
            {
                ["characterId"] = ActorId,
                ["fastStateRuleId"] =
                    CharacterRitualFastingRuntime.StateRuleId,
                ["moodNeedRuleId"] = CharacterRitualFastingRuntime.NeedId,
                ["breakMeal"] = breakMeal,
                ["postFastMeal"] = postFastMeal,
                ["fastingTimedDecay"] = activeDecay,
                ["postFastTimedDecay"] = postFastDecay,
                ["mealReplayMutationCount"] = 0,
                ["narrativeOwnerRoundTrips"] = fixture.NarrativeRoundTrips,
                ["physicalFoodRemaining"] = fixture.FoodQuantity,
                ["mealParentCount"] = fixture.ParentCount,
                ["mealEventCount"] = fixture.MealEvents,
                ["fastingTransitions"] = transitions,
                ["fastingEffectBinding"] = fastingEffect.bindingId,
                ["postFastEffectBinding"] = postFastEffect.bindingId,
                ["finalPhase"] = fasting.GetStatus(actor).Phase.ToString()
            };
        }
    }

    private static JObject VerifyObserverRecovery()
    {
        int cases = 0;
        foreach (CharacterRitualFastTransition transition in Enum.GetValues(typeof(CharacterRitualFastTransition)))
        foreach (bool hiddenAck in new[] { false, true })
        {
            using Fixture fixture = new();
            PrepareFaster(fixture);
            string previousCycle = null;
            if (transition == CharacterRitualFastTransition.Interrupted)
            {
                Require(fixture.Fasting.TryBegin(fixture.Actor, out _), "Previous cycle could not start.");
                previousCycle = ReadFasting(fixture).cycleStartResultKey;
                Require(fixture.Fasting.TryBreak(fixture.Actor, out _), "Previous cycle could not end.");
            }
            if (transition != CharacterRitualFastTransition.Started)
                Require(fixture.Fasting.TryBegin(fixture.Actor, out _), "Observer recovery setup could not start fasting.");
            if (transition == CharacterRitualFastTransition.Completed) fixture.Calendar.SetDateTime(11, 8);
            string before = JsonConvert.SerializeObject(fixture.Store.Capture());
            string beforeMood = JsonConvert.SerializeObject(fixture.Actor.Stats.CaptureMoodDeliveryTransactionState());
            int count = fixture.ObserverLedger.CaptureGameplayOutcomes().exactOutcomes.Count;
            fixture.FastingDelivery.RejectReservation = true;
            Require(!Invoke(fixture, transition) && before == JsonConvert.SerializeObject(fixture.Store.Capture())
                && beforeMood == JsonConvert.SerializeObject(fixture.Actor.Stats.CaptureMoodDeliveryTransactionState())
                && count == fixture.ObserverLedger.CaptureGameplayOutcomes().exactOutcomes.Count,
                "Fasting observer reservation rejection changed effects or identity.");
            fixture.FastingDelivery.RejectReservation = false;
            fixture.FastingDelivery.ThrowBefore = !hiddenAck;
            fixture.FastingDelivery.HideAcknowledgement = hiddenAck;
            Require(!Invoke(fixture, transition), "Unknown fasting publication was reported delivered.");
            var saved = ReadFasting(fixture);
            Require(saved.pending.active && !string.IsNullOrEmpty(saved.pending.publicationKey)
                && fixture.ObserverLedger.CaptureGameplayOutcomes().exactOutcomes.Count == count + (hiddenAck ? 1 : 0),
                "Unknown fasting publication lost its saved owner or fabricated a record.");
            string key = saved.pending.publicationKey;
            var frozen = new CharacterRitualFastTransitionFact(saved.pending.fact);
            saved.pending.fact.characterName += " changed";
            Require(frozen.Snapshot().characterName != saved.pending.fact.characterName, "Fasting fact retained mutable save data.");
            var missing = JObject.Parse(JsonUtility.ToJson(ReadFasting(fixture)));
            ((JObject)missing["pending"]["fact"]).Remove("moodAfter");
            bool rejected = false;
            try { CharacterRitualFastingRuntime.ValidateStatePayload(missing.ToString()); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Fasting restore accepted an incomplete frozen fact.");
            fixture.RoundTripNarrative();
            string mood = JsonConvert.SerializeObject(fixture.Actor.Stats.CaptureMoodDeliveryTransactionState());
            var results = new DomainFactDelivery[1];
            Require(!fixture.FastingDelivery.Inner.ReplayBatch(new[] { DomainFactReplay.Create(key + ":wrong", frozen) },
                results, out _), "Fasting observer accepted another publication key.");
            if (previousCycle != null)
            {
                var wrongCycle = frozen.Snapshot();
                wrongCycle.cycleStartPublicationKey = previousCycle;
                int countBeforeInvalidReplay = fixture.ObserverLedger.CaptureGameplayOutcomes().exactOutcomes.Count;
                bool invalidCycleRejected;
                try
                {
                    invalidCycleRejected = !fixture.FastingDelivery.Inner.ReplayBatch(new[]
                        { DomainFactReplay.Create(key, new CharacterRitualFastTransitionFact(wrongCycle)) }, results, out _);
                }
                catch (InvalidOperationException) { invalidCycleRejected = true; }
                Require(invalidCycleRejected
                    && fixture.ObserverLedger.CaptureGameplayOutcomes().exactOutcomes.Count == countBeforeInvalidReplay,
                    "Fasting replay accepted a different cycle's committed start.");
            }
            fixture.FastingDelivery.HoldReplay = true;
            fixture.Fasting.Tick();
            Require(ReadFasting(fixture).pending.active && mood == JsonConvert.SerializeObject(
                fixture.Actor.Stats.CaptureMoodDeliveryTransactionState()), "Deferred replay changed fasting effects.");
            fixture.FastingDelivery.HoldReplay = false;
            fixture.Fasting.Tick();
            Require(!ReadFasting(fixture).pending.active && ReadFasting(fixture).lastTransitionResultKey == key
                && fixture.FastingDelivery.LastReplayKey == key
                && fixture.ObserverLedger.CaptureGameplayOutcomes().exactOutcomes.Count == count + 1
                && mood == JsonConvert.SerializeObject(fixture.Actor.Stats.CaptureMoodDeliveryTransactionState()),
                "Restored fasting fact failed to settle exactly once without repeating mood.");
            string settled = JsonConvert.SerializeObject(fixture.Store.Capture());
            fixture.Fasting.Tick(); fixture.Fasting.Tick();
            Require(settled == JsonConvert.SerializeObject(fixture.Store.Capture())
                && fixture.ObserverLedger.CaptureGameplayOutcomes().exactOutcomes.Count == count + 1,
                "Settled fasting tick duplicated state or records.");
            cases++;
        }
        using (Fixture fixture = new())
        {
            PrepareFaster(fixture);
            Require(fixture.Fasting.TryBegin(fixture.Actor, out _), "Retention setup did not start fasting.");
            int count = fixture.ObserverLedger.CaptureGameplayOutcomes().exactOutcomes.Count;
            fixture.FastingDelivery.ThrowBefore = true;
            Require(!fixture.Fasting.TryBreak(fixture.Actor, out _), "Retention setup has no pending fact.");
            fixture.Store.RemoveCharacter(ActorId, new ICharacterIdentityDeathStateRetentionPolicy[] { fixture.Fasting });
            Require(ReadFasting(fixture).pending.retainedForDeath, "Death cleanup removed pending fasting evidence.");
            fixture.RoundTripNarrative();
            fixture.DetachWorldActorForRecovery();
            fixture.Fasting.Tick();
            Require(fixture.Store.CaptureForRule(CharacterRitualFastingRuntime.StateRuleId).Count == 0
                && fixture.ObserverLedger.CaptureGameplayOutcomes().exactOutcomes.Count == count + 1,
                "Death-retained fasting fact did not settle/retire without its live actor.");
        }
        return new JObject { ["transitionRecoveryCases"] = cases, ["retentionPolicyCases"] = 1,
            ["actualCombatDeath"] = false, ["naturalAi"] = false, ["passed"] = true };

        static bool Invoke(Fixture fixture, CharacterRitualFastTransition transition) => transition switch
        {
            CharacterRitualFastTransition.Started => fixture.Fasting.TryBegin(fixture.Actor, out _),
            CharacterRitualFastTransition.Completed => fixture.Fasting.TryComplete(fixture.Actor, out _),
            CharacterRitualFastTransition.Interrupted => fixture.Fasting.TryBreak(fixture.Actor, out _),
            _ => throw new ArgumentOutOfRangeException(nameof(transition))
        };
        static void PrepareFaster(Fixture fixture) => fixture.Actor.Progression.ApplyPreparedIdentity(
            "A08 Observer Recovery", "audit", new[] { 213 }, CharacterPotentialGrade.Ordinary, 8008, autoChooseDrafts: false);
        static CharacterRitualFastRuntimeState ReadFasting(Fixture fixture) => JsonUtility.FromJson<CharacterRitualFastRuntimeState>(
            fixture.Store.CaptureForRule(CharacterRitualFastingRuntime.StateRuleId).Single().State.statePayload);
    }

    private sealed class FastingFactProbe : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        internal readonly DomainFactDispatcher Inner;
        internal bool RejectReservation, ThrowBefore, HideAcknowledgement, HoldReplay;
        internal string LastReplayKey;
        internal CharacterRitualFastMealObservedFact? LastMeal;
        internal FastingFactProbe(DomainFactDispatcher inner) => Inner = inner;
        public bool TryReserve<TFact>(in TFact fact, out DomainFactLease lease, out string failure)
        {
            if (fact is CharacterRitualFastMealObservedFact meal) LastMeal = meal;
            if (typeof(TFact) == typeof(CharacterRitualFastTransitionFact) && RejectReservation)
            { lease = default; failure = "qa:fasting-reservation-rejected"; return false; }
            return Inner.TryReserve(fact, out lease, out failure);
        }
        public DomainFactDelivery Publish<TFact>(in DomainFactLease lease, in TFact fact)
        {
            bool target = typeof(TFact) == typeof(CharacterRitualFastTransitionFact);
            if (target && ThrowBefore)
            { ThrowBefore = false; Inner.Cancel(lease); throw new InvalidOperationException("qa:fasting-unknown-before-commit"); }
            var delivery = Inner.Publish(lease, fact);
            if (target && HideAcknowledgement && delivery.Completed)
            { HideAcknowledgement = false; return DomainFactDelivery.Uncertain("qa:fasting-hidden-ack"); }
            return delivery;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure)
        {
            LastReplayKey = facts[0].PublicationKey;
            if (!HoldReplay) return Inner.ReplayBatch(facts, results, out failure);
            for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Uncertain("qa:fasting-held-replay");
            failure = "qa:fasting-held-replay"; return true;
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            Inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<TFact>(in TFact fact) => Inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => Inner.Cancel(lease);
    }

    private static CharacterRitualFastingRuntime CreateRuntime(
        CharacterIdentityStateStore store,
        IGameCalendar calendar,
        World world,
        MigratedProducerOutcomeEditorFixture outcomes,
        out DomainFactDispatcher boundFacts, out FastingFactProbe probe)
    {
        CharacterPersistentNeedRuntime needs = new(store, calendar);
        CharacterMoodPolicyService moods = new(
            new CharacterIdentityRuleRouter(),
            needs);
        boundFacts = CreateFastingFacts(outcomes);
        probe = new FastingFactProbe(boundFacts);
        return new CharacterRitualFastingRuntime(
            store,
            moods,
            calendar,
            world,
            new EmptyFestivals(), probe, probe);
    }

    private static DungeonStory.Foundation.Facts.DomainFactDispatcher CreateFastingFacts(MigratedProducerOutcomeEditorFixture outcomes)
    {
        var facts = new DungeonStory.Foundation.Facts.DomainFactDispatcher();
        var mapping = new CharacterRitualFastingFactMapping(outcomes.Ledger, outcomes.Transaction,
            new GameplayDomainFactCommitGroup(outcomes.Recorder, outcomes.Ledger));
        facts.Subscribe("test:ritual-fast", new GameplayDomainFactObserver<CharacterRitualFastTransitionFact,
            CharacterRitualFastingOutcomeReceipt>(mapping.Capture, outcomes.Registry, outcomes.Ledger,
            outcomes.Recorder, new GameplayDomainFactCommitGroup(outcomes.Recorder, outcomes.Ledger)));
        facts.Subscribe("test:ritual-fast-meal", CharacterRitualFastingAuxiliaryObservation.Meals(mapping));
        facts.Subscribe("test:ritual-fast-mood", CharacterRitualFastingAuxiliaryObservation.Moods());
        return facts;
    }

    // Real production owners under bounded inputs; not natural UI/AI gameplay.
    private sealed class Fixture : IDisposable
    {
        private const string FoodId = "food:roasted-meat";
        private readonly CharacterSO actorData;
        private readonly BuildingSO buildingData;
        private readonly DungeonRuntimeAggregateRootStore root = new();
        private readonly IGameContentCatalog content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        private readonly World world;
        private readonly MutableClock clock = new();
        private readonly WorldItemStackRuntime items;
        private readonly ItemQuantityReservationService reservations;
        private readonly IReservedItemTransferService transfers;
        private readonly IReservedPhysicalItemBatchDispositionService reservedBatch;
        private readonly IPhysicalItemBatchDispositionService batch;
        private readonly IItemDefinitionCatalog itemCatalog;
        private readonly GameEventBus events = new();
        private readonly IDisposable mealSubscription;
        private MigratedProducerOutcomeEditorFixture outcomes;
        private DomainFactDispatcher consumptionFacts;
        private DomainFactDispatcher restFacts;
        private DomainFactDispatcher fastingFacts;
        internal FastingFactProbe FastingDelivery;
        internal GameplayOutcomeLedger ObserverLedger => outcomes.Ledger;
        internal void DetachWorldActorForRecovery() => world.Actor = null;
        private readonly string stackId;
        private CharacterNarrativeRuntime narrative;
        private CharacterPerformanceQuery performance;
        private CharacterConsumablesApplicationPorts ports;
        private CharacterConsumablesRuntime consumables;
        internal readonly CharacterActor Actor;
        internal readonly BuildableObject Facility;
        internal readonly EditorFixedGameCalendar Calendar = new(10, 8);
        internal CharacterIdentityStateStore Store = new();
        internal CharacterRitualFastingRuntime Fasting;
        internal int NarrativeRoundTrips;
        internal int MealEvents;
        internal int FoodQuantity => items.GetAllStacks().Where(value => value.ItemId == FoodId).Sum(value => value.Quantity);
        private string MealOutcomeType => MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.CharacterConsumablesMealResult).OutcomeTypeId.Value;
        internal int ParentCount => outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(value => value.outcomeTypeId == MealOutcomeType);

        internal Fixture()
        {
            GameObject host = new("A08 Ritual Fasting Audit");
            host.AddComponent<SpriteRenderer>();
            Actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(host);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(CharacterType.NPC, "A08 Ritual Fasting", "Slime");
            Actor.Initialization(actorData);
            Actor.Identity.SetPersistentId(ActorId);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (Actor.IsUnpublishedComposition) Actor.PublishComposition();
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacterLifetime(Actor);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(Actor);
            CharacterAiEditorTestDependencies.BodyHealthRuntime.ConfigureVitals(Actor, 100f, true);
            Require(Actor.CanRunAi, "A08 real meal actor is not active.");
            Actor.stats[CharacterCondition.HUNGER] = 20f;
            world = new World { Actor = Actor };

            Facility = new GameObject("A08 Meal Fixture").AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(Facility);
            buildingData = ScriptableObject.CreateInstance<BuildingSO>();
            buildingData.id = 99168;
            buildingData.objectName = "A08 Meal Fixture";
            buildingData.width = buildingData.height = 1;
            buildingData.category = BuildingCategory.Shop;
            buildingData.Facility = new FacilityData { roles = FacilityRole.Meal, capacity = 1 };
            Facility.Initialization(buildingData, Vector2Int.zero);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterBuilding(Facility);
            itemCatalog = new ResourceItemDefinitionCatalog(content);
            var provider = new ResourceDungeonItemCatalogProvider(itemCatalog);
            items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(provider, root,
                out var repository, out _, out reservations, out transfers, out reservedBatch, out batch);
            var spawner = new WorldItemSpawner(provider, repository, EditorNullItemMarkerPresenter.Instance);
            string destination = CharacterConsumablesRuntime.GetMealDestinationId(Facility.RequirePersistentInstanceId(), new ConsumableItemDefinitionId(FoodId));
            Require(spawner.Spawn(FoodId, 2, Vector2Int.zero, WorldItemStackState.FacilityBuffer,
                destination, contamination: 0f) == 2, "A08 could not prepare two physical test servings.");
            stackId = items.GetAllStacks().Single(value => value.ItemId == FoodId).StackId;
            outcomes = new MigratedProducerOutcomeEditorFixture("run:a08-ritual-fasting", root, includeRitualFasting: true);
            RebindFasting();
            narrative = NewNarrative(new DungeonRuntimeAggregateRootStore(), Store);
            narrative.Register(CharacterPersistentIdentity.Require(Actor), Actor.profile.PhenotypeSpeciesId,
                Array.Empty<string>(), Array.Empty<string>());
            mealSubscription = events.Subscribe<PhysicalMealConsumedEvent>(value =>
            {
                Require(ReferenceEquals(value.Actor, Actor) && ReferenceEquals(value.Facility, Facility)
                    && CharacterPersistentIdentity.Require(value.Actor).Equals(CharacterPersistentIdentity.Require(Actor))
                    && value.FacilityId.Equals(Facility.RequirePersistentInstanceId())
                    && value.OperationId.IsValid && value.Result.OperationId.Equals(value.OperationId)
                    && value.Result.Success && !string.IsNullOrWhiteSpace(value.Result.OutcomeResultKey),
                    "A08 production physical meal event identity is not exact.");
                MealEvents++;
            });
            BindConsumers(null);
        }

        private CharacterNarrativeRuntime NewNarrative(DungeonRuntimeAggregateRootStore ownerRoot, CharacterIdentityStateStore store) =>
            new(ownerRoot, new CharacterNarrativeCatalog(content), store, content);

        private void BindConsumers(DungeonCharacterConsumablesSaveData saved)
        {
            consumptionFacts?.Dispose();
            consumptionFacts = CharacterConsumptionFactFixture.Create(
                outcomes.Transaction,
                outcomes.Recorder,
                outcomes.Ledger);
            restFacts?.Dispose();
            restFacts = outcomes.CreateCharacterRestFacts();
            var shared = new CharacterDerivedStatsSnapshotProjector(content, new EmptyEffects(), new EmptyEffects(),
                new ExtremeTraitRuntime(Store), clock);
            performance = new CharacterPerformanceQuery(CharacterAiEditorTestDependencies.BodyHealthRuntime,
                new ResourceAnatomyProfileCatalog(content), narrative, Calendar, shared,
                new CharacterPerformanceFormulaCatalog(content));
            var projection = new CharacterStatsProjectionService(new NoStaffDiscontent(), new NoMetaProgression(),
                NoCharacterDeprivationBoundary.Instance, NeutralCharacterSubstanceRuntime.Instance,
                NeutralCharacterEnvironmentStatusQuery.Instance, NeutralExternalCombatInfluenceQuery.Instance,
                NeutralContentWorkDelayQuery.Instance, NeutralDiseaseSymptomEffectQuery.Instance,
                NeutralCharacterCombatSpecialStatusQuery.Instance, NeutralCombatEquipmentBurdenQuery.Instance,
                Calendar, shared, ritualFasting: Fasting, performance: performance);
            Actor.Stats.ConstructCharacterStats(clock, Calendar, Actor.Stats.NeedDefinitionCatalog,
                DisabledDungeonDebugRuleQuery.Instance, projection,
                new CharacterNeedStateService(DefaultCharacterNeedBalanceRuntime.Instance, DisabledDungeonDebugRuleQuery.Instance),
                new CharacterMoodStateService(clock, Actor.Stats.NeedDefinitionCatalog), new CharacterStatsMaintenanceSchedule(),
                events, restFacts, restFacts, performance,
                new CharacterWorkPerformanceContextResolver(narrative, Calendar, combatEquipment: null));
            ports = new CharacterConsumablesApplicationPorts(itemCatalog, items, CharacterAiEditorTestDependencies.WorldRegistry,
                events, new NoCombat(), performance, narratives: narrative, narrativeCatalog: new CharacterNarrativeCatalog(content),
                ritualFastingQuery: Fasting, ritualFastingCommand: Fasting,
                quantityReservations: reservations, reservedTransfers: transfers,
                reservedBatchDispositions: reservedBatch, batchDispositions: batch,
                tareDispositions: new PackagedLotTareDispositionService((IPackagedLotDefinitionQuery)items.MassQuery, new NoTareExpected()),
                bodyHealthCommands: CharacterAiEditorTestDependencies.BodyHealthRuntime,
                bodyHealthPersistence: CharacterAiEditorTestDependencies.BodyHealthRuntime);
            consumables = new CharacterConsumablesRuntime(ports, ports, ports, clock, new RandomStreamProvider(8008), root,
                DefaultCharacterNeedBalanceRuntime.Instance,
                domainFacts: consumptionFacts,
                calendar: Calendar);
            if (saved != null) consumables.PublishRestoreCandidate(consumables.BuildRestoreCandidate(saved));
        }

        internal void RoundTripNarrative()
        {
            string before = JsonConvert.SerializeObject(narrative.Capture());
            var saved = JsonConvert.DeserializeObject<CharacterNarrativeWorldSaveData>(before)
                ?? throw new InvalidOperationException("A08 Narrative JSON returned null.");
            var meals = JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(JsonUtility.ToJson(consumables.Capture()));
            var originalLedger = outcomes.Ledger.CaptureGameplayOutcomes();
            string ledgerBefore = new GameplayOutcomeLedgerSaveSection(outcomes.Ledger).Capture();
            var ledgerSave = DungeonSaveSectionPayload.Deserialize<GameplayOutcomeLedgerSaveData>(ledgerBefore);
            for (int index = 0; index < originalLedger.exactOutcomes.Count; index++)
            {
                var original = originalLedger.exactOutcomes[index];
                var parsed = ledgerSave.exactOutcomes[index];
                for (int metric = 0; metric < original.metrics.Count; metric++)
                    Require(BitConverter.DoubleToInt64Bits(original.metrics[metric].value)
                        == BitConverter.DoubleToInt64Bits(parsed.metrics[metric].value),
                        "A08 official ledger reader changed exact metric bits.");
            }
            var restoredOutcomes = new MigratedProducerOutcomeEditorFixture("run:a08-fresh-ledger", root, includeRitualFasting: true);
            restoredOutcomes.Ledger.BeginRestoreCandidate();
            restoredOutcomes.Ledger.PublishGameplayOutcomeRestore(restoredOutcomes.Ledger.PrepareGameplayOutcomeRestore(ledgerSave));
            restoredOutcomes.Ledger.PublishRestoreCandidate();
            restoredOutcomes.Ledger.CompleteRestoreCandidate();
            JObject restoredLedger = JObject.Parse(JsonUtility.ToJson(restoredOutcomes.Ledger.CaptureGameplayOutcomes()));
            JObject previousLedger = JObject.Parse(ledgerBefore);
            Require(restoredLedger.Value<long>("worldEpoch") == previousLedger.Value<long>("worldEpoch") + 1,
                "A08 fresh canonical ledger did not advance exactly one restore epoch.");
            restoredLedger["worldEpoch"] = previousLedger["worldEpoch"];
            Require(JToken.DeepEquals(previousLedger, restoredLedger), "A08 fresh ledger changed canonical outcomes or replay identity.");
            outcomes = restoredOutcomes;
            CharacterIdentityStateStore restoredStore = new();
            CharacterNarrativeRuntime restored = NewNarrative(new DungeonRuntimeAggregateRootStore(), restoredStore);
            restored.PublishRestore(restored.PrepareRestore(saved));
            Require(JToken.DeepEquals(JToken.Parse(before), JToken.FromObject(restored.Capture())),
                "A08 fresh Narrative owner round trip changed its payload.");
            Store = restoredStore;
            narrative = restored;
            RebindFasting();
            BindConsumers(meals);
            NarrativeRoundTrips++;
        }

        private void RebindFasting()
        {
            fastingFacts?.Dispose();
            Fasting = CreateRuntime(Store, Calendar, world, outcomes, out fastingFacts, out FastingDelivery);
        }

        internal JObject RequireDecayMultiplier(float expectedRatio, float baseline)
        {
            float consumption = Actor.GetConsumptionMultiplier();
            Require(Mathf.Abs(consumption - baseline * expectedRatio) < .0001f,
                $"A08 production consumption ratio mismatch: expected={expectedRatio}; actual={consumption / baseline}.");
            var decay = new CharacterNeedStateService(DefaultCharacterNeedBalanceRuntime.Instance, DisabledDungeonDebugRuleQuery.Instance)
                .CalculateTimedDecay(Actor, 5f);
            float hungerBefore = Actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
            var tick = Actor.Stats.ChangeStatByTick();
            Require(tick.MoveNext(), "A08 real need tick did not execute.");
            (tick as IDisposable)?.Dispose();
            float hungerAfter = Actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
            Require(Mathf.Abs(hungerAfter - Mathf.Clamp(hungerBefore - decay.Hunger, 0f, HungerMaximum)) < .0001f,
                "A08 projected consumption did not reach authoritative hunger decay.");
            return new JObject { ["ratio"] = expectedRatio, ["consumption"] = consumption,
                ["hungerDepletion"] = decay.Hunger, ["hungerBefore"] = hungerBefore, ["hungerAfter"] = hungerAfter };
        }

        internal string MealState() => JsonConvert.SerializeObject(new
        {
            food = JsonUtility.ToJson(items.Capture()), hunger = Actor.Stats.GetConditionValue(CharacterCondition.HUNGER),
            meals = consumables.Capture(), parents = outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Where(value => value.outcomeTypeId == MealOutcomeType).ToArray(), events = MealEvents
        });

        internal void RequireTransitionRejection(string kind)
        {
            string before = JsonConvert.SerializeObject(Store.Capture());
            string moods = JsonConvert.SerializeObject(Actor.Stats.CaptureMoodDeliveryTransactionState());
            int roots = outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;
            Fasting.DebugRejectTransitionCommit = value => CharacterRitualFastingFactMapping.Kind(value) == kind;
            bool accepted;
            string failure;
            try
            {
                accepted = kind == CharacterRitualFastingOutcomeIds.Started ? Fasting.TryBegin(Actor, out failure)
                    : kind == CharacterRitualFastingOutcomeIds.Completed ? Fasting.TryComplete(Actor, out failure)
                    : Fasting.TryBreak(Actor, out failure);
            }
            finally { Fasting.DebugRejectTransitionCommit = null; }
            Require(!accepted && failure == "ritual-fast-debug-commit-rejected"
                && before == JsonConvert.SerializeObject(Store.Capture())
                && moods == JsonConvert.SerializeObject(Actor.Stats.CaptureMoodDeliveryTransactionState())
                && roots == outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count,
                "A08 rejected transition partially changed state, mood or canonical outcomes: " + kind);
        }

        internal JObject RequireCanonicalTransitions()
        {
            JObject ledger = JObject.Parse(JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()));
            JObject[] rows = ledger["exactOutcomes"].OfType<JObject>()
                .Where(value => value.Value<string>("outcomeTypeId") == CharacterRitualFastingOutcomeIds.Transitioned.Value).ToArray();
            string Fact(JObject row, string id) => row["facts"].OfType<JObject>().Single(value => value.Value<string>("factId") == id).Value<string>("value");
            Require(rows.Length == 6
                && rows.Count(value => Fact(value, "ritual-fast.transition") == "started") == 3
                && rows.Count(value => Fact(value, "ritual-fast.transition") == "completed") == 1
                && rows.Count(value => Fact(value, "ritual-fast.transition") == "interrupted") == 2,
                "A08 did not retain exact start/complete/manual and meal interruption roots.");
            Require(rows.Select(value => value.Value<string>("operationId")).Distinct().Count() == 6,
                "A08 same-day legitimate restart reused a transition identity.");
            JObject interrupted = rows.Single(value => Fact(value, "ritual-fast.reason") == CharacterRitualFastingOutcomeIds.Meal);
            Require(Fact(interrupted, "ritual-fast.meal-operation") == "consumable-operation:a08-break-fast"
                && Fact(interrupted, "ritual-fast.meal-result") == consumables.Capture().completedOperations
                    .Single(value => value.operationId == "consumable-operation:a08-break-fast").outcomeResultKey,
                "A08 meal interruption lost its exact causal result key.");
            var mealOwner = consumables.Capture().completedOperations.Single(value => value.operationId == "consumable-operation:a08-break-fast");
            JObject mealRoot = ledger["exactOutcomes"].OfType<JObject>().Single(value =>
                value.Value<string>("producerId") + "|" + value.Value<string>("operationId") + "|"
                + value.Value<long>("commitRevision") + "|" + value.Value<int>("localResultIndex") == mealOwner.outcomeResultKey);
            Require(interrupted.Value<string>("parentRunId") == mealRoot.Value<string>("runId")
                && interrupted.Value<long>("parentSequence") == mealRoot.Value<long>("sequence")
                && interrupted.Value<string>("causationRelationId") == "direct-effect",
                "A08 interruption is not causally linked to the exact physical meal root.");
            foreach (JObject row in rows)
            {
                Require(row["participants"].OfType<JObject>().Single().Value<string>("entityId") == ActorId
                    && row.Value<long>("commitRevision") == row.Value<long>("ownerRevision")
                    && row.Value<int>("absoluteDay") == (Fact(row, "ritual-fast.transition") == "completed" ? 11 : 10),
                    "A08 transition actor is not exact.");
            }
            return new JObject { ["count"] = rows.Length, ["started"] = 3, ["completed"] = 1, ["interrupted"] = 2,
                ["roots"] = new JArray(rows), ["diagnosticFailuresExcluded"] = true };
        }

        internal void RequireAutonomousMealRejected()
        {
            string before = MealState();
            Require(!consumables.TryConsumeMeal(CharacterPersistentIdentity.Require(Actor), Facility.RequirePersistentInstanceId(), out var result)
                && result.FailureCode == CharacterConsumablesFailureCode.PolicyForbidden
                && result.Parameters.Contains("ritual-fast")
                && MealState() == before, "Autonomous fasting meal was not rejected without mutation.");
        }

        private void RequireExactMealEvidence()
        {
            Require(FastingDelivery.LastMeal.HasValue, "Fasting did not observe the actual physical meal fact.");
            var observed = FastingDelivery.LastMeal.Value;
            var mapping = new CharacterRitualFastingFactMapping(outcomes.Ledger, outcomes.Transaction,
                new GameplayDomainFactCommitGroup(outcomes.Recorder, outcomes.Ledger));
            string before = JsonConvert.SerializeObject(outcomes.Ledger.CaptureGameplayOutcomes());
            mapping.RequireMeal(observed);
            var saved = JsonUtility.FromJson<CharacterMealConsumedFactData>(
                JsonUtility.ToJson(CharacterMealConsumedFactData.Capture(observed.Meal)));
            mapping.RequireMeal(new(observed.CharacterId, saved.ToFact(), observed.PublicationKey));
            foreach (bool forgedActor in new[] { false, true })
            {
                var changed = saved.Clone();
                if (forgedActor) changed.characterId = "character:a08-wrong-meal-recipient";
                else changed.quantity++;
                bool rejected = false;
                try { mapping.RequireMeal(new(changed.characterId, changed.ToFact(), observed.PublicationKey)); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Fasting observer accepted altered meal consumption evidence.");
            }
            Require(before == JsonConvert.SerializeObject(outcomes.Ledger.CaptureGameplayOutcomes()),
                "Meal evidence validation changed its canonical ledger.");
        }

        internal JObject ConsumeAndReplay(string operationId)
        {
            int foodBefore = FoodQuantity;
            int parentBefore = ParentCount;
            int eventsBefore = MealEvents;
            float hungerBefore = Actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
            Require(ports.TryGetMeal(new ConsumableItemDefinitionId(FoodId), out var definition), "Authored A08 meal is missing.");
            float nutrition = definition.Nutrition * performance.Evaluate(Actor, CharacterPerformanceFormulaIds.NutritionEfficiency).Value;
            ConsumeMealCommand command = new(new ConsumableOperationId(operationId), CharacterPersistentIdentity.Require(Actor),
                Facility.RequirePersistentInstanceId(), new ItemStackId(stackId));
            Require(!consumables.TryConsumeMeal(command, out var pending) && pending.IsAcceptedPending,
                "A08 direct meal did not enter the real Eating phase.");
            var plan = consumables.Capture().activeMealPlans.Single();
            Require(plan.planId == operationId && plan.phase == CharacterMealPlanPhase.Eating && !plan.automaticOperation
                && plan.sourceStackId == command.ItemStackId.Value
                && FoodQuantity == foodBefore && ParentCount == parentBefore,
                "A08 Eating phase consumed food or changed the operation identity early.");
            Require(ports.TryRebindMealQuantityLease(command.OperationId, out string reservedLeaseId, out ItemStackId reservedStackId)
                && ports.TryResolveMealQuantityStack(reservedLeaseId, out ItemStackId resolvedStackId)
                && resolvedStackId.Equals(reservedStackId),
                "A08 Eating phase has no authoritative reserved serving.");
            var reservedServing = items.GetAllStacks().Single(value => value.StackId == reservedStackId.Value);
            Require(reservations.Revalidate(reservedLeaseId, out ItemQuantityLease reservedLease, out _)
                && reservedLease.ownerOperationId == operationId && reservedLease.ownerCharacterId == ActorId
                && reservedLease.purpose == ItemReservationPurpose.Meal && reservedLease.remainingQuantity == 1
                && reservedLease.slices.Where(value => value != null && value.quantity > 0).Sum(value => value.quantity) == 1
                && reservedLease.slices.Where(value => value != null && value.quantity > 0)
                    .All(value => value.stackId == reservedStackId.Value)
                && reservedServing.ItemId == FoodId && reservedServing.Quantity >= reservedLease.remainingQuantity,
                "A08 reservation does not own exactly one available serving of the selected food.");
            // Save while Eating, then rebind the production owners before completion.
            RoundTripNarrative();
            var restoredPlan = consumables.Capture().activeMealPlans.Single();
            Require(restoredPlan.planId == operationId && restoredPlan.sourceStackId == plan.sourceStackId
                && ports.TryRebindMealQuantityLease(command.OperationId, out string restoredLeaseId, out ItemStackId restoredStackId)
                && restoredLeaseId == reservedLeaseId
                && restoredStackId.Equals(reservedStackId),
                "A08 Eating restore changed the exact reserved physical serving.");
            clock.Advance(4.1f);
            bool testPendingConsequence = operationId == "consumable-operation:a08-break-fast";
            if (testPendingConsequence)
                Fasting.DebugRejectTransitionCommit = value => value == CharacterRitualFastTransition.Interrupted;
            consumables.Tick();
            RequireExactMealEvidence();
            if (testPendingConsequence)
            {
                Require(FoodQuantity == foodBefore - 1 && ParentCount == parentBefore + 1 && MealEvents == eventsBefore + 1
                    && consumables.Capture().activeMealPlans.Single().phase == CharacterMealPlanPhase.EffectsPublished
                    && !consumables.Capture().activeMealPlans.Single().ritualFastingConsequenceDelivered
                    && Fasting.GetStatus(Actor).Phase == CharacterRitualFastPhase.Fasting,
                    "A08 failed fasting consequence did not retain the committed physical meal for retry.");
                float appliedHunger = Actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
                RoundTripNarrative();
                consumables.Tick();
                Require(Actor.Stats.GetConditionValue(CharacterCondition.HUNGER) == appliedHunger
                    && FoodQuantity == foodBefore - 1 && ParentCount == parentBefore + 1 && MealEvents == eventsBefore + 1,
                    "A08 saved consequence retry repeated a real meal effect.");
            }
            var completed = consumables.Capture().completedOperations.Single(value => value.operationId == operationId);
            float hungerAfter = Actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
            bool resultAvailable = consumables.TryGetMealOperationResult(command.OperationId, out var result);
            float expectedHunger = Mathf.Clamp(hungerBefore + nutrition, 0f, HungerMaximum);
            JObject checks = new()
            {
                ["successfulResult"] = resultAvailable && result.Success,
                ["characterIdentity"] = completed.characterId == ActorId,
                ["foodIdentity"] = completed.itemDefinitionId == FoodId,
                // The committed owner and result must identify the exact source
                // slice held by the physical reservation, not a guessed new stack.
                ["committedStackIdentity"] = completed.itemStackId == reservedStackId.Value,
                ["resultStackIdentity"] = result.ItemStackId.Equals(reservedStackId),
                ["facilityIdentity"] = completed.facilityInstanceId == command.FacilityId.Value,
                ["resultKeyIdentity"] = !string.IsNullOrWhiteSpace(completed.outcomeResultKey)
                    && result.OutcomeResultKey == completed.outcomeResultKey,
                ["oneServingConsumed"] = FoodQuantity == foodBefore - 1,
                ["oneParentCommitted"] = ParentCount == parentBefore + 1,
                ["oneMealEvent"] = MealEvents == eventsBefore + 1,
                ["exactHungerRecovery"] = Mathf.Abs(hungerAfter - expectedHunger) < .0001f,
                ["noActivePlan"] = consumables.Capture().activeMealPlans.Count == 0,
                ["noPendingPhysicalCommit"] = !batch.TryGetPending(operationId, out _)
            };
            JObject observations = new()
            {
                ["operationId"] = operationId, ["commandStackId"] = command.ItemStackId.Value,
                ["reservedStackId"] = reservedStackId.Value, ["completedStackId"] = completed.itemStackId,
                ["originalCommandStackMatched"] = completed.itemStackId == command.ItemStackId.Value,
                ["foodBefore"] = foodBefore, ["foodAfter"] = FoodQuantity,
                ["expectedHunger"] = expectedHunger, ["hungerBefore"] = hungerBefore, ["hungerAfter"] = hungerAfter,
                ["parentDelta"] = ParentCount - parentBefore, ["eventDelta"] = MealEvents - eventsBefore,
                ["checks"] = checks
            };
            Debug.Log("A08 meal commit observations: " + observations.ToString(Formatting.None));
            Require(checks.Properties().All(value => value.Value.Value<bool>()),
                "A08 real meal commit mismatch: " + observations.ToString(Formatting.None));
            string beforeReplay = MealState();
            string ledgerBeforeReplay = JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes());
            string identityBeforeReplay = JsonConvert.SerializeObject(Store.Capture());
            consumables.TryConsumeMeal(command, out _);
            consumables.Tick();
            Require(MealState() == beforeReplay && identityBeforeReplay == JsonConvert.SerializeObject(Store.Capture())
                && ledgerBeforeReplay == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()),
                "A08 terminal meal replay mutated its real owners.");
            RoundTripNarrative();
            string beforeRestoredReplay = MealState();
            string restoredLedger = JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes());
            string restoredIdentity = JsonConvert.SerializeObject(Store.Capture());
            consumables.TryConsumeMeal(command, out _);
            consumables.Tick();
            Require(MealState() == beforeRestoredReplay && restoredIdentity == JsonConvert.SerializeObject(Store.Capture())
                && restoredLedger == JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()),
                "A08 restored terminal meal replay duplicated physical/effect/fasting publication.");
            return new JObject { ["operationId"] = operationId, ["resultKey"] = completed.outcomeResultKey,
                ["sourceStackId"] = reservedStackId.Value, ["commandStackId"] = command.ItemStackId.Value,
                ["commitObservations"] = observations, ["foodBefore"] = foodBefore, ["foodAfter"] = FoodQuantity,
                ["nutrition"] = nutrition, ["hungerBefore"] = hungerBefore, ["hungerAfter"] = hungerAfter,
                ["parentDelta"] = ParentCount - parentBefore, ["eventDelta"] = MealEvents - eventsBefore,
                ["completedOwner"] = JToken.FromObject(completed),
                ["ledgerOutcomes"] = JToken.Parse(JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes())),
                ["mealReplayMutationCount"] = 0 };
        }

        public void Dispose()
        {
            mealSubscription.Dispose();
            consumptionFacts?.Dispose();
            restFacts?.Dispose();
            fastingFacts?.Dispose();
            var registry = CharacterAiEditorTestDependencies.WorldRegistry;
            registry.UnregisterBuilding(Facility);
            registry.UnregisterCharacter(Actor);
            registry.UnregisterCharacterLifetime(Actor);
            items.Dispose();
            UnityEngine.Object.DestroyImmediate(Facility.gameObject);
            UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            UnityEngine.Object.DestroyImmediate(buildingData);
            UnityEngine.Object.DestroyImmediate(actorData);
        }
    }

    private sealed class MutableClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Advance(float seconds) { DeltaTime = seconds; Time += seconds; FrameCount++; }
    }
    private sealed class EmptyEffects : ICharacterEquipmentGameplayEffectSourceQuery, ICharacterTransientGameplayEffectSourceQuery
    {
        public IReadOnlyList<IGameplayEffectSource> GetEquipmentSources(CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
        public IReadOnlyList<IGameplayEffectSource> GetStatusSources(CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
        public IReadOnlyList<IGameplayEffectSource> GetCompletedResearchSources(CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
    }
    private sealed class NoStaffDiscontent : IStaffDiscontentRuntimeService
    {
        public float GetWorkEfficiencyMultiplier(CharacterActor staff) => 1f;
        public bool ShouldBlockWork(CharacterActor staff, out string reason) { reason = string.Empty; return false; }
        public bool IsRebellionTarget(CharacterActor target) => false;
        public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender) => false;
    }
    private sealed class NoMetaProgression : IMetaProgressionRuntimeReader
    {
        public int GetStartingFacilityCandidateBonus() => 0;
        public int GetStartingOwnerTraitCandidateBonus() => 0;
        public float GetOwnerMaxHealthMultiplier() => 1f;
        public float GetInvasionWarningThresholdMultiplier() => 1f;
        public float GetCommerceStockCostMultiplier(StockCategory category) => 1f;
        public float GetFortressFacilityCostMultiplier(BuildingSO building) => 1f;
        public float GetArcaneResearchWorkMultiplier() => 1f;
        public bool IsRecipePreserved(string recipeId) => false;
        public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings) => Array.Empty<int>();
    }
    private sealed class NoCombat : ICharacterCombatStanceQuery
    {
        public bool IsInCombatStance(CharacterActor actor) => false;
    }
    private sealed class NoTareExpected : IPackagedLotTareOutputGateway
    {
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => Array.Empty<WorldItemStackSnapshot>();
        public bool SpawnOutput(string id, int quantity, Vector2Int position, IReadOnlyList<ItemInstanceComponentSaveData> components, out int spawned) =>
            throw new InvalidOperationException("A08 unpackaged fixture unexpectedly requested tare output.");
    }

    private static void RequireSeparatedStateKeys(
        CharacterIdentityStateStore store,
        CharacterTraitSO trait,
        bool awaitingPostFastMeal)
    {
        string traitId = trait.DefinitionId.Value;
        CharacterIdentityRuleStateSaveData fastSaved = null;
        CharacterIdentityRuleStateSaveData moodSaved = null;
        Require(store.TryGet(
                ActorId,
                traitId,
                CharacterRitualFastingRuntime.StateRuleId,
                out fastSaved)
            && store.TryGet(
                ActorId,
                traitId,
                CharacterRitualFastingRuntime.NeedId,
                out moodSaved),
            "Ritual-fast phase and mood need do not have separate save keys.");
        CharacterRitualFastRuntimeState fast =
            JsonUtility.FromJson<CharacterRitualFastRuntimeState>(
                fastSaved.statePayload);
        CharacterPersistentNeedRuntimeState mood =
            JsonUtility.FromJson<CharacterPersistentNeedRuntimeState>(
                moodSaved.statePayload);
        Require(fast != null
            && fast.awaitingPostFastMeal == awaitingPostFastMeal
            && mood != null
            && mood.lastMoodAppliedDay == 11,
            "Separated ritual-fast state payloads are not exact.");
    }

    private static bool HasMood(
        CharacterActor actor,
        string factorId,
        float value) => actor.Stats.GetMoodSnapshot().Factors.Any(factor =>
        factor != null
        && string.Equals(factor.Id, factorId, StringComparison.Ordinal)
        && Mathf.Approximately(factor.Value, value));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void WriteReport(string output, JObject report)
    {
        string path = Path.Combine(output, "report.json");
        if (File.Exists(path))
            throw new IOException("A08 report already exists: " + path);
        using FileStream stream = new(path, FileMode.CreateNew);
        using StreamWriter writer = new(stream);
        writer.Write(report.ToString(Formatting.Indented));
    }
}
#endif
