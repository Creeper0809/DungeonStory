using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// The purchase owner retains only opaque observation proof. Native identity and
// canonical payload validation are owned by this save-side observer adapter.
public sealed class RetailPurchaseFactPreflight : IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator, IDungeonSaveRegistryPreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) =>
        Validate(save.sections.ToDictionary(row => row.sectionId, StringComparer.Ordinal), report);

    public void Validate(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections,
        DungeonGameRestoreReport report)
    {
        try
        {
            if (!sections.TryGetValue(CharacterWorldSaveSection.Id, out var section)
                || section.sectionVersion != CharacterWorldSaveSection.CurrentVersion)
                throw new InvalidOperationException("retail-observation-character-section-missing");
            CharacterWorldSaveSection.ValidateRetailPurchaseJson(section.payloadJson);
            var characters = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(section.payloadJson);
            ValidatePending(characters, Read<GameplayOutcomeLedgerSaveData>(sections, GameplayOutcomeLedgerSaveSection.Id, GameplayOutcomeLedgerSaveData.CurrentVersion),
                Read<DungeonMigratedProducerOutcomeSaveData>(sections, MigratedProducerOutcomeSaveSection.Id, DungeonMigratedProducerOutcomeSaveData.CurrentVersion));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("retail-observation-save-join:" + error.Message); }
    }

    public static void ValidatePending(DungeonCharacterWorldSaveData characters,
        GameplayOutcomeLedgerSaveData incoming, DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (characters?.actors == null || incoming?.outbox == null || incoming.exactOutcomes == null
            || incoming.tombstones == null || counters?.owners == null)
            throw new InvalidOperationException("retail-observation-state-missing");
        var pending = new List<RetailPurchaseRecoverySaveData>();
        var operations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var actor in characters.actors)
        {
            if (actor?.pendingRetailPurchases == null)
                throw new InvalidOperationException("retail-observation-owner-missing");
            foreach (var row in actor.pendingRetailPurchases)
            {
                if (row == null || row.buyerId != actor.persistentId || !operations.Add(row.operationId))
                    throw new InvalidOperationException("retail-observation-owner-conflict");
                row.Validate(); pending.Add(row);
            }
        }
        if (pending.Count == 0) return;
        const MigratedProducerOutcomeKind kind = MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult;
        string producer = MigratedProducerOutcomeCatalog.Get(kind).ProducerId;
        var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes)) Add(RetiredCopy(row));
        foreach (var row in incoming.tombstones) Add(row);
        var savedCounters = counters.owners.ToDictionary(row => row.ownerKey, row => row.revision, StringComparer.Ordinal);
        var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        var observer = new MigratedDomainFactObserver<RetailPurchaseFact>(RetailPurchaseFactMapping.Capture,
            migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        foreach (var row in pending)
        {
            var fact = new RetailPurchaseFact(row);
            string owner = RetailPurchaseFactMapping.Capture(fact).OwnerIdentity;
            string key = MigratedProducerOutcomeCatalog.ResultKey(kind, owner, 1).ToString();
            canonical.TryGetValue(key, out var actual);
            long revision = savedCounters.TryGetValue(producer + "|" + owner, out long value) ? value : 0L;
            if (row.publicationKey.Length == 0)
            {
                if (actual != null || revision != 0L)
                    throw new InvalidOperationException("retail-observation-unreserved-canonical-conflict");
                continue;
            }
            if (row.publicationKey != key || (actual != null ? revision != 1L : revision != 0L && revision != 1L))
                throw new InvalidOperationException("retail-observation-key-counter-conflict");
            bool acknowledged = row.phase >= RetailPurchaseRecoveryPhase.OutcomeCommitted;
            if (actual == null && acknowledged)
                throw new InvalidOperationException("retail-observation-proof-without-canonical");
            if (actual != null && (actual.absoluteDay != row.absoluteDay || actual.ownerRevision != 1L
                || acknowledged && (row.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(actual.runId), actual.sequence).ToString()
                    || row.publicationFingerprint != actual.canonicalHash)))
                throw new InvalidOperationException("retail-observation-proof-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (actual != null) subset.tombstones.Add(actual);
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            migrated.Restore(migrated.PrepareRestore(counters));
            bool accepted = observer.TryPrepareReplay(key, fact, out var prepared, out string failure);
            prepared?.Cancel();
            if (!accepted) throw new InvalidOperationException("retail-observation-canonical-fact-conflict:" + failure);
        }

        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null) throw new InvalidOperationException("retail-observation-canonical-null");
            if (row.producerId != producer) return;
            string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
            if (row.runId != incoming.runId || row.sequence <= 0L || row.sequence >= incoming.nextSequence || !canonical.TryAdd(key, row))
                throw new InvalidOperationException("retail-observation-canonical-identity-conflict");
        }
    }

    private static T Read<T>(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections, string id, int version) where T : class
    {
        if (!sections.TryGetValue(id, out var section) || section == null || section.sectionVersion != version)
            throw new InvalidOperationException("retail-observation-section-missing:" + id);
        return DungeonSaveJson.Deserialize<T>(section.payloadJson);
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row)
    {
        if (row == null) throw new InvalidOperationException("retail-observation-canonical-null");
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
