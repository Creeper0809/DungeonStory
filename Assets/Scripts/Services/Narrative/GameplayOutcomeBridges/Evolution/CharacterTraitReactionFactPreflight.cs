using System;
using System.Linq;
using DungeonStory.Foundation;

// Publication identities remain an observation/save concern; gameplay owns only the frozen fact.
public sealed class CharacterTraitReactionFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value =>
                value?.sectionId == CharacterTraitReactionPublicationSaveSection.Id);
            if (section == null
                || section.sectionVersion
                    != CharacterTraitReactionPublicationSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "trait-reaction-publication-section-missing-or-version-unsupported");
            }

            CharacterTraitReactionPublicationSaveSection.ValidateJson(
                section.payloadJson);
            CharacterTraitReactionPublicationSaveData journal =
                CharacterTraitReactionPublicationRuntime.ValidateAndClone(
                    DungeonSaveJson.Deserialize<
                        CharacterTraitReactionPublicationSaveData>(
                            section.payloadJson));

            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    GameplayOutcomeLedgerSaveSection.Id,
                    out GameplayOutcomeLedgerSaveData ledger))
            {
                throw new InvalidOperationException(
                    "trait-reaction-publication-ledger-section-missing");
            }

            foreach (CharacterTraitReactionPublicationData observation
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
                    "trait-reaction-publication-character-section-missing");
            }

            foreach (var history in journal.observations
                .GroupBy(value => value.fact.characterId, StringComparer.Ordinal))
            {
                int latestRevision = history.Max(value => value.fact.afterRevision);
                DungeonCharacterSaveData actor = characters.actors.SingleOrDefault(
                    value => value != null
                        && value.persistentId == history.Key);
                if (actor != null
                    && (actor.acquiredTraits == null
                        || actor.acquiredTraits.revision < latestRevision))
                {
                    throw new InvalidOperationException(
                        "trait-reaction-publication-owner-revision-behind");
                }
            }
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException
            or Newtonsoft.Json.JsonException)
        {
            report.AddError(
                "trait-reaction-publication-save-join:" + error.Message);
        }
    }

    public static void ValidateObservation(
        CharacterTraitReactionPublicationData observation,
        GameplayOutcomeLedgerSaveData incoming)
    {
        if (observation == null
            || incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "trait-reaction-publication-observation-save-missing");
        }

        var single = new CharacterTraitReactionPublicationSaveData();
        single.observations.Add(observation);
        CharacterTraitReactionPublicationData saved =
            CharacterTraitReactionPublicationRuntime.ValidateAndClone(single)
                .observations.Single();
        GameplayResultKey key = CharacterTraitReactionFactMapping.ResultKey(
            saved.fact);
        if (saved.publicationKey != key.ToString())
        {
            throw new InvalidOperationException(
                "trait-reaction-publication-key-conflict");
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
                "trait-reaction-publication-canonical-duplicate");
        }

        GameplayOutcomeSnapshot live = liveRows.SingleOrDefault();
        GameplayOutcomeTombstoneSnapshot canonical = live == null
            ? retiredRows.SingleOrDefault()
            : RetiredCopy(live);
        if (canonical == null && !saved.pending)
        {
            throw new InvalidOperationException(
                "trait-reaction-publication-acknowledged-canonical-missing");
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
                "trait-reaction-publication-canonical-proof-conflict");
        }

        AcquiredTraitReactionOutcomeReceipt receipt =
            CharacterTraitReactionFactMapping.Capture(
                new CharacterTraitReactionFact(saved.fact));
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new AcquiredTraitReactionOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new AcquiredTraitReactionOutcomeAdapter()
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
                "trait-reaction-publication-canonical-fact-conflict:"
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
