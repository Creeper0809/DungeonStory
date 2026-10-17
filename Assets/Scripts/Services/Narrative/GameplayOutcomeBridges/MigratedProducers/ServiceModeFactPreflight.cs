using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using DungeonStory.ServiceRooms;

/// <summary>Only the observer validates opaque mode publications against canonical log identities.</summary>
public sealed class ServiceModeFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<ServiceRoomsSaveData>(save, ServiceRoomsSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(ServiceRoomsSaveData services, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            ServiceModeChangedFact.ValidatePending(services?.pendingModeFacts);
            if (services.pendingModeFacts.Count == 0) return;
            _ = ServiceSessionAggregate.CreateRestored(services, 0);
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("service-mode-canonical-save-missing");
            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                    (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new MigratedDomainFactObserver<ServiceModeChangedFact>(ServiceModeChangedFactMapping.Capture,
                migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var pending in services.pendingModeFacts)
            {
                if (!GameplayDomainFactDeliveryQuery.TryReadKey(pending.publicationKey, out var key))
                    throw new InvalidOperationException("service-mode-publication-reference-invalid");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
                    if (row != null && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value
                        && row.commitRevision == key.CommitRevision && row.localResultIndex == key.LocalResultIndex)
                        subset.tombstones.Add(new GameplayOutcomeTombstoneSnapshot
                        {
                            runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                            operationId = row.operationId, commitRevision = row.commitRevision,
                            localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                            canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                        });
                foreach (var row in incoming.tombstones)
                    if (row != null && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value
                        && row.commitRevision == key.CommitRevision && row.localResultIndex == key.LocalResultIndex)
                        subset.tombstones.Add(row);
                if (subset.tombstones.Count > 1)
                    throw new InvalidOperationException("service-mode-canonical-identity-duplicate");
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                bool accepted = observer.TryPrepareReplay(pending.publicationKey,
                    new ServiceModeChangedFact(pending.fact), out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("service-mode-canonical-conflict:" + failure);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("service-mode-observation-save-join:" + exception.Message); }
    }
}
