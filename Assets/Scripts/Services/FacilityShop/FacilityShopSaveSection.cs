using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FacilityShopSaveSection :
    DungeonJsonSaveSection<DungeonFacilityShopSaveData>
{
    public const string Id = "facility-shop.state";

    private readonly IDailyFacilityShopRuntimeProvider runtimeProvider;
    private readonly IBlueprintResearchRuntimeProvider researchRuntimeProvider;

    public FacilityShopSaveSection(
        IDailyFacilityShopRuntimeProvider runtimeProvider,
        IBlueprintResearchRuntimeProvider researchRuntimeProvider)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.researchRuntimeProvider = researchRuntimeProvider
            ?? throw new ArgumentNullException(nameof(researchRuntimeProvider));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[] { BlueprintResearchSaveSection.Id };

    protected override DungeonFacilityShopSaveData CapturePayload()
    {
        DungeonFacilityShopSaveData destination = new DungeonFacilityShopSaveData();
        if (researchRuntimeProvider.TryGetRuntime(out BlueprintResearchRuntime research))
        {
            destination.unlockedBuildingIds =
                research.State.UnlockedBuildingIds.OrderBy(id => id).ToList();
        }

        if (!runtimeProvider.TryGetRuntime(out DailyFacilityShopRuntime runtime))
        {
            return destination;
        }

        destination.currentOfferDay = runtime.CurrentOfferDay;
        destination.basicPurchaseBuildingIds =
            runtime.UnlockState.BasicPurchaseBuildingIds.OrderBy(id => id).ToList();
        destination.acquiredBlueprintIds =
            runtime.UnlockState.AcquiredBlueprintIds.OrderBy(id => id).ToList();
        return destination;
    }

    protected override void RestorePayload(
        DungeonFacilityShopSaveData source,
        DungeonGameRestoreReport report)
    {
        IReadOnlyList<int> unlockedIds =
            source.unlockedBuildingIds ?? new List<int>();
        if (researchRuntimeProvider.TryGetRuntime(out BlueprintResearchRuntime research))
        {
            foreach (int buildingId in unlockedIds)
            {
                research.State.RestoreUnlockedBuildingId(buildingId);
            }
        }
        else if (unlockedIds.Count > 0)
        {
            report.AddWarning(
                "Research runtime was not present; legacy facility unlocks were skipped.");
        }

        if (!runtimeProvider.TryGetRuntime(out DailyFacilityShopRuntime runtime))
        {
            report.AddWarning(
                "Facility shop runtime was not present; shop state was skipped.");
            return;
        }

        runtime.RestoreState(
            source.currentOfferDay,
            source.basicPurchaseBuildingIds,
            source.acquiredBlueprintIds);
    }
}
