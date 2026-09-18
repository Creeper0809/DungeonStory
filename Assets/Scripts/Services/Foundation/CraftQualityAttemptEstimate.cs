using System;

/// <summary>Read-only distribution query; never inspects a pending order's hidden roll.</summary>
public interface ICraftQualityProbabilityQuery
{
    double EstimateSuccessProbability(CraftsmanshipQualityTier minimumQuality,
        float weightedSkill, float facilityBonus, float toolBonus, float complexityPenalty);
}

public readonly struct CraftQualityAttemptEstimate
{
    private CraftQualityAttemptEstimate(bool available, string condition, double probability,
        int? maximumAttempts, double workPerAttempt, string materialId, int materialPerAttempt)
    {
        IsAvailable = available;
        Condition = condition;
        SuccessProbability = probability;
        MaximumAttempts = maximumAttempts;
        WorkPerAttempt = workPerAttempt;
        MaterialId = materialId;
        MaterialPerAttempt = materialPerAttempt;
    }

    public bool IsAvailable { get; }
    public string Condition { get; }
    public double SuccessProbability { get; }
    public int? MaximumAttempts { get; }
    public double WorkPerAttempt { get; }
    public string MaterialId { get; }
    public int MaterialPerAttempt { get; }
    public double ExpectedAttempts => SuccessProbability > 0d
        ? 1d / SuccessProbability : double.PositiveInfinity;
    public bool NeedsLowProbabilityWarning => IsAvailable && ExpectedAttempts > 20d;
    public double? SuccessWithinLimit => MaximumAttempts.HasValue
        ? 1d - Math.Pow(1d - SuccessProbability, MaximumAttempts.Value) : null;
    public double? MaximumCraftWork => MaximumAttempts.HasValue
        ? MaximumAttempts.Value * WorkPerAttempt : null;
    public long? MaximumGrossMaterial => MaximumAttempts.HasValue
        ? (long)MaximumAttempts.Value * MaterialPerAttempt : null;

    public static CraftQualityAttemptEstimate Unavailable(string reason) =>
        new(false, reason, 0d, null, 0d, string.Empty, 0);

    public static CraftQualityAttemptEstimate Create(double probability, int? maximumAttempts,
        double workPerAttempt, string materialId, int materialPerAttempt, string condition)
    {
        if (double.IsNaN(probability) || probability < 0d || probability > 1d
            || maximumAttempts < 0 || double.IsNaN(workPerAttempt)
            || double.IsInfinity(workPerAttempt) || workPerAttempt < 0d || materialPerAttempt < 0)
            throw new ArgumentOutOfRangeException(nameof(probability), "Invalid quality estimate inputs.");
        return new(true, condition, probability, maximumAttempts, workPerAttempt,
            materialId ?? string.Empty, materialPerAttempt);
    }
}
