using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IEmploymentContractRuntime
{
    IReadOnlyList<EmployeeWageState> WageStates { get; }
    IReadOnlyList<MercenaryContract> MercenaryContracts { get; }
    int ForecastCost(int days);
    int GetDailyCost(string characterId);
    int QuoteMercenaryDailyCost(
        string characterId,
        int level,
        int rolePremium);
    EmploymentDailySettlement SettleDay(int day);
    bool TryHireMercenary(
        CharacterActor actor,
        int rolePremium,
        int day,
        out string failureReason);
    bool SetEmployeeRolePremium(
        string characterId,
        int premium,
        out string failureReason);
    EmploymentContractSaveData Capture();
    void Restore(EmploymentContractSaveData saveData);
}

public sealed class EmploymentContractRuntime : IEmploymentContractRuntime
{
    private const int EmployeeBaseWage = 30;
    private const int EmployeeLevelWage = 2;
    private const int MercenaryBaseWage = 60;
    private const int MercenaryLevelWage = 4;

    private readonly ICharacterWorldQuery characterWorld;
    private readonly ICombatEquipmentRuntime equipmentRuntime;
    private readonly IGameMoneyRuntime money;
    private readonly IGameEventBus eventBus;
    private readonly Dictionary<string, EmployeeWageState> wageByCharacterId =
        new Dictionary<string, EmployeeWageState>(StringComparer.Ordinal);
    private readonly Dictionary<string, MercenaryContract> mercenaryByCharacterId =
        new Dictionary<string, MercenaryContract>(StringComparer.Ordinal);

    public EmploymentContractRuntime(
        ICharacterWorldQuery characterWorld,
        ICombatEquipmentRuntime equipmentRuntime,
        IGameMoneyRuntime money,
        IGameEventBus eventBus)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.equipmentRuntime = equipmentRuntime
            ?? throw new ArgumentNullException(nameof(equipmentRuntime));
        this.money = money
            ?? throw new ArgumentNullException(nameof(money));
        this.eventBus = eventBus
            ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public IReadOnlyList<EmployeeWageState> WageStates =>
        wageByCharacterId.Values
            .OrderBy(state => state.characterId, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<MercenaryContract> MercenaryContracts =>
        mercenaryByCharacterId.Values
            .OrderBy(contract => contract.characterId, StringComparer.Ordinal)
            .ToArray();

    public int ForecastCost(int days)
    {
        EnsureCurrentStaff();
        int dayCount = Mathf.Max(0, days);
        if (dayCount == 0)
        {
            return 0;
        }

        int daily = wageByCharacterId.Values
            .Where(state => state.active
                && state.contractKind != EmploymentContractKind.Founder)
            .Sum(state => GetDailyCost(state.characterId));
        int arrears = wageByCharacterId.Values
            .Where(state => state.active
                && state.contractKind == EmploymentContractKind.Employee)
            .Sum(state => Mathf.Max(0, state.unpaidWages));
        return daily * dayCount + arrears;
    }

    public int GetDailyCost(string characterId)
    {
        string normalizedId = NormalizeId(characterId);
        if (!wageByCharacterId.TryGetValue(
                normalizedId,
                out EmployeeWageState state)
            || !state.active
            || state.contractKind == EmploymentContractKind.Founder)
        {
            return 0;
        }

        int level = FindActor(normalizedId)?.Progression?.Level ?? 1;
        if (state.contractKind == EmploymentContractKind.Mercenary)
        {
            state.equipmentGradePremium =
                CalculateEquipmentPremium(normalizedId);
            return MercenaryBaseWage
                + level * MercenaryLevelWage
                + Mathf.Max(0, state.equipmentGradePremium);
        }

        return EmployeeBaseWage
            + level * EmployeeLevelWage
            + Mathf.Max(0, state.rolePremium);
    }

    public int QuoteMercenaryDailyCost(
        string characterId,
        int level,
        int rolePremium)
    {
        return MercenaryBaseWage
            + Mathf.Max(1, level) * MercenaryLevelWage
            + Mathf.Max(0, rolePremium)
            + CalculateEquipmentPremium(NormalizeId(characterId));
    }

    public EmploymentDailySettlement SettleDay(int day)
    {
        EnsureCurrentStaff();
        int settlementDay = Mathf.Max(1, day);
        EmploymentDailySettlement result = new EmploymentDailySettlement
        {
            day = settlementDay
        };

        foreach (EmployeeWageState state in wageByCharacterId.Values
                     .Where(candidate => candidate.active
                         && candidate.contractKind
                            == EmploymentContractKind.Employee)
                     .OrderBy(
                         candidate => candidate.characterId,
                         StringComparer.Ordinal))
        {
            if (state.lastSettledDay >= settlementDay)
            {
                continue;
            }

            int dailyCost = GetDailyCost(state.characterId);
            int due = dailyCost + Mathf.Max(0, state.unpaidWages);
            result.employeeWagesDue += due;
            if (money.TrySpend(
                    due,
                    new EconomyTransactionContext(
                        EconomyTransactionKind.EmployeeWage,
                        "employment",
                        state.characterId,
                        "직원 임금"),
                    out _))
            {
                result.employeeWagesPaid += due;
                state.unpaidWages = 0;
            }
            else
            {
                state.unpaidWages = due;
                result.unpaidEmployeeWages += due;
                ApplyUnpaidWageMood(state.characterId);
            }

            state.lastSettledDay = settlementDay;
        }

        foreach (MercenaryContract contract in mercenaryByCharacterId.Values
                     .Where(candidate => candidate.active)
                     .OrderBy(
                         candidate => candidate.characterId,
                         StringComparer.Ordinal))
        {
            if (contract.lastRenewedDay >= settlementDay)
            {
                continue;
            }

            int due = GetDailyCost(contract.characterId);
            result.mercenaryFeesDue += due;
            if (money.TrySpend(
                    due,
                    new EconomyTransactionContext(
                        EconomyTransactionKind.MercenaryRenewal,
                        "mercenary-contract",
                        contract.characterId,
                        "용병 계약 갱신"),
                    out _))
            {
                result.mercenaryFeesPaid += due;
                contract.lastRenewedDay = settlementDay;
                if (wageByCharacterId.TryGetValue(
                        contract.characterId,
                        out EmployeeWageState state))
                {
                    state.lastSettledDay = settlementDay;
                }
            }
            else
            {
                EndMercenaryContract(contract, result);
            }
        }

        return result;
    }

    public bool TryHireMercenary(
        CharacterActor actor,
        int rolePremium,
        int day,
        out string failureReason)
    {
        string characterId = actor?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        if (characterId.Length == 0)
        {
            failureReason = "용병의 영구 ID가 없습니다.";
            return false;
        }

        EmployeeWageState state = GetOrCreateState(
            characterId,
            EmploymentContractKind.Mercenary);
        state.rolePremium = Mathf.Max(0, rolePremium);
        state.equipmentGradePremium =
            CalculateEquipmentPremium(characterId);
        int firstDailyFee = QuoteMercenaryDailyCost(
            characterId,
            actor.Progression?.Level ?? 1,
            rolePremium);
        if (!money.TrySpend(
                firstDailyFee,
                new EconomyTransactionContext(
                    EconomyTransactionKind.MercenaryAdvance,
                    "mercenary-contract",
                    characterId,
                    "용병 첫 일급"),
                out failureReason))
        {
            wageByCharacterId.Remove(characterId);
            return false;
        }

        int hiredDay = Mathf.Max(1, day);
        mercenaryByCharacterId[characterId] = new MercenaryContract
        {
            characterId = characterId,
            hiredDay = hiredDay,
            lastRenewedDay = hiredDay,
            active = true
        };
        state.lastSettledDay = hiredDay;
        state.active = true;
        state.departed = false;
        failureReason = string.Empty;
        return true;
    }

    public bool SetEmployeeRolePremium(
        string characterId,
        int premium,
        out string failureReason)
    {
        string normalizedId = NormalizeId(characterId);
        if (!wageByCharacterId.TryGetValue(
                normalizedId,
                out EmployeeWageState state)
            || state.contractKind == EmploymentContractKind.Founder)
        {
            failureReason = "임금을 설정할 직원을 찾지 못했습니다.";
            return false;
        }

        state.rolePremium = Mathf.Max(0, premium);
        failureReason = string.Empty;
        return true;
    }

    public EmploymentContractSaveData Capture()
    {
        EnsureCurrentStaff();
        return new EmploymentContractSaveData
        {
            wageStates = wageByCharacterId.Values
                .OrderBy(state => state.characterId, StringComparer.Ordinal)
                .Select(state => state.Clone())
                .ToList(),
            mercenaryContracts = mercenaryByCharacterId.Values
                .OrderBy(contract => contract.characterId, StringComparer.Ordinal)
                .Select(contract => contract.Clone())
                .ToList()
        };
    }

    public void Restore(EmploymentContractSaveData saveData)
    {
        wageByCharacterId.Clear();
        mercenaryByCharacterId.Clear();
        foreach (EmployeeWageState source in saveData?.wageStates
                     ?? new List<EmployeeWageState>())
        {
            string characterId = NormalizeId(source?.characterId);
            if (characterId.Length > 0)
            {
                EmployeeWageState state = source.Clone();
                state.characterId = characterId;
                wageByCharacterId[characterId] = state;
            }
        }

        foreach (MercenaryContract source in saveData?.mercenaryContracts
                     ?? new List<MercenaryContract>())
        {
            string characterId = NormalizeId(source?.characterId);
            if (characterId.Length > 0)
            {
                MercenaryContract contract = source.Clone();
                contract.characterId = characterId;
                mercenaryByCharacterId[characterId] = contract;
            }
        }

        EnsureCurrentStaff();
    }

    private void EnsureCurrentStaff()
    {
        foreach (CharacterActor actor in characterWorld.Characters
                     ?? Array.Empty<CharacterActor>())
        {
            CharacterIdentity identity = actor?.Identity;
            if (identity == null
                || identity.CharacterType != CharacterType.NPC
                || actor.IsDead)
            {
                continue;
            }

            string characterId = NormalizeId(identity.PersistentId);
            if (characterId.Length == 0
                || wageByCharacterId.ContainsKey(characterId))
            {
                continue;
            }

            GetOrCreateState(
                characterId,
                IsFounder(characterId, actor)
                    ? EmploymentContractKind.Founder
                    : EmploymentContractKind.Employee);
        }
    }

    private EmployeeWageState GetOrCreateState(
        string characterId,
        EmploymentContractKind kind)
    {
        if (!wageByCharacterId.TryGetValue(
                characterId,
                out EmployeeWageState state))
        {
            state = new EmployeeWageState
            {
                characterId = characterId,
                contractKind = kind,
                active = true
            };
            wageByCharacterId.Add(characterId, state);
        }
        else
        {
            state.contractKind = kind;
            state.active = true;
        }

        return state;
    }

    private int CalculateEquipmentPremium(string characterId)
    {
        return equipmentRuntime.Instances
            .Where(instance => instance != null
                && string.Equals(
                    instance.ownerCharacterId,
                    characterId,
                    StringComparison.Ordinal))
            .Sum(instance => Mathf.Max(0, (int)instance.quality) * 4);
    }

    private void ApplyUnpaidWageMood(string characterId)
    {
        CharacterActor actor = FindActor(characterId);
        actor?.Stats?.ApplyMoodFactor(
            "economy:unpaid-wages",
            "밀린 임금",
            -6f,
            300f,
            3);
    }

    private void EndMercenaryContract(
        MercenaryContract contract,
        EmploymentDailySettlement result)
    {
        contract.active = false;
        result.departedMercenaryIds.Add(contract.characterId);
        if (wageByCharacterId.TryGetValue(
                contract.characterId,
                out EmployeeWageState state))
        {
            state.active = false;
            state.departed = true;
        }

        CharacterActor actor = FindActor(contract.characterId);
        if (actor?.AbilityCache != null
            && actor.AbilityCache.TryGetAbility(out AbilityMove move))
        {
            move.StartSystemExitDungeon();
        }
        else
        {
            actor?.SetLifecycleState(CharacterLifecycleState.ExitingDungeon);
        }

        eventBus.RaiseAlert(
            "용병 계약 종료",
            $"{actor?.Identity?.DisplayName ?? contract.characterId}에게 일급을 지급하지 못해 용병이 떠납니다.",
            EventAlertImportance.Medium,
            "경영");
    }

    private CharacterActor FindActor(string characterId)
    {
        return characterWorld.Characters.FirstOrDefault(actor =>
            actor?.Identity != null
            && string.Equals(
                actor.Identity.PersistentId,
                characterId,
                StringComparison.Ordinal));
    }

    private static bool IsFounder(
        string characterId,
        CharacterActor actor)
    {
        if (actor != null && actor.IsOwner)
        {
            return true;
        }

        if (string.Equals(characterId, "owner", StringComparison.Ordinal))
        {
            return true;
        }

        string[] parts = characterId.Split(':');
        return parts.Length == 3
            && string.Equals(parts[0], "staff", StringComparison.Ordinal)
            && (string.Equals(parts[2], "01", StringComparison.Ordinal)
                || string.Equals(parts[2], "02", StringComparison.Ordinal));
    }

    private static string NormalizeId(string value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
