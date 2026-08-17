#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class V27BalanceVerticalSlicePlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-vertical-slice-full-loop-playmode.txt";

    public static void RequestRun() =>
        V27BalanceVerticalSlicePlayModeRunner.RequestRun();
}

public sealed class V27BalanceVerticalSlicePlayModeRunner : MonoBehaviour
{
    private const string PendingFlagPath =
        "Temp/v27-balance-vertical-slice-full-loop.flag";
    private const string D03Path =
        "Assets/Resources/SO/Building/Modular/D03_조리손질대.asset";

    private readonly List<string> checks = new();
    private readonly List<string> failures = new();
    private readonly List<string> unexpectedLogs = new();
    private bool started;

    [MenuItem("DungeonStory/V27/Verify Full Vertical Slice PlayMode")]
    public static void RequestRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner();
            return;
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingFlagPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPendingRun()
    {
        if (!File.Exists(PendingFlagPath))
            return;
        File.Delete(PendingFlagPath);
        StartRunner();
    }

    private static void StartRunner()
    {
        if (FindFirstObjectByType<V27BalanceVerticalSlicePlayModeRunner>() != null)
            return;
        new GameObject(nameof(V27BalanceVerticalSlicePlayModeRunner))
            .AddComponent<V27BalanceVerticalSlicePlayModeRunner>();
    }

    private void Start()
    {
        if (started)
            return;
        started = true;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(
            V27BalanceVerticalSlicePlayModeVerifier.ReportPath,
            "RESULT=RUNNING\n");

        Exception exception = null;
        yield return RunGuarded(Execute(), caught => exception = caught);
        if (exception != null)
            failures.Add(exception.ToString());

        WriteReport();
        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private static IEnumerator RunGuarded(
        IEnumerator root,
        Action<Exception> capture)
    {
        Stack<IEnumerator> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            object current;
            try
            {
                IEnumerator routine = stack.Peek();
                if (!routine.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                current = routine.Current;
            }
            catch (Exception exception)
            {
                capture(exception);
                yield break;
            }

            if (current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return current;
        }
    }

    private IEnumerator Execute()
    {
        yield return StartPartyPlayModeTestDriver.CompleteIfVisible(45f);

        DungeonRuntimeLifetimeScope scope = null;
        ICharacterAiWorldRegistry world = null;
        float deadline = Time.realtimeSinceStartup + 15f;
        bool attemptedFastCommit = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            if (scope?.Container != null)
            {
                world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
                if (world.Characters.Count >= 1)
                    break;

                if (!attemptedFastCommit)
                {
                    attemptedFastCommit = true;
                    StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
                    for (int frame = 0; frame < 8; frame++)
                        yield return null;
                }
            }
            yield return null;
        }

        Require(scope?.Container != null, "Runtime container is unavailable.");
        Require(world != null && world.Characters.Count > 0,
            "A live authored worker is required.");

        IObjectResolver container = scope.Container;
        IDungeonSaveSectionRegistry saves =
            container.Resolve<IDungeonSaveSectionRegistry>();
        List<DungeonSaveSectionEnvelope> baseline = saves.CaptureAll();
        float originalTimeScale = Time.timeScale;
        Dictionary<CharacterActor, bool> pauseStates = new();
        Application.logMessageReceived += CaptureUnexpectedLog;

        try
        {
            Time.timeScale = 0f;
            foreach (CharacterActor actor in world.Characters
                         .Where(IsUsableWorker)
                         .Distinct())
            {
                pauseStates[actor] = actor.IsAiPaused();
                actor.SetAiPaused(true);
                actor.Brain?.StopAllAiForLifecycleTransition(
                    "v27-balance-vertical-slice-isolation");
                actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
                actor.GetComponent<AbilityHaul>()?.StopHauling(
                    "v27-balance-vertical-slice-isolation");
            }

            ExecuteFullLoop(container, world);
        }
        finally
        {
            DungeonGameRestoreReport restoreReport = new();
            bool restored = saves.RestoreAll(baseline, restoreReport);
            Check(
                restored && restoreReport.Success,
                "V27_SLICE_BASELINE_RESTORED",
                "errors=" + string.Join("|", restoreReport.Errors)
                + "; warnings=" + string.Join("|", restoreReport.Warnings));

            if (restored && restoreReport.Success)
            {
                List<DungeonSaveSectionEnvelope> after = saves.CaptureAll();
                Check(
                    EnvelopesEqual(baseline, after),
                    "V27_SLICE_BASELINE_BYTE_EQUIVALENT",
                    $"before={baseline.Count}; after={after.Count}");
            }

            foreach (KeyValuePair<CharacterActor, bool> pair in pauseStates)
            {
                if (pair.Key != null)
                    pair.Key.SetAiPaused(pair.Value);
            }
            Time.timeScale = originalTimeScale;
            Application.logMessageReceived -= CaptureUnexpectedLog;
        }

        Check(
            unexpectedLogs.Count == 0,
            "V27_SLICE_CONSOLE_ZERO",
            unexpectedLogs.Count == 0
                ? "warnings=0; errors=0"
                : string.Join(" | ", unexpectedLogs.Take(8)));
    }

    private void ExecuteFullLoop(
        IObjectResolver container,
        ICharacterAiWorldRegistry world)
    {
        BuildingSO d03 = AssetDatabase.LoadAssetAtPath<BuildingSO>(D03Path);
        Require(d03 != null, "D03 authority is missing.");

        IGridSystemProvider gridProvider = container.Resolve<IGridSystemProvider>();
        IDungeonGridBuildingControllerProvider controllerProvider =
            container.Resolve<IDungeonGridBuildingControllerProvider>();
        IWorkOrderRuntime orders = container.Resolve<IWorkOrderRuntime>();
        IQualityTargetPipelineCommand qualityCommand =
            container.Resolve<IQualityTargetPipelineCommand>();
        IQualityTargetPipelineQuery qualityQuery =
            container.Resolve<IQualityTargetPipelineQuery>();
        IWorldItemStackRuntime items = container.Resolve<IWorldItemStackRuntime>();
        IItemDefinitionCatalog itemCatalog =
            container.Resolve<IItemDefinitionCatalog>();
        ISurvivalFoodCommand survival =
            container.Resolve<ISurvivalFoodCommand>();
        ICharacterPerformanceQuery performance =
            container.Resolve<ICharacterPerformanceQuery>();
        ICraftQualityResolver qualityResolver =
            container.Resolve<ICraftQualityResolver>();
        IRunSeedProvider runSeed = container.Resolve<IRunSeedProvider>();
        IMaterialSalvageCalculator salvage =
            container.Resolve<IMaterialSalvageCalculator>();

        Grid grid = gridProvider.Grid;
        Vector2Int anchor = FindPlacementAnchor(grid, d03, items);
        Require(
            controllerProvider.Controller.TryPlaceConstructionSite(
                d03,
                anchor,
                out string placementMessage),
            "D03 placement failed: " + placementMessage);

        ConstructionSite site = FindSite(world, d03, anchor);
        Require(site != null, "Placed D03 construction site was not published.");
        Require(
            orders.TryGetOrderFor(
                site,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState construction),
            "D03 construction order was not published.");
        CheckApproximately(
            construction.RequiredWork,
            468f,
            "V27_SLICE_CONSTRUCTION_ORDER_EXACT",
            "destination=" + construction.MaterialDestinationId);

        QualityFixture qualityFixture = ResolveRejectingQualityFixture(
            world,
            performance,
            qualityResolver,
            runSeed.RunSeed,
            construction,
            d03);
        QualityTargetPipelineSaveData request = new()
        {
            definitionId = d03.ContentDefinitionId,
            minimumQuality = qualityFixture.Minimum,
            requiredAcceptedCount = 1,
            rejectedDisposition = RejectedOutputDisposition.DismantleFacilityAndRetry,
            limitMode = QualityRepeatLimitMode.SafeLimits,
            maximumAttempts = 2,
            workBudget = construction.RequiredWork * 3f,
            workerPolicy = new WorkerSelectionPolicySaveData
            {
                mode = WorkerSelectionMode.SpecificCharacters,
                sortMode = WorkerCandidateSortMode.SpecificThenBestExpectedQuality,
                specificCharacterIds = new List<string>
                {
                    qualityFixture.Worker.Identity.PersistentId
                }
            }
        };
        Require(
            qualityCommand.CreateForWorkOrder(
                construction.WorkOrderId,
                request,
                out string pipelineId,
                out DomainFailure qualityFailure),
            "Quality pipeline rejected: " + qualityFailure);

        SpawnConstructionMaterials(items, construction, anchor);
        Require(orders.RefreshMaterialsReady(site),
            "D03 construction materials did not become ready.");
        Require(
            orders.ApplyWork(
                qualityFixture.Worker,
                site,
                BuiltInWorkTypeIds.Construct,
                construction.RequiredWork,
                out bool constructionCompleted,
                out bool constructionEffects,
                out string constructionMessage)
            && constructionCompleted
            && constructionEffects,
            "D03 construction did not complete: " + constructionMessage);

        BuildableObject building = FindCompletedBuilding(world, d03, anchor);
        Require(building != null, "Completed D03 was not published.");
        Check(
            building.Craftsmanship.Quality == qualityFixture.Actual,
            "V27_SLICE_CONSTRUCTION_COMPLETED",
            $"quality={building.Craftsmanship.Quality}; expected={qualityFixture.Actual}; "
            + $"minimum={qualityFixture.Minimum}");

        VerifyPhysicalFoodProduction(
            world,
            items,
            itemCatalog,
            survival,
            qualityFixture.Worker,
            building);

        Require(
            orders.TryGetOrderFor(
                building,
                BuiltInWorkTypeIds.Dismantle,
                out WorkOrderProgressState dismantle),
            "Quality rejection did not publish a dismantle order.");
        CheckApproximately(
            dismantle.RequiredWork,
            117f,
            "V27_SLICE_DISMANTLE_ORDER_EXACT",
            "pipeline=" + pipelineId);
        Require(
            qualityQuery.TryGetQualityPipeline(
                pipelineId,
                out QualityTargetPipelineSaveData rejected)
            && rejected.stage == QualityTargetPipelineStage.Dismantling,
            "Quality pipeline did not enter Dismantling.");

        MaterialSalvageResult expectedRecovery = salvage.Calculate(
            DismantleTargetKind.GeneralFacility,
            468f,
            d03.GetConstructionMaterials(),
            qualityFixture.Skill);
        Dictionary<string, int> expectedRecovered = expectedRecovery
            .RecoveredMaterials
            .ToDictionary(value => value.ItemId, value => value.Amount,
                StringComparer.Ordinal);
        Require(
            orders.ApplyWork(
                qualityFixture.Worker,
                building,
                BuiltInWorkTypeIds.Dismantle,
                dismantle.RequiredWork,
                out bool dismantleCompleted,
                out bool dismantleEffects,
                out string dismantleMessage)
            && dismantleCompleted
            && dismantleEffects,
            "D03 dismantle did not complete: " + dismantleMessage);

        Require(FindCompletedBuilding(world, d03, anchor) == null,
            "Rejected D03 remained after dismantle.");
        ConstructionSite retrySite = FindSite(world, d03, anchor);
        Require(retrySite != null,
            "Quality pipeline did not publish the rebuild site.");
        Dictionary<string, int> physicalRecovered = CountStacksAt(
            items,
            anchor,
            expectedRecovered.Keys,
            includeDestinationStacks: true);
        Check(
            DictionariesEqual(expectedRecovered, physicalRecovered),
            "V27_SLICE_RECOVERY_PHYSICAL_EXACT",
            "expected=" + FormatAmounts(expectedRecovered)
            + "; actual=" + FormatAmounts(physicalRecovered));

        Require(
            orders.TryGetOrderFor(
                retrySite,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState rebuild),
            "Rebuild construction order was not published.");
        CheckApproximately(
            rebuild.RequiredWork,
            468f,
            "V27_SLICE_REBUILD_ORDER_EXACT",
            "attempt=" + rebuild.QualityAttemptIndex);

        RouteRecoveryAndTopUp(
            items,
            anchor,
            rebuild,
            expectedRecovered);
        Require(orders.RefreshMaterialsReady(retrySite),
            "Rebuild materials did not become ready.");
        Require(
            orders.ApplyWork(
                qualityFixture.Worker,
                retrySite,
                BuiltInWorkTypeIds.Construct,
                rebuild.RequiredWork,
                out bool rebuildCompleted,
                out bool rebuildEffects,
                out string rebuildMessage)
            && rebuildCompleted
            && rebuildEffects,
            "D03 rebuild did not complete: " + rebuildMessage);

        BuildableObject rebuilt = FindCompletedBuilding(world, d03, anchor);
        Require(rebuilt != null, "Rebuilt D03 was not published.");
        Check(
            items.GetAllStacks().All(stack => stack == null
                || !string.Equals(
                    stack.DestinationId,
                    rebuild.MaterialDestinationId,
                    StringComparison.Ordinal)),
            "V27_SLICE_REBUILD_MATERIAL_CONSERVATION",
            "recovered reused=" + FormatAmounts(expectedRecovered)
            + "; topup=" + FormatAmounts(Subtract(
                rebuild.ItemMaterialRequirements,
                expectedRecovered)));
        Check(
            qualityQuery.TryGetQualityPipeline(
                pipelineId,
                out QualityTargetPipelineSaveData finalPipeline)
            && finalPipeline.stage is QualityTargetPipelineStage.Completed
                or QualityTargetPipelineStage.Paused,
            "V27_SLICE_REBUILD_COMPLETED",
            qualityQuery.TryGetQualityPipeline(pipelineId, out finalPipeline)
                ? $"stage={finalPipeline.stage}; quality={rebuilt.Craftsmanship.Quality}"
                : "pipeline missing");
    }

    private static QualityFixture ResolveRejectingQualityFixture(
        ICharacterAiWorldRegistry world,
        ICharacterPerformanceQuery performance,
        ICraftQualityResolver resolver,
        int runSeed,
        WorkOrderProgressState construction,
        BuildingSO definition)
    {
        string pipelineId = "quality:" + construction.WorkOrderId;
        CraftQualityRollSaveData actualRoll = resolver.Roll(
            unchecked((ulong)(uint)runSeed),
            pipelineId,
            definition.ContentDefinitionId,
            0);
        CraftQualityRollSaveData bestRoll = new()
        {
            attemptIndex = 0,
            randomA = 10,
            randomB = 10,
            randomC = 10
        };

        foreach (CharacterActor actor in world.Characters
                     .Where(IsUsableWorker)
                     .OrderBy(candidate => GetConstructionQualitySkill(
                         candidate,
                         performance))
                     .ThenBy(candidate => candidate.Identity.PersistentId,
                         StringComparer.Ordinal))
        {
            float skill = GetConstructionQualitySkill(actor, performance);
            CraftsmanshipQualityTier actual = resolver.Resolve(
                actualRoll,
                skill,
                0f,
                0f,
                4f).Tier;
            CraftsmanshipQualityTier potential = resolver.Resolve(
                bestRoll,
                skill,
                0f,
                0f,
                4f).Tier;
            CraftsmanshipQualityTier? minimum = Enum
                .GetValues(typeof(CraftsmanshipQualityTier))
                .Cast<CraftsmanshipQualityTier>()
                .Where(value => value != CraftsmanshipQualityTier.Mythic
                    && (int)value > (int)actual
                    && (int)value <= (int)potential)
                .OrderBy(value => value)
                .Cast<CraftsmanshipQualityTier?>()
                .FirstOrDefault();
            if (minimum.HasValue)
                return new QualityFixture(actor, skill, actual, minimum.Value);
        }

        throw new InvalidOperationException(
            "No live worker can deterministically miss a reachable D03 quality target.");
    }

    private static float GetConstructionQualitySkill(
        CharacterActor actor,
        ICharacterPerformanceQuery performance)
    {
        CharacterPerformanceSnapshot snapshot = performance.Evaluate(
            actor,
            "performance:work:construct:quality");
        if (!snapshot.IsApplicable)
            throw new InvalidOperationException(
                snapshot.Failure?.Message
                ?? "Construction quality performance is unavailable.");
        return Mathf.Clamp(snapshot.Value * 58f, 0f, 100f);
    }

    private void VerifyPhysicalFoodProduction(
        ICharacterAiWorldRegistry world,
        IWorldItemStackRuntime items,
        IItemDefinitionCatalog itemCatalog,
        ISurvivalFoodCommand survival,
        CharacterActor worker,
        BuildableObject d03)
    {
        IWarehouseFacility warehouse = world.Warehouses
            .Where(value => value?.Inventory != null
                && value.Inventory.Accepts(StockCategory.Food)
                && value.Inventory.CanStore(StockCategory.Food, 1))
            .OrderBy(value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        Require(warehouse != null, "A Food-compatible warehouse is required.");

        int before = CountPhysicalCategory(items, itemCatalog, StockCategory.Food);
        HashSet<string> stackIdsBefore = items.GetAllStacks()
            .Where(stack => stack != null)
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        Require(
            items.SpawnStockInWarehouse(
                warehouse,
                StockCategory.Food,
                1,
                out int spawned)
            && spawned == 1,
            "Could not seed one physical Food input.");
        Require(
            survival.TryApplySurvivalWork(
                worker.BuildingVisitor,
                d03,
                BuiltInWorkTypeIds.Cook,
                out int cooked,
                out DomainFailure failure)
            && cooked == 1,
            "D03 survival cooking failed: " + failure);

        WorldItemStackSnapshot output = items.GetAllStacks()
            .Where(stack => stack != null
                && !stackIdsBefore.Contains(stack.StackId)
                && stack.State == WorldItemStackState.Loose
                && stack.Position == d03.centerPos
                && itemCatalog.TryGet(
                    (ItemDefinitionId)stack.ItemId,
                    out ItemDefinitionSO definition)
                && definition.StockCategory == StockCategory.Food)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        int after = CountPhysicalCategory(items, itemCatalog, StockCategory.Food);
        Check(
            output != null
            && string.Equals(
                output.ItemId,
                "survival:cooked_meal",
                StringComparison.Ordinal)
            && after == before + 1,
            "V27_SLICE_D03_FOOD_PHYSICAL_CONSERVATION",
            $"before={before}; seeded=1; cooked={cooked}; after={after}; "
            + $"output={output?.ItemId ?? "<missing>"}");
    }

    private static int CountPhysicalCategory(
        IWorldItemStackRuntime items,
        IItemDefinitionCatalog catalog,
        StockCategory category) => items.GetAllStacks()
        .Where(stack => stack != null
            && stack.Quantity > 0
            && catalog.TryGet(
                (ItemDefinitionId)stack.ItemId,
                out ItemDefinitionSO definition)
            && definition.StockCategory == category)
        .Sum(stack => stack.Quantity);

    private static Vector2Int FindPlacementAnchor(
        Grid grid,
        BuildingSO definition,
        IWorldItemStackRuntime items)
    {
        HashSet<Vector2Int> occupiedByItems = items.GetAllStacks()
            .Where(stack => stack != null && stack.Quantity > 0)
            .Select(stack => stack.Position)
            .ToHashSet();
        return grid.GetCells()
            .Where(cell => cell != null)
            .Select(cell => cell.Position)
            .Where(anchor => definition.GetGridPosList(anchor).All(position =>
                grid.GetGridCell(position) is GridCell cell
                && cell.CanBuildInArea(definition)
                && cell.CanOccupy(definition.Placement.Layer)
                && cell.CanOccupy(GridLayer.Construction)
                && !occupiedByItems.Contains(position)))
            .OrderBy(position => position.y)
            .ThenBy(position => position.x)
            .FirstOrDefault();
    }

    private static void SpawnConstructionMaterials(
        IWorldItemStackRuntime items,
        WorkOrderProgressState order,
        Vector2Int anchor)
    {
        foreach (KeyValuePair<string, int> material in
                 order.ItemMaterialRequirements.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (!items.SpawnItemAt(
                    material.Key,
                    material.Value,
                    anchor,
                    WorldItemStackState.FacilityBuffer,
                    order.MaterialDestinationId,
                    out int spawned)
                || spawned != material.Value)
            {
                throw new InvalidOperationException(
                    "Construction material spawn failed: " + material.Key);
            }
        }
    }

    private static void RouteRecoveryAndTopUp(
        IWorldItemStackRuntime items,
        Vector2Int anchor,
        WorkOrderProgressState rebuild,
        IReadOnlyDictionary<string, int> recovered)
    {
        foreach (string itemId in recovered.Keys.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                         .Where(stack => stack != null
                             && stack.Position == anchor
                             && string.Equals(
                                 stack.ItemId,
                                 itemId,
                                 StringComparison.Ordinal))
                         .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
            {
                if (string.Equals(
                        stack.DestinationId,
                        rebuild.MaterialDestinationId,
                        StringComparison.Ordinal)
                    && stack.State == WorldItemStackState.FacilityBuffer)
                {
                    continue;
                }
                if (!items.TryRouteStackToDestination(
                        stack.StackId,
                        WorldItemStackState.FacilityBuffer,
                        rebuild.MaterialDestinationId,
                        anchor,
                        out string routeFailure))
                {
                    throw new InvalidOperationException(
                        "Recovered material route failed: " + routeFailure);
                }
            }
        }

        foreach (KeyValuePair<string, int> requirement in
                 rebuild.ItemMaterialRequirements.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            int routed = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        requirement.Key,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.DestinationId,
                        rebuild.MaterialDestinationId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            int missing = Mathf.Max(0, requirement.Value - routed);
            if (missing == 0)
                continue;
            if (!items.SpawnItemAt(
                    requirement.Key,
                    missing,
                    anchor,
                    WorldItemStackState.FacilityBuffer,
                    rebuild.MaterialDestinationId,
                    out int spawned)
                || spawned != missing)
            {
                throw new InvalidOperationException(
                    "Rebuild top-up failed: " + requirement.Key);
            }
        }
    }

    private static ConstructionSite FindSite(
        ICharacterAiWorldRegistry world,
        BuildingSO definition,
        Vector2Int anchor) => world.Buildings
        .OfType<ConstructionSite>()
        .Where(value => value != null
            && !value.isDestroy
            && value.id == definition.id
            && value.centerPos == anchor)
        .OrderBy(value => value.PersistentInstanceId.Value,
            StringComparer.Ordinal)
        .FirstOrDefault();

    private static BuildableObject FindCompletedBuilding(
        ICharacterAiWorldRegistry world,
        BuildingSO definition,
        Vector2Int anchor) => world.Buildings
        .Where(value => value != null
            && !value.isDestroy
            && value is not ConstructionSite
            && value.id == definition.id
            && value.centerPos == anchor)
        .OrderBy(value => value.PersistentInstanceId.Value,
            StringComparer.Ordinal)
        .FirstOrDefault();

    private static Dictionary<string, int> CountStacksAt(
        IWorldItemStackRuntime items,
        Vector2Int position,
        IEnumerable<string> itemIds,
        bool includeDestinationStacks)
    {
        HashSet<string> accepted = itemIds.ToHashSet(StringComparer.Ordinal);
        return items.GetAllStacks()
            .Where(stack => stack != null
                && stack.Position == position
                && stack.Quantity > 0
                && accepted.Contains(stack.ItemId)
                && (includeDestinationStacks
                    || string.IsNullOrWhiteSpace(stack.DestinationId)))
            .GroupBy(stack => stack.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(stack => stack.Quantity),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, int> Subtract(
        IReadOnlyDictionary<string, int> total,
        IReadOnlyDictionary<string, int> recovered) => total
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .ToDictionary(
            pair => pair.Key,
            pair => Mathf.Max(
                0,
                pair.Value - (recovered.TryGetValue(pair.Key, out int value)
                    ? value
                    : 0)),
            StringComparer.Ordinal);

    private static bool IsUsableWorker(CharacterActor actor) =>
        actor != null
        && actor.Identity != null
        && !actor.IsDead
        && !string.IsNullOrWhiteSpace(actor.Identity.PersistentId);

    private static bool DictionariesEqual(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right) =>
        left.Count == right.Count
        && left.All(pair => right.TryGetValue(pair.Key, out int value)
            && value == pair.Value);

    private static bool EnvelopesEqual(
        IReadOnlyList<DungeonSaveSectionEnvelope> left,
        IReadOnlyList<DungeonSaveSectionEnvelope> right)
    {
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            DungeonSaveSectionEnvelope a = left[i];
            DungeonSaveSectionEnvelope b = right[i];
            if (!string.Equals(a.sectionId, b.sectionId, StringComparison.Ordinal)
                || a.sectionVersion != b.sectionVersion
                || a.restorePhase != b.restorePhase
                || a.optional != b.optional
                || !string.Equals(a.payloadJson, b.payloadJson,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static string FormatAmounts(IReadOnlyDictionary<string, int> values) =>
        string.Join("|", values.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + pair.Value));

    private void CaptureUnexpectedLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Warning or LogType.Error or LogType.Exception
            or LogType.Assert)
        {
            unexpectedLogs.Add(type + ":" + condition);
        }
    }

    private void CheckApproximately(
        float actual,
        float expected,
        string marker,
        string detail)
    {
        Check(
            Mathf.Approximately(actual, expected),
            marker,
            $"expected={Token(expected)}; actual={Token(actual)}; {detail}");
    }

    private void Check(bool passed, string marker, string detail)
    {
        string row = (passed ? "PASS " : "FAIL ") + marker + " " + detail;
        checks.Add(row);
        if (!passed)
            failures.Add(row);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private void WriteReport()
    {
        StringBuilder report = new(4096);
        report.Append("RESULT=")
            .Append(failures.Count == 0 ? "PASS" : "FAIL")
            .Append("; checks=")
            .Append(checks.Count)
            .Append("; failures=")
            .Append(failures.Count)
            .AppendLine();
        foreach (string check in checks)
            report.AppendLine(check);
        foreach (string failure in failures.Where(value =>
                     !value.StartsWith("FAIL ", StringComparison.Ordinal)))
        {
            report.Append("ERROR ").AppendLine(failure);
        }

        V27BalanceArtifactWriter.WriteIfDifferent(
            V27BalanceVerticalSlicePlayModeVerifier.ReportPath,
            stream =>
            {
                using StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(false, true),
                    4096,
                    leaveOpen: true);
                writer.Write(report.ToString());
                writer.Flush();
            });
        Debug.Log(report.ToString());
    }

    private static string Token(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private readonly struct QualityFixture
    {
        public QualityFixture(
            CharacterActor worker,
            float skill,
            CraftsmanshipQualityTier actual,
            CraftsmanshipQualityTier minimum)
        {
            Worker = worker;
            Skill = skill;
            Actual = actual;
            Minimum = minimum;
        }

        public CharacterActor Worker { get; }
        public float Skill { get; }
        public CraftsmanshipQualityTier Actual { get; }
        public CraftsmanshipQualityTier Minimum { get; }
    }
}
#endif
