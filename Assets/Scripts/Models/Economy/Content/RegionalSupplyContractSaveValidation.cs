using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class RegionalSupplyContractSaveValidation
{
    private const int MaximumHistory = 24;

    public static void Validate(
        DungeonRegionalSupplyContractSaveData data,
        IResourceEconomyContentCatalog catalog,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (data == null || data.contracts == null)
        {
            report.AddError("Regional-contract payload or contract list is null.");
            return;
        }
        if (catalog == null)
        {
            report.AddError("Regional-contract validation has no item catalog.");
            return;
        }
        if (data.version != DungeonRegionalSupplyContractSaveData.CurrentVersion)
        {
            report.AddError(
                $"Regional-contract payload version {data.version} is unsupported.");
        }
        if (data.currentDay < 1
            || data.nextOfferDay < data.currentDay
            || data.nextSequence < 1)
        {
            report.AddError("Regional-contract scheduling state is invalid.");
        }
        if (data.contracts.Count > MaximumHistory)
        {
            report.AddError("Regional-contract history exceeds its canonical limit.");
        }

        HashSet<string> contractIds = new(StringComparer.Ordinal);
        int previousDay = 0;
        int previousSequence = 0;
        int maximumSequence = 0;
        foreach (RegionalSupplyContractState contract in data.contracts)
        {
            if (contract == null
                || !TryParseContractId(
                    contract.contractId,
                    out int idDay,
                    out int idSequence))
            {
                report.AddError(
                    "Regional-contract payload has a null or non-canonical contract ID.");
                continue;
            }
            if (!contractIds.Add(contract.contractId))
            {
                report.AddError(
                    $"Regional contract '{contract.contractId}' is duplicated.");
            }
            if (contract.offeredDay != idDay
                || contract.offeredDay < 1
                || contract.offeredDay > data.currentDay
                || contract.deadlineDay < contract.offeredDay
                || contract.rewardGold <= 0)
            {
                report.AddError(
                    $"Regional contract '{contract.contractId}' has invalid schedule or reward data.");
            }
            if (idDay < previousDay
                || (idDay == previousDay && idSequence <= previousSequence))
            {
                report.AddError(
                    "Regional contracts are not in canonical offered-day/sequence order.");
            }
            previousDay = idDay;
            previousSequence = idSequence;
            maximumSequence = Math.Max(maximumSequence, idSequence);

            ValidateContract(contract, catalog, report);
        }

        if (data.nextSequence <= maximumSequence)
        {
            report.AddError(
                "Regional-contract next sequence does not follow saved contract IDs.");
        }
    }

    private static void ValidateContract(
        RegionalSupplyContractState contract,
        IResourceEconomyContentCatalog catalog,
        DungeonGameRestoreReport report)
    {
        if (!IsCanonicalRequired(contract.title)
            || !IsCanonicalRequired(contract.regionName)
            || !IsCanonicalOptional(contract.lastStatus)
            || !Enum.IsDefined(
                typeof(RegionalSupplyContractStatus),
                contract.status)
            || !Enum.IsDefined(
                typeof(RegionalSupplyDeliveryCommitPhase),
                contract.deliveryCommitPhase))
        {
            report.AddError(
                $"Regional contract '{contract.contractId}' has invalid text or status.");
        }

        if (contract.deliveryCommitPhase ==
                RegionalSupplyDeliveryCommitPhase.None
            ? !RegionalSupplyContractDeliveryOutbox.HasCanonicalEmpty(contract)
            : !RegionalSupplyContractDeliveryOutbox.HasCanonicalPending(contract))
        {
            report.AddError(
                $"Regional contract '{contract.contractId}' has invalid delivery outbox provenance.");
        }

        string expectedDestination =
            $"regional-contract:{contract.contractId}";
        bool needsDestination = contract.status is
            RegionalSupplyContractStatus.Accepted
            or RegionalSupplyContractStatus.Delivering
            or RegionalSupplyContractStatus.Completed
            or RegionalSupplyContractStatus.Failed;
        string destination = contract.destinationId ?? string.Empty;
        if ((needsDestination
                && !string.Equals(
                    destination,
                    expectedDestination,
                    StringComparison.Ordinal))
            || (!needsDestination && destination.Length != 0))
        {
            report.AddError(
                $"Regional contract '{contract.contractId}' has a non-canonical destination.");
        }

        if (contract.requirements == null
            || contract.requirements.Count is < 1 or > 2)
        {
            report.AddError(
                $"Regional contract '{contract.contractId}' has invalid requirements.");
            return;
        }

        HashSet<string> requirementIds = new(StringComparer.Ordinal);
        foreach (RegionalSupplyContractRequirement requirement in
                 contract.requirements)
        {
            if (requirement == null
                || !IsCanonicalRequired(requirement.itemId)
                || requirement.amount <= 0
                || !requirementIds.Add(requirement.itemId)
                || !catalog.TryGetItem(requirement.itemId, out _))
            {
                report.AddError(
                    $"Regional contract '{contract.contractId}' has an invalid concrete item requirement.");
            }
        }
    }

    private static bool TryParseContractId(
        string value,
        out int day,
        out int sequence)
    {
        day = 0;
        sequence = 0;
        if (!IsCanonicalRequired(value))
        {
            return false;
        }
        string[] segments = value.Split(':');
        return segments.Length == 3
            && string.Equals(segments[0], "contract", StringComparison.Ordinal)
            && int.TryParse(segments[1], out day)
            && int.TryParse(segments[2], out sequence)
            && day > 0
            && sequence > 0
            && string.Equals(
                value,
                $"contract:{day}:{sequence}",
                StringComparison.Ordinal);
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalOptional(string value) =>
        value != null
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
