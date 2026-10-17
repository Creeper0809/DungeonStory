using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Join immutable social history with its canonical observations, not today's mutable mood.</summary>
public sealed class SocialInteractionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(row => row?.sectionId == SocialInteractionPublicationSaveSection.Id);
            if (section == null || section.sectionVersion != SocialInteractionPublicationSaveData.CurrentVersion)
                throw new InvalidOperationException("social-interaction-save-section-missing");
            SocialInteractionPublicationSaveSection.ValidateJson(section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger))
                throw new InvalidOperationException("social-interaction-ledger-missing");
            ValidateJournal(DungeonSaveJson.Deserialize<SocialInteractionPublicationSaveData>(section.payloadJson), ledger);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("social-interaction-save-join:" + error.Message); }
    }

    public static void ValidateJournal(SocialInteractionPublicationSaveData source, GameplayOutcomeLedgerSaveData incoming)
    {
        var journal = SocialInteractionPublicationRuntime.ValidateAndClone(source);
        if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("social-interaction-ledger-invalid");
        var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes)) Add(RetiredCopy(row));
        foreach (var row in incoming.tombstones) Add(row);
        var expected = new HashSet<string>(journal.observations.Select(row => row.publicationKey), StringComparer.Ordinal);
        // Combat also owns conflict parent results. Apology has this sole owner.
        foreach (var pair in canonical)
            if (pair.Value.producerId == SocialLifeOutcomeIds.ApologyProducerId && !expected.Contains(pair.Key))
                throw new InvalidOperationException("social-apology-canonical-owner-missing");

        var josa = new KoreanJosaFormatter();
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { new SocialConflictOutcomeDescriptor(josa), new ApologyOutcomeDescriptor(josa) },
            new IGameplayOutcomeAdapterRegistration[] { new SocialConflictOutcomeAdapter(), new ApologyOutcomeAdapter() });
        var comparison = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(comparison, registry, new GameEventBus());
        foreach (var saved in journal.observations)
        {
            var key = SocialInteractionFactMapping.ResultKey(saved.fact);
            if (key.ToString() != saved.publicationKey) throw new InvalidOperationException("social-interaction-key-conflict");
            canonical.TryGetValue(saved.publicationKey, out var row);
            if (row == null && !saved.pending) throw new InvalidOperationException("social-interaction-acknowledged-result-missing");
            if (row != null && (row.runId != incoming.runId || row.sequence <= 0 || row.sequence >= incoming.nextSequence
                || row.ownerRevision != 0L || row.absoluteDay != saved.fact.absoluteDay
                || !saved.pending && (saved.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(row.runId), row.sequence).ToString()
                    || saved.fingerprint != row.canonicalHash)))
                throw new InvalidOperationException("social-interaction-proof-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (row != null) subset.tombstones.Add(row);
            if (saved.fact.kind == SocialInteractionKind.Apology)
            {
                var cause = SocialOffenseHistory.ParsePublicationId(saved.fact.offendingPublicationId);
                var parent = canonical.Values.SingleOrDefault(value => value.runId == cause.RunId.Value && value.sequence == cause.Sequence);
                if (parent == null || parent.producerId != SocialLifeOutcomeIds.ConflictProducerId)
                    throw new InvalidOperationException("social-apology-parent-missing");
                if (row == null || parent.sequence != row.sequence) subset.tombstones.Add(parent);
            }
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = comparison.PrepareGameplayOutcomeRestore(subset);
            comparison.BeginRestoreCandidate(); comparison.PublishGameplayOutcomeRestore(candidate);
            comparison.PublishRestoreCandidate(); comparison.CompleteRestoreCandidate();
            OutcomePrepareResult result;
            PreparedOutcomeToken token;
            if (saved.fact.kind == SocialInteractionKind.Apology)
                result = recorder.TryPrepare(SocialInteractionFactMapping.Capture(new SocialApologyFact(saved.fact)), out token);
            else result = recorder.TryPrepare(SocialInteractionFactMapping.Capture(new SocialConflictFact(saved.fact)), out token);
            recorder.CancelPrepared(token);
            if (row == null ? !result.Success : result.Code != OutcomePrepareCode.AlreadyTerminal)
                throw new InvalidOperationException("social-interaction-canonical-fact-conflict:" + result.DetailCode);
        }
        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null) throw new InvalidOperationException("social-interaction-canonical-null");
            var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex);
            if (!canonical.TryAdd(key.ToString(), row)) throw new InvalidOperationException("social-interaction-canonical-duplicate");
        }
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row)
    {
        if (row == null) throw new InvalidOperationException("social-interaction-canonical-null");
        return new GameplayOutcomeTombstoneSnapshot
        {
            runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
            commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
            temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
            parentRunId = row.parentRunId, parentSequence = row.parentSequence, deliveryMetadataVersion = row.deliveryMetadataVersion,
            deliveryClockId = row.deliveryClockId, recordedAbsoluteHour = row.recordedAbsoluteHour,
            publishedRevision = row.publishedRevision, deliveryFaultCount = row.deliveryFaultCount,
            deliveryDelayReason = row.deliveryDelayReason, canonicalHash = row.immutablePayloadHash,
            terminalTier = NarrativeMemoryTier.Forgotten
        };
    }
}
