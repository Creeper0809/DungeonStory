using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MetaProgressionSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonMetaProgressionSaveData,
        MetaProgressionRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "meta.progression";

    private readonly IMetaProgressionPersistencePort runtime;

    public MetaProgressionSaveSection(
        IMetaProgressionPersistencePort runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => 1;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Foundation;

    protected override DungeonMetaProgressionSaveData CapturePayload()
    {
        DungeonMetaProgressionSaveData destination =
            new DungeonMetaProgressionSaveData();
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

    protected override MetaProgressionRestoreCandidate BuildRestoreCandidate(
        DungeonMetaProgressionSaveData payload) =>
        runtime.PrepareRestore(payload);

    protected override void PublishRestoreCandidate(
        MetaProgressionRestoreCandidate candidate) =>
        runtime.Restore(candidate);

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
            survivalPressure = result.survivalPressure,
            legacyCurrency = result.legacyCurrency,
            outcome = result.outcome
        };
    }

}
