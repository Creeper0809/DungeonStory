using System;
using System.Collections.Generic;

namespace DungeonStory.Foundation
{
    public static class RandomStreamScopeIds
    {
        public static string WildlifeActor(string wildlifeId) =>
            Build("wildlife.actor:", wildlifeId, nameof(wildlifeId));

        public static string Encounter(string encounterId) =>
            Build("combat-resolution:", encounterId, nameof(encounterId));

        private static string Build(
            string prefix,
            string persistentId,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(persistentId)
                || !string.Equals(
                    persistentId,
                    persistentId.Trim(),
                    StringComparison.Ordinal)
                || persistentId.IndexOf('\r') >= 0
                || persistentId.IndexOf('\n') >= 0)
            {
                throw new ArgumentException(
                    "A canonical persistent ID is required for a random stream.",
                    parameterName);
            }

            return string.Concat(prefix, persistentId);
        }
    }

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

    public readonly struct RandomStreamDiagnosticSnapshot
    {
        public RandomStreamDiagnosticSnapshot(
            string streamId,
            ulong state,
            long drawCount)
        {
            StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
            State = state;
            DrawCount = drawCount;
        }

        public string StreamId { get; }
        public ulong State { get; }
        public long DrawCount { get; }
    }

    public interface IRandomStreamDiagnosticsQuery
    {
        IReadOnlyList<RandomStreamDiagnosticSnapshot> Capture();
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

    public sealed class RandomStreamProvider :
        IRandomStreamProvider,
        IRandomStreamDiagnosticsQuery
    {
        private readonly Dictionary<string, ProviderRandomStream> handles =
            new Dictionary<string, ProviderRandomStream>(StringComparer.Ordinal);
        private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
        private readonly RandomStreamAggregateState standaloneState;
        private readonly Dictionary<string, long> drawCounts =
            new Dictionary<string, long>(StringComparer.Ordinal);

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
            string canonical = RequireCanonicalStreamId(streamId);
            if (handles.TryGetValue(canonical, out ProviderRandomStream stream))
            {
                return stream;
            }

            EnsureStreamState(canonical);
            stream = new ProviderRandomStream(this, canonical);
            handles.Add(canonical, stream);
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

            drawCounts.Clear();
        }

        public IReadOnlyList<RandomStreamDiagnosticSnapshot> Capture()
        {
            RandomStreamAggregateState state = State;
            List<string> streamIds = new List<string>(state.StreamStates.Keys);
            streamIds.Sort(StringComparer.Ordinal);
            List<RandomStreamDiagnosticSnapshot> snapshots =
                new List<RandomStreamDiagnosticSnapshot>(streamIds.Count);
            for (int index = 0; index < streamIds.Count; index++)
            {
                string streamId = streamIds[index];
                snapshots.Add(new RandomStreamDiagnosticSnapshot(
                    streamId,
                    state.StreamStates[streamId],
                    GetDrawCount(streamId)));
            }

            return snapshots;
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

                string streamId = RequireCanonicalStreamId(snapshot.StreamId);
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
            drawCounts.Remove(streamId);
        }

        private ulong NextState(string streamId)
        {
            ulong value = GetState(streamId);
            value ^= value << 13;
            value ^= value >> 7;
            value ^= value << 17;
            value = NormalizeStreamState(value);
            WritableState.StreamStates[streamId] = value;
            IncrementDrawCount(streamId);
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
            drawCounts.Clear();
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

        private long GetDrawCount(string streamId) =>
            drawCounts.TryGetValue(streamId, out long count) ? count : 0L;

        private void IncrementDrawCount(string streamId)
        {
            long current = GetDrawCount(streamId);
            drawCounts[streamId] = checked(current + 1L);
        }

        private static string RequireCanonicalStreamId(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                throw new ArgumentException(
                    "A random stream requires a stable, non-empty ID.",
                    nameof(streamId));
            }

            if (!string.Equals(streamId, streamId.Trim(), StringComparison.Ordinal)
                || streamId.IndexOf('\r') >= 0
                || streamId.IndexOf('\n') >= 0)
            {
                throw new ArgumentException(
                    $"Random stream ID '{streamId}' is not canonical.",
                    nameof(streamId));
            }

            if (string.Equals(streamId, "character-ai", StringComparison.Ordinal)
                || string.Equals(streamId, "character-movement", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Global character random stream '{streamId}' is forbidden; "
                    + "use a persistent-character scoped stream ID.");
            }

            return streamId;
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

    public readonly struct CounterfactualRandomKey :
        IEquatable<CounterfactualRandomKey>
    {
        public CounterfactualRandomKey(
            int rootSeed,
            string scenarioId,
            string eventKind,
            string entityId,
            int windowIndex,
            int ordinal)
        {
            if (rootSeed == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rootSeed),
                    "A counterfactual root seed must be non-zero.");
            }
            if (windowIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowIndex));
            }
            if (ordinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }

            RootSeed = rootSeed;
            ScenarioId = RequireCanonicalPart(scenarioId, nameof(scenarioId));
            EventKind = RequireCanonicalPart(eventKind, nameof(eventKind));
            EntityId = RequireCanonicalPart(entityId, nameof(entityId));
            WindowIndex = windowIndex;
            Ordinal = ordinal;
        }

        public int RootSeed { get; }
        public string ScenarioId { get; }
        public string EventKind { get; }
        public string EntityId { get; }
        public int WindowIndex { get; }
        public int Ordinal { get; }

        public DeterministicRandomSequence CreateSequence() =>
            new DeterministicRandomSequence(ComputeSeed());

        public bool Equals(CounterfactualRandomKey other) =>
            RootSeed == other.RootSeed
            && WindowIndex == other.WindowIndex
            && Ordinal == other.Ordinal
            && string.Equals(ScenarioId, other.ScenarioId, StringComparison.Ordinal)
            && string.Equals(EventKind, other.EventKind, StringComparison.Ordinal)
            && string.Equals(EntityId, other.EntityId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is CounterfactualRandomKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RootSeed;
                hash = (hash * 397) ^ WindowIndex;
                hash = (hash * 397) ^ Ordinal;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ScenarioId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(EventKind);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(EntityId);
                return hash;
            }
        }

        private int ComputeSeed()
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                MixInt(ref hash, RootSeed);
                MixString(ref hash, ScenarioId);
                MixString(ref hash, EventKind);
                MixString(ref hash, EntityId);
                MixInt(ref hash, WindowIndex);
                MixInt(ref hash, Ordinal);
                int seed = (int)(hash ^ (hash >> 32));
                return seed == 0 ? 1 : seed;
            }
        }

        private static string RequireCanonicalPart(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
                || value.IndexOf('\r') >= 0
                || value.IndexOf('\n') >= 0)
            {
                throw new ArgumentException(
                    "Counterfactual random key parts must be canonical non-empty text.",
                    parameterName);
            }

            return value;
        }

        private static void MixInt(ref ulong hash, int value)
        {
            unchecked
            {
                MixChar(ref hash, (char)value);
                MixChar(ref hash, (char)(value >> 16));
            }
        }

        private static void MixString(ref ulong hash, string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                MixChar(ref hash, value[index]);
            }
            MixChar(ref hash, '\0');
        }

        private static void MixChar(ref ulong hash, char value)
        {
            unchecked
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
        }
    }

    public sealed class CounterfactualRandomKeySet
    {
        private readonly HashSet<CounterfactualRandomKey> keys = new();

        public DeterministicRandomSequence CreateUnique(
            CounterfactualRandomKey key)
        {
            if (!keys.Add(key))
            {
                throw new InvalidOperationException(
                    "A counterfactual random event key was used more than once.");
            }

            return key.CreateSequence();
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
