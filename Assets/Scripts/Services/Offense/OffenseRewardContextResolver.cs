using System.Collections.Generic;
using System;

public sealed class OffenseRewardDebugContext
{
    public GameData gameData;
    public IEnumerable<IWarehouseFacility> warehouses;
    public FacilityShopUnlockState shopUnlockState;
    public BlueprintResearchState researchState;

    public void Clear()
    {
        gameData = null;
        warehouses = null;
        shopUnlockState = null;
        researchState = null;
    }
}

public interface IOffenseRewardContextBuilder
{
    OffenseRewardContext Create(
        OffenseTargetDefinition target,
        OffenseRewardState state,
        OffenseRewardDebugContext debugContext);
}

public sealed class OffenseRewardContextBuilder : IOffenseRewardContextBuilder
{
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly IDailyFacilityShopRuntimeProvider shopProvider;
    private readonly IGameDataProvider gameDataProvider;
    private readonly IWarehouseWorldQuery warehouseWorld;

    public OffenseRewardContextBuilder(
        IBlueprintResearchRuntimeProvider researchProvider,
        IDailyFacilityShopRuntimeProvider shopProvider,
        IGameDataProvider gameDataProvider,
        IWarehouseWorldQuery warehouseWorld)
    {
        this.researchProvider = researchProvider
            ?? throw new ArgumentNullException(nameof(researchProvider));
        this.shopProvider = shopProvider
            ?? throw new ArgumentNullException(nameof(shopProvider));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.warehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
    }

    public OffenseRewardContext Create(
        OffenseTargetDefinition target,
        OffenseRewardState state,
        OffenseRewardDebugContext debugContext)
    {
        researchProvider.TryGetRuntime(out BlueprintResearchRuntime researchRuntime);
        shopProvider.TryGetRuntime(out DailyFacilityShopRuntime shopRuntime);
        gameDataProvider.TryGetGameData(out GameData runtimeGameData);
        GameData gameData = debugContext?.gameData != null
            ? debugContext.gameData
            : runtimeGameData;
        FacilityShopUnlockState shopUnlockState = debugContext?.shopUnlockState != null
            ? debugContext.shopUnlockState
            : shopRuntime != null
                ? shopRuntime.UnlockState
                : researchRuntime != null
                    ? researchRuntime.ShopUnlockState
                    : null;

        return new OffenseRewardContext
        {
            gameData = gameData,
            warehouses = debugContext?.warehouses ?? warehouseWorld.Warehouses,
            shopUnlockState = shopUnlockState,
            researchState = debugContext?.researchState ?? researchRuntime?.State,
            researchRuntime = debugContext?.researchState == null ? researchRuntime : null,
            rewardState = state,
            target = target
        };
    }
}
