using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

public sealed class SocialRitualFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(v => v?.sectionId == SocialRitualFactSaveSection.Id);
            if (section?.sectionVersion != SocialRitualFactSaveData.CurrentVersion)
                throw new InvalidOperationException("social-ritual-section-missing");
            SocialRitualFactSaveSection.ValidateJson(section.payloadJson);
            var journal = SocialRitualFactJournal.ValidateAndClone(DungeonSaveJson.Deserialize<SocialRitualFactSaveData>(section.payloadJson));
            if (journal.pending.Count == 0) return;
            if (!DungeonSaveSectionPayload.TryRead(save, FestivalExecutionSaveSection.Id, out FestivalExecutionWorldSaveData festivals)
                || !DungeonSaveSectionPayload.TryRead(save, SocietyEventsSaveSection.Id, out SocietyEventWorldSaveData society)
                || !DungeonSaveSectionPayload.TryRead(save, KinshipHouseholdSaveSection.Id, out KinshipHouseholdWorldSaveData kinship)
                || !DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger)
                || !DungeonSaveSectionPayload.TryRead(save, MigratedProducerOutcomeSaveSection.Id, out DungeonMigratedProducerOutcomeSaveData counters))
                throw new InvalidOperationException("social-ritual-owners-missing");
            ValidateOwners(journal, festivals, society, kinship);
            if (journal.pending.Any(v => v.kind == SocialRitualFactKind.ContentResolution))
            {
                if (!DungeonSaveSectionPayload.TryRead(save, RunMilestonesSaveSection.Id, out RunMilestoneWorldSaveData milestones))
                    throw new InvalidOperationException("content-resolution-owner-missing");
                ValidateContentRecoveries(journal, milestones);
            }
            ValidateJournal(journal, ledger, counters);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("social-ritual-save-join:" + error.Message); }
    }
    public static void ValidateOwners(SocialRitualFactSaveData source, FestivalExecutionWorldSaveData festivals,
        SocietyEventWorldSaveData society, KinshipHouseholdWorldSaveData kinship)
    {
        foreach (var row in SocialRitualFactJournal.ValidateAndClone(source).pending)
        {
            object fact = SocialRitualFactCodec.Decode(row);
            // A completed resolution has no permanent mutable owner row (one-shot effects may
            // change again). Its frozen receipt is the delivery owner, not a life-event receipt.
            if (fact is ContentResolutionFact) continue;
            // Mentorship assignments and the physical ledger may subsequently retire.
            // The journal owns the immutable completed-use receipt, not those mutable rows.
            if (fact is CareerMentorshipAwardFact) continue;
            if (fact is ObservedServiceIncidentFact incident)
            {
                var data = incident.Snapshot();
                var owner = society?.successfulIncidentOperations?.SingleOrDefault(value => value?.operationId == data.operationId);
                if (owner == null || DungeonCharacterWorldSaveCodec.SerializeFields(owner) != incident.CanonicalPayload)
                    throw new InvalidOperationException("social-ritual-service-incident-owner-conflict");
                continue;
            }
            if (fact is FestivalResolutionFact festival)
            {
                var data = festival.Snapshot();
                var owner = festivals?.occurrences?.SingleOrDefault(v => v?.occurrenceId == data.occurrenceId);
                if (owner == null || !owner.effectsApplied || owner.festivalId != data.festivalId || owner.resultGrade != data.grade
                    || owner.phase != (data.stoppedByVenueHazard ? FestivalExecutionPhase.Stopped : FestivalExecutionPhase.Resolved)
                    || owner.venueCapacity != data.venueCapacity || owner.plannedDurationSeconds != data.plannedDurationSeconds
                    || owner.elapsedFestivalSeconds != data.elapsedFestivalSeconds || owner.attendance?.Count != data.assignedParticipantCount
                    || data.participantIds.Any(id => !owner.attendance.Any(a => a.characterId == id)))
                    throw new InvalidOperationException("social-ritual-festival-owner-conflict");
            }
            else
            {
                var capture = Capture(fact);
                var matches = society?.successfulLifeEventOperations?.Where(v => v != null
                    && v.sourceKind == (ObservedLifeEventSourceKind)(int)row.kind
                    && v.sourceOperationId == capture.OwnerIdentity).ToArray();
                if (matches?.Length != 1 || matches[0].canonicalPayload != row.payload)
                    throw new InvalidOperationException("social-ritual-campaign-receipt-conflict");
                if (fact is ObservedLineageCompressionLifeEventReceipt lineage
                    && (kinship?.kinship?.tombstones == null || kinship.kinship.lineageSummaries == null
                        || kinship.kinship.tombstones.Any(v => v?.characterId == lineage.CharacterId.Value)
                        || !kinship.kinship.lineageSummaries.Any(v => v?.householdId == lineage.SummaryHouseholdId
                            && v.generation == lineage.Generation && v.archivedCharacterCount >= lineage.SummaryArchivedCharacterCount
                            && v.earliestBirthDay <= lineage.SummaryEarliestBirthDay && v.latestDeathDay >= lineage.SummaryLatestDeathDay)))
                    throw new InvalidOperationException("social-ritual-lineage-owner-conflict");
            }
        }
    }
    internal static MigratedDomainFactCapture Capture(object fact) => fact switch
    {
        FestivalResolutionFact v => SocialRitualFactMapping.Capture(v),
        ObservedFuneralLifeEventReceipt v => SocialRitualFactMapping.Capture(v),
        ObservedLineageCompressionLifeEventReceipt v => SocialRitualFactMapping.Capture(v),
        ObservedProductionLossLifeEventReceipt v => SocialRitualFactMapping.Capture(v),
        ObservedLastLessonLifeEventReceipt v => SocialRitualFactMapping.Capture(v),
        ObservedQuietPromotionLifeEventReceipt v => SocialRitualFactMapping.Capture(v),
        ContentResolutionFact v => ContentResolutionFactMapping.Capture(v),
        ObservedServiceIncidentFact v => ObservedServiceIncidentFactMapping.Capture(v),
        CareerMentorshipAwardFact v => CareerMentorshipAwardFactMapping.Capture(v),
        _ => throw new InvalidOperationException("social-ritual-kind-unmapped")
    };
    public static void ValidateJournal(SocialRitualFactSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        var journal = SocialRitualFactJournal.ValidateAndClone(source);
        if (incoming?.exactOutcomes == null || incoming.outbox == null || incoming.tombstones == null || counters?.owners == null)
            throw new InvalidOperationException("social-ritual-native-state-missing");
        var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(v =>
            (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(v, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
        var canonical = incoming.outbox.Concat(incoming.exactOutcomes).Select(RetiredCopy).Concat(incoming.tombstones)
            .ToDictionary(v => new GameplayResultKey(v.producerId, new GameplayOperationId(v.operationId), v.commitRevision, v.localResultIndex).ToString(), StringComparer.Ordinal);
        var revisions = counters.owners.ToDictionary(v => v.ownerKey, v => v.revision, StringComparer.Ordinal);
        foreach (var row in journal.pending)
        {
            object fact = SocialRitualFactCodec.Decode(row);
            var capture = Capture(fact);
            string expected = MigratedProducerOutcomeCatalog.ResultKey(capture.Kind, capture.OwnerIdentity, 1).ToString();
            string producer = MigratedProducerOutcomeCatalog.Get(capture.Kind).ProducerId;
            canonical.TryGetValue(expected, out var actual);
            long revision = revisions.TryGetValue(producer + "|" + capture.OwnerIdentity, out var number) ? number : 0;
            if (row.publicationKey != expected || (actual != null ? revision != 1 : revision is not (0 or 1))
                || actual != null && (actual.absoluteDay != capture.AbsoluteDay || actual.ownerRevision != 1 || actual.commitRevision != 1))
                throw new InvalidOperationException("social-ritual-native-identity-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (actual != null) subset.tombstones.Add(actual);
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            migrated.Restore(migrated.PrepareRestore(counters));
            switch (fact)
            {
                case FestivalResolutionFact v: Verify(v, SocialRitualFactMapping.Capture); break;
                case ObservedFuneralLifeEventReceipt v: Verify(v, SocialRitualFactMapping.Capture); break;
                case ObservedLineageCompressionLifeEventReceipt v: Verify(v, SocialRitualFactMapping.Capture); break;
                case ObservedProductionLossLifeEventReceipt v: Verify(v, SocialRitualFactMapping.Capture); break;
                case ObservedLastLessonLifeEventReceipt v: Verify(v, SocialRitualFactMapping.Capture); break;
                case ObservedQuietPromotionLifeEventReceipt v: Verify(v, SocialRitualFactMapping.Capture); break;
                case ContentResolutionFact v: Verify(v, ContentResolutionFactMapping.Capture); break;
                case ObservedServiceIncidentFact v: Verify(v, ObservedServiceIncidentFactMapping.Capture); break;
                case CareerMentorshipAwardFact v: Verify(v, CareerMentorshipAwardFactMapping.Capture); break;
            }
            void Verify<T>(T value, Func<T, MigratedDomainFactCapture> mapping)
            {
                var observer = new MigratedDomainFactObserver<T>(mapping, migrated, commits);
                bool accepted = observer.TryPrepareReplay(row.publicationKey, value, out var prepared, out var failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("social-ritual-canonical-conflict:" + failure);
            }
        }
    }
    public static void ValidateContentRecoveries(SocialRitualFactSaveData source, RunMilestoneWorldSaveData milestones)
    {
        if (milestones?.pendingContentResolutions == null)
            throw new InvalidOperationException("content-resolution-owner-invalid");
        foreach (var row in SocialRitualFactJournal.ValidateAndClone(source).pending)
        {
            if (SocialRitualFactCodec.Decode(row) is not ContentResolutionFact fact) continue;
            var data = fact.Snapshot();
            var recovery = milestones.pendingContentResolutions.SingleOrDefault(v => v?.actionId == data.operationId);
            // Normally removed once delivery ownership passes to the journal. If retained,
            // both owners must describe the same already-applied effect, never an earlier phase.
            if (recovery != null && (recovery.phase < V20ContentResolutionRecoveryPhase.TypedEffectsApplied
                || recovery.absoluteDay != data.absoluteDay || recovery.resolutions == null
                || data.resolutionIndex >= recovery.resolutions.Count
                || DungeonCharacterWorldSaveCodec.SerializeFields(recovery.resolutions[data.resolutionIndex])
                    != DungeonCharacterWorldSaveCodec.SerializeFields(data.resolution)
                || recovery.moneyTransactionId != data.moneyTransactionId
                || recovery.itemConsumptionOperationId != data.itemConsumptionOperationId
                || recovery.itemGrantBatchCommitId != data.itemGrantBatchCommitId
                || recovery.stateBeforeFingerprint != data.stateBeforeFingerprint
                || recovery.stateAfterFingerprint != data.stateAfterFingerprint))
                throw new InvalidOperationException("content-resolution-recovery-conflict");
        }
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
