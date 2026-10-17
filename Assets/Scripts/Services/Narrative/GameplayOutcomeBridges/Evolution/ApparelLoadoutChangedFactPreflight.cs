using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

// Apparel identity is definition-level terminal truth. The journal joins that
// truth to the native outcome ledger without inventing a physical item owner.
public sealed class ApparelLoadoutChangedFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value =>
                value?.sectionId == ApparelChangePublicationSaveSection.Id);
            if (section == null
                || section.sectionVersion
                    != ApparelChangePublicationSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "apparel-change-publication-section-missing-or-version-unsupported");
            }

            ApparelChangePublicationSaveSection.ValidateJson(section.payloadJson);
            ApparelChangePublicationSaveData journal =
                DungeonSaveJson.Deserialize<ApparelChangePublicationSaveData>(section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    GameplayOutcomeLedgerSaveSection.Id,
                    out GameplayOutcomeLedgerSaveData ledger))
            {
                throw new InvalidOperationException(
                    "apparel-change-publication-ledger-section-missing");
            }

            ValidateJournal(journal, ledger);
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException
            or Newtonsoft.Json.JsonException)
        {
            report.AddError("apparel-change-publication-save-join:"
                + error.Message);
        }
    }

    public static void ValidateJournal(
        ApparelChangePublicationSaveData source,
        GameplayOutcomeLedgerSaveData ledger)
    {
        var journal = ApparelChangePublicationRuntime.ValidateAndClone(source);
        if (ledger?.outbox == null || ledger.exactOutcomes == null || ledger.tombstones == null)
            throw new InvalidOperationException("apparel-change-publication-ledger-missing");
        foreach (ApparelChangePublicationData observation
            in journal.observations)
        {
            ValidateObservation(observation, ledger);
        }
        ValidateCohorts(journal, ledger);
        // Current-only saves cannot drop completed history and restart its
        // sequence while the canonical ledger still owns those identities.
        var retainedKeys = journal.observations.Select(value =>
            ApparelLoadoutChangedFactMapping.ResultKey(value.fact).ToString()).ToHashSet(StringComparer.Ordinal);
        foreach (var row in ledger.outbox.Concat(ledger.exactOutcomes))
            if (row != null && row.producerId == EvolutionOutcomeIds.ApparelProducerId
                && !retainedKeys.Contains(new GameplayResultKey(row.producerId,
                    new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString()))
                throw new InvalidOperationException("apparel-change-journal-history-missing");
        foreach (var row in ledger.tombstones)
            if (row != null && row.producerId == EvolutionOutcomeIds.ApparelProducerId
                && !retainedKeys.Contains(new GameplayResultKey(row.producerId,
                    new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString()))
                throw new InvalidOperationException("apparel-change-journal-history-missing");
    }

    public static void ValidateObservation(
        ApparelChangePublicationData observation,
        GameplayOutcomeLedgerSaveData incoming)
    {
        if (observation?.fact == null
            || incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "apparel-change-publication-observation-save-missing");
        }

        observation.fact.Validate();
        if (!DomainFactValueSyntax.IsText(
                observation.publicationKey,
                1024,
                false)
            || (observation.pending
                ? observation.publicationId != string.Empty
                    || observation.fingerprint != string.Empty
                : !DomainFactValueSyntax.IsText(
                        observation.publicationId,
                        1024,
                        false)
                    || !IsHash(observation.fingerprint)))
        {
            throw new InvalidOperationException(
                "apparel-change-publication-proof-invalid");
        }

        ApparelChangePublicationData saved = observation.Clone();
        GameplayResultKey key = ApparelLoadoutChangedFactMapping.ResultKey(
            saved.fact);
        if (saved.publicationKey != key.ToString())
        {
            throw new InvalidOperationException(
                "apparel-change-publication-key-conflict");
        }

        GameplayOutcomeSnapshot[] liveRows = incoming.outbox
            .Concat(incoming.exactOutcomes)
            .Where(row => MatchesExact(row, key))
            .ToArray();
        GameplayOutcomeTombstoneSnapshot[] retiredRows = incoming.tombstones
            .Where(row => MatchesExact(row, key))
            .ToArray();
        if (liveRows.Length + retiredRows.Length > 1)
        {
            throw new InvalidOperationException(
                "apparel-change-publication-canonical-duplicate");
        }

        GameplayOutcomeSnapshot live = liveRows.SingleOrDefault();
        GameplayOutcomeTombstoneSnapshot canonical = live == null
            ? retiredRows.SingleOrDefault()
            : RetiredCopy(live);
        if (canonical == null && !saved.pending)
        {
            throw new InvalidOperationException(
                "apparel-change-publication-acknowledged-canonical-missing");
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
                "apparel-change-publication-canonical-proof-conflict");
        }

        ApparelChangeOutcomeReceipt receipt =
            ApparelLoadoutChangedFactMapping.Capture(
                new ApparelLoadoutChangedFact(saved.fact));
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new ApparelChangeOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new ApparelChangeOutcomeAdapter()
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
                "apparel-change-publication-canonical-fact-conflict:"
                + result.DetailCode);
        }
    }

    private static void ValidateCohorts(
        ApparelChangePublicationSaveData journal,
        GameplayOutcomeLedgerSaveData incoming)
    {
        foreach (var cohort in journal.observations.GroupBy(
            value => (value.fact.characterId, value.fact.transitionRevision)))
        {
            ApparelChangePublicationData first = cohort.First();
            GameplayResultKey firstKey =
                ApparelLoadoutChangedFactMapping.ResultKey(first.fact);
            GameplayOutcomeSnapshot[] liveRows = incoming.outbox
                .Concat(incoming.exactOutcomes)
                .Where(row => row != null
                    && row.producerId == firstKey.ProducerId
                    && row.operationId == firstKey.OperationId.Value)
                .ToArray();
            GameplayOutcomeTombstoneSnapshot[] retiredRows = incoming.tombstones
                .Where(row => row != null
                    && row.producerId == firstKey.ProducerId
                    && row.operationId == firstKey.OperationId.Value)
                .ToArray();
            int canonicalCount = liveRows.Length + retiredRows.Length;
            if (canonicalCount != 0 && canonicalCount != first.fact.changeCount)
            {
                throw new InvalidOperationException(
                    "apparel-change-publication-canonical-cohort-partial");
            }
            if (canonicalCount == 0)
                continue;

            foreach (ApparelChangePublicationData row in cohort)
            {
                GameplayResultKey key =
                    ApparelLoadoutChangedFactMapping.ResultKey(row.fact);
                int exactCount = liveRows.Count(value => MatchesExact(value, key))
                    + retiredRows.Count(value => MatchesExact(value, key));
                if (exactCount != 1)
                {
                    throw new InvalidOperationException(
                        "apparel-change-publication-canonical-cohort-key-conflict");
                }
            }
        }
    }

    private static bool MatchesExact(
        GameplayOutcomeSnapshot row,
        GameplayResultKey key) => row != null
        && row.producerId == key.ProducerId
        && row.operationId == key.OperationId.Value
        && row.commitRevision == key.CommitRevision
        && row.localResultIndex == key.LocalResultIndex;

    private static bool MatchesExact(
        GameplayOutcomeTombstoneSnapshot row,
        GameplayResultKey key) => row != null
        && row.producerId == key.ProducerId
        && row.operationId == key.OperationId.Value
        && row.commitRevision == key.CommitRevision
        && row.localResultIndex == key.LocalResultIndex;

    private static bool IsHash(string value) => value?.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F');

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
