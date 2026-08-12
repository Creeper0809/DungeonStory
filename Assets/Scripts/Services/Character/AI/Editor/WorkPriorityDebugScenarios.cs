using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class WorkPriorityDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run P1 Work Priority Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 work priority scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("Urgent work overrides rest protection", VerifyUrgentWorkOverridesRestProtection, errors);
        RunScenario("Rest protection has hysteresis", VerifyRestProtectionHysteresis, errors);

        RunScenario("우선순위 기본값", VerifyPriorityDefaults, errors);
        RunScenario("꺼진 작업 제외", VerifyDisabledWorkIsExcluded, errors);
        RunScenario("종족/능력에 따른 작업 차이", VerifySpeciesWorkPreferenceChangesTarget, errors);
        RunScenario("파손 시설 긴급도", VerifyDamagedFacilityUrgency, errors);
        RunScenario("Undamaged repair is excluded", VerifyUndamagedRepairIsExcluded, errors);
        RunScenario("창고 재고 없으면 보충 작업 제외", VerifyRestockWithoutWarehouseSupplyIsExcluded, errors);
        RunScenario("피로 보호", VerifyFatigueProtection, errors);
        RunScenario("우선순위 UI 프리팹", VerifyPriorityPanelPrefab, errors);
        RunScenario("림월드식 우선순위 UI 매트릭스", VerifyPriorityPanelBuildsMatrix, errors);

        RunScenario("작업 지속은 AI bestAction 흔들림에 즉시 종료되지 않음", VerifyWorkContinuationIgnoresTransientBestActionLoss, errors);
        RunScenario("파손으로 중단된 운영은 완료로 집계하지 않음", VerifyInterruptedOperationIsNotCompleted, errors);
        RunScenario("진행 중 작업 우선순위 끄면 즉시 중단", VerifyDisablingCurrentWorkPriorityStopsWork, errors);
        RunScenario("상향한 최선 작업은 진행 중 작업을 재계획", VerifyRaisedBestPriorityReplansCurrentWork, errors);
        RunScenario("다른 작업 우선순위 변경은 진행 중 작업 유지", VerifyUnrelatedPriorityChangeKeepsCurrentWork, errors);

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
            Debug.Log("P1 work priority scenarios passed.");
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

    private static bool VerifyPriorityDefaults()
    {
        WorkPriorityProfile priorities = WorkPriorityProfile.CreateDefault();
        return WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Construct, out _)
            && priorities.GetPriority(BuiltInWorkTypeIds.Construct) == WorkPriorityLevel.Priority2
            && priorities.GetPriority(BuiltInWorkTypeIds.Operate) == WorkPriorityLevel.Priority1
            && priorities.GetPriority(BuiltInWorkTypeIds.Restock) == WorkPriorityLevel.Priority2
            && priorities.GetPriority(BuiltInWorkTypeIds.Research) == WorkPriorityLevel.Priority2
            && priorities.GetPriority(BuiltInWorkTypeIds.Craft) == WorkPriorityLevel.Priority2
            && priorities.GetPriority(BuiltInWorkTypeIds.Reception) == WorkPriorityLevel.Priority2
            && priorities.GetPriority(BuiltInWorkTypeIds.Rest) == WorkPriorityLevel.Priority3
            && WorkPriorityLevel.Priority1.Next() == WorkPriorityLevel.Priority2
            && WorkPriorityLevel.Priority3.Next() == WorkPriorityLevel.Off
            && WorkPriorityLevel.Off.Next() == WorkPriorityLevel.Priority1;
    }

    private static bool VerifyDisabledWorkIsExcluded()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(2, 0));
        CharacterActor character = CreateCharacter("Owner_Vampire");
        AbilityWork work = character.GetAbility<AbilityWork>();

        work.SetWorkPriority(BuiltInWorkTypeIds.Operate, WorkPriorityLevel.Off);
        work.SetWorkPriority(BuiltInWorkTypeIds.Repair, WorkPriorityLevel.Off);
        work.SetWorkPriority(BuiltInWorkTypeIds.Research, WorkPriorityLevel.Off);

        bool valid = !work.TryAssignShop(world.Grid.SearchPath(Vector2Int.zero))
            && work.assignedShop == null
            && lab != null;

        Object.DestroyImmediate(character.gameObject);
        return valid;
    }

    private static bool VerifySpeciesWorkPreferenceChangesTarget()
    {
        DestroyScenarioResearchRuntimes();

        GameObject runtimeObject = new GameObject("Work Priority Research Runtime");
        WorkScenarioWorld world = null;
        CharacterActor orc = null;
        CharacterActor vampire = null;

        try
        {
            BlueprintResearchRuntime researchRuntime =
                runtimeObject.AddComponent<BlueprintResearchRuntime>();
            CharacterAiEditorTestDependencies.Inject(researchRuntime);
            researchRuntime.EnqueueBlueprint(AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>(
                "Assets/Resources/SO/Blueprint/P1/BP_SupportBasics.asset"));
            world = new WorkScenarioWorld(researchRuntime);
            BuildableObject training = world.Place(
                "P1_TrainingRoom",
                new Vector2Int(2, 0));
            BuildableObject lab = world.Place(
                "P1_ResearchLab",
                new Vector2Int(6, 0));
            GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);

            orc = CreateCharacter("Owner_Orc", researchRuntime);
            AbilityWork orcWork = orc.GetAbility<AbilityWork>();
            SetOnly(orcWork, BuiltInWorkTypeIds.Guard, BuiltInWorkTypeIds.Research);
            bool orcAssigned = orcWork.TryAssignShop(search);
            BuildableObject orcTarget = orcWork.assignedShop;

            Object.DestroyImmediate(orc.gameObject);
            orc = null;

            vampire = CreateCharacter("Owner_Vampire", researchRuntime);
            AbilityWork vampireWork = vampire.GetAbility<AbilityWork>();
            SetOnly(vampireWork, BuiltInWorkTypeIds.Guard, BuiltInWorkTypeIds.Research);
            bool vampireAssigned = vampireWork.TryAssignShop(search);
            BuildableObject vampireTarget = vampireWork.assignedShop;
            bool valid = orcAssigned
                && vampireAssigned
                && orcTarget == training
                && vampireTarget == lab;

            if (!valid)
            {
                Debug.LogError(
                    $"Species work preference detail: " +
                    $"researchActive={researchRuntime.HasActiveResearch}, " +
                    $"training={(training != null ? training.name : "null")}, " +
                    $"lab={(lab != null ? lab.name : "null")}, " +
                    $"trainingGuard={training != null && training.SupportsWork(BuiltInWorkTypeIds.Guard)}, " +
                    $"labResearch={lab != null && lab.SupportsWork(BuiltInWorkTypeIds.Research)}, " +
                    $"labHasResearchWork={lab != null && lab.SupportsWork(BuiltInWorkTypeIds.Research) && researchRuntime.HasActiveResearch}, " +
                    $"orcAssigned={orcAssigned}, " +
                    $"vampireAssigned={vampireAssigned}, " +
                    $"orcTarget={(orcTarget != null ? orcTarget.name : "null")}, " +
                    $"vampireTarget={(vampireTarget != null ? vampireTarget.name : "null")}");
            }

            return valid;
        }
        finally
        {
            if (orc != null)
            {
                Object.DestroyImmediate(orc.gameObject);
            }
            if (vampire != null)
            {
                Object.DestroyImmediate(vampire.gameObject);
            }
            world?.Dispose();
            Object.DestroyImmediate(runtimeObject);
            DestroyScenarioResearchRuntimes();
        }
    }

    private static bool VerifyDamagedFacilityUrgency()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject normal = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        BuildableObject damaged = world.Place("P1_RestRoom", new Vector2Int(6, 0));
        damaged.SetDamaged(true);

        CharacterActor slime = CreateCharacter("Owner_Slime");
        AbilityWork work = slime.GetAbility<AbilityWork>();
        SetOnly(work, BuiltInWorkTypeIds.Repair);

        bool valid = work.TryAssignShop(world.Grid.SearchPath(Vector2Int.zero))
            && work.assignedShop == damaged
            && normal != null;

        Object.DestroyImmediate(slime.gameObject);
        return valid;
    }

    private static bool VerifyUndamagedRepairIsExcluded()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject normal = world.Place("P1_RestRoom", new Vector2Int(2, 0));

        CharacterActor slime = CreateCharacter("Owner_Slime");
        AbilityWork work = slime.GetAbility<AbilityWork>();
        SetOnly(work, BuiltInWorkTypeIds.Repair);

        bool structurallySupportsRepair =
            normal.CanAssignWork(BuiltInWorkTypeIds.Repair, out _);
        bool assigned = work.TryAssignWork(
            BuiltInWorkTypeIds.Repair,
            world.Grid.SearchPath(Vector2Int.zero));
        bool valid = !normal.IsDamaged
            && structurallySupportsRepair
            && !assigned
            && work.assignedShop == null;
        if (!valid)
        {
            Debug.Log(
                $"Undamaged repair diagnostics: damaged={normal.IsDamaged}; "
                + $"structurallySupportsRepair={structurallySupportsRepair}; "
                + $"assigned={assigned}; "
                + $"target={(work.assignedShop != null ? work.assignedShop.name : "none")}; "
                + $"registeredBuildings={CharacterAiEditorTestDependencies.WorldRegistry.Buildings.Count}");
        }

        Object.DestroyImmediate(slime.gameObject);
        return valid;
    }

    private static bool VerifyFatigueProtection()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        world.Place("P1_ResearchLab", new Vector2Int(2, 0));
        CharacterActor vampire = CreateCharacter("Owner_Vampire");
        vampire.stats[CharacterCondition.SLEEP] = 0f;
        AbilityWork work = vampire.GetAbility<AbilityWork>();

        bool valid = work.ShouldUseRestProtection()
            && !work.TryAssignShop(world.Grid.SearchPath(Vector2Int.zero));

        Object.DestroyImmediate(vampire.gameObject);
        return valid;
    }

    private static bool VerifyRestProtectionHysteresis()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        world.Place("P1_ResearchLab", new Vector2Int(2, 0));
        CharacterActor vampire = CreateCharacter("Owner_Vampire");
        AbilityWork work = vampire.GetAbility<AbilityWork>();

        vampire.stats[CharacterCondition.SLEEP] = 0f;
        bool enteredProtection = work.ShouldUseRestProtection();
        vampire.stats[CharacterCondition.SLEEP] = 20f;
        bool stillProtected = work.ShouldUseRestProtection()
            && !work.TryAssignShop(world.Grid.SearchPath(Vector2Int.zero));

        Object.DestroyImmediate(vampire.gameObject);
        return enteredProtection
            && stillProtected
            && work.assignedShop == null;
    }

    private static bool VerifyRestockWithoutWarehouseSupplyIsExcluded()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject shopBuilding = world.Place("P1_LowFoodShop", new Vector2Int(2, 0));
        BuildableObject warehouseBuilding = world.Place("P1_Warehouse", new Vector2Int(8, 0));
        Shop shop = shopBuilding as Shop;
        IWarehouseFacility warehouse = warehouseBuilding as IWarehouseFacility;
        CharacterActor slime = CreateCharacter("Owner_Slime");
        AbilityWork work = slime.GetAbility<AbilityWork>();
        SetOnly(work, BuiltInWorkTypeIds.Restock);

        try
        {
            ClearShopStock(shopBuilding);
            DrainWarehouse(warehouse);

            GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
            string failureReason = string.Empty;
            bool hasSupply = shop != null && shop.HasRestockSupply(new[] { warehouse }, out failureReason);
            bool assigned = work.TryAssignWork(BuiltInWorkTypeIds.Restock, search);

            bool valid = shop != null
                && shop.NeedsRestock
                && !hasSupply
                && !string.IsNullOrWhiteSpace(failureReason)
                && !assigned
                && work.assignedShop == null;
            if (!valid)
            {
                Debug.LogError(
                    $"Restock exclusion detail: shop={(shop != null)}, " +
                    $"needs={(shop != null && shop.NeedsRestock)}, " +
                    $"hasSupply={hasSupply}, reason={failureReason}, " +
                    $"assigned={assigned}, assignedShop={(work.assignedShop != null ? work.assignedShop.name : "null")}");
            }

            return valid;
        }
        finally
        {
            Object.DestroyImmediate(slime.gameObject);
        }
    }

    private static bool VerifyUrgentWorkOverridesRestProtection()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject damaged = world.Place("P1_RestRoom", new Vector2Int(2, 0));
        damaged.SetDamaged(true);

        CharacterActor slime = CreateCharacter("Owner_Slime");
        slime.stats[CharacterCondition.SLEEP] = 0f;
        AbilityWork work = slime.GetAbility<AbilityWork>();
        SetOnly(work, BuiltInWorkTypeIds.Repair, BuiltInWorkTypeIds.Rest);

        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);
        bool restProtectionBlocksNormalStart = work.ShouldUseRestProtection()
            && !work.CanStartWorkAction();
        bool urgentStartAllowed = work.CanStartWorkAction(BuiltInWorkTypeIds.Repair, search);
        bool assigned = work.TryAssignWork(BuiltInWorkTypeIds.Repair, search)
            && work.assignedShop == damaged;
        bool valid = restProtectionBlocksNormalStart
            && urgentStartAllowed
            && assigned;

        Object.DestroyImmediate(slime.gameObject);
        return valid;
    }

    private static bool VerifyPriorityPanelPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/StaffWorkPriorityPanel.prefab");
        return prefab != null
            && prefab.GetComponent<StaffWorkPriorityPanel>() != null;
    }

    private static bool VerifyPriorityPanelBuildsMatrix()
    {
        GameObject panelObject = null;
        CharacterActor orc = null;
        CharacterActor worker = null;

        try
        {
            panelObject = new GameObject(
                "Work Priority Matrix Test",
                typeof(RectTransform));
            StaffWorkPriorityPanel panel =
                panelObject.AddComponent<StaffWorkPriorityPanel>();
            StaffWorkforceSceneAdapter workforceScene = new(
                CharacterAiEditorTestDependencies.WorldRegistry);
            panel.ConstructStaffWorkPriorityPanel(
                new StaffWorkPriorityPanelModelBuilder(
                    new StaffWorkforceRuntimeQueryServiceAdapter(
                        workforceScene,
                        new DungeonStory.Work.StaffWorkforceRuntimeQueryService(
                            workforceScene))),
                new StaffWorkPriorityPanelUiFactory(
                    TMPKoreanFontEditorResolver.CreateService()),
                new DungeonStory.Foundation.UnityUiClock());
            panel.ConstructStaffWorkPriorityArcane(
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<ICharacterManaQuery>(),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IArcaneOverchargeCommand>(),
                OffenseEditorTestDependencies.CreateCombatEquipmentRuntime(),
                CharacterAiEditorTestDependencies.NeutralPerformance);
            orc = CreateCharacter("Owner_Orc");
            worker = CreateRegularWorker(
                "Assets/Resources/SO/Character/Customer_Vampire.asset");
            AbilityWork orcWork = orc.GetAbility<AbilityWork>();
            AbilityWork workerWork = worker.GetAbility<AbilityWork>();

            panel.Refresh();
            Button[] buttons = panelObject.GetComponentsInChildren<Button>(true);
            int buttonCount = buttons.Length;
            string[] buttonNames = buttons
                .Where((button) => button != null)
                .Select((button) => button.gameObject.name)
                .Take(12)
                .ToArray();
            Dictionary<AbilityWork, WorkPriorityLevel> beforeByWorker = new[]
                {
                    orcWork,
                    workerWork
                }
                .Where((work) => work != null)
                .Distinct()
                .ToDictionary(
                    (work) => work,
                    (work) => work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Operate));

            Button operateButton = buttons.FirstOrDefault((button) =>
                button != null
                && button.gameObject.name.Contains($"_{FacilityWorkType.Operate}"));
            bool hadOperateButton = operateButton != null;
            string operateButtonName = hadOperateButton ? operateButton.gameObject.name : "null";
            if (operateButton != null)
            {
                operateButton.onClick.Invoke();
            }

            bool anyOperatePriorityChanged = beforeByWorker.Any((pair) =>
                pair.Key.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Operate) == pair.Value.Next());
            int requiredCells = WorkTaskCatalog.Definitions.Count * 2;
            bool valid = panel.VisibleWorkerCount >= 2
                && panel.VisibleCellCount >= requiredCells
                && buttonCount >= requiredCells
                && hadOperateButton
                && orcWork != null
                && anyOperatePriorityChanged
                && worker != null;

            if (!valid)
            {
                Debug.LogError(
                    $"Priority panel matrix detail: " +
                    $"visibleWorkers={panel.VisibleWorkerCount}, " +
                    $"visibleCells={panel.VisibleCellCount}, " +
                    $"requiredCells={requiredCells}, " +
                    $"buttons={buttonCount}, " +
                    $"operateButton={operateButtonName}, " +
                    $"operateChanged={anyOperatePriorityChanged}, " +
                    $"buttonNames={string.Join(", ", buttonNames)}");
            }

            return valid;
        }
        finally
        {
            if (orc != null)
            {
                Object.DestroyImmediate(orc.gameObject);
            }
            if (worker != null)
            {
                Object.DestroyImmediate(worker.gameObject);
            }
            if (panelObject != null)
            {
                Object.DestroyImmediate(panelObject);
            }
        }
    }

    private static void DestroyScenarioResearchRuntimes()
    {
        foreach (BlueprintResearchRuntime runtime in Object.FindObjectsByType<BlueprintResearchRuntime>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (runtime != null && runtime.name == "Work Priority Research Runtime")
            {
                Object.DestroyImmediate(runtime.gameObject);
            }
        }
    }

    private static bool VerifyWorkContinuationIgnoresTransientBestActionLoss()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject training = world.Place("P1_TrainingRoom", new Vector2Int(2, 0));
        CharacterActor worker = CreateCharacter("Owner_Slime");
        AbilityWork work = worker.GetAbility<AbilityWork>();

        try
        {
            if (!work.TryAssignWorkTarget(training, BuiltInWorkTypeIds.Operate, world.Grid.SearchPath(worker.GetNowXY())))
            {
                return false;
            }

            work.isWorking = true;
            worker.ai.bestAction = null;

            var routine = work.CheckActionWork();
            bool yielded = routine.MoveNext();
            bool stillWorkingAfterFirstTick = yielded && work.isWorking;

            work.isWorking = false;
            return training != null
                && stillWorkingAfterFirstTick;
        }
        finally
        {
            Object.DestroyImmediate(worker.gameObject);
        }
    }

    private static bool VerifyDisablingCurrentWorkPriorityStopsWork()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject training = world.Place("P1_TrainingRoom", new Vector2Int(2, 0));
        CharacterActor worker = CreateCharacter("Owner_Slime");
        AbilityWork work = worker.GetAbility<AbilityWork>();

        try
        {
            bool assigned = work.TryAssignWorkTarget(
                training,
                BuiltInWorkTypeIds.Operate,
                world.Grid.SearchPath(worker.GetNowXY()));
            work.isWorking = true;
            worker.ai.bestAction = new AIAction(
                AssetDatabase.LoadAssetAtPath<AIWork>("Assets/Resources/SO/AI/Action/Work.asset"),
                AIActionPlan.AtDestination(training));
            worker.ai.isBestActionEnd = false;

            work.SetWorkPriority(BuiltInWorkTypeIds.Operate, WorkPriorityLevel.Off);

            return assigned
                && !work.isWorking
                && work.assignedShop == null
                && !work.AssignedWorkTypeId.IsValid
                && worker.ai.bestAction == null
                && worker.ai.isBestActionEnd;
        }
        finally
        {
            Object.DestroyImmediate(worker.gameObject);
        }
    }

    private static bool VerifyUnrelatedPriorityChangeKeepsCurrentWork()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject training = world.Place("P1_TrainingRoom", new Vector2Int(2, 0));
        CharacterActor worker = CreateCharacter("Owner_Slime");
        AbilityWork work = worker.GetAbility<AbilityWork>();

        try
        {
            bool assigned = work.TryAssignWorkTarget(
                training,
                BuiltInWorkTypeIds.Operate,
                world.Grid.SearchPath(worker.GetNowXY()));
            work.isWorking = true;

            work.SetWorkPriority(BuiltInWorkTypeIds.Research, WorkPriorityLevel.Off);

            bool valid = assigned
                && work.isWorking
                && work.assignedShop == training
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Operate;
            work.isWorking = false;
            return valid;
        }
        finally
        {
            Object.DestroyImmediate(worker.gameObject);
        }
    }

    private static bool VerifyInterruptedOperationIsNotCompleted()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject training = world.Place("P1_TrainingRoom", new Vector2Int(2, 0));
        CharacterActor worker = CreateCharacter("Owner_Slime");
        AbilityWork work = worker.GetAbility<AbilityWork>();

        try
        {
            if (training == null
                || !work.TryAssignWorkTarget(
                    training,
                    BuiltInWorkTypeIds.Operate,
                    world.Grid.SearchPath(worker.GetNowXY())))
            {
                return false;
            }

            work.isWorking = true;
            var routine = work.CheckActionWork();
            bool beganWork = routine.MoveNext() && work.isWorking;

            training.SetDamaged(true);
            bool yieldedAfterDamage = routine.MoveNext();
            PropertyInfo completionProperty = typeof(AbilityWork).GetProperty(
                "LastWorkRunCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            bool markedCompleted = completionProperty != null
                && (bool)completionProperty.GetValue(work);

            return beganWork
                && !yieldedAfterDamage
                && !work.isWorking
                && !markedCompleted;
        }
        finally
        {
            Object.DestroyImmediate(worker.gameObject);
        }
    }

    private static bool VerifyRaisedBestPriorityReplansCurrentWork()
    {
        using WorkScenarioWorld world = new WorkScenarioWorld();
        BuildableObject training = world.Place("P1_TrainingRoom", new Vector2Int(2, 0));
        BuildableObject damaged = world.Place("P1_RestRoom", new Vector2Int(6, 0));
        damaged.SetDamaged(true);
        CharacterActor worker = CreateCharacter("Owner_Slime");
        AbilityWork work = worker.GetAbility<AbilityWork>();
        GridPathSearchResult search = world.Grid.SearchPath(worker.GetNowXY());

        try
        {
            work.WorkPriorities.SetPriority(BuiltInWorkTypeIds.Operate, WorkPriorityLevel.Priority3);
            work.WorkPriorities.SetPriority(BuiltInWorkTypeIds.Repair, WorkPriorityLevel.Priority2);
            bool assigned = work.TryAssignWorkTarget(
                training,
                BuiltInWorkTypeIds.Operate,
                search);
            work.isWorking = true;
            worker.ai.bestAction = new AIAction(
                AssetDatabase.LoadAssetAtPath<AIWork>("Assets/Resources/SO/AI/Action/Work.asset"),
                AIActionPlan.AtDestination(training));
            worker.ai.isBestActionEnd = false;

            work.SetWorkPriority(
                BuiltInWorkTypeIds.Repair,
                WorkPriorityLevel.Priority1,
                search);

            bool valid = assigned
                && damaged.IsDamaged
                && !work.isWorking
                && work.assignedShop == null
                && !work.AssignedWorkTypeId.IsValid
                && worker.ai.bestAction == null
                && worker.ai.isBestActionEnd;
            if (!valid)
            {
                bool repairFound = work.TryGetBestWorkCandidate(
                    BuiltInWorkTypeIds.Repair,
                    search,
                    out WorkTargetCandidate repairCandidate);
                bool overallFound = work.TryGetBestAnyWorkCandidate(
                    search,
                    out WorkTargetCandidate overallCandidate);
                Debug.LogError(
                    $"Raised priority replan detail: assigned={assigned}, " +
                    $"damaged={damaged.IsDamaged}, working={work.isWorking}, " +
                    $"assignedType={work.AssignedWorkTypeId}, " +
                    $"assignedTarget={(work.assignedShop != null ? work.assignedShop.name : "none")}, " +
                    $"repair={repairFound}:{WorkTargetCandidateRuntimeAdapter.ResolveBuilding(repairCandidate)?.name}:{repairCandidate.Score:0.##}, " +
                    $"overall={overallFound}:{overallCandidate.WorkTypeId}:{WorkTargetCandidateRuntimeAdapter.ResolveBuilding(overallCandidate)?.name}:{overallCandidate.Score:0.##}, " +
                    $"bestAction={(worker.ai.bestAction != null ? worker.ai.bestAction.actionset?.name : "none")}, " +
                    $"actionEnded={worker.ai.isBestActionEnd}");
            }

            return valid;
        }
        finally
        {
            Object.DestroyImmediate(worker.gameObject);
        }
    }

    private static void SetOnly(AbilityWork work, params WorkTypeId[] enabledTypes)
    {
        foreach (WorkTypeDefinition definition in WorkTaskCatalog.Definitions)
        {
            WorkTypeId workTypeId = definition.WorkTypeId;
            work.SetWorkPriority(workTypeId, enabledTypes.Contains(workTypeId)
                ? WorkPriorityLevel.Priority1
                : WorkPriorityLevel.Off);
        }
    }

    private static void ClearShopStock(BuildableObject building)
    {
        if (building is Shop shop)
        {
            shop.DebugClearStock();
        }
    }

    private static void DrainWarehouse(IWarehouseFacility warehouse)
    {
        if (warehouse == null || warehouse.Inventory == null)
        {
            return;
        }

        foreach (StockCategoryDefinition definition in
                 ((IStockCategoryDefinitionCatalog)CharacterAiEditorTestDependencies.AuthoredGameplay).All)
        {
            StockCategory category = definition.Category;
            warehouse.Inventory.ConsumePhysicalStockForTest(category, int.MaxValue);
        }
    }

    private static CharacterActor CreateCharacter(
        string ownerAssetName,
        BlueprintResearchRuntime researchRuntime = null)
    {
        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            $"Assets/Resources/SO/Character/Owners/{ownerAssetName}.asset");

        return CreateCharacter(data, ownerAssetName, researchRuntime, regularWorker: false);
    }

    private static CharacterActor CreateRegularWorker(string assetPath)
    {
        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(assetPath);
        return CreateCharacter(data, "Work Priority Regular Worker", null, regularWorker: true);
    }

    private static CharacterActor CreateCharacter(
        CharacterSO data,
        string objectName,
        BlueprintResearchRuntime researchRuntime,
        bool regularWorker)
    {
        GameObject obj = new GameObject(objectName);

        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityWork>();
        AIBrain brain = obj.AddComponent<AIBrain>();
        brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();

        if (researchRuntime != null)
        {
            CharacterAiEditorTestDependencies.Inject(obj, researchRuntime);
        }
        else
        {
            CharacterAiEditorTestDependencies.Inject(obj);
        }
        CharacterActor character = obj.GetComponent<CharacterActor>();
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(character, null);
        character.RefreshAbilityCache();
        character.Initialization(data);
        if (regularWorker)
        {
            character.Identity.SetCharacterType(CharacterType.NPC);
        }
        character.SetLifecycleState(CharacterLifecycleState.Active);
        return character;
    }

    private sealed class WorkScenarioWorld : IDisposable
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly BlueprintResearchRuntime researchRuntime;

        public WorkScenarioWorld(BlueprintResearchRuntime researchRuntime = null)
        {
            this.researchRuntime = researchRuntime;
            Grid = new Grid(12, 1);
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
            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            objects.Add(building.gameObject);
            if (researchRuntime != null)
            {
                CharacterAiEditorTestDependencies.Inject(building, researchRuntime);
            }
            else
            {
                CharacterAiEditorTestDependencies.Inject(building);
            }
            if (building is Shop shop)
            {
                CharacterAiEditorTestDependencies.InjectShop(shop);
            }
            building.SetGrid(Grid);
            building.Initialization(buildingData, position);
            Grid.RegisterOccupant(
                building,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            return building;
        }

        public void Dispose()
        {
            foreach (GameObject obj in objects.Where((obj) => obj != null))
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
}
