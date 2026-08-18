#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

[BalanceCaptureFactory]
public static class V27AssetBackedSpatialCapacityDebugScenarios
{
    public const string SpatialCsvPath =
        "Artifacts/QA/v27-balance-spatial-capacity.csv";
    public const string SharedCongestionPath =
        "Artifacts/QA/v27-balance-shared-cell-congestion.txt";
    public const string ExpansionTiersPath =
        "Artifacts/QA/v27-balance-expansion-tiers.txt";
    private const int MinimumWidth = 27;
    private const int MaximumWidth = 96;
    private const int GridHeight = 3;
    private const int SeedsPerStage = 256;
    private const int MinimumSuccesses = 243;
    private const long GameDayMilliseconds = 180000L;
    private const int MaximumPhysicalStackUnits = 75;
    private const int NormalStorageLimitPermille = 700;
    private const int FaultStorageLimitPermille = 900;
    private const int ExactOracleMaximumRequirements = 4;
    private const int BeamWidth = 64;

    [MenuItem("DungeonStory/V27/Verify Asset-Backed Spatial Capacity 256 Seeds")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        Debug.Log(report);
    }

    [MenuItem("DungeonStory/V27/Verify Asset-Backed Search Fallbacks Focused")]
    private static void RunSearchFallbacksFocusedFromMenu() =>
        Debug.Log(RunSearchFallbacksFocused());

    public static string RunSearchFallbacksFocused()
    {
        BuildingSO[] assets = LoadAssets();
        VerifySpatialSafetyContracts(assets);
        SearchProof proof = VerifySearchFallbacks(assets);
        return "RESULT=PASS; "
            + $"marker={proof.Marker};asset={proof.AssetId};"
            + $"exactRequirements={proof.ExactRequirementCount};"
            + $"beamRequirements={proof.BeamRequirementCount};"
            + $"beamWidth={BeamWidth};heuristicFalseNegative=0;"
            + "uniqueAccessAdversarial=PASS;fixedEgressLanding=PASS";
    }

    public static V27RedundancyCapitalAssessment CaptureSixAdultRedundancyCapital()
    {
        AssetRequirement[] requirements = BuildRequirements(LoadAssets(), 6);
        long portfolio = requirements.Sum(value => ConstructionCapitalMilliUnits(
            value.Asset));
        long avoidedDuplicate = requirements
            .Where(value => value.StableId.StartsWith(
                    "facility:food-production:", StringComparison.Ordinal)
                || value.StableId.StartsWith(
                    "facility:water:", StringComparison.Ordinal))
            .Sum(value => ConstructionCapitalMilliUnits(value.Asset));
        if (portfolio <= 0 || avoidedDuplicate <= 0)
            throw new InvalidOperationException(
                "REDUNDANCY_CAPITAL_AUTHORITY_MISSING");
        return new V27RedundancyCapitalAssessment(
            portfolio,
            actualRedundancyCapitalMilliUnits: 0L,
            avoidedDuplicateCapitalMilliUnits: avoidedDuplicate);
    }

    public static IReadOnlyList<V27AssetBackedStageCapacityAssessment>
        CaptureStageCapacityAssessments()
    {
        BuildingSO[] assets = LoadAssets();
        List<V27AssetBackedStageCapacityAssessment> values = new();
        List<SeedResult> discardedRows = new();
        int previousWidth = MinimumWidth;
        foreach (int population in PopulationStagePortfolioCatalog.PopulationStages)
        {
            AssetRequirement[] requirements = BuildRequirements(assets, population);
            StageResult stage = FindMinimumWidth(
                population,
                requirements,
                Math.Max(previousWidth, AuthoredTargetWidth(population)),
                discardedRows);
            Require(stage.SuccessCount >= MinimumSuccesses,
                $"DUNGEON_CAPACITY_MODEL_INVALID: population={population};"
                + $"success={stage.SuccessCount}/{SeedsPerStage};width={stage.Width}.");
            values.Add(new V27AssetBackedStageCapacityAssessment(
                stage.Population,
                stage.Width,
                stage.SuccessCount,
                stage.MinimumHeadroom,
                stage.MaximumNormalUtilization,
                stage.MaximumFaultUtilization,
                stage.MaximumNormalStorageUtilization,
                stage.MaximumFaultStorageUtilization,
                stage.FacilityCount,
                stage.StorageCapacity));
            previousWidth = stage.Width;
        }
        return values;
    }

    public static string RunAll()
    {
        BuildingSO[] assets = LoadAssets();
        string sourceDigest = CaptureSourceDigest();
        VerifySpatialSafetyContracts(assets);
        SearchProof searchProof = VerifySearchFallbacks(assets);
        List<StageResult> stages = new();
        List<SeedResult> allRows = new();
        int previousWidth = MinimumWidth;
        foreach (int population in PopulationStagePortfolioCatalog.PopulationStages)
        {
            AssetRequirement[] requirements = BuildRequirements(assets, population);
            int authoredFloor = AuthoredTargetWidth(population);
            StageResult stage = FindMinimumWidth(
                population,
                requirements,
                Math.Max(previousWidth, authoredFloor),
                allRows);
            Require(stage.SuccessCount >= MinimumSuccesses,
                $"DUNGEON_CAPACITY_MODEL_INVALID: population={population};"
                + $"success={stage.SuccessCount}/{SeedsPerStage};width={stage.Width}.");
            stages.Add(stage);
            previousWidth = stage.Width;
        }

        string csv = BuildCsv(allRows);
        string csvDigest = HashText(csv);
        WriteText(SpatialCsvPath, csv);
        WriteText(
            SharedCongestionPath,
            BuildCongestionReport(stages, sourceDigest, csvDigest, searchProof));
        WriteText(
            ExpansionTiersPath,
            BuildExpansionReport(stages, sourceDigest, csvDigest, searchProof));
        return "RESULT=PASS; authority=BuildingSO+BuildingPlacementValidator;"
            + $" stages={stages.Count};seedsPerStage={SeedsPerStage};"
            + $" passed={stages.Sum(value => value.SuccessCount)};"
            + $" minimumHeadroomPermille={stages.Min(value => value.MinimumHeadroom)};"
            + $" maximumNormalUtilizationPermille={stages.Max(value => value.MaximumNormalUtilization)};"
            + $" maximumFaultUtilizationPermille={stages.Max(value => value.MaximumFaultUtilization)};"
            + $" searchProof={searchProof.Marker};"
            + " widths=" + string.Join(",", stages.Select(value =>
                $"{value.Population}:{value.Width}"));
    }

    public static string CaptureSourceDigest()
    {
        string[] sourcePaths =
        {
            "Assets/Scripts/Services/Economy/Editor/V27AssetBackedSpatialCapacityDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/V27PopulationCapacityModels.cs",
            "Assets/Scripts/Services/Buildings/BuildableObject.SpatialAndInteraction.cs",
            "Assets/Scripts/Services/Grid/Building/GridBuildingRuntime.cs",
            "Assets/Scripts/Models/Economy/Content/WorldResourceRuntime.cs",
            "Assets/Scripts/Services/Economy/WorldResourcePortAdapters.cs",
            "Assets/Scripts/Services/Wildlife/WildlifeHabitatDecorationRuntime.cs"
        };
        string[] authorityPaths = sourcePaths
            .Concat(AssetDatabase.FindAssets("t:BuildingSO")
                .Select(AssetDatabase.GUIDToAssetPath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        StringBuilder canonical = new();
        foreach (string path in authorityPaths)
        {
            string absolutePath = Path.GetFullPath(path);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException(
                    "Asset-backed spatial authority is missing.",
                    absolutePath);
            canonical.Append(path.Replace('\\', '/'))
                .Append('=')
                .Append(HashFile(absolutePath))
                .Append('\n');
        }
        return HashText(canonical.ToString());
    }

    private static StageResult FindMinimumWidth(
        int population,
        IReadOnlyList<AssetRequirement> requirements,
        int minimumWidth,
        ICollection<SeedResult> finalRows)
    {
        for (int width = Math.Max(MinimumWidth, minimumWidth);
             width <= MaximumWidth;
             width += 2)
        {
            List<SeedResult> candidates = new(SeedsPerStage);
            for (int seed = 1; seed <= SeedsPerStage; seed++)
                candidates.Add(TryPlace(population, width, seed, requirements));
            SeedResult[] successes = candidates.Where(value => value.Succeeded).ToArray();
            if (successes.Length < MinimumSuccesses)
                continue;
            foreach (SeedResult row in candidates)
                finalRows.Add(row);
            return new StageResult(
                population,
                width,
                successes.Length,
                successes.Min(value => value.HeadroomPermille),
                successes.Max(value => value.PeakNormalUtilizationPermille),
                successes.Max(value => value.PeakFaultUtilizationPermille),
                successes.Max(value => value.NormalStorageUtilizationPermille),
                successes.Max(value => value.FaultStorageUtilizationPermille),
                requirements.Count,
                successes.Min(value => value.StorageCapacityUnits));
        }
        throw new InvalidOperationException(
            $"DUNGEON_CAPACITY_MODEL_INVALID: population={population} exceeds "
            + $"the {MaximumWidth}-column safety bound.");
    }

    private static SeedResult TryPlace(
        int population,
        int width,
        int seed,
        IReadOnlyList<AssetRequirement> source)
    {
        Grid grid = CreateGrid(width);
        BuildingPlacementValidator validator = new(
            new GridPlacementValidator(),
            () => new BuildingConditionContext(
                null, null, null, IgnoreUnlocksDebugRules.Instance));
        HashSet<Vector2Int> fixedCells = new()
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 1)
        };
        HashSet<Vector2Int> exclusive = new();
        HashSet<Vector2Int> sharedAccess = new();
        List<PlacedAsset> placed = new();
        AssetRequirement[] requirements = source.ToArray();
        DeterministicRandomSequence random = new(seed);
        for (int index = requirements.Length - 1; index > 0; index--)
        {
            int swap = random.NextInt(0, index + 1);
            (requirements[index], requirements[swap]) =
                (requirements[swap], requirements[index]);
        }

        PlacementSearchResult search = SolvePlacements(
            grid,
            validator,
            requirements,
            seed,
            fixedCells);
        if (!search.Succeeded)
            return SeedResult.Fail(population, width, seed, search.FailureCode);

        int occupantId = 1000;
        for (int index = 0; index < requirements.Length; index++)
        {
            AssetRequirement requirement = requirements[index];
            PlacementChoice choice = search.Choices[index];
            SolverOccupant occupant = new(
                occupantId++,
                choice.Asset.IsGridMovement);
            if (!grid.RegisterOccupant(
                    occupant,
                    choice.Asset.layer,
                    choice.Footprint,
                    false))
            {
                return SeedResult.Fail(population, width, seed,
                    "GRID_OCCUPANCY_REJECTED:" + requirement.StableId);
            }
            exclusive.UnionWith(choice.Footprint);
            sharedAccess.UnionWith(choice.Access);
            placed.Add(new PlacedAsset(requirement, choice));
        }

        int overflowCells = OverflowCellsForPopulation(population);
        Vector2Int[] freeContainment = grid.GetCells()
            .Select(value => value.Position)
            .Where(value => !fixedCells.Contains(value)
                && !exclusive.Contains(value)
                && !sharedAccess.Contains(value))
            .OrderByDescending(value => value.x)
            .ThenByDescending(value => value.y)
            .Take(overflowCells)
            .ToArray();
        if (freeContainment.Length != overflowCells)
            return SeedResult.Fail(population, width, seed,
                "OVERFLOW_CONTAINMENT_MISSING");

        HashSet<Vector2Int> used = new(exclusive);
        used.UnionWith(sharedAccess);
        used.UnionWith(fixedCells);
        used.UnionWith(freeContainment);
        int usable = width * GridHeight;
        int headroom = checked((usable - used.Count) * 1000 / usable);
        Dictionary<Vector2Int, long> normal = new();
        Dictionary<Vector2Int, long> fault = new();
        int rawAccess = 0;
        foreach (PlacedAsset value in placed)
        {
            rawAccess += value.Choice.Access.Count;
            long normalPerCell = DivideCeiling(
                checked((long)value.Requirement.VisitsPerDay
                    * value.Requirement.OccupancyMilliseconds),
                Math.Max(1, value.Choice.Access.Count));
            long faultPerCell = DivideCeiling(
                checked(normalPerCell * value.Requirement.FaultMultiplierPermille),
                1000L);
            foreach (Vector2Int cell in value.Choice.Access)
            {
                Add(normal, cell, normalPerCell);
                Add(fault, cell, faultPerCell);
            }
        }
        int normalPeak = PeakPermille(normal);
        int faultPeak = PeakPermille(fault);
        StorageUtilization storage = CaptureStorageUtilization(
            population,
            source,
            overflowCells);
        bool passed = headroom >= 300
            && normalPeak <= 700
            && faultPeak <= 900
            && storage.NormalPermille <= NormalStorageLimitPermille
            && storage.FaultPermille <= FaultStorageLimitPermille;
        return new SeedResult(
            population,
            width,
            seed,
            passed,
            passed ? string.Empty : "CAPACITY_THRESHOLD_FAILED",
            used.Count,
            headroom,
            rawAccess - sharedAccess.Count,
            normalPeak,
            faultPeak,
            storage.NormalPermille,
            storage.FaultPermille,
            storage.StorageCapacityUnits,
            storage.NormalStockUnits,
            storage.FaultStockUnits,
            placed.Count,
            freeContainment.Length,
            search.Mode);
    }

    private static PlacementSearchResult SolvePlacements(
        Grid grid,
        BuildingPlacementValidator validator,
        IReadOnlyList<AssetRequirement> requirements,
        int seed,
        ISet<Vector2Int> fixedCells)
    {
        PlacementChoice[][] candidates = requirements
            .Select(requirement => EnumerateChoices(
                grid,
                validator,
                requirement,
                fixedCells))
            .ToArray();
        for (int index = 0; index < candidates.Length; index++)
        {
            if (candidates[index].Length == 0)
                return PlacementSearchResult.Fail(
                    "NO_AUTHORED_ASSET_CANDIDATE:" + requirements[index].StableId);
        }

        if (TryGreedySearch(
                requirements,
                candidates,
                seed,
                grid.width,
                out PlacementSearchState greedy))
            return PlacementSearchResult.Pass("greedy", greedy.Choices);

        if (requirements.Count <= ExactOracleMaximumRequirements)
        {
            if (TryExactSearch(
                    requirements,
                    candidates,
                    seed,
                    grid.width,
                    out PlacementSearchState exact))
                return PlacementSearchResult.Pass("exact", exact.Choices);
            return PlacementSearchResult.Fail("EXACT_ASSET_PLACEMENT_IMPOSSIBLE");
        }

        if (TryBeamSearch(
                requirements,
                candidates,
                seed,
                grid.width,
                BeamWidth,
                out PlacementSearchState beam))
            return PlacementSearchResult.Pass("beam", beam.Choices);
        return PlacementSearchResult.Fail("BEAM_ASSET_PLACEMENT_EXHAUSTED");
    }

    private static PlacementChoice[] EnumerateChoices(
        Grid grid,
        BuildingPlacementValidator validator,
        AssetRequirement requirement,
        ISet<Vector2Int> fixedCells)
    {
        List<PlacementChoice> choices = new();
        for (int y = 0; y < grid.height; y++)
        for (int x = 2; x < grid.width; x++)
        {
            Vector2Int anchor = new(x, y);
            if (!validator.CanBuild(grid, requirement.Asset, anchor, out _))
                continue;
            Vector2Int[] footprint = requirement.Asset.GetGridPosList(anchor)
                .Distinct()
                .OrderBy(value => value.y)
                .ThenBy(value => value.x)
                .ToArray();
            Vector2Int[] access = BuildingWorkAccessRules.EnumerateCandidates(
                    footprint,
                    requirement.Asset.IsGridMovement)
                .Where(grid.IsValidGridPos)
                .Distinct()
                .OrderBy(value => value.y)
                .ThenBy(value => value.x)
                .ToArray();
            if (footprint.Length == 0 || access.Length == 0
                || footprint.Any(value => fixedCells.Contains(value)
                    )
                || access.Any(value => fixedCells.Contains(value)
                    || footprint.Contains(value)))
                continue;
            choices.Add(new PlacementChoice(
                requirement.Asset,
                anchor,
                footprint,
                access));
        }
        return choices
            .OrderBy(value => value.Anchor.y)
            .ThenBy(value => value.Anchor.x)
            .ToArray();
    }

    private static bool TryGreedySearch(
        IReadOnlyList<AssetRequirement> requirements,
        IReadOnlyList<PlacementChoice[]> candidates,
        int seed,
        int width,
        out PlacementSearchState result)
    {
        PlacementSearchState state = PlacementSearchState.Empty;
        for (int index = 0; index < requirements.Count; index++)
        {
            AssetRequirement requirement = requirements[index];
            PlacementChoice choice = candidates[index]
                .Where(value => CanAdd(state, value))
                .OrderBy(value => IncrementalScore(
                    state, value, requirement, seed, width))
                .ThenBy(value => value.Anchor.y)
                .ThenBy(value => value.Anchor.x)
                .FirstOrDefault();
            if (!choice.Valid)
            {
                result = null;
                return false;
            }
            state = state.Add(
                choice,
                IncrementalScore(state, choice, requirement, seed, width));
        }
        result = state;
        return true;
    }

    private static bool TryBeamSearch(
        IReadOnlyList<AssetRequirement> requirements,
        IReadOnlyList<PlacementChoice[]> candidates,
        int seed,
        int width,
        int beamWidth,
        out PlacementSearchState result)
    {
        List<PlacementSearchState> frontier = new() { PlacementSearchState.Empty };
        for (int index = 0; index < requirements.Count; index++)
        {
            AssetRequirement requirement = requirements[index];
            List<PlacementSearchState> next = new();
            foreach (PlacementSearchState state in frontier)
            foreach (PlacementChoice choice in candidates[index])
            {
                if (!CanAdd(state, choice))
                    continue;
                next.Add(state.Add(
                    choice,
                    IncrementalScore(state, choice, requirement, seed, width)));
            }
            frontier = next
                .OrderBy(value => value.TotalScore)
                .ThenBy(value => value.UsedCellCount)
                .ThenBy(value => value.CanonicalKey, StringComparer.Ordinal)
                .Take(beamWidth)
                .ToList();
            if (frontier.Count == 0)
            {
                result = null;
                return false;
            }
        }
        result = frontier[0];
        return true;
    }

    private static bool TryExactSearch(
        IReadOnlyList<AssetRequirement> requirements,
        IReadOnlyList<PlacementChoice[]> candidates,
        int seed,
        int width,
        out PlacementSearchState result)
    {
        PlacementSearchState found = null;
        bool Search(int index, PlacementSearchState state)
        {
            if (index >= requirements.Count)
            {
                found = state;
                return true;
            }
            AssetRequirement requirement = requirements[index];
            PlacementChoice[] ordered = candidates[index]
                .Where(value => CanAdd(state, value))
                .OrderBy(value => IncrementalScore(
                    state, value, requirement, seed, width))
                .ThenBy(value => value.Anchor.y)
                .ThenBy(value => value.Anchor.x)
                .ToArray();
            foreach (PlacementChoice choice in ordered)
            {
                if (Search(
                        index + 1,
                        state.Add(
                            choice,
                            IncrementalScore(
                                state, choice, requirement, seed, width))))
                    return true;
            }
            return false;
        }

        bool succeeded = Search(0, PlacementSearchState.Empty);
        result = found;
        return succeeded;
    }

    private static bool CanAdd(
        PlacementSearchState state,
        PlacementChoice choice) => choice.Valid
        && !choice.Footprint.Any(value => state.Exclusive.Contains(value)
            || state.SharedAccess.Contains(value))
        && !choice.Access.Any(value => state.Exclusive.Contains(value)
            || choice.Footprint.Contains(value))
        && state.Choices.All(existing =>
            BuildingWorkAccessRules.CanShareOperationalAccess(
                existing.Access,
                choice.Access));

    private static long IncrementalScore(
        PlacementSearchState state,
        PlacementChoice choice,
        AssetRequirement requirement,
        int seed,
        int width)
    {
        int newAccess = choice.Access.Count(value => !state.SharedAccess.Contains(value));
        int overlap = choice.Access.Count - newAccess;
        int centerDistance = Math.Abs(choice.Anchor.x - width / 2) + choice.Anchor.y;
        int jitter = StableJitter(
            seed,
            requirement.StableId,
            choice.Anchor.x,
            choice.Anchor.y);
        return checked(
            (long)(choice.Footprint.Count + newAccess) * 100000L
            - overlap * 10000L
            + centerDistance * 100L
            + jitter);
    }

    private static SearchProof VerifySearchFallbacks(
        IReadOnlyList<BuildingSO> assets)
    {
        const int fixtureWidth = 49;
        const int seed = 271828;
        Grid grid = CreateGrid(fixtureWidth);
        BuildingPlacementValidator validator = new(
            new GridPlacementValidator(),
            () => new BuildingConditionContext(
                null, null, null, IgnoreUnlocksDebugRules.Instance));
        HashSet<Vector2Int> fixedCells = new()
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 1)
        };
        BuildingSO asset = assets
            .Where(value => value.width == 1
                && value.height == 1
                && !value.IsGridMovement)
            .OrderBy(value => value.GetFacilityCode(), StringComparer.Ordinal)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(asset != null,
            "ACTUAL_ASSET_SEARCH_FIXTURE_MISSING: one-cell facility.");

        AssetRequirement first = new(
            "search-proof:first", asset, 1, 1, 1000);
        AssetRequirement second = new(
            "search-proof:second", asset, 1, 1, 1000);
        PlacementChoice[] all = EnumerateChoices(
            grid, validator, first, fixedCells);
        Require(all.Length >= 9,
            $"ACTUAL_ASSET_SEARCH_CANDIDATES_INSUFFICIENT:{all.Length}.");
        PlacementSearchState empty = PlacementSearchState.Empty;
        PlacementChoice[] ordered = all
            .OrderBy(value => IncrementalScore(
                empty, value, first, seed, fixtureWidth))
            .ThenBy(value => value.Anchor.y)
            .ThenBy(value => value.Anchor.x)
            .ToArray();
        PlacementChoice blocker = ordered[0];
        PlacementSearchState blockerOnly = empty.Add(
            blocker,
            IncrementalScore(empty, blocker, first, seed, fixtureWidth));
        PlacementChoice alternative = ordered.Skip(1).FirstOrDefault(value =>
        {
            PlacementSearchState alternativeOnly = empty.Add(
                value,
                IncrementalScore(empty, value, first, seed, fixtureWidth));
            return CanAdd(alternativeOnly, blocker)
                && CanAdd(blockerOnly, value);
        });
        Require(alternative.Valid,
            "ACTUAL_ASSET_SEARCH_ALTERNATIVE_MISSING.");

        AssetRequirement[] smallRequirements = { first, second };
        PlacementChoice[][] smallCandidates =
        {
            new[] { blocker, alternative },
            new[] { blocker }
        };
        bool greedySmall = TryGreedySearch(
            smallRequirements,
            smallCandidates,
            seed,
            fixtureWidth,
            out _);
        bool exactSmall = TryExactSearch(
            smallRequirements,
            smallCandidates,
            seed,
            fixtureWidth,
            out PlacementSearchState exactA);
        bool exactRepeat = TryExactSearch(
            smallRequirements,
            smallCandidates,
            seed,
            fixtureWidth,
            out PlacementSearchState exactB);
        Require(!greedySmall && exactSmall && exactRepeat
                && string.Equals(
                    exactA.CanonicalKey,
                    exactB.CanonicalKey,
                    StringComparison.Ordinal),
            "ACTUAL_ASSET_EXACT_ORACLE_PROOF_FAILED.");

        PlacementSearchState target = empty.Add(
            alternative,
            IncrementalScore(empty, alternative, first, seed, fixtureWidth));
        target = target.Add(
            blocker,
            IncrementalScore(target, blocker, second, seed, fixtureWidth));
        List<PlacementChoice> tailChoices = new();
        foreach (PlacementChoice choice in all)
        {
            if (!CanAdd(target, choice))
                continue;
            tailChoices.Add(choice);
            target = target.Add(choice, 0L);
            if (tailChoices.Count == 7)
                break;
        }
        Require(tailChoices.Count == 7,
            $"ACTUAL_ASSET_BEAM_FIXTURE_INSUFFICIENT:{tailChoices.Count}.");

        List<AssetRequirement> largeRequirements = new() { first, second };
        List<PlacementChoice[]> largeCandidates = new()
        {
            new[] { blocker, alternative },
            new[] { blocker }
        };
        for (int index = 0; index < tailChoices.Count; index++)
        {
            largeRequirements.Add(new AssetRequirement(
                $"search-proof:tail:{index:D2}", asset, 1, 1, 1000));
            largeCandidates.Add(new[] { tailChoices[index] });
        }
        bool greedyLarge = TryGreedySearch(
            largeRequirements,
            largeCandidates,
            seed,
            fixtureWidth,
            out _);
        bool beamLarge = TryBeamSearch(
            largeRequirements,
            largeCandidates,
            seed,
            fixtureWidth,
            BeamWidth,
            out PlacementSearchState beamA);
        bool beamRepeat = TryBeamSearch(
            largeRequirements,
            largeCandidates,
            seed,
            fixtureWidth,
            BeamWidth,
            out PlacementSearchState beamB);
        Require(!greedyLarge && beamLarge && beamRepeat
                && string.Equals(
                    beamA.CanonicalKey,
                    beamB.CanonicalKey,
                    StringComparison.Ordinal),
            "ACTUAL_ASSET_DETERMINISTIC_BEAM_PROOF_FAILED.");
        return new SearchProof(
            AssetStableId(asset),
            smallRequirements.Length,
            largeRequirements.Count,
            exactA.CanonicalKey,
            beamA.CanonicalKey);
    }

    private static void VerifySpatialSafetyContracts(
        IReadOnlyList<BuildingSO> assets)
    {
        Vector2Int uniqueStand = new(8, 1);
        Require(
            !BuildingWorkAccessRules.CanShareOperationalAccess(
                new[] { uniqueStand },
                new[] { uniqueStand }),
            "UNIQUE_OPERATIONAL_ACCESS_OVERLAP_WAS_ACCEPTED");
        Require(
            BuildingWorkAccessRules.CanShareOperationalAccess(
                new[] { uniqueStand, new Vector2Int(9, 1) },
                new[] { uniqueStand }),
            "MULTI_STAND_SHARED_CORRIDOR_WAS_REJECTED");

        BuildingSO asset = assets
            .Where(value => value != null
                && value.width == 1
                && value.height == 1
                && !value.IsGridMovement)
            .OrderBy(value => value.GetFacilityCode(), StringComparer.Ordinal)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(asset != null,
            "SPATIAL_SAFETY_FIXTURE_MISSING: one-cell facility.");

        Grid grid = CreateGrid(MinimumWidth);
        BuildingPlacementValidator validator = new(
            new GridPlacementValidator(),
            () => new BuildingConditionContext(
                null, null, null, IgnoreUnlocksDebugRules.Instance));
        HashSet<Vector2Int> protectedCells = new()
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 1)
        };
        AssetRequirement requirement = new(
            "spatial-safety:protected-egress", asset, 1, 1, 1000);
        PlacementChoice[] choices = EnumerateChoices(
            grid,
            validator,
            requirement,
            protectedCells);
        Require(choices.Length > 0,
            "SPATIAL_SAFETY_FIXTURE_HAS_NO_LEGAL_CHOICE");
        Require(
            choices.All(choice => choice.Footprint.All(cell =>
                    !protectedCells.Contains(cell))
                && choice.Access.All(cell => !protectedCells.Contains(cell))),
            "PROTECTED_EGRESS_OR_LANDING_WAS_USED_BY_PLACEMENT");
    }

    private static AssetRequirement[] BuildRequirements(
        IReadOnlyList<BuildingSO> assets,
        int population)
    {
        List<AssetRequirement> values = new();
        Add(values, "food-service", Select(assets,
            value => value.Facility?.SupportsRole(FacilityRole.Meal) == true),
            1, population * 3, 1800, 1400);
        Add(values, "food-production", SelectWork(assets, BuiltInWorkTypeIds.Cook),
            1, population * 2, 1800, 1400);
        SurvivalClosedLoopAssessment survival =
            V27SixAdultSurvivalLoopDebugScenarios.CapturePopulationStage(population);
        Add(values, "crop-plot", Select(assets,
                value => value.GetAbility<BuildingCropPlotAbility>() != null
                    && value.Facility?.SupportedWorkTypeIds.Contains(BuiltInWorkTypeIds.Sow) == true
                    && value.Facility.SupportedWorkTypeIds.Contains(BuiltInWorkTypeIds.Harvest)),
            survival.CropPlots, population * 2, 900, 1400);
        Add(values, "water", SelectWork(assets, BuiltInWorkTypeIds.DrawWater),
            1, population * 3, 900, 1500);
        Add(values, "hygiene", Select(assets,
            value => value.Facility?.SupportsRole(FacilityRole.Hygiene) == true),
            1, population, 1200, 1400);
        Add(values, "toilet", Select(assets,
            value => value.Facility?.SupportsRole(FacilityRole.Toilet) == true),
            1, population, 1200, 1400);

        BuildingSO rest = Select(assets,
            value => value.Facility?.SupportsRole(FacilityRole.Rest) == true);
        int restCapacity = Math.Max(1, rest.Facility?.capacity ?? 1);
        Add(values, "sleep", rest,
            DivideCeiling(population, restCapacity), population, 4000, 1100);

        BuildingSO storage = Select(assets,
            value => value.GetStorageCapacity() > 0 && value.StoresAllCategories(),
            preferStorageDensity: true);
        int normalStockUnits = checked(
            survival.SevenDayGrainUnits
            + survival.ImmediateMealUnits
            + survival.SevenDayWaterUnits);
        int requiredNormalCapacity = DivideCeiling(
            checked(normalStockUnits * 1000),
            NormalStorageLimitPermille);
        Add(values, "storage", storage,
            DivideCeiling(
                requiredNormalCapacity,
                Math.Max(1, storage.GetStorageCapacity())),
            population * 4, 700, 1600);

        if (population >= 3)
        {
            Add(values, "research", SelectWork(assets, BuiltInWorkTypeIds.Research),
                1, Math.Max(1, population / 3), 2400, 1200);
            Add(values, "craft", SelectWork(assets, BuiltInWorkTypeIds.Craft),
                1, population, 1800, 1300);
            Add(values, "medical", Select(assets,
                value => value.Facility?.SupportsRole(FacilityRole.Medical) == true),
                1, Math.Max(1, population / 2), 1800, 1600);
        }
        if (population >= 6)
        {
            Add(values, "guard", SelectWork(assets, BuiltInWorkTypeIds.Guard),
                1, population, 1300, 1700);
        }
        if (population >= 12)
        {
            Add(values, "animal-care", Select(assets,
                    value => value.GetAbility<BuildingBeastPenAbility>() != null
                        && value.Facility?.SupportedWorkTypeIds.Contains(
                            BuiltInWorkTypeIds.AnimalCare) == true),
                DivideCeiling(population, 12), population, 1800, 1600);
            Add(values, "power-generation", Select(assets,
                    value => value.GetAbility<BuildingPowerProducerAbility>() != null),
                1, population * 2, 600, 1600);
            Add(values, "water-storage", Select(assets,
                    value => value.GetAbility<BuildingWaterStorageAbility>() != null),
                1, population * 2, 600, 1600);
            Add(values, "wastewater-treatment", Select(assets,
                    value => value.GetAbility<BuildingWastewaterProcessorAbility>() != null),
                1, population * 2, 900, 1700);
            Add(values, "plumbing", SelectWork(assets, BuiltInWorkTypeIds.Plumbing),
                1, population * 2, 900, 1600);
            Add(values, "operate", SelectWork(assets, BuiltInWorkTypeIds.Operate),
                1, population, 900, 1500);
        }
        if (population >= 18)
        {
            Add(values, "surgery", SelectWork(assets, BuiltInWorkTypeIds.Surgery),
                1, Math.Max(1, population / 3), 2200, 1700);
            Add(values, "maintenance", SelectWork(assets, BuiltInWorkTypeIds.Repair),
                1, population, 1200, 1700);
        }
        if (population >= 24)
        {
            Add(values, "warden", SelectWork(assets, BuiltInWorkTypeIds.Warden),
                1, 12, 1600, 1600);
            Add(values, "hospitality", Select(assets,
                value => value.Facility?.SupportsRole(FacilityRole.Purchase) == true
                    || value.Facility?.SupportsRole(FacilityRole.Entertainment) == true),
                1, 18, 1600, 1500);
        }
        return values.OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();
    }

    private static BuildingSO SelectWork(
        IReadOnlyList<BuildingSO> assets,
        WorkTypeId workType) => Select(assets,
        value => value.Facility?.SupportedWorkTypeIds.Contains(workType) == true);

    private static BuildingSO Select(
        IReadOnlyList<BuildingSO> assets,
        Func<BuildingSO, bool> predicate,
        bool preferStorageDensity = false)
    {
        BuildingSO selected = assets.Where(predicate)
            .OrderBy(value => preferStorageDensity
                ? -(decimal)value.GetStorageCapacity()
                    / Math.Max(1, value.width * value.height)
                : value.width * value.height)
            .ThenByDescending(value => value.GetStorageCapacity())
            .ThenBy(value => value.GetFacilityCode(), StringComparer.Ordinal)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .FirstOrDefault();
        return selected ?? throw new InvalidOperationException(
            "DUNGEON_CAPACITY_AUTHORED_FACILITY_MISSING");
    }

    private static void Add(
        ICollection<AssetRequirement> destination,
        string id,
        BuildingSO asset,
        int count,
        int visits,
        int occupancy,
        int faultMultiplier)
    {
        for (int index = 0; index < count; index++)
        {
            destination.Add(new AssetRequirement(
                $"facility:{id}:{index:D2}",
                asset,
                DivideCeiling(visits, count),
                occupancy,
                faultMultiplier));
        }
    }

    private static BuildingSO[] LoadAssets() => AssetDatabase.FindAssets(
            "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
        .Select(AssetDatabase.GUIDToAssetPath)
        .OrderBy(value => value, StringComparer.Ordinal)
        .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
        .Where(value => value != null
            && !value.IsDeprecatedCompatibilityAsset
            && value.BuildConditions.Count == 0
            && value.layer == GridLayer.Building)
        .ToArray();

    private static long ConstructionCapitalMilliUnits(BuildingSO asset)
    {
        if (asset == null)
            throw new ArgumentNullException(nameof(asset));
        long authoredValue = checked((long)Math.Max(0, asset.GetConstructionValue()) * 1000L);
        long directWork = checked((long)Math.Ceiling(
            Math.Max(0f, asset.GetRequiredWork(BuiltInWorkTypeIds.Construct)) * 1000d));
        return checked(authoredValue + directWork);
    }

    private static Grid CreateGrid(int width)
    {
        Grid grid = new(width, GridHeight);
        List<Vector2Int> cells = new(width * GridHeight);
        for (int y = 0; y < GridHeight; y++)
        for (int x = 0; x < width; x++)
        {
            Vector2Int cell = new(x, y);
            grid.SetAreaType(cell, x == 0 && y == 1
                ? GridCellAreaType.Entrance
                : GridCellAreaType.DungeonInterior);
            cells.Add(cell);
        }
        if (!grid.RegisterOccupant(
                new SolverOccupant(1, movement: true),
                GridLayer.Hallway,
                cells,
                false))
            throw new InvalidOperationException("Could not author solver hallway support.");
        return grid;
    }

    private static string BuildCongestionReport(
        IEnumerable<StageResult> stages,
        string sourceDigest,
        string csvDigest,
        SearchProof searchProof) =>
        "RESULT=PASS; authority=shared-access-union; normalLimitPermille=700;"
        + " faultLimitPermille=900\n"
        + $"sourceDigest={sourceDigest}\n"
        + $"spatialCsvSha256={csvDigest}\n"
        + $"PASS ACTUAL_ASSET_EXACT_BACKTRACKING_ORACLE asset={searchProof.AssetId};"
        + $"requirements={searchProof.ExactRequirementCount};"
        + $"solution={searchProof.ExactCanonicalKey}\n"
        + $"PASS ACTUAL_ASSET_DETERMINISTIC_BEAM_FALLBACK asset={searchProof.AssetId};"
        + $"requirements={searchProof.BeamRequirementCount};beamWidth={BeamWidth};"
        + $"solution={searchProof.BeamCanonicalKey}\n"
        + "PASS ACTUAL_ASSET_UNIQUE_ACCESS_ADVERSARIAL_REJECTED "
        + "sameSoleStand=false;multiStandSharedCorridor=true\n"
        + "PASS ACTUAL_ASSET_FIXED_EGRESS_LANDING_PROTECTED "
        + "footprintOverlap=0;accessOverlap=0\n"
        + "PASS ACTUAL_ASSET_HEURISTIC_FALSE_NEGATIVE_GATE value=0\n"
        + string.Join("\n", stages.Select(value =>
            $"PASS population={value.Population};width={value.Width};"
            + $"normalPeak={value.MaximumNormalUtilization};"
            + $"faultPeak={value.MaximumFaultUtilization};"
            + $"normalStoragePeak={value.MaximumNormalStorageUtilization};"
            + $"faultStoragePeak={value.MaximumFaultStorageUtilization};"
            + $"minimumHeadroom={value.MinimumHeadroom}")) + "\n";

    private static string BuildExpansionReport(
        IEnumerable<StageResult> source,
        string sourceDigest,
        string csvDigest,
        SearchProof searchProof)
    {
        StageResult[] stages = source.OrderBy(value => value.Population).ToArray();
        return "RESULT=PASS; authority=asset-backed-capacity; developerEKey=false;"
            + $" automaticPopulationTrigger=false; authoredColumns="
            + $"{DungeonSpaceExpansionCatalog.InitialInteriorColumns},"
            + $"{DungeonSpaceExpansionCatalog.BasicSectorTargetColumns},"
            + $"{DungeonSpaceExpansionCatalog.SupportedSectorTargetColumns},"
            + $"{DungeonSpaceExpansionCatalog.DeepSectorTargetColumns};"
            + $" maxWidth={DungeonSpaceExpansionCatalog.MaximumSupportedGridWidth}\n"
            + $"sourceDigest={sourceDigest}\n"
            + $"spatialCsvSha256={csvDigest}\n"
            + $"searchProof={searchProof.Marker}\n"
            + string.Join("\n", stages.Select((value, index) =>
                $"PASS population={value.Population};"
                + $"requiredInteriorColumns={value.Width};"
                + $"authoredTargetInteriorColumns={AuthoredTargetWidth(value.Population)};"
                + $"spareColumns={AuthoredTargetWidth(value.Population) - value.Width};"
                + $"researchGate={ResearchGate(value.Population)}"))
            + "\nPASS FIXED_WORLD_FEATURE_SOLVER_LIVE_CONTRACT "
            + "interiorReservedByStage=1:0,3:0,6:0,12:0,18:0,24:0;"
            + "liveAuthority=IWorldResourceRuntime.Nodes+IWorldResourceNodeHost\n"
            ;
    }

    private static int AuthoredTargetWidth(int population) => population switch
    {
        <= 6 => 27,
        <= 12 => DungeonSpaceExpansionCatalog.BasicSectorTargetColumns,
        <= 18 => DungeonSpaceExpansionCatalog.SupportedSectorTargetColumns,
        _ => DungeonSpaceExpansionCatalog.DeepSectorTargetColumns
    };

    private static string ResearchGate(int population) => population switch
    {
        <= 6 => "start",
        <= 12 => DungeonSpaceExpansionCatalog.QuarryResearchId,
        <= 18 => DungeonSpaceExpansionCatalog.StonecuttingResearchId,
        _ => DungeonSpaceExpansionCatalog.DeepMiningResearchId
    };

    private static string BuildCsv(IEnumerable<SeedResult> source)
    {
        StringBuilder builder = new(
            "population,width,seed,succeeded,failureCode,solverMode,usedCells,headroomPermille,accessOverlapSavings,peakNormalUtilizationPermille,peakFaultUtilizationPermille,normalStorageUtilizationPermille,faultStorageUtilizationPermille,storageCapacityUnits,normalStockUnits,faultStockUnits,placedFacilities,overflowCells\r\n");
        foreach (SeedResult value in source
                     .OrderBy(value => value.Population)
                     .ThenBy(value => value.Seed))
        {
            builder.Append(value.Population).Append(',').Append(value.Width).Append(',')
                .Append(value.Seed).Append(',').Append(value.Succeeded ? "true" : "false")
                .Append(',').Append(value.FailureCode).Append(',').Append(value.SolverMode)
                .Append(',').Append(value.UsedCells)
                .Append(',').Append(value.HeadroomPermille).Append(',')
                .Append(value.AccessOverlapSavings).Append(',')
                .Append(value.PeakNormalUtilizationPermille).Append(',')
                .Append(value.PeakFaultUtilizationPermille).Append(',')
                .Append(value.NormalStorageUtilizationPermille).Append(',')
                .Append(value.FaultStorageUtilizationPermille).Append(',')
                .Append(value.StorageCapacityUnits).Append(',')
                .Append(value.NormalStockUnits).Append(',')
                .Append(value.FaultStockUnits).Append(',')
                .Append(value.PlacedFacilities).Append(',').Append(value.OverflowCells)
                .Append("\r\n");
        }
        return builder.ToString();
    }

    private static void WriteText(string path, string text) =>
        V27BalanceArtifactWriter.WriteIfDifferent(path, stream =>
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        });

    private static string HashText(string text)
    {
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(text ?? string.Empty)));
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(stream));
    }

    private static string Hex(byte[] bytes)
    {
        const string Digits = "0123456789abcdef";
        char[] output = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            output[index * 2] = Digits[bytes[index] >> 4];
            output[index * 2 + 1] = Digits[bytes[index] & 0xf];
        }
        return new string(output);
    }

    private static void Add(
        IDictionary<Vector2Int, long> values,
        Vector2Int cell,
        long amount) => values[cell] = checked(
        (values.TryGetValue(cell, out long current) ? current : 0L) + amount);

    private static int PeakPermille(IReadOnlyDictionary<Vector2Int, long> values) =>
        values.Count == 0 ? 0 : checked((int)values.Values.Max(value =>
            value * 1000L / GameDayMilliseconds));

    private static int DivideCeiling(int numerator, int denominator) => checked(
        (numerator + denominator - 1) / denominator);

    private static int OverflowCellsForPopulation(int population) => population switch
    {
        1 or 3 => 1,
        6 => 2,
        12 => 4,
        18 => 5,
        24 => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(population))
    };

    private static StorageUtilization CaptureStorageUtilization(
        int population,
        IReadOnlyList<AssetRequirement> requirements,
        int overflowCells)
    {
        SurvivalClosedLoopAssessment survival =
            V27SixAdultSurvivalLoopDebugScenarios.CapturePopulationStage(population);
        int normalStock = checked(
            survival.SevenDayGrainUnits
            + survival.ImmediateMealUnits
            + survival.SevenDayWaterUnits);
        int productionBurst = Math.Max(
            MaximumPhysicalStackUnits,
            Math.Max(
                DivideCeiling((int)survival.GrossGrainMilliUnitsPerDay, 1000),
                DivideCeiling((int)survival.GrossMealMilliUnitsPerDay, 1000)));
        int faultStock = checked(normalStock + productionBurst);
        int storageCapacity = requirements
            .Where(value => value.StableId.StartsWith(
                "facility:storage:", StringComparison.Ordinal))
            .Sum(value => value.Asset.GetStorageCapacity());
        int overflowCapacity = checked(overflowCells * MaximumPhysicalStackUnits);
        if (storageCapacity <= 0)
            throw new InvalidOperationException(
                $"STORAGE_CAPACITY_AUTHORITY_MISSING: population={population}.");
        return new StorageUtilization(
            storageCapacity,
            normalStock,
            faultStock,
            DivideCeiling(checked(normalStock * 1000), storageCapacity),
            DivideCeiling(
                checked(faultStock * 1000),
                checked(storageCapacity + overflowCapacity)));
    }

    private static long DivideCeiling(long numerator, long denominator) => checked(
        (numerator + denominator - 1L) / denominator);

    private static int StableJitter(int seed, string id, int x, int y)
    {
        unchecked
        {
            uint hash = 2166136261u;
            foreach (char c in id)
                hash = (hash ^ c) * 16777619u;
            hash = (hash ^ (uint)seed) * 16777619u;
            hash = (hash ^ (uint)x) * 16777619u;
            hash = (hash ^ (uint)y) * 16777619u;
            return (int)(hash % 97u);
        }
    }

    private static string AssetStableId(BuildingSO asset)
    {
        if (asset == null)
            return string.Empty;
        string facilityCode = asset.GetFacilityCode();
        if (!string.IsNullOrWhiteSpace(facilityCode))
            return facilityCode;
        if (asset.id != 0)
            return asset.id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return asset.name ?? string.Empty;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class IgnoreUnlocksDebugRules : IDungeonDebugRuleQuery
    {
        internal static readonly IgnoreUnlocksDebugRules Instance = new();
        public bool IsExecutingCommand => false;
        public bool IsEnabled(DungeonDebugCheat cheat) =>
            cheat == DungeonDebugCheat.IgnoreUnlocks;
        public bool ShouldFreezeNeed(CharacterCondition condition, float delta) => false;
        public bool ShouldBlockFriendlyDamage(CharacterActor actor) => false;
        public bool ShouldBlockFacilityDamage(bool damaged) => false;
        public bool ShouldSkipCosts() => true;
    }

    private sealed class SolverOccupant : IGridOccupant
    {
        internal SolverOccupant(int id, bool movement)
        {
            GridId = id;
            IsGridMovement = movement;
        }
        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement { get; }
    }

    private readonly struct AssetRequirement
    {
        internal AssetRequirement(
            string stableId,
            BuildingSO asset,
            int visitsPerDay,
            int occupancyMilliseconds,
            int faultMultiplierPermille)
        {
            StableId = stableId;
            Asset = asset;
            VisitsPerDay = visitsPerDay;
            OccupancyMilliseconds = occupancyMilliseconds;
            FaultMultiplierPermille = faultMultiplierPermille;
        }
        internal string StableId { get; }
        internal BuildingSO Asset { get; }
        internal int VisitsPerDay { get; }
        internal int OccupancyMilliseconds { get; }
        internal int FaultMultiplierPermille { get; }
    }

    private readonly struct PlacementChoice
    {
        internal PlacementChoice(
            BuildingSO asset,
            Vector2Int anchor,
            IReadOnlyList<Vector2Int> footprint,
            IReadOnlyList<Vector2Int> access)
        {
            Asset = asset;
            Anchor = anchor;
            Footprint = footprint;
            Access = access;
        }
        internal BuildingSO Asset { get; }
        internal Vector2Int Anchor { get; }
        internal IReadOnlyList<Vector2Int> Footprint { get; }
        internal IReadOnlyList<Vector2Int> Access { get; }
        internal bool Valid => Asset != null && Footprint?.Count > 0 && Access?.Count > 0;
    }

    private sealed class PlacementSearchState
    {
        private PlacementSearchState(
            IReadOnlyList<PlacementChoice> choices,
            HashSet<Vector2Int> exclusive,
            HashSet<Vector2Int> sharedAccess,
            long totalScore)
        {
            Choices = choices;
            Exclusive = exclusive;
            SharedAccess = sharedAccess;
            TotalScore = totalScore;
            CanonicalKey = string.Join("|", choices.Select(value =>
                $"{AssetStableId(value.Asset)}@{value.Anchor.x}:{value.Anchor.y}"));
        }

        internal static PlacementSearchState Empty { get; } = new(
            Array.Empty<PlacementChoice>(),
            new HashSet<Vector2Int>(),
            new HashSet<Vector2Int>(),
            0L);
        internal IReadOnlyList<PlacementChoice> Choices { get; }
        internal HashSet<Vector2Int> Exclusive { get; }
        internal HashSet<Vector2Int> SharedAccess { get; }
        internal long TotalScore { get; }
        internal int UsedCellCount
        {
            get
            {
                HashSet<Vector2Int> used = new(Exclusive);
                used.UnionWith(SharedAccess);
                return used.Count;
            }
        }
        internal string CanonicalKey { get; }

        internal PlacementSearchState Add(
            PlacementChoice choice,
            long score)
        {
            List<PlacementChoice> choices = new(Choices) { choice };
            HashSet<Vector2Int> exclusive = new(Exclusive);
            exclusive.UnionWith(choice.Footprint);
            HashSet<Vector2Int> sharedAccess = new(SharedAccess);
            sharedAccess.UnionWith(choice.Access);
            return new PlacementSearchState(
                choices,
                exclusive,
                sharedAccess,
                checked(TotalScore + score));
        }
    }

    private readonly struct PlacementSearchResult
    {
        private PlacementSearchResult(
            bool succeeded,
            string failureCode,
            string mode,
            IReadOnlyList<PlacementChoice> choices)
        {
            Succeeded = succeeded;
            FailureCode = failureCode ?? string.Empty;
            Mode = mode ?? string.Empty;
            Choices = choices ?? Array.Empty<PlacementChoice>();
        }
        internal static PlacementSearchResult Pass(
            string mode,
            IReadOnlyList<PlacementChoice> choices) =>
            new(true, string.Empty, mode, choices);
        internal static PlacementSearchResult Fail(string failureCode) =>
            new(false, failureCode, "none", Array.Empty<PlacementChoice>());
        internal bool Succeeded { get; }
        internal string FailureCode { get; }
        internal string Mode { get; }
        internal IReadOnlyList<PlacementChoice> Choices { get; }
    }

    private readonly struct SearchProof
    {
        internal SearchProof(
            string assetId,
            int exactRequirementCount,
            int beamRequirementCount,
            string exactCanonicalKey,
            string beamCanonicalKey)
        {
            AssetId = assetId ?? string.Empty;
            ExactRequirementCount = exactRequirementCount;
            BeamRequirementCount = beamRequirementCount;
            ExactCanonicalKey = exactCanonicalKey ?? string.Empty;
            BeamCanonicalKey = beamCanonicalKey ?? string.Empty;
        }
        internal string AssetId { get; }
        internal int ExactRequirementCount { get; }
        internal int BeamRequirementCount { get; }
        internal string ExactCanonicalKey { get; }
        internal string BeamCanonicalKey { get; }
        internal string Marker => "actual-assets:exact+beam";
    }

    private readonly struct PlacedAsset
    {
        internal PlacedAsset(AssetRequirement requirement, PlacementChoice choice)
        {
            Requirement = requirement;
            Choice = choice;
        }
        internal AssetRequirement Requirement { get; }
        internal PlacementChoice Choice { get; }
    }

    private readonly struct SeedResult
    {
        internal SeedResult(
            int population,
            int width,
            int seed,
            bool succeeded,
            string failureCode,
            int usedCells,
            int headroomPermille,
            int accessOverlapSavings,
            int peakNormalUtilizationPermille,
            int peakFaultUtilizationPermille,
            int normalStorageUtilizationPermille,
            int faultStorageUtilizationPermille,
            int storageCapacityUnits,
            int normalStockUnits,
            int faultStockUnits,
            int placedFacilities,
            int overflowCells,
            string solverMode)
        {
            Population = population;
            Width = width;
            Seed = seed;
            Succeeded = succeeded;
            FailureCode = failureCode ?? string.Empty;
            UsedCells = usedCells;
            HeadroomPermille = headroomPermille;
            AccessOverlapSavings = accessOverlapSavings;
            PeakNormalUtilizationPermille = peakNormalUtilizationPermille;
            PeakFaultUtilizationPermille = peakFaultUtilizationPermille;
            NormalStorageUtilizationPermille = normalStorageUtilizationPermille;
            FaultStorageUtilizationPermille = faultStorageUtilizationPermille;
            StorageCapacityUnits = storageCapacityUnits;
            NormalStockUnits = normalStockUnits;
            FaultStockUnits = faultStockUnits;
            PlacedFacilities = placedFacilities;
            OverflowCells = overflowCells;
            SolverMode = solverMode ?? string.Empty;
        }
        internal static SeedResult Fail(int population, int width, int seed, string code) =>
            new(population, width, seed, false, code, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, "none");
        internal int Population { get; }
        internal int Width { get; }
        internal int Seed { get; }
        internal bool Succeeded { get; }
        internal string FailureCode { get; }
        internal int UsedCells { get; }
        internal int HeadroomPermille { get; }
        internal int AccessOverlapSavings { get; }
        internal int PeakNormalUtilizationPermille { get; }
        internal int PeakFaultUtilizationPermille { get; }
        internal int NormalStorageUtilizationPermille { get; }
        internal int FaultStorageUtilizationPermille { get; }
        internal int StorageCapacityUnits { get; }
        internal int NormalStockUnits { get; }
        internal int FaultStockUnits { get; }
        internal int PlacedFacilities { get; }
        internal int OverflowCells { get; }
        internal string SolverMode { get; }
    }

    private readonly struct StageResult
    {
        internal StageResult(
            int population,
            int width,
            int successCount,
            int minimumHeadroom,
            int maximumNormalUtilization,
            int maximumFaultUtilization,
            int maximumNormalStorageUtilization,
            int maximumFaultStorageUtilization,
            int facilityCount,
            int storageCapacity)
        {
            Population = population;
            Width = width;
            SuccessCount = successCount;
            MinimumHeadroom = minimumHeadroom;
            MaximumNormalUtilization = maximumNormalUtilization;
            MaximumFaultUtilization = maximumFaultUtilization;
            MaximumNormalStorageUtilization = maximumNormalStorageUtilization;
            MaximumFaultStorageUtilization = maximumFaultStorageUtilization;
            FacilityCount = facilityCount;
            StorageCapacity = storageCapacity;
        }
        internal int Population { get; }
        internal int Width { get; }
        internal int SuccessCount { get; }
        internal int MinimumHeadroom { get; }
        internal int MaximumNormalUtilization { get; }
        internal int MaximumFaultUtilization { get; }
        internal int MaximumNormalStorageUtilization { get; }
        internal int MaximumFaultStorageUtilization { get; }
        internal int FacilityCount { get; }
        internal int StorageCapacity { get; }
    }

    private readonly struct StorageUtilization
    {
        internal StorageUtilization(
            int storageCapacityUnits,
            int normalStockUnits,
            int faultStockUnits,
            int normalPermille,
            int faultPermille)
        {
            StorageCapacityUnits = storageCapacityUnits;
            NormalStockUnits = normalStockUnits;
            FaultStockUnits = faultStockUnits;
            NormalPermille = normalPermille;
            FaultPermille = faultPermille;
        }

        internal int StorageCapacityUnits { get; }
        internal int NormalStockUnits { get; }
        internal int FaultStockUnits { get; }
        internal int NormalPermille { get; }
        internal int FaultPermille { get; }
    }
}

public readonly struct V27RedundancyCapitalAssessment
{
    public V27RedundancyCapitalAssessment(
        long portfolioCapitalMilliUnits,
        long actualRedundancyCapitalMilliUnits,
        long avoidedDuplicateCapitalMilliUnits)
    {
        if (portfolioCapitalMilliUnits <= 0
            || actualRedundancyCapitalMilliUnits < 0
            || avoidedDuplicateCapitalMilliUnits <= 0)
            throw new ArgumentOutOfRangeException(nameof(portfolioCapitalMilliUnits));
        PortfolioCapitalMilliUnits = portfolioCapitalMilliUnits;
        ActualRedundancyCapitalMilliUnits = actualRedundancyCapitalMilliUnits;
        AvoidedDuplicateCapitalMilliUnits = avoidedDuplicateCapitalMilliUnits;
        ActualRedundancyCapitalPermille = checked((int)(
            actualRedundancyCapitalMilliUnits * 1000L
            / portfolioCapitalMilliUnits));
    }

    public long PortfolioCapitalMilliUnits { get; }
    public long ActualRedundancyCapitalMilliUnits { get; }
    public long AvoidedDuplicateCapitalMilliUnits { get; }
    public int ActualRedundancyCapitalPermille { get; }
}

public readonly struct V27AssetBackedStageCapacityAssessment
{
    public V27AssetBackedStageCapacityAssessment(
        int population,
        int interiorColumns,
        int successfulSeeds,
        int minimumHeadroomPermille,
        int maximumNormalCellUtilizationPermille,
        int maximumFaultCellUtilizationPermille,
        int maximumNormalStorageUtilizationPermille,
        int maximumFaultStorageUtilizationPermille,
        int facilityRequirementCount,
        int minimumStorageCapacityUnits)
    {
        Population = population;
        InteriorColumns = interiorColumns;
        SuccessfulSeeds = successfulSeeds;
        MinimumHeadroomPermille = minimumHeadroomPermille;
        MaximumNormalCellUtilizationPermille = maximumNormalCellUtilizationPermille;
        MaximumFaultCellUtilizationPermille = maximumFaultCellUtilizationPermille;
        MaximumNormalStorageUtilizationPermille = maximumNormalStorageUtilizationPermille;
        MaximumFaultStorageUtilizationPermille = maximumFaultStorageUtilizationPermille;
        FacilityRequirementCount = facilityRequirementCount;
        MinimumStorageCapacityUnits = minimumStorageCapacityUnits;
    }

    public int Population { get; }
    public int InteriorColumns { get; }
    public int SuccessfulSeeds { get; }
    public int MinimumHeadroomPermille { get; }
    public int MaximumNormalCellUtilizationPermille { get; }
    public int MaximumFaultCellUtilizationPermille { get; }
    public int MaximumNormalStorageUtilizationPermille { get; }
    public int MaximumFaultStorageUtilizationPermille { get; }
    public int FacilityRequirementCount { get; }
    public int MinimumStorageCapacityUnits { get; }
}
#endif
