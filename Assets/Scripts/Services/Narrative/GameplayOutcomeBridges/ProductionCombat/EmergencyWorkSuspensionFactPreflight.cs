using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using DungeonStory.Narrative.Korean;

public sealed class EmergencyWorkSuspensionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonEventAlertSaveData>(save, EventAlertSaveSection.Id).threatAlert,
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(SettlementThreatAlertSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            EmergencyWorkSuspensionObservationState.Validate(source);
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("suspension-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("suspension-canonical-null");
                if (row.producerId != EmergencyWorkSuspensionOutcomeIds.ProducerId) continue;
                Add(new GameplayOutcomeTombstoneSnapshot { runId = row.runId, sequence = row.sequence,
                    producerId = row.producerId, operationId = row.operationId, commitRevision = row.commitRevision,
                    localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten });
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("suspension-canonical-null");
                if (row.producerId == EmergencyWorkSuspensionOutcomeIds.ProducerId) Add(row);
            }
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex);
                if (row.runId != incoming.runId || row.sequence >= incoming.nextSequence || row.ownerRevision != row.commitRevision
                    || row.commitRevision <= 0 || row.localResultIndex != 0 || !rows.TryAdd(key.ToString(), row))
                    throw new InvalidOperationException("suspension-canonical-identity-conflict");
            }
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new EmergencyWorkSuspensionOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new EmergencyWorkSuspensionOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(smallPageCount: 1, largePageCount: 0,
                knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 0));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            foreach (var observation in source.suspensionObservations)
            {
                var receipt = EmergencyWorkSuspensionFactMapping.Capture(new EmergencyWorkSuspensionFact(observation.fact));
                string key = receipt.ResultKey.ToString();
                if (observation.publicationKey != key) throw new InvalidOperationException("suspension-observation-key-conflict");
                rows.TryGetValue(key, out var canonical);
                if (!observation.pending && canonical == null) throw new InvalidOperationException("suspension-completed-canonical-missing");
                var subset = new GameplayOutcomeLedgerSaveData { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (canonical != null) { subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1; }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate); ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                var prepared = recorder.TryPrepare(receipt, out var token);
                recorder.CancelPrepared(token);
                if (canonical == null ? !prepared.Success : prepared.Code != OutcomePrepareCode.AlreadyTerminal)
                    throw new InvalidOperationException("suspension-canonical-payload-conflict:" + prepared.DetailCode);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("suspension-observation-save-join:" + exception.Message); }
    }
}
