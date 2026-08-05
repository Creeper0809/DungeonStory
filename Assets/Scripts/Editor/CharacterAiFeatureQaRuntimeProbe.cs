using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorDesigner.Runtime;
using DG.Tweening;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

public static class CharacterAiFeatureQaRuntimeProbe
{
    private static readonly List<string> capturedErrors = new List<string>();
    private static string lastReport = "Character AI feature probe has not run.";
    private static string lastLongObservationReport = "Character AI long observation probe has not run.";
    private static bool capturingLogs;

    [MenuItem("DungeonStory/Debug/QA/Run Character AI Feature Probe v2")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        if (!Application.isPlaying)
        {
            lastReport = "FAIL: Probe requires PlayMode.";
            return lastReport;
        }

        StartLogCapture();

        float originalTimeScale = Time.timeScale;
        List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object>();
        List<string> lines = new List<string>();

        try
        {
            Time.timeScale = 4f;
            Scene activeScene = SceneManager.GetActiveScene();
            DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();
            List<BuildableObject> buildings = FindActiveSceneComponents<BuildableObject>()
                .Where((building) => building != null && !building.isDestroy)
                .ToList();
            List<CharacterActor> actors = FindActiveSceneComponents<CharacterActor>()
                .Where((actor) => actor != null)
                .ToList();

            lines.Add($"activeScene={activeScene.path}; buildings={buildings.Count}; actors={actors.Count}");
            RunCustomerAi(sceneQuery, tempObjects, lines);
            RunStaffDutyPriority(sceneQuery, tempObjects, lines);
            RunOwnerPriority(sceneQuery, tempObjects, lines);
            RunLocalLlmFlow(sceneQuery, tempObjects, lines);
            RunVisualFeedback(tempObjects, lines);
            RunUi(lines);

            string screenshotPath = "Temp/full-game-qa-character-ai-feature-probe.png";
            ScreenCapture.CaptureScreenshot(screenshotPath);
            lines.Add($"screenCapture={screenshotPath}");
        }
        catch (Exception ex)
        {
            lines.Add($"EXCEPTION={ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            DestroyTempObjects(tempObjects);
        }

        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
        lastReport = string.Join("\n", lines);
        return lastReport;
    }

    public static string GetReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    [MenuItem("DungeonStory/Debug/QA/Start Character AI Long Observation Probe (Standalone)")]
    public static void StartLongObservationProbeFromMenu()
    {
        Debug.Log(StartLongObservationProbe());
    }

    public static string StartLongObservationProbe()
    {
        if (!Application.isPlaying)
        {
            lastLongObservationReport = "FAIL: Probe requires PlayMode.";
            return lastLongObservationReport;
        }

        StartLogCapture();
        LongObservationRunner existing = UnityEngine.Object.FindFirstObjectByType<LongObservationRunner>();
        if (existing != null)
        {
            lastLongObservationReport = "RUNNING: Character AI long observation probe is already active.";
            return lastLongObservationReport;
        }

        GameObject runnerObject = new GameObject("QA Character AI Long Observation Runner");
        LongObservationRunner runner = runnerObject.AddComponent<LongObservationRunner>();
        lastLongObservationReport = "RUNNING: Character AI long observation probe started.";
        runner.Begin();
        return lastLongObservationReport;
    }

    public static string GetLongObservationProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastLongObservationReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    private sealed class LongObservationRunner : MonoBehaviour
    {
        public void Begin()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float originalTimeScale = Time.timeScale;
            List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object> { gameObject };
            List<string> lines = new List<string>();

            try
            {
                Time.timeScale = 12f;
                yield return null;

                Scene activeScene = SceneManager.GetActiveScene();
                DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();
                Grid grid = ResolveGrid(sceneQuery);
                lines.Add($"activeScene={activeScene.path}; grid={(grid != null ? $"{grid.width}x{grid.height}" : "<none>")}; timeScale={Time.timeScale:0.##}");

                yield return ObserveCustomerVisit(grid, tempObjects, lines);
                yield return ObserveStaffWanderAndFatigue(grid, tempObjects, lines);
                RunUi(lines);

                string screenshotPath = "Temp/full-game-qa-character-ai-long-observation.png";
                ScreenCapture.CaptureScreenshot(screenshotPath);
                lines.Add($"screenCapture={screenshotPath}");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                string errors = capturedErrors.Count == 0
                    ? "<none>"
                    : string.Join(" || ", capturedErrors.Select(CompactLog));
                lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
                lastLongObservationReport = string.Join("\n", lines);

                tempObjects.Remove(gameObject);
                DestroyTempObjects(tempObjects);
                Destroy(gameObject);
            }
        }
    }

    public static void StopLogCapture()
    {
        if (!capturingLogs)
        {
            return;
        }

        Application.logMessageReceived -= OnLogMessageReceived;
        capturingLogs = false;
    }

    private static void RunCustomerAi(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        CharacterAiScheduler scheduler = sceneQuery.First<CharacterAiScheduler>(includeInactive: true);
        CharacterActor customer = CreateQaCharacter(
            "QA Customer AI",
            CharacterType.Customer,
            CharacterRole.Regular,
            70,
            addWork: false,
            addShopping: true,
            tempObjects);
        SetCondition(customer, CharacterCondition.HUNGER, 20f);
        SetCondition(customer, CharacterCondition.FUN, 35f);
        SetCondition(customer, CharacterCondition.MOOD, 80f);

        bool decided = false;
        bool scheduledDecided = false;
        bool directFallback = false;
        int maxSchedulerProcessed = 0;
        int maxBehaviorTicks = 0;
        int treeTicksBefore = -1;
        int treeTicksAfter = -1;
        string treeBranch = string.Empty;
        string treeTask = string.Empty;
        string treeStatus = string.Empty;
        string exception = string.Empty;
        try
        {
            customer.Brain.RequestImmediateReplan(clearFailures: true);
            scheduler?.RequestImmediateDecisionFor(customer);
            BehaviorTree behaviorTree = customer.GetComponent<BehaviorTree>();
            treeTicksBefore = behaviorTree != null ? behaviorTree.DungeonStoryTickCount : -1;
            for (int i = 0; i < 5; i++)
            {
                scheduler?.RunManualTick(0.5f);
                maxSchedulerProcessed = Mathf.Max(
                    maxSchedulerProcessed,
                    scheduler != null ? scheduler.LastProcessedDecisionCount : 0);
                maxBehaviorTicks = Mathf.Max(
                    maxBehaviorTicks,
                    scheduler != null ? scheduler.LastBehaviorTreeTickCount : 0);
                scheduledDecided = scheduledDecided
                    || (customer.Brain != null && customer.Brain.bestAction != null);
            }

            behaviorTree = customer.GetComponent<BehaviorTree>();
            if (behaviorTree != null)
            {
                treeTicksAfter = behaviorTree.DungeonStoryTickCount;
                treeBranch = behaviorTree.DungeonStoryBranch;
                treeTask = behaviorTree.DungeonStoryTask;
                treeStatus = behaviorTree.DungeonStoryStatus;
            }

            if (scheduledDecided)
            {
                decided = true;
            }
            else
            {
                directFallback = true;
                decided = customer.Brain.DecideAction();
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"CUSTOMER-AI injected=True; canRun={customer.CanRunAi}; scheduler={scheduler != null}; schedulerRegistered={(scheduler != null ? scheduler.RegisteredCharacterCount : -1)}; schedulerProcessedLast={(scheduler != null ? scheduler.LastProcessedDecisionCount : -1)}; schedulerProcessedMax={maxSchedulerProcessed}; behaviorTicksLast={(scheduler != null ? scheduler.LastBehaviorTreeTickCount : -1)}; behaviorTicksMax={maxBehaviorTicks}; treeTicks={treeTicksBefore}->{treeTicksAfter}; scheduledDecided={scheduledDecided}; directFallback={directFallback}; decided={decided}; action={CompactText(customer.Brain != null ? customer.Brain.CurrentActionDebugLabel : string.Empty)}; destination={CompactText(customer.Brain != null && customer.Brain.bestAction != null && customer.Brain.bestAction.ReservedDestination != null ? customer.Brain.bestAction.ReservedDestination.name : string.Empty)}; btBranch={CompactText(treeBranch)}; btTask={CompactText(treeTask)}; btStatus={CompactText(treeStatus)}; exception={CompactText(exception)}");
    }

    private static void RunStaffDutyPriority(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        Grid grid = ResolveGrid(sceneQuery);
        CharacterActor staff = CreateQaCharacter(
            "QA Duty Staff",
            CharacterType.NPC,
            CharacterRole.Regular,
            75,
            addWork: true,
            addShopping: true,
            tempObjects);

        bool hasWork = staff.TryGetAbility(out AbilityWork work);
        bool hasShopping = staff.TryGetAbility(out AbilityShopping shopping);
        bool shouldOffDuty = false;
        bool canStartLowCondition = false;
        bool offDutyAfterLowCondition = false;
        bool offDutyVisitorDuringOffDuty = false;
        bool agedOffDutyForQa = false;
        bool returnReadyAfterRecover = false;
        bool canStartAfterRecover = false;
        bool returnedToWorkAfterRecover = false;
        bool prioritySet = false;
        string priorityMessage = string.Empty;
        string exception = string.Empty;
        BuildableObject target = null;
        WorkTypeId targetWorkType = default;

        try
        {
            if (hasWork)
            {
                SetCondition(staff, CharacterCondition.SLEEP, 5f);
                SetCondition(staff, CharacterCondition.MOOD, 10f);
                SetCondition(staff, CharacterCondition.EXCRETION, 10f);
                SetCondition(staff, CharacterCondition.HYGIENE, 10f);
                shouldOffDuty = work.ShouldTakeOffDuty();
                canStartLowCondition = work.CanStartWorkAction();
                offDutyAfterLowCondition = work.IsOffDuty;
                offDutyVisitorDuringOffDuty = shopping != null && shopping.IsOffDutyStaffVisitor();

                agedOffDutyForQa = AgeOffDutyForQa(work);
                work.RecoverOffDuty(100f, 100f, 100f, 100f, 100f, 100f);
                returnReadyAfterRecover = work.ShouldReturnToWork();
                canStartAfterRecover = work.CanStartWorkAction();
                returnedToWorkAfterRecover = work.CurrentDutyState == AbilityWork.DutyState.OnDuty;

                GridPathSearchResult search = grid != null ? grid.SearchPath(staff.GetNowXY()) : null;
                if (work.TryGetBestAnyWorkCandidate(search, out WorkTargetCandidate candidate)
                    && candidate.IsValid)
                {
                    target = WorkTargetCandidateRuntimeAdapter.ResolveBuilding(
                        candidate);
                    targetWorkType = candidate.WorkTypeId;
                    prioritySet = work.TrySetPriorityWorkTarget(target, targetWorkType, search, out priorityMessage);
                }
                else
                {
                    priorityMessage = candidate.FailureReason;
                }
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"STAFF-DUTY-PRIORITY hasWork={hasWork}; hasShopping={hasShopping}; shouldOffDuty={shouldOffDuty}; canStartLowCondition={canStartLowCondition}; offDutyAfterLowCondition={offDutyAfterLowCondition}; offDutyVisitorDuringOffDuty={offDutyVisitorDuringOffDuty}; agedOffDutyForQa={agedOffDutyForQa}; returnReadyAfterRecover={returnReadyAfterRecover}; canStartAfterRecover={canStartAfterRecover}; returnedToWorkAfterRecover={returnedToWorkAfterRecover}; prioritySet={prioritySet}; priorityTarget={(target != null ? target.name : "<none>")}; priorityType={targetWorkType}; assigned={(work != null && work.assignedShop != null ? work.assignedShop.name : "<none>")}; message={CompactText(priorityMessage)}; exception={CompactText(exception)}");
    }

    private static void RunOwnerPriority(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        Grid grid = ResolveGrid(sceneQuery);
        CharacterActor owner = CreateQaCharacter(
            "QA Owner",
            CharacterType.NPC,
            CharacterRole.Owner,
            90,
            addWork: true,
            addShopping: false,
            tempObjects);

        bool hasWork = owner.TryGetAbility(out AbilityWork work);
        bool decided = false;
        bool prioritySet = false;
        string priorityMessage = string.Empty;
        string exception = string.Empty;
        BuildableObject target = null;
        WorkTypeId targetWorkType = default;

        try
        {
            owner.Brain.RequestImmediateReplan(clearFailures: true);
            decided = owner.Brain.DecideAction();

            if (hasWork)
            {
                GridPathSearchResult search = grid != null ? grid.SearchPath(owner.GetNowXY()) : null;
                if (work.TryGetBestAnyWorkCandidate(search, out WorkTargetCandidate candidate)
                    && candidate.IsValid)
                {
                    target = WorkTargetCandidateRuntimeAdapter.ResolveBuilding(
                        candidate);
                    targetWorkType = candidate.WorkTypeId;
                    prioritySet = work.TrySetPriorityWorkTarget(target, targetWorkType, search, out priorityMessage);
                }
                else
                {
                    priorityMessage = candidate.FailureReason;
                }
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"OWNER-PRIORITY isOwner={owner.IsOwner}; hasWork={hasWork}; decided={decided}; action={CompactText(owner.Brain != null ? owner.Brain.CurrentActionDebugLabel : string.Empty)}; prioritySet={prioritySet}; priorityTarget={(target != null ? target.name : "<none>")}; priorityType={targetWorkType}; message={CompactText(priorityMessage)}; exception={CompactText(exception)}");
    }

    private static void RunLocalLlmFlow(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        LocalLlmRequestQueue queue = sceneQuery.First<LocalLlmRequestQueue>(includeInactive: true);
        if (queue == null)
        {
            GameObject queueObject = new GameObject("QA Local LLM Queue");
            tempObjects.Add(queueObject);
            queue = queueObject.AddComponent<LocalLlmRequestQueue>();
            queue.Construct(new UnityUiClock());
        }

        queue.ClearForDebug();
        queue.SetWarningLogsSuppressedForDebug(true);

        AiDirectorRuntime director = sceneQuery.First<AiDirectorRuntime>(includeInactive: true);
        if (director == null)
        {
            GameObject directorObject = new GameObject("QA AI Director");
            tempObjects.Add(directorObject);
            director = directorObject.AddComponent<AiDirectorRuntime>();
            InjectGameObjectFromLifetimeScope(directorObject);
        }

        CharacterActor actor = CreateQaCharacter(
            "QA LLM Customer",
            CharacterType.Customer,
            CharacterRole.Regular,
            80,
            addWork: false,
            addShopping: true,
            tempObjects);
        SetCondition(actor, CharacterCondition.MOOD, 35f);
        SetCondition(actor, CharacterCondition.FUN, 25f);

        bool personaRequested = false;
        bool personaApplied = false;
        bool moodRequested = false;
        bool moodApplied = false;
        bool macroRequested = false;
        bool macroApplied = false;
        bool bubbleApplied = false;
        string exception = string.Empty;

        try
        {
            personaRequested = actor.PersonaRuntime != null
                && actor.PersonaRuntime.RequestPersonaIfNeeded(logIfMissingQueue: false);
            InvokePrivateLlmResult(
                actor.PersonaRuntime,
                "OnPersonaResult",
                "{\"traitName\":\"QA Curious\",\"flavorText\":\"Checks every corner.\",\"selfCareMultiplier\":1.1,\"curiosityMultiplier\":1.4,\"shoppingMultiplier\":1.2,\"patienceMultiplier\":0.9,\"hungerCurveMultiplier\":1.0,\"funCurveMultiplier\":1.1,\"moodCurveMultiplier\":1.0,\"preferredFacilityTags\":[\"Meal\",\"Rest\"]}");
            personaApplied = actor.PersonaRuntime != null && actor.PersonaRuntime.HasGeneratedPersona;

            moodRequested = director != null && director.RequestMoodImpulse(actor);
            InvokePrivateLlmResult(
                director,
                "OnMoodImpulseResult",
                actor,
                "{\"moodImpulse\":\"Wait\",\"strength\":0.65,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"low mood wants a pause\",\"validSeconds\":30}");
            moodApplied = actor.Blackboard != null && actor.Blackboard.HasActiveMoodImpulse();

            macroRequested = director != null && director.RequestMacroGoal(actor);
            InvokePrivateLlmResult(
                director,
                "OnMacroGoalResult",
                actor,
                "{\"macroGoal\":\"SeekFun\",\"reason\":\"needs a better mood\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"validSeconds\":30}");
            macroApplied = actor.Blackboard != null && actor.Blackboard.HasActiveMacroGoal();

            if (actor.DialogueRuntime != null)
            {
                InvokePrivateLlmResult(
                    actor.DialogueRuntime,
                    "OnBubbleResult",
                    "{\"line\":\"I need a short break.\"}",
                    "original line");
                bubbleApplied = !string.IsNullOrWhiteSpace(actor.DialogueRuntime.LastGeneratedBubbleLine)
                    && !string.IsNullOrWhiteSpace(actor.DialogueRuntime.LastBubbleLine);
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            queue.AbortAllForDebug();
            queue.SetWarningLogsSuppressedForDebug(false);
        }

        lines.Add($"LOCAL-LLM-FLOW queue=True; personaRequested={personaRequested}; personaApplied={personaApplied}; trait={CompactText(actor.PersonaRuntime != null ? actor.PersonaRuntime.Persona.traitName : string.Empty)}; moodRequested={moodRequested}; moodApplied={moodApplied}; moodType={(actor.Blackboard != null && actor.Blackboard.ActiveMoodImpulse != null ? actor.Blackboard.ActiveMoodImpulse.type.ToString() : "<none>")}; macroRequested={macroRequested}; macroApplied={macroApplied}; macroType={(actor.Blackboard != null && actor.Blackboard.ActiveMacroGoal != null ? actor.Blackboard.ActiveMacroGoal.type.ToString() : "<none>")}; bubbleApplied={bubbleApplied}; bubble={CompactText(actor.DialogueRuntime != null ? actor.DialogueRuntime.LastGeneratedBubbleLine : string.Empty)}; exception={CompactText(exception)}");
    }

    private static void RunVisualFeedback(
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        CharacterActor actor = CreateQaCharacter(
            "QA Visual Feedback",
            CharacterType.NPC,
            CharacterRole.Regular,
            80,
            addWork: false,
            addShopping: false,
            tempObjects);

        bool hasVisual = actor.VisualRenderer != null || actor.VisualRoot != null;
        CharacterFeedbackBubble bubble = actor.GetComponent<CharacterFeedbackBubble>();
        bool bubblePresent = bubble != null;
        CharacterFeedbackState shownState = CharacterFeedbackState.None;
        CharacterFeedbackState persistentState = CharacterFeedbackState.None;
        string exception = string.Empty;

        try
        {
            if (bubble != null)
            {
                bubble.Show(CharacterFeedbackState.Joy);
                shownState = bubble.CurrentState;
                SetCondition(actor, CharacterCondition.SLEEP, 5f);
                persistentState = bubble.EvaluatePersistentState();
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"CHARACTER-VISUAL-FEEDBACK hasVisual={hasVisual}; bubblePresent={bubblePresent}; shownState={shownState}; persistentState={persistentState}; renderer={(actor.VisualRenderer != null ? actor.VisualRenderer.name : "<none>")}; exception={CompactText(exception)}");
    }

    private static CharacterActor CreateQaCharacter(
        string name,
        CharacterType type,
        CharacterRole role,
        int statValue,
        bool addWork,
        bool addShopping,
        List<UnityEngine.Object> tempObjects)
    {
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        tempObjects.Add(data);
        data.id = 980000 + tempObjects.Count;
        data.characterName = name;
        data.characterType = type;
        data.role = role;
        data.speciesTag = type == CharacterType.Intruder ? "Intruder" : "QA";
        data.baseStats = CharacterStatBlock.CreateDefault(statValue);
        data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

        GameObject obj = new GameObject(name);
        tempObjects.Add(obj);
        obj.AddComponent<SpriteRenderer>();
        CharacterActor actor = obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        if (addShopping)
        {
            obj.AddComponent<AbilityShopping>();
        }

        if (addWork)
        {
            obj.AddComponent<AbilityWork>();
        }

        AIBrain brain = obj.AddComponent<AIBrain>();
        brain.availableActions = type == CharacterType.Customer
            ? AiDebugScenarioActionFactory.CreateCustomerActions()
            : AiDebugScenarioActionFactory.CreateStaffActions();

        InjectGameObjectFromLifetimeScope(obj);
        actor.RefreshAbilityCache();
        actor.Initialization(data);
        actor.EnsureRuntimeState();
        InjectGameObjectFromLifetimeScope(obj);
        actor.RefreshAbilityCache();
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        SetCondition(actor, CharacterCondition.HUNGER, 100f);
        SetCondition(actor, CharacterCondition.SLEEP, 100f);
        SetCondition(actor, CharacterCondition.FUN, 100f);
        SetCondition(actor, CharacterCondition.MOOD, 100f);
        SetCondition(actor, CharacterCondition.EXCRETION, 100f);
        SetCondition(actor, CharacterCondition.HYGIENE, 100f);
        return actor;
    }

    private static void InjectGameObjectFromLifetimeScope(GameObject target)
    {
        LifetimeScope scope = FindActiveSceneLifetimeScope();
        if (scope == null || scope.Container == null || target == null)
        {
            return;
        }

        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
    }

    private static LifetimeScope FindActiveSceneLifetimeScope()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        LifetimeScope[] scopes = UnityEngine.Object.FindObjectsByType<LifetimeScope>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        return scopes.FirstOrDefault((scope) =>
                scope != null
                && scope.Container != null
                && scope.gameObject.scene == activeScene)
            ?? scopes.FirstOrDefault((scope) => scope != null && scope.Container != null);
    }

    private static Grid ResolveGrid(DungeonSceneComponentQuery sceneQuery)
    {
        GridSystemManager gridManager = sceneQuery.First<GridSystemManager>(includeInactive: true);
        return gridManager != null ? gridManager.grid : null;
    }

    private static IEnumerator ObserveCustomerVisit(
        Grid grid,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        CharacterActor customer = CreateQaCharacter(
            "QA Long Customer",
            CharacterType.Customer,
            CharacterRole.Regular,
            90,
            addWork: false,
            addShopping: true,
            tempObjects);
        bool placedAtGrid = TryPlaceActorAtNearestWalkable(grid, customer, new Vector2Int(4, 0), out Vector2Int startGrid);
        SetCondition(customer, CharacterCondition.HUNGER, 15f);
        SetCondition(customer, CharacterCondition.FUN, 20f);
        SetCondition(customer, CharacterCondition.MOOD, 85f);

        AbilityShopping shopping = customer.GetAbility<AbilityShopping>();
        AIShopping shoppingAction = Resources.Load<AIShopping>("SO/AI/Action/Shopping");
        AIAction action = shoppingAction != null ? new AIAction { actionset = shoppingAction } : null;
        bool planned = false;
        string failure = string.Empty;
        string destination = string.Empty;
        int visitCountBefore = shopping != null ? shopping.visitCount : -1;
        int visitedBefore = shopping != null && shopping.visitedBuilding != null ? shopping.visitedBuilding.Count : -1;
        Vector3 startWorld = customer.transform.position;

        if (action != null)
        {
            planned = action.SetDestinationWithFailure(customer, out AIActionFailure actionFailure);
            failure = actionFailure.ToString();
            destination = action.destination != null ? action.destination.name : string.Empty;
            if (planned)
            {
                customer.Brain.bestAction = action;
                action.MarkStarted(Time.time);
                shoppingAction.Execute(customer);
            }
        }

        bool moved = false;
        bool visited = false;
        float deadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < deadline)
        {
            moved = moved || Vector3.Distance(startWorld, customer.transform.position) > 0.1f;
            visited = shopping != null
                && ((shopping.visitedBuilding != null && shopping.visitedBuilding.Count > visitedBefore)
                    || shopping.visitCount < visitCountBefore);
            if (visited)
            {
                break;
            }

            yield return null;
        }

        action?.ReleaseReservation(customer);
        Vector2Int endGrid = grid != null ? grid.GetXY(customer.transform.position) : Vector2Int.zero;
        int visitedAfter = shopping != null && shopping.visitedBuilding != null ? shopping.visitedBuilding.Count : -1;
        int visitCountAfter = shopping != null ? shopping.visitCount : -1;

        lines.Add($"LONG-CUSTOMER-VISIT placedAtGrid={placedAtGrid}; start={startGrid}; end={endGrid}; planned={planned}; destination={CompactText(destination)}; moved={moved}; visited={visited}; visitCount={visitCountBefore}->{visitCountAfter}; visitedBuildings={visitedBefore}->{visitedAfter}; failure={CompactText(failure)}");
    }

    private static IEnumerator ObserveStaffWanderAndFatigue(
        Grid grid,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        CharacterActor wanderStaff = CreateQaCharacter(
            "QA Long Wander Staff",
            CharacterType.NPC,
            CharacterRole.Regular,
            85,
            addWork: true,
            addShopping: true,
            tempObjects);
        bool wanderPlaced = TryPlaceActorAtNearestWalkable(grid, wanderStaff, new Vector2Int(4, 0), out Vector2Int wanderStart);
        AbilityMove wanderMove = wanderStaff.GetAbility<AbilityMove>();
        Queue<GridMoveStep> previewPath = null;
        bool wanderPathFound = wanderMove != null
            && wanderMove.TryFindIdleWanderPath(1, 6, out previewPath);
        int wanderPathSteps = previewPath != null ? previewPath.Count : 0;
        Vector3 wanderStartWorld = wanderStaff.transform.position;
        bool wanderStarted = wanderMove != null && wanderMove.StartIdleWander(0.25f, 1, 6);
        bool wanderMoved = false;
        float wanderDeadline = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < wanderDeadline)
        {
            wanderMoved = wanderMoved || Vector3.Distance(wanderStartWorld, wanderStaff.transform.position) > 0.1f;
            if (wanderMoved)
            {
                break;
            }

            yield return null;
        }

        Vector2Int wanderEnd = grid != null ? grid.GetXY(wanderStaff.transform.position) : Vector2Int.zero;

        CharacterActor fatigueStaff = CreateQaCharacter(
            "QA Long Fatigue Staff",
            CharacterType.NPC,
            CharacterRole.Regular,
            90,
            addWork: true,
            addShopping: true,
            tempObjects);
        bool fatiguePlaced = TryPlaceActorAtNearestWalkable(grid, fatigueStaff, new Vector2Int(4, 0), out Vector2Int fatigueStart);
        AbilityWork work = fatigueStaff.GetAbility<AbilityWork>();
        float sleepBefore = GetCondition(fatigueStaff, CharacterCondition.SLEEP);
        float moodBefore = GetCondition(fatigueStaff, CharacterCondition.MOOD);
        bool workCandidateFound = false;
        bool workStarted = false;
        BuildableObject workTarget = null;
        WorkTypeId workType = default;
        string workMessage = string.Empty;

        if (work != null && grid != null)
        {
            GridPathSearchResult search = grid.SearchPath(fatigueStaff.GetNowXY());
            workCandidateFound = work.TryGetBestAnyWorkCandidate(search, out WorkTargetCandidate candidate)
                && candidate.IsValid;
            if (workCandidateFound)
            {
                workTarget = WorkTargetCandidateRuntimeAdapter.ResolveBuilding(
                    candidate);
                workType = candidate.WorkTypeId;
                if (workTarget != null && workTarget.buildPoses != null && workTarget.buildPoses.Count > 0)
                {
                    fatigueStaff.transform.position = grid.GetWorldPos(workTarget.buildPoses[0]);
                }

                work.StartWorking(workType, workTarget);
                workStarted = work.isWorking;
            }
            else
            {
                workMessage = candidate.FailureReason;
            }
        }

        bool fatigueChanged = false;
        float fatigueDeadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < fatigueDeadline)
        {
            fatigueChanged = GetCondition(fatigueStaff, CharacterCondition.SLEEP) < sleepBefore
                || GetCondition(fatigueStaff, CharacterCondition.MOOD) < moodBefore;
            if (fatigueChanged)
            {
                break;
            }

            yield return null;
        }

        float sleepAfter = GetCondition(fatigueStaff, CharacterCondition.SLEEP);
        float moodAfter = GetCondition(fatigueStaff, CharacterCondition.MOOD);
        Vector2Int fatigueEnd = grid != null ? grid.GetXY(fatigueStaff.transform.position) : Vector2Int.zero;

        lines.Add($"LONG-STAFF-WANDER-FATIGUE wanderPlaced={wanderPlaced}; wanderStart={wanderStart}; wanderEnd={wanderEnd}; wanderPathFound={wanderPathFound}; wanderPathSteps={wanderPathSteps}; wanderStarted={wanderStarted}; wanderMoved={wanderMoved}; fatiguePlaced={fatiguePlaced}; fatigueStart={fatigueStart}; fatigueEnd={fatigueEnd}; workCandidate={workCandidateFound}; workStarted={workStarted}; workTarget={(workTarget != null ? workTarget.name : "<none>")}; workType={workType}; fatigueChanged={fatigueChanged}; sleep={sleepBefore:0.##}->{sleepAfter:0.##}; mood={moodBefore:0.##}->{moodAfter:0.##}; message={CompactText(workMessage)}");
    }

    private static bool TryPlaceActorAtNearestWalkable(
        Grid grid,
        CharacterActor actor,
        Vector2Int preferred,
        out Vector2Int position)
    {
        position = default;
        if (grid == null || actor == null || !grid.TryFindNearestWalkablePosition(preferred, out position))
        {
            return false;
        }

        actor.transform.position = grid.GetWorldPos(position);
        return true;
    }

    private static float GetCondition(CharacterActor actor, CharacterCondition condition)
    {
        if (actor == null || actor.Stats == null)
        {
            return 0f;
        }

        return actor.Stats.Stats.TryGetValue(condition, out float value) ? value : 0f;
    }

    private static bool AgeOffDutyForQa(AbilityWork work)
    {
        if (work == null || !work.IsOffDuty)
        {
            return false;
        }

        FieldInfo dutyControllerField = typeof(AbilityWork).GetField(
            "dutyController",
            BindingFlags.Instance | BindingFlags.NonPublic);
        object dutyController = dutyControllerField?.GetValue(work);
        if (dutyController == null)
        {
            return false;
        }

        FieldInfo startedAtField = dutyController.GetType().GetField(
            "offDutyStartedAt",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (startedAtField == null)
        {
            return false;
        }

        startedAtField.SetValue(dutyController, Time.time - 999f);
        return true;
    }

    private static void SetCondition(CharacterActor actor, CharacterCondition condition, float value)
    {
        if (actor == null)
        {
            return;
        }

        actor.EnsureRuntimeState();
        if (actor.stats != null)
        {
            actor.stats[condition] = value;
        }
    }

    private static void InvokePrivateLlmResult(
        object target,
        string methodName,
        string json,
        string originalText = "")
    {
        if (target == null)
        {
            return;
        }

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(
            target,
            new object[]
            {
                new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    json,
                    string.Empty,
                    originalText)
            });
    }

    private static void InvokePrivateLlmResult(
        object target,
        string methodName,
        CharacterActor actor,
        string json)
    {
        if (target == null)
        {
            return;
        }

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(
            target,
            new object[]
            {
                actor,
                new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    json,
                    string.Empty,
                    string.Empty)
            });
    }

    private static void RunUi(List<string> lines)
    {
        RectTransform[] rects = UnityEngine.Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where((rect) => rect != null && rect.gameObject.activeInHierarchy)
            .ToArray();
        TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where((text) => text != null && text.gameObject.activeInHierarchy)
            .ToArray();
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where((button) => button != null && button.gameObject.activeInHierarchy)
            .ToArray();

        int invalid = 0;
        int oversized = 0;
        foreach (RectTransform rect in rects)
        {
            Rect worldRect = GetWorldRect(rect);
            if (float.IsNaN(worldRect.width) || float.IsNaN(worldRect.height))
            {
                invalid++;
            }

            if (worldRect.width > Screen.width * 1.2f || worldRect.height > Screen.height * 1.2f)
            {
                oversized++;
            }
        }

        lines.Add($"UI activeRects={rects.Length}; invalid={invalid}; oversized={oversized}; activeTexts={texts.Length}; activeButtons={buttons.Length}");
    }

    private static IReadOnlyList<T> FindActiveSceneComponents<T>() where T : Component
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where((candidate) => candidate != null && candidate.gameObject.scene == activeScene)
            .ToList();
    }

    private static Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float minX = corners.Min((corner) => corner.x);
        float maxX = corners.Max((corner) => corner.x);
        float minY = corners.Min((corner) => corner.y);
        float maxY = corners.Max((corner) => corner.y);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static void StartLogCapture()
    {
        capturedErrors.Clear();
        if (capturingLogs)
        {
            return;
        }

        Application.logMessageReceived += OnLogMessageReceived;
        capturingLogs = true;
    }

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
        {
            return;
        }

        capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace)
            ? condition
            : $"{condition}\n{stackTrace}");
    }

    private static void DestroyTempObjects(IEnumerable<UnityEngine.Object> tempObjects)
    {
        if (tempObjects == null)
        {
            return;
        }

        foreach (UnityEngine.Object obj in tempObjects.Where((item) => item != null).Reverse())
        {
            KillTweens(obj);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }

    private static void KillTweens(UnityEngine.Object obj)
    {
        DOTween.Kill(obj, complete: false);
        if (obj is GameObject gameObject)
        {
            foreach (Component component in gameObject.GetComponentsInChildren<Component>(true))
            {
                if (component != null)
                {
                    DOTween.Kill(component, complete: false);
                    DOTween.Kill(component.gameObject, complete: false);
                    DOTween.Kill(component.transform, complete: false);
                }
            }
        }
    }

    private static string CompactLog(string text)
    {
        return CompactText(text, 260);
    }

    private static string CompactText(string text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "<none>";
        }

        string singleLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= maxLength
            ? singleLine
            : singleLine.Substring(0, maxLength) + "...";
    }
}
