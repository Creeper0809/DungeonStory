using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>The observer, not the item owner, joins opaque saved publications to its ledger.</summary>
public sealed class PhysicalItemDispositionFactPreflight :
    IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData saveData, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(saveData, PhysicalItemsSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(saveData, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(DungeonPhysicalItemSaveData physical,
        GameplayOutcomeLedgerSaveData incoming, DungeonGameRestoreReport report)
    {
        try
        {
            if (!physical.pendingBatchDispositions.Any(value => value?.defaultObservationDay > 0)) return;
            var producer = TradeInventoryOutcomeDefinitions.Get(TradeInventoryOutcomeKind.PhysicalItemBatchDisposition).ProducerId;
            var identities = new Dictionary<string, (GameplayOutcomeTombstoneSnapshot Row, bool Delivered)>(StringComparer.Ordinal);
            AddExact(incoming.outbox, false);
            AddExact(incoming.exactOutcomes, true);
            foreach (var value in incoming.tombstones)
                if (value != null && value.producerId == producer) Add(value, true);

            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new TradeInventoryOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new TradeInventoryOutcomeAdapter() });
            // Bounded comparison buffers reused for the join; multi-stack receipts may need a large page.
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 2,
                retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var joined = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in physical.pendingBatchDispositions)
            {
                if (saved == null || saved.defaultObservationDay == 0) continue;
                if (!saved.publicationExpected) throw new InvalidOperationException("disposition-publication-not-expected");
                var receipt = PhysicalItemDispositionFactObservation.Capture(PhysicalItemDisposedFact.FromSave(saved));
                string key = receipt.ResultKey.ToString();
                if (key != saved.publicationKey || !joined.Add(key))
                    throw new InvalidOperationException("disposition-observer-key-conflict");
                bool exists = identities.TryGetValue(key, out var identity);
                if (!exists && (!string.IsNullOrEmpty(saved.publicationId) || !string.IsNullOrEmpty(saved.publicationFingerprint) || saved.publicationDelivered))
                    throw new InvalidOperationException("disposition-publication-identity-missing");
                if (exists)
                {
                    if (identity.Row.ownerRevision != saved.ownerRevision
                        || (!string.IsNullOrEmpty(saved.publicationId) && saved.publicationId != new GameplayOutcomeId(
                            new GameplayOutcomeRunId(identity.Row.runId), identity.Row.sequence).ToString())
                        || (!string.IsNullOrEmpty(saved.publicationFingerprint)
                            && saved.publicationFingerprint != identity.Row.canonicalHash)
                        || (saved.publicationDelivered && !identity.Delivered))
                        throw new InvalidOperationException("disposition-publication-proof-conflict");
                }
                var comparison = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = 1 };
                if (exists)
                {
                    comparison.tombstones.Add(identity.Row);
                    comparison.knownResultKeyHighWater = 1;
                    comparison.nextSequence = checked(identity.Row.sequence + 1);
                }
                var candidate = ledger.PrepareGameplayOutcomeRestore(comparison);
                ledger.BeginRestoreCandidate();
                ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate();
                ledger.CompleteRestoreCandidate();
                var prepared = recorder.TryPrepare(receipt, out var token);
                recorder.CancelPrepared(token);
                if (exists ? prepared.Code != OutcomePrepareCode.AlreadyTerminal : !prepared.Success)
                    throw new InvalidOperationException("disposition-canonical-fact-conflict:" + prepared.Code + ":" + prepared.DetailCode);
            }
            // Acknowledged debits retire their owner journal. Only retained pending rows join here.

            void AddExact(IEnumerable<GameplayOutcomeSnapshot> values, bool delivered)
            {
                foreach (var row in values)
                    if (row != null && row.producerId == producer) Add(new GameplayOutcomeTombstoneSnapshot
                    {
                        runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                        operationId = row.operationId, commitRevision = row.commitRevision,
                        localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                        canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                    }, delivered);
            }
            void Add(GameplayOutcomeTombstoneSnapshot row, bool delivered)
            {
                // Copy; normalization must not mutate incoming save data.
                var copy = new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision,
                    localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.canonicalHash, terminalTier = NarrativeMemoryTier.Forgotten
                };
                var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
                    row.commitRevision, row.localResultIndex);
                if (!identities.TryAdd(key.ToString(), (copy, delivered)))
                    throw new InvalidOperationException("disposition-publication-identity-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("physical-disposition-save-join:" + exception.Message); }
    }
}
