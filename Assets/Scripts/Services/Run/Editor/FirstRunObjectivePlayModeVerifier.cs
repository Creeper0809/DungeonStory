using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class FirstRunObjectivePlayModeVerifier
{
    public const string RequestPath = "Temp/first-run-objective.request";
    public const string ReportPath = "Temp/first-run-objective-report.txt";
    public const string ScreenshotPath = "Temp/first-run-objective.png";
    public const string PhysicalFlowScreenshotPath = "Artifacts/QA/research-blueprint-physical-flow.png";
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    private const string PersistenceSnapshotId = "first-run-objective";
    private const string StartSceneLeaseOwnerId =
        "qa:first-run-objective";
    private const string PersistenceOwnedKey =
        "DungeonStory.FirstRunObjective.PersistenceOwned";

    private static bool runnerCreated;

    static FirstRunObjectivePlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall -= RecoverStaleStartSceneLeaseIfOrphaned;
        EditorApplication.delayCall += RecoverStaleStartSceneLeaseIfOrphaned;
    }

    [MenuItem("DungeonStory/Debug/QA/Request First Run Objective Verification")]
    public static void RequestRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        runnerCreated = false;
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
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

        try
        {
            if (SessionState.GetBool(PersistenceOwnedKey, false)
                && !PlayModeVerificationPersistenceSnapshot.Exists(
                    PersistenceSnapshotId))
            {
                SessionState.EraseBool(PersistenceOwnedKey);
            }

            if (!DungeonFinalPlayModeAcceptanceRequestFacade
                    .IsPersistenceCoordinatorActive
                && !SessionState.GetBool(PersistenceOwnedKey, false))
            {
                PlayModeVerificationPersistenceSnapshot.CaptureCurrent(
                    PersistenceSnapshotId);
                SessionState.SetBool(PersistenceOwnedKey, true);
            }

            ApplyTitleStartSceneOverride();
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            FailBeforePlay("EDITOR_BOOT_PREPARE_FAILED: " + exception);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            RestoreTitleStartSceneOverride();
            RestoreOwnedPersistence();
            PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
            if (File.Exists(RequestPath))
            {
                File.WriteAllText(
                    ReportPath,
                    "FIRST_RUN_OBJECTIVE FAIL\n"
                    + "[FAIL] PLAYMODE_ABORTED verifier returned to EditMode before completion\n");
                File.Delete(RequestPath);
            }
            return;
        }

        if (change == PlayModeStateChange.ExitingPlayMode)
        {
            return;
        }

        if (change == PlayModeStateChange.EnteredPlayMode
            && !runnerCreated
            && File.Exists(RequestPath))
        {
            RestoreTitleStartSceneOverride();
            if (!string.Equals(
                    SceneManager.GetActiveScene().path,
                    TitleScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(
                    ReportPath,
                    "FIRST_RUN_OBJECTIVE FAIL\n"
                    + "[FAIL] BOOT_TITLE_SCENE active="
                    + SceneManager.GetActiveScene().path
                    + "\n");
                File.Delete(RequestPath);
                EditorApplication.ExitPlaymode();
                return;
            }

            runnerCreated = true;
            GameObject runnerObject = new GameObject("First Run Objective Verification Runner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<FirstRunObjectiveVerificationRunner>();
        }
    }

    private static void ApplyTitleStartSceneOverride()
    {
        string[] requiredSceneNames =
        {
            DungeonSceneNavigator.TitleSceneName,
            DungeonSceneNavigator.PreparationSceneName,
            DungeonSceneNavigator.GameplaySceneName
        };
        HashSet<string> enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
            .ToHashSet(StringComparer.Ordinal);
        string[] missing = requiredSceneNames
            .Where(sceneName => !enabledScenes.Contains(sceneName))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Required product boot scenes are not enabled: "
                + string.Join(", ", missing));
        }

        PlayModeVerificationStartSceneLease.Acquire(
            StartSceneLeaseOwnerId,
            TitleScenePath);
    }

    private static void RestoreTitleStartSceneOverride()
    {
        if (PlayModeVerificationStartSceneLease.IsOwnedBy(
                StartSceneLeaseOwnerId))
        {
            PlayModeVerificationStartSceneLease.RestoreOwned(
                StartSceneLeaseOwnerId);
        }
    }

    private static void RestoreOwnedPersistence()
    {
        if (!SessionState.GetBool(PersistenceOwnedKey, false))
        {
            return;
        }

        PlayModeVerificationPersistenceSnapshot.Restore(
            PersistenceSnapshotId);
        SessionState.EraseBool(PersistenceOwnedKey);
    }

    private static void FailBeforePlay(string detail)
    {
        RestoreTitleStartSceneOverride();
        try
        {
            RestoreOwnedPersistence();
        }
        catch (Exception restoreException)
        {
            detail += " | PERSISTENCE_RESTORE_FAILED: " + restoreException;
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(
            ReportPath,
            "FIRST_RUN_OBJECTIVE FAIL\n"
            + "[FAIL] EDITOR_BOOT_GUARD " + detail + "\n");
        File.Delete(RequestPath);
        Debug.LogError(detail);
    }

    private static void RecoverStaleStartSceneLeaseIfOrphaned()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || File.Exists(RequestPath)
            || !PlayModeVerificationStartSceneLease.IsOwnedBy(
                StartSceneLeaseOwnerId))
        {
            return;
        }

        try
        {
            PlayModeVerificationStartSceneLease.RestoreOwned(
                StartSceneLeaseOwnerId);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Failed to recover an orphaned first-run start-scene lease: "
                + exception);
        }
    }
}

public sealed class FirstRunObjectiveVerificationRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private const float RuntimeReadyTimeoutSeconds = 45f;
    private const float PartyReadyTimeoutSeconds = 20f;

    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private float originalTimeScale;

    private IEnumerator Start()
    {
        yield return Run();
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(FirstRunObjectivePlayModeVerifier.ReportPath, "FIRST_RUN_OBJECTIVE IN_PROGRESS\n");
        Application.logMessageReceived += CaptureLog;
        ConfigureInput();
        originalTimeScale = Time.timeScale;

        try
        {
            yield return EnsureProductBoot();
            yield return WaitForSceneTransitionInputRelease();
            DungeonRuntimeLifetimeScope scope = FindScope();
            Check(scope != null, "DI_SCOPE", "active game container resolved");
            if (scope == null)
            {
                yield break;
            }

            ResetFirstRunMetaForVerification(scope);
            IFirstRunObjectiveRuntime objective = scope.Container.Resolve<IFirstRunObjectiveRuntime>();
            Check(objective != null, "OBJECTIVE_RUNTIME", "runtime resolved");
            objective?.RefreshNow();
            Check(
                objective != null && objective.CurrentObjective == FirstRunObjectiveId.AcquireBlueprint,
                "INITIAL_OBJECTIVE",
                objective != null ? objective.CurrentObjective.ToString() : "missing");
            CheckNonBlocking(objective);
            CheckPanelBounds(objective);

            ProgressionSceneRuntimeReferences progressionRuntimes =
                scope.Container.Resolve<ProgressionSceneRuntimeReferences>();
            BlueprintResearchRuntime research = progressionRuntimes.BlueprintResearch;
            DailyFacilityShopRuntime shop = progressionRuntimes.FacilityShop;
            IWorldItemStackRuntime itemRuntime =
                scope.Container.Resolve<IWorldItemStackRuntime>();
            IGameSessionStateProvider gameDataProvider =
                scope.Container.Resolve<IGameSessionStateProvider>();
            gameDataProvider.TryGetSessionState(out GameSessionState gameData);
            int queueCountBefore = research?.State.Projects.Queue.Count ?? -1;
            int moneyBefore = gameData?.holdingMoney?.Value ?? -1;

            Button shopTab = FindTopTabButton(TabId.Shop);
            yield return Click(shopTab, "shop tab");
            yield return new WaitForSecondsRealtime(0.25f);

            int blueprintOfferIndex = FindBlueprintOfferIndex(shop);
            FacilityBlueprintSO purchasedBlueprint = GetBlueprintOffer(shop, blueprintOfferIndex);
            int purchaseAttempts = 0;
            while (purchasedBlueprint != null
                && !shop.UnlockState.IsBlueprintAcquired(purchasedBlueprint.id)
                && purchaseAttempts < 6)
            {
                Button blueprintButton = FindButton(
                    $"P0Action_ShopDaily_{blueprintOfferIndex}");
                yield return Click(
                    blueprintButton,
                    $"daily blueprint attempt {purchaseAttempts + 1}");
                purchaseAttempts++;
                yield return new WaitForSecondsRealtime(0.5f);
            }

            bool acquired = purchasedBlueprint != null
                && shop.UnlockState.IsBlueprintAcquired(purchasedBlueprint.id);
            float physicalDeadline = Time.realtimeSinceStartup + 2f;
            bool physicalBlueprintExists = false;
            while (Time.realtimeSinceStartup < physicalDeadline)
            {
                physicalBlueprintExists = purchasedBlueprint != null
                    && itemRuntime.GetAllStacks().Any(stack => stack != null
                        && stack.Quantity > 0
                        && string.Equals(
                            stack.ItemId,
                            purchasedBlueprint.PhysicalItemId,
                            StringComparison.Ordinal));
                if (physicalBlueprintExists)
                {
                    break;
                }
                yield return null;
            }

            int queueCountAfter = research?.State.Projects.Queue.Count ?? -1;
            int moneyAfter = gameData?.holdingMoney?.Value ?? -1;
            objective.RefreshNow();
            Check(
                blueprintOfferIndex >= 0
                    && purchasedBlueprint != null
                    && acquired
                    && physicalBlueprintExists
                    && queueCountAfter == queueCountBefore,
                "PUBLIC_BLUEPRINT_PURCHASE",
                $"offer={blueprintOfferIndex}; blueprint={purchasedBlueprint?.id}; "
                + $"attempts={purchaseAttempts}; acquired={acquired}; "
                + $"physical={physicalBlueprintExists}; "
                + $"money={moneyBefore}->{moneyAfter}; "
                + $"cost={(blueprintOfferIndex >= 0 && shop != null ? shop.CurrentDailyOffers[blueprintOfferIndex].Cost : -1)}; "
                + $"queue={queueCountBefore}->{queueCountAfter}");
            Check(
                objective.CurrentObjective == FirstRunObjectiveId.CompleteResearch,
                "POST_PURCHASE_OBJECTIVE",
                objective.CurrentObjective.ToString());

            yield return Click(shopTab, "shop tab close");
            yield return new WaitForSecondsRealtime(0.2f);
            yield return VerifyPhysicalBlueprintResearchFlow(
                scope,
                research,
                itemRuntime,
                purchasedBlueprint);
            CheckNonBlocking(objective);
            CheckPanelBounds(objective);
            yield return CaptureScreen();
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            Application.logMessageReceived -= CaptureLog;
            TeardownInput();

            report.Add($"capturedErrors={errors.Count}; capturedWarnings={warnings.Count}");
            foreach (string error in errors) report.Add("[CONSOLE ERROR] " + error.Replace('\n', ' '));
            foreach (string warning in warnings) report.Add("[CONSOLE WARNING] " + warning.Replace('\n', ' '));
            bool passed = report.All(line => !line.StartsWith("[FAIL]", StringComparison.Ordinal))
                && errors.Count == 0
                && warnings.Count == 0;
            report.Insert(0, passed ? "FIRST_RUN_OBJECTIVE PASS" : "FIRST_RUN_OBJECTIVE FAIL");
            File.WriteAllLines(FirstRunObjectivePlayModeVerifier.ReportPath, report);
            File.Delete(FirstRunObjectivePlayModeVerifier.RequestPath);
            EditorApplication.ExitPlaymode();
        }
    }

    private IEnumerator VerifyPhysicalBlueprintResearchFlow(
        DungeonRuntimeLifetimeScope scope,
        BlueprintResearchRuntime research,
        IWorldItemStackRuntime itemRuntime,
        FacilityBlueprintSO blueprint)
    {
        IFirstRunObjectiveRuntime objectiveRuntime =
            scope.Container.Resolve<IFirstRunObjectiveRuntime>();
        IResearchBlueprintArchiveQuery archiveQuery =
            scope.Container.Resolve<IResearchBlueprintArchiveQuery>();
        IResearchProjectCatalog projectCatalog =
            scope.Container.Resolve<IResearchProjectCatalog>();
        IResearchQueueCommandService queueCommands =
            scope.Container.Resolve<IResearchQueueCommandService>();
        IWorldItemHaulPlanningService haulPlanning =
            scope.Container.Resolve<IWorldItemHaulPlanningService>();
        IFacilityBufferDestinationClaimQuery destinationClaims =
            scope.Container.Resolve<IFacilityBufferDestinationClaimQuery>();
        IFacilityBufferMassCapacityQuery archiveCapacities =
            scope.Container.Resolve<IFacilityBufferMassCapacityQuery>();
        IFacilityBufferPhysicalOccupancyQuery archiveOccupancy =
            scope.Container.Resolve<IFacilityBufferPhysicalOccupancyQuery>();
        IPhysicalItemMassQuery physicalMass =
            scope.Container.Resolve<IPhysicalItemMassQuery>();
        IResearchDurableEquipmentWorkPolicyQuery durableResearchPolicies =
            scope.Container.Resolve<IResearchDurableEquipmentWorkPolicyQuery>();
        IDurableFacilityEquipmentPolicyQuery durableEquipmentPolicies =
            scope.Container.Resolve<IDurableFacilityEquipmentPolicyQuery>();
        IDurableFacilityEquipmentSlotCommand durableEquipmentSlots =
            scope.Container.Resolve<IDurableFacilityEquipmentSlotCommand>();
        IDurableFacilityEquipmentSlotQuery durableEquipmentSlotQuery =
            scope.Container.Resolve<IDurableFacilityEquipmentSlotQuery>();
        IRoomLayoutCache roomLayoutCache =
            scope.Container.Resolve<IRoomLayoutCache>();
        IReadOnlyList<BuildableObject> archives = archiveQuery.GetValidArchives();
        Check(
            archives.Count > 0,
            "RESEARCH_ARCHIVE_READY",
            archives.Count > 0
                ? $"{archives[0].BuildingData?.objectName}@{archives[0].centerPos}"
                : DescribeArchiveCandidates(roomLayoutCache));
        if (archives.Count == 0 || research == null || blueprint == null)
        {
            yield break;
        }

        WorldItemStackSnapshot blueprintStack = itemRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && stack.Quantity > 0
                && string.Equals(stack.ItemId, blueprint.PhysicalItemId, StringComparison.Ordinal));
        Check(
            blueprintStack != null && blueprintStack.State == WorldItemStackState.Loose,
            "BLUEPRINT_AT_DROPOFF",
            blueprintStack != null
                ? $"state={blueprintStack.State}; pos={blueprintStack.Position}"
                : "stack missing");
        if (blueprintStack == null)
        {
            yield break;
        }

        float assignmentStartedAt = Time.realtimeSinceStartup;
        string blueprintStackId = blueprintStack.StackId;
        while (Time.realtimeSinceStartup - assignmentStartedAt < 4f)
        {
            blueprintStack = itemRuntime.GetAllStacks()
                .FirstOrDefault(stack => stack != null
                    && string.Equals(stack.StackId, blueprintStackId, StringComparison.Ordinal));
            if (blueprintStack != null
                && !string.IsNullOrWhiteSpace(blueprintStack.DestinationId))
            {
                break;
            }
            yield return null;
        }

        if (blueprintStack == null)
        {
            blueprintStack = itemRuntime.GetAllStacks()
                .FirstOrDefault(stack => stack != null
                    && stack.Quantity > 0
                    && string.Equals(
                        stack.ItemId,
                        blueprint.PhysicalItemId,
                        StringComparison.Ordinal));
        }

        string expectedDestination = ResearchBlueprintArchiveQuery.GetDestinationId(archives[0]);
        bool exactDestinationClaim = destinationClaims.TryGetClaim(
                expectedDestination,
                archives[0].centerPos,
                out FacilityBufferDestinationClaim archiveClaim)
            && string.Equals(
                archiveClaim.OwnerDomain,
                ResearchBlueprintArchiveDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                archiveClaim.OwnerOperationId,
                expectedDestination,
                StringComparison.Ordinal)
            && string.Equals(
                archiveClaim.OwnerFacilityId,
                archives[0].RequirePersistentInstanceId().Value,
                StringComparison.Ordinal)
            && archiveClaim.AnchorKind
                == FacilityBufferDestinationAnchorKind.LiveBuilding
            && archiveClaim.AdmissionPolicy
                == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired;
        Check(
            exactDestinationClaim,
            "BLUEPRINT_ARCHIVE_DESTINATION_CLAIM_EXACT",
            exactDestinationClaim
                ? $"destination={expectedDestination}; facility={archiveClaim.OwnerFacilityId}; drop={archiveClaim.DropPosition}"
                : $"destination={expectedDestination}; drop={archives[0].centerPos}; claim=missing-or-mismatched");
        if (!exactDestinationClaim)
        {
            yield break;
        }
        bool exactCapacity = archiveCapacities.TryGetCapacity(
                expectedDestination,
                archives[0].centerPos,
                out FacilityBufferMassCapacitySnapshot archiveCapacity)
            && archiveCapacity.Profile.MaxMassGrams == 1_200L
            && archiveCapacity.Profile.CapacityRevision
                == ResearchBlueprintArchiveDestinationAuthority
                    .CapacitySchemaRevision
            && string.Equals(
                archiveCapacity.Profile.OwnerDomain,
                archiveClaim.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                archiveCapacity.Profile.OwnerOperationId,
                archiveClaim.OwnerOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                archiveCapacity.Profile.OwnerFacilityId,
                archiveClaim.OwnerFacilityId,
                StringComparison.Ordinal)
            && archiveCapacity.Profile.DropPosition == archiveClaim.DropPosition;
        Check(
            exactCapacity,
            "BLUEPRINT_ARCHIVE_CAPACITY_EXACT",
            exactCapacity
                ? $"max={archiveCapacity.Profile.MaxMassGrams}g; revision={archiveCapacity.Profile.CapacityRevision}; reserved={archiveCapacity.ReservedMassGrams}g"
                : $"destination={expectedDestination}; profile=missing-or-mismatched");
        if (!exactCapacity)
        {
            yield break;
        }
        long blueprintUnitMass = physicalMass.GetDefinitionUnitMass(
            (ItemDefinitionId)blueprint.PhysicalItemId).Value;
        Check(
            blueprintUnitMass == 150L,
            "BLUEPRINT_UNIT_MASS_EXACT",
            $"item={blueprint.PhysicalItemId}; mass={blueprintUnitMass}g");
        ResearchBlueprintArchiveStatus assignmentStatus = archiveQuery.GetStatus(blueprint);
        bool deliveryAssigned = assignmentStatus.IsArchived
            || assignmentStatus.IsInTransit
            || (blueprintStack != null
                && string.Equals(
                    blueprintStack.DestinationId,
                    expectedDestination,
                    StringComparison.Ordinal));
        Check(
            deliveryAssigned,
            "BLUEPRINT_ARCHIVE_DELIVERY_ASSIGNED",
            blueprintStack != null
                ? $"destination={blueprintStack.DestinationId}; expected={expectedDestination}"
                : $"status={assignmentStatus.Location}; blocker={assignmentStatus.Blocker}");
        if (!deliveryAssigned)
        {
            yield break;
        }

        CharacterActor hauler = null;
        AIBrain brain = null;
        bool brainWasEnabled = false;
        bool haulerWasAiPaused = false;
        CharacterActor previewHauler = FindHauler();
        ResearchBlueprintArchiveStatus planStatus = archiveQuery.GetStatus(blueprint);
        bool productionHaulAlreadyStarted = planStatus.IsArchived
            || blueprintStack.HasReservations
            || itemRuntime.GetCommittedHaulDeliveryQuantity(
                expectedDestination,
                blueprint.PhysicalItemId) > 0;
        bool exactPlanReady = productionHaulAlreadyStarted;
        string planDetail;
        if (!productionHaulAlreadyStarted && previewHauler != null)
        {
            bool previewed = haulPlanning.TryPreviewBestPlan(
                previewHauler,
                out WorldItemHaulPlan previewPlan,
                out string previewFailure);
            exactPlanReady = previewed
                && previewPlan != null
                && string.Equals(
                    previewPlan.PrimaryDestinationId,
                    expectedDestination,
                    StringComparison.Ordinal)
                && previewPlan.ReservedStackQuantities.Any(candidate =>
                    string.Equals(
                        candidate.StackId,
                        blueprintStack.StackId,
                        StringComparison.Ordinal));
            planDetail = previewed && previewPlan != null
                ? $"actor={previewHauler.name}; destination={previewPlan.PrimaryDestinationId}; "
                    + $"stacks={string.Join(",", previewPlan.ReservedStackQuantities.Select(candidate => candidate.StackId))}"
                : $"actor={previewHauler.name}; failure={previewFailure}";
        }
        else
        {
            planDetail = productionHaulAlreadyStarted
                ? $"status={planStatus.Location}; reserved={blueprintStack.ReservedQuantity}"
                : "eligible hauler missing";
        }
        Time.timeScale = 8f;
        float haulStartedAt = Time.realtimeSinceStartup;
        bool exactHaulOwnershipObserved = false;
        bool committedCarriedMassObserved = false;
        while (Time.realtimeSinceStartup - haulStartedAt < 24f)
        {
            ResearchBlueprintArchiveStatus status = archiveQuery.GetStatus(blueprint);
            WorldItemStackSnapshot currentBlueprintStack = itemRuntime.GetAllStacks()
                .FirstOrDefault(stack => stack != null
                    && string.Equals(
                        stack.StackId,
                        blueprintStackId,
                        StringComparison.Ordinal));
            bool liveAiHaul = CharacterActorCollection.DistinctByGameObject(
                    FindObjectsByType<CharacterActor>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None))
                .Any(actor => actor != null
                    && actor.Brain?.bestAction?.actionset is AIHaul
                    && actor.GetComponent<AbilityHaul>()?.IsHauling == true);
            exactHaulOwnershipObserved |= liveAiHaul
                && (currentBlueprintStack?.HasReservations == true
                    || itemRuntime.GetCommittedHaulDeliveryQuantity(
                        expectedDestination,
                        blueprint.PhysicalItemId) > 0);
            int committedQuantity = itemRuntime.GetCommittedHaulDeliveryQuantity(
                expectedDestination,
                blueprint.PhysicalItemId);
            if (committedQuantity > 0)
            {
                FacilityBufferPhysicalOccupancySnapshot inTransit =
                    archiveOccupancy.Capture(expectedDestination);
                committedCarriedMassObserved |=
                    inTransit.CommittedCarriedMassGrams == blueprintUnitMass
                    && inTransit.TotalMassGrams == blueprintUnitMass;
            }
            if (!exactPlanReady && exactHaulOwnershipObserved)
            {
                planDetail = "production AIHaul ownership committed for "
                    + blueprintStackId;
            }
            exactPlanReady |= exactHaulOwnershipObserved;
            if (!exactPlanReady)
            {
                CharacterActor availableHauler = FindHauler();
                if (availableHauler != null)
                {
                    bool previewed = haulPlanning.TryPreviewBestPlan(
                        availableHauler,
                        out WorldItemHaulPlan previewPlan,
                        out string previewFailure);
                    exactPlanReady = previewed
                        && previewPlan != null
                        && string.Equals(
                            previewPlan.PrimaryDestinationId,
                            expectedDestination,
                            StringComparison.Ordinal)
                        && previewPlan.ReservedStackQuantities.Any(candidate =>
                            string.Equals(
                                candidate.StackId,
                                blueprintStackId,
                                StringComparison.Ordinal));
                    planDetail = previewed && previewPlan != null
                        ? $"actor={availableHauler.name}; destination={previewPlan.PrimaryDestinationId}; "
                            + $"stacks={string.Join(",", previewPlan.ReservedStackQuantities.Select(candidate => candidate.StackId))}"
                        : $"actor={availableHauler.name}; failure={previewFailure}";
                }
            }
            if (status.IsArchived)
            {
                break;
            }
            yield return null;
        }

        Check(
            exactPlanReady,
            "BLUEPRINT_HAUL_PLAN_READY",
            exactPlanReady
                ? planDetail
                : planDetail + "; characters=" + DescribeCharacterPublicationState());
        Check(
            exactHaulOwnershipObserved,
            "BLUEPRINT_AI_HAUL_OWNERSHIP_OBSERVED",
            $"destination={expectedDestination}; stack={blueprintStackId}");
        Check(
            committedCarriedMassObserved,
            "BLUEPRINT_ARCHIVE_COMMITTED_CARRIED_MASS",
            $"destination={expectedDestination}; expected={blueprintUnitMass}g");
        bool archived = archiveQuery.GetStatus(blueprint).IsArchived;
        Check(
            archived,
            "BLUEPRINT_AI_HAUL_TO_ARCHIVE",
            archived
                ? $"elapsed={Time.realtimeSinceStartup - haulStartedAt:0.0}s; {archiveQuery.GetStatus(blueprint).Location}"
                : DescribeBlueprintStack(itemRuntime, blueprint)
                    + "; characters=" + DescribeCharacterPublicationState());
        if (!archived)
        {
            yield break;
        }
        WorldItemStackSnapshot archivedStack = itemRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && stack.Quantity > 0
                && string.Equals(
                    stack.ItemId,
                    blueprint.PhysicalItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    expectedDestination,
                    StringComparison.Ordinal));
        int committedAfterArchive = itemRuntime.GetCommittedHaulDeliveryQuantity(
            expectedDestination,
            blueprint.PhysicalItemId);
        FacilityBufferPhysicalOccupancySnapshot archivedOccupancy =
            archiveOccupancy.Capture(expectedDestination);
        Check(
            archivedStack != null
                && archivedStack.State == WorldItemStackState.FacilityBuffer
                && !archivedStack.HasReservations
                && committedAfterArchive == 0
                && archivedOccupancy.NonCarriedMassGrams == blueprintUnitMass
                && archivedOccupancy.CommittedCarriedMassGrams == 0L
                && archivedOccupancy.TotalMassGrams == blueprintUnitMass,
            "BLUEPRINT_AI_HAUL_OWNERSHIP_CLEAN",
            archivedStack != null
                ? $"state={archivedStack.State}; reserved={archivedStack.ReservedQuantity}; committed={committedAfterArchive}; mass={archivedOccupancy.TotalMassGrams}g"
                : $"stack missing; committed={committedAfterArchive}");
        // Keep the bounded first-run flow ahead of unrelated operating-day
        // defense commands; approved-WU accounting remains game-clock based.
        Time.timeScale = 8f;

        bool mapped = projectCatalog.TryGetForBlueprint(blueprint.id, out ResearchProjectSO project);
        Check(
            mapped && project != null,
            "BLUEPRINT_PROJECT_MAPPING",
            mapped && project != null ? project.ProjectId.Value : $"blueprint={blueprint.id}");
        if (!mapped || project == null)
        {
            RestoreResearchWorkerAi(
                hauler,
                brain,
                brainWasEnabled,
                haulerWasAiPaused);
            yield break;
        }

        if (hauler == null)
        {
            float workerDeadline = Time.realtimeSinceStartup + 12f;
            while (hauler == null && Time.realtimeSinceStartup < workerDeadline)
            {
                hauler = FindHauler();
                if (hauler == null)
                {
                    yield return null;
                }
            }
            Check(
                hauler != null,
                "RESEARCH_WORKER_READY",
                hauler != null ? hauler.name : DescribeCharacterPublicationState());
            if (hauler == null)
            {
                yield break;
            }

            brain = hauler.Brain;
            brainWasEnabled = brain != null && brain.enabled;
            haulerWasAiPaused = hauler.IsAiPaused();
            hauler.SetAiPaused(true);
            brain?.StopCurrentActionForReplan(
                "first-run research verification isolation");
            hauler.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "first-run research verification isolation");
            yield return null;
            yield return null;
        }

        ResearchNodeState nodeState = research.GetNodeState(project, out string nodeBlocker);
        Check(
            nodeState != ResearchNodeState.BlueprintInTransit
                && !nodeBlocker.Contains("물리 설계도", StringComparison.Ordinal),
            "BLUEPRINT_NODE_CONDITION_ACTIVATED",
            $"state={nodeState}; blocker={nodeBlocker}");

        HashSet<string> completedProjectsBefore = research.State.Projects
            .CompletedProjectIds
            .ToHashSet(StringComparer.Ordinal);
        ResearchQueueCommandResult queued = queueCommands.Enqueue(project.ProjectId);
        Check(
            queued.Succeeded,
            "PROJECT_AUTO_QUEUE",
            $"{queued.Message}; added={queued.AffectedProjects.Count}");
        if (!queued.Succeeded)
        {
            RestoreResearchWorkerAi(
                hauler,
                brain,
                brainWasEnabled,
                haulerWasAiPaused);
            yield break;
        }

        BuildableObject researchFacility = FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(candidate => candidate != null
                && candidate.SupportsWork(BuiltInWorkTypeIds.Research))
            .OrderBy(candidate => Vector2Int.Distance(hauler.GetNowXY(), candidate.centerPos))
            .FirstOrDefault();
        AbilityWork work = hauler.GetComponent<AbilityWork>();
        ICharacterDeprivationRuntime deprivationRuntime =
            scope.Container.Resolve<ICharacterDeprivationRuntime>();
        Check(
            researchFacility != null && work != null,
            "RESEARCH_WORKER_AND_FACILITY_READY",
            $"worker={hauler.name}; facility={researchFacility?.BuildingData?.objectName ?? "missing"}");
        if (researchFacility == null || work == null)
        {
            RestoreResearchWorkerAi(
                hauler,
                brain,
                brainWasEnabled,
                haulerWasAiPaused);
            yield break;
        }

        string researchFacilityId =
            researchFacility.RequirePersistentInstanceId().Value;
        bool durableWorkPolicyReady = durableResearchPolicies.TryResolve(
            researchFacility,
            out ResearchDurableEquipmentWorkPolicy durableWorkPolicy,
            out string durablePolicyFailure);
        DurableFacilityEquipmentPolicy durablePolicy = null;
        bool durablePolicyReady = durableWorkPolicyReady
            && durableEquipmentPolicies.TryGetPolicy(
                durableWorkPolicy.EquipmentPolicyId,
                out durablePolicy);
        DurableFacilityEquipmentAssignment arcaneAssignment = durablePolicyReady
            ? durablePolicy.CreateAssignment(
                researchFacilityId,
                researchFacility.RequirePersistentInstanceId(),
                researchFacility.centerPos)
            : null;
        DurableFacilityEquipmentSlotResult arcaneReconcile =
            arcaneAssignment != null
                ? durableEquipmentSlots.TryReconcile(arcaneAssignment)
                : default;
        DurableFacilityEquipmentSlotSnapshot arcaneSequenceOne = null;
        bool arcaneSlotReady = arcaneAssignment != null
            && arcaneReconcile.Succeeded
            && durableEquipmentSlotQuery.TryCapture(
                arcaneAssignment.Key,
                out arcaneSequenceOne)
            && arcaneSequenceOne.AssignmentSequence == 1L
            && arcaneSequenceOne.Capacity.Value == 1300L
            && !string.Equals(
                arcaneSequenceOne.DestinationId,
                researchFacilityId,
                StringComparison.Ordinal)
            && destinationClaims.TryGetClaim(
                arcaneSequenceOne.DestinationId,
                researchFacility.centerPos,
                out FacilityBufferDestinationClaim arcaneClaim)
            && string.Equals(
                arcaneClaim.OwnerDomain,
                DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                arcaneClaim.OwnerFacilityId,
                researchFacilityId,
                StringComparison.Ordinal);
        Check(
            durableWorkPolicyReady && durablePolicyReady && arcaneSlotReady,
            "RESEARCH_ARCANE_INDEX_SEQUENCE_AUTHORITY",
            arcaneSlotReady
                ? $"destination={arcaneSequenceOne.DestinationId}; sequence={arcaneSequenceOne.AssignmentSequence}; capacity={arcaneSequenceOne.Capacity.Value}g"
                : $"workPolicy={durableWorkPolicyReady}; equipmentPolicy={durablePolicyReady}; failure={durablePolicyFailure}");
        Check(
            brain != null,
            "RESEARCH_ARCANE_INDEX_HAULER_BRAIN",
            brain != null ? hauler.name : "research hauler brain missing");
        if (!arcaneSlotReady || brain == null)
        {
            RestoreResearchWorkerAi(
                hauler,
                brain,
                brainWasEnabled,
                haulerWasAiPaused);
            yield break;
        }

        bool arcaneSpawned = itemRuntime.SpawnUniqueItemAt(
            DurableToolItemRules.ArcaneIndex,
            hauler.GetNowXY(),
            WorldItemStackState.Loose,
            string.Empty,
            out string arcaneStackId);
        bool lowDurabilityApplied = arcaneSpawned
            && itemRuntime.TrySetInstanceComponent(
                arcaneStackId,
                DurableToolItemRules.CreateDurability(
                    DurableToolItemRules.ArcaneIndex,
                    0.0001f));
        DurableFacilityEquipmentSlotResult arcaneSupply = lowDurabilityApplied
            ? durableEquipmentSlots.TryEnsureSupply(arcaneAssignment.Key)
            : default;
        bool exactArcaneSupplyRequested = lowDurabilityApplied
            && arcaneSupply.Succeeded
            && itemRuntime.GetAllStacks().Any(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    arcaneStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    arcaneSequenceOne.DestinationId,
                    StringComparison.Ordinal));
        Check(
            exactArcaneSupplyRequested,
            "RESEARCH_ARCANE_INDEX_EXACT_SUPPLY_REQUEST",
            $"spawned={arcaneSpawned}; durability={lowDurabilityApplied}; status={arcaneSupply.Status}; failure={arcaneSupply.FailureReason}");
        if (!exactArcaneSupplyRequested)
        {
            RestoreResearchWorkerAi(
                hauler,
                brain,
                brainWasEnabled,
                haulerWasAiPaused);
            yield break;
        }

        AbilityHaul researchHaul = hauler.GetComponent<AbilityHaul>();
        long initialHaulStartsBefore = researchHaul?.RuntimeHaulStartCount ?? 0L;
        bool initialHaulActionObserved = false;
        bool initialArcaneDelivered = false;
        brain.enabled = true;
        hauler.SetAiPaused(false);
        bool initialHaulPreferred = brain.PreferActionOnNextDecision<AIHaul>(90f);
        brain.RequestImmediateReplan(clearFailures: true);
        float initialArcaneDeadline = Time.realtimeSinceStartup + 24f;
        while (!initialArcaneDelivered
               && Time.realtimeSinceStartup < initialArcaneDeadline)
        {
            WorldItemStackSnapshot current = itemRuntime.GetAllStacks()
                .FirstOrDefault(stack => stack != null
                    && string.Equals(
                        stack.StackId,
                        arcaneStackId,
                        StringComparison.Ordinal));
            initialHaulActionObserved |=
                brain.bestAction?.actionset is AIHaul
                && researchHaul?.IsHauling == true;
            initialArcaneDelivered = current != null
                && current.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    current.DestinationId,
                    arcaneSequenceOne.DestinationId,
                    StringComparison.Ordinal)
                && !current.HasReservations
                && itemRuntime.GetCommittedHaulDeliveryQuantity(
                    arcaneSequenceOne.DestinationId,
                    DurableToolItemRules.ArcaneIndex) == 0;
            if (!initialArcaneDelivered)
                yield return null;
        }
        hauler.SetAiPaused(true);
        brain.StopCurrentActionForReplan(
            "first-run arcane-index delivery verification");
        yield return null;
        Check(
            initialHaulPreferred
                && initialHaulActionObserved
                && researchHaul != null
                && researchHaul.RuntimeHaulStartCount > initialHaulStartsBefore
                && initialArcaneDelivered,
            "RESEARCH_ARCANE_INDEX_AI_HAUL_TO_SEQUENCE",
            $"preferred={initialHaulPreferred}; action={initialHaulActionObserved}; starts={initialHaulStartsBefore}->{researchHaul?.RuntimeHaulStartCount}; delivered={initialArcaneDelivered}; destination={arcaneSequenceOne.DestinationId}");
        if (!initialArcaneDelivered)
        {
            RestoreResearchWorkerAi(
                hauler,
                brain,
                brainWasEnabled,
                haulerWasAiPaused);
            yield break;
        }

        StabilizeResearchWorker(hauler, deprivationRuntime);
        Check(
            !deprivationRuntime.HasActiveBreakdown(hauler),
            "RESEARCH_WORKER_STABILIZED",
            "연구 검증 중 생존 결핍과 붕괴를 격리함");
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Research,
            WorkPriorityLevel.Priority1);
        Grid researchGrid = FindFirstObjectByType<GridSystemManager>()?.grid;
        string priorityMessage = researchGrid != null
            ? string.Empty
            : "research grid missing";
        bool priorityAccepted = researchGrid != null;
        if (priorityAccepted)
        {
            priorityAccepted = work.TrySetPriorityWorkTarget(
                researchFacility,
                BuiltInWorkTypeIds.Research,
                researchGrid.SearchPath(hauler.GetNowXY()),
                out priorityMessage);
        }
        Check(
            priorityAccepted,
            "RESEARCH_PRIORITY_COMMAND",
            priorityAccepted ? researchFacility.BuildingData?.objectName : priorityMessage);
        if (!priorityAccepted)
        {
            RestoreResearchWorkerAi(
                hauler,
                brain,
                brainWasEnabled,
                haulerWasAiPaused);
            yield break;
        }

        ResearchProjectId firstActiveProjectId =
            research.State.Projects.ActiveProjectId;
        ResearchProjectProgressState firstActiveProgress =
            research.State.Projects.GetProgress(firstActiveProjectId);
        float firstProjectProgressBefore = firstActiveProgress.Progress;
        long approvedRevisionBefore =
            work.ApprovedWorkProgressRevisionForDiagnostics;
        bool firstApprovedWuObserved = false;
        float firstApprovedGenericCompleted = 0f;
        float firstApprovedGenericRequired = 0f;
        float projectProgressAtFirstApprovedWu = float.NaN;
        ResearchProgressEvent? firstResearchProgressEvent = null;
        float aggregateProgressAfterFirstCommit = float.NaN;
        int matchingResearchProgressEvents = 0;
        int workStarts = 0;
        int workStartsAtFirstCommit = -1;
        bool wasWorking = false;
        CharacterId researcherId = hauler.BuildingCharacterId;
        float firstContributionAtCommit = float.NaN;
        DungeonStory.Foundation.IGameEventBus gameEvents =
            scope.Container.Resolve<DungeonStory.Foundation.IGameEventBus>();
        IProjectWorkforceRuntime projectWorkforce =
            scope.Container.Resolve<IProjectWorkforceRuntime>();
        IDisposable researchProgressSubscription =
            gameEvents.Subscribe<ResearchProgressEvent>(progressEvent =>
            {
                if (!string.Equals(
                        progressEvent.Researcher.Value,
                        researcherId.Value,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        progressEvent.ProjectId,
                        firstActiveProjectId.Value,
                        StringComparison.Ordinal))
                {
                    return;
                }

                matchingResearchProgressEvents++;
                if (firstResearchProgressEvent.HasValue)
                {
                    return;
                }

                firstResearchProgressEvent = progressEvent;
                aggregateProgressAfterFirstCommit = firstActiveProgress.Progress;
                workStartsAtFirstCommit = workStarts;
                firstContributionAtCommit =
                    projectWorkforce.GetContributionMultiplier(
                        firstActiveProjectId.Value,
                        researcherId.Value);
            });

        WorldItemStackSnapshot arcaneAtResearchStart = itemRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    arcaneStackId,
                    StringComparison.Ordinal));
        bool arcaneSequenceReadyBefore = arcaneAtResearchStart != null
            && arcaneAtResearchStart.Quantity == 1
            && arcaneAtResearchStart.State == WorldItemStackState.FacilityBuffer
            && string.Equals(
                arcaneAtResearchStart.DestinationId,
                arcaneSequenceOne.DestinationId,
                StringComparison.Ordinal)
            && DurableToolItemRules.ReadCurrentDurability(
                arcaneAtResearchStart.ItemId,
                arcaneAtResearchStart.Components) > 0f;
        bool rawArcaneDestinationPresentBefore = itemRuntime.GetAllStacks().Any(
            stack => stack != null
                && stack.Quantity > 0
                && string.Equals(
                    stack.ItemId,
                    DurableToolItemRules.ArcaneIndex,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    researchFacilityId,
                    StringComparison.Ordinal));
        bool knowledgeResiduePresentBefore = itemRuntime.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && stack.State == WorldItemStackState.FacilityBuffer
            && stack.StockCategory == StockCategory.Knowledge
            && string.Equals(
                stack.DestinationId,
                $"research:{researchFacilityId}",
                StringComparison.Ordinal));
        IDungeonDebugRuleQuery debugRules =
            scope.Container.Resolve<IDungeonDebugRuleQuery>();
        IMetaProgressionRuntimeReader metaProgression =
            scope.Container.Resolve<IMetaProgressionRuntimeReader>();
        float researchCycleWork = Mathf.Max(
            0.1f,
            researchFacility.BuildingData.GetRequiredWork(
                BuiltInWorkTypeIds.Research));
        float metaResearchMultiplier = Mathf.Max(
            0.05f,
            metaProgression.GetArcaneResearchWorkMultiplier());

        if (brain != null)
        {
            brain.enabled = true;
            bool preferred = brain.PreferWorkActionOnNextDecision(
                BuiltInWorkTypeIds.Research,
                persistenceSeconds: 90f);
            Check(
                preferred,
                "RESEARCH_AI_ACTION_AVAILABLE",
                preferred
                    ? "연구 작업 액션을 다음 판단에 예약함"
                    : $"canRun={hauler.CanRunAi}; lifecycle={hauler.CurrentLifecycleState}");
            brain.RequestImmediateReplan(clearFailures: true);
        }
        hauler.SetAiPaused(false);

        float researchStartedAt = Time.realtimeSinceStartup;
        float nextPriorityRefreshAt = researchStartedAt + 1f;
        float nextNeedStabilizationAt = researchStartedAt + 0.5f;
        while (!research.State.Projects.CompletedProjectIds.Any(
                   id => !completedProjectsBefore.Contains(id))
               && Time.realtimeSinceStartup - researchStartedAt < 45f)
        {
            if (work.isWorking && !wasWorking)
            {
                workStarts++;
            }
            wasWorking = work.isWorking;

            if (!firstApprovedWuObserved
                && work.ApprovedWorkProgressRevisionForDiagnostics
                    > approvedRevisionBefore)
            {
                firstApprovedWuObserved = true;
                firstApprovedGenericCompleted =
                    work.GenericCompletedWorkForDiagnostics;
                firstApprovedGenericRequired =
                    work.GenericRequiredWorkForDiagnostics;
                projectProgressAtFirstApprovedWu =
                    firstActiveProgress.Progress;
            }

            if (Time.realtimeSinceStartup >= nextNeedStabilizationAt)
            {
                nextNeedStabilizationAt = Time.realtimeSinceStartup + 0.5f;
                StabilizeResearchWorker(hauler, deprivationRuntime);
            }

            if (!work.isWorking
                && Time.realtimeSinceStartup >= nextPriorityRefreshAt)
            {
                nextPriorityRefreshAt = Time.realtimeSinceStartup + 1f;
                work.TrySetPriorityWorkTarget(
                    researchFacility,
                    BuiltInWorkTypeIds.Research,
                    researchGrid.SearchPath(hauler.GetNowXY()),
                    out _);
                brain?.RequestImmediateReplan(clearFailures: false);
            }

            yield return null;
        }

        researchProgressSubscription.Dispose();
        DurableFacilityEquipmentSlotSnapshot arcaneSequenceOneAfter =
            durableEquipmentSlotQuery.CaptureAll()
                .FirstOrDefault(value => value != null
                    && value.AssignmentSequence
                        == arcaneSequenceOne.AssignmentSequence
                    && value.Key.Equals(arcaneAssignment.Key));
        WorldItemStackSnapshot depletedArcane = itemRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    arcaneStackId,
                    StringComparison.Ordinal));
        bool arcaneDrainExact = firstResearchProgressEvent.HasValue
            && arcaneSequenceOneAfter != null
            && arcaneSequenceOneAfter.LifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase
                    .ClosedAwaitingCheckpointGc
            && arcaneSequenceOneAfter.AuthoritiesRevoked
            && arcaneSequenceOneAfter.Drain?.OwnerAcknowledged == true
            && arcaneSequenceOneAfter.Drain.InputQuantity == 1
            && arcaneSequenceOneAfter.Drain.InputMassGrams == 1300L
            && arcaneSequenceOneAfter.Drain.ReleasedQuantity == 1
            && arcaneSequenceOneAfter.Drain.ReleasedMassGrams == 1300L
            && depletedArcane != null
            && depletedArcane.Quantity == 1
            && DurableToolItemRules.ReadCurrentDurability(
                depletedArcane.ItemId,
                depletedArcane.Components) <= 0f
            && !string.Equals(
                depletedArcane.DestinationId,
                arcaneSequenceOne.DestinationId,
                StringComparison.Ordinal)
            && !itemRuntime.GetAllStacks().Any(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    researchFacilityId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    DurableToolItemRules.ArcaneIndex,
                    StringComparison.Ordinal))
            && itemRuntime.CaptureHaulDeliveryIntentsByDestination(
                    researchFacilityId)
                .Count == 0;
        Check(
            arcaneDrainExact,
            "RESEARCH_ARCANE_INDEX_DEPLETION_DRAIN_EXACT",
            arcaneSequenceOneAfter != null
                ? $"phase={arcaneSequenceOneAfter.LifecyclePhase}; revoked={arcaneSequenceOneAfter.AuthoritiesRevoked}; input={arcaneSequenceOneAfter.Drain?.InputQuantity}/{arcaneSequenceOneAfter.Drain?.InputMassGrams}g; released={arcaneSequenceOneAfter.Drain?.ReleasedQuantity}/{arcaneSequenceOneAfter.Drain?.ReleasedMassGrams}g; stack={depletedArcane?.State}/{depletedArcane?.DestinationId}"
                : "sequence-1 slot missing after first research commit");

        bool replacementDelivered = false;
        bool replacementHaulObserved = false;
        string replacementStackId = string.Empty;
        DurableFacilityEquipmentSlotSnapshot arcaneSequenceTwo = null;
        if (arcaneDrainExact)
        {
            hauler.SetAiPaused(true);
            brain.StopCurrentActionForReplan(
                "first-run arcane-index replacement verification");
            yield return null;

            bool replacementSpawned = itemRuntime.SpawnUniqueItemAt(
                DurableToolItemRules.ArcaneIndex,
                hauler.GetNowXY(),
                WorldItemStackState.Loose,
                string.Empty,
                out replacementStackId);
            DurableFacilityEquipmentSlotResult replacementReconcile =
                replacementSpawned
                    ? durableEquipmentSlots.TryReconcile(arcaneAssignment)
                    : default;
            bool replacementSequenceOpened = replacementReconcile.Succeeded
                && durableEquipmentSlotQuery.TryCapture(
                    arcaneAssignment.Key,
                    out arcaneSequenceTwo)
                && arcaneSequenceTwo.AssignmentSequence == 2L
                && !string.Equals(
                    arcaneSequenceTwo.DestinationId,
                    arcaneSequenceOne.DestinationId,
                    StringComparison.Ordinal);
            DurableFacilityEquipmentSlotResult replacementSupply =
                replacementSequenceOpened
                    ? durableEquipmentSlots.TryEnsureSupply(arcaneAssignment.Key)
                    : default;
            bool replacementRequested = replacementSequenceOpened
                && replacementSupply.Succeeded
                && itemRuntime.GetAllStacks().Any(stack => stack != null
                    && string.Equals(
                        stack.StackId,
                        replacementStackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.DestinationId,
                        arcaneSequenceTwo.DestinationId,
                        StringComparison.Ordinal));
            long replacementHaulStartsBefore =
                researchHaul.RuntimeHaulStartCount;
            if (replacementRequested)
            {
                brain.enabled = true;
                hauler.SetAiPaused(false);
                brain.PreferActionOnNextDecision<AIHaul>(90f);
                brain.RequestImmediateReplan(clearFailures: true);
                float replacementDeadline = Time.realtimeSinceStartup + 24f;
                while (!replacementDelivered
                       && Time.realtimeSinceStartup < replacementDeadline)
                {
                    WorldItemStackSnapshot replacement = itemRuntime.GetAllStacks()
                        .FirstOrDefault(stack => stack != null
                            && string.Equals(
                                stack.StackId,
                                replacementStackId,
                                StringComparison.Ordinal));
                    replacementHaulObserved |=
                        brain.bestAction?.actionset is AIHaul
                        && researchHaul.IsHauling;
                    replacementDelivered = replacement != null
                        && replacement.State ==
                            WorldItemStackState.FacilityBuffer
                        && string.Equals(
                            replacement.DestinationId,
                            arcaneSequenceTwo.DestinationId,
                            StringComparison.Ordinal)
                        && !replacement.HasReservations
                        && itemRuntime.GetCommittedHaulDeliveryQuantity(
                            arcaneSequenceTwo.DestinationId,
                            DurableToolItemRules.ArcaneIndex) == 0;
                    if (!replacementDelivered)
                        yield return null;
                }
            }
            hauler.SetAiPaused(true);
            brain.StopCurrentActionForReplan(
                "first-run arcane-index replacement delivered");
            yield return null;
            bool replacementSlotReady = arcaneSequenceTwo != null
                && durableEquipmentSlotQuery.TryCapture(
                    arcaneAssignment.Key,
                    out DurableFacilityEquipmentSlotSnapshot liveSequenceTwo)
                && liveSequenceTwo.AssignmentSequence == 2L
                && liveSequenceTwo.SupplyReady;
            Check(
                replacementSpawned
                    && replacementSequenceOpened
                    && replacementRequested
                    && replacementHaulObserved
                    && researchHaul.RuntimeHaulStartCount
                        > replacementHaulStartsBefore
                    && replacementDelivered
                    && replacementSlotReady,
                "RESEARCH_ARCANE_INDEX_REPLACEMENT_AI_HAUL",
                $"spawned={replacementSpawned}; sequence={arcaneSequenceTwo?.AssignmentSequence}; requested={replacementRequested}; action={replacementHaulObserved}; starts={replacementHaulStartsBefore}->{researchHaul.RuntimeHaulStartCount}; delivered={replacementDelivered}; ready={replacementSlotReady}");
        }

        float expectedFirstCommittedWork =
            BlueprintResearchService.CalculateApprovedResearchWork(
                hauler,
                researchCycleWork
                * Mathf.Max(0f, firstContributionAtCommit)
                * metaResearchMultiplier)
            * (arcaneSequenceReadyBefore ? 1.1f : 1f);
        bool approvedWuGate = firstApprovedWuObserved
            && work.ApprovedWorkProgressRevisionForDiagnostics
                > approvedRevisionBefore
            && Mathf.Approximately(
                firstApprovedGenericRequired,
                researchCycleWork)
            && firstResearchProgressEvent.HasValue;
        Check(
            approvedWuGate,
            "RESEARCH_FIRST_APPROVED_WU",
            $"revision={approvedRevisionBefore}->{work.ApprovedWorkProgressRevisionForDiagnostics}; "
            + $"generic={firstApprovedGenericCompleted:0.###}/{firstApprovedGenericRequired:0.###}; "
            + $"cycle={researchCycleWork:0.###}; project={firstProjectProgressBefore:0.###}->{projectProgressAtFirstApprovedWu:0.###}");

        bool firstCommitObserved = firstResearchProgressEvent.HasValue;
        ResearchProgressEvent firstCommit =
            firstResearchProgressEvent.GetValueOrDefault();
        Check(
            firstCommitObserved
                && firstCommit.ApprovedWork > 0f
                && firstCommit.ProgressDelta > 0f
                && workStartsAtFirstCommit == 1,
            "RESEARCH_FIRST_APPROVED_COMMIT",
            firstCommitObserved
                ? $"project={firstCommit.ProjectId}; approved={firstCommit.ApprovedWork:0.###}; "
                  + $"delta={firstCommit.ProgressDelta:0.###}; starts={workStartsAtFirstCommit}; events={matchingResearchProgressEvents}"
                : $"project={firstActiveProjectId.Value}; events=0; starts={workStarts}");
        Check(
            firstCommitObserved
                && !debugRules.IsEnabled(DungeonDebugCheat.InstantWork)
                && arcaneSequenceReadyBefore
                && !rawArcaneDestinationPresentBefore
                && !knowledgeResiduePresentBefore
                && Mathf.Abs(
                    firstCommit.ApprovedWork - expectedFirstCommittedWork)
                    <= 0.011f,
            "RESEARCH_FIRST_COMMIT_MODIFIERS",
            firstCommitObserved
                ? $"actual={firstCommit.ApprovedWork:0.###}; expected={expectedFirstCommittedWork:0.###}; "
                  + $"cycle={researchCycleWork:0.###}; contribution={firstContributionAtCommit:0.###}; meta={metaResearchMultiplier:0.###}; "
                  + $"sequenceIndex={arcaneSequenceReadyBefore}; rawIndex={rawArcaneDestinationPresentBefore}; residue={knowledgeResiduePresentBefore}; instant={debugRules.IsEnabled(DungeonDebugCheat.InstantWork)}"
                : "first research progress event missing");
        Check(
            firstCommitObserved
                && Mathf.Abs(
                    firstCommit.ApprovedWork - firstCommit.ProgressDelta)
                    <= 0.011f,
            "RESEARCH_FIRST_COMMIT_CONSERVATION",
            firstCommitObserved
                ? $"approved={firstCommit.ApprovedWork:0.###}; delta={firstCommit.ProgressDelta:0.###}; "
                  + $"aggregateSample={firstProjectProgressBefore:0.###}->{aggregateProgressAfterFirstCommit:0.###}"
                : "first research progress event missing");

        string completedProjectId = research.State.Projects
            .CompletedProjectIds
            .FirstOrDefault(id => !completedProjectsBefore.Contains(id));
        bool completed = !string.IsNullOrWhiteSpace(completedProjectId);
        CharacterAiJobCandidate workJobCandidate = default;
        bool workJobAvailable = brain != null
            && brain.RequireJobGiverCatalog().Work.TryEvaluate(
                hauler,
                out workJobCandidate);
        bool researchTargetAvailable = work.TryGetBestWorkCandidate(
            BuiltInWorkTypeIds.Research,
            researchGrid.SearchPath(hauler.GetNowXY()),
            out WorkTargetCandidate researchTargetCandidate);
        Check(
            completed,
            "PROJECT_COMPLETED_BY_WORK_ROUTINE",
            completed
                ? $"project={completedProjectId}; purchasedTarget={project.ProjectId.Value}; starts={workStarts}; elapsed={Time.realtimeSinceStartup - researchStartedAt:0.0}s"
                : $"purchasedTarget={project.ProjectId.Value}; starts={workStarts}; queue={research.State.Projects.Queue.Count}; active={research.State.Projects.ActiveProjectId.Value}; "
                  + $"canRun={hauler.CanRunAi}; lifecycle={hauler.CurrentLifecycleState}; brainEnabled={brain?.enabled}; "
                  + $"action={brain?.CurrentActionDebugLabel}; phase={brain?.CurrentActionPhase}; detail={brain?.CurrentActionPhaseDetail}; "
                  + $"failure={brain?.LastActionFailure}; priority={work.PriorityWorkTypeId}/{work.PriorityWorkTarget?.name}; "
                  + $"assigned={work.AssignedWorkTypeId}/{work.assignedShop?.name}; "
                  + $"workJob={workJobAvailable}:{workJobCandidate.DebugSummary}; "
                  + $"researchTarget={researchTargetAvailable}:{WorkTargetCandidateRuntimeAdapter.ResolveBuilding(researchTargetCandidate)?.name}:{researchTargetCandidate.FailureReason}");
        objectiveRuntime.RefreshNow();
        bool reachedPostResearchObjective =
            objectiveRuntime.CurrentObjective == FirstRunObjectiveId.CompleteSettlement
            || objectiveRuntime.CurrentObjective == FirstRunObjectiveId.DefendInvasion;
        Check(
            completed && reachedPostResearchObjective,
            "POST_RESEARCH_OBJECTIVE",
            objectiveRuntime.CurrentObjective.ToString());
        RestoreResearchWorkerAi(
            hauler,
            brain,
            brainWasEnabled,
            haulerWasAiPaused);

        yield return CapturePhysicalFlowScreen();
    }

    private static void RestoreResearchWorkerAi(
        CharacterActor actor,
        AIBrain brain,
        bool brainWasEnabled,
        bool aiWasPaused)
    {
        if (brain != null)
        {
            brain.enabled = brainWasEnabled;
        }
        actor?.SetAiPaused(aiWasPaused);
    }

    private static void StabilizeResearchWorker(
        CharacterActor actor,
        ICharacterDeprivationRuntime deprivationRuntime)
    {
        if (actor == null)
        {
            return;
        }

        IDictionary<CharacterCondition, float> conditions = actor.stats;
        if (conditions != null)
        {
            conditions[CharacterCondition.HUNGER] = 100f;
            conditions[CharacterCondition.THIRST] = 100f;
            conditions[CharacterCondition.SLEEP] = 100f;
            conditions[CharacterCondition.FUN] = 100f;
            conditions[CharacterCondition.MOOD] = 100f;
            conditions[CharacterCondition.EXCRETION] = 100f;
            conditions[CharacterCondition.HYGIENE] = 100f;
        }

        deprivationRuntime?.DebugClearBreakdown(actor);
    }

    private static void ResetFirstRunMetaForVerification(
        DungeonRuntimeLifetimeScope scope)
    {
        MetaProgressionRuntime meta = scope.Container
            .Resolve<ProgressionSceneRuntimeReferences>()
            .MetaProgression;
        if (meta != null)
        {
            MetaProgressionState state = meta.State;
            state.Restore(
                state.LifetimeEarnedCurrency,
                state.SpentCurrency,
                state.UpgradeLevels.ToArray(),
                state.PreservedRecipeIds.ToArray(),
                completedRunCount: 0);
            meta.StartNewRun();
        }
    }

    private IEnumerator EnsureProductBoot()
    {
        float titleDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        DungeonTitleLifetimeScope titleScope = null;
        IDungeonSceneNavigator navigator = null;
        while (Time.realtimeSinceStartup < titleDeadline)
        {
            titleScope = FindFirstObjectByType<DungeonTitleLifetimeScope>(
                FindObjectsInactive.Include);
            if (titleScope?.Container != null)
            {
                try
                {
                    navigator = titleScope.Container.Resolve<
                        IDungeonSceneNavigator>();
                }
                catch (Exception exception)
                {
                    errors.Add("[BOOT-DI-ERROR] " + exception);
                }
            }
            if (navigator != null
                && string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.TitleSceneName,
                    StringComparison.Ordinal))
            {
                break;
            }
            yield return null;
        }

        bool titleReady = navigator != null
            && string.Equals(
                SceneManager.GetActiveScene().name,
                DungeonSceneNavigator.TitleSceneName,
                StringComparison.Ordinal);
        Check(
            titleReady,
            "BOOT_TITLE_READY",
            titleReady
                ? "Title scope and production scene navigator are ready."
                : "Title scope or production scene navigator was not ready.");
        if (!titleReady
            || !navigator.StartNewGame(
                DungeonDifficulty.Normal,
                DungeonSurvivalPressure.Standard))
        {
            Check(
                false,
                "BOOT_PREPARATION_REQUESTED",
                "Production StartNewGame request was rejected.");
            yield break;
        }

        float preparationDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        Button owner = null;
        Button next = null;
        while (Time.realtimeSinceStartup < preparationDeadline)
        {
            owner = Resources.FindObjectsOfTypeAll<Button>()
                .Where(candidate => candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.activeInHierarchy
                    && candidate.interactable
                    && candidate.name.StartsWith(
                        "OwnerCandidate_",
                        StringComparison.Ordinal))
                .OrderBy(candidate => candidate.name, StringComparer.Ordinal)
                .FirstOrDefault();
            next = StartPartyPlayModeTestDriver.FindButton(
                "PreparationOwnerNextButton",
                requireInteractable: false);
            if (owner != null
                && next != null
                && string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.PreparationSceneName,
                    StringComparison.Ordinal))
            {
                break;
            }
            yield return null;
        }

        bool preparationReady = owner != null
            && next != null
            && string.Equals(
                SceneManager.GetActiveScene().name,
                DungeonSceneNavigator.PreparationSceneName,
                StringComparison.Ordinal);
        Check(
            preparationReady,
            "BOOT_PREPARATION_READY",
            preparationReady
                ? "Preparation owner selection is ready."
                : "Preparation owner selection did not become ready.");
        if (!preparationReady)
        {
            yield break;
        }

        yield return Click(owner, "preparation owner");
        yield return null;
        next = StartPartyPlayModeTestDriver.FindButton(
            "PreparationOwnerNextButton",
            requireInteractable: true);
        if (next == null)
        {
            Check(
                false,
                "BOOT_PREPARATION_OWNER_SELECTED",
                "Owner selection did not enable the next command.");
            yield break;
        }

        yield return Click(next, "preparation owner next");

        float startDeadline = Time.realtimeSinceStartup
            + PartyReadyTimeoutSeconds;
        Button start = null;
        while (Time.realtimeSinceStartup < startDeadline)
        {
            start = StartPartyPlayModeTestDriver.FindButton(
                "PreparationStartRunButton",
                requireInteractable: true);
            if (start != null)
            {
                break;
            }
            yield return null;
        }

        Check(
            start != null,
            "BOOT_PREPARED_START_READY",
            start != null
                ? "Prepared start command is interactable."
                : "Prepared start command did not become interactable.");
        if (start == null)
        {
            yield break;
        }

        yield return StartPartyPlayModeTestDriver.CompleteIfVisible(
            RuntimeReadyTimeoutSeconds);

        float gameplayDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < gameplayDeadline)
        {
            DungeonRuntimeLifetimeScope scope = FindScope();
            OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
            CharacterActor worldActor = FindWorldReadyActor();
            if (string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.GameplaySceneName,
                    StringComparison.Ordinal)
                && scope?.Container != null
                && ownerManager?.CurrentOwnerActor != null
                && worldActor != null)
            {
                Check(
                    true,
                    "BOOT_PREPARED_START_REQUESTED",
                    "PreparedNewRun reached Gameplay through the production preparation UI.");
                Check(
                    true,
                    "BOOT_GAMEPLAY_READY",
                    $"owner={ownerManager.CurrentOwnerActor.name}; "
                    + $"actor={worldActor.name}@{worldActor.GetNowXY()}");
                yield break;
            }
            yield return null;
        }

        Check(
            false,
            "BOOT_GAMEPLAY_READY",
            "PreparedNewRun did not reach a ready Gameplay world before timeout. "
            + DescribeCharacterPublicationState());
    }

    private static string DescribeCharacterPublicationState()
    {
        CharacterActor[] actors = CharacterActorCollection.DistinctByGameObject(
                FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null)
            .ToArray();
        if (actors.Length == 0)
        {
            return "character actors missing";
        }

        return string.Join(
            " | ",
            actors.Select(actor =>
                $"{actor.name}:active={actor.gameObject.activeInHierarchy},"
                + $"lifecycle={actor.CurrentLifecycleState},dead={actor.IsDead},"
                + $"move={actor.TryGetAbility(out AbilityMove _)},"
                + $"work={actor.TryGetAbility(out AbilityWork _)},"
                + $"role={actor.Identity?.Role}"));
    }

    private IEnumerator Click(Button button, string label)
    {
        bool available = button != null && button.gameObject.activeInHierarchy && button.interactable;
        Check(available, "POINTER_TARGET", available ? label : label + " missing");
        if (!available)
        {
            yield break;
        }

        yield return ScrollIntoView(button);
        RectTransform rect = button.GetComponent<RectTransform>();
        Vector2 point = GetScreenPoint(rect, rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = point
        };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current?.RaycastAll(pointer, hits);
        GameObject topTarget = hits.FirstOrDefault().gameObject;
        bool targetMatched = topTarget != null
            && (topTarget == button.gameObject
                || topTarget.transform.IsChildOf(button.transform));
        string raycastDetail = targetMatched
            ? $"{label}->{topTarget.name}"
            : $"{label}->top={topTarget?.name ?? "none"}; point={point}";
        if (targetMatched)
        {
            Check(true, "POINTER_RAYCAST", raycastDetail);
        }
        else
        {
            report.Add($"[INFO] POINTER_RAYCAST_MISS {raycastDetail}");
        }
        if (!targetMatched)
        {
            yield break;
        }
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(
            verificationMouse,
            new MouseState { position = point }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = point });
        yield return null;
        yield return null;
    }

    private IEnumerator WaitForSceneTransitionInputRelease()
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        GameObject blocker;
        do
        {
            blocker = GameObject.Find("SceneTransitionInputBlocker");
            if (blocker == null)
            {
                break;
            }

            yield return null;
        }
        while (Time.realtimeSinceStartup < deadline);

        Check(
            blocker == null,
            "SCENE_TRANSITION_INPUT_RELEASED",
            blocker == null
                ? "transition blocker destroyed before product UI interaction"
                : "transition blocker remained active after 5 realtime seconds");
    }

    private IEnumerator ScrollIntoView(Button button)
    {
        ScrollRect scroll = button != null ? button.GetComponentInParent<ScrollRect>() : null;
        RectTransform viewport = scroll != null ? scroll.viewport : null;
        if (scroll == null || viewport == null || !scroll.vertical)
        {
            yield break;
        }

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            Vector2 buttonPoint = GetScreenPoint(
                buttonRect,
                buttonRect.TransformPoint(buttonRect.rect.center));
            Camera viewportCamera = GetEventCamera(viewport);
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    viewport,
                    buttonPoint,
                    viewportCamera))
            {
                yield break;
            }

            Vector2 viewportPoint = GetScreenPoint(
                viewport,
                viewport.TransformPoint(viewport.rect.center));
            float scrollDelta = buttonPoint.y < viewportPoint.y ? -120f : 120f;
            verificationMouse.MakeCurrent();
            InputSystem.QueueStateEvent(
                verificationMouse,
                new MouseState { position = viewportPoint, scroll = new Vector2(0f, scrollDelta) });
            yield return null;
            InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = viewportPoint });
            yield return null;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform targetRect = button.GetComponent<RectTransform>();
        Vector2 targetPoint = GetScreenPoint(
            targetRect,
            targetRect.TransformPoint(targetRect.rect.center));
        Camera targetCamera = GetEventCamera(viewport);
        if (!RectTransformUtility.RectangleContainsScreenPoint(
                viewport,
                targetPoint,
                targetCamera))
        {
            float before = scroll.verticalNormalizedPosition;
            Vector2 viewportPoint = GetScreenPoint(
                viewport,
                viewport.TransformPoint(viewport.rect.center));
            scroll.StopMovement();

            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            float targetYAtZero = GetScreenPoint(
                targetRect,
                targetRect.TransformPoint(targetRect.rect.center)).y;

            scroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            float targetYAtOne = GetScreenPoint(
                targetRect,
                targetRect.TransformPoint(targetRect.rect.center)).y;

            float travel = targetYAtOne - targetYAtZero;
            float desiredNormalized = Mathf.Abs(travel) > 0.01f
                ? Mathf.Clamp01((viewportPoint.y - targetYAtZero) / travel)
                : before;
            scroll.verticalNormalizedPosition = desiredNormalized;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;

            targetPoint = GetScreenPoint(
                targetRect,
                targetRect.TransformPoint(targetRect.rect.center));
            bool visible = RectTransformUtility.RectangleContainsScreenPoint(
                viewport,
                targetPoint,
                targetCamera);
            report.Add(
                $"[INFO] SCROLL_POSITION_FALLBACK {button.name}; "
                + $"normalized={before:0.###}->{scroll.verticalNormalizedPosition:0.###}; "
                + $"targetY0={targetYAtZero:0.##}; targetY1={targetYAtOne:0.##}; "
                + $"visible={visible}; point={targetPoint}");
        }
    }

    private static Vector2 GetScreenPoint(RectTransform rect, Vector3 worldPoint)
    {
        return RectTransformUtility.WorldToScreenPoint(
            GetEventCamera(rect),
            worldPoint);
    }

    private static Camera GetEventCamera(RectTransform rect)
    {
        Canvas canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
        return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
    }

    private void CheckNonBlocking(IFirstRunObjectiveRuntime objective)
    {
        RectTransform panel = objective?.PanelRect;
        Check(
            panel == null || !panel.gameObject.activeInHierarchy,
            "OBJECTIVE_HIDDEN",
            $"panelExists={panel != null}; active={panel != null && panel.gameObject.activeInHierarchy}");
    }

    private void CheckPanelBounds(IFirstRunObjectiveRuntime objective)
    {
        RectTransform panel = objective?.PanelRect;
        Check(
            panel == null || !panel.gameObject.activeInHierarchy,
            "NO_PRODUCT_OBJECTIVE_PANEL",
            $"panelExists={panel != null}; active={panel != null && panel.gameObject.activeInHierarchy}");
    }

    private IEnumerator CaptureScreen()
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        bool nonblank = pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8));
        Check(nonblank, "SCREEN_CAPTURE", $"nonblank={nonblank}; pixels={pixels.Length}");
        if (capture != null)
        {
            File.WriteAllBytes(FirstRunObjectivePlayModeVerifier.ScreenshotPath, capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private IEnumerator CapturePhysicalFlowScreen()
    {
        Directory.CreateDirectory("Artifacts/QA");
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        bool nonblank = pixels.Any(pixel =>
            pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8));
        Check(
            nonblank,
            "PHYSICAL_RESEARCH_FLOW_CAPTURE",
            $"nonblank={nonblank}; pixels={pixels.Length}");
        if (capture != null)
        {
            File.WriteAllBytes(
                FirstRunObjectivePlayModeVerifier.PhysicalFlowScreenshotPath,
                capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private static CharacterActor FindHauler()
    {
        return CharacterActorCollection.DistinctByGameObject(
                FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null && !actor.IsDead)
            .Where(actor => actor.GetComponent<AbilityHaul>()?.IsHauling != true)
            .OrderByDescending(actor => actor.TryGetAbility(out AbilityWork _))
            .ThenBy(actor =>
                actor.GetComponent<CharacterCarryInventory>()?.HasItems == true ? 1 : 0)
            .ThenBy(actor =>
                actor.Identity != null && actor.Identity.Role == CharacterRole.Owner ? 1 : 0)
            .FirstOrDefault(actor =>
                actor.TryGetAbility(out AbilityMove _)
                && (actor.TryGetAbility(out AbilityWork _)
                    || actor.Identity != null
                    && actor.Identity.Role == CharacterRole.Owner));
    }

    private static CharacterActor FindWorldReadyActor()
    {
        return CharacterActorCollection.DistinctByGameObject(
                FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null
                && actor.gameObject.activeInHierarchy
                && !actor.IsDead
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
            .FirstOrDefault(actor =>
                actor.TryGetAbility(out AbilityMove _)
                && (actor.TryGetAbility(out AbilityWork _)
                    || actor.Identity != null
                    && actor.Identity.Role == CharacterRole.Owner));
    }

    private static string DescribeBlueprintStack(
        IWorldItemStackRuntime itemRuntime,
        FacilityBlueprintSO blueprint)
    {
        WorldItemStackSnapshot stack = itemRuntime?.GetAllStacks()
            .FirstOrDefault(candidate => candidate != null
                && blueprint != null
                && string.Equals(
                    candidate.ItemId,
                    blueprint.PhysicalItemId,
                    StringComparison.Ordinal));
        if (stack == null)
        {
            string carrierDetails = string.Join(
                " | ",
                CharacterActorCollection.DistinctByGameObject(
                        FindObjectsByType<CharacterActor>(
                            FindObjectsInactive.Exclude,
                            FindObjectsSortMode.None))
                    .Where(actor => actor != null && !actor.IsDead)
                    .Select(actor => new
                    {
                        Actor = actor,
                        Carry = actor.GetComponent<CharacterCarryInventory>(),
                        Haul = actor.GetComponent<AbilityHaul>()
                    })
                    .Where(entry =>
                        entry.Carry?.CountItem(blueprint?.PhysicalItemId) > 0)
                    .Select(entry =>
                    {
                        Grid grid = FindFirstObjectByType<GridSystemManager>()?.grid;
                        BuildableObject archive = FindObjectsByType<BuildableObject>(
                                FindObjectsInactive.Exclude,
                                FindObjectsSortMode.None)
                            .FirstOrDefault(candidate => candidate?.BuildingData?
                                .GetAbility<BuildingResearchArchiveAbility>() != null);
                        int pathCost = grid != null && archive != null
                            ? grid.SearchPathTo(
                                    entry.Actor.GetNowXY(),
                                    archive.centerPos)
                                .GetMoveCostTo(archive.centerPos)
                            : int.MaxValue;
                        string route = "none";
                        if (grid != null && archive != null)
                        {
                            Queue<GridMoveStep> path = grid.SearchPathTo(
                                    entry.Actor.GetNowXY(),
                                    archive.centerPos)
                                .GetMovePathTo(archive.centerPos);
                            route = string.Join(
                                ",",
                                path.Take(12).Select(step =>
                                    $"{step.From}->{step.To}:{step.MoveType}:"
                                    + $"{step.MovementOccupant?.GetType().Name ?? "-"}"));
                        }
                        return $"{entry.Actor.name}@{entry.Actor.GetNowXY()}; "
                            + $"count={entry.Carry.CountItem(blueprint.PhysicalItemId)}; "
                            + $"hauling={entry.Haul?.IsHauling}; "
                            + $"unload={entry.Haul?.CurrentUnloadReason}; "
                            + $"pathCost={pathCost}; route={route}";
                    }));
            return string.IsNullOrWhiteSpace(carrierDetails)
                ? "blueprint stack and carrier missing"
                : "carried: " + carrierDetails;
        }

        return $"state={stack.State}; pos={stack.Position}; destination={stack.DestinationId}; "
            + $"reserved={stack.ReservedQuantity}; available={stack.AvailableQuantity}";
    }

    private static string DescribeArchiveCandidates(IRoomLayoutCache roomLayoutCache)
    {
        BuildableObject[] candidates = FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(candidate => candidate?.BuildingData?
                .GetAbility<BuildingResearchArchiveAbility>() != null)
            .ToArray();
        if (candidates.Length == 0)
        {
            return "archive ability building missing";
        }

        return string.Join(
            " | ",
            candidates.Select(candidate =>
            {
                RoomInstance room = null;
                bool found = roomLayoutCache != null
                    && (roomLayoutCache.TryGetRoom(candidate, out room)
                        || roomLayoutCache.TryGetRoom(
                            candidate.Grid,
                            candidate.centerPos,
                            out room));
                return found
                    ? $"{candidate.BuildingData.objectName}@{candidate.centerPos}:"
                    + $"usable={room.IsUsable},self={room.IsSelfContained},"
                    + $"research={room.SupportsFacilityRole(FacilityRole.Research)},"
                    + $"closed={room.IsClosed},doors={room.Doors.Count},"
                    + $"open={room.OpenBoundaryCount},roles={room.Roles}"
                    : $"{candidate.BuildingData.objectName}@{candidate.centerPos}:room=missing";
            }));
    }

    private static int FindBlueprintOfferIndex(DailyFacilityShopRuntime shop)
    {
        if (shop == null)
        {
            return -1;
        }

        for (int index = 0; index < shop.CurrentDailyOffers.Count; index++)
        {
            FacilityShopOffer offer = shop.CurrentDailyOffers[index];
            if (offer != null
                && string.Equals(offer.OfferTypeId, FacilityShopOfferTypeIds.Blueprint, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static FacilityBlueprintSO GetBlueprintOffer(
        DailyFacilityShopRuntime shop,
        int index)
    {
        if (index < 0
            || shop == null
            || index >= shop.CurrentDailyOffers.Count)
        {
            return null;
        }

        return (shop.CurrentDailyOffers[index] as FacilityBlueprintOffer)?.Blueprint;
    }

    private static Button FindTopTabButton(TabId tabId)
    {
        return Resources.FindObjectsOfTypeAll<UITabButtonBinding>()
            .Where(binding => binding != null
                && binding.Id == tabId
                && binding.gameObject.scene.IsValid()
                && binding.gameObject.activeInHierarchy)
            .Select(binding => binding.GetComponent<Button>())
            .FirstOrDefault(button => button != null);
    }

    private static Button FindButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.name == name);
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private void ConfigureInput()
    {
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>("FirstRunObjectiveVerificationMouse");
        verificationMouse.MakeCurrent();
    }

    private void TeardownInput()
    {
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
        }

        InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition);
        }
        else if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
    }

    private void Check(bool condition, string id, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {id} {detail}");
        File.WriteAllLines(
            FirstRunObjectivePlayModeVerifier.ReportPath,
            new[] { "FIRST_RUN_OBJECTIVE IN_PROGRESS" }.Concat(report));
    }
}
