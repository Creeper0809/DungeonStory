#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

internal sealed class CharacterSkillActivationEditorFixture : IDisposable
{
    private readonly List<IDisposable> bindings = new();

    internal CharacterSkillActivationEditorFixture(
        string runId = null,
        ICharacterSkillActivationEffectExecutor effects = null,
        IGameCalendar calendar = null,
        bool includeBattleCommand = false,
        IDurableDomainFactObserver<CharacterSkillActivationFact>
            activationObserver = null)
    {
        string canonicalRunId = string.IsNullOrWhiteSpace(runId)
            ? "run:skill-activation-editor:"
                + Guid.NewGuid().ToString("N")
            : runId.Trim();
        var descriptors = new List<IGameplayOutcomeDescriptor>
        {
            new CharacterSkillActivationOutcomeDescriptor()
        };
        var registrations = new List<IGameplayOutcomeAdapterRegistration>
        {
            new CharacterSkillActivationOutcomeAdapter()
        };
        var catalogs = new List<IGameplayOutcomeDescriptorCatalog>();
        if (includeBattleCommand)
        {
            registrations.Add(new MigratedProducerOutcomeAdapter());
            catalogs.Add(new MigratedProducerOutcomeDescriptorCatalog(
                new KoreanJosaFormatter()));
        }
        Registry = new GameplayOutcomeRegistry(
            descriptors,
            registrations,
            catalogs);
        Ledger = new GameplayOutcomeLedger(
            Registry,
            GameplayOutcomeBufferLimits.Default,
            new GameplayOutcomeRunId(canonicalRunId),
            1L);
        Recorder = new GameplayOutcomeRecorder(
            Ledger,
            Registry,
            new GameEventBus());
        CommitGroup = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        Dispatcher = new DomainFactDispatcher();
        bindings.Add(activationObserver == null
            ? CharacterSkillActivationFactMapping.CreateBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    CommitGroup)
                .Connect(Dispatcher)
            : Dispatcher.Subscribe(
                "test:character-skill-activation",
                activationObserver));
        Publications = new CharacterSkillActivationPublicationRuntime(
            Dispatcher,
            Dispatcher);
        if (includeBattleCommand)
        {
            AggregateRoots = new DungeonRuntimeAggregateRootStore();
            Migrated = new MigratedProducerOutcomeRuntime(
                AggregateRoots,
                Ledger,
                Recorder,
                Ledger);
            bindings.Add(new OffenseBattleCommandFactObserver(
                    Migrated,
                    CommitGroup)
                .Connect(Dispatcher));
            CommandPublications = new OffenseBattleCommandPublicationRuntime(
                Dispatcher,
                Dispatcher);
        }
        Transaction = new CharacterSkillActivationTransaction(
            effects ?? new CharacterSkillActivationEffectExecutor(),
            calendar ?? CharacterAiEditorTestDependencies.GameCalendar,
            Dispatcher,
            Publications);
    }

    internal GameplayOutcomeRegistry Registry { get; }
    internal GameplayOutcomeLedger Ledger { get; }
    internal GameplayOutcomeRecorder Recorder { get; }
    internal GameplayDomainFactCommitGroup CommitGroup { get; }
    internal DomainFactDispatcher Dispatcher { get; }
    internal DungeonRuntimeAggregateRootStore AggregateRoots { get; }
    internal MigratedProducerOutcomeRuntime Migrated { get; }
    internal OffenseBattleCommandPublicationRuntime CommandPublications { get; }
    internal CharacterSkillActivationPublicationRuntime Publications { get; }
    internal CharacterSkillActivationTransaction Transaction { get; }

    public void Dispose()
    {
        for (int index = bindings.Count - 1; index >= 0; index--)
            bindings[index].Dispose();
        Dispatcher.Dispose();
    }
}
#endif
