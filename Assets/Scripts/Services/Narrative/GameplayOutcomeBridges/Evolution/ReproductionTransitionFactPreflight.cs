using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>Only the observer interprets publication keys and canonical log identities.</summary>
public sealed class ReproductionTransitionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    private readonly IReproductionDefinitionCatalog definitions;
    public ReproductionTransitionFactPreflight(IReproductionDefinitionCatalog definitions) =>
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));

    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<ReproductionWorldSaveData>(save, ReproductionSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        definitions, report);

    internal static void Validate(ReproductionWorldSaveData source, GameplayOutcomeLedgerSaveData incoming,
        IReproductionDefinitionCatalog definitions, DungeonGameRestoreReport report)
    {
        try
        {
            _ = ReproductionWorldAggregate.Restore(source, definitions);
            if (source.processes.All(value => value.lastPublicationKey.Length == 0 && value.pendingTransitions.Count == 0)) return;
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("reproduction-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            var undelivered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("reproduction-canonical-row-null");
                var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
                    row.commitRevision, row.localResultIndex).ToString();
                Add(key, new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision,
                    localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
                if (incoming.outbox.Contains(row)) undelivered.Add(key);
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("reproduction-canonical-tombstone-null");
                Add(new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
                    row.commitRevision, row.localResultIndex).ToString(), row);
            }
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
                { new ReproductionProcessTransitionOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new ReproductionProcessTransitionOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var mapping = new ReproductionTransitionFactMapping((text, death) =>
            {
                string producer = death
                    ? MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.CharacterDeathEvent).ProducerId
                    : ReproductionProcessTransitionOutcomeIds.ProducerId;
                if (!GameplayDomainFactDeliveryQuery.TryReadKey(text, out var key) || key.ProducerId != producer)
                    throw new InvalidOperationException("reproduction-reference-producer-conflict");
                return Identity(RequireRow(text));
            });
            var observer = new GameplayDomainFactObserver<ReproductionTransitionFact, ReproductionProcessTransitionOutcomeReceipt>(
                mapping.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var process in source.processes)
            {
                var pending = process.pendingTransitions.SingleOrDefault();
                bool birthMarker = process.pendingBirth?.phase == ReproductionBirthTransactionPhase.PublicationPrepared;
                long lastRevision = process.transitionRevision - (pending != null || birthMarker ? 1L : 0L);
                if (process.lastPublicationKey.Length > 0)
                {
                    RequireProcessKey(process.lastPublicationKey, process.processId, lastRevision);
                    var prior = RequireRow(process.lastPublicationKey);
                    if (undelivered.Contains(process.lastPublicationKey)
                        || prior.ownerRevision != lastRevision
                        || Identity(prior).ToString() != process.lastPublicationId
                        || prior.canonicalHash != process.lastPublicationFingerprint)
                        throw new InvalidOperationException("reproduction-last-publication-proof-conflict");
                }
                if (pending == null) continue;
                var expected = ReproductionTransitionFactMapping.Key(pending.fact);
                if (pending.publicationKey != expected.ToString())
                    throw new InvalidOperationException("reproduction-pending-publication-key-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (rows.TryGetValue(pending.publicationKey, out var canonical))
                {
                    if (canonical.ownerRevision != pending.fact.revisionAfter)
                        throw new InvalidOperationException("reproduction-pending-owner-revision-conflict");
                    subset.tombstones.Add(canonical);
                }
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var restored = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(restored);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(pending.publicationKey,
                    new ReproductionTransitionFact(pending.fact), out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("reproduction-pending-canonical-payload-conflict:" + failure);
            }

            void Add(string key, GameplayOutcomeTombstoneSnapshot row)
            {
                if (!rows.TryAdd(key, row)) throw new InvalidOperationException("reproduction-canonical-identity-duplicate");
            }
            GameplayOutcomeTombstoneSnapshot RequireRow(string key)
            {
                if (!rows.TryGetValue(key, out var row) || !Identity(row).IsValid || string.IsNullOrWhiteSpace(row.canonicalHash))
                    throw new InvalidOperationException("reproduction-canonical-reference-missing");
                return row;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("reproduction-observation-save-join:" + exception.Message); }
    }

    private static GameplayOutcomeId Identity(GameplayOutcomeTombstoneSnapshot value) =>
        new(new GameplayOutcomeRunId(value.runId), value.sequence);

    private static void RequireProcessKey(string text, string processId, long revision)
    {
        if (!GameplayDomainFactDeliveryQuery.TryReadKey(text, out var key)
            || key.ProducerId != ReproductionProcessTransitionOutcomeIds.ProducerId
            || key.CommitRevision != revision || key.LocalResultIndex != 0
            || !new[] { "planned", "started", "daily-transition", "carrier-death", "emergency-extraction", "birth-published" }
                .Any(kind => ReproductionProcessTransitionOutcomeIds.OperationId(processId, kind, revision) == key.OperationId.Value))
            throw new InvalidOperationException("reproduction-last-publication-key-conflict");
    }
}
