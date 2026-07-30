using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

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

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly IGrandProjectBenefitQuery projectBenefits;
    private readonly IGameDataProvider gameDataProvider;
    private readonly IGameMoneyRuntime money;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly IWorkforceReplanService workforce;
    private readonly List<RegionalSupplyContractState> contracts =
        new List<RegionalSupplyContractState>();
    private IDisposable daySubscription;
    private int currentDay = 1;
    private int nextOfferDay = 1;
    private int nextSequence = 1;
    private float nextEvaluationTime;
    private IReadOnlyList<RegionalSupplyContractState> contractView =
        Array.Empty<RegionalSupplyContractState>();

    public RegionalSupplyContractRuntime(
        IResourceEconomyContentCatalog catalog,
        IWorldItemStackRuntime itemRuntime,
        IWorldDropZoneQuery dropZones,
        ICharacterWorldQuery characterWorld,
        IGameDataProvider gameDataProvider,
        IGameMoneyRuntime money,
        IGameEventBus gameEventBus,
        IGameClock gameClock,
        IBlueprintResearchRuntimeProvider researchProvider = null,
        IGrandProjectBenefitQuery projectBenefits = null,
        IWorkforceReplanService workforce = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.dropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.researchProvider = researchProvider;
        this.projectBenefits = projectBenefits;
        this.workforce = workforce;
    }

    public int Version { get; private set; }
    public bool IsUnlocked => IsResearchCompleted("research:commerce:integration");
    public IReadOnlyList<RegionalSupplyContractState> Contracts => contractView;

    public void Initialize()
    {
        if (gameDataProvider.TryGetGameData(out GameData data)
            && data?.day != null)
        {
            currentDay = Mathf.Max(1, data.day.Value);
        }

        daySubscription =
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnDayStarted);
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
        if (gameClock.IsPaused || gameClock.Time < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = gameClock.Time + EvaluationInterval;
        bool changed = false;
        foreach (RegionalSupplyContractState contract in contracts)
        {
            if (contract == null
                || contract.status is not (
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

        if (!dropZones.TryGetDeliveryDropoff(out _))
        {
            message = "계약 물품을 모을 하차장이 없습니다.";
            return false;
        }

        contract.status = RegionalSupplyContractStatus.Accepted;
        contract.deadlineDay = Mathf.Max(
            currentDay + ContractDurationDays,
            contract.deadlineDay);
        contract.destinationId =
            $"regional-contract:{contract.contractId}";
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
        return new DungeonRegionalSupplyContractSaveData
        {
            currentDay = currentDay,
            nextOfferDay = nextOfferDay,
            nextSequence = nextSequence,
            contracts = contracts
                .Where(contract => contract != null)
                .Select(contract => contract.Clone())
                .ToList()
        };
    }

    public void Restore(DungeonRegionalSupplyContractSaveData saveData)
    {
        contracts.Clear();
        currentDay = Mathf.Max(1, saveData?.currentDay ?? 1);
        nextOfferDay = Mathf.Max(1, saveData?.nextOfferDay ?? currentDay);
        nextSequence = Mathf.Max(1, saveData?.nextSequence ?? 1);
        foreach (RegionalSupplyContractState saved in saveData?.contracts
                 ?? new List<RegionalSupplyContractState>())
        {
            if (saved == null
                || string.IsNullOrWhiteSpace(saved.contractId)
                || saved.requirements == null
                || saved.requirements.Count == 0)
            {
                continue;
            }

            contracts.Add(saved.Clone());
        }

        EnsureOffers();
        Touch();
    }

    private void OnDayStarted(OperatingDayStartedEvent eventType)
    {
        currentDay = Mathf.Max(1, eventType.day);
        foreach (RegionalSupplyContractState contract in contracts)
        {
            if (contract != null
                && contract.status is RegionalSupplyContractStatus.Accepted
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
        if (currentDay < nextOfferDay)
        {
            return;
        }

        foreach (RegionalSupplyContractState offered in contracts.Where(
                     contract => contract != null
                         && contract.status == RegionalSupplyContractStatus.Offered))
        {
            offered.status = RegionalSupplyContractStatus.Declined;
            offered.lastStatus = "새 계약이 도착해 제안이 만료되었습니다.";
        }

        IReadOnlyList<ResourceItemDefinitionSO> candidates = catalog.Items
            .Where(IsContractCandidate)
            .OrderBy(item => StableHash(item.ItemId, currentDay))
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();
        int population = characterWorld.Characters.Count(IsResident);
        int researchCount = ResolveCompletedResearchCount();
        for (int index = 0; index < 3 && candidates.Count > 0; index++)
        {
            ResourceItemDefinitionSO primary =
                candidates[(index * 7 + currentDay) % candidates.Count];
            ResourceItemDefinitionSO secondary = candidates.Count > 1
                ? candidates[(index * 11 + currentDay + 3) % candidates.Count]
                : null;
            RegionalSupplyContractState contract = CreateOffer(
                primary,
                secondary != primary && index == 2 ? secondary : null,
                population,
                researchCount,
                index);
            contracts.Add(contract);
        }

        nextOfferDay = currentDay + OfferIntervalDays;
        TrimHistory();
    }

    private RegionalSupplyContractState CreateOffer(
        ResourceItemDefinitionSO primary,
        ResourceItemDefinitionSO secondary,
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
            catalog.TryGetItem(
                requirement.itemId,
                out ResourceItemDefinitionSO item)
                ? item.UnitPrice * requirement.amount
                : requirement.amount);
        int reward = Mathf.Max(
            25,
            Mathf.RoundToInt(
                baseValue
                * 1.35f
                * (projectBenefits?.ContractRewardMultiplier ?? 1f)));
        string region = RegionNames[index % RegionNames.Length];
        return new RegionalSupplyContractState
        {
            contractId = $"contract:{currentDay}:{nextSequence++}",
            title = $"{region} {primary.DisplayName} 조달",
            regionName = region,
            offeredDay = currentDay,
            deadlineDay = currentDay + ContractDurationDays,
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
        if (!dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            contract.lastStatus = "계약 집결점이 없습니다.";
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
                requiredState: null);
            int missing = Mathf.Max(0, requirement.amount - pending);
            if (missing > 0)
            {
                complete = false;
                itemRuntime.TryRequestItemDelivery(
                    requirement.itemId,
                    missing,
                    dropoff,
                    contract.destinationId,
                    out int requested,
                    out _);
                requestedAny |= requested > 0;
            }

            int delivered = CountAtDestination(
                requirement.itemId,
                contract.destinationId,
                WorldItemStackState.FacilityBuffer);
            complete &= delivered >= requirement.amount;
        }

        if (!complete)
        {
            contract.status = RegionalSupplyContractStatus.Delivering;
            contract.lastStatus = BuildDeliveryStatus(contract);
            if (requestedAny)
            {
                PrioritizeDestination(contract.destinationId);
                workforce?.RequestOneHaulerToReplan(forceInterrupt: false);
            }
            return requestedAny;
        }

        Dictionary<string, int> costs = contract.requirements
            .GroupBy(requirement => requirement.itemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(requirement => requirement.amount),
                StringComparer.Ordinal);
        if (!itemRuntime.TryConsumeFacilityItemBuffer(
                contract.destinationId,
                costs,
                out string failureReason))
        {
            contract.lastStatus = failureReason;
            return false;
        }

        AddMoney(contract.rewardGold);
        contract.status = RegionalSupplyContractStatus.Completed;
        contract.lastStatus = $"납품 완료 · {contract.rewardGold} 골드 획득";
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

        if (!string.IsNullOrWhiteSpace(contract.destinationId)
            && dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            itemRuntime.ReleaseStacksByDestination(
                contract.destinationId,
                dropoff);
        }

        contract.status = RegionalSupplyContractStatus.Failed;
        contract.lastStatus = reason ?? "계약 실패";
    }

    private bool IsContractCandidate(ResourceItemDefinitionSO item)
    {
        return item != null
            && item.UnitPrice > 0
            && item.Kind is ResourceItemKind.Raw
                or ResourceItemKind.Intermediate
                or ResourceItemKind.FinishedGood
                or ResourceItemKind.Food
                or ResourceItemKind.Medicine
                or ResourceItemKind.Ammunition
            && (string.IsNullOrWhiteSpace(item.RequiredResearchId)
                || IsResearchCompleted(item.RequiredResearchId));
    }

    private int ResolveCompletedResearchCount()
    {
        return researchProvider != null
            && researchProvider.TryGetRuntime(out BlueprintResearchRuntime runtime)
            ? runtime.State.Projects.CompletedProjectIds.Count
            : 0;
    }

    private bool IsResearchCompleted(string researchId)
    {
        return string.IsNullOrWhiteSpace(researchId)
            || (researchProvider != null
                && researchProvider.TryGetRuntime(out BlueprintResearchRuntime runtime)
                && runtime.State.Projects.IsCompleted(
                    new ResearchProjectId(researchId)));
    }

    private int CountAtDestination(
        string itemId,
        string destinationId,
        WorldItemStackState? requiredState)
    {
        return itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && (!requiredState.HasValue
                    || stack.State == requiredState.Value))
            .Sum(stack => stack.Quantity);
    }

    private string BuildDeliveryStatus(
        RegionalSupplyContractState contract)
    {
        string progress = string.Join(
            ", ",
            contract.requirements.Select(requirement =>
                $"{ResolveItemName(requirement.itemId)} "
                + $"{CountAtDestination(requirement.itemId, contract.destinationId, null)}"
                + $"/{requirement.amount}"));
        return $"납품 운반 중 · {progress}";
    }

    private string ResolveItemName(string itemId)
    {
        return catalog.TryGetItem(
            itemId,
            out ResourceItemDefinitionSO item)
            ? item.DisplayName
            : itemId;
    }

    private void PrioritizeDestination(string destinationId)
    {
        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks())
        {
            if (stack != null
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            {
                itemRuntime.PrioritizeHaul(stack.StackId);
            }
        }
    }

    private void AddMoney(int amount)
    {
        if (amount > 0)
        {
            money.Add(
                amount,
                new EconomyTransactionContext(
                    EconomyTransactionKind.ContractIncome,
                    "regional-supply",
                    description: "지역 공급 계약"));
        }
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
        RegionalSupplyContractState[] removable = contracts
            .Where(contract => contract != null
                && contract.status is RegionalSupplyContractStatus.Completed
                    or RegionalSupplyContractStatus.Failed
                    or RegionalSupplyContractStatus.Declined)
            .OrderBy(contract => contract.offeredDay)
            .ToArray();
        int removeCount = Mathf.Max(0, contracts.Count - MaximumHistory);
        for (int i = 0; i < removeCount && i < removable.Length; i++)
        {
            contracts.Remove(removable[i]);
        }
    }

    private void Touch()
    {
        Version++;
        RefreshView();
    }

    private void RefreshView()
    {
        contractView = contracts
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

    private static bool IsResident(CharacterActor actor)
    {
        return actor != null
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            && (actor.IsOwner
                || StaffDiscontentService.IsTrackableStaff(actor));
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
