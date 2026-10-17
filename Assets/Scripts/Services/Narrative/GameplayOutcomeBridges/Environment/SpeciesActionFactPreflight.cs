using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Only observers interpret canonical receipt identities and payloads.</summary>
public sealed class SpeciesActionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<CharacterSpeciesRuntimeSaveData>(save, SpeciesRuntimeSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(CharacterSpeciesRuntimeSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            if (source?.characters == null || source.pendingObservations == null
                || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("species-observation-save-shape-invalid");
            // The section separately checks authored species. Reuse owner state
            // validation here without taking a second content-state authority.
            CharacterSpeciesStateCodec.BuildRestore(source, 0f, id => source.characters
                .FirstOrDefault(value => value != null && value.speciesDefinitionId == id.Value)?.lastIncidentId);
            if (source.pendingObservations.Count == 0) return;
            var josa = new KoreanJosaFormatter();
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
            {
                new GolemUpkeepOutcomeDescriptor(josa), new SpeciesIncidentOutcomeDescriptor(josa),
                new HarpyGaleRelocationOutcomeDescriptor(josa)
            }, new IGameplayOutcomeAdapterRegistration[]
            {
                new GolemUpkeepOutcomeAdapter(), new SpeciesIncidentOutcomeAdapter(), new HarpyGaleRelocationOutcomeAdapter()
            });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
                retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            foreach (var row in source.pendingObservations)
            {
                if (row.actionType == 0) Check(row, SpeciesActionFactMapping.Capture(new GolemUpkeepFact(row.golem)));
                else if (row.actionType == 1) Check(row, SpeciesActionFactMapping.Capture(new SpeciesIncidentFact(row.incident)));
                else Check(row, SpeciesActionFactMapping.Capture(new HarpyGaleFact(row.harpy)));
            }

            void Check<TReceipt>(SpeciesActionObservationSaveData observation, TReceipt receipt)
                where TReceipt : IEnvironmentOutcomeReceipt
            {
                var key = receipt.Payload.ResultKey;
                if (key.ToString() != observation.publicationKey)
                    throw new InvalidOperationException("species-observation-key-conflict");
                var live = incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null
                    && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value).ToArray();
                var retired = incoming.tombstones.Where(row => row != null
                    && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value).ToArray();
                if (live.Length + retired.Length > 1)
                    throw new InvalidOperationException("species-canonical-duplicate");
                var canonical = retired.FirstOrDefault() ?? (live.Length == 0 ? null : new GameplayOutcomeTombstoneSnapshot
                {
                    runId = live[0].runId, sequence = live[0].sequence, producerId = live[0].producerId,
                    operationId = live[0].operationId, commitRevision = live[0].commitRevision,
                    localResultIndex = live[0].localResultIndex, ownerRevision = live[0].ownerRevision,
                    canonicalHash = live[0].immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
                var owner = source.characters.First(value => value.characterInstanceId == observation.CharacterId);
                if (observation.rollbackBlocked && canonical != null)
                    throw new InvalidOperationException("species-rejected-rollback-has-canonical");
                if (observation.actionType == 0 && observation.golem.kind == "recharge"
                    && owner.rechargeObservationCompleted && canonical == null)
                    throw new InvalidOperationException("species-recharge-completion-canonical-missing");
                if (canonical != null && (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence
                    || canonical.commitRevision != observation.Revision || canonical.ownerRevision != observation.Revision
                    || canonical.localResultIndex != 0 || live.Length == 1 && live[0].outcomeTypeId != receipt.Payload.OutcomeTypeId.Value))
                    throw new InvalidOperationException("species-canonical-identity-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (canonical != null) { subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1; }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                var result = recorder.TryPrepare(receipt, out var token);
                recorder.CancelPrepared(token);
                if (canonical == null ? !result.Success : result.Code != OutcomePrepareCode.AlreadyTerminal)
                    throw new InvalidOperationException("species-canonical-payload-conflict:" + result.DetailCode);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("species-observation-save-join:" + exception.Message); }
    }
}
