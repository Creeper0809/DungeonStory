using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterAiNaturalnessDebugScenarios
{
    private static CharacterActor lowMoodProbeActor;
    private static Vector2Int lowMoodProbeStartCell;
    private static readonly HashSet<Vector2Int> lowMoodProbeVisitedCells = new HashSet<Vector2Int>();
    private static readonly HashSet<string> lowMoodProbeObservedStates = new HashSet<string>();
    private static double lowMoodProbeStartedAt;
    private static double lowMoodProbeNextSampleAt;
    private static double lowMoodProbeInjectAt;
    private static bool lowMoodProbeInjected;
    private static bool lowMoodProbeSawAutonomousPhase;
    private static string lowMoodProbeStatus = "Not started.";

    [MenuItem("DungeonStory/Debug/AI/Run Naturalness Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(logSuccess: true);
        if (!success)
        {
            Debug.LogError("Character AI naturalness scenarios failed.");
        }
    }

    public static void RunAllFromCommandLine()
    {
        if (!RunAll(logSuccess: true))
        {
            EditorApplication.Exit(1);
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("SoftLock interruption rules", VerifySoftLockInterruptionRules, errors);
        RunScenario("Utility factor Korean labels", VerifyUtilityFactorLabels, errors);
        RunScenario("Memory pressure", VerifyMemoryPressure, errors);
        RunScenario("World signal target overlay", VerifyWorldSignalTargetOverlay, errors);
        RunScenario("Leisure need is not a survival emergency", VerifyLeisureNeedIsNotEmergency, errors);
        RunScenario("Multiple low needs use weighted survival triage", VerifyWeightedSurvivalTriage, errors);
        RunScenario("Owner and worker can respond to depleted needs", VerifyWorkerSelfCareAccess, errors);
        RunScenario("Routine need service resumes interrupted work", VerifyRoutineNeedWorkResume, errors);
        RunScenario("External intent arbitration rejects stale completions", VerifyExternalIntentArbitration, errors);
        RunScenario("Low mood chooses autonomous movement without LLM", VerifyLowMoodAutonomy, errors);
        RunScenario("Critical logistics survives low mood autonomy", VerifyCriticalLogisticsSurvivesLowMoodAutonomy, errors);
        RunScenario("Critical mood interrupts ordinary work", VerifyCriticalMoodInterruptsWork, errors);
        RunScenario("Survival emergency interrupts only safe actions", VerifySurvivalEmergencyInterruptPolicy, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Character AI naturalness scenarios passed.");
        }

        return true;
    }

    public static string StartLowMoodMovementPlayModeProbe()
    {
        StopLowMoodMovementPlayModeProbe();
        if (!Application.isPlaying)
        {
            lowMoodProbeStatus = "FAIL: Play Mode is not active.";
            return lowMoodProbeStatus;
        }

        lowMoodProbeActor = UnityEngine.Object
            .FindObjectsByType<CharacterActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(candidate => candidate != null
                && candidate.CanRunAi
                && CharacterWorkRoleUtility.TryGetWork(candidate, out _))
            .OrderByDescending(candidate => candidate.IsOwner)
            .FirstOrDefault();
        if (lowMoodProbeActor == null)
        {
            OwnerRunManager ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
            CharacterSO defaultOwner = ownerManager != null ? ownerManager.GetDefaultOwner() : null;
            if (ownerManager != null && defaultOwner != null)
            {
                ownerManager.SelectOwner(defaultOwner, "AI 이동 검증 사장");
                lowMoodProbeActor = ownerManager.CurrentOwnerActor;
            }
        }

        if (lowMoodProbeActor == null)
        {
            lowMoodProbeStatus = "FAIL: No active worker actor found.";
            return lowMoodProbeStatus;
        }

        Time.timeScale = 1f;
        lowMoodProbeStartCell = lowMoodProbeActor.GetNowXY();
        lowMoodProbeVisitedCells.Clear();
        lowMoodProbeVisitedCells.Add(lowMoodProbeStartCell);
        lowMoodProbeObservedStates.Clear();
        lowMoodProbeInjectAt = EditorApplication.timeSinceStartup + 0.75d;
        lowMoodProbeStartedAt = 0d;
        lowMoodProbeNextSampleAt = lowMoodProbeInjectAt;
        lowMoodProbeInjected = false;
        lowMoodProbeSawAutonomousPhase = false;
        lowMoodProbeStatus = $"RUNNING-WARMUP: {lowMoodProbeActor.name} from {lowMoodProbeStartCell}";
        EditorApplication.update += TickLowMoodMovementPlayModeProbe;
        return lowMoodProbeStatus;
    }

    public static string GetLowMoodMovementPlayModeProbeStatus()
    {
        return lowMoodProbeStatus;
    }

    private static void TickLowMoodMovementPlayModeProbe()
    {
        if (!Application.isPlaying || lowMoodProbeActor == null)
        {
            lowMoodProbeStatus = "FAIL: Probe actor or Play Mode disappeared.";
            StopLowMoodMovementPlayModeProbe();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (!lowMoodProbeInjected)
        {
            if (now < lowMoodProbeInjectAt)
            {
                return;
            }

            lowMoodProbeActor.stats[CharacterCondition.HUNGER] = 100f;
            lowMoodProbeActor.stats[CharacterCondition.SLEEP] = 100f;
            lowMoodProbeActor.stats[CharacterCondition.FUN] = 100f;
            lowMoodProbeActor.stats[CharacterCondition.EXCRETION] = 100f;
            lowMoodProbeActor.stats[CharacterCondition.HYGIENE] = 100f;
            lowMoodProbeActor.stats[CharacterCondition.MOOD] = 8f;
            lowMoodProbeActor.Blackboard?.ClearMoodImpulse("Low-mood local autonomy probe.");
            lowMoodProbeStartCell = lowMoodProbeActor.GetNowXY();
            lowMoodProbeVisitedCells.Clear();
            lowMoodProbeVisitedCells.Add(lowMoodProbeStartCell);
            lowMoodProbeStartedAt = now;
            lowMoodProbeNextSampleAt = now;
            lowMoodProbeInjected = true;
            lowMoodProbeStatus = $"RUNNING: {lowMoodProbeActor.name} from {lowMoodProbeStartCell}";
            lowMoodProbeActor.Brain?.RequestImmediateReplan(clearFailures: true);
            return;
        }

        if (now < lowMoodProbeNextSampleAt)
        {
            return;
        }

        lowMoodProbeNextSampleAt = now + 0.2d;
        lowMoodProbeVisitedCells.Add(lowMoodProbeActor.GetNowXY());
        float mood = lowMoodProbeActor.stats.TryGetValue(CharacterCondition.MOOD, out float currentMood)
            ? currentMood
            : -1f;
        string action = lowMoodProbeActor.Brain != null
            ? lowMoodProbeActor.Brain.CurrentActionDebugLabel
            : "행동 없음";
        string phase = lowMoodProbeActor.Brain != null
            ? lowMoodProbeActor.Brain.CurrentActionPhase
            : string.Empty;
        string branch = lowMoodProbeActor.Blackboard != null
            ? lowMoodProbeActor.Blackboard.CurrentBranch.ToString()
            : "분기 없음";
        lowMoodProbeObservedStates.Add($"mood={mood:0.#}:{branch}:{action}/{phase}");
        lowMoodProbeSawAutonomousPhase |= phase.Contains("기분 내키는 대로", StringComparison.Ordinal)
            || phase.Contains("주변 배회", StringComparison.Ordinal)
            || action.Contains("기분 내키는 대로", StringComparison.Ordinal)
            || action.Contains("주변 배회", StringComparison.Ordinal);
        if (now - lowMoodProbeStartedAt < 6d)
        {
            return;
        }

        Vector2Int endCell = lowMoodProbeActor.GetNowXY();
        bool passed = lowMoodProbeVisitedCells.Count >= 2 && lowMoodProbeSawAutonomousPhase;
        lowMoodProbeStatus = passed
            ? $"PASS: actor={lowMoodProbeActor.name}; start={lowMoodProbeStartCell}; end={endCell}; cells={lowMoodProbeVisitedCells.Count}; states=[{string.Join(" | ", lowMoodProbeObservedStates)}]"
            : $"FAIL: actor={lowMoodProbeActor.name}; start={lowMoodProbeStartCell}; end={endCell}; cells={lowMoodProbeVisitedCells.Count}; autonomous={lowMoodProbeSawAutonomousPhase}; states=[{string.Join(" | ", lowMoodProbeObservedStates)}]";
        if (passed)
        {
            Debug.Log(lowMoodProbeStatus);
        }
        else
        {
            Debug.LogError(lowMoodProbeStatus);
        }

        StopLowMoodMovementPlayModeProbe();
    }

    private static void StopLowMoodMovementPlayModeProbe()
    {
        EditorApplication.update -= TickLowMoodMovementPlayModeProbe;
        lowMoodProbeActor = null;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario())
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifySoftLockInterruptionRules()
    {
        CharacterAiIntentState state = new CharacterAiIntentState();
        const float now = 100f;
        state.Begin(
            CharacterAiBranch.Work,
            CharacterAiIntentionType.Work,
            "작업하러 이동",
            null,
            minSeconds: 10f,
            maxSeconds: 20f,
            canInterrupt: true,
            now: now);

        return state.IsActive(now)
            && state.IsWithinMinimum(now)
            && !state.CanBreak(CharacterAiInterruptReason.CurrentActionStopped, now)
            && state.CanBreak(CharacterAiInterruptReason.NoPath, now)
            && state.CanBreak(CharacterAiInterruptReason.MoodImpulseChanged, now);
    }

    private static bool VerifyUtilityFactorLabels()
    {
        return CharacterAiUtilityText.GetFactorLabel(CharacterAiUtilityFactorKind.Queue) == "대기열"
            && CharacterAiUtilityText.GetFactorLabel(CharacterAiUtilityFactorKind.Weather) == "날씨"
            && CharacterAiUtilityText.GetBranchLabel(CharacterAiBranch.SoftLock) == "의도 유지";
    }

    private static bool VerifyMemoryPressure()
    {
        GameObject host = new GameObject("AI Naturalness Memory Test");
        try
        {
            CharacterAiMemoryRuntime memory = host.AddComponent<CharacterAiMemoryRuntime>();
            memory.Bind(null);
            memory.RecordFailure(AIActionFailureKind.NoPath, "경로 실패", Vector2Int.zero);
            memory.RecordMovement(Vector2Int.right, 8f, true);
            return memory.GetRecentFailurePressure() > 0f
                && memory.GetRecentMovementPressure() > 0f
                && memory.GetRecentMemorySummary().Contains("경로 실패");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static bool VerifyWorldSignalTargetOverlay()
    {
        CharacterAiWorldSignalSnapshot baseSnapshot = CharacterAiWorldSignalSnapshot.Neutral;
        return baseSnapshot.PathConfidence > 0f
            && baseSnapshot.QueuePressure <= 0f
            && baseSnapshot.ToCompactString().Contains("대기");
    }

    private static bool VerifyLeisureNeedIsNotEmergency()
    {
        return WithActor("Leisure Is Not Emergency", actor =>
        {
            SetNeeds(actor, hunger: 100f, sleep: 100f, fun: 0f, excretion: 100f, hygiene: 100f);
            CharacterAiDecisionContext context = CharacterAiDecisionContext.Capture(
                actor,
                CharacterAiBranch.Emergency);
            return context.StrongestNeed != CharacterCondition.FUN
                && context.StrongestNeedUrgency <= 0.001f
                && context.EmergencyScore < 0.58f;
        });
    }

    private static bool VerifyWeightedSurvivalTriage()
    {
        return WithActor("Weighted Survival Triage", actor =>
        {
            SetNeeds(actor, hunger: 10f, sleep: 5f, fun: 0f, excretion: 100f, hygiene: 0f);
            CharacterAiDecisionContext sleepFirst = CharacterAiDecisionContext.Capture(actor);
            if (sleepFirst.StrongestNeed != CharacterCondition.SLEEP)
            {
                return false;
            }

            SetNeeds(actor, hunger: 5f, sleep: 50f, fun: 0f, excretion: 100f, hygiene: 0f);
            CharacterAiDecisionContext hungerFirst = CharacterAiDecisionContext.Capture(actor);
            return hungerFirst.StrongestNeed == CharacterCondition.HUNGER
                && hungerFirst.StrongestNeedUrgency > 0.9f;
        });
    }

    private static bool VerifyWorkerSelfCareAccess()
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject("Low Need Worker Access");
        CharacterSO data = CharacterAiPlanDebugFixtures.CreateCharacterData(
            CharacterType.NPC,
            "Low Need Worker",
            "Slime");
        AIEat eat = ScriptableObject.CreateInstance<AIEat>();
        AIRest rest = ScriptableObject.CreateInstance<AIRest>();
        try
        {
            actorObject.SetActive(false);
            AbilityShopping shopping = actorObject.GetComponent<AbilityShopping>()
                ?? actorObject.AddComponent<AbilityShopping>();
            AbilityWork work = actorObject.GetComponent<AbilityWork>()
                ?? actorObject.AddComponent<AbilityWork>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorObject.SetActive(true);

            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(actorObject);
            actor.EnsureRuntimeState();
            actor.RefreshAbilityCache();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            SetNeeds(actor, hunger: 20f, sleep: 20f, fun: 100f, excretion: 100f, hygiene: 100f);
            work.SetDutyState(AbilityWork.DutyState.OnDuty);

            bool staffInterruptsForFood = work.ShouldInterruptCurrentWork(out string staffInterruptReason)
                && staffInterruptReason == "식사 필요"
                && !work.IsOffDuty;
            work.BeginOffDuty("test hunger");
            bool staffCanEat = shopping != null && eat.CanStart(actor);
            bool staffCanRest = rest.CanStart(actor);

            data.role = CharacterRole.Owner;
            actor.Initialize(data);
            work.SetDutyState(AbilityWork.DutyState.OnDuty);
            SetNeeds(actor, hunger: 20f, sleep: 20f, fun: 100f, excretion: 100f, hygiene: 100f);
            bool ownerActions = actor.Brain.availableActions != null
                && new[]
                {
                    CharacterAiBranch.Eat,
                    CharacterAiBranch.Rest,
                    CharacterAiBranch.Toilet,
                    CharacterAiBranch.Hygiene
                }.All(branch => actor.Brain.availableActions.Any(action => action?.actionset?.Branch == branch));

            bool passed = staffInterruptsForFood
                && staffCanEat
                && staffCanRest
                && ownerActions
                && eat.CanStart(actor)
                && rest.CanStart(actor);
            if (!passed)
            {
                Debug.LogError(
                    "Low-need self-care access probe failed: "
                    + $"staffInterruptsForFood={staffInterruptsForFood}:{staffInterruptReason}; staffCanEat={staffCanEat}; "
                    + $"staffCanRest={staffCanRest}; ownerActions={ownerActions}; "
                    + $"ownerCanEat={eat.CanStart(actor)}; ownerCanRest={rest.CanStart(actor)}; "
                    + $"owner={actor.IsOwner}; offDuty={work.IsOffDuty}; "
                    + $"actions={string.Join(",", actor.Brain.availableActions.Select(action => action?.actionset?.Branch.ToString() ?? "null"))}");
            }

            return passed;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(eat);
            UnityEngine.Object.DestroyImmediate(rest);
            UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyRoutineNeedWorkResume()
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            "Routine Need Work Resume");
        GameObject targetObject = new GameObject(
            "Routine Need Resume Target",
            typeof(BuildableObject));
        CharacterSO data = CharacterAiPlanDebugFixtures.CreateCharacterData(
            CharacterType.NPC,
            "Routine Need Worker",
            "Slime");
        BuildingSO targetData = CharacterAiPlanDebugFixtures.CreateBuildingData(
            910701,
            "Routine Need Resume Target");
        try
        {
            actorObject.SetActive(false);
            AbilityWork work = actorObject.GetComponent<AbilityWork>()
                ?? actorObject.AddComponent<AbilityWork>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorObject.SetActive(true);

            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(actorObject);
            actor.EnsureRuntimeState();
            actor.RefreshAbilityCache();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);

            BuildableObject target = targetObject.GetComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(target);
            target.Initialization(targetData, Vector2Int.right);
            target.SetDamaged(true);
            work.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Repair,
                WorkPriorityLevel.Priority2);
            System.Reflection.MethodInfo assignWork = typeof(AbilityWork).GetMethod(
                "AssignWork",
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(BuildableObject), typeof(WorkTypeId) },
                modifiers: null);
            assignWork?.Invoke(
                work,
                new object[] { target, BuiltInWorkTypeIds.Repair });
            bool assigned = work.assignedShop == target
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Repair;

            SetNeeds(
                actor,
                hunger: 20f,
                sleep: 100f,
                fun: 100f,
                excretion: 100f,
                hygiene: 100f);
            bool interrupted = work.ShouldInterruptCurrentWork(out string reason)
                && reason == "식사 필요"
                && assigned;

            actor.stats[CharacterCondition.HUNGER] = 100f;
            bool resumed = work.NotifyRoutineNeedServiceCompleted();
            bool passed = interrupted
                && resumed
                && work.assignedShop == target
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Repair;
            if (!passed)
            {
                Debug.LogError(
                    "Routine need resume probe failed: "
                    + $"assigned={assigned}; interrupted={interrupted}:{reason}; resumed={resumed}; "
                    + $"target={(work.assignedShop == target)}; workType={work.AssignedWorkTypeId}");
            }

            return passed;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(targetData);
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyLowMoodAutonomy()
    {
        return WithActor("Low Mood Autonomous Idle", actor =>
        {
            actor.stats[CharacterCondition.MOOD] = 10f;
            float duty = CharacterMoodImpulseUtility.ApplyRoutineBias(
                actor,
                CharacterAiBranch.DutyWork,
                30f,
                out string dutyReason);
            float idle = CharacterMoodImpulseUtility.ApplyRoutineBias(
                actor,
                CharacterAiBranch.Idle,
                10f,
                out string idleReason);
            float workJob = CharacterMoodImpulseUtility.ApplyJobGiverBias(
                actor,
                CharacterAiBranch.Work,
                1f,
                out string workJobReason);
            float waitJob = CharacterMoodImpulseUtility.ApplyJobGiverBias(
                actor,
                CharacterAiBranch.Wait,
                0.1f,
                out string waitJobReason);
            float finalWork = CharacterMoodImpulseUtility.ApplyFinalAutonomyBias(
                actor,
                CharacterAiBranch.Work,
                0.9f);
            float finalWait = CharacterMoodImpulseUtility.ApplyFinalAutonomyBias(
                actor,
                CharacterAiBranch.Wait,
                0.1f);

            return CharacterMoodImpulseUtility.ShouldPreferAutonomousIdle(actor, out string autonomyReason)
                && duty < 30f
                && idle > duty
                && waitJob > workJob
                && finalWait > finalWork
                && dutyReason.Contains("lowMoodDutyDrop", StringComparison.Ordinal)
                && idleReason.Contains("lowMoodAutonomy", StringComparison.Ordinal)
                && workJobReason.Contains("lowMoodDutyDrop", StringComparison.Ordinal)
                && waitJobReason.Contains("lowMoodAutonomy", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(autonomyReason)
                && IdleBehaviorRunner.GetSelectedBehaviorTypeNameForDebug(actor, true)
                    == nameof(MoodDrivenWanderIdleBehavior)
                && IdleBehaviorRunner.IsSelectedBehaviorMovementBasedForDebug(actor, true);
        });
    }

    private static bool VerifyExternalIntentArbitration()
    {
        return WithActor("External Intent Arbitration", actor =>
        {
            AIBrain brain = actor.Brain;
            if (brain == null
                || !brain.TryBeginExternallyDrivenAction(
                    "test:routine",
                    CharacterActionIntentKind.RoutineNeed,
                    "일상 욕구",
                    "이동",
                    string.Empty,
                    out CharacterActionIntentLease routine))
            {
                return false;
            }

            bool peerRejected = !brain.TryBeginExternallyDrivenAction(
                "test:peer-routine",
                CharacterActionIntentKind.RoutineNeed,
                "다른 일상 욕구",
                "대기",
                string.Empty,
                out _);
            bool emergencyPreempted = brain.TryBeginExternallyDrivenAction(
                "test:emergency",
                CharacterActionIntentKind.EmergencyNeed,
                "긴급 욕구",
                "실행",
                string.Empty,
                out CharacterActionIntentLease emergency);
            bool staleEndRejected = !brain.EndExternallyDrivenAction(
                routine,
                clearFailures: true);
            bool emergencyStillOwned = brain.IsExternallyDrivenActionActive
                && brain.ExternalIntentOwnerId == "test:emergency"
                && brain.ExternalIntentEpoch == emergency.Epoch;
            brain.BeginManualMoveCommand(actor.GetNowXY());
            bool directOrderProtected = brain.IsManualCommandActive
                && !brain.IsExternallyDrivenActionActive
                && !brain.TryBeginExternallyDrivenAction(
                    "test:breakdown-during-order",
                    CharacterActionIntentKind.Breakdown,
                    "붕괴",
                    "실행",
                    string.Empty,
                    out _);
            brain.CompleteManualMoveCommand(
                actor.GetNowXY(),
                succeeded: true);
            bool emergencyEnded = brain.EndExternallyDrivenAction(
                emergency,
                clearFailures: true);

            return peerRejected
                && emergencyPreempted
                && staleEndRejected
                && emergencyStillOwned
                && directOrderProtected
                && !emergencyEnded
                && !brain.IsExternallyDrivenActionActive;
        });
    }

    private static bool VerifyCriticalMoodInterruptsWork()
    {
        return WithActor("Critical Mood Work Interrupt", actor =>
        {
            AIWork workActionSet = ScriptableObject.CreateInstance<AIWork>();
            try
            {
                actor.stats[CharacterCondition.MOOD] = 12f;
                AIAction runningAction = new AIAction { actionset = workActionSet };
                bool interrupted = CharacterMoodImpulseUtility.ShouldInterruptCurrentAction(
                        actor,
                        runningAction,
                        out string reason);
                return interrupted
                    && reason.Contains("기분이 바닥", StringComparison.Ordinal);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(workActionSet);
            }
        });
    }

    private static bool VerifyCriticalLogisticsSurvivesLowMoodAutonomy()
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            "Critical Logistics Low Mood");
        CharacterSO data = CharacterAiPlanDebugFixtures.CreateCharacterData(
            CharacterType.NPC,
            "Critical Logistics Worker",
            "Slime");
        AIHaul haulActionSet = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            actorObject.SetActive(false);
            AbilityWork work = actorObject.GetComponent<AbilityWork>()
                ?? actorObject.AddComponent<AbilityWork>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorObject.SetActive(true);

            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(actorObject);
            actor.EnsureRuntimeState();
            actor.RefreshAbilityCache();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.MOOD] = 10f;
            work.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Haul,
                WorkPriorityLevel.Priority2);

            float ordinaryWork = CharacterMoodImpulseUtility.ApplyFinalAutonomyBias(
                actor,
                CharacterAiBranch.Work,
                0.8f);
            AIAction haulAction = new AIAction(
                haulActionSet,
                AIActionPlan.WithoutDestination);
            float protectedHaul = CharacterMoodImpulseUtility.ApplyFinalAutonomyBias(
                actor,
                CharacterAiBranch.Work,
                0.8f,
                haulAction);

            return ordinaryWork < 0.2f
                && Mathf.Approximately(protectedHaul, 0.8f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(haulActionSet);
            UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifySurvivalEmergencyInterruptPolicy()
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            "Survival Emergency Interrupt");
        CharacterSO data = CharacterAiPlanDebugFixtures.CreateCharacterData(
            CharacterType.NPC,
            "Survival Emergency Worker",
            "Slime");
        ProbeContinuableWorkActionSet workAction =
            ScriptableObject.CreateInstance<ProbeContinuableWorkActionSet>();
        SurvivalHardLockProbeActionSet hardLockAction =
            ScriptableObject.CreateInstance<SurvivalHardLockProbeActionSet>();
        try
        {
            actorObject.SetActive(false);
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorObject.SetActive(true);

            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(actorObject);
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            SerializedObject brainFixture = new SerializedObject(actor.Brain);
            brainFixture.FindProperty("defaultActionPersistenceSeconds").floatValue = 0f;
            brainFixture.FindProperty("actionTransitionCooldown").floatValue = 0f;
            brainFixture.ApplyModifiedPropertiesWithoutUndo();

            ProbeSafeReliefRuntime deprivation = new ProbeSafeReliefRuntime();
            CharacterAiDecisionPipeline pipeline = new CharacterAiDecisionPipeline(
                deprivation,
                deprivation);
            AIAction runningWork = new AIAction { actionset = workAction };
            runningWork.MarkStarted(Mathf.Max(0f, Time.time - 1f));
            actor.Brain.bestAction = runningWork;

            bool workCanInterrupt = !pipeline.HasLockedAction(actor)
                && pipeline.CanInterruptCurrentAction(actor);
            CharacterAiDecisionTickResult interrupted =
                pipeline.StopCurrentActionForReplan(actor);
            bool workStopped = interrupted.Handled
                && interrupted.Branch == CharacterAiBranch.InterruptCheck
                && actor.Brain.bestAction == null
                && actor.Blackboard.LastCommitBreakReason.StartsWith(
                    CharacterAiInterruptReason.SurvivalEmergency.ToString(),
                    StringComparison.Ordinal);

            AIAction runningHardLock = new AIAction { actionset = hardLockAction };
            runningHardLock.MarkStarted(Mathf.Max(0f, Time.time - 1f));
            actor.Brain.bestAction = runningHardLock;
            bool hardLockPreserved = pipeline.HasLockedAction(actor)
                && !pipeline.CanInterruptCurrentAction(actor)
                && actor.Brain.bestAction == runningHardLock;

            return workCanInterrupt && workStopped && hardLockPreserved;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(workAction);
            UnityEngine.Object.DestroyImmediate(hardLockAction);
            UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static bool WithActor(string name, Func<CharacterActor, bool> verify)
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(name);
        CharacterSO data = CharacterAiPlanDebugFixtures.CreateCharacterData(CharacterType.Customer, name, "Slime");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(actorObject);
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            return verify(actor);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static void SetNeeds(
        CharacterActor actor,
        float hunger,
        float sleep,
        float fun,
        float excretion,
        float hygiene)
    {
        actor.stats[CharacterCondition.HUNGER] = hunger;
        actor.stats[CharacterCondition.THIRST] = 100f;
        actor.stats[CharacterCondition.SLEEP] = sleep;
        actor.stats[CharacterCondition.FUN] = fun;
        actor.stats[CharacterCondition.EXCRETION] = excretion;
        actor.stats[CharacterCondition.HYGIENE] = hygiene;
        actor.stats[CharacterCondition.MOOD] = 100f;
    }
}

internal sealed class SurvivalHardLockProbeActionSet : AIActionSet
{
    public override bool IsContinuous => true;
    public override bool AllowsSurvivalEmergencyInterrupt => false;

    public override bool CanContinue(
        CharacterActor actor,
        AIAction runningAction,
        out string stopReason)
    {
        stopReason = string.Empty;
        return true;
    }
}

internal sealed class ProbeSafeReliefRuntime : ICharacterDeprivationRuntime
{
    public bool HasActiveBreakdown(CharacterActor actor) => false;
    public bool HasBreakdownKind(CharacterActor actor, CharacterBreakdownKind kind) => false;
    public bool TryGetDisplayState(CharacterActor actor, out CharacterDeprivationDisplayState displayState)
    {
        displayState = default;
        return false;
    }
    public bool TryGetSnapshot(CharacterActor actor, out CharacterDeprivationSnapshot snapshot)
    {
        snapshot = default;
        return false;
    }
    public bool TryRunActiveBreakdown(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return false;
    }
    public bool NeedsRoutineDrink(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        return false;
    }
    public bool TryRunRoutineDrink(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return false;
    }
    public bool NeedsPrimitiveMeal(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        return false;
    }
    public bool NeedsPrimitiveRest(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        return false;
    }
    public bool NeedsPrimitiveRelief(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        return false;
    }
    public bool NeedsPrimitiveWash(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        return false;
    }
    public bool TryRunPrimitiveMeal(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return false;
    }
    public bool TryRunPrimitiveRest(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return false;
    }
    public bool TryRunPrimitiveRelief(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return false;
    }
    public bool TryRunPrimitiveWash(CharacterActor actor, out string status)
    {
        status = string.Empty;
        return false;
    }
    public bool NeedsSafeEmergencyRelief(CharacterActor actor, out string reason)
    {
        reason = "갈증 10: 안전한 식수 필요";
        return actor != null;
    }
    public bool TryRunSafeEmergencyRelief(CharacterActor actor, out string status)
    {
        status = "안전한 식수로 이동";
        return actor != null;
    }
    public bool TryRunMostUrgentEmergencySelfCare(
        CharacterActor actor,
        out string status) => TryRunSafeEmergencyRelief(actor, out status);
    public CharacterDeprivationDiagnosticsSnapshot GetDiagnostics() => default;
    public void ResetDiagnostics() { }
    public void BeginBreakdownAction(CharacterActor actor, CharacterBreakdownKind kind) { }
    public bool IsSuppressible(CharacterActor actor) => false;
    public bool ApplySuppression(CharacterActor actor, float amount, out bool ended)
    {
        ended = false;
        return false;
    }
    public float GetMoveSpeedMultiplier(CharacterActor actor) => 1f;
    public float GetWorkSpeedMultiplier(CharacterActor actor) => 1f;
    public void AddInfectionBurden(CharacterActor actor, float amount) { }
    public void ReduceInfectionBurden(CharacterActor actor, float amount) { }
    public void RecordTaboo(CharacterActor actor, string memory) { }
    public void RecordTabooWitnesses(
        CharacterActor source,
        Vector2Int position,
        string memory,
        float moodPenalty) { }
    public DungeonDarkSurvivalSaveData Capture() => new DungeonDarkSurvivalSaveData();
    public DarkSurvivalRestoreCandidate BuildRestoreCandidate(
        DungeonDarkSurvivalSaveData saveData) =>
        throw new NotSupportedException();
    public void PublishRestoreCandidate(DarkSurvivalRestoreCandidate candidate) { }
    public bool DebugForceBreakdown(CharacterActor actor, CharacterBreakdownKind kind) => false;
    public bool DebugClearBreakdown(CharacterActor actor) => false;
    public bool DebugResetForDeterministicScenario(CharacterActor actor) => false;
}
