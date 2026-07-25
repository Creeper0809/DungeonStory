using System;
using System.Linq;
using DungeonStory.Foundation;
using NUnit.Framework;

namespace DungeonStory.Tests.Foundation
{
    public sealed class RandomStreamProviderTests
    {
        [Test]
        public void SameSeedAndStreamIdProduceSameSequence()
        {
            RandomStreamProvider first = new RandomStreamProvider(771);
            RandomStreamProvider second = new RandomStreamProvider(771);

            int[] firstSequence = Draw(first.Get("contract"), 12);
            int[] secondSequence = Draw(second.Get("contract"), 12);

            Assert.That(secondSequence, Is.EqualTo(firstSequence));
        }

        [Test]
        public void CapturedStateRestoresAtTheNextDraw()
        {
            RandomStreamProvider source = new RandomStreamProvider(991);
            IRandomStream sourceStream = source.Get("save-round-trip");
            Draw(sourceStream, 9);
            RandomStreamStateSnapshot[] snapshots =
                source.CaptureStates().ToArray();
            int expectedNext = sourceStream.NextInt(0, 1_000_000);

            RandomStreamProvider restored = new RandomStreamProvider(1);
            restored.RestoreStates(source.RootSeed, snapshots);
            int actualNext = restored
                .Get("save-round-trip")
                .NextInt(0, 1_000_000);

            Assert.That(actualNext, Is.EqualTo(expectedNext));
        }

        [Test]
        public void ReseedUpdatesPreviouslyIssuedStreamReferences()
        {
            RandomStreamProvider provider = new RandomStreamProvider(37);
            IRandomStream issuedBeforeReseed = provider.Get("held-reference");

            provider.Reseed(811);
            int actual = issuedBeforeReseed.NextInt(0, 1_000_000);

            RandomStreamProvider expectedProvider =
                new RandomStreamProvider(811);
            int expected = expectedProvider
                .Get("held-reference")
                .NextInt(0, 1_000_000);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void DuplicateStreamStateIsRejected()
        {
            RandomStreamStateSnapshot duplicate =
                new RandomStreamStateSnapshot("duplicate", 17UL);
            RandomStreamProvider provider = new RandomStreamProvider(3);

            Assert.Throws<InvalidOperationException>(() =>
                provider.RestoreStates(3, new[] { duplicate, duplicate }));
        }

        private static int[] Draw(IRandomStream stream, int count)
        {
            int[] values = new int[count];
            for (int index = 0; index < count; index++)
            {
                values[index] = stream.NextInt(-100_000, 100_000);
            }

            return values;
        }
    }
}
