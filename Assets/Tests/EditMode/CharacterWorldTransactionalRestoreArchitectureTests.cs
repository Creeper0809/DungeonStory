using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    public sealed class CharacterWorldTransactionalRestoreArchitectureTests
    {
        [Test]
        public void PrepareBuildsDetachedPopulationAndReputationCandidatesOnly()
        {
            string source = ReadScript(
                "Services/Infrastructure/CharacterWorldSaveService.cs");
            string identityResolver = ReadScript(
                "Services/Infrastructure/CharacterV18RestoreIdentityResolver.cs");
            string aggregatePreflight = ReadScript(
                "Services/Infrastructure/Save/DungeonAggregateReferencePreflight.cs");
            string physicalValidation = ReadScript(
                "Services/Infrastructure/Save/DungeonPhysicalSaveReferenceValidation.cs");
            string referenceRules = ReadScript(
                "Services/Infrastructure/Save/DungeonSaveReferenceRules.cs");
            string tryResolve = ExtractMethod(
                identityResolver,
                "public static bool TryResolve(",
                "public static CharacterId RequireCharacterId(");
            string validateCharacters = ExtractMethod(
                physicalValidation,
                "internal DungeonSaveCharacterReferenceIndex ValidateCharacters(",
                "internal DungeonSaveBuildingReferenceIndex ValidateBuildings(");
            int requireUniqueCharacterIdStart = referenceRules.IndexOf(
                "internal static bool RequireUniqueCharacterId(",
                StringComparison.Ordinal);
            Assert.That(requireUniqueCharacterIdStart, Is.GreaterThanOrEqualTo(0),
                "Missing delegated canonical character identity validator.");
            string requireUniqueCharacterId = referenceRules.Substring(
                requireUniqueCharacterIdStart);
            string prepare = ExtractMethod(
                source,
                "public CharacterWorldRestoreCandidate PrepareRestoreCandidate(",
                "public void StageRestoreCandidate(");

            AssertInOrder(
                prepare,
                "characterPopulationService.BuildRestoreCandidate(",
                "socialReputation.BuildRestoreCandidate(",
                "new CharacterWorldRestoreCandidate(");
            Assert.That(prepare, Does.Not.Contain("ApplyRestoreCandidate("));
            Assert.That(prepare, Does.Not.Contain("PublishDetachedInactive("));
            Assert.That(prepare, Does.Not.Contain("BeginRestoreCandidatePublication("));
            Assert.That(prepare, Does.Not.Contain("restoredActorsById ="));
            Assert.That(prepare, Does.Contain(
                "CharacterV18RestoreIdentityResolver.BuildCanonicalActorIds("));
            Assert.That(identityResolver, Does.Contain(
                "CharacterId.TryCanonicalizeV18Restore("));
            Assert.That(identityResolver, Does.Contain(
                "public static void AddCandidate("));
            AssertInOrder(
                tryResolve,
                "direct.IsValid",
                "direct.Value,",
                "value,",
                "StringComparison.Ordinal");
            Assert.That(identityResolver, Does.Not.Contain(
                "string normalized = persistentId.Trim();"));
            AssertInOrder(
                aggregatePreflight,
                "physicalSaveReference = new DungeonPhysicalSaveReferenceValidation(",
                "physicalSaveReference.ValidateCharacters(characters, report);");
            AssertInOrder(
                validateCharacters,
                "foreach (DungeonCharacterSaveData actor in source?.actors",
                "if (RequireUniqueCharacterId(",
                "actor.persistentId,",
                "result.KnownCharacterIds,",
                "rejectDuplicate: true,",
                "result.ActorIds.Add(actorId);",
                "foreach (WorldCharacterProfile profile in source?.populationProfiles",
                "if (RequireUniqueCharacterId(",
                "profile?.persistentId,",
                "result.KnownCharacterIds,",
                "rejectDuplicate: false,",
                "result.LivingCharacterIds.Add(profileId);");
            AssertInOrder(
                requireUniqueCharacterId,
                "CharacterV18RestoreIdentityResolver.TryResolve(",
                "allowLegacyCharacterIds: false,",
                "string.Equals(",
                "rawId,",
                "characterId.Value,",
                "StringComparison.Ordinal",
                "canonicalId = characterId.Value;",
                "if (!ids.Add(canonicalId) && rejectDuplicate)",
                "return false;",
                "return true;");
            Assert.That(requireUniqueCharacterId, Does.Not.Contain(".Trim()"));
        }

        [Test]
        public void PublishAppliesDomainRootsBeforeInactiveActorAndPointerPublication()
        {
            string source = ReadScript(
                "Services/Infrastructure/CharacterWorldSaveService.cs");
            string publish = ExtractMethod(
                source,
                "private void PublishCharacterCandidate(",
                "private void RollbackCharacterPublication(");

            AssertInOrder(
                publish,
                "characterPopulationService.ApplyRestoreCandidate(",
                "socialReputation.ApplyRestoreCandidate(",
                "characterObjectFactory.PublishDetachedInactive(",
                "BeginRestoreCandidatePublication(",
                "characterObjectFactory.ValidateDetachedPublication(",
                "restoredActorsById = candidate.ActorsById;",
                "publication.ActorIndexPublished = true;");
            Assert.That(publish, Does.Not.Contain("CompleteDetachedPublication("));
            Assert.That(publish, Does.Not.Contain("CompleteRestoreCandidatePublication("));
            Assert.That(publish, Does.Not.Contain("CompleteRestore("));
            Assert.That(publish, Does.Not.Contain("SetActive(true)"));
            Assert.That(publish, Does.Not.Contain(".Destroy("));
        }

        [Test]
        public void PublishExposesRollbackTokenBeforeAnyFalliblePublicationWork()
        {
            string source = ReadScript(
                "Services/Infrastructure/CharacterWorldSaveService.cs");
            string publish = ExtractMethod(
                source,
                "public void PublishRestoreCandidate()",
                "public void RollbackPublishedRestoreCandidate()");

            AssertInOrder(
                publish,
                "new CharacterWorldPublication(",
                "activePublication = publication;",
                "PublishCharacterCandidate(publication);");
        }

        [Test]
        public void RollbackRestoresPointerOwnerAndStaffThenSocialBeforePopulation()
        {
            string source = ReadScript(
                "Services/Infrastructure/CharacterWorldSaveService.cs");
            string rollback = ExtractMethod(
                source,
                "private void RollbackCharacterPublication(",
                "private void CompleteCharacterPublication(");

            AssertInOrder(
                rollback,
                "if (publication.ActorIndexPublished)",
                "restoredActorsById = publication.PreviousActorIndex;",
                "RollbackRestoreCandidatePublication(",
                "characterObjectFactory.RollbackDetachedPublication(",
                "socialReputation.RollbackRestore(",
                "characterPopulationService.RollbackRestore(");
            Assert.That(rollback, Does.Contain(
                "for (int index = publication.PublishedStaff.Count - 1;"));
        }

        [Test]
        public void ActorIndexUsesExactPreviousRootForRollback()
        {
            string source = ReadScript(
                "Services/Infrastructure/CharacterWorldSaveService.cs");
            string publishEntry = ExtractMethod(
                source,
                "public void PublishRestoreCandidate()",
                "public void RollbackPublishedRestoreCandidate()");
            string publicationType = source.Substring(source.IndexOf(
                "private sealed class CharacterWorldPublication",
                StringComparison.Ordinal));

            AssertInOrder(
                publishEntry,
                "new CharacterWorldPublication(",
                "stagedCandidate,",
                "restoredActorsById,",
                "restoredLegacyActorIds);");
            Assert.That(publicationType, Does.Contain(
                "PreviousActorIndex = previousActorIndex"));
            Assert.That(publicationType, Does.Contain(
                "PreviousLegacyActorIds = previousLegacyActorIds"));
            Assert.That(publicationType, Does.Not.Contain(
                "new Dictionary<string, CharacterActor>(previousActorIndex"));
            Assert.That(publicationType, Does.Not.Contain(
                "new Dictionary<string, string>(previousLegacyActorIds"));
        }

        [Test]
        public void CompletionActivatesCandidatesAndRetiresOldWorldWithoutChangingSavedPopulation()
        {
            string source = ReadScript(
                "Services/Infrastructure/CharacterWorldSaveService.cs");
            string prepare = ExtractMethod(
                source,
                "public CharacterWorldRestoreCandidate PrepareRestoreCandidate(",
                "public void StageRestoreCandidate(");
            string publish = ExtractMethod(
                source,
                "private void PublishCharacterCandidate(",
                "private void RollbackCharacterPublication(");
            string rollback = ExtractMethod(
                source,
                "private void RollbackCharacterPublication(",
                "private void CompleteCharacterPublication(");
            string complete = ExtractMethod(
                source,
                "private void CompleteCharacterPublication(",
                "private static void PrepareRestoredStaffComposition(");

            AssertInOrder(
                complete,
                "socialReputation.CompleteRestore(",
                "characterPopulationService.CompleteRestore(",
                "candidate.ExistingStaff.Concat(",
                "publication.OwnerPublication?.PreviousOwner",
                "PrepareForWorldRetirement(",
                "persistentRetiringActors,",
                "candidate.ActorsById);",
                "oldStaff.gameObject.SetActive(false);",
                "characterObjectFactory.CompleteDetachedPublication(",
                "characterObjectFactory.Destroy(oldStaff.gameObject);",
                "CompleteRestoreCandidatePublication(");
            Assert.That(complete, Does.Not.Contain(
                "characterWorldQuery.Characters"));
            Assert.That(complete, Does.Not.Contain(
                "ReplenishPreparedPoolBestEffort("));

            string completionOnlyOperations = prepare + publish + rollback;
            Assert.That(completionOnlyOperations, Does.Not.Contain(
                "CompleteDetachedPublication("));
            Assert.That(completionOnlyOperations, Does.Not.Contain(
                "CompleteRestoreCandidatePublication("));
            Assert.That(completionOnlyOperations, Does.Not.Contain(
                "EnsurePreparedPool("));
        }

        [Test]
        public void OwnerPublicationTokenIsReversibleUntilCompletion()
        {
            string source = ReadScript(
                "Services/Character/Core/OwnerRunManager.cs");
            string begin = ExtractMethod(
                source,
                "public OwnerRestorePublication BeginRestoreCandidatePublication(",
                "public void RollbackRestoreCandidatePublication(");
            string rollback = ExtractMethod(
                source,
                "private void RollbackRestoreCandidatePublicationCore(",
                "private static void DestroyOwnerObject(");
            string complete = ExtractMethod(
                source,
                "public void CompleteRestoreCandidatePublication(",
                "public void HandleOwnerDeath(");

            AssertInOrder(
                begin,
                "new OwnerRestorePublication(",
                "pendingRestorePublication = publication;",
                "currentOwnerActor = candidate;",
                "candidate.PublishDetachedRestore();");
            Assert.That(begin, Does.Contain(
                "RollbackRestoreCandidatePublicationCore(publication);"));
            AssertInOrder(
                rollback,
                "candidate.RollbackDetachedRestorePublication();",
                "candidate.transform.SetParent(",
                "publication.PreviousCandidateParent",
                "currentOwnerActor = publication.PreviousOwner;",
                "selectedOwnerData = publication.PreviousSelectionContainer;",
                "IsRunEnded = publication.PreviousRunEnded;",
                "publication.MarkRolledBack();");
            AssertInOrder(
                complete,
                "previousOwner.gameObject.SetActive(false);",
                "publication.Candidate.gameObject.SetActive(true);",
                "publication.MarkCompleted();",
                "DestroyOwnerObject(previousOwner.gameObject);",
                "OnOwnerSelected?.Invoke(publication.OwnerData);");
        }

        private static string ReadScript(string relativePath)
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts",
                relativePath));
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
    }
}
