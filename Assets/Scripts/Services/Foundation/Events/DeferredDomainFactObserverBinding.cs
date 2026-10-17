using System;
using System.Collections.Generic;

namespace DungeonStory.Foundation.Facts
{
    // Reaction owners can themselves publish facts. Their observers connect only
    // after the dispatcher is cached by DI, before the gameplay container is handed out.
    public abstract class DeferredDomainFactObserverBinding : IDomainFactObserverBinding
    {
        protected DeferredDomainFactObserverBinding(Type factType) =>
            FactTypes = Array.AsReadOnly(new[] { factType ?? throw new ArgumentNullException(nameof(factType)) });
        public IReadOnlyCollection<Type> FactTypes { get; }
        public abstract IDisposable Connect(DomainFactDispatcher dispatcher);
    }

    public sealed class DeferredDomainFactObserverBinding<TFact> : DeferredDomainFactObserverBinding
    {
        private readonly Func<DomainFactDispatcher, IDisposable> connect;
        public DeferredDomainFactObserverBinding(Func<DomainFactDispatcher, IDisposable> connect)
            : base(typeof(TFact)) => this.connect = connect ?? throw new ArgumentNullException(nameof(connect));
        public override IDisposable Connect(DomainFactDispatcher dispatcher) => connect(dispatcher);
    }
}
