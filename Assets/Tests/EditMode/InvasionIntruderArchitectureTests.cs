using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    public sealed class InvasionIntruderArchitectureTests
    {
        private static string InvasionPath(string fileName)
        {
            return Path.Combine(
                Application.dataPath,
                "Scripts",
                "Services",
                "Invasion",
                fileName);
        }

        [Test]
        public void RuntimeDelegatesExecutionWithoutDuplicatingStateAuthority()
        {
            string runtime = File.ReadAllText(InvasionPath("InvasionIntruderSystem.cs"));
            string restore = File.ReadAllText(InvasionPath("InvasionIntruderRuntime.Restore.cs"));
            string content = File.ReadAllText(InvasionPath("InvasionIntruderContentBinding.cs"));
            string coordinator = File.ReadAllText(
                InvasionPath("InvasionIntruderExecutionCoordinator.cs"));
            string director = File.ReadAllText(
                InvasionPath("InvasionDirectorRuntime.cs"));
            string directorRestore = File.ReadAllText(
                InvasionPath("InvasionDirectorRuntime.Restore.cs"));

            int runtimeLines = File.ReadAllLines(
                InvasionPath("InvasionIntruderSystem.cs")).Length;
            int restoreLines = File.ReadAllLines(
                InvasionPath("InvasionIntruderRuntime.Restore.cs")).Length;
            int contentLines = File.ReadAllLines(
                InvasionPath("InvasionIntruderContentBinding.cs")).Length;

            Assert.That(runtimeLines, Is.LessThanOrEqualTo(800));
            Assert.That(restoreLines, Is.LessThanOrEqualTo(800));
            Assert.That(contentLines, Is.LessThanOrEqualTo(800));
            Assert.That(
                File.ReadAllLines(InvasionPath("InvasionDirectorRuntime.cs")).Length,
                Is.LessThanOrEqualTo(800));
            Assert.That(runtime, Does.Contain("IInvasionIntruderExecutionHost"));
            Assert.That(runtime, Does.Contain("ExecutionCoordinator.Run("));
            Assert.That(runtime, Does.Contain("ExecutionCoordinator.RunInside()"));
            Assert.That(runtime, Does.Contain("InvasionIntruderRestoreCoordinator"));
            Assert.That(runtime, Does.Contain("RestoreCoordinator.TryPrepare("));
            Assert.That(runtime, Does.Contain("RestoreCoordinator.Publish()"));
            Assert.That(runtime, Does.Not.Contain("private IEnumerator ExecuteBreach"));
            Assert.That(runtime, Does.Not.Contain("private IEnumerator MovePathWithDefense"));
            Assert.That(coordinator, Does.Contain("sealed class InvasionIntruderExecutionCoordinator"));
            Assert.That(coordinator, Does.Not.Contain(": MonoBehaviour"));
            Assert.That(coordinator, Does.Not.Contain("partial class InvasionIntruderRuntime"));
            Assert.That(restore, Does.Contain("sealed class InvasionIntruderRestoreCoordinator"));
            Assert.That(restore, Does.Contain("port.StartRestoredInside()"));
            Assert.That(restore, Does.Contain("DiscardPrepared();"));
            Assert.That(restore, Does.Not.Contain(": MonoBehaviour"));
            Assert.That(restore, Does.Not.Contain("partial class InvasionIntruderRuntime"));
            Assert.That(content, Does.Contain("sealed class InvasionIntruderContentBinding"));
            Assert.That(content, Does.Contain("public void Configure("));
            Assert.That(content, Does.Not.Contain("InvasionIntruderRuntime"));
            Assert.That(director, Does.Contain("InvasionDirectorRestoreCoordinator"));
            Assert.That(director, Does.Contain("restoreCoordinator.Prepare("));
            Assert.That(director, Does.Not.Contain("partial class InvasionDirectorRuntime"));
            Assert.That(
                directorRestore,
                Does.Contain("sealed class InvasionDirectorRestoreCoordinator"));
            Assert.That(directorRestore, Does.Not.Contain(": MonoBehaviour"));
            Assert.That(
                directorRestore,
                Does.Not.Contain("partial class InvasionDirectorRuntime"));
        }

        [Test]
        public void ExecutionCoordinatorRetainsDeterministicAndLifecycleContracts()
        {
            string runtime = File.ReadAllText(InvasionPath("InvasionIntruderSystem.cs"));
            string coordinator = File.ReadAllText(
                InvasionPath("InvasionIntruderExecutionCoordinator.cs"));

            Assert.That(coordinator, Does.Contain("host.Clock.Time"));
            Assert.That(coordinator, Does.Contain("host.Clock.DeltaTime"));
            Assert.That(coordinator, Does.Contain("CommittedAwarenessVersion"));
            Assert.That(coordinator, Does.Contain("RouteCommitmentUntil"));
            Assert.That(coordinator, Does.Contain(".OrderBy(position => awareness"));
            Assert.That(coordinator, Does.Contain(".ThenBy(position => position.y)"));
            Assert.That(coordinator, Does.Contain(".ThenBy(position => position.x)"));
            Assert.That(coordinator, Does.Not.Contain("UnityEngine.Random"));
            Assert.That(coordinator, Does.Not.Contain("Time.time"));

            Assert.That(runtime, Does.Contain("private void Awake()"));
            Assert.That(runtime, Does.Contain("StartCoroutine(Run("));
            Assert.That(runtime, Does.Contain("StopCoroutine(routine)"));
            Assert.That(runtime, Does.Contain("Destroy(gameObject)"));
            Assert.That(runtime, Does.Contain("public event Action<InvasionIntruderRuntime> OnFinished"));
            Assert.That(runtime, Does.Contain("public InvasionIntruderPersistenceState CapturePersistentState"));
            Assert.That(runtime, Does.Contain("public Queue<GridMoveStep> CreateNextPath"));
            Assert.That(runtime, Does.Contain("public bool TryDamageNearbyFacility"));
            Assert.That(runtime, Does.Contain("public void ApplyFinalCombat"));
        }

        [Test]
        public void FactoryOwnsPostCreationFailureCleanup()
        {
            string source = File.ReadAllText(InvasionPath("InvasionIntruderFactory.cs"));
            string create = ExtractMethod(
                source,
                "public InvasionIntruderRuntime Create(",
                "public InvasionIntruderRuntime CreateDetached(");
            string createDetached = ExtractMethod(
                source,
                "public InvasionIntruderRuntime CreateDetached(",
                "public void PublishDetached(");
            string cleanup = ExtractMethod(
                source,
                "private void DestroyFailedCandidate(",
                "private static GameObject CreateDetachedPrefablessObject(");

            AssertInOrder(
                create,
                "GameObject intruderObject = null;",
                "try",
                "characterObjectFactory.CreateInactive(",
                "EnsureRuntime(intruderObject)",
                "ConfigureRuntime(intruderObject)",
                "catch",
                "DestroyFailedCandidate(intruderObject);",
                "throw;");
            AssertInOrder(
                createDetached,
                "GameObject intruderObject = null;",
                "try",
                "characterObjectFactory.CreateDetached(",
                "characterObjectFactory.ComposeDetached(intruderObject);",
                "ConfigureRuntime(intruderObject)",
                "catch",
                "DestroyFailedCandidate(intruderObject);",
                "throw;");
            Assert.That(cleanup, Does.Contain(
                "characterObjectFactory.Destroy(intruderObject);"));
        }

        private static string ExtractMethod(
            string source,
            string startMarker,
            string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
            Assert.That(end, Is.GreaterThan(start), endMarker);
            return source.Substring(start, end - start);
        }

        private static void AssertInOrder(string source, params string[] markers)
        {
            int previous = -1;
            foreach (string marker in markers)
            {
                int current = source.IndexOf(
                    marker,
                    previous + 1,
                    StringComparison.Ordinal);
                Assert.That(current, Is.GreaterThan(previous), marker);
                previous = current;
            }
        }

    }
}
