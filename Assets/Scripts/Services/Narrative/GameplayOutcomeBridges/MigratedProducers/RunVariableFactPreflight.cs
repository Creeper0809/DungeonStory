using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// Only this observer-side boundary interprets the owner's opaque publication key.
public sealed class RunVariableFactPreflight : IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator, IDungeonSaveRegistryPreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) =>
        Validate(save.sections.ToDictionary(row => row.sectionId, StringComparer.Ordinal), report);

    public void Validate(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections, DungeonGameRestoreReport report)
    {
        try
        {
            var owner = Read<DungeonRunVariableSaveData>(sections, RunVariableSaveSection.Id, DungeonRunVariableSaveData.CurrentVersion);
            RunVariableSaveSection.ValidateObservationJson(sections[RunVariableSaveSection.Id].payloadJson);
            RunVariableSaveSection.ValidateObservationOwners(owner);
            if (owner.version != DungeonRunVariableSaveData.CurrentVersion)
                throw new InvalidOperationException("run-variable-fact-owner-version-conflict");
            if (owner.pendingObservations.Count == 0) return;
            ValidatePending(owner,
                Read<GameplayOutcomeLedgerSaveData>(sections, GameplayOutcomeLedgerSaveSection.Id, GameplayOutcomeLedgerSaveData.CurrentVersion),
                Read<DungeonMigratedProducerOutcomeSaveData>(sections, MigratedProducerOutcomeSaveSection.Id, DungeonMigratedProducerOutcomeSaveData.CurrentVersion));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("run-variable-fact-save-join:" + error.Message); }
    }

    public static void ValidatePending(DungeonRunVariableSaveData owner, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        RunVariableSaveSection.ValidateObservationOwners(owner);
        if (owner.pendingObservations.Count == 0) return;
        if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
            throw new InvalidOperationException("run-variable-fact-native-state-missing");
        var row = owner.pendingObservations.Single();
        var fact = new RunVariableFact(row.fact);
        var capture = RunVariableFactMapping.Capture(fact);
        string expectedKey = MigratedProducerOutcomeCatalog.ResultKey(capture.Kind, capture.OwnerIdentity, 1).ToString();
        string producer = MigratedProducerOutcomeCatalog.Get(capture.Kind).ProducerId;
        var canonical = incoming.outbox.Concat(incoming.exactOutcomes).Select(RetiredCopy).Concat(incoming.tombstones)
            .ToDictionary(value => new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId),
                value.commitRevision, value.localResultIndex).ToString(), StringComparer.Ordinal);
        var revisions = counters.owners.ToDictionary(value => value.ownerKey, value => value.revision, StringComparer.Ordinal);
        canonical.TryGetValue(expectedKey, out var actual);
        long revision = revisions.TryGetValue(producer + "|" + capture.OwnerIdentity, out long value) ? value : 0;
        if (row.publicationKey != expectedKey || (actual != null ? revision != 1 : revision is not (0 or 1))
            || actual != null && (actual.absoluteDay != capture.AbsoluteDay || actual.ownerRevision != 1 || actual.commitRevision != 1))
            throw new InvalidOperationException("run-variable-fact-native-identity-conflict");

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
        var observer = new MigratedDomainFactObserver<RunVariableFact>(RunVariableFactMapping.Capture,
            migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        bool accepted = observer.TryPrepareReplay(expectedKey, fact, out var prepared, out string failure);
        prepared?.Cancel();
        if (!accepted) throw new InvalidOperationException("run-variable-fact-canonical-conflict:" + failure);
    }

    private static T Read<T>(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections, string id, int version) where T : class
    {
        if (!sections.TryGetValue(id, out var row) || row?.sectionVersion != version)
            throw new InvalidOperationException("run-variable-fact-section-missing:" + id);
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
