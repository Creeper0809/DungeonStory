using System;
using System.Collections.Generic;

namespace DungeonStory.Foundation
{
    public interface IGameEventBus
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> listener);
        void Publish<TEvent>(TEvent gameEvent);
        void Clear();
    }

    public sealed class GameEventBus : IGameEventBus
    {
        private readonly Dictionary<Type, IEventChannel> channels =
            new Dictionary<Type, IEventChannel>();

        public IDisposable Subscribe<TEvent>(Action<TEvent> listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            EventChannel<TEvent> channel = GetOrCreateChannel<TEvent>();
            channel.Add(listener);
            return new EventSubscription<TEvent>(channel, listener);
        }

        public void Publish<TEvent>(TEvent gameEvent)
        {
            if (channels.TryGetValue(typeof(TEvent), out IEventChannel channel))
            {
                ((EventChannel<TEvent>)channel).Publish(gameEvent);
            }
        }

        public void Clear()
        {
            channels.Clear();
        }

        private EventChannel<TEvent> GetOrCreateChannel<TEvent>()
        {
            Type eventType = typeof(TEvent);
            if (channels.TryGetValue(eventType, out IEventChannel channel))
            {
                return (EventChannel<TEvent>)channel;
            }

            EventChannel<TEvent> created = new EventChannel<TEvent>();
            channels.Add(eventType, created);
            return created;
        }

        private interface IEventChannel
        {
        }

        private sealed class EventChannel<TEvent> : IEventChannel
        {
            private readonly List<Action<TEvent>> listeners = new List<Action<TEvent>>();

            public void Add(Action<TEvent> listener)
            {
                if (!listeners.Contains(listener))
                {
                    listeners.Add(listener);
                }
            }

            public void Remove(Action<TEvent> listener)
            {
                listeners.Remove(listener);
            }

            public void Publish(TEvent gameEvent)
            {
                Action<TEvent>[] snapshot = listeners.ToArray();
                for (int index = 0; index < snapshot.Length; index++)
                {
                    snapshot[index]?.Invoke(gameEvent);
                }
            }
        }

        private sealed class EventSubscription<TEvent> : IDisposable
        {
            private EventChannel<TEvent> channel;
            private Action<TEvent> listener;

            public EventSubscription(EventChannel<TEvent> channel, Action<TEvent> listener)
            {
                this.channel = channel;
                this.listener = listener;
            }

            public void Dispose()
            {
                if (channel == null)
                {
                    return;
                }

                channel.Remove(listener);
                channel = null;
                listener = null;
            }
        }
    }
}
