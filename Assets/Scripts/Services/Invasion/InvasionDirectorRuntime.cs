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
    private readonly InvasionDirectorRestoreCoordinator restoreCoordinator = new();
    private IReadOnlyList<InvasionIntruderRuntime> activeIntrudersView;
    private IInvasionIntruderContext invasionContext;
    private IInvasionIntruderDataProvider intruderDataProvider;
    private IInvasionIntruderFactory intruderFactory;
    private IDefenseStatusRuntimeService defenseStatusRuntimeService;
    private IGameClock gameClock;
    private IGameEventBus gameEventBus;
    private IRandomStreamProvider randomStreamProvider;
    private IOffenseRegionRuntime offenseRegionRuntime;
    private ITreasuryDefenseRuntime treasuryDefenseRuntime;
    private IExternalInfluenceRuntime externalInfluence;
    private IInvasionCampaignRuntime campaignRuntime;
    private IEnemyArchetypeCatalog enemyArchetypes;
    private IEnemyIndividualFactory enemyIndividuals;
    private IDisposable invasionCandidateSubscription;
    private IDisposable invasionResolvedSubscription;
    private bool nextInvasionIsBoss;
    private float nextBossHealthMultiplier = 1f;
    private float nextBossDamageMultiplier = 1f;
    private bool nextInvasionIsRehearsal;
    private float nextRehearsalPowerMultiplier = 1f;
    private float nextRehearsalOwnerDamageMultiplier = 1f;
    private float nextRehearsalRetreatHealthRatio;
    private CharacterActor ralliedOwner;
    private Coroutine ownerRallyRoutine;

    public IReadOnlyList<InvasionIntruderRuntime> ActiveIntruders =>
        activeIntrudersView ??= ReadOnlyView.List(activeIntruders);
    public bool IsBossArmed => nextInvasionIsBoss;
    public bool IsRehearsalArmed => nextInvasionIsRehearsal;

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

    public bool ArmNextInvasionAsRehearsal(
        float powerMultiplier,
        float ownerDamageMultiplier,
        float retreatHealthRatio)
    {
        if (nextInvasionIsBoss || nextInvasionIsRehearsal)
        {
            return false;
        }

        nextInvasionIsRehearsal = true;
        nextRehearsalPowerMultiplier = Mathf.Clamp(powerMultiplier, 0.05f, 1f);
        nextRehearsalOwnerDamageMultiplier = Mathf.Clamp01(ownerDamageMultiplier);
        nextRehearsalRetreatHealthRatio = Mathf.Clamp01(retreatHealthRatio);
        return true;
    }

    public void CancelArmedRehearsal()
    {
        nextInvasionIsRehearsal = false;
        nextRehearsalPowerMultiplier = 1f;
        nextRehearsalOwnerDamageMultiplier = 1f;
        nextRehearsalRetreatHealthRatio = 0f;
    }

    [Inject]
    public void Construct(
        IInvasionIntruderContext invasionContext,
        IInvasionIntruderDataProvider intruderDataProvider,
        IInvasionIntruderFactory intruderFactory,
        IDefenseStatusRuntimeService defenseStatusRuntimeService,
        IGameClock gameClock,
        IRandomStreamProvider randomStreamProvider,
        IGameEventBus gameEventBus,
        IOffenseRegionRuntime offenseRegionRuntime,
        ITreasuryDefenseRuntime treasuryDefenseRuntime,
        IExternalInfluenceRuntime externalInfluence,
        IInvasionCampaignRuntime campaignRuntime)
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
        this.offenseRegionRuntime = offenseRegionRuntime
            ?? throw new ArgumentNullException(nameof(offenseRegionRuntime));
        this.treasuryDefenseRuntime = treasuryDefenseRuntime
            ?? throw new ArgumentNullException(nameof(treasuryDefenseRuntime));
        this.externalInfluence = externalInfluence;
        this.campaignRuntime = campaignRuntime;
        SubscribeToScopedEvents();
    }

    [Inject]
    public void ConfigureEnemyIndividuals(
        IEnemyArchetypeCatalog enemyArchetypes,
        IEnemyIndividualFactory enemyIndividuals)
    {
        this.enemyArchetypes = enemyArchetypes
            ?? throw new ArgumentNullException(nameof(enemyArchetypes));
        this.enemyIndividuals = enemyIndividuals
            ?? throw new ArgumentNullException(nameof(enemyIndividuals));
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

        IInvasionIntruderFactory factory = ResolveIntruderFactory();
        InvasionIntruderRuntime runtime = null;
        bool registered = false;
        DungeonExternalInfluenceSaveData influenceSnapshot = null;
        InvasionCampaignSaveData campaignSnapshot = null;
        int randomRootSeed = 0;
        IReadOnlyList<RandomStreamStateSnapshot> randomSnapshots = null;
        try
        {
        runtime = factory.Create(intruderPrefab, entry.OutsidePosition);
        runtime.Initialize(
            context,
            ResolveDefenseStatusRuntimeService(),
            gameClock,
            randomStreamProvider,
            gameEventBus,
            treasuryDefenseRuntime);
        CharacterActor preparedIntruder = runtime.IntruderActor;
        string individualRuntimeId = $"invasion:{Guid.NewGuid():N}";
        EnemyArchetypeDefinitionSO enemyArchetype = SelectEnemyArchetype(
            individualRuntimeId,
            snapshot,
            nextInvasionIsBoss);
        EnemyIndividualSaveData individualData = ResolveEnemyIndividuals().Create(
            enemyArchetype.stableId,
            CharacterId.FromStableSuffix(individualRuntimeId),
            "defense:" + snapshot.threat.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        EnemyIndividualBlueprint individual = ResolveEnemyIndividuals()
            .RequireBlueprint(individualData);
        bool isBoss = nextInvasionIsBoss;
        bool isRehearsal = nextInvasionIsRehearsal;
        randomRootSeed = randomStreamProvider.RootSeed;
        randomSnapshots = randomStreamProvider.CaptureStates();
        influenceSnapshot = externalInfluence?.Capture();
        campaignSnapshot = campaignRuntime?.Capture();
        bool dreadDefense =
            externalInfluence?.BeginInvasionDread(isBoss) == true;
        InvasionIntruderSettings effectiveSettings = context.ApplyRunVariables(intruderSettings);
        ApplyStrategicPressure(effectiveSettings);
        ScheduledInvasionOperationState operation =
            campaignRuntime?.ScheduleNextOperation(snapshot.threat);
        ApplyCampaignOperation(effectiveSettings, operation);
        if (isRehearsal)
        {
            effectiveSettings.operationKind = InvasionOperationKind.FrontalAssault;
            effectiveSettings.patternId = InvasionIntruderPatternIds.Straggler;
            effectiveSettings.healthMultiplier *= nextRehearsalPowerMultiplier;
            effectiveSettings.meleeDamageMultiplier *= nextRehearsalPowerMultiplier;
            effectiveSettings.structureDamageMultiplier *= Mathf.Lerp(
                0.6f,
                1f,
                nextRehearsalPowerMultiplier);
            effectiveSettings.finalCombatDamage *= nextRehearsalOwnerDamageMultiplier;
            effectiveSettings.retreatHealthRatio = nextRehearsalRetreatHealthRatio;
            effectiveSettings.rallyDurationSeconds = Mathf.Max(
                effectiveSettings.rallyDurationSeconds,
                MinimumRallyDurationSeconds + 6f);
        }
        effectiveSettings.rallyDurationSeconds = Mathf.Max(
            MinimumRallyDurationSeconds,
            effectiveSettings.rallyDurationSeconds);
        if (dreadDefense)
        {
            effectiveSettings.rallyDurationSeconds += isBoss ? 5f : 10f;
        }

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
        Vector2Int? finalDefenseTarget = null;

        runtime.PrepareBegin(
            data,
            snapshot,
            effectiveSettings,
            entry.OutsidePosition,
            finalDefenseTarget,
            isBoss,
            individual,
            individualRuntimeId);
        runtime.gameObject.name = individual.SaveData.displayName;
        factory.Publish(runtime);
        activeIntruders.Add(runtime);
        registered = true;
        runtime.OnFinished += OnIntruderFinished;
        runtime.StartPrepared(entry.DoorPosition, entry.GridPosition);
        InvasionIntruderPatternDefinition pattern = runtime.Pattern;
        intruder = preparedIntruder;
        nextInvasionIsBoss = false;
        nextBossHealthMultiplier = 1f;
        nextBossDamageMultiplier = 1f;
        nextInvasionIsRehearsal = false;
        nextRehearsalPowerMultiplier = 1f;
        nextRehearsalOwnerDamageMultiplier = 1f;
        nextRehearsalRetreatHealthRatio = 0f;

        gameEventBus.Publish(new InvasionStartedEvent(snapshot));
        gameEventBus.Publish(new InvasionSpawnedEvent(intruder, snapshot));
        if (isBoss)
        {
            gameEventBus.Publish(new BossInvasionStartedEvent(intruder, snapshot));
        }
        gameEventBus.RaiseAlert(
            isBoss
                ? $"최종 침공 집결 · {pattern.title}"
                : $"침입자 집결 · {pattern.title}",
            BuildRallyDescription(effectiveSettings, operation),
            EventAlertImportance.High,
            "침입");
        return true;
        }
        catch (Exception exception)
        {
            intruder = null;
            List<Exception> cleanupFailures = CleanupFailedSpawn(
                factory,
                runtime,
                registered,
                influenceSnapshot,
                campaignSnapshot,
                randomRootSeed,
                randomSnapshots);
            if (cleanupFailures.Count > 0)
            {
                cleanupFailures.Insert(0, exception);
                throw new AggregateException(
                    "Invasion spawn failed and could not be rolled back completely.",
                    cleanupFailures);
            }

            gameEventBus.RaiseInvasionResult(
                $"Invasion spawn failed: {exception.Message}",
                EventAlertImportance.High);
            return false;
        }
    }

    private List<Exception> CleanupFailedSpawn(
        IInvasionIntruderFactory factory,
        InvasionIntruderRuntime runtime,
        bool registered,
        DungeonExternalInfluenceSaveData influenceSnapshot,
        InvasionCampaignSaveData campaignSnapshot,
        int randomRootSeed,
        IReadOnlyList<RandomStreamStateSnapshot> randomSnapshots)
    {
        List<Exception> failures = new List<Exception>();
        if (registered && runtime != null)
        {
            runtime.OnFinished -= OnIntruderFinished;
            activeIntruders.Remove(runtime);
        }

        if (runtime != null)
        {
            try
            {
                runtime.gameObject.SetActive(false);
                factory.DestroyDetached(runtime);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (campaignSnapshot != null && campaignRuntime != null)
        {
            try
            {
                campaignRuntime.ReplaceFromValidatedSnapshot(campaignSnapshot);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (influenceSnapshot != null && externalInfluence != null)
        {
            try
            {
                externalInfluence.PublishRestoreCandidate(
                    externalInfluence.BuildRestoreCandidate(influenceSnapshot));
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (randomSnapshots != null && randomStreamProvider != null)
        {
            try
            {
                randomStreamProvider.RestoreStates(
                    randomRootSeed,
                    randomSnapshots);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    private void ApplyStrategicPressure(InvasionIntruderSettings settings)
    {
        if (settings == null || offenseRegionRuntime == null)
        {
            return;
        }

        OffenseStrategicPressureSnapshot pressure =
            offenseRegionRuntime.GetFactionPressure(OffenseRegionRuntime.HumanFactionId);
        float warningMultiplier = 1f
            + pressure.Logistics * 0.004f
            + pressure.Intelligence * 0.003f;
        settings.rallyDurationSeconds *= Mathf.Clamp(warningMultiplier, 1f, 1.7f);
        settings.healthMultiplier *= Mathf.Clamp(1f - pressure.Manpower * 0.002f, 0.8f, 1f);
        settings.meleeDamageMultiplier *= Mathf.Clamp(
            1f - pressure.Armament * 0.002f,
            0.8f,
            1f);
        settings.attackSpeedMultiplier *= Mathf.Clamp(
            1f - pressure.Logistics * 0.0015f,
            0.85f,
            1f);
    }

    private void ApplyCampaignOperation(
        InvasionIntruderSettings settings,
        ScheduledInvasionOperationState operation)
    {
        if (settings == null || operation == null || campaignRuntime == null)
        {
            return;
        }

        float branch = campaignRuntime.GetBranchStrengthMultiplier(
            operation.primaryBranchId);
        settings.operationKind = operation.kind;
        settings.raidId = operation.operationId?.Trim() ?? string.Empty;
        switch (operation.kind)
        {
            case InvasionOperationKind.FrontalAssault:
                settings.patternId = InvasionIntruderPatternIds.Breaker;
                settings.healthMultiplier *= branch;
                settings.riskTolerance = 0.7f;
                settings.routeCommitmentSeconds = 4f;
                settings.structureDamageMultiplier = 1.25f;
                break;
            case InvasionOperationKind.Siege:
                settings.patternId = InvasionIntruderPatternIds.Breaker;
                settings.meleeDamageMultiplier *= branch;
                settings.facilityDamageIntervalSeconds /= Mathf.Max(0.35f, branch);
                settings.riskTolerance = 0.65f;
                settings.routeCommitmentSeconds = 4f;
                settings.structureDamageMultiplier = 1.5f;
                break;
            case InvasionOperationKind.FacilitySabotage:
                settings.patternId = InvasionIntruderPatternIds.Ambusher;
                settings.repathIntervalSeconds /= Mathf.Max(0.35f, branch);
                settings.riskTolerance = 0.2f;
                settings.routeCommitmentSeconds = 1f;
                settings.structureDamageMultiplier = 1f;
                break;
            case InvasionOperationKind.Loot:
                settings.patternId = InvasionIntruderPatternIds.Plunderer;
                settings.attackSpeedMultiplier *= branch;
                settings.structureDamageMultiplier = 1f;
                break;
            case InvasionOperationKind.CaptiveRescue:
                settings.patternId = InvasionIntruderPatternIds.Hunter;
                settings.healthMultiplier *= Mathf.Lerp(1f, branch, 0.6f);
                settings.structureDamageMultiplier = 1f;
                break;
            case InvasionOperationKind.OwnerAssassination:
                settings.patternId = InvasionIntruderPatternIds.Executioner;
                settings.rallyDurationSeconds /= Mathf.Max(0.5f, branch);
                settings.meleeDamageMultiplier *= branch;
                settings.riskTolerance = 1f;
                settings.routeCommitmentSeconds = 3f;
                settings.structureDamageMultiplier = 0.9f;
                break;
        }
    }

    private static string BuildRallyDescription(
        InvasionIntruderSettings settings,
        ScheduledInvasionOperationState operation)
    {
        string operationText = operation != null
            ? $" 작전: {operation.kind} · 목표: {operation.objectiveId} · " +
              $"정보 신뢰도 {operation.intelligenceConfidence * 100f:0}%."
            : string.Empty;
        return
            $"침입자들이 외부에서 집결 중입니다. 약 " +
            $"{Mathf.CeilToInt(settings.rallyDurationSeconds)}초 뒤 진입합니다." +
            operationText;
    }

    public IReadOnlyList<InvasionIntruderPersistenceState> CapturePersistentState(Grid grid) =>
        restoreCoordinator.Capture(activeIntruders, grid);

    public int PrepareRestoreCandidates(
        IEnumerable<InvasionIntruderPersistenceState> restoredIntruders,
        DungeonGameRestoreReport report) =>
        restoreCoordinator.Prepare(
            restoredIntruders,
            report,
            ResolveIntruderData,
            source =>
            {
                EnemyIndividualBlueprint blueprint = ResolveEnemyIndividuals()
                    .RequireBlueprint(source);
                return blueprint;
            },
            source => ResolveIntruderFactory().CreateDetached(
                intruderPrefab,
                source.WorldPosition),
            runtime => runtime.Initialize(
                ResolveInvasionContext(),
                ResolveDefenseStatusRuntimeService(),
                gameClock,
                randomStreamProvider,
                gameEventBus,
                treasuryDefenseRuntime),
            runtime => ResolveIntruderFactory().DestroyDetached(runtime));

    public void PublishRestoreCandidates() => restoreCoordinator.Publish();

    public void RollbackPublishedRestoreCandidates() =>
        restoreCoordinator.Rollback(
            runtime => ResolveIntruderFactory().DestroyDetached(runtime));

    public void CompleteRestoreCandidates()
    {
        restoreCoordinator.Complete(
            ClearForPersistentRestore,
            runtime =>
            {
                ResolveIntruderFactory().PublishDetached(runtime);
                runtime.PublishPreparedRestore();
                activeIntruders.Add(runtime);
                runtime.OnFinished += OnIntruderFinished;
            });
    }

    public void DiscardRestoreCandidates() =>
        restoreCoordinator.Discard(
            runtime => ResolveIntruderFactory().DestroyDetached(runtime));

    public bool TryGetRestoreCandidate(
        string persistentId,
        out InvasionIntruderRuntime candidate) =>
        restoreCoordinator.TryGet(persistentId, out candidate);

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

    private IEnemyIndividualFactory ResolveEnemyIndividuals() =>
        enemyIndividuals
        ?? throw new InvalidOperationException(
            $"{nameof(InvasionDirectorRuntime)} requires {nameof(IEnemyIndividualFactory)} injection.");

    private EnemyArchetypeDefinitionSO SelectEnemyArchetype(
        string runtimeId,
        InvasionThreatSnapshot snapshot,
        bool boss)
    {
        IEnemyArchetypeCatalog catalog = enemyArchetypes
            ?? throw new InvalidOperationException(
                $"{nameof(InvasionDirectorRuntime)} requires {nameof(IEnemyArchetypeCatalog)} injection.");
        EnemyArchetypeDefinitionSO[] candidates = catalog.All
            .Where(value => value != null
                && value.individualGeneration != null
                && value.individualGeneration.recruitable
                && (!boss || value.role == EnemyCombatRole.Boss))
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0 && boss)
        {
            candidates = catalog.All
                .Where(value => value != null
                    && value.individualGeneration != null
                    && value.individualGeneration.recruitable)
                .OrderBy(value => value.stableId, StringComparer.Ordinal)
                .ToArray();
        }
        if (candidates.Length == 0)
        {
            throw new InvalidOperationException("No recruitable defense enemy archetype is authored.");
        }

        uint hash = PersistentEntityId.GetStableHash32(
            runtimeId + ":" + snapshot.threat.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture));
        return candidates[(int)(hash % (uint)candidates.Length)];
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

            path = grid.GetMovePathTo(owner.GetNowXY(), plan.Target);
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

        if (runtime.IntruderActor != null
            && runtime.IntruderActor.CurrentLifecycleState
                == CharacterLifecycleState.Downed
            && runtime.EnemyIndividual != null)
        {
            ResolveEnemyIndividuals().EnsureCharacterDomains(
                ResolveEnemyIndividuals().RequireBlueprint(
                    runtime.EnemyIndividual));
        }

        runtime.OnFinished -= OnIntruderFinished;
        activeIntruders.Remove(runtime);
    }
}
