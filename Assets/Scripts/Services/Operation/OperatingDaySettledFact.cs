using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;

[Serializable]
public sealed class OperatingDaySettledFactData
{
    public int reportDay, absoluteDay, totalRevenue, totalVisits;
    public float averageSatisfaction;
    public int paidAmount, carriedDebt, terminatedContractAmount, closingBalance;
    public List<OperatingDayEmploymentSettlementLine> employmentLines = new();
    public OperatingDaySettledFactData Clone()
    {
        var result = (OperatingDaySettledFactData)MemberwiseClone();
        result.employmentLines = employmentLines?.Select(line => line?.Clone()).ToList();
        return result;
    }
    public void Validate(bool proposal = false)
    {
        if (reportDay < 1 || absoluteDay < 0 || totalRevenue < 0 || totalVisits < 0
            || !float.IsFinite(averageSatisfaction) || averageSatisfaction < 0f || averageSatisfaction > 100f
            || paidAmount < 0 || carriedDebt < 0 || terminatedContractAmount < 0 || closingBalance < 0
            || employmentLines == null || employmentLines.Count > 10000)
            throw new ArgumentException("operating-day-fact-invalid");
        foreach (var line in employmentLines)
            if (line == null || line.day < 1 || line.day > reportDay
                || !Key(line.characterId) || !Enum.IsDefined(typeof(OperatingDayEmploymentContractKind), line.contractKind)
                || line.due < 0 || line.paid < 0 || line.paid > line.due || line.unpaidDebtBefore < 0 || line.unpaidDebtAfter < 0
                || line.departureFailureReason == null || line.financeSourceId == null || line.financeTransactionId == null)
                throw new ArgumentException("operating-day-employment-fact-invalid");
        if (proposal && (totalRevenue != 0 || totalVisits != 0 || averageSatisfaction != 0f
            || paidAmount != 0 || carriedDebt != 0 || terminatedContractAmount != 0 || closingBalance != 0 || employmentLines.Count != 0))
            throw new ArgumentException("operating-day-proposal-has-results");
    }
    private static bool Key(string text) => DomainFactValueSyntax.IsText(text, 2048, false) && text == text.Trim();
    public static OperatingDaySettledFactData From(OperatingDayReport report, int absoluteDay) => new()
    {
        reportDay = report?.day ?? throw new ArgumentNullException(nameof(report)), absoluteDay = absoluteDay,
        totalRevenue = report.totalRevenue, totalVisits = report.totalVisits, averageSatisfaction = report.averageSatisfaction,
        paidAmount = report.paidOperatingCost, carriedDebt = report.unpaidOperatingCost,
        terminatedContractAmount = report.terminatedContractCost, closingBalance = report.closingBalance,
        employmentLines = report.employmentSettlements.Select(value => value.Clone()).ToList()
    };
}

public readonly struct OperatingDaySettledFact
{
    private readonly OperatingDaySettledFactData data;
    public OperatingDaySettledFact(OperatingDaySettledFactData source, bool proposal = false)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate(proposal); data = source.Clone(); IsProposal = proposal;
    }
    public bool IsProposal { get; }
    public OperatingDaySettledFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("operating-day-fact-empty");
}
