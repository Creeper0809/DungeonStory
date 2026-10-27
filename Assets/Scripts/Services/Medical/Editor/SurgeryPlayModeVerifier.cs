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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class SurgeryPlayModeVerifier
{
    public const string RequestPath = "Temp/surgery-playmode.request";
    private const string DefaultReportPath =
        "Artifacts/QA/surgery-playmode-report.txt";
    private const string DefaultCapturePath = "Artifacts/QA/surgery-playmode.png";
    private const string W05IdentityEnvironment =
        "DUNGEONSTORY_W05_SURGERY_IDENTITY_VERIFY";
    private const string AuditOutputEnvironment =
        "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;

    public static bool IsW05IdentityVerification => string.Equals(
        Environment.GetEnvironmentVariable(W05IdentityEnvironment),
        "1",
        StringComparison.Ordinal);

    public static bool IsW01PublicationRecoveryVerification => string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_W01_SURGERY_PUBLICATION_RECOVERY_VERIFY"),
        "1", StringComparison.Ordinal);

    public static bool IsW01ClinicalFailureVerification => string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_W01_SURGERY_CLINICAL_FAILURE_VERIFY"),
        "1", StringComparison.Ordinal);

    public static string ReportPath => IsW05IdentityVerification
        ? Path.Combine(
            RequireAuditOutput(),
            IsW01PublicationRecoveryVerification
                ? "w01-surgery-publication-report.txt"
                : IsW01ClinicalFailureVerification
                    ? "w01-surgery-clinical-failure-report.txt"
                    : "w05-surgery-identity-report.txt")
        : DefaultReportPath;

    public static string CapturePath => IsW05IdentityVerification
        ? Path.Combine(RequireAuditOutput(), "w05-surgery-identity.png")
        : DefaultCapturePath;

    static SurgeryPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/Medical/Request Surgery PlayMode Verification")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory(
            Path.GetDirectoryName(ReportPath) ?? "Artifacts/QA");
        if (IsW05IdentityVerification)
        {
            if (File.Exists(ReportPath))
            {
                throw new IOException(
                    "W05 surgery identity report already exists: " + ReportPath);
            }
        }
        else
        {
            File.Delete(ReportPath);
            File.Delete(CapturePath);
        }
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static string RequireAuditOutput()
    {
        string output = Environment.GetEnvironmentVariable(
            AuditOutputEnvironment);
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "W05 surgery identity verification requires an isolated audit output directory.");
        }
        return Path.GetFullPath(output);
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
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

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode
            || runnerCreated
            || !File.Exists(RequestPath))
        {
            return;
        }

        runnerCreated = true;
        new GameObject("Surgery PlayMode Verification Runner")
            .AddComponent<SurgeryPlayModeVerificationRunner>();
    }
}

public sealed class SurgeryPlayModeVerificationRunner : MonoBehaviour
{
    private const string StandardMedicineItemId = "medicine:standard";
    private const string AnestheticItemId = "medicine:anesthetic";
    private const string DisinfectantItemId = "medicine:disinfectant";
    private const string MedicalVialItemId = "container:medical-vial";
    private const string DreamleafItemId = "resource:dreamleaf";
    private const string AlcoholItemId = "material:alcohol";
    private const string CleanWaterItemId = "resource:clean-water";
    private const string SutureProcedureId = "procedure:emergency-suture";
    private const string ForeignBodyProcedureId =
        "procedure:foreign-body-removal";
    private const string MedicalResearchId = "research:survival:medical";
    private const string AnesthesiaResearchId =
        "research:pharmacology:anesthesia";
    private const float VerificationTimeScale = 4f;
    private const float NoProgressTimeoutSeconds = 60f;
    private const float NoProgressSimulationSeconds = 30f;
    private const int NoProgressFrameCount = 20;
    private const float ActorWorldProgressDistance = 0.05f;
    private const float OverallTimeoutSeconds = 180f;
    private const float OverallSimulationSeconds = 180f;
    private const int OverallFrameCount = 120;
    private const float VerifierHardTimeoutSeconds = 600f;

    private readonly List<string> report = new();
    private readonly List<string> failures = new();
    private readonly List<string> capturedErrors = new();
    private readonly List<string> capturedWarnings = new();
    private readonly List<GameObject> temporaryObjects = new();
    private readonly HashSet<SurgeryOrderState> observedStates = new();
    private readonly List<WorkCompletedIdentityEvent> identityEvents = new();

    private DungeonGameSaveData gameSnapshot;
    private CharacterActor patient;
    private CharacterActor doctor;
    private AbilityWork doctorWork;
    private WorkPriorityLevel originalSurgeryPriority;
    private AbilityWork.DutyState originalDutyState;
    private bool originalPatientAiPaused;
    private float originalTimeScale;
    private bool originalRunInBackground;
    private Vector3 originalCameraPosition;
    private Camera gameplayCamera;
    private IWorldItemStackRuntime items;
    private ISurgeryQuery surgery;
    private IDungeonGameSaveService gameSave;
    private IAnatomyHealthRuntime anatomy;
    private ICharacterDeprivationRuntime deprivation;
    private string targetNodeId = string.Empty;
    private float verifierStartedAt;
    private bool finishing;
    private IDisposable identitySubscription;
    private IDisposable publicationFaultSubscription;
    private const string PublicationFaultMarker = "W01_QA_SURGERY_AFTER_IDENTITY_COMMIT";
    private const string CommandAckFaultMarker = "W01_QA_SURGERY_AFTER_IDENTITY_COMMAND";
    private int publicationFaultCount;
    private int commandAckFaultCount;
    private IWorkCompletionIdentityDeliveryCommand originalCompletionCommand;
    private System.Reflection.FieldInfo completionCommandField;
    private object completionObserver;
    private WorkCompletedIdentityEvent publicationFaultEvent;
    private ulong clinicalFailureExpectedRandomState;

    public string CurrentStage { get; private set; } = "created";
    public float ElapsedRealtimeSeconds =>
        verifierStartedAt > 0f
            ? Time.realtimeSinceStartup - verifierStartedAt
            : 0f;

    public void AddInitialConsoleIssues(
        IEnumerable<string> warnings,
        IEnumerable<string> errors)
    {
        if (warnings != null)
            capturedWarnings.AddRange(warnings);
        if (errors != null)
            capturedErrors.AddRange(errors);
    }

    private void Update()
    {
        if (finishing
            || verifierStartedAt <= 0f
            || ElapsedRealtimeSeconds <= VerifierHardTimeoutSeconds)
        {
            return;
        }

        failures.Add(
            "SURGERY_VERIFIER_HARD_TIMEOUT: stage=" + CurrentStage
            + $"; elapsed={ElapsedRealtimeSeconds:0.0}s; "
            + $"limit={VerifierHardTimeoutSeconds:0}s");
        Finish();
    }

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        verifierStartedAt = Time.realtimeSinceStartup;
        CurrentStage = "bootstrap";
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += OnLogMessageReceived;
        originalTimeScale = Time.timeScale;
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        Time.timeScale = VerificationTimeScale;
        EnsureEventSystem();

        yield return null;
        yield return null;

        DungeonRuntimeLifetimeScope scope = FindScope();
        IGameEventBus gameEvents = Resolve<IGameEventBus>(scope);
        identitySubscription = gameEvents.Subscribe<WorkCompletedIdentityEvent>(
            identityEvents.Add);
        if (SurgeryPlayModeVerifier.IsW01PublicationRecoveryVerification)
            publicationFaultSubscription = gameEvents.Subscribe<WorkCompletedIdentityEvent>(
                InjectPublicationFaultOnce);
        surgery = Resolve<ISurgeryQuery>(scope);
        if (SurgeryPlayModeVerifier.IsW01PublicationRecoveryVerification)
        {
            var binding = Resolve<WorkCompletionDomainFactBinding<SurgeryWorkCompletedFact>>(scope);
            completionObserver = binding.GetType().GetField("observer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(binding)
                ?? throw new InvalidOperationException("Surgery completion observer is missing.");
            completionCommandField = completionObserver.GetType().GetField("completion",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Surgery completion command boundary is missing.");
            originalCompletionCommand = (IWorkCompletionIdentityDeliveryCommand)
                completionCommandField.GetValue(completionObserver);
            if (originalCompletionCommand == null)
                throw new InvalidOperationException("Production surgery identity command is missing.");
            completionCommandField.SetValue(completionObserver, new CompletionAckFaultProbe(this,
                originalCompletionCommand));
        }
        ISurgeryCommandService commands = Resolve<ISurgeryCommandService>(scope);
        ISurgicalProcedureCatalog procedures = Resolve<ISurgicalProcedureCatalog>(scope);
        ISurgicalFacilityQuery facilities = Resolve<ISurgicalFacilityQuery>(scope);
        IRoomLayoutCache rooms = Resolve<IRoomLayoutCache>(scope);
        IBlueprintResearchStateService research = Resolve<IBlueprintResearchStateService>(scope);
        ICharacterWorldQuery characters = Resolve<ICharacterWorldQuery>(scope);
        ICharacterAiWorldRegistry worldRegistry =
            Resolve<ICharacterAiWorldRegistry>(scope);
        IFacilityCandidateCache facilityCandidates =
            Resolve<IFacilityCandidateCache>(scope);
        IWorldItemHaulPlanningService haulPlanning =
            Resolve<IWorldItemHaulPlanningService>(scope);
        IFacilityBufferDestinationClaimQuery destinationClaims =
            Resolve<IFacilityBufferDestinationClaimQuery>(scope);
        ICharacterSurgeryWindowService surgeryWindow =
            Resolve<ICharacterSurgeryWindowService>(scope);
        anatomy = Resolve<IAnatomyHealthRuntime>(scope);
        deprivation = Resolve<ICharacterDeprivationRuntime>(scope);
        items = Resolve<IWorldItemStackRuntime>(scope);
        gameSave = Resolve<IDungeonGameSaveService>(scope);
        IRandomStreamProvider randomStreams =
            Resolve<IRandomStreamProvider>(scope);
        IFluidInfrastructureQuery fluidQuery =
            Resolve<IFluidInfrastructureQuery>(scope);
        IEnvironmentalFieldPersistence environmentFields =
            Resolve<IEnvironmentalFieldPersistence>(scope);
        IGameClock simulationClock = Resolve<IGameClock>(scope);
        ISurgeryPolicyRuntime surgeryPolicies =
            Resolve<ISurgeryPolicyRuntime>(scope);
        IGridSystemProvider gridProvider = Resolve<IGridSystemProvider>(scope);
        randomStreams?.Get("medical:surgery-outcomes").Restore(1UL);
        Grid grid = null;
        if (gridProvider != null)
        {
            gridProvider.TryGetGrid(out grid);
        }

        Check(scope?.Container != null, "SCOPE_READY", "gameplay LifetimeScope resolved");
        Check(surgery != null && commands != null, "SURGERY_RUNTIME_READY", "runtime and commands resolved");
        Check(procedures != null && facilities != null, "SURGERY_CATALOG_READY", "catalog and facility query resolved");
        Check(rooms != null && grid != null, "SURGERY_GRID_READY", "room cache and grid resolved");
        Check(research != null && characters != null, "SURGERY_WORLD_READY", "research and character query resolved");
        Check(
            anatomy != null
                && items != null
                && gameSave != null
                && destinationClaims != null
                && environmentFields != null
                && simulationClock != null
                && gridProvider != null,
            "SURGERY_DATA_READY",
            "anatomy, item runtime, destination claim, environment, clock, authoritative grid, and V18 save service resolved");
        Check(surgeryWindow != null, "SURGERY_UI_READY", "surgery planning window resolved");
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        CurrentStage = "ensure-playable-run";
        Grid preRunGrid = grid;
        yield return EnsurePlayableRun();
        CurrentStage = "baseline-surgery-live-ai";
        bool currentGridResolved = gridProvider.TryGetGrid(out Grid currentGrid)
            && currentGrid != null;
        Check(
            currentGridResolved,
            "SURGERY_POST_RUN_GRID_READY",
            currentGridResolved
                ? $"changed={!ReferenceEquals(preRunGrid, currentGrid)}; size={currentGrid.width}x{currentGrid.height}"
                : "authoritative grid missing after prepared-run commit");
        if (!currentGridResolved)
        {
            Finish();
            yield break;
        }
        grid = currentGrid;
        if (SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification)
        {
            // Input control only: the production RNG still rolls the outcome
            // and severity after real work. Seed1's first draw rounds to zero
            // and would pass the production <= comparison even at chance0.
            const ulong failureSeed = 65537UL;
            DeterministicRandomSequence probe = new(1);
            probe.Restore(failureSeed);
            float successDraw = probe.NextFloat();
            float severityDraw = probe.NextFloat();
            clinicalFailureExpectedRandomState = probe.State;
            if (!Check(randomStreams != null && successDraw > 0f && severityDraw < 0.6f,
                    "W01_CLINICAL_FAILURE_INPUT_READY",
                    $"seed={failureSeed}; chance=0; successDraw={successDraw:R}; severityDraw={severityDraw:R}; expected=Minor"))
            {
                Finish();
                yield break;
            }
            randomStreams.Get("medical:surgery-outcomes").Restore(failureSeed);
        }
        // Starting a prepared run restores the player's saved speed. Reapply the
        // verification speed after that transition so physical hauling and work
        // are observed without making the test depend on editor focus.
        Time.timeScale = VerificationTimeScale;
        CharacterActor[] workers = characters.Characters
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.characterType == CharacterType.NPC)
            .OrderByDescending(actor => actor.Stats.EvaluatePerformance(
                "performance:medical:surgery-success").Value)
            .ToArray();
        if (SurgeryPlayModeVerifier.IsW01PublicationRecoveryVerification)
        {
            ICharacterSettlementStandingQuery standings = Resolve<ICharacterSettlementStandingQuery>(scope);
            doctor = workers.FirstOrDefault(actor => !actor.IsOwner
                && standings?.IsFormalResident(actor) == true);
            patient = workers.FirstOrDefault(actor => actor != doctor && !actor.IsOwner);
            Check(doctor != null && patient != null, "W01_SURGERY_FORMAL_STAFF_DOCTOR",
                "doctor=" + doctor?.Identity?.PersistentId + "; patient=" + patient?.Identity?.PersistentId
                    + "; owner is not used as a departure substitute");
        }
        else
        {
            doctor = workers.FirstOrDefault();
            patient = workers.Skip(1).FirstOrDefault() ?? workers.FirstOrDefault();
        }
        CharacterActor[] surgeryHaulers = workers
            .Where(actor => actor != doctor
                && actor != patient
                && actor.GetComponent<AbilityHaul>() != null
                && actor.GetAbility<AbilityWork>() != null)
            .ToArray();
        Check(doctor != null && patient != null, "SURGERY_ACTORS_READY",
            doctor != null && patient != null
                ? $"doctor={doctor.Identity?.DisplayName}; patient={patient.Identity?.DisplayName}"
                : "doctor or patient missing");
        if (doctor == null || patient == null)
        {
            Finish();
            yield break;
        }
        Check(
            surgeryHaulers.Length > 0,
            "SURGERY_INDEPENDENT_HAULER_READY",
            surgeryHaulers.Length > 0
                ? string.Join(",", surgeryHaulers.Select(actor =>
                    actor.Identity?.DisplayName ?? actor.name))
                : "a third live NPC with AbilityHaul is required");
        if (surgeryHaulers.Length == 0)
        {
            Finish();
            yield break;
        }

        surgeryPolicies?.SetAutomaticEmergencySurgery(
            new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.Character,
                subjectId = patient.Identity?.PersistentId ?? string.Empty,
                displayName = patient.Identity?.DisplayName ?? string.Empty,
                speciesId = patient.Identity?.SpeciesTag ?? string.Empty,
                willing = true,
                automaticEmergencyDefault = false
            },
            false);

        gameSnapshot = gameSave.Capture();
        IServiceSessionRuntime serviceSessions =
            Resolve<IServiceSessionRuntime>(scope);
        serviceSessions?.SetAdvertisingEnabled(
            ServiceCategory.Medical,
            false);
        Check(
            serviceSessions != null
                && !serviceSessions.IsAdvertisingEnabled(
                    ServiceCategory.Medical),
            "SURGERY_MEDICAL_SERVICE_ADVERTISING_ISOLATED",
            "temporary surgery fixture is not exposed to unrelated visitor service demand");
        ICharacterProficiencyCommand proficiencyCommands =
            Resolve<ICharacterProficiencyCommand>(scope);
        IGameCalendar calendar = Resolve<IGameCalendar>(scope);
        Check(
            proficiencyCommands != null && calendar != null,
            "SURGERY_PROFICIENCY_AUTHORITY_READY",
            "proficiency command and calendar resolved");
        if (proficiencyCommands == null || calendar == null)
        {
            Finish();
            yield break;
        }

        // A live surgery requires an actually qualified operator. Prepare that
        // state through the same proficiency command authority used by gameplay;
        // do not bypass the qualification check or patch the performance result.
        proficiencyCommands.AddDirectExperience(
            CharacterPersistentIdentity.Require(doctor),
            BuiltInCharacterProficiencyIds.Medicine,
            900f,
            calendar.AbsoluteHour,
            applyLearningMultiplier: false);
        proficiencyCommands.AddDirectExperience(
            CharacterPersistentIdentity.Require(doctor),
            BuiltInCharacterProficiencyIds.Scholarship,
            300f,
            calendar.AbsoluteHour,
            applyLearningMultiplier: false);

        foreach (CharacterActor actor in characters.Characters
                     .Where(candidate => candidate != null
                         && !candidate.IsDead
                         && candidate.characterType == CharacterType.NPC))
        {
            StabilizeVerificationActor(actor);
        }

        originalPatientAiPaused = patient.IsAiPaused();
        doctorWork = doctor.GetAbility<AbilityWork>();
        if (doctorWork != null)
        {
            originalSurgeryPriority =
                doctorWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Surgery);
            originalDutyState = doctorWork.CurrentDutyState;
            doctorWork.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Surgery,
                WorkPriorityLevel.Priority1);
            doctorWork.SetDutyState(AbilityWork.DutyState.OnDuty);
        }

            research.GetState().Projects.RestoreCompleted(
                new ResearchProjectId(MedicalResearchId));
            BuildingSO tableAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Medical/M01_응급처치대.asset");
            Check(tableAsset != null, "EMERGENCY_TABLE_ASSET", "M01 asset loaded");
            Check(procedures.TryGet(SutureProcedureId, out SurgicalProcedureSO procedure),
                "SUTURE_PROCEDURE", SutureProcedureId);
            if (tableAsset == null || procedure == null)
            {
                Finish();
                yield break;
            }

            RoomLayout layout = rooms.GetLayout(grid);
            Check(TryFindPlacement(
                    layout,
                    grid,
                    tableAsset,
                    patient.GetNowXY(),
                    out Vector2Int tablePosition,
                    out Vector2Int supplyPosition),
                "MEDICAL_ROOM_PLACEMENT",
                $"table={tablePosition}; supply={supplyPosition}; rooms={layout.Rooms.Count}");
            if (failures.Count > 0)
            {
                Finish();
                yield break;
            }

            Facility table = CreateInjectedFacility(
                scope,
                grid,
                tableAsset,
                tablePosition,
                "QA_Surgery_EmergencyTable");
            bool registered = table != null
                && grid.RegisterOccupant(
                    table,
                    tableAsset.Placement.Layer,
                    table.buildPoses,
                    false);
            bool publishedToAiWorld = table != null
                && worldRegistry?.Buildings.Contains(table) == true;
            rooms.Clear();
            SurgicalFacilitySnapshot tableSnapshot = facilities.Evaluate(
                table,
                procedure.RequiredFacilityTags);
            Check(registered, "EMERGENCY_TABLE_REGISTERED", $"position={tablePosition}");
            Check(
                publishedToAiWorld,
                "EMERGENCY_TABLE_AI_WORLD_PUBLISHED",
                publishedToAiWorld
                    ? $"buildingVersion={worldRegistry.BuildingVersion}"
                    : "the live AI world registry cannot see the injected facility");
            Check(tableSnapshot.IsAvailable, "EMERGENCY_TABLE_AVAILABLE",
                tableSnapshot.IsAvailable
                    ? "closed medical room recognized"
                    : tableSnapshot.BlockFailure.Code.ToString());
            if (!registered || !tableSnapshot.IsAvailable)
            {
                Finish();
                yield break;
            }

            gameplayCamera = Camera.main;
            if (gameplayCamera != null)
            {
                originalCameraPosition = gameplayCamera.transform.position;
                Vector3 focus = grid.GetWorldPos(tablePosition);
                gameplayCamera.transform.position =
                    new Vector3(focus.x, focus.y + 0.75f, originalCameraPosition.z);
            }

            IAnatomyProfileCatalog anatomyProfiles =
                Resolve<IAnatomyProfileCatalog>(scope);
            AnatomyProfileDefinition profile =
                anatomyProfiles.GetForSpecies(patient.Identity?.SpeciesTag);
            AnatomyNodeDefinition targetNode =
                profile.Nodes.FirstOrDefault(node => string.Equals(
                    node.NodeId,
                    "arm:left",
                    StringComparison.Ordinal))
                ?? profile.Nodes.FirstOrDefault(node => !node.Vital)
                ?? profile.Nodes.First();
            targetNodeId = targetNode.NodeId;

            AnatomyHealthSnapshot before = anatomy.GetAnatomySnapshot(patient);
            float beforeHealth = GetNodeHealth(before, targetNodeId);
            bool injured = anatomy.TryDamageNode(
                patient,
                targetNodeId,
                SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification ? 2f : 18f,
                0f,
                "수술 PlayMode 검증");
            float injuredHealth = GetNodeHealth(
                anatomy.GetAnatomySnapshot(patient),
                targetNodeId);
            Check(injured && injuredHealth < beforeHealth, "PATIENT_INJURED",
                $"{beforeHealth:0.##}->{injuredHealth:0.##}");

            string medicineId = StandardMedicineItemId;
            bool medicineSpawned = items.SpawnItemAt(
                medicineId,
                2,
                supplyPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawnedMedicine);
            Check(medicineSpawned && spawnedMedicine == 2, "MEDICINE_SPAWNED",
                $"item={medicineId}; amount={spawnedMedicine}; position={supplyPosition}");
            string processWaterDestinationId =
                $"plumbing:process-water:{facilities.GetFacilityId(table)}:{BuiltInWorkTypeIds.Surgery.Value}";
            BuildingProcessFluidAbility processFluidAbility = table.BuildingData
                ?.GetAbility<BuildingProcessFluidAbility>();
            bool requiresProcessWater = processFluidAbility != null
                && processFluidAbility.Supports(BuiltInWorkTypeIds.Surgery)
                && processFluidAbility.cleanWaterPerCycle > 0f;
            int spawnedWater = 0;
            bool waterSpawned = !requiresProcessWater
                || items.SpawnItemAt(
                    CleanWaterItemId,
                    1,
                    table.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    processWaterDestinationId,
                    out spawnedWater);
            Check(
                waterSpawned
                    && spawnedWater == (requiresProcessWater ? 1 : 0),
                "PROCESS_WATER_SPAWNED",
                requiresProcessWater
                    ? $"item={CleanWaterItemId}; amount={spawnedWater}; position={table.centerPos}; destination={processWaterDestinationId}"
                    : "facility-authored-process-water=not-required");

            DungeonEnvironmentalFieldSaveData safeSurgeryEnvironment =
                JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(
                    JsonUtility.ToJson(environmentFields.Capture()));
            safeSurgeryEnvironment.cells.Clear();
            for (int y = 0; y < safeSurgeryEnvironment.height; y++)
            {
                for (int x = 0; x < safeSurgeryEnvironment.width; x++)
                {
                    safeSurgeryEnvironment.cells.Add(
                        new EnvironmentalCellSaveData
                        {
                            x = x,
                            y = y,
                            temperatureC = 22f,
                            airQuality = 100f,
                            lightLevel = 100f
                        });
                }
            }
            environmentFields.Restore(
                environmentFields.PrepareRestore(safeSurgeryEnvironment));
            Check(
                safeSurgeryEnvironment.cells.Count
                    == safeSurgeryEnvironment.width
                        * safeSurgeryEnvironment.height,
                "SURGERY_SAFE_ENVIRONMENT_READY",
                $"cells={safeSurgeryEnvironment.cells.Count}; temperature=22; air=100; light=100");

            Canvas canvas = UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .OrderByDescending(candidate => candidate.sortingOrder)
                .FirstOrDefault();
            Check(canvas != null, "SURGERY_CANVAS_READY", canvas != null ? canvas.name : "canvas missing");
            if (canvas == null)
            {
                Finish();
                yield break;
            }

            surgeryWindow.Open(patient, canvas.transform);
            yield return null;
            GameObject window = GameObject.Find("CharacterSurgeryWindow");
            Check(window != null, "SURGERY_WINDOW_OPENED", "planning window visible");
            if (window == null)
            {
                Finish();
                yield break;
            }

            Button procedureNext = FindSelectorButton(window, "수술Row", "Next");
            Button nodeNext = FindSelectorButton(window, "대상 부위Row", "Next");
            // Stable hierarchy IDs are the interaction contract; localized
            // display labels above may change independently.
            procedureNext = FindSelectorButton(
                window,
                "ProcedureRow",
                "Next");
            nodeNext = FindSelectorButton(
                window,
                "TargetRow",
                "Next");
            Button scheduleButton = window.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button != null && button.name == "Schedule");
            Check(procedureNext != null && nodeNext != null && scheduleButton != null,
                "SURGERY_POINTER_TARGETS_READY",
                $"procedure={procedureNext != null}; node={nodeNext != null}; schedule={scheduleButton != null}");
            if (procedureNext == null || nodeNext == null || scheduleButton == null)
            {
                Finish();
                yield break;
            }

            SendPointerClick(procedureNext);
            int targetIndex = profile.Nodes
                .Select((node, index) => (node, index))
                .First(pair => string.Equals(
                    pair.node.NodeId,
                    targetNodeId,
                    StringComparison.Ordinal))
                .index;
            for (int i = 0; i < targetIndex; i++)
            {
                SendPointerClick(nodeNext);
            }

            SendPointerClick(scheduleButton);
            yield return null;
            string surgeryUiDetail = window.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .FirstOrDefault(text => text != null && text.name == "Details")
                ?.text ?? string.Empty;
            SurgeryOrder order = surgery.ActiveOrders
                .FirstOrDefault(candidate => candidate != null
                    && candidate.IsActive
                    && string.Equals(
                        candidate.subject?.subjectId,
                        patient.Identity?.PersistentId,
                        StringComparison.Ordinal));
            Check(order != null, "SURGERY_SCHEDULED_BY_POINTER",
                order != null
                    ? $"order={order.orderId}; procedure={order.procedureId}; target={order.targetNodeId}"
                    : "no active order; ui=" + surgeryUiDetail);
            if (order == null)
            {
                Finish();
                yield break;
            }
            // Keep the surgeon out of unrelated work until the patient and
            // physical inputs make Surgery a legal candidate. Admission and
            // hauling still execute through their production movement/action
            // routes; only the test party's normal player-facing priorities are
            // made deterministic here.
            doctor.Brain?.StopCurrentActionForReplan(
                "surgery-verifier-awaiting-admission-and-materials");
            doctor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            doctor.SetAiPaused(true);
            foreach (CharacterActor hauler in surgeryHaulers)
            {
                AbilityWork haulerWork = hauler.GetAbility<AbilityWork>();
                haulerWork.SetDutyState(AbilityWork.DutyState.OnDuty);
                haulerWork.WorkPriorities.SetPriority(
                    BuiltInWorkTypeIds.Haul,
                    WorkPriorityLevel.Priority1);
                hauler.SetAiPaused(false);
                hauler.Brain?.StopCurrentActionForReplan(
                    "surgery-verifier-priority-haul");
                hauler.GetAbility<AbilityMove>()?.CancelActiveMovement();
                hauler.Brain?.PreferWorkActionOnNextDecision(
                    BuiltInWorkTypeIds.Haul,
                    120f);
                hauler.Brain?.RequestImmediateReplan(clearFailures: true);
            }
            int identityBaseline = identityEvents.Count;

            Check(string.Equals(order.procedureId, SutureProcedureId, StringComparison.Ordinal),
                "SURGERY_PROCEDURE_SELECTED", order.procedureId);
            Check(string.Equals(order.targetNodeId, targetNodeId, StringComparison.Ordinal),
                "SURGERY_NODE_SELECTED", order.targetNodeId);
            bool exactClaim = destinationClaims.TryGetClaim(
                    order.materialDestinationId,
                    table.centerPos,
                    out FacilityBufferDestinationClaim materialClaim)
                && string.Equals(
                    materialClaim.OwnerDomain,
                    SurgeryMaterialDestinationAuthority.OwnerDomain,
                    StringComparison.Ordinal)
                && string.Equals(
                    materialClaim.OwnerOperationId,
                    order.orderId,
                    StringComparison.Ordinal)
                && string.Equals(
                    materialClaim.OwnerFacilityId,
                    order.facilityId,
                    StringComparison.Ordinal)
                && materialClaim.AnchorKind
                    == FacilityBufferDestinationAnchorKind.LiveFacility;
            Check(
                exactClaim,
                "SURGERY_MATERIAL_DESTINATION_CLAIM_EXACT",
                exactClaim
                    ? $"destination={materialClaim.DestinationId}; facility={materialClaim.OwnerFacilityId}; drop={materialClaim.DropPosition}"
                    : $"destination={order.materialDestinationId}; drop={table.centerPos}; claim missing or mismatched");
            SetOrderSuccessChanceForFixture(order.orderId,
                SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification ? 0f : 1f);
            order = RequireCurrentOrder(order.orderId);
            bool pipedProcessWaterRouteExists = requiresProcessWater
                && fluidQuery != null
                && fluidQuery.TryGetNetwork(table, out _);

            float startedAt = Time.realtimeSinceStartup;
            float nextActorStabilizationAt = startedAt;
            float nextEnvironmentRefreshAt = simulationClock.Time + 5f;
            int initialMedicine = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(stack.ItemId, medicineId, StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            int initialWater = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        CleanWaterItemId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            int initialProcessWater = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        CleanWaterItemId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.DestinationId,
                        processWaterDestinationId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            Dictionary<string, int> requiredMaterials = order.materials
                .Where(requirement => requirement != null && !requirement.optional)
                .GroupBy(requirement => requirement.itemId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(requirement => Mathf.Max(1, requirement.quantity)),
                    StringComparer.Ordinal);
            Dictionary<string, int> maxRoutedMaterials = requiredMaterials.Keys
                .ToDictionary(itemId => itemId, _ => 0, StringComparer.Ordinal);
            bool noDuplicateMaterialRequest = true;
            bool exactHaulPlanObserved = false;
            bool committedMaterialObserved = false;
            bool deliveredMaterialObserved = false;
            float lastAuthoritativeProgressAt = startedAt;
            float noProgressDeadline =
                startedAt + NoProgressTimeoutSeconds;
            float overallDeadline = startedAt + OverallTimeoutSeconds;
            float startedSimulationTime = simulationClock.Time;
            int startedFrame = Time.frameCount;
            float lastAuthoritativeProgressSimulationTime =
                startedSimulationTime;
            int lastAuthoritativeProgressFrame = startedFrame;
            SurgeryOrderState lastProgressState = order.state;
            float lastProgressWork = order.completedWork;
            bool lastCommittedMaterialObserved = false;
            bool lastDeliveredMaterialObserved = false;
            bool lastMaterialsConsumed = order.materialsConsumed;
            bool lastProcessFluidConsumed = order.processFluidConsumed;
            string liveOrderId = order.orderId;
            string liveDoctorId = CharacterPersistentIdentity.Require(doctor).Value;
            string livePatientId = CharacterPersistentIdentity.Require(patient).Value;
            string liveFacilityId = order.facilityId;
            bool currentFormatRestorePerformed = false;
            bool currentFormatRestoreResumed = false;
            bool currentFormatRestoreSingleOwner = true;
            bool restoredSurgeryWasLive = false;
            int restoredSurgeryStartCount = 0;
            long restoredActionStartBaseline = 0L;
            Vector2Int lastPatientPosition = patient.GetNowXY();
            Vector3 lastDoctorWorldPosition = doctor.transform.position;
            long lastHaulHeartbeat = surgeryHaulers.Sum(actor =>
                (long)(actor.GetComponent<AbilityHaul>()?.RoutineHeartbeat ?? 0));
            bool surgeonPriorityIssued = false;
            bool surgeonPriorityAccepted = false;
            string surgeonPriorityFailure = string.Empty;
            while ((order = RequireCurrentOrder(liveOrderId)).IsActive)
            {
                if (SurgeryPlayModeVerifier.IsW01PublicationRecoveryVerification
                    && commandAckFaultCount == 1)
                {
                    yield return VerifySurgeryPublicationRecovery(scope, order, simulationClock);
                    Finish();
                    yield break;
                }
                // Observe work that other gameplay coroutines applied after this
                // verifier yielded. Timeout evaluation must come afterwards or a
                // late but real clinical WU can be ignored on the expiry frame.
                Vector2Int currentPatientPosition = patient.GetNowXY();
                Vector3 currentDoctorWorldPosition = doctor.transform.position;
                bool doctorWorldPositionAdvanced =
                    (currentDoctorWorldPosition - lastDoctorWorldPosition)
                        .sqrMagnitude
                    >= ActorWorldProgressDistance
                        * ActorWorldProgressDistance;
                long currentHaulHeartbeat = surgeryHaulers.Sum(actor =>
                    (long)(actor.GetComponent<AbilityHaul>()?.RoutineHeartbeat ?? 0));
                bool madeAuthoritativeProgress =
                    order.state != lastProgressState
                    || order.completedWork > lastProgressWork + 0.0001f
                    || currentPatientPosition != lastPatientPosition
                    || doctorWorldPositionAdvanced
                    || currentHaulHeartbeat > lastHaulHeartbeat
                    || (committedMaterialObserved
                        && !lastCommittedMaterialObserved)
                    || (deliveredMaterialObserved
                        && !lastDeliveredMaterialObserved)
                    || (order.materialsConsumed && !lastMaterialsConsumed)
                    || (order.processFluidConsumed
                        && !lastProcessFluidConsumed);
                if (madeAuthoritativeProgress)
                {
                    lastAuthoritativeProgressAt =
                        Time.realtimeSinceStartup;
                    noProgressDeadline = lastAuthoritativeProgressAt
                        + NoProgressTimeoutSeconds;
                    lastAuthoritativeProgressSimulationTime =
                        simulationClock.Time;
                    lastAuthoritativeProgressFrame = Time.frameCount;
                    lastDoctorWorldPosition = currentDoctorWorldPosition;
                }

                lastProgressState = order.state;
                lastProgressWork = Mathf.Max(
                    lastProgressWork,
                    order.completedWork);
                lastCommittedMaterialObserved =
                    committedMaterialObserved;
                lastDeliveredMaterialObserved =
                    deliveredMaterialObserved;
                lastMaterialsConsumed = order.materialsConsumed;
                lastProcessFluidConsumed = order.processFluidConsumed;
                lastPatientPosition = currentPatientPosition;
                lastHaulHeartbeat = Math.Max(
                    lastHaulHeartbeat,
                    currentHaulHeartbeat);

                bool overallRealtimeExpired =
                    Time.realtimeSinceStartup >= overallDeadline;
                bool overallSimulationExpired =
                    simulationClock.Time - startedSimulationTime
                        >= OverallSimulationSeconds;
                bool overallFramesExpired =
                    Time.frameCount - startedFrame >= OverallFrameCount;
                bool noProgressRealtimeExpired =
                    Time.realtimeSinceStartup >= noProgressDeadline;
                bool noProgressSimulationExpired =
                    simulationClock.Time
                        - lastAuthoritativeProgressSimulationTime
                        >= NoProgressSimulationSeconds;
                bool noProgressFramesExpired =
                    Time.frameCount - lastAuthoritativeProgressFrame
                        >= NoProgressFrameCount;
                if ((overallRealtimeExpired
                        && (overallSimulationExpired
                            || overallFramesExpired))
                    || (noProgressRealtimeExpired
                        && (noProgressSimulationExpired
                            || noProgressFramesExpired)))
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(order.doctorId)
                    && order.risk != null)
                {
                    // The runtime recalculates risk when the surgeon reserves the
                    // order. Pin only the final QA outcome after that reservation;
                    // movement, hauling, materials, and work remain fully real.
                    SetOrderSuccessChanceForFixture(liveOrderId,
                        SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification ? 0f : 1f);
                    order = RequireCurrentOrder(liveOrderId);
                }

                if (Time.timeScale <= 0f)
                {
                    Time.timeScale = VerificationTimeScale;
                }

                // The temporary table has no authored HVAC source. Keep the
                // fixture's environmental input neutral while production
                // SurgeryEnvironmentRuntime continues to evaluate it. The
                // watchdog below counts real simulation progress, so this
                // cannot turn a stalled clinical action into a false PASS.
                if (simulationClock.Time >= nextEnvironmentRefreshAt)
                {
                    environmentFields.Restore(
                        environmentFields.PrepareRestore(
                            safeSurgeryEnvironment));
                    nextEnvironmentRefreshAt = simulationClock.Time + 5f;
                }

                if (!surgeonPriorityIssued
                    && order.state is SurgeryOrderState.Anesthetizing
                        or SurgeryOrderState.Incision
                        or SurgeryOrderState.Procedure
                        or SurgeryOrderState.Suturing)
                {
                    doctor.SetAiPaused(false);
                    surgeonPriorityAccepted = doctorWork.TrySetPriorityWorkTarget(
                        table,
                        BuiltInWorkTypeIds.Surgery,
                        null,
                        out surgeonPriorityFailure);
                    doctor.Brain?.PreferWorkActionOnNextDecision(
                        BuiltInWorkTypeIds.Surgery,
                        120f);
                    doctor.Brain?.RequestImmediateReplan(clearFailures: true);
                    surgeonPriorityIssued = true;
                }

                if (Time.realtimeSinceStartup >= nextActorStabilizationAt)
                {
                    nextActorStabilizationAt =
                        Time.realtimeSinceStartup + 0.5f;
                    foreach (CharacterActor actor in characters.Characters
                                 .Where(candidate => candidate != null
                                     && !candidate.IsDead
                                     && candidate.characterType
                                         == CharacterType.NPC))
                    {
                        StabilizeVerificationActor(actor);
                    }
                }

                order = RequireCurrentOrder(liveOrderId);
                observedStates.Add(order.state);
                if (!currentFormatRestorePerformed
                    && order.state == SurgeryOrderState.Procedure
                    && order.completedWork > order.anesthesiaWork
                        + order.incisionWork + 0.001f
                    && order.materialsConsumed
                    && order.processFluidConsumed
                    && doctor?.Brain?.HasRunningWorkAction == true
                    && doctorWork?.HasActiveWorkRoutineForDiagnostics == true
                    && doctorWork.AssignedWorkTypeId
                        == BuiltInWorkTypeIds.Surgery)
                {
                    SurgeryOrderState savedState = order.state;
                    float savedCompletedWork = order.completedWork;
                    float savedRequiredWork = order.requiredWork;
                    int savedMedicine = CountPhysicalItem(medicineId);
                    int savedWater = CountPhysicalItem(CleanWaterItemId);
                    int savedPatientOrders = surgery.ActiveOrders.Count(candidate =>
                        candidate != null
                        && candidate.IsActive
                        && string.Equals(
                            candidate.subject?.subjectId,
                            livePatientId,
                            StringComparison.Ordinal));
                    CharacterAiRuntimeGateSnapshot preRestoreGate =
                        doctor.Brain.CaptureRuntimeGateSnapshot();
                    Check(
                        preRestoreGate.LiveActions == 1
                            && savedPatientOrders == 1,
                        "SURGERY_CURRENT_SAVE_LIVE_CLINICAL_STAGE",
                        $"order={liveOrderId}; state={savedState}; work={savedCompletedWork:0.###}/{savedRequiredWork:0.###}; "
                        + $"doctor={liveDoctorId}; action={doctor.Brain.CurrentActionDebugLabel}; "
                        + $"live={preRestoreGate.LiveActions}; patientOrders={savedPatientOrders}");

                    DungeonGameSaveData liveClinicalSave = gameSave.Capture();
                    bool savedFacilityExact =
                        DungeonSaveSectionPayload.TryRead(
                            liveClinicalSave,
                            ModularFacilityWorldSaveSection.Id,
                            out ModularFacilityWorldSaveData savedFacilities)
                        && (savedFacilities.buildings
                            ?? new List<ModularFacilityBuildingSaveData>())
                            .Any(candidate => candidate != null
                                && string.Equals(
                                    candidate.persistentInstanceId,
                                    liveFacilityId,
                                    StringComparison.Ordinal)
                                && candidate.buildingId
                                    == table.BuildingData.id
                                && candidate.centerX == table.centerPos.x
                                && candidate.centerY == table.centerPos.y);
                    Check(
                        savedFacilityExact,
                        "SURGERY_CURRENT_SAVE_FACILITY_EXACT",
                        savedFacilityExact
                            ? $"facility={liveFacilityId}; definition={table.BuildingData.id}; position={table.centerPos}"
                            : $"facility={liveFacilityId}; authoritative world.facilities row missing");
                    if (!savedFacilityExact)
                    {
                        Finish();
                        yield break;
                    }

                    Time.timeScale = 0f;
                    bool restored = gameSave.TryRestore(
                        liveClinicalSave,
                        out DungeonGameRestoreReport liveRestoreReport);
                    Check(
                        restored,
                        "SURGERY_CURRENT_RESTORE_COMMITTED",
                        restored
                            ? $"version={liveClinicalSave.version}; order={liveOrderId}"
                            : string.Join(" | ",
                                liveRestoreReport?.Errors
                                ?? Array.Empty<string>()));
                    if (!restored)
                    {
                        Finish();
                        yield break;
                    }

                    doctor = characters.Characters.FirstOrDefault(candidate =>
                        candidate != null
                        && string.Equals(
                            CharacterPersistentIdentity.TryGet(
                                candidate,
                                out CharacterId candidateId)
                                ? candidateId.Value
                                : string.Empty,
                            liveDoctorId,
                            StringComparison.Ordinal));
                    patient = characters.Characters.FirstOrDefault(candidate =>
                        candidate != null
                        && string.Equals(
                            CharacterPersistentIdentity.TryGet(
                                candidate,
                                out CharacterId candidateId)
                                ? candidateId.Value
                                : string.Empty,
                            livePatientId,
                            StringComparison.Ordinal));
                    order = surgery.ActiveOrders.FirstOrDefault(candidate =>
                        candidate != null
                        && string.Equals(
                            candidate.orderId,
                            liveOrderId,
                            StringComparison.Ordinal));
                    table = worldRegistry.Buildings
                        .OfType<Facility>()
                        .FirstOrDefault(candidate => candidate != null
                            && string.Equals(
                                facilities.GetFacilityId(candidate),
                                liveFacilityId,
                                StringComparison.Ordinal));
                    grid = null;
                    gridProvider.TryGetGrid(out grid);
                    doctorWork = doctor?.GetAbility<AbilityWork>();
                    surgeryHaulers = characters.Characters
                        .Where(actor => actor != null
                            && !actor.IsDead
                            && actor.characterType == CharacterType.NPC
                            && actor != doctor
                            && actor != patient
                            && actor.GetComponent<AbilityHaul>() != null
                            && actor.GetAbility<AbilityWork>() != null)
                        .ToArray();

                    bool exactRestoredState = doctor != null
                        && patient != null
                        && table != null
                        && grid != null
                        && order != null
                        && order.IsActive
                        && order.state == savedState
                        && Mathf.Abs(
                            order.completedWork - savedCompletedWork) <= 0.001f
                        && Mathf.Abs(
                            order.requiredWork - savedRequiredWork) <= 0.001f
                        && order.materialsConsumed
                        && order.processFluidConsumed
                        && string.IsNullOrEmpty(order.doctorId)
                        && string.Equals(
                            order.subject?.subjectId,
                            livePatientId,
                            StringComparison.Ordinal);
                    Check(
                        exactRestoredState,
                        "SURGERY_CURRENT_RESTORE_STATE_PROGRESS_EXACT",
                        order != null
                            ? $"state={savedState}->{order.state}; work={savedCompletedWork:0.###}->{order.completedWork:0.###}; "
                              + $"doctor={order.doctorId}; patient={order.subject?.subjectId}; facility={order.facilityId}"
                            : "active order missing after restore");

                    CharacterAiRuntimeGateSnapshot restoredGate =
                        doctor?.Brain?.CaptureRuntimeGateSnapshot() ?? default;
                    AbilityMove restoredMove = doctor?.GetAbility<AbilityMove>();
                    AbilityHaul restoredHaul = doctor?.GetComponent<AbilityHaul>();
                    bool noTransientOwner = doctor?.Brain != null
                        && doctorWork != null
                        && !doctor.Brain.HasRunningAction
                        && !doctor.Brain.IsExternallyDrivenActionActive
                        && restoredGate.LiveActions == 0
                        && !doctorWork.isWorking
                        && !doctorWork.HasActiveWorkRoutineForDiagnostics
                        && restoredMove?.HasActiveMovementRoutineForDiagnostics
                            != true
                        && string.IsNullOrWhiteSpace(
                            restoredMove?
                                .ActiveMovementOperationOwnerForDiagnostics)
                        && restoredHaul?.IsHauling != true;
                    Check(
                        noTransientOwner,
                        "SURGERY_CURRENT_RESTORE_NO_TRANSIENT_OWNER",
                        $"action={doctor?.Brain?.CurrentActionDebugLabel}; live={restoredGate.LiveActions}; "
                        + $"work={doctorWork?.isWorking}/{doctorWork?.HasActiveWorkRoutineForDiagnostics}; "
                        + $"move={restoredMove?.ActiveMovementOperationOwnerForDiagnostics}; haul={restoredHaul?.IsHauling}");

                    int restoredPatientOrders = surgery.ActiveOrders.Count(candidate =>
                        candidate != null
                        && candidate.IsActive
                        && string.Equals(
                            candidate.subject?.subjectId,
                            livePatientId,
                            StringComparison.Ordinal));
                    bool restoredClaimExact = order != null
                        && destinationClaims.TryGetClaim(
                            order.materialDestinationId,
                            table.centerPos,
                            out FacilityBufferDestinationClaim restoredClaim)
                        && string.Equals(
                            restoredClaim.OwnerOperationId,
                            liveOrderId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            restoredClaim.OwnerFacilityId,
                            liveFacilityId,
                            StringComparison.Ordinal);
                    int restoredCommittedMedicine =
                        items.GetCommittedHaulDeliveryQuantity(
                            order.materialDestinationId,
                            medicineId);
                    WorldItemStackSnapshot[] restoredDestinationStacks =
                        items.GetAllStacks()
                        .Where(stack => stack != null
                            && string.Equals(
                                stack.DestinationId,
                                order.materialDestinationId,
                                StringComparison.Ordinal))
                        .ToArray();
                    int restoredDestinationQuantity = restoredDestinationStacks
                        .Sum(stack => stack.Quantity);
                    bool materialConserved = CountPhysicalItem(medicineId)
                            == savedMedicine
                        && CountPhysicalItem(CleanWaterItemId) == savedWater
                        && restoredPatientOrders == 1
                        && order != null
                        && restoredCommittedMedicine == 0
                        && restoredDestinationQuantity == 0;
                    Check(
                        materialConserved && restoredClaimExact,
                        "SURGERY_CURRENT_RESTORE_MATERIAL_CONSERVATION",
                        $"medicine={savedMedicine}->{CountPhysicalItem(medicineId)}; "
                        + $"water={savedWater}->{CountPhysicalItem(CleanWaterItemId)}; "
                        + $"patientOrders={restoredPatientOrders}; "
                        + $"committedMedicine={restoredCommittedMedicine}; "
                        + $"destinationQuantity={restoredDestinationQuantity}; "
                        + $"destinationStacks={DescribeDestinationStacks(order.materialDestinationId)}; "
                        + $"claim={restoredClaimExact}");

                    restoredActionStartBaseline = restoredGate.ActionStarts;
                    currentFormatRestorePerformed = true;
                    bool restoredPriorityAccepted = doctorWork
                        .TrySetPriorityWorkTarget(
                            table,
                            BuiltInWorkTypeIds.Surgery,
                            null,
                            out string restoredPriorityFailure);
                    Check(
                        restoredPriorityAccepted,
                        "SURGERY_CURRENT_RESTORE_PRIORITY_TARGET_REBOUND",
                        restoredPriorityAccepted
                            ? $"doctor={liveDoctorId}; order={liveOrderId}; facility={liveFacilityId}"
                            : restoredPriorityFailure);
                    doctor.Brain.PreferWorkActionOnNextDecision(
                        BuiltInWorkTypeIds.Surgery,
                        120f);
                    doctor.Brain.RequestImmediateReplan(clearFailures: true);
                    Time.timeScale = VerificationTimeScale;
                }

                if (currentFormatRestorePerformed && doctor?.Brain != null)
                {
                    CharacterAiRuntimeGateSnapshot resumedGate =
                        doctor.Brain.CaptureRuntimeGateSnapshot();
                    bool resumedSurgery = doctor.Brain.HasRunningWorkAction
                        && doctorWork?.HasActiveWorkRoutineForDiagnostics == true
                        && doctorWork.AssignedWorkTypeId
                            == BuiltInWorkTypeIds.Surgery;
                    if (resumedSurgery && !restoredSurgeryWasLive)
                    {
                        restoredSurgeryStartCount++;
                    }
                    restoredSurgeryWasLive = resumedSurgery;
                    currentFormatRestoreResumed |= resumedSurgery;
                    int liveSurgeryOwners = characters.Characters.Count(actor =>
                    {
                        if (actor == null || actor.IsDead)
                        {
                            return false;
                        }

                        AbilityWork actorWork = actor.GetComponent<AbilityWork>();
                        return actor.Brain?.HasRunningWorkAction == true
                            && actorWork?.HasActiveWorkRoutineForDiagnostics == true
                            && actorWork.AssignedWorkTypeId
                                == BuiltInWorkTypeIds.Surgery;
                    });
                    currentFormatRestoreSingleOwner &= liveSurgeryOwners <= 1;
                }

                foreach (KeyValuePair<string, int> required in requiredMaterials)
                {
                    int destinationWorld = items.GetAllStacks()
                        .Where(stack => stack != null
                            && string.Equals(
                                stack.DestinationId,
                                order.materialDestinationId,
                                StringComparison.Ordinal)
                            && string.Equals(
                                stack.ItemId,
                                required.Key,
                                StringComparison.Ordinal))
                        .Sum(stack => stack.Quantity);
                    int committed = items.GetCommittedHaulDeliveryQuantity(
                        order.materialDestinationId,
                        required.Key);
                    int routed = destinationWorld + committed;
                    maxRoutedMaterials[required.Key] = Mathf.Max(
                        maxRoutedMaterials[required.Key],
                        routed);
                    noDuplicateMaterialRequest &= routed <= required.Value;
                    committedMaterialObserved |= committed > 0;
                    deliveredMaterialObserved |= items.GetAllStacks().Any(
                        stack => stack != null
                            && stack.State == WorldItemStackState.FacilityBuffer
                            && string.Equals(
                                stack.DestinationId,
                                order.materialDestinationId,
                                StringComparison.Ordinal)
                            && string.Equals(
                                stack.ItemId,
                                required.Key,
                                StringComparison.Ordinal));
                }

                if (!exactHaulPlanObserved)
                {
                    exactHaulPlanObserved = characters.Characters
                        .Where(actor => actor != null && !actor.IsDead)
                        .Any(actor => haulPlanning.TryPreviewBestPlan(
                                actor,
                                out WorldItemHaulPlan preview,
                                out _)
                            && preview != null
                            && preview.IsValid
                            && preview.PrimaryDestination
                                == WorldItemHaulDestinationKind.FacilityBuffer
                            && string.Equals(
                                preview.PrimaryDestinationId,
                                order.materialDestinationId,
                                StringComparison.Ordinal)
                            && preview.DeliveryLegs.All(leg =>
                                leg.DropPosition == table.centerPos));
                }

                yield return null;
            }

            observedStates.Add(order.state);
            if (SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification && !order.IsActive)
                Time.timeScale = 0f;
            if (SurgeryPlayModeVerifier.IsW01PublicationRecoveryVerification)
            {
                Check(false, "W01_SURGERY_PUBLICATION_FAULT_REACHED",
                    "expected recovery checkpoint was not observed; faults=" + publicationFaultCount
                        + "; state=" + order.state);
                Finish();
                yield break;
            }
            Check(
                surgeonPriorityIssued && surgeonPriorityAccepted,
                "SURGERY_PRIORITY_TARGET_ACCEPTED",
                $"issued={surgeonPriorityIssued}; accepted={surgeonPriorityAccepted}; failure={surgeonPriorityFailure}");
            Check(
                currentFormatRestorePerformed,
                "SURGERY_CURRENT_RESTORE_TRIGGERED",
                currentFormatRestorePerformed
                    ? $"order={liveOrderId}; clinical save restored"
                    : $"lastState={order.state}; work={order.completedWork:0.###}/{order.requiredWork:0.###}");
            Check(
                currentFormatRestoreResumed
                    && currentFormatRestoreSingleOwner
                    && restoredSurgeryStartCount == 1,
                "SURGERY_CURRENT_RESTORE_AIWORK_RESUMED_EXACT_ONCE",
                $"resumed={currentFormatRestoreResumed}; singleOwner={currentFormatRestoreSingleOwner}; "
                + $"surgeryStarts={restoredSurgeryStartCount}; allStarts={restoredActionStartBaseline}->{doctor?.Brain?.CaptureRuntimeGateSnapshot().ActionStarts}");
            report.Add("[INFO] SURGERY_HAUL_DIAGNOSTICS "
                + DescribeHaulDiagnostics(
                    characters,
                    haulPlanning,
                    order,
                    grid));
            report.Add("[INFO] SURGERY_WORK_DIAGNOSTICS "
                + DescribeSurgeryWorkDiagnostics(
                    table,
                    doctor,
                    doctorWork,
                    order,
                    surgery,
                    worldRegistry,
                    facilityCandidates,
                    grid));
            float healedHealth = GetNodeHealth(
                anatomy.GetAnatomySnapshot(patient),
                targetNodeId);
            int remainingMedicine = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(stack.ItemId, medicineId, StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            int remainingWater = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        CleanWaterItemId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            int remainingProcessWater = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        CleanWaterItemId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.DestinationId,
                        processWaterDestinationId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            SurgeryOrderState expectedTerminalState = SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification
                ? SurgeryOrderState.Failed : SurgeryOrderState.Completed;
            Check(!order.IsActive && order.state == expectedTerminalState,
                SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification
                    ? "SURGERY_CLINICAL_FAILURE_BY_WORK_AI" : "SURGERY_COMPLETED_BY_WORK_AI",
                $"state={order.state}; status={order.statusData?.code}; elapsed={Time.realtimeSinceStartup - startedAt:0.0}s; "
                + $"simulationElapsed={simulationClock.Time - startedSimulationTime:0.###}s; frames={Time.frameCount - startedFrame}; "
                + $"noProgress={Time.realtimeSinceStartup - lastAuthoritativeProgressAt:0.0}s/"
                + $"{simulationClock.Time - lastAuthoritativeProgressSimulationTime:0.###}gameSeconds/"
                + $"{Time.frameCount - lastAuthoritativeProgressFrame}frames; "
                + $"overallLimit={OverallTimeoutSeconds:0}s+{OverallSimulationSeconds:0}gameSeconds|{OverallFrameCount}frames; "
                + $"noProgressLimit={NoProgressTimeoutSeconds:0}s+{NoProgressSimulationSeconds:0}gameSeconds|{NoProgressFrameCount}frames");
            Check(
                exactHaulPlanObserved || committedMaterialObserved,
                "SURGERY_MATERIAL_HAUL_PLAN_PREFLIGHT",
                $"preview={exactHaulPlanObserved}; committed={committedMaterialObserved}; destination={order.materialDestinationId}");
            Check(
                committedMaterialObserved
                    && deliveredMaterialObserved
                    && order.materialsConsumed,
                "SURGERY_MATERIALS_DELIVERED_BY_AI_HAUL",
                $"committed={committedMaterialObserved}; buffered={deliveredMaterialObserved}; consumed={order.materialsConsumed}");
            Check(
                noDuplicateMaterialRequest,
                "SURGERY_REPEATED_MATERIAL_POLL_NO_DUPLICATE",
                string.Join(
                    ",",
                    requiredMaterials.Select(pair =>
                        $"{pair.Key}:required={pair.Value}:maxRouted={maxRoutedMaterials[pair.Key]}")));
            Check(order.materialsRequested && order.materialsConsumed,
                "SURGERY_MATERIAL_FLOW",
                $"requested={order.materialsRequested}; consumed={order.materialsConsumed}; medicine={initialMedicine}->{remainingMedicine}");
            Check(remainingMedicine == initialMedicine - requiredMaterials[medicineId], "SURGERY_PHYSICAL_MEDICINE_CONSUMED",
                $"{initialMedicine}->{remainingMedicine}");
            bool manualProcessWaterConsumed = initialProcessWater == 1
                && remainingProcessWater == 0;
            Check(
                order.processFluidConsumed
                    && (!requiresProcessWater
                        || manualProcessWaterConsumed
                        || pipedProcessWaterRouteExists),
                "SURGERY_PROCESS_FLUID_SOURCE_ACCOUNTED",
                $"consumed={order.processFluidConsumed}; "
                + $"source={(!requiresProcessWater ? "not-required" : manualProcessWaterConsumed ? "manual-container" : pipedProcessWaterRouteExists ? "piped-network" : "unaccounted")}; "
                + $"processWater={initialProcessWater}->{remainingProcessWater}; "
                + $"globalWater={initialWater}->{remainingWater}");
            Check(order.completedWork >= order.requiredWork, "SURGERY_WORK_ACCUMULATED",
                $"{order.completedWork:0.##}/{order.requiredWork:0.##}");
            HashSet<SurgeryOrderState> reachedStates =
                new HashSet<SurgeryOrderState>(observedStates);
            reachedStates.UnionWith(
                order.reachedClinicalStages
                ?? Enumerable.Empty<SurgeryOrderState>());
            Check(reachedStates.Contains(SurgeryOrderState.Anesthetizing)
                    && reachedStates.Contains(SurgeryOrderState.Incision)
                    && reachedStates.Contains(SurgeryOrderState.Procedure)
                    && reachedStates.Contains(SurgeryOrderState.Suturing),
                "SURGERY_STAGES_OBSERVED",
                string.Join(",", reachedStates.OrderBy(state => (int)state)));
            if (SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification)
            {
                VerifyW01ClinicalFailureResult(scope, order, initialWater, remainingWater);
            }
            else
            {
                Check(healedHealth > injuredHealth, "SURGERY_PATIENT_RECOVERED",
                    $"{injuredHealth:0.##}->{healedHealth:0.##}");
                bool fullyHealedForIsolation = anatomy.TryHealNode(
                    patient, targetNodeId, 100f, 100f);
                Check(fullyHealedForIsolation, "SURGERY_POST_COMPLETION_TEST_ISOLATION",
                    $"patient={patient.Identity?.PersistentId}; node={targetNodeId}");
            }
            bool completedClaimRevoked = !destinationClaims.CaptureClaims().Any(
                claim => claim != null
                    && string.Equals(
                        claim.DestinationId,
                        order.materialDestinationId,
                        StringComparison.Ordinal));
            Check(
                completedClaimRevoked,
                "SURGERY_MATERIAL_DESTINATION_CLAIM_REVOKED_AFTER_COMPLETE",
                $"destination={order.materialDestinationId}; remaining={destinationClaims.CaptureClaims().Count}");

            WorkCompletedIdentityEvent[] terminalIdentities = identityEvents
                .Skip(identityBaseline)
                .Where(value => value.Character.Equals(
                    CharacterPersistentIdentity.Require(doctor)))
                .ToArray();
            WorkCompletedIdentityEvent[] domainIdentities = terminalIdentities
                .Where(value => string.Equals(
                    value.WorkId,
                    "surgery:" + order.procedureId,
                    StringComparison.Ordinal))
                .ToArray();
            int genericIdentityCount = terminalIdentities.Count(value =>
                string.Equals(
                    value.WorkId,
                    BuiltInWorkTypeIds.Surgery.Value,
                    StringComparison.Ordinal));
            Check(
                order.completionFactDelivered
                    && W01CompletionAcknowledged(scope, order)
                    && W01CompletionNotificationAttempted(scope, order)
                    && order.completionDoctorId == doctor.Identity.PersistentId
                    && domainIdentities.Length == 1
                    && genericIdentityCount == 0
                    && string.Equals(
                        domainIdentities[0].ProductId,
                        order.resultSucceeded ? order.resultOutcomeId : "outcome:" + order.resultOutcomeId,
                        StringComparison.Ordinal),
                "SURGERY_W05_TERMINAL_IDENTITY_EXACTLY_ONCE",
                "published=" + order.completionFactDelivered
                    + "; domain=" + domainIdentities.Length
                    + "; generic=" + genericIdentityCount
                    + "; domainProduct="
                    + (domainIdentities.Length == 1
                        ? domainIdentities[0].ProductId
                        : "-")
                    + "; outcome=" + order.resultOutcomeId);

            if (SurgeryPlayModeVerifier.IsW05IdentityVerification)
            {
                if (domainIdentities.Length == 1)
                {
                    try
                    {
                        VerifyW01SurgeryTerminalIdentity(scope, order, domainIdentities[0]);
                    }
                    catch (Exception error)
                    {
                        Check(false, "W01_SURGERY_TERMINAL_VERIFICATION_EXCEPTION", error.ToString());
                    }
                }
                Finish();
                yield break;
            }

            UnityEngine.Object.Destroy(window);
            yield return null;
            surgeryWindow.Open(patient, canvas.transform);
            yield return null;
            yield return CaptureScreen(SurgeryPlayModeVerifier.CapturePath);
            GameObject reopenedWindow = GameObject.Find("CharacterSurgeryWindow");
            Button closeButton = reopenedWindow != null
                ? reopenedWindow.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button != null && button.name == "Close")
                : null;
            if (closeButton != null)
            {
                SendPointerClick(closeButton);
                yield return null;
            }

            Check(GameObject.Find("CharacterSurgeryWindow") == null,
                "SURGERY_WINDOW_POINTER_CLOSE",
                "close button consumed the pointer event");

            anatomy.TryDamageNode(
                patient,
                targetNodeId,
                2f,
                0f,
                "수술 취소 목적지 검증");
            items.SpawnItemAt(
                medicineId,
                2,
                supplyPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int cancelSeeded);
            int cancelBefore = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        medicineId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            SurgeryOrder cancelOrder = null;
            DomainFailure cancelScheduleFailure = DomainFailure.None;
            bool cancelScheduled = cancelSeeded == 2
                && commands.TrySchedule(
                    order.subject.Clone(),
                    SutureProcedureId,
                    targetNodeId,
                    string.Empty,
                    doctor.Identity?.PersistentId,
                    facilities.GetFacilityId(table),
                    out cancelOrder,
                    out cancelScheduleFailure);
            Check(
                cancelScheduled && cancelOrder != null,
                "SURGERY_CANCEL_ORDER_CREATED",
                cancelScheduled
                    ? $"order={cancelOrder.orderId}; destination={cancelOrder.materialDestinationId}"
                    : $"seeded={cancelSeeded}; failure={cancelScheduleFailure.Code}:{string.Join(",", cancelScheduleFailure.Parameters.ToArray())}");
            if (cancelScheduled && cancelOrder != null)
            {
                bool cancelled = commands.TryCancel(
                    cancelOrder.orderId,
                    out DomainFailure cancelFailure);
                int cancelAfter = items.GetAllStacks()
                    .Where(stack => stack != null
                        && string.Equals(
                            stack.ItemId,
                            medicineId,
                            StringComparison.Ordinal))
                    .Sum(stack => stack.Quantity);
                bool cancelDestinationCleared = !items.GetAllStacks().Any(
                    stack => stack != null
                        && string.Equals(
                            stack.DestinationId,
                            cancelOrder.materialDestinationId,
                            StringComparison.Ordinal));
                bool cancelClaimRevoked = !destinationClaims.CaptureClaims().Any(
                    claim => claim != null
                        && string.Equals(
                            claim.DestinationId,
                            cancelOrder.materialDestinationId,
                            StringComparison.Ordinal));
                Check(
                    cancelled
                        && cancelDestinationCleared
                        && cancelBefore == cancelAfter,
                    "SURGERY_CANCEL_RELEASE_CONSERVED",
                    $"cancelled={cancelled}; failure={cancelFailure.Code}:{string.Join(",", cancelFailure.Parameters.ToArray())}; quantity={cancelBefore}->{cancelAfter}; destinationCleared={cancelDestinationCleared}");
                Check(
                    cancelClaimRevoked,
                    "SURGERY_CANCEL_CLAIM_REVOKED",
                    $"destination={cancelOrder.materialDestinationId}; revoked={cancelClaimRevoked}");
            }

            CurrentStage = "packaged-anesthetic-surgery";
            yield return VerifyPackagedAnestheticSurgery(
                scope,
                commands,
                facilities,
                characters,
                table,
                order.subject.Clone(),
                supplyPosition,
                processWaterDestinationId);
        CurrentStage = "finish";
        Finish();
    }

    private IEnumerator VerifyPackagedAnestheticSurgery(
        DungeonRuntimeLifetimeScope scope,
        ISurgeryCommandService commands,
        ISurgicalFacilityQuery facilities,
        ICharacterWorldQuery characters,
        Facility table,
        SurgicalSubjectRef subject,
        Vector2Int supplyPosition,
        string processWaterDestinationId)
    {
        CurrentStage = "packaged-anesthetic-surgery-setup";
        Resolve<IRandomStreamProvider>(scope)
            ?.Get("medical:surgery-outcomes")
            .Restore(1UL);
        int anestheticBefore = CountPhysicalItem(AnestheticItemId);
        int vialBefore = CountPhysicalItem(MedicalVialItemId);
        anatomy.TryDamageNode(
            patient,
            targetNodeId,
            2f,
            0f,
            "포장 마취약 반환 검증");
        bool scheduled = commands.TrySchedule(
            subject,
            ForeignBodyProcedureId,
            targetNodeId,
            string.Empty,
            doctor.Identity?.PersistentId,
            facilities.GetFacilityId(table),
            out SurgeryOrder packagedOrder,
            out DomainFailure scheduleFailure);
        Check(
            scheduled && packagedOrder != null,
            "SURGERY_PACKAGED_ANESTHETIC_ORDER_CREATED",
            scheduled
                ? $"order={packagedOrder.orderId}; destination={packagedOrder.materialDestinationId}"
                : $"failure={scheduleFailure.Code}:{string.Join(",", scheduleFailure.Parameters.ToArray())}");
        if (!scheduled || packagedOrder == null)
        {
            yield break;
        }

        int requiredAnesthetic = packagedOrder.materials
            .Where(requirement => requirement != null
                && !requirement.optional
                && string.Equals(
                    requirement.itemId,
                    AnestheticItemId,
                    StringComparison.Ordinal))
            .Sum(requirement => Mathf.Max(1, requirement.quantity));
        int requiredDisinfectant = packagedOrder.materials
            .Where(requirement => requirement != null
                && !requirement.optional
                && string.Equals(
                    requirement.itemId,
                    DisinfectantItemId,
                    StringComparison.Ordinal))
            .Sum(requirement => Mathf.Max(1, requirement.quantity));
        bool exactAuthoredInputs = requiredAnesthetic == 2
            && requiredDisinfectant == 2;
        Check(
            exactAuthoredInputs,
            "SURGERY_PACKAGED_ANESTHETIC_AUTHORED_REQUIREMENT",
            string.Join(",", packagedOrder.materials.Select(requirement =>
                $"{requirement?.itemId}:{requirement?.quantity}:{requirement?.optional}")));

        bool anestheticSpawned = items.SpawnItemAt(
            AnestheticItemId,
            requiredAnesthetic,
            supplyPosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int spawnedAnesthetic);
        bool disinfectantSpawned = items.SpawnItemAt(
            DisinfectantItemId,
            requiredDisinfectant,
            supplyPosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int spawnedDisinfectant);
        bool waterSpawned = items.SpawnItemAt(
            CleanWaterItemId,
            1,
            table.centerPos,
            WorldItemStackState.FacilityBuffer,
            processWaterDestinationId,
            out int spawnedWater);
        Check(
            exactAuthoredInputs
                && anestheticSpawned
                && spawnedAnesthetic == requiredAnesthetic
                && disinfectantSpawned
                && spawnedDisinfectant == requiredDisinfectant
                && waterSpawned
                && spawnedWater == 1,
            "SURGERY_PACKAGED_ANESTHETIC_INPUTS_READY",
            $"anesthetic={spawnedAnesthetic}/{requiredAnesthetic}; disinfectant={spawnedDisinfectant}/{requiredDisinfectant}; water={spawnedWater}");

        CurrentStage = "packaged-anesthetic-surgery-execution";
        float deadline = Time.realtimeSinceStartup + OverallTimeoutSeconds;
        string packagedOrderId = packagedOrder.orderId;
        while ((packagedOrder = RequireCurrentOrder(packagedOrderId)).IsActive
               && Time.realtimeSinceStartup < deadline)
        {
            if (packagedOrder.risk != null)
            {
                SetOrderSuccessChanceForFixture(packagedOrderId, 1f);
                packagedOrder = RequireCurrentOrder(packagedOrderId);
            }
            foreach (CharacterActor actor in characters.Characters
                         .Where(candidate => candidate != null
                             && !candidate.IsDead
                             && candidate.characterType == CharacterType.NPC))
            {
                StabilizeVerificationActor(actor);
            }
            doctor?.Brain?.PreferWorkActionOnNextDecision(
                BuiltInWorkTypeIds.Surgery,
                120f);
            if (Time.timeScale <= 0f)
            {
                Time.timeScale = VerificationTimeScale;
            }
            yield return null;
        }

        Check(
            !packagedOrder.IsActive
                && packagedOrder.state == SurgeryOrderState.Completed
                && packagedOrder.materialsConsumed
                && packagedOrder.anesthesiaConsumed,
            "SURGERY_PACKAGED_ANESTHETIC_CONSUMED_LIVE",
            $"state={packagedOrder.state}; status={packagedOrder.statusData?.code}; "
            + $"successChance={packagedOrder.risk?.successChance:0.###}; "
            + $"resultRolled={packagedOrder.resultRolled}; "
            + $"materials={packagedOrder.materialsConsumed}; anesthesia={packagedOrder.anesthesiaConsumed}");

        int anestheticAfter = CountPhysicalItem(AnestheticItemId);
        int vialAfter = CountPhysicalItem(MedicalVialItemId);
        WorldItemStackSnapshot[] committedVials = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    MedicalVialItemId,
                    StringComparison.Ordinal)
                && stack.State == WorldItemStackState.Loose
                && stack.Position == table.centerPos
                && (stack.Components ?? Array.Empty<ItemInstanceComponentSaveData>())
                    .Any(component => component != null
                        && string.Equals(
                            component.componentTypeId,
                            ItemInstanceComponentIds.ProductionOutputCommit,
                            StringComparison.Ordinal)))
            .ToArray();
        Check(
            anestheticAfter == anestheticBefore
                && vialAfter == vialBefore + requiredAnesthetic
                && committedVials.Length == 1
                && committedVials[0].Quantity == requiredAnesthetic,
            "SURGERY_PACKAGED_ANESTHETIC_VIAL_RETURNED_EXACT_ONCE",
            $"anesthetic={anestheticBefore}+{requiredAnesthetic}->{anestheticAfter}; vial={vialBefore}->{vialAfter}; committedStacks={committedVials.Length}");

        IPhysicalItemBatchDispositionService dispositions =
            Resolve<IPhysicalItemBatchDispositionService>(scope);
        bool pendingCleared = dispositions != null
            && !dispositions.TryGetPending(
                "surgery-material-sink:" + packagedOrder.orderId,
                out _);
        Check(
            pendingCleared,
            "SURGERY_PACKAGED_ANESTHETIC_SINK_ACKNOWLEDGED",
            $"order={packagedOrder.orderId}; pending={!pendingCleared}");

        DungeonGameSaveData packagedSave = gameSave.Capture();
        bool restored = gameSave.TryRestore(
            packagedSave,
            out DungeonGameRestoreReport restoreReport);
        int vialAfterRestore = CountPhysicalItem(MedicalVialItemId);
        Check(
            restored && vialAfterRestore == vialAfter,
            "SURGERY_PACKAGED_ANESTHETIC_RESTORE_NO_DUPLICATE",
            restored
                ? $"vial={vialAfter}->{vialAfterRestore}"
                : string.Join(" | ", restoreReport?.Errors ?? Array.Empty<string>()));
        if (restored)
        {
            CurrentStage = "returned-vial-warehouse-and-production-reuse";
            yield return VerifyReturnedVialWarehouseAndProductionReuse(
                scope,
                requiredAnesthetic);
        }
    }

    private SurgeryOrder RequireCurrentOrder(string orderId)
    {
        if (surgery == null || !surgery.TryGetOrder(orderId, out SurgeryOrder order))
            throw new InvalidOperationException("Surgery fixture lost its current order: " + orderId);
        return order;
    }

    private void SetOrderSuccessChanceForFixture(string orderId, float successChance)
    {
        if (surgery is not SurgeryRuntime runtime)
            throw new InvalidOperationException("Surgery fixture requires the production order owner.");
        var field = typeof(SurgeryRuntime).GetField("stateStore",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Surgery fixture could not locate the owned state store.");
        var store = field.GetValue(runtime) as SurgeryAggregateStateStore
            ?? throw new InvalidOperationException("Surgery fixture has no owned state store.");
        SurgeryOrder owned = store.State.Orders.SingleOrDefault(value => value != null
            && string.Equals(value.orderId, orderId, StringComparison.Ordinal));
        if (owned?.risk == null)
            throw new InvalidOperationException("Surgery fixture lost its owned risk: " + orderId);
        owned.risk.successChance = successChance;
    }

    private IEnumerator VerifyReturnedVialWarehouseAndProductionReuse(
        DungeonRuntimeLifetimeScope scope,
        int returnedVialQuantity)
    {
        CurrentStage = "returned-vial-fixture";
        ICharacterAiWorldRegistry world =
            Resolve<ICharacterAiWorldRegistry>(scope);
        ICharacterWorldQuery characters = Resolve<ICharacterWorldQuery>(scope);
        IProductionBillQuery productionQuery =
            Resolve<IProductionBillQuery>(scope);
        IProductionBillOrderCommand productionOrders =
            Resolve<IProductionBillOrderCommand>(scope);
        IProductionBillWorkExecution productionWork =
            Resolve<IProductionBillWorkExecution>(scope);
        IBlueprintResearchStateService research =
            Resolve<IBlueprintResearchStateService>(scope);
        IGridBuildingObjectFactory buildingFactory =
            Resolve<IGridBuildingObjectFactory>(scope);
        Grid grid = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>()
            ?.grid;
        Check(
            world != null
                && characters != null
                && productionQuery != null
                && productionOrders != null
                && productionWork != null
                && research != null
                && buildingFactory != null
                && grid != null,
            "ANESTHETIC_RECYCLE_RUNTIME_READY",
            $"world={world != null}; characters={characters != null}; "
            + $"query={productionQuery != null}; orders={productionOrders != null}; "
            + $"work={productionWork != null}; research={research != null}; "
            + $"factory={buildingFactory != null}; grid={grid != null}");
        if (world == null
            || characters == null
            || productionQuery == null
            || productionOrders == null
            || productionWork == null
            || research == null
            || buildingFactory == null
            || grid == null)
        {
            yield break;
        }

        IWarehouseFacility warehouse = world.Warehouses
            .Where(candidate => candidate?.Inventory != null
                && candidate.Inventory.Accepts(StockCategory.General)
                && candidate is BuildableObject)
            .OrderBy(candidate => candidate.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        CharacterActor hauler = characters.Characters
            .Where(candidate => candidate != null
                && !candidate.IsDead
                && candidate.characterType == CharacterType.NPC
                && candidate.TryGetAbility(out AbilityWork _))
            .OrderBy(candidate => candidate.Identity?.PersistentId,
                StringComparer.Ordinal)
            .FirstOrDefault();
        Check(
            warehouse is BuildableObject && hauler != null,
            "ANESTHETIC_RECYCLE_HAUL_FIXTURE_READY",
            $"warehouse={warehouse?.PersistentInstanceId.Value ?? "<none>"}; "
            + $"hauler={hauler?.Identity?.PersistentId ?? "<none>"}");
        if (warehouse is not BuildableObject warehouseBuilding || hauler == null)
        {
            yield break;
        }

        Dictionary<CharacterActor, bool> otherActorPauseStates = characters
            .Characters
            .Where(candidate => candidate != null
                && candidate != hauler
                && !candidate.IsDead)
            .ToDictionary(candidate => candidate, candidate => candidate.IsAiPaused());
        foreach (CharacterActor candidate in otherActorPauseStates.Keys)
        {
            candidate.SetAiPaused(true);
        }

        WorldItemStackSnapshot[] returnedStacks = items.GetAllStacks()
            .Where(IsReturnedMedicalVial)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        int returnedBeforeHaul = returnedStacks.Sum(stack => stack.Quantity);
        string returnedStackDetail = string.Join(
            ",",
            returnedStacks.Select(stack =>
                stack.StackId + ":" + stack.State + ":" + stack.Quantity));
        Check(
            returnedBeforeHaul == returnedVialQuantity
                && returnedStacks.All(stack =>
                    stack.State == WorldItemStackState.Loose),
            "ANESTHETIC_RECYCLE_RETURNED_VIAL_LOOSE_SOURCE",
            $"expected={returnedVialQuantity}; actual={returnedBeforeHaul}; "
            + "stacks=" + returnedStackDetail);
        if (returnedBeforeHaul != returnedVialQuantity)
        {
            yield break;
        }

        AbilityWork haulerWork = hauler.GetAbility<AbilityWork>();
        WorkPriorityLevel originalHaulPriority =
            haulerWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul);
        bool originalHaulerPause = hauler.IsAiPaused();
        hauler.SetAiPaused(true);
        haulerWork.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Haul,
            WorkPriorityLevel.Priority1);
        foreach (WorldItemStackSnapshot stack in returnedStacks)
        {
            items.PrioritizeHaul(stack.StackId);
        }

        CurrentStage = "returned-vial-ai-haul";
        float haulDeadline = Time.realtimeSinceStartup + 45f;
        int haulAttempts = 0;
        while (CountReturnedMedicalVials(WorldItemStackState.Stored)
                   < returnedVialQuantity
               && Time.realtimeSinceStartup < haulDeadline)
        {
            AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
            try
            {
                if (action.CanStart(hauler))
                {
                    haulAttempts++;
                    AbilityHaul ability = AbilityHaul.Ensure(hauler);
                    action.Execute(hauler);
                    while (ability != null
                           && ability.IsHauling
                           && CountReturnedMedicalVials(
                               WorldItemStackState.Stored)
                               < returnedVialQuantity
                           && Time.realtimeSinceStartup < haulDeadline)
                    {
                        Time.timeScale = 8f;
                        yield return null;
                    }
                }
                else
                {
                    yield return null;
                }
            }
            finally
            {
                Destroy(action);
            }
        }

        int returnedStored = CountReturnedMedicalVials(
            WorldItemStackState.Stored);
        int returnedAll = items.GetAllStacks()
            .Where(IsReturnedMedicalVial)
            .Sum(stack => stack.Quantity);
        HashSet<string> liveWarehouseDestinations = world.Warehouses
            .Where(candidate => candidate?.Inventory != null)
            .Select(WarehouseStorageIdentity.RequireDestinationId)
            .ToHashSet(StringComparer.Ordinal);
        WorldItemStackSnapshot[] storedReturnedStacks = items.GetAllStacks()
            .Where(IsReturnedMedicalVial)
            .ToArray();
        bool storedAtWarehouse = storedReturnedStacks.All(stack =>
            stack.State == WorldItemStackState.Stored
                && liveWarehouseDestinations.Contains(stack.DestinationId));
        string returnedWarehouseDestination = storedReturnedStacks
            .Select(stack => stack.DestinationId)
            .Distinct(StringComparer.Ordinal)
            .SingleOrDefault();
        IWarehouseFacility returnedWarehouse = world.Warehouses
            .FirstOrDefault(candidate => candidate?.Inventory != null
                && string.Equals(
                    WarehouseStorageIdentity.RequireDestinationId(candidate),
                    returnedWarehouseDestination,
                    StringComparison.Ordinal));
        Check(
            returnedStored == returnedVialQuantity
                && returnedAll == returnedVialQuantity
                && storedAtWarehouse
                && returnedWarehouse is BuildableObject,
            "ANESTHETIC_RECYCLE_VIAL_AI_WAREHOUSE_INTAKE",
            $"stored={returnedStored}; total={returnedAll}; "
            + $"attempts={haulAttempts}; destination={returnedWarehouseDestination}");
        if (returnedStored != returnedVialQuantity
            || !storedAtWarehouse
            || returnedWarehouse is not BuildableObject returnedWarehouseBuilding)
        {
            haulerWork.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Haul,
                originalHaulPriority);
            hauler.SetAiPaused(originalHaulerPause);
            yield break;
        }
        warehouse = returnedWarehouse;
        warehouseBuilding = returnedWarehouseBuilding;

        BuildingSO apothecaryAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/P18_약제대.asset");
        BuildableObject apothecary = TryPlaceProductionFacility(
            scope,
            buildingFactory,
            grid,
            apothecaryAsset,
            out string placementFailure);
        Check(
            apothecary != null
                && apothecary.MatchesProductionWorkstation(
                    AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                        "Assets/Resources/SO/Economy/Recipes/recipe_anesthetic.asset")),
            "ANESTHETIC_RECYCLE_APOTHECARY_READY",
            apothecary != null
                ? $"facility={apothecary.RequirePersistentInstanceId().Value}; "
                    + $"position={apothecary.centerPos}"
                : placementFailure);
        if (apothecary == null)
        {
            haulerWork.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Haul,
                originalHaulPriority);
            hauler.SetAiPaused(originalHaulerPause);
            yield break;
        }

        research.GetState().Projects.RestoreCompleted(
            new ResearchProjectId(AnesthesiaResearchId));
        int anestheticBeforeProduction = CountPhysicalItem(AnestheticItemId);
        int vialBeforeProduction = CountPhysicalItem(MedicalVialItemId);
        string warehouseDestination =
            WarehouseStorageIdentity.RequireDestinationId(warehouse);
        bool dreamleafSeeded = items.SpawnItemAt(
            DreamleafItemId,
            2,
            warehouseBuilding.centerPos,
            WorldItemStackState.Stored,
            warehouseDestination,
            out int dreamleafSpawned);
        bool alcoholSeeded = items.SpawnItemAt(
            AlcoholItemId,
            1,
            warehouseBuilding.centerPos,
            WorldItemStackState.Stored,
            warehouseDestination,
            out int alcoholSpawned);
        Check(
            dreamleafSeeded && dreamleafSpawned == 2
                && alcoholSeeded && alcoholSpawned == 1,
            "ANESTHETIC_RECYCLE_INPUT_STOCK_READY",
            $"dreamleaf={dreamleafSpawned}; alcohol={alcoholSpawned}; "
            + $"vial={vialBeforeProduction}");

        ProductionBillCommandResult added = productionOrders.AddBill(
            apothecary,
            "recipe:anesthetic",
            ProductionOrderMode.RepeatCount,
            1);
        ProductionBillSnapshot bill = added.Succeeded
            ? productionQuery.GetBills(apothecary)
                .FirstOrDefault(candidate => candidate.BillId == added.BillId)
            : null;
        Check(
            added.Succeeded
                && bill != null
                && !string.IsNullOrWhiteSpace(bill.MaterialDestinationId),
            "ANESTHETIC_RECYCLE_PRODUCTION_BILL_CREATED",
            added.Succeeded
                ? $"bill={added.BillId.Value}; destination={bill?.MaterialDestinationId}"
                : added.Failure.ToString());
        if (!added.Succeeded || bill == null)
        {
            haulerWork.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Haul,
                originalHaulPriority);
            hauler.SetAiPaused(originalHaulerPause);
            yield break;
        }
        string expectedOutputDestinationId = bill.OutputDestinationId;

        CurrentStage = "anesthetic-input-ai-delivery";
        float materialDeadline = Time.realtimeSinceStartup + 60f;
        int materialHaulAttempts = 0;
        while (!productionWork.CheckWorkAvailability(
                   apothecary,
                   BuiltInWorkTypeIds.Craft).Available
               && Time.realtimeSinceStartup < materialDeadline)
        {
            foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                         .Where(stack => stack != null
                             && string.Equals(
                                 stack.DestinationId,
                                 bill.MaterialDestinationId,
                                 StringComparison.Ordinal)))
            {
                items.PrioritizeHaul(stack.StackId);
            }

            AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
            try
            {
                if (action.CanStart(hauler))
                {
                    materialHaulAttempts++;
                    AbilityHaul ability = AbilityHaul.Ensure(hauler);
                    action.Execute(hauler);
                    while (ability != null
                           && ability.IsHauling
                           && Time.realtimeSinceStartup < materialDeadline)
                    {
                        Time.timeScale = 8f;
                        yield return null;
                    }
                }
                else
                {
                    yield return null;
                }
            }
            finally
            {
                Destroy(action);
            }
        }

        ProductionWorkAvailabilityResult availability =
            productionWork.CheckWorkAvailability(
                apothecary,
                BuiltInWorkTypeIds.Craft);
        string inputStackDetail = DescribePhysicalItems(
            DreamleafItemId,
            AlcoholItemId,
            MedicalVialItemId);
        Check(
            availability.Available
                && availability.Bill?.BillId == bill.BillId,
            "ANESTHETIC_RECYCLE_INPUTS_AI_DELIVERED",
            $"available={availability.Available}; "
            + $"failure={availability.Failure}; attempts={materialHaulAttempts}; "
            + $"haul={AbilityHaul.Ensure(hauler)?.CurrentUnloadReason}:"
            + $"{AbilityHaul.Ensure(hauler)?.LastFailureReason}:"
            + $"{AbilityHaul.Ensure(hauler)?.LastTerminalDiagnostics}; "
            + $"buffer={DescribeDestinationStacks(bill.MaterialDestinationId)}; "
            + $"allInputs={inputStackDetail}");
        if (!availability.Available)
        {
            haulerWork.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Haul,
                originalHaulPriority);
            hauler.SetAiPaused(originalHaulerPause);
            yield break;
        }

        CurrentStage = "anesthetic-production-execution";
        ProductionWorkBeginResult began = productionWork.BeginWork(
            hauler,
            apothecary,
            BuiltInWorkTypeIds.Craft);
        ProductionWorkExecutionResult execution = default;
        int executionSteps = 0;
        while (began.Succeeded
               && !execution.CycleCompleted
               && executionSteps++ < 16)
        {
            execution = productionWork.ExecuteWork(
                hauler,
                apothecary,
                began.Bill.BillId,
                Mathf.Max(1f, began.Bill.RequiredWork));
            if (!execution.Succeeded)
            {
                break;
            }
            if (!execution.CycleCompleted)
            {
                yield return null;
            }
        }

        int anestheticAfterProduction = CountPhysicalItem(AnestheticItemId);
        int vialAfterProduction = CountPhysicalItem(MedicalVialItemId);
        ProductionBillSnapshot completedBill = productionQuery
            .GetBills(apothecary)
            .FirstOrDefault(candidate => candidate.BillId == bill.BillId);
        WorldItemStackSnapshot[] outputStacks = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    AnestheticItemId,
                    StringComparison.Ordinal)
                && stack.State == WorldItemStackState.FacilityOutputBuffer
                && stack.Position == apothecary.centerPos
                && string.Equals(
                    stack.DestinationId,
                    expectedOutputDestinationId,
                    StringComparison.Ordinal))
            .ToArray();
        string outputStackDetail = string.Join(
            ",",
            outputStacks.Select(stack =>
                stack.StackId + ":" + stack.State + ":" + stack.Quantity));
        Check(
            began.Succeeded
                && execution.Succeeded
                && execution.CycleCompleted
                && anestheticAfterProduction == anestheticBeforeProduction + 1
                && vialAfterProduction == vialBeforeProduction - 1
                && outputStacks.Sum(stack => stack.Quantity) == 1
                && !outputStacks.Any(stack =>
                    stack.State == WorldItemStackState.Stored),
            "ANESTHETIC_RECYCLE_PRODUCTION_LIVE_EXACT_ONCE",
            $"begin={began.Succeeded}:{began.Failure}; "
            + $"execute={execution.Succeeded}:{execution.Outcome}:{execution.Failure}; "
            + $"anesthetic={anestheticBeforeProduction}->{anestheticAfterProduction}; "
            + $"vial={vialBeforeProduction}->{vialAfterProduction}; "
            + $"outputDestination={expectedOutputDestinationId}; "
            + $"completedBillPresent={completedBill != null}; "
            + "output=" + outputStackDetail);

        CurrentStage = "anesthetic-production-save-restore";
        DungeonGameSaveData recycleSave = gameSave.Capture();
        if (DungeonSaveSectionPayload.TryRead(
                recycleSave,
                CharacterConsumablesSaveSection.Id,
                out DungeonCharacterConsumablesSaveData consumablesPayload))
        {
            ICharacterConsumablesWorldPort consumablesWorld =
                Resolve<ICharacterConsumablesWorldPort>(scope);
            report.Add(
                "[INFO] ANESTHETIC_RECYCLE_CONSUMABLE_DELIVERIES "
                + string.Join(
                    ",",
                    (consumablesPayload.pendingMealDeliveries
                        ?? new List<CharacterMealDeliveryState>())
                    .Select(delivery =>
                        delivery.deliveryId + ":" + delivery.characterId
                        + ":characterLive="
                        + (consumablesWorld?.CharacterIds.Contains(
                            delivery.CharacterId) == true)
                        + ":" + delivery.buildingInstanceId
                        + ":facilityLive="
                        + (consumablesWorld?.FacilityIds.Contains(
                            delivery.BuildingInstanceId) == true))));
        }
        bool recycleRestored = gameSave.TryRestore(
            recycleSave,
            out DungeonGameRestoreReport recycleRestoreReport);
        Check(
            recycleRestored
                && CountPhysicalItem(AnestheticItemId)
                    == anestheticAfterProduction
                && CountPhysicalItem(MedicalVialItemId)
                    == vialAfterProduction,
            "ANESTHETIC_RECYCLE_PRODUCTION_RESTORE_NO_DUPLICATE",
            recycleRestored
                ? $"anesthetic={CountPhysicalItem(AnestheticItemId)}; "
                    + $"vial={CountPhysicalItem(MedicalVialItemId)}"
                : string.Join(
                    " | ",
                    recycleRestoreReport?.Errors ?? Array.Empty<string>()));

        haulerWork.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Haul,
            originalHaulPriority);
        hauler.SetAiPaused(originalHaulerPause);
        foreach (KeyValuePair<CharacterActor, bool> pair in otherActorPauseStates)
        {
            if (pair.Key != null)
            {
                pair.Key.SetAiPaused(pair.Value);
            }
        }
    }

    private string DescribePhysicalItems(params string[] itemIds)
    {
        HashSet<string> included = (itemIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        return string.Join(
            ",",
            items.GetAllStacks()
                .Where(stack => stack != null && included.Contains(stack.ItemId))
                .OrderBy(stack => stack.ItemId, StringComparer.Ordinal)
                .ThenBy(stack => stack.State)
                .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
                .Select(stack =>
                    stack.ItemId + ":" + stack.StackId + ":" + stack.State
                    + ":" + stack.Quantity + ":dest=" + stack.DestinationId
                    + ":reserved=" + stack.ReservedQuantity));
    }

    private IEnumerator EnsurePlayableRun()
    {
        OwnerRunManager ownerManager =
            UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
        {
            string fastCommit =
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            report.Add("[INFO] FAST_PARTY_COMMIT " + fastCommit);
            // Fast commit deliberately pauses the game while replacing the
            // prepared party. Restore the verifier speed before yielding so a
            // background MCP-driven PlayMode run cannot strand this coroutine
            // at timeScale zero.
            Time.timeScale = 8f;
            for (int i = 0; i < 8; i++)
            {
                yield return null;
            }
        }

        ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "RUN_READY",
            ownerManager?.CurrentOwnerActor != null
                ? $"owner={ownerManager.CurrentOwnerActor.name}"
                : "owner missing");
    }

    private static bool IsReturnedMedicalVial(WorldItemStackSnapshot stack)
    {
        return stack != null
            && string.Equals(
                stack.ItemId,
                MedicalVialItemId,
                StringComparison.Ordinal)
            && (stack.Components ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Any(component => component != null
                    && string.Equals(
                        component.componentTypeId,
                        ItemInstanceComponentIds.ProductionOutputCommit,
                        StringComparison.Ordinal));
    }

    private int CountReturnedMedicalVials(WorldItemStackState state)
    {
        return items.GetAllStacks()
            .Where(stack => IsReturnedMedicalVial(stack)
                && stack.State == state)
            .Sum(stack => stack.Quantity);
    }

    private string DescribeDestinationStacks(string destinationId)
    {
        return string.Join(
            ",",
            items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                .Select(stack =>
                    $"{stack.StackId}:{stack.ItemId}:{stack.State}:"
                    + $"{stack.Quantity}:reserved={stack.ReservedQuantity}"));
    }

    private BuildableObject TryPlaceProductionFacility(
        DungeonRuntimeLifetimeScope scope,
        IGridBuildingObjectFactory buildingFactory,
        Grid grid,
        BuildingSO definition,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (scope?.Container == null
            || buildingFactory == null
            || grid == null
            || definition == null)
        {
            failureReason = "production placement authority missing";
            return null;
        }

        Vector2Int position = default;
        bool found = false;
        for (int y = 0; y < grid.height && !found; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int candidate = new(x, y);
                bool available = definition.GetGridPosList(candidate)
                    .All(cellPosition =>
                    {
                        GridCell cell = grid.GetGridCell(cellPosition);
                        return cell != null
                            && cell.AreaType
                                != GridCellAreaType.BlockedExterior
                            && cell.CanOccupy(definition.layer);
                    });
                if (!available)
                {
                    continue;
                }

                position = candidate;
                found = true;
                break;
            }
        }

        if (!found)
        {
            failureReason = $"no free grid position for {definition.objectName}";
            return null;
        }

        BuildableObject building = buildingFactory.Create(
            grid,
            definition,
            position);
        if (building == null)
        {
            failureReason = $"factory failed for {definition.objectName}";
            return null;
        }

        foreach (MonoBehaviour component in
                 building.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
        building.SetGrid(grid);
        building.Initialization(definition, position);
        bool registered = grid.RegisterOccupant(
            building,
            definition.layer,
            definition.GetGridPosList(position),
            definition.Placement.IsMovement);
        if (!registered)
        {
            Destroy(building.gameObject);
            failureReason = $"grid registration failed at {position}";
            return null;
        }

        temporaryObjects.Add(building.gameObject);
        return building;
    }

    private static bool TryFindPlacement(
        RoomLayout layout,
        Grid grid,
        BuildingSO asset,
        Vector2Int patientPosition,
        out Vector2Int tablePosition,
        out Vector2Int supplyPosition)
    {
        tablePosition = default;
        supplyPosition = default;
        if (layout == null || grid == null || asset == null)
        {
            return false;
        }

        foreach (RoomInstance room in layout.Rooms
                     .Where(candidate => candidate != null && candidate.IsUsable)
                     .OrderBy(candidate => candidate.Cells.Min(position =>
                         Mathf.Abs(position.x - patientPosition.x)
                         + Mathf.Abs(position.y - patientPosition.y)))
                     .ThenByDescending(candidate => candidate.Cells.Count))
        {
            HashSet<Vector2Int> roomCells = new(room.Cells);
            foreach (Vector2Int cell in room.Cells
                         .Where(position => Mathf.Abs(
                                 position.x - patientPosition.x)
                             + Mathf.Abs(position.y - patientPosition.y) >= 4)
                         .OrderBy(position => Mathf.Abs(
                                 position.x - patientPosition.x)
                             + Mathf.Abs(position.y - patientPosition.y))
                         .ThenBy(position => position.x)
                         .ThenBy(position => position.y))
            {
                IReadOnlyList<Vector2Int> footprint =
                    asset.GetGridPosList(cell);
                if (footprint.Any(position =>
                        !roomCells.Contains(position)
                        || grid.GetGridCell(position) == null
                        || grid.GetGridCell(position)
                            .HasOccupantInLayer(asset.Placement.Layer)))
                {
                    continue;
                }

                Vector2Int supply = room.Cells
                    .Where(position => !footprint.Contains(position))
                    .OrderByDescending(position =>
                        Mathf.Abs(position.x - cell.x)
                        + Mathf.Abs(position.y - cell.y))
                    .FirstOrDefault();
                if (footprint.Contains(supply))
                {
                    continue;
                }

                tablePosition = cell;
                supplyPosition = supply;
                return true;
            }
        }

        return false;
    }

    private Facility CreateInjectedFacility(
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        BuildingSO asset,
        Vector2Int position,
        string objectName)
    {
        GameObject obj = new(objectName);
        temporaryObjects.Add(obj);
        Facility facility = obj.AddComponent<Facility>();
        InjectGameObject(scope, obj);
        facility.SetGrid(grid);
        facility.Initialization(asset, position);
        Vector3 world = grid.GetWorldPos(position);
        if (asset.Placement.HasEvenWidth)
        {
            world.x += 0.5f;
        }

        obj.transform.position = new Vector3(world.x, world.y, obj.transform.position.z);
        return facility;
    }

    private static Button FindSelectorButton(
        GameObject window,
        string rowName,
        string buttonName)
    {
        Transform row = window.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate != null && candidate.name == rowName);
        return row != null
            ? row.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button != null && button.name == buttonName)
            : null;
    }

    private static void SendPointerClick(Button button)
    {
        PointerEventData pointer = new(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(
                null,
                button.transform.position)
        };
        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerClickHandler);
    }

    private static float GetNodeHealth(
        AnatomyHealthSnapshot snapshot,
        string nodeId)
    {
        return snapshot.Nodes?
            .FirstOrDefault(node => node != null
                && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal))
            ?.currentHealth ?? -1f;
    }

    private int CountPhysicalItem(string itemId)
    {
        return items?.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    itemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity) ?? 0;
    }

    private void StabilizeVerificationActor(CharacterActor actor)
    {
        if (actor?.stats == null)
        {
            return;
        }

        foreach (CharacterCondition condition in
                 Enum.GetValues(typeof(CharacterCondition)))
        {
            actor.stats[condition] = 100f;
        }

        deprivation?.DebugClearBreakdown(actor);
    }

    private string DescribeHaulDiagnostics(
        ICharacterWorldQuery characters,
        IWorldItemHaulPlanningService haulPlanning,
        SurgeryOrder order,
        Grid grid)
    {
        string actors = string.Join(
            " || ",
            characters.Characters
                .Where(actor => actor != null && !actor.IsDead)
                .Select(actor =>
                {
                    AbilityWork work = actor.GetAbility<AbilityWork>();
                    WorldItemHaulPlan plan = null;
                    string reason = "운반 계획 서비스를 찾을 수 없음";
                    bool preview = haulPlanning != null
                        && haulPlanning.TryPreviewBestPlan(
                            actor,
                            out plan,
                            out reason);
                    string action = actor.Brain?.bestAction?.actionset != null
                        ? actor.Brain.bestAction.actionset.GetType().Name
                        : "none";
                    AbilityMove move = actor.GetAbility<AbilityMove>();
                    AbilityHaul activeHaul = actor.GetComponent<AbilityHaul>();
                    CharacterCarryInventory carry = actor.CarryInventory;
                    string planDetail = DescribeHaulPlan(actor, plan, preview, grid);
                    CharacterAiRuntimeDiagnosticsSnapshot diagnostics =
                        actor.Brain?.CaptureRuntimeDiagnostics() ?? default;
                    string carried = carry == null
                        ? "none"
                        : string.Join(
                            ",",
                            carry.Items
                                .Where(item => item != null && item.quantity > 0)
                                .Select(item => $"{item.itemId}x{item.quantity}"));
                    return $"{actor.Identity?.DisplayName}"
                        + $":pos={actor.GetNowXY()}"
                        + $":world={actor.transform.position}"
                        + $":canAi={actor.CanRunAi}"
                        + $":paused={actor.IsAiPaused()}"
                        + $":duty={work?.CurrentDutyState}"
                        + $":haul={work?.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul)}"
                        + $":assigned={work?.AssignedWorkTypeId.Value}"
                        + $":action={action}"
                        + $":phase={actor.Brain?.CurrentActionPhase}"
                        + $":moveBlocked={move?.LastGridMoveWasBlocked}"
                        + $":moveFailure={move?.LastGridMoveFailureReason}"
                        + $":haulStage={activeHaul?.CurrentExecutionStage}"
                        + $":haulBeat={activeHaul?.RoutineHeartbeat}"
                        + $":haulPath={activeHaul?.ActivePathDebug}"
                        + $":moveSpeed={actor.GetMoveSpeed():0.###}"
                        + $":carried={carried}"
                        + $":preview={preview}"
                        + $":plan={(preview ? plan?.Summary : reason)}"
                        + $":route={planDetail}"
                        + $":diag={diagnostics.FormatDeltaFrom(default)}"
                        + $":trace={diagnostics.FormatRecentTrace()}";
                }));
        string stacks = string.Join(
            " || ",
            items.GetAllStacks()
                .Where(stack => stack != null
                    && (string.Equals(
                            stack.DestinationId,
                            order.materialDestinationId,
                            StringComparison.Ordinal)
                        || stack.DestinationId?.Contains(
                            "process-water",
                            StringComparison.Ordinal) == true))
                .Select(stack =>
                    $"{stack.StackId}:{stack.ItemId}x{stack.Quantity}"
                    + $":state={stack.State}:pos={stack.Position}"
                    + $":destPos={stack.DestinationPosition}"
                    + $":reserved={stack.ReservedQuantity}:available={stack.AvailableQuantity}"
                    + $":source={stack.SourceStorageDestinationId}"));
        return $"timeScale={Time.timeScale:0.##}; delta={Time.deltaTime:0.####}; "
            + $"actors=[{actors}]; stacks=[{stacks}]";
    }

    private static string DescribeSurgeryWorkDiagnostics(
        BuildableObject table,
        CharacterActor doctor,
        AbilityWork doctorWork,
        SurgeryOrder order,
        ISurgeryQuery surgery,
        ICharacterAiWorldRegistry worldRegistry,
        IFacilityCandidateCache facilityCandidates,
        Grid grid)
    {
        bool published = table != null
            && worldRegistry?.Buildings.Contains(table) == true;
        IReadOnlyList<BuildableObject> indexed = facilityCandidates?
            .GetWorkCandidates(grid, FacilityWorkType.Surgery)
            ?? Array.Empty<BuildableObject>();
        bool runtimeHasWork = table != null
            && surgery?.TryGetWorkFor(table, out _) == true;
        FacilityAssignmentStatus assignment = table is IWorkableFacility workable
            ? workable.GetWorkerAssignmentStatus(doctor?.BuildingVisitor)
            : FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Unknown,
                "missing table");
        bool canStart = doctorWork?.CanStartWorkAction(
            BuiltInWorkTypeIds.Surgery,
            null) == true;
        WorkTargetCandidate candidate = default;
        bool found = doctorWork != null
            && doctorWork.TryGetBestWorkCandidate(
                BuiltInWorkTypeIds.Surgery,
                null,
                out candidate);
        WorkTargetCandidate rejected = doctorWork?.LastRejectedWorkCandidate
            ?? default;
        return $"published={published}; worldBuildings={worldRegistry?.Buildings.Count ?? -1}; "
            + $"indexed={indexed.Count}; tableIndexed={indexed.Contains(table)}; "
            + $"runtimeHasWork={runtimeHasWork}; canStart={canStart}; candidate={found}; "
            + $"candidateTarget={WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate)?.PersistentInstanceId.Value}; "
            + $"assignment={assignment.IsAllowed}:{assignment.FailureKind}:{assignment.Reason}; "
            + $"rejected={rejected.FailureKind}:{rejected.FailureReason}; "
            + $"preferredDoctor={order?.preferredDoctorId}; assignedDoctor={order?.doctorId}; "
            + $"doctor={doctor?.Identity?.PersistentId}; priority={doctorWork?.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Surgery)}";
    }

    private static string DescribeHaulPlan(
        CharacterActor actor,
        WorldItemHaulPlan plan,
        bool preview,
        Grid grid)
    {
        if (!preview || plan == null || !plan.IsValid || plan.PickupLegs.Count == 0)
        {
            return "none";
        }

        WorldItemHaulPlanLeg pickup = plan.PickupLegs[0];
        WorldItemHaulPlanLeg delivery = plan.DeliveryLegs.Count > 0
            ? plan.DeliveryLegs[0]
            : pickup;
        GridPathRequestStatus status = GridPathRequestStatus.Unreachable;
        Queue<GridMoveStep> path = null;
        if (actor?.PathSearchBroker != null && grid != null)
        {
            status = actor.PathSearchBroker.RequestMovePathTo(
                grid,
                actor.GetNowXY(),
                pickup.PickupStandPosition,
                out path,
                GridPathSearchPriority.Urgent,
                GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor)));
        }

        return $"pickup={pickup.ItemPosition}"
            + $"/stand={pickup.PickupStandPosition}"
            + $"/delivery={delivery.DeliveryPosition}"
            + $"/drop={delivery.DropPosition}"
            + $"/path={status}:{path?.Count ?? -1}";
    }

    private IEnumerator CaptureScreen(string path)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture =
            PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        if (capture == null)
        {
            Check(false, "SURGERY_SCREEN_CAPTURE", "capture returned null");
            yield break;
        }

        byte[] bytes = capture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Check(bytes.Length > 1000, "SURGERY_SCREEN_CAPTURE_NONBLANK",
            $"{path}; bytes={bytes.Length}");
        Destroy(capture);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject("QA_Surgery_EventSystem", typeof(EventSystem));
        }
    }

    private static void InjectGameObject(
        DungeonRuntimeLifetimeScope scope,
        GameObject target)
    {
        foreach (MonoBehaviour component in
                 target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
    }

    private static T Resolve<T>(
        DungeonRuntimeLifetimeScope scope)
        where T : class
    {
        try
        {
            return scope?.Container?.Resolve<T>();
        }
        catch
        {
            return null;
        }
    }

    private void InjectPublicationFaultOnce(WorkCompletedIdentityEvent gameEvent)
    {
        if (publicationFaultCount != 0
            || !gameEvent.WorkId.StartsWith("surgery:", StringComparison.Ordinal))
            return;
        publicationFaultEvent = gameEvent;
        publicationFaultCount++;
        Time.timeScale = 0f;
        throw new InvalidOperationException(PublicationFaultMarker);
    }

    // QA-only transparent decorator: the real registered command commits first.
    // This cuts only its return/owner-ACK boundary, never fabricates a result.
    private sealed class CompletionAckFaultProbe : IWorkCompletionIdentityDeliveryCommand
    {
        private readonly SurgeryPlayModeVerificationRunner owner;
        private readonly IWorkCompletionIdentityDeliveryCommand inner;

        public CompletionAckFaultProbe(SurgeryPlayModeVerificationRunner owner,
            IWorkCompletionIdentityDeliveryCommand inner)
        {
            this.owner = owner;
            this.inner = inner;
        }

        public WorkCompletionIdentityDeliveryResult EnsureApplied(
            WorkCompletionIdentityDeliveryRequest request)
        {
            WorkCompletionIdentityDeliveryResult result = inner.EnsureApplied(request);
            if (result.IsApplied && owner.commandAckFaultCount == 0
                && request.WorkId.StartsWith("surgery:", StringComparison.Ordinal))
            {
                owner.publicationFaultEvent = request.ToEvent();
                owner.commandAckFaultCount++;
                Time.timeScale = 0f;
                throw new InvalidOperationException(CommandAckFaultMarker);
            }
            return result;
        }

        public bool RetireProducerStream(string producerStreamId) =>
            inner.RetireProducerStream(producerStreamId);
    }

    private IEnumerator VerifySurgeryPublicationRecovery(
        DungeonRuntimeLifetimeScope scope, SurgeryOrder order, IGameClock simulationClock)
    {
        CurrentStage = "w01-publication-pending-capture";
        Time.timeScale = 0f;
        WorkCompletedIdentityEvent terminal = publicationFaultEvent;
        string orderId = order.orderId;
        string patientId = order.subject.subjectId;
        GameplayResultKey childKey = SurgeryOutcomeIds.ResultKey(orderId);
        List<GameplayOutcomeSnapshot> before = W01ExactOutcomes(scope);
        GameplayOutcomeSnapshot[] children = before.Where(value =>
            W01ResultKey(value) == childKey.ToString()).ToArray();
        GameplayOutcomeSnapshot[] identities = W01IdentityOutcomes(before, terminal);
        WorkCompletionIdentityDeliveryLedger deliveries =
            Resolve<WorkCompletionIdentityDeliveryLedger>(scope);
        string cursorBefore = W01CursorFingerprint(deliveries);
        string identityBefore = W01IdentityFingerprint(identities);
        string childBefore = W01IdentityFingerprint(children);
        float workBefore = order.completedWork;
        int medicineBefore = CountPhysicalItem(StandardMedicineItemId);
        int waterBefore = CountPhysicalItem(CleanWaterItemId);
        bool pendingExact = commandAckFaultCount == 1 && publicationFaultCount == 0
            && order.resultRolled && order.resultSucceeded && order.resolutionFactAccepted
            && W01CompletionAcknowledged(scope, order) && !W01CompletionNotificationAttempted(scope, order)
            && !string.IsNullOrEmpty(order.completionPublicationKey)
            && order.completionDoctorId == terminal.Character.Value
            && order.completionAbsoluteDay == terminal.AbsoluteDay
            && !order.completionFactDelivered && order.completedWork >= order.requiredWork
            && children.Length == 1 && identities.Length == 1;
        Check(pendingExact, "W01_SURGERY_PUBLICATION_PENDING_EXACT",
            "commandFaults=" + commandAckFaultCount + "; state=" + order.state
                + "; committed=" + order.resolutionFactAccepted
                + "; published=" + order.completionFactDelivered
                + "; work=" + order.completedWork + "; doctor=" + order.doctorId
                + "; identities=" + identities.Length + "; children=" + children.Length
                + "; medicine=" + medicineBefore + "; water=" + waterBefore);
        if (!pendingExact)
            yield break;

        DungeonGameSaveData pending = gameSave.Capture();
        string pendingJson = gameSave.ToJson(pending, true);
        W01WriteWorldEvidence("w01-surgery-publication-pending-world.json", pendingJson);
        VerifyW01SurgeryStoredOwnerRejections(scope, pendingJson, orderId, patientId);
        CharacterActor previousDoctor = doctor;
        CharacterActor previousPatient = patient;
        bool restored = gameSave.TryRestore(gameSave.FromJson(pendingJson),
            out DungeonGameRestoreReport restoreReport);
        Check(restored && restoreReport.Success, "W01_SURGERY_PUBLICATION_PENDING_WORLD_RESTORE",
            restored ? "sections=" + pending.sections.Count : string.Join(" | ", restoreReport.Errors));
        if (!restored)
            yield break;
        ICharacterWorldQuery characters = Resolve<ICharacterWorldQuery>(scope);
        doctor = characters.Characters.Single(value => value != null
            && value.Identity?.PersistentId == terminal.Character.Value);
        patient = characters.Characters.Single(value => value != null
            && value.Identity?.PersistentId == patientId);
        doctorWork = doctor.GetAbility<AbilityWork>();
        surgery.TryGetOrder(orderId, out order);
        bool freshExact = order != null && !ReferenceEquals(previousDoctor, doctor)
            && !ReferenceEquals(previousPatient, patient)
            && order.resolutionFactAccepted && !order.completionFactDelivered
            && W01CompletionAcknowledged(scope, order) && !W01CompletionNotificationAttempted(scope, order)
            && !string.IsNullOrEmpty(order.completionPublicationKey)
            && order.completionDoctorId == terminal.Character.Value
            && order.completionAbsoluteDay == terminal.AbsoluteDay
            && string.IsNullOrEmpty(order.doctorId)
            && Mathf.Approximately(order.completedWork, workBefore)
            && W01CursorFingerprint(deliveries) == cursorBefore;
        Check(freshExact, "W01_SURGERY_PUBLICATION_PENDING_FRESH_OWNER",
            "freshActors=" + (!ReferenceEquals(previousDoctor, doctor) && !ReferenceEquals(previousPatient, patient))
                + "; state=" + order?.state + "; doctor=" + order?.doctorId
                + "; published=" + order?.completionFactDelivered);
        if (!freshExact)
            yield break;
        foreach (CharacterActor actor in characters.Characters.Where(value => value != null))
            actor.SetAiPaused(true);

        // Controlled calendar input, not evidence of a natural operating-day cycle.
        // The completion receipt must retain its original date during ACK retry.
        IGameCalendar recoveryCalendar = Resolve<IGameCalendar>(scope);
        recoveryCalendar.SetDateTime(terminal.AbsoluteDay + 1, recoveryCalendar.Hour);
        Check(recoveryCalendar.Day > terminal.AbsoluteDay,
            "W01_SURGERY_LATER_CALENDAR_INPUT", "receiptDay=" + terminal.AbsoluteDay
                + "; currentDay=" + recoveryCalendar.Day + "; controlled public calendar command");

        CurrentStage = "w01-command-ack-natural-tick";
        float ackWaitStarted = Time.realtimeSinceStartup;
        Time.timeScale = VerificationTimeScale;
        while (publicationFaultCount == 0
            && Time.realtimeSinceStartup - ackWaitStarted < OverallTimeoutSeconds)
            yield return null;
        Time.timeScale = 0f;
        surgery.TryGetOrder(orderId, out order);
        bool acknowledgedBeforeNotification = publicationFaultCount == 1
            && order != null && W01CompletionAcknowledged(scope, order)
            && W01CompletionNotificationAttempted(scope, order) && !order.completionFactDelivered;
        Check(acknowledgedBeforeNotification,
            "W01_SURGERY_COMMAND_ACK_RECOVERED_BEFORE_NOTIFICATION_FAULT",
            "ack=" + W01CompletionAcknowledged(scope, order)
                + "; attempted=" + W01CompletionNotificationAttempted(scope, order)
                + "; published=" + order?.completionFactDelivered
                + "; commandFaults=" + commandAckFaultCount
                + "; notificationFaults=" + publicationFaultCount);
        if (!acknowledgedBeforeNotification)
            yield break;
        string notificationJson = gameSave.ToJson(gameSave.Capture(), true);
        W01WriteWorldEvidence("w01-surgery-notification-failed-world.json", notificationJson);
        VerifyW01SurgeryStoredOwnerRejections(scope, notificationJson, orderId, patientId, acknowledged: true);
        bool notificationRestored = gameSave.TryRestore(gameSave.FromJson(notificationJson),
            out DungeonGameRestoreReport notificationRestoreReport);
        Check(notificationRestored && notificationRestoreReport.Success,
            "W01_SURGERY_NOTIFICATION_FAILURE_WORLD_RESTORE",
            notificationRestored ? "ack retained; notification failure not converted to success"
                : string.Join(" | ", notificationRestoreReport.Errors));
        if (!notificationRestored)
            yield break;
        doctor = characters.Characters.Single(value => value != null
            && value.Identity?.PersistentId == terminal.Character.Value);
        patient = characters.Characters.Single(value => value != null
            && value.Identity?.PersistentId == patientId);
        doctorWork = doctor.GetAbility<AbilityWork>();
        surgery.TryGetOrder(orderId, out order);
        foreach (CharacterActor actor in characters.Characters.Where(value => value != null))
            actor.SetAiPaused(true);

        CurrentStage = "w01-publication-natural-tick-recovery";
        float startedSimulation = simulationClock.Time;
        float startedRealtime = Time.realtimeSinceStartup;
        int startedFrame = Time.frameCount;
        float observationUntil = Mathf.Max(startedSimulation + 5f, order.recoveryUntil + 2f);
        Time.timeScale = VerificationTimeScale;
        while ((simulationClock.Time < observationUntil || Time.frameCount - startedFrame < 4)
            && Time.realtimeSinceStartup - startedRealtime < OverallTimeoutSeconds)
        {
            yield return null;
        }
        Time.timeScale = 0f;
        bool progressed = simulationClock.Time >= observationUntil && Time.frameCount - startedFrame >= 4;
        Check(progressed, "W01_SURGERY_PUBLICATION_RECOVERY_TICK_OBSERVED",
            "simulation=" + (simulationClock.Time - startedSimulation)
                + "; frames=" + (Time.frameCount - startedFrame)
                + "; realtime=" + (Time.realtimeSinceStartup - startedRealtime));
        surgery.TryGetOrder(orderId, out order);
        List<GameplayOutcomeSnapshot> after = W01ExactOutcomes(scope);
        GameplayOutcomeSnapshot[] afterIdentities = W01IdentityOutcomes(after, terminal);
        GameplayOutcomeSnapshot[] afterChildren = after.Where(value =>
            W01ResultKey(value) == childKey.ToString()).ToArray();
        int generic = identityEvents.Count(value => value.Character.Equals(terminal.Character)
            && value.WorkId == BuiltInWorkTypeIds.Surgery.Value);
        int domainAttempts = identityEvents.Count(value => value.WorkId == terminal.WorkId);
        Check(order != null && W01CompletionAcknowledged(scope, order)
                && W01CompletionNotificationAttempted(scope, order) && order.completionFactDelivered
                && !string.IsNullOrEmpty(order.completionPublicationId)
                && !string.IsNullOrEmpty(order.completionFingerprint)
                && order.completionDoctorId == terminal.Character.Value
                && order.completionAbsoluteDay == terminal.AbsoluteDay && domainAttempts == 1,
            "W01_SURGERY_ACK_RETAINED_NOTIFICATION_FAILURE_NOT_REPLAYED",
            "state=" + order?.state + "; doctor=" + order?.doctorId
                + "; frozenDoctor=" + order?.completionDoctorId
                + "; frozenDay=" + order?.completionAbsoluteDay
                + "; ack=" + W01CompletionAcknowledged(scope, order)
                + "; published=" + order?.completionFactDelivered
                + "; domainEventAttempts=" + domainAttempts);
        Check(order != null && order.state == SurgeryOrderState.Completed
                && afterIdentities.Length == 1 && afterChildren.Length == 1
                && W01IdentityFingerprint(afterIdentities) == identityBefore
                && W01IdentityFingerprint(afterChildren) == childBefore
                && W01CursorFingerprint(deliveries) == cursorBefore
                && Mathf.Approximately(order.completedWork, workBefore)
                && CountPhysicalItem(StandardMedicineItemId) == medicineBefore
                && CountPhysicalItem(CleanWaterItemId) == waterBefore && generic == 0,
            "W01_SURGERY_PUBLICATION_RECOVERY_NO_DUPLICATE",
            "identities=" + afterIdentities.Length + "; children=" + afterChildren.Length
                + "; generic=" + generic + "; state=" + order?.state
                + "; medicine=" + medicineBefore + "->" + CountPhysicalItem(StandardMedicineItemId)
                + "; water=" + waterBefore + "->" + CountPhysicalItem(CleanWaterItemId));
        W01WriteWorldEvidence("w01-surgery-publication-after-wait-world.json",
            gameSave.ToJson(gameSave.Capture(), true));
        Check(commandAckFaultCount == 1 && publicationFaultCount == 1
                && capturedErrors.Count == 0,
            "W01_SURGERY_PUBLICATION_EXPECTED_FAULT_ACCOUNTED",
            "command=" + commandAckFaultCount + "; notification=" + publicationFaultCount
                + "; unhandledErrors=" + capturedErrors.Count
                + "; observer recovered unknown ACK without retrying attempted notification");
        yield return VerifySurgeryDepartedDoctorRecovery(scope, orderId, pendingJson, terminal, simulationClock);
        SurgeryDebugScenarios.RunDoctorReferenceAvailabilityContracts();
        Check(true, "W01_SURGERY_CURRENT_DOCTOR_REFERENCE_BOUNDARY",
            "committed clinical history permits absent doctors; unfinished work still requires current doctors");
        WorkTerminalIdentityOwnershipDebugScenarios.RunOrThrow();
        Check(true, "W01_SURGERY_BOUNDED_DELIVERY_OWNER", "bounded command/defer/notification/save tests completed");
    }

    private IEnumerator VerifySurgeryDepartedDoctorRecovery(DungeonRuntimeLifetimeScope scope,
        string orderId, string pendingJson, WorkCompletedIdentityEvent terminal, IGameClock simulationClock)
    {
        CurrentStage = "w01-doctor-departure-pending-rewind";
        Time.timeScale = 0f;
        // Independent controlled branch from the actual committed clinical/identity
        // result. Never fabricate a historical doctor, outcome, or alive flag.
        DungeonGameSaveData pendingWorld = gameSave.FromJson(pendingJson);
        WorldCharacterProfile priorPopulationProfile = W01CharacterWorld(pendingWorld)
            .populationProfiles.SingleOrDefault(value => value.persistentId == terminal.Character.Value);
        bool rewound = gameSave.TryRestore(pendingWorld, out var rewindReport);
        if (!Check(rewound && rewindReport.Success, "W01_SURGERY_DEPARTURE_BRANCH_REWIND",
                rewound ? "actual pending world restored for an independent departure branch"
                    : string.Join(" | ", rewindReport.Errors)))
            yield break;
        ICharacterWorldQuery characters = Resolve<ICharacterWorldQuery>(scope);
        doctor = characters.Characters.Single(value => value != null
            && value.Identity?.PersistentId == terminal.Character.Value);
        doctorWork = doctor.GetAbility<AbilityWork>();
        foreach (CharacterActor actor in characters.Characters.Where(value => value != null))
            actor.SetAiPaused(true);
        surgery.TryGetOrder(orderId, out SurgeryOrder order);
        StaffDiscontentRuntime discontent = Resolve<CharacterSceneRuntimeReferences>(scope)?.StaffDiscontent;
        ICharacterSettlementStandingQuery standings = Resolve<ICharacterSettlementStandingQuery>(scope);
        if (!Check(order != null && discontent != null && !doctor.IsOwner && !doctor.IsDead
                && standings?.IsFormalResident(doctor) == true
                && order.resolutionFactAccepted && !order.completionFactDelivered
                && W01CompletionAcknowledged(scope, order) && !W01CompletionNotificationAttempted(scope, order),
                "W01_SURGERY_DEPARTURE_SOURCE_READY", "doctor=" + terminal.Character.Value
                    + "; health=" + doctor.Stats.CurrentHealth + "; ack=" + W01CompletionAcknowledged(scope, order)))
            yield break;
        string patientId = order.subject.subjectId;
        float healthBefore = doctor.Stats.CurrentHealth;
        float workBefore = order.completedWork;
        int medicineBefore = CountPhysicalItem(StandardMedicineItemId);
        int waterBefore = CountPhysicalItem(CleanWaterItemId);
        WorkCompletionIdentityDeliveryLedger deliveries = Resolve<WorkCompletionIdentityDeliveryLedger>(scope);
        string cursorBefore = W01CursorFingerprint(deliveries);
        List<GameplayOutcomeSnapshot> outcomesBefore = W01ExactOutcomes(scope);
        string childBefore = W01IdentityFingerprint(outcomesBefore.Where(value => W01ResultKey(value) == terminal.ChildResultKey));
        string identitiesBefore = W01IdentityFingerprint(outcomesBefore.Where(value =>
            value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
                && W01FactEquals(value, WorkCompletionIdentityOutcomeIds.WorkIdFact, terminal.WorkId)));

        float departureMood = (discontent.Rules.rebellionMoodThreshold + discontent.Rules.departureMoodThreshold) / 2f;
        if (!Check(departureMood > discontent.Rules.rebellionMoodThreshold
                && departureMood <= discontent.Rules.departureMoodThreshold,
                "W01_SURGERY_DEPARTURE_INPUT_RANGE", "controlled mood=" + departureMood))
            yield break;
        IGameCalendar calendar = Resolve<IGameCalendar>(scope);
        int nextDay = calendar.Day + 1;
        if (discontent.State.TryGetRecord(doctor, out StaffDiscontentRecord prior))
            nextDay = Math.Max(nextDay, prior.LastProcessedAbsoluteDay + 1);
        calendar.SetDateTime(nextDay, calendar.Hour);
        // Lower the controlled base first. Its floor cannot cancel positive
        // need factors, but the remaining pressure must fit the production
        // interaction-factor limit instead of requesting a clamped large value.
        doctor.Stats.SetControlledStatValue(CharacterCondition.MOOD, departureMood);
        CharacterMoodSnapshot moodBefore = doctor.Stats.GetMoodSnapshot();
        float departurePressure = departureMood - moodBefore.BaseValue
            - moodBefore.Factors.Sum(value => value.Value);
        float boundedPressure = new CharacterMoodMemory(
            "qa:w01:departure-input-probe", "Departure input range probe",
            departurePressure, 120f, 1, simulationClock.Time).TotalValue;
        if (!Check(Mathf.Approximately(boundedPressure, departurePressure),
                "W01_SURGERY_DEPARTURE_FACTOR_NOT_CLAMPED", "base=" + moodBefore.BaseValue
                    + "; factors=" + moodBefore.Factors.Sum(value => value.Value)
                    + "; requested=" + departurePressure + "; bounded=" + boundedPressure))
            yield break;
        bool pressureApplied = doctor.Stats.ApplyResolvedMoodFactor(
            "qa:w01:departure-pressure:" + orderId, "Controlled departure pressure",
            departurePressure, 120f, 1, absoluteDay: nextDay);
        float observedMood = StaffDiscontentService.GetMood(doctor);
        if (!Check(pressureApplied && Mathf.Approximately(observedMood, departureMood)
                && StaffDiscontentService.EvaluateStage(observedMood, 0, discontent.Rules)
                    == StaffDiscontentStage.Departure,
                "W01_SURGERY_DEPARTURE_PROJECTED_INPUT", "base=" + moodBefore.BaseValue
                    + "; factors=" + moodBefore.Factors.Sum(value => value.Value)
                    + "; pressure=" + departurePressure + "; observed=" + observedMood))
            yield break;
        StaffDiscontentRecord departed = discontent.ProcessStaff(doctor, out StaffDiscontentOutcome departure);
        if (!Check(departure == StaffDiscontentOutcome.PermanentDeparture
                && departed != null && departed.IsDeparted && departed.IsPermanentLoss,
                "W01_SURGERY_PRODUCTION_STAFF_DEPARTURE", "outcome=" + departure
                    + "; stage=" + departed?.Stage + "; observedMood=" + departed?.LastMood
                    + "; lifecycle=" + doctor.CurrentLifecycleState))
            yield break;
        // A real departure is not permission to rewrite living staff as dead.
        // Actor.IsDead deliberately includes Despawned/unavailable actors;
        // biological life belongs to Stats, not that composite availability flag.
        Check(!doctor.Stats.IsDead && doctor.CurrentLifecycleState == CharacterLifecycleState.Despawned
                && Mathf.Approximately(doctor.Stats.CurrentHealth, healthBefore),
            "W01_SURGERY_DEPARTURE_PRESERVES_LIFE", "vitalsDead=" + doctor.Stats.IsDead
                + "; actorDead=" + doctor.IsDead + "; health=" + healthBefore + "->" + doctor.Stats.CurrentHealth);
        DungeonGameSaveData absentSave = null;
        try { absentSave = gameSave.Capture(); }
        catch (Exception error) { Check(false, "W01_SURGERY_DEPARTURE_CAPTURE", error.ToString()); }
        if (absentSave == null)
            yield break;
        DungeonCharacterWorldSaveData absentWorld = W01CharacterWorld(absentSave);
        WorldCharacterProfile history = absentWorld.populationProfiles.SingleOrDefault(value => value.persistentId == terminal.Character.Value);
        DungeonStaffDiscontentSaveData departureOwner = DungeonSaveJson.Deserialize<DungeonStaffDiscontentSaveData>(
            absentSave.sections.Single(value => value.sectionId == "characters.staff-discontent").payloadJson);
        DungeonStaffDiscontentRecordSaveData savedDeparture = departureOwner.records.SingleOrDefault(value =>
            value.staffId == terminal.Character.Value);
        // Initial run staff need not have a visitor-population profile. Retain
        // existing membership; do not invent a profile to witness departure.
        bool populationUnchanged = priorPopulationProfile == null ? history == null
            : history != null && history.isAlive == priorPopulationProfile.isAlive;
        Check(populationUnchanged && savedDeparture?.departed == true
                && savedDeparture.permanentLoss
                && !absentWorld.actors.Any(value => value.persistentId == terminal.Character.Value),
            "W01_SURGERY_DEPARTURE_OWNER_WITHOUT_ACTOR", "profileBefore=" + (priorPopulationProfile != null)
                + "; profileAfter=" + (history != null) + "; departed=" + savedDeparture?.departed
                + "; permanentLoss=" + savedDeparture?.permanentLoss + "; actorRecords="
                + absentWorld.actors.Count(value => value.persistentId == terminal.Character.Value));
        string absentJson = gameSave.ToJson(absentSave, true);
        W01WriteWorldEvidence("w01-surgery-departed-doctor-pending-world.json", absentJson);
        bool restored = gameSave.TryRestore(gameSave.FromJson(absentJson), out var restoreReport);
        if (!Check(restored && restoreReport.Success, "W01_SURGERY_DEPARTURE_WORLD_RESTORE",
                restored ? "sections=" + absentSave.sections.Count : string.Join(" | ", restoreReport.Errors)))
            yield break;
        doctor = null;
        doctorWork = null;
        patient = characters.Characters.Single(value => value != null && value.Identity?.PersistentId == patientId);
        foreach (CharacterActor actor in characters.Characters.Where(value => value != null))
            actor.SetAiPaused(true);
        surgery.TryGetOrder(orderId, out order);
        if (!Check(!characters.Characters.Any(value => value != null && value.Identity?.PersistentId == terminal.Character.Value)
                && discontent.State.TryGetRecord(terminal.Character.Value, out StaffDiscontentRecord restoredDeparture)
                && restoredDeparture.IsDeparted && restoredDeparture.IsPermanentLoss
                && order != null && order.completionDoctorId == terminal.Character.Value
                && order.completionAbsoluteDay == terminal.AbsoluteDay && !order.completionFactDelivered
                && W01CompletionAcknowledged(scope, order) && !W01CompletionNotificationAttempted(scope, order)
                && W01CursorFingerprint(deliveries) == cursorBefore,
                "W01_SURGERY_DEPARTED_DOCTOR_FROZEN_OWNER", "doctor=" + order?.completionDoctorId
                    + "; day=" + order?.completionAbsoluteDay + "; ack=" + W01CompletionAcknowledged(scope, order)))
            yield break;
        int attemptsBefore = identityEvents.Count(value => value.Character.Equals(terminal.Character) && value.WorkId == terminal.WorkId);
        float startedSimulation = simulationClock.Time;
        float startedRealtime = Time.realtimeSinceStartup;
        int startedFrame = Time.frameCount;
        float observationUntil = Mathf.Max(startedSimulation + 5f, order.recoveryUntil + 2f);
        CurrentStage = "w01-departed-doctor-natural-ack";
        Time.timeScale = VerificationTimeScale;
        while ((simulationClock.Time < observationUntil || Time.frameCount - startedFrame < 4)
            && Time.realtimeSinceStartup - startedRealtime < OverallTimeoutSeconds)
            yield return null;
        Time.timeScale = 0f;
        surgery.TryGetOrder(orderId, out order);
        List<GameplayOutcomeSnapshot> after = W01ExactOutcomes(scope);
        bool exact = W01IdentityFingerprint(after.Where(value => W01ResultKey(value) == terminal.ChildResultKey)) == childBefore
            && W01IdentityFingerprint(after.Where(value => value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
                && W01FactEquals(value, WorkCompletionIdentityOutcomeIds.WorkIdFact, terminal.WorkId))) == identitiesBefore
            && W01CursorFingerprint(deliveries) == cursorBefore;
        Check(simulationClock.Time >= observationUntil && Time.frameCount - startedFrame >= 4
                && order != null && order.state == SurgeryOrderState.Completed
                && W01CompletionAcknowledged(scope, order) && W01CompletionNotificationAttempted(scope, order) && order.completionFactDelivered
                && order.completionDoctorId == terminal.Character.Value && order.completionAbsoluteDay == terminal.AbsoluteDay
                && Mathf.Approximately(order.completedWork, workBefore) && exact
                && CountPhysicalItem(StandardMedicineItemId) == medicineBefore && CountPhysicalItem(CleanWaterItemId) == waterBefore
                && identityEvents.Count(value => value.Character.Equals(terminal.Character) && value.WorkId == terminal.WorkId) == attemptsBefore + 1,
            "W01_SURGERY_DEPARTED_DOCTOR_ACK_NO_DUPLICATE", "state=" + order?.state
                + "; ack=" + W01CompletionAcknowledged(scope, order) + "; published=" + order?.completionFactDelivered
                + "; exact=" + exact + "; simulation=" + (simulationClock.Time - startedSimulation)
                + "; frames=" + (Time.frameCount - startedFrame));
        string survivorsBefore = SurvivorEffects();
        WorkCompletionIdentityDeliveryRequest request = WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(terminal);
        WorkCompletionIdentityDeliveryResult replay = originalCompletionCommand.EnsureApplied(request);
        Resolve<CharacterIdentityEventPublisher>(scope).Publish(terminal);
        Resolve<CharacterIdentityEventPublisher>(scope).Publish(terminal);
        Check(replay.Status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied
                && W01CursorFingerprint(deliveries) == cursorBefore && SurvivorEffects() == survivorsBefore
                && W01IdentityFingerprint(W01ExactOutcomes(scope).Where(value =>
                    value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
                        && W01FactEquals(value, WorkCompletionIdentityOutcomeIds.WorkIdFact, terminal.WorkId))) == identitiesBefore,
            "W01_SURGERY_DEPARTED_DOCTOR_REDELIVERY_NO_OTHER_RECIPIENT", "status=" + replay.Status
                + "; originalDoctor=" + terminal.Character.Value + "; surviving effects unchanged=" + (SurvivorEffects() == survivorsBefore));
        W01WriteWorldEvidence("w01-surgery-departed-doctor-completed-world.json", gameSave.ToJson(gameSave.Capture(), true));

        string SurvivorEffects() => string.Join("|", characters.Characters.Where(value => value != null)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .Select(value => value.Identity.PersistentId + ":" + W01WorkerIdentityFingerprint(scope, value)));
    }

    private static DungeonCharacterWorldSaveData W01CharacterWorld(DungeonGameSaveData save) =>
        DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(save.sections.Single(value =>
            value.sectionId == "characters.world").payloadJson);

    private void VerifyW01SurgeryStoredOwnerRejections(
        DungeonRuntimeLifetimeScope scope, string sourceJson, string orderId, string otherDoctorId,
        bool acknowledged = false)
    {
        if (!acknowledged)
        {
            Reject("empty-doctor", value => value.completionDoctorId = string.Empty);
            Reject("negative-day", value => value.completionAbsoluteDay = -1);
            Reject("different-valid-doctor", value => value.completionDoctorId = otherDoctorId);
            Reject("different-valid-day", value => value.completionAbsoluteDay =
                value.completionAbsoluteDay == 0 ? 1 : value.completionAbsoluteDay - 1);
        }
        else
        {
            Reject("ack-without-cursor", null, (candidate, owner) =>
            {
                var narrativeSection = candidate.sections.Single(value => value.sectionId == CharacterNarrativeSaveSection.Id);
                var narrative = DungeonSaveJson.Deserialize<CharacterNarrativeWorldSaveData>(narrativeSection.payloadJson);
                var request = WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(
                    SurgeryWorkCompletionFactMapping.CreateCompletionEvent(new SurgeryWorkCompletedFact(owner.resolvedFact)));
                int removed = narrative.workCompletionDeliveries.RemoveAll(value => value.producerStreamId == request.ProducerStreamId);
                if (removed != 1) throw new InvalidOperationException("Surgery ACK corruption requires exactly one real cursor.");
                narrativeSection.payloadJson = JsonUtility.ToJson(narrative);
            });
        }

        void Reject(string caseId, Action<SurgeryOrder> mutate,
            Action<DungeonGameSaveData, SurgeryOrder> mutateOther = null)
        {
            DungeonGameSaveData candidate = gameSave.FromJson(sourceJson);
            DungeonSaveSectionEnvelope section = candidate.sections.Single(value =>
                value.sectionId == "medical.surgery");
            DungeonSurgerySaveData medical = DungeonSaveJson.Deserialize<DungeonSurgerySaveData>(section.payloadJson);
            SurgeryOrder owner = medical.orders.Single(value => value.orderId == orderId);
            mutate?.Invoke(owner);
            section.payloadJson = JsonUtility.ToJson(medical);
            mutateOther?.Invoke(candidate, owner);
            // Only reseal this detached QA copy: test the domain join, not checksum rejection.
            candidate.manifest = DungeonSaveManifest.Capture(candidate.sections);
            W01WriteWorldEvidence("w01-surgery-owner-" + caseId + "-invalid-world.json",
                gameSave.ToJson(candidate, true));
            DungeonRuntimeAggregateRootStore root = Resolve<DungeonRuntimeAggregateRootStore>(scope);
            int revision = root.PublishedRestoreRevision;
            surgery.TryGetOrder(orderId, out SurgeryOrder ownerBefore);
            string before = SectionPayloads(gameSave.Capture());
            bool accepted = gameSave.TryRestore(candidate, out DungeonGameRestoreReport failure);
            bool unchanged = false;
            string captureError = string.Empty;
            if (!accepted)
            {
                try { unchanged = before == SectionPayloads(gameSave.Capture()); }
                catch (Exception error) { captureError = error.Message; }
            }
            surgery.TryGetOrder(orderId, out SurgeryOrder ownerAfter);
            bool rejectedForOwner = !accepted && failure.Errors.Any(value =>
                value.IndexOf("surgery", StringComparison.OrdinalIgnoreCase) >= 0);
            bool referencesExact = ReferenceEquals(ownerBefore, ownerAfter)
                && root.PublishedRestoreRevision == revision && !root.IsRestoreStaging;
            string detail = "accepted=" + accepted + "; sectionsUnchanged=" + unchanged
                + "; ownerReferencesUnchanged=" + referencesExact
                + "; errors=" + string.Join(" | ", failure.Errors) + "; captureError=" + captureError;
            W01WriteWorldEvidence("w01-surgery-owner-" + caseId + "-witness.txt", detail);
            Check(rejectedForOwner && unchanged && referencesExact,
                "W01_SURGERY_STORED_OWNER_REJECT_" + caseId.ToUpperInvariant().Replace('-', '_'), detail);
            if (accepted || !unchanged || !referencesExact)
            {
                // Explicit independent QA rewind after recording FAIL. Never run a
                // corrupt accepted world forward or turn its acceptance into a PASS.
                bool rewound = gameSave.TryRestore(gameSave.FromJson(sourceJson), out var rewind);
                Check(rewound && rewind.Success, "W01_SURGERY_OWNER_TEST_REWIND_" + caseId,
                    rewound ? "clean official world restored after failed rejection test"
                        : string.Join(" | ", rewind.Errors));
                if (!rewound)
                    throw new InvalidOperationException("Surgery owner rejection QA rewind failed: " + caseId);
            }
        }

        static string SectionPayloads(DungeonGameSaveData data) => string.Join("\n", data.sections
            .OrderBy(value => value.sectionId, StringComparer.Ordinal)
            .Select(value => value.sectionId + ":" + value.sectionVersion + ":" + value.payloadJson));
    }

    private static void W01WriteWorldEvidence(string filename, string json)
    {
        string output = Path.GetDirectoryName(SurgeryPlayModeVerifier.ReportPath);
        Directory.CreateDirectory(output);
        using FileStream stream = new(Path.Combine(output, filename),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false));
        writer.Write(json);
    }

    private void VerifyW01ClinicalFailureResult(DungeonRuntimeLifetimeScope scope,
        SurgeryOrder order, int initialWater, int remainingWater)
    {
        CurrentStage = "w01-clinical-failure-result";
        GameplayResultKey childKey = SurgeryOutcomeIds.ResultKey(order.orderId);
        GameplayOutcomeSnapshot[] children = W01ExactOutcomes(scope)
            .Where(value => W01ResultKey(value) == childKey.ToString()).ToArray();
        bool failed = order.state == SurgeryOrderState.Failed
            && order.resultRolled && !order.resultSucceeded
            && order.resultOutcomeId == "failure" && order.failureSeverity == SurgeryFailureSeverity.Minor
            && order.outcomeConsequencesApplied && order.outcomeConsequenceStep == 2
            && order.resolvedEffectCount == 0 && order.resolutionFactAccepted
            && order.statusData.code == SurgeryStatusCode.CompletedWithMinorFailure;
        Check(failed, "W01_CLINICAL_FAILURE_FROZEN_RESULT",
            $"state={order.state}; result={order.resultOutcomeId}; succeeded={order.resultSucceeded}; severity={order.failureSeverity}; consequence={order.outcomeConsequenceStep}; successEffects={order.resolvedEffectCount}; status={order.statusData.code}");
        ulong randomState = Resolve<IRandomStreamProvider>(scope).Get("medical:surgery-outcomes").State;
        Check(randomState == clinicalFailureExpectedRandomState,
            "W01_CLINICAL_FAILURE_PRODUCTION_RNG_TWO_DRAWS",
            $"expected={clinicalFailureExpectedRandomState}; actual={randomState}");
        Check(initialWater - remainingWater == 1, "W01_CLINICAL_FAILURE_WATER_CONSUMED_ONCE",
            $"water={initialWater}->{remainingWater}");
        bool exactChild = children.Length == 1 && children[0].status == GameplayOutcomeStatus.Failed
            && W01FactEquals(children[0], SurgeryOutcomeIds.Result, "failure")
            && W01FactEquals(children[0], SurgeryOutcomeIds.Severity, "Minor")
            && children[0].absoluteDay == order.completionAbsoluteDay;
        Check(exactChild, "W01_CLINICAL_FAILURE_FAILED_CHILD_NOT_SUCCESS",
            $"children={children.Length}; exact={exactChild}; childStatus={(children.Length == 1 ? children[0].status.ToString() : "missing")}; frozenDoctor={order.completionDoctorId}; frozenDay={order.completionAbsoluteDay}");
        if (children.Length != 1)
            return;
        string before = children[0].facts.Single(value => value.factId == SurgeryOutcomeIds.AnatomyBefore.Value).value;
        string after = children[0].facts.Single(value => value.factId == SurgeryOutcomeIds.AnatomyAfter.Value).value;
        float beforeHealth = W01ClinicalScalar(before, "health");
        float afterHealth = W01ClinicalScalar(after, "health");
        float beforeBleeding = W01ClinicalScalar(before, "bleeding");
        float afterBleeding = W01ClinicalScalar(after, "bleeding");
        float beforeInfection = W01ClinicalScalar(before, "infection");
        float afterInfection = W01ClinicalScalar(after, "infection");
        AnatomyNodeHealthState current = anatomy.GetAnatomySnapshot(patient).Nodes.Single(value => value.nodeId == targetNodeId);
        Check(beforeHealth > 3f && Mathf.Abs(beforeHealth - afterHealth - 3f) < 0.001f
                && Mathf.Abs(afterBleeding - beforeBleeding - 0.08f) < 0.001f
                && Mathf.Abs(afterInfection - Mathf.Min(100f, beforeInfection + order.risk.infectionChance * 20f)) < 0.001f
                && Mathf.Abs(current.currentHealth - afterHealth) < 0.001f
                && !patient.Stats.IsDead && after.Split('|').Contains("life=alive"),
            "W01_CLINICAL_FAILURE_ACTUAL_PATIENT_CONSEQUENCES",
            $"health={beforeHealth:R}->{afterHealth:R}/live={current.currentHealth:R}; bleeding={beforeBleeding:R}->{afterBleeding:R}; infection={beforeInfection:R}->{afterInfection:R}; risk={order.risk.infectionChance:R}; alive={!patient.Stats.IsDead}");
        Check(children[0].facts.Single(value => value.factId == SurgeryOutcomeIds.Materials.Value).value
                    .Contains("consumed=1")
                && children[0].facts.Single(value => value.factId == SurgeryOutcomeIds.ChildReferences.Value).value
                    .Split('|').Contains("material-sink=" + order.materialSinkCommitId)
                && !string.IsNullOrEmpty(order.materialSinkCommitId),
            "W01_CLINICAL_FAILURE_MATERIAL_SINK_JOIN",
            "sink=" + order.materialSinkCommitId + "; physical inputs are consumed even though the procedure failed");
    }

    private static float W01ClinicalScalar(string fact, string key) => float.Parse(
        fact.Split('|').Single(value => value.StartsWith(key + "=", StringComparison.Ordinal))
            .Substring(key.Length + 1).Split('/')[0],
        System.Globalization.CultureInfo.InvariantCulture);

    private string W01PatientClinicalFingerprint()
    {
        AnatomyNodeHealthState node = anatomy.GetAnatomySnapshot(patient).Nodes
            .Single(value => value.nodeId == targetNodeId);
        return JsonUtility.ToJson(node) + "|dead=" + patient.Stats.IsDead
            + "|health=" + patient.Stats.CurrentHealth
                .ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void VerifyW01SurgeryTerminalIdentity(
        DungeonRuntimeLifetimeScope scope,
        SurgeryOrder order,
        WorkCompletedIdentityEvent terminal)
    {
        CurrentStage = "w01-terminal-identity-join";
        GameplayResultKey childKey = SurgeryOutcomeIds.ResultKey(order.orderId);
        List<GameplayOutcomeSnapshot> original = W01ExactOutcomes(scope);
        GameplayOutcomeSnapshot[] children = original.Where(value =>
            W01ResultKey(value) == childKey.ToString()).ToArray();
        GameplayOutcomeSnapshot[] identities = W01IdentityOutcomes(original, terminal);
        bool childExact = order.resolutionFactAccepted && children.Length == 1
            && children[0].outcomeTypeId == SurgeryOutcomeIds.Resolved.Value
            && children[0].status == (order.resultSucceeded
                ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.Failed)
            && children[0].absoluteDay == order.completionAbsoluteDay
            && children[0].participants.Any(value => value.entityId == order.subject.subjectId
                && value.roleId == SurgeryOutcomeIds.Patient.Value)
            && children[0].facts.Any(value => value.factId == SurgeryOutcomeIds.Procedure.Value
                && value.value.Split('|').Contains("doctor=" + terminal.Character.Value));
        Check(childExact, "W01_SURGERY_COMMITTED_CHILD_EXACT",
            "committed=" + order.resolutionFactAccepted + "; children=" + children.Length
                + "; child=" + childKey + "; doctor=" + terminal.Character.Value
                + "; patient=" + order.subject.subjectId);
        bool referencesExact = terminal.Character.Value == order.completionDoctorId
            && terminal.AbsoluteDay == order.completionAbsoluteDay
            && terminal.OperationId == childKey.OperationId.Value
            && terminal.TerminalOwnerId == childKey.ProducerId
            && terminal.ChildResultKey == childKey.ToString()
            && terminal.TargetEntityId == order.facilityId
            && Mathf.Approximately(terminal.ContributionWorkUnits, order.completedWork);
        bool ledgerJoined = identities.Length == 1
            && W01FactEquals(identities[0], WorkCompletionIdentityOutcomeIds.OperationIdFact,
                childKey.OperationId.Value)
            && W01FactEquals(identities[0], WorkCompletionIdentityOutcomeIds.TerminalOwnerFact,
                childKey.ProducerId)
            && W01FactEquals(identities[0], WorkCompletionIdentityOutcomeIds.ChildResultKeyFact,
                childKey.ToString())
            && identities[0].subjects.Any(value =>
                value.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value
                && value.entityId == order.facilityId)
            && identities[0].metrics.Any(value =>
                value.metricId == WorkCompletionIdentityOutcomeIds.ContributionMetric.Value
                && Math.Abs(value.value - order.completedWork) < 0.001d);
        Check(childExact && referencesExact && ledgerJoined, "W01_SURGERY_IDENTITY_CHILD_JOIN",
            "eventRefs=" + referencesExact + "; identityLedgerJoin=" + ledgerJoined
                + "; identities=" + identities.Length + "; operation=" + terminal.OperationId
                + "; owner=" + terminal.TerminalOwnerId + "; child=" + terminal.ChildResultKey
                + "; facility=" + terminal.TargetEntityId
                + "; work=" + terminal.ContributionWorkUnits + "/" + order.completedWork);

        doctor.Brain?.StopCurrentActionForReplan("W01 completed surgery replay");
        doctorWork?.ClearPriorityWorkTarget();
        doctor.SetAiPaused(true);
        patient.SetAiPaused(true);
        Time.timeScale = 0f;
        DungeonGameSaveData completed = gameSave.Capture();
        string completedJson = gameSave.ToJson(completed, true);
        string output = Path.GetDirectoryName(SurgeryPlayModeVerifier.ReportPath);
        Directory.CreateDirectory(output);
        using (FileStream stream = new(Path.Combine(output, SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification
                       ? "w01-surgery-clinical-failed-world.json" : "w01-surgery-completed-world.json"),
                   FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false)))
            writer.Write(completedJson);

        IGameEventBus events = Resolve<IGameEventBus>(scope);
        WorkCompletionIdentityDeliveryLedger deliveries =
            Resolve<WorkCompletionIdentityDeliveryLedger>(scope);
        string cursorBefore = W01CursorFingerprint(deliveries);
        string identityBefore = W01IdentityFingerprint(identities);
        string workerBefore = W01WorkerIdentityFingerprint(scope, doctor);
        string patientBefore = W01PatientClinicalFingerprint();
        ulong randomBefore = Resolve<IRandomStreamProvider>(scope).Get("medical:surgery-outcomes").State;
        string childBefore = children.Length == 1 ? children[0].immutablePayloadHash : "missing";
        int medicineBefore = CountPhysicalItem(StandardMedicineItemId);
        int waterBefore = CountPhysicalItem(CleanWaterItemId);
        events.Publish(terminal);
        events.Publish(terminal);
        List<GameplayOutcomeSnapshot> replayed = W01ExactOutcomes(scope);
        GameplayOutcomeSnapshot[] replayIdentities = W01IdentityOutcomes(replayed, terminal);
        Check(replayIdentities.Length == 1
                && W01IdentityFingerprint(replayIdentities) == identityBefore
                && W01CursorFingerprint(deliveries) == cursorBefore
                && W01WorkerIdentityFingerprint(scope, doctor) == workerBefore
                && W01PatientClinicalFingerprint() == patientBefore
                && Resolve<IRandomStreamProvider>(scope).Get("medical:surgery-outcomes").State == randomBefore
                && replayed.Count(value => W01ResultKey(value) == childKey.ToString()) == 1
                && CountPhysicalItem(StandardMedicineItemId) == medicineBefore
                && CountPhysicalItem(CleanWaterItemId) == waterBefore,
            "W01_SURGERY_SAME_EVENT_REDELIVERY_NO_DUPLICATE",
            "identities=" + identities.Length + "->" + replayIdentities.Length
                + "; cursorUnchanged=" + (W01CursorFingerprint(deliveries) == cursorBefore)
                + "; workerEffectsUnchanged=" + (W01WorkerIdentityFingerprint(scope, doctor) == workerBefore)
                + "; child=" + childKey);

        WorkCompletedIdentityEvent changedPayload = new(
            terminal.Character, terminal.WorkId, terminal.ProductId, terminal.Origin,
            terminal.AbsoluteDay, terminal.OperationId, terminal.TerminalOwnerId,
            terminal.ChildResultKey, terminal.TargetEntityId, terminal.ContributionWorkUnits + 1f);
        bool changedRejected = W01TryRejectDelivery(events, changedPayload, out string changedFailure);
        GameplayResultKey absentChild = SurgeryOutcomeIds.ResultKey("surgery:w01-absent");
        WorkCompletedIdentityEvent absentReference = new(
            terminal.Character, terminal.WorkId, terminal.ProductId, terminal.Origin,
            terminal.AbsoluteDay, absentChild.OperationId.Value, absentChild.ProducerId,
            absentChild.ToString(), terminal.TargetEntityId, terminal.ContributionWorkUnits);
        bool absentRejected = W01TryRejectDelivery(events, absentReference, out string absentFailure);
        Check(changedRejected && absentRejected
                && changedFailure == "The completion delivery sequence has conflicting provenance."
                && absentFailure == "work-identity-child-not-committed:" + absentChild
                && W01IdentityFingerprint(W01IdentityOutcomes(W01ExactOutcomes(scope), terminal)) == identityBefore
                && W01CursorFingerprint(deliveries) == cursorBefore
                && W01WorkerIdentityFingerprint(scope, doctor) == workerBefore,
            "W01_SURGERY_INVALID_REDELIVERY_REJECTED_WITHOUT_EFFECTS",
            "changedRejected=" + changedRejected + "; changedReason=" + changedFailure
                + "; absentRejected=" + absentRejected + "; absentReason=" + absentFailure);

        // Inject an inconsistent owner snapshot without touching the committed
        // outcome. An existing outcome must never authorize a second effect apply.
        IReadOnlyList<WorkCompletionIdentityDeliveryCursorSaveData> savedCursors = deliveries.Capture();
        WorkCompletionIdentityDeliveryRequest referenced =
            WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(terminal);
        try
        {
            deliveries.Restore(savedCursors.Where(value =>
                value.producerStreamId != referenced.ProducerStreamId));
            string missingCursorBefore = W01CursorFingerprint(deliveries);
            bool missingCursorRejected = W01TryRejectDelivery(events, terminal, out string cursorFailure);
            Check(missingCursorRejected
                    && cursorFailure.Contains("outcome exists without its committed delivery cursor")
                    && W01CursorFingerprint(deliveries) == missingCursorBefore
                    && W01WorkerIdentityFingerprint(scope, doctor) == workerBefore
                    && W01IdentityFingerprint(W01IdentityOutcomes(W01ExactOutcomes(scope), terminal)) == identityBefore,
                "W01_SURGERY_MISSING_CURSOR_REJECTED_WITHOUT_EFFECTS",
                "rejected=" + missingCursorRejected + "; reason=" + cursorFailure);
        }
        finally
        {
            deliveries.Restore(savedCursors);
        }

        CharacterActor previousDoctor = doctor;
        CharacterActor previousPatient = patient;
        string patientId = CharacterPersistentIdentity.Require(patient).Value;
        bool restored = gameSave.TryRestore(gameSave.FromJson(completedJson),
            out DungeonGameRestoreReport restoreReport);
        Check(restored && restoreReport.Success, "W01_SURGERY_TERMINAL_WORLD_RESTORE",
            restored ? "sections=" + completed.sections.Count
                : string.Join(" | ", restoreReport.Errors));
        if (!restored)
            return;
        ICharacterWorldQuery characters = Resolve<ICharacterWorldQuery>(scope);
        doctor = characters.Characters.Single(value => value != null
            && value.Identity?.PersistentId == terminal.Character.Value);
        patient = characters.Characters.Single(value => value != null
            && value.Identity?.PersistentId == patientId);
        doctorWork = doctor.GetAbility<AbilityWork>();
        bool freshActors = !ReferenceEquals(previousDoctor, doctor)
            && !ReferenceEquals(previousPatient, patient);
        string restoredWorkerBefore = W01WorkerIdentityFingerprint(scope, doctor);
        string restoredPatientBefore = W01PatientClinicalFingerprint();
        if (SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification)
        {
            bool found = surgery.TryGetOrder(order.orderId, out SurgeryOrder restoredOrder);
            bool applied = Resolve<ISurgeryWorkCommand>(scope).ApplyWork(order.orderId, doctor,
                order.requiredWork, out bool repeatedCompleted, out DomainFailure repeatedFailure);
            Check(found && restoredOrder.state == SurgeryOrderState.Failed
                    && restoredOrder.resultRolled && !restoredOrder.resultSucceeded
                    && restoredOrder.outcomeConsequencesApplied && restoredOrder.outcomeConsequenceStep == 2
                    && restoredOrder.completionDoctorId == terminal.Character.Value
                    && restoredOrder.completionAbsoluteDay == terminal.AbsoluteDay
                    && W01CompletionAcknowledged(scope, restoredOrder) && restoredOrder.completionFactDelivered
                    && !applied && !repeatedCompleted && repeatedFailure.Code == FailureCode.SurgeryOrderMissing
                    && restoredPatientBefore == patientBefore
                    && W01PatientClinicalFingerprint() == patientBefore
                    && Resolve<IRandomStreamProvider>(scope).Get("medical:surgery-outcomes").State == randomBefore,
                "W01_CLINICAL_FAILURE_RESTORED_TERMINAL_NO_REROLL",
                $"found={found}; state={restoredOrder?.state}; workRetry={applied}/{repeatedCompleted}/{repeatedFailure.Code}; patientUnchanged={restoredPatientBefore == patientBefore}");
        }
        events.Publish(terminal);
        List<GameplayOutcomeSnapshot> afterRestore = W01ExactOutcomes(scope);
        GameplayOutcomeSnapshot[] restoredIdentities = W01IdentityOutcomes(afterRestore, terminal);
        GameplayOutcomeSnapshot[] restoredChildren = afterRestore.Where(value =>
            W01ResultKey(value) == childKey.ToString()).ToArray();
        Check(freshActors && restoredIdentities.Length == 1
                && W01IdentityFingerprint(restoredIdentities) == identityBefore
                && W01CursorFingerprint(deliveries) == cursorBefore
                && W01WorkerIdentityFingerprint(scope, doctor) == restoredWorkerBefore
                && W01PatientClinicalFingerprint() == restoredPatientBefore
                && Resolve<IRandomStreamProvider>(scope).Get("medical:surgery-outcomes").State == randomBefore
                && restoredChildren.Length == 1
                && restoredChildren[0].immutablePayloadHash == childBefore
                && CountPhysicalItem(StandardMedicineItemId) == medicineBefore
                && CountPhysicalItem(CleanWaterItemId) == waterBefore,
            "W01_SURGERY_RESTORED_REDELIVERY_NO_DUPLICATE",
            "freshActors=" + freshActors + "; identities=" + restoredIdentities.Length
                + "; children=" + restoredChildren.Length
                + "; workerEffectsUnchanged=" + (W01WorkerIdentityFingerprint(scope, doctor) == restoredWorkerBefore)
                + "; cursorUnchanged=" + (W01CursorFingerprint(deliveries) == cursorBefore));
        if (SurgeryPlayModeVerifier.IsW01ClinicalFailureVerification)
            W01WriteWorldEvidence("w01-surgery-clinical-failed-restored-world.json",
                gameSave.ToJson(gameSave.Capture(), true));
        WorkTerminalIdentityOwnershipDebugScenarios.RunOrThrow();
        Check(WorkCompletionIdentityOutcomeDebugScenarios.RunAll(),
            "W01_BOUNDED_TERMINAL_DELIVERY_REGRESSION",
            "separate ownership/reference/cursor/conflict/JSON fixture; not actual AI evidence");
    }

    private static bool W01TryRejectDelivery(IGameEventBus events,
        WorkCompletedIdentityEvent gameEvent, out string failure)
    {
        try
        {
            events.Publish(gameEvent);
            failure = "accepted-invalid-delivery";
            return false;
        }
        catch (InvalidOperationException error)
        {
            failure = error.Message;
            return true;
        }
    }

    private static string W01WorkerIdentityFingerprint(
        DungeonRuntimeLifetimeScope scope, CharacterActor worker)
    {
        CanonicalSemanticDigestBuilder digest = new();
        foreach (CharacterIdentityRuntimeStateSaveData state in
                 Resolve<CharacterIdentityStateStore>(scope).Capture())
            digest.Append(JsonUtility.ToJson(state));
        digest.Append(JsonUtility.ToJson(worker.Progression.CapturePersistentState().NarrativeLedger));
        digest.Append(JsonUtility.ToJson(worker.Lifecycle.ExpeditionRecovery));
        CharacterMoodSnapshot mood = worker.Stats.GetMoodSnapshot();
        digest.AppendFloat(mood.Value);
        digest.AppendFloat(mood.BaseValue);
        foreach (CharacterMoodFactorSnapshot factor in mood.Factors.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            digest.Append(factor.Id);
            digest.AppendFloat(factor.Value);
            digest.AppendFloat(factor.RemainingSeconds);
        }
        return digest.ComputeSha256();
    }

    private static List<GameplayOutcomeSnapshot> W01ExactOutcomes(
        DungeonRuntimeLifetimeScope scope)
    {
        IGameplayOutcomeQuery query = Resolve<IGameplayOutcomeQuery>(scope);
        List<GameplayOutcomeSnapshot> result = new();
        OutcomeCursor cursor = OutcomeCursor.FirstPage(200);
        while (true)
        {
            GameplayOutcomeQueryPage page = query.GetGlobal(cursor, OutcomeFilter.All);
            if (page.Items.Count == 0)
                return result;
            result.AddRange(page.Items.Where(value => value.Exact != null)
                .Select(value => value.Exact));
            cursor = page.NextCursor;
        }
    }

    private static GameplayOutcomeSnapshot[] W01IdentityOutcomes(
        IEnumerable<GameplayOutcomeSnapshot> outcomes,
        WorkCompletedIdentityEvent terminal) => outcomes.Where(value =>
            value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
            && value.participants.Any(participant =>
                participant.entityId == terminal.Character.Value)
            && W01FactEquals(value, WorkCompletionIdentityOutcomeIds.WorkIdFact,
                terminal.WorkId)).ToArray();

    private static bool W01FactEquals(GameplayOutcomeSnapshot outcome,
        GameplayOutcomeFactId fact, string value) =>
        outcome.facts.Any(entry => entry.factId == fact.Value && entry.value == value);

    private static string W01ResultKey(GameplayOutcomeSnapshot value) =>
        new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId),
            value.commitRevision, value.localResultIndex).ToString();

    private static string W01IdentityFingerprint(IEnumerable<GameplayOutcomeSnapshot> values) =>
        string.Join("|", values.OrderBy(W01ResultKey, StringComparer.Ordinal)
            .Select(value => W01ResultKey(value) + ":" + value.immutablePayloadHash));

    private static string W01CursorFingerprint(WorkCompletionIdentityDeliveryLedger deliveries) =>
        string.Join("|", deliveries.Capture().Select(value =>
            value.producerStreamId + ":" + value.operationSequence + ":"
            + value.deliveryId + ":" + value.payloadFingerprint + ":" + value.disposition));

    // Observe the single delivery authority, not duplicated flags on the surgery owner.
    private static WorkCompletionIdentityDeliveryCursorSaveData W01CompletionCursor(
        DungeonRuntimeLifetimeScope scope, SurgeryOrder order)
    {
        if (order == null || !order.resolutionFactAccepted) return null;
        var request = SurgeryWorkCompletionFactMapping.Capture(new SurgeryWorkCompletedFact(order.resolvedFact));
        return Resolve<WorkCompletionIdentityDeliveryLedger>(scope).Capture().SingleOrDefault(value =>
            value.producerStreamId == request.ProducerStreamId
            && value.operationSequence == request.OperationSequence && value.deliveryId == request.DeliveryId
            && value.payloadFingerprint == request.PayloadFingerprint);
    }

    private static bool W01CompletionAcknowledged(DungeonRuntimeLifetimeScope scope, SurgeryOrder order) =>
        W01CompletionCursor(scope, order)?.disposition is
            WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome
            or WorkCompletionIdentityDeliveryDisposition.TerminalRecipientUnavailable;

    private static bool W01CompletionNotificationAttempted(DungeonRuntimeLifetimeScope scope, SurgeryOrder order) =>
        W01CompletionCursor(scope, order)?.notificationAttempted == true;

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private bool Check(
        bool condition,
        string key,
        string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {key} {detail}");
        if (!condition)
        {
            failures.Add($"{key}: {detail}");
        }

        return condition;
    }

    private void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            capturedErrors.Add(condition + "\n" + stackTrace);
        }
        else if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
    }

    private void Finish()
    {
        if (finishing)
        {
            return;
        }

        finishing = true;
        CurrentStage = "finishing";
        Cleanup();
        Application.logMessageReceived -= OnLogMessageReceived;
        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        if (SurgeryPlayModeVerifier.IsW01PublicationRecoveryVerification)
            report.Add("injectedCommandAckFaults=" + commandAckFaultCount
                + "; injectedNotificationFaults=" + publicationFaultCount
                + "; unhandledErrors=" + capturedErrors.Count);
        bool passed = failures.Count == 0
            && capturedErrors.Count == 0
            && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        if (SurgeryPlayModeVerifier.IsW05IdentityVerification)
        {
            using FileStream stream = new(
                SurgeryPlayModeVerifier.ReportPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            using StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false));
            writer.Write(string.Join("\n", report));
            writer.Write('\n');
        }
        else
        {
            File.WriteAllText(
                SurgeryPlayModeVerifier.ReportPath,
                string.Join("\n", report));
        }
        File.Delete(SurgeryPlayModeVerifier.RequestPath);

        if (passed)
        {
            Debug.Log(
                "Surgery PlayMode verification passed. "
                + SurgeryPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError(
                "Surgery PlayMode verification failed. "
                + SurgeryPlayModeVerifier.ReportPath);
        }

        if (SurgeryPlayModeVerifier.IsW05IdentityVerification)
        {
            DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
            EditorApplication.delayCall += () =>
                EditorApplication.Exit(passed ? 0 : 1);
        }
        else
        {
            EditorApplication.ExitPlaymode();
        }
        Destroy(gameObject);
    }

    private void Cleanup()
    {
        identitySubscription?.Dispose();
        identitySubscription = null;
        publicationFaultSubscription?.Dispose();
        publicationFaultSubscription = null;
        if (completionCommandField != null && originalCompletionCommand != null && completionObserver != null)
            completionCommandField.SetValue(completionObserver, originalCompletionCommand);
        completionObserver = null;
        originalCompletionCommand = null;
        if (patient != null)
        {
            patient.SetAiPaused(originalPatientAiPaused);
            if (!string.IsNullOrWhiteSpace(targetNodeId))
            {
                anatomy?.TryHealNode(patient, targetNodeId, 100f, 100f);
            }
        }

        if (doctorWork != null)
        {
            doctorWork.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Surgery,
                originalSurgeryPriority);
            doctorWork.SetDutyState(originalDutyState);
        }

        DungeonGameRestoreReport restoreReport = null;
        if (gameSnapshot != null
            && (gameSave == null
                || !gameSave.TryRestore(
                    gameSnapshot,
                    out restoreReport)))
        {
            capturedErrors.Add(
                "Surgery verifier baseline restore failed: "
                + (restoreReport == null
                    ? "missing restore report"
                    : string.Join(" | ", restoreReport.Errors)));
        }

        foreach (GameObject obj in temporaryObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        temporaryObjects.Clear();
        if (gameplayCamera != null)
        {
            gameplayCamera.transform.position = originalCameraPosition;
        }

        Time.timeScale = originalTimeScale;
        Application.runInBackground = originalRunInBackground;
    }

    private static string Compact(IEnumerable<string> values)
    {
        return Compact(string.Join(" | ", values ?? Array.Empty<string>()));
    }

    private static string Compact(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "<none>"
            : value.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
#endif
