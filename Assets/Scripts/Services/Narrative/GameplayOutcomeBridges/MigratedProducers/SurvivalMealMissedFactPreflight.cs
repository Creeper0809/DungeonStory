using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

// Native identity and canonical payload checks belong to the observer, not survival.
public sealed class SurvivalMealMissedFactPreflight :
    IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(
                DungeonSaveSectionPayload.ReadOrNew<DungeonSurvivalSaveData>(save, SurvivalResourcesSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("survival-meal-missed-save-join:" + exception.Message); }
    }

    public static void Validate(DungeonSurvivalSaveData survival, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (survival?.missedMealCohorts == null || incoming?.outbox == null
            || incoming.exactOutcomes == null || incoming.tombstones == null || counters == null
            || survival.missedMealCohorts.Count > SurvivalMealMissedCohortSaveData.MaximumCohorts)
            throw new InvalidOperationException("meal-missed-save-state-missing");
        string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.MealMissedEvent).ProducerId;
        var identities = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        foreach (var row in incoming.tombstones) if (row != null && row.producerId == producer) Add(row);
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            if (row != null && row.producerId == producer) Add(new GameplayOutcomeTombstoneSnapshot
            {
                runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                operationId = row.operationId, commitRevision = row.commitRevision,
                localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
                parentRunId = row.parentRunId, parentSequence = row.parentSequence,
                deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
                recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
                deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
                canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
            });
        var registry = new GameplayOutcomeRegistry(
            MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        int capacity = Math.Max(1, survival.missedMealCohorts.Select(row => row?.members?.Count ?? 0).DefaultIfEmpty().Max());
        if (capacity > GameplayOutcomeTransactionLimits.MaximumAtomicCommitBatchCount)
            throw new InvalidOperationException("meal-missed-cohort-exceeds-atomic-boundary");
        // One reusable cohort comparison authority, not a full-world ledger per member.
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: capacity, largePageCount: 0, knownResultKeyCapacity: capacity,
            retainedSmallPageCount: capacity, retainedLargePageCount: 0));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        migrated.Restore(migrated.PrepareRestore(counters));
        var observer = new MigratedDomainFactObserver<SurvivalMealMissedFact>(
            SurvivalMealMissedFactMapping.Capture, migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        var joined = new HashSet<string>(StringComparer.Ordinal);
        int previousDay = 0;
        foreach (var cohort in survival.missedMealCohorts)
        {
            if (cohort == null) throw new InvalidOperationException("meal-missed-cohort-null");
            cohort.Validate();
            if (cohort.absoluteDay <= previousDay || cohort.absoluteDay > survival.lastProcessedDay)
                throw new InvalidOperationException("meal-missed-cohort-day-conflict");
            previousDay = cohort.absoluteDay;
            var selected = new List<GameplayOutcomeTombstoneSnapshot>();
            foreach (var member in cohort.members)
            {
                var capture = SurvivalMealMissedFactMapping.Capture(new SurvivalMealMissedFact(member.fact));
                string expected = MigratedProducerOutcomeCatalog.ResultKey(capture.Kind, capture.OwnerIdentity, 1L).ToString();
                if (member.publicationKey != expected || !joined.Add(expected))
                    throw new InvalidOperationException("meal-missed-publication-key-conflict");
                if (!identities.TryGetValue(expected, out var identity)) continue;
                if (selected.Count > 0 && (identity.runId != selected[0].runId
                    || identity.sequence != selected[0].sequence + selected.Count))
                    throw new InvalidOperationException("meal-missed-cohort-sequence-conflict");
                selected.Add(identity);
            }
            if (selected.Count != 0 && selected.Count != cohort.members.Count
                || cohort.publicationConfirmed && selected.Count != cohort.members.Count)
                throw new InvalidOperationException("meal-missed-partial-cohort");
            var subset = new GameplayOutcomeLedgerSaveData
            {
                runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
                nextSequence = incoming.nextSequence, knownResultKeyHighWater = selected.Count, tombstones = selected
            };
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            foreach (var member in cohort.members)
            {
                bool accepted = observer.TryPrepareReplay(member.publicationKey, new SurvivalMealMissedFact(member.fact),
                    out IDomainFactPreparedPublication prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("meal-missed-canonical-conflict:" + failure);
            }
        }
        if (identities.Keys.Any(key => !joined.Contains(key)))
            throw new InvalidOperationException("meal-missed-lost-owner-journal");

        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
                row.commitRevision, row.localResultIndex).ToString();
            if (!identities.TryAdd(key, row)) throw new InvalidOperationException("meal-missed-publication-duplicate");
        }
    }
}
