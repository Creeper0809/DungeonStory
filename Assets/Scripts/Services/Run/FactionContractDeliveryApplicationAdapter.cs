using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

public enum FactionContractDeliveryCommitPhase
{
    None,
    PhysicalCommitted,
    RewardPublished
}

public readonly struct FactionContractDeliveryTarget
{
    public FactionContractDeliveryTarget(
        string destinationId,
        Vector2Int position)
    {
        DestinationId = destinationId?.Trim() ?? string.Empty;
        Position = position;
    }

    public string DestinationId { get; }
    public Vector2Int Position { get; }
    public bool IsValid => EconomyProjectInputOwnerAuthority.IsCanonical(
        DestinationId);
}

public readonly struct FactionContractDeliveryReceipt
{
    public FactionContractDeliveryReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        string requestFingerprint,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams)
    {
        Kind = kind;
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        SourceStackIds = (sourceStackIds ?? Array.Empty<string>()).ToArray();
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
        CommitId = $"physical-batch-disposition:{(int)kind}:{OperationId}:"
            + $"{quantity}:{inputMassGrams}";
    }

    public static FactionContractDeliveryReceipt FromPhysical(
        PhysicalItemBatchDispositionReceipt receipt) =>
        new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            receipt.RequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams);

    public PhysicalItemDispositionKind Kind { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public string CommitId { get; }
    public bool IsCommitted => Kind == PhysicalItemDispositionKind.Transfer
        && (OperationId?.Length ?? 0) > 0
        && (ReasonCode?.Length ?? 0) > 0
        && (SourceStackIds?.Count ?? 0) > 0
        && Quantity > 0
        && InputMassGrams > 0L;
}

public sealed class FactionContractItemProgressView
{
    public FactionContractItemProgressView(
        string itemId,
        string displayName,
        int required,
        int assigned,
        int hauling,
        int arrived,
        int delivered)
    {
        ItemId = itemId ?? string.Empty;
        DisplayName = displayName ?? ItemId;
        Required = Mathf.Max(0, required);
        Assigned = Mathf.Max(0, assigned);
        Hauling = Mathf.Max(0, hauling);
        Arrived = Mathf.Max(0, arrived);
        Delivered = Mathf.Max(0, delivered);
    }

    public string ItemId { get; }
    public string DisplayName { get; }
    public int Required { get; }
    public int Assigned { get; }
    public int Hauling { get; }
    public int Arrived { get; }
    public int Delivered { get; }
}

public sealed class FactionContractView
{
    public string ContractId { get; internal set; } = string.Empty;
    public string OccurrenceId { get; internal set; } = string.Empty;
    public string AcceptActionId { get; internal set; } = string.Empty;
    public string DisplayName { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;
    public V20FactionContractKind Kind { get; internal set; }
    public int DeadlineDays { get; internal set; }
    public int DeadlineAbsoluteDay { get; internal set; }
    public bool IsActive { get; internal set; }
    public bool IsCompleted { get; internal set; }
    public bool IsFailed { get; internal set; }
    public bool CanAccept { get; internal set; }
    public string DisabledReason { get; internal set; } = string.Empty;
    public string EligibilityReason { get; internal set; } = string.Empty;
    public string StatusReason { get; internal set; } = string.Empty;
    public IReadOnlyList<string> CompletionConditions { get; internal set; } =
        Array.Empty<string>();
    public IReadOnlyList<string> SuccessEffects { get; internal set; } =
        Array.Empty<string>();
    public IReadOnlyList<string> FailureEffects { get; internal set; } =
        Array.Empty<string>();
    public IReadOnlyList<FactionContractItemProgressView> Items { get; internal set; } =
        Array.Empty<FactionContractItemProgressView>();
}

public interface IFactionContractDeliveryQuery
{
    IReadOnlyList<FactionContractView> GetContracts(string factionId);
}

public interface IFactionContractDeliveryResolutionCommand
{
    bool RequiresAdvance { get; }

    void AdvanceFactionContractDeliveries(
        int absoluteDay,
        RunMilestoneEvaluationSnapshot requirements);
}

public interface IFactionCampaignDeliveryCommand
{
    bool TryAcceptContract(
        string factionId,
        string contractId,
        int absoluteDay,
        FactionContractDeliveryTarget target,
        out string failure);
    bool TryAcceptSeasonalContract(
        string occurrenceId,
        string contractId,
        FactionContractDeliveryTarget target,
        out string failure);
    bool TrySetInputOwnerProjection(
        string factionId,
        long capacityGrams,
        long massAuthorityRevision,
        string capacityFingerprint,
        out string failure);
    bool TryRecordDeliveryReceipt(
        string factionId,
        FactionContractDeliveryReceipt receipt,
        out string failure);
    bool TryResolveDeliveredContract(
        string factionId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure);
    bool TrySetDeliveryFailure(
        string factionId,
        string failureReason,
        out string failure);
    bool TryMarkInputOwnerRetired(
        string factionId,
        out string failure);
    bool TryClearDeliveryOutbox(
        string factionId,
        out string failure);
}

public sealed class FactionContractWorldMapServices
{
    public FactionContractWorldMapServices(
        IFactionContractDeliveryQuery contracts,
        IEventAlertChoiceActionDispatcher actions)
    {
        Contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public IFactionContractDeliveryQuery Contracts { get; }
    public IEventAlertChoiceActionDispatcher Actions { get; }
}

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

public sealed class FactionContractDeliveryApplicationAdapter :
    IStartable,
    ITickable
{
    private const float EvaluationIntervalSeconds = 0.5f;
    private readonly IFactionContractDeliveryResolutionCommand command;
    private readonly IV20MilestoneWorldSnapshotQuery world;
    private readonly IGameCalendar calendar;
    private float nextEvaluationTime;

    public FactionContractDeliveryApplicationAdapter(
        IFactionContractDeliveryResolutionCommand command,
        IV20MilestoneWorldSnapshotQuery world,
        IGameCalendar calendar)
    {
        this.command = command ?? throw new ArgumentNullException(nameof(command));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public void Start()
    {
        Advance();
        nextEvaluationTime = Time.time + EvaluationIntervalSeconds;
    }

    public void Tick()
    {
        if (Time.time < nextEvaluationTime)
            return;
        nextEvaluationTime = Time.time + EvaluationIntervalSeconds;
        Advance();
    }

    private void Advance()
    {
        if (!command.RequiresAdvance)
            return;
        int absoluteDay = Math.Max(1, calendar.Day);
        command.AdvanceFactionContractDeliveries(
            absoluteDay,
            world.Build(absoluteDay));
    }
}
