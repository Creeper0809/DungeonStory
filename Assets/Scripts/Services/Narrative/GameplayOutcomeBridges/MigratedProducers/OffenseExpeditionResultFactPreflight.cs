using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>Joins applied domain results, immutable canonical publications and observer revision ownership.</summary>
public sealed class OffenseExpeditionResultFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(save, OffenseAggregateSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(DungeonOffenseAggregateSaveData aggregate, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            if (aggregate == null || aggregate.version != DungeonOffenseAggregateSaveData.CurrentVersion
                || aggregate.expedition?.resultHistory == null || aggregate.expedition.activeExpeditions == null)
                throw new InvalidOperationException("offense-result-aggregate-invalid");
            var saved = OffenseExpeditionResultPublicationRuntime.ValidateAndClone(aggregate.resultPublications);
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
                throw new InvalidOperationException("offense-result-canonical-save-missing");
            string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.OffenseExpeditionResult).ProducerId;
            string stockProducer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.StockSupplyResult).ProducerId;
            string stockPrefix = stockProducer + "|offense-stock-reward:";
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var value in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (value == null) throw new InvalidOperationException("offense-result-canonical-null");
                if (value.producerId != producer && value.producerId != stockProducer) continue;
                Add(RetiredCopy(value));
            }
            foreach (var value in incoming.tombstones)
            {
                if (value == null) throw new InvalidOperationException("offense-result-canonical-null");
                if (value.producerId == producer || value.producerId == stockProducer) Add(value);
            }
            void Add(GameplayOutcomeTombstoneSnapshot value)
            {
                var key = new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex);
                if (value.commitRevision != 1 || value.ownerRevision != 1 || value.localResultIndex != 0
                    || value.runId != incoming.runId || value.sequence >= incoming.nextSequence || !rows.TryAdd(key.ToString(), value))
                    throw new InvalidOperationException("offense-result-canonical-identity-conflict");
            }

            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                    (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new OffenseExpeditionResultFactObserver(migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            var stockObserver = new MigratedDomainFactObserver<OffenseStockRewardCompletedFact>(
                OffenseStockRewardFactMapping.Capture, migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            var expectedKeys = new HashSet<string>(StringComparer.Ordinal);
            var expectedOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var observation in saved.observations)
            {
                var fact = new OffenseExpeditionResolvedFact(observation.fact);
                string key = OffenseExpeditionResultFactObserver.Key(fact).ToString();
                if (key != observation.publicationKey || !expectedKeys.Add(key))
                    throw new InvalidOperationException("offense-result-owner-publication-conflict");
                expectedOwners.Add(producer + "|offense-expedition-result:" + fact.Result.expeditionId);
                bool exists = rows.TryGetValue(key, out var canonical);
                if (!exists && !observation.pending) throw new InvalidOperationException("offense-result-delivered-canonical-missing");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
                if (exists) { subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1; }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                bool accepted = observer.TryPrepareReplay(key, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("offense-result-canonical-payload-conflict:" + failure);
                var stocks = OffenseStockRewardCompletedFact.From(fact);
                for (int index = 0; index < stocks.Count; index++)
                {
                    var stock = stocks[index];
                    var capture = OffenseStockRewardFactMapping.Capture(stock);
                    string stockKey = MigratedProducerOutcomeCatalog.ResultKey(capture.Kind, capture.OwnerIdentity, 1).ToString();
                    string owner = stockProducer + "|" + capture.OwnerIdentity;
                    if (observation.stockPublicationKeys[index] != stockKey || !expectedKeys.Add(stockKey))
                        throw new InvalidOperationException("offense-stock-publication-key-conflict");
                    expectedOwners.Add(owner);
                    bool stockExists = rows.TryGetValue(stockKey, out var stockCanonical);
                    if (stockExists != exists)
                        throw new InvalidOperationException("offense-stock-cohort-partially-committed");
                    var counter = counters.owners.SingleOrDefault(value => value?.ownerKey == owner);
                    if (stockExists && (counter?.revision != 1 || stockCanonical.absoluteDay != capture.AbsoluteDay))
                        throw new InvalidOperationException("offense-stock-native-owner-conflict");
                    var stockSubset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
                    if (stockExists) { stockSubset.tombstones.Add(stockCanonical); stockSubset.knownResultKeyHighWater = 1; }
                    var stockCandidate = ledger.PrepareGameplayOutcomeRestore(stockSubset);
                    ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(stockCandidate);
                    ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                    migrated.Restore(migrated.PrepareRestore(counters));
                    bool stockAccepted = stockObserver.TryPrepareReplay(stockKey, stock, out var stockPrepared, out string stockFailure);
                    stockPrepared?.Cancel();
                    if (!stockAccepted) throw new InvalidOperationException("offense-stock-canonical-payload-conflict:" + stockFailure);
                }
            }
            if (rows.Any(pair => pair.Value.producerId == producer && !expectedKeys.Contains(pair.Key))
                || counters.owners.Any(value => value == null || value.ownerKey == null
                    || (value.ownerKey.StartsWith(producer + "|", StringComparison.Ordinal) || value.ownerKey.StartsWith(stockPrefix, StringComparison.Ordinal))
                    && (value.revision != 1 || !expectedOwners.Contains(value.ownerKey))))
                throw new InvalidOperationException("offense-result-canonical-owner-missing");

            var byId = saved.observations.ToDictionary(value => value.fact.result.expeditionId, StringComparer.Ordinal);
            foreach (var active in aggregate.expedition.activeExpeditions)
            {
                if (active == null) throw new InvalidOperationException("offense-result-active-run-null");
                if (byId.TryGetValue(active.expeditionId, out var finalized)
                    && (!active.returnPending || !active.returnResourcesCommitted || active.returnProgress == null
                        || active.returnProgress.Any(value => value == null || value.stage != ExpeditionReturnStage.Arrived
                            && value.stage != ExpeditionReturnStage.RequiresRescue && value.stage != ExpeditionReturnStage.Dead)
                        || active.targetId != finalized.fact.result.targetId || active.returnSuccess != finalized.fact.result.success))
                    throw new InvalidOperationException("offense-result-active-return-conflict");
            }
            var serializer = JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, Culture = System.Globalization.CultureInfo.InvariantCulture });
            foreach (var history in aggregate.expedition.resultHistory)
            {
                if (history == null || !byId.TryGetValue(history.expeditionId, out var observation))
                    throw new InvalidOperationException("offense-result-history-publication-missing");
                var frozen = JObject.FromObject(observation.fact.result, serializer);
                var current = JObject.FromObject(history, serializer);
                // Arrival settlement legitimately advances after the terminal result. Its own authority validates it.
                frozen.Remove("arrivalReceipts"); current.Remove("arrivalReceipts");
                if (!JToken.DeepEquals(frozen, current)) throw new InvalidOperationException("offense-result-history-fact-conflict");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("offense-result-save-join:" + exception.Message); }
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
