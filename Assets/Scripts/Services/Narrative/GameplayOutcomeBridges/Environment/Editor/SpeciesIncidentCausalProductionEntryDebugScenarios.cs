#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
{
    internal const string SpeciesCausalOwnerRestoreKind =
        "owner-family:species-incident";
    internal const string SpeciesFirstIncidentActionId =
        "action:environment-species-golem-overload";
    internal const string SpeciesRepeatIncidentActionId =
        "action:environment-species-golem-overload-repeat";

    private const string SpeciesCausalOwnerKind =
        "owner-kind:species-incident";
    private const string SpeciesCausalStateSchema =
        "dungeonstory-causal-species-incident-owner-state@1";

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateSpeciesCausalFixtures(
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
                "A species-incident checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalSpeciesIncidentContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                SpeciesFirstIncidentActionId,
                "environment",
                EnvironmentOutcomeIds.SpeciesIncidentTriggered,
                context.CaptureState,
                context.TriggerFirstIncident,
                context.RequireFirstOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? SpeciesCausalOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                SpeciesRepeatIncidentActionId,
                "environment",
                EnvironmentOutcomeIds.SpeciesIncidentTriggered,
                () => new Dictionary<string, object>(),
                context.TriggerRepeatIncident,
                context.RequireRepeatOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.OwnerId,
                context.OwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? SpeciesCausalOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunSpeciesCausalOwnerFamily(bool log = false)
    {
        const string ownerId = "species-owner:causal-golem-overload:focused";
        JObject firstCheckpoint;
        using (var fixtures = new SpeciesCausalFixtureSet(
                   CreateSpeciesCausalFixtures(
                       "run:causal-species-incident:first",
                       ownerId)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                SpeciesFirstIncidentActionId);
            IReadOnlyDictionary<string, object> before = first.CaptureState();
            RequireSpeciesCausal(
                (int)before["speciesIncident.stage"] == 0
                && (int)before["speciesIncident.incidentCount"] == 0
                && !(bool)before["speciesIncident.facilityDamaged"]
                && (int)before["speciesIncident.exactOutcomeCount"] == 0
                && (long)before["speciesIncident.golemEpisodeRevision"] == 0L
                && !(bool)before["speciesIncident.dischargeEpisodeActive"],
                "Species-incident owner did not start from the expected live state.");
            RequireSpeciesCausal(first.Execute(),
                "The first real Golem overload did not execute.");
            RequireSpeciesCausal(
                first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first species incident has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            RequireSpeciesCausal(
                (int)after["speciesIncident.stage"] == 1
                && (int)after["speciesIncident.incidentCount"] == 1
                && (bool)after["speciesIncident.facilityDamaged"]
                && (int)after["speciesIncident.exactOutcomeCount"] == 1
                && (long)after["speciesIncident.golemEpisodeRevision"] == 0L,
                "The first overload did not retain species, facility, and ledger state.");
            firstCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new SpeciesCausalFixtureSet(
                   CreateSpeciesCausalFixtures(
                       "run:causal-species-incident:repeat",
                       ownerId,
                       firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                SpeciesFirstIncidentActionId);
            IProductionCausalActionFixture repeat = fixtures.Get(
                SpeciesRepeatIncidentActionId);
            IReadOnlyDictionary<string, object> restored = first.CaptureState();
            float healthAfterFirst =
                (float)restored["speciesIncident.actorHealth"];
            RequireSpeciesCausal(
                (int)restored["speciesIncident.stage"] == 1
                && (int)restored["speciesIncident.incidentCount"] == 1
                && (bool)restored["speciesIncident.facilityDamaged"]
                && !first.Execute(),
                "Fresh composition restore lost or replayed the first overload.");
            RequireSpeciesCausal(repeat.Execute(),
                "The repeated real Golem overload did not execute.");
            RequireSpeciesCausal(
                repeat.RequireExactOutcome().HasCanonicalPayloadHash,
                "The repeated species incident has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            RequireSpeciesCausal(
                (int)after["speciesIncident.stage"] == 2
                && (int)after["speciesIncident.incidentCount"] == 2
                && (float)after["speciesIncident.actorHealth"] < healthAfterFirst
                && (bool)after["speciesIncident.facilityDamaged"]
                // The real cooldown also crosses charge exhaustion: two incidents
                // plus one authoritative discharge/body-health result must persist.
                && (int)after["speciesIncident.exactOutcomeCount"] == 3
                && (long)after["speciesIncident.golemEpisodeRevision"] == 1L
                && (bool)after["speciesIncident.dischargeEpisodeActive"],
                "The repeated overload did not consume the restored owner state.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new SpeciesCausalFixtureSet(
                   CreateSpeciesCausalFixtures(
                       "run:causal-species-incident:terminal",
                       ownerId,
                       terminalCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                SpeciesFirstIncidentActionId);
            IProductionCausalActionFixture repeat = fixtures.Get(
                SpeciesRepeatIncidentActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            RequireSpeciesCausal(!first.Execute() && !repeat.Execute(),
                "Terminal species-incident owner was not idempotent.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
            RequireSpeciesCausal(JToken.DeepEquals(before, after),
                "Rejected species-incident replay mutated the owner checkpoint.");
        }

        JObject actorTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)actorTamper["domainState"])["actorId"] =
            "character:causal-species:tampered";
        RequireSpeciesCausal(
            RejectSpeciesCheckpoint(ownerId, actorTamper, "actor-tamper"),
            "A tampered species actor identity was accepted.");

        JObject countTamper = (JObject)firstCheckpoint.DeepClone();
        JObject tamperedDomain = (JObject)countTamper["domainState"];
        JObject tamperedSpecies = (JObject)tamperedDomain["speciesState"];
        JArray tamperedCharacters = (JArray)tamperedSpecies["characters"];
        ((JObject)tamperedCharacters[0])["incidentCount"] = 0;
        RequireSpeciesCausal(
            RejectSpeciesCheckpoint(ownerId, countTamper, "count-tamper"),
            "A species incident-count/ledger mismatch was accepted.");

        if (log)
            Debug.Log("Golem overload -> restored recurrence causal owner family PASS");
        return true;
    }

    public static void RunSpeciesCausalBatch()
    {
        try
        {
            RequireSpeciesCausal(RunSpeciesCausalOwnerFamily(log: true),
                "Species-incident causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static bool RejectSpeciesCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new SpeciesCausalFixtureSet(
                CreateSpeciesCausalFixtures(
                    "run:causal-species-incident:" + suffix,
                    ownerId,
                    checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class CausalSpeciesIncidentContext : IDisposable
    {
        private readonly string ownerId;
        private readonly string actorId;
        private readonly string facilityId;
        private readonly Fixture fixture;
        private readonly CharacterSpeciesRuntime runtime;
        private bool firstExecuted;
        private bool repeatExecuted;
        private bool disposed;

        internal CausalSpeciesIncidentContext(
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
            actorId = "character:causal-species-golem:" + scope;
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    SpeciesCausalOwnerKind)
                : causalRunId + ":species-incident-ledger";
            fixture = new Fixture(ledgerRunId, actorId);
            RoomWorld room = RoomWorld.Create(fixture.World, scope);
            fixture.World.SetGrid(room.Grid);
            fixture.AdjacentIncidentBuilding = room.LeftDoor;
            fixture.Retain(room);
            facilityId = room.LeftDoor.RequirePersistentInstanceId().Value;
            runtime = CreateSpeciesRuntime(fixture);

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
            ? SpeciesCausalOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            CharacterSpeciesRuntimeState state = RequireSpeciesState();
            return new Dictionary<string, object>
            {
                ["speciesIncident.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "species-owner:ephemeral-golem-overload",
                ["speciesIncident.actorId"] = actorId,
                ["speciesIncident.facilityId"] = facilityId,
                ["speciesIncident.stage"] = Stage,
                ["speciesIncident.speciesId"] = state.SpeciesId.Value,
                ["speciesIncident.incidentId"] = state.LastIncidentId ?? string.Empty,
                ["speciesIncident.incidentCount"] = state.IncidentCount,
                ["speciesIncident.nextIncidentAt"] = state.NextIncidentAt,
                ["speciesIncident.charge"] = state.Charge,
                ["speciesIncident.integrity"] = state.Integrity,
                ["speciesIncident.actorHealth"] = fixture.Actor.Stats.CurrentHealth,
                ["speciesIncident.facilityDamaged"] =
                    fixture.AdjacentIncidentBuilding.IsDamaged,
                ["speciesIncident.exactOutcomeCount"] = ExactOutcomeCount(),
                ["speciesIncident.golemEpisodeRevision"] = state.GolemEpisodeRevision,
                ["speciesIncident.dischargeEpisodeActive"] = state.DischargeEpisodeActive,
                ["speciesIncident.firstExecuted"] = firstExecuted,
                ["speciesIncident.repeatExecuted"] = repeatExecuted
            };
        }

        internal bool TriggerFirstIncident()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            runtime.Tick();
            if (!RequireOutcome(1L, throwOnFailure: false)
                    .HasCanonicalPayloadHash)
                return false;
            firstExecuted = true;
            ValidateState();
            return true;
        }

        internal bool TriggerRepeatIncident()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            CharacterSpeciesRuntimeState state = RequireSpeciesState();
            float incidentAt = state.NextIncidentAt;
            while (fixture.Clock.Time + 1f < incidentAt)
            {
                fixture.Clock.Time += 1f;
                runtime.Tick();
            }
            fixture.Clock.Time = incidentAt;
            runtime.Tick();
            if (!RequireOutcome(2L, throwOnFailure: false)
                    .HasCanonicalPayloadHash)
                return false;
            repeatExecuted = true;
            ValidateState();
            return true;
        }

        internal GameplayOutcomeReplayIdentity RequireFirstOutcome() =>
            RequireOutcome(1L, throwOnFailure: true);

        internal GameplayOutcomeReplayIdentity RequireRepeatOutcome() =>
            RequireOutcome(2L, throwOnFailure: true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(fixture.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral species-incident fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                SpeciesCausalOwnerKind,
                SpeciesCausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = SpeciesCausalStateSchema,
                    ["actorId"] = actorId,
                    ["facilityId"] = facilityId,
                    ["stage"] = Stage,
                    ["speciesState"] = JObject.FromObject(runtime.Capture()),
                    ["actorStats"] = CaptureSpeciesActorStats(fixture.Actor),
                    ["facilityDamaged"] =
                        fixture.AdjacentIncidentBuilding.IsDamaged,
                    ["clockTime"] = fixture.Clock.Time,
                    ["firstExecuted"] = firstExecuted,
                    ["repeatExecuted"] = repeatExecuted
                },
                fixture.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            fixture?.Dispose();
        }

        private int Stage => repeatExecuted ? 2 : firstExecuted ? 1 : 0;

        private void PrepareInitialState()
        {
            var initial = new CharacterSpeciesRuntimeSaveData
            {
                characters = new List<CharacterSpeciesRuntimeRecordSaveData>
                {
                    new()
                    {
                        characterInstanceId = actorId,
                        speciesDefinitionId = "Golem",
                        charge = 5f,
                        integrity = 100f,
                        nextIncidentAt = 0f,
                        lastIncidentId = string.Empty,
                        incidentCount = 0,
                        rechargeWorkerId = string.Empty,
                        rechargeFacilityId = string.Empty,
                        rechargeMaterialStackId = string.Empty
                    }
                }
            };
            runtime.Restore(runtime.BuildRestore(initial));
            fixture.Clock.Time = 2f;
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                SpeciesCausalOwnerKind,
                SpeciesCausalOwnerRestoreKind,
                fixture.Ledger);
            RequireSpeciesFields(
                state,
                "causal species-incident owner state",
                "schemaVersion",
                "schema",
                "actorId",
                "facilityId",
                "stage",
                "speciesState",
                "actorStats",
                "facilityDamaged",
                "clockTime",
                "firstExecuted",
                "repeatExecuted");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    SpeciesCausalStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["actorId"], actorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["facilityId"], facilityId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Causal species-incident identities changed.");
            }

            CharacterSpeciesRuntimeSaveData speciesState = state["speciesState"]
                ?.ToObject<CharacterSpeciesRuntimeSaveData>()
                ?? throw new InvalidOperationException(
                    "Causal species state is missing.");
            CharacterSpeciesRuntimeRecordSaveData record =
                speciesState.characters?.SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "Causal species state must contain exactly one actor.");
            if (!string.Equals(
                    record.characterInstanceId, actorId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    record.speciesDefinitionId, "Golem",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Causal species state changed actor or species identity.");
            }

            RestoreSpeciesActorStats(fixture.Actor, state["actorStats"]);
            fixture.AdjacentIncidentBuilding.SetDamaged(
                (bool)state["facilityDamaged"]);
            fixture.Clock.Time = (float)state["clockTime"];
            runtime.Restore(runtime.BuildRestore(speciesState));
            firstExecuted = (bool)state["firstExecuted"];
            repeatExecuted = (bool)state["repeatExecuted"];
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Causal species-incident stage disagrees with action history.");
            }
        }

        private void ValidateState()
        {
            CharacterSpeciesRuntimeState state = RequireSpeciesState();
            if (repeatExecuted && !firstExecuted)
            {
                throw new InvalidOperationException(
                    "A repeated species incident cannot precede the first incident.");
            }
            bool incidentExpected = Stage > 0;
            float health = fixture.Actor.Stats.CurrentHealth;
            float expectedHealth = fixture.Actor.Stats.MaxHealth
                * (1f - 0.08f * Stage);
            bool healthMatches = Stage < 2
                ? Mathf.Approximately(health, expectedHealth)
                : health > 0f
                    && health < fixture.Actor.Stats.MaxHealth * 0.84f;
            int expectedDischarges = Stage == 2 ? 1 : 0;
            if (state.IncidentCount != Stage
                || ExactOutcomeCount() != Stage + expectedDischarges
                || state.GolemEpisodeRevision != expectedDischarges
                || state.DischargeEpisodeActive != (expectedDischarges == 1)
                || !state.SpeciesId.Equals(new CharacterSpeciesId("Golem"))
                || (incidentExpected
                    != string.Equals(
                        state.LastIncidentId,
                        CharacterSpeciesIncidentIds.GolemCoreOverload,
                        StringComparison.Ordinal))
                || fixture.AdjacentIncidentBuilding.IsDamaged != incidentExpected
                || !healthMatches)
            {
                throw new InvalidOperationException(
                    "Causal species, actor, facility, and ledger state diverged. "
                    + $"stage={Stage}; incidentCount={state.IncidentCount}; "
                    + $"exactOutcomeCount={ExactOutcomeCount()}; "
                    + $"speciesId={state.SpeciesId.Value}; "
                    + $"lastIncidentId={state.LastIncidentId}; "
                    + $"facilityDamaged={fixture.AdjacentIncidentBuilding.IsDamaged}; "
                    + $"health={fixture.Actor.Stats.CurrentHealth}; "
                    + $"expectedHealth={expectedHealth}; "
                    + $"maxHealth={fixture.Actor.Stats.MaxHealth}; "
                    + $"charge={state.Charge}; integrity={state.Integrity}.");
            }
            if (firstExecuted)
                _ = RequireOutcome(1L, throwOnFailure: true);
            if (repeatExecuted)
                _ = RequireOutcome(2L, throwOnFailure: true);
            fixture.RequirePublished(
                EnvironmentOutcomeIds.GolemUpkeepProducer,
                EnvironmentOutcomeIds.GolemUpkeepEpisode,
                GameplayOutcomeStatus.Succeeded,
                expectedDischarges);
            if (expectedDischarges == 1)
            {
                var key = new GameplayResultKey(
                    EnvironmentOutcomeIds.GolemUpkeepProducer,
                    new GameplayOperationId("golem-discharge:" + actorId + ":1"),
                    1L,
                    0);
                RequireSpeciesCausal(fixture.Ledger.TryGetResultIdentity(key, out var identity)
                    && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                    && identity.HasCanonicalPayloadHash
                    && fixture.Ledger.TryGetExact(identity.OutcomeId, out var exact)
                    && exact.facts.Any(value => value.factId == EnvironmentOutcomeIds.EpisodeKindFact.Value
                        && value.value == "discharge")
                    && exact.facts.Any(value => value.factId == EnvironmentOutcomeIds.BodyChildFact.Value
                        && value.value.Contains("subject=" + actorId + "|", StringComparison.Ordinal)
                        && value.value.Contains("reason=species:golem-discharge|", StringComparison.Ordinal)
                        && value.value.Contains("applied=", StringComparison.Ordinal)),
                    "The cooldown discharge lost its exact owner identity or physical body-health child.");
            }
        }

        private CharacterSpeciesRuntimeState RequireSpeciesState()
        {
            if (!runtime.TryGet(
                    new CharacterId(actorId),
                    out CharacterSpeciesRuntimeState state)
                || state == null)
            {
                throw new InvalidOperationException(
                    "Causal Golem species state is unavailable.");
            }
            return state;
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            long revision,
            bool throwOnFailure)
        {
            GameplayResultKey key = new(
                EnvironmentOutcomeIds.SpeciesIncidentProducer,
                new GameplayOperationId(
                    "species-incident:" + actorId + ":" + (revision - 1L)),
                revision,
                0);
            bool valid = fixture.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && fixture.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(
                    exact.producerId,
                    EnvironmentOutcomeIds.SpeciesIncidentProducer,
                    StringComparison.Ordinal)
                && string.Equals(
                    exact.outcomeTypeId,
                    EnvironmentOutcomeIds.SpeciesIncidentTriggered.Value,
                    StringComparison.Ordinal)
                && exact.status == GameplayOutcomeStatus.Failed
                && exact.ownerRevision == revision
                && exact.commitRevision == revision;
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal species incident did not publish exact revision "
                    + revision + ".");
            }
            return valid ? identity : default;
        }

        private int ExactOutcomeCount() => fixture.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(16), OutcomeFilter.All).Items.Count;

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private static CharacterSpeciesRuntime CreateSpeciesRuntime(Fixture fixture)
    {
        IWorldFilthQuery filth = Proxy<IWorldFilthQuery>();
        SpeciesIncidentHandlerRegistry handlers = new(
            Proxy<IWorldItemStackRuntime>(),
            Proxy<IPhysicalItemRelocationService>(),
            filth,
            Proxy<IWorldWaterContaminationCommand>(),
            fixture.World);
        return new CharacterSpeciesRuntime(
            fixture.World,
            CharacterAiEditorTestDependencies.CharacterSpeciesCatalog,
            handlers,
            fixture.Clock,
            fixture.Events,
            new DungeonRuntimeAggregateRootStore(),
            CharacterAiEditorTestDependencies.BodyHealthRuntime,
            Proxy<IAnatomyProfileCatalog>(),
            new CharacterPerformanceFormulaCatalog(
                CharacterAiEditorTestDependencies.ContentDefinitions),
            new FixedRunSeedProvider(61317),
            Proxy<IStockQuery>(),
            Proxy<IItemReservationService>(),
            Proxy<IAtomicItemConsumptionService>(),
            filth,
            Proxy<IWorldWaterQuery>(),
            CharacterAiEditorTestDependencies.BodyHealthRuntime,
            fixture.Facts,
            fixture.Facts,
            fixture.Calendar,
            Proxy<IPreparedPhysicalItemRelocationService>());
    }

    private sealed class SpeciesCausalFixtureSet : IDisposable
    {
        private readonly Dictionary<string, IProductionCausalActionFixture>
            fixtures;

        internal SpeciesCausalFixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> source)
        {
            fixtures = (source
                    ?? throw new ArgumentNullException(nameof(source)))
                .ToDictionary(value => value.ActionId, StringComparer.Ordinal);
        }

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.TryGetValue(actionId, out IProductionCausalActionFixture value)
                ? value
                : throw new InvalidOperationException(
                    "Missing species-incident causal fixture: " + actionId);

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures.Values)
                fixture.Dispose();
        }
    }

    private static JObject CaptureSpeciesActorStats(CharacterActor actor)
    {
        CharacterMoodSnapshot mood = actor?.Mood
            ?? throw new InvalidOperationException(
                "Causal species actor mood is unavailable.");
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

    private static void RestoreSpeciesActorStats(
        CharacterActor actor,
        JToken token)
    {
        if (actor == null || token is not JObject value)
            throw new InvalidOperationException(
                "Causal species actor stats are missing.");
        RequireSpeciesFields(
            value,
            "causal species actor stats",
            "conditions",
            "currentHealth",
            "injurySeverity",
            "baseMood",
            "moodFactors");
        if (value["conditions"] is not JArray conditions
            || value["moodFactors"] is not JArray factors)
        {
            throw new InvalidOperationException(
                "Causal species stat arrays are invalid.");
        }

        var restoredStats = new Dictionary<CharacterCondition, float>();
        foreach (JObject row in conditions.Cast<JObject>())
        {
            RequireSpeciesFields(
                row,
                "causal species condition",
                "condition",
                "value");
            if (!Enum.TryParse(
                    (string)row["condition"],
                    ignoreCase: false,
                    out CharacterCondition condition)
                || !restoredStats.TryAdd(condition, (float)row["value"]))
            {
                throw new InvalidOperationException(
                    "Causal species condition is unknown or duplicated.");
            }
        }

        var restoredFactors = new List<CharacterMoodFactorSnapshot>();
        string previousFactor = string.Empty;
        foreach (JObject row in factors.Cast<JObject>())
        {
            RequireSpeciesFields(
                row,
                "causal species mood factor",
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
                    "Causal species mood factors are invalid or unordered.");
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

    private static void RequireSpeciesFields(
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

    private static void RequireSpeciesCausal(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
