using System;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;

// The observer, never the analyzing gameplay owner, interprets native ledger identities.
public sealed class TraitAnalysisFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value => value?.sectionId == CharacterNarrativeSaveSection.Id);
            if (section == null || section.sectionVersion != CharacterNarrativeWorldSaveData.CurrentVersion)
                throw new InvalidOperationException("trait-analysis-owner-section-missing-or-version-unsupported");
            CharacterNarrativeSaveSection.ValidateTraitAnalysisObservationJson(JObject.Parse(section.payloadJson));
            var owner = DungeonSaveJson.Deserialize<CharacterNarrativeWorldSaveData>(section.payloadJson);
            if (owner?.version != CharacterNarrativeWorldSaveData.CurrentVersion || owner.pendingTraitAnalysis == null)
                throw new InvalidOperationException("trait-analysis-owner-missing-or-version-unsupported");
            var pending = owner.pendingTraitAnalysis;
            pending.ValidateObservationState();
            if ((TraitAnalysisCommitPhase)pending.phase == TraitAnalysisCommitPhase.None) return;
            if (!DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger)
                || !DungeonSaveSectionPayload.TryRead(save, PhysicalItemsSaveSection.Id, out DungeonPhysicalItemSaveData physical)
                || physical.pendingBatchDispositions == null)
                throw new InvalidOperationException("trait-analysis-observation-or-physical-section-missing");
            ValidateObservation(pending, ledger);
            var receipts = physical.pendingBatchDispositions.Where(row => row?.operationId == pending.operationId).ToArray();
            if (receipts.Length > 1 || receipts.Length == 0 && pending.phase == (int)TraitAnalysisCommitPhase.RevealPublished)
                throw new InvalidOperationException("trait-analysis-physical-pending-missing-or-duplicate");
            if (receipts.Length == 1)
            {
                var receipt = receipts[0];
                if (receipt.kind != (int)PhysicalItemDispositionKind.Sink
                    || receipt.reasonCode != TraitAnalysisCommandRuntime.DispositionReasonCode || receipt.quantity != 1
                    || receipt.sourceStackIds == null || receipt.sourceStackIds.Count != 1
                    || receipt.sourceStackIds[0] != pending.sourceStackId
                    || pending.phase >= (int)TraitAnalysisCommitPhase.RevealPublished
                        && (receipt.commitId != pending.physicalCommitId || receipt.inputMassGrams != pending.physicalInputMassGrams
                            || !receipt.sourceStackIds.SequenceEqual(pending.physicalSourceStackIds, StringComparer.Ordinal)))
                    throw new InvalidOperationException("trait-analysis-physical-pending-conflict");
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException
            or Newtonsoft.Json.JsonException)
        { report.AddError("trait-analysis-observation-save-join:" + error.Message); }
    }

    public static void ValidateObservation(TraitAnalysisCommitSaveData pending, GameplayOutcomeLedgerSaveData incoming)
    {
        if (pending == null || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("trait-analysis-observation-save-missing");
        pending.ValidateObservationState();
        var phase = (TraitAnalysisCommitPhase)pending.phase;
        if (phase is not (TraitAnalysisCommitPhase.IntentRecorded or TraitAnalysisCommitPhase.RevealPublished
            or TraitAnalysisCommitPhase.OutcomePublished))
            throw new InvalidOperationException("trait-analysis-observation-owner-phase-invalid");
        var key = new GameplayResultKey(HeritableTraitAnalysisOutcomeIds.ProducerId,
            new GameplayOperationId(pending.operationId), checked((long)pending.operationSequence + 1L), 0);
        if (pending.publicationKey.Length > 0 && pending.publicationKey != key.ToString())
            throw new InvalidOperationException("trait-analysis-observation-key-conflict");
        var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null
            && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value).ToArray();
        var retired = incoming.tombstones.Where(row => row != null && row.producerId == key.ProducerId
            && row.operationId == key.OperationId.Value).ToArray();
        if (rows.Length + retired.Length > 1) throw new InvalidOperationException("trait-analysis-canonical-duplicate");
        var live = rows.SingleOrDefault();
        var canonical = live == null ? retired.SingleOrDefault() : RetiredCopy(live);
        bool accepted = pending.publicationId.Length > 0;
        if (canonical == null && accepted)
            throw new InvalidOperationException("trait-analysis-accepted-canonical-missing");
        if (canonical != null && (phase == TraitAnalysisCommitPhase.IntentRecorded
            || pending.publicationKey.Length == 0 || canonical.runId != incoming.runId
            || canonical.sequence < 0 || canonical.sequence >= incoming.nextSequence
            || canonical.commitRevision != key.CommitRevision || canonical.ownerRevision != key.CommitRevision
            || canonical.localResultIndex != key.LocalResultIndex || canonical.absoluteDay != pending.absoluteDay
            || accepted && (pending.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(canonical.runId), canonical.sequence).ToString()
                || pending.publicationFingerprint != canonical.canonicalHash)
            || phase == TraitAnalysisCommitPhase.OutcomePublished && incoming.outbox.Contains(live)))
            throw new InvalidOperationException("trait-analysis-canonical-proof-conflict");
        if (phase == TraitAnalysisCommitPhase.IntentRecorded) return;

        var receipt = HeritableTraitsAnalyzedFactMapping.Capture(pending.CreateCompletedFact());
        var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new HeritableTraitAnalysisOutcomeDescriptor() },
            new IGameplayOutcomeAdapterRegistration[] { new HeritableTraitAnalysisOutcomeAdapter() });
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
            throw new InvalidOperationException("trait-analysis-canonical-fact-conflict:" + result.DetailCode);
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
