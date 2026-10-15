using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// Only the observer-side save boundary interprets native publication keys.
public sealed class StockSupplyFactPreflight : IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator, IDungeonSaveRegistryPreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) =>
        Validate(save.sections.ToDictionary(row => row.sectionId, StringComparer.Ordinal), report);

    public void Validate(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections, DungeonGameRestoreReport report)
    {
        try
        {
            var owner = Read<TreasuryEconomySaveData>(sections, TreasuryEconomySaveSection.Id, TreasuryEconomySaveData.CurrentVersion);
            TreasuryEconomySaveSection.ValidateObservationJson(sections[TreasuryEconomySaveSection.Id].payloadJson);
            if (owner.version != TreasuryEconomySaveData.CurrentVersion)
                throw new InvalidOperationException("stock-supply-owner-version-conflict");
            ValidatePending(owner.warehouseSupply,
                Read<GameplayOutcomeLedgerSaveData>(sections, GameplayOutcomeLedgerSaveSection.Id, GameplayOutcomeLedgerSaveData.CurrentVersion),
                Read<DungeonMigratedProducerOutcomeSaveData>(sections, MigratedProducerOutcomeSaveSection.Id, DungeonMigratedProducerOutcomeSaveData.CurrentVersion));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("stock-supply-fact-save-join:" + error.Message); }
    }

    public static void ValidatePending(StockSupplyFactSaveData owner, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        owner = StockSupplyFactSaveData.ValidateAndClone(owner);
        if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
            throw new InvalidOperationException("stock-supply-native-state-missing");
        string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.StockSupplyResult).ProducerId;
        string prefix = producer + "|warehouse-delivery:";
        // The cursor is still checked after pending delivery has been cleared. Retained native
        // owner counters prevent a restored cursor from reusing an earlier purchase identity.
        foreach (var counter in counters.owners)
        {
            if (counter?.ownerKey == null) throw new InvalidOperationException("stock-supply-native-owner-missing");
            if (!counter.ownerKey.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string suffix = counter.ownerKey.Substring(prefix.Length);
            if (!long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out long sequence)
                || sequence < 1 || sequence >= owner.nextSequence || counter.revision != 1
                || suffix != sequence.ToString(CultureInfo.InvariantCulture))
                throw new InvalidOperationException("stock-supply-sequence-rewind-or-owner-conflict");
        }
        if (owner.pending.Count == 0) return;
        var row = owner.pending.Single();
        var fact = new StockSupplyCompletedFact(row.fact);
        var capture = StockSupplyCompletedFactMapping.Capture(fact);
        string expectedKey = MigratedProducerOutcomeCatalog.ResultKey(capture.Kind, capture.OwnerIdentity, 1).ToString();
        var canonical = incoming.outbox.Concat(incoming.exactOutcomes).Select(RetiredCopy).Concat(incoming.tombstones)
            .ToDictionary(value => new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId),
                value.commitRevision, value.localResultIndex).ToString(), StringComparer.Ordinal);
        var revisions = counters.owners.ToDictionary(value => value.ownerKey, value => value.revision, StringComparer.Ordinal);
        canonical.TryGetValue(expectedKey, out var actual);
        long revision = revisions.TryGetValue(producer + "|" + capture.OwnerIdentity, out long value) ? value : 0;
        if (row.publicationKey != expectedKey || (actual != null ? revision != 1 : revision is not (0 or 1))
            || actual != null && (actual.absoluteDay != capture.AbsoluteDay || actual.ownerRevision != 1 || actual.commitRevision != 1))
            throw new InvalidOperationException("stock-supply-native-identity-conflict");

        var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(definition =>
            (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(definition, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var subset = new GameplayOutcomeLedgerSaveData
        { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
        if (actual != null) subset.tombstones.Add(actual);
        subset.knownResultKeyHighWater = subset.tombstones.Count;
        var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
        ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
        ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        migrated.Restore(migrated.PrepareRestore(counters));
        var observer = new MigratedDomainFactObserver<StockSupplyCompletedFact>(StockSupplyCompletedFactMapping.Capture,
            migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        bool accepted = observer.TryPrepareReplay(expectedKey, fact, out var prepared, out string failure);
        prepared?.Cancel();
        if (!accepted) throw new InvalidOperationException("stock-supply-canonical-conflict:" + failure);
    }

    private static T Read<T>(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections, string id, int version) where T : class
    {
        if (!sections.TryGetValue(id, out var row) || row?.sectionVersion != version)
            throw new InvalidOperationException("stock-supply-section-missing:" + id);
        return DungeonSaveJson.Deserialize<T>(row.payloadJson);
    }
    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row) => new()
    {
        runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
        commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
        temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
        parentRunId = row.parentRunId, parentSequence = row.parentSequence, deliveryMetadataVersion = row.deliveryMetadataVersion,
        deliveryClockId = row.deliveryClockId, recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
        deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
        canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
    };
}
