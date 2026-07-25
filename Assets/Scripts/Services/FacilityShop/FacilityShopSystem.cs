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

public enum FacilityShopRarity
{
    Common,
    Rare,
    Special
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
        Building.unlocked = true;
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

public class FacilityShopUnlockState
{
    private readonly HashSet<int> basicPurchaseBuildingIds = new HashSet<int>();
    private readonly HashSet<int> acquiredBlueprintIds = new HashSet<int>();

    public IReadOnlyCollection<int> BasicPurchaseBuildingIds =>
        Array.AsReadOnly(basicPurchaseBuildingIds.OrderBy((id) => id).ToArray());

    public IReadOnlyCollection<int> AcquiredBlueprintIds =>
        Array.AsReadOnly(acquiredBlueprintIds.OrderBy((id) => id).ToArray());

    public bool UnlockBasicPurchase(BuildingSO building)
    {
        if (building == null || !FacilityShopService.CanEnterBasicPurchase(building))
        {
            return false;
        }

        return basicPurchaseBuildingIds.Add(building.id);
    }

    public bool UnlockBasicPurchaseById(int buildingId)
    {
        if (buildingId < 0)
        {
            return false;
        }

        return basicPurchaseBuildingIds.Add(buildingId);
    }

    public bool IsBasicPurchaseUnlocked(BuildingSO building)
    {
        return building != null && basicPurchaseBuildingIds.Contains(building.id);
    }

    public bool MarkBlueprintAcquired(FacilityBlueprintSO blueprint)
    {
        if (blueprint == null)
        {
            return false;
        }

        return acquiredBlueprintIds.Add(blueprint.id);
    }

    public bool IsBlueprintAcquired(FacilityBlueprintSO blueprint)
    {
        return blueprint != null && acquiredBlueprintIds.Contains(blueprint.id);
    }

    public void Restore(IEnumerable<int> basicBuildingIds, IEnumerable<int> blueprintIds)
    {
        basicPurchaseBuildingIds.Clear();
        acquiredBlueprintIds.Clear();

        foreach (int id in basicBuildingIds ?? Array.Empty<int>())
        {
            if (id >= 0)
            {
                basicPurchaseBuildingIds.Add(id);
            }
        }

        foreach (int id in blueprintIds ?? Array.Empty<int>())
        {
            if (id >= 0)
            {
                acquiredBlueprintIds.Add(id);
            }
        }
    }
}

public static class FacilityShopService
{
    private const int RandomBuildingSlots = 3;
    private const int GuaranteedBlueprintSlots = 1;
    private const float RareOfferChance = 0.35f;

    public static IReadOnlyList<FacilityShopOffer> CreateDailyOffers(
        int day,
        IFacilityShopCatalog catalog,
        IRunVariableRuntimeReader runVariableReader)
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
            runVariableReader.GetStartingBlueprintCandidateIds());
    }

    public static IReadOnlyList<FacilityShopOffer> CreateDailyOffers(
        int day,
        IEnumerable<BuildingSO> buildings,
        IEnumerable<FacilityBlueprintSO> blueprints,
        int runShopSeed,
        Func<BuildingSO, float> buildingCostMultiplier,
        Func<FacilityBlueprintSO, float> blueprintCostMultiplier,
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
        System.Random random = new System.Random(7919 + (safeDay * 104729) + runShopSeed);
        List<FacilityShopOffer> offers = new List<FacilityShopOffer>();

        List<BuildingSO> buildingPool = buildings?
            .Where(IsDailyShopBuildingCandidate)
            .OrderBy((building) => random.NextDouble())
            .ToList()
            ?? new List<BuildingSO>();

        foreach (BuildingSO building in buildingPool.Take(RandomBuildingSlots))
        {
            offers.Add(CreateBuildingOffer(building, false, true, buildingCostMultiplier));
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
            .OrderBy((blueprint) => random.NextDouble())
            .Take(Mathf.Max(0, GuaranteedBlueprintSlots - guaranteedBlueprints.Count)));
        foreach (FacilityBlueprintSO blueprint in guaranteedBlueprints)
        {
            offers.Add(CreateBlueprintOffer(blueprint, true, blueprintCostMultiplier));
        }

        List<FacilityBlueprintSO> rareBlueprints = blueprints?
            .Where((blueprint) => blueprint != null && blueprint.rarity != FacilityShopRarity.Common)
            .OrderBy((blueprint) => random.NextDouble())
            .ToList()
            ?? new List<FacilityBlueprintSO>();

        if (rareBlueprints.Count > 0 && random.NextDouble() <= RareOfferChance)
        {
            offers.Add(CreateBlueprintOffer(rareBlueprints[0], true, blueprintCostMultiplier));
        }

        return offers.Where((offer) => offer != null && offer.IsValid).ToList();
    }

    public static IReadOnlyList<FacilityShopOffer> CreateBasicPurchaseOffers(
        IFacilityShopCatalog catalog,
        FacilityShopUnlockState unlockState,
        IMetaProgressionRuntimeReader metaProgressionReader,
        IRunVariableRuntimeReader runVariableReader)
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
            runVariableReader.GetFacilityShopCostMultiplier);
    }

    public static IReadOnlyList<FacilityShopOffer> CreateBasicPurchaseOffers(
        IEnumerable<BuildingSO> buildings,
        FacilityShopUnlockState unlockState,
        IEnumerable<int> expandedBasicPurchaseBuildingIds,
        Func<BuildingSO, float> buildingCostMultiplier)
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
            .Select((building) => CreateBuildingOffer(building, true, false, buildingCostMultiplier))
            .Where((offer) => offer != null && offer.IsValid)
            .ToList()
            ?? new List<FacilityShopOffer>();
    }

    public static bool TryPurchaseOffer(
        GameData gameData,
        FacilityShopOffer offer,
        FacilityShopUnlockState unlockState,
        out FacilityShopPurchaseResult result,
        Action<FacilityShopPurchaseResult> purchaseCompleted = null)
    {
        if (offer == null || !offer.IsValid)
        {
            result = new FacilityShopPurchaseResult(false, offer, 0, "상품 정보가 올바르지 않습니다");
            purchaseCompleted?.Invoke(result);
            return false;
        }

        if (gameData == null || gameData.holdingMoney == null)
        {
            result = new FacilityShopPurchaseResult(false, offer, offer.Cost, "게임 자금 데이터가 없습니다");
            purchaseCompleted?.Invoke(result);
            return false;
        }

        if (!DungeonDebugRuntimeRules.ShouldSkipCosts()
            && gameData.holdingMoney.Value < offer.Cost)
        {
            result = new FacilityShopPurchaseResult(false, offer, offer.Cost, "자금 부족");
            purchaseCompleted?.Invoke(result);
            return false;
        }

        if (!DungeonDebugRuntimeRules.ShouldSkipCosts())
        {
            gameData.holdingMoney.Value -= offer.Cost;
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
        Func<BuildingSO, float> buildingCostMultiplier)
    {
        if (building == null)
        {
            return null;
        }

        FacilityShopRarity rarity = ResolveBuildingRarity(building);
        int cost = CalculateBuildingCost(building, basicPurchase, rarity, buildingCostMultiplier);
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
        Func<BuildingSO, float> buildingCostMultiplier)
    {
        int star = Mathf.Max(1, GetBuildingStar(building));
        int categoryWeight = BuildingCategoryCatalog.GetShopCostWeight(building.category);

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

public class DailyFacilityShopRuntime : MonoBehaviour
{
    [SerializeField] private bool raiseAlertOnRefresh = true;

    private readonly List<FacilityShopOffer> currentDailyOffers = new List<FacilityShopOffer>();
    private IReadOnlyList<FacilityShopOffer> currentDailyOffersView;
    private readonly FacilityShopUnlockState unlockState = new FacilityShopUnlockState();
    private int currentOfferDay = 1;
    private IFacilityShopCatalog facilityShopCatalog;
    private IRunVariableRuntimeReader runVariableReader;
    private IMetaProgressionRuntimeReader metaProgressionReader;
    private IGameEventBus gameEventBus;
    private IDisposable runStartVariablesSubscription;
    private IDisposable operatingDayEndedSubscription;

    public event Action<int, IReadOnlyList<FacilityShopOffer>, IReadOnlyList<FacilityShopOffer>> Refreshed;

    public IReadOnlyList<FacilityShopOffer> CurrentDailyOffers =>
        currentDailyOffersView ??= ReadOnlyView.List(currentDailyOffers);
    public IReadOnlyList<FacilityShopOffer> CurrentBasicPurchaseOffers =>
        FacilityShopService.CreateBasicPurchaseOffers(
            ResolveFacilityShopCatalog(),
            unlockState,
            ResolveMetaProgressionReader(),
            ResolveRunVariableReader());
    public FacilityShopUnlockState UnlockState => unlockState;
    public int CurrentOfferDay => currentOfferDay;

    [Inject]
    public void ConstructDailyFacilityShopRuntime(
        IFacilityShopCatalog facilityShopCatalog,
        IRunVariableRuntimeReader runVariableReader,
        IMetaProgressionRuntimeReader metaProgressionReader,
        IGameEventBus gameEventBus)
    {
        this.facilityShopCatalog = facilityShopCatalog
            ?? throw new ArgumentNullException(nameof(facilityShopCatalog));
        this.runVariableReader = runVariableReader
            ?? throw new ArgumentNullException(nameof(runVariableReader));
        this.metaProgressionReader = metaProgressionReader
            ?? throw new ArgumentNullException(nameof(metaProgressionReader));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
    }

    private void Start()
    {
        if (currentDailyOffers.Count == 0)
        {
            Refresh(1, false);
        }
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        Refresh(Mathf.Max(1, eventType.day + 1), raiseAlertOnRefresh);
    }

    public void OnTriggerEvent(RunStartVariablesSelectedEvent eventType)
    {
        if (currentOfferDay <= 1)
        {
            Refresh(1, false);
        }
    }

    public void Refresh(int day, bool raiseAlert)
    {
        currentOfferDay = Mathf.Max(1, day);
        currentDailyOffers.Clear();
        currentDailyOffers.AddRange(FacilityShopService.CreateDailyOffers(
            currentOfferDay,
            ResolveFacilityShopCatalog(),
            ResolveRunVariableReader()));

        IReadOnlyList<FacilityShopOffer> basicPurchaseOffers = CurrentBasicPurchaseOffers;
        Refreshed?.Invoke(
            currentOfferDay,
            EventPayloadSnapshot.Copy(currentDailyOffers),
            EventPayloadSnapshot.Copy(basicPurchaseOffers));

        if (raiseAlert)
        {
            gameEventBus.RaiseAlert(
                "시설 상점 갱신",
                FormatOfferList(currentDailyOffers, basicPurchaseOffers),
                EventAlertImportance.Medium,
                "상점");
        }
    }

    public void RestoreState(
        int offerDay,
        IEnumerable<int> basicBuildingIds,
        IEnumerable<int> acquiredBlueprintIds)
    {
        unlockState.Restore(basicBuildingIds, acquiredBlueprintIds);
        Refresh(Mathf.Max(1, offerDay), false);
    }

    public bool TryPurchaseDailyOffer(int index, GameData gameData, out FacilityShopPurchaseResult result)
    {
        if (index < 0 || index >= currentDailyOffers.Count)
        {
            result = new FacilityShopPurchaseResult(false, null, 0, "선택한 상품이 없습니다");
            PublishPurchase(result);
            return false;
        }

        return FacilityShopService.TryPurchaseOffer(
            gameData,
            currentDailyOffers[index],
            unlockState,
            out result,
            PublishPurchase);
    }

    public bool TryPurchaseBasicOffer(int index, GameData gameData, out FacilityShopPurchaseResult result)
    {
        IReadOnlyList<FacilityShopOffer> offers = CurrentBasicPurchaseOffers;
        if (index < 0 || index >= offers.Count)
        {
            result = new FacilityShopPurchaseResult(false, null, 0, "선택한 기본 구매 상품이 없습니다");
            PublishPurchase(result);
            return false;
        }

        return FacilityShopService.TryPurchaseOffer(
            gameData,
            offers[index],
            unlockState,
            out result,
            PublishPurchase);
    }

    private void PublishPurchase(FacilityShopPurchaseResult result)
    {
        gameEventBus.Publish(new FacilityShopPurchasedEvent(result));
        if (result.success
            && string.Equals(
                result.offerTypeId,
                FacilityShopOfferTypeIds.Blueprint,
                StringComparison.Ordinal))
        {
            gameEventBus.RaiseBlueprintAcquired(
                $"{result.offer?.displayName ?? "설계도"} 획득");
        }
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        runStartVariablesSubscription?.Dispose();
        runStartVariablesSubscription = null;
        operatingDayEndedSubscription?.Dispose();
        operatingDayEndedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        runStartVariablesSubscription ??=
            gameEventBus.Subscribe<RunStartVariablesSelectedEvent>(OnTriggerEvent);
        operatingDayEndedSubscription ??=
            gameEventBus.Subscribe<OperatingDayEndedEvent>(OnTriggerEvent);
    }

    private static string FormatOfferList(
        IEnumerable<FacilityShopOffer> dailyOffers,
        IEnumerable<FacilityShopOffer> basicPurchaseOffers)
    {
        List<string> lines = new List<string> { "일일 상품:" };
        List<string> dailyRows = dailyOffers?
            .Where((offer) => offer != null && offer.IsValid)
            .Select((offer) => $"- {offer.ToSnapshot().ToSummaryText()}")
            .ToList()
            ?? new List<string>();
        if (dailyRows.Count > 0)
        {
            lines.AddRange(dailyRows);
        }
        else
        {
            lines.Add("- 없음");
        }

        List<string> basicRows = basicPurchaseOffers?
            .Where((offer) => offer != null && offer.IsValid)
            .Select((offer) => $"- {offer.ToSnapshot().ToSummaryText()}")
            .ToList()
            ?? new List<string>();
        lines.Add(string.Empty);
        lines.Add("기본 구매:");
        if (basicRows.Count > 0)
        {
            lines.AddRange(basicRows);
        }
        else
        {
            lines.Add("- 없음");
        }
        return string.Join("\n", lines);
    }

    private IFacilityShopCatalog ResolveFacilityShopCatalog()
    {
        return facilityShopCatalog
            ?? throw new InvalidOperationException($"{nameof(DailyFacilityShopRuntime)} requires {nameof(IFacilityShopCatalog)} injection.");
    }

    private IRunVariableRuntimeReader ResolveRunVariableReader()
    {
        return runVariableReader
            ?? throw new InvalidOperationException($"{nameof(DailyFacilityShopRuntime)} requires {nameof(IRunVariableRuntimeReader)} injection.");
    }

    private IMetaProgressionRuntimeReader ResolveMetaProgressionReader()
    {
        return metaProgressionReader
            ?? throw new InvalidOperationException($"{nameof(DailyFacilityShopRuntime)} requires {nameof(IMetaProgressionRuntimeReader)} injection.");
    }
}
