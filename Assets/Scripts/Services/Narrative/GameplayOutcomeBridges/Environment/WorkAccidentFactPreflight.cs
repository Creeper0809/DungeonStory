using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

public sealed class WorkAccidentFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonWorkOrderSaveData>(save, WorkOrdersSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(DungeonWorkOrderSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            WorkAccidentObservationSaveData.ValidateAll(source);
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("work-accident-ledger-save-invalid");
            if (source.pendingAccidents.Count == 0) return;
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
            { new ProcessAccidentOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new ProcessAccidentOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            foreach (var row in source.pendingAccidents)
            {
                var receipt = WorkAccidentFactMapping.Capture(new WorkAccidentFact(row.fact));
                var key = receipt.Payload.ResultKey;
                if (key.ToString() != row.publicationKey)
                    throw new InvalidOperationException("work-accident-publication-key-conflict");
                var live = incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
                    && value.producerId == key.ProducerId && value.operationId == key.OperationId.Value).ToArray();
                var retired = incoming.tombstones.Where(value => value != null
                    && value.producerId == key.ProducerId && value.operationId == key.OperationId.Value).ToArray();
                if (live.Length + retired.Length > 1)
                    throw new InvalidOperationException("work-accident-canonical-duplicate");
                var canonical = retired.FirstOrDefault() ?? (live.Length == 0 ? null : new GameplayOutcomeTombstoneSnapshot
                {
                    runId = live[0].runId, sequence = live[0].sequence, producerId = live[0].producerId,
                    operationId = live[0].operationId, commitRevision = live[0].commitRevision,
                    localResultIndex = live[0].localResultIndex, ownerRevision = live[0].ownerRevision,
                    canonicalHash = live[0].immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
                if (row.rollbackBlocked && canonical != null || row.observationCompleted && canonical == null)
                    throw new InvalidOperationException("work-accident-observation-phase-conflict");
                if (canonical != null && (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence
                    || canonical.commitRevision != 0 || canonical.ownerRevision != 0 || canonical.localResultIndex != 0
                    || live.Length == 1 && live[0].outcomeTypeId != receipt.Payload.OutcomeTypeId.Value))
                    throw new InvalidOperationException("work-accident-canonical-identity-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (canonical != null) { subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1; }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                var result = recorder.TryPrepare(receipt, out var token);
                recorder.CancelPrepared(token);
                if (canonical == null ? !result.Success : result.Code != OutcomePrepareCode.AlreadyTerminal)
                    throw new InvalidOperationException("work-accident-canonical-payload-conflict:" + result.DetailCode);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("work-accident-save-join:" + exception.Message); }
    }
}
