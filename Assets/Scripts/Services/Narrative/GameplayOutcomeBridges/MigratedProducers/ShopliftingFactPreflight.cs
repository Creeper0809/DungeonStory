using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

public sealed class ShopliftingFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(row => row?.sectionId == ShopliftingFactSaveSection.Id);
            if (section == null || section.sectionVersion != ShopliftingFactJournalData.CurrentVersion)
                throw new InvalidOperationException("shoplifting-observations-section-missing");
            ShopliftingFactSaveSection.ValidateJson(section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, ModularFacilityWorldSaveSection.Id, out ModularFacilityWorldSaveData buildings)
                || !DungeonSaveSectionPayload.TryRead(save, PhysicalItemsSaveSection.Id, out DungeonPhysicalItemSaveData physical)
                || !DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger)
                || !DungeonSaveSectionPayload.TryRead(save, MigratedProducerOutcomeSaveSection.Id, out DungeonMigratedProducerOutcomeSaveData counters))
                throw new InvalidOperationException("shoplifting-observations-owner-section-missing");
            var journal = DungeonSaveJson.Deserialize<ShopliftingFactJournalData>(section.payloadJson);
            ValidateOwners(journal, buildings, physical);
            ValidateJournal(journal, ledger, counters);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("shoplifting-observations-save-join:" + error.Message); }
    }

    public static void ValidateOwners(ShopliftingFactJournalData source, ModularFacilityWorldSaveData buildings,
        DungeonPhysicalItemSaveData physical)
    {
        var journal = ShopliftingFactJournal.ValidateAndClone(source);
        if (buildings?.buildings == null || physical?.uniqueItems == null)
            throw new InvalidOperationException("shoplifting-observations-owner-invalid");
        if (journal.pending.Count == 0) return;
        var lots = new List<RetailStockLotSnapshot>();
        foreach (var building in buildings.buildings)
        foreach (var module in building?.stateModules ?? new List<BuildingStateModuleSaveData>())
        {
            if (module?.moduleId != BuildingStateModuleIds.ShopStock) continue;
            var stock = UnityEngine.JsonUtility.FromJson<ShopStockStateSnapshot>(module.payload);
            if (module.version != 1 || stock?.schemaVersion != ShopStockStateSnapshot.CurrentSchemaVersion || stock.lots == null)
                throw new InvalidOperationException("shoplifting-observations-shop-stock-invalid");
            lots.AddRange(stock.lots);
        }
        foreach (var row in journal.pending)
        {
            var fact = row.fact;
            if (lots.Where(lot => lot?.sourceOperationId == fact.lot.sourceOperationId).Sum(lot => lot.quantity)
                    >= fact.sourceQuantityBefore
                || fact.lot.itemInstanceId.Length > 0 && physical.uniqueItems.Any(item => item?.itemInstanceId == fact.lot.itemInstanceId))
                throw new InvalidOperationException("shoplifting-observations-effect-not-applied");
        }
    }

    public static void ValidateJournal(ShopliftingFactJournalData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        var journal = ShopliftingFactJournal.ValidateAndClone(source);
        if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
            throw new InvalidOperationException("shoplifting-observations-native-state-missing");
        if (journal.pending.Count == 0) return;
        var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.FacilityCrimeEvent).ProducerId;
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
        var observer = new MigratedDomainFactObserver<ShopliftingFact>(
            ShopliftingFactMapping.Capture, migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        foreach (var row in journal.pending)
        {
            string expected = MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.FacilityCrimeEvent, ShopliftingFactMapping.Capture(new ShopliftingFact(row.fact)).OwnerIdentity, 1).ToString();
            if (row.publicationKey != expected)
                throw new InvalidOperationException("shoplifting-observations-key-conflict");
            canonical.TryGetValue(expected, out var actual);
            long revision = savedCounters.TryGetValue(producer + "|" + ShopliftingFactMapping.Capture(new ShopliftingFact(row.fact)).OwnerIdentity, out long value) ? value : 0;
            if (actual != null && revision != 1
                || actual == null && revision != 0)
                throw new InvalidOperationException("shoplifting-observations-result-counter-conflict");
            if (actual != null && (actual.absoluteDay != row.fact.absoluteDay
                || actual.ownerRevision != 1 || actual.commitRevision != 1))
                throw new InvalidOperationException("shoplifting-observations-proof-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (actual != null) subset.tombstones.Add(actual);
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            migrated.Restore(migrated.PrepareRestore(counters));
            bool accepted = observer.TryPrepareReplay(row.publicationKey,
                new ShopliftingFact(row.fact), out var prepared, out string failure);
            prepared?.Cancel();
            if (!accepted) throw new InvalidOperationException("shoplifting-observations-canonical-fact-conflict:" + failure);
        }
        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null) throw new InvalidOperationException("shoplifting-observations-canonical-null");
            if (row.producerId != producer) return;
            var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex);
            if (row.runId != incoming.runId || row.sequence <= 0 || row.sequence >= incoming.nextSequence
                || !canonical.TryAdd(key.ToString(), row))
                throw new InvalidOperationException("shoplifting-observations-canonical-identity-conflict");
        }
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row)
    {
        if (row == null) throw new InvalidOperationException("shoplifting-observations-canonical-null");
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
