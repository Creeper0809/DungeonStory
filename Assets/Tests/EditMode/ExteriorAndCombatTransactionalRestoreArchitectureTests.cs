using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    public sealed class ExteriorAndCombatTransactionalRestoreArchitectureTests
    {
        [Test]
        public void ExteriorPublishSwapsOnlyReversibleRoots()
        {
            string source = ReadScript(
                "Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs");
            string publish = Extract(
                source,
                "public void PublishRestoreCandidate()",
                "public void RollbackPublishedRestoreCandidate()");

            AssertInOrder(
                publish,
                "activePublication = publication;",
                "zones = candidate.Zones;",
                "zonesView = zones.AsReadOnly();",
                "incidentAggregate = restoredIncidents;");
            Assert.That(publish, Does.Not.Contain("RetireForWorldReplacement("));
            Assert.That(publish, Does.Not.Contain("PublishDetachedRestore("));
            Assert.That(publish, Does.Not.Contain("SetActive(true)"));
        }

        [Test]
        public void ExteriorRollbackRestoresExactPreviousRootsBeforeDiscard()
        {
            string source = ReadScript(
                "Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs");
            string rollback = Extract(
                source,
                "public void RollbackPublishedRestoreCandidate()",
                "public void CompleteRestoreCandidate()");

            AssertInOrder(
                rollback,
                "zones = publication.PreviousZones;",
                "zonesView = publication.PreviousZonesView;",
                "incidentAggregate = publication.PreviousIncidents;",
                "incidentSequence = publication.PreviousIncidentSequence;",
                "restoreCoordinator.RollbackPublished();");
        }

        [Test]
        public void ExteriorCompleteRetiresOldWorldOnlyAfterSuccess()
        {
            string source = ReadScript(
                "Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs");
            string complete = Extract(
                source,
                "public void CompleteRestoreCandidate()",
                "public void DiscardRestoreCandidate()");

            AssertInOrder(
                complete,
                "oldZone.gameObject.SetActive(false);",
                "zone.PublishDetachedRestore();",
                "zone.gameObject.SetActive(true);",
                "oldZone.RetireForWorldReplacement();",
                "restoreCoordinator.CompletePublished();");
        }

        [Test]
        public void CombatProjectionChangesLivePresentationOnlyOnCompletion()
        {
            string source = ReadScript(
                "Services/Combat/CharacterCombatCommandRestoreCoordinator.cs");
            string publish = Extract(
                source,
                "internal void PublishRestoreCandidate()",
                "internal void RollbackPublishedRestoreCandidate()");
            string rollback = Extract(
                source,
                "internal void RollbackPublishedRestoreCandidate()",
                "internal void CompleteRestoreCandidate()");
            string complete = Extract(
                source,
                "internal void CompleteRestoreCandidate()",
                "internal void DiscardRestoreCandidate()");
            string verifier = ReadScript(
                "Services/Combat/Editor/CombatV14PlayModeVerifier.cs");

            AssertInOrder(
                publish,
                "publicationPendingCompletion = true;",
                "restoreCandidateReady = false;",
                "restoreTransactionActive = false;");
            Assert.That(publish, Does.Not.Contain("SetAiPaused("));
            Assert.That(publish, Does.Not.Contain("SetStatus("));
            Assert.That(publish, Does.Not.Contain("published();"));
            AssertInOrder(
                rollback,
                "publicationPendingCompletion = false;",
                "restoreCandidateReady = false;",
                "restoreTransactionActive = false;");
            Assert.That(rollback, Does.Not.Contain("SetAiPaused("));
            Assert.That(rollback, Does.Not.Contain("SetStatus("));
            AssertInOrder(
                complete,
                "aggregateRootStore.GetOrCreate(",
                "actor.SetAiPaused(inCombatStance);",
                "SetStatus(",
                "publicationPendingCompletion = false;",
                "published();");
            Assert.That(verifier, Does.Contain(
                "VerifyCombatCommandLateParticipantRollbackAndComplete();"));
        }

        [Test]
        public void ParticipantCompletionUsesReverseDependencyOrder()
        {
            string source = ReadScript(
                "Services/Foundation/Save/DungeonSaveSections.cs");
            string complete = Extract(
                source,
                "private bool TryCompleteTransactionParticipants(",
                "private void RollbackTransactionParticipants(");

            Assert.That(complete, Does.Contain(
                "for (int index = transactionParticipants.Count - 1;"));
            Assert.That(complete, Does.Contain("index--"));
            Assert.That(complete, Does.Contain(
                "transactionParticipants[index]"));
        }

        private static string ReadScript(string relativePath) =>
            File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts",
                relativePath));

        private static string Extract(
            string source,
            string startMarker,
            string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
            int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endMarker);
            return source.Substring(start, end - start);
        }

        private static void AssertInOrder(string source, params string[] markers)
        {
            int cursor = -1;
            foreach (string marker in markers)
            {
                int next = source.IndexOf(
                    marker,
                    cursor + 1,
                    StringComparison.Ordinal);
                Assert.That(next, Is.GreaterThan(cursor), marker);
                cursor = next;
            }
        }
    }
}
