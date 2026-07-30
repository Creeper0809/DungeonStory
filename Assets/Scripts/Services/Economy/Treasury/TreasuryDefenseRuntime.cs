using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct TreasuryDefenseAuthorization
{
    public TreasuryDefenseAuthorization(
        bool authorized,
        bool malfunctioned,
        float effectMultiplier,
        int paidGold,
        string reason)
    {
        Authorized = authorized;
        Malfunctioned = malfunctioned;
        EffectMultiplier = Mathf.Max(0f, effectMultiplier);
        PaidGold = Mathf.Max(0, paidGold);
        Reason = reason ?? string.Empty;
    }

    public bool Authorized { get; }
    public bool Malfunctioned { get; }
    public float EffectMultiplier { get; }
    public int PaidGold { get; }
    public string Reason { get; }
}

public interface ITreasuryDefenseRuntime
{
    IReadOnlyList<TreasuryDefensePolicy> Policies { get; }
    TreasuryDefensePolicy GetPolicy(DefenseFacility facility);
    void UpsertPolicy(TreasuryDefensePolicy policy);
    TreasuryDefenseAuthorization AuthorizeShot(
        DefenseFacility facility,
        CharacterActor intruder,
        string invasionId,
        float threat,
        bool isBoss);
    int GetSpent(string invasionId, string facilityPersistentId);
    string GetLastFailureReason(string facilityPersistentId);
    TreasuryDefenseSaveData Capture();
    void Restore(TreasuryDefenseSaveData saveData);
}

public sealed class TreasuryDefenseRuntime : ITreasuryDefenseRuntime
{
    private const int MaxRememberedInvasions = 32;

    private readonly IGameMoneyRuntime money;
    private readonly IAutoProcurementRuntime procurement;
    private readonly IFacilityOverclockRuntime overclock;
    private readonly IFacilityEvolutionStateComponentFactory facilityStates;
    private readonly Dictionary<string, TreasuryDefensePolicy> policies =
        new Dictionary<string, TreasuryDefensePolicy>(StringComparer.Ordinal);
    private readonly Dictionary<string, TreasuryDefenseInvasionSpendState> spending =
        new Dictionary<string, TreasuryDefenseInvasionSpendState>(
            StringComparer.Ordinal);
    private readonly Dictionary<string, string> failureReasons =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public TreasuryDefenseRuntime(
        IGameMoneyRuntime money,
        IAutoProcurementRuntime procurement,
        IFacilityOverclockRuntime overclock,
        IFacilityEvolutionStateComponentFactory facilityStates)
    {
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.procurement = procurement
            ?? throw new ArgumentNullException(nameof(procurement));
        this.overclock = overclock
            ?? throw new ArgumentNullException(nameof(overclock));
        this.facilityStates = facilityStates
            ?? throw new ArgumentNullException(nameof(facilityStates));
    }

    public IReadOnlyList<TreasuryDefensePolicy> Policies => policies.Values
        .OrderBy(policy => policy.facilityPersistentId, StringComparer.Ordinal)
        .Select(policy => policy.Clone())
        .ToArray();

    public TreasuryDefensePolicy GetPolicy(DefenseFacility facility)
    {
        string facilityId = ResolveFacilityId(facility);
        if (policies.TryGetValue(facilityId, out TreasuryDefensePolicy policy))
        {
            return policy.Clone();
        }

        BuildingTreasuryPoweredDefenseAbility ability =
            GetAbility(facility);
        return new TreasuryDefensePolicy
        {
            facilityPersistentId = facilityId,
            automaticFire = true,
            bossOnly = ability?.defaultBossOnly ?? false,
            minimumThreat = Mathf.Max(
                0,
                ability?.defaultMinimumThreat ?? 0),
            invasionBudget = Mathf.Max(
                0,
                ability?.defaultInvasionBudget ?? 300),
            protectedFunds = -1
        };
    }

    public void UpsertPolicy(TreasuryDefensePolicy policy)
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }

        string facilityId = Normalize(policy.facilityPersistentId);
        if (facilityId.Length == 0)
        {
            throw new ArgumentException(
                "시설 영구 ID가 필요합니다.",
                nameof(policy));
        }

        TreasuryDefensePolicy normalized = policy.Clone();
        normalized.facilityPersistentId = facilityId;
        normalized.minimumThreat = Mathf.Max(0, normalized.minimumThreat);
        normalized.invasionBudget = Mathf.Max(0, normalized.invasionBudget);
        normalized.protectedFunds = Mathf.Max(-1, normalized.protectedFunds);
        policies[facilityId] = normalized;
    }

    public TreasuryDefenseAuthorization AuthorizeShot(
        DefenseFacility facility,
        CharacterActor intruder,
        string invasionId,
        float threat,
        bool isBoss)
    {
        BuildingTreasuryPoweredDefenseAbility ability = GetAbility(facility);
        if (ability == null || !ability.IsValid)
        {
            return new TreasuryDefenseAuthorization(
                true,
                false,
                1f,
                0,
                string.Empty);
        }

        string facilityId = ResolveFacilityId(facility);
        TreasuryDefensePolicy policy = GetPolicy(facility);
        int shotCost = Mathf.Max(1, ability.shotCost);
        string normalizedInvasionId = Normalize(invasionId);
        if (normalizedInvasionId.Length == 0)
        {
            normalizedInvasionId = "invasion:unknown";
        }

        string reason = Validate(
            policy,
            normalizedInvasionId,
            facilityId,
            shotCost,
            threat,
            isBoss);
        if (reason.Length > 0)
        {
            failureReasons[facilityId] = reason;
            return new TreasuryDefenseAuthorization(
                false,
                false,
                0f,
                0,
                reason);
        }

        if (!money.TrySpend(
                shotCost,
                new EconomyTransactionContext(
                    EconomyTransactionKind.TreasuryDefenseShot,
                    facilityId,
                    intruder?.Identity?.PersistentId ?? string.Empty,
                    "금고 연동 방어 발사"),
                out reason))
        {
            failureReasons[facilityId] = reason;
            return new TreasuryDefenseAuthorization(
                false,
                false,
                0f,
                0,
                reason);
        }

        string spendKey = SpendKey(normalizedInvasionId, facilityId);
        if (!spending.TryGetValue(
                spendKey,
                out TreasuryDefenseInvasionSpendState spendState))
        {
            spendState = new TreasuryDefenseInvasionSpendState
            {
                invasionId = normalizedInvasionId,
                facilityPersistentId = facilityId
            };
            spending.Add(spendKey, spendState);
        }

        spendState.spent += shotCost;
        failureReasons.Remove(facilityId);
        PruneSpending();

        bool malfunctioned =
            overclock.TryRollFacilityActionMalfunction(facilityId);
        return new TreasuryDefenseAuthorization(
            true,
            malfunctioned,
            malfunctioned
                ? 0f
                : overclock.GetFacilityPerformanceMultiplier(facilityId),
            shotCost,
            malfunctioned ? "과부하로 발사가 빗나갔습니다." : string.Empty);
    }

    public int GetSpent(string invasionId, string facilityPersistentId)
    {
        return spending.TryGetValue(
                SpendKey(invasionId, facilityPersistentId),
                out TreasuryDefenseInvasionSpendState state)
            ? Mathf.Max(0, state.spent)
            : 0;
    }

    public string GetLastFailureReason(string facilityPersistentId)
    {
        return failureReasons.TryGetValue(
                Normalize(facilityPersistentId),
                out string reason)
            ? reason
            : string.Empty;
    }

    public TreasuryDefenseSaveData Capture()
    {
        return new TreasuryDefenseSaveData
        {
            policies = policies.Values
                .OrderBy(policy => policy.facilityPersistentId, StringComparer.Ordinal)
                .Select(policy => policy.Clone())
                .ToList(),
            invasionSpending = spending.Values
                .OrderBy(state => state.invasionId, StringComparer.Ordinal)
                .ThenBy(state => state.facilityPersistentId, StringComparer.Ordinal)
                .Select(state => state.Clone())
                .ToList()
        };
    }

    public void Restore(TreasuryDefenseSaveData saveData)
    {
        policies.Clear();
        spending.Clear();
        failureReasons.Clear();

        foreach (TreasuryDefensePolicy source in saveData?.policies
                     ?? new List<TreasuryDefensePolicy>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.facilityPersistentId))
            {
                continue;
            }

            UpsertPolicy(source);
        }

        foreach (TreasuryDefenseInvasionSpendState source in
                 saveData?.invasionSpending
                 ?? new List<TreasuryDefenseInvasionSpendState>())
        {
            string invasionId = Normalize(source?.invasionId);
            string facilityId = Normalize(source?.facilityPersistentId);
            if (invasionId.Length == 0 || facilityId.Length == 0)
            {
                continue;
            }

            TreasuryDefenseInvasionSpendState state = source.Clone();
            state.invasionId = invasionId;
            state.facilityPersistentId = facilityId;
            state.spent = Mathf.Max(0, state.spent);
            spending[SpendKey(invasionId, facilityId)] = state;
        }

        PruneSpending();
    }

    private string Validate(
        TreasuryDefensePolicy policy,
        string invasionId,
        string facilityId,
        int shotCost,
        float threat,
        bool isBoss)
    {
        if (!policy.automaticFire)
        {
            return "자동 발사가 꺼져 있습니다.";
        }

        if (policy.bossOnly && !isBoss)
        {
            return "보스 침공에서만 발사합니다.";
        }

        if (threat < policy.minimumThreat)
        {
            return $"최소 위협도 {policy.minimumThreat} 미만입니다.";
        }

        int spent = GetSpent(invasionId, facilityId);
        if (policy.invasionBudget <= 0
            || spent + shotCost > policy.invasionBudget)
        {
            return "침공당 금고 사용 예산을 모두 썼습니다.";
        }

        int reserve = policy.protectedFunds >= 0
            ? policy.protectedFunds
            : procurement.ProtectedFunds;
        if (money.Balance - shotCost < reserve)
        {
            return "금고 보호액 때문에 발사 중지";
        }

        return string.Empty;
    }

    private BuildingTreasuryPoweredDefenseAbility GetAbility(
        DefenseFacility facility)
    {
        return facility?.BuildingData?
            .GetAbility<BuildingTreasuryPoweredDefenseAbility>();
    }

    private string ResolveFacilityId(DefenseFacility facility)
    {
        if (facility == null)
        {
            return string.Empty;
        }

        FacilityEvolutionStateComponent state = facilityStates.GetOrAdd(facility);
        state.InitializeIfNeeded(facility);
        return Normalize(state.FacilityPersistentId);
    }

    private void PruneSpending()
    {
        string[] retainedInvasions = spending.Values
            .Select(state => state.invasionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(id => id, StringComparer.Ordinal)
            .Take(MaxRememberedInvasions)
            .ToArray();
        HashSet<string> retained =
            new HashSet<string>(retainedInvasions, StringComparer.Ordinal);
        foreach (string key in spending
                     .Where(pair => !retained.Contains(pair.Value.invasionId))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            spending.Remove(key);
        }
    }

    private static string SpendKey(string invasionId, string facilityId)
    {
        return $"{Normalize(invasionId)}|{Normalize(facilityId)}";
    }

    private static string Normalize(string value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
