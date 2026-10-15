using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// Only the observer boundary understands the saved reference and canonical ledger.
// Compare detached owners; preflight must not deliver records or repeat a drink.
public sealed class CharacterWaterFactPreflight :
    IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonDarkSurvivalSaveData>(save, DarkSurvivalSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(DungeonDarkSurvivalSaveData survival, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            var pending = survival?.pendingWaterPublications
                ?? throw new InvalidOperationException("water-publications-missing");
            if (pending.Any(value => value?.fact == null) || incoming?.outbox == null
                || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("water-publication-save-shape-invalid");
            if (pending.Count == 0) return;
            var needed = new HashSet<string>(StringComparer.Ordinal);
            var characters = new HashSet<string>(StringComparer.Ordinal);
            foreach (var owner in pending)
            {
                if (!characters.Add(owner.fact.characterId))
                    throw new InvalidOperationException("water-publication-character-duplicate");
                foreach (string key in Keys(owner))
                    if (!needed.Add(key)) throw new InvalidOperationException("water-publication-key-duplicate");
            }
            var identities = new Dictionary<string, (GameplayOutcomeTombstoneSnapshot Row, bool Delivered)>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox) AddExact(row, false);
            foreach (var row in incoming.exactOutcomes) AddExact(row, true);
            foreach (var row in incoming.tombstones) Add(row, true);
            var descriptors = MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter()));
            var registry = new GameplayOutcomeRegistry(descriptors,
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 2, retainedLargePageCount: 2));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new CharacterWaterFactObserver(migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var owner in pending)
            {
                var fact = new CharacterWaterConsumedFact(owner.fact);
                var keys = Keys(owner);
                int present = keys.Count(identities.ContainsKey);
                if (present != 0 && present != keys.Length)
                    throw new InvalidOperationException("water-atomic-ledger-pair-torn");
                if (owner.notificationDispatched && present != keys.Length)
                    throw new InvalidOperationException("water-notification-without-committed-fact");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                foreach (string key in keys)
                    if (identities.TryGetValue(key, out var identity)) subset.tombstones.Add(identity.Row);
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                if (!observer.TryPrepareReplay(owner.publicationKey, fact, out var prepared, out string failure))
                    throw new InvalidOperationException("water-canonical-fact-conflict:" + failure);
                prepared.Cancel();
            }

            void AddExact(GameplayOutcomeSnapshot row, bool delivered)
            {
                if (row == null) return;
                Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision,
                    localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                }, delivered);
            }
            void Add(GameplayOutcomeTombstoneSnapshot row, bool delivered)
            {
                if (row == null) return;
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
                    row.commitRevision, row.localResultIndex).ToString();
                if (needed.Contains(key) && !identities.TryAdd(key, (row, delivered)))
                    throw new InvalidOperationException("water-publication-ledger-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("water-consumption-save-join:" + exception.Message); }
    }

    private static string[] Keys(CharacterWaterPublicationData owner)
    {
        bool pair = GameplayDomainFactPairProof.TryReadKeys(owner.publicationKey, out string first, out string second);
        if (pair != (owner.fact.quality != WorldWaterQuality.Clean))
            throw new InvalidOperationException("water-publication-reference-shape");
        var keys = pair ? new[] { first, second } : new[] { owner.publicationKey };
        if (keys.Any(key => !GameplayDomainFactDeliveryQuery.TryReadKey(key, out _)))
            throw new InvalidOperationException("water-publication-reference-invalid");
        return keys;
    }
}
