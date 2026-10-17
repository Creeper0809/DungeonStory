using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

/// <summary>Only the observer joins the owner's opaque publication to native storage.</summary>
public sealed class ApparelWorkCompletedFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(OwnerSnapshots(DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterEnvironmentSaveData>(save, CharacterEnvironmentSaveSection.Id)),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("apparel-completed-save-join:" + exception.Message); }
    }

    public static void Validate(IEnumerable<ApparelWorkOrderSaveData> orders, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (orders == null || incoming?.outbox == null || incoming.exactOutcomes == null
            || incoming.tombstones == null || counters == null)
            throw new InvalidOperationException("apparel-completed-save-state-missing");
        var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null)
            .ToLookup(row => Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex));
        var retired = incoming.tombstones.Where(row => row != null)
            .ToLookup(row => Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex));
        var owners = new HashSet<string>(StringComparer.Ordinal);
        foreach (ApparelWorkOrderSaveData owner in orders)
        {
            ApparelWorkCompletedData.ValidateOwner(owner);
            if (!owners.Add(owner.orderId))
                throw new InvalidOperationException("apparel-completed-owner-duplicate");
            string expected = MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.ApparelWorkOrderTerminalResult,
                "apparel-work-order:order=" + owner.orderId, 1L).ToString();
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
            if (owner.terminalPublicationConfirmed && found.Count != 1)
                throw new InvalidOperationException("apparel-completed-confirmation-missing");
            if (found.Count > 1)
                throw new InvalidOperationException("apparel-completed-publication-duplicate");
            if (owner.terminalPublicationKey == string.Empty)
            {
                if (found.Count != 0)
                    throw new InvalidOperationException("apparel-completed-owner-lost-publication-key");
                continue;
            }
            if (!owner.terminalOutcomeReady || owner.terminalPublicationKey != expected)
                throw new InvalidOperationException("apparel-completed-publication-key-conflict");
            ValidatePublication(owner, incoming, counters, found);
        }
    }

    private static void ValidatePublication(ApparelWorkOrderSaveData owner, GameplayOutcomeLedgerSaveData incoming,
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
        var observer = new MigratedDomainFactObserver<ApparelWorkCompletedFact>(ApparelWorkCompletedFactMapping.Capture,
            migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        bool accepted = observer.TryPrepareReplay(owner.terminalPublicationKey, new ApparelWorkCompletedFact(ApparelWorkCompletedData.FromOwner(owner)),
            out IDomainFactPreparedPublication publication, out string failure);
        publication?.Cancel();
        if (!accepted) throw new InvalidOperationException("apparel-completed-payload-conflict:" + failure);
    }

    private static IEnumerable<ApparelWorkOrderSaveData> OwnerSnapshots(DungeonCharacterEnvironmentSaveData source)
    {
        if (source?.apparelWorkOrders == null || source.apparelWorkOrderTerminalStates == null)
            throw new InvalidOperationException("apparel-completed-owner-collections-missing");
        var owners = new Dictionary<string, ApparelWorkOrderSaveData>(StringComparer.Ordinal);
        foreach (ApparelWorkOrderSaveData order in source.apparelWorkOrders)
        {
            if (order == null || string.IsNullOrEmpty(order.orderId) || !owners.TryAdd(order.orderId, order))
                throw new InvalidOperationException("apparel-completed-active-owner-duplicate");
        }
        var terminalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var terminal in source.apparelWorkOrderTerminalStates)
        {
            var order = terminal?.sourceOrder;
            if (order == null || string.IsNullOrEmpty(order.orderId) || !terminalIds.Add(order.orderId))
                throw new InvalidOperationException("apparel-completed-terminal-owner-duplicate");
            if (owners.TryGetValue(order.orderId, out var existing))
            {
                if (UnityEngine.JsonUtility.ToJson(existing) != UnityEngine.JsonUtility.ToJson(order))
                    throw new InvalidOperationException("apparel-completed-terminal-owner-conflict");
            }
            else owners.Add(order.orderId, order);
        }
        return owners.Values;
    }

    private static string Key(string producer, string operation, long revision, int index) =>
        new GameplayResultKey(producer, new GameplayOperationId(operation), revision, index).ToString();
}
