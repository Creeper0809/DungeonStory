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
public static class WimTransfusionTargetedHemostasisDebugScenarios
{
    public const string RequestPath =
        "Temp/wim-021-022-medical-effects.request";
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-021-022-medical-effects.txt";
    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;

    static WimTransfusionTargetedHemostasisDebugScenarios()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem(
        "DungeonStory/Debug/Medical/Request WIM-021-022 Verification")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        File.Delete(ReportPath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(
                GameplayScenePath,
                OpenSceneMode.Single);
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
        new GameObject("WIM-021-022 Medical Effects Verification Runner")
            .AddComponent<WimTransfusionTargetedHemostasisRunner>();
    }
}

public sealed class WimTransfusionTargetedHemostasisRunner : MonoBehaviour
{
    private const string SutureProcedureId =
        "procedure:emergency-suture";
    private const string TransfusionProcedureId =
        "procedure:blood-transfusion";
    private const string MedicalResearchId =
        "research:survival:medical";
    private const string CleanWaterItemId =
        "resource:clean-water";
    private const float TimeoutSeconds = 30f;

    private readonly List<string> report = new();
    private readonly List<string> failures = new();
    private readonly List<string> capturedErrors = new();
    private readonly List<GameObject> temporaryObjects = new();

    private DungeonGameSaveData baseline;
    private IDungeonGameSaveService gameSave;
    private ICharacterWorldQuery characters;
    private ICharacterBodyHealthQuery bodyHealth;
    private ICharacterBodyHealthCommand bodyCommands;
    private IAnatomyHealthRuntime anatomy;
    private IEnvironmentalFieldQuery environmentalField;
    private ISurgeryQuery surgery;
    private ISurgeryCommandService commands;
    private ISurgeryWorkCommand work;
    private ISurgicalFacilityQuery facilities;
    private IWorldItemStackRuntime items;
    private ICharacterDeprivationRuntime deprivation;
    private ICharacterAiWorldRegistry worldRegistry;
    private SurgeryRuntime surgeryRuntime;
    private CharacterActor doctor;
    private CharacterActor patient;
    private Facility table;
    private string doctorId = string.Empty;
    private string patientId = string.Empty;
    private string targetNodeId = string.Empty;
    private string otherNodeId = string.Empty;
    private float originalTimeScale;
    private bool originalRunInBackground;
    private bool finishing;

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        Application.logMessageReceived += OnLogMessageReceived;
        originalTimeScale = Time.timeScale;
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        Time.timeScale = 8f;

        yield return null;
        yield return null;
        yield return EnsurePlayableRun();

        DungeonRuntimeLifetimeScope scope = FindScope();
        surgery = Resolve<ISurgeryQuery>(scope);
        commands = Resolve<ISurgeryCommandService>(scope);
        work = Resolve<ISurgeryWorkCommand>(scope);
        facilities = Resolve<ISurgicalFacilityQuery>(scope);
        items = Resolve<IWorldItemStackRuntime>(scope);
        characters = Resolve<ICharacterWorldQuery>(scope);
        bodyHealth = Resolve<ICharacterBodyHealthQuery>(scope);
        bodyCommands = Resolve<ICharacterBodyHealthCommand>(scope);
        anatomy = Resolve<IAnatomyHealthRuntime>(scope);
        gameSave = Resolve<IDungeonGameSaveService>(scope);
        deprivation = Resolve<ICharacterDeprivationRuntime>(scope);
        worldRegistry = Resolve<ICharacterAiWorldRegistry>(scope);
        environmentalField = Resolve<IEnvironmentalFieldQuery>(scope);
        surgeryRuntime = surgery as SurgeryRuntime;
        IAnatomyProfileCatalog anatomyProfiles =
            Resolve<IAnatomyProfileCatalog>(scope);
        ISurgicalProcedureCatalog procedures =
            Resolve<ISurgicalProcedureCatalog>(scope);
        IRoomLayoutCache rooms = Resolve<IRoomLayoutCache>(scope);
        IBlueprintResearchStateService research =
            Resolve<IBlueprintResearchStateService>(scope);
        ICharacterProficiencyCommand proficiencies =
            Resolve<ICharacterProficiencyCommand>(scope);
        IGameCalendar calendar = Resolve<IGameCalendar>(scope);
        ISurgeryPolicyRuntime policies = Resolve<ISurgeryPolicyRuntime>(scope);
        IGridBuildingObjectFactory buildingFactory =
            Resolve<IGridBuildingObjectFactory>(scope);
        IEnvironmentalFieldCommand environmentalCommands =
            Resolve<IEnvironmentalFieldCommand>(scope);
        ISurvivalRefuelCompletionCommand refuelCommands =
            Resolve<ISurvivalRefuelCompletionCommand>(scope);
        ISurvivalRefuelSupplyQuery refuelSupply =
            Resolve<ISurvivalRefuelSupplyQuery>(scope);
        ISurvivalEnvironmentQuery survivalEnvironment =
            Resolve<ISurvivalEnvironmentQuery>(scope);
        GridSystemManager gridSystem =
            UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = gridSystem != null ? gridSystem.grid : null;

        Check(scope?.Container != null, "RUNTIME_SCOPE", "scope resolved");
        Check(
            surgeryRuntime != null
                && commands != null
                && work != null
                && facilities != null,
            "SURGERY_PRODUCTION_BOUNDARY",
            "query, command, work command, and tick runtime resolved");
        Check(
            items != null
                && characters != null
                && bodyHealth != null
                && bodyCommands != null
                && anatomy != null
                && gameSave != null
                && environmentalField != null,
            "MEDICAL_AUTHORITIES",
            "physical items, body/anatomy, environment, character world, and save resolved");
        Check(
            procedures != null
                && anatomyProfiles != null
                && rooms != null
                && research != null
                && proficiencies != null
                && calendar != null
                && grid != null
                && worldRegistry != null
                && buildingFactory != null
                && environmentalCommands != null
                && refuelCommands != null
                && refuelSupply != null
                && survivalEnvironment != null,
            "FIXTURE_AUTHORITIES",
            "catalog, research, proficiency, room, grid, building, fuel, and environment fixtures resolved");
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        CharacterActor[] workers = characters.Characters
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.characterType == CharacterType.NPC)
            .ToArray();
        patient = workers.FirstOrDefault(actor =>
        {
            AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(actor);
            HashSet<string> presentNodeIds = new(
                snapshot.Nodes
                    .Where(node => node != null && !node.missing)
                    .Select(node => node.nodeId),
                StringComparer.Ordinal);
            return anatomyProfiles.TryGet(
                    snapshot.ProfileId,
                    out AnatomyProfileDefinition profile)
                && !string.Equals(
                    profile.AnatomyFamily,
                    "construct",
                    StringComparison.OrdinalIgnoreCase)
                && profile.Nodes.Count(node =>
                    !node.Vital && presentNodeIds.Contains(node.NodeId)) >= 2;
        });
        doctor = workers.FirstOrDefault(actor => !ReferenceEquals(actor, patient));
        Check(
            patient != null && doctor != null,
            "BIOLOGICAL_PATIENT_AND_DOCTOR",
            patient != null && doctor != null
                ? $"patient={patient.Identity?.PersistentId}; doctor={doctor.Identity?.PersistentId}"
                : $"npc-count={workers.Length}");
        if (patient == null || doctor == null)
        {
            Finish();
            yield break;
        }

        patientId = patient.Identity?.PersistentId ?? string.Empty;
        doctorId = doctor.Identity?.PersistentId ?? string.Empty;
        baseline = gameSave.Capture();
        research.GetState().Projects.RestoreCompleted(
            new ResearchProjectId(MedicalResearchId));
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
        foreach (CharacterActor actor in workers)
        {
            StabilizeActor(actor);
        }

        SurgicalSubjectRef subject = CreateSubject(patient);
        policies?.SetAutomaticEmergencySurgery(subject, false);

        Check(
            procedures.TryGet(
                SutureProcedureId,
                out SurgicalProcedureSO suture)
                && suture.Effects.Count == 2
                && suture.Effects.OfType<HealSurgicalNodeEffect>().SingleOrDefault()
                    is { health: 8f, infectionReduction: 8f }
                && suture.Effects.OfType<StopSurgicalNodeBleedingEffect>()
                    .Count() == 1,
            "SUTURE_AUTHORED_EFFECTS",
            "heal 8/infection 8 plus exact-node hemostasis");
        Check(
            procedures.TryGet(
                TransfusionProcedureId,
                out SurgicalProcedureSO transfusion)
                && !transfusion.AllowsWildlife
                && transfusion.Effects.Count == 2
                && transfusion.Effects.OfType<HealSurgicalNodeEffect>()
                    .SingleOrDefault()
                    is { health: 14f, infectionReduction: 2f }
                && transfusion.Effects.OfType<RecoverBloodLossEffect>()
                    .SingleOrDefault()
                    is { amount: 25f },
            "TRANSFUSION_AUTHORED_EFFECTS",
            "character-only heal 14/infection 2 plus blood-loss recovery 25");
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        BuildingSO tableAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Medical/M01_응급처치대.asset");
        RoomLayout layout = rooms.GetLayout(grid);
        bool foundPlacement = TryFindPlacement(
            layout,
            grid,
            tableAsset,
            out Vector2Int tablePosition);
        Check(
            tableAsset != null && foundPlacement,
            "EMERGENCY_TABLE_PLACEMENT",
            $"asset={tableAsset != null}; position={tablePosition}");
        if (tableAsset == null || !foundPlacement)
        {
            Finish();
            yield break;
        }

        table = CreateInjectedFacility(
            scope,
            grid,
            tableAsset,
            tablePosition);
        bool registered = table != null
            && grid.RegisterOccupant(
                table,
                tableAsset.Placement.Layer,
                table.buildPoses,
                false);
        rooms.Clear();
        bool tableVisible = table != null
            && worldRegistry.Buildings.Contains(table);
        Check(
            registered && tableVisible,
            "EMERGENCY_TABLE_PRODUCTION_VISIBLE",
            $"registered={registered}; world={tableVisible}");
        SurgicalFacilitySnapshot tableState = registered
            ? facilities.Evaluate(table, suture.RequiredFacilityTags)
            : default;
        Check(
            tableState.IsAvailable,
            "EMERGENCY_TABLE_AVAILABLE",
            tableState.BlockFailure.Code.ToString());
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        BuildingSO torchAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/E01_벽횃불.asset");
        BuildableObject torch = CreateInjectedBuilding(
            scope,
            buildingFactory,
            grid,
            torchAsset,
            tablePosition);
        bool torchRegistered = torch != null
            && grid.RegisterOccupant(
                torch,
                torchAsset.Placement.Layer,
                torchAsset.GetGridPosList(tablePosition),
                torchAsset.Placement.IsMovement);
        environmentalCommands.MarkTopologyDirty();

        int requiredFuel = torchAsset
            ?.GetAbility<BuildingFuelConsumerAbility>()
            ?.fuelPerRefuel ?? 0;
        // Controlled lighting preparation, not evidence of natural hauling:
        // publish the real facility input owner, then create an arrived lot at
        // its public destination. Remote Stored stock cannot refuel this torch.
        refuelCommands.TryEnsureRefuelSupply(
            null, torch, out _, out DomainFailure fuelPreparationFailure);
        bool fuelPlanFound = refuelSupply.TryGetRefuelSupplyPlan(
            torch, out SurvivalFacilityFuelSupplyPlan fuelPlan);
        bool fuelSpawned = fuelPlanFound
            && requiredFuel > 0
            && fuelPlan.RequiredQuantity == requiredFuel
            && items.SpawnItemAt(
                fuelPlan.ItemId,
                fuelPlan.RequiredQuantity,
                torch.centerPos,
                WorldItemStackState.FacilityBuffer,
                fuelPlan.DestinationId,
                out int spawnedFuel)
            && spawnedFuel == requiredFuel;

        int consumedFuel = 0;
        DomainFailure refuelFailure = DomainFailure.None;
        bool refueled = fuelSpawned
            && refuelCommands.TryEnsureRefuelSupply(
                null, torch, out _, out refuelFailure)
            && refuelCommands.TryApplyRefuelWork(
                null,
                torch,
                out consumedFuel,
                out refuelFailure)
            && consumedFuel == requiredFuel;

        EnvironmentalCellSnapshot surgeryEnvironment = default;
        float environmentDeadline = Time.realtimeSinceStartup + 5f;
        while (torchRegistered
               && refueled
               && Time.realtimeSinceStartup < environmentDeadline
               && (!environmentalField.TryGetCell(
                       table.centerPos,
                       out surgeryEnvironment)
                   || !SurgeryEnvironmentRiskEvaluator.IsNormalEnvironment(
                       surgeryEnvironment)))
        {
            yield return null;
        }

        bool environmentReady = environmentalField.TryGetCell(
                table.centerPos,
                out surgeryEnvironment)
            && SurgeryEnvironmentRiskEvaluator.IsNormalEnvironment(
                surgeryEnvironment);
        Check(
            torchAsset != null
                && torchAsset.GetAbility<BuildingLightingAbility>()
                    is { intensity: 0.75f }
                && torchRegistered
                && fuelSpawned
                && refueled
                && survivalEnvironment.HasFuelSupply(torch)
                && environmentReady,
            "AUTHORED_SURGERY_LIGHTING_FIXTURE",
            $"fixture-only E01; registered={torchRegistered}; fuelSpawned={fuelSpawned}; "
            + $"fuelDestination={fuelPlan.DestinationId}; controlled-arrived-lot=true; "
            + $"preparationFailure={fuelPreparationFailure.Code}; "
            + $"refueled={refueled}; consumed={consumedFuel}; failure={refuelFailure.Code}; "
            + $"supplied={survivalEnvironment.HasFuelSupply(torch)}; "
            + DescribeEnvironment(surgeryEnvironment));
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        AnatomyHealthSnapshot initialAnatomy =
            anatomy.GetAnatomySnapshot(patient);
        Check(
            anatomyProfiles.TryGet(
                initialAnatomy.ProfileId,
                out AnatomyProfileDefinition patientProfile),
            "PATIENT_ANATOMY_PROFILE",
            initialAnatomy.ProfileId);
        HashSet<string> presentPatientNodeIds = new(
            initialAnatomy.Nodes
                .Where(node => node != null && !node.missing)
                .Select(node => node.nodeId),
            StringComparer.Ordinal);
        AnatomyNodeDefinition[] selectedNodes = patientProfile?.Nodes
            .Where(node =>
                !node.Vital && presentPatientNodeIds.Contains(node.NodeId))
            .Take(2)
            .ToArray() ?? Array.Empty<AnatomyNodeDefinition>();
        Check(
            selectedNodes.Length == 2,
            "TWO_EXISTING_NONVITAL_NODES",
            string.Join(",", selectedNodes.Select(node => node.NodeId)));
        if (selectedNodes.Length != 2)
        {
            Finish();
            yield break;
        }

        targetNodeId = selectedNodes[0].NodeId;
        otherNodeId = selectedNodes[1].NodeId;
        StopAllBleeding(patient);
        anatomy.TryHealNode(patient, targetNodeId, 1000f, 1000f);
        anatomy.TryHealNode(patient, otherNodeId, 1000f, 1000f);

        SetBloodLoss(patient, 30f);
        bool cancelWoundApplied = anatomy.TryDamageNode(
            patient,
            targetNodeId,
            1f,
            0.23f,
            "wim-021-cancel-setup");
        anatomy.TryHealNode(patient, targetNodeId, 1f, 0f);
        float cancelBloodBefore = bodyHealth.GetSnapshot(patient).BloodLoss;
        float cancelBleedBefore = GetNode(patient, targetNodeId)
            .bleedingPerSecond;
        Check(
            cancelWoundApplied && cancelBleedBefore > 0f,
            "CANCEL_ACTIVE_WOUND_PRECONDITION",
            $"applied={cancelWoundApplied}; bleed={cancelBleedBefore:0.###}");
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        bool cancelScheduled = commands.TrySchedule(
            subject.Clone(),
            TransfusionProcedureId,
            targetNodeId,
            string.Empty,
            doctor.Identity?.PersistentId,
            facilities.GetFacilityId(table),
            out SurgeryOrder cancelOrder,
            out DomainFailure cancelScheduleFailure);
        DomainFailure cancelFailure = DomainFailure.None;
        bool cancelled = cancelScheduled
            && commands.TryCancel(
                cancelOrder.orderId,
                out cancelFailure);
        Check(
            cancelScheduled && cancelled,
            "TRANSFUSION_CANCELLED_PRE_COMPLETION",
            cancelScheduled
                ? $"cancelled={cancelled}; failure={cancelFailure.Code}"
                : $"schedule={cancelScheduleFailure.Code}");
        CheckApprox(
            bodyHealth.GetSnapshot(patient).BloodLoss,
            cancelBloodBefore,
            "CANCEL_PRESERVES_BLOOD_LOSS");
        CheckApprox(
            GetNode(patient, targetNodeId).bleedingPerSecond,
            cancelBleedBefore,
            "CANCEL_PRESERVES_NODE_BLEEDING");
        yield return WaitForTerminal(
            cancelOrder,
            SurgeryOrderState.Cancelled);
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        StopAllBleeding(patient);
        anatomy.TryHealNode(patient, targetNodeId, 1000f, 1000f);
        anatomy.TryHealNode(patient, otherNodeId, 1000f, 1000f);
        anatomy.TryDamageNode(
            patient,
            targetNodeId,
            18f,
            0.41f,
            "wim-022-target-setup");
        anatomy.TryAddNodeBurden(
            patient,
            targetNodeId,
            0f,
            0f,
            12f,
            out _);
        anatomy.TryDamageNode(
            patient,
            otherNodeId,
            5f,
            0.22f,
            "wim-022-other-setup");
        anatomy.TryAddNodeBurden(
            patient,
            otherNodeId,
            0f,
            0f,
            7f,
            out _);
        SetBloodLoss(patient, 45f);
        AnatomyNodeHealthState sutureTargetBefore = CloneNode(
            GetNode(patient, targetNodeId));
        AnatomyNodeHealthState sutureOtherBefore = CloneNode(
            GetNode(patient, otherNodeId));
        CharacterBodyHealthSnapshot sutureBodyBefore =
            bodyHealth.GetSnapshot(patient);
        bool sutureCompleted = TryCompleteProductionSurgery(
            subject.Clone(),
            SutureProcedureId,
            targetNodeId,
            out SurgeryOrder sutureOrder,
            out string sutureDetail);
        Check(
            sutureCompleted,
            "SUTURE_PRODUCTION_COMPLETION",
            sutureDetail);
        AnatomyNodeHealthState sutureTargetAfter =
            GetNode(patient, targetNodeId);
        AnatomyNodeHealthState sutureOtherAfter =
            GetNode(patient, otherNodeId);
        CharacterBodyHealthSnapshot sutureBodyAfter =
            bodyHealth.GetSnapshot(patient);
        CheckApprox(
            sutureTargetAfter.bleedingPerSecond,
            0f,
            "SUTURE_TARGET_BLEEDING_ZERO");
        CheckNodeEquivalent(
            sutureOtherAfter,
            sutureOtherBefore,
            "SUTURE_OTHER_NODE_INVARIANT");
        CheckApprox(
            sutureBodyAfter.BloodLoss,
            sutureBodyBefore.BloodLoss,
            "SUTURE_EXISTING_BLOOD_LOSS_INVARIANT");
        CheckApprox(
            sutureTargetAfter.currentHealth,
            Mathf.Min(
                sutureTargetBefore.maxHealth,
                sutureTargetBefore.currentHealth + 8f),
            "SUTURE_HEALTH_EFFECT_PRESERVED");
        CheckApprox(
            sutureTargetAfter.infection,
            Mathf.Max(0f, sutureTargetBefore.infection - 8f),
            "SUTURE_INFECTION_EFFECT_PRESERVED");
        if (selectedNodes[0].MapsToLegacyBodyPart)
        {
            CheckApprox(
                GetLegacyPart(
                    sutureBodyAfter,
                    selectedNodes[0].LegacyBodyPart).bleedingPerSecond,
                0f,
                "SUTURE_TARGET_LEGACY_PROJECTION_ZERO");
        }
        else
        {
            Check(
                true,
                "SUTURE_TARGET_LEGACY_PROJECTION_NOT_APPLICABLE",
                $"node={selectedNodes[0].NodeId}; no authored legacy mapping");
        }

        if (selectedNodes[1].MapsToLegacyBodyPart)
        {
            CheckApprox(
                GetLegacyPart(
                    sutureBodyAfter,
                    selectedNodes[1].LegacyBodyPart).bleedingPerSecond,
                GetLegacyPart(
                    sutureBodyBefore,
                    selectedNodes[1].LegacyBodyPart).bleedingPerSecond,
                "SUTURE_OTHER_LEGACY_PROJECTION_INVARIANT");
        }
        else
        {
            Check(
                true,
                "SUTURE_OTHER_LEGACY_PROJECTION_NOT_APPLICABLE",
                $"node={selectedNodes[1].NodeId}; no authored legacy mapping");
        }
        StopAllBleeding(patient);
        yield return WaitForTerminal(
            sutureOrder,
            SurgeryOrderState.Completed);
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        anatomy.TryHealNode(patient, targetNodeId, 1000f, 1000f);
        bool transfusionWoundApplied = anatomy.TryDamageNode(
            patient,
            targetNodeId,
            1f,
            0.31f,
            "wim-021-continuing-wound");
        anatomy.TryHealNode(patient, targetNodeId, 1f, 0f);
        SetBloodLoss(patient, 40f);
        AnatomyNodeHealthState transfusionNodeBefore = CloneNode(
            GetNode(patient, targetNodeId));
        Check(
            transfusionWoundApplied
                && transfusionNodeBefore.bleedingPerSecond > 0f,
            "TRANSFUSION_ACTIVE_WOUND_PRECONDITION",
            $"applied={transfusionWoundApplied}; "
            + $"bleed={transfusionNodeBefore.bleedingPerSecond:0.###}");
        CheckApprox(
            transfusionNodeBefore.currentHealth,
            transfusionNodeBefore.maxHealth,
            "TRANSFUSION_FULL_NODE_HP_PRECONDITION");
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        bool transfusionCompleted = TryCompleteProductionSurgery(
            subject.Clone(),
            TransfusionProcedureId,
            targetNodeId,
            out SurgeryOrder transfusionOrder,
            out string transfusionDetail);
        Check(
            transfusionCompleted,
            "TRANSFUSION_PRODUCTION_COMPLETION",
            transfusionDetail);
        if (!transfusionCompleted)
        {
            Finish();
            yield break;
        }

        AnatomyNodeHealthState transfusionNodeAfter =
            GetNode(patient, targetNodeId);
        CheckApprox(
            bodyHealth.GetSnapshot(patient).BloodLoss,
            15f,
            "TRANSFUSION_REDUCES_BLOOD_LOSS_BY_25");
        CheckApprox(
            transfusionNodeAfter.bleedingPerSecond,
            transfusionNodeBefore.bleedingPerSecond,
            "TRANSFUSION_DOES_NOT_STOP_WOUND_BLEEDING");
        CheckApprox(
            transfusionNodeAfter.currentHealth,
            transfusionNodeBefore.currentHealth,
            "TRANSFUSION_VALID_HEAL_NOOP_REACHES_BLOOD_EFFECT");
        CheckApprox(
            transfusionNodeAfter.infection,
            transfusionNodeBefore.infection,
            "TRANSFUSION_ZERO_INFECTION_REMAINS_ZERO");
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        StopAllBleeding(patient);
        float committedBloodLoss = bodyHealth.GetSnapshot(patient).BloodLoss;
        string savedOrderId = transfusionOrder.orderId;
        DungeonGameSaveData recoveringSave = gameSave.Capture();
        bool restored = gameSave.TryRestore(
            recoveringSave,
            out DungeonGameRestoreReport restoreReport);
        Check(
            restored,
            "RESULT_ROLLED_RECOVERY_RESTORE",
            restored
                ? $"order={savedOrderId}"
                : string.Join(" | ", restoreReport?.Errors
                    ?? Array.Empty<string>()));
        if (!restored)
        {
            Finish();
            yield break;
        }

        patient = characters.Characters.FirstOrDefault(actor => actor != null
            && string.Equals(
                actor.Identity?.PersistentId,
                patientId,
                StringComparison.Ordinal));
        doctor = characters.Characters.FirstOrDefault(actor => actor != null
            && string.Equals(
                actor.Identity?.PersistentId,
                doctorId,
                StringComparison.Ordinal));
        transfusionOrder = surgery.ActiveOrders.FirstOrDefault(order =>
            order != null
            && string.Equals(order.orderId, savedOrderId, StringComparison.Ordinal));
        table = worldRegistry.Buildings
            .OfType<Facility>()
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    facilities.GetFacilityId(candidate),
                    transfusionOrder?.facilityId,
                    StringComparison.Ordinal));
        Check(
            patient != null
                && doctor != null
                && transfusionOrder is
                {
                    resultRolled: true,
                    state: SurgeryOrderState.Recovering
                }
                && table != null,
            "RECOVERING_AUTHORITY_REBOUND",
            $"patient={patient != null}; doctor={doctor != null}; state={transfusionOrder?.state}; resultRolled={transfusionOrder?.resultRolled}; table={table != null}");
        if (patient == null
            || doctor == null
            || transfusionOrder is not
            {
                resultRolled: true,
                state: SurgeryOrderState.Recovering
            }
            || table == null)
        {
            Finish();
            yield break;
        }

        CheckApprox(
            bodyHealth.GetSnapshot(patient).BloodLoss,
            committedBloodLoss,
            "RESTORE_DOES_NOT_REPEAT_TRANSFUSION_EFFECT");
        yield return WaitForTerminal(
            transfusionOrder,
            SurgeryOrderState.Completed);
        Check(
            transfusionOrder?.state == SurgeryOrderState.Completed,
            "RESTORED_RECOVERY_COMPLETES",
            transfusionOrder?.state.ToString() ?? "missing");
        CheckApprox(
            bodyHealth.GetSnapshot(patient).BloodLoss,
            committedBloodLoss,
            "COMPLETION_DOES_NOT_REPEAT_TRANSFUSION_EFFECT");
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        SetBloodLoss(patient, 10f);
        anatomy.TryHealNode(patient, targetNodeId, 1000f, 1000f);
        bool clampCompleted = TryCompleteProductionSurgery(
            CreateSubject(patient),
            TransfusionProcedureId,
            targetNodeId,
            out SurgeryOrder clampOrder,
            out string clampDetail);
        Check(
            clampCompleted,
            "TRANSFUSION_CLAMP_PRODUCTION_COMPLETION",
            clampDetail);
        if (!clampCompleted)
        {
            Finish();
            yield break;
        }

        CheckApprox(
            bodyHealth.GetSnapshot(patient).BloodLoss,
            0f,
            "TRANSFUSION_CLAMPS_TO_ACTUAL_LOSS");
        yield return WaitForTerminal(
            clampOrder,
            SurgeryOrderState.Completed);
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        int activeBeforeRejection = surgery.ActiveOrders.Count;
        bool noBloodScheduled = commands.TrySchedule(
            CreateSubject(patient),
            TransfusionProcedureId,
            targetNodeId,
            string.Empty,
            doctor.Identity?.PersistentId,
            facilities.GetFacilityId(table),
            out _,
            out DomainFailure noBloodFailure);
        Check(
            !noBloodScheduled
                && noBloodFailure.Code == FailureCode.SurgeryEffectFailed
                && surgery.ActiveOrders.Count == activeBeforeRejection,
            "TRANSFUSION_NO_BLOOD_REJECTED",
            $"scheduled={noBloodScheduled}; failure={noBloodFailure.Code}; active={surgery.ActiveOrders.Count}");

        SetBloodLoss(patient, 20f);
        SurgicalSubjectRef wildlifeSubject = CreateSubject(patient);
        wildlifeSubject.kind = SurgicalSubjectKind.Wildlife;
        bool wildlifeScheduled = commands.TrySchedule(
            wildlifeSubject,
            TransfusionProcedureId,
            targetNodeId,
            string.Empty,
            doctor.Identity?.PersistentId,
            facilities.GetFacilityId(table),
            out _,
            out DomainFailure wildlifeFailure);
        Check(
            !wildlifeScheduled
                && wildlifeFailure.Code
                    == FailureCode.SurgerySubjectKindUnsupported,
            "TRANSFUSION_WILDLIFE_REJECTED",
            $"scheduled={wildlifeScheduled}; failure={wildlifeFailure.Code}");

        SurgicalSubjectRef constructSpoof = CreateSubject(patient);
        constructSpoof.speciesId = "Golem";
        bool constructScheduled = commands.TrySchedule(
            constructSpoof,
            TransfusionProcedureId,
            targetNodeId,
            string.Empty,
            doctor.Identity?.PersistentId,
            facilities.GetFacilityId(table),
            out _,
            out DomainFailure constructFailure);
        Check(
            !constructScheduled
                && constructFailure.Code
                    == FailureCode.SurgerySubjectMaintenanceOnly,
            "TRANSFUSION_CONSTRUCT_REJECTED_BY_EXISTING_GATE",
            $"scheduled={constructScheduled}; failure={constructFailure.Code}");

        SurgicalSubjectRef speciesSpoof = CreateSubject(patient);
        speciesSpoof.speciesId = "not-the-live-biological-species";
        bool speciesSpoofScheduled = commands.TrySchedule(
            speciesSpoof,
            TransfusionProcedureId,
            targetNodeId,
            string.Empty,
            doctor.Identity?.PersistentId,
            facilities.GetFacilityId(table),
            out _,
            out DomainFailure speciesSpoofFailure);
        Check(
            !speciesSpoofScheduled
                && speciesSpoofFailure.Code
                    == FailureCode.SurgerySpeciesUnsupported,
            "TRANSFUSION_SPECIES_SPOOF_REJECTED_BY_LIVE_AUTHORITY",
            $"scheduled={speciesSpoofScheduled}; failure={speciesSpoofFailure.Code}");

        Finish();
    }

    private bool TryCompleteProductionSurgery(
        SurgicalSubjectRef subject,
        string procedureId,
        string nodeId,
        out SurgeryOrder order,
        out string detail)
    {
        order = null;
        detail = string.Empty;
        bool scheduled = commands.TrySchedule(
            subject,
            procedureId,
            nodeId,
            string.Empty,
            doctor.Identity?.PersistentId,
            facilities.GetFacilityId(table),
            out SurgeryOrder scheduledOrder,
            out DomainFailure scheduleFailure);
        if (!scheduled || scheduledOrder == null)
        {
            detail = "schedule=" + Describe(scheduleFailure);
            return false;
        }

        order = scheduledOrder;
        bool materialsReady = true;
        foreach (SurgicalMaterialRequirement requirement in
                 order.materials.Where(requirement => requirement != null
                     && !requirement.optional))
        {
            bool spawned = items.SpawnItemAt(
                requirement.itemId,
                Mathf.Max(1, requirement.quantity),
                table.centerPos,
                WorldItemStackState.FacilityBuffer,
                order.materialDestinationId,
                out int amount);
            materialsReady &= spawned
                && amount == Mathf.Max(1, requirement.quantity);
        }

        BuildingProcessFluidAbility processFluid = table.BuildingData
            ?.GetAbility<BuildingProcessFluidAbility>();
        if (processFluid != null
            && processFluid.Supports(BuiltInWorkTypeIds.Surgery)
            && processFluid.cleanWaterPerCycle > 0f)
        {
            string waterDestination =
                $"plumbing:process-water:{facilities.GetFacilityId(table)}:"
                + BuiltInWorkTypeIds.Surgery.Value;
            bool waterSpawned = items.SpawnItemAt(
                CleanWaterItemId,
                1,
                table.centerPos,
                WorldItemStackState.FacilityBuffer,
                waterDestination,
                out int waterAmount);
            materialsReady &= waterSpawned && waterAmount == 1;
        }

        order.patientAdmitted = true;
        surgeryRuntime.Tick();
        DomainFailure reserveFailure = DomainFailure.None;
        SurgeryOrder reservedOrder = null;
        bool reserved = materialsReady
            && work.TryReserveWork(
                table,
                doctor,
                out reservedOrder,
                out reserveFailure);
        if (!reserved || !ReferenceEquals(reservedOrder, order))
        {
            environmentalField.TryGetCell(
                table.centerPos,
                out EnvironmentalCellSnapshot liveEnvironment);
            detail = $"materials={materialsReady}; reserve="
                + Describe(reserveFailure)
                + $"; evaluated=temp={order.environmentWait.scalarValue:0.###},"
                + $"air={order.environmentWait.secondaryScalarValue:0.###},"
                + $"light={order.environmentWait.tertiaryScalarValue:0.###}; "
                + DescribeEnvironment(liveEnvironment);
            return false;
        }

        float preCompletionWork = Mathf.Max(0f, order.requiredWork - 0.01f);
        bool progressed = work.ApplyWork(
            order.orderId,
            doctor,
            preCompletionWork,
            out bool completedEarly,
            out DomainFailure progressFailure);
        if (!progressed
            || completedEarly
            || order.resultRolled
            || order.completedWork + 0.001f < preCompletionWork)
        {
            detail = $"pre-completion progress={progressed}; "
                + $"completed={completedEarly}; resultRolled={order.resultRolled}; "
                + $"work={order.completedWork:0.###}/{order.requiredWork:0.###}; "
                + $"failure={Describe(progressFailure)}";
            return false;
        }

        order.risk.successChance = 1f;
        order.risk.infectionChance = 0f;
        order.risk.bleedingChance = 0f;
        order.risk.organDamageChance = 0f;
        order.risk.deathChance = 0f;
        bool applied = work.ApplyWork(
            order.orderId,
            doctor,
            order.requiredWork - order.completedWork,
            out bool completed,
            out DomainFailure workFailure);
        detail = $"order={order.orderId}; preCompletion={progressed}; "
            + $"applied={applied}; completed={completed}; "
            + $"state={order.state}; resultRolled={order.resultRolled}; "
            + $"failure={Describe(workFailure)}";
        return applied
            && completed
            && order.resultRolled
            && order.state == SurgeryOrderState.Recovering;
    }

    private IEnumerator WaitForTerminal(
        SurgeryOrder order,
        SurgeryOrderState expectedState)
    {
        float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
        while (order?.IsActive == true
               && Time.realtimeSinceStartup < deadline)
        {
            if (Time.timeScale <= 0f)
            {
                Time.timeScale = 8f;
            }

            yield return null;
        }

        Check(
            order != null
                && !order.IsActive
                && order.state == expectedState,
            "ORDER_REACHES_TERMINAL_" + (order?.orderId ?? "missing"),
            $"expected={expectedState}; actual={order?.state.ToString() ?? "missing"}");
    }

    private void SetBloodLoss(CharacterActor actor, float amount)
    {
        CharacterBodyHealthSnapshot current = bodyHealth.GetSnapshot(actor);
        bodyCommands.ApplySnapshot(
            actor,
            new CharacterBodyHealthSnapshot(
                current.Parts,
                amount,
                current.Suppression,
                current.Consciousness,
                current.Manipulation,
                current.Mobility,
                current.Downed),
            "wim-021-blood-loss-fixture");
    }

    private void StopAllBleeding(CharacterActor actor)
    {
        foreach (AnatomyNodeHealthState node in anatomy
                     .GetAnatomySnapshot(actor)
                     .Nodes
                     .Where(node => node != null && !node.missing))
        {
            anatomy.TryStopBleeding(actor, node.nodeId, out _);
        }
    }

    private AnatomyNodeHealthState GetNode(
        CharacterActor actor,
        string nodeId)
    {
        return anatomy.GetAnatomySnapshot(actor).Nodes.Single(node =>
            node != null
            && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal));
    }

    private static AnatomyNodeHealthState CloneNode(
        AnatomyNodeHealthState source) => new()
    {
        nodeId = source.nodeId,
        maxHealth = source.maxHealth,
        currentHealth = source.currentHealth,
        bleedingPerSecond = source.bleedingPerSecond,
        infection = source.infection,
        missing = source.missing,
        installedPartId = source.installedPartId,
        installedPartKind = source.installedPartKind,
        installedPartEfficiency = source.installedPartEfficiency,
        rejectionBurden = source.rejectionBurden,
        mutationBurden = source.mutationBurden,
        moduleBonus = source.moduleBonus,
        recoveryPolicy = source.recoveryPolicy
    };

    private void CheckNodeEquivalent(
        AnatomyNodeHealthState actual,
        AnatomyNodeHealthState expected,
        string key)
    {
        Check(
            actual != null
                && expected != null
                && Approximately(actual.maxHealth, expected.maxHealth)
                && Approximately(actual.currentHealth, expected.currentHealth)
                && Approximately(
                    actual.bleedingPerSecond,
                    expected.bleedingPerSecond)
                && Approximately(actual.infection, expected.infection)
                && actual.missing == expected.missing
                && string.Equals(
                    actual.installedPartId,
                    expected.installedPartId,
                    StringComparison.Ordinal)
                && actual.installedPartKind == expected.installedPartKind
                && Approximately(
                    actual.installedPartEfficiency,
                    expected.installedPartEfficiency)
                && Approximately(
                    actual.rejectionBurden,
                    expected.rejectionBurden)
                && Approximately(
                    actual.mutationBurden,
                    expected.mutationBurden)
                && Approximately(actual.moduleBonus, expected.moduleBonus)
                && actual.recoveryPolicy == expected.recoveryPolicy,
            key,
            actual == null || expected == null
                ? "missing"
                : $"health={expected.currentHealth:0.###}->{actual.currentHealth:0.###}; "
                    + $"bleed={expected.bleedingPerSecond:0.###}->{actual.bleedingPerSecond:0.###}; "
                    + $"infection={expected.infection:0.###}->{actual.infection:0.###}");
    }

    private static CharacterBodyPartHealthState GetLegacyPart(
        CharacterBodyHealthSnapshot snapshot,
        CombatBodyPart bodyPart) => snapshot.Parts.Single(part =>
            part != null && part.bodyPart == bodyPart);

    private static SurgicalSubjectRef CreateSubject(CharacterActor actor) =>
        new()
        {
            kind = SurgicalSubjectKind.Character,
            subjectId = actor.Identity?.PersistentId ?? string.Empty,
            displayName = actor.Identity?.DisplayName ?? string.Empty,
            speciesId = actor.Identity?.SpeciesTag ?? string.Empty,
            willing = true,
            automaticEmergencyDefault = false
        };

    private IEnumerator EnsurePlayableRun()
    {
        OwnerRunManager ownerManager =
            UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
        {
            report.Add(
                "[INFO] FAST_PARTY_COMMIT "
                + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            Time.timeScale = 8f;
            for (int i = 0; i < 8; i++)
            {
                yield return null;
            }
        }

        ownerManager =
            UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Check(
            ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "PLAYABLE_RUN",
            ownerManager?.CurrentOwnerActor?.name ?? "owner missing");
    }

    private static bool TryFindPlacement(
        RoomLayout layout,
        Grid grid,
        BuildingSO asset,
        out Vector2Int tablePosition)
    {
        tablePosition = default;
        if (layout == null || grid == null || asset == null)
        {
            return false;
        }

        foreach (RoomInstance room in layout.Rooms
                     .Where(candidate => candidate != null && candidate.IsUsable)
                     .OrderByDescending(candidate => candidate.Cells.Count))
        {
            HashSet<Vector2Int> roomCells = new(room.Cells);
            foreach (Vector2Int cell in room.Cells)
            {
                IReadOnlyList<Vector2Int> footprint = asset.GetGridPosList(cell);
                if (footprint.All(position => roomCells.Contains(position)
                    && grid.GetGridCell(position) != null
                    && !grid.GetGridCell(position)
                        .HasOccupantInLayer(asset.Placement.Layer)))
                {
                    tablePosition = cell;
                    return true;
                }
            }
        }

        return false;
    }

    private Facility CreateInjectedFacility(
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        BuildingSO asset,
        Vector2Int position)
    {
        GameObject obj = new("QA_WIM021022_EmergencyTable");
        temporaryObjects.Add(obj);
        Facility facility = obj.AddComponent<Facility>();
        foreach (MonoBehaviour component in
                 obj.GetComponentsInChildren<MonoBehaviour>(true))
        {
            scope.Container.Inject(component);
        }

        facility.SetGrid(grid);
        facility.Initialization(asset, position);
        Vector3 world = grid.GetWorldPos(position);
        if (asset.Placement.HasEvenWidth)
        {
            world.x += 0.5f;
        }

        obj.transform.position = new Vector3(
            world.x,
            world.y,
            obj.transform.position.z);
        return facility;
    }

    private BuildableObject CreateInjectedBuilding(
        DungeonRuntimeLifetimeScope scope,
        IGridBuildingObjectFactory factory,
        Grid grid,
        BuildingSO asset,
        Vector2Int position)
    {
        if (scope?.Container == null
            || factory == null
            || grid == null
            || asset == null)
        {
            return null;
        }

        BuildableObject building = factory.Create(grid, asset, position);
        if (building == null)
        {
            return null;
        }

        temporaryObjects.Add(building.gameObject);
        foreach (MonoBehaviour component in
                 building.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }

        building.SetGrid(grid);
        building.Initialization(asset, position);
        return building;
    }

    private void StabilizeActor(CharacterActor actor)
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

    private bool Check(bool condition, string key, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {key} {detail}");
        if (!condition)
        {
            failures.Add(key + ": " + detail);
        }

        return condition;
    }

    private void CheckApprox(float actual, float expected, string key)
    {
        Check(
            Approximately(actual, expected),
            key,
            $"expected={expected:0.###}; actual={actual:0.###}");
    }

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.001f;

    private static string Describe(DomainFailure failure) =>
        failure.Code + ":"
        + string.Join(",", failure.Parameters.ToArray());

    private static string DescribeEnvironment(
        EnvironmentalCellSnapshot environment) =>
        $"environment=temp={environment.TemperatureC:0.###},"
        + $"air={environment.AirQuality:0.###},"
        + $"light={environment.LightLevel:0.###}";

    private void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Error or LogType.Exception)
        {
            capturedErrors.Add(condition + "\n" + stackTrace);
        }
    }

    private void Finish()
    {
        if (finishing)
        {
            return;
        }

        finishing = true;
        Application.logMessageReceived -= OnLogMessageReceived;
        DungeonGameRestoreReport baselineRestore = null;
        if (baseline != null
            && (gameSave == null
                || !gameSave.TryRestore(baseline, out baselineRestore)))
        {
            capturedErrors.Add(
                "Baseline restore failed: "
                + string.Join(" | ", baselineRestore?.Errors
                    ?? Array.Empty<string>()));
        }

        foreach (GameObject temporaryObject in temporaryObjects)
        {
            if (temporaryObject != null)
            {
                Destroy(temporaryObject);
            }
        }

        bool passed = failures.Count == 0 && capturedErrors.Count == 0;
        report.Add(
            $"capturedErrors={capturedErrors.Count}; "
            + string.Join(" | ", capturedErrors.Select(Compact)));
        report.Add(
            $"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; "
            + string.Join(" | ", failures.Select(Compact)));
        File.WriteAllText(
            WimTransfusionTargetedHemostasisDebugScenarios.ReportPath,
            string.Join("\n", report));
        File.Delete(
            WimTransfusionTargetedHemostasisDebugScenarios.RequestPath);
        Time.timeScale = originalTimeScale;
        Application.runInBackground = originalRunInBackground;
        if (passed)
        {
            Debug.Log(
                "WIM-021-022 medical effects verification passed. "
                + WimTransfusionTargetedHemostasisDebugScenarios.ReportPath);
        }
        else
        {
            Debug.LogError(
                "WIM-021-022 medical effects verification failed. "
                + WimTransfusionTargetedHemostasisDebugScenarios.ReportPath);
        }

        EditorApplication.ExitPlaymode();
        Destroy(gameObject);
    }

    private static string Compact(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "<none>"
            : value.Replace("\r", " ").Replace("\n", " ").Trim();

    private static T Resolve<T>(DungeonRuntimeLifetimeScope scope)
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

    private static DungeonRuntimeLifetimeScope FindScope() =>
        UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
}
#endif
