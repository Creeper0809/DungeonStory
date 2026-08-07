using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

internal sealed class SurvivalFoodStockRuntime
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly IStockQuery stockQuery;
    private IReadOnlyList<WorldItemStackSnapshot> cachedItemStacks =
        Array.Empty<WorldItemStackSnapshot>();
    private int cachedItemStackVersion = -1;

    public SurvivalFoodStockRuntime(
        IGridSystemProvider gridSystemProvider,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldItemStackRuntime itemStackRuntime,
        IItemDefinitionCatalog itemCatalog,
        IStockQuery stockQuery)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.itemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        this.stockQuery = stockQuery
            ?? throw new ArgumentNullException(nameof(stockQuery));
    }

    public int CountStoredStock(StockCategory category)
    {
        int total = 0;
        foreach (IWarehouseFacility warehouse in GetWarehouses())
        {
            if (warehouse is not BuildableObject building)
            {
                continue;
            }

            total = SaturatingAdd(
                total,
                stockQuery.GetWarehouseQuantity(
                    building.RequirePersistentInstanceId(),
                    category));
        }

        return total;
    }

    public int CountLooseStock(StockCategory category)
    {
        int total = 0;
        IReadOnlyList<WorldItemStackSnapshot> stacks = GetCachedItemStacks();
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (IsUsableCategoryStack(stack, category)
                && stack.State != WorldItemStackState.Stored
                && stack.State != WorldItemStackState.Carried)
            {
                total = SaturatingAdd(total, stack.Quantity);
            }
        }

        return total;
    }

    public int WithdrawStock(StockCategory category, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        HashSet<string> warehouseDestinations = GetWarehouses()
            .Select(WarehouseStorageIdentity.RequireDestinationId)
            .ToHashSet(StringComparer.Ordinal);
        int remaining = amount;
        foreach (WorldItemStackSnapshot stack in GetCachedItemStacks()
                     .Where(stack => IsUsableCategoryStack(stack, category)
                         && stack.State == WorldItemStackState.Stored
                         && warehouseDestinations.Contains(
                             string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId)
                                 ? stack.DestinationId
                                 : stack.SourceStorageDestinationId))
                     .OrderBy(stack => stack.ItemId, StringComparer.Ordinal)
                     .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (remaining <= 0)
            {
                break;
            }

            int requested = Math.Min(remaining, stack.Quantity);
            if (itemStackRuntime.TryConsumeStackQuantity(
                    stack.StackId,
                    requested,
                    out WorldItemStackSnapshot consumed))
            {
                remaining -= Math.Min(requested, consumed?.Quantity ?? requested);
            }
        }

        return amount - remaining;
    }

    public bool TryConsumeTreatmentMaterial(out bool usedBloodSubstitute)
    {
        usedBloodSubstitute = false;
        if (WithdrawStock(StockCategory.Medicine, 1) > 0)
        {
            return true;
        }

        if (WithdrawStock(StockCategory.Biological, 1) <= 0)
        {
            return false;
        }

        usedBloodSubstitute = true;
        return true;
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetCachedItemStacks()
    {
        if (cachedItemStackVersion == itemStackRuntime.ItemStackVersion)
        {
            return cachedItemStacks;
        }

        cachedItemStackVersion = itemStackRuntime.ItemStackVersion;
        cachedItemStacks = itemStackRuntime.GetAllStacks();
        return cachedItemStacks;
    }

    private IEnumerable<IWarehouseFacility> GetWarehouses()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return Array.Empty<IWarehouseFacility>();
        }

        IReadOnlyList<IWarehouseFacility> registered = worldRegistry.Warehouses;
        return registered.Count > 0
            ? registered.Where(warehouse => IsWarehouseOnGrid(warehouse, grid)).ToArray()
            : grid.FindAllOccupants(null)
                .OfType<IWarehouseFacility>()
                .Where(warehouse => IsWarehouseOnGrid(warehouse, grid))
                .ToArray();
    }

    private bool IsUsableCategoryStack(
        WorldItemStackSnapshot stack,
        StockCategory category)
    {
        return stack != null
            && stack.Quantity > 0
            && !stack.Forbidden
            && stack.Contamination <= 0.01f
            && itemCatalog.TryGet((ItemDefinitionId)stack.ItemId, out ItemDefinitionSO definition)
            && definition.StockCategory == category;
    }

    private static bool IsWarehouseOnGrid(IWarehouseFacility warehouse, Grid grid)
    {
        if (warehouse == null)
        {
            return false;
        }

        BuildableObject building = warehouse as BuildableObject;
        return building == null || building.Grid == grid;
    }

    private static int SaturatingAdd(int current, int value)
    {
        long total = (long)Math.Max(0, current) + Math.Max(0, value);
        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }
}

public sealed class SurvivalFoodRuntimeDependencies
{
    public SurvivalFoodRuntimeDependencies(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IItemDefinitionCatalog itemCatalog,
        IStockQuery stockQuery,
        IClimateQuery climate)
    {
        GridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        ItemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        ItemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        StockQuery = stockQuery
            ?? throw new ArgumentNullException(nameof(stockQuery));
        Climate = climate ?? throw new ArgumentNullException(nameof(climate));
    }

    public IGridSystemProvider GridSystemProvider { get; }

    public IWorldItemStackRuntime ItemStackRuntime { get; }

    public IItemDefinitionCatalog ItemCatalog { get; }

    public IStockQuery StockQuery { get; }

    public IClimateQuery Climate { get; }
}

internal sealed class SurvivalFoodOverviewCache
{
    private const float RefreshIntervalSeconds = 0.5f;

    private readonly IGameClock gameClock;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private SurvivalFoodOverview cachedOverview;
    private int cachedItemVersion = -1;
    private int cachedCharacterVersion = -1;
    private int cachedBuildingVersion = -1;
    private float cachedTime = float.NegativeInfinity;
    private bool hasCachedOverview;

    public SurvivalFoodOverviewCache(
        IGameClock gameClock,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldItemStackRuntime itemStackRuntime)
    {
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
    }

    public SurvivalFoodOverview GetOrCreate(Func<SurvivalFoodOverview> factory)
    {
        _ = factory ?? throw new ArgumentNullException(nameof(factory));
        int itemVersion = itemStackRuntime.ItemStackVersion;
        int characterVersion = worldRegistry.CharacterVersion;
        int buildingVersion = worldRegistry.BuildingVersion;
        float now = gameClock.Time;
        bool refreshIntervalValid =
            now - cachedTime <= RefreshIntervalSeconds;
        if (hasCachedOverview
            && refreshIntervalValid
            && cachedItemVersion == itemVersion
            && cachedCharacterVersion == characterVersion
            && cachedBuildingVersion == buildingVersion)
        {
            return cachedOverview;
        }

        SurvivalFoodOverview overview = factory();
        cachedOverview = overview;
        cachedItemVersion = itemStackRuntime.ItemStackVersion;
        cachedCharacterVersion = worldRegistry.CharacterVersion;
        cachedBuildingVersion = worldRegistry.BuildingVersion;
        cachedTime = now;
        hasCachedOverview = true;

        return overview;
    }

    public void Invalidate()
    {
        hasCachedOverview = false;
        cachedTime = float.NegativeInfinity;
    }
}
