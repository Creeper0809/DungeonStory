#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerEconomyEntryExtendedE2EDebugScenarios
{
    public static void RunOperatingDayObserversForBatch()
    {
        VerifyOperatingDayReportEntry();
        Debug.Log("B103_OPERATING_DAY_ACTUAL_ENTRY_AND_FAILURES=PASS");
        OperatingDayScenario.VerifySavedObservation();
        EmploymentSettlementG02DebugScenarios.RunOperatingDayEmploymentObserverOrThrow();
        Debug.Log("B103_OPERATING_DAY_ACTUAL_EMPLOYMENT_REJECTED_OBSERVER=PASS");
        Debug.Log("B103_OPERATING_DAY_OBSERVERS_AND_RESTORE=PASS");
    }

    private sealed partial class OperatingDayScenario
    {
        internal static void VerifySavedObservation()
        {
            const MigratedProducerOutcomeKind Kind = MigratedProducerOutcomeKind.OperatingDayReportEvent;
            string checkpoint;
            using (var source = new OperatingDayScenario(Kind))
            {
                source.SeedDetailedLedger();
                source.Facts.HideNextAcknowledgement = true;
                source.Facts.BlockRecovery = true;
                RequireThrows<InvalidOperationException>(() => source.Settle());
                var journal = source.Facts.Journal.Capture();
                var ledger = source.Outcomes.Ledger.CaptureGameplayOutcomes();
                var counters = source.Outcomes.Transaction.Capture();
                var settlement = DungeonSaveJson.Deserialize<DungeonOperatingDaySettlementSaveData>(source.CaptureSavePayload());
                Require(source.Facts.HiddenAcknowledgements == 1 && journal.pending.Count == 1
                    && source.ReportCount == 1 && source.Money == 93 && source.PaymentRecords.Count == 1,
                    "Unknown observation acknowledgement lost the completed settlement.");
                OperatingDayFactPreflight.ValidateJournal(journal, ledger, counters);
                OperatingDayFactPreflight.ValidateOwners(journal, settlement);
                var changed = OperatingDayFactJournal.ValidateAndClone(journal);
                changed.pending[0].fact.paidAmount++;
                RequireThrows<InvalidOperationException>(() => OperatingDayFactPreflight.ValidateJournal(changed, ledger, counters));
                RequireThrows<InvalidOperationException>(() => OperatingDayFactPreflight.ValidateOwners(changed, settlement));
                string strict = new OperatingDayFactSaveSection(source.Facts.Journal).Capture();
                OperatingDayFactSaveSection.ValidateJson(strict);
                var missing = JObject.Parse(strict);
                ((JObject)missing["pending"][0]["fact"]).Remove("absoluteDay");
                RequireThrows<InvalidOperationException>(() => OperatingDayFactSaveSection.ValidateJson(missing.ToString(Formatting.None)));
                string financeBefore = source.CaptureFinancePayload();
                RequireThrows<InvalidOperationException>(() => source.Settle());
                RequireThrows<InvalidOperationException>(() => source.runtime.OnTriggerEvent(new OperatingDayStartedEvent(2)));
                Require(source.CaptureFinancePayload() == financeBefore && source.ReportCount == 1,
                    "Unavailable observation recovery allowed another settlement mutation.");
                checkpoint = source.CaptureCheckpoint();
            }

            var saved = JsonUtility.FromJson<OperatingDayCheckpoint>(checkpoint);
            using (var restored = new OperatingDayScenario(Kind))
            {
                restored.RestoreCheckpoint(checkpoint);
                OperatingDayFactPreflight.ValidateOwners(restored.Facts.Journal.Capture(),
                    DungeonSaveJson.Deserialize<DungeonOperatingDaySettlementSaveData>(restored.CaptureSavePayload()));
                OperatingDayFactPreflight.ValidateJournal(restored.Facts.Journal.Capture(), saved.outcomes, saved.outcomeOwners);
                string beforeFinance = restored.CaptureFinancePayload();
                string beforeReport = restored.CaptureSavePayload();
                restored.Facts.BlockRecovery = true;
                Require(!restored.Facts.Journal.RecoverPending(out _)
                    && restored.Facts.Journal.Capture().pending.Count == 1, "Blocked recovery dropped the saved settlement fact.");
                restored.Facts.BlockRecovery = false;
                restored.Settle();
                restored.Settle();
                Require(restored.Facts.Journal.Capture().pending.Count == 0
                    && restored.CaptureFinancePayload() == beforeFinance && restored.CaptureSavePayload() == beforeReport,
                    "Saved settlement replay repeated a payment/report or lost the fact.");
                var after = restored.Outcomes.Ledger.CaptureGameplayOutcomes();
                string type = MigratedProducerOutcomeCatalog.Get(Kind).OutcomeTypeId.Value;
                var original = saved.outcomes.exactOutcomes.Concat(saved.outcomes.outbox).Single(row => row.outcomeTypeId == type);
                var actual = after.exactOutcomes.Concat(after.outbox).Single(row => row.outcomeTypeId == type);
                Require(after.nextSequence == saved.outcomes.nextSequence && actual.sequence == original.sequence
                    && actual.operationId == original.operationId && actual.immutablePayloadHash == original.immutablePayloadHash,
                    "Saved replay changed or duplicated canonical settlement identity.");
            }
            using (var detached = new OperatingDayScenario(Kind))
            {
                string before = detached.CaptureCheckpoint();
                detached.Facts.Dispose();
                RequireThrows<InvalidOperationException>(() => detached.Settle());
                Require(detached.CaptureCheckpoint() == before, "Detached observer allowed an unobserved settlement.");
            }
            Debug.Log("B103_OPERATING_DAY_ACK_LOSS_STRICT_FRESH_OWNER_REPLAY=PASS");
        }
    }

    public static void VerifyOperatingDayEmploymentSeal(OperatingDayFactJournalData journal,
        GameplayOutcomeLedgerSaveData ledger, DungeonMigratedProducerOutcomeSaveData counters,
        DungeonOperatingDaySettlementSaveData settlement)
    {
        OperatingDayFactPreflight.ValidateJournal(journal, ledger, counters);
        OperatingDayFactPreflight.ValidateOwners(journal, settlement);
        var changed = OperatingDayFactJournal.ValidateAndClone(journal);
        var fact = changed.pending.Single().fact;
        Require(fact.employmentLines.Count > 0, "Employment seal test needs actual employment lines.");
        fact.employmentLines[0].moodPenaltyApplied = !fact.employmentLines[0].moodPenaltyApplied;
        var report = settlement.reportHistory.Single(row => row.day == fact.reportDay);
        report.employmentSettlements[0].moodPenaltyApplied = fact.employmentLines[0].moodPenaltyApplied;
        // Both current owner DTO and saved observation now agree, but the original
        // canonical result must reject the changed field omitted from display text.
        OperatingDayFactPreflight.ValidateOwners(changed, settlement);
        RequireThrows<InvalidOperationException>(() => OperatingDayFactPreflight.ValidateJournal(changed, ledger, counters));
        Debug.Log("B103_OPERATING_DAY_COMPLETE_FACT_SEAL=PASS");
    }
}
#endif
