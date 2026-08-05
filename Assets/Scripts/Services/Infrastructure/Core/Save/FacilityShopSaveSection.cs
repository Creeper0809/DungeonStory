using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FacilityShopSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonFacilityShopSaveData,
        FacilityShopRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "facility-shop.state";

    private readonly IFacilityShopPersistence persistence;
    private readonly IFacilityShopDefinitionCatalog catalog;

    public FacilityShopSaveSection(
        IFacilityShopPersistence persistence,
        IFacilityShopDefinitionCatalog catalog)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonFacilityShopSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;

    protected override DungeonFacilityShopSaveData CapturePayload()
    {
        FacilityShopStateSnapshot source = persistence.CaptureState();
        return new DungeonFacilityShopSaveData
        {
            currentOfferDay = source.CurrentOfferDay,
            basicPurchaseBuildingIds = source.BasicPurchaseBuildingIds.ToList(),
            acquiredBlueprintIds = source.AcquiredBlueprintIds.ToList()
        };
    }

    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(
            payloadJson,
            nameof(DungeonFacilityShopSaveData.basicPurchaseBuildingIds),
            nameof(DungeonFacilityShopSaveData.acquiredBlueprintIds));

    private void ValidatePayload(
        DungeonFacilityShopSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null
            || payload.basicPurchaseBuildingIds == null
            || payload.acquiredBlueprintIds == null)
        {
            report.AddError("Facility-shop payload or unlock lists are null.");
            return;
        }
        if (payload.version != DungeonFacilityShopSaveData.CurrentVersion)
        {
            report.AddError(
                $"Facility-shop payload version {payload.version} is unsupported.");
        }
        if (payload.currentOfferDay < 1)
        {
            report.AddError("Facility-shop payload has an invalid offer day.");
            return;
        }

        ValidateIds(
            payload.basicPurchaseBuildingIds,
            catalog.Buildings.Select(building => building.Id),
            "basic-purchase building",
            report);
        ValidateIds(
            payload.acquiredBlueprintIds,
            catalog.BlueprintIds,
            "acquired blueprint",
            report);
    }

    protected override FacilityShopRestoreCandidate BuildRestoreCandidate(
        DungeonFacilityShopSaveData source)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ValidatePayload(source, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Facility-shop restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        return persistence.BuildRestoreCandidate(new FacilityShopStateSnapshot(
            source.currentOfferDay,
            source.basicPurchaseBuildingIds,
            source.acquiredBlueprintIds));
    }

    protected override void PublishRestoreCandidate(
        FacilityShopRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);

    private static void ValidateIds(
        IReadOnlyList<int> savedIds,
        IEnumerable<int> validIds,
        string label,
        DungeonGameRestoreReport report)
    {
        HashSet<int> valid = (validIds ?? Array.Empty<int>()).ToHashSet();
        int previousId = -1;
        foreach (int id in savedIds)
        {
            if (id < 0 || !valid.Contains(id))
            {
                report.AddError(
                    $"Facility-shop payload references missing {label} {id}.");
            }
            else if (id <= previousId)
            {
                report.AddError(
                    $"Facility-shop payload contains duplicate or unordered {label} {id}.");
            }
            previousId = id;
        }
    }
}
