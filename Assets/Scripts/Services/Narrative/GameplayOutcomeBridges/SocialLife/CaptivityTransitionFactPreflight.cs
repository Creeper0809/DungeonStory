using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>Joins the retained transition fact to canonical history without deriving it from current actor state.</summary>
public sealed class CaptivityTransitionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<CaptivitySaveData>(save, CaptivitySaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(CaptivitySaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            var shape = new DungeonGameRestoreReport();
            CaptivitySaveValidation.Validate(source, shape);
            if (!shape.Success) throw new InvalidOperationException(string.Join(" | ", shape.Errors));
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("captivity-transition-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("captivity-transition-canonical-row-null");
                if (!IsTransition(row.producerId)) continue;
                Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision, localResultIndex = row.localResultIndex,
                    ownerRevision = row.ownerRevision, canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("captivity-transition-canonical-tombstone-null");
                if (IsTransition(row.producerId)) Add(row);
            }
            if (!source.captives.Any(value => value.rehabilitationTerminal.HasOutcome || value.releaseTerminal.HasOutcome)) return;
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new CaptivityRehabilitationOutcomeDescriptor(), new CaptivityReleaseOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new CaptivityRehabilitationOutcomeAdapter(), new CaptivityReleaseOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var commitGroup = new GameplayDomainFactCommitGroup(recorder, ledger);
            var rehabilitationObserver = new GameplayDomainFactObserver<CaptivityRehabilitationProgressedFact, CaptivityRehabilitationOutcomeReceipt>(
                CaptivityTransitionFactMapping.Capture, registry, ledger, recorder, commitGroup);
            var releaseObserver = new GameplayDomainFactObserver<CaptivityReleasedFact, CaptivityReleaseOutcomeReceipt>(
                CaptivityTransitionFactMapping.Capture, registry, ledger, recorder, commitGroup);
            foreach (var captive in source.captives)
            {
                var rehabilitation = captive.rehabilitationTerminal;
                if (rehabilitation.HasOutcome)
                {
                    var fact = new CaptivityRehabilitationProgressedFact(captive.captiveId, rehabilitation);
                    ValidateRecord(fact, rehabilitation.publicationKey, fact.OwnerRevision, rehabilitation.outcomePending, true,
                        CaptivityTransitionFactMapping.Capture(fact).ResultKey, rehabilitationObserver);
                }
                var release = captive.releaseTerminal;
                if (release.HasOutcome)
                {
                    var fact = new CaptivityReleasedFact(captive.captiveId, release);
                    ValidateRecord(fact, release.publicationKey, fact.OwnerRevision, release.outcomePending, release.effectsApplied,
                        CaptivityTransitionFactMapping.Capture(fact).ResultKey, releaseObserver);
                }
            }
            // Only the latest owner terminal is retained. Older completed rehabilitation rows remain valid history.
            void ValidateRecord<TFact, TReceipt>(TFact fact, string publicationKey, long revision, bool pending, bool applied,
                GameplayResultKey key, GameplayDomainFactObserver<TFact, TReceipt> observer)
            {
                if (publicationKey != key.ToString()) throw new InvalidOperationException("captivity-transition-publication-key-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (rows.TryGetValue(publicationKey, out var canonical))
                {
                    if (!applied) throw new InvalidOperationException("captivity-transition-unapplied-canonical-conflict");
                    if (canonical.runId != incoming.runId || canonical.sequence >= incoming.nextSequence || canonical.ownerRevision != revision)
                        throw new InvalidOperationException("captivity-transition-canonical-identity-conflict");
                    subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1;
                    rows.Remove(publicationKey);
                }
                else if (!pending) throw new InvalidOperationException("captivity-transition-completed-publication-missing");
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(publicationKey, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("captivity-transition-canonical-payload-conflict:" + failure);
            }
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
                if (!rows.TryAdd(key, row)) throw new InvalidOperationException("captivity-transition-canonical-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("captivity-transition-observation-save-join:" + exception.Message); }
    }

    private static bool IsTransition(string producerId) => producerId == CaptivityTransitionOutcomeIds.RehabilitationProducer
        || producerId == CaptivityTransitionOutcomeIds.ReleaseProducer;
}
