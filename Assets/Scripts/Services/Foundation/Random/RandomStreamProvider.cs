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
        void RestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots);
    }

    public sealed class RandomStreamStateSnapshot
    {
        public RandomStreamStateSnapshot(string streamId, ulong state)
        {
            StreamId = string.IsNullOrWhiteSpace(streamId)
                ? throw new ArgumentException(
                    "A random stream snapshot requires an ID.",
                    nameof(streamId))
                : streamId;
            State = state;
        }

        public string StreamId { get; }
        public ulong State { get; }
    }

    public sealed class RandomStreamProvider : IRandomStreamProvider
    {
        private readonly Dictionary<string, XorShiftRandomStream> streams =
            new Dictionary<string, XorShiftRandomStream>(StringComparer.Ordinal);

        public RandomStreamProvider()
            : this(1)
        {
        }

        public RandomStreamProvider(int rootSeed)
        {
            Reseed(rootSeed);
        }

        public int RootSeed { get; private set; }

        public IRandomStream Get(string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId))
            {
                throw new ArgumentException("A random stream requires a stable, non-empty ID.", nameof(streamId));
            }

            if (streams.TryGetValue(streamId, out XorShiftRandomStream stream))
            {
                return stream;
            }

            ulong seed = CombineSeed(RootSeed, streamId);
            stream = new XorShiftRandomStream(seed);
            streams.Add(streamId, stream);
            return stream;
        }

        public void Reseed(int rootSeed)
        {
            RootSeed = rootSeed == 0 ? 1 : rootSeed;
            foreach (KeyValuePair<string, XorShiftRandomStream> pair in streams)
            {
                pair.Value.Restore(CombineSeed(RootSeed, pair.Key));
            }
        }

        public IReadOnlyList<RandomStreamStateSnapshot> CaptureStates()
        {
            List<string> streamIds = new List<string>(streams.Keys);
            streamIds.Sort(StringComparer.Ordinal);
            List<RandomStreamStateSnapshot> snapshots =
                new List<RandomStreamStateSnapshot>(streamIds.Count);
            for (int index = 0; index < streamIds.Count; index++)
            {
                string streamId = streamIds[index];
                snapshots.Add(new RandomStreamStateSnapshot(
                    streamId,
                    streams[streamId].State));
            }

            return snapshots;
        }

        public void RestoreStates(
            int rootSeed,
            IEnumerable<RandomStreamStateSnapshot> snapshots)
        {
            Reseed(rootSeed);
            HashSet<string> restoredIds =
                new HashSet<string>(StringComparer.Ordinal);
            foreach (RandomStreamStateSnapshot snapshot in
                     snapshots ?? Array.Empty<RandomStreamStateSnapshot>())
            {
                if (snapshot == null || !restoredIds.Add(snapshot.StreamId))
                {
                    if (snapshot != null)
                    {
                        throw new InvalidOperationException(
                            $"Duplicate random stream state '{snapshot.StreamId}'.");
                    }

                    continue;
                }

                Get(snapshot.StreamId).Restore(snapshot.State);
            }
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
