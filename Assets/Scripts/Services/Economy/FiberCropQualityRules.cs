using UnityEngine;

/// <summary>
/// V23 fiber crops have no material quality. Genetics may increase throughput,
/// but that gain always increases water and fertility demand.
/// </summary>
public readonly struct FiberCropResourceInput
{
    public FiberCropResourceInput(float yieldMultiplier, float growthMultiplier)
    {
        YieldMultiplier = Mathf.Clamp(yieldMultiplier, 0.9f, 1.1f);
        GrowthMultiplier = Mathf.Clamp(growthMultiplier, 0.84f, 1.16f);
    }

    public float YieldMultiplier { get; }
    public float GrowthMultiplier { get; }
}

public readonly struct FiberCropResourceResult
{
    public FiberCropResourceResult(
        float waterMultiplier,
        float fertilityMultiplier)
    {
        WaterMultiplier = Mathf.Max(1f, waterMultiplier);
        FertilityMultiplier = Mathf.Max(1f, fertilityMultiplier);
    }

    public float WaterMultiplier { get; }
    public float FertilityMultiplier { get; }
}

public static class FiberCropResourceRules
{
    public static FiberCropResourceResult Evaluate(FiberCropResourceInput input)
    {
        float positiveYield = Mathf.Clamp01((input.YieldMultiplier - 1f) / 0.1f);
        float positiveGrowth = Mathf.Clamp01((input.GrowthMultiplier - 1f) / 0.16f);
        return new FiberCropResourceResult(
            1f + 0.5f * positiveYield + 0.35f * positiveGrowth,
            1f + 0.6f * positiveYield + 0.4f * positiveGrowth);
    }
}
