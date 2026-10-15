using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

public sealed class ApparelPhysicalCompletedFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value =>
                value?.sectionId == ApparelPhysicalPublicationSaveSection.Id);
            if (section == null
                || section.sectionVersion
                    != ApparelPhysicalPublicationSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "apparel-physical-publication-section-missing-or-version-unsupported");
            }

            ApparelPhysicalPublicationSaveSection.ValidateJson(
                section.payloadJson);
            ApparelPhysicalPublicationSaveData journal =
                DungeonSaveJson.Deserialize<ApparelPhysicalPublicationSaveData>(
                    section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    GameplayOutcomeLedgerSaveSection.Id,
                    out GameplayOutcomeLedgerSaveData ledger))
            {
                throw new InvalidOperationException(
                    "apparel-physical-publication-ledger-section-missing");
            }
            ValidateJournal(journal, ledger);
            if (!DungeonSaveSectionPayload.TryRead(save, CharacterEnvironmentSaveSection.Id,
                out DungeonCharacterEnvironmentSaveData environment) || environment?.apparelWorkOrders == null)
                throw new InvalidOperationException("apparel-physical-owner-section-missing");
            ValidateOwners(journal, environment.apparelWorkOrders);
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException
            or Newtonsoft.Json.JsonException)
        {
            report.AddError("apparel-physical-publication-save-join:"
                + error.Message);
        }
    }

    // Active orders still carry their physical terminal proof. Historical facts outlive cleared/removed orders.
    public static void ValidateOwners(ApparelPhysicalPublicationSaveData source,
        IEnumerable<ApparelWorkOrderSaveData> orders)
    {
        var journal = ApparelPhysicalPublicationRuntime.ValidateAndClone(source);
        if (orders == null) throw new ArgumentException("apparel-physical-orders-missing");
        foreach (var order in orders)
        {
            if (order == null) throw new ArgumentException("apparel-physical-order-null");
            foreach (var kind in new[] { ApparelPhysicalChangeKind.Craft, ApparelPhysicalChangeKind.RejectedDismantle })
            {
                bool craft = kind == ApparelPhysicalChangeKind.Craft;
                var fact = journal.observations.SingleOrDefault(row => row.fact.orderId == order.orderId
                    && row.fact.attemptIndex == order.qualityAttemptIndex && row.fact.kind == (int)kind)?.fact;
                bool active = craft ? !string.IsNullOrEmpty(order.craftOutputBatchCommitId)
                    : order.dismantlingRejectedOutput;
                if (!active) continue;
                bool sale = order.craftRejectedBelowMinimum
                    && order.rejectedDisposition == RejectedOutputDisposition.MarkForSale
                    && order.craftResolvedQuality != CraftsmanshipQualityTier.Mythic;
                bool ready = craft
                    ? order.craftInputAcknowledged && order.craftOutputAcknowledged
                        && order.craftOutputPublished && order.craftAdmissionCommitted && (!sale || order.craftMarketRouted)
                    : order.rejectedOutputConsumed && order.rejectedDismantleAcknowledged
                        && order.rejectedRecoveryPublished && order.rejectedRecoveryAdmissionCommitted
                        && order.rejectedRecoveryOutputAcknowledged;
                if (fact == null)
                {
                    if (ready) throw new InvalidOperationException("apparel-physical-completed-owner-fact-missing");
                    continue;
                }
                if (!ready || fact.facilityId != order.facilityInstanceId
                    || fact.inputMassGrams != (craft ? order.craftInputMassGrams : order.rejectedDismantleInputMassGrams)
                    || fact.outputMassGrams != (craft ? order.craftOutputMassGrams : order.rejectedRecoveryOutputMassGrams)
                    || fact.outputStackId != (craft ? order.craftOutputStackId
                        : order.rejectedRecoveryStackIds?.FirstOrDefault() ?? string.Empty)
                    || fact.outputInstanceId != (craft ? order.craftOutputInstanceId : string.Empty)
                    || fact.outputItemId != (craft ? order.craftOutputCapability?.itemId : order.rejectedRecoveryItemId)
                    || craft && (fact.operationId != order.craftOutputBatchCommitId
                        || fact.hasProductQuality != (order.craftQualityOutcomeSchemaVersion == 1)
                        || fact.hasProductQuality && (fact.makerId != order.craftMakerCharacterId
                            || fact.makerName != order.craftMakerDisplayName
                            || fact.quality != (int)order.craftResolvedQuality
                            || fact.rejectedBelowMinimum != order.craftRejectedBelowMinimum)))
                    throw new InvalidOperationException("apparel-physical-fact-owner-conflict");
            }
        }
    }

    public static void ValidateJournal(
        ApparelPhysicalPublicationSaveData source,
        GameplayOutcomeLedgerSaveData incoming)
    {
        ApparelPhysicalPublicationSaveData journal =
            ApparelPhysicalPublicationRuntime.ValidateAndClone(source);
        if (incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "apparel-physical-publication-ledger-save-missing");
        }

        var expected = new HashSet<string>(
            journal.observations.Select(value =>
                ApparelPhysicalCompletedFactMapping.ResultKey(value.fact)
                    .ToString()),
            StringComparer.Ordinal);
        foreach (ApparelPhysicalPublicationData observation
            in journal.observations)
        {
            ValidateObservation(observation, incoming);
        }

        var canonical = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameplayOutcomeSnapshot row in incoming.outbox
            .Concat(incoming.exactOutcomes)
            .Where(value => value != null
                && value.producerId
                    == EvolutionOutcomeIds.ApparelPhysicalProducerId))
        {
            RequireOwnedCanonical(
                row.producerId,
                row.operationId,
                row.commitRevision,
                row.localResultIndex,
                expected,
                canonical);
        }
        foreach (GameplayOutcomeTombstoneSnapshot row in incoming.tombstones
            .Where(value => value != null
                && value.producerId
                    == EvolutionOutcomeIds.ApparelPhysicalProducerId))
        {
            RequireOwnedCanonical(
                row.producerId,
                row.operationId,
                row.commitRevision,
                row.localResultIndex,
                expected,
                canonical);
        }
    }

    public static void ValidateObservation(
        ApparelPhysicalPublicationData observation,
        GameplayOutcomeLedgerSaveData incoming)
    {
        if (observation?.fact == null
            || incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "apparel-physical-publication-observation-save-missing");
        }

        observation.fact.Validate(requireCompleted: true);
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
                "apparel-physical-publication-proof-invalid");
        }

        ApparelPhysicalPublicationData saved = observation.Clone();
        GameplayResultKey key =
            ApparelPhysicalCompletedFactMapping.ResultKey(saved.fact);
        if (saved.publicationKey != key.ToString())
        {
            throw new InvalidOperationException(
                "apparel-physical-publication-key-conflict");
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
                "apparel-physical-publication-canonical-duplicate");
        }

        GameplayOutcomeSnapshot live = liveRows.SingleOrDefault();
        GameplayOutcomeTombstoneSnapshot canonical = live == null
            ? retiredRows.SingleOrDefault()
            : RetiredCopy(live);
        if (canonical == null && !saved.pending)
        {
            throw new InvalidOperationException(
                "apparel-physical-publication-acknowledged-canonical-missing");
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
                "apparel-physical-publication-canonical-proof-conflict");
        }

        ApparelPhysicalOutcomeReceipt receipt =
            ApparelPhysicalCompletedFactMapping.Capture(
                new ApparelPhysicalCompletedFact(saved.fact));
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new ApparelPhysicalOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new ApparelPhysicalOutcomeAdapter()
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
                "apparel-physical-publication-canonical-fact-conflict:"
                + result.DetailCode);
        }
    }

    private static void RequireOwnedCanonical(
        string producerId,
        string operationId,
        long commitRevision,
        int localResultIndex,
        ISet<string> expected,
        ISet<string> canonical)
    {
        var key = new GameplayResultKey(
            producerId,
            new GameplayOperationId(operationId),
            commitRevision,
            localResultIndex);
        string opaque = key.ToString();
        if (!expected.Contains(opaque))
        {
            throw new InvalidOperationException(
                "apparel-physical-publication-canonical-history-missing");
        }
        if (!canonical.Add(opaque))
        {
            throw new InvalidOperationException(
                "apparel-physical-publication-canonical-duplicate");
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
