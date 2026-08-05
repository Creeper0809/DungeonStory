using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public static class FacilityShopOfferTypeIds
{
    public const string Building = "facility-shop.offer.building";
    public const string Blueprint = "facility-shop.offer.blueprint";
}

public static class FacilityInstallationKitItemIds
{
    public const string Prefix = "facility-kit:";

    public static string ForBuilding(int buildingId)
    {
        return buildingId >= 0 ? $"{Prefix}{buildingId}" : string.Empty;
    }

    public static string ForBuilding(BuildingSO building)
    {
        return building != null ? ForBuilding(building.id) : string.Empty;
    }

    public static bool TryGetBuildingId(string itemId, out int buildingId)
    {
        buildingId = -1;
        string normalized = itemId?.Trim() ?? string.Empty;
        return normalized.StartsWith(Prefix, StringComparison.Ordinal)
            && int.TryParse(
                normalized.Substring(Prefix.Length),
                out buildingId)
            && buildingId >= 0;
    }
}

[Serializable]
public sealed class FacilityShopOfferSnapshot
{
    public FacilityShopOfferSnapshot(
        string offerTypeId,
        string typeDisplayName,
        FacilityShopRarity rarity,
        string displayName,
        int cost,
        int star,
        bool basicPurchase)
    {
        this.offerTypeId = offerTypeId ?? string.Empty;
        this.typeDisplayName = typeDisplayName ?? string.Empty;
        this.rarity = rarity;
        this.displayName = displayName ?? string.Empty;
        this.cost = Mathf.Max(0, cost);
        this.star = Mathf.Max(0, star);
        this.basicPurchase = basicPurchase;
    }

    public string offerTypeId { get; }
    public string typeDisplayName { get; }
    public FacilityShopRarity rarity { get; }
    public string displayName { get; }
    public int cost { get; }
    public int star { get; }
    public bool basicPurchase { get; }

    public string ToSummaryText()
    {
        string rarityText = rarity == FacilityShopRarity.Common ? string.Empty : $" / {rarity}";
        string basicText = basicPurchase ? " / 기본 구매" : string.Empty;
        string starText = star > 0 ? $" / {star}성" : string.Empty;
        return $"{typeDisplayName}: {displayName}{starText} / 비용 {cost}{rarityText}{basicText}";
    }
}

public abstract class FacilityShopOffer
{
    protected FacilityShopOffer(
        int cost,
        FacilityShopRarity rarity,
        bool basicPurchase,
        bool randomOffer)
    {
        Cost = Mathf.Max(0, cost);
        Rarity = rarity;
        IsBasicPurchase = basicPurchase;
        IsRandomOffer = randomOffer;
    }

    public abstract string OfferTypeId { get; }
    public abstract string TypeDisplayName { get; }
    public int Cost { get; }
    public FacilityShopRarity Rarity { get; }
    public bool IsBasicPurchase { get; }
    public bool IsRandomOffer { get; }
    public abstract bool IsValid { get; }
    public abstract int Star { get; }
    public abstract int DataId { get; }
    public abstract string DisplayName { get; }

    protected internal abstract string ApplyPurchase(FacilityShopUnlockState unlockState);

    public FacilityShopOfferSnapshot ToSnapshot()
    {
        return new FacilityShopOfferSnapshot(
            OfferTypeId,
            TypeDisplayName,
            Rarity,
            DisplayName,
            Cost,
            Star,
            IsBasicPurchase);
    }
}

public sealed class FacilityBuildingOffer : FacilityShopOffer
{
    public FacilityBuildingOffer(
        BuildingSO building,
        int cost,
        FacilityShopRarity rarity,
        bool basicPurchase,
        bool randomOffer)
        : base(cost, rarity, basicPurchase, randomOffer)
    {
        Building = building;
    }

    public BuildingSO Building { get; }
    public override string OfferTypeId => FacilityShopOfferTypeIds.Building;
    public override string TypeDisplayName => "시설";
    public override bool IsValid => Building != null;
    public override int Star => FacilityShopService.GetBuildingStar(Building);
    public override int DataId => Building != null ? Building.id : -1;
    public override string DisplayName => FacilityShopService.GetBuildingName(Building);

    protected internal override string ApplyPurchase(FacilityShopUnlockState unlockState)
    {
        if (IsBasicPurchase)
        {
            unlockState?.UnlockBasicPurchase(Building);
        }

        return $"{DisplayName} 구매 완료";
    }
}

public sealed class FacilityBlueprintOffer : FacilityShopOffer
{
    public FacilityBlueprintOffer(
        FacilityBlueprintSO blueprint,
        int cost,
        FacilityShopRarity rarity,
        bool randomOffer)
        : base(cost, rarity, false, randomOffer)
    {
        Blueprint = blueprint;
    }

    public FacilityBlueprintSO Blueprint { get; }
    public override string OfferTypeId => FacilityShopOfferTypeIds.Blueprint;
    public override string TypeDisplayName => "설계도";
    public override bool IsValid => Blueprint != null;
    public override int Star => 0;
    public override int DataId => Blueprint != null ? Blueprint.id : -1;
    public override string DisplayName => Blueprint != null ? Blueprint.DisplayName : "설계도";

    protected internal override string ApplyPurchase(FacilityShopUnlockState unlockState)
    {
        unlockState?.MarkBlueprintAcquired(Blueprint);
        return $"{DisplayName} 설계도 획득";
    }
}

public readonly struct FacilityShopPurchaseResult
{
    public readonly bool success;
    public readonly FacilityShopOfferSnapshot offer;
    public readonly FacilityShopOffer purchasedOffer;
    public readonly string offerTypeId;
    public readonly int dataId;
    public readonly int cost;
    public readonly string message;

    public FacilityShopPurchaseResult(bool success, FacilityShopOffer offer, int cost, string message)
    {
        this.success = success;
        this.offer = offer != null ? offer.ToSnapshot() : null;
        purchasedOffer = offer;
        offerTypeId = offer?.OfferTypeId ?? string.Empty;
        dataId = offer?.DataId ?? -1;
        this.cost = Mathf.Max(0, cost);
        this.message = message ?? string.Empty;
    }

    public bool TryGetBuilding(out BuildingSO building)
    {
        building = (purchasedOffer as FacilityBuildingOffer)?.Building;
        return building != null;
    }

    public bool TryGetBlueprint(out FacilityBlueprintSO blueprint)
    {
        blueprint = (purchasedOffer as FacilityBlueprintOffer)?.Blueprint;
        return blueprint != null;
    }
}

public readonly struct FacilityShopPurchasedEvent
{
    public FacilityShopPurchaseResult result { get; }

    public FacilityShopPurchasedEvent(FacilityShopPurchaseResult result)
    {
        this.result = result;
    }
}

public static class FacilityShopUnityUnlockAdapter
{
    public static bool UnlockBasicPurchase(
        this FacilityShopUnlockState state,
        BuildingSO building) =>
        state != null
        && building != null
        && FacilityShopService.CanEnterBasicPurchase(building)
        && state.UnlockBasicPurchaseById(building.id);

    public static bool IsBasicPurchaseUnlocked(
        this FacilityShopUnlockState state,
        BuildingSO building) =>
        state != null
        && building != null
        && state.IsBasicPurchaseUnlocked(building.id);

    public static bool MarkBlueprintAcquired(
        this FacilityShopUnlockState state,
        FacilityBlueprintSO blueprint) =>
        state != null
        && blueprint != null
        && state.MarkBlueprintAcquiredById(blueprint.id);

    public static bool IsBlueprintAcquired(
        this FacilityShopUnlockState state,
        FacilityBlueprintSO blueprint) =>
        state != null
        && blueprint != null
        && state.IsBlueprintAcquired(blueprint.id);
}

public static class FacilityShopService
{
    private const int RandomBuildingSlots = 3;
    private const int GuaranteedBlueprintSlots = 1;
    private const float RareOfferChance = 0.35f;

    public static IReadOnlyList<FacilityShopOffer> CreateDailyOffers(
        int day,
        IFacilityShopCatalog catalog,
        IRunVariableRuntimeReader runVariableReader,
        IBuildingCategoryDefinitionCatalog buildingCategoryCatalog)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        if (runVariableReader == null)
        {
            throw new ArgumentNullException(nameof(runVariableReader));
        }

        return CreateDailyOffers(
            day,
            catalog.Buildings,
            catalog.Blueprints,
            runVariableReader.GetInitialShopSeed(),
            runVariableReader.GetFacilityShopCostMultiplier,
            runVariableReader.GetBlueprintCostMultiplier,
            buildingCategoryCatalog,
            runVariableReader.GetStartingBlueprintCandidateIds());
    }

    public static IReadOnlyList<FacilityShopOffer> CreateDailyOffers(
        int day,
        IEnumerable<BuildingSO> buildings,
        IEnumerable<FacilityBlueprintSO> blueprints,
        int runShopSeed,
        Func<BuildingSO, float> buildingCostMultiplier,
        Func<FacilityBlueprintSO, float> blueprintCostMultiplier,
        IBuildingCategoryDefinitionCatalog buildingCategoryCatalog,
        IEnumerable<int> prioritizedBlueprintIds = null)
    {
        if (buildingCostMultiplier == null)
        {
            throw new ArgumentNullException(nameof(buildingCostMultiplier));
        }

        if (blueprintCostMultiplier == null)
        {
            throw new ArgumentNullException(nameof(blueprintCostMultiplier));
        }

        int safeDay = Mathf.Max(1, day);
        IRandomStream random = new DeterministicRandomSequence(
            7919 + (safeDay * 104729) + runShopSeed);
        List<FacilityShopOffer> offers = new List<FacilityShopOffer>();

        List<BuildingSO> buildingPool = buildings?
            .Where(IsDailyShopBuildingCandidate)
            .OrderBy((building) => random.NextFloat())
            .ToList()
            ?? new List<BuildingSO>();

        foreach (BuildingSO building in buildingPool.Take(RandomBuildingSlots))
        {
            offers.Add(CreateBuildingOffer(
                building,
                false,
                true,
                buildingCostMultiplier,
                buildingCategoryCatalog));
        }

        List<FacilityBlueprintSO> commonBlueprints = blueprints?
            .Where((blueprint) => blueprint != null && blueprint.rarity == FacilityShopRarity.Common)
            .ToList()
            ?? new List<FacilityBlueprintSO>();

        List<FacilityBlueprintSO> guaranteedBlueprints = new List<FacilityBlueprintSO>();
        if (safeDay == 1)
        {
            foreach (int candidateId in prioritizedBlueprintIds ?? Array.Empty<int>())
            {
                FacilityBlueprintSO candidate = commonBlueprints
                    .FirstOrDefault((blueprint) => blueprint.id == candidateId);
                if (candidate == null)
                {
                    continue;
                }

                guaranteedBlueprints.Add(candidate);
                commonBlueprints.Remove(candidate);
                break;
            }
        }

        guaranteedBlueprints.AddRange(commonBlueprints
            .OrderBy((blueprint) => random.NextFloat())
            .Take(Mathf.Max(0, GuaranteedBlueprintSlots - guaranteedBlueprints.Count)));
        foreach (FacilityBlueprintSO blueprint in guaranteedBlueprints)
        {
            offers.Add(CreateBlueprintOffer(blueprint, true, blueprintCostMultiplier));
        }

        List<FacilityBlueprintSO> rareBlueprints = blueprints?
            .Where((blueprint) => blueprint != null && blueprint.rarity != FacilityShopRarity.Common)
            .OrderBy((blueprint) => random.NextFloat())
            .ToList()
            ?? new List<FacilityBlueprintSO>();

        if (rareBlueprints.Count > 0 && random.NextFloat() <= RareOfferChance)
        {
            offers.Add(CreateBlueprintOffer(rareBlueprints[0], true, blueprintCostMultiplier));
        }

        return offers.Where((offer) => offer != null && offer.IsValid).ToList();
    }

    public static IReadOnlyList<FacilityShopOffer> CreateBasicPurchaseOffers(
        IFacilityShopCatalog catalog,
        FacilityShopUnlockState unlockState,
        IMetaProgressionRuntimeReader metaProgressionReader,
        IRunVariableRuntimeReader runVariableReader,
        IBuildingCategoryDefinitionCatalog buildingCategoryCatalog)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        if (metaProgressionReader == null)
        {
            throw new ArgumentNullException(nameof(metaProgressionReader));
        }

        if (runVariableReader == null)
        {
            throw new ArgumentNullException(nameof(runVariableReader));
        }

        return CreateBasicPurchaseOffers(
            catalog.Buildings,
            unlockState,
            metaProgressionReader.GetExpandedBasicPurchaseBuildingIds(catalog.Buildings),
            runVariableReader.GetFacilityShopCostMultiplier,
            buildingCategoryCatalog);
    }

    public static IReadOnlyList<FacilityShopOffer> CreateBasicPurchaseOffers(
        IEnumerable<BuildingSO> buildings,
        FacilityShopUnlockState unlockState,
        IEnumerable<int> expandedBasicPurchaseBuildingIds,
        Func<BuildingSO, float> buildingCostMultiplier,
        IBuildingCategoryDefinitionCatalog buildingCategoryCatalog)
    {
        if (buildingCostMultiplier == null)
        {
            throw new ArgumentNullException(nameof(buildingCostMultiplier));
        }

        if (unlockState == null)
        {
            return Array.Empty<FacilityShopOffer>();
        }

        List<BuildingSO> buildingList = buildings?
            .Where((building) => building != null)
            .ToList()
            ?? new List<BuildingSO>();
        HashSet<int> metaBasicPurchaseIds = expandedBasicPurchaseBuildingIds?.ToHashSet()
            ?? throw new ArgumentNullException(nameof(expandedBasicPurchaseBuildingIds));

        return buildingList
            .Where((building) => (unlockState.IsBasicPurchaseUnlocked(building) || metaBasicPurchaseIds.Contains(building.id))
                && CanEnterBasicPurchase(building))
            .OrderBy((building) => building.id)
            .Select((building) => CreateBuildingOffer(
                building,
                true,
                false,
                buildingCostMultiplier,
                buildingCategoryCatalog))
            .Where((offer) => offer != null && offer.IsValid)
            .ToList()
            ?? new List<FacilityShopOffer>();
    }

    public static bool TryPurchaseOffer(
        IGameMoneyAccount money,
        FacilityShopOffer offer,
        FacilityShopUnlockState unlockState,
        EconomyTransactionContext transactionContext,
        IDungeonDebugRuleQuery debugRules,
        out FacilityShopPurchaseResult result,
        Action<FacilityShopPurchaseResult> purchaseCompleted = null)
    {
        if (offer == null || !offer.IsValid)
        {
            result = new FacilityShopPurchaseResult(false, offer, 0, "상품 정보가 올바르지 않습니다");
            purchaseCompleted?.Invoke(result);
            return false;
        }

        if (money == null)
        {
            result = new FacilityShopPurchaseResult(false, offer, offer.Cost, "게임 자금 데이터가 없습니다");
            purchaseCompleted?.Invoke(result);
            return false;
        }

        if (!(debugRules ?? throw new ArgumentNullException(nameof(debugRules))).ShouldSkipCosts()
            && !money.CanSpend(offer.Cost))
        {
            result = new FacilityShopPurchaseResult(false, offer, offer.Cost, "자금 부족");
            purchaseCompleted?.Invoke(result);
            return false;
        }

        if (!debugRules.ShouldSkipCosts())
        {
            if (!money.TrySpend(offer.Cost, transactionContext, out string reason))
            {
                result = new FacilityShopPurchaseResult(
                    false,
                    offer,
                    offer.Cost,
                    reason);
                purchaseCompleted?.Invoke(result);
                return false;
            }
        }
        string message = offer.ApplyPurchase(unlockState);
        result = new FacilityShopPurchaseResult(true, offer, offer.Cost, message);
        purchaseCompleted?.Invoke(result);
        return true;
    }

    public static bool CanEnterBasicPurchase(BuildingSO building)
    {
        return building != null && GetBuildingStar(building) <= 2;
    }

    public static int GetBuildingStar(BuildingSO building)
    {
        if (building == null)
        {
            return 0;
        }

        if (building.TryGetAbility(out BuildingQualityAbility quality))
        {
            return Mathf.Clamp(quality.star, 1, 5);
        }

        if (building.Defense != null && building.Defense.IsDefenseFacility)
        {
            return Mathf.Max(1, building.Defense.star);
        }

        return 1;
    }

    public static string GetBuildingName(BuildingSO building)
    {
        if (building == null)
        {
            return "시설";
        }

        return string.IsNullOrWhiteSpace(building.objectName) ? building.name : building.objectName;
    }

    public static BuildingSO FindBuildingById(IFacilityShopCatalog catalog, int buildingId)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        if (buildingId < 0)
        {
            return null;
        }

        return catalog.FindBuildingById(buildingId);
    }

    private static FacilityShopOffer CreateBuildingOffer(
        BuildingSO building,
        bool basicPurchase,
        bool randomOffer,
        Func<BuildingSO, float> buildingCostMultiplier,
        IBuildingCategoryDefinitionCatalog buildingCategoryCatalog)
    {
        if (building == null)
        {
            return null;
        }

        FacilityShopRarity rarity = ResolveBuildingRarity(building);
        int cost = CalculateBuildingCost(
            building,
            basicPurchase,
            rarity,
            buildingCostMultiplier,
            buildingCategoryCatalog);
        return new FacilityBuildingOffer(building, cost, rarity, basicPurchase, randomOffer);
    }

    private static FacilityShopOffer CreateBlueprintOffer(
        FacilityBlueprintSO blueprint,
        bool randomOffer,
        Func<FacilityBlueprintSO, float> blueprintCostMultiplier)
    {
        if (blueprint == null)
        {
            return null;
        }

        return new FacilityBlueprintOffer(
            blueprint,
            Mathf.Max(0, Mathf.RoundToInt(blueprint.defaultCost * GetBlueprintCostMultiplier(blueprint, blueprintCostMultiplier))),
            blueprint.rarity,
            randomOffer);
    }

    private static bool IsDailyShopBuildingCandidate(BuildingSO building)
    {
        return building != null
            && !building.IsGridMovement
            && !building.IsWall
            && GetBuildingStar(building) <= 2;
    }

    private static FacilityShopRarity ResolveBuildingRarity(BuildingSO building)
    {
        int star = GetBuildingStar(building);
        if (star >= 2)
        {
            return FacilityShopRarity.Rare;
        }

        return FacilityShopRarity.Common;
    }

    private static int CalculateBuildingCost(
        BuildingSO building,
        bool basicPurchase,
        FacilityShopRarity rarity,
        Func<BuildingSO, float> buildingCostMultiplier,
        IBuildingCategoryDefinitionCatalog buildingCategoryCatalog)
    {
        int star = Mathf.Max(1, GetBuildingStar(building));
        int categoryWeight = (buildingCategoryCatalog
                ?? throw new ArgumentNullException(nameof(buildingCategoryCatalog)))
            .GetShopCostWeight(building.category);

        int rarityWeight = rarity switch
        {
            FacilityShopRarity.Rare => 80,
            FacilityShopRarity.Special => 160,
            _ => 0
        };
        int basicDiscount = basicPurchase ? 20 : 0;
        int baseCost = Mathf.Max(25, (star * categoryWeight) + rarityWeight - basicDiscount);
        return Mathf.Max(1, Mathf.RoundToInt(baseCost * GetBuildingCostMultiplier(building, buildingCostMultiplier)));
    }

    private static float GetBuildingCostMultiplier(BuildingSO building, Func<BuildingSO, float> buildingCostMultiplier)
    {
        return Mathf.Max(0.05f, buildingCostMultiplier(building));
    }

    private static float GetBlueprintCostMultiplier(
        FacilityBlueprintSO blueprint,
        Func<FacilityBlueprintSO, float> blueprintCostMultiplier)
    {
        return Mathf.Max(0.05f, blueprintCostMultiplier(blueprint));
    }

}
