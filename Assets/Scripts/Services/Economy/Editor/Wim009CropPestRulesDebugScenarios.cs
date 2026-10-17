#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Wim009CropPestRulesDebugScenarios
{
    private const string VerminBloomDefinitionId =
        "seasonal:summer-vermin-bloom";

    private static readonly LossVector[] TenPercentVectors =
    {
        new("p10_zero", 0, 10, 0),
        new("p10_one", 1, 10, 1),
        new("p10_four", 4, 10, 4),
        new("p10_nine", 9, 10, 9),
        new("p10_ten", 10, 10, 9),
        new("p10_twenty", 20, 10, 18),
        new("p10_one_hundred_one", 101, 10, 91),
        new("p10_int_max", int.MaxValue, 10, 1932735283)
    };

    private static readonly LossVector[] OtherPercentVectors =
    {
        new("p0_twenty", 20, 0, 20),
        new("p5_four", 4, 5, 4),
        new("p5_twenty", 20, 5, 19)
    };

    public static bool Run(out string report)
    {
        List<string> lines = new()
        {
            "WIM009 crop-primary-batch-loss rules/authoring",
            "scope=pure output rule plus real seasonal asset inspection; no crop runtime, save, AI, or UI claim"
        };
        int passed = 0;
        string current = "not-started";
        try
        {
            foreach (LossVector vector in TenPercentVectors
                         .Concat(OtherPercentVectors))
            {
                current = vector.Name;
                int first = CropHarvestOutputRules.ApplyPrimaryBatchLoss(
                    vector.PrimaryBatchQuantity,
                    vector.LossPercent);
                Require(first == vector.ExpectedQuantity,
                    $"Expected {vector.ExpectedQuantity}, received {first}.");
                int repeated = CropHarvestOutputRules.ApplyPrimaryBatchLoss(
                    vector.PrimaryBatchQuantity,
                    vector.LossPercent);
                Require(repeated == first,
                    $"Repeated input changed {first} to {repeated}.");
                passed++;
            }

            ExpectArgumentOutOfRange(
                "negative-primary-batch",
                -1,
                10);
            passed++;
            ExpectArgumentOutOfRange(
                "negative-loss-percent",
                20,
                -1);
            passed++;
            ExpectArgumentOutOfRange(
                "loss-percent-above-ten",
                20,
                11);
            passed++;

            current = "post-rejection-determinism";
            Require(CropHarvestOutputRules.ApplyPrimaryBatchLoss(20, 10) == 18,
                "Invalid inputs changed a subsequent valid result.");
            passed++;

            current = "actual-seasonal-authoring";
            SeasonalWorldEventDefinitionSO[] definitions = Resources
                .LoadAll<SeasonalWorldEventDefinitionSO>(
                    "SO/V20/World/SeasonalEvents")
                .Where(value => value != null)
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ToArray();
            Require(definitions.Length == 28,
                $"Expected 28 real seasonal assets, found {definitions.Length}.");
            Require(definitions.Select(value => value.StableId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == definitions.Length,
                "Real seasonal assets contain duplicate stable IDs.");

            SeasonalWorldEventDefinitionSO[] vermin = definitions
                .Where(value => string.Equals(
                    value.StableId,
                    VerminBloomDefinitionId,
                    StringComparison.Ordinal))
                .ToArray();
            Require(vermin.Length == 1,
                "Expected exactly one real summer-vermin-bloom asset.");
            Require(vermin[0].cropPrimaryBatchLossPercent == 10,
                "Summer vermin bloom must author a 10 percent primary-batch loss.");
            Require(vermin[0].minimumDurationDays == 3
                    && vermin[0].maximumDurationDays == 5,
                "Summer vermin bloom duration must remain 3-5 days.");
            Require(definitions.Where(value => !ReferenceEquals(value, vermin[0]))
                    .All(value => value.cropPrimaryBatchLossPercent == 0),
                "A non-vermin seasonal asset authors crop primary-batch loss.");
            passed++;

            lines.Add($"PASS cases={passed}; assets={definitions.Length}; verminLoss=10; otherLoss=0; duration=3-5");
            report = string.Join("\n", lines);
            return true;
        }
        catch (Exception error)
        {
            lines.Add("FAIL case=" + current + "; passed=" + passed
                + "; " + error.GetType().Name + ": " + error.Message);
            report = string.Join("\n", lines);
            return false;
        }
    }

    private static void ExpectArgumentOutOfRange(
        string name,
        int primaryBatchQuantity,
        int lossPercent)
    {
        try
        {
            _ = CropHarvestOutputRules.ApplyPrimaryBatchLoss(
                primaryBatchQuantity,
                lossPercent);
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                name + " rejected with " + error.GetType().Name
                + " instead of ArgumentOutOfRangeException.",
                error);
        }

        throw new InvalidOperationException(
            name + " was accepted instead of rejecting before output.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct LossVector
    {
        public LossVector(
            string name,
            int primaryBatchQuantity,
            int lossPercent,
            int expectedQuantity)
        {
            Name = name;
            PrimaryBatchQuantity = primaryBatchQuantity;
            LossPercent = lossPercent;
            ExpectedQuantity = expectedQuantity;
        }

        public string Name { get; }
        public int PrimaryBatchQuantity { get; }
        public int LossPercent { get; }
        public int ExpectedQuantity { get; }
    }
}
#endif
