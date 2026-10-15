using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>Compare frozen historical effects with canonical output, never re-evaluate today's mood or relationships.</summary>
public sealed class IdleSocialInteractionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<IdleSocialInteractionPendingSaveData>(save, IdleSocialInteractionPendingSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);
    internal static void Validate(IdleSocialInteractionPendingSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            _ = IdleSocialInteractionPendingState.Restore(source);
            if (source.pending.Count == 0) return;
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("idle-social-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("idle-social-canonical-row-null");
                Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision, localResultIndex = row.localResultIndex,
                    ownerRevision = row.ownerRevision, canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
            }
            foreach (var row in incoming.tombstones) Add(row);
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new IdleSocialInteractionOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new IdleSocialInteractionOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<IdleSocialInteractionResolvedFact, IdleSocialInteractionOutcomeReceipt>(
                IdleSocialInteractionFactMapping.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger),
                IdleSocialInteractionFactMapping.ReserveShape);
            foreach (var pending in source.pending)
            {
                var fact = new IdleSocialInteractionResolvedFact(pending.fact);
                var key = IdleSocialInteractionFactMapping.Capture(fact).ResultKey;
                if (pending.publicationKey != key.ToString()) throw new InvalidOperationException("idle-social-pending-key-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (rows.TryGetValue(pending.publicationKey, out var canonical))
                {
                    if (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence || canonical.ownerRevision != 0L)
                        throw new InvalidOperationException("idle-social-pending-canonical-identity-conflict");
                    subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1;
                }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(pending.publicationKey, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("idle-social-canonical-payload-conflict:" + failure);
            }
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                if (row == null) throw new InvalidOperationException("idle-social-canonical-tombstone-null");
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
                if (!rows.TryAdd(key, row)) throw new InvalidOperationException("idle-social-canonical-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("idle-social-observation-save-join:" + exception.Message); }
    }
}
