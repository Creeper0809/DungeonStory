using System;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>Validates saved quality facts against canonical records without granting the craft owner ledger access.</summary>
public sealed class CombatEquipmentQualityFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonCombatEquipmentSaveData>(save, CombatEquipmentSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(DungeonCombatEquipmentSaveData equipment, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            if (equipment?.craftOrders == null || equipment.craftOrders.Any(value => value == null))
                throw new InvalidOperationException("combat-quality-orders-invalid");
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new ProductQualityOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new ProductQualityOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<CombatEquipmentQualityResolvedFact, ProductQualityOutcomeReceipt>(
                CombatEquipmentQualityFactMapping.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var order in equipment.craftOrders)
            {
                CombatEquipmentQualityResolvedFact.ValidateOwner(order);
                if (!order.attemptOutcomeResolved || order.qualityOutcomeSchemaVersion == 0) continue;
                var fact = CombatEquipmentQualityResolvedFact.FromOrder(order);
                var key = CombatEquipmentQualityFactMapping.Capture(fact).ResultKey;
                if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                    throw new InvalidOperationException("combat-quality-canonical-save-missing");
                var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
                    && value.producerId == key.ProducerId && value.operationId == key.OperationId.Value
                    && value.commitRevision == key.CommitRevision && value.localResultIndex == key.LocalResultIndex).ToArray();
                var retired = incoming.tombstones.Where(value => value != null && value.producerId == key.ProducerId
                    && value.operationId == key.OperationId.Value && value.commitRevision == key.CommitRevision
                    && value.localResultIndex == key.LocalResultIndex).ToArray();
                int count = rows.Length + retired.Length;
                if (count > 1) throw new InvalidOperationException("combat-quality-canonical-duplicate");
                if (order.qualityOutcomeResultKey.Length == 0)
                {
                    if (count != 0) throw new InvalidOperationException("combat-quality-observation-key-missing");
                    continue;
                }
                if (order.qualityOutcomeResultKey != key.ToString())
                    throw new InvalidOperationException("combat-quality-observation-key-conflict");
                if (order.qualityOutcomeCommitted && (count == 0 || retired.Length == 0 && !incoming.exactOutcomes.Contains(rows[0])))
                    throw new InvalidOperationException("combat-quality-delivery-proof-missing");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (count == 1)
                {
                    var canonical = retired.Length == 1 ? retired[0] : new GameplayOutcomeTombstoneSnapshot
                    {
                        runId = rows[0].runId, sequence = rows[0].sequence, producerId = rows[0].producerId,
                        operationId = rows[0].operationId, commitRevision = rows[0].commitRevision,
                        localResultIndex = rows[0].localResultIndex, ownerRevision = rows[0].ownerRevision,
                        canonicalHash = rows[0].immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                    };
                    if (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence
                        || canonical.ownerRevision != fact.OwnerRevision)
                        throw new InvalidOperationException("combat-quality-canonical-owner-conflict");
                    subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1;
                }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(order.qualityOutcomeResultKey, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("combat-quality-canonical-payload-conflict:" + failure);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("combat-quality-observation-save-join:" + exception.Message); }
    }
}
