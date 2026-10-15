using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

public sealed class PrimitiveSurvivalFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(DungeonSaveSectionPayload.ReadOrNew<DungeonDarkSurvivalSaveData>(save, DarkSurvivalSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("primitive-survival-save-join:" + exception.Message); }
    }

    public static void Validate(DungeonDarkSurvivalSaveData survival, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (survival?.characters == null || survival.pendingPrimitivePublications == null
            || incoming?.outbox == null || incoming.exactOutcomes == null
            || incoming.tombstones == null || counters == null)
            throw new InvalidOperationException("primitive-survival-save-state-missing");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterDeprivationState character in survival.characters)
        {
            if (character == null) throw new InvalidOperationException("primitive-survival-character-missing");
            PrimitiveSurvivalCompletedFact.ValidateSaved(character.primitiveSurvivalPending, character.characterId);
        }
        foreach (PrimitiveSurvivalPendingSaveData owner in survival.characters.Select(character => character.primitiveSurvivalPending)
            .Concat(survival.pendingPrimitivePublications))
        {
            PrimitiveSurvivalCompletedFact.ValidateSaved(owner, owner?.characterId);
            if (!owner.IsPending) continue;
            if (!keys.Add(owner.publicationKey) || !operations.Add(owner.operationId)
                || !GameplayDomainFactDeliveryQuery.TryReadKey(owner.publicationKey, out GameplayResultKey key)
                || key.CommitRevision <= 0
                || owner.publicationKey != MigratedProducerOutcomeCatalog.ResultKey(
                    MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
                    "primitive-survival:" + owner.characterId, key.CommitRevision).ToString())
                throw new InvalidOperationException("primitive-survival-publication-key-conflict");
            ValidateRestChild(owner, incoming, counters);
            var found = incoming.tombstones.Where(row => row != null
                && Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex) == owner.publicationKey).ToList();
            foreach (GameplayOutcomeSnapshot row in incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null
                && Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex) == owner.publicationKey))
                found.Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision,
                    localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
                    absoluteDay = row.absoluteDay, parentRunId = row.parentRunId, parentSequence = row.parentSequence,
                    deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
                    recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
                    deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
            if (found.Count > 1 || owner.rollbackBlocked && found.Count != 0)
                throw new InvalidOperationException("primitive-survival-publication-phase-conflict");
            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                    (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 2, retainedLargePageCount: 2));
            var subset = new GameplayOutcomeLedgerSaveData
            {
                runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
                nextSequence = incoming.nextSequence, knownResultKeyHighWater = found.Count, tombstones = found
            };
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            migrated.Restore(migrated.PrepareRestore(counters));
            var observer = new MigratedDomainFactObserver<PrimitiveSurvivalCompletedFact>(PrimitiveSurvivalCompletedFactMapping.Capture,
                migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            bool accepted = observer.TryPrepareReplay(owner.publicationKey, new PrimitiveSurvivalCompletedFact(owner),
                out IDomainFactPreparedPublication publication, out string failure);
            publication?.Cancel();
            if (!accepted) throw new InvalidOperationException("primitive-survival-payload-conflict:" + failure);
        }
    }

    private static void ValidateRestChild(PrimitiveSurvivalPendingSaveData owner,
        GameplayOutcomeLedgerSaveData incoming, DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (owner.actionId != "survival:floor-rest") return;
        if (!GameplayDomainFactDeliveryQuery.TryReadKey(owner.restPublicationKey, out GameplayResultKey key)
            || key.CommitRevision <= 0 || owner.restPublicationKey != MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.RestOutcomeIdentityEvent, "rest-outcome:" + owner.characterId,
                key.CommitRevision).ToString())
            throw new InvalidOperationException("primitive-survival-rest-owner-conflict");
        var rest = owner.GetRestObservation();
        if (rest == null)
        {
            int count = incoming.tombstones.Count(row => row != null
                && Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex) == owner.restPublicationKey)
                + incoming.outbox.Concat(incoming.exactOutcomes).Count(row => row != null
                    && Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex) == owner.restPublicationKey);
            if (count != 1) throw new InvalidOperationException("primitive-survival-confirmed-rest-child-missing");
            return;
        }
        var report = new DungeonGameRestoreReport();
        CharacterRestFactPreflight.Validate(new DungeonCharacterWorldSaveData
        {
            actors = new List<DungeonCharacterSaveData> { new()
                { persistentId = owner.characterId, restObservation = rest.Clone() } }
        }, incoming, counters, report);
        if (!report.Success)
            throw new InvalidOperationException("primitive-survival-rest-child:" + string.Join(";", report.Errors));
    }

    private static string Key(string producer, string operation, long revision, int index) =>
        new GameplayResultKey(producer, new GameplayOperationId(operation), revision, index).ToString();
}
