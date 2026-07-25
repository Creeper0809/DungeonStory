using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FacilityShopSaveSection :
    DungeonJsonSaveSection<DungeonFacilityShopSaveData>
{
    public const string Id = "facility-shop.state";

    private readonly IDailyFacilityShopRuntimeProvider runtimeProvider;
    private readonly IFacilityShopCatalog facilityCatalog;

    public FacilityShopSaveSection(
        IDailyFacilityShopRuntimeProvider runtimeProvider,
        IFacilityShopCatalog facilityCatalog)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.facilityCatalog = facilityCatalog
            ?? throw new ArgumentNullException(nameof(facilityCatalog));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;

    protected override DungeonFacilityShopSaveData CapturePayload()
    {
        DungeonFacilityShopSaveData destination = new DungeonFacilityShopSaveData
        {
            unlockedBuildingIds = facilityCatalog.Buildings
                .Where(building => building != null && building.unlocked)
                .Select(building => building.id)
                .OrderBy(id => id)
                .ToList()
        };
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
        HashSet<int> unlockedIds =
            new HashSet<int>(source.unlockedBuildingIds ?? new List<int>());
        foreach (BuildingSO building in facilityCatalog.Buildings
                     .Where(building => building != null))
        {
            if (unlockedIds.Contains(building.id))
            {
                building.unlocked = true;
            }
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
