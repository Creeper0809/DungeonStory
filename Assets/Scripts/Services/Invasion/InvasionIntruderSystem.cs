using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;

public class InvasionIntruderRuntime :
    MonoBehaviour,
    IInvasionIntruderExecutionHost,
    IInvasionIntruderRestorePort
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
    private IGameCalendar calendar;
    private IGameEventBus gameEventBus;
    private IRandomStreamProvider randomStreamProvider;
    private IRandomStream pathRandomStream;
    private ITreasuryDefenseRuntime treasuryDefenseRuntime;
    private IDefenseEngagementRuntime defenseEngagementRuntime;
    private ICharacterBodyHealthQuery bodyHealth;
    private IDefenseBreachPlanner breachPlanner;
    private IBuildingStructuralIntegrityRuntime structuralIntegrity;
    private IDefenseRaidAwarenessRuntime raidAwareness;
    private IDefenseFacilityNetworkRuntime facilityNetwork;
    private InvasionIntruderPatternDefinition pattern;
    private readonly InvasionIntruderContentBinding contentBinding =
        new InvasionIntruderContentBinding();
    private BuildableObject currentPriorityTarget;
    private bool hasFinalDefenseTarget;
    private Vector2Int finalDefenseTarget;
    private string runtimeId = string.Empty;
    private InvasionThreatSnapshot threatSnapshot;
    private float rallyRemainingSeconds;
    private bool hasBreachedDungeonInterior;
    private bool breachEventRaised;
    private readonly HashSet<BuildingInstanceId> damagedFacilityIds =
        new HashSet<BuildingInstanceId>();
    private int facilityDamageCount;
    private bool isBoss;
    private BuildableObject currentBreachTarget;
    private Vector2Int breachAttackCell;
    private float trappedSince;
    private float nextStructureAttackAt;
    private bool enragedBreach;
    private bool noBreachableExitAlerted;
    private int committedAwarenessVersion;
    private float routeCommitmentUntil;
    private float restoredStructureAttackDelay;
    private float restoredTrappedSeconds;
    private bool restoredEnragedBreach;
    private EnemyIndividualSaveData enemyIndividual;
    private string preparedEnemyArchetypeId = string.Empty;
    private InvasionCommittedWarningProjection committedWarningProjection;
    private bool hasCommittedWarningProjection;
    private InvasionIntruderExecutionCoordinator executionCoordinator;
    private InvasionIntruderRestoreCoordinator restoreCoordinator;
    private ICharacterPerformanceQuery performance;
    private InvasionFactPublicationRuntime publications;
    private string invasionStartPublicationKey = string.Empty;
    private string lastInvasionStagePublicationKey = string.Empty;

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
    public InvasionIntruderPatternDefinition Pattern =>
        pattern ?? contentBinding.Default;
    public BuildableObject CurrentPriorityTarget => currentPriorityTarget;
    public bool HasFinalDefenseTarget => hasFinalDefenseTarget;
    public Vector2Int FinalDefenseTarget => finalDefenseTarget;
    public int FacilityDamageCount => facilityDamageCount;
    public string RuntimeId => runtimeId;
    public EnemyIndividualSaveData EnemyIndividual => enemyIndividual?.Clone();
    public float WarningRallySecondsRemaining =>
        Mathf.Max(0f, rallyRemainingSeconds);
    public string RaidId => InvasionIntruderCombatRules.ResolveRaidId(settings, runtimeId);
    public InvasionOperationKind OperationKind =>
        settings?.operationKind ?? InvasionOperationKind.FrontalAssault;
    public float MeleeDamageMultiplier => settings != null
        ? Mathf.Max(0.01f, settings.meleeDamageMultiplier)
        : 1f;
    public float AttackSpeedMultiplier => settings != null
        ? Mathf.Max(0.01f, settings.attackSpeedMultiplier)
        : 1f;
    public BuildableObject CurrentBreachTarget => currentBreachTarget;
    public Vector2Int BreachAttackCell => breachAttackCell;
    public bool IsEnragedBreach => enragedBreach;
    public float TrappedSeconds => currentBreachTarget != null
        ? Mathf.Max(0f, ResolveGameClock().Time - trappedSince)
        : 0f;
    public int BreachAttackerCount => currentBreachTarget != null
        ? breachPlanner?.GetReservedAttackerCount(currentBreachTarget) ?? 1
        : 0;

    public bool TryGetCommittedWarningProjection(
        out InvasionCommittedWarningProjection projection)
    {
        projection = committedWarningProjection;
        return hasCommittedWarningProjection;
    }

    public event Action<InvasionIntruderRuntime> OnFinished;

    private InvasionIntruderExecutionCoordinator ExecutionCoordinator =>
        executionCoordinator ??= new InvasionIntruderExecutionCoordinator(this);
    private InvasionIntruderRestoreCoordinator RestoreCoordinator =>
        restoreCoordinator ??= new InvasionIntruderRestoreCoordinator(this);

    InvasionIntruderRuntime IInvasionIntruderExecutionHost.Runtime => this;
    CharacterActor IInvasionIntruderExecutionHost.Actor => intruderActor;
    AbilityMove IInvasionIntruderExecutionHost.Move => move;
    InvasionIntruderSettings IInvasionIntruderExecutionHost.Settings => settings;
    InvasionIntruderState IInvasionIntruderExecutionHost.State { get => State; set => State = value; }
    bool IInvasionIntruderExecutionHost.Resolved { get => resolved; set => resolved = value; }
    float IInvasionIntruderExecutionHost.RallyRemainingSeconds { get => rallyRemainingSeconds; set => rallyRemainingSeconds = value; }
    IGameClock IInvasionIntruderExecutionHost.Clock => ResolveGameClock();
    IGameCalendar IInvasionIntruderExecutionHost.Calendar => calendar;
    IGameEventBus IInvasionIntruderExecutionHost.GameEventBus => gameEventBus;
    IInvasionIntruderContext IInvasionIntruderExecutionHost.Context => ResolveInvasionContext();
    IDefenseEngagementRuntime IInvasionIntruderExecutionHost.DefenseEngagement => defenseEngagementRuntime;
    BuildableObject IInvasionIntruderExecutionHost.PriorityTarget { get => currentPriorityTarget; set => currentPriorityTarget = value; }
    bool IInvasionIntruderExecutionHost.HasFinalDefenseTarget { get => hasFinalDefenseTarget; set => hasFinalDefenseTarget = value; }
    Vector2Int IInvasionIntruderExecutionHost.FinalDefenseTarget => finalDefenseTarget;
    float IInvasionIntruderExecutionHost.Elapsed { get => elapsed; set => elapsed = value; }
    bool IInvasionIntruderExecutionHost.HasBreachedDungeonInterior { get => hasBreachedDungeonInterior; set => hasBreachedDungeonInterior = value; }
    bool IInvasionIntruderExecutionHost.BreachEventRaised { get => breachEventRaised; set => breachEventRaised = value; }
    float IInvasionIntruderExecutionHost.NextDamageTime { get => nextDamageTime; set => nextDamageTime = value; }
    IDefenseStatusRuntimeService IInvasionIntruderExecutionHost.DefenseStatusRuntimeService => ResolveDefenseStatusRuntimeService();
    ITreasuryDefenseRuntime IInvasionIntruderExecutionHost.TreasuryDefenseRuntime => treasuryDefenseRuntime;
    string IInvasionIntruderExecutionHost.RuntimeId => runtimeId;
    InvasionThreatSnapshot IInvasionIntruderExecutionHost.ThreatSnapshot => threatSnapshot;
    bool IInvasionIntruderExecutionHost.IsBoss => isBoss;
    IDefenseRaidAwarenessRuntime IInvasionIntruderExecutionHost.RaidAwareness => raidAwareness;
    int IInvasionIntruderExecutionHost.CommittedAwarenessVersion => committedAwarenessVersion;
    float IInvasionIntruderExecutionHost.RouteCommitmentUntil => routeCommitmentUntil;
    IDefenseFacilityNetworkRuntime IInvasionIntruderExecutionHost.FacilityNetwork => facilityNetwork;
    IDefenseBreachPlanner IInvasionIntruderExecutionHost.BreachPlanner => breachPlanner;
    IBuildingStructuralIntegrityRuntime IInvasionIntruderExecutionHost.StructuralIntegrity => structuralIntegrity;
    bool IInvasionIntruderExecutionHost.NoBreachableExitAlerted { get => noBreachableExitAlerted; set => noBreachableExitAlerted = value; }
    BuildableObject IInvasionIntruderExecutionHost.BreachTarget { get => currentBreachTarget; set => currentBreachTarget = value; }
    Vector2Int IInvasionIntruderExecutionHost.BreachAttackCell { get => breachAttackCell; set => breachAttackCell = value; }
    float IInvasionIntruderExecutionHost.TrappedSince { get => trappedSince; set => trappedSince = value; }
    float IInvasionIntruderExecutionHost.NextStructureAttackAt { get => nextStructureAttackAt; set => nextStructureAttackAt = value; }
    bool IInvasionIntruderExecutionHost.EnragedBreach { get => enragedBreach; set => enragedBreach = value; }
    float IInvasionIntruderExecutionHost.RestoredStructureAttackDelay { get => restoredStructureAttackDelay; set => restoredStructureAttackDelay = value; }
    float IInvasionIntruderExecutionHost.RestoredTrappedSeconds { get => restoredTrappedSeconds; set => restoredTrappedSeconds = value; }
    bool IInvasionIntruderExecutionHost.RestoredEnragedBreach { get => restoredEnragedBreach; set => restoredEnragedBreach = value; }
    float IInvasionIntruderExecutionHost.MeleeDamageMultiplier => MeleeDamageMultiplier;
    float IInvasionIntruderExecutionHost.AttackSpeedMultiplier => AttackSpeedMultiplier;
    ICharacterPerformanceQuery IInvasionIntruderExecutionHost.Performance => performance;
    Queue<GridMoveStep> IInvasionIntruderExecutionHost.CreateNextPath(Grid grid, Vector2Int ownerPosition, out bool direct, out BuildableObject priorityTarget) => CreateNextPath(grid, ownerPosition, out direct, out priorityTarget);
    bool IInvasionIntruderExecutionHost.TryDamageNearbyFacility(Grid grid, BuildableObject preferredTarget) => TryDamageNearbyFacility(grid, preferredTarget);
    void IInvasionIntruderExecutionHost.MarkDungeonBreached(Grid grid, Vector2Int cellPosition) => TryMarkDungeonBreached(grid, cellPosition);
    void IInvasionIntruderExecutionHost.ClearBreachState() => ClearBreachState();
    void IInvasionIntruderExecutionHost.Finish() => Finish();

    InvasionIntruderSettings IInvasionIntruderRestorePort.Settings
    {
        get => settings;
        set => settings = value;
    }
    InvasionThreatSnapshot IInvasionIntruderRestorePort.ThreatSnapshot
    {
        set => threatSnapshot = value;
    }
    InvasionIntruderPatternDefinition IInvasionIntruderRestorePort.Pattern
    {
        get => pattern;
        set => pattern = value;
    }
    BuildableObject IInvasionIntruderRestorePort.PriorityTarget
    {
        set => currentPriorityTarget = value;
    }
    ISet<BuildingInstanceId> IInvasionIntruderRestorePort.DamagedFacilityIds =>
        damagedFacilityIds;
    int IInvasionIntruderRestorePort.FacilityDamageCount
    {
        set => facilityDamageCount = value;
    }
    float IInvasionIntruderRestorePort.RestoredStructureAttackDelay
    {
        set => restoredStructureAttackDelay = value;
    }
    float IInvasionIntruderRestorePort.RestoredTrappedSeconds
    {
        set => restoredTrappedSeconds = value;
    }
    bool IInvasionIntruderRestorePort.RestoredEnragedBreach
    {
        set => restoredEnragedBreach = value;
    }
    bool IInvasionIntruderRestorePort.HasFinalDefenseTarget
    {
        set => hasFinalDefenseTarget = value;
    }
    Vector2Int IInvasionIntruderRestorePort.FinalDefenseTarget
    {
        set => finalDefenseTarget = value;
    }
    float IInvasionIntruderRestorePort.Elapsed { set => elapsed = value; }
    float IInvasionIntruderRestorePort.RallyRemainingSeconds
    {
        set => rallyRemainingSeconds = value;
    }
    bool IInvasionIntruderRestorePort.HasBreachedDungeonInterior
    {
        get => hasBreachedDungeonInterior;
        set => hasBreachedDungeonInterior = value;
    }
    bool IInvasionIntruderRestorePort.BreachEventRaised
    {
        set => breachEventRaised = value;
    }
    float IInvasionIntruderRestorePort.NextDamageTime
    {
        set => nextDamageTime = value;
    }
    bool IInvasionIntruderRestorePort.Resolved { set => resolved = value; }
    string IInvasionIntruderRestorePort.RuntimeId { set => runtimeId = value; }
    InvasionIntruderState IInvasionIntruderRestorePort.State
    {
        get => State;
        set => State = value;
    }
    IGameClock IInvasionIntruderRestorePort.Clock => ResolveGameClock();
    IInvasionIntruderContext IInvasionIntruderRestorePort.Context =>
        ResolveInvasionContext();
    IDefenseRaidAwarenessRuntime IInvasionIntruderRestorePort.RaidAwareness =>
        raidAwareness;
    IDefenseStatusRuntimeService
        IInvasionIntruderRestorePort.DefenseStatusRuntimeService =>
        ResolveDefenseStatusRuntimeService();
    CharacterActor IInvasionIntruderRestorePort.Actor => intruderActor;
    Transform IInvasionIntruderRestorePort.Transform => transform;
    bool IInvasionIntruderRestorePort.IsActiveAndEnabled => isActiveAndEnabled;
    void IInvasionIntruderRestorePort.StopActiveRoutine()
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }

    void IInvasionIntruderRestorePort.RestorePublicationLineage(
        string startPublicationKey, string lastPublicationKey) =>
        RestorePublicationLineage(startPublicationKey, lastPublicationKey);
    void IInvasionIntruderRestorePort.RequireRuntimeComponents() =>
        RequireRuntimeComponents();
    InvasionIntruderPatternDefinition
        IInvasionIntruderRestorePort.ResolvePattern(string id) =>
        ResolvePattern(id);
    void IInvasionIntruderRestorePort.ClearBreachState() =>
        ExecutionCoordinator.ClearLocalBreachState();
    void IInvasionIntruderRestorePort.RefreshPathRandomStream() =>
        pathRandomStream = ResolvePathRandomStream();
    void IInvasionIntruderRestorePort.StartRestoredInside()
    {
        CommitWarningProjection();
        routine = StartCoroutine(ResumeRestoredInside());
    }
    void IInvasionIntruderRestorePort.StartRestoredEntry(
        InvasionIntruderEntry entry,
        bool includeRally)
    {
        CommitWarningProjection(entry);
        routine = StartCoroutine(ResumeRestoredEntry(
            entry.DoorPosition,
            entry.GridPosition,
            includeRally));
    }

    private IEnumerator ResumeRestoredEntry(
        Vector3 entryDoorPosition,
        Vector2Int entryGridPosition,
        bool includeRally)
    {
        // StartCoroutine advances once synchronously. Deferring the restored
        // routine preserves the exact saved countdown/state until the next
        // simulation frame instead of consuming one clock tick during load.
        yield return null;
        yield return Run(
            entryDoorPosition,
            entryGridPosition,
            includeRally);
    }

    private IEnumerator ResumeRestoredInside()
    {
        // Preserve the saved inside-state at the restore boundary as well.
        // The normal decision loop resumes on the following simulation frame.
        yield return null;
        yield return RunInside();
    }

    public void SetEngagementState(bool engaged, Vector2Int? holdCell = null)
    {
        if (State == InvasionIntruderState.Finished)
        {
            return;
        }

        if (engaged)
        {
            ClearBreachState();
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

    public void ConfigureContent(
        IInvasionIntruderPatternDefinitionCatalog patternCatalog)
    {
        contentBinding.Configure(patternCatalog);
    }

    private InvasionIntruderPatternDefinition ResolvePattern(string id) =>
        contentBinding.Resolve(id);

    public void Initialize(
        IInvasionIntruderContext invasionContext,
        IDefenseStatusRuntimeService defenseStatusRuntimeService,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IGameEventBus gameEventBus,
        ITreasuryDefenseRuntime treasuryDefenseRuntime,
        ICharacterPerformanceQuery performance,
        InvasionFactPublicationRuntime publications,
        IGameCalendar calendar)
    {
        this.invasionContext = invasionContext
            ?? throw new ArgumentNullException(nameof(invasionContext));
        this.defenseStatusRuntimeService = defenseStatusRuntimeService
            ?? throw new ArgumentNullException(nameof(defenseStatusRuntimeService));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.randomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.treasuryDefenseRuntime = treasuryDefenseRuntime
            ?? throw new ArgumentNullException(nameof(treasuryDefenseRuntime));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
    }

    public void ConfigureDefenseEngagement(
        IDefenseEngagementRuntime defenseEngagementRuntime,
        ICharacterBodyHealthQuery bodyHealth)
    {
        this.defenseEngagementRuntime = defenseEngagementRuntime
            ?? throw new ArgumentNullException(nameof(defenseEngagementRuntime));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
    }

    public void ConfigureTacticalServices(
        IDefenseBreachPlanner breachPlanner,
        IBuildingStructuralIntegrityRuntime structuralIntegrity,
        IDefenseRaidAwarenessRuntime raidAwareness,
        IDefenseFacilityNetworkRuntime facilityNetwork)
    {
        this.breachPlanner = breachPlanner
            ?? throw new ArgumentNullException(nameof(breachPlanner));
        this.structuralIntegrity = structuralIntegrity
            ?? throw new ArgumentNullException(nameof(structuralIntegrity));
        this.raidAwareness = raidAwareness
            ?? throw new ArgumentNullException(nameof(raidAwareness));
        this.facilityNetwork = facilityNetwork
            ?? throw new ArgumentNullException(nameof(facilityNetwork));
    }

    public void PrepareBegin(
        CharacterSO data,
        InvasionThreatSnapshot threatSnapshot,
        InvasionIntruderSettings settings,
        Vector3 outsidePosition,
        Vector2Int? finalDefenseTarget = null,
        bool isBoss = false,
        EnemyIndividualBlueprint individualBlueprint = null,
        string preparedRuntimeId = "",
        string selectedEnemyArchetypeId = "")
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        this.settings = settings ?? new InvasionIntruderSettings();
        this.isBoss = isBoss;
        this.threatSnapshot = threatSnapshot;
        pattern = ResolvePattern(this.settings.patternId);
        this.settings.patternId = pattern.id;
        currentPriorityTarget = null;
        damagedFacilityIds.Clear();
        facilityDamageCount = 0;
        ClearBreachState();
        noBreachableExitAlerted = false;
        restoredStructureAttackDelay = 0f;
        restoredTrappedSeconds = 0f;
        restoredEnragedBreach = false;
        hasFinalDefenseTarget = finalDefenseTarget.HasValue;
        this.finalDefenseTarget = finalDefenseTarget.GetValueOrDefault();
        elapsed = 0f;
        rallyRemainingSeconds = Mathf.Max(0f, this.settings.rallyDurationSeconds);
        hasBreachedDungeonInterior = false;
        breachEventRaised = false;
        nextDamageTime = ResolveGameClock().Time
            + this.settings.facilityDamageIntervalSeconds;
        resolved = false;
        invasionStartPublicationKey = string.Empty;
        lastInvasionStagePublicationKey = string.Empty;
        runtimeId = string.IsNullOrWhiteSpace(preparedRuntimeId)
            ? $"invasion:{Guid.NewGuid():N}"
            : preparedRuntimeId.Trim();
        pathRandomStream = ResolvePathRandomStream();
        RequireRuntimeComponents();

        transform.position = outsidePosition;
        intruderActor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
        enemyIndividual = individualBlueprint?.SaveData.Clone();
        preparedEnemyArchetypeId = selectedEnemyArchetypeId?.Trim()
            ?? string.Empty;
        committedWarningProjection = default;
        hasCommittedWarningProjection = false;
        if (individualBlueprint != null)
        {
            intruderActor.Initialize(data, individualBlueprint.SpawnRequest);
        }
        else
        {
            intruderActor.Initialize(data);
        }
        intruderActor.Identity?.SetPersistentId(
            individualBlueprint?.CharacterId
            ?? CharacterId.FromStableSuffix(runtimeId));
        intruderActor.ScaleMaxHealth(this.settings.healthMultiplier);
        intruderActor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
    }

    public void StartPrepared(
        Vector3 entryDoorPosition,
        Vector2Int entryGridPosition)
    {
        StartPrepared(
            entryDoorPosition,
            entryGridPosition,
            outsidePosition: default,
            hasEntryGeometry: false);
    }

    public void StartPrepared(InvasionIntruderEntry entry)
    {
        StartPrepared(
            entry.DoorPosition,
            entry.GridPosition,
            entry.OutsidePosition,
            hasEntryGeometry: true);
    }

    private void StartPrepared(
        Vector3 entryDoorPosition,
        Vector2Int entryGridPosition,
        Vector3 outsidePosition,
        bool hasEntryGeometry)
    {
        if (!gameObject.activeInHierarchy
            || intruderActor == null
            || intruderActor.Identity?.Data == null)
        {
            throw new InvalidOperationException(
                "An invasion intruder must be initialized and published before execution starts.");
        }

        CommitWarningProjection(
            entryDoorPosition,
            entryGridPosition,
            outsidePosition,
            hasEntryGeometry);
        routine = StartCoroutine(Run(entryDoorPosition, entryGridPosition, includeRally: true));
        raidAwareness?.IdentifyOperation(
            InvasionIntruderCombatRules.ResolveRaidId(settings, runtimeId),
            Pattern.preferredFacilityFamilyIds.Count > 0 ? 2 : 1);
    }

    private void CommitWarningProjection()
    {
        CommitWarningProjection(
            doorPosition: default,
            entryGridPosition: default,
            outsidePosition: default,
            hasEntryGeometry: false);
    }

    private void CommitWarningProjection(InvasionIntruderEntry entry)
    {
        CommitWarningProjection(
            entry.DoorPosition,
            entry.GridPosition,
            entry.OutsidePosition,
            hasEntryGeometry: true);
    }

    private void CommitWarningProjection(
        Vector3 doorPosition,
        Vector2Int entryGridPosition,
        Vector3 outsidePosition,
        bool hasEntryGeometry)
    {
        committedWarningProjection = new InvasionCommittedWarningProjection(
            RaidId,
            OperationKind,
            string.IsNullOrWhiteSpace(preparedEnemyArchetypeId)
                ? enemyIndividual?.enemyArchetypeId?.Trim()
                : preparedEnemyArchetypeId,
            Pattern?.title,
            entryGridPosition,
            outsidePosition,
            doorPosition,
            hasEntryGeometry);
        hasCommittedWarningProjection = true;
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
            hasBreachedDungeonInterior,
            currentBreachTarget?.BuildingData?.id ?? -1,
            currentBreachTarget != null
                ? currentBreachTarget.centerPos
                : default,
            breachAttackCell,
            Mathf.Max(
                0f,
                nextStructureAttackAt - ResolveGameClock().Time),
            TrappedSeconds,
            enragedBreach,
            raidAwareness?.Capture(InvasionIntruderCombatRules.ResolveRaidId(settings, runtimeId)),
            damagedFacilityIds.OrderBy(
                id => id.Value,
                StringComparer.Ordinal),
            enemyIndividual,
            invasionStartPublicationKey,
            lastInvasionStagePublicationKey);
    }

    internal void InitializeInvasionPublicationLineage(string publicationKey)
    {
        if (!DomainFactValueSyntax.IsText(publicationKey, 2048, false)
            || invasionStartPublicationKey != string.Empty || lastInvasionStagePublicationKey != string.Empty)
            throw new InvalidOperationException("invasion-start-publication-invalid");
        invasionStartPublicationKey = lastInvasionStagePublicationKey = publicationKey;
    }

    internal void RecordInvasionStagePublication(string publicationKey)
    {
        if (!DomainFactValueSyntax.IsText(publicationKey, 2048, false)
            || !DomainFactValueSyntax.IsText(invasionStartPublicationKey, 2048, false))
            throw new InvalidOperationException("invasion-stage-publication-without-start");
        lastInvasionStagePublicationKey = publicationKey;
    }

    internal void RestorePublicationLineage(string startPublicationKey, string lastPublicationKey)
    {
        if (!DomainFactValueSyntax.IsText(startPublicationKey, 2048, true)
            || !DomainFactValueSyntax.IsText(lastPublicationKey, 2048, true)
            || (startPublicationKey.Length == 0) != (lastPublicationKey.Length == 0))
            throw new InvalidOperationException("invasion-publication-lineage-invalid");
        invasionStartPublicationKey = startPublicationKey;
        lastInvasionStagePublicationKey = lastPublicationKey;
    }

    internal InvasionLifecycleFactData CaptureLifecycleFact(InvasionLifecycleStage stage) => new()
    {
        runtimeId = runtimeId, intruderId = CharacterPersistentIdentity.Require(intruderActor).Value,
        intruderName = intruderActor.Identity?.DisplayName ?? intruderActor.name,
        absoluteDay = calendar.Day, stage = stage, stateBefore = State, stateAfter = State,
        damageIndexBefore = facilityDamageCount, damageIndexAfter = facilityDamageCount,
        riskBefore = Mathf.Max(0f, threatSnapshot.threat), riskAfter = Mathf.Max(0f, threatSnapshot.threat)
    };

    internal bool TryApplyLifecycle(InvasionLifecycleFactData fact, Action apply, Action rollback)
    {
        if (apply == null || rollback == null) throw new ArgumentNullException(nameof(apply));
        if (!DomainFactValueSyntax.IsText(invasionStartPublicationKey, 2048, false))
        { Debug.LogError("invasion-lifecycle-start-missing"); return false; }
        if (!publications.TryBegin(fact, out var prepared, out string failure))
        { Debug.LogError("invasion-lifecycle-reservation-failed:" + failure); return false; }
        using (prepared)
        {
            var actual = prepared.Snapshot();
            string previous = lastInvasionStagePublicationKey;
            if (actual.parentPublicationKey != previous)
            { Debug.LogError("invasion-lifecycle-owner-lineage-conflict"); return false; }
            prepared.SetRollback(() => { rollback(); lastInvasionStagePublicationKey = previous; });
            apply();
            RecordInvasionStagePublication(prepared.PublicationKey);
            var delivered = prepared.PublishApplied(actual);
            if (delivered.MayBeCommitted) return true;
            Debug.LogError("invasion-lifecycle-publication-rejected:" + delivered.Detail);
            return false;
        }
    }

    public bool TryPrepareRestore(
        CharacterSO data,
        InvasionIntruderPersistenceState source,
        EnemyIndividualBlueprint individualBlueprint,
        Vector2Int? finalDefenseTarget,
        out string warning)
    {
        enemyIndividual = individualBlueprint?.SaveData.Clone();
        preparedEnemyArchetypeId = enemyIndividual?.enemyArchetypeId?.Trim()
            ?? string.Empty;
        return RestoreCoordinator.TryPrepare(
            data,
            source,
            individualBlueprint,
            finalDefenseTarget,
            out warning);
    }

    public void PublishPreparedRestore() => RestoreCoordinator.Publish();

    public void ReleaseForPersistentRestore()
    {
        RestoreCoordinator.DiscardPrepared();
        ClearBreachState();
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
        Vector2Int start = intruderActor != null
            ? intruderActor.GetNowXY()
            : Vector2Int.zero;
        InvasionIntruderPathPlanResult result = InvasionIntruderPathPlanning.Plan(
            grid,
            start,
            ownerPosition,
            Focus,
            intruderActor?.PathSearchBroker,
            ResolvePathRandomStream(),
            Pattern,
            damagedFacilityIds,
            facilityDamageCount,
            breachPlanner,
            raidAwareness,
            InvasionIntruderCombatRules.ResolveRaidId(settings, runtimeId),
            settings,
            ResolveGameClock().Time);
        direct = result.Direct;
        priorityTarget = result.PriorityTarget;
        committedAwarenessVersion = result.AwarenessVersion;
        routeCommitmentUntil = result.CommitmentUntil;
        return result.Path;
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
                damagedFacilityIds))
        {
            return false;
        }

        BuildingInstanceId facilityId = target.RequirePersistentInstanceId();
        InvasionIntruderState previousState = State;
        int previousDamageCount = facilityDamageCount;
        bool wasDamaged = target.IsDamaged;
        bool wasRemembered = damagedFacilityIds.Contains(facilityId);
        var fact = CaptureLifecycleFact(InvasionLifecycleStage.FacilityDamaged);
        fact.stateAfter = InvasionIntruderState.DamagingFacility;
        fact.damageIndexAfter = checked(previousDamageCount + 1);
        fact.targetKind = InvasionLifecycleTargetKind.Facility;
        fact.targetId = facilityId.Value; fact.targetName = target.name;
        if (!TryApplyLifecycle(fact,
            () =>
            {
                State = InvasionIntruderState.DamagingFacility;
                target.SetDamaged(true);
                if (!target.IsDamaged) throw new InvalidOperationException("invasion-facility-damage-not-applied");
                damagedFacilityIds.Add(facilityId); facilityDamageCount = fact.damageIndexAfter;
            },
            () =>
            {
                target.SetDamaged(wasDamaged);
                if (!wasRemembered) damagedFacilityIds.Remove(facilityId);
                facilityDamageCount = previousDamageCount; State = previousState;
            })) return false;

        PublishDomainNotification(
            () => gameEventBus.Publish(
                new InvasionFacilityDamagedEvent(intruderActor, target)),
            "invasion-facility-damage-event-observer");
        if (target == currentPriorityTarget)
        {
            currentPriorityTarget = null;
        }
        return true;
    }

    public void ApplyFinalCombat(CharacterActor owner) =>
        ExecutionCoordinator.ApplyFinalCombat(owner);

    public bool CanResolveDefeat(out string failureReason)
    {
        CharacterActor actor = IntruderActor;
        if (actor == null)
        {
            failureReason = "Defeat requires the owned intruder actor.";
            return false;
        }
        if (resolved || State == InvasionIntruderState.Finished)
        {
            failureReason = "The intruder has already resolved.";
            return false;
        }
        if (bodyHealth == null)
            throw new InvalidOperationException("Intruder defeat requires body-health authority.");
        if (!actor.IsDead && !bodyHealth.GetSnapshot(actor).Downed)
        {
            failureReason = "Intruder is neither dead nor downed by body-health authority.";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public void ResolveSuppressedBy(CharacterActor defender)
    {
        if (resolved)
        {
            // A repeated terminal must not despawn the living captive actor.
            return;
        }
        if (!CanResolveDefeat(out string failureReason))
            throw new InvalidOperationException(failureReason);

        if (!TryCommitResolution(
                defended: true,
                residualRisk: 1f,
                InvasionIntruderState.Finished,
                InvasionResolutionKind.Suppressed,
                defender))
        {
            throw new InvalidOperationException(
                "The suppressed invasion result could not be committed.");
        }
        PublishDomainNotification(
            () => gameEventBus.Publish(new InvasionSuppressedEvent(intruderActor, defender)),
            "invasion-suppressed-notification");
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

        if (!TryCommitResolution(
                defended: false,
                residualRisk: 5f,
                InvasionIntruderState.Finished,
                InvasionResolutionKind.DefenseFailed,
                owner))
        {
            throw new InvalidOperationException(
                "The failed-defense invasion result could not be committed.");
        }
        PublishDomainNotification(
            () => gameEventBus.Publish(new InvasionDefenseFailedEvent(intruderActor, owner)),
            "invasion-defense-failed-notification");
        Finish();
    }

    internal bool TryCommitFrontCollapsedOutcome(string reason, CharacterActor defender = null)
    {
        if (resolved || string.IsNullOrWhiteSpace(runtimeId)) return false;
        var fact = CaptureLifecycleFact(InvasionLifecycleStage.FrontCollapsed);
        fact.stateAfter = InvasionIntruderState.FrontBroken;
        fact.reason = reason?.Trim() ?? string.Empty;
        SetDefender(fact, defender);
        var before = State;
        return TryApplyLifecycle(fact, () => State = InvasionIntruderState.FrontBroken, () => State = before);
    }

    internal bool TryCommitResolution(bool defended, float residualRisk,
        InvasionIntruderState terminalState, InvasionResolutionKind resolution, CharacterActor defender = null)
    {
        if (resolved || string.IsNullOrWhiteSpace(runtimeId) || !float.IsFinite(residualRisk) || residualRisk < 0f)
            return false;
        var fact = CaptureLifecycleFact(InvasionLifecycleStage.Resolved);
        fact.stateAfter = terminalState; fact.riskAfter = residualRisk;
        fact.defended = defended; fact.resolution = resolution;
        SetDefender(fact, defender);
        bool previousResolved = resolved; var previousState = State;
        if (!TryApplyLifecycle(fact, () => { resolved = true; State = terminalState; },
            () => { resolved = previousResolved; State = previousState; })) return false;
        PublishDomainNotification(() => gameEventBus.Publish(
            new InvasionResolvedEvent(runtimeId, defended, residualRisk)), "invasion-resolved-notification");
        return true;
    }

    private static void SetDefender(InvasionLifecycleFactData fact, CharacterActor defender)
    {
        if (defender == null) return;
        fact.targetKind = InvasionLifecycleTargetKind.Defender;
        fact.targetId = CharacterPersistentIdentity.Require(defender).Value;
        fact.targetName = defender.Identity?.DisplayName ?? defender.name;
    }

    private static void PublishDomainNotification(Action observer, string context)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(context + ":" + exception.GetType().Name);
        }
    }

    private IEnumerator Run(
        Vector3 entryDoorPosition,
        Vector2Int entryGridPosition,
        bool includeRally) =>
        ExecutionCoordinator.Run(
            entryDoorPosition,
            entryGridPosition,
            includeRally);

    private IEnumerator RunInside() => ExecutionCoordinator.RunInside();

    private void ClearBreachState()
    {
        ExecutionCoordinator.ClearBreachState();
    }


    private void TryMarkDungeonBreached(Grid grid, Vector2Int cellPosition)
    {
        ExecutionCoordinator.MarkDungeonBreached(grid, cellPosition);
    }
    private void Finish()
    {
        ClearBreachState();
        defenseEngagementRuntime?.NotifyIntruderFinished(this);
        State = InvasionIntruderState.Finished;
        if (intruderActor != null)
        {
            intruderActor.SetLifecycleState(CharacterLifecycleState.Despawned);
        }

        OnFinished?.Invoke(this);
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    private void FinishAsDownedCaptureCandidate()
    {
        ClearBreachState();
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        defenseEngagementRuntime?.NotifyIntruderFinished(this);
        State = InvasionIntruderState.Finished;
        // Project the already-committed body-health result. Never manufacture
        // a downed state to force a healthy intruder through this terminal.
        intruderActor.SetLifecycleState(CharacterLifecycleState.Downed);
        OnFinished?.Invoke(this);
        if (Application.isPlaying)
        {
            Destroy(this);
        }
        else
        {
            DestroyImmediate(this);
        }
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
