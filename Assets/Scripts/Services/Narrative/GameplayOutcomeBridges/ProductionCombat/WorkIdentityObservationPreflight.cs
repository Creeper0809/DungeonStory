using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;

// Native proof interpretation stays at the observer boundary, never in the identity owner.
public sealed class WorkIdentityObservationPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    private readonly IGameplayOutcomeRegistry registry;
    public WorkIdentityObservationPreflight(IGameplayOutcomeRegistry registry) => this.registry = registry;
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<CharacterNarrativeWorldSaveData>(save, CharacterNarrativeSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), registry, report);

    internal static void Validate(CharacterNarrativeWorldSaveData source, GameplayOutcomeLedgerSaveData incoming,
        IGameplayOutcomeRegistry registry, DungeonGameRestoreReport report)
    {
        try
        {
            if (source?.version != CharacterNarrativeWorldSaveData.CurrentVersion || source.workCompletionDeliveries == null
                || incoming?.exactOutcomes == null || incoming.outbox == null || incoming.tombstones == null)
                throw new InvalidOperationException("work-identity-observation-sections-missing");
            var cursors = WorkCompletionIdentityDeliveryLedger.ValidateAndClone(source.workCompletionDeliveries);
            var exact = incoming.exactOutcomes.Concat(incoming.outbox).ToDictionary(row => Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex), StringComparer.Ordinal);
            var rows = exact.ToDictionary(pair => pair.Key, pair => RetiredCopy(pair.Value), StringComparer.Ordinal);
            foreach (var row in incoming.tombstones)
                if (!rows.TryAdd(Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex), row))
                    throw new InvalidOperationException("work-identity-canonical-duplicate");
            var byIdentity = rows.Values.ToDictionary(row => Identity(row.runId, row.sequence), StringComparer.Ordinal);
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(smallPageCount: 2, largePageCount: 2,
                knownResultKeyCapacity: Math.Max(8, rows.Count + 1), retainedSmallPageCount: 2, retainedLargePageCount: 2));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<WorkIdentityAppliedFact, WorkCompletionIdentityOutcomeReceipt>(
                new WorkIdentityAppliedFactMapping(ledger, ledger).Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var cursor in cursors)
            {
                if (cursor.disposition is not (WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome
                    or WorkCompletionIdentityDeliveryDisposition.EffectsAppliedPendingObservation)) continue;
                var fact = new WorkIdentityAppliedFact(cursor.observation);
                string expected = WorkCompletionIdentityOutcomeIds.ResultKey(fact.Request).ToString();
                if (cursor.publicationKey != expected) throw new InvalidOperationException("work-identity-publication-key-conflict");
                rows.TryGetValue(expected, out var canonical);
                bool completed = cursor.disposition == WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome;
                if (completed && (canonical == null || Identity(canonical.runId, canonical.sequence) != cursor.publicationId
                    || canonical.canonicalHash != cursor.publicationFingerprint))
                    throw new InvalidOperationException("work-identity-publication-proof-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
                var used = new HashSet<string>(StringComparer.Ordinal);
                void AddBranch(GameplayOutcomeTombstoneSnapshot row, bool keepExact)
                {
                    var chain = new HashSet<string>(StringComparer.Ordinal);
                    while (row != null)
                    {
                        string id = Identity(row.runId, row.sequence);
                        if (!chain.Add(id)) throw new InvalidOperationException("work-identity-parent-cycle");
                        if (!used.Add(id)) break;
                        string rowKey = Key(row.producerId, row.operationId, row.commitRevision, row.localResultIndex);
                        if (keepExact && exact.TryGetValue(rowKey, out var retained)) subset.exactOutcomes.Add(retained);
                        else subset.tombstones.Add(row);
                        keepExact = false;
                        if (row.parentSequence <= 0) break;
                        if (!byIdentity.TryGetValue(Identity(row.parentRunId, row.parentSequence), out row))
                            throw new InvalidOperationException("work-identity-parent-missing");
                    }
                }
                if (fact.Request.ChildResultKey != WorkCompletionIdentityReferenceDefaults.None)
                {
                    if (!rows.TryGetValue(fact.Request.ChildResultKey, out var parent))
                        throw new InvalidOperationException("work-identity-child-missing");
                    AddBranch(parent, true);
                }
                if (canonical != null) AddBranch(canonical, false);
                subset.knownResultKeyHighWater = used.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate); ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(expected, fact, out var publication, out string failure);
                publication?.Cancel();
                if (!accepted) throw new InvalidOperationException("work-identity-observation-payload-conflict:" + failure);
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("work-identity-observation-save-join:" + error.Message); }
    }

    private static string Key(string producer, string operation, long revision, int index) => new GameplayResultKey(producer, new GameplayOperationId(operation), revision, index).ToString();
    private static string Identity(string run, long sequence) => run + "/" + sequence.ToString(CultureInfo.InvariantCulture);
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
