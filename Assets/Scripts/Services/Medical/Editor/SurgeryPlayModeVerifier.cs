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
    private const string CleanWaterItemId = "resource:clean-water";
    private const string SutureProcedureId = "procedure:emergency-suture";
    private const string MedicalResearchId = "research:survival:medical";
    private const float NoProgressTimeoutSeconds = 60f;
    private const float OverallTimeoutSeconds = 180f;

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

    private IEnumerator Start()
    {
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

        yield return EnsurePlayableRun();
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
            bool waterSpawned = items.SpawnItemAt(
                CleanWaterItemId,
                1,
                table.centerPos,
                WorldItemStackState.FacilityBuffer,
                processWaterDestinationId,
                out int spawnedWater);
            Check(
                waterSpawned && spawnedWater == 1,
                "PROCESS_WATER_SPAWNED",
                $"item={CleanWaterItemId}; amount={spawnedWater}; position={table.centerPos}; destination={processWaterDestinationId}");

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
                        && string.Equals(
                            order.doctorId,
                            liveDoctorId,
                            StringComparison.Ordinal)
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
            Check(
                order.processFluidConsumed && remainingWater < initialWater,
                "SURGERY_PHYSICAL_PROCESS_WATER_CONSUMED",
                $"consumed={order.processFluidConsumed}; water={initialWater}->{remainingWater}");
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
        Finish();
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
