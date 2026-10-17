using System;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>Joins pending body facts to their canonical observation without reapplying disease.</summary>
public sealed class DiseaseAnatomyBurdenFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<PopulationHealthWorldSaveData>(save, PopulationHealthSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(PopulationHealthWorldSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            if (source?.version != PopulationHealthWorldSaveData.CurrentVersion || source.pendingAnatomyBurdenProjections == null
                || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("disease-body-observation-save-shape-invalid");
            foreach (var projection in source.pendingAnatomyBurdenProjections)
            {
                if (projection == null) throw new InvalidOperationException("disease-body-observation-null");
                var key = new GameplayResultKey(DiseaseAnatomyBurdenOutcomeIds.ProducerId,
                    new GameplayOperationId(projection.operationId), 1L, 0);
                var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null
                    && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value).ToArray();
                var retired = incoming.tombstones.Where(row => row != null
                    && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value).ToArray();
                if (rows.Length + retired.Length > 1)
                    throw new InvalidOperationException("disease-body-observation-canonical-duplicate");
                bool exists = rows.Length + retired.Length == 1;
                if (projection.phase == (int)DiseaseAnatomyBurdenProjectionPhase.Pending)
                {
                    if (exists || projection.publicationKey != string.Empty || projection.characterDisplayName != string.Empty)
                        throw new InvalidOperationException("disease-body-unapplied-canonical-conflict");
                    continue;
                }
                var fact = new DiseaseAnatomyBurdenAppliedFact(projection);
                if (projection.publicationKey == null || projection.publicationKey.Length > 0 && projection.publicationKey != key.ToString()
                    || exists && projection.publicationKey.Length == 0)
                    throw new InvalidOperationException("disease-body-observer-key-conflict");
                var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new DiseaseAnatomyBurdenOutcomeDescriptor() },
                    new IGameplayOutcomeAdapterRegistration[] { new DiseaseAnatomyBurdenOutcomeAdapter() });
                var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(smallPageCount: 1, largePageCount: 1,
                    knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
                if (exists)
                {
                    var canonical = retired.Length == 1 ? retired[0] : new GameplayOutcomeTombstoneSnapshot
                    { runId = rows[0].runId, sequence = rows[0].sequence, producerId = rows[0].producerId,
                        operationId = rows[0].operationId, commitRevision = rows[0].commitRevision,
                        localResultIndex = rows[0].localResultIndex, ownerRevision = rows[0].ownerRevision,
                        canonicalHash = rows[0].immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten };
                    if (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence
                        || canonical.commitRevision != 1 || canonical.ownerRevision != 1 || canonical.localResultIndex != 0
                        || rows.Length == 1 && rows[0].outcomeTypeId != DiseaseAnatomyBurdenOutcomeIds.Applied.Value)
                        throw new InvalidOperationException("disease-body-canonical-identity-conflict");
                    var subset = new GameplayOutcomeLedgerSaveData { runId = incoming.runId,
                        worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence,
                        knownResultKeyHighWater = 1, tombstones = new() { canonical } };
                    var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                    ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                    ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                }
                var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
                var result = recorder.TryPrepare(DiseaseAnatomyBurdenFactMapping.Capture(fact), out var token);
                recorder.CancelPrepared(token);
                if (exists ? result.Code != OutcomePrepareCode.AlreadyTerminal : !result.Success)
                    throw new InvalidOperationException("disease-body-canonical-payload-conflict:" + result.DetailCode);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("disease-body-observation-save-join:" + exception.Message); }
    }
}
