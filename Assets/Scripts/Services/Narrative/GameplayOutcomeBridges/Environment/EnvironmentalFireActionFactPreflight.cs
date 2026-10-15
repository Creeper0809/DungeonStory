using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;

/// <summary>The observer, not the fire simulation, joins saved facts to native recording identities.</summary>
public sealed class EnvironmentalFireActionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value => value?.sectionId == EnvironmentalFireSaveSection.Id);
            if (section == null) throw new InvalidOperationException("fire-action-section-missing");
            EnvironmentalFireSaveSection.ValidateActionPublicationJson(JObject.Parse(section.payloadJson,
                new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }));
            var fire = DungeonSaveJson.Deserialize<DungeonEnvironmentalFireSaveData>(section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger))
                throw new InvalidOperationException("fire-action-ledger-missing");
            Validate(fire, ledger, report);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Newtonsoft.Json.JsonException)
        { report.AddError("fire-action-save-join:" + exception.Message); }
    }

    internal static void Validate(DungeonEnvironmentalFireSaveData fire, GameplayOutcomeLedgerSaveData incoming, DungeonGameRestoreReport report)
    {
        try
        {
            if (fire?.version != DungeonEnvironmentalFireSaveData.CurrentVersion || fire.processedCauses == null
                || fire.suppressionOperations == null || fire.activeFires == null || fire.history == null
                || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("fire-action-owner-or-ledger-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            var undelivered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("fire-action-ledger-row-null");
                if (!Relevant(row.producerId)) continue;
                string key = Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex);
                Add(key, new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
                    commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.immutablePayloadHash, absoluteDay = row.absoluteDay, terminalTier = NarrativeMemoryTier.Forgotten
                });
                if (incoming.outbox.Contains(row)) undelivered.Add(key);
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("fire-action-ledger-tombstone-null");
                if (Relevant(row.producerId)) Add(Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex), row);
            }
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
                { new FireIgnitionOutcomeDescriptor(new KoreanJosaFormatter()), new FireSuppressionOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new EnvironmentalFireIgnitionOutcomeAdapter(), new EnvironmentalFireSuppressionOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var group = new GameplayDomainFactCommitGroup(recorder, ledger);
            var ignition = new GameplayDomainFactObserver<EnvironmentalFireIgnitedFact, EnvironmentalFireIgnitionOutcomeReceipt>(
                EnvironmentalFireActionFactMapping.Capture, registry, ledger, recorder, group);
            var suppression = new GameplayDomainFactObserver<EnvironmentalFireSuppressedFact, EnvironmentalFireSuppressionOutcomeReceipt>(
                EnvironmentalFireActionFactMapping.Capture, registry, ledger, recorder, group);
            var expectedKeys = new HashSet<string>(StringComparer.Ordinal);
            var fires = fire.activeFires.Concat(fire.history.Select(value => value?.fire))
                .ToDictionary(value => value?.fireId ?? throw new InvalidOperationException("fire-action-fire-null"), StringComparer.Ordinal);
            foreach (var owner in fire.processedCauses)
            {
                var fact = new EnvironmentalFireIgnitedFact(owner);
                string key = EnvironmentalFireActionFactMapping.Capture(fact).Payload.ResultKey.ToString();
                Bind(owner.publicationKey, key);
                if (owner.disposition == (int)EnvironmentalFireIgnitionDisposition.Ignited
                    && (!fires.TryGetValue(owner.fireId, out var created) || created.causeId != owner.causeId
                        || created.targetKind != owner.targetKind || created.targetId != owner.targetId
                        || created.ignitionKind != owner.ignitionKind || created.producerId != owner.producerId || created.evidenceId != owner.evidenceId))
                    throw new InvalidOperationException("fire-action-ignition-owner-conflict");
                RestoreSubset(key, owner.outcomeRevision, owner.absoluteDay, owner.outcomePending);
                bool accepted = ignition.TryPrepareReplay(key, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("fire-action-ignition-payload-conflict:" + failure);
            }
            foreach (var owner in fire.suppressionOperations)
            {
                if (owner == null) throw new InvalidOperationException("fire-action-suppression-null");
                string key = EnvironmentalFireCompletionIdentity.ChildKey(owner).ToString();
                Bind(owner.publicationKey, key);
                if (!fires.TryGetValue(owner.fireId, out var target) || target.targetId != owner.targetId)
                    throw new InvalidOperationException("fire-action-suppression-target-conflict");
                if (owner.phase != (int)EnvironmentalFireSuppressionPhase.Applied)
                {
                    if (rows.ContainsKey(key) || !string.IsNullOrEmpty(owner.completionPublicationKey) || owner.completionIdentityAcknowledged)
                        throw new InvalidOperationException("fire-action-unapplied-has-publication");
                    continue;
                }
                var fact = new EnvironmentalFireSuppressedFact(owner);
                RestoreSubset(key, owner.outcomeRevision, owner.absoluteDay, owner.outcomePending);
                bool accepted = suppression.TryPrepareReplay(key, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("fire-action-suppression-payload-conflict:" + failure);
                if (!string.IsNullOrEmpty(owner.completionPublicationKey))
                {
                    var request = EnvironmentalFireActionFactMapping.CreateRequest(new EnvironmentalFireSuppressionCompletedFact(owner));
                    if (owner.completionPublicationKey != WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString())
                        throw new InvalidOperationException("fire-action-completion-key-conflict");
                }
                else if (owner.completionIdentityAcknowledged)
                    throw new InvalidOperationException("fire-action-completion-binding-missing");
            }
            if (rows.Keys.Any(key => !expectedKeys.Contains(key)))
                throw new InvalidOperationException("fire-action-publication-without-owner");

            void Bind(string actual, string expected)
            {
                if (actual != expected || !expectedKeys.Add(expected))
                    throw new InvalidOperationException("fire-action-publication-binding-conflict");
            }
            void Add(string key, GameplayOutcomeTombstoneSnapshot row)
            { if (!rows.TryAdd(key, row)) throw new InvalidOperationException("fire-action-canonical-identity-duplicate"); }
            void RestoreSubset(string key, long revision, int day, bool pending)
            {
                bool exists = rows.TryGetValue(key, out var row);
                if (!pending && (!exists || undelivered.Contains(key))
                    || exists && (row.ownerRevision != revision || row.absoluteDay != day || string.IsNullOrEmpty(row.canonicalHash)))
                    throw new InvalidOperationException("fire-action-publication-delivery-conflict");
                var subset = new GameplayOutcomeLedgerSaveData { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (exists) subset.tombstones.Add(row);
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("fire-action-save-join:" + exception.Message); }
    }

    private static bool Relevant(string producer) =>
        producer == EnvironmentOutcomeIds.FireIgnitionProducer || producer == EnvironmentOutcomeIds.FireSuppressionProducer;
    private static string Key(string producer, string operation, long revision, int local) =>
        new GameplayResultKey(producer, new GameplayOperationId(operation), revision, local).ToString();
}

