using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public enum ExpeditionIntelPaymentMethod
{
    Renown = 0,
    Gold = 1,
    ScoutingLabor = 2,
    TrailCharm = 3
}

public enum HostileRumorMitigationMethod
{
    Renown = 0,
    Gold = 1
}

public enum EcologyRaidPhase
{
    Inactive = 0,
    Scheduled = 1,
    InProgress = 2,
    Resolved = 3
}

public readonly struct EcologyRaidSnapshot
{
    public EcologyRaidSnapshot(
        EcologyRaidPhase phase,
        float remainingSeconds,
        float weatherPressure,
        float exposedFoodPressure,
        IReadOnlyList<Vector2Int> exposedFoodPositions,
        int raiderCount,
        int stolenQuantity)
    {
        Phase = phase;
        RemainingSeconds = Mathf.Max(0f, remainingSeconds);
        WeatherPressure = Mathf.Max(0f, weatherPressure);
        ExposedFoodPressure = Mathf.Max(0f, exposedFoodPressure);
        ExposedFoodPositions =
            exposedFoodPositions ?? Array.Empty<Vector2Int>();
        RaiderCount = Mathf.Max(0, raiderCount);
        StolenQuantity = Mathf.Max(0, stolenQuantity);
    }

    public EcologyRaidPhase Phase { get; }
    public float RemainingSeconds { get; }
    public float WeatherPressure { get; }
    public float ExposedFoodPressure { get; }
    public IReadOnlyList<Vector2Int> ExposedFoodPositions { get; }
    public int RaiderCount { get; }
    public int StolenQuantity { get; }
}

[Serializable]
public sealed class DungeonExternalInfluenceSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public float renown;
    public float dread;
    public float hostileRumor;
    public float ecologyPressure;
    public float scoutingLabor;
    public bool dreadDefenseArmed;
    public bool dreadDefenseActive;
    public bool dreadDefenseBoss;
    public bool ecologyWarningIssued;
    public bool ecologyRaidScheduled;
    public bool ecologyRaidInProgress;
    public float ecologyRaidRemainingSeconds;
    public int ecologyRaidSequence;
    public float lastWeatherPressure;
    public float lastExposedFoodPressure;
    public int currentOperatingDay = -1;
    public int lastRumorMitigationDay = -1;
    public List<string> intelUnlockedSiteIds = new List<string>();
    public List<string> dreadAffectedIntruderIds = new List<string>();
}

public interface IExternalCombatInfluenceQuery
{
    float GetMoveSpeedMultiplier(string characterId);
    float GetAttackSpeedMultiplier(string characterId);
    bool IsDreadDefenseActive { get; }
}

public interface IExternalInfluenceRuntime :
    IExternalCombatInfluenceQuery
{
    float Renown { get; }
    float Dread { get; }
    float HostileRumor { get; }
    float EcologyPressure { get; }
    float ScoutingLabor { get; }
    bool IsDreadDefenseArmed { get; }
    EcologyRaidSnapshot GetEcologyRaidSnapshot();
    void AddRenown(float amount, string source);
    void AddDread(float amount, string source);
    void AddHostileRumor(float amount, string source);
    void AddEcologyPressure(float amount, string source);
    void AddScoutingLabor(float amount);
    bool TryMitigateHostileRumor(
        HostileRumorMitigationMethod method,
        out float reducedAmount,
        out int cost,
        out string failureReason);
    bool TryArmDreadDefense(out string failureReason);
    bool BeginInvasionDread(bool boss);
    bool IsIntelUnlocked(string siteId);
    bool TryUnlockIntel(
        string siteId,
        ExpeditionIntelPaymentMethod payment,
        out string failureReason);
    bool TryUnlockIntelForActiveSite(
        string siteId,
        bool fixedBoss,
        int expiresDay,
        int currentDay,
        ExpeditionIntelPaymentMethod payment,
        out string failureReason);
    DungeonExternalInfluenceSaveData Capture();
    void Restore(
        DungeonExternalInfluenceSaveData saveData,
        DungeonGameRestoreReport report = null);
    void Reset();
}

public sealed class ExternalInfluenceRuntime :
    IExternalInfluenceRuntime,
    IStartable,
    ITickable,
    IDisposable
{
    public const string TrailCharmItemId = "resource:trail-charm";
    private const float RenownIntelCost = 10f;
    private const int GoldIntelCost = 200;
    private const float ScoutingIntelCost = 60f;
    private const float DreadDefenseCost = 15f;
    private const float EcologyRaidCountdownSeconds = 60f;
    private const float MaxRumorMitigation = 15f;
    private const int MaxRumorRenownCost = 10;
    private const int MaxRumorGoldCost = 200;

    private readonly IGameEventBus events;
    private readonly IGameMoneyRuntime money;
    private readonly IWorldItemStackRuntime items;
    private readonly IWildlifeRuntime wildlife;
    private readonly ISurvivalEnvironmentQuery survival;
    private readonly IGameClock gameClock;
    private readonly HashSet<string> intelUnlocked =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> dreadAffectedIntruders =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<IDisposable> subscriptions =
        new List<IDisposable>();
    private DungeonExternalInfluenceSaveData state =
        new DungeonExternalInfluenceSaveData();
    private bool ecologyResolutionReported;

    public ExternalInfluenceRuntime(
        IGameEventBus events,
        IGameMoneyRuntime money,
        IWorldItemStackRuntime items,
        IWildlifeRuntime wildlife,
        ISurvivalEnvironmentQuery survival,
        IGameClock gameClock)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.wildlife = wildlife
            ?? throw new ArgumentNullException(nameof(wildlife));
        this.survival = survival
            ?? throw new ArgumentNullException(nameof(survival));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public float Renown => state.renown;
    public float Dread => state.dread;
    public float HostileRumor => state.hostileRumor;
    public float EcologyPressure => state.ecologyPressure;
    public float ScoutingLabor => state.scoutingLabor;
    public bool IsDreadDefenseArmed => state.dreadDefenseArmed;
    public bool IsDreadDefenseActive => state.dreadDefenseActive;

    public EcologyRaidSnapshot GetEcologyRaidSnapshot()
    {
        IReadOnlyList<WorldItemStackSnapshot> exposed =
            wildlife.GetReachableFoodRaidTargets();
        IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders =
            wildlife.GetFoodRaidOrders();
        EcologyRaidPhase phase = state.ecologyRaidScheduled
            ? EcologyRaidPhase.Scheduled
            : state.ecologyRaidInProgress
                ? EcologyRaidPhase.InProgress
                : ecologyResolutionReported
                    ? EcologyRaidPhase.Resolved
                    : EcologyRaidPhase.Inactive;
        return new EcologyRaidSnapshot(
            phase,
            state.ecologyRaidRemainingSeconds,
            state.lastWeatherPressure,
            state.lastExposedFoodPressure,
            exposed.Select(stack => stack.Position).Distinct().ToArray(),
            orders.Count,
            orders.Sum(order => order.StolenQuantity));
    }

    public void Start()
    {
        subscriptions.Add(
            events.Subscribe<OperatingDayReportEvent>(OnOperatingDayReport));
        subscriptions.Add(
            events.Subscribe<OperatingDayStartedEvent>(OnOperatingDayStarted));
        subscriptions.Add(
            events.Subscribe<InvasionSpawnedEvent>(OnInvasionSpawned));
        subscriptions.Add(
            events.Subscribe<InvasionResolvedEvent>(_ => EndDreadDefense()));
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in subscriptions)
        {
            subscription?.Dispose();
        }

        subscriptions.Clear();
    }

    public void Tick()
    {
        if (gameClock.IsPaused)
        {
            return;
        }

        if (state.ecologyRaidScheduled)
        {
            state.ecologyRaidRemainingSeconds =
                AdvanceEcologyRaidCountdown(
                    state.ecologyRaidRemainingSeconds,
                    gameClock.DeltaTime,
                    gameClock.IsPaused);
            if (state.ecologyRaidRemainingSeconds <= 0f)
            {
                BeginScheduledEcologyRaid();
            }
        }

        if (state.ecologyRaidInProgress)
        {
            ResolveEcologyRaidIfComplete();
        }
        else if (!state.ecologyRaidScheduled
                 && state.ecologyPressure >= 80f)
        {
            CheckEcologyThresholds();
        }
    }

    public void AddRenown(float amount, string source)
    {
        state.renown = Mathf.Clamp(state.renown + Mathf.Max(0f, amount), 0f, 999f);
    }

    public void AddDread(float amount, string source)
    {
        state.dread = Mathf.Clamp(state.dread + Mathf.Max(0f, amount), 0f, 999f);
    }

    public void AddHostileRumor(float amount, string source)
    {
        state.hostileRumor = Mathf.Clamp(
            state.hostileRumor + Mathf.Max(0f, amount),
            0f,
            100f);
    }

    public void AddEcologyPressure(float amount, string source)
    {
        state.ecologyPressure = Mathf.Clamp(
            state.ecologyPressure + amount,
            0f,
            100f);
        CheckEcologyThresholds();
    }

    public void AddScoutingLabor(float amount)
    {
        state.scoutingLabor = Mathf.Clamp(
            state.scoutingLabor + Mathf.Max(0f, amount),
            0f,
            999f);
    }

    public bool TryMitigateHostileRumor(
        HostileRumorMitigationMethod method,
        out float reducedAmount,
        out int cost,
        out string failureReason)
    {
        reducedAmount = Mathf.Min(
            MaxRumorMitigation,
            Mathf.Max(0f, state.hostileRumor));
        cost = 0;
        if (reducedAmount <= 0f)
        {
            failureReason = "수습할 적대적 소문이 없습니다.";
            return false;
        }

        if (state.currentOperatingDay < 0)
        {
            failureReason = "영업일이 시작된 뒤 소문을 수습할 수 있습니다.";
            return false;
        }

        if (state.lastRumorMitigationDay == state.currentOperatingDay)
        {
            failureReason = "소문 수습은 하루에 한 번만 실행할 수 있습니다.";
            return false;
        }

        float ratio = reducedAmount / MaxRumorMitigation;
        switch (method)
        {
            case HostileRumorMitigationMethod.Renown:
                cost = Mathf.CeilToInt(MaxRumorRenownCost * ratio);
                if (state.renown < cost)
                {
                    failureReason =
                        $"명성이 부족합니다. {state.renown:0.#}/{cost}";
                    return false;
                }

                state.renown -= cost;
                break;
            case HostileRumorMitigationMethod.Gold:
                cost = Mathf.CeilToInt(MaxRumorGoldCost * ratio);
                if (!money.TrySpend(
                        cost,
                        new EconomyTransactionContext(
                            EconomyTransactionKind.Bribe,
                            nameof(ExternalInfluenceRuntime),
                            $"rumor:{state.currentOperatingDay}",
                            "적대적 소문 수습"),
                        out failureReason))
                {
                    return false;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(method),
                    method,
                    null);
        }

        state.hostileRumor = Mathf.Max(
            0f,
            state.hostileRumor - reducedAmount);
        state.lastRumorMitigationDay = state.currentOperatingDay;
        failureReason = string.Empty;
        return true;
    }

    public bool TryArmDreadDefense(out string failureReason)
    {
        if (state.dreadDefenseArmed)
        {
            failureReason = "다음 침입 공포 효과가 이미 준비되어 있습니다.";
            return false;
        }

        if (state.dread < DreadDefenseCost)
        {
            failureReason =
                $"공포가 부족합니다. {state.dread:0.#}/{DreadDefenseCost:0}";
            return false;
        }

        state.dread -= DreadDefenseCost;
        state.dreadDefenseArmed = true;
        failureReason = string.Empty;
        return true;
    }

    public bool BeginInvasionDread(bool boss)
    {
        if (state.dreadDefenseActive)
        {
            return true;
        }

        if (!state.dreadDefenseArmed)
        {
            return false;
        }

        state.dreadDefenseArmed = false;
        state.dreadDefenseActive = true;
        state.dreadDefenseBoss = boss;
        dreadAffectedIntruders.Clear();
        return true;
    }

    public float GetMoveSpeedMultiplier(string characterId)
    {
        return state.dreadDefenseActive
            && dreadAffectedIntruders.Contains(
                characterId?.Trim() ?? string.Empty)
                    ? state.dreadDefenseBoss ? 0.95f : 0.9f
                    : 1f;
    }

    public float GetAttackSpeedMultiplier(string characterId)
    {
        return GetMoveSpeedMultiplier(characterId);
    }

    public bool IsIntelUnlocked(string siteId)
    {
        return intelUnlocked.Contains(siteId?.Trim() ?? string.Empty);
    }

    public bool TryUnlockIntel(
        string siteId,
        ExpeditionIntelPaymentMethod payment,
        out string failureReason)
    {
        string normalized = siteId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            failureReason = "원정지 ID가 필요합니다.";
            return false;
        }

        if (intelUnlocked.Contains(normalized))
        {
            failureReason = string.Empty;
            return true;
        }

        bool paid = payment switch
        {
            ExpeditionIntelPaymentMethod.Renown =>
                TrySpendRenown(out failureReason),
            ExpeditionIntelPaymentMethod.Gold =>
                money.TrySpend(
                    GoldIntelCost,
                    new EconomyTransactionContext(
                        EconomyTransactionKind.Bribe,
                        nameof(ExternalInfluenceRuntime),
                        normalized,
                        "원정 정보 구매"),
                    out failureReason),
            ExpeditionIntelPaymentMethod.ScoutingLabor =>
                TrySpendScouting(out failureReason),
            ExpeditionIntelPaymentMethod.TrailCharm =>
                TryConsumeTrailCharm(out failureReason),
            _ => throw new ArgumentOutOfRangeException(
                nameof(payment),
                payment,
                null)
        };
        if (!paid)
        {
            return false;
        }

        intelUnlocked.Add(normalized);
        failureReason = string.Empty;
        return true;
    }

    public bool TryUnlockIntelForActiveSite(
        string siteId,
        bool fixedBoss,
        int expiresDay,
        int currentDay,
        ExpeditionIntelPaymentMethod payment,
        out string failureReason)
    {
        if (!IsIntelSiteActive(fixedBoss, expiresDay, currentDay))
        {
            failureReason =
                $"거점이 Day {expiresDay}에 만료되어 결제를 취소했습니다. 재화는 차감되지 않았습니다.";
            return false;
        }

        return TryUnlockIntel(siteId, payment, out failureReason);
    }

    public static float AdvanceEcologyRaidCountdown(
        float remainingSeconds,
        float gameDeltaTime,
        bool paused)
    {
        return paused
            ? Mathf.Max(0f, remainingSeconds)
            : Mathf.Max(
                0f,
                remainingSeconds - Mathf.Max(0f, gameDeltaTime));
    }

    public static bool IsIntelSiteActive(
        bool fixedBoss,
        int expiresDay,
        int currentDay)
    {
        return fixedBoss
            || expiresDay > 0 && currentDay < expiresDay;
    }

    public DungeonExternalInfluenceSaveData Capture()
    {
        DungeonExternalInfluenceSaveData result =
            JsonUtility.FromJson<DungeonExternalInfluenceSaveData>(
                JsonUtility.ToJson(state));
        result.intelUnlockedSiteIds = intelUnlocked
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        result.dreadAffectedIntruderIds = dreadAffectedIntruders
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        return result;
    }

    public void Restore(
        DungeonExternalInfluenceSaveData saveData,
        DungeonGameRestoreReport report = null)
    {
        Reset();
        if (saveData == null)
        {
            return;
        }

        if (saveData.version != DungeonExternalInfluenceSaveData.CurrentVersion)
        {
            report?.AddError(
                $"Unsupported external influence version {saveData.version}.");
            return;
        }

        state = JsonUtility.FromJson<DungeonExternalInfluenceSaveData>(
            JsonUtility.ToJson(saveData));
        state.renown = Mathf.Clamp(state.renown, 0f, 999f);
        state.dread = Mathf.Clamp(state.dread, 0f, 999f);
        state.hostileRumor = Mathf.Clamp(state.hostileRumor, 0f, 100f);
        state.ecologyPressure = Mathf.Clamp(
            state.ecologyPressure,
            0f,
            100f);
        state.scoutingLabor = Mathf.Clamp(
            state.scoutingLabor,
            0f,
            999f);
        state.ecologyRaidRemainingSeconds = Mathf.Max(
            0f,
            state.ecologyRaidRemainingSeconds);
        state.currentOperatingDay = Mathf.Max(
            -1,
            state.currentOperatingDay);
        state.lastRumorMitigationDay = Mathf.Max(
            -1,
            state.lastRumorMitigationDay);
        ecologyResolutionReported = false;
        foreach (string id in saveData.intelUnlockedSiteIds
                     ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                intelUnlocked.Add(id.Trim());
            }
        }

        foreach (string id in saveData.dreadAffectedIntruderIds
                     ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                dreadAffectedIntruders.Add(id.Trim());
            }
        }
    }

    public void Reset()
    {
        state = new DungeonExternalInfluenceSaveData();
        intelUnlocked.Clear();
        dreadAffectedIntruders.Clear();
        ecologyResolutionReported = false;
    }

    private void OnOperatingDayReport(OperatingDayReportEvent eventType)
    {
        OperatingDayReport report = eventType.report;
        if (report == null || report.totalVisits <= 0)
        {
            return;
        }

        float amount = Mathf.Max(1f, report.totalVisits / 5f);
        if (report.averageSatisfaction >= 70f)
        {
            float renownBefore = state.renown;
            AddRenown(amount, $"operating-day:{report.day}");
            float generatedRenown = state.renown - renownBefore;
            state.hostileRumor = Mathf.Max(
                0f,
                state.hostileRumor - generatedRenown);
        }
        else if (report.averageSatisfaction < 40f)
        {
            AddHostileRumor(amount, $"operating-day:{report.day}");
        }
    }

    private void OnOperatingDayStarted(OperatingDayStartedEvent eventType)
    {
        state.currentOperatingDay = eventType.day;
        SurvivalEnvironmentSnapshot environment =
            survival.GetEnvironmentSnapshot();
        float weatherPressure =
            environment.Weather == SurvivalWeatherType.ColdSnap
            ? 25f
            : 4f;
        int exposedFood = wildlife.GetReachableFoodRaidTargets().Count;
        float exposedPressure = Mathf.Min(20f, exposedFood * 1.5f);
        state.lastWeatherPressure = weatherPressure;
        state.lastExposedFoodPressure = exposedPressure;
        float pressure = weatherPressure + exposedPressure;
        AddEcologyPressure(
            pressure,
            $"operating-day:{eventType.day}");
    }

    private void CheckEcologyThresholds()
    {
        IReadOnlyList<WorldItemStackSnapshot> exposed =
            wildlife.GetReachableFoodRaidTargets();
        if (state.ecologyPressure >= 60f
            && !state.ecologyWarningIssued)
        {
            state.ecologyWarningIssued = true;
            string positions = exposed.Count == 0
                ? "없음"
                : string.Join(
                    ", ",
                    exposed.Take(5)
                        .Select(stack =>
                            $"{stack.DisplayName} ({stack.Position.x},{stack.Position.y})"));
            events.RaiseAlert(
                "야생 무리 흔적",
                $"생태 압력 {state.ecologyPressure:0.#}: 날씨 +{state.lastWeatherPressure:0.#}, "
                + $"도달 가능한 외부 노출 식량 +{state.lastExposedFoodPressure:0.#}. "
                + $"위험 식량: {positions}",
                EventAlertImportance.Medium,
                "생태 압박");
        }

        if (state.ecologyPressure < 80f
            || state.ecologyRaidScheduled
            || state.ecologyRaidInProgress)
        {
            return;
        }

        state.ecologyRaidScheduled = true;
        state.ecologyRaidRemainingSeconds = EcologyRaidCountdownSeconds;
        state.ecologyRaidSequence++;
        ecologyResolutionReported = false;
        events.RaiseAlert(
            "늑대 식량 습격 예정",
            $"60게임초 뒤 외부 진입로에서 늑대 2마리가 접근합니다. "
            + $"도달 가능한 Loose 식량 {exposed.Count}개를 옮기거나 경로를 차단하십시오.",
            EventAlertImportance.High,
            "야생 침입");
    }

    private void BeginScheduledEcologyRaid()
    {
        state.ecologyRaidScheduled = false;
        state.ecologyRaidRemainingSeconds = 0f;
        string raidId = $"ecology:{state.ecologyRaidSequence}";
        if (!wildlife.TryBeginFoodRaid(
                raidId,
                2,
                out IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders,
                out string failureReason))
        {
            ecologyResolutionReported = true;
            events.RaiseAlert(
                "늑대 식량 습격 무산",
                string.IsNullOrWhiteSpace(failureReason)
                    ? "늑대가 출현하지 못해 식량 손실 없이 습격이 끝났습니다."
                    : failureReason,
                EventAlertImportance.Medium,
                "야생 침입");
            state.ecologyPressure = Mathf.Max(
                35f,
                state.ecologyPressure - 45f);
            state.ecologyWarningIssued = false;
            return;
        }

        state.ecologyRaidInProgress = true;
        ecologyResolutionReported = false;
        events.RaiseAlert(
            "늑대 식량 습격 진행",
            $"외부 진입로에서 늑대 {orders.Count}마리가 실제 식량으로 이동합니다. "
            + "각 늑대는 식량에 도달한 경우에만 최대 1개를 훔칩니다.",
            EventAlertImportance.High,
            "야생 침입");
    }

    private void ResolveEcologyRaidIfComplete()
    {
        IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders =
            wildlife.GetFoodRaidOrders();
        if (orders.Count == 0 || orders.Any(order => !order.IsTerminal))
        {
            return;
        }

        int stolen = orders.Sum(order => order.StolenQuantity);
        int cancelled = orders.Count(order =>
            order.State == WildlifeFoodRaidOrderState.Cancelled);
        state.ecologyRaidInProgress = false;
        state.ecologyPressure = Mathf.Max(
            35f,
            state.ecologyPressure - 45f);
        state.ecologyWarningIssued = false;
        ecologyResolutionReported = true;
        events.RaiseAlert(
            "늑대 식량 습격 해결",
            $"실제 도난 {stolen}개, 처치·제거로 취소 {cancelled}마리. "
            + "도달하지 못한 늑대는 아무것도 훔치지 않았습니다.",
            stolen > 0
                ? EventAlertImportance.High
                : EventAlertImportance.Medium,
            "야생 침입");
    }

    private void OnInvasionSpawned(InvasionSpawnedEvent eventType)
    {
        if (!state.dreadDefenseActive)
        {
            return;
        }

        string id = eventType.intruderActor?.Identity?.PersistentId;
        if (!string.IsNullOrWhiteSpace(id))
        {
            dreadAffectedIntruders.Add(id);
        }
    }

    private void EndDreadDefense()
    {
        state.dreadDefenseActive = false;
        state.dreadDefenseBoss = false;
        dreadAffectedIntruders.Clear();
    }

    private bool TrySpendRenown(out string failureReason)
    {
        if (state.renown < RenownIntelCost)
        {
            failureReason =
                $"명성이 부족합니다. {state.renown:0.#}/{RenownIntelCost:0}";
            return false;
        }

        state.renown -= RenownIntelCost;
        failureReason = string.Empty;
        return true;
    }

    private bool TrySpendScouting(out string failureReason)
    {
        if (state.scoutingLabor < ScoutingIntelCost)
        {
            failureReason =
                $"정찰 노동이 부족합니다. {state.scoutingLabor:0.#}/{ScoutingIntelCost:0}";
            return false;
        }

        state.scoutingLabor -= ScoutingIntelCost;
        failureReason = string.Empty;
        return true;
    }

    private bool TryConsumeTrailCharm(out string failureReason)
    {
        WorldItemStackSnapshot charm = items.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && !stack.Forbidden
                && string.Equals(
                    stack.ItemId,
                    TrailCharmItemId,
                    StringComparison.Ordinal)
                && stack.Quantity > 0);
        if (charm == null
            || !items.TryConsumeStackQuantity(
                charm.StackId,
                1,
                out _))
        {
            failureReason = "사용 가능한 길잡이 부적이 없습니다.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }
}
