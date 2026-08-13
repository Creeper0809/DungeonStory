using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterAiPriorityCornerCaseDebugScenarios
{
    [MenuItem("DungeonStory/Debug/AI/Run Priority Corner Case Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Character AI priority corner case scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("AI action score edge cases", VerifyActionScoreEdgeCases, errors);
        RunScenario("AI action plan invariants", VerifyActionPlanInvariants, errors);
        RunScenario("Running AI action execution is idempotent", VerifyRunningActionExecutionIsIdempotent, errors);
        RunScenario("AI Execute exception terminates the expected action", VerifyExecutionExceptionTerminatesExpectedAction, errors);
        RunScenario("Lifecycle transition releases action ownership", VerifyLifecycleTransitionReleasesActionOwnership, errors);
        RunScenario("Orphan Work action is recovered at coroutine boundary", VerifyOrphanWorkActionRecovery, errors);
        RunScenario("Multi-frame self-care actions retain lifecycle ownership", VerifySelfCareActionLifecycleContracts, errors);
        RunScenario("Workforce replans preserve running non-work actions", VerifyWorkforceReplanPreservesRunningNonWorkAction, errors);
        RunScenario("Destroyed committed destination reports exact execution failure", VerifyDestroyedCommittedDestinationFailure, errors);
        RunScenario("Repeated execution failures survive action restart diagnostics", VerifyRepeatedFailureDiagnosticsAcrossRestarts, errors);
        RunScenario("Facility destruction aborts an in-flight interaction", VerifyFacilityDestructionAbortsInFlightInteraction, errors);
        RunScenario("Action replacement aborts only the obsolete interaction", VerifyActionReplacementAbortsOnlyObsoleteInteraction, errors);
        RunScenario("Candidate commit reuses decision evaluation", VerifyCandidateCommitReusesDecisionEvaluation, errors);
        RunScenario("AI selects next action after failed high-score destination", VerifyBrainSelectsNextActionAfterDestinationFailure, errors);
        RunScenario("AI tie keeps action order", VerifyBrainTieKeepsActionOrder, errors);
        RunScenario("Off priority excludes urgent automatic work", VerifyOffPriorityExcludesUrgentAutomaticWork, errors);
        RunScenario("Direct command bypasses Off through assignment", VerifyDirectCommandBypassesOffThroughAssignment, errors);
        RunScenario("Requested work type does not substitute", VerifyRequestedWorkTypeDoesNotSubstitute, errors);
        RunScenario("Unrelated work query preserves priority command", VerifyUnrelatedWorkQueryPreservesPriorityCommand, errors);
        RunScenario("Occupied priority target waits and resumes", VerifyOccupiedPriorityTargetWaitsAndResumes, errors);
        RunScenario("Invalid priority target clears and resumes automatic work", VerifyInvalidPriorityTargetClearsAndResumesAutomaticWork, errors);
        RunScenario("Nearest equivalent work target wins", VerifyNearestEquivalentWorkTargetWins, errors);
        RunScenario("Priority level beats lower urgent work", VerifyPriorityLevelBeatsLowerUrgentWork, errors);
        RunScenario("Combined priority profile edges", VerifyCombinedPriorityProfileEdges, errors);
        RunScenario("AI personality modifies action score", VerifyPersonalityModifierAffectsActionScore, errors);
        RunScenario("Occupied work target is classified", VerifyOccupiedWorkTargetFailureClassification, errors);
        RunScenario("Work and wait scores prefer real work", VerifyWorkAndWaitScoresPreferRealWork, errors);
        RunScenario("No work target uses explicit wait", VerifyNoWorkTargetUsesExplicitWait, errors);
        RunScenario("Thirst outranks work and social wait", VerifyThirstOutranksWorkAndSocialWait, errors);
        RunScenario("Emergency thirst is routed and can fall through", VerifyEmergencyThirstRouting, errors);

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
            Debug.Log("Character AI priority corner case scenarios passed.");
        }

        return true;
    }

    public static bool RunPriorityRetentionRegression(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario(
            "Unrelated work query preserves priority command",
            VerifyUnrelatedWorkQueryPreservesPriorityCommand,
            errors);
        RunScenario(
            "Occupied priority target waits and resumes",
            VerifyOccupiedPriorityTargetWaitsAndResumes,
            errors);

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
            Debug.Log("Priority command retention scenarios passed.");
        }
        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyActionPlanInvariants()
    {
        GameObject targetObject = new GameObject("AI Action Plan Target");
        try
        {
            BuildableObject target = targetObject.AddComponent<BuildableObject>();
            Queue<GridMoveStep> sourcePath = new Queue<GridMoveStep>();
            sourcePath.Enqueue(new GridMoveStep(
                Vector2Int.zero,
                Vector2Int.right,
                null,
                null,
                GridMoveType.Walk));

            AIActionPlan movePlan = AIActionPlan.MoveTo(target, sourcePath);
            sourcePath.Clear();

            bool emptyMoveRejected = false;
            try
            {
                AIActionPlan.MoveTo(target, Array.Empty<GridMoveStep>());
            }
            catch (ArgumentException)
            {
                emptyMoveRejected = true;
            }

            bool missingDestinationRejected = false;
            try
            {
                AIActionPlan.AtDestination(null);
            }
            catch (ArgumentException)
            {
                missingDestinationRejected = true;
            }

            return movePlan.Kind == AIActionPlanKind.MovePath
                && movePlan.Destination == target
                && movePlan.PathSteps.Count == 1
                && movePlan.PathSteps is not IList<GridMoveStep>
                && emptyMoveRejected
                && missingDestinationRejected
                && !typeof(AIAction).GetProperty(nameof(AIAction.destination)).CanWrite
                && !typeof(AIAction).GetProperty(nameof(AIAction.pathSteps)).CanWrite
                && !typeof(AIAction).GetProperty(nameof(AIAction.planKind)).CanWrite;
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
        }
    }

    private static bool VerifyActionScoreEdgeCases()
    {
        TestActionSet actionSet = ScriptableObject.CreateInstance<TestActionSet>();
        FixedScoreConsideration one = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        FixedScoreConsideration zero = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        FixedScoreConsideration overMax = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        FixedScoreConsideration half = ScriptableObject.CreateInstance<FixedScoreConsideration>();

        try
        {
            AIAction missingSet = new AIAction();
            bool nullActionSetScoresZero = NearlyEqual(missingSet.CalculateScore((CharacterActor)null), 0f);

            SetConsiderations(actionSet);
            bool emptyConsiderationsScoreOne = NearlyEqual(new AIAction { actionset = actionSet }.CalculateScore((CharacterActor)null), 1f);

            SetConsiderations(actionSet, one, null);
            one.FixedScore = 1f;
            bool nullConsiderationScoresZero = NearlyEqual(new AIAction { actionset = actionSet }.CalculateScore((CharacterActor)null), 0f);

            zero.FixedScore = 0f;
            SetConsiderations(actionSet, one, zero);
            bool zeroConsiderationScoresZero = NearlyEqual(new AIAction { actionset = actionSet }.CalculateScore((CharacterActor)null), 0f);

            overMax.FixedScore = 5f;
            half.FixedScore = 0.5f;
            SetConsiderations(actionSet, overMax, half);
            float clampedScore = new AIAction { actionset = actionSet }.CalculateScore((CharacterActor)null);
            bool overMaxIsClamped = NearlyEqual(clampedScore, ExpectedWeightedScore(1f, 0.5f));

            AIAction propertyClamp = new AIAction();
            propertyClamp.score = 2f;
            bool clampsHigh = NearlyEqual(propertyClamp.score, 1f);
            propertyClamp.score = -1f;
            bool clampsLow = NearlyEqual(propertyClamp.score, 0f);

            return nullActionSetScoresZero
                && emptyConsiderationsScoreOne
                && nullConsiderationScoresZero
                && zeroConsiderationScoresZero
                && overMaxIsClamped
                && clampsHigh
                && clampsLow;
        }
        finally
        {
            Object.DestroyImmediate(actionSet);
            Object.DestroyImmediate(one);
            Object.DestroyImmediate(zero);
            Object.DestroyImmediate(overMax);
            Object.DestroyImmediate(half);
        }
    }

    private static bool VerifyBrainSelectsNextActionAfterDestinationFailure()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor character = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        TestActionSet failingHighScore = CreateAction("Failing high score", 1f, requiresDestination: true, resolvesDestination: false);
        TestActionSet nextAction = CreateAction("Next action", 0.25f, requiresDestination: false, resolvesDestination: true);

        try
        {
            character.ai.availableActions = new[]
            {
                new AIAction { actionset = failingHighScore },
                new AIAction { actionset = nextAction }
            };

            bool decided = character.ai.DecideAction();
            return decided
                && character.ai.bestAction != null
                && character.ai.bestAction.actionset == nextAction
                && character.ai.bestAction.planKind == AIActionPlanKind.NoDestination;
        }
        finally
        {
            Object.DestroyImmediate(failingHighScore);
            Object.DestroyImmediate(nextAction);
        }
    }

    private static bool VerifyBrainTieKeepsActionOrder()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor character = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        TestActionSet first = CreateAction("Tie first", 0.5f, requiresDestination: false, resolvesDestination: true);
        TestActionSet second = CreateAction("Tie second", 0.5f, requiresDestination: false, resolvesDestination: true);

        try
        {
            character.ai.availableActions = new[]
            {
                new AIAction { actionset = first },
                new AIAction { actionset = second }
            };

            bool decided = character.ai.DecideAction();
            return decided
                && character.ai.bestAction != null
                && character.ai.bestAction.actionset == first;
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    private static bool VerifyOffPriorityExcludesUrgentAutomaticWork()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject damaged = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        damaged.SetDamaged(true);
        CharacterActor owner = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AbilityWork work = owner.GetAbility<AbilityWork>();
        SetOnly(work);

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool assigned = work.TryAssignShop(search);
        return !assigned
            && work.assignedShop == null
            && Mathf.Approximately(work.GetWorkUtilityScore(BuiltInWorkTypeIds.Repair, search), 0f);
    }

    private static bool VerifyDirectCommandBypassesOffThroughAssignment()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject damaged = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        damaged.SetDamaged(true);
        CharacterActor owner = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AbilityWork work = owner.GetAbility<AbilityWork>();
        SetOnly(work);

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool prioritySet = work.TrySetPriorityWorkTarget(damaged, BuiltInWorkTypeIds.Repair, search, out _);
        bool assigned = work.TryAssignWork(BuiltInWorkTypeIds.Repair, search);
        return prioritySet
            && assigned
            && work.assignedShop == damaged
            && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Repair;
    }

    private static bool VerifyRequestedWorkTypeDoesNotSubstitute()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject restRoom = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        CharacterActor owner = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AbilityWork work = owner.GetAbility<AbilityWork>();
        SetOnly(work, BuiltInWorkTypeIds.Operate, BuiltInWorkTypeIds.Repair);

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool assigned = work.TryAssignWork(BuiltInWorkTypeIds.Repair, search);
        return restRoom != null
            && !assigned
            && work.assignedShop == null;
    }

    private static bool VerifyInvalidPriorityTargetClearsAndResumesAutomaticWork()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject priorityTarget = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        BuildableObject alternateTarget = world.Place("P1_RestRoom", new Vector2Int(7, 0));
        priorityTarget.SetDamaged(true);
        alternateTarget.SetDamaged(true);

        CharacterActor owner = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AbilityWork work = owner.GetAbility<AbilityWork>();
        SetOnly(work, BuiltInWorkTypeIds.Repair);

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool prioritySet = work.TrySetPriorityWorkTarget(priorityTarget, BuiltInWorkTypeIds.Repair, search, out _);
        priorityTarget.SetDamaged(false);
        bool assigned = work.TryAssignShop(search);

        return prioritySet
            && assigned
            && work.PriorityWorkTarget == null
            && work.assignedShop == alternateTarget
            && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Repair;
    }

    private static bool VerifyNearestEquivalentWorkTargetWins()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject near = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        BuildableObject far = world.Place("P1_RestRoom", new Vector2Int(9, 0));
        near.SetDamaged(true);
        far.SetDamaged(true);

        CharacterActor owner = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AbilityWork work = owner.GetAbility<AbilityWork>();
        SetOnly(work, BuiltInWorkTypeIds.Repair);

        bool assigned = work.TryAssignShop(world.Grid.SearchPath(Vector2Int.zero));
        return assigned
            && work.assignedShop == near
            && work.assignedShop != far;
    }

    private static bool VerifyPriorityLevelBeatsLowerUrgentWork()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject damagedRepair = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        BuildableObject restockShop = world.Place("P1_LowFoodShop", new Vector2Int(9, 0));
        BuildableObject warehouse = world.Place("P1_Warehouse", new Vector2Int(14, 0));
        damagedRepair.SetDamaged(true);
        ClearShopStock(restockShop);
        ((Facility)warehouse).Inventory.SeedPhysicalStockForTest(
            StockCategory.Food,
            12);

        CharacterActor owner = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AbilityWork work = owner.GetAbility<AbilityWork>();
        SetAllOff(work);
        work.SetWorkPriority(BuiltInWorkTypeIds.Restock, WorkPriorityLevel.Priority1);
        work.SetWorkPriority(BuiltInWorkTypeIds.Repair, WorkPriorityLevel.Priority2);

        bool assigned = work.TryAssignShop(world.Grid.SearchPath(Vector2Int.zero));
        bool passed = warehouse != null
            && assigned
            && work.assignedShop == restockShop
            && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Restock;
        if (!passed)
        {
            bool foundRestock = work.TryGetBestWorkCandidate(
                BuiltInWorkTypeIds.Restock,
                world.Grid.SearchPath(Vector2Int.zero),
                out WorkTargetCandidate restockCandidate);
            bool foundRepair = work.TryGetBestWorkCandidate(
                BuiltInWorkTypeIds.Repair,
                world.Grid.SearchPath(Vector2Int.zero),
                out WorkTargetCandidate repairCandidate);
            Debug.LogError(
                $"Priority work diagnostic: assigned={assigned}; target={work.assignedShop?.name}; type={work.AssignedWorkTypeId}; "
                + $"restock={foundRestock}/{restockCandidate.Score:0.###}/{restockCandidate.FailureKind}/{restockCandidate.FailureReason}; "
                + $"repair={foundRepair}/{repairCandidate.Score:0.###}/{repairCandidate.FailureKind}/{repairCandidate.FailureReason}; "
                + $"lastRejected={work.LastRejectedWorkCandidate.FailureKind}/{work.LastRejectedWorkCandidate.FailureReason}");
        }
        return passed;
    }

    private static bool VerifyCombinedPriorityProfileEdges()
    {
        WorkPriorityProfile priorities = WorkPriorityProfile.CreateDefault();
        priorities.SetPriority(BuiltInWorkTypeIds.Operate, WorkPriorityLevel.Priority3);
        priorities.SetPriority(BuiltInWorkTypeIds.Repair, WorkPriorityLevel.Priority1);
        priorities.SetPriority(BuiltInWorkTypeIds.Guard, WorkPriorityLevel.Off);

        bool bestCombinedPriority = priorities.GetBestPriority(
                BuiltInWorkTypeIds.Operate,
                BuiltInWorkTypeIds.Repair)
            == WorkPriorityLevel.Priority1;
        bool noneIsOff = priorities.GetBestPriority() == WorkPriorityLevel.Off;

        priorities.ApplyPreferredTypes(BuiltInWorkTypeIds.Guard, BuiltInWorkTypeIds.Operate);
        bool preferredRevivesOff = priorities.GetPriority(BuiltInWorkTypeIds.Guard) == WorkPriorityLevel.Priority1;
        bool preferredUpgradesLow = priorities.GetPriority(BuiltInWorkTypeIds.Operate) == WorkPriorityLevel.Priority1;

        return bestCombinedPriority
            && noneIsOff
            && preferredRevivesOff
            && preferredUpgradesLow;
    }

    private static bool VerifyPersonalityModifierAffectsActionScore()
    {
        GameObject obj = new GameObject("Personality Score Character");
        CharacterSO source = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Owners/Owner_Slime.asset");
        CharacterSO data = source != null
            ? Object.Instantiate(source)
            : null;
        AIWait waitAction = ScriptableObject.CreateInstance<AIWait>();
        try
        {
            if (data == null)
            {
                return false;
            }
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(obj);
            data.aiPersonality.patience = 0.5f;
            character.data = data;
            character.stats = new Dictionary<CharacterCondition, float>
            {
                [CharacterCondition.MOOD] = 50f
            };

            float score = new AIAction { actionset = waitAction }.CalculateScore(CharacterActor.From(character));
            return NearlyEqual(score, 0.5f);
        }
        finally
        {
            Object.DestroyImmediate(waitAction);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(obj);
        }
    }

    private static bool VerifyOccupiedWorkTargetFailureClassification()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject shop = world.Place("P1_LowFoodShop", new Vector2Int(2, 0));
        CharacterActor first = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        CharacterActor second = world.CreateOwner("Owner_Slime", new Vector2Int(5, 0));
        if (shop is not IWorkableFacility workable)
        {
            return false;
        }

        IEnumerator allocation = workable.AllocateWorker(first.BuildingVisitor);
        allocation?.MoveNext();

        AbilityWork secondWork = second.GetAbility<AbilityWork>();
        GridPathSearchResult search = world.Grid.SearchPath(second.GetNowXY());
        bool found = secondWork.TryGetBestAnyWorkCandidate(search, out _);
        bool rejected = secondWork.TryGetLastRejectedWorkCandidate(out WorkTargetCandidate candidate);
        return !found
            && rejected
            && WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate) == shop
            && candidate.FailureKind == AIActionFailureKind.DestinationOccupied
            && !string.IsNullOrWhiteSpace(candidate.FailureReason);
    }

    private static bool VerifyWorkAndWaitScoresPreferRealWork()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject shop = world.Place("P1_LowFoodShop", new Vector2Int(2, 0));
        CharacterActor owner = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AIWork workAction = ScriptableObject.CreateInstance<AIWork>();
        AIWait waitAction = ScriptableObject.CreateInstance<AIWait>();
        try
        {
            owner.ai.availableActions = new[]
            {
                new AIAction { actionset = workAction },
                new AIAction { actionset = waitAction }
            };

            bool decided = owner.ai.DecideAction();
            return shop != null
                && decided
                && owner.ai.bestAction != null
                && owner.ai.bestAction.actionset == workAction
                && owner.ai.bestAction.score > owner.ai.availableActions[1].score;
        }
        finally
        {
            Object.DestroyImmediate(workAction);
            Object.DestroyImmediate(waitAction);
        }
    }

    private static bool VerifyNoWorkTargetUsesExplicitWait()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor owner = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AIWork workAction = ScriptableObject.CreateInstance<AIWork>();
        AIWait waitAction = ScriptableObject.CreateInstance<AIWait>();
        try
        {
            owner.ai.availableActions = new[]
            {
                new AIAction { actionset = workAction },
                new AIAction { actionset = waitAction }
            };

            bool decided = owner.ai.DecideAction();
            return decided
                && owner.ai.bestAction != null
                && owner.ai.bestAction.actionset == waitAction
                && Mathf.Approximately(owner.ai.availableActions[0].score, 0f)
                && owner.ai.availableActions[1].score > 0f;
        }
        finally
        {
            Object.DestroyImmediate(workAction);
            Object.DestroyImmediate(waitAction);
        }
    }

    private static bool VerifyDestroyedCommittedDestinationFailure()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor character = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        BuildableObject destination = world.Place("P1_RestRoom", new Vector2Int(4, 0));
        TestActionSet actionSet = CreateAction(
            "Destroyed destination action",
            1f,
            requiresDestination: true,
            resolvesDestination: true);
        actionSet.ResolvedDestination = destination;

        try
        {
            CharacterAiRuntimeDiagnosticsSnapshot before =
                character.ai.CaptureRuntimeDiagnostics();
            character.ai.availableActions = new[]
            {
                new AIAction { actionset = actionSet }
            };
            bool selected = character.ai.DecideAction();
            destination.DestroySelf();

            CharacterAiDecisionTickResult result =
                new CharacterAiDecisionPipeline(
                    NoCharacterDeprivationBoundary.Instance,
                    NoCharacterDeprivationBoundary.Instance)
                .RunSelectedAction(character, "destroyed-destination-regression");
            CharacterAiRuntimeDiagnosticsSnapshot diagnostics =
                character.ai.CaptureRuntimeDiagnostics();

            bool passed = selected
                && !result.Handled
                && result.Status.Contains(AIActionFailureKind.Destroyed.ToString())
                && character.ai.LastActionFailure.Kind == AIActionFailureKind.Destroyed
                && diagnostics.ExecutionFailures - before.ExecutionFailures == 1
                && diagnostics.ImmediateReplans - before.ImmediateReplans == 1
                && character.ai.bestAction == null;
            if (!passed)
            {
                Debug.LogError(
                    $"Destroyed destination diagnostic: selected={selected}, handled={result.Handled}, " +
                    $"status={result.Status}, lastFailure={character.ai.LastActionFailure.Kind}, " +
                    $"executionFailureDelta={diagnostics.ExecutionFailures - before.ExecutionFailures}, " +
                    $"immediateReplanDelta={diagnostics.ImmediateReplans - before.ImmediateReplans}, " +
                    $"bestActionNull={character.ai.bestAction == null}.");
            }

            return passed;
        }
        finally
        {
            Object.DestroyImmediate(actionSet);
        }
    }

    private static bool VerifyRunningActionExecutionIsIdempotent()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor character = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        TestActionSet actionSet = CreateAction(
            "Idempotent running action",
            1f,
            requiresDestination: false,
            resolvesDestination: true);

        try
        {
            character.ai.bestAction = new AIAction(
                actionSet,
                AIActionPlan.WithoutDestination);
            character.ai.isBestActionEnd = false;
            character.ai.isExecuted = false;
            CharacterAiRuntimeDiagnosticsSnapshot before =
                character.ai.CaptureRuntimeDiagnostics();

            bool first = character.TryExecuteSelectedAiAction();
            bool second = character.TryExecuteSelectedAiAction();
            CharacterAiRuntimeDiagnosticsSnapshot after =
                character.ai.CaptureRuntimeDiagnostics();

            return first
                && second
                && actionSet.ExecuteCount == 1
                && character.ai.bestAction.HasStarted
                && character.ai.isExecuted
                && after.ActionStarts - before.ActionStarts == 1
                && after.DuplicateExecutionSuppressions
                    - before.DuplicateExecutionSuppressions == 1;
        }
        finally
        {
            Object.DestroyImmediate(actionSet);
        }
    }

    private static bool VerifyExecutionExceptionTerminatesExpectedAction()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor actor = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        TestActionSet actionSet = CreateAction(
            "Throwing action",
            1f,
            requiresDestination: false,
            resolvesDestination: true);
        actionSet.ThrowOnExecute = true;

        try
        {
            actor.ai.bestAction = new AIAction(
                actionSet,
                AIActionPlan.WithoutDestination);
            actor.ai.isBestActionEnd = false;
            actor.ai.isExecuted = false;
            CharacterAiRuntimeDiagnosticsSnapshot before =
                actor.ai.CaptureRuntimeDiagnostics();

            bool executed = actor.TryExecuteSelectedAiAction();
            CharacterAiRuntimeDiagnosticsSnapshot after =
                actor.ai.CaptureRuntimeDiagnostics();

            return !executed
                && actionSet.ExecuteCount == 1
                && actionSet.StopCount == 1
                && actor.ai.bestAction == null
                && !actor.ai.isExecuted
                && actor.ai.isBestActionEnd
                && after.ExecutionFailures - before.ExecutionFailures == 1
                && after.LastExecutionFailureDetail.Contains(
                    nameof(InvalidOperationException),
                    StringComparison.Ordinal);
        }
        finally
        {
            Object.DestroyImmediate(actionSet);
        }
    }

    private static bool VerifyLifecycleTransitionReleasesActionOwnership()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor actor = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        TestActionSet actionSet = CreateAction(
            "Lifecycle-owned action",
            1f,
            requiresDestination: false,
            resolvesDestination: true);

        try
        {
            actor.ai.bestAction = new AIAction(
                actionSet,
                AIActionPlan.WithoutDestination);
            actor.ai.isBestActionEnd = false;
            actor.ai.NotifyActionStarted();

            actor.SetLifecycleState(CharacterLifecycleState.Downed);
            bool stopped = actionSet.StopCount == 1
                && actor.ai.bestAction == null
                && !actor.ai.isExecuted
                && !actor.ai.isBestActionEnd
                && actor.CurrentLifecycleState == CharacterLifecycleState.Downed;

            actor.SetLifecycleState(CharacterLifecycleState.Active);
            return stopped
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                && actor.ai.isBestActionEnd;
        }
        finally
        {
            Object.DestroyImmediate(actionSet);
        }
    }

    private static bool VerifySelfCareActionLifecycleContracts()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor actor = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AIEat eat = ScriptableObject.CreateInstance<AIEat>();
        AIRest rest = ScriptableObject.CreateInstance<AIRest>();
        AIFacilityRoleAction toilet =
            ScriptableObject.CreateInstance<AIFacilityRoleAction>();
        AIFacilityRoleAction hygiene =
            ScriptableObject.CreateInstance<AIFacilityRoleAction>();
        AIFacilityRoleAction recreation =
            ScriptableObject.CreateInstance<AIFacilityRoleAction>();
        toilet.Role = FacilityRole.Toilet;
        hygiene.Role = FacilityRole.Hygiene;
        recreation.Role = FacilityRole.Entertainment;

        try
        {
            AIActionSet[] actions = { eat, rest, toilet, hygiene, recreation };
            return actions.All(action =>
            {
                AIAction running = new(action, AIActionPlan.WithoutDestination);
                actor.ai.bestAction = running;
                actor.ai.isBestActionEnd = false;
                actor.ai.NotifyActionStarted();
                bool passed = action.IsContinuous
                    && action.MinimumDuration > 0f
                    && action.AllowsSurvivalEmergencyInterrupt
                    && action.CanContinue(actor, running, out _)
                    && !actor.ai.CanInterruptCurrentActionForSurvivalEmergency(
                        out _);
                actor.ai.bestAction = null;
                actor.ai.isBestActionEnd = true;
                return passed;
            });
        }
        finally
        {
            Object.DestroyImmediate(eat);
            Object.DestroyImmediate(rest);
            Object.DestroyImmediate(toilet);
            Object.DestroyImmediate(hygiene);
            Object.DestroyImmediate(recreation);
        }
    }

    private static bool VerifyOrphanWorkActionRecovery()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor actor = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AbilityWork work = actor.GetComponent<AbilityWork>();
        AIWork workActionSet = ScriptableObject.CreateInstance<AIWork>();
        try
        {
            AIAction running = new(workActionSet, AIActionPlan.WithoutDestination);
            actor.ai.bestAction = running;
            actor.ai.isBestActionEnd = false;
            actor.ai.NotifyActionStarted();
            work.isWorking = false;
            CharacterAiRuntimeDiagnosticsSnapshot before =
                actor.ai.CaptureRuntimeDiagnostics();

            typeof(AbilityWork).GetMethod(
                    "ClearActiveWorkRoutine",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(
                    work,
                    new object[] { work.ActiveWorkRunIdForDiagnostics });
            CharacterAiRuntimeDiagnosticsSnapshot after =
                actor.ai.CaptureRuntimeDiagnostics();

            return actor.ai.isBestActionEnd
                && !actor.ai.isExecuted
                && after.OrphanWorkActionRecoveries
                    - before.OrphanWorkActionRecoveries == 1
                && after.LastOrphanWorkActionRecoveryDetail.Contains(
                    "active-work-coroutine-finalized",
                    StringComparison.Ordinal);
        }
        finally
        {
            Object.DestroyImmediate(workActionSet);
        }
    }

    private static bool VerifyWorkforceReplanPreservesRunningNonWorkAction()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor actor = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AbilityWork work = actor.GetComponent<AbilityWork>();
        AIFacilityRoleAction recreation =
            ScriptableObject.CreateInstance<AIFacilityRoleAction>();
        recreation.Role = FacilityRole.Entertainment;

        try
        {
            AIAction running = new(recreation, AIActionPlan.WithoutDestination);
            actor.ai.bestAction = running;
            actor.ai.isBestActionEnd = false;
            actor.ai.isExecuted = false;
            actor.ai.NotifyActionStarted();
            // AbilityWork can remain true for one scheduling boundary after a
            // routine-need interruption. Preservation must follow the current
            // AI action semantic, not this stale execution flag.
            work.isWorking = true;

            CharacterAiRuntimeDiagnosticsSnapshot before =
                actor.ai.CaptureRuntimeDiagnostics();
            actor.ai.RequestImmediateReplan(clearFailures: true);
            CharacterAiRuntimeDiagnosticsSnapshot diagnostics =
                actor.ai.CaptureRuntimeDiagnostics();
            AIWork workActionSet = ScriptableObject.CreateInstance<AIWork>();
            AIAction runningWork = new(
                workActionSet,
                AIActionPlan.WithoutDestination);
            actor.ai.bestAction = runningWork;
            actor.ai.isBestActionEnd = false;
            actor.ai.NotifyActionStarted();
            bool preservesRunningWorkWakeup =
                DungeonWorkforceReplanService.ShouldPreserveRunningNonWorkAction(
                    work,
                    actor.ai,
                    forceInterrupt: false);
            Object.DestroyImmediate(workActionSet);

            return preservesRunningWorkWakeup
                && diagnostics.ProtectedRunningActionReplans
                    == before.ProtectedRunningActionReplans + 1
                && diagnostics.ImmediateReplans == before.ImmediateReplans
                && DungeonWorkforceReplanService.ShouldPreserveRunningNonWorkAction(
                    work,
                    actor.ai,
                    forceInterrupt: false)
                && !DungeonWorkforceReplanService.ShouldPreserveRunningNonWorkAction(
                    work,
                    actor.ai,
                    forceInterrupt: true);
        }
        finally
        {
            Object.DestroyImmediate(recreation);
        }
    }

    private static bool VerifyRepeatedFailureDiagnosticsAcrossRestarts()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor character = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        TestActionSet actionSet = CreateAction(
            "Repeated failure action",
            1f,
            requiresDestination: false,
            resolvesDestination: true);

        try
        {
            character.ai.bestAction = new AIAction(
                actionSet,
                AIActionPlan.WithoutDestination);
            AIActionFailure failure = AIActionFailure.Create(
                AIActionFailureKind.CannotStart,
                "forced repeated start failure");
            for (int i = 0; i < 3; i++)
            {
                character.ai.NotifyActionStarted();
                ReportRuntimeActionFailureForTest(
                    character.ai,
                    failure,
                    requestImmediateReplan: false);
            }

            CharacterAiRuntimeDiagnosticsSnapshot diagnostics =
                character.ai.CaptureRuntimeDiagnostics();
            return diagnostics.ExecutionFailures == 3
                && diagnostics.CurrentRepeatedFailureCount == 3
                && diagnostics.PeakRepeatedFailureCount == 3
                && diagnostics.RepeatedFailureKind == AIActionFailureKind.CannotStart;
        }
        finally
        {
            Object.DestroyImmediate(actionSet);
        }
    }

    private static void ReportRuntimeActionFailureForTest(
        AIBrain brain,
        AIActionFailure failure,
        bool requestImmediateReplan)
    {
        MethodInfo method = typeof(AIBrain).GetMethod(
            "ReportRuntimeActionFailure",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new MissingMethodException(
                typeof(AIBrain).FullName,
                "ReportRuntimeActionFailure");
        }

        method.Invoke(
            brain,
            new object[] { failure, requestImmediateReplan });
    }

    private static bool VerifyFacilityDestructionAbortsInFlightInteraction()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor actor = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        Facility facility = world.Place("P1_RestRoom", new Vector2Int(4, 0)) as Facility;
        TestActionSet actionSet = CreateAction(
            "In-flight destroyed facility",
            1f,
            requiresDestination: true,
            resolvesDestination: true);
        actionSet.ResolvedDestination = facility;

        try
        {
            if (facility == null)
            {
                return false;
            }

            actor.stats[CharacterCondition.SLEEP] = 10f;
            float sleepBefore = actor.stats[CharacterCondition.SLEEP];
            AIAction action = new AIAction(
                actionSet,
                AIActionPlan.AtDestination(facility));
            actor.ai.bestAction = action;
            actor.ai.isBestActionEnd = false;
            actor.GetAbility<AbilityShopping>()?.BeginVisitInteraction(facility);

            IEnumerator interaction = facility.Interact(actor.BuildingVisitor);
            bool reachedFirstYield = interaction.MoveNext();
            int usersBeforeDestroy = facility.CurrentUserCount;
            facility.CanQueueVisit(actor.BuildingVisitor, out string queueReasonBeforeDestroy);
            facility.CanVisit(actor.BuildingVisitor, out string visitReasonBeforeDestroy);
            facility.DestroySelf();
            bool yieldedAfterDestroy = interaction.MoveNext();
            CharacterAiRuntimeDiagnosticsSnapshot diagnostics =
                actor.ai.CaptureRuntimeDiagnostics();
            ShoppingVisitOutcome outcome =
                actor.GetAbility<AbilityShopping>()?.LastVisitOutcome
                ?? ShoppingVisitOutcome.None;

            bool passed = reachedFirstYield
                && usersBeforeDestroy == 1
                && !yieldedAfterDestroy
                && Mathf.Approximately(actor.stats[CharacterCondition.SLEEP], sleepBefore)
                && outcome == ShoppingVisitOutcome.Abandoned
                && diagnostics.ExecutionFailures == 1
                && actor.ai.LastActionFailure.Kind == AIActionFailureKind.Destroyed
                && actor.ai.bestAction == null;
            if (!passed)
            {
                Debug.LogError(
                    "In-flight facility destruction detail: "
                    + $"firstYield={reachedFirstYield}; users={usersBeforeDestroy}; "
                    + $"id={actor.Identity?.PersistentId}; queue={queueReasonBeforeDestroy}; visit={visitReasonBeforeDestroy}; "
                    + $"afterDestroyYield={yieldedAfterDestroy}; "
                    + $"sleep={sleepBefore:0.###}->{actor.stats[CharacterCondition.SLEEP]:0.###}; "
                    + $"outcome={outcome}; failures={diagnostics.ExecutionFailures}; "
                    + $"lastFailure={actor.ai.LastActionFailure.Kind}; "
                    + $"bestAction={actor.ai.bestAction != null}.");
            }
            return passed;
        }
        finally
        {
            Object.DestroyImmediate(actionSet);
        }
    }

    private static bool VerifyActionReplacementAbortsOnlyObsoleteInteraction()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor actor = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        Facility facility = world.Place("P1_RestRoom", new Vector2Int(4, 0)) as Facility;
        TestActionSet obsoleteActionSet = CreateAction(
            "Obsolete facility interaction",
            1f,
            requiresDestination: true,
            resolvesDestination: true);
        TestActionSet replacementActionSet = CreateAction(
            "Replacement action",
            1f,
            requiresDestination: false,
            resolvesDestination: true);
        obsoleteActionSet.ResolvedDestination = facility;

        try
        {
            if (facility == null)
            {
                return false;
            }

            actor.stats[CharacterCondition.SLEEP] = 10f;
            float sleepBefore = actor.stats[CharacterCondition.SLEEP];
            AIAction obsoleteAction = new AIAction(
                obsoleteActionSet,
                AIActionPlan.AtDestination(facility));
            AIAction replacementAction = new AIAction(
                replacementActionSet,
                AIActionPlan.WithoutDestination);
            actor.ai.bestAction = obsoleteAction;
            actor.ai.isBestActionEnd = false;
            actor.GetAbility<AbilityShopping>()?.BeginVisitInteraction(facility);

            IEnumerator interaction = facility.Interact(actor.BuildingVisitor);
            bool reachedFirstYield = interaction.MoveNext();
            int usersBeforeReplacement = facility.CurrentUserCount;
            facility.CanQueueVisit(actor.BuildingVisitor, out string queueReasonBeforeReplacement);
            facility.CanVisit(actor.BuildingVisitor, out string visitReasonBeforeReplacement);
            actor.ai.bestAction = replacementAction;
            bool yieldedAfterReplacement = interaction.MoveNext();
            CharacterAiRuntimeDiagnosticsSnapshot diagnostics =
                actor.ai.CaptureRuntimeDiagnostics();
            ShoppingVisitOutcome outcome =
                actor.GetAbility<AbilityShopping>()?.LastVisitOutcome
                ?? ShoppingVisitOutcome.None;

            bool passed = reachedFirstYield
                && usersBeforeReplacement == 1
                && !yieldedAfterReplacement
                && facility.CurrentUserCount == 0
                && Mathf.Approximately(actor.stats[CharacterCondition.SLEEP], sleepBefore)
                && outcome == ShoppingVisitOutcome.Abandoned
                && diagnostics.ExecutionFailures == 0
                && diagnostics.InteractionActionReplacements == 1
                && ReferenceEquals(actor.ai.bestAction, replacementAction);
            if (!passed)
            {
                Debug.LogError(
                    "Action replacement interaction detail: "
                    + $"firstYield={reachedFirstYield}; users={usersBeforeReplacement}->{facility.CurrentUserCount}; "
                    + $"id={actor.Identity?.PersistentId}; queue={queueReasonBeforeReplacement}; visit={visitReasonBeforeReplacement}; "
                    + $"afterReplacementYield={yieldedAfterReplacement}; "
                    + $"sleep={sleepBefore:0.###}->{actor.stats[CharacterCondition.SLEEP]:0.###}; "
                    + $"outcome={outcome}; failures={diagnostics.ExecutionFailures}; "
                    + $"interactionReplacements={diagnostics.InteractionActionReplacements}; "
                    + $"replacementPreserved={ReferenceEquals(actor.ai.bestAction, replacementAction)}.");
            }
            return passed;
        }
        finally
        {
            Object.DestroyImmediate(obsoleteActionSet);
            Object.DestroyImmediate(replacementActionSet);
        }
    }

    private static bool VerifyCandidateCommitReusesDecisionEvaluation()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor character = world.CreateOwner(
            "Owner_Slime",
            Vector2Int.zero);
        TestActionSet actionSet = CreateAction(
            "Decision-local evaluation",
            0.75f,
            requiresDestination: false,
            resolvesDestination: true);

        try
        {
            character.ai.availableActions = new[]
            {
                new AIAction { actionset = actionSet }
            };

            bool selected = character.ai.TryFindBestScoredAction(
                candidate => ReferenceEquals(candidate, actionSet),
                out CharacterAiActionCandidate candidate);
            int evaluationsBeforeCommit = actionSet.CanStartRequestCount;
            AIActionFailure failure = AIActionFailure.None;
            bool committed = selected
                && character.ai.TryCommitActionCandidate(
                    candidate,
                    out failure);

            bool passed = selected
                && committed
                && !failure.HasFailure
                && evaluationsBeforeCommit == 1
                && actionSet.CanStartRequestCount == 1
                && character.ai.bestAction?.actionset == actionSet;
            if (!passed)
            {
                Debug.LogError(
                    "Decision-local candidate evaluation was not reused: "
                    + $"selected={selected}; committed={committed}; "
                    + $"before={evaluationsBeforeCommit}; "
                    + $"after={actionSet.CanStartRequestCount}; "
                    + $"failure={failure}");
            }

            return passed;
        }
        finally
        {
            Object.DestroyImmediate(actionSet);
        }
    }

    private static bool VerifyThirstOutranksWorkAndSocialWait()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor thirsty = world.CreateOwner("Owner_Slime", Vector2Int.zero);
        AIWait waitAction = ScriptableObject.CreateInstance<AIWait>();
        try
        {
            thirsty.stats[CharacterCondition.HUNGER] = 100f;
            thirsty.stats[CharacterCondition.THIRST] = 0f;
            thirsty.stats[CharacterCondition.SLEEP] = 100f;
            thirsty.stats[CharacterCondition.EXCRETION] = 100f;
            thirsty.stats[CharacterCondition.HYGIENE] = 100f;
            thirsty.stats[CharacterCondition.FUN] = 100f;
            thirsty.stats[CharacterCondition.MOOD] = 100f;

            CharacterAiDecisionContext drinkContext =
                CharacterAiDecisionContext.Capture(
                    thirsty,
                    CharacterAiBranch.Drink);
            new DrinkJobGiver().TryEvaluate(
                thirsty,
                out CharacterAiJobCandidate drinkCandidate);
            new WorkJobGiver().TryEvaluate(
                thirsty,
                out CharacterAiJobCandidate workCandidate);
            new WaitJobGiver().TryEvaluate(
                thirsty,
                out CharacterAiJobCandidate waitCandidate);
            float drinkDomain = drinkCandidate.DomainScore;
            float workDomain = workCandidate.DomainScore;
            float waitDomain = waitCandidate.DomainScore;
            bool priorityUsesThirst = drinkContext.GetPriorityScore(
                    CharacterAiBranch.Drink)
                >= 0.99f;
            waitAction.Execute(thirsty);
            bool waitIsExplicitlySurvivalBlocked =
                thirsty.Brain.CurrentActionPhaseDetail.Contains(
                    "THIRST=",
                    StringComparison.Ordinal);
            return drinkDomain > workDomain
                && drinkDomain > waitDomain
                && priorityUsesThirst
                && waitIsExplicitlySurvivalBlocked;
        }
        finally
        {
            Object.DestroyImmediate(waitAction);
        }
    }

    private static bool VerifyEmergencyThirstRouting()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        CharacterActor thirsty = world.CreateOwner(
            "Owner_Slime",
            Vector2Int.zero);
        thirsty.stats[CharacterCondition.HUNGER] = 100f;
        thirsty.stats[CharacterCondition.THIRST] = 0f;
        thirsty.stats[CharacterCondition.SLEEP] = 100f;
        thirsty.stats[CharacterCondition.EXCRETION] = 100f;
        thirsty.stats[CharacterCondition.HYGIENE] = 100f;
        thirsty.stats[CharacterCondition.FUN] = 100f;
        thirsty.stats[CharacterCondition.MOOD] = 100f;

        CharacterAiDecisionPipeline pipeline =
            thirsty.Brain.RequireDecisionPipeline()
                as CharacterAiDecisionPipeline;
        MethodInfo builder = typeof(CharacterAiDecisionPipeline).GetMethod(
            "BuildEmergencyJobGivers",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (pipeline == null || builder == null)
        {
            return false;
        }

        CharacterAiDecisionContext context =
            CharacterAiDecisionContext.Capture(
                thirsty,
                CharacterAiBranch.Emergency);
        IReadOnlyList<CharacterAiJobGiver> emergency =
            builder.Invoke(pipeline, new object[] { thirsty, context })
                as IReadOnlyList<CharacterAiJobGiver>;
        bool drinkIncluded = emergency?.Any(
            giver => giver?.Branch == CharacterAiBranch.Drink) == true;
        bool waitExcluded = emergency?.All(
            giver => giver?.Branch != CharacterAiBranch.Wait) == true;
        bool workExcluded = emergency?.All(
            giver => giver?.Branch != CharacterAiBranch.Work) == true;

        CharacterAiDecisionTickResult result =
            pipeline.RunEmergencyDecision(thirsty);
        bool unavailableEmergencyFallsThrough = !result.Handled;
        return context.EmergencyScore >= 0.58f
            && CharacterNeedAiThresholds.IsEmergency(
                thirsty,
                CharacterCondition.THIRST)
            && drinkIncluded
            && waitExcluded
            && workExcluded
            && unavailableEmergencyFallsThrough;
    }


    private static TestActionSet CreateAction(
        string actionName,
        float score,
        bool requiresDestination,
        bool resolvesDestination)
    {
        TestActionSet action = ScriptableObject.CreateInstance<TestActionSet>();
        action.actionName = actionName;
        action.RequireDestination = requiresDestination;
        action.ResolveDestination = resolvesDestination;

        FixedScoreConsideration consideration = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        consideration.FixedScore = score;
        action.OwnedConsideration = consideration;
        SetConsiderations(action, consideration);
        return action;
    }

    private static void SetOnly(AbilityWork work, params WorkTypeId[] enabledTypes)
    {
        SetAllOff(work);
        foreach (WorkTypeId workTypeId in enabledTypes)
        {
            if (WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition))
            {
                work.SetWorkPriority(definition.WorkTypeId, WorkPriorityLevel.Priority1);
            }
        }
    }

    private static void SetAllOff(AbilityWork work)
    {
        foreach (WorkTypeDefinition definition in WorkTaskCatalog.Definitions)
        {
            work.SetWorkPriority(definition.WorkTypeId, WorkPriorityLevel.Off);
        }
    }

    private static bool VerifyUnrelatedWorkQueryPreservesPriorityCommand()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject priorityTarget = world.Place(
            "P1_RestRoom",
            new Vector2Int(2, 0));
        priorityTarget.SetDamaged(true);

        CharacterActor owner = world.CreateOwner(
            "Owner_Slime",
            Vector2Int.zero);
        AbilityWork work = owner.GetAbility<AbilityWork>();
        SetOnly(work, BuiltInWorkTypeIds.Repair, BuiltInWorkTypeIds.Research);

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool prioritySet = work.TrySetPriorityWorkTarget(
            priorityTarget,
            BuiltInWorkTypeIds.Repair,
            search,
            out _);
        bool unrelatedAssigned = work.TryAssignWork(
            BuiltInWorkTypeIds.Research,
            search);

        return prioritySet
            && !unrelatedAssigned
            && work.PriorityWorkTarget == priorityTarget
            && work.PriorityWorkTypeId == BuiltInWorkTypeIds.Repair;
    }

    private static bool VerifyOccupiedPriorityTargetWaitsAndResumes()
    {
        using PriorityScenarioWorld world = new PriorityScenarioWorld();
        BuildableObject priorityTarget = world.Place(
            "P1_RestRoom",
            new Vector2Int(2, 0));
        priorityTarget.SetDamaged(true);

        CharacterActor owner = world.CreateOwner(
            "Owner_Slime",
            Vector2Int.zero);
        CharacterActor blocker = world.CreateOwner(
            "Owner_Slime",
            new Vector2Int(5, 0));
        AbilityWork work = owner.GetAbility<AbilityWork>();
        SetOnly(work, BuiltInWorkTypeIds.Repair);

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool prioritySet = work.TrySetPriorityWorkTarget(
            priorityTarget,
            BuiltInWorkTypeIds.Repair,
            search,
            out _);
        if (!prioritySet || priorityTarget is not IWorkableFacility workable)
        {
            return false;
        }

        IEnumerator allocation = workable.AllocateWorker(blocker.BuildingVisitor);
        allocation?.MoveNext();
        bool assignedWhileOccupied = work.TryAssignWork(
            BuiltInWorkTypeIds.Repair,
            search);
        bool retainedWhileOccupied = !assignedWhileOccupied
            && work.PriorityWorkTarget == priorityTarget
            && work.TryGetLastRejectedWorkCandidate(out WorkTargetCandidate rejected)
            && rejected.FailureKind == AIActionFailureKind.DestinationOccupied;

        workable.DeallocateWorker(blocker.BuildingVisitor);
        bool assignedAfterRelease = work.TryAssignWork(
            BuiltInWorkTypeIds.Repair,
            search);
        return retainedWhileOccupied
            && assignedAfterRelease
            && work.PriorityWorkTarget == priorityTarget
            && work.assignedShop == priorityTarget
            && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Repair;
    }

    private static void ClearShopStock(BuildableObject building)
    {
        if (building is Shop shop)
        {
            shop.DebugClearStock();
        }
        FacilityCandidateCache.MarkDynamicStateDirty();
    }

    private static void SetConsiderations(AIActionSet actionSet, params Consideration[] considerations)
    {
        FieldInfo field = typeof(AIActionSet).GetField(
            "<considerations>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(actionSet, considerations);
    }

    private static float ExpectedWeightedScore(params float[] scores)
    {
        if (scores == null || scores.Length == 0)
        {
            return 1f;
        }

        float totalScore = 0f;
        foreach (float score in scores)
        {
            float clampedScore = Mathf.Clamp01(score);
            if (clampedScore <= 0f)
            {
                return 0f;
            }

            totalScore += clampedScore;
        }

        return Mathf.Clamp01(totalScore / scores.Length);
    }

    private static bool NearlyEqual(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f;
    }

    private static bool IsAdjacentWalkPath(Queue<GridMoveStep> path)
    {
        if (path == null || path.Count == 0)
        {
            return false;
        }

        bool first = true;
        Vector2Int expectedFrom = default;
        foreach (GridMoveStep step in path)
        {
            if (!step.IsValid || step.MoveType != GridMoveType.Walk)
            {
                return false;
            }

            if (!first && step.From != expectedFrom)
            {
                return false;
            }

            int distance = Mathf.Abs(step.From.x - step.To.x) + Mathf.Abs(step.From.y - step.To.y);
            if (distance != 1)
            {
                return false;
            }

            expectedFrom = step.To;
            first = false;
        }

        return true;
    }

    private sealed class FixedScoreConsideration : Consideration
    {
        public float FixedScore { get; set; }

        public override float ScoreConsideration(CharacterActor actor)
        {
            return FixedScore;
        }
    }

    private sealed class TestActionSet : AIActionSet
    {
        public bool RequireDestination { get; set; }
        public bool ResolveDestination { get; set; }
        public BuildableObject ResolvedDestination { get; set; }
        public FixedScoreConsideration OwnedConsideration { get; set; }
        public int CanStartRequestCount { get; private set; }
        public int ExecuteCount { get; private set; }
        public int StopCount { get; private set; }
        public bool ThrowOnExecute { get; set; }

        public override bool RequiresDestination => RequireDestination;

        public override bool CanStart(CharacterActor actor)
        {
            CanStartRequestCount++;
            return actor != null;
        }

        public override void Execute(CharacterActor actor)
        {
            ExecuteCount++;
            if (ThrowOnExecute)
            {
                throw new InvalidOperationException("forced execute failure");
            }
        }

        public override void OnStop(
            CharacterActor actor,
            AIAction runningAction,
            string reason)
        {
            StopCount++;
        }

        public override bool TryResolveDestinationWithFailure(
            CharacterActor actor,
            GridPathSearchResult searchResult,
            out BuildableObject destination,
            out AIActionFailure failure)
        {
            destination = null;
            failure = AIActionFailure.None;
            if (!RequireDestination)
            {
                return true;
            }

            if (!ResolveDestination)
            {
                failure = AIActionFailure.Create(
                    AIActionFailureKind.DestinationSelectionFailed,
                    "forced destination failure");
                return false;
            }

            if (ResolvedDestination != null)
            {
                destination = ResolvedDestination;
                return true;
            }

            failure = AIActionFailure.Create(
                AIActionFailureKind.NoDestination,
                "test destination not configured");
            return false;
        }

        private void OnDestroy()
        {
            if (OwnedConsideration != null)
            {
                Object.DestroyImmediate(OwnedConsideration);
            }
        }
    }

    private sealed class PriorityScenarioWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CharacterAiSchedulerInstanceField =
            typeof(CharacterAiScheduler).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly CharacterAiScheduler previousScheduler;
        private readonly List<GameObject> objects = new List<GameObject>();
        private int nextCharacterId = 1;

        public PriorityScenarioWorld(int width = 16)
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;
            previousScheduler = CharacterAiSchedulerInstanceField?.GetValue(null) as CharacterAiScheduler;
            CharacterAiSchedulerInstanceField?.SetValue(null, null);
            Grid = new Grid(width, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                Grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            GameObject gridSystemObject = new GameObject("Character AI Priority Corner GridSystemManager");
            objects.Add(gridSystemObject);
            GridSystemManager manager = gridSystemObject.AddComponent<GridSystemManager>();
            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);
        }

        public Grid Grid { get; }

        public BuildableObject Place(string assetName, Vector2Int position)
        {
            BuildingSO buildingData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                $"Assets/Resources/SO/Building/P1/{assetName}.asset");
            if (buildingData == null)
            {
                throw new InvalidOperationException($"{assetName} asset not found.");
            }

            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            if (building == null)
            {
                throw new InvalidOperationException($"{assetName} could not be created.");
            }

            objects.Add(building.gameObject);
            CharacterAiEditorTestDependencies.InjectWithRoomPolicy(
                building,
                PermissivePriorityRoomPolicy.Instance);
            building.SetGrid(Grid);
            building.Initialization(buildingData, position);
            bool registered = Grid.RegisterOccupant(
                building,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            if (!registered)
            {
                throw new InvalidOperationException($"{assetName} could not be registered.");
            }

            if (building is Shop shop)
            {
                CharacterAiEditorTestDependencies.InjectShop(shop);
            }

            return building;
        }

        public CharacterActor CreateOwner(string ownerAssetName, Vector2Int position)
        {
            CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                $"Assets/Resources/SO/Character/Owners/{ownerAssetName}.asset");
            if (data == null)
            {
                throw new InvalidOperationException($"{ownerAssetName} asset not found.");
            }

            GameObject obj = new GameObject(ownerAssetName);
            objects.Add(obj);
            obj.SetActive(false);
            obj.transform.position = Grid.GetWorldPos(position);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<CharacterActor>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityShopping>();
            obj.AddComponent<AbilityWork>();
            obj.AddComponent<AIBrain>();
            CharacterAiEditorTestDependencies.Inject(obj);
            obj.SetActive(true);

            CharacterActor character = obj.GetComponent<CharacterActor>();
            character.EnsureRuntimeState();
            character.RefreshAbilityCache();
            character.Initialization(data);
            character.Identity.SetPersistentId(
                $"character:priority-corner:{nextCharacterId++:D4}");
            character.SetLifecycleState(CharacterLifecycleState.Active);
            return character;
        }

        public void Dispose()
        {
            GridSystemInstanceField?.SetValue(null, previousGridSystem);
            CharacterAiSchedulerInstanceField?.SetValue(null, previousScheduler);
            FacilityCandidateCache.Clear();

            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private sealed class PermissivePriorityRoomPolicy : IBuildingRoomPolicyPort
    {
        public static readonly PermissivePriorityRoomPolicy Instance = new();

        public bool IsFacilityRoleAvailable(
            IBuildingWorldEntryPort building,
            FacilityRole requestedRole,
            out string rejectReason)
        {
            rejectReason = string.Empty;
            return true;
        }

        public float GetRoomUtilityScore(
            IBuildingWorldEntryPort building,
            FacilityRole role) => 1f;

        public int GetEffectiveCapacity(IBuildingWorldEntryPort building) =>
            building is BuildableObject buildable && buildable.Facility != null
                ? Mathf.Max(1, buildable.Facility.capacity)
                : 1;

        public BuildingRoomOperationalSnapshot GetOperationalProfile(
            IBuildingWorldEntryPort building)
        {
            int capacity = GetEffectiveCapacity(building);
            IReadOnlyList<IBuildingWorldEntryPort> parts = building != null
                ? new[] { building }
                : Array.Empty<IBuildingWorldEntryPort>();
            return new BuildingRoomOperationalSnapshot(
                parts,
                hasRoom: true,
                isUsableRoom: true,
                qualityScore: 1f,
                seatCapacity: capacity,
                tableCapacity: capacity,
                serviceCapacity: capacity,
                retailCategory: StockCategory.General,
                storage: new Dictionary<StockCategory, int>());
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }
}
