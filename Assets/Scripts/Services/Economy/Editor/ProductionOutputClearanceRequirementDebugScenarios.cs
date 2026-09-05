#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class ProductionOutputClearanceRequirementDebugScenarios
{
    private const string SourceDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [MenuItem("DungeonStory/V27/Production/Run Output Clearance Requirement")]
    public static void RunAll()
    {
        VerifyMinimumTwoCycleCapacity();
        VerifyFractionalRequirementRoundsToWholeCycle();
        VerifyExactFourCycleBoundaryPasses();
        VerifyFourPointZeroZeroOneCyclesRequiresBackpressure();
        VerifyCeilingAndDeterminism();
        VerifyInvalidInputsFailLoudly();
        Debug.Log("[ProductionOutputClearanceRequirement] focused scenarios passed.");
    }

    private static void VerifyMinimumTwoCycleCapacity()
    {
        ProductionOutputClearanceRequirementAssessment result =
            ProductionOutputClearanceRequirementProjector.Assess(
                1_000L,
                Profile(p95MilliHours: 500L, peakGramsPerHour: 1_000L));
        Require(result.IsAccepted, "A half-cycle clearance must be accepted.");
        Require(result.MeasuredClearanceDemandGrams == 500L,
            "Half-cycle clearance demand drifted.");
        Require(result.RequiredCapacityGrams == 2_000L
            && result.PublishedCapacityGrams == 2_000L
            && result.RequiredCycleMilliCycles == 2_000L
            && result.RequiredWholeCycles == 2L
            && result.PublishedWholeCycles == 2L,
            "The two-cycle minimum was not enforced exactly.");
        Require(string.IsNullOrEmpty(result.FailureCode),
            "An accepted assessment published a failure code.");
    }

    private static void VerifyFractionalRequirementRoundsToWholeCycle()
    {
        ProductionOutputClearanceRequirementAssessment result =
            ProductionOutputClearanceRequirementProjector.Assess(
                1_000L,
                Profile(p95MilliHours: 2_001L, peakGramsPerHour: 1_000L));
        Require(result.IsAccepted,
            "A 2.001-cycle requirement should fit within the four-cycle limit.");
        Require(result.RequiredCapacityGrams == 2_001L
            && result.RequiredCycleMilliCycles == 2_001L
            && result.RequiredWholeCycles == 3L
            && result.PublishedWholeCycles == 3L
            && result.PublishedCapacityGrams == 3_000L,
            "A fractional capacity requirement was not rounded to a whole cycle.");
    }

    private static void VerifyExactFourCycleBoundaryPasses()
    {
        ProductionOutputClearanceRequirementAssessment result =
            ProductionOutputClearanceRequirementProjector.Assess(
                1_000L,
                Profile(p95MilliHours: 4_000L, peakGramsPerHour: 1_000L));
        Require(result.IsAccepted,
            "Exactly 4.000 cycles must pass the clearance gate.");
        Require(result.RequiredCapacityGrams == 4_000L
            && result.PublishedCapacityGrams == 4_000L
            && result.RequiredCycleMilliCycles == 4_000L
            && result.RequiredWholeCycles == 4L
            && result.PublishedWholeCycles == 4L,
            "The exact 4.000-cycle boundary drifted.");
    }

    private static void VerifyFourPointZeroZeroOneCyclesRequiresBackpressure()
    {
        ProductionOutputClearanceRequirementAssessment result =
            ProductionOutputClearanceRequirementProjector.Assess(
                1_000L,
                Profile(p95MilliHours: 4_001L, peakGramsPerHour: 1_000L));
        Require(!result.IsAccepted
            && result.CanPublishBoundedCapacity
            && !result.IsBlockingCritical
            && result.RequiresBackpressure
            && result.Disposition
                == ProductionOutputClearanceDisposition.BackpressureExpected,
            "4.001 cycles must produce a publishable backpressure disposition.");
        Require(result.RequiredCapacityGrams == 4_001L
            && result.PublishedCapacityGrams == 4_000L
            && result.RequiredCycleMilliCycles == 4_001L
            && result.RequiredWholeCycles == 5L
            && result.PublishedWholeCycles == 4L,
            "Backpressure demand expanded the physical buffer past four cycles.");
        Require(string.IsNullOrEmpty(result.FailureCode),
            "A nonblocking backpressure result published a failure code.");
        Require(string.Equals(
                result.DiagnosticCode,
                ProductionOutputClearanceRequirementProjector
                    .BackpressureExpectedDiagnosticCode,
                StringComparison.Ordinal),
            "The 4.001-cycle boundary did not publish the required diagnostic.");
    }

    private static void VerifyCeilingAndDeterminism()
    {
        ProductionOutputClearanceProfileSnapshot profile = Profile(
            p95MilliHours: 1L,
            peakGramsPerHour: 1_001L);
        ProductionOutputClearanceRequirementAssessment first =
            ProductionOutputClearanceRequirementProjector.Assess(1_000L, profile);
        ProductionOutputClearanceRequirementAssessment second =
            ProductionOutputClearanceRequirementProjector.Assess(1_000L, profile);
        Require(first.MeasuredClearanceDemandGrams == 2L,
            "Fractional measured grams were not rounded upward.");
        Require(string.Equals(
                first.SourceDigest,
                second.SourceDigest,
                StringComparison.Ordinal),
            "Identical clearance inputs produced different semantic digests.");
    }

    private static void VerifyInvalidInputsFailLoudly()
    {
        Expect<ArgumentOutOfRangeException>(() =>
            ProductionOutputClearanceRequirementProjector.Assess(
                0L,
                Profile(1L, 1L)));
        Expect<ArgumentOutOfRangeException>(() =>
            new ProductionOutputClearanceProfileSnapshot(
                -1L,
                1L,
                SourceDigest));
        Expect<ArgumentException>(() =>
            new ProductionOutputClearanceProfileSnapshot(
                1L,
                1L,
                SourceDigest.ToUpperInvariant()));
        Expect<OverflowException>(() =>
            ProductionOutputClearanceRequirementProjector.Assess(
                long.MaxValue,
                Profile(1L, 1L)));
    }

    private static ProductionOutputClearanceProfileSnapshot Profile(
        long p95MilliHours,
        long peakGramsPerHour) => new(
            p95MilliHours,
            peakGramsPerHour,
            SourceDigest);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Expect<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(
            $"Expected {typeof(TException).Name} was not thrown.");
    }
}
#endif
