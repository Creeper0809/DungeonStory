#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Operation;

public static class OperatingDayFacilityDisplayIdentityDebugScenarios
{
    private const string FacilityId = "facility:display-identity";
    private const string OriginalName = "Original Facility";
    private const string EvolvedName = "Evolved Facility";
    private const int StockCategory = 7;

    public static void VerifyForBatch()
    {
        var report = new TestReport("prior-report");
        OperatingDaySettlementDomain<TestReport, string> domain =
            OperatingDaySettlementDomain<TestReport, string>.Attach(
                new DungeonRuntimeAggregateRootStore());
        domain.RestoreOwnerState(domain.PrepareRestoreState(
            CreateInitialSnapshot(report)));

        domain.RecordVisit(FacilityId, OriginalName, "slime", 42f);
        domain.RecordRevenue(FacilityId, OriginalName, 120);
        domain.RecordStockConsumed(FacilityId, OriginalName, StockCategory, 3);
        domain.RecordFacilityCost(FacilityId, OriginalName, 25);

        OperatingDaySettlementStateSnapshot<TestReport, string> beforeRebind =
            domain.Capture();
        RequireLedger(
            beforeRebind,
            OriginalName,
            expectedVisits: 1,
            expectedReport: report);

        ExpectInvalidOperation(
            () => domain.RecordVisit(FacilityId, EvolvedName, "slime", 42f),
            "A visit must not authorize an arbitrary display rename.");
        RequireLedger(
            domain.Capture(),
            OriginalName,
            expectedVisits: 1,
            expectedReport: report);

        domain.RebindFacilityDisplayName(FacilityId, OriginalName, EvolvedName);
        OperatingDaySettlementStateSnapshot<TestReport, string> rebound =
            domain.Capture();
        RequireLedger(
            rebound,
            EvolvedName,
            expectedVisits: 1,
            expectedReport: report);
        Require(
            beforeRebind.Ledger.FacilityDetails[0].FacilityName == OriginalName,
            "Rebinding must not mutate a previously captured snapshot.");

        domain.RebindFacilityDisplayName(FacilityId, OriginalName, EvolvedName);
        RequireLedger(
            domain.Capture(),
            EvolvedName,
            expectedVisits: 1,
            expectedReport: report);

        ExpectInvalidOperation(
            () => domain.RebindFacilityDisplayName(
                FacilityId,
                OriginalName,
                "Unrelated Facility"),
            "A stale source name must not authorize a display transition.");
        RequireLedger(
            domain.Capture(),
            EvolvedName,
            expectedVisits: 1,
            expectedReport: report);

        domain.RebindFacilityDisplayName(
            "facility:not-recorded-today",
            OriginalName,
            EvolvedName);
        RequireLedger(
            domain.Capture(),
            EvolvedName,
            expectedVisits: 1,
            expectedReport: report);

        OperatingDaySettlementAggregateState<TestReport, string> prepared =
            domain.PrepareRestoreState(domain.Capture());
        domain.RestoreOwnerState(prepared);
        OperatingDaySettlementAggregateState<TestReport, string> ownerState =
            domain.CaptureOwnerState();
        OperatingDaySettlementDomain<TestReport, string> restored =
            OperatingDaySettlementDomain<TestReport, string>.Attach(
                new DungeonRuntimeAggregateRootStore());
        restored.RestoreOwnerState(ownerState);
        RequireLedger(
            restored.Capture(),
            EvolvedName,
            expectedVisits: 1,
            expectedReport: report);

        restored.RecordVisit(FacilityId, EvolvedName, "slime", 43f);
        RequireLedger(
            restored.Capture(),
            EvolvedName,
            expectedVisits: 2,
            expectedReport: report);
    }

    private static OperatingDaySettlementStateSnapshot<TestReport, string>
        CreateInitialSnapshot(TestReport report)
    {
        return new OperatingDaySettlementStateSnapshot<TestReport, string>(
            currentDay: 9,
            facilityDetails: Array.Empty<OperatingDayFacilityLedgerEntry>(),
            speciesVisits: new Dictionary<string, int>(),
            consumedStock: new Dictionary<int, int>(),
            visitorMoodSamples: Array.Empty<float>(),
            stockSupplyResults: Array.Empty<string>(),
            incidents: Array.Empty<string>(),
            eventLog: Array.Empty<string>(),
            reportHistory: new[] { report },
            totalRevenue: 0,
            totalVisits: 0,
            restockFailureCount: 0,
            outstandingDebt: 0,
            consecutiveShortfallDays: 0,
            emergencyFundingUsed: false,
            lastSettledDay: 8);
    }

    private static void RequireLedger(
        OperatingDaySettlementStateSnapshot<TestReport, string> snapshot,
        string expectedName,
        int expectedVisits,
        TestReport expectedReport)
    {
        Require(snapshot.CurrentDay == 9, "The operating day changed unexpectedly.");
        Require(snapshot.Ledger.FacilityDetails.Count == 1,
            "The stable facility identity must keep one ledger row.");
        OperatingDayFacilityLedgerEntry detail =
            snapshot.Ledger.FacilityDetails[0];
        Require(detail.FacilityId == FacilityId,
            "The facility persistent identity changed.");
        Require(detail.FacilityName == expectedName,
            "The facility display identity was not the expected value.");
        Require(detail.Revenue == 120 && detail.VisitCount == expectedVisits,
            "Revenue or visit counts changed during the display transition.");
        Require(detail.OperatingCost == 25,
            "Operating cost changed during the display transition.");
        Require(detail.ConsumedStock.Count == 1
                && detail.ConsumedStock.TryGetValue(StockCategory, out int amount)
                && amount == 3,
            "Consumed stock changed during the display transition.");
        Require(detail.VisitorMoodSamples.Count == expectedVisits,
            "Visitor mood samples did not follow accepted visits only.");
        Require(snapshot.Ledger.TotalRevenue == 120
                && snapshot.Ledger.TotalVisits == expectedVisits
                && snapshot.Ledger.ConsumedStock.TryGetValue(
                    StockCategory,
                    out int totalConsumed)
                && totalConsumed == 3,
            "Aggregate ledger totals changed unexpectedly.");
        Require(snapshot.ReportHistory.Count == 1
                && ReferenceEquals(snapshot.ReportHistory[0], expectedReport),
            "Display rebinding must preserve settled report history.");
    }

    private static void ExpectInvalidOperation(Action action, string message)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class TestReport
    {
        public TestReport(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}
#endif
