using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public sealed class FactionObservationPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
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
            if (source?.routes == null || source.factions == null || source.version != DungeonFactionSaveData.CurrentVersion
                || incoming?.exactOutcomes == null || incoming.outbox == null || incoming.tombstones == null || counters?.owners == null)
                throw new InvalidOperationException("faction-observation-sections-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            var identities = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.exactOutcomes.Concat(incoming.outbox)) Add(FactionCargoFactPreflight.RetiredCopy(row));
            foreach (var row in incoming.tombstones) Add(row);
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                if (row == null || !rows.TryAdd(new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString(), row)
                    || !identities.TryAdd(Identity(row.runId, row.sequence), row))
                    throw new InvalidOperationException("faction-observation-canonical-duplicate");
            }
            var owners = counters.owners.ToDictionary(value => value.ownerKey, StringComparer.Ordinal);
            var expectedRouteKeys = new HashSet<string>(StringComparer.Ordinal);
            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: Math.Max(8, rows.Count + 1), retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var runtime = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
            var mapping = new FactionDomainFactMapping(ledger);
            FactionRelationshipObservationValidation.Validate(
                source.relationshipObservationSequence,
                source.relationshipObservations,
                source.currentDay,
                source.factions.Where(value => value != null)
                    .Select(value => value.factionId)
                    .ToArray());
            if (source.relationshipObservations.Count == 1)
            {
                FactionRelationshipObservationData observation = source.relationshipObservations[0];
                switch (observation.kind)
                {
                    case FactionRelationshipObservationKind.Trust:
                    {
                        FactionTrustChangedDomainFact fact = FactionRelationshipObservationFacts.ToTrust(observation);
                        ObserveRelationship(observation, fact, mapping.Capture);
                        break;
                    }
                    case FactionRelationshipObservationKind.Betrayal:
                    {
                        FactionBetrayalDomainFact fact = FactionRelationshipObservationFacts.ToBetrayal(observation);
                        ObserveRelationship(observation, fact, mapping.Capture);
                        break;
                    }
                    default:
                        throw new InvalidOperationException("faction-relationship-observation-kind-invalid");
                }
            }
            foreach (var route in source.routes)
            {
                if (route == null) throw new InvalidOperationException("faction-route-owner-null");
                if (!string.IsNullOrEmpty(route.settlementPublicationKey))
                {
                    var fact = new FactionRouteSettlementDomainFact(route.settlementObservation);
                    RequireRoute(fact.Route, route);
                    if (JsonUtility.ToJson(fact.CaptureData().settlement) != JsonUtility.ToJson(route.settlement))
                        throw new InvalidOperationException("faction-route-settlement-domain-conflict");
                    Observe(route.settlementPublicationKey, route.settlementPublicationId, route.settlementPublicationFingerprint,
                        fact.AbsoluteDay, route.createdDay, MigratedProducerOutcomeKind.FactionRouteSettlementReceipt,
                        fact.SettlementIdentity, fact, mapping.Capture, string.Empty);
                    expectedRouteKeys.Add(route.settlementPublicationKey);
                }
                else if (route.settlementPublicationId != string.Empty || route.settlementPublicationFingerprint != string.Empty
                    || route.settlementObservation?.IsEmpty != true || route.settlement?.state is FactionRouteSettlementState.Paid
                        or FactionRouteSettlementState.AllianceBenefitDebited or FactionRouteSettlementState.NoDebitRequired)
                    throw new InvalidOperationException("faction-route-settlement-observation-missing");
                if (!string.IsNullOrEmpty(route.arrivalPublicationKey))
                {
                    var fact = new FactionRouteArrivedDomainFact(route.arrivalObservation);
                    RequireRoute(fact.After, route);
                    if (fact.Before.RouteId != route.routeId || fact.Before.FactionId != route.factionId
                        || fact.After.Status != FactionRouteStatus.Arrived || route.status != FactionRouteStatus.Arrived
                        || fact.After.Strength != route.strength || fact.After.SettlementPublicationKey != route.settlementPublicationKey)
                        throw new InvalidOperationException("faction-route-arrival-domain-conflict");
                    Observe(route.arrivalPublicationKey, route.arrivalPublicationId, route.arrivalPublicationFingerprint,
                        fact.AbsoluteDay, route.createdDay, MigratedProducerOutcomeKind.FactionRouteArrivedEvent,
                        fact.After.RouteId, fact, mapping.Capture, fact.After.SettlementPublicationKey);
                    expectedRouteKeys.Add(route.arrivalPublicationKey);
                }
                else if (route.arrivalPublicationId != string.Empty || route.arrivalPublicationFingerprint != string.Empty
                    || route.arrivalObservation?.IsEmpty != true || route.status == FactionRouteStatus.Arrived)
                    throw new InvalidOperationException("faction-route-arrival-observation-missing");
            }
            foreach (var faction in source.factions)
            {
                if (!FactionGoodwillOutbox.HasProvenance(faction)) continue;
                if (faction.goodwillObservationDay < 1 || faction.goodwillObservationDay > source.currentDay
                    || string.IsNullOrWhiteSpace(faction.goodwillFactionDisplayName)
                    || faction.goodwillPublicationKey == null || faction.goodwillPublicationId == null || faction.goodwillPublicationFingerprint == null)
                    throw new InvalidOperationException("faction-goodwill-observation-shape-invalid");
                var prepared = JsonUtility.FromJson<DungeonFactionState>(JsonUtility.ToJson(faction));
                // Before relationship application only a physical transfer may exist.
                // Construct a proposed observation for validation, never publish it here.
                prepared.goodwillTransferCompleted = true;
                var fact = new FactionGoodwillTransferredFact(prepared);
                if ((!faction.goodwillTransferCompleted && faction.goodwillPublicationKey.Length > 0)
                    || faction.goodwillObserved != (faction.goodwillPublicationId.Length > 0))
                    throw new InvalidOperationException("faction-goodwill-observation-phase-conflict");
                Observe(faction.goodwillPublicationKey, faction.goodwillPublicationId, faction.goodwillPublicationFingerprint,
                    fact.AbsoluteDay, 1, MigratedProducerOutcomeKind.FactionTrustChangedEvent, fact.OperationId, fact,
                    new FactionGoodwillFactMapping(ledger).Capture, FactionGoodwillFactMapping.ParentKey(fact).ToString());
            }
            string settlementProducer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.FactionRouteSettlementReceipt).ProducerId;
            string arrivalProducer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.FactionRouteArrivedEvent).ProducerId;
            if (rows.Any(value => (value.Value.producerId == settlementProducer || value.Value.producerId == arrivalProducer)
                && !expectedRouteKeys.Contains(value.Key))) throw new InvalidOperationException("faction-route-canonical-owner-missing");

            void ObserveRelationship<T>(FactionRelationshipObservationData observation, T fact,
                Func<T, MigratedDomainFactCapture> capture)
            {
                MigratedDomainFactCapture captured = capture(fact);
                if (captured.AbsoluteDay != observation.absoluteDay
                    || captured.AbsoluteDay < 1 || captured.AbsoluteDay > source.currentDay
                    || !GameplayDomainFactDeliveryQuery.TryReadKey(observation.publicationKey, out GameplayResultKey key)
                    || key.CommitRevision < 1 || key.LocalResultIndex != 0
                    || key != MigratedProducerOutcomeCatalog.ResultKey(
                        captured.Kind, captured.OwnerIdentity, key.CommitRevision))
                    throw new InvalidOperationException("faction-relationship-observation-identity-conflict");

                string expectedKey = key.ToString();
                rows.TryGetValue(expectedKey, out GameplayOutcomeTombstoneSnapshot canonical);
                string ownerKey = MigratedProducerOutcomeCatalog.Get(captured.Kind).ProducerId
                    + "|" + captured.OwnerIdentity;
                long revision = owners.TryGetValue(ownerKey, out MigratedProducerOwnerRevisionSaveData counter)
                    ? counter.revision
                    : 0L;
                if (canonical != null
                        ? revision != key.CommitRevision
                            || canonical.absoluteDay != captured.AbsoluteDay
                            || canonical.ownerRevision != key.CommitRevision
                            || canonical.commitRevision != key.CommitRevision
                        : revision != key.CommitRevision - 1L && revision != key.CommitRevision)
                    throw new InvalidOperationException("faction-relationship-observation-native-state-conflict");

                var subset = new GameplayOutcomeLedgerSaveData
                {
                    runId = incoming.runId,
                    worldEpoch = incoming.worldEpoch,
                    revision = incoming.revision,
                    nextSequence = incoming.nextSequence
                };
                if (canonical != null)
                    subset.tombstones.Add(canonical);
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate();
                ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate();
                ledger.CompleteRestoreCandidate();
                runtime.Restore(runtime.PrepareRestore(counters));
                var observer = new MigratedDomainFactObserver<T>(capture, runtime, commits);
                bool accepted = observer.TryPrepareReplay(
                    expectedKey, fact, out var publication, out string failure);
                publication?.Cancel();
                if (!accepted)
                    throw new InvalidOperationException(
                        "faction-relationship-observation-canonical-payload-conflict:" + failure);
            }

            void Observe<T>(string key, string publicationId, string fingerprint, int day, int minimumDay,
                MigratedProducerOutcomeKind kind, string ownerId, T fact, Func<T, MigratedDomainFactCapture> capture, string parentKey)
            {
                string expectedKey = MigratedProducerOutcomeCatalog.ResultKey(kind, ownerId, 1).ToString();
                bool reserved = !string.IsNullOrEmpty(key);
                if (key == null || publicationId == null || fingerprint == null || (reserved && key != expectedKey)
                    || day < minimumDay || day > source.currentDay)
                    throw new InvalidOperationException("faction-observation-identity-conflict");
                rows.TryGetValue(expectedKey, out var canonical);
                if ((!reserved && (canonical != null || publicationId.Length > 0 || fingerprint.Length > 0))
                    || (publicationId.Length == 0 ? fingerprint.Length > 0 : canonical == null
                        || publicationId != Identity(canonical.runId, canonical.sequence) || fingerprint != canonical.canonicalHash))
                    throw new InvalidOperationException("faction-observation-proof-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
                var added = new HashSet<string>(StringComparer.Ordinal);
                void Branch(GameplayOutcomeTombstoneSnapshot row)
                {
                    var chain = new HashSet<string>(StringComparer.Ordinal);
                    while (row != null)
                    {
                        string id = Identity(row.runId, row.sequence);
                        if (!chain.Add(id)) throw new InvalidOperationException("faction-observation-parent-cycle");
                        if (!added.Add(id)) break;
                        subset.tombstones.Add(row);
                        if (row.parentSequence <= 0) break;
                        if (!identities.TryGetValue(Identity(row.parentRunId, row.parentSequence), out row))
                            throw new InvalidOperationException("faction-observation-parent-missing");
                    }
                }
                if (canonical != null) Branch(canonical);
                if (!string.IsNullOrEmpty(parentKey))
                {
                    if (!rows.TryGetValue(parentKey, out var parent)) throw new InvalidOperationException("faction-observation-parent-missing");
                    Branch(parent);
                }
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate); ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                string ownerKey = MigratedProducerOutcomeCatalog.Get(kind).ProducerId + "|" + ownerId;
                var count = new DungeonMigratedProducerOutcomeSaveData();
                if (owners.TryGetValue(ownerKey, out var counter)) count.owners.Add(counter.Clone());
                if ((canonical != null && counter?.revision != 1) || (counter != null && counter.revision != 1)
                    || (!reserved && counter != null)) throw new InvalidOperationException("faction-observation-owner-revision-conflict");
                runtime.Restore(runtime.PrepareRestore(count));
                var observer = new MigratedDomainFactObserver<T>(capture, runtime, commits);
                bool accepted = observer.TryPrepareReplay(expectedKey, fact, out var publication, out string failure);
                publication?.Cancel();
                if (!accepted) throw new InvalidOperationException("faction-observation-canonical-payload-conflict:" + failure);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("faction-observation-save-join:" + exception.Message); }
    }

    private static string Identity(string run, long sequence) => run + "/" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void RequireRoute(FactionRouteFactSnapshot fact, FactionRouteState route)
    {
        if (route.path == null || route.path.Count == 0 || route.cargo == null
            || fact.RouteId != route.routeId || fact.FactionId != route.factionId || fact.Kind != route.kind
            || fact.SourceQ != route.path[0].q || fact.SourceR != route.path[0].r
            || fact.DestinationQ != route.path[route.path.Count - 1].q || fact.DestinationR != route.path[route.path.Count - 1].r
            || !fact.Cargo.Select(value => value.ItemId).SequenceEqual(route.cargo.Select(value => value.itemId), StringComparer.Ordinal))
            throw new InvalidOperationException("faction-route-frozen-domain-conflict");
    }
}
