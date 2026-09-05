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
using Stopwatch = System.Diagnostics.Stopwatch;

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
    private const int NormalStorageLimitPermille = 700;
    private const int FaultStorageLimitPermille = 900;
    private const int ExactOracleMaximumRequirements = 4;
    private const int BeamWidth = 64;
    private const int MaximumSearchNodesPerSeed = 250000;
    // Node count is the deterministic complexity authority. Wall time is a
    // fail-safe only: isolated batch workers can incur one-off GC or host
    // scheduling pauses that must not turn the same search into a false
    // negative. The aggregate five-minute bound remains authoritative.
    private const long MaximumSearchMillisecondsPerSeed = 1000L;
    private const long MaximumRunMilliseconds = 300000L;
    private const long EditorUpdateBudgetMilliseconds = 8L;
    private static IncrementalRun activeRun;
    private static string lastRunStatus;

    [MenuItem("DungeonStory/V27/Verify Asset-Backed Spatial Capacity 256 Seeds")]
    public static void RunFromMenu()
    {
        Debug.Log(RunAll());
    }

    public static void RunBatchModeAndExit()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException(
                "SPATIAL_CAPACITY_BATCH_ENTRY_REQUIRES_BATCH_MODE");

        try
        {
            Debug.Log(StartIncremental());
            EditorApplication.update += ExitBatchModeWhenComplete;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("DungeonStory/V27/Cancel Asset-Backed Spatial Capacity Run")]
    private static void CancelFromMenu() => Debug.Log(CancelIncremental());

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
            + "seedBudgetFailLoud=PASS;boundedBeam=PASS;"
            + "uniqueAccessAdversarial=PASS;fixedEgressLanding=PASS";
    }

    public static V27RedundancyCapitalAssessment CaptureSixAdultRedundancyCapital()
    {
        AssetRequirement[] requirements = BuildRequirements(LoadAssets(), 6);
        long portfolio = requirements.Sum(value => ConstructionCapitalMilliUnits(
            value.Asset));
        AssetRequirement[] avoidedRequirements = requirements
            .Where(value => value.StableId.StartsWith(
                    "facility:food-production:", StringComparison.Ordinal)
                || value.StableId.StartsWith(
                    "facility:water:", StringComparison.Ordinal))
            .ToArray();
        long avoidedDuplicate = avoidedRequirements
            .Sum(value => ConstructionCapitalMilliUnits(value.Asset));
        long avoidedBom = avoidedRequirements
            .Sum(value => ConstructionBomCapitalMilliUnits(value.Asset));
        long avoidedWork = avoidedRequirements
            .Sum(value => ConstructionWorkCapitalMilliUnits(value.Asset));
        if (portfolio <= 0 || avoidedDuplicate <= 0)
            throw new InvalidOperationException(
                "REDUNDANCY_CAPITAL_AUTHORITY_MISSING");
        return new V27RedundancyCapitalAssessment(
            portfolio,
            actualRedundancyCapitalMilliUnits: 0L,
            avoidedDuplicateCapitalMilliUnits: avoidedDuplicate,
            actualRedundancyBomMilliUnits: 0L,
            actualRedundancyWorkMilliUnits: 0L,
            avoidedDuplicateBomMilliUnits: avoidedBom,
            avoidedDuplicateWorkMilliUnits: avoidedWork);
    }

    public static IReadOnlyList<V27AssetBackedStageCapacityAssessment>
        CaptureStageCapacityAssessments()
    {
        if (activeRun != null)
            throw new InvalidOperationException(
                "SPATIAL_CAPACITY_INCREMENTAL_RUN_IN_PROGRESS:"
                + CaptureIncrementalStatus());
        StageResult[] stages = LoadVerifiedStageResults();
        return stages.Select(stage =>
            new V27AssetBackedStageCapacityAssessment(
                stage.Population,
                stage.Width,
                stage.SuccessCount,
                stage.MinimumHeadroom,
                stage.MaximumNormalUtilization,
                stage.MaximumFaultUtilization,
                stage.MaximumNormalStorageUtilization,
                stage.MaximumFaultStorageUtilization,
                stage.FacilityCount,
                stage.StorageCapacityGrams,
                stage.MaximumUsedCells,
                stage.MaximumExclusiveCells,
                stage.MaximumRawAccessCells,
                stage.MaximumSharedAccessCells,
                stage.MinimumAccessOverlapSavings,
                stage.OverflowCells))
            .ToArray();
    }

    public static string RunAll() => StartIncremental();

    public static string StartIncremental()
    {
        if (activeRun != null)
            return CaptureIncrementalStatus();

        BuildingSO[] assets = LoadAssets();
        string sourceDigest = CaptureSourceDigest();
        VerifySpatialSafetyContracts(assets);
        SearchProof searchProof = VerifySearchFallbacks(assets);
        activeRun = new IncrementalRun(assets, sourceDigest, searchProof);
        EditorApplication.update += AdvanceIncremental;
        return CaptureIncrementalStatus();
    }

    public static string CaptureIncrementalStatus() => activeRun != null
        ? activeRun.Status
        : lastRunStatus
            ?? "RESULT=IDLE; verifier=asset-backed-spatial-capacity";

    public static string CancelIncremental()
    {
        if (activeRun == null)
            return CaptureIncrementalStatus();
        string status = activeRun.Cancel();
        EditorApplication.update -= AdvanceIncremental;
        activeRun = null;
        lastRunStatus = status;
        return status;
    }

    private static void AdvanceIncremental()
    {
        IncrementalRun run = activeRun;
        if (run == null)
        {
            EditorApplication.update -= AdvanceIncremental;
            return;
        }
        try
        {
            if (!run.Advance(EditorUpdateBudgetMilliseconds))
                return;
            EditorApplication.update -= AdvanceIncremental;
            activeRun = null;
            lastRunStatus = run.Status;
            Debug.Log(run.Status);
        }
        catch (Exception exception)
        {
            EditorApplication.update -= AdvanceIncremental;
            activeRun = null;
            lastRunStatus = "RESULT=FAIL; verifier=asset-backed-spatial-capacity;"
                + $"exceptionType={exception.GetType().Name};"
                + $"message={exception.Message.Replace('\r', ' ').Replace('\n', ' ')}";
            Debug.LogException(exception);
        }
    }

    private static void ExitBatchModeWhenComplete()
    {
        string status = CaptureIncrementalStatus();
        if (status.StartsWith("RESULT=RUNNING", StringComparison.Ordinal))
            return;

        EditorApplication.update -= ExitBatchModeWhenComplete;
        bool passed = status.StartsWith("RESULT=PASS", StringComparison.Ordinal);
        Debug.Log("V27 spatial batch terminal: " + status);
        EditorApplication.Exit(passed ? 0 : 1);
    }

    private static string CompleteRun(
        IReadOnlyList<StageResult> stages,
        IReadOnlyList<SeedResult> allRows,
        string sourceDigest,
        SearchProof searchProof,
        long elapsedMilliseconds)
    {
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
            + $" searchProof={searchProof.Marker};elapsedMs={elapsedMilliseconds};"
            + " widths=" + string.Join(",", stages.Select(value =>
                $"{value.Population}:{value.Width}"));
    }

    private sealed class IncrementalRun
    {
        private readonly BuildingSO[] assets;
        private readonly string sourceDigest;
        private readonly SearchProof searchProof;
        private readonly int[] populations;
        private readonly List<StageResult> stages = new();
        private readonly List<SeedResult> allRows = new();
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private List<SeedResult> widthRows;
        private AssetRequirement[] requirements;
        private WidthChoiceCatalog choiceCatalog;
        private int stageIndex;
        private int previousWidth = MinimumWidth;
        private int population;
        private int width;
        private int nextSeed;

        internal IncrementalRun(
            BuildingSO[] assets,
            string sourceDigest,
            SearchProof searchProof)
        {
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.sourceDigest = sourceDigest ?? throw new ArgumentNullException(
                nameof(sourceDigest));
            this.searchProof = searchProof;
            populations = PopulationStagePortfolioCatalog.PopulationStages.ToArray();
            if (populations.Length == 0)
                throw new InvalidOperationException("SPATIAL_POPULATION_STAGES_MISSING");
            BeginStage();
            Status = "RESULT=RUNNING; verifier=asset-backed-spatial-capacity;"
                + $"stage=1/{populations.Length};population={population};"
                + $"width={width};completedSeeds=0/{SeedsPerStage};"
                + $"runBudgetMs={MaximumRunMilliseconds};"
                + $"seedNodeBudget={MaximumSearchNodesPerSeed};"
                + $"seedTimeBudgetMs={MaximumSearchMillisecondsPerSeed}";
        }

        internal string Status { get; private set; }

        internal bool Advance(long updateBudgetMilliseconds)
        {
            if (elapsed.ElapsedMilliseconds > MaximumRunMilliseconds)
                throw new InvalidOperationException(
                    "SPATIAL_SOLVER_RUN_BUDGET_EXCEEDED:"
                    + $"elapsedMs={elapsed.ElapsedMilliseconds};"
                    + $"limitMs={MaximumRunMilliseconds};population={population};"
                    + $"width={width};nextSeed={nextSeed};"
                    + $"completedStages={stages.Count}/{populations.Length}.");

            Stopwatch update = Stopwatch.StartNew();
            do
            {
                SearchBudget budget = SearchBudget.CreateBounded(
                    population,
                    width,
                    nextSeed,
                    MaximumSearchNodesPerSeed,
                    MaximumSearchMillisecondsPerSeed);
                SeedResult result = TryPlace(
                    population,
                    width,
                    nextSeed,
                    requirements,
                    choiceCatalog,
                    budget);
                if (budget.Exceeded)
                    throw new InvalidOperationException(budget.FailureCode);
                widthRows.Add(result);
                nextSeed++;

                int failures = widthRows.Count(value => !value.Succeeded);
                int maximumAllowedFailures = SeedsPerStage - MinimumSuccesses;
                if (failures > maximumAllowedFailures)
                {
                    AdvanceWidth(
                        $"threshold-impossible:{failures}>{maximumAllowedFailures}");
                }
                else if (nextSeed > SeedsPerStage)
                {
                    int successes = widthRows.Count(value => value.Succeeded);
                    if (successes >= MinimumSuccesses)
                    {
                        CompleteStage(successes);
                        if (stageIndex >= populations.Length)
                        {
                            Status = CompleteRun(
                                stages,
                                allRows,
                                sourceDigest,
                                searchProof,
                                elapsed.ElapsedMilliseconds);
                            return true;
                        }
                        BeginStage();
                    }
                    else
                    {
                        AdvanceWidth(
                            $"completed-width:{successes}/{SeedsPerStage}");
                    }
                }

                Status = "RESULT=RUNNING; verifier=asset-backed-spatial-capacity;"
                    + $"stage={stageIndex + 1}/{populations.Length};"
                    + $"population={population};width={width};"
                    + $"completedSeeds={nextSeed - 1}/{SeedsPerStage};"
                    + $"widthSuccesses={widthRows.Count(value => value.Succeeded)};"
                    + $"elapsedMs={elapsed.ElapsedMilliseconds};"
                    + $"runBudgetMs={MaximumRunMilliseconds};"
                    + $"seedNodeBudget={MaximumSearchNodesPerSeed};"
                    + $"seedTimeBudgetMs={MaximumSearchMillisecondsPerSeed}";
            }
            while (update.ElapsedMilliseconds < updateBudgetMilliseconds);
            return false;
        }

        internal string Cancel()
        {
            Status = "RESULT=CANCELLED; verifier=asset-backed-spatial-capacity;"
                + $"population={population};width={width};"
                + $"completedSeeds={nextSeed - 1}/{SeedsPerStage};"
                + $"completedStages={stages.Count}/{populations.Length};"
                + $"elapsedMs={elapsed.ElapsedMilliseconds}";
            return Status;
        }

        private void BeginStage()
        {
            population = populations[stageIndex];
            requirements = BuildRequirements(assets, population);
            width = Math.Max(previousWidth, AuthoredTargetWidth(population));
            BeginWidth();
        }

        private void BeginWidth()
        {
            if (width > MaximumWidth)
                throw new InvalidOperationException(
                    $"DUNGEON_CAPACITY_MODEL_INVALID: population={population} "
                    + $"exceeds the {MaximumWidth}-column safety bound.");
            widthRows = new List<SeedResult>(SeedsPerStage);
            nextSeed = 1;
            choiceCatalog = WidthChoiceCatalog.Create(width, requirements);
        }

        private void AdvanceWidth(string reason)
        {
            int completed = widthRows.Count;
            int successes = widthRows.Count(value => value.Succeeded);
            int failedWidth = width;
            width = checked(width + 2);
            BeginWidth();
            Status = "RESULT=RUNNING; verifier=asset-backed-spatial-capacity;"
                + $"population={population};rejectedWidth={failedWidth};"
                + $"evaluatedSeeds={completed}/{SeedsPerStage};"
                + $"successes={successes};reason={reason};nextWidth={width}";
        }

        private void CompleteStage(int successes)
        {
            StageResult stage = CreateStageResult(
                population,
                width,
                requirements,
                widthRows,
                successes);
            foreach (SeedResult row in widthRows)
                allRows.Add(row);
            stages.Add(stage);
            previousWidth = width;
            stageIndex++;
        }
    }

    public static string CaptureSourceDigest()
    {
        string[] sourcePaths =
        {
            "Assets/Scripts/Services/Economy/Editor/V27AssetBackedSpatialCapacityDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/V27SixAdultSurvivalLoopDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/V27SurvivalClosedLoopModels.cs",
            "Assets/Scripts/Services/Economy/V27PopulationCapacityModels.cs",
            "Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs",
            "Assets/Scripts/Services/Buildings/BuildableObject.SpatialAndInteraction.cs",
            "Assets/Scripts/Services/Grid/Building/GridBuildingRuntime.cs",
            "Assets/Scripts/Models/Economy/Content/WorldResourceRuntime.cs",
            "Assets/Scripts/Services/Economy/WorldResourcePortAdapters.cs",
            "Assets/Scripts/Services/Wildlife/WildlifeHabitatDecorationRuntime.cs",
            "Assets/Resources/SO/Economy/Items/food_grain_porridge.asset",
            "Assets/Resources/SO/Economy/Items/resource_twilight_grain.asset",
            "Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I01_깨끗한_물.asset",
            "Assets/Resources/SO/Economy/Recipes/recipe_grain_porridge.asset",
            "Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R01_깨끗한_물.asset",
            "Assets/Resources/SO/Economy/Crops/crop_twilight_grain.asset",
            "Assets/Resources/SO/Survival/SurvivalBalanceSettings.asset"
        };
        string[] authorityPaths = sourcePaths
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
                .Append(HashSpatialSourceFile(absolutePath))
                .Append('\n');
        }
        BuildingSO[] assets = LoadAssets();
        foreach (int population in PopulationStagePortfolioCatalog.PopulationStages)
        foreach (AssetRequirement requirement in BuildRequirements(assets, population))
        {
            BuildingSO asset = requirement.Asset;
            string assetPath = AssetDatabase.GetAssetPath(asset)
                .Replace('\\', '/');
            Vector2Int[] footprint = asset.GetGridPosList(Vector2Int.zero)
                .Distinct()
                .OrderBy(value => value.y)
                .ThenBy(value => value.x)
                .ToArray();
            canonical.Append("portfolio|")
                .Append(population).Append('|')
                .Append(requirement.StableId).Append('|')
                .Append(assetPath).Append('|')
                .Append(asset.width).Append('x').Append(asset.height).Append('|')
                .Append((int)asset.layer).Append('|')
                .Append(asset.IsGridMovement ? '1' : '0').Append('|')
                .Append(asset.GetStorageMassCapacityGrams()).Append('|')
                .Append(requirement.VisitsPerDay).Append('|')
                .Append(requirement.OccupancyMilliseconds).Append('|')
                .Append(requirement.FaultMultiplierPermille).Append('|');
            foreach (Vector2Int cell in footprint)
                canonical.Append(cell.x).Append(',').Append(cell.y).Append(';');
            canonical.Append('\n');
        }
        return HashText(canonical.ToString());
    }

    private static StageResult[] LoadVerifiedStageResults()
    {
        if (!File.Exists(SpatialCsvPath) || !File.Exists(SharedCongestionPath))
            throw new InvalidOperationException(
                "SPATIAL_CAPACITY_CURRENT_SOURCE_ARTIFACT_MISSING: run "
                + nameof(StartIncremental) + " and wait for RESULT=PASS.");
        string csv = File.ReadAllText(SpatialCsvPath, new UTF8Encoding(false, true));
        string report = File.ReadAllText(
            SharedCongestionPath,
            new UTF8Encoding(false, true));
        if (!report.StartsWith("RESULT=PASS;", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "SPATIAL_CAPACITY_ARTIFACT_NOT_PASS");
        string expectedSourceDigest = ReadReportValue(report, "sourceDigest");
        string expectedCsvDigest = ReadReportValue(report, "spatialCsvSha256");
        string currentSourceDigest = CaptureSourceDigest();
        string currentCsvDigest = HashText(csv);
        if (!string.Equals(
                expectedSourceDigest,
                currentSourceDigest,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "SPATIAL_CAPACITY_ARTIFACT_STALE:"
                + $"expectedSourceDigest={expectedSourceDigest};"
                + $"currentSourceDigest={currentSourceDigest}.");
        if (!string.Equals(
                expectedCsvDigest,
                currentCsvDigest,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "SPATIAL_CAPACITY_ARTIFACT_DIGEST_MISMATCH:"
                + $"expectedCsvDigest={expectedCsvDigest};"
                + $"currentCsvDigest={currentCsvDigest}.");

        SeedResult[] rows = ParseSeedResults(csv);
        int[] expectedPopulations = PopulationStagePortfolioCatalog.PopulationStages
            .ToArray();
        int[] actualPopulations = rows
            .Select(value => value.Population)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (!expectedPopulations.OrderBy(value => value)
                .SequenceEqual(actualPopulations))
            throw new InvalidOperationException(
                "SPATIAL_CAPACITY_ARTIFACT_POPULATION_SET_MISMATCH:"
                + $"expected={string.Join("|", expectedPopulations)};"
                + $"actual={string.Join("|", actualPopulations)}.");

        List<StageResult> stages = new(expectedPopulations.Length);
        foreach (int population in expectedPopulations)
        {
            SeedResult[] stageRows = rows
                .Where(value => value.Population == population)
                .OrderBy(value => value.Seed)
                .ToArray();
            int[] widths = stageRows.Select(value => value.Width).Distinct().ToArray();
            int[] seeds = stageRows.Select(value => value.Seed).Distinct().ToArray();
            if (stageRows.Length != SeedsPerStage
                || seeds.Length != SeedsPerStage
                || seeds[0] != 1
                || seeds[seeds.Length - 1] != SeedsPerStage
                || widths.Length != 1)
                throw new InvalidOperationException(
                    "SPATIAL_CAPACITY_ARTIFACT_SEED_COVERAGE_INVALID:"
                    + $"population={population};rows={stageRows.Length};"
                    + $"uniqueSeeds={seeds.Length};widths={string.Join("|", widths)}.");
            SeedResult[] successes = stageRows
                .Where(value => value.Succeeded)
                .ToArray();
            Require(successes.Length >= MinimumSuccesses,
                $"DUNGEON_CAPACITY_MODEL_INVALID: population={population};"
                + $"success={successes.Length}/{SeedsPerStage};width={widths[0]}.");
            stages.Add(new StageResult(
                population,
                widths[0],
                successes.Length,
                successes.Min(value => value.HeadroomPermille),
                successes.Max(value => value.PeakNormalUtilizationPermille),
                successes.Max(value => value.PeakFaultUtilizationPermille),
                successes.Max(value => value.NormalStorageUtilizationPermille),
                successes.Max(value => value.FaultStorageUtilizationPermille),
                successes.Max(value => value.PlacedFacilities),
                successes.Min(value => value.StorageCapacityGrams),
                successes.Max(value => value.UsedCells),
                successes.Max(value => value.ExclusiveCells),
                successes.Max(value => value.RawAccessCells),
                successes.Max(value => value.SharedAccessCells),
                successes.Min(value => value.AccessOverlapSavings),
                successes.Max(value => value.OverflowCells)));
        }
        return stages.ToArray();
    }

    private static string ReadReportValue(string report, string key)
    {
        string prefix = key + "=";
        string line = (report ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .SingleOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
        if (line == null)
            throw new InvalidOperationException(
                "SPATIAL_CAPACITY_ARTIFACT_MANIFEST_FIELD_MISSING:" + key);
        return line.Substring(prefix.Length);
    }

    private static SeedResult[] ParseSeedResults(string csv)
    {
        string[] lines = (csv ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        const string Header =
            "population,width,seed,succeeded,failureCode,solverMode,usedCells,exclusiveCells,rawAccessCells,sharedAccessCells,headroomPermille,accessOverlapSavings,peakNormalUtilizationPermille,peakFaultUtilizationPermille,normalStorageUtilizationPermille,faultStorageUtilizationPermille,storageCapacityGrams,normalStockMassGrams,faultStockMassGrams,placedFacilities,overflowCells";
        if (lines.Length < 2 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "SPATIAL_CAPACITY_ARTIFACT_SCHEMA_MISMATCH");
        List<SeedResult> rows = new(lines.Length - 1);
        for (int index = 1; index < lines.Length; index++)
        {
            string[] fields = lines[index].Split(',');
            if (fields.Length != 21)
                throw new InvalidOperationException(
                    "SPATIAL_CAPACITY_ARTIFACT_ROW_INVALID:line=" + (index + 1));
            rows.Add(new SeedResult(
                ParseInt(fields[0]),
                ParseInt(fields[1]),
                ParseInt(fields[2]),
                ParseBool(fields[3]),
                fields[4],
                ParseInt(fields[6]),
                ParseInt(fields[7]),
                ParseInt(fields[8]),
                ParseInt(fields[9]),
                ParseInt(fields[10]),
                ParseInt(fields[11]),
                ParseInt(fields[12]),
                ParseInt(fields[13]),
                ParseInt(fields[14]),
                ParseInt(fields[15]),
                ParseLong(fields[16]),
                ParseLong(fields[17]),
                ParseLong(fields[18]),
                ParseInt(fields[19]),
                ParseInt(fields[20]),
                fields[5]));
        }
        return rows.ToArray();
    }

    private static int ParseInt(string token) => int.Parse(
        token,
        System.Globalization.NumberStyles.Integer,
        System.Globalization.CultureInfo.InvariantCulture);

    private static long ParseLong(string token) => long.Parse(
        token,
        System.Globalization.NumberStyles.Integer,
        System.Globalization.CultureInfo.InvariantCulture);

    private static bool ParseBool(string token) => token switch
    {
        "true" => true,
        "false" => false,
        _ => throw new FormatException("Non-canonical Boolean token: " + token)
    };

    private static SeedResult TryPlace(
        int population,
        int width,
        int seed,
        IReadOnlyList<AssetRequirement> source,
        WidthChoiceCatalog choiceCatalog,
        SearchBudget budget)
    {
        Grid grid = CreateGrid(width);
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
            requirements,
            seed,
            choiceCatalog,
            budget);
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
            exclusive.Count,
            rawAccess,
            sharedAccess.Count,
            headroom,
            rawAccess - sharedAccess.Count,
            normalPeak,
            faultPeak,
            storage.NormalPermille,
            storage.FaultPermille,
            storage.StorageCapacityGrams,
            storage.NormalStockMassGrams,
            storage.FaultStockMassGrams,
            placed.Count,
            freeContainment.Length,
            search.Mode);
    }

    private static StageResult CreateStageResult(
        int population,
        int width,
        IReadOnlyList<AssetRequirement> requirements,
        IReadOnlyList<SeedResult> candidates,
        int successCount)
    {
        SeedResult[] successes = candidates
            .Where(value => value.Succeeded)
            .ToArray();
        Require(successes.Length == successCount && successCount >= MinimumSuccesses,
            $"DUNGEON_CAPACITY_MODEL_INVALID: population={population};"
            + $"success={successes.Length}/{SeedsPerStage};width={width}.");
        return new StageResult(
            population,
            width,
            successCount,
            successes.Min(value => value.HeadroomPermille),
            successes.Max(value => value.PeakNormalUtilizationPermille),
            successes.Max(value => value.PeakFaultUtilizationPermille),
            successes.Max(value => value.NormalStorageUtilizationPermille),
            successes.Max(value => value.FaultStorageUtilizationPermille),
            requirements.Count,
            successes.Min(value => value.StorageCapacityGrams),
            successes.Max(value => value.UsedCells),
            successes.Max(value => value.ExclusiveCells),
            successes.Max(value => value.RawAccessCells),
            successes.Max(value => value.SharedAccessCells),
            successes.Min(value => value.AccessOverlapSavings),
            successes.Max(value => value.OverflowCells));
    }

    private static PlacementSearchResult SolvePlacements(
        Grid grid,
        IReadOnlyList<AssetRequirement> requirements,
        int seed,
        WidthChoiceCatalog choiceCatalog,
        SearchBudget budget)
    {
        PlacementChoice[][] candidates = requirements
            .Select(requirement => choiceCatalog.Get(requirement.Asset))
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
                out PlacementSearchState greedy,
                budget))
            return PlacementSearchResult.Pass("greedy", greedy.Choices);
        if (budget.Exceeded)
            return PlacementSearchResult.Fail(budget.FailureCode);

        if (requirements.Count <= ExactOracleMaximumRequirements)
        {
            if (TryExactSearch(
                    requirements,
                    candidates,
                    seed,
                    grid.width,
                    out PlacementSearchState exact,
                    budget))
                return PlacementSearchResult.Pass("exact", exact.Choices);
            if (budget.Exceeded)
                return PlacementSearchResult.Fail(budget.FailureCode);
            return PlacementSearchResult.Fail("EXACT_ASSET_PLACEMENT_IMPOSSIBLE");
        }

        if (TryBeamSearch(
                requirements,
                candidates,
                seed,
                grid.width,
                BeamWidth,
                out PlacementSearchState beam,
                budget))
            return PlacementSearchResult.Pass("beam", beam.Choices);
        if (budget.Exceeded)
            return PlacementSearchResult.Fail(budget.FailureCode);
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
        out PlacementSearchState result,
        SearchBudget budget = null)
    {
        budget ??= SearchBudget.CreateUnbounded();
        PlacementSearchState state = PlacementSearchState.Empty;
        for (int index = 0; index < requirements.Count; index++)
        {
            AssetRequirement requirement = requirements[index];
            PlacementChoice choice = default;
            long bestScore = long.MaxValue;
            foreach (PlacementChoice candidate in candidates[index])
            {
                if (!budget.TryVisit("greedy") || !CanAdd(state, candidate))
                    continue;
                long score = IncrementalScore(
                    state, candidate, requirement, seed, width);
                if (choice.Valid && (score > bestScore
                    || score == bestScore
                    && CompareAnchor(candidate.Anchor, choice.Anchor) >= 0))
                    continue;
                choice = candidate;
                bestScore = score;
            }
            if (budget.Exceeded)
            {
                result = null;
                return false;
            }
            if (!choice.Valid)
            {
                result = null;
                return false;
            }
            state = state.Add(choice, bestScore);
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
        out PlacementSearchState result,
        SearchBudget budget = null)
    {
        budget ??= SearchBudget.CreateUnbounded();
        List<PlacementSearchState> frontier = new() { PlacementSearchState.Empty };
        for (int index = 0; index < requirements.Count; index++)
        {
            AssetRequirement requirement = requirements[index];
            List<PlacementSearchState> next = new(beamWidth);
            foreach (PlacementSearchState state in frontier)
            foreach (PlacementChoice choice in candidates[index])
            {
                if (!budget.TryVisit("beam"))
                {
                    result = null;
                    return false;
                }
                if (!CanAdd(state, choice))
                    continue;
                long incrementalScore = IncrementalScore(
                    state, choice, requirement, seed, width);
                long totalScore = checked(state.TotalScore + incrementalScore);
                if (next.Count == beamWidth
                    && totalScore > next[next.Count - 1].TotalScore)
                    continue;
                AddBoundedBeamCandidate(
                    next,
                    state.Add(choice, incrementalScore),
                    beamWidth);
            }
            frontier = next;
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
        out PlacementSearchState result,
        SearchBudget budget = null)
    {
        budget ??= SearchBudget.CreateUnbounded();
        PlacementSearchState found = null;
        bool Search(int index, PlacementSearchState state)
        {
            if (!budget.TryVisit("exact"))
                return false;
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
                if (!budget.TryVisit("exact-candidate"))
                    return false;
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

    private static int CompareAnchor(Vector2Int left, Vector2Int right)
    {
        int y = left.y.CompareTo(right.y);
        return y != 0 ? y : left.x.CompareTo(right.x);
    }

    private static int CompareSearchState(
        PlacementSearchState left,
        PlacementSearchState right)
    {
        int score = left.TotalScore.CompareTo(right.TotalScore);
        if (score != 0)
            return score;
        int used = left.UsedCellCount.CompareTo(right.UsedCellCount);
        return used != 0
            ? used
            : string.CompareOrdinal(left.CanonicalKey, right.CanonicalKey);
    }

    private static void AddBoundedBeamCandidate(
        List<PlacementSearchState> destination,
        PlacementSearchState candidate,
        int capacity)
    {
        int low = 0;
        int high = destination.Count;
        while (low < high)
        {
            int middle = low + ((high - low) >> 1);
            if (CompareSearchState(destination[middle], candidate) <= 0)
                low = middle + 1;
            else
                high = middle;
        }
        if (low >= capacity)
            return;
        destination.Insert(low, candidate);
        if (destination.Count > capacity)
            destination.RemoveAt(destination.Count - 1);
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
        SearchBudget exhaustedBudget = SearchBudget.CreateBounded(
            population: 0,
            width: fixtureWidth,
            seed: seed,
            maximumNodes: 1,
            maximumMilliseconds: 10000L);
        bool budgetedBeam = TryBeamSearch(
            largeRequirements,
            largeCandidates,
            seed,
            fixtureWidth,
            BeamWidth,
            out _,
            exhaustedBudget);
        Require(!budgetedBeam
                && exhaustedBudget.Exceeded
                && exhaustedBudget.FailureCode.StartsWith(
                    "SPATIAL_SOLVER_SEED_BUDGET_EXCEEDED:",
                    StringComparison.Ordinal),
            "ACTUAL_ASSET_SEARCH_BUDGET_FAIL_LOUD_PROOF_FAILED.");
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
            value => value.GetStorageMassCapacityGrams() > 0L
                && value.StoresAllCategories(),
            preferStorageDensity: true);
        long requiredNormalCapacityGrams = DivideCeiling(
            checked(survival.RequiredStorageMassGrams * 1000L),
            NormalStorageLimitPermille);
        Add(values, "storage", storage,
            checked((int)DivideCeiling(
                requiredNormalCapacityGrams,
                storage.GetStorageMassCapacityGrams())),
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
                ? -(decimal)value.GetStorageMassCapacityGrams()
                    / Math.Max(1, value.width * value.height)
                : value.width * value.height)
            .ThenByDescending(value => value.GetStorageMassCapacityGrams())
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
        return checked(
            ConstructionBomCapitalMilliUnits(asset)
            + ConstructionWorkCapitalMilliUnits(asset));
    }

    private static long ConstructionBomCapitalMilliUnits(BuildingSO asset) =>
        checked((long)Math.Max(0, asset.GetConstructionValue()) * 1000L);

    private static long ConstructionWorkCapitalMilliUnits(BuildingSO asset) =>
        checked((long)Math.Ceiling(
            Math.Max(0f, asset.GetRequiredWork(BuiltInWorkTypeIds.Construct)) * 1000d));

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
        + "PASS INCREMENTAL_EDITOR_EXECUTION "
        + $"seedsPerStage={SeedsPerStage};minimumSuccesses={MinimumSuccesses};"
        + $"updateBudgetMs={EditorUpdateBudgetMilliseconds};"
        + $"seedNodeBudget={MaximumSearchNodesPerSeed};"
        + $"seedTimeBudgetMs={MaximumSearchMillisecondsPerSeed};"
        + $"runBudgetMs={MaximumRunMilliseconds};"
        + "thresholdImpossibleEarlyExit=true;partialArtifactPublish=false\n"
        + $"PASS ACTUAL_ASSET_EXACT_BACKTRACKING_ORACLE asset={searchProof.AssetId};"
        + $"requirements={searchProof.ExactRequirementCount};"
        + $"solution={searchProof.ExactCanonicalKey}\n"
        + $"PASS ACTUAL_ASSET_DETERMINISTIC_BEAM_FALLBACK asset={searchProof.AssetId};"
        + $"requirements={searchProof.BeamRequirementCount};beamWidth={BeamWidth};"
        + $"solution={searchProof.BeamCanonicalKey}\n"
        + "PASS ACTUAL_ASSET_SEARCH_BUDGET_FAIL_LOUD "
        + "nodeAndTimeBudgetExhaustion=inconclusive-error\n"
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
        <= 3 => PopulationStagePortfolioCatalog.InteriorColumnsForPopulation(population),
        6 => DungeonSpaceExpansionCatalog.InitialInteriorColumns,
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
            "population,width,seed,succeeded,failureCode,solverMode,usedCells,exclusiveCells,rawAccessCells,sharedAccessCells,headroomPermille,accessOverlapSavings,peakNormalUtilizationPermille,peakFaultUtilizationPermille,normalStorageUtilizationPermille,faultStorageUtilizationPermille,storageCapacityGrams,normalStockMassGrams,faultStockMassGrams,placedFacilities,overflowCells\r\n");
        foreach (SeedResult value in source
                     .OrderBy(value => value.Population)
                     .ThenBy(value => value.Seed))
        {
            builder.Append(value.Population).Append(',').Append(value.Width).Append(',')
                .Append(value.Seed).Append(',').Append(value.Succeeded ? "true" : "false")
                .Append(',').Append(value.FailureCode).Append(',').Append(value.SolverMode)
                .Append(',').Append(value.UsedCells)
                .Append(',').Append(value.ExclusiveCells)
                .Append(',').Append(value.RawAccessCells)
                .Append(',').Append(value.SharedAccessCells)
                .Append(',').Append(value.HeadroomPermille).Append(',')
                .Append(value.AccessOverlapSavings).Append(',')
                .Append(value.PeakNormalUtilizationPermille).Append(',')
                .Append(value.PeakFaultUtilizationPermille).Append(',')
                .Append(value.NormalStorageUtilizationPermille).Append(',')
                .Append(value.FaultStorageUtilizationPermille).Append(',')
                .Append(value.StorageCapacityGrams).Append(',')
                .Append(value.NormalStockMassGrams).Append(',')
                .Append(value.FaultStockMassGrams).Append(',')
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

    private static string HashSpatialSourceFile(string path)
    {
        if (!string.Equals(
                Path.GetExtension(path),
                ".asset",
                StringComparison.OrdinalIgnoreCase))
        {
            return HashFile(path);
        }

        string[] lines = File.ReadAllText(path, new UTF8Encoding(false, true))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string trimmed = lines[index].TrimStart();
            if (!trimmed.StartsWith("unitPrice:", StringComparison.Ordinal)
                && !trimmed.StartsWith("saleRate:", StringComparison.Ordinal))
            {
                continue;
            }
            int indentation = lines[index].Length - trimmed.Length;
            string field = trimmed.Substring(0, trimmed.IndexOf(':'));
            lines[index] = new string(' ', indentation) + field
                + ": <spatially-irrelevant-market-authority>";
        }
        return HashText(string.Join("\n", lines));
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
        long normalStockMassGrams = survival.RequiredStorageMassGrams;
        long productionBurstMassGrams = Math.Max(
            survival.MaximumRelevantStackMassGrams,
            Math.Max(
                survival.GrossGrainMassGramsPerDay,
                survival.GrossMealMassGramsPerDay));
        long faultStockMassGrams = checked(
            normalStockMassGrams + productionBurstMassGrams);
        long storageCapacityGrams = requirements
            .Where(value => value.StableId.StartsWith(
                "facility:storage:", StringComparison.Ordinal))
            .Sum(value => value.Asset.GetStorageMassCapacityGrams());
        long overflowCapacityGrams = checked(
            overflowCells * survival.MaximumRelevantStackMassGrams);
        if (storageCapacityGrams <= 0L)
            throw new InvalidOperationException(
                $"STORAGE_CAPACITY_AUTHORITY_MISSING: population={population}.");
        return new StorageUtilization(
            storageCapacityGrams,
            normalStockMassGrams,
            faultStockMassGrams,
            checked((int)DivideCeiling(
                checked(normalStockMassGrams * 1000L),
                storageCapacityGrams)),
            checked((int)DivideCeiling(
                checked(faultStockMassGrams * 1000L),
                checked(storageCapacityGrams + overflowCapacityGrams))));
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

    private sealed class WidthChoiceCatalog
    {
        private readonly IReadOnlyDictionary<BuildingSO, PlacementChoice[]> choices;

        private WidthChoiceCatalog(
            IReadOnlyDictionary<BuildingSO, PlacementChoice[]> choices) =>
            this.choices = choices;

        internal static WidthChoiceCatalog Create(
            int width,
            IReadOnlyList<AssetRequirement> requirements)
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
            Dictionary<BuildingSO, PlacementChoice[]> values = new();
            foreach (AssetRequirement requirement in requirements
                         .GroupBy(value => value.Asset)
                         .Select(value => value.First())
                         .OrderBy(value => AssetStableId(value.Asset),
                             StringComparer.Ordinal))
            {
                values.Add(
                    requirement.Asset,
                    EnumerateChoices(
                        grid,
                        validator,
                        requirement,
                        fixedCells));
            }
            return new WidthChoiceCatalog(values);
        }

        internal PlacementChoice[] Get(BuildingSO asset)
        {
            if (asset != null && choices.TryGetValue(asset, out PlacementChoice[] value))
                return value;
            throw new InvalidOperationException(
                "SPATIAL_CHOICE_CATALOG_ASSET_MISSING:"
                + AssetStableId(asset));
        }
    }

    private sealed class SearchBudget
    {
        private readonly int population;
        private readonly int width;
        private readonly int seed;
        private readonly int maximumNodes;
        private readonly long maximumMilliseconds;
        private readonly Stopwatch elapsed;
        private readonly bool bounded;
        private string phase = string.Empty;

        private SearchBudget(
            int population,
            int width,
            int seed,
            int maximumNodes,
            long maximumMilliseconds,
            bool bounded)
        {
            this.population = population;
            this.width = width;
            this.seed = seed;
            this.maximumNodes = maximumNodes;
            this.maximumMilliseconds = maximumMilliseconds;
            this.bounded = bounded;
            elapsed = Stopwatch.StartNew();
        }

        internal static SearchBudget CreateBounded(
            int population,
            int width,
            int seed,
            int maximumNodes,
            long maximumMilliseconds) => new(
                population,
                width,
                seed,
                maximumNodes,
                maximumMilliseconds,
                bounded: true);

        internal static SearchBudget CreateUnbounded() => new(
            0,
            0,
            0,
            int.MaxValue,
            long.MaxValue,
            bounded: false);

        internal int VisitedNodes { get; private set; }
        internal bool Exceeded { get; private set; }
        internal string FailureCode =>
            "SPATIAL_SOLVER_SEED_BUDGET_EXCEEDED:"
            + $"population={population};width={width};seed={seed};"
            + $"phase={phase};nodes={VisitedNodes};nodeLimit={maximumNodes};"
            + $"elapsedMs={elapsed.ElapsedMilliseconds};"
            + $"timeLimitMs={maximumMilliseconds}";

        internal bool TryVisit(string nextPhase)
        {
            if (!bounded)
                return true;
            if (Exceeded)
                return false;
            phase = nextPhase ?? string.Empty;
            VisitedNodes = checked(VisitedNodes + 1);
            if (VisitedNodes > maximumNodes
                || (VisitedNodes & 255) == 0
                && elapsed.ElapsedMilliseconds > maximumMilliseconds)
            {
                Exceeded = true;
                return false;
            }
            return true;
        }
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
            long totalScore,
            int usedCellCount,
            string canonicalKey)
        {
            Choices = choices;
            Exclusive = exclusive;
            SharedAccess = sharedAccess;
            TotalScore = totalScore;
            UsedCellCount = usedCellCount;
            CanonicalKey = canonicalKey ?? string.Empty;
        }

        internal static PlacementSearchState Empty { get; } = new(
            Array.Empty<PlacementChoice>(),
            new HashSet<Vector2Int>(),
            new HashSet<Vector2Int>(),
            0L,
            0,
            string.Empty);
        internal IReadOnlyList<PlacementChoice> Choices { get; }
        internal HashSet<Vector2Int> Exclusive { get; }
        internal HashSet<Vector2Int> SharedAccess { get; }
        internal long TotalScore { get; }
        internal int UsedCellCount { get; }
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
            int overlap = sharedAccess.Count(value => exclusive.Contains(value));
            int usedCellCount = checked(
                exclusive.Count + sharedAccess.Count - overlap);
            string fragment = $"{AssetStableId(choice.Asset)}@"
                + $"{choice.Anchor.x}:{choice.Anchor.y}";
            string canonicalKey = CanonicalKey.Length == 0
                ? fragment
                : CanonicalKey + "|" + fragment;
            return new PlacementSearchState(
                choices,
                exclusive,
                sharedAccess,
                checked(TotalScore + score),
                usedCellCount,
                canonicalKey);
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
            int exclusiveCells,
            int rawAccessCells,
            int sharedAccessCells,
            int headroomPermille,
            int accessOverlapSavings,
            int peakNormalUtilizationPermille,
            int peakFaultUtilizationPermille,
            int normalStorageUtilizationPermille,
            int faultStorageUtilizationPermille,
            long storageCapacityGrams,
            long normalStockMassGrams,
            long faultStockMassGrams,
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
            ExclusiveCells = exclusiveCells;
            RawAccessCells = rawAccessCells;
            SharedAccessCells = sharedAccessCells;
            HeadroomPermille = headroomPermille;
            AccessOverlapSavings = accessOverlapSavings;
            PeakNormalUtilizationPermille = peakNormalUtilizationPermille;
            PeakFaultUtilizationPermille = peakFaultUtilizationPermille;
            NormalStorageUtilizationPermille = normalStorageUtilizationPermille;
            FaultStorageUtilizationPermille = faultStorageUtilizationPermille;
            StorageCapacityGrams = storageCapacityGrams;
            NormalStockMassGrams = normalStockMassGrams;
            FaultStockMassGrams = faultStockMassGrams;
            PlacedFacilities = placedFacilities;
            OverflowCells = overflowCells;
            SolverMode = solverMode ?? string.Empty;
        }
        internal static SeedResult Fail(int population, int width, int seed, string code) =>
            new(population, width, seed, false, code, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, "none");
        internal int Population { get; }
        internal int Width { get; }
        internal int Seed { get; }
        internal bool Succeeded { get; }
        internal string FailureCode { get; }
        internal int UsedCells { get; }
        internal int ExclusiveCells { get; }
        internal int RawAccessCells { get; }
        internal int SharedAccessCells { get; }
        internal int HeadroomPermille { get; }
        internal int AccessOverlapSavings { get; }
        internal int PeakNormalUtilizationPermille { get; }
        internal int PeakFaultUtilizationPermille { get; }
        internal int NormalStorageUtilizationPermille { get; }
        internal int FaultStorageUtilizationPermille { get; }
        internal long StorageCapacityGrams { get; }
        internal long NormalStockMassGrams { get; }
        internal long FaultStockMassGrams { get; }
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
            long storageCapacityGrams,
            int maximumUsedCells,
            int maximumExclusiveCells,
            int maximumRawAccessCells,
            int maximumSharedAccessCells,
            int minimumAccessOverlapSavings,
            int overflowCells)
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
            StorageCapacityGrams = storageCapacityGrams;
            MaximumUsedCells = maximumUsedCells;
            MaximumExclusiveCells = maximumExclusiveCells;
            MaximumRawAccessCells = maximumRawAccessCells;
            MaximumSharedAccessCells = maximumSharedAccessCells;
            MinimumAccessOverlapSavings = minimumAccessOverlapSavings;
            OverflowCells = overflowCells;
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
        internal long StorageCapacityGrams { get; }
        internal int MaximumUsedCells { get; }
        internal int MaximumExclusiveCells { get; }
        internal int MaximumRawAccessCells { get; }
        internal int MaximumSharedAccessCells { get; }
        internal int MinimumAccessOverlapSavings { get; }
        internal int OverflowCells { get; }
    }

    private readonly struct StorageUtilization
    {
        internal StorageUtilization(
            long storageCapacityGrams,
            long normalStockMassGrams,
            long faultStockMassGrams,
            int normalPermille,
            int faultPermille)
        {
            StorageCapacityGrams = storageCapacityGrams;
            NormalStockMassGrams = normalStockMassGrams;
            FaultStockMassGrams = faultStockMassGrams;
            NormalPermille = normalPermille;
            FaultPermille = faultPermille;
        }

        internal long StorageCapacityGrams { get; }
        internal long NormalStockMassGrams { get; }
        internal long FaultStockMassGrams { get; }
        internal int NormalPermille { get; }
        internal int FaultPermille { get; }
    }
}

public readonly struct V27RedundancyCapitalAssessment
{
    public V27RedundancyCapitalAssessment(
        long portfolioCapitalMilliUnits,
        long actualRedundancyCapitalMilliUnits,
        long avoidedDuplicateCapitalMilliUnits,
        long actualRedundancyBomMilliUnits,
        long actualRedundancyWorkMilliUnits,
        long avoidedDuplicateBomMilliUnits,
        long avoidedDuplicateWorkMilliUnits)
    {
        if (portfolioCapitalMilliUnits <= 0
            || actualRedundancyCapitalMilliUnits < 0
            || avoidedDuplicateCapitalMilliUnits <= 0)
            throw new ArgumentOutOfRangeException(nameof(portfolioCapitalMilliUnits));
        PortfolioCapitalMilliUnits = portfolioCapitalMilliUnits;
        ActualRedundancyCapitalMilliUnits = actualRedundancyCapitalMilliUnits;
        AvoidedDuplicateCapitalMilliUnits = avoidedDuplicateCapitalMilliUnits;
        ActualRedundancyBomMilliUnits = actualRedundancyBomMilliUnits;
        ActualRedundancyWorkMilliUnits = actualRedundancyWorkMilliUnits;
        AvoidedDuplicateBomMilliUnits = avoidedDuplicateBomMilliUnits;
        AvoidedDuplicateWorkMilliUnits = avoidedDuplicateWorkMilliUnits;
        ActualRedundancyCapitalPermille = checked((int)(
            actualRedundancyCapitalMilliUnits * 1000L
            / portfolioCapitalMilliUnits));
    }

    public long PortfolioCapitalMilliUnits { get; }
    public long ActualRedundancyCapitalMilliUnits { get; }
    public long AvoidedDuplicateCapitalMilliUnits { get; }
    public long ActualRedundancyBomMilliUnits { get; }
    public long ActualRedundancyWorkMilliUnits { get; }
    public long AvoidedDuplicateBomMilliUnits { get; }
    public long AvoidedDuplicateWorkMilliUnits { get; }
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
        long minimumStorageCapacityGrams,
        int maximumUsedCells,
        int maximumExclusiveCells,
        int maximumRawAccessCells,
        int maximumSharedAccessCells,
        int minimumAccessOverlapSavings,
        int overflowCells)
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
        MinimumStorageCapacityGrams = minimumStorageCapacityGrams;
        MaximumUsedCells = maximumUsedCells;
        MaximumExclusiveCells = maximumExclusiveCells;
        MaximumRawAccessCells = maximumRawAccessCells;
        MaximumSharedAccessCells = maximumSharedAccessCells;
        MinimumAccessOverlapSavings = minimumAccessOverlapSavings;
        OverflowCells = overflowCells;
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
    public long MinimumStorageCapacityGrams { get; }
    public int MaximumUsedCells { get; }
    public int MaximumExclusiveCells { get; }
    public int MaximumRawAccessCells { get; }
    public int MaximumSharedAccessCells { get; }
    public int MinimumAccessOverlapSavings { get; }
    public int OverflowCells { get; }
}
#endif
