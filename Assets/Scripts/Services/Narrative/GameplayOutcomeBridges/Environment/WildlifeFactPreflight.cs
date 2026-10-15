using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// Only the observer interprets the opaque identities stored by wildlife owners.
public sealed class WildlifeFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(
                DungeonSaveSectionPayload.ReadOrNew<DungeonWildlifeSaveData>(save, WildlifeSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<CircusSaveData>(save, CircusSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("wildlife-fact-save-join:" + exception.Message); }
    }

    public static void Validate(DungeonWildlifeSaveData wildlife, CircusSaveData circus, GameplayOutcomeLedgerSaveData incoming)
    {
        if (wildlife?.gameplayOutcomes == null || circus?.wildlifeGameplayOutcomes == null
            || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("wildlife-fact-save-state-missing");
        ValidateOwner(wildlife.gameplayOutcomes, wildlife.nextGameplayOutcomeSequence);
        ValidateOwner(circus.wildlifeGameplayOutcomes, circus.nextWildlifeGameplayOutcomeSequence);
        if (wildlife.gameplayOutcomes.Count + circus.wildlifeGameplayOutcomes.Count == 0) return;
        var josa = new KoreanJosaFormatter();
        var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
        { new WildlifeCommandOutcomeDescriptor(josa), new WildlifeActionOutcomeDescriptor(josa) },
            new IGameplayOutcomeAdapterRegistration[] { new WildlifeCommandOutcomeAdapter(), new WildlifeActionOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var joined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in wildlife.gameplayOutcomes.Concat(circus.wildlifeGameplayOutcomes))
        {
            if (!joined.Add(row.publicationKey))
                throw new InvalidOperationException("wildlife-fact-duplicate-owner-publication");
            if (row.profile == WildlifeGameplayOutcomeProfile.Command)
                Check(row, WildlifeFactMapping.Capture(new WildlifeCommandFact(row)));
            else Check(row, WildlifeFactMapping.Capture(new WildlifeActionFact(row)));
        }

        void Check<TReceipt>(WildlifeGameplayOutcomeSaveData row, TReceipt receipt) where TReceipt : IEnvironmentOutcomeReceipt
        {
            var key = receipt.Payload.ResultKey;
            if (key.ToString() != row.publicationKey)
                throw new InvalidOperationException("wildlife-fact-publication-identity-conflict");
            var live = incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
                && value.producerId == key.ProducerId && value.operationId == key.OperationId.Value).ToArray();
            var retired = incoming.tombstones.Where(value => value != null
                && value.producerId == key.ProducerId && value.operationId == key.OperationId.Value).ToArray();
            if (live.Length + retired.Length > 1)
                throw new InvalidOperationException("wildlife-fact-canonical-duplicate");
            var canonical = retired.FirstOrDefault();
            if (live.Length == 1)
            {
                var value = live[0];
                if (value.outcomeTypeId != receipt.Payload.OutcomeTypeId.Value)
                    throw new InvalidOperationException("wildlife-fact-canonical-type-conflict");
                canonical = new GameplayOutcomeTombstoneSnapshot
                {
                    runId = value.runId, sequence = value.sequence, producerId = value.producerId,
                    operationId = value.operationId, commitRevision = value.commitRevision,
                    localResultIndex = value.localResultIndex, ownerRevision = value.ownerRevision,
                    temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = value.absoluteDay,
                    parentRunId = value.parentRunId, parentSequence = value.parentSequence,
                    deliveryMetadataVersion = value.deliveryMetadataVersion, deliveryClockId = value.deliveryClockId,
                    recordedAbsoluteHour = value.recordedAbsoluteHour, publishedRevision = value.publishedRevision,
                    deliveryFaultCount = value.deliveryFaultCount, deliveryDelayReason = value.deliveryDelayReason,
                    canonicalHash = value.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                };
            }
            if (canonical != null && (canonical.runId != incoming.runId || canonical.sequence < 0
                || canonical.sequence >= incoming.nextSequence || canonical.commitRevision != row.ownerRevision
                || canonical.ownerRevision != row.ownerRevision || canonical.localResultIndex != 0))
                throw new InvalidOperationException("wildlife-fact-canonical-revision-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (canonical != null) { subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1; }
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            var result = recorder.TryPrepare(receipt, out var token);
            recorder.CancelPrepared(token);
            if (canonical == null ? !result.Success : result.Code != OutcomePrepareCode.AlreadyTerminal)
                throw new InvalidOperationException("wildlife-fact-canonical-payload-conflict:" + result.DetailCode);
        }
    }

    private static void ValidateOwner(IReadOnlyList<WildlifeGameplayOutcomeSaveData> rows, int nextSequence)
    {
        if (nextSequence < 0 || rows.Count > 128)
            throw new InvalidOperationException("wildlife-fact-owner-shape-invalid");
        long previous = 0;
        foreach (var row in rows)
        {
            if (row == null) throw new InvalidOperationException("wildlife-fact-owner-row-null");
            row.Validate();
            if (string.IsNullOrWhiteSpace(row.publicationKey) || row.ownerRevision <= previous || row.ownerRevision > nextSequence)
                throw new InvalidOperationException("wildlife-fact-owner-sequence-invalid");
            previous = row.ownerRevision;
        }
    }
}
