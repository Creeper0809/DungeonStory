using System;
using System.Collections.Generic;
using System.Linq;

public sealed class MetaProgressionSaveSection :
    DungeonJsonSaveSection<DungeonMetaProgressionSaveData>
{
    public const string Id = "meta.progression";

    private readonly IMetaProgressionRuntimeProvider runtimeProvider;

    public MetaProgressionSaveSection(IMetaProgressionRuntimeProvider runtimeProvider)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Foundation;

    protected override DungeonMetaProgressionSaveData CapturePayload()
    {
        DungeonMetaProgressionSaveData destination =
            new DungeonMetaProgressionSaveData();
        if (!runtimeProvider.TryGetRuntime(out MetaProgressionRuntime runtime))
        {
            return destination;
        }

        destination.lifetimeEarnedCurrency = runtime.State.LifetimeEarnedCurrency;
        destination.spentCurrency = runtime.State.SpentCurrency;
        destination.completedRunCount = runtime.State.CompletedRunCount;
        destination.upgradeLevels = runtime.State.UpgradeLevels
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new DungeonStringIntSaveEntry
            {
                key = pair.Key,
                value = pair.Value
            })
            .ToList();
        destination.preservedRecipeIds = runtime.State.PreservedRecipeIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        destination.runProgress = new DungeonMetaRunProgressSaveData
        {
            elapsedSeconds = runtime.RunProgress.ElapsedSeconds,
            currentDay = runtime.RunProgress.CurrentDay,
            settlementCount = runtime.RunProgress.SettlementCount,
            defendedInvasionCount = runtime.RunProgress.DefendedInvasionCount,
            maxThreatStage = runtime.RunProgress.MaxThreatStage,
            finalInvasionThreat = runtime.RunProgress.FinalInvasionThreat,
            offenseSuccessCount = runtime.RunProgress.OffenseSuccessCount,
            discoveredFacilityIds =
                runtime.RunProgress.DiscoveredFacilityIds.OrderBy(id => id).ToList(),
            unlockedRecipeIds = runtime.RunProgress.UnlockedRecipeIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList()
        };
        destination.ended = runtime.HasEnded;
        destination.hasLatestResult = runtime.LatestResult != null;
        if (runtime.LatestResult != null)
        {
            destination.latestResult = ToSaveData(runtime.LatestResult);
        }

        return destination;
    }

    protected override void RestorePayload(
        DungeonMetaProgressionSaveData source,
        DungeonGameRestoreReport report)
    {
        if (!runtimeProvider.TryGetRuntime(out MetaProgressionRuntime runtime))
        {
            report.AddWarning(
                "Meta progression runtime was not present; meta state was skipped.");
            return;
        }

        runtime.State.Merge(
            source.lifetimeEarnedCurrency,
            source.spentCurrency,
            (source.upgradeLevels ?? new List<DungeonStringIntSaveEntry>())
                .Where(entry => entry != null)
                .Select(entry =>
                    new KeyValuePair<string, int>(entry.key, entry.value)),
            source.preservedRecipeIds,
            source.completedRunCount);

        DungeonMetaRunProgressSaveData progress =
            source.runProgress ?? new DungeonMetaRunProgressSaveData();
        runtime.RunProgress.Restore(
            progress.elapsedSeconds,
            progress.currentDay,
            progress.settlementCount,
            progress.defendedInvasionCount,
            progress.maxThreatStage,
            progress.finalInvasionThreat,
            progress.offenseSuccessCount,
            progress.discoveredFacilityIds,
            progress.unlockedRecipeIds);
        runtime.RestoreRunState(
            source.ended,
            source.hasLatestResult ? ToRuntimeResult(source.latestResult) : null);
    }

    private static DungeonRunResultSaveData ToSaveData(RunResultSnapshot result)
    {
        return new DungeonRunResultSaveData
        {
            ownerName = result.ownerName,
            endReason = result.endReason,
            survivalSeconds = result.survivalSeconds,
            survivedOperatingDays = result.survivedOperatingDays,
            settlementCount = result.settlementCount,
            defendedInvasionCount = result.defendedInvasionCount,
            maxThreatStage = result.maxThreatStage,
            finalInvasionThreat = result.finalInvasionThreat,
            firstDiscoveredFacilityCount = result.firstDiscoveredFacilityCount,
            firstUnlockedRecipeCount = result.firstUnlockedRecipeCount,
            offenseSuccessCount = result.offenseSuccessCount,
            difficultyMultiplier = result.difficultyMultiplier,
            difficulty = result.difficulty,
            legacyCurrency = result.legacyCurrency,
            outcome = result.outcome
        };
    }

    private static RunResultSnapshot ToRuntimeResult(
        DungeonRunResultSaveData result)
    {
        result ??= new DungeonRunResultSaveData();
        return new RunResultSnapshot(
            result.ownerName,
            result.endReason,
            result.survivalSeconds,
            result.survivedOperatingDays,
            result.settlementCount,
            result.defendedInvasionCount,
            result.maxThreatStage,
            result.finalInvasionThreat,
            result.firstDiscoveredFacilityCount,
            result.firstUnlockedRecipeCount,
            result.offenseSuccessCount,
            result.difficultyMultiplier,
            result.legacyCurrency,
            result.outcome,
            result.difficulty);
    }
}
