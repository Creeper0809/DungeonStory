using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using Newtonsoft.Json.Linq;
using DungeonStory.Narrative.Korean;

public sealed class OperatingDayFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(row => row?.sectionId == OperatingDayFactSaveSection.Id);
            if (section == null || section.sectionVersion != OperatingDayFactJournalData.CurrentVersion)
                throw new InvalidOperationException("operating-day-observations-section-missing");
            OperatingDayFactSaveSection.ValidateJson(section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, OperatingDaySettlementSaveSection.Id, out DungeonOperatingDaySettlementSaveData settlement)
                || !DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger)
                || !DungeonSaveSectionPayload.TryRead(save, MigratedProducerOutcomeSaveSection.Id, out DungeonMigratedProducerOutcomeSaveData counters))
                throw new InvalidOperationException("operating-day-observations-owner-section-missing");
            var journal = DungeonSaveJson.Deserialize<OperatingDayFactJournalData>(section.payloadJson);
            ValidateOwners(journal, settlement);
            ValidateJournal(journal, ledger, counters);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("operating-day-observations-save-join:" + error.Message); }
    }

    public static void ValidateOwners(OperatingDayFactJournalData source, DungeonOperatingDaySettlementSaveData settlement)
    {
        var journal = OperatingDayFactJournal.ValidateAndClone(source);
        if (settlement?.reportHistory == null)
            throw new InvalidOperationException("operating-day-observations-owner-invalid");
        foreach (var row in journal.pending)
        {
            var fact = row.fact;
            var report = settlement.reportHistory.SingleOrDefault(value => value?.day == fact.reportDay);
            if (report == null || report.totalRevenue != fact.totalRevenue || report.totalVisits != fact.totalVisits
                || report.averageSatisfaction != fact.averageSatisfaction || report.paidOperatingCost != fact.paidAmount
                || report.unpaidOperatingCost != fact.carriedDebt || report.terminatedContractCost != fact.terminatedContractAmount
                || report.closingBalance != fact.closingBalance || report.employmentSettlements == null
                || report.employmentSettlements.Count != fact.employmentLines.Count)
                throw new InvalidOperationException("operating-day-observations-report-conflict");
            for (int i = 0; i < fact.employmentLines.Count; i++)
                if (!JToken.DeepEquals(
                    JObject.Parse(DungeonCharacterWorldSaveCodec.SerializeFields(report.employmentSettlements[i])),
                    JObject.Parse(DungeonCharacterWorldSaveCodec.SerializeFields(fact.employmentLines[i]))))
                    throw new InvalidOperationException("operating-day-observations-employment-conflict");
        }
    }

    public static void ValidateJournal(OperatingDayFactJournalData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        var journal = OperatingDayFactJournal.ValidateAndClone(source);
        if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
            throw new InvalidOperationException("operating-day-observations-native-state-missing");
        if (journal.pending.Count == 0) return;
        var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.OperatingDayReportEvent).ProducerId;
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes)) Add(RetiredCopy(row));
        foreach (var row in incoming.tombstones) Add(row);
        var savedCounters = counters.owners.ToDictionary(row => row.ownerKey, row => row.revision, StringComparer.Ordinal);
        var registry = new GameplayOutcomeRegistry(
            MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        var observer = new MigratedDomainFactObserver<OperatingDaySettledFact>(
            OperatingDaySettledFactMapping.Capture, migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
        foreach (var row in journal.pending)
        {
            string expected = MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.OperatingDayReportEvent, OperatingDaySettledFactMapping.Capture(new OperatingDaySettledFact(row.fact)).OwnerIdentity, 1).ToString();
            if (row.publicationKey != expected)
                throw new InvalidOperationException("operating-day-observations-key-conflict");
            canonical.TryGetValue(expected, out var actual);
            long revision = savedCounters.TryGetValue(producer + "|" + OperatingDaySettledFactMapping.Capture(new OperatingDaySettledFact(row.fact)).OwnerIdentity, out long value) ? value : 0;
            if (actual != null && revision != 1
                || actual == null && revision != 0)
                throw new InvalidOperationException("operating-day-observations-result-counter-conflict");
            if (actual != null && (actual.absoluteDay != row.fact.absoluteDay
                || actual.ownerRevision != 1 || actual.commitRevision != 1))
                throw new InvalidOperationException("operating-day-observations-proof-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (actual != null) subset.tombstones.Add(actual);
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            migrated.Restore(migrated.PrepareRestore(counters));
            bool accepted = observer.TryPrepareReplay(row.publicationKey,
                new OperatingDaySettledFact(row.fact), out var prepared, out string failure);
            prepared?.Cancel();
            if (!accepted) throw new InvalidOperationException("operating-day-observations-canonical-fact-conflict:" + failure);
        }
        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null) throw new InvalidOperationException("operating-day-observations-canonical-null");
            if (row.producerId != producer) return;
            var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex);
            if (row.runId != incoming.runId || row.sequence <= 0 || row.sequence >= incoming.nextSequence
                || !canonical.TryAdd(key.ToString(), row))
                throw new InvalidOperationException("operating-day-observations-canonical-identity-conflict");
        }
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row)
    {
        if (row == null) throw new InvalidOperationException("operating-day-observations-canonical-null");
        return new GameplayOutcomeTombstoneSnapshot
        {
            runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
            commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
            temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
            parentRunId = row.parentRunId, parentSequence = row.parentSequence, deliveryMetadataVersion = row.deliveryMetadataVersion,
            deliveryClockId = row.deliveryClockId, recordedAbsoluteHour = row.recordedAbsoluteHour,
            publishedRevision = row.publishedRevision, deliveryFaultCount = row.deliveryFaultCount,
            deliveryDelayReason = row.deliveryDelayReason, canonicalHash = row.immutablePayloadHash,
            terminalTier = NarrativeMemoryTier.Forgotten
        };
    }
}
