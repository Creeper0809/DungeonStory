using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Retained interaction results join canonical output by their complete frozen domain facts.</summary>
public sealed class CaptivityInteractionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<CaptivitySaveData>(save, CaptivitySaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(CaptivitySaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            var shape = new DungeonGameRestoreReport();
            CaptivitySaveValidation.Validate(source, shape);
            if (!shape.Success) throw new InvalidOperationException(string.Join(" | ", shape.Errors));
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("captivity-interaction-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("captivity-interaction-canonical-row-null");
                if (row.producerId != CaptivityInteractionOutcomeIds.ProducerId) continue;
                Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision, localResultIndex = row.localResultIndex,
                    ownerRevision = row.ownerRevision, canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("captivity-interaction-canonical-tombstone-null");
                if (row.producerId == CaptivityInteractionOutcomeIds.ProducerId) Add(row);
            }
            var observations = source.captives.Where(value => value.interactionTerminal.HasCommittedOutcome).ToArray();
            foreach (var captive in source.captives.Where(value => value.interactionTerminal.HasOutcome
                         && !value.interactionTerminal.HasCommittedOutcome))
            {
                var fact = new CaptivityInteractionResolvedFact(captive.captiveId, captive.interactionTerminal,
                    checked(captive.interactionOutcomeRevision + 1L));
                if (rows.ContainsKey(CaptivityInteractionFactMapping.Capture(fact).ResultKey.ToString()))
                    throw new InvalidOperationException("captivity-interaction-unapplied-canonical-conflict");
            }
            if (observations.Length == 0) return;
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new CaptivityInteractionOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new CaptivityInteractionOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<CaptivityInteractionResolvedFact, CaptivityInteractionOutcomeReceipt>(
                CaptivityInteractionFactMapping.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var captive in observations)
            {
                var observation = captive.interactionTerminal;
                var fact = new CaptivityInteractionResolvedFact(captive.captiveId, observation, observation.outcomeRevision);
                var key = CaptivityInteractionFactMapping.Capture(fact).ResultKey;
                if (observation.publicationKey != key.ToString()) throw new InvalidOperationException("captivity-interaction-publication-key-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (rows.TryGetValue(observation.publicationKey, out var canonical))
                {
                    if (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence
                        || canonical.ownerRevision != observation.outcomeRevision)
                        throw new InvalidOperationException("captivity-interaction-canonical-identity-conflict");
                    subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1;
                    rows.Remove(observation.publicationKey);
                }
                else if (!observation.deliveryPending) throw new InvalidOperationException("captivity-interaction-completed-publication-missing");
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(observation.publicationKey, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("captivity-interaction-canonical-payload-conflict:" + failure);
            }
            // Completed attempts clear their terminal; the ledger retains their history.
            // Only a still-retained owner result must join. Unmatched historical rows are legitimate.
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
                if (!rows.TryAdd(key, row)) throw new InvalidOperationException("captivity-interaction-canonical-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("captivity-interaction-observation-save-join:" + exception.Message); }
    }
}
