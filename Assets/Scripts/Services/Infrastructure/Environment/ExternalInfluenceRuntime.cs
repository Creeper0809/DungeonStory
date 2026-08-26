using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.CoreSession;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class ExternalInfluenceRuntimeApplicationAdapter :
    IExternalInfluenceRuntime,
    IStartable,
    ITickable,
    IDisposable
{
    public sealed class Dependencies
    {
        public Dependencies(
            IGameClock gameClock,
            ICoreSessionRulesProvider rulesProvider)
        {
            GameClock = gameClock
                ?? throw new ArgumentNullException(nameof(gameClock));
            Rules = (rulesProvider
                    ?? throw new ArgumentNullException(nameof(rulesProvider)))
                .CoreSessionRules
                ?? throw new InvalidOperationException(
                    "Core-session rules are not authored.");
        }

        public IGameClock GameClock { get; }
        public CoreSessionRulesDefinition Rules { get; }
    }

    public const string TrailCharmItemId =
        ExternalInfluenceTrailCharmSaveContract.TrailCharmItemId;

    private readonly IGameEventBus events;
    private readonly IGameMoneyAccount money;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly ItemDefinitionId trailCharmItemId;
    private readonly IWildlifeRuntime wildlife;
    private readonly ISurvivalEnvironmentQuery survival;
    private readonly IGameClock gameClock;
    private readonly CoreSessionRulesDefinition rules;
    private readonly List<IDisposable> subscriptions =
        new List<IDisposable>();
    private readonly ExternalInfluenceAggregateStateStore stateStore;

    private ExternalInfluenceAggregateState aggregateState =>
        stateStore.Current;

    private DungeonExternalInfluenceSaveData state => aggregateState.Data;
    private HashSet<string> intelUnlocked => aggregateState.IntelUnlocked;
    private HashSet<string> dreadAffectedIntruders =>
        aggregateState.DreadAffectedIntruders;
    public ExternalInfluenceRuntimeApplicationAdapter(
        IGameEventBus events,
        IGameMoneyAccount money,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService batchDispositions,
        IItemDefinitionCatalog itemDefinitions,
        IWildlifeRuntime wildlife,
        ISurvivalEnvironmentQuery survival,
        Dependencies dependencies,
        ExternalInfluenceAggregateStateStore stateStore)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
        trailCharmItemId = (itemDefinitions
                ?? throw new ArgumentNullException(nameof(itemDefinitions)))
            .GetRequired((ItemDefinitionId)TrailCharmItemId)
            .StableId;
        this.wildlife = wildlife
            ?? throw new ArgumentNullException(nameof(wildlife));
        this.survival = survival
            ?? throw new ArgumentNullException(nameof(survival));
        dependencies = dependencies
            ?? throw new ArgumentNullException(nameof(dependencies));
        gameClock = dependencies.GameClock;
        rules = dependencies.Rules;
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
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
                : state.ecologyResolutionReported
                    ? EcologyRaidPhase.Resolved
                    : EcologyRaidPhase.Inactive;
        return new EcologyRaidSnapshot(
            phase,
            state.ecologyRaidRemainingSeconds,
            state.lastWeatherPressure,
            state.lastExposedFoodPressure,
            exposed
                .Select(stack => new CoreGridCell(
                    stack.Position.x,
                    stack.Position.y))
                .Distinct()
                .ToArray(),
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
                ExternalInfluenceDomainRules.AdvanceEcologyRaidCountdown(
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
        ExternalInfluenceDomainRules.AddRenown(state, amount);
    }

    public void AddDread(float amount, string source)
    {
        ExternalInfluenceDomainRules.AddDread(state, amount);
    }

    public void AddHostileRumor(float amount, string source)
    {
        ExternalInfluenceDomainRules.AddHostileRumor(state, amount);
    }

    public void AddEcologyPressure(float amount, string source)
    {
        ExternalInfluenceDomainRules.AddEcologyPressure(state, amount);
        CheckEcologyThresholds();
    }

    public void AddScoutingLabor(float amount)
    {
        ExternalInfluenceDomainRules.AddScoutingLabor(state, amount);
    }

    public bool TryMitigateHostileRumor(
        HostileRumorMitigationMethod method,
        out float reducedAmount,
        out int cost,
        out DomainFailure failure)
    {
        if (!ExternalInfluenceDomainRules.TryPrepareRumorMitigation(
                state,
                method,
                rules.MaximumRumorMitigation,
                rules.MaximumRumorRenownCost,
                rules.MaximumRumorGoldCost,
                out reducedAmount,
                out cost,
                out failure))
        {
            return false;
        }

        if (method == HostileRumorMitigationMethod.Gold)
        {
            if (!money.CanSpend(cost))
            {
                failure = new DomainFailure(
                    FailureCode.InsufficientGold,
                    money.Balance.ToString(),
                    cost.ToString());
                return false;
            }
            if (!money.TrySpend(
                    cost,
                    new EconomyTransactionContext(
                        EconomyTransactionKind.Bribe,
                        "ExternalInfluenceRuntime",
                        $"rumor:{state.currentOperatingDay}",
                        "적대적 소문 수습"),
                    out _))
            {
                failure = new DomainFailure(
                    FailureCode.ExternalPaymentRejected);
                return false;
            }
        }

        ExternalInfluenceDomainRules.CommitRumorMitigation(
            state,
            method,
            reducedAmount,
            cost);
        failure = DomainFailure.None;
        return true;
    }

    public bool TryArmDreadDefense(out DomainFailure failure)
    {
        return ExternalInfluenceDomainRules.TryArmDreadDefense(
            state,
            rules.DreadDefenseCost,
            out failure);
    }

    public bool BeginInvasionDread(bool boss)
    {
        return ExternalInfluenceDomainRules.BeginInvasionDread(
            aggregateState,
            boss);
    }

    public float GetMoveSpeedMultiplier(string characterId)
    {
        return ExternalInfluenceDomainRules.GetDreadSpeedMultiplier(
            aggregateState,
            characterId);
    }

    public float GetAttackSpeedMultiplier(string characterId)
    {
        return GetMoveSpeedMultiplier(characterId);
    }

    public bool IsIntelUnlocked(string siteId)
    {
        return ExternalInfluenceDomainRules.IsIntelUnlocked(
            aggregateState,
            siteId);
    }

    public bool TryUnlockIntel(
        string siteId,
        ExpeditionIntelPaymentMethod payment,
        out DomainFailure failure)
    {
        string normalized = ExternalInfluenceDomainRules.NormalizeId(siteId);
        if (normalized.Length == 0)
        {
            failure = new DomainFailure(FailureCode.ExpeditionSiteIdMissing);
            return false;
        }

        if (ExternalInfluenceTrailCharmOutbox.HasPending(state)
            && !ExternalInfluenceTrailCharmOutbox.TryFinalizePending(
                aggregateState,
                batchDispositions,
                out string pendingFailure))
        {
            failure = new DomainFailure(
                FailureCode.ExternalPaymentRejected,
                pendingFailure);
            return false;
        }

        if (ExternalInfluenceDomainRules.IsIntelUnlocked(
                aggregateState,
                normalized))
        {
            failure = DomainFailure.None;
            return true;
        }

        bool paid = payment switch
        {
            ExpeditionIntelPaymentMethod.Renown =>
                TrySpendRenown(out failure),
            ExpeditionIntelPaymentMethod.Gold =>
                TrySpendGoldForIntel(normalized, out failure),
            ExpeditionIntelPaymentMethod.ScoutingLabor =>
                TrySpendScouting(out failure),
            ExpeditionIntelPaymentMethod.TrailCharm =>
                TryConsumeTrailCharm(normalized, out failure),
            _ => throw new ArgumentOutOfRangeException(
                nameof(payment),
                payment,
                null)
        };
        if (!paid)
        {
            return false;
        }

        if (payment != ExpeditionIntelPaymentMethod.TrailCharm)
        {
            ExternalInfluenceDomainRules.UnlockIntel(
                aggregateState,
                normalized);
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool TryUnlockIntelForActiveSite(
        string siteId,
        bool fixedBoss,
        int expiresDay,
        int currentDay,
        ExpeditionIntelPaymentMethod payment,
        out DomainFailure failure)
    {
        if (!ExternalInfluenceDomainRules.IsIntelSiteActive(
                fixedBoss,
                expiresDay,
                currentDay))
        {
            failure = new DomainFailure(
                FailureCode.ExpeditionSiteExpired,
                expiresDay.ToString());
            return false;
        }

        return TryUnlockIntel(siteId, payment, out failure);
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

    public ExternalInfluenceRestoreCandidate BuildRestoreCandidate(
        DungeonExternalInfluenceSaveData saveData)
    {
        if (saveData == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        if (saveData.version != DungeonExternalInfluenceSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported external influence version {saveData.version}.");
        }

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ExternalInfluenceSaveValidation.Validate(saveData, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "External influence restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        DungeonExternalInfluenceSaveData restored =
            JsonUtility.FromJson<DungeonExternalInfluenceSaveData>(
                JsonUtility.ToJson(saveData))
                ?? throw new InvalidOperationException(
                    "External influence snapshot clone returned null.");
        return new ExternalInfluenceRestoreCandidate(
            restored,
            saveData.intelUnlockedSiteIds.ToArray(),
            saveData.dreadAffectedIntruderIds.ToArray());
    }

    public void PublishRestoreCandidate(
        ExternalInfluenceRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        ExternalInfluenceAggregateState restored = new()
        {
            Data = candidate.Data
        };
        foreach (string id in candidate.IntelUnlocked)
        {
            restored.IntelUnlocked.Add(id);
        }
        foreach (string id in candidate.DreadAffectedIntruders)
        {
            restored.DreadAffectedIntruders.Add(id);
        }
        stateStore.Replace(restored);
        if (ExternalInfluenceTrailCharmOutbox.HasPending(restored.Data)
            && !ExternalInfluenceTrailCharmOutbox.TryFinalizePending(
                restored,
                batchDispositions,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "External-influence trail-charm restore reconciliation failed: "
                + failureReason);
        }
    }

    public void Reset()
    {
        stateStore.Replace(new ExternalInfluenceAggregateState());
    }

    private void OnOperatingDayReport(OperatingDayReportEvent eventType)
    {
        OperatingDayReport report = eventType.report;
        if (report == null)
        {
            return;
        }

        ExternalInfluenceDomainRules.ApplyOperatingDayReport(
            state,
            report.totalVisits,
            report.averageSatisfaction);
    }

    private void OnOperatingDayStarted(OperatingDayStartedEvent eventType)
    {
        SurvivalEnvironmentSnapshot environment =
            survival.GetEnvironmentSnapshot();
        int exposedFood = wildlife.GetReachableFoodRaidTargets().Count;
        ExternalInfluenceDomainRules.BeginOperatingDay(
            state,
            eventType.day,
            environment.Weather == SurvivalWeatherType.ColdSnap,
            exposedFood);
        CheckEcologyThresholds();
    }

    private void CheckEcologyThresholds()
    {
        IReadOnlyList<WorldItemStackSnapshot> exposed =
            wildlife.GetReachableFoodRaidTargets();
        if (ExternalInfluenceDomainRules.TryIssueEcologyWarning(state))
        {
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

        if (!ExternalInfluenceDomainRules.TryScheduleEcologyRaid(
                state,
                rules.EcologyRaidCountdownSeconds))
        {
            return;
        }

        events.RaiseAlert(
            "늑대 식량 습격 예정",
            $"60게임초 뒤 외부 진입로에서 늑대 2마리가 접근합니다. "
            + $"도달 가능한 Loose 식량 {exposed.Count}개를 옮기거나 경로를 차단하십시오.",
            EventAlertImportance.High,
            "야생 침입");
    }

    private void BeginScheduledEcologyRaid()
    {
        string raidId =
            ExternalInfluenceDomainRules.BeginScheduledEcologyRaid(state);
        if (!wildlife.TryBeginFoodRaid(
                raidId,
                2,
                out IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders,
                out string failureReason))
        {
            ExternalInfluenceDomainRules.RecordEcologyRaidStartFailure(state);
            events.RaiseAlert(
                "늑대 식량 습격 무산",
                string.IsNullOrWhiteSpace(failureReason)
                    ? "늑대가 출현하지 못해 식량 손실 없이 습격이 끝났습니다."
                    : failureReason,
                EventAlertImportance.Medium,
                "야생 침입");
            return;
        }

        ExternalInfluenceDomainRules.RecordEcologyRaidStarted(state);
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
        ExternalInfluenceDomainRules.RecordEcologyRaidResolved(state);
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

        ExternalInfluenceDomainRules.MarkDreadAffectedIntruder(
            aggregateState,
            eventType.intruderActor?.Identity?.PersistentId);
    }

    private void EndDreadDefense()
    {
        ExternalInfluenceDomainRules.EndInvasionDread(aggregateState);
    }

    private bool TrySpendRenown(out DomainFailure failure)
    {
        return ExternalInfluenceDomainRules.TrySpendRenownForIntel(
            state,
            rules.RenownIntelCost,
            out failure);
    }

    private bool TrySpendGoldForIntel(
        string siteId,
        out DomainFailure failure)
    {
        if (!money.CanSpend(rules.GoldIntelCost))
        {
            failure = new DomainFailure(
                FailureCode.InsufficientGold,
                money.Balance.ToString(),
                rules.GoldIntelCost.ToString());
            return false;
        }

        if (!money.TrySpend(
                rules.GoldIntelCost,
                new EconomyTransactionContext(
                    EconomyTransactionKind.Bribe,
                    "ExternalInfluenceRuntime",
                    siteId,
                    "external-intel"),
                out _))
        {
            failure = new DomainFailure(FailureCode.ExternalPaymentRejected);
            return false;
        }

        failure = DomainFailure.None;
        return true;
    }

    private bool TrySpendScouting(out DomainFailure failure)
    {
        return ExternalInfluenceDomainRules.TrySpendScoutingForIntel(
            state,
            rules.ScoutingIntelCost,
            out failure);
    }

    private bool TryConsumeTrailCharm(
        string siteId,
        out DomainFailure failure)
    {
        WorldItemStackSnapshot charm = items.GetAllStacks()
            .Where(stack => stack != null
                && !stack.Forbidden
                && string.Equals(
                    stack.ItemId,
                    trailCharmItemId.Value,
                    StringComparison.Ordinal)
                && stack.Quantity > 0)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        string operationId =
            ExternalInfluenceTrailCharmOutbox.FormatOperationId(siteId);
        string commitFailure = string.Empty;
        if (charm == null
            || !batchDispositions.TryCommitPending(
                new[] { new PhysicalItemTransformInput(charm.StackId, 1) },
                PhysicalItemDispositionKind.Sink,
                operationId,
                ExternalInfluenceTrailCharmOutbox.ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out commitFailure))
        {
            failure = charm == null
                ? new DomainFailure(FailureCode.TrailCharmMissing)
                : new DomainFailure(
                    FailureCode.ExternalPaymentRejected,
                    commitFailure);
            return false;
        }

        ExternalInfluenceTrailCharmOutbox.RecordPending(
            state,
            siteId,
            trailCharmItemId.Value,
            receipt);
        if (!ExternalInfluenceTrailCharmOutbox.TryFinalizePending(
                aggregateState,
                batchDispositions,
                out string finalizeFailure))
        {
            failure = new DomainFailure(
                FailureCode.ExternalPaymentRejected,
                finalizeFailure);
            return false;
        }

        failure = DomainFailure.None;
        return true;
    }
}
