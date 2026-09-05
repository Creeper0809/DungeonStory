#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DungeonSpaceExpansionPlayModeVerifier
{
    public const string RequestPath =
        "Temp/v27-balance-expansion-playmode.request";
    public const string ReportPath =
        "Artifacts/QA/v27-balance-expansion-playmode.txt";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;

    static DungeonSpaceExpansionPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/V27/Request Research-Gated Expansion PlayMode")]
    public static void RequestRun()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.WriteAllText(RequestPath, "requested");
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            WriteFailure(
                "EXPANSION_PLAYMODE_SCENE_REQUIRED",
                "GameplayScene must already be active; the verifier will not reopen or discard a dirty scene.");
            File.Delete(RequestPath);
            return;
        }

        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
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
        new GameObject("Dungeon Space Expansion PlayMode Verification Runner")
            .AddComponent<DungeonSpaceExpansionPlayModeVerificationRunner>();
    }

    internal static void Finish()
    {
        File.Delete(RequestPath);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        };
    }

    internal static void WriteReport(IEnumerable<string> lines)
    {
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(ReportPath, string.Join("\n", lines) + "\n");
    }

    private static void WriteFailure(string marker, string detail)
    {
        WriteReport(new[]
        {
            "RESULT=FAIL; failures=1;",
            "FAIL\t" + marker,
            "DETAIL\t" + detail
        });
    }
}

public sealed class DungeonSpaceExpansionPlayModeVerificationRunner : MonoBehaviour
{
    private readonly List<string> rows = new List<string>();
    private readonly List<string> failures = new List<string>();

    private IEnumerator Start()
    {
        yield return null;
        GameObject researchRoot = null;
        GameObject facilityRoot = null;
        DungeonSpaceExpansionRuntime expansion = null;
        GameEventBus events = null;
        try
        {
            Dictionary<string, ResearchProjectSO> projects = LoadProjects();
            Require(projects.Count == 180,
                $"Expected 180 research projects; found {projects.Count}.");

            TestGridAuthority gridAuthority =
                new TestGridAuthority(CreateInitialGrid());
            events = new GameEventBus();
            expansion = new DungeonSpaceExpansionRuntime(
                events,
                gridAuthority,
                gridAuthority);
            expansion.Start();

            researchRoot = new GameObject("Expansion Research Runtime Fixture");
            researchRoot.SetActive(false);
            BlueprintResearchRuntime research =
                researchRoot.AddComponent<BlueprintResearchRuntime>();
            FacilityCandidateCacheStore facilityCandidates =
                new FacilityCandidateCacheStore(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    frameWorkBudget: null);
            research.Construct(
                new FixedUnlockStateService(),
                EmptyShopCatalog.Instance,
                facilityCandidates,
                new DungeonWorkforceReplanService(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    facilityCandidates,
                    haulPlanningService: null),
                events,
                itemStackRuntime: null,
                projectCoordinator: new BlueprintResearchProjectCoordinator(
                    new ResourceResearchProjectCatalog(projects.Values),
                    UnavailableResearchBlueprintArchiveQuery.Instance,
                    UnrestrictedResearchFacilityCapacityQuery.Instance),
                worldDropZoneQuery: null,
                aggregateRootStore: new DungeonRuntimeAggregateRootStore(),
                debugRules: DisabledDungeonDebugRuleQuery.Instance,
                uiClock: new UnityUiClock());

            facilityRoot = new GameObject("Expansion Research Facility Fixture");
            facilityRoot.SetActive(false);
            Facility facility = facilityRoot.AddComponent<Facility>();
            CharacterAiEditorTestDependencies.Inject(facility, research);
            BuildingSO facilityDefinition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset");
            Require(facilityDefinition != null,
                "P1_ResearchLab asset is unavailable.");
            Vector2Int facilityAnchor = new(20, 1);
            facility.SetGrid(gridAuthority.Grid);
            facility.Initialization(facilityDefinition, facilityAnchor);
            Vector2Int[] requiredFacilityCells = facilityDefinition
                .GetGridPosList(facilityAnchor)
                .Distinct()
                .OrderBy(value => value.y)
                .ThenBy(value => value.x)
                .ToArray();
            Require(gridAuthority.Grid.RegisterOccupant(
                    facility,
                    facilityDefinition.layer,
                    requiredFacilityCells,
                    false),
                "The authored research facility could not be registered on the live grid.");
            Require(requiredFacilityCells.All(value => ReferenceEquals(
                    gridAuthority.Grid.GetGridCell(value)?.GetOccupant(
                        facilityDefinition.layer),
                    facility)),
                "The authored research facility was not registered on the live grid.");
            Require(facility.SupportsWork(BuiltInWorkTypeIds.Research),
                "P1_ResearchLab does not expose authored Research work.");

            Require(expansion.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult tierZero,
                    out string tierZeroFailure),
                "New-run Tier-0 reconciliation failed: " + tierZeroFailure);
            Require(tierZero.Changed
                    && tierZero.PreviousInteriorColumns == 27
                    && tierZero.CurrentInteriorColumns == 29,
                "New-run Tier-0 reconciliation was not the exact 27-to-29 transition.");
            rows.Add("PASS\tEXPANSION_LIVE_NEW_RUN_TIER_ZERO_27_TO_29");

            CompleteExpansion(
                research,
                facility,
                projects[DungeonSpaceExpansionCatalog.QuarryResearchId],
                gridAuthority,
                expectedInteriorColumns: 51,
                expectedGridWidth: 68);
            rows.Add("PASS\tEXPANSION_LIVE_RESEARCH_QUARRY_29_TO_51");

            CompleteExpansion(
                research,
                facility,
                projects[DungeonSpaceExpansionCatalog.StonecuttingResearchId],
                gridAuthority,
                expectedInteriorColumns: 71,
                expectedGridWidth: 88);
            rows.Add("PASS\tEXPANSION_LIVE_RESEARCH_STONECUTTING_51_TO_71");

            CompleteExpansion(
                research,
                facility,
                projects[DungeonSpaceExpansionCatalog.DeepMiningResearchId],
                gridAuthority,
                expectedInteriorColumns: 87,
                expectedGridWidth: 104);
            rows.Add("PASS\tEXPANSION_LIVE_RESEARCH_DEEP_MINING_71_TO_87");

            Require(gridAuthority.PublicationCount == 4
                    && gridAuthority.CompletionCount == 4,
                $"Expected Tier-0 plus three exact publications; found {gridAuthority.PublicationCount}/{gridAuthority.CompletionCount}.");
            Require(research.State.Projects.IsCompleted(
                    new ResearchProjectId(
                        DungeonSpaceExpansionCatalog.DeepMiningResearchId)),
                "Deep-mining expansion research was not completed in the authored research state.");
            rows.Add("PASS\tEXPANSION_LIVE_EVENT_PUBLICATION_EXACT_ONCE");
            rows.Add("PASS\tEXPANSION_LIVE_ENTRANCE_AND_COORDINATES_PRESERVED");
            Require(requiredFacilityCells.All(value => ReferenceEquals(
                    gridAuthority.Grid.GetGridCell(value)?.GetOccupant(
                        facilityDefinition.layer),
                    facility)),
                "A required facility was demolished or moved by research expansion.");
            rows.Add("PASS\tEXPANSION_LIVE_REQUIRED_FACILITIES_PRESERVED_NO_DEMOLITION");
        }
        catch (Exception exception)
        {
            failures.Add(exception.GetType().Name + ": " + exception.Message);
            Debug.LogException(exception);
        }
        finally
        {
            expansion?.Dispose();
            events?.Clear();
            if (facilityRoot != null)
            {
                DestroyImmediate(facilityRoot);
            }
            if (researchRoot != null)
            {
                DestroyImmediate(researchRoot);
            }

            List<string> report = new List<string>
            {
                failures.Count == 0
                    ? "RESULT=PASS; failures=0; liveResearchCompletions=3; publications=4;"
                    : $"RESULT=FAIL; failures={failures.Count};"
            };
            report.AddRange(rows);
            report.AddRange(failures.Select(value => "DETAIL\t" + value));
            DungeonSpaceExpansionPlayModeVerifier.WriteReport(report);
            DungeonSpaceExpansionPlayModeVerifier.Finish();
        }
    }

    private static void CompleteExpansion(
        BlueprintResearchRuntime research,
        BuildableObject facility,
        ResearchProjectSO project,
        TestGridAuthority gridAuthority,
        int expectedInteriorColumns,
        int expectedGridWidth)
    {
        CompletePrerequisites(research, project);
        ResearchQueueCommandResult queued = research.EnqueueProject(project.ProjectId);
        Require(queued.Succeeded,
            $"Could not enqueue {project.ProjectId.Value}: {queued.Message}");
        BlueprintResearchWorkResult work = research.ApplyApprovedResearchWork(
            researcher: null,
            researchFacility: facility,
            approvedWorkUnits: project.RequiredWork);
        Require(work.Success && work.Completed,
            $"Authored research completion failed for {project.ProjectId.Value}: {work.Message}");
        Require(research.State.Projects.IsCompleted(project.ProjectId),
            $"Research state did not complete {project.ProjectId.Value}.");
        Require(DungeonSpaceGridLayout.TryCapture(
                gridAuthority.Grid,
                out DungeonInteriorLayoutSnapshot layout,
                out string failureReason),
            "Published expansion layout is invalid: " + failureReason);
        Require(layout.StartX == 17
                && layout.ColumnCount == expectedInteriorColumns
                && layout.EntrancePosition == new Vector2Int(17, 0)
                && gridAuthority.Grid.width == expectedGridWidth,
            $"Published layout mismatch after {project.ProjectId.Value}: start={layout.StartX}; columns={layout.ColumnCount}; width={gridAuthority.Grid.width}; entrance={layout.EntrancePosition}.");
    }

    private static void CompletePrerequisites(
        BlueprintResearchRuntime research,
        ResearchProjectSO project)
    {
        foreach (ResearchProjectSO prerequisite in project.Prerequisites)
        {
            if (research.State.Projects.IsCompleted(prerequisite.ProjectId))
            {
                continue;
            }

            CompletePrerequisites(research, prerequisite);
            research.State.Projects.RestoreCompleted(prerequisite.ProjectId);
        }
    }

    private static Dictionary<string, ResearchProjectSO> LoadProjects()
    {
        return AssetDatabase.FindAssets(
                "t:ResearchProjectSO",
                new[] { "Assets/Resources/SO/Research/Projects" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null && project.ProjectId.IsValid)
            .ToDictionary(
                project => project.ProjectId.Value,
                project => project,
                StringComparer.Ordinal);
    }

    private static Grid CreateInitialGrid()
    {
        Grid grid = new Grid(60, DungeonSpaceExpansionCatalog.SupportedGridHeight);
        foreach (GridCell cell in grid.GetCells())
        {
            grid.SetAreaType(cell.Position, GridCellAreaType.BlockedExterior);
        }
        for (int x = 17;
             x < 17 + DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns;
             x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                grid.SetAreaType(
                    new Vector2Int(x, y),
                    x == 17 && y == 0
                        ? GridCellAreaType.Entrance
                        : GridCellAreaType.DungeonInterior);
            }
        }
        return grid;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedUnlockStateService : IFacilityShopUnlockStateService
    {
        private readonly FacilityShopUnlockState state = new FacilityShopUnlockState();

        public FacilityShopUnlockState GetUnlockState() => state;
    }

    private sealed class EmptyShopCatalog : IFacilityShopCatalog
    {
        public static readonly EmptyShopCatalog Instance = new EmptyShopCatalog();
        public IReadOnlyCollection<BuildingSO> Buildings => Array.Empty<BuildingSO>();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
            Array.Empty<FacilityBlueprintSO>();
        public BuildingSO FindBuildingById(int buildingId) => null;
    }

    private sealed class TestGridAuthority : IGridSystemProvider, IGridSystemPublisher
    {
        public TestGridAuthority(Grid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public GridSystemManager Manager => null;
        public Grid Grid { get; private set; }
        public int PublicationCount { get; private set; }
        public int CompletionCount { get; private set; }

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return grid != null;
        }

        public bool TryPublishGrid(
            Grid expectedCurrent,
            Grid replacement,
            out string failureReason)
        {
            if (!ReferenceEquals(Grid, expectedCurrent) || replacement == null)
            {
                failureReason = "Grid publication expectation changed.";
                return false;
            }
            Grid = replacement;
            PublicationCount++;
            failureReason = string.Empty;
            return true;
        }

        public void CompleteGridPublication()
        {
            CompletionCount++;
        }
    }
}
#endif
