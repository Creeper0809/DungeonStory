using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Canonical validation uses frozen facts, never current actors or policy guesses.</summary>
public sealed class CombatActionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<CombatActionPendingSaveData>(save, CombatActionPendingSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);
    internal static void Validate(CombatActionPendingSaveData save, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            _ = CombatActionPendingState.Restore(save);
            if (save.pending.Count == 0) return;
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("combat-action-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("combat-action-canonical-row-null");
                Add(new GameplayOutcomeTombstoneSnapshot { runId = row.runId, sequence = row.sequence,
                    producerId = row.producerId, operationId = row.operationId, commitRevision = row.commitRevision,
                    localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten });
            }
            foreach (var row in incoming.tombstones) Add(row);
            var josa = new KoreanJosaFormatter();
            var descriptors = MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, josa)).ToList();
            descriptors.Add(new CombatDamageOutcomeDescriptor()); descriptors.Add(new SocialConflictOutcomeDescriptor(josa));
            var registry = new GameplayOutcomeRegistry(descriptors, new IGameplayOutcomeAdapterRegistration[]
                { new MigratedProducerOutcomeAdapter(), new CombatDamageOutcomeAdapter(), new SocialConflictOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 8, largePageCount: 8, knownResultKeyCapacity: 32,
                retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new CombatActionFactObserver(ledger, recorder, migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var pending in save.pending)
            {
                var fact = new CombatActionFact(pending.fact);
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                foreach (var key in CombatActionFactObserver.RelatedKeys(fact))
                    if (rows.TryGetValue(key.ToString(), out var row)) subset.tombstones.Add(row);
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                bool accepted = observer.TryPrepareReplay(pending.publicationKey, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("combat-action-canonical-conflict:" + failure);
            }
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                if (row == null) throw new InvalidOperationException("combat-action-canonical-tombstone-null");
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
                    row.commitRevision, row.localResultIndex).ToString();
                if (!rows.TryAdd(key, row)) throw new InvalidOperationException("combat-action-canonical-identity-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("combat-action-observation-save-join:" + exception.Message); }
    }
}
