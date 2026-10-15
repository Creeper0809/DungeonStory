using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

// Native identity/canonical payload joins belong to the observer, never to seasonal gameplay.
public sealed class SeasonalTerminalFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(DungeonSaveSectionPayload.ReadOrNew<SeasonalEventWorldSaveData>(save, SeasonalWorldEventsSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("seasonal-terminal-save-join:" + error.Message); }
    }

    public static void Validate(SeasonalEventWorldSaveData owners, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (owners?.activeEvents == null || incoming?.outbox == null || incoming.exactOutcomes == null
            || incoming.tombstones == null || counters == null)
            throw new InvalidOperationException("seasonal-terminal-save-state-missing");
        var known = incoming.tombstones.Where(row => row != null).ToList();
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null))
            known.Add(new GameplayOutcomeTombstoneSnapshot
            {
                runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                operationId = row.operationId, commitRevision = row.commitRevision, localResultIndex = row.localResultIndex,
                ownerRevision = row.ownerRevision, temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
                absoluteDay = row.absoluteDay, parentRunId = row.parentRunId, parentSequence = row.parentSequence,
                deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
                recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
                deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
                canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
            });
        var index = known.ToLookup(row => new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
            row.commitRevision, row.localResultIndex).ToString());
        foreach (var owner in owners.activeEvents)
        {
            if (owner?.seasonalWildlifeArrival == null || owner.seasonalDriftCargo == null)
                throw new InvalidOperationException("seasonal-terminal-owner-missing");
            SeasonalTerminalObservation.ValidateWildlife(owner);
            SeasonalTerminalObservation.ValidateCargo(owner);
            var wildlife = owner.seasonalWildlifeArrival;
            if (wildlife.configured)
                Join(wildlife.observation.IsEmpty ? null : new SeasonalWildlifeArrivalFact(wildlife.observation),
                    wildlife.publication, wildlife.outcomeResultKey, MigratedProducerOutcomeKind.SeasonalWildlifeArrivalResult,
                    "seasonal-wildlife-arrival:occurrence=" + owner.instanceId + ":species=" + wildlife.speciesId,
                    SeasonalTerminalFactMapping.Capture, index, incoming, counters);
            var cargo = owner.seasonalDriftCargo;
            if (cargo.configured)
                Join(cargo.observation.IsEmpty ? null : new SeasonalDriftCargoFact(cargo.observation),
                    cargo.publication, cargo.outcomeResultKey, MigratedProducerOutcomeKind.SeasonalDriftCargoResult,
                    "seasonal-drift-cargo:occurrence=" + owner.instanceId,
                    SeasonalTerminalFactMapping.Capture, index, incoming, counters);
        }
    }

    private static void Join<T>(T fact, SeasonalTerminalPublicationSaveData proof, string completed,
        MigratedProducerOutcomeKind kind, string ownerIdentity, Func<T, MigratedDomainFactCapture> capture,
        ILookup<string, GameplayOutcomeTombstoneSnapshot> index, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters) where T : class
    {
        string expected = MigratedProducerOutcomeCatalog.ResultKey(kind, ownerIdentity, 1L).ToString();
        var found = index[expected].ToList();
        if (found.Count > 1) throw new InvalidOperationException("seasonal-terminal-duplicate-publication");
        if (proof.key.Length == 0)
        {
            if (found.Count != 0) throw new InvalidOperationException("seasonal-terminal-lost-publication-key");
            return;
        }
        if (fact == null || proof.key != expected) throw new InvalidOperationException("seasonal-terminal-key-conflict");
        if (completed.Length != 0 && (found.Count != 1
            || proof.id != new GameplayOutcomeId(new GameplayOutcomeRunId(found[0].runId), found[0].sequence).ToString()
            || proof.fingerprint != found[0].canonicalHash))
            throw new InvalidOperationException("seasonal-terminal-completion-proof-conflict");
        var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 2, largePageCount: 2, knownResultKeyCapacity: 4, retainedSmallPageCount: 2, retainedLargePageCount: 2));
        var candidate = ledger.PrepareGameplayOutcomeRestore(new GameplayOutcomeLedgerSaveData
        {
            runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
            nextSequence = incoming.nextSequence, knownResultKeyHighWater = found.Count, tombstones = found
        });
        ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
        ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        migrated.Restore(migrated.PrepareRestore(counters));
        var observer = new MigratedDomainFactObserver<T>(capture, migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        bool accepted = observer.TryPrepareReplay(proof.key, fact, out var publication, out string failure);
        publication?.Cancel();
        if (!accepted) throw new InvalidOperationException("seasonal-terminal-payload-conflict:" + failure);
    }
}
