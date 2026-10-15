using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;

public sealed class DefenseActivationFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(row => row?.sectionId == DefenseActivationPublicationSaveSection.Id);
            if (section == null || section.sectionVersion != DefenseActivationPublicationSaveData.CurrentVersion)
                throw new InvalidOperationException("defense-activation-section-missing");
            DefenseActivationPublicationSaveSection.ValidateJson(section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger)
                || !DungeonSaveSectionPayload.TryRead(save, DefenseFacilitySaveSection.Id, out DefenseFacilitySaveData facilities)
                || !DungeonSaveSectionPayload.TryRead(save, MigratedProducerOutcomeSaveSection.Id, out DungeonMigratedProducerOutcomeSaveData counters))
                throw new InvalidOperationException("defense-activation-owner-section-missing");
            var raw = JObject.Parse(save.sections.Single(row => row.sectionId == DefenseFacilitySaveSection.Id).payloadJson);
            if (raw["facilities"] is not JArray rows || rows.Any(row => row?["activationCount"]?.Type != JTokenType.Integer))
                throw new InvalidOperationException("defense-activation-owner-count-missing");
            var journal = DungeonSaveJson.Deserialize<DefenseActivationPublicationSaveData>(section.payloadJson);
            ValidateOwners(journal, facilities);
            ValidateJournal(journal, ledger, counters);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("defense-activation-save-join:" + error.Message); }
    }

    public static void ValidateOwners(DefenseActivationPublicationSaveData source, DefenseFacilitySaveData facilities)
    {
        var journal = DefenseActivationPublicationRuntime.ValidateAndClone(source);
        var failures = DefenseFacilitySaveRules.Validate(facilities);
        if (failures.Count != 0) throw new InvalidOperationException("defense-activation-owner-invalid:" + string.Join("|", failures));
        var owners = facilities.facilities.ToDictionary(row => row.facilityPersistentId, StringComparer.Ordinal);
        if (journal.observations.Any(row => !owners.ContainsKey(row.activation.facilityId)))
            throw new InvalidOperationException("defense-activation-history-owner-missing");
        foreach (var owner in owners.Values)
            if (journal.observations.Count(row => row.activation.facilityId == owner.facilityPersistentId) != owner.activationCount)
                throw new InvalidOperationException("defense-activation-owner-history-incomplete");
    }

    public static void ValidateJournal(DefenseActivationPublicationSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        var journal = DefenseActivationPublicationRuntime.ValidateAndClone(source);
        if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
            throw new InvalidOperationException("defense-activation-ledger-invalid");
        var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes)) Add(RetiredCopy(row));
        foreach (var row in incoming.tombstones) Add(row);
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var triggerProducer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent).ProducerId;
        var stateProducer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent).ProducerId;
        var expectedCounters = new Dictionary<string, long>(StringComparer.Ordinal);
        var uncertainCounters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in journal.observations)
        {
            if (!GameplayDomainFactPairProof.TryReadKeys(row.publicationKey, out string first, out string second))
                throw new InvalidOperationException("defense-activation-pair-key-invalid");
            expected.Add(first); expected.Add(second);
            if (row.effect != null) expected.Add(row.effectPublicationKey);
            long revision = row.activation.activationCount;
            foreach (string producer in new[] { triggerProducer, stateProducer })
            {
                string key = producer + "|" + row.activation.facilityId;
                expectedCounters[key] = revision;
                uncertainCounters.Remove(key);
                if (row.pending && !canonical.ContainsKey(first)) uncertainCounters.Add(key);
            }
        }
        foreach (var pair in canonical)
            if ((pair.Value.producerId == triggerProducer || pair.Value.producerId == stateProducer
                    || pair.Value.producerId == DefenseFacilityEffectOutcomeIds.ProducerId) && !expected.Contains(pair.Key))
                throw new InvalidOperationException("defense-activation-canonical-history-missing");
        var revisions = counters.owners.ToDictionary(row => row.ownerKey, row => row.revision, StringComparer.Ordinal);
        foreach (var pair in expectedCounters)
        {
            long value = revisions.TryGetValue(pair.Key, out long savedRevision) ? savedRevision : 0;
            if (value != pair.Value && !(uncertainCounters.Contains(pair.Key) && value == pair.Value - 1))
                throw new InvalidOperationException("defense-activation-observer-revision-conflict");
        }
        foreach (var pair in revisions)
            if ((pair.Key.StartsWith(triggerProducer + "|", StringComparison.Ordinal)
                    || pair.Key.StartsWith(stateProducer + "|", StringComparison.Ordinal)) && !expectedCounters.ContainsKey(pair.Key))
                throw new InvalidOperationException("defense-activation-observer-history-missing");

        var descriptors = MigratedProducerOutcomeCatalog.Definitions.Select(value =>
            (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter()))
            .Concat(new[] { new DefenseFacilityEffectOutcomeDescriptor(new KoreanJosaFormatter()) });
        var registry = new GameplayOutcomeRegistry(descriptors,
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter(), new DefenseFacilityEffectOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4, retainedSmallPageCount: 2, retainedLargePageCount: 2));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        var observer = new DefenseActivationFactObserver(migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        foreach (var row in journal.observations)
        {
            GameplayDomainFactPairProof.TryReadKeys(row.publicationKey, out string first, out string second);
            canonical.TryGetValue(first, out var trigger); canonical.TryGetValue(second, out var changed);
            if ((trigger == null) != (changed == null) || trigger == null && !row.pending)
                throw new InvalidOperationException("defense-activation-pair-missing-or-torn");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (trigger != null)
            {
                ValidateIdentity(trigger, row.activation); ValidateIdentity(changed, row.activation);
                subset.tombstones.Add(trigger); subset.tombstones.Add(changed);
                var proof = GameplayDomainFactPairProof.Combine(row.publicationKey, Delivery(first, trigger), Delivery(second, changed));
                if (!row.pending && (row.publicationId != proof.PublicationId || row.fingerprint != proof.Fingerprint))
                    throw new InvalidOperationException("defense-activation-proof-conflict");
            }
            GameplayOutcomeTombstoneSnapshot effect = null;
            if (row.effect != null)
            {
                if (trigger == null || DefenseEffectFactMapping.ResultKey(row.effect).ToString() != row.effectPublicationKey)
                    throw new InvalidOperationException("defense-effect-parent-or-key-conflict");
                canonical.TryGetValue(row.effectPublicationKey, out effect);
                if (effect == null && !row.effectPending) throw new InvalidOperationException("defense-effect-result-missing");
                if (effect != null)
                {
                    ValidateIdentity(effect, row.activation); subset.tombstones.Add(effect);
                    if (!row.effectPending && (row.effectPublicationId != Identity(effect).ToString() || row.effectFingerprint != effect.canonicalHash))
                        throw new InvalidOperationException("defense-effect-proof-conflict");
                }
            }
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            migrated.Restore(migrated.PrepareRestore(counters));
            if (!observer.TryPrepareReplay(row.publicationKey, new DefenseActivationFact(row.activation), out var prepared, out string failure))
                throw new InvalidOperationException("defense-activation-canonical-fact-conflict:" + failure);
            prepared.Cancel();
            if (row.effect == null) continue;
            var result = recorder.TryPrepare(DefenseEffectFactMapping.CaptureWithParent(new DefenseEffectFact(row.effect), Identity(trigger)), out var token);
            recorder.CancelPrepared(token);
            if (effect == null ? !result.Success : result.Code != OutcomePrepareCode.AlreadyTerminal)
                throw new InvalidOperationException("defense-effect-canonical-fact-conflict:" + result.DetailCode);
        }
        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null) throw new InvalidOperationException("defense-activation-canonical-null");
            string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
            if (!canonical.TryAdd(key, row)) throw new InvalidOperationException("defense-activation-canonical-duplicate");
        }
        void ValidateIdentity(GameplayOutcomeTombstoneSnapshot row, DefenseActivationFactData fact)
        {
            if (row.runId != incoming.runId || row.sequence <= 0 || row.sequence >= incoming.nextSequence
                || row.ownerRevision != fact.activationCount || row.absoluteDay != fact.absoluteDay)
                throw new InvalidOperationException("defense-activation-canonical-identity-conflict");
        }
    }
    private static GameplayOutcomeId Identity(GameplayOutcomeTombstoneSnapshot row) =>
        new(new GameplayOutcomeRunId(row.runId), row.sequence);
    private static DomainFactDelivery Delivery(string key, GameplayOutcomeTombstoneSnapshot row) =>
        new(DomainFactDeliveryPhase.Delivered, key, Identity(row).ToString(), row.canonicalHash, string.Empty);
    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row)
    {
        if (row == null) throw new InvalidOperationException("defense-activation-canonical-null");
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
