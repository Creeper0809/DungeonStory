using System;
using System.Collections.Generic;

namespace DungeonStory.Foundation
{
    /// <summary>
    /// Owns an application adapter's bound target and event subscriptions.
    /// This is transient wiring state: it is never a gameplay or save authority.
    /// </summary>
    public sealed class EventSubscriptionLifetime<TTarget>
        where TTarget : class
    {
        private readonly string multipleBindingMessage;
        private readonly string unboundMessage;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private TTarget target;

        public EventSubscriptionLifetime(
            string multipleBindingMessage,
            string unboundMessage)
        {
            this.multipleBindingMessage = RequireMessage(
                multipleBindingMessage,
                nameof(multipleBindingMessage));
            this.unboundMessage = RequireMessage(unboundMessage, nameof(unboundMessage));
        }

        public bool BeginBinding(TTarget nextTarget)
        {
            if (nextTarget == null)
            {
                throw new ArgumentNullException(nameof(nextTarget));
            }

            if (ReferenceEquals(target, nextTarget) && subscriptions.Count > 0)
            {
                return false;
            }

            if (target != null && !ReferenceEquals(target, nextTarget))
            {
                throw new InvalidOperationException(multipleBindingMessage);
            }

            target = nextTarget;
            return true;
        }

        public void Add(IDisposable subscription)
        {
            subscriptions.Add(subscription);
        }

        public void Unbind(TTarget expectedTarget)
        {
            if (!ReferenceEquals(target, expectedTarget))
            {
                return;
            }

            foreach (IDisposable subscription in subscriptions)
            {
                subscription?.Dispose();
            }

            subscriptions.Clear();
            target = null;
        }

        public TTarget RequireTarget()
        {
            return target ?? throw new InvalidOperationException(unboundMessage);
        }

        private static string RequireMessage(string message, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("A lifetime failure message is required.", parameterName);
            }

            return message;
        }
    }
}
