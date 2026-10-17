using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Canonical log joins belong to observation, not the equipment save owner.</summary>
public sealed class CombatEquipmentCraftCompletionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonCombatEquipmentSaveData>(save, CombatEquipmentSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(DungeonCombatEquipmentSaveData equipment, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            if (equipment?.craftOrders == null || equipment.craftOrders.Any(v => v == null))
                throw new InvalidOperationException("combat-craft-observation-orders-invalid");
            foreach (var order in equipment.craftOrders)
            {
                var owner = order.outputPublication;
                if (owner?.observationExpected != true)
                {
                    if (order.completionFact?.IsEmpty != true)
                        throw new InvalidOperationException("combat-craft-unapplied-completion-fact");
                    continue;
                }
                var fact = CombatEquipmentCraftCompletedFact.Restore(order);
                if (!ProductionDomainOutputPublicationService.TryValidateCommittedOwner(owner, out string failure))
                    throw new InvalidOperationException("combat-craft-observation-output-invalid:" + failure);
                if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                    throw new InvalidOperationException("combat-craft-observation-canonical-save-missing");
                var key = CombatEquipmentCraftCompletionFactObserver.Key(fact);
                if (owner.observationKey != key.ToString())
                    throw new InvalidOperationException("combat-craft-observation-key-conflict");
                var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(v => v != null
                    && v.producerId == key.ProducerId && v.operationId == key.OperationId.Value
                    && v.commitRevision == key.CommitRevision && v.localResultIndex == key.LocalResultIndex).ToArray();
                var retired = incoming.tombstones.Where(v => v != null && v.producerId == key.ProducerId
                    && v.operationId == key.OperationId.Value && v.commitRevision == key.CommitRevision
                    && v.localResultIndex == key.LocalResultIndex).ToArray();
                if (rows.Length + retired.Length > 1)
                    throw new InvalidOperationException("combat-craft-canonical-duplicate");
                bool exists = rows.Length + retired.Length == 1;
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (exists)
                {
                    var canonical = retired.Length == 1 ? retired[0] : new GameplayOutcomeTombstoneSnapshot
                    {
                        runId = rows[0].runId, sequence = rows[0].sequence, producerId = rows[0].producerId,
                        operationId = rows[0].operationId, commitRevision = rows[0].commitRevision,
                        localResultIndex = rows[0].localResultIndex, ownerRevision = rows[0].ownerRevision,
                        canonicalHash = rows[0].immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                    };
                    string id = new GameplayOutcomeId(new GameplayOutcomeRunId(canonical.runId), canonical.sequence).ToString();
                    if (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence || canonical.ownerRevision != 1L
                        || !string.IsNullOrEmpty(owner.observationId) && owner.observationId != id
                        || !string.IsNullOrEmpty(owner.observationFingerprint) && owner.observationFingerprint != canonical.canonicalHash
                        || owner.observationDelivered && retired.Length == 0 && !incoming.exactOutcomes.Contains(rows[0]))
                        throw new InvalidOperationException("combat-craft-observation-proof-conflict");
                    subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1;
                }
                else if (owner.observationDelivered || !string.IsNullOrEmpty(owner.observationId) || !string.IsNullOrEmpty(owner.observationFingerprint))
                    throw new InvalidOperationException("combat-craft-observation-proof-without-canonical");
                var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(v =>
                        (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(v, new KoreanJosaFormatter())),
                    new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
                var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                    smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
                var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
                migrated.Restore(migrated.PrepareRestore(counters));
                var observer = new CombatEquipmentCraftCompletionFactObserver(migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
                bool accepted = observer.TryPrepareReplay(owner.observationKey, fact, out var prepared, out failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("combat-craft-canonical-payload-conflict:" + failure);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("combat-craft-observation-save-join:" + exception.Message); }
    }
}
