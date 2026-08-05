using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Exterior
{
    public readonly struct ExteriorIncidentTransition<TState>
        where TState : class
    {
        internal ExteriorIncidentTransition(
            TState state,
            bool wasTerminal,
            bool isTerminal,
            float remainingSeconds)
        {
            State = state;
            WasTerminal = wasTerminal;
            IsTerminal = isTerminal;
            RemainingSeconds = Math.Max(0f, remainingSeconds);
        }

        public TState State { get; }
        public bool WasTerminal { get; }
        public bool IsTerminal { get; }
        public float RemainingSeconds { get; }
    }

    /// <summary>
    /// Single mutable owner for exterior incident history and countdowns. Unity
    /// markers consume transitions from this Aggregate and never advance time.
    /// </summary>
    public sealed class ExteriorIncidentAggregate<TState>
        where TState : class
    {
        private const int MaximumIncidentHistory = 32;

        private readonly List<TState> states = new();
        private readonly IReadOnlyList<TState> statesView;
        private readonly Func<TState, bool> isTerminal;
        private readonly Func<TState, float> getRemainingSeconds;
        private readonly Action<TState, float> setRemainingSeconds;

        public ExteriorIncidentAggregate(
            Func<TState, bool> isTerminal,
            Func<TState, float> getRemainingSeconds,
            Action<TState, float> setRemainingSeconds)
        {
            this.isTerminal = isTerminal
                ?? throw new ArgumentNullException(nameof(isTerminal));
            this.getRemainingSeconds = getRemainingSeconds
                ?? throw new ArgumentNullException(nameof(getRemainingSeconds));
            this.setRemainingSeconds = setRemainingSeconds
                ?? throw new ArgumentNullException(nameof(setRemainingSeconds));
            statesView = states.AsReadOnly();
        }

        public IReadOnlyList<TState> States => statesView;
        public int ActiveCount => states.Count(state => !isTerminal(state));

        public bool AnyActive(Predicate<TState> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return states.Any(state => !isTerminal(state) && predicate(state));
        }

        public TState Find(Predicate<TState> predicate)
        {
            return predicate == null
                ? throw new ArgumentNullException(nameof(predicate))
                : states.Find(predicate);
        }

        public ExteriorIncidentTransition<TState> Add(TState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            NormalizeRemainingSeconds(state);
            states.Add(state);
            return CreateTransition(state, wasTerminal: false);
        }

        public ExteriorIncidentTransition<TState> Mutate(
            TState state,
            Action<TState> mutation)
        {
            if (state == null || !states.Contains(state))
            {
                throw new InvalidOperationException(
                    "Exterior incident mutation requires an Aggregate-owned state.");
            }
            if (mutation == null)
            {
                throw new ArgumentNullException(nameof(mutation));
            }

            bool wasTerminal = isTerminal(state);
            mutation(state);
            NormalizeRemainingSeconds(state);
            return CreateTransition(state, wasTerminal);
        }

        public IReadOnlyList<ExteriorIncidentTransition<TState>> Tick(
            float deltaSeconds,
            Action<TState, float> tickHandler)
        {
            if (tickHandler == null)
            {
                throw new ArgumentNullException(nameof(tickHandler));
            }

            float elapsed = Math.Max(0f, deltaSeconds);
            List<ExteriorIncidentTransition<TState>> transitions = new();
            foreach (TState state in states.ToArray())
            {
                if (state == null || isTerminal(state))
                {
                    continue;
                }

                bool wasTerminal = false;
                setRemainingSeconds(
                    state,
                    Math.Max(0f, getRemainingSeconds(state) - elapsed));
                tickHandler(state, elapsed);
                NormalizeRemainingSeconds(state);
                transitions.Add(CreateTransition(state, wasTerminal));
            }

            TrimHistory();
            return transitions;
        }

        public void ReplaceAll(IEnumerable<TState> restoredStates)
        {
            if (restoredStates == null)
            {
                throw new ArgumentNullException(nameof(restoredStates));
            }

            states.Clear();
            states.AddRange(restoredStates.Where(state => state != null));
            foreach (TState state in states)
            {
                NormalizeRemainingSeconds(state);
            }
            TrimHistory();
        }

        public void Clear()
        {
            states.Clear();
        }

        private ExteriorIncidentTransition<TState> CreateTransition(
            TState state,
            bool wasTerminal)
        {
            return new ExteriorIncidentTransition<TState>(
                state,
                wasTerminal,
                isTerminal(state),
                getRemainingSeconds(state));
        }

        private void NormalizeRemainingSeconds(TState state)
        {
            setRemainingSeconds(
                state,
                Math.Max(0f, getRemainingSeconds(state)));
        }

        private void TrimHistory()
        {
            while (states.Count > MaximumIncidentHistory)
            {
                int index = states.FindIndex(state =>
                    state == null || isTerminal(state));
                if (index < 0)
                {
                    break;
                }

                states.RemoveAt(index);
            }
        }
    }
}
