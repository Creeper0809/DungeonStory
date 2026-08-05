using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    public sealed class WildlifeRuntimeArchitectureTests
    {
        private static string WildlifePath(string fileName)
        {
            return Path.Combine(
                Application.dataPath,
                "Scripts",
                "Services",
                "Wildlife",
                fileName);
        }

        [Test]
        public void RuntimeRestoreAndWorldResponsibilitiesRemainSeparated()
        {
            string runtime = File.ReadAllText(WildlifePath("WildlifeRuntime.cs"));
            string restore = File.ReadAllText(WildlifePath("WildlifeRestoreRuntime.cs"));
            string world = File.ReadAllText(WildlifePath("WildlifeWorldRuntime.cs"));

            Assert.That(File.ReadAllLines(WildlifePath("WildlifeRuntime.cs")).Length,
                Is.LessThanOrEqualTo(800));
            Assert.That(File.ReadAllLines(WildlifePath("WildlifeRestoreRuntime.cs")).Length,
                Is.LessThanOrEqualTo(800));
            Assert.That(File.ReadAllLines(WildlifePath("WildlifeWorldRuntime.cs")).Length,
                Is.LessThanOrEqualTo(800));

            Assert.That(runtime, Does.Contain("public sealed class WildlifeRuntime"));
            Assert.That(runtime, Does.Contain("IWildlifeRestorePort"));
            Assert.That(runtime, Does.Contain("restoreCoordinator.BuildCandidate("));
            Assert.That(runtime, Does.Contain("restoreCoordinator.Publish()"));
            Assert.That(runtime, Does.Contain("restoreCoordinator.RollbackPublished()"));
            Assert.That(runtime, Does.Contain("restoreCoordinator.Complete()"));
            Assert.That(runtime, Does.Not.Contain("partial class WildlifeRuntime"));

            Assert.That(restore, Does.Contain("sealed class WildlifeRestoreCoordinator"));
            Assert.That(restore, Does.Contain("interface IWildlifeRestorePort"));
            Assert.That(restore, Does.Not.Contain("partial class WildlifeRuntime"));
            Assert.That(restore, Does.Not.Contain("WildlifeRuntime.Carcass"));

            Assert.That(world, Does.Contain("sealed class WildlifeWorldRuntime"));
            Assert.That(world, Does.Contain("actor.PrepareForDetachedRestore()"));
            Assert.That(world, Does.Contain("actor.DiscardDetachedRestore()"));
            Assert.That(world, Does.Not.Contain("partial class WildlifeRuntime"));
        }

        [Test]
        public void RestorePublicationRetainsV18WorldOrdering()
        {
            string restore = File.ReadAllText(WildlifePath("WildlifeRestoreRuntime.cs"));
            string world = File.ReadAllText(WildlifePath("WildlifeWorldRuntime.cs"));

            AssertOrdered(
                restore,
                "activePublication = publication;",
                "ecosystemRuntime.ApplyRestoreCandidate(published.Ecosystem)",
                "carcassService.ApplyFreshnessRestoreCandidate(",
                "port.ReplacePopulation(published.Population);",
                "port.RebuildPopulationRuntimes()",
                "actor.PublishDetachedRestore()",
                "actor.ValidateDetachedRestorePublication()");

            AssertOrdered(
                restore,
                "public void Complete()",
                "ecosystemRuntime.CompleteRestore(",
                "carcassService.CompleteFreshnessRestore(",
                "oldActor.gameObject.SetActive(false)",
                "actor.CompleteDetachedRestorePublication",
                "actor.gameObject.SetActive(true)",
                "worldRuntime.DestroyPopulationActors(",
                "restoreServices.CandidatePublisher.ClearWildlifeCandidate()");
            Assert.That(restore, Does.Not.Contain(
                "worldRuntime.DestroyPopulationActors(previous)"));

            AssertOrdered(
                world,
                "gameObject.SetActive(false)",
                "actor.PrepareForDetachedRestore()",
                "actor.ConfigureRuntimeServices(",
                "actor.Initialize(");
            Assert.That(restore, Does.Contain("worldRuntime.DiscardCandidateActors("));
        }

        private static void AssertOrdered(string source, params string[] markers)
        {
            int previous = -1;
            foreach (string marker in markers)
            {
                int current = source.IndexOf(marker, System.StringComparison.Ordinal);
                Assert.That(current, Is.GreaterThan(previous), marker);
                previous = current;
            }
        }
    }
}
