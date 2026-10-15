#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

internal sealed class SocialRitualFactEditorFixture : IDisposable
{
    private readonly List<IDisposable> subscriptions = new();

    internal SocialRitualFactEditorFixture(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe = null)
        : this(
            outcomes?.Transaction
                ?? throw new ArgumentNullException(nameof(outcomes)),
            new GameplayDomainFactCommitGroup(
                outcomes.Recorder,
                outcomes.Ledger),
            probe)
    {
    }

    internal SocialRitualFactEditorFixture(
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits,
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        commits = commits ?? throw new ArgumentNullException(nameof(commits));
        probe ??= new MigratedProducerOutcomeProbeTransaction(runtime);
        Dispatcher = new DomainFactDispatcher();
        try
        {
            Subscribe<ObservedFuneralLifeEventReceipt>(
                "fixture:social-ritual:funeral",
                SocialRitualFactMapping.Capture,
                runtime,
                commits,
                probe);
            Subscribe<ObservedLineageCompressionLifeEventReceipt>(
                "fixture:social-ritual:lineage",
                SocialRitualFactMapping.Capture,
                runtime,
                commits,
                probe);
            Subscribe<ObservedProductionLossLifeEventReceipt>(
                "fixture:social-ritual:production-loss",
                SocialRitualFactMapping.Capture,
                runtime,
                commits,
                probe);
            Subscribe<ObservedLastLessonLifeEventReceipt>(
                "fixture:social-ritual:last-lesson",
                SocialRitualFactMapping.Capture,
                runtime,
                commits,
                probe);
            Subscribe<ObservedQuietPromotionLifeEventReceipt>(
                "fixture:social-ritual:quiet-promotion",
                SocialRitualFactMapping.Capture,
                runtime,
                commits,
                probe);
            Subscribe<FestivalResolutionFact>(
                "fixture:social-ritual:festival-resolution",
                SocialRitualFactMapping.Capture,
                runtime,
                commits,
                probe);
            Subscribe<ContentResolutionFact>(
                "fixture:social-ritual:content-resolution",
                ContentResolutionFactMapping.Capture,
                runtime,
                commits,
                probe);
            Subscribe<ObservedServiceIncidentFact>(
                "fixture:social-ritual:observed-service-incident",
                ObservedServiceIncidentFactMapping.Capture,
                runtime,
                commits,
                probe);
            Subscribe<CareerMentorshipAwardFact>(
                "fixture:social-ritual:career-mentorship-award",
                CareerMentorshipAwardFactMapping.Capture,
                runtime,
                commits,
                probe);
            Journal = new SocialRitualFactJournal(Dispatcher, Dispatcher);
        }
        catch
        {
            DisposeSubscriptions();
            Dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Dispatcher { get; }
    internal SocialRitualFactJournal Journal { get; }

    private void Subscribe<TFact>(
        string observerId,
        Func<TFact, MigratedDomainFactCapture> capture,
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        subscriptions.Add(Dispatcher.Subscribe(
            observerId,
            new MigratedProducerDomainFactProbeObserver<TFact>(
                capture,
                runtime,
                commits,
                probe)));
    }

    public void Dispose()
    {
        try
        {
            Journal?.AssertIdle();
        }
        finally
        {
            DisposeSubscriptions();
            Dispatcher.Dispose();
        }
    }

    private void DisposeSubscriptions()
    {
        for (int index = subscriptions.Count - 1; index >= 0; index--)
            subscriptions[index]?.Dispose();
        subscriptions.Clear();
    }
}
#endif
