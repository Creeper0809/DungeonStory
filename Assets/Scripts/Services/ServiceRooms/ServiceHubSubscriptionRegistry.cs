using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

internal sealed class ServiceHubSubscriptionRegistry<THub>
    where THub : class
{
    private sealed class ReferenceComparer : IEqualityComparer<THub>
    {
        internal static readonly ReferenceComparer Instance = new();

        public bool Equals(THub left, THub right) =>
            ReferenceEquals(left, right);

        public int GetHashCode(THub value) =>
            RuntimeHelpers.GetHashCode(value);
    }

    private readonly Dictionary<THub, Action> handlers =
        new(ReferenceComparer.Instance);
    private readonly Action<THub, Action> attach;
    private readonly Action<THub, Action> detach;

    internal ServiceHubSubscriptionRegistry(
        Action<THub, Action> attach,
        Action<THub, Action> detach)
    {
        this.attach = attach ?? throw new ArgumentNullException(nameof(attach));
        this.detach = detach ?? throw new ArgumentNullException(nameof(detach));
    }

    internal int Count => handlers.Count;

    internal void Subscribe(THub hub, Action<THub> destroyed)
    {
        if (hub == null || destroyed == null || handlers.ContainsKey(hub))
        {
            return;
        }

        Action handler = () => destroyed(hub);
        handlers.Add(hub, handler);
        attach(hub, handler);
    }

    internal void Synchronize(
        IEnumerable<THub> hubs,
        Action<THub> destroyed)
    {
        HashSet<THub> desired = new(
            (hubs ?? Array.Empty<THub>())
            .Where(hub => hub != null)
            .ToArray(),
            ReferenceComparer.Instance);
        foreach (THub stale in handlers.Keys
                     .Where(hub => !desired.Contains(hub))
                     .ToArray())
        {
            Unsubscribe(stale);
        }
        foreach (THub hub in desired)
        {
            Subscribe(hub, destroyed);
        }
    }

    internal void Unsubscribe(THub hub)
    {
        if (hub != null && handlers.Remove(hub, out Action handler))
        {
            detach(hub, handler);
        }
    }

    internal void Clear()
    {
        foreach (KeyValuePair<THub, Action> pair in handlers)
        {
            if (pair.Key != null)
            {
                detach(pair.Key, pair.Value);
            }
        }
        handlers.Clear();
    }
}
