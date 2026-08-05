using System;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public class DungeonRunFlowApplicationAdapter :
    IStartable,
    ITickable,
    IDisposable,
    IDungeonRunFlowRuntime,
    IDungeonRunFlowRestorePublisher
{
    private readonly IOwnerRunManagerProvider ownerProvider;
    private readonly InvasionThreatRuntime threat;
    private readonly InvasionDirectorRuntime director;
    private readonly IGameEventBus gameEventBus;
    private readonly IExperiencePacingRuntime experiencePacing;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly CoreSessionRulesDefinition rules;

    [ApplicationAdapterTransientState]
    private IDisposable ownerRunEndedSubscription;
    [ApplicationAdapterTransientState]
    private IDisposable truthRevealedSubscription;
    [ApplicationAdapterTransientState]
    private IDisposable bossInvasionStartedSubscription;
    [ApplicationAdapterTransientState]
    private IDisposable invasionResolvedSubscription;
    [ApplicationAdapterTransientState]
    private IDisposable operatingDayStartedSubscription;
    [ApplicationAdapterTransientState]
    private int projectedRestoreRevision;

    public DungeonRunFlowApplicationAdapter(
        IOwnerRunManagerProvider ownerProvider,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IGameEventBus gameEventBus,
        IExperiencePacingRuntime experiencePacing,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICoreSessionRulesProvider rulesProvider)
    {
        this.ownerProvider = ownerProvider
            ?? throw new ArgumentNullException(nameof(ownerProvider));
        invasionRuntimes = invasionRuntimes
            ?? throw new ArgumentNullException(nameof(invasionRuntimes));
        threat = invasionRuntimes.Threat
            ?? throw new InvalidOperationException(
                "Run flow requires a loaded invasion threat runtime.");
        director = invasionRuntimes.Director
            ?? throw new InvalidOperationException(
                "Run flow requires a loaded invasion director runtime.");
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.experiencePacing = experiencePacing
            ?? throw new ArgumentNullException(nameof(experiencePacing));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        rules = (rulesProvider
                ?? throw new ArgumentNullException(nameof(rulesProvider)))
            .CoreSessionRules
            ?? throw new InvalidOperationException(
                "Core-session rules are not authored.");
    }

    public DungeonRunPhase Phase => State.Phase;
    public DungeonRunOutcome Outcome => State.Outcome;
    public int CurrentDay => State.CurrentDay;
    public int BossCycle => State.BossCycle;
    public bool IsBossArmed => State.BossArmed;
    public bool IsBossActive => State.BossActive;

    private DungeonRunFlowAggregateState State =>
        aggregateRootStore.GetOrCreate(
            () => new DungeonRunFlowAggregateState());

    public void Start()
    {
        if (operatingDayStartedSubscription != null)
        {
            return;
        }

        EnsureProjectionCurrent();
        projectedRestoreRevision = aggregateRootStore.PublishedRestoreRevision;
        operatingDayStartedSubscription =
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnTriggerEvent);
        invasionResolvedSubscription =
            gameEventBus.Subscribe<InvasionResolvedEvent>(OnTriggerEvent);
        bossInvasionStartedSubscription =
            gameEventBus.Subscribe<BossInvasionStartedEvent>(OnTriggerEvent);
        truthRevealedSubscription =
            gameEventBus.Subscribe<OffenseTruthRevealedEvent>(OnTriggerEvent);
        ownerRunEndedSubscription =
            gameEventBus.Subscribe<OwnerRunEndedEvent>(OnTriggerEvent);
    }

    public void Dispose()
    {
        DisposeSubscription(ref operatingDayStartedSubscription);
        DisposeSubscription(ref invasionResolvedSubscription);
        DisposeSubscription(ref bossInvasionStartedSubscription);
        DisposeSubscription(ref truthRevealedSubscription);
        DisposeSubscription(ref ownerRunEndedSubscription);
    }

    public void Tick()
    {
        int revision = aggregateRootStore.PublishedRestoreRevision;
        if (projectedRestoreRevision == revision)
        {
            return;
        }

        projectedRestoreRevision = revision;
        EnsureProjectionCurrent();
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType) =>
        ApplyTransition(DungeonRunFlowReducer.Reduce(
            State,
            DungeonRunFlowEvent.DayStarted(eventType.day),
            rules));

    public void OnTriggerEvent(BossInvasionStartedEvent eventType) =>
        ApplyTransition(DungeonRunFlowReducer.Reduce(
            State,
            DungeonRunFlowEvent.BossInvasionStarted(),
            rules));

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        int rehearsalDay = experiencePacing.IsRehearsalActive
            ? experiencePacing.ActiveRehearsalDay
            : 0;
        ApplyTransition(DungeonRunFlowReducer.Reduce(
            State,
            DungeonRunFlowEvent.InvasionResolved(
                eventType.defended,
                rehearsalDay),
            rules));
    }

    public void OnTriggerEvent(OffenseTruthRevealedEvent eventType)
    {
        string title = string.IsNullOrWhiteSpace(eventType.title)
            ? OffenseWorldMapService.TruthTitle
            : eventType.title;
        string text = string.IsNullOrWhiteSpace(eventType.truthText)
            ? OffenseWorldMapService.TruthRevealText
            : eventType.truthText;
        ApplyTransition(
            DungeonRunFlowReducer.Reduce(
                State,
                DungeonRunFlowEvent.TruthRevealed(),
                rules),
            $"{title} 발견: {text}");
    }

    public void OnTriggerEvent(OwnerRunEndedEvent eventType) =>
        ApplyTransition(DungeonRunFlowReducer.Reduce(
            State,
            DungeonRunFlowEvent.OwnerRunEnded(eventType.Outcome),
            rules));

    public void RestoreState(
        DungeonRunPhase phase,
        DungeonRunOutcome outcome,
        int currentDay,
        bool bossArmed,
        bool bossActive,
        int bossCycle)
    {
        PublishRestoreState(new DungeonRunFlowAggregateState
        {
            CurrentDay = currentDay,
            Outcome = outcome,
            BossCycle = bossCycle,
            Phase = phase,
            BossArmed = bossArmed,
            BossActive = bossActive
        });
        if (!aggregateRootStore.IsRestoreStaging)
        {
            EnsureProjectionCurrent();
        }
    }

    public void PublishRestoreState(DungeonRunFlowAggregateState candidate) =>
        aggregateRootStore.Replace(candidate
            ?? throw new ArgumentNullException(nameof(candidate)));

    private void ApplyTransition(
        DungeonRunFlowTransition transition,
        string completionReason = null)
    {
        if (transition == null)
        {
            throw new ArgumentNullException(nameof(transition));
        }
        if (transition.StateChanged)
        {
            aggregateRootStore.Replace(transition.State);
        }

        foreach (DungeonRunFlowEffect effect in transition.Effects)
        {
            ExecuteEffect(effect, completionReason);
        }
    }

    private void ExecuteEffect(
        DungeonRunFlowEffect effect,
        string completionReason)
    {
        switch (effect.Kind)
        {
            case DungeonRunFlowEffectKind.AdvancePacingDay:
                experiencePacing.AdvanceToDay(effect.Day);
                break;
            case DungeonRunFlowEffectKind.RaisePhaseAlert:
                RaisePhaseAlert(effect.Phase);
                break;
            case DungeonRunFlowEffectKind.EvaluateRehearsal:
                bool scheduled = TryScheduleRehearsal(effect.Day);
                ApplyTransition(DungeonRunFlowReducer.Reduce(
                    State,
                    DungeonRunFlowEvent.RehearsalSchedulingResolved(
                        effect.Day,
                        scheduled,
                        effect.BossCycle),
                    rules));
                break;
            case DungeonRunFlowEffectKind.ScheduleBossInvasion:
                ScheduleBossInvasion(effect.BossCycle);
                break;
            case DungeonRunFlowEffectKind.ResolveRehearsal:
                experiencePacing.ResolveRehearsal();
                break;
            case DungeonRunFlowEffectKind.RaiseRehearsalResolvedAlert:
                RaiseRehearsalResolvedAlert(effect.Day, effect.Defended);
                break;
            case DungeonRunFlowEffectKind.RaiseDefenseFailedAlert:
                gameEventBus.RaiseAlert(
                    "방어선 돌파",
                    "침공을 막지 못했습니다. 이번 운영 주기는 패배로 끝납니다.",
                    EventAlertImportance.High,
                    "침입");
                break;
            case DungeonRunFlowEffectKind.RaiseBossDefendedAlert:
                gameEventBus.RaiseAlert(
                    "보스 침공 방어",
                    $"{effect.BossCycle}차 보스 침공을 버텼습니다. 방어전은 계속됩니다.",
                    EventAlertImportance.Medium,
                    "침입");
                break;
            case DungeonRunFlowEffectKind.ForceArmedInvasion:
                ForceArmedInvasion();
                break;
            case DungeonRunFlowEffectKind.CompleteRun:
                CompleteRun(
                    effect.Outcome,
                    effect.Outcome == DungeonRunOutcome.Victory
                        ? completionReason ?? "진실 발견"
                        : "침공 방어 실패");
                break;
        }
    }

    private void EnsureProjectionCurrent()
    {
        DungeonRunFlowAggregateState current = State;
        ApplyThreatCycleMultiplier(current.BossCycle);
        if (current.BossArmed)
        {
            director.ArmNextInvasionAsBoss(
                DungeonRunFlowReducer.ResolveBossHealthMultiplier(
                    current.BossCycle),
                DungeonRunFlowReducer.ResolveBossDamageMultiplier(
                    current.BossCycle));
        }
        if (ownerProvider.TryGetManager(out OwnerRunManager ownerManager)
            && ownerManager != null)
        {
            ownerManager.RestoreRunEnded(
                current.Outcome != DungeonRunOutcome.None);
        }
    }

    private void ScheduleBossInvasion(int cycle)
    {
        director.ArmNextInvasionAsBoss(
            DungeonRunFlowReducer.ResolveBossHealthMultiplier(cycle),
            DungeonRunFlowReducer.ResolveBossDamageMultiplier(cycle));
        director.WithdrawActiveIntrudersForFinalInvasion();
        ApplyThreatCycleMultiplier(cycle);
        gameEventBus.RaiseAlert(
            $"{cycle}차 보스 침공",
            $"보스 체력과 돌파 피해가 {DungeonRunFlowReducer.ResolveBossHealthMultiplier(cycle):0.##}배로 강화됩니다.",
            EventAlertImportance.High,
            "침입");
        ForceArmedInvasion();
    }

    private void ApplyThreatCycleMultiplier(int cycle) =>
        threat.SetEndlessDefenseThreatMultiplier(
            DungeonRunFlowReducer.ResolveThreatRiseMultiplier(cycle));

    private void ForceArmedInvasion() => threat.ForceCandidateNow();

    private bool TryScheduleRehearsal(int day)
    {
        if (!experiencePacing.TryBeginRehearsal(
                day,
                out RehearsalInvasionProfile profile))
        {
            return false;
        }
        if (!director.ArmNextInvasionAsRehearsal(
                profile.PowerMultiplier,
                profile.OwnerDamageMultiplier,
                profile.RetreatHealthRatio))
        {
            director.CancelArmedRehearsal();
            experiencePacing.ResolveRehearsal();
            Debug.LogWarning(
                $"Day {day} rehearsal invasion could not be armed.");
            return false;
        }
        if (!threat.ForceCandidateNow(
                $"{day}일차 침입 징후",
                $"기존 전력의 약 {profile.PowerMultiplier * 100f:0}% 규모가 입구로 접근하고 있습니다."))
        {
            director.CancelArmedRehearsal();
            experiencePacing.ResolveRehearsal();
            Debug.LogWarning(
                $"Day {day} rehearsal invasion candidate could not be raised.");
            return false;
        }
        return true;
    }

    private void RaiseRehearsalResolvedAlert(int day, bool defended)
    {
        gameEventBus.RaiseAlert(
            $"{day}일차 침입 종료",
            defended
                ? "침입자가 물러났습니다."
                : "침입자가 목표 일부를 달성한 뒤 철수했습니다.",
            defended
                ? EventAlertImportance.Low
                : EventAlertImportance.Medium,
            "침입");
    }

    private void CompleteRun(DungeonRunOutcome outcome, string reason)
    {
        if (!ownerProvider.TryGetManager(out OwnerRunManager ownerManager)
            || ownerManager == null
            || !ownerManager.CompleteRun(outcome, reason))
        {
            Debug.LogWarning(
                $"Run completion could not be delivered to {nameof(OwnerRunManager)}.");
        }
    }

    private void RaisePhaseAlert(DungeonRunPhase phase)
    {
        switch (phase)
        {
            case DungeonRunPhase.Growth:
                gameEventBus.RaiseAlert(
                    "운영 활동 증가",
                    "손님 방문과 시설 이용 기록이 늘어나기 시작했습니다.",
                    EventAlertImportance.Medium,
                    "운영");
                break;
            case DungeonRunPhase.Escalation:
                gameEventBus.RaiseAlert(
                    "침입 위협 증가",
                    "사전 준비에서 무장과 인원의 움직임이 확인됐습니다.",
                    EventAlertImportance.High,
                    "침입");
                break;
            case DungeonRunPhase.EndlessDefense:
                gameEventBus.RaiseAlert(
                    "정규 공세 시작",
                    "인간 원정군이 정상 전력으로 집결했습니다.",
                    EventAlertImportance.High,
                    "오펜스");
                break;
        }
    }

    private static void DisposeSubscription(ref IDisposable subscription)
    {
        subscription?.Dispose();
        subscription = null;
    }
}
