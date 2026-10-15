using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using DungeonStory.ServiceRooms;

/// <summary>Canonical log comparison belongs to the observer, not the service owner.</summary>
public sealed class ServiceSessionTerminalFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<ServiceRoomsSaveData>(save, ServiceRoomsSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(ServiceRoomsSaveData services, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            _ = ServiceSessionAggregate.CreateRestored(services, 0);
            var terminals = services.sessions.Where(value => value.stage is ServiceSessionStage.Completed or ServiceSessionStage.Cancelled).ToArray();
            if (terminals.Length == 0) return;
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("service-terminal-canonical-save-missing");
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
                { new ServiceSessionOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new ServiceSessionOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<ServiceSessionTerminalFact, ServiceSessionOutcomeReceipt>(
                ServiceSessionTerminalFactMapping.Capture, registry, ledger, recorder,
                new GameplayDomainFactCommitGroup(recorder, ledger), ServiceSessionTerminalFactMapping.Reserve);
            foreach (var saved in terminals)
            {
                var key = ServiceSessionOutcomeKey.Create(saved.sessionId);
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                bool undelivered = false;
                foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
                    if (row != null && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value)
                    {
                        if (row.commitRevision != key.CommitRevision || row.localResultIndex != key.LocalResultIndex)
                            throw new InvalidOperationException("service-terminal-canonical-identity-drift");
                        undelivered |= incoming.outbox.Contains(row);
                        subset.tombstones.Add(new GameplayOutcomeTombstoneSnapshot
                        {
                            runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                            operationId = row.operationId, commitRevision = row.commitRevision,
                            localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                            canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                        });
                    }
                foreach (var row in incoming.tombstones)
                    if (row != null && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value)
                    {
                        if (row.commitRevision != key.CommitRevision || row.localResultIndex != key.LocalResultIndex)
                            throw new InvalidOperationException("service-terminal-canonical-identity-drift");
                        subset.tombstones.Add(row);
                    }
                if (subset.tombstones.Count > 1)
                    throw new InvalidOperationException("service-terminal-canonical-identity-duplicate");
                var canonical = subset.tombstones.SingleOrDefault();
                bool bound = saved.terminalPublicationKey.Length > 0;
                bool paymentReady = saved.stage != ServiceSessionStage.Completed || !saved.contract.paymentRequired
                    || saved.contract.price == 0 || saved.paymentCommitted;
                if (bound && saved.terminalPublicationKey != key.ToString()
                    || canonical != null && (!bound || !paymentReady)
                    || canonical == null && saved.terminalPhase >= ServiceSessionTerminalPhase.ObservationCommitted
                    || saved.terminalPhase == ServiceSessionTerminalPhase.Acknowledged && undelivered)
                    throw new InvalidOperationException("service-terminal-owner-canonical-phase-conflict");
                if (canonical != null
                    && (canonical.ownerRevision != saved.terminalOwnerRevision
                        || saved.terminalPublicationId.Length > 0 && saved.terminalPublicationId !=
                            new GameplayOutcomeId(new GameplayOutcomeRunId(canonical.runId), canonical.sequence).ToString()
                        || saved.terminalPublicationFingerprint.Length > 0 && saved.terminalPublicationFingerprint != canonical.canonicalHash))
                    throw new InvalidOperationException("service-terminal-publication-proof-conflict");
                if (!bound || !paymentReady) continue;
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(saved.terminalPublicationKey,
                    ServiceSessionTerminalFact.Actual(saved.ToSnapshot()), out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("service-terminal-canonical-payload-conflict:" + failure);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("service-terminal-observation-save-join:" + exception.Message); }
    }
}
