using System;
using DungeonStory.Foundation.Facts;
using VContainer;

public static class GameplayDomainFactRegistration
{
    public static void RegisterDeferredDomainFactBinding<TFact>(this IContainerBuilder builder,
        Func<IObjectResolver, DomainFactDispatcher, IDisposable> connect)
    {
        if (connect == null) throw new ArgumentNullException(nameof(connect));
        builder.Register(resolver => new DeferredDomainFactObserverBinding<TFact>(
            dispatcher => connect(resolver, dispatcher)), Lifetime.Singleton)
            .As<IDomainFactObserverBinding>();
        builder.RegisterBuildCallback(resolver =>
            resolver.Resolve<DomainFactDispatcher>().ConnectDeferredBindings());
    }

    public static void RegisterGameplayDomainFact<TFact, TReceipt>(this IContainerBuilder builder,
        string observerId, Func<TFact, TReceipt> capture)
    {
        builder.Register(resolver => new GameplayDomainFactObserverBinding<TFact, TReceipt>(
            observerId, capture, resolver.Resolve<IGameplayOutcomeRegistry>(),
            resolver.Resolve<GameplayOutcomeLedger>(), resolver.Resolve<IGameplayOutcomeRecorder>(),
            resolver.Resolve<GameplayDomainFactCommitGroup>()), Lifetime.Singleton)
            .As<IDomainFactObserverBinding>();
    }
}
