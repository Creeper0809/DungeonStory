using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MetaProgressionRestoreCandidate
{
    internal MetaProgressionRestoreCandidate(
        MetaProgressionAggregateState progression,
        MetaRunProgressAggregateState runProgress,
        MetaRunLifecycleAggregateState lifecycle)
    {
        Progression = progression
            ?? throw new ArgumentNullException(nameof(progression));
        RunProgress = runProgress
            ?? throw new ArgumentNullException(nameof(runProgress));
        Lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    internal MetaProgressionAggregateState Progression { get; }
    internal MetaRunProgressAggregateState RunProgress { get; }
    internal MetaRunLifecycleAggregateState Lifecycle { get; }

    public void CommitTo(DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        if (aggregateRootStore == null)
        {
            throw new ArgumentNullException(nameof(aggregateRootStore));
        }

        aggregateRootStore.Replace(Progression);
        aggregateRootStore.Replace(RunProgress);
        aggregateRootStore.Replace(Lifecycle);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class MetaProgressionRestoreBuilder
{
    public static MetaProgressionRestoreCandidate Build(
        DungeonMetaProgressionSaveData source,
        IMetaUpgradeDefinitionCatalog catalog,
        float currentTime)
    {
        if (source == null
            || source.upgradeLevels == null
            || source.preservedRecipeIds == null
            || source.runProgress == null
            || source.latestResult == null)
        {
            throw new InvalidOperationException(
                "Meta progression V1 payload is missing a required object or collection.");
        }
        if (source.lifetimeEarnedCurrency < 0
            || source.spentCurrency < 0
            || source.spentCurrency > source.lifetimeEarnedCurrency
            || source.completedRunCount < 0
            || !float.IsFinite(currentTime))
        {
            throw new InvalidOperationException(
                "Meta progression currency, run count, or restore clock is invalid.");
        }
        if (source.ended != source.hasLatestResult)
        {
            throw new InvalidOperationException(
                "Meta progression ended/latest-result flags are inconsistent.");
        }

        MetaProgressionAggregateState progression = new()
        {
            LifetimeEarnedCurrency = source.lifetimeEarnedCurrency,
            SpentCurrency = source.spentCurrency,
            CompletedRunCount = source.completedRunCount
        };
        foreach (DungeonStringIntSaveEntry entry in source.upgradeLevels)
        {
            if (entry == null)
            {
                throw new InvalidOperationException(
                    "Meta upgrade-level collection contains null.");
            }
            RequireCanonicalId(entry.key, "meta upgrade");
            MetaUpgradeDefinition definition = catalog.Get(entry.key);
            if (definition == null
                || entry.value < 1
                || entry.value > definition.maxLevel
                || !progression.UpgradeLevels.TryAdd(entry.key, entry.value))
            {
                throw new InvalidOperationException(
                    $"Meta upgrade '{entry.key}' is duplicate, unknown, or out of range.");
            }
        }
        foreach (string recipeId in source.preservedRecipeIds)
        {
            RequireCanonicalId(recipeId, "preserved recipe");
            if (!progression.PreservedRecipeIds.Add(recipeId))
            {
                throw new InvalidOperationException(
                    $"Duplicate preserved recipe id '{recipeId}'.");
            }
        }

        MetaRunProgressAggregateState runProgress = BuildRunProgress(
            source.runProgress,
            currentTime);
        RunResultSnapshot latest = source.hasLatestResult
            ? BuildRunResult(source.latestResult, source.lifetimeEarnedCurrency)
            : null;
        MetaRunLifecycleAggregateState lifecycle = new()
        {
            Ended = source.ended,
            LatestResult = latest
        };
        return new MetaProgressionRestoreCandidate(
            progression,
            runProgress,
            lifecycle);
    }

    private static MetaRunProgressAggregateState BuildRunProgress(
        DungeonMetaRunProgressSaveData source,
        float currentTime)
    {
        if (!IsFiniteNonNegative(source.elapsedSeconds)
            || source.currentDay < 1
            || source.settlementCount < 0
            || source.defendedInvasionCount < 0
            || source.offenseSuccessCount < 0
            || !Enum.IsDefined(typeof(InvasionThreatStage), source.maxThreatStage)
            || !IsFiniteNonNegative(source.finalInvasionThreat)
            || source.discoveredFacilityIds == null
            || source.unlockedRecipeIds == null)
        {
            throw new InvalidOperationException(
                "Meta run progress has missing collections or out-of-range values.");
        }

        MetaRunProgressAggregateState restored = new()
        {
            RunStartTime = currentTime - source.elapsedSeconds,
            CurrentDay = source.currentDay,
            SettlementCount = source.settlementCount,
            DefendedInvasionCount = source.defendedInvasionCount,
            MaxThreatStage = source.maxThreatStage,
            FinalInvasionThreat = source.finalInvasionThreat,
            OffenseSuccessCount = source.offenseSuccessCount
        };
        foreach (int id in source.discoveredFacilityIds)
        {
            if (id < 0 || !restored.DiscoveredFacilityIds.Add(id))
            {
                throw new InvalidOperationException(
                    $"Discovered facility id '{id}' is negative or duplicate.");
            }
        }
        foreach (string id in source.unlockedRecipeIds)
        {
            RequireCanonicalId(id, "run-progress recipe");
            if (!restored.UnlockedRecipeIds.Add(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate run-progress recipe id '{id}'.");
            }
        }
        return restored;
    }

    private static RunResultSnapshot BuildRunResult(
        DungeonRunResultSaveData source,
        int lifetimeEarnedCurrency)
    {
        RequireCanonicalTextOrEmpty(source.ownerName, "run-result owner name");
        RequireCanonicalTextOrEmpty(source.endReason, "run-result end reason");
        if (!IsFiniteNonNegative(source.survivalSeconds)
            || source.survivedOperatingDays < 0
            || source.settlementCount < 0
            || source.defendedInvasionCount < 0
            || source.firstDiscoveredFacilityCount < 0
            || source.firstUnlockedRecipeCount < 0
            || source.offenseSuccessCount < 0
            || !Enum.IsDefined(typeof(InvasionThreatStage), source.maxThreatStage)
            || !IsFiniteNonNegative(source.finalInvasionThreat)
            || !float.IsFinite(source.difficultyMultiplier)
            || source.difficultyMultiplier < 0.1f
            || !Enum.IsDefined(typeof(DungeonDifficulty), source.difficulty)
            || !Enum.IsDefined(
                typeof(DungeonSurvivalPressure),
                source.survivalPressure)
            || source.legacyCurrency < 0
            || source.legacyCurrency > lifetimeEarnedCurrency
            || !Enum.IsDefined(typeof(DungeonRunOutcome), source.outcome))
        {
            throw new InvalidOperationException(
                "Meta run result contains an invalid enum, reference, or numeric range.");
        }
        return new RunResultSnapshot(
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
            source.survivalPressure);
    }

    private static bool IsFiniteNonNegative(float value) =>
        float.IsFinite(value) && value >= 0f;

    private static void RequireCanonicalId(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} id must be non-empty and canonical.");
        }
    }

    private static void RequireCanonicalTextOrEmpty(string value, string label)
    {
        if (value == null
            || (!string.IsNullOrEmpty(value)
                && !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{label} must be non-null and canonical.");
        }
    }
}
