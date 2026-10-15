using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

public sealed class ProductionCommandFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(DungeonSaveSectionPayload.ReadOrNew<DungeonProductionBillSaveData>(save, ProductionBillsSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("production-command-save-join:" + error.Message); }
    }

    public static void Validate(DungeonProductionBillSaveData owner, GameplayOutcomeLedgerSaveData incoming)
    {
        if (owner?.pendingCommandObservations == null || owner.nextCommandSequence < 1
            || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("production-command-save-state-missing");
        var known = incoming.tombstones.Where(row => row != null && row.producerId == ProductionCommandOutcomeIds.ProducerId).ToList();
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null && row.producerId == ProductionCommandOutcomeIds.ProducerId))
            known.Add(new GameplayOutcomeTombstoneSnapshot
            {
                runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                operationId = row.operationId, commitRevision = row.commitRevision, localResultIndex = row.localResultIndex,
                ownerRevision = row.ownerRevision, temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
                absoluteDay = row.absoluteDay, parentRunId = row.parentRunId, parentSequence = row.parentSequence,
                deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
                recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
                deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
                canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
            });
        if (known.Any(row => row.ownerRevision < 1 || row.ownerRevision >= owner.nextCommandSequence
                || new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex)
                    != ProductionCommandOutcomeIds.ResultKey(row.ownerRevision))
            || known.GroupBy(row => row.ownerRevision).Any(group => group.Count() != 1))
            throw new InvalidOperationException("production-command-native-sequence-conflict");

        long previous = 0;
        foreach (var pending in owner.pendingCommandObservations)
        {
            if (pending?.fact == null || pending.fact.ownerRevision <= previous)
                throw new InvalidOperationException("production-command-pending-order-invalid");
            pending.Validate(owner.nextCommandSequence); previous = pending.fact.ownerRevision;
            var fact = new ProductionCommandAppliedFact(pending.fact);
            var receipt = ProductionCommandFactObservation.Capture(fact);
            var found = known.Where(row => row.ownerRevision == fact.OwnerRevision).ToList();
            if (pending.publicationKey.Length == 0 ? found.Count != 0 : pending.publicationKey != receipt.ResultKey.ToString())
                throw new InvalidOperationException("production-command-publication-key-conflict");
            if (pending.publicationId.Length > 0 && (found.Count != 1
                || pending.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(found[0].runId), found[0].sequence).ToString()
                || pending.publicationFingerprint != found[0].canonicalHash))
                throw new InvalidOperationException("production-command-publication-proof-conflict");

            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new ProductionCommandOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new ProductionCommandOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4, retainedSmallPageCount: 2, retainedLargePageCount: 2));
            var candidate = ledger.PrepareGameplayOutcomeRestore(new GameplayOutcomeLedgerSaveData
            {
                runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
                nextSequence = incoming.nextSequence, knownResultKeyHighWater = found.Count, tombstones = found
            });
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var prepared = recorder.TryPrepare(receipt, out var token); recorder.CancelPrepared(token);
            if (found.Count == 0 ? !prepared.Success : prepared.Code != OutcomePrepareCode.AlreadyTerminal)
                throw new InvalidOperationException("production-command-canonical-fact-conflict:" + prepared.Code + ":" + prepared.DetailCode);
        }
    }
}
