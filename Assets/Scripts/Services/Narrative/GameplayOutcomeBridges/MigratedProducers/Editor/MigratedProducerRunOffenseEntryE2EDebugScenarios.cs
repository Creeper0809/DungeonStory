#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MigratedProducerRunOffenseEntryE2EDebugScenarios
{
    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        new[]
        {
            MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent,
            MigratedProducerOutcomeKind.FactionRouteArrivedEvent,
            MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt,
            MigratedProducerOutcomeKind.FactionRouteSettlementReceipt,
            MigratedProducerOutcomeKind.FactionTrustChangedEvent,
            MigratedProducerOutcomeKind.FestivalCelebratedEvent,
            MigratedProducerOutcomeKind.OffenseBattleCommandResult,
            MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt,
            MigratedProducerOutcomeKind.OffenseExpeditionNodeResult,
            MigratedProducerOutcomeKind.OffenseExpeditionResult,
            MigratedProducerOutcomeKind.RunResultReadyEvent,
            MigratedProducerOutcomeKind.RunVariableActivatedEvent,
            MigratedProducerOutcomeKind.RunVariableExpiredEvent,
            MigratedProducerOutcomeKind.V20ResolvedEventResult,
            MigratedProducerOutcomeKind.CareerMentorshipAwardCommitReceipt,
            MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            Require(
                CoveredKinds.Count == CoveredKinds.Distinct().Count(),
                "Run/offense migrated-producer coverage contains a duplicate kind.");
            var observed = new HashSet<MigratedProducerOutcomeKind>();

            VerifyRunResult(observed);
            VerifyRunVariableActivated(observed);
            VerifyRunVariableExpired(observed);
            VerifyCharacterAgeConditionChanged(observed);
            VerifyExteriorReception(observed);
            VerifyCareerMentorshipAward(observed);
            VerifyOffenseBattleCommand(observed);
            VerifyOffenseExpeditionNode(observed);
            VerifyOffenseExpeditionResult(observed);
            VerifyOffenseExpeditionArrival(observed);
            VerifyFactionTrustChanged(observed);
            VerifyFactionRouteArrived(observed);
            VerifyFactionRouteCargoDelivery(observed);
            VerifyFactionRouteSettlement(observed);
            VerifyFestivalCelebrated(observed);
            VerifyV20ResolvedEvent(observed);

            Require(
                observed.SetEquals(CoveredKinds),
                "Run/offense migrated-producer observed coverage did not match CoveredKinds."
                + " Missing=" + string.Join(",", CoveredKinds.Except(observed))
                + "; unexpected=" + string.Join(",", observed.Except(CoveredKinds)));
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    private static void VerifyRunResult(ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.RunResultReadyEvent,
            (kind, fault, delivery) => new RunResultScenario(kind, fault, delivery));
        observed.Add(MigratedProducerOutcomeKind.RunResultReadyEvent);
    }

    private static void VerifyRunVariableActivated(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.RunVariableActivatedEvent,
            (kind, fault, delivery) => new RunVariableActivatedScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.RunVariableActivatedEvent);
    }

    private static void VerifyRunVariableExpired(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.RunVariableExpiredEvent,
            (kind, fault, delivery) => new RunVariableExpiredScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.RunVariableExpiredEvent);
    }

    private static void VerifyCharacterAgeConditionChanged(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent,
            (kind, fault, delivery) => new CharacterAgeConditionScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent);
    }

    private static void VerifyExteriorReception(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent,
            (kind, fault, delivery) => new ExteriorReceptionScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent);
    }

    private static void VerifyCareerMentorshipAward(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CareerMentorshipAwardCommitReceipt,
            (kind, fault, delivery) => new CareerMentorshipAwardScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.CareerMentorshipAwardCommitReceipt);
    }

    private static void VerifyOffenseBattleCommand(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.OffenseBattleCommandResult,
            (kind, fault, delivery) => new OffenseBattleCommandScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.OffenseBattleCommandResult);
    }

    private static void VerifyOffenseExpeditionNode(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.OffenseExpeditionNodeResult,
            (kind, fault, delivery) => new OffenseExpeditionNodeScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.OffenseExpeditionNodeResult);
    }

    private static void VerifyOffenseExpeditionResult(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.OffenseExpeditionResult,
            (kind, fault, delivery) => new OffenseExpeditionResultScenario(
                kind,
                fault,
                delivery));
        VerifyTriplet(
            MigratedProducerOutcomeKind.OffenseExpeditionResult,
            (kind, fault, delivery) => new OffenseExpeditionResultScenario(
                kind,
                fault,
                delivery,
                success: true));
        observed.Add(MigratedProducerOutcomeKind.OffenseExpeditionResult);
    }

    private static void VerifyOffenseExpeditionArrival(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt,
            (kind, fault, delivery) => new OffenseExpeditionArrivalScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt);
    }

    private static void VerifyFactionTrustChanged(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.FactionTrustChangedEvent,
            (kind, fault, delivery) => new FactionTrustScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.FactionTrustChangedEvent);
    }

    private static void VerifyFactionRouteArrived(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.FactionRouteArrivedEvent,
            (kind, fault, delivery) => new FactionRouteArrivalScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.FactionRouteArrivedEvent);
    }

    private static void VerifyFactionRouteCargoDelivery(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt,
            (kind, fault, delivery) => new FactionRouteCargoScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt);
    }

    private static void VerifyFactionRouteSettlement(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.FactionRouteSettlementReceipt,
            (kind, fault, delivery) => new FactionRouteSettlementScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.FactionRouteSettlementReceipt);
    }

    private static void VerifyFestivalCelebrated(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.FestivalCelebratedEvent,
            (kind, fault, delivery) => new FestivalCelebratedScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.FestivalCelebratedEvent);
    }

    private static void VerifyV20ResolvedEvent(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.V20ResolvedEventResult,
            (kind, fault, delivery) => new V20ResolvedEventScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.V20ResolvedEventResult);
    }

    private static void VerifyTriplet(
        MigratedProducerOutcomeKind kind,
        Func<MigratedProducerOutcomeKind,
            MigratedProducerOutcomeFaultMode,
            bool,
            IEntryScenario> create)
    {
        using (IEntryScenario scenario = create(
                   kind,
                   MigratedProducerOutcomeFaultMode.None,
                   false))
        {
            string before = scenario.CaptureDomain();
            scenario.Execute();
            string after = scenario.CaptureDomain();
            Require(before != after, kind + " production entry did not mutate its owner.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                kind,
                scenario.Probe);
        }

        using (IEntryScenario scenario = create(
                   kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit,
                   false))
        {
            string domainBefore = scenario.CaptureDomain();
            string ownerBefore = JsonUtility.ToJson(
                scenario.Outcomes.Transaction.Capture());
            scenario.ExecuteExpectingRejectedCommit();
            Require(
                scenario.Probe.ReservationCount(kind) > 0
                && scenario.Probe.CommitAttemptCount(kind) > 0,
                kind + " rejection path did not reach the real commit boundary.");
            Require(
                domainBefore == scenario.CaptureDomain(),
                kind + " left domain mutation after a definite commit rejection.");
            Require(
                ownerBefore == JsonUtility.ToJson(
                    scenario.Outcomes.Transaction.Capture()),
                kind + " advanced outcome owner state after a definite rejection.");
        }

        using (IEntryScenario scenario = create(
                   kind,
                   MigratedProducerOutcomeFaultMode.None,
                   true))
        {
            string before = scenario.CaptureDomain();
            scenario.Execute();
            string committed = scenario.CaptureDomain();
            Require(before != committed, kind + " pending commit did not mutate its owner.");
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                committed == scenario.CaptureDomain(),
                kind + " delivery retry duplicated or reversed domain mutation.");
        }
    }

    private interface IEntryScenario : IDisposable
    {
        MigratedProducerOutcomeEditorFixture Outcomes { get; }
        MigratedProducerOutcomeProbeTransaction Probe { get; }
        MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        string CaptureDomain();
        void Execute();
        void ExecuteExpectingRejectedCommit();
    }

    private abstract class EntryScenarioBase : IEntryScenario
    {
        protected EntryScenarioBase(
            string runPrefix,
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                runPrefix + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = fault
            };
        }

        public MigratedProducerOutcomeEditorFixture Outcomes { get; }
        public MigratedProducerOutcomeProbeTransaction Probe { get; }
        public MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        public abstract string CaptureDomain();
        public abstract void Execute();

        public virtual void ExecuteExpectingRejectedCommit()
        {
            try
            {
                Execute();
            }
            catch (InvalidOperationException)
            {
            }
        }

        public abstract void Dispose();
    }

    private sealed class RunResultScenario : EntryScenarioBase
    {
        private readonly GameObject root;
        private readonly MetaProgressionRuntime runtime;

        internal RunResultScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-run-result-", target, fault, deliveryFault)
        {
            root = new GameObject("Migrated Run Result E2E");
            runtime = root.AddComponent<MetaProgressionRuntime>();
            runtime.Construct(
                new MetaRunResultBuilder(),
                new RunResultApplicationPort(),
                new UnityGameClock(),
                EmptyMetaCatalog.Instance,
                Outcomes.AggregateRootStore);
            runtime.ConstructRunResultOutcomeTransactions(
                new MetaUpgradePurchaseOutcomeTransaction(Probe));
            runtime.SetShowRunResultPanel(false);
        }

        public override string CaptureDomain() =>
            runtime.HasEnded + ":" + runtime.State.CompletedRunCount + ":"
            + runtime.State.LifetimeEarnedCurrency + ":"
            + (runtime.LatestResult?.endReason ?? string.Empty);

        public override void Execute() =>
            runtime.EndRun("원장 검증 소유자", "원장 검증 종료");

        public override void Dispose() => Object.DestroyImmediate(root);
    }

    private abstract class RunVariableScenarioBase : EntryScenarioBase
    {
        private readonly GameObject root;
        protected readonly RunVariableRuntime Runtime;
        protected const string VariableId = "run:migrated-e2e:operation-variable";

        protected RunVariableScenarioBase(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-run-variable-", target, fault, deliveryFault)
        {
            root = new GameObject("Migrated Run Variable E2E");
            Runtime = root.AddComponent<RunVariableRuntime>();
            var catalog = new SingleRunVariableCatalog(
                new RunVariableDefinition(
                    VariableId,
                    RunVariableCategory.Operation,
                    "원장 검증 변수",
                    "원장 검증 변수",
                    EventAlertImportance.Low,
                    1,
                    Array.Empty<IRunVariableEffect>()));
            Runtime.Construct(
                EmptyOwnerRunDataProvider.Instance,
                EditorRuntimeReferenceFixtures.Invasion,
                EmptyRunStartVariableSelector.Instance,
                new RandomStreamProvider(54123),
                new GameEventBus(),
                catalog,
                EmptyOwnerDoctrineCatalog.Instance,
                Outcomes.AggregateRootStore,
                Probe);
            Runtime.StartRun(54123, null, InvasionThreatDifficulty.Normal);
            Probe.ClearObservations();
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(Runtime.CaptureForSave());

        public override void Dispose() => Object.DestroyImmediate(root);
    }

    private sealed class RunVariableActivatedScenario : RunVariableScenarioBase
    {
        internal RunVariableActivatedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base(target, fault, deliveryFault)
        {
        }

        public override void Execute() =>
            Runtime.ActivateOperationVariable(VariableId, 1, false);

    }

    private sealed class RunVariableExpiredScenario : RunVariableScenarioBase
    {
        internal RunVariableExpiredScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base(target, fault, deliveryFault)
        {
            Runtime.ActivateOperationVariable(VariableId, 1, false);
            Probe.ClearObservations();
        }

        public override void Execute() =>
            Runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));
    }

    private sealed class CharacterAgeConditionScenario : EntryScenarioBase
    {
        private const string CharacterIdValue =
            "character:world:migrated-e2e:aging";
        private const string SpeciesIdValue = "species:migrated-e2e";
        private const string ConditionId = "age:migrated-e2e";
        private readonly CharacterLifeRuntime life;
        private readonly GameEventBus events;
        private readonly CharacterLifeApplicationAdapter adapter;

        internal CharacterAgeConditionScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-character-life-", target, fault, deliveryFault)
        {
            life = new CharacterLifeRuntime(
                Outcomes.AggregateRootStore,
                CharacterLifeCatalog.Instance,
                new RandomStreamProvider(104729));
            var save = new CharacterLifeWorldSaveData();
            save.characters.Add(new CharacterLifeRecordSaveData
            {
                characterId = CharacterIdValue,
                phenotypeSpeciesId = SpeciesIdValue,
                chronologicalAgeDays = 100,
                biologicalAgeDayUnits = 100d,
                birthdayDayOfYear = 1,
                lifeStage = CharacterLifeCatalog.Instance.History.ResolveStage(100d),
                ageConditions = new List<CharacterAgeConditionSaveData>
                {
                    new()
                    {
                        conditionId = ConditionId,
                        severity = AgeConditionSeverity.Mild,
                        onsetBiologicalAgeDayUnits = 1d,
                        nextProgressBiologicalAgeDayUnits = 101d
                    }
                }
            });
            life.PublishRestore(life.PrepareRestore(save));
            events = new GameEventBus();
            adapter = new CharacterLifeApplicationAdapter(
                life,
                events,
                NeutralHeritableTraitEffects.Instance,
                Probe);
            adapter.Start();
        }

        public override string CaptureDomain() => JsonUtility.ToJson(life.Capture());

        public override void Execute() =>
            events.Publish(new OperatingDayEndedEvent(1));

        public override void Dispose() => adapter.Dispose();
    }

    private sealed class ExteriorReceptionScenario : EntryScenarioBase
    {
        private const string VisitorId =
            "character:world:migrated-e2e:exterior-visitor";
        private readonly CharacterActor visitor;
        private readonly ExteriorZoneMarker zone;
        private readonly InformantExteriorIncidentHandler handler;
        private readonly ExteriorIncidentRuntimeState state;

        internal ExteriorReceptionScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-exterior-reception-", target, fault, deliveryFault)
        {
            GameObject visitorObject = new("Migrated Exterior Visitor");
            visitorObject.AddComponent<SpriteRenderer>();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(
                visitorObject);
            visitor = visitorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(visitorObject);
            CharacterSO data =
                CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                    CharacterType.Customer,
                    "원장 검증 외부 방문객",
                    "Slime");
            visitor.Initialization(data);
            visitor.Identity.SetPersistentId(VisitorId);
            visitor.stats ??= new Dictionary<CharacterCondition, float>();
            visitor.stats[CharacterCondition.MOOD] = 50f;

            GameObject zoneObject = new("Migrated Exterior Zone");
            zone = zoneObject.AddComponent<ExteriorZoneMarker>();
            state = new ExteriorIncidentRuntimeState
            {
                incidentId = "exterior-incident:migrated-e2e",
                kind = ExteriorIncidentKind.Informant,
                stage = ExteriorIncidentStage.Active,
                durationSeconds = 150f,
                remainingSeconds = 150f,
                actorIds = new List<string> { VisitorId }
            };
            handler = new InformantExteriorIncidentHandler(
                new EnteredExteriorActorService(VisitorId, visitor),
                new OffenseRegionRuntime(),
                new GameEventBus(),
                EditorFixedGameCalendar.Instance,
                Probe);
        }

        public override string CaptureDomain()
        {
            CharacterMoodSnapshot mood = visitor.Stats.GetMoodSnapshot();
            return state.receptionApplied + ":" + state.progress + ":"
                + mood.Factors.Count;
        }

        public override void Execute() => handler.Tick(state, zone, 0f);

        public override void ExecuteExpectingRejectedCommit() => Execute();

        public override void Dispose()
        {
            if (visitor?.data != null)
                Object.DestroyImmediate(visitor.data);
            if (visitor != null)
                Object.DestroyImmediate(visitor.gameObject);
            if (zone != null)
                Object.DestroyImmediate(zone.gameObject);
        }
    }

    private sealed class CareerMentorshipAwardScenario : EntryScenarioBase
    {
        private static readonly BuildingInstanceId AcademyId =
            new("building:migrated-e2e:academy");
        private static readonly CharacterId MentorId =
            new("character:world:migrated-e2e:mentor");
        private static readonly CharacterId StudentId =
            new("character:world:migrated-e2e:student");
        private readonly CareerRuntime careers;
        private readonly CareerDurableEquipmentAwardRuntime runtime;
        private readonly CareerEquipmentSlots slots;

        internal CareerMentorshipAwardScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-career-award-", target, fault, deliveryFault)
        {
            careers = new CareerRuntime(Outcomes.AggregateRootStore);
            slots = new CareerEquipmentSlots();
            runtime = new CareerDurableEquipmentAwardRuntime(
                new DurableFacilityEquipmentPolicyRegistry(
                    new IDurableFacilityEquipmentPolicySource[]
                    {
                        new CareerDurableEquipmentPolicySource()
                    }),
                slots,
                new CareerEquipmentUse(slots),
                careers,
                Probe);
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(careers.Capture());

        public override void Execute()
        {
            var mentorship = new CareerMentorshipSnapshot(
                MentorId,
                StudentId,
                AcademyId,
                new CharacterProficiencyId("proficiency:migrated-e2e"),
                0,
                1,
                CareerRules.MentoringWorkAmountPerParticipant,
                CareerRules.MentoringWorkAmountPerParticipant);
            bool committed = runtime.TryCommitAward(
                AcademyId,
                new Vector2Int(7, 3),
                mentorship,
                2,
                () =>
                {
                    careers.AssignPosition(
                        StudentId,
                        CareerPositionKind.Mentor,
                        AcademyId.Value,
                        2);
                    return true;
                },
                out _);
            Require(committed, "Career mentorship award entry was rejected.");
        }

        public override void Dispose() { }
    }

    private sealed class OffenseBattleCommandScenario : EntryScenarioBase
    {
        private const string AllyId = "character:world:migrated-e2e:battle-ally";
        private const string EnemyId = "enemy:migrated-e2e:battle";
        private readonly OffenseBattleRuntime runtime;

        internal OffenseBattleCommandScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-offense-battle-", target, fault, deliveryFault)
        {
            ICombatEquipmentRuntime equipment =
                OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
            runtime = new OffenseBattleRuntime(
                EmptyCharacterWorldSaveService.Instance,
                EditorRuntimeReferenceFixtures.DungeonWithRunVariables,
                new GameEventBus(),
                OffenseEditorTestDependencies.CreateCombatResolution(),
                equipment,
                null,
                null,
                null,
                OffenseEditorTestDependencies.CreateEnemyEncounterFactory(
                    equipment),
                new EnemyTacticalDecisionService(
                    new EnemyCombatContentCatalog(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))),
                EmptyReturnArrivalRuntime.Instance,
                CharacterAiEditorTestDependencies.NeutralPerformance);
            runtime.ConstructProficiencyProgression(
                null,
                EditorFixedGameCalendar.Instance,
                Probe);

            var ally = new OffenseBattleCombatant(
                AllyId,
                "원장 검증 아군",
                "Slime",
                OffenseBattleTeam.Allies,
                new OffenseBattleStats(100f, 10f, 8f, 7f, 12f, 6f),
                100f);
            var enemy = new OffenseBattleCombatant(
                EnemyId,
                "원장 검증 적군",
                "Orc",
                OffenseBattleTeam.Enemies,
                new OffenseBattleStats(100f, 8f, 7f, 6f, 8f, 5f),
                100f);
            var session = new OffenseBattleSession(
                "battle:migrated-e2e",
                "expedition:migrated-e2e",
                "target:migrated-e2e",
                "원장 검증 표적",
                DungeonDifficulty.Normal,
                new[] { ally, enemy },
                OffenseEditorTestDependencies.CreateCombatResolution(),
                equipment);
            runtime.PublishPersistentRestore(new OffenseBattleRestoreCandidate(
                session,
                new Dictionary<string, CharacterActor>(StringComparer.Ordinal),
                true,
                string.Empty,
                Array.Empty<EnemyIndividualSaveData>()));
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(runtime.Session.CapturePersistentState());

        public override void Execute()
        {
            Require(
                runtime.TryExecutePlannedCommand(
                    1,
                    AllyId,
                    EnemyId,
                    OffenseBattleActionType.BasicAttack,
                    string.Empty,
                    out _),
                "Offense battle production command entry was rejected.");
        }

        public override void Dispose() => runtime.Dispose();
    }

    private sealed class OffenseExpeditionNodeScenario : EntryScenarioBase
    {
        private const string ExpeditionId = "expedition:migrated-e2e:node";
        private readonly GameObject root;
        private readonly GameObject worldRoot;
        private readonly OffenseExpeditionRuntime runtime;

        internal OffenseExpeditionNodeScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-offense-node-", target, fault, deliveryFault)
        {
            var events = new GameEventBus();
            worldRoot = new GameObject("Migrated Offense Node World");
            OffenseWorldMapRuntime world =
                worldRoot.AddComponent<OffenseWorldMapRuntime>();
            var campaign = new OffenseCampaignRuntime();
            world.Construct(
                events,
                null,
                campaign,
                campaign,
                OffenseEditorTestDependencies.CreateCampaignCatalog(),
                EditorNoOpOffenseOutcomeCommitter.Instance,
                EditorFixedGameCalendar.Instance);
            world.StartWorldMap();

            root = new GameObject("Migrated Offense Node Runtime");
            runtime = root.AddComponent<OffenseExpeditionRuntime>();
            runtime.Construct(
                EmptyExpeditionMemberQuery.Instance,
                new OffenseSceneRuntimeReferences(
                    world,
                    null,
                    runtime,
                    null,
                    null),
                EmptyOffensePanelService.Instance,
                events,
                EmptyExpeditionResultFinalizer.Instance);
            runtime.ConstructOutcomeTransaction(
                EditorFixedGameCalendar.Instance,
                Probe);

            const string EntranceId = "node:migrated-e2e:entrance";
            const string CacheId = "node:migrated-e2e:cache";
            var route = new OffenseRouteGraph(
                new[]
                {
                    new OffenseRouteNode(
                        EntranceId,
                        0,
                        0,
                        OffenseRouteNodeKind.Entrance,
                        "입구",
                        "원장 검증 입구",
                        1f,
                        new[] { CacheId }),
                    new OffenseRouteNode(
                        CacheId,
                        1,
                        0,
                        OffenseRouteNodeKind.Cache,
                        "보급고",
                        "원장 검증 보급고",
                        1f,
                        Array.Empty<string>())
                },
                EntranceId);
            var expedition = new OffenseExpeditionRun(
                ExpeditionId,
                new OffenseTargetDefinition
                {
                    id = "target:migrated-e2e:node",
                    title = "원장 검증 노드 표적",
                    requiredMembers = 1,
                    requiredPower = 0f,
                    durationSeconds = 10f,
                    campaignOrder = 1
                },
                Array.Empty<CharacterActor>(),
                0f,
                10f,
                route,
                new OffenseSupplyLoadout(
                    new Dictionary<OffenseSupplyType, int>
                    {
                        [OffenseSupplyType.Rations] = 1
                    }),
                new OffenseExpeditionPreparation());
            Require(
                expedition.TryEnterNode(CacheId, out _),
                "Offense node fixture could not enter its cache node.");
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(
                new[] { expedition },
                Array.Empty<OffenseExpeditionResult>()));
        }

        public override string CaptureDomain()
        {
            OffenseExpeditionRun expedition = runtime.ActiveExpeditions.Single();
            return expedition.Phase + ":" + expedition.CurrentNodeId + ":"
                + string.Join(",", expedition.CompletedNodeIds.OrderBy(x => x))
                + ":" + string.Join(",", expedition.CarriedStock
                    .OrderBy(pair => pair.Key)
                    .Select(pair => pair.Key + "=" + pair.Value));
        }

        public override void Execute()
        {
            Require(
                runtime.TryResolveCurrentNode(
                    ExpeditionId,
                    false,
                    out _,
                    out _),
                "Offense expedition-node production entry was rejected.");
        }

        public override void Dispose()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(worldRoot);
        }
    }

    private sealed class OffenseExpeditionResultScenario : EntryScenarioBase
    {
        private readonly GameObject root;
        private readonly OffenseExpeditionResultFinalizer finalizer;
        private readonly OffenseExpeditionRun expedition;
        private readonly OffenseExpeditionResult result;
        private readonly List<OffenseExpeditionResult> history = new();
        private readonly OffenseRewardRuntime rewards;
        private readonly MetaProgressionRuntime meta;
        private readonly ScenarioOffenseCampaign campaign;
        private readonly OffenseExpeditionReturnCoordinator returnCoordinator;
        private readonly CharacterActor participant;
        private readonly CharacterSO participantData;

        internal OffenseExpeditionResultScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            bool success = false)
            : base("run:migrated-entry-offense-result-", target, fault, deliveryFault)
        {
            root = new GameObject("Migrated Offense Result E2E");
            rewards = root.AddComponent<OffenseRewardRuntime>();
            var arrivals = new OffenseReturnArrivalRuntime(
                new DungeonRuntimeAggregateRootStore(),
                PositiveGameClock.Instance,
                EditorFixedGameCalendar.Instance,
                new GameEventBus(),
                Probe);
            rewards.Construct(
                new ScenarioOffenseRewardContextBuilder(arrivals),
                new ScenarioOffenseRewardGrantService());
            meta = root.AddComponent<MetaProgressionRuntime>();
            meta.Construct(
                new MetaRunResultBuilder(),
                new RunResultApplicationPort(),
                new UnityGameClock(),
                EmptyMetaCatalog.Instance,
                new DungeonRuntimeAggregateRootStore());
            campaign = new ScenarioOffenseCampaign(
                "target:migrated-e2e:result");
            var events = new GameEventBus();
            finalizer = new OffenseExpeditionResultFinalizer(
                new OffenseSceneRuntimeReferences(
                    null,
                    rewards,
                    null,
                    null,
                    null),
                new ProgressionSceneRuntimeReferences(null, null, meta),
                events,
                campaign);
            finalizer.ConstructOutcomeTransaction(
                EditorFixedGameCalendar.Instance,
                Probe);
            CharacterActor[] members = Array.Empty<CharacterActor>();
            if (success)
            {
                GameObject participantObject = new(
                    "Migrated Offense Return Participant");
                participantObject.SetActive(false);
                participantObject.AddComponent<SpriteRenderer>();
                participantObject.AddComponent<AIBrain>();
                participant = participantObject.AddComponent<CharacterActor>();
                CharacterAiEditorTestDependencies.Inject(participantObject);
                participantData = CharacterAiEditorTestDependencies
                    .CreateCharacterFixtureData(
                        CharacterType.NPC,
                        "원정 귀환 검증 대원",
                        "Orc");
                participant.Initialization(participantData);
                participant.Identity.SetPersistentId(
                    "character:migrated-e2e:offense-return:"
                    + Guid.NewGuid().ToString("N"));
                members = new[] { participant };
                returnCoordinator = new OffenseExpeditionReturnCoordinator(
                    NoOpOffenseExpeditionReturnPort.Instance,
                    finalizer,
                    events,
                    CharacterAiEditorTestDependencies.NeutralPerformance);
            }
            expedition = new OffenseExpeditionRun(
                "expedition:migrated-e2e:result",
                new OffenseTargetDefinition
                {
                    id = "target:migrated-e2e:result",
                    title = "원장 검증 결과 표적",
                    requiredMembers = 1,
                    requiredPower = 10f,
                    durationSeconds = 10f
                },
                members,
                0f);
            if (success)
                expedition.MemberStates.Single().AddStress(23f);
            result = new OffenseExpeditionResult(
                expedition.ExpeditionId,
                expedition.Target.id,
                expedition.Target.title,
                success,
                0f,
                10f,
                1f,
                5f,
                Array.Empty<OffenseExpeditionMemberSnapshot>(),
                new[] { "원장 검증 실패 결과" });
        }

        public override string CaptureDomain() =>
            history.Count + ":" + string.Join(",", history.Select(x => x.expeditionId))
            + ":reward=" + rewards.State.MoneyEarned
            + ":meta=" + meta.RunProgress.OffenseSuccessCount
            + ":campaign=" + JsonUtility.ToJson(campaign.Capture())
            + ":returnFinalizing=" + expedition.ReturnFinalizing
            + ":participantStress="
            + (participant?.Lifecycle?.ExpeditionRecovery?.stress ?? 0f)
            + ":participantLevel=" + (participant?.Progression?.Level ?? 0)
            + ":participantXp="
            + (participant?.Progression?.CurrentExperience ?? 0);

        public override void Execute()
        {
            if (returnCoordinator != null)
            {
                returnCoordinator.Complete(
                    expedition,
                    true,
                    "원정 귀환 검증",
                    history,
                    null);
                return;
            }
            _ = finalizer.Finalize(expedition, result, history);
        }

        public override void Dispose()
        {
            if (participantData != null)
                Object.DestroyImmediate(participantData);
            if (participant != null)
                Object.DestroyImmediate(participant.gameObject);
            Object.DestroyImmediate(root);
        }
    }

    private sealed class OffenseExpeditionArrivalScenario : EntryScenarioBase
    {
        private readonly OffenseReturnArrivalRuntime runtime;

        internal OffenseExpeditionArrivalScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-offense-arrival-", target, fault, deliveryFault)
        {
            runtime = new OffenseReturnArrivalRuntime(
                Outcomes.AggregateRootStore,
                PositiveGameClock.Instance,
                EditorFixedGameCalendar.Instance,
                new GameEventBus(),
                Probe);
            var save = new DungeonOffenseReturnArrivalSaveData
            {
                nextArrivalSequence = 2
            };
            save.arrivals.Add(new OffenseReturnArrivalState
            {
                arrivalId = "return:1",
                expeditionId = "expedition:migrated-e2e:arrival",
                targetId = "target:migrated-e2e:arrival",
                kind = OffenseReturnArrivalKind.SpecialWildlife,
                requestedAmount = 1,
                returnSealed = true,
                stage = OffenseReturnArrivalStage.AwaitingContainment,
                materializedIds = new List<string> { "wild:1" },
                escapedIds = new List<string> { "wild:1" },
                lastStatus = "원장 검증 수용 대기"
            });
            var report = new DungeonGameRestoreReport();
            OffenseReturnArrivalRestoreCandidate candidate =
                runtime.BuildRestoreCandidate(save, report);
            Require(
                report.Success && candidate != null,
                "Offense arrival fixture restore was rejected.");
            runtime.PublishRestoreCandidate(candidate);
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(runtime.Capture());

        public override void Execute() => runtime.Tick();

        public override void ExecuteExpectingRejectedCommit() => Execute();

        public override void Dispose() { }
    }

    private abstract class FactionScenarioBase : EntryScenarioBase
    {
        protected const string FactionId = "faction:migrated-entry-e2e";
        protected readonly FactionCampaignDouble Campaign;
        protected readonly FactionRuntimeApplicationAdapter Runtime;

        protected FactionScenarioBase(
            string runPrefix,
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            FactionAggregateState initialState,
            IWorldDropZoneQuery dropZones = null,
            IPhysicalItemExactSourcePublicationService exactSources = null,
            IWorldItemStackRuntime itemRuntime = null,
            IIdempotentGameMoneyAccount money = null,
            IReadOnlyList<FactionDefinitionSnapshot> definitions = null,
            IOffenseWorldSimulation world = null,
            IFactionRouteEconomicPolicyRegistry routeEconomicPolicies = null,
            IGameSessionStateStore gameSessionState = null,
            TreasuryEconomyAggregateStateStore treasuryState = null)
            : base(runPrefix, target, fault, deliveryFault)
        {
            Campaign = new FactionCampaignDouble(FactionId, 35);
            Runtime = new FactionRuntimeApplicationAdapter(
                initialState,
                PositiveGameClock.Instance,
                new GameEventBus(),
                money ?? new ScenarioMoneyAccount(),
                Campaign,
                Campaign,
                Campaign,
                Probe,
                Outcomes.AggregateRootStore,
                dropZones,
                exactSources,
                itemRuntime,
                definitions,
                world,
                routeEconomicPolicies,
                gameSessionState,
                treasuryState);
        }

        public override void Dispose() { }
    }

    private sealed class FactionTrustScenario : FactionScenarioBase
    {
        internal FactionTrustScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base(
                "run:migrated-entry-faction-trust-",
                target,
                fault,
                deliveryFault,
                CreateState()) { }

        public override string CaptureDomain()
        {
            Require(
                Runtime.TryGetFaction(FactionId, out DungeonFactionState faction),
                "Faction trust fixture lost its production faction owner.");
            return faction.trust + ":" + Campaign.CaptureRelationship();
        }

        public override void Execute()
        {
            bool applied = Runtime.TryAdjustTrust(
                FactionId,
                7,
                "migrated producer entry E2E",
                out string message);
            if (Probe.FaultMode == MigratedProducerOutcomeFaultMode.RejectCommit)
            {
                Require(!applied, "Faction trust rejection was reported as applied.");
                return;
            }
            Require(applied, "Faction trust entry failed: " + message);
        }

        public override void ExecuteExpectingRejectedCommit() => Execute();

        private static FactionAggregateState CreateState()
        {
            var state = new FactionAggregateState { CurrentDay = 3 };
            state.Factions.Add(
                FactionId,
                new DungeonFactionState
                {
                    factionId = FactionId,
                    discovered = true,
                    trust = 35
                });
            return state;
        }
    }

    private sealed class FactionRouteArrivalScenario : FactionScenarioBase
    {
        internal FactionRouteArrivalScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base(
                "run:migrated-entry-faction-arrival-",
                target,
                fault,
                deliveryFault,
                CreateState()) { }

        public override string CaptureDomain()
        {
            FactionRouteState route = Runtime.Routes.Single();
            return route.status + ":" + route.pathIndex + ":"
                + route.segmentProgress.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + ":" + route.cargoDelivery.state;
        }

        public override void Execute() => Runtime.Tick();
        public override void ExecuteExpectingRejectedCommit() => Execute();

        private static FactionAggregateState CreateState()
        {
            var state = new FactionAggregateState
            {
                CurrentDay = 4,
                RouteSequence = 1
            };
            state.Routes.Add(new FactionRouteState
            {
                routeId = "faction-route:1",
                factionId = FactionId,
                kind = FactionRouteKind.TradeCaravan,
                status = FactionRouteStatus.Traveling,
                path = new List<FactionHexCoordSaveData>
                {
                    new() { q = 1, r = 1 },
                    new() { q = 2, r = 1 }
                },
                pathIndex = 0,
                segmentProgress = 0.999f,
                strength = 100,
                createdDay = 3,
                estimatedArrivalDay = 4,
                cargoDelivery = new FactionRouteCargoDeliveryReceipt
                {
                    state = FactionRouteCargoDeliveryState.NotApplicable
                }
            });
            return state;
        }
    }

    private sealed class FactionRouteCargoScenario : FactionScenarioBase
    {
        internal FactionRouteCargoScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base(
                "run:migrated-entry-faction-cargo-",
                target,
                fault,
                deliveryFault,
                CreateState(),
                FixedDropZone.Instance,
                new ExactSourcePublicationDouble(),
                new EditorWarehouseStockRuntime()) { }

        public override string CaptureDomain()
        {
            FactionRouteCargoDeliveryReceipt receipt =
                Runtime.Routes.Single().cargoDelivery;
            return receipt.state + ":" + receipt.batchCommitId + ":"
                + receipt.destinationId + ":" + receipt.totalMassGrams + ":"
                + string.Join(",", receipt.stacks.Select(value =>
                    value.stackId + "=" + value.quantity));
        }

        public override void Execute() => Runtime.Tick();
        public override void ExecuteExpectingRejectedCommit() => Execute();

        private static FactionAggregateState CreateState()
        {
            var state = new FactionAggregateState
            {
                CurrentDay = 5,
                RouteSequence = 1
            };
            state.Routes.Add(new FactionRouteState
            {
                routeId = "faction-route:1",
                factionId = FactionId,
                kind = FactionRouteKind.TradeCaravan,
                status = FactionRouteStatus.Arrived,
                path = new List<FactionHexCoordSaveData>
                {
                    new() { q = 0, r = 0 },
                    new() { q = 1, r = 0 }
                },
                pathIndex = 1,
                segmentProgress = 1f,
                strength = 100,
                cargo = new List<FactionCargoLine>
                {
                    new() { itemId = "material:migrated-e2e", amount = 3 }
                },
                cargoDelivery = new FactionRouteCargoDeliveryReceipt
                {
                    state = FactionRouteCargoDeliveryState.Ready
                }
            });
            return state;
        }
    }

    private sealed class FactionRouteSettlementScenario : EntryScenarioBase
    {
        private const string FactionId = "faction:migrated-entry-settlement";
        private readonly FactionRuntimeApplicationAdapter runtime;
        private readonly GameSessionState sessionState;

        internal FactionRouteSettlementScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base(
                "run:migrated-entry-faction-settlement-",
                target,
                fault,
                deliveryFault)
        {
            var domain = new FactionAggregateState { CurrentDay = 6 };
            domain.Factions.Add(
                FactionId,
                new DungeonFactionState
                {
                    factionId = FactionId,
                    discovered = true,
                    homeQ = 2,
                    homeR = 3
                });
            var campaign = new FactionCampaignDouble(FactionId, 35);
            sessionState = new GameSessionState(1000, 6);
            var sessionStore = new ScenarioGameSessionStore(sessionState);
            var money = new SessionMoneyAccount(sessionState);
            var treasury = new TreasuryEconomyAggregateStateStore(
                Outcomes.AggregateRootStore);
            FactionDefinitionSnapshot definition = CreateDefinition();
            runtime = new FactionRuntimeApplicationAdapter(
                domain,
                PositiveGameClock.Instance,
                new GameEventBus(),
                money,
                campaign,
                campaign,
                campaign,
                Probe,
                Outcomes.AggregateRootStore,
                definitions: new[] { definition },
                world: new FactionWorldDouble(),
                routeEconomicPolicies: new FactionEconomicPolicyDouble(),
                gameSessionState: sessionStore,
                treasuryState: treasury);
        }

        public override string CaptureDomain()
        {
            FactionRouteState route = runtime.Routes.SingleOrDefault();
            return sessionState.holdingMoney.Value + ":" + runtime.Routes.Count
                + ":" + (route?.routeId ?? string.Empty) + ":"
                + (route?.settlement?.state.ToString() ?? string.Empty) + ":"
                + (route?.settlement?.transactionId ?? string.Empty);
        }

        public override void Execute()
        {
            bool created = runtime.TryRequestTrade(
                FactionId,
                out string routeId,
                out string message);
            if (Probe.FaultMode == MigratedProducerOutcomeFaultMode.RejectCommit)
            {
                Require(!created && routeId.Length == 0,
                    "Faction settlement rejection was reported as created.");
                return;
            }
            Require(created && routeId.Length > 0,
                "Faction settlement production entry failed: " + message);
        }

        public override void ExecuteExpectingRejectedCommit() => Execute();
        public override void Dispose() { }

        private static FactionDefinitionSnapshot CreateDefinition() => new(
            FactionId,
            "원장 검증 세력",
            "human",
            "migrated producer settlement fixture",
            Array.Empty<string>(),
            Array.Empty<string>(),
            string.Empty,
            new[]
            {
                new FactionCargoLine
                {
                    itemId = "material:migrated-e2e",
                    amount = 2
                }
            },
            Array.Empty<FactionCargoLine>(),
            FactionRouteEconomicPolicyDescriptor.Create(
                "faction-economy:migrated-e2e",
                1),
            FactionRouteEconomicPolicyDescriptor.Create(
                "faction-economy:migrated-e2e-supply",
                1),
            7,
            20,
                10);
    }

    private sealed class FestivalCelebratedScenario : EntryScenarioBase
    {
        private const string FestivalId = "festival:migrated-entry-e2e";
        private readonly FestivalDefinitionSO definition;
        private readonly FestivalExecutionRuntime runtime;
        private readonly GriefTraumaRuntime psychosocial;
        private readonly FestivalPreparedOrder order;

        internal FestivalCelebratedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base(
                "run:migrated-entry-festival-",
                target,
                fault,
                deliveryFault)
        {
            definition = ScriptableObject.CreateInstance<FestivalDefinitionSO>();
            definition.festivalId = FestivalId;
            definition.displayName = "원장 검증 축제";
            definition.authoringRevision = 1;
            definition.season = Season.Spring;
            definition.dayOfSeason = 1;
            definition.minimumParticipants = 1;
            definition.requiredItems = new List<FestivalItemRequirement>();
            definition.successOutcome = new FestivalOutcomeDefinition
            {
                griefConversionPercent = 5f
            };
            definition.partialOutcome = new FestivalOutcomeDefinition();
            definition.failureOutcome = new FestivalOutcomeDefinition();

            psychosocial = new GriefTraumaRuntime(Outcomes.AggregateRootStore);
            var campaign = new FactionCampaignDouble(
                "faction:migrated-entry-festival",
                10);
            runtime = new FestivalExecutionRuntime(
                new SingleFestivalCatalog(definition),
                EmptyCharacterWorldQuery.Instance,
                EditorFixedGameCalendar.Instance,
                new GameEventBus(),
                psychosocial,
                campaign,
                campaign,
                campaign,
                Probe);

            string occurrenceId = FestivalExecutionRules.OccurrenceId(
                FestivalId,
                1);
            var state = new FestivalExecutionOccurrenceSaveData
            {
                occurrenceId = occurrenceId,
                actionId = "festival-action:migrated-entry-e2e",
                festivalId = FestivalId,
                occurrenceYear = 1,
                phase = FestivalExecutionPhase.Running,
                festivalAbsoluteDay = 1,
                startHour = FestivalExecutionRules.StartHour,
                deadlineAbsoluteDay = 1,
                deadlineHour = FestivalExecutionRules.StartHour,
                requiredPreparationWork = 1f,
                completedPreparationWork = 1f,
                plannedDurationSeconds = 10f,
                elapsedFestivalSeconds = 10f,
                venueCapacity = 1,
                venueCells = new List<FestivalExecutionCellSaveData>
                {
                    new() { x = 3, y = 4 }
                },
                attendance = new List<FestivalAttendanceProgressSaveData>
                {
                    new()
                    {
                        characterId = "character:migrated-entry-festival",
                        assignedCellX = 3,
                        assignedCellY = 4,
                        attendedSeconds = 10f
                    }
                },
                inputOwnerActive = false,
                materialReceiptPending = false
            };
            var payload = new FestivalExecutionWorldSaveData();
            payload.occurrences.Add(state);
            runtime.PublishFestivalExecutionRestore(
                runtime.PrepareFestivalExecutionRestore(payload));
            order = new FestivalPreparedOrder
            {
                OccurrenceId = occurrenceId,
                FestivalId = FestivalId
            };
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(runtime.CaptureFestivalExecutions()) + ":"
            + JsonUtility.ToJson(psychosocial.Capture());

        public override void Execute()
        {
            bool resolved = runtime.Resolve(order, out DomainFailure failure);
            Require(resolved && !failure.IsFailure,
                "Festival production resolution entry failed: " + failure);
        }

        public override void Dispose() => Object.DestroyImmediate(definition);
    }

    private sealed class V20ResolvedEventScenario : EntryScenarioBase
    {
        private readonly V20CampaignRuntime campaign;
        private readonly V20ContentResolutionService service;
        private readonly ContentResolutionRequest request;
        private readonly CharacterNarrativeRuntime narrative;
        private readonly CharacterActor firstActor;
        private readonly CharacterActor secondActor;
        private readonly CharacterSO firstActorData;
        private readonly CharacterSO secondActorData;

        internal V20ResolvedEventScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base(
                "run:migrated-entry-v20-resolved-",
                target,
                fault,
                deliveryFault)
        {
            var source = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            var catalog = new V20StoryContentCatalog(source);
            LifeEventDefinitionSO definition = catalog.LifeEvents
                .Single(value => string.Equals(
                    value.StableId,
                    "life-event:childhood-bully",
                    StringComparison.Ordinal));
            V20ChoiceDefinition choice = definition.choices.Single(value =>
                string.Equals(
                    value.choiceId,
                    "first",
                    StringComparison.Ordinal));
            Require(
                choice.effects.Count == 1
                && choice.effects[0].kind
                    == V20ContentEffectKind.Relationship,
                "Authored V20 relationship fixture changed.");

            var narrativeCatalog = new CharacterNarrativeCatalog(source);
            narrative = new CharacterNarrativeRuntime(
                Outcomes.AggregateRootStore,
                narrativeCatalog,
                content: source);
            firstActor = CreateV20Actor(
                "Migrated V20 First Actor",
                "character:migrated-entry-v20:first",
                out firstActorData);
            secondActor = CreateV20Actor(
                "Migrated V20 Second Actor",
                "character:migrated-entry-v20:second",
                out secondActorData);
            string speciesId = narrativeCatalog.Cultures[0].defaultSpeciesId;
            narrative.Register(
                firstActor.Identity.TypedPersistentId,
                new CharacterSpeciesId(speciesId),
                Array.Empty<string>(),
                Array.Empty<string>());
            narrative.Register(
                secondActor.Identity.TypedPersistentId,
                new CharacterSpeciesId(speciesId),
                Array.Empty<string>(),
                Array.Empty<string>());

            campaign = new V20CampaignRuntime(
                Outcomes.AggregateRootStore,
                catalog,
                null,
                EmptySeasonalFeedTargetQuery.Instance);
            const string instanceId = "society-event:migrated-entry-e2e";
            var society = new SocietyEventWorldSaveData
            {
                lastEvaluationAbsoluteDay = 2
            };
            society.activeEvents.Add(new V20ActiveEventSaveData
            {
                instanceId = instanceId,
                definitionId = definition.StableId,
                startedAbsoluteDay = 2,
                deadlineAbsoluteDay = 3,
                generation = 1,
                deterministicRoll = 17u,
                contextFactionId = catalog.Arcs.First().factionId,
                participantCharacterIds = new List<string>
                {
                    firstActor.Identity.PersistentId,
                    secondActor.Identity.PersistentId
                },
                riskTier = definition.riskTier,
                riskReason = definition.riskReason
            });
            campaign.PublishSociety(campaign.PrepareSociety(society));
            service = new V20ContentResolutionService(
                campaign,
                catalog,
                AlwaysSatisfiedContentRequirements.Instance,
                new FixedCharacterWorldQuery(firstActor, secondActor),
                EmptyStockQuery.Instance,
                EmptyAtomicItemConsumption.Instance,
                Probe,
                narrative);
            request = new ContentResolutionRequest
            {
                ActionId = "content-resolution:migrated-entry-e2e",
                Kind = ContentResolutionRequestKind.SocietyChoice,
                InstanceId = instanceId,
                ChoiceId = choice.choiceId,
                AbsoluteDay = 2,
                Requirements = new RunMilestoneEvaluationSnapshot()
            };
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(campaign.CaptureSeasonal()) + ":"
            + JsonUtility.ToJson(campaign.CaptureSociety()) + ":"
            + JsonUtility.ToJson(campaign.CaptureFactions()) + ":"
            + JsonUtility.ToJson(campaign.CaptureMilestones()) + ":"
            + JsonUtility.ToJson(narrative.Capture()) + ":"
            + JsonUtility.ToJson(firstActor.SocialMemory.CaptureSnapshot()) + ":"
            + JsonUtility.ToJson(secondActor.SocialMemory.CaptureSnapshot());

        public override void Execute()
        {
            bool resolved = service.TryExecute(
                request,
                out ContentResolutionResult result,
                out DomainFailure failure);
            Require(
                resolved && !failure.IsFailure && result.Resolutions.Count == 1,
                "V20 content production entry failed: " + failure);
        }

        public override void Dispose()
        {
            if (firstActorData != null)
                Object.DestroyImmediate(firstActorData);
            if (secondActorData != null)
                Object.DestroyImmediate(secondActorData);
            if (firstActor != null)
                Object.DestroyImmediate(firstActor.gameObject);
            if (secondActor != null)
                Object.DestroyImmediate(secondActor.gameObject);
        }

        private static CharacterActor CreateV20Actor(
            string objectName,
            string persistentId,
            out CharacterSO data)
        {
            GameObject actorObject = new(objectName);
            actorObject.AddComponent<SpriteRenderer>();
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                objectName,
                "Orc");
            actor.Initialization(data);
            actor.Identity.SetPersistentId(persistentId);
            return actor;
        }
    }

    private sealed class EmptyMetaCatalog : IMetaUpgradeDefinitionCatalog
    {
        internal static readonly EmptyMetaCatalog Instance = new();
        public IReadOnlyCollection<MetaUpgradeDefinition> All =>
            Array.Empty<MetaUpgradeDefinition>();
        public MetaUpgradeDefinition Get(string id) => null;
        public MetaUpgradeDefinition Require(string id) =>
            throw new KeyNotFoundException(id);
    }

    private sealed class ScenarioOffenseRewardContextBuilder :
        IOffenseRewardContextBuilder
    {
        private readonly IOffenseReturnArrivalRuntime arrivals;

        internal ScenarioOffenseRewardContextBuilder(
            IOffenseReturnArrivalRuntime arrivals) =>
            this.arrivals = arrivals
                ?? throw new ArgumentNullException(nameof(arrivals));

        public OffenseRewardContext Create(
            OffenseTargetDefinition target,
            OffenseRewardState state,
            OffenseRewardDebugContext debugContext,
            string expeditionId = "") => new()
        {
            target = target,
            rewardState = state,
            expeditionId = expeditionId,
            returnArrivalRuntime = arrivals
        };
    }

    private sealed class ScenarioOffenseRewardGrantService :
        IOffenseRewardGrantService
    {
        public IReadOnlyList<OffenseRewardGrantResult> GrantRewards(
            IEnumerable<OffenseRewardPreview> rewards,
            OffenseRewardContext context)
        {
            context?.rewardState?.RecordMoney(7);
            return Array.Empty<OffenseRewardGrantResult>();
        }
    }

    private sealed class ScenarioOffenseCampaign :
        IOffenseCampaignCommands,
        IOffenseCampaignRuntime
    {
        private readonly string targetId;
        private OffenseWorldMapState state = new();

        internal ScenarioOffenseCampaign(string targetId)
        {
            this.targetId = targetId;
            state.Reset();
            state.AddKnownTarget(targetId);
        }

        public IOffenseWorldMapStateView State => state;

        public DungeonOffenseCampaignSaveData Capture() => new()
        {
            version = DungeonOffenseCampaignSaveData.CurrentVersion,
            reconLevel = state.ReconLevel,
            selectedTargetId = state.SelectedTargetId,
            knownTargetIds = state.KnownTargetIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            completedTargetIds = state.CompletedTargetIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            revealedTruthTargetId = state.RevealedTruthTargetId
        };

        public OffenseCampaignRestoreCandidate BuildRestoreCandidate(
            DungeonOffenseCampaignSaveData source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var candidate = new OffenseWorldMapState();
            candidate.Restore(
                source.reconLevel,
                source.selectedTargetId,
                source.knownTargetIds,
                source.completedTargetIds,
                source.revealedTruthTargetId);
            return new OffenseCampaignRestoreCandidate(candidate);
        }

        public void PublishRestoreCandidate(
            OffenseCampaignRestoreCandidate candidate) =>
            state = (candidate
                ?? throw new ArgumentNullException(nameof(candidate))).State;

        public bool TryUpgradeRecon(out string message)
        {
            message = string.Empty;
            return false;
        }

        public bool TrySelectTarget(
            string selectedTargetId,
            out OffenseTargetSnapshot snapshot,
            out string message)
        {
            snapshot = null;
            message = string.Empty;
            return false;
        }

        public bool TryRecordSuccessfulExpedition(
            string completedTargetId,
            out OffenseTargetSnapshot completedTarget,
            out string message)
        {
            completedTarget = null;
            message = string.Empty;
            return string.Equals(
                    completedTargetId,
                    targetId,
                    StringComparison.Ordinal)
                && state.MarkTargetCompleted(completedTargetId);
        }

        public bool TryRecordStrategicTruthReveal(
            string completedTargetId,
            out string message)
        {
            message = string.Empty;
            if (!string.Equals(
                    completedTargetId,
                    targetId,
                    StringComparison.Ordinal)
                || !state.MarkTargetCompleted(completedTargetId))
            {
                return false;
            }
            state.RevealTruth(completedTargetId);
            return true;
        }
    }

    private sealed class EmptyOffenseCampaignCommands :
        IOffenseCampaignCommands
    {
        internal static readonly EmptyOffenseCampaignCommands Instance = new();

        public bool TryUpgradeRecon(out string message) =>
            Unsupported(out message);

        public bool TrySelectTarget(
            string targetId,
            out OffenseTargetSnapshot snapshot,
            out string message)
        {
            snapshot = default;
            return Unsupported(out message);
        }

        public bool TryRecordSuccessfulExpedition(
            string targetId,
            out OffenseTargetSnapshot completedTarget,
            out string message)
        {
            completedTarget = default;
            return Unsupported(out message);
        }

        public bool TryRecordStrategicTruthReveal(
            string targetId,
            out string message) => Unsupported(out message);

        private static bool Unsupported(out string message)
        {
            message = "unsupported fixture command";
            return false;
        }
    }

    private sealed class RunResultApplicationPort : IMetaRuntimeApplicationPort
    {
        public void Bind(IMetaRuntimeEventSink runtime) { }
        public void Unbind(IMetaRuntimeEventSink runtime) { }
        public MetaRunEnvironmentSnapshot CaptureRunEnvironment() => new(
            1f,
            DungeonDifficulty.Normal,
            DungeonSurvivalPressure.Standard);
        public void PublishUpgradePurchased(
            MetaUpgradePurchasedEvent purchasedEvent,
            string message) { }
        public void PublishRunResult(RunResultReadyEvent readyEvent) { }
        public void ShowRunResult(RunResultSnapshot result) { }
    }

    private sealed class SingleRunVariableCatalog : IRunVariableDefinitionCatalog
    {
        private readonly RunVariableDefinition definition;

        internal SingleRunVariableCatalog(RunVariableDefinition definition) =>
            this.definition = definition;

        public IReadOnlyCollection<RunVariableDefinition> All =>
            new[] { definition };
        public RunVariableDefinition Get(string id) =>
            string.Equals(id, definition.id, StringComparison.Ordinal)
                ? definition
                : null;
        public RunVariableDefinition Require(string id) => Get(id)
            ?? throw new KeyNotFoundException(id);
        public IReadOnlyList<RunVariableDefinition> GetByCategory(
            RunVariableCategory category) => definition.category == category
                ? new[] { definition }
                : Array.Empty<RunVariableDefinition>();
    }

    private sealed class EmptyOwnerRunDataProvider : IOwnerRunDataProvider
    {
        internal static readonly EmptyOwnerRunDataProvider Instance = new();
        public CharacterSO SelectedOwnerData => null;
    }

    private sealed class EmptyRunStartVariableSelector : IRunStartVariableSelector
    {
        internal static readonly EmptyRunStartVariableSelector Instance = new();

        public RunStartVariableSnapshot Create(
            int seed,
            CharacterSO ownerData,
            DungeonDifficulty difficulty,
            DungeonSurvivalPressure survivalPressure =
                DungeonSurvivalPressure.Standard) => new(
            seed,
            "Unknown",
            difficulty,
            Array.Empty<int>(),
            Array.Empty<string>(),
            Array.Empty<int>(),
            seed,
            "migrated-e2e",
            1f,
            survivalPressure: survivalPressure);
    }

    private sealed class EmptyOwnerDoctrineCatalog : IOwnerDoctrineDefinitionCatalog
    {
        internal static readonly EmptyOwnerDoctrineCatalog Instance = new();
        public IReadOnlyCollection<OwnerDoctrineDefinition> All =>
            Array.Empty<OwnerDoctrineDefinition>();
        public OwnerDoctrineDefinition Get(string id) => null;
        public OwnerDoctrineDefinition Require(string id) =>
            throw new KeyNotFoundException(id);
        public OwnerDoctrineDefinition ResolveFor(CharacterSO owner) => null;
        public OwnerDoctrineDefinition ResolveForSpecies(string speciesTag) => null;
    }

    private sealed class CharacterLifeCatalog : ICharacterLifeDefinitionCatalog
    {
        internal static readonly CharacterLifeCatalog Instance = new();
        internal SpeciesLifeHistoryDefinition History { get; } = new(
            new CharacterSpeciesId("species:migrated-e2e"),
            1,
            5,
            10,
            70,
            80f,
            false);
        private readonly AgeConditionDefinition condition = new(
            "age:migrated-e2e",
            false,
            new[] { "body:core" });

        public SpeciesLifeHistoryDefinition RequireLifeHistory(
            CharacterSpeciesId speciesId) => speciesId.Equals(History.SpeciesId)
                ? History
                : throw new KeyNotFoundException(speciesId.Value);

        public AgeConditionDefinition RequireAgeCondition(string conditionId) =>
            string.Equals(
                conditionId,
                condition.ConditionId,
                StringComparison.Ordinal)
                ? condition
                : throw new KeyNotFoundException(conditionId);

        public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(
            bool construct) => construct == condition.ConstructCondition
                ? new[] { condition }
                : Array.Empty<AgeConditionDefinition>();
    }

    private sealed class NeutralHeritableTraitEffects : IHeritableTraitEffectQuery
    {
        internal static readonly NeutralHeritableTraitEffects Instance = new();

        public float GetMultiplier(
            CharacterId characterId,
            HeritableTraitConsequenceKind kind,
            string targetId) => 1f;
    }

    private sealed class EnteredExteriorActorService : IExteriorIncidentActorService
    {
        private readonly string actorId;
        private readonly CharacterActor actor;

        internal EnteredExteriorActorService(string actorId, CharacterActor actor)
        {
            this.actorId = actorId;
            this.actor = actor;
        }

        public bool TrySpawn(
            ExteriorIncidentKind kind,
            string requestedActorId,
            Vector2Int position,
            bool downed,
            out CharacterActor spawned,
            out string failureReason)
        {
            spawned = actor;
            failureReason = string.Empty;
            return string.Equals(
                requestedActorId,
                actorId,
                StringComparison.Ordinal);
        }

        public bool TryFind(string requestedActorId, out CharacterActor found)
        {
            found = string.Equals(requestedActorId, actorId, StringComparison.Ordinal)
                ? actor
                : null;
            return found != null;
        }

        public bool HasEnteredDungeon(string requestedActorId) =>
            string.Equals(requestedActorId, actorId, StringComparison.Ordinal);

        public void Despawn(string requestedActorId) { }
    }

    private sealed class CareerEquipmentSlots : IDurableFacilityEquipmentSlotCommand
    {
        internal DurableFacilityEquipmentSlotSnapshot Snapshot { get; private set; }

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired)
        {
            const long sequence = 1L;
            Snapshot = new DurableFacilityEquipmentSlotSnapshot(
                desired,
                sequence,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    desired.Key,
                    sequence),
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    desired.Key,
                    sequence),
                DurableFacilityEquipmentFingerprint.CreateAssignment(desired),
                new DurableFacilityEquipmentCapacityProjection(
                    desired.CapacityPolicyKind,
                    new PhysicalMassGrams(1_000L),
                    1L,
                    new string('a', 64)),
                new[]
                {
                    new DurableFacilityEquipmentRequirementStatus(
                        desired.Requirements[0],
                        0,
                        1)
                });
            return new DurableFacilityEquipmentSlotResult(
                DurableFacilityEquipmentSlotStatus.Applied,
                Snapshot,
                string.Empty);
        }

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key) =>
            new(
                DurableFacilityEquipmentSlotStatus.Replay,
                Snapshot,
                string.Empty);

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode) => throw new NotSupportedException();

        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();
    }

    private sealed class CareerEquipmentUse : IDurableFacilityEquipmentUseCommand
    {
        private readonly CareerEquipmentSlots slots;

        internal CareerEquipmentUse(CareerEquipmentSlots slots) =>
            this.slots = slots;

        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect)
        {
            DurableFacilityEquipmentSlotSnapshot snapshot = slots.Snapshot;
            DurableFacilityEquipmentRequirement requirement =
                snapshot.Assignment.Requirements[0];
            var before = new DurableFacilityEquipmentUseSubject(
                "stack:migrated-e2e:career-ledger",
                1L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            if (!effect.TryPreflight(
                    snapshot,
                    requirement,
                    before,
                    wearAmount,
                    out string failure))
            {
                return new DurableFacilityEquipmentUseResult(
                    DurableFacilityEquipmentUseStatus.Deferred,
                    snapshot,
                    string.Empty,
                    failure);
            }

            var after = new DurableFacilityEquipmentUseSubject(
                before.StackId,
                2L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            if (!effect.TryCommit(
                    new DurableFacilityEquipmentUseContext(
                        snapshot,
                        requirement,
                        before,
                        after,
                        wearAmount),
                    out failure))
            {
                return new DurableFacilityEquipmentUseResult(
                    DurableFacilityEquipmentUseStatus.Deferred,
                    snapshot,
                    string.Empty,
                    failure);
            }

            return new DurableFacilityEquipmentUseResult(
                DurableFacilityEquipmentUseStatus.Applied,
                snapshot,
                before.StackId,
                string.Empty);
        }
    }

    private sealed class EmptyCharacterWorldSaveService : ICharacterWorldSaveService
    {
        internal static readonly EmptyCharacterWorldSaveService Instance = new();

        public DungeonCharacterWorldSaveData Capture(Grid grid) => new();
        public void ValidateRestorePayload(
            Grid grid,
            DungeonCharacterWorldSaveData source) { }
        public CharacterWorldRestoreCandidate PrepareRestoreCandidate(
            Grid grid,
            DungeonCharacterWorldSaveData source) => null;
        public void StageRestoreCandidate(CharacterWorldRestoreCandidate candidate) { }
        public bool TryGetPersistentId(
            CharacterActor actor,
            out string persistentId)
        {
            persistentId = string.Empty;
            return false;
        }
        public string GetOrAssignPersistentId(CharacterActor actor) =>
            actor?.Identity?.PersistentId ?? string.Empty;
        public bool TryGetRestoredActor(
            string persistentId,
            out CharacterActor actor)
        {
            actor = null;
            return false;
        }
    }

    private sealed class EmptyReturnArrivalRuntime : IOffenseReturnArrivalRuntime
    {
        internal static readonly EmptyReturnArrivalRuntime Instance = new();
        public IReadOnlyList<OffenseReturnArrivalState> Arrivals =>
            Array.Empty<OffenseReturnArrivalState>();
        public IReadOnlyList<OffenseExpeditionArrivalReceipt>
            GetSettlementReceipts(string expeditionId) =>
                Array.Empty<OffenseExpeditionArrivalReceipt>();
        public void BeginExpeditionReturn(string expeditionId) { }
        public void RegisterReturningMember(string expeditionId) { }
        public void CompleteReturningMember(string expeditionId) { }
        public void SealExpeditionReturn(string expeditionId) { }
        public void RegisterBattlePrisonerCandidates(
            string expeditionId,
            IEnumerable<EnemyIndividualSaveData> individuals) { }
        public void DiscardBattlePrisonerCandidates(string expeditionId) { }
        public int QueueArrival(
            string expeditionId,
            string targetId,
            OffenseReturnArrivalKind kind,
            int amount) => Math.Max(0, amount);
        public DungeonOffenseReturnArrivalSaveData Capture() => new();
        public OffenseReturnArrivalRestoreCandidate BuildRestoreCandidate(
            DungeonOffenseReturnArrivalSaveData saveData,
            DungeonGameRestoreReport report) => null;
        public void PublishRestoreCandidate(
            OffenseReturnArrivalRestoreCandidate candidate) { }
    }

    private sealed class EmptyExpeditionMemberQuery : IOffenseExpeditionMemberQuery
    {
        internal static readonly EmptyExpeditionMemberQuery Instance = new();
        public IReadOnlyList<CharacterActor> GetAvailableMemberActors() =>
            Array.Empty<CharacterActor>();
    }

    private sealed class EmptyOffensePanelService : IOffensePanelService
    {
        internal static readonly EmptyOffensePanelService Instance = new();
        public OffenseWorldMapPanel ShowWorldMap() => null;
        public OffenseExpeditionPanel ShowExpedition(
            OffenseExpeditionRuntime runtime) => null;
    }

    private sealed class EmptyExpeditionResultFinalizer :
        IOffenseExpeditionResultFinalizer
    {
        internal static readonly EmptyExpeditionResultFinalizer Instance = new();
        public OffenseExpeditionResult Finalize(
            OffenseExpeditionRun expedition,
            OffenseExpeditionResult result,
            List<OffenseExpeditionResult> resultHistory,
            Action finalizeReturn = null)
        {
            finalizeReturn?.Invoke();
            return result;
        }
    }

    private sealed class PositiveGameClock : IGameClock
    {
        internal static readonly PositiveGameClock Instance = new();
        public float DeltaTime => 0.1f;
        public float Time => 1f;
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private sealed class ScenarioMoneyAccount : IIdempotentGameMoneyAccount
    {
        public int Balance { get; private set; } = 100000;
        public bool CanSpend(int amount) => amount >= 0 && Balance >= amount;

        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (!CanSpend(amount))
            {
                reason = "insufficient fixture balance";
                return false;
            }
            Balance -= amount;
            reason = string.Empty;
            return true;
        }

        public void Add(int amount) => Balance += amount;
        public void Add(int amount, EconomyTransactionContext context) => Add(amount);
        public void SetBalance(int amount, EconomyTransactionContext context) =>
            Balance = amount;

        public bool TrySpendOnce(
            int amount,
            EconomyTransactionContext context,
            out EconomyTransactionRecord receipt,
            out string reason)
        {
            int before = Balance;
            bool spent = TrySpend(amount, context, out reason);
            receipt = new EconomyTransactionRecord
            {
                transactionId = context.sourceId + ":fixture",
                kind = context.kind,
                sourceId = context.sourceId,
                targetId = context.targetId,
                amount = spent ? -amount : 0,
                balanceBefore = before,
                balanceAfter = Balance,
                succeeded = spent
            };
            return spent;
        }

        public bool TryCreditOnce(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            Add(amount);
            reason = string.Empty;
            return true;
        }
    }

    private sealed class SessionMoneyAccount : IIdempotentGameMoneyAccount
    {
        private readonly GameSessionState state;
        private readonly Dictionary<string, EconomyTransactionRecord> debits =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> credits = new(StringComparer.Ordinal);

        internal SessionMoneyAccount(GameSessionState state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));

        public int Balance => state.holdingMoney.Value;
        public bool CanSpend(int amount) => amount >= 0 && Balance >= amount;
        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (!CanSpend(amount))
            {
                reason = "insufficient fixture balance";
                return false;
            }
            state.holdingMoney.Value = Balance - amount;
            reason = string.Empty;
            return true;
        }

        public void Add(int amount) => state.holdingMoney.Value = Balance + amount;
        public void Add(int amount, EconomyTransactionContext context) => Add(amount);
        public void SetBalance(int amount, EconomyTransactionContext context) =>
            state.holdingMoney.Value = Math.Max(0, amount);

        public bool TrySpendOnce(
            int amount,
            EconomyTransactionContext context,
            out EconomyTransactionRecord receipt,
            out string reason)
        {
            if (debits.TryGetValue(context.sourceId ?? string.Empty, out receipt))
            {
                reason = string.Empty;
                return receipt.succeeded;
            }
            int before = Balance;
            bool spent = TrySpend(amount, context, out reason);
            receipt = new EconomyTransactionRecord
            {
                transactionId = "transaction:" + (context.sourceId ?? string.Empty),
                kind = context.kind,
                sourceId = context.sourceId,
                targetId = context.targetId,
                amount = spent ? -amount : 0,
                balanceBefore = before,
                balanceAfter = Balance,
                succeeded = spent
            };
            debits[context.sourceId ?? string.Empty] = receipt;
            return spent;
        }

        public bool TryCreditOnce(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (credits.Add(context.sourceId ?? string.Empty))
                Add(amount);
            reason = string.Empty;
            return true;
        }
    }

    private sealed class ScenarioGameSessionStore : IGameSessionStateStore
    {
        private readonly GameSessionState state;
        internal ScenarioGameSessionStore(GameSessionState state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        public bool TryGetSessionState(out GameSessionState session)
        {
            session = state;
            return true;
        }
        public void Restore(GameSessionSnapshot snapshot)
        {
            state.holdingMoney.Initialize(snapshot.Money);
            state.day.Initialize(snapshot.Day);
            state.gameSpeed.Initialize(snapshot.GameSpeed);
            state.curTime.Initialize(snapshot.ElapsedSeconds);
            state.hour.Initialize(snapshot.Hour);
            state.timeOfDay.Initialize(snapshot.TimeOfDay);
        }
    }

    private sealed class FactionEconomicPolicyDouble :
        IFactionRouteEconomicPolicyRegistry
    {
        public bool TryCreateQuote(
            FactionDefinitionSnapshot definition,
            FactionRouteKind routeKind,
            out FactionRouteQuoteSnapshot quote,
            out string failureReason)
        {
            quote = new FactionRouteQuoteSnapshot(
                definition.StableId,
                routeKind,
                definition.TradeEconomicPolicy.capabilityId,
                definition.TradeEconomicPolicy.capabilityVersion,
                20,
                10,
                Array.Empty<FactionRouteQuoteLineReceipt>(),
                "source-digest:migrated-e2e",
                "quote-digest:migrated-e2e");
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FactionWorldDouble : IOffenseWorldSimulation
    {
        public int WorldSeed => 1;
        public int WorldDay => 6;
        public float WorldHour => 0f;
        public OffenseHexCoord DungeonCoord => new(0, 0);
        public IReadOnlyCollection<OffenseHexTileState> Tiles =>
            Array.Empty<OffenseHexTileState>();
        public IReadOnlyCollection<OffenseWorldSiteStateData> Sites =>
            Array.Empty<OffenseWorldSiteStateData>();
        public IReadOnlyCollection<OffenseUrgentSiteStateData> UrgentSites =>
            Array.Empty<OffenseUrgentSiteStateData>();
        public event Action Changed { add { } remove { } }
        public void Initialize(int runSeed) { }
        public void AdvanceHours(float hours) { }
        public bool TryGetTile(
            OffenseHexCoord coord,
            out OffenseHexTileState tile)
        {
            tile = null;
            return false;
        }
        public bool TryGetSite(
            string siteId,
            out OffenseWorldSiteStateData site)
        {
            site = null;
            return false;
        }
        public bool TryGetUrgentSite(
            string siteId,
            out OffenseUrgentSiteStateData site)
        {
            site = null;
            return false;
        }
        public bool TryRevealSite(string siteId) => false;
        public bool TryEngageSite(string siteId) => false;
        public bool TryResolveSite(string siteId) => false;
        public bool TryRegisterStrategicSite(OffenseWorldSiteStateData site) => true;
        public bool TrySpawnUrgentSite(
            string definitionId,
            OffenseHexCoord coord,
            out string siteId)
        {
            siteId = string.Empty;
            return false;
        }
        public bool TryMitigateUrgentSite(string siteId, float amount) => false;
        public bool TryDestroyUrgentSite(string siteId) => false;
        public bool TryFindPath(
            OffenseHexCoord start,
            OffenseHexCoord goal,
            OffenseTravelProfile profile,
            out IReadOnlyList<OffenseHexCoord> path,
            out float totalCost)
        {
            path = new[] { start, goal };
            totalCost = 1f;
            return true;
        }
        public int GetMinimumStepDistance(
            OffenseHexCoord start,
            OffenseHexCoord goal) => 1;
        public OffenseWorldSaveData Capture() => new();
        public void Restore(OffenseWorldSaveData snapshot) { }
    }

    private sealed class FixedDropZone : IWorldDropZoneQuery
    {
        internal static readonly FixedDropZone Instance = new();
        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = new Vector2Int(4, 7);
            return true;
        }
        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = default;
            return false;
        }
        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class ExactSourcePublicationDouble :
        IPhysicalItemExactSourcePublicationService
    {
        private PhysicalItemExactSourcePublicationPlan plan;

        public bool TryPrepare(
            PhysicalItemExactSourcePublicationPlan requested,
            out PhysicalItemExactSourcePublicationTransaction transaction,
            out string failureReason)
        {
            plan = requested ?? throw new ArgumentNullException(nameof(requested));
            FacilityBufferPublishedOutputStackReceipt[] stacks = plan.Outputs
                .Select((output, index) =>
                    new FacilityBufferPublishedOutputStackReceipt(
                        "stack:faction-migrated-e2e:" + index,
                        output.OutputLineId,
                        output.ItemDefinitionId,
                        output.Quantity,
                        new PhysicalMassGrams(output.Quantity * 100L),
                        string.Empty))
                .ToArray();
            transaction = new PhysicalItemExactSourcePublicationTransaction(
                plan.BatchCommitId,
                plan.DestinationId,
                stacks);
            failureReason = string.Empty;
            return true;
        }

        public bool TryCommitRetained(
            PhysicalItemExactSourcePublicationTransaction transaction,
            out PhysicalItemExactSourcePublicationReceipt receipt,
            out string failureReason) =>
            Commit(transaction, retained: true, out receipt, out failureReason);

        public bool TryCommitReleased(
            PhysicalItemExactSourcePublicationTransaction transaction,
            Vector2Int releasePosition,
            string reasonCode,
            out PhysicalItemExactSourcePublicationReceipt receipt,
            out string failureReason) =>
            Commit(transaction, retained: false, out receipt, out failureReason);

        public bool TryCommitReleased(
            PhysicalItemExactSourcePublicationTransaction transaction,
            FacilityBufferAcknowledgedOutputReleaseTarget target,
            string reasonCode,
            out PhysicalItemExactSourcePublicationReceipt receipt,
            out string failureReason) =>
            Commit(transaction, retained: false, out receipt, out failureReason);

        public bool TryRollback(
            PhysicalItemExactSourcePublicationTransaction transaction,
            string reasonCode,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryReleaseRetained(
            PhysicalItemExactSourcePublicationPlan requested,
            Vector2Int releasePosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            releasedQuantity = 0;
            failureReason = "unsupported fixture operation";
            return false;
        }

        public bool TrySinkRetained(
            PhysicalItemExactSourcePublicationPlan requested,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt disposition,
            out string failureReason)
        {
            disposition = default;
            failureReason = "unsupported fixture operation";
            return false;
        }

        private bool Commit(
            PhysicalItemExactSourcePublicationTransaction transaction,
            bool retained,
            out PhysicalItemExactSourcePublicationReceipt receipt,
            out string failureReason)
        {
            Require(plan != null, "Exact-source fixture was committed before prepare.");
            var publication = new FacilityBufferPlannedOutputPublicationReceipt(
                "admission:faction-migrated-e2e",
                transaction.BatchCommitId,
                plan.OutcomeFingerprint,
                transaction.DestinationId,
                plan.DropPosition,
                plan.OwnerDomain,
                plan.OwnerOperationId,
                plan.AuthorityOwnerId,
                1L,
                plan.OutcomeFingerprint,
                transaction.PreparedStacks);
            receipt = new PhysicalItemExactSourcePublicationReceipt(
                publication,
                transaction.PreparedStacks.Sum(value => value.MassGrams),
                retained);
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FactionCampaignDouble :
        IFactionCampaignQuery,
        IFactionCampaignCommand,
        IV20CampaignPersistence
    {
        private FactionCampaignStateSaveData relationship;

        internal FactionCampaignDouble(string factionId, int rapport)
        {
            relationship = new FactionCampaignStateSaveData
            {
                factionId = factionId,
                rapport = rapport
            };
        }

        public IReadOnlyList<FactionCampaignStateSaveData> Factions =>
            new[] { relationship };

        public bool TryGetFaction(
            string factionId,
            out FactionCampaignStateSaveData state)
        {
            state = string.Equals(
                factionId,
                relationship.factionId,
                StringComparison.Ordinal)
                ? relationship
                : null;
            return state != null;
        }

        public void ApplyFactionChange(
            string factionId,
            int rapportDelta,
            int grievanceDelta,
            int obligationDelta)
        {
            Require(
                string.Equals(factionId, relationship.factionId, StringComparison.Ordinal),
                "Faction campaign fixture received an unknown faction.");
            relationship.rapport = Math.Clamp(
                relationship.rapport + rapportDelta,
                -100,
                100);
            relationship.grievance = Math.Clamp(
                relationship.grievance + grievanceDelta,
                0,
                100);
            relationship.obligationTokens = Math.Clamp(
                relationship.obligationTokens + obligationDelta,
                0,
                5);
        }

        internal string CaptureRelationship() =>
            relationship.rapport + ":" + relationship.grievance + ":"
            + relationship.obligationTokens;

        public FactionCampaignWorldSaveData CaptureFactions() =>
            CloneFactions(relationship);

        public FactionCampaignAggregateState PrepareFactions(
            FactionCampaignWorldSaveData data) =>
            new() { Data = CloneFactions(data?.factions?.SingleOrDefault()) };

        public FactionCampaignAggregateState PrepareFactionsPreflight(
            FactionCampaignWorldSaveData data) => PrepareFactions(data);

        public FactionCampaignAggregateState PrepareFactionsRestore(
            FactionCampaignWorldSaveData data) => PrepareFactions(data);

        public void PublishFactions(FactionCampaignAggregateState state)
        {
            relationship = CloneRelationship(
                state?.Data?.factions?.SingleOrDefault());
        }

        public bool TryResolveChapter(
            string factionId,
            string choiceId,
            RunMilestoneEvaluationSnapshot requirements,
            out V20ResolvedEventResult result,
            out string failure) => Unsupported(out result, out failure);

        public bool TryAcceptContract(
            string factionId,
            string contractId,
            int absoluteDay,
            out string failure)
        {
            failure = "unsupported fixture command";
            return false;
        }

        public bool TryResolveContract(
            string factionId,
            bool success,
            RunMilestoneEvaluationSnapshot requirements,
            out V20ResolvedEventResult result,
            out string failure) => Unsupported(out result, out failure);

        public SeasonalEventWorldSaveData CaptureSeasonal() => new();
        public SocietyEventWorldSaveData CaptureSociety() => new();
        public RunMilestoneWorldSaveData CaptureMilestones() => new();
        public SeasonalEventAggregateState PrepareSeasonal(
            SeasonalEventWorldSaveData data) => new();
        public SeasonalEventAggregateState PrepareSeasonalRestore(
            SeasonalEventWorldSaveData data) => new();
        public SocietyEventAggregateState PrepareSociety(
            SocietyEventWorldSaveData data) => new();
        public RunMilestoneAggregateState PrepareMilestones(
            RunMilestoneWorldSaveData data) => new();
        public void PublishSeasonal(SeasonalEventAggregateState state) { }
        public void PublishSeasonalRestore(SeasonalEventAggregateState state) { }
        public void PublishSociety(SocietyEventAggregateState state) { }
        public void PublishMilestones(RunMilestoneAggregateState state) { }

        private static bool Unsupported(
            out V20ResolvedEventResult result,
            out string failure)
        {
            result = default;
            failure = "unsupported fixture command";
            return false;
        }

        private static FactionCampaignWorldSaveData CloneFactions(
            FactionCampaignStateSaveData state)
        {
            var data = new FactionCampaignWorldSaveData();
            if (state != null)
                data.factions.Add(CloneRelationship(state));
            return data;
        }

        private static FactionCampaignStateSaveData CloneRelationship(
            FactionCampaignStateSaveData source) => new()
        {
            factionId = source?.factionId ?? string.Empty,
            rapport = source?.rapport ?? 0,
            grievance = source?.grievance ?? 0,
            obligationTokens = source?.obligationTokens ?? 0
        };
    }

    private sealed class SingleFestivalCatalog : IFestivalDefinitionCatalog
    {
        private readonly FestivalDefinitionSO definition;
        internal SingleFestivalCatalog(FestivalDefinitionSO definition) =>
            this.definition = definition
                ?? throw new ArgumentNullException(nameof(definition));
        public IReadOnlyList<FestivalDefinitionSO> All => new[] { definition };
        public FestivalDefinitionSO Require(string festivalId)
        {
            if (!string.Equals(
                    festivalId,
                    definition.StableId,
                    StringComparison.Ordinal))
            {
                throw new KeyNotFoundException(
                    "Unknown festival fixture ID: " + festivalId);
            }
            return definition;
        }
    }

    private sealed class EmptyCharacterWorldQuery : ICharacterWorldQuery
    {
        internal static readonly EmptyCharacterWorldQuery Instance = new();
        public int CharacterVersion => 0;
        public IReadOnlyList<CharacterActor> Characters =>
            Array.Empty<CharacterActor>();
    }

    private sealed class FixedCharacterWorldQuery : ICharacterWorldQuery
    {
        private readonly CharacterActor[] characters;

        internal FixedCharacterWorldQuery(params CharacterActor[] characters) =>
            this.characters = characters
                ?.Where(value => value != null)
                .ToArray()
                ?? Array.Empty<CharacterActor>();

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => characters;
    }

    private sealed class EmptyStockQuery : IStockQuery
    {
        internal static readonly EmptyStockQuery Instance = new();

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();

        public int GetGlobalQuantity(string itemDefinitionId) => 0;

        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;

        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) => 0;

        public int GetWarehouseTotal(BuildingInstanceId warehouseId) => 0;
    }

    private sealed class EmptyAtomicItemConsumption :
        IAtomicItemConsumptionService
    {
        internal static readonly EmptyAtomicItemConsumption Instance = new();

        public bool TryConsumeReserved(
            IReadOnlyList<ReservedItemConsumption> consumptions,
            string reservationOwnerId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return consumptions == null || consumptions.Count == 0;
        }
    }

    private sealed class AlwaysSatisfiedContentRequirements :
        IContentRequirementEvaluator
    {
        internal static readonly AlwaysSatisfiedContentRequirements Instance =
            new();
        public bool TryEvaluate(
            V20ContentRequirementSet requirements,
            RunMilestoneEvaluationSnapshot world,
            IReadOnlyList<string> participantCharacterIds,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }
        public bool IsLivingCharacterAtStage(
            CharacterId characterId,
            CharacterLifeStage lifeStage) => false;
    }

    private sealed class EmptySeasonalFeedTargetQuery :
        ISeasonalFeedSelfHeatingTargetQuery
    {
        internal static readonly EmptySeasonalFeedTargetQuery Instance = new();
        public bool TrySelect(
            SeasonalFeedSelfHeatingFireProfile profile,
            out SeasonalFeedSelfHeatingTarget target,
            out SeasonalFeedSelfHeatingTargetFailure reason)
        {
            target = default;
            reason = default;
            return false;
        }
        public bool TryRevalidate(
            in SeasonalFeedSelfHeatingTarget selected,
            SeasonalFeedSelfHeatingFireProfile profile,
            out SeasonalFeedSelfHeatingTarget current,
            out SeasonalFeedSelfHeatingTargetFailure reason)
        {
            current = default;
            reason = default;
            return false;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected " + typeof(TException).Name + " was not surfaced.");
    }
}
#endif
