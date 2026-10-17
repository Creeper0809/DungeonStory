#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class DiseaseRouteExposureCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:environment-disease-route-exposure";
    internal const string FirstExposureActionId =
        "action:environment-disease-route-first-exposure";
    internal const string RepeatExposureActionId =
        "action:environment-disease-route-repeat-exposure";

    private const string OwnerKind =
        "owner-kind:environment-disease-route-exposure";
    private const string DomainStateSchema =
        "dungeonstory-causal-disease-route-owner-state@1";
    private const string DiseaseId = "disease:causal-wildlife-contact";
    private const string ActorId = "character:causal-disease-route:primary";
    private const string VectorId = "wildlife:causal-disease-route:vector";
    private const int FirstDay = 17;
    private const int RepeatDay = 18;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A disease-route checkpoint requires an owner ID.",
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
                FirstExposureActionId,
                context,
                context.CaptureState,
                context.PublishFirstExposure,
                context.RequireFirstOutcome,
                lifetime),
            Fixture(
                RepeatExposureActionId,
                context,
                () => new Dictionary<string, object>(),
                context.PublishRepeatExposure,
                context.RequireRepeatOutcome,
                lifetime)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string ownerId = "owner:causal-disease-route:focused";
        JObject firstCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-disease-route:first",
                   ownerId)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstExposureActionId);
            Require((int)first.CaptureState()["diseaseRoute.stage"] == 0,
                "Disease-route owner did not start at stage zero.");
            Require(first.Execute(),
                "The first wildlife disease-route production entry was rejected.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first disease-route result has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["diseaseRoute.stage"] == 1
                && (int)after["diseaseRoute.lastPublishedAbsoluteDay"] == FirstDay
                && Mathf.Approximately(
                    Convert.ToSingle(after["diseaseRoute.weightedExposureHours"]),
                    1f)
                && (int)after["diseaseRoute.healthVersion"] == 2
                && (int)after["diseaseRoute.exactOutcomeCount"] == 1,
                "First disease-route exposure state is incomplete.");
            firstCheckpoint = ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-disease-route:repeat",
                   ownerId,
                   firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(
                FirstExposureActionId);
            IProductionCausalActionFixture repeat = fixtures.Get(
                RepeatExposureActionId);
            Require(
                (int)first.CaptureState()["diseaseRoute.stage"] == 1
                && !first.Execute(),
                "Fresh composition restore lost or replayed the first exposure.");
            Require(repeat.Execute(),
                "The repeated wildlife disease-route production entry was rejected.");
            Require(repeat.RequireExactOutcome().HasCanonicalPayloadHash,
                "The repeated disease-route result has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require(
                (int)after["diseaseRoute.stage"] == 2
                && (int)after["diseaseRoute.calendarDay"] == RepeatDay
                && (int)after["diseaseRoute.lastPublishedAbsoluteDay"] == RepeatDay
                && Mathf.Approximately(
                    Convert.ToSingle(after["diseaseRoute.weightedExposureHours"]),
                    2f)
                && (int)after["diseaseRoute.healthVersion"] == 3
                && (int)after["diseaseRoute.exactOutcomeCount"] == 2,
                "Repeated exposure did not preserve and extend the health owner.");
            JObject before = ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
            Require(
                !first.Execute() && !repeat.Execute(),
                "Terminal disease-route owner was not idempotent.");
            JObject afterReplay = ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, afterReplay),
                "Rejected disease-route replay mutated the checkpoint.");
        }

        JObject cursorTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)cursorTamper["domainState"])["lastPublishedAbsoluteDay"] = 16;
        Require(RejectCheckpoint(ownerId, cursorTamper, "cursor-tamper"),
            "A disease-route publication cursor mismatch was accepted.");

        JObject healthTamper = (JObject)firstCheckpoint.DeepClone();
        JArray exposures = (JArray)((JObject)((JObject)healthTamper["domainState"])
            ["populationHealth"])["pendingExposures"];
        ((JObject)exposures[0])["weightedExposureHours"] = 2f;
        Require(RejectCheckpoint(ownerId, healthTamper, "health-tamper"),
            "A disease-route exposure-hours mismatch was accepted.");

        JObject ledgerTamper = (JObject)firstCheckpoint.DeepClone();
        ((JArray)((JObject)ledgerTamper["outcomeLedger"])["exactOutcomes"])
            .Clear();
        Require(RejectCheckpoint(ownerId, ledgerTamper, "ledger-tamper"),
            "A disease-route health/ledger mismatch was accepted.");

        if (log)
        {
            Debug.Log(
                "Disease-route day 17 -> day 18 persistent causal owner family PASS");
        }
        return true;
    }

    internal static bool RunPendingFactRoundTrip(bool log = false)
    {
        using var context = new Context(
            "run:exposure-pending-fact-roundtrip",
            null,
            null);
        context.VerifyInDoubtPendingRoundTrip();
        if (log)
            Debug.Log("Disease-route InDoubt pending fact JSON roundtrip PASS");
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Disease-route causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunPersistedIdentityBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("The persisted exposure check requires batch mode.");
        try
        {
            Require(RunCausalOwnerFamily(log: true), "Existing disease-route owner regression failed.");
            Require(RunPendingFactRoundTrip(log: true),
                "Disease-route pending fact roundtrip regression failed.");
            const string ownerId = "owner:causal-disease-route:persisted-identity";
            JObject checkpoint;
            using (var first = new Context("run:exposure-identity:first", ownerId, null))
            {
                first.VerifyRequiredSaveIdentity();
                first.VerifyRejectedCommitRollback();
                Require(first.PublishFirstExposure() && first.PublishRepeatExposure(),
                    "The two original physical exposures did not execute.");
                checkpoint = first.CaptureOwnerCheckpoint();
            }

            // A fresh composition after the SECOND exposure reproduces the old
            // transient-version collision; one restore after the first did not.
            for (int expectedStage = 3; expectedStage <= 4; expectedStage++)
            {
                checkpoint = JObject.Parse(checkpoint.ToString(Newtonsoft.Json.Formatting.None));
                using var restored = new Context("run:exposure-identity:resume", ownerId, checkpoint);
                Require(restored.PublishFurtherExposure(expectedStage),
                    "A fresh health owner reused a prior exposure result identity.");
                restored.VerifySameDayNoReplay();
                checkpoint = restored.CaptureOwnerCheckpoint();
            }
            JObject revisionTamper = (JObject)checkpoint.DeepClone();
            ((JObject)((JObject)revisionTamper["domainState"])["populationHealth"])
                ["exposureRevision"] = 1L;
            Require(RejectCheckpoint(ownerId, revisionTamper, "exposure-revision-tamper"),
                "A persisted exposure revision disagreed with its owner and ledger.");
            Require(DiseaseAnatomyBurdenProjectionDebugScenarios.RunAll(true),
                "Current-schema disease anatomy projection regression failed.");
            Debug.Log("PERSISTED_EXPOSURE_IDENTITY=PASS; physical-publisher=1; fresh-json-restores=2; "
                + "exact-outcomes=4; same-day-duplicates=0; commit-rejection-rollback=1; "
                + "legacy-migration=0; controlled-not-natural-play=1");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static IProductionCausalActionFixture Fixture(
        string actionId,
        Context context,
        Func<IReadOnlyDictionary<string, object>> capture,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            "environment",
            EnvironmentOutcomeIds.DiseaseRouteExposure,
            capture,
            execute,
            requireOutcome,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.PersistentOwnerId,
            context.PersistentOwnerKind,
            context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
            context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
            context.HasPersistentOwner ? context : null);

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-disease-route:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly GameplayOutcomeLedger ledger;
        private readonly FixedCalendar calendar;
        private readonly SingleDiseaseCatalog diseases;
        private readonly CharacterAiWorldRegistry world;
        private readonly PopulationHealthRuntime health;
        private readonly PopulationHealthApplicationAdapter adapter;
        private readonly WildlifeDiseaseVectorRuntime publisher;
        private readonly RejectedCommitFixture outcomeCommitter;
        private readonly DomainFactDispatcher facts;
        private readonly CharacterSO characterData;
        private readonly GameObject actorObject;
        private readonly GameObject vectorObject;
        private readonly WildlifeActor vector;
        private int lastPublishedAbsoluteDay = FirstDay - 1;
        private bool disposed;

        internal Context(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":disease-route-ledger";
            var events = new GameEventBus();
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new DiseaseExposureOutcomeDescriptor(
                        new DungeonStory.Narrative.Korean.KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new PopulationDiseaseExposureOutcomeAdapter()
                });
            ledger = new GameplayOutcomeLedger(
                registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            GameplayOutcomeRecorder recorder = new(ledger, registry, events);
            facts = new DomainFactDispatcher();
            var factCommits = new GameplayDomainFactCommitGroup(recorder, ledger);
            facts.Subscribe("disease-exposure:fixture", new GameplayDomainFactObserver<
                PopulationDiseaseExposureFact, PopulationDiseaseExposureOutcomeReceipt>(
                PopulationDiseaseExposureFactMapping.Capture,
                registry,
                ledger,
                recorder,
                factCommits));
            outcomeCommitter = new RejectedCommitFixture(facts);
            calendar = new FixedCalendar(FirstDay, 8);
            diseases = new SingleDiseaseCatalog(new DiseaseDefinition(
                DiseaseId,
                "야생 접촉열",
                DiseaseTransmissionRoute.Contact,
                incubationDays: 1,
                contagiousDays: 3,
                baseInfectionProbability: 0.25f,
                baseSeverity: 5f,
                targetSystem: DiseaseTargetSystem.Core,
                vaccineAllowed: true,
                symptomProfileId: "symptom:causal-wildlife-contact",
                fieldResponseIds: new[] { "field:causal-wildlife-contact" }));
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
                "CausalDiseaseRouteActor");
            characterData = CharacterAiPlanDebugFixtures.CreateCharacterData(
                CharacterType.NPC,
                "질병 경로 관측자",
                "Golem");
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.Initialize(characterData);
            actor.Identity.SetPersistentId(ActorId);
            world.RegisterCharacter(actor);

            health = new PopulationHealthRuntime(
                new DungeonRuntimeAggregateRootStore(),
                diseases,
                NeutralDiseaseModifierQuery.Instance,
                calendar,
                new RandomStreamProvider(71017));
            adapter = new PopulationHealthApplicationAdapter(
                health,
                diseases,
                world,
                world,
                Proxy<IRoomLayoutCache>(),
                NoEnvironmentalFieldQuery.Instance,
                Proxy<IAnatomyProfileCatalog>(),
                Proxy<IAnatomyHealthRuntime>(),
                events,
                health,
                health,
                outcomeCommitter,
                calendar);

            Grid vectorGrid = new(4, 1);
            for (int x = 0; x < vectorGrid.width; x++)
            {
                vectorGrid.GetGridCell(new Vector2Int(x, 0))
                    .SetAreaType(GridCellAreaType.ExteriorPath);
            }
            vectorObject = new GameObject("CausalDiseaseRouteVector");
            vector = vectorObject.AddComponent<WildlifeActor>();
            vector.ConfigureRuntimeServices(
                Proxy<IGridPathSearchBroker>(),
                world,
                new ControlledClock(),
                new RandomStreamProvider(71018),
                Proxy<IDoorAccessQuery>());
            vector.Initialize(
                vectorGrid,
                CreateDiseaseVectorSpecies(),
                VectorId,
                Vector2Int.zero);
            WildlifeWorldServices wildlifeServices = new(
                Proxy<IGridSystemProvider>(),
                Proxy<IWildlifeSpeciesCatalogProvider>(),
                Proxy<IGameSessionStateProvider>(),
                Proxy<IWildlifeEcosystemRuntime>(),
                Proxy<IMainCameraProvider>(),
                Proxy<IGridPathSearchBroker>(),
                world,
                Proxy<IWorldItemStackRuntime>(),
                Proxy<IPhysicalItemBatchDispositionService>(),
                calendar,
                events,
                diseases,
                facts,
                facts,
                facts);
            publisher = new WildlifeDiseaseVectorRuntime(wildlifeServices);

            if (checkpoint != null)
                RestoreOwnerCheckpoint(checkpoint);
            adapter.Start();
            ValidateState();
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
            DiseaseExposureSaveData exposure = Exposure;
            GameplayOutcomeSnapshot latest = ExactOutcomes.LastOrDefault();
            return new Dictionary<string, object>
            {
                ["diseaseRoute.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-disease-route",
                ["diseaseRoute.stage"] = Stage,
                ["diseaseRoute.calendarDay"] = calendar.Day,
                ["diseaseRoute.lastPublishedAbsoluteDay"] =
                    lastPublishedAbsoluteDay,
                ["diseaseRoute.actorId"] = ActorId,
                ["diseaseRoute.vectorId"] = VectorId,
                ["diseaseRoute.diseaseId"] = DiseaseId,
                ["diseaseRoute.healthCurrentAbsoluteDay"] =
                    health.Capture().currentAbsoluteDay,
                ["diseaseRoute.healthVersion"] = health.Version,
                ["diseaseRoute.pendingExposureCount"] =
                    health.Capture().pendingExposures.Count,
                ["diseaseRoute.weightedExposureHours"] =
                    exposure?.weightedExposureHours ?? 0f,
                ["diseaseRoute.exactOutcomeCount"] = ExactOutcomes.Length,
                ["diseaseRoute.lastOutcomeOperationId"] =
                    latest?.operationId ?? string.Empty,
                ["diseaseRoute.lastOutcomeCommitRevision"] =
                    checked((int)(latest?.commitRevision ?? 0L)),
                ["diseaseRoute.lastOutcomeOwnerRevision"] =
                    checked((int)(latest?.ownerRevision ?? 0L))
            };
        }

        internal bool PublishFirstExposure()
        {
            ThrowIfDisposed();
            if (Stage != 0 || calendar.Day != FirstDay)
                return false;
            return PublishExpectedDay(FirstDay, 1);
        }

        internal bool PublishRepeatExposure()
        {
            ThrowIfDisposed();
            if (Stage != 1 || calendar.Day != FirstDay)
                return false;
            calendar.SetDateTime(RepeatDay, 8);
            return PublishExpectedDay(RepeatDay, 2);
        }

        internal bool PublishFurtherExposure(int expectedStage)
        {
            Require(Stage == expectedStage - 1, "Fresh exposure owner lost its prior outcomes.");
            calendar.SetDateTime(FirstDay + expectedStage - 1, 8);
            return PublishExpectedDay(calendar.Day, expectedStage);
        }

        internal void VerifySameDayNoReplay()
        {
            JObject before = CaptureOwnerCheckpoint();
            Require(publisher.PublishDailyExposure(new[] { vector }, lastPublishedAbsoluteDay)
                    == lastPublishedAbsoluteDay,
                "An already published physical exposure day was not suppressed.");
            Require(JToken.DeepEquals(before, CaptureOwnerCheckpoint()),
                "Same-day physical publication replay changed health or the ledger.");
        }

        internal void VerifyRejectedCommitRollback()
        {
            string healthBefore = JsonUtility.ToJson(health.Capture());
            int cacheVersionBefore = health.Version;
            outcomeCommitter.RejectNextCommit = true;
            bool rejected = false;
            try
            {
                publisher.PublishDailyExposure(new[] { vector }, lastPublishedAbsoluteDay);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains(
                       "Population disease-exposure fact publication was rejected: "
                       + "controlled-exposure-commit-rejected"))
            {
                rejected = true;
            }
            Require(rejected && Stage == 0 && health.Version == cacheVersionBefore
                    && string.Equals(healthBefore, JsonUtility.ToJson(health.Capture()),
                        StringComparison.Ordinal),
                "Rejected exposure commit mutated health, its durable revision, or a durable outcome.");
            ValidateState();
        }

        internal void VerifyInDoubtPendingRoundTrip()
        {
            string before = JsonUtility.ToJson(health.Capture());
            outcomeCommitter.HideAcknowledgementCount = 1;
            bool inDoubt = false;
            try
            {
                publisher.PublishDailyExposure(
                    new[] { vector },
                    lastPublishedAbsoluteDay);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains(
                       "Population disease-exposure fact publication is in doubt"))
            {
                inDoubt = true;
            }

            PopulationHealthWorldSaveData pending = health.Capture();
            PopulationDiseaseExposurePendingFactSaveData pendingFact =
                pending.pendingExposureFacts.SingleOrDefault();
            Require(
                inDoubt
                && pendingFact != null
                && pendingFact.ownerRevision == pending.exposureRevision
                && string.Equals(pendingFact.characterId, ActorId,
                    StringComparison.Ordinal)
                && string.Equals(pendingFact.diseaseId, DiseaseId,
                    StringComparison.Ordinal)
                && pendingFact.route == (int)DiseaseTransmissionRoute.Contact
                && Mathf.Approximately(pendingFact.exposureHours, 1f)
                && Mathf.Approximately(pendingFact.environmentCoefficient, 1f)
                && pendingFact.absoluteDay == FirstDay
                && string.Equals(pendingFact.sourceKind, "wildlife-vector",
                    StringComparison.Ordinal)
                && string.Equals(pendingFact.sourceId, VectorId,
                    StringComparison.Ordinal)
                && ExactOutcomes.Length == 1,
                "In-doubt disease exposure did not retain one exact pending fact.");
            string pendingJson = JsonUtility.ToJson(pending);
            PopulationHealthWorldSaveData restored = JsonUtility.FromJson<
                PopulationHealthWorldSaveData>(pendingJson);
            health.PublishRestore(health.PrepareRestore(restored));
            Require(
                string.Equals(
                    pendingJson,
                    JsonUtility.ToJson(health.Capture()),
                    StringComparison.Ordinal),
                "Disease-exposure pending fact changed across JSON restore.");

            adapter.Start();
            PopulationHealthWorldSaveData replayed = health.Capture();
            Require(
                replayed.pendingExposureFacts.Count == 0
                && replayed.exposureRevision == pending.exposureRevision
                && ExactOutcomes.Length == 1
                && !string.Equals(before, JsonUtility.ToJson(replayed),
                    StringComparison.Ordinal),
                "Restored disease-exposure fact did not reconcile through its observer exactly once.");
        }

        internal void VerifyRequiredSaveIdentity()
        {
            var section = new PopulationHealthSaveSection(health);
            var valid = new DungeonGameRestoreReport();
            string original = JsonUtility.ToJson(health.Capture());
            section.ValidatePayload(original, PopulationHealthWorldSaveData.CurrentVersion, valid);
            Require(valid.Success, "The current exposure save section was rejected.");
            foreach (JToken badRevision in new JToken[] { null, JValue.CreateNull(), new JValue("1"), new JValue(0L) })
            {
                JObject bad = JObject.Parse(original);
                if (badRevision == null)
                    bad.Remove("exposureRevision");
                else
                    bad["exposureRevision"] = badRevision;
                bool rejected = false;
                try
                {
                    section.ValidatePayload(bad.ToString(Newtonsoft.Json.Formatting.None),
                        PopulationHealthWorldSaveData.CurrentVersion, new DungeonGameRestoreReport());
                }
                catch (InvalidOperationException exception) when (exception.Message.Contains(
                           "Population-health exposure revision is missing or invalid."))
                {
                    rejected = true;
                }
                Require(rejected && original == JsonUtility.ToJson(health.Capture()),
                    "Malformed/missing durable exposure identity was defaulted or mutated live state.");
            }
            foreach (JToken badPending in new JToken[]
                     {
                         null,
                         JValue.CreateNull(),
                         new JObject()
                     })
            {
                JObject bad = JObject.Parse(original);
                if (badPending == null)
                    bad.Remove("pendingExposureFacts");
                else
                    bad["pendingExposureFacts"] = badPending;
                bool rejected = false;
                try
                {
                    section.ValidatePayload(
                        bad.ToString(Newtonsoft.Json.Formatting.None),
                        PopulationHealthWorldSaveData.CurrentVersion,
                        new DungeonGameRestoreReport());
                }
                catch (InvalidOperationException exception) when (
                    exception.Message.Contains("pendingExposureFacts"))
                {
                    rejected = true;
                }
                Require(rejected && original == JsonUtility.ToJson(health.Capture()),
                    "Missing/non-array pending exposure facts were defaulted or mutated live state.");
            }
        }

        internal GameplayOutcomeReplayIdentity RequireFirstOutcome() =>
            RequireOutcome(2L);

        internal GameplayOutcomeReplayIdentity RequireRepeatOutcome() =>
            RequireOutcome(3L);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral disease-route fixtures have no checkpoint.");
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
                    ["actorId"] = ActorId,
                    ["vectorId"] = VectorId,
                    ["diseaseId"] = DiseaseId,
                    ["calendarDay"] = calendar.Day,
                    ["lastPublishedAbsoluteDay"] = lastPublishedAbsoluteDay,
                    ["populationHealth"] = JObject.FromObject(health.Capture())
                },
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            adapter.Dispose();
            facts.Dispose();
            world.UnregisterWildlife(vector);
            world.UnregisterCharacter(actorObject.GetComponent<CharacterActor>());
            UnityEngine.Object.DestroyImmediate(vectorObject);
            CharacterAiPlanDebugFixtures.DestroyProbeObject(actorObject);
            UnityEngine.Object.DestroyImmediate(characterData);
        }

        private int Stage => ExactOutcomes.Length;
        private GameplayOutcomeSnapshot[] ExactOutcomes => ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .Where(value => value != null
                && string.Equals(
                    value.producerId,
                    EnvironmentOutcomeIds.DiseaseExposureProducer,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.outcomeTypeId,
                    EnvironmentOutcomeIds.DiseaseRouteExposure.Value,
                    StringComparison.Ordinal))
            .OrderBy(value => value.ownerRevision)
            .ToArray();
        private DiseaseExposureSaveData Exposure => health
            .Capture()
            .pendingExposures
            .SingleOrDefault();

        private bool PublishExpectedDay(int expectedDay, int expectedStage)
        {
            int published = publisher.PublishDailyExposure(
                new[] { vector },
                lastPublishedAbsoluteDay);
            if (published != expectedDay)
                return false;
            lastPublishedAbsoluteDay = published;
            ValidateState();
            return Stage == expectedStage;
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(long revision)
        {
            string operationId = OperationId(revision);
            GameplayOutcomeSnapshot exact = ExactOutcomes.SingleOrDefault(value =>
                string.Equals(value.operationId, operationId, StringComparison.Ordinal));
            GameplayOutcomeReplayIdentity identity = default;
            bool valid = exact != null
                && exact.commitRevision == revision
                && exact.ownerRevision == revision
                && ledger.TryGetResultIdentity(
                    new GameplayResultKey(
                        EnvironmentOutcomeIds.DiseaseExposureProducer,
                        new GameplayOperationId(operationId),
                        revision,
                        0),
                    out identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash;
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Disease-route production entry did not publish exact revision "
                    + revision + ".");
            }
            return identity;
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
                "disease-route owner state",
                "schemaVersion",
                "schema",
                "stage",
                "actorId",
                "vectorId",
                "diseaseId",
                "calendarDay",
                "lastPublishedAbsoluteDay",
                "populationHealth");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["actorId"], ActorId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["vectorId"], VectorId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["diseaseId"], DiseaseId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Disease-route checkpoint schema or identity changed.");
            }
            calendar.SetDateTime((int)state["calendarDay"], 8);
            lastPublishedAbsoluteDay = (int)state["lastPublishedAbsoluteDay"];
            PopulationHealthWorldSaveData healthData = state["populationHealth"]
                .ToObject<PopulationHealthWorldSaveData>()
                ?? throw new InvalidOperationException(
                    "Disease-route population-health state is missing.");
            health.PublishRestore(health.PrepareRestore(healthData));
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Disease-route checkpoint stage disagrees with its ledger.");
            }
        }

        private void ValidateState()
        {
            GameplayOutcomeSnapshot[] outcomes = ExactOutcomes;
            PopulationHealthWorldSaveData state = health.Capture();
            PopulationHealthSaveSection.RequireExposureLedgerRevision(state, ledger.CaptureGameplayOutcomes());
            DiseaseExposureSaveData exposure = state.pendingExposures.SingleOrDefault();
            bool stageShape = Stage >= 0 && Stage <= 4
                && lastPublishedAbsoluteDay == FirstDay + Stage - 1
                && calendar.Day == FirstDay + Math.Max(0, Stage - 1)
                && health.ExposureRevision == Stage + 1L
                && (Stage == 0 ? exposure == null : ExposureMatches(exposure, Stage));
            bool outcomesValid = outcomes.Length == Stage;
            for (int index = 0; index < outcomes.Length && outcomesValid; index++)
            {
                long revision = index + 2L;
                int day = FirstDay + index;
                GameplayOutcomeSnapshot outcome = outcomes[index];
                outcomesValid = string.Equals(
                        outcome.operationId,
                        OperationId(revision),
                        StringComparison.Ordinal)
                    && outcome.commitRevision == revision
                    && outcome.ownerRevision == revision
                    && outcome.absoluteDay == day
                    && outcome.status == GameplayOutcomeStatus.Succeeded
                    && outcome.lifecycle
                        == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
                    && HasParticipant(outcome, "character", ActorId, "actor")
                    && HasParticipant(
                        outcome,
                        "disease-definition",
                        DiseaseId,
                        "disease")
                    && HasMetric(outcome, "exposure-hours", 1f, "hour", DiseaseId)
                    && HasMetric(
                        outcome,
                        "environment-coefficient",
                        1f,
                        "ratio",
                        DiseaseId)
                    && HasMetric(
                        outcome,
                        "sequence",
                        revision,
                        "revision",
                        ActorId)
                    && outcome.provenance.Any(value => value != null
                        && string.Equals(value.kindId, "wildlife-vector",
                            StringComparison.Ordinal)
                        && string.Equals(value.value, VectorId,
                            StringComparison.Ordinal))
                    && HasFact(outcome, "receipt-kind",
                        nameof(PopulationDiseaseRouteExposureEvent))
                    && HasFact(outcome, "route", "Contact")
                    && HasFact(outcome, "detail", VectorId);
            }
            if (!stageShape
                || !outcomesValid
                || state.version != PopulationHealthWorldSaveData.CurrentVersion
                || state.currentAbsoluteDay != FirstDay
                || state.pendingExposureFacts.Count != 0
                || state.pendingExposures.Count != (Stage == 0 ? 0 : 1))
            {
                throw new InvalidOperationException(
                    "Disease-route health, cursor and exact ledger state diverged.");
            }
        }

        private static bool ExposureMatches(
            DiseaseExposureSaveData exposure,
            float hours) => exposure != null
            && string.Equals(exposure.characterId, ActorId, StringComparison.Ordinal)
            && string.Equals(exposure.diseaseId, DiseaseId, StringComparison.Ordinal)
            && Mathf.Approximately(exposure.weightedExposureHours, hours);

        private static bool HasParticipant(
            GameplayOutcomeSnapshot outcome,
            string kind,
            string entityId,
            string role) => outcome.participants.Any(value => value != null
                && string.Equals(value.entityKindId, kind, StringComparison.Ordinal)
                && string.Equals(value.entityId, entityId, StringComparison.Ordinal)
                && string.Equals(value.roleId, role, StringComparison.Ordinal));

        private static bool HasMetric(
            GameplayOutcomeSnapshot outcome,
            string metricId,
            double value,
            string unitId,
            string referenceId) => outcome.metrics.Any(metric => metric != null
                && string.Equals(metric.metricId, metricId, StringComparison.Ordinal)
                && Math.Abs(metric.value - value) < 0.0001d
                && string.Equals(metric.unitId, unitId, StringComparison.Ordinal)
                && string.Equals(metric.referenceId, referenceId,
                    StringComparison.Ordinal));

        private static bool HasFact(
            GameplayOutcomeSnapshot outcome,
            string factId,
            string value) => outcome.facts.Any(fact => fact != null
                && string.Equals(fact.factId, factId, StringComparison.Ordinal)
                && string.Equals(fact.value, value, StringComparison.Ordinal));

        private static string OperationId(long revision) =>
            "population-exposure:" + ActorId + ":" + revision;

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    "DiseaseRouteExposureCausalProductionEntryContext");
            }
        }
    }

    // Reject before the durable observer, or hide one successful acknowledgement
    // after commit. Both paths exercise the production adapter without changing
    // the gameplay-ledger implementation.
    private sealed class RejectedCommitFixture : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;

        internal RejectedCommitFixture(IDomainFactPublisher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool RejectNextCommit { get; set; }
        internal int HideAcknowledgementCount { get; set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failureReason) =>
            inner.TryReserve(proposed, out lease, out failureReason);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (RejectNextCommit)
            {
                RejectNextCommit = false;
                inner.Cancel(lease);
                return DomainFactDelivery.Reject(
                    "controlled-exposure-commit-rejected");
            }

            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (HideAcknowledgementCount <= 0 || !delivered.MayBeCommitted)
                return delivered;
            HideAcknowledgementCount--;
            return DomainFactDelivery.Uncertain(
                "controlled-exposure-commit-in-doubt");
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private static WildlifeSpeciesDefinition CreateDiseaseVectorSpecies() => new(
        "causal_disease_route_vector",
        "질병 경로 매개체",
        "causal disease-route fixture vector",
        null,
        maxHealth: 10,
        moveSpeed: 1f,
        fearSensitivity: 1f,
        aggression: 0f,
        retaliationDamage: 0,
        spawnWeight: 1f,
        herdSize: 1,
        canEnterDungeon: true,
        carcassWeight: 1f,
        butcherYields: Array.Empty<WildlifeButcherYield>(),
        husbandry: new WildlifeHusbandryProfile(
            true,
            0.5f,
            2f,
            20f,
            3f,
            false,
            1f,
            2f),
        diseaseVectorIds: new[] { DiseaseId },
        migrationProfile: WildlifeMigrationProfile.GeneralHabitat());

    private static T Proxy<T>() where T : class =>
        DispatchProxy.Create<
            T,
            EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                .DefaultInterfaceProxy>();

    private sealed class SingleDiseaseCatalog : IDiseaseDefinitionCatalog
    {
        private readonly DiseaseDefinition definition;

        internal SingleDiseaseCatalog(DiseaseDefinition definition)
        {
            if (!definition.IsValid)
                throw new ArgumentException("Fixture disease must be valid.", nameof(definition));
            this.definition = definition;
            Definitions = new[] { definition };
        }

        public IReadOnlyList<DiseaseDefinition> Definitions { get; }

        public DiseaseDefinition Require(string diseaseId) =>
            string.Equals(diseaseId, definition.Id, StringComparison.Ordinal)
                ? definition
                : throw new KeyNotFoundException(
                    "Unknown disease '" + diseaseId + "'.");
    }

    private sealed class NeutralDiseaseModifierQuery : IPopulationDiseaseModifierQuery
    {
        internal static NeutralDiseaseModifierQuery Instance { get; } = new();

        public PopulationDiseaseStatModifiers Resolve(
            CharacterId characterId,
            DiseaseDefinition disease) => PopulationDiseaseStatModifiers.Neutral;
    }

    private sealed class ControlledClock : IGameClock
    {
        public float DeltaTime { get; set; } = 1f;
        public float Time { get; set; }
        public int FrameCount { get; set; }
        public bool IsPaused { get; set; }
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        private int day;
        private int hour;

        internal FixedCalendar(int day, int hour) => SetDateTime(day, hour);

        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds =>
            ((Day - 1) * GameCalendarRules.SecondsPerDay)
            + (Hour * GameCalendarRules.SecondsPerGameHour);
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, GameCalendarRules.HoursPerDay - 1);
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IProductionCausalActionFixture[] fixtures;

        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures)
        {
            this.fixtures = fixtures?.ToArray()
                ?? throw new ArgumentNullException(nameof(fixtures));
            Require(this.fixtures.Length == 2,
                "Disease-route causal fixture set is incomplete.");
        }

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

    private static void RequireFields(
        JObject value,
        string label,
        params string[] fields)
    {
        if (value == null)
            throw new InvalidOperationException(label + " is missing.");
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();
        string[] expected = fields
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields are not closed. expected="
                + string.Join(",", expected)
                + "; actual=" + string.Join(",", actual));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
