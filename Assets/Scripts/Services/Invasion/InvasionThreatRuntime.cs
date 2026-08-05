using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public sealed class InvasionThreatPersistenceState
{
    public InvasionThreatPersistenceState(
        float currentThreat,
        float secondsSinceLastInvasion,
        float safetyRemaining,
        float candidateDelayRemaining,
        float warningCooldownRemaining,
        bool warningRaisedThisCycle,
        bool candidateRaisedThisCycle,
        float residualRisk,
        InvasionThreatFactors lastFactors)
    {
        CurrentThreat = Mathf.Max(0f, currentThreat);
        SecondsSinceLastInvasion = Mathf.Max(0f, secondsSinceLastInvasion);
        SafetyRemaining = Mathf.Max(0f, safetyRemaining);
        CandidateDelayRemaining = candidateDelayRemaining < 0f
            ? -1f
            : Mathf.Max(0f, candidateDelayRemaining);
        WarningCooldownRemaining = Mathf.Max(0f, warningCooldownRemaining);
        WarningRaisedThisCycle = warningRaisedThisCycle;
        CandidateRaisedThisCycle = candidateRaisedThisCycle;
        ResidualRisk = Mathf.Max(0f, residualRisk);
        LastFactors = lastFactors;
    }

    public float CurrentThreat { get; }
    public float SecondsSinceLastInvasion { get; }
    public float SafetyRemaining { get; }
    public float CandidateDelayRemaining { get; }
    public float WarningCooldownRemaining { get; }
    public bool WarningRaisedThisCycle { get; }
    public bool CandidateRaisedThisCycle { get; }
    public float ResidualRisk { get; }
    public InvasionThreatFactors LastFactors { get; }
}

public class InvasionThreatRuntime : MonoBehaviour
{
    private const int PreparationEndDay = 3;

    [SerializeField] private InvasionThreatSettings settings = new InvasionThreatSettings();

    private InvasionAggregateStateStore aggregateStateStore;
    private float endlessDefenseThreatMultiplier = 1f;
    private IInvasionThreatWorldSampler worldSampler;
    private IRunVariableRuntimeReader runVariableReader;
    private IMetaProgressionRuntimeReader metaProgressionReader;
    private IGameClock gameClock;
    private IGameEventBus gameEventBus;
    private IWorldThreatModifierQuery worldThreatModifiers;
    private IExperiencePacingRuntime experiencePacing;
    private IDisposable invasionStartedSubscription;
    private IDisposable invasionResolvedSubscription;
    private IDisposable operatingDayStartedSubscription;
    private IRandomStream randomStream;

    private InvasionThreatAggregateState State =>
        (aggregateStateStore
            ?? throw new InvalidOperationException(
                $"{nameof(InvasionThreatRuntime)} is not constructed."))
        .Threat;
    private float currentThreat
    {
        get => State.CurrentThreat;
        set => State.CurrentThreat = value;
    }
    private float secondsSinceLastInvasion
    {
        get => State.SecondsSinceLastInvasion;
        set => State.SecondsSinceLastInvasion = value;
    }
    private float safetyRemaining
    {
        get => State.SafetyRemaining;
        set => State.SafetyRemaining = value;
    }
    private float candidateDelayRemaining
    {
        get => State.CandidateDelayRemaining;
        set => State.CandidateDelayRemaining = value;
    }
    private float warningCooldownRemaining
    {
        get => State.WarningCooldownRemaining;
        set => State.WarningCooldownRemaining = value;
    }
    private bool warningRaisedThisCycle
    {
        get => State.WarningRaisedThisCycle;
        set => State.WarningRaisedThisCycle = value;
    }
    private bool candidateRaisedThisCycle
    {
        get => State.CandidateRaisedThisCycle;
        set => State.CandidateRaisedThisCycle = value;
    }
    private float residualRisk
    {
        get => State.ResidualRisk;
        set => State.ResidualRisk = value;
    }
    private InvasionThreatFactors lastFactors
    {
        get => State.LastFactors;
        set => State.LastFactors = value;
    }

    public float CurrentThreat => currentThreat;
    public float SafetyRemaining => safetyRemaining;
    public bool IsCandidatePending => candidateDelayRemaining >= 0f;
    public InvasionThreatStage CurrentStage => ResolveStage();
    public InvasionThreatSnapshot LatestSnapshot => BuildSnapshot();
    public InvasionThreatSettings Settings => settings;
    public float EndlessDefenseThreatMultiplier => endlessDefenseThreatMultiplier;

    public void SetEndlessDefenseThreatMultiplier(float multiplier)
    {
        endlessDefenseThreatMultiplier = Mathf.Max(1f, multiplier);
    }

    public InvasionThreatPersistenceState CapturePersistentState()
    {
        return new InvasionThreatPersistenceState(
            currentThreat,
            secondsSinceLastInvasion,
            safetyRemaining,
            candidateDelayRemaining,
            warningCooldownRemaining,
            warningRaisedThisCycle,
            candidateRaisedThisCycle,
            residualRisk,
            lastFactors);
    }

    public void RestorePersistentState(InvasionThreatPersistenceState source)
    {
        source ??= new InvasionThreatPersistenceState(
            0f,
            0f,
            0f,
            -1f,
            0f,
            false,
            false,
            0f,
            default);
        currentThreat = source.CurrentThreat;
        secondsSinceLastInvasion = source.SecondsSinceLastInvasion;
        safetyRemaining = source.SafetyRemaining;
        candidateDelayRemaining = source.CandidateDelayRemaining;
        warningCooldownRemaining = source.WarningCooldownRemaining;
        warningRaisedThisCycle = source.WarningRaisedThisCycle;
        candidateRaisedThisCycle = source.CandidateRaisedThisCycle;
        residualRisk = source.ResidualRisk;
        lastFactors = source.LastFactors;
    }

    [Inject]
    public void Construct(
        IInvasionThreatWorldSampler worldSampler,
        IRunVariableRuntimeReader runVariableReader,
        IMetaProgressionRuntimeReader metaProgressionReader,
        IGameClock gameClock,
        IGameEventBus gameEventBus,
        IRandomStreamProvider randomStreamProvider,
        IWorldThreatModifierQuery worldThreatModifiers,
        IExperiencePacingRuntime experiencePacing,
        InvasionAggregateStateStore aggregateStateStore)
    {
        this.worldSampler = worldSampler
            ?? throw new ArgumentNullException(nameof(worldSampler));
        this.runVariableReader = runVariableReader
            ?? throw new ArgumentNullException(nameof(runVariableReader));
        this.metaProgressionReader = metaProgressionReader
            ?? throw new ArgumentNullException(nameof(metaProgressionReader));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.worldThreatModifiers = worldThreatModifiers;
        this.experiencePacing = experiencePacing;
        this.aggregateStateStore = aggregateStateStore
            ?? throw new ArgumentNullException(nameof(aggregateStateStore));
        randomStream = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("invasion-threat");
        SubscribeToScopedEvents();
    }

    private void Update()
    {
        if (worldSampler == null || runVariableReader == null || metaProgressionReader == null)
        {
            return;
        }

        Tick(gameClock.DeltaTime);
    }

    private void Start()
    {
        BeginInitialSafetyIfFresh();
    }

    public void Tick(float deltaTime)
    {
        float safeDelta = Mathf.Max(0f, deltaTime);
        if (safeDelta <= 0f)
        {
            return;
        }

        if (warningCooldownRemaining > 0f)
        {
            warningCooldownRemaining = Mathf.Max(0f, warningCooldownRemaining - safeDelta);
        }

        if (safetyRemaining > 0f)
        {
            safetyRemaining = Mathf.Max(0f, safetyRemaining - safeDelta);
            secondsSinceLastInvasion += safeDelta;
            return;
        }

        if (experiencePacing != null && !experiencePacing.AllowsRandomInvasion)
        {
            secondsSinceLastInvasion += safeDelta;
            return;
        }

        secondsSinceLastInvasion += safeDelta;
        lastFactors = SampleWorldFactors();
        if (residualRisk > 0f)
        {
            lastFactors = new InvasionThreatFactors(
                lastFactors.dungeonValue,
                lastFactors.reputation,
                lastFactors.time,
                lastFactors.risk + residualRisk);
        }

        currentThreat += InvasionThreatCalculator.CalculateRisePerSecond(
            settings,
            lastFactors,
            RequireRunVariableReader().GetThreatRiseMultiplier() * endlessDefenseThreatMultiplier) * safeDelta;
        currentThreat = Mathf.Max(0f, currentThreat);

        TryRaiseWarning();
        TickCandidateDelay(safeDelta);
    }

    public void AddThreat(float amount)
    {
        currentThreat = Mathf.Max(0f, currentThreat + amount);
        lastFactors = SampleWorldFactors();
        TryRaiseWarning();
        TickCandidateDelay(0f);
    }

    public void DebugSetThreat(float value)
    {
        currentThreat = Mathf.Max(0f, value);
        warningRaisedThisCycle = false;
        candidateRaisedThisCycle = false;
        candidateDelayRemaining = -1f;
        lastFactors = SampleWorldFactors();
        TryRaiseWarning();
    }

    public bool ForceCandidateNow()
    {
        return ForceCandidateNow(
            "최종 침공 임박",
            "던전의 운명을 가를 적이 입구로 접근하고 있습니다.");
    }

    public bool ForceCandidateNow(string title, string detail)
    {
        if (settings == null || candidateRaisedThisCycle)
        {
            return false;
        }

        safetyRemaining = 0f;
        float thresholdMultiplier = GetWarningThresholdMultiplier();
        currentThreat = Mathf.Max(
            currentThreat,
            settings.candidateThreshold * Mathf.Max(0.05f, thresholdMultiplier));
        lastFactors = SampleWorldFactors();
        warningRaisedThisCycle = true;
        candidateDelayRemaining = 0f;
        candidateRaisedThisCycle = true;

        InvasionThreatSnapshot snapshot = BuildSnapshot();
        gameEventBus.Publish(new InvasionCandidateEvent(snapshot));
        gameEventBus.RaiseAlert(
            string.IsNullOrWhiteSpace(title) ? "침입 임박" : title,
            string.IsNullOrWhiteSpace(detail)
                ? InvasionThreatCalculator.BuildCandidateDetail(snapshot)
                : detail,
            EventAlertImportance.High,
            "침입");
        return true;
    }

    public void OnTriggerEvent(InvasionStartedEvent eventType)
    {
        ResetAfterInvasion();
    }

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        residualRisk = Mathf.Max(0f, eventType.residualRisk);
        if (!eventType.defended)
        {
            residualRisk += 2f;
        }
    }

    public void OnTriggerEvent(OperatingDayStartedEvent eventType)
    {
        experiencePacing?.AdvanceToDay(eventType.day);
        if (eventType.day <= PreparationEndDay)
        {
            if (settings != null)
            {
                safetyRemaining = Mathf.Max(
                    safetyRemaining,
                    settings.GetInitialSafetyDuration());
            }

            return;
        }

        if (experiencePacing != null && !experiencePacing.AllowsRandomInvasion)
        {
            return;
        }

        AddThreat(Mathf.Min(6f, eventType.day * 0.5f));
    }

    private void BeginInitialSafetyIfFresh()
    {
        if (settings == null
            || currentThreat > 0f
            || secondsSinceLastInvasion > 0f
            || safetyRemaining > 0f
            || candidateDelayRemaining >= 0f
            || warningRaisedThisCycle
            || candidateRaisedThisCycle)
        {
            return;
        }

        safetyRemaining = settings.GetInitialSafetyDuration();
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        invasionStartedSubscription?.Dispose();
        invasionStartedSubscription = null;
        invasionResolvedSubscription?.Dispose();
        invasionResolvedSubscription = null;
        operatingDayStartedSubscription?.Dispose();
        operatingDayStartedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        invasionStartedSubscription ??=
            gameEventBus.Subscribe<InvasionStartedEvent>(OnTriggerEvent);
        invasionResolvedSubscription ??=
            gameEventBus.Subscribe<InvasionResolvedEvent>(OnTriggerEvent);
        operatingDayStartedSubscription ??=
            gameEventBus.Subscribe<OperatingDayStartedEvent>(OnTriggerEvent);
    }

    private void TryRaiseWarning()
    {
        if (experiencePacing != null && !experiencePacing.AllowsRandomInvasion)
        {
            return;
        }
        float thresholdMultiplier = GetWarningThresholdMultiplier();
        float warningThreshold = settings != null
            ? settings.warningThreshold * Mathf.Max(0.05f, thresholdMultiplier)
            : 0f;

        if (settings == null
            || currentThreat < warningThreshold
            || warningRaisedThisCycle
            || warningCooldownRemaining > 0f)
        {
            return;
        }

        warningRaisedThisCycle = true;
        warningCooldownRemaining = settings.warningCooldownSeconds;
        InvasionThreatSnapshot snapshot = BuildSnapshot();
        gameEventBus.Publish(new InvasionThreatWarningEvent(snapshot));
        gameEventBus.RaiseAlert(
            "침입 경고",
            InvasionThreatCalculator.BuildWarningDetail(snapshot),
            EventAlertImportance.Medium,
            "침입");
    }

    private void TickCandidateDelay(float deltaTime)
    {
        if (settings == null
            || candidateRaisedThisCycle
            || (experiencePacing != null && !experiencePacing.AllowsRandomInvasion))
        {
            return;
        }

        float thresholdMultiplier = GetWarningThresholdMultiplier();
        float candidateThreshold = settings.candidateThreshold * Mathf.Max(0.05f, thresholdMultiplier);
        if (currentThreat < candidateThreshold)
        {
            return;
        }

        if (candidateDelayRemaining < 0f)
        {
            candidateDelayRemaining = settings.GetCandidateDelay(randomStream);
            return;
        }

        candidateDelayRemaining = Mathf.Max(0f, candidateDelayRemaining - Mathf.Max(0f, deltaTime));
        if (candidateDelayRemaining > 0f)
        {
            return;
        }

        candidateRaisedThisCycle = true;
        InvasionThreatSnapshot snapshot = BuildSnapshot();
        gameEventBus.Publish(new InvasionCandidateEvent(snapshot));
        gameEventBus.RaiseAlert(
            "침입 임박",
            InvasionThreatCalculator.BuildCandidateDetail(snapshot),
            EventAlertImportance.High,
            "침입");
    }

    private void ResetAfterInvasion()
    {
        currentThreat = 0f;
        secondsSinceLastInvasion = 0f;
        safetyRemaining = settings != null ? Mathf.Max(0f, settings.safetyDurationSeconds) : 0f;
        candidateDelayRemaining = -1f;
        warningCooldownRemaining = 0f;
        warningRaisedThisCycle = false;
        candidateRaisedThisCycle = false;
        lastFactors = default;
    }

    private InvasionThreatSnapshot BuildSnapshot()
    {
        return new InvasionThreatSnapshot(
            currentThreat,
            ResolveStage(),
            lastFactors,
            candidateDelayRemaining,
            safetyRemaining);
    }

    private InvasionThreatStage ResolveStage()
    {
        if (safetyRemaining > 0f)
        {
            return InvasionThreatStage.Safety;
        }

        if (candidateDelayRemaining >= 0f || candidateRaisedThisCycle)
        {
            return InvasionThreatStage.Candidate;
        }

        float thresholdMultiplier = GetWarningThresholdMultiplier();
        float warningThreshold = settings != null
            ? settings.warningThreshold * Mathf.Max(0.05f, thresholdMultiplier)
            : 0f;
        if (settings != null && currentThreat >= warningThreshold)
        {
            return InvasionThreatStage.Warning;
        }

        return InvasionThreatStage.Peaceful;
    }

    private InvasionThreatFactors SampleWorldFactors()
    {
        if (worldSampler == null)
        {
            throw new InvalidOperationException($"{nameof(InvasionThreatRuntime)} requires {nameof(IInvasionThreatWorldSampler)} injection.");
        }

        return worldSampler.Sample(secondsSinceLastInvasion);
    }

    private float GetWarningThresholdMultiplier()
    {
        return RequireRunVariableReader().GetWarningThresholdMultiplier()
            * RequireMetaProgressionReader().GetInvasionWarningThresholdMultiplier()
            * (worldThreatModifiers?.GetMultiplier(
                OffenseThreatModifierKind.InvasionWarning) ?? 1f);
    }

    private IRunVariableRuntimeReader RequireRunVariableReader()
    {
        if (runVariableReader == null)
        {
            throw new InvalidOperationException($"{nameof(InvasionThreatRuntime)} requires {nameof(IRunVariableRuntimeReader)} injection.");
        }

        return runVariableReader;
    }

    private IMetaProgressionRuntimeReader RequireMetaProgressionReader()
    {
        if (metaProgressionReader == null)
        {
            throw new InvalidOperationException($"{nameof(InvasionThreatRuntime)} requires {nameof(IMetaProgressionRuntimeReader)} injection.");
        }

        return metaProgressionReader;
    }
}
