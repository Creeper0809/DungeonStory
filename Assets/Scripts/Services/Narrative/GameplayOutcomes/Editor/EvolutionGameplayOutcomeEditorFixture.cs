#if UNITY_EDITOR
using DungeonStory.Foundation;

internal sealed class EvolutionGameplayOutcomeEditorFixture
{
    internal EvolutionGameplayOutcomeEditorFixture(
        string runId,
        float clockTime = 0f)
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new ApparelPhysicalOutcomeDescriptor(),
                new ProductQualityOutcomeDescriptor(),
                new AcquiredTraitInferenceOutcomeDescriptor(),
                new MemoryErasureBossAwardOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new ApparelPhysicalOutcomeAdapter(),
                new ProductQualityOutcomeAdapter(),
                new AcquiredTraitInferenceOutcomeAdapter(),
                new MemoryErasureBossAwardOutcomeAdapter()
            });
        Ledger = new GameplayOutcomeLedger(
            registry,
            new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId(runId),
            1L);
        GameplayOutcomeRecorder recorder = new(
            Ledger,
            registry,
            new GameEventBus());
        Bridge = new EvolutionGameplayOutcomeBridge(recorder, Ledger, Ledger);
        Clock = new FixedGameClock(clockTime);
    }

    internal GameplayOutcomeLedger Ledger { get; }
    internal EvolutionGameplayOutcomeBridge Bridge { get; }
    internal IGameClock Clock { get; }

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
