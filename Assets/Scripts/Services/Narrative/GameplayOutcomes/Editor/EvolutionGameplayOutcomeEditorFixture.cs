#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

internal sealed class EvolutionGameplayOutcomeEditorFixture : IDisposable
{
    private readonly IDisposable facilityEvolutionBinding;
    private readonly IDisposable traitInferenceBinding;
    private readonly IDisposable traitReactionBinding;
    private readonly IDisposable memoryErasureBinding;
    private readonly IDisposable bossAwardBinding;
    private readonly IDisposable moduleSelectionBinding;
    private readonly IDisposable apparelChangeBinding;
    private readonly IDisposable apparelPhysicalBinding;

    internal EvolutionGameplayOutcomeEditorFixture(
        string runId,
        float clockTime = 0f,
        bool includeMigratedProducers = false,
        bool includeFacilityVandalism = false,
        bool includeTradeInventory = false,
        DungeonRuntimeAggregateRootStore aggregateRootStore = null,
        IGameCalendar calendar = null,
        bool includeFacilityEvolutionFacts = false)
    {
        var descriptors = new List<IGameplayOutcomeDescriptor>
        {
                new ApparelChangeOutcomeDescriptor(),
                new ApparelPhysicalOutcomeDescriptor(),
                new ProductQualityOutcomeDescriptor(),
                new EquipmentModuleSelectionOutcomeDescriptor(),
                new CharacterAmbitionTransitionOutcomeDescriptor(),
                new HeritableTraitAnalysisOutcomeDescriptor(),
                new AcquiredTraitInferenceOutcomeDescriptor(),
                new AcquiredTraitReactionOutcomeDescriptor(),
                new FacilityEvolutionOutcomeDescriptor(),
                new MemoryErasureOutcomeDescriptor(),
                new MemoryErasureBossAwardOutcomeDescriptor()
        };
        var registrations = new List<IGameplayOutcomeAdapterRegistration>
        {
                new ApparelChangeOutcomeAdapter(),
                new ApparelPhysicalOutcomeAdapter(),
                new ProductQualityOutcomeAdapter(),
                new EquipmentModuleSelectionOutcomeAdapter(),
                new CharacterAmbitionTransitionOutcomeAdapter(),
                new HeritableTraitAnalysisOutcomeAdapter(),
                new AcquiredTraitInferenceOutcomeAdapter(),
                new AcquiredTraitReactionOutcomeAdapter(),
                new FacilityEvolutionOutcomeAdapter(),
                new MemoryErasureOutcomeAdapter(),
                new MemoryErasureBossAwardOutcomeAdapter()
        };
        var catalogs = new List<IGameplayOutcomeDescriptorCatalog>();
        if (includeMigratedProducers)
        {
            registrations.Add(new MigratedProducerOutcomeAdapter());
            catalogs.Add(new MigratedProducerOutcomeDescriptorCatalog(
                new KoreanJosaFormatter()));
        }
        if (includeFacilityVandalism)
        {
            descriptors.Add(new FacilityVandalismOutcomeDescriptor(
                new KoreanJosaFormatter()));
            registrations.Add(new FacilityVandalismOutcomeAdapter());
        }
        if (includeTradeInventory)
        {
            descriptors.Add(new TradeInventoryOutcomeDescriptor(
                new KoreanJosaFormatter()));
            registrations.Add(new TradeInventoryOutcomeAdapter());
        }
        Registry = new GameplayOutcomeRegistry(
            descriptors,
            registrations,
            catalogs);
        Ledger = new GameplayOutcomeLedger(
            Registry,
            new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId(runId),
            1L,
            calendar);
        Recorder = new GameplayOutcomeRecorder(
            Ledger,
            Registry,
            new GameEventBus());
        Bridge = new EvolutionGameplayOutcomeBridge(Recorder, Ledger, Ledger);
        TraitInferenceFacts = new DomainFactDispatcher();
        traitInferenceBinding = CharacterTraitInferredFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                new GameplayDomainFactCommitGroup(Recorder, Ledger))
            .Connect(TraitInferenceFacts);
        TraitInferences = new CharacterTraitInferencePublicationRuntime(
            TraitInferenceFacts,
            TraitInferenceFacts);
        TraitReactionFacts = new DomainFactDispatcher();
        traitReactionBinding = CharacterTraitReactionFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                new GameplayDomainFactCommitGroup(Recorder, Ledger))
            .Connect(TraitReactionFacts);
        TraitReactions = new CharacterTraitReactionPublicationRuntime(
            TraitReactionFacts,
            TraitReactionFacts);
        MemoryErasureFacts = new DomainFactDispatcher();
        memoryErasureBinding = MemoryErasureTerminalFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                new GameplayDomainFactCommitGroup(Recorder, Ledger))
            .Connect(MemoryErasureFacts);
        MemoryErasures = new MemoryErasurePublicationRuntime(
            MemoryErasureFacts,
            MemoryErasureFacts);
        BossAwardFacts = new DomainFactDispatcher();
        bossAwardBinding = MemoryErasureBossAwardFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                new GameplayDomainFactCommitGroup(Recorder, Ledger))
            .Connect(BossAwardFacts);
        BossAwards = new MemoryErasureBossAwardPublicationRuntime(
            BossAwardFacts,
            BossAwardFacts);
        ModuleSelectionFacts = new DomainFactDispatcher();
        moduleSelectionBinding = EquipmentModuleSelectedFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                new GameplayDomainFactCommitGroup(Recorder, Ledger))
            .Connect(ModuleSelectionFacts);
        ModuleSelections = new EquipmentModuleSelectionPublicationRuntime(
            ModuleSelectionFacts,
            ModuleSelectionFacts);
        ApparelChangeFacts = new DomainFactDispatcher();
        apparelChangeBinding = ApparelLoadoutChangedFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                new GameplayDomainFactCommitGroup(Recorder, Ledger))
            .Connect(ApparelChangeFacts);
        ApparelChanges = new ApparelChangePublicationRuntime(
            ApparelChangeFacts,
            ApparelChangeFacts);
        ApparelPhysicalFacts = new DomainFactDispatcher();
        apparelPhysicalBinding = ApparelPhysicalCompletedFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                new GameplayDomainFactCommitGroup(Recorder, Ledger))
            .Connect(ApparelPhysicalFacts);
        ApparelPhysicalPublications = new ApparelPhysicalPublicationRuntime(
            ApparelPhysicalFacts,
            ApparelPhysicalFacts);
        if (includeFacilityEvolutionFacts)
        {
            FacilityEvolutionFacts = new DomainFactDispatcher();
            facilityEvolutionBinding =
                FacilityEvolutionCompletedFactMapping.CreateBinding(
                        Registry,
                        Ledger,
                        Recorder,
                        new GameplayDomainFactCommitGroup(Recorder, Ledger))
                    .Connect(FacilityEvolutionFacts);
        }
        if (includeMigratedProducers)
        {
            MigratedProducerRuntime = new MigratedProducerOutcomeRuntime(
                aggregateRootStore ?? new DungeonRuntimeAggregateRootStore(),
                Ledger,
                Recorder,
                Ledger);
        }
        Clock = new FixedGameClock(clockTime);
        Calendar = calendar ?? new EditorFixedGameCalendar();
    }

    internal GameplayOutcomeRegistry Registry { get; }
    internal GameplayOutcomeLedger Ledger { get; }
    internal GameplayOutcomeRecorder Recorder { get; }
    internal EvolutionGameplayOutcomeBridge Bridge { get; }
    internal DomainFactDispatcher TraitInferenceFacts { get; }
    internal CharacterTraitInferencePublicationRuntime TraitInferences { get; }
    internal DomainFactDispatcher TraitReactionFacts { get; }
    internal CharacterTraitReactionPublicationRuntime TraitReactions { get; }
    internal DomainFactDispatcher MemoryErasureFacts { get; }
    internal MemoryErasurePublicationRuntime MemoryErasures { get; }
    internal DomainFactDispatcher BossAwardFacts { get; }
    internal MemoryErasureBossAwardPublicationRuntime BossAwards { get; }
    internal DomainFactDispatcher ModuleSelectionFacts { get; }
    internal EquipmentModuleSelectionPublicationRuntime ModuleSelections { get; }
    internal DomainFactDispatcher ApparelChangeFacts { get; }
    internal ApparelChangePublicationRuntime ApparelChanges { get; }
    internal DomainFactDispatcher ApparelPhysicalFacts { get; }
    internal ApparelPhysicalPublicationRuntime ApparelPhysicalPublications { get; }
    internal DomainFactDispatcher FacilityEvolutionFacts { get; }
    internal MigratedProducerOutcomeRuntime MigratedProducerRuntime { get; }
    internal IGameClock Clock { get; }
    internal IGameCalendar Calendar { get; }

    public void Dispose()
    {
        traitInferenceBinding.Dispose();
        TraitInferenceFacts.Dispose();
        traitReactionBinding.Dispose();
        TraitReactionFacts.Dispose();
        memoryErasureBinding.Dispose();
        MemoryErasureFacts.Dispose();
        bossAwardBinding.Dispose();
        BossAwardFacts.Dispose();
        moduleSelectionBinding.Dispose();
        ModuleSelectionFacts.Dispose();
        apparelChangeBinding.Dispose();
        ApparelChangeFacts.Dispose();
        apparelPhysicalBinding.Dispose();
        ApparelPhysicalFacts.Dispose();
        facilityEvolutionBinding?.Dispose();
        FacilityEvolutionFacts?.Dispose();
    }

    private sealed class FixedGameClock : IGameClock
    {
        internal FixedGameClock(float time)
        {
            Time = time;
        }

        public float DeltaTime => 0f;
        public float Time { get; }
        public int FrameCount => 0;
        public bool IsPaused => false;
    }
}
#endif
