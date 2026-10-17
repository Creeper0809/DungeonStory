using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;

public interface IPaidFacilityContractRuntime : IBuildingPaidFacilityContractPort
{
    int ForecastCost(int days);
    int GetDailyCost(string facilityPersistentId);
    int SettleDay(int day);
    IReadOnlyList<PaidFacilityContractState> Contracts { get; }
    PaidFacilityContractState GetContract(BuildableObject facility);
    bool CanBeginUse(BuildableObject facility, out string failureReason);
    bool TryChargeUse(BuildableObject facility, out string failureReason);
    bool TryChargeOrder(
        BuildableObject facility,
        string orderKey,
        out string failureReason);
    bool TrySetDailyContractActive(
        BuildableObject facility,
        bool active,
        out string failureReason);
    void SynchronizeFacility(BuildableObject facility);
    void RemoveFacility(BuildableObject facility);
    string GetLastFailureReason(BuildableObject facility);
    PaidFacilityContractSaveData Capture();
}

public sealed class PaidFacilityContractRuntime : IPaidFacilityContractRuntime
{
    private const int MaxChargedOrderKeys = 256;

    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IGameMoneyAccount money;
    private readonly TreasuryEconomyAggregateStateStore stateStore;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;

    private List<PaidFacilityContractState> contracts =>
        stateStore.Current.FacilityContracts;
    private HashSet<string> chargedOrderKeys =>
        stateStore.Current.ChargedFacilityOrderKeys;
    private Dictionary<string, string> lastFailureByFacility =>
        stateStore.Current.FacilityFailures;

    public PaidFacilityContractRuntime(
        IGameSessionStateProvider gameDataProvider,
        IGameMoneyAccount money,
        TreasuryEconomyAggregateStateStore stateStore,
        IMilestoneGameplayModifierQuery milestoneModifiers = null)
    {
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.milestoneModifiers = milestoneModifiers
            ?? NeutralMilestoneGameplayModifierQuery.Instance;
    }

    public IReadOnlyList<PaidFacilityContractState> Contracts => contracts;

    bool IBuildingPaidFacilityContractPort.CanBeginUse(
        IBuildingWorldEntryPort facility,
        out string failureReason)
    {
        return CanBeginUse(RequireBuildableFacility(facility), out failureReason);
    }

    bool IBuildingPaidFacilityContractPort.TryChargeUse(
        IBuildingWorldEntryPort facility,
        out string failureReason)
    {
        return TryChargeUse(RequireBuildableFacility(facility), out failureReason);
    }

    void IBuildingPaidFacilityContractPort.SynchronizeFacility(
        IBuildingWorldEntryPort facility)
    {
        SynchronizeFacility(RequireBuildableFacility(facility));
    }

    void IBuildingPaidFacilityContractPort.RemoveFacility(
        IBuildingWorldEntryPort facility)
    {
        RemoveFacility(RequireBuildableFacility(facility));
    }

    public PaidFacilityContractState GetContract(BuildableObject facility)
    {
        string facilityId = ResolveFacilityId(facility);
        return contracts.FirstOrDefault(contract =>
            contract != null
            && string.Equals(
                contract.facilityPersistentId,
                facilityId,
                StringComparison.Ordinal));
    }

    public bool CanBeginUse(
        BuildableObject facility,
        out string failureReason)
    {
        BuildingPaidFacilityServiceAbility ability = GetAbility(facility);
        if (ability == null || ability.cost <= 0)
        {
            failureReason = string.Empty;
            return true;
        }

        if (ability.chargeMode == PaidFacilityChargeMode.DailyContract)
        {
            PaidFacilityContractState contract = GetContract(facility);
            if (contract?.active == true)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason =
                $"{ResolveDisplayName(ability, "시설 계약")}: 계약이 중단되었습니다.";
            RememberFailure(facility, failureReason);
            return false;
        }

        if (ability.chargeMode != PaidFacilityChargeMode.PerUse)
        {
            failureReason = string.Empty;
            return true;
        }

        if (money.CanSpend(ability.cost))
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = $"이용료 {ability.cost:N0}골드가 필요합니다.";
        RememberFailure(facility, failureReason);
        return false;
    }

    public bool TryChargeUse(
        BuildableObject facility,
        out string failureReason)
    {
        BuildingPaidFacilityServiceAbility ability = GetAbility(facility);
        if (ability?.chargeMode == PaidFacilityChargeMode.DailyContract)
        {
            return CanBeginUse(facility, out failureReason);
        }

        if (ability == null
            || ability.chargeMode != PaidFacilityChargeMode.PerUse)
        {
            failureReason = string.Empty;
            return true;
        }

        return TryCharge(
            facility,
            ability,
            EconomyTransactionKind.PaidFacilityUse,
            "시설 이용",
            out failureReason);
    }

    public bool TryChargeOrder(
        BuildableObject facility,
        string orderKey,
        out string failureReason)
    {
        BuildingPaidFacilityServiceAbility ability = GetAbility(facility);
        if (ability?.chargeMode == PaidFacilityChargeMode.DailyContract)
        {
            return CanBeginUse(facility, out failureReason);
        }

        if (ability == null
            || ability.chargeMode != PaidFacilityChargeMode.PerOrder)
        {
            failureReason = string.Empty;
            return true;
        }

        string facilityId = ResolveFacilityId(facility);
        string normalizedOrderKey = orderKey?.Trim() ?? string.Empty;
        string chargedKey = $"{facilityId}|{normalizedOrderKey}";
        if (normalizedOrderKey.Length > 0
            && chargedOrderKeys.Contains(chargedKey))
        {
            failureReason = string.Empty;
            return true;
        }

        if (!TryCharge(
                facility,
                ability,
                EconomyTransactionKind.PaidFacilityOrder,
                "시설 작업 주문",
                out failureReason))
        {
            return false;
        }

        if (normalizedOrderKey.Length > 0)
        {
            chargedOrderKeys.Add(chargedKey);
            TrimChargedOrderKeys();
        }

        return true;
    }

    public bool TrySetDailyContractActive(
        BuildableObject facility,
        bool active,
        out string failureReason)
    {
        BuildingPaidFacilityServiceAbility ability = GetAbility(facility);
        if (ability == null
            || ability.chargeMode != PaidFacilityChargeMode.DailyContract
            || ability.cost <= 0)
        {
            failureReason = "일일 계약 시설이 아닙니다.";
            return false;
        }

        SynchronizeFacility(facility);
        PaidFacilityContractState contract = GetContract(facility);
        if (contract == null)
        {
            failureReason = "시설 계약 상태를 찾지 못했습니다.";
            RememberFailure(facility, failureReason);
            return false;
        }

        if (!active)
        {
            contract.active = false;
            failureReason = string.Empty;
            lastFailureByFacility.Remove(ResolveFacilityId(facility));
            return true;
        }

        if (contract.active)
        {
            failureReason = string.Empty;
            return true;
        }

        int currentDay = ResolveCurrentDay();
        if (!money.TrySpend(
                Mathf.Max(0, contract.dailyCost),
                new EconomyTransactionContext(
                    EconomyTransactionKind.PaidFacilityContract,
                    contract.contractId,
                    contract.facilityPersistentId,
                    $"{ResolveDisplayName(ability, "유료 시설")} 계약 재개"),
                out failureReason))
        {
            failureReason =
                $"{ResolveDisplayName(ability, "유료 시설")}: {failureReason}";
            RememberFailure(facility, failureReason);
            return false;
        }

        contract.active = true;
        contract.lastSettledDay = currentDay;
        lastFailureByFacility.Remove(ResolveFacilityId(facility));
        failureReason = string.Empty;
        return true;
    }

    public void SynchronizeFacility(BuildableObject facility)
    {
        if (facility == null)
        {
            return;
        }

        string facilityId = ResolveFacilityId(facility);
        PaidFacilityContractState previous = contracts.FirstOrDefault(contract =>
            contract != null
            && string.Equals(
                contract.facilityPersistentId,
                facilityId,
                StringComparison.Ordinal));
        contracts.RemoveAll(contract =>
            contract != null
            && string.Equals(
                contract.facilityPersistentId,
                facilityId,
                StringComparison.Ordinal));

        BuildingPaidFacilityServiceAbility ability = GetAbility(facility);
        if (ability == null
            || ability.chargeMode != PaidFacilityChargeMode.DailyContract
            || ability.cost <= 0)
        {
            return;
        }

        string configuredId = ability.contractId?.Trim() ?? string.Empty;
        contracts.Add(new PaidFacilityContractState
        {
            contractId = configuredId.Length > 0
                ? configuredId
                : $"facility-contract:{facilityId}",
            facilityPersistentId = facilityId,
            dailyCost = Mathf.Max(0, ability.cost),
            active = previous?.active ?? true,
            lastSettledDay = Mathf.Max(0, previous?.lastSettledDay ?? 0)
        });
    }

    public void RemoveFacility(BuildableObject facility)
    {
        string facilityId = ResolveFacilityId(facility);
        if (facilityId.Length == 0)
        {
            return;
        }

        contracts.RemoveAll(contract =>
            contract != null
            && string.Equals(
                contract.facilityPersistentId,
                facilityId,
                StringComparison.Ordinal));
        lastFailureByFacility.Remove(facilityId);
    }

    public string GetLastFailureReason(BuildableObject facility)
    {
        return lastFailureByFacility.TryGetValue(
            ResolveFacilityId(facility),
            out string reason)
            ? reason
            : string.Empty;
    }

    public int ForecastCost(int days)
    {
        return contracts
            .Where(contract => contract != null && contract.active)
            .Sum(contract => ResolveDailyCost(contract.dailyCost))
            * Mathf.Max(0, days);
    }

    public int GetDailyCost(string facilityPersistentId)
    {
        if (string.IsNullOrWhiteSpace(facilityPersistentId))
        {
            throw new ArgumentException(
                "Facility persistent identity is required.",
                nameof(facilityPersistentId));
        }
        PaidFacilityContractState contract = contracts.FirstOrDefault(value =>
            value != null
            && value.active
            && string.Equals(
                value.facilityPersistentId,
                facilityPersistentId,
                StringComparison.Ordinal));
        return contract == null ? 0 : ResolveDailyCost(contract.dailyCost);
    }

    public int SettleDay(int day)
    {
        int paid = 0;
        foreach (PaidFacilityContractState contract in contracts
                     .Where(candidate => candidate != null && candidate.active)
                     .OrderBy(candidate => candidate.contractId, StringComparer.Ordinal))
        {
            if (contract.lastSettledDay >= day)
            {
                continue;
            }

            int cost = ResolveDailyCost(contract.dailyCost);
            if (money.TrySpend(
                    cost,
                    new EconomyTransactionContext(
                        EconomyTransactionKind.PaidFacilityContract,
                        contract.contractId,
                        contract.facilityPersistentId,
                        "유료 시설 계약"),
                    out _))
            {
                paid += cost;
                contract.lastSettledDay = day;
            }
            else
            {
                contract.active = false;
                lastFailureByFacility[contract.facilityPersistentId] =
                    $"일일 계약비 {cost:N0}골드가 부족해 운영이 중단되었습니다.";
            }
        }

        return paid;
    }

    private int ResolveDailyCost(int authoredCost) => Mathf.Max(
        0,
        Mathf.RoundToInt(
            Mathf.Max(0, authoredCost)
            * Mathf.Clamp(
                milestoneModifiers.FacilityMaintenanceGoldMultiplier,
                0f,
                1f)));

    public PaidFacilityContractSaveData Capture()
    {
        return new PaidFacilityContractSaveData
        {
            contracts = contracts
                .Where(contract => contract != null)
                .Select(contract => new PaidFacilityContractState
                {
                    contractId = contract.contractId,
                    facilityPersistentId = contract.facilityPersistentId,
                    dailyCost = contract.dailyCost,
                    active = contract.active,
                    lastSettledDay = contract.lastSettledDay
                })
                .ToList(),
            chargedOrderKeys = chargedOrderKeys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList()
        };
    }

    internal void PopulateRestoreState(
        TreasuryEconomyAggregateState target,
        PaidFacilityContractSaveData saveData)
    {
        target = target ?? throw new ArgumentNullException(nameof(target));
        target.FacilityContracts.Clear();
        target.ChargedFacilityOrderKeys.Clear();
        target.FacilityFailures.Clear();
        target.FacilityContracts.AddRange((saveData?.contracts
                ?? new List<PaidFacilityContractState>())
            .Where(contract => contract != null)
            .Select(contract => new PaidFacilityContractState
            {
                contractId = contract.contractId?.Trim() ?? string.Empty,
                facilityPersistentId =
                    contract.facilityPersistentId?.Trim() ?? string.Empty,
                dailyCost = Mathf.Max(0, contract.dailyCost),
                active = contract.active,
                lastSettledDay = Mathf.Max(0, contract.lastSettledDay)
            })
            .Where(contract => contract.contractId.Length > 0));
        foreach (string key in saveData?.chargedOrderKeys
                     ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                target.ChargedFacilityOrderKeys.Add(key.Trim());
            }
        }

        TrimChargedOrderKeys(target.ChargedFacilityOrderKeys);
    }

    private bool TryCharge(
        BuildableObject facility,
        BuildingPaidFacilityServiceAbility ability,
        EconomyTransactionKind kind,
        string sourceLabel,
        out string failureReason)
    {
        int cost = Mathf.Max(0, ability?.cost ?? 0);
        if (cost == 0)
        {
            failureReason = string.Empty;
            return true;
        }

        string facilityId = ResolveFacilityId(facility);
        string displayName = ResolveDisplayName(ability, sourceLabel);
        if (money.TrySpend(
                cost,
                new EconomyTransactionContext(
                    kind,
                    ability.AbilityId,
                    facilityId,
                    displayName),
                out failureReason))
        {
            lastFailureByFacility.Remove(facilityId);
            return true;
        }

        failureReason = $"{displayName}: {failureReason}";
        RememberFailure(facility, failureReason);
        return false;
    }

    private void RememberFailure(
        BuildableObject facility,
        string failureReason)
    {
        string facilityId = ResolveFacilityId(facility);
        if (facilityId.Length > 0)
        {
            lastFailureByFacility[facilityId] =
                failureReason?.Trim() ?? string.Empty;
        }
    }

    private static BuildingPaidFacilityServiceAbility GetAbility(
        BuildableObject facility)
    {
        return facility?.BuildingData?
            .GetAbility<BuildingPaidFacilityServiceAbility>();
    }

    private int ResolveCurrentDay()
    {
        return gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            && gameData?.day != null
            ? Mathf.Max(1, gameData.day.Value)
            : 1;
    }

    private static string ResolveDisplayName(
        BuildingPaidFacilityServiceAbility ability,
        string fallback)
    {
        return string.IsNullOrWhiteSpace(ability?.displayName)
            ? fallback
            : ability.displayName.Trim();
    }

    private static string ResolveFacilityId(BuildableObject facility)
    {
        if (facility == null)
        {
            return string.Empty;
        }

        return facility.RequirePersistentInstanceId().Value;
    }

    private static BuildableObject RequireBuildableFacility(
        IBuildingWorldEntryPort facility)
    {
        if (facility == null)
        {
            return null;
        }

        return facility as BuildableObject
            ?? throw new ArgumentException(
                $"{nameof(IBuildingPaidFacilityContractPort)} only accepts {nameof(BuildableObject)} facilities.",
                nameof(facility));
    }

    private void TrimChargedOrderKeys()
    {
        TrimChargedOrderKeys(chargedOrderKeys);
    }

    private static void TrimChargedOrderKeys(HashSet<string> keys)
    {
        if (keys.Count <= MaxChargedOrderKeys)
        {
            return;
        }

        foreach (string key in keys
                     .OrderBy(value => value, StringComparer.Ordinal)
                     .Take(keys.Count - MaxChargedOrderKeys)
                     .ToArray())
        {
            keys.Remove(key);
        }
    }
}

public interface IAutoProcurementRuntime
{
    int DailyBudget { get; }
    int MinimumReserve { get; }
    int ProtectedFunds { get; }
    IReadOnlyList<AutoProcurementRule> StockRules { get; }
    IReadOnlyList<ProcurementWishlistRule> WishlistRules { get; }
    IReadOnlyList<AutoProcurementResult> LastResults { get; }
    void ConfigureBudget(int dailyBudget, int minimumReserve);
    void UpsertStockRule(AutoProcurementRule rule);
    void UpsertWishlistRule(ProcurementWishlistRule rule);
    void ProcessShopRefresh(
        int day,
        IReadOnlyList<FacilityShopOffer> dailyOffers,
        DailyFacilityShopRuntime shopRuntime);
    AutoProcurementSaveData Capture();
}

public sealed class AutoProcurementFinancialDependencies
{
    public AutoProcurementFinancialDependencies(
        IGameMoneyAccount money,
        IEmploymentContractRuntime employment,
        IPaidFacilityContractRuntime paidContracts)
    {
        Money = money ?? throw new ArgumentNullException(nameof(money));
        Employment = employment
            ?? throw new ArgumentNullException(nameof(employment));
        PaidContracts = paidContracts
            ?? throw new ArgumentNullException(nameof(paidContracts));
    }

    internal IGameMoneyAccount Money { get; }
    internal IEmploymentContractRuntime Employment { get; }
    internal IPaidFacilityContractRuntime PaidContracts { get; }
}

public sealed class AutoProcurementStockDependencies
{
    public AutoProcurementStockDependencies(
        IWorldItemStackRuntime itemStacks,
        IStockCategoryDefinitionCatalog stockCategoryCatalog)
    {
        ItemStacks = itemStacks
            ?? throw new ArgumentNullException(nameof(itemStacks));
        StockCategoryCatalog = stockCategoryCatalog
            ?? throw new ArgumentNullException(nameof(stockCategoryCatalog));
    }

    internal IWorldItemStackRuntime ItemStacks { get; }
    internal IStockCategoryDefinitionCatalog StockCategoryCatalog { get; }
}

public sealed class AutoProcurementRuntime : IAutoProcurementRuntime
{
    private const int MaxProcessedOfferKeys = 256;

    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly AutoProcurementFinancialDependencies finance;
    private readonly IRunVariableRuntimeReader runVariables;
    private readonly AutoProcurementStockDependencies stock;
    private readonly TreasuryEconomyAggregateStateStore stateStore;
    private readonly IDomainFactPublisher domainFacts;
    private readonly IGameSessionStateStore gameSessionState;
    private IFacilityShopFulfillmentQuery fulfillmentQuery;
    private int activeProcessDurableBoundaryCount;
    private bool processingRefresh;

    private List<AutoProcurementRule> stockRules => stateStore.Current.StockRules;
    private List<ProcurementWishlistRule> wishlistRules => stateStore.Current.WishlistRules;
    private List<AutoProcurementResult> lastResults => stateStore.Current.ProcurementResults;
    private List<AutoProcurementParentSaveData> parentResults =>
        stateStore.Current.ProcurementParents;
    private HashSet<string> processedOfferKeys => stateStore.Current.ProcessedOfferKeys;
    private int dailyBudget
    {
        get => stateStore.Current.DailyBudget;
        set => stateStore.Current.DailyBudget = value;
    }
    private int minimumReserve
    {
        get => stateStore.Current.MinimumReserve;
        set => stateStore.Current.MinimumReserve = value;
    }
    private int lastProcessedDay
    {
        get => stateStore.Current.LastProcessedDay;
        set => stateStore.Current.LastProcessedDay = value;
    }

    public AutoProcurementRuntime(
        IGameSessionStateProvider gameDataProvider,
        AutoProcurementFinancialDependencies finance,
        IRunVariableRuntimeReader runVariables,
        AutoProcurementStockDependencies stock,
        TreasuryEconomyAggregateStateStore stateStore)
        : this(
            gameDataProvider,
            finance,
            runVariables,
            stock,
            stateStore,
            domainFacts: null,
            gameSessionState: null)
    {
    }

    [Inject]
    public AutoProcurementRuntime(
        IGameSessionStateProvider gameDataProvider,
        AutoProcurementFinancialDependencies finance,
        IRunVariableRuntimeReader runVariables,
        AutoProcurementStockDependencies stock,
        TreasuryEconomyAggregateStateStore stateStore,
        IDomainFactPublisher domainFacts,
        IGameSessionStateStore gameSessionState)
    {
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.finance = finance
            ?? throw new ArgumentNullException(nameof(finance));
        this.runVariables = runVariables
            ?? throw new ArgumentNullException(nameof(runVariables));
        this.stock = stock
            ?? throw new ArgumentNullException(nameof(stock));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.domainFacts = domainFacts;
        this.gameSessionState = gameSessionState;
    }

    public int DailyBudget => dailyBudget;
    public int MinimumReserve => minimumReserve;
    public int ProtectedFunds => Mathf.Max(
        minimumReserve,
        finance.Employment.ForecastCost(3)
            + finance.PaidContracts.ForecastCost(3));
    public IReadOnlyList<AutoProcurementRule> StockRules => stockRules;
    public IReadOnlyList<ProcurementWishlistRule> WishlistRules => wishlistRules;
    public IReadOnlyList<AutoProcurementResult> LastResults => lastResults;

    [Inject]
    public void ConstructFulfillmentQuery(
        ProgressionSceneRuntimeReferences progressionRuntimes)
    {
        fulfillmentQuery = progressionRuntimes?.BlueprintResearch
            ?? throw new ArgumentNullException(nameof(progressionRuntimes));
    }

    public void SetFulfillmentQuery(IFacilityShopFulfillmentQuery query) =>
        fulfillmentQuery = query
            ?? throw new ArgumentNullException(nameof(query));

    public void ConfigureBudget(int dailyBudget, int minimumReserve)
    {
        this.dailyBudget = Mathf.Max(0, dailyBudget);
        this.minimumReserve = Mathf.Max(0, minimumReserve);
    }

    public void UpsertStockRule(AutoProcurementRule rule)
    {
        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule));
        }

        AutoProcurementRule normalized = rule.Clone();
        normalized.ruleId = NormalizeRuleId(
            normalized.ruleId,
            $"stock:{normalized.category}");
        normalized.targetQuantity = Mathf.Max(0, normalized.targetQuantity);
        normalized.maximumUnitPrice = Mathf.Max(0, normalized.maximumUnitPrice);
        normalized.dailyMaximumQuantity =
            Mathf.Max(0, normalized.dailyMaximumQuantity);
        ReplaceById(stockRules, normalized.ruleId, normalized);
    }

    public void UpsertWishlistRule(ProcurementWishlistRule rule)
    {
        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule));
        }

        ProcurementWishlistRule normalized = rule.Clone();
        normalized.ruleId = NormalizeRuleId(
            normalized.ruleId,
            $"wishlist:{normalized.offerTypeId}:{normalized.dataId}");
        normalized.maximumPrice = Mathf.Max(0, normalized.maximumPrice);
        normalized.maximumOwned = Mathf.Max(0, normalized.maximumOwned);
        ReplaceById(wishlistRules, normalized.ruleId, normalized);
    }

    public void ProcessShopRefresh(
        int day,
        IReadOnlyList<FacilityShopOffer> dailyOffers,
        DailyFacilityShopRuntime shopRuntime)
    {
        if (processingRefresh)
            throw new InvalidOperationException("Auto-procurement refresh is already active.");
        processingRefresh = true;
        try { ProcessShopRefreshCore(day, dailyOffers, shopRuntime); }
        finally { processingRefresh = false; }
    }

    private void ProcessShopRefreshCore(
        int day,
        IReadOnlyList<FacilityShopOffer> dailyOffers,
        DailyFacilityShopRuntime shopRuntime)
    {
        int refreshDay = Mathf.Max(1, day);
        if (domainFacts == null || gameSessionState == null)
        {
            throw new InvalidOperationException(
                $"{nameof(AutoProcurementRuntime)} requires "
                + $"{nameof(IDomainFactPublisher)} and "
                + $"{nameof(IGameSessionStateStore)} before processing results.");
        }
        RetryPendingWishlistParents();
        if (refreshDay <= lastProcessedDay)
        {
            return;
        }

        TreasuryEconomyAggregateState processTreasuryBefore =
            stateStore.Current.Copy();
        GameSessionSnapshot processSessionBefore = CaptureSessionSnapshot();
        DungeonPhysicalItemSaveData processPhysicalBefore =
            stock.ItemStacks.Capture();
        activeProcessDurableBoundaryCount = 0;
        using var employmentEffects = finance.Employment.BeginSettlement()
            ?? throw new InvalidOperationException("Employment settlement scope is required.");
        bool processCompleted = false;
        try
        {
            if (refreshDay > 1)
            {
                int settledDay = refreshDay - 1;
                finance.Employment.SettleDay(settledDay);
                finance.PaidContracts.SettleDay(settledDay);
            }

            lastProcessedDay = refreshDay;
            lastResults.Clear();
            int availableBudget = Mathf.Min(
                dailyBudget,
                Mathf.Max(0, finance.Money.Balance - ProtectedFunds));
            if (availableBudget <= 0)
            {
                AddSkipped(refreshDay, "budget", "자동 구매", "보호 자금을 제외한 구매 가능액이 없습니다.");
                processCompleted = true;
                return;
            }

            ProcessStockRules(refreshDay, ref availableBudget);
            ProcessWishlist(
                refreshDay,
                dailyOffers ?? Array.Empty<FacilityShopOffer>(),
                shopRuntime,
                ref availableBudget);
            TrimProcessedKeys();
            processCompleted = true;
        }
        catch
        {
            if (activeProcessDurableBoundaryCount == 0)
            {
                RestorePurchaseOwners(
                    processTreasuryBefore,
                    processSessionBefore,
                    processPhysicalBefore);
            }
            throw;
        }
        finally
        {
            // A committed procurement child cannot be rolled back. Employment
            // consequences follow that same durable boundary even on later failure.
            if (processCompleted || activeProcessDurableBoundaryCount > 0)
                employmentEffects.Commit();
            activeProcessDurableBoundaryCount = 0;
        }
    }

    public AutoProcurementSaveData Capture()
    {
        return new AutoProcurementSaveData
        {
            dailyBudget = dailyBudget,
            minimumReserve = minimumReserve,
            lastProcessedDay = lastProcessedDay,
            stockRules = stockRules.Select(rule => rule.Clone()).ToList(),
            wishlistRules = wishlistRules.Select(rule => rule.Clone()).ToList(),
            processedOfferKeys = processedOfferKeys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList(),
            lastResults = lastResults.Select(CloneResult).ToList(),
            parentResults = parentResults.Select(value => value.Clone()).ToList(),
            pendingFacts = stateStore.Current.ProcurementPendingFact == null
                ? new List<AutoProcurementPendingFactSaveData>()
                : new List<AutoProcurementPendingFactSaveData> { stateStore.Current.ProcurementPendingFact.Clone() }
        };
    }

    internal void PopulateRestoreState(
        TreasuryEconomyAggregateState target,
        AutoProcurementSaveData saveData)
    {
        target = target ?? throw new ArgumentNullException(nameof(target));
        target.StockRules.Clear();
        target.WishlistRules.Clear();
        target.ProcurementResults.Clear();
        target.ProcurementParents.Clear();
        if (saveData?.pendingFacts == null || saveData.pendingFacts.Count > 1)
            throw new InvalidOperationException("Procurement pending fact collection is missing or unbounded.");
        target.ProcurementPendingFact = saveData.pendingFacts.Count == 0 ? null : saveData.pendingFacts[0].Clone();
        target.ProcessedOfferKeys.Clear();
        target.DailyBudget = Mathf.Max(0, saveData?.dailyBudget ?? 500);
        target.MinimumReserve = Mathf.Max(0, saveData?.minimumReserve ?? 0);
        target.LastProcessedDay = Mathf.Max(0, saveData?.lastProcessedDay ?? 0);
        foreach (AutoProcurementRule rule in saveData?.stockRules
                     ?? new List<AutoProcurementRule>())
        {
            if (rule == null)
                continue;
            AutoProcurementRule normalized = rule.Clone();
            normalized.ruleId = NormalizeRuleId(
                normalized.ruleId,
                $"stock:{normalized.category}");
            normalized.targetQuantity = Mathf.Max(0, normalized.targetQuantity);
            normalized.maximumUnitPrice = Mathf.Max(0, normalized.maximumUnitPrice);
            normalized.dailyMaximumQuantity = Mathf.Max(0, normalized.dailyMaximumQuantity);
            ReplaceById(target.StockRules, normalized.ruleId, normalized);
        }

        foreach (ProcurementWishlistRule rule in saveData?.wishlistRules
                     ?? new List<ProcurementWishlistRule>())
        {
            if (rule == null)
                continue;
            ProcurementWishlistRule normalized = rule.Clone();
            normalized.ruleId = NormalizeRuleId(
                normalized.ruleId,
                $"wishlist:{normalized.offerTypeId}:{normalized.dataId}");
            normalized.maximumPrice = Mathf.Max(0, normalized.maximumPrice);
            normalized.maximumOwned = Mathf.Max(0, normalized.maximumOwned);
            ReplaceById(target.WishlistRules, normalized.ruleId, normalized);
        }

        foreach (string key in saveData?.processedOfferKeys
                     ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                target.ProcessedOfferKeys.Add(key.Trim());
            }
        }

        target.ProcurementResults.AddRange((saveData?.lastResults
            ?? new List<AutoProcurementResult>()).Select(CloneResult));
        target.ProcurementParents.AddRange((saveData?.parentResults
            ?? new List<AutoProcurementParentSaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone()));
        TrimProcessedKeys(target.ProcessedOfferKeys);
    }

    private void ProcessStockRules(int day, ref int availableBudget)
    {
        IReadOnlyList<StockDeliveryOffer> offers =
            StockSupplyService.CreateDailyDeliveryOffers(
                day,
                runVariables,
                stock.StockCategoryCatalog);
        foreach (AutoProcurementRule rule in stockRules
                     .Where(candidate => candidate.enabled)
                     .OrderByDescending(candidate => candidate.priority)
                     .ThenBy(candidate => candidate.ruleId, StringComparer.Ordinal))
        {
            StockDeliveryOffer? offer = offers
                .Where(candidate => candidate.category == rule.category)
                .Cast<StockDeliveryOffer?>()
                .FirstOrDefault();
            if (!offer.HasValue)
            {
                AddSkipped(day, rule.ruleId, rule.category.ToString(), "오늘 시장에 상품이 없습니다.");
                continue;
            }

            StockDeliveryOffer marketOffer = offer.Value;
            int owned = CountOwned(rule.category);
            int needed = Mathf.Max(0, rule.targetQuantity - owned);
            int quantity = Mathf.Min(
                needed,
                rule.dailyMaximumQuantity > 0
                    ? rule.dailyMaximumQuantity
                    : needed);
            quantity = Mathf.Min(quantity, marketOffer.amount);
            if (quantity <= 0)
            {
                AddSkipped(day, rule.ruleId, rule.category.ToString(), "목표 재고를 이미 확보했습니다.");
                continue;
            }

            float unitPrice =
                marketOffer.cost / (float)Mathf.Max(1, marketOffer.amount);
            if (rule.maximumUnitPrice > 0 && unitPrice > rule.maximumUnitPrice)
            {
                AddSkipped(day, rule.ruleId, rule.category.ToString(), "최대 단가를 초과했습니다.");
                continue;
            }

            int cost = Mathf.CeilToInt(unitPrice * quantity);
            if (cost > availableBudget)
            {
                quantity = Mathf.Min(
                    quantity,
                    Mathf.FloorToInt(availableBudget / Mathf.Max(0.01f, unitPrice)));
                cost = Mathf.CeilToInt(unitPrice * quantity);
            }

            if (quantity <= 0)
            {
                AddSkipped(day, rule.ruleId, rule.category.ToString(), "오늘 자동 구매 예산이 부족합니다.");
                continue;
            }

            string offerKey = $"stock:{day}:{rule.category}";
            if (processedOfferKeys.Contains(offerKey))
            {
                AddSkipped(day, rule.ruleId, rule.category.ToString(), "오늘 이미 처리한 상품입니다.");
                continue;
            }

            AutoProcurementResult purchased = new AutoProcurementResult
            {
                day = day,
                ruleId = rule.ruleId,
                itemLabel = marketOffer.itemId,
                quantity = quantity,
                cost = cost,
                purchased = true,
                reason = "하차장 배송 완료",
                attemptedItemId = marketOffer.itemId,
                attemptedQuantity = quantity,
                attemptedCost = cost,
                committedItemId = marketOffer.itemId,
                committedQuantity = quantity,
                committedCost = cost
            };
            DomainFactLease prepared = ReserveCompletionFact(
                purchased,
                offerKey);
            int durableBefore = activeProcessDurableBoundaryCount;
            TreasuryEconomyAggregateState treasuryBefore;
            GameSessionSnapshot sessionBefore;
            DungeonPhysicalItemSaveData physicalBefore;
            Dictionary<string, int> stackQuantitiesBefore;
            try
            {
                treasuryBefore = stateStore.Current.Copy();
                sessionBefore = CaptureSessionSnapshot();
                physicalBefore = stock.ItemStacks.Capture();
                stackQuantitiesBefore = stock.ItemStacks.GetAllStacks()
                    .Where(value => value != null
                        && !string.IsNullOrWhiteSpace(value.StackId))
                    .ToDictionary(value => value.StackId,
                        value => value.Quantity,
                        StringComparer.Ordinal);
            }
            catch
            {
                domainFacts.Cancel(prepared);
                throw;
            }
            try
            {
                processedOfferKeys.Add(offerKey);
                if (!finance.Money.TrySpend(
                        cost,
                        new EconomyTransactionContext(
                            EconomyTransactionKind.AutoProcurement,
                            rule.ruleId,
                            rule.category.ToString(),
                            "자동 원자재 구매"),
                        out string failureReason))
                {
                    domainFacts.Cancel(prepared);
                    RestorePurchaseOwners(
                        treasuryBefore,
                        sessionBefore,
                        physicalBefore);
                    RecordProcessedFailure(
                        offerKey,
                        new AutoProcurementResult
                        {
                            day = day,
                            ruleId = rule.ruleId,
                            itemLabel = marketOffer.itemId,
                            purchased = false,
                            reason = failureReason,
                            attemptedItemId = marketOffer.itemId,
                            attemptedQuantity = quantity,
                            attemptedCost = cost
                        });
                    continue;
                }

                if (!stock.ItemStacks.SpawnItemAtDropoff(
                        marketOffer.itemId,
                        quantity,
                        "자동 구매",
                        out int spawned)
                    || spawned != quantity)
                {
                    domainFacts.Cancel(prepared);
                    RestorePurchaseOwners(
                        treasuryBefore,
                        sessionBefore,
                        physicalBefore);
                    RecordProcessedFailure(
                        offerKey,
                        new AutoProcurementResult
                        {
                            day = day,
                            ruleId = rule.ruleId,
                            itemLabel = marketOffer.itemId,
                            purchased = false,
                            reason = "하차장에 배송할 수 없어 구매 상태를 복원했습니다.",
                            attemptedItemId = marketOffer.itemId,
                            attemptedQuantity = quantity,
                            attemptedCost = cost
                        });
                    continue;
                }

                purchased.physicalStacks = CapturePhysicalSpawnRefs(
                    marketOffer.itemId,
                    stackQuantitiesBefore);
                if (purchased.physicalStacks.Sum(value => value.quantity)
                    != quantity)
                {
                    domainFacts.Cancel(prepared);
                    RestorePurchaseOwners(
                        treasuryBefore, sessionBefore, physicalBefore);
                    RecordProcessedFailure(offerKey, new AutoProcurementResult
                    {
                        day = day,
                        ruleId = rule.ruleId,
                        itemLabel = marketOffer.itemId,
                        attemptedItemId = marketOffer.itemId,
                        attemptedQuantity = quantity,
                        attemptedCost = cost,
                        reason = "실제 배송 stack 수량이 구매 수량과 일치하지 않습니다."
                    });
                    continue;
                }

                PublishCompletionFact(prepared, purchased, offerKey);
                availableBudget -= cost;
            }
            catch
            {
                domainFacts.Cancel(prepared);
                if (activeProcessDurableBoundaryCount == durableBefore)
                    RestorePurchaseOwners(treasuryBefore, sessionBefore, physicalBefore);
                throw;
            }
        }
    }

    private void ProcessWishlist(
        int day,
        IReadOnlyList<FacilityShopOffer> offers,
        DailyFacilityShopRuntime shopRuntime,
        ref int availableBudget)
    {
        if (shopRuntime == null
            || !gameDataProvider.TryGetSessionState(out GameSessionState gameData))
        {
            return;
        }

        foreach (ProcurementWishlistRule rule in wishlistRules
                     .Where(candidate => candidate.enabled)
                     .OrderByDescending(candidate => candidate.priority)
                     .ThenBy(candidate => candidate.ruleId, StringComparer.Ordinal))
        {
            int index = FindWishlistOfferIndex(rule, offers);
            if (index < 0)
            {
                AddSkipped(day, rule.ruleId, "찜 상품", "오늘 갱신 목록에 없습니다.");
                continue;
            }

            FacilityShopOffer offer = offers[index];
            if (rule.maximumPrice > 0 && offer.Cost > rule.maximumPrice)
            {
                AddSkipped(day, rule.ruleId, offer.DisplayName, "최대 가격을 초과했습니다.");
                continue;
            }

            if (offer.Cost > availableBudget)
            {
                AddSkipped(day, rule.ruleId, offer.DisplayName, "오늘 자동 구매 예산이 부족합니다.");
                continue;
            }

            if (CountOwned(offer, shopRuntime) >= rule.maximumOwned)
            {
                AddSkipped(day, rule.ruleId, offer.DisplayName, "최대 보유 수량에 도달했습니다.");
                continue;
            }

            string offerKey = $"facility:{day}:{offer.OfferTypeId}:{offer.DataId}";
            if (!processedOfferKeys.Add(offerKey))
            {
                AddSkipped(day, rule.ruleId, offer.DisplayName, "오늘 이미 처리한 상품입니다.");
                continue;
            }

            EconomyTransactionContext transactionContext =
                new EconomyTransactionContext(
                    EconomyTransactionKind.AutoProcurement,
                    rule.ruleId,
                    $"{offer.OfferTypeId}:{offer.DataId}",
                    "찜 상품 자동 구매");
            if (!shopRuntime.TryPurchaseDailyOffer(
                    index,
                    gameData,
                    transactionContext,
                    out FacilityShopPurchaseResult purchase))
            {
                if (!string.IsNullOrWhiteSpace(purchase.message)
                    && purchase.message.StartsWith(
                        "시설 상점 결과",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Auto-procurement facility child transaction failed: "
                        + purchase.message);
                }
                RecordResult(new AutoProcurementResult
                {
                    day = day,
                    ruleId = rule.ruleId,
                    itemLabel = offer.DisplayName,
                    attemptedItemId = PhysicalItemIdForOffer(offer),
                    attemptedQuantity = 1,
                    attemptedCost = offer.Cost,
                    reason = purchase.message
                });
                continue;
            }

            activeProcessDurableBoundaryCount = checked(
                activeProcessDurableBoundaryCount + 1);
            availableBudget -= purchase.cost;
            if (string.IsNullOrWhiteSpace(purchase.purchasePublicationKey))
                throw new InvalidOperationException(
                    "Wishlist child purchase has no durable result key.");
            AutoProcurementParentSaveData parent = new()
            {
                offerKey = offerKey,
                childPurchaseResultKey = purchase.purchasePublicationKey,
                phase = AutoProcurementParentPhase.ChildCommitted,
                result = new AutoProcurementResult
                {
                    day = day,
                    ruleId = rule.ruleId,
                    itemLabel = offer.DisplayName,
                    quantity = 1,
                    cost = purchase.cost,
                    purchased = true,
                    reason = "하차장 배송 대기",
                    attemptedItemId = PhysicalItemIdForOffer(offer),
                    attemptedQuantity = 1,
                    attemptedCost = offer.Cost,
                    committedQuantity = 1,
                    committedCost = purchase.cost,
                    childPurchaseResultKey = purchase.purchasePublicationKey
                }
            };
            parent.result.committedItemId = parent.result.attemptedItemId;
            parentResults.Add(parent);
            TryCommitWishlistParent(parent);
        }
    }

    public int RetryPendingWishlistParents()
    {
        ResumePendingCompletionFact();
        int committed = 0;
        foreach (AutoProcurementParentSaveData parent in parentResults
                     .Where(value => value != null
                         && value.phase != AutoProcurementParentPhase.ParentCommitted)
                     .OrderBy(value => value.offerKey, StringComparer.Ordinal)
                     .ToArray())
        {
            if (TryCommitWishlistParent(parent))
                committed++;
        }
        return committed;
    }

    private bool TryCommitWishlistParent(AutoProcurementParentSaveData parent)
    {
        if (parent == null
            || parent.phase == AutoProcurementParentPhase.ParentCommitted)
            return false;
        if (fulfillmentQuery == null
            || !fulfillmentQuery.TryGetShopFulfillment(
                parent.childPurchaseResultKey, out var delivery)
            || delivery.Phase != FacilityShopFulfillmentPhase.Delivered)
            return false;
        if (delivery.ItemDefinitionId != parent.result.committedItemId
            || string.IsNullOrWhiteSpace(delivery.StackId)
            || string.IsNullOrWhiteSpace(delivery.ItemInstanceId))
            throw new InvalidOperationException(
                "Wishlist child delivery does not match its committed purchase.");
        parent.result.physicalStacks = new List<AutoProcurementPhysicalStackRef>
        {
            new AutoProcurementPhysicalStackRef
            {
                stackId = delivery.StackId,
                itemInstanceId = delivery.ItemInstanceId,
                quantity = 1
            }
        };
        parent.result.reason = "하차장 배송 완료";
        parent.phase = AutoProcurementParentPhase.ParentPending;
        RecordResult(parent.result, parent.offerKey);
        parent.phase = AutoProcurementParentPhase.ParentCommitted;
        return true;
    }

    private int CountOwned(StockCategory category)
    {
        return stock.ItemStacks.GetAllStacks()
            .Where(stack => stack != null && stack.StockCategory == category)
            .Sum(stack => Mathf.Max(0, stack.Quantity));
    }

    private static string PhysicalItemIdForOffer(FacilityShopOffer offer) =>
        offer switch
        {
            FacilityBuildingOffer building =>
                FacilityInstallationKitItemIds.ForBuilding(building.Building),
            FacilityBlueprintOffer blueprint => blueprint.Blueprint.PhysicalItemId,
            _ => throw new InvalidOperationException(
                "Wishlist offer has no physical item definition.")
        };

    private List<AutoProcurementPhysicalStackRef> CapturePhysicalSpawnRefs(
        string itemId,
        IReadOnlyDictionary<string, int> before)
    {
        return stock.ItemStacks.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.ItemId, itemId,
                    StringComparison.Ordinal))
            .Select(value => new AutoProcurementPhysicalStackRef
            {
                stackId = value.StackId,
                itemInstanceId = value.ItemInstanceId ?? string.Empty,
                quantity = value.Quantity - (before.TryGetValue(
                    value.StackId, out int previous) ? previous : 0)
            })
            .Where(value => value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToList();
    }

    private int CountOwned(
        FacilityShopOffer offer,
        DailyFacilityShopRuntime shopRuntime)
    {
        if (offer is FacilityBlueprintOffer blueprintOffer)
        {
            string itemId = blueprintOffer.Blueprint?.PhysicalItemId
                ?? string.Empty;
            return stock.ItemStacks.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
                .Sum(stack => Mathf.Max(0, stack.Quantity));
        }

        if (offer is FacilityBuildingOffer buildingOffer)
        {
            string itemId = FacilityInstallationKitItemIds.ForBuilding(
                buildingOffer.Building);
            return stock.ItemStacks.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                .Sum(stack => Mathf.Max(0, stack.Quantity));
        }

        return 0;
    }

    private static int FindWishlistOfferIndex(
        ProcurementWishlistRule rule,
        IReadOnlyList<FacilityShopOffer> offers)
    {
        for (int index = 0; index < offers.Count; index++)
        {
            FacilityShopOffer offer = offers[index];
            if (offer == null
                || (!string.IsNullOrWhiteSpace(rule.offerTypeId)
                    && !string.Equals(
                        offer.OfferTypeId,
                        rule.offerTypeId,
                        StringComparison.Ordinal))
                || (rule.dataId >= 0 && offer.DataId != rule.dataId)
                || (!string.IsNullOrWhiteSpace(rule.requiredTag)
                    && offer.DisplayName.IndexOf(
                        rule.requiredTag,
                        StringComparison.OrdinalIgnoreCase) < 0))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private void AddSkipped(
        int day,
        string ruleId,
        string label,
        string reason)
    {
        RecordResult(new AutoProcurementResult
        {
            day = day,
            ruleId = ruleId ?? string.Empty,
            itemLabel = label ?? string.Empty,
            purchased = false,
            reason = reason ?? string.Empty
        });
    }

    private void RecordProcessedFailure(string offerKey, AutoProcurementResult result)
    {
        TreasuryEconomyAggregateState before = stateStore.Current.Copy();
        DomainFactLease lease = ReserveCompletionFact(result, offerKey);
        int durableBefore = activeProcessDurableBoundaryCount;
        try
        {
            processedOfferKeys.Add(offerKey);
            PublishCompletionFact(lease, result, offerKey);
        }
        catch
        {
            domainFacts.Cancel(lease);
            if (activeProcessDurableBoundaryCount == durableBefore)
                stateStore.Replace(before);
            throw;
        }
    }

    private void RecordResult(AutoProcurementResult result, string offerKey = null)
    {
        DomainFactLease lease = ReserveCompletionFact(result, offerKey);
        int insertIndex = lastResults.Count;
        int durableBefore = activeProcessDurableBoundaryCount;
        try { PublishCompletionFact(lease, result, offerKey); }
        catch
        {
            domainFacts.Cancel(lease);
            if (activeProcessDurableBoundaryCount == durableBefore && lastResults.Count > insertIndex)
                lastResults.RemoveAt(insertIndex);
            throw;
        }
    }

    private DomainFactLease ReserveCompletionFact(AutoProcurementResult result, string offerKey)
    {
        if (stateStore.Current.ProcurementPendingFact != null)
            throw new InvalidOperationException("Auto-procurement has an unresolved publication.");
        var fact = new AutoProcurementCompletedFact(offerKey, result);
        if (!domainFacts.TryReserve(fact, out var lease, out string failure))
            throw new InvalidOperationException("Auto-procurement fact reservation failed: " + failure);
        if (string.IsNullOrWhiteSpace(lease.PublicationKey))
        {
            domainFacts.Cancel(lease);
            throw new InvalidOperationException("Auto-procurement observer supplied no publication identity.");
        }
        return lease;
    }

    private void PublishCompletionFact(DomainFactLease lease, AutoProcurementResult result, string offerKey)
    {
        var fact = new AutoProcurementCompletedFact(offerKey, result);
        var pending = new AutoProcurementPendingFactSaveData
        {
            publicationKey = lease.PublicationKey,
            offerKey = fact.OfferKey,
            result = fact.CaptureResult()
        };
        if (string.IsNullOrWhiteSpace(pending.publicationKey))
            throw new InvalidOperationException("Auto-procurement publication reservation expired.");
        lastResults.Add(fact.CaptureResult());
        stateStore.Current.ProcurementPendingFact = pending;
        // An exception after invoking the observer cannot authorize refunding
        // money/removing delivered goods. Preserve the exact fact for recovery.
        activeProcessDurableBoundaryCount = checked(activeProcessDurableBoundaryCount + 1);
        var delivery = domainFacts.Publish(lease, fact);
        if (!delivery.MayBeCommitted)
        {
            activeProcessDurableBoundaryCount--;
            stateStore.Current.ProcurementPendingFact = null;
            throw new InvalidOperationException("Auto-procurement fact publication rejected: " + delivery.Detail);
        }
        if (!IsKnownPublication(delivery, pending.publicationKey))
            throw new InvalidOperationException("Auto-procurement fact publication pending: " + delivery.Detail);
        stateStore.Current.ProcurementPendingFact = null;
    }

    private void ResumePendingCompletionFact()
    {
        var pending = stateStore.Current.ProcurementPendingFact;
        if (pending == null) return;
        if (domainFacts is not IDomainFactRecoveryPublisher recovery)
            throw new InvalidOperationException("Auto-procurement fact recovery is unavailable.");
        var fact = new AutoProcurementCompletedFact(pending.offerKey, pending.result);
        var results = new DomainFactDelivery[1];
        recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey, fact) },
            results, out string failure);
        if (!IsKnownPublication(results[0], pending.publicationKey))
            throw new InvalidOperationException("Auto-procurement fact recovery pending: " + failure + ":" + results[0].Detail);
        if (!string.IsNullOrEmpty(fact.ChildPurchaseResultKey))
        {
            var parent = parentResults.SingleOrDefault(value => value.offerKey == pending.offerKey
                && value.childPurchaseResultKey == fact.ChildPurchaseResultKey);
            if (parent == null || parent.phase != AutoProcurementParentPhase.ParentPending)
                throw new InvalidOperationException("Auto-procurement recovered fact has no pending delivery parent.");
            parent.phase = AutoProcurementParentPhase.ParentCommitted;
        }
        stateStore.Current.ProcurementPendingFact = null;
    }

    private static bool IsKnownPublication(DomainFactDelivery result, string expectedKey) =>
        result.OperationKey == expectedKey && !string.IsNullOrWhiteSpace(expectedKey)
        && result.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery;

    private void RestorePurchaseOwners(
        TreasuryEconomyAggregateState treasuryBefore,
        GameSessionSnapshot sessionBefore,
        DungeonPhysicalItemSaveData physicalBefore)
    {
        stateStore.Replace(treasuryBefore
            ?? throw new ArgumentNullException(nameof(treasuryBefore)));
        gameSessionState.Restore(sessionBefore);
        stock.ItemStacks.Restore(physicalBefore
            ?? throw new ArgumentNullException(nameof(physicalBefore)));
    }

    private GameSessionSnapshot CaptureSessionSnapshot()
    {
        if (!gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            || gameData == null)
        {
            throw new InvalidOperationException(
                "Auto-procurement cannot capture the current session owner state.");
        }
        return gameData.Capture();
    }


    private void TrimProcessedKeys()
    {
        TrimProcessedKeys(processedOfferKeys);
    }

    private static void TrimProcessedKeys(HashSet<string> keys)
    {
        if (keys.Count <= MaxProcessedOfferKeys)
        {
            return;
        }

        string[] keep = keys
            .OrderByDescending(key => key, StringComparer.Ordinal)
            .Take(MaxProcessedOfferKeys)
            .ToArray();
        keys.Clear();
        foreach (string key in keep)
        {
            keys.Add(key);
        }
    }

    private static void ReplaceById<T>(
        IList<T> list,
        string id,
        T value)
    {
        for (int index = 0; index < list.Count; index++)
        {
            string existingId = list[index] switch
            {
                AutoProcurementRule stock => stock.ruleId,
                ProcurementWishlistRule wishlist => wishlist.ruleId,
                _ => string.Empty
            };
            if (string.Equals(existingId, id, StringComparison.Ordinal))
            {
                list[index] = value;
                return;
            }
        }

        list.Add(value);
    }

    private static string NormalizeRuleId(string value, string fallback)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0 ? normalized : fallback;
    }

    private static AutoProcurementResult CloneResult(
        AutoProcurementResult source)
    {
        return source == null
            ? new AutoProcurementResult()
            : source.Clone();
    }
}
