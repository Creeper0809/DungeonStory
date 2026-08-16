#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public enum CharacterAiAdditionalChaosMode
{
    RoutineLockdown = 1,
    ResponderCasualty = 2,
    ExternalIntentAlert = 3,
    FacilityAlertDestroy = 4,
    HaulAlertCasualty = 5,
    RescueAlertRescuerDowned = 6,
    HuntAlertTopologyLoss = 7
}

[InitializeOnLoad]
public static class CharacterAiAdditionalChaosPlayModeVerifier
{
    public const string RoutineLockdownReportPath =
        "Artifacts/QA/character-ai-chaos-routine-lockdown.txt";
    public const string ResponderCasualtyReportPath =
        "Artifacts/QA/character-ai-chaos-responder-casualty.txt";
    public const string ExternalIntentAlertReportPath =
        "Artifacts/QA/character-ai-chaos-external-intent-alert.txt";
    public const string FacilityAlertDestroyReportPath =
        "Artifacts/QA/character-ai-chaos-facility-alert-destroy.txt";
    public const string HaulAlertCasualtyReportPath =
        "Artifacts/QA/character-ai-chaos-haul-alert-casualty.txt";
    public const string RescueAlertRescuerDownedReportPath =
        "Artifacts/QA/character-ai-chaos-rescue-alert-rescuer-downed.txt";
    public const string HuntAlertTopologyLossReportPath =
        "Artifacts/QA/character-ai-chaos-hunt-alert-topology-loss.txt";

    private const string RequestPath =
        "Temp/character-ai-additional-chaos-playmode.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;

    static CharacterAiAdditionalChaosPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += TryStartPendingEditModeRequest;
    }

    [MenuItem("DungeonStory/Debug/QA/Chaos/Run Routine Self-Care Lockdown")]
    public static void RequestRoutineLockdown() =>
        RequestRun(CharacterAiAdditionalChaosMode.RoutineLockdown);

    [MenuItem("DungeonStory/Debug/QA/Chaos/Run Responder Casualty Handoff")]
    public static void RequestResponderCasualty() =>
        RequestRun(CharacterAiAdditionalChaosMode.ResponderCasualty);

    [MenuItem("DungeonStory/Debug/QA/Chaos/Run External Intent Alert Handoff")]
    public static void RequestExternalIntentAlert() =>
        RequestRun(CharacterAiAdditionalChaosMode.ExternalIntentAlert);

    [MenuItem("DungeonStory/Debug/QA/Chaos/Run Facility Alert Destroy Handoff")]
    public static void RequestFacilityAlertDestroy() =>
        RequestRun(CharacterAiAdditionalChaosMode.FacilityAlertDestroy);

    [MenuItem("DungeonStory/Debug/QA/Chaos/Run Haul Alert Casualty Handoff")]
    public static void RequestHaulAlertCasualty() =>
        RequestRun(CharacterAiAdditionalChaosMode.HaulAlertCasualty);

    [MenuItem("DungeonStory/Debug/QA/Chaos/Run Rescue Alert Rescuer Downed")]
    public static void RequestRescueAlertRescuerDowned() =>
        RequestRun(CharacterAiAdditionalChaosMode.RescueAlertRescuerDowned);

    [MenuItem("DungeonStory/Debug/QA/Chaos/Run Hunt Alert Topology Loss")]
    public static void RequestHuntAlertTopologyLoss() =>
        RequestRun(CharacterAiAdditionalChaosMode.HuntAlertTopologyLoss);

    public static string GetReportPath(CharacterAiAdditionalChaosMode mode) =>
        mode switch
        {
            CharacterAiAdditionalChaosMode.RoutineLockdown => RoutineLockdownReportPath,
            CharacterAiAdditionalChaosMode.ResponderCasualty => ResponderCasualtyReportPath,
            CharacterAiAdditionalChaosMode.ExternalIntentAlert => ExternalIntentAlertReportPath,
            CharacterAiAdditionalChaosMode.FacilityAlertDestroy => FacilityAlertDestroyReportPath,
            CharacterAiAdditionalChaosMode.HaulAlertCasualty => HaulAlertCasualtyReportPath,
            CharacterAiAdditionalChaosMode.RescueAlertRescuerDowned => RescueAlertRescuerDownedReportPath,
            _ => HuntAlertTopologyLossReportPath
        };

    private static void RequestRun(CharacterAiAdditionalChaosMode mode)
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(GetReportPath(mode));
        File.WriteAllText(RequestPath, ((int)mode).ToString());
        if (EditorApplication.isPlaying)
        {
            StartRunner(mode);
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => TryStartPendingRunner();

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
        }
        else if (change == PlayModeStateChange.EnteredPlayMode)
        {
            TryStartPendingRunner();
        }
    }

    private static void TryStartPendingEditModeRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || !File.Exists(RequestPath))
        {
            return;
        }

        string raw = File.ReadAllText(RequestPath).Trim();
        if (!int.TryParse(raw, out int encoded)
            || !Enum.IsDefined(typeof(CharacterAiAdditionalChaosMode), encoded))
        {
            File.Delete(RequestPath);
            Debug.LogError($"Invalid additional-chaos request flag: '{raw}'.");
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    private static void TryStartPendingRunner()
    {
        if (!File.Exists(RequestPath)) return;
        int raw = (int)CharacterAiAdditionalChaosMode.RoutineLockdown;
        int.TryParse(File.ReadAllText(RequestPath).Trim(), out raw);
        File.Delete(RequestPath);
        CharacterAiAdditionalChaosMode mode = Enum.IsDefined(
            typeof(CharacterAiAdditionalChaosMode), raw)
            ? (CharacterAiAdditionalChaosMode)raw
            : CharacterAiAdditionalChaosMode.RoutineLockdown;
        StartRunner(mode);
    }

    private static void StartRunner(CharacterAiAdditionalChaosMode mode)
    {
        CharacterAiAdditionalChaosPlayModeRunner existing =
            UnityEngine.Object.FindFirstObjectByType<
                CharacterAiAdditionalChaosPlayModeRunner>();
        if (existing != null || runnerCreated) return;
        CharacterAiAdditionalChaosPlayModeRunner runner =
            new GameObject("Character AI Additional Chaos Runner")
                .AddComponent<CharacterAiAdditionalChaosPlayModeRunner>();
        runner.Mode = mode;
        runnerCreated = runner != null;
    }
}

public sealed class CharacterAiAdditionalChaosPlayModeRunner : MonoBehaviour
{
    private const string CleanWaterItemId = "resource:clean-water";
    private const float SetupTimeout = 20f;
    private const float ActionTimeout = 30f;
    private const float RecoveryTimeout = 45f;

    private readonly List<string> rows = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly Dictionary<CharacterActor, CharacterAiRuntimeGateSnapshot>
        gateBaselines = new();
    private readonly List<BuildingSO> runtimeFacilityDefinitions = new();
    private readonly List<BuildableObject> runtimeFacilities = new();
    private readonly List<ChaosFaultWall> faultWalls = new();

    private DungeonRuntimeLifetimeScope scope;
    private IDungeonGameSaveService saves;
    private DungeonGameSaveData baseline;
    private IWorldItemStackRuntime items;
    private IWorldItemHaulPlanningService haulPlanning;
    private ICharacterDeprivationRuntime deprivation;
    private ICharacterNeedBalanceRuntime needBalance;
    private ISettlementAlertService alerts;
    private SettlementAlertRuntime alertRuntime;
    private CharacterAlarmResponseRuntime alarmRuntime;
    private IGameEventBus events;
    private IGameCalendar calendar;
    private ICharacterBodyHealthQuery bodyHealthQuery;
    private ICharacterBodyHealthCommand bodyHealthCommand;
    private ICharacterMedicalQuery medical;
    private WildlifeRuntime wildlife;
    private GridSystemManager gridSystem;
    private Grid grid;
    private IFacilityCandidateCache facilityCandidates;
    private CharacterActor[] actors = Array.Empty<CharacterActor>();
    private int originalDay;
    private int originalHour;
    private float originalTimeScale;
    private bool originalRunInBackground;
    private bool finished;

    public CharacterAiAdditionalChaosMode Mode { get; set; }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        originalTimeScale = Time.timeScale;
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        Time.timeScale = 8f;
        Application.logMessageReceived += CaptureIssue;
        yield return ExecuteGuarded(Run());
        FinishRun();
    }

    private IEnumerator Run()
    {
        rows.Add($"INFO\tSCENARIO\tmode={Mode};revision=additional-chaos-v1-20260816");
        yield return ResolveWorld();
        if (failures.Count > 0) yield break;

        if (Mode == CharacterAiAdditionalChaosMode.RoutineLockdown)
        {
            yield return RunRoutineLockdown();
        }
        else if (Mode == CharacterAiAdditionalChaosMode.ResponderCasualty)
        {
            yield return RunResponderCasualty();
        }
        else if (Mode == CharacterAiAdditionalChaosMode.ExternalIntentAlert)
        {
            yield return RunExternalIntentAlert();
        }
        else if (Mode == CharacterAiAdditionalChaosMode.FacilityAlertDestroy)
        {
            yield return RunFacilityAlertDestroy();
        }
        else if (Mode == CharacterAiAdditionalChaosMode.HaulAlertCasualty)
        {
            yield return RunHaulAlertCasualty();
        }
        else if (Mode == CharacterAiAdditionalChaosMode.RescueAlertRescuerDowned)
        {
            yield return RunRescueAlertRescuerDowned();
        }
        else
        {
            yield return RunHuntAlertTopologyLoss();
        }
    }

    private IEnumerator ResolveWorld()
    {
        bool prepared = false;
        float deadline = Time.realtimeSinceStartup + SetupTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            if (scope?.Container != null && LiveActors().Length == 0 && !prepared)
            {
                prepared = true;
                rows.Add("INFO\tSTART_PARTY\t"
                    + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            }
            if (scope?.Container != null && LiveActors().Length >= 3) break;
            yield return null;
        }
        if (prepared)
        {
            for (int frame = 0; frame < 8; frame++) yield return null;
            Time.timeScale = 8f;
        }

        Check(scope?.Container != null, "CHAOS_SCOPE_READY", scope?.name ?? "missing");
        Check(LiveActors().Length >= 3, "CHAOS_ACTOR_COUNT",
            $"activeHumanoids={LiveActors().Length}");
        if (scope?.Container == null || LiveActors().Length < 3) yield break;

        saves = Resolve<IDungeonGameSaveService>();
        items = Resolve<IWorldItemStackRuntime>();
        haulPlanning = Resolve<IWorldItemHaulPlanningService>();
        deprivation = Resolve<ICharacterDeprivationRuntime>();
        needBalance = Resolve<ICharacterNeedBalanceRuntime>();
        alerts = Resolve<ISettlementAlertService>();
        alertRuntime = Resolve<SettlementAlertRuntime>();
        alarmRuntime = Resolve<CharacterAlarmResponseRuntime>();
        events = Resolve<IGameEventBus>();
        calendar = Resolve<IGameCalendar>();
        bodyHealthQuery = Resolve<ICharacterBodyHealthQuery>();
        bodyHealthCommand = Resolve<ICharacterBodyHealthCommand>();
        medical = Resolve<ICharacterMedicalQuery>();
        wildlife = Resolve<WildlifeRuntime>();
        gridSystem = FindFirstObjectByType<GridSystemManager>();
        grid = gridSystem?.grid;
        facilityCandidates = Resolve<IFacilityCandidateCache>();
        bool ready = saves != null
            && items != null
            && haulPlanning != null
            && deprivation != null
            && needBalance != null
            && alerts != null
            && alertRuntime != null
            && alarmRuntime != null
            && events != null
            && calendar != null
            && bodyHealthQuery != null
            && bodyHealthCommand != null
            && medical != null
            && wildlife != null
            && grid != null
            && facilityCandidates != null;
        Check(ready, "CHAOS_AUTHORITIES_READY",
            $"save={saves != null};items={items != null};haulPlanning={haulPlanning != null};deprivation={deprivation != null};"
            + $"alert={alerts != null}/{alarmRuntime != null};body={bodyHealthQuery != null}/{bodyHealthCommand != null};medical={medical != null}");
        if (!ready) yield break;

        IEnvironmentalFieldQuery environment = Resolve<IEnvironmentalFieldQuery>();
        deadline = Time.realtimeSinceStartup + SetupTimeout;
        while (environment != null
               && !environment.IsInitialized
               && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        Check(environment?.IsInitialized == true,
            "CHAOS_SAVE_AUTHORITIES_SETTLED",
            $"environment={environment != null};initialized={environment?.IsInitialized == true}");
        if (environment?.IsInitialized != true) yield break;

        baseline = saves.Capture();
        originalDay = calendar.Day;
        originalHour = calendar.Hour;
        actors = LiveActors();
        foreach (CharacterActor actor in actors)
        {
            gateBaselines[actor] = actor.Brain?.CaptureRuntimeGateSnapshot() ?? default;
            actor.SetAiPaused(true);
            actor.Brain?.StopCurrentActionForReplan("additional-chaos-fixture-isolation");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "additional-chaos-fixture-isolation");
            actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            Neutralize(actor);
            deprivation.DebugResetForDeterministicScenario(actor);
        }
        yield return null;
        yield return null;
    }

    private IEnumerator RunRoutineLockdown()
    {
        WorldItemStackSnapshot[] water = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, CleanWaterItemId, StringComparison.Ordinal)
                && stack.Quantity > 0)
            .ToArray();
        CharacterActor subject = actors
            .Where(actor => IsAlarmEligible(actor)
                && actor.Brain.availableActions.Any(action => action?.actionset is AIDrink)
                && actor.Brain.availableActions.Any(action => action?.actionset is AIWork))
            .OrderByDescending(actor => water.Length == 0
                ? 0
                : water.Min(stack => Manhattan(actor.GetNowXY(), stack.Position)))
            .ThenBy(ActorId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(subject != null, "ROUTINE_LOCKDOWN_ACTOR_READY", ActorId(subject));
        int waterBefore = CountWorldItem(CleanWaterItemId);
        Check(waterBefore > 0, "ROUTINE_LOCKDOWN_PHYSICAL_WATER",
            $"water={waterBefore};stacks={water.Length}");
        if (subject == null || waterBefore <= 0) yield break;

        AIBrain brain = subject.Brain;
        AbilityWork work = subject.GetAbility<AbilityWork>();
        AIAction drinkAction = brain.availableActions.First(action => action?.actionset is AIDrink);
        AIAction workAction = brain.availableActions.First(action => action?.actionset is AIWork);
        AIAction[] originalActions = brain.availableActions;
        CharacterNeedResponseProfile response = needBalance.GetResponse(CharacterCondition.THIRST);
        float forecastLoss = subject.Stats.GetExpectedTimedNeedLoss(
            CharacterCondition.THIRST,
            90f);
        float nonEmergencyFloor = 20f + forecastLoss + 1f;
        bool validBand = response.routineStart > Mathf.Max(
            response.emergencyStart + 1f,
            nonEmergencyFloor);
        Check(validBand, "ROUTINE_LOCKDOWN_NON_EMERGENCY_BAND",
            $"emergency={response.emergencyStart:0.##};routine={response.routineStart:0.##};forecast={forecastLoss:0.##}");
        if (!validBand) yield break;

        subject.Stats.Stats[CharacterCondition.THIRST] = response.routineStart;
        string needReason = string.Empty;
        bool routineNeeded = deprivation.NeedsRoutineDrink(subject, out needReason);
        bool emergencyNeed = CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(
            subject,
            CharacterCondition.THIRST);
        Check(routineNeeded && !emergencyNeed,
            "ROUTINE_LOCKDOWN_PRECONDITION",
            $"routine={routineNeeded};emergency={emergencyNeed};reason={needReason};thirst={subject.Stats.Stats[CharacterCondition.THIRST]:0.##}");
        if (!routineNeeded || emergencyNeed) yield break;

        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkPriorities.SetPriority(BuiltInWorkTypeIds.Guard, WorkPriorityLevel.Priority1);
        brain.availableActions = new[] { drinkAction, workAction };
        CharacterAiRuntimeGateSnapshot before = brain.CaptureRuntimeGateSnapshot();
        int externalBefore = brain.ExternalIntentTransitionCount;
        subject.SetAiPaused(false);
        brain.PreferActionOnNextDecision<AIDrink>(180f);
        brain.RequestImmediateReplan(clearFailures: true);

        bool drinkLive = false;
        bool ownerOverlap = false;
        int waterAtCollision = waterBefore;
        float deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            CharacterAiRuntimeGateSnapshot gate = brain.CaptureRuntimeGateSnapshot();
            drinkLive = brain.bestAction?.actionset is AIDrink
                && gate.GetBranch(CharacterAiBranch.Drink).LiveActions > 0;
            ownerOverlap |= drinkLive && brain.IsExternallyDrivenActionActive;
            if (drinkLive)
            {
                waterAtCollision = CountWorldItem(CleanWaterItemId);
                events.Publish(new InvasionStartedEvent(default));
                break;
            }
            yield return null;
        }
        Check(drinkLive, "ROUTINE_LOCKDOWN_DRINK_LIVE_BEFORE_ALERT",
            $"action={brain.CurrentActionDebugLabel};phase={brain.CurrentActionPhase};water={waterBefore}->{waterAtCollision}");
        Check(waterAtCollision == waterBefore,
            "ROUTINE_LOCKDOWN_NO_PREMATURE_CONSUME",
            $"water={waterBefore}->{waterAtCollision}");
        if (!drinkLive || waterAtCollision != waterBefore)
        {
            brain.availableActions = originalActions;
            yield break;
        }

        SettlementAlertSnapshot red = alerts.Capture();
        long epoch = red.AlertEpochId;
        bool guardOwned = false;
        bool drinkRetired = false;
        bool guardActionObserved = false;
        deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            CharacterAiRuntimeGateSnapshot gate = brain.CaptureRuntimeGateSnapshot();
            bool currentDrinkLive = brain.bestAction?.actionset is AIDrink
                && gate.GetBranch(CharacterAiBranch.Drink).LiveActions > 0;
            ownerOverlap |= currentDrinkLive && brain.IsExternallyDrivenActionActive;
            guardOwned = work.HasEmergencyResponseWorkGateForDiagnostics
                && work.EmergencyResponseWorkEpochForDiagnostics == epoch
                && work.EmergencyResponseOnlyWorkTypeForDiagnostics == BuiltInWorkTypeIds.Guard;
            drinkRetired = !currentDrinkLive
                && brain.bestAction?.actionset is not AIDrink;
            guardActionObserved |= brain.bestAction?.actionset is AIWork
                && (work.AssignedWorkTypeId == BuiltInWorkTypeIds.Guard
                    || work.EmergencyResponseOnlyWorkTypeForDiagnostics
                        == BuiltInWorkTypeIds.Guard);
            if (guardOwned && drinkRetired && guardActionObserved) break;
            yield return null;
        }
        int waterDuringRed = CountWorldItem(CleanWaterItemId);
        Check(red.CommittedLevel == SettlementThreatAlertLevel.Red
                && red.ActiveIncidentIds.Contains("incident:invasion:active"),
            "ROUTINE_LOCKDOWN_RED_COMMITTED",
            $"level={red.CommittedLevel};epoch={epoch};active=[{string.Join(",", red.ActiveIncidentIds)}]");
        Check(guardOwned && drinkRetired && guardActionObserved,
            "ROUTINE_LOCKDOWN_ATOMIC_DRINK_HANDOFF_TO_GUARD",
            $"gate={work.HasEmergencyResponseWorkGateForDiagnostics}/{work.EmergencyResponseOnlyWorkTypeForDiagnostics};"
            + $"epoch={work.EmergencyResponseWorkEpochForDiagnostics}/{epoch};guardAction={guardActionObserved};action={brain.CurrentActionDebugLabel}");
        // AIDrink declares CanInterrupt=false and the Brain deliberately protects
        // every running action from a wake-only replan. The alert may bind its
        // gate immediately, but the already-owned one-unit transaction must
        // reach exactly one terminal before Guard starts; cancellation in the
        // middle would strand its safe-relief runner or item reservation.
        Check(waterDuringRed == waterAtCollision - 1,
            "ROUTINE_LOCKDOWN_ATOMIC_DRINK_CONSUMED_EXACTLY_ONCE",
            $"water={waterAtCollision}->{waterDuringRed}");

        events.Publish(new InvasionResolvedEvent(true, 0f));
        SettlementAlertSnapshot resolved = alerts.Capture();
        Check(resolved.DesiredLevel == SettlementThreatAlertLevel.Green
                && !resolved.ActiveIncidentIds.Contains("incident:invasion:active"),
            "ROUTINE_LOCKDOWN_INVASION_RESOLVED_SYNC",
            $"desired={resolved.DesiredLevel};committed={resolved.CommittedLevel};active=[{string.Join(",", resolved.ActiveIncidentIds)}]");
        AdvanceAlertFourHours();
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        while (Time.realtimeSinceStartup < deadline
               && (alerts.Capture().CommittedLevel != SettlementThreatAlertLevel.Green
                   || work.HasEmergencyResponseWorkGateForDiagnostics
                   || alarmRuntime.ReturningResponderCountForDiagnostics > 0))
        {
            alarmRuntime.Tick();
            yield return null;
        }
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green
                && !work.HasEmergencyResponseWorkGateForDiagnostics,
            "ROUTINE_LOCKDOWN_GREEN_GATE_RELEASED",
            $"level={alerts.Capture().CommittedLevel};gate={work.HasEmergencyResponseWorkGateForDiagnostics};"
            + $"pending={alarmRuntime.PendingResponderCountForDiagnostics};returning={alarmRuntime.ReturningResponderCountForDiagnostics}");

        int waterAfterGreen = CountWorldItem(CleanWaterItemId);
        for (int frame = 0; frame < 8; frame++)
        {
            CharacterAiRuntimeGateSnapshot gate = brain.CaptureRuntimeGateSnapshot();
            bool currentDrinkLive = brain.bestAction?.actionset is AIDrink
                && gate.GetBranch(CharacterAiBranch.Drink).LiveActions > 0;
            ownerOverlap |= currentDrinkLive && brain.IsExternallyDrivenActionActive;
            yield return null;
        }
        subject.SetAiPaused(true);
        brain.availableActions = originalActions;
        brain.StopCurrentActionForReplan("routine-lockdown-complete");
        yield return null;
        CharacterAiRuntimeGateSnapshot after = brain.CaptureRuntimeGateSnapshot();
        int waterAfter = CountWorldItem(CleanWaterItemId);
        long startDelta = after.ActionStarts - before.ActionStarts;
        long terminalDelta = after.ActionTerminals - before.ActionTerminals;
        Check(waterAfterGreen == waterAtCollision - 1
                && waterAfter == waterAfterGreen
                && !deprivation.NeedsRoutineDrink(subject, out _),
            "ROUTINE_LOCKDOWN_NO_DUPLICATE_DRINK_AFTER_GREEN",
            $"water={waterAtCollision}->{waterAfterGreen}->{waterAfter};thirst={subject.Stats.Stats[CharacterCondition.THIRST]:0.##}");
        Check(!ownerOverlap && brain.ExternalIntentTransitionCount == externalBefore,
            "ROUTINE_LOCKDOWN_NO_DUAL_OWNER",
            $"overlap={ownerOverlap};externalTransitions={externalBefore}->{brain.ExternalIntentTransitionCount}");
        Check(startDelta == terminalDelta && terminalDelta >= 2
                && after.InvariantAnomalies == before.InvariantAnomalies,
            "ROUTINE_LOCKDOWN_LIFECYCLE_CONSERVED",
            $"starts={startDelta};terminals={terminalDelta};invariants={before.InvariantAnomalies}->{after.InvariantAnomalies}");
    }

    private IEnumerator RunResponderCasualty()
    {
        CharacterActor[] eligible = actors
            .Where(IsAlarmEligible)
            .OrderBy(ActorId, StringComparer.Ordinal)
            .ToArray();
        Check(eligible.Length >= 2, "RESPONDER_CASUALTY_ELIGIBLE_POOL",
            $"eligible=[{string.Join(",", eligible.Select(ActorId))}]");
        if (eligible.Length < 2) yield break;

        foreach (CharacterActor actor in eligible)
        {
            AbilityWork work = actor.GetAbility<AbilityWork>();
            work.SetDutyState(AbilityWork.DutyState.OnDuty);
            work.WorkPriorities.SetPriority(BuiltInWorkTypeIds.Guard, WorkPriorityLevel.Priority1);
            actor.SetAiPaused(false);
            actor.Brain.RequestImmediateReplan(clearFailures: true);
        }
        events.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot red = alerts.Capture();
        long epoch = red.AlertEpochId;
        CharacterActor casualty = null;
        float deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            casualty = eligible.FirstOrDefault(actor => HasGuardGate(actor, epoch));
            if (casualty != null) break;
            yield return null;
        }
        Check(casualty != null, "RESPONDER_CASUALTY_INITIAL_GUARD_OWNED",
            $"epoch={epoch};responders={DescribeResponders(eligible)}");
        if (casualty == null) yield break;

        AbilityWork casualtyWork = casualty.GetAbility<AbilityWork>();
        AbilityMove casualtyMove = casualty.GetAbility<AbilityMove>();
        CharacterBodyHealthSnapshot originalBody = bodyHealthQuery.GetSnapshot(casualty);
        CharacterAiRuntimeGateSnapshot gateBefore = casualty.Brain.CaptureRuntimeGateSnapshot();
        List<CharacterBodyPartHealthState> injured = originalBody.Parts
            .Select(ClonePart)
            .ToList();
        foreach (CharacterBodyPartHealthState part in injured)
        {
            if (part.bodyPart is CombatBodyPart.LeftLeg or CombatBodyPart.RightLeg)
            {
                part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
            }
        }
        bodyHealthCommand.ApplySnapshot(
            casualty,
            new CharacterBodyHealthSnapshot(
                injured,
                5f,
                0f,
                1f,
                1f,
                0.08f,
                true),
            "qa-responder-casualty-chaos");

        string casualtyId = ActorId(casualty);
        int orderCount = medical.ActiveOrders.Count(order => order != null
            && order.IsActive
            && string.Equals(order.patientId, casualtyId, StringComparison.Ordinal));
        bool synchronousCleanup = casualty.CurrentLifecycleState == CharacterLifecycleState.Downed
            && !casualty.CanRunAi
            && casualty.Brain.bestAction == null
            && !casualty.Brain.IsExternallyDrivenActionActive
            && casualtyMove?.HasActiveMovementRoutineForDiagnostics != true;
        Check(bodyHealthQuery.GetSnapshot(casualty).Downed && synchronousCleanup,
            "RESPONDER_CASUALTY_SYNCHRONOUS_LIFECYCLE_RELEASE",
            $"bodyDowned={bodyHealthQuery.GetSnapshot(casualty).Downed};lifecycle={casualty.CurrentLifecycleState};"
            + $"best={casualty.Brain.CurrentActionDebugLabel};external={casualty.Brain.IsExternallyDrivenActionActive};"
            + $"move={casualtyMove?.HasActiveMovementRoutineForDiagnostics}");
        Check(orderCount == 1, "RESPONDER_CASUALTY_MEDICAL_ORDER_EXACTLY_ONE",
            $"patient={casualtyId};orders={orderCount}");

        CharacterActor replacement = null;
        deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            replacement = eligible.FirstOrDefault(actor => actor != casualty
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                && HasGuardGate(actor, epoch));
            if (!casualtyWork.HasEmergencyResponseWorkGateForDiagnostics
                && replacement != null)
            {
                break;
            }
            yield return null;
        }
        Check(!casualtyWork.HasEmergencyResponseWorkGateForDiagnostics,
            "RESPONDER_CASUALTY_OLD_GUARD_GATE_RETIRED",
            $"gate={casualtyWork.HasEmergencyResponseWorkGateForDiagnostics};revision={casualtyWork.EmergencyResponseWorkGateRevisionForDiagnostics}");
        Check(replacement != null && replacement != casualty,
            "RESPONDER_CASUALTY_REPLACEMENT_GUARD_SAME_EPOCH",
            $"casualty={casualtyId};replacement={ActorId(replacement)};epoch={epoch};responders={DescribeResponders(eligible)}");

        bool reacquired = false;
        for (int frame = 0; frame < 8; frame++)
        {
            alarmRuntime.Tick();
            reacquired |= casualtyWork.HasEmergencyResponseWorkGateForDiagnostics
                || casualty.Brain.bestAction != null
                || casualtyMove?.HasActiveMovementRoutineForDiagnostics == true;
            yield return null;
        }
        Check(!reacquired, "RESPONDER_CASUALTY_NO_DOWNED_REACQUIRE",
            $"reacquired={reacquired};gate={casualtyWork.HasEmergencyResponseWorkGateForDiagnostics};"
            + $"best={casualty.Brain.CurrentActionDebugLabel};move={casualtyMove?.HasActiveMovementRoutineForDiagnostics}");

        bodyHealthCommand.ApplySnapshot(
            casualty,
            originalBody,
            "qa-responder-casualty-recover");
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            orderCount = medical.ActiveOrders.Count(order => order != null
                && order.IsActive
                && string.Equals(order.patientId, casualtyId, StringComparison.Ordinal));
            if (!bodyHealthQuery.GetSnapshot(casualty).Downed
                && casualty.CurrentLifecycleState == CharacterLifecycleState.Active
                && orderCount == 0)
            {
                break;
            }
            yield return null;
        }
        Check(!bodyHealthQuery.GetSnapshot(casualty).Downed
                && casualty.CurrentLifecycleState == CharacterLifecycleState.Active
                && orderCount == 0,
            "RESPONDER_CASUALTY_MEDICAL_RECOVERY_CONVERGED",
            $"bodyDowned={bodyHealthQuery.GetSnapshot(casualty).Downed};lifecycle={casualty.CurrentLifecycleState};orders={orderCount}");

        // The casualty has proven the production recovery transition. Freeze
        // only that recovered actor now so unrelated routine work cannot add a
        // fresh reservation while the replacement responder completes its
        // Green handoff.
        casualty.SetAiPaused(true);
        casualty.Brain.StopCurrentActionForReplan(
            "responder-casualty-post-recovery-isolation");
        casualtyMove?.CancelActiveMovement();
        yield return null;
        yield return null;

        events.Publish(new InvasionResolvedEvent(true, 0f));
        AdvanceAlertFourHours();
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        bool allGatesReleased = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            allGatesReleased = eligible.All(actor =>
                !actor.GetAbility<AbilityWork>().HasEmergencyResponseWorkGateForDiagnostics);
            if (alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green
                && alarmRuntime.PendingResponderCountForDiagnostics == 0
                && alarmRuntime.ReturningResponderCountForDiagnostics == 0
                && alarmRuntime.AssignedResponderCountForDiagnostics == 0
                && allGatesReleased)
            {
                break;
            }
            yield return null;
        }
        CharacterAiRuntimeGateSnapshot gateAfter = casualty.Brain.CaptureRuntimeGateSnapshot();
        SettlementAlertSnapshot final = alerts.Capture();
        Check(final.CommittedLevel == SettlementThreatAlertLevel.Green
                && final.DesiredLevel == SettlementThreatAlertLevel.Green
                && alarmRuntime.PendingResponderCountForDiagnostics == 0
                && alarmRuntime.ReturningResponderCountForDiagnostics == 0
                && alarmRuntime.AssignedResponderCountForDiagnostics == 0
                && allGatesReleased,
            "RESPONDER_CASUALTY_FINAL_OWNERSHIP_CONVERGED",
            $"alert={final.DesiredLevel}/{final.CommittedLevel};pending={alarmRuntime.PendingResponderCountForDiagnostics};"
            + $"returning={alarmRuntime.ReturningResponderCountForDiagnostics};assigned={alarmRuntime.AssignedResponderCountForDiagnostics};gatesReleased={allGatesReleased}");
        Check(gateAfter.InvariantAnomalies == gateBefore.InvariantAnomalies
                && gateAfter.LivePathRequests == 0
                && gateAfter.LiveReservations == 0,
            "RESPONDER_CASUALTY_RUNTIME_GATE_CONSERVED",
            $"invariants={gateBefore.InvariantAnomalies}->{gateAfter.InvariantAnomalies};"
            + $"path={gateAfter.LivePathRequests};reservations={gateAfter.LiveReservations}");
    }

    private IEnumerator RunExternalIntentAlert()
    {
        const string expectedOwner = "survival:breakdown";
        CharacterActor subject = actors
            .Where(IsAlarmEligible)
            .OrderBy(ActorId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(subject != null, "EXTERNAL_ALERT_ACTOR_READY", ActorId(subject));
        if (subject == null) yield break;

        AbilityWork work = subject.GetAbility<AbilityWork>();
        AbilityMove move = subject.GetAbility<AbilityMove>();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Guard,
            WorkPriorityLevel.Priority1);
        CharacterAiRuntimeGateSnapshot before =
            subject.Brain.CaptureRuntimeGateSnapshot();
        int externalTerminalBefore = subject.Brain.ExternalIntentTerminalCount;
        bool forced = deprivation.DebugForceBreakdown(
            subject,
            CharacterBreakdownKind.ViolentImpulse);
        subject.SetAiPaused(false);
        subject.Brain.RequestImmediateReplan(clearFailures: true);
        Check(forced, "EXTERNAL_ALERT_BREAKDOWN_AUTHORED",
            $"kind={CharacterBreakdownKind.ViolentImpulse};actor={ActorId(subject)}");
        if (!forced) yield break;

        bool externalLive = false;
        long externalEpoch = 0;
        float deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            externalLive = subject.Brain.IsExternallyDrivenActionActive
                && string.Equals(
                    subject.Brain.ExternalIntentOwnerId,
                    expectedOwner,
                    StringComparison.Ordinal);
            if (externalLive)
            {
                externalEpoch = subject.Brain.ExternalIntentEpoch;
                break;
            }
            yield return null;
        }
        Check(externalLive, "EXTERNAL_ALERT_LEASE_LIVE_BEFORE_RED",
            $"owner={subject.Brain.ExternalIntentOwnerId};epoch={subject.Brain.ExternalIntentEpoch};"
            + $"moveOwner={move?.ActiveMovementOperationOwnerForDiagnostics};"
            + subject.Brain.CaptureRuntimeDiagnostics().FormatRecentTrace());
        if (!externalLive) yield break;

        events.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot red = alerts.Capture();
        long alertEpoch = red.AlertEpochId;
        bool gateObservedWhileExternal = false;
        bool executionOverlap = false;
        bool externalTerminal = false;
        bool guardActionObserved = false;
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            bool externalCurrent = subject.Brain.IsExternallyDrivenActionActive
                && string.Equals(
                    subject.Brain.ExternalIntentOwnerId,
                    expectedOwner,
                    StringComparison.Ordinal);
            bool gateCurrent = work.HasEmergencyResponseWorkGateForDiagnostics
                && work.EmergencyResponseWorkEpochForDiagnostics == alertEpoch
                && work.EmergencyResponseOnlyWorkTypeForDiagnostics
                    == BuiltInWorkTypeIds.Guard;
            bool guardActionCurrent = subject.Brain.bestAction?.actionset is AIWork
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Guard
                && subject.Brain.HasRunningWorkAction;
            gateObservedWhileExternal |= externalCurrent && gateCurrent;
            executionOverlap |= externalCurrent && guardActionCurrent;
            externalTerminal = !externalCurrent
                && subject.Brain.ExternalIntentTerminalCount
                    == externalTerminalBefore + 1;
            guardActionObserved |= guardActionCurrent;
            if (externalTerminal && guardActionObserved) break;
            yield return null;
        }

        Check(red.CommittedLevel == SettlementThreatAlertLevel.Red
                && red.ActiveIncidentIds.Contains("incident:invasion:active"),
            "EXTERNAL_ALERT_RED_COMMITTED",
            $"epoch={alertEpoch};active=[{string.Join(",", red.ActiveIncidentIds)}]");
        Check(gateObservedWhileExternal,
            "EXTERNAL_ALERT_POLICY_GATE_BOUND_DURING_LEASE",
            $"gate={work.HasEmergencyResponseWorkGateForDiagnostics};"
            + $"gateEpoch={work.EmergencyResponseWorkEpochForDiagnostics};"
            + $"externalEpoch={externalEpoch}");
        Check(!executionOverlap,
            "EXTERNAL_ALERT_NO_GUARD_EXECUTION_OVERLAP",
            $"overlap={executionOverlap};owner={subject.Brain.ExternalIntentOwnerId};"
            + $"action={subject.Brain.CurrentActionDebugLabel};work={work.AssignedWorkTypeId}");
        Check(externalTerminal,
            "EXTERNAL_ALERT_EXTERNAL_TERMINAL_EXACTLY_ONCE",
            $"terminals={externalTerminalBefore}->{subject.Brain.ExternalIntentTerminalCount};"
            + $"kind={subject.Brain.LastExternalIntentTerminalKind};external={subject.Brain.IsExternallyDrivenActionActive}");
        Check(guardActionObserved,
            "EXTERNAL_ALERT_GUARD_STARTED_AFTER_EXTERNAL_TERMINAL",
            $"guard={guardActionObserved};action={subject.Brain.CurrentActionDebugLabel};"
            + $"assigned={work.AssignedWorkTypeId};running={subject.Brain.HasRunningWorkAction}");

        events.Publish(new InvasionResolvedEvent(true, 0f));
        AdvanceAlertFourHours();
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        while (Time.realtimeSinceStartup < deadline
               && (alerts.Capture().CommittedLevel != SettlementThreatAlertLevel.Green
                   || work.HasEmergencyResponseWorkGateForDiagnostics
                   || alarmRuntime.ReturningResponderCountForDiagnostics > 0))
        {
            alarmRuntime.Tick();
            yield return null;
        }

        subject.SetAiPaused(true);
        subject.Brain.StopCurrentActionForReplan("external-alert-complete");
        move?.CancelActiveMovement();
        yield return null;
        yield return null;
        CharacterAiRuntimeGateSnapshot after =
            subject.Brain.CaptureRuntimeGateSnapshot();
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green
                && !work.HasEmergencyResponseWorkGateForDiagnostics
                && !subject.Brain.IsExternallyDrivenActionActive,
            "EXTERNAL_ALERT_FINAL_OWNERSHIP_CONVERGED",
            $"alert={alerts.Capture().DesiredLevel}/{alerts.Capture().CommittedLevel};"
            + $"gate={work.HasEmergencyResponseWorkGateForDiagnostics};"
            + $"external={subject.Brain.IsExternallyDrivenActionActive}");
        Check(after.InvariantAnomalies == before.InvariantAnomalies
                && after.LivePathRequests == 0
                && after.LiveReservations == 0,
            "EXTERNAL_ALERT_RUNTIME_GATE_CONSERVED",
            $"invariants={before.InvariantAnomalies}->{after.InvariantAnomalies};"
            + $"paths={after.LivePathRequests};reservations={after.LiveReservations}");
    }

    private IEnumerator RunFacilityAlertDestroy()
    {
        CharacterActor subject = actors
            .Where(actor => IsAlarmEligible(actor)
                && actor.Brain.availableActions?.Any(
                    action => action?.actionset is AIRest) == true)
            .OrderBy(ActorId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(subject != null, "FACILITY_ALERT_ACTOR_READY", ActorId(subject));
        if (subject == null) yield break;

        if (!TryFindRestFacilityPosition(subject, out Vector2Int facilityPosition,
                out string positionFailure))
        {
            Check(false, "FACILITY_ALERT_LAWFUL_FIXTURE", positionFailure);
            yield break;
        }

        BuildableObject facility = CreateRestFacility(
            facilityPosition,
            "alert-destroy",
            useDuration: 3f);
        Check(facility != null, "FACILITY_ALERT_FACILITY_CREATED",
            $"position={facilityPosition};failure={positionFailure}");
        if (facility == null) yield break;

        float deadline = Time.realtimeSinceStartup + SetupTimeout;
        bool published = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            published = facilityCandidates
                .GetCandidates(grid, FacilityRole.Rest)
                .Any(candidate => ReferenceEquals(candidate, facility));
            if (published) break;
            facilityCandidates.AdvanceIndex(1.0);
            yield return null;
        }
        Check(published, "FACILITY_ALERT_FACILITY_PUBLISHED",
            $"facility={facility.name};position={facility.centerPos}");
        if (!published) yield break;

        AIBrain brain = subject.Brain;
        AbilityMove move = subject.GetAbility<AbilityMove>();
        AbilityWork work = subject.GetAbility<AbilityWork>();
        AIAction[] originalActions = brain.availableActions;
        AIAction restAction = originalActions.First(
            action => action?.actionset is AIRest);
        AIAction workAction = originalActions.First(
            action => action?.actionset is AIWork);
        work.ClearPriorityWorkTarget();
        work.SetDutyState(AbilityWork.DutyState.OffDuty);
        brain.availableActions = new[] { restAction };
        RestoreNeutralPersistentState(subject);
        bool deprivationReset = deprivation.DebugResetForDeterministicScenario(subject);
        subject.Stats.Stats[CharacterCondition.SLEEP] = 0f;
        GridPathSearchResult restSearch = grid.SearchPath(subject.GetNowXY());
        bool exactDestinationResolved = restAction.actionset
            .TryResolveDestinationWithFailure(
                subject,
                restSearch,
                out BuildableObject resolvedDestination,
                out AIActionFailure resolveFailure)
            && ReferenceEquals(resolvedDestination, facility);
        Check(deprivationReset, "FACILITY_ALERT_DEPRIVATION_RESET",
            $"reset={deprivationReset}");
        Check(exactDestinationResolved,
            "FACILITY_ALERT_EXACT_DESTINATION_PREFLIGHT",
            $"expected={facility.name}@{facility.centerPos};"
            + $"actual={resolvedDestination?.name}@{resolvedDestination?.centerPos};"
            + $"failure={resolveFailure};visitable={restSearch?.ContainsVisitableOccupant(facility)}");
        if (!deprivationReset || !exactDestinationResolved)
        {
            brain.availableActions = originalActions;
            yield break;
        }
        CharacterAiRuntimeGateSnapshot before = brain.CaptureRuntimeGateSnapshot();
        long failuresBefore = brain.RuntimeExecutionFailureCount;
        long terminalsBefore = before.ActionTerminals;
        int completedUsesBefore = facility.FacilityState.completedUses;
        subject.SetAiPaused(false);
        brain.PreferActionOnNextDecision<AIRest>(180f);
        brain.RequestImmediateReplan(clearFailures: true);

        bool visitLive = false;
        deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            AIAction action = brain.bestAction;
            visitLive = action?.actionset is AIRest
                && action.HasStarted
                && ReferenceEquals(action.destination, facility)
                && facility.ActiveVisitReservationCount == 1
                && facility.CurrentUserCount == 0
                && subject.GetNowXY() != facility.centerPos;
            if (visitLive) break;
            yield return null;
        }
        Check(visitLive, "FACILITY_ALERT_VISIT_LIVE_BEFORE_RED",
            $"action={brain.CurrentActionDebugLabel};phase={brain.CurrentActionPhase};"
            + $"actor={subject.GetNowXY()};facility={facility.centerPos};"
            + $"reservations={facility.ActiveVisitReservationCount};users={facility.CurrentUserCount}");
        if (!visitLive)
        {
            brain.availableActions = originalActions;
            yield break;
        }

        // This is the collision boundary: the alert policy is committed while
        // AIRest still owns the visit, and the exact destination is destroyed
        // before either the scheduler or movement loop receives another frame.
        brain.availableActions = new[] { restAction, workAction };
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Guard,
            WorkPriorityLevel.Priority1);
        bool visitStillLiveAtCollision = brain.bestAction?.actionset is AIRest
            && ReferenceEquals(brain.bestAction.destination, facility)
            && brain.bestAction.HasStarted
            && facility.ActiveVisitReservationCount == 1;
        Check(visitStillLiveAtCollision,
            "FACILITY_ALERT_VISIT_OWNER_STABLE_AT_COLLISION",
            $"action={brain.CurrentActionDebugLabel};phase={brain.CurrentActionPhase};"
            + $"reservations={facility.ActiveVisitReservationCount};duty={work.CurrentDutyState}");
        if (!visitStillLiveAtCollision)
        {
            brain.availableActions = originalActions;
            yield break;
        }
        events.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot red = alerts.Capture();
        long alertEpoch = red.AlertEpochId;
        bool gateBoundBeforeDestroy = HasGuardGate(subject, alertEpoch);
        facility.DestroySelf();

        Check(red.CommittedLevel == SettlementThreatAlertLevel.Red
                && red.ActiveIncidentIds.Contains("incident:invasion:active")
                && gateBoundBeforeDestroy,
            "FACILITY_ALERT_RED_COMMITTED_DURING_VISIT",
            $"alert={red.CommittedLevel};epoch={alertEpoch};gate={gateBoundBeforeDestroy};"
            + $"action={brain.CurrentActionDebugLabel};facilityDestroyed={facility.IsBuildingDestroyed}");
        Check(facility.CurrentUserCount == 0
                && facility.ActiveVisitReservationCount == 0
                && facility.WaitingVisitReservationCount == 0
                && facility.WorkerReservation == null,
            "FACILITY_ALERT_SYNCHRONOUS_OCCUPANCY_RELEASE",
            $"users={facility.CurrentUserCount};reservations={facility.ActiveVisitReservationCount};"
            + $"waiting={facility.WaitingVisitReservationCount};worker={facility.WorkerReservation != null}");

        bool overlap = false;
        AIActionFailure capturedFailure = AIActionFailure.None;
        long terminalsAtFailure = -1;
        deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            bool restCurrent = brain.bestAction?.actionset is AIRest
                && ReferenceEquals(brain.bestAction.destination, facility)
                && brain.bestAction.HasStarted;
            bool guardCurrentBeforeTerminal = brain.bestAction?.actionset is AIWork
                && brain.HasRunningWorkAction
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Guard;
            overlap |= restCurrent && guardCurrentBeforeTerminal;
            if (brain.RuntimeExecutionFailureCount > failuresBefore)
            {
                capturedFailure = brain.LastActionFailure;
                terminalsAtFailure = brain.CaptureRuntimeGateSnapshot()
                    .ActionTerminals;
                break;
            }
            yield return null;
        }
        bool destroyedTerminal = brain.RuntimeExecutionFailureCount
                == failuresBefore + 1
            && terminalsAtFailure == terminalsBefore + 1
            && capturedFailure.Kind == AIActionFailureKind.Destroyed;

        Check(destroyedTerminal,
            "FACILITY_ALERT_DESTROYED_TERMINAL_EXACTLY_ONCE",
            $"failures={failuresBefore}->{brain.RuntimeExecutionFailureCount};"
            + $"terminals={terminalsBefore}->{terminalsAtFailure};"
            + $"failure={capturedFailure}");
        Check(!overlap, "FACILITY_ALERT_NO_GUARD_EXECUTION_OVERLAP",
            $"overlap={overlap};action={brain.CurrentActionDebugLabel};"
            + $"assigned={work.AssignedWorkTypeId}");
        Check(facility.FacilityState.completedUses == completedUsesBefore,
            "FACILITY_ALERT_NO_LATE_INTERACTION",
            $"completedUses={completedUsesBefore}->{facility.FacilityState.completedUses};"
            + $"users={facility.CurrentUserCount};reservations={facility.ActiveVisitReservationCount}");
        if (!destroyedTerminal)
        {
            brain.availableActions = originalActions;
            yield break;
        }

        // The collision is proven. Remove only incidental fixture needs before
        // observing the emergency handoff, without yielding while paused so a
        // current-epoch responder gate cannot be retired between owners.
        subject.SetAiPaused(true);
        brain.StopCurrentActionForReplan("facility-alert-post-destroy-neutralize");
        move?.CancelActiveMovement();
        RestoreNeutralPersistentState(subject);
        bool postCollisionReset = deprivation.DebugResetForDeterministicScenario(subject);
        brain.availableActions = new[] { restAction, workAction };
        subject.SetAiPaused(false);
        brain.PreferWorkActionOnNextDecision(BuiltInWorkTypeIds.Guard, 180f);
        brain.RequestImmediateReplan(clearFailures: true);

        bool guardStartedAfterTerminal = false;
        deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            bool restCurrent = brain.bestAction?.actionset is AIRest
                && ReferenceEquals(brain.bestAction.destination, facility)
                && brain.bestAction.HasStarted;
            bool guardCurrent = brain.bestAction?.actionset is AIWork
                && brain.HasRunningWorkAction
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Guard;
            overlap |= restCurrent && guardCurrent;
            guardStartedAfterTerminal |= guardCurrent;
            if (guardStartedAfterTerminal) break;
            yield return null;
        }
        Check(postCollisionReset,
            "FACILITY_ALERT_POST_COLLISION_NEEDS_NEUTRAL",
            $"reset={postCollisionReset};gate={HasGuardGate(subject, alertEpoch)}");
        Check(guardStartedAfterTerminal,
            "FACILITY_ALERT_GUARD_AFTER_VISIT_TERMINAL",
            $"guard={guardStartedAfterTerminal};terminal={destroyedTerminal};"
            + $"action={brain.CurrentActionDebugLabel};assigned={work.AssignedWorkTypeId}");
        Check(!overlap, "FACILITY_ALERT_NO_OWNER_OVERLAP_THROUGH_HANDOFF",
            $"overlap={overlap};action={brain.CurrentActionDebugLabel};"
            + $"assigned={work.AssignedWorkTypeId}");

        events.Publish(new InvasionResolvedEvent(true, 0f));
        AdvanceAlertFourHours();
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        while (Time.realtimeSinceStartup < deadline
               && (alerts.Capture().CommittedLevel != SettlementThreatAlertLevel.Green
                   || work.HasEmergencyResponseWorkGateForDiagnostics
                   || alarmRuntime.ReturningResponderCountForDiagnostics > 0))
        {
            alarmRuntime.Tick();
            yield return null;
        }

        subject.SetAiPaused(true);
        brain.StopCurrentActionForReplan("facility-alert-destroy-complete");
        move?.CancelActiveMovement();
        deprivation.DebugResetForDeterministicScenario(subject);
        brain.availableActions = originalActions;
        yield return null;
        yield return null;
        CharacterAiRuntimeGateSnapshot after = brain.CaptureRuntimeGateSnapshot();
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green
                && alerts.Capture().DesiredLevel == SettlementThreatAlertLevel.Green
                && !work.HasEmergencyResponseWorkGateForDiagnostics
                && !brain.IsExternallyDrivenActionActive,
            "FACILITY_ALERT_FINAL_OWNERSHIP_CONVERGED",
            $"alert={alerts.Capture().DesiredLevel}/{alerts.Capture().CommittedLevel};"
            + $"gate={work.HasEmergencyResponseWorkGateForDiagnostics};external={brain.IsExternallyDrivenActionActive}");
        Check(after.InvariantAnomalies == before.InvariantAnomalies
                && after.LivePathRequests == 0
                && after.LiveReservations == 0
                && move?.HasActiveMovementRoutineForDiagnostics != true,
            "FACILITY_ALERT_RUNTIME_GATE_CONSERVED",
            $"invariants={before.InvariantAnomalies}->{after.InvariantAnomalies};"
            + $"paths={after.LivePathRequests};reservations={after.LiveReservations};"
            + $"move={move?.HasActiveMovementRoutineForDiagnostics}");
    }

    private IEnumerator RunHaulAlertCasualty()
    {
        CharacterActor subject = actors
            .Where(actor => IsAlarmEligible(actor)
                && actor.Brain.availableActions?.Any(
                    action => action?.actionset is AIHaul) == true)
            .OrderBy(ActorId, StringComparer.Ordinal)
            .FirstOrDefault();
        CharacterActor[] eligible = actors
            .Where(IsAlarmEligible)
            .OrderBy(ActorId, StringComparer.Ordinal)
            .ToArray();
        Check(subject != null && eligible.Length >= 2,
            "HAUL_ALERT_ACTOR_AND_REPLACEMENT_READY",
            $"subject={ActorId(subject)};eligible=[{string.Join(",", eligible.Select(ActorId))}]");
        if (subject == null || eligible.Length < 2) yield break;

        AIBrain brain = subject.Brain;
        AbilityWork work = subject.GetAbility<AbilityWork>();
        AbilityMove move = subject.GetAbility<AbilityMove>();
        AbilityHaul haul = AbilityHaul.Ensure(subject);
        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(subject);
        AIAction[] originalActions = brain.availableActions;
        AIAction haulAction = originalActions.First(
            action => action?.actionset is AIHaul);
        AIAction workAction = originalActions.First(
            action => action?.actionset is AIWork);
        string itemId = FindStackableChaosItemId();
        Vector2Int seedPosition = FindHaulSeedPosition(subject);
        HashSet<string> beforeIds = items.GetAllStacks()
            .Where(stack => stack != null)
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        bool spawned = !string.IsNullOrWhiteSpace(itemId)
            && items.SpawnItemAt(
                itemId,
                2,
                seedPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawnedQuantity)
            && spawnedQuantity == 2;
        WorldItemStackSnapshot source = items.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && !beforeIds.Contains(stack.StackId)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && stack.Position == seedPosition);
        bool prioritized = source != null && items.PrioritizeHaul(source.StackId);
        WorldItemHaulPlan preview = null;
        string previewFailure = "source-not-prioritized";
        bool previewed = prioritized
            && haulPlanning.TryPreviewBestPlan(
                subject,
                out preview,
                out previewFailure)
            && preview != null
            && preview.PickupLegs.Any(leg => string.Equals(
                leg.Reservation.StackId,
                source.StackId,
                StringComparison.Ordinal));
        Check(spawned && source != null && prioritized,
            "HAUL_ALERT_PHYSICAL_SOURCE_AUTHORED",
            $"item={itemId};spawned={spawned};source={source?.StackId};position={seedPosition};prioritized={prioritized}");
        Check(previewed,
            "HAUL_ALERT_PRODUCTION_PLAN_PREFLIGHT",
            $"source={source?.StackId};preview={preview?.Summary};"
            + $"destination={preview?.PrimaryDestination}/{preview?.PrimaryDestinationId};"
            + $"pickup={preview?.PickupLegs.FirstOrDefault().PickupStandPosition};"
            + $"delivery={preview?.DeliveryLegs.FirstOrDefault().DeliveryPosition};"
            + $"failure={previewFailure}");
        if (!spawned || source == null || !prioritized || !previewed)
        {
            brain.availableActions = originalActions;
            yield break;
        }

        int physicalTotalBefore = CountWorldItem(itemId);
        work.ClearPriorityWorkTarget();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Haul,
            WorkPriorityLevel.Priority1);
        brain.availableActions = new[] { haulAction };
        RestoreNeutralPersistentState(subject);
        bool needsReset = deprivation.DebugResetForDeterministicScenario(subject);
        yield return null;
        yield return null;
        CharacterAiRuntimeGateSnapshot before = brain.CaptureRuntimeGateSnapshot();
        bool canStartBeforeWake = haul.CanStartHauling(out string canStartFailure);
        subject.SetAiPaused(false);
        bool preferredAccepted = brain.PreferActionOnNextDecision<AIHaul>(180f);
        brain.RequestImmediateReplan(clearFailures: true);
        Check(canStartBeforeWake && preferredAccepted,
            "HAUL_ALERT_BRAIN_ADMISSION_READY",
            $"canStart={canStartBeforeWake};failure={canStartFailure};"
            + $"preferred={preferredAccepted};priority={work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul)};"
            + $"actions=[{string.Join(",", brain.availableActions.Select(action => action?.actionset?.GetType().Name ?? "<null>"))}]");

        string[] committedOperations = Array.Empty<string>();
        HaulDeliveryIntentSaveData[] committedIntents =
            Array.Empty<HaulDeliveryIntentSaveData>();
        List<string> actionTrace = new();
        string lastActionLabel = null;
        float deadline = Time.realtimeSinceStartup + ActionTimeout;
        bool pickupCommitted = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            string actionLabel = brain.CurrentActionDebugLabel ?? "<none>";
            if (!string.Equals(actionLabel, lastActionLabel, StringComparison.Ordinal))
            {
                lastActionLabel = actionLabel;
                actionTrace.Add($"f={Time.frameCount}:action={actionLabel}:"
                    + $"phase={brain.CurrentActionPhase}:haul={haul.IsHauling}/{haul.CurrentExecutionStage}:"
                    + $"failure={brain.LastActionFailure}");
            }
            committedOperations = carry.Items
                .Where(item => item != null
                    && item.quantity > 0
                    && string.Equals(item.itemId, itemId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(item.ownerOperationId))
                .Select(item => item.ownerOperationId.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            List<HaulDeliveryIntentSaveData> intents = new();
            foreach (string operationId in committedOperations)
            {
                if (items.TryCaptureHaulDeliveryIntent(operationId, out
                        HaulDeliveryIntentSaveData intent)
                    && intent?.HasCommittedPickup == true)
                {
                    intents.Add(intent);
                }
            }
            committedIntents = intents.ToArray();
            pickupCommitted = brain.bestAction?.actionset is AIHaul
                && brain.bestAction.HasStarted
                && haul.IsHauling
                && carry.HasItems
                && committedOperations.Length > 0
                && committedIntents.Length == committedOperations.Length;
            if (pickupCommitted) break;
            yield return null;
        }
        bool exactDestination = committedIntents.Length > 0
            && committedIntents.All(intent => intent != null
                && string.Equals(
                    intent.destinationId,
                    preview.PrimaryDestinationId,
                    StringComparison.Ordinal)
                && intent.destinationKind == preview.PrimaryDestination
                && string.Equals(
                    intent.ownerCharacterId,
                    ActorId(subject),
                    StringComparison.Ordinal));
        Check(needsReset && pickupCommitted,
            "HAUL_ALERT_PICKUP_COMMITTED_THROUGH_BRAIN",
            $"reset={needsReset};action={brain.CurrentActionDebugLabel};stage={haul.CurrentExecutionStage};"
            + $"carry={carry.Items.Count};operations=[{string.Join(",", committedOperations)}];"
            + $"haulFailure={haul.LastFailureReason};actionFailure={brain.LastActionFailure};"
            + $"executionFailures={brain.RuntimeExecutionFailureCount};"
            + $"preferred={brain.RuntimePreferredActionDisposition}/{brain.FirstPreferredActionHardFailure};"
            + $"trace=[{string.Join(" || ", actionTrace)}]");
        Check(exactDestination,
            "HAUL_ALERT_COMMITTED_DESTINATION_EXACT",
            $"preview={preview.PrimaryDestination}/{preview.PrimaryDestinationId};"
            + $"intents=[{string.Join(" || ", committedIntents.Select(intent => $"{intent?.destinationKind}/{intent?.destinationId}/{intent?.ownerCharacterId}"))}]");
        if (!pickupCommitted || !exactDestination)
        {
            brain.availableActions = originalActions;
            yield break;
        }

        // Collision boundary: the committed physical haul remains the current
        // owner while Red policy is bound synchronously. The lawful body-health
        // Downed transition follows before either scheduler or haul coroutine
        // receives another frame.
        brain.availableActions = new[] { haulAction, workAction };
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Guard,
            WorkPriorityLevel.Priority1);
        events.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot red = alerts.Capture();
        long epoch = red.AlertEpochId;
        bool ownerStableAtCollision = HasGuardGate(subject, epoch)
            && brain.bestAction?.actionset is AIHaul
            && haul.IsHauling
            && carry.HasItems;
        CharacterAiRuntimeGateSnapshot beforeDown =
            brain.CaptureRuntimeGateSnapshot();
        CharacterBodyHealthSnapshot originalBody =
            bodyHealthQuery.GetSnapshot(subject);
        List<CharacterBodyPartHealthState> injured = originalBody.Parts
            .Select(ClonePart)
            .ToList();
        foreach (CharacterBodyPartHealthState part in injured)
        {
            if (part.bodyPart is CombatBodyPart.LeftLeg or CombatBodyPart.RightLeg)
                part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
        }
        bodyHealthCommand.ApplySnapshot(
            subject,
            new CharacterBodyHealthSnapshot(
                injured,
                5f,
                0f,
                1f,
                1f,
                0.08f,
                true),
            "qa-haul-alert-casualty");

        CharacterAiRuntimeGateSnapshot afterDown =
            brain.CaptureRuntimeGateSnapshot();
        bool intentsReleased = committedOperations.All(operationId =>
            !items.TryCaptureHaulDeliveryIntent(operationId, out _));
        bool synchronousRelease = bodyHealthQuery.GetSnapshot(subject).Downed
            && subject.CurrentLifecycleState == CharacterLifecycleState.Downed
            && !subject.CanRunAi
            && brain.bestAction == null
            && !haul.IsHauling
            && !carry.HasItems
            && move?.HasActiveMovementRoutineForDiagnostics != true
            && intentsReleased;
        Check(red.CommittedLevel == SettlementThreatAlertLevel.Red
                && ownerStableAtCollision,
            "HAUL_ALERT_RED_BOUND_WHILE_PICKUP_COMMITTED",
            $"alert={red.CommittedLevel};epoch={epoch};gate={HasGuardGate(subject, epoch)};"
            + $"action={brain.CurrentActionDebugLabel};stage={haul.CurrentExecutionStage}");
        Check(synchronousRelease
                && afterDown.ActionTerminals == beforeDown.ActionTerminals + 1,
            "HAUL_ALERT_DOWNED_SYNCHRONOUS_EXACT_TERMINAL",
            $"downed={bodyHealthQuery.GetSnapshot(subject).Downed};lifecycle={subject.CurrentLifecycleState};"
            + $"terminals={beforeDown.ActionTerminals}->{afterDown.ActionTerminals};"
            + $"hauling={haul.IsHauling};carry={carry.Items.Count};move={move?.HasActiveMovementRoutineForDiagnostics};"
            + $"intentsReleased={intentsReleased}");
        Check(CountWorldItem(itemId) == physicalTotalBefore,
            "HAUL_ALERT_PHYSICAL_QUANTITY_CONSERVED_ON_CANCEL",
            $"item={itemId};quantity={physicalTotalBefore}->{CountWorldItem(itemId)};"
            + $"stacks={DescribeItemStacks(itemId)}");

        string subjectId = ActorId(subject);
        int orderCount = medical.ActiveOrders.Count(order => order != null
            && order.IsActive
            && string.Equals(order.patientId, subjectId, StringComparison.Ordinal));
        Check(orderCount == 1,
            "HAUL_ALERT_MEDICAL_ORDER_EXACTLY_ONE",
            $"patient={subjectId};orders={orderCount}");

        foreach (CharacterActor candidate in eligible.Where(candidate => candidate != subject))
        {
            AbilityWork candidateWork = candidate.GetAbility<AbilityWork>();
            candidateWork.SetDutyState(AbilityWork.DutyState.OnDuty);
            candidateWork.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Guard,
                WorkPriorityLevel.Priority1);
            candidate.SetAiPaused(false);
            candidate.Brain.RequestImmediateReplan(clearFailures: true);
        }
        CharacterActor replacement = null;
        bool overlap = false;
        deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            overlap |= haul.IsHauling && eligible.Any(candidate =>
                candidate.Brain?.bestAction?.actionset is AIWork
                && candidate.Brain.HasRunningWorkAction
                && candidate.GetAbility<AbilityWork>().AssignedWorkTypeId
                    == BuiltInWorkTypeIds.Guard);
            replacement = eligible.FirstOrDefault(candidate => candidate != subject
                && candidate.CurrentLifecycleState == CharacterLifecycleState.Active
                && HasGuardGate(candidate, epoch));
            if (!work.HasEmergencyResponseWorkGateForDiagnostics
                && replacement != null)
            {
                break;
            }
            yield return null;
        }
        Check(!work.HasEmergencyResponseWorkGateForDiagnostics,
            "HAUL_ALERT_DOWNED_GATE_RETIRED",
            $"subject={subjectId};gate={work.HasEmergencyResponseWorkGateForDiagnostics}");
        Check(replacement != null,
            "HAUL_ALERT_REPLACEMENT_GUARD_SAME_EPOCH",
            $"subject={subjectId};replacement={ActorId(replacement)};epoch={epoch};"
            + $"responders={DescribeResponders(eligible)}");
        Check(!overlap,
            "HAUL_ALERT_NO_HAUL_GUARD_OWNER_OVERLAP",
            $"overlap={overlap};hauling={haul.IsHauling};replacement={ActorId(replacement)}");

        bodyHealthCommand.ApplySnapshot(
            subject,
            originalBody,
            "qa-haul-alert-casualty-recover");
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            orderCount = medical.ActiveOrders.Count(order => order != null
                && order.IsActive
                && string.Equals(order.patientId, subjectId, StringComparison.Ordinal));
            if (!bodyHealthQuery.GetSnapshot(subject).Downed
                && subject.CurrentLifecycleState == CharacterLifecycleState.Active
                && orderCount == 0)
            {
                break;
            }
            yield return null;
        }
        subject.SetAiPaused(true);
        brain.StopCurrentActionForReplan("haul-alert-post-recovery-isolation");
        move?.CancelActiveMovement();
        Check(!bodyHealthQuery.GetSnapshot(subject).Downed
                && subject.CurrentLifecycleState == CharacterLifecycleState.Active
                && orderCount == 0,
            "HAUL_ALERT_MEDICAL_RECOVERY_CONVERGED",
            $"downed={bodyHealthQuery.GetSnapshot(subject).Downed};"
            + $"lifecycle={subject.CurrentLifecycleState};orders={orderCount}");

        events.Publish(new InvasionResolvedEvent(true, 0f));
        AdvanceAlertFourHours();
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        bool allGatesReleased = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            allGatesReleased = eligible.All(candidate =>
                !candidate.GetAbility<AbilityWork>()
                    .HasEmergencyResponseWorkGateForDiagnostics);
            if (alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green
                && alarmRuntime.PendingResponderCountForDiagnostics == 0
                && alarmRuntime.ReturningResponderCountForDiagnostics == 0
                && alarmRuntime.AssignedResponderCountForDiagnostics == 0
                && allGatesReleased)
            {
                break;
            }
            yield return null;
        }
        foreach (CharacterActor candidate in eligible)
        {
            candidate.SetAiPaused(true);
            candidate.Brain?.StopCurrentActionForReplan(
                "haul-alert-casualty-complete");
            candidate.GetAbility<AbilityMove>()?.CancelActiveMovement();
        }
        brain.availableActions = originalActions;
        yield return null;
        yield return null;
        CharacterAiRuntimeGateSnapshot after = brain.CaptureRuntimeGateSnapshot();
        Check(alerts.Capture().DesiredLevel == SettlementThreatAlertLevel.Green
                && alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green
                && allGatesReleased
                && !haul.IsHauling
                && !carry.HasItems
                && committedOperations.All(operationId =>
                    !items.TryCaptureHaulDeliveryIntent(operationId, out _)),
            "HAUL_ALERT_FINAL_OWNERSHIP_CONVERGED",
            $"alert={alerts.Capture().DesiredLevel}/{alerts.Capture().CommittedLevel};"
            + $"gates={allGatesReleased};hauling={haul.IsHauling};carry={carry.Items.Count}");
        Check(after.InvariantAnomalies == before.InvariantAnomalies
                && after.LivePathRequests == 0
                && after.LiveReservations == 0
                && CountWorldItem(itemId) == physicalTotalBefore,
            "HAUL_ALERT_RUNTIME_AND_PHYSICAL_CONSERVED",
            $"invariants={before.InvariantAnomalies}->{after.InvariantAnomalies};"
            + $"paths={after.LivePathRequests};reservations={after.LiveReservations};"
            + $"quantity={physicalTotalBefore}->{CountWorldItem(itemId)}");
    }

    private IEnumerator RunRescueAlertRescuerDowned()
    {
        CharacterActor[] eligible = actors
            .Where(actor => IsAlarmEligible(actor)
                && actor.Brain.availableActions?.Any(
                    action => action?.actionset is AIRescue) == true)
            .OrderBy(ActorId, StringComparer.Ordinal)
            .ToArray();
        CharacterActor rescuer = eligible.FirstOrDefault();
        CharacterActor patient = actors
            .Where(actor => actor != rescuer
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
            .OrderByDescending(actor => eligible.Contains(actor))
            .ThenBy(ActorId, StringComparer.Ordinal)
            .FirstOrDefault();
        CharacterActor replacementCandidate = eligible
            .FirstOrDefault(actor => actor != rescuer && actor != patient)
            ?? eligible.FirstOrDefault(actor => actor != rescuer);
        Check(rescuer != null && patient != null && replacementCandidate != null,
            "RESCUE_ALERT_PARTICIPANTS_READY",
            $"rescuer={ActorId(rescuer)};patient={ActorId(patient)};"
            + $"replacement={ActorId(replacementCandidate)};eligible=[{string.Join(",", eligible.Select(ActorId))}]");
        if (rescuer == null || patient == null || replacementCandidate == null)
            yield break;

        bool positioned = TryPositionNearMedicalFacility(
            rescuer,
            patient,
            out BuildableObject medicalFacility,
            out string positionDetail);
        Check(positioned,
            "RESCUE_ALERT_LAWFUL_MEDICAL_FIXTURE",
            $"facility={medicalFacility?.name}@{medicalFacility?.centerPos};{positionDetail}");
        if (!positioned) yield break;

        AIBrain brain = rescuer.Brain;
        AbilityWork work = rescuer.GetAbility<AbilityWork>();
        AbilityMove move = rescuer.GetAbility<AbilityMove>();
        AbilityRescue rescue = AbilityRescue.Ensure(rescuer);
        AIAction[] originalActions = brain.availableActions;
        AIAction rescueAction = originalActions.First(
            action => action?.actionset is AIRescue);
        AIAction workAction = originalActions.First(
            action => action?.actionset is AIWork);
        CharacterBodyHealthSnapshot originalPatientBody =
            bodyHealthQuery.GetSnapshot(patient);
        CharacterBodyHealthSnapshot originalRescuerBody =
            bodyHealthQuery.GetSnapshot(rescuer);

        RestoreNeutralPersistentState(rescuer);
        deprivation.DebugResetForDeterministicScenario(rescuer);
        work.ClearPriorityWorkTarget();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Rescue,
            WorkPriorityLevel.Priority1);
        brain.availableActions = new[] { rescueAction };

        List<CharacterBodyPartHealthState> patientInjured =
            originalPatientBody.Parts.Select(ClonePart).ToList();
        foreach (CharacterBodyPartHealthState part in patientInjured)
        {
            if (part.bodyPart is CombatBodyPart.LeftLeg or CombatBodyPart.RightLeg)
                part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
            else if (part.bodyPart == CombatBodyPart.LeftArm)
            {
                part.currentHealth = Mathf.Max(1f, part.maxHealth * 0.55f);
                part.bleedingPerSecond = 0.01f;
            }
        }
        bodyHealthCommand.ApplySnapshot(
            patient,
            new CharacterBodyHealthSnapshot(
                patientInjured,
                5f,
                0f,
                1f,
                1f,
                0.08f,
                true),
            "qa-rescue-alert-patient");
        yield return null;
        yield return null;

        string patientId = ActorId(patient);
        CharacterMedicalOrder order = medical.ActiveOrders.FirstOrDefault(candidate =>
            candidate != null
            && candidate.IsActive
            && string.Equals(candidate.patientId, patientId, StringComparison.Ordinal));
        bool canStart = rescue.CanStartRescue(out DomainFailure rescueFailure);
        Check(bodyHealthQuery.GetSnapshot(patient).Downed
                && patient.CurrentLifecycleState == CharacterLifecycleState.Downed
                && order != null
                && canStart,
            "RESCUE_ALERT_ORDER_AND_BRAIN_ADMISSION_READY",
            $"patient={patient.CurrentLifecycleState}/{bodyHealthQuery.GetSnapshot(patient).Downed};"
            + $"order={order?.orderId}/{order?.state};canStart={canStart};failure={rescueFailure}");
        if (order == null || !canStart)
        {
            brain.availableActions = originalActions;
            yield break;
        }

        CharacterAiRuntimeGateSnapshot before = brain.CaptureRuntimeGateSnapshot();
        rescuer.SetAiPaused(false);
        bool preferred = brain.PreferActionOnNextDecision<AIRescue>(180f);
        brain.RequestImmediateReplan(clearFailures: true);
        bool carryLive = false;
        float deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (medical.TryGetOrder(order.orderId, out CharacterMedicalOrder current))
                order = current;
            carryLive = preferred
                && brain.bestAction?.actionset is AIRescue
                && brain.bestAction.HasStarted
                && rescue.IsRescuing
                && order.carried
                && order.state == CharacterMedicalOrderState.Carrying
                && string.Equals(order.rescuerId, ActorId(rescuer), StringComparison.Ordinal)
                && patient.transform.IsChildOf(rescuer.transform);
            if (carryLive) break;
            yield return null;
        }
        Check(carryLive,
            "RESCUE_ALERT_PHYSICAL_CARRY_LIVE_THROUGH_BRAIN",
            $"preferred={preferred};action={brain.CurrentActionDebugLabel};"
            + $"stage={rescue.RescueStageForDiagnostics};order={order.state}/{order.statusCode};"
            + $"rescuer={order.rescuerId};carried={order.carried};parented={patient.transform.IsChildOf(rescuer.transform)}");
        if (!carryLive)
        {
            brain.availableActions = originalActions;
            yield break;
        }

        brain.availableActions = new[] { rescueAction, workAction };
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Guard,
            WorkPriorityLevel.Priority1);
        events.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot red = alerts.Capture();
        long epoch = red.AlertEpochId;
        bool ownerStableAtCollision = HasGuardGate(rescuer, epoch)
            && rescue.IsRescuing
            && order.carried
            && patient.transform.IsChildOf(rescuer.transform);
        CharacterAiRuntimeGateSnapshot beforeDown =
            brain.CaptureRuntimeGateSnapshot();
        List<CharacterBodyPartHealthState> rescuerInjured =
            originalRescuerBody.Parts.Select(ClonePart).ToList();
        foreach (CharacterBodyPartHealthState part in rescuerInjured)
        {
            if (part.bodyPart is CombatBodyPart.LeftLeg or CombatBodyPart.RightLeg)
                part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
        }
        bodyHealthCommand.ApplySnapshot(
            rescuer,
            new CharacterBodyHealthSnapshot(
                rescuerInjured,
                5f,
                0f,
                1f,
                1f,
                0.08f,
                true),
            "qa-rescue-alert-rescuer");

        if (medical.TryGetOrder(order.orderId, out CharacterMedicalOrder released))
            order = released;
        CharacterAiRuntimeGateSnapshot afterDown =
            brain.CaptureRuntimeGateSnapshot();
        bool synchronousRelease = bodyHealthQuery.GetSnapshot(rescuer).Downed
            && rescuer.CurrentLifecycleState == CharacterLifecycleState.Downed
            && !rescuer.CanRunAi
            && brain.bestAction == null
            && !rescue.IsRescuing
            && move?.HasActiveMovementRoutineForDiagnostics != true
            && !order.carried
            && string.IsNullOrWhiteSpace(order.rescuerId)
            && !patient.transform.IsChildOf(rescuer.transform)
            && patient.CurrentLifecycleState == CharacterLifecycleState.Downed;
        Check(red.CommittedLevel == SettlementThreatAlertLevel.Red
                && ownerStableAtCollision,
            "RESCUE_ALERT_RED_BOUND_WHILE_CARRY_LIVE",
            $"alert={red.CommittedLevel};epoch={epoch};gate={HasGuardGate(rescuer, epoch)};"
            + $"carry={ownerStableAtCollision}");
        Check(synchronousRelease
                && afterDown.ActionTerminals == beforeDown.ActionTerminals + 1,
            "RESCUE_ALERT_RESCUER_DOWNED_EXACT_TERMINAL",
            $"downed={bodyHealthQuery.GetSnapshot(rescuer).Downed};"
            + $"lifecycle={rescuer.CurrentLifecycleState};terminals={beforeDown.ActionTerminals}->{afterDown.ActionTerminals};"
            + $"rescuing={rescue.IsRescuing};move={move?.HasActiveMovementRoutineForDiagnostics};"
            + $"order={order.state}/{order.statusCode};rescuerId={order.rescuerId};"
            + $"carried={order.carried};parented={patient.transform.IsChildOf(rescuer.transform)}");

        int patientOrders = medical.ActiveOrders.Count(candidate => candidate != null
            && candidate.IsActive
            && string.Equals(candidate.patientId, patientId, StringComparison.Ordinal));
        int rescuerOrders = medical.ActiveOrders.Count(candidate => candidate != null
            && candidate.IsActive
            && string.Equals(candidate.patientId, ActorId(rescuer), StringComparison.Ordinal));
        Check(patientOrders == 1 && rescuerOrders == 1,
            "RESCUE_ALERT_MEDICAL_ORDERS_CONSERVED",
            $"patientOrders={patientOrders};rescuerOrders={rescuerOrders};"
            + $"patientState={patient.CurrentLifecycleState};rescuerState={rescuer.CurrentLifecycleState}");

        foreach (CharacterActor candidate in eligible.Where(candidate => candidate != rescuer
                     && candidate.CurrentLifecycleState == CharacterLifecycleState.Active))
        {
            AbilityWork candidateWork = candidate.GetAbility<AbilityWork>();
            candidateWork.SetDutyState(AbilityWork.DutyState.OnDuty);
            candidateWork.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Guard,
                WorkPriorityLevel.Priority1);
            candidate.SetAiPaused(false);
            candidate.Brain.RequestImmediateReplan(clearFailures: true);
        }
        CharacterActor replacement = null;
        bool overlap = false;
        deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            overlap |= rescue.IsRescuing && eligible.Any(candidate =>
                candidate != rescuer
                && candidate.Brain?.bestAction?.actionset is AIWork
                && candidate.Brain.HasRunningWorkAction
                && candidate.GetAbility<AbilityWork>().AssignedWorkTypeId
                    == BuiltInWorkTypeIds.Guard);
            replacement = eligible.FirstOrDefault(candidate => candidate != rescuer
                && candidate.CurrentLifecycleState == CharacterLifecycleState.Active
                && HasGuardGate(candidate, epoch));
            if (!work.HasEmergencyResponseWorkGateForDiagnostics
                && replacement != null)
                break;
            yield return null;
        }
        Check(!work.HasEmergencyResponseWorkGateForDiagnostics,
            "RESCUE_ALERT_DOWNED_GATE_RETIRED",
            $"gate={work.HasEmergencyResponseWorkGateForDiagnostics};rescuer={ActorId(rescuer)}");
        Check(replacement != null,
            "RESCUE_ALERT_REPLACEMENT_GUARD_SAME_EPOCH",
            $"replacement={ActorId(replacement)};epoch={epoch};responders={DescribeResponders(eligible)}");
        Check(!overlap,
            "RESCUE_ALERT_NO_RESCUE_GUARD_OWNER_OVERLAP",
            $"overlap={overlap};rescuing={rescue.IsRescuing};replacement={ActorId(replacement)}");

        bodyHealthCommand.ApplySnapshot(
            rescuer,
            originalRescuerBody,
            "qa-rescue-alert-rescuer-recover");
        bodyHealthCommand.ApplySnapshot(
            patient,
            originalPatientBody,
            "qa-rescue-alert-patient-recover");
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            patientOrders = medical.ActiveOrders.Count(candidate => candidate != null
                && candidate.IsActive
                && (string.Equals(candidate.patientId, patientId, StringComparison.Ordinal)
                    || string.Equals(candidate.patientId, ActorId(rescuer), StringComparison.Ordinal)));
            if (!bodyHealthQuery.GetSnapshot(rescuer).Downed
                && !bodyHealthQuery.GetSnapshot(patient).Downed
                && rescuer.CurrentLifecycleState == CharacterLifecycleState.Active
                && patient.CurrentLifecycleState == CharacterLifecycleState.Active
                && patientOrders == 0)
                break;
            yield return null;
        }
        rescuer.SetAiPaused(true);
        brain.StopCurrentActionForReplan("rescue-alert-post-recovery-isolation");
        move?.CancelActiveMovement();
        Check(!bodyHealthQuery.GetSnapshot(rescuer).Downed
                && !bodyHealthQuery.GetSnapshot(patient).Downed
                && rescuer.CurrentLifecycleState == CharacterLifecycleState.Active
                && patient.CurrentLifecycleState == CharacterLifecycleState.Active
                && patientOrders == 0,
            "RESCUE_ALERT_BOTH_MEDICAL_RECOVERIES_CONVERGED",
            $"rescuer={rescuer.CurrentLifecycleState}/{bodyHealthQuery.GetSnapshot(rescuer).Downed};"
            + $"patient={patient.CurrentLifecycleState}/{bodyHealthQuery.GetSnapshot(patient).Downed};orders={patientOrders}");

        events.Publish(new InvasionResolvedEvent(true, 0f));
        AdvanceAlertFourHours();
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        bool allGatesReleased = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            allGatesReleased = eligible.All(candidate =>
                !candidate.GetAbility<AbilityWork>()
                    .HasEmergencyResponseWorkGateForDiagnostics);
            if (alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green
                && alarmRuntime.PendingResponderCountForDiagnostics == 0
                && alarmRuntime.ReturningResponderCountForDiagnostics == 0
                && alarmRuntime.AssignedResponderCountForDiagnostics == 0
                && allGatesReleased)
                break;
            yield return null;
        }
        foreach (CharacterActor candidate in eligible)
        {
            candidate.SetAiPaused(true);
            candidate.Brain?.StopCurrentActionForReplan(
                "rescue-alert-rescuer-downed-complete");
            candidate.GetAbility<AbilityMove>()?.CancelActiveMovement();
        }
        brain.availableActions = originalActions;
        yield return null;
        yield return null;
        CharacterAiRuntimeGateSnapshot after = brain.CaptureRuntimeGateSnapshot();
        Check(alerts.Capture().DesiredLevel == SettlementThreatAlertLevel.Green
                && alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green
                && allGatesReleased
                && !rescue.IsRescuing
                && !patient.transform.IsChildOf(rescuer.transform),
            "RESCUE_ALERT_FINAL_OWNERSHIP_CONVERGED",
            $"alert={alerts.Capture().DesiredLevel}/{alerts.Capture().CommittedLevel};"
            + $"gates={allGatesReleased};rescuing={rescue.IsRescuing};"
            + $"parented={patient.transform.IsChildOf(rescuer.transform)}");
        Check(after.InvariantAnomalies == before.InvariantAnomalies
                && after.LivePathRequests == 0
                && after.LiveReservations == 0,
            "RESCUE_ALERT_RUNTIME_GATE_CONSERVED",
            $"invariants={before.InvariantAnomalies}->{after.InvariantAnomalies};"
            + $"paths={after.LivePathRequests};reservations={after.LiveReservations}");
    }

    private bool TryPositionNearMedicalFacility(
        CharacterActor rescuer,
        CharacterActor patient,
        out BuildableObject facility,
        out string detail)
    {
        facility = UnityEngine.Object.FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(building => building != null
                && !building.IsBuildingDestroyed
                && building.BuildingData?.GetAbility<BuildingMedicalAbility>() != null)
            .OrderBy(building => Manhattan(rescuer.GetNowXY(), building.centerPos))
            .FirstOrDefault();
        detail = "medical facility missing";
        if (facility == null || grid == null) return false;

        BuildableObject selectedFacility = facility;
        HashSet<Vector2Int> buildingCells = selectedFacility.buildPoses.ToHashSet();
        Vector2Int[] stands = grid.GetCells()
            .Where(cell => cell != null
                && grid.IsWalkable(cell.Position)
                && !buildingCells.Contains(cell.Position))
            .Select(cell => cell.Position)
            .Distinct()
            .OrderBy(position => Manhattan(position, selectedFacility.centerPos))
            .ThenBy(position => position.x)
            .ThenBy(position => position.y)
            .Take(2)
            .ToArray();
        if (stands.Length < 2)
        {
            detail = $"facility={selectedFacility.name};reachableStands={stands.Length}";
            return false;
        }
        rescuer.transform.position = grid.GetWorldPos(stands[0]);
        patient.transform.position = grid.GetWorldPos(stands[1]);
        detail = $"rescuer={stands[0]};patient={stands[1]};facility={selectedFacility.centerPos}";
        return true;
    }

    private IEnumerator RunHuntAlertTopologyLoss()
    {
        wildlife.Tick();
        CharacterActor hunter = actors
            .Where(actor => IsAlarmEligible(actor)
                && actor.Brain.availableActions?.Any(
                    action => action?.actionset is AIHunt) == true)
            .OrderBy(ActorId, StringComparer.Ordinal)
            .FirstOrDefault();
        WildlifeActor target = null;
        Vector2Int huntStart = default;
        int huntPathCount = 0;
        if (hunter != null)
        {
            foreach (WildlifeActor candidate in wildlife.Wildlife
                         .Where(value => value != null && value.IsAlive)
                         .OrderByDescending(value => Manhattan(
                             hunter.GetNowXY(),
                             value.GridPosition))
                         .ThenBy(value => value.WildlifeId, StringComparer.Ordinal))
            {
                if (!TryPositionHunterForPursuit(
                        hunter,
                        candidate,
                        out huntStart,
                        out huntPathCount))
                {
                    continue;
                }
                target = candidate;
                break;
            }
        }
        Check(hunter != null && target != null,
            "HUNT_ALERT_PARTICIPANTS_READY",
            $"hunter={ActorId(hunter)};target={target?.WildlifeId}@{target?.GridPosition}");
        if (hunter == null || target == null) yield break;

        Check(huntPathCount > 0,
            "HUNT_ALERT_LAWFUL_PURSUIT_ROUTE",
            $"start={huntStart};target={target.GridPosition};path={huntPathCount}");
        if (huntPathCount <= 0) yield break;
        Time.timeScale = 1f;

        AIBrain brain = hunter.Brain;
        AbilityWork work = hunter.GetAbility<AbilityWork>();
        AbilityMove move = hunter.GetAbility<AbilityMove>();
        AbilityHunt hunt = AbilityHunt.Ensure(hunter, wildlife);
        AIAction[] originalActions = brain.availableActions;
        AIAction huntAction = originalActions.First(
            action => action?.actionset is AIHunt);
        AIAction workAction = originalActions.First(
            action => action?.actionset is AIWork);
        RestoreNeutralPersistentState(hunter);
        deprivation.DebugResetForDeterministicScenario(hunter);
        work.ClearPriorityWorkTarget();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Hunt,
            WorkPriorityLevel.Priority1);
        bool designated = wildlife.DesignateHunt(
            target.WildlifeId,
            true,
            priority: true);
        brain.availableActions = new[] { huntAction };
        bool canStart = hunt.CanStartHunting(out string huntFailure);
        CharacterAiRuntimeGateSnapshot before = brain.CaptureRuntimeGateSnapshot();
        int healthBefore = target.CurrentHealth;
        hunter.SetAiPaused(false);
        bool preferred = brain.PreferActionOnNextDecision<AIHunt>(180f);
        brain.RequestImmediateReplan(clearFailures: true);

        bool pursuitLive = false;
        float deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            pursuitLive = preferred
                && brain.bestAction?.actionset is AIHunt
                && brain.bestAction.HasStarted
                && hunt.IsHunting
                && !string.IsNullOrWhiteSpace(
                    move.ActiveMovementOperationOwnerForDiagnostics)
                && string.Equals(
                    target.ReservedByPersistentId,
                    ActorId(hunter),
                    StringComparison.Ordinal)
                && target.CurrentHealth == healthBefore;
            if (pursuitLive) break;
            yield return null;
        }
        Check(designated && canStart && pursuitLive,
            "HUNT_ALERT_PURSUIT_LIVE_THROUGH_BRAIN",
            $"designated={designated};canStart={canStart};failure={huntFailure};"
            + $"action={brain.CurrentActionDebugLabel};phase={brain.CurrentActionPhase};"
            + $"hunting={hunt.IsHunting};moveOwner={move.ActiveMovementOperationOwnerForDiagnostics};"
            + $"reservation={target.ReservedByPersistentId};health={healthBefore}->{target.CurrentHealth}");
        if (!pursuitLive)
        {
            brain.availableActions = originalActions;
            yield break;
        }

        brain.availableActions = new[] { huntAction, workAction };
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Guard,
            WorkPriorityLevel.Priority1);
        events.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot red = alerts.Capture();
        long epoch = red.AlertEpochId;
        bool ownerStableAtCollision = HasGuardGate(hunter, epoch)
            && hunt.IsHunting
            && !string.IsNullOrWhiteSpace(
                move.ActiveMovementOperationOwnerForDiagnostics)
            && string.Equals(
                target.ReservedByPersistentId,
                ActorId(hunter),
                StringComparison.Ordinal);
        int traversalVersionBeforeWalls = grid.TraversalVersion;
        int wallsAdded = SurroundHunterWithFaultWalls(hunter);
        int traversalVersionAfterWalls = grid.TraversalVersion;
        int openCardinalExitsAfterWalls = CountWalkableCardinalExits(
            hunter.GetNowXY());
        Check(red.CommittedLevel == SettlementThreatAlertLevel.Red
                && ownerStableAtCollision
                && wallsAdded > 0
                && traversalVersionAfterWalls > traversalVersionBeforeWalls,
            "HUNT_ALERT_RED_AND_TOPOLOGY_COLLISION",
            $"alert={red.CommittedLevel};epoch={epoch};gate={HasGuardGate(hunter, epoch)};"
            + $"hunting={hunt.IsHunting};moveOwner={move.ActiveMovementOperationOwnerForDiagnostics};"
            + $"walls={wallsAdded};openCardinalExits={openCardinalExitsAfterWalls};"
            + $"traversalVersion={traversalVersionBeforeWalls}->{traversalVersionAfterWalls}");

        long terminalsBefore = brain.CaptureRuntimeGateSnapshot().ActionTerminals;
        long failuresBefore = brain.RuntimeExecutionFailureCount;
        bool overlap = false;
        AIActionFailure capturedFailure = AIActionFailure.None;
        deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            bool huntCurrent = brain.bestAction?.actionset is AIHunt
                && hunt.IsHunting;
            bool guardCurrent = brain.bestAction?.actionset is AIWork
                && brain.HasRunningWorkAction
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Guard;
            overlap |= huntCurrent && guardCurrent;
            if (!hunt.IsHunting
                && brain.RuntimeExecutionFailureCount > failuresBefore)
            {
                capturedFailure = brain.LastActionFailure;
                break;
            }
            yield return null;
        }
        CharacterAiRuntimeGateSnapshot terminalGate =
            brain.CaptureRuntimeGateSnapshot();
        bool topologyTerminal = !hunt.IsHunting
            && brain.RuntimeExecutionFailureCount == failuresBefore + 1
            && terminalGate.ActionTerminals == terminalsBefore + 1
            && capturedFailure.Kind == AIActionFailureKind.NoPath
            && target.CurrentHealth == healthBefore
            && string.IsNullOrWhiteSpace(target.ReservedByPersistentId);
        Check(topologyTerminal,
            "HUNT_ALERT_TOPOLOGY_NOPATH_EXACT_TERMINAL",
            $"hunting={hunt.IsHunting};failures={failuresBefore}->{brain.RuntimeExecutionFailureCount};"
            + $"terminals={terminalsBefore}->{terminalGate.ActionTerminals};failure={capturedFailure};"
            + $"health={healthBefore}->{target.CurrentHealth};reservation={target.ReservedByPersistentId}");
        Check(!overlap,
            "HUNT_ALERT_NO_HUNT_GUARD_OWNER_OVERLAP",
            $"overlap={overlap};action={brain.CurrentActionDebugLabel};assigned={work.AssignedWorkTypeId}");

        CleanupFaultWalls();
        hunter.SetAiPaused(true);
        brain.StopCurrentActionForReplan("hunt-alert-post-topology-neutralize");
        move.CancelActiveMovement();
        hunt.StopHunting("hunt-alert-post-topology-neutralize");
        wildlife.DesignateHunt(target.WildlifeId, false);
        RestoreNeutralPersistentState(hunter);
        bool postCollisionReset =
            deprivation.DebugResetForDeterministicScenario(hunter);
        brain.availableActions = new[] { workAction };
        hunter.SetAiPaused(false);
        brain.PreferWorkActionOnNextDecision(BuiltInWorkTypeIds.Guard, 180f);
        brain.RequestImmediateReplan(clearFailures: true);
        bool guardAfterTerminal = false;
        deadline = Time.realtimeSinceStartup + ActionTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            guardAfterTerminal = brain.bestAction?.actionset is AIWork
                && brain.HasRunningWorkAction
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Guard;
            overlap |= hunt.IsHunting && guardAfterTerminal;
            if (guardAfterTerminal) break;
            yield return null;
        }
        Check(postCollisionReset,
            "HUNT_ALERT_POST_COLLISION_NEEDS_NEUTRAL",
            $"reset={postCollisionReset};gate={HasGuardGate(hunter, epoch)}");
        Check(guardAfterTerminal,
            "HUNT_ALERT_GUARD_AFTER_HUNT_TERMINAL",
            $"guard={guardAfterTerminal};action={brain.CurrentActionDebugLabel};"
            + $"assigned={work.AssignedWorkTypeId};hunting={hunt.IsHunting}");
        Check(!overlap,
            "HUNT_ALERT_NO_OWNER_OVERLAP_THROUGH_HANDOFF",
            $"overlap={overlap};guard={guardAfterTerminal};hunting={hunt.IsHunting}");

        events.Publish(new InvasionResolvedEvent(true, 0f));
        AdvanceAlertFourHours();
        deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        while (Time.realtimeSinceStartup < deadline
               && (alerts.Capture().CommittedLevel != SettlementThreatAlertLevel.Green
                   || work.HasEmergencyResponseWorkGateForDiagnostics
                   || alarmRuntime.ReturningResponderCountForDiagnostics > 0))
        {
            alarmRuntime.Tick();
            yield return null;
        }
        hunter.SetAiPaused(true);
        brain.StopCurrentActionForReplan("hunt-alert-topology-complete");
        move.CancelActiveMovement();
        hunt.StopHunting("hunt-alert-topology-complete");
        brain.availableActions = originalActions;
        yield return null;
        yield return null;
        CharacterAiRuntimeGateSnapshot after = brain.CaptureRuntimeGateSnapshot();
        Check(alerts.Capture().DesiredLevel == SettlementThreatAlertLevel.Green
                && alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green
                && !work.HasEmergencyResponseWorkGateForDiagnostics
                && !hunt.IsHunting
                && string.IsNullOrWhiteSpace(target.ReservedByPersistentId),
            "HUNT_ALERT_FINAL_OWNERSHIP_CONVERGED",
            $"alert={alerts.Capture().DesiredLevel}/{alerts.Capture().CommittedLevel};"
            + $"gate={work.HasEmergencyResponseWorkGateForDiagnostics};"
            + $"hunting={hunt.IsHunting};reservation={target.ReservedByPersistentId}");
        Check(after.InvariantAnomalies == before.InvariantAnomalies
                && after.LivePathRequests == 0
                && after.LiveReservations == 0,
            "HUNT_ALERT_RUNTIME_GATE_CONSERVED",
            $"invariants={before.InvariantAnomalies}->{after.InvariantAnomalies};"
            + $"paths={after.LivePathRequests};reservations={after.LiveReservations}");
    }

    private bool TryPositionHunterForPursuit(
        CharacterActor hunter,
        WildlifeActor target,
        out Vector2Int start,
        out int pathCount)
    {
        start = default;
        pathCount = 0;
        if (hunter == null || target == null || grid == null) return false;
        Vector2Int original = hunter.GetNowXY();
        foreach (Vector2Int candidate in grid.SearchPath(original)
                     .GetReachablePositions()
                     .Where(position => grid.IsWalkable(position)
                         && CanSealAllCardinalExits(position)
                         && !wildlife.CanAttackHuntTargetFrom(
                             hunter,
                             target,
                             grid,
                             position))
                     .Distinct()
                     .OrderBy(position => Manhattan(position, original)))
        {
            Queue<GridMoveStep> path = grid.GetMovePath(
                candidate,
                position => wildlife.CanAttackHuntTargetFrom(
                    hunter,
                    target,
                    grid,
                    position));
            if (path == null || path.Count < 12 || path.Count > 32) continue;
            start = candidate;
            pathCount = path.Count;
            hunter.transform.position = grid.GetWorldPos(candidate);
            return true;
        }
        return false;
    }

    private bool CanSealAllCardinalExits(Vector2Int center)
    {
        return new[]
            {
                center + Vector2Int.left,
                center + Vector2Int.right,
                center + Vector2Int.up,
                center + Vector2Int.down
            }
            .All(position => grid.GetGridCell(position) == null
                || grid.IsMovementBlockedByWall(position)
                || grid.GetGridCell(position)
                    .GetOccupant(GridLayer.Building) == null);
    }

    private int SurroundHunterWithFaultWalls(CharacterActor hunter)
    {
        if (hunter == null || grid == null) return 0;
        Vector2Int center = hunter.GetNowXY();
        int added = 0;
        foreach (Vector2Int position in new[]
                 {
                     center + Vector2Int.left,
                     center + Vector2Int.right,
                     center + Vector2Int.up,
                     center + Vector2Int.down
                 })
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell == null || cell.GetOccupant(GridLayer.Building) != null)
                continue;
            ChaosFaultWall wall = new($"qa-hunt-wall:{position}", position);
            if (grid.RegisterOccupant(
                    wall,
                    GridLayer.Building,
                    wall.Positions,
                    connectPositions: false))
            {
                faultWalls.Add(wall);
                added++;
            }
        }
        if (added > 0) gridSystem?.NotifyGridObjectChanged();
        return added;
    }

    private void CleanupFaultWalls()
    {
        if (grid != null)
        {
            foreach (ChaosFaultWall wall in faultWalls.ToArray())
                grid.RemoveOccupant(
                    wall,
                    GridLayer.Building,
                    wall.Positions,
                    disconnectPositions: false);
        }
        faultWalls.Clear();
        gridSystem?.NotifyGridObjectChanged();
    }

    private int CountWalkableCardinalExits(Vector2Int center) => new[]
        {
            center + Vector2Int.left,
            center + Vector2Int.right,
            center + Vector2Int.up,
            center + Vector2Int.down
        }
        .Count(position => grid.GetGridCell(position) != null
            && grid.IsWalkable(position));

    private string FindStackableChaosItemId() => items.GetAllStacks()
        .Where(stack => stack != null
            && stack.AvailableQuantity > 0
            && !stack.Forbidden
            && string.IsNullOrWhiteSpace(stack.ItemInstanceId)
            && !string.IsNullOrWhiteSpace(stack.ItemId))
        .OrderBy(stack => stack.ItemId, StringComparer.Ordinal)
        .Select(stack => stack.ItemId)
        .FirstOrDefault();

    private Vector2Int FindHaulSeedPosition(CharacterActor subject)
    {
        Vector2Int origin = subject?.GetNowXY() ?? Vector2Int.zero;
        HashSet<Vector2Int> occupied = items.GetAllStacks()
            .Where(stack => stack != null)
            .Select(stack => stack.Position)
            .ToHashSet();
        Vector2Int[] reachable = grid.SearchPath(origin)
            .GetReachablePositions()
            .Where(position => grid.IsWalkable(position)
                && !occupied.Contains(position))
            .ToArray();
        Vector2Int? distant = reachable
            .Where(position => Manhattan(origin, position) is >= 6 and <= 12)
            .OrderByDescending(position => Manhattan(origin, position))
            .ThenBy(position => position.x)
            .ThenBy(position => position.y)
            .Select(position => (Vector2Int?)position)
            .FirstOrDefault();
        return distant ?? reachable
            .OrderByDescending(position => Manhattan(origin, position))
            .ThenBy(position => position.x)
            .ThenBy(position => position.y)
            .Select(position => (Vector2Int?)position)
            .FirstOrDefault() ?? origin;
    }

    private string DescribeItemStacks(string itemId) => string.Join(
        " || ",
        items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .Select(stack => $"{stack.StackId}:{stack.State}:q={stack.Quantity}:r={stack.ReservedQuantity}:"
                + $"pos={stack.Position}:dest={stack.DestinationId}"));

    private bool TryFindRestFacilityPosition(
        CharacterActor subject,
        out Vector2Int position,
        out string failure)
    {
        position = default;
        failure = "no reachable empty hallway cell at distance 2..8";
        if (subject == null || grid == null) return false;

        Vector2Int start = subject.GetNowXY();
        HashSet<Vector2Int> actorCells = actors
            .Where(actor => actor != null)
            .Select(actor => actor.GetNowXY())
            .ToHashSet();
        List<(Vector2Int Position, int Distance, int PathCount)> candidates = new();
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                Vector2Int candidate = new(x, y);
                int distance = Manhattan(start, candidate);
                GridCell cell = grid.GetGridCell(candidate);
                if (distance < 2 || distance > 8
                    || cell == null
                    || actorCells.Contains(candidate)
                    || cell.GetOccupant(GridLayer.Building) != null
                    || !cell.IsBuildableArea
                    || !cell.HasOccupantInLayer(GridLayer.Hallway)
                    || !grid.IsWalkable(candidate))
                {
                    continue;
                }

                Queue<GridMoveStep> path = grid.GetMovePathTo(start, candidate);
                if (path == null || path.Count == 0) continue;
                candidates.Add((candidate, distance, path.Count));
            }
        }

        if (candidates.Count == 0) return false;
        var selected = candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.PathCount)
            .ThenBy(candidate => candidate.Position.x)
            .ThenBy(candidate => candidate.Position.y)
            .First();
        position = selected.Position;
        failure = $"selected={position};distance={selected.Distance};path={selected.PathCount};"
            + $"candidates={candidates.Count}";
        return true;
    }

    private BuildableObject CreateRestFacility(
        Vector2Int position,
        string suffix,
        float useDuration)
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        runtimeFacilityDefinitions.Add(data);
        data.name = "QA_Chaos_Rest_" + suffix;
        data.id = 984000 + runtimeFacilityDefinitions.Count;
        data.objectName = "QA Chaos Rest " + suffix;
        data.width = 1;
        data.height = 1;
        data.layer = GridLayer.Building;
        data.category = BuildingCategory.Special;
        data.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
        data.unlocked = true;
        BuildingSO visualTemplate = Resources.LoadAll<BuildingSO>("SO/Building")
            .FirstOrDefault(candidate => candidate != null && candidate.sprite != null);
        data.sprite = visualTemplate?.sprite;
        data.icon = visualTemplate?.icon ?? visualTemplate?.sprite;
        data.Facility = new FacilityData
        {
            roles = FacilityRole.Rest,
            capacity = 1,
            useDuration = useDuration,
            requiredWorkers = 0,
            disabledWhenDamaged = false
        };
        data.AbilityModules.Add(new BuildingNeedRecoveryAbility
        {
            recovery = new FacilityNeedRecoveryData { sleep = 12f }
        });
        data.ValidateAbilitiesOrThrow();

        GridBuildingFactory factory = new(building =>
            InjectGameObject(building.gameObject));
        BuildableObject facility = factory.Create(grid, data, position);
        if (facility == null) return null;
        facility.SetGrid(grid);
        facility.Initialization(data, position);
        if (!grid.RegisterOccupant(
                facility,
                data.layer,
                data.GetGridPosList(position),
                connectPositions: false))
        {
            Destroy(facility.gameObject);
            return null;
        }
        runtimeFacilities.Add(facility);
        gridSystem.NotifyGridObjectChanged();
        return facility;
    }

    private void InjectGameObject(GameObject target)
    {
        if (scope?.Container == null || target == null) return;
        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            scope.Container.Inject(component);
        }
    }

    private void CleanupRuntimeFacilities()
    {
        foreach (BuildableObject facility in runtimeFacilities.ToArray())
        {
            if (facility != null && !facility.IsBuildingDestroyed)
                facility.DestroySelf();
        }
        runtimeFacilities.Clear();
        foreach (BuildingSO definition in runtimeFacilityDefinitions)
        {
            if (definition != null) Destroy(definition);
        }
        runtimeFacilityDefinitions.Clear();
        gridSystem?.NotifyGridObjectChanged();
    }

    private void AdvanceAlertFourHours()
    {
        int baseDay = calendar.Day;
        int baseHour = calendar.Hour;
        for (int offset = 1; offset <= 4; offset++)
        {
            int total = baseHour + offset;
            calendar.SetDateTime(baseDay + total / 24, total % 24);
            alertRuntime.Tick();
            alarmRuntime.Tick();
        }
    }

    private bool HasGuardGate(CharacterActor actor, long epoch)
    {
        AbilityWork work = actor?.GetAbility<AbilityWork>();
        return work != null
            && work.HasEmergencyResponseWorkGateForDiagnostics
            && work.EmergencyResponseWorkEpochForDiagnostics == epoch
            && work.EmergencyResponseOnlyWorkTypeForDiagnostics == BuiltInWorkTypeIds.Guard;
    }

    private string DescribeResponders(IEnumerable<CharacterActor> values) => string.Join(
        " || ",
        values.Select(actor =>
        {
            AbilityWork work = actor.GetAbility<AbilityWork>();
            return $"{ActorId(actor)}:state={actor.CurrentLifecycleState}:paused={actor.IsAiPaused()}:"
                + $"gate={work?.HasEmergencyResponseWorkGateForDiagnostics}/{work?.EmergencyResponseOnlyWorkTypeForDiagnostics}:"
                + $"epoch={work?.EmergencyResponseWorkEpochForDiagnostics}:action={actor.Brain?.CurrentActionDebugLabel}";
        }));

    private int CountWorldItem(string itemId) => items?.GetAllStacks()
        .Where(stack => stack != null
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
        .Sum(stack => stack.Quantity) ?? 0;

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    private static CharacterBodyPartHealthState ClonePart(
        CharacterBodyPartHealthState part) => new()
        {
            bodyPart = part.bodyPart,
            maxHealth = part.maxHealth,
            currentHealth = part.currentHealth,
            bleedingPerSecond = part.bleedingPerSecond
        };

    private static bool IsAlarmEligible(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.Brain != null
            && actor.TryGetAbility(out AbilityWork _)
            && actor.Brain.availableActions?.Any(action => action?.actionset is AIWork) == true
            && actor.Stats?.EvaluatePerformance(
                CharacterPerformanceFormulaIds.AlarmResponse)?.IsApplicable == true;
    }

    private static CharacterActor[] LiveActors() => UnityEngine.Object
        .FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None)
        .Select(CharacterActorCollection.GetCanonical)
        .Where(actor => actor != null
            && !actor.IsDead
            && actor.characterType is not CharacterType.Customer
                and not CharacterType.Intruder
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
        .Distinct()
        .ToArray();

    private static string ActorId(CharacterActor actor) =>
        actor?.Identity?.PersistentId ?? string.Empty;

    private static void Neutralize(CharacterActor actor)
    {
        if (actor?.Stats == null) return;
        foreach (CharacterCondition condition in actor.Stats.StatSnapshot.Keys.ToArray())
        {
            actor.Stats.Stats[condition] = 100f;
        }
    }

    private static void RestoreNeutralPersistentState(CharacterActor actor)
    {
        if (actor?.Stats == null) return;
        Dictionary<CharacterCondition, float> values = actor.Stats.StatSnapshot
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (CharacterCondition condition in values.Keys.ToArray())
        {
            values[condition] = 100f;
        }
        actor.Stats.RestorePersistentState(
            values,
            actor.CurrentHealth,
            actor.InjurySeverity,
            100f,
            Array.Empty<CharacterMoodFactorSnapshot>());
    }

    private T Resolve<T>() where T : class
    {
        try { return scope?.Container?.Resolve<T>(); }
        catch { return null; }
    }

    private IEnumerator ExecuteGuarded(IEnumerator root)
    {
        Stack<IEnumerator> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek();
            object yielded;
            try
            {
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                yielded = current.Current;
            }
            catch (Exception exception)
            {
                string detail = exception.GetType().Name + ": "
                    + exception.Message + "\n" + exception.StackTrace;
                consoleIssues.Add("Exception: " + detail);
                Check(false, "CHAOS_UNHANDLED_EXCEPTION", detail);
                yield break;
            }
            if (yielded is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return yielded;
        }
    }

    private bool Check(bool condition, string key, string detail)
    {
        rows.Add($"{(condition ? "PASS" : "FAIL")}\t{key}\t{detail}");
        if (!condition) failures.Add(key + ": " + detail);
        return condition;
    }

    private void CaptureIssue(string condition, string stack, LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            consoleIssues.Add(type + ": " + condition + "\n" + stack);
        }
        else if (type == LogType.Warning)
        {
            consoleIssues.Add("Warning: " + condition);
        }
    }

    private void FinishRun()
    {
        if (finished) return;
        finished = true;
        try
        {
            if (events != null
                && alerts?.Capture().ActiveIncidentIds.Contains(
                    "incident:invasion:active") == true)
            {
                events.Publish(new InvasionResolvedEvent(true, 0f));
            }
            CleanupFaultWalls();
            CleanupRuntimeFacilities();
            if (calendar != null) calendar.SetDateTime(originalDay, originalHour);
            DungeonGameRestoreReport report = null;
            if (baseline != null
                && (saves == null || !saves.TryRestore(baseline, out report)))
            {
                failures.Add("CHAOS_BASELINE_RESTORE: "
                    + (report == null
                        ? "missing report"
                        : string.Join(" | ", report.Errors)));
            }
        }
        catch (Exception exception)
        {
            failures.Add("CHAOS_CLEANUP_EXCEPTION: " + exception);
        }
        finally
        {
            Application.logMessageReceived -= CaptureIssue;
            Time.timeScale = originalTimeScale;
            Application.runInBackground = originalRunInBackground;
        }

        rows.Add($"INFO\tCONSOLE\tissues={consoleIssues.Count};"
            + string.Join(" || ", consoleIssues.Select(OneLine)));
        bool passed = failures.Count == 0 && consoleIssues.Count == 0;
        rows.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; "
            + string.Join(" || ", failures.Select(OneLine)));
        string path = CharacterAiAdditionalChaosPlayModeVerifier.GetReportPath(Mode);
        File.WriteAllText(path, string.Join("\n", rows));
        if (passed) Debug.Log($"Additional AI chaos {Mode} passed. {path}");
        else Debug.LogError($"Additional AI chaos {Mode} failed. {path}");
        Destroy(gameObject);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        };
    }

    private void OnDisable()
    {
        if (!finished)
        {
            failures.Add("CHAOS_RUNNER_DISABLED_BEFORE_COMPLETION");
            FinishRun();
        }
    }

    private sealed class ChaosFaultWall : IGridBuildingOccupantCapability
    {
        public ChaosFaultWall(string id, Vector2Int position)
        {
            Id = id ?? string.Empty;
            Positions = new[] { position };
        }

        public string Id { get; }
        public IReadOnlyList<Vector2Int> Positions { get; }
        public int GridId => Id.GetHashCode();
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => false;
        public bool BlocksGridMovement => true;
        public bool AllowsInteriorWalkability => false;
    }

    private static string OneLine(string value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
}
#endif
