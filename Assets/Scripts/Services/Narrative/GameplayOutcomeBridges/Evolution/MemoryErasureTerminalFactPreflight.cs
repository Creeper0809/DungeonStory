using System;
using System.Linq;
using DungeonStory.Foundation;

// Publication identities remain an observation/save concern; gameplay owns only the frozen fact.
public sealed class MemoryErasureTerminalFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value =>
                value?.sectionId == MemoryErasurePublicationSaveSection.Id);
            if (section == null
                || section.sectionVersion
                    != MemoryErasurePublicationSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "memory-erasure-publication-section-missing-or-version-unsupported");
            }

            MemoryErasurePublicationSaveSection.ValidateJson(section.payloadJson);
            MemoryErasurePublicationSaveData journal =
                MemoryErasurePublicationRuntime.ValidateAndClone(
                    DungeonSaveJson.Deserialize<MemoryErasurePublicationSaveData>(
                        section.payloadJson));

            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    GameplayOutcomeLedgerSaveSection.Id,
                    out GameplayOutcomeLedgerSaveData ledger))
            {
                throw new InvalidOperationException(
                    "memory-erasure-publication-ledger-section-missing");
            }

            foreach (MemoryErasurePublicationData observation
                in journal.observations)
            {
                ValidateObservation(observation, ledger);
            }

            if (!DungeonSaveSectionPayload.TryRead(save, PhysicalItemsSaveSection.Id,
                    out DungeonPhysicalItemSaveData physical)
                || physical.pendingBatchDispositions == null)
                throw new InvalidOperationException("memory-erasure-physical-section-missing");

            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    CharacterWorldSaveSection.Id,
                    out DungeonCharacterWorldSaveData characters)
                || characters.actors == null)
            {
                throw new InvalidOperationException(
                    "memory-erasure-publication-character-section-missing");
            }

            foreach (MemoryErasureTerminalFactData fact in journal.observations
                .Select(value => value.fact)
                .Where(value => value.status == MemoryErasureSealUseStatus.Succeeded))
            {
                // Successful observations are retained only after ACK. The item owner retires that
                // receipt at ACK; a remaining receipt would contradict the committed domain phase.
                if (physical.pendingBatchDispositions.Any(value => value != null
                    && (value.commitId == fact.physicalCommitId || value.operationId == fact.operationId)))
                    throw new InvalidOperationException("memory-erasure-success-physical-ack-conflict");
                DungeonCharacterSaveData actor = characters.actors.SingleOrDefault(
                    value => value != null
                        && value.persistentId == fact.characterId);
                if (actor == null)
                    continue;
                if (actor.carryInventory?.items?.Any(value => value != null && value.quantity > 0
                        && value.ownerOperationId == fact.operationId) == true)
                    throw new InvalidOperationException("memory-erasure-success-cargo-conflict");
                if (actor.acquiredTraits == null
                    || actor.acquiredTraits.revision < fact.committedRevision)
                {
                    throw new InvalidOperationException(
                        "memory-erasure-publication-owner-revision-behind");
                }

                CharacterAcquiredTraitInstanceState selected =
                    actor.acquiredTraits.instances?.SingleOrDefault(value =>
                        value != null
                        && value.instanceId == fact.traitInstanceId);
                if (selected == null
                    || !selected.erased
                    || selected.erasureAuditId != fact.auditId
                    || selected.erasedRevision != fact.committedRevision
                    || selected.erasedAt != fact.absoluteHour)
                {
                    throw new InvalidOperationException(
                        "memory-erasure-publication-erased-trait-conflict");
                }
            }
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException
            or Newtonsoft.Json.JsonException)
        {
            report.AddError("memory-erasure-publication-save-join:" + error.Message);
        }
    }

    public static void ValidateObservation(
        MemoryErasurePublicationData observation,
        GameplayOutcomeLedgerSaveData incoming)
    {
        if (observation?.fact == null
            || incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "memory-erasure-publication-observation-save-missing");
        }

        var single = new MemoryErasurePublicationSaveData
        {
            nextRevision = checked(observation.fact.terminalRevision + 1L)
        };
        single.observations.Add(observation);
        MemoryErasurePublicationData saved =
            MemoryErasurePublicationRuntime.ValidateAndClone(single)
                .observations.Single();
        GameplayResultKey key = MemoryErasureTerminalFactMapping.ResultKey(
            saved.fact);
        bool hasPublicationKey = !string.IsNullOrEmpty(saved.publicationKey);
        if (hasPublicationKey && saved.publicationKey != key.ToString())
        {
            throw new InvalidOperationException(
                "memory-erasure-publication-key-conflict");
        }
        if (!hasPublicationKey && !saved.pending)
        {
            throw new InvalidOperationException(
                "memory-erasure-publication-acknowledged-key-missing");
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
                "memory-erasure-publication-canonical-duplicate");
        }

        GameplayOutcomeSnapshot live = liveRows.SingleOrDefault();
        GameplayOutcomeTombstoneSnapshot canonical = live == null
            ? retiredRows.SingleOrDefault()
            : RetiredCopy(live);
        if (canonical == null && !saved.pending)
        {
            throw new InvalidOperationException(
                "memory-erasure-publication-acknowledged-canonical-missing");
        }
        if (!hasPublicationKey && canonical != null)
        {
            throw new InvalidOperationException(
                "memory-erasure-publication-unreserved-canonical-conflict");
        }

        if (canonical != null
            && (canonical.runId != incoming.runId
                || canonical.sequence < 0
                || canonical.sequence >= incoming.nextSequence
                || canonical.ownerRevision != 0L
                || canonical.absoluteDay != saved.fact.absoluteDay
                || !saved.pending
                    && (saved.publicationId
                            != new GameplayOutcomeId(
                                new GameplayOutcomeRunId(canonical.runId),
                                canonical.sequence).ToString()
                        || saved.fingerprint != canonical.canonicalHash)))
        {
            throw new InvalidOperationException(
                "memory-erasure-publication-canonical-proof-conflict");
        }

        MemoryErasureOutcomeReceipt receipt =
            MemoryErasureTerminalFactMapping.Capture(
                new MemoryErasureTerminalFact(saved.fact));
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new MemoryErasureOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new MemoryErasureOutcomeAdapter()
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
                "memory-erasure-publication-canonical-fact-conflict:"
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
