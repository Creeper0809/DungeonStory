using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using DungeonStory.Narrative.Korean;

public sealed class FactionCargoFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonFactionSaveData>(save, FactionSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(DungeonFactionSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            if (source?.routes == null || source.version != DungeonFactionSaveData.CurrentVersion
                || incoming?.exactOutcomes == null || incoming.outbox == null || incoming.tombstones == null || counters?.owners == null)
                throw new InvalidOperationException("faction-cargo-save-sections-missing");
            string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt).ProducerId;
            string parentProducer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.FactionRouteArrivedEvent).ProducerId;
            string settlementProducer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.FactionRouteSettlementReceipt).ProducerId;
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.exactOutcomes.Concat(incoming.outbox))
                if (row?.producerId == producer || row?.producerId == parentProducer || row?.producerId == settlementProducer) Add(RetiredCopy(row));
            foreach (var row in incoming.tombstones)
                if (row?.producerId == producer || row?.producerId == parentProducer || row?.producerId == settlementProducer) Add(row);
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                if (row == null || !rows.TryAdd(new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString(), row))
                    throw new InvalidOperationException("faction-cargo-canonical-identity-invalid");
            }
            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var runtime = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new MigratedDomainFactObserver<FactionCargoDeliveredFact>(new FactionCargoFactMapping(ledger).Capture,
                runtime, new GameplayDomainFactCommitGroup(recorder, ledger));
            var expected = new HashSet<string>(StringComparer.Ordinal);
            var expectedOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var route in source.routes)
            {
                if (route?.cargoDelivery == null || route.cargoPublicationKey == null || route.cargoPublicationId == null
                    || route.cargoPublicationFingerprint == null || route.cargoFactionDisplayName == null)
                    throw new InvalidOperationException("faction-cargo-owner-missing");
                if (route.cargoDelivery.state != FactionRouteCargoDeliveryState.Delivered)
                {
                    if (route.cargoDelivery.state == FactionRouteCargoDeliveryState.Publishing || route.cargoPublicationKey.Length > 0
                        || route.cargoPublicationId.Length > 0 || route.cargoPublicationFingerprint.Length > 0
                        || route.cargoObservationDay != 0 || route.cargoFactionDisplayName.Length > 0)
                        throw new InvalidOperationException("faction-cargo-unfinished-publication");
                    continue;
                }
                var fact = new FactionCargoDeliveredFact(route, route.cargoFactionDisplayName, route.cargoObservationDay, route.cargoDelivery);
                string key = MigratedProducerOutcomeCatalog.ResultKey(MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt,
                    route.cargoDelivery.batchCommitId, 1).ToString();
                string owner = producer + "|" + route.cargoDelivery.batchCommitId;
                if (route.cargoObservationDay > source.currentDay || route.cargoObservationDay < route.createdDay
                    || key != route.cargoPublicationKey || !expected.Add(key) || !expectedOwners.Add(owner)
                    || !rows.TryGetValue(key, out var canonical) || canonical.ownerRevision != 1
                    || route.cargoPublicationId != canonical.runId + "/" + canonical.sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    || route.cargoPublicationFingerprint != canonical.canonicalHash)
                    throw new InvalidOperationException("faction-cargo-publication-proof-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
                if (!string.IsNullOrEmpty(fact.ArrivalPublicationKey) && !rows.ContainsKey(fact.ArrivalPublicationKey))
                    throw new InvalidOperationException("faction-cargo-arrival-parent-missing");
                // The arrival itself refers to its paid settlement. Preserve the
                // complete causal chain while validating this small owner subset.
                var lineage = new HashSet<GameplayOutcomeTombstoneSnapshot>();
                var ancestor = canonical;
                while (ancestor != null)
                {
                    if (!lineage.Add(ancestor)) throw new InvalidOperationException("faction-cargo-parent-cycle");
                    subset.tombstones.Add(ancestor);
                    if (ancestor.parentSequence <= 0) break;
                    string parentRun = ancestor.parentRunId;
                    long parentSequence = ancestor.parentSequence;
                    ancestor = rows.Values.SingleOrDefault(value => value.runId == parentRun && value.sequence == parentSequence)
                        ?? throw new InvalidOperationException("faction-cargo-causal-ancestor-missing");
                }
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                runtime.Restore(runtime.PrepareRestore(counters));
                bool accepted = observer.TryPrepareReplay(key, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("faction-cargo-canonical-payload-conflict:" + failure);
            }
            if (rows.Any(pair => pair.Value.producerId == producer && !expected.Contains(pair.Key)))
                throw new InvalidOperationException("faction-cargo-domain-owner-missing");
            var actualOwners = counters.owners.Where(value => value?.ownerKey?.StartsWith(producer + "|", StringComparison.Ordinal) == true).ToArray();
            if (actualOwners.Length != expectedOwners.Count || actualOwners.Any(value => value.revision != 1 || !expectedOwners.Contains(value.ownerKey)))
                throw new InvalidOperationException("faction-cargo-observer-owner-conflict");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("faction-cargo-save-join:" + exception.Message); }
    }

    internal static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row)
    {
        if (row == null) throw new InvalidOperationException("faction-cargo-canonical-null");
        return new GameplayOutcomeTombstoneSnapshot
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
}
