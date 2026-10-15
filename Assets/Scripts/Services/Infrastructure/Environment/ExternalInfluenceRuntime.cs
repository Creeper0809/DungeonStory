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
        float before = state.renown;
        ExternalInfluenceDomainRules.AddRenown(state, amount);
        RecordValueChange(
            ExternalInfluenceOperationKind.RenownChange,
            source,
            amount,
            before,
            state.renown);
    }

    public void AddDread(float amount, string source)
    {
        float before = state.dread;
        ExternalInfluenceDomainRules.AddDread(state, amount);
        RecordValueChange(
            ExternalInfluenceOperationKind.DreadChange,
            source,
            amount,
            before,
            state.dread);
    }

    public void AddHostileRumor(float amount, string source)
    {
        float before = state.hostileRumor;
        ExternalInfluenceDomainRules.AddHostileRumor(state, amount);
        RecordValueChange(
            ExternalInfluenceOperationKind.HostileRumorChange,
            source,
            amount,
            before,
            state.hostileRumor);
    }

    public void AddEcologyPressure(float amount, string source)
    {
        float before = state.ecologyPressure;
        ExternalInfluenceDomainRules.AddEcologyPressure(state, amount);
        RecordValueChange(
            ExternalInfluenceOperationKind.EcologyPressureChange,
            source,
            amount,
            before,
            state.ecologyPressure);
        CheckEcologyThresholds();
    }

    public void AddScoutingLabor(
        float amount,
        int reconLevel,
        int newlyRevealed)
    {
        float before = state.scoutingLabor;
        ExternalInfluenceDomainRules.AddScoutingLabor(state, amount);
        ExternalInfluenceOperationSaveData operation = CreateOperation(
            $"external-influence:recon:{reconLevel}",
            ExternalInfluenceOperationKind.ReconUpgrade,
            ExternalInfluenceOperationPhase.Completed,
            nameof(OffenseWorldMapRuntime),
            $"recon:{reconLevel}");
        operation.requestedAmount = Math.Max(0f, amount);
        operation.appliedAmount = Math.Max(0f, state.scoutingLabor - before);
        operation.valueBefore = before;
        operation.valueAfter = state.scoutingLabor;
        operation.newlyRevealed = Math.Max(0, newlyRevealed);
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

        string operationId =
            $"external-influence:rumor:{state.currentOperatingDay}:{method}";
        string financeTransactionId = string.Empty;
        float rumorBefore = state.hostileRumor;
        if (method == HostileRumorMitigationMethod.Gold)
        {
            if (money is not IIdempotentGameMoneyAccount idempotentMoney)
            {
                failure = new DomainFailure(
                    FailureCode.ExternalPaymentRejected,
                    "idempotent-money-account-required");
                return false;
            }
            if (!money.CanSpend(cost))
            {
                failure = new DomainFailure(
                    FailureCode.InsufficientGold,
                    money.Balance.ToString(),
                    cost.ToString());
                return false;
            }
            if (!idempotentMoney.TrySpendOnce(
                    cost,
                    new EconomyTransactionContext(
                        EconomyTransactionKind.Bribe,
                        operationId,
                        $"rumor:{state.currentOperatingDay}",
                        "적대적 소문 수습"),
                    out EconomyTransactionRecord receipt,
                    out string paymentFailure))
            {
                failure = new DomainFailure(
                    FailureCode.ExternalPaymentRejected,
                    paymentFailure);
                return false;
            }
            financeTransactionId = receipt?.transactionId ?? string.Empty;
        }

        ExternalInfluenceOperationSaveData operation = GetOrCreateOperation(
            operationId,
            ExternalInfluenceOperationKind.RumorMitigation,
            method == HostileRumorMitigationMethod.Gold
                ? ExternalInfluenceOperationPhase.CostCommitted
                : ExternalInfluenceOperationPhase.Applied,
            "circus-panel",
            $"rumor:{state.currentOperatingDay}");
        operation.costKind = method == HostileRumorMitigationMethod.Gold
            ? ExternalInfluenceCostKind.Gold
            : ExternalInfluenceCostKind.Renown;
        operation.costAmount = cost;
        operation.requestedAmount = reducedAmount;
        operation.valueBefore = rumorBefore;
        operation.financeTransactionId = financeTransactionId;
        ExternalInfluenceDomainRules.CommitRumorMitigation(
            state,
            method,
            reducedAmount,
            cost);
        operation.appliedAmount = Math.Max(0f, rumorBefore - state.hostileRumor);
        operation.valueAfter = state.hostileRumor;
        operation.phase = ExternalInfluenceOperationPhase.Completed;
        failure = DomainFailure.None;
        return true;
    }

    public bool TryArmDreadDefense(out DomainFailure failure)
    {
        float before = state.dread;
        if (!ExternalInfluenceDomainRules.TryArmDreadDefense(
            state,
            rules.DreadDefenseCost,
            out failure))
        {
            return false;
        }

        ExternalInfluenceOperationSaveData operation = CreateOperation(
            operationId: null,
            ExternalInfluenceOperationKind.DreadDefense,
            ExternalInfluenceOperationPhase.CostCommitted,
            "circus-panel",
            "invasion:pending");
        operation.costKind = ExternalInfluenceCostKind.Dread;
        operation.costAmount = rules.DreadDefenseCost;
        operation.valueBefore = before;
        operation.valueAfter = state.dread;
        operation.appliedAmount = Math.Max(0f, before - state.dread);
        return true;
    }

    public bool BeginInvasionDread(bool boss)
    {
        if (!ExternalInfluenceDomainRules.BeginInvasionDread(
                aggregateState,
                boss))
        {
            return false;
        }

        ExternalInfluenceOperationSaveData operation = FindLatestOperation(
            ExternalInfluenceOperationKind.DreadDefense,
            ExternalInfluenceOperationPhase.CostCommitted);
        if (operation != null)
        {
            operation.phase = ExternalInfluenceOperationPhase.ResultPublished;
            operation.targetId = boss ? "invasion:boss" : "invasion:regular";
        }
        return true;
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

        if (ExternalInfluenceTrailCharmOutbox.HasPending(state))
        {
            string pendingSiteId = state.pendingTrailCharmSiteId;
            string pendingCommitId = state.pendingTrailCharmCommitId;
            if (!ExternalInfluenceTrailCharmOutbox.TryFinalizePending(
                    aggregateState,
                    batchDispositions,
                    out string pendingFailure))
            {
                RecordTrailCharmIntelPhase(
                    pendingSiteId,
                    pendingCommitId,
                    pendingFailure);
                failure = new DomainFailure(
                    FailureCode.ExternalPaymentRejected,
                    pendingFailure);
                return false;
            }
            CompleteTrailCharmIntelOperation(
                pendingSiteId,
                pendingCommitId);
        }

        if (ExternalInfluenceDomainRules.IsIntelUnlocked(
                aggregateState,
                normalized))
        {
            failure = DomainFailure.None;
            return true;
        }

        string operationId = FormatIntelOperationId(normalized);
        string financeTransactionId = string.Empty;
        string physicalCommitId = string.Empty;
        float valueBefore = GetIntelCostBalance(payment);
        bool paid = payment switch
        {
            ExpeditionIntelPaymentMethod.Renown =>
                TrySpendRenown(out failure),
            ExpeditionIntelPaymentMethod.Gold =>
                TrySpendGoldForIntel(
                    normalized,
                    operationId,
                    out financeTransactionId,
                    out failure),
            ExpeditionIntelPaymentMethod.ScoutingLabor =>
                TrySpendScouting(out failure),
            ExpeditionIntelPaymentMethod.TrailCharm =>
                TryConsumeTrailCharm(
                    normalized,
                    out physicalCommitId,
                    out failure),
            _ => throw new ArgumentOutOfRangeException(
                nameof(payment),
                payment,
                null)
        };
        if (!paid)
        {
            if (payment == ExpeditionIntelPaymentMethod.TrailCharm
                && ExternalInfluenceTrailCharmOutbox.HasPending(state))
            {
                RecordTrailCharmIntelPhase(
                    normalized,
                    state.pendingTrailCharmCommitId,
                    failure.Code.ToString());
            }
            return false;
        }

        ExternalInfluenceOperationSaveData operation = GetOrCreateOperation(
            operationId,
            ExternalInfluenceOperationKind.IntelUnlock,
            ExternalInfluenceOperationPhase.CostCommitted,
            "external-influence:intel-unlock",
            normalized);
        operation.costKind = GetIntelCostKind(payment);
        operation.costAmount = GetIntelCostAmount(payment);
        operation.valueBefore = valueBefore;
        operation.valueAfter = GetIntelCostBalance(payment);
        operation.appliedAmount = Math.Max(
            0f,
            operation.valueBefore - operation.valueAfter);
        operation.financeTransactionId = financeTransactionId;
        operation.physicalCommitId = physicalCommitId;
        if (payment != ExpeditionIntelPaymentMethod.TrailCharm)
        {
            ExternalInfluenceDomainRules.UnlockIntel(
                aggregateState,
                normalized);
        }
        operation.phase = ExternalInfluenceOperationPhase.Completed;
        operation.failureReason = string.Empty;
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
        if (ExternalInfluenceTrailCharmOutbox.HasPending(restored.Data))
        {
            string pendingSiteId = restored.Data.pendingTrailCharmSiteId;
            string pendingCommitId = restored.Data.pendingTrailCharmCommitId;
            if (!ExternalInfluenceTrailCharmOutbox.TryFinalizePending(
                    restored,
                    batchDispositions,
                    out string failureReason))
            {
                RecordTrailCharmIntelPhase(
                    pendingSiteId,
                    pendingCommitId,
                    failureReason);
                throw new InvalidOperationException(
                    "External-influence trail-charm restore reconciliation failed: "
                    + failureReason);
            }
            CompleteTrailCharmIntelOperation(
                pendingSiteId,
                pendingCommitId);
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

        float renownBefore = state.renown;
        float rumorBefore = state.hostileRumor;
        ExternalInfluenceDomainRules.ApplyOperatingDayReport(
            state,
            report.totalVisits,
            report.averageSatisfaction);
        string source = $"operating-day-report:{state.currentOperatingDay}";
        RecordValueChange(
            ExternalInfluenceOperationKind.RenownChange,
            source,
            state.renown - renownBefore,
            renownBefore,
            state.renown);
        RecordValueChange(
            ExternalInfluenceOperationKind.HostileRumorChange,
            source,
            state.hostileRumor - rumorBefore,
            rumorBefore,
            state.hostileRumor);
    }

    private void OnOperatingDayStarted(OperatingDayStartedEvent eventType)
    {
        SurvivalEnvironmentSnapshot environment =
            survival.GetEnvironmentSnapshot();
        int exposedFood = wildlife.GetReachableFoodRaidTargets().Count;
        float ecologyBefore = state.ecologyPressure;
        ExternalInfluencePressureResult pressure =
            ExternalInfluenceDomainRules.BeginOperatingDay(
            state,
            eventType.day,
            environment.Weather == SurvivalWeatherType.ColdSnap,
            exposedFood);
        RecordValueChange(
            ExternalInfluenceOperationKind.EcologyPressureChange,
            $"operating-day:{eventType.day}",
            pressure.Total,
            ecologyBefore,
            state.ecologyPressure);
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

        string raidId = FormatEcologyRaidOperationId(state.ecologyRaidSequence);
        ExternalInfluenceOperationSaveData pressureSource =
            FindLatestOperation(ExternalInfluenceOperationKind.EcologyPressureChange);
        CreateOperation(
            raidId,
            ExternalInfluenceOperationKind.EcologyRaid,
            ExternalInfluenceOperationPhase.Applied,
            pressureSource?.sourceId ?? "external-influence:ecology-threshold",
            raidId);

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
            ExternalInfluenceOperationSaveData failed =
                GetRequiredOperation(raidId);
            failed.phase = ExternalInfluenceOperationPhase.Failed;
            failed.failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "wildlife-raid-start-rejected"
                : failureReason;
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
        ExternalInfluenceOperationSaveData started = GetRequiredOperation(raidId);
        started.phase = ExternalInfluenceOperationPhase.ResultPublished;
        started.resultCount = orders.Count;
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
        string raidId = FormatEcologyRaidOperationId(state.ecologyRaidSequence);
        ExternalInfluenceOperationSaveData resolved = GetRequiredOperation(raidId);
        resolved.phase = ExternalInfluenceOperationPhase.Completed;
        resolved.resultCount = stolen;
        resolved.failureReason = cancelled > 0
            ? $"cancelled:{cancelled}"
            : string.Empty;
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
        ExternalInfluenceOperationSaveData operation = FindLatestOperation(
            ExternalInfluenceOperationKind.DreadDefense,
            ExternalInfluenceOperationPhase.ResultPublished);
        string intruderId = ExternalInfluenceDomainRules.NormalizeId(
            eventType.intruderActor?.Identity?.PersistentId);
        if (operation != null && intruderId.Length > 0)
        {
            operation.targetId = intruderId;
            operation.resultCount = dreadAffectedIntruders.Count;
        }
    }

    private void EndDreadDefense()
    {
        ExternalInfluenceDomainRules.EndInvasionDread(aggregateState);
        ExternalInfluenceOperationSaveData operation = FindLatestOperation(
            ExternalInfluenceOperationKind.DreadDefense,
            ExternalInfluenceOperationPhase.ResultPublished);
        if (operation != null)
        {
            operation.phase = ExternalInfluenceOperationPhase.Completed;
        }
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
        string operationId,
        out string transactionId,
        out DomainFailure failure)
    {
        transactionId = string.Empty;
        if (money is not IIdempotentGameMoneyAccount idempotentMoney)
        {
            failure = new DomainFailure(
                FailureCode.ExternalPaymentRejected,
                "idempotent-money-account-required");
            return false;
        }
        if (!money.CanSpend(rules.GoldIntelCost))
        {
            failure = new DomainFailure(
                FailureCode.InsufficientGold,
                money.Balance.ToString(),
                rules.GoldIntelCost.ToString());
            return false;
        }

        if (!idempotentMoney.TrySpendOnce(
                rules.GoldIntelCost,
                new EconomyTransactionContext(
                    EconomyTransactionKind.Bribe,
                    operationId,
                    siteId,
                    "external-intel"),
                out EconomyTransactionRecord receipt,
                out string paymentFailure))
        {
            failure = new DomainFailure(
                FailureCode.ExternalPaymentRejected,
                paymentFailure);
            return false;
        }

        transactionId = receipt?.transactionId ?? string.Empty;
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
        out string physicalCommitId,
        out DomainFailure failure)
    {
        physicalCommitId = string.Empty;
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

        physicalCommitId = receipt.CommitId;
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

    private void RecordValueChange(
        ExternalInfluenceOperationKind kind,
        string source,
        float requestedAmount,
        float before,
        float after)
    {
        if (Math.Abs(after - before) <= 0.0001f)
        {
            return;
        }

        ExternalInfluenceOperationSaveData operation = CreateOperation(
            operationId: null,
            kind,
            ExternalInfluenceOperationPhase.Completed,
            RequireCanonicalId(source, nameof(source)),
            string.Empty);
        operation.requestedAmount = requestedAmount;
        operation.appliedAmount = after - before;
        operation.valueBefore = before;
        operation.valueAfter = after;
    }

    private ExternalInfluenceOperationSaveData CreateOperation(
        string operationId,
        ExternalInfluenceOperationKind kind,
        ExternalInfluenceOperationPhase phase,
        string sourceId,
        string targetId)
    {
        if (state.nextOperationRevision <= 0L
            || state.nextOperationRevision == long.MaxValue)
        {
            throw new InvalidOperationException(
                "External-influence operation revision is exhausted.");
        }
        state.operations ??= new List<ExternalInfluenceOperationSaveData>();
        long revision = state.nextOperationRevision;
        string normalizedOperationId = string.IsNullOrWhiteSpace(operationId)
            ? $"external-influence:operation:{revision:D8}"
            : RequireCanonicalId(operationId, nameof(operationId));
        if (state.operations.Any(candidate => candidate != null
            && string.Equals(
                candidate.operationId,
                normalizedOperationId,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"External-influence operation '{normalizedOperationId}' already exists.");
        }

        ExternalInfluenceOperationSaveData operation = new()
        {
            revision = revision,
            operationId = normalizedOperationId,
            kind = kind,
            phase = phase,
            sourceId = RequireCanonicalId(sourceId, nameof(sourceId)),
            targetId = NormalizeOptionalId(targetId),
            recordedDay = state.currentOperatingDay
        };
        state.operations.Add(operation);
        state.nextOperationRevision = checked(revision + 1L);
        return operation;
    }

    private ExternalInfluenceOperationSaveData GetOrCreateOperation(
        string operationId,
        ExternalInfluenceOperationKind kind,
        ExternalInfluenceOperationPhase phase,
        string sourceId,
        string targetId)
    {
        ExternalInfluenceOperationSaveData existing = state.operations?
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.operationId,
                    operationId,
                    StringComparison.Ordinal));
        if (existing == null)
        {
            return CreateOperation(
                operationId,
                kind,
                phase,
                sourceId,
                targetId);
        }
        if (existing.kind != kind
            || !string.Equals(existing.sourceId, sourceId, StringComparison.Ordinal)
            || !string.Equals(existing.targetId, targetId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"External-influence operation '{operationId}' conflicts with its saved identity.");
        }
        return existing;
    }

    private ExternalInfluenceOperationSaveData GetRequiredOperation(
        string operationId) => state.operations?
        .FirstOrDefault(candidate => candidate != null
            && string.Equals(
                candidate.operationId,
                operationId,
                StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"External-influence operation '{operationId}' is missing.");

    private ExternalInfluenceOperationSaveData FindLatestOperation(
        ExternalInfluenceOperationKind kind,
        ExternalInfluenceOperationPhase? phase = null) => state.operations?
        .Where(candidate => candidate != null
            && candidate.kind == kind
            && (!phase.HasValue || candidate.phase == phase.Value))
        .OrderByDescending(candidate => candidate.revision)
        .FirstOrDefault();

    private void RecordTrailCharmIntelPhase(
        string siteId,
        string physicalCommitId,
        string failureReason)
    {
        string normalized = RequireCanonicalId(siteId, nameof(siteId));
        ExternalInfluenceOperationSaveData operation = GetOrCreateOperation(
            FormatIntelOperationId(normalized),
            ExternalInfluenceOperationKind.IntelUnlock,
            ExternalInfluenceOperationPhase.CostCommitted,
            "external-influence:intel-unlock",
            normalized);
        operation.costKind = ExternalInfluenceCostKind.TrailCharm;
        operation.costAmount = 1f;
        operation.valueBefore = Math.Max(1f, operation.valueBefore);
        operation.valueAfter = 0f;
        operation.appliedAmount = 1f;
        operation.physicalCommitId =
            RequireCanonicalId(physicalCommitId, nameof(physicalCommitId));
        operation.phase = state.trailCharmCommitPhase
            == ExternalInfluenceTrailCharmCommitPhase.IntelPublished
                ? ExternalInfluenceOperationPhase.ResultPublished
                : ExternalInfluenceOperationPhase.CostCommitted;
        operation.failureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "trail-charm-finalization-incomplete"
            : failureReason.Trim();
    }

    private void CompleteTrailCharmIntelOperation(
        string siteId,
        string physicalCommitId)
    {
        string normalized = RequireCanonicalId(siteId, nameof(siteId));
        ExternalInfluenceOperationSaveData operation = GetOrCreateOperation(
            FormatIntelOperationId(normalized),
            ExternalInfluenceOperationKind.IntelUnlock,
            ExternalInfluenceOperationPhase.Completed,
            "external-influence:intel-unlock",
            normalized);
        operation.costKind = ExternalInfluenceCostKind.TrailCharm;
        operation.costAmount = 1f;
        operation.appliedAmount = 1f;
        operation.physicalCommitId =
            RequireCanonicalId(physicalCommitId, nameof(physicalCommitId));
        operation.phase = ExternalInfluenceOperationPhase.Completed;
        operation.failureReason = string.Empty;
    }

    private float GetIntelCostBalance(ExpeditionIntelPaymentMethod payment) =>
        payment switch
        {
            ExpeditionIntelPaymentMethod.Renown => state.renown,
            ExpeditionIntelPaymentMethod.Gold => money.Balance,
            ExpeditionIntelPaymentMethod.ScoutingLabor => state.scoutingLabor,
            ExpeditionIntelPaymentMethod.TrailCharm => items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        trailCharmItemId.Value,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity),
            _ => throw new ArgumentOutOfRangeException(
                nameof(payment),
                payment,
                null)
        };

    private ExternalInfluenceCostKind GetIntelCostKind(
        ExpeditionIntelPaymentMethod payment) => payment switch
        {
            ExpeditionIntelPaymentMethod.Renown =>
                ExternalInfluenceCostKind.Renown,
            ExpeditionIntelPaymentMethod.Gold =>
                ExternalInfluenceCostKind.Gold,
            ExpeditionIntelPaymentMethod.ScoutingLabor =>
                ExternalInfluenceCostKind.ScoutingLabor,
            ExpeditionIntelPaymentMethod.TrailCharm =>
                ExternalInfluenceCostKind.TrailCharm,
            _ => throw new ArgumentOutOfRangeException(
                nameof(payment),
                payment,
                null)
        };

    private float GetIntelCostAmount(
        ExpeditionIntelPaymentMethod payment) => payment switch
        {
            ExpeditionIntelPaymentMethod.Renown => rules.RenownIntelCost,
            ExpeditionIntelPaymentMethod.Gold => rules.GoldIntelCost,
            ExpeditionIntelPaymentMethod.ScoutingLabor => rules.ScoutingIntelCost,
            ExpeditionIntelPaymentMethod.TrailCharm => 1f,
            _ => throw new ArgumentOutOfRangeException(
                nameof(payment),
                payment,
                null)
        };

    private static string FormatIntelOperationId(string siteId) =>
        $"external-influence:intel:{siteId}";

    private static string FormatEcologyRaidOperationId(int sequence) =>
        $"ecology:{sequence}";

    private static string RequireCanonicalId(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0
            || !string.Equals(normalized, value, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical non-empty ID is required.",
                parameterName);
        }
        return normalized;
    }

    private static string NormalizeOptionalId(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (!string.Equals(normalized, value ?? string.Empty, StringComparison.Ordinal))
        {
            throw new ArgumentException("Optional IDs must already be canonical.");
        }
        return normalized;
    }
}
