using System;
using System.Collections.Generic;
using System.Linq;


public static class FactionContractMaterialRules
{
    public static IReadOnlyDictionary<string, int> BuildMaterialRequirements(
        FactionContractDefinitionSO contract) =>
        (contract?.completionRequirements?.items
                ?? new List<V20ItemAmountRequirement>())
            .Where(value => value != null
                && value.consume
                && !string.IsNullOrWhiteSpace(value.itemDefinitionId))
            .GroupBy(
                value => value.itemDefinitionId.Trim(),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => Math.Max(0, value.amount)),
                StringComparer.Ordinal);

    public static bool HasMaterialRequirements(
        FactionContractDefinitionSO contract) =>
        BuildMaterialRequirements(contract).Count > 0;

    public static V20ContentRequirementSet BuildNonMaterialRequirements(
        FactionContractDefinitionSO contract)
    {
        V20ContentRequirementSet source = contract?.completionRequirements
            ?? new V20ContentRequirementSet();
        return new V20ContentRequirementSet
        {
            items = (source.items ?? new List<V20ItemAmountRequirement>())
                .Where(value => value != null && !value.consume)
                .ToList(),
            facilities = (source.facilities ?? new List<V20FacilityRequirement>())
                .Where(value => value != null)
                .ToList(),
            research = (source.research ?? new List<V20ResearchRequirement>())
                .Where(value => value != null)
                .ToList(),
            characters = (source.characters ?? new List<V20CharacterRequirement>())
                .Where(value => value != null)
                .ToList(),
            factions = (source.factions ?? new List<V20FactionRequirement>())
                .Where(value => value != null)
                .ToList(),
            worldMetrics = (source.worldMetrics
                    ?? new List<V20WorldMetricRequirement>())
                .Where(value => value != null)
                .ToList(),
            requiredFlags = (source.requiredFlags ?? new List<string>()).ToList(),
            excludedFlags = (source.excludedFlags ?? new List<string>()).ToList()
        };
    }
}

public static class FactionContractDeliveryOutbox
{
    public const string TransferReason = "faction-contract-delivery";
    private const string OperationPrefix = "faction-contract-transfer:";
    private const string OwnerPrefix = "faction-contract-owner:";
    private const string OccurrenceMarker = ":occurrence:";

    public static string FormatOperationId(
        string contractId,
        string occurrenceId = "")
    {
        if (!EconomyProjectInputOwnerAuthority.IsCanonical(contractId))
            throw new ArgumentException(
                "Faction contract delivery requires a canonical contract ID.",
                nameof(contractId));
        if (string.IsNullOrEmpty(occurrenceId))
            return OperationPrefix + Uri.EscapeDataString(contractId);
        if (!EconomyProjectInputOwnerAuthority.IsCanonical(occurrenceId))
            throw new ArgumentException(
                "Seasonal faction contract delivery requires a canonical occurrence ID.",
                nameof(occurrenceId));
        return OperationPrefix + Uri.EscapeDataString(contractId)
            + OccurrenceMarker + Uri.EscapeDataString(occurrenceId);
    }

    public static string FormatOwnerId(
        string contractId,
        string occurrenceId = "")
    {
        if (!EconomyProjectInputOwnerAuthority.IsCanonical(contractId))
            throw new ArgumentException(
                "Faction contract input owner requires a canonical contract ID.",
                nameof(contractId));
        if (string.IsNullOrEmpty(occurrenceId))
            return contractId;
        if (!EconomyProjectInputOwnerAuthority.IsCanonical(occurrenceId))
            throw new ArgumentException(
                "Seasonal faction contract input owner requires a canonical occurrence ID.",
                nameof(occurrenceId));
        return OwnerPrefix + Uri.EscapeDataString(contractId)
            + OccurrenceMarker + Uri.EscapeDataString(occurrenceId);
    }

    public static string FormatDestinationId(
        string contractId,
        string occurrenceId = "") =>
        EconomyProjectInputOwnerAuthority.BuildFactionContractDestinationId(
            FormatOwnerId(contractId, occurrenceId));

    public static bool TryGetContractIdFromOwnerId(
        string ownerId,
        out string contractId)
    {
        contractId = string.Empty;
        if (!Canonical(ownerId))
            return false;
        if (!ownerId.StartsWith(OwnerPrefix, StringComparison.Ordinal))
        {
            contractId = ownerId;
            return true;
        }

        int marker = ownerId.IndexOf(
            OccurrenceMarker,
            OwnerPrefix.Length,
            StringComparison.Ordinal);
        if (marker <= OwnerPrefix.Length)
            return false;
        try
        {
            contractId = Uri.UnescapeDataString(ownerId.Substring(
                OwnerPrefix.Length,
                marker - OwnerPrefix.Length));
            string occurrenceId = Uri.UnescapeDataString(ownerId.Substring(
                marker + OccurrenceMarker.Length));
            return Canonical(contractId)
                && Canonical(occurrenceId)
                && string.Equals(
                    ownerId,
                    FormatOwnerId(contractId, occurrenceId),
                    StringComparison.Ordinal);
        }
        catch (UriFormatException)
        {
            contractId = string.Empty;
            return false;
        }
    }

    public static bool HasPending(FactionCampaignStateSaveData state) =>
        state != null
        && state.deliveryCommitPhase != FactionContractDeliveryCommitPhase.None;

    public static void RecordPending(
        FactionCampaignStateSaveData state,
        string contractId,
        string occurrenceId,
        FactionContractDeliveryReceipt receipt)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (!ReceiptMatchesIdentity(contractId, occurrenceId, receipt)
            || receipt.SourceStackIds == null
            || receipt.SourceStackIds.Count == 0
            || receipt.SourceStackIds.Any(value => !Canonical(value))
            || receipt.SourceStackIds.Distinct(StringComparer.Ordinal).Count()
                != receipt.SourceStackIds.Count
            || receipt.Quantity <= 0
            || receipt.InputMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Faction-contract delivery receipt is not canonical.");
        }

        state.deliveryContractId = contractId;
        state.deliveryCommitPhase =
            FactionContractDeliveryCommitPhase.PhysicalCommitted;
        state.deliveryOperationId = receipt.OperationId;
        state.deliveryCommitId = receipt.CommitId;
        state.deliverySourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        state.deliveryQuantity = receipt.Quantity;
        state.deliveryMassGrams = receipt.InputMassGrams;
        state.deliveryFailureReason = string.Empty;
    }

    public static void MarkRewardPublished(FactionCampaignStateSaveData state)
    {
        if (!HasCanonicalPending(state)
            || state.deliveryCommitPhase !=
                FactionContractDeliveryCommitPhase.PhysicalCommitted)
        {
            throw new InvalidOperationException(
                "Faction-contract reward requires an exact physical receipt.");
        }
        state.deliveryCommitPhase =
            FactionContractDeliveryCommitPhase.RewardPublished;
    }

    public static void Clear(FactionCampaignStateSaveData state)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        state.deliveryContractId = string.Empty;
        state.deliveryCommitPhase = FactionContractDeliveryCommitPhase.None;
        state.deliveryOperationId = string.Empty;
        state.deliveryCommitId = string.Empty;
        state.deliverySourceStackIds.Clear();
        state.deliveryQuantity = 0;
        state.deliveryMassGrams = 0L;
        state.deliveryFailureReason = string.Empty;
    }

    public static bool HasCanonicalPending(FactionCampaignStateSaveData state) =>
        HasPending(state)
        && state.deliveryCommitPhase is
            FactionContractDeliveryCommitPhase.PhysicalCommitted
            or FactionContractDeliveryCommitPhase.RewardPublished
        && Canonical(state.deliveryContractId)
        && string.Equals(
            state.deliveryOperationId,
            FormatOperationId(
                state.deliveryContractId,
                state.activeContractOccurrenceId),
            StringComparison.Ordinal)
        && Canonical(state.deliveryCommitId)
        && state.deliverySourceStackIds != null
        && state.deliverySourceStackIds.Count > 0
        && state.deliverySourceStackIds.All(Canonical)
        && state.deliverySourceStackIds.SequenceEqual(
            state.deliverySourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal),
            StringComparer.Ordinal)
        && state.deliverySourceStackIds.Distinct(StringComparer.Ordinal).Count()
            == state.deliverySourceStackIds.Count
        && state.deliveryQuantity > 0
        && state.deliveryMassGrams > 0L
        && string.Equals(
            state.deliveryCommitId,
            FormatCommitId(state),
            StringComparison.Ordinal);

    public static bool HasCanonicalEmpty(FactionCampaignStateSaveData state) =>
        state != null
        && state.deliveryCommitPhase == FactionContractDeliveryCommitPhase.None
        && string.IsNullOrEmpty(state.deliveryContractId)
        && string.IsNullOrEmpty(state.deliveryOperationId)
        && string.IsNullOrEmpty(state.deliveryCommitId)
        && (state.deliverySourceStackIds?.Count ?? 0) == 0
        && state.deliveryQuantity == 0
        && state.deliveryMassGrams == 0L;

    public static bool ReceiptMatchesSaved(
        FactionCampaignStateSaveData state,
        PhysicalItemBatchDispositionReceipt receipt) =>
        HasCanonicalPending(state)
        && ReceiptMatchesIdentity(
            state.deliveryContractId,
            state.activeContractOccurrenceId,
            receipt)
        && string.Equals(
            receipt.CommitId,
            state.deliveryCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == state.deliveryQuantity
        && receipt.InputMassGrams == state.deliveryMassGrams
        && (receipt.SourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(
                state.deliverySourceStackIds,
                StringComparer.Ordinal);

    public static bool IsOwnedOperationId(string operationId) =>
        operationId != null
        && operationId.StartsWith(OperationPrefix, StringComparison.Ordinal);

    private static bool ReceiptMatchesIdentity(
        string contractId,
        string occurrenceId,
        FactionContractDeliveryReceipt receipt) =>
        receipt.IsCommitted
        && string.Equals(
            receipt.OperationId,
            FormatOperationId(contractId, occurrenceId),
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            TransferReason,
            StringComparison.Ordinal)
        && receipt.Kind == PhysicalItemDispositionKind.Transfer;

    private static bool ReceiptMatchesIdentity(
        string contractId,
        string occurrenceId,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && string.Equals(
            receipt.OperationId,
            FormatOperationId(contractId, occurrenceId),
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            TransferReason,
            StringComparison.Ordinal)
        && receipt.Kind == PhysicalItemDispositionKind.Transfer;

    private static string FormatCommitId(FactionCampaignStateSaveData state) =>
        $"physical-batch-disposition:1:{state.deliveryOperationId}:"
        + $"{state.deliveryQuantity}:{state.deliveryMassGrams}";

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
