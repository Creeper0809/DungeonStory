using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

/// <summary>Only the observer joins the owner's opaque publication to native storage.</summary>
public sealed class ConstructionWorkEndedFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(DungeonSaveSectionPayload.ReadOrNew<DungeonWorkOrderSaveData>(save, WorkOrdersSaveSection.Id).orders,
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("construction-ended-save-join:" + exception.Message); }
    }

    public static void Validate(IEnumerable<WorkOrderSaveData> orders, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (orders == null || incoming?.outbox == null || incoming.exactOutcomes == null
            || incoming.tombstones == null || counters == null)
            throw new InvalidOperationException("construction-ended-save-state-missing");
        var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null)
            .ToLookup(row => Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex));
        var retired = incoming.tombstones.Where(row => row != null)
            .ToLookup(row => Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex));
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkOrderSaveData owner in orders)
        {
            ConstructionWorkEndedData.ValidateOwner(owner);
            if (!owners.Add(owner.workOrderId))
                throw new InvalidOperationException("construction-ended-owner-duplicate");
            if (owner.workTypeId != BuiltInWorkTypeIds.Construct.Value) continue;
            string expected = MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.WorkOrderTerminalResult,
                "work-order-terminal:" + owner.workOrderId, 1L).ToString();
            var found = retired[expected].ToList();
            foreach (GameplayOutcomeSnapshot row in rows[expected])
                found.Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision,
                    localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
                    absoluteDay = row.absoluteDay, parentRunId = row.parentRunId, parentSequence = row.parentSequence,
                    deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
                    recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
                    deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
            if (found.Count > 1)
                throw new InvalidOperationException("construction-ended-publication-duplicate");
            if (owner.terminalPublicationKey == string.Empty)
            {
                if (found.Count != 0)
                    throw new InvalidOperationException("construction-ended-owner-lost-publication-key");
                continue;
            }
            if (!owner.terminalFactCaptured || owner.terminalPublicationKey != expected)
                throw new InvalidOperationException("construction-ended-publication-key-conflict");
            ValidatePublication(owner, incoming, counters, found);
        }
    }

    private static void ValidatePublication(WorkOrderSaveData owner, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, List<GameplayOutcomeTombstoneSnapshot> found)
    {
        var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 2, retainedLargePageCount: 2));
        var subset = new GameplayOutcomeLedgerSaveData
        {
            runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
            nextSequence = incoming.nextSequence, knownResultKeyHighWater = found.Count, tombstones = found
        };
        var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
        ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
        ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        migrated.Restore(migrated.PrepareRestore(counters));
        var observer = new MigratedDomainFactObserver<ConstructionWorkEndedFact>(ConstructionWorkEndedFactMapping.Capture,
            migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        bool accepted = observer.TryPrepareReplay(owner.terminalPublicationKey, new ConstructionWorkEndedFact(owner.terminalFact),
            out IDomainFactPreparedPublication publication, out string failure);
        publication?.Cancel();
        if (!accepted) throw new InvalidOperationException("construction-ended-payload-conflict:" + failure);
    }

    private static string Key(string producer, string operation, long revision, int index) =>
        new GameplayResultKey(producer, new GameplayOperationId(operation), revision, index).ToString();
}
