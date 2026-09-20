#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MigratedProducerCharacterSurvivalEntryE2EDebugScenarios
{
    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        new[]
        {
            MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
            MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult,
            MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult,
            MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent,
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt,
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
            MigratedProducerOutcomeKind.MealMissedEvent,
            MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
            MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt,
            MigratedProducerOutcomeKind.ObservedLineageCompressionLifeEventReceipt,
            MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult,
            MigratedProducerOutcomeKind.ObservedQuietPromotionLifeEventReceipt,
            MigratedProducerOutcomeKind.OwnerRunEndedEvent,
            MigratedProducerOutcomeKind.RestOutcomeIdentityEvent
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            Require(
                CoveredKinds.Count == CoveredKinds.Distinct().Count(),
                "Character/survival migrated-producer coverage contains a duplicate kind.");
            var observed = new HashSet<MigratedProducerOutcomeKind>();

            VerifyConsumableMeal(observed);
            VerifyConsumableSubstance(observed);
            VerifyDetoxTreatment(observed);
            VerifyLifeStageChanged(observed);
            VerifyPrimitiveSurvivalCompleted(observed);
            VerifyProficiencyAward(observed);
            VerifyTabooIncident(observed);
            VerifyWaterConsumed(observed);
            VerifyMealMissed(observed);
            VerifyObservedFuneral(observed);
            VerifyObservedLastLesson(observed);
            VerifyLineageCompression(observed);
            VerifyObservedMealIncident(observed);
            VerifyObservedQuietPromotion(observed);
            VerifyOwnerRunEnded(observed);
            VerifyRestOutcome(observed);

            Require(
                observed.SetEquals(CoveredKinds),
                "Character/survival migrated-producer observed coverage did not match CoveredKinds."
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

    private static void VerifyConsumableMeal(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
            (kind, fault, delivery) => new ConsumablesScenario(
                kind,
                ConsumablesEntry.Meal,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.CharacterConsumablesMealResult);
    }

    private static void VerifyConsumableSubstance(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult,
            (kind, fault, delivery) => new ConsumablesScenario(
                kind,
                ConsumablesEntry.Substance,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult);
    }

    private static void VerifyDetoxTreatment(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult,
            (kind, fault, delivery) => new ConsumablesScenario(
                kind,
                ConsumablesEntry.Detox,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult);
    }

    private static void VerifyLifeStageChanged(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent,
            (kind, fault, delivery) => new LifeStageScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent);
    }

    private static void VerifyProficiencyAward(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt,
            (kind, fault, delivery) => new ProficiencyAwardScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt);
    }

    private static void VerifyPrimitiveSurvivalCompleted(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            (kind, fault, delivery) => new PrimitiveSurvivalScenario(
                kind,
                fault,
                delivery));
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            (kind, fault, delivery) => new ConsumablesScenario(
                kind,
                ConsumablesEntry.Meal,
                fault,
                delivery,
                primitiveFieldMeal: true));
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            (kind, fault, delivery) => new PrimitiveSurvivalScenario(
                kind,
                fault,
                delivery,
                CharacterPrimitiveSurvivalActionKind.Latrine));
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            (kind, fault, delivery) => new PrimitiveSurvivalScenario(
                kind,
                fault,
                delivery,
                CharacterPrimitiveSurvivalActionKind.BucketWash));
        observed.Add(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent);
    }

    private static void VerifyMealMissed(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.MealMissedEvent,
            (kind, fault, delivery) => new MealMissedScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.MealMissedEvent);
    }

    private static void VerifyWaterConsumed(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
            (kind, fault, delivery) => new WaterConsumedScenario(
                kind,
                fault,
                delivery));
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
            (kind, fault, delivery) => new BreakdownWaterConsumedScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.CharacterWaterConsumedEvent);
    }

    private static void VerifyTabooIncident(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
            (kind, fault, delivery) => new TabooIncidentScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent);
    }

    private static void VerifyObservedFuneral(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
            (kind, fault, delivery) => new ObservedLifeEventScenario(
                kind,
                ObservedLifeEventEntry.Funeral,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt);
    }

    private static void VerifyObservedLastLesson(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt,
            (kind, fault, delivery) => new ObservedLifeEventScenario(
                kind,
                ObservedLifeEventEntry.LastLesson,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt);
    }

    private static void VerifyLineageCompression(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind
                .ObservedLineageCompressionLifeEventReceipt,
            (kind, fault, delivery) => new LineageCompressionScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind
                .ObservedLineageCompressionLifeEventReceipt);
    }

    private static void VerifyObservedMealIncident(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult,
            (kind, fault, delivery) => new ObservedMealIncidentScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult);
    }

    private static void VerifyObservedQuietPromotion(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.ObservedQuietPromotionLifeEventReceipt,
            (kind, fault, delivery) => new ObservedLifeEventScenario(
                kind,
                ObservedLifeEventEntry.QuietPromotion,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.ObservedQuietPromotionLifeEventReceipt);
    }

    private static void VerifyOwnerRunEnded(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.OwnerRunEndedEvent,
            (kind, fault, delivery) => new OwnerRunEndedScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.OwnerRunEndedEvent);
    }

    private static void VerifyRestOutcome(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.RestOutcomeIdentityEvent,
            (kind, fault, delivery) => new RestOutcomeScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.RestOutcomeIdentityEvent);
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
            Require(
                before != after,
                kind + " production entry did not mutate its real owner.");
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
            string outcomeOwnerBefore = JsonUtility.ToJson(
                scenario.Outcomes.Transaction.Capture());
            scenario.ExecuteExpectingRejectedCommit();
            Require(
                scenario.Probe.ReservationCount(kind) > 0
                && scenario.Probe.CommitAttemptCount(kind) > 0
                && scenario.Probe.CommitCount(kind) == 0,
                kind + " rejection case did not reach the production outcome commit.");
            Require(
                domainBefore == scenario.CaptureDomain(),
                kind + " left domain mutation after a definite commit rejection.");
            Require(
                outcomeOwnerBefore == JsonUtility.ToJson(
                    scenario.Outcomes.Transaction.Capture()),
                kind + " advanced the outcome owner after a definite rejection.");
        }

        using (IEntryScenario scenario = create(
                   kind,
                   MigratedProducerOutcomeFaultMode.None,
                   true))
        {
            string before = scenario.CaptureDomain();
            scenario.Execute();
            string committed = scenario.CaptureDomain();
            Require(
                before != committed,
                kind + " durable pending commit did not mutate its owner.");
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

        public virtual void Dispose()
        {
        }
    }

    private enum ConsumablesEntry
    {
        Meal,
        Substance,
        Detox
    }

    private sealed class ConsumablesScenario : EntryScenarioBase
    {
        private static readonly CharacterId ActorId = new(
            "character:migrated-e2e:consumables");
        private static readonly BuildingInstanceId FacilityId = new(
            "building:migrated-e2e:medical");
        private static readonly ConsumableItemDefinitionId MealId = new(
            "food:migrated-e2e:meal");
        private static readonly ConsumableItemDefinitionId SubstanceId = new(
            "drug:migrated-e2e:medicine");
        private static readonly ConsumableItemDefinitionId DetoxId = new(
            "medicine:migrated-e2e:antidote");
        private static readonly ItemStackId MealStack = new(
            "stack:migrated-e2e:meal");
        private static readonly ItemStackId SubstanceStack = new(
            "stack:migrated-e2e:substance");
        private static readonly ItemStackId DetoxStack = new(
            "stack:migrated-e2e:detox");

        private readonly ConsumablesEntry entry;
        private readonly bool primitiveFieldMeal;
        private readonly ConsumablesPort port;
        private readonly CharacterConsumablesRuntime runtime;

        internal ConsumablesScenario(
            MigratedProducerOutcomeKind target,
            ConsumablesEntry entry,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            bool primitiveFieldMeal = false)
            : base("run:migrated-entry-consumables-", target, fault, deliveryFault)
        {
            this.entry = entry;
            this.primitiveFieldMeal = primitiveFieldMeal;
            port = new ConsumablesPort(
                ActorId,
                FacilityId,
                MealId,
                SubstanceId,
                DetoxId,
                MealStack,
                SubstanceStack,
                DetoxStack);
            runtime = new CharacterConsumablesRuntime(
                port,
                port,
                port,
                FixedClock.Instance,
                new RandomStreamProvider(104729),
                Outcomes.AggregateRootStore,
                DefaultCharacterNeedBalanceRuntime.Instance,
                outcomeTransactions: new CharacterConsumablesOutcomeTransaction(
                    new FixedGameSessionStateProvider(42),
                    Probe));
            runtime.SetSubstancePolicy(
                ActorId,
                "substance:migrated-e2e:medicine",
                SubstancePolicyMode.MedicalOnly,
                0f,
                0);
            if (entry == ConsumablesEntry.Detox)
            {
                DungeonCharacterConsumablesSaveData save = runtime.Capture();
                save.toxicityStates.Add(new CharacterToxicityState
                {
                    characterId = ActorId.Value,
                    toxicity = 60f,
                    lastNaturalRecoveryDay = 1
                });
                runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(save));
            }
        }

        public override string CaptureDomain()
        {
            DungeonCharacterConsumablesSaveData save = runtime.Capture();
            CharacterSubstanceState substance = save.substanceStates
                .FirstOrDefault(value => value != null);
            return string.Join("|", new[]
            {
                save.completedOperations.Count.ToString(CultureInfo.InvariantCulture),
                (substance?.tolerance ?? 0f).ToString("R", CultureInfo.InvariantCulture),
                runtime.GetToxicityStatus(ActorId).Toxicity.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                port.HungerRecovered.ToString("R", CultureInfo.InvariantCulture),
                port.MoodApplied.ToString("R", CultureInfo.InvariantCulture),
                port.DamageApplied.ToString("R", CultureInfo.InvariantCulture),
                port.AcknowledgementCount.ToString(CultureInfo.InvariantCulture)
            });
        }

        public override void Execute()
        {
            switch (entry)
            {
                case ConsumablesEntry.Meal:
                    if (primitiveFieldMeal)
                        runtime.TryConsumePrimitiveFieldMeal(
                            ActorId,
                            MealStack,
                            out _);
                    else
                        runtime.TryConsumeFieldMeal(ActorId, MealStack, out _);
                    return;
                case ConsumablesEntry.Substance:
                    runtime.TryConsumeSubstance(
                        new ConsumeSubstanceByIdCommand(
                            new ConsumableOperationId(
                                "consumable-operation:migrated-e2e:substance"),
                            ActorId,
                            SubstanceId,
                            SubstanceStack,
                            medicalContext: true,
                            combatContext: false),
                        out _);
                    return;
                case ConsumablesEntry.Detox:
                    runtime.TryApplyDetoxTreatment(
                        new ConsumableOperationId(
                            "consumable-operation:migrated-e2e:detox"),
                        ActorId,
                        FacilityId,
                        out _);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private sealed class LifeStageScenario : EntryScenarioBase
    {
        private const string CharacterIdValue =
            "character:migrated-e2e:life-stage";
        private readonly CharacterLifeRuntime life;
        private readonly GameEventBus events;
        private readonly CharacterLifeApplicationAdapter adapter;

        internal LifeStageScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-life-stage-", target, fault, deliveryFault)
        {
            life = new CharacterLifeRuntime(
                Outcomes.AggregateRootStore,
                CharacterLifeCatalog.Instance,
                new RandomStreamProvider(7103));
            SpeciesLifeHistoryDefinition history =
                CharacterLifeCatalog.Instance.History;
            life.Register(
                new CharacterId(CharacterIdValue),
                history.SpeciesId,
                chronologicalAgeDays: 100,
                biologicalAgeDayUnits: history.AdultAgeDayUnits - 4d,
                birthdayDayOfYear: 1);
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

    private sealed class ProficiencyAwardScenario : EntryScenarioBase
    {
        private readonly CharacterNarrativeRuntime narrative;
        private readonly CharacterId characterId = new(
            "character:migrated-e2e:proficiency");
        private readonly CharacterProficiencyId proficiencyId;

        internal ProficiencyAwardScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-proficiency-", target, fault, deliveryFault)
        {
            var content = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            var catalog = new CharacterNarrativeCatalog(content);
            narrative = new CharacterNarrativeRuntime(
                Outcomes.AggregateRootStore,
                catalog,
                content: content,
                migratedOutcomes: Probe);
            proficiencyId = BuiltInCharacterProficiencyIds.All[0];
            string speciesId = catalog.Cultures[0].defaultSpeciesId;
            IReadOnlyList<CharacterStartingProficiencyExperience> starts =
                CharacterStartingProficiencyRules.Create(7104);
            CharacterStartingProficiencyExperience awarded = starts.Single(
                value => string.Equals(
                    value.proficiencyId,
                    proficiencyId.Value,
                    StringComparison.Ordinal));
            awarded.experience = 99;
            narrative.Register(
                characterId,
                new CharacterSpeciesId(speciesId),
                Array.Empty<string>(),
                Array.Empty<string>(),
                starts);
        }

        public override string CaptureDomain()
        {
            Require(
                narrative.TryGetProficiency(
                    characterId,
                    proficiencyId,
                    24L,
                    out CharacterProficiencySnapshot snapshot),
                "The proficiency fixture lost its registered proficiency.");
            return snapshot.CurrentMilliExperience.ToString(
                CultureInfo.InvariantCulture);
        }

        public override void Execute() => narrative.AddDirectExperience(
            characterId,
            proficiencyId,
            1f,
            absoluteHour: 24L,
            applyLearningMultiplier: true);
    }

    private sealed class PrimitiveSurvivalScenario : EntryScenarioBase
    {
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;
        private readonly CharacterPrimitiveSurvivalRunner runner;
        private readonly WorldItemStackRuntime itemStacks;
        private readonly CharacterPrimitiveSurvivalActionKind actionKind;

        internal PrimitiveSurvivalScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            CharacterPrimitiveSurvivalActionKind actionKind =
                CharacterPrimitiveSurvivalActionKind.FloorRest)
            : base("run:migrated-entry-primitive-survival-", target, fault, deliveryFault)
        {
            this.actionKind = actionKind;
            var grid = new Grid(3, 1);
            for (int x = 0; x < grid.width; x++)
            {
                grid.SetAreaType(
                    new Vector2Int(x, 0),
                    GridCellAreaType.ExteriorPath);
            }
            var gridProvider = new FixedGridSystemProvider(grid);
            itemStacks =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    gridProvider,
                    out _,
                    out _,
                    out ItemQuantityReservationService quantityReservations,
                    out IReservedItemTransferService reservedTransfers);
            if (actionKind == CharacterPrimitiveSurvivalActionKind.BucketWash)
            {
                itemStacks.Restore(new DungeonPhysicalItemSaveData
                {
                    version = DungeonPhysicalItemSaveData.CurrentVersion,
                    stacks = new List<WorldItemStackSaveData>
                    {
                        new()
                        {
                            stackId = "stack:migrated-e2e:bucket-water",
                            itemId = PrimitiveSurvivalBalanceAuthority
                                .CleanWaterItemId,
                            quantity = 1,
                            state = WorldItemStackState.Loose,
                            gridX = 0,
                            gridY = 0
                        }
                    }
                });
            }
            var stateStore = new CharacterDeprivationStateStore(
                Outcomes.AggregateRootStore);
            var clock = new DrainingGameClock();
            runner = new CharacterPrimitiveSurvivalRunner(
                new CharacterBreakdownWorld(
                    gridProvider,
                    itemStacks,
                    EmptyWorldFilthQuery.Instance,
                    EmptyWorldWaterQuery.Instance,
                    new RoomLayoutCache(),
                    CharacterAiEditorTestDependencies.WorldRegistry),
                new CharacterEmergencyMovement(gridProvider, stateStore),
                clock,
                new GameEventBus(),
                new CharacterPrimitiveSurvivalDependencies(
                    EmptyFieldMealConsumption.Instance,
                    quantityReservations,
                    reservedTransfers,
                    EditorFixedGameCalendar.Instance,
                    Probe,
                    new DrainingCoroutineScheduler(clock)));

            GameObject actorObject = new("Migrated Primitive Survival Owner");
            actorObject.SetActive(false);
            actorObject.AddComponent<SpriteRenderer>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AIBrain>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Brain.ConstructCharacterAbility(gridProvider);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "초기 생존 검증 직원",
                "Orc");
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(
                "character:migrated-e2e:primitive:" + Guid.NewGuid().ToString("N"));
            actor.stats[CharacterCondition.SLEEP] = 20f;
            actor.stats[CharacterCondition.HYGIENE] = 60f;
            actor.stats[CharacterCondition.EXCRETION] = 15f;
        }

        public override string CaptureDomain() => string.Join("|", new[]
        {
            actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            actor.Stats.GetConditionValue(CharacterCondition.EXCRETION, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            JsonUtility.ToJson(itemStacks.Capture())
        });

        public override void Execute()
        {
            Require(
                runner.TryStart(
                    actor,
                    actionKind,
                    out string status),
                "Primitive survival TryStart rejected the branch fixture "
                + actionKind + ": "
                + status);
        }

        public override void Dispose()
        {
            if (actor != null)
                runner.ReleaseActor(CharacterPersistentIdentity.Require(actor));
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private sealed class MealMissedScenario : EntryScenarioBase
    {
        private readonly GameEventBus events = new();
        private readonly SurvivalFoodRuntime runtime;
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;

        internal MealMissedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-meal-missed-", target, fault, deliveryFault)
        {
            runtime = SurvivalDebugScenarios.CreateMealMissedOutcomeEntryFixture(
                events,
                Outcomes.AggregateRootStore,
                Probe);
            GameObject actorObject = new("Migrated Meal Missed Owner");
            actorObject.AddComponent<SpriteRenderer>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "결식 검증 사장",
                "Orc",
                CharacterRole.Owner);
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(
                "character:migrated-e2e:meal-missed");
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(actor);
            CharacterAiEditorTestDependencies.WorldRegistry
                .RegisterCharacterLifetime(actor);
            runtime.Initialize();
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(runtime.Capture());

        public override void Execute() =>
            runtime.OnTriggerEvent(new OperatingDayStartedEvent(2));

        public override void Dispose()
        {
            runtime.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(actor);
            CharacterAiEditorTestDependencies.WorldRegistry
                .UnregisterCharacterLifetime(actor);
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private sealed class WaterConsumedScenario : EntryScenarioBase
    {
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;
        private readonly MutableWorldWaterQuery water = new();
        private readonly CharacterWaterConsumptionCoordinator consumption;

        internal WaterConsumedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-water-", target, fault, deliveryFault)
        {
            var gridProvider = new EmptyGridSystemProvider();
            WorldItemStackRuntime itemStacks =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    gridProvider,
                    out _,
                    out _,
                    out _,
                    out IReservedItemTransferService reservedTransfers);
            consumption = new CharacterWaterConsumptionCoordinator(
                itemStacks,
                reservedTransfers,
                water,
                DefaultCharacterNeedBalanceRuntime.Instance,
                new GameEventBus(),
                EditorFixedGameCalendar.Instance,
                Probe);

            GameObject actorObject = new("Migrated Water Consumption Owner");
            actorObject.AddComponent<SpriteRenderer>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "식수 검증 직원",
                "Orc");
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(
                "character:migrated-e2e:water:" + Guid.NewGuid().ToString("N"));
            actor.stats[CharacterCondition.THIRST] = 15f;
        }

        public override string CaptureDomain() => string.Join("|", new[]
        {
            actor.Stats.GetConditionValue(CharacterCondition.THIRST, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            water.Remaining.ToString("R", CultureInfo.InvariantCulture)
        });

        public override void Execute()
        {
            Require(
                consumption.TryConsumeWorldSource(actor, water.SourceId),
                "The production water-consumption coordinator rejected clean water.");
        }

        public override void Dispose()
        {
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private sealed class BreakdownWaterConsumedScenario : EntryScenarioBase
    {
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;
        private readonly CharacterDeprivationStateStore stateStore;
        private readonly CharacterBreakdownActionRunner runner;
        private readonly DrainingCoroutineScheduler scheduler;
        private readonly MutableWorldWaterQuery water = new();
        private readonly CharacterActionIntentLease intentLease;

        internal BreakdownWaterConsumedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-breakdown-water-", target, fault, deliveryFault)
        {
            var grid = new Grid(1, 1);
            grid.SetAreaType(Vector2Int.zero, GridCellAreaType.ExteriorPath);
            var gridProvider = new FixedGridSystemProvider(grid);
            WorldItemStackRuntime itemStacks =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    gridProvider,
                    out _,
                    out _,
                    out ItemQuantityReservationService quantityReservations,
                    out _);
            stateStore = new CharacterDeprivationStateStore(
                Outcomes.AggregateRootStore);
            var diagnostics = new CharacterDeprivationDiagnostics();
            var clock = new DrainingGameClock();
            scheduler = new DrainingCoroutineScheduler(clock);
            var world = new CharacterBreakdownWorld(
                gridProvider,
                itemStacks,
                EmptyWorldFilthQuery.Instance,
                water,
                new RoomLayoutCache(),
                CharacterAiEditorTestDependencies.WorldRegistry);
            var safeDrinkPlanner = new CharacterSafeDrinkPlanner(
                gridProvider,
                itemStacks,
                water,
                quantityReservations,
                OpenDoorAccessQuery.Instance,
                SafeEnvironmentWorkPolicy.Instance,
                diagnostics);
            var consequences = new CharacterDeprivationConsequences(
                stateStore,
                CharacterAiEditorTestDependencies.WorldRegistry);
            runner = new CharacterBreakdownActionRunner(
                world,
                new CharacterBreakdownActionPolicyDependencies(
                    new RandomStreamProvider(104729).Get(
                        "migrated-entry-breakdown-water"),
                    DefaultCharacterNeedBalanceRuntime.Instance,
                    new ResourceItemDefinitionCatalog(
                        Resources.LoadAll<ItemDefinitionSO>(
                            ItemDefinitionSO.UnifiedResourcePath)),
                    CharacterAiEditorTestDependencies.BodyHealthRuntime,
                    CharacterAiEditorTestDependencies.NeutralPerformance),
                new CharacterBreakdownActionExecutionDependencies(
                    stateStore,
                    safeDrinkPlanner,
                    new CharacterEmergencyMovement(gridProvider, stateStore),
                    diagnostics,
                    consequences,
                    EmptyFieldMealConsumption.Instance,
                    new GameEventBus(),
                    EditorFixedGameCalendar.Instance,
                    Probe));

            GameObject actorObject = new("Migrated Breakdown Water Owner");
            actorObject.SetActive(false);
            actorObject.AddComponent<SpriteRenderer>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AIBrain>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Brain.ConstructCharacterAbility(gridProvider);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "갈증 붕괴 검증 직원",
                "Orc");
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(
                "character:migrated-e2e:breakdown-water:"
                + Guid.NewGuid().ToString("N"));
            actor.stats[CharacterCondition.THIRST] = 15f;
            actor.stats[CharacterCondition.HYGIENE] = 65f;
            CharacterDeprivationState state = stateStore.Ensure(actor);
            state.breakdownGeneration = 1;
            state.dispatchedBreakdownGeneration = 1;
            state.breakdown = new CharacterBreakdownState
            {
                active = true,
                cause = DeprivationKind.Thirst,
                kind = CharacterBreakdownKind.DesperateDrink,
                startedAt = 0f
            };
            Require(
                actor.Brain.TryBeginExternallyDrivenAction(
                    CharacterBreakdownActionRunner.IntentOwnerId,
                    CharacterActionIntentKind.Breakdown,
                    "결핍 붕괴",
                    "갈증 붕괴",
                    "검증",
                    out intentLease),
                "The breakdown-water fixture could not acquire its action intent.");
        }

        public override string CaptureDomain()
        {
            var deprivation = new DungeonDarkSurvivalSaveData
            {
                characters = stateStore.Capture()
            };
            return string.Join("|", new[]
            {
                actor.Stats.GetConditionValue(CharacterCondition.THIRST, 0f)
                    .ToString("R", CultureInfo.InvariantCulture),
                actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f)
                    .ToString("R", CultureInfo.InvariantCulture),
                water.Remaining.ToString("R", CultureInfo.InvariantCulture),
                JsonUtility.ToJson(deprivation)
            });
        }

        public override void Execute()
        {
            System.Reflection.MethodInfo method =
                typeof(CharacterBreakdownActionRunner).GetMethod(
                    "RunDesperateDrink",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "The production breakdown-water routine is missing.");
            IEnumerator routine = method.Invoke(
                    runner,
                    new object[] { actor, intentLease, true, false })
                as IEnumerator
                ?? throw new InvalidOperationException(
                    "The production breakdown-water routine was not executable.");
            scheduler.Start(actor, routine);
        }

        public override void Dispose()
        {
            runner.ReleaseActor(CharacterPersistentIdentity.Require(actor));
            actor.Brain?.EndExternallyDrivenAction(
                CharacterBreakdownActionRunner.IntentOwnerId,
                clearFailures: true);
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private sealed class TabooIncidentScenario : EntryScenarioBase
    {
        private readonly WorldItemStackRuntime itemStacks;
        private readonly WildlifeCarcassService carcasses;
        private readonly CharacterActor butcher;
        private readonly CharacterActor source;
        private readonly CharacterSO butcherData;
        private readonly CharacterSO sourceData;

        internal TabooIncidentScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-taboo-", target, fault, deliveryFault)
        {
            itemStacks = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture(
                    out WorldItemRepository repository,
                    out _);
            var spawner = new WorldItemSpawner(
                itemStacks.CatalogProvider,
                repository,
                EditorNullItemMarkerPresenter.Instance);
            var transformOutcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-taboo-physical-"
                + Guid.NewGuid().ToString("N"));
            var transforms = new PhysicalItemTransformService(
                repository,
                spawner,
                itemStacks.MassQuery,
                itemStacks.CatalogProvider,
                EditorNullItemMarkerPresenter.Instance,
                new FixedGameSessionStateProvider(73),
                transformOutcomes.Transaction);
            carcasses = new WildlifeCarcassService(
                itemStacks,
                transforms,
                EmptyWildlifeSpeciesCatalog.Instance,
                new GameEventBus());
            carcasses.ConstructOutcomeTransaction(
                EditorFixedGameCalendar.Instance,
                Probe);

            (source, sourceData) = CreateActor(
                "금기 원천",
                "character:migrated-e2e:taboo-source:"
                + Guid.NewGuid().ToString("N"));
            (butcher, butcherData) = CreateActor(
                "금기 도축자",
                "character:migrated-e2e:taboo-butcher:"
                + Guid.NewGuid().ToString("N"));
            butcher.stats[CharacterCondition.HYGIENE] = 75f;
            Require(
                itemStacks.SpawnHumanoidCorpse(
                    source,
                    new Vector2Int(4, 5),
                    "migrated-e2e",
                    out string stackId)
                && itemStacks.SetEmergencyButcheryAllowed(stackId, true),
                "The taboo fixture could not create an authorized humanoid corpse.");
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(itemStacks.Capture())
            + "|" + butcher.Stats.GetConditionValue(
                CharacterCondition.HYGIENE,
                0f).ToString("R", CultureInfo.InvariantCulture)
            + "|" + JsonUtility.ToJson(
                butcher.Progression.CapturePersistentState());

        public override void Execute()
        {
            Require(
                carcasses.TryButcherNextCarcass(
                    butcher.BuildingVisitor,
                    building: null,
                    out int produced,
                    out string message)
                && produced == 6,
                "The production humanoid-butchery entry failed: " + message);
        }

        public override void Dispose()
        {
            DestroyActor(source, sourceData);
            DestroyActor(butcher, butcherData);
        }

        private static (CharacterActor Actor, CharacterSO Data) CreateActor(
            string displayName,
            string persistentId)
        {
            GameObject actorObject = new(displayName);
            actorObject.AddComponent<SpriteRenderer>();
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    displayName,
                    "Orc");
            actor.Initialization(data);
            actor.Identity.SetPersistentId(persistentId);
            return (actor, data);
        }

        private static void DestroyActor(CharacterActor actor, CharacterSO data)
        {
            if (data != null)
                Object.DestroyImmediate(data);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private enum ObservedLifeEventEntry
    {
        Funeral,
        LastLesson,
        QuietPromotion
    }

    private sealed class ObservedLifeEventScenario : EntryScenarioBase
    {
        private readonly ObservedLifeEventEntry entry;
        private readonly V20CampaignRuntime campaign;
        private readonly ObservedLifeEventOutcomeCoordinator coordinator;
        private readonly ObservedFuneralLifeEventReceipt funeral;
        private readonly ObservedLastLessonLifeEventReceipt lastLesson;
        private readonly ObservedQuietPromotionLifeEventReceipt quietPromotion;

        internal ObservedLifeEventScenario(
            MigratedProducerOutcomeKind target,
            ObservedLifeEventEntry entry,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-observed-life-", target, fault,
                deliveryFault)
        {
            this.entry = entry;
            V20StoryContentCatalog catalog = new(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            campaign = new V20CampaignRuntime(
                Outcomes.AggregateRootStore,
                catalog);
            coordinator = new ObservedLifeEventOutcomeCoordinator(Probe);
            funeral = CreateFuneralReceipt();
            lastLesson = CreateLastLessonReceipt();
            quietPromotion = CreateQuietPromotionReceipt();
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(campaign.CaptureSociety());

        public override void Execute()
        {
            bool accepted;
            bool changed;
            string failureReason;
            switch (entry)
            {
                case ObservedLifeEventEntry.Funeral:
                    accepted = coordinator.TryRecordFuneral(
                        campaign,
                        funeral,
                        out changed,
                        out failureReason);
                    break;
                case ObservedLifeEventEntry.LastLesson:
                    accepted = coordinator.TryRecordLastLesson(
                        campaign,
                        lastLesson,
                        out changed,
                        out failureReason);
                    break;
                case ObservedLifeEventEntry.QuietPromotion:
                    accepted = coordinator.TryRecordQuietPromotion(
                        campaign,
                        quietPromotion,
                        out changed,
                        out failureReason);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Require(
                accepted && changed,
                "Observed life-event production entry failed: " + failureReason);
        }

        private static ObservedFuneralLifeEventReceipt CreateFuneralReceipt() =>
            new(
                "funeral-operation:migrated-e2e",
                new CharacterId("character:migrated-e2e:deceased"),
                "building:migrated-e2e:memorial",
                absoluteDay: 42,
                generation: 1,
                new[]
                {
                    new CharacterId("character:migrated-e2e:mourner")
                });

        private static ObservedLastLessonLifeEventReceipt
            CreateLastLessonReceipt()
        {
            CharacterId mentorId = new("character:migrated-e2e:mentor");
            CharacterId studentId = new("character:migrated-e2e:student");
            BuildingInstanceId academyId = new(
                "building:migrated-e2e:academy");
            CharacterProficiencyId proficiencyId = new(
                "proficiency:crafting");
            CareerMentorshipSnapshot mentorship = new(
                mentorId,
                studentId,
                academyId,
                proficiencyId,
                1,
                0,
                0f,
                0f);
            CharacterCareerSnapshot retirement = new(
                mentorId,
                retired: false,
                default,
                string.Empty,
                0,
                0f,
                RetirementScheduleStatus.Pending,
                "life-event:retirement-request",
                "one-more-season",
                40,
                70,
                0);
            CombatEquipmentInstance protectiveEquipment = new()
            {
                instanceId = "equipment:migrated-e2e:last-lesson:blast-coat",
                definitionId = "armor:powder-cuirass",
                ownerCharacterId = mentorId.Value,
                worldState = CombatEquipmentWorldState.Equipped
            };
            const string LedgerStackId =
                "stack:migrated-e2e:last-lesson:career-ledger";
            CareerMentorshipAwardCommitReceipt award =
                CareerMentorshipAwardCommitReceipt.Restore(
                    academyId,
                    LedgerStackId,
                    beforeContentRevision: 4L,
                    afterContentRevision: 5L,
                    "career-mentorship-award:" + LedgerStackId + ":5");
            return new ObservedLastLessonLifeEventReceipt(
                mentorship,
                retirement,
                protectiveEquipment,
                award,
                absoluteDay: 42,
                generation: 1);
        }

        private static ObservedQuietPromotionLifeEventReceipt
            CreateQuietPromotionReceipt()
        {
            const long AbsoluteHour = 41L * GameCalendarRules.HoursPerDay;
            CharacterProficiencyAwardCommitReceipt source = new(
                CharacterProficiencyAwardKind.DirectExperience,
                new CharacterId("character:migrated-e2e:promoted"),
                new CharacterProficiencyId("proficiency:crafting"),
                beforeCurrentMilliExperience: 0L,
                afterCurrentMilliExperience: 100_000L,
                beforeLifetimeMilliExperience: 0L,
                afterLifetimeMilliExperience: 100_000L,
                AbsoluteHour);
            return new ObservedQuietPromotionLifeEventReceipt(
                source,
                absoluteDay: 42,
                generation: 1);
        }
    }

    private sealed class LineageCompressionScenario : EntryScenarioBase
    {
        private readonly KinshipHouseholdRuntime social;
        private readonly V20CampaignRuntime campaign;

        internal LineageCompressionScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-lineage-", target, fault, deliveryFault)
        {
            V20StoryContentCatalog catalog = new(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            campaign = new V20CampaignRuntime(
                Outcomes.AggregateRootStore,
                catalog);
            social = new KinshipHouseholdRuntime(
                Outcomes.AggregateRootStore,
                campaign,
                new GameEventBus(),
                Probe);
            social.ArchiveDeath(
                new CharacterId("character:migrated-e2e:lineage"),
                new CharacterSpeciesId("species:human"),
                birthAbsoluteDay: 1,
                deathAbsoluteDay: 10,
                famous: false,
                new HouseholdId("household:migrated-e2e:lineage"),
                generation: 2);
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(social.Capture())
            + "|" + JsonUtility.ToJson(campaign.CaptureSociety())
            + "|" + JsonUtility.ToJson(campaign.CaptureMilestones());

        public override void Execute() => social.ArchiveColdData(
            currentAbsoluteDay: 200,
            Array.Empty<CharacterId>());
    }

    private sealed class ObservedMealIncidentScenario : EntryScenarioBase
    {
        private readonly V20CampaignRuntime campaign;
        private readonly ObservedMealIncidentSnapshot evidence;
        private readonly V20DailyEventContext context;

        internal ObservedMealIncidentScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-observed-meal-", target, fault, deliveryFault)
        {
            V20StoryContentCatalog catalog = new(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            campaign = new V20CampaignRuntime(
                Outcomes.AggregateRootStore,
                catalog,
                null,
                null,
                Probe);
            evidence = new ObservedMealIncidentSnapshot(
                new ConsumableOperationId(
                    "consumable-operation:migrated-e2e:observed-meal"),
                new CharacterId("character:migrated-e2e:customer"),
                new ItemDefinitionId("food:lavish-meat"),
                new ItemStackId("stack:migrated-e2e:observed-meal"),
                default,
                fieldMeal: true,
                new CoreGridCell(3, 4),
                absoluteDay: 42,
                observedPolicyViolation: false,
                observedDowned: false,
                observedDead: false,
                observedContaminated: true);
            context = new V20DailyEventContext
            {
                AbsoluteDay = 42,
                RunSeed = 157181,
                Season = Season.Spring
            };
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(campaign.CaptureSociety());

        public override void Execute() =>
            campaign.CaptureObservedMealIncident(evidence, context);
    }

    private sealed class OwnerRunEndedScenario : EntryScenarioBase
    {
        private readonly OwnerRunManager manager;

        internal OwnerRunEndedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-owner-run-", target, fault, deliveryFault)
        {
            CoreSessionRulesSO rulesAsset =
                AssetDatabase.LoadAssetAtPath<CoreSessionRulesSO>(
                    "Assets/Resources/SO/Content/CoreSessionRules.asset")
                ?? throw new InvalidOperationException(
                    "Authored core-session rules are missing.");
            manager = new GameObject("Migrated Owner Run Manager")
                .AddComponent<OwnerRunManager>();
            CharacterAiEditorTestDependencies
                .InjectOwnerRunManagerOutcomeForDiagnostics(
                    manager,
                    Probe,
                    new FixedRunSeedProvider(104729),
                    Outcomes.AggregateRootStore,
                    new FixedCoreSessionRulesProvider(
                        rulesAsset.CreateRuntimeDefinition()));
            CharacterSO owner = manager.OwnerCandidates.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored owner candidate is available.");
            manager.SelectOwner(owner, "결말 검증 사장");
        }

        public override string CaptureDomain() => manager.IsRunEnded.ToString();

        public override void Execute() => manager.CompleteRun(
            DungeonRunOutcome.Victory,
            "migrated-producer-e2e");

        public override void Dispose()
        {
            if (manager?.CurrentOwnerActor != null)
                Object.DestroyImmediate(manager.CurrentOwnerActor.gameObject);
            if (manager != null)
                Object.DestroyImmediate(manager.gameObject);
        }
    }

    private sealed class RestOutcomeScenario : EntryScenarioBase
    {
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;

        internal RestOutcomeScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-rest-", target, fault, deliveryFault)
        {
            GameObject actorObject = new("Migrated Rest Owner");
            actorObject.AddComponent<SpriteRenderer>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "휴식 검증 직원",
                "Orc");
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId("character:migrated-e2e:rest");
            CharacterAiEditorTestDependencies
                .InjectCharacterStatsOutcomeForDiagnostics(actor.Stats, Probe);
            actor.stats[CharacterCondition.SLEEP] = 20f;
        }

        public override string CaptureDomain() => actor.Stats
            .GetConditionValue(CharacterCondition.SLEEP, 0f)
            .ToString("R", CultureInfo.InvariantCulture);

        public override void Execute() => actor.Stats.RecoverNeed(
            CharacterCondition.SLEEP,
            10f,
            CharacterNeedRecoverySource.Rest,
            Array.Empty<string>());

        public override void Dispose()
        {
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private sealed class ConsumablesPort :
        ICharacterConsumablesWorldPort,
        ICharacterConsumablesInventoryPort,
        ICharacterConsumablesEventPort
    {
        private readonly CharacterId actorId;
        private readonly BuildingInstanceId facilityId;
        private readonly CharacterConsumablesMealDefinitionSnapshot meal;
        private readonly CharacterConsumablesSubstanceDefinitionSnapshot substance;
        private readonly CharacterDetoxMedicineDefinitionSnapshot detox;
        private readonly List<CharacterConsumablesStackSnapshot> stacks;
        private readonly Dictionary<string, string> pendingCommitByOperation = new(
            StringComparer.Ordinal);

        internal ConsumablesPort(
            CharacterId actorId,
            BuildingInstanceId facilityId,
            ConsumableItemDefinitionId mealId,
            ConsumableItemDefinitionId substanceId,
            ConsumableItemDefinitionId detoxId,
            ItemStackId mealStack,
            ItemStackId substanceStack,
            ItemStackId detoxStack)
        {
            this.actorId = actorId;
            this.facilityId = facilityId;
            meal = new CharacterConsumablesMealDefinitionSnapshot(
                mealId,
                "검증 식사",
                MealDietClass.Vegan,
                MealQualityTier.Simple,
                nutrition: 30f,
                mood: 3f,
                unitPrice: 1,
                forbiddenIngredient: false,
                sweet: false,
                salted: false);
            substance = new CharacterConsumablesSubstanceDefinitionSnapshot(
                substanceId,
                new SubstanceDefinitionView(
                    "substance:migrated-e2e:medicine",
                    substanceId.Value,
                    "검증 약물",
                    SubstanceUseClass.Medicine,
                    addictionChance: 0f,
                    overdoseChance: 0f,
                    toleranceGain: 1f,
                    withdrawalPerHour: 0f,
                    moodEffect: 1f,
                    workSpeedEffect: 0f,
                    combatEffect: 0f,
                    fatigueAccumulationReduction: 0f,
                    researchSpeedEffect: 0f,
                    suppressesPerceivedPain: false,
                    durationSeconds: 60f,
                    requiredResearchId: string.Empty));
            detox = new CharacterDetoxMedicineDefinitionSnapshot(
                detoxId,
                "검증 해독제",
                detoxReduction: 30f);
            stacks = new List<CharacterConsumablesStackSnapshot>
            {
                new(
                    mealStack,
                    mealId,
                    1,
                    CharacterConsumablesStackState.Loose,
                    string.Empty,
                    forbidden: false,
                    reservedQuantity: 0,
                    contamination: 0f,
                    freshness01: 1f,
                    remainingFreshnessSeconds: 100f,
                    preserved: false),
                new(
                    substanceStack,
                    substanceId,
                    1,
                    CharacterConsumablesStackState.Loose,
                    string.Empty,
                    forbidden: false,
                    reservedQuantity: 0,
                    contamination: 0f,
                    freshness01: 1f,
                    remainingFreshnessSeconds: 100f,
                    preserved: false),
                new(
                    detoxStack,
                    detoxId,
                    1,
                    CharacterConsumablesStackState.FacilityBuffer,
                    CharacterConsumablesInputDestinationIdentity.Build(
                        CharacterConsumablesInputKind.MedicalTreatment,
                        facilityId,
                        detoxId),
                    forbidden: false,
                    reservedQuantity: 0,
                    contamination: 0f,
                    freshness01: 1f,
                    remainingFreshnessSeconds: 100f,
                    preserved: false)
            };
        }

        internal float HungerRecovered { get; private set; }
        internal float MoodApplied { get; private set; }
        internal float DamageApplied { get; private set; }
        internal int AcknowledgementCount { get; private set; }

        public IReadOnlyList<CharacterId> CharacterIds => new[] { actorId };
        public IReadOnlyList<BuildingInstanceId> FacilityIds =>
            new[] { facilityId };

        public bool TryGetActor(
            CharacterId id,
            out CharacterConsumablesActorSnapshot actor)
        {
            actor = id.Equals(actorId)
                ? new CharacterConsumablesActorSnapshot(
                    actorId,
                    active: true,
                    health: 100f,
                    maxHealth: 100f,
                    mood: 50f,
                    hunger: 0f,
                    combatStance: false)
                : default;
            return actor.Id.IsValid;
        }

        public bool TryGetFacility(
            BuildingInstanceId id,
            out CharacterConsumablesFacilitySnapshot facility)
        {
            facility = id.Equals(facilityId)
                ? new CharacterConsumablesFacilitySnapshot(
                    facilityId,
                    mealFacility: false,
                    recreationalSubstanceFacility: false,
                    Vector2Int.zero)
                : default;
            return facility.Id.IsValid;
        }

        public CharacterCultureMealPreference GetCultureMealPreference(
            CharacterId characterId,
            ConsumableItemDefinitionId itemId) =>
            CharacterCultureMealPreference.Neutral;

        public float ProjectGameplayEffect(
            CharacterId characterId,
            string targetId,
            float baseValue) => baseValue;

        public float GetBehaviorUtilityMultiplier(
            CharacterId characterId,
            IReadOnlyCollection<string> semanticTags) => 1f;

        public void RecoverHunger(CharacterId id, float amount) =>
            HungerRecovered += amount;

        public void ApplyMood(
            CharacterId id,
            string sourceId,
            string label,
            float value,
            float durationSeconds) => MoodApplied += value;

        public void ApplyDamage(CharacterId id, float amount, string reason) =>
            DamageApplied += amount;

        public void RecordNeedNarrative(
            CharacterId id,
            string factId,
            string subjectId,
            string outcome,
            float value)
        {
        }

        public IReadOnlyList<CharacterConsumablesStackSnapshot> GetAllStacks() =>
            stacks;

        public IReadOnlyList<CharacterConsumablesSubstanceDefinitionSnapshot>
            GetSubstances() => new[] { substance };

        public IReadOnlyList<CharacterDetoxMedicineDefinitionSnapshot>
            GetDetoxMedicines() => new[] { detox };

        public bool TryGetMeal(
            ConsumableItemDefinitionId id,
            out CharacterConsumablesMealDefinitionSnapshot value)
        {
            value = id.Equals(meal.Id) ? meal : default;
            return value.Id.IsValid;
        }

        public bool TryResolveSubstance(
            string substanceOrItemId,
            out CharacterConsumablesSubstanceDefinitionSnapshot value)
        {
            value = string.Equals(
                    substanceOrItemId,
                    substance.Id.Value,
                    StringComparison.Ordinal)
                || string.Equals(
                    substanceOrItemId,
                    substance.Definition.SubstanceId,
                    StringComparison.Ordinal)
                    ? substance
                    : default;
            return value.Id.IsValid;
        }

        public bool TryResolveSubstance(
            ConsumableItemDefinitionId id,
            out CharacterConsumablesSubstanceDefinitionSnapshot value)
        {
            value = id.Equals(substance.Id) ? substance : default;
            return value.Id.IsValid;
        }

        public bool TryResolveDetoxMedicine(
            ConsumableItemDefinitionId id,
            out CharacterDetoxMedicineDefinitionSnapshot medicine)
        {
            medicine = id.Equals(detox.Id) ? detox : default;
            return medicine.Id.IsValid;
        }

        public bool TryConsume(ItemStackId stackId, int quantity) => true;

        public bool TryReserveMealQuantity(
            ConsumableOperationId operationId,
            CharacterId characterId,
            BuildingInstanceId targetFacilityId,
            ItemStackId stackId,
            out string leaseId)
        {
            leaseId = "lease:" + operationId.Value;
            return true;
        }

        public bool RevalidateMealQuantity(string leaseId, ItemStackId stackId) =>
            !string.IsNullOrWhiteSpace(leaseId);

        public bool TryCommitReservedMealQuantityPending(
            ConsumableOperationId operationId,
            string leaseId,
            int quantity,
            out CharacterMealPhysicalCommitSnapshot commit,
            out string failureReason)
        {
            string commitId = "commit:" + operationId.Value;
            pendingCommitByOperation[operationId.Value] = commitId;
            commit = new CharacterMealPhysicalCommitSnapshot(
                operationId.Value,
                CharacterConsumablesRuntime.MealPhysicalSinkReason,
                commitId,
                new[] { stacks[0].StackId.Value },
                quantity,
                inputMassGrams: 100L);
            failureReason = string.Empty;
            return true;
        }

        public bool TryAcknowledgeMealConsumption(
            string commitId,
            out string failureReason) => Acknowledge(commitId, out failureReason);

        public bool TryCommitSubstanceConsumptionPending(
            ConsumableOperationId operationId,
            CharacterId characterId,
            ItemStackId stackId,
            out CharacterSubstancePhysicalCommitSnapshot commit,
            out string failureReason)
        {
            string commitId = "commit:" + operationId.Value;
            pendingCommitByOperation[operationId.Value] = commitId;
            commit = new CharacterSubstancePhysicalCommitSnapshot(
                operationId.Value,
                CharacterConsumablesRuntime.SubstancePhysicalSinkReason,
                commitId,
                new[] { stackId.Value },
                quantity: 1,
                inputMassGrams: 10L);
            failureReason = string.Empty;
            return true;
        }

        public bool TryAcknowledgeSubstanceConsumption(
            string commitId,
            out string failureReason) => Acknowledge(commitId, out failureReason);

        public bool TryCommitDetoxConsumptionPending(
            ConsumableOperationId operationId,
            ItemStackId stackId,
            out CharacterDetoxPhysicalCommitSnapshot commit,
            out string failureReason)
        {
            string commitId = "commit:" + operationId.Value;
            pendingCommitByOperation[operationId.Value] = commitId;
            commit = new CharacterDetoxPhysicalCommitSnapshot(
                operationId.Value,
                CharacterConsumablesRuntime.DetoxPhysicalSinkReason,
                commitId,
                new[] { stackId.Value },
                quantity: 1,
                inputMassGrams: 10L);
            failureReason = string.Empty;
            return true;
        }

        public bool TryAcknowledgeDetoxConsumption(
            CharacterId characterId,
            ConsumableItemDefinitionId itemId,
            int quantity,
            string commitId,
            out string failureReason) => Acknowledge(commitId, out failureReason);

        public bool TryRequestDelivery(
            ConsumableItemDefinitionId itemId,
            int quantity,
            Vector2Int position,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = 0;
            failureReason = "not-needed";
            return false;
        }

        public void Publish(CharacterConsumablesMealConsumedEvent consumedEvent)
        {
        }

        private bool Acknowledge(string commitId, out string failureReason)
        {
            string key = pendingCommitByOperation.FirstOrDefault(pair =>
                string.Equals(pair.Value, commitId, StringComparison.Ordinal)).Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                failureReason = "pending-commit-missing";
                return false;
            }
            pendingCommitByOperation.Remove(key);
            AcknowledgementCount++;
            failureReason = string.Empty;
            return true;
        }
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

        public SpeciesLifeHistoryDefinition RequireLifeHistory(
            CharacterSpeciesId speciesId) => speciesId.Equals(History.SpeciesId)
                ? History
                : throw new KeyNotFoundException(speciesId.Value);

        public AgeConditionDefinition RequireAgeCondition(string conditionId) =>
            throw new KeyNotFoundException(conditionId);

        public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(
            bool construct) => Array.Empty<AgeConditionDefinition>();
    }

    private sealed class NeutralHeritableTraitEffects : IHeritableTraitEffectQuery
    {
        internal static readonly NeutralHeritableTraitEffects Instance = new();

        public float GetMultiplier(
            CharacterId characterId,
            HeritableTraitConsequenceKind kind,
            string targetId) => 1f;
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

    private sealed class EmptyGridSystemProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
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

    private sealed class OpenDoorAccessQuery : IDoorAccessQuery
    {
        internal static readonly OpenDoorAccessQuery Instance = new();
        public int DoorAccessVersion => 0;
        public DoorAccessSubjectRef ResolveSubject(
            GridTraversalContext context) => default;
        public bool CanUse(
            Door door,
            GridTraversalContext context,
            out string denialReason)
        {
            denialReason = string.Empty;
            return true;
        }
        public bool CanTraverse(
            Grid grid,
            Vector2Int position,
            GridTraversalContext context,
            out string denialReason)
        {
            denialReason = string.Empty;
            return true;
        }
    }

    private sealed class SafeEnvironmentWorkPolicy : IEnvironmentWorkPolicy
    {
        internal static readonly SafeEnvironmentWorkPolicy Instance = new();
        private static WorkEnvironmentAssessment Safe => new(
            canStart: true,
            needsProtection: false,
            projectedExposure: 0f,
            workSpeedMultiplier: 1f,
            failure: DomainFailure.None);
        public WorkEnvironmentAssessment Assess(
            CharacterActor actor,
            Vector2Int destination,
            float expectedSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;
        public WorkEnvironmentAssessment AssessStart(
            CharacterActor actor,
            Vector2Int destination,
            IReadOnlyList<GridMoveStep> route,
            float expectedSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;
        public WorkEnvironmentAssessment PrepareActiveWork(
            CharacterActor actor,
            Vector2Int destination,
            IReadOnlyList<GridMoveStep> route,
            float expectedSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;
        public WorkEnvironmentAssessment RecheckActive(
            CharacterActor actor,
            Vector2Int currentPosition,
            float remainingSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;
        public bool TryFindEvacuationCell(
            CharacterActor actor,
            Grid grid,
            out Vector2Int destination,
            out bool fullySafe,
            out DomainFailure failure)
        {
            destination = actor != null ? actor.GetNowXY() : Vector2Int.zero;
            fullySafe = true;
            failure = DomainFailure.None;
            return true;
        }
    }

    private sealed class EmptyFieldMealConsumption : IFieldMealConsumptionCommand
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

    private sealed class EmptyWildlifeSpeciesCatalog :
        IWildlifeSpeciesCatalogProvider
    {
        internal static readonly EmptyWildlifeSpeciesCatalog Instance = new();
        public IReadOnlyList<WildlifeSpeciesDefinition> All =>
            Array.Empty<WildlifeSpeciesDefinition>();
        public bool TryGetSpecies(
            string speciesId,
            out WildlifeSpeciesDefinition species)
        {
            species = null;
            return false;
        }
        public WildlifeSpeciesDefinition GetRandomSpecies(IRandomStream randomStream) =>
            throw new InvalidOperationException(
                "The humanoid-corpse fixture does not select wildlife species.");
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
                "filth:migrated-e2e:" + type,
                type,
                amount,
                position,
                sourceCharacterId,
                infectionRisk,
                wallStain);
        public bool Clean(string filthId, float workAmount, out float remainingAmount)
        {
            remainingAmount = 0f;
            return false;
        }
        public float GetCleanlinessPenalty(Vector2Int position, int radius = 0) => 0f;
        public List<WorldFilthSaveData> CaptureFilth() => new();
        public void RestoreFilth(
            IEnumerable<WorldFilthSaveData> saveData,
            int nextSequence)
        {
        }
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

    private sealed class MutableWorldWaterQuery : IWorldWaterQuery
    {
        private WorldWaterSourceSaveData state = new()
        {
            sourceId = "water:migrated-e2e:clean",
            gridX = 0,
            gridY = 0,
            terrainType = GridCellTerrainType.ShallowWater,
            quality = WorldWaterQuality.Clean,
            capacity = 10f,
            remaining = 10f,
            regenerationPerSecond = 0f,
            pathogenDiseaseId = string.Empty
        };

        internal string SourceId => state.sourceId;
        internal float Remaining => state.remaining;
        public int NextWaterSequence { get; private set; } = 2;

        public IReadOnlyList<WorldWaterSourceSnapshot> GetAllSources() =>
            new[] { Snapshot() };

        public bool TryGetSource(
            string sourceId,
            out WorldWaterSourceSnapshot source)
        {
            source = Snapshot();
            return string.Equals(state.sourceId, sourceId, StringComparison.Ordinal);
        }

        public bool TryFindDrinkSource(
            Vector2Int origin,
            bool allowFoul,
            out WorldWaterSourceSnapshot source)
        {
            source = Snapshot();
            return source.CanDrink;
        }

        public bool TryDrink(
            string sourceId,
            float amount,
            out WorldWaterQuality quality,
            out float consumed)
        {
            quality = state.quality;
            consumed = 0f;
            if (!string.Equals(state.sourceId, sourceId, StringComparison.Ordinal))
                return false;
            consumed = Mathf.Min(Mathf.Max(0f, amount), state.remaining);
            state.remaining -= consumed;
            return consumed > 0f;
        }

        public List<WorldWaterSourceSaveData> CaptureWaterSources() => new()
        {
            Clone(state)
        };

        public void RestoreWaterSources(
            IEnumerable<WorldWaterSourceSaveData> saveData,
            int nextSequence)
        {
            state = Clone(saveData?.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Water rollback snapshot was empty."));
            NextWaterSequence = nextSequence;
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

        private WorldWaterSourceSnapshot Snapshot() => new(
            state.sourceId,
            new Vector2Int(state.gridX, state.gridY),
            state.terrainType,
            state.quality,
            state.capacity,
            state.remaining,
            state.regenerationPerSecond,
            state.pathogenDiseaseId);

        private static WorldWaterSourceSaveData Clone(
            WorldWaterSourceSaveData source) => new()
        {
            sourceId = source.sourceId,
            gridX = source.gridX,
            gridY = source.gridY,
            terrainType = source.terrainType,
            quality = source.quality,
            capacity = source.capacity,
            remaining = source.remaining,
            regenerationPerSecond = source.regenerationPerSecond,
            pathogenDiseaseId = source.pathogenDiseaseId
        };
    }

    private sealed class FixedClock : IGameClock
    {
        internal static readonly FixedClock Instance = new();
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FixedRunSeedProvider : IRunSeedProvider
    {
        internal FixedRunSeedProvider(int runSeed) => RunSeed = runSeed;
        public int RunSeed { get; }
    }

    private sealed class FixedCoreSessionRulesProvider : ICoreSessionRulesProvider
    {
        internal FixedCoreSessionRulesProvider(CoreSessionRulesDefinition rules) =>
            CoreSessionRules = rules
                ?? throw new ArgumentNullException(nameof(rules));
        public CoreSessionRulesDefinition CoreSessionRules { get; }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
#endif
