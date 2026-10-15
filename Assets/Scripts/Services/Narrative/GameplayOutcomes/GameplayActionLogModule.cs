using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

/// <summary>
/// One action's complete logging capability. The adapter consumes the owner's committed
/// receipt; the descriptor validates/presents it; the observer interprets its experience.
/// Modules never own gameplay state or a second durable log.
/// </summary>
public interface IGameplayActionLogModule
{
    GameplayOutcomeTypeId OutcomeTypeId { get; }
    IGameplayOutcomeAdapterRegistration Adapter { get; }
    IGameplayOutcomeDescriptor Descriptor { get; }
    IGameplayExperienceObserver ExperienceObserver { get; }
}

public interface IGameplayActionLogRegistry
{
    IReadOnlyList<IGameplayActionLogModule> ActionModules { get; }
    GameplayExperienceObserverRegistry ExperienceObservers { get; }
}

public class GameplayActionLogModule : IGameplayActionLogModule
{
    public GameplayActionLogModule(IGameplayOutcomeAdapterRegistration adapter,
        IGameplayOutcomeDescriptor descriptor, IGameplayExperienceObserver observer)
    {
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        ExperienceObserver = observer ?? throw new ArgumentNullException(nameof(observer));
        if (!OutcomeTypeId.IsValid || adapter.ReceiptType == null
            || observer.OutcomeTypes == null || observer.OutcomeTypes.Count != 1
            || observer.OutcomeTypes[0] != OutcomeTypeId.Value
            || (adapter is IGameplayOutcomeDynamicAdapterRegistration dynamicAdapter
                ? !dynamicAdapter.SupportsOutcomeType(OutcomeTypeId) : adapter.OutcomeTypeId != OutcomeTypeId))
            throw new ArgumentException("An action module must bind the same receipt, descriptor and experience type.");
    }
    public GameplayOutcomeTypeId OutcomeTypeId => Descriptor.OutcomeTypeId;
    public IGameplayOutcomeAdapterRegistration Adapter { get; }
    public IGameplayOutcomeDescriptor Descriptor { get; }
    public IGameplayExperienceObserver ExperienceObserver { get; }
}

/// <summary>Distinct singleton implementation identity per action in the DI collection.</summary>
public sealed class GameplayActionLogModule<TPolicy> : GameplayActionLogModule
    where TPolicy : IGameplayExperienceObserver
{
    public GameplayActionLogModule(IGameplayOutcomeAdapterRegistration adapter,
        IGameplayOutcomeDescriptor descriptor, TPolicy observer)
        : base(adapter, descriptor, observer) { }
}

/// <summary>Authored composition, not reflection discovery or an outcome-name switch.</summary>
public sealed class GameplayActionLogDefinition
{
    private readonly Action<IContainerBuilder> register;
    private GameplayActionLogDefinition(string group, IGameplayExperienceObserver observer,
        Action<IContainerBuilder> register)
    { Group = group; ExperienceObserver = observer; this.register = register; }

    public string Group { get; }
    public IGameplayExperienceObserver ExperienceObserver { get; }
    public void Register(IContainerBuilder builder) => register(builder);

    public static GameplayActionLogDefinition Typed<TAdapter, TDescriptor, TPolicy>(string group)
        where TAdapter : class, IGameplayOutcomeAdapterRegistration
        where TDescriptor : class, IGameplayOutcomeDescriptor
        where TPolicy : IGameplayExperienceObserver, new()
    {
        var policy = new TPolicy();
        return new GameplayActionLogDefinition(group, policy, builder =>
        {
            builder.Register<TAdapter>(Lifetime.Singleton).AsSelf();
            builder.Register<TDescriptor>(Lifetime.Singleton).AsSelf();
            builder.Register(resolver => new GameplayActionLogModule<TPolicy>(resolver.Resolve<TAdapter>(),
                resolver.Resolve<TDescriptor>(), policy), Lifetime.Singleton).As<IGameplayActionLogModule>();
        });
    }

    public static GameplayActionLogDefinition Migrated<TPolicy>(MigratedProducerOutcomeKind kind)
        where TPolicy : IGameplayExperienceObserver, new()
    {
        var policy = new TPolicy();
        var definition = MigratedProducerOutcomeCatalog.Definitions.Single(value => value.Kind == kind);
        if (policy.OutcomeTypes.Count != 1 || policy.OutcomeTypes[0] != definition.OutcomeTypeId.Value)
            throw new ArgumentException("Migrated action policy/producer binding differs.");
        return new GameplayActionLogDefinition("migrated", policy, builder =>
            builder.Register(resolver => new GameplayActionLogModule<TPolicy>(
                resolver.Resolve<MigratedProducerOutcomeAdapter>(),
                new MigratedProducerOutcomeDescriptor(definition,
                    resolver.Resolve<DungeonStory.Narrative.Korean.IKoreanJosaFormatter>()), policy),
                Lifetime.Singleton).As<IGameplayActionLogModule>());
    }
}

public static class GameplayActionLogRegistration
{
    public static void RegisterGameplayActionLogs(this IContainerBuilder builder, string group)
    {
        var definitions = GameplayActionLogCatalog.Definitions.Where(value => value.Group == group).ToArray();
        if (definitions.Length == 0) throw new ArgumentException("No action log group: " + group);
        foreach (var definition in definitions) definition.Register(builder);
    }
}
