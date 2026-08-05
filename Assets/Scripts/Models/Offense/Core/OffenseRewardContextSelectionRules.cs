public enum OffenseRewardContextSource
{
    None = 0,
    Runtime = 1,
    DebugOverride = 2,
    ShopRuntime = 3,
    ResearchRuntime = 4
}

public readonly struct OffenseRewardContextAvailabilitySnapshot
{
    public OffenseRewardContextAvailabilitySnapshot(
        bool hasDebugGameData,
        bool hasDebugWarehouses,
        bool hasDebugShopUnlockState,
        bool hasDebugResearchState,
        bool hasShopRuntime,
        bool hasResearchRuntime)
    {
        HasDebugGameData = hasDebugGameData;
        HasDebugWarehouses = hasDebugWarehouses;
        HasDebugShopUnlockState = hasDebugShopUnlockState;
        HasDebugResearchState = hasDebugResearchState;
        HasShopRuntime = hasShopRuntime;
        HasResearchRuntime = hasResearchRuntime;
    }

    public bool HasDebugGameData { get; }
    public bool HasDebugWarehouses { get; }
    public bool HasDebugShopUnlockState { get; }
    public bool HasDebugResearchState { get; }
    public bool HasShopRuntime { get; }
    public bool HasResearchRuntime { get; }
}

public readonly struct OffenseRewardContextSelection
{
    public OffenseRewardContextSelection(
        OffenseRewardContextSource gameDataSource,
        OffenseRewardContextSource warehouseSource,
        OffenseRewardContextSource shopUnlockStateSource,
        OffenseRewardContextSource researchStateSource,
        bool includeResearchRuntime,
        string expeditionId)
    {
        GameDataSource = gameDataSource;
        WarehouseSource = warehouseSource;
        ShopUnlockStateSource = shopUnlockStateSource;
        ResearchStateSource = researchStateSource;
        IncludeResearchRuntime = includeResearchRuntime;
        ExpeditionId = expeditionId;
    }

    public OffenseRewardContextSource GameDataSource { get; }
    public OffenseRewardContextSource WarehouseSource { get; }
    public OffenseRewardContextSource ShopUnlockStateSource { get; }
    public OffenseRewardContextSource ResearchStateSource { get; }
    public bool IncludeResearchRuntime { get; }
    public string ExpeditionId { get; }
}

public static class OffenseRewardContextSelectionRules
{
    public static OffenseRewardContextSelection Select(
        OffenseRewardContextAvailabilitySnapshot availability,
        string expeditionId)
    {
        return new OffenseRewardContextSelection(
            availability.HasDebugGameData
                ? OffenseRewardContextSource.DebugOverride
                : OffenseRewardContextSource.Runtime,
            availability.HasDebugWarehouses
                ? OffenseRewardContextSource.DebugOverride
                : OffenseRewardContextSource.Runtime,
            SelectShopUnlockStateSource(availability),
            availability.HasDebugResearchState
                ? OffenseRewardContextSource.DebugOverride
                : availability.HasResearchRuntime
                    ? OffenseRewardContextSource.ResearchRuntime
                    : OffenseRewardContextSource.None,
            includeResearchRuntime: !availability.HasDebugResearchState
                && availability.HasResearchRuntime,
            expeditionId: expeditionId?.Trim() ?? string.Empty);
    }

    private static OffenseRewardContextSource SelectShopUnlockStateSource(
        OffenseRewardContextAvailabilitySnapshot availability)
    {
        if (availability.HasDebugShopUnlockState)
        {
            return OffenseRewardContextSource.DebugOverride;
        }

        if (availability.HasShopRuntime)
        {
            return OffenseRewardContextSource.ShopRuntime;
        }

        return availability.HasResearchRuntime
            ? OffenseRewardContextSource.ResearchRuntime
            : OffenseRewardContextSource.None;
    }
}
