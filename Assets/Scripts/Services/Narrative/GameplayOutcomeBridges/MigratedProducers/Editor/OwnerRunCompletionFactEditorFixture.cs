#if UNITY_EDITOR
using System;
using DungeonStory.Foundation.Facts;

internal sealed class OwnerRunCompletionFactEditorFixture : IDisposable
{
    private readonly IDisposable subscription;

    internal OwnerRunCompletionFactEditorFixture(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
        : this(
            outcomes?.Transaction
                ?? throw new ArgumentNullException(nameof(outcomes)),
            new GameplayDomainFactCommitGroup(
                outcomes.Recorder,
                outcomes.Ledger),
            probe)
    {
    }

    internal OwnerRunCompletionFactEditorFixture(
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        Dispatcher = new DomainFactDispatcher();
        try
        {
            subscription = Dispatcher.Subscribe(
                OwnerRunCompletionFactMapping.ObserverId,
                new MigratedProducerDomainFactProbeObserver<
                    OwnerRunCompletionFact>(
                    OwnerRunCompletionFactMapping.Capture,
                    runtime ?? throw new ArgumentNullException(
                        nameof(runtime)),
                    commits ?? throw new ArgumentNullException(
                        nameof(commits)),
                    probe ?? throw new ArgumentNullException(
                        nameof(probe))));
            Publisher = Dispatcher;
            Recovery = Dispatcher;
        }
        catch
        {
            Dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Dispatcher { get; }
    internal IDomainFactPublisher Publisher { get; }
    internal IDomainFactRecoveryPublisher Recovery { get; }

    public void Dispose()
    {
        subscription?.Dispose();
        Dispatcher.Dispose();
    }
}

// These focused fixtures intentionally have no meta-progression settlement.
// They still require the real durable owner-completion fact path; this
// participant only models the absent meta domain in that controlled setup.
internal sealed class EmptyRunCompletionPreparation :
    IRunCompletionPreparation
{
    internal static readonly EmptyRunCompletionPreparation Instance = new();

    private EmptyRunCompletionPreparation()
    {
    }

    public IRunCompletionPreparedState Prepare(
        OwnerRunCompletionFactData fact) => PreparedState.Instance;

    private sealed class PreparedState : IRunCompletionPreparedState
    {
        internal static readonly PreparedState Instance = new();

        public void Publish()
        {
        }

        public void Cancel()
        {
        }
    }
}
#endif
