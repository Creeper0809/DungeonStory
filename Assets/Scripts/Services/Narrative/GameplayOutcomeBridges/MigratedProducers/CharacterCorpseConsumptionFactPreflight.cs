using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

// Saved gameplay facts retain no knowledge of ledger identities. This observer-side
// join validates both atomic members before any live aggregate is changed.
public sealed class CharacterCorpseConsumptionFactPreflight :
    IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonDarkSurvivalSaveData>(save, DarkSurvivalSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(save, PhysicalItemsSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(DungeonDarkSurvivalSaveData survival, DungeonPhysicalItemSaveData physical,
        GameplayOutcomeLedgerSaveData incoming, DungeonMigratedProducerOutcomeSaveData counters,
        DungeonGameRestoreReport report)
    {
        try
        {
            var pending = survival?.pendingCorpsePublications
                ?? throw new InvalidOperationException("corpse-publications-missing");
            if (pending.Any(value => value?.fact?.physical == null || value.fact.experience == null)
                || physical?.pendingBatchDispositions == null || physical.pendingBatchDispositions.Any(value => value == null)
                || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("corpse-publication-save-shape-invalid");
            var byOperation = pending.ToDictionary(value => value.fact.physical.operationId, StringComparer.Ordinal);
            var definition = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent);
            string physicalProducer = TradeInventoryOutcomeDefinitions.Get(TradeInventoryOutcomeKind.PhysicalItemBatchDisposition).ProducerId;
            // A saved physical debit cannot lose its experience journal before acknowledgement.
            foreach (var debit in physical.pendingBatchDispositions)
            {
                if (!GameplayDomainFactPairProof.TryReadKeys(debit.publicationKey, out _, out string second)
                    || !GameplayDomainFactDeliveryQuery.TryReadKey(second, out var key)
                    || key.ProducerId != definition.ProducerId) continue;
                if (!byOperation.TryGetValue(debit.operationId, out var owner)
                    || owner.publicationKey != debit.publicationKey)
                    throw new InvalidOperationException("corpse-debit-experience-journal-missing");
            }
            if (pending.Count == 0) return;
            var needed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var owner in pending)
            {
                if (!GameplayDomainFactPairProof.TryReadKeys(owner.publicationKey, out string first, out string second)
                    || !needed.Add(first) || !needed.Add(second))
                    throw new InvalidOperationException("corpse-publication-key-invalid-or-duplicate");
            }
            var identities = new Dictionary<string, (GameplayOutcomeTombstoneSnapshot Row, bool Delivered)>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox) AddExact(row, false);
            foreach (var row in incoming.exactOutcomes) AddExact(row, true);
            foreach (var row in incoming.tombstones) Add(row, true);
            var descriptors = MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter()))
                .Append(new TradeInventoryOutcomeDescriptor(new KoreanJosaFormatter()));
            var registry = new GameplayOutcomeRegistry(descriptors,
                new IGameplayOutcomeAdapterRegistration[] { new TradeInventoryOutcomeAdapter(), new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 2, retainedLargePageCount: 2));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new CharacterCorpseConsumptionFactObserver(registry, ledger, recorder, migrated,
                new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var owner in pending)
            {
                var fact = CharacterCorpseConsumedFact.FromSave(owner.fact);
                GameplayDomainFactPairProof.TryReadKeys(owner.publicationKey, out string firstKey, out string secondKey);
                bool firstExists = identities.TryGetValue(firstKey, out var first);
                bool secondExists = identities.TryGetValue(secondKey, out var second);
                if (firstExists != secondExists) throw new InvalidOperationException("corpse-atomic-ledger-pair-torn");
                var debit = physical.pendingBatchDispositions.SingleOrDefault(
                    row => row.operationId == fact.Physical.Receipt.OperationId);
                if (debit == null)
                {
                    if (!firstExists || !first.Delivered || !second.Delivered)
                        throw new InvalidOperationException("corpse-applied-debit-journal-missing");
                }
                else
                {
                    if (!debit.publicationExpected || debit.defaultObservationDay != 0
                        || debit.publicationKey != owner.publicationKey)
                        throw new InvalidOperationException("corpse-debit-publication-conflict");
                    var normalized = new CharacterCorpseConsumptionFactData { physical = debit }.Clone().physical;
                    normalized.publicationExpected = false;
                    normalized.publicationKey = normalized.publicationId = normalized.publicationFingerprint = string.Empty;
                    normalized.publicationDelivered = false;
                    normalized.defaultObservationDay = fact.Experience.absoluteDay;
                    if (!CharacterCorpseConsumedFact.PhysicalEquals(owner.fact.physical, normalized))
                        throw new InvalidOperationException("corpse-frozen-debit-conflict");
                    if (!firstExists && (!string.IsNullOrEmpty(debit.publicationId)
                        || !string.IsNullOrEmpty(debit.publicationFingerprint) || debit.publicationDelivered))
                        throw new InvalidOperationException("corpse-publication-proof-without-ledger");
                    if (firstExists)
                    {
                        var proof = GameplayDomainFactPairProof.Combine(owner.publicationKey,
                            Delivery(firstKey, first), Delivery(secondKey, second));
                        if ((!string.IsNullOrEmpty(debit.publicationId) && debit.publicationId != proof.PublicationId)
                            || (!string.IsNullOrEmpty(debit.publicationFingerprint) && debit.publicationFingerprint != proof.Fingerprint)
                            || (debit.publicationDelivered && !proof.Completed))
                            throw new InvalidOperationException("corpse-publication-proof-conflict");
                    }
                }
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (firstExists)
                {
                    subset.tombstones.Add(first.Row); subset.tombstones.Add(second.Row);
                    subset.knownResultKeyHighWater = 2;
                }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                if (!observer.TryPrepareReplay(owner.publicationKey, fact, out var prepared, out string failure))
                    throw new InvalidOperationException("corpse-canonical-fact-conflict:" + failure);
                prepared.Cancel(); // Detached comparison only: never publish or apply effects during preflight.
            }

            void AddExact(GameplayOutcomeSnapshot row, bool delivered)
            {
                if (row == null || row.producerId != definition.ProducerId && row.producerId != physicalProducer) return;
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
                    throw new InvalidOperationException("corpse-publication-ledger-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("corpse-consumption-save-join:" + exception.Message); }
    }

    private static DomainFactDelivery Delivery(string key, (GameplayOutcomeTombstoneSnapshot Row, bool Delivered) value) =>
        new(value.Delivered ? DomainFactDeliveryPhase.Delivered : DomainFactDeliveryPhase.AcceptedPendingDelivery,
            key, new GameplayOutcomeId(new GameplayOutcomeRunId(value.Row.runId), value.Row.sequence).ToString(),
            value.Row.canonicalHash, string.Empty);
}
