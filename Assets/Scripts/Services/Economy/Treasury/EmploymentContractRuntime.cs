using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IEmploymentContractRuntime
{
    IEmploymentSettlementScope BeginSettlement();
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
}

// The caller owns the financial transaction; this scope owns reversible mood
// changes and delays irreversible departure/observation until that caller commits.
public interface IEmploymentSettlementScope : IDisposable
{
    void Commit();
}

public readonly struct EmploymentStandingState
{
    public EmploymentStandingState(
        string characterId,
        EmployeeWageState wage,
        MercenaryContract mercenary)
    {
        CharacterId = characterId ?? string.Empty;
        Wage = wage?.Clone();
        Mercenary = mercenary?.Clone();
    }

    public string CharacterId { get; }
    public EmployeeWageState Wage { get; }
    public MercenaryContract Mercenary { get; }
}

public interface IEmploymentStandingCommand
{
    EmploymentStandingState CaptureStandingState(string characterId);
    void ApplyStanding(string characterId, CharacterSettlementStanding standing);
    void RestoreStandingState(EmploymentStandingState snapshot);
}

public sealed class EmploymentContractRuntime :
    IEmploymentContractRuntime,
    IEmploymentStandingCommand
{
    private const int EmployeeBaseWage = 30;
    private const int EmployeeLevelWage = 2;
    private const int MercenaryBaseWage = 60;
    private const int MercenaryLevelWage = 4;

    private readonly ICharacterWorldQuery characterWorld;
    private readonly ICharacterWorldPersistenceIdentityQuery persistentCharacters;
    private readonly ICombatEquipmentRuntime equipmentRuntime;
    private readonly IGameMoneyAccount money;
    private readonly IGameEventBus eventBus;
    private readonly TreasuryEconomyAggregateStateStore stateStore;
    private readonly ICharacterSettlementStandingQuery settlementStandings;
    private SettlementScope activeSettlement;
    private bool settlingDay;

    public string LastSettlementEffectFailure { get; private set; } = string.Empty;

    private Dictionary<string, EmployeeWageState> wageByCharacterId =>
        stateStore.Current.Wages;
    private Dictionary<string, MercenaryContract> mercenaryByCharacterId =>
        stateStore.Current.Mercenaries;

    public EmploymentContractRuntime(
        ICharacterWorldQuery characterWorld,
        ICharacterWorldPersistenceIdentityQuery persistentCharacters,
        ICombatEquipmentRuntime equipmentRuntime,
        IGameMoneyAccount money,
        IGameEventBus eventBus,
        TreasuryEconomyAggregateStateStore stateStore,
        ICharacterSettlementStandingQuery settlementStandings = null)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.persistentCharacters = persistentCharacters
            ?? throw new ArgumentNullException(nameof(persistentCharacters));
        this.equipmentRuntime = equipmentRuntime
            ?? throw new ArgumentNullException(nameof(equipmentRuntime));
        this.money = money
            ?? throw new ArgumentNullException(nameof(money));
        this.eventBus = eventBus
            ?? throw new ArgumentNullException(nameof(eventBus));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.settlementStandings = settlementStandings;
    }

    public IReadOnlyList<EmployeeWageState> WageStates =>
        wageByCharacterId.Values
            .OrderBy(state => state.characterId, StringComparer.Ordinal)
            .Select(state => state.Clone())
            .ToArray();

    public IReadOnlyList<MercenaryContract> MercenaryContracts =>
        mercenaryByCharacterId.Values
            .OrderBy(contract => contract.characterId, StringComparer.Ordinal)
            .Select(contract => contract.Clone())
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
        if (settlementStandings?.GetStanding(normalizedId)
            == CharacterSettlementStanding.Minion)
        {
            return 0;
        }

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
        if (settlingDay)
            throw new InvalidOperationException("Employment day settlement is already active.");
        settlingDay = true;
        try
        {
            if (activeSettlement != null)
            {
                activeSettlement.RequirePreparing();
                return SettleDayCore(day);
            }
            using var settlement = BeginSettlement();
            EmploymentDailySettlement result = SettleDayCore(day);
            settlement.Commit();
            return result;
        }
        finally { settlingDay = false; }
    }

    public IEmploymentSettlementScope BeginSettlement()
    {
        if (activeSettlement != null)
            throw new InvalidOperationException("An employment settlement is already active.");
        LastSettlementEffectFailure = string.Empty;
        return activeSettlement = new SettlementScope(this);
    }

    private EmploymentDailySettlement SettleDayCore(int day)
    {
        EnsureCurrentStaff();
        int settlementDay = Mathf.Max(1, day);
        EmploymentDailySettlement result = new EmploymentDailySettlement
        {
            day = settlementDay
        };

        foreach (EmployeeWageState state in wageByCharacterId.Values
                     .Where(candidate => candidate.departurePending)
                     .OrderBy(
                         candidate => candidate.characterId,
                         StringComparer.Ordinal))
        {
            EmploymentSettlementLine line = new EmploymentSettlementLine
            {
                day = settlementDay,
                characterId = state.characterId,
                contractKind = EmploymentContractKind.Mercenary,
                contractEnded = true,
                financeSourceId = state.departureFinanceSourceId,
                financeTransactionId =
                    state.departureFinanceTransactionId,
                financeSucceeded = false
            };
            PrepareMercenaryDeparture(state, line, result);
            result.lines.Add(line);
        }

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
            int debtBefore = Mathf.Max(0, state.unpaidWages);
            int due = dailyCost + debtBefore;
            result.employeeWagesDue += due;
            EconomyTransactionContext context = new EconomyTransactionContext(
                EconomyTransactionKind.EmployeeWage,
                CreateSettlementSourceId(
                    "wage",
                    settlementDay,
                    state.characterId),
                state.characterId,
                "직원 임금");
            bool paid = TrySpendSettlement(
                due,
                context,
                out EconomyTransactionRecord finance,
                out _);
            bool moodPenaltyApplied = false;
            if (paid)
            {
                result.employeeWagesPaid += due;
                state.unpaidWages = 0;
            }
            else
            {
                state.unpaidWages = due;
                result.unpaidEmployeeWages += due;
                moodPenaltyApplied = ApplyUnpaidWageMood(state.characterId);
            }

            state.lastSettledDay = settlementDay;
            result.lines.Add(new EmploymentSettlementLine
            {
                day = settlementDay,
                characterId = state.characterId,
                contractKind = EmploymentContractKind.Employee,
                due = due,
                paid = paid ? due : 0,
                unpaidDebtBefore = debtBefore,
                unpaidDebtAfter = state.unpaidWages,
                moodPenaltyApplied = moodPenaltyApplied,
                financeSourceId = context.sourceId,
                financeTransactionId = finance.transactionId,
                financeSucceeded = finance.succeeded
            });
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
            EconomyTransactionContext context = new EconomyTransactionContext(
                EconomyTransactionKind.MercenaryRenewal,
                CreateSettlementSourceId(
                    "mercenary-renewal",
                    settlementDay,
                    contract.characterId),
                contract.characterId,
                "용병 계약 갱신");
            bool paid = TrySpendSettlement(
                due,
                context,
                out EconomyTransactionRecord finance,
                out _);
            EmploymentSettlementLine line = new EmploymentSettlementLine
            {
                day = settlementDay,
                characterId = contract.characterId,
                contractKind = EmploymentContractKind.Mercenary,
                due = due,
                paid = paid ? due : 0,
                financeSourceId = context.sourceId,
                financeTransactionId = finance.transactionId,
                financeSucceeded = finance.succeeded
            };
            if (paid)
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
                result.unpaidMercenaryFees += due;
                EndMercenaryContract(
                    contract,
                    settlementDay,
                    finance,
                    line,
                    result);
            }
            result.lines.Add(line);
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
        state.departurePending = false;
        state.departureContractDay = 0;
        state.departureFinanceSourceId = string.Empty;
        state.departureFinanceTransactionId = string.Empty;
        state.departureFailureReason = string.Empty;
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
        if (activeSettlement != null)
            throw new InvalidOperationException("Cannot save during an employment settlement.");
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

    public EmploymentStandingState CaptureStandingState(string characterId)
    {
        string normalizedId = NormalizeId(characterId);
        wageByCharacterId.TryGetValue(normalizedId, out EmployeeWageState wage);
        mercenaryByCharacterId.TryGetValue(normalizedId, out MercenaryContract mercenary);
        return new EmploymentStandingState(normalizedId, wage, mercenary);
    }

    public void ApplyStanding(
        string characterId,
        CharacterSettlementStanding standing)
    {
        string normalizedId = NormalizeId(characterId);
        if (normalizedId.Length == 0)
        {
            throw new ArgumentException("Character ID is required.", nameof(characterId));
        }

        if (standing == CharacterSettlementStanding.Minion)
        {
            wageByCharacterId.Remove(normalizedId);
            mercenaryByCharacterId.Remove(normalizedId);
            return;
        }

        if (standing == CharacterSettlementStanding.Resident)
        {
            CharacterActor actor = FindActor(normalizedId);
            GetOrCreateState(
                normalizedId,
                IsFounder(normalizedId, actor)
                    ? EmploymentContractKind.Founder
                    : EmploymentContractKind.Employee);
        }
    }

    public void RestoreStandingState(EmploymentStandingState snapshot)
    {
        string characterId = NormalizeId(snapshot.CharacterId);
        if (characterId.Length == 0)
        {
            return;
        }

        wageByCharacterId.Remove(characterId);
        mercenaryByCharacterId.Remove(characterId);
        if (snapshot.Wage != null)
        {
            wageByCharacterId[characterId] = snapshot.Wage.Clone();
        }
        if (snapshot.Mercenary != null)
        {
            mercenaryByCharacterId[characterId] = snapshot.Mercenary.Clone();
        }
    }

    internal void PopulateRestoreState(
        TreasuryEconomyAggregateState target,
        EmploymentContractSaveData saveData)
    {
        target = target ?? throw new ArgumentNullException(nameof(target));
        target.Wages.Clear();
        target.Mercenaries.Clear();
        foreach (EmployeeWageState source in saveData?.wageStates
                     ?? new List<EmployeeWageState>())
        {
            string characterId = NormalizeId(source?.characterId);
            if (characterId.Length > 0)
            {
                EmployeeWageState state = source.Clone();
                state.characterId = characterId;
                target.Wages[characterId] = state;
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
                target.Mercenaries[characterId] = contract;
            }
        }

    }

    private void EnsureCurrentStaff()
    {
        EnsureCurrentStaff(stateStore.Current);
    }

    private void EnsureCurrentStaff(TreasuryEconomyAggregateState target)
    {
        HashSet<string> persistentIds = new(
            (persistentCharacters.GetPersistentCharacterIds()
                ?? Array.Empty<CharacterId>())
            .Select(id => id.Value)
            .Where(id => id.Length > 0),
            StringComparer.Ordinal);
        foreach (string minionId in target.Wages.Keys
                     .Where(id => settlementStandings?.GetStanding(id)
                         == CharacterSettlementStanding.Minion)
                     .ToArray())
        {
            target.Wages.Remove(minionId);
            target.Mercenaries.Remove(minionId);
        }
        foreach (string staleId in target.Wages.Keys
                     .Where(id => !persistentIds.Contains(id))
                     .ToArray())
        {
            target.Wages.Remove(staleId);
        }
        foreach (string staleId in target.Mercenaries.Keys
                     .Where(id => !persistentIds.Contains(id))
                     .ToArray())
        {
            target.Mercenaries.Remove(staleId);
        }

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
                || !persistentIds.Contains(characterId)
                || target.Wages.ContainsKey(characterId)
                || settlementStandings?.GetStanding(characterId)
                    == CharacterSettlementStanding.Minion)
            {
                continue;
            }

            GetOrCreateState(
                target,
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
        return GetOrCreateState(stateStore.Current, characterId, kind);
    }

    private static EmployeeWageState GetOrCreateState(
        TreasuryEconomyAggregateState target,
        string characterId,
        EmploymentContractKind kind)
    {
        if (!target.Wages.TryGetValue(
                characterId,
                out EmployeeWageState state))
        {
            state = new EmployeeWageState
            {
                characterId = characterId,
                contractKind = kind,
                active = true
            };
            target.Wages.Add(characterId, state);
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

    private bool ApplyUnpaidWageMood(string characterId)
    {
        CharacterActor actor = FindActor(characterId);
        if (actor?.Stats == null) return false;
        activeSettlement.CaptureMood(actor.Stats);
        float resolved = actor.ApplyMoodFactorBeforeOutcomeCommit(
            "economy:unpaid-wages",
            "밀린 임금",
            -6f,
            300f,
            3,
            out bool applied);
        if (applied)
            activeSettlement.MoodPublications.Add(() => actor.Stats.PublishCommittedMoodFactor(
                "economy:unpaid-wages", resolved, 0));
        return applied;
    }

    private void EndMercenaryContract(
        MercenaryContract contract,
        int settlementDay,
        EconomyTransactionRecord finance,
        EmploymentSettlementLine line,
        EmploymentDailySettlement result)
    {
        contract.active = false;
        if (wageByCharacterId.TryGetValue(
                contract.characterId,
                out EmployeeWageState state))
        {
            state.active = false;
            state.departed = false;
            state.departurePending = true;
            state.departureContractDay = settlementDay;
            state.departureFinanceSourceId = finance.sourceId;
            state.departureFinanceTransactionId = finance.transactionId;
            state.departureFailureReason = string.Empty;
            line.contractEnded = true;
            PrepareMercenaryDeparture(state, line, result);
        }

        activeSettlement.ContractEvents.Add(new MercenaryContractEndedEvent(
            contract.characterId,
            FindActor(contract.characterId)?.Identity?.DisplayName ?? contract.characterId,
            settlementDay,
            finance.transactionId));
    }

    private void PrepareMercenaryDeparture(EmployeeWageState state,
        EmploymentSettlementLine line, EmploymentDailySettlement result)
    {
        // Admission is a real attempt, but starting exit releases AI ownership and
        // cannot be undone. Reports honestly retain pending until the outer commit.
        TryResumeMercenaryDeparture(state, line, result, start: false);
        if (state.departureFailureReason == "employment-departure-awaiting-settlement")
            activeSettlement.Departures.TryAdd(state.characterId,
                () => TryResumeMercenaryDeparture(state, line, result, start: true));
    }

    private void TryResumeMercenaryDeparture(
        EmployeeWageState state,
        EmploymentSettlementLine line,
        EmploymentDailySettlement result,
        bool start)
    {
        line.departureAttempted = true;
        CharacterActor actor = FindActor(state.characterId);
        string failureReason = string.Empty;
        bool started = false;
        if (actor == null)
        {
            failureReason = "employment-departure-actor-missing";
        }
        else if (actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon
                 || actor.CurrentLifecycleState == CharacterLifecycleState.Despawned)
        {
            started = true;
        }
        else if (actor.AbilityCache != null
                 && actor.AbilityCache.TryGetAbility(out AbilityMove move))
        {
            if (!move.CanStartExitDungeon(out AIActionFailure failure))
            {
                failureReason = string.IsNullOrWhiteSpace(failure.Reason)
                    ? "employment-departure-admission-rejected"
                    : failure.Reason;
            }
            else if (!start)
            {
                failureReason = "employment-departure-awaiting-settlement";
            }
            else
            {
                move.StartSystemExitDungeon();
                started = actor.CurrentLifecycleState
                    == CharacterLifecycleState.ExitingDungeon
                    || actor.CurrentLifecycleState
                    == CharacterLifecycleState.Despawned;
                if (!started)
                {
                    failureReason =
                        "employment-departure-did-not-start";
                }
            }
        }
        else if (actor.Lifecycle == null)
        {
            failureReason = "employment-departure-lifecycle-missing";
        }
        else if (!start)
        {
            failureReason = "employment-departure-awaiting-settlement";
        }
        else
        {
            actor.SetLifecycleState(CharacterLifecycleState.ExitingDungeon);
            started = actor.CurrentLifecycleState
                == CharacterLifecycleState.ExitingDungeon;
            if (!started)
            {
                failureReason = "employment-departure-did-not-start";
            }
        }

        state.departurePending = !started;
        state.departed = started;
        state.departureFailureReason = started
            ? string.Empty
            : failureReason;
        line.departureStarted = started;
        line.departurePending = !started;
        line.departureFailureReason = state.departureFailureReason;
        line.financeSourceId = state.departureFinanceSourceId;
        line.financeTransactionId = state.departureFinanceTransactionId;
        if (started
            && !result.departedMercenaryIds.Contains(
                state.characterId,
                StringComparer.Ordinal))
        {
            result.departedMercenaryIds.Add(state.characterId);
        }
    }

    private sealed class SettlementScope : IEmploymentSettlementScope
    {
        private readonly EmploymentContractRuntime owner;
        private readonly Dictionary<CharacterStats, CharacterMoodDeliveryTransactionSnapshot> moodBefore = new();
        internal readonly List<Action> MoodPublications = new();
        internal readonly Dictionary<string, Action> Departures = new(StringComparer.Ordinal);
        internal readonly List<MercenaryContractEndedEvent> ContractEvents = new();
        private bool committed;
        private bool disposed;

        internal SettlementScope(EmploymentContractRuntime owner) => this.owner = owner;

        internal void RequirePreparing()
        {
            if (disposed || committed)
                throw new InvalidOperationException("Employment settlement is no longer preparing.");
        }

        internal void CaptureMood(CharacterStats stats)
        {
            RequirePreparing();
            if (!moodBefore.ContainsKey(stats))
                moodBefore.Add(stats, stats.CaptureMoodDeliveryTransactionState());
        }

        public void Commit()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SettlementScope));
            if (committed) return;
            committed = true;
            foreach (var departure in Departures)
            {
                RunCommitted(() =>
                {
                    try { departure.Value(); }
                    finally
                    {
                        // A listener may throw after the actual transition. Never
                        // repeat an already-started exit because its ACK was lost.
                        var state = owner.wageByCharacterId[departure.Key];
                        var actor = owner.FindActor(departure.Key);
                        bool started = actor != null && (actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon
                            || actor.CurrentLifecycleState == CharacterLifecycleState.Despawned);
                        if (started)
                        {
                            state.departed = true;
                            state.departurePending = false;
                            state.departureFailureReason = string.Empty;
                        }
                        else if (state.departureFailureReason == "employment-departure-awaiting-settlement")
                            state.departureFailureReason = "employment-departure-execution-failed";
                    }
                }, "employment-departure:" + departure.Key);
            }
            foreach (Action publish in MoodPublications)
                RunCommitted(publish, "employment-mood-observation");
            foreach (MercenaryContractEndedEvent fact in ContractEvents)
                RunCommitted(() => owner.eventBus.Publish(fact), "employment-contract-observation");
        }

        private void RunCommitted(Action effect, string label)
        {
            try { effect(); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            {
                string failure = label + ":" + exception.GetType().Name + ":" + exception.Message;
                owner.LastSettlementEffectFailure = string.IsNullOrEmpty(owner.LastSettlementEffectFailure)
                    ? failure : owner.LastSettlementEffectFailure + " | " + failure;
                Debug.LogWarning(failure);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (!committed)
                    foreach (var mood in moodBefore)
                        mood.Key.RestoreMoodDeliveryTransactionState(mood.Value, publish: false);
            }
            finally { owner.activeSettlement = null; }
        }
    }

    private bool TrySpendSettlement(
        int amount,
        EconomyTransactionContext context,
        out EconomyTransactionRecord finance,
        out string failureReason)
    {
        if (money is not IIdempotentGameMoneyAccount idempotent
            || money is not IGameMoneyTransactionAttemptQuery attempts)
        {
            throw new InvalidOperationException(
                "Employment settlement requires idempotent money and exact transaction-attempt query authorities.");
        }

        bool paid = idempotent.TrySpendOnce(
            amount,
            context,
            out EconomyTransactionRecord receipt,
            out failureReason);
        if (!attempts.TryGetLatestTransaction(
                context.kind,
                context.sourceId,
                context.targetId,
                out finance)
            || finance.succeeded != paid
            || (paid && (receipt == null
                || !string.Equals(
                    receipt.transactionId,
                    finance.transactionId,
                    StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "Employment settlement did not retain its exact finance child transaction.");
        }
        return paid;
    }

    private static string CreateSettlementSourceId(
        string kind,
        int day,
        string characterId) =>
        "employment:" + kind
        + ":day=" + Mathf.Max(1, day)
        + ":character=" + NormalizeId(characterId);

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
