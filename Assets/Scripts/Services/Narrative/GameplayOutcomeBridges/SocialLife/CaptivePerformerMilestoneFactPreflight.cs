using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Historical unlocks join canonical output by their frozen facts, not today's fame or custody.</summary>
public sealed class CaptivePerformerMilestoneFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
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
                throw new InvalidOperationException("captive-milestone-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("captive-milestone-canonical-row-null");
                if (row.producerId != CaptivePerformerMilestoneOutcomeIds.ProducerId) continue;
                Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision, localResultIndex = row.localResultIndex,
                    ownerRevision = row.ownerRevision, canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("captive-milestone-canonical-tombstone-null");
                if (row.producerId == CaptivePerformerMilestoneOutcomeIds.ProducerId) Add(row);
            }
            var observations = source.captives.SelectMany(value => value.performerMilestones).ToArray();
            if (observations.Length == 0 && rows.Count == 0) return;
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new CaptivePerformerMilestoneOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new CaptivePerformerMilestoneOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<CaptivePerformerMilestoneUnlockedFact, CaptivePerformerMilestoneOutcomeReceipt>(
                CaptivePerformerMilestoneFactMapping.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var observation in observations)
            {
                var fact = new CaptivePerformerMilestoneUnlockedFact(observation.fact);
                var key = CaptivePerformerMilestoneFactMapping.Capture(fact).ResultKey;
                if (observation.publicationKey != key.ToString()) throw new InvalidOperationException("captive-milestone-publication-key-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (rows.TryGetValue(observation.publicationKey, out var canonical))
                {
                    if (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence
                        || canonical.ownerRevision != observation.fact.revision)
                        throw new InvalidOperationException("captive-milestone-canonical-identity-conflict");
                    subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1;
                    rows.Remove(observation.publicationKey);
                }
                else if (!observation.pending) throw new InvalidOperationException("captive-milestone-completed-publication-missing");
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(observation.publicationKey, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("captive-milestone-canonical-payload-conflict:" + failure);
            }
            if (rows.Count != 0) throw new InvalidOperationException("captive-milestone-canonical-owner-missing");
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
                if (!rows.TryAdd(key, row)) throw new InvalidOperationException("captive-milestone-canonical-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("captive-milestone-observation-save-join:" + exception.Message); }
    }
}
