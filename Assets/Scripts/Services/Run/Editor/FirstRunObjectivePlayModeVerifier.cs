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
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class FirstRunObjectivePlayModeVerifier
{
    public const string RequestPath = "Temp/first-run-objective.request";
    public const string ReportPath = "Temp/first-run-objective-report.txt";
    public const string ScreenshotPath = "Temp/first-run-objective.png";
    public const string PhysicalFlowScreenshotPath = "Artifacts/QA/research-blueprint-physical-flow.png";

    private static bool runnerCreated;

    static FirstRunObjectivePlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request First Run Objective Verification")]
    public static void RequestRunFromMenu()
    {
        // Enter Play Mode may keep static fields when domain reload is disabled.
        // Every explicit request owns a fresh runner lifecycle.
        runnerCreated = false;
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (File.Exists(RequestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.EnterPlaymode();
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }

        if (change == PlayModeStateChange.EnteredPlayMode
            && !runnerCreated
            && File.Exists(RequestPath))
        {
            runnerCreated = true;
            GameObject runnerObject = new GameObject("First Run Objective Verification Runner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<FirstRunObjectiveVerificationRunner>();
        }
    }
}

public sealed class FirstRunObjectiveVerificationRunner : MonoBehaviour
{
    private sealed class FileBackup
    {
        public string Path;
        public byte[] Bytes;
    }

    private readonly List<string> report = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<FileBackup> backups = new List<FileBackup>();

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
            yield return new WaitForSecondsRealtime(2f);
            DungeonRuntimeLifetimeScope scope = FindScope();
            Check(scope != null, "DI_SCOPE", "active game container resolved");
            if (scope == null)
            {
                yield break;
            }

            BackupPersistentFiles(scope);
            ResetFirstRunMilestones(scope);
            IFirstRunObjectiveRuntime objective = scope.Container.Resolve<IFirstRunObjectiveRuntime>();
            Check(objective != null, "OBJECTIVE_RUNTIME", "runtime resolved");
            objective?.RefreshNow();
            Check(
                objective != null && objective.CurrentObjective == FirstRunObjectiveId.ChooseOwner,
                "INITIAL_OBJECTIVE",
                objective != null ? objective.CurrentObjective.ToString() : "missing");
            CheckNonBlocking(objective);

            yield return StartFreshRun();
            objective.RefreshNow();
            Check(
                objective.CurrentObjective == FirstRunObjectiveId.AcquireBlueprint,
                "POST_OWNER_OBJECTIVE",
                objective.CurrentObjective.ToString());
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
            RestoreFiles();

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
                == FacilityBufferDestinationAnchorKind.LiveBuilding;
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
        Check(
            archivedStack != null
                && archivedStack.State == WorldItemStackState.FacilityBuffer
                && !archivedStack.HasReservations
                && committedAfterArchive == 0,
            "BLUEPRINT_AI_HAUL_OWNERSHIP_CLEAN",
            archivedStack != null
                ? $"state={archivedStack.State}; reserved={archivedStack.ReservedQuantity}; committed={committedAfterArchive}"
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

        string researchFacilityId =
            researchFacility.RequirePersistentInstanceId().Value;
        bool arcaneIndexPresentBefore = itemRuntime.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(
                stack.DestinationId,
                researchFacilityId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.ItemId,
                DurableToolItemRules.ArcaneIndex,
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
        float expectedFirstCommittedWork =
            BlueprintResearchService.CalculateApprovedResearchWork(
                hauler,
                researchCycleWork
                * Mathf.Max(0f, firstContributionAtCommit)
                * metaResearchMultiplier);
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
                && !arcaneIndexPresentBefore
                && !knowledgeResiduePresentBefore
                && Mathf.Abs(
                    firstCommit.ApprovedWork - expectedFirstCommittedWork)
                    <= 0.011f,
            "RESEARCH_FIRST_COMMIT_MODIFIERS",
            firstCommitObserved
                ? $"actual={firstCommit.ApprovedWork:0.###}; expected={expectedFirstCommittedWork:0.###}; "
                  + $"cycle={researchCycleWork:0.###}; contribution={firstContributionAtCommit:0.###}; meta={metaResearchMultiplier:0.###}; "
                  + $"index={arcaneIndexPresentBefore}; residue={knowledgeResiduePresentBefore}; instant={debugRules.IsEnabled(DungeonDebugCheat.InstantWork)}"
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

    private static void ResetFirstRunMilestones(DungeonRuntimeLifetimeScope scope)
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

        IDungeonRunFlowRuntime runFlow =
            scope.Container.Resolve<IDungeonRunFlowRuntime>();
        runFlow.RestoreState(
            DungeonRunPhase.Preparation,
            DungeonRunOutcome.None,
            currentDay: 1,
            bossArmed: false,
            bossActive: false,
            bossCycle: 0);
    }

    private IEnumerator StartFreshRun()
    {
        Button startNew = FindButton("StartNewRunButton");
        if (startNew != null && startNew.gameObject.activeInHierarchy)
        {
            yield return Click(startNew, "new game");
            if (startNew.gameObject.activeInHierarchy)
            {
                yield return Click(startNew, "confirm new game");
            }
        }

        OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
        for (int ownerAttempt = 0;
             ownerAttempt < 4
             && (ownerManager == null || ownerManager.CurrentOwnerActor == null);
             ownerAttempt++)
        {
            Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(button => button != null
                    && button.gameObject.scene.IsValid()
                    && button.gameObject.activeInHierarchy
                    && button.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
            yield return Click(
                ownerButton,
                $"owner option attempt {ownerAttempt + 1}");
            yield return new WaitForSecondsRealtime(0.25f);
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
            yield return new WaitForSecondsRealtime(0.25f);
            ownerManager = FindFirstObjectByType<OwnerRunManager>();
        }

        Check(
            ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "PUBLIC_NEW_RUN",
            "new game and owner selected with pointer input");

        float worldReadyDeadline = Time.realtimeSinceStartup + 12f;
        CharacterActor worldActor = FindWorldReadyActor();
        while (worldActor == null && Time.realtimeSinceStartup < worldReadyDeadline)
        {
            yield return null;
            worldActor = FindWorldReadyActor();
        }
        Check(
            worldActor != null,
            "START_PARTY_WORLD_READY",
            worldActor != null
                ? $"{worldActor.name}@{worldActor.GetNowXY()}"
                : DescribeCharacterPublicationState());
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

    private void BackupPersistentFiles(DungeonRuntimeLifetimeScope scope)
    {
        IMetaProfileStore profile = scope.Container.Resolve<IMetaProfileStore>();
        BackupFile(profile.ProfilePath);
        IDungeonGameSaveSlotService slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
        foreach (DungeonSaveSlotInfo slot in slots.GetSlots())
        {
            BackupFile(slot.Path);
        }
    }

    private void BackupFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || backups.Any(backup => string.Equals(backup.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        backups.Add(new FileBackup
        {
            Path = path,
            Bytes = File.Exists(path) ? File.ReadAllBytes(path) : null
        });
    }

    private void RestoreFiles()
    {
        foreach (FileBackup backup in backups)
        {
            if (backup.Bytes == null)
            {
                if (File.Exists(backup.Path)) File.Delete(backup.Path);
                continue;
            }

            string directory = Path.GetDirectoryName(backup.Path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(backup.Path, backup.Bytes);
        }
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
