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
    public const string ReportPath = "Artifacts/QA/surgery-playmode-report.txt";
    public const string CapturePath = "Artifacts/QA/surgery-playmode.png";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;

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
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.Delete(CapturePath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
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
    private const float NoProgressTimeoutSeconds = 60f;
    private const float OverallTimeoutSeconds = 180f;
    private const float VerifierHardTimeoutSeconds = 600f;

    private readonly List<string> report = new();
    private readonly List<string> failures = new();
    private readonly List<string> capturedErrors = new();
    private readonly List<string> capturedWarnings = new();
    private readonly List<GameObject> temporaryObjects = new();
    private readonly HashSet<SurgeryOrderState> observedStates = new();

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

    public string CurrentStage { get; private set; } = "created";
    public float ElapsedRealtimeSeconds =>
        verifierStartedAt > 0f
            ? Time.realtimeSinceStartup - verifierStartedAt
            : 0f;

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
        Time.timeScale = 8f;
        EnsureEventSystem();

        yield return null;
        yield return null;

        DungeonRuntimeLifetimeScope scope = FindScope();
        surgery = Resolve<ISurgeryQuery>(scope);
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
        ISurgeryPolicyRuntime surgeryPolicies =
            Resolve<ISurgeryPolicyRuntime>(scope);
        randomStreams?.Get("medical:surgery-outcomes").Restore(1UL);
        GridSystemManager gridSystem =
            UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = gridSystem != null ? gridSystem.grid : null;

        Check(scope?.Container != null, "SCOPE_READY", "gameplay LifetimeScope resolved");
        Check(surgery != null && commands != null, "SURGERY_RUNTIME_READY", "runtime and commands resolved");
        Check(procedures != null && facilities != null, "SURGERY_CATALOG_READY", "catalog and facility query resolved");
        Check(rooms != null && grid != null, "SURGERY_GRID_READY", "room cache and grid resolved");
        Check(research != null && characters != null, "SURGERY_WORLD_READY", "research and character query resolved");
        Check(
            anatomy != null
                && items != null
                && gameSave != null
                && destinationClaims != null,
            "SURGERY_DATA_READY",
            "anatomy, item runtime, destination claim, and V18 save service resolved");
        Check(surgeryWindow != null, "SURGERY_UI_READY", "surgery planning window resolved");
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        CurrentStage = "ensure-playable-run";
        yield return EnsurePlayableRun();
        CurrentStage = "baseline-surgery-live-ai";
        // Starting a prepared run restores the player's saved speed. Reapply the
        // verification speed after that transition so physical hauling and work
        // are observed without making the test depend on editor focus.
        Time.timeScale = 8f;
        CharacterActor[] workers = characters.Characters
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.characterType == CharacterType.NPC)
            .OrderByDescending(actor => actor.Stats.EvaluatePerformance(
                "performance:medical:surgery-success").Value)
            .ToArray();
        doctor = workers.FirstOrDefault();
        patient = workers.Skip(1).FirstOrDefault() ?? workers.FirstOrDefault();
        Check(doctor != null && patient != null, "SURGERY_ACTORS_READY",
            doctor != null && patient != null
                ? $"doctor={doctor.Identity?.DisplayName}; patient={patient.Identity?.DisplayName}"
                : "doctor or patient missing");
        if (doctor == null || patient == null)
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
            Check(TryFindPlacement(layout, grid, tableAsset, out Vector2Int tablePosition, out Vector2Int supplyPosition),
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
                18f,
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
            order.risk.successChance = 1f;
            bool pipedProcessWaterRouteExists = requiresProcessWater
                && fluidQuery != null
                && fluidQuery.TryGetNetwork(table, out _);

            float startedAt = Time.realtimeSinceStartup;
            float nextActorStabilizationAt = startedAt;
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
            while (Time.realtimeSinceStartup < overallDeadline
                   && Time.realtimeSinceStartup < noProgressDeadline
                   && order.IsActive)
            {
                if (!string.IsNullOrWhiteSpace(order.doctorId)
                    && order.risk != null)
                {
                    // The runtime recalculates risk when the surgeon reserves the
                    // order. Pin only the final QA outcome after that reservation;
                    // movement, hauling, materials, and work remain fully real.
                    order.risk.successChance = 1f;
                }

                if (Time.timeScale <= 0f)
                {
                    Time.timeScale = 8f;
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
                    gridSystem = UnityEngine.Object.FindFirstObjectByType<
                        GridSystemManager>();
                    grid = gridSystem != null ? gridSystem.grid : null;
                    doctorWork = doctor?.GetAbility<AbilityWork>();

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
                    bool materialConserved = CountPhysicalItem(medicineId)
                            == savedMedicine
                        && CountPhysicalItem(CleanWaterItemId) == savedWater
                        && restoredPatientOrders == 1
                        && order != null
                        && items.GetCommittedHaulDeliveryQuantity(
                            order.materialDestinationId,
                            medicineId) == 0
                        && !items.GetAllStacks().Any(stack => stack != null
                            && string.Equals(
                                stack.DestinationId,
                                order.materialDestinationId,
                                StringComparison.Ordinal));
                    Check(
                        materialConserved && restoredClaimExact,
                        "SURGERY_CURRENT_RESTORE_MATERIAL_CONSERVATION",
                        $"medicine={savedMedicine}->{CountPhysicalItem(medicineId)}; "
                        + $"water={savedWater}->{CountPhysicalItem(CleanWaterItemId)}; "
                        + $"patientOrders={restoredPatientOrders}; claim={restoredClaimExact}");

                    restoredActionStartBaseline = restoredGate.ActionStarts;
                    currentFormatRestorePerformed = true;
                    doctor.Brain.PreferWorkActionOnNextDecision(
                        BuiltInWorkTypeIds.Surgery,
                        120f);
                    doctor.Brain.RequestImmediateReplan(clearFailures: true);
                    Time.timeScale = 8f;
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

                bool madeAuthoritativeProgress =
                    order.state != lastProgressState
                    || order.completedWork > lastProgressWork + 0.0001f
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
                yield return null;
            }

            observedStates.Add(order.state);
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
            Check(!order.IsActive && order.state == SurgeryOrderState.Completed,
                "SURGERY_COMPLETED_BY_WORK_AI",
                $"state={order.state}; status={order.statusData?.code}; elapsed={Time.realtimeSinceStartup - startedAt:0.0}s; noProgress={Time.realtimeSinceStartup - lastAuthoritativeProgressAt:0.0}s; overallLimit={OverallTimeoutSeconds:0}s; noProgressLimit={NoProgressTimeoutSeconds:0}s");
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
            Check(remainingMedicine < initialMedicine, "SURGERY_PHYSICAL_MEDICINE_CONSUMED",
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
            Check(healedHealth > injuredHealth, "SURGERY_PATIENT_RECOVERED",
                $"{injuredHealth:0.##}->{healedHealth:0.##}");
            bool fullyHealedForIsolation = anatomy.TryHealNode(
                patient,
                targetNodeId,
                100f,
                100f);
            Check(
                fullyHealedForIsolation,
                "SURGERY_POST_COMPLETION_TEST_ISOLATION",
                $"patient={patient.Identity?.PersistentId}; node={targetNodeId}");
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
        while (packagedOrder.IsActive
               && Time.realtimeSinceStartup < deadline)
        {
            if (packagedOrder.risk != null)
            {
                packagedOrder.risk.successChance = 1f;
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
                Time.timeScale = 8f;
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
                     .OrderByDescending(candidate => candidate.Cells.Count))
        {
            HashSet<Vector2Int> roomCells = new(room.Cells);
            foreach (Vector2Int cell in room.Cells.OrderBy(position => position.x))
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
        bool passed = failures.Count == 0
            && capturedErrors.Count == 0
            && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        File.WriteAllText(
            SurgeryPlayModeVerifier.ReportPath,
            string.Join("\n", report));
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

        EditorApplication.ExitPlaymode();
        Destroy(gameObject);
    }

    private void Cleanup()
    {
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
