using System;
using System.Linq;
using DungeonStory.Foundation;

public sealed class MemoryErasureBossAwardFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value =>
                value?.sectionId == MemoryErasureBossAwardPublicationSaveSection.Id);
            if (section == null
                || section.sectionVersion
                    != MemoryErasureBossAwardPublicationSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "memory-erasure-boss-award-publication-section-missing-or-version-unsupported");
            }

            MemoryErasureBossAwardPublicationSaveSection.ValidateJson(
                section.payloadJson);
            MemoryErasureBossAwardPublicationSaveData journal =
                MemoryErasureBossAwardPublicationRuntime.ValidateAndClone(
                    DungeonSaveJson.Deserialize<
                        MemoryErasureBossAwardPublicationSaveData>(
                            section.payloadJson));

            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    GameplayOutcomeLedgerSaveSection.Id,
                    out GameplayOutcomeLedgerSaveData ledger))
            {
                throw new InvalidOperationException(
                    "memory-erasure-boss-award-ledger-section-missing");
            }
            foreach (MemoryErasureBossAwardPublicationData observation
                in journal.observations)
            {
                ValidateObservation(observation, ledger);
            }

            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    OffenseAggregateSaveSection.Id,
                    out DungeonOffenseAggregateSaveData offense)
                || offense == null
                || offense.version != DungeonOffenseAggregateSaveData.CurrentVersion
                || offense.regions?.version != DungeonOffenseRegionSaveData.CurrentVersion
                || offense.regions?.regions == null)
            {
                throw new InvalidOperationException(
                    "memory-erasure-boss-award-offense-section-missing");
            }

            foreach (MemoryErasureBossAwardPublicationData observation
                in journal.observations)
            {
                MemoryErasureBossAwardFactData fact = observation.fact;
                OffenseRegionState region = offense.regions.regions.SingleOrDefault(
                    value => value != null
                        && value.regionId == fact.regionId
                        && value.memoryErasureSealAwardOperationId
                            == fact.operationId);
                if (region == null)
                {
                    throw new InvalidOperationException(
                        "memory-erasure-boss-award-region-claim-missing");
                }
            }

            foreach (OffenseRegionState region in offense.regions.regions
                .Where(value => value?.memoryErasureSealAwardPublished == true))
            {
                MemoryErasureBossAwardPublicationData delivered =
                    journal.observations.SingleOrDefault(value =>
                        value.fact.regionId == region.regionId
                        && value.fact.operationId
                            == region.memoryErasureSealAwardOperationId
                        && value.delivered);
                if (delivered?.fact == null
                    || string.IsNullOrEmpty(delivered.fact.physicalCommitId))
                {
                    throw new InvalidOperationException(
                        "memory-erasure-boss-award-published-proof-missing");
                }
            }
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException
            or Newtonsoft.Json.JsonException)
        {
            report.AddError(
                "memory-erasure-boss-award-save-join:" + error.Message);
        }
    }

    public static void ValidateObservation(
        MemoryErasureBossAwardPublicationData observation,
        GameplayOutcomeLedgerSaveData incoming)
    {
        if (observation?.fact == null
            || incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "memory-erasure-boss-award-observation-save-missing");
        }

        var single = new MemoryErasureBossAwardPublicationSaveData();
        single.observations.Add(observation);
        MemoryErasureBossAwardPublicationData saved =
            MemoryErasureBossAwardPublicationRuntime.ValidateAndClone(single)
                .observations.Single();
        if (string.IsNullOrEmpty(saved.fact.physicalCommitId))
        {
            if (saved.delivered
                || saved.publicationKey.Length != 0
                || saved.publicationId.Length != 0
                || saved.fingerprint.Length != 0)
            {
                throw new InvalidOperationException(
                    "memory-erasure-boss-award-draft-proof-conflict");
            }
            bool hasCanonical = incoming.outbox
                    .Concat(incoming.exactOutcomes)
                    .Any(row => row != null
                        && row.producerId
                            == EvolutionOutcomeIds.MemoryErasureBossAwardProducerId
                        && row.operationId == saved.fact.operationId
                        && row.commitRevision == 0L
                        && row.localResultIndex == 0)
                || incoming.tombstones.Any(row => row != null
                    && row.producerId
                        == EvolutionOutcomeIds.MemoryErasureBossAwardProducerId
                    && row.operationId == saved.fact.operationId
                    && row.commitRevision == 0L
                    && row.localResultIndex == 0);
            if (hasCanonical)
            {
                throw new InvalidOperationException(
                    "memory-erasure-boss-award-draft-canonical-conflict");
            }
            return;
        }

        GameplayResultKey key = MemoryErasureBossAwardFactMapping.ResultKey(
            saved.fact);
        bool hasPublicationKey = saved.publicationKey.Length != 0;
        if (hasPublicationKey && saved.publicationKey != key.ToString())
        {
            throw new InvalidOperationException(
                "memory-erasure-boss-award-publication-key-conflict");
        }

        GameplayOutcomeSnapshot[] liveRows = incoming.outbox
            .Concat(incoming.exactOutcomes)
            .Where(row => row != null
                && row.producerId == key.ProducerId
                && row.operationId == key.OperationId.Value
                && row.commitRevision == key.CommitRevision
                && row.localResultIndex == key.LocalResultIndex)
            .ToArray();
        GameplayOutcomeTombstoneSnapshot[] retiredRows = incoming.tombstones
            .Where(row => row != null
                && row.producerId == key.ProducerId
                && row.operationId == key.OperationId.Value
                && row.commitRevision == key.CommitRevision
                && row.localResultIndex == key.LocalResultIndex)
            .ToArray();
        if (liveRows.Length + retiredRows.Length > 1)
        {
            throw new InvalidOperationException(
                "memory-erasure-boss-award-canonical-duplicate");
        }

        GameplayOutcomeSnapshot live = liveRows.SingleOrDefault();
        GameplayOutcomeTombstoneSnapshot canonical = live == null
            ? retiredRows.SingleOrDefault()
            : RetiredCopy(live);
        if (canonical == null && saved.delivered)
        {
            throw new InvalidOperationException(
                "memory-erasure-boss-award-delivered-canonical-missing");
        }
        if (!hasPublicationKey && canonical != null)
        {
            throw new InvalidOperationException(
                "memory-erasure-boss-award-unreserved-canonical-conflict");
        }
        if (canonical != null
            && (canonical.runId != incoming.runId
                || canonical.sequence < 0
                || canonical.sequence >= incoming.nextSequence
                || canonical.ownerRevision != 0L
                || canonical.absoluteDay != saved.fact.absoluteDay
                || saved.delivered
                    && (saved.publicationId
                            != new GameplayOutcomeId(
                                new GameplayOutcomeRunId(canonical.runId),
                                canonical.sequence).ToString()
                        || saved.fingerprint != canonical.canonicalHash)))
        {
            throw new InvalidOperationException(
                "memory-erasure-boss-award-canonical-proof-conflict");
        }

        MemoryErasureBossAwardOutcomeReceipt receipt =
            MemoryErasureBossAwardFactMapping.Capture(
                new MemoryErasureBossAwardFact(saved.fact));
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new MemoryErasureBossAwardOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new MemoryErasureBossAwardOutcomeAdapter()
            });
        var comparison = new GameplayOutcomeLedger(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 1,
                largePageCount: 1,
                knownResultKeyCapacity: 4,
                retainedSmallPageCount: 1,
                retainedLargePageCount: 1));
        var subset = new GameplayOutcomeLedgerSaveData
        {
            runId = incoming.runId,
            worldEpoch = incoming.worldEpoch,
            revision = incoming.revision,
            nextSequence = incoming.nextSequence,
            knownResultKeyHighWater = canonical == null ? 0 : 1
        };
        if (canonical != null)
            subset.tombstones.Add(canonical);

        GameplayOutcomeLedgerRestoreCandidate candidate =
            comparison.PrepareGameplayOutcomeRestore(subset);
        comparison.BeginRestoreCandidate();
        comparison.PublishGameplayOutcomeRestore(candidate);
        comparison.PublishRestoreCandidate();
        comparison.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(
            comparison,
            registry,
            new GameEventBus());
        OutcomePrepareResult result = recorder.TryPrepare(receipt, out var token);
        recorder.CancelPrepared(token);
        if (canonical == null
            ? !result.Success
            : result.Code != OutcomePrepareCode.AlreadyTerminal)
        {
            throw new InvalidOperationException(
                "memory-erasure-boss-award-canonical-fact-conflict:"
                + result.DetailCode);
        }
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(
        GameplayOutcomeSnapshot row) => new()
    {
        runId = row.runId,
        sequence = row.sequence,
        producerId = row.producerId,
        operationId = row.operationId,
        commitRevision = row.commitRevision,
        localResultIndex = row.localResultIndex,
        ownerRevision = row.ownerRevision,
        temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
        absoluteDay = row.absoluteDay,
        parentRunId = row.parentRunId,
        parentSequence = row.parentSequence,
        deliveryMetadataVersion = row.deliveryMetadataVersion,
        deliveryClockId = row.deliveryClockId,
        recordedAbsoluteHour = row.recordedAbsoluteHour,
        publishedRevision = row.publishedRevision,
        deliveryFaultCount = row.deliveryFaultCount,
        deliveryDelayReason = row.deliveryDelayReason,
        canonicalHash = row.immutablePayloadHash,
        terminalTier = NarrativeMemoryTier.Forgotten
    };
}
