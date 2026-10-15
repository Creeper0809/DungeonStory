using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RegionalSupplyContractOutcomeBridge :
    IRegionalSupplyContractOutcomePort
{
    private static readonly GameplayOutcomeFactId ContractFact =
        new("regional-contract-id");
    private static readonly GameplayOutcomeFactId RegionFact =
        new("regional-contract-region");
    private static readonly GameplayOutcomeFactId RequirementFact =
        new("regional-contract-requirements");
    private static readonly GameplayOutcomeFactId DestinationFact =
        new("regional-contract-destination-id");
    private static readonly GameplayOutcomeFactId PreviousStatusFact =
        new("regional-contract-status-before");
    private static readonly GameplayOutcomeFactId CurrentStatusFact =
        new("regional-contract-status-after");
    private static readonly GameplayOutcomeFactId PhysicalOperationFact =
        new("regional-contract-physical-operation-id");
    private static readonly GameplayOutcomeFactId PhysicalCommitFact =
        new("regional-contract-physical-commit-id");
    private static readonly GameplayOutcomeFactId RewardTransactionFact =
        new("regional-contract-reward-transaction-id");

    private sealed class Entry
    {
        internal PreparedMigratedProducerOutcome Prepared;
        internal RegionalSupplyContractStatus Before;
        internal RegionalSupplyContractStatus After;
        internal string Action = string.Empty;
        internal bool Completion;
    }

    private readonly IMigratedProducerOutcomeTransaction transaction;
    private readonly Dictionary<string, Entry> entries =
        new(StringComparer.Ordinal);

    public RegionalSupplyContractOutcomeBridge(
        IMigratedProducerOutcomeTransaction transaction)
    {
        this.transaction = transaction
            ?? throw new ArgumentNullException(nameof(transaction));
    }

    public bool TryReserveStatus(
        RegionalSupplyContractState contract,
        RegionalSupplyContractStatus before,
        RegionalSupplyContractStatus after,
        string action,
        bool succeeded,
        int absoluteDay,
        out RegionalSupplyContractOutcomeReservation reservation,
        out string failureReason)
    {
        reservation = default;
        string canonicalAction = action?.Trim() ?? string.Empty;
        if (!IsCanonicalContract(contract) || canonicalAction.Length == 0)
        {
            failureReason = "regional-supply-status-request-invalid";
            return false;
        }
        string identity = FormatIdentity(contract.contractId, canonicalAction);
        if (!transaction.TryReserve(
                MigratedProducerOutcomeKind.RegionalSupplyContractResult,
                identity,
                Math.Max(0, absoluteDay),
                succeeded
                    ? GameplayOutcomeStatus.Succeeded
                    : GameplayOutcomeStatus.Failed,
                participantCount: 2,
                metricCount: 4,
                subjectCount: 2,
                additionalFactCount: 8,
                out PreparedMigratedProducerOutcome prepared,
                out failureReason))
            return false;
        string token = prepared.ResultKey.ToString();
        if (token.Length == 0 || entries.ContainsKey(token))
        {
            transaction.Cancel(prepared);
            failureReason = "regional-supply-status-reservation-duplicate";
            return false;
        }
        entries.Add(token, new Entry
        {
            Prepared = prepared,
            Before = before,
            After = after,
            Action = canonicalAction
        });
        reservation = new RegionalSupplyContractOutcomeReservation(token);
        return true;
    }

    public bool TryCommitStatus(
        in RegionalSupplyContractOutcomeReservation reservation,
        RegionalSupplyContractState contract,
        string reason,
        out string resultKey,
        out string failureReason)
    {
        resultKey = string.Empty;
        if (!Take(reservation, completion: false, out Entry entry))
        {
            failureReason = "regional-supply-status-reservation-missing";
            return false;
        }
        MigratedProducerOutcomeDetails details = BuildStatusDetails(
            contract,
            entry.Before,
            entry.After,
            entry.Action,
            reason);
        MigratedProducerOutcomeCommitResult committed =
            transaction.CommitDetailed(entry.Prepared, details);
        if (!committed.DurablyCommitted)
        {
            failureReason = committed.DetailCode;
            return false;
        }
        resultKey = committed.ResultKey.ToString();
        failureReason = string.Empty;
        return resultKey.Length > 0;
    }

    public bool TryReserveCompletion(
        RegionalSupplyContractState contract,
        int absoluteDay,
        out RegionalSupplyContractOutcomeReservation reservation,
        out string failureReason)
    {
        reservation = default;
        if (!IsCanonicalContract(contract))
        {
            failureReason = "regional-supply-completion-request-invalid";
            return false;
        }
        if (!transaction.TryReserve(
                MigratedProducerOutcomeKind.RegionalSupplyContractResult,
                FormatIdentity(contract.contractId, "completed"),
                Math.Max(0, absoluteDay),
                GameplayOutcomeStatus.Succeeded,
                participantCount: 2,
                metricCount: 6,
                subjectCount: 2,
                additionalFactCount: 10,
                out PreparedMigratedProducerOutcome prepared,
                out failureReason))
            return false;
        string token = prepared.ResultKey.ToString();
        if (token.Length == 0 || entries.ContainsKey(token))
        {
            transaction.Cancel(prepared);
            failureReason = "regional-supply-completion-reservation-duplicate";
            return false;
        }
        entries.Add(token, new Entry
        {
            Prepared = prepared,
            Before = contract.status,
            After = RegionalSupplyContractStatus.Completed,
            Action = "completed",
            Completion = true
        });
        reservation = new RegionalSupplyContractOutcomeReservation(token);
        return true;
    }

    public bool TryCommitCompletion(
        in RegionalSupplyContractOutcomeReservation reservation,
        RegionalSupplyContractState contract,
        out string resultKey,
        out string failureReason)
    {
        resultKey = string.Empty;
        if (!Take(reservation, completion: true, out Entry entry))
        {
            failureReason = "regional-supply-completion-reservation-missing";
            return false;
        }
        MigratedProducerOutcomeDetails details = BuildCompletionDetails(
            contract,
            entry.Before);
        MigratedProducerOutcomeCommitResult committed =
            transaction.CommitDetailed(entry.Prepared, details);
        if (!committed.DurablyCommitted)
        {
            failureReason = committed.DetailCode;
            return false;
        }
        resultKey = committed.ResultKey.ToString();
        failureReason = string.Empty;
        return resultKey.Length > 0;
    }

    public void Cancel(
        in RegionalSupplyContractOutcomeReservation reservation)
    {
        if (!reservation.IsValid
            || !entries.Remove(reservation.Token, out Entry entry))
            return;
        transaction.Cancel(entry.Prepared);
    }

    private bool Take(
        in RegionalSupplyContractOutcomeReservation reservation,
        bool completion,
        out Entry entry)
    {
        entry = null;
        if (!reservation.IsValid
            || !entries.TryGetValue(reservation.Token, out Entry found)
            || found.Completion != completion)
            return false;
        entries.Remove(reservation.Token);
        entry = found;
        return true;
    }

    private static MigratedProducerOutcomeDetails BuildStatusDetails(
        RegionalSupplyContractState contract,
        RegionalSupplyContractStatus before,
        RegionalSupplyContractStatus after,
        string action,
        string reason)
    {
        CreateSubjects(
            contract,
            out MigratedProducerOutcomeSubject contractSubject,
            out MigratedProducerOutcomeSubject regionSubject);
        GameplayEntityId contractId = contractSubject.EntityId;
        return new MigratedProducerOutcomeDetails(
            new[] { contractSubject, regionSubject },
            new[]
            {
                Metric(MigratedProducerOutcomeIds.AmountMetric,
                    contract.rewardGold,
                    MigratedProducerOutcomeIds.CurrencyUnit, contractId),
                Metric(MigratedProducerOutcomeIds.QuantityMetric,
                    RequiredQuantity(contract),
                    MigratedProducerOutcomeIds.CountUnit, contractId),
                Metric(MigratedProducerOutcomeIds.PreviousMetric,
                    contract.offeredDay,
                    MigratedProducerOutcomeIds.DayUnit, contractId),
                Metric(MigratedProducerOutcomeIds.CurrentMetric,
                    contract.deadlineDay,
                    MigratedProducerOutcomeIds.DayUnit, contractId)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.OperationFact,
                    FormatIdentity(contract.contractId, action)),
                Fact(ContractFact, contract.contractId),
                Fact(RegionFact, contract.regionName),
                Fact(RequirementFact, FormatRequirements(contract)),
                Fact(DestinationFact,
                    string.IsNullOrWhiteSpace(contract.destinationId)
                        ? "none"
                        : contract.destinationId),
                Fact(PreviousStatusFact, before.ToString()),
                Fact(CurrentStatusFact, after.ToString()),
                Fact(MigratedProducerOutcomeIds.ReasonFact,
                    string.IsNullOrWhiteSpace(reason) ? "none" : reason)
            },
            "지역 공급 계약 상태 확정: " + contract.title
            + "; " + before + "->" + after);
    }

    private static MigratedProducerOutcomeDetails BuildCompletionDetails(
        RegionalSupplyContractState contract,
        RegionalSupplyContractStatus before)
    {
        CreateSubjects(
            contract,
            out MigratedProducerOutcomeSubject contractSubject,
            out MigratedProducerOutcomeSubject regionSubject);
        GameplayEntityId contractId = contractSubject.EntityId;
        return new MigratedProducerOutcomeDetails(
            new[] { contractSubject, regionSubject },
            new[]
            {
                Metric(MigratedProducerOutcomeIds.AmountMetric,
                    contract.rewardGold,
                    MigratedProducerOutcomeIds.CurrencyUnit, contractId),
                Metric(MigratedProducerOutcomeIds.QuantityMetric,
                    contract.deliveryQuantity,
                    MigratedProducerOutcomeIds.CountUnit, contractId),
                Metric(MigratedProducerOutcomeIds.MassMetric,
                    contract.deliveryMassGrams,
                    MigratedProducerOutcomeIds.GramUnit, contractId),
                Metric(MigratedProducerOutcomeIds.DayMetric,
                    contract.deadlineDay,
                    MigratedProducerOutcomeIds.DayUnit, contractId),
                Metric(MigratedProducerOutcomeIds.PreviousMetric,
                    contract.rewardBalanceBefore,
                    MigratedProducerOutcomeIds.CurrencyUnit, contractId),
                Metric(MigratedProducerOutcomeIds.CurrentMetric,
                    contract.rewardBalanceAfter,
                    MigratedProducerOutcomeIds.CurrencyUnit, contractId)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.OperationFact,
                    FormatIdentity(contract.contractId, "completed")),
                Fact(ContractFact, contract.contractId),
                Fact(RegionFact, contract.regionName),
                Fact(RequirementFact, FormatRequirements(contract)),
                Fact(DestinationFact, contract.destinationId),
                Fact(PreviousStatusFact, before.ToString()),
                Fact(CurrentStatusFact,
                    RegionalSupplyContractStatus.Completed.ToString()),
                Fact(PhysicalOperationFact, contract.deliveryOperationId),
                Fact(PhysicalCommitFact, contract.deliveryCommitId),
                Fact(RewardTransactionFact, contract.rewardTransactionId)
            },
            "지역 공급 계약 완료: " + contract.title
            + "; reward=" + contract.rewardGold
            + "; balance=" + contract.rewardBalanceBefore
            + "->" + contract.rewardBalanceAfter);
    }

    private static void CreateSubjects(
        RegionalSupplyContractState contract,
        out MigratedProducerOutcomeSubject contractSubject,
        out MigratedProducerOutcomeSubject regionSubject)
    {
        contractSubject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.OperationKind,
            "regional-contract:" + contract.contractId,
            contract.title,
            MigratedProducerOutcomeIds.OperationRole);
        regionSubject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.FactionKind,
            "regional-market:" + contract.contractId,
            contract.regionName,
            MigratedProducerOutcomeIds.FactionRole);
    }

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId metric,
        long value,
        GameplayMetricUnitId unit,
        GameplayEntityId subject) =>
        new(metric, value, unit, subject);

    private static GameplayOutcomeFact Fact(
        GameplayOutcomeFactId fact,
        string value) => new(fact, value);

    private static int RequiredQuantity(
        RegionalSupplyContractState contract) =>
        (contract.requirements ?? new List<RegionalSupplyContractRequirement>())
            .Where(value => value != null)
            .Sum(value => value.amount);

    private static string FormatRequirements(
        RegionalSupplyContractState contract) =>
        string.Join(",", (contract.requirements
                ?? new List<RegionalSupplyContractRequirement>())
            .Where(value => value != null)
            .OrderBy(value => value.itemId, StringComparer.Ordinal)
            .Select(value => value.itemId + "=" + value.amount));

    private static string FormatIdentity(string contractId, string action) =>
        "regional-supply-contract:" + contractId + ":" + action;

    private static bool IsCanonicalContract(
        RegionalSupplyContractState contract) =>
        contract != null
        && !string.IsNullOrWhiteSpace(contract.contractId)
        && !string.IsNullOrWhiteSpace(contract.title)
        && !string.IsNullOrWhiteSpace(contract.regionName)
        && contract.offeredDay > 0
        && contract.deadlineDay >= contract.offeredDay
        && contract.rewardGold > 0
        && RequiredQuantity(contract) > 0
        && FormatRequirements(contract).Length > 0;
}
