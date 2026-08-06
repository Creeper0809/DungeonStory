using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    public sealed class CircusTransactionalRestoreArchitectureTests
    {
        [Test]
        public void LateParticipantFailureRestoresExactCircusProjection()
        {
            DungeonRuntimeAggregateRootStore root =
                new DungeonRuntimeAggregateRootStore();
            int releasedOrders = 0;
            int transientClears = 0;
            CircusStateSession session = new CircusStateSession(
                root,
                _ => releasedOrders++,
                () => transientClears++);
            CircusShowOrder original = new CircusShowOrder
            {
                orderId = "circus-order-original",
                stageId = "circus-stage-original"
            };
            session.Add(original);
            session.EnsureProjection();
            releasedOrders = 0;
            transientClears = 0;

            BeginRestoreStaging(root);
            session.Stage(CreateCandidate("circus-order-candidate"));
            CircusProjectionPublication publication =
                session.BeginProjectionPublication();

            Assert.Throws<InvalidOperationException>(() =>
                throw new InvalidOperationException("late participant fault seam"));
            session.RollbackProjection(publication);
            DiscardRestoreStaging(root);

            Assert.That(session.Orders, Has.Count.EqualTo(1));
            Assert.That(session.Orders[0], Is.SameAs(original));
            Assert.That(releasedOrders, Is.Zero,
                "Publish and rollback must not retire the previous projection.");
            Assert.That(transientClears, Is.Zero,
                "Publish and rollback must preserve transient circus state.");

            BeginRestoreStaging(root);
            session.Stage(CreateCandidate("circus-order-committed"));
            publication = session.BeginProjectionPublication();
            PublishRestoreStaging(root);
            session.CompleteProjection(publication);

            Assert.That(session.Orders[0].orderId,
                Is.EqualTo("circus-order-committed"));
            Assert.That(releasedOrders, Is.EqualTo(1));
            Assert.That(transientClears, Is.EqualTo(1));
        }

        [Test]
        public void CapturedWildlifeProjectionTokenRollsBackToPreviousRoot()
        {
            DungeonRuntimeAggregateRootStore root =
                new DungeonRuntimeAggregateRootStore();
            CapturedWildlifeStateSession session =
                new CapturedWildlifeStateSession(root);
            session.Set(new CapturedWildlifeState
            {
                wildlifeId = "wildlife-original",
                penId = "pen-original"
            });
            session.PublishProjection(null);

            BeginRestoreStaging(root);
            session.Stage(CreateCandidate(
                "circus-order-candidate",
                "wildlife-candidate"));
            CapturedWildlifeProjectionPublication publication =
                session.BeginProjectionPublication();
            Assert.That(publication.Changed, Is.True);

            Assert.Throws<InvalidOperationException>(() =>
                throw new InvalidOperationException("late participant fault seam"));
            session.RollbackProjectionPublication(publication);
            DiscardRestoreStaging(root);

            CapturedWildlifeProjectionPublication unchanged =
                session.BeginProjectionPublication();
            Assert.That(unchanged.Changed, Is.False,
                "Rollback must restore the exact previous projection root.");
            session.CompleteProjectionPublication(unchanged);
            Assert.That(session.Count, Is.EqualTo(1));
            Assert.That(session.TryGet("wildlife-original", out _), Is.True);
            Assert.That(session.TryGet("wildlife-candidate", out _), Is.False);
        }

        [Test]
        public void CoordinatorExposesRollbackTokenBeforeFalliblePublication()
        {
            string source = ReadScript(
                "Services/Infrastructure/Save/CircusRestoreCoordinator.cs");
            string publish = ExtractMethod(
                source,
                "public void PublishRestoreCandidate()",
                "public void RollbackPublishedRestoreCandidate()");

            AssertInOrder(
                publish,
                "CircusRestorePublication publication = new();",
                "activePublication = publication;",
                "stateSession.BeginProjectionPublication();",
                "wildlifeCapture.BeginRestoreProjectionPublication();");
            Assert.That(publish, Does.Not.Contain("CompletePublish("));
        }

        [Test]
        public void CoordinatorRollsBackInReverseAndCompletesOnlyAfterCommit()
        {
            string source = ReadScript(
                "Services/Infrastructure/Save/CircusRestoreCoordinator.cs");
            string rollback = ExtractMethod(
                source,
                "public void RollbackPublishedRestoreCandidate()",
                "public void CompleteRestoreCandidate()");
            string complete = ExtractMethod(
                source,
                "public void CompleteRestoreCandidate()",
                "public void DiscardRestoreCandidate()");

            AssertInOrder(
                rollback,
                "wildlifeCapture.RollbackRestoreProjection(",
                "stateSession.RollbackProjection(",
                "activePublication = null;",
                "restoreTransaction.Discard();");
            AssertInOrder(
                complete,
                "stateSession.CompleteProjection(",
                "wildlifeCapture.CompleteRestoreProjection(",
                "activePublication = null;",
                "restoreTransaction.CompletePublish();");
        }

        [Test]
        public void WildlifeActorsCarryAndDoorProjectionChangeOnlyOnCompletion()
        {
            string source = ReadScript(
                "Services/Captivity/WildlifeCaptureRuntime.cs");
            string begin = ExtractMethod(
                source,
                "BeginRestoreProjectionPublication()",
                "public void RollbackRestoreProjection(");
            string finalize = ExtractMethod(
                source,
                "private void FinalizeProjectionBestEffort()",
                "private static");

            Assert.That(begin, Does.Not.Contain("EndManagedCarry("));
            Assert.That(begin, Does.Not.Contain("SetCaptured("));
            Assert.That(begin, Does.Not.Contain("WarpTo("));
            Assert.That(begin, Does.Not.Contain(
                "ReplaceCapturedWildlifeSubjects("));
            AssertInOrder(
                begin,
                "stateSession.BeginProjectionPublication();",
                "rollback: () =>",
                "stateSession.RollbackProjectionPublication(projection)",
                "complete: () =>",
                "FinalizeProjectionBestEffort();");
            Assert.That(finalize, Does.Contain("EndManagedCarry("));
            Assert.That(finalize, Does.Contain(
                "HashSet<string> activelyCapturedIds"));
            Assert.That(finalize, Does.Contain(
                "actor.State == WildlifeState.Captured"));
            Assert.That(finalize, Does.Contain("SetCaptured(false)"));
            Assert.That(finalize, Does.Contain(
                "!= shouldBeCaptured"));
            Assert.That(finalize, Does.Contain(
                "actor.SetCaptured(shouldBeCaptured);"));
            Assert.That(finalize, Does.Contain(
                "actor.GridPosition != target"));
            Assert.That(finalize, Does.Contain("WarpTo("));
            Assert.That(finalize, Does.Contain(
                "ReplaceCapturedWildlifeSubjects("));
        }

        private static CircusRestoreCandidate CreateCandidate(
            string orderId,
            string wildlifeId = null)
        {
            CircusSaveData payload = new CircusSaveData
            {
                version = CircusSaveData.CurrentVersion,
                orders = new System.Collections.Generic.List<CircusShowOrder>
                {
                    new CircusShowOrder
                    {
                        orderId = orderId,
                        stageId = "circus-stage-candidate"
                    }
                }
            };
            if (!string.IsNullOrWhiteSpace(wildlifeId))
            {
                payload.capturedWildlife.Add(new CapturedWildlifeState
                {
                    wildlifeId = wildlifeId,
                    speciesId = "species-test",
                    penId = "pen-candidate"
                });
            }
            return CircusRestoreCandidate.Create(payload);
        }

        private static void BeginRestoreStaging(
            DungeonRuntimeAggregateRootStore root) =>
            InvokeRootMethod(root, "BeginRestoreStaging");

        private static void PublishRestoreStaging(
            DungeonRuntimeAggregateRootStore root) =>
            InvokeRootMethod(root, "PublishRestoreStaging");

        private static void DiscardRestoreStaging(
            DungeonRuntimeAggregateRootStore root) =>
            InvokeRootMethod(root, "DiscardRestoreStaging");

        private static void InvokeRootMethod(
            DungeonRuntimeAggregateRootStore root,
            string methodName)
        {
            MethodInfo method = typeof(DungeonRuntimeAggregateRootStore)
                .GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing root method: {methodName}");
            method.Invoke(root, null);
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
                int index = source.IndexOf(
                    marker,
                    cursor + 1,
                    StringComparison.Ordinal);
                Assert.That(index, Is.GreaterThan(cursor),
                    $"Expected marker after index {cursor}: {marker}");
                cursor = index;
            }
        }
    }
}
