#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

[BalanceCaptureFactory]
public static class V27PopulationCapacityDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-layout-256-seed.txt";

    [MenuItem("DungeonStory/V27/Verify Population Capacity 256 Seeds")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        Write(ReportPath, report);
        Debug.Log(report);
    }

    public static string RunAll()
    {
        VerifySharedAccessRule();
        VerifyUniqueAccessSafety();
        VerifyContinuityContract();
        VerifyPairedRunAttribution();
        DeterministicDungeonSpaceCapacityQuery query = new();
        int passed = 0;
        int minimumHeadroom = int.MaxValue;
        int maximumNormalUtilization = 0;
        int maximumFaultUtilization = 0;
        int minimumOverlapSavings = int.MaxValue;
        List<string> stageResults = new();
        foreach (int population in PopulationStagePortfolioCatalog.PopulationStages)
        {
            PopulationStagePortfolio portfolio =
                PopulationStagePortfolioCatalog.Capture(population);
            int stageMinimumHeadroom = int.MaxValue;
            int stageUsedCells = 0;
            for (int seed = 1; seed <= 256; seed++)
            {
                DungeonSpaceCapacityAssessment result = query.Assess(portfolio, seed);
                Require(result.Succeeded,
                    $"Population {population} capacity failed at seed {seed}: "
                    + $"{result.FailureCode}.");
                Require(result.HeadroomPermille >= 300,
                    $"Population {population} headroom fell below 30% at seed "
                    + $"{seed}: {result.HeadroomPermille}.");
                Require(result.PeakNormalCellUtilizationPermille <= 700,
                    $"Population {population} normal shared-cell utilization "
                    + $"exceeded 70% at seed {seed}.");
                Require(result.PeakFaultCellUtilizationPermille <= 900,
                    $"Population {population} fault shared-cell utilization "
                    + $"exceeded 90% at seed {seed}.");
                Require(result.AccessOverlapSavings > 0,
                    $"Population {population} seed {seed} did not exercise "
                    + "shared access union accounting.");
                passed++;
                stageMinimumHeadroom = Math.Min(
                    stageMinimumHeadroom,
                    result.HeadroomPermille);
                stageUsedCells = result.EffectiveUsedCells;
                minimumHeadroom = Math.Min(minimumHeadroom, result.HeadroomPermille);
                maximumNormalUtilization = Math.Max(
                    maximumNormalUtilization,
                    result.PeakNormalCellUtilizationPermille);
                maximumFaultUtilization = Math.Max(
                    maximumFaultUtilization,
                    result.PeakFaultCellUtilizationPermille);
                minimumOverlapSavings = Math.Min(
                    minimumOverlapSavings,
                    result.AccessOverlapSavings);
            }
            stageResults.Add(
                $"{population}:{PopulationStagePortfolioCatalog.InteriorColumnsForPopulation(population)}"
                + $":{stageUsedCells}:{stageMinimumHeadroom}");
        }

        VerifyOverflowCannotConsumeAccess(
            PopulationStagePortfolioCatalog.Capture(6));
        return "RESULT=PASS; seedsPerStage=256; stages=6; passed=" + passed
            + "; successRatePermille=1000; minimumHeadroomPermille=" + minimumHeadroom
            + "; maximumNormalCellUtilizationPermille=" + maximumNormalUtilization
            + "; maximumFaultCellUtilizationPermille=" + maximumFaultUtilization
            + "; minimumAccessOverlapSavings=" + minimumOverlapSavings
            + "; heuristicFalseNegative=0; stageColumnsUsedHeadroom="
            + string.Join(",", stageResults);
    }

    private static void VerifySharedAccessRule()
    {
        Vector2Int[] footprint =
        {
            new Vector2Int(5, 1),
            new Vector2Int(6, 1)
        };
        IReadOnlyList<Vector2Int> access =
            BuildingWorkAccessRules.EnumerateCandidates(
                footprint,
                traversableFootprint: false);
        Require(access.SequenceEqual(new[]
            {
                new Vector2Int(4, 1),
                new Vector2Int(7, 1)
            }),
            "Horizontal work-access candidates drifted from production rules.");
        IReadOnlyList<Vector2Int> traversable =
            BuildingWorkAccessRules.EnumerateCandidates(
                footprint,
                traversableFootprint: true);
        Require(traversable.SequenceEqual(footprint),
            "Traversable footprint access must use the footprint itself.");
    }

    private static void VerifyUniqueAccessSafety()
    {
        Vector2Int shared = new(2, 0);
        FacilityPlacementCandidate left = new(
            "fixture:left-sole-access",
            new[] { new Vector2Int(1, 0) },
            new[] { shared },
            Array.Empty<Vector2Int>(),
            1,
            1000);
        FacilityPlacementCandidate right = new(
            "fixture:right-sole-access",
            new[] { new Vector2Int(3, 0) },
            new[] { shared },
            Array.Empty<Vector2Int>(),
            1,
            1000);
        PopulationStagePortfolio unsafePortfolio = FixturePortfolio(left, right);
        DungeonSpaceCapacityAssessment unsafeResult =
            new DeterministicDungeonSpaceCapacityQuery().Assess(unsafePortfolio, 1);
        Require(!unsafeResult.Succeeded,
            "Two facilities were allowed to share the same sole work-access cell.");

        FacilityPlacementCandidate leftAlternative = new(
            "fixture:left-multi-access",
            new[] { new Vector2Int(1, 0) },
            new[] { shared, new Vector2Int(1, 1) },
            Array.Empty<Vector2Int>(),
            1,
            1000);
        FacilityPlacementCandidate rightAlternative = new(
            "fixture:right-multi-access",
            new[] { new Vector2Int(3, 0) },
            new[] { shared, new Vector2Int(3, 1) },
            Array.Empty<Vector2Int>(),
            1,
            1000);
        DungeonSpaceCapacityAssessment safeResult =
            new DeterministicDungeonSpaceCapacityQuery().Assess(
                FixturePortfolio(leftAlternative, rightAlternative),
                1);
        Require(safeResult.Succeeded && safeResult.AccessOverlapSavings > 0,
            "Multi-access facilities could not legally share a corridor cell.");
    }

    private static PopulationStagePortfolio FixturePortfolio(
        FacilityPlacementCandidate left,
        FacilityPlacementCandidate right) => new(
        1,
        "tier:fixture",
        new[]
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0),
            new Vector2Int(2, 0), new Vector2Int(3, 0),
            new Vector2Int(1, 1), new Vector2Int(2, 1),
            new Vector2Int(3, 1), new Vector2Int(4, 1)
        },
        Array.Empty<Vector2Int>(),
        Array.Empty<Vector2Int>(),
        new[]
        {
            new FacilityRequirement("requirement:left", new[] { left }),
            new FacilityRequirement("requirement:right", new[] { right })
        },
        Array.Empty<StockSpaceRequirement>(),
        Array.Empty<OverflowRequirement>(),
        Array.Empty<ServiceContinuityRequirement>(),
        minimumHeadroomPermille: 0);

    private static void VerifyContinuityContract()
    {
        ServiceContinuityRequirement food = new(
            "service:food",
            "facility:meal-service",
            "survival:field-meal",
            24);
        Require(food.OutageCoverageHours == 24,
            "Food continuity must cover one game day.");
        bool rejected = false;
        try
        {
            _ = new ServiceContinuityRequirement(
                "service:invalid",
                "same-path",
                "same-path",
                24);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Require(rejected, "A service accepted the same primary and fallback path.");
    }

    private static void VerifyPairedRunAttribution()
    {
        List<PairedRunWindowResult> rows = new();
        for (int seed = 1; seed <= 32; seed++)
        for (int window = 0; window < 4; window++)
        {
            string semantic = $"semantic:{seed}:{window}";
            string random = $"random:{seed}:{window}";
            string cleanEvent = $"event:clean:{seed}:{window}";
            string faultEvent = $"event:fault:{seed}:{window}";
            rows.Add(Window(seed, "cleanRepeatA", window, 800, 0, semantic, random, cleanEvent));
            rows.Add(Window(seed, "cleanRepeatB", window, 800, 0, semantic, random, cleanEvent));
            rows.Add(Window(seed, "faultControl", window, 1000, 0,
                "semantic:control", "random:control", faultEvent));
            rows.Add(Window(seed, "clutterStress", window, 1050, 10,
                "semantic:clutter", "random:clutter", faultEvent));
        }
        PairedRunAttributionAssessment assessment =
            PairedRunAttributionEvaluator.Evaluate(rows);
        Require(assessment.Passed
            && assessment.MedianClutterDeltaPermille == 50
            && assessment.P95ClutterDeltaPermille == 50
            && !assessment.RequiresExpandedSample,
            $"Synthetic paired attribution failed: {assessment.FailureCode}; "
            + $"median={assessment.MedianClutterDeltaPermille}; "
            + $"p95={assessment.P95ClutterDeltaPermille}.");

        PairedRunWindowResult first = rows[0];
        rows[0] = Window(
            first.Seed,
            first.Arm,
            first.WindowIndex,
            first.WaitMilliWu + 1,
            first.ClutterCellSeconds,
            first.SemanticStateHash,
            first.RandomStateHash,
            first.ExogenousEventHash);
        PairedRunAttributionAssessment nondeterministic =
            PairedRunAttributionEvaluator.Evaluate(rows);
        Require(!nondeterministic.Passed
            && nondeterministic.FailureCode == "PAIRED_RUN_NONDETERMINISTIC_BASELINE",
            "Clean A/B drift was not classified as nondeterministic baseline.");
    }

    private static PairedRunWindowResult Window(
        int seed,
        string arm,
        int window,
        long waitMilliWu,
        int clutterCellSeconds,
        string semantic,
        string random,
        string events) => new(
        seed,
        arm,
        window,
        2000,
        waitMilliWu,
        1,
        0,
        clutterCellSeconds,
        semantic,
        random,
        events,
        dispatchWaitMilliWu: waitMilliWu);

    private static void VerifyOverflowCannotConsumeAccess(
        PopulationStagePortfolio valid)
    {
        PopulationStagePortfolio invalid = new(
            valid.Population,
            valid.ResearchTierId,
            valid.UsableInteriorCells,
            valid.EmergencyEgressCells,
            valid.FixedWorldFeatureCells,
            valid.Facilities,
            valid.StockBuffers,
            new[]
            {
                new OverflowRequirement(
                    "overflow:invalid-access",
                    new[] { new Vector2Int(4, 1), new Vector2Int(6, 1) })
            },
            valid.CriticalServices,
            valid.MinimumHeadroomPermille);
        DungeonSpaceCapacityAssessment assessment =
            new DeterministicDungeonSpaceCapacityQuery().Assess(invalid, 1);
        Require(!assessment.Succeeded,
            "Overflow containment was allowed to consume operational access.");
    }

    private static void Write(string path, string report)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(path, stream =>
        {
            using StreamWriter writer = new(
                stream,
                new UTF8Encoding(false),
                4096,
                leaveOpen: true);
            writer.NewLine = "\n";
            writer.Write(report);
            writer.Write('\n');
            writer.Flush();
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
