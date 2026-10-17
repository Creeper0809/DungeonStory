using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Validates saved arrival facts against their exact canonical identity, payload and revision.</summary>
public sealed class OffenseArrivalSettlementFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(save, OffenseAggregateSaveSection.Id).returnArrivals,
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(DungeonOffenseReturnArrivalSaveData arrivals, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            OffenseReturnArrivalSaveValidation.Validate(arrivals, report);
            if (!report.Success) return;
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
                throw new InvalidOperationException("offense-arrival-canonical-save-missing");
            string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt).ProducerId;
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var value in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (value == null) throw new InvalidOperationException("offense-arrival-canonical-null");
                if (value.producerId != producer) continue;
                Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = value.runId, sequence = value.sequence, producerId = value.producerId,
                    operationId = value.operationId, commitRevision = value.commitRevision, localResultIndex = value.localResultIndex,
                    ownerRevision = value.ownerRevision, canonicalHash = value.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
            }
            foreach (var value in incoming.tombstones)
            {
                if (value == null) throw new InvalidOperationException("offense-arrival-canonical-null");
                if (value.producerId == producer) Add(value);
            }
            void Add(GameplayOutcomeTombstoneSnapshot value)
            {
                var key = new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex);
                if (value.commitRevision != 1 || value.ownerRevision != 1 || value.localResultIndex != 0
                    || value.runId != incoming.runId || value.sequence >= incoming.nextSequence || !rows.TryAdd(key.ToString(), value))
                    throw new InvalidOperationException("offense-arrival-canonical-identity-conflict");
            }
            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                    (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new OffenseArrivalSettlementFactObserver(migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var arrival in arrivals.arrivals)
            {
                OffenseArrivalSettlementRuntime.ValidateState(arrival);
                var observation = arrival.settlement;
                if (!observation.hasFact) continue;
                var fact = new OffenseArrivalSettledFact(observation.fact);
                string key = OffenseArrivalSettlementFactObserver.Key(fact).ToString();
                if (key != observation.publicationKey || !keys.Add(key))
                    throw new InvalidOperationException("offense-arrival-owner-publication-conflict");
                owners.Add(producer + "|" + arrival.arrivalId);
                bool exists = rows.TryGetValue(key, out var canonical);
                if (!exists && !observation.pending) throw new InvalidOperationException("offense-arrival-delivered-canonical-missing");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (exists) { subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1; }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                bool accepted = observer.TryPrepareReplay(key, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("offense-arrival-canonical-payload-conflict:" + failure);
            }
            if (rows.Keys.Any(key => !keys.Contains(key))
                || counters.owners.Any(value => value == null || value.ownerKey == null || value.ownerKey.StartsWith(producer + "|", StringComparison.Ordinal)
                    && (value.revision != 1 || !owners.Contains(value.ownerKey))))
                throw new InvalidOperationException("offense-arrival-canonical-owner-missing");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("offense-arrival-save-join:" + exception.Message); }
    }
}
