using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class InvasionDirectorRuntime : MonoBehaviour
{
    public const float MinimumRallyDurationSeconds = 12f;

    [SerializeField] private CharacterSO intruderData;
    [SerializeField] private GameObject intruderPrefab;
    [SerializeField] private InvasionIntruderSettings intruderSettings = new InvasionIntruderSettings();
    [SerializeField, Min(0f)] private float normalOwnerBreachDamage =
        InvasionOwnerDamageTuning.DefaultNormalBreachDamage;
    [SerializeField, Min(0f)] private float bossOwnerBreachDamage =
        InvasionOwnerDamageTuning.DefaultBossBreachDamage;

    private readonly List<InvasionIntruderRuntime> activeIntruders = new List<InvasionIntruderRuntime>();
    private IReadOnlyList<InvasionIntruderRuntime> activeIntrudersView;
    private IInvasionIntruderContext invasionContext;
    private IInvasionIntruderDataProvider intruderDataProvider;
    private IInvasionIntruderFactory intruderFactory;
    private IDefenseStatusRuntimeService defenseStatusRuntimeService;
    private IGameClock gameClock;
    private IGameEventBus gameEventBus;
    private IRandomStreamProvider randomStreamProvider;
    private IDisposable invasionCandidateSubscription;
    private IDisposable invasionResolvedSubscription;
    private bool nextInvasionIsBoss;
    private float nextBossHealthMultiplier = 1f;
    private float nextBossDamageMultiplier = 1f;
    private CharacterActor ralliedOwner;
    private Coroutine ownerRallyRoutine;

    public IReadOnlyList<InvasionIntruderRuntime> ActiveIntruders =>
        activeIntrudersView ??= ReadOnlyView.List(activeIntruders);
    public bool IsBossArmed => nextInvasionIsBoss;

    public bool ArmNextInvasionAsBoss()
    {
        return ArmNextInvasionAsBoss(1f, 1f);
    }

    public bool ArmNextInvasionAsBoss(float healthMultiplier, float damageMultiplier)
    {
        if (nextInvasionIsBoss)
        {
            return false;
        }

        nextInvasionIsBoss = true;
        nextBossHealthMultiplier = Mathf.Max(1f, healthMultiplier);
        nextBossDamageMultiplier = Mathf.Max(1f, damageMultiplier);
        return true;
    }

    [Inject]
    public void Construct(
        IInvasionIntruderContext invasionContext,
        IInvasionIntruderDataProvider intruderDataProvider,
        IInvasionIntruderFactory intruderFactory,
        IDefenseStatusRuntimeService defenseStatusRuntimeService,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IGameEventBus gameEventBus)
    {
        this.invasionContext = invasionContext
            ?? throw new ArgumentNullException(nameof(invasionContext));
        this.intruderDataProvider = intruderDataProvider
            ?? throw new ArgumentNullException(nameof(intruderDataProvider));
        this.intruderFactory = intruderFactory
            ?? throw new ArgumentNullException(nameof(intruderFactory));
        this.defenseStatusRuntimeService = defenseStatusRuntimeService
            ?? throw new ArgumentNullException(nameof(defenseStatusRuntimeService));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.randomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
    }

    private void OnInvasionCandidate(InvasionCandidateEvent eventType)
    {
        TrySpawnIntruder(eventType.snapshot, out _);
    }

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        ReleaseOwnerRally();
    }

    public bool TrySpawnIntruder(InvasionThreatSnapshot snapshot, out CharacterActor intruder)
    {
        intruder = null;
        CharacterSO data = ResolveIntruderData();
        if (data == null)
        {
            gameEventBus.RaiseInvasionResult("침입자 데이터가 없어 침입을 시작하지 못했습니다.", EventAlertImportance.High);
            return false;
        }

        IInvasionIntruderContext context = ResolveInvasionContext();
        if (!context.TryResolveEntry(out InvasionIntruderEntry entry))
        {
            gameEventBus.RaiseInvasionResult("침입자가 들어올 수 있는 입구를 찾지 못했습니다.", EventAlertImportance.High);
            return false;
        }

        InvasionIntruderRuntime runtime = ResolveIntruderFactory().Create(intruderPrefab, entry.OutsidePosition);
        runtime.Initialize(
            context,
            ResolveDefenseStatusRuntimeService(),
            gameClock,
            randomStreamProvider,
            gameEventBus);
        intruder = runtime.IntruderActor;
        bool isBoss = nextInvasionIsBoss;
        InvasionIntruderSettings effectiveSettings = context.ApplyRunVariables(intruderSettings);
        effectiveSettings.rallyDurationSeconds = Mathf.Max(
            MinimumRallyDurationSeconds,
            effectiveSettings.rallyDurationSeconds);

        float runAdjustedOwnerDamage = effectiveSettings.finalCombatDamage;
        if (isBoss)
        {
            effectiveSettings.patternId = InvasionIntruderPatternIds.Executioner;
            effectiveSettings.rallyDurationSeconds = Mathf.Max(
                0f,
                effectiveSettings.rallyDurationSeconds * 1.5f);
            effectiveSettings.secondsToFullFocus = Mathf.Max(0.1f, effectiveSettings.secondsToFullFocus * 0.5f);
            effectiveSettings.repathIntervalSeconds = Mathf.Max(0.1f, effectiveSettings.repathIntervalSeconds * 0.7f);
            effectiveSettings.facilityDamageIntervalSeconds = Mathf.Max(0f, effectiveSettings.facilityDamageIntervalSeconds * 0.6f);
            effectiveSettings.healthMultiplier = nextBossHealthMultiplier;
            effectiveSettings.meleeDamageMultiplier = Mathf.Max(
                0.01f,
                effectiveSettings.meleeDamageMultiplier * nextBossDamageMultiplier);
        }

        effectiveSettings.finalCombatDamage = InvasionOwnerDamageTuning.Resolve(
            intruderSettings.finalCombatDamage,
            runAdjustedOwnerDamage,
            isBoss,
            normalOwnerBreachDamage,
            bossOwnerBreachDamage * (isBoss ? nextBossDamageMultiplier : 1f));
        InvasionIntruderPatternDefinition pattern = InvasionIntruderPatternCatalog.Resolve(
            effectiveSettings.patternId);
        effectiveSettings.patternId = pattern.id;

        Vector2Int? finalDefenseTarget = null;

        runtime.Begin(
            data,
            snapshot,
            effectiveSettings,
            entry.OutsidePosition,
            entry.DoorPosition,
            entry.GridPosition,
            finalDefenseTarget);
        activeIntruders.Add(runtime);
        runtime.OnFinished += OnIntruderFinished;
        nextInvasionIsBoss = false;
        nextBossHealthMultiplier = 1f;
        nextBossDamageMultiplier = 1f;

        gameEventBus.Publish(new InvasionStartedEvent(snapshot));
        gameEventBus.Publish(new InvasionSpawnedEvent(intruder, snapshot));
        if (isBoss)
        {
            gameEventBus.Publish(new BossInvasionStartedEvent(intruder, snapshot));
        }
        gameEventBus.RaiseAlert(
            isBoss ? $"최종 침공 집결 · {pattern.title}" : $"침입자 집결 · {pattern.title}",
            $"침입자들이 외부에서 집결 중입니다. 약 {Mathf.CeilToInt(effectiveSettings.rallyDurationSeconds)}초 뒤 진입을 시작합니다.",
            EventAlertImportance.High,
            "침입");
        return true;
    }

    public IReadOnlyList<InvasionIntruderPersistenceState> CapturePersistentState(Grid grid)
    {
        if (grid == null)
        {
            return Array.Empty<InvasionIntruderPersistenceState>();
        }

        return activeIntruders
            .Where(runtime => runtime != null
                && runtime.State != InvasionIntruderState.Finished
                && runtime.IntruderActor != null
                && !runtime.IntruderActor.IsDead)
            .Select(runtime => runtime.CapturePersistentState(grid))
            .ToArray();
    }

    public int RestorePersistentState(
        IEnumerable<InvasionIntruderPersistenceState> restoredIntruders,
        IList<string> warnings)
    {
        ClearForPersistentRestore();
        CharacterSO data = ResolveIntruderData();
        if (data == null)
        {
            warnings?.Add("Invasion intruder data was not present; active intruders were skipped.");
            return 0;
        }

        int restoredCount = 0;
        foreach (InvasionIntruderPersistenceState source in restoredIntruders
            ?? Array.Empty<InvasionIntruderPersistenceState>())
        {
            if (source == null)
            {
                continue;
            }

            if (source.DataId >= 0 && source.DataId != data.id)
            {
                warnings?.Add($"Invasion intruder data {source.DataId} no longer exists.");
                continue;
            }

            InvasionIntruderRuntime runtime = ResolveIntruderFactory().Create(
                intruderPrefab,
                source.WorldPosition);
            runtime.Initialize(
                ResolveInvasionContext(),
                ResolveDefenseStatusRuntimeService(),
                gameClock,
                randomStreamProvider,
                gameEventBus);
            Vector2Int? finalDefenseTarget = null;

            if (!runtime.TryRestore(data, source, finalDefenseTarget, out string warning))
            {
                warnings?.Add(warning);
                runtime.ReleaseForPersistentRestore();
                continue;
            }

            activeIntruders.Add(runtime);
            runtime.OnFinished += OnIntruderFinished;
            restoredCount++;
        }

        return restoredCount;
    }

    public void ClearForPersistentRestore()
    {
        ReleaseOwnerRally();
        foreach (InvasionIntruderRuntime runtime in activeIntruders.ToArray())
        {
            if (runtime == null)
            {
                continue;
            }

            runtime.OnFinished -= OnIntruderFinished;
            runtime.ReleaseForPersistentRestore();
        }

        activeIntruders.Clear();
    }

    public int WithdrawActiveIntrudersForFinalInvasion()
    {
        InvasionIntruderRuntime[] withdrawing = activeIntruders
            .Where(runtime => runtime != null)
            .ToArray();
        foreach (InvasionIntruderRuntime runtime in withdrawing)
        {
            runtime.OnFinished -= OnIntruderFinished;
            runtime.ReleaseForPersistentRestore();
        }

        activeIntruders.Clear();
        return withdrawing.Length;
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        invasionCandidateSubscription?.Dispose();
        invasionCandidateSubscription = null;
        invasionResolvedSubscription?.Dispose();
        invasionResolvedSubscription = null;
        ReleaseOwnerRally();
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        invasionCandidateSubscription ??=
            gameEventBus.Subscribe<InvasionCandidateEvent>(OnInvasionCandidate);
        invasionResolvedSubscription ??=
            gameEventBus.Subscribe<InvasionResolvedEvent>(OnTriggerEvent);
    }

    private CharacterSO ResolveIntruderData()
    {
        intruderData = ResolveIntruderDataProvider().GetRequiredIntruderData(intruderData);
        return intruderData;
    }

    private IInvasionIntruderDataProvider ResolveIntruderDataProvider()
    {
        return intruderDataProvider
            ?? throw new InvalidOperationException($"{nameof(InvasionDirectorRuntime)} requires {nameof(IInvasionIntruderDataProvider)} injection.");
    }

    private IInvasionIntruderContext ResolveInvasionContext()
    {
        return invasionContext
            ?? throw new InvalidOperationException($"{nameof(InvasionDirectorRuntime)} requires {nameof(IInvasionIntruderContext)} injection.");
    }

    private IInvasionIntruderFactory ResolveIntruderFactory()
    {
        return intruderFactory
            ?? throw new InvalidOperationException($"{nameof(InvasionDirectorRuntime)} requires {nameof(IInvasionIntruderFactory)} injection.");
    }

    private IDefenseStatusRuntimeService ResolveDefenseStatusRuntimeService()
    {
        return defenseStatusRuntimeService
            ?? throw new InvalidOperationException($"{nameof(InvasionDirectorRuntime)} requires {nameof(IDefenseStatusRuntimeService)} injection.");
    }

    private bool TryStartOwnerRally(
        IInvasionIntruderContext context,
        InvasionIntruderEntry entry,
        out FinalDefenseRallyPlan plan)
    {
        plan = default;
        if (context == null
            || !context.TryGetGrid(out Grid grid)
            || !context.TryGetOwner(out CharacterActor owner)
            || owner == null
            || owner.IsDead
            || !FinalDefenseRallyPlanner.TryCreate(
                grid,
                entry.GridPosition,
                owner.GetNowXY(),
                owner.PathSearchBroker,
                out plan))
        {
            return false;
        }

        ReleaseOwnerRally();
        AbilityMove ownerMove = owner.GetAbility<AbilityMove>();
        if (ownerMove == null)
        {
            return false;
        }

        owner.Brain?.RequestImmediateReplan(clearFailures: false);
        owner.SetAiPaused(true);
        ralliedOwner = owner;
        ownerRallyRoutine = StartCoroutine(RunOwnerRally(context, owner, ownerMove, plan));
        return true;
    }

    private IEnumerator RunOwnerRally(
        IInvasionIntruderContext context,
        CharacterActor owner,
        AbilityMove ownerMove,
        FinalDefenseRallyPlan plan)
    {
        Queue<GridMoveStep> path = plan.CreateOwnerPath();
        for (int attempt = 0; attempt < 3 && owner != null && !owner.IsDead; attempt++)
        {
            if (path.Count > 0)
            {
                yield return ownerMove.MoveByPath(path);
            }

            if (!context.TryGetGrid(out Grid grid) || owner.GetNowXY() == plan.Target)
            {
                break;
            }

            path = grid.GetMovePath(owner.GetNowXY(), position => position == plan.Target);
            if (path.Count == 0)
            {
                break;
            }
        }

        if (owner != null && !owner.IsDead && owner.GetNowXY() == plan.Target)
        {
            owner.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Combat,
                CharacterActivityOutcomes.Started,
                "최종 방어선 집결",
                actionId: "invasion:final-rally",
                sentiment: -0.15f,
                bubbleEligible: true));
        }

        ownerRallyRoutine = null;
    }

    private void ReleaseOwnerRally()
    {
        if (ownerRallyRoutine != null)
        {
            StopCoroutine(ownerRallyRoutine);
            ownerRallyRoutine = null;
        }

        CharacterActor owner = ralliedOwner;
        ralliedOwner = null;
        if (owner == null || owner.IsDead)
        {
            return;
        }

        owner.GetAbility<AbilityMove>()?.CancelActiveMovement();
        owner.SetAiPaused(false);
    }

    private void OnIntruderFinished(InvasionIntruderRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        runtime.OnFinished -= OnIntruderFinished;
        activeIntruders.Remove(runtime);
    }
}

public class InvasionIntruderRuntime : MonoBehaviour
{
    private CharacterActor intruderActor;
    private AbilityMove move;
    private InvasionIntruderSettings settings;
    private float elapsed;
    private float nextDamageTime;
    private Coroutine routine;
    private bool resolved;
    private IInvasionIntruderContext invasionContext;
    private IDefenseStatusRuntimeService defenseStatusRuntimeService;
    private IGameClock gameClock;
    private IGameEventBus gameEventBus;
    private IRandomStreamProvider randomStreamProvider;
    private IRandomStream pathRandomStream;
    private IDefenseEngagementRuntime defenseEngagementRuntime;
    private InvasionIntruderPatternDefinition pattern;
    private BuildableObject currentPriorityTarget;
    private bool hasFinalDefenseTarget;
    private Vector2Int finalDefenseTarget;
    private string runtimeId = string.Empty;
    private InvasionThreatSnapshot threatSnapshot;
    private float rallyRemainingSeconds;
    private bool hasBreachedDungeonInterior;
    private bool breachEventRaised;
    private readonly HashSet<int> damagedFacilityInstanceIds = new HashSet<int>();
    private int facilityDamageCount;

    public CharacterActor IntruderActor => intruderActor != null ? intruderActor : GetComponent<CharacterActor>();
    public InvasionIntruderState State { get; private set; }
    public float Focus => settings != null ? InvasionIntruderPlanner.CalculateFocus(elapsed, settings.secondsToFullFocus) : 1f;
    public float ElapsedSeconds => elapsed;
    public float DamageDelayRemaining =>
        Mathf.Max(0f, nextDamageTime - ResolveGameClock().Time);
    public float RallySecondsRemaining => State == InvasionIntruderState.Rallying
        ? Mathf.Max(0f, rallyRemainingSeconds)
        : 0f;
    public float ConfiguredRallyDurationSeconds => settings != null
        ? Mathf.Max(0f, settings.rallyDurationSeconds)
        : 0f;
    public bool HasBreachedDungeonInterior => hasBreachedDungeonInterior;
    public InvasionIntruderPatternDefinition Pattern => pattern ?? InvasionIntruderPatternCatalog.Default;
    public BuildableObject CurrentPriorityTarget => currentPriorityTarget;
    public bool HasFinalDefenseTarget => hasFinalDefenseTarget;
    public Vector2Int FinalDefenseTarget => finalDefenseTarget;
    public int FacilityDamageCount => facilityDamageCount;
    public string RuntimeId => runtimeId;
    public float MeleeDamageMultiplier => settings != null
        ? Mathf.Max(0.01f, settings.meleeDamageMultiplier)
        : 1f;
    public float AttackSpeedMultiplier => settings != null
        ? Mathf.Max(0.01f, settings.attackSpeedMultiplier)
        : 1f;

    public event Action<InvasionIntruderRuntime> OnFinished;

    public void SetEngagementState(bool engaged, Vector2Int? holdCell = null)
    {
        if (State == InvasionIntruderState.Finished)
        {
            return;
        }

        if (engaged)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            if (holdCell.HasValue
                && invasionContext != null
                && invasionContext.TryGetGrid(out Grid grid)
                && grid.IsValidGridPos(holdCell.Value))
            {
                transform.position = grid.GetWorldPos(holdCell.Value);
            }

            State = InvasionIntruderState.Engaged;
            return;
        }

        State = InvasionIntruderState.InterceptPlanned;
        ResumeInsideIfNeeded();
    }

    public void SetFrontBrokenState()
    {
        if (State != InvasionIntruderState.Finished)
        {
            State = InvasionIntruderState.FrontBroken;
            ResumeInsideIfNeeded();
        }
    }

    private void ResumeInsideIfNeeded()
    {
        if (routine != null
            || resolved
            || !isActiveAndEnabled
            || intruderActor == null
            || intruderActor.IsDead)
        {
            return;
        }

        routine = StartCoroutine(RunInside());
    }

    private void Awake()
    {
        intruderActor = GetComponent<CharacterActor>();
        move = GetComponent<AbilityMove>();
    }

    public void Initialize(
        IInvasionIntruderContext invasionContext,
        IDefenseStatusRuntimeService defenseStatusRuntimeService,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IGameEventBus gameEventBus)
    {
        this.invasionContext = invasionContext
            ?? throw new ArgumentNullException(nameof(invasionContext));
        this.defenseStatusRuntimeService = defenseStatusRuntimeService
            ?? throw new ArgumentNullException(nameof(defenseStatusRuntimeService));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.randomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public void ConfigureDefenseEngagement(IDefenseEngagementRuntime defenseEngagementRuntime)
    {
        this.defenseEngagementRuntime = defenseEngagementRuntime
            ?? throw new ArgumentNullException(nameof(defenseEngagementRuntime));
    }

    public void Begin(
        CharacterSO data,
        InvasionThreatSnapshot threatSnapshot,
        InvasionIntruderSettings settings,
        Vector3 outsidePosition,
        Vector3 entryDoorPosition,
        Vector2Int entryGridPosition,
        Vector2Int? finalDefenseTarget = null)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        this.settings = settings ?? new InvasionIntruderSettings();
        this.threatSnapshot = threatSnapshot;
        pattern = InvasionIntruderPatternCatalog.Resolve(this.settings.patternId);
        this.settings.patternId = pattern.id;
        currentPriorityTarget = null;
        damagedFacilityInstanceIds.Clear();
        facilityDamageCount = 0;
        hasFinalDefenseTarget = finalDefenseTarget.HasValue;
        this.finalDefenseTarget = finalDefenseTarget.GetValueOrDefault();
        elapsed = 0f;
        rallyRemainingSeconds = Mathf.Max(0f, this.settings.rallyDurationSeconds);
        hasBreachedDungeonInterior = false;
        breachEventRaised = false;
        nextDamageTime = ResolveGameClock().Time
            + this.settings.facilityDamageIntervalSeconds;
        resolved = false;
        runtimeId = $"invasion:{Guid.NewGuid():N}";
        pathRandomStream = ResolvePathRandomStream();
        RequireRuntimeComponents();

        transform.position = outsidePosition;
        intruderActor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
        intruderActor.Initialize(data);
        intruderActor.Identity?.SetPersistentId(runtimeId);
        intruderActor.ScaleMaxHealth(this.settings.healthMultiplier);
        intruderActor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
        routine = StartCoroutine(Run(entryDoorPosition, entryGridPosition, includeRally: true));
    }

    public InvasionIntruderPersistenceState CapturePersistentState(Grid grid)
    {
        RequireRuntimeComponents();
        CharacterSO data = intruderActor.Identity != null ? intruderActor.Identity.Data : null;
        CharacterMoodSnapshot mood = intruderActor.Stats.GetMoodSnapshot();
        DefenseStatusRuntime statusRuntime = ResolveDefenseStatusRuntimeService().Get(intruderActor);
        return new InvasionIntruderPersistenceState(
            data != null ? data.id : -1,
            transform.position,
            grid.GetXY(transform.position),
            State,
            elapsed,
            DamageDelayRemaining,
            facilityDamageCount,
            intruderActor.CurrentHealth,
            intruderActor.InjurySeverity,
            mood.BaseValue,
            intruderActor.Stats.StatSnapshot,
            settings,
            statusRuntime != null
                ? statusRuntime.ActiveStatuses
                : Array.Empty<DefenseStatusSnapshot>(),
            runtimeId,
            RallySecondsRemaining,
            hasBreachedDungeonInterior);
    }

    public bool TryRestore(
        CharacterSO data,
        InvasionIntruderPersistenceState source,
        Vector2Int? finalDefenseTarget,
        out string warning)
    {
        warning = string.Empty;
        if (data == null || source == null)
        {
            warning = "Active invasion state was incomplete.";
            return false;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        RequireRuntimeComponents();
        settings = InvasionIntruderPersistenceState.CloneSettings(source.Settings);
        threatSnapshot = default;
        pattern = InvasionIntruderPatternCatalog.Resolve(settings.patternId);
        settings.patternId = pattern.id;
        currentPriorityTarget = null;
        damagedFacilityInstanceIds.Clear();
        facilityDamageCount = source.FacilityDamageCount;
        hasFinalDefenseTarget = finalDefenseTarget.HasValue;
        this.finalDefenseTarget = finalDefenseTarget.GetValueOrDefault();
        elapsed = source.ElapsedSeconds;
        rallyRemainingSeconds = source.RallyRemainingSeconds;
        hasBreachedDungeonInterior = source.HasBreachedDungeonInterior
            || IsPostBreachState(source.State);
        breachEventRaised = hasBreachedDungeonInterior;
        nextDamageTime = ResolveGameClock().Time + source.DamageDelayRemaining;
        resolved = false;
        runtimeId = !string.IsNullOrWhiteSpace(source.RuntimeId)
            ? source.RuntimeId
            : $"invasion:{Guid.NewGuid():N}";
        pathRandomStream = ResolvePathRandomStream();
        transform.position = source.WorldPosition;
        intruderActor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
        intruderActor.Initialize(data);
        intruderActor.Identity?.SetPersistentId(runtimeId);
        intruderActor.ScaleMaxHealth(settings.healthMultiplier);
        intruderActor.Stats.RestorePersistentState(
            source.Conditions,
            source.CurrentHealth,
            source.InjurySeverity,
            source.BaseMood,
            Array.Empty<CharacterMoodFactorSnapshot>());

        DefenseStatusRuntime statusRuntime = ResolveDefenseStatusRuntimeService().GetOrAdd(intruderActor);
        foreach (DefenseStatusKind kind in Enum.GetValues(typeof(DefenseStatusKind)))
        {
            statusRuntime.ClearStatus(kind);
        }

        foreach (DefenseStatusSnapshot status in source.DefenseStatuses)
        {
            if (status.RemainingSeconds > 0f && status.Stacks > 0)
            {
                statusRuntime.ApplyStatus(status.Kind, status.Value, status.RemainingSeconds, status.Stacks);
            }
        }

        IInvasionIntruderContext context = ResolveInvasionContext();
        if (source.State == InvasionIntruderState.Rallying
            || source.State == InvasionIntruderState.Entering)
        {
            if (!context.TryResolveEntry(out InvasionIntruderEntry entry))
            {
                warning = "The active intruder entrance no longer exists.";
                return false;
            }

            State = source.State;
            intruderActor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
            routine = StartCoroutine(Run(
                entry.DoorPosition,
                entry.GridPosition,
                includeRally: source.State == InvasionIntruderState.Rallying));
            return true;
        }

        if (!context.TryGetGrid(out Grid grid))
        {
            warning = "The dungeon grid was unavailable while restoring an active intruder.";
            return false;
        }

        Vector2Int restoredPosition = source.GridPosition;
        if (!grid.IsValidGridPos(restoredPosition)
            && !grid.TryFindNearestWalkablePosition(restoredPosition, out restoredPosition))
        {
            warning = "The active intruder has no valid restore position.";
            return false;
        }

        transform.position = grid.GetWorldPos(restoredPosition);
        intruderActor.SetLifecycleState(CharacterLifecycleState.Active);
        State = source.State == InvasionIntruderState.None
            || source.State == InvasionIntruderState.Finished
            ? InvasionIntruderState.Searching
            : source.State;
        routine = StartCoroutine(RunInside());
        return true;
    }

    public void ReleaseForPersistentRestore()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        State = InvasionIntruderState.Finished;
        if (intruderActor != null)
        {
            intruderActor.SetLifecycleState(CharacterLifecycleState.Despawned);
        }

        OnFinished = null;
        gameObject.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    public Queue<GridMoveStep> CreateNextPath(Grid grid, Vector2Int ownerPosition, out bool direct)
    {
        return CreateNextPath(grid, ownerPosition, out direct, out _);
    }

    public Queue<GridMoveStep> CreateNextPath(
        Grid grid,
        Vector2Int ownerPosition,
        out bool direct,
        out BuildableObject priorityTarget)
    {
        Vector2Int start = intruderActor != null ? intruderActor.GetNowXY() : Vector2Int.zero;
        return InvasionIntruderPlanner.GetNextPath(
            grid,
            start,
            ownerPosition,
            Focus,
            intruderActor != null ? intruderActor.PathSearchBroker : null,
            ResolvePathRandomStream(),
            Pattern,
            out direct,
            out priorityTarget,
            damagedFacilityInstanceIds,
            facilityDamageCount);
    }

    public bool TryDamageNearbyFacility(Grid grid)
    {
        return TryDamageNearbyFacility(grid, currentPriorityTarget);
    }

    public bool TryDamageNearbyFacility(Grid grid, BuildableObject preferredTarget)
    {
        if (grid == null || intruderActor == null)
        {
            return false;
        }

        if (facilityDamageCount >= Pattern.maxFacilityDamageCount)
        {
            return false;
        }

        if (!InvasionFacilityDamageResolver.TryFindDamageTarget(
                grid,
                intruderActor.GetNowXY(),
                Pattern.targetPreference,
                preferredTarget,
                out BuildableObject target,
                damagedFacilityInstanceIds))
        {
            return false;
        }

        State = InvasionIntruderState.DamagingFacility;
        target.SetDamaged(true);
        damagedFacilityInstanceIds.Add(target.GetInstanceID());
        facilityDamageCount++;
        intruderActor.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Damaged,
            $"{target.name} 손상",
            target,
            actionId: "invasion:damage-facility",
            value: 1f,
            bubbleEligible: true));
        gameEventBus.Publish(new InvasionFacilityDamagedEvent(intruderActor, target));
        if (target == currentPriorityTarget)
        {
            currentPriorityTarget = null;
        }
        return true;
    }

    public void ApplyFinalCombat(CharacterActor owner)
    {
        if (owner == null || intruderActor == null || settings == null)
        {
            return;
        }

        State = InvasionIntruderState.FinalCombat;
        gameEventBus.Publish(new InvasionFinalCombatStartedEvent(intruderActor, owner));
        owner.ApplyDamage(settings.finalCombatDamage, "침입자 최종 교전");
        resolved = true;
        gameEventBus.Publish(new InvasionResolvedEvent(!owner.IsDead, owner.IsDead ? 5f : 2f));
    }

    public void ResolveSuppressedBy(CharacterActor defender)
    {
        if (resolved)
        {
            Finish();
            return;
        }

        resolved = true;
        State = InvasionIntruderState.Finished;
        intruderActor?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Defeated,
            defender != null ? $"{defender.name}에게 제압됨" : "제압됨",
            actionId: "invasion:suppressed",
            targetId: defender != null ? $"character:{defender.GetInstanceID()}" : string.Empty,
            targetName: defender != null ? defender.name : string.Empty,
            sentiment: -0.9f,
            bubbleEligible: true));
        gameEventBus.Publish(new InvasionResolvedEvent(true, 1f));
        if (intruderActor != null && !intruderActor.IsDead)
        {
            FinishAsDownedCaptureCandidate();
            return;
        }

        Finish();
    }

    public void ResolveDefenseFailed(CharacterActor owner)
    {
        if (resolved)
        {
            Finish();
            return;
        }

        resolved = true;
        intruderActor?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Completed,
            "최종 방어선 돌파",
            actionId: "invasion:owner-defeated",
            targetId: owner?.Identity?.PersistentId ?? "owner",
            targetName: owner?.Identity?.DisplayName ?? "사장",
            sentiment: 0.8f,
            bubbleEligible: true));
        gameEventBus.Publish(new InvasionResolvedEvent(false, 5f));
        Finish();
    }

    private IEnumerator Run(
        Vector3 entryDoorPosition,
        Vector2Int entryGridPosition,
        bool includeRally)
    {
        if (includeRally && rallyRemainingSeconds > 0f)
        {
            State = InvasionIntruderState.Rallying;
            while (rallyRemainingSeconds > 0f
                && intruderActor != null
                && !intruderActor.IsDead)
            {
                rallyRemainingSeconds = Mathf.Max(
                    0f,
                    rallyRemainingSeconds - ResolveGameClock().DeltaTime);
                yield return null;
            }

            if (intruderActor == null || intruderActor.IsDead)
            {
                ResolveIntruderDefeated();
                yield break;
            }

            gameEventBus.RaiseAlert(
                "침입 개시",
                "집결을 마친 침입자들이 던전 입구로 접근합니다.",
                EventAlertImportance.High,
                "침입");
        }

        State = InvasionIntruderState.Entering;
        yield return move.Move2PosBySpeed(entryDoorPosition);

        IInvasionIntruderContext context = ResolveInvasionContext();
        context.TryGetGrid(out Grid grid);
        if (grid != null && grid.IsValidGridPos(entryGridPosition))
        {
            yield return move.Move2PosBySpeed(grid.GetWorldPos(entryGridPosition));
            TryMarkDungeonBreached(grid, entryGridPosition);
        }

        intruderActor.SetLifecycleState(CharacterLifecycleState.Active);
        yield return RunInside();
    }

    private IEnumerator RunInside()
    {
        IInvasionIntruderContext context = ResolveInvasionContext();
        Grid grid;
        while (State != InvasionIntruderState.Finished && intruderActor != null && !intruderActor.IsDead)
        {
            context.TryGetGrid(out grid);
            context.TryGetOwner(out CharacterActor owner);
            if (grid == null || owner == null || owner.IsDead)
            {
                Finish();
                yield break;
            }

            TryMarkDungeonBreached(grid, intruderActor.GetNowXY());

            if (defenseEngagementRuntime?.ShouldHoldIntruder(this) ?? false)
            {
                currentPriorityTarget = null;
                yield return null;
                continue;
            }

            if (hasFinalDefenseTarget && intruderActor.GetNowXY() == finalDefenseTarget)
            {
                hasFinalDefenseTarget = false;
            }

            if (!hasFinalDefenseTarget
                && InvasionIntruderPlanner.IsAtOwner(grid, intruderActor, owner))
            {
                if (defenseEngagementRuntime != null)
                {
                    defenseEngagementRuntime.TryBeginOwnerFinalDefense(this, owner);
                    yield return null;
                    continue;
                }

                yield return FinalCombat(owner);
                yield break;
            }

            elapsed += Mathf.Max(0.01f, settings.repathIntervalSeconds);
            Queue<GridMoveStep> path = CreateNextPath(
                grid,
                hasFinalDefenseTarget ? finalDefenseTarget : owner.GetNowXY(),
                out bool direct,
                out BuildableObject priorityTarget);
            currentPriorityTarget = priorityTarget;
            State = priorityTarget != null
                ? InvasionIntruderState.MovingToFacility
                : direct
                    ? InvasionIntruderState.MovingToOwner
                    : InvasionIntruderState.Searching;

            if (hasBreachedDungeonInterior
                && !(defenseEngagementRuntime?.ShouldHoldIntruder(this) ?? false)
                && ResolveGameClock().Time >= nextDamageTime)
            {
                TryDamageNearbyFacility(grid, currentPriorityTarget);
                nextDamageTime = ResolveGameClock().Time
                    + settings.facilityDamageIntervalSeconds;
            }

            if (path.Count == 0)
            {
                if (hasFinalDefenseTarget)
                {
                    hasFinalDefenseTarget = false;
                    yield return null;
                    continue;
                }

                TickDefenseStatuses(settings.repathIntervalSeconds);
                yield return new WaitForSeconds(settings.repathIntervalSeconds);
                continue;
            }

            yield return MovePathWithDefense(grid, path);
            if (intruderActor == null || intruderActor.IsDead)
            {
                ResolveIntruderDefeated();
                yield break;
            }

            if (hasBreachedDungeonInterior
                && !(defenseEngagementRuntime?.ShouldHoldIntruder(this) ?? false)
                && currentPriorityTarget != null
                && ResolveGameClock().Time >= nextDamageTime)
            {
                TryDamageNearbyFacility(grid, currentPriorityTarget);
                nextDamageTime = ResolveGameClock().Time
                    + settings.facilityDamageIntervalSeconds;
            }

            yield return null;
        }

        if (intruderActor != null && intruderActor.IsDead && State != InvasionIntruderState.Finished)
        {
            ResolveIntruderDefeated();
        }
    }

    private IEnumerator MovePathWithDefense(Grid grid, Queue<GridMoveStep> path)
    {
        if (path == null)
        {
            yield break;
        }

        while (path.Count > 0 && intruderActor != null && !intruderActor.IsDead)
        {
            GridMoveStep step = path.Dequeue();
            if (step == null) continue;

            if (!(defenseEngagementRuntime?.CanIntruderAdvanceTo(this, step.To) ?? true))
            {
                yield break;
            }

            yield return move.MoveByStep(step);
            if (move.LastGridMoveWasBlocked)
            {
                yield break;
            }

            TryMarkDungeonBreached(grid, step.To);

            List<DefenseActivationReport> reports = DefenseFacilityResolver.TriggerAt(
                grid,
                intruderActor,
                step.To,
                DefenseTriggerTiming.OnEnter,
                ResolveDefenseStatusRuntimeService());
            TickDefenseStatuses(settings.repathIntervalSeconds);

            if (intruderActor.IsDead)
            {
                yield break;
            }

            if (defenseEngagementRuntime?.ShouldHoldIntruder(this) ?? false)
            {
                yield break;
            }

            float delay = reports.Count > 0
                ? reports.Max((report) => report.MovementDelaySeconds)
                : 0f;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
        }
    }

    private void TickDefenseStatuses(float deltaSeconds)
    {
        if (intruderActor == null || intruderActor.IsDead)
        {
            return;
        }

        DefenseEffectResolver.TickStatuses(
            intruderActor,
            deltaSeconds,
            ResolveDefenseStatusRuntimeService());
    }

    private void TryMarkDungeonBreached(Grid grid, Vector2Int cellPosition)
    {
        if (hasBreachedDungeonInterior || grid == null || !grid.IsValidGridPos(cellPosition))
        {
            return;
        }

        GridCell cell = grid.GetGridCell(cellPosition);
        if (cell == null || cell.AreaType != GridCellAreaType.DungeonInterior)
        {
            return;
        }

        hasBreachedDungeonInterior = true;
        if (breachEventRaised)
        {
            return;
        }

        breachEventRaised = true;
        gameEventBus.Publish(new InvasionDungeonBreachedEvent(
            this,
            intruderActor,
            threatSnapshot));
        gameEventBus.RaiseAlert(
            "던전 내부 침입",
            "침입자가 내부에 진입했습니다. 당직 경비가 저지하러 이동합니다.",
            EventAlertImportance.High,
            "방어");
    }

    private static bool IsPostBreachState(InvasionIntruderState state)
    {
        return state != InvasionIntruderState.None
            && state != InvasionIntruderState.Rallying
            && state != InvasionIntruderState.Entering
            && state != InvasionIntruderState.Finished;
    }

    private IEnumerator FinalCombat(CharacterActor owner)
    {
        State = InvasionIntruderState.FinalCombat;
        if (settings.finalCombatWindupSeconds > 0f)
        {
            yield return new WaitForSeconds(settings.finalCombatWindupSeconds);
        }

        ApplyFinalCombat(owner);
        Finish();
    }

    private void ResolveIntruderDefeated()
    {
        if (!resolved)
        {
            resolved = true;
            gameEventBus.Publish(new InvasionResolvedEvent(true, 1f));
        }

        Finish();
    }

    private void Finish()
    {
        defenseEngagementRuntime?.NotifyIntruderFinished(this);
        State = InvasionIntruderState.Finished;
        if (intruderActor != null)
        {
            intruderActor.SetLifecycleState(CharacterLifecycleState.Despawned);
        }

        OnFinished?.Invoke(this);
        Destroy(gameObject);
    }

    private void FinishAsDownedCaptureCandidate()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        defenseEngagementRuntime?.NotifyIntruderFinished(this);
        State = InvasionIntruderState.Finished;
        intruderActor.SetLifecycleState(CharacterLifecycleState.Downed);
        OnFinished?.Invoke(this);
        Destroy(this);
    }

    private void RequireRuntimeComponents()
    {
        intruderActor = GetComponent<CharacterActor>();
        move = GetComponent<AbilityMove>();
        if (intruderActor == null)
        {
            throw new InvalidOperationException(
                $"{nameof(InvasionIntruderRuntime)} requires {nameof(CharacterActor)} prepared by {nameof(IInvasionIntruderFactory)}.");
        }

        if (move == null)
        {
            throw new InvalidOperationException(
                $"{nameof(InvasionIntruderRuntime)} requires {nameof(AbilityMove)} prepared by {nameof(IInvasionIntruderFactory)}.");
        }

        intruderActor.EnsureRuntimeState();
        intruderActor.AbilityCache?.RefreshAbilityCache();
    }

    private IInvasionIntruderContext ResolveInvasionContext()
    {
        return invasionContext
            ?? throw new InvalidOperationException($"{nameof(InvasionIntruderRuntime)} requires {nameof(IInvasionIntruderContext)} initialization.");
    }

    private IDefenseStatusRuntimeService ResolveDefenseStatusRuntimeService()
    {
        return defenseStatusRuntimeService
            ?? throw new InvalidOperationException($"{nameof(InvasionIntruderRuntime)} requires {nameof(IDefenseStatusRuntimeService)} initialization.");
    }

    private IGameClock ResolveGameClock()
    {
        return gameClock
            ?? throw new InvalidOperationException(
                $"{nameof(InvasionIntruderRuntime)} requires {nameof(IGameClock)} initialization.");
    }

    private IRandomStream ResolvePathRandomStream()
    {
        if (pathRandomStream != null)
        {
            return pathRandomStream;
        }

        IRandomStreamProvider provider = randomStreamProvider
            ?? throw new InvalidOperationException(
                $"{nameof(InvasionIntruderRuntime)} requires "
                + $"{nameof(IRandomStreamProvider)} injection.");
        string streamId = string.IsNullOrWhiteSpace(runtimeId)
            ? "invasion-intruder:pending"
            : $"invasion-intruder:{runtimeId}";
        pathRandomStream = provider.Get(streamId);
        return pathRandomStream;
    }
}
