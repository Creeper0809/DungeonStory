using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
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
            int queueCountBefore = research?.State.Projects.Queue.Count ?? -1;

            Button shopTab = FindTopTabButton(TabId.Shop);
            yield return Click(shopTab, "shop tab");
            yield return new WaitForSecondsRealtime(0.25f);

            int blueprintOfferIndex = FindBlueprintOfferIndex(shop);
            FacilityBlueprintSO purchasedBlueprint = GetBlueprintOffer(shop, blueprintOfferIndex);
            Button blueprintButton = FindButton($"P0Action_ShopDaily_{blueprintOfferIndex}");
            yield return Click(blueprintButton, "daily blueprint");
            yield return new WaitForSecondsRealtime(0.25f);

            int queueCountAfter = research?.State.Projects.Queue.Count ?? -1;
            bool physicalBlueprintExists = purchasedBlueprint != null
                && itemRuntime.GetAllStacks().Any(stack => stack != null
                    && stack.Quantity > 0
                    && string.Equals(
                        stack.ItemId,
                        purchasedBlueprint.PhysicalItemId,
                        StringComparison.Ordinal));
            objective.RefreshNow();
            Check(
                blueprintOfferIndex >= 0
                    && purchasedBlueprint != null
                    && physicalBlueprintExists
                    && queueCountAfter == queueCountBefore,
                "PUBLIC_BLUEPRINT_PURCHASE",
                $"offer={blueprintOfferIndex}; blueprint={purchasedBlueprint?.id}; "
                + $"physical={physicalBlueprintExists}; queue={queueCountBefore}->{queueCountAfter}");
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
        IResearchBlueprintArchiveQuery archiveQuery =
            scope.Container.Resolve<IResearchBlueprintArchiveQuery>();
        IResearchProjectCatalog projectCatalog =
            scope.Container.Resolve<IResearchProjectCatalog>();
        IResearchQueueCommandService queueCommands =
            scope.Container.Resolve<IResearchQueueCommandService>();
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
        AIHaul haulAction = null;
        bool normalAiAlreadyReserved = blueprintStack != null
            && !string.IsNullOrWhiteSpace(blueprintStack.ReservedByPersistentId);
        if (!assignmentStatus.IsArchived
            && blueprintStack != null
            && !normalAiAlreadyReserved)
        {
            hauler = FindHauler();
            Check(
                hauler != null,
                "BLUEPRINT_HAULER_READY",
                hauler != null ? hauler.name : "staff/owner hauler missing");
            if (hauler == null)
            {
                yield break;
            }

            brain = hauler.Brain;
            brainWasEnabled = brain != null && brain.enabled;
            if (brain != null)
            {
                brain.enabled = false;
            }

            itemRuntime.PrioritizeHaul(blueprintStack.StackId);
            haulAction = ScriptableObject.CreateInstance<AIHaul>();
            haulAction.Execute(hauler);
        }
        else
        {
            Check(
                true,
                "BLUEPRINT_HAULER_READY",
                assignmentStatus.IsArchived
                    ? "일반 AI가 이미 보관을 완료함"
                    : normalAiAlreadyReserved
                        ? $"일반 AI 예약자 {blueprintStack.ReservedByPersistentId}"
                        : "일반 AI가 설계도를 운반 중");
        }

        Time.timeScale = 8f;
        float haulStartedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - haulStartedAt < 24f)
        {
            ResearchBlueprintArchiveStatus status = archiveQuery.GetStatus(blueprint);
            if (status.IsArchived)
            {
                break;
            }
            yield return null;
        }

        bool archived = archiveQuery.GetStatus(blueprint).IsArchived;
        Check(
            archived,
            "BLUEPRINT_AI_HAUL_TO_ARCHIVE",
            archived
                ? $"elapsed={Time.realtimeSinceStartup - haulStartedAt:0.0}s; {archiveQuery.GetStatus(blueprint).Location}"
                : DescribeBlueprintStack(itemRuntime, blueprint));
        if (haulAction != null)
        {
            Destroy(haulAction);
        }
        if (!archived)
        {
            if (brain != null)
            {
                brain.enabled = brainWasEnabled;
            }
            yield break;
        }

        bool mapped = projectCatalog.TryGetForBlueprint(blueprint.id, out ResearchProjectSO project);
        Check(
            mapped && project != null,
            "BLUEPRINT_PROJECT_MAPPING",
            mapped && project != null ? project.ProjectId.Value : $"blueprint={blueprint.id}");
        if (!mapped || project == null)
        {
            if (brain != null)
            {
                brain.enabled = brainWasEnabled;
            }
            yield break;
        }

        if (hauler == null)
        {
            hauler = FindHauler();
            Check(
                hauler != null,
                "RESEARCH_WORKER_READY",
                hauler != null ? hauler.name : "research worker missing");
            if (hauler == null)
            {
                yield break;
            }

            brain = hauler.Brain;
            brainWasEnabled = brain != null && brain.enabled;
            if (brain != null)
            {
                brain.enabled = false;
            }
        }

        ResearchNodeState nodeState = research.GetNodeState(project, out string nodeBlocker);
        Check(
            nodeState != ResearchNodeState.BlueprintInTransit
                && !nodeBlocker.Contains("물리 설계도", StringComparison.Ordinal),
            "BLUEPRINT_NODE_CONDITION_ACTIVATED",
            $"state={nodeState}; blocker={nodeBlocker}");

        ResearchQueueCommandResult queued = queueCommands.Enqueue(project.ProjectId);
        Check(
            queued.Succeeded,
            "PROJECT_AUTO_QUEUE",
            $"{queued.Message}; added={queued.AffectedProjects.Count}");
        if (!queued.Succeeded)
        {
            if (brain != null)
            {
                brain.enabled = brainWasEnabled;
            }
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
            if (brain != null)
            {
                brain.enabled = brainWasEnabled;
            }
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
            if (brain != null)
            {
                brain.enabled = brainWasEnabled;
            }
            yield break;
        }

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

        float researchStartedAt = Time.realtimeSinceStartup;
        int workStarts = 0;
        int directDecisionTicks = 0;
        bool wasWorking = false;
        float nextPriorityRefreshAt = researchStartedAt + 1f;
        float nextDirectDecisionAt = researchStartedAt;
        float nextNeedStabilizationAt = researchStartedAt + 0.5f;
        CharacterAiDecisionTickResult lastDecision = default;
        while (!research.State.Projects.IsCompleted(project.ProjectId)
               && Time.realtimeSinceStartup - researchStartedAt < 45f)
        {
            if (work.isWorking && !wasWorking)
            {
                workStarts++;
            }
            wasWorking = work.isWorking;

            if (Time.realtimeSinceStartup >= nextNeedStabilizationAt)
            {
                nextNeedStabilizationAt = Time.realtimeSinceStartup + 0.5f;
                StabilizeResearchWorker(hauler, deprivationRuntime);
            }

            if (brain != null
                && hauler.CanRunAi
                && !work.isWorking
                && Time.realtimeSinceStartup >= nextDirectDecisionAt)
            {
                nextDirectDecisionAt = Time.realtimeSinceStartup + 0.1f;
                lastDecision = brain.RunDecisionTreeDirect();
                directDecisionTicks++;
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

        bool completed = research.State.Projects.IsCompleted(project.ProjectId);
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
                ? $"project={project.ProjectId.Value}; starts={workStarts}; elapsed={Time.realtimeSinceStartup - researchStartedAt:0.0}s"
                : $"project={project.ProjectId.Value}; starts={workStarts}; queue={research.State.Projects.Queue.Count}; active={research.State.Projects.ActiveProjectId.Value}; "
                  + $"canRun={hauler.CanRunAi}; lifecycle={hauler.CurrentLifecycleState}; brainEnabled={brain?.enabled}; "
                  + $"decisionTicks={directDecisionTicks}; decision={lastDecision.Branch}/{lastDecision.Task}/{lastDecision.Status}; "
                  + $"action={brain?.CurrentActionDebugLabel}; phase={brain?.CurrentActionPhase}; detail={brain?.CurrentActionPhaseDetail}; "
                  + $"failure={brain?.LastActionFailure}; priority={work.PriorityWorkTypeId}/{work.PriorityWorkTarget?.name}; "
                  + $"assigned={work.AssignedWorkTypeId}/{work.assignedShop?.name}; "
                  + $"workJob={workJobAvailable}:{workJobCandidate.DebugSummary}; "
                  + $"researchTarget={researchTargetAvailable}:{WorkTargetCandidateRuntimeAdapter.ResolveBuilding(researchTargetCandidate)?.name}:{researchTargetCandidate.FailureReason}");
        if (brain != null)
        {
            brain.enabled = brainWasEnabled;
        }

        yield return CapturePhysicalFlowScreen();
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
        if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
        {
            Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(button => button != null
                    && button.gameObject.scene.IsValid()
                    && button.gameObject.activeInHierarchy
                    && button.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
            yield return Click(ownerButton, "owner option");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
        }

        Check(
            ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "PUBLIC_NEW_RUN",
            "new game and owner selected with pointer input");
        yield return new WaitForSecondsRealtime(0.25f);
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
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
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

        for (int attempt = 0; attempt < 10; attempt++)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            Vector2 buttonPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                buttonRect.TransformPoint(buttonRect.rect.center));
            if (RectTransformUtility.RectangleContainsScreenPoint(viewport, buttonPoint, null))
            {
                yield break;
            }

            Vector2 viewportPoint = RectTransformUtility.WorldToScreenPoint(
                null,
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
            + $"reserved={stack.ReservedByPersistentId}";
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
