using System;

/// <summary>Immutable completed meta-upgrade purchase transition.</summary>
public sealed class MetaUpgradePurchaseDomainFact
{
    public MetaUpgradePurchaseDomainFact(
        string upgradeId,
        string displayName,
        int previousLevel,
        int currentLevel,
        int cost,
        int absoluteDay)
    {
        UpgradeId = upgradeId?.Trim() ?? string.Empty;
        DisplayName = displayName?.Trim() ?? string.Empty;
        PreviousLevel = previousLevel;
        CurrentLevel = currentLevel;
        Cost = cost;
        AbsoluteDay = absoluteDay;
    }

    public string UpgradeId { get; }
    public string DisplayName { get; }
    public int PreviousLevel { get; }
    public int CurrentLevel { get; }
    public int Cost { get; }
    public int AbsoluteDay { get; }
}

/// <summary>Immutable completed run result and its meta-progression sequence.</summary>
public sealed class MetaRunResultDomainFact
{
    private readonly RunResultSnapshot result;

    public MetaRunResultDomainFact(
        int runSequence,
        int absoluteDay,
        RunResultSnapshot result)
    {
        RunSequence = runSequence;
        AbsoluteDay = absoluteDay;
        this.result = Copy(
            result ?? throw new ArgumentNullException(nameof(result)));
    }

    public int RunSequence { get; }
    public int AbsoluteDay { get; }
    public RunResultSnapshot Snapshot() => Copy(result);

    private static RunResultSnapshot Copy(RunResultSnapshot source) => new(
        source.ownerName,
        source.endReason,
        source.survivalSeconds,
        source.survivedOperatingDays,
        source.settlementCount,
        source.defendedInvasionCount,
        source.maxThreatStage,
        source.finalInvasionThreat,
        source.firstDiscoveredFacilityCount,
        source.firstUnlockedRecipeCount,
        source.offenseSuccessCount,
        source.difficultyMultiplier,
        source.legacyCurrency,
        source.outcome,
        source.difficulty,
        source.survivalPressure,
        source.completedMilestoneIds,
        source.committedChoices);
}
