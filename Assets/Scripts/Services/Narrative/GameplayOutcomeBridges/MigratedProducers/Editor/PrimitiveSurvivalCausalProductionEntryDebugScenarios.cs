#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal sealed class PrimitiveSurvivalModuleSelectionCapture
{
    internal PrimitiveSurvivalModuleSelectionCapture(
        string ownerId,
        string characterId,
        JObject checkpoint,
        CharacterNarrativeLedger narrativeLedger,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence)
    {
        OwnerId = ownerId;
        CharacterId = characterId;
        Checkpoint = (JObject)checkpoint.DeepClone();
        NarrativeLedger = narrativeLedger.Clone();
        Evidence = evidence.Select(value => value.Clone()).ToArray();
    }

    internal string OwnerId { get; }
    internal string CharacterId { get; }
    internal JObject Checkpoint { get; }
    internal CharacterNarrativeLedger NarrativeLedger { get; }
    internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> Evidence { get; }
}

public static partial class PrimitiveSurvivalCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:primitive-survival-rest";
    internal const string FirstRestActionId =
        "action:survival-primitive-floor-rest-first";
    internal const string RepeatRestActionId =
        "action:survival-primitive-floor-rest-repeat";

    private const string OwnerKind = "owner-kind:primitive-survival-rest";
    private const string DomainStateSchema =
        "dungeonstory-causal-primitive-survival-rest-owner-state@3";
    private const string CharacterIdValue =
        "character:causal:primitive-survival";
    private const int InitialDay = 13;
    private const int FirstDay = 14;
    private const int SecondDay = 15;
    private const int ThirdDay = 16;
    private const float InitialSleep = 0f;
    private const float InitialHygiene = 60f;
    private const float InitialExcretion = 100f;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
            throw new ArgumentException(
                "A primitive-survival checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Fixture(
                FirstRestActionId,
                context,
                context.CompleteFirstRest,
                () => context.RequireOutcome(1),
                lifetime),
            Fixture(
                RepeatRestActionId,
                context,
                context.CompleteRepeatRest,
                () => context.RequireOutcome(2),
                lifetime)
        };
    }

    internal static PrimitiveSurvivalModuleSelectionCapture
        CaptureModuleSelectionSource(string ownerId)
    {
        string canonicalOwner = CausalGameplaySyntax.RequireId(
            ownerId,
            nameof(ownerId));
        JObject checkpoint;
        using (var source = new Context(
                   "run:module-source:primitive-survival:write",
                   canonicalOwner,
                   checkpoint: null))
        {
            if (!source.CompleteFirstRest()
                || !source.CompleteRepeatRest()
                || !source.CompleteThirdRest()
                || !source.RequireOutcome(3).HasCanonicalPayloadHash)
            {
                throw new InvalidOperationException(
                    "Three actual primitive-survival outcomes were not committed.");
            }
            checkpoint = source.CaptureOwnerCheckpoint();
        }

        using var restored = new Context(
            "run:module-source:primitive-survival:restore",
            canonicalOwner,
            checkpoint);
        if (restored.SourceStage != 3
            || CharacterAcquiredTraitExperienceScore.Require(
                restored.SourceActor.Progression.NarrativeLedger) != 3)
        {
            throw new InvalidOperationException(
                "The restored primitive-survival owner lost its three narrative results.");
        }
        IReadOnlyDictionary<string, object> state = restored.CaptureState();
        string characterId = (string)state["primitiveSurvival.characterId"];
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidence =
            restored.CaptureNarrativeEvidence(
                new GameplayEntityId(
                    MigratedProducerOutcomeIds.CharacterKind,
                    characterId),
                8);
        if (evidence.Count != 3)
        {
            throw new InvalidOperationException(
                "The restored primitive-survival owner did not expose three exact facts.");
        }
        return new PrimitiveSurvivalModuleSelectionCapture(
            canonicalOwner,
            characterId,
            checkpoint,
            restored.SourceActor.Progression.NarrativeLedger,
            evidence);
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue = "primitive-survival:rest:focused";
        JObject firstCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-primitive-survival:first",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstRestActionId);
            Require((int)first.CaptureState()["primitiveSurvival.stage"] == 0,
                "Primitive-survival owner did not start before its first rest.");
            Require(first.Execute(), "The first real primitive rest failed.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first primitive-rest outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require((int)after["primitiveSurvival.stage"] == 1
                    && Approximately(ReadFloat(
                        after["primitiveSurvival.sleep"]), 55f)
                    && Approximately(ReadFloat(
                        after["primitiveSurvival.hygiene"]), 56f)
                    && Convert.ToInt64(
                        after["primitiveSurvival.outcomeOwnerRevision"],
                        CultureInfo.InvariantCulture) == 1L,
                "The first primitive rest did not persist its real effects.");
            firstCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-primitive-survival:repeat",
                   OwnerIdValue,
                   firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstRestActionId);
            IProductionCausalActionFixture repeat = fixtures.Get(
                RepeatRestActionId);
            Require(!first.Execute(),
                "Fresh composition replayed the first primitive rest.");
            Require(repeat.Execute(),
                "The restored owner did not complete the next-day rest.");
            Require(repeat.RequireExactOutcome().HasCanonicalPayloadHash,
                "The repeated primitive-rest outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = repeat.CaptureState();
            Require((int)after["primitiveSurvival.stage"] == 2
                    && Approximately(ReadFloat(
                        after["primitiveSurvival.sleep"]), 100f)
                    && Approximately(ReadFloat(
                        after["primitiveSurvival.hygiene"]), 52f)
                    && Convert.ToInt64(
                        after["primitiveSurvival.outcomeOwnerRevision"],
                        CultureInfo.InvariantCulture) == 2L,
                "The next-day primitive rest did not preserve prior body state.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-primitive-survival:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstRestActionId);
            IProductionCausalActionFixture repeat = fixtures.Get(
                RepeatRestActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
            Require(!first.Execute() && !repeat.Execute(),
                "Terminal primitive-survival owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected primitive-survival replay mutated the checkpoint.");
        }

        JObject statTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)statTamper["domainState"])["actorStats"]![
            "conditions"]!.First(value => string.Equals(
                (string)value!["condition"],
                CharacterCondition.SLEEP.ToString(),
                StringComparison.Ordinal))!["value"] = 0f;
        Require(RejectCheckpoint(OwnerIdValue, statTamper, "stat-tamper"),
            "A primitive-survival checkpoint with false sleep was accepted.");

        JObject ownerTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)ownerTamper["domainState"])["outcomeOwners"]![
            "owners"]![0]!["revision"] = 2L;
        Require(RejectCheckpoint(OwnerIdValue, ownerTamper, "owner-tamper"),
            "A primitive-survival checkpoint with a false owner revision was accepted.");

        JObject originTamper = (JObject)firstCheckpoint.DeepClone();
        JObject moodFact = (JObject)originTamper["domainState"]!["narrativeLedger"]!["facts"]![0]!;
        moodFact["lastEventContext"]!["eventInstanceId"] = "run:unrelated/1";
        moodFact["lastEventContext"]!["chainId"] = "run:unrelated/1";
        Require(RejectCheckpoint(OwnerIdValue, originTamper, "experience-origin-tamper"),
            "A primitive experience referencing an unrelated result was accepted.");

        JObject countTamper = (JObject)firstCheckpoint.DeepClone();
        JObject counted = (JObject)countTamper["domainState"]!["narrativeLedger"]!["facts"]![0]!;
        counted["count"] = 2;
        counted["totalValue"] = -6f;
        Require(RejectCheckpoint(OwnerIdValue, countTamper, "experience-count-tamper"),
            "A primitive experience claiming an extra rest was accepted.");

        if (log)
        {
            Debug.Log(
                "Primitive floor rest -> restored next-day floor rest causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Primitive-survival causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunB0RollbackBatch()
    {
        foreach (MigratedProducerOutcomeFaultMode fault in new[]
                 {
                     MigratedProducerOutcomeFaultMode.RejectReservation,
                     MigratedProducerOutcomeFaultMode.RejectCommit
                 })
        {
            using var rejectedChild = new Context(
                "run:b0-a02-rest-child-" + fault,
                ownerId: null,
                checkpoint: null);
            rejectedChild.VerifyFloorRestChildFailureIsAtomic(fault);
        }
        using (var floorRest = new Context(
                   "run:b0-a02-floor-rest-rollback",
                   ownerId: null,
                   checkpoint: null))
        {
            floorRest.VerifyFloorRestPendingRecovery();
        }
        using (var retiredFloorRest = new Context(
                   "run:b0-a02-floor-rest-retired-recovery",
                   ownerId: null,
                   checkpoint: null))
        {
            retiredFloorRest.VerifyRetiredFloorRestPendingRecovery();
        }
        using (var latrine = new Context(
                   "run:b0-a02-latrine-rollback",
                   ownerId: null,
                   checkpoint: null))
        {
            latrine.VerifyLatrinePendingRecovery();
        }
        Debug.Log("B0 A02 primitive survival pending-recovery scenarios PASS");
    }

    public static void RunCompletionPublicationBatch()
    {
        var failures = new List<Exception>();
        void Run(string label, Action<Context> verify)
        {
            try
            {
                using var context = new Context("run:primitive-completion:" + label,
                    "primitive-completion-owner:" + label, null);
                verify(context);
                Debug.Log("PRIMITIVE_COMPLETION_CASE=" + label + "; PASS");
            }
            catch (Exception exception)
            {
                failures.Add(new InvalidOperationException(label + ": " + exception.Message, exception));
                Debug.Log("PRIMITIVE_COMPLETION_CASE=" + label + "; FAIL; " + exception);
            }
        }
        Run("repeated", context => context.VerifyRepeatedExperienceGroups());
        Run("mood-observer", context => context.VerifyCompletionSurvivesMoodObserverFailure());
        Run("stats-observer", context => context.VerifyCompletionSurvivesMoodObserverFailure("stats"));
        Run("invalidation-observer", context => context.VerifyCompletionSurvivesMoodObserverFailure("invalidation"));
        Run("causal-owner", _ => Require(RunCausalOwnerFamily(log: true),
            "The restored primitive owner failed."));
        Run("module-source", _ =>
        {
            PrimitiveSurvivalModuleSelectionCapture capture = CaptureModuleSelectionSource(
                "primitive-completion-owner:module-source:captured");
            Require(capture.Evidence.Count == 3
                    && CharacterAcquiredTraitExperienceScore.Require(capture.NarrativeLedger) == 3,
                "The three real rests did not preserve their exact evidence and stable experience score.");
        });
        if (failures.Count > 0)
            throw new AggregateException("Primitive completion publication cases failed.", failures);
        Debug.Log("PRIMITIVE_COMPLETION_PUBLICATION=PASS");
    }

    public static void RunCompletionNotificationBatch()
    {
        var failures = new List<Exception>();
        foreach (string channel in new[] { "none", "mood", "stats", "invalidation", "activity", "event" })
        {
            try
            {
                using var context = new Context("run:primitive-notification:" + channel,
                    "primitive-notification-owner:" + channel, null);
                context.VerifyCompletionNotificationIsolation(channel);
                Debug.Log("PRIMITIVE_NOTIFICATION_CASE=" + channel + "; PASS");
            }
            catch (Exception failure)
            {
                failures.Add(new InvalidOperationException(channel + ": " + failure.Message, failure));
                Debug.Log("PRIMITIVE_NOTIFICATION_CASE=" + channel + "; FAIL; " + failure);
            }
        }
        if (failures.Count > 0)
            throw new AggregateException("Primitive completion notification cases failed.", failures);
        Debug.Log("PRIMITIVE_COMPLETION_NOTIFICATION=PASS; production-entry; not-natural-AI-or-full-world-restore");
    }

    public static void RunMoodImmunityExperienceBatch()
    {
        using var context = new Context("run:primitive-mood-immunity",
            "primitive-mood-immunity-owner", null, moodImmune: true);
        context.VerifyMoodImmunityExperience();
        Debug.Log("PRIMITIVE_MOOD_IMMUNITY=PASS; transient-trait-fixture; production-entry; not-natural-AI-or-full-world-restore");
    }

    public static void RunMoodImmunityRegressionBatch()
    {
        var failures = new List<Exception>();
        void Run(string label, Action verify)
        {
            try
            {
                verify();
                Debug.Log("PRIMITIVE_IMMUNITY_REGRESSION_CASE=" + label + "; PASS");
            }
            catch (Exception failure)
            {
                failures.Add(new InvalidOperationException(label + ": " + failure.Message, failure));
                Debug.Log("PRIMITIVE_IMMUNITY_REGRESSION_CASE=" + label + "; FAIL; " + failure);
            }
        }
        Run("immunity", RunMoodImmunityExperienceBatch);
        Run("publication", RunCompletionPublicationBatch);
        Run("notification", RunCompletionNotificationBatch);
        Run("pending", RunB0RollbackBatch);
        Run("start-lifetime", RunStartLifetimeBatch);
        if (failures.Count > 0)
            throw new AggregateException("Primitive mood immunity and regression cases failed.", failures);
        Debug.Log("PRIMITIVE_IMMUNITY_REGRESSION=PASS");
    }

    public static void RunStartLifetimeBatch()
    {
        foreach (string channel in new[] { "started-event", "changed-event", "scheduler", "replacement" })
        {
            var scheduler = new HoldingCoroutineScheduler();
            using var context = new Context("run:primitive-start:" + channel,
                "primitive-start-owner:" + channel, null, coroutineScheduler: scheduler);
            if (channel == "replacement") context.VerifyReplacementLifetime(scheduler);
            else context.VerifyStartFailureLifetime(channel, scheduler);
            Debug.Log("PRIMITIVE_START_LIFETIME_CASE=" + channel + "; PASS");
        }
        Debug.Log("PRIMITIVE_START_LIFETIME=PASS; actual actor/Brain; held scheduler; no physical effects");
    }

    private static IProductionCausalActionFixture Fixture(
        string actionId,
        Context context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            "survival",
            MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind
                    .CharacterPrimitiveSurvivalCompletedEvent).OutcomeTypeId,
            context.CaptureState,
            execute,
            requireOutcome,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.PersistentOwnerId,
            context.PersistentOwnerKind,
            context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
            context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
            context.HasPersistentOwner ? context : null,
            captureNarrativeEvidence: context.CaptureNarrativeEvidence);

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-primitive-survival:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    internal sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly string characterId;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly DomainFactDispatcher traitInferenceFacts;
        private readonly IDisposable traitInferenceBinding;
        private readonly CharacterTraitInferencePublicationRuntime traitInferences;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly CharacterPrimitiveSurvivalRunner runner;
        private readonly GameEventBus events = new();
        private readonly CharacterDeprivationStateStore stateStore;
        private readonly CharacterBreakdownWorld world;
        private readonly RecordingWorldFilthQuery filth;
        private readonly EditorFixedGameCalendar calendar;
        private readonly DrainingGameClock clock;
        private readonly DomainFactDispatcher primitiveFacts;
        private readonly PrimitiveSurvivalActivityObserver activityObserver;
        private readonly int originalCalendarDay;
        private readonly int originalCalendarHour;
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;
        private readonly bool moodImmune;
        private DomainFactDispatcher restFacts;
        private CharacterTraitSO immunityTrait;
        private CharacterTraitSO authoredImmunityTrait;
        private string authoredImmunityTraitJson;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint, bool moodImmune = false,
            ICharacterPrimitiveSurvivalCoroutineScheduler coroutineScheduler = null)
        {
            this.moodImmune = moodImmune;
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            characterId = HasPersistentOwner
                ? "character:causal:primitive-survival:"
                    + ProductionCausalOutcomeEvidence.Sha256Text(this.ownerId)
                        .Substring("sha256:".Length, 24)
                : CharacterIdValue;
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":primitive-survival-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore,
                includeAcquiredTraitInference: true);
            traitInferenceFacts = new DomainFactDispatcher();
            traitInferenceBinding = CharacterTraitInferredFactMapping.CreateBinding(
                    outcomes.Registry,
                    outcomes.Ledger,
                    outcomes.Recorder,
                    new GameplayDomainFactCommitGroup(
                        outcomes.Recorder,
                        outcomes.Ledger))
                .Connect(traitInferenceFacts);
            traitInferences = new CharacterTraitInferencePublicationRuntime(
                traitInferenceFacts,
                traitInferenceFacts);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            primitiveFacts = outcomes.CreatePrimitiveSurvivalFacts(probe);

            var grid = new Grid(3, 1);
            for (int x = 0; x < grid.width; x++)
            {
                grid.SetAreaType(
                    new Vector2Int(x, 0),
                    GridCellAreaType.ExteriorPath);
            }
            var gridProvider = new FixedGridSystemProvider(grid);
            IWorldItemStackRuntime itemStacks =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    gridProvider,
                    out _,
                    out _,
                    out ItemQuantityReservationService quantityReservations,
                    out IReservedItemTransferService reservedTransfers);
            stateStore = new CharacterDeprivationStateStore(
                aggregateRootStore);
            clock = new DrainingGameClock();
            calendar = EditorFixedGameCalendar.Instance;
            originalCalendarDay = calendar.Day;
            originalCalendarHour = calendar.Hour;
            calendar.SetDateTime(InitialDay, 0);
            filth = new RecordingWorldFilthQuery();
            world = new CharacterBreakdownWorld(
                    new DungeonStory.Foundation.UnityUiClock(),
                    gridProvider,
                    itemStacks,
                    filth,
                    EmptyWorldWaterQuery.Instance,
                    new RoomLayoutCache(),
                    CharacterAiEditorTestDependencies.WorldRegistry);
            runner = new CharacterPrimitiveSurvivalRunner(
                world,
                new CharacterEmergencyMovement(gridProvider, stateStore),
                stateStore,
                clock,
                events,
                new CharacterPrimitiveSurvivalDependencies(
                    EmptyFieldMealConsumption.Instance,
                    quantityReservations,
                    reservedTransfers,
                    calendar,
                    primitiveFacts,
                    coroutineScheduler ?? new DrainingCoroutineScheduler(clock)));

            GameObject actorObject = new("Causal Primitive Survival Owner");
            actorObject.SetActive(false);
            actorObject.AddComponent<SpriteRenderer>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AIBrain>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Brain.availableActions =
                AiDebugScenarioActionFactory.CreateCustomerActions();
            actor.Brain.ConstructCharacterAbility(gridProvider);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "맨바닥 생존자",
                "Orc",
                CharacterRole.Regular);
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(characterId);
            actor.stats[CharacterCondition.SLEEP] = InitialSleep;
            actor.stats[CharacterCondition.HYGIENE] = InitialHygiene;
            actor.stats[CharacterCondition.EXCRETION] = InitialExcretion;
            activityObserver = new PrimitiveSurvivalActivityObserver(events);
            activityObserver.Start();

            try
            {
                if (moodImmune)
                    ConfigureMoodImmunity();
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                probe.ClearObservations();
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
        internal string PersistentOwnerKind =>
            HasPersistentOwner ? OwnerKind : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            CharacterMoodSnapshot mood = actor.Mood;
            CharacterMoodFactorSnapshot restMood = mood.Factors
                .SingleOrDefault(value => value != null
                    && value.Kind == CharacterMoodFactorKind.Interaction
                    && string.Equals(
                        value.Id,
                        "survival:floor-rest",
                        StringComparison.Ordinal));
            return new Dictionary<string, object>
            {
                ["primitiveSurvival.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "primitive-survival:ephemeral",
                ["primitiveSurvival.stage"] = Stage,
                ["primitiveSurvival.characterId"] = characterId,
                ["primitiveSurvival.calendarDay"] = calendar.Day,
                ["primitiveSurvival.sleep"] = actor.Stats.GetConditionValue(
                    CharacterCondition.SLEEP,
                    0f),
                ["primitiveSurvival.hygiene"] = actor.Stats.GetConditionValue(
                    CharacterCondition.HYGIENE,
                    0f),
                ["primitiveSurvival.excretion"] = actor.Stats.GetConditionValue(
                    CharacterCondition.EXCRETION,
                    0f),
                ["primitiveSurvival.baseMood"] = mood.BaseValue,
                ["primitiveSurvival.moodFactorCount"] = mood.Factors.Count(
                    value => value != null
                        && value.Kind == CharacterMoodFactorKind.Interaction),
                ["primitiveSurvival.floorRestMood"] = restMood?.Value ?? 0f,
                ["primitiveSurvival.outcomeOwnerRevision"] =
                    OutcomeOwnerRevision(),
                ["primitiveSurvival.outcomeCount"] = OutcomeCount(),
                ["primitiveSurvival.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool CompleteFirstRest() => CompleteRest(0, FirstDay);
        internal bool CompleteRepeatRest() => CompleteRest(1, SecondDay);
        internal bool CompleteThirdRest() => CompleteRest(2, ThirdDay);

        private void ConfigureMoodImmunity()
        {
            var source = CharacterAiEditorTestDependencies.ContentDefinitions as IGameContentCatalog
                ?? throw new InvalidOperationException("The immunity fixture requires the real content catalog.");
            authoredImmunityTrait = source.GetAll<CharacterTraitSO>().Single(value => value.id == 239);
            authoredImmunityTraitJson = JsonUtility.ToJson(authoredImmunityTrait);
            immunityTrait = UnityEngine.Object.Instantiate(authoredImmunityTrait);
            immunityTrait.hideFlags = HideFlags.HideAndDontSave;
            immunityTrait.identityRules = new List<CharacterIdentityRule>
            {
                new MoodImmunityRule
                { ruleId = "qa:primitive-floor-rest-immunity", eventId = "survival:floor-rest" }
            };
            var catalog = new MoodImmunityCatalog(source, authoredImmunityTrait, immunityTrait);
            actor.Progression.ConstructCharacterProgression(
                new EditorCharacterSkillGenerationService(),
                new ResourceCharacterSkillSystemSettingsProvider(catalog),
                CharacterAiEditorTestDependencies.GameEvents,
                new CharacterProgressionProfileProjector(catalog, new CharacterRuntimeProfileFactory(catalog)));
            actor.Progression.ApplyPreparedIdentity(
                actor.Progression.GrowthState.displayName,
                actor.Progression.GrowthState.origin,
                new[] { 239 }, CharacterPotentialGrade.Ordinary, 239,
                autoChooseDrafts: false, ensureInitialDrafts: false);
            Require(ReferenceEquals(actor.Progression.ResolveSelectedTraits().Single(), immunityTrait)
                && JsonUtility.ToJson(authoredImmunityTrait) == authoredImmunityTraitJson,
                "The mood immunity fixture changed shared content or failed to bind its private trait.");
        }

        internal void VerifyMoodImmunityExperience()
        {
            Require(moodImmune, "The immunity probe requires its explicit private fixture.");
            int completedEvents = 0;
            CharacterLog activityLog = actor.GetComponent<CharacterLog>();
            using IDisposable subscription = events.Subscribe<CharacterPrimitiveSurvivalCompletedEvent>(value =>
            {
                Require(value.CharacterId.Value == characterId && value.ActionId == "survival:floor-rest"
                    && Approximately(value.NeedRecovery, 55f) && value.PhysicalItemCount == 0,
                    "The immune completion event payload changed.");
                completedEvents++;
            });
            for (int index = 0; index < 3; index++)
            {
                calendar.SetDateTime(FirstDay + index, 0);
                Require(runner.TryStart(actor, CharacterPrimitiveSurvivalActionKind.FloorRest, out _)
                    && !runner.IsRunning((CharacterId)characterId), "The immune real rest did not finish.");
                GameplayOutcomeReplayIdentity committed = RequireOutcome(index + 1);
                var narrative = actor.Progression.NarrativeLedger;
                int completedActivities = activityLog.ActivityEntries.Count(value =>
                    value.ActionId == "survival:floor-rest" && value.OutcomeId == CharacterActivityOutcomes.Completed);
                var witness = new JObject
                {
                    ["stage"] = index + 1, ["outcomeId"] = committed.OutcomeId.ToString(),
                    ["outcomes"] = OutcomeCount(), ["ownerRevision"] = OutcomeOwnerRevision(),
                    ["completedEvents"] = completedEvents,
                    ["completedActivities"] = completedActivities,
                    ["sleep"] = actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f),
                    ["hygiene"] = actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f),
                    ["moodFactors"] = actor.Mood.Factors.Count(value => value.Id == "survival:floor-rest"),
                    ["experienceScore"] = CharacterAcquiredTraitExperienceScore.Require(narrative),
                    ["facts"] = JArray.FromObject(narrative.Facts)
                };
                Debug.Log("PRIMITIVE_IMMUNITY_STAGE=" + witness.ToString(Formatting.None));
                Require(OutcomeCount() == index + 1 && OutcomeOwnerRevision() == index + 1
                    && completedEvents == index + 1 && completedActivities == index + 1
                    && !stateStore.TryGetPrimitiveSurvivalPending((CharacterId)characterId, out _)
                    && Approximately(actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f), index == 0 ? 55f : 100f)
                    && Approximately(actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f), 56f - 4f * index)
                    && !actor.Mood.Factors.Any(value => value.Id == "survival:floor-rest")
                    && !narrative.Facts.Any(value => value.domain == CharacterNarrativeDomain.Mood
                        && value.factId == "survival:floor-rest"),
                    "The real immune policy or body/parent/notification boundary failed: " + witness.ToString(Formatting.None));
            }
            // Observe all three production completions before checking stable groups.
            // A per-event fact bug must be visible as growth, not only an ID mismatch.
            Require(JsonUtility.ToJson(authoredImmunityTrait) == authoredImmunityTraitJson,
                "The immunity probe mutated its authored trait.");
            Require(HasExpectedRestExperience(3)
                && actor.Progression.NarrativeLedger.Facts.Count == 2
                && CharacterAcquiredTraitExperienceScore.Require(actor.Progression.NarrativeLedger) == 3,
                "Immune rest did not retain one stable Survival experience group plus its satisfied Need group.");
            var bindings = CaptureNarrativeEvidence(new GameplayEntityId(
                MigratedProducerOutcomeIds.CharacterKind, characterId), 8);
            Require(GameplayOutcomeEvidenceFormulaProjection.GetRemainingLegacyMilestoneCounts(
                    actor.Progression.NarrativeLedger.Facts, bindings).Count == 0,
                "Primitive business occurrences were counted again outside their exact ledger evidence.");
            JObject checkpoint = CaptureOwnerCheckpoint();
            Require(!runner.TryResumePending(actor, out _) && !runner.TryResumePending(actor, out _)
                && completedEvents == 3 && JToken.DeepEquals(checkpoint, CaptureOwnerCheckpoint()),
                "The completed immune action replayed live body, evidence or events.");
            using var restored = new Context("run:primitive-mood-immunity-restored", ownerId, checkpoint, moodImmune: true);
            JObject restoredBefore = restored.CaptureOwnerCheckpoint();
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(checkpoint, restoredBefore);
            int restoredEvents = 0;
            using IDisposable restoredSubscription = restored.events.Subscribe<CharacterPrimitiveSurvivalCompletedEvent>(_ => restoredEvents++);
            Require(!restored.runner.TryResumePending(restored.actor, out _)
                && !restored.runner.TryResumePending(restored.actor, out _)
                && restoredEvents == 0 && JToken.DeepEquals(restoredBefore, restored.CaptureOwnerCheckpoint())
                && restored.HasExpectedRestExperience(3)
                && CharacterAcquiredTraitExperienceScore.Require(restored.actor.Progression.NarrativeLedger) == 3,
                "The fresh immune owner lost stable experience or replayed its completion.");
            Debug.Log("PRIMITIVE_IMMUNITY_RESUME=PASS; freshOwner=true; realResumeCalls=4; exactResults=3; experienceScore=3; replayUnchanged=true");
        }

        internal void VerifyCompletionNotificationIsolation(string faultChannel)
        {
            const string actionId = "survival:floor-rest";
            string fault = "primitive-completed-channel-fault:" + faultChannel;
            var delivered = new Dictionary<string, int>
            { ["mood"] = 0, ["stats"] = 0, ["invalidation"] = 0, ["activity"] = 0, ["event"] = 0 };
            int faultCount = 0;
            void Observe(string channel)
            {
                GameplayResultKey key = probe.LatestCommittedKey(
                    MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent);
                if (!key.IsValid || !outcomes.Ledger.TryGetResultIdentity(key, out var identity)
                    || identity.State != GameplayOutcomeReplayState.PublishedAcknowledged
                    || !identity.HasCanonicalPayloadHash
                    || !actor.Mood.Factors.Any(value => value.Id == actionId)) return;
                delivered[channel]++;
                if (channel == faultChannel && faultCount++ == 0)
                    throw new InvalidOperationException(fault);
            }
            Action<CharacterMoodSnapshot> mood = _ => Observe("mood");
            Action<IReadOnlyDictionary<CharacterCondition, float>> stats = _ => Observe("stats");
            Action invalidation = () => Observe("invalidation");
            var log = actor.GetComponent<CharacterLog>();
            Action<CharacterLogEntry> activity = entry =>
            {
                if (entry.Activity?.ActionId == actionId
                    && entry.Activity.OutcomeId == CharacterActivityOutcomes.Completed) Observe("activity");
            };
            using IDisposable subscription = events.Subscribe<CharacterPrimitiveSurvivalCompletedEvent>(value =>
            {
                Require(value.CharacterId.Value == characterId && value.ActionId == actionId
                    && Approximately(value.NeedRecovery, PrimitiveSurvivalBalanceAuthority.FloorRestRecovery)
                    && value.PhysicalItemCount == 0, "Primitive completion event payload changed.");
                Observe("event");
            });
            actor.Stats.OnMoodChange += mood;
            actor.Stats.OnStatChange += stats;
            actor.Stats.OnStatsInvalidated += invalidation;
            log.OnLogAdded += activity;
            Exception observed = null;
            try
            {
                calendar.SetDateTime(FirstDay, 0);
                Require(runner.TryStart(actor, CharacterPrimitiveSurvivalActionKind.FloorRest, out _),
                    "Actual primitive command was not admitted.");
            }
            catch (Exception failure) { observed = failure; }
            finally
            {
                actor.Stats.OnMoodChange -= mood;
                actor.Stats.OnStatChange -= stats;
                actor.Stats.OnStatsInvalidated -= invalidation;
                log.OnLogAdded -= activity;
            }
            GameplayOutcomeReplayIdentity committed = RequireOutcome(1);
            bool bodyExact = Approximately(actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f), 55f)
                && Approximately(actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f), 56f);
            bool pending = stateStore.TryGetPrimitiveSurvivalPending((CharacterId)characterId, out _);
            bool activityStored = log.ActivityEntries.Count(value => value.ActionId == actionId
                && value.OutcomeId == CharacterActivityOutcomes.Completed) == 1;
            Exception[] errors = observed is AggregateException aggregate
                ? aggregate.Flatten().InnerExceptions.ToArray()
                : observed == null ? Array.Empty<Exception>() : new[] { observed };
            bool originalFailureExact = faultChannel == "none" ? errors.Length == 0 && faultCount == 0
                : errors.Length == 1 && errors[0].Message == fault && faultCount == 1;
            var witness = new JObject
            {
                ["faultChannel"] = faultChannel, ["faultCount"] = faultCount,
                ["delivered"] = JObject.FromObject(delivered), ["activityStored"] = activityStored,
                ["bodyExact"] = bodyExact, ["pending"] = pending,
                ["running"] = runner.IsRunning((CharacterId)characterId),
                ["outcomeId"] = committed.OutcomeId.ToString(), ["outcomes"] = OutcomeCount(),
                ["ownerRevision"] = OutcomeOwnerRevision(), ["experienceExact"] = HasExpectedRestExperience(1),
                ["originalFailureExact"] = originalFailureExact, ["exception"] = observed?.ToString() ?? string.Empty
            };
            Debug.Log("PRIMITIVE_NOTIFICATION_BOUNDARY=" + witness.ToString(Formatting.None));
            // Exercise the real resume command, not the fixture's stage guard.
            JObject checkpoint = CaptureOwnerCheckpoint();
            string deliveredBefore = JsonConvert.SerializeObject(delivered);
            Require(!runner.TryResumePending(actor, out _) && !runner.TryResumePending(actor, out _)
                && JToken.DeepEquals(checkpoint, CaptureOwnerCheckpoint())
                && deliveredBefore == JsonConvert.SerializeObject(delivered),
                "A completed primitive resumed effects or notifications without a pending owner.");
            using var restored = new Context("run:primitive-notification-restored:" + faultChannel, ownerId, checkpoint);
            JObject restoredBefore = restored.CaptureOwnerCheckpoint();
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(checkpoint, restoredBefore);
            int restoredEvents = 0;
            using IDisposable restoredSubscription = restored.events.Subscribe<CharacterPrimitiveSurvivalCompletedEvent>(_ => restoredEvents++);
            Require(!restored.runner.TryResumePending(restored.actor, out _)
                && !restored.runner.TryResumePending(restored.actor, out _)
                && JToken.DeepEquals(restoredBefore, restored.CaptureOwnerCheckpoint()) && restoredEvents == 0,
                "Restoring a completed primitive replayed its completion command.");
            Debug.Log("PRIMITIVE_NOTIFICATION_RESUME=" + faultChannel + "; PASS; freshOwner=true; resumeCalls=4; effectsAndEventsReplayed=0");
            Require(originalFailureExact && bodyExact && !pending && !runner.IsRunning((CharacterId)characterId)
                && OutcomeCount() == 1 && OutcomeOwnerRevision() == 1 && HasExpectedRestExperience(1)
                && activityStored && delivered.Values.All(value => value == 1),
                "A committed primitive lost an independent completion channel or its original error: " + witness.ToString(Formatting.None));
        }

        internal void VerifyRepeatedExperienceGroups()
        {
            var stages = new JArray();
            for (int index = 0; index < 3; index++)
            {
                calendar.SetDateTime(FirstDay + index, 0);
                Require(runner.TryStart(actor, CharacterPrimitiveSurvivalActionKind.FloorRest,
                            out _) && !runner.IsRunning((CharacterId)characterId),
                    "The repeated-experience probe did not complete its real rest command.");
                GameplayOutcomeReplayIdentity committed = RequireOutcome(index + 1);
                CharacterNarrativeLedger narrative = actor.Progression.NarrativeLedger;
                int score = CharacterAcquiredTraitExperienceScore.Require(narrative);
                var observed = new JObject
                {
                    ["stage"] = index + 1,
                    ["outcomeId"] = committed.OutcomeId.ToString(),
                    ["experienceScore"] = score,
                    ["facts"] = JArray.FromObject(narrative.Facts)
                };
                stages.Add(observed);
                Debug.Log("PRIMITIVE_EXPERIENCE_STAGE=" + observed.ToString(Formatting.None));
                // The second rest crosses the real satisfied-need threshold.
                // Keep that distinct experience while prohibiting per-event IDs.
                CharacterNarrativeFact mood = narrative.Facts.SingleOrDefault(value =>
                    value?.domain == CharacterNarrativeDomain.Mood && value.factId == "survival:floor-rest");
                CharacterNarrativeFact need = narrative.Facts.SingleOrDefault(value =>
                    value?.domain == CharacterNarrativeDomain.Need && value.factId == "need:sleep");
                Require(mood != null && mood.count == index + 1
                        && Approximately(mood.totalValue, -3f * (index + 1))
                        && (index == 0 ? need == null : need != null && need.count == 1
                            && Approximately(need.totalValue, 100f) && need.outcome == "satisfied")
                        && narrative.Facts.Count == (index == 0 ? 1 : 2)
                        && score == CharacterAcquiredTraitExperienceScore.RecomputeGroupScore(index + 1)
                            + (index == 0 ? 0 : 1),
                    "Repeated floor rest changed its stable Mood/Need experience groups: "
                    + observed.ToString(Formatting.None));
            }
            Require(OutcomeCount() == 3 && OutcomeOwnerRevision() == 3L
                    && Approximately(actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f), 100f)
                    && Approximately(actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f), 48f),
                "Repeated floor rest lost or duplicated its body/ledger effects.");
            Debug.Log("PRIMITIVE_EXPERIENCE_OBSERVATIONS=" + stages.ToString(Formatting.None));
        }

        internal void VerifyCompletionSurvivesMoodObserverFailure(string faultChannel = "mood")
        {
            const string fault = "primitive-post-commit-mood-observer-probe";
            int observerFailures = 0;
            var deliveries = new Dictionary<string, int>
            { ["mood"] = 0, ["stats"] = 0, ["invalidation"] = 0 };
            string faultOrigin = string.Empty;
            Require(OutcomeCount() == 0 && OutcomeOwnerRevision() == 0L
                    && !actor.Mood.Factors.Any(value => value.Id == "survival:floor-rest"),
                "The observer probe did not start before its first rest and mood effect.");
            void Observe(string channel)
            {
                if (!actor.Mood.Factors.Any(value => value.Kind == CharacterMoodFactorKind.Interaction
                        && value.Id == "survival:floor-rest"))
                    return;
                // Stats also notify while the parent reservation is active. Query
                // only the committed identity; a full save capture is forbidden there.
                GameplayResultKey key = probe.LatestCommittedKey(
                    MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent);
                if (!key.IsValid
                    || !outcomes.Ledger.TryGetResultIdentity(key, out GameplayOutcomeReplayIdentity identity)
                    || identity.State != GameplayOutcomeReplayState.PublishedAcknowledged
                    || !identity.HasCanonicalPayloadHash)
                    return;
                deliveries[channel]++;
                if (channel != faultChannel || observerFailures != 0)
                    return;
                observerFailures++;
                faultOrigin = Environment.StackTrace;
                throw new InvalidOperationException(fault);
            }
            Action<CharacterMoodSnapshot> observer = _ => Observe("mood");
            Action<IReadOnlyDictionary<CharacterCondition, float>> statsObserver = _ => Observe("stats");
            Action invalidationObserver = () => Observe("invalidation");
            Exception observed = null;
            actor.Stats.OnMoodChange += observer;
            actor.Stats.OnStatChange += statsObserver;
            actor.Stats.OnStatsInvalidated += invalidationObserver;
            try
            {
                calendar.SetDateTime(FirstDay, 0);
                _ = runner.TryStart(actor, CharacterPrimitiveSurvivalActionKind.FloorRest, out _);
            }
            catch (Exception exception)
            {
                observed = exception;
            }
            finally
            {
                actor.Stats.OnMoodChange -= observer;
                actor.Stats.OnStatChange -= statsObserver;
                actor.Stats.OnStatsInvalidated -= invalidationObserver;
            }
            var boundary = new JObject
            {
                ["observerFailures"] = observerFailures,
                ["faultChannel"] = faultChannel,
                ["deliveries"] = JObject.FromObject(deliveries),
                ["exception"] = observed?.ToString() ?? string.Empty,
                ["faultOrigin"] = faultOrigin,
                ["running"] = runner.IsRunning((CharacterId)characterId),
                ["outcomes"] = OutcomeCount(),
                ["ownerRevision"] = OutcomeOwnerRevision(),
                ["sleep"] = actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f),
                ["hygiene"] = actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f),
                ["facts"] = JArray.FromObject(actor.Progression.NarrativeLedger.Facts)
            };
            Debug.Log("PRIMITIVE_OBSERVER_BOUNDARY=" + boundary.ToString(Formatting.None));
            Require(observerFailures == 1 && observed != null
                    && observed.ToString().Contains(fault, StringComparison.Ordinal)
                    && !runner.IsRunning((CharacterId)characterId)
                    && OutcomeCount() == 1 && OutcomeOwnerRevision() == 1L
                    && Approximately(actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f), 55f)
                    && Approximately(actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f), 56f),
                "The probe did not preserve one visible post-commit observer failure and one body result: "
                + boundary.ToString(Formatting.None));
            GameplayOutcomeReplayIdentity committed = RequireOutcome(1);
            bool pending = stateStore.TryGetPrimitiveSurvivalPending((CharacterId)characterId, out _);
            int committedFacts = actor.Progression.NarrativeLedger.Facts.Count(value =>
                value?.lastEventContext != null
                && value.lastEventContext.eventInstanceId == committed.OutcomeId.ToString());
            var evidence = new JObject
            {
                ["observerFailures"] = observerFailures,
                ["outcomeId"] = committed.OutcomeId.ToString(),
                ["outcomes"] = OutcomeCount(),
                ["ownerRevision"] = OutcomeOwnerRevision(),
                ["pending"] = pending,
                ["committedNarrativeFacts"] = committedFacts,
                ["facts"] = JArray.FromObject(actor.Progression.NarrativeLedger.Facts)
            };
            Debug.Log("PRIMITIVE_POST_COMMIT_OBSERVER_OBSERVATION=" + evidence.ToString(Formatting.None));
            Require(!pending && committedFacts == 1 && HasExpectedRestExperience(1)
                    && deliveries.Values.All(value => value == 1)
                    && observed is AggregateException aggregate
                    && aggregate.Flatten().InnerExceptions.Count == 1
                    && aggregate.Flatten().InnerExceptions[0].Message == fault,
                "Post-commit publication lost exact experience, independent delivery or the original error. "
                + evidence.ToString(Formatting.None));
            JObject checkpoint = CaptureOwnerCheckpoint();
            // The event observers resolve a persistent ID through the live registry.
            // A cold owner replacement must retire the old actor before restoring it.
            Dispose();
            using var restored = new Context("run:primitive-observer:restored:" + faultChannel,
                ownerId, checkpoint);
            CharacterActor[] restoredActors = CharacterAiEditorTestDependencies.WorldRegistry.AllCharacters
                .Where(candidate => candidate != null
                    && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
                    && id.Value == characterId).ToArray();
            Require(restoredActors.Length == 1 && ReferenceEquals(restoredActors[0], restored.actor),
                "Cold primitive restore retained another live actor with the same persistent ID.");
            JObject restoredBeforeReplay = restored.CaptureOwnerCheckpoint();
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(checkpoint, restoredBeforeReplay);
            Require(!restored.CompleteFirstRest()
                    && JToken.DeepEquals(restoredBeforeReplay, restored.CaptureOwnerCheckpoint()),
                "A committed rest replay changed body, experience or its canonical result after restore.");
            Require(restored.CompleteRepeatRest() && restored.CompleteThirdRest()
                    && restored.HasExpectedRestExperience(3)
                    && CharacterAcquiredTraitExperienceScore.Require(
                        restored.actor.Progression.NarrativeLedger) == 3,
                "Restored rest publication lost stable experience or introduced occurrence-specific groups.");
            Debug.Log("PRIMITIVE_OBSERVER_RESTORE=" + faultChannel
                + "; PASS; exactResults=3; experienceScore=3; replayUnchanged=true");
        }

        internal void VerifyStartFailureLifetime(string channel, HoldingCoroutineScheduler scheduler)
        {
            var fault = new InvalidOperationException("primitive-start-fault:" + channel);
            CharacterId id = (CharacterId)characterId;
            string before = JsonConvert.SerializeObject(CaptureStats(actor));
            IDisposable subscription = null;
            if (channel == "started-event")
                subscription = events.Subscribe<CharacterPrimitiveSurvivalStartedEvent>(_ => throw fault);
            else if (channel == "changed-event")
                subscription = events.Subscribe<PrimitiveSurvivalChangedEvent>(value =>
                { if (value.Transition == PrimitiveSurvivalTransition.Started) throw fault; });
            else if (channel == "scheduler") scheduler.Failure = fault;
            else throw new ArgumentOutOfRangeException(nameof(channel));
            Exception observed = null;
            try { runner.TryStart(actor, CharacterPrimitiveSurvivalActionKind.FloorRest, out _); }
            catch (Exception failure) { observed = failure; }
            finally { subscription?.Dispose(); scheduler.Failure = null; }
            Exception[] errors = observed is AggregateException aggregate
                ? aggregate.Flatten().InnerExceptions.ToArray()
                : observed == null ? Array.Empty<Exception>() : new[] { observed };
            Require(errors.Length == 1 && ReferenceEquals(errors[0], fault)
                    && !runner.IsRunning(id) && !actor.Brain.IsExternallyDrivenActionActive
                    && !stateStore.TryGetPrimitiveSurvivalPending(id, out _)
                    && JsonConvert.SerializeObject(CaptureStats(actor)) == before && OutcomeCount() == 0,
                "Failed primitive start lost its error, retained an owner or applied effects: " + channel);
            Require(runner.TryStart(actor, CharacterPrimitiveSurvivalActionKind.FloorRest, out _)
                    && runner.IsRunning(id) && actor.Brain.IsExternallyDrivenActionActive
                    && scheduler.Pending != null && scheduler.Pending.MoveNext(),
                "Same primitive start could not retry after releasing its failed admission: " + channel);
            (scheduler.Pending as IDisposable)?.Dispose();
            Require(!runner.IsRunning(id) && !actor.Brain.IsExternallyDrivenActionActive
                    && !stateStore.TryGetPrimitiveSurvivalPending(id, out _)
                    && JsonConvert.SerializeObject(CaptureStats(actor)) == before && OutcomeCount() == 0,
                "Cancelling a retried primitive start left ownership or effects: " + channel);
        }

        internal void VerifyReplacementLifetime(HoldingCoroutineScheduler scheduler)
        {
            CharacterId id = (CharacterId)characterId;
            string before = JsonConvert.SerializeObject(CaptureStats(actor));
            Require(runner.TryStart(actor, CharacterPrimitiveSurvivalActionKind.FloorRest, out _)
                    && scheduler.Pending != null && scheduler.Pending.MoveNext(),
                "Original primitive run did not suspend before replacement.");
            IEnumerator original = scheduler.Pending;
            long epoch = actor.Brain.ExternalIntentEpoch;
            runner.Reset();
            Require(runner.TryStart(actor, CharacterPrimitiveSurvivalActionKind.FloorRest, out _)
                    && runner.IsRunning(id) && actor.Brain.ExternalIntentEpoch == epoch
                    && !ReferenceEquals(original, scheduler.Pending),
                "The replacement did not renew the original actor's exact intent epoch.");
            (original as IDisposable)?.Dispose();
            Require(runner.IsRunning(id) && actor.Brain.IsExternallyDrivenActionActive
                    && actor.Brain.ExternalIntentEpoch == epoch,
                "Retired primitive disposal ended its replacement's actual Brain lease.");
            Require(scheduler.Pending.MoveNext(), "The surviving replacement could not resume.");
            (scheduler.Pending as IDisposable)?.Dispose();
            Require(!runner.IsRunning(id) && !actor.Brain.IsExternallyDrivenActionActive
                    && !stateStore.TryGetPrimitiveSurvivalPending(id, out _)
                    && JsonConvert.SerializeObject(CaptureStats(actor)) == before && OutcomeCount() == 0,
                "The replacement did not release its own lease without physical effects.");
        }

        internal void VerifyFloorRestChildFailureIsAtomic(
            MigratedProducerOutcomeFaultMode fault)
        {
            restFacts?.Dispose();
            restFacts = outcomes.CreateCharacterRestFacts(probe);
            CharacterAiEditorTestDependencies.InjectCharacterStatsOutcomeForDiagnostics(
                actor.Stats,
                restFacts,
                restFacts);
            probe.TargetKind = MigratedProducerOutcomeKind.RestOutcomeIdentityEvent;
            probe.FaultMode = fault;
            string parentOwnerKey = MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent)
                .ProducerId + "|primitive-survival:" + characterId;
            string restOwnerKey = MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.RestOutcomeIdentityEvent)
                .ProducerId + "|rest-outcome:" + characterId;
            long Revision(string ownerKey) => outcomes.Transaction.Capture().owners
                .SingleOrDefault(value => string.Equals(
                    value.ownerKey, ownerKey, StringComparison.Ordinal))?.revision ?? 0L;
            string bodyBefore = CaptureDurableChild();
            string restKeyBefore = actor.Stats.LastRestOutcomeResultKey;
            string experiencesBefore = JsonConvert.SerializeObject(
                actor.Progression.NarrativeLedger.Facts);
            bool rejected = ExpectRejectedStart(
                CharacterPrimitiveSurvivalActionKind.FloorRest);
            bool pending = stateStore.TryGetPrimitiveSurvivalPending(
                (CharacterId)characterId, out _);
            var observation = new JObject
            {
                ["fault"] = fault.ToString(),
                ["rejected"] = rejected,
                ["bodyBefore"] = bodyBefore,
                ["bodyAfter"] = CaptureDurableChild(),
                ["pending"] = pending,
                ["parentCount"] = OutcomeCount(),
                ["parentRevision"] = Revision(parentOwnerKey),
                ["restRevision"] = Revision(restOwnerKey),
                ["restResultKey"] = actor.Stats.LastRestOutcomeResultKey
            };
            Debug.Log("PRIMITIVE_REST_CHILD_FAILURE="
                + observation.ToString(Formatting.None));
            Require(rejected
                    && !runner.IsRunning(new CharacterId(characterId))
                    && !pending
                    && CaptureDurableChild() == bodyBefore
                    && actor.Stats.LastRestOutcomeResultKey == restKeyBefore
                    && JsonConvert.SerializeObject(actor.Progression.NarrativeLedger.Facts)
                        == experiencesBefore
                    && OutcomeCount() == 0
                    && ExactOutcomeCount() == 0
                    && Revision(parentOwnerKey) == 0L
                    && Revision(restOwnerKey) == 0L
                    && outcomes.Transaction.Capture().owners.Count == 0,
                "Rejected rest child produced primitive completion: "
                + observation.ToString(Formatting.None));

            probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            bool retryStarted = runner.TryStart(actor,
                CharacterPrimitiveSurvivalActionKind.FloorRest, out _);
            var retryObservation = new JObject
            {
                ["fault"] = fault.ToString(),
                ["started"] = retryStarted,
                ["running"] = runner.IsRunning(new CharacterId(characterId)),
                ["parentCount"] = OutcomeCount(),
                ["parentRevision"] = Revision(parentOwnerKey),
                ["restRevision"] = Revision(restOwnerKey),
                ["ownerCount"] = outcomes.Transaction.Capture().owners.Count,
                ["restResultKey"] = actor.Stats.LastRestOutcomeResultKey
            };
            Debug.Log("PRIMITIVE_REST_CHILD_RETRY="
                + retryObservation.ToString(Formatting.None));
            Require(retryStarted
                    && !runner.IsRunning(new CharacterId(characterId))
                    && !stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId, out _)
                    && !string.IsNullOrWhiteSpace(actor.Stats.LastRestOutcomeResultKey)
                    && actor.Stats.LastRestOutcomeResultKey != restKeyBefore
                    && OutcomeCount() == 1
                    && ExactOutcomeCount() == 1
                    && Revision(parentOwnerKey) == 1L
                    && Revision(restOwnerKey) == 1L
                    && outcomes.Transaction.Capture().owners.Count == 2,
                "Rest child rejection prevented one normal retry: "
                    + retryObservation.ToString(Formatting.None));
            string completedBody = CaptureDurableChild();
            Require(!runner.TryResumePending(actor, out _)
                    && CaptureDurableChild() == completedBody
                    && OutcomeCount() == 1
                    && Revision(parentOwnerKey) == 1L
                    && Revision(restOwnerKey) == 1L
                    && outcomes.Transaction.Capture().owners.Count == 2,
                "Rest child rejection recovery replay duplicated completion.");
        }

        internal void VerifyFloorRestPendingRecovery()
        {
            float sleepBefore = actor.Stats.GetConditionValue(
                CharacterCondition.SLEEP,
                0f);
            float hygieneBefore = actor.Stats.GetConditionValue(
                CharacterCondition.HYGIENE,
                0f);
            probe.TargetKind = MigratedProducerOutcomeKind
                .CharacterPrimitiveSurvivalCompletedEvent;
            probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectReservation;
            Require(ExpectRejectedStart(
                        CharacterPrimitiveSurvivalActionKind.FloorRest)
                    && !runner.IsRunning(new CharacterId(characterId))
                    && Approximately(actor.Stats.GetConditionValue(
                        CharacterCondition.SLEEP,
                        0f), sleepBefore)
                    && Approximately(actor.Stats.GetConditionValue(
                        CharacterCondition.HYGIENE,
                        0f), hygieneBefore)
                    && !stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out _)
                    && OutcomeCount() == 0
                    && ExactOutcomeCount() == 0
                    && OutcomeOwnerRevision() == 0L,
                "Floor-rest reservation rejection changed body, pending, or outcome state.");

            probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            bool hasPending = stateStore.TryGetPrimitiveSurvivalPending(
                (CharacterId)characterId,
                out PrimitiveSurvivalPendingSaveData pending);
            Require(!hasPending,
                "Floor rest had a pending receipt before its child ran.");
            Require(ExpectRejectedStart(
                        CharacterPrimitiveSurvivalActionKind.FloorRest)
                    && stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out pending)
                    && string.Equals(
                        pending.actionId,
                        "survival:floor-rest",
                        StringComparison.Ordinal)
                    && string.Equals(
                        pending.publicationKey,
                        ExpectedFirstPublicationKey(),
                        StringComparison.Ordinal)
                    && pending.absoluteDay == InitialDay
                    && actor.Stats.GetConditionValue(
                        CharacterCondition.SLEEP,
                        0f) > sleepBefore
                    && actor.Stats.GetConditionValue(
                        CharacterCondition.HYGIENE,
                        0f) < hygieneBefore
                    && OutcomeCount() == 0
                    && ExactOutcomeCount() == 0
                    && OutcomeOwnerRevision() == 0L,
                "Floor-rest commit rejection did not retain an explicit body partial.");

            string childAfterFailure = CaptureDurableChild();
            RoundTripPendingState();
            Require(string.Equals(
                        CaptureDurableChild(),
                        childAfterFailure,
                        StringComparison.Ordinal)
                    && stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out PrimitiveSurvivalPendingSaveData restored)
                    && string.Equals(
                        restored.operationId,
                        pending.operationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        restored.publicationKey,
                        pending.publicationKey,
                        StringComparison.Ordinal),
                "Floor-rest pending receipt did not round-trip with its child.");

            probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            Require(runner.TryResumePending(actor, out _)
                    && !stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out _)
                    && string.Equals(
                        CaptureDurableChild(),
                        childAfterFailure,
                        StringComparison.Ordinal)
                    && OutcomeCount() == 1
                    && ExactOutcomeCount() == 1
                    && OutcomeOwnerRevision() == 1L,
                "Floor-rest pending parent did not resume exactly once.");
            RequireRecoveredOutcome(
                pending,
                "survival:floor-rest",
                PrimitiveSurvivalBalanceAuthority.FloorRestRecovery);
            string terminal = CaptureDurableChild();
            Require(!runner.TryResumePending(actor, out _)
                    && string.Equals(
                        CaptureDurableChild(),
                        terminal,
                        StringComparison.Ordinal)
                    && OutcomeCount() == 1
                    && OutcomeOwnerRevision() == 1L,
                "Floor-rest terminal replay changed body or outcome state.");
        }

        internal void VerifyRetiredFloorRestPendingRecovery()
        {
            restFacts?.Dispose();
            restFacts = outcomes.CreateCharacterRestFacts(probe);
            CharacterAiEditorTestDependencies.InjectCharacterStatsOutcomeForDiagnostics(
                actor.Stats,
                restFacts,
                restFacts);
            probe.TargetKind = MigratedProducerOutcomeKind
                .CharacterPrimitiveSurvivalCompletedEvent;
            probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            bool hadPending = stateStore.TryGetPrimitiveSurvivalPending(
                (CharacterId)characterId,
                out PrimitiveSurvivalPendingSaveData pending);

            Require(!hadPending
                    && ExpectRejectedStart(
                        CharacterPrimitiveSurvivalActionKind.FloorRest)
                    && stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out pending)
                    && !actor.Stats.HasUnconfirmedRestObservation
                    && !string.IsNullOrWhiteSpace(pending.restPublicationKey)
                    && string.Equals(
                        pending.publicationKey,
                        ExpectedFirstPublicationKey(),
                        StringComparison.Ordinal),
                "Retired floor-rest setup did not retain its applied child and pending parent.");

            string pendingJson = JsonConvert.SerializeObject(pending);
            stateStore.Remove((CharacterId)characterId);
            List<PrimitiveSurvivalPendingSaveData> retired =
                stateStore.CaptureRetiredPrimitivePublications();
            Require(stateStore.Capture().Count == 0
                    && retired.Count == 1
                    && string.Equals(
                        retired[0].operationId,
                        pending.operationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        retired[0].publicationKey,
                        pending.publicationKey,
                        StringComparison.Ordinal)
                    && string.Equals(
                        JsonConvert.SerializeObject(retired[0]),
                        pendingJson,
                        StringComparison.Ordinal),
                "Removing the floor-rest owner did not retire its exact pending parent and child.");

            var root = new DungeonDarkSurvivalSaveData
            {
                version = DungeonDarkSurvivalSaveData.CurrentVersion,
                characters = new List<CharacterDeprivationState>(),
                pendingPrimitivePublications = retired
            };
            string rootJson = JsonConvert.SerializeObject(root);
            DungeonDarkSurvivalSaveData restoredRoot =
                JsonConvert.DeserializeObject<DungeonDarkSurvivalSaveData>(rootJson);
            Require(restoredRoot?.pendingPrimitivePublications?.Count == 1
                    && string.Equals(
                        JsonConvert.SerializeObject(
                            restoredRoot.pendingPrimitivePublications[0]),
                        pendingJson,
                        StringComparison.Ordinal),
                "The retired primitive publication did not survive its Newtonsoft root round-trip.");
            PrimitiveSurvivalFactPreflight.Validate(
                restoredRoot,
                outcomes.Ledger.CaptureGameplayOutcomes(),
                outcomes.Transaction.Capture());

            CharacterDeprivationAggregateState restoredAggregate =
                CharacterDeprivationStateStore.BuildValidatedAggregate(
                    Array.Empty<CharacterDeprivationState>(),
                    Array.Empty<CharacterId>());
            CharacterDeprivationStateStore.RestoreRetiredPrimitivePublications(
                restoredAggregate,
                restoredRoot.pendingPrimitivePublications);
            stateStore.ReplaceValidatedAggregate(restoredAggregate);
            Require(stateStore.Capture().Count == 0
                    && stateStore.CaptureRetiredPrimitivePublications().Count == 1,
                "The ownerless primitive publication was not restored independently of character state.");

            string statsBeforeRecovery = CaptureStats(actor).ToString(Formatting.None);
            string filthBeforeRecovery = filth.CanonicalState();
            string moodBeforeRecovery = JsonConvert.SerializeObject(actor.Mood);
            string narrativeBeforeRecovery = JsonConvert.SerializeObject(
                actor.Progression.NarrativeLedger.Facts);
            int RestOutcomeCount()
            {
                string type = MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.RestOutcomeIdentityEvent)
                    .OutcomeTypeId.Value;
                return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                    value => value != null
                        && string.Equals(
                            value.outcomeTypeId,
                            type,
                            StringComparison.Ordinal));
            }

            probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            runner.RecoverRetiredPublications();
            clock.Advance();
            runner.RecoverRetiredPublications();
            Require(stateStore.Capture().Count == 0
                    && stateStore.CaptureRetiredPrimitivePublications().Count == 0
                    && !stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out _)
                    && OutcomeCount() == 1
                    && ExactOutcomeCount() == 1
                    && OutcomeOwnerRevision() == 1L
                    && RestOutcomeCount() == 1
                    && string.Equals(
                        CaptureStats(actor).ToString(Formatting.None),
                        statsBeforeRecovery,
                        StringComparison.Ordinal)
                    && string.Equals(
                        filth.CanonicalState(),
                        filthBeforeRecovery,
                        StringComparison.Ordinal)
                    && string.Equals(
                        JsonConvert.SerializeObject(actor.Mood),
                        moodBeforeRecovery,
                        StringComparison.Ordinal)
                    && string.Equals(
                        JsonConvert.SerializeObject(
                            actor.Progression.NarrativeLedger.Facts),
                        narrativeBeforeRecovery,
                        StringComparison.Ordinal),
                "Retired floor-rest recovery duplicated facts or changed the ownerless actor state.");
            RequireRecoveredOutcome(
                pending,
                "survival:floor-rest",
                PrimitiveSurvivalBalanceAuthority.FloorRestRecovery);
        }

        internal void VerifyLatrinePendingRecovery()
        {
            actor.stats[CharacterCondition.EXCRETION] = 0f;
            float excretionBefore = actor.Stats.GetConditionValue(
                CharacterCondition.EXCRETION,
                0f);
            float hygieneBefore = actor.Stats.GetConditionValue(
                CharacterCondition.HYGIENE,
                0f);
            probe.TargetKind = MigratedProducerOutcomeKind
                .CharacterPrimitiveSurvivalCompletedEvent;
            string filthBefore = filth.CanonicalState();
            probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectReservation;
            Require(ExpectRejectedStart(CharacterPrimitiveSurvivalActionKind.Latrine)
                    && !runner.IsRunning(new CharacterId(characterId))
                    && Approximately(actor.Stats.GetConditionValue(
                        CharacterCondition.EXCRETION,
                        0f), excretionBefore)
                    && Approximately(actor.Stats.GetConditionValue(
                        CharacterCondition.HYGIENE,
                        0f), hygieneBefore)
                    && string.Equals(
                        filth.CanonicalState(),
                        filthBefore,
                        StringComparison.Ordinal)
                    && !stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out _)
                    && OutcomeCount() == 0
                    && ExactOutcomeCount() == 0
                    && OutcomeOwnerRevision() == 0L,
                "Latrine reservation rejection changed body, filth, pending, or outcome state.");

            probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            bool hasPending = stateStore.TryGetPrimitiveSurvivalPending(
                (CharacterId)characterId,
                out PrimitiveSurvivalPendingSaveData pending);
            Require(!hasPending,
                "Latrine had a pending receipt before its child ran.");
            Require(ExpectRejectedStart(CharacterPrimitiveSurvivalActionKind.Latrine)
                    && stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out pending)
                    && string.Equals(
                        pending.actionId,
                        "survival:primitive-latrine",
                        StringComparison.Ordinal)
                    && string.Equals(
                        pending.publicationKey,
                        ExpectedFirstPublicationKey(),
                        StringComparison.Ordinal)
                    && pending.absoluteDay == InitialDay
                    && actor.Stats.GetConditionValue(
                        CharacterCondition.EXCRETION,
                        0f) > excretionBefore
                    && actor.Stats.GetConditionValue(
                        CharacterCondition.HYGIENE,
                        0f) < hygieneBefore
                    && filth.CaptureFilth().Count == 2
                    && OutcomeCount() == 0
                    && ExactOutcomeCount() == 0
                    && OutcomeOwnerRevision() == 0L,
                "Latrine commit rejection did not retain an explicit body/filth partial.");

            string childAfterFailure = CaptureDurableChild();
            RoundTripPendingState();
            Require(string.Equals(
                        CaptureDurableChild(),
                        childAfterFailure,
                        StringComparison.Ordinal)
                    && stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out PrimitiveSurvivalPendingSaveData restored)
                    && string.Equals(
                        restored.operationId,
                        pending.operationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        restored.publicationKey,
                        pending.publicationKey,
                        StringComparison.Ordinal),
                "Latrine pending receipt did not round-trip with its body/filth child.");

            probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            Require(runner.TryResumePending(actor, out _)
                    && !stateStore.TryGetPrimitiveSurvivalPending(
                        (CharacterId)characterId,
                        out _)
                    && string.Equals(
                        CaptureDurableChild(),
                        childAfterFailure,
                        StringComparison.Ordinal)
                    && filth.CaptureFilth().Count == 2
                    && OutcomeCount() == 1
                    && ExactOutcomeCount() == 1
                    && OutcomeOwnerRevision() == 1L,
                "Latrine pending parent did not resume without reapplying its child.");
            RequireRecoveredOutcome(
                pending,
                "survival:primitive-latrine",
                PrimitiveSurvivalBalanceAuthority.LatrineRecovery);
            string terminal = CaptureDurableChild();
            Require(!runner.TryResumePending(actor, out _)
                    && string.Equals(
                        CaptureDurableChild(),
                        terminal,
                        StringComparison.Ordinal)
                    && filth.CaptureFilth().Count == 2
                    && OutcomeCount() == 1
                    && OutcomeOwnerRevision() == 1L,
                "Latrine terminal replay changed body, filth, or outcome state.");
        }

        private bool ExpectRejectedStart(
            CharacterPrimitiveSurvivalActionKind kind)
        {
            try
            {
                _ = runner.TryStart(actor, kind, out _);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private string CaptureDurableChild() => string.Join("|", new[]
        {
            actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            actor.Stats.GetConditionValue(CharacterCondition.EXCRETION, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            filth.CanonicalState()
        });

        private string ExpectedFirstPublicationKey() =>
            MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind
                    .CharacterPrimitiveSurvivalCompletedEvent,
                "primitive-survival:" + characterId,
                1L).ToString();

        private void RoundTripPendingState()
        {
            string actorStatsJson = CaptureStats(actor).ToString(
                Formatting.None);
            var save = new DungeonDarkSurvivalSaveData
            {
                version = DungeonDarkSurvivalSaveData.CurrentVersion,
                nextFilthSequence = filth.NextFilthSequence,
                nextWaterSequence = 1,
                characters = stateStore.Capture(),
                filth = filth.CaptureFilth(),
                waterSources = new List<WorldWaterSourceSaveData>()
            };
            string json = JsonUtility.ToJson(save);
            DungeonDarkSurvivalSaveData restored =
                JsonUtility.FromJson<DungeonDarkSurvivalSaveData>(json);
            CharacterDeprivationPersistenceCoordinator.ValidatePayloadShape(
                restored);
            actor.stats[CharacterCondition.SLEEP] = 0f;
            actor.stats[CharacterCondition.HYGIENE] = 0f;
            actor.stats[CharacterCondition.EXCRETION] = 0f;
            RestoreStats(actor, JObject.Parse(actorStatsJson));
            stateStore.Restore(
                restored.characters,
                new[] { (CharacterId)characterId });
            filth.RestoreFilth(
                restored.filth,
                restored.nextFilthSequence);
            runner.Reset();
        }

        private void RequireRecoveredOutcome(
            PrimitiveSurvivalPendingSaveData pending,
            string actionId,
            float recovery)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    MigratedProducerOutcomeKind
                        .CharacterPrimitiveSurvivalCompletedEvent,
                    probe);
            Require(identity.ResultKey.CommitRevision == 1L
                    && identity.ResultKey.LocalResultIndex == 0
                    && string.Equals(
                        identity.ResultKey.ToString(),
                        pending.publicationKey,
                        StringComparison.Ordinal)
                    && string.Equals(
                        pending.publicationKey,
                        ExpectedFirstPublicationKey(),
                        StringComparison.Ordinal)
                    && outcomes.Ledger.TryGetExact(
                        identity.OutcomeId,
                        out GameplayOutcomeSnapshot exact)
                    && exact.ownerRevision == 1L
                    && exact.absoluteDay == pending.absoluteDay
                    && exact.subjects?.Count == 1
                    && string.Equals(
                        exact.subjects[0].entityId,
                        characterId,
                        StringComparison.Ordinal)
                    && exact.facts != null
                    && exact.facts.Any(value => string.Equals(
                            value.factId,
                            MigratedProducerOutcomeIds.SummaryFact.Value,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.value,
                            $"action={actionId}; recovery={recovery:0.###}; physicalItemCount=0",
                            StringComparison.Ordinal)),
                "Recovered primitive-survival parent lost its exact saved identity.");
        }

        internal GameplayOutcomeReplayIdentity RequireOutcome(
            int expectedSequence)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    MigratedProducerOutcomeKind
                        .CharacterPrimitiveSurvivalCompletedEvent,
                    probe);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind
                        .CharacterPrimitiveSurvivalCompletedEvent);
            int expectedDay = expectedSequence switch
            {
                1 => FirstDay,
                2 => SecondDay,
                3 => ThirdDay,
                _ => throw new ArgumentOutOfRangeException(nameof(expectedSequence))
            };
            string expectedSummary =
                "action=survival:floor-rest; recovery=55; physicalItemCount=0";
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedSequence
                || exact.commitRevision != expectedSequence
                || exact.ownerRevision != expectedSequence
                || exact.absoluteDay != expectedDay
                || exact.status != GameplayOutcomeStatus.Succeeded
                || !string.Equals(
                    exact.producerId,
                    definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || exact.subjects?.Count != 1
                || exact.participants?.Count != 1
                || !string.Equals(
                    exact.subjects[0].entityId,
                    characterId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.participants[0].entityId,
                    characterId,
                    StringComparison.Ordinal)
                || exact.facts == null
                || !exact.facts.Any(value => string.Equals(
                        value.factId,
                        MigratedProducerOutcomeIds.SummaryFact.Value,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.value,
                        expectedSummary,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Primitive-survival production entry did not publish its exact outcome.");
            }
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
            CaptureNarrativeEvidence(GameplayEntityId subject, int maximumCount) =>
            ProductionCausalNarrativeEvidence.Capture(
                outcomes.Ledger, subject, maximumCount);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral primitive-survival fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["stage"] = Stage,
                    ["characterId"] = characterId,
                    ["calendarDay"] = calendar.Day,
                    ["actorStats"] = CaptureStats(actor),
                    ["growthState"] = JObject.Parse(JsonUtility.ToJson(
                        actor.Progression.GrowthState)),
                    ["narrativeLedger"] = JObject.Parse(JsonUtility.ToJson(
                        actor.Progression.NarrativeLedger)),
                    ["acquiredTraitState"] = JObject.Parse(JsonUtility.ToJson(
                        actor.Progression.CaptureAcquiredTraitState())),
                    ["traitInferencePublications"] = JObject.Parse(
                        JsonUtility.ToJson(traitInferences.Capture())),
                    ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(
                        outcomes.Transaction.Capture()))
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            activityObserver?.Dispose();
            restFacts?.Dispose();
            primitiveFacts?.Dispose();
            traitInferenceBinding?.Dispose();
            traitInferenceFacts?.Dispose();
            calendar.SetDateTime(originalCalendarDay, originalCalendarHour);
            runner.ReleaseActor(new CharacterId(characterId));
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
            if (actor != null)
                UnityEngine.Object.DestroyImmediate(actor.gameObject);
            if (immunityTrait != null)
                UnityEngine.Object.DestroyImmediate(immunityTrait);
        }

        private int Stage
        {
            get
            {
                int outcomesCount = OutcomeCount();
                int exactCount = ExactOutcomeCount();
                long ownerRevision = OutcomeOwnerRevision();
                if (outcomesCount == 0 && exactCount == 0 && ownerRevision == 0)
                    return 0;
                if (outcomesCount == 1 && exactCount == 1 && ownerRevision == 1)
                    return 1;
                if (outcomesCount == 2 && exactCount == 2 && ownerRevision == 2)
                    return 2;
                if (outcomesCount == 3 && exactCount == 3 && ownerRevision == 3)
                    return 3;
                return -1;
            }
        }

        internal int SourceStage => Stage;
        internal CharacterActor SourceActor => actor;
        internal GameplayOutcomeLedger SourceOutcomeLedger => outcomes.Ledger;
        internal IDomainFactPublisher SourceTraitInferenceFacts =>
            traitInferenceFacts;
        internal CharacterTraitInferencePublicationRuntime SourceTraitInferences =>
            traitInferences;

        private bool CompleteRest(int expectedStage, int day)
        {
            ThrowIfDisposed();
            if (Stage != expectedStage)
                return false;
            calendar.SetDateTime(day, 0);
            if (!runner.TryStart(
                    actor,
                    CharacterPrimitiveSurvivalActionKind.FloorRest,
                    out _))
            {
                return false;
            }
            bool valid = Stage == expectedStage + 1
                && !runner.IsRunning(new CharacterId(characterId));
            if (valid)
                ValidateState();
            return valid;
        }

        private int OutcomeCount()
        {
            string type = MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind
                    .CharacterPrimitiveSurvivalCompletedEvent).OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        type,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount()
        {
            string type = MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind
                    .CharacterPrimitiveSurvivalCompletedEvent).OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        type,
                        StringComparison.Ordinal));
        }

        private long OutcomeOwnerRevision()
        {
            DungeonMigratedProducerOutcomeSaveData save =
                outcomes.Transaction.Capture();
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind
                        .CharacterPrimitiveSurvivalCompletedEvent);
            string expectedKey = definition.ProducerId
                + "|primitive-survival:"
                + characterId;
            MigratedProducerOwnerRevisionSaveData[] matches = save.owners
                .Where(value => value != null && string.Equals(
                    value.ownerKey,
                    expectedKey,
                    StringComparison.Ordinal))
                .ToArray();
            return matches.Length switch
            {
                0 => 0L,
                1 => matches[0].revision,
                _ => -1L
            };
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                outcomes.Ledger);
            RequireFields(
                state,
                "primitive-survival owner state",
                "schemaVersion",
                "schema",
                "stage",
                "characterId",
                "calendarDay",
                "actorStats",
                "growthState",
                "narrativeLedger",
                "acquiredTraitState",
                "traitInferencePublications",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["characterId"],
                    characterId,
                    StringComparison.Ordinal)
                || state["actorStats"] is not JObject actorStats
                || state["growthState"] is not JObject growthState
                || state["narrativeLedger"] is not JObject narrativeLedger
                || state["acquiredTraitState"] is not JObject acquiredTraitState
                || state["traitInferencePublications"] is not JObject publications
                || state["outcomeOwners"] is not JObject ownerJson)
            {
                throw new InvalidOperationException(
                    "Primitive-survival checkpoint identity changed.");
            }
            int savedDay = (int)state["calendarDay"];
            if (savedDay is < InitialDay or > ThirdDay)
                throw new InvalidOperationException(
                    "Primitive-survival checkpoint day is invalid.");
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    ownerJson.ToString(Formatting.None))));
            RestoreStats(actor, actorStats);
            CharacterProgressionSnapshot current = actor.Progression
                .CapturePersistentState();
            actor.Progression.RestorePersistentState(
                new CharacterProgressionSnapshot(
                    current.Level,
                    current.CurrentExperience,
                    JsonUtility.FromJson<CharacterGrowthState>(
                        growthState.ToString(Formatting.None)),
                    JsonUtility.FromJson<CharacterNarrativeLedger>(
                        narrativeLedger.ToString(Formatting.None)),
                    JsonUtility.FromJson<CharacterAcquiredTraitAggregateState>(
                        acquiredTraitState.ToString(Formatting.None))));
            CharacterTraitInferencePublicationSaveData savedPublications =
                CharacterTraitInferencePublicationRuntime.ValidateAndClone(
                    JsonUtility.FromJson<CharacterTraitInferencePublicationSaveData>(
                        publications.ToString(Formatting.None)));
            foreach (CharacterTraitInferencePublicationData observation
                in savedPublications.observations)
            {
                CharacterTraitInferredFactPreflight.ValidateObservation(
                    observation,
                    outcomes.Ledger.CaptureGameplayOutcomes());
            }
            traitInferences.Restore(savedPublications);
            calendar.SetDateTime(savedDay, 0);
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Primitive-survival checkpoint stage disagrees with its saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            CharacterMoodSnapshot mood = actor.Mood;
            CharacterMoodFactorSnapshot[] factors = mood.Factors
                .Where(value => value != null
                    && value.Kind == CharacterMoodFactorKind.Interaction)
                .ToArray();
            CharacterMoodFactorSnapshot restMood = factors.SingleOrDefault(
                value => string.Equals(
                    value.Id,
                    "survival:floor-rest",
                    StringComparison.Ordinal));
            float expectedSleep = stage switch
            {
                0 => InitialSleep,
                1 => 55f,
                2 => 100f,
                3 => 100f,
                _ => float.NaN
            };
            float expectedHygiene = stage switch
            {
                0 => InitialHygiene,
                1 => 56f,
                2 => 52f,
                3 => 48f,
                _ => float.NaN
            };
            int expectedDay = stage switch
            {
                0 => InitialDay,
                1 => FirstDay,
                2 => SecondDay,
                3 => ThirdDay,
                _ => -1
            };
            bool valid = stage is >= 0 and <= 3
                && string.Equals(
                    actor.Identity.PersistentId,
                    characterId,
                    StringComparison.Ordinal)
                && calendar.Day == expectedDay
                && Approximately(
                    actor.Stats.GetConditionValue(
                        CharacterCondition.SLEEP,
                        0f),
                    expectedSleep)
                && Approximately(
                    actor.Stats.GetConditionValue(
                        CharacterCondition.HYGIENE,
                        0f),
                    expectedHygiene)
                && Approximately(
                    actor.Stats.GetConditionValue(
                        CharacterCondition.EXCRETION,
                        0f),
                    InitialExcretion)
                && OutcomeCount() == stage
                && ExactOutcomeCount() == stage
                && OutcomeOwnerRevision() == stage
                && HasExpectedRestExperience(stage)
                && (stage == 0 || moodImmune
                    ? restMood == null
                    : restMood != null
                        && Approximately(
                            restMood.Value,
                            PrimitiveSurvivalBalanceAuthority
                                .FloorRestMoodDelta));
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Primitive-survival body state, owner revision and exact ledger diverged. "
                    + $"stage={stage}; day={calendar.Day}/{expectedDay}; "
                    + $"sleep={actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f):0.###}/{expectedSleep:0.###}; "
                    + $"hygiene={actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f):0.###}/{expectedHygiene:0.###}; "
                    + $"outcomes={OutcomeCount()}/{stage}; exact={ExactOutcomeCount()}/{stage}; "
                    + $"ownerRevision={OutcomeOwnerRevision()}/{stage}; "
                    + "narrative=" + JArray.FromObject(actor.Progression.NarrativeLedger.Facts)
                        .ToString(Formatting.None) + "; "
                    + $"restMood={(restMood == null ? "null" : restMood.Value.ToString("0.###"))}.");
            }
        }

        private bool HasExpectedRestExperience(int stage)
        {
            CharacterNarrativeFact[] related = actor.Progression.NarrativeLedger.Facts
                .Where(value => value != null
                    && (value.factId.StartsWith("survival:floor-rest", StringComparison.Ordinal)
                        || value.factId == "need:sleep"))
                .ToArray();
            if (stage == 0)
                return related.Length == 0;
            if (stage is < 1 or > 3 || related.Length != (stage == 1 ? 1 : 2))
                return false;
            GameplayOutcomeSnapshot[] exact = outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes
                .Where(value => value.outcomeTypeId == MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent).OutcomeTypeId.Value)
                .OrderBy(value => value.sequence).ToArray();
            if (exact.Length != stage)
                return false;
            if (moodImmune)
            {
                CharacterNarrativeFact survival = related.SingleOrDefault(value =>
                    value.domain == CharacterNarrativeDomain.Survival && value.factId == "survival:floor-rest:completed");
                GameplayOutcomeSnapshot latest = exact[stage - 1];
                string eventId = latest.runId + "/" + latest.sequence.ToString(CultureInfo.InvariantCulture);
                if (survival == null || survival.count != stage || !Approximately(survival.totalValue, 55f * stage)
                    || survival.subjectId != characterId || survival.outcome != CharacterActivityOutcomes.Completed
                    || survival.milestoneCount != CharacterAcquiredTraitExperienceScore.RecomputeGroupScore(stage)
                    || survival.eventGroupKey != "primitive-survival" || survival.actionKey != "survival:floor-rest"
                    || survival.relationshipKey != string.Empty || !Approximately(survival.importancePoints, 1f)
                    || survival.lastDay != latest.absoluteDay || survival.lastEventContext == null
                    || survival.lastEventContext.eventInstanceId != eventId || survival.lastEventContext.chainId != eventId
                    || survival.lastEventContext.actorId != characterId || survival.lastEventContext.occurredDay != latest.absoluteDay
                    || !latest.subjects.Any(subject => subject.entityId == characterId))
                    return false;
            }
            else
            {
                CharacterNarrativeFact mood = related.SingleOrDefault(value =>
                    value.domain == CharacterNarrativeDomain.Mood && value.factId == "survival:floor-rest");
                if (!MatchesRestExperience(mood, stage, -3f * stage, "negative", exact[stage - 1], false))
                    return false;
            }
            if (stage == 1)
                return true;
            CharacterNarrativeFact need = related.SingleOrDefault(value =>
                value.domain == CharacterNarrativeDomain.Need && value.factId == "need:sleep");
            return MatchesRestExperience(need, 1, 100f, "satisfied", exact[1], true);
        }

        private bool MatchesRestExperience(CharacterNarrativeFact fact, int count, float value,
            string result, GameplayOutcomeSnapshot committed, bool operationContext)
        {
            if (fact == null || committed == null)
                return false;
            CharacterNarrativeEvidenceMetadata metadata = CharacterNarrativeEvidenceMetadata.Ordinary(
                fact.domain, fact.factId);
            string eventId = operationContext ? committed.provenance.Single(row => row.kindId == "domain-commit").value
                : committed.runId + "/" + committed.sequence.ToString(CultureInfo.InvariantCulture);
            GameplayNarrativeEventContext context = fact.lastEventContext;
            return fact.count == count && Approximately(fact.totalValue, value)
                && fact.subjectId == string.Empty && fact.outcome == result
                && fact.milestoneCount == CharacterAcquiredTraitExperienceScore.RecomputeGroupScore(count)
                && fact.eventGroupKey == metadata.eventGroupKey && fact.actionKey == metadata.actionKey
                && fact.relationshipKey == metadata.relationshipKey
                && Approximately(fact.importancePoints, metadata.importancePoints)
                && fact.lastDay == committed.absoluteDay
                && context != null && context.eventInstanceId == eventId && context.chainId == eventId
                && context.actorId == characterId && context.occurredDay == committed.absoluteDay
                && committed.subjects.Any(subject => subject.entityId == characterId);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    // Forward authored content unchanged, replacing one trait only for this actor's projector.
    private sealed class MoodImmunityCatalog : IGameContentCatalog
    {
        private readonly IGameContentCatalog source;
        private readonly CharacterTraitSO original;
        private readonly CharacterTraitSO replacement;

        internal MoodImmunityCatalog(IGameContentCatalog source, CharacterTraitSO original, CharacterTraitSO replacement)
        { this.source = source; this.original = original; this.replacement = replacement; }

        public GameContentCatalogSO Root => source.Root;
        public ItemDefinitionCatalogSO Items => source.Items;
        public WorldInteractionPresentationCatalogSO WorldPresentation => source.WorldPresentation;
        public CharacterSkillSystemSettingsSO CharacterSkillSettings => source.CharacterSkillSettings;
        public GameMediaCatalogSO Media => source.Media;
        public GameDomainContentCatalogSO Domain => source.Domain;
        public IReadOnlyList<ServiceProcessSO> ServiceProcesses => source.ServiceProcesses;
        public RoomEnvironmentSettingsSO RoomEnvironmentSettings => source.RoomEnvironmentSettings;
        public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes => source.SiteArchetypes;
        public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites => source.UrgentSites;
        public IReadOnlyList<OffenseDecisionCardSO> DecisionCards => source.DecisionCards;
        public IReadOnlyList<OffenseEncounterSO> Encounters => source.Encounters;
        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject => source.GetAll<T>()
            .Select(value => ReferenceEquals(value, original) ? (T)(ScriptableObject)replacement : value).ToArray();
        public T RequireSingle<T>() where T : ScriptableObject => GetAll<T>().Single();
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
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

    private sealed class DrainingGameClock : IGameClock
    {
        public float DeltaTime => 10f;
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;

        internal void Advance()
        {
            Time += DeltaTime;
            FrameCount++;
        }
    }

    internal sealed class HoldingCoroutineScheduler : ICharacterPrimitiveSurvivalCoroutineScheduler
    {
        internal Exception Failure;
        internal IEnumerator Pending;

        public void Start(CharacterActor owner, IEnumerator routine)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (routine == null) throw new ArgumentNullException(nameof(routine));
            if (Failure != null) throw Failure;
            Pending = routine;
        }
    }

    private sealed class DrainingCoroutineScheduler :
        ICharacterPrimitiveSurvivalCoroutineScheduler
    {
        private readonly DrainingGameClock clock;

        internal DrainingCoroutineScheduler(DrainingGameClock clock) =>
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));

        public void Start(CharacterActor owner, IEnumerator routine)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (routine == null)
                throw new ArgumentNullException(nameof(routine));
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        (current as IDisposable)?.Dispose();
                        stack.Pop();
                        continue;
                    }
                    if (current.Current is IEnumerator nested)
                    {
                        stack.Push(nested);
                        continue;
                    }
                    clock.Advance();
                }
            }
            finally
            {
                while (stack.Count > 0)
                    (stack.Pop() as IDisposable)?.Dispose();
            }
        }
    }

    private sealed class FixedGridSystemProvider : IGridSystemProvider
    {
        internal FixedGridSystemProvider(Grid grid) =>
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        public GridSystemManager Manager => null;
        public Grid Grid { get; }
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }
        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return true;
        }
    }

    private sealed class EmptyFieldMealConsumption :
        IFieldMealConsumptionCommand
    {
        internal static readonly EmptyFieldMealConsumption Instance = new();
        public bool TryFindFieldMeal(
            CharacterActor actor,
            out ItemStackId stackId,
            out Vector2Int position,
            out CharacterConsumablesFailure failure)
        {
            stackId = default;
            position = default;
            failure = default;
            return false;
        }
        public bool TryConsumeFieldMeal(
            CharacterActor actor,
            ItemStackId stackId,
            out MealConsumptionResult result)
        {
            result = default;
            return false;
        }
    }

    private sealed class EmptyWorldFilthQuery : IWorldFilthQuery
    {
        internal static readonly EmptyWorldFilthQuery Instance = new();
        public int StateVersion => 0;
        public int NextFilthSequence => 1;
        public IReadOnlyList<WorldFilthSnapshot> GetAll() =>
            Array.Empty<WorldFilthSnapshot>();
        public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position) =>
            Array.Empty<WorldFilthSnapshot>();
        public WorldFilthSnapshot AddFilth(
            WorldFilthType type,
            Vector2Int position,
            float amount,
            string sourceCharacterId,
            float infectionRisk,
            bool wallStain = false) => new(
                "filth:causal-primitive:" + type,
                type,
                amount,
                position,
                sourceCharacterId,
                infectionRisk,
                wallStain);
        public bool Clean(
            string filthId,
            float workAmount,
            out float remainingAmount)
        {
            remainingAmount = 0f;
            return false;
        }
        public float GetCleanlinessPenalty(
            Vector2Int position,
            int radius = 0) => 0f;
        public List<WorldFilthSaveData> CaptureFilth() => new();
        public void RestoreFilth(
            IEnumerable<WorldFilthSaveData> saveData,
            int nextSequence)
        {
        }
    }

    private sealed class RecordingWorldFilthQuery : IWorldFilthQuery
    {
        private readonly List<WorldFilthSaveData> rows = new();
        private int nextSequence = 1;
        private int stateVersion;

        public int StateVersion => stateVersion;
        public int NextFilthSequence => nextSequence;

        public IReadOnlyList<WorldFilthSnapshot> GetAll() => rows
            .Select(ToSnapshot)
            .ToArray();

        public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position) =>
            rows.Where(value => value.gridX == position.x
                    && value.gridY == position.y)
                .Select(ToSnapshot)
                .ToArray();

        public WorldFilthSnapshot AddFilth(
            WorldFilthType type,
            Vector2Int position,
            float amount,
            string sourceCharacterId,
            float infectionRisk,
            bool wallStain = false)
        {
            var row = new WorldFilthSaveData
            {
                filthId = $"filth:b0-a02:{nextSequence++:D8}",
                type = type,
                amount = amount,
                gridX = position.x,
                gridY = position.y,
                sourceCharacterId = sourceCharacterId ?? string.Empty,
                infectionRisk = infectionRisk,
                wallStain = wallStain
            };
            rows.Add(row);
            stateVersion++;
            return ToSnapshot(row);
        }

        public bool Clean(
            string filthId,
            float workAmount,
            out float remainingAmount)
        {
            remainingAmount = 0f;
            return false;
        }

        public float GetCleanlinessPenalty(
            Vector2Int position,
            int radius = 0) => 0f;

        public List<WorldFilthSaveData> CaptureFilth() => rows
            .Select(Clone)
            .ToList();

        public void RestoreFilth(
            IEnumerable<WorldFilthSaveData> saveData,
            int restoredNextSequence)
        {
            rows.Clear();
            rows.AddRange((saveData ?? Array.Empty<WorldFilthSaveData>())
                .Where(value => value != null)
                .Select(Clone));
            nextSequence = restoredNextSequence;
            stateVersion++;
        }

        internal string CanonicalState() => JsonConvert.SerializeObject(new
        {
            nextSequence,
            rows = rows.OrderBy(value => value.filthId, StringComparer.Ordinal)
                .Select(value => new
                {
                    value.filthId,
                    type = value.type.ToString(),
                    value.amount,
                    value.gridX,
                    value.gridY,
                    value.sourceCharacterId,
                    value.infectionRisk,
                    value.wallStain
                })
        });

        private static WorldFilthSaveData Clone(WorldFilthSaveData value) =>
            new()
            {
                filthId = value.filthId,
                type = value.type,
                amount = value.amount,
                gridX = value.gridX,
                gridY = value.gridY,
                sourceCharacterId = value.sourceCharacterId,
                infectionRisk = value.infectionRisk,
                wallStain = value.wallStain
            };

        private static WorldFilthSnapshot ToSnapshot(
            WorldFilthSaveData value) => new(
            value.filthId,
            value.type,
            value.amount,
            new Vector2Int(value.gridX, value.gridY),
            value.sourceCharacterId,
            value.infectionRisk,
            value.wallStain);
    }

    private sealed class EmptyWorldWaterQuery : IWorldWaterQuery
    {
        internal static readonly EmptyWorldWaterQuery Instance = new();
        public int NextWaterSequence => 1;
        public IReadOnlyList<WorldWaterSourceSnapshot> GetAllSources() =>
            Array.Empty<WorldWaterSourceSnapshot>();
        public bool TryGetSource(
            string sourceId,
            out WorldWaterSourceSnapshot source)
        {
            source = default;
            return false;
        }
        public bool TryFindDrinkSource(
            Vector2Int origin,
            bool allowFoul,
            out WorldWaterSourceSnapshot source)
        {
            source = default;
            return false;
        }
        public bool TryDrink(
            string sourceId,
            float amount,
            out WorldWaterQuality quality,
            out float consumed)
        {
            quality = default;
            consumed = 0f;
            return false;
        }
        public List<WorldWaterSourceSaveData> CaptureWaterSources() => new();
        public void RestoreWaterSources(
            IEnumerable<WorldWaterSourceSaveData> saveData,
            int nextSequence)
        {
        }
        public bool DebugCreateSource(
            Vector2Int position,
            WorldWaterQuality quality,
            float capacity,
            GridCellTerrainType terrainType,
            out string sourceId)
        {
            sourceId = string.Empty;
            return false;
        }
        public bool DebugSetSource(
            string sourceId,
            WorldWaterQuality quality,
            float capacity,
            float remaining) => false;
    }

    private static JObject CaptureStats(CharacterActor actor)
    {
        CharacterMoodSnapshot mood = actor?.Mood
            ?? throw new InvalidOperationException(
                "Causal primitive-survival actor mood is unavailable.");
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
        {
            throw new InvalidOperationException(
                "Causal primitive-survival actor stats are missing.");
        }
        RequireFields(
            value,
            "causal primitive-survival actor stats",
            "conditions",
            "currentHealth",
            "injurySeverity",
            "baseMood",
            "moodFactors");
        if (value["conditions"] is not JArray conditions
            || value["moodFactors"] is not JArray factors)
        {
            throw new InvalidOperationException(
                "Causal primitive-survival stat arrays are invalid.");
        }
        var restoredStats = new Dictionary<CharacterCondition, float>();
        foreach (JObject row in conditions.Cast<JObject>())
        {
            RequireFields(
                row,
                "causal primitive-survival condition",
                "condition",
                "value");
            if (!Enum.TryParse(
                    (string)row["condition"],
                    ignoreCase: false,
                    out CharacterCondition condition)
                || !restoredStats.TryAdd(condition, (float)row["value"]))
            {
                throw new InvalidOperationException(
                    "Causal primitive-survival condition is unknown or duplicated.");
            }
        }
        var restoredFactors = new List<CharacterMoodFactorSnapshot>();
        string previousFactor = string.Empty;
        foreach (JObject row in factors.Cast<JObject>())
        {
            RequireFields(
                row,
                "causal primitive-survival mood factor",
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
                    "Causal primitive-survival mood factors are invalid or unordered.");
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
        string[] actual = value?.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields changed; expected=" + string.Join(",", wanted)
                + ";actual=" + string.Join(",", actual));
        }
    }

    private static float ReadFloat(object value) =>
        Convert.ToSingle(value, CultureInfo.InvariantCulture);

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.001f;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
