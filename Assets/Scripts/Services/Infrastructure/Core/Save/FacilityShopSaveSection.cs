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
            purchaseSequence = source.PurchaseSequence,
            dailyOfferPurchaseStateVersion = 1,
            purchasedDailyOfferKeys = source.PurchasedDailyOfferKeys.ToList(),
            basicPurchaseBuildingIds = source.BasicPurchaseBuildingIds.ToList(),
            acquiredBlueprintIds = source.AcquiredBlueprintIds.ToList()
        };
    }

    protected override void ValidateRawPayload(string payloadJson)
    {
        RequireTopLevelArrayFields(
            payloadJson,
            nameof(DungeonFacilityShopSaveData.basicPurchaseBuildingIds),
            nameof(DungeonFacilityShopSaveData.acquiredBlueprintIds));
        var json = Newtonsoft.Json.Linq.JObject.Parse(payloadJson,
            new Newtonsoft.Json.Linq.JsonLoadSettings
            { DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error });
        var version = json[nameof(DungeonFacilityShopSaveData.dailyOfferPurchaseStateVersion)];
        var keys = json[nameof(DungeonFacilityShopSaveData.purchasedDailyOfferKeys)];
        if (version == null && keys == null) return; // 기존 V1은 저장하지 않은 내역을 합성하지 않는다.
        if (version?.Type != Newtonsoft.Json.Linq.JTokenType.Integer || version.ToObject<int>() != 1)
            throw new InvalidOperationException("Facility-shop daily purchase state version is invalid.");
        RequireTopLevelArrayFields(payloadJson, nameof(DungeonFacilityShopSaveData.purchasedDailyOfferKeys));
        if (keys.Any(key => key.Type != Newtonsoft.Json.Linq.JTokenType.String))
            throw new InvalidOperationException("Facility-shop daily purchase keys must be strings.");
    }

    protected override void NormalizeRestorePayload(
        DungeonFacilityShopSaveData payload, DungeonGameRestoreReport report)
    {
        if (payload.dailyOfferPurchaseStateVersion != 0) return;
        // 과거 UI 캐시는 저장되지 않았다. 기존 저장의 해금/자금/구매 번호는 그대로 보존한다.
        payload.dailyOfferPurchaseStateVersion = 1;
        payload.purchasedDailyOfferKeys = new List<string>();
    }

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
        if (payload.dailyOfferPurchaseStateVersion != 1 || payload.purchasedDailyOfferKeys == null
            || payload.purchasedDailyOfferKeys.Any(key =>
                !FacilityShopUnlockState.IsDailyOfferKey(key, payload.currentOfferDay))
            || !payload.purchasedDailyOfferKeys.SequenceEqual(
                payload.purchasedDailyOfferKeys.Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)))
            report.AddError("Facility-shop daily purchase keys are invalid, duplicate or unordered.");
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
        if (payload.purchaseSequence < 0)
        {
            report.AddError("Facility-shop payload has an invalid purchase sequence.");
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
            source.acquiredBlueprintIds,
            source.purchaseSequence,
            source.purchasedDailyOfferKeys));
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
