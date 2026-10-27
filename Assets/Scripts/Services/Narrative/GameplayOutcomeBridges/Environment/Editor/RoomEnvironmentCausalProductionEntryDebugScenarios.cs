#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class RoomEnvironmentCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:room-environment-experience";
    internal const string ExperienceActionId =
        "action:environment-room-experience-apply";
    internal const string ConditionActionId =
        "action:environment-room-condition-change";

    private const string OwnerKind =
        "owner-kind:room-environment-experience";
    private const string StateSchema =
        "dungeonstory-causal-room-environment-owner-state@2";
    private const int SessionAbsoluteDay = 137;
    private const int SessionHour = 9;

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
                "A room-environment checkpoint requires an owner ID.",
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
            Fixture(
                ExperienceActionId,
                EnvironmentOutcomeIds.RoomExperienceApplied,
                context,
                context.ApplyInitialExperience,
                context.RequireInitialExperience,
                lifetime),
            Fixture(
                ConditionActionId,
                EnvironmentOutcomeIds.RoomConditionChanged,
                context,
                context.ApplyDeterioratedCondition,
                context.RequireConditionChange,
                lifetime)
        };
    }

    internal static bool RunAll(bool log = false)
    {
        const string ownerId =
            "room-environment:causal-focused:research-room";
        JObject experienceCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-room-environment:experience",
                   ownerId)))
        {
            IProductionCausalActionFixture experience = fixtures.Get(
                ExperienceActionId);
            IReadOnlyDictionary<string, object> before =
                experience.CaptureState();
            Require((int)before["roomEnvironment.stage"] == 0
                    && (int)before["roomEnvironment.exactOutcomeCount"] == 0
                    && !(bool)before["roomEnvironment.experienceApplied"],
                "Room environment owner did not start at the canonical baseline.");
            Require(experience.Execute(),
                "The real room experience service rejected the clean-room event.");
            Require(experience.RequireExactOutcome().HasCanonicalPayloadHash,
                "The clean-room experience has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after =
                experience.CaptureState();
            Require((int)after["roomEnvironment.stage"] == 1
                    && (int)after["roomEnvironment.nextOutcomeSequence"] == 2
                    && (int)after["roomEnvironment.exactOutcomeCount"] == 1
                    && (bool)after["roomEnvironment.experienceApplied"]
                    && !(bool)after["roomEnvironment.conditionChanged"],
                "The initial room experience did not retain its owner state.");
            experienceCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)experience)
                .CaptureOwnerCheckpoint();
        }

        JObject conditionCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-room-environment:condition",
                   ownerId,
                   experienceCheckpoint)))
        {
            IProductionCausalActionFixture experience = fixtures.Get(
                ExperienceActionId);
            IProductionCausalActionFixture condition = fixtures.Get(
                ConditionActionId);
            IReadOnlyDictionary<string, object> restored =
                experience.CaptureState();
            Require((int)restored["roomEnvironment.stage"] == 1
                    && !experience.Execute(),
                "Fresh composition restore lost or replayed the room experience.");
            Require(condition.Execute(),
                "The real room service rejected the cleanliness transition.");
            Require(condition.RequireExactOutcome().HasCanonicalPayloadHash,
                "The room condition change has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = condition.CaptureState();
            Require((int)after["roomEnvironment.stage"] == 2
                    && (int)after["roomEnvironment.nextOutcomeSequence"] == 4
                    && (int)after["roomEnvironment.exactOutcomeCount"] == 3
                    && (bool)after["roomEnvironment.experienceApplied"]
                    && (bool)after["roomEnvironment.conditionChanged"],
                "The dirty-room condition and consequence were not retained.");
            conditionCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)condition)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-room-environment:terminal",
                   ownerId,
                   conditionCheckpoint)))
        {
            IProductionCausalActionFixture experience = fixtures.Get(
                ExperienceActionId);
            IProductionCausalActionFixture condition = fixtures.Get(
                ConditionActionId);
            IReadOnlyDictionary<string, object> restored =
                experience.CaptureState();
            Require((int)restored["roomEnvironment.stage"] == 2
                    && !experience.Execute()
                    && !condition.Execute(),
                "Terminal room-environment owner was not idempotent.");
        }

        JObject tampered = (JObject)experienceCheckpoint.DeepClone();
        ((JObject)tampered["domainState"])["facilityId"] =
            "building:causal-room:tampered:research-desk";
        bool rejected = false;
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-room-environment:tampered",
                ownerId,
                tampered));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected,
            "A tampered room facility identity was accepted across restore.");
        using (var pending = new Context("run:room-observer-pending", null, null))
            pending.VerifyPendingObservationRestore();

        if (log)
        {
            Debug.Log(
                "Room experience -> room condition change causal owner family PASS");
        }
        return true;
    }

    public static void RunBatch()
    {
        try
        {
            Require(RunAll(log: true),
                "Room-environment causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static DelegateProductionCausalActionFixture Fixture(
        string actionId,
        GameplayOutcomeTypeId outcomeTypeId,
        Context context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) => new(
        actionId,
        "environment",
        outcomeTypeId,
        context.CaptureState,
        execute,
        requireOutcome,
        context.CaptureOutcomeEvidence,
        lifetime.Release,
        context.PersistentOwnerId,
        context.PersistentOwnerKind,
        context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
        context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
        context.HasPersistentOwner ? context : null);

    private sealed class DeferredFactPublisher : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;
        public DeferredFactPublisher(IDomainFactPublisher inner) => this.inner = inner;
        public bool Uncertain;
        public bool TryReserve<TFact>(in TFact proposed, out DomainFactLease lease, out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);
        public DomainFactDelivery Publish<TFact>(in DomainFactLease lease, in TFact actual)
        {
            var delivered = inner.Publish(lease, actual);
            return Uncertain && delivered.MayBeCommitted
                ? DomainFactDelivery.Uncertain("room-smoke-hidden-delivery") : delivered;
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<TFact>(in TFact fact) => Uncertain
            ? DomainFactDelivery.Uncertain("room-smoke-replay-held") : inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly string actorId;
        private readonly GameObject actorObject;
        private readonly CharacterSO actorData;
        private readonly CharacterAiWorldRegistry world;
        private readonly GameEventBus events;
        private readonly EditorFixedGameCalendar calendar;
        private readonly GameplayOutcomeLedger ledger;
        private readonly DomainFactDispatcher factDispatcher;
        private readonly DeferredFactPublisher deferredFacts;
        private readonly EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
            .RoomWorld roomWorld;
        private readonly RoomEnvironmentEvaluator evaluator;
        private readonly RoomEnvironmentExperienceService service;
        private readonly RoomEnvironmentSnapshot cleanSnapshot;
        private readonly RoomEnvironmentSnapshot dirtySnapshot;
        private readonly CharacterActor actor;
        private readonly string facilityId;
        private readonly string roomId;
        private int stage;
        private long initialExperienceRevision;
        private long conditionRevision;
        private long dirtyExperienceRevision;
        private bool disposed;

        internal Context(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            actorId = "character:causal-room:" + scope;
            events = new GameEventBus();
            // Recreated checkpoint segments use the same explicit session date.
            calendar = new EditorFixedGameCalendar(
                SessionAbsoluteDay,
                SessionHour);
            world = new CharacterAiWorldRegistry(
                new SceneRuntimeRegistry<CharacterActor>(),
                new SceneRuntimeRegistry<WildlifeActor>(),
                new SceneRuntimeRegistry<BuildableObject>(),
                new SceneRuntimeRegistry<IWarehouseFacility>(),
                new SceneRuntimeRegistry<IRetailFacility>(),
                Proxy<IGameSessionStateProvider>(),
                Proxy<IRestoreWorldCandidateQuery>(),
                Proxy<ICharacterLifePublicationService>());

            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "CausalRoomEnvironmentWorker");
            actorData = CharacterAiPlanDebugFixtures.CreateCharacterData(
                CharacterType.NPC,
                "은서",
                "Human");
            actor = actorObject.GetComponent<CharacterActor>();
            actor.Initialize(actorData);
            actor.Identity.SetPersistentId(new CharacterId(actorId));
            world.RegisterCharacter(actor);

            roomWorld = EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                .RoomWorld.Create(world, scope);
            world.SetGrid(roomWorld.Grid);
            facilityId = roomWorld.MainFixture.RequirePersistentInstanceId().Value;
            evaluator = roomWorld.CreateEvaluator();
            cleanSnapshot = evaluator.Evaluate(
                roomWorld.Grid,
                roomWorld.GetMainRoom());
            roomWorld.MainFixture.SetCleanliness(0f);
            dirtySnapshot = evaluator.Evaluate(
                roomWorld.Grid,
                roomWorld.GetMainRoom());
            roomWorld.MainFixture.SetCleanliness(100f);
            Require(!Mathf.Approximately(
                    cleanSnapshot.Cleanliness,
                    dirtySnapshot.Cleanliness),
                "The causal room fixture has no cleanliness delta.");
            RoomInstance room = roomWorld.GetMainRoom();
            roomId = $"room:{facilityId}:{room.Bounds.xMin}:{room.Bounds.yMin}:"
                + $"{room.Bounds.xMax}:{room.Bounds.yMax}";

            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new RoomConditionOutcomeDescriptor(new KoreanJosaFormatter()),
                    new RoomEnvironmentExperienceOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new RoomConditionOutcomeAdapter(),
                    new RoomEnvironmentExperienceOutcomeAdapter()
                });
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":room-environment-ledger";
            ledger = new GameplayOutcomeLedger(
                registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            GameplayOutcomeRecorder recorder = new(ledger, registry, events);
            factDispatcher = new DomainFactDispatcher();

            try
            {
                var commits = new GameplayDomainFactCommitGroup(
                    recorder,
                    ledger);
                factDispatcher.Subscribe(
                    "log:environment.room-experience",
                    new GameplayDomainFactObserver<
                        RoomEnvironmentExperienceFact,
                        RoomEnvironmentExperienceOutcomeReceipt>(
                        RoomEnvironmentFactObserverMapping.CaptureExperience,
                        registry,
                        ledger,
                        recorder,
                        commits));
                factDispatcher.Subscribe(
                    "log:environment.room-condition",
                    new GameplayDomainFactObserver<
                        RoomEnvironmentConditionFact,
                        RoomConditionOutcomeReceipt>(
                        RoomEnvironmentFactObserverMapping.CaptureCondition,
                        registry,
                        ledger,
                        recorder,
                        commits));
            deferredFacts = new DeferredFactPublisher(factDispatcher);
            service = new RoomEnvironmentExperienceService(
                    new RoomLayoutCache(),
                    evaluator,
                    roomWorld.SettingsProvider,
                    events,
                    calendar,
                    Proxy<IHeritableTraitEffectQuery>(),
                deferredFacts);
                if (checkpoint != null)
                {
                    JObject domain = ProductionCausalOwnerCheckpoint
                        .RestoreFamilyCommon(
                            checkpoint,
                            this.ownerId,
                            OwnerKind,
                            CausalOwnerRestoreKind,
                            ledger);
                    RestoreDomainState(domain);
                }
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
            RoomEnvironmentOutcomeSaveData outcomeState =
                service.CaptureOutcomeState();
            RoomEnvironmentSnapshot snapshot = evaluator.Evaluate(
                roomWorld.Grid,
                roomWorld.GetMainRoom());
            CharacterMoodSnapshot mood = actor.Stats.GetMoodSnapshot();
            return new Dictionary<string, object>
            {
                ["roomEnvironment.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "room-environment:ephemeral",
                ["roomEnvironment.actorId"] = actorId,
                ["roomEnvironment.facilityId"] = facilityId,
                ["roomEnvironment.roomId"] = roomId,
                ["roomEnvironment.stage"] = stage,
                ["roomEnvironment.facilityCleanliness"] =
                    roomWorld.MainFixture.FacilityState.cleanliness,
                ["roomEnvironment.observedCleanliness"] =
                    outcomeState.observations.Count == 0
                        ? -1f
                        : outcomeState.observations.Single().cleanliness,
                ["roomEnvironment.evaluatedCleanliness"] = snapshot.Cleanliness,
                ["roomEnvironment.impressiveness"] = snapshot.Impressiveness,
                ["roomEnvironment.nextOutcomeSequence"] =
                    (int)outcomeState.nextOutcomeSequence,
                ["roomEnvironment.mood"] = mood.Value,
                ["roomEnvironment.roomMoodFactorCount"] = RoomFactors(mood).Count,
                ["roomEnvironment.exactOutcomeCount"] = ledger
                    .CaptureGameplayOutcomes().exactOutcomes.Count,
                ["roomEnvironment.experienceApplied"] = stage >= 1,
                ["roomEnvironment.conditionChanged"] = stage >= 2
            };
        }

        internal void VerifyPendingObservationRestore()
        {
            deferredFacts.Uncertain = true;
            bool blocked = false;
            try { ApplyInitialExperience(); }
            catch (InvalidOperationException) { blocked = true; }
            var saved = JsonUtility.FromJson<RoomEnvironmentOutcomeSaveData>(
                JsonUtility.ToJson(service.CaptureOutcomeState()));
            Require(blocked && saved.pendingFacts.Count == 1 && saved.pendingFacts[0].kind == "experience"
                && saved.nextOutcomeSequence == 1L, "Uncertain room fact was not saved before advancing its sequence.");
            int rows = ledger.CaptureGameplayOutcomes().exactOutcomes.Count;
            float mood = actor.Stats.GetMoodSnapshot().Value;
            int factors = RoomFactors(actor.Stats.GetMoodSnapshot()).Count;
            Require(rows == 1 && factors > 0, "Pending room fixture did not apply one real mood effect and outcome.");
            var restored = new RoomEnvironmentExperienceService(new RoomLayoutCache(), evaluator,
                roomWorld.SettingsProvider, events, calendar, Proxy<IHeritableTraitEffectQuery>(), deferredFacts);
            restored.PublishOutcomeRestore(restored.PrepareOutcomeRestore(saved));
            Require(restored.CaptureOutcomeState().pendingFacts.Count == 1,
                "Fresh room owner lost its serialized pending fact.");
            deferredFacts.Uncertain = false;
            Require(!restored.Apply(default), "A null room action unexpectedly applied gameplay effects.");
            var completed = restored.CaptureOutcomeState();
            Require(completed.pendingFacts.Count == 0 && completed.nextOutcomeSequence == 2L
                && ledger.CaptureGameplayOutcomes().exactOutcomes.Count == rows
                && actor.Stats.GetMoodSnapshot().Value == mood
                && RoomFactors(actor.Stats.GetMoodSnapshot()).Count == factors,
                "Pending room replay duplicated its outcome or mood effect.");
            restored.Apply(default);
            Require(ledger.CaptureGameplayOutcomes().exactOutcomes.Count == rows,
                "Completed pending room fact was delivered twice.");
        }

        internal bool ApplyInitialExperience()
        {
            ThrowIfDisposed();
            if (stage != 0)
                return false;
            int exactBefore = ExactOutcomeCount();
            bool applied = service.Apply(new RoomEnvironmentExperienceEvent(
                actor,
                roomWorld.MainFixture,
                RoomExperienceActivity.Work,
                BuiltInWorkTypeIds.Research));
            if (!applied || ExactOutcomeCount() != exactBefore + 1)
                return false;
            initialExperienceRevision = 1L;
            stage = 1;
            ValidateState();
            return RequireInitialExperience(false).HasCanonicalPayloadHash;
        }

        internal bool ApplyDeterioratedCondition()
        {
            ThrowIfDisposed();
            if (stage != 1)
                return false;
            roomWorld.MainFixture.SetCleanliness(0f);
            int exactBefore = ExactOutcomeCount();
            bool applied = service.Apply(new RoomEnvironmentExperienceEvent(
                actor,
                roomWorld.MainFixture,
                RoomExperienceActivity.Work,
                BuiltInWorkTypeIds.Research));
            if (!applied || ExactOutcomeCount() != exactBefore + 2)
                return false;
            conditionRevision = 2L;
            dirtyExperienceRevision = 3L;
            stage = 2;
            ValidateState();
            return RequireConditionChange(false).HasCanonicalPayloadHash;
        }

        internal GameplayOutcomeReplayIdentity RequireInitialExperience() =>
            RequireInitialExperience(true);

        internal GameplayOutcomeReplayIdentity RequireConditionChange() =>
            RequireConditionChange(true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
                throw new InvalidOperationException(
                    "Ephemeral room-environment fixtures have no checkpoint.");
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                CaptureDomainState(),
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            factDispatcher?.Dispose();
            roomWorld?.Dispose();
            if (actor != null)
                world?.UnregisterCharacter(actor);
            CharacterAiPlanDebugFixtures.DestroyProbeObject(actorObject);
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
            events?.Clear();
        }

        private JObject CaptureDomainState()
        {
            CharacterMoodSnapshot mood = actor.Stats.GetMoodSnapshot();
            return new JObject
            {
                ["schemaVersion"] = 2,
                ["schema"] = StateSchema,
                ["stage"] = stage,
                ["ownerId"] = ownerId,
                ["actorId"] = actorId,
                ["facilityId"] = facilityId,
                ["roomId"] = roomId,
                ["calendarDay"] = calendar.Day,
                ["calendarHour"] = calendar.Hour,
                ["facilityCleanliness"] =
                    roomWorld.MainFixture.FacilityState.cleanliness,
                ["roomOutcomeState"] = ParseJson(
                    JsonUtility.ToJson(service.CaptureOutcomeState()),
                    "room outcome state"),
                ["baseMood"] = mood.BaseValue,
                ["currentHealth"] = actor.CurrentHealth,
                ["injurySeverity"] = actor.InjurySeverity,
                ["moodFactors"] = new JArray(RoomFactors(mood).Select(value =>
                    new JObject
                    {
                        ["id"] = value.Id,
                        ["label"] = value.Label,
                        ["value"] = value.Value,
                        ["remainingSeconds"] = value.RemainingSeconds
                    })),
                ["initialExperienceRevision"] = initialExperienceRevision,
                ["conditionRevision"] = conditionRevision,
                ["dirtyExperienceRevision"] = dirtyExperienceRevision
            };
        }

        private void RestoreDomainState(JObject domain)
        {
            RequireFields(
                domain,
                "room-environment domain state",
                "schemaVersion",
                "schema",
                "stage",
                "ownerId",
                "actorId",
                "facilityId",
                "roomId",
                "calendarDay",
                "calendarHour",
                "facilityCleanliness",
                "roomOutcomeState",
                "baseMood",
                "currentHealth",
                "injurySeverity",
                "moodFactors",
                "initialExperienceRevision",
                "conditionRevision",
                "dirtyExperienceRevision");
            if ((int)domain["schemaVersion"] != 2
                || !string.Equals((string)domain["schema"], StateSchema,
                    StringComparison.Ordinal)
                || !string.Equals((string)domain["ownerId"], ownerId,
                    StringComparison.Ordinal)
                || !string.Equals((string)domain["actorId"], actorId,
                    StringComparison.Ordinal)
                || !string.Equals((string)domain["facilityId"], facilityId,
                    StringComparison.Ordinal)
                || !string.Equals((string)domain["roomId"], roomId,
                    StringComparison.Ordinal)
                || domain["roomOutcomeState"] is not JObject outcomeJson
                || domain["moodFactors"] is not JArray moodRows)
            {
                throw new InvalidOperationException(
                    "Room-environment checkpoint identity is invalid.");
            }
            calendar.SetDateTime(
                (int)domain["calendarDay"],
                (int)domain["calendarHour"]);
            int restoredStage = (int)domain["stage"];
            if (restoredStage is < 0 or > 2)
                throw new InvalidOperationException(
                    "Room-environment checkpoint stage is invalid.");
            float facilityCleanliness = (float)domain["facilityCleanliness"];
            if (!float.IsFinite(facilityCleanliness)
                || facilityCleanliness < 0f
                || facilityCleanliness > 100f)
            {
                throw new InvalidOperationException(
                    "Room-environment facility cleanliness is invalid.");
            }
            roomWorld.MainFixture.SetCleanliness(facilityCleanliness);
            RoomEnvironmentOutcomeSaveData outcomeState =
                Deserialize<RoomEnvironmentOutcomeSaveData>(
                    outcomeJson,
                    "room outcome state");
            service.PublishOutcomeRestore(
                service.PrepareOutcomeRestore(outcomeState));

            var factors = new List<CharacterMoodFactorSnapshot>();
            foreach (JObject row in moodRows.Cast<JObject>())
            {
                RequireFields(
                    row,
                    "room mood factor",
                    "id",
                    "label",
                    "value",
                    "remainingSeconds");
                string id = (string)row["id"];
                string label = (string)row["label"];
                float value = (float)row["value"];
                float remaining = (float)row["remainingSeconds"];
                if (string.IsNullOrWhiteSpace(id)
                    || string.IsNullOrWhiteSpace(label)
                    || !float.IsFinite(value)
                    || !float.IsFinite(remaining)
                    || remaining <= 0f)
                {
                    throw new InvalidOperationException(
                        "Room mood factor checkpoint row is invalid.");
                }
                factors.Add(new CharacterMoodFactorSnapshot(
                    id,
                    label,
                    value,
                    CharacterMoodFactorKind.Interaction,
                    remaining));
            }
            actor.Stats.RestorePersistentState(
                actor.Stats.StatSnapshot,
                (float)domain["currentHealth"],
                (float)domain["injurySeverity"],
                (float)domain["baseMood"],
                factors);
            stage = restoredStage;
            initialExperienceRevision =
                (long)domain["initialExperienceRevision"];
            conditionRevision = (long)domain["conditionRevision"];
            dirtyExperienceRevision =
                (long)domain["dirtyExperienceRevision"];
            ValidateState();
            if (!JToken.DeepEquals(domain, CaptureDomainState()))
            {
                throw new InvalidOperationException(
                    "Room-environment state changed during fresh restore.");
            }
        }

        private void ValidateState()
        {
            ThrowIfDisposed();
            RoomEnvironmentOutcomeSaveData outcomeState =
                service.CaptureOutcomeState();
            RoomEnvironmentSnapshot current = evaluator.Evaluate(
                roomWorld.Grid,
                roomWorld.GetMainRoom());
            float expectedCleanliness = stage < 2
                ? cleanSnapshot.Cleanliness
                : dirtySnapshot.Cleanliness;
            int expectedExactCount = stage switch
            {
                0 => 0,
                1 => 1,
                _ => 3
            };
            long expectedNextSequence = stage switch
            {
                0 => 1L,
                1 => 2L,
                _ => 4L
            };
            int expectedObservationCount = stage == 0 ? 0 : 1;
            if (!Mathf.Approximately(current.Cleanliness, expectedCleanliness)
                || outcomeState.nextOutcomeSequence != expectedNextSequence
                || outcomeState.observations.Count != expectedObservationCount
                || (expectedObservationCount == 1
                    && !Mathf.Approximately(
                        outcomeState.observations[0].cleanliness,
                        expectedCleanliness))
                || ExactOutcomeCount() != expectedExactCount)
            {
                throw new InvalidOperationException(
                    "Room-environment owner state does not match its causal stage.");
            }
            int roomFactorCount = RoomFactors(actor.Stats.GetMoodSnapshot()).Count;
            if ((stage == 0 && roomFactorCount != 0)
                || (stage >= 1 && roomFactorCount != 2))
            {
                throw new InvalidOperationException(
                    "Room-environment mood state does not match its causal stage.");
            }
            if (stage == 0
                && (initialExperienceRevision != 0L
                    || conditionRevision != 0L
                    || dirtyExperienceRevision != 0L))
            {
                throw new InvalidOperationException(
                    "Initial room-environment state contains terminal revisions.");
            }
            if (stage >= 1)
                RequireInitialExperience(false);
            if (stage >= 2)
            {
                RequireConditionChange(false);
                RequireExperience(
                    dirtyExperienceRevision,
                    dirtySnapshot,
                    false,
                    "dirty-room experience");
            }
            if (stage < 2
                && (conditionRevision != 0L || dirtyExperienceRevision != 0L))
            {
                throw new InvalidOperationException(
                    "Room condition revisions appeared before their causal stage.");
            }
        }

        private GameplayOutcomeReplayIdentity RequireInitialExperience(
            bool throwOnFailure) => RequireExperience(
                initialExperienceRevision,
                cleanSnapshot,
                throwOnFailure,
                "initial room experience");

        private GameplayOutcomeReplayIdentity RequireExperience(
            long revision,
            RoomEnvironmentSnapshot snapshot,
            bool throwOnFailure,
            string label)
        {
            if (revision <= 0L)
            {
                if (throwOnFailure)
                    throw new InvalidOperationException(
                        "The " + label + " revision is missing.");
                return default;
            }
            GameplayResultKey key = new(
                EnvironmentOutcomeIds.RoomExperienceProducer,
                new GameplayOperationId("room-experience:" + revision),
                revision,
                0);
            DungeonStory.Rooms.RoomMoodDecision expected =
                roomWorld.SettingsProvider.Settings.EvaluateMood(
                    snapshot.Impressiveness,
                    snapshot.Cleanliness);
            if (TryRequireExact(
                    key,
                    EnvironmentOutcomeIds.RoomExperienceApplied,
                    revision,
                    out GameplayOutcomeReplayIdentity identity,
                    out GameplayOutcomeSnapshot exact)
                && HasMetric(
                    exact,
                    EnvironmentOutcomeIds.ImpressionMoodMetric.Value,
                    expected.ImpressionMood)
                && HasMetric(
                    exact,
                    EnvironmentOutcomeIds.CleanlinessMoodMetric.Value,
                    expected.CleanlinessMood)
                && HasMetric(
                    exact,
                    EnvironmentOutcomeIds.SequenceMetric.Value,
                    revision)
                && HasFact(
                    exact,
                    EnvironmentOutcomeIds.ActivityFact.Value,
                    RoomExperienceActivity.Work.ToString())
                && HasFact(
                    exact,
                    EnvironmentOutcomeIds.WorkTypeFact.Value,
                    BuiltInWorkTypeIds.Research.Value)
                && HasRoomParticipants(exact))
            {
                return identity;
            }
            if (throwOnFailure)
                throw new InvalidOperationException(
                    "The " + label + " exact outcome is missing or inconsistent.");
            return default;
        }

        private GameplayOutcomeReplayIdentity RequireConditionChange(
            bool throwOnFailure)
        {
            if (conditionRevision <= 0L)
            {
                if (throwOnFailure)
                    throw new InvalidOperationException(
                        "The room condition revision is missing.");
                return default;
            }
            GameplayResultKey key = new(
                EnvironmentOutcomeIds.RoomConditionProducer,
                new GameplayOperationId("room-condition:" + conditionRevision),
                conditionRevision,
                0);
            if (TryRequireExact(
                    key,
                    EnvironmentOutcomeIds.RoomConditionChanged,
                    conditionRevision,
                    out GameplayOutcomeReplayIdentity identity,
                    out GameplayOutcomeSnapshot exact)
                && HasMetric(
                    exact,
                    EnvironmentOutcomeIds.PreviousCleanlinessMetric.Value,
                    cleanSnapshot.Cleanliness / 100f)
                && HasMetric(
                    exact,
                    EnvironmentOutcomeIds.CurrentCleanlinessMetric.Value,
                    dirtySnapshot.Cleanliness / 100f)
                && HasMetric(
                    exact,
                    EnvironmentOutcomeIds.SequenceMetric.Value,
                    conditionRevision)
                && HasRoomParticipants(exact))
            {
                return identity;
            }
            if (throwOnFailure)
                throw new InvalidOperationException(
                    "The room condition exact outcome is missing or inconsistent.");
            return default;
        }

        private bool TryRequireExact(
            GameplayResultKey key,
            GameplayOutcomeTypeId outcomeTypeId,
            long revision,
            out GameplayOutcomeReplayIdentity identity,
            out GameplayOutcomeSnapshot exact)
        {
            exact = null;
            return ledger.TryGetResultIdentity(key, out identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(identity.OutcomeId, out exact)
                && exact != null
                && string.Equals(
                    exact.outcomeTypeId,
                    outcomeTypeId.Value,
                    StringComparison.Ordinal)
                && exact.ownerRevision == revision;
        }

        private bool HasRoomParticipants(GameplayOutcomeSnapshot exact) =>
            HasParticipant(
                exact,
                EnvironmentOutcomeIds.ActorRole.Value,
                EnvironmentOutcomeIds.CharacterKind.Value,
                actorId)
            && HasParticipant(
                exact,
                EnvironmentOutcomeIds.FacilityRole.Value,
                EnvironmentOutcomeIds.FacilityKind.Value,
                facilityId)
            && HasParticipant(
                exact,
                EnvironmentOutcomeIds.RoomRole.Value,
                EnvironmentOutcomeIds.RoomKind.Value,
                roomId);

        private static bool HasParticipant(
            GameplayOutcomeSnapshot exact,
            string roleId,
            string kindId,
            string entityId) => exact.participants.Any(value => value != null
                && string.Equals(value.roleId, roleId, StringComparison.Ordinal)
                && string.Equals(value.entityKindId, kindId,
                    StringComparison.Ordinal)
                && string.Equals(value.entityId, entityId,
                    StringComparison.Ordinal));

        private static bool HasMetric(
            GameplayOutcomeSnapshot exact,
            string metricId,
            double expected) => exact.metrics.Any(value => value != null
                && string.Equals(value.metricId, metricId,
                    StringComparison.Ordinal)
                && Math.Abs(value.value - expected) < 0.0001d);

        private static bool HasFact(
            GameplayOutcomeSnapshot exact,
            string factId,
            string expected) => exact.facts.Any(value => value != null
                && string.Equals(value.factId, factId,
                    StringComparison.Ordinal)
                && string.Equals(value.value, expected,
                    StringComparison.Ordinal));

        private int ExactOutcomeCount() => ledger.CaptureGameplayOutcomes()
            .exactOutcomes.Count;

        private static IReadOnlyList<CharacterMoodFactorSnapshot> RoomFactors(
            CharacterMoodSnapshot mood) => (mood?.Factors
                ?? Array.Empty<CharacterMoodFactorSnapshot>())
            .Where(value => value != null
                && value.Kind == CharacterMoodFactorKind.Interaction
                && value.Id.StartsWith("room:", StringComparison.Ordinal))
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Context));
        }
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

    private sealed class FixedClock : IGameClock
    {
        public float DeltaTime => 1f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private static T Proxy<T>() where T : class =>
        DispatchProxy.Create<T, DefaultInterfaceProxy>();

    public class DefaultInterfaceProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private static T Deserialize<T>(JObject value, string label)
        where T : class
    {
        try
        {
            T result = JsonUtility.FromJson<T>(
                (value ?? throw new ArgumentNullException(nameof(value)))
                .ToString(Formatting.None));
            return result ?? throw new InvalidOperationException(
                "Room-environment " + label + " deserialized to null.");
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or JsonException
                                           or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Could not restore room-environment " + label + ".",
                exception);
        }
    }

    private static JObject ParseJson(string json, string label)
    {
        try
        {
            return JObject.Parse(json ?? string.Empty);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Could not serialize room-environment " + label + ".",
                exception);
        }
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        if (value == null)
            throw new InvalidOperationException(label + " is missing.");
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
