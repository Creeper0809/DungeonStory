using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Only the two latest retained staff facts join canonical history; prior completed rows may outlive their terminal.</summary>
public sealed class StaffDiscontentFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonStaffDiscontentSaveData>(save, StaffDiscontentSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(DungeonStaffDiscontentSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            var shape = new DungeonGameRestoreReport();
            StaffDiscontentSaveSection.ValidatePayload(source, shape);
            if (!shape.Success) throw new InvalidOperationException(string.Join(" | ", shape.Errors));
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("staff-discontent-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("staff-discontent-canonical-row-null");
                if (!IsStaff(row.producerId)) continue;
                Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision, localResultIndex = row.localResultIndex,
                    ownerRevision = row.ownerRevision, canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("staff-discontent-canonical-tombstone-null");
                if (IsStaff(row.producerId)) Add(row);
            }
            if (StaffDiscontentFactValues.IsEmpty(source.transitionObservation.fact)
                && StaffDiscontentFactValues.IsEmpty(source.responseObservation.fact)) return;
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                    { new StaffDiscontentTransitionOutcomeDescriptor(new KoreanJosaFormatter()), new StaffDiscontentOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new StaffDiscontentTransitionOutcomeAdapter(), new StaffDiscontentOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
            if (!StaffDiscontentFactValues.IsEmpty(source.transitionObservation.fact))
            {
                var fact = new StaffDiscontentTransitionedFact(source.transitionObservation.fact);
                ValidateRecord(fact, source.transitionObservation, StaffDiscontentFactMapping.Capture(fact).ResultKey,
                    new GameplayDomainFactObserver<StaffDiscontentTransitionedFact, StaffDiscontentTransitionOutcomeReceipt>(
                        StaffDiscontentFactMapping.Capture, registry, ledger, recorder, commits));
            }
            if (!StaffDiscontentFactValues.IsEmpty(source.responseObservation.fact))
            {
                var fact = new StaffRebellionResponseResolvedFact(source.responseObservation.fact);
                ValidateRecord(fact, source.responseObservation, StaffDiscontentFactMapping.Capture(fact).ResultKey,
                    new GameplayDomainFactObserver<StaffRebellionResponseResolvedFact, StaffDiscontentOutcomeReceipt>(
                        StaffDiscontentFactMapping.Capture, registry, ledger, recorder, commits));
            }
            void ValidateRecord<TFact, TReceipt>(TFact fact, StaffDiscontentObservationData observation, GameplayResultKey key,
                GameplayDomainFactObserver<TFact, TReceipt> observer)
            {
                if (observation.publicationKey != key.ToString()) throw new InvalidOperationException("staff-discontent-publication-key-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (rows.TryGetValue(observation.publicationKey, out var canonical))
                {
                    if (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence
                        || canonical.ownerRevision != observation.fact.revision)
                        throw new InvalidOperationException("staff-discontent-canonical-identity-conflict");
                    subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1;
                }
                else if (!observation.pending) throw new InvalidOperationException("staff-discontent-completed-publication-missing");
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(observation.publicationKey, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("staff-discontent-canonical-payload-conflict:" + failure);
            }
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
                if (!rows.TryAdd(key, row)) throw new InvalidOperationException("staff-discontent-canonical-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("staff-discontent-observation-save-join:" + exception.Message); }
    }
    private static bool IsStaff(string producer) => producer == StaffDiscontentTransitionOutcomeIds.ProducerId
        || producer == StaffDiscontentOutcomeIds.ProducerId;
}
