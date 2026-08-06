#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class EquipmentExpeditionUiMatrixPlayModeVerifier
{
    public const string FacilityFlowMarker =
        "FACILITY_FLOW=RF42,RF43,RF44,I17,I18";
    public const string RequestPath =
        "Temp/equipment-expedition-ui-matrix.request";
    public const string ReportPath =
        "Artifacts/QA/equipment-expedition-ui-matrix-report.txt";

    private static bool runnerCreated;

    static EquipmentExpeditionUiMatrixPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("DungeonStory/QA/Request Equipment Expedition UI Matrix")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        FinalAcceptanceReportPolicy.DeleteFiles(GetCapturePaths());
        File.WriteAllText(RequestPath, DateTime.UtcNow.Ticks.ToString());
    }

    public static string GetCapturePath(Vector2Int resolution, string surface)
    {
        return $"Artifacts/QA/equipment-expedition-{surface}-"
            + $"{resolution.x}x{resolution.y}.png";
    }

    public static string[] GetCapturePaths()
    {
        Vector2Int[] resolutions =
        {
            new(1600, 900),
            new(900, 1600)
        };
        string[] surfaces = { "equipment", "expedition" };
        return resolutions
            .SelectMany(resolution => surfaces.Select(surface =>
                GetCapturePath(resolution, surface)))
            .ToArray();
    }

    private static void OnEditorUpdate()
    {
        if (File.Exists(RequestPath)
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.EnterPlaymode();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
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
        new GameObject("Equipment Expedition UI Matrix Runner")
            .AddComponent<EquipmentExpeditionUiMatrixRunner>();
    }
}

public sealed class EquipmentExpeditionUiMatrixRunner : MonoBehaviour
{
    private const string AppraisalFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF42_부품_감정대.asset";
    private const string RestorationFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF43_부품_복원_작업대.asset";
    private const string PrecisionFittingFacilityPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF44_정밀_장착대.asset";
    private const string RuneTuningFacilityPath =
        "Assets/Resources/SO/Building/Industrial/I17_룬_조율실.asset";
    private const string LineageArchiveFacilityPath =
        "Assets/Resources/SO/Building/Industrial/I18_계보_기록실.asset";
    private const string WrongFacilityPath =
        "Assets/Resources/SO/Building/Modular/S08_대장작업대.asset";

    private static readonly Vector2Int[] Resolutions =
    {
        new(1600, 900),
        new(900, 1600)
    };

    private static readonly string[] ProgressionCommandPrefixes =
    {
        "EquipmentModuleAppraise_",
        "EquipmentModuleRestore_",
        "EquipmentModuleTune_",
        "EquipmentModuleInstall_",
        "EquipmentModuleRemove_",
        "EquipmentLineageSource_",
        "EquipmentLineageTarget_",
        "EquipmentLineageSeal_",
        "EquipmentLineageConfirm"
    };

    private static readonly string[] RequiredResearchIds =
    {
        "research:equipment:relic-appraisal",
        "research:equipment:relic-restoration",
        "research:equipment:precision-fitting",
        "research:equipment:rune-module-tuning",
        "research:equipment:lineage-binding",
        "research:equipment:weapon-patterns",
        "research:metallurgy:steel"
    };

    private readonly List<string> report = new();
    private readonly List<string> failures = new();
    private readonly List<string> errors = new();
    private readonly List<string> warnings = new();
    private readonly List<UnityEngine.Object> created = new();

    private ICombatEquipmentRuntime equipment;
    private IWorldItemStackRuntime physicalItems;
    private IEquipmentCraftingPanelPresenter equipmentPresenter;
    private TMP_FontAsset equipmentFont;
    private BlueprintResearchRuntime research;
    private OffenseExpeditionRuntime offenseExpeditions;
    private IOffenseBattleRuntime offenseBattle;
    private IDungeonSaveSection researchSaveSection;
    private IDungeonSaveSection offenseSaveSection;
    private DungeonPhysicalItemSaveData physicalSnapshot;
    private DungeonCombatEquipmentSaveData equipmentSnapshot;
    private string researchSnapshot = string.Empty;
    private string offenseSnapshot = string.Empty;
    private BuildableObject equipmentFacility;
    private readonly Dictionary<string, BuildableObject> equipmentFacilities =
        new(StringComparer.Ordinal);
    private RectTransform equipmentContent;
    private ScrollRect equipmentScroll;
    private GameObject equipmentSurface;
    private int originalResolutionIndex = -1;
    private string appraiseModuleId = string.Empty;
    private string restoreModuleId = string.Empty;
    private string tuneModuleId = string.Empty;
    private string installModuleId = string.Empty;
    private string installTargetId = string.Empty;
    private string lineageSourceId = string.Empty;
    private string lineageTargetId = string.Empty;
    private string lineageSealStackId = string.Empty;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += CaptureLog;
        originalResolutionIndex = GameViewResolutionController.SelectedSizeIndex;

        yield return ExecuteGuarded(RunMatrix());
        CleanupAfterRun();
        Finish();
    }

    private IEnumerator RunMatrix()
    {
        yield return CompleteOwnerSelectionIfVisible();
        yield return StartPartyPlayModeTestDriver.CompleteIfVisible(45f);
        yield return ResolveRuntime();
        if (equipment == null
            || physicalItems == null
            || equipmentPresenter == null
            || research == null
            || offenseExpeditions == null
            || offenseBattle == null
            || researchSaveSection == null
            || offenseSaveSection == null)
        {
            yield break;
        }

        physicalSnapshot = physicalItems.Capture();
        equipmentSnapshot = equipment.Capture();
        researchSnapshot = researchSaveSection.Capture();
        offenseSnapshot = offenseSaveSection.Capture();

        foreach (Vector2Int resolution in Resolutions)
        {
            yield return SelectResolution(resolution);
            RestoreRuntimeState();
            CreateEquipmentSurface();
            SeedEquipmentUiState();
            yield return VerifyEquipmentPointerFlow(resolution);
            yield return VerifyExpeditionPointerFlow(resolution);
        }
    }

    private IEnumerator ExecuteGuarded(IEnumerator root)
    {
        Stack<IEnumerator> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek();
            bool moved;
            object yielded = null;
            Exception caught = null;
            try
            {
                moved = current.MoveNext();
                if (moved)
                {
                    yielded = current.Current;
                }
            }
            catch (Exception exception)
            {
                moved = false;
                caught = exception;
            }
            if (caught != null)
            {
                Check(false, "UNHANDLED_EXCEPTION", caught.ToString());
                break;
            }

            if (!moved)
            {
                stack.Pop();
                continue;
            }
            if (yielded is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return yielded;
        }
    }

    private void CleanupAfterRun()
    {
        try
        {
            RestoreRuntimeState();
            CleanupCreatedObjects();
            if (originalResolutionIndex >= 0)
            {
                GameViewResolutionController.SelectedSizeIndex =
                    originalResolutionIndex;
            }
        }
        catch (Exception exception)
        {
            Check(false, "CLEANUP_EXCEPTION", exception.ToString());
        }
    }

    private IEnumerator ResolveRuntime()
    {
        DungeonRuntimeLifetimeScope scope = null;
        float deadline = Time.realtimeSinceStartup + 20f;
        while ((scope == null || scope.Container == null)
            && Time.realtimeSinceStartup < deadline)
        {
            scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate != null && candidate.Container != null);
            yield return null;
        }

        Check(scope?.Container != null, "SCOPE", "runtime container resolved");
        if (scope?.Container == null)
        {
            yield break;
        }

        equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        physicalItems = scope.Container.Resolve<IWorldItemStackRuntime>();
        equipmentPresenter =
            scope.Container.Resolve<IEquipmentCraftingPanelPresenter>();
        equipmentFont = scope.Container.Resolve<ITmpKoreanFontService>()
            .Resolve();
        research = scope.Container.Resolve<ProgressionSceneRuntimeReferences>()
            .BlueprintResearch;
        offenseExpeditions = FindFirstObjectByType<OffenseExpeditionRuntime>();
        offenseBattle = scope.Container.Resolve<IOffenseBattleRuntime>();
        IDungeonSaveSectionRegistry saveSections =
            scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        researchSaveSection = saveSections.OrderedSections.FirstOrDefault(
            section => string.Equals(
                section.SectionId,
                BlueprintResearchSaveSection.Id,
                StringComparison.Ordinal));
        offenseSaveSection = saveSections.OrderedSections.FirstOrDefault(
            section => string.Equals(
                section.SectionId,
                OffenseAggregateSaveSection.Id,
                StringComparison.Ordinal));
        Check(equipment != null, "EQUIPMENT_RUNTIME", "resolved");
        Check(physicalItems != null, "PHYSICAL_ITEMS", "resolved");
        Check(equipmentPresenter != null, "EQUIPMENT_PRESENTER", "resolved");
        Check(research != null, "RESEARCH_RUNTIME", "resolved");
        Check(offenseExpeditions != null,
            "OFFENSE_EXPEDITION_RUNTIME",
            "resolved");
        Check(offenseBattle != null,
            "OFFENSE_BATTLE_RUNTIME",
            "resolved");
        Check(researchSaveSection != null,
            "RESEARCH_SAVE_SECTION",
            "resolved");
        Check(offenseSaveSection != null,
            "OFFENSE_SAVE_SECTION",
            "resolved");
    }

    private void SeedEquipmentUiState()
    {
        foreach (string researchId in RequiredResearchIds)
        {
            research.State.Projects.RestoreCompleted(
                new ResearchProjectId(researchId));
        }

        CombatEquipmentInstance source = equipment.CreateInstance(
            "weapon:longsword",
            CombatEquipmentQuality.Normal,
            CombatEquipmentWorldState.Stored);
        CombatEquipmentInstance target = equipment.CreateInstance(
            "weapon:greatsword",
            CombatEquipmentQuality.Masterwork,
            CombatEquipmentWorldState.Stored);
        MaterializeEquipment(source, new Vector2Int(1, 0));
        MaterializeEquipment(target, new Vector2Int(2, 0));

        BuildableObject appraisalFacility = RequireEquipmentFacility("appraisal");
        string appraisalDestination = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(appraisalFacility);
        EquipmentModuleInstance progressionModule =
            equipment.CreateExpeditionModule(
                "module:weapon:mana-conduit",
                4,
                appraisalFacility.centerPos,
                WorldItemStackState.FacilityBuffer,
                appraisalDestination);
        appraiseModuleId = progressionModule.instanceId;
        restoreModuleId = progressionModule.instanceId;
        tuneModuleId = progressionModule.instanceId;
        installModuleId = progressionModule.instanceId;
        installTargetId = target.instanceId;
        lineageSourceId = source.instanceId;
        lineageTargetId = target.instanceId;

        bool historySeeded = equipment.TryUpdateEvolutionState(
            source.instanceId,
            new EquipmentEvolutionState
            {
                generation = 3,
                mastery = 42f,
                activeHistoricalNodeIds = new List<string>
                {
                    "history:qa:equipment-ui-matrix"
                }
            });
        Check(
            progressionModule != null
                && !string.IsNullOrWhiteSpace(progressionModule.sourceStackId)
                && historySeeded,
            "EQUIPMENT_COMMAND_SEED",
            $"module={progressionModule?.instanceId}; "
            + $"stack={progressionModule?.sourceStackId}; history={historySeeded}");

        HashSet<string> sealIdsBefore = physicalItems.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    EquipmentProgressionItemIds.LineageSeal,
                    StringComparison.Ordinal))
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        bool sealSpawned = physicalItems.SpawnItemAt(
            EquipmentProgressionItemIds.LineageSeal,
            1,
            new Vector2Int(9001, 9000),
            WorldItemStackState.Loose,
            string.Empty,
            out int spawnedSealCount);
        lineageSealStackId = physicalItems.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    EquipmentProgressionItemIds.LineageSeal,
                    StringComparison.Ordinal)
                && !sealIdsBefore.Contains(stack.StackId))
            .Select(stack => stack.StackId)
            .FirstOrDefault() ?? string.Empty;
        Check(sealSpawned && spawnedSealCount == 1,
            "LINEAGE_SEAL_SEEDED",
            $"spawned={spawnedSealCount}; stack={lineageSealStackId}");

        report.Add(
            $"SEED equipment={source.instanceId},{target.instanceId}; "
            + $"module={progressionModule.instanceId}:"
            + progressionModule.sourceStackId);
    }

    private IEnumerator VerifyEquipmentPointerFlow(Vector2Int resolution)
    {
        SelectEquipmentFacility("appraisal");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Canvas.ForceUpdateCanvases();

        CheckFacilityCommandVisibility(
            "appraisal",
            new[] { "EquipmentModuleAppraise_" },
            resolution);
        bool appraised = ClickExactName(
            "EquipmentModuleAppraise_" + Sanitize(appraiseModuleId),
            $"MODULE_APPRAISAL_POINTER_{Key(resolution)}");
        EquipmentModuleInstance appraisedState = FindModule(appraiseModuleId);
        bool appraisedTransition = appraisedState?.state
            == EquipmentModuleProcessState.IdentifiedDamaged;
        CheckModuleInFacilityBuffer(
            appraisedState,
            "appraisal",
            $"MODULE_APPRAISAL_BUFFER_{Key(resolution)}");

        RouteModuleToFacility(
            appraisedState,
            "restoration",
            $"MODULE_ROUTE_RESTORATION_{Key(resolution)}");
        SelectEquipmentFacility("restoration");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        CheckFacilityCommandVisibility(
            "restoration",
            new[] { "EquipmentModuleRestore_" },
            resolution);
        bool restored = ClickExactName(
            "EquipmentModuleRestore_" + Sanitize(restoreModuleId),
            $"MODULE_RESTORATION_POINTER_{Key(resolution)}");
        EquipmentModuleInstance restoredState = FindModule(restoreModuleId);
        bool restoredTransition = restoredState?.state
            == EquipmentModuleProcessState.Restored;
        CheckModuleInFacilityBuffer(
            restoredState,
            "restoration",
            $"MODULE_RESTORATION_BUFFER_{Key(resolution)}");

        RouteModuleToFacility(
            restoredState,
            "rune-tuning",
            $"MODULE_ROUTE_RUNE_TUNING_{Key(resolution)}");
        SelectEquipmentFacility("rune-tuning");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        CheckFacilityCommandVisibility(
            "rune-tuning",
            new[] { "EquipmentModuleTune_" },
            resolution);
        bool tuned = ClickExactName(
            "EquipmentModuleTune_" + Sanitize(tuneModuleId),
            $"MODULE_RUNE_TUNING_POINTER_{Key(resolution)}");
        EquipmentModuleInstance tunedState = FindModule(tuneModuleId);
        bool tunedTransition = tunedState?.state
                == EquipmentModuleProcessState.Tuned
            && tunedState.runeTuned;
        CheckModuleInFacilityBuffer(
            tunedState,
            "rune-tuning",
            $"MODULE_RUNE_TUNING_BUFFER_{Key(resolution)}");

        RouteModuleToFacility(
            tunedState,
            "precision-fitting",
            $"MODULE_ROUTE_PRECISION_FITTING_{Key(resolution)}");
        RouteEquipmentToFacility(
            installTargetId,
            "precision-fitting",
            $"EQUIPMENT_ROUTE_PRECISION_FITTING_{Key(resolution)}");
        SelectEquipmentFacility("precision-fitting");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        CheckFacilityCommandVisibility(
            "precision-fitting",
            new[] { "EquipmentModuleInstall_" },
            resolution);
        string absorbedStackId = FindModule(installModuleId)?.sourceStackId
            ?? string.Empty;
        bool installed = ClickExactName(
            "EquipmentModuleInstall_" + Sanitize(installModuleId)
            + "_" + Sanitize(installTargetId) + "_0",
            $"MODULE_INSTALL_POINTER_{Key(resolution)}");
        EquipmentModuleInstance installedState = FindModule(installModuleId);
        bool physicalStackAbsorbed = installedState?.state
                == EquipmentModuleProcessState.Installed
            && string.IsNullOrWhiteSpace(installedState.sourceStackId)
            && !physicalItems.GetAllStacks().Any(stack =>
                stack.StackId == absorbedStackId);
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        CheckFacilityCommandVisibility(
            "precision-fitting",
            new[] { "EquipmentModuleRemove_" },
            resolution);
        bool removed = ClickExactName(
            "EquipmentModuleRemove_" + Sanitize(installTargetId) + "_0",
            $"MODULE_REMOVE_POINTER_{Key(resolution)}");
        EquipmentModuleInstance removedState = FindModule(installModuleId);
        bool physicalStackRecreated = removedState?.state
                == EquipmentModuleProcessState.IdentifiedDamaged
            && removedState.condition <= 0.7001f
            && string.IsNullOrWhiteSpace(
                removedState.attachedEquipmentInstanceId)
            && !string.IsNullOrWhiteSpace(removedState.sourceStackId);
        CheckModuleInFacilityBuffer(
            removedState,
            "precision-fitting",
            $"MODULE_REMOVAL_BUFFER_{Key(resolution)}");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();

        SelectEquipmentFacility("wrong");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        CheckFacilityCommandVisibility("wrong", Array.Empty<string>(), resolution);

        SelectEquipmentFacility("lineage");
        RouteLineageInputsToFacility();
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        CheckFacilityCommandVisibility(
            "lineage",
            new[]
            {
                "EquipmentLineageSource_",
                "EquipmentLineageTarget_",
                "EquipmentLineageSeal_",
                "EquipmentLineageConfirm"
            },
            resolution);
        bool sourceSelected = ClickExactName(
            "EquipmentLineageSource_" + Sanitize(lineageSourceId),
            $"LINEAGE_SOURCE_POINTER_{Key(resolution)}");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        bool targetSelected = ClickExactName(
            "EquipmentLineageTarget_" + Sanitize(lineageTargetId),
            $"LINEAGE_TARGET_POINTER_{Key(resolution)}");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        bool sealSelected = ClickByPrefix(
            "EquipmentLineageSeal_",
            $"LINEAGE_SEAL_POINTER_{Key(resolution)}");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        int ordersBefore = equipment.HistoryTransferOrders.Count;
        bool lineageConfirmed = ClickByPrefix(
            "EquipmentLineageConfirm",
            $"LINEAGE_CONFIRM_POINTER_{Key(resolution)}");
        RebuildEquipmentSurface();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        bool lineageQueued = equipment.HistoryTransferOrders.Count > ordersBefore;

        bool moduleTransitions = appraisedTransition
            && restoredTransition
            && tunedTransition
            && physicalStackAbsorbed
            && physicalStackRecreated;

        Check(
            appraised && restored && tuned && installed && removed,
            $"EQUIPMENT_MODULE_POINTER_{Key(resolution)}",
            $"appraise={appraised}; restore={restored}; tune={tuned}; "
            + $"install={installed}; remove={removed}");
        Check(
            moduleTransitions,
            $"EQUIPMENT_MODULE_STATE_{Key(resolution)}",
            $"appraise={appraisedState?.state}; restore={restoredState?.state}; "
            + $"tune={tunedState?.state}:{tunedState?.runeTuned}; "
            + $"absorbed={physicalStackAbsorbed}:{absorbedStackId}; "
            + $"removed={removedState?.state}:{removedState?.condition:0.00}:"
            + $"{removedState?.sourceStackId}; recreated={physicalStackRecreated}");
        Check(
            sourceSelected
                && targetSelected
                && sealSelected
                && lineageConfirmed
                && lineageQueued,
            $"EQUIPMENT_LINEAGE_POINTER_{Key(resolution)}",
            $"source={sourceSelected}; target={targetSelected}; "
            + $"seal={sealSelected}; "
            + $"confirm={lineageConfirmed}; queued={lineageQueued}");
        CheckLineageInputsInFacilityBuffer(resolution);
        CheckSurfaceInsideScreen(
            equipmentSurface,
            resolution,
            $"EQUIPMENT_BOUNDS_{Key(resolution)}");

        equipmentScroll.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
        yield return Capture(
            EquipmentExpeditionUiMatrixPlayModeVerifier.GetCapturePath(
                resolution,
                "equipment"),
            resolution,
            $"EQUIPMENT_CAPTURE_{Key(resolution)}");

        EquipmentHistoryTransferOrder queuedOrder = equipment
            .HistoryTransferOrders
            .FirstOrDefault(order => order != null
                && !order.completed
                && order.sourceEquipmentInstanceId == lineageSourceId
                && order.targetEquipmentInstanceId == lineageTargetId);
        bool transferCompleted = false;
        DomainFailure transferFailure = DomainFailure.None;
        bool workApplied = queuedOrder != null
            && equipment.ApplyHistoryTransferWork(
                queuedOrder.orderId,
                queuedOrder.requiredWork,
                RequireEquipmentFacility("lineage"),
                out transferCompleted,
                out transferFailure);
        bool sourceConsumed = !equipment.Instances.Any(instance =>
                instance.instanceId == lineageSourceId)
            && !physicalItems.GetAllStacks().Any(stack =>
                stack.ItemInstanceId == lineageSourceId);
        bool sealConsumed = !physicalItems.GetAllStacks().Any(stack =>
            stack.StackId == lineageSealStackId);
        CombatEquipmentInstance transferredTarget = equipment.Instances
            .FirstOrDefault(instance => instance.instanceId == lineageTargetId);
        bool targetRetainedAndInherited = transferredTarget != null
            && transferredTarget.definitionId == "weapon:greatsword"
            && transferredTarget.quality == CombatEquipmentQuality.Masterwork
            && transferredTarget.evolution?.generation == 3
            && Mathf.Approximately(transferredTarget.evolution.mastery, 42f)
            && transferredTarget.evolution.activeHistoricalNodeIds.Contains(
                "history:qa:equipment-ui-matrix");
        Check(
            workApplied
                && transferCompleted
                && !transferFailure.IsFailure
                && sourceConsumed
                && sealConsumed
                && targetRetainedAndInherited,
            $"EQUIPMENT_LINEAGE_CONSUMPTION_{Key(resolution)}",
            $"work={workApplied}; completed={transferCompleted}; "
            + $"failure={transferFailure.Code}; source={sourceConsumed}; "
            + $"seal={sealConsumed}; target={targetRetainedAndInherited}");
        DestroySurface();
    }

    private IEnumerator VerifyExpeditionPointerFlow(Vector2Int resolution)
    {
        PrepareIsolatedExpeditionState();
        string setup = OffenseJourneyPlayModeFacade.Setup();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        bool setupPassed = setup.StartsWith("PASS:", StringComparison.Ordinal);
        OffenseExpeditionRuntime runtime =
            FindFirstObjectByType<OffenseExpeditionRuntime>();
        OffenseExpeditionRun activeBefore = runtime?.ActiveExpeditions
            .FirstOrDefault();
        string phaseBefore = activeBefore != null
            ? $"{activeBefore.Phase}:{activeBefore.CurrentNodeId}"
            : "none";
        OffenseExpeditionPanel panel =
            FindFirstObjectByType<OffenseExpeditionPanel>();
        Button action = panel != null
            ? panel.GetComponentsInChildren<Button>(false)
                .FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.interactable
                    && !IsCloseButton(candidate))
            : null;
        bool pointerClicked = ClickPointer(
            action,
            $"EXPEDITION_ACTION_POINTER_{Key(resolution)}");
        yield return null;
        Canvas.ForceUpdateCanvases();

        OffenseExpeditionRun activeAfter = runtime?.ActiveExpeditions
            .FirstOrDefault();
        string phaseAfter = activeAfter != null
            ? $"{activeAfter.Phase}:{activeAfter.CurrentNodeId}"
            : "none";
        bool stateChanged = !string.Equals(
            phaseBefore,
            phaseAfter,
            StringComparison.Ordinal);
        Check(
            setupPassed && panel != null && pointerClicked && stateChanged,
            $"EXPEDITION_POINTER_{Key(resolution)}",
            $"setup={setup}; pointer={pointerClicked}; "
            + $"state={phaseBefore}->{phaseAfter}");
        CheckSurfaceInsideScreen(
            panel != null ? panel.gameObject : null,
            resolution,
            $"EXPEDITION_BOUNDS_{Key(resolution)}");
        yield return Capture(
            EquipmentExpeditionUiMatrixPlayModeVerifier.GetCapturePath(
                resolution,
                "expedition"),
            resolution,
            $"EXPEDITION_CAPTURE_{Key(resolution)}");
    }

    private void PrepareIsolatedExpeditionState()
    {
        offenseBattle.ClearForPersistentRestore();
        offenseExpeditions.PublishRestoreCandidate(
            offenseExpeditions.BuildRestoreCandidate(
                Array.Empty<OffenseExpeditionRun>(),
                Array.Empty<OffenseExpeditionResult>()));
        Check(
            offenseExpeditions.ActiveExpeditions.Count == 0
                && offenseExpeditions.ResultHistory.Count == 0
                && !offenseBattle.HasActiveBattle,
            "EXPEDITION_ROW_ISOLATED",
            $"active={offenseExpeditions.ActiveExpeditions.Count}; "
            + $"history={offenseExpeditions.ResultHistory.Count}; "
            + $"battle={offenseBattle.HasActiveBattle}");
    }

    private void MaterializeEquipment(
        CombatEquipmentInstance instance,
        Vector2Int position)
    {
        string physicalItemId = PhysicalItemIds.ForEquipment(
            instance.definitionId);
        bool spawned = physicalItems.SpawnExistingUniqueItemAt(
            physicalItemId,
            (ItemInstanceId)instance.instanceId,
            position,
            WorldItemStackState.Stored,
            "qa:equipment-ui-matrix",
            out string stackId);
        bool linked = spawned && equipment.TryLinkToWorldStack(
            instance.instanceId,
            stackId,
            CombatEquipmentWorldState.Stored);
        Check(
            linked,
            "EQUIPMENT_PHYSICAL_" + instance.definitionId,
            $"spawned={spawned}; stack={stackId}");
    }

    private void CreateEquipmentSurface()
    {
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Check(scope?.Container != null,
            "EQUIPMENT_FACILITY_SCOPE",
            "runtime container available");
        if (scope?.Container == null)
        {
            return;
        }

        equipmentFacilities.Clear();
        CreateEquipmentFacility(
            scope,
            "appraisal",
            AppraisalFacilityPath,
            new Vector2Int(9000, 9000));
        CreateEquipmentFacility(
            scope,
            "restoration",
            RestorationFacilityPath,
            new Vector2Int(9002, 9000));
        CreateEquipmentFacility(
            scope,
            "precision-fitting",
            PrecisionFittingFacilityPath,
            new Vector2Int(9004, 9000));
        CreateEquipmentFacility(
            scope,
            "rune-tuning",
            RuneTuningFacilityPath,
            new Vector2Int(9006, 9000));
        CreateEquipmentFacility(
            scope,
            "lineage",
            LineageArchiveFacilityPath,
            new Vector2Int(9008, 9000));
        CreateEquipmentFacility(
            scope,
            "wrong",
            WrongFacilityPath,
            new Vector2Int(9010, 9000));

        equipmentSurface = new GameObject(
            "EquipmentUiMatrixSurface",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        created.Add(equipmentSurface);
        Canvas canvas = equipmentSurface.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        CanvasScaler scaler = equipmentSurface.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.matchWidthOrHeight = Screen.width < Screen.height ? 0f : 1f;

        GameObject viewport = new(
            "EquipmentViewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask),
            typeof(ScrollRect));
        viewport.transform.SetParent(equipmentSurface.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.04f, 0.04f);
        viewportRect.anchorMax = new Vector2(0.96f, 0.96f);
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = DungeonUiTheme.Surface;
        viewport.GetComponent<Mask>().showMaskGraphic = true;

        GameObject content = new(
            "EquipmentContent",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        equipmentContent = content.GetComponent<RectTransform>();
        equipmentContent.anchorMin = new Vector2(0f, 1f);
        equipmentContent.anchorMax = new Vector2(1f, 1f);
        equipmentContent.pivot = new Vector2(0.5f, 1f);
        equipmentContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        content.GetComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        equipmentScroll = viewport.GetComponent<ScrollRect>();
        equipmentScroll.viewport = viewportRect;
        equipmentScroll.content = equipmentContent;
        equipmentScroll.horizontal = false;
        equipmentScroll.vertical = true;
    }

    private void CreateEquipmentFacility(
        DungeonRuntimeLifetimeScope scope,
        string key,
        string assetPath,
        Vector2Int position)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
        Check(
            building != null,
            "EQUIPMENT_FACILITY_ASSET_" + key,
            assetPath);
        if (building == null)
        {
            return;
        }

        string expectedTag = key switch
        {
            "appraisal" => EquipmentProgressionWorkstationTags.Appraisal,
            "restoration" => EquipmentProgressionWorkstationTags.Restoration,
            "precision-fitting" =>
                EquipmentProgressionWorkstationTags.PrecisionFitting,
            "rune-tuning" => EquipmentProgressionWorkstationTags.RuneTuning,
            "lineage" => EquipmentProgressionWorkstationTags.LineageArchive,
            _ => string.Empty
        };
        string actualTag = building
            .GetAbility<BuildingProductionWorkstationAbility>()?
            .WorkstationTag ?? string.Empty;
        Check(
            string.IsNullOrEmpty(expectedTag)
                ? !new[]
                    {
                        EquipmentProgressionWorkstationTags.Appraisal,
                        EquipmentProgressionWorkstationTags.Restoration,
                        EquipmentProgressionWorkstationTags.PrecisionFitting,
                        EquipmentProgressionWorkstationTags.RuneTuning,
                        EquipmentProgressionWorkstationTags.LineageArchive
                    }.Contains(actualTag, StringComparer.Ordinal)
                : string.Equals(
                    actualTag,
                    expectedTag,
                    StringComparison.Ordinal),
            "EQUIPMENT_FACILITY_TAG_" + key,
            $"expected={expectedTag}; actual={actualTag}");

        GameObject facilityObject = new("EquipmentUiMatrixFacility_" + key);
        created.Add(facilityObject);
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        scope.Container.Inject(facility);
        facility.Initialization(building, position);
        equipmentFacilities.Add(key, facility);
        report.Add(
            $"FACILITY key={key}; code={building.GetFacilityCode()}; "
            + $"asset={assetPath}; id={facility.RequirePersistentInstanceId().Value}");
    }

    private void SelectEquipmentFacility(string key)
    {
        equipmentFacility = equipmentFacilities.TryGetValue(
            key,
            out BuildableObject facility)
                ? facility
                : null;
        Check(
            equipmentFacility != null,
            "EQUIPMENT_FACILITY_SELECTED_" + key,
            equipmentFacility != null
                ? equipmentFacility.BuildingData?.objectName
                : "missing");
    }

    private BuildableObject RequireEquipmentFacility(string key)
    {
        if (equipmentFacilities.TryGetValue(
                key,
                out BuildableObject facility)
            && facility != null)
        {
            return facility;
        }
        throw new InvalidOperationException(
            "Equipment UI matrix facility is missing: " + key);
    }

    private EquipmentModuleInstance FindModule(string moduleInstanceId)
    {
        return equipment.ModuleInstances.FirstOrDefault(module =>
            module != null
            && string.Equals(
                module.instanceId,
                moduleInstanceId,
                StringComparison.Ordinal));
    }

    private void RouteModuleToFacility(
        EquipmentModuleInstance module,
        string facilityKey,
        string checkId)
    {
        BuildableObject facility = RequireEquipmentFacility(facilityKey);
        string destinationId = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(facility);
        string stackId = module?.sourceStackId ?? string.Empty;
        bool routed = physicalItems.TryRouteStackToDestination(
            stackId,
            WorldItemStackState.FacilityBuffer,
            destinationId,
            facility.centerPos,
            out string failureReason);
        Check(
            routed,
            checkId,
            routed
                ? $"module={module?.instanceId}; stack={stackId}; "
                    + $"destination={destinationId}"
                : failureReason);
        CheckModuleInFacilityBuffer(module, facilityKey, checkId + "_STATE");
    }

    private void RouteEquipmentToFacility(
        string equipmentInstanceId,
        string facilityKey,
        string checkId)
    {
        BuildableObject facility = RequireEquipmentFacility(facilityKey);
        string destinationId = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(facility);
        CombatEquipmentInstance instance = equipment.Instances.FirstOrDefault(
            candidate => candidate != null
                && string.Equals(
                    candidate.instanceId,
                    equipmentInstanceId,
                    StringComparison.Ordinal));
        string stackId = instance?.sourceStackId ?? string.Empty;
        bool routed = physicalItems.TryRouteStackToDestination(
            stackId,
            WorldItemStackState.FacilityBuffer,
            destinationId,
            facility.centerPos,
            out string failureReason);
        WorldItemStackSnapshot delivered = physicalItems.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && stack.StackId == stackId
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
        Check(
            routed && delivered != null,
            checkId,
            routed
                ? $"equipment={equipmentInstanceId}; stack={stackId}; "
                    + $"destination={destinationId}"
                : failureReason);
    }

    private void CheckModuleInFacilityBuffer(
        EquipmentModuleInstance module,
        string facilityKey,
        string checkId)
    {
        BuildableObject facility = RequireEquipmentFacility(facilityKey);
        string destinationId = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(facility);
        WorldItemStackSnapshot stack = physicalItems.GetAllStacks()
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.StackId,
                    module?.sourceStackId,
                    StringComparison.Ordinal));
        bool delivered = module != null
            && !string.IsNullOrWhiteSpace(module.sourceStackId)
            && stack != null
            && string.Equals(
                stack.ItemInstanceId,
                module.instanceId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.ItemId,
                PhysicalItemIds.ForEquipmentModule(),
                StringComparison.Ordinal)
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal);
        Check(
            delivered,
            checkId,
            $"module={module?.instanceId}; stack={module?.sourceStackId}; "
            + $"state={stack?.State}; destination={stack?.DestinationId}; "
            + $"expected={destinationId}");
    }

    private void CheckFacilityCommandVisibility(
        string facilityKey,
        IReadOnlyCollection<string> requiredPrefixes,
        Vector2Int resolution)
    {
        HashSet<string> allowed = GetAllowedCommandPrefixes(facilityKey);
        string[] visible = equipmentContent != null
            ? equipmentContent.GetComponentsInChildren<Button>(false)
                .Where(button => button != null
                    && button.gameObject.activeInHierarchy)
                .Select(button => button.name)
                .Where(name => ProgressionCommandPrefixes.Any(prefix =>
                    name.StartsWith(prefix, StringComparison.Ordinal)))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();
        string[] missing = (requiredPrefixes ?? Array.Empty<string>())
            .Where(prefix => !visible.Any(name =>
                name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();
        string[] forbidden = visible
            .Where(name => !allowed.Any(prefix =>
                name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();
        Check(
            missing.Length == 0 && forbidden.Length == 0,
            $"EQUIPMENT_FACILITY_COMMANDS_{facilityKey}_{Key(resolution)}",
            $"visible={string.Join(",", visible)}; "
            + $"missing={string.Join(",", missing)}; "
            + $"forbidden={string.Join(",", forbidden)}");
    }

    private static HashSet<string> GetAllowedCommandPrefixes(string facilityKey)
    {
        string[] prefixes = facilityKey switch
        {
            "appraisal" => new[] { "EquipmentModuleAppraise_" },
            "restoration" => new[] { "EquipmentModuleRestore_" },
            "precision-fitting" => new[]
            {
                "EquipmentModuleInstall_",
                "EquipmentModuleRemove_"
            },
            "rune-tuning" => new[] { "EquipmentModuleTune_" },
            "lineage" => new[]
            {
                "EquipmentLineageSource_",
                "EquipmentLineageTarget_",
                "EquipmentLineageSeal_",
                "EquipmentLineageConfirm"
            },
            _ => Array.Empty<string>()
        };
        return prefixes.ToHashSet(StringComparer.Ordinal);
    }

    private void RouteLineageInputsToFacility()
    {
        if (!equipmentFacilities.TryGetValue(
                "lineage",
                out BuildableObject lineageFacility)
            || lineageFacility == null)
        {
            Check(false, "LINEAGE_FACILITY_BUFFER_ROUTE", "facility missing");
            return;
        }

        string destinationId = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(lineageFacility);
        CombatEquipmentInstance source = equipment.Instances.FirstOrDefault(
            instance => instance.instanceId == lineageSourceId);
        CombatEquipmentInstance target = equipment.Instances.FirstOrDefault(
            instance => instance.instanceId == lineageTargetId);
        string[] stackIds =
        {
            source?.sourceStackId ?? string.Empty,
            target?.sourceStackId ?? string.Empty,
            lineageSealStackId
        };
        List<string> routeFailures = new();
        foreach (string stackId in stackIds)
        {
            if (!physicalItems.TryRouteStackToDestination(
                    stackId,
                    WorldItemStackState.FacilityBuffer,
                    destinationId,
                    lineageFacility.centerPos,
                    out string failureReason))
            {
                routeFailures.Add(stackId + ":" + failureReason);
            }
        }
        Check(
            routeFailures.Count == 0,
            "LINEAGE_FACILITY_BUFFER_ROUTE",
            routeFailures.Count == 0
                ? $"destination={destinationId}; stacks={string.Join(",", stackIds)}"
                : string.Join(" | ", routeFailures));
    }

    private void CheckLineageInputsInFacilityBuffer(Vector2Int resolution)
    {
        if (!equipmentFacilities.TryGetValue(
                "lineage",
                out BuildableObject lineageFacility)
            || lineageFacility == null)
        {
            Check(false, $"LINEAGE_BUFFER_{Key(resolution)}", "facility missing");
            return;
        }

        string destinationId = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(lineageFacility);
        HashSet<string> expected = new(
            new[]
            {
                equipment.Instances.FirstOrDefault(instance =>
                    instance.instanceId == lineageSourceId)?.sourceStackId,
                equipment.Instances.FirstOrDefault(instance =>
                    instance.instanceId == lineageTargetId)?.sourceStackId,
                lineageSealStackId
            }.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        WorldItemStackSnapshot[] delivered = physicalItems.GetAllStacks()
            .Where(stack => stack != null
                && expected.Contains(stack.StackId)
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .ToArray();
        Check(
            expected.Count == 3 && delivered.Length == 3,
            $"LINEAGE_BUFFER_{Key(resolution)}",
            $"destination={destinationId}; expected={expected.Count}; "
            + $"delivered={string.Join(",", delivered.Select(stack => stack.StackId))}");
    }

    private void RebuildEquipmentSurface()
    {
        if (equipmentContent == null || equipmentFacility == null)
        {
            return;
        }
        for (int index = equipmentContent.childCount - 1; index >= 0; index--)
        {
            GameObject child = equipmentContent.GetChild(index).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        TMP_FontAsset font = equipmentFont
            ?? TMP_Settings.defaultFontAsset
            ?? FindObjectsByType<TMP_Text>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Select(text => text.font)
                .FirstOrDefault(candidate => candidate != null);
        equipmentPresenter.Render(
            equipmentContent,
            equipmentFacility,
            font,
            message => report.Add("EQUIPMENT_FEEDBACK " + message),
            () => { });
        Canvas.ForceUpdateCanvases();
        equipmentScroll.verticalNormalizedPosition = 0f;
    }

    private IEnumerator SelectResolution(Vector2Int resolution)
    {
        GameViewResolutionController.Select(resolution.x, resolution.y);
        float deadline = Time.realtimeSinceStartup + 4f;
        while ((Screen.width != resolution.x || Screen.height != resolution.y)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Check(
            Screen.width == resolution.x && Screen.height == resolution.y,
            $"RESOLUTION_{Key(resolution)}",
            $"actual={Screen.width}x{Screen.height}");
    }

    private IEnumerator Capture(
        string path,
        Vector2Int resolution,
        string checkId)
    {
        Canvas.ForceUpdateCanvases();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D screenshot =
            PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        bool valid = screenshot != null
            && screenshot.width == resolution.x
            && screenshot.height == resolution.y;
        if (valid)
        {
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
        }
        Check(
            valid && File.Exists(path) && new FileInfo(path).Length > 0,
            checkId,
            screenshot != null
                ? $"size={screenshot.width}x{screenshot.height}; path={path}"
                : "capture was null");
        if (screenshot != null)
        {
            Destroy(screenshot);
        }
    }

    private bool ClickByPrefix(string prefix, string checkId)
    {
        Button button = FindObjectsByType<Button>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.activeInHierarchy
                && candidate.interactable
                && candidate.name.StartsWith(prefix, StringComparison.Ordinal));
        return ClickPointer(button, checkId);
    }

    private bool ClickExactName(string objectName, string checkId)
    {
        Button button = FindObjectsByType<Button>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.activeInHierarchy
                && candidate.interactable
                && string.Equals(
                    candidate.name,
                    objectName,
                    StringComparison.Ordinal));
        return ClickPointer(button, checkId);
    }

    private static string Sanitize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }

    private bool ClickPointer(Button button, string checkId)
    {
        EventSystem eventSystem = EventSystem.current;
        bool targetAvailable = button != null
            && eventSystem != null
            && button.gameObject.activeInHierarchy
            && button.IsInteractable();
        Check(
            targetAvailable,
            checkId + "_TARGET",
            $"button={button?.name ?? "<missing>"}; "
            + $"active={button != null && button.gameObject.activeInHierarchy}; "
            + $"interactable={button != null && button.IsInteractable()}; "
            + $"eventSystem={eventSystem != null}");
        if (!targetAvailable)
        {
            return false;
        }

        RectTransform rect = button.transform as RectTransform;
        bool visibleInViewports = TryBringIntoScrollViewports(
            rect,
            out string viewportDiagnostic);
        Check(
            visibleInViewports,
            checkId + "_VIEWPORT",
            viewportDiagnostic);
        if (!visibleInViewports)
        {
            return false;
        }

        Canvas.ForceUpdateCanvases();
        Camera eventCamera = GetEventCamera(button);
        Vector2 position = RectTransformUtility.WorldToScreenPoint(
            eventCamera,
            rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = position,
            pressPosition = position
        };
        List<RaycastResult> hits = new();
        eventSystem.RaycastAll(pointer, hits);
        RaycastResult top = hits.FirstOrDefault(hit =>
            hit.gameObject != null && hit.gameObject.activeInHierarchy);
        GameObject topHandler = top.gameObject != null
            ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(top.gameObject)
            : null;
        bool targetIsTop = top.gameObject != null
            && (top.gameObject == button.gameObject
                || top.gameObject.transform.IsChildOf(button.transform))
            && topHandler == button.gameObject
            && RectTransformUtility.RectangleContainsScreenPoint(
                rect,
                position,
                eventCamera);
        Check(
            targetIsTop,
            checkId + "_HIT_TEST",
            $"point={position}; hits={hits.Count}; "
            + $"top={top.gameObject?.name ?? "<none>"}; "
            + $"handler={topHandler?.name ?? "<none>"}; expected={button.name}");
        if (!targetIsTop)
        {
            return false;
        }

        bool dispatched = PlayModeVerificationFrameWait.DispatchPointerClick(
            top.gameObject,
            position);
        Check(
            dispatched,
            checkId + "_DISPATCH",
            dispatched
                ? $"top={top.gameObject.name}; point={position}"
                : "pointer dispatch failed");
        return dispatched;
    }

    private static bool TryBringIntoScrollViewports(
        RectTransform target,
        out string diagnostic)
    {
        if (target == null)
        {
            diagnostic = "target RectTransform missing";
            return false;
        }

        ScrollRect[] scrolls = target.GetComponentsInParent<ScrollRect>(false)
            .Where(scroll => scroll != null
                && scroll.content != null
                && target.IsChildOf(scroll.content))
            .ToArray();
        List<string> states = new();
        foreach (ScrollRect scroll in scrolls)
        {
            RectTransform viewport = scroll.viewport
                ?? scroll.transform as RectTransform;
            if (viewport == null)
            {
                diagnostic = $"scroll={scroll.name}; viewport=<missing>";
                return false;
            }

            scroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Canvas.ForceUpdateCanvases();
            for (int pass = 0; pass < 3; pass++)
            {
                Bounds bounds =
                    RectTransformUtility.CalculateRelativeRectTransformBounds(
                        viewport,
                        target);
                Vector2 adjustment = CalculateViewportAdjustment(
                    scroll,
                    viewport.rect,
                    bounds);
                if (adjustment.sqrMagnitude < 0.25f)
                {
                    break;
                }

                scroll.content.anchoredPosition += adjustment;
                scroll.velocity = Vector2.zero;
                Canvas.ForceUpdateCanvases();
            }

            Bounds finalBounds =
                RectTransformUtility.CalculateRelativeRectTransformBounds(
                    viewport,
                    target);
            bool visible = IsFullyInside(finalBounds, viewport.rect);
            states.Add(
                $"{scroll.name}:visible={visible}:"
                + $"target={FormatBounds(finalBounds)}:"
                + $"viewport={viewport.rect}");
            if (!visible)
            {
                diagnostic = string.Join(" | ", states);
                return false;
            }
        }

        diagnostic = scrolls.Length == 0
            ? "no containing ScrollRect; target does not require viewport scrolling"
            : string.Join(" | ", states);
        return true;
    }

    private static Vector2 CalculateViewportAdjustment(
        ScrollRect scroll,
        Rect viewport,
        Bounds target)
    {
        const float tolerance = 0.5f;
        Vector2 adjustment = Vector2.zero;
        if (scroll.horizontal)
        {
            if (target.min.x < viewport.xMin - tolerance)
            {
                adjustment.x = viewport.xMin - target.min.x;
            }
            else if (target.max.x > viewport.xMax + tolerance)
            {
                adjustment.x = viewport.xMax - target.max.x;
            }
        }
        if (scroll.vertical)
        {
            if (target.min.y < viewport.yMin - tolerance)
            {
                adjustment.y = viewport.yMin - target.min.y;
            }
            else if (target.max.y > viewport.yMax + tolerance)
            {
                adjustment.y = viewport.yMax - target.max.y;
            }
        }
        return adjustment;
    }

    private static bool IsFullyInside(Bounds target, Rect viewport)
    {
        const float tolerance = 0.5f;
        return target.min.x >= viewport.xMin - tolerance
            && target.max.x <= viewport.xMax + tolerance
            && target.min.y >= viewport.yMin - tolerance
            && target.max.y <= viewport.yMax + tolerance;
    }

    private static string FormatBounds(Bounds bounds) =>
        $"({bounds.min.x:0.#},{bounds.min.y:0.#})-"
        + $"({bounds.max.x:0.#},{bounds.max.y:0.#})";

    private static Camera GetEventCamera(Button button)
    {
        Canvas canvas = button != null
            ? button.GetComponentInParent<Canvas>()
            : null;
        canvas = canvas != null ? canvas.rootCanvas : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera ?? Camera.main
            : null;
    }

    private static bool IsCloseButton(Button button)
    {
        string label = button?.GetComponentInChildren<TMP_Text>(true)?.text
            ?? string.Empty;
        return label.Contains("닫기", StringComparison.Ordinal)
            || label.Contains("Close", StringComparison.OrdinalIgnoreCase);
    }

    private void CheckSurfaceInsideScreen(
        GameObject surface,
        Vector2Int resolution,
        string checkId)
    {
        RectTransform rect = surface != null
            ? surface.GetComponent<RectTransform>()
            : null;
        if (rect == null)
        {
            Check(false, checkId, "surface rect missing");
            return;
        }
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        bool inside = corners.All(corner =>
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner);
            return point.x >= -2f
                && point.y >= -2f
                && point.x <= resolution.x + 2f
                && point.y <= resolution.y + 2f;
        });
        Check(inside, checkId, string.Join(",", corners.Select(value => value.ToString("F0"))));
    }

    private IEnumerator CompleteOwnerSelectionIfVisible()
    {
        Button owner = FindObjectsByType<Button>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null
                && candidate.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
        if (owner != null)
        {
            ClickPointer(owner, "OWNER_SELECTION_POINTER");
            yield return null;
            yield return null;
        }
    }

    private void RestoreRuntimeState()
    {
        if (physicalItems != null && physicalSnapshot != null)
        {
            TryRestoreStep(
                "PHYSICAL_STATE_RESTORE_EXCEPTION",
                () => physicalItems.Restore(physicalSnapshot));
        }
        if (equipment != null && equipmentSnapshot != null)
        {
            TryRestoreStep(
                "EQUIPMENT_STATE_RESTORE_EXCEPTION",
                () => equipment.PublishRestoreCandidate(
                    equipment.BuildRestoreCandidate(equipmentSnapshot)));
        }
        TryRestoreStep(
            "RESEARCH_STATE_RESTORE_EXCEPTION",
            () => RestoreSection(
                researchSaveSection,
                researchSnapshot,
                "RESEARCH_STATE_RESTORED"));
        TryRestoreStep(
            "OFFENSE_STATE_RESTORE_EXCEPTION",
            () => RestoreSection(
                offenseSaveSection,
                offenseSnapshot,
                "OFFENSE_STATE_RESTORED"));
    }

    private void TryRestoreStep(string checkId, Action restore)
    {
        try
        {
            restore();
        }
        catch (Exception exception)
        {
            Check(false, checkId, exception.ToString());
        }
    }

    private void RestoreSection(
        IDungeonSaveSection section,
        string snapshot,
        string checkId)
    {
        if (section == null || string.IsNullOrWhiteSpace(snapshot))
        {
            return;
        }

        DungeonGameRestoreReport restoreReport = new();
        section.Restore(snapshot, section.SectionVersion, restoreReport);
        bool restored = restoreReport.Success
            && string.Equals(
                section.Capture(),
                snapshot,
                StringComparison.Ordinal);
        Check(
            restored,
            checkId,
            restoreReport.Success
                ? "canonical snapshot matched"
                : string.Join(" | ", restoreReport.Errors));
    }

    private void DestroySurface()
    {
        if (equipmentSurface != null)
        {
            equipmentSurface.SetActive(false);
            Destroy(equipmentSurface);
            equipmentSurface = null;
            equipmentContent = null;
            equipmentScroll = null;
        }
        foreach (BuildableObject facility in equipmentFacilities.Values)
        {
            if (facility == null)
            {
                continue;
            }
            facility.gameObject.SetActive(false);
            Destroy(facility.gameObject);
        }
        equipmentFacilities.Clear();
        equipmentFacility = null;
    }

    private void CleanupCreatedObjects()
    {
        DestroySurface();
        foreach (UnityEngine.Object value in created)
        {
            if (value != null)
            {
                Destroy(value);
            }
        }
        created.Clear();
        OffenseJourneyPlayModeFacade.Cleanup();
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
        else if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            errors.Add(string.IsNullOrWhiteSpace(stackTrace)
                ? condition
                : condition + "\n" + stackTrace);
        }
    }

    private void Check(bool passed, string id, string detail)
    {
        string line = $"{(passed ? "PASS" : "FAIL")} {id} {detail}";
        report.Add(line);
        if (!passed)
        {
            failures.Add(line);
        }
    }

    private void Finish()
    {
        Application.logMessageReceived -= CaptureLog;
        Check(errors.Count == 0, "CONSOLE_ERRORS", $"count={errors.Count}");
        Check(warnings.Count == 0, "CONSOLE_WARNINGS", $"count={warnings.Count}");
        report.Insert(0, $"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}");
        report.Insert(1, EquipmentExpeditionUiMatrixPlayModeVerifier.FacilityFlowMarker);
        if (errors.Count > 0)
        {
            report.Add("ERRORS " + string.Join(" | ", errors));
        }
        if (warnings.Count > 0)
        {
            report.Add("WARNINGS " + string.Join(" | ", warnings));
        }
        File.WriteAllLines(
            EquipmentExpeditionUiMatrixPlayModeVerifier.ReportPath,
            report);
        File.Delete(EquipmentExpeditionUiMatrixPlayModeVerifier.RequestPath);
        EditorApplication.ExitPlaymode();
    }

    private static string Key(Vector2Int resolution) =>
        $"{resolution.x}x{resolution.y}";
}
#endif
