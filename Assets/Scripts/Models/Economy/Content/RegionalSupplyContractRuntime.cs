using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class RegionalSupplyContractAggregateState
{
    internal List<RegionalSupplyContractState> Contracts { get; } = new();
    internal IReadOnlyList<RegionalSupplyContractState> ContractView { get; set; } =
        Array.Empty<RegionalSupplyContractState>();
    internal int Version { get; set; }
    internal int CurrentDay { get; set; } = 1;
    internal int NextOfferDay { get; set; } = 1;
    internal int NextSequence { get; set; } = 1;
    internal float NextEvaluationTime { get; set; }
}

public sealed class RegionalSupplyContractRestoreCandidate
{
    public RegionalSupplyContractRestoreCandidate(
        DungeonRegionalSupplyContractSaveData payload)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    internal RegionalSupplyContractRestoreCandidate(
        RegionalSupplyContractAggregateState state,
        DungeonRegionalSupplyContractSaveData payload)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    internal RegionalSupplyContractAggregateState State { get; }
    public DungeonRegionalSupplyContractSaveData Payload { get; }
}

public sealed class RegionalSupplyContractItemSnapshot
{
    public RegionalSupplyContractItemSnapshot(
        string itemId,
        string displayName,
        ResourceItemKind kind,
        int unitPrice,
        string requiredResearchId)
    {
        ItemId = itemId?.Trim() ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Kind = kind;
        UnitPrice = Mathf.Max(0, unitPrice);
        RequiredResearchId = requiredResearchId?.Trim() ?? string.Empty;
    }

    public string ItemId { get; }
    public string DisplayName { get; }
    public ResourceItemKind Kind { get; }
    public int UnitPrice { get; }
    public string RequiredResearchId { get; }
}

public interface IRegionalSupplyContractWorldQuery
{
    IReadOnlyList<RegionalSupplyContractItemSnapshot> Items { get; }
    bool TryGetItem(
        string itemId,
        out RegionalSupplyContractItemSnapshot item);
    int ResidentPopulation { get; }
    int CompletedResearchCount { get; }
    bool IsResearchCompleted(string researchId);
    bool TryGetDeliveryDropoff(out Vector2Int dropoff);
    int CountAtDestination(
        string itemId,
        string destinationId,
        bool deliveredOnly);
}

public interface IRegionalSupplyContractCommandPort
{
    bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int dropoff,
        string destinationId,
        out int requested);
    bool TryCommitDeliveryTransferPending(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        string operationId,
        string reasonCode,
        out RegionalSupplyDeliveryTransferReceipt receipt,
        out string failureReason);
    bool TryGetPendingDeliveryTransfer(
        string operationId,
        out RegionalSupplyDeliveryTransferReceipt receipt);
    bool AcknowledgeDeliveryTransfer(
        string commitId,
        out string failureReason);
    void PrioritizeDestination(string destinationId);
    void RequestHauler();
    bool TryAddContractIncome(
        int amount,
        string operationId,
        out string failureReason);
}

public interface IRegionalSupplyContractSessionPort
{
    bool IsPaused { get; }
    float Time { get; }
    bool TryGetCurrentDay(out int day);
    IDisposable SubscribeDayStarted(Action<int> handler);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RegionalSupplyContractRuntime :
    IRegionalSupplyContractRuntime,
    IInitializable,
    ITickable,
    IDisposable
{
    private const float EvaluationInterval = 1f;
    private const int OfferIntervalDays = 3;
    private const int ContractDurationDays = 3;
    private const int MaximumHistory = 24;
    private static readonly string[] RegionNames =
    {
        "변경 교역권",
        "경쟁 던전 전초권",
        "봉인 지대"
    };

    private readonly IRegionalSupplyContractWorldQuery world;
    private readonly IRegionalSupplyContractCommandPort commands;
    private readonly IRegionalSupplyContractSessionPort session;
    private readonly IGrandProjectBenefitQuery projectBenefits;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IEconomyProjectInputOwnerPort inputOwners;
    private IDisposable daySubscription;

    private RegionalSupplyContractAggregateState state
    {
        get => aggregateRootStore.GetOrCreate(
            () => new RegionalSupplyContractAggregateState());
        set => aggregateRootStore.Replace(value);
    }

    private List<RegionalSupplyContractState> contracts => state.Contracts;
    private int currentDay
    {
        get => state.CurrentDay;
        set => state.CurrentDay = value;
    }
    private int nextOfferDay
    {
        get => state.NextOfferDay;
        set => state.NextOfferDay = value;
    }
    private int nextSequence
    {
        get => state.NextSequence;
        set => state.NextSequence = value;
    }

    public RegionalSupplyContractRuntime(
        IRegionalSupplyContractWorldQuery world,
        IRegionalSupplyContractCommandPort commands,
        IRegionalSupplyContractSessionPort session,
        IGrandProjectBenefitQuery projectBenefits,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IEconomyProjectInputOwnerPort inputOwners)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.commands = commands
            ?? throw new ArgumentNullException(nameof(commands));
        this.session = session
            ?? throw new ArgumentNullException(nameof(session));
        this.projectBenefits = projectBenefits
            ?? throw new ArgumentNullException(nameof(projectBenefits));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
    }

    public int Version => state.Version;
    public bool IsUnlocked => IsResearchCompleted("research:commerce:integration");
    public IReadOnlyList<RegionalSupplyContractState> Contracts => state.ContractView;

    public void Initialize()
    {
        if (session.TryGetCurrentDay(out int day))
        {
            currentDay = Mathf.Max(1, day);
        }

        daySubscription = session.SubscribeDayStarted(OnDayStarted);
        EnsureOffers();
        RefreshView();
    }

    public void Dispose()
    {
        daySubscription?.Dispose();
        daySubscription = null;
    }

    public void Tick()
    {
        if (session.IsPaused || session.Time < state.NextEvaluationTime)
        {
            return;
        }

        state.NextEvaluationTime = session.Time + EvaluationInterval;
        bool changed = false;
        foreach (RegionalSupplyContractState contract in contracts)
        {
            if (contract == null)
            {
                continue;
            }
            if (RegionalSupplyContractDeliveryOutbox.HasPending(contract))
            {
                changed |= ProcessDelivery(contract);
                continue;
            }
            if (contract.status is not (
                    RegionalSupplyContractStatus.Accepted
                    or RegionalSupplyContractStatus.Delivering))
            {
                continue;
            }

            if (currentDay > contract.deadlineDay)
            {
                Fail(contract, "납품 기한이 지났습니다.");
                changed = true;
                continue;
            }

            changed |= ProcessDelivery(contract);
        }

        if (changed)
        {
            Touch();
        }
    }

    public bool Accept(string contractId, out string message)
    {
        RegionalSupplyContractState contract = Find(contractId);
        if (contract == null
            || contract.status != RegionalSupplyContractStatus.Offered)
        {
            message = "수락할 수 있는 계약이 아닙니다.";
            return false;
        }

        if (!IsUnlocked)
        {
            message = "상권 통합 연구가 필요합니다.";
            return false;
        }

        if (!world.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            message = "계약 물품을 모을 하차장이 없습니다.";
            return false;
        }

        contract.status = RegionalSupplyContractStatus.Accepted;
        contract.deadlineDay = Mathf.Max(
            currentDay + ContractDurationDays,
            contract.deadlineDay);
        contract.destinationId = EconomyProjectInputOwnerAuthority
            .BuildRegionalContractDestinationId(contract.contractId);
        contract.inputOwnerActive = true;
        contract.inputDestinationX = dropoff.x;
        contract.inputDestinationY = dropoff.y;
        if (!TryEnsureInputOwner(contract, out string ownerFailure))
        {
            contract.status = RegionalSupplyContractStatus.Offered;
            contract.destinationId = string.Empty;
            ClearInputOwnerProjection(contract);
            message = "계약 집결 목적지를 열지 못했습니다: " + ownerFailure;
            return false;
        }
        contract.lastStatus = "계약 물품 운반을 시작했습니다.";
        ProcessDelivery(contract);
        Touch();
        message = $"{contract.title} 계약을 수락했습니다.";
        return true;
    }

    public bool Decline(string contractId, out string message)
    {
        RegionalSupplyContractState contract = Find(contractId);
        if (contract == null
            || contract.status != RegionalSupplyContractStatus.Offered)
        {
            message = "거절할 수 있는 계약이 아닙니다.";
            return false;
        }

        contract.status = RegionalSupplyContractStatus.Declined;
        contract.lastStatus = "계약을 거절했습니다.";
        Touch();
        message = $"{contract.title} 계약을 거절했습니다.";
        return true;
    }

    public DungeonRegionalSupplyContractSaveData Capture()
    {
        ValidateInputOwnersForCapture();
        return new DungeonRegionalSupplyContractSaveData
        {
            currentDay = currentDay,
            nextOfferDay = nextOfferDay,
            nextSequence = nextSequence,
            contracts = contracts
                .Where(contract => contract != null)
                .OrderBy(contract => contract.offeredDay)
                .ThenBy(contract => ResolveContractSequence(
                    contract.contractId))
                .Select(contract => contract.Clone())
                .ToList()
        };
    }

    public RegionalSupplyContractRestoreCandidate PrepareRestoreCandidate(
        DungeonRegionalSupplyContractSaveData saveData)
    {
        if (saveData?.contracts == null)
        {
            throw new InvalidOperationException(
                "Regional-contract restore payload or contract list is missing.");
        }
        RegionalSupplyContractAggregateState restored = new()
        {
            Version = state.Version + 1,
            CurrentDay = saveData.currentDay,
            NextOfferDay = saveData.nextOfferDay,
            NextSequence = saveData.nextSequence,
            NextEvaluationTime = session.Time + EvaluationInterval
        };
        foreach (RegionalSupplyContractState saved in saveData.contracts)
        {
            restored.Contracts.Add(saved.Clone());
        }

        RefreshView(restored);
        return new RegionalSupplyContractRestoreCandidate(restored, saveData);
    }

    public void PublishRestoreCandidate(
        RegionalSupplyContractRestoreCandidate candidate)
    {
        state = candidate.State;
    }

    private static int ResolveContractSequence(string contractId)
    {
        int separator = contractId?.LastIndexOf(':') ?? -1;
        return separator >= 0
            && int.TryParse(contractId[(separator + 1)..], out int sequence)
                ? sequence
                : int.MaxValue;
    }

    private void OnDayStarted(int day)
    {
        currentDay = Mathf.Max(1, day);
        foreach (RegionalSupplyContractState contract in contracts)
        {
            if (contract == null)
            {
                continue;
            }
            if (RegionalSupplyContractDeliveryOutbox.HasPending(contract))
            {
                ProcessDelivery(contract);
                continue;
            }
            if (contract.status is RegionalSupplyContractStatus.Accepted
                    or RegionalSupplyContractStatus.Delivering
                && currentDay > contract.deadlineDay)
            {
                Fail(contract, "납품 기한이 지났습니다.");
            }
        }

        EnsureOffers();
        Touch();
    }

    private void EnsureOffers()
    {
        EnsureOffers(state);
    }

    private void EnsureOffers(RegionalSupplyContractAggregateState target)
    {
        if (target.CurrentDay < target.NextOfferDay)
        {
            return;
        }

        foreach (RegionalSupplyContractState offered in target.Contracts.Where(
                     contract => contract != null
                         && contract.status == RegionalSupplyContractStatus.Offered))
        {
            offered.status = RegionalSupplyContractStatus.Declined;
            offered.lastStatus = "새 계약이 도착해 제안이 만료되었습니다.";
        }

        IReadOnlyList<RegionalSupplyContractItemSnapshot> candidates = world.Items
            .Where(IsContractCandidate)
            .OrderBy(item => StableHash(item.ItemId, target.CurrentDay))
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();
        int population = world.ResidentPopulation;
        int researchCount = world.CompletedResearchCount;
        for (int index = 0; index < 3 && candidates.Count > 0; index++)
        {
            RegionalSupplyContractItemSnapshot primary =
                candidates[(index * 7 + target.CurrentDay) % candidates.Count];
            RegionalSupplyContractItemSnapshot secondary = candidates.Count > 1
                ? candidates[(index * 11 + target.CurrentDay + 3) % candidates.Count]
                : null;
            RegionalSupplyContractState contract = CreateOffer(
                target,
                primary,
                secondary != primary && index == 2 ? secondary : null,
                population,
                researchCount,
                index);
            target.Contracts.Add(contract);
        }

        target.NextOfferDay = target.CurrentDay + OfferIntervalDays;
        TrimHistory(target);
    }

    private RegionalSupplyContractState CreateOffer(
        RegionalSupplyContractAggregateState target,
        RegionalSupplyContractItemSnapshot primary,
        RegionalSupplyContractItemSnapshot secondary,
        int population,
        int researchCount,
        int index)
    {
        int primaryAmount = RegionalSupplyContractSizing.ResolveAmount(
            primary.Kind,
            population,
            researchCount,
            index);
        List<RegionalSupplyContractRequirement> requirements =
            new List<RegionalSupplyContractRequirement>
            {
                new RegionalSupplyContractRequirement
                {
                    itemId = primary.ItemId,
                    amount = primaryAmount
                }
            };
        if (secondary != null)
        {
            requirements.Add(new RegionalSupplyContractRequirement
            {
                itemId = secondary.ItemId,
                amount = Mathf.Max(
                    2,
                    RegionalSupplyContractSizing.ResolveAmount(
                        secondary.Kind,
                        population,
                        researchCount,
                        index + 1) / 2)
            });
        }

        int baseValue = requirements.Sum(requirement =>
            world.TryGetItem(
                requirement.itemId,
                out RegionalSupplyContractItemSnapshot item)
                ? item.UnitPrice * requirement.amount
                : requirement.amount);
        int reward = GoldEconomyBalanceRules.CalculateRegionalContractReward(
            baseValue,
            projectBenefits.ContractRewardMultiplier);
        string region = RegionNames[index % RegionNames.Length];
        return new RegionalSupplyContractState
        {
            contractId = $"contract:{target.CurrentDay}:{target.NextSequence++}",
            title = $"{region} {primary.DisplayName} 조달",
            regionName = region,
            offeredDay = target.CurrentDay,
            deadlineDay = target.CurrentDay + ContractDurationDays,
            rewardGold = reward,
            status = RegionalSupplyContractStatus.Offered,
            lastStatus = IsUnlocked
                ? "수락 대기"
                : "상권 통합 연구 필요",
            requirements = requirements
        };
    }

    private bool ProcessDelivery(RegionalSupplyContractState contract)
    {
        if (RegionalSupplyContractDeliveryOutbox.HasPending(contract))
        {
            if (!RegionalSupplyContractDeliveryOutbox.TryFinalizePending(
                    contract,
                    commands,
                    out string pendingFailure))
            {
                contract.lastStatus = pendingFailure;
            }
            else
            {
                TryRetireCompletedInputOwner(contract);
            }
            return true;
        }

        if (!world.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            contract.lastStatus = "계약 집결점이 없습니다.";
            return false;
        }

        if (contract.inputDestinationX != dropoff.x
            || contract.inputDestinationY != dropoff.y)
        {
            contract.inputDestinationX = dropoff.x;
            contract.inputDestinationY = dropoff.y;
            contract.inputCapacityGrams = 0L;
            contract.inputMassAuthorityRevision = 0L;
            contract.inputCapacityFingerprint = string.Empty;
        }

        if (!TryEnsureInputOwner(contract, out string ownerFailure))
        {
            contract.lastStatus = ownerFailure;
            return false;
        }

        bool requestedAny = false;
        bool complete = true;
        foreach (RegionalSupplyContractRequirement requirement in
                 contract.requirements)
        {
            int pending = CountAtDestination(
                requirement.itemId,
                contract.destinationId,
                deliveredOnly: false);
            int missing = Mathf.Max(0, requirement.amount - pending);
            if (missing > 0)
            {
                complete = false;
                commands.RequestDelivery(
                    requirement.itemId,
                    missing,
                    dropoff,
                    contract.destinationId,
                    out int requested);
                requestedAny |= requested > 0;
            }

            int delivered = CountAtDestination(
                requirement.itemId,
                contract.destinationId,
                deliveredOnly: true);
            complete &= delivered >= requirement.amount;
        }

        if (!complete)
        {
            contract.status = RegionalSupplyContractStatus.Delivering;
            contract.lastStatus = BuildDeliveryStatus(contract);
            if (requestedAny)
            {
                commands.PrioritizeDestination(contract.destinationId);
                commands.RequestHauler();
            }
            return requestedAny;
        }

        Dictionary<string, int> costs = contract.requirements
            .GroupBy(requirement => requirement.itemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(requirement => requirement.amount),
                StringComparer.Ordinal);
        string operationId =
            RegionalSupplyContractDeliveryOutbox.FormatOperationId(
                contract.contractId);
        if (!commands.TryCommitDeliveryTransferPending(
                contract.destinationId,
                costs,
                operationId,
                RegionalSupplyContractDeliveryOutbox.TransferReason,
                out RegionalSupplyDeliveryTransferReceipt receipt,
                out string failureReason))
        {
            contract.lastStatus = failureReason;
            return false;
        }

        RegionalSupplyContractDeliveryOutbox.RecordPending(
            contract,
            receipt);
        if (!RegionalSupplyContractDeliveryOutbox.TryFinalizePending(
                contract,
                commands,
                out string finalizeFailure))
        {
            contract.lastStatus = finalizeFailure;
        }
        else
        {
            TryRetireCompletedInputOwner(contract);
        }
        return true;
    }

    private void Fail(
        RegionalSupplyContractState contract,
        string reason)
    {
        if (contract == null)
        {
            return;
        }

        if (contract.inputOwnerActive
            && !inputOwners.TryRetireDestination(
                EconomyProjectInputOwnerAuthority.RegionalContractDomain,
                contract.destinationId,
                EconomyProjectInputOwnerAuthority.RegionalContractTerminalReason,
                out string releaseFailure))
        {
            contract.lastStatus = releaseFailure;
            return;
        }

        ClearInputOwnerProjection(contract);
        contract.status = RegionalSupplyContractStatus.Failed;
        contract.lastStatus = reason ?? "계약 실패";
    }

    private bool IsContractCandidate(RegionalSupplyContractItemSnapshot item)
    {
        return item != null
            && item.UnitPrice > 0
            && item.UnitPrice >= RegionalSupplyContractSizing.MinimumViableUnitPrice(
                item.Kind)
            && item.Kind is ResourceItemKind.Raw
                or ResourceItemKind.Intermediate
                or ResourceItemKind.FinishedGood
                or ResourceItemKind.Food
                or ResourceItemKind.Medicine
                or ResourceItemKind.Ammunition
            && (string.IsNullOrWhiteSpace(item.RequiredResearchId)
                || IsResearchCompleted(item.RequiredResearchId));
    }

    private bool TryEnsureInputOwner(
        RegionalSupplyContractState contract,
        out string failureReason)
    {
        if (contract == null || !contract.inputOwnerActive)
        {
            failureReason = "regional-contract-input-owner-inactive";
            return false;
        }
        if (!inputOwners.TryEnsure(
                EconomyProjectInputOwnerAuthority.RegionalContractDomain,
                contract.contractId,
                contract.destinationId,
                new Vector2Int(contract.inputDestinationX, contract.inputDestinationY),
                EconomyProjectInputOwnerAnchorKind.ReservedTarget,
                string.Empty,
                BuildRequirements(contract),
                contract.inputCapacityGrams,
                contract.inputMassAuthorityRevision,
                contract.inputCapacityFingerprint,
                out EconomyProjectInputOwnerProjection projection,
                out failureReason))
            return false;
        contract.inputCapacityGrams = projection.CapacityGrams;
        contract.inputMassAuthorityRevision = projection.MassAuthorityRevision;
        contract.inputCapacityFingerprint = projection.Fingerprint;
        return true;
    }

    private void TryRetireCompletedInputOwner(
        RegionalSupplyContractState contract)
    {
        if (contract == null || !contract.inputOwnerActive
            || contract.status != RegionalSupplyContractStatus.Completed)
            return;
        if (!inputOwners.TryRetireDestination(
                EconomyProjectInputOwnerAuthority.RegionalContractDomain,
                contract.destinationId,
                EconomyProjectInputOwnerAuthority.RegionalContractTerminalReason,
                out string failureReason))
        {
            contract.lastStatus = failureReason;
            return;
        }
        ClearInputOwnerProjection(contract);
    }

    private void ValidateInputOwnersForCapture()
    {
        foreach (RegionalSupplyContractState contract in contracts
                     .Where(value => value != null && value.inputOwnerActive))
        {
            if (!inputOwners.TryValidate(
                    EconomyProjectInputOwnerAuthority.RegionalContractDomain,
                    contract.contractId,
                    contract.destinationId,
                    new Vector2Int(contract.inputDestinationX, contract.inputDestinationY),
                    EconomyProjectInputOwnerAnchorKind.ReservedTarget,
                    string.Empty,
                    BuildRequirements(contract),
                    contract.inputCapacityGrams,
                    contract.inputMassAuthorityRevision,
                    contract.inputCapacityFingerprint,
                    out string failureReason))
                throw new InvalidOperationException(
                    "Regional-contract input owner capture validation failed: "
                    + failureReason);
        }
    }

    private static IReadOnlyDictionary<string, int> BuildRequirements(
        RegionalSupplyContractState contract) =>
        (contract.requirements ?? new List<RegionalSupplyContractRequirement>())
            .GroupBy(value => value.itemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.amount),
                StringComparer.Ordinal);

    private static void ClearInputOwnerProjection(
        RegionalSupplyContractState contract)
    {
        contract.inputOwnerActive = false;
        contract.inputDestinationX = 0;
        contract.inputDestinationY = 0;
        contract.inputCapacityGrams = 0L;
        contract.inputMassAuthorityRevision = 0L;
        contract.inputCapacityFingerprint = string.Empty;
    }

    private bool IsResearchCompleted(string researchId)
    {
        return string.IsNullOrWhiteSpace(researchId)
            || world.IsResearchCompleted(researchId);
    }

    private int CountAtDestination(
        string itemId,
        string destinationId,
        bool deliveredOnly)
    {
        return world.CountAtDestination(
            itemId,
            destinationId,
            deliveredOnly);
    }

    private string BuildDeliveryStatus(
        RegionalSupplyContractState contract)
    {
        string progress = string.Join(
            ", ",
            contract.requirements.Select(requirement =>
                $"{ResolveItemName(requirement.itemId)} "
                + $"{CountAtDestination(requirement.itemId, contract.destinationId, deliveredOnly: false)}"
                + $"/{requirement.amount}"));
        return $"납품 운반 중 · {progress}";
    }

    private string ResolveItemName(string itemId)
    {
        return world.TryGetItem(
            itemId,
            out RegionalSupplyContractItemSnapshot item)
            ? item.DisplayName
            : itemId;
    }

    private RegionalSupplyContractState Find(string contractId)
    {
        return contracts.FirstOrDefault(contract => contract != null
            && string.Equals(
                contract.contractId,
                contractId,
                StringComparison.Ordinal));
    }

    private void TrimHistory()
    {
        TrimHistory(state);
    }

    private static void TrimHistory(RegionalSupplyContractAggregateState target)
    {
        RegionalSupplyContractState[] removable = target.Contracts
            .Where(contract => contract != null
                && !RegionalSupplyContractDeliveryOutbox.HasPending(contract)
                && contract.status is RegionalSupplyContractStatus.Completed
                    or RegionalSupplyContractStatus.Failed
                    or RegionalSupplyContractStatus.Declined)
            .OrderBy(contract => contract.offeredDay)
            .ToArray();
        int removeCount = Mathf.Max(
            0,
            target.Contracts.Count - MaximumHistory);
        for (int i = 0; i < removeCount && i < removable.Length; i++)
        {
            target.Contracts.Remove(removable[i]);
        }
    }

    private void Touch()
    {
        state.Version++;
        RefreshView();
    }

    private void RefreshView()
    {
        RefreshView(state);
    }

    private static void RefreshView(RegionalSupplyContractAggregateState target)
    {
        target.ContractView = target.Contracts
            .Where(contract => contract != null)
            .OrderBy(contract => ContractSortOrder(contract.status))
            .ThenByDescending(contract => contract.offeredDay)
            .ThenBy(contract => contract.contractId, StringComparer.Ordinal)
            .Select(contract => contract.Clone())
            .ToArray();
    }

    private static int ContractSortOrder(
        RegionalSupplyContractStatus status)
    {
        return status switch
        {
            RegionalSupplyContractStatus.Accepted => 0,
            RegionalSupplyContractStatus.Delivering => 1,
            RegionalSupplyContractStatus.Offered => 2,
            RegionalSupplyContractStatus.Completed => 3,
            RegionalSupplyContractStatus.Failed => 4,
            _ => 5
        };
    }

    private static uint StableHash(string value, int salt)
    {
        uint hash = 2166136261u ^ unchecked((uint)salt);
        foreach (char character in value ?? string.Empty)
        {
            hash ^= character;
            hash *= 16777619u;
        }
        return hash;
    }
}
