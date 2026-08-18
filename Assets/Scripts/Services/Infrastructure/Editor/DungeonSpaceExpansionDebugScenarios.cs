#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class DungeonSpaceExpansionDebugScenarios
{
    private const string ReportPath =
        "Artifacts/QA/v27-balance-expansion-editmode.txt";

    [MenuItem("DungeonStory/V27/Verify Research-Gated Dungeon Expansion")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Research-gated dungeon expansion scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> rows = new List<string>();
        List<string> failures = new List<string>();
        Verify(
            "EXPANSION_RESEARCH_ASSETS_EXACT",
            VerifyResearchAssets,
            rows,
            failures);
        Verify(
            "EXPANSION_EVENT_27_49_65_81_EXACT",
            VerifyResearchEventExpansion,
            rows,
            failures);
        Verify(
            "EXPANSION_OUT_OF_ORDER_DEEP_IDEMPOTENT",
            VerifyDeepCompletionDirectExpansion,
            rows,
            failures);
        Verify(
            "EXPANSION_GRID_COPY_ATOMIC_AND_OCCUPANTS_PRESERVED",
            VerifyDetachedExpansionCopy,
            rows,
            failures);
        Verify(
            "EXPANSION_SAVE_V5_LAYOUT_ROUNDTRIP_EXACT",
            VerifySaveLayoutRoundTrip,
            rows,
            failures);
        Verify(
            "EXPANSION_SAVE_RESEARCH_LAYOUT_AUTHORITY_EXACT",
            VerifySaveResearchLayoutAuthority,
            rows,
            failures);
        Verify(
            "EXPANSION_E_KEY_DEVELOPER_ONLY",
            VerifyDeveloperKeyIsolation,
            rows,
            failures);

        rows.Insert(
            0,
            failures.Count == 0
                ? "RESULT=PASS; failures=0; research=3; columns=27,49,65,81;"
                : $"RESULT=FAIL; failures={failures.Count}; research=3; columns=27,49,65,81;");
        rows.AddRange(failures.Select(failure => "DETAIL\t" + failure));
        WriteReport(rows);

        foreach (string failure in failures)
        {
            Debug.LogError(failure);
        }
        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log("Research-gated dungeon expansion scenarios passed.");
        }

        return failures.Count == 0;
    }

    private static bool VerifyResearchAssets()
    {
        Dictionary<string, ResearchProjectSO> projects = LoadProjects();
        Require(projects.Count == 180, $"Expected 180 research projects; found {projects.Count}.");

        VerifyProject(
            projects,
            DungeonSpaceExpansionCatalog.QuarryResearchId,
            numericId: 7082,
            requiredWork: 28f,
            expectedPrerequisites: new[]
            {
                "research:mining:surface"
            },
            expectedFacilities: new[]
            {
                ResearchFacilityCapabilityId.Basic
            });
        VerifyProject(
            projects,
            DungeonSpaceExpansionCatalog.StonecuttingResearchId,
            numericId: 7083,
            requiredWork: 42f,
            expectedPrerequisites: new[]
            {
                "research:mining:quarry"
            },
            expectedFacilities: new[]
            {
                ResearchFacilityCapabilityId.Basic,
                ResearchFacilityCapabilityId.Design
            });
        VerifyProject(
            projects,
            DungeonSpaceExpansionCatalog.DeepMiningResearchId,
            numericId: 7085,
            requiredWork: 60f,
            expectedPrerequisites: new[]
            {
                "research:mining:sorting"
            },
            expectedFacilities: new[]
            {
                ResearchFacilityCapabilityId.Basic,
                ResearchFacilityCapabilityId.Design
            });

        float totalWork = projects.Values.Sum(project => project.RequiredWork);
        Require(Mathf.Approximately(totalWork, 63173f),
            $"Expected research work total 63173; found {totalWork:0.###}.");
        ResourceResearchProjectCatalog catalog =
            new ResourceResearchProjectCatalog(projects.Values);
        Require(catalog.Validate().Count == 0,
            "The 180-project research catalog failed validation.");
        return true;
    }

    private static bool VerifyResearchEventExpansion()
    {
        Dictionary<string, ResearchProjectSO> projects = LoadProjects();
        Grid initial = CreateInitialGrid();
        TestGridAuthority authority = new TestGridAuthority(initial);
        GameEventBus events = new GameEventBus();
        DungeonSpaceExpansionRuntime runtime =
            new DungeonSpaceExpansionRuntime(events, authority, authority);
        runtime.Start();
        try
        {
            ResearchProjectSO unrelated = projects["research:mining:surface"];
            events.Publish(new BlueprintResearchCompletedEvent(unrelated, default));
            Require(ReferenceEquals(authority.Grid, initial),
                "Unrelated research replaced the dungeon grid.");
            RequireLayout(authority.Grid, 27, 60, new Vector2Int(17, 0));

            Grid beforeQuarry = authority.Grid;
            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.QuarryResearchId]);
            RequireLayout(authority.Grid, 49, 66, new Vector2Int(17, 0));
            Require(beforeQuarry.GetGridCell(new Vector2Int(44, 0)).AreaType
                    == GridCellAreaType.BlockedExterior,
                "Quarry expansion mutated the previously published grid in place.");

            Grid quarryGrid = authority.Grid;
            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.QuarryResearchId]);
            Require(ReferenceEquals(authority.Grid, quarryGrid),
                "Re-completing quarry expansion was not idempotent.");
            Require(!runtime.LastResult.Changed,
                "Idempotent quarry expansion reported a mutation.");

            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.StonecuttingResearchId]);
            RequireLayout(authority.Grid, 65, 82, new Vector2Int(17, 0));

            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.DeepMiningResearchId]);
            RequireLayout(authority.Grid, 81, 98, new Vector2Int(17, 0));
            Require(runtime.LastResult.Changed
                    && runtime.LastResult.AddedInteriorColumns == 16,
                "Deep expansion result did not report the exact +16 columns.");
            Require(authority.PublicationCount == 3,
                $"Expected exactly 3 grid publications; found {authority.PublicationCount}.");
            Require(authority.CompletionCount == 3,
                $"Expected exactly 3 completed publications; found {authority.CompletionCount}.");
        }
        finally
        {
            runtime.Dispose();
            events.Clear();
        }

        return true;
    }

    private static bool VerifyDeepCompletionDirectExpansion()
    {
        Dictionary<string, ResearchProjectSO> projects = LoadProjects();
        Grid initial = CreateInitialGrid();
        TestGridAuthority authority = new TestGridAuthority(initial);
        GameEventBus events = new GameEventBus();
        DungeonSpaceExpansionRuntime runtime =
            new DungeonSpaceExpansionRuntime(events, authority, authority);
        runtime.Start();
        try
        {
            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.DeepMiningResearchId]);
            RequireLayout(authority.Grid, 81, 98, new Vector2Int(17, 0));
            Require(runtime.LastResult.Changed
                    && runtime.LastResult.AddedInteriorColumns == 54,
                "Direct deep-mining completion did not publish the exact 27-to-81 expansion.");

            Grid deepGrid = authority.Grid;
            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.QuarryResearchId]);
            Require(ReferenceEquals(authority.Grid, deepGrid)
                    && !runtime.LastResult.Changed,
                "Late quarry completion changed an already-expanded deep layout.");
            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.StonecuttingResearchId]);
            Require(ReferenceEquals(authority.Grid, deepGrid)
                    && !runtime.LastResult.Changed,
                "Late stonecutting completion changed an already-expanded deep layout.");
            Require(authority.PublicationCount == 1
                    && authority.CompletionCount == 1,
                $"Direct deep expansion expected one publication; found {authority.PublicationCount}/{authority.CompletionCount}.");
        }
        finally
        {
            runtime.Dispose();
            events.Clear();
        }

        return true;
    }

    private static bool VerifyDetachedExpansionCopy()
    {
        Grid grid = CreateInitialGrid();
        TestOccupant occupant = new TestOccupant(901);
        Vector2Int occupied = new Vector2Int(20, 0);
        Require(grid.RegisterOccupant(
                occupant,
                GridLayer.Building,
                new[] { occupied },
                connectPositions: false),
            "Could not register the expansion-copy fixture occupant.");

        Grid expanded = grid.TryExpandGrid(6, 0);
        Require(expanded != null, "Grid expansion copy returned null.");
        Require(!ReferenceEquals(
                grid.GetGridCell(occupied),
                expanded.GetGridCell(occupied)),
            "Expanded grid reused a live GridCell reference.");
        Require(ReferenceEquals(
                expanded.GetGridCell(occupied).GetOccupant(GridLayer.Building),
                occupant),
            "Expanded grid lost the registered occupant.");
        Require(ReferenceEquals(
                grid.GetGridCell(occupied).GetOccupant(GridLayer.Building),
                occupant),
            "Building the expansion candidate mutated the live occupant registration.");
        return true;
    }

    private static bool VerifySaveLayoutRoundTrip()
    {
        Grid grid = CreateInitialGrid();
        foreach (DungeonSpaceExpansionDefinition definition in
                 DungeonSpaceExpansionCatalog.All)
        {
            int targetEnd = 17 + definition.TargetInteriorColumns;
            int widthDelta = Mathf.Max(0, targetEnd - grid.width);
            Grid expanded = grid.TryExpandGrid(widthDelta, 0);
            Require(expanded != null,
                $"Could not allocate tier {definition.Tier} save fixture.");
            for (int x = 17 + (definition.Tier == 1
                         ? 27
                         : DungeonSpaceExpansionCatalog.All[definition.Tier - 2]
                             .TargetInteriorColumns);
                 x < targetEnd;
                 x++)
            {
                for (int y = 0; y < expanded.height; y++)
                {
                    expanded.SetAreaType(
                        new Vector2Int(x, y),
                        GridCellAreaType.DungeonInterior);
                }
            }
            grid = expanded;
        }

        ModularFacilityWorldSaveData original = new ModularFacilityWorldSaveData
        {
            version = ModularFacilityWorldSaveService.CurrentVersion,
            gridWidth = grid.width,
            gridHeight = grid.height,
            gridCells = grid.GetCells()
                .OrderBy(cell => cell.Position.y)
                .ThenBy(cell => cell.Position.x)
                .Select(ModularFacilityGridCellSaveData.From)
                .ToList()
        };
        string json = JsonUtility.ToJson(original);
        ModularFacilityWorldSaveData restored =
            JsonUtility.FromJson<ModularFacilityWorldSaveData>(json);
        Require(restored != null
                && restored.version == ModularFacilityWorldSaveService.CurrentVersion,
            "V5 facility layout did not survive JSON round-trip.");
        Require(DungeonSpaceGridLayout.TryCapture(
                restored,
                out DungeonInteriorLayoutSnapshot layout,
                out string failureReason),
            "Restored V5 layout is invalid: " + failureReason);
        Require(layout.StartX == 17
                && layout.ColumnCount == 81
                && layout.EntrancePosition == new Vector2Int(17, 0),
            $"Restored V5 layout mismatch: start={layout.StartX}; columns={layout.ColumnCount}; entrance={layout.EntrancePosition}.");
        return true;
    }

    private static bool VerifyDeveloperKeyIsolation()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string runtimeSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs"));
        string controllerSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "Assets/Scripts/Controllers/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs"));
        Require(!runtimeSource.Contains("GridExpand(", StringComparison.Ordinal),
            "Production expansion runtime calls the developer GridExpand command.");
        Require(controllerSource.Contains(
                "debugMode?.IsDeveloperModeEnabled != true",
                StringComparison.Ordinal)
                && controllerSource.Contains("gridSystem.GridExpand(2, 2);", StringComparison.Ordinal),
            "The E-key expansion is not visibly guarded as a developer-only command.");
        return true;
    }

    private static bool VerifySaveResearchLayoutAuthority()
    {
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                Array.Empty<string>()) == 27,
            "A save with no expansion research did not resolve to 27 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[] { DungeonSpaceExpansionCatalog.QuarryResearchId }) == 49,
            "A quarry-completed save did not resolve to 49 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[] { DungeonSpaceExpansionCatalog.StonecuttingResearchId }) == 65,
            "A stonecutting-only restored set did not resolve to 65 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[] { DungeonSpaceExpansionCatalog.DeepMiningResearchId }) == 81,
            "A deep-mining-only restored set did not resolve to 81 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[]
                {
                    DungeonSpaceExpansionCatalog.DeepMiningResearchId,
                    DungeonSpaceExpansionCatalog.QuarryResearchId,
                    DungeonSpaceExpansionCatalog.StonecuttingResearchId
                }) == 81,
            "The highest completed mining expansion was not authoritative.");
        return true;
    }

    private static void VerifyProject(
        IReadOnlyDictionary<string, ResearchProjectSO> projects,
        string stableId,
        int numericId,
        float requiredWork,
        IReadOnlyList<string> expectedPrerequisites,
        IReadOnlyList<ResearchFacilityCapabilityId> expectedFacilities)
    {
        Require(projects.TryGetValue(stableId, out ResearchProjectSO project),
            $"Research project is missing: {stableId}.");
        Require(project.id == numericId,
            $"Research numeric ID mismatch for {stableId}: {project.id}.");
        Require(Mathf.Approximately(project.RequiredWork, requiredWork),
            $"Research WU mismatch for {stableId}: {project.RequiredWork:0.###}.");
        string[] prerequisites = project.Prerequisites
            .Select(value => value.ProjectId.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(prerequisites.SequenceEqual(
                expectedPrerequisites.OrderBy(value => value, StringComparer.Ordinal)),
            $"Research prerequisite mismatch for {stableId}: {string.Join(",", prerequisites)}.");
        ResearchFacilityCapabilityId[] facilities = project.FacilityRequirements
            .Select(value => value.capability)
            .OrderBy(value => value)
            .ToArray();
        Require(facilities.SequenceEqual(expectedFacilities.OrderBy(value => value)),
            $"Research facility requirement mismatch for {stableId}: {string.Join(",", facilities)}.");
        Require(project.BlueprintRule == ResearchBlueprintRule.None
                && project.Blueprint == null,
            $"Expansion research unexpectedly requires a blueprint: {stableId}.");
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
        for (int x = 17; x < 17 + DungeonSpaceExpansionCatalog.InitialInteriorColumns; x++)
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

    private static void Publish(GameEventBus events, ResearchProjectSO project)
    {
        events.Publish(new BlueprintResearchCompletedEvent(project, default));
    }

    private static void RequireLayout(
        Grid grid,
        int expectedColumns,
        int expectedWidth,
        Vector2Int expectedEntrance)
    {
        Require(grid != null && grid.width == expectedWidth,
            $"Grid width mismatch: expected {expectedWidth}; found {grid?.width ?? 0}.");
        Require(DungeonSpaceGridLayout.TryCapture(
                grid,
                out DungeonInteriorLayoutSnapshot layout,
                out string failureReason),
            "Grid layout is invalid: " + failureReason);
        Require(layout.ColumnCount == expectedColumns
                && layout.EntrancePosition == expectedEntrance,
            $"Layout mismatch: columns={layout.ColumnCount}; entrance={layout.EntrancePosition}.");
    }

    private static void Verify(
        string marker,
        Func<bool> scenario,
        ICollection<string> rows,
        ICollection<string> failures)
    {
        try
        {
            if (!scenario())
            {
                throw new InvalidOperationException("Scenario returned false.");
            }
            rows.Add("PASS\t" + marker);
        }
        catch (Exception exception)
        {
            rows.Add("FAIL\t" + marker);
            failures.Add(marker + ": " + exception.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void WriteReport(IEnumerable<string> rows)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string fullPath = Path.Combine(projectRoot, ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        File.WriteAllText(fullPath, string.Join("\n", rows) + "\n");
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

    private sealed class TestOccupant : IGridOccupant
    {
        public TestOccupant(int id)
        {
            GridId = id;
        }

        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => true;
        public bool IsGridMovement => false;
    }
}
#endif
