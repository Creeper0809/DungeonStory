using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>The observer, not the item owner, joins opaque saved publications to its ledger.</summary>
public sealed class PhysicalItemRelocationFactPreflight :
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
            var producer = TradeInventoryOutcomeDefinitions.Get(TradeInventoryOutcomeKind.PhysicalItemRelocation).ProducerId;
            var identities = new Dictionary<string, (GameplayOutcomeTombstoneSnapshot Row, bool Delivered)>(StringComparer.Ordinal);
            AddExact(incoming.outbox, false);
            AddExact(incoming.exactOutcomes, true);
            foreach (var value in incoming.tombstones)
                if (value != null && value.producerId == producer) Add(value, true);
            if (physical.physicalItemRelocations.Count == 0 && identities.Count == 0) return;

            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new TradeInventoryOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new TradeInventoryOutcomeAdapter() });
            // One small comparison page reused for the entire join, no live game state or delivery.
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 0, knownResultKeyCapacity: 2,
                retainedSmallPageCount: 1, retainedLargePageCount: 0));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var joined = new HashSet<string>(StringComparer.Ordinal);
            foreach (var saved in physical.physicalItemRelocations)
            {
                PhysicalItemRelocationService.ValidateJournal(saved);
                var receipt = PhysicalItemRelocationFactObservation.Capture(PhysicalItemRelocatedFact.FromSave(saved));
                string key = receipt.ResultKey.ToString();
                if (key != saved.publicationKey || !joined.Add(key))
                    throw new InvalidOperationException("relocation-observer-key-conflict");
                bool exists = identities.TryGetValue(key, out var identity);
                if (!exists && saved.phase != (int)PhysicalItemRelocationJournalPhase.DomainCommitted)
                    throw new InvalidOperationException("relocation-publication-identity-missing");
                if (exists)
                {
                    if (identity.Row.ownerRevision != saved.ownerRevision
                        || (!string.IsNullOrEmpty(saved.publicationId) && saved.publicationId != new GameplayOutcomeId(
                            new GameplayOutcomeRunId(identity.Row.runId), identity.Row.sequence).ToString())
                        || (!string.IsNullOrEmpty(saved.publicationFingerprint)
                            && saved.publicationFingerprint != identity.Row.canonicalHash)
                        || (saved.phase == (int)PhysicalItemRelocationJournalPhase.Delivered && !identity.Delivered))
                        throw new InvalidOperationException("relocation-publication-proof-conflict");
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
                    throw new InvalidOperationException("relocation-canonical-fact-conflict:" + prepared.Code + ":" + prepared.DetailCode);
            }
            foreach (var key in identities.Keys)
                if (!joined.Contains(key)) throw new InvalidOperationException("relocation-owner-journal-missing");

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
                    throw new InvalidOperationException("relocation-publication-identity-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("physical-relocation-save-join:" + exception.Message); }
    }
}
