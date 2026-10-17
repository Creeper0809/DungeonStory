using System;
using System.Linq;
using DungeonStory.Foundation;

// Native publication identities belong to the observer's detached save join, not the evolution owner.
public sealed class FacilityEvolutionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            if (!DungeonSaveSectionPayload.TryRead(save, ModularFacilityWorldSaveSection.Id, out ModularFacilityWorldSaveData world)
                || !DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger)
                || world?.buildings == null)
                throw new InvalidOperationException("facility-evolution-owner-or-ledger-missing");
            foreach (var building in world.buildings)
            {
                if (building?.stateModules == null) throw new InvalidOperationException("facility-evolution-owner-modules-missing");
                var modules = building.stateModules.Where(value => value?.moduleId == BuildingStateModuleIds.FacilityEvolution).ToArray();
                if (modules.Length > 1) throw new InvalidOperationException("facility-evolution-owner-module-duplicate");
                foreach (var module in modules)
                {
                    if (module.version != FacilityEvolutionStateComponent.SchemaVersion)
                        throw new InvalidOperationException("facility-evolution-owner-version-unsupported");
                    FacilityEvolutionAggregateAdapter.ValidateObservationJson(module.payload);
                    var owner = DungeonSaveJson.Deserialize<FacilityEvolutionStateSnapshot>(module.payload);
                    FacilityEvolutionAggregateAdapter.ValidatePendingMaterialCommit(owner);
                    if (string.IsNullOrEmpty(owner.pendingMaterialCommit?.operationId)) continue;
                    if (owner.pendingMaterialCommit.sourceFacilityPersistentId != building.persistentInstanceId)
                        throw new InvalidOperationException("facility-evolution-owner-identity-conflict");
                    ValidateObservation(owner.pendingMaterialCommit, ledger);
                }
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException
            or Newtonsoft.Json.JsonException)
        { report.AddError("facility-evolution-observation-save-join:" + error.Message); }
    }

    public static void ValidateObservation(FacilityEvolutionPendingMaterialCommitSnapshot pending, GameplayOutcomeLedgerSaveData incoming)
    {
        if (pending?.completedFact == null || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("facility-evolution-observation-save-missing");
        var receipt = FacilityEvolutionCompletedFactMapping.Capture(new FacilityEvolutionCompletedFact(pending.completedFact));
        var key = receipt.ResultKey;
        if (pending.publicationKey != string.Empty && pending.publicationKey != key.ToString())
            throw new InvalidOperationException("facility-evolution-observation-key-conflict");
        if (pending.phase == FacilityEvolutionMaterialCommitPhase.DomainApplied && string.IsNullOrEmpty(pending.publicationKey))
            throw new InvalidOperationException("facility-evolution-applied-observation-key-missing");
        var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null
            && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value).ToArray();
        var retired = incoming.tombstones.Where(row => row != null && row.producerId == key.ProducerId
            && row.operationId == key.OperationId.Value).ToArray();
        if (rows.Length + retired.Length > 1) throw new InvalidOperationException("facility-evolution-canonical-duplicate");
        var live = rows.SingleOrDefault();
        var canonical = live == null ? retired.SingleOrDefault() : RetiredCopy(live);
        if (canonical == null && pending.observationAccepted)
            throw new InvalidOperationException("facility-evolution-accepted-canonical-missing");
        if (canonical != null && (pending.phase != FacilityEvolutionMaterialCommitPhase.DomainApplied
            || string.IsNullOrEmpty(pending.publicationKey) || canonical.runId != incoming.runId
            || canonical.sequence < 0 || canonical.sequence >= incoming.nextSequence
            || canonical.commitRevision != key.CommitRevision || canonical.ownerRevision != key.CommitRevision
            || canonical.localResultIndex != key.LocalResultIndex || canonical.absoluteDay != receipt.AbsoluteDay
            || pending.observationAccepted && (pending.publicationId !=
                new GameplayOutcomeId(new GameplayOutcomeRunId(canonical.runId), canonical.sequence).ToString()
                || pending.publicationFingerprint != canonical.canonicalHash)
            || pending.observationDelivered && incoming.outbox.Contains(live)))
            throw new InvalidOperationException("facility-evolution-canonical-proof-conflict");

        // One bounded comparison ledger. A compacted row still binds the full canonical payload.
        var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new FacilityEvolutionOutcomeDescriptor() },
            new IGameplayOutcomeAdapterRegistration[] { new FacilityEvolutionOutcomeAdapter() });
        var comparison = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var subset = new GameplayOutcomeLedgerSaveData
        {
            runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision,
            nextSequence = incoming.nextSequence, knownResultKeyHighWater = canonical == null ? 0 : 1
        };
        if (canonical != null) subset.tombstones.Add(canonical);
        var candidate = comparison.PrepareGameplayOutcomeRestore(subset);
        comparison.BeginRestoreCandidate(); comparison.PublishGameplayOutcomeRestore(candidate);
        comparison.PublishRestoreCandidate(); comparison.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(comparison, registry, new GameEventBus());
        var result = recorder.TryPrepare(receipt, out var token);
        recorder.CancelPrepared(token);
        if (canonical == null ? !result.Success : result.Code != OutcomePrepareCode.AlreadyTerminal)
            throw new InvalidOperationException("facility-evolution-canonical-fact-conflict:" + result.DetailCode);
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row) => new()
    {
        runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
        commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
        temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
        parentRunId = row.parentRunId, parentSequence = row.parentSequence,
        deliveryMetadataVersion = row.deliveryMetadataVersion, deliveryClockId = row.deliveryClockId,
        recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
        deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
        canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
    };
}
