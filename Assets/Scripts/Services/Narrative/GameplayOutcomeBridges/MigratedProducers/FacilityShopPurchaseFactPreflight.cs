using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

public sealed class FacilityShopPurchaseFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(row => row?.sectionId == FacilityShopPurchaseFactSaveSection.Id);
            if (section == null || section.sectionVersion != FacilityShopPurchaseJournalData.CurrentVersion)
                throw new InvalidOperationException("facility-shop-observations-section-missing");
            FacilityShopPurchaseFactSaveSection.ValidateJson(section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, FacilityShopSaveSection.Id, out DungeonFacilityShopSaveData shop)
                || !DungeonSaveSectionPayload.TryRead(save, BlueprintResearchSaveSection.Id, out DungeonResearchSaveData research)
                || !DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger)
                || !DungeonSaveSectionPayload.TryRead(save, MigratedProducerOutcomeSaveSection.Id, out DungeonMigratedProducerOutcomeSaveData counters))
                throw new InvalidOperationException("facility-shop-observations-owner-section-missing");
            var journal = DungeonSaveJson.Deserialize<FacilityShopPurchaseJournalData>(section.payloadJson);
            ValidateOwners(journal, shop, research);
            ValidateJournal(journal, ledger, counters);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("facility-shop-observations-save-join:" + error.Message); }
    }

    public static void ValidateOwners(FacilityShopPurchaseJournalData source, DungeonFacilityShopSaveData shop,
        DungeonResearchSaveData research)
    {
        var journal = FacilityShopPurchaseJournal.ValidateAndClone(source);
        if (shop?.version != DungeonFacilityShopSaveData.CurrentVersion || shop.purchaseSequence < 0
            || research?.shopFulfillments == null)
            throw new InvalidOperationException("facility-shop-observations-owner-invalid");
        foreach (var row in journal.pending)
        {
            if (row.fact.sequence > shop.purchaseSequence)
                throw new InvalidOperationException("facility-shop-observations-purchase-sequence-conflict");
            var delivery = research.shopFulfillments.SingleOrDefault(value => value?.purchaseResultKey == row.publicationKey);
            if (delivery != null && (delivery.offerTypeId != row.fact.offerTypeId
                || delivery.offerDataId != row.fact.offerDataId || delivery.itemDefinitionId != row.fact.itemDefinitionId
                || delivery.itemDisplayName != row.fact.offerName))
                throw new InvalidOperationException("facility-shop-observations-delivery-conflict");
        }
    }

    // Only still-pending facts live here. Successfully enqueued purchases are owned by the delivery aggregate.
    public static void ValidateJournal(FacilityShopPurchaseJournalData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        var journal = FacilityShopPurchaseJournal.ValidateAndClone(source);
        if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
            throw new InvalidOperationException("facility-shop-observations-native-state-missing");
        if (journal.pending.Count == 0) return;
        var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.FacilityShopPurchaseResult).ProducerId;
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes)) Add(RetiredCopy(row));
        foreach (var row in incoming.tombstones) Add(row);
        var savedCounters = counters.owners.ToDictionary(row => row.ownerKey, row => row.revision, StringComparer.Ordinal);
        var registry = new GameplayOutcomeRegistry(
            MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        var observer = new MigratedDomainFactObserver<FacilityShopPurchaseFact>(
            FacilityShopPurchaseFactMapping.Capture, migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        foreach (var row in journal.pending)
        {
            string expected = MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.FacilityShopPurchaseResult, row.fact.operationId, 1).ToString();
            if (row.publicationKey != expected)
                throw new InvalidOperationException("facility-shop-observations-key-conflict");
            canonical.TryGetValue(expected, out var actual);
            long revision = savedCounters.TryGetValue(producer + "|" + row.fact.operationId, out long value) ? value : 0;
            if (actual == null && row.observationCommitted || actual != null && revision != 1
                || actual == null && revision != 0)
                throw new InvalidOperationException("facility-shop-observations-result-counter-conflict");
            if (actual != null && (actual.absoluteDay != row.fact.absoluteDay
                || actual.ownerRevision != 1 || actual.commitRevision != 1))
                throw new InvalidOperationException("facility-shop-observations-proof-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (actual != null) subset.tombstones.Add(actual);
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            migrated.Restore(migrated.PrepareRestore(counters));
            bool accepted = observer.TryPrepareReplay(row.publicationKey,
                new FacilityShopPurchaseFact(row.fact), out var prepared, out string failure);
            prepared?.Cancel();
            if (!accepted) throw new InvalidOperationException("facility-shop-observations-canonical-fact-conflict:" + failure);
        }
        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null) throw new InvalidOperationException("facility-shop-observations-canonical-null");
            if (row.producerId != producer) return;
            var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex);
            if (row.runId != incoming.runId || row.sequence <= 0 || row.sequence >= incoming.nextSequence
                || !canonical.TryAdd(key.ToString(), row))
                throw new InvalidOperationException("facility-shop-observations-canonical-identity-conflict");
        }
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row)
    {
        if (row == null) throw new InvalidOperationException("facility-shop-observations-canonical-null");
        return new GameplayOutcomeTombstoneSnapshot
        {
            runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
            commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
            temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
            parentRunId = row.parentRunId, parentSequence = row.parentSequence, deliveryMetadataVersion = row.deliveryMetadataVersion,
            deliveryClockId = row.deliveryClockId, recordedAbsoluteHour = row.recordedAbsoluteHour,
            publishedRevision = row.publishedRevision, deliveryFaultCount = row.deliveryFaultCount,
            deliveryDelayReason = row.deliveryDelayReason, canonicalHash = row.immutablePayloadHash,
            terminalTier = NarrativeMemoryTier.Forgotten
        };
    }
}
