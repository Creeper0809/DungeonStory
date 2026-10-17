using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

/// <summary>Observer-owned canonical join; physical and wildlife owners keep only opaque keys.</summary>
public sealed class PhysicalTransformationFactPreflight :
    IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(
                DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(save, PhysicalItemsSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<DungeonWildlifeSaveData>(save, WildlifeSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("physical-transformation-save-join:" + exception.Message); }
    }

    public static void Validate(DungeonPhysicalItemSaveData physical, DungeonWildlifeSaveData wildlife,
        GameplayOutcomeLedgerSaveData incoming, DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (physical?.physicalItemTransformations == null || wildlife?.carcassProcessing == null
            || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters == null)
            throw new InvalidOperationException("transformation-save-state-missing");
        var identities = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        foreach (var row in incoming.tombstones) if (row != null) Add(row);
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            if (row != null) Add(new GameplayOutcomeTombstoneSnapshot
            {
                runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                operationId = row.operationId, commitRevision = row.commitRevision,
                localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
                parentRunId = row.parentRunId, parentSequence = row.parentSequence,
                deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
                recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
                deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
                canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
            });
        var parents = new Dictionary<string, WildlifeCarcassProcessingSaveData>(StringComparer.Ordinal);
        foreach (var parent in wildlife.carcassProcessing)
        {
            if (parent == null) throw new InvalidOperationException("carcass-parent-null");
            parent.Validate();
            if (!parents.TryAdd(parent.publicationKey, parent))
                throw new InvalidOperationException("carcass-parent-duplicate");
        }
        var registry = new GameplayOutcomeRegistry(
            MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        // Reuse a bounded two-row comparison authority for the whole join.
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 2, retainedLargePageCount: 2));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        migrated.Restore(migrated.PrepareRestore(counters));
        var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
        var childObserver = new MigratedDomainFactObserver<PhysicalItemTransformedFact>(
            PhysicalItemTransformedFactMapping.Capture, migrated, commits);
        var parentObserver = new MigratedDomainFactObserver<WildlifeCarcassProcessedFact>(
            WildlifeCarcassProcessedFactMapping.Capture, migrated, commits);
        var joined = new HashSet<string>(StringComparer.Ordinal);
        var joinedParents = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in physical.physicalItemTransformations)
        {
            if (child == null) throw new InvalidOperationException("physical-transform-null");
            child.Validate();
            var fact = new PhysicalItemTransformedFact(child.fact);
            string expected = MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.PhysicalItemTransformReceipt, child.fact.operationId, 1L).ToString();
            if (child.publicationKey != expected || !joined.Add(expected))
                throw new InvalidOperationException("physical-transform-key-conflict");
            bool exists = identities.TryGetValue(expected, out var identity);
            if (child.publicationConfirmed && !exists || child.rollbackBlocked && exists)
                throw new InvalidOperationException("physical-transform-confirmation-conflict");
            var selected = new List<GameplayOutcomeTombstoneSnapshot>();
            if (exists) selected.Add(identity);
            WildlifeCarcassProcessingSaveData parent = null;
            if (child.companionPublicationKey != string.Empty)
            {
                if (!parents.TryGetValue(child.companionPublicationKey, out parent)
                    || !joinedParents.Add(parent.publicationKey)
                    || parent.fact.transformOperationId != child.fact.operationId
                    || parent.fact.absoluteDay != child.fact.absoluteDay
                    || parent.fact.inputQuantity != child.fact.inputQuantity
                    || parent.fact.outputQuantity != child.fact.outputQuantity
                    || child.fact.inputs.Count != 1
                    || parent.fact.carcassStackId != child.fact.inputs[0].stackId
                    || parent.publicationConfirmed && !child.publicationConfirmed)
                    throw new InvalidOperationException("physical-transform-companion-conflict");
                string parentExpected = MigratedProducerOutcomeCatalog.ResultKey(parent.fact.taboo
                    ? MigratedProducerOutcomeKind.CharacterTabooIncidentEvent
                    : MigratedProducerOutcomeKind.AnimalHusbandryWorkResult, parent.fact.operationId, 1L).ToString();
                if (parentExpected != parent.publicationKey)
                    throw new InvalidOperationException("carcass-parent-key-conflict");
                bool parentExists = identities.TryGetValue(parentExpected, out var parentIdentity);
                if (exists != parentExists)
                    throw new InvalidOperationException("physical-transform-partial-cohort");
                if (parentExists)
                {
                    if (identity.runId != parentIdentity.runId || parentIdentity.sequence != identity.sequence + 1)
                        throw new InvalidOperationException("physical-transform-cohort-identity-conflict");
                    selected.Add(parentIdentity);
                }
            }
            var subset = new GameplayOutcomeLedgerSaveData
            {
                runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
                nextSequence = incoming.nextSequence, knownResultKeyHighWater = selected.Count, tombstones = selected
            };
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            bool accepted = childObserver.TryPrepareReplay(child.publicationKey, fact,
                out IDomainFactPreparedPublication prepared, out string failure);
            prepared?.Cancel();
            if (!accepted) throw new InvalidOperationException("physical-transform-canonical-conflict:" + failure);
            if (parent != null)
            {
                accepted = parentObserver.TryPrepareReplay(parent.publicationKey,
                    new WildlifeCarcassProcessedFact(parent.fact), out prepared, out failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("carcass-parent-canonical-conflict:" + failure);
            }
        }
        if (joinedParents.Count != parents.Count)
            throw new InvalidOperationException("carcass-parent-lost-physical-journal");
        string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.PhysicalItemTransformReceipt).ProducerId;
        foreach (var pair in identities)
            if (pair.Value.producerId == producer && !joined.Contains(pair.Key))
                throw new InvalidOperationException("physical-transform-lost-owner-journal");

        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
                row.commitRevision, row.localResultIndex).ToString();
            if (!identities.TryAdd(key, row))
                throw new InvalidOperationException("transformation-publication-duplicate");
        }
    }
}
