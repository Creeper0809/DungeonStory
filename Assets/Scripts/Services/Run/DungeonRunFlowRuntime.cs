using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public enum DungeonRunPhase
{
    Preparation = 0,
    Growth = 1,
    Escalation = 2,
    EndlessDefense = 3,
    [Obsolete("Use EndlessDefense.")]
    FinalChallenge = EndlessDefense,
    Finished = 4,
    [Obsolete("Truth is offense progress, not a defense phase.")]
    TruthHunt = 5
}

public enum DungeonRunOutcome
{
    None,
    Victory,
    Defeat
}

public interface IDungeonRunFlowRuntime
{
    DungeonRunPhase Phase { get; }
    DungeonRunOutcome Outcome { get; }
    int CurrentDay { get; }
    int BossCycle { get; }
    bool IsBossArmed { get; }
    bool IsBossActive { get; }
    bool IsFinalInvasionDefended { get; }
    void RestoreState(
        DungeonRunPhase phase,
        DungeonRunOutcome outcome,
        int currentDay,
        bool bossArmed,
        bool bossActive,
        bool finalInvasionDefended = false,
        int bossCycle = 0);
}

public sealed class DungeonRunFlowRuntime :
    IStartable,
    IDisposable,
    IDungeonRunFlowRuntime
{
    private const int FirstBossDay = 10;
    private readonly IOwnerRunManagerProvider ownerProvider;
    private readonly IInvasionThreatRuntimeProvider threatProvider;
    private readonly IInvasionDirectorRuntimeProvider directorProvider;
    private readonly IGameEventBus gameEventBus;
    private IDisposable ownerRunEndedSubscription;
    private IDisposable truthRevealedSubscription;
    private IDisposable bossInvasionStartedSubscription;
    private IDisposable invasionResolvedSubscription;
    private IDisposable operatingDayStartedSubscription;
    private bool started;
    private bool bossArmed;
    private bool bossActive;

    public DungeonRunFlowRuntime(
        IOwnerRunManagerProvider ownerProvider,
        IInvasionThreatRuntimeProvider threatProvider,
        IInvasionDirectorRuntimeProvider directorProvider,
        IGameEventBus gameEventBus)
    {
        this.ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
        this.threatProvider = threatProvider ?? throw new ArgumentNullException(nameof(threatProvider));
        this.directorProvider = directorProvider ?? throw new ArgumentNullException(nameof(directorProvider));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public DungeonRunPhase Phase { get; private set; } = DungeonRunPhase.Preparation;
    public DungeonRunOutcome Outcome { get; private set; }
    public int CurrentDay { get; private set; } = 1;
    public int BossCycle { get; private set; }
    public bool IsBossArmed => bossArmed;
    public bool IsBossActive => bossActive;
    public bool IsFinalInvasionDefended => false;

    public void Start()
    {
        if (started) return;
        started = true;
        operatingDayStartedSubscription =
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnTriggerEvent);
        invasionResolvedSubscription = gameEventBus.Subscribe<InvasionResolvedEvent>(OnTriggerEvent);
        bossInvasionStartedSubscription =
            gameEventBus.Subscribe<BossInvasionStartedEvent>(OnTriggerEvent);
        truthRevealedSubscription = gameEventBus.Subscribe<OffenseTruthRevealedEvent>(OnTriggerEvent);
        ownerRunEndedSubscription = gameEventBus.Subscribe<OwnerRunEndedEvent>(OnTriggerEvent);
    }

    public void Dispose()
    {
        if (!started) return;
        operatingDayStartedSubscription?.Dispose();
        operatingDayStartedSubscription = null;
        invasionResolvedSubscription?.Dispose();
        invasionResolvedSubscription = null;
        bossInvasionStartedSubscription?.Dispose();
        bossInvasionStartedSubscription = null;
        truthRevealedSubscription?.Dispose();
        truthRevealedSubscription = null;
        ownerRunEndedSubscription?.Dispose();
        ownerRunEndedSubscription = null;
        started = false;
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
        if (Outcome != DungeonRunOutcome.None) return;

        CurrentDay = Mathf.Max(1, eventType.day);
        DungeonRunPhase nextPhase = ResolvePhase(CurrentDay);
        if (nextPhase != Phase)
        {
            Phase = nextPhase;
            RaisePhaseAlert(nextPhase);
        }

        int dueCycle = CurrentDay / FirstBossDay;
        if (dueCycle > BossCycle && !bossArmed && !bossActive)
        {
            ScheduleBossInvasion(dueCycle);
        }
    }

    public void OnTriggerEvent(BossInvasionStartedEvent eventType)
    {
        if (Outcome != DungeonRunOutcome.None) return;
        bossArmed = false;
        bossActive = true;
        Phase = DungeonRunPhase.EndlessDefense;
    }

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        if (Outcome != DungeonRunOutcome.None) return;

        if (!eventType.defended)
        {
            bossActive = false;
            bossArmed = false;
            gameEventBus.RaiseAlert(
                "방어선 돌파",
                "침공을 막지 못했습니다. 이번 런은 패배로 끝납니다.",
                EventAlertImportance.High,
                "침입");
            CompleteRun(DungeonRunOutcome.Defeat, "침공 방어 실패");
            return;
        }

        if (bossActive)
        {
            bossActive = false;
            gameEventBus.RaiseAlert(
                "보스 침공 방어",
                $"{BossCycle}차 보스 침공을 버텼습니다. 방어전은 계속됩니다.",
                EventAlertImportance.Medium,
                "침입");
            return;
        }

        if (bossArmed) ForceArmedInvasion();
    }

    public void OnTriggerEvent(OffenseTruthRevealedEvent eventType)
    {
        if (Outcome != DungeonRunOutcome.None) return;

        string truthTitle = string.IsNullOrWhiteSpace(eventType.title)
            ? OffenseWorldMapService.TruthTitle
            : eventType.title;
        string truthText = string.IsNullOrWhiteSpace(eventType.truthText)
            ? OffenseWorldMapService.TruthRevealText
            : eventType.truthText;
        CompleteRun(DungeonRunOutcome.Victory, $"{truthTitle} 발견: {truthText}");
    }

    public void OnTriggerEvent(OwnerRunEndedEvent eventType)
    {
        Outcome = eventType.Outcome == DungeonRunOutcome.None
            ? DungeonRunOutcome.Defeat
            : eventType.Outcome;
        bossArmed = false;
        bossActive = false;
        Phase = DungeonRunPhase.Finished;
    }

    public void RestoreState(
        DungeonRunPhase phase,
        DungeonRunOutcome outcome,
        int currentDay,
        bool restoredBossArmed,
        bool restoredBossActive,
        bool legacyFinalInvasionDefended = false,
        int restoredBossCycle = 0)
    {
        CurrentDay = Mathf.Max(1, currentDay);
        Outcome = outcome;
        BossCycle = Mathf.Max(
            restoredBossCycle,
            legacyFinalInvasionDefended ? 1 : Mathf.Max(0, CurrentDay / FirstBossDay));
        Phase = outcome == DungeonRunOutcome.None
            ? ResolvePhase(CurrentDay)
            : DungeonRunPhase.Finished;
        bossArmed = outcome == DungeonRunOutcome.None && restoredBossArmed;
        bossActive = outcome == DungeonRunOutcome.None && restoredBossActive;

        ApplyThreatCycleMultiplier(BossCycle);
        if (bossArmed && directorProvider.TryGetRuntime(out InvasionDirectorRuntime director))
        {
            director.ArmNextInvasionAsBoss(
                ResolveBossHealthMultiplier(BossCycle),
                ResolveBossDamageMultiplier(BossCycle));
        }

        if (ownerProvider.TryGetManager(out OwnerRunManager ownerManager) && ownerManager != null)
        {
            ownerManager.RestoreRunEnded(Outcome != DungeonRunOutcome.None);
        }
    }

    public static float ResolveBossHealthMultiplier(int cycle)
    {
        return 1f + 0.35f * Mathf.Max(0, cycle);
    }

    public static float ResolveBossDamageMultiplier(int cycle)
    {
        return 1f + 0.35f * Mathf.Max(0, cycle);
    }

    public static float ResolveThreatRiseMultiplier(int cycle)
    {
        return 1f + 0.2f * Mathf.Max(0, cycle);
    }

    private void ScheduleBossInvasion(int cycle)
    {
        if (!directorProvider.TryGetRuntime(out InvasionDirectorRuntime director))
        {
            Debug.LogWarning("Boss invasion could not be armed because the invasion director is missing.");
            return;
        }

        BossCycle = Mathf.Max(1, cycle);
        bossArmed = true;
        director.ArmNextInvasionAsBoss(
            ResolveBossHealthMultiplier(BossCycle),
            ResolveBossDamageMultiplier(BossCycle));
        director.WithdrawActiveIntrudersForFinalInvasion();
        ApplyThreatCycleMultiplier(BossCycle);
        gameEventBus.RaiseAlert(
            $"{BossCycle}차 보스 침공",
            $"보스 체력과 돌파 피해가 {ResolveBossHealthMultiplier(BossCycle):0.##}배로 강화됩니다.",
            EventAlertImportance.High,
            "침입");
        ForceArmedInvasion();
    }

    private void ApplyThreatCycleMultiplier(int cycle)
    {
        if (threatProvider.TryGetRuntime(out InvasionThreatRuntime threat))
        {
            threat.SetEndlessDefenseThreatMultiplier(ResolveThreatRiseMultiplier(cycle));
        }
    }

    private void ForceArmedInvasion()
    {
        if (threatProvider.TryGetRuntime(out InvasionThreatRuntime threat))
        {
            threat.ForceCandidateNow();
        }
    }

    private void CompleteRun(DungeonRunOutcome outcome, string reason)
    {
        if (!ownerProvider.TryGetManager(out OwnerRunManager ownerManager)
            || ownerManager == null
            || !ownerManager.CompleteRun(outcome, reason))
        {
            Debug.LogWarning($"Run completion could not be delivered to {nameof(OwnerRunManager)}.");
        }
    }

    private static DungeonRunPhase ResolvePhase(int day)
    {
        if (day >= FirstBossDay) return DungeonRunPhase.EndlessDefense;
        if (day >= 7) return DungeonRunPhase.Escalation;
        if (day >= 4) return DungeonRunPhase.Growth;
        return DungeonRunPhase.Preparation;
    }

    private void RaisePhaseAlert(DungeonRunPhase phase)
    {
        switch (phase)
        {
            case DungeonRunPhase.Growth:
                gameEventBus.RaiseAlert(
                    "성장 단계",
                    "직원과 시설을 늘리고 연구와 생산을 확장할 때입니다.",
                    EventAlertImportance.Medium,
                    "운영");
                break;
            case DungeonRunPhase.Escalation:
                gameEventBus.RaiseAlert(
                    "위기 단계",
                    "침공이 거세집니다. 방어선을 보강하고 오펜스 원정대를 준비하세요.",
                    EventAlertImportance.High,
                    "침입");
                break;
            case DungeonRunPhase.EndlessDefense:
                gameEventBus.RaiseAlert(
                    "무한 방어",
                    "방어전은 계속 강해집니다. 승리하려면 오펜스 끝에서 진실을 밝혀야 합니다.",
                    EventAlertImportance.High,
                    "오펜스");
                break;
        }
    }
}
