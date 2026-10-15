using System;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;

// Canonical log identities are interpreted at the observation/save boundary, never by gameplay.
public sealed class CharacterAmbitionTransitionFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value => value?.sectionId == CharacterNarrativeSaveSection.Id);
            if (section == null || section.sectionVersion != CharacterNarrativeWorldSaveData.CurrentVersion)
                throw new InvalidOperationException("ambition-transition-owner-section-missing-or-version-unsupported");
            CharacterNarrativeSaveSection.ValidateAmbitionObservationJson(JObject.Parse(section.payloadJson,
                new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }));
            var owner = DungeonSaveJson.Deserialize<CharacterNarrativeWorldSaveData>(section.payloadJson);
            if (owner?.version != CharacterNarrativeWorldSaveData.CurrentVersion || owner.ambitionTransitionObservations == null)
                throw new InvalidOperationException("ambition-transition-owner-missing-or-version-unsupported");
            if (owner.ambitionTransitionObservations.Count == 0) return;
            if (!DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger))
                throw new InvalidOperationException("ambition-transition-ledger-section-missing");
            foreach (var pending in owner.ambitionTransitionObservations) ValidateObservation(pending, ledger);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException
            or Newtonsoft.Json.JsonException)
        { report.AddError("ambition-transition-observation-save-join:" + error.Message); }
    }

    public static void ValidateObservation(CharacterAmbitionTransitionObservationSaveData pending,
        GameplayOutcomeLedgerSaveData incoming)
    {
        if (pending == null || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("ambition-transition-observation-save-missing");
        pending.Validate();
        var key = CharacterAmbitionTransitionFactMapping.ResultKey(pending.fact);
        if (pending.publicationKey != key.ToString())
            throw new InvalidOperationException("ambition-transition-observation-key-conflict");
        var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(row => row != null
            && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value).ToArray();
        var retired = incoming.tombstones.Where(row => row != null && row.producerId == key.ProducerId
            && row.operationId == key.OperationId.Value).ToArray();
        if (rows.Length + retired.Length > 1) throw new InvalidOperationException("ambition-transition-canonical-duplicate");
        var live = rows.SingleOrDefault();
        var canonical = live == null ? retired.SingleOrDefault() : RetiredCopy(live);
        bool accepted = pending.publicationId.Length > 0;
        if (canonical == null && accepted)
            throw new InvalidOperationException("ambition-transition-accepted-canonical-missing");
        if (canonical != null && (canonical.runId != incoming.runId
            || canonical.sequence < 0 || canonical.sequence >= incoming.nextSequence
            || canonical.commitRevision != key.CommitRevision || canonical.ownerRevision != key.CommitRevision
            || canonical.localResultIndex != key.LocalResultIndex || canonical.absoluteDay != pending.fact.transitionDay
            || accepted && (pending.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(canonical.runId), canonical.sequence).ToString()
                || pending.publicationFingerprint != canonical.canonicalHash)))
            throw new InvalidOperationException("ambition-transition-canonical-proof-conflict");

        var receipt = CharacterAmbitionTransitionFactMapping.Capture(new CharacterAmbitionTransitionFact(pending.fact));
        var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new CharacterAmbitionTransitionOutcomeDescriptor() },
            new IGameplayOutcomeAdapterRegistration[] { new CharacterAmbitionTransitionOutcomeAdapter() });
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
            throw new InvalidOperationException("ambition-transition-canonical-fact-conflict:" + result.DetailCode);
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
