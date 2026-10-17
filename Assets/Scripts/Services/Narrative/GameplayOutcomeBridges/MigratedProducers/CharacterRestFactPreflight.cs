using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

public sealed class CharacterRestFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(save, CharacterWorldSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(DungeonCharacterWorldSaveData characters, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            if (characters?.actors == null || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("rest-save-shape-invalid");
            var actors = characters.actors.Where(value => value?.restObservation != null).ToArray();
            if (actors.Length == 0) return;
            if (actors.Select(value => value.persistentId).Distinct(StringComparer.Ordinal).Count() != actors.Length
                || actors.Select(value => value.restObservation.publicationKey).Distinct(StringComparer.Ordinal).Count() != actors.Length)
                throw new InvalidOperationException("rest-save-owner-or-publication-duplicate");
            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                    (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 2, retainedLargePageCount: 2));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new MigratedDomainFactObserver<CharacterRestAppliedFact>(CharacterRestFactMapping.Capture,
                migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var actor in actors)
            {
                var row = actor.restObservation;
                row.Validate(actor.persistentId);
                if (!GameplayDomainFactDeliveryQuery.TryReadKey(row.publicationKey, out GameplayResultKey childKey)
                    || childKey.CommitRevision <= 0 || row.publicationKey != MigratedProducerOutcomeCatalog.ResultKey(
                        MigratedProducerOutcomeKind.RestOutcomeIdentityEvent, "rest-outcome:" + actor.persistentId,
                        childKey.CommitRevision).ToString())
                    throw new InvalidOperationException("rest-save-publication-owner-conflict");
                var found = incoming.tombstones.Where(value => value != null && Key(value.producerId,
                    value.operationId, value.commitRevision, value.localResultIndex) == row.publicationKey).ToList();
                foreach (var value in incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
                    && Key(value.producerId, value.operationId, value.commitRevision, value.localResultIndex) == row.publicationKey))
                    found.Add(new GameplayOutcomeTombstoneSnapshot
                    {
                        runId = value.runId, sequence = value.sequence, producerId = value.producerId,
                        operationId = value.operationId, commitRevision = value.commitRevision,
                        localResultIndex = value.localResultIndex, ownerRevision = value.ownerRevision,
                        temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
                        absoluteDay = value.absoluteDay, parentRunId = value.parentRunId, parentSequence = value.parentSequence,
                        deliveryMetadataVersion = value.deliveryMetadataVersion, deliveryClockId = value.deliveryClockId,
                        recordedAbsoluteHour = value.recordedAbsoluteHour, publishedRevision = value.publishedRevision,
                        deliveryFaultCount = value.deliveryFaultCount, deliveryDelayReason = value.deliveryDelayReason,
                        canonicalHash = value.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                    });
                if (found.Count > 1 || row.observationCommitted && found.Count != 1 || row.rollbackBlocked && found.Count != 0)
                    throw new InvalidOperationException("rest-save-canonical-phase-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence,
                    knownResultKeyHighWater = found.Count, tombstones = found };
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                bool accepted = observer.TryPrepareReplay(row.publicationKey, new CharacterRestAppliedFact(row.fact),
                    out IDomainFactPreparedPublication publication, out string failure);
                publication?.Cancel();
                if (!accepted) throw new InvalidOperationException("rest-save-canonical-payload-conflict:" + failure);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("rest-observation-save-join:" + exception.Message); }
    }
    private static string Key(string producer, string operation, long revision, int index) =>
        new GameplayResultKey(producer, new GameplayOperationId(operation), revision, index).ToString();
}
