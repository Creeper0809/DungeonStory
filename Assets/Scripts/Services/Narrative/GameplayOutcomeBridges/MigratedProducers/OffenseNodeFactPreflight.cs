using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

public sealed class OffenseNodeFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
        var aggregate = DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(save, OffenseAggregateSaveSection.Id);
        if (aggregate.version != DungeonOffenseAggregateSaveData.CurrentVersion || aggregate.expedition?.activeExpeditions == null
            || aggregate.resultPublications?.observations == null)
            throw new InvalidOperationException("offense-node-aggregate-invalid");
        Validate(aggregate.nodePublications,
            DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
            DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);
        if (!report.Success) return;
        foreach (var observation in aggregate.nodePublications.observations)
        {
            var fact = observation.fact;
            var active = aggregate.expedition.activeExpeditions.SingleOrDefault(value => value.expeditionId == fact.expeditionId);
            var terminal = aggregate.resultPublications.observations.SingleOrDefault(value => value.fact.result.expeditionId == fact.expeditionId);
            if (active != null ? active.targetId != fact.targetId || active.completedNodeIds == null || !active.completedNodeIds.Contains(fact.nodeId)
                : terminal == null || terminal.fact.result.targetId != fact.targetId)
                report.AddError("offense-node-domain-owner-conflict:" + fact.expeditionId + ":" + fact.nodeId);
        }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { report.AddError("offense-node-domain-join:" + exception.Message); }
    }
    internal static void Validate(OffenseNodePublicationSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            var saved = OffenseNodePublicationRuntime.ValidateAndClone(source);
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
                throw new InvalidOperationException("offense-node-canonical-save-missing");
            string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.OffenseExpeditionNodeResult).ProducerId;
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var value in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (value == null) throw new InvalidOperationException("offense-node-canonical-null");
                if (value.producerId != producer) continue;
                Add(new GameplayOutcomeTombstoneSnapshot { runId = value.runId, sequence = value.sequence, producerId = value.producerId,
                    operationId = value.operationId, commitRevision = value.commitRevision, localResultIndex = value.localResultIndex,
                    ownerRevision = value.ownerRevision, canonicalHash = value.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten });
            }
            foreach (var value in incoming.tombstones)
            { if (value == null) throw new InvalidOperationException("offense-node-canonical-null"); if (value.producerId == producer) Add(value); }
            void Add(GameplayOutcomeTombstoneSnapshot value)
            {
                var key = new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex);
                if (value.commitRevision < 1 || value.ownerRevision != value.commitRevision || value.localResultIndex != 0
                    || value.runId != incoming.runId || value.sequence >= incoming.nextSequence || !rows.TryAdd(key.ToString(), value))
                    throw new InvalidOperationException("offense-node-canonical-identity-conflict");
            }
            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(smallPageCount: 1, largePageCount: 1,
                knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new OffenseNodeFactObserver(migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var owners = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var row in saved.observations)
            {
                var fact = new OffenseNodeResolutionFact(row.fact);
                string key = OffenseNodeFactObserver.Key(fact).ToString();
                if (key != row.publicationKey || !keys.Add(key)) throw new InvalidOperationException("offense-node-publication-identity-conflict");
                bool exists = rows.TryGetValue(key, out var canonical);
                if (!exists && !row.pending) throw new InvalidOperationException("offense-node-delivered-canonical-missing");
                string owner = producer + "|offense-expedition-node:" + row.fact.expeditionId;
                // A commit can advance its counter before the durable write throws.
                // The exact replay check below requires canonical rows to have their
                // counter, and permits only previous/current revision for a pending row.
                owners[owner] = Math.Max(owners.TryGetValue(owner, out long previous) ? previous : 0L, row.fact.sequence);
                var subset = new GameplayOutcomeLedgerSaveData { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (exists) { subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1; }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate); ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                bool accepted = observer.TryPrepareReplay(key, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("offense-node-canonical-payload-conflict:" + failure);
            }
            if (rows.Keys.Any(key => !keys.Contains(key)) || counters.owners.Any(value => value == null || value.ownerKey == null
                || value.ownerKey.StartsWith(producer + "|", StringComparison.Ordinal)
                    && (!owners.TryGetValue(value.ownerKey, out long expected)
                        || value.revision < expected - 1L || value.revision > expected)))
                throw new InvalidOperationException("offense-node-canonical-owner-conflict");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("offense-node-save-join:" + exception.Message); }
    }
}
