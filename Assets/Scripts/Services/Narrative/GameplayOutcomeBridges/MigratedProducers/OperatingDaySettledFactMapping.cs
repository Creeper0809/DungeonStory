using System;
using System.Linq;
using System.Globalization;
using System.Text;

public static class OperatingDaySettledFactMapping
{
    public static MigratedDomainFactCapture Capture(
        OperatingDaySettledFact fact)
    {
        OperatingDaySettledFactData data = fact.Snapshot();
        data.Validate(fact.IsProposal);
        string ownerIdentity =
            OperatingDayReport.CreatePersistentId(data.reportDay);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.OperatingDayReportEvent,
            ownerIdentity,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            key => CreateDetails(data, key),
            reservationOnly: fact.IsProposal);
    }

    private static MigratedProducerOutcomeDetails CreateDetails(
        OperatingDaySettledFactData data,
        GameplayResultKey key)
    {
        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.OperationKind,
            key.OperationId.Value,
            "영업일 " + data.reportDay,
            MigratedProducerOutcomeIds.OperationRole);
        return new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            CreateSummary(data),
            provenance: new[] { new GameplayOutcomeProvenanceReference(
                "operating-day-fact-sha256", ComputeContextSha256(data)) });
    }

    public static string ComputeContextSha256(OperatingDaySettledFactData data)
    {
        data.Validate();
        var text = new StringBuilder();
        Append("operating-day-settled-v1");
        Append(data.reportDay); Append(data.absoluteDay); Append(data.totalRevenue); Append(data.totalVisits);
        Append(data.averageSatisfaction.ToString("R", CultureInfo.InvariantCulture));
        Append(data.paidAmount); Append(data.carriedDebt); Append(data.terminatedContractAmount); Append(data.closingBalance);
        Append(data.employmentLines.Count);
        foreach (var line in data.employmentLines)
        {
            Append(line.day); Append(line.characterId); Append((int)line.contractKind);
            Append(line.due); Append(line.paid); Append(line.unpaidDebtBefore); Append(line.unpaidDebtAfter);
            Append(line.moodPenaltyApplied); Append(line.contractEnded); Append(line.departureAttempted);
            Append(line.departureStarted); Append(line.departurePending); Append(line.departureFailureReason);
            Append(line.financeSourceId); Append(line.financeTransactionId); Append(line.financeSucceeded);
        }
        return NarrativeInferenceHash.ComputeSha256Utf8(text.ToString());

        void Append(object value)
        {
            string field = Convert.ToString(value, CultureInfo.InvariantCulture);
            text.Append(Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(field).Append('|');
        }
    }

    private static string CreateSummary(OperatingDaySettledFactData data) =>
        "영업일 정산 확정: report-id="
        + OperatingDayReport.CreatePersistentId(data.reportDay)
        + "; day=" + data.reportDay
        + "; revenue=" + data.totalRevenue
        + "; visits=" + data.totalVisits
        + "; average-satisfaction=" + data.averageSatisfaction
        + "; paid=" + data.paidAmount
        + "; debt=" + data.carriedDebt
        + "; terminated-contract-cost=" + data.terminatedContractAmount
        + "; finance-children=" + FinanceChildIds(data)
        + "; employment-lines=" + EmploymentLineSummary(data)
        + "; closing-balance=" + data.closingBalance;

    private static string FinanceChildIds(OperatingDaySettledFactData data)
    {
        string[] ids = data.employmentLines
            .Where(value => !string.IsNullOrWhiteSpace(
                value.financeTransactionId))
            .Select(value => value.financeTransactionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return ids.Length == 0 ? "none" : string.Join(",", ids);
    }

    private static string EmploymentLineSummary(
        OperatingDaySettledFactData data)
    {
        string[] lines = data.employmentLines
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .Select(value => value.characterId
                + "[due=" + value.due
                + ",paid=" + value.paid
                + ",debt=" + value.unpaidDebtAfter
                + ",ended=" + value.contractEnded
                + ",departure=" + (value.departureStarted
                    ? "started"
                    : value.departurePending ? "pending" : "none")
                + "]")
            .ToArray();
        return lines.Length == 0 ? "none" : string.Join(",", lines);
    }
}
