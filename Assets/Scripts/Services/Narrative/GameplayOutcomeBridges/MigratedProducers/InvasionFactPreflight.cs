using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;

public sealed class InvasionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(row => row?.sectionId == InvasionFactPublicationSaveSection.Id);
            if (section == null || section.sectionVersion != InvasionFactPublicationSaveData.CurrentVersion)
                throw new InvalidOperationException("invasion-observations-section-missing");
            InvasionFactPublicationSaveSection.ValidateJson(section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, InvasionSaveSection.Id, out DungeonInvasionSaveData owners)
                || !DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger)
                || !DungeonSaveSectionPayload.TryRead(save, MigratedProducerOutcomeSaveSection.Id, out DungeonMigratedProducerOutcomeSaveData counters))
                throw new InvalidOperationException("invasion-observations-owner-section-missing");
            var raw = JObject.Parse(save.sections.Single(row => row.sectionId == InvasionSaveSection.Id).payloadJson);
            if (raw["activeIntruders"] is not JArray actors || actors.Any(actor =>
                    actor?["invasionStartPublicationKey"]?.Type != JTokenType.String
                    || actor?["lastInvasionStagePublicationKey"]?.Type != JTokenType.String)
                || raw["campaign"]?["operations"] is not JArray operations
                || operations.Any(operation => operation?["startedPublicationKey"]?.Type != JTokenType.String))
                throw new InvalidOperationException("invasion-observations-raw-owner-fields-missing");
            var journal = DungeonSaveJson.Deserialize<InvasionFactPublicationSaveData>(section.payloadJson);
            ValidateOwners(journal, owners);
            ValidateJournal(journal, ledger, counters);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("invasion-observations-save-join:" + error.Message); }
    }

    public static void ValidateOwners(InvasionFactPublicationSaveData source, DungeonInvasionSaveData owners)
    {
        var journal = InvasionFactPublicationRuntime.ValidateAndClone(source);
        if (owners?.version != DungeonInvasionSaveData.CurrentVersion || owners.activeIntruders == null || owners.campaign?.operations == null)
            throw new InvalidOperationException("invasion-observations-domain-owner-missing");
        var histories = journal.observations.GroupBy(row => row.fact.runtimeId).ToDictionary(group => group.Key, group => group.ToArray());
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var owner in owners.activeIntruders)
        {
            if (owner == null || !ids.Add(owner.runtimeId) || !histories.TryGetValue(owner.runtimeId, out var history)
                || owner.enemyIndividual?.characterId != history[0].fact.intruderId
                || owner.invasionStartPublicationKey != history[0].publicationKey
                || owner.lastInvasionStagePublicationKey != history[^1].publicationKey
                || owner.facilityDamageCount != history[^1].fact.damageIndexAfter
                || owner.hasBreachedDungeonInterior != history.Any(row => row.fact.stage == InvasionLifecycleStage.Breached)
                || owner.damagedFacilityBuildingInstanceIds == null
                || !owner.damagedFacilityBuildingInstanceIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(
                    history.Where(row => row.fact.stage == InvasionLifecycleStage.FacilityDamaged)
                        .Select(row => row.fact.targetId).OrderBy(id => id, StringComparer.Ordinal)))
                throw new InvalidOperationException("invasion-observations-active-owner-conflict");
        }
        foreach (var operation in owners.campaign.operations)
        {
            if (operation == null) throw new InvalidOperationException("invasion-observations-campaign-null");
            if (operation.startedInvasionRuntimeId == string.Empty && operation.startedPublicationKey == string.Empty) continue;
            if (!histories.TryGetValue(operation.startedInvasionRuntimeId, out var history)
                || operation.startedPublicationKey != history[0].publicationKey
                || history[0].fact.milestonePressure && (history[0].fact.targetId != operation.operationId
                    || history[0].fact.sourceOwnerId != operation.sourceOwnerId))
                throw new InvalidOperationException("invasion-observations-campaign-start-conflict");
        }
    }

    public static void ValidateJournal(InvasionFactPublicationSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        var journal = InvasionFactPublicationRuntime.ValidateAndClone(source);
        if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
            throw new InvalidOperationException("invasion-observations-ledger-invalid");
        var kinds = new[] { MigratedProducerOutcomeKind.InvasionStartedEvent, MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
            MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent, MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
            MigratedProducerOutcomeKind.InvasionResolvedEvent };
        var producers = kinds.Select(kind => MigratedProducerOutcomeCatalog.Get(kind).ProducerId).ToHashSet(StringComparer.Ordinal);
        var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes)) Add(RetiredCopy(row));
        foreach (var row in incoming.tombstones) Add(row);
        var keys = journal.observations.Select(row => row.publicationKey).ToHashSet(StringComparer.Ordinal);
        var historyByKey = journal.observations.ToDictionary(row => row.publicationKey, StringComparer.Ordinal);
        if (canonical.Keys.Any(key => !keys.Contains(key))) throw new InvalidOperationException("invasion-observations-history-missing");
        var revisions = new Dictionary<string, long>(StringComparer.Ordinal);
        var uncertain = new HashSet<string>(StringComparer.Ordinal);
        var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
            (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(smallPageCount: 1, largePageCount: 1,
            knownResultKeyCapacity: Math.Max(4, journal.observations.Count), retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        var observer = new InvasionLifecycleFactObserver(migrated, new GameplayDomainFactCommitGroup(recorder, ledger), ledger);
        foreach (var row in journal.observations)
        {
            string producer = MigratedProducerOutcomeCatalog.Get(InvasionLifecycleFactObservation.Kind(row.fact)).ProducerId;
            string identity = InvasionLifecycleFactObservation.OwnerIdentity(row.fact);
            string owner = producer + "|" + identity;
            long revision = revisions.TryGetValue(owner, out long previous) ? checked(previous + 1) : 1;
            revisions[owner] = revision; uncertain.Remove(owner);
            if (row.publicationKey != MigratedProducerOutcomeCatalog.ResultKey(InvasionLifecycleFactObservation.Kind(row.fact), identity, revision).ToString())
                throw new InvalidOperationException("invasion-observations-key-conflict");
            canonical.TryGetValue(row.publicationKey, out var actual);
            if (actual == null && !row.pending) throw new InvalidOperationException("invasion-observations-result-missing");
            if (actual == null) uncertain.Add(owner);
            if (actual != null && (actual.absoluteDay != row.fact.absoluteDay || actual.ownerRevision != revision
                    || actual.commitRevision != revision || !row.pending && (row.publicationId != Identity(actual).ToString()
                        || row.fingerprint != actual.canonicalHash)))
                throw new InvalidOperationException("invasion-observations-proof-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (actual != null) subset.tombstones.Add(actual);
            string parentKey = row.fact.parentPublicationKey;
            while (parentKey != string.Empty)
            {
                if (!canonical.TryGetValue(parentKey, out var parent)
                    || !historyByKey.TryGetValue(parentKey, out var parentFact))
                    throw new InvalidOperationException("invasion-observations-parent-missing");
                subset.tombstones.Add(parent);
                parentKey = parentFact.fact.parentPublicationKey;
            }
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            migrated.Restore(migrated.PrepareRestore(counters));
            bool accepted = observer.TryPrepareReplay(row.publicationKey, new InvasionLifecycleFact(row.fact), out var prepared, out string failure);
            prepared?.Cancel();
            if (!accepted) throw new InvalidOperationException("invasion-observations-canonical-fact-conflict:" + failure);
        }
        var savedCounters = counters.owners.ToDictionary(row => row.ownerKey, row => row.revision, StringComparer.Ordinal);
        foreach (var pair in revisions)
        {
            long value = savedCounters.TryGetValue(pair.Key, out long revision) ? revision : 0;
            if (value != pair.Value && !(uncertain.Contains(pair.Key) && value == pair.Value - 1))
                throw new InvalidOperationException("invasion-observations-observer-revision-conflict");
        }
        if (savedCounters.Keys.Any(key => producers.Any(producer => key.StartsWith(producer + "|", StringComparison.Ordinal)) && !revisions.ContainsKey(key)))
            throw new InvalidOperationException("invasion-observations-observer-history-missing");
        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null) throw new InvalidOperationException("invasion-observations-canonical-null");
            if (!producers.Contains(row.producerId)) return;
            var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex);
            if (row.runId != incoming.runId || row.sequence <= 0 || row.sequence >= incoming.nextSequence || !canonical.TryAdd(key.ToString(), row))
                throw new InvalidOperationException("invasion-observations-canonical-identity-conflict");
        }
    }
    private static GameplayOutcomeId Identity(GameplayOutcomeTombstoneSnapshot row) => new(new GameplayOutcomeRunId(row.runId), row.sequence);
    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row)
    {
        if (row == null) throw new InvalidOperationException("invasion-observations-canonical-null");
        return new GameplayOutcomeTombstoneSnapshot
        {
            runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
            commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
            temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
            parentRunId = row.parentRunId, parentSequence = row.parentSequence, deliveryMetadataVersion = row.deliveryMetadataVersion,
            deliveryClockId = row.deliveryClockId, recordedAbsoluteHour = row.recordedAbsoluteHour,
            publishedRevision = row.publishedRevision, deliveryFaultCount = row.deliveryFaultCount,
            deliveryDelayReason = row.deliveryDelayReason, canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
        };
    }
}
