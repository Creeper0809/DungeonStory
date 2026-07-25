using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StaffDutyDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run P1 Staff Duty Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 staff duty scenarios failed.");
        }
    }

    public static void RunForBatchMode()
    {
        bool success = RunAll(true);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(success ? 0 : 1);
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("Recruited staff cancels customer exit behavior", VerifyRecruitedStaffCancelsCustomerExit, errors);
        RunScenario("Spawner exit handoff preserves recruited staff", VerifySpawnerExitHandoffPreservesRecruitedStaff, errors);
        RunScenario("On-duty injured expedition staff can use recovery rest", VerifyOnDutyExpeditionRecoveryRest, errors);

        RunScenario("吏곸썝 珥덇린 而⑤뵒??蹂댁젙", VerifyWorkerInitialCondition, errors);
        RunScenario("洹쇰Т ?쇰줈濡?鍮꾨쾲 ?꾪솚", VerifyWorkFatigueTriggersOffDuty, errors);
        RunScenario("Critical fatigue enters off-duty instead of stuck rest protection", VerifyCriticalFatigueEntersOffDuty, errors);
        RunScenario("鍮꾨쾲 諛⑸Ц ?ъ씠???쒖옉", VerifyOffDutyVisitCycle, errors);
        RunScenario("吏곸썝 AI 鍮꾨쾲 ?됰룞 蹂닿컯", VerifyStaffBrainAddsOffDutyActions, errors);
        RunScenario("?⑤???吏곸썝 援ш꼍 ?쒖쇅", VerifyOnDutyStaffDoesNotUseLookAround, errors);
        RunScenario("Owner keeps self-care but excludes discretionary visitor actions", VerifyOwnerSelfCareActionPolicy, errors);
        RunScenario("鍮꾨쾲 吏곸썝 留ㅼ텧 ?쒖쇅", VerifyOffDutyStaffDoesNotCreateRevenue, errors);
        RunScenario("湲닿툒 ?묒뾽 鍮꾨쾲 以묐떒", VerifyEmergencyCanInterruptOffDuty, errors);

        RunScenario("Hunger interrupts work without forcing off-duty", VerifyHungerInterruptsWithoutForcingOffDuty, errors);
        RunScenario("鍮꾨쾲 ?湲곕뒗 ?덇린瑜??뚮났?섏? ?딆쓬", VerifyWaitDoesNotRecoverHunger, errors);
        RunScenario("?⑤????湲?吏곸썝 ?섏쟾 諛고쉶 寃쎈줈", VerifyOnDutyWaitCanWander, errors);
        RunScenario("On-duty wait selects dungeon wander", VerifyOnDutyWaitSelectsDungeonWander, errors);
        RunScenario("Idle wander can use valid stairs", VerifyIdleWanderCanUseValidStairs, errors);
        RunScenario("Off-duty wait can wander", VerifyOffDutyWaitCanWander, errors);
        RunScenario("Occupied work wait uses dungeon wander", VerifyOccupiedWorkWaitUsesDungeonWander, errors);
        RunScenario("Customer ???吏곸썝??吏곸썝 AI 洹쒖튃 ?곸슜", VerifyCustomerTypedWorkerUsesStaffRules, errors);
        RunScenario("Expedition return wakes staff work decision", VerifyExpeditionReturnWakesWorkDecision, errors);

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
            Debug.Log("P1 staff duty scenarios passed.");
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

    private static bool VerifyWorkerInitialCondition()
    {
        CharacterActor staff = CreateStaff("Staff Initial Condition", withShopping: true, withWorkAction: true);
        AbilityWork work = staff.GetAbility<AbilityWork>();
        bool canStartWork = work.CanStartWorkAction();

        bool valid = staff.stats[CharacterCondition.SLEEP] >= 80f
            && staff.stats[CharacterCondition.MOOD] >= 70f
            && !work.IsOffDuty
            && canStartWork;

        if (!valid)
        {
            Debug.LogError(
                $"Worker initial condition detail: " +
                $"sleep={staff.stats[CharacterCondition.SLEEP]}, " +
                $"mood={staff.stats[CharacterCondition.MOOD]}, " +
                $"offDuty={work.IsOffDuty}, " +
                $"restProtection={work.ShouldUseRestProtection()}, " +
                $"canStartWork={canStartWork}, " +
                $"actions={(staff.ai != null && staff.ai.availableActions != null ? staff.ai.availableActions.Length : -1)}");
        }

        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifyWorkFatigueTriggersOffDuty()
    {
        CharacterActor staff = CreateStaff("Staff Fatigue", withShopping: true, withWorkAction: true);
        AbilityWork work = staff.GetAbility<AbilityWork>();
        staff.stats[CharacterCondition.SLEEP] = 26f;
        staff.stats[CharacterCondition.MOOD] = 26f;

        work.ApplyWorkFatigueTick();
        bool canWork = work.CanStartWorkAction();
        bool valid = !canWork
            && work.IsOffDuty
            && staff.stats[CharacterCondition.SLEEP] < 26f
            && staff.stats[CharacterCondition.MOOD] < 26f;

        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifyCriticalFatigueEntersOffDuty()
    {
        CharacterActor staff = CreateStaff("Staff Critical Fatigue", withShopping: true, withWorkAction: true);
        AbilityWork work = staff.GetAbility<AbilityWork>();
        staff.stats[CharacterCondition.SLEEP] = 0.5f;
        staff.stats[CharacterCondition.MOOD] = 100f;

        bool canWork = work.CanStartWorkAction();
        bool valid = !canWork
            && work.IsOffDuty
            && staff.ai.isBestActionEnd;

        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifyOffDutyVisitCycle()
    {
        CharacterActor staff = CreateStaff("Staff Off Duty Visit", withShopping: true, withWorkAction: true);
        AbilityWork work = staff.GetAbility<AbilityWork>();
        AbilityShopping shopping = staff.GetAbility<AbilityShopping>();
        BuildableObject visited = CreateDummyBuilding();

        shopping.RegisterVisit(visited);
        work.BeginOffDuty("test");
        bool valid = work.IsOffDuty
            && shopping.visitCount >= 1
            && shopping.lookAroundCount >= 1
            && shopping.visitedBuilding.Count == 0;

        Object.DestroyImmediate(visited.gameObject);
        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifyStaffBrainAddsOffDutyActions()
    {
        CharacterActor staff = CreateStaff("Staff Brain Actions", withShopping: true, withWorkAction: true);
        Type[] actionTypes = staff.ai.availableActions
            .Where((action) => action != null && action.actionset != null)
            .Select((action) => action.actionset.GetType())
            .ToArray();

        bool valid = actionTypes.Contains(typeof(AIWork))
            && actionTypes.Contains(typeof(AIWait))
            && actionTypes.Contains(typeof(AIEat))
            && actionTypes.Contains(typeof(AIRest))
            && actionTypes.Contains(typeof(AIShopping))
            && !actionTypes.Contains(typeof(AIExitDungeon));

        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifyRecruitedStaffCancelsCustomerExit()
    {
        CharacterActor staff = CreateStaff(
            "Recruited Staff Exit Guard",
            withShopping: true,
            withWorkAction: true);
        AbilityShopping shopping = staff.GetAbility<AbilityShopping>();
        AIExitDungeon exitAction = ScriptableObject.CreateInstance<AIExitDungeon>();

        staff.ai.availableActions = AiDebugScenarioActionFactory.CreateCustomerActions();
        staff.SetLifecycleState(CharacterLifecycleState.ExitingDungeon);
        shopping.RestorePersistentState(0, 3, 0);
        staff.ai.UseStaffWorkActions();
        staff.SetLifecycleState(CharacterLifecycleState.Active);
        staff.GetAbility<AbilityMove>()?.StartExitDungeon();

        CharacterAiBranch[] branches = staff.ai.availableActions
            .Where(action => action?.actionset != null)
            .Select(action => action.actionset.Branch)
            .ToArray();
        bool valid = staff.CurrentLifecycleState == CharacterLifecycleState.Active
            && branches.Contains(CharacterAiBranch.Work)
            && branches.Contains(CharacterAiBranch.Wait)
            && !branches.Contains(CharacterAiBranch.ExitDungeon)
            && shopping.ShouldExitDungeon()
            && !exitAction.CanStart(staff);

        Object.DestroyImmediate(exitAction);
        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifySpawnerExitHandoffPreservesRecruitedStaff()
    {
        CharacterActor staff = CreateStaff(
            "Recruited Staff Spawner Guard",
            withShopping: true,
            withWorkAction: true);
        GameObject spawnerObject = new GameObject("Spawner Exit Guard");
        CharacterSpawner spawner = spawnerObject.AddComponent<CharacterSpawner>();

        staff.SetLifecycleState(CharacterLifecycleState.Despawned);
        staff.gameObject.SetActive(true);

        System.Collections.IEnumerator routine = spawner.Interact(staff);
        while (routine.MoveNext())
        {
        }

        bool valid = staff.gameObject.activeSelf
            && staff.CurrentLifecycleState == CharacterLifecycleState.Active
            && staff.Identity.CharacterType == CharacterType.NPC
            && staff.TryGetAbility(out AbilityWork _);

        Object.DestroyImmediate(spawnerObject);
        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifyOnDutyExpeditionRecoveryRest()
    {
        FieldInfo instanceField = typeof(GridSystemManager)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo gridField = typeof(GridSystemManager)
            .GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        object previousGridSystem = instanceField?.GetValue(null);

        GameObject managerObject = null;
        CharacterActor staff = null;
        try
        {
            using WorkScenarioWorld world = new WorkScenarioWorld();
            managerObject = new GameObject("Expedition Recovery Facility GridSystem");
            instanceField?.SetValue(null, null);
            GridSystemManager manager = managerObject.AddComponent<GridSystemManager>();
            gridField?.SetValue(manager, world.Grid);
            instanceField?.SetValue(null, manager);

            BuildableObject restFacility = world.Place("P1_RestRoom", new Vector2Int(2, 0));
            BuildableObject hygieneFacility = world.Place("P1_Washroom", new Vector2Int(8, 0));

            staff = CreateStaff(
                "Staff Expedition Recovery Rest",
                withShopping: true,
                withWorkAction: true);
            staff.transform.position = world.Grid.GetWorldPos(Vector2Int.zero);
            staff.ApplyDamage(staff.MaxHealth * 0.45f, "test expedition injury");
            staff.Lifecycle.RecordExpeditionReturn(70f, alive: true);

            AbilityWork work = staff.GetAbility<AbilityWork>();
            float restNeed = FacilityCandidateScorer.GetNeedScore(staff, FacilityRole.Rest);
            float hygieneNeed = FacilityCandidateScorer.GetNeedScore(staff, FacilityRole.Hygiene);
            bool foundRestAction = staff.ai.TryFindBestScoredAction(
                actionSet => actionSet is AIRest,
                out CharacterAiActionCandidate restActionCandidate);
            bool foundHygieneAction = staff.ai.TryFindBestScoredAction(
                actionSet => actionSet is AIFacilityRoleAction roleAction
                    && roleAction.Role == FacilityRole.Hygiene,
                out CharacterAiActionCandidate hygieneActionCandidate);
            bool restJobEvaluated = CharacterAiJobGiverRegistry.Rest.TryEvaluate(
                staff,
                out CharacterAiJobCandidate restJobCandidate);
            bool hygieneJobEvaluated = CharacterAiJobGiverRegistry.Hygiene.TryEvaluate(
                staff,
                out CharacterAiJobCandidate hygieneJobCandidate);
            GridPathSearchResult search = staff.ai.GetPathSearch(staff) ?? world.Grid.SearchPath(Vector2Int.zero);
            bool restReachable = search != null && search.ContainsVisitableOccupant(restFacility);
            bool hygieneReachable = search != null && search.ContainsVisitableOccupant(hygieneFacility);
            bool restCanVisit = restFacility.CanVisit(staff, out string restVisitReason);
            bool hygieneCanVisit = hygieneFacility.CanVisit(staff, out string hygieneVisitReason);
            bool restCandidate = FacilityCandidateScorer.IsCandidate(
                staff,
                restFacility,
                FacilityRole.Rest,
                out string restCandidateReason);
            bool hygieneCandidate = FacilityCandidateScorer.IsCandidate(
                staff,
                hygieneFacility,
                FacilityRole.Hygiene,
                out string hygieneCandidateReason);
            BuildableObject restResolvedDestination = null;
            BuildableObject hygieneResolvedDestination = null;
            AIActionFailure restResolveFailure = AIActionFailure.None;
            AIActionFailure hygieneResolveFailure = AIActionFailure.None;
            bool restResolved = restActionCandidate.ActionSet != null
                && restActionCandidate.ActionSet.TryResolveDestinationWithFailure(
                    staff,
                    search,
                    out restResolvedDestination,
                    out restResolveFailure);
            bool hygieneResolved = hygieneActionCandidate.ActionSet != null
                && hygieneActionCandidate.ActionSet.TryResolveDestinationWithFailure(
                    staff,
                    search,
                    out hygieneResolvedDestination,
                    out hygieneResolveFailure);
            string visitableNames = search != null
                ? string.Join(",", search.GetAllVisitableBuilding().Select(building => building.name))
                : "no-search";

            bool valid = work != null
                && !work.IsOffDuty
                && restNeed >= 0.65f
                && hygieneNeed >= 0.5f
                && foundRestAction
                && restActionCandidate.Score >= 0.65f
                && restResolved
                && restResolvedDestination == restFacility
                && foundHygieneAction
                && hygieneActionCandidate.Score >= 0.5f
                && hygieneResolved
                && hygieneResolvedDestination == hygieneFacility
                && restJobEvaluated
                && restJobCandidate.ActionCandidate.ActionSet is AIRest
                && hygieneJobEvaluated
                && hygieneJobCandidate.ActionCandidate.ActionSet is AIFacilityRoleAction hygieneAction
                && hygieneAction.Role == FacilityRole.Hygiene;

            if (!valid)
            {
                Debug.LogError(
                    $"Expedition recovery rest detail: " +
                    $"offDuty={work?.IsOffDuty}, restNeed={restNeed:0.###}, hygieneNeed={hygieneNeed:0.###}, " +
                    $"restAction={foundRestAction}:{restActionCandidate.DebugLabel}:{restActionCandidate.Failure}, " +
                    $"restDest={(restActionCandidate.Action?.destination != null ? restActionCandidate.Action.destination.name : "null")}, " +
                    $"hygieneAction={foundHygieneAction}:{hygieneActionCandidate.DebugLabel}:{hygieneActionCandidate.Failure}, " +
                    $"hygieneDest={(hygieneActionCandidate.Action?.destination != null ? hygieneActionCandidate.Action.destination.name : "null")}, " +
                    $"restJob={restJobEvaluated}:{restJobCandidate.DebugSummary}, " +
                    $"hygieneJob={hygieneJobEvaluated}:{hygieneJobCandidate.DebugSummary}, " +
                    $"restResolved={restResolved}:{restResolvedDestination?.name ?? "null"}:{restResolveFailure}, " +
                    $"hygieneResolved={hygieneResolved}:{hygieneResolvedDestination?.name ?? "null"}:{hygieneResolveFailure}, " +
                    $"restReachable={restReachable}, restVisit={restCanVisit}:{restVisitReason}, restCandidate={restCandidate}:{restCandidateReason}, " +
                    $"hygieneReachable={hygieneReachable}, hygieneVisit={hygieneCanVisit}:{hygieneVisitReason}, hygieneCandidate={hygieneCandidate}:{hygieneCandidateReason}, " +
                    $"visitable={visitableNames}");
            }

            return valid;
        }
        finally
        {
            if (staff != null)
            {
                Object.DestroyImmediate(staff.gameObject);
            }
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
            instanceField?.SetValue(null, previousGridSystem);
        }
    }

    private static bool VerifyOnDutyStaffDoesNotUseLookAround()
    {
        CharacterActor staff = CreateStaff("Staff LookAround Guard", withShopping: true, withWorkAction: true);
        CharacterActor customer = CreateCustomer();
        CharacterActor staffActor = CharacterActor.From(staff);
        CharacterActor customerActor = CharacterActor.From(customer);
        ConsiderationCanLookAround consideration = ScriptableObject.CreateInstance<ConsiderationCanLookAround>();

        bool valid = !AILookAround.CanUseVisitLookAround(staffActor)
            && Mathf.Approximately(consideration.ScoreConsideration(staffActor), 0f)
            && AILookAround.CanUseVisitLookAround(customerActor)
            && Mathf.Approximately(consideration.ScoreConsideration(customerActor), 1f);

        Object.DestroyImmediate(consideration);
        Object.DestroyImmediate(staff.gameObject);
        Object.DestroyImmediate(customer.gameObject);
        return valid;
    }

    private static bool VerifyOwnerSelfCareActionPolicy()
    {
        CharacterActor owner = CreateOwner("Owner_Orc");
        Type[] actionTypes = owner.ai.availableActions
            .Where((action) => action != null && action.actionset != null)
            .Select((action) => action.actionset.GetType())
            .ToArray();

        bool valid = actionTypes.Contains(typeof(AIWork))
            && actionTypes.Contains(typeof(AIWait))
            && actionTypes.Contains(typeof(AIEat))
            && actionTypes.Contains(typeof(AIRest))
            && !actionTypes.Contains(typeof(AIShopping))
            && !actionTypes.Contains(typeof(AIExitDungeon));

        Object.DestroyImmediate(owner.gameObject);
        return valid;
    }

    private static bool VerifyOffDutyStaffDoesNotCreateRevenue()
    {
        CharacterActor staff = CreateStaff("Staff No Revenue", withShopping: true, withWorkAction: true);
        CharacterActor customer = CreateCustomer();
        CharacterActor npcVisitor = CreateCustomer();
        npcVisitor.characterType = CharacterType.NPC;

        bool valid = !Shop.CreatesRevenueFor(CharacterActor.From(staff))
            && Shop.CreatesRevenueFor(CharacterActor.From(customer))
            && Shop.CreatesRevenueFor(CharacterActor.From(npcVisitor))
            && Shop.CreatesRevenueFor((CharacterActor)null);

        Object.DestroyImmediate(staff.gameObject);
        Object.DestroyImmediate(customer.gameObject);
        Object.DestroyImmediate(npcVisitor.gameObject);
        return valid;
    }

    private static bool VerifyEmergencyCanInterruptOffDuty()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject damaged = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        damaged.SetDamaged(true);

        CharacterActor staff = CreateStaff("Staff Emergency", withShopping: true, withWorkAction: true);
        AbilityWork work = staff.GetAbility<AbilityWork>();
        staff.stats[CharacterCondition.SLEEP] = 10f;
        staff.stats[CharacterCondition.MOOD] = 10f;
        work.BeginOffDuty("test");

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool prioritySet = work.TrySetPriorityWorkTarget(damaged, BuiltInWorkTypeIds.Repair, search, out _);
        bool assigned = work.TryAssignShop(search);
        bool valid = prioritySet
            && assigned
            && !work.IsOffDuty
            && work.assignedShop == damaged
            && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Repair;
        if (!valid)
        {
            Debug.LogError(
                $"Emergency priority detail: prioritySet={prioritySet}, assigned={assigned}, "
                + $"offDuty={work.IsOffDuty}, canStart={work.CanStartWorkAction()}, "
                + $"assignedShop={work.assignedShop?.name ?? "null"}, assignedType={work.AssignedWorkTypeId}, "
                + $"sleep={staff.stats[CharacterCondition.SLEEP]:0.##}, hunger={staff.stats[CharacterCondition.HUNGER]:0.##}, "
                + $"mood={staff.stats[CharacterCondition.MOOD]:0.##}, "
                + $"priorityTarget={work.PriorityWorkTarget?.name ?? "null"}");
        }

        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifyHungerInterruptsWithoutForcingOffDuty()
    {
        CharacterActor staff = CreateStaff("Staff Hungry Still Works", withShopping: true, withWorkAction: true);
        AbilityWork work = staff.GetAbility<AbilityWork>();

        staff.stats[CharacterCondition.SLEEP] = 100f;
        staff.stats[CharacterCondition.MOOD] = 100f;
        staff.stats[CharacterCondition.HUNGER] = 5f;

        bool interruptsForFood = work.ShouldInterruptCurrentWork(out string interruptReason)
            && interruptReason == "식사 필요";
        bool valid = work.CanStartWorkAction()
            && !work.IsOffDuty
            && !work.ShouldTakeOffDuty()
            && interruptsForFood;

        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifyWaitDoesNotRecoverHunger()
    {
        CharacterActor staff = CreateStaff("Staff Wait Hunger", withShopping: true, withWorkAction: true);
        AbilityWork work = staff.GetAbility<AbilityWork>();
        AIWait wait = ScriptableObject.CreateInstance<AIWait>();

        staff.stats[CharacterCondition.SLEEP] = 20f;
        staff.stats[CharacterCondition.MOOD] = 20f;
        staff.stats[CharacterCondition.FUN] = 20f;
        staff.stats[CharacterCondition.HUNGER] = 10f;
        work.BeginOffDuty("test");

        float beforeHunger = staff.stats[CharacterCondition.HUNGER];
        wait.Execute(CharacterActor.From(staff));
        float afterHunger = staff.stats[CharacterCondition.HUNGER];
        CharacterMoodSnapshot mood = staff.Mood;
        bool recoveredOtherStats = staff.stats[CharacterCondition.SLEEP] > 20f
            && staff.stats[CharacterCondition.FUN] > 20f;
        bool addedRestMoodFactor = mood.Factors.Any(factor =>
            factor.Id == "rest:off-duty" && factor.Value > 0f);

        if (!(Mathf.Approximately(beforeHunger, afterHunger)
            && recoveredOtherStats
            && addedRestMoodFactor))
        {
            Debug.LogError(
                $"Wait recovery detail: hunger={beforeHunger:0.#}->{afterHunger:0.#}, "
                + $"sleep={staff.stats[CharacterCondition.SLEEP]:0.#}, "
                + $"mood={staff.stats[CharacterCondition.MOOD]:0.#}, "
                + $"fun={staff.stats[CharacterCondition.FUN]:0.#}, "
                + $"restFactor={addedRestMoodFactor}, "
                + $"offDuty={work.IsOffDuty}, recovery={work.RestRecoveryOnWait:0.#}");
        }

        Object.DestroyImmediate(wait);
        Object.DestroyImmediate(staff.gameObject);
        return Mathf.Approximately(beforeHunger, afterHunger)
            && recoveredOtherStats
            && addedRestMoodFactor;
    }

    private static bool VerifyOnDutyWaitCanWander()
    {
        FieldInfo instanceField = typeof(GridSystemManager)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo gridField = typeof(GridSystemManager)
            .GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        object previousGridSystem = instanceField?.GetValue(null);

        GameObject managerObject = null;
        GameObject staffObject = null;
        CharacterSO data = null;
        try
        {
            managerObject = new GameObject("Idle Wander GridSystem");
            instanceField?.SetValue(null, null);
            GridSystemManager manager = managerObject.AddComponent<GridSystemManager>();

            Grid grid = new Grid(6, 1);
            for (int x = 0; x < grid.width; x++)
            {
                grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            gridField?.SetValue(manager, grid);
            instanceField?.SetValue(null, manager);

            data = ScriptableObject.CreateInstance<CharacterSO>();
            data.characterType = CharacterType.NPC;
            data.role = CharacterRole.Regular;
            data.characterName = "Staff Idle Wander";
            data.speciesTag = "Orc";

            staffObject = new GameObject("Staff Idle Wander");
            staffObject.AddComponent<SpriteRenderer>();
            staffObject.AddComponent<CharacterActor>();
            staffObject.AddComponent<AbilityMove>();
            staffObject.transform.position = grid.GetWorldPos(Vector2Int.zero);

            CharacterActor staff = InitializeCharacterObject(staffObject, data);
            AbilityMove move = staff.GetAbility<AbilityMove>();
            bool foundPath = move.TryFindIdleWanderPath(2, 8, out Queue<GridMoveStep> path);
            GridMoveStep lastStep = path != null && path.Count > 0 ? path.Last() : null;
            int distance = lastStep != null
                ? Mathf.Abs(lastStep.To.x) + Mathf.Abs(lastStep.To.y)
                : 0;
            return foundPath
                && path != null
                && path.Count > 0
                && IsAdjacentWalkPath(path)
                && lastStep != null
                && distance >= 2;
        }
        finally
        {
            if (staffObject != null)
            {
                Object.DestroyImmediate(staffObject);
            }
            if (data != null)
            {
                Object.DestroyImmediate(data);
            }
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
            instanceField?.SetValue(null, previousGridSystem);
        }
    }

    private static bool VerifyOnDutyWaitSelectsDungeonWander()
    {
        FieldInfo instanceField = typeof(GridSystemManager)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo gridField = typeof(GridSystemManager)
            .GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        object previousGridSystem = instanceField?.GetValue(null);

        GameObject managerObject = null;
        GameObject staffObject = null;
        CharacterSO data = null;
        try
        {
            managerObject = new GameObject("On Duty Wait Selection GridSystem");
            instanceField?.SetValue(null, null);
            GridSystemManager manager = managerObject.AddComponent<GridSystemManager>();

            Grid grid = new Grid(6, 1);
            for (int x = 0; x < grid.width; x++)
            {
                grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            gridField?.SetValue(manager, grid);
            instanceField?.SetValue(null, manager);

            data = ScriptableObject.CreateInstance<CharacterSO>();
            data.characterType = CharacterType.NPC;
            data.role = CharacterRole.Regular;
            data.characterName = "Staff On Duty Wait Selection";
            data.speciesTag = "Orc";
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            staffObject = new GameObject("Staff On Duty Wait Selection");
            staffObject.AddComponent<SpriteRenderer>();
            staffObject.AddComponent<CharacterActor>();
            staffObject.AddComponent<AbilityMove>();
            staffObject.AddComponent<AbilityWork>();
            AIBrain brain = staffObject.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
            staffObject.transform.position = grid.GetWorldPos(Vector2Int.zero);

            CharacterActor staff = InitializeCharacterObject(staffObject, data);
            string selected = IdleBehaviorRunner.GetSelectedBehaviorTypeNameForDebug(CharacterActor.From(staff), true);
            AbilityMove move = staff.GetAbility<AbilityMove>();
            bool foundPath = move.TryFindIdleWanderPath(2, 8, out Queue<GridMoveStep> path);

            return selected == nameof(StaffWanderIdleBehavior)
                && foundPath
                && path != null
                && path.Count > 0
                && IsAdjacentWalkPath(path);
        }
        finally
        {
            if (staffObject != null)
            {
                Object.DestroyImmediate(staffObject);
            }
            if (data != null)
            {
                Object.DestroyImmediate(data);
            }
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
            instanceField?.SetValue(null, previousGridSystem);
        }
    }

    private static bool VerifyIdleWanderCanUseValidStairs()
    {
        FieldInfo instanceField = typeof(GridSystemManager)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo gridField = typeof(GridSystemManager)
            .GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        object previousGridSystem = instanceField?.GetValue(null);

        GameObject managerObject = null;
        GameObject staffObject = null;
        CharacterSO data = null;
        try
        {
            managerObject = new GameObject("Idle Wander Stair Link GridSystem");
            instanceField?.SetValue(null, null);
            GridSystemManager manager = managerObject.AddComponent<GridSystemManager>();

            Grid grid = new Grid(3, 2);
            grid.RegisterOccupant(
                new TestHallwayOccupant(),
                GridLayer.Hallway,
                new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(2, 1) },
                false);

            grid.RegisterOccupant(
                new TestStairOccupant(),
                GridLayer.Building,
                new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(1, 1) },
                true);

            gridField?.SetValue(manager, grid);
            instanceField?.SetValue(null, manager);

            data = ScriptableObject.CreateInstance<CharacterSO>();
            data.characterType = CharacterType.NPC;
            data.role = CharacterRole.Regular;
            data.characterName = "Staff Idle Wander Stair Link";
            data.speciesTag = "Orc";

            staffObject = new GameObject("Staff Idle Wander Stair Link");
            staffObject.AddComponent<SpriteRenderer>();
            staffObject.AddComponent<CharacterActor>();
            staffObject.AddComponent<AbilityMove>();
            staffObject.transform.position = grid.GetWorldPos(Vector2Int.zero);

            CharacterActor staff = InitializeCharacterObject(staffObject, data);
            AbilityMove move = staff.GetAbility<AbilityMove>();
            bool foundPath = move.TryFindIdleWanderPath(2, 8, out Queue<GridMoveStep> path);
            GridMoveStep lastStep = path != null && path.Count > 0 ? path.Last() : null;

            return foundPath
                && path != null
                && path.Count > 0
                && path.Any((step) => step.MoveType == GridMoveType.Stair)
                && path.All((step) => step.MoveType == GridMoveType.Walk || step.MoveType == GridMoveType.Stair)
                && lastStep != null
                && lastStep.To == new Vector2Int(2, 1);
        }
        finally
        {
            if (staffObject != null)
            {
                Object.DestroyImmediate(staffObject);
            }
            if (data != null)
            {
                Object.DestroyImmediate(data);
            }
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
            instanceField?.SetValue(null, previousGridSystem);
        }
    }

    private static bool VerifyOffDutyWaitCanWander()
    {
        FieldInfo instanceField = typeof(GridSystemManager)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo gridField = typeof(GridSystemManager)
            .GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        object previousGridSystem = instanceField?.GetValue(null);

        GameObject managerObject = null;
        GameObject staffObject = null;
        CharacterSO data = null;
        AIWait wait = null;
        try
        {
            managerObject = new GameObject("Off Duty Wait Wander GridSystem");
            instanceField?.SetValue(null, null);
            GridSystemManager manager = managerObject.AddComponent<GridSystemManager>();

            Grid grid = new Grid(6, 1);
            for (int x = 0; x < grid.width; x++)
            {
                grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            gridField?.SetValue(manager, grid);
            instanceField?.SetValue(null, manager);

            data = ScriptableObject.CreateInstance<CharacterSO>();
            data.characterType = CharacterType.NPC;
            data.role = CharacterRole.Regular;
            data.characterName = "Staff Off Duty Wait Wander";
            data.speciesTag = "Orc";
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            staffObject = new GameObject("Staff Off Duty Wait Wander");
            staffObject.AddComponent<SpriteRenderer>();
            staffObject.AddComponent<CharacterActor>();
            staffObject.AddComponent<AbilityMove>();
            staffObject.AddComponent<AbilityWork>();
            AIBrain brain = staffObject.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
            staffObject.transform.position = grid.GetWorldPos(Vector2Int.zero);

            CharacterActor staff = InitializeCharacterObject(staffObject, data);
            AbilityWork work = staff.GetAbility<AbilityWork>();
            AbilityMove move = staff.GetAbility<AbilityMove>();
            wait = ScriptableObject.CreateInstance<AIWait>();
            work.BeginOffDuty("test");

            bool canStartWait = wait.CanStart(CharacterActor.From(staff));
            bool foundPath = move.TryFindIdleWanderPath(2, 8, out Queue<GridMoveStep> path);
            GridMoveStep lastStep = path != null && path.Count > 0 ? path.Last() : null;
            return work.IsOffDuty
                && canStartWait
                && foundPath
                && IsAdjacentWalkPath(path)
                && lastStep != null
                && grid.IsWalkable(lastStep.To);
        }
        finally
        {
            if (wait != null)
            {
                Object.DestroyImmediate(wait);
            }
            if (staffObject != null)
            {
                Object.DestroyImmediate(staffObject);
            }
            if (data != null)
            {
                Object.DestroyImmediate(data);
            }
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
            instanceField?.SetValue(null, previousGridSystem);
        }
    }

    private static bool VerifyOccupiedWorkWaitUsesDungeonWander()
    {
        FieldInfo instanceField = typeof(GridSystemManager)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo gridField = typeof(GridSystemManager)
            .GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo lastFailureField = typeof(AIBrain)
            .GetField("lastActionFailure", BindingFlags.Instance | BindingFlags.NonPublic);
        object previousGridSystem = instanceField?.GetValue(null);

        GameObject managerObject = null;
        GameObject staffObject = null;
        BuildableObject occupiedTarget = null;
        CharacterSO data = null;
        try
        {
            managerObject = new GameObject("Occupied Wait Wander GridSystem");
            instanceField?.SetValue(null, null);
            GridSystemManager manager = managerObject.AddComponent<GridSystemManager>();

            Grid grid = new Grid(8, 1);
            for (int x = 0; x < grid.width; x++)
            {
                grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            gridField?.SetValue(manager, grid);
            instanceField?.SetValue(null, manager);

            data = ScriptableObject.CreateInstance<CharacterSO>();
            data.characterType = CharacterType.NPC;
            data.role = CharacterRole.Regular;
            data.characterName = "Staff Occupied Wait Wander";
            data.speciesTag = "Orc";
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            staffObject = new GameObject("Staff Occupied Wait Wander");
            staffObject.AddComponent<SpriteRenderer>();
            staffObject.AddComponent<CharacterActor>();
            staffObject.AddComponent<AbilityMove>();
            staffObject.AddComponent<AbilityWork>();
            AIBrain brain = staffObject.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
            staffObject.transform.position = grid.GetWorldPos(Vector2Int.zero);

            CharacterActor staff = InitializeCharacterObject(staffObject, data);
            occupiedTarget = CreateDummyBuilding();
            lastFailureField?.SetValue(
                staff.ai,
                AIActionFailure.Create(
                    AIActionFailureKind.DestinationOccupied,
                    "test occupied",
                    occupiedTarget));

            string selected = IdleBehaviorRunner.GetSelectedBehaviorTypeNameForDebug(CharacterActor.From(staff), true);
            return selected == nameof(StaffWanderIdleBehavior);
        }
        finally
        {
            if (occupiedTarget != null)
            {
                Object.DestroyImmediate(occupiedTarget.gameObject);
            }
            if (staffObject != null)
            {
                Object.DestroyImmediate(staffObject);
            }
            if (data != null)
            {
                Object.DestroyImmediate(data);
            }
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
            instanceField?.SetValue(null, previousGridSystem);
        }
    }

    private static bool VerifyCustomerTypedWorkerUsesStaffRules()
    {
        CharacterActor staff = CreateStaff(
            "Customer Typed Staff",
            withShopping: true,
            withWorkAction: true,
            characterType: CharacterType.Customer);
        AbilityWork work = staff.GetAbility<AbilityWork>();
        AIWait wait = ScriptableObject.CreateInstance<AIWait>();
        ConsiderationCanLookAround lookAround = ScriptableObject.CreateInstance<ConsiderationCanLookAround>();

        Type[] actionTypes = staff.ai.availableActions
            .Where((action) => action != null && action.actionset != null)
            .Select((action) => action.actionset.GetType())
            .ToArray();

        bool valid = CharacterWorkRoleUtility.IsOnDutyWorker(CharacterActor.From(staff))
            && work.CanStartWorkAction()
            && wait.CanStart(CharacterActor.From(staff))
            && !AILookAround.CanUseVisitLookAround(CharacterActor.From(staff))
            && Mathf.Approximately(lookAround.ScoreConsideration(CharacterActor.From(staff)), 0f)
            && actionTypes.Contains(typeof(AIWork))
            && actionTypes.Contains(typeof(AIWait))
            && actionTypes.Contains(typeof(AIEat))
            && actionTypes.Contains(typeof(AIRest))
            && actionTypes.Contains(typeof(AIShopping));

        Object.DestroyImmediate(lookAround);
        Object.DestroyImmediate(wait);
        Object.DestroyImmediate(staff.gameObject);
        return valid;
    }

    private static bool VerifyExpeditionReturnWakesWorkDecision()
    {
        FieldInfo instanceField = typeof(GridSystemManager)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo gridField = typeof(GridSystemManager)
            .GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo schedulerInstanceField = typeof(CharacterAiScheduler)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previousGridSystem = instanceField?.GetValue(null);
        object previousScheduler = schedulerInstanceField?.GetValue(null);

        GameObject managerObject = null;
        CharacterActor staff = null;
        try
        {
            using WorkScenarioWorld world = new WorkScenarioWorld();
            managerObject = new GameObject("Expedition Return GridSystem");
            instanceField?.SetValue(null, null);
            GridSystemManager manager = managerObject.AddComponent<GridSystemManager>();
            gridField?.SetValue(manager, world.Grid);
            instanceField?.SetValue(null, manager);

            BuildableObject damaged = world.Place("P1_RestRoom", new Vector2Int(2, 0));
            damaged.SetDamaged(true);

            staff = CreateStaff("Staff Expedition Return", withShopping: true, withWorkAction: true);
            staff.transform.position = world.Grid.GetWorldPos(Vector2Int.zero);

            bool leftDungeon = staff.BeginExpedition()
                && staff.IsOnExpedition
                && !staff.CanRunAi
                && !staff.ai.isBestActionEnd;

            staff.EndExpedition(alive: true);

            bool returnedReady = !staff.IsOnExpedition
                && staff.CanRunAi
                && staff.ai.isBestActionEnd;
            schedulerInstanceField?.SetValue(null, null);
            AbilityWork work = staff.GetAbility<AbilityWork>();
            bool canStartWork = work != null && work.CanStartWorkAction();
            bool decided = staff.ai.DecideAction();
            bool selectedWork = decided
                && staff.ai.bestAction != null
                && staff.ai.bestAction.actionset is AIWork
                && staff.ai.bestAction.destination == damaged;

            bool valid = leftDungeon && returnedReady && selectedWork;
            if (!valid)
            {
                Debug.LogError(
                    $"Expedition return detail: left={leftDungeon}, returned={returnedReady}, decided={decided}, " +
                    $"best={staff.ai.bestAction?.actionset?.GetType().Name ?? "null"}, " +
                    $"dest={(staff.ai.bestAction?.destination != null ? staff.ai.bestAction.destination.name : "null")}, " +
                    $"target={damaged.name}, failure={staff.ai.LastActionFailure}, "
                    + $"canStartWork={canStartWork}, offDuty={work?.IsOffDuty}, "
                    + $"sleep={staff.stats[CharacterCondition.SLEEP]:0.##}, hunger={staff.stats[CharacterCondition.HUNGER]:0.##}, "
                    + $"mood={staff.stats[CharacterCondition.MOOD]:0.##}, excretion={staff.stats[CharacterCondition.EXCRETION]:0.##}, "
                    + $"hygiene={staff.stats[CharacterCondition.HYGIENE]:0.##}");
            }

            return valid;
        }
        finally
        {
            if (staff != null)
            {
                Object.DestroyImmediate(staff.gameObject);
            }
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }
            schedulerInstanceField?.SetValue(null, previousScheduler);
            instanceField?.SetValue(null, previousGridSystem);
        }
    }

    private static CharacterActor CreateStaff(
        string name,
        bool withShopping,
        bool withWorkAction,
        CharacterType characterType = CharacterType.NPC)
    {
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.characterType = characterType;
        data.role = CharacterRole.Regular;
        data.characterName = name;
        data.speciesTag = "Orc";
        data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

        GameObject obj = new GameObject(name);
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        if (withShopping)
        {
            obj.AddComponent<AbilityShopping>();
        }
        obj.AddComponent<AbilityWork>();
        AIBrain brain = obj.AddComponent<AIBrain>();
        if (withWorkAction)
        {
            brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
        }

        CharacterActor character = InitializeCharacterObject(obj, data);

        return character;
    }

    private static CharacterActor CreateOwner(string ownerAssetName)
    {
        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            $"Assets/Resources/SO/Character/Owners/{ownerAssetName}.asset");

        GameObject obj = new GameObject(ownerAssetName);
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityShopping>();
        obj.AddComponent<AbilityWork>();
        obj.AddComponent<AIBrain>();

        return InitializeCharacterObject(obj, data);
    }

    private static CharacterActor CreateCustomer()
    {
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.characterType = CharacterType.Customer;
        data.role = CharacterRole.Regular;
        data.characterName = "Customer";
        data.speciesTag = "Slime";

        GameObject obj = new GameObject("Customer");
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityShopping>();
        return InitializeCharacterObject(obj, data);
    }

    private static CharacterActor InitializeCharacterObject(GameObject obj, CharacterSO data)
    {
        CharacterAiEditorTestDependencies.Inject(obj);
        CharacterActor character = obj.GetComponent<CharacterActor>();
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(character, null);
        character.RefreshAbilityCache();
        character.Initialization(data);
        if (character.Identity != null && character.Identity.CharacterType == CharacterType.NPC)
        {
            string idSource = string.IsNullOrWhiteSpace(data != null ? data.characterName : null)
                ? obj.name
                : data.characterName;
            character.Identity.SetPersistentId($"staff-duty-test:{idSource}");
        }
        character.SetLifecycleState(CharacterLifecycleState.Active);
        return character;
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
            if (step == null || step.MoveType != GridMoveType.Walk)
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

    private static BuildableObject CreateDummyBuilding()
    {
        GameObject obj = new GameObject("Dummy Building");
        return obj.AddComponent<BuildableObject>();
    }

    private sealed class WorkScenarioWorld : IDisposable
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();

        public WorkScenarioWorld()
        {
            Grid = new Grid(14, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                Grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }
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
            objects.Add(building.gameObject);
            CharacterAiEditorTestDependencies.Inject(building);
            if (building is Shop shop)
            {
                CharacterAiEditorTestDependencies.InjectShop(shop);
            }
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

            if (building.BuildingData.RequiresRoomRole())
            {
                PlaceRoomDoorsFor(building);
            }

            return building;
        }

        private void PlaceRoomDoorsFor(BuildableObject building)
        {
            if (building == null || building.buildPoses == null || building.buildPoses.Count == 0)
            {
                return;
            }

            int minX = building.buildPoses.Min(pos => pos.x);
            int maxX = building.buildPoses.Max(pos => pos.x);
            int y = building.centerPos.y;
            PlaceRuntimeDoor(new Vector2Int(minX - 1, y));
            PlaceRuntimeDoor(new Vector2Int(maxX + 1, y));
        }

        private void PlaceRuntimeDoor(Vector2Int position)
        {
            if (!Grid.IsValidGridPos(position))
            {
                return;
            }

            GridCell cell = Grid.GetGridCell(position);
            if (cell == null || cell.HasOccupantInLayer(GridLayer.Building))
            {
                return;
            }

            BuildingSO buildingData = ScriptableObject.CreateInstance<BuildingSO>();
            scriptableObjects.Add(buildingData);
            buildingData.id = 901;
            buildingData.objectName = "Door";
            buildingData.width = 1;
            buildingData.height = 1;
            buildingData.layer = GridLayer.Building;
            buildingData.category = BuildingCategory.None;
            buildingData.type = typeof(Door);
            buildingData.unlocked = true;
            buildingData.Facility = new FacilityData();

            GameObject obj = new GameObject("Room Boundary Door");
            objects.Add(obj);
            BuildableObject door = obj.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(door);
            door.SetGrid(Grid);
            door.Initialization(buildingData, position);
            Grid.RegisterOccupant(
                door,
                GridLayer.Building,
                buildingData.GetGridPosList(position),
                false);
        }

        public void Dispose()
        {
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }

            foreach (ScriptableObject obj in scriptableObjects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class TestStairOccupant : IGridOccupant, IGridMovementOccupant, IGridMovementHandler
    {
        public int GridId => 1;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => true;
        public bool IsGridMovement => true;
        public GridMoveType GridMoveType => GridMoveType.Stair;

        public System.Collections.IEnumerator Traverse(CharacterActor actor, GridMoveStep step)
        {
            yield break;
        }
    }
}
