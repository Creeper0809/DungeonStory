using System;
using System.Collections.Generic;

namespace DungeonStory.Foundation
{
    public interface IRandomStream
    {
        ulong State { get; }
        int NextInt(int minInclusive, int maxExclusive);
        float NextFloat();
        bool Chance(float probability);
        void Restore(ulong state);
    }

    public interface IRandomStreamProvider
    {
        int RootSeed { get; }
        IRandomStream Get(string streamId);
        void Reseed(int rootSeed);
        IReadOnlyList<RandomStreamStateSnapshot> CaptureStates();
        RandomStreamRestoreCandidate BuildRestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots);
        void RestoreStates(RandomStreamRestoreCandidate candidate);
        void RestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots);
    }

    public sealed class RandomStreamRestoreCandidate
    {
        internal RandomStreamRestoreCandidate(RandomStreamAggregateState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        internal RandomStreamAggregateState State { get; }
    }

    public sealed class RandomStreamStateSnapshot
    {
        public RandomStreamStateSnapshot(string streamId, ulong state)
        {
            StreamId = string.IsNullOrWhiteSpace(streamId)
                ? throw new ArgumentException(
                    "A random stream snapshot requires an ID.",
                    nameof(streamId))
                : streamId.Trim();
            State = state;
        }

        public string StreamId { get; }
        public ulong State { get; }
    }

    internal sealed class RandomStreamAggregateState
    {
        internal int RootSeed = 1;
        internal Dictionary<string, ulong> StreamStates { get; } =
            new Dictionary<string, ulong>(StringComparer.Ordinal);
    }

    public sealed class RandomStreamProvider : IRandomStreamProvider
    {
        private readonly Dictionary<string, ProviderRandomStream> handles =
            new Dictionary<string, ProviderRandomStream>(StringComparer.Ordinal);
        private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
        private readonly RandomStreamAggregateState standaloneState;

        public RandomStreamProvider()
            : this(1)
        {
        }

        public RandomStreamProvider(int rootSeed)
        {
            standaloneState = CreateState(rootSeed);
        }

        public RandomStreamProvider(
            DungeonRuntimeAggregateRootStore aggregateRootStore)
        {
            this.aggregateRootStore = aggregateRootStore
                ?? throw new ArgumentNullException(nameof(aggregateRootStore));
            aggregateRootStore.GetOrCreate(() => CreateState(1));
        }

        public int RootSeed => State.RootSeed;

        public IRandomStream Get(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                throw new ArgumentException("A random stream requires a stable, non-empty ID.", nameof(streamId));
            }

            string normalized = streamId.Trim();
            if (handles.TryGetValue(normalized, out ProviderRandomStream stream))
            {
                return stream;
            }

            EnsureStreamState(normalized);
            stream = new ProviderRandomStream(this, normalized);
            handles.Add(normalized, stream);
            return stream;
        }

        public void Reseed(int rootSeed)
        {
            RandomStreamAggregateState state = WritableState;
            state.RootSeed = NormalizeRootSeed(rootSeed);
            foreach (string streamId in new List<string>(state.StreamStates.Keys))
            {
                state.StreamStates[streamId] = CombineSeed(
                    state.RootSeed,
                    streamId);
            }
        }

        public IReadOnlyList<RandomStreamStateSnapshot> CaptureStates()
        {
            RandomStreamAggregateState state = State;
            List<string> streamIds = new List<string>(state.StreamStates.Keys);
            streamIds.Sort(StringComparer.Ordinal);
            List<RandomStreamStateSnapshot> snapshots =
                new List<RandomStreamStateSnapshot>(streamIds.Count);
            for (int index = 0; index < streamIds.Count; index++)
            {
                string streamId = streamIds[index];
                snapshots.Add(new RandomStreamStateSnapshot(
                    streamId,
                    state.StreamStates[streamId]));
            }

            return snapshots;
        }

        public void RestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots)
        {
            RestoreStates(BuildRestoreStates(rootSeed, snapshots));
        }

        public RandomStreamRestoreCandidate BuildRestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots)
        {
            if (rootSeed == 0)
            {
                throw new InvalidOperationException(
                    "Random-stream restore root seed must be non-zero.");
            }
            if (snapshots == null)
            {
                throw new InvalidOperationException(
                    "Random-stream restore snapshots are missing.");
            }

            RandomStreamAggregateState restored = new()
            {
                RootSeed = rootSeed
            };
            HashSet<string> restoredIds =
                new HashSet<string>(StringComparer.Ordinal);
            foreach (RandomStreamStateSnapshot snapshot in snapshots)
            {
                if (snapshot == null)
                {
                    throw new InvalidOperationException(
                        "Random-stream restore contains a null snapshot.");
                }

                string streamId = snapshot.StreamId;
                if (!restoredIds.Add(streamId))
                {
                    throw new InvalidOperationException(
                        $"Duplicate random stream state '{streamId}'.");
                }
                if (snapshot.State == 0UL)
                {
                    throw new InvalidOperationException(
                        $"Random stream '{streamId}' has a zero restore state.");
                }

                restored.StreamStates.Add(
                    streamId,
                    snapshot.State);
            }

            return new RandomStreamRestoreCandidate(restored);
        }

        public void RestoreStates(RandomStreamRestoreCandidate candidate)
        {
            ReplaceState((candidate
                    ?? throw new ArgumentNullException(nameof(candidate)))
                .State);
        }

        private RandomStreamAggregateState State =>
            aggregateRootStore != null
                ? aggregateRootStore.GetOrCreate(() => CreateState(1))
                : standaloneState;

        private RandomStreamAggregateState WritableState =>
            aggregateRootStore != null
                ? aggregateRootStore.GetOrCreateWritable(
                    () => CreateState(1),
                    CloneState)
                : standaloneState;

        private ulong GetState(string streamId)
        {
            EnsureStreamState(streamId);
            return State.StreamStates[streamId];
        }

        private void RestoreState(string streamId, ulong value)
        {
            WritableState.StreamStates[streamId] = NormalizeStreamState(value);
        }

        private ulong NextState(string streamId)
        {
            ulong value = GetState(streamId);
            value ^= value << 13;
            value ^= value >> 7;
            value ^= value << 17;
            value = NormalizeStreamState(value);
            WritableState.StreamStates[streamId] = value;
            return value;
        }

        private void EnsureStreamState(string streamId)
        {
            RandomStreamAggregateState state = WritableState;
            if (!state.StreamStates.ContainsKey(streamId))
            {
                state.StreamStates.Add(
                    streamId,
                    CombineSeed(state.RootSeed, streamId));
            }
        }

        private void ReplaceState(RandomStreamAggregateState restored)
        {
            if (aggregateRootStore != null)
            {
                aggregateRootStore.Replace(restored);
                return;
            }

            standaloneState.RootSeed = restored.RootSeed;
            standaloneState.StreamStates.Clear();
            foreach (KeyValuePair<string, ulong> pair in restored.StreamStates)
            {
                standaloneState.StreamStates.Add(pair.Key, pair.Value);
            }
        }

        private static RandomStreamAggregateState CreateState(int rootSeed)
        {
            return new RandomStreamAggregateState
            {
                RootSeed = NormalizeRootSeed(rootSeed)
            };
        }

        private static RandomStreamAggregateState CloneState(
            RandomStreamAggregateState source)
        {
            RandomStreamAggregateState clone = CreateState(source?.RootSeed ?? 1);
            if (source == null)
            {
                return clone;
            }

            foreach (KeyValuePair<string, ulong> pair in source.StreamStates)
            {
                clone.StreamStates.Add(pair.Key, pair.Value);
            }

            return clone;
        }

        private static int NormalizeRootSeed(int rootSeed)
        {
            return rootSeed == 0 ? 1 : rootSeed;
        }

        private static ulong NormalizeStreamState(ulong state)
        {
            return state == 0UL
                ? 0x9E3779B97F4A7C15UL
                : state;
        }

        private static ulong CombineSeed(int rootSeed, string streamId)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                hash ^= (uint)rootSeed;
                hash *= 1099511628211UL;
                for (int index = 0; index < streamId.Length; index++)
                {
                    hash ^= streamId[index];
                    hash *= 1099511628211UL;
                }

                return hash == 0UL ? 0x9E3779B97F4A7C15UL : hash;
            }
        }

        private sealed class ProviderRandomStream : IRandomStream
        {
            private readonly RandomStreamProvider provider;
            private readonly string streamId;

            public ProviderRandomStream(
                RandomStreamProvider provider,
                string streamId)
            {
                this.provider = provider
                    ?? throw new ArgumentNullException(nameof(provider));
                this.streamId = streamId
                    ?? throw new ArgumentNullException(nameof(streamId));
            }

            public ulong State => provider.GetState(streamId);

            public int NextInt(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(maxExclusive),
                        "The exclusive maximum must be greater than the inclusive minimum.");
                }

                uint range = (uint)(maxExclusive - minInclusive);
                return minInclusive + (int)(provider.NextState(streamId) % range);
            }

            public float NextFloat()
            {
                return (float)(provider.NextState(streamId) >> 40)
                    * (1f / (1u << 24));
            }

            public bool Chance(float probability)
            {
                if (probability <= 0f)
                {
                    return false;
                }

                return probability >= 1f || NextFloat() < probability;
            }

            public void Restore(ulong state)
            {
                provider.RestoreState(streamId, state);
            }
        }
    }

    /// <summary>
    /// A local deterministic sequence for pure, seed-addressed calculations.
    /// It is deliberately not registered as run state and must never be saved;
    /// callers can reproduce the same result from the same explicit seed.
    /// </summary>
    public sealed class DeterministicRandomSequence : IRandomStream
    {
        private readonly XorShiftRandomStream stream;

        public DeterministicRandomSequence(int seed)
        {
            unchecked
            {
                ulong expanded = (ulong)(uint)(seed == 0 ? 1 : seed);
                expanded ^= 0x9E3779B97F4A7C15UL;
                expanded *= 0xBF58476D1CE4E5B9UL;
                stream = new XorShiftRandomStream(expanded);
            }
        }

        public ulong State => stream.State;

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return stream.NextInt(minInclusive, maxExclusive);
        }

        public float NextFloat()
        {
            return stream.NextFloat();
        }

        public bool Chance(float probability)
        {
            return stream.Chance(probability);
        }

        public void Restore(ulong state)
        {
            stream.Restore(state);
        }
    }

    internal sealed class XorShiftRandomStream : IRandomStream
    {
        private const ulong NonZeroFallback = 0x9E3779B97F4A7C15UL;
        private ulong state;

        public XorShiftRandomStream(ulong seed)
        {
            state = seed == 0UL ? NonZeroFallback : seed;
        }

        public ulong State => state;

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    "The exclusive maximum must be greater than the inclusive minimum.");
            }

            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt64() % range);
        }

        public float NextFloat()
        {
            return (float)(NextUInt64() >> 40) * (1f / (1u << 24));
        }

        public bool Chance(float probability)
        {
            if (probability <= 0f)
            {
                return false;
            }

            return probability >= 1f || NextFloat() < probability;
        }

        public void Restore(ulong restoredState)
        {
            state = restoredState == 0UL ? NonZeroFallback : restoredState;
        }

        private ulong NextUInt64()
        {
            ulong value = state;
            value ^= value << 13;
            value ^= value >> 7;
            value ^= value << 17;
            state = value == 0UL ? NonZeroFallback : value;
            return state;
        }
    }
}
