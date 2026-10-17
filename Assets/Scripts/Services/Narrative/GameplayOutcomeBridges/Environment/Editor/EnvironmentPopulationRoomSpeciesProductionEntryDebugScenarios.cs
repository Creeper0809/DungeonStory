#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static partial class EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
{
    private const string DiseaseId = "disease:environment-production-entry";
    private const string MenuPath =
        "DungeonStory/Debug/Run Environment Population Room Species Production Entry Scenarios";

    [MenuItem(MenuPath)]
    public static void RunAllFromMenu()
    {
        RunAll(true);
    }

    public static bool RunAll(bool log = false)
    {
        try
        {
            using Fixture fixture = new();
            VerifyDiseaseRouteExposure(fixture);
            VerifyRoomConditionAndExperience(fixture);
            VerifySpeciesIncident(fixture);

            fixture.RequirePublished(
                EnvironmentOutcomeIds.DiseaseExposureProducer,
                EnvironmentOutcomeIds.DiseaseRouteExposure,
                GameplayOutcomeStatus.Succeeded,
                expectedCount: 1);
            fixture.RequireDiseaseExposureProjection();
            fixture.RequirePublished(
                EnvironmentOutcomeIds.RoomConditionProducer,
                EnvironmentOutcomeIds.RoomConditionChanged,
                GameplayOutcomeStatus.Succeeded,
                expectedCount: 1);
            fixture.RequirePublished(
                EnvironmentOutcomeIds.RoomExperienceProducer,
                EnvironmentOutcomeIds.RoomExperienceApplied,
                GameplayOutcomeStatus.Succeeded,
                expectedCount: 2);
            fixture.RequirePublished(
                EnvironmentOutcomeIds.SpeciesIncidentProducer,
                EnvironmentOutcomeIds.SpeciesIncidentTriggered,
                GameplayOutcomeStatus.Failed,
                expectedCount: 1);

            Require(
                fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 5,
                "The shared ledger must contain only the five production-entry outcomes.");
            if (log)
            {
                Debug.Log(
                    "Environment population/room/species production-entry scenarios passed.");
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Environment population/room/species production-entry scenarios failed: "
                + exception);
            return false;
        }
    }

    private static void VerifyDiseaseRouteExposure(Fixture fixture)
    {
        PopulationHealthRuntime health = new(
            new DungeonRuntimeAggregateRootStore(),
            fixture.Diseases,
            NeutralDiseaseModifierQuery.Instance,
            fixture.Calendar,
            new RandomStreamProvider(61117));
        using PopulationHealthApplicationAdapter adapter = new(
            health,
            fixture.Diseases,
            fixture.World,
            fixture.World,
            Proxy<IRoomLayoutCache>(),
            NoEnvironmentalFieldQuery.Instance,
            Proxy<IAnatomyProfileCatalog>(),
            Proxy<IAnatomyHealthRuntime>(),
            fixture.Events,
            health,
            health,
            fixture.Facts,
            fixture.Calendar);
        adapter.Start();

        Grid vectorGrid = new(4, 1);
        for (int x = 0; x < vectorGrid.width; x++)
        {
            vectorGrid.GetGridCell(new Vector2Int(x, 0))
                .SetAreaType(GridCellAreaType.ExteriorPath);
        }

        GameObject vectorObject = new("EnvironmentProductionEntryDiseaseVector");
        WildlifeActor vector = vectorObject.AddComponent<WildlifeActor>();
        try
        {
            vector.ConfigureRuntimeServices(
                Proxy<IGridPathSearchBroker>(),
                fixture.World,
                fixture.Clock,
                new RandomStreamProvider(61217),
                Proxy<IDoorAccessQuery>());
            vector.Initialize(
                vectorGrid,
                CreateDiseaseVectorSpecies(),
                "wildlife:environment-production-entry-vector",
                Vector2Int.zero);

            WildlifeWorldServices services = new(
                Proxy<IGridSystemProvider>(),
                Proxy<IWildlifeSpeciesCatalogProvider>(),
                Proxy<IGameSessionStateProvider>(),
                Proxy<IWildlifeEcosystemRuntime>(),
                Proxy<IMainCameraProvider>(),
                Proxy<IGridPathSearchBroker>(),
                fixture.World,
                Proxy<IWorldItemStackRuntime>(),
                Proxy<IPhysicalItemBatchDispositionService>(),
                fixture.Calendar,
                fixture.Events,
                fixture.Diseases,
                fixture.Facts,
                fixture.Facts,
                fixture.Facts);
            WildlifeDiseaseVectorRuntime runtime = new(services);

            int versionBefore = health.Version;
            PopulationHealthWorldSaveData stateBefore = health.Capture();
            int publishedDay = runtime.PublishDailyExposure(
                new[] { vector },
                lastPublishedAbsoluteDay: fixture.Calendar.Day - 1);
            PopulationHealthWorldSaveData stateAfter = health.Capture();

            Require(
                publishedDay == fixture.Calendar.Day,
                "Wildlife disease publisher did not advance its daily publication owner state.");
            Require(
                health.Version == versionBefore + 1,
                "Population health exposure did not advance exactly one owner revision.");
            Require(
                stateBefore.pendingExposures.Count == 0
                && stateAfter.pendingExposures.Count == 1,
                "Wildlife route exposure did not add exactly one population-health exposure.");
            DiseaseExposureSaveData exposure = stateAfter.pendingExposures[0];
            Require(
                string.Equals(exposure.characterId, fixture.CharacterId.Value,
                    StringComparison.Ordinal)
                && string.Equals(exposure.diseaseId, DiseaseId,
                    StringComparison.Ordinal)
                && Mathf.Approximately(exposure.weightedExposureHours, 1f),
                "Population-health exposure state lost the wildlife target or exact hours.");
            fixture.RequirePublished(
                EnvironmentOutcomeIds.DiseaseExposureProducer,
                EnvironmentOutcomeIds.DiseaseRouteExposure,
                GameplayOutcomeStatus.Succeeded,
                expectedCount: 1);
        }
        finally
        {
            fixture.World.UnregisterWildlife(vector);
            UnityEngine.Object.DestroyImmediate(vectorObject);
        }
    }

    private static void VerifyRoomConditionAndExperience(Fixture fixture)
    {
        RoomWorld roomWorld = RoomWorld.Create(fixture.World);
        try
        {
            fixture.World.SetGrid(roomWorld.Grid);
            RoomEnvironmentEvaluator evaluator = roomWorld.CreateEvaluator();
            RoomEnvironmentExperienceService service = new(
                new RoomLayoutCache(),
                evaluator,
                roomWorld.SettingsProvider,
                fixture.Events,
                fixture.Calendar,
                Proxy<IHeritableTraitEffectQuery>(),
                fixture.Facts);

            RoomInstance firstRoom = roomWorld.GetMainRoom();
            RoomEnvironmentSnapshot firstSnapshot = evaluator.Evaluate(
                roomWorld.Grid,
                firstRoom);
            CharacterMoodDeliveryTransactionSnapshot moodBefore =
                fixture.Actor.Stats.CaptureMoodDeliveryTransactionState();
            bool firstApplied = service.Apply(new RoomEnvironmentExperienceEvent(
                fixture.Actor,
                roomWorld.MainFixture,
                RoomExperienceActivity.Work,
                BuiltInWorkTypeIds.Research));
            RoomEnvironmentOutcomeSaveData afterFirst = service.CaptureOutcomeState();
            CharacterMoodDeliveryTransactionSnapshot moodAfterFirst =
                fixture.Actor.Stats.CaptureMoodDeliveryTransactionState();

            Require(firstApplied, "First room Apply did not execute a non-zero mood experience.");
            Require(
                moodAfterFirst.Factors.Count > moodBefore.Factors.Count,
                "Room experience did not add an authoritative character mood factor.");
            Require(
                afterFirst.nextOutcomeSequence == 2L
                && afterFirst.observations.Count == 1
                && Mathf.Approximately(
                    afterFirst.observations[0].cleanliness,
                    firstSnapshot.Cleanliness),
                "First room Apply did not persist its cleanliness baseline and experience sequence.");

            roomWorld.MainFixture.FacilityState.cleanliness = 0f;
            RoomInstance secondRoom = roomWorld.GetMainRoom();
            RoomEnvironmentSnapshot secondSnapshot = evaluator.Evaluate(
                roomWorld.Grid,
                secondRoom);
            Require(
                !Mathf.Approximately(firstSnapshot.Cleanliness, secondSnapshot.Cleanliness),
                "Room fixture did not create a real cleanliness state delta.");

            bool secondApplied = service.Apply(new RoomEnvironmentExperienceEvent(
                fixture.Actor,
                roomWorld.MainFixture,
                RoomExperienceActivity.Work,
                BuiltInWorkTypeIds.Research));
            RoomEnvironmentOutcomeSaveData afterSecond = service.CaptureOutcomeState();

            Require(secondApplied, "Second room Apply did not execute a mood experience.");
            Require(
                afterSecond.nextOutcomeSequence == 4L
                && afterSecond.observations.Count == 1
                && Mathf.Approximately(
                    afterSecond.observations[0].cleanliness,
                    secondSnapshot.Cleanliness),
                "Second room Apply did not commit condition plus experience owner sequences.");
            fixture.RequirePublished(
                EnvironmentOutcomeIds.RoomConditionProducer,
                EnvironmentOutcomeIds.RoomConditionChanged,
                GameplayOutcomeStatus.Succeeded,
                expectedCount: 1);
            fixture.RequirePublished(
                EnvironmentOutcomeIds.RoomExperienceProducer,
                EnvironmentOutcomeIds.RoomExperienceApplied,
                GameplayOutcomeStatus.Succeeded,
                expectedCount: 2);

            fixture.AdjacentIncidentBuilding = roomWorld.LeftDoor;
            fixture.Retain(roomWorld);
            roomWorld = null;
        }
        finally
        {
            roomWorld?.Dispose();
        }
    }

    private static void VerifySpeciesIncident(Fixture fixture)
    {
        IWorldFilthQuery filth = Proxy<IWorldFilthQuery>();
        SpeciesIncidentHandlerRegistry handlers = new(
            Proxy<IWorldItemStackRuntime>(),
            Proxy<IPhysicalItemRelocationService>(),
            filth,
            Proxy<IWorldWaterContaminationCommand>(),
            fixture.World);
        CharacterSpeciesRuntime runtime = new(
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

        CharacterSpeciesRuntimeSaveData restored = new()
        {
            characters = new List<CharacterSpeciesRuntimeRecordSaveData>
            {
                new()
                {
                    characterInstanceId = fixture.CharacterId.Value,
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
        runtime.Restore(runtime.BuildRestore(restored));
        fixture.Clock.Time = 2f;

        float healthBefore = fixture.Actor.Stats.CurrentHealth;
        float expectedDamage = fixture.Actor.Stats.MaxHealth * 0.08f;
        bool buildingDamagedBefore = fixture.AdjacentIncidentBuilding.IsDamaged;
        runtime.Tick();

        Require(
            runtime.TryGet(fixture.CharacterId, out CharacterSpeciesRuntimeState state),
            "Species runtime did not retain the Golem owner state.");
        Require(
            state.IncidentCount == 1
            && string.Equals(
                state.LastIncidentId,
                CharacterSpeciesIncidentIds.GolemCoreOverload,
                StringComparison.Ordinal)
            && Mathf.Approximately(state.NextIncidentAt, fixture.Clock.Time + 300f),
            "Golem Tick did not advance incident count, id, and cooldown atomically.");
        Require(
            Mathf.Approximately(
                healthBefore - fixture.Actor.Stats.CurrentHealth,
                expectedDamage),
            "Real Golem overload handler did not apply exact non-lethal health damage.");
        Require(
            !buildingDamagedBefore && fixture.AdjacentIncidentBuilding.IsDamaged,
            "Real Golem overload handler did not damage the adjacent facility.");
        fixture.RequirePublished(
            EnvironmentOutcomeIds.SpeciesIncidentProducer,
            EnvironmentOutcomeIds.SpeciesIncidentTriggered,
            GameplayOutcomeStatus.Failed,
            expectedCount: 1);
    }

    private static WildlifeSpeciesDefinition CreateDiseaseVectorSpecies() => new(
        "environment_entry_vector",
        "환경 진입 질병 매개체",
        "production-entry fixture vector",
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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

    private sealed class Fixture : IDisposable
    {
        private readonly CharacterSO characterData;
        private readonly GameObject actorObject;
        private readonly List<IDisposable> factBindings = new();
        private RoomWorld retainedRoomWorld;

        internal Fixture(
            string ledgerRunId =
                "run:environment-population-room-species-production-entry",
            string persistentCharacterId = null)
        {
            Events = new GameEventBus();
            KoreanJosaFormatter josa = new();
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new DiseaseExposureOutcomeDescriptor(josa),
                    new RoomConditionOutcomeDescriptor(josa),
                    new RoomEnvironmentExperienceOutcomeDescriptor(josa),
                    new SpeciesIncidentOutcomeDescriptor(josa),
                    new GolemUpkeepOutcomeDescriptor(josa),
                    new HarpyGaleRelocationOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new PopulationDiseaseExposureOutcomeAdapter(),
                    new RoomConditionOutcomeAdapter(),
                    new RoomEnvironmentExperienceOutcomeAdapter(),
                    new SpeciesIncidentOutcomeAdapter(),
                    new GolemUpkeepOutcomeAdapter(),
                    new HarpyGaleRelocationOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            GameplayOutcomeRecorder recorder = new(Ledger, registry, Events);
            Facts = new DomainFactDispatcher();
            var factCommits = new GameplayDomainFactCommitGroup(recorder, Ledger);
            factBindings.Add(Facts.Subscribe("disease-exposure:fixture", new GameplayDomainFactObserver<
                PopulationDiseaseExposureFact, PopulationDiseaseExposureOutcomeReceipt>(
                PopulationDiseaseExposureFactMapping.Capture, registry, Ledger, recorder, factCommits)));
            factBindings.Add(Facts.Subscribe("room-experience:fixture", new GameplayDomainFactObserver<
                RoomEnvironmentExperienceFact, RoomEnvironmentExperienceOutcomeReceipt>(
                RoomEnvironmentFactObserverMapping.CaptureExperience, registry, Ledger, recorder, factCommits)));
            factBindings.Add(Facts.Subscribe("room-condition:fixture", new GameplayDomainFactObserver<
                RoomEnvironmentConditionFact, RoomConditionOutcomeReceipt>(
                RoomEnvironmentFactObserverMapping.CaptureCondition, registry, Ledger, recorder, factCommits)));
            factBindings.Add(SpeciesActionFactMapping.CreateGolemBinding(
                registry,
                Ledger,
                recorder,
                factCommits).Connect(Facts));
            factBindings.Add(SpeciesActionFactMapping.CreateSpeciesIncidentBinding(
                registry,
                Ledger,
                recorder,
                factCommits).Connect(Facts));
            factBindings.Add(SpeciesActionFactMapping.CreateHarpyGaleBinding(
                registry,
                Ledger,
                recorder,
                factCommits).Connect(Facts));
            EnvironmentGameplayOutcomeBridge bridge = new(
                recorder,
                Ledger,
                Ledger);

            Calendar = new FixedCalendar(17, 8);
            Clock = new ControlledClock();
            Diseases = new SingleDiseaseCatalog(new DiseaseDefinition(
                DiseaseId,
                "환경 진입 접촉병",
                DiseaseTransmissionRoute.Contact,
                incubationDays: 1,
                contagiousDays: 3,
                baseInfectionProbability: 0.25f,
                baseSeverity: 5f,
                targetSystem: DiseaseTargetSystem.Core,
                vaccineAllowed: true,
                symptomProfileId: "symptom:environment-production-entry",
                fieldResponseIds: new[] { "field:environment-production-entry" }));
            World = new CharacterAiWorldRegistry(
                new SceneRuntimeRegistry<CharacterActor>(),
                new SceneRuntimeRegistry<WildlifeActor>(),
                new SceneRuntimeRegistry<BuildableObject>(),
                new SceneRuntimeRegistry<IWarehouseFacility>(),
                new SceneRuntimeRegistry<IRetailFacility>(),
                Proxy<IGameSessionStateProvider>(),
                Proxy<IRestoreWorldCandidateQuery>(),
                Proxy<ICharacterLifePublicationService>());

            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "EnvironmentProductionEntryGolem");
            characterData = CharacterAiPlanDebugFixtures.CreateCharacterData(
                CharacterType.NPC,
                "환경 진입 골렘",
                "Golem");
            Actor = actorObject.GetComponent<CharacterActor>();
            Actor.Initialize(characterData);
            if (!string.IsNullOrWhiteSpace(persistentCharacterId))
                Actor.Identity.SetPersistentId(persistentCharacterId);
            World.RegisterCharacter(Actor);
            CharacterId = CharacterPersistentIdentity.Require(Actor);
            Bridge = bridge;
            Require(
                Actor.Stats != null
                && Actor.Stats.CurrentHealth > 0f
                && string.Equals(Actor.SpeciesTag, "Golem", StringComparison.Ordinal),
                "Authored Golem production actor was not initialized.");
        }

        internal GameEventBus Events { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal DomainFactDispatcher Facts { get; }
        internal IEnvironmentGameplayOutcomeCommitter Bridge { get; }
        internal FixedCalendar Calendar { get; }
        internal ControlledClock Clock { get; }
        internal SingleDiseaseCatalog Diseases { get; }
        internal CharacterAiWorldRegistry World { get; }
        internal CharacterActor Actor { get; }
        internal CharacterId CharacterId { get; }
        internal BuildableObject AdjacentIncidentBuilding { get; set; }

        internal void Retain(RoomWorld roomWorld)
        {
            retainedRoomWorld = roomWorld
                ?? throw new ArgumentNullException(nameof(roomWorld));
        }

        internal void RequirePublished(
            string producerId,
            GameplayOutcomeTypeId outcomeTypeId,
            GameplayOutcomeStatus expectedStatus,
            int expectedCount)
        {
            GameplayOutcomeSnapshot[] matches = Ledger.CaptureGameplayOutcomes()
                .exactOutcomes
                .Where(value => value != null
                    && string.Equals(value.producerId, producerId,
                        StringComparison.Ordinal)
                    && string.Equals(value.outcomeTypeId, outcomeTypeId.Value,
                        StringComparison.Ordinal))
                .ToArray();
            Require(
                matches.Length == expectedCount,
                $"Expected {expectedCount} exact {producerId}/{outcomeTypeId.Value} outcomes, found {matches.Length}.");
            foreach (GameplayOutcomeSnapshot snapshot in matches)
            {
                Require(
                    snapshot.lifecycle
                        == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
                    && snapshot.status == expectedStatus,
                    $"{producerId}/{outcomeTypeId.Value} was not PublishedAcknowledged with the expected status.");
                GameplayResultKey key = new(
                    snapshot.producerId,
                    new GameplayOperationId(snapshot.operationId),
                    snapshot.commitRevision,
                    snapshot.localResultIndex);
                Require(
                    Ledger.TryGetResultIdentity(
                        key,
                        out GameplayOutcomeReplayIdentity identity)
                    && identity.State
                        == GameplayOutcomeReplayState.PublishedAcknowledged
                    && identity.HasCanonicalPayloadHash
                    && Ledger.TryGetExact(
                        identity.OutcomeId,
                        out GameplayOutcomeSnapshot exact)
                    && string.Equals(exact.producerId, producerId,
                        StringComparison.Ordinal)
                    && string.Equals(exact.outcomeTypeId, outcomeTypeId.Value,
                        StringComparison.Ordinal),
                    $"{producerId}/{outcomeTypeId.Value} replay identity or exact ledger payload is missing.");
            }
        }

        internal void RequireDiseaseExposureProjection()
        {
            GameplayOutcomeSnapshot snapshot = Ledger.CaptureGameplayOutcomes()
                .exactOutcomes.Single(value => value != null
                    && string.Equals(
                        value.producerId,
                        EnvironmentOutcomeIds.DiseaseExposureProducer,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.outcomeTypeId,
                        EnvironmentOutcomeIds.DiseaseRouteExposure.Value,
                        StringComparison.Ordinal));
            GameplayOutcomeParticipantSnapshot disease = snapshot.participants
                .Single(value => string.Equals(
                    value.roleId,
                    EnvironmentOutcomeIds.DiseaseRole.Value,
                    StringComparison.Ordinal));
            Require(
                disease.participationKind == GameplayParticipationKind.Direct
                && !string.IsNullOrWhiteSpace(disease.displayText)
                && !string.IsNullOrWhiteSpace(disease.displayRevision)
                && !string.IsNullOrWhiteSpace(disease.locale)
                && !string.IsNullOrWhiteSpace(disease.pronunciationRevision)
                && snapshot.subjects.Any(value => string.Equals(
                        value.entityKindId,
                        disease.entityKindId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.entityId,
                        disease.entityId,
                        StringComparison.Ordinal)),
                "Disease exposure lost its direct disease subject or immutable display-name snapshot.");
        }

        public void Dispose()
        {
            for (int index = factBindings.Count - 1; index >= 0; index--)
                factBindings[index].Dispose();
            factBindings.Clear();
            Facts.Dispose();
            retainedRoomWorld?.Dispose();
            retainedRoomWorld = null;
            if (Actor != null)
                World.UnregisterCharacter(Actor);
            CharacterAiPlanDebugFixtures.DestroyProbeObject(actorObject);
            if (characterData != null)
                UnityEngine.Object.DestroyImmediate(characterData);
        }
    }

    internal sealed class RoomWorld : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly List<BuildableObject> buildings = new();
        private readonly CharacterAiWorldRegistry world;

        private RoomWorld(CharacterAiWorldRegistry world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            Grid = new Grid(8, 1);
            Records = new RecordProvider();
            RoomEnvironmentSettingsSO settings =
                ScriptableObject.CreateInstance<RoomEnvironmentSettingsSO>();
            cleanup.Add(settings);
            SettingsProvider = new FixedSettingsProvider(settings);
        }

        internal Grid Grid { get; }
        internal RecordProvider Records { get; }
        internal FixedSettingsProvider SettingsProvider { get; }
        internal BuildableObject MainFixture { get; private set; }
        internal BuildableObject LeftDoor { get; private set; }

        internal static RoomWorld Create(
            CharacterAiWorldRegistry world,
            string stableIdentityScope = null)
        {
            RoomWorld result = new(world);
            for (int x = 0; x < result.Grid.width; x++)
            {
                result.Grid.RegisterOccupant(
                    new HallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new(x, 0) },
                    false);
            }

            result.LeftDoor = result.Place(
                "Environment Entry Door",
                new Vector2Int(1, 0),
                BuildingCategory.Movement,
                FacilityRole.None,
                StableBuildingId(stableIdentityScope, "door"));
            result.Place(
                "Environment Entry Wall",
                new Vector2Int(6, 0),
                BuildingCategory.Wall,
                FacilityRole.None,
                StableBuildingId(stableIdentityScope, "wall"));
            result.MainFixture = result.Place(
                "Environment Entry Research Desk",
                new Vector2Int(3, 0),
                BuildingCategory.Special,
                FacilityRole.Research | FacilityRole.Hygiene,
                StableBuildingId(stableIdentityScope, "research-desk"));
            result.MainFixture.FacilityState.cleanliness = 100f;
            result.MainFixture.BuildingData.Evolution.scores = new[]
            {
                new FacilityEvolutionValue(FacilityEvolutionTerms.Luxury, 40f)
            };
            result.Records.GetRecord(result.MainFixture).AddToken(
                FacilityEvolutionTerms.CleanServiceStreak,
                3);
            return result;
        }

        internal RoomEnvironmentEvaluator CreateEvaluator() => new(
            SettingsProvider,
            Records,
            worldFilthQuery: null,
            survivalFoodRuntime: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance);

        internal RoomInstance GetMainRoom() => RoomDetector.Build(Grid).Rooms
            .First(value => value != null && value.ContainsPart(MainFixture));

        private BuildableObject Place(
            string displayName,
            Vector2Int position,
            BuildingCategory category,
            FacilityRole roles,
            string persistentId)
        {
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            cleanup.Add(data);
            data.id = cleanup.Count + 7100;
            data.objectName = displayName;
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = category;
            data.runtimeArchetype = category == BuildingCategory.Movement
                ? BuildingRuntimeArchetypeKind.Door
                : BuildingRuntimeArchetypeKind.Generic;
            data.Facility = new FacilityData
            {
                roles = roles,
                capacity = roles == FacilityRole.None ? 0 : 1,
                useDuration = 1f
            };
            data.Evolution = new FacilityEvolutionContributionData();

            GameObject instance = new(displayName);
            cleanup.Add(instance);
            BuildableObject building = instance.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.SetGrid(Grid);
            building.Initialization(data, position);
            if (!string.IsNullOrWhiteSpace(persistentId))
            {
                building.RestorePersistentIdentity(
                    new BuildingInstanceId(persistentId));
            }
            Require(
                Grid.RegisterOccupant(
                    building,
                    GridLayer.Building,
                    new List<Vector2Int> { position },
                    category == BuildingCategory.Movement),
                "Could not place room production-entry fixture " + displayName + ".");
            buildings.Add(building);
            world.RegisterBuilding(building);
            return building;
        }

        private static string StableBuildingId(string scope, string suffix) =>
            string.IsNullOrWhiteSpace(scope)
                ? null
                : "building:causal-room:" + scope + ":" + suffix;

        public void Dispose()
        {
            foreach (BuildableObject building in buildings)
            {
                if (building != null)
                    world.UnregisterBuilding(building);
            }
            foreach (UnityEngine.Object item in cleanup.Where(value => value != null))
                UnityEngine.Object.DestroyImmediate(item);
            buildings.Clear();
            cleanup.Clear();
        }

        internal sealed class FixedSettingsProvider : IRoomEnvironmentSettingsProvider
        {
            internal FixedSettingsProvider(RoomEnvironmentSettingsSO settings)
            {
                Settings = settings
                    ?? throw new ArgumentNullException(nameof(settings));
            }

            public RoomEnvironmentSettingsSO Settings { get; }
        }

        internal sealed class RecordProvider : IFacilityEvolutionRecordProvider
        {
            private readonly Dictionary<BuildableObject, FacilityEvolutionRecord>
                records = new();

            public FacilityEvolutionRecord GetRecord(BuildableObject facility)
            {
                if (facility == null)
                    return new FacilityEvolutionRecord();
                if (!records.TryGetValue(facility, out FacilityEvolutionRecord record))
                {
                    record = new FacilityEvolutionRecord();
                    records.Add(facility, record);
                }
                return record;
            }
        }

        private sealed class HallwayOccupant : IGridOccupant
        {
            public int GridId => 0;
            public bool IsGridDestroyed => false;
            public bool IsGridVisitable => false;
            public bool IsGridMovement => true;
        }
    }

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
                : throw new KeyNotFoundException("Unknown disease '" + diseaseId + "'.");
    }

    private sealed class NeutralDiseaseModifierQuery : IPopulationDiseaseModifierQuery
    {
        internal static NeutralDiseaseModifierQuery Instance { get; } = new();

        public PopulationDiseaseStatModifiers Resolve(
            CharacterId characterId,
            DiseaseDefinition disease) => PopulationDiseaseStatModifiers.Neutral;
    }

    private sealed class FixedRunSeedProvider : IRunSeedProvider
    {
        internal FixedRunSeedProvider(int runSeed)
        {
            RunSeed = runSeed;
        }

        public int RunSeed { get; }
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

        internal FixedCalendar(int day, int hour)
        {
            SetDateTime(day, hour);
        }

        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds =>
            (Day - 1) * GameCalendarRules.SecondsPerDay
            + Hour * GameCalendarRules.SecondsPerGameHour;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);

        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);

        public void Start()
        {
            IsRunning = true;
        }

        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, GameCalendarRules.HoursPerDay - 1);
        }
    }
}
#endif
