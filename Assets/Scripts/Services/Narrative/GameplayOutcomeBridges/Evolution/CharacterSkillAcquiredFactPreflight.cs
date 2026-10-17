using System;
using System.Linq;
using DungeonStory.Foundation;

// Publication identities remain an observation/save concern; gameplay owns only the frozen fact.
public sealed class CharacterSkillAcquiredFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value =>
                value?.sectionId == CharacterSkillAcquisitionPublicationSaveSection.Id);
            if (section == null
                || section.sectionVersion
                    != CharacterSkillAcquisitionPublicationSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "skill-acquisition-publication-section-missing-or-version-unsupported");
            }

            CharacterSkillAcquisitionPublicationSaveSection.ValidateJson(
                section.payloadJson);
            CharacterSkillAcquisitionPublicationSaveData journal =
                CharacterSkillAcquisitionPublicationRuntime.ValidateAndClone(
                    DungeonSaveJson.Deserialize<
                        CharacterSkillAcquisitionPublicationSaveData>(
                            section.payloadJson));

            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    GameplayOutcomeLedgerSaveSection.Id,
                    out GameplayOutcomeLedgerSaveData ledger))
            {
                throw new InvalidOperationException(
                    "skill-acquisition-publication-ledger-section-missing");
            }

            foreach (CharacterSkillAcquisitionPublicationData observation
                in journal.observations)
            {
                ValidateObservation(observation, ledger);
            }

            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    CharacterWorldSaveSection.Id,
                    out DungeonCharacterWorldSaveData characters)
                || characters.actors == null)
            {
                throw new InvalidOperationException(
                    "skill-acquisition-publication-character-section-missing");
            }

            foreach (var history in journal.observations
                .GroupBy(value => value.fact.characterId, StringComparer.Ordinal))
            {
                long latestRevision = history.Max(value => value.fact.afterRevision);
                DungeonCharacterSaveData actor = characters.actors.SingleOrDefault(
                    value => value != null
                        && value.persistentId == history.Key);
                if (actor != null
                    && (actor.growth == null
                        || actor.growth.skillAcquisitionRevision < latestRevision))
                {
                    throw new InvalidOperationException(
                        "skill-acquisition-publication-growth-revision-behind");
                }
            }
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException
            or Newtonsoft.Json.JsonException)
        {
            report.AddError(
                "skill-acquisition-publication-save-join:" + error.Message);
        }
    }

    public static void ValidateObservation(
        CharacterSkillAcquisitionPublicationData observation,
        GameplayOutcomeLedgerSaveData incoming)
    {
        if (observation == null
            || incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "skill-acquisition-publication-observation-save-missing");
        }

        var single = new CharacterSkillAcquisitionPublicationSaveData();
        single.observations.Add(observation);
        CharacterSkillAcquisitionPublicationData saved =
            CharacterSkillAcquisitionPublicationRuntime.ValidateAndClone(single)
                .observations.Single();
        GameplayResultKey key =
            CharacterSkillAcquiredFactMapping.ResultKey(saved.fact);
        if (saved.publicationKey != key.ToString())
        {
            throw new InvalidOperationException(
                "skill-acquisition-publication-key-conflict");
        }

        GameplayOutcomeSnapshot[] liveRows = incoming.outbox
            .Concat(incoming.exactOutcomes)
            .Where(row => row != null
                && row.producerId == key.ProducerId
                && row.operationId == key.OperationId.Value)
            .ToArray();
        GameplayOutcomeTombstoneSnapshot[] retiredRows = incoming.tombstones
            .Where(row => row != null
                && row.producerId == key.ProducerId
                && row.operationId == key.OperationId.Value)
            .ToArray();
        if (liveRows.Length + retiredRows.Length > 1)
        {
            throw new InvalidOperationException(
                "skill-acquisition-publication-canonical-duplicate");
        }

        GameplayOutcomeSnapshot live = liveRows.SingleOrDefault();
        GameplayOutcomeTombstoneSnapshot canonical = live == null
            ? retiredRows.SingleOrDefault()
            : RetiredCopy(live);
        if (canonical == null && !saved.pending)
        {
            throw new InvalidOperationException(
                "skill-acquisition-publication-acknowledged-canonical-missing");
        }

        if (canonical != null
            && (canonical.runId != incoming.runId
                || canonical.sequence < 0
                || canonical.sequence >= incoming.nextSequence
                || canonical.commitRevision != key.CommitRevision
                || canonical.ownerRevision != key.CommitRevision
                || canonical.localResultIndex != key.LocalResultIndex
                || canonical.absoluteDay != saved.fact.absoluteDay
                || !saved.pending
                    && (saved.publicationId
                            != new GameplayOutcomeId(
                                new GameplayOutcomeRunId(canonical.runId),
                                canonical.sequence).ToString()
                        || saved.fingerprint != canonical.canonicalHash)))
        {
            throw new InvalidOperationException(
                "skill-acquisition-publication-canonical-proof-conflict");
        }

        CharacterSkillAcquiredOutcomeReceipt receipt =
            CharacterSkillAcquiredFactMapping.Capture(
                new CharacterSkillAcquiredFact(saved.fact));
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new CharacterSkillAcquiredOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new CharacterSkillAcquiredOutcomeAdapter()
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
                "skill-acquisition-publication-canonical-fact-conflict:"
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
