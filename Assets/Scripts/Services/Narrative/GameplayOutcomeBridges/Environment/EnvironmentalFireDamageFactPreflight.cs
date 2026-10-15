using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;

public sealed class EnvironmentalFireDamageFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var envelope = save?.sections?.SingleOrDefault(value => value?.sectionId == EnvironmentalFireSaveSection.Id);
            if (envelope == null) throw new InvalidOperationException("fire-damage-section-missing");
            EnvironmentalFireSaveSection.ValidateDamagePublicationJson(JObject.Parse(envelope.payloadJson));
            var fire = DungeonSaveJson.Deserialize<DungeonEnvironmentalFireSaveData>(envelope.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger))
                throw new InvalidOperationException("fire-damage-ledger-section-missing");
            Validate(fire.damageOperations, ledger, report);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Newtonsoft.Json.JsonException)
        { report.AddError("fire-damage-observation-save-join:" + exception.Message); }
    }

    internal static void Validate(IReadOnlyList<EnvironmentalFireDamageRecord> source,
        GameplayOutcomeLedgerSaveData incoming, DungeonGameRestoreReport report)
    {
        try
        {
            if (source == null) throw new InvalidOperationException("fire-damage-owners-missing");
            var operations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in source)
            {
                EnvironmentalFireDamageFact.ValidateRecord(record);
                if (!operations.Add(record.operationId)) throw new InvalidOperationException("fire-damage-operation-duplicate");
            }
            if (source.Count == 0) return;
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("fire-damage-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            var undelivered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("fire-damage-canonical-row-null");
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
                Add(key, new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
                    commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
                if (incoming.outbox.Contains(row)) undelivered.Add(key);
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("fire-damage-canonical-tombstone-null");
                Add(new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString(), row);
            }
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
                { new FireDamageOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new EnvironmentalFireDamageOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<EnvironmentalFireDamageFact, EnvironmentalFireDamageOutcomeReceipt>(
                EnvironmentalFireDamageFactMapping.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger),
                EnvironmentalFireDamageFactMapping.Reserve);
            foreach (var record in source)
            {
                string expected = EnvironmentalFireDamageFactMapping.Key(record).ToString();
                if (record.publicationKey != expected) throw new InvalidOperationException("fire-damage-publication-key-conflict");
                bool exists = rows.TryGetValue(expected, out var canonical);
                if (record.phase is EnvironmentalFireDamagePhase.Prepared or EnvironmentalFireDamagePhase.AwaitingWorldRemoval)
                {
                    if (exists) throw new InvalidOperationException("fire-damage-unapplied-canonical-conflict");
                    continue;
                }
                if (record.phase == EnvironmentalFireDamagePhase.Delivered && (!exists || undelivered.Contains(expected)
                    || record.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(canonical.runId), canonical.sequence).ToString()
                    || record.publicationFingerprint != canonical.canonicalHash))
                    throw new InvalidOperationException("fire-damage-delivered-proof-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (exists)
                {
                    if (canonical.ownerRevision != record.ownerRevision || string.IsNullOrWhiteSpace(canonical.canonicalHash))
                        throw new InvalidOperationException("fire-damage-canonical-revision-conflict");
                    subset.tombstones.Add(canonical);
                }
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var restored = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(restored);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(expected, new EnvironmentalFireDamageFact(record), out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("fire-damage-canonical-payload-conflict:" + failure);
            }
            void Add(string key, GameplayOutcomeTombstoneSnapshot row)
            {
                if (!rows.TryAdd(key, row)) throw new InvalidOperationException("fire-damage-canonical-identity-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("fire-damage-observation-save-join:" + exception.Message); }
    }
}
