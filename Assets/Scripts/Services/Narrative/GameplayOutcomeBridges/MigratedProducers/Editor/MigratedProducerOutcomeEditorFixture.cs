#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

internal sealed class MigratedProducerOutcomeEditorFixture
{
    internal MigratedProducerOutcomeEditorFixture(
        string runId,
          DungeonRuntimeAggregateRootStore aggregateRootStore = null,
          MigratedProducerOutcomeDeliveryFault deliveryFault = null,
          bool includeAcquiredTraitInference = false,
          bool includeAcquiredTraitReaction = false,
          bool includeTradeInventory = false,
          bool includeCharacterSkillActivation = false,
          bool includeResearchWork = false,
          bool includeCombatDamage = false,
          bool includeRitualFasting = false,
          bool includeProductionCompletion = false)
    {
        string canonicalRunId = runId?.Trim() ?? string.Empty;
        if (canonicalRunId.Length == 0)
            throw new ArgumentException("A fixture run ID is required.", nameof(runId));

        var adapter = new MigratedProducerOutcomeAdapter();
        IGameplayOutcomeDescriptorCatalog descriptorCatalog = deliveryFault == null
            ? new MigratedProducerOutcomeDescriptorCatalog(
                new KoreanJosaFormatter())
            : new FaultingMigratedProducerOutcomeDescriptorCatalog(
                new KoreanJosaFormatter(),
                deliveryFault);
          var descriptors = new List<IGameplayOutcomeDescriptor>();
          var registrations = new List<IGameplayOutcomeAdapterRegistration>
          {
              adapter
          };
          if (includeCombatDamage)
          {
              descriptors.Add(new CombatDamageOutcomeDescriptor());
              registrations.Add(new CombatDamageOutcomeAdapter());
          }
          if (includeRitualFasting)
          {
              descriptors.Add(new CharacterRitualFastingOutcomeDescriptor());
              registrations.Add(new CharacterRitualFastingOutcomeAdapter());
          }
          if (includeProductionCompletion)
          {
              descriptors.Add(new ProductionCompletedOutcomeDescriptor());
              registrations.Add(new ProductionCompletedOutcomeAdapter());
          }
          if (includeAcquiredTraitInference)
          {
              descriptors.Add(new AcquiredTraitInferenceOutcomeDescriptor());
              registrations.Add(new AcquiredTraitInferenceOutcomeAdapter());
          }
          if (includeAcquiredTraitReaction)
          {
              descriptors.Add(new AcquiredTraitReactionOutcomeDescriptor());
              registrations.Add(new AcquiredTraitReactionOutcomeAdapter());
          }
          if (includeTradeInventory)
          {
              descriptors.Add(new TradeInventoryOutcomeDescriptor(
                  new KoreanJosaFormatter()));
              registrations.Add(new TradeInventoryOutcomeAdapter());
          }
          if (includeCharacterSkillActivation)
          {
              descriptors.Add(new CharacterSkillActivationOutcomeDescriptor());
              registrations.Add(new CharacterSkillActivationOutcomeAdapter());
          }
          if (includeResearchWork)
          {
              descriptors.Add(new ResearchWorkOutcomeDescriptor());
              registrations.Add(new ResearchWorkOutcomeAdapter());
          }
          Registry = new GameplayOutcomeRegistry(
              descriptors,
              registrations,
              new IGameplayOutcomeDescriptorCatalog[] { descriptorCatalog });
        var limits = new GameplayOutcomeBufferLimits(
            smallPageCount: 128,
            largePageCount: 8,
            knownResultKeyCapacity: 512,
            retainedSmallPageCount: 128,
            retainedLargePageCount: 8);
        Ledger = new GameplayOutcomeLedger(
            Registry,
            limits,
            new GameplayOutcomeRunId(canonicalRunId),
            1L);
        Recorder = new GameplayOutcomeRecorder(
            Ledger,
            Registry,
            new GameEventBus());
        AggregateRootStore = aggregateRootStore
            ?? new DungeonRuntimeAggregateRootStore();
        Transaction = new MigratedProducerOutcomeRuntime(
            AggregateRootStore,
            Ledger,
            Recorder,
            Ledger);
    }

    internal DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
    internal GameplayOutcomeRegistry Registry { get; }
    internal GameplayOutcomeLedger Ledger { get; }
    internal GameplayOutcomeRecorder Recorder { get; }
    internal MigratedProducerOutcomeRuntime Transaction { get; }

    internal DomainFactDispatcher CreateProficiencyAwardFacts(
        MigratedProducerOutcomeProbeTransaction probe)
    {
        if (probe == null)
            throw new ArgumentNullException(nameof(probe));

        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        dispatcher.Subscribe(
            "test:migrated:character-proficiency-award",
            new MigratedProducerDomainFactProbeObserver<
                CharacterProficiencyAwardCommitReceipt>(
                ProficiencyAwardFactMapping.Capture,
                Transaction,
                commits,
                probe));
        return dispatcher;
    }

    internal DomainFactDispatcher CreateCharacterRestFacts(
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        try
        {
            if (probe == null)
            {
                dispatcher.Subscribe(
                    "test:migrated:character-rest-applied",
                    new MigratedDomainFactObserver<CharacterRestAppliedFact>(
                        CharacterRestFactMapping.Capture,
                        Transaction,
                        commits));
            }
            else
            {
                dispatcher.Subscribe(
                    "test:migrated:character-rest-applied",
                    new MigratedProducerDomainFactProbeObserver<
                        CharacterRestAppliedFact>(
                            CharacterRestFactMapping.Capture,
                            Transaction,
                            commits,
                            probe));
            }
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher CreateRunVariableFacts(
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        try
        {
            if (probe == null)
            {
                dispatcher.Subscribe(
                    "test:migrated:run-variable",
                    new MigratedDomainFactObserver<RunVariableFact>(
                        RunVariableFactMapping.Capture,
                        Transaction,
                        commits));
            }
            else
            {
                dispatcher.Subscribe(
                    "test:migrated:run-variable",
                    new MigratedProducerDomainFactProbeObserver<RunVariableFact>(
                        RunVariableFactMapping.Capture,
                        Transaction,
                        commits,
                        probe));
            }
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher CreateSeasonalTerminalFacts(
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        try
        {
            if (probe == null)
            {
                dispatcher.Subscribe(
                    SeasonalTerminalFactMapping.WildlifeArrivalObserverId,
                    new MigratedDomainFactObserver<
                        SeasonalWildlifeArrivalFact>(
                            SeasonalTerminalFactMapping.Capture,
                            Transaction,
                            commits));
                dispatcher.Subscribe(
                    SeasonalTerminalFactMapping.DriftCargoObserverId,
                    new MigratedDomainFactObserver<SeasonalDriftCargoFact>(
                        SeasonalTerminalFactMapping.Capture,
                        Transaction,
                        commits));
            }
            else
            {
                dispatcher.Subscribe(
                    SeasonalTerminalFactMapping.WildlifeArrivalObserverId,
                    new MigratedProducerDomainFactProbeObserver<
                        SeasonalWildlifeArrivalFact>(
                            SeasonalTerminalFactMapping.Capture,
                            Transaction,
                            commits,
                            probe));
                dispatcher.Subscribe(
                    SeasonalTerminalFactMapping.DriftCargoObserverId,
                    new MigratedProducerDomainFactProbeObserver<
                        SeasonalDriftCargoFact>(
                            SeasonalTerminalFactMapping.Capture,
                            Transaction,
                            commits,
                            probe));
            }
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher CreateMealMissedFacts(
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        try
        {
            if (probe == null)
            {
                dispatcher.Subscribe(
                    "test:migrated:survival-meal-missed",
                    new MigratedDomainFactObserver<SurvivalMealMissedFact>(
                        SurvivalMealMissedFactMapping.Capture,
                        Transaction,
                        commits));
            }
            else
            {
                dispatcher.Subscribe(
                    "test:migrated:survival-meal-missed",
                    new MigratedProducerDomainFactProbeObserver<
                        SurvivalMealMissedFact>(
                            SurvivalMealMissedFactMapping.Capture,
                            Transaction,
                            commits,
                            probe));
            }
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher CreatePrimitiveSurvivalFacts(MigratedProducerOutcomeProbeTransaction probe = null)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        try
        {
            dispatcher.Subscribe("test:migrated:primitive-rest-child",
                new MigratedDomainFactObserver<CharacterRestAppliedFact>(CharacterRestFactMapping.Capture, Transaction, commits));
            if (probe == null)
                dispatcher.Subscribe("test:migrated:primitive-survival-completed",
                    new MigratedDomainFactObserver<PrimitiveSurvivalCompletedFact>(
                        PrimitiveSurvivalCompletedFactMapping.Capture, Transaction, commits));
            else
                dispatcher.Subscribe("test:migrated:primitive-survival-completed",
                    new MigratedProducerDomainFactProbeObserver<PrimitiveSurvivalCompletedFact>(
                        PrimitiveSurvivalCompletedFactMapping.Capture, Transaction, commits, probe));
            return dispatcher;
        }
        catch { dispatcher.Dispose(); throw; }
    }

    internal DomainFactDispatcher CreateApparelWorkFacts(MigratedProducerOutcomeProbeTransaction probe = null)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        try
        {
            if (probe == null)
                dispatcher.Subscribe("test:migrated:apparel-work-completed",
                    new MigratedDomainFactObserver<ApparelWorkCompletedFact>(
                        ApparelWorkCompletedFactMapping.Capture, Transaction, commits));
            else
                dispatcher.Subscribe("test:migrated:apparel-work-completed",
                    new MigratedProducerDomainFactProbeObserver<ApparelWorkCompletedFact>(
                        ApparelWorkCompletedFactMapping.Capture, Transaction, commits, probe));
            return dispatcher;
        }
        catch { dispatcher.Dispose(); throw; }
    }

    internal DomainFactDispatcher CreatePhysicalTransformationFacts(
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        try
        {
            if (probe == null)
            {
                dispatcher.Subscribe(
                    "test:migrated:physical-item-transformed",
                    new MigratedDomainFactObserver<PhysicalItemTransformedFact>(
                        PhysicalItemTransformedFactMapping.Capture,
                        Transaction,
                        commits));
                dispatcher.Subscribe(
                    "test:migrated:wildlife-carcass-processed",
                    new MigratedDomainFactObserver<WildlifeCarcassProcessedFact>(
                        WildlifeCarcassProcessedFactMapping.Capture,
                        Transaction,
                        commits));
            }
            else
            {
                dispatcher.Subscribe(
                    "test:migrated:physical-item-transformed",
                    new MigratedProducerDomainFactProbeObserver<
                        PhysicalItemTransformedFact>(
                            PhysicalItemTransformedFactMapping.Capture,
                            Transaction,
                            commits,
                            probe));
                dispatcher.Subscribe(
                    "test:migrated:wildlife-carcass-processed",
                    new MigratedProducerDomainFactProbeObserver<
                        WildlifeCarcassProcessedFact>(
                            WildlifeCarcassProcessedFactMapping.Capture,
                            Transaction,
                            commits,
                            probe));
            }
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher CreateConstructionWorkFacts(
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        try
        {
            if (probe == null)
            {
                dispatcher.Subscribe(
                    "test:migrated:construction-work-ended",
                    new MigratedDomainFactObserver<ConstructionWorkEndedFact>(
                        ConstructionWorkEndedFactMapping.Capture,
                        Transaction,
                        commits));
            }
            else
            {
                dispatcher.Subscribe(
                    "test:migrated:construction-work-ended",
                    new MigratedProducerDomainFactProbeObserver<
                        ConstructionWorkEndedFact>(
                            ConstructionWorkEndedFactMapping.Capture,
                            Transaction,
                            commits,
                            probe));
            }
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher CreateRepairWorkFacts(
        IWorkCompletionIdentityDeliveryCommand deliveries,
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        if (deliveries == null)
            throw new ArgumentNullException(nameof(deliveries));

        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        try
        {
            if (probe == null)
            {
                dispatcher.Subscribe(
                    "test:migrated:repair-work-completed",
                    new MigratedDomainFactObserver<RepairWorkCompletedFact>(
                        RepairWorkCompletedFactMapping.Capture,
                        Transaction,
                        commits));
            }
            else
            {
                dispatcher.Subscribe(
                    "test:migrated:repair-work-completed",
                    new MigratedProducerDomainFactProbeObserver<
                        RepairWorkCompletedFact>(
                            RepairWorkCompletedFactMapping.Capture,
                            Transaction,
                            commits,
                            probe));
            }
            dispatcher.Subscribe(
                "test:repair-work-completion",
                new WorkCompletionDomainFactObserver<RepairWorkCompletionFact>(
                    RepairWorkCompletionObservation.CreateCompletionRequest,
                    deliveries,
                    new WorkCompletionIdentityDeliveryLedger(),
                    new CharacterIdentityEventPublisher(new GameEventBus()),
                    WorkCompletionNotificationMode.ApplyOnly,
                    new WorkIdentityAppliedFactMapping(Ledger, Ledger)));
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }
}

internal sealed class FixedGameSessionStateProvider : IGameSessionStateProvider
{
    private readonly GameSessionState state;

    internal FixedGameSessionStateProvider(int absoluteDay = 1)
    {
        state = new GameSessionState(
            startingMoney: 0,
            startingDay: Math.Max(0, absoluteDay));
    }

    public bool TryGetSessionState(out GameSessionState gameData)
    {
        gameData = state;
        return true;
    }
}
#endif
