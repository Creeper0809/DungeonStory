#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
            "EXPANSION_NEW_RUN_TIER_ZERO_27_TO_29_RESTORE_PRESERVED",
            VerifyNewRunTierZeroAndRestorePreservation,
            rows,
            failures);
        Verify(
            "EXPANSION_NEW_RUN_TIER_ZERO_REJECTS_NONCANONICAL_WIDTHS",
            VerifyNewRunTierZeroRejectsNoncanonicalWidths,
            rows,
            failures);
        Verify(
            "EXPANSION_EVENT_29_51_71_87_EXACT",
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
            "EXPANSION_CAPTURE_AND_RESTORE_PREFLIGHT_EXACT",
            VerifyExpansionPreflightContract,
            rows,
            failures);
        Verify(
            "EXPANSION_E_KEY_DEVELOPER_ONLY",
            VerifyDeveloperKeyIsolation,
            rows,
            failures);
        Verify(
            "EXPANSION_GAMEPLAY_SCENE_SHA_UNCHANGED",
            VerifyGameplaySceneShaUnchanged,
            rows,
            failures);

        rows.Insert(
            0,
            failures.Count == 0
                ? "RESULT=PASS; failures=0; research=3; columns=29,51,71,87; sceneSeed=27;"
                : $"RESULT=FAIL; failures={failures.Count}; research=3; columns=29,51,71,87; sceneSeed=27;");
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
                ResearchFacilityCapabilityId.Design,
                ResearchFacilityCapabilityId.Advanced
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

            Require(runtime.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult tierZero,
                    out string tierZeroFailure),
                "New-run Tier-0 reconciliation failed: " + tierZeroFailure);
            Require(tierZero.Changed
                    && tierZero.PreviousInteriorColumns == 27
                    && tierZero.CurrentInteriorColumns == 29,
                "New-run Tier-0 reconciliation did not publish exact 27-to-29 columns.");
            RequireLayout(authority.Grid, 29, 60, new Vector2Int(17, 0));

            Grid beforeQuarry = authority.Grid;
            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.QuarryResearchId]);
            RequireLayout(authority.Grid, 51, 68, new Vector2Int(17, 0));
            Require(beforeQuarry.GetGridCell(new Vector2Int(46, 0)).AreaType
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
            RequireLayout(authority.Grid, 71, 88, new Vector2Int(17, 0));

            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.DeepMiningResearchId]);
            RequireLayout(authority.Grid, 87, 104, new Vector2Int(17, 0));
            Require(runtime.LastResult.Changed
                    && runtime.LastResult.AddedInteriorColumns == 16,
                "Deep expansion result did not report the exact +16 columns.");
            Require(authority.PublicationCount == 4,
                $"Expected Tier-0 plus 3 research publications; found {authority.PublicationCount}.");
            Require(authority.CompletionCount == 4,
                $"Expected Tier-0 plus 3 completed publications; found {authority.CompletionCount}.");
        }
        finally
        {
            runtime.Dispose();
            events.Clear();
        }

        return true;
    }

    private static bool VerifyNewRunTierZeroAndRestorePreservation()
    {
        Grid restoredTierZero = CreateTierZeroGrid();
        TestGridAuthority restoredAuthority = new(restoredTierZero);
        GameEventBus restoredEvents = new();
        DungeonSpaceExpansionRuntime restoredRuntime =
            new(restoredEvents, restoredAuthority, restoredAuthority);
        restoredRuntime.Start();
        try
        {
            Require(ReferenceEquals(restoredAuthority.Grid, restoredTierZero)
                    && restoredAuthority.PublicationCount == 0,
                "Runtime startup mutated a current-format restored Tier-0 layout.");
            RequireLayout(restoredAuthority.Grid, 29, 60, new Vector2Int(17, 0));
        }
        finally
        {
            restoredRuntime.Dispose();
            restoredEvents.Clear();
        }

        Grid sceneSeed = CreateInitialGrid();
        TestGridAuthority newRunAuthority = new(sceneSeed);
        GameEventBus newRunEvents = new();
        DungeonSpaceExpansionRuntime newRunRuntime =
            new(newRunEvents, newRunAuthority, newRunAuthority);
        try
        {
            Require(newRunRuntime.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult result,
                    out string failureReason),
                "Explicit new-run Tier-0 reconciliation failed: " + failureReason);
            Require(result.Changed
                    && result.AddedInteriorColumns == 2
                    && newRunAuthority.PublicationCount == 1,
                "Explicit new-run reconciliation did not publish exactly one +2 expansion.");
            RequireLayout(newRunAuthority.Grid, 29, 60, new Vector2Int(17, 0));
            Require(newRunRuntime.TryReconcileNewRunTierZero(
                    out DungeonSpaceExpansionResult repeated,
                    out failureReason)
                    && !repeated.Changed
                    && newRunAuthority.PublicationCount == 1,
                "Repeated new-run Tier-0 reconciliation was not idempotent: "
                + failureReason);
        }
        finally
        {
            newRunRuntime.Dispose();
            newRunEvents.Clear();
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
            Require(runtime.TryReconcileNewRunTierZero(out _, out string tierZeroFailure),
                "Direct-deep fixture could not reconcile Tier 0: " + tierZeroFailure);
            Publish(
                events,
                projects[DungeonSpaceExpansionCatalog.DeepMiningResearchId]);
            RequireLayout(authority.Grid, 87, 104, new Vector2Int(17, 0));
            Require(runtime.LastResult.Changed
                    && runtime.LastResult.AddedInteriorColumns == 58,
                "Direct deep-mining completion did not publish the exact 29-to-87 expansion.");

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
            Require(authority.PublicationCount == 2
                    && authority.CompletionCount == 2,
                $"Tier-0 plus direct deep expansion expected two publications; found {authority.PublicationCount}/{authority.CompletionCount}.");
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
        Grid grid = CreateTierZeroGrid();
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
        Grid grid = CreateTierZeroGrid();
        foreach (DungeonSpaceExpansionDefinition definition in
                 DungeonSpaceExpansionCatalog.All)
        {
            int targetEnd = 17 + definition.TargetInteriorColumns;
            int widthDelta = Mathf.Max(0, targetEnd - grid.width);
            Grid expanded = grid.TryExpandGrid(widthDelta, 0);
            Require(expanded != null,
                $"Could not allocate tier {definition.Tier} save fixture.");
            for (int x = 17 + (definition.Tier == 1
                         ? DungeonSpaceExpansionCatalog.InitialInteriorColumns
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
                && layout.ColumnCount == 87
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

    private static bool VerifyNewRunTierZeroRejectsNoncanonicalWidths()
    {
        foreach (int columns in new[] { 28, 30, 51 })
        {
            Grid original = CreateGridWithInteriorColumns(columns);
            TestGridAuthority authority = new(original);
            GameEventBus events = new();
            DungeonSpaceExpansionRuntime runtime = new(events, authority, authority);
            try
            {
                Require(!runtime.TryReconcileNewRunTierZero(
                        out _,
                        out string failureReason),
                    $"New-run Tier-0 reconciliation accepted noncanonical {columns}-column input.");
                Require(failureReason.Contains(
                        $"found {columns}",
                        StringComparison.Ordinal)
                    && ReferenceEquals(authority.Grid, original)
                    && authority.PublicationCount == 0
                    && authority.CompletionCount == 0,
                    $"Noncanonical {columns}-column input was not rejected atomically: {failureReason}");
            }
            finally
            {
                runtime.Dispose();
                events.Clear();
            }
        }
        return true;
    }

    private static bool VerifyGameplaySceneShaUnchanged()
    {
        const string expectedSha256 =
            "B390A975545B55D5AAE48C27514C889E3386BE372FD227F92E7572983E5643C8";
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string scenePath = Path.Combine(
            projectRoot,
            "Assets/Scenes/GameplayScene.unity");
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(scenePath);
        string actual = BitConverter.ToString(sha.ComputeHash(stream))
            .Replace("-", string.Empty);
        Require(string.Equals(actual, expectedSha256, StringComparison.Ordinal),
            $"GameplayScene SHA drifted: expected={expectedSha256}; actual={actual}.");
        return true;
    }

    private static bool VerifySaveResearchLayoutAuthority()
    {
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                Array.Empty<string>()) == 29,
            "A save with no expansion research did not resolve to 29 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[] { DungeonSpaceExpansionCatalog.QuarryResearchId }) == 51,
            "A quarry-completed save did not resolve to 51 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[] { DungeonSpaceExpansionCatalog.StonecuttingResearchId }) == 71,
            "A stonecutting-only restored set did not resolve to 71 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[] { DungeonSpaceExpansionCatalog.DeepMiningResearchId }) == 87,
            "A deep-mining-only restored set did not resolve to 87 columns.");
        Require(
            DungeonSpaceExpansionCatalog.ResolveExpectedInteriorColumns(
                new[]
                {
                    DungeonSpaceExpansionCatalog.DeepMiningResearchId,
                    DungeonSpaceExpansionCatalog.QuarryResearchId,
                    DungeonSpaceExpansionCatalog.StonecuttingResearchId
                }) == 87,
            "The highest completed mining expansion was not authoritative.");
        return true;
    }

    private static bool VerifyExpansionPreflightContract()
    {
        Require(typeof(IDungeonCapturedSavePreflightValidator).IsAssignableFrom(
                typeof(DungeonAggregateReferencePreflight)),
            "Dungeon aggregate expansion validation is missing from the captured-save boundary.");

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string registrationSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonSaveRegistration.cs"));
        Require(registrationSource.Contains(
                ".As<IDungeonCapturedSavePreflightValidator>();",
                StringComparison.Ordinal),
            "Dungeon aggregate preflight is not registered for captured saves.");

        VerifyExpansionPreflight(
            completedResearchIds: Array.Empty<string>(),
            interiorColumns: 29,
            expectedSuccess: true,
            expectedErrorFragment: string.Empty);
        VerifyExpansionPreflight(
            completedResearchIds: Array.Empty<string>(),
            interiorColumns: 27,
            expectedSuccess: false,
            expectedErrorFragment: "expects 29 interior columns, save contains 27");
        VerifyExpansionPreflight(
            completedResearchIds: new[]
            {
                DungeonSpaceExpansionCatalog.QuarryResearchId
            },
            interiorColumns: 29,
            expectedSuccess: false,
            expectedErrorFragment: "expects 51 interior columns, save contains 29");
        VerifyExpansionPreflight(
            completedResearchIds: Array.Empty<string>(),
            interiorColumns: 51,
            expectedSuccess: false,
            expectedErrorFragment: "expects 29 interior columns, save contains 51");
        return true;
    }

    private static void VerifyExpansionPreflight(
        IReadOnlyList<string> completedResearchIds,
        int interiorColumns,
        bool expectedSuccess,
        string expectedErrorFragment)
    {
        DungeonResearchSaveData research = new()
        {
            completedProjectIds = completedResearchIds.ToList()
        };
        Grid grid = CreateGridWithInteriorColumns(interiorColumns);
        ModularFacilityWorldSaveData facilities = new()
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
        DungeonGameRestoreReport report = new();
        MethodInfo validator = typeof(DungeonAggregateReferencePreflight)
            .GetMethod(
                "ValidateDungeonExpansionResearch",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Dungeon expansion aggregate preflight entry point is unavailable.");
        validator.Invoke(null, new object[] { research, facilities, report });
        Require(report.Success == expectedSuccess,
            $"Expansion preflight result mismatch for {interiorColumns} columns: "
            + string.Join(" | ", report.Errors));
        if (!expectedSuccess)
        {
            Require(report.Errors.Any(error => error.Contains(
                    expectedErrorFragment,
                    StringComparison.Ordinal)),
                $"Expansion preflight did not report '{expectedErrorFragment}': "
                + string.Join(" | ", report.Errors));
        }
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
        return CreateGridWithInteriorColumns(
            DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns);
    }

    private static Grid CreateGridWithInteriorColumns(int interiorColumns)
    {
        int width = Mathf.Max(60, 17 + interiorColumns);
        Grid grid = new Grid(width, DungeonSpaceExpansionCatalog.SupportedGridHeight);
        foreach (GridCell cell in grid.GetCells())
        {
            grid.SetAreaType(cell.Position, GridCellAreaType.BlockedExterior);
        }
        for (int x = 17; x < 17 + interiorColumns; x++)
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

    private static Grid CreateTierZeroGrid()
    {
        Grid initial = CreateInitialGrid();
        TestGridAuthority authority = new(initial);
        GameEventBus events = new();
        DungeonSpaceExpansionRuntime runtime = new(events, authority, authority);
        try
        {
            Require(runtime.TryReconcileNewRunTierZero(out _, out string failureReason),
                "Tier-0 fixture reconciliation failed: " + failureReason);
            return authority.Grid;
        }
        finally
        {
            runtime.Dispose();
            events.Clear();
        }
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
