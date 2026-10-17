using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public sealed class FacilitySynthesisFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(
        DungeonGameSaveData save,
        DungeonGameRestoreReport report)
    {
        try
        {
            DungeonSaveSectionEnvelope section = save?.sections?.SingleOrDefault(
                value => value?.sectionId == FacilitySynthesisFactSaveSection.Id);
            if (section == null
                || section.sectionVersion
                    != FacilitySynthesisFactSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "facility-synthesis-observation-section-missing-or-version-unsupported");
            }

            FacilitySynthesisFactSaveSection.ValidateJson(section.payloadJson);
            FacilitySynthesisFactSaveData journal =
                DungeonSaveJson.Deserialize<FacilitySynthesisFactSaveData>(
                    section.payloadJson);
            journal = FacilitySynthesisFactOutbox.ValidateAndClone(journal);
            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    GameplayOutcomeLedgerSaveSection.Id,
                    out GameplayOutcomeLedgerSaveData ledger))
            {
                throw new InvalidOperationException(
                    "facility-synthesis-observation-ledger-section-missing");
            }

            ValidateJournal(journal, ledger);
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException
            or Newtonsoft.Json.JsonException)
        {
            report.AddError(
                "facility-synthesis-observation-save-join:"
                + error.Message);
        }
    }

    public static void ValidateJournal(
        FacilitySynthesisFactSaveData source,
        GameplayOutcomeLedgerSaveData incoming)
    {
        FacilitySynthesisFactSaveData journal =
            FacilitySynthesisFactOutbox.ValidateAndClone(source);
        RequireLedger(incoming);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (FacilitySynthesisPendingFactData pending in journal.pending)
        {
            ValidateObservation(pending, incoming);
            if (!keys.Add(pending.publicationKey))
            {
                throw new InvalidOperationException(
                    "facility-synthesis-observation-pending-duplicate");
            }
        }
    }

    public static void ValidateObservation(
        FacilitySynthesisPendingFactData pending,
        GameplayOutcomeLedgerSaveData incoming)
    {
        RequireLedger(incoming);
        if (pending?.fact == null)
        {
            throw new InvalidOperationException(
                "facility-synthesis-observation-fact-missing");
        }

        var fact = new FacilitySynthesisCompletedFact(pending.fact);
        FacilitySynthesisOutcomeReceipt receipt =
            FacilitySynthesisFactObserver.Capture(fact);
        GameplayResultKey key = receipt.ResultKey;
        if (!string.Equals(
                pending.publicationKey,
                key.ToString(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "facility-synthesis-observation-key-conflict");
        }

        GameplayOutcomeSnapshot[] liveRows = incoming.outbox
            .Concat(incoming.exactOutcomes)
            .Where(row => MatchesOperation(row, key))
            .ToArray();
        GameplayOutcomeTombstoneSnapshot[] retiredRows = incoming.tombstones
            .Where(row => MatchesOperation(row, key))
            .ToArray();
        if (liveRows.Length + retiredRows.Length > 1)
        {
            throw new InvalidOperationException(
                "facility-synthesis-observation-canonical-duplicate");
        }

        GameplayOutcomeSnapshot live = liveRows.SingleOrDefault();
        GameplayOutcomeTombstoneSnapshot canonical = live == null
            ? retiredRows.SingleOrDefault()
            : RetiredCopy(live);
        if (canonical != null
            && (canonical.runId != incoming.runId
                || canonical.sequence < 0
                || canonical.sequence >= incoming.nextSequence
                || canonical.commitRevision != key.CommitRevision
                || canonical.localResultIndex != key.LocalResultIndex
                || canonical.ownerRevision != key.CommitRevision
                || canonical.absoluteDay != receipt.AbsoluteDay))
        {
            throw new InvalidOperationException(
                "facility-synthesis-observation-canonical-proof-conflict");
        }

        ValidateCanonicalPayload(receipt, incoming, canonical);
    }

    private static void ValidateCanonicalPayload(
        FacilitySynthesisOutcomeReceipt receipt,
        GameplayOutcomeLedgerSaveData incoming,
        GameplayOutcomeTombstoneSnapshot canonical)
    {
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new FacilitySynthesisOutcomeDescriptor(new DungeonStory.Narrative.Korean.KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new FacilitySynthesisOutcomeAdapter()
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
        OutcomePrepareResult result = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken prepared);
        recorder.CancelPrepared(prepared);
        if (canonical == null
            ? !result.Success
            : result.Code != OutcomePrepareCode.AlreadyTerminal)
        {
            throw new InvalidOperationException(
                "facility-synthesis-observation-canonical-fact-conflict:"
                + result.DetailCode);
        }
    }

    private static void RequireLedger(GameplayOutcomeLedgerSaveData incoming)
    {
        if (incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "facility-synthesis-observation-ledger-save-missing");
        }
    }

    private static bool MatchesOperation(
        GameplayOutcomeSnapshot row,
        GameplayResultKey key) => row != null
        && row.producerId == key.ProducerId
        && row.operationId == key.OperationId.Value;

    private static bool MatchesOperation(
        GameplayOutcomeTombstoneSnapshot row,
        GameplayResultKey key) => row != null
        && row.producerId == key.ProducerId
        && row.operationId == key.OperationId.Value;

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
