using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>Joins the single campaign truth observation to its canonical result without mutating live state.</summary>
public sealed class OffenseTruthFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(save, OffenseAggregateSaveSection.Id).campaign,
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(DungeonOffenseCampaignSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            if (source == null || source.version != DungeonOffenseCampaignSaveData.CurrentVersion
                || source.knownTargetIds == null || source.completedTargetIds == null || source.truthObservation == null)
                throw new InvalidOperationException("offense-truth-campaign-save-invalid");
            source.truthObservation.Validate(source.revealedTruthTargetId, source.completedTargetIds.Count);
            if (source.truthObservation.hasFact
                && (!source.completedTargetIds.Contains(source.revealedTruthTargetId) || !source.knownTargetIds.Contains(source.revealedTruthTargetId)))
                throw new InvalidOperationException("offense-truth-owner-state-conflict");
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("offense-truth-canonical-save-missing");
            var rows = new List<GameplayOutcomeTombstoneSnapshot>();
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("offense-truth-canonical-row-null");
                if (row.producerId == ExternalFactionOutcomeIds.TruthRevealProducerId)
                    rows.Add(new GameplayOutcomeTombstoneSnapshot
                    {
                        runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                        operationId = row.operationId, commitRevision = row.commitRevision, localResultIndex = row.localResultIndex,
                        ownerRevision = row.ownerRevision, canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                    });
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("offense-truth-canonical-tombstone-null");
                if (row.producerId == ExternalFactionOutcomeIds.TruthRevealProducerId) rows.Add(row);
            }
            var observation = source.truthObservation;
            if (rows.Count > 1 || !observation.hasFact && rows.Count != 0)
                throw new InvalidOperationException("offense-truth-canonical-owner-count-conflict");
            if (!observation.hasFact) return;

            var fact = new OffenseTruthRevealedFact(observation.fact);
            var expected = OffenseTruthRevealFactMapping.Capture(fact).ResultKey;
            if (observation.publicationKey != expected.ToString())
                throw new InvalidOperationException("offense-truth-publication-key-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
            if (rows.Count == 1)
            {
                var row = rows[0];
                var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex);
                if (key != expected || row.runId != incoming.runId || row.sequence >= incoming.nextSequence || row.ownerRevision != fact.OwnerRevision)
                    throw new InvalidOperationException("offense-truth-canonical-identity-conflict");
                subset.tombstones.Add(row); subset.knownResultKeyHighWater = 1;
            }
            else if (!observation.pending) throw new InvalidOperationException("offense-truth-completed-publication-missing");
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new OffenseTruthRevealOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new OffenseTruthRevealOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<OffenseTruthRevealedFact, OffenseTruthRevealOutcomeReceipt>(
                OffenseTruthRevealFactMapping.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger));
            bool accepted = observer.TryPrepareReplay(observation.publicationKey, fact, out var prepared, out string failure);
            prepared?.Cancel();
            if (!accepted) throw new InvalidOperationException("offense-truth-canonical-payload-conflict:" + failure);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("offense-truth-save-join:" + exception.Message); }
    }
}
