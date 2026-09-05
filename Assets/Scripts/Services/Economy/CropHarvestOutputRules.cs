using System;
using UnityEngine;

public static class CropHarvestOutputRules
{
    public const string PerformanceFormulaId =
        "performance:work:harvest:yield";
    public const string SeedYieldEffectTargetId = "harvest:seed-yield";
    public const int MaximumReturnedSeedCount = 4;
    public const int SeedSelectionBonus = 1;
    public const float SoilDiagnosticsMultiplier = 1.05f;

    public static readonly ProductionOutputFactor EcologyYieldMaximum =
        new(11L, 10L);
    public static readonly ProductionOutputFactor SoilDiagnosticsMaximum =
        new(21L, 20L);

    public static int ResolveHarvestQuantity(
        int authoredYield,
        float outputMultiplier,
        float workerYieldMultiplier,
        float extremeYieldMultiplier,
        float ecologyYieldMultiplier,
        bool hasSoilDiagnostics)
    {
        if (authoredYield <= 0
            || !FinitePositive(outputMultiplier)
            || !FinitePositive(workerYieldMultiplier)
            || !FinitePositive(extremeYieldMultiplier)
            || !FinitePositive(ecologyYieldMultiplier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoredYield),
                "Crop harvest factors must be finite and positive.");
        }
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                authoredYield
                * outputMultiplier
                * workerYieldMultiplier
                * extremeYieldMultiplier
                * ecologyYieldMultiplier
                * (hasSoilDiagnostics
                    ? SoilDiagnosticsMultiplier
                    : 1f)));
    }

    public static int ResolveReturnedSeedQuantity(
        int returnedSeedCount,
        float extremeSeedMultiplier,
        bool hasSeedSelection)
    {
        if (returnedSeedCount < 0
            || float.IsNaN(extremeSeedMultiplier)
            || float.IsInfinity(extremeSeedMultiplier)
            || extremeSeedMultiplier < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(returnedSeedCount));
        }
        return checked(
            Mathf.Max(0, Mathf.RoundToInt(
                returnedSeedCount * extremeSeedMultiplier))
            + (hasSeedSelection ? SeedSelectionBonus : 0));
    }

    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}
