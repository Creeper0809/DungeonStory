using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    public sealed class WildlifeTransactionalRestoreArchitectureTests
    {
        [Test]
        public void ActorRestorePublicationCanRollbackOrComplete()
        {
            FakeRestoreHost host = new FakeRestoreHost();
            WildlifeActorRestoreLifecycle lifecycle =
                new WildlifeActorRestoreLifecycle(host);

            lifecycle.Prepare();
            lifecycle.Publish();
            lifecycle.ValidatePublication();
            Assert.That(host.RegisterCount, Is.EqualTo(1));
            Assert.That(lifecycle.IsDetached, Is.False);
            Assert.That(lifecycle.IsPublicationPending, Is.True);

            lifecycle.RollbackPublication();
            Assert.That(host.UnregisterCount, Is.EqualTo(1));
            Assert.That(lifecycle.IsDetached, Is.True);
            Assert.That(lifecycle.IsPublicationPending, Is.False);

            lifecycle.Publish();
            lifecycle.CompletePublication();
            Assert.That(host.RegisterCount, Is.EqualTo(2));
            Assert.That(host.UnregisterCount, Is.EqualTo(1));
            Assert.That(lifecycle.IsDetached, Is.False);
            Assert.That(lifecycle.IsPublicationPending, Is.False);
            Assert.Throws<InvalidOperationException>(
                lifecycle.RollbackPublication);
        }

        [Test]
        public void ActorPublicationFailureReturnsToDetachedCandidateState()
        {
            FakeRestoreHost host = new FakeRestoreHost
            {
                ThrowOnRegister = true
            };
            WildlifeActorRestoreLifecycle lifecycle =
                new WildlifeActorRestoreLifecycle(host);

            lifecycle.Prepare();
            Assert.Throws<InvalidOperationException>(lifecycle.Publish);
            Assert.That(host.RegisterCount, Is.EqualTo(1));
            Assert.That(host.UnregisterCount, Is.EqualTo(1));
            Assert.That(lifecycle.IsDetached, Is.True);
            Assert.That(lifecycle.IsPublicationPending, Is.False);
        }

        [Test]
        public void PublishKeepsOldPopulationAndActorsUntilCompletion()
        {
            string source = ReadScript(
                "Services/Wildlife/WildlifeRestoreRuntime.cs");
            string publish = ExtractMethod(
                source,
                "public void Publish()",
                "public void RollbackPublished()");

            AssertInOrder(
                publish,
                "new WildlifePublication(",
                "activePublication = publication;",
                "ecosystemRuntime.ApplyRestoreCandidate(",
                "carcassService.ApplyFreshnessRestoreCandidate(",
                "port.ReplacePopulation(published.Population);",
                "port.RebuildPopulationRuntimes();",
                "actor.PublishDetachedRestore();",
                "publication.PublishedActors.Add(actor);",
                "actor.ValidateDetachedRestorePublication();");
            Assert.That(publish, Does.Not.Contain("DestroyPopulationActors("));
            Assert.That(publish, Does.Not.Contain("SetActive(true)"));
            Assert.That(publish, Does.Not.Contain("ClearWildlifeCandidate("));
        }

        [Test]
        public void RollbackReversesActorsPopulationFreshnessThenEcosystem()
        {
            string source = ReadScript(
                "Services/Wildlife/WildlifeRestoreRuntime.cs");
            string rollback = ExtractMethod(
                source,
                "public void RollbackPublished()",
                "public void Complete()");

            AssertInOrder(
                rollback,
                "actor.RollbackDetachedRestorePublication",
                "port.ReplacePopulation(publication.PreviousPopulation);",
                "port.RebuildPopulationRuntimes",
                "carcassService.RollbackFreshnessRestore(",
                "ecosystemRuntime.RollbackRestore(",
                "publication.Candidate.Discard",
                "ClearWildlifeCandidate");
            Assert.That(rollback, Does.Contain(
                "for (int index = publication.PublishedActors.Count - 1;"));
        }

        [Test]
        public void CompletionAloneActivatesNewActorsAndRetiresOldPopulation()
        {
            string source = ReadScript(
                "Services/Wildlife/WildlifeRestoreRuntime.cs");
            string complete = ExtractMethod(
                source,
                "public void Complete()",
                "public void Discard()");

            AssertInOrder(
                complete,
                "ecosystemRuntime.CompleteRestore(",
                "carcassService.CompleteFreshnessRestore(",
                "oldActor.gameObject.SetActive(false)",
                "actor.CompleteDetachedRestorePublication",
                "actor.gameObject.SetActive(true)",
                "worldRuntime.DestroyPopulationActors(",
                "ClearWildlifeCandidate");
            Assert.That(complete, Does.Contain("void Attempt(Action completion)"));
            Assert.That(complete, Does.Contain("catch"));
        }

        [Test]
        public void EcosystemAndFreshnessUseExactRootSwapTransactions()
        {
            string ecosystem = ReadScript(
                "Models/Wildlife/Core/WildlifeEcosystemRuntime.Restore.cs");
            string carcasses = ReadScript(
                "Services/Wildlife/WildlifeCarcassService.cs");

            AssertInOrder(
                ecosystem,
                "previousSpeciesRespawnAt = speciesRespawnAt;",
                "previousPatches = patches;",
                "new WildlifeEcosystemRestoreTransaction(",
                "speciesRespawnAt = candidate.SpeciesRespawnAt;",
                "patches = candidate.Patches;");
            Assert.That(ecosystem, Does.Contain(
                "speciesRespawnAt = previousSpeciesRespawnAt;"));
            Assert.That(ecosystem, Does.Contain(
                "patches = previousPatches;"));
            Assert.That(ecosystem.IndexOf(
                "presentation.Clear();",
                StringComparison.Ordinal),
                Is.GreaterThan(ecosystem.IndexOf(
                    "complete: () =>",
                    StringComparison.Ordinal)));

            AssertInOrder(
                carcasses,
                "BuildFreshnessState(entries);",
                "previous =",
                "new WildlifeCarcassFreshnessRestoreTransaction(",
                "freshnessByStackId = restored;");
            Assert.That(carcasses, Does.Contain(
                "freshnessByStackId = previous;"));
        }

        [Test]
        public void RuntimeOverridesEveryTransactionParticipantPhase()
        {
            string source = ReadScript(
                "Services/Wildlife/WildlifeRuntime.cs");

            Assert.That(source, Does.Contain(
                "WildlifeRestoreCoordinator.RestoreParticipantId"));
            Assert.That(source, Does.Contain(
                "public void BeginRestoreCandidate()"));
            Assert.That(source, Does.Contain(
                "public void PublishRestoreCandidate()"));
            Assert.That(source, Does.Contain(
                "public void RollbackPublishedRestoreCandidate()"));
            Assert.That(source, Does.Contain(
                "public void CompleteRestoreCandidate()"));
            Assert.That(source, Does.Contain(
                "public void DiscardRestoreCandidate()"));
        }

        private static string ReadScript(string relativePath)
        {
            return File.ReadAllText(Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                relativePath)));
        }

        private static string ExtractMethod(
            string source,
            string startMarker,
            string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0),
                $"Missing start marker: {startMarker}");
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start),
                $"Missing end marker after {startMarker}: {endMarker}");
            return source.Substring(start, end - start);
        }

        private static void AssertInOrder(string source, params string[] markers)
        {
            int cursor = -1;
            foreach (string marker in markers)
            {
                int index = source.IndexOf(marker, cursor + 1, StringComparison.Ordinal);
                Assert.That(index, Is.GreaterThan(cursor),
                    $"Expected marker after index {cursor}: {marker}");
                cursor = index;
            }
        }

        private sealed class FakeRestoreHost : IWildlifeActorRestoreHost
        {
            public bool IsInitialized => false;
            public int RegisterCount { get; private set; }
            public int UnregisterCount { get; private set; }
            public bool ThrowOnRegister { get; set; }

            public void Register()
            {
                RegisterCount++;
                if (ThrowOnRegister)
                {
                    throw new InvalidOperationException("fault seam");
                }
            }
            public void Unregister() => UnregisterCount++;
            public void Discard()
            {
            }
        }
    }
}
