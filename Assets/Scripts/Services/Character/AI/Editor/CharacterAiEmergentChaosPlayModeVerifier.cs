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

[InitializeOnLoad]
public static class CharacterAiEmergentChaosPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/character-ai-emergent-chaos-playmode.txt";
    private const string RequestPath =
        "Temp/character-ai-emergent-chaos-playmode.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const int DefaultSeed = 271828;
    private static bool runnerCreated;

    static CharacterAiEmergentChaosPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Run Emergent AI Perfect Storm")]
    public static void RequestRun() => RequestRun(DefaultSeed);

    public static string GetSeedReportPath(int seed) =>
        $"Artifacts/QA/character-ai-emergent-chaos-seed-{seed}.txt";

    public static void RequestRun(int seed)
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.Delete(GetSeedReportPath(seed));
        File.WriteAllText(RequestPath, seed.ToString());
        if (EditorApplication.isPlaying)
        {
            StartRunner(seed);
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
            return;
        }
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            TryStartPendingRunner();
        }
    }

    private static void TryStartPendingRunner()
    {
        if (!File.Exists(RequestPath)) return;
        int seed = DefaultSeed;
        int.TryParse(File.ReadAllText(RequestPath).Trim(), out seed);
        File.Delete(RequestPath);
        StartRunner(seed == 0 ? DefaultSeed : seed);
    }

    private static void StartRunner(int seed)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterAiEmergentChaosPlayModeRunner>() != null)
        {
            runnerCreated = true;
            return;
        }
        if (runnerCreated) return;
        CharacterAiEmergentChaosPlayModeRunner runner =
            new GameObject("Character AI Emergent Chaos Runner")
                .AddComponent<CharacterAiEmergentChaosPlayModeRunner>();
        runner.Seed = seed;
        runnerCreated = runner != null;
    }
}

public sealed class CharacterAiEmergentChaosPlayModeRunner : MonoBehaviour
{
    private const string MedicineItemId = "medicine:standard";
    private const string CleanWaterItemId = "resource:clean-water";
    private const string ProcedureId = "procedure:emergency-suture";
    private const string MedicalResearchId = "research:survival:medical";
    private const string BreakdownOwnerId = "survival:breakdown";
    private const float SetupTimeout = 20f;
    private const float SurgeryStartTimeout = 90f;
    private const float ChaosTimeout = 30f;
    private const float RecoveryTimeout = 90f;

    private readonly List<string> rows = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly List<GameObject> temporaryObjects = new();
    private readonly List<BuildableObject> temporaryWalls = new();
    private readonly Dictionary<CharacterActor, bool> originalPause = new();
    private readonly Dictionary<CharacterActor, CharacterAiRuntimeGateSnapshot>
        gateBaselines = new();

    private DungeonRuntimeLifetimeScope scope;
    private IDungeonGameSaveService saves;
    private DungeonGameSaveData baseline;
    private ISurgeryQuery surgery;
    private ISurgeryCommandService surgeryCommands;
    private ICharacterDeprivationRuntime deprivation;
    private IWorldItemStackRuntime items;
    private ISettlementAlertService alerts;
    private SettlementAlertRuntime alertRuntime;
    private CharacterAlarmResponseRuntime alarmRuntime;
    private IGameEventBus events;
    private IGameCalendar calendar;
    private IRandomStream breakdownRandom;
    private ulong originalBreakdownState;
    private IAnatomyHealthRuntime anatomy;
    private ICharacterBodyHealthCommand bodyHealth;
    private GridSystemManager gridManager;
    private Grid grid;
    private CharacterActor doctor;
    private CharacterActor patient;
    private CharacterActor breaker;
    private AbilityWork doctorWork;
    private SurgeryOrder order;
    private Facility table;
    private BuildingSO tableAsset;
    private BuildingSO wallAsset;
    private string targetNodeId = string.Empty;
    private float oldTimeScale;
    private bool oldRunInBackground;
    private int originalDay;
    private int originalHour;
    private bool finished;

    public int Seed { get; set; }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        oldTimeScale = Time.timeScale;
        oldRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        Time.timeScale = 8f;
        Application.logMessageReceived += CaptureIssue;
        yield return ExecuteGuarded(RunPerfectStorm());
        FinishRun();
    }

    private IEnumerator RunPerfectStorm()
    {
        rows.Add($"INFO\tSCENARIO\tseed={Seed};revision=emergent-chaos-v2-20260816");
        yield return ResolveWorld();
        if (failures.Count > 0) yield break;
        yield return CreateSurgeryFixture();
        if (failures.Count > 0) yield break;
        yield return WaitForLiveSurgery();
        if (failures.Count > 0) yield break;
        yield return RunChaosWindow();
        if (failures.Count > 0) yield break;
        yield return RecoverAndConverge();
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
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
            }
            // Starting a prepared run restores the saved clock speed. Reapply
            // the verifier speed only after that production transition settles.
            Time.timeScale = 8f;
        }

        Check(scope?.Container != null, "CHAOS_SCOPE_READY", scope?.name ?? "missing");
        Check(LiveActors().Length >= 3, "CHAOS_ACTOR_COUNT",
            $"activeHumanoids={LiveActors().Length}");
        if (scope?.Container == null || LiveActors().Length < 3) yield break;

        saves = Resolve<IDungeonGameSaveService>();
        surgery = Resolve<ISurgeryQuery>();
        surgeryCommands = Resolve<ISurgeryCommandService>();
        deprivation = Resolve<ICharacterDeprivationRuntime>();
        items = Resolve<IWorldItemStackRuntime>();
        alerts = Resolve<ISettlementAlertService>();
        alertRuntime = Resolve<SettlementAlertRuntime>();
        alarmRuntime = Resolve<CharacterAlarmResponseRuntime>();
        events = Resolve<IGameEventBus>();
        calendar = Resolve<IGameCalendar>();
        anatomy = Resolve<IAnatomyHealthRuntime>();
        bodyHealth = Resolve<ICharacterBodyHealthCommand>();
        IRandomStreamProvider randomStreams = Resolve<IRandomStreamProvider>();
        breakdownRandom = randomStreams?.Get("character-deprivation");
        gridManager = FindFirstObjectByType<GridSystemManager>(
            FindObjectsInactive.Include);
        grid = gridManager?.grid;

        bool authoritiesReady = saves != null
            && surgery != null
            && surgeryCommands != null
            && deprivation != null
            && items != null
            && alerts != null
            && alertRuntime != null
            && alarmRuntime != null
            && events != null
            && calendar != null
            && anatomy != null
            && bodyHealth != null
            && breakdownRandom != null
            && grid != null;
        Check(authoritiesReady, "CHAOS_AUTHORITIES_READY",
            $"save={saves != null};surgery={surgery != null}/{surgeryCommands != null};"
            + $"deprivation={deprivation != null};alert={alerts != null}/{alarmRuntime != null};"
            + $"events={events != null};random={breakdownRandom != null};grid={grid != null}");
        if (!authoritiesReady) yield break;

        IEnvironmentalFieldQuery environment =
            Resolve<IEnvironmentalFieldQuery>();
        float environmentDeadline = Time.realtimeSinceStartup + SetupTimeout;
        while (environment != null
               && !environment.IsInitialized
               && Time.realtimeSinceStartup < environmentDeadline)
        {
            yield return null;
        }
        Check(environment?.IsInitialized == true,
            "CHAOS_SAVE_AUTHORITIES_SETTLED",
            $"environment={environment != null};initialized={environment?.IsInitialized == true}");
        if (environment?.IsInitialized != true) yield break;

        baseline = saves.Capture();
        originalBreakdownState = breakdownRandom.State;
        originalDay = calendar.Day;
        originalHour = calendar.Hour;

        CharacterActor[] actors = LiveActors();
        doctor = actors
            .Where(IsAlarmEligible)
            .OrderBy(actor => actor.Identity.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        patient = actors
            .Where(actor => actor != doctor
                && actor.characterType == CharacterType.NPC)
            .OrderBy(actor => HealthRatio(actor))
            .ThenBy(actor => actor.Identity.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        breaker = actors
            .Where(actor => actor != doctor && actor != patient)
            .OrderBy(actor => actor.Identity.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(doctor != null && patient != null && breaker != null,
            "CHAOS_ROLES_READY",
            $"doctor={ActorId(doctor)};patient={ActorId(patient)};breaker={ActorId(breaker)}");
        if (doctor == null || patient == null || breaker == null) yield break;

        foreach (CharacterActor actor in actors)
        {
            originalPause[actor] = actor.IsAiPaused();
            gateBaselines[actor] = actor.Brain?.CaptureRuntimeGateSnapshot() ?? default;
            actor.SetAiPaused(true);
            actor.Brain?.StopCurrentActionForReplan("emergent-chaos-fixture-isolation");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "emergent-chaos-fixture-isolation");
            actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            Neutralize(actor);
            deprivation.DebugResetForDeterministicScenario(actor);
        }
        yield return null;
        yield return null;

        ICharacterProficiencyCommand proficiencies =
            Resolve<ICharacterProficiencyCommand>();
        IBlueprintResearchStateService research =
            Resolve<IBlueprintResearchStateService>();
        Check(proficiencies != null && research != null,
            "CHAOS_SURGERY_QUALIFICATION_AUTHORITY",
            $"proficiency={proficiencies != null};research={research != null}");
        if (proficiencies == null || research == null) yield break;
        proficiencies.AddDirectExperience(
            CharacterPersistentIdentity.Require(doctor),
            BuiltInCharacterProficiencyIds.Medicine,
            900f,
            calendar.AbsoluteHour,
            applyLearningMultiplier: false);
        proficiencies.AddDirectExperience(
            CharacterPersistentIdentity.Require(doctor),
            BuiltInCharacterProficiencyIds.Scholarship,
            300f,
            calendar.AbsoluteHour,
            applyLearningMultiplier: false);
        research.GetState().Projects.RestoreCompleted(
            new ResearchProjectId(MedicalResearchId));
        doctorWork = doctor.GetAbility<AbilityWork>();
        doctorWork?.SetDutyState(AbilityWork.DutyState.OnDuty);
        doctorWork?.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Surgery,
            WorkPriorityLevel.Priority1);
        Check(doctorWork != null, "CHAOS_SURGEON_WORK_READY",
            doctorWork != null ? ActorId(doctor) : "AbilityWork missing");
    }

    private IEnumerator CreateSurgeryFixture()
    {
        ISurgicalProcedureCatalog procedures = Resolve<ISurgicalProcedureCatalog>();
        ISurgicalFacilityQuery facilities = Resolve<ISurgicalFacilityQuery>();
        IRoomLayoutCache rooms = Resolve<IRoomLayoutCache>();
        ICharacterAiWorldRegistry world = Resolve<ICharacterAiWorldRegistry>();
        IAnatomyProfileCatalog anatomyProfiles = Resolve<IAnatomyProfileCatalog>();
        tableAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Medical/M01_응급처치대.asset");
        wallAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        SurgicalProcedureSO procedure = null;
        bool contentReady = procedures?.TryGet(ProcedureId, out procedure) == true
            && facilities != null
            && rooms != null
            && world != null
            && anatomyProfiles != null
            && tableAsset != null
            && wallAsset?.IsStructuralWall == true;
        Check(contentReady, "CHAOS_CONTENT_READY",
            $"procedure={procedure != null};table={tableAsset != null};wall={wallAsset != null};"
            + $"rooms={rooms != null};world={world != null}");
        if (!contentReady) yield break;

        RoomLayout layout = rooms.GetLayout(grid);
        bool placement = TryFindPlacement(
            layout,
            grid,
            tableAsset,
            out Vector2Int tablePosition,
            out Vector2Int supplyPosition);
        Check(placement, "CHAOS_MEDICAL_ROOM_PLACEMENT",
            $"table={tablePosition};supply={supplyPosition};rooms={layout?.Rooms.Count ?? -1}");
        if (!placement) yield break;

        table = CreateInjectedFacility(tableAsset, tablePosition, "QA_Chaos_EmergencyTable");
        bool registered = table != null
            && grid.RegisterOccupant(
                table,
                tableAsset.Placement.Layer,
                table.buildPoses,
                false);
        rooms.Clear();
        SurgicalFacilitySnapshot facilitySnapshot = facilities.Evaluate(
            table,
            procedure.RequiredFacilityTags);
        Check(registered && world.Buildings.Contains(table) && facilitySnapshot.IsAvailable,
            "CHAOS_SURGERY_FACILITY_LIVE",
            $"registered={registered};published={world.Buildings.Contains(table)};"
            + $"available={facilitySnapshot.IsAvailable};failure={facilitySnapshot.BlockFailure.Code}");
        if (!registered || !facilitySnapshot.IsAvailable) yield break;

        AnatomyProfileDefinition profile = anatomyProfiles.GetForSpecies(
            patient.Identity?.SpeciesTag);
        AnatomyNodeDefinition target = profile.Nodes.FirstOrDefault(node =>
                string.Equals(node.NodeId, "arm:left", StringComparison.Ordinal))
            ?? profile.Nodes.FirstOrDefault(node => !node.Vital)
            ?? profile.Nodes.First();
        targetNodeId = target.NodeId;
        bool injured = anatomy.TryDamageNode(
            patient,
            targetNodeId,
            18f,
            0f,
            "emergent-chaos-surgery");
        bodyHealth.ApplyLegacyDamage(
            patient,
            12f,
            "emergent-chaos-assault-target",
            allowDeath: false);
        Check(injured, "CHAOS_PATIENT_INJURED",
            $"patient={ActorId(patient)};node={targetNodeId};health={patient.CurrentHealth:0.##}");

        bool medicine = items.SpawnItemAt(
            MedicineItemId,
            2,
            supplyPosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int medicineAmount);
        bool water = items.SpawnItemAt(
            CleanWaterItemId,
            1,
            supplyPosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int waterAmount);
        Check(medicine && medicineAmount == 2 && water && waterAmount == 1,
            "CHAOS_SURGERY_PHYSICAL_INPUTS",
            $"medicine={medicine}:{medicineAmount};water={water}:{waterAmount};at={supplyPosition}");
        if (!medicine || medicineAmount != 2 || !water || waterAmount != 1) yield break;

        SurgicalSubjectRef subject = new()
        {
            kind = SurgicalSubjectKind.Character,
            subjectId = patient.Identity.PersistentId,
            displayName = patient.Identity.DisplayName,
            speciesId = patient.Identity.SpeciesTag,
            willing = true,
            automaticEmergencyDefault = false
        };
        bool scheduled = surgeryCommands.TrySchedule(
            subject,
            ProcedureId,
            targetNodeId,
            string.Empty,
            doctor.Identity.PersistentId,
            facilities.GetFacilityId(table),
            out order,
            out DomainFailure scheduleFailure);
        Check(scheduled && order != null, "CHAOS_SURGERY_SCHEDULED",
            scheduled
                ? $"order={order.orderId};doctor={ActorId(doctor)};patient={ActorId(patient)}"
                : $"failure={scheduleFailure.Code}:{string.Join(",", scheduleFailure.Parameters.ToArray())}");
        if (!scheduled || order == null) yield break;

        doctor.SetAiPaused(false);
        doctor.Brain.PreferWorkActionOnNextDecision(
            BuiltInWorkTypeIds.Surgery,
            120f);
        doctor.Brain.RequestImmediateReplan(clearFailures: true);
        yield return null;
    }

    private IEnumerator WaitForLiveSurgery()
    {
        float deadline = Time.realtimeSinceStartup + SurgeryStartTimeout;
        float noProgressDeadline = Time.realtimeSinceStartup + 15f;
        float lastProgress = order.completedWork;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (!surgery.TryGetOrder(order.orderId, out order)) break;
            if (order.completedWork > lastProgress + 0.001f)
            {
                lastProgress = order.completedWork;
                noProgressDeadline = Time.realtimeSinceStartup + 15f;
            }
            bool clinical = order.state is SurgeryOrderState.Anesthetizing
                or SurgeryOrderState.Incision
                or SurgeryOrderState.Procedure
                or SurgeryOrderState.Suturing;
            if (clinical
                && doctorWork.isWorking
                && doctorWork.AssignedWorkTypeId == BuiltInWorkTypeIds.Surgery)
            {
                break;
            }
            if (Time.realtimeSinceStartup >= noProgressDeadline)
            {
                rows.Add("INFO\tSURGERY_STALL\t" + DescribeActors());
                break;
            }
            yield return null;
        }

        bool started = surgery.TryGetOrder(order.orderId, out order)
            && order.state is SurgeryOrderState.Anesthetizing
                or SurgeryOrderState.Incision
                or SurgeryOrderState.Procedure
                or SurgeryOrderState.Suturing
            && doctorWork.isWorking
            && doctorWork.AssignedWorkTypeId == BuiltInWorkTypeIds.Surgery
            && patient.IsAiPaused();
        Check(started, "PERFECT_STORM_SURGERY_LIVE",
            $"state={order?.state};work={doctorWork?.isWorking}/{doctorWork?.AssignedWorkTypeId};"
            + $"patientPaused={patient?.IsAiPaused()};progress={order?.completedWork:0.###};"
            + DescribeActors());
    }

    private IEnumerator RunChaosWindow()
    {
        float surgeryProgressAtChaos = order.completedWork;
        long doctorCancellationBefore =
            doctorWork.ActiveWorkCancellationCountForDiagnostics;
        int breakerTerminalBefore = breaker.Brain.ExternalIntentTerminalCount;
        float patientHealthBefore = patient.CurrentHealth;
        float minimumPatientHealth = patientHealthBefore;
        int startFrame = Time.frameCount;

        if (!TryPrepareBreakdownRoute(
                breaker,
                patient.GetNowXY(),
                out Vector2Int breakerStart,
                out Vector2Int wallCell,
                out string routeDetail))
        {
            Check(false, "PERFECT_STORM_BREAKDOWN_ROUTE_READY", routeDetail);
            yield break;
        }
        breaker.SetAiPaused(true);
        breaker.Brain.StopCurrentActionForReplan("emergent-chaos-route-setup");
        breaker.GetAbility<AbilityMove>()?.CancelActiveMovement();
        Neutralize(breaker);
        deprivation.DebugResetForDeterministicScenario(breaker);
        breaker.transform.position = grid.GetWorldPos(breakerStart);
        yield return null;
        yield return null;
        bool breakerNeutralized = !breaker.Brain.IsExternallyDrivenActionActive
            && !deprivation.HasActiveBreakdown(breaker);
        Check(breakerNeutralized,
            "PERFECT_STORM_BREAKER_NEUTRALIZED_BEFORE_AUTHORED_BREAKDOWN",
            $"external={breaker.Brain.IsExternallyDrivenActionActive}:"
            + $"{breaker.Brain.ExternalIntentOwnerId};"
            + $"breakdown={deprivation.HasActiveBreakdown(breaker)}");
        if (!breakerNeutralized) yield break;

        ulong replayState = FindAssaultReplayState(breaker, Seed);
        breakdownRandom.Restore(replayState);
        bool forced = deprivation.DebugForceBreakdown(
            breaker,
            CharacterBreakdownKind.ViolentImpulse);
        // DebugForceBreakdown is the only state injector. Restore the authored
        // stream after its synchronous notifications so the next production
        // breakdown choice, rather than a fixture-side callback, owns the seed.
        breakdownRandom.Restore(replayState);
        breaker.SetAiPaused(false);
        breaker.Brain.RequestImmediateReplan(clearFailures: true);
        Check(forced, "PERFECT_STORM_BREAKDOWN_AUTHORED",
            $"seed={Seed};streamState={replayState};start={breakerStart};wall={wallCell};route={routeDetail}");
        if (!forced) yield break;

        AbilityMove breakerMove = breaker.GetAbility<AbilityMove>();
        bool simultaneousBreakdownMovement = false;
        bool breakdownExternalObserved = false;
        Vector3 breakerWorldStart = breaker.transform.position;
        float movementDeadline = Time.realtimeSinceStartup + ChaosTimeout;
        while (Time.realtimeSinceStartup < movementDeadline)
        {
            bool externalCurrent = breaker.Brain.IsExternallyDrivenActionActive
                && string.Equals(
                    breaker.Brain.ExternalIntentOwnerId,
                    BreakdownOwnerId,
                    StringComparison.Ordinal);
            breakdownExternalObserved |= externalCurrent;
            bool movementCurrent = string.Equals(
                    breakerMove?.ActiveMovementOperationOwnerForDiagnostics,
                    "raw-path",
                    StringComparison.Ordinal)
                && Vector3.Distance(breaker.transform.position, breakerWorldStart) > 0.01f;
            simultaneousBreakdownMovement = externalCurrent && movementCurrent;
            minimumPatientHealth = Mathf.Min(minimumPatientHealth, patient.CurrentHealth);
            if (simultaneousBreakdownMovement) break;
            if (!breakdownExternalObserved
                && deprivation.HasBreakdownKind(
                    breaker,
                    CharacterBreakdownKind.ViolentImpulse))
            {
                // The shared stream is deterministic but other survival checks
                // may run earlier in the frame. Re-arm only before ownership is
                // acquired; never mutate randomness during the live action.
                breakdownRandom.Restore(replayState);
            }
            yield return null;
        }
        Check(simultaneousBreakdownMovement,
            "PERFECT_STORM_BREAKDOWN_BT_MOVEMENT_STARTED",
            $"externalObserved={breakdownExternalObserved};simultaneous={simultaneousBreakdownMovement};"
            + $"owner={breaker.Brain.ExternalIntentOwnerId};frame={Time.frameCount - startFrame};"
            + breaker.Brain.CaptureRuntimeDiagnostics().FormatRecentTrace());
        if (!simultaneousBreakdownMovement) yield break;

        int wallFrame = Time.frameCount - startFrame;
        int traversalVersionBeforeWall = grid.TraversalVersion;
        BuildableObject wall = PlaceWall(wallCell);
        Check(wall != null, "PERFECT_STORM_DYNAMIC_WALL_PLACED",
            $"cell={wallCell};traversalVersion={traversalVersionBeforeWall}->{grid.TraversalVersion};"
            + $"frame={wallFrame}");
        if (wall == null) yield break;

        bool blockedObserved = false;
        bool wallEntryObserved = false;
        bool movementStoppedAfterWall = false;
        bool breakdownTerminal = false;
        float chaosDeadline = Time.realtimeSinceStartup + ChaosTimeout;
        while (Time.realtimeSinceStartup < chaosDeadline)
        {
            blockedObserved |= breakerMove.LastGridMoveWasBlocked
                || breakerMove.LastGridMoveFailureReason is GridMoveFailureReason.TraversalChanged
                    or GridMoveFailureReason.WallBlocked
                    or GridMoveFailureReason.MissingPath;
            wallEntryObserved |= breaker.GetNowXY() == wallCell;
            minimumPatientHealth = Mathf.Min(minimumPatientHealth, patient.CurrentHealth);
            movementStoppedAfterWall |= string.IsNullOrEmpty(
                breakerMove?.ActiveMovementOperationOwnerForDiagnostics);
            breakdownTerminal = breaker.Brain.ExternalIntentTerminalCount
                    == breakerTerminalBefore + 1
                && !breaker.Brain.IsExternallyDrivenActionActive
                && !deprivation.HasActiveBreakdown(breaker);
            if (movementStoppedAfterWall && breakdownTerminal) break;
            yield return null;
        }

        bool topologyTerminatedMove = grid.TraversalVersion > traversalVersionBeforeWall
            && movementStoppedAfterWall
            && breakdownTerminal;
        Check(topologyTerminatedMove && !wallEntryObserved,
            "PERFECT_STORM_LIVE_ROUTE_INVALIDATED",
            $"topologyTerminal={topologyTerminatedMove};blockedSignal={blockedObserved};"
            + $"failure={breakerMove.LastGridMoveFailureReason};wallEntry={wallEntryObserved};"
            + $"breaker={breaker.GetNowXY()};wall={wallCell};"
            + $"traversal={traversalVersionBeforeWall}->{grid.TraversalVersion}");
        Check(breakdownTerminal
                && minimumPatientHealth >= patientHealthBefore - 0.001f,
            "PERFECT_STORM_BREAKDOWN_TERMINAL_NO_LATE_DAMAGE",
            $"terminal={breakerTerminalBefore}->{breaker.Brain.ExternalIntentTerminalCount};"
            + $"kind={breaker.Brain.LastExternalIntentTerminalKind};"
            + $"patientHealth={patientHealthBefore:0.##}/min={minimumPatientHealth:0.##}/"
             + $"final={patient.CurrentHealth:0.##};"
             + breaker.Brain.CaptureRuntimeDiagnostics().FormatRecentTrace());

        int scheduleJitterFrames = Math.Abs(Seed % 3);
        bool jitterTerminalHeld = true;
        for (int frame = 0; frame < scheduleJitterFrames; frame++)
        {
            yield return null;
            minimumPatientHealth = Mathf.Min(
                minimumPatientHealth,
                patient.CurrentHealth);
            jitterTerminalHeld &= !breaker.Brain.IsExternallyDrivenActionActive
                && !deprivation.HasActiveBreakdown(breaker)
                && breaker.Brain.ExternalIntentTerminalCount
                    == breakerTerminalBefore + 1
                && patient.CurrentHealth >= patientHealthBefore - 0.001f;
        }
        Check(jitterTerminalHeld,
            "PERFECT_STORM_SEEDED_SCHEDULE_JITTER",
            $"seed={Seed};frames={scheduleJitterFrames};"
            + $"external={breaker.Brain.IsExternallyDrivenActionActive}:"
            + $"{breaker.Brain.ExternalIntentOwnerId};"
            + $"moveOwner={breakerMove?.ActiveMovementOperationOwnerForDiagnostics};"
            + $"terminal={breakerTerminalBefore}"
            + $"->{breaker.Brain.ExternalIntentTerminalCount};"
            + $"patient={patientHealthBefore:0.##}->{patient.CurrentHealth:0.##}");
        if (!jitterTerminalHeld) yield break;

        events.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot invasionAlert = alerts.Capture();
        long invasionEpoch = invasionAlert.AlertEpochId;
        int invasionFrame = Time.frameCount - startFrame;
        Check(invasionAlert.CommittedLevel == SettlementThreatAlertLevel.Red
                && invasionAlert.ActiveIncidentIds.Contains("incident:invasion:active"),
            "PERFECT_STORM_INVASION_RED_SYNC",
            $"epoch={invasionEpoch};frame={invasionFrame};active=[{string.Join(",", invasionAlert.ActiveIncidentIds)}]");

        AbilityWork breakerWork = breaker.GetAbility<AbilityWork>();
        bool breakerGuardGate = false;
        float guardDeadline = Time.realtimeSinceStartup + ChaosTimeout;
        while (Time.realtimeSinceStartup < guardDeadline)
        {
            alarmRuntime.Tick();
            breakerGuardGate = breakerWork != null
                && breakerWork.HasEmergencyResponseWorkGateForDiagnostics
                && breakerWork.EmergencyResponseWorkEpochForDiagnostics == invasionEpoch
                && breakerWork.EmergencyResponseOnlyWorkTypeForDiagnostics
                    == BuiltInWorkTypeIds.Guard;
            if (breakerGuardGate) break;
            yield return null;
        }

        bool doctorSuspended = alerts.TryGetSuspendedWork(
            ActorId(doctor),
            out SettlementSuspendedWorkSnapshot doctorSuspension);
        bool surgeryStillOwned = surgery.TryGetOrder(order.orderId, out order)
            && order.state is (SurgeryOrderState.Anesthetizing
                or SurgeryOrderState.Incision
                or SurgeryOrderState.Procedure
                or SurgeryOrderState.Suturing
                or SurgeryOrderState.Recovering)
            && order.completedWork >= surgeryProgressAtChaos;
        bool clinicalLaborStillActive = order?.state is (
            SurgeryOrderState.Anesthetizing
            or SurgeryOrderState.Incision
            or SurgeryOrderState.Procedure
            or SurgeryOrderState.Suturing);
        bool recoveryHandoffComplete = order?.state == SurgeryOrderState.Recovering
            && order.completedWork + 0.001f >= order.requiredWork;
        bool doctorResponseAuthorityValid = clinicalLaborStillActive
            ? !doctorWork.HasEmergencyResponseWorkGateForDiagnostics
            : recoveryHandoffComplete
                && (!doctorWork.HasEmergencyResponseWorkGateForDiagnostics
                    || doctorWork.EmergencyResponseWorkEpochForDiagnostics
                            == invasionEpoch
                        && doctorWork.EmergencyResponseOnlyWorkTypeForDiagnostics
                            == BuiltInWorkTypeIds.Guard
                        && (!doctorWork.HasActiveWorkRoutineForDiagnostics
                            || doctorWork.AssignedWorkTypeId
                                != BuiltInWorkTypeIds.Surgery));
        Check(!doctorSuspended
                && doctorResponseAuthorityValid
                && doctorWork.ActiveWorkCancellationCountForDiagnostics
                    == doctorCancellationBefore
                && surgeryStillOwned,
            "PERFECT_STORM_CRITICAL_SURGERY_PROTECTED_FROM_INVASION",
            $"suspended={doctorSuspended}:{doctorSuspension.WorkTypeId};"
            + $"gate={doctorWork.HasEmergencyResponseWorkGateForDiagnostics};"
            + $"gateEpoch={doctorWork.EmergencyResponseWorkEpochForDiagnostics}/{invasionEpoch};"
            + $"gateWork={doctorWork.EmergencyResponseOnlyWorkTypeForDiagnostics};"
            + $"work={doctorWork.isWorking}/{doctorWork.AssignedWorkTypeId}/"
            + $"{doctorWork.HasActiveWorkRoutineForDiagnostics};"
            + $"cancel={doctorCancellationBefore}->{doctorWork.ActiveWorkCancellationCountForDiagnostics};"
            + $"state={order?.state};progress={surgeryProgressAtChaos:0.###}->{order?.completedWork:0.###}");
        Check(breakerGuardGate,
            "PERFECT_STORM_RELEASED_BREAKER_BECOMES_GUARD",
            $"gate={breakerWork?.HasEmergencyResponseWorkGateForDiagnostics};"
            + $"epoch={breakerWork?.EmergencyResponseWorkEpochForDiagnostics}/{invasionEpoch};"
            + $"workType={breakerWork?.EmergencyResponseOnlyWorkTypeForDiagnostics}");
        rows.Add($"INFO\tREPLAY\tseed={Seed};streamState={replayState};"
            + $"startFrame=0;wallFrame={wallFrame};invasionFrame={invasionFrame};"
            + $"doctor={ActorId(doctor)};patient={ActorId(patient)};breaker={ActorId(breaker)};"
            + $"wall={wallCell};epoch={invasionEpoch}");
    }

    private IEnumerator RecoverAndConverge()
    {
        RemoveWalls();
        events.Publish(new InvasionResolvedEvent(true, 0f));
        Check(!alerts.Capture().ActiveIncidentIds.Contains("incident:invasion:active")
                && alerts.Capture().DesiredLevel == SettlementThreatAlertLevel.Green,
            "PERFECT_STORM_INVASION_RESOLVED_SYNC",
            $"desired={alerts.Capture().DesiredLevel};committed={alerts.Capture().CommittedLevel};"
            + $"active=[{string.Join(",", alerts.Capture().ActiveIncidentIds)}]");

        int baseDay = calendar.Day;
        int baseHour = calendar.Hour;
        for (int offset = 1; offset <= 4; offset++)
        {
            SetCalendarOffset(baseDay, baseHour, offset);
            alertRuntime.Tick();
            alarmRuntime.Tick();
            yield return null;
        }

        float progressBeforeReturn = order.completedWork;
        bool madePostChaosProgress = false;
        bool terminal = false;
        float deadline = Time.realtimeSinceStartup + RecoveryTimeout;
        float noProgressDeadline = Time.realtimeSinceStartup + 20f;
        float lastProgress = progressBeforeReturn;
        while (Time.realtimeSinceStartup < deadline)
        {
            alarmRuntime.Tick();
            if (!surgery.TryGetOrder(order.orderId, out order)) break;
            if (order.completedWork > lastProgress + 0.001f)
            {
                madePostChaosProgress = true;
                lastProgress = order.completedWork;
                noProgressDeadline = Time.realtimeSinceStartup + 20f;
            }
            terminal = order.state is SurgeryOrderState.Completed
                or SurgeryOrderState.Failed
                or SurgeryOrderState.Cancelled;
            if (terminal) break;
            if (Time.realtimeSinceStartup >= noProgressDeadline) break;
            yield return null;
        }

        SettlementAlertSnapshot finalAlert = alerts.Capture();
        Check(finalAlert.CommittedLevel == SettlementThreatAlertLevel.Green
                && alarmRuntime.PendingResponderCountForDiagnostics == 0
                && alarmRuntime.ReturningResponderCountForDiagnostics == 0
                && alarmRuntime.AssignedResponderCountForDiagnostics == 0
                && !doctorWork.HasEmergencyResponseWorkGateForDiagnostics
                && !alerts.TryGetSuspendedWork(ActorId(doctor), out _),
            "PERFECT_STORM_EMERGENCY_OWNERSHIP_RELEASED",
            $"level={finalAlert.CommittedLevel};pending={alarmRuntime.PendingResponderCountForDiagnostics};"
            + $"returning={alarmRuntime.ReturningResponderCountForDiagnostics};"
            + $"assigned={alarmRuntime.AssignedResponderCountForDiagnostics};"
            + $"gate={doctorWork.HasEmergencyResponseWorkGateForDiagnostics}");
        Check(terminal && order.completedWork > 0f,
            "PERFECT_STORM_BOUNDED_LIVENESS",
            $"postChaosProgress={madePostChaosProgress};terminal={terminal}:{order?.state};"
            + $"progress={progressBeforeReturn:0.###}->{order?.completedWork:0.###};"
            + DescribeActors());

        foreach (CharacterActor actor in new[] { doctor, patient, breaker })
        {
            actor?.SetAiPaused(true);
            actor?.Brain?.StopCurrentActionForReplan("emergent-chaos-final-fence");
            actor?.GetAbility<AbilityMove>()?.CancelActiveMovement();
        }
        yield return null;
        yield return null;

        bool gateClean = true;
        foreach (CharacterActor actor in new[] { doctor, patient, breaker })
        {
            if (actor?.Brain == null) continue;
            CharacterAiRuntimeGateSnapshot before = gateBaselines[actor];
            CharacterAiRuntimeGateSnapshot after = actor.Brain.CaptureRuntimeGateSnapshot();
            gateClean &= after.InvariantAnomalies == before.InvariantAnomalies
                && after.LivePathRequests == 0
                && after.LiveReservations == 0
                && !actor.Brain.IsExternallyDrivenActionActive;
            rows.Add($"INFO\tGATE\tactor={ActorId(actor)};"
                + $"actions={after.ActionStarts - before.ActionStarts}/"
                + $"{after.ActionTerminals - before.ActionTerminals}/{after.LiveActions};"
                + $"paths={after.PathRequests - before.PathRequests}/"
                + $"{after.PathResults - before.PathResults}/{after.LivePathRequests};"
                + $"reservations={after.ReservationAcquires - before.ReservationAcquires}/"
                + $"{after.ReservationReleases - before.ReservationReleases}/{after.LiveReservations};"
                + $"invariants={before.InvariantAnomalies}->{after.InvariantAnomalies};"
                + $"branches={after.FormatObservedBranchesFrom(in before)}");
        }
        Check(gateClean, "PERFECT_STORM_RUNTIME_GATE_CONSERVED",
            DescribeActors());
    }

    private bool TryPrepareBreakdownRoute(
        CharacterActor actor,
        Vector2Int target,
        out Vector2Int start,
        out Vector2Int wallCell,
        out string detail)
    {
        start = default;
        wallCell = default;
        detail = "no lawful route";
        if (actor?.PathSearchBroker == null || grid == null) return false;
        foreach (int side in new[] { -1, 1 })
        {
            Vector2Int candidateWall = target + new Vector2Int(side, 0);
            if (!IsEmptyWallCell(candidateWall)) continue;
            IEnumerable<GridCell> candidates = grid.GetCells()
                .Where(cell => cell != null
                    && grid.IsWalkable(cell.Position)
                    && !grid.IsMovementBlockedByWall(cell.Position)
                    && Mathf.Abs(cell.Position.x - target.x)
                        + Mathf.Abs(cell.Position.y - target.y) >= 6)
                .OrderBy(cell => Mathf.Abs(cell.Position.y - target.y))
                .ThenByDescending(cell => Mathf.Abs(cell.Position.x - target.x));
            foreach (GridCell candidate in candidates)
            {
                bool choosesWall = candidate.Position.x <= target.x
                    ? candidateWall == target + Vector2Int.left
                    : candidateWall == target + Vector2Int.right;
                if (!choosesWall) continue;
                Queue<GridMoveStep> path = grid.GetMovePath(
                    candidate.Position,
                    position => position == candidateWall);
                if (path == null || path.Count < 4) continue;
                start = candidate.Position;
                wallCell = candidateWall;
                detail = $"pathSteps={path.Count};target={target}";
                return true;
            }
        }
        return false;
    }

    private bool IsEmptyWallCell(Vector2Int position)
    {
        GridCell cell = grid?.GetGridCell(position);
        return cell != null
            && grid.IsWalkable(position)
            && cell.GetOccupant(GridLayer.Building) == null;
    }

    private BuildableObject PlaceWall(Vector2Int position)
    {
        BuildableObject wall = new GridBuildingFactory().Create(
            grid,
            wallAsset,
            position);
        if (wall == null) return null;
        Inject(wall.gameObject);
        wall.SetGrid(grid);
        wall.Initialization(wallAsset, position);
        if (!grid.RegisterOccupant(
                wall,
                GridLayer.Building,
                wallAsset.GetGridPosList(position),
                false))
        {
            Destroy(wall.gameObject);
            return null;
        }
        temporaryWalls.Add(wall);
        temporaryObjects.Add(wall.gameObject);
        gridManager.NotifyGridObjectChanged();
        return wall;
    }

    private void RemoveWalls()
    {
        foreach (BuildableObject wall in temporaryWalls.ToArray())
        {
            if (wall == null) continue;
            grid?.RemoveOccupant(
                GridLayer.Building,
                wall.buildPoses,
                false);
            Destroy(wall.gameObject);
        }
        temporaryWalls.Clear();
        gridManager?.NotifyGridObjectChanged();
    }

    private static ulong FindAssaultReplayState(CharacterActor actor, int seed)
    {
        CharacterAiPersonality personality = actor?.Identity?.Data?.aiPersonality;
        float risk = personality != null
            ? Mathf.InverseLerp(0.25f, 2f, personality.riskTaking)
            : 0.5f;
        float order = personality != null
            ? Mathf.InverseLerp(0.25f, 2f, personality.orderliness)
            : 0.5f;
        float social = personality != null
            ? Mathf.InverseLerp(0.25f, 2f, personality.sociability)
            : 0.5f;
        float vandal = 0.25f + (1f - order) * 0.35f;
        float assault = 0.2f + risk * 0.4f + (1f - social) * 0.1f;
        float restless = 0.2f + (1f - risk) * 0.25f;
        float total = vandal + assault + restless;
        float minimum = vandal / total + 0.01f;
        float maximum = (vandal + assault) / total - 0.01f;
        ulong candidate = unchecked((ulong)(uint)(seed == 0 ? 1 : seed));
        candidate ^= 0x9E3779B97F4A7C15UL;
        for (int attempt = 0; attempt < 100000; attempt++)
        {
            ulong state = candidate + unchecked((ulong)attempt * 0x9E3779B97F4A7C15UL);
            ulong next = XorShift(state);
            float roll = (float)(next >> 40) * (1f / (1u << 24));
            if (roll > minimum && roll < maximum) return state;
        }
        throw new InvalidOperationException(
            $"Could not derive assault replay state for interval {minimum:0.###}-{maximum:0.###}.");
    }

    private static ulong XorShift(ulong state)
    {
        if (state == 0UL) state = 0x9E3779B97F4A7C15UL;
        state ^= state << 13;
        state ^= state >> 7;
        state ^= state << 17;
        return state == 0UL ? 0x9E3779B97F4A7C15UL : state;
    }

    private static bool TryFindPlacement(
        RoomLayout layout,
        Grid grid,
        BuildingSO asset,
        out Vector2Int tablePosition,
        out Vector2Int supplyPosition)
    {
        tablePosition = default;
        supplyPosition = default;
        if (layout == null || grid == null || asset == null) return false;
        foreach (RoomInstance room in layout.Rooms
                     .Where(candidate => candidate != null && candidate.IsUsable)
                     .OrderByDescending(candidate => candidate.Cells.Count))
        {
            HashSet<Vector2Int> roomCells = new(room.Cells);
            foreach (Vector2Int cell in room.Cells.OrderBy(position => position.x))
            {
                IReadOnlyList<Vector2Int> footprint = asset.GetGridPosList(cell);
                if (footprint.Any(position =>
                        !roomCells.Contains(position)
                        || grid.GetGridCell(position) == null
                        || grid.GetGridCell(position)
                            .HasOccupantInLayer(asset.Placement.Layer)))
                    continue;
                Vector2Int supply = room.Cells
                    .Where(position => !footprint.Contains(position))
                    .OrderByDescending(position =>
                        Mathf.Abs(position.x - cell.x)
                        + Mathf.Abs(position.y - cell.y))
                    .FirstOrDefault();
                if (footprint.Contains(supply)) continue;
                tablePosition = cell;
                supplyPosition = supply;
                return true;
            }
        }
        return false;
    }

    private Facility CreateInjectedFacility(
        BuildingSO asset,
        Vector2Int position,
        string objectName)
    {
        GameObject obj = new(objectName);
        temporaryObjects.Add(obj);
        Facility facility = obj.AddComponent<Facility>();
        Inject(obj);
        facility.SetGrid(grid);
        facility.Initialization(asset, position);
        Vector3 world = grid.GetWorldPos(position);
        if (asset.Placement.HasEvenWidth) world.x += 0.5f;
        obj.transform.position = new Vector3(world.x, world.y, obj.transform.position.z);
        return facility;
    }

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

    private static float HealthRatio(CharacterActor actor) => actor == null
        ? float.PositiveInfinity
        : actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth);

    private static string ActorId(CharacterActor actor) =>
        actor?.Identity?.PersistentId ?? string.Empty;

    private static void Neutralize(CharacterActor actor)
    {
        if (actor?.Stats == null) return;
        foreach (CharacterCondition condition in Enum.GetValues(typeof(CharacterCondition)))
            actor.stats[condition] = 100f;
    }

    private void SetCalendarOffset(int day, int hour, int offset)
    {
        int total = hour + offset;
        calendar.SetDateTime(day + total / 24, total % 24);
    }

    private string DescribeActors()
    {
        return string.Join(" || ", new[] { doctor, patient, breaker }
            .Where(actor => actor != null)
            .Select(actor =>
            {
                AbilityWork work = actor.GetAbility<AbilityWork>();
                AbilityMove move = actor.GetAbility<AbilityMove>();
                return $"{ActorId(actor)}:pos={actor.GetNowXY()}:paused={actor.IsAiPaused()}"
                    + $":action={actor.Brain?.CurrentActionDebugLabel}:phase={actor.Brain?.CurrentActionPhase}"
                    + $":external={actor.Brain?.IsExternallyDrivenActionActive}/{actor.Brain?.ExternalIntentOwnerId}"
                    + $":work={work?.isWorking}/{work?.AssignedWorkTypeId}:routine={work?.HasActiveWorkRoutineForDiagnostics}"
                    + $":gate={work?.HasEmergencyResponseWorkGateForDiagnostics}/{work?.EmergencyResponseOnlyWorkTypeForDiagnostics}"
                    + $":move={move?.HasActiveMovementRoutineForDiagnostics}/{move?.LastGridMoveFailureReason}";
            }));
    }

    private T Resolve<T>() where T : class
    {
        try { return scope?.Container?.Resolve<T>(); }
        catch { return null; }
    }

    private void Inject(GameObject target)
    {
        foreach (MonoBehaviour component in
                 target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null) scope.Container.Inject(component);
        }
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
            consoleIssues.Add(type + ": " + condition + "\n" + stack);
        else if (type == LogType.Warning)
            consoleIssues.Add("Warning: " + condition);
    }

    private void FinishRun()
    {
        if (finished) return;
        finished = true;
        try
        {
            RemoveWalls();
            if (breakdownRandom != null) breakdownRandom.Restore(originalBreakdownState);
            if (calendar != null) calendar.SetDateTime(originalDay, originalHour);
            DungeonGameRestoreReport restoreReport = null;
            if (baseline != null
                && (saves == null
                    || !saves.TryRestore(baseline, out restoreReport)))
            {
                failures.Add("CHAOS_BASELINE_RESTORE: "
                    + (restoreReport == null
                        ? "missing report"
                        : string.Join(" | ", restoreReport.Errors)));
            }
        }
        catch (Exception exception)
        {
            failures.Add("CHAOS_CLEANUP_EXCEPTION: " + exception);
        }
        finally
        {
            foreach (GameObject obj in temporaryObjects)
                if (obj != null) Destroy(obj);
            Application.logMessageReceived -= CaptureIssue;
            Time.timeScale = oldTimeScale;
            Application.runInBackground = oldRunInBackground;
        }

        rows.Add($"INFO\tCONSOLE\tissues={consoleIssues.Count};"
            + string.Join(" || ", consoleIssues.Select(OneLine)));
        bool passed = failures.Count == 0 && consoleIssues.Count == 0;
        rows.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; "
            + string.Join(" || ", failures.Select(OneLine)));
        File.WriteAllText(
            CharacterAiEmergentChaosPlayModeVerifier.ReportPath,
            string.Join("\n", rows));
        File.WriteAllText(
            CharacterAiEmergentChaosPlayModeVerifier.GetSeedReportPath(Seed),
            string.Join("\n", rows));
        if (passed)
            Debug.Log("Emergent AI Perfect Storm passed. "
                + CharacterAiEmergentChaosPlayModeVerifier.ReportPath);
        else
            Debug.LogError("Emergent AI Perfect Storm failed. "
                + CharacterAiEmergentChaosPlayModeVerifier.ReportPath);
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

    private static string OneLine(string value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
}
#endif
